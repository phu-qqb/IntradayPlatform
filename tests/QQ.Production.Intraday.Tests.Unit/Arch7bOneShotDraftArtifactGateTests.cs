using System.Security.Cryptography;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Infrastructure.PostgreSql;
using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bOneShotDraftArtifactGateTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(),
        "arch7b-draft-gate", Guid.NewGuid().ToString("N"));
    private static readonly DateTimeOffset Now =
        new(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
    private const string RunId = "arch7b-draft-gate-run";
    private const string MarketSession = "00000000-0000-0000-0000-000000000900";

    [Fact]
    public void Gate_rejects_missing_draft_file()
    {
        var facts = Facts(GuidFrom(502));

        Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bOneShotLiveExecutionRuntimeV2.ExecutePositionMarketDraftStage(
                facts, root, Now, []));
    }

    [Fact]
    public void Gate_rejects_tampered_draft_contract()
    {
        var facts = Facts(GuidFrom(502));
        var path = WriteDraft();
        File.WriteAllText(path, File.ReadAllText(path).Replace(
            "1754288005", "1754288006", StringComparison.Ordinal));

        Assert.ThrowsAny<Exception>(() =>
            Arch7bOneShotLiveExecutionRuntimeV2.ExecutePositionMarketDraftStage(
                facts, root, Now, []));
    }

    [Fact]
    public void Gate_rejects_draft_bound_to_wrong_runtime_snapshot()
    {
        var facts = Facts(GuidFrom(999));
        WriteDraft();

        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bOneShotLiveExecutionRuntimeV2.ExecutePositionMarketDraftStage(
                facts, root, Now, []));
        Assert.Equal(Arch7bV2Blockers.AuthorityBindingMismatch, error.BlockerCode);
    }

    [Fact]
    public void Gate_publishes_real_path_sha_and_snapshot_artifact()
    {
        var facts = Facts(GuidFrom(502));
        var path = WriteDraft();
        var produced = new List<string>();

        Arch7bOneShotLiveExecutionRuntimeV2.ExecutePositionMarketDraftStage(
            facts, root, Now, produced);

        var artifact = facts.Facts.Single(value =>
            value.FactType == "position_market_draft_artifact");
        using var document = System.Text.Json.JsonDocument.Parse(artifact.ValueJson);
        Assert.Equal(path, document.RootElement.GetProperty("path").GetString());
        Assert.Equal(ShaFile(path),
            document.RootElement.GetProperty("sha256").GetString());
        Assert.Equal(GuidFrom(502),
            document.RootElement.GetProperty("selected_position_snapshot_id").GetGuid());
        Assert.Equal(MarketSession,
            document.RootElement.GetProperty("market_capture_session_id").GetString());
        Assert.Single(produced);
    }

    private Arch7bOneShotLiveFactStore Facts(Guid selectedSnapshotId)
    {
        Directory.CreateDirectory(root);
        var facts = new Arch7bOneShotLiveFactStore(root);
        facts.Append("run_identity", "ONE_SHOT_IDENTITIES_CREATED",
            new { value = RunId }, Arch7bOneShotContracts.Sha256(RunId), Now);
        facts.Append("market_capture_session_identity", "ONE_SHOT_IDENTITIES_CREATED",
            new { value = MarketSession },
            Arch7bOneShotContracts.Sha256(MarketSession), Now);
        var planned = Arch7bOneShotRunArtifactPath.ReservePositionMarketDraft(root, RunId);
        facts.Append("position_market_draft_output_path",
            "ONE_SHOT_IDENTITIES_CREATED", planned, planned.EvidenceSha256, Now);
        var selectionPath = Path.Combine(root, "runtime-selection.json");
        File.WriteAllText(selectionPath, System.Text.Json.JsonSerializer.Serialize(new
        {
            contract = "arch7b_position_snapshot_runtime_selection_v1",
            selected_position_snapshot_id = selectedSnapshotId.ToString("D")
        }));
        var selectionSha = ShaFile(selectionPath);
        facts.Append("runtime_selection_artifact", "RUNTIME_SELECTION",
            new
            {
                result = "ARCH7B_RUNTIME_POSITION_SNAPSHOT_SELECTED",
                artifact_paths = new[] { selectionPath }
            }, selectionSha, Now);
        return facts;
    }

    private string WriteDraft()
    {
        var path = Path.Combine(root,
            Arch7bOneShotRunArtifactPath.PositionMarketDraftFilename);
        Arch7bPositionMarketLineageFileStore.WriteDraftCreateNew(path,
            Arch7bPositionMarketSlotLineageContract.BuildDraft(
                RunId, "1754288005", "ARCH7B_RDS_TEST",
                Commit('a'), Commit('b'), Source(), Slot(), MarketSession, Symbols()));
        return path;
    }

    private static PmsShadowEconomicSource Source()
    {
        var mappings = Enumerable.Range(1, 99).Select(value =>
            new PmsShadowEconomicMapping(GuidFrom(value), GuidFrom(value + 100),
                GuidFrom(value + 200), value.ToString("D4"), $"S{value:D5}",
                (10_000 + value).ToString(), 1m, 0.01m, 0.00001m)).ToArray();
        return new(GuidFrom(500), "arch6b-source", GuidFrom(501), 1_000_000m,
            GuidFrom(502), Now.AddMinutes(-1),
            "LMAX_PORTAL_GLOBAL_FLAT_EXPLICIT",
            mappings.ToDictionary(value => value.InstrumentId, _ => 0m),
            mappings, []);
    }

    private static PmsShadowIntradaySlotWindow Slot() =>
        PmsShadowIntradayCadenceContract.WindowEnding(Now.AddMinutes(15));

    private static string[] Symbols() => Enumerable.Range(1, 49)
        .Select(value => $"M{value:D5}").ToArray();

    private static Guid GuidFrom(int value) =>
        Guid.Parse($"00000000-0000-0000-0000-{value:D12}");

    private static string Commit(char value) => new(value, 40);

    private static string ShaFile(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
