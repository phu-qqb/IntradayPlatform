using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum VenueExecutionEventType
{
    OrderAccepted,
    OrderRejected,
    Fill,
    PartialFill,
    Expired,
    Cancelled,
    CancelReject,
    OrderStatus,
    ProtocolReject,
    DuplicateExecution,
    TradeCaptureRecovery,
    Unknown
}

public enum VenueExecutionEventStatus
{
    Unknown,
    PendingNew,
    Accepted,
    Rejected,
    PartiallyFilled,
    Filled,
    Expired,
    Cancelled,
    Informational,
    ProtocolRejected
}

public enum VenueAdapterContractSide
{
    Buy,
    Sell,
    Unknown
}

public sealed record VenueOrderSubmissionRequest(
    string ScenarioId,
    string ClientOrderId,
    string Symbol,
    VenueAdapterContractSide Side,
    decimal Quantity,
    string OrderType,
    string TimeInForce);

public sealed record VenueOrderSubmissionResult(
    bool AcceptedByTransport,
    string? BrokerOrderId,
    string? RejectReason);

public sealed record VenueExecutionEvent(
    VenueExecutionEventType EventType,
    VenueExecutionEventStatus Status,
    string? ClientOrderId,
    string? BrokerOrderId,
    string? BrokerExecutionId,
    string? Symbol,
    VenueAdapterContractSide Side,
    decimal LastQuantity,
    decimal LastPrice,
    decimal LeavesQuantity,
    decimal CumulativeQuantity,
    DateTimeOffset? EventTimeUtc,
    string Source,
    string? Message = null,
    bool IsRecoveryEvidence = false);

public sealed record VenueExecutionLifecycle(IReadOnlyList<VenueExecutionEvent> Events);

public enum VenueAdapterContractObservationType
{
    DuplicateExecutionIgnored,
    TradeCaptureMatchedExistingFill,
    TradeCaptureMissingInternalFill,
    ExecutionReportFillMissingFromTradeCapture,
    OrderStatusFilledMatchesInternalChild,
    OrderStatusFilledWithoutInternalFilledState,
    ProtocolRejectObserved,
    UnknownEventObserved
}

public enum VenueAdapterContractObservationSeverity
{
    Info,
    Warning,
    Critical
}

public sealed record VenueAdapterContractObservation(
    VenueAdapterContractObservationType Type,
    VenueAdapterContractObservationSeverity Severity,
    string Message,
    string? ClientOrderId,
    string? BrokerOrderId,
    string? BrokerExecutionId);

public sealed record VenueAdapterContractExpectation(
    OrderStatus ExpectedParentOrderStatus,
    OrderStatus ExpectedChildOrderStatus,
    int ExpectedExecutionReportCount,
    int ExpectedFillCount,
    decimal ExpectedPositionLedgerDelta,
    IReadOnlyList<VenueAdapterContractObservationType> ExpectedObservations);

public sealed record VenueAdapterContractScenario(
    string ScenarioId,
    string Name,
    VenueOrderSubmissionRequest OrderRequest,
    VenueOrderSubmissionResult SubmissionResult,
    VenueExecutionLifecycle Lifecycle,
    VenueAdapterContractExpectation Expected);

public sealed record VenueAdapterContractResult(
    string ScenarioId,
    OrderStatus ParentOrderStatus,
    OrderStatus ChildOrderStatus,
    int ExecutionReportCount,
    int FillCount,
    decimal PositionLedgerDelta,
    IReadOnlyList<VenueAdapterContractObservation> Observations);

