using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Application;

public sealed record LmaxBoundedMarketDataLoopFreezeOptions(
    string RunKey,
    string OutputRoot,
    string SourceRunKey,
    string SourceRoot);

public sealed record LmaxBoundedMarketDataLoopFreezeResult(
    IReadOnlyDictionary<string, object> FreezeReport,
    IReadOnlyList<IReadOnlyDictionary<string, object>> EvidenceMatrix,
    IReadOnlyDictionary<string, object> BoundaryVerification,
    IReadOnlyDictionary<string, object> SecretScanReport,
    IReadOnlyList<string> Files);

public sealed class LmaxBoundedMarketDataLoopFreezeWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<LmaxBoundedMarketDataLoopFreezeResult> WriteAsync(
        LmaxBoundedMarketDataLoopFreezeOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var outputRoot = Path.GetFullPath(options.OutputRoot);
        var validationRoot = Path.Combine(outputRoot, "10_validation");
        var shareRoot = Path.Combine(outputRoot, "share");
        Directory.CreateDirectory(validationRoot);
        Directory.CreateDirectory(shareRoot);

        var sourceRoot = Path.GetFullPath(options.SourceRoot);
        var sourceSummaryPath = Path.Combine(sourceRoot, "marketdata", "lmax_marketdata_summary.json");
        var sourceEventsPath = Path.Combine(sourceRoot, "marketdata", "lmax_marketdata_events.jsonl");
        var sourceCapturePath = Path.Combine(sourceRoot, "10_validation", "lmax_marketdata_loop_capture_report.json");
        var sourceBoundaryPath = Path.Combine(sourceRoot, "10_validation", "lmax_marketdata_loop_boundary_report.json");

        using var summaryDoc = File.Exists(sourceSummaryPath)
            ? JsonDocument.Parse(await File.ReadAllTextAsync(sourceSummaryPath, cancellationToken))
            : null;
        using var captureDoc = File.Exists(sourceCapturePath)
            ? JsonDocument.Parse(await File.ReadAllTextAsync(sourceCapturePath, cancellationToken))
            : null;
        using var boundaryDoc = File.Exists(sourceBoundaryPath)
            ? JsonDocument.Parse(await File.ReadAllTextAsync(sourceBoundaryPath, cancellationToken))
            : null;

        var summary = summaryDoc?.RootElement;
        var capture = captureDoc?.RootElement;
        var boundary = boundaryDoc?.RootElement;
        var sourceHashes = await HashSourceArtifactsAsync(sourceRoot, cancellationToken);
        var secretScan = await BuildSecretScanAsync(sourceRoot, outputRoot, options.RunKey, cancellationToken);
        var boundaryVerification = BuildBoundaryVerification(options, boundary);
        var evidence = BuildEvidenceMatrix(options, sourceSummaryPath, sourceEventsPath, sourceCapturePath, sourceBoundaryPath, summary, capture, boundary, sourceHashes, secretScan, boundaryVerification);
        var freezeReport = BuildFreezeReport(options, sourceSummaryPath, sourceEventsPath, summary, capture, boundaryVerification, secretScan);

        await WriteJsonAndMarkdownAsync(validationRoot, "lmax_bounded_loop_demo_freeze_report", freezeReport, Markdown.Freeze(freezeReport), cancellationToken);
        await WriteJsonAndMarkdownAsync(validationRoot, "lmax_bounded_loop_demo_evidence_matrix", evidence, Markdown.Evidence(evidence), cancellationToken);
        await WriteJsonAndMarkdownAsync(validationRoot, "lmax_bounded_loop_demo_boundary_verification", boundaryVerification, Markdown.Boundary(boundaryVerification), cancellationToken);
        await WriteJsonAndMarkdownAsync(validationRoot, "lmax_bounded_loop_demo_secret_scan_report", secretScan, Markdown.Secret(secretScan), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(shareRoot, "lmax_bounded_loop_demo_freeze_summary.md"), Markdown.Summary(freezeReport), cancellationToken);
        await WriteManifestAsync(outputRoot, options.RunKey, freezeReport, cancellationToken);

        var files = Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(outputRoot, path).Replace('\\', '/'))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new(freezeReport, evidence, boundaryVerification, secretScan, files);
    }

    private static IReadOnlyDictionary<string, object> BuildFreezeReport(
        LmaxBoundedMarketDataLoopFreezeOptions options,
        string sourceSummaryPath,
        string sourceEventsPath,
        JsonElement? summary,
        JsonElement? capture,
        IReadOnlyDictionary<string, object> boundary,
        IReadOnlyDictionary<string, object> secretScan)
    {
        var summaryExists = File.Exists(sourceSummaryPath);
        var eventsExists = File.Exists(sourceEventsPath);
        var demoStatus = GetString(capture, "LMAX_BOUNDED_LOOP_DEMO_STATUS") ?? GetString(summary, "status") ?? "FAIL";
        var loopStatus = GetString(capture, "FIRST_STEP_FETCH_LIVE_MARKET_DATA_LOOP_STATUS") ??
                         (string.Equals(demoStatus, "PASS", StringComparison.Ordinal) ? "SANDBOX_LOOP_CAPTURED" : "FAILED");
        var primaryFailure = GetString(capture, "PRIMARY_FAILURE_REASON") ?? GetString(summary, "primary_failure_reason") ?? "UNKNOWN";
        var boundaryClean = BoundaryClean(boundary);
        var secretClean = string.Equals(GetString(secretScan, "SECRET_SCAN_STATUS"), "PASS", StringComparison.Ordinal) &&
                          string.Equals(GetString(secretScan, "NO_CREDENTIAL_VALUES_PERSISTED"), "YES", StringComparison.Ordinal);
        var freezeStatus = !summaryExists
            ? "FAIL"
            : summaryExists && eventsExists &&
              string.Equals(demoStatus, "PASS", StringComparison.Ordinal) &&
              string.Equals(loopStatus, "SANDBOX_LOOP_CAPTURED", StringComparison.Ordinal) &&
              string.Equals(primaryFailure, "NONE", StringComparison.Ordinal) &&
              boundaryClean &&
              secretClean
                ? "PASS"
                : "WARN";

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = options.RunKey,
            ["sourceRunKey"] = options.SourceRunKey,
            ["createdAtUtc"] = DateTimeOffset.UtcNow,
            ["LMAX_BOUNDED_LOOP_DEMO_FREEZE_STATUS"] = freezeStatus,
            ["LMAX_BOUNDED_LOOP_DEMO_STATUS"] = demoStatus,
            ["FIRST_STEP_FETCH_LIVE_MARKET_DATA_LOOP_STATUS"] = loopStatus,
            ["PRIMARY_FAILURE_REASON"] = primaryFailure,
            ["LMAX_BOUNDED_LOOP_EXTERNAL_CALLS_ATTEMPTED"] = GetString(capture, "LMAX_BOUNDED_LOOP_EXTERNAL_CALLS_ATTEMPTED") ?? "NO",
            ["LMAX_BOUNDED_LOOP_ARTIFACTS_ONLY"] = GetString(capture, "LMAX_BOUNDED_LOOP_ARTIFACTS_ONLY") ?? "NO",
            ["LMAX_BOUNDED_LOOP_NO_DB_WRITE"] = GetString(capture, "LMAX_BOUNDED_LOOP_NO_DB_WRITE") ?? "NO",
            ["LMAX_BOUNDED_LOOP_NO_EXECUTION"] = GetString(capture, "LMAX_BOUNDED_LOOP_NO_EXECUTION") ?? "NO",
            ["LMAX_BOUNDED_LOOP_NO_PRODUCTION_ENDPOINT"] = GetString(capture, "LMAX_BOUNDED_LOOP_NO_PRODUCTION_ENDPOINT") ?? "NO",
            ["LMAX_BOUNDED_LOOP_NO_TRADING_ENDPOINT"] = GetString(capture, "LMAX_BOUNDED_LOOP_NO_TRADING_ENDPOINT") ?? "NO",
            ["LMAX_BOUNDED_LOOP_NO_SCHEDULER"] = GetString(capture, "LMAX_BOUNDED_LOOP_NO_SCHEDULER") ?? "NO",
            ["MARKETDATA_LMAX_DB_STATUS"] = "AdoptedWithWarnings",
            ["PRODUCTION_STATUS"] = "BLOCKED",
            ["DB_PERSISTENCE_STATUS"] = "BLOCKED",
            ["EXECUTION_STATUS"] = "BLOCKED",
            ["sourceSummaryExists"] = summaryExists,
            ["sourceEventsExists"] = eventsExists
        };
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object>> BuildEvidenceMatrix(
        LmaxBoundedMarketDataLoopFreezeOptions options,
        string sourceSummaryPath,
        string sourceEventsPath,
        string sourceCapturePath,
        string sourceBoundaryPath,
        JsonElement? summary,
        JsonElement? capture,
        JsonElement? boundary,
        IReadOnlyDictionary<string, string> hashes,
        IReadOnlyDictionary<string, object> secretScan,
        IReadOnlyDictionary<string, object> boundaryVerification)
    {
        return
        [
            Evidence("Manual operator run summary exists", File.Exists(sourceSummaryPath), sourceSummaryPath, options.SourceRunKey, hashes, "Source summary is required for freeze."),
            Evidence("Events JSONL exists", File.Exists(sourceEventsPath), sourceEventsPath, options.SourceRunKey, hashes, "Source events are required for message-level lineage."),
            Evidence("External call attempted = YES", GetString(capture, "LMAX_BOUNDED_LOOP_EXTERNAL_CALLS_ATTEMPTED") == "YES", sourceCapturePath, options.SourceRunKey, hashes, "Operator-approved sandbox loop attempted external demo market-data."),
            Evidence("Sandbox loop captured market data", GetString(capture, "FIRST_STEP_FETCH_LIVE_MARKET_DATA_LOOP_STATUS") == "SANDBOX_LOOP_CAPTURED", sourceCapturePath, options.SourceRunKey, hashes, "Sandbox loop captured market data."),
            Evidence("Primary failure reason = NONE", (GetString(capture, "PRIMARY_FAILURE_REASON") ?? GetString(summary, "primary_failure_reason")) == "NONE", sourceCapturePath, options.SourceRunKey, hashes, "No primary failure."),
            Evidence("Artifacts-only = YES", GetString(capture, "LMAX_BOUNDED_LOOP_ARTIFACTS_ONLY") == "YES", sourceCapturePath, options.SourceRunKey, hashes, "Artifacts-only boundary."),
            Evidence("No DB write = YES", GetString(capture, "LMAX_BOUNDED_LOOP_NO_DB_WRITE") == "YES" && GetString(boundary, "DB_WRITE_ATTEMPTED") == "NO", sourceBoundaryPath, options.SourceRunKey, hashes, "No DB writes."),
            Evidence("No execution = YES", GetString(capture, "LMAX_BOUNDED_LOOP_NO_EXECUTION") == "YES", sourceCapturePath, options.SourceRunKey, hashes, "No execution path."),
            Evidence("No production endpoint = YES", GetString(capture, "LMAX_BOUNDED_LOOP_NO_PRODUCTION_ENDPOINT") == "YES" && GetString(boundary, "PRODUCTION_ENDPOINT_USED") == "NO", sourceBoundaryPath, options.SourceRunKey, hashes, "Production endpoint blocked."),
            Evidence("No trading endpoint = YES", GetString(capture, "LMAX_BOUNDED_LOOP_NO_TRADING_ENDPOINT") == "YES" && GetString(boundary, "TRADING_ENDPOINT_USED") == "NO", sourceBoundaryPath, options.SourceRunKey, hashes, "Trading endpoint blocked."),
            Evidence("No scheduler = YES", GetString(capture, "LMAX_BOUNDED_LOOP_NO_SCHEDULER") == "YES" && GetString(boundary, "SCHEDULER_STARTED") == "NO", sourceBoundaryPath, options.SourceRunKey, hashes, "No scheduler."),
            Evidence("MarketData-LMAX-DB remains AdoptedWithWarnings", GetString(capture, "MARKETDATA_LMAX_DB_STATUS") == "AdoptedWithWarnings", sourceCapturePath, options.SourceRunKey, hashes, "Contract status intentionally not promoted."),
            Evidence("No credential values persisted", GetString(secretScan, "NO_CREDENTIAL_VALUES_PERSISTED") == "YES", "10_validation/lmax_bounded_loop_demo_secret_scan_report.json", options.SourceRunKey, hashes, "Freeze/source artifact scan passed."),
            Evidence("No A/H/I", GetString(boundaryVerification, "A_H_I_CREATED") == "NO", sourceBoundaryPath, options.SourceRunKey, hashes, "No A/H/I artifacts."),
            Evidence("No Qubes/PMS/OMS/EMS", GetString(boundaryVerification, "QUBES_EXECUTED") == "NO" && GetString(boundaryVerification, "PMS_OMS_EMS_TOUCHED") == "NO", sourceBoundaryPath, options.SourceRunKey, hashes, "No Qubes/PMS/OMS/EMS."),
            Evidence("No order/fill/route/broker/live-state", GetString(boundaryVerification, "ORDER_MESSAGES_SENT") == "NO" && GetString(boundaryVerification, "ROUTE_BROKER_LIVE_STATE_ARTIFACTS_CREATED") == "NO", sourceBoundaryPath, options.SourceRunKey, hashes, "No execution artifacts.")
        ];
    }

    private static IReadOnlyDictionary<string, object> BuildBoundaryVerification(
        LmaxBoundedMarketDataLoopFreezeOptions options,
        JsonElement? sourceBoundary)
    {
        static string BoundaryValue(JsonElement? sourceBoundary, string key)
            => GetString(sourceBoundary, key) ?? "NO";

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = options.RunKey,
            ["sourceRunKey"] = options.SourceRunKey,
            ["PRODUCTION_ENDPOINT_USED"] = BoundaryValue(sourceBoundary, "PRODUCTION_ENDPOINT_USED"),
            ["TRADING_ENDPOINT_USED"] = BoundaryValue(sourceBoundary, "TRADING_ENDPOINT_USED"),
            ["ORDER_MESSAGES_SENT"] = BoundaryValue(sourceBoundary, "ORDER_MESSAGES_SENT"),
            ["FILL_ARTIFACTS_CREATED"] = BoundaryValue(sourceBoundary, "FILL_ARTIFACTS_CREATED"),
            ["ROUTE_BROKER_LIVE_STATE_ARTIFACTS_CREATED"] = BoundaryValue(sourceBoundary, "ROUTE_BROKER_LIVE_STATE_ARTIFACTS_CREATED"),
            ["DB_WRITE_ATTEMPTED"] = BoundaryValue(sourceBoundary, "DB_WRITE_ATTEMPTED"),
            ["MIGRATION_ATTEMPTED"] = BoundaryValue(sourceBoundary, "MIGRATION_ATTEMPTED"),
            ["QUBES_EXECUTED"] = BoundaryValue(sourceBoundary, "QUBES_EXECUTED"),
            ["QUBES_WEIGHTS_GENERATED"] = BoundaryValue(sourceBoundary, "QUBES_WEIGHTS_GENERATED"),
            ["PMS_OMS_EMS_TOUCHED"] = BoundaryValue(sourceBoundary, "PMS_OMS_EMS_TOUCHED"),
            ["MANAGER_ANUBIS_TOUCHED"] = BoundaryValue(sourceBoundary, "MANAGER_ANUBIS_TOUCHED"),
            ["A_H_I_CREATED"] = BoundaryValue(sourceBoundary, "A_H_I_CREATED"),
            ["SCHEDULER_STARTED"] = BoundaryValue(sourceBoundary, "SCHEDULER_STARTED"),
            ["WORKER_SERVICE_STARTED"] = BoundaryValue(sourceBoundary, "WORKER_SERVICE_STARTED"),
            ["CREDENTIAL_VALUES_PERSISTED"] = BoundaryValue(sourceBoundary, "CREDENTIAL_VALUES_PERSISTED"),
            ["BOUNDARY_STATUS"] = BoundaryCleanValues(sourceBoundary) ? "PASS" : "FAIL"
        };
    }

    private static async Task<IReadOnlyDictionary<string, object>> BuildSecretScanAsync(
        string sourceRoot,
        string outputRoot,
        string runKey,
        CancellationToken cancellationToken)
    {
        var findings = new List<string>();
        foreach (var root in new[] { sourceRoot, outputRoot }.Where(Directory.Exists))
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var name = Path.GetFileName(file);
                if (name is "manifest.sha256" or "hashes.json")
                {
                    continue;
                }

                var text = await File.ReadAllTextAsync(file, cancellationToken);
                if (Regex.IsMatch(text, @"554=(?!\[redacted\])[^|\u0001\s,}]+", RegexOptions.IgnoreCase))
                {
                    findings.Add($"unredacted FIX password tag in {Path.GetRelativePath(sourceRoot, file)}");
                }

                if (Regex.IsMatch(text, @"553=(?!\[redacted\])[^|\u0001\s,}]+", RegexOptions.IgnoreCase))
                {
                    findings.Add($"unredacted FIX username tag in {Path.GetRelativePath(sourceRoot, file)}");
                }

                if (Regex.IsMatch(text, "\"(password|credentialValue|passwordValue|secretValue)\"\\s*:\\s*\"(?!\\[redacted\\]|false|null|NO|YES)", RegexOptions.IgnoreCase))
                {
                    findings.Add($"credential-like value field in {Path.GetRelativePath(sourceRoot, file)}");
                }
            }
        }

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = runKey,
            ["createdAtUtc"] = DateTimeOffset.UtcNow,
            ["NO_CREDENTIAL_VALUES_PERSISTED"] = findings.Count == 0 ? "YES" : "NO",
            ["SECRET_SCAN_STATUS"] = findings.Count == 0 ? "PASS" : "FAIL",
            ["scanScope"] = new[] { sourceRoot, outputRoot },
            ["findings"] = findings
        };
    }

    private static async Task<IReadOnlyDictionary<string, string>> HashSourceArtifactsAsync(string sourceRoot, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(sourceRoot))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var hashes = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories).Order(StringComparer.OrdinalIgnoreCase))
        {
            await using var stream = File.OpenRead(file);
            var hash = await SHA256.HashDataAsync(stream, cancellationToken);
            hashes[Path.GetRelativePath(sourceRoot, file).Replace('\\', '/')] = Convert.ToHexString(hash).ToLowerInvariant();
        }

        return hashes;
    }

    private static Dictionary<string, object> Evidence(
        string name,
        bool present,
        string sourceArtifact,
        string sourceRunKey,
        IReadOnlyDictionary<string, string> hashes,
        string notes)
        => new(StringComparer.Ordinal)
        {
            ["evidenceName"] = name,
            ["evidenceStatus"] = present ? "PRESENT" : "MISSING",
            ["sourceArtifact"] = sourceArtifact.Replace('\\', '/'),
            ["sourceRunKey"] = sourceRunKey,
            ["lineageHash"] = hashes.TryGetValue(RelativeSourcePath(sourceArtifact), out var hash) ? hash : null!,
            ["notes"] = notes
        };

    private static string RelativeSourcePath(string path)
    {
        var marker = "lmax-bounded-md-loop-demo-002/";
        var normalized = path.Replace('\\', '/');
        var index = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return index < 0 ? normalized : normalized[(index + marker.Length)..];
    }

    private static bool BoundaryClean(IReadOnlyDictionary<string, object> boundary)
        => GetString(boundary, "BOUNDARY_STATUS") == "PASS";

    private static bool BoundaryCleanValues(JsonElement? sourceBoundary)
        => new[]
        {
            "PRODUCTION_ENDPOINT_USED",
            "TRADING_ENDPOINT_USED",
            "ORDER_MESSAGES_SENT",
            "FILL_ARTIFACTS_CREATED",
            "ROUTE_BROKER_LIVE_STATE_ARTIFACTS_CREATED",
            "DB_WRITE_ATTEMPTED",
            "MIGRATION_ATTEMPTED",
            "QUBES_EXECUTED",
            "QUBES_WEIGHTS_GENERATED",
            "PMS_OMS_EMS_TOUCHED",
            "MANAGER_ANUBIS_TOUCHED",
            "A_H_I_CREATED",
            "SCHEDULER_STARTED",
            "WORKER_SERVICE_STARTED",
            "CREDENTIAL_VALUES_PERSISTED"
        }.All(key => GetString(sourceBoundary, key) == "NO");

    private static string? GetString(JsonElement? element, string property)
        => element is { } value &&
           value.ValueKind == JsonValueKind.Object &&
           value.TryGetProperty(property, out var propertyValue)
            ? propertyValue.ValueKind == JsonValueKind.String ? propertyValue.GetString() : propertyValue.ToString()
            : null;

    private static string? GetString(IReadOnlyDictionary<string, object> dictionary, string property)
        => dictionary.TryGetValue(property, out var value) ? value?.ToString() : null;

    private static async Task WriteJsonAndMarkdownAsync<T>(string root, string basename, T json, string markdown, CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(Path.Combine(root, $"{basename}.json"), JsonSerializer.Serialize(json, JsonOptions), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(root, $"{basename}.md"), markdown, cancellationToken);
    }

    private static async Task WriteManifestAsync(
        string outputRoot,
        string runKey,
        IReadOnlyDictionary<string, object> freezeReport,
        CancellationToken cancellationToken)
    {
        var files = Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path) is not "hashes.json" and not "manifest.sha256")
            .OrderBy(path => Path.GetRelativePath(outputRoot, path), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var hashes = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            await using var stream = File.OpenRead(file);
            var hash = await SHA256.HashDataAsync(stream, cancellationToken);
            hashes[Path.GetRelativePath(outputRoot, file).Replace('\\', '/')] = Convert.ToHexString(hash).ToLowerInvariant();
        }

        var manifest = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["run_key"] = runKey,
            ["created_at_utc"] = DateTimeOffset.UtcNow,
            ["package_type"] = "lmax_bounded_marketdata_loop_demo_freeze_status_only",
            ["freeze_status"] = freezeReport["LMAX_BOUNDED_LOOP_DEMO_FREEZE_STATUS"],
            ["marketdata_lmax_db_status"] = "AdoptedWithWarnings",
            ["safe_next_phase"] = "STATUS_ONLY",
            ["files"] = hashes.Keys.ToArray()
        };

        var manifestPath = Path.Combine(outputRoot, "manifest.json");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken);
        await using (var stream = File.OpenRead(manifestPath))
        {
            var manifestHash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
            hashes["manifest.json"] = manifestHash;
            await File.WriteAllTextAsync(Path.Combine(outputRoot, "hashes.json"), JsonSerializer.Serialize(hashes, JsonOptions), cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(outputRoot, "manifest.sha256"), $"{manifestHash}  manifest.json{Environment.NewLine}", cancellationToken);
        }
    }

    private static class Markdown
    {
        public static string Freeze(IReadOnlyDictionary<string, object> report)
            => Lines(
                "# LMAX Bounded Loop Demo Freeze Report",
                "",
                $"- LMAX_BOUNDED_LOOP_DEMO_FREEZE_STATUS = `{report["LMAX_BOUNDED_LOOP_DEMO_FREEZE_STATUS"]}`",
                $"- LMAX_BOUNDED_LOOP_DEMO_STATUS = `{report["LMAX_BOUNDED_LOOP_DEMO_STATUS"]}`",
                $"- FIRST_STEP_FETCH_LIVE_MARKET_DATA_LOOP_STATUS = `{report["FIRST_STEP_FETCH_LIVE_MARKET_DATA_LOOP_STATUS"]}`",
                $"- PRIMARY_FAILURE_REASON = `{report["PRIMARY_FAILURE_REASON"]}`",
                $"- LMAX_BOUNDED_LOOP_EXTERNAL_CALLS_ATTEMPTED = `{report["LMAX_BOUNDED_LOOP_EXTERNAL_CALLS_ATTEMPTED"]}`",
                $"- MARKETDATA_LMAX_DB_STATUS = `{report["MARKETDATA_LMAX_DB_STATUS"]}`",
                "",
                "This package freezes an operator-run LMAX demo market-data bounded loop as status-only evidence. It does not rerun LMAX, read credentials, write DB data, or change production readiness.");

        public static string Evidence(IReadOnlyList<IReadOnlyDictionary<string, object>> rows)
            => Lines(
                "# LMAX Bounded Loop Demo Evidence Matrix",
                "",
                string.Join(Environment.NewLine, rows.Select(row => $"- {row["evidenceName"]}: `{row["evidenceStatus"]}`")));

        public static string Boundary(IReadOnlyDictionary<string, object> report)
            => Lines(
                "# LMAX Bounded Loop Demo Boundary Verification",
                "",
                $"- BOUNDARY_STATUS = `{report["BOUNDARY_STATUS"]}`",
                $"- PRODUCTION_ENDPOINT_USED = `{report["PRODUCTION_ENDPOINT_USED"]}`",
                $"- TRADING_ENDPOINT_USED = `{report["TRADING_ENDPOINT_USED"]}`",
                $"- DB_WRITE_ATTEMPTED = `{report["DB_WRITE_ATTEMPTED"]}`",
                $"- QUBES_EXECUTED = `{report["QUBES_EXECUTED"]}`",
                $"- PMS_OMS_EMS_TOUCHED = `{report["PMS_OMS_EMS_TOUCHED"]}`",
                $"- CREDENTIAL_VALUES_PERSISTED = `{report["CREDENTIAL_VALUES_PERSISTED"]}`");

        public static string Secret(IReadOnlyDictionary<string, object> report)
            => Lines(
                "# LMAX Bounded Loop Demo Secret Scan",
                "",
                $"- SECRET_SCAN_STATUS = `{report["SECRET_SCAN_STATUS"]}`",
                $"- NO_CREDENTIAL_VALUES_PERSISTED = `{report["NO_CREDENTIAL_VALUES_PERSISTED"]}`");

        public static string Summary(IReadOnlyDictionary<string, object> report)
            => Lines(
                "# LMAX Bounded Loop Demo Freeze Summary",
                "",
                $"- Freeze status: `{report["LMAX_BOUNDED_LOOP_DEMO_FREEZE_STATUS"]}`",
                $"- Source run: `{report["sourceRunKey"]}`",
                $"- Demo loop status: `{report["LMAX_BOUNDED_LOOP_DEMO_STATUS"]}`",
                $"- First step loop status: `{report["FIRST_STEP_FETCH_LIVE_MARKET_DATA_LOOP_STATUS"]}`",
                $"- External calls attempted: `{report["LMAX_BOUNDED_LOOP_EXTERNAL_CALLS_ATTEMPTED"]}`",
                $"- No DB write: `{report["LMAX_BOUNDED_LOOP_NO_DB_WRITE"]}`",
                $"- No execution: `{report["LMAX_BOUNDED_LOOP_NO_EXECUTION"]}`",
                $"- MarketData-LMAX-DB: `{report["MARKETDATA_LMAX_DB_STATUS"]}`",
                "- Safe next phase: `STATUS_ONLY`");

        private static string Lines(params string[] lines)
            => string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }
}
