using QQ.Production.Intraday.Application;

var cli = CliOptions.Parse(args);
if (cli.ShowHelp)
{
    Console.WriteLine("Usage: dotnet run --project tools/QQ.Production.Intraday.Tools.LmaxBoundedMarketDataLoop -- --run-key <RunKey> --output-root <path> --environment demo --no-external true --no-execution true --use-fake-fix-server true --duration-seconds 30 --max-messages 100 --instrument GBPUSD");
    return 0;
}

var runKey = cli.RunKey ?? "lmax-bounded-md-loop-fake-001";
var outputRoot = Path.GetFullPath(cli.OutputRoot ?? Path.Combine("artifacts", "lmax-sandbox-marketdata", runKey));
RefuseOverwrite(outputRoot);

var options = new LmaxBoundedMarketDataLoopOptions(
    runKey,
    outputRoot,
    cli.Environment ?? LmaxBoundedMarketDataLoopOptions.DemoEnvironment,
    cli.Host ?? LmaxBoundedMarketDataLoopOptions.DemoMarketDataHost,
    cli.Port ?? 443,
    cli.TargetCompId ?? LmaxBoundedMarketDataLoopOptions.DemoMarketDataTargetCompId,
    cli.SenderCompId ?? "QQPRODMD",
    cli.UsernameSecretRef ?? "LMAX_DEMO_MD_USERNAME",
    cli.PasswordSecretRef ?? "LMAX_DEMO_MD_PASSWORD",
    cli.NoExternal,
    cli.ExternalApproved,
    cli.OperatorApprovalPhrase,
    cli.NoExecution,
    cli.UseFakeFixServer,
    cli.DurationSeconds ?? 30,
    cli.MaxMessages ?? 100,
    cli.MdUpdateType ?? 0,
    cli.Instruments.Count == 0 ? ["GBPUSD"] : cli.Instruments);

var result = await new LmaxBoundedMarketDataLoopRunner().RunAsync(options, CancellationToken.None);
await new LmaxBoundedMarketDataLoopReports().WriteAsync(options, result, CancellationToken.None);

Console.WriteLine($"LMAX_BOUNDED_LOOP_DEMO_STATUS={result.CaptureReport["LMAX_BOUNDED_LOOP_DEMO_STATUS"]}");
Console.WriteLine($"LMAX_BOUNDED_LOOP_FAKE_STATUS={result.CaptureReport["LMAX_BOUNDED_LOOP_FAKE_STATUS"]}");
Console.WriteLine($"LMAX_BOUNDED_LOOP_EXTERNAL_CALLS_ATTEMPTED={result.CaptureReport["LMAX_BOUNDED_LOOP_EXTERNAL_CALLS_ATTEMPTED"]}");
Console.WriteLine($"LMAX_BOUNDED_LOOP_ARTIFACTS_ONLY={result.CaptureReport["LMAX_BOUNDED_LOOP_ARTIFACTS_ONLY"]}");
Console.WriteLine($"LMAX_BOUNDED_LOOP_NO_DB_WRITE={result.CaptureReport["LMAX_BOUNDED_LOOP_NO_DB_WRITE"]}");
Console.WriteLine($"LMAX_BOUNDED_LOOP_NO_EXECUTION={result.CaptureReport["LMAX_BOUNDED_LOOP_NO_EXECUTION"]}");
Console.WriteLine($"LMAX_BOUNDED_LOOP_NO_PRODUCTION_ENDPOINT={result.CaptureReport["LMAX_BOUNDED_LOOP_NO_PRODUCTION_ENDPOINT"]}");
Console.WriteLine($"LMAX_BOUNDED_LOOP_NO_TRADING_ENDPOINT={result.CaptureReport["LMAX_BOUNDED_LOOP_NO_TRADING_ENDPOINT"]}");
Console.WriteLine($"LMAX_BOUNDED_LOOP_NO_SCHEDULER={result.CaptureReport["LMAX_BOUNDED_LOOP_NO_SCHEDULER"]}");
Console.WriteLine($"MARKETDATA_LMAX_DB_STATUS={result.CaptureReport["MARKETDATA_LMAX_DB_STATUS"]}");
Console.WriteLine($"FIRST_STEP_FETCH_LIVE_MARKET_DATA_LOOP_STATUS={result.CaptureReport["FIRST_STEP_FETCH_LIVE_MARKET_DATA_LOOP_STATUS"]}");
Console.WriteLine($"PRIMARY_FAILURE_REASON={result.CaptureReport["PRIMARY_FAILURE_REASON"]}");
Console.WriteLine($"summary={Path.Combine(outputRoot, "marketdata", "lmax_marketdata_summary.json")}");

return result.Summary.Status is "PASS" or "WARN" or "NOT_ATTEMPTED" ? 0 : 2;

static void RefuseOverwrite(string outputRoot)
{
    var protectedFiles = new[]
    {
        Path.Combine(outputRoot, "marketdata", "lmax_marketdata_events.jsonl"),
        Path.Combine(outputRoot, "marketdata", "lmax_marketdata_summary.json"),
        Path.Combine(outputRoot, "10_validation", "lmax_marketdata_loop_capture_report.json"),
        Path.Combine(outputRoot, "manifest.json"),
        Path.Combine(outputRoot, "hashes.json"),
        Path.Combine(outputRoot, "manifest.sha256")
    };

    var existing = protectedFiles.FirstOrDefault(File.Exists);
    if (existing is not null)
    {
        throw new IOException($"LMAX bounded MarketData loop refuses to overwrite existing artifact: {existing}");
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
    bool NoExternal,
    bool ExternalApproved,
    string? OperatorApprovalPhrase,
    bool NoExecution,
    bool UseFakeFixServer,
    int? DurationSeconds,
    int? MaxMessages,
    int? MdUpdateType,
    List<string> Instruments,
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
        var noExternal = false;
        var externalApproved = false;
        string? operatorApprovalPhrase = null;
        var noExecution = false;
        var useFakeFixServer = false;
        int? durationSeconds = null;
        int? maxMessages = null;
        int? mdUpdateType = null;
        var instruments = new List<string>();
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
                case "--no-external": noExternal = ParseBool(Next()); break;
                case "--external-approved": externalApproved = ParseBool(Next()); break;
                case "--operator-approval-phrase": operatorApprovalPhrase = Next(); break;
                case "--no-execution": noExecution = ParseBool(Next()); break;
                case "--use-fake-fix-server": useFakeFixServer = ParseBool(Next()); break;
                case "--duration-seconds": durationSeconds = ParseInt(Next()); break;
                case "--max-messages": maxMessages = ParseInt(Next()); break;
                case "--md-update-type": mdUpdateType = ParseInt(Next()); break;
                case "--instrument":
                    var instrument = Next();
                    if (!string.IsNullOrWhiteSpace(instrument))
                    {
                        instruments.Add(instrument);
                    }

                    break;
            }
        }

        return new(runKey, outputRoot, environment, host, port, targetCompId, senderCompId, usernameSecretRef, passwordSecretRef, noExternal, externalApproved, operatorApprovalPhrase, noExecution, useFakeFixServer, durationSeconds, maxMessages, mdUpdateType, instruments, showHelp);
    }

    private static bool ParseBool(string? value)
        => bool.TryParse(value, out var parsed) && parsed;

    private static int? ParseInt(string? value)
        => int.TryParse(value, out var parsed) ? parsed : null;
}
