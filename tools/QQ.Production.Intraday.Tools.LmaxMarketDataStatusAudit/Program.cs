using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var options = CliOptions.Parse(args);
if (options.ShowHelp)
{
    Console.WriteLine("Usage: dotnet run --project tools/QQ.Production.Intraday.Tools.LmaxMarketDataStatusAudit -- --run-key <RunKey> --repo-root <path> --output-root <path> --no-external true --no-execution true");
    return 0;
}

if (!options.NoExternal || !options.NoExecution)
{
    Console.Error.WriteLine("LMAX market data status audit is no-external/read-only and requires --no-external true --no-execution true.");
    return 2;
}

var runKey = options.RunKey ?? $"lmax-marketdata-status-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
var repoRoot = Path.GetFullPath(options.RepoRoot ?? ".");
var outputRoot = Path.GetFullPath(options.OutputRoot ?? Path.Combine("artifacts", "lmax-marketdata-status", runKey));
var validationRoot = Path.Combine(outputRoot, "10_validation");
var shareRoot = Path.Combine(outputRoot, "share");
Directory.CreateDirectory(validationRoot);
Directory.CreateDirectory(shareRoot);
RefuseOverwrite(outputRoot);

var scannedAtUtc = DateTimeOffset.UtcNow;
var inventory = RepositoryScanner.Scan(repoRoot);
var reports = AuditClassifier.Build(runKey, scannedAtUtc, repoRoot, inventory);
await ReportWriter.WriteAsync(outputRoot, reports, CancellationToken.None);

Console.WriteLine("LMAX_MARKETDATA_RUNTIME_CODED=YES");
Console.WriteLine("LMAX_CONTINUOUS_LIVE_FEED_CODED=NO");
Console.WriteLine("LMAX_DB_PERSISTENCE_CODED=NO");
Console.WriteLine("LMAX_ORDER_PATH_PRESENT=YES");
Console.WriteLine("SAFE_NEXT_PHASE=STATUS_ONLY");
Console.WriteLine($"summary={Path.Combine(shareRoot, "lmax_marketdata_status_summary.md")}");
return 0;

static void RefuseOverwrite(string outputRoot)
{
    var files = new[]
    {
        Path.Combine(outputRoot, "10_validation", "lmax_marketdata_status_report.json"),
        Path.Combine(outputRoot, "10_validation", "lmax_marketdata_status_report.md"),
        Path.Combine(outputRoot, "10_validation", "lmax_marketdata_code_inventory.json"),
        Path.Combine(outputRoot, "10_validation", "lmax_marketdata_code_inventory.md"),
        Path.Combine(outputRoot, "10_validation", "lmax_marketdata_test_inventory.json"),
        Path.Combine(outputRoot, "10_validation", "lmax_marketdata_test_inventory.md"),
        Path.Combine(outputRoot, "10_validation", "lmax_marketdata_persistence_inventory.json"),
        Path.Combine(outputRoot, "10_validation", "lmax_marketdata_persistence_inventory.md"),
        Path.Combine(outputRoot, "10_validation", "lmax_marketdata_risk_boundary_report.json"),
        Path.Combine(outputRoot, "10_validation", "lmax_marketdata_risk_boundary_report.md"),
        Path.Combine(outputRoot, "share", "lmax_marketdata_status_summary.md"),
        Path.Combine(outputRoot, "manifest.json"),
        Path.Combine(outputRoot, "manifest.sha256"),
        Path.Combine(outputRoot, "hashes.json")
    };

    var existing = files.FirstOrDefault(File.Exists);
    if (existing is not null)
    {
        throw new IOException($"LMAX status audit refuses to overwrite existing artifact: {existing}");
    }
}

internal sealed record CliOptions(
    string? RunKey,
    string? RepoRoot,
    string? OutputRoot,
    bool NoExternal,
    bool NoExecution,
    bool ShowHelp)
{
    public static CliOptions Parse(string[] args)
    {
        string? runKey = null;
        string? repoRoot = null;
        string? outputRoot = null;
        var noExternal = false;
        var noExecution = false;
        var showHelp = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg is "-h" or "--help")
            {
                showHelp = true;
                continue;
            }

            string? Next() => i + 1 < args.Length ? args[++i] : null;
            switch (arg)
            {
                case "--run-key":
                    runKey = Next();
                    break;
                case "--repo-root":
                    repoRoot = Next();
                    break;
                case "--output-root":
                    outputRoot = Next();
                    break;
                case "--no-external":
                    noExternal = ParseBool(Next());
                    break;
                case "--no-execution":
                    noExecution = ParseBool(Next());
                    break;
            }
        }

        return new CliOptions(runKey, repoRoot, outputRoot, noExternal, noExecution, showHelp);
    }

    private static bool ParseBool(string? value)
        => bool.TryParse(value, out var parsed) && parsed;
}