public static class LmaxVenueExecutionEventMapper
{
    public static VenueExecutionEvent FromExecutionReport(LmaxNormalizedExecutionReport report)
    {
        var eventType = report.ExecType switch
        {
            LmaxNormalizedExecutionType.New => VenueExecutionEventType.OrderAccepted,
            LmaxNormalizedExecutionType.Trade when (report.LeavesQty ?? 0m) > 0m => VenueExecutionEventType.PartialFill,
            LmaxNormalizedExecutionType.Trade => VenueExecutionEventType.Fill,
            LmaxNormalizedExecutionType.Rejected => VenueExecutionEventType.OrderRejected,
            LmaxNormalizedExecutionType.Expired => VenueExecutionEventType.Expired,
            LmaxNormalizedExecutionType.Canceled => VenueExecutionEventType.Cancelled,
            LmaxNormalizedExecutionType.OrderStatus => VenueExecutionEventType.OrderStatus,
            _ => VenueExecutionEventType.Unknown
        };

        return new VenueExecutionEvent(
            eventType,
            MapStatus(report.OrderStatus, eventType),
            report.ClientOrderId,
            report.BrokerOrderId,
            report.ExecId,
            report.InternalSymbol ?? report.Symbol,
            MapSide(report.Side),
            report.LastQty ?? 0m,
            report.LastPx ?? report.AvgPx ?? report.Price ?? 0m,
            report.LeavesQty ?? 0m,
            report.CumQty ?? 0m,
            report.TransactTimeUtc,
            "LMAX.ExecutionReport",
            report.Text);
    }

    public static VenueExecutionEvent FromTradeCaptureReport(LmaxNormalizedTradeCaptureReport report)
        => new(
            VenueExecutionEventType.TradeCaptureRecovery,
            VenueExecutionEventStatus.Informational,
            report.ClientOrderId,
            report.BrokerOrderId,
            report.ExecId,
            report.InternalSymbol ?? report.Symbol,
            MapSide(report.Side),
            report.LastQty ?? 0m,
            report.LastPx ?? 0m,
            0m,
            report.LastQty ?? 0m,
            report.TransactTimeUtc,
            "LMAX.TradeCaptureReport",
            "TradeCaptureReport is recovery evidence and must not create a second fill when the ExecutionReport fill already exists internally.",
            IsRecoveryEvidence: true);

    public static VenueExecutionEvent FromFixReject(LmaxNormalizedFixReject reject)
        => new(
            VenueExecutionEventType.ProtocolReject,
            VenueExecutionEventStatus.ProtocolRejected,
            null,
            null,
            null,
            null,
            VenueAdapterContractSide.Unknown,
            0m,
            0m,
            0m,
            0m,
            null,
            "LMAX.SessionReject",
            reject.RefMsgType == "D"
                ? $"FIX session reject for NewOrderSingle 35=D: {reject.Text ?? reject.SessionRejectReason ?? "Protocol reject"}"
                : reject.Text ?? reject.SessionRejectReason);

    private static VenueExecutionEventStatus MapStatus(LmaxNormalizedOrderStatusValue status, VenueExecutionEventType eventType)
        => eventType switch
        {
            VenueExecutionEventType.OrderAccepted => VenueExecutionEventStatus.Accepted,
            VenueExecutionEventType.OrderRejected => VenueExecutionEventStatus.Rejected,
            VenueExecutionEventType.PartialFill => VenueExecutionEventStatus.PartiallyFilled,
            VenueExecutionEventType.Fill => VenueExecutionEventStatus.Filled,
            VenueExecutionEventType.Expired => VenueExecutionEventStatus.Expired,
            VenueExecutionEventType.Cancelled => VenueExecutionEventStatus.Cancelled,
            _ => status switch
            {
                LmaxNormalizedOrderStatusValue.New => VenueExecutionEventStatus.Accepted,
                LmaxNormalizedOrderStatusValue.PendingNew => VenueExecutionEventStatus.PendingNew,
                LmaxNormalizedOrderStatusValue.PartiallyFilled => VenueExecutionEventStatus.PartiallyFilled,
                LmaxNormalizedOrderStatusValue.Filled => VenueExecutionEventStatus.Filled,
                LmaxNormalizedOrderStatusValue.Rejected => VenueExecutionEventStatus.Rejected,
                LmaxNormalizedOrderStatusValue.Expired => VenueExecutionEventStatus.Expired,
                LmaxNormalizedOrderStatusValue.Canceled => VenueExecutionEventStatus.Cancelled,
                _ => VenueExecutionEventStatus.Unknown
            }
        };

