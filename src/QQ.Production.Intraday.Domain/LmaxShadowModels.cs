namespace QQ.Production.Intraday.Domain;

public readonly record struct LmaxShadowObservationId(Guid Value)
{
    public static LmaxShadowObservationId New() => new(Guid.NewGuid());
}

public readonly record struct LmaxShadowReplayRunId(Guid Value)
{
    public static LmaxShadowReplayRunId New() => new(Guid.NewGuid());
}

public enum LmaxShadowObservationType
{
    ExecutionReportMatchesInternalFill,
    ExecutionReportMissingInternalFill,
    InternalFillMissingInExecutionReports,
    TradeCaptureMatchesInternalFill,
    TradeCaptureMissingInternalFill,
    InternalFillMissingInTradeCapture,
    OrderStatusMatchesInternalOrder,
    OrderStatusMismatch,
    UnknownLmaxExecution,
    UnknownLmaxOrder,
    MarketDataSnapshotObserved,
    ProtocolRejectObserved,
    DuplicateExecutionObserved,
    Other
}

public enum LmaxShadowObservationSeverity
{
    Info,
    Warning,
    Blocking
}

public enum LmaxShadowObservationStatus
{
    Open,
    Acknowledged,
    Resolved,
    Ignored
}

public enum LmaxShadowReplayStatus
{
    Created,
    Running,
    Completed,
    CompletedWithWarnings,
    Failed
}

public enum LmaxShadowInputSource
{
    SyntheticFixture,
    LabEvidenceFile,
    ManualJson,
    FutureLiveShadow,
    Other
}

public sealed record LmaxShadowObservation(
    LmaxShadowObservationId Id,
    LmaxShadowReplayRunId? ReplayRunId,
    DateTimeOffset ObservedAtUtc,
    LmaxShadowObservationType Type,
    LmaxShadowObservationSeverity Severity,
    LmaxShadowObservationStatus Status,
    InstrumentId? InstrumentId,
    string? Symbol,
    string? BrokerExecutionId,
    string? BrokerOrderId,
    string? ClientOrderId,
    FillId? InternalFillId,
    ChildOrderId? InternalOrderId,
    string Description,
    string? LmaxPayloadJson,
    string? InternalPayloadJson,
    string? DifferenceJson,
    string? CorrelationId,
    DateTimeOffset CreatedAtUtc);

public sealed record LmaxShadowReplayRun(
    LmaxShadowReplayRunId Id,
    LmaxShadowInputSource InputSource,
    LmaxShadowReplayStatus Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? InputJson,
    string? OutputJson,
    int ObservationCount,
    int BlockingObservationCount,
    int WarningObservationCount,
    string? Message,
    string? CorrelationId,
    DateTimeOffset CreatedAtUtc);
