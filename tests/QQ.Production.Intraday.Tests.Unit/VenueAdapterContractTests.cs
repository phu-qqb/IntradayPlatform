using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Infrastructure.Lmax;
using QQ.Production.Intraday.Infrastructure.Simulator;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class VenueAdapterContractTests
{
    public static IEnumerable<object[]> ContractScenarios()
        => VenueAdapterContractScenarios.All().Select(scenario => new object[] { scenario });

    [Fact]
    public void Lmax_exec_type_new_maps_to_order_accepted()
    {
        var venueEvent = LmaxVenueExecutionEventMapper.FromExecutionReport(ExecutionReport(LmaxNormalizedExecutionType.New, LmaxNormalizedOrderStatusValue.New, lastQty: null, leavesQty: 1m));

        Assert.Equal(VenueExecutionEventType.OrderAccepted, venueEvent.EventType);
        Assert.Equal(VenueExecutionEventStatus.Accepted, venueEvent.Status);
    }

    [Fact]
    public void Lmax_exec_type_trade_filled_maps_to_fill()
    {
        var venueEvent = LmaxVenueExecutionEventMapper.FromExecutionReport(ExecutionReport(LmaxNormalizedExecutionType.Trade, LmaxNormalizedOrderStatusValue.Filled, lastQty: 1m, leavesQty: 0m));

        Assert.Equal(VenueExecutionEventType.Fill, venueEvent.EventType);
        Assert.Equal(VenueExecutionEventStatus.Filled, venueEvent.Status);
        Assert.Equal(1m, venueEvent.LastQuantity);
    }

    [Fact]
    public void Lmax_exec_type_trade_partial_maps_to_partial_fill()
    {
        var venueEvent = LmaxVenueExecutionEventMapper.FromExecutionReport(ExecutionReport(LmaxNormalizedExecutionType.Trade, LmaxNormalizedOrderStatusValue.PartiallyFilled, lastQty: 0.4m, leavesQty: 0.6m));

        Assert.Equal(VenueExecutionEventType.PartialFill, venueEvent.EventType);
        Assert.Equal(VenueExecutionEventStatus.PartiallyFilled, venueEvent.Status);
        Assert.Equal(0.6m, venueEvent.LeavesQuantity);
    }

    [Fact]
    public void Lmax_exec_type_i_maps_to_order_status_only()
    {
        var venueEvent = LmaxVenueExecutionEventMapper.FromExecutionReport(ExecutionReport(LmaxNormalizedExecutionType.OrderStatus, LmaxNormalizedOrderStatusValue.Filled, lastQty: null, leavesQty: 0m));

        Assert.Equal(VenueExecutionEventType.OrderStatus, venueEvent.EventType);
        Assert.Equal(VenueExecutionEventStatus.Filled, venueEvent.Status);
        Assert.Equal(0m, venueEvent.LastQuantity);
    }

    [Fact]
    public void Lmax_protocol_reject_maps_to_protocol_reject()
    {
        var reject = new LmaxNormalizedFixReject("2", "21", "D", "Tag not defined", "Unknown tag", null);

        var venueEvent = LmaxVenueExecutionEventMapper.FromFixReject(reject);

        Assert.Equal(VenueExecutionEventType.ProtocolReject, venueEvent.EventType);
        Assert.Equal(VenueExecutionEventStatus.ProtocolRejected, venueEvent.Status);
        Assert.Contains("35=D", venueEvent.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(ContractScenarios))]
    public void Contract_scenarios_match_expected_state_transitions(VenueAdapterContractScenario scenario)
    {
        var result = VenueAdapterContractEvaluator.Evaluate(scenario);

        Assert.Equal(scenario.Expected.ExpectedParentOrderStatus, result.ParentOrderStatus);
        Assert.Equal(scenario.Expected.ExpectedChildOrderStatus, result.ChildOrderStatus);
        Assert.Equal(scenario.Expected.ExpectedExecutionReportCount, result.ExecutionReportCount);
        Assert.Equal(scenario.Expected.ExpectedFillCount, result.FillCount);
        Assert.Equal(scenario.Expected.ExpectedPositionLedgerDelta, result.PositionLedgerDelta);

        foreach (var expectedObservation in scenario.Expected.ExpectedObservations)
        {
            Assert.Contains(result.Observations, x => x.Type == expectedObservation);
        }
    }

    [Fact]
    public async Task FakeLmax_full_fill_matches_contract_full_fill_scenario()
    {
        var result = await SendFakeOrderAsync(FakeLmaxBehavior.FullFill);
        var events = FakeLmaxVenueExecutionEventMapper.FromExecutionReports(result.Reports, "EURUSD", OrderSide.Buy);
        var scenario = VenueAdapterContractScenarios.MarketIocFullFill() with
        {
            Lifecycle = new VenueExecutionLifecycle(events)
        };

        var contractResult = VenueAdapterContractEvaluator.Evaluate(scenario);

        Assert.Equal([VenueExecutionEventType.OrderAccepted, VenueExecutionEventType.Fill], events.Select(x => x.EventType));
        Assert.Equal(OrderStatus.Filled, contractResult.ParentOrderStatus);
        Assert.Equal(OrderStatus.Filled, contractResult.ChildOrderStatus);
        Assert.Equal(1, contractResult.FillCount);
        Assert.Equal(1m, contractResult.PositionLedgerDelta);
    }

    [Fact]
    public async Task FakeLmax_partial_expired_matches_contract_scenario()
    {
        var result = await SendFakeOrderAsync(FakeLmaxBehavior.PartialFill, partialFillRatio: 0.4m);
        var events = FakeLmaxVenueExecutionEventMapper.FromExecutionReports(result.Reports, "EURUSD", OrderSide.Buy);
        var scenario = VenueAdapterContractScenarios.MarketIocPartialFillThenExpired() with
        {
            Lifecycle = new VenueExecutionLifecycle(events)
        };

        var contractResult = VenueAdapterContractEvaluator.Evaluate(scenario);

        Assert.Equal([VenueExecutionEventType.OrderAccepted, VenueExecutionEventType.PartialFill, VenueExecutionEventType.Expired], events.Select(x => x.EventType));
        Assert.Equal(OrderStatus.PartiallyFilled, contractResult.ParentOrderStatus);
        Assert.Equal(OrderStatus.Expired, contractResult.ChildOrderStatus);
        Assert.Equal(1, contractResult.FillCount);
        Assert.Equal(0.4m, contractResult.PositionLedgerDelta);
    }

    [Fact]
    public void Duplicate_broker_execution_id_does_not_create_duplicate_fill()
    {
        var result = VenueAdapterContractEvaluator.Evaluate(VenueAdapterContractScenarios.DuplicateExecutionReport());

        Assert.Equal(1, result.FillCount);
        Assert.Equal(1m, result.PositionLedgerDelta);
        Assert.Contains(result.Observations, x => x.Type == VenueAdapterContractObservationType.DuplicateExecutionIgnored);
    }

    [Fact]
    public void TradeCapture_recovery_for_existing_exec_id_does_not_double_count_fill()
    {
        var result = VenueAdapterContractEvaluator.Evaluate(VenueAdapterContractScenarios.TradeCaptureRecoveryMatchingExistingFill());

        Assert.Equal(1, result.FillCount);
        Assert.Equal(1m, result.PositionLedgerDelta);
        Assert.Contains(result.Observations, x => x.Type == VenueAdapterContractObservationType.TradeCaptureMatchedExistingFill);
        Assert.DoesNotContain(result.Observations, x => x.Type == VenueAdapterContractObservationType.TradeCaptureMissingInternalFill);
    }

    [Fact]
    public void TradeCapture_recovery_missing_internal_fill_creates_observation()
    {
        var result = VenueAdapterContractEvaluator.Evaluate(VenueAdapterContractScenarios.TradeCaptureRecoveryMissingInternalFill());

        Assert.Equal(0, result.FillCount);
        Assert.Equal(0m, result.PositionLedgerDelta);
        Assert.Contains(result.Observations, x => x.Type == VenueAdapterContractObservationType.TradeCaptureMissingInternalFill);
    }

    [Fact]
    public void ExecutionReport_fill_missing_from_trade_capture_creates_observation()
    {
        var result = VenueAdapterContractEvaluator.Evaluate(VenueAdapterContractScenarios.ExecutionReportFillMissingFromTradeCapture());

        Assert.Equal(1, result.FillCount);
        Assert.Contains(result.Observations, x => x.Type == VenueAdapterContractObservationType.ExecutionReportFillMissingFromTradeCapture);
    }

    [Fact]
    public void OrderStatus_exec_type_i_does_not_create_fill()
    {
        var result = VenueAdapterContractEvaluator.Evaluate(VenueAdapterContractScenarios.OrderStatusOnlyReport());

        Assert.Equal(0, result.ExecutionReportCount);
        Assert.Equal(0, result.FillCount);
        Assert.Contains(result.Observations, x => x.Type == VenueAdapterContractObservationType.OrderStatusFilledWithoutInternalFilledState);
    }

    [Fact]
    public void Protocol_reject_does_not_create_fill()
    {
        var result = VenueAdapterContractEvaluator.Evaluate(VenueAdapterContractScenarios.ProtocolReject());

        Assert.Equal(0, result.ExecutionReportCount);
        Assert.Equal(0, result.FillCount);
        Assert.Equal(OrderStatus.PendingNew, result.ChildOrderStatus);
        Assert.Contains(result.Observations, x => x.Type == VenueAdapterContractObservationType.ProtocolRejectObserved);
    }

    [Fact]
    public void Partial_fill_then_expired_creates_one_fill_and_expired_child_status()
    {
        var result = VenueAdapterContractEvaluator.Evaluate(VenueAdapterContractScenarios.MarketIocPartialFillThenExpired());

        Assert.Equal(1, result.FillCount);
        Assert.Equal(OrderStatus.PartiallyFilled, result.ParentOrderStatus);
        Assert.Equal(OrderStatus.Expired, result.ChildOrderStatus);
    }

    private static async Task<VenueExecutionResult> SendFakeOrderAsync(FakeLmaxBehavior behavior, decimal partialFillRatio = 0.5m)
    {
        var gateway = new FakeLmaxGateway(
            new FakeLmaxOptions { Behavior = behavior, PartialFillRatio = partialFillRatio, FillPrice = 1.1m },
            new FixedClock(new DateTimeOffset(2026, 5, 6, 9, 0, 0, TimeSpan.Zero)));

        return await gateway.SendOrderAsync(
            new VenueOrderRequest(
                ChildOrderId.New(),
                VenueId.New(),
                InstrumentId.New(),
                new ClientOrderId("CLIENT-FAKE"),
                OrderSide.Buy,
                OrderType.Market,
                TimeInForce.IOC,
                1m,
                1m),
            CancellationToken.None);
    }

    private static LmaxNormalizedExecutionReport ExecutionReport(
        LmaxNormalizedExecutionType execType,
        LmaxNormalizedOrderStatusValue orderStatus,
        decimal? lastQty,
        decimal? leavesQty)
        => new(
            "EXEC-1",
            "ORDER-1",
            "CLIENT-1",
            null,
            execType,
            execType switch
            {
                LmaxNormalizedExecutionType.New => "0",
                LmaxNormalizedExecutionType.Trade => "F",
                LmaxNormalizedExecutionType.OrderStatus => "I",
                LmaxNormalizedExecutionType.Rejected => "8",
                LmaxNormalizedExecutionType.Expired => "C",
                LmaxNormalizedExecutionType.Canceled => "4",
                _ => null
            },
            orderStatus,
            orderStatus switch
            {
                LmaxNormalizedOrderStatusValue.New => "0",
                LmaxNormalizedOrderStatusValue.PartiallyFilled => "1",
                LmaxNormalizedOrderStatusValue.Filled => "2",
                LmaxNormalizedOrderStatusValue.Rejected => "8",
                LmaxNormalizedOrderStatusValue.Expired => "C",
                LmaxNormalizedOrderStatusValue.Canceled => "4",
                _ => null
            },
            "4001",
            "8",
            "EURUSD",
            "EURUSD",
            LmaxNormalizedSide.Buy,
            1m,
            leavesQty,
            lastQty,
            lastQty,
            1.1m,
            1.1m,
            null,
            new DateTimeOffset(2026, 5, 6, 9, 0, 0, TimeSpan.Zero),
            null,
            null,
            execType == LmaxNormalizedExecutionType.Trade,
            null,
            new DateTimeOffset(2026, 5, 6, 9, 0, 0, TimeSpan.Zero),
            []);
}
