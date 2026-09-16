using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxDemoBatchExecutionTests
{
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

    private static (PlatformState State, BatchGateway Gateway, ProcessModelRunService Service, DateTimeOffset Start) Fixture()
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
        var repository = new InMemoryIntradayRepository(state);
        var gateway = new BatchGateway(state, clock);
        return (state, gateway, new ProcessModelRunService(repository, gateway, gateway, clock, new ReferenceDataIntegrityService(repository, clock)), at);
    }
    private sealed class MovingClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now;
    }
    private sealed class BatchGateway(PlatformState state, MovingClock clock) : ILmaxDemoBatchExecutionGateway, IBrokerPositionProvider
    {
        private readonly Dictionary<InstrumentId, decimal> positions = [];
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
                return new VenueExecutionResult([new ExecutionReport(ExecutionReportId.New(), x.ChildOrderId, x.VenueId,
                    "simulated-order-" + x.ChildOrderId.Value, "simulated-fill-" + x.ChildOrderId.Value, x.ClientOrderId,
                    ExecutionReportType.Fill, x.VenueQuantity, 1.1m, 0m, x.VenueQuantity, 1.1m, clock.UtcNow)]);
            }).ToArray());
        }
        public Task<IReadOnlyList<BrokerPositionSnapshot>> GetPositionsAsync(BrokerAccountId id, CancellationToken token)
            => Task.FromResult<IReadOnlyList<BrokerPositionSnapshot>>(positions.Select(x => new BrokerPositionSnapshot(id, x.Key, x.Value, clock.UtcNow)).ToArray());
        public Task<IReadOnlyList<VenueOpenOrder>> GetOpenOrdersAsync(VenueId id, CancellationToken token) => Task.FromResult<IReadOnlyList<VenueOpenOrder>>([]);
        public Task<VenueExecutionResult> SendOrderAsync(VenueOrderRequest r, CancellationToken t) => throw new InvalidOperationException("BATCH_SEAM_REQUIRED");
        public Task<VenueExecutionResult> CancelOrderAsync(VenueCancelRequest r, CancellationToken t) => throw new NotSupportedException();
    }
}
