using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Infrastructure.Lmax;
using QQ.Production.Intraday.Infrastructure.Simulator;

namespace QQ.Production.Intraday.Tests.Integration;

public sealed class WorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 04, 29, 12, 00, 00, TimeSpan.Zero);
    private static readonly FixedClock Clock = new(Now);

    [Fact]
    public async Task ProcessModelRunService_processes_sample_model_run_end_to_end()
    {
        var state = SeedData.Create(Now);
        var service = CreateService(state, FakeLmaxBehavior.FullFill);

        var result = await service.ProcessNextAsync();

        Assert.True(result.Processed);
        Assert.Single(state.TargetPositions);
        Assert.Single(state.DriftSnapshots);
        Assert.Single(state.TradeIntents);
        Assert.Single(state.RiskDecisions);
        Assert.Single(state.ParentOrders);
        Assert.Single(state.ChildOrders);
        Assert.Single(state.Fills);
        Assert.Equal(OrderStatus.Filled, state.ChildOrders.Single().Status);
        Assert.Equal(OrderStatus.Filled, state.ParentOrders.Single().Status);
        Assert.Equal(-91_000m, state.PositionLedger.Sum(x => x.BaseQuantityDelta));
        Assert.Contains(state.ReconciliationRuns, x => x.Phase == ReconciliationPhase.PreTrade && !x.HasBlockingBreaks);
        Assert.Contains(state.ReconciliationRuns, x => x.Phase == ReconciliationPhase.PostTrade && !x.HasBlockingBreaks);
        Assert.True(state.ModelRuns.Single().IsProcessed);
    }

    [Fact]
    public async Task ProcessModelRunService_blocks_when_positions_mismatch_before_trade()
    {
        var state = SeedData.Create(Now);
        var broker = new FakeBrokerPositionProvider(state, Clock) { ForceMismatch = true };
        var service = CreateService(state, FakeLmaxBehavior.FullFill, broker);

        var result = await service.ProcessNextAsync();

        Assert.True(result.Blocked);
        Assert.Empty(state.TradeIntents);
        Assert.Empty(state.ParentOrders);
        var breakItem = Assert.Single(state.ReconciliationBreaks);
        Assert.Equal(ReconciliationBreakType.InternalBrokerPositionMismatch, breakItem.Type);
        Assert.Equal(ReconciliationBreakSeverity.Blocking, breakItem.Severity);
    }

    [Fact]
    public async Task Post_trade_position_mismatch_creates_blocking_break_without_corrective_trade()
    {
        var state = SeedData.Create(Now);
        var broker = new FakeBrokerPositionProvider(state, Clock) { ForcePostTradeMismatch = true };
        var service = CreateService(state, FakeLmaxBehavior.FullFill, broker);

        await service.ProcessNextAsync();

        Assert.Single(state.TradeIntents);
        Assert.Single(state.ParentOrders);
        Assert.Contains(state.ReconciliationRuns, x => x.Phase == ReconciliationPhase.PostTrade && x.HasBlockingBreaks);
        Assert.Contains(state.ReconciliationBreaks, x => x.Type == ReconciliationBreakType.InternalBrokerPositionMismatch && x.Severity == ReconciliationBreakSeverity.Blocking);
    }

    [Fact]
    public async Task ProcessModelRunService_is_idempotent_for_processed_run()
    {
        var state = SeedData.Create(Now);
        var service = CreateService(state, FakeLmaxBehavior.FullFill);

        await service.ProcessNextAsync();
        await service.ProcessAsync(state.ModelRuns.Single().Id);

        Assert.Single(state.TargetPositions);
        Assert.Single(state.DriftSnapshots);
        Assert.Single(state.TradeIntents);
        Assert.Single(state.RiskDecisions);
        Assert.Single(state.ParentOrders);
        Assert.Equal(2, state.ExecutionReports.Count);
        Assert.Single(state.Fills);
        Assert.Single(state.PositionLedger, x => x.Type == PositionLedgerEventType.Fill);
    }

    [Fact]
    public async Task Duplicate_model_run_id_does_not_create_duplicate_run_or_weights()
    {
        var state = SeedData.Create(Now);
        var repository = new InMemoryIntradayRepository(state);
        var run = state.ModelRuns.Single();

        await repository.AddModelRunAsync(run, state.TargetWeights.ToList(), CancellationToken.None);

        Assert.Single(state.ModelRuns);
        Assert.Single(state.TargetWeights);
    }

    [Fact]
    public async Task Duplicate_broker_execution_id_is_rejected_safely_and_does_not_update_ledger()
    {
        var state = SeedData.Create(Now);
        var repository = new InMemoryIntradayRepository(state);
        var fill = new Fill(FillId.New(), "DUP-1", ChildOrderId.New(), state.Instruments.Single().Id, state.Venues.Single().Id, TradeSide.Buy, 10_000m, 1m, 1.1m, Now, Now);

        Assert.True(await repository.TryAddFillAsync(fill, CancellationToken.None));
        await repository.AddPositionLedgerEventAsync(new PositionLedgerEvent(Guid.NewGuid(), state.Funds.Single().Id, state.Instruments.Single().Id, PositionLedgerEventType.Fill, fill.BaseQuantity, fill.BrokerExecutionId, Now), CancellationToken.None);
        Assert.False(await repository.TryAddFillAsync(fill with { Id = FillId.New() }, CancellationToken.None));

        Assert.Single(state.Fills);
        Assert.Single(state.PositionLedger, x => x.Type == PositionLedgerEventType.Fill);
        Assert.Equal(10_000m, state.PositionLedger.Sum(x => x.BaseQuantityDelta));
    }

    [Fact]
    public async Task Risk_rejection_prevents_order_creation()
    {
        var state = SeedData.Create(Now);
        state.KillSwitch = state.KillSwitch with { IsActive = true };
        var service = CreateService(state, FakeLmaxBehavior.FullFill);

        var result = await service.ProcessNextAsync();

        Assert.True(result.Blocked);
        Assert.Equal(ProcessModelRunStatus.Blocked, result.Status);
        Assert.Equal(ProcessModelRunBlockedReason.KillSwitchActive, result.BlockedReason);
        Assert.Single(state.TradeIntents);
        Assert.Single(state.RiskDecisions);
        Assert.Empty(state.ParentOrders);
        Assert.Empty(state.Fills);
    }

    [Fact]
    public async Task Stale_market_data_blocks_without_throwing_or_creating_orders()
    {
        var state = SeedData.Create(Now);
        state.MarketData[0] = state.MarketData[0] with { ReceivedAtUtc = Now.AddHours(-1) };
        var service = CreateService(state, FakeLmaxBehavior.FullFill);

        var result = await service.ProcessNextAsync();

        Assert.True(result.Blocked);
        Assert.Equal(ProcessModelRunBlockedReason.StaleMarketData, result.BlockedReason);
        Assert.Single(state.TradeIntents);
        Assert.Single(state.RiskDecisions);
        Assert.Empty(state.ParentOrders);
        Assert.Empty(state.Fills);
        Assert.False(state.ModelRuns.Single().IsProcessed);
    }

    [Fact]
    public async Task Missing_market_data_blocks_without_throwing()
    {
        var state = SeedData.Create(Now);
        state.MarketData.Clear();
        var service = CreateService(state, FakeLmaxBehavior.FullFill);

        var result = await service.ProcessNextAsync();

        Assert.True(result.Blocked);
        Assert.Equal(ProcessModelRunBlockedReason.NoMarketData, result.BlockedReason);
        Assert.Empty(state.TradeIntents);
        Assert.Empty(state.ParentOrders);
        Assert.False(state.ModelRuns.Single().IsProcessed);
    }

    [Fact]
    public async Task Missing_trading_window_blocks_without_throwing()
    {
        var state = SeedData.Create(Now);
        state.TradingWindows.Clear();
        var service = CreateService(state, FakeLmaxBehavior.FullFill);

        var result = await service.ProcessNextAsync();

        Assert.True(result.Blocked);
        Assert.Equal(ProcessModelRunBlockedReason.TradingWindowClosed, result.BlockedReason);
        Assert.Single(state.TradeIntents);
        Assert.Single(state.RiskDecisions);
        Assert.Empty(state.ParentOrders);
    }

    [Fact]
    public async Task Blocked_risk_retry_is_idempotent()
    {
        var state = SeedData.Create(Now);
        state.KillSwitch = state.KillSwitch with { IsActive = true };
        var service = CreateService(state, FakeLmaxBehavior.FullFill);

        await service.ProcessNextAsync();
        var retry = await service.ProcessNextAsync();

        Assert.True(retry.Blocked);
        Assert.Single(state.TradeIntents);
        Assert.Single(state.RiskDecisions);
        Assert.Empty(state.ParentOrders);
    }

    [Theory]
    [InlineData(FakeLmaxBehavior.FullFill, OrderStatus.Filled, OrderStatus.Filled, 1, -91_000)]
    [InlineData(FakeLmaxBehavior.PartialFill, OrderStatus.Expired, OrderStatus.PartiallyFilled, 1, -46_000)]
    [InlineData(FakeLmaxBehavior.Reject, OrderStatus.Rejected, OrderStatus.Rejected, 0, 0)]
    [InlineData(FakeLmaxBehavior.NoFill, OrderStatus.Expired, OrderStatus.Expired, 0, 0)]
    public async Task Ioc_execution_behaviors_update_orders_fills_and_positions(FakeLmaxBehavior behavior, OrderStatus expectedChild, OrderStatus expectedParent, int expectedFills, decimal expectedPositionDelta)
    {
        var state = SeedData.Create(Now);
        var service = CreateService(state, behavior);

        await service.ProcessNextAsync();

        Assert.Equal(expectedChild, state.ChildOrders.Single().Status);
        Assert.Equal(expectedParent, state.ParentOrders.Single().Status);
        Assert.Equal(expectedFills, state.Fills.Count);
        Assert.Equal(expectedPositionDelta, state.PositionLedger.Where(x => x.Type == PositionLedgerEventType.Fill).Sum(x => x.BaseQuantityDelta));
    }

    [Fact]
    public async Task Buy_fill_updates_position_positive_and_sell_fill_updates_position_negative()
    {
        var buyState = SeedData.Create(Now);
        buyState.TargetWeights[0] = buyState.TargetWeights[0] with { Weight = 0.10m };
        await CreateService(buyState, FakeLmaxBehavior.FullFill).ProcessNextAsync();

        var sellState = SeedData.Create(Now);
        await CreateService(sellState, FakeLmaxBehavior.FullFill).ProcessNextAsync();

        Assert.Equal(91_000m, buyState.PositionLedger.Where(x => x.Type == PositionLedgerEventType.Fill).Sum(x => x.BaseQuantityDelta));
        Assert.Equal(-91_000m, sellState.PositionLedger.Where(x => x.Type == PositionLedgerEventType.Fill).Sum(x => x.BaseQuantityDelta));
    }

    [Fact]
    public void Api_and_worker_register_fake_lmax_only()
    {
        var root = FindRepoRoot();
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs"));

        Assert.Contains("AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>", apiProgram);
        Assert.Contains("AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>", workerProgram);
        Assert.DoesNotContain(nameof(LmaxVenueGateway), apiProgram);
        Assert.DoesNotContain(nameof(LmaxVenueGateway), workerProgram);
    }

    private static ProcessModelRunService CreateService(PlatformState state, FakeLmaxBehavior behavior, IBrokerPositionProvider? brokerProvider = null)
    {
        var repository = new InMemoryIntradayRepository(state);
        var gateway = new FakeLmaxGateway(new FakeLmaxOptions { Behavior = behavior }, Clock);
        return new ProcessModelRunService(repository, gateway, brokerProvider ?? new FakeBrokerPositionProvider(state, Clock), Clock);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
