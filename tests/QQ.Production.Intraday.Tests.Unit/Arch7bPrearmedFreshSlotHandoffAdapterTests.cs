using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bPrearmedFreshSlotHandoffAdapterTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(),
        "arch7b-prearmed-handoff-adapter", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("CLOCK_CAPTURE_START", "assert-prearmed",
        "ARCH7B_POSITION_MARKET_SLOT_BINDING_DRAFT_READY",
        "clock-authority-capture", "clock_authority_capture.json",
        "ARCH7B_CAPTURE_START_PREARMED")]
    [InlineData("MARKET_FINALIZATION", "publish-ready",
        "READY_MARKER_PUBLISHED", "position-market-lineage",
        "position_market_lineage.json",
        "ARCH7B_MARKET_FINALIZATION_QUALIFIED")]
    [InlineData("PMS_IMPORT", "prearm-and-import", "COMPLETED",
        "position-market-revision-binding",
        "position_market_revision_binding.json",
        "ARCH7B_PMS_ECONOMIC_REPLAY_QUALIFIED")]
    public async Task Native_prearmed_handoff_envelope_is_strictly_adapted(
        string stageId, string mode, string status, string artifactType,
        string fileName, string expectedResult)
    {
        Directory.CreateDirectory(root);
        var artifactPath = Path.Combine(root, fileName);
        await File.WriteAllTextAsync(artifactPath, stageId);
        var native = new JsonObject
        {
            ["contract"] = Arch7bPrearmedFreshSlotHandoffAdapter.NativeContract,
            ["status"] = status,
            ["no_order"] = true,
            ["artifacts"] = new JsonArray
            {
                new JsonObject
                {
                    ["path"] = artifactPath,
                    ["sha256"] = ShaFile(artifactPath),
                    ["artifact_type"] = artifactType
                }
            }
        };
        native["evidence_sha256"] = Sha(native.ToJsonString());

        var normalized = await new Arch7bPrearmedFreshSlotHandoffAdapter()
            .AdaptAsync(native.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true
            }), Command(stageId, mode, artifactPath), root);

        Assert.Equal(expectedResult, normalized.ResultCode);
        Assert.Equal(1, normalized.NativeArtifactCount);
        Assert.Equal(artifactPath, Assert.Single(normalized.ArtifactPaths));
    }

    private Arch7bOneShotMaterializedCommand Command(string stageId,
        string mode, string artifactPath)
    {
        var executable = typeof(Arch7bPrearmedFreshSlotHandoffAdapter)
            .Assembly.Location;
        var arguments = new List<string> { "--mode", mode };
        if (mode == "publish-ready")
            arguments.AddRange(["--position-market-lineage-path", artifactPath]);
        if (mode == "prearm-and-import")
            arguments.AddRange([
                "--position-market-revision-binding-path", artifactPath]);
        return new(Arch7bV2Contracts.MaterializedCommandVersion,
            $"native-{mode}", stageId, Arch7bExecutionKind.ChildInvoke,
            executable, ShaFile(executable), arguments, root,
            "prearmed-handoff-v1",
            Arch7bV2Contracts.ChildResultAdapterVersion,
            Arch7bPrearmedFreshSlotHandoffAdapter.NativeContract,
            30, 1_048_576, 1_048_576, "qualification-child-process",
            false, false, false, [], [], null,
            Path.Combine(root, "command-authority.json"),
            new string('a', 64), new string('b', 64));
    }

    private static string Sha(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string ShaFile(string path) => Convert.ToHexStringLower(
        SHA256.HashData(File.ReadAllBytes(path)));

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
