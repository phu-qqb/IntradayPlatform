using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bPositionMarketRuntimeWiringTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(),
        $"arch7b-position-market-wiring-{Guid.NewGuid():N}");
    private static readonly DateTimeOffset Start =
        new(2026, 8, 3, 10, 0, 0, TimeSpan.Zero);
    private static readonly PmsShadowIntradaySlotWindow Slot =
        PmsShadowIntradayCadenceContract.WindowEnding(Start.AddMinutes(15));

    [Fact]
    public void Selected_runtime_snapshot_publishes_and_prearms_exact_draft()
    {
        var path = Path.Combine(root, "position-market-slot-binding-draft.json");
        var published = Arch7bPositionMarketLiveWiring.BuildAndPublishDraft(
            path, "qualification-run", "1754288005", "ARCH7B_RDS_TEST",
            Commit('a'), Commit('b'), Source(), Slot, "market-session", Symbols());

        var prearmed = Arch7bPositionMarketLiveWiring.RequirePrearmedDraft(
            path, published.File.Sha256, Slot, "market-session",
            Commit('a'), Commit('b'), Symbols());

        Assert.Equal(published.Draft, prearmed);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Draft_create_new_rejects_reuse()
    {
        var path = Path.Combine(root, "position-market-slot-binding-draft.json");
        Arch7bPositionMarketLineageFileStore.WriteDraftCreateNew(path, Draft());
        var error = Assert.Throws<InvalidDataException>(() =>
            Arch7bPositionMarketLineageFileStore.WriteDraftCreateNew(path, Draft()));
        Assert.Equal(Arch7bPositionMarketRuntimeContract.DraftAlreadyExists, error.Message);
    }

    [Fact]
    public void Finalized_lineage_round_trips_by_exact_file_sha()
    {
        var path = Path.Combine(root, "position-market-slot-lineage.json");
        var lineage = Lineage();
        var file = Arch7bPositionMarketLineageFileStore.WriteLineageCreateNew(path, lineage);
        Assert.Equal(lineage,
            Arch7bPositionMarketLineageFileStore.ReadLineage(path, file.Sha256));
    }

    [Fact]
    public void Revision_binding_is_idempotent_for_identical_bytes()
    {
        var path = Path.Combine(root, "position-market-revision-input-binding.json");
        var binding = Arch7bPositionMarketSlotLineageContract.BindRevision(
            Lineage(), Projection());
        var first = Arch7bPositionMarketLineageFileStore.WriteRevisionBindingIdempotent(
            path, binding);
        var second = Arch7bPositionMarketLineageFileStore.WriteRevisionBindingIdempotent(
            path, binding);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Exact_arch7a_revision_binding_is_accepted()
    {
        var binding = Arch7bPositionMarketSlotLineageContract.BindRevision(
            Lineage(), Projection());
        Arch7bPositionMarketSlotLineageContract.RequireArch7aRevision(
            binding, binding.ProjectionRevisionId);
    }

    [Fact]
    public void Exact_99_288_288_projection_is_bound()
    {
        var path = Path.Combine(root, "position-market-revision-input-binding.json");
        var result = Arch7bPositionMarketLiveWiring.BindAndPublishRevision(
            Lineage(), Projection(), path);
        Assert.Equal(Arch7bPositionMarketSlotLineageContract
            .EconomicRevisionInputBindingVersion, result.Binding.ContractVersion);
        Assert.Equal(result.Binding,
            Arch7bPositionMarketLineageFileStore.ReadRevisionBinding(
                path, result.File.Sha256));
    }

    [Fact]
    public void Relative_content_addressed_path_is_rejected()
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            Arch7bPositionMarketLineageFileStore.WriteDraftCreateNew(
                "position-market-slot-binding-draft.json", Draft()));
        Assert.Equal(Arch7bPositionMarketSlotLineageContract.BindingRequired,
            error.Message);
    }

    [Fact]
    public void Same_inputs_produce_byte_identical_files_across_distinct_roots()
    {
        var first = WritePositiveChain(Path.Combine(root, "first"));
        var second = WritePositiveChain(Path.Combine(root, "second"));
        Assert.Equal(first.Select(File.ReadAllBytes), second.Select(File.ReadAllBytes),
            ByteArrayComparer.Instance);
    }

    [Fact]
    public void Offline_live_wiring_qualification_passes()
    {
        var files = WritePositiveChain(Path.Combine(root, "qualification"));
        Assert.Equal(3, files.Length);
        Assert.All(files, path => Assert.True(File.Exists(path)));
        Assert.Empty(Directory.EnumerateFiles(root, "*.tmp",
            SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData(1, Arch7bPositionMarketRuntimeContract.DraftRequiredBeforeMarketCapture)]
    [InlineData(2, Arch7bPositionMarketRuntimeContract.DraftEvidenceShaMismatch)]
    [InlineData(3, Arch7bPositionMarketSlotLineageContract.PositionSnapshotMismatch)]
    [InlineData(4, Arch7bPositionMarketSlotLineageContract.PositionSnapshotMismatch)]
    [InlineData(5, Arch7bPositionMarketSlotLineageContract.SourceIngestionMismatch)]
    [InlineData(6, Arch7bPositionMarketSlotLineageContract.RequiredUniverseMismatch)]
    [InlineData(7, Arch7bPositionMarketRuntimeContract.DraftNotBoundToSelectedSnapshot)]
    [InlineData(8, Arch7bPositionMarketRuntimeContract.DraftNotBoundToSelectedSnapshot)]
    [InlineData(9, Arch7bPositionMarketRuntimeContract.DraftNotBoundToSelectedSnapshot)]
    [InlineData(10, Arch7bPositionMarketSlotLineageContract.MappingAuthorityMismatch)]
    [InlineData(11, Arch7bPositionMarketSlotLineageContract.ManifestBindingMismatch)]
    [InlineData(12, Arch7bPositionMarketSlotLineageContract.ManifestBindingMismatch)]
    [InlineData(13, Arch7bPositionMarketRuntimeContract.LineageNotInMarketManifest)]
    [InlineData(14, Arch7bPositionMarketRuntimeContract.LineageNotInReadyMarker)]
    [InlineData(15, Arch7bPositionMarketSlotLineageContract.ManifestBindingMismatch)]
    [InlineData(16, Arch7bPositionMarketRuntimeContract.ProjectionCardinalityMismatch)]
    [InlineData(17, Arch7bPositionMarketRuntimeContract.ProjectionCardinalityMismatch)]
    [InlineData(18, Arch7bPositionMarketRuntimeContract.ProjectionCardinalityMismatch)]
    [InlineData(19, Arch7bPositionMarketSlotLineageContract.RevisionInputMismatch)]
    [InlineData(20, Arch7bPositionMarketRuntimeContract.Arch7aBindingRequired)]
    [InlineData(21, Arch7bPositionMarketRuntimeContract.DraftNotBoundToSelectedSnapshot)]
    [InlineData(22, Arch7bPositionMarketRuntimeContract.DraftNotBoundToSelectedSnapshot)]
    [InlineData(23, Arch7bPositionMarketRuntimeContract.ReplayLineageMismatch)]
    public void Negative_integration_matrix_fails_on_exact_blocker(
        int caseId, string expected)
    {
        var error = Assert.Throws<InvalidDataException>(() => Negative(caseId));
        Assert.Equal(expected, error.Message);
    }

    private void Negative(int caseId)
    {
        var caseRoot = Path.Combine(root, $"case-{caseId:00}");
        var draftPath = Path.Combine(caseRoot,
            "position-market-slot-binding-draft.json");
        switch (caseId)
        {
            case 1:
                Arch7bPositionMarketLineageFileStore.ReadDraft(draftPath, Sha('1'));
                return;
            case 2:
                var draftFile = Arch7bPositionMarketLineageFileStore
                    .WriteDraftCreateNew(draftPath, Draft());
                Arch7bPositionMarketLineageFileStore.ReadDraft(
                    draftPath, DifferentSha(draftFile.Sha256));
                return;
            case 3:
                RequireDifferent(Rehash(Lineage() with
                { SelectedPositionSnapshotId = Guid.NewGuid() }));
                return;
            case 4:
                Arch7bPositionMarketSlotLineageContract.BuildDraft(
                    "qualification-run", "1754288005", "ARCH7B_RDS_TEST",
                    Commit('a'), Commit('b'), Source(Start.AddMinutes(-6)), Slot,
                    "market-session", Symbols());
                return;
            case 5:
                RequireDifferent(Rehash(Lineage() with
                { SourceIngestionId = Guid.NewGuid() }));
                return;
            case 6:
                RequireDifferent(Rehash(Lineage() with
                { RequiredPmsUniverseSha256 = Sha('2') }));
                return;
            case 7:
                Prearm(caseRoot, PmsShadowIntradayCadenceContract.WindowEnding(
                    Slot.SlotEndUtc.AddMinutes(15)), "market-session",
                    Commit('a'), Commit('b'), Symbols());
                return;
            case 8:
                Prearm(caseRoot, Slot, "wrong-session",
                    Commit('a'), Commit('b'), Symbols());
                return;
            case 9:
                Prearm(caseRoot, Slot, "market-session",
                    Commit('a'), Commit('b'), Symbols()[..48]);
                return;
            case 10:
                RequireDifferent(Rehash(Lineage() with
                { MarketMappingSetSha256 = Sha('3') }));
                return;
            case 11:
                Arch7bPositionMarketSlotLineageContract.Finalize(
                    Draft(), Sha('1'), Sha('2'), Sha('3'), Sha('4'), 48,
                    Start.AddSeconds(1), Slot.SlotEndUtc.AddSeconds(-1));
                return;
            case 12:
                Arch7bPositionMarketSlotLineageContract.Finalize(
                    Draft(), Sha('1'), Sha('2'), Sha('3'), Sha('4'), 49,
                    Start.AddSeconds(1), Slot.SlotEndUtc.AddTicks(1));
                return;
            case 13:
                ImportWithManifest(caseRoot, markerHasLineage: true,
                    wrongManifestSha: false);
                return;
            case 14:
                ImportWithManifest(caseRoot, markerHasLineage: false,
                    wrongManifestSha: false);
                return;
            case 15:
                ImportWithManifest(caseRoot, markerHasLineage: true,
                    wrongManifestSha: true);
                return;
            case 16:
                Arch7bPositionMarketLiveWiring.BindAndPublishRevision(
                    Lineage(), Projection(observations: 98),
                    Path.Combine(caseRoot,
                        "position-market-revision-input-binding.json"));
                return;
            case 17:
                Arch7bPositionMarketLiveWiring.BindAndPublishRevision(
                    Lineage(), Projection(targets: 287),
                    Path.Combine(caseRoot,
                        "position-market-revision-input-binding.json"));
                return;
            case 18:
                Arch7bPositionMarketLiveWiring.BindAndPublishRevision(
                    Lineage(), Projection(drifts: 287),
                    Path.Combine(caseRoot,
                        "position-market-revision-input-binding.json"));
                return;
            case 19:
                var binding = Arch7bPositionMarketSlotLineageContract.BindRevision(
                    Lineage(), Projection());
                Arch7bPositionMarketSlotLineageContract.RequireArch7aRevision(
                    binding, Guid.NewGuid());
                return;
            case 20:
                var bindingPath = Path.Combine(caseRoot,
                    "position-market-revision-input-binding.json");
                var bound = Arch7bPositionMarketLineageFileStore
                    .WriteRevisionBindingIdempotent(bindingPath,
                        Arch7bPositionMarketSlotLineageContract.BindRevision(
                            Lineage(), Projection()));
                Arch7bPositionMarketLiveWiring.RequireArch7aRevision(
                    bindingPath, bound.Sha256, Guid.NewGuid());
                return;
            case 21:
                Prearm(caseRoot, Slot, "market-session",
                    Commit('9'), Commit('b'), Symbols());
                return;
            case 22:
                Prearm(caseRoot, Slot, "market-session",
                    Commit('a'), Commit('9'), Symbols());
                return;
            case 23:
                ReplayDifferentLineage(caseRoot);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(caseId));
        }
    }

    private static string[] WritePositiveChain(string chainRoot)
    {
        var draftPath = Path.Combine(chainRoot,
            "position-market-slot-binding-draft.json");
        var lineagePath = Path.Combine(chainRoot,
            "position-market-slot-lineage.json");
        var bindingPath = Path.Combine(chainRoot,
            "position-market-revision-input-binding.json");
        var draft = Arch7bPositionMarketLineageFileStore.WriteDraftCreateNew(
            draftPath, Draft());
        Arch7bPositionMarketLineageFileStore.ReadDraft(draftPath, draft.Sha256);
        var lineage = Arch7bPositionMarketLineageFileStore.WriteLineageCreateNew(
            lineagePath, Lineage());
        Arch7bPositionMarketLineageFileStore.ReadLineage(
            lineagePath, lineage.Sha256);
        var binding = Arch7bPositionMarketLiveWiring.BindAndPublishRevision(
            Lineage(), Projection(), bindingPath);
        Arch7bPositionMarketLiveWiring.RequireArch7aRevision(
            bindingPath, binding.File.Sha256,
            binding.Binding.ProjectionRevisionId);
        return [draftPath, lineagePath, bindingPath];
    }

    private static void Prearm(string caseRoot,
        PmsShadowIntradaySlotWindow slot, string session,
        string coreCommit, string intradayCommit, string[] symbols)
    {
        var path = Path.Combine(caseRoot,
            "position-market-slot-binding-draft.json");
        var file = Arch7bPositionMarketLineageFileStore.WriteDraftCreateNew(
            path, Draft());
        Arch7bPositionMarketLiveWiring.RequirePrearmedDraft(
            path, file.Sha256, slot, session, coreCommit, intradayCommit, symbols);
    }

    private static void ImportWithManifest(string caseRoot,
        bool markerHasLineage, bool wrongManifestSha)
    {
        var draftPath = Path.Combine(caseRoot,
            "position-market-slot-binding-draft.json");
        var lineagePath = Path.Combine(caseRoot,
            "position-market-slot-lineage.json");
        var bindingPath = Path.Combine(caseRoot,
            "position-market-revision-input-binding.json");
        var manifestPath = Path.Combine(caseRoot, "slot_manifest.json");
        var draftFile = Arch7bPositionMarketLineageFileStore.WriteDraftCreateNew(
            draftPath, Draft());
        var lineageFile = Arch7bPositionMarketLineageFileStore.WriteLineageCreateNew(
            lineagePath, Lineage());
        Directory.CreateDirectory(caseRoot);
        File.WriteAllText(manifestPath, "{}");
        var actualManifestSha =
            Arch7bPositionMarketLineageFileStore.Sha256File(manifestPath);
        var marker = new PmsShadowFreshSlotReadyMarker(
            PmsShadowFreshSlotHandoffContract.Version, Slot.SlotId,
            Slot.SlotEndUtc, Source().SourceSessionId, "fixture.jsonl", Sha('8'),
            wrongManifestSha ? Sha('9') : actualManifestSha, Sha('1'), Sha('2'),
            Slot.SlotEndUtc.AddSeconds(1), 1, Commit('b'), "ARCH7B_RDS_TEST",
            Sha('7'), "TEST", true,
            markerHasLineage ? lineagePath : null,
            markerHasLineage ? lineageFile.Sha256 : null);
        var authority = new Arch7bPositionMarketImportAuthority(
            draftPath, draftFile.Sha256, lineagePath, lineageFile.Sha256,
            bindingPath, marker);
        Arch7bPositionMarketLiveWiring.RequireImportBinding(
            manifestPath, authority, Source(), Capture());
    }

    private static void ReplayDifferentLineage(string caseRoot)
    {
        var path = Path.Combine(caseRoot,
            "position-market-revision-input-binding.json");
        var first = Arch7bPositionMarketSlotLineageContract.BindRevision(
            Lineage(), Projection());
        Arch7bPositionMarketLineageFileStore.WriteRevisionBindingIdempotent(
            path, first);
        var differentLineage = Rehash(Lineage() with
        { MarketManifestSha256 = Sha('9') });
        var second = Arch7bPositionMarketSlotLineageContract.BindRevision(
            differentLineage, Projection());
        Arch7bPositionMarketLineageFileStore.WriteRevisionBindingIdempotent(
            path, second);
    }

    private static void RequireDifferent(Arch7bPositionMarketSlotLineage actual) =>
        Arch7bPositionMarketSlotLineageContract.RequireExactBinding(
            Lineage(), actual);

    private static Arch7bPositionMarketSlotBindingDraft Draft() =>
        Arch7bPositionMarketSlotLineageContract.BuildDraft(
            "qualification-run", "1754288005", "ARCH7B_RDS_TEST",
            Commit('a'), Commit('b'), Source(), Slot, "market-session", Symbols());

    private static Arch7bPositionMarketSlotLineage Lineage() =>
        Arch7bPositionMarketSlotLineageContract.Finalize(
            Draft(), Sha('1'), Sha('2'), Sha('3'), Sha('4'), 49,
            Start.AddSeconds(1), Slot.SlotEndUtc.AddSeconds(-1));

    private static PmsShadowEconomicSource Source(DateTimeOffset? asOf = null)
    {
        var mappings = Enumerable.Range(1, 99).Select(value =>
            new PmsShadowEconomicMapping(GuidFrom(value), GuidFrom(value + 100),
                GuidFrom(value + 200), value.ToString("D4"), $"S{value:D5}",
                (10_000 + value).ToString(), 1m, 0.01m, 0.00001m)).ToArray();
        return new(GuidFrom(500), "arch6b-source", GuidFrom(501), 1_000_000m,
            GuidFrom(502), asOf ?? Start.AddMinutes(-1),
            "LMAX_PORTAL_GLOBAL_FLAT_EXPLICIT",
            mappings.ToDictionary(value => value.InstrumentId, _ => 0m),
            mappings, []);
    }

    private static PmsShadowRealSlotCapture Capture() =>
        new(Slot.SlotId, Slot.SlotStartUtc, Slot.SlotEndUtc, "market-session",
            "fixture.jsonl", Sha('8'), Symbols().Select((symbol, index) =>
                new PmsShadowRealSlotBbo(symbol, (10_000 + index).ToString(),
                    1m, 1.1m, Slot.SlotEndUtc.AddSeconds(-1),
                    Slot.SlotEndUtc.AddMilliseconds(-500))).ToArray(),
            true, 0, true, true);

    private static PmsShadowIntradayEconomicProjection Projection(
        int observations = 99, int targets = 288, int drifts = 288) =>
        new(GuidFrom(700), 1, Slot.SlotId, Slot.SlotStartUtc, Slot.SlotEndUtc,
            Sha('5'), GuidFrom(701), Sha('6'), Source().IngestionId,
            Source().SourceSessionId, GuidFrom(501), Source().PositionSnapshotId,
            Source().PositionAsOfUtc, Source().PositionAuthority,
            [], [], [],
            new PmsShadowSlotMarketObservation[observations],
            new PmsShadowSlotTargetPosition[targets],
            new PmsShadowSlotPositionOnlyDrift[drifts],
            Sha('7'), Sha('8'), Sha('9'), Sha('a'), null, "COMPLETED",
            PmsShadowStateContract.CompletedNoExternal, true, true,
            Slot.SlotEndUtc.AddMinutes(1));

    private static Arch7bPositionMarketSlotLineage Rehash(
        Arch7bPositionMarketSlotLineage value)
    {
        var draft = new Arch7bPositionMarketSlotBindingDraft(
            value.ContractVersion, value.RunId, value.Account, value.TargetProfile,
            value.CoreCommit, value.IntradayCommit, value.SelectedPositionSnapshotId,
            value.PositionSnapshotAsOfUtc, value.PositionSnapshotLineSetSha256,
            value.PositionAuthority, value.SourceIngestionId, value.SourceSessionId,
            value.RequiredPmsUniverseSha256, value.SlotId, value.SlotStartUtc,
            value.SlotEndUtc, value.MarketCaptureSessionId,
            value.RequiredMarketSymbolSetSha256, value.MarketMappingContractVersion,
            value.MarketMappingSetSha256,
            value.PositionSnapshotAgeAtSlotStartMilliseconds, value.NoOrder, string.Empty);
        draft = draft with
        {
            EvidenceSha256 = Arch5bHashing.HashCanonical(
                draft with { EvidenceSha256 = string.Empty })
        };
        return Arch7bPositionMarketSlotLineageContract.Finalize(
            draft, value.ClockCaptureStartEvidenceSha256,
            value.ClockPostCloseEvidenceSha256, value.MarketSelectionSha256,
            value.MarketManifestSha256, value.MarketCoverageCount,
            value.SourceTimestampStartUtc, value.SourceTimestampEndUtc);
    }

    private static string[] Symbols() => Enumerable.Range(1, 49)
        .Select(value => $"M{value:D5}").ToArray();
    private static Guid GuidFrom(int value) =>
        Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
    private static string Sha(char value) => new(value, 64);
    private static string Commit(char value) => new(value, 40);
    private static string DifferentSha(string value) =>
        (value[0] == '0' ? "1" : "0") + value[1..];

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    private sealed class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public static ByteArrayComparer Instance { get; } = new();
        public bool Equals(byte[]? x, byte[]? y) =>
            x is not null && y is not null && x.SequenceEqual(y);
        public int GetHashCode(byte[] obj) => obj.Length;
    }
}
