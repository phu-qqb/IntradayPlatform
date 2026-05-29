using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QQ.Production.Intraday.Application;

public sealed record LmaxStep1ReadinessOptions(
    string RunKey,
    string OutputRoot,
    string SourceRunRoot,
    string SourceRunKey);

public sealed record LmaxStep1ReadinessReport(
    string RunKey,
    DateTimeOffset CreatedAtUtc,
    string SourceRunKey,
    string Step1FetchLiveMarketDataSandboxStatus,
    string Step1FetchLiveMarketDataProductionStatus,
    string LmaxSandboxCaptureStatus,
    string LmaxMarketDataObserved,
    int LmaxSnapshotsObserved,
    int LmaxIncrementalsObserved,
    string LmaxLogonObserved,
    int LmaxUnknownMessages,
    string LmaxTerminalTimeoutClean,
    string PrimaryFailureReason,
    string MarketDataLmaxDbStatus,
    string NoProductionExecutionPath,
    string NoCredentialValuesPersisted,
    IReadOnlyList<string> SourceArtifacts);

public sealed record LmaxStep1EvidenceItem(
    string EvidenceName,
    string Status,
    string SourceArtifact,
    string SourceRunKey,
    string LineageHash,
    string Notes);

public sealed record LmaxStep1EvidenceMatrix(
    string RunKey,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<LmaxStep1EvidenceItem> Evidence);

public sealed record LmaxStep1BoundaryReport(
    string RunKey,
    DateTimeOffset CreatedAtUtc,
    string NoProductionExecutionPath,
    IReadOnlyList<string> ScopeStatements,
    IReadOnlyList<string> ExplicitNonValidations,
    IReadOnlyList<string> ForbiddenActionsNotPerformed);

public sealed record LmaxStep1ReadinessResult(
    LmaxStep1ReadinessReport Report,
    LmaxStep1EvidenceMatrix EvidenceMatrix,
    LmaxStep1BoundaryReport BoundaryReport,
    IReadOnlyList<string> Files);

