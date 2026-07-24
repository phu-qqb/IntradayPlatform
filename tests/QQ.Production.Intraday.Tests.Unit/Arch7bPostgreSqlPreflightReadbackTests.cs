using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bPostgreSqlPreflightReadbackTests
{
    private static readonly Guid EconomicRevisionId =
        Guid.Parse("68b9a204-bc63-a546-b60e-ff46b685617a");
    private static readonly Guid IngestionId =
        Guid.Parse("11111111-2222-3333-4444-555555555555");
    private const string SlotId = "pms-shadow-15m-20260723T1745Z";
    private const string SourceSessionId =
        "arch6b-daily-tier1-20260721T130346Z-422530a8";
    private const string MarketSha =
        "dde31b6ae0fecb9f4c44ee4fb8d076eec908c48b02890b91ae6145c4d8bddc73";

    [Fact]
    public void Canonical_query_reads_only_the_intraday_revision_and_observation_tables()
    {
        var sql = EfArch7bPostgreSqlPreflightReader.CanonicalMarketObservationSql;

        Assert.Contains("pms_shadow.intraday_projection_revisions", sql);
        Assert.Contains("pms_shadow.intraday_market_data_observations", sql);
        Assert.Contains("pr.projection_revision_id = @economic_revision_id", sql);
        Assert.DoesNotContain("pms_shadow.market_data_snapshots", sql);
        Assert.DoesNotContain("pms_shadow.market_data_observations AS", sql);
    }

    [Fact]
    public void Exact_real_arch7b_revision_identity_resolves_without_legacy_fallback()
    {
        var selected = Arch7bIntradayMarketObservationResolver.Resolve(
            Expected(),
            [Valid()]);

        Assert.Equal(EconomicRevisionId, selected.EconomicRevisionId);
        Assert.Equal(SlotId, selected.SlotId);
        Assert.Equal("4002", selected.SecurityId);
        Assert.Equal("GBPUSD", selected.Symbol);
        Assert.Equal(MarketSha, selected.MarketDataSnapshotSha256);
        Assert.Equal("LMAX_DIRECT", selected.ProjectionMethod);
    }

    [Fact]
    public void Zero_canonical_observations_fails_closed_even_if_legacy_rows_might_exist()
        => AssertBlocker(
            [],
            "ARCH7B_POSTGRESQL_PREFLIGHT_INTRADAY_MARKET_OBSERVATION_MISSING");

    [Fact]
    public void Wrong_slot_fails_closed()
        => AssertBlocker(
            [Valid() with { SlotId = "pms-shadow-15m-20260723T1730Z" }],
            "ARCH7B_POSTGRESQL_PREFLIGHT_INTRADAY_MARKET_OBSERVATION_SLOT_MISMATCH");

    [Fact]
    public void Wrong_market_snapshot_sha_fails_closed()
        => AssertBlocker(
            [Valid() with { MarketDataSnapshotSha256 = new string('a', 64) }],
            "ARCH7B_POSTGRESQL_PREFLIGHT_INTRADAY_MARKET_OBSERVATION_SHA_MISMATCH");

    [Fact]
    public void Wrong_security_id_fails_closed()
        => AssertBlocker(
            [Valid() with { SecurityId = "4001", Symbol = "EURUSD", LmaxInstrumentId = "4001" }],
            "ARCH7B_POSTGRESQL_PREFLIGHT_INTRADAY_MARKET_OBSERVATION_INSTRUMENT_MISMATCH");

    [Fact]
    public void Multiple_matching_security_rows_are_ambiguous()
        => AssertBlocker(
            [Valid(), Valid() with { InstrumentId = Guid.NewGuid() }],
            "ARCH7B_POSTGRESQL_PREFLIGHT_INTRADAY_MARKET_OBSERVATION_AMBIGUOUS");

    [Fact]
    public void Polygon_projection_is_never_an_order_price_source()
        => AssertBlocker(
            [Valid() with
            {
                ProjectionMethod = "POLYGON_DIRECT",
                ProjectionLegSecurityIdsJson = "[\"POLYGON:GBPUSD\"]"
            }],
            "ARCH7B_POSTGRESQL_PREFLIGHT_INTRADAY_MARKET_OBSERVATION_NON_LMAX");

    [Fact]
    public void Canonical_intraday_row_wins_by_construction_over_contradictory_legacy_data()
    {
        var sql = EfArch7bPostgreSqlPreflightReader.CanonicalMarketObservationSql;
        var selected = Arch7bIntradayMarketObservationResolver.Resolve(
            Expected(),
            [Valid()]);

        Assert.Equal(MarketSha, selected.MarketDataSnapshotSha256);
        Assert.DoesNotContain("pms_shadow.market_data_snapshots", sql);
        Assert.DoesNotContain("pms_shadow.market_data_observations", sql);
    }

    [Fact]
    public void Incomplete_ingestion_or_source_session_lineage_fails_closed()
        => AssertBlocker(
            [Valid() with { SourceIngestionId = Guid.NewGuid() }],
            "ARCH7B_POSTGRESQL_PREFLIGHT_INTRADAY_MARKET_OBSERVATION_LINEAGE_INCOMPLETE");

    [Fact]
    public void Event_outside_the_exact_slot_fails_closed()
        => AssertBlocker(
            [Valid() with { EventTimeUtc = new DateTimeOffset(2026, 7, 23, 18, 0, 1, TimeSpan.Zero) }],
            "ARCH7B_POSTGRESQL_PREFLIGHT_INTRADAY_MARKET_OBSERVATION_SLOT_MISMATCH");

    private static void AssertBlocker(
        IReadOnlyList<Arch7bIntradayMarketObservationReadRow> rows,
        string expected)
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => Arch7bIntradayMarketObservationResolver.Resolve(Expected(), rows));

        Assert.Equal(expected, error.Message);
    }

    private static Arch7bIntradayMarketObservationExpectation Expected()
        => new(
            EconomicRevisionId,
            2,
            SlotId,
            IngestionId,
            SourceSessionId,
            MarketSha,
            "4002",
            "GBPUSD");

    private static Arch7bIntradayMarketObservationReadRow Valid()
        => new(
            EconomicRevisionId,
            2,
            SlotId,
            IngestionId,
            SourceSessionId,
            MarketSha,
            "COMPLETED",
            PmsShadowStateContract.CompletedNoExternal,
            true,
            true,
            new DateTimeOffset(2026, 7, 23, 17, 45, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 23, 18, 0, 0, TimeSpan.Zero),
            "COMPLETED",
            true,
            Guid.Parse("99999999-8888-7777-6666-555555555555"),
            "4002",
            "GBPUSD",
            "4002",
            1.34000m,
            1.34010m,
            new DateTimeOffset(2026, 7, 23, 17, 59, 59, TimeSpan.Zero),
            "LMAX_DIRECT",
            "[\"4002\"]");
}