    private static VenueAdapterContractSide MapSide(LmaxNormalizedSide? side)
        => side switch
        {
            LmaxNormalizedSide.Buy => VenueAdapterContractSide.Buy,
            LmaxNormalizedSide.Sell => VenueAdapterContractSide.Sell,
            _ => VenueAdapterContractSide.Unknown
        };
}

public static class FakeLmaxVenueExecutionEventMapper
{
    public static IReadOnlyList<VenueExecutionEvent> FromExecutionReports(IReadOnlyList<ExecutionReport> reports, string? symbol = null, OrderSide? side = null)
        => reports.Select(report => FromExecutionReport(report, symbol, side)).ToList();

    public static VenueExecutionEvent FromExecutionReport(ExecutionReport report, string? symbol = null, OrderSide? side = null)
        => new(
            MapType(report.ExecutionReportType),
            MapStatus(report.ExecutionReportType),
            report.ClientOrderId.Value,
            report.BrokerOrderId,
            string.IsNullOrWhiteSpace(report.BrokerExecutionId) ? null : report.BrokerExecutionId,
            symbol,
            side switch
            {
                OrderSide.Buy => VenueAdapterContractSide.Buy,
                OrderSide.Sell => VenueAdapterContractSide.Sell,
                _ => VenueAdapterContractSide.Unknown
            },
            report.LastQuantity,
            report.LastPrice,
            report.LeavesQuantity,
            report.CumulativeQuantity,
            report.ReceivedAtUtc,
            "FakeLmax.ExecutionReport");

    private static VenueExecutionEventType MapType(ExecutionReportType reportType)
        => reportType switch
        {
            ExecutionReportType.OrderAck => VenueExecutionEventType.OrderAccepted,
            ExecutionReportType.OrderReject => VenueExecutionEventType.OrderRejected,
            ExecutionReportType.Fill => VenueExecutionEventType.Fill,
            ExecutionReportType.PartialFill => VenueExecutionEventType.PartialFill,
            ExecutionReportType.CancelAck => VenueExecutionEventType.Cancelled,
            ExecutionReportType.CancelReject => VenueExecutionEventType.CancelReject,
            ExecutionReportType.Expired => VenueExecutionEventType.Expired,
            _ => VenueExecutionEventType.Unknown
        };

    private static VenueExecutionEventStatus MapStatus(ExecutionReportType reportType)
        => reportType switch
        {
            ExecutionReportType.OrderAck => VenueExecutionEventStatus.Accepted,
            ExecutionReportType.OrderReject => VenueExecutionEventStatus.Rejected,
            ExecutionReportType.Fill => VenueExecutionEventStatus.Filled,
            ExecutionReportType.PartialFill => VenueExecutionEventStatus.PartiallyFilled,
            ExecutionReportType.CancelAck => VenueExecutionEventStatus.Cancelled,
            ExecutionReportType.CancelReject => VenueExecutionEventStatus.Rejected,
            ExecutionReportType.Expired => VenueExecutionEventStatus.Expired,
            _ => VenueExecutionEventStatus.Unknown
        };
}

