using System.Security.Cryptography;
using System.Text.Json;
using QQ.Production.Intraday.Application.CanonicalRecorder;

var root = GetArg("--root") ?? Path.Combine("artifacts", "readiness", "anubis-m2b-canonical-shadow-wiring-offline", "sample-run");
var runId = GetArg("--run-id") ?? "M2B-CANONICAL-SHADOW-OFFLINE-SAMPLE";
var toolCommit = GetArg("--tool-commit") ?? "WORKTREE";
var baseline = GetArg("--baseline") ?? "0c2366b1b167401cbd7f1b3441004f1ab03a2955";

Directory.CreateDirectory(root);
var result = await new CanonicalShadowOfflineHost().RunAsync(root, runId, toolCommit, baseline);
var summary = new
{
    result.Status,
    result.RunRoot,
    result.FinalManifest.RecorderRunId,
    result.FinalManifest.EventsWritten,
    result.ReplayReport.ReplayHashVersion,
    result.ReplayReport.DeterministicReplayHash,
    result.DataQualityReport.ShadowReady,
    result.ParityReport.RowCount,
    result.ParityReport.MismatchCount,
    Safety = new
    {
        NoLiveRun = true,
        NoFixLogon = true,
        NoOrderGenerated = true,
        NoBrokerTraffic = true,
        NoAccountApi = true,
        NoDatabento = true,
        NoDbApply = true
    }
};
var summaryPath = Path.Combine(result.RunRoot, "shadow_offline_summary.json");
await File.WriteAllTextAsync(summaryPath, JsonSerializer.Serialize(summary, CanonicalRecorderV2Constants.JsonOptions));
Console.WriteLine(JsonSerializer.Serialize(summary, new JsonSerializerOptions(JsonSerializerDefaults.General) { WriteIndented = true }));

string? GetArg(string name)
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
