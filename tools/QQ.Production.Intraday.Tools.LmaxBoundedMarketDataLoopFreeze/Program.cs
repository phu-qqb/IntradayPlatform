using QQ.Production.Intraday.Application;

var cli = CliOptions.Parse(args);
if (cli.ShowHelp)
{
    Console.WriteLine("Usage: dotnet run --project tools/QQ.Production.Intraday.Tools.LmaxBoundedMarketDataLoopFreeze -- --run-key lmax-bounded-md-loop-demo-002-freeze-001 --source-run-key lmax-bounded-md-loop-demo-002 --source-root artifacts/lmax-sandbox-marketdata/lmax-bounded-md-loop-demo-002 --output-root artifacts/lmax-sandbox-marketdata/lmax-bounded-md-loop-demo-002-freeze-001");
    return 0;
}

var runKey = cli.RunKey ?? "lmax-bounded-md-loop-demo-002-freeze-001";
var sourceRunKey = cli.SourceRunKey ?? "lmax-bounded-md-loop-demo-002";
var sourceRoot = Path.GetFullPath(cli.SourceRoot ?? Path.Combine("artifacts", "lmax-sandbox-marketdata", sourceRunKey));
var outputRoot = Path.GetFullPath(cli.OutputRoot ?? Path.Combine("artifacts", "lmax-sandbox-marketdata", runKey));
RefuseOverwrite(outputRoot);

var result = await new LmaxBoundedMarketDataLoopFreezeWriter().WriteAsync(
    new LmaxBoundedMarketDataLoopFreezeOptions(runKey, outputRoot, sourceRunKey, sourceRoot),
    CancellationToken.None);

Console.WriteLine($"LMAX_BOUNDED_LOOP_DEMO_FREEZE_STATUS={result.FreezeReport["LMAX_BOUNDED_LOOP_DEMO_FREEZE_STATUS"]}");
Console.WriteLine($"LMAX_BOUNDED_LOOP_DEMO_STATUS={result.FreezeReport["LMAX_BOUNDED_LOOP_DEMO_STATUS"]}");
Console.WriteLine($"FIRST_STEP_FETCH_LIVE_MARKET_DATA_LOOP_STATUS={result.FreezeReport["FIRST_STEP_FETCH_LIVE_MARKET_DATA_LOOP_STATUS"]}");
Console.WriteLine($"PRIMARY_FAILURE_REASON={result.FreezeReport["PRIMARY_FAILURE_REASON"]}");
Console.WriteLine($"SECRET_SCAN_STATUS={result.SecretScanReport["SECRET_SCAN_STATUS"]}");
Console.WriteLine($"BOUNDARY_STATUS={result.BoundaryVerification["BOUNDARY_STATUS"]}");
Console.WriteLine($"MARKETDATA_LMAX_DB_STATUS={result.FreezeReport["MARKETDATA_LMAX_DB_STATUS"]}");
Console.WriteLine($"summary={Path.Combine(outputRoot, "share", "lmax_bounded_loop_demo_freeze_summary.md")}");

return string.Equals(result.FreezeReport["LMAX_BOUNDED_LOOP_DEMO_FREEZE_STATUS"].ToString(), "FAIL", StringComparison.Ordinal) ? 2 : 0;

static void RefuseOverwrite(string outputRoot)
{
    var protectedFiles = new[]
    {
        Path.Combine(outputRoot, "10_validation", "lmax_bounded_loop_demo_freeze_report.json"),
        Path.Combine(outputRoot, "manifest.json"),
        Path.Combine(outputRoot, "hashes.json"),
        Path.Combine(outputRoot, "manifest.sha256")
    };

    var existing = protectedFiles.FirstOrDefault(File.Exists);
    if (existing is not null)
    {
        throw new IOException($"LMAX bounded loop demo freeze refuses to overwrite existing artifact: {existing}");
    }
}

internal sealed record CliOptions(
    string? RunKey,
    string? OutputRoot,
    string? SourceRunKey,
    string? SourceRoot,
    bool ShowHelp)
{
    public static CliOptions Parse(string[] args)
    {
        string? runKey = null;
        string? outputRoot = null;
        string? sourceRunKey = null;
        string? sourceRoot = null;
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
                case "--source-run-key": sourceRunKey = Next(); break;
                case "--source-root": sourceRoot = Next(); break;
            }
        }

        return new(runKey, outputRoot, sourceRunKey, sourceRoot, showHelp);
    }
}