public static class VenueAdapterContractEvaluator
{
    public static VenueAdapterContractResult Evaluate(VenueAdapterContractScenario scenario)
    {
        var childStatus = OrderStatus.PendingNew;
        var parentStatus = OrderStatus.Created;
        var reportCount = 0;
        var fills = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var tradeCaptureExecIds = new HashSet<string>(StringComparer.Ordinal);
        var executionReportFillExecIds = new HashSet<string>(StringComparer.Ordinal);
        var observations = new List<VenueAdapterContractObservation>();

        foreach (var venueEvent in scenario.Lifecycle.Events)
        {
            switch (venueEvent.EventType)
            {
                case VenueExecutionEventType.OrderAccepted:
                    reportCount++;
                    childStatus = OrderStatus.Acked;
                    break;
                case VenueExecutionEventType.OrderRejected:
                    reportCount++;
                    childStatus = OrderStatus.Rejected;
                    parentStatus = OrderStatus.Rejected;
                    break;
                case VenueExecutionEventType.Fill:
                case VenueExecutionEventType.PartialFill:
                    reportCount++;
                    HandleFill(venueEvent, fills, observations);
                    if (!string.IsNullOrWhiteSpace(venueEvent.BrokerExecutionId))
                    {
                        executionReportFillExecIds.Add(venueEvent.BrokerExecutionId);
                    }

                    childStatus = venueEvent.EventType == VenueExecutionEventType.Fill ? OrderStatus.Filled : OrderStatus.PartiallyFilled;
                    parentStatus = childStatus;
                    break;
                case VenueExecutionEventType.TradeCaptureRecovery:
                    HandleTradeCapture(venueEvent, fills, tradeCaptureExecIds, observations);
                    break;
                case VenueExecutionEventType.Expired:
                    reportCount++;
                    childStatus = OrderStatus.Expired;
                    parentStatus = fills.Count > 0 ? OrderStatus.PartiallyFilled : OrderStatus.Expired;
                    break;
                case VenueExecutionEventType.Cancelled:
                    reportCount++;
                    childStatus = OrderStatus.Cancelled;
                    parentStatus = OrderStatus.Cancelled;
                    break;
                case VenueExecutionEventType.CancelReject:
                    reportCount++;
                    observations.Add(Observation(VenueAdapterContractObservationType.ProtocolRejectObserved, VenueAdapterContractObservationSeverity.Warning, venueEvent, "Cancel reject observed without mutating fills or position ledger."));
                    break;
                case VenueExecutionEventType.OrderStatus:
                    HandleOrderStatus(venueEvent, childStatus, observations);
                    break;
                case VenueExecutionEventType.ProtocolReject:
                    observations.Add(Observation(VenueAdapterContractObservationType.ProtocolRejectObserved, VenueAdapterContractObservationSeverity.Warning, venueEvent, "Protocol reject observed without creating fills or ledger events."));
                    break;
                case VenueExecutionEventType.DuplicateExecution:
                    observations.Add(Observation(VenueAdapterContractObservationType.DuplicateExecutionIgnored, VenueAdapterContractObservationSeverity.Info, venueEvent, "Duplicate execution observed and ignored."));
                    break;
                case VenueExecutionEventType.Unknown:
                    observations.Add(Observation(VenueAdapterContractObservationType.UnknownEventObserved, VenueAdapterContractObservationSeverity.Warning, venueEvent, "Unknown venue event observed."));
                    break;
            }
        }

        foreach (var execId in executionReportFillExecIds.Where(execId => !tradeCaptureExecIds.Contains(execId)))
        {
            observations.Add(new VenueAdapterContractObservation(
                VenueAdapterContractObservationType.ExecutionReportFillMissingFromTradeCapture,
                VenueAdapterContractObservationSeverity.Warning,
                "ExecutionReport fill has no matching TradeCapture recovery evidence.",
                scenario.OrderRequest.ClientOrderId,
                scenario.SubmissionResult.BrokerOrderId,
                execId));
        }

        return new VenueAdapterContractResult(
            scenario.ScenarioId,
            parentStatus,
            childStatus,
            reportCount,
            fills.Count,
            fills.Values.Sum(),
            observations);
    }

    private static void HandleFill(
        VenueExecutionEvent venueEvent,
        IDictionary<string, decimal> fills,
        ICollection<VenueAdapterContractObservation> observations)
    {
        var execId = venueEvent.BrokerExecutionId;
        if (string.IsNullOrWhiteSpace(execId))
        {
            return;
        }

        if (fills.ContainsKey(execId))
        {
            observations.Add(Observation(VenueAdapterContractObservationType.DuplicateExecutionIgnored, VenueAdapterContractObservationSeverity.Info, venueEvent, "Duplicate BrokerExecutionId ignored for fill and ledger idempotency."));
            return;
        }

        fills.Add(execId, venueEvent.LastQuantity);
    }

