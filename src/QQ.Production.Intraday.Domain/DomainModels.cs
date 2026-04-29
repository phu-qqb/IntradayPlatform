namespace QQ.Production.Intraday.Domain;

public readonly record struct FundId(Guid Value)
{
    public static FundId New() => new(Guid.NewGuid());
}

public readonly record struct BrokerAccountId(Guid Value)
{
    public static BrokerAccountId New() => new(Guid.NewGuid());
}

public readonly record struct InstrumentId(Guid Value)
{
    public static InstrumentId New() => new(Guid.NewGuid());
}

public readonly record struct VenueId(Guid Value)
{
    public static VenueId New() => new(Guid.NewGuid());
}

public readonly record struct VenueInstrumentId(Guid Value)
{
    public static VenueInstrumentId New() => new(Guid.NewGuid());
}

public readonly record struct ModelRunId(Guid Value)
{
    public static ModelRunId New() => new(Guid.NewGuid());
}

public readonly record struct TradeIntentId(Guid Value)
{
    public static TradeIntentId New() => new(Guid.NewGuid());
}

public readonly record struct ParentOrderId(Guid Value)
{
    public static ParentOrderId New() => new(Guid.NewGuid());
}

public readonly record struct ChildOrderId(Guid Value)
{
    public static ChildOrderId New() => new(Guid.NewGuid());
}

public readonly record struct ExecutionReportId(Guid Value)
{
    public static ExecutionReportId New() => new(Guid.NewGuid());
}

public readonly record struct FillId(Guid Value)
{
    public static FillId New() => new(Guid.NewGuid());
}

public readonly record struct ClientOrderId(string Value)
{
    public override string ToString() => Value;
}

public sealed record Currency(string Code)
{
    public static Currency Usd { get; } = new("USD");
    public static Currency Eur { get; } = new("EUR");
}

public sealed record Fund(FundId Id, string Name, Currency BaseCurrency, bool IsEnabled = true);
public sealed record BrokerAccount(BrokerAccountId Id, FundId FundId, string AccountCode, bool IsEnabled = true);
public sealed record NavSnapshot(FundId FundId, decimal NavUsd, NavSource Source, DateTimeOffset AsOfUtc);

public enum NavSource { Manual, ModelRun, Seed }
public enum AssetClass { FxSpot }
public enum InstrumentStatus { Enabled, Disabled }
public enum VenueType { Broker, Exchange, Simulator }
public enum VenueStatus { Enabled, Disabled }

public sealed record Instrument(
    InstrumentId Id,
    string Symbol,
    AssetClass AssetClass,
    Currency BaseCurrency,
    Currency QuoteCurrency,
    int PricePrecision,
    int QuantityPrecision,
    bool IsEnabled = true);

public sealed record Venue(VenueId Id, string Name, VenueType VenueType, bool IsEnabled = true);

public sealed record VenueInstrumentMapping(
    VenueInstrumentId Id,
    VenueId VenueId,
    InstrumentId InstrumentId,
    string VenueSymbol,
    string VenueInstrumentCode,
    decimal ContractSize,
    decimal MinOrderQuantity,
    decimal QuantityStep,
    decimal PriceTickSize,
    bool IsEnabled = true);

public enum ModelRunStatus { Received, Processing, Processed, Blocked, Failed }
public enum TargetQuantityMode { PortfolioBaseCurrencyNotional, FxBaseCurrencyQuantity }

public sealed record ModelRun(
    ModelRunId Id,
    FundId FundId,
    string ModelName,
    DateTimeOffset AsOfUtc,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset EffectiveAtUtc,
    int FrequencyMinutes,
    decimal NavUsd,
    ModelRunStatus Status,
    string InputHash,
    string SourceFileName,
    bool IsProcessed,
    TargetQuantityMode TargetQuantityMode = TargetQuantityMode.PortfolioBaseCurrencyNotional);

public sealed record TargetWeight(ModelRunId ModelRunId, InstrumentId InstrumentId, decimal Weight, string RawSecurityId);

public sealed record TargetPosition(
    ModelRunId ModelRunId,
    InstrumentId InstrumentId,
    decimal TargetNotionalUsd,
    decimal TargetBaseQuantity,
    decimal TargetVenueQuantity,
    TargetQuantityMode TargetQuantityMode);

public sealed record MarketDataSnapshot(
    InstrumentId InstrumentId,
    VenueId VenueId,
    decimal Bid,
    decimal Ask,
    decimal? ExplicitMid,
    DateTimeOffset ReceivedAtUtc)
{
    public decimal Mid => ExplicitMid ?? (Bid + Ask) / 2m;

    public void Validate()
    {
        if (Bid <= 0 || Ask <= 0 || Ask < Bid)
        {
            throw new DomainRuleViolationException("Market data bid/ask are invalid.");
        }

        if (Mid <= 0)
        {
            throw new DomainRuleViolationException("Market data mid is invalid.");
        }
    }

    public bool IsStale(TimeSpan maxAge, DateTimeOffset now) => now - ReceivedAtUtc > maxAge;
}