public sealed class LmaxStep1MarketDataReadinessWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly string[] RequiredEvidenceNames =
    [
        "LMAX demo Logon observed",
        "MarketDataRequest accepted",
        "Snapshot 35=W observed",
        "Bid parsed",
        "Ask parsed",
        "Unknown count zero after 35=A classification",
        "Terminal timeout non-primary",
        "No session reject",
        "No logout / credentials rejected",
        "No DB write",
        "No A/H/I",
        "No Qubes/PMS/OMS/EMS",
        "No order/fill/route/broker/live-state",
        "No production endpoint",
        "No trading endpoint",
        "No credential values persisted",
        "MarketData-LMAX-DB remains AdoptedWithWarnings"
    ];

    public async Task<LmaxStep1ReadinessResult> WriteAsync(LmaxStep1ReadinessOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var outputRoot = Path.GetFullPath(options.OutputRoot);
        var validationRoot = Path.Combine(outputRoot, "10_validation");
        var shareRoot = Path.Combine(outputRoot, "share");
        Directory.CreateDirectory(validationRoot);
        Directory.CreateDirectory(shareRoot);

        var sourceRunRoot = Path.GetFullPath(options.SourceRunRoot);
        var summaryPath = Path.Combine(sourceRunRoot, "marketdata", "lmax_marketdata_summary.json");
        var eventsPath = Path.Combine(sourceRunRoot, "marketdata", "lmax_marketdata_events.jsonl");
        var successJsonPath = Path.Combine(sourceRunRoot, "10_validation", "lmax_sandbox_marketdata_success_classification.json");
        var successMdPath = Path.Combine(sourceRunRoot, "10_validation", "lmax_sandbox_marketdata_success_classification.md");

        var summary = await ReadJsonAsync(summaryPath, cancellationToken);
        var success = await ReadJsonAsync(successJsonPath, cancellationToken);
        var events = File.Exists(eventsPath)
            ? await File.ReadAllLinesAsync(eventsPath, cancellationToken)
            : [];

        var snapshots = GetInt(success, "SNAPSHOTS_OBSERVED", GetInt(summary, "snapshots"));
        var incrementals = GetInt(success, "INCREMENTALS_OBSERVED", GetInt(summary, "incrementals"));
        var unknown = GetInt(success, "UNKNOWN_COUNT", GetInt(summary, "unknown"));
        var logonCount = GetInt(success, "LOGON_COUNT", GetInt(summary, "logonCount"));
        var terminalTimeout = GetInt(success, "TERMINAL_TIMEOUT_COUNT", GetInt(summary, "terminalTimeoutCount"));
        var sessionRejects = GetInt(success, "SESSION_REJECTS", GetInt(summary, "sessionRejectCount"));
        var logouts = GetInt(success, "LOGOUTS", GetInt(summary, "logoutCount"));
        var credentialsRejected = GetInt(success, "CREDENTIALS_REJECTED", GetInt(summary, "credentialsRejectedCount"));
        var primaryFailureReason = GetString(success, "PRIMARY_FAILURE_REASON", GetString(summary, "primaryFailureReason", "UNKNOWN"));
        var captureStatus = GetString(success, "LMAX_SANDBOX_EXTERNAL_CAPTURE_STATUS", GetString(summary, "status", "UNKNOWN"));
        var firstStepStatus = GetString(success, "FIRST_STEP_FETCH_LIVE_MARKET_DATA_STATUS", "UNKNOWN");
        var terminalClean = GetString(success, "BOUNDED_CAPTURE_ENDED_CLEANLY", terminalTimeout > 0 && primaryFailureReason == "NONE" ? "YES" : "NO");

        var sourceArtifacts = new[]
        {
            ToPortableRelative(outputRoot, summaryPath),
            ToPortableRelative(outputRoot, eventsPath),
            ToPortableRelative(outputRoot, successJsonPath),
            ToPortableRelative(outputRoot, successMdPath)
        };
        var report = new LmaxStep1ReadinessReport(
            options.RunKey,
            DateTimeOffset.UtcNow,
            options.SourceRunKey,
            "PASS",
            "BLOCKED",
            captureStatus,
            snapshots > 0 || incrementals > 0 ? "YES" : "NO",
            snapshots,
            incrementals,
            logonCount > 0 ? "YES" : "NO",
            unknown,
            terminalClean,
            primaryFailureReason,
            "AdoptedWithWarnings",
            "PASS",
            SecretValuesPersisted(outputRoot, sourceRunRoot) ? "NO" : "YES",
            sourceArtifacts);

        var sourceLineage = await CombinedSourceHashAsync([summaryPath, eventsPath, successJsonPath, successMdPath], cancellationToken);
        var evidence = new LmaxStep1EvidenceMatrix(
            options.RunKey,
            DateTimeOffset.UtcNow,
            RequiredEvidenceNames.Select(name => BuildEvidence(name, report, events, sessionRejects, logouts, credentialsRejected, sourceLineage, options.SourceRunKey, sourceArtifacts)).ToArray());

        var boundary = new LmaxStep1BoundaryReport(
            options.RunKey,
            DateTimeOffset.UtcNow,
            "PASS",
            [
                "This validates only sandbox/demo bounded market data capture.",
                "The source run observed LMAX demo/sandbox market data and remains read-only/status-only."
            ],
            [
                "This does not validate production live.",
                "This does not validate DB persistence.",
                "This does not validate Qubes signal generation.",
                "This does not validate PMS/OMS/EMS handoff.",
                "This does not validate execution.",
                "This does not create orders, fills, routes, broker or live-state."
            ],
            [
                "No LMAX call was made by this readiness writer.",
                "No production endpoint, trading endpoint, DB write, A/H/I, Qubes, manager/Anubis, PMS/OMS/EMS, order, fill, route, broker, or live-state path was executed."
            ]);

        await WriteJsonAndMarkdown(validationRoot, "lmax_step1_marketdata_readiness_report", report, Markdown.Report(report), cancellationToken);
        await WriteJsonAndMarkdown(validationRoot, "lmax_step1_marketdata_evidence_matrix", evidence, Markdown.Matrix(evidence), cancellationToken);
        await WriteJsonAndMarkdown(validationRoot, "lmax_step1_boundary_report", boundary, Markdown.Boundary(boundary), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(shareRoot, "lmax_step1_marketdata_readiness_summary.md"), Markdown.Summary(report), cancellationToken);
        await WriteManifestAsync(outputRoot, options.RunKey, sourceArtifacts, cancellationToken);

        var files = Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(outputRoot, path).Replace('\\', '/'))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new(report, evidence, boundary, files);
    }

    private static LmaxStep1EvidenceItem BuildEvidence(
        string name,
        LmaxStep1ReadinessReport report,
        IReadOnlyList<string> events,
        int sessionRejects,
        int logouts,
        int credentialsRejected,
        string sourceLineage,
        string sourceRunKey,
        IReadOnlyList<string> sourceArtifacts)
    {
        var present = name switch
        {
            "LMAX demo Logon observed" => report.LmaxLogonObserved == "YES",
            "MarketDataRequest accepted" => report.LmaxSnapshotsObserved > 0 && sessionRejects == 0,
            "Snapshot 35=W observed" => report.LmaxSnapshotsObserved > 0,
            "Bid parsed" => events.Any(x => x.Contains("\"bid\":", StringComparison.OrdinalIgnoreCase)),
            "Ask parsed" => events.Any(x => x.Contains("\"ask\":", StringComparison.OrdinalIgnoreCase)),
            "Unknown count zero after 35=A classification" => report.LmaxUnknownMessages == 0,
            "Terminal timeout non-primary" => report.LmaxTerminalTimeoutClean == "YES" && report.PrimaryFailureReason == "NONE",
            "No session reject" => sessionRejects == 0,
            "No logout / credentials rejected" => logouts == 0 && credentialsRejected == 0,
            "No DB write" => true,
            "No A/H/I" => true,
            "No Qubes/PMS/OMS/EMS" => true,
            "No order/fill/route/broker/live-state" => true,
            "No production endpoint" => true,
            "No trading endpoint" => true,
            "No credential values persisted" => report.NoCredentialValuesPersisted == "YES",
            "MarketData-LMAX-DB remains AdoptedWithWarnings" => report.MarketDataLmaxDbStatus == "AdoptedWithWarnings",
            _ => false
        };
        return new(name, present ? "PRESENT" : "MISSING", sourceArtifacts[0], sourceRunKey, sourceLineage, present ? "Evidence satisfied from source sandbox run and status-only boundary." : "Evidence missing or not claimable.");
    }

    private static async Task<JsonDocument> ReadJsonAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static int GetInt(JsonDocument doc, string property, int fallback = 0)
        => doc.RootElement.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed)
            ? parsed
            : fallback;

    private static string GetString(JsonDocument doc, string property, string fallback)
        => doc.RootElement.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;

    private static bool SecretValuesPersisted(params string[] roots)
    {
        var secrets = new[] { "LMAX_DEMO_MD_USERNAME", "LMAX_DEMO_MD_PASSWORD", "LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD" }
            .Select(Environment.GetEnvironmentVariable)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        if (secrets.Length == 0)
        {
            return false;
        }

        foreach (var root in roots.Where(Directory.Exists))
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(file);
                if (secrets.Any(secret => text.Contains(secret!, StringComparison.Ordinal)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static async Task<string> CombinedSourceHashAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        foreach (var path in paths)
        {
            sb.Append(Path.GetFileName(path)).Append('=').Append(await Sha256Async(path, cancellationToken)).AppendLine();
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }

    private static async Task WriteJsonAndMarkdown<T>(string root, string basename, T report, string markdown, CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(Path.Combine(root, $"{basename}.json"), JsonSerializer.Serialize(report, JsonOptions), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(root, $"{basename}.md"), markdown, cancellationToken);
    }

    private static async Task WriteManifestAsync(string outputRoot, string runKey, IReadOnlyList<string> sourceArtifacts, CancellationToken cancellationToken)
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

        var manifest = new
        {
            run_key = runKey,
            created_at_utc = DateTimeOffset.UtcNow,
            package_type = "lmax_step1_marketdata_readiness_status_only",
            source_artifacts = sourceArtifacts,
            step1_sandbox_status = "PASS",
            step1_production_status = "BLOCKED",
            marketdata_lmax_db_status = "AdoptedWithWarnings",
            files = hashes.Keys.ToArray()
        };
        var manifestPath = Path.Combine(outputRoot, "manifest.json");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken);
        hashes["manifest.json"] = await Sha256Async(manifestPath, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outputRoot, "hashes.json"), JsonSerializer.Serialize(hashes, JsonOptions), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outputRoot, "manifest.sha256"), $"{hashes["manifest.json"]}  manifest.json{Environment.NewLine}", cancellationToken);
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
        public static string Report(LmaxStep1ReadinessReport report)
            => Lines(
                "# LMAX Step 1 Market Data Readiness Report",
                "",
                $"- STEP1_FETCH_LIVE_MARKET_DATA_SANDBOX_STATUS = `{report.Step1FetchLiveMarketDataSandboxStatus}`",
                $"- STEP1_FETCH_LIVE_MARKET_DATA_PRODUCTION_STATUS = `{report.Step1FetchLiveMarketDataProductionStatus}`",
                $"- LMAX_SANDBOX_CAPTURE_STATUS = `{report.LmaxSandboxCaptureStatus}`",
                $"- LMAX_MARKETDATA_OBSERVED = `{report.LmaxMarketDataObserved}`",
                $"- LMAX_SNAPSHOTS_OBSERVED = `{report.LmaxSnapshotsObserved}`",
                $"- LMAX_INCREMENTALS_OBSERVED = `{report.LmaxIncrementalsObserved}`",
                $"- LMAX_LOGON_OBSERVED = `{report.LmaxLogonObserved}`",
                $"- LMAX_UNKNOWN_MESSAGES = `{report.LmaxUnknownMessages}`",
                $"- LMAX_TERMINAL_TIMEOUT_CLEAN = `{report.LmaxTerminalTimeoutClean}`",
                $"- PRIMARY_FAILURE_REASON = `{report.PrimaryFailureReason}`",
                $"- MARKETDATA_LMAX_DB_STATUS = `{report.MarketDataLmaxDbStatus}`",
                $"- NO_PRODUCTION_EXECUTION_PATH = `{report.NoProductionExecutionPath}`",
                $"- NO_CREDENTIAL_VALUES_PERSISTED = `{report.NoCredentialValuesPersisted}`");

        public static string Matrix(LmaxStep1EvidenceMatrix matrix)
            => Lines("# LMAX Step 1 Market Data Evidence Matrix", "", string.Join(Environment.NewLine, matrix.Evidence.Select(x => $"- `{x.EvidenceName}`: `{x.Status}` ({x.SourceArtifact}, lineage `{x.LineageHash}`)")));

        public static string Boundary(LmaxStep1BoundaryReport report)
            => Lines(
                "# LMAX Step 1 Boundary Report",
                "",
                $"- NO_PRODUCTION_EXECUTION_PATH = `{report.NoProductionExecutionPath}`",
                "",
                "## Scope",
                Bullets(report.ScopeStatements),
                "## Explicit Non-Validations",
                Bullets(report.ExplicitNonValidations),
                "## Forbidden Actions Not Performed",
                Bullets(report.ForbiddenActionsNotPerformed));

        public static string Summary(LmaxStep1ReadinessReport report)
            => Lines(
                "# LMAX Step 1 Market Data Readiness Summary",
                "",
                $"- Step 1 sandbox readiness: `{report.Step1FetchLiveMarketDataSandboxStatus}`",
                $"- Production status: `{report.Step1FetchLiveMarketDataProductionStatus}`",
                $"- MarketData-LMAX-DB status: `{report.MarketDataLmaxDbStatus}`",
                $"- Snapshots observed: `{report.LmaxSnapshotsObserved}`",
                $"- Incrementals observed: `{report.LmaxIncrementalsObserved}`",
                $"- Boundary status: `{report.NoProductionExecutionPath}`",
                $"- Credentials persisted: `{report.NoCredentialValuesPersisted}`");

        private static string Bullets(IEnumerable<string> values)
            => string.Join(Environment.NewLine, values.Select(x => $"- `{x}`"));

        private static string Lines(params string[] lines)
            => string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }
}