    private static void HandleTradeCapture(
        VenueExecutionEvent venueEvent,
        IReadOnlyDictionary<string, decimal> fills,
        ICollection<string> tradeCaptureExecIds,
        ICollection<VenueAdapterContractObservation> observations)
    {
        var execId = venueEvent.BrokerExecutionId;
        if (string.IsNullOrWhiteSpace(execId))
        {
            return;
        }

        tradeCaptureExecIds.Add(execId);
        observations.Add(fills.ContainsKey(execId)
            ? Observation(VenueAdapterContractObservationType.TradeCaptureMatchedExistingFill, VenueAdapterContractObservationSeverity.Info, venueEvent, "TradeCapture recovery matched an existing internal fill and did not create a duplicate.")
            : Observation(VenueAdapterContractObservationType.TradeCaptureMissingInternalFill, VenueAdapterContractObservationSeverity.Warning, venueEvent, "TradeCapture recovery evidence has no matching internal fill."));
    }

    private static void HandleOrderStatus(
        VenueExecutionEvent venueEvent,
        OrderStatus childStatus,
        ICollection<VenueAdapterContractObservation> observations)
    {
        if (venueEvent.Status == VenueExecutionEventStatus.Filled && childStatus == OrderStatus.Filled)
        {
            observations.Add(Observation(VenueAdapterContractObservationType.OrderStatusFilledMatchesInternalChild, VenueAdapterContractObservationSeverity.Info, venueEvent, "OrderStatus Filled matches internal child status."));
            return;
        }

        if (venueEvent.Status == VenueExecutionEventStatus.Filled)
        {
            observations.Add(Observation(VenueAdapterContractObservationType.OrderStatusFilledWithoutInternalFilledState, VenueAdapterContractObservationSeverity.Warning, venueEvent, "OrderStatus Filled observed without internal filled child state."));
        }
    }

    private static VenueAdapterContractObservation Observation(
        VenueAdapterContractObservationType type,
        VenueAdapterContractObservationSeverity severity,
        VenueExecutionEvent venueEvent,
        string message)
        => new(type, severity, message, venueEvent.ClientOrderId, venueEvent.BrokerOrderId, venueEvent.BrokerExecutionId);
}

public static class VenueAdapterContractScenarios
{
    public static IReadOnlyList<VenueAdapterContractScenario> All()
        =>
        [
            MarketIocFullFill(),
            MarketIocPartialFillThenExpired(),
            MarketIocReject(),
            MarketIocNoFillExpired(),
            ProtocolReject(),
            DuplicateExecutionReport(),
            OrderStatusOnlyReport(),
            TradeCaptureRecoveryMatchingExistingFill(),
            TradeCaptureRecoveryMissingInternalFill(),
            ExecutionReportFillMissingFromTradeCapture()
        ];

    public static VenueAdapterContractScenario MarketIocFullFill()
    {
        var request = Request("market-ioc-full-fill");
        return Scenario(
            request,
            "Market IOC full fill",
            [Accepted(request, "ORDER-1"), Fill(request, "ORDER-1", "EXEC-1", 1m, 0m)],
            new VenueAdapterContractExpectation(OrderStatus.Filled, OrderStatus.Filled, 2, 1, 1m, [VenueAdapterContractObservationType.ExecutionReportFillMissingFromTradeCapture]));
    }

    public static VenueAdapterContractScenario MarketIocPartialFillThenExpired()
    {
        var request = Request("market-ioc-partial-fill-expired");
        return Scenario(
            request,
            "Market IOC partial fill then expired",
            [Accepted(request, "ORDER-2"), PartialFill(request, "ORDER-2", "EXEC-2", 0.4m, 0.6m), Expired(request, "ORDER-2", 0.6m, 0.4m)],
            new VenueAdapterContractExpectation(OrderStatus.PartiallyFilled, OrderStatus.Expired, 3, 1, 0.4m, [VenueAdapterContractObservationType.ExecutionReportFillMissingFromTradeCapture]));
    }

