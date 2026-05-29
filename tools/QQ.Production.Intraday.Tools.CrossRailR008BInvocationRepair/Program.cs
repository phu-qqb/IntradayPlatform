using QQ.Production.Intraday.Application;

var cli = CliOptions.Parse(args);
if (cli.ShowHelp)
{
    Console.WriteLine("Usage: dotnet run --project tools/QQ.Production.Intraday.Tools.CrossRailR008BInvocationRepair -- --run-key cross-rail-r008b-invocation-repair-only-001 --output-root artifacts/readiness/cross-rail-sandbox-handoff/cross-rail-r008b-invocation-repair-only-001 --r008-package-root artifacts/readiness/cross-rail-sandbox-handoff/cross-rail-r008-controlled-sandbox-failure-diagnosis-001 --no-external true --no-execution true");
    return 0;
}

if (!cli.NoExternal || !cli.NoExecution)
{
    Console.Error.WriteLine("CROSS-RAIL-R008B is invocation-repair-only and requires --no-external true --no-execution true.");
    return 2;
}

var runKey = cli.RunKey ?? "cross-rail-r008b-invocation-repair-only-001";
var outputRoot = Path.GetFullPath(cli.OutputRoot ?? Path.Combine("artifacts", "readiness", "cross-rail-sandbox-handoff", runKey));
var r008Root = Path.GetFullPath(cli.R008PackageRoot ?? Path.Combine("artifacts", "readiness", "cross-rail-sandbox-handoff", "cross-rail-r008-controlled-sandbox-failure-diagnosis-001"));

RefuseOverwrite(outputRoot);

var result = await new CrossRailR008BInvocationRepairWriter().WriteAsync(
    new CrossRailR008BOptions(runKey, outputRoot, r008Root),
    CancellationToken.None);

Console.WriteLine($"CROSS_RAIL_R008B_STATUS={result.RepairReport["CROSS_RAIL_R008B_STATUS"]}");
Console.WriteLine($"R008B_RESULT={result.RepairReport["R008B_RESULT"]}");
Console.WriteLine($"R009_RUNNABLE_INVOCATION_COMPLETE={result.RepairReport["R009_RUNNABLE_INVOCATION_COMPLETE"]}");
Console.WriteLine($"CANDIDATE_SET_HASH_BOUND={result.CandidateBinding["CANDIDATE_SET_HASH_BOUND"]}");
Console.WriteLine($"NO_EXECUTION_BOUNDARY_STATUS={result.Boundary["NO_EXECUTION_BOUNDARY_STATUS"]}");
Console.WriteLine($"report={Path.Combine(outputRoot, "10_validation", "cross_rail_r008b_invocation_repair_report.json")}");

return string.Equals(result.RepairReport["CROSS_RAIL_R008B_STATUS"].ToString(), "PASS", StringComparison.Ordinal) ? 0 : 2;

static void RefuseOverwrite(string outputRoot)
{
    var protectedFiles = new[]
    {
        Path.Combine(outputRoot, "10_validation", "cross_rail_r008b_invocation_repair_report.json"),
        Path.Combine(outputRoot, "manifest.json"),
        Path.Combine(outputRoot, "hashes.json"),
        Path.Combine(outputRoot, "manifest.sha256")
    };

    var existing = protectedFiles.FirstOrDefault(File.Exists);
    if (existing is not null)
    {
        throw new IOException($"CROSS-RAIL-R008B writer refuses to overwrite existing artifact: {existing}");
    }
}

internal sealed record CliOptions(
    string? RunKey,
    string? OutputRoot,
    string? R008PackageRoot,
    bool NoExternal,
    bool NoExecution,
    bool ShowHelp)
{
    public static CliOptions Parse(string[] args)
    {
        string? runKey = null;
        string? outputRoot = null;
        string? r008PackageRoot = null;
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
                case "--r008-package-root": r008PackageRoot = Next(); break;
                case "--no-external": noExternal = ParseBool(Next()); break;
                case "--no-execution": noExecution = ParseBool(Next()); break;
            }
        }

        return new(runKey, outputRoot, r008PackageRoot, noExternal, noExecution, showHelp);
    }

    private static bool ParseBool(string? value)
        => bool.TryParse(value, out var parsed) && parsed;
}
