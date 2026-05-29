using QQ.Production.Intraday.Application;

var cli = CliOptions.Parse(args);
if (cli.ShowHelp)
{
    Console.WriteLine("Usage: dotnet run --project tools/QQ.Production.Intraday.Tools.LmaxBoundedMarketDataLoopDesign -- --run-key lmax-bounded-md-loop-design-001 --repo-root . --output-root artifacts/lmax-sandbox-marketdata/lmax-bounded-md-loop-design-001 --no-external true --no-execution true");
    return 0;
}

if (!cli.NoExternal || !cli.NoExecution)
{
    Console.Error.WriteLine("LMAX bounded MarketData loop design is status-only and requires --no-external true --no-execution true.");
    return 2;
}

var runKey = cli.RunKey ?? "lmax-bounded-md-loop-design-001";
var repoRoot = Path.GetFullPath(cli.RepoRoot ?? ".");
var outputRoot = Path.GetFullPath(cli.OutputRoot ?? Path.Combine("artifacts", "lmax-sandbox-marketdata", runKey));

RefuseOverwrite(outputRoot);

var result = await new LmaxBoundedMarketDataLoopDesignWriter().WriteAsync(
    new LmaxBoundedMarketDataLoopDesignOptions(runKey, outputRoot, repoRoot),
    CancellationToken.None);

Console.WriteLine($"LMAX_BOUNDED_LOOP_DESIGN_STATUS={result.Design["LMAX_BOUNDED_LOOP_DESIGN_STATUS"]}");
Console.WriteLine($"LMAX_BOUNDED_LOOP_ARTIFACTS_ONLY={result.Design["LMAX_BOUNDED_LOOP_ARTIFACTS_ONLY"]}");
Console.WriteLine($"LMAX_BOUNDED_LOOP_NO_DB_WRITE={result.Design["LMAX_BOUNDED_LOOP_NO_DB_WRITE"]}");
Console.WriteLine($"LMAX_BOUNDED_LOOP_NO_EXECUTION={result.Design["LMAX_BOUNDED_LOOP_NO_EXECUTION"]}");
Console.WriteLine($"MARKETDATA_LMAX_DB_STATUS={result.Design["MARKETDATA_LMAX_DB_STATUS"]}");
Console.WriteLine($"SAFE_NEXT_PHASE={result.Design["SAFE_NEXT_PHASE"]}");
Console.WriteLine($"report={Path.Combine(outputRoot, "10_validation", "lmax_bounded_marketdata_loop_design.json")}");

return string.Equals(result.Design["LMAX_BOUNDED_LOOP_DESIGN_STATUS"].ToString(), "PASS", StringComparison.Ordinal) ? 0 : 2;

static void RefuseOverwrite(string outputRoot)
{
    var protectedFiles = new[]
    {
        Path.Combine(outputRoot, "10_validation", "lmax_bounded_marketdata_loop_design.json"),
        Path.Combine(outputRoot, "manifest.json"),
        Path.Combine(outputRoot, "hashes.json"),
        Path.Combine(outputRoot, "manifest.sha256")
    };

    var existing = protectedFiles.FirstOrDefault(File.Exists);
    if (existing is not null)
    {
        throw new IOException($"LMAX bounded MarketData loop design refuses to overwrite existing artifact: {existing}");
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
                case "--run-key": runKey = Next(); break;
                case "--repo-root": repoRoot = Next(); break;
                case "--output-root": outputRoot = Next(); break;
                case "--no-external": noExternal = ParseBool(Next()); break;
                case "--no-execution": noExecution = ParseBool(Next()); break;
            }
        }

        return new(runKey, repoRoot, outputRoot, noExternal, noExecution, showHelp);
    }

    private static bool ParseBool(string? value)
        => bool.TryParse(value, out var parsed) && parsed;
}