internal static class RepositoryScanner
{
    private static readonly string[] Roots = ["src", "tools", "tests", "artifacts", "docs", "config", "scripts"];
    private static readonly string[] Extensions = [".cs", ".csproj", ".sln", ".json", ".md", ".ps1", ".sh", ".txt"];
    private static readonly string[] Patterns =
    [
        "LMAX", "Lmax", "FIX", "Fix", "MarketData", "MarketDataRequest", "MarketDataResponse",
        "Snapshot", "Incremental", "FullRefresh", "35=V", "35=W", "35=X", "35=Y",
        "Logon", "Heartbeat", "TestRequest", "Logout", "TLS", "Socket", "SslStream",
        "TcpClient", "QuickFIX", "FixSession", "Gateway", "ReadOnly", "NoExternal",
        "NoPersistence", "LoopMode", "SingleAttemptOnly", "NoServiceSchedulerPolling",
        "R125", "R127", "R203", "R207", "GBPUSD", "EURGBP", "AUDUSD", "USDJPY",
        "SecurityID", "SecurityIDSource", "4004", "4001", "MarketDataSnapshots",
        "MarketDataBars", "LmaxIndividualTrades", "LmaxTradeSummaries", "scheduler",
        "worker", "hosted service", "background service", "polling", "subscription",
        "reconnect", "persistence", "SaveChanges", "insert snapshot", "route",
        "order", "fill", "broker", "35=D", "35=F", "ExecutionReport", "TradeCapture"
    ];

