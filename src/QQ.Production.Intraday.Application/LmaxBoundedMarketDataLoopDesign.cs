using System.Security.Cryptography;
using System.Text.Json;

namespace QQ.Production.Intraday.Application;

public sealed record LmaxBoundedMarketDataLoopDesignOptions(
    string RunKey,
    string OutputRoot,
    string RepoRoot);

public sealed record LmaxBoundedMarketDataLoopDesignResult(
    IReadOnlyDictionary<string, object> Design,
    IReadOnlyDictionary<string, object> Gates,
    IReadOnlyDictionary<string, object> ArtifactsContract,
    IReadOnlyDictionary<string, object> Boundary,
    IReadOnlyList<string> Files);

public sealed class LmaxBoundedMarketDataLoopDesignWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<LmaxBoundedMarketDataLoopDesignResult> WriteAsync(
        LmaxBoundedMarketDataLoopDesignOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        var outputRoot = Path.GetFullPath(options.OutputRoot);
        var validationRoot = Path.Combine(outputRoot, "10_validation");
        var shareRoot = Path.Combine(outputRoot, "share");
        Directory.CreateDirectory(validationRoot);
        Directory.CreateDirectory(shareRoot);

        var design = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = options.RunKey,
            ["createdAtUtc"] = DateTimeOffset.UtcNow,
            ["sourceStep1RunKey"] = "lmax-sandbox-md-r002-demo-retry-005",
            ["sourceReadinessPackage"] = "artifacts/lmax-sandbox-marketdata/lmax-step1-readiness-001",
            ["sourceCrossThreadStatusPackage"] = "artifacts/cross-thread-status/marketdata-step1-status-001",
            ["LMAX_BOUNDED_LOOP_DESIGN_STATUS"] = "PASS",
            ["LMAX_BOUNDED_LOOP_ARTIFACTS_ONLY"] = "YES",
            ["LMAX_BOUNDED_LOOP_NO_DB_WRITE"] = "YES",
            ["LMAX_BOUNDED_LOOP_NO_EXECUTION"] = "YES",
            ["LMAX_BOUNDED_LOOP_NO_PRODUCTION_ENDPOINT"] = "YES",
            ["LMAX_BOUNDED_LOOP_NO_TRADING_ENDPOINT"] = "YES",
            ["LMAX_BOUNDED_LOOP_NO_SCHEDULER"] = "YES",
            ["LMAX_BOUNDED_LOOP_SECRET_REDACTION_REQUIRED"] = "YES",
            ["MARKETDATA_LMAX_DB_STATUS"] = "AdoptedWithWarnings",
            ["SAFE_NEXT_PHASE"] = "IMPLEMENT_BOUNDED_LOOP_FAKE_ONLY",
            ["loopShape"] = new Dictionary<string, object>
            {
                ["durationSecondsBounded"] = true,
                ["maxMessagesBounded"] = true,
                ["instrumentsAllowListed"] = true,
                ["marketDataDemoEndpointOnly"] = true,
                ["productionEndpointAllowed"] = false,
                ["tradingEndpointAllowed"] = false,
                ["orderMessagesAllowed"] = false,
                ["dbWriteAllowed"] = false,
                ["artifactsOnlyOutput"] = true,
                ["cleanCancellationRequired"] = true,
                ["terminalTimeoutClassified"] = true,
                ["rejectClassificationRequired"] = true,
                ["logoutClassificationRequired"] = true,
                ["snapshotIncrementalClassificationRequired"] = true,
                ["secretRedactionRequired"] = true,
                ["manifestHashesRequired"] = true
            },
            ["explicitlyForbiddenInDesign"] = new[]
            {
                "Windows service auto-start",
                "background worker permanent",
                "production live",
                "DB persistence enabled",
                "Qubes integration",
                "PMS/OMS/EMS handoff",
                "order/fill/route/broker/live-state"
            },
            ["dbPersistencePreflightRequirements"] = new[]
            {
                "row counts DB available",
                "QQPRODUCTIONINTRADAY_CONNECTION_STRING present locally and never serialized",
                "tick schema clarified",
                "target storage table chosen",
                "idempotency / lineage / hash strategy approved",
                "no mutation until explicit persistence gate"
            },
            ["productionLivePreflightRequirements"] = new[]
            {
                "production endpoint approval",
                "production credentials via secret store",
                "operational monitoring",
                "reconnect/backoff policy",
                "sequence/session policy",
                "kill-switch",
                "execution boundary audit",
                "no order path leak"
            }
        };

        var gates = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = options.RunKey,
            ["LMAX_BOUNDED_LOOP_DESIGN_STATUS"] = "PASS",
            ["LMAX_BOUNDED_LOOP_ARTIFACTS_ONLY"] = "YES",
            ["LMAX_BOUNDED_LOOP_NO_DB_WRITE"] = "YES",
            ["LMAX_BOUNDED_LOOP_NO_EXECUTION"] = "YES",
            ["LMAX_BOUNDED_LOOP_NO_PRODUCTION_ENDPOINT"] = "YES",
            ["LMAX_BOUNDED_LOOP_NO_TRADING_ENDPOINT"] = "YES",
            ["LMAX_BOUNDED_LOOP_NO_SCHEDULER"] = "YES",
            ["LMAX_BOUNDED_LOOP_SECRET_REDACTION_REQUIRED"] = "YES",
            ["MARKETDATA_LMAX_DB_STATUS"] = "AdoptedWithWarnings",
            ["SAFE_NEXT_PHASE"] = "IMPLEMENT_BOUNDED_LOOP_FAKE_ONLY",
            ["gateRows"] = new[]
            {
                Gate("artifacts-only", "PASS", "Future loop writes local artifacts only."),
                Gate("no-db-write", "PASS", "DB writes and migrations remain blocked."),
                Gate("no-execution", "PASS", "No orders, fills, routes, broker or live-state."),
                Gate("no-production-endpoint", "PASS", "Production endpoint forbidden."),
                Gate("no-trading-endpoint", "PASS", "Trading endpoint forbidden."),
                Gate("no-scheduler", "PASS", "No permanent scheduler, worker, or service."),
                Gate("secret-redaction", "PASS", "Credentials must remain secret refs only."),
                Gate("marketdata-lmax-db-status", "PASS", "Status remains AdoptedWithWarnings.")
            }
        };

        var artifactsContract = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = options.RunKey,
            ["contractStatus"] = "PASS",
            ["marketdataArtifacts"] = new[]
            {
                "marketdata/lmax_marketdata_events.jsonl",
                "marketdata/lmax_marketdata_summary.json"
            },
            ["validationArtifacts"] = new[]
            {
                "10_validation/lmax_marketdata_loop_capture_report.json",
                "10_validation/lmax_marketdata_loop_capture_report.md",
                "10_validation/lmax_marketdata_loop_boundary_report.json",
                "10_validation/lmax_marketdata_loop_boundary_report.md"
            },
            ["eventJsonlColumns"] = new[]
            {
                "run_key",
                "received_at_utc",
                "environment",
                "source",
                "instrument",
                "security_id",
                "security_id_source",
                "fix_msg_type",
                "classification",
                "bid",
                "ask",
                "bid_size",
                "ask_size",
                "raw_redacted_fix"
            },
            ["summaryFields"] = new[]
            {
                "run_key",
                "started_at_utc",
                "ended_at_utc",
                "duration_seconds",
                "instruments",
                "messages_read",
                "logons",
                "snapshots",
                "incrementals",
                "session_rejects",
                "marketdata_request_rejects",
                "logouts",
                "credentials_rejected",
                "terminal_timeouts",
                "malformed",
                "unknown",
                "primary_failure_reason",
                "final_fix_session_state",
                "status"
            }
        };

        var boundary = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = options.RunKey,
            ["NO_EXTERNAL_CALL_ATTEMPTED"] = "YES",
            ["FIX_SESSION_OPENED"] = "NO",
            ["LMAX_CALL_MADE"] = "NO",
            ["PRODUCTION_ENDPOINT_USED"] = "NO",
            ["TRADING_ENDPOINT_USED"] = "NO",
            ["DB_WRITE"] = "NO",
            ["DB_MIGRATION"] = "NO",
            ["A_H_I_GENERATED"] = "NO",
            ["QUBES_EXECUTED"] = "NO",
            ["MANAGER_ANUBIS_EXECUTED"] = "NO",
            ["PMS_OMS_EMS_TOUCHED"] = "NO",
            ["SCHEDULER_WORKER_SERVICE_STARTED"] = "NO",
            ["ORDER_FILL_ROUTE_BROKER_LIVE_STATE_CREATED"] = "NO",
            ["CREDENTIAL_VALUES_PERSISTED"] = "NO",
            ["MARKETDATA_LMAX_DB_STATUS"] = "AdoptedWithWarnings"
        };

        await WriteJsonAndMarkdown(validationRoot, "lmax_bounded_marketdata_loop_design", design, Markdown.Design(design), cancellationToken);
        await WriteJsonAndMarkdown(validationRoot, "lmax_bounded_marketdata_loop_gates", gates, Markdown.Gates(gates), cancellationToken);
        await WriteJsonAndMarkdown(validationRoot, "lmax_bounded_marketdata_artifacts_contract", artifactsContract, Markdown.Artifacts(artifactsContract), cancellationToken);
        await WriteJsonAndMarkdown(validationRoot, "lmax_bounded_marketdata_no_execution_boundary", boundary, Markdown.Boundary(boundary), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(shareRoot, "lmax_bounded_marketdata_loop_design_summary.md"), Markdown.Summary(design), cancellationToken);
        await WriteManifestAsync(outputRoot, options.RunKey, cancellationToken);

        var files = Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(outputRoot, path).Replace('\\', '/'))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new(design, gates, artifactsContract, boundary, files);
    }

    private static Dictionary<string, string> Gate(string name, string status, string evidence)
        => new(StringComparer.Ordinal)
        {
            ["gate"] = name,
            ["status"] = status,
            ["evidence"] = evidence
        };

    private static async Task WriteJsonAndMarkdown(string root, string basename, IReadOnlyDictionary<string, object> report, string markdown, CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(Path.Combine(root, $"{basename}.json"), JsonSerializer.Serialize(report, JsonOptions), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(root, $"{basename}.md"), markdown, cancellationToken);
    }

    private static async Task WriteManifestAsync(string outputRoot, string runKey, CancellationToken cancellationToken)
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
            ["package_type"] = "lmax_bounded_marketdata_loop_design_status_only",
            ["lmax_bounded_loop_design_status"] = "PASS",
            ["marketdata_lmax_db_status"] = "AdoptedWithWarnings",
            ["safe_next_phase"] = "IMPLEMENT_BOUNDED_LOOP_FAKE_ONLY",
            ["files"] = hashes.Keys.ToArray()
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

    private static class Markdown
    {
        public static string Design(IReadOnlyDictionary<string, object> report)
            => Lines(
                "# LMAX Bounded MarketData Loop Design",
                "",
                $"- LMAX_BOUNDED_LOOP_DESIGN_STATUS = `{report["LMAX_BOUNDED_LOOP_DESIGN_STATUS"]}`",
                $"- LMAX_BOUNDED_LOOP_ARTIFACTS_ONLY = `{report["LMAX_BOUNDED_LOOP_ARTIFACTS_ONLY"]}`",
                $"- LMAX_BOUNDED_LOOP_NO_DB_WRITE = `{report["LMAX_BOUNDED_LOOP_NO_DB_WRITE"]}`",
                $"- LMAX_BOUNDED_LOOP_NO_EXECUTION = `{report["LMAX_BOUNDED_LOOP_NO_EXECUTION"]}`",
                $"- LMAX_BOUNDED_LOOP_NO_PRODUCTION_ENDPOINT = `{report["LMAX_BOUNDED_LOOP_NO_PRODUCTION_ENDPOINT"]}`",
                $"- LMAX_BOUNDED_LOOP_NO_TRADING_ENDPOINT = `{report["LMAX_BOUNDED_LOOP_NO_TRADING_ENDPOINT"]}`",
                $"- LMAX_BOUNDED_LOOP_NO_SCHEDULER = `{report["LMAX_BOUNDED_LOOP_NO_SCHEDULER"]}`",
                $"- MARKETDATA_LMAX_DB_STATUS = `{report["MARKETDATA_LMAX_DB_STATUS"]}`",
                "",
                "This design moves from a one-shot sandbox capture to a controlled bounded loop with explicit duration and message caps. It remains artifacts-only and cannot be a scheduler, worker, production feed, execution handoff, Qubes integration, or DB persistence path.",
                "",
                "## DB Persistence Preflight Requirements",
                "- row counts DB available",
                "- local connection string present without serialization",
                "- tick schema clarified",
                "- target storage table chosen",
                "- idempotency, lineage, and hash strategy approved",
                "- no mutation until an explicit persistence gate",
                "",
                "## Production Live Preflight Requirements",
                "- production endpoint approval",
                "- production credentials via secret store",
                "- monitoring, reconnect/backoff, sequence/session policy, and kill-switch",
                "- execution boundary audit and no order path leak");

        public static string Gates(IReadOnlyDictionary<string, object> report)
            => Lines(
                "# LMAX Bounded MarketData Loop Gates",
                "",
                $"- LMAX_BOUNDED_LOOP_DESIGN_STATUS = `{report["LMAX_BOUNDED_LOOP_DESIGN_STATUS"]}`",
                $"- SAFE_NEXT_PHASE = `{report["SAFE_NEXT_PHASE"]}`",
                "",
                "All gates are status-only design gates. No external call, DB write, scheduler, execution, Qubes, PMS/OMS/EMS, or A/H/I generation is allowed.");

        public static string Artifacts(IReadOnlyDictionary<string, object> report)
            => Lines(
                "# LMAX Bounded MarketData Artifacts Contract",
                "",
                "Future artifacts:",
                "- `marketdata/lmax_marketdata_events.jsonl`",
                "- `marketdata/lmax_marketdata_summary.json`",
                "- `10_validation/lmax_marketdata_loop_capture_report.json`",
                "- `10_validation/lmax_marketdata_loop_capture_report.md`",
                "- `10_validation/lmax_marketdata_loop_boundary_report.json`",
                "- `10_validation/lmax_marketdata_loop_boundary_report.md`");

        public static string Boundary(IReadOnlyDictionary<string, object> report)
            => Lines(
                "# LMAX Bounded MarketData No Execution Boundary",
                "",
                $"- NO_EXTERNAL_CALL_ATTEMPTED = `{report["NO_EXTERNAL_CALL_ATTEMPTED"]}`",
                $"- FIX_SESSION_OPENED = `{report["FIX_SESSION_OPENED"]}`",
                $"- LMAX_CALL_MADE = `{report["LMAX_CALL_MADE"]}`",
                $"- DB_WRITE = `{report["DB_WRITE"]}`",
                $"- QUBES_EXECUTED = `{report["QUBES_EXECUTED"]}`",
                $"- ORDER_FILL_ROUTE_BROKER_LIVE_STATE_CREATED = `{report["ORDER_FILL_ROUTE_BROKER_LIVE_STATE_CREATED"]}`",
                $"- CREDENTIAL_VALUES_PERSISTED = `{report["CREDENTIAL_VALUES_PERSISTED"]}`");

        public static string Summary(IReadOnlyDictionary<string, object> report)
            => Lines(
                "# LMAX Bounded MarketData Loop Design Summary",
                "",
                $"- Bounded loop design status: `{report["LMAX_BOUNDED_LOOP_DESIGN_STATUS"]}`",
                $"- Artifacts-only: `{report["LMAX_BOUNDED_LOOP_ARTIFACTS_ONLY"]}`",
                $"- No DB write: `{report["LMAX_BOUNDED_LOOP_NO_DB_WRITE"]}`",
                $"- No execution: `{report["LMAX_BOUNDED_LOOP_NO_EXECUTION"]}`",
                $"- No production endpoint: `{report["LMAX_BOUNDED_LOOP_NO_PRODUCTION_ENDPOINT"]}`",
                $"- No trading endpoint: `{report["LMAX_BOUNDED_LOOP_NO_TRADING_ENDPOINT"]}`",
                $"- No scheduler: `{report["LMAX_BOUNDED_LOOP_NO_SCHEDULER"]}`",
                $"- MarketData-LMAX-DB: `{report["MARKETDATA_LMAX_DB_STATUS"]}`",
                $"- Safe next phase: `{report["SAFE_NEXT_PHASE"]}`");

        private static string Lines(params string[] lines)
            => string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }
}