public sealed record InternalPositionSnapshot(FundId FundId, InstrumentId InstrumentId, decimal BaseQuantity, DateTimeOffset AsOfUtc);
public sealed record BrokerPositionSnapshot(BrokerAccountId BrokerAccountId, InstrumentId InstrumentId, decimal BaseQuantity, DateTimeOffset AsOfUtc);
public enum PositionLedgerEventType { StartOfDay, Fill, ManualCorrection }
public sealed record PositionLedgerEvent(Guid Id, FundId FundId, InstrumentId InstrumentId, PositionLedgerEventType Type, decimal BaseQuantityDelta, string ReferenceId, DateTimeOffset CreatedAtUtc);

public enum ReconciliationBreakType
{
    InternalBrokerPositionMismatch,
    BrokerFillMissingInternally,
    InternalFillMissingInBrokerReport,
    OrderExpectedButNotSent,
    OrderSentButNoBrokerAck,
    OrderAckedButNoFill,
    ParentOrderNoFill,
    ChildOrderNoFill,
    QuantityMismatch,
    PriceMismatch,
    UnknownBrokerExecution,
    CommissionOrFeeMismatch
}

public enum ReconciliationBreakSeverity { Info, Warning, Blocking }
public enum ReconciliationBreakStatus { Open, Resolved }
public enum ReconciliationPhase { PreTrade, PostTrade, EndOfDay }
public sealed record ReconciliationRun(Guid Id, ModelRunId ModelRunId, ReconciliationPhase Phase, DateTimeOffset CreatedAtUtc, bool HasBlockingBreaks);
public sealed record ReconciliationBreak(Guid Id, Guid ReconciliationRunId, ReconciliationBreakType Type, ReconciliationBreakSeverity Severity, ReconciliationBreakStatus Status, InstrumentId? InstrumentId, string Description);

public sealed record DriftSnapshot(ModelRunId ModelRunId, InstrumentId InstrumentId, decimal TargetBaseQuantity, decimal CurrentBaseQuantity, decimal DriftBaseQuantity, decimal TargetVenueQuantity, decimal CurrentVenueQuantity, decimal DriftVenueQuantity);

public enum TradeIntentStatus { Created, RiskApproved, RiskRejected, Ordered, Cancelled }
public enum TradeSide { Buy, Sell }
public sealed record TradeIntent(TradeIntentId Id, ModelRunId ModelRunId, FundId FundId, InstrumentId InstrumentId, TradeSide Side, decimal RequestedBaseQuantity, decimal RequestedVenueQuantity, string Reason, TradeIntentStatus Status, DateTimeOffset CreatedAtUtc);

public sealed record RiskLimitSet(Guid Id, FundId FundId, bool GlobalTradingEnabled, decimal MaxGrossExposureUsd, TimeSpan MaxModelRunAge, TimeSpan MaxMarketDataAge, decimal PositionToleranceBaseQuantity, decimal MinDriftVenueQuantity);
public sealed record RiskLimit(Guid Id, Guid RiskLimitSetId, string Name, decimal Value);
public sealed record InstrumentRiskLimit(Guid Id, Guid RiskLimitSetId, InstrumentId InstrumentId, decimal MaxTradeNotionalUsd, decimal MaxExposureUsd, bool IsEnabled = true);
public sealed record VenueRiskLimit(Guid Id, Guid RiskLimitSetId, VenueId VenueId, decimal MaxTradeNotionalUsd, bool IsEnabled = true);
public sealed record TradingWindow(Guid Id, FundId FundId, string ModelName, string TimeZoneId, DayOfWeek DayOfWeek, TimeOnly OpensAtUtc, TimeOnly ClosesAtUtc, TimeOnly NoNewOrdersAfterUtc, TimeOnly? FlattenAtUtc, bool IsEnabled = true, bool TradingEnabled = true);
public sealed record KillSwitchState(Guid Id, bool IsActive, string? Reason, DateTimeOffset UpdatedAtUtc);

public enum RiskDecisionStatus { Approved, Rejected, Blocked, RequiresManualApproval }
public enum RiskRejectReason
{
    None,
    GlobalTradingDisabled,
    KillSwitchActive,
    FundDisabled,
    VenueDisabled,
    InstrumentDisabled,
    UnknownCurrentPosition,
    PositionMismatch,
    StaleModelRun,
    StaleMarketData,
    InvalidQuantity,
    MaxTradeNotionalExceeded,
    MaxInstrumentExposureExceeded,
    MaxGrossExposureExceeded,
    TradingWindowClosed
}

public sealed record RiskDecision(Guid Id, TradeIntentId TradeIntentId, RiskDecisionStatus Status, RiskRejectReason RejectReason, string Explanation, DateTimeOffset CreatedAtUtc);

