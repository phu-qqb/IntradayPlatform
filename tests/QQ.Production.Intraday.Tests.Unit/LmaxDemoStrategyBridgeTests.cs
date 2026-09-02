using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Infrastructure.Simulator;
using QQ.Production.Intraday.Lmax.ConnectivityLab;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxDemoStrategyBridgeTests
{
    [Fact]
    public void Bridge_ImplementsExistingVenueExecutionSeam()
        => Assert.True(typeof(IVenueExecutionGateway).IsAssignableFrom(typeof(LmaxDemoStrategyVenueExecutionGateway)));

    [Fact]
    public void Production_IsRejectedBeforeAnyExecution()
    {
        var options = Options().withEnvironment("Production");
        Assert.Throws<InvalidOperationException>(() => LmaxDemoStrategyVenueExecutionGateway.EnsureDemoOnly(options));
    }

    [Fact]
    public void Policy_PreservesPassiveRepriceResidualProgression()
    {
        var close = new DateTimeOffset(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);
        Assert.Equal(LmaxDemoStrategyPhase.PassivePosted, LmaxDemoStrategyPolicy.Phase(close.AddMinutes(-10), close));
        Assert.Equal(LmaxDemoStrategyPhase.PassiveReprice, LmaxDemoStrategyPolicy.Phase(close.AddMinutes(-4), close));
        Assert.Equal(LmaxDemoStrategyPhase.AggressiveResidual, LmaxDemoStrategyPolicy.Phase(close.AddSeconds(-30), close));
    }

    [Fact]
    public async Task RealQubesTarget_ReachesQualifiedSessionWithoutEconomicMutation()
    {
        var fixture = Fixture();
        var session = new CapturingSession();
        var gateway = new LmaxDemoStrategyVenueExecutionGateway(fixture.Repository, fixture.Options, session, fixture.Clock);

        await gateway.SendOrderAsync(fixture.Request, CancellationToken.None);

        Assert.Equal(1, session.SendCount);
        Assert.NotNull(session.Request);
        Assert.Equal(fixture.Request.VenueQuantity, session.Request!.VenueQuantity);
        Assert.Equal(fixture.Run.ReceivedAtUtc, session.Request.TargetKnownAtUtc);
        Assert.Equal(fixture.Run.EffectiveAtUtc, session.Request.TargetCloseUtc);
    }

    [Fact]
    public async Task DuplicateEconomicParent_IsBlockedBeforeSecondVenueSend()
    {
        var fixture = Fixture();
        fixture.State.TradeIntents.Add(new TradeIntent(
            TradeIntentId.New(), fixture.Run.Id, fixture.Fund.Id, fixture.Instrument.Id,
            TradeSide.Buy, fixture.Request.BaseQuantity, fixture.Request.VenueQuantity,
            "duplicate retry", TradeIntentStatus.Ordered, fixture.Clock.UtcNow));
        var duplicateIntent = fixture.State.TradeIntents[^1];
        fixture.State.ParentOrders.Add(new ParentOrder(
            ParentOrderId.New(), duplicateIntent.Id, new ClientOrderId("DUPLICATE"), OrderSide.Buy,
            fixture.Request.BaseQuantity, ExecutionAlgo.CloseSeeking15m, OrderStatus.Created, fixture.Clock.UtcNow));
        var session = new CapturingSession();
        var gateway = new LmaxDemoStrategyVenueExecutionGateway(fixture.Repository, fixture.Options, session, fixture.Clock);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.SendOrderAsync(fixture.Request, CancellationToken.None));

        Assert.Equal("DEMO_STRATEGY_DUPLICATE_PARENT_RETRY_BLOCKED", ex.Message);
        Assert.Equal(0, session.SendCount);
    }

    [Fact]
    public async Task FreshOperatorAttestation_ProvidesOnlyTheObservedFlatPreRunState()
    {
        var fixture = Fixture();
        var provider = new LmaxDemoOperatorBrokerStateAttestationProvider(fixture.Repository, fixture.Options, fixture.Clock);

        var positions = await provider.GetPositionsAsync(fixture.State.BrokerAccounts.Single().Id, CancellationToken.None);

        Assert.Empty(positions);
    }

    [Fact]
    public async Task NormalizedFixFillEvidence_ProjectsTheControlledSessionPosition()
    {
        var fixture = Fixture();
        var fillTime = fixture.Clock.UtcNow;
        const string executionId = "FIX-EXEC-1";
        fixture.State.Fills.Add(new Fill(
            FillId.New(), executionId, fixture.State.ChildOrders.Single().Id,
            fixture.Instrument.Id, fixture.State.Venues.Single().Id, TradeSide.Sell,
            1_000m, 0.1m, 1.1m, fillTime, fillTime));
        fixture.State.PositionLedger.Add(new PositionLedgerEvent(
            Guid.NewGuid(), fixture.Fund.Id, fixture.Instrument.Id,
            PositionLedgerEventType.Fill, -1_000m, executionId, fillTime));
        var provider = new LmaxDemoOperatorBrokerStateAttestationProvider(fixture.Repository, fixture.Options, fixture.Clock);

        var positions = await provider.GetPositionsAsync(fixture.State.BrokerAccounts.Single().Id, CancellationToken.None);

        var position = Assert.Single(positions);
        Assert.Equal(fixture.Instrument.Id, position.InstrumentId);
        Assert.Equal(-1_000m, position.BaseQuantity);
    }

    [Fact]
    public async Task GenuineZeroCloseTarget_CanExecuteANonZeroFlattenDrift()
    {
        var fixture = Fixture();
        var target = fixture.State.TargetPositions.Single(x => x.ModelRunId == fixture.Run.Id && x.InstrumentId == fixture.Instrument.Id);
        fixture.State.TargetPositions.Remove(target);
        fixture.State.TargetPositions.Add(target with { TargetBaseQuantity = 0m, TargetVenueQuantity = 0m });
        var session = new CapturingSession();
        var gateway = new LmaxDemoStrategyVenueExecutionGateway(fixture.Repository, fixture.Options, session, fixture.Clock);

        await gateway.SendOrderAsync(fixture.Request, CancellationToken.None);

        Assert.Equal(1, session.SendCount);
        Assert.Equal(fixture.Request.VenueQuantity, session.Request!.VenueQuantity);
    }

    [Theory]
    [InlineData("approval", "", true, true, "DEMO_STRATEGY_ATTESTATION_APPROVAL_REQUIRED")]
    [InlineData("observed", "2026-08-31T08:00:00Z", true, true, "DEMO_STRATEGY_ATTESTATION_STALE")]
    [InlineData("flat", null, false, true, "DEMO_STRATEGY_ATTESTATION_FLAT_REQUIRED")]
    [InlineData("orders", null, true, false, "DEMO_STRATEGY_ATTESTATION_NO_WORKING_ORDERS_REQUIRED")]
    public async Task OperatorAttestation_FailsClosedWhenRequiredEvidenceIsInvalid(string field, string? value, bool flat, bool noWorkingOrders, string expected)
    {
        var fixture = Fixture();
        fixture.Options.DemoBrokerStateAttestationFlat = flat;
        fixture.Options.DemoBrokerStateAttestationNoWorkingOrders = noWorkingOrders;
        if (field == "approval") fixture.Options.DemoBrokerStateAttestationApprovalId = value;
        if (field == "observed") fixture.Options.DemoBrokerStateAttestationObservedAtUtc = value;
        var provider = new LmaxDemoOperatorBrokerStateAttestationProvider(fixture.Repository, fixture.Options, fixture.Clock);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetPositionsAsync(fixture.State.BrokerAccounts.Single().Id, CancellationToken.None));

        Assert.Equal(expected, ex.Message);
    }

    private static BridgeFixture Fixture()
    {
        var now = new DateTimeOffset(2026, 8, 31, 8, 30, 0, TimeSpan.Zero);
        var clock = new FixedClock(now);
        var state = SeedData.Create(now);
        var fund = state.Funds.Single();
        var instrument = state.Instruments.Single(x => x.Symbol == "EURUSD");
        var venue = state.Venues.Single(x => x.Name == "LMAX");
        var mapping = state.VenueInstrumentMappings.Single(x => x.InstrumentId == instrument.Id && x.VenueId == venue.Id);
        var run = new ModelRun(
            ModelRunId.New(), fund.Id, "IntradayFxModel", now, now, now.AddMinutes(15), 15,
            1_000_000m, ModelRunStatus.Processing, "qubes-hash", "qubes-r009", false,
            TargetQuantityMode.FxBaseCurrencyQuantity);
        state.ModelRuns.Add(run);
        state.TargetPositions.Add(new TargetPosition(run.Id, instrument.Id, 1_100m, 1_000m, 0.1m, TargetQuantityMode.FxBaseCurrencyQuantity));
        var intent = new TradeIntent(TradeIntentId.New(), run.Id, fund.Id, instrument.Id, TradeSide.Buy, 1_000m, 0.1m, "Model drift", TradeIntentStatus.Ordered, now);
        state.TradeIntents.Add(intent);
        var parent = new ParentOrder(ParentOrderId.New(), intent.Id, new ClientOrderId("PTEST"), OrderSide.Buy, 1_000m, ExecutionAlgo.CloseSeeking15m, OrderStatus.Created, now);
        var child = new ChildOrder(ChildOrderId.New(), parent.Id, venue.Id, new ClientOrderId("CTEST"), OrderSide.Buy, OrderType.Limit, TimeInForce.GFD, 1_000m, 0.1m, OrderStatus.PendingNew, now);
        state.ParentOrders.Add(parent);
        state.ChildOrders.Add(child);
        state.ReconciliationRuns.Add(new ReconciliationRun(Guid.NewGuid(), run.Id, ReconciliationPhase.PreTrade, now, false));
        state.MarketData.Add(new MarketDataSnapshot(MarketDataSnapshotId.New(), instrument.Id, venue.Id, 1.1m, 1.1001m, null, "LMAX Demo", now, now));
        var repository = new InMemoryIntradayRepository(state);
        var options = Options();
        options.InstrumentSymbol = instrument.Symbol;
        options.LmaxSlashSymbol = mapping.VenueInstrumentCode;
        return new(state, repository, options, clock, fund, instrument, run,
            new VenueOrderRequest(child.Id, venue.Id, instrument.Id, child.ClientOrderId, child.Side, child.OrderType, child.TimeInForce, child.BaseQuantity, child.VenueQuantity));
    }

    private static LmaxConnectivityLabOptions Options()
        => new()
        {
            Enabled = true,
            EnvironmentName = "Demo",
            AllowExternalConnections = true,
            AllowOrderSubmission = true,
            AllowLiveTrading = false,
            DryRun = false,
            FixOrderHost = "fix-order.london-demo.lmax.com",
            FixOrderPort = 443,
            FixMarketDataHost = "fix-marketdata.london-demo.lmax.com",
            FixMarketDataPort = 443,
            FixUsername = "demo-user",
            FixSenderCompId = "demo-user",
            FixPassword = "demo-password",
            FixOrderTargetCompId = "LMXBD",
            FixMarketDataTargetCompId = "LMXBD",
            MaxDemoOrderQuantity = 1m,
            AccountCode = "LMAX_DEMO_LOCAL",
            DemoBrokerStateAttestationAccountCode = "LMAX_DEMO_LOCAL",
            DemoBrokerStateAttestationInstruments = "EURUSD",
            DemoBrokerStateAttestationObservedAtUtc = "2026-08-31T08:29:00Z",
            DemoBrokerStateAttestationFlat = true,
            DemoBrokerStateAttestationNoWorkingOrders = true,
            DemoBrokerStateAttestationApprovalId = "DEMO-APPROVED-001"
        };

    private sealed class CapturingSession : ILmaxDemoStrategySession
    {
        public int SendCount { get; private set; }
        public LmaxDemoStrategyExecutionRequest? Request { get; private set; }
        public Task<LmaxDemoStrategyQuote> GetTopOfBookAsync(LmaxConnectivityLabOptions options, TimeSpan maxAge, CancellationToken cancellationToken)
            => Task.FromResult(new LmaxDemoStrategyQuote(1.1m, 1.1001m, 1.10005m, DateTimeOffset.UtcNow));
        public Task<LmaxDemoStrategyExecutionResult> ExecuteStrategyParentAsync(LmaxConnectivityLabOptions options, LmaxDemoStrategyExecutionRequest request, CancellationToken cancellationToken)
        {
            SendCount++;
            Request = request;
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new LmaxDemoStrategyExecutionResult([], [LmaxDemoStrategyPhase.PassivePosted, LmaxDemoStrategyPhase.PassiveReprice, LmaxDemoStrategyPhase.AggressiveResidual, LmaxDemoStrategyPhase.Complete], [], request.VenueQuantity, request.VenueQuantity, 0m, true, "BROKER", now, now, []));
        }
    }

    private sealed record BridgeFixture(
        PlatformState State,
        InMemoryIntradayRepository Repository,
        LmaxConnectivityLabOptions Options,
        FixedClock Clock,
        Fund Fund,
        Instrument Instrument,
        ModelRun Run,
        VenueOrderRequest Request);
}

file static class LmaxDemoStrategyTestOptionsExtensions
{
    public static LmaxConnectivityLabOptions withEnvironment(this LmaxConnectivityLabOptions options, string environment)
    {
        options.EnvironmentName = environment;
        return options;
    }
}