    public static VenueAdapterContractScenario MarketIocReject()
    {
        var request = Request("market-ioc-reject");
        return Scenario(
            request,
            "Market IOC reject",
            [Rejected(request, "ORDER-3")],
            new VenueAdapterContractExpectation(OrderStatus.Rejected, OrderStatus.Rejected, 1, 0, 0m, []));
    }

    public static VenueAdapterContractScenario MarketIocNoFillExpired()
    {
        var request = Request("market-ioc-no-fill-expired");
        return Scenario(
            request,
            "Market IOC no fill expired",
            [Accepted(request, "ORDER-4"), Expired(request, "ORDER-4", 1m, 0m)],
            new VenueAdapterContractExpectation(OrderStatus.Expired, OrderStatus.Expired, 2, 0, 0m, []));
    }

    public static VenueAdapterContractScenario ProtocolReject()
    {
        var request = Request("protocol-reject");
        return Scenario(
            request,
            "Protocol reject",
            [new VenueExecutionEvent(VenueExecutionEventType.ProtocolReject, VenueExecutionEventStatus.ProtocolRejected, request.ClientOrderId, null, null, request.Symbol, request.Side, 0m, 0m, 0m, 0m, null, "LMAX.SessionReject", "FIX session reject for 35=D")],
            new VenueAdapterContractExpectation(OrderStatus.Created, OrderStatus.PendingNew, 0, 0, 0m, [VenueAdapterContractObservationType.ProtocolRejectObserved]));
    }

    public static VenueAdapterContractScenario DuplicateExecutionReport()
    {
        var request = Request("duplicate-execution-report");
        return Scenario(
            request,
            "Duplicate execution report",
            [Accepted(request, "ORDER-5"), Fill(request, "ORDER-5", "EXEC-DUP", 1m, 0m), Fill(request, "ORDER-5", "EXEC-DUP", 1m, 0m)],
            new VenueAdapterContractExpectation(OrderStatus.Filled, OrderStatus.Filled, 3, 1, 1m, [VenueAdapterContractObservationType.DuplicateExecutionIgnored, VenueAdapterContractObservationType.ExecutionReportFillMissingFromTradeCapture]));
    }

    public static VenueAdapterContractScenario OrderStatusOnlyReport()
    {
        var request = Request("order-status-only-report");
        return Scenario(
            request,
            "OrderStatus-only report",
            [new VenueExecutionEvent(VenueExecutionEventType.OrderStatus, VenueExecutionEventStatus.Filled, request.ClientOrderId, "ORDER-6", "STATUS-1", request.Symbol, request.Side, 0m, 0m, 0m, 1m, null, "LMAX.ExecutionReport")],
            new VenueAdapterContractExpectation(OrderStatus.Created, OrderStatus.PendingNew, 0, 0, 0m, [VenueAdapterContractObservationType.OrderStatusFilledWithoutInternalFilledState]));
    }

    public static VenueAdapterContractScenario TradeCaptureRecoveryMatchingExistingFill()
    {
        var request = Request("trade-capture-recovery-matching-existing-fill");
        return Scenario(
            request,
            "TradeCapture recovery matching existing fill",
            [Accepted(request, "ORDER-7"), Fill(request, "ORDER-7", "EXEC-7", 1m, 0m), TradeCapture(request, "ORDER-7", "EXEC-7", 1m)],
            new VenueAdapterContractExpectation(OrderStatus.Filled, OrderStatus.Filled, 2, 1, 1m, [VenueAdapterContractObservationType.TradeCaptureMatchedExistingFill]));
    }

    public static VenueAdapterContractScenario TradeCaptureRecoveryMissingInternalFill()
    {
        var request = Request("trade-capture-recovery-missing-internal-fill");
        return Scenario(
            request,
            "TradeCapture recovery missing internal fill",
            [TradeCapture(request, "ORDER-8", "EXEC-8", 1m)],
            new VenueAdapterContractExpectation(OrderStatus.Created, OrderStatus.PendingNew, 0, 0, 0m, [VenueAdapterContractObservationType.TradeCaptureMissingInternalFill]));
    }

