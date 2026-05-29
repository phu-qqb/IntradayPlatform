using QQ.Production.Intraday.Application;

var cli = CliOptions.Parse(args);
if (cli.ShowHelp)
{
    Console.WriteLine("Usage: dotnet run --project tools/QQ.Production.Intraday.Tools.MarketDataStep1CrossThreadStatus -- --run-key marketdata-step1-status-001 --output-root artifacts/cross-thread-status/marketdata-step1-status-001 --readiness-package-root artifacts/lmax-sandbox-marketdata/lmax-step1-readiness-001 --source-run-key lmax-sandbox-md-r002-demo-retry-005 --no-external true --no-execution true");
    return 0;
}

if (!cli.NoExternal || !cli.NoExecution)
{
    Console.Error.WriteLine("This status writer requires --no-external true --no-execution true.");
    return 2;
}

var runKey = cli.RunKey ?? "marketdata-step1-status-001";
var outputRoot = Path.GetFullPath(cli.OutputRoot ?? Path.Combine("artifacts", "cross-thread-status", runKey));
var readinessRoot = Path.GetFullPath(cli.ReadinessPackageRoot ?? Path.Combine("artifacts", "lmax-sandbox-marketdata", "lmax-step1-readiness-001"));
var sourceRunKey = cli.SourceRunKey ?? "lmax-sandbox-md-r002-demo-retry-005";

RefuseOverwrite(outputRoot);

var result = await new MarketDataStep1CrossThreadStatusWriter().WriteAsync(
    new MarketDataStep1CrossThreadStatusOptions(runKey, outputRoot, readinessRoot, sourceRunKey),
    CancellationToken.None);

Console.WriteLine($"STEP1_FETCH_LIVE_MARKET_DATA_SANDBOX_STATUS={result.Report["STEP1_FETCH_LIVE_MARKET_DATA_SANDBOX_STATUS"]}");
Console.WriteLine($"STEP1_FETCH_LIVE_MARKET_DATA_PRODUCTION_STATUS={result.Report["STEP1_FETCH_LIVE_MARKET_DATA_PRODUCTION_STATUS"]}");
Console.WriteLine($"MARKETDATA_LMAX_DB_STATUS={result.Report["MARKETDATA_LMAX_DB_STATUS"]}");
Console.WriteLine($"NO_PRODUCTION_EXECUTION_PATH={result.Report["NO_PRODUCTION_EXECUTION_PATH"]}");
Console.WriteLine($"NO_CREDENTIAL_VALUES_PERSISTED={result.Report["NO_CREDENTIAL_VALUES_PERSISTED"]}");
Console.WriteLine($"report={Path.Combine(outputRoot, "10_validation", "marketdata_step1_cross_thread_status_report.json")}");

return 0;

static void RefuseOverwrite(string outputRoot)
{
    var protectedFiles = new[]
    {
        Path.Combine(outputRoot, "10_validation", "marketdata_step1_cross_thread_status_report.json"),
        Path.Combine(outputRoot, "manifest.json"),
        Path.Combine(outputRoot, "hashes.json"),
        Path.Combine(outputRoot, "manifest.sha256")
    };

    var existing = protectedFiles.FirstOrDefault(File.Exists);
    if (existing is not null)
    {
        throw new IOException($"MarketData step1 cross-thread status writer refuses to overwrite existing artifact: {existing}");
    }
}

internal sealed record CliOptions(
    string? RunKey,
    string? OutputRoot,
    string? ReadinessPackageRoot,
    string? SourceRunKey,
    bool NoExternal,
    bool NoExecution,
    bool ShowHelp)
{
    public static CliOptions Parse(string[] args)
    {
        string? runKey = null;
        string? outputRoot = null;
        string? readinessPackageRoot = null;
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
                case "--readiness-package-root": readinessPackageRoot = Next(); break;
                case "--source-run-key": sourceRunKey = Next(); break;
                case "--no-external": noExternal = ParseBool(Next()); break;
                case "--no-execution": noExecution = ParseBool(Next()); break;
            }
        }

        return new(runKey, outputRoot, readinessPackageRoot, sourceRunKey, noExternal, noExecution, showHelp);
    }

    private static bool ParseBool(string? value)
        => bool.TryParse(value, out var parsed) && parsed;
}
