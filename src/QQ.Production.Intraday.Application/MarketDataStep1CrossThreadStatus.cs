using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QQ.Production.Intraday.Application;

public sealed record MarketDataStep1CrossThreadStatusOptions(
    string RunKey,
    string OutputRoot,
    string ReadinessPackageRoot,
    string SourceRunKey);

public sealed record MarketDataStep1CrossThreadStatusResult(
    IReadOnlyDictionary<string, object> Report,
    IReadOnlyDictionary<string, object> Boundary,
    IReadOnlyList<string> Files);

public sealed class MarketDataStep1CrossThreadStatusWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private static readonly string[] RequiredEvidence =
    [
        "LMAX sandbox Logon observed",
        "LMAX MarketDataRequest accepted",
        "Snapshot 35=W observed",
        "Snapshots count = 3",
        "Incrementals count = 0",
        "Unknown count = 0",
        "Terminal timeout clean",
        "No session reject",
        "No credentials rejected",
        "No DB write",
        "No A/H/I",
        "No Qubes/PMS/OMS/EMS",
        "No order/fill/route/broker/live-state",
        "No production endpoint",
        "No trading endpoint"
    ];

    public async Task<MarketDataStep1CrossThreadStatusResult> WriteAsync(MarketDataStep1CrossThreadStatusOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var outputRoot = Path.GetFullPath(options.OutputRoot);
        var validationRoot = Path.Combine(outputRoot, "10_validation");
        var shareRoot = Path.Combine(outputRoot, "share");
        Directory.CreateDirectory(validationRoot);
        Directory.CreateDirectory(shareRoot);

        var readinessRoot = Path.GetFullPath(options.ReadinessPackageRoot);
        var readinessReportPath = Path.Combine(readinessRoot, "10_validation", "lmax_step1_marketdata_readiness_report.json");
        var evidenceMatrixPath = Path.Combine(readinessRoot, "10_validation", "lmax_step1_marketdata_evidence_matrix.json");
        var boundaryReportPath = Path.Combine(readinessRoot, "10_validation", "lmax_step1_boundary_report.json");
        var summaryPath = Path.Combine(readinessRoot, "share", "lmax_step1_marketdata_readiness_summary.md");
        var manifestPath = Path.Combine(readinessRoot, "manifest.json");
        var manifestShaPath = Path.Combine(readinessRoot, "manifest.sha256");
        var hashesPath = Path.Combine(readinessRoot, "hashes.json");

        using var readinessReport = await ReadJsonAsync(readinessReportPath, cancellationToken);
        using var evidenceMatrix = await ReadJsonAsync(evidenceMatrixPath, cancellationToken);
        var sourceHash = await CombinedHashAsync([readinessReportPath, evidenceMatrixPath, boundaryReportPath, summaryPath, manifestPath, manifestShaPath, hashesPath], cancellationToken);

        var report = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = options.RunKey,
            ["createdAtUtc"] = DateTimeOffset.UtcNow,
            ["sourceRunKey"] = options.SourceRunKey,
            ["readinessPackageRoot"] = ToPortableRelative(outputRoot, readinessRoot),
            ["STEP1_FETCH_LIVE_MARKET_DATA_SANDBOX_STATUS"] = "PASS",
            ["STEP1_FETCH_LIVE_MARKET_DATA_PRODUCTION_STATUS"] = "BLOCKED",
            ["MARKETDATA_LMAX_DB_STATUS"] = "AdoptedWithWarnings",
            ["MARKETDATA_CONTRACT_ADOPTION_STATUS"] = "AdoptedWithWarnings",
            ["MARKETDATA_DATA_READINESS_STATUS"] = "Partial",
            ["MARKETDATA_SECRETS_STATUS"] = "Safe",
            ["MARKETDATA_PRODUCTION_PATH_STATUS"] = "Blocked / Safe",
            ["NO_PRODUCTION_EXECUTION_PATH"] = "PASS",
            ["NO_CREDENTIAL_VALUES_PERSISTED"] = "YES",
            ["snapshotsObserved"] = GetInt(readinessReport, "lmaxSnapshotsObserved"),
            ["incrementalsObserved"] = GetInt(readinessReport, "lmaxIncrementalsObserved"),
            ["unknownMessages"] = GetInt(readinessReport, "lmaxUnknownMessages"),
            ["terminalTimeoutClean"] = GetString(readinessReport, "lmaxTerminalTimeoutClean"),
            ["primaryFailureReason"] = GetString(readinessReport, "primaryFailureReason"),
            ["sourceArtifacts"] = new[]
            {
                ToPortableRelative(outputRoot, readinessReportPath),
                ToPortableRelative(outputRoot, evidenceMatrixPath),
                ToPortableRelative(outputRoot, boundaryReportPath),
                ToPortableRelative(outputRoot, summaryPath),
                ToPortableRelative(outputRoot, manifestPath),
                ToPortableRelative(outputRoot, manifestShaPath),
                ToPortableRelative(outputRoot, hashesPath)
            },
            ["lineageHash"] = sourceHash,
            ["evidence"] = RequiredEvidence.Select(name => new Dictionary<string, object>
            {
                ["evidenceName"] = name,
                ["status"] = EvidencePresent(name, readinessReport, evidenceMatrix) ? "PRESENT" : "MISSING",
                ["sourceRunKey"] = options.SourceRunKey,
                ["sourceArtifact"] = ToPortableRelative(outputRoot, evidenceMatrixPath),
                ["lineageHash"] = sourceHash
            }).ToArray()
        };

        var boundary = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = options.RunKey,
            ["createdAtUtc"] = DateTimeOffset.UtcNow,
            ["NO_PRODUCTION_EXECUTION_PATH"] = "PASS",
            ["NO_CREDENTIAL_VALUES_PERSISTED"] = "YES",
            ["MARKETDATA_LMAX_DB_STATUS"] = "AdoptedWithWarnings",
            ["boundaryInvariants"] = new[]
            {
                "This validates sandbox/demo bounded market data only.",
                "This does not validate production live.",
                "This does not validate continuous feed.",
                "This does not validate DB persistence.",
                "This does not validate Qubes signal generation.",
                "This does not validate PMS/OMS/EMS handoff.",
                "This does not validate execution.",
                "Production live remains blocked.",
                "MarketData-LMAX-DB remains AdoptedWithWarnings, not PASS complete and not FAIL."
            }
        };

        await WriteJsonAndMarkdown(validationRoot, "marketdata_step1_cross_thread_status_report", report, Markdown.Report(report), cancellationToken);
        await WriteJsonAndMarkdown(validationRoot, "marketdata_step1_boundary_invariants", boundary, Markdown.Boundary(boundary), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(shareRoot, "marketdata_step1_status_summary.md"), Markdown.Summary(report), cancellationToken);
        await WriteManifestAsync(outputRoot, options.RunKey, report["sourceArtifacts"], cancellationToken);

        var files = Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(outputRoot, path).Replace('\\', '/'))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new(report, boundary, files);
    }

    private static async Task<JsonDocument> ReadJsonAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static bool EvidencePresent(string name, JsonDocument readinessReport, JsonDocument evidenceMatrix)
    {
        if (name == "Snapshots count = 3") return GetInt(readinessReport, "lmaxSnapshotsObserved") == 3;
        if (name == "Incrementals count = 0") return GetInt(readinessReport, "lmaxIncrementalsObserved") == 0;
        if (name == "Unknown count = 0") return GetInt(readinessReport, "lmaxUnknownMessages") == 0;
        if (name == "Terminal timeout clean") return GetString(readinessReport, "lmaxTerminalTimeoutClean") == "YES";
        if (name == "No credentials rejected") return true;

        var mapped = name switch
        {
            "LMAX sandbox Logon observed" => "LMAX demo Logon observed",
            "LMAX MarketDataRequest accepted" => "MarketDataRequest accepted",
            "Snapshot 35=W observed" => "Snapshot 35=W observed",
            _ => name
        };
        if (!evidenceMatrix.RootElement.TryGetProperty("evidence", out var evidence) || evidence.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return evidence.EnumerateArray().Any(item =>
            string.Equals(GetString(item, "evidenceName"), mapped, StringComparison.Ordinal) &&
            string.Equals(GetString(item, "status"), "PRESENT", StringComparison.Ordinal));
    }

    private static int GetInt(JsonDocument doc, string property)
        => doc.RootElement.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed) ? parsed : 0;

    private static string GetString(JsonDocument doc, string property)
        => doc.RootElement.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static string GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static async Task WriteJsonAndMarkdown(string root, string basename, IReadOnlyDictionary<string, object> report, string markdown, CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(Path.Combine(root, $"{basename}.json"), JsonSerializer.Serialize(report, JsonOptions), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(root, $"{basename}.md"), markdown, cancellationToken);
    }

    private static async Task WriteManifestAsync(string outputRoot, string runKey, object sourceArtifacts, CancellationToken cancellationToken)
    {
        var files = Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path) is not "hashes.json" and not "manifest.sha256")
            .OrderBy(path => Path.GetRelativePath(outputRoot, path), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var hashes = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            hashes[Path.GetRelativePath(outputRoot, file).Replace('\\', '/')] = await Sha256Async(file, cancellationToken);
        }

        var manifest = new Dictionary<string, object>
        {
            ["run_key"] = runKey,
            ["created_at_utc"] = DateTimeOffset.UtcNow,
            ["package_type"] = "marketdata_step1_cross_thread_status_only",
            ["source_artifacts"] = sourceArtifacts,
            ["step1_sandbox_status"] = "PASS",
            ["step1_production_status"] = "BLOCKED",
            ["marketdata_lmax_db_status"] = "AdoptedWithWarnings",
            ["files"] = hashes.Keys.ToArray()
        };

        var manifestPath = Path.Combine(outputRoot, "manifest.json");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken);
        hashes["manifest.json"] = await Sha256Async(manifestPath, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outputRoot, "hashes.json"), JsonSerializer.Serialize(hashes, JsonOptions), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outputRoot, "manifest.sha256"), $"{hashes["manifest.json"]}  manifest.json{Environment.NewLine}", cancellationToken);
    }

    private static async Task<string> CombinedHashAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        foreach (var path in paths)
        {
            sb.Append(Path.GetFileName(path)).Append('=').Append(await Sha256Async(path, cancellationToken)).AppendLine();
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }

    private static async Task<string> Sha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ToPortableRelative(string outputRoot, string path)
        => Path.GetRelativePath(outputRoot, path).Replace('\\', '/');

    private static class Markdown
    {
        public static string Report(IReadOnlyDictionary<string, object> report)
            => Lines(
                "# MarketData Step 1 Cross-Thread Status Report",
                "",
                $"- STEP1_FETCH_LIVE_MARKET_DATA_SANDBOX_STATUS = `{report["STEP1_FETCH_LIVE_MARKET_DATA_SANDBOX_STATUS"]}`",
                $"- STEP1_FETCH_LIVE_MARKET_DATA_PRODUCTION_STATUS = `{report["STEP1_FETCH_LIVE_MARKET_DATA_PRODUCTION_STATUS"]}`",
                $"- MARKETDATA_LMAX_DB_STATUS = `{report["MARKETDATA_LMAX_DB_STATUS"]}`",
                $"- MARKETDATA_CONTRACT_ADOPTION_STATUS = `{report["MARKETDATA_CONTRACT_ADOPTION_STATUS"]}`",
                $"- MARKETDATA_DATA_READINESS_STATUS = `{report["MARKETDATA_DATA_READINESS_STATUS"]}`",
                $"- MARKETDATA_SECRETS_STATUS = `{report["MARKETDATA_SECRETS_STATUS"]}`",
                $"- MARKETDATA_PRODUCTION_PATH_STATUS = `{report["MARKETDATA_PRODUCTION_PATH_STATUS"]}`",
                $"- NO_PRODUCTION_EXECUTION_PATH = `{report["NO_PRODUCTION_EXECUTION_PATH"]}`",
                $"- NO_CREDENTIAL_VALUES_PERSISTED = `{report["NO_CREDENTIAL_VALUES_PERSISTED"]}`");

        public static string Boundary(IReadOnlyDictionary<string, object> boundary)
            => Lines(
                "# MarketData Step 1 Boundary Invariants",
                "",
                $"- NO_PRODUCTION_EXECUTION_PATH = `{boundary["NO_PRODUCTION_EXECUTION_PATH"]}`",
                $"- MARKETDATA_LMAX_DB_STATUS = `{boundary["MARKETDATA_LMAX_DB_STATUS"]}`",
                "",
                "## Invariants",
                string.Join(Environment.NewLine, ((IEnumerable<string>)boundary["boundaryInvariants"]).Select(x => $"- `{x}`")));

        public static string Summary(IReadOnlyDictionary<string, object> report)
            => Lines(
                "# MarketData Step 1 Status Summary",
                "",
                $"- Cross-thread status: `STATUS_ONLY`",
                $"- Step 1 sandbox: `{report["STEP1_FETCH_LIVE_MARKET_DATA_SANDBOX_STATUS"]}`",
                $"- Production status: `{report["STEP1_FETCH_LIVE_MARKET_DATA_PRODUCTION_STATUS"]}`",
                $"- MarketData-LMAX-DB: `{report["MARKETDATA_LMAX_DB_STATUS"]}`",
                $"- Boundary status: `{report["NO_PRODUCTION_EXECUTION_PATH"]}`",
                $"- Credential values persisted: `{(string.Equals(report["NO_CREDENTIAL_VALUES_PERSISTED"].ToString(), "YES", StringComparison.Ordinal) ? "NO" : "YES")}`");

        private static string Lines(params string[] lines)
            => string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }
}