public enum OrderStatus { Created, RiskRejected, PendingNew, Acked, PartiallyFilled, Filled, PendingCancel, Cancelled, Rejected, Expired, Unknown }
public enum OrderSide { Buy, Sell }
public enum OrderType { Market, Limit }
public enum TimeInForce { IOC, FOK, GFD, GTC }
public enum ExecutionAlgo { MarketImmediate }

public sealed record ParentOrder(ParentOrderId Id, TradeIntentId TradeIntentId, ClientOrderId ClientOrderId, OrderSide Side, decimal BaseQuantity, ExecutionAlgo Algo, OrderStatus Status, DateTimeOffset CreatedAtUtc);
public sealed record ChildOrder(ChildOrderId Id, ParentOrderId ParentOrderId, VenueId VenueId, ClientOrderId ClientOrderId, OrderSide Side, OrderType OrderType, TimeInForce TimeInForce, decimal BaseQuantity, decimal VenueQuantity, OrderStatus Status, DateTimeOffset CreatedAtUtc);

public enum ExecutionReportType { OrderAck, OrderReject, Fill, PartialFill, CancelAck, CancelReject, Expired, Unknown }
public sealed record ExecutionReport(ExecutionReportId Id, ChildOrderId ChildOrderId, VenueId VenueId, string BrokerOrderId, string? BrokerExecutionId, ClientOrderId ClientOrderId, ExecutionReportType ExecutionReportType, decimal LastQuantity, decimal LastPrice, decimal LeavesQuantity, decimal CumulativeQuantity, decimal AveragePrice, DateTimeOffset ReceivedAtUtc);

public sealed record Fill(FillId Id, string BrokerExecutionId, ChildOrderId ChildOrderId, InstrumentId InstrumentId, VenueId VenueId, TradeSide Side, decimal BaseQuantity, decimal VenueQuantity, decimal Price, DateTimeOffset TradeDateUtc, DateTimeOffset ReceivedAtUtc);

public sealed class DomainRuleViolationException(string message) : InvalidOperationException(message);

public static class QuantityRounding
{
    public static decimal RoundToStep(decimal quantity, decimal step)
    {
        if (step <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(step), "Quantity step must be positive.");
        }

        if (quantity == 0)
        {
            return 0;
        }

        var sign = Math.Sign(quantity);
        var absolute = Math.Abs(quantity);
        var rounded = Math.Round(absolute / step, 0, MidpointRounding.AwayFromZero) * step;
        return rounded * sign;
    }
}

public sealed class TargetPositionCalculator
{
    public TargetPosition Calculate(ModelRun run, TargetWeight weight, MarketDataSnapshot marketData, VenueInstrumentMapping mapping)
    {
        marketData.Validate();

        var targetNotional = run.TargetQuantityMode == TargetQuantityMode.PortfolioBaseCurrencyNotional
            ? weight.Weight * run.NavUsd
            : weight.Weight * run.NavUsd * marketData.Mid;

        var targetBase = run.TargetQuantityMode == TargetQuantityMode.PortfolioBaseCurrencyNotional
            ? targetNotional / marketData.Mid
            : weight.Weight * run.NavUsd;

        var roundedVenue = QuantityRounding.RoundToStep(targetBase / mapping.ContractSize, mapping.QuantityStep);
        var roundedBase = roundedVenue * mapping.ContractSize;

        if (roundedVenue != 0 && Math.Abs(roundedVenue) < mapping.MinOrderQuantity)
        {
            throw new DomainRuleViolationException("Rounded target venue quantity is below minimum order quantity.");
        }

        return new TargetPosition(run.Id, weight.InstrumentId, targetNotional, roundedBase, roundedVenue, run.TargetQuantityMode);
    }
}

public sealed class OrderStateMachine
{
    public OrderStatus Transition(OrderStatus current, ExecutionReportType reportType)
    {
        var next = (current, reportType) switch
        {
            (OrderStatus.Created, ExecutionReportType.OrderAck) => OrderStatus.PendingNew,
            (OrderStatus.PendingNew, ExecutionReportType.OrderAck) => OrderStatus.Acked,
            (OrderStatus.PendingNew, ExecutionReportType.OrderReject) => OrderStatus.Rejected,
            (OrderStatus.Acked, ExecutionReportType.PartialFill) => OrderStatus.PartiallyFilled,
            (OrderStatus.Acked, ExecutionReportType.Fill) => OrderStatus.Filled,
            (OrderStatus.PartiallyFilled, ExecutionReportType.Fill) => OrderStatus.Filled,
            (OrderStatus.PartiallyFilled, ExecutionReportType.Expired) => OrderStatus.Expired,
            (OrderStatus.Acked, ExecutionReportType.CancelAck) => OrderStatus.PendingCancel,
            (OrderStatus.PendingCancel, ExecutionReportType.CancelAck) => OrderStatus.Cancelled,
            (OrderStatus.Acked, ExecutionReportType.Expired) => OrderStatus.Expired,
            _ => throw new DomainRuleViolationException($"Invalid order transition from {current} using {reportType}.")
        };

        return next;
    }
}
