using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bPositionMarketSlotLineageTests
{
    [Fact]
    public void Exact_binding_is_accepted()
    {
        var value = ValidLineage();
        Arch7bPositionMarketSlotLineageContract.Validate(value);
        Arch7bPositionMarketSlotLineageContract.RequireExactBinding(value, value);
    }

    [Fact]
    public void Wrong_snapshot_id_is_rejected() => RejectExact(
        value => Rehash(value with { SelectedPositionSnapshotId = Guid.NewGuid() }),
        Arch7bPositionMarketSlotLineageContract.PositionSnapshotMismatch);

    [Fact]
    public void Wrong_position_line_set_sha_is_rejected() => RejectExact(
        value => Rehash(value with { PositionSnapshotLineSetSha256 = Sha('1') }),
        Arch7bPositionMarketSlotLineageContract.PositionSnapshotMismatch);

    [Fact]
    public void Wrong_source_ingestion_is_rejected() => RejectExact(
        value => Rehash(value with { SourceIngestionId = Guid.NewGuid() }),
        Arch7bPositionMarketSlotLineageContract.SourceIngestionMismatch);

    [Fact]
    public void Wrong_required_universe_is_rejected() => RejectExact(
        value => Rehash(value with { RequiredPmsUniverseSha256 = Sha('2') }),
        Arch7bPositionMarketSlotLineageContract.RequiredUniverseMismatch);

    [Fact]
    public void Wrong_slot_is_rejected()
    {
        var expected = ValidLineage();
        var exception = Assert.Throws<InvalidDataException>(() =>
            Rehash(expected with { SlotId = "pms-shadow-15m-20260803T1015Z" }));
        Assert.Equal(Arch7bPositionMarketSlotLineageContract.SlotMismatch,
            exception.Message);
    }

    [Fact]
    public void Wrong_market_session_is_rejected() => RejectExact(
        value => Rehash(value with { MarketCaptureSessionId = "wrong-session" }),
        Arch7bPositionMarketSlotLineageContract.MappingAuthorityMismatch);

    [Fact]
    public void Wrong_required_symbol_set_is_rejected() => RejectExact(
        value => Rehash(value with { RequiredMarketSymbolSetSha256 = Sha('3') }),
        Arch7bPositionMarketSlotLineageContract.MappingAuthorityMismatch);

    [Fact]
    public void Wrong_mapping_set_is_rejected() => RejectExact(
        value => Rehash(value with { MarketMappingSetSha256 = Sha('4') }),
        Arch7bPositionMarketSlotLineageContract.MappingAuthorityMismatch);

    [Fact]
    public void Wrong_market_manifest_is_rejected() => RejectExact(
        value => Rehash(value with { MarketManifestSha256 = Sha('5') }),
        Arch7bPositionMarketSlotLineageContract.ManifestBindingMismatch);

    [Fact]
    public void Wrong_core_commit_is_rejected() => RejectExact(
        value => Rehash(value with { CoreCommit = Commit('1') }),
        Arch7bPositionMarketSlotLineageContract.ManifestBindingMismatch);

    [Fact]
    public void Wrong_intraday_commit_is_rejected() => RejectExact(
        value => Rehash(value with { IntradayCommit = Commit('2') }),
        Arch7bPositionMarketSlotLineageContract.ManifestBindingMismatch);

    [Fact]
    public void Stale_position_snapshot_is_rejected()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            Rehash(ValidLineage() with
            {
                PositionSnapshotAsOfUtc = Start.AddMinutes(-6),
                PositionSnapshotAgeAtSlotStartMilliseconds = 360_000
            }));
        Assert.Equal(Arch7bPositionMarketSlotLineageContract.PositionSnapshotMismatch,
            exception.Message);
    }

    [Fact]
    public void Future_position_snapshot_is_rejected()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            Rehash(ValidLineage() with
            {
                PositionSnapshotAsOfUtc = Start.AddSeconds(1),
                PositionSnapshotAgeAtSlotStartMilliseconds = -1_000
            }));
        Assert.Equal(Arch7bPositionMarketSlotLineageContract.PositionSnapshotMismatch,
            exception.Message);
    }

    [Fact]
    public void Incomplete_market_coverage_is_rejected()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            Rehash(ValidLineage() with { MarketCoverageCount = 48 }));
        Assert.Equal(Arch7bPositionMarketSlotLineageContract.ManifestBindingMismatch,
            exception.Message);
    }

    [Fact]
    public void Pms_revision_mismatch_is_rejected()
    {
        var lineage = ValidLineage();
        var projection = ValidProjection() with { PositionSnapshotId = Guid.NewGuid() };
        var exception = Assert.Throws<InvalidDataException>(() =>
            Arch7bPositionMarketSlotLineageContract.BindRevision(lineage, projection));
        Assert.Equal(Arch7bPositionMarketSlotLineageContract.RevisionInputMismatch,
            exception.Message);
    }

    [Fact]
    public void Arch7a_revision_mismatch_is_rejected()
    {
        var lineage = ValidLineage();
        var projection = ValidProjection();
        var binding = Arch7bPositionMarketSlotLineageContract.BindRevision(
            lineage, projection);
        var exception = Assert.Throws<InvalidDataException>(() =>
            Arch7bPositionMarketSlotLineageContract.RequireArch7aRevision(
                binding, Guid.NewGuid()));
        Assert.Equal(Arch7bPositionMarketSlotLineageContract.RevisionInputMismatch,
            exception.Message);
    }

    private static readonly DateTimeOffset Start =
        new(2026, 8, 3, 10, 0, 0, TimeSpan.Zero);

    private static Arch7bPositionMarketSlotLineage ValidLineage()
    {
        var draft = Arch7bPositionMarketSlotLineageContract.BuildDraft(
            "arch7b-live-next", "1754288005", "ARCH7B_RDS_TEST",
            Commit('a'), Commit('b'), Source(),
            PmsShadowIntradayCadenceContract.WindowEnding(Start.AddMinutes(15)),
            "lmax-md-next", MarketSymbols());
        return Arch7bPositionMarketSlotLineageContract.Finalize(
            draft, Sha('c'), Sha('d'), Sha('e'), Sha('f'), 49,
            Start.AddSeconds(1), Start.AddMinutes(14));
    }

    private static PmsShadowEconomicSource Source()
    {
        var mappings = Enumerable.Range(1, 99).Select(value =>
            new PmsShadowEconomicMapping(GuidFrom(value), GuidFrom(value + 100),
                GuidFrom(value + 200), value.ToString("D4"), $"S{value:D5}",
                (10_000 + value).ToString(), 1m, 0.01m, 0.00001m)).ToArray();
        return new(GuidFrom(500), "arch6b-source", GuidFrom(501), 1_000_000m,
            GuidFrom(502), Start.AddMinutes(-1), "LMAX_PORTAL_GLOBAL_FLAT_EXPLICIT",
            mappings.ToDictionary(value => value.InstrumentId, _ => 0m), mappings, []);
    }

    private static PmsShadowIntradayEconomicProjection ValidProjection()
    {
        var lineage = ValidLineageWithoutProjection();
        var observations = Enumerable.Range(1, 99).Select(value =>
            new PmsShadowSlotMarketObservation(GuidFrom(value), value.ToString("D4"),
                $"S{value:D5}", (10_000 + value).ToString(), 1m, 1.1m, 1.05m,
                Start.AddMinutes(14), "LMAX_DIRECT", [value.ToString("D4")]))
            .ToArray();
        return new(GuidFrom(700), 1, lineage.SlotId, lineage.SlotStartUtc,
            lineage.SlotEndUtc, Sha('1'), GuidFrom(701), Sha('2'),
            lineage.SourceIngestionId, lineage.SourceSessionId, GuidFrom(501),
            lineage.SelectedPositionSnapshotId, lineage.PositionSnapshotAsOfUtc,
            lineage.PositionAuthority, [], [], [], observations, [], [],
            Sha('3'), Sha('4'), Sha('5'), Sha('6'), null, "COMPLETED",
            PmsShadowStateContract.CompletedNoExternal, true, true,
            lineage.SlotEndUtc.AddMinutes(1));
    }

    private static Arch7bPositionMarketSlotLineage ValidLineageWithoutProjection()
    {
        var draft = Arch7bPositionMarketSlotLineageContract.BuildDraft(
            "arch7b-live-next", "1754288005", "ARCH7B_RDS_TEST",
            Commit('a'), Commit('b'), Source(),
            PmsShadowIntradayCadenceContract.WindowEnding(Start.AddMinutes(15)),
            "lmax-md-next", MarketSymbols());
        return Arch7bPositionMarketSlotLineageContract.Finalize(
            draft, Sha('c'), Sha('d'), Sha('e'), Sha('f'), 49,
            Start.AddSeconds(1), Start.AddMinutes(14));
    }

    private static void RejectExact(
        Func<Arch7bPositionMarketSlotLineage, Arch7bPositionMarketSlotLineage> mutate,
        string blocker)
    {
        var expected = ValidLineage();
        var exception = Assert.Throws<InvalidDataException>(() =>
            Arch7bPositionMarketSlotLineageContract.RequireExactBinding(
                expected, mutate(expected)));
        Assert.Equal(blocker, exception.Message);
    }

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

    private static string[] MarketSymbols() => Enumerable.Range(1, 49)
        .Select(value => $"M{value:D5}").ToArray();
    private static Guid GuidFrom(int value) =>
        Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
    private static string Sha(char value) => new(value, 64);
    private static string Commit(char value) => new(value, 40);
}
