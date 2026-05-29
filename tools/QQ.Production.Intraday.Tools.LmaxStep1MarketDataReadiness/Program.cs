using QQ.Production.Intraday.Application;

var cli = CliOptions.Parse(args);
if (cli.ShowHelp)
{
    Console.WriteLine("Usage: dotnet run --project tools/QQ.Production.Intraday.Tools.LmaxStep1MarketDataReadiness -- --run-key <RunKey> --output-root <path> --source-run-root <path> --source-run-key <RunKey> --no-external true --no-execution true");
    return 0;
}

if (!cli.NoExternal || !cli.NoExecution)
{
    Console.Error.WriteLine("This readiness writer is status-only and requires --no-external true --no-execution true.");
    return 2;
}

var runKey = cli.RunKey ?? "lmax-step1-readiness-001";
var outputRoot = Path.GetFullPath(cli.OutputRoot ?? Path.Combine("artifacts", "lmax-sandbox-marketdata", runKey));
var sourceRunKey = cli.SourceRunKey ?? "lmax-sandbox-md-r002-demo-retry-005";
var sourceRunRoot = Path.GetFullPath(cli.SourceRunRoot ?? Path.Combine("artifacts", "lmax-sandbox-marketdata", sourceRunKey));

RefuseOverwrite(outputRoot);

var result = await new LmaxStep1MarketDataReadinessWriter().WriteAsync(
    new LmaxStep1ReadinessOptions(runKey, outputRoot, sourceRunRoot, sourceRunKey),
    CancellationToken.None);

Console.WriteLine($"STEP1_FETCH_LIVE_MARKET_DATA_SANDBOX_STATUS={result.Report.Step1FetchLiveMarketDataSandboxStatus}");
Console.WriteLine($"STEP1_FETCH_LIVE_MARKET_DATA_PRODUCTION_STATUS={result.Report.Step1FetchLiveMarketDataProductionStatus}");
Console.WriteLine($"MARKETDATA_LMAX_DB_STATUS={result.Report.MarketDataLmaxDbStatus}");
Console.WriteLine($"NO_PRODUCTION_EXECUTION_PATH={result.Report.NoProductionExecutionPath}");
Console.WriteLine($"NO_CREDENTIAL_VALUES_PERSISTED={result.Report.NoCredentialValuesPersisted}");
Console.WriteLine($"report={Path.Combine(outputRoot, "10_validation", "lmax_step1_marketdata_readiness_report.json")}");

return result.Report.Step1FetchLiveMarketDataSandboxStatus == "PASS" &&
       result.Report.Step1FetchLiveMarketDataProductionStatus == "BLOCKED" &&
       result.Report.NoProductionExecutionPath == "PASS" &&
       result.Report.NoCredentialValuesPersisted == "YES"
    ? 0
    : 2;

static void RefuseOverwrite(string outputRoot)
{
    var protectedFiles = new[]
    {
        Path.Combine(outputRoot, "10_validation", "lmax_step1_marketdata_readiness_report.json"),
        Path.Combine(outputRoot, "manifest.json"),
        Path.Combine(outputRoot, "hashes.json"),
        Path.Combine(outputRoot, "manifest.sha256")
    };

    var existing = protectedFiles.FirstOrDefault(File.Exists);
    if (existing is not null)
    {
        throw new IOException($"LMAX step1 readiness writer refuses to overwrite existing artifact: {existing}");
    }
}

internal sealed record CliOptions(
    string? RunKey,
    string? OutputRoot,
    string? SourceRunRoot,
    string? SourceRunKey,
    bool NoExternal,
    bool NoExecution,
    bool ShowHelp)
{
    public static CliOptions Parse(string[] args)
    {
        string? runKey = null;
        string? outputRoot = null;
        string? sourceRunRoot = null;
        string? sourceRunKey = null;
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
                case "--run-key": runKey = Next(); break;
                case "--output-root": outputRoot = Next(); break;
                case "--source-run-root": sourceRunRoot = Next(); break;
                case "--source-run-key": sourceRunKey = Next(); break;
                case "--no-external": noExternal = ParseBool(Next()); break;
                case "--no-execution": noExecution = ParseBool(Next()); break;
            }
        }

        return new(runKey, outputRoot, sourceRunRoot, sourceRunKey, noExternal, noExecution, showHelp);
    }

    private static bool ParseBool(string? value)
        => bool.TryParse(value, out var parsed) && parsed;
}
