using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxShadowModeTests
{
    private static readonly DateTimeOffset Now = new(2026, 05, 06, 09, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task Replay_with_matching_execution_report_fill_creates_info_match()
    {
        var (state, service) = CreateService();
        var fill = SeedFilledOrder(state, "exec-1", "CO-1", OrderStatus.Filled);

        var run = await service.ReplayAsync(new LmaxShadowReplayRequest(
            LmaxShadowInputSource.SyntheticFixture,
            [new LmaxShadowExecutionReportInput("exec-1", "BO-1", "CO-1", "Trade", "Filled", fill.InstrumentId, "EURUSD", "Buy", 0.1m, 1.17m, 0m, 0.1m, 1.17m, Now)],
            [],
            [],
            [],
            "Unit test replay"), CancellationToken.None);

        Assert.Equal(LmaxShadowReplayStatus.Completed, run.Status);
        Assert.Contains(state.LmaxShadowObservations, x => x.Type == LmaxShadowObservationType.ExecutionReportMatchesInternalFill && x.Severity == LmaxShadowObservationSeverity.Info);
    }

    [Fact]
    public async Task Replay_with_missing_internal_fill_creates_warning()
    {
        var (state, service) = CreateService();

        await service.ReplayAsync(new LmaxShadowReplayRequest(
            LmaxShadowInputSource.SyntheticFixture,
            [new LmaxShadowExecutionReportInput("missing-exec", "BO-2", "CO-2", "F", "Filled", null, "EURUSD", "Buy", 0.1m, 1.17m, 0m, 0.1m, 1.17m, Now)],
            [],
            [],
            [],
            "Unit test replay"), CancellationToken.None);

        Assert.Contains(state.LmaxShadowObservations, x => x.Type == LmaxShadowObservationType.ExecutionReportMissingInternalFill && x.Severity == LmaxShadowObservationSeverity.Warning);
    }

    [Fact]
    public async Task Trade_capture_match_creates_info_observation_and_trade_uti_warning_only()
    {
        var (state, service) = CreateService();
        var fill = SeedFilledOrder(state, "exec-2", "CO-3", OrderStatus.Filled);

        await service.ReplayAsync(new LmaxShadowReplayRequest(
            LmaxShadowInputSource.SyntheticFixture,
            [],
            [new LmaxShadowTradeCaptureInput("exec-2", null, "BO-3", "CO-3", fill.InstrumentId, "EURUSD", "Buy", 0.1m, 1.17m, DateOnly.FromDateTime(Now.UtcDateTime), Now, null, true)],
            [],
            [],
            "Unit test replay"), CancellationToken.None);

        var observation = Assert.Single(state.LmaxShadowObservations, x => x.Type == LmaxShadowObservationType.TradeCaptureMatchesInternalFill);
        Assert.Equal(LmaxShadowObservationSeverity.Info, observation.Severity);
        Assert.Contains("TradeUTI", observation.DifferenceJson);
    }

    [Fact]
    public async Task Order_status_report_does_not_create_fill_observation()
    {
        var (state, service) = CreateService();
        SeedFilledOrder(state, "exec-3", "CO-4", OrderStatus.Filled);

        await service.ReplayAsync(new LmaxShadowReplayRequest(
            LmaxShadowInputSource.SyntheticFixture,
            [new LmaxShadowExecutionReportInput("status-1", "BO-4", "CO-4", "I", "Filled", null, "EURUSD", "Buy", null, null, 0m, 0.1m, 1.17m, Now)],
            [],
            [],
            [],
            "Unit test replay"), CancellationToken.None);

        Assert.DoesNotContain(state.LmaxShadowObservations, x => x.Type == LmaxShadowObservationType.ExecutionReportMatchesInternalFill);
        Assert.Contains(state.LmaxShadowObservations, x => x.Type == LmaxShadowObservationType.OrderStatusMatchesInternalOrder);
    }

    [Fact]
    public async Task Protocol_reject_creates_blocking_observation_exception_and_audit()
    {
        var (state, service) = CreateService();

        await service.ReplayAsync(new LmaxShadowReplayRequest(
            LmaxShadowInputSource.SyntheticFixture,
            [],
            [],
            [],
            [new LmaxShadowProtocolRejectInput("D", 21, 0, "UnknownTag", "CO-5", null)],
            "Unit test protocol reject"), CancellationToken.None);

        Assert.Contains(state.LmaxShadowObservations, x => x.Type == LmaxShadowObservationType.ProtocolRejectObserved && x.Severity == LmaxShadowObservationSeverity.Blocking);
        Assert.Contains(state.ExceptionCases, x => x.EntityType == "LmaxShadowObservation");
        Assert.Contains(state.OperatorAuditEvents, x => x.EventType == OperatorAuditEventType.LmaxShadowObservationCreated);
        Assert.Contains(state.OperatorAuditEvents, x => x.EventType == OperatorAuditEventType.LmaxShadowReplayCompleted);
    }

    [Fact]
    public async Task Observation_actions_require_reason_and_create_audit()
    {
        var (state, service) = CreateService();
        await service.ReplayAsync(new LmaxShadowReplayRequest(
            LmaxShadowInputSource.SyntheticFixture,
            [],
            [],
            [],
            [new LmaxShadowProtocolRejectInput("D", 21, 0, "UnknownTag", "CO-6", null)],
            "Unit test protocol reject"), CancellationToken.None);
        var observation = state.LmaxShadowObservations[0];

        await Assert.ThrowsAsync<DomainRuleViolationException>(() => service.AcknowledgeObservationAsync(observation.Id, "", CancellationToken.None));
        var updated = await service.AcknowledgeObservationAsync(observation.Id, "Operator reviewed", CancellationToken.None);

        Assert.Equal(LmaxShadowObservationStatus.Acknowledged, updated.Status);
        Assert.Contains(state.OperatorAuditEvents, x => x.EventType == OperatorAuditEventType.LmaxShadowObservationAcknowledged && x.Reason == "Operator reviewed");
    }

    private static (PlatformState State, ILmaxShadowReplayService Service) CreateService()
    {
        var state = new PlatformState();
        var context = new StaticOperatorContext(OperatorAuditActorType.Operator, "local-admin", "Local Admin", "corr-shadow", "req-shadow");
        var clock = new FixedClock(Now);
        var audit = new OperatorAuditService(new InMemoryOperatorAuditRepository(state), context, clock);
        var intraday = new InMemoryIntradayRepository(state);
        var exceptionCases = new ExceptionCaseService(new InMemoryExceptionCaseRepository(state), audit, context, clock, intraday);
        var service = new LmaxShadowModeService(new InMemoryLmaxShadowRepository(state), intraday, audit, exceptionCases, context, clock);
        return (state, service);
    }

    private static Fill SeedFilledOrder(PlatformState state, string brokerExecutionId, string clientOrderId, OrderStatus childStatus)
    {
        var fundId = FundId.New();
        var instrumentId = InstrumentId.New();
        var venueId = VenueId.New();
        var tradeIntentId = TradeIntentId.New();
        var parent = new ParentOrder(ParentOrderId.New(), tradeIntentId, new ClientOrderId(clientOrderId), OrderSide.Buy, 0.1m, ExecutionAlgo.MarketImmediate, childStatus, Now);
        var child = new ChildOrder(ChildOrderId.New(), parent.Id, venueId, new ClientOrderId(clientOrderId), OrderSide.Buy, OrderType.Market, TimeInForce.IOC, 0.1m, 0.1m, childStatus, Now);
        var report = new ExecutionReport(ExecutionReportId.New(), child.Id, venueId, "BO-1", brokerExecutionId, new ClientOrderId(clientOrderId), ExecutionReportType.Fill, 0.1m, 1.17m, 0m, 0.1m, 1.17m, Now);
        var fill = new Fill(FillId.New(), brokerExecutionId, child.Id, instrumentId, venueId, TradeSide.Buy, 0.1m, 0.1m, 1.17m, Now, Now);
        state.Funds.Add(new Fund(fundId, "QQ_MASTER", Currency.Usd));
        state.Instruments.Add(new Instrument(instrumentId, "EURUSD", AssetClass.FxSpot, Currency.Eur, Currency.Usd, 5, 1));
        state.Venues.Add(new Venue(venueId, "LMAX", VenueType.Broker));
        state.ParentOrders.Add(parent);
        state.ChildOrders.Add(child);
        state.ExecutionReports.Add(report);
        state.Fills.Add(fill);
        return fill;
    }
}
