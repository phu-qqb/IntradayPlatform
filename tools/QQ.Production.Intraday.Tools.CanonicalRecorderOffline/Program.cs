using System.Text.Json;
using QQ.Production.Intraday.Application.CanonicalRecorder;

static string? Arg(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }

    return null;
}

var outputRoot = Arg(args, "--output-root") ?? Path.Combine("artifacts", "readiness", "anubis-m2a-canonical-recorder-foundation-no-live", "sample_run");
var runId = Arg(args, "--run-id") ?? "M2A-SYNTHETIC-VERTICAL-SLICE-001";
var toolCommit = Arg(args, "--tool-commit") ?? "UNVERIFIED_USER_SUPPLIED";
var sourceBaselineCommit = Arg(args, "--source-baseline-commit") ?? toolCommit;

Directory.CreateDirectory(outputRoot);
var result = await CanonicalRecorderSyntheticScenario.RunAsync(outputRoot, runId, toolCommit, sourceBaselineCommit);
var summary = new
{
    final_status = result.ReplayReport.Status == "PASS" && result.DataQualityReport.ShadowReady
        ? "GO_M2B_CANONICAL_SHADOW_WIRING"
        : "NO_GO_M2B",
    result.RunRoot,
    result.FinalManifest.EventsWritten,
    result.FinalManifest.EventsDropped,
    result.FinalManifest.WriterErrors,
    result.DataQualityReport.ShadowReady,
    result.ReplayReport.DeterministicReplayHash
};

Console.WriteLine(JsonSerializer.Serialize(summary, CanonicalRecorderConstants.JsonOptions));
