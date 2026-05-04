using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Infrastructure.Simulator;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class DomainAndRiskTests
{
    private static readonly DateTimeOffset Now = new(2026, 04, 29, 12, 00, 00, TimeSpan.Zero);
    private static readonly FixedClock Clock = new(Now);

    [Theory]
    [InlineData(-0.10, -100_000, -91_000, -9.1, TradeSide.Sell)]
    [InlineData(0.10, 100_000, 91_000, 9.1, TradeSide.Buy)]
    public void PortfolioBaseCurrencyNotional_calculates_fx_target(decimal weight, decimal expectedNotional, decimal expectedBase, decimal expectedVenue, TradeSide expectedSide)
    {
        var state = SeedData.Create(Now);
        var run = state.ModelRuns.Single() with { TargetQuantityMode = TargetQuantityMode.PortfolioBaseCurrencyNotional };
        var targetWeight = state.TargetWeights.Single() with { Weight = weight };

        var target = new TargetPositionCalculator().Calculate(run, targetWeight, state.MarketData.Single(), state.VenueInstrumentMappings.Single());

        Assert.Equal(expectedNotional, target.TargetNotionalUsd);
        Assert.Equal(expectedBase, target.TargetBaseQuantity);
        Assert.Equal(expectedVenue, target.TargetVenueQuantity);
        Assert.Equal(expectedSide, target.TargetBaseQuantity < 0 ? TradeSide.Sell : TradeSide.Buy);
    }

    [Theory]
    [InlineData(-0.10, -110_000, -100_000, -10.0, TradeSide.Sell)]
    [InlineData(0.10, 110_000, 100_000, 10.0, TradeSide.Buy)]
    public void FxBaseCurrencyQuantity_calculates_fx_target(decimal weight, decimal expectedNotional, decimal expectedBase, decimal expectedVenue, TradeSide expectedSide)
    {
        var state = SeedData.Create(Now);
        var run = state.ModelRuns.Single() with { TargetQuantityMode = TargetQuantityMode.FxBaseCurrencyQuantity };
        var targetWeight = state.TargetWeights.Single() with { Weight = weight };

        var target = new TargetPositionCalculator().Calculate(run, targetWeight, state.MarketData.Single(), state.VenueInstrumentMappings.Single());

        Assert.Equal(expectedNotional, target.TargetNotionalUsd);
        Assert.Equal(expectedBase, target.TargetBaseQuantity);
        Assert.Equal(expectedVenue, target.TargetVenueQuantity);
        Assert.Equal(expectedSide, target.TargetBaseQuantity < 0 ? TradeSide.Sell : TradeSide.Buy);
    }

    [Theory]
    [InlineData(1.04, 1.0)]
    [InlineData(1.05, 1.1)]
    [InlineData(-1.04, -1.0)]
    [InlineData(-1.05, -1.1)]
    public void Rounding_preserves_sign(decimal value, decimal expected)
        => Assert.Equal(expected, QuantityRounding.RoundToStep(value, 0.1m));

    [Fact]
    public void Minimum_quantity_violation_fails_clearly()
    {
        var state = SeedData.Create(Now);
        var mapping = state.VenueInstrumentMappings.Single() with { MinOrderQuantity = 0.2m };
        var run = state.ModelRuns.Single() with { NavUsd = 100_000m };
        var weight = state.TargetWeights.Single() with { Weight = 0.01m };

        Assert.Throws<DomainRuleViolationException>(() => new TargetPositionCalculator().Calculate(run, weight, state.MarketData.Single(), mapping));
    }

    [Fact]
    public void Order_state_machine_accepts_valid_transitions_and_rejects_invalid()
    {
        var machine = new OrderStateMachine();
        Assert.Equal(OrderStatus.PendingNew, machine.Transition(OrderStatus.Created, ExecutionReportType.OrderAck));
        Assert.Equal(OrderStatus.Acked, machine.Transition(OrderStatus.PendingNew, ExecutionReportType.OrderAck));
        Assert.Equal(OrderStatus.Rejected, machine.Transition(OrderStatus.PendingNew, ExecutionReportType.OrderReject));
        Assert.Equal(OrderStatus.PartiallyFilled, machine.Transition(OrderStatus.Acked, ExecutionReportType.PartialFill));
        Assert.Equal(OrderStatus.Filled, machine.Transition(OrderStatus.Acked, ExecutionReportType.Fill));
        Assert.Equal(OrderStatus.Filled, machine.Transition(OrderStatus.PartiallyFilled, ExecutionReportType.Fill));
        Assert.Equal(OrderStatus.Expired, machine.Transition(OrderStatus.PartiallyFilled, ExecutionReportType.Expired));
        Assert.Equal(OrderStatus.PendingCancel, machine.Transition(OrderStatus.Acked, ExecutionReportType.CancelAck));
        Assert.Equal(OrderStatus.Cancelled, machine.Transition(OrderStatus.PendingCancel, ExecutionReportType.CancelAck));
        Assert.Equal(OrderStatus.Expired, machine.Transition(OrderStatus.Acked, ExecutionReportType.Expired));
        Assert.Throws<DomainRuleViolationException>(() => machine.Transition(OrderStatus.Filled, ExecutionReportType.Fill));
        Assert.Throws<DomainRuleViolationException>(() => machine.Transition(OrderStatus.Expired, ExecutionReportType.Expired));
    }

    [Theory]
    [InlineData(FakeLmaxBehavior.FullFill, ExecutionReportType.OrderAck, ExecutionReportType.Fill)]
    [InlineData(FakeLmaxBehavior.PartialFill, ExecutionReportType.OrderAck, ExecutionReportType.PartialFill, ExecutionReportType.Expired)]
    [InlineData(FakeLmaxBehavior.Reject, ExecutionReportType.OrderReject)]
    [InlineData(FakeLmaxBehavior.NoFill, ExecutionReportType.OrderAck, ExecutionReportType.Expired)]
    public async Task Fake_lmax_returns_expected_ioc_report_sequence(FakeLmaxBehavior behavior, params ExecutionReportType[] expected)
    {
        var state = SeedData.Create(Now);
        var gateway = new FakeLmaxGateway(new FakeLmaxOptions { Behavior = behavior }, Clock);

        var result = await gateway.SendOrderAsync(new VenueOrderRequest(ChildOrderId.New(), state.Venues.Single().Id, state.Instruments.Single().Id, new ClientOrderId("C-1"), OrderSide.Buy, OrderType.Market, TimeInForce.IOC, 10_000m, 1m), CancellationToken.None);
        var openOrders = await gateway.GetOpenOrdersAsync(state.Venues.Single().Id, CancellationToken.None);

        Assert.Equal(expected, result.Reports.Select(x => x.ExecutionReportType));
        Assert.Empty(openOrders);
    }

    [Theory]
    [InlineData(nameof(RiskRejectReason.KillSwitchActive))]
    [InlineData(nameof(RiskRejectReason.StaleModelRun))]
    [InlineData(nameof(RiskRejectReason.StaleMarketData))]
    [InlineData(nameof(RiskRejectReason.InstrumentDisabled))]
    [InlineData(nameof(RiskRejectReason.VenueDisabled))]
    [InlineData(nameof(RiskRejectReason.MaxTradeNotionalExceeded))]
    [InlineData(nameof(RiskRejectReason.MaxInstrumentExposureExceeded))]
    [InlineData(nameof(RiskRejectReason.MaxGrossExposureExceeded))]
    [InlineData(nameof(RiskRejectReason.TradingWindowClosed))]
    public void Risk_engine_blocks_configured_reasons(string reasonName)
    {
        var reason = Enum.Parse<RiskRejectReason>(reasonName);
        var state = SeedData.Create(Now);
        var intent = NewIntent(state, requestedBaseQuantity: 91_000m);
        var context = NewRiskContext(state, Now);
        var limits = state.RiskLimitSets.Single();
        var instrumentLimit = state.InstrumentRiskLimits.Single();
        var venueLimit = state.VenueRiskLimits.Single();
        var window = state.TradingWindows.Single(x => x.ModelName == "Sample FX Intraday");
        var killSwitch = state.KillSwitch;

        switch (reason)
        {
            case RiskRejectReason.KillSwitchActive: killSwitch = killSwitch with { IsActive = true }; break;
            case RiskRejectReason.StaleModelRun: context = context with { ModelRun = context.ModelRun with { AsOfUtc = Now.AddDays(-2) } }; break;
            case RiskRejectReason.StaleMarketData: context = context with { MarketData = context.MarketData with { ReceivedAtUtc = Now.AddHours(-2) } }; break;
            case RiskRejectReason.InstrumentDisabled: context = context with { Instrument = context.Instrument with { IsEnabled = false } }; break;
            case RiskRejectReason.VenueDisabled: context = context with { Venue = context.Venue with { IsEnabled = false } }; break;
            case RiskRejectReason.MaxTradeNotionalExceeded: instrumentLimit = instrumentLimit with { MaxTradeNotionalUsd = 1m }; break;
            case RiskRejectReason.MaxInstrumentExposureExceeded: instrumentLimit = instrumentLimit with { MaxExposureUsd = 1m }; break;
            case RiskRejectReason.MaxGrossExposureExceeded: limits = limits with { MaxGrossExposureUsd = 1m }; break;
            case RiskRejectReason.TradingWindowClosed: window = window with { OpensAtUtc = new TimeOnly(13, 0), ClosesAtUtc = new TimeOnly(14, 0), NoNewOrdersAfterUtc = new TimeOnly(14, 0) }; break;
        }

        var decision = new RiskEngine().Evaluate(intent, context, limits, instrumentLimit, venueLimit, window, killSwitch);

        Assert.Equal(reason, decision.RejectReason);
        Assert.NotEqual(RiskDecisionStatus.Approved, decision.Status);
    }

    [Fact]
    public void Changing_configured_trade_limit_changes_risk_decision()
    {
        var state = SeedData.Create(Now);
        var intent = NewIntent(state, requestedBaseQuantity: 91_000m);
        var context = NewRiskContext(state, Now);
        var engine = new RiskEngine();

        var window = state.TradingWindows.Single(x => x.ModelName == "Sample FX Intraday");
        var approved = engine.Evaluate(intent, context, state.RiskLimitSets.Single(), state.InstrumentRiskLimits.Single(), state.VenueRiskLimits.Single(), window, state.KillSwitch);
        var rejected = engine.Evaluate(intent, context, state.RiskLimitSets.Single(), state.InstrumentRiskLimits.Single() with { MaxTradeNotionalUsd = 1m }, state.VenueRiskLimits.Single(), window, state.KillSwitch);

        Assert.Equal(RiskDecisionStatus.Approved, approved.Status);
        Assert.Equal(RiskRejectReason.MaxTradeNotionalExceeded, rejected.RejectReason);
    }

    [Fact]
    public void Risk_engine_records_limit_set_and_observed_limit_details()
    {
        var state = SeedData.Create(Now);
        var intent = NewIntent(state, requestedBaseQuantity: 91_000m);
        var context = NewRiskContext(state, Now);
        var limits = state.RiskLimitSets.Single();
        var instrumentLimit = state.InstrumentRiskLimits.Single() with { MaxTradeNotionalUsd = 1m };
        var window = state.TradingWindows.Single(x => x.ModelName == "Sample FX Intraday");

        var result = new RiskEngine().EvaluateDetailed(intent, context, limits, instrumentLimit, state.VenueRiskLimits.Single(), window, state.KillSwitch);

        Assert.Equal(limits.Id, result.Decision.RiskLimitSetId);
        Assert.Equal(context.ModelRun.Id, result.Decision.ModelRunId);
        Assert.Equal(context.Instrument.Id, result.Decision.InstrumentId);
        Assert.Contains(result.Details, x =>
            x.CheckName == "MaxTradeNotionalUsd" &&
            x.Status == RiskDecisionCheckStatus.Failed &&
            x.ObservedValue > x.LimitValue &&
            x.Unit == "USD");
    }

    [Fact]
    public void Approved_risk_decision_contains_passed_detail_rows()
    {
        var state = SeedData.Create(Now);
        var intent = NewIntent(state, requestedBaseQuantity: 91_000m);
        var context = NewRiskContext(state, Now);
        var window = state.TradingWindows.Single(x => x.ModelName == "Sample FX Intraday");

        var result = new RiskEngine().EvaluateDetailed(intent, context, state.RiskLimitSets.Single(), state.InstrumentRiskLimits.Single(), state.VenueRiskLimits.Single(), window, state.KillSwitch);

        Assert.Equal(RiskDecisionStatus.Approved, result.Decision.Status);
        Assert.NotEmpty(result.Details);
        Assert.Contains(result.Details, x => x.CheckName == "MaxTradeNotionalUsd" && x.Status == RiskDecisionCheckStatus.Passed && x.ObservedValue is not null && x.LimitValue is not null);
        Assert.Contains(result.Details, x => x.CheckName == "TradingWindow" && x.Status == RiskDecisionCheckStatus.Passed);
    }

    [Fact]
    public void Stale_market_data_failure_contains_observed_and_limit_values()
    {
        var state = SeedData.Create(Now);
        var intent = NewIntent(state, requestedBaseQuantity: 91_000m);
        var context = NewRiskContext(state, Now) with { MarketData = state.MarketData.Single() with { ReceivedAtUtc = Now.AddHours(-2) } };
        var window = state.TradingWindows.Single(x => x.ModelName == "Sample FX Intraday");

        var result = new RiskEngine().EvaluateDetailed(intent, context, state.RiskLimitSets.Single(), state.InstrumentRiskLimits.Single(), state.VenueRiskLimits.Single(), window, state.KillSwitch);

        Assert.Equal(RiskRejectReason.StaleMarketData, result.Decision.RejectReason);
        Assert.Contains(result.Details, x =>
            x.CheckName == "MarketDataStalenessSeconds" &&
            x.Status == RiskDecisionCheckStatus.Failed &&
            x.ObservedValue > x.LimitValue &&
            x.Unit == "seconds");
    }

    [Fact]
    public async Task Risk_control_activation_retires_prior_active_set_and_audits()
    {
        var state = SeedData.Create(Now);
        var repository = new InMemoryIntradayRepository(state);
        var auditRepository = new InMemoryOperatorAuditRepository(state);
        var context = new StaticOperatorContext(OperatorAuditActorType.Operator, "local-dev", "Local Dev", "corr-risk");
        var audit = new OperatorAuditService(auditRepository, context, Clock);
        var service = new RiskControlService(repository, audit, context, Clock);

        var active = await service.GetActiveRiskLimitSetAsync("QQ_MASTER", "IntradayFxModel", CancellationToken.None);
        Assert.NotNull(active);

        var draft = await service.CloneRiskLimitSetAsync(active.Id, "prepare safer thresholds", CancellationToken.None);
        var activated = await service.ActivateRiskLimitSetAsync(draft.Id, "activate tested draft profile", CancellationToken.None);
        var loaded = await repository.LoadStateAsync(CancellationToken.None);

        Assert.Equal(RiskLimitSetStatus.Active, activated.Status);
        Assert.Single(loaded.RiskLimitSets, x => x.FundId == active.FundId && x.ModelName == active.ModelName && x.IsActive && x.Status == RiskLimitSetStatus.Active);
        Assert.Contains(loaded.RiskLimitSets, x => x.Id == active.Id && x.Status == RiskLimitSetStatus.Retired && !x.IsActive);
        Assert.Contains(loaded.OperatorAuditEvents, x => x.EventType == OperatorAuditEventType.RiskLimitSetActivated && x.Reason == "activate tested draft profile");
    }

    [Fact]
    public async Task Risk_control_mutations_require_reasons_and_active_limits_are_read_only()
    {
        var state = SeedData.Create(Now);
        var repository = new InMemoryIntradayRepository(state);
        var auditRepository = new InMemoryOperatorAuditRepository(state);
        var context = new StaticOperatorContext(OperatorAuditActorType.Operator, "local-dev", "Local Dev");
        var service = new RiskControlService(repository, new OperatorAuditService(auditRepository, context, Clock), context, Clock);
        var activeLimit = state.RiskLimits.Single(x => x.Name == "MaxTradeNotionalUsd");

        await Assert.ThrowsAsync<DomainRuleViolationException>(() => service.CloneRiskLimitSetAsync(state.RiskLimitSets.Single().Id, "", CancellationToken.None));
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => service.UpdateRiskLimitAsync(activeLimit.Id, 2m, "USD", "direct active edit", CancellationToken.None));
    }

    private static TradeIntent NewIntent(PlatformState state, decimal requestedBaseQuantity)
        => new(TradeIntentId.New(), state.ModelRuns.Single().Id, state.Funds.Single().Id, state.Instruments.Single().Id, TradeSide.Buy, requestedBaseQuantity, requestedBaseQuantity / 10_000m, "test", TradeIntentStatus.Created, Now);

    private static RiskContext NewRiskContext(PlatformState state, DateTimeOffset now)
        => new(state.Funds.Single(), state.Venues.Single(), state.Instruments.Single(), state.ModelRuns.Single(), state.MarketData.Single(), 0m, true, 0m, now);
}
