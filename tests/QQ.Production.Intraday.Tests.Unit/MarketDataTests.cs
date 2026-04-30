using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class MarketDataTests
{
    private static readonly DateTimeOffset Now = new(2026, 04, 29, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Snapshot_validates_and_derives_mid_and_spread()
    {
        var state = SeedData.Create(Now);
        var snapshot = new MarketDataSnapshot(MarketDataSnapshotId.New(), state.Instruments.Single().Id, state.Venues.Single().Id, 1.10000m, 1.10004m, null, "Test", Now, Now);

        snapshot.Validate();

        Assert.Equal(1.10002m, snapshot.Mid);
        Assert.Equal(0.00004m, snapshot.Spread);
    }

    [Fact]
    public void Snapshot_rejects_ask_below_bid()
    {
        var state = SeedData.Create(Now);
        var snapshot = new MarketDataSnapshot(MarketDataSnapshotId.New(), state.Instruments.Single().Id, state.Venues.Single().Id, 1.10005m, 1.10004m, null, "Test", Now, Now);

        Assert.Throws<DomainRuleViolationException>(snapshot.Validate);
    }

    [Theory]
    [InlineData("2026-04-29T09:00:00Z", "2026-04-29T09:00:00Z")]
    [InlineData("2026-04-29T09:14:59Z", "2026-04-29T09:00:00Z")]
    [InlineData("2026-04-29T09:15:00Z", "2026-04-29T09:15:00Z")]
    [InlineData("2026-04-29T09:29:59Z", "2026-04-29T09:15:00Z")]
    [InlineData("2026-04-29T09:30:00Z", "2026-04-29T09:30:00Z")]
    [InlineData("2026-04-30T00:00:00Z", "2026-04-30T00:00:00Z")]
    public void Fifteen_minute_alignment_returns_containing_bar_start(string timestamp, string expected)
        => Assert.Equal(DateTimeOffset.Parse(expected), BarIntervalAlignment.GetBarStart(DateTimeOffset.Parse(timestamp), BarTimeframe.FifteenMinutes));

    [Fact]
    public void Enumerate_intervals_uses_half_open_utc_boundaries_across_midnight()
    {
        var intervals = BarIntervalAlignment.EnumerateIntervals(DateTimeOffset.Parse("2026-04-29T23:45:00Z"), DateTimeOffset.Parse("2026-04-30T00:15:00Z"), BarTimeframe.FifteenMinutes);

        Assert.Equal(2, intervals.Count);
        Assert.Equal(DateTimeOffset.Parse("2026-04-29T23:45:00Z"), intervals[0].StartUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-04-30T00:00:00Z"), intervals[1].StartUtc);
    }

    [Fact]
    public async Task Bar_builder_builds_complete_bar_with_decimal_ohlc_and_spread_average()
    {
        var state = SeedData.Create(Now);
        state.MarketData.Clear();
        var clock = new FixedClock(DateTimeOffset.Parse("2026-04-29T09:30:00Z"));
        await AddSnapshots(state, clock, DateTimeOffset.Parse("2026-04-29T09:15:00Z"), [1.10000m, 1.10010m, 1.10005m], [1.10004m, 1.10015m, 1.10007m]);

        var result = await CreateBuilder(state, clock).BuildBarsAsync(state.Venues.Single().Id, BarTimeframe.FifteenMinutes, DateTimeOffset.Parse("2026-04-29T09:15:00Z"), DateTimeOffset.Parse("2026-04-29T09:30:00Z"), CancellationToken.None);

        Assert.Equal(BarBuildRunStatus.Completed, result.Status);
        var bar = Assert.Single(state.MarketDataBars);
        Assert.Equal(1.10000m, bar.BidOpen);
        Assert.Equal(1.10010m, bar.BidHigh);
        Assert.Equal(1.10000m, bar.BidLow);
        Assert.Equal(1.10005m, bar.BidClose);
        Assert.Equal(1.10004m, bar.AskOpen);
        Assert.Equal(1.10015m, bar.AskHigh);
        Assert.Equal(1.10004m, bar.AskLow);
        Assert.Equal(1.10007m, bar.AskClose);
        Assert.Equal(1.10002m, bar.MidOpen);
        Assert.Equal(0.00004m, bar.SpreadOpen);
        Assert.Equal(0.00005m, bar.SpreadHigh);
        Assert.Equal(0.00002m, bar.SpreadLow);
        Assert.Equal(0.00002m, bar.SpreadClose);
        Assert.Equal(0.0000366666666666666666666667m, bar.SpreadAverage);
        Assert.Equal(3, bar.ObservationCount);
        Assert.True(bar.IsComplete);
        Assert.Equal(BarQualityStatus.Complete, bar.QualityStatus);
    }

    [Fact]
    public async Task Bar_builder_respects_half_open_interval_boundary()
    {
        var state = SeedData.Create(Now);
        state.MarketData.Clear();
        var clock = new FixedClock(DateTimeOffset.Parse("2026-04-29T09:45:00Z"));
        await AddSnapshots(state, clock, DateTimeOffset.Parse("2026-04-29T09:15:00Z"), [1.1m, 1.2m], [1.1001m, 1.2001m], TimeSpan.FromMinutes(15));

        await CreateBuilder(state, clock).BuildBarsAsync(state.Venues.Single().Id, BarTimeframe.FifteenMinutes, DateTimeOffset.Parse("2026-04-29T09:15:00Z"), DateTimeOffset.Parse("2026-04-29T09:45:00Z"), CancellationToken.None);

        var first = state.MarketDataBars.Single(x => x.BarStartUtc == DateTimeOffset.Parse("2026-04-29T09:15:00Z"));
        var second = state.MarketDataBars.Single(x => x.BarStartUtc == DateTimeOffset.Parse("2026-04-29T09:30:00Z"));
        Assert.Equal(1, first.ObservationCount);
        Assert.Equal(1.1m, first.BidOpen);
        Assert.Equal(1, second.ObservationCount);
        Assert.Equal(1.2m, second.BidOpen);
    }

    [Fact]
    public async Task Bar_builder_marks_sparse_and_incomplete_bars()
    {
        var state = SeedData.Create(Now);
        state.MarketData.Clear();
        var clock = new FixedClock(DateTimeOffset.Parse("2026-04-29T09:20:00Z"));
        await AddSnapshots(state, clock, DateTimeOffset.Parse("2026-04-29T09:15:00Z"), [1.1m], [1.1001m]);

        await CreateBuilder(state, clock).BuildBarsAsync(state.Venues.Single().Id, BarTimeframe.FifteenMinutes, DateTimeOffset.Parse("2026-04-29T09:15:00Z"), DateTimeOffset.Parse("2026-04-29T09:30:00Z"), CancellationToken.None);
        Assert.Equal(BarQualityStatus.Incomplete, state.MarketDataBars.Single().QualityStatus);

        state.MarketDataBars.Clear();
        clock.UtcNow = DateTimeOffset.Parse("2026-04-29T09:45:00Z");
        await CreateBuilder(state, clock).BuildBarsAsync(state.Venues.Single().Id, BarTimeframe.FifteenMinutes, DateTimeOffset.Parse("2026-04-29T09:15:00Z"), DateTimeOffset.Parse("2026-04-29T09:30:00Z"), CancellationToken.None);
        Assert.Equal(BarQualityStatus.SparseData, state.MarketDataBars.Single().QualityStatus);
    }

    [Fact]
    public async Task Bar_builder_upsert_is_idempotent()
    {
        var state = SeedData.Create(Now);
        state.MarketData.Clear();
        var clock = new FixedClock(DateTimeOffset.Parse("2026-04-29T09:45:00Z"));
        await AddSnapshots(state, clock, DateTimeOffset.Parse("2026-04-29T09:15:00Z"), [1.1m, 1.2m, 1.3m], [1.1001m, 1.2001m, 1.3001m]);
        var builder = CreateBuilder(state, clock);

        await builder.BuildBarsAsync(state.Venues.Single().Id, BarTimeframe.FifteenMinutes, DateTimeOffset.Parse("2026-04-29T09:15:00Z"), DateTimeOffset.Parse("2026-04-29T09:30:00Z"), CancellationToken.None);
        await builder.BuildBarsAsync(state.Venues.Single().Id, BarTimeframe.FifteenMinutes, DateTimeOffset.Parse("2026-04-29T09:15:00Z"), DateTimeOffset.Parse("2026-04-29T09:30:00Z"), CancellationToken.None);

        Assert.Single(state.MarketDataBars);
        Assert.Equal(2, state.BarBuildRuns.Count);
        Assert.Equal(1, state.BarBuildRuns.Count(x => x.BarsCreated == 1));
        Assert.Equal(1, state.BarBuildRuns.Count(x => x.BarsUpdated == 1));
    }

    private static async Task AddSnapshots(PlatformState state, FixedClock clock, DateTimeOffset start, decimal[] bids, decimal[] asks, TimeSpan? interval = null)
    {
        var repository = new InMemoryMarketDataSnapshotRepository(state);
        var snapshots = bids.Select((bid, i) => new MarketDataSnapshot(MarketDataSnapshotId.New(), state.Instruments.Single().Id, state.Venues.Single().Id, bid, asks[i], null, "Test", start.AddTicks((interval ?? TimeSpan.FromMinutes(1)).Ticks * i), clock.UtcNow) { IsSynthetic = true, CreatedAtUtc = clock.UtcNow }).ToList();
        await repository.AddRangeAsync(snapshots, CancellationToken.None);
    }

    private static BarBuilderService CreateBuilder(PlatformState state, FixedClock clock)
        => new(state, new InMemoryMarketDataSnapshotRepository(state), new InMemoryMarketDataBarRepository(state), new InMemoryBarBuildRunRepository(state, clock), clock, new BarBuilderOptions { FifteenMinuteMinimumObservationCount = 3 });
}
