using QQ.Production.Intraday.Application;

var cli = CliOptions.Parse(args);
if (cli.ShowHelp)
{
    Console.WriteLine("Usage: dotnet run --project tools/QQ.Production.Intraday.Tools.LmaxSandboxMarketDataCollector -- --run-key <RunKey> --output-root <path> --environment demo --no-external true --no-execution true --use-fake-fix-server true --duration-seconds 30 --max-messages 100 --instrument GBPUSD --md-update-type 0");
    return 0;
}

var runKey = cli.RunKey ?? $"lmax-sandbox-md-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
var outputRoot = Path.GetFullPath(cli.OutputRoot ?? Path.Combine("artifacts", "lmax-sandbox-marketdata", runKey));
RefuseOverwrite(outputRoot);

var options = new LmaxSandboxMarketDataOptions(
    runKey,
    outputRoot,
    cli.Environment ?? LmaxSandboxMarketDataOptions.DemoEnvironment,
    cli.Host ?? LmaxSandboxMarketDataOptions.DemoMarketDataHost,
    cli.Port ?? 443,
    cli.TargetCompId ?? LmaxSandboxMarketDataOptions.DemoMarketDataTargetCompId,
    cli.SenderCompId ?? "QQPRODMD",
    cli.UsernameSecretRef ?? "LMAX_DEMO_MD_USERNAME",
    cli.PasswordSecretRef ?? "LMAX_DEMO_MD_PASSWORD",
    cli.Instruments.Count == 0 ? ["GBPUSD"] : cli.Instruments,
    cli.DurationSeconds ?? 30,
    cli.MaxMessages ?? 100,
    cli.ConnectTimeoutMs ?? 10_000,
    cli.ReadTimeoutMs ?? 2_000,
    cli.HeartbeatIntervalSeconds ?? 30,
    cli.MdUpdateType ?? 0,
    cli.NoExternal,
    cli.ExternalApproved,
    cli.OperatorApprovalPhrase,
    cli.NoExecution,
    cli.DisableOrderPath,
    cli.UseFakeFixServer);

IFixTransport transport = options.UseFakeFixServer
    ? new FakeFixTransport()
    : new TlsTcpFixTransport();

var collector = new LmaxSandboxMarketDataCollector(transport);
var result = await collector.RunAsync(options, CancellationToken.None);
await new LmaxSandboxMarketDataArtifactWriter().WriteAsync(options, result, CancellationToken.None);

Console.WriteLine($"LMAX_SANDBOX_PREFLIGHT_GATE={result.Preflight.LmaxSandboxPreflightGate}");
Console.WriteLine($"LMAX_SANDBOX_FAKE_SERVER_TESTS_PASS={(options.UseFakeFixServer && options.NoExternal && result.Summary.MessagesRead > 0 && !result.ExternalCallsAttempted && result.Failures.Count == 0 ? "YES" : "NO")}");
Console.WriteLine($"LMAX_EXTERNAL_CALLS_ATTEMPTED={(result.ExternalCallsAttempted ? "YES" : "NO")}");
Console.WriteLine($"LMAX_ORDER_PATH_DISABLED={(options.DisableOrderPath ? "YES" : "NO")}");
Console.WriteLine($"LMAX_TRADING_ENDPOINT_BLOCKED={(result.Preflight.TradingEndpointBlocked ? "YES" : "NO")}");
Console.WriteLine("LMAX_DB_PERSISTENCE_ENABLED=NO");
Console.WriteLine($"summary={Path.Combine(outputRoot, "marketdata", "lmax_marketdata_summary.json")}");

return result.Preflight.LmaxSandboxPreflightGate == "PASS" && result.Failures.Count == 0 ? 0 : 2;

static void RefuseOverwrite(string outputRoot)
{
    var protectedFiles = new[]
    {
        Path.Combine(outputRoot, "marketdata", "lmax_marketdata_events.jsonl"),
        Path.Combine(outputRoot, "marketdata", "lmax_marketdata_summary.json"),
        Path.Combine(outputRoot, "10_validation", "lmax_sandbox_marketdata_capture_report.json"),
        Path.Combine(outputRoot, "manifest.json"),
        Path.Combine(outputRoot, "hashes.json"),
        Path.Combine(outputRoot, "manifest.sha256")
    };

    var existing = protectedFiles.FirstOrDefault(File.Exists);
    if (existing is not null)
    {
        throw new IOException($"LMAX sandbox market-data collector refuses to overwrite existing artifact: {existing}");
    }
}

