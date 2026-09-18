using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Infrastructure.SqlServer;

namespace QQ.Production.Intraday.Tools.LmaxDemoDayLauncher;

internal static class SessionBindings
{
    internal const string Worker = @"C:\deploy\IntradayPlatform\operator\lmax-demo-orchestration\gmv-resume-20260918\worker\QQ.Production.Intraday.Worker.dll";
    internal const string WorkerHash = "83396ce71e70cd06b32cb438445fbb4cec35f2a412927a09b6673647eedf141c";
    internal const string WorkerManifest = @"C:\deploy\IntradayPlatform\operator\lmax-demo-orchestration\gmv-resume-20260918\worker-manifest.json";
    internal const string WorkerManifestHash = "9389ba693c7a3ed12ec673406f27a319d1bd48d84b53fdd4df34c831a9d3564f";

    internal static void VerifyWorkerClosure()
    {
        Files.Require(Files.Hash(WorkerManifest) == WorkerManifestHash, "WORKER_MANIFEST_PIN_MISMATCH");
        using var document = JsonDocument.Parse(File.ReadAllBytes(WorkerManifest));
        var manifest = document.RootElement;
        Files.Require(manifest.GetProperty("schema").GetString() == "lmax_demo_worker_closure_v1", "WORKER_MANIFEST_SCHEMA");
        var root = Path.GetDirectoryName(Worker)!;
        var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.GetProperty("files").EnumerateArray())
        {
            var relative = file.GetProperty("path").GetString()!;
            Files.Require(!Path.IsPathRooted(relative) && !relative.Split('/', '\\').Contains(".."), "WORKER_MANIFEST_PATH");
            var full = Path.GetFullPath(Path.Combine(root, relative));
            Files.Require(full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && listed.Add(full)
                && (File.GetAttributes(full) & FileAttributes.ReparsePoint) == 0
                && Files.Hash(full) == file.GetProperty("sha256").GetString(), "WORKER_DEPENDENCY_PIN_MISMATCH");
        }
        Files.Require(listed.Count > 0 && listed.SetEquals(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)), "WORKER_CLOSURE_CHANGED");
        Files.Require(Files.Hash(Worker) == WorkerHash, "WORKER_PIN_MISMATCH");
    }

    internal static Dictionary<string, string> Bind(JsonElement secret)
    {
        string Get(string suffix)
        {
            var value = secret.GetProperty("QQ_LMAX_FIX_" + suffix).GetString();
            Files.Require(!string.IsNullOrWhiteSpace(value), "DEMO_SECRET_FIELD_MISSING");
            return value!;
        }
        Files.Require(Get("ORDER_USERNAME") == Get("MARKETDATA_USERNAME") && Get("ORDER_PASSWORD") == Get("MARKETDATA_PASSWORD"), "SEPARATE_MD_CREDENTIAL_BINDING_REQUIRED");
        Files.Require(Get("ORDER_HOST") == "fix-order.london-demo.lmax.com" && Get("ORDER_PORT") == "443"
            && Get("MARKETDATA_HOST") == "fix-marketdata.london-demo.lmax.com" && Get("MARKETDATA_PORT") == "443", "DEMO_ENDPOINT_REQUIRED");
        return new()
        {
            ["QQ_LMAX_LAB_ENABLED"] = "true", ["QQ_LMAX_ENVIRONMENT"] = "Demo", ["QQ_LMAX_ACCOUNT_CODE"] = "1754288005",
            ["QQ_LMAX_ALLOW_EXTERNAL_CONNECTIONS"] = "true", ["QQ_LMAX_ALLOW_ORDER_SUBMISSION"] = "true",
            ["QQ_LMAX_ALLOW_LIVE_TRADING"] = "false", ["QQ_LMAX_DRY_RUN"] = "false", ["QQ_LMAX_USE_TLS"] = "true",
            // Retains the already-qualified Demo-only transport setting (#84, 5474763459 / 5474995054).
            ["QQ_LMAX_CERTIFICATE_REVOCATION_CHECK"] = "false", ["QQ_LMAX_SHOW_FIX_MESSAGES"] = "false",
            ["QQ_LMAX_FIX_ORDER_HOST"] = Get("ORDER_HOST"), ["QQ_LMAX_FIX_ORDER_PORT"] = Get("ORDER_PORT"),
            ["QQ_LMAX_FIX_MARKET_DATA_HOST"] = Get("MARKETDATA_HOST"), ["QQ_LMAX_FIX_MARKET_DATA_PORT"] = Get("MARKETDATA_PORT"),
            ["QQ_LMAX_FIX_ORDER_TARGET_COMP_ID"] = Get("ORDER_TARGET_COMP_ID"),
            ["QQ_LMAX_FIX_MARKET_DATA_TARGET_COMP_ID"] = Get("MARKETDATA_TARGET_COMP_ID"),
            ["QQ_LMAX_FIX_TARGET_COMP_ID"] = Get("ORDER_TARGET_COMP_ID"),
            // Logon uses username for tag 49; all subsequent order messages must use that same accepted identity.
            ["QQ_LMAX_FIX_SENDER_COMP_ID"] = Get("ORDER_USERNAME"), ["QQ_LMAX_FIX_USERNAME"] = Get("ORDER_USERNAME"),
            ["QQ_LMAX_FIX_PASSWORD"] = Get("ORDER_PASSWORD"),
            // Owner amendment: #84, issuecomment-5716970587. General portfolio risk controls remain active.
            ["QQ_LMAX_DEMO_ORDER_CAPS_ENABLED"] = "false",
            // Exact read-only LMAX probe rejected 263=0 and accepted 263=1 with SecurityId encoding.
            ["QQ_LMAX_MARKET_DATA_REQUEST_MODE"] = "SnapshotPlusUpdates",
            ["QQ_LMAX_MARKET_DATA_SYMBOL_ENCODING_MODE"] = "SecurityId",
            ["LmaxDemoCycle__Enabled"] = "true", ["LmaxDemoStrategyBridge__Enabled"] = "true", ["LmaxDemoContinuing__Enabled"] = "true",
            ["Worker__StopAfterInitialLmaxDemoCycle"] = "false", ["Safety__AllowExternalConnections"] = "true",
            ["Safety__AllowLiveTrading"] = "false", ["Safety__RequireFakeExecutionGateway"] = "false",
            ["Persistence__Provider"] = "SqlServerLocal", ["Database__ApplyMigrationsOnStartup"] = "false",
            ["Database__SeedReferenceDataOnStartup"] = "false", ["Database__SeedDemoDataOnStartup"] = "false",
            ["LocalScheduler__Enabled"] = "false", ["ModelWeights__PromoteReadyBatches"] = "false",
            ["LegacyAnubisPortfolio__Enabled"] = "false", ["LegacyAnubisWeights__Enabled"] = "false",
            ["LmaxCanonicalSnapshotIngestion__Enabled"] = "false", ["Intraday15m__Enabled"] = "false",
            ["MarketDataBars__Enabled"] = "false",
            ["ConnectionStrings__IntradaySqlServer"] = @"Server=(localdb)\MSSQLLocalDB;Database=QQProductionIntraday;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
        };
    }
    internal static async Task<Dictionary<string, string>> Load()
    {
        VerifyWorkerClosure();
        await Runtime.VerifyRole();
        using var response = JsonDocument.Parse(await Runtime.Aws(DateTimeOffset.UtcNow.AddSeconds(45), "ORDER_SECRET", "secretsmanager", "get-secret-value", "--secret-id", "qq/fund-platform/demo/lmax/fix-order-session"));
        using var secret = JsonDocument.Parse(response.RootElement.GetProperty("SecretString").GetString()!);
        return Bind(secret.RootElement);
    }
    internal static async Task InspectReference()
    {
        await using var db = new IntradayDbContext(new DbContextOptionsBuilder<IntradayDbContext>()
            .UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=QQProductionIntraday;Integrated Security=true;TrustServerCertificate=true;Application Name=QQ84UsdScopePreflight").Options);
        var state = await new SqlServerIntradayRepository(db).LoadStateAsync(default);
        var now = DateTimeOffset.UtcNow;
        var issues = LmaxDemoUsdExecutionUniverse.ConfigurationIssues(state, now)
            .Concat(LmaxDemoGmvRiskProfile.ConfigurationIssues(state, now)).ToArray();
        Console.WriteLine(JsonSerializer.Serialize(new { marker = "DEMO_USD_REFERENCE_PREFLIGHT", instruments = LmaxDemoUsdExecutionUniverse.Symbols,
            positionGmvUsd = LmaxDemoGmvRiskProfile.PositionGmvUsd, portfolioGmvUsd = LmaxDemoGmvRiskProfile.PortfolioGmvUsd,
            authorizedDays = Enum.GetNames<DayOfWeek>(), issues, passed = issues.Length == 0, databaseWrites = 0, brokerSends = 0 }));
        Files.Require(issues.Length == 0, "FULL_USD_REFERENCE_AND_APPROVED_GMV_CALENDAR_REQUIRED");
    }
    internal static async Task Inspect()
    {
        var environment = await Load();
        try
        {
            // The pinned Program.cs returns before DI registration, database access, or any FIX client creation.
            var result = await Runtime.Run(Runtime.Dotnet, [Worker, "--demo-config-inspect=true"], DateTimeOffset.UtcNow.AddSeconds(30), "WORKER_INSPECTION", environment: environment);
            using var document = JsonDocument.Parse(result);
            var root = document.RootElement;
            Files.Require(root.GetProperty("marker").GetString() == "DEMO_CONFIG_INSPECTION_ONLY"
                && new[] { "accountMatches", "demoEndpoint", "credentialsPresent", "senderMatches", "demoOrderCapsDisabled", "streamingMarketData", "securityIdMarketData" }.All(k => root.GetProperty(k).GetBoolean())
                && !root.GetProperty("brokerConnectionOpened").GetBoolean() && !root.GetProperty("databaseAccessed").GetBoolean(), "WORKER_BINDING_INSPECTION_FAILED");
            Console.WriteLine(result);
        }
        finally { environment.Clear(); }
    }

    private static async Task VerifyMarketData(IReadOnlyDictionary<string, string> environment)
    {
        foreach (var leg in LmaxDemoUsdExecutionUniverse.Legs)
        {
        // Worker exits before DI/database setup. Its quote adapter opens only the market-data connection.
        var result = await Runtime.Run(Runtime.Dotnet, [Worker, "--demo-marketdata-inspect=true", "--instrument=" + leg.Symbol,
            "--lmax-instrument-id=" + leg.SecurityId, "--slash-symbol=" + leg.SlashSymbol],
            DateTimeOffset.UtcNow.AddSeconds(40), "DEMO_MARKET_DATA_PREFLIGHT", environment: environment);
        using var document = JsonDocument.Parse(result);
        var root = document.RootElement;
        var observedAt = root.GetProperty("ObservedAtUtc").GetDateTimeOffset();
        var now = DateTimeOffset.UtcNow;
        Files.Require(root.GetProperty("marker").GetString() == "DEMO_MARKET_DATA_PREFLIGHT_PASS"
            && root.GetProperty("symbol").GetString() == leg.Symbol && root.GetProperty("securityId").GetString() == leg.SecurityId
            && root.GetProperty("BestBid").GetDecimal() > 0m
            && root.GetProperty("BestAsk").GetDecimal() > root.GetProperty("BestBid").GetDecimal()
            && observedAt <= now && now - observedAt <= TimeSpan.FromSeconds(60)
            && !root.GetProperty("orderConnectionOpened").GetBoolean()
            && root.GetProperty("orderSends").GetInt32() == 0 && !root.GetProperty("databaseAccessed").GetBoolean(),
            "DEMO_MARKET_DATA_PREFLIGHT_FAILED");
        Console.WriteLine(result);
        }
    }

    internal static async Task InspectMarketData()
    {
        var environment = await Load();
        try { await VerifyMarketData(environment); }
        finally { environment.Clear(); }
    }

    internal static void ValidateObservation(LmaxDemoSessionStart start, DateTimeOffset now)
    {
        if (start.ObservationSource != "OFFICIAL_UI")
        {
            Files.Require(start.ObservationSource == LmaxDemoOwnerConfirmedOpening.Source && LmaxDemoOwnerConfirmedOpening.IsApprovedReference(start.OwnerApprovalId), "UNKNOWN_OBSERVATION_SOURCE");
            LmaxDemoOwnerConfirmedOpening.ValidateFile(start.ObservationEvidencePath!, start.ObservationEvidenceSha256!, start.ObservedAtUtc, now);
        }
        Files.Require(!start.Simulated && start.Environment == "Demo" && start.AccountId == "1754288005"
            && start.InternalBrokerAccountCode == "LMAX_DEMO_LOCAL" && start.ObservedFlat && start.ObservedNoWorkingOrders && start.ExclusiveOrderActivityDeclared
            && !string.IsNullOrWhiteSpace(start.OwnerApprovalId) && !new[] { "NONE", "N/A", "PLACEHOLDER", "TBD", "UNKNOWN" }.Contains(start.OwnerApprovalId.Trim().ToUpperInvariant())
            && start.InitialObservationMaxAgeSeconds is > 0 and <= 900
            && now.Offset == TimeSpan.Zero && start.ObservedAtUtc.Offset == TimeSpan.Zero && start.DeadlineUtc.Offset == TimeSpan.Zero
            && start.ObservedAtUtc <= now && now - start.ObservedAtUtc <= TimeSpan.FromSeconds(start.InitialObservationMaxAgeSeconds)
            && start.DeadlineUtc == LmaxDemoDaySchedule.FinalClose(now) && start.DeadlineUtc > now && start.DeadlineUtc - now <= TimeSpan.FromHours(15)
            && LmaxDemoUsdExecutionUniverse.Matches(start.Instruments)
            && start.SessionId.Length is > 0 and <= 100 && start.SessionId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'), "GENUINE_CURRENT_STARTING_OBSERVATION_REQUIRED");
    }

    internal static async Task Start(string observationPath)
    {
        var observationHash = Files.Hash(observationPath);
        var start = JsonSerializer.Deserialize<LmaxDemoSessionStart>(File.ReadAllText(observationPath), Files.Json)
            ?? throw new InvalidOperationException("STARTING_OBSERVATION_MISSING");
        ValidateObservation(start, DateTimeOffset.UtcNow);
        await InspectReference();
        Files.Require(!Process.GetProcessesByName("QQ.Production.Intraday.Worker").Any(), "PRIOR_WORKER_MUST_BE_RECONCILED_AND_STOPPED");
        var environment = await Load();
        try
        {
            ValidateObservation(start, DateTimeOffset.UtcNow);
            Files.Require(Files.Hash(observationPath) == observationHash, "STARTING_OBSERVATION_MUTATED");
            await VerifyMarketData(environment);
            ValidateObservation(start, DateTimeOffset.UtcNow);
            environment["LmaxDemoContinuing__StartObservationPath"] = Path.GetFullPath(observationPath);
            var directory = Path.Combine(LmaxDemoSessionOwnership.RealAccountRoot, start.SessionId);
            Files.Require(!Directory.Exists(directory), "SESSION_START_REPLAY_BLOCKED");
            Directory.CreateDirectory(directory);
            Files.Atomic(Path.Combine(directory, "launch-attempt.json"), new { start.SessionId, observationHash, workerHash = WorkerHash, atUtc = DateTimeOffset.UtcNow, automaticRetryAllowed = false });
            var info = Runtime.StartInfo(Runtime.Dotnet, [Worker], environment);
            info.WorkingDirectory = Path.GetDirectoryName(Worker)!;
            using var process = Process.Start(info) ?? throw new InvalidOperationException("WORKER_START_FAILED");
            environment.Clear();
            await using var stdout = new FileStream(Path.Combine(directory, "worker.stdout.txt"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            await using var stderr = new FileStream(Path.Combine(directory, "worker.stderr.txt"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            var outputCopy = process.StandardOutput.BaseStream.CopyToAsync(stdout);
            var errorCopy = process.StandardError.BaseStream.CopyToAsync(stderr);
            Files.Atomic(Path.Combine(directory, "worker-process.json"), new { start.SessionId, process.Id, startedAtUtc = DateTimeOffset.UtcNow, workerHash = WorkerHash });
            Console.WriteLine(JsonSerializer.Serialize(new { marker = "DEMO_WORKER_PROCESS_STARTED", start.SessionId, process.Id, observationHash, cycleHandoffs = 0 }));
            // Retain supervision and output drains until the actual Worker exits. Never kill or restart an unresolved account owner.
            await process.WaitForExitAsync();
            await Task.WhenAll(outputCopy, errorCopy);
            Console.WriteLine(JsonSerializer.Serialize(new { marker = "DEMO_WORKER_PROCESS_EXITED", start.SessionId, process.ExitCode, reconciliationRequired = true }));
        }
        finally { environment.Clear(); }
    }
}
