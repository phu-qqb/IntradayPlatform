using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Infrastructure.Simulator;

namespace QQ.Production.Intraday.Tests.Integration;

public sealed class MarketDataIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 04, 29, 10, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task Persist_snapshots_and_query_latest_snapshot()
    {
        var state = SeedData.Create(Now);
        state.MarketData.Clear();
        var repository = new InMemoryMarketDataSnapshotRepository(state);
        var first = Snapshot(state, DateTimeOffset.Parse("2026-04-29T09:15:00Z"), 1.1000m, 1.1002m);
        var second = Snapshot(state, DateTimeOffset.Parse("2026-04-29T09:16:00Z"), 1.1010m, 1.1012m);

        await repository.AddRangeAsync([first, second], CancellationToken.None);
        var latest = await repository.GetLatestAsync(state.Instruments.Single().Id, state.Venues.Single().Id, CancellationToken.None);

        Assert.NotNull(latest);
        Assert.Equal(1.1010m, latest.Bid);
    }

    [Fact]
    public async Task Persist_snapshots_and_build_fifteen_minute_bars()
    {
        var state = SeedData.Create(Now);
        state.MarketData.Clear();
        var snapshotRepository = new InMemoryMarketDataSnapshotRepository(state);
        await snapshotRepository.AddRangeAsync([
            Snapshot(state, DateTimeOffset.Parse("2026-04-29T09:15:00Z"), 1.1000m, 1.1002m),
            Snapshot(state, DateTimeOffset.Parse("2026-04-29T09:16:00Z"), 1.1010m, 1.1012m),
            Snapshot(state, DateTimeOffset.Parse("2026-04-29T09:17:00Z"), 1.0990m, 1.0993m)
        ], CancellationToken.None);

        var result = await CreateBuilder(state).BuildBarsAsync(state.Venues.Single().Id, BarTimeframe.FifteenMinutes, DateTimeOffset.Parse("2026-04-29T09:15:00Z"), DateTimeOffset.Parse("2026-04-29T09:30:00Z"), CancellationToken.None);

        Assert.Equal(BarBuildRunStatus.Completed, result.Status);
        var bar = Assert.Single(state.MarketDataBars);
        Assert.Equal(1.1010m, bar.BidHigh);
        Assert.Equal(1.0990m, bar.BidLow);
        Assert.Equal(BarQualityStatus.Complete, bar.QualityStatus);
    }

    [Fact]
    public async Task Unique_bar_upsert_prevents_duplicate_bars()
    {
        var state = SeedData.Create(Now);
        var repository = new InMemoryMarketDataBarRepository(state);
        var bar = EmptyBar(state, DateTimeOffset.Parse("2026-04-29T09:15:00Z"));

        var first = await repository.UpsertAsync(bar, CancellationToken.None);
        var second = await repository.UpsertAsync(bar with { BidClose = 1.2m }, CancellationToken.None);

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Single(state.MarketDataBars);
        Assert.Equal(1.2m, state.MarketDataBars.Single().BidClose);
    }

    [Fact]
    public async Task Api_fake_snapshots_endpoint_creates_snapshots()
    {
        await using var factory = CreateInMemoryFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/market-data/fake-snapshots", new
        {
            startUtc = DateTimeOffset.Parse("2026-04-29T09:15:00Z"),
            intervalSeconds = 60,
            count = 3,
            bid = 1.1000m,
            ask = 1.1002m,
            bidStep = 0.0001m,
            askStep = 0.0001m
        });

        response.EnsureSuccessStatusCode();
        var snapshots = await client.GetFromJsonAsync<List<ApiSnapshot>>("/market-data/snapshots?instrument=EURUSD&venue=LMAX");
        Assert.NotNull(snapshots);
        Assert.True(snapshots.Count >= 3);
    }

    [Fact]
    public async Task Api_build_bars_endpoint_creates_bars()
    {
        await using var factory = CreateInMemoryFactory();
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/market-data/fake-snapshots", new
        {
            startUtc = DateTimeOffset.Parse("2026-04-29T09:15:00Z"),
            intervalSeconds = 60,
            count = 3,
            bid = 1.1000m,
            ask = 1.1002m,
            bidStep = 0.0001m,
            askStep = 0.0001m
        });

        var response = await client.PostAsJsonAsync("/market-data/build-bars", new
        {
            venueName = "LMAX",
            timeframe = BarTimeframe.FifteenMinutes,
            startUtc = DateTimeOffset.Parse("2026-04-29T09:15:00Z"),
            endUtc = DateTimeOffset.Parse("2026-04-29T09:30:00Z")
        });

        response.EnsureSuccessStatusCode();
        var bars = await client.GetFromJsonAsync<List<ApiBar>>("/market-data/bars?instrument=EURUSD&venue=LMAX");
        Assert.NotNull(bars);
        Assert.NotEmpty(bars);
    }

    [Fact]
    public async Task Bar_building_does_not_change_execution_workflow_behavior()
    {
        var state = SeedData.Create(Now);
        var snapshots = await new FakeMarketDataProvider(new FixedClock(Now)).GetSnapshotsAsync(state.Instruments.Single(), state.Venues.Single(), Now.AddMinutes(-15), TimeSpan.FromMinutes(1), 3, 1.1m, 1.1002m, 0.0001m, 0.0001m, CancellationToken.None);
        await new InMemoryMarketDataSnapshotRepository(state).AddRangeAsync(snapshots, CancellationToken.None);
        var builder = CreateBuilder(state);
        await builder.BuildBarsAsync(state.Venues.Single().Id, BarTimeframe.FifteenMinutes, Now.AddMinutes(-15), Now, CancellationToken.None);
        var service = new ProcessModelRunService(new InMemoryIntradayRepository(state), new FakeLmaxGateway(new FakeLmaxOptions(), new FixedClock(Now)), new FakeBrokerPositionProvider(state, new FixedClock(Now)), new FixedClock(Now));

        await service.ProcessNextAsync();

        Assert.Single(state.MarketDataBars);
        Assert.Single(state.TradeIntents);
        Assert.Single(state.Fills);
    }

    [Fact]
    public async Task Bar_build_failure_does_not_create_orders_or_affect_execution_workflow()
    {
        var state = SeedData.Create(Now);
        var result = await CreateBuilder(state).BuildBarsAsync(state.Venues.Single().Id, BarTimeframe.OneHour, Now.AddHours(-1), Now, CancellationToken.None);

        Assert.Equal(BarBuildRunStatus.Failed, result.Status);
        Assert.Empty(state.ParentOrders);
        Assert.Empty(state.TradeIntents);
    }

    [Fact]
    public async Task Worker_bar_build_service_can_run_locally_from_fake_data_only()
    {
        var state = SeedData.Create(Now);
        state.MarketData.Clear();
        var clock = new FixedClock(Now);
        var provider = new FakeMarketDataProvider(clock);
        var snapshots = await provider.GetSnapshotsAsync(state.Instruments.Single(), state.Venues.Single(), Now.AddMinutes(-15), TimeSpan.FromMinutes(1), 3, 1.1m, 1.1002m, 0.0001m, 0.0001m, CancellationToken.None);
        await new InMemoryMarketDataSnapshotRepository(state).AddRangeAsync(snapshots, CancellationToken.None);

        var result = await CreateBuilder(state, clock).BuildLatestFifteenMinuteBarsAsync(state.Venues.Single().Id, CancellationToken.None);

        Assert.Equal(BarBuildRunStatus.Completed, result.Status);
        Assert.Single(state.MarketDataBars);
    }

    private static MarketDataSnapshot Snapshot(PlatformState state, DateTimeOffset timestamp, decimal bid, decimal ask)
        => new(MarketDataSnapshotId.New(), state.Instruments.Single().Id, state.Venues.Single().Id, bid, ask, null, "Test", timestamp, Now) { IsSynthetic = true, CreatedAtUtc = Now };

    private static MarketDataBar EmptyBar(PlatformState state, DateTimeOffset start)
        => new(MarketDataBarId.New(), state.Instruments.Single().Id, state.Venues.Single().Id, BarTimeframe.FifteenMinutes, start, start.AddMinutes(15), "Test", 1m, 1m, 1m, 1m, 1.1m, 1.1m, 1.1m, 1.1m, 1.05m, 1.05m, 1.05m, 1.05m, 0.1m, 0.1m, 0.1m, 0.1m, 0.1m, 1, start, start, true, BarQualityStatus.Complete, null, "test", Now);

    private static BarBuilderService CreateBuilder(PlatformState state)
        => CreateBuilder(state, new FixedClock(Now));

    private static BarBuilderService CreateBuilder(PlatformState state, FixedClock clock)
        => new(state, new InMemoryMarketDataSnapshotRepository(state), new InMemoryMarketDataBarRepository(state), new InMemoryBarBuildRunRepository(state, clock), clock, new BarBuilderOptions { FifteenMinuteMinimumObservationCount = 3 });

    private static WebApplicationFactory<Program> CreateInMemoryFactory()
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Persistence:Provider"] = "InMemory",
                    ["Safety:AllowExternalConnections"] = "false",
                    ["Safety:AllowLiveTrading"] = "false",
                    ["Safety:RequireFakeExecutionGateway"] = "true"
                });
            });
        });

    private sealed record ApiSnapshot(decimal Bid, decimal Ask);
    private sealed record ApiBar(DateTimeOffset BarStartUtc, int ObservationCount);
}
