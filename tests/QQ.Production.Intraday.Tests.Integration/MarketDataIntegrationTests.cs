using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    public async Task Api_process_endpoint_returns_processed_json_for_clean_fake_lmax_path()
    {
        await using var factory = CreateInMemoryFactory();
        var client = factory.CreateClient();
        var now = DateTimeOffset.UtcNow;
        await CreateFakeSnapshotsAsync(client, now.AddMinutes(-1), 2);
        var run = await CreateModelRunAsync(client, now);

        var response = await client.PostAsync($"/model-runs/{run.Id}/process", null);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiProcessResult>();
        Assert.NotNull(result);
        Assert.Equal("Processed", result.Status);
        Assert.True(result.Processed);
        Assert.Equal(1, result.TradeIntentCount);
        Assert.Equal(1, result.OrderCount);
        Assert.Equal(1, result.FillCount);
    }

    [Fact]
    public async Task Api_orders_endpoint_returns_plain_non_empty_string_ids_after_process()
    {
        await using var factory = CreateInMemoryFactory();
        var client = factory.CreateClient();
        var now = DateTimeOffset.UtcNow;
        await CreateFakeSnapshotsAsync(client, now.AddMinutes(-1), 2);
        var run = await CreateModelRunAsync(client, now);
        (await client.PostAsync($"/model-runs/{run.Id}/process", null)).EnsureSuccessStatusCode();

        var orders = await client.GetFromJsonAsync<ApiOrdersResponse>("/orders");

        Assert.NotNull(orders);
        var parent = Assert.Single(orders.ParentOrders);
        var child = Assert.Single(orders.ChildOrders);
        Assert.False(string.IsNullOrWhiteSpace(parent.Id));
        Assert.False(string.IsNullOrWhiteSpace(parent.TradeIntentId));
        Assert.False(string.IsNullOrWhiteSpace(parent.InstrumentId));
        Assert.False(string.IsNullOrWhiteSpace(parent.ClientOrderId));
        Assert.False(string.IsNullOrWhiteSpace(child.Id));
        Assert.False(string.IsNullOrWhiteSpace(child.ParentOrderId));
        Assert.False(string.IsNullOrWhiteSpace(child.VenueId));
        Assert.False(string.IsNullOrWhiteSpace(child.InstrumentId));
        Assert.False(string.IsNullOrWhiteSpace(child.ClientOrderId));
        Assert.False(string.IsNullOrWhiteSpace(child.BrokerOrderId));
        Assert.DoesNotContain("{", parent.Id);
        Assert.DoesNotContain("{", child.ClientOrderId);
    }

    [Fact]
    public async Task Api_process_endpoint_returns_blocked_json_for_stale_model_run()
    {
        await using var factory = CreateInMemoryFactory();
        var client = factory.CreateClient();
        await CreateFakeSnapshotsAsync(client, DateTimeOffset.UtcNow.AddMinutes(-1), 2);
        var run = await CreateModelRunAsync(client, DateTimeOffset.UtcNow.AddDays(-2));

        var response = await client.PostAsync($"/model-runs/{run.Id}/process", null);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiProcessResult>();
        Assert.NotNull(result);
        Assert.Equal("Blocked", result.Status);
        Assert.Equal("StaleModelRun", result.BlockedReason);
        Assert.Equal(0, result.OrderCount);
        Assert.Equal(0, result.FillCount);
    }

    [Fact]
    public async Task Api_process_endpoint_returns_blocked_json_for_stale_market_data()
    {
        var now = DateTimeOffset.UtcNow;
        var state = SeedData.Create(now);
        state.MarketData[0] = state.MarketData[0] with { ReceivedAtUtc = now.AddHours(-2) };
        await using var factory = CreateInMemoryFactory(now, state);
        var client = factory.CreateClient();
        var run = await CreateModelRunAsync(client, now);

        var response = await client.PostAsync($"/model-runs/{run.Id}/process", null);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiProcessResult>();
        Assert.NotNull(result);
        Assert.Equal("Blocked", result.Status);
        Assert.Equal("StaleMarketData", result.BlockedReason);
        Assert.Equal(0, result.OrderCount);
        Assert.Equal(0, result.FillCount);
    }

    [Fact]
    public async Task Api_process_endpoint_returns_already_processed_for_duplicate_processing()
    {
        await using var factory = CreateInMemoryFactory();
        var client = factory.CreateClient();
        var now = DateTimeOffset.UtcNow;
        await CreateFakeSnapshotsAsync(client, now.AddMinutes(-1), 2);
        var run = await CreateModelRunAsync(client, now);

        (await client.PostAsync($"/model-runs/{run.Id}/process", null)).EnsureSuccessStatusCode();
        var response = await client.PostAsync($"/model-runs/{run.Id}/process", null);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiProcessResult>();
        Assert.NotNull(result);
        Assert.Equal("AlreadyProcessed", result.Status);
        Assert.True(result.IsAlreadyProcessed);
    }

    [Fact]
    public async Task Api_smoke_like_dynamic_workflow_does_not_return_http_500()
    {
        await using var factory = CreateInMemoryFactory();
        var client = factory.CreateClient();
        var now = DateTimeOffset.UtcNow;
        var floorMinute = now.Minute / 15 * 15;
        var barEnd = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, floorMinute, 0, TimeSpan.Zero);
        var barStart = barEnd.AddMinutes(-15);

        await CreateFakeSnapshotsAsync(client, barStart, 15);
        (await client.PostAsJsonAsync("/market-data/build-bars", new { venueName = "LMAX", timeframe = "FifteenMinutes", startUtc = barStart, endUtc = barEnd })).EnsureSuccessStatusCode();
        await CreateFakeSnapshotsAsync(client, now.AddMinutes(-1), 2);
        var run = await CreateModelRunAsync(client, now);

        var response = await client.PostAsync($"/model-runs/{run.Id}/process", null);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiProcessResult>();
        Assert.NotNull(result);
        Assert.NotEqual("Failed", result.Status);
    }

    [Fact]
    public async Task Api_reference_data_integrity_endpoint_returns_clean_state()
    {
        await using var factory = CreateInMemoryFactory(Now);
        var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<ApiReferenceDataIntegrityCheck>("/admin/reference-data/integrity");

        Assert.NotNull(result);
        Assert.Equal(0, result.BlockingIssueCount);
    }

    [Fact]
    public async Task Api_health_and_admin_endpoints_report_local_fake_state()
    {
        await using var factory = CreateInMemoryFactory(Now);
        var client = factory.CreateClient();

        var health = await client.GetFromJsonAsync<ApiHealth>("/health");
        var killSwitch = await client.GetFromJsonAsync<ApiKillSwitch>("/admin/kill-switch");
        var instruments = await client.GetFromJsonAsync<List<ApiInstrument>>("/instruments");
        var venues = await client.GetFromJsonAsync<List<ApiVenue>>("/venues");

        Assert.NotNull(health);
        Assert.Equal("FakeLmaxGateway", health.ExecutionGateway);
        Assert.Equal("FakeMarketDataProvider", health.MarketDataMode);
        Assert.False(health.LiveTradingEnabled);
        Assert.False(health.ExternalConnectionsEnabled);
        Assert.NotNull(killSwitch);
        Assert.False(killSwitch.IsActive);
        Assert.Contains(instruments!, x => x.Symbol == "EURUSD");
        Assert.Contains(venues!, x => x.Name == "LMAX");
    }

    [Fact]
    public async Task Api_ui_list_endpoints_return_plain_dtos()
    {
        await using var factory = CreateInMemoryFactory();
        var client = factory.CreateClient();
        var now = DateTimeOffset.UtcNow;
        await CreateFakeSnapshotsAsync(client, now.AddMinutes(-1), 2);
        var run = await CreateModelRunAsync(client, now);
        (await client.PostAsync($"/model-runs/{run.Id}/process", null)).EnsureSuccessStatusCode();

        var targets = await client.GetFromJsonAsync<List<ApiTargetPosition>>("/target-positions");
        var drifts = await client.GetFromJsonAsync<List<ApiDriftSnapshot>>("/drift-snapshots");
        var risk = await client.GetFromJsonAsync<List<ApiRiskDecision>>("/risk-decisions");
        var fills = await client.GetFromJsonAsync<List<ApiFill>>("/fills");

        Assert.NotNull(targets);
        Assert.NotEmpty(targets);
        Assert.DoesNotContain("{", targets[0].ModelRunId);
        Assert.NotNull(drifts);
        Assert.NotEmpty(drifts);
        Assert.NotNull(risk);
        Assert.NotEmpty(risk);
        Assert.NotNull(fills);
        Assert.NotEmpty(fills);
        Assert.False(string.IsNullOrWhiteSpace(fills[0].BrokerExecutionId));
    }

    [Fact]
    public async Task Api_lmax_eod_endpoints_return_plain_dtos_and_pnl_totals()
    {
        var state = SeedData.Create(Now);
        var instrument = state.Instruments.Single(x => x.Symbol == "EURUSD");
        var venue = state.Venues.Single(x => x.Name == "LMAX");
        state.Fills.Add(new Fill(FillId.New(), "API-EOD-EXEC-1", ChildOrderId.New(), instrument.Id, venue.Id, TradeSide.Sell, 10_000m, 1m, 1.10000m, Now, Now));
        await using var factory = CreateInMemoryFactory(Now, state);
        var client = factory.CreateClient();

        var reportDate = Now.ToString("yyyy-MM-dd");
        (await client.PostAsJsonAsync("/lmax-eod/generate-fake", new { reportDate, venueName = "LMAX", brokerAccountCode = "LMAX_DEMO_LOCAL", mutationMode = "None" })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/lmax-eod/import-generated", new { reportDate, venueName = "LMAX", brokerAccountCode = "LMAX_DEMO_LOCAL" })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/eod-reconciliation/run", new { reportDate, venueName = "LMAX", brokerAccountCode = "LMAX_DEMO_LOCAL" })).EnsureSuccessStatusCode();

        var importRuns = await client.GetFromJsonAsync<List<ApiLmaxImportRun>>("/lmax-eod/import-runs");
        var individualTrades = await client.GetFromJsonAsync<List<ApiLmaxIndividualTrade>>("/lmax-eod/individual-trades");
        var summaries = await client.GetFromJsonAsync<List<ApiLmaxTradeSummary>>("/lmax-eod/trade-summaries");
        var wallets = await client.GetFromJsonAsync<List<ApiLmaxCurrencyWallet>>("/lmax-eod/currency-wallets");
        var pnl = await client.GetFromJsonAsync<ApiEodPnlSummary>($"/eod-pnl/summary?reportDate={reportDate}&venueName=LMAX&brokerAccountCode=LMAX_DEMO_LOCAL");
        var breaks = await client.GetFromJsonAsync<List<ApiEodBreak>>("/eod-reconciliation/breaks");

        Assert.NotNull(importRuns);
        Assert.NotEmpty(importRuns);
        Assert.False(string.IsNullOrWhiteSpace(importRuns[0].Id));
        Assert.NotNull(individualTrades);
        Assert.NotEmpty(individualTrades);
        Assert.False(string.IsNullOrWhiteSpace(individualTrades[0].Id));
        Assert.False(string.IsNullOrWhiteSpace(individualTrades[0].ExecutionId));
        Assert.False(string.IsNullOrWhiteSpace(individualTrades[0].LmaxSymbol));
        Assert.True(individualTrades[0].TradePrice > 0m);
        Assert.NotNull(summaries);
        Assert.NotEmpty(summaries);
        Assert.False(string.IsNullOrWhiteSpace(summaries[0].Id));
        Assert.NotNull(wallets);
        Assert.NotEmpty(wallets);
        Assert.True(wallets[0].RateToBaseCcy > 0m);
        Assert.NotNull(pnl);
        Assert.Equal(pnl.TotalProfitLossUsd + pnl.TotalCommissionUsd + pnl.TotalDividendsUsd + pnl.TotalFinancingUsd, pnl.TotalNetPnlUsd);
        Assert.NotNull(breaks);
    }

    [Fact]
    public async Task Bar_building_does_not_change_execution_workflow_behavior()
    {
        var state = SeedData.Create(Now);
        var snapshots = await new FakeMarketDataProvider(new FixedClock(Now)).GetSnapshotsAsync(state.Instruments.Single(), state.Venues.Single(), Now.AddMinutes(-15), TimeSpan.FromMinutes(1), 3, 1.1m, 1.1002m, 0.0001m, 0.0001m, CancellationToken.None);
        await new InMemoryMarketDataSnapshotRepository(state).AddRangeAsync(snapshots, CancellationToken.None);
        var builder = CreateBuilder(state);
        await builder.BuildBarsAsync(state.Venues.Single().Id, BarTimeframe.FifteenMinutes, Now.AddMinutes(-15), Now, CancellationToken.None);
        var repository = new InMemoryIntradayRepository(state);
        var clock = new FixedClock(Now);
        var service = new ProcessModelRunService(repository, new FakeLmaxGateway(new FakeLmaxOptions(), clock), new FakeBrokerPositionProvider(state, clock), clock, new ReferenceDataIntegrityService(repository, clock));

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

    private static WebApplicationFactory<Program> CreateInMemoryFactory(DateTimeOffset? fixedNow = null, PlatformState? stateOverride = null)
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
            builder.ConfigureServices(services =>
            {
                if (fixedNow is not null)
                {
                    services.RemoveAll<IClock>();
                    services.AddSingleton<IClock>(new FixedClock(fixedNow.Value));
                }

                if (stateOverride is not null)
                {
                    services.RemoveAll<PlatformState>();
                    services.AddSingleton(stateOverride);
                }
            });
        });

    private static async Task CreateFakeSnapshotsAsync(HttpClient client, DateTimeOffset startUtc, int count)
    {
        var response = await client.PostAsJsonAsync("/market-data/fake-snapshots", new
        {
            startUtc,
            intervalSeconds = 60,
            count,
            bid = 1.1000m,
            ask = 1.1002m,
            bidStep = 0.00001m,
            askStep = 0.00001m
        });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<ApiModelRun> CreateModelRunAsync(HttpClient client, DateTimeOffset asOfUtc)
    {
        var response = await client.PostAsJsonAsync("/model-runs", new
        {
            modelName = "IntradayFxModel",
            asOfUtc,
            effectiveAtUtc = asOfUtc,
            navUsd = 1_000_000m,
            frequencyMinutes = 15,
            targetQuantityMode = "PortfolioBaseCurrencyNotional",
            weights = new[] { new { symbol = "EURUSD", weight = -0.10m, rawSecurityId = "EURUSD" } }
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiModelRun>())!;
    }

    private sealed record ApiSnapshot(decimal Bid, decimal Ask);
    private sealed record ApiBar(DateTimeOffset BarStartUtc, int ObservationCount);
    private sealed record ApiModelRun(string Id);
    private sealed record ApiProcessResult(bool Processed, string Status, string? BlockedReason, int TradeIntentCount, int OrderCount, int FillCount, bool IsAlreadyProcessed);
    private sealed record ApiReferenceDataIntegrityCheck(int BlockingIssueCount, int WarningIssueCount);
    private sealed record ApiOrdersResponse(List<ApiParentOrder> ParentOrders, List<ApiChildOrder> ChildOrders);
    private sealed record ApiParentOrder(string Id, string TradeIntentId, string? InstrumentId, string ClientOrderId);
    private sealed record ApiChildOrder(string Id, string ParentOrderId, string VenueId, string? InstrumentId, string ClientOrderId, string? BrokerOrderId);
    private sealed record ApiHealth(string ExecutionGateway, string MarketDataMode, bool LiveTradingEnabled, bool ExternalConnectionsEnabled);
    private sealed record ApiKillSwitch(bool IsActive);
    private sealed record ApiInstrument(string Symbol);
    private sealed record ApiVenue(string Name);
    private sealed record ApiTargetPosition(string ModelRunId);
    private sealed record ApiDriftSnapshot(string ModelRunId);
    private sealed record ApiRiskDecision(string Id);
    private sealed record ApiFill(string BrokerExecutionId);
    private sealed record ApiLmaxImportRun(string Id);
    private sealed record ApiLmaxIndividualTrade(string Id, string ExecutionId, string LmaxSymbol, string? InstrumentId, decimal TradeQuantity, decimal TradePrice);
    private sealed record ApiLmaxTradeSummary(string Id);
    private sealed record ApiLmaxCurrencyWallet(string Id, string Currency, decimal WalletBalance, decimal RateToBaseCcy, decimal WalletBalanceBaseUsd);
    private sealed record ApiEodPnlSummary(decimal TotalProfitLossUsd, decimal TotalCommissionUsd, decimal TotalDividendsUsd, decimal TotalFinancingUsd, decimal TotalNetPnlUsd);
    private sealed record ApiEodBreak(string Id);
}