    public static VenueAdapterContractScenario ExecutionReportFillMissingFromTradeCapture()
    {
        var request = Request("execution-report-fill-missing-from-trade-capture");
        return Scenario(
            request,
            "ExecutionReport fill missing from TradeCapture",
            [Accepted(request, "ORDER-9"), Fill(request, "ORDER-9", "EXEC-9", 1m, 0m)],
            new VenueAdapterContractExpectation(OrderStatus.Filled, OrderStatus.Filled, 2, 1, 1m, [VenueAdapterContractObservationType.ExecutionReportFillMissingFromTradeCapture]));
    }

    private static VenueAdapterContractScenario Scenario(
        VenueOrderSubmissionRequest request,
        string name,
        IReadOnlyList<VenueExecutionEvent> events,
        VenueAdapterContractExpectation expectation)
        => new(
            request.ScenarioId,
            name,
            request,
            new VenueOrderSubmissionResult(true, events.FirstOrDefault(x => x.BrokerOrderId is not null)?.BrokerOrderId, null),
            new VenueExecutionLifecycle(events),
            expectation);

    private static VenueOrderSubmissionRequest Request(string scenarioId)
        => new(scenarioId, $"CLIENT-{scenarioId}", "EURUSD", VenueAdapterContractSide.Buy, 1m, "Market", "IOC");

    private static VenueExecutionEvent Accepted(VenueOrderSubmissionRequest request, string brokerOrderId)
        => new(VenueExecutionEventType.OrderAccepted, VenueExecutionEventStatus.Accepted, request.ClientOrderId, brokerOrderId, null, request.Symbol, request.Side, 0m, 0m, request.Quantity, 0m, null, "Synthetic.ExecutionReport");

    private static VenueExecutionEvent Rejected(VenueOrderSubmissionRequest request, string brokerOrderId)
        => new(VenueExecutionEventType.OrderRejected, VenueExecutionEventStatus.Rejected, request.ClientOrderId, brokerOrderId, null, request.Symbol, request.Side, 0m, 0m, request.Quantity, 0m, null, "Synthetic.ExecutionReport");

    private static VenueExecutionEvent Fill(VenueOrderSubmissionRequest request, string brokerOrderId, string brokerExecutionId, decimal quantity, decimal leavesQuantity)
        => new(VenueExecutionEventType.Fill, VenueExecutionEventStatus.Filled, request.ClientOrderId, brokerOrderId, brokerExecutionId, request.Symbol, request.Side, quantity, 1.1m, leavesQuantity, request.Quantity - leavesQuantity, null, "Synthetic.ExecutionReport");

    private static VenueExecutionEvent PartialFill(VenueOrderSubmissionRequest request, string brokerOrderId, string brokerExecutionId, decimal quantity, decimal leavesQuantity)
        => new(VenueExecutionEventType.PartialFill, VenueExecutionEventStatus.PartiallyFilled, request.ClientOrderId, brokerOrderId, brokerExecutionId, request.Symbol, request.Side, quantity, 1.1m, leavesQuantity, quantity, null, "Synthetic.ExecutionReport");

    private static VenueExecutionEvent Expired(VenueOrderSubmissionRequest request, string brokerOrderId, decimal leavesQuantity, decimal cumulativeQuantity)
        => new(VenueExecutionEventType.Expired, VenueExecutionEventStatus.Expired, request.ClientOrderId, brokerOrderId, null, request.Symbol, request.Side, 0m, 0m, leavesQuantity, cumulativeQuantity, null, "Synthetic.ExecutionReport");

    private static VenueExecutionEvent TradeCapture(VenueOrderSubmissionRequest request, string brokerOrderId, string brokerExecutionId, decimal quantity)
        => new(VenueExecutionEventType.TradeCaptureRecovery, VenueExecutionEventStatus.Informational, request.ClientOrderId, brokerOrderId, brokerExecutionId, request.Symbol, request.Side, quantity, 1.1m, 0m, quantity, null, "Synthetic.TradeCaptureReport", IsRecoveryEvidence: true);
}
