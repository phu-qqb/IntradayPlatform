using QQ.Production.Intraday.Application;

var cli = CliOptions.Parse(args);
if (cli.ShowHelp)
{
    Console.WriteLine("Usage: dotnet run --project tools/QQ.Production.Intraday.Tools.CrossRailR008ControlledSandboxFailureDiagnosis -- --run-key cross-rail-r008-controlled-sandbox-failure-diagnosis-001 --output-root artifacts/readiness/cross-rail-sandbox-handoff/cross-rail-r008-controlled-sandbox-failure-diagnosis-001 --source-artifact-root artifacts/readiness/cross-rail-sandbox-handoff --no-external true --no-execution true");
    return 0;
}

if (!cli.NoExternal || !cli.NoExecution)
{
    Console.Error.WriteLine("CROSS-RAIL-R008 is diagnosis-only and requires --no-external true --no-execution true.");
    return 2;
}

var runKey = cli.RunKey ?? "cross-rail-r008-controlled-sandbox-failure-diagnosis-001";
var outputRoot = Path.GetFullPath(cli.OutputRoot ?? Path.Combine("artifacts", "readiness", "cross-rail-sandbox-handoff", runKey));
var sourceRoot = Path.GetFullPath(cli.SourceArtifactRoot ?? Path.Combine("artifacts", "readiness", "cross-rail-sandbox-handoff"));

RefuseOverwrite(outputRoot);

var result = await new CrossRailR008ControlledSandboxFailureDiagnosisWriter().WriteAsync(
    new CrossRailR008Options(runKey, outputRoot, sourceRoot),
    CancellationToken.None);

Console.WriteLine($"CROSS_RAIL_R008_STATUS={result.FailureDiagnosis["CROSS_RAIL_R008_STATUS"]}");
Console.WriteLine($"R008_RESULT={result.FailureDiagnosis["R008_RESULT"]}");
Console.WriteLine($"PHASE_B_SANDBOX_EXECUTION_ATTEMPTED={result.FailureDiagnosis["PHASE_B_SANDBOX_EXECUTION_ATTEMPTED"]}");
Console.WriteLine($"CANDIDATE_SET_PRESERVED={result.CandidateSetIntegrity["CANDIDATE_SET_PRESERVED"]}");
Console.WriteLine($"NO_EXECUTION_BOUNDARY_STATUS={result.NoExecutionBoundary["NO_EXECUTION_BOUNDARY_STATUS"]}");
Console.WriteLine($"report={Path.Combine(outputRoot, "10_validation", "cross_rail_r008_failure_diagnosis_report.json")}");

return string.Equals(result.FailureDiagnosis["CROSS_RAIL_R008_STATUS"].ToString(), "PASS", StringComparison.Ordinal) ? 0 : 2;

static void RefuseOverwrite(string outputRoot)
{
    var protectedFiles = new[]
    {
        Path.Combine(outputRoot, "10_validation", "cross_rail_r008_failure_diagnosis_report.json"),
        Path.Combine(outputRoot, "manifest.json"),
        Path.Combine(outputRoot, "hashes.json"),
        Path.Combine(outputRoot, "manifest.sha256")
    };

    var existing = protectedFiles.FirstOrDefault(File.Exists);
    if (existing is not null)
    {
        throw new IOException($"CROSS-RAIL-R008 writer refuses to overwrite existing artifact: {existing}");
    }
}

internal sealed record CliOptions(
    string? RunKey,
    string? OutputRoot,
    string? SourceArtifactRoot,
    bool NoExternal,
    bool NoExecution,
    bool ShowHelp)
{
    public static CliOptions Parse(string[] args)
    {
        string? runKey = null;
        string? outputRoot = null;
        string? sourceArtifactRoot = null;
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
                case "--source-artifact-root": sourceArtifactRoot = Next(); break;
                case "--no-external": noExternal = ParseBool(Next()); break;
                case "--no-execution": noExecution = ParseBool(Next()); break;
            }
        }

        return new(runKey, outputRoot, sourceArtifactRoot, noExternal, noExecution, showHelp);
    }

    private static bool ParseBool(string? value)
        => bool.TryParse(value, out var parsed) && parsed;
}
