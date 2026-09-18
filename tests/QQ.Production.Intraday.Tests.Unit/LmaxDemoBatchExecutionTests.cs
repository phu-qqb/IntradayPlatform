using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxDemoBatchExecutionTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(14)]
    public async Task DetachedRepositorySnapshotProducesDistinctPersistedOrdersForEveryPair(int pairCount)
    {
        var f = Fixture(detachedSnapshots: true);
        var symbols = LmaxDemoUsdExecutionUniverse.Symbols.Except(f.State.Instruments.Select(x => x.Symbol)).Take(pairCount - 2);
        foreach (var symbol in symbols)
        {
            var instrument = f.State.Instruments[0] with { Id = new InstrumentId(Guid.NewGuid()), Symbol = symbol,
                BaseCurrency = new Currency(symbol[..3]), QuoteCurrency = new Currency(symbol[3..]) };
            f.State.Instruments.Add(instrument);
            f.State.VenueInstrumentMappings.Add(f.State.VenueInstrumentMappings[0] with { Id = new VenueInstrumentId(Guid.NewGuid()), InstrumentId = instrument.Id, VenueSymbol = symbol, VenueInstrumentCode = symbol });
            f.State.InstrumentAliases.Add(f.State.InstrumentAliases[0] with { Id = new InstrumentAliasId(Guid.NewGuid()), InstrumentId = instrument.Id, ExternalSymbol = symbol, ExternalInstrumentId = symbol });
            f.State.InstrumentRiskLimits.Add(f.State.InstrumentRiskLimits[0] with { Id = Guid.NewGuid(), InstrumentId = instrument.Id });
            f.State.MarketData.Add(f.State.MarketData[0] with { Id = MarketDataSnapshotId.New(), InstrumentId = instrument.Id });
            f.State.TargetWeights.Add(f.State.TargetWeights[0] with { InstrumentId = instrument.Id });
        }
        f.Gateway.ExecutionScope = f.State.Instruments.Select(x => x.Id).ToArray();

        var result = await f.Service.ProcessAsync(f.State.ModelRuns.Single().Id);

        Assert.True(result.Processed, result.Message);
        Assert.Equal(1, f.Gateway.BatchCalls);
        Assert.Equal(pairCount, f.State.ParentOrders.Count);
        Assert.Equal(pairCount, f.State.ChildOrders.Count);
        Assert.Equal(pairCount, f.State.ParentOrders.Select(x => x.ClientOrderId).Distinct().Count());
        Assert.Equal(pairCount, f.State.ChildOrders.Select(x => x.ClientOrderId).Distinct().Count());
        Assert.Equal(pairCount, f.State.Fills.Count);
        Assert.DoesNotContain(f.State.ReconciliationRuns, x => x.HasBlockingBreaks);
    }

    [Fact]
    public async Task BatchPreparesAllRiskDecisionsAndReconcilesFillsReceivedAfterCycleStart()
    {
        var f = Fixture();
        var result = await f.Service.ProcessAsync(f.State.ModelRuns.Single().Id);
        Assert.True(result.Processed, result.Message);
        Assert.Equal(1, f.Gateway.BatchCalls);
        Assert.Equal(2, f.Gateway.PreparedRiskCount);
        Assert.Equal(2, f.State.Fills.Count);
        Assert.All(f.State.Fills, x => Assert.True(x.ReceivedAtUtc > f.Start));
        Assert.DoesNotContain(f.State.ReconciliationRuns, x => x.HasBlockingBreaks);
    }

    [Fact]
    public async Task SecondInstrumentRiskRejection_PreventsEveryBrokerSend()
    {
        var f = Fixture();
        f.State.InstrumentRiskLimits[1] = f.State.InstrumentRiskLimits[1] with { MaxTradeNotionalUsd = 1m };
        var result = await f.Service.ProcessAsync(f.State.ModelRuns.Single().Id);
        Assert.True(result.Blocked);
        Assert.Equal(0, f.Gateway.BatchCalls);
        Assert.Empty(f.State.Fills);
        Assert.Equal(2, f.State.RiskDecisions.Count);
    }

    [Fact]
    public async Task ObservedScopeExecutesOnlyEurUsdAndRetainsTheFullNaturalPortfolio()
    {
        var f = Fixture();
        var eurusd = f.State.Instruments.Single(x => x.Symbol == "EURUSD").Id;
        f.Gateway.ExecutionScope = [eurusd];
        f.State.VenueInstrumentMappings[1] = f.State.VenueInstrumentMappings[1] with { IsEnabled = false };
        f.State.MarketData.RemoveAll(x => x.InstrumentId != eurusd);
        var weights = f.State.TargetWeights.ToArray();
        var limits = f.State.RiskLimitSets.ToArray();
        var nav = f.State.ModelRuns.Single().NavUsd;

        var result = await f.Service.ProcessAsync(f.State.ModelRuns.Single().Id);

        Assert.True(result.Processed, result.Message);
        Assert.Equal(weights, f.State.TargetWeights);
        Assert.Equal(limits, f.State.RiskLimitSets);
        Assert.Equal(nav, f.State.ModelRuns.Single().NavUsd);
        Assert.Equal(eurusd, Assert.Single(f.State.TargetPositions).InstrumentId);
        Assert.Equal(eurusd, Assert.Single(f.State.RiskDecisions).InstrumentId);
        Assert.Equal(eurusd, Assert.Single(f.State.Fills).InstrumentId);
        Assert.Equal(1, f.Gateway.BatchCalls);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    public async Task InvalidObservedScopeBlocksBeforeRiskAndSend(string kind)
    {
        var f = Fixture();
        var id = f.State.Instruments[0].Id;
        f.Gateway.ExecutionScope = kind switch
        {
            "empty" => [],
            "duplicate" => [id, id],
            _ => [new InstrumentId(Guid.NewGuid())]
        };
        var result = await f.Service.ProcessAsync(f.State.ModelRuns.Single().Id);
        Assert.True(result.Blocked);
        Assert.Equal(ProcessModelRunBlockedReason.ReferenceDataInvalid, result.BlockedReason);
        Assert.Empty(f.State.RiskDecisions);
        Assert.Equal(0, f.Gateway.BatchCalls);
    }

    [Fact]
    public async Task MissingObservedTargetIsNotInventedAsAZeroPosition()
    {
        var f = Fixture();
        f.Gateway.ExecutionScope = [f.State.Instruments[0].Id];
        f.State.TargetWeights.RemoveAt(0);
        var result = await f.Service.ProcessAsync(f.State.ModelRuns.Single().Id);
        Assert.True(result.Blocked);
        Assert.Equal(ProcessModelRunBlockedReason.NoTargetWeights, result.BlockedReason);
        Assert.Empty(f.State.TargetPositions);
        Assert.Equal(0, f.Gateway.BatchCalls);
    }

    [Fact]
    public async Task ExistingPositionOutsideObservedScopeCannotDisappearFromReconciliation()
    {
        var f = Fixture();
        f.Gateway.ExecutionScope = [f.State.Instruments[0].Id];
        f.Gateway.SetBrokerPosition(f.State.Instruments[1].Id, 1000m);
        var result = await f.Service.ProcessAsync(f.State.ModelRuns.Single().Id);
        Assert.True(result.Blocked);
        Assert.Equal(ProcessModelRunBlockedReason.PositionMismatch, result.BlockedReason);
        Assert.Empty(f.State.RiskDecisions);
        Assert.Equal(0, f.Gateway.BatchCalls);
    }

    [Fact]
    public async Task DisabledMappingInsideObservedScopeStillBlocks()
    {
        var f = Fixture();
        f.Gateway.ExecutionScope = [f.State.Instruments[0].Id];
        f.State.VenueInstrumentMappings[0] = f.State.VenueInstrumentMappings[0] with { IsEnabled = false };
        var result = await f.Service.ProcessAsync(f.State.ModelRuns.Single().Id);
        Assert.True(result.Blocked);
        Assert.Empty(f.State.TargetPositions);
        Assert.Equal(0, f.Gateway.BatchCalls);
    }

    [Fact]
    public async Task NettedCurrencyCannotBeDroppedByOldEurUsdExecutionMask()
    {
        var f = Fixture();
        f.State.ModelRuns[0] = f.State.ModelRuns[0] with { SourceFileName = "legacy-anubis:" + LmaxDemoUsdNetting.BatchPrefix + "test" };
        f.Gateway.ExecutionScope = [f.State.Instruments[0].Id];
        var result = await f.Service.ProcessAsync(f.State.ModelRuns[0].Id);
        Assert.True(result.Blocked);
        Assert.Contains("cannot be discarded", result.Message);
        Assert.Equal(0, f.Gateway.BatchCalls);
        Assert.Empty(f.State.TargetPositions);
    }

    [Fact]
    public async Task NettedUsdJpyIsSizedAndRiskCheckedInUsdThroughoutExistingBatchPipeline()
    {
        var f = Fixture();
        f.State.ModelRuns[0] = f.State.ModelRuns[0] with { SourceFileName = "legacy-anubis:" + LmaxDemoUsdNetting.BatchPrefix + "test" };
        var id = f.State.Instruments[1].Id;
        f.State.Instruments[1] = f.State.Instruments[1] with { Symbol = "USDJPY", BaseCurrency = Currency.Usd, QuoteCurrency = new("JPY") };
        f.State.VenueInstrumentMappings[1] = f.State.VenueInstrumentMappings[1] with { VenueSymbol = "USDJPY", VenueInstrumentCode = "USD/JPY" };
        f.State.InstrumentAliases[1] = f.State.InstrumentAliases[1] with { ExternalSymbol = "USD/JPY", ExternalInstrumentId = "4004" };
        f.State.MarketData[1] = f.State.MarketData[1] with { Bid = 149.99m, Ask = 150.01m, ExplicitMid = null };
        f.State.TargetWeights[1] = f.State.TargetWeights[1] with { Weight = -.01m, RawSecurityId = "USDJPY Curncy" };
        var result = await f.Service.ProcessAsync(f.State.ModelRuns[0].Id);
        Assert.True(result.Processed, result.Message);
        Assert.Equal(-10_000m, f.State.TargetPositions.Single(x => x.InstrumentId == id).TargetBaseQuantity);
        Assert.Equal(10_000m, f.State.TradeIntents.Single(x => x.InstrumentId == id).RequestedBaseQuantity);
        Assert.DoesNotContain(f.State.ReconciliationRuns, x => x.HasBlockingBreaks);
    }

    private static (PlatformState State, BatchGateway Gateway, ProcessModelRunService Service, DateTimeOffset Start) Fixture(bool detachedSnapshots = false)
    {
        var at = new DateTimeOffset(2026, 9, 16, 13, 0, 0, TimeSpan.Zero);
        var clock = new MovingClock(at);
        var state = SeedData.Create(at);
        state.TargetWeights[0] = state.TargetWeights[0] with { Weight = .01m };
        var instrument = state.Instruments.Single() with { Id = new InstrumentId(Guid.NewGuid()), Symbol = "GBPUSD", BaseCurrency = new Currency("GBP") };
        state.Instruments.Add(instrument);
        state.VenueInstrumentMappings.Add(state.VenueInstrumentMappings[0] with { Id = new VenueInstrumentId(Guid.NewGuid()), InstrumentId = instrument.Id, VenueSymbol = "GBPUSD", VenueInstrumentCode = "GBP/USD" });
        state.InstrumentAliases.Add(state.InstrumentAliases[0] with { Id = new InstrumentAliasId(Guid.NewGuid()), InstrumentId = instrument.Id, ExternalSymbol = "GBP/USD", ExternalInstrumentId = "4002" });
        state.InstrumentRiskLimits.Add(state.InstrumentRiskLimits[0] with { Id = Guid.NewGuid(), InstrumentId = instrument.Id });
        state.MarketData.Add(state.MarketData[0] with { Id = MarketDataSnapshotId.New(), InstrumentId = instrument.Id });
        state.TargetWeights.Add(state.TargetWeights[0] with { InstrumentId = instrument.Id });
        IIntradayRepository repository = detachedSnapshots ? new DetachedSnapshotRepository(state) : new InMemoryIntradayRepository(state);
        var gateway = new BatchGateway(state, clock);
        return (state, gateway, new ProcessModelRunService(repository, gateway, gateway, clock, new ReferenceDataIntegrityService(repository, clock)), at);
    }
    private sealed class MovingClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now;
    }

    // SQL loads detached lists and enforces unique client IDs. The usual in-memory
    // fixture shares its live lists, which hid the multi-pair collision on 18 Sep.
    private sealed class DetachedSnapshotRepository(PlatformState state) : IIntradayRepository, ILmaxDemoExecutionRepository
    {
        private readonly InMemoryIntradayRepository inner = new(state);
        public Task<PlatformState> LoadStateAsync(CancellationToken token)
        {
            var snapshot = new PlatformState { KillSwitch = state.KillSwitch };
            foreach (var property in typeof(PlatformState).GetProperties())
                if (property.GetValue(state) is System.Collections.IList source && property.GetValue(snapshot) is System.Collections.IList target)
                    foreach (var item in source) target.Add(item);
            return Task.FromResult(snapshot);
        }
        public Task AddOrdersAsync(ParentOrder parent, ChildOrder child, CancellationToken token)
        {
            if (state.ParentOrders.Any(x => x.ClientOrderId == parent.ClientOrderId) || state.ChildOrders.Any(x => x.ClientOrderId == child.ClientOrderId))
                throw new InvalidOperationException("TEST_SQL_UNIQUE_CLIENT_ORDER_ID_VIOLATION");
            return inner.AddOrdersAsync(parent, child, token);
        }
        public Task<ModelRun?> GetNextUnprocessedModelRunAsync(CancellationToken t) => inner.GetNextUnprocessedModelRunAsync(t);
        public Task<ModelRun?> GetModelRunAsync(ModelRunId id, CancellationToken t) => inner.GetModelRunAsync(id, t);
        public Task AddModelRunAsync(ModelRun r, IReadOnlyList<TargetWeight> w, CancellationToken t) => inner.AddModelRunAsync(r, w, t);
        public Task MarkModelRunProcessedAsync(ModelRunId id, ModelRunStatus s, CancellationToken t) => inner.MarkModelRunProcessedAsync(id, s, t);
        public Task SaveReconciliationAsync(ReconciliationRun r, IReadOnlyList<ReconciliationBreak> b, CancellationToken t) => inner.SaveReconciliationAsync(r, b, t);
        public Task SaveTargetAndDriftAsync(TargetPosition p, DriftSnapshot d, CancellationToken t) => inner.SaveTargetAndDriftAsync(p, d, t);
        public Task AddTradeIntentAsync(TradeIntent i, CancellationToken t) => inner.AddTradeIntentAsync(i, t);
        public Task AddRiskDecisionAsync(RiskDecision d, IReadOnlyList<RiskDecisionDetail>? a, CancellationToken t) => inner.AddRiskDecisionAsync(d, a, t);
        public Task AddExecutionReportAsync(ExecutionReport r, CancellationToken t) => inner.AddExecutionReportAsync(r, t);
        public Task<bool> TryAddFillAsync(Fill f, CancellationToken t) => inner.TryAddFillAsync(f, t);
        public Task AddPositionLedgerEventAsync(PositionLedgerEvent e, CancellationToken t) => inner.AddPositionLedgerEventAsync(e, t);
        public Task PersistDemoParentAsync(LmaxDemoExecutionPersistence e, CancellationToken t) => inner.PersistDemoParentAsync(e, t);
        public Task SetKillSwitchAsync(bool a, string? r, CancellationToken t) => inner.SetKillSwitchAsync(a, r, t);
        public Task UpsertRiskLimitSetAsync(RiskLimitSet s, CancellationToken t) => inner.UpsertRiskLimitSetAsync(s, t);
        public Task UpsertRiskLimitAsync(RiskLimit l, CancellationToken t) => inner.UpsertRiskLimitAsync(l, t);
        public Task UpsertInstrumentRiskLimitAsync(InstrumentRiskLimit l, CancellationToken t) => inner.UpsertInstrumentRiskLimitAsync(l, t);
        public Task UpsertVenueRiskLimitAsync(VenueRiskLimit l, CancellationToken t) => inner.UpsertVenueRiskLimitAsync(l, t);
        public Task UpsertTradingWindowAsync(TradingWindow w, CancellationToken t) => inner.UpsertTradingWindowAsync(w, t);
        public Task UpsertInstrumentAsync(Instrument i, CancellationToken t) => inner.UpsertInstrumentAsync(i, t);
        public Task UpsertVenueAsync(Venue v, CancellationToken t) => inner.UpsertVenueAsync(v, t);
    }
    private sealed class BatchGateway(PlatformState state, MovingClock clock) : ILmaxDemoBatchExecutionGateway, IBrokerPositionProvider
    {
        private readonly Dictionary<InstrumentId, decimal> positions = [];
        public IReadOnlyList<InstrumentId> ExecutionScope { get; set; } = state.Instruments.Select(x => x.Id).ToArray();
        public Task<IReadOnlyList<InstrumentId>> GetExecutionScopeAsync(CancellationToken token)
            => Task.FromResult(ExecutionScope);
        public void SetBrokerPosition(InstrumentId id, decimal quantity) => positions[id] = quantity;
        public int BatchCalls { get; private set; }
        public int PreparedRiskCount { get; private set; }
        public Task<IReadOnlyList<VenueExecutionResult>> SendModelRunAsync(ModelRun run, IReadOnlyList<TargetPosition> targets,
            IReadOnlyList<VenueOrderRequest> orders, CancellationToken token)
        {
            BatchCalls++;
            PreparedRiskCount = state.RiskDecisions.Count;
            clock.UtcNow = clock.UtcNow.AddMinutes(2);
            return Task.FromResult<IReadOnlyList<VenueExecutionResult>>(orders.Select(x =>
            {
                positions[x.InstrumentId] = (x.Side == OrderSide.Buy ? 1m : -1m) * x.BaseQuantity;
                var reports = new[] { new ExecutionReport(ExecutionReportId.New(), x.ChildOrderId, x.VenueId,
                    "simulated-order-" + x.ChildOrderId.Value, "simulated-fill-" + x.ChildOrderId.Value, x.ClientOrderId,
                    ExecutionReportType.Fill, x.VenueQuantity, 1.1m, 0m, x.VenueQuantity, 1.1m, clock.UtcNow) };
                var child = state.ChildOrders.Single(c => c.Id == x.ChildOrderId);
                return new VenueExecutionResult(reports) { DemoPersistence = new LmaxDemoExecutionPersistence(
                    "1754288005", state.BrokerAccounts.Single().AccountCode, "simulated-batch", child.ParentOrderId, child.Id,
                    [child with { Status = OrderStatus.Filled }], reports, OrderStatus.Filled) };
            }).ToArray());
        }
        public Task<IReadOnlyList<BrokerPositionSnapshot>> GetPositionsAsync(BrokerAccountId id, CancellationToken token)
            => Task.FromResult<IReadOnlyList<BrokerPositionSnapshot>>(positions.Select(x => new BrokerPositionSnapshot(id, x.Key, x.Value, clock.UtcNow)).ToArray());
        public Task<IReadOnlyList<VenueOpenOrder>> GetOpenOrdersAsync(VenueId id, CancellationToken token) => Task.FromResult<IReadOnlyList<VenueOpenOrder>>([]);
        public Task<VenueExecutionResult> SendOrderAsync(VenueOrderRequest r, CancellationToken t) => throw new InvalidOperationException("BATCH_SEAM_REQUIRED");
        public Task<VenueExecutionResult> CancelOrderAsync(VenueCancelRequest r, CancellationToken t) => throw new NotSupportedException();
    }
}
