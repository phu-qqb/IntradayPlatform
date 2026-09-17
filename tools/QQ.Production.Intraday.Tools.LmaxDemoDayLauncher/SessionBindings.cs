using System.Diagnostics;
using System.Text.Json;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tools.LmaxDemoDayLauncher;

internal static class SessionBindings
{
    internal const string Worker = @"C:\deploy\IntradayPlatform\staging\lmax-demo-uncapped-20260917\src\QQ.Production.Intraday.Worker\bin\Release\net10.0\QQ.Production.Intraday.Worker.dll";
    internal const string WorkerHash = "1979993286309f733b4315b4a4ba5f563c6f5c8aa544a8c30a7cb34deb119f0a";

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
        Files.Require(Files.Hash(Worker) == WorkerHash, "WORKER_PIN_MISMATCH");
        await Runtime.VerifyRole();
        using var response = JsonDocument.Parse(await Runtime.Aws(DateTimeOffset.UtcNow.AddSeconds(45), "ORDER_SECRET", "secretsmanager", "get-secret-value", "--secret-id", "qq/fund-platform/demo/lmax/fix-order-session"));
        using var secret = JsonDocument.Parse(response.RootElement.GetProperty("SecretString").GetString()!);
        return Bind(secret.RootElement);
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
                && new[] { "accountMatches", "demoEndpoint", "credentialsPresent", "senderMatches", "demoOrderCapsDisabled" }.All(k => root.GetProperty(k).GetBoolean())
                && !root.GetProperty("brokerConnectionOpened").GetBoolean() && !root.GetProperty("databaseAccessed").GetBoolean(), "WORKER_BINDING_INSPECTION_FAILED");
            Console.WriteLine(result);
        }
        finally { environment.Clear(); }
    }

    internal static void ValidateObservation(LmaxDemoSessionStart start, DateTimeOffset now)
    {
        Files.Require(!start.Simulated && start.Environment == "Demo" && start.AccountId == "1754288005"
            && start.InternalBrokerAccountCode == "LMAX_DEMO_LOCAL" && start.ObservedFlat && start.ObservedNoWorkingOrders && start.ExclusiveOrderActivityDeclared
            && !string.IsNullOrWhiteSpace(start.OwnerApprovalId) && !new[] { "NONE", "N/A", "PLACEHOLDER", "TBD", "UNKNOWN" }.Contains(start.OwnerApprovalId.Trim().ToUpperInvariant())
            && start.InitialObservationMaxAgeSeconds is > 0 and <= 900
            && now.Offset == TimeSpan.Zero && start.ObservedAtUtc.Offset == TimeSpan.Zero && start.DeadlineUtc.Offset == TimeSpan.Zero
            && start.ObservedAtUtc <= now && now - start.ObservedAtUtc <= TimeSpan.FromSeconds(start.InitialObservationMaxAgeSeconds)
            && start.DeadlineUtc == LmaxDemoDaySchedule.FinalClose(now) && start.DeadlineUtc > now && start.DeadlineUtc - now <= TimeSpan.FromHours(15)
            && start.Instruments.Count == 1 && start.Instruments[0] == new LmaxDemoSessionInstrument("EURUSD", "4001", 10000m)
            && start.SessionId.Length is > 0 and <= 100 && start.SessionId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'), "GENUINE_CURRENT_STARTING_OBSERVATION_REQUIRED");
    }

    internal static async Task Start(string observationPath)
    {
        var observationHash = Files.Hash(observationPath);
        var start = JsonSerializer.Deserialize<LmaxDemoSessionStart>(File.ReadAllText(observationPath), Files.Json)
            ?? throw new InvalidOperationException("STARTING_OBSERVATION_MISSING");
        ValidateObservation(start, DateTimeOffset.UtcNow);
        Files.Require(!Process.GetProcessesByName("QQ.Production.Intraday.Worker").Any(), "PRIOR_WORKER_MUST_BE_RECONCILED_AND_STOPPED");
        var environment = await Load();
        try
        {
            ValidateObservation(start, DateTimeOffset.UtcNow);
            Files.Require(Files.Hash(observationPath) == observationHash, "STARTING_OBSERVATION_MUTATED");
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