internal sealed record CliOptions(
    string? RunKey,
    string? OutputRoot,
    string? Environment,
    string? Host,
    int? Port,
    string? TargetCompId,
    string? SenderCompId,
    string? UsernameSecretRef,
    string? PasswordSecretRef,
    List<string> Instruments,
    int? DurationSeconds,
    int? MaxMessages,
    int? ConnectTimeoutMs,
    int? ReadTimeoutMs,
    int? HeartbeatIntervalSeconds,
    int? MdUpdateType,
    bool NoExternal,
    bool ExternalApproved,
    string? OperatorApprovalPhrase,
    bool NoExecution,
    bool DisableOrderPath,
    bool UseFakeFixServer,
    bool ShowHelp)
{
    public static CliOptions Parse(string[] args)
    {
        string? runKey = null;
        string? outputRoot = null;
        string? environment = null;
        string? host = null;
        int? port = null;
        string? targetCompId = null;
        string? senderCompId = null;
        string? usernameSecretRef = null;
        string? passwordSecretRef = null;
        var instruments = new List<string>();
        int? durationSeconds = null;
        int? maxMessages = null;
        int? connectTimeoutMs = null;
        int? readTimeoutMs = null;
        int? heartbeatIntervalSeconds = null;
        int? mdUpdateType = null;
        var noExternal = false;
        var externalApproved = false;
        string? operatorApprovalPhrase = null;
        var noExecution = false;
        var disableOrderPath = true;
        var useFakeFixServer = false;
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
                case "--run-key": runKey = Next(); break;
                case "--output-root": outputRoot = Next(); break;
                case "--environment": environment = Next(); break;
                case "--host": host = Next(); break;
                case "--port": port = ParseInt(Next()); break;
                case "--target-comp-id": targetCompId = Next(); break;
                case "--sender-comp-id": senderCompId = Next(); break;
                case "--username-secret-ref": usernameSecretRef = Next(); break;
                case "--password-secret-ref": passwordSecretRef = Next(); break;
                case "--instrument":
                    var instrument = Next();
                    if (!string.IsNullOrWhiteSpace(instrument)) instruments.Add(instrument);
                    break;
                case "--duration-seconds": durationSeconds = ParseInt(Next()); break;
                case "--max-messages": maxMessages = ParseInt(Next()); break;
                case "--connect-timeout-ms": connectTimeoutMs = ParseInt(Next()); break;
                case "--read-timeout-ms": readTimeoutMs = ParseInt(Next()); break;
                case "--heartbeat-interval-seconds": heartbeatIntervalSeconds = ParseInt(Next()); break;
                case "--md-update-type": mdUpdateType = ParseInt(Next()); break;
                case "--no-external": noExternal = ParseBool(Next()); break;
                case "--external-approved": externalApproved = ParseBool(Next()); break;
                case "--operator-approval-phrase": operatorApprovalPhrase = Next(); break;
                case "--no-execution": noExecution = ParseBool(Next()); break;
                case "--disable-order-path": disableOrderPath = ParseBool(Next()); break;
                case "--use-fake-fix-server": useFakeFixServer = ParseBool(Next()); break;
            }
        }

        return new CliOptions(
            runKey,
            outputRoot,
            environment,
            host,
            port,
            targetCompId,
            senderCompId,
            usernameSecretRef,
            passwordSecretRef,
            instruments,
            durationSeconds,
            maxMessages,
            connectTimeoutMs,
            readTimeoutMs,
            heartbeatIntervalSeconds,
            mdUpdateType,
            noExternal,
            externalApproved,
            operatorApprovalPhrase,
            noExecution,
            disableOrderPath,
            useFakeFixServer,
            showHelp);
    }

    private static bool ParseBool(string? value)
        => bool.TryParse(value, out var parsed) && parsed;

    private static int? ParseInt(string? value)
        => int.TryParse(value, out var parsed) ? parsed : null;
}