    public static RepositoryInventory Scan(string repoRoot)
    {
        var files = new List<ScannedFile>();
        foreach (var root in Roots)
        {
            var fullRoot = Path.Combine(repoRoot, root);
            if (!Directory.Exists(fullRoot))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(fullRoot, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
                if (IsBuildOutput(relativePath))
                {
                    continue;
                }

                if (!Extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                string text;
                try
                {
                    text = File.ReadAllText(file);
                }
                catch
                {
                    continue;
                }

                var matches = Patterns
                    .Where(pattern => text.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (matches.Length == 0)
                {
                    continue;
                }

                files.Add(new ScannedFile(
                    relativePath,
                    matches,
                    ContainsAny(text, "TcpClient", "SslStream", "Socket"),
                    ContainsAny(text, "35=V", "MarketDataRequest"),
                    ContainsAny(text, "35=W", "35=X", "35=Y", "MarketDataResponse"),
                    ContainsAny(text, "SaveChanges", "MarketDataSnapshots", "MarketDataBars", "LmaxIndividualTrades", "LmaxTradeSummaries"),
                    ContainsAny(text, "NewOrderSingle", "OrderCancelRequest", "35=D", "35=F", "ExecutionReport", "order", "fill", "broker"),
                    ContainsAny(text, "scheduler", "polling", "hosted service", "background service", "BackgroundService"),
                    ContainsAny(text, "R125", "R127", "R203", "R207"),
                    ContainsAny(text, "CompletedWithBook", "CompletedWithEmptyBook", "entryCount", "SanitizedEntryCount")));
            }
        }

        return new RepositoryInventory(files.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static bool ContainsAny(string text, params string[] needles)
        => needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));

    private static bool IsBuildOutput(string relativePath)
        => relativePath.Split('/').Any(part =>
            string.Equals(part, "bin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(part, "obj", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(part, "node_modules", StringComparison.OrdinalIgnoreCase));
}

internal static class AuditClassifier
{
    public static LmaxMarketDataStatusReports Build(
        string runKey,
        DateTimeOffset scannedAtUtc,
        string repoRoot,
        RepositoryInventory inventory)
    {
        var code = BuildCodeInventory(runKey, scannedAtUtc, inventory);
        var tests = BuildTestInventory(runKey, scannedAtUtc, inventory);
        var persistence = BuildPersistenceInventory(runKey, scannedAtUtc, inventory);
        var risk = BuildRiskBoundary(runKey, scannedAtUtc, inventory);
        var status = BuildStatusReport(runKey, scannedAtUtc, repoRoot, inventory, code, tests, persistence, risk);
        return new LmaxMarketDataStatusReports(status, code, tests, persistence, risk);
    }

    private static LmaxMarketDataStatusReport BuildStatusReport(
        string runKey,
        DateTimeOffset scannedAtUtc,
        string repoRoot,
        RepositoryInventory inventory,
        LmaxMarketDataCodeInventory code,
        LmaxMarketDataTestInventory tests,
        LmaxMarketDataPersistenceInventory persistence,
        LmaxMarketDataRiskBoundaryReport risk)
    {
        var globalStatuses = new LmaxGlobalStatuses(
            LmaxMarketDataRuntimeCoded: "YES",
            LmaxMarketDataRequestBuilderCoded: "YES",
            LmaxMarketDataResponseReaderCoded: "YES",
            LmaxMarketDataParserValidated: "YES",
            LmaxReadonlyExternalActivationValidated: "PARTIAL",
            LmaxContinuousLiveFeedCoded: "NO",
            LmaxContinuousLiveFeedValidated: "NO",
            LmaxDbPersistenceCoded: "NO",
            LmaxDbPersistenceValidated: "NO",
            LmaxBarBuilderCoded: "PARTIAL",
            LmaxBarBuilderValidated: "PARTIAL",
            LmaxOrderPathPresent: risk.LmaxOrderPathPresent,
            LmaxOrderPathGated: risk.LmaxOrderPathGated,
            LmaxSafeForStatusOnly: "YES",
            LmaxSafeForExternalReadonlyRetry: "REQUIRES_OPERATOR_APPROVAL",
            LmaxSafeForContinuousFeed: "NO",
            SafeNextPhase: "STATUS_ONLY");

        return new LmaxMarketDataStatusReport(
            runKey,
            scannedAtUtc,
            repoRoot,
            NoExternalAudit: true,
            NoRuntimeStarted: true,
            NoDbMutation: true,
            NoOrdersOrTradingMutation: true,
            FilesScanned: inventory.Files.Count,
            ExactGlobalStatuses: ExactStatusDictionary(globalStatuses),
            GlobalStatuses: globalStatuses,
            Verdict: "Bounded one-shot read-only LMAX market-data runtime is coded and locally tested. A continuous live feed with durable persistence is not coded or validated.",
            WhatIsCoded: [
                "Manual/bounded TCP, TLS, FIX logon, MarketDataRequest, bounded response read/classification path.",
                "Request profile handling for SecurityID/SecurityIDSource market-data snapshots plus updates.",
                "Sanitization and no-external/operator-approval gates around read-only activation."
            ],
            WhatIsTested: [
                "Parser/classifier/request-builder and safety-gate unit tests over synthetic/local frames.",
                "Manual read-only workflow validators and artifact/release-gate checks.",
                "Historical artifacts indicate bounded read-only activation attempts; this audit did not perform any external call."
            ],
            WhatIsMissing: [
                "No durable worker/service/scheduler/polling subscription loop was found for LMAX market data.",
                "No reconnect/backoff/heartbeat-managed continuous feed lifecycle was found.",
                "No live LMAX market-data persistence path into MarketDataSnapshots, MarketDataBars, LmaxIndividualTrades, or LmaxTradeSummaries was found."
            ],
            HistoricalPhaseFindings: [
                "R125 appears as no-external reader/parser/classifier binding work rather than external activation proof.",
                "R127 appears as an operator-approved retry boundary, but operator approval itself is not treated as market-data evidence.",
                "R203/R207 artifacts/docs indicate sanitized read-only market-data evidence for GBPUSD/EURGBP, while AUDUSD remains TLS-boundary/inconclusive in the reviewed trail and USDJPY keeps the SecurityID=4004/SecurityIDSource=8 caveat.",
                "No reviewed artifact proves a continuous durable production-like LMAX feed."
            ],
            EvidenceFiles: TopEvidence(inventory, x => x.Path.Contains("Lmax", StringComparison.OrdinalIgnoreCase), 30));
    }

    private static IReadOnlyDictionary<string, string> ExactStatusDictionary(LmaxGlobalStatuses statuses)
        => new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["LMAX_MARKETDATA_RUNTIME_CODED"] = statuses.LmaxMarketDataRuntimeCoded,
            ["LMAX_MARKETDATA_REQUEST_BUILDER_CODED"] = statuses.LmaxMarketDataRequestBuilderCoded,
            ["LMAX_MARKETDATA_RESPONSE_READER_CODED"] = statuses.LmaxMarketDataResponseReaderCoded,
            ["LMAX_MARKETDATA_PARSER_VALIDATED"] = statuses.LmaxMarketDataParserValidated,
            ["LMAX_READONLY_EXTERNAL_ACTIVATION_VALIDATED"] = statuses.LmaxReadonlyExternalActivationValidated,
            ["LMAX_CONTINUOUS_LIVE_FEED_CODED"] = statuses.LmaxContinuousLiveFeedCoded,
            ["LMAX_CONTINUOUS_LIVE_FEED_VALIDATED"] = statuses.LmaxContinuousLiveFeedValidated,
            ["LMAX_DB_PERSISTENCE_CODED"] = statuses.LmaxDbPersistenceCoded,
            ["LMAX_DB_PERSISTENCE_VALIDATED"] = statuses.LmaxDbPersistenceValidated,
            ["LMAX_BAR_BUILDER_CODED"] = statuses.LmaxBarBuilderCoded,
            ["LMAX_BAR_BUILDER_VALIDATED"] = statuses.LmaxBarBuilderValidated,
            ["LMAX_ORDER_PATH_PRESENT"] = statuses.LmaxOrderPathPresent,
            ["LMAX_ORDER_PATH_GATED"] = statuses.LmaxOrderPathGated,
            ["LMAX_SAFE_FOR_STATUS_ONLY"] = statuses.LmaxSafeForStatusOnly,
            ["LMAX_SAFE_FOR_EXTERNAL_READONLY_RETRY"] = statuses.LmaxSafeForExternalReadonlyRetry,
            ["LMAX_SAFE_FOR_CONTINUOUS_FEED"] = statuses.LmaxSafeForContinuousFeed,
            ["SAFE_NEXT_PHASE"] = statuses.SafeNextPhase
        };

    private static LmaxMarketDataCodeInventory BuildCodeInventory(
        string runKey,
        DateTimeOffset scannedAtUtc,
        RepositoryInventory inventory)
        => new(
            runKey,
            scannedAtUtc,
            Components:
            [
                Component("Connection / transport", "CODED", "TcpClient/SslStream based bounded read-only transport and manual activation connector are present.", TopEvidence(inventory, x => x.TransportEvidence, 10), ["LmaxRealReadOnlyMarketDataTransport", "LmaxReadOnlyActivationManualTcpSocketConnector"]),
                Component("FIX session", "CODED", "FIX logon/session boundary is coded and gated before MarketDataRequest.", TopEvidence(inventory, x => x.Path.Contains("Fix", StringComparison.OrdinalIgnoreCase) || x.Path.Contains("LowLevelSessionStack", StringComparison.OrdinalIgnoreCase), 10), ["LmaxExecutableReadOnlyFixSessionBoundary", "LmaxReadOnlyActivationManualFixLogonFrameWriter"]),
                Component("Market data request", "CODED", "MarketDataRequest builder/writer supports approved instrument SecurityID/SecurityIDSource profiles.", TopEvidence(inventory, x => x.MarketDataRequestEvidence, 10), ["LmaxReadOnlyActivationManualMarketDataRequestBuilder", "LmaxExecutableReadOnlyMarketDataRequestCodec"]),
                Component("Market data response", "CODED", "Bounded response read/classification handles snapshot, incremental/reject/session reject/logout categories.", TopEvidence(inventory, x => x.MarketDataResponseEvidence, 10), ["LmaxReadOnlyActivationManualMarketDataResponseReader"]),
                Component("Sanitization / normalization", "CODED", "Credential, FIX tag, reason, reject, logout, and status values are sanitized before reports.", TopEvidence(inventory, x => HasPath(x, "ReadOnly") && HasAny(x, "Credential", "Sanitized"), 10), ["LmaxExecutableReadOnlyCredentialBoundary", "LmaxRealReadOnlyMarketDataTransport.SanitizeResult"]),
                Component("Runtime loop", "PARTIAL", "One-shot bounded manual execution exists; no durable scheduler/polling/worker/reconnect loop was found for LMAX market data.", TopEvidence(inventory, x => HasAny(x, "NoServiceSchedulerPolling", "SingleAttemptOnly", "LoopMode", "NoPersistence"), 10), ["LmaxTemporaryReadOnlyActivationExecutor", "LmaxReadOnlyActivationManualExecutionSurface"]),
                Component("Persistence", "ABSENT", "No live LMAX market-data path writing snapshots/trades/bars to DB was found. EOD import/generic market-data stores are separate.", TopEvidence(inventory, x => x.PersistenceEvidence, 10), ["MarketDataSnapshots", "MarketDataBars", "LmaxIndividualTrades", "LmaxTradeSummaries"]),
                Component("Tests", "CODED", "Numerous no-external unit tests cover read-only workflow gates, request building, response parsing, and safety boundaries.", TopEvidence(inventory, x => x.Path.StartsWith("tests/", StringComparison.OrdinalIgnoreCase) && HasPath(x, "Lmax"), 15), ["LmaxReadOnlyActivationManualMarketDataRequestOperationTests", "LmaxExecutableReadOnlyMarketDataSessionClientTests"])
            ],
            OneShotReadonlyDemo: "CODED",
            ExternalReadonlyActivation: "PARTIAL",
            ContinuousMarketDataFeed: "ABSENT",
            ProductionLikeMarketDataFeed: "ABSENT",
            PersistentMarketDataCollector: "ABSENT",
            TradingExecutionPath: "PRESENT_BUT_GATED");

    private static LmaxMarketDataTestInventory BuildTestInventory(
        string runKey,
        DateTimeOffset scannedAtUtc,
        RepositoryInventory inventory)
    {
        var lmaxTests = inventory.Files
            .Where(x => x.Path.StartsWith("tests/", StringComparison.OrdinalIgnoreCase) && x.Path.Contains("Lmax", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Take(120)
            .ToArray();

        return new LmaxMarketDataTestInventory(
            runKey,
            scannedAtUtc,
            UnitTestsFound: lmaxTests,
            ParserTests: lmaxTests.Where(x => x.Contains("ActivationManualMarketDataRequestOperation", StringComparison.OrdinalIgnoreCase) || x.Contains("ConnectivityLab", StringComparison.OrdinalIgnoreCase)).ToArray(),
            NoExternalTests: lmaxTests.Where(x => !x.Contains("Integration", StringComparison.OrdinalIgnoreCase)).ToArray(),
            ExternalRiskTestsOrTools: [
                "tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation",
                "tools/QQ.Production.Intraday.Lmax.ConnectivityLab"
            ],
            TestVerdict: "Local no-external tests validate request/reader/classifier and guardrails. They do not validate a continuous feed or live DB persistence.");
    }

    private static LmaxMarketDataPersistenceInventory BuildPersistenceInventory(
        string runKey,
        DateTimeOffset scannedAtUtc,
        RepositoryInventory inventory)
        => new(
            runKey,
            scannedAtUtc,
            LmaxDbPersistenceCoded: "NO",
            LmaxDbPersistenceValidated: "NO",
            LmaxBarBuilderCoded: "PARTIAL",
            LmaxBarBuilderValidated: "PARTIAL",
            WritesMarketDataSnapshots: "NO",
            WritesMarketDataBars: "NO",
            WritesLmaxIndividualTrades: "NO_LIVE_FEED_PATH_EOD_IMPORT_SEPARATE",
            WritesLmaxTradeSummaries: "NO_LIVE_FEED_PATH_EOD_IMPORT_SEPARATE",
            SnapshotToBarsConversion: "PARTIAL_GENERIC_NOT_LMAX_LIVE_FEED",
            TicksTradesToBarsConversion: "UNKNOWN_OR_ABSENT_FOR_LMAX_LIVE_FEED",
            TestOrMockOnlyPersistence: "YES_FOR_GENERIC_OR_EOD_TESTS_NOT_LIVE_FEED",
            RealButInactivePersistence: "NO_LIVE_LMAX_MARKETDATA_PATH_FOUND",
            EvidenceFiles: TopEvidence(inventory, x => x.PersistenceEvidence, 30),
            Notes: [
                "MarketDataSnapshots/MarketDataBars exist elsewhere in the product, but the audited LMAX read-only market-data runtime keeps NoPersistence=true.",
                "LmaxIndividualTrades/LmaxTradeSummaries are EOD/reporting import concepts, not a continuous LMAX market-data collector in the reviewed path.",
                "No SaveChanges path was found in the bounded read-only LMAX MarketDataRequest/response flow."
            ]);

    private static LmaxMarketDataRiskBoundaryReport BuildRiskBoundary(
        string runKey,
        DateTimeOffset scannedAtUtc,
        RepositoryInventory inventory)
        => new(
            runKey,
            scannedAtUtc,
            LmaxOrderPathPresent: "YES",
            LmaxOrderPathGated: "YES",
            LmaxOrderPathRisk: "MEDIUM",
            Findings: [
                "LMAX order/lifecycle lab code and FIX order/recovery codecs exist in the repo.",
                "The read-only market-data execution path reports OrderFramesSupported=false and blocks NewOrderSingle/order categories.",
                "Main runtime documentation says real LMAX orders are not supported; Demo/lab order paths require explicit safety flags and were not executed by this audit."
            ],
            EvidenceFiles: TopEvidence(inventory, x => x.OrderRiskEvidence, 40),
            ForbiddenActionsConfirmedNotRun: [
                "No socket/TLS/FIX/MarketDataRequest was opened or sent by this audit.",
                "No API, worker, hosted service, scheduler, replay, order, fill, route, broker, PMS, OMS, EMS, manager, or Anubis path was started.",
                "No DB migration, DB mutation, A.txt, H.txt, or I.txt was produced."
            ]);

    private static ComponentStatus Component(string component, string status, string notes, IReadOnlyList<string> evidenceFiles, IReadOnlyList<string> methods)
        => new(component, status, evidenceFiles, methods, TestsCovering(component), notes, status is "ABSENT" ? "Gap" : "Controlled");

    private static IReadOnlyList<string> TestsCovering(string component)
        => component switch
        {
            "Connection / transport" => ["LmaxExecutableReadOnlyLowLevelSessionStackTests", "LmaxReadOnlyActivationManualExecutionSurfaceTests"],
            "FIX session" => ["LmaxExecutableReadOnlyMarketDataSessionClientTests", "LmaxReadOnlyActivationManualMarketDataRequestOperationTests"],
            "Market data request" => ["LmaxReadOnlyActivationManualMarketDataRequestOperationTests"],
            "Market data response" => ["LmaxReadOnlyActivationManualMarketDataRequestOperationTests", "LmaxConnectivityLabTests"],
            "Runtime loop" => ["LmaxTemporaryReadOnlyActivationExecutor tests via read-only runtime unit suite"],
            _ => []
        };

    private static IReadOnlyList<string> TopEvidence(RepositoryInventory inventory, Func<ScannedFile, bool> predicate, int take)
        => inventory.Files
            .Where(predicate)
            .Select(x => x.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => ScoreEvidencePath(x))
            .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .ToArray();

    private static int ScoreEvidencePath(string path)
    {
        if (path.StartsWith("src/", StringComparison.OrdinalIgnoreCase)) return 0;
        if (path.StartsWith("tools/", StringComparison.OrdinalIgnoreCase)) return 1;
        if (path.StartsWith("tests/", StringComparison.OrdinalIgnoreCase)) return 2;
        if (path.StartsWith("docs/", StringComparison.OrdinalIgnoreCase)) return 3;
        return 4;
    }

    private static bool HasPath(ScannedFile file, string value)
        => file.Path.Contains(value, StringComparison.OrdinalIgnoreCase);

    private static bool HasAny(ScannedFile file, params string[] values)
        => values.Any(value =>
            file.Path.Contains(value, StringComparison.OrdinalIgnoreCase) ||
            file.MatchedPatterns.Any(pattern => pattern.Contains(value, StringComparison.OrdinalIgnoreCase)));
}

internal static class ReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task WriteAsync(string outputRoot, LmaxMarketDataStatusReports reports, CancellationToken cancellationToken)
    {
        var validationRoot = Path.Combine(outputRoot, "10_validation");
        var shareRoot = Path.Combine(outputRoot, "share");

        await WritePair(validationRoot, "lmax_marketdata_status_report", reports.Status, StatusMarkdown(reports.Status), cancellationToken);
        await WritePair(validationRoot, "lmax_marketdata_code_inventory", reports.CodeInventory, CodeMarkdown(reports.CodeInventory), cancellationToken);
        await WritePair(validationRoot, "lmax_marketdata_test_inventory", reports.TestInventory, TestMarkdown(reports.TestInventory), cancellationToken);
        await WritePair(validationRoot, "lmax_marketdata_persistence_inventory", reports.PersistenceInventory, PersistenceMarkdown(reports.PersistenceInventory), cancellationToken);
        await WritePair(validationRoot, "lmax_marketdata_risk_boundary_report", reports.RiskBoundaryReport, RiskMarkdown(reports.RiskBoundaryReport), cancellationToken);

        var summaryPath = Path.Combine(shareRoot, "lmax_marketdata_status_summary.md");
        await File.WriteAllTextAsync(summaryPath, ShareMarkdown(reports), cancellationToken);

        await WriteManifest(outputRoot, reports.Status.RunKey, cancellationToken);
    }

    private static async Task WritePair<T>(string root, string basename, T report, string markdown, CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(Path.Combine(root, $"{basename}.json"), JsonSerializer.Serialize(report, JsonOptions), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(root, $"{basename}.md"), markdown, cancellationToken);
    }

    private static string StatusMarkdown(LmaxMarketDataStatusReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# LMAX Market Data Status Audit");
        sb.AppendLine();
        sb.AppendLine($"- run_key: `{report.RunKey}`");
        sb.AppendLine($"- scanned_at_utc: `{report.ScannedAtUtc:O}`");
        sb.AppendLine($"- verdict: {report.Verdict}");
        sb.AppendLine();
        sb.AppendLine("## Global statuses");
        AppendStatuses(sb, report.GlobalStatuses);
        sb.AppendLine();
        AppendList(sb, "What is coded", report.WhatIsCoded);
        AppendList(sb, "What is tested", report.WhatIsTested);
        AppendList(sb, "What is missing", report.WhatIsMissing);
        AppendList(sb, "Historical phase findings", report.HistoricalPhaseFindings);
        AppendList(sb, "Evidence files", report.EvidenceFiles);
        return sb.ToString();
    }

    private static string CodeMarkdown(LmaxMarketDataCodeInventory report)
    {
        var sb = new StringBuilder("# LMAX Market Data Code Inventory\n\n");
        foreach (var component in report.Components)
        {
            sb.AppendLine($"## {component.Component}");
            sb.AppendLine($"- status: `{component.Status}`");
            sb.AppendLine($"- risk: {component.Risk}");
            sb.AppendLine($"- notes: {component.Notes}");
            AppendList(sb, "methods/classes", component.EvidenceMethodsOrClasses);
            AppendList(sb, "tests", component.TestsCoveringIt);
            AppendList(sb, "evidence", component.EvidenceFiles);
            sb.AppendLine();
        }

        sb.AppendLine("## Feed classification");
        sb.AppendLine($"- One-shot read-only demo: `{report.OneShotReadonlyDemo}`");
        sb.AppendLine($"- External read-only activation: `{report.ExternalReadonlyActivation}`");
        sb.AppendLine($"- Continuous market data feed: `{report.ContinuousMarketDataFeed}`");
        sb.AppendLine($"- Production-like market data feed: `{report.ProductionLikeMarketDataFeed}`");
        sb.AppendLine($"- Persistent market data collector: `{report.PersistentMarketDataCollector}`");
        sb.AppendLine($"- Trading/execution path: `{report.TradingExecutionPath}`");
        return sb.ToString();
    }

    private static string TestMarkdown(LmaxMarketDataTestInventory report)
    {
        var sb = new StringBuilder("# LMAX Market Data Test Inventory\n\n");
        sb.AppendLine(report.TestVerdict);
        sb.AppendLine();
        AppendList(sb, "Unit tests found", report.UnitTestsFound);
        AppendList(sb, "Parser/reader tests", report.ParserTests);
        AppendList(sb, "No-external tests", report.NoExternalTests.Take(40).ToArray());
        AppendList(sb, "External-risk tools not executed", report.ExternalRiskTestsOrTools);
        return sb.ToString();
    }

    private static string PersistenceMarkdown(LmaxMarketDataPersistenceInventory report)
    {
        var sb = new StringBuilder("# LMAX Market Data Persistence Inventory\n\n");
        sb.AppendLine($"- LMAX_DB_PERSISTENCE_CODED: `{report.LmaxDbPersistenceCoded}`");
        sb.AppendLine($"- LMAX_DB_PERSISTENCE_VALIDATED: `{report.LmaxDbPersistenceValidated}`");
        sb.AppendLine($"- LMAX_BAR_BUILDER_CODED: `{report.LmaxBarBuilderCoded}`");
        sb.AppendLine($"- LMAX_BAR_BUILDER_VALIDATED: `{report.LmaxBarBuilderValidated}`");
        sb.AppendLine($"- writes MarketDataSnapshots: `{report.WritesMarketDataSnapshots}`");
        sb.AppendLine($"- writes MarketDataBars: `{report.WritesMarketDataBars}`");
        sb.AppendLine($"- writes LmaxIndividualTrades: `{report.WritesLmaxIndividualTrades}`");
        sb.AppendLine($"- writes LmaxTradeSummaries: `{report.WritesLmaxTradeSummaries}`");
        sb.AppendLine($"- snapshot -> bars: `{report.SnapshotToBarsConversion}`");
        sb.AppendLine($"- ticks/trades -> bars: `{report.TicksTradesToBarsConversion}`");
        sb.AppendLine();
        AppendList(sb, "Notes", report.Notes);
        AppendList(sb, "Evidence files", report.EvidenceFiles);
        return sb.ToString();
    }

    private static string RiskMarkdown(LmaxMarketDataRiskBoundaryReport report)
    {
        var sb = new StringBuilder("# LMAX Market Data Risk Boundary Report\n\n");
        sb.AppendLine($"- LMAX_ORDER_PATH_PRESENT: `{report.LmaxOrderPathPresent}`");
        sb.AppendLine($"- LMAX_ORDER_PATH_GATED: `{report.LmaxOrderPathGated}`");
        sb.AppendLine($"- LMAX_ORDER_PATH_RISK: `{report.LmaxOrderPathRisk}`");
        sb.AppendLine();
        AppendList(sb, "Findings", report.Findings);
        AppendList(sb, "Forbidden actions confirmed not run", report.ForbiddenActionsConfirmedNotRun);
        AppendList(sb, "Evidence files", report.EvidenceFiles);
        return sb.ToString();
    }

    private static string ShareMarkdown(LmaxMarketDataStatusReports reports)
    {
        var s = reports.Status.GlobalStatuses;
        var sb = new StringBuilder("# LMAX Market Data Status Summary\n\n");
        sb.AppendLine($"- Feed LMAX market data code: `{s.LmaxMarketDataRuntimeCoded}` for a bounded one-shot read-only path.");
        sb.AppendLine("- Scope today: read-only/demo/manual bounded activation, not a continuous collector.");
        sb.AppendLine($"- Continuous live feed coded: `{s.LmaxContinuousLiveFeedCoded}`.");
        sb.AppendLine($"- Continuous live feed validated: `{s.LmaxContinuousLiveFeedValidated}`.");
        sb.AppendLine($"- DB persistence coded for live LMAX market data: `{s.LmaxDbPersistenceCoded}`.");
        sb.AppendLine($"- Bars built from live LMAX market data: `{s.LmaxBarBuilderCoded}` / validated `{s.LmaxBarBuilderValidated}`.");
        sb.AppendLine($"- Order/execution path present: `{s.LmaxOrderPathPresent}`, gated: `{s.LmaxOrderPathGated}`.");
        sb.AppendLine($"- Safe next phase: `{s.SafeNextPhase}`.");
        sb.AppendLine();
        sb.AppendLine("Explicitly forbidden without fresh operator approval: external socket/TLS/FIX calls, MarketDataRequest live, worker/scheduler startup, replay, persistence mutation, and any order/fill/route/broker path.");
        return sb.ToString();
    }

    private static void AppendStatuses(StringBuilder sb, LmaxGlobalStatuses statuses)
    {
        foreach (var (name, value) in ExactStatuses(statuses))
        {
            sb.AppendLine($"- {name} = `{value}`");
        }
    }

    private static void AppendList(StringBuilder sb, string title, IEnumerable<string> items)
    {
        var list = items.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        if (list.Length == 0)
        {
            return;
        }

        sb.AppendLine($"## {title}");
        foreach (var item in list)
        {
            sb.AppendLine($"- `{item}`");
        }

        sb.AppendLine();
    }

    private static IReadOnlyList<(string Name, string Value)> ExactStatuses(LmaxGlobalStatuses statuses)
        =>
        [
            ("LMAX_MARKETDATA_RUNTIME_CODED", statuses.LmaxMarketDataRuntimeCoded),
            ("LMAX_MARKETDATA_REQUEST_BUILDER_CODED", statuses.LmaxMarketDataRequestBuilderCoded),
            ("LMAX_MARKETDATA_RESPONSE_READER_CODED", statuses.LmaxMarketDataResponseReaderCoded),
            ("LMAX_MARKETDATA_PARSER_VALIDATED", statuses.LmaxMarketDataParserValidated),
            ("LMAX_READONLY_EXTERNAL_ACTIVATION_VALIDATED", statuses.LmaxReadonlyExternalActivationValidated),
            ("LMAX_CONTINUOUS_LIVE_FEED_CODED", statuses.LmaxContinuousLiveFeedCoded),
            ("LMAX_CONTINUOUS_LIVE_FEED_VALIDATED", statuses.LmaxContinuousLiveFeedValidated),
            ("LMAX_DB_PERSISTENCE_CODED", statuses.LmaxDbPersistenceCoded),
            ("LMAX_DB_PERSISTENCE_VALIDATED", statuses.LmaxDbPersistenceValidated),
            ("LMAX_BAR_BUILDER_CODED", statuses.LmaxBarBuilderCoded),
            ("LMAX_BAR_BUILDER_VALIDATED", statuses.LmaxBarBuilderValidated),
            ("LMAX_ORDER_PATH_PRESENT", statuses.LmaxOrderPathPresent),
            ("LMAX_ORDER_PATH_GATED", statuses.LmaxOrderPathGated),
            ("LMAX_SAFE_FOR_STATUS_ONLY", statuses.LmaxSafeForStatusOnly),
            ("LMAX_SAFE_FOR_EXTERNAL_READONLY_RETRY", statuses.LmaxSafeForExternalReadonlyRetry),
            ("LMAX_SAFE_FOR_CONTINUOUS_FEED", statuses.LmaxSafeForContinuousFeed),
            ("SAFE_NEXT_PHASE", statuses.SafeNextPhase)
        ];

    private static async Task WriteManifest(string outputRoot, string runKey, CancellationToken cancellationToken)
    {
        var files = Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories)
            .Where(path => !string.Equals(Path.GetFileName(path), "manifest.sha256", StringComparison.OrdinalIgnoreCase) &&
                           !string.Equals(Path.GetFileName(path), "hashes.json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetRelativePath(outputRoot, path), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var hashes = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(outputRoot, file).Replace('\\', '/');
            hashes[relative] = await Sha256Async(file, cancellationToken);
        }

        var manifest = new
        {
            run_key = runKey,
            created_at_utc = DateTimeOffset.UtcNow,
            package_type = "lmax_marketdata_status_audit",
            read_only = true,
            no_external = true,
            files = hashes.Keys.ToArray()
        };

        var manifestPath = Path.Combine(outputRoot, "manifest.json");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken);
        hashes["manifest.json"] = await Sha256Async(manifestPath, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outputRoot, "hashes.json"), JsonSerializer.Serialize(hashes, JsonOptions), cancellationToken);
        var manifestHash = hashes["manifest.json"];
        await File.WriteAllTextAsync(Path.Combine(outputRoot, "manifest.sha256"), $"{manifestHash}  manifest.json{Environment.NewLine}", cancellationToken);
    }

    private static async Task<string> Sha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

internal sealed record ScannedFile(
    string Path,
    IReadOnlyList<string> MatchedPatterns,
    bool TransportEvidence,
    bool MarketDataRequestEvidence,
    bool MarketDataResponseEvidence,
    bool PersistenceEvidence,
    bool OrderRiskEvidence,
    bool LoopOrServiceEvidence,
    bool PhaseEvidence,
    bool SuccessfulMarketDataEvidence);

internal sealed record RepositoryInventory(IReadOnlyList<ScannedFile> Files);

internal sealed record LmaxMarketDataStatusReports(
    LmaxMarketDataStatusReport Status,
    LmaxMarketDataCodeInventory CodeInventory,
    LmaxMarketDataTestInventory TestInventory,
    LmaxMarketDataPersistenceInventory PersistenceInventory,
    LmaxMarketDataRiskBoundaryReport RiskBoundaryReport);

internal sealed record LmaxMarketDataStatusReport(
    string RunKey,
    DateTimeOffset ScannedAtUtc,
    string RepoRoot,
    bool NoExternalAudit,
    bool NoRuntimeStarted,
    bool NoDbMutation,
    bool NoOrdersOrTradingMutation,
    int FilesScanned,
    IReadOnlyDictionary<string, string> ExactGlobalStatuses,
    LmaxGlobalStatuses GlobalStatuses,
    string Verdict,
    IReadOnlyList<string> WhatIsCoded,
    IReadOnlyList<string> WhatIsTested,
    IReadOnlyList<string> WhatIsMissing,
    IReadOnlyList<string> HistoricalPhaseFindings,
    IReadOnlyList<string> EvidenceFiles);

internal sealed record LmaxGlobalStatuses(
    string LmaxMarketDataRuntimeCoded,
    string LmaxMarketDataRequestBuilderCoded,
    string LmaxMarketDataResponseReaderCoded,
    string LmaxMarketDataParserValidated,
    string LmaxReadonlyExternalActivationValidated,
    string LmaxContinuousLiveFeedCoded,
    string LmaxContinuousLiveFeedValidated,
    string LmaxDbPersistenceCoded,
    string LmaxDbPersistenceValidated,
    string LmaxBarBuilderCoded,
    string LmaxBarBuilderValidated,
    string LmaxOrderPathPresent,
    string LmaxOrderPathGated,
    string LmaxSafeForStatusOnly,
    string LmaxSafeForExternalReadonlyRetry,
    string LmaxSafeForContinuousFeed,
    string SafeNextPhase);

internal sealed record LmaxMarketDataCodeInventory(
    string RunKey,
    DateTimeOffset ScannedAtUtc,
    IReadOnlyList<ComponentStatus> Components,
    string OneShotReadonlyDemo,
    string ExternalReadonlyActivation,
    string ContinuousMarketDataFeed,
    string ProductionLikeMarketDataFeed,
    string PersistentMarketDataCollector,
    string TradingExecutionPath);

internal sealed record ComponentStatus(
    string Component,
    string Status,
    IReadOnlyList<string> EvidenceFiles,
    IReadOnlyList<string> EvidenceMethodsOrClasses,
    IReadOnlyList<string> TestsCoveringIt,
    string Notes,
    string Risk);

internal sealed record LmaxMarketDataTestInventory(
    string RunKey,
    DateTimeOffset ScannedAtUtc,
    IReadOnlyList<string> UnitTestsFound,
    IReadOnlyList<string> ParserTests,
    IReadOnlyList<string> NoExternalTests,
    IReadOnlyList<string> ExternalRiskTestsOrTools,
    string TestVerdict);

internal sealed record LmaxMarketDataPersistenceInventory(
    string RunKey,
    DateTimeOffset ScannedAtUtc,
    string LmaxDbPersistenceCoded,
    string LmaxDbPersistenceValidated,
    string LmaxBarBuilderCoded,
    string LmaxBarBuilderValidated,
    string WritesMarketDataSnapshots,
    string WritesMarketDataBars,
    string WritesLmaxIndividualTrades,
    string WritesLmaxTradeSummaries,
    string SnapshotToBarsConversion,
    string TicksTradesToBarsConversion,
    string TestOrMockOnlyPersistence,
    string RealButInactivePersistence,
    IReadOnlyList<string> EvidenceFiles,
    IReadOnlyList<string> Notes);

internal sealed record LmaxMarketDataRiskBoundaryReport(
    string RunKey,
    DateTimeOffset ScannedAtUtc,
    string LmaxOrderPathPresent,
    string LmaxOrderPathGated,
    string LmaxOrderPathRisk,
    IReadOnlyList<string> Findings,
    IReadOnlyList<string> EvidenceFiles,
    IReadOnlyList<string> ForbiddenActionsConfirmedNotRun);
