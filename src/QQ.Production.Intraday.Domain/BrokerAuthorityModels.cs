namespace QQ.Production.Intraday.Domain;

public enum BrokerSourceQuality
{
    AUTHORITATIVE,
    RECONSTRUCTED,
    MANUAL_EVIDENCE,
    STALE,
    INCOMPLETE,
    UNKNOWN
}

public enum BrokerExecutionEventType
{
    OrderAccepted,
    OrderRejected,
    Fill,
    PartialFill,
    Cancelled,
    Expired,
    PendingCancel,
    OrderStatus,
    ProtocolReject,
    Unknown
}

public enum BrokerOrderLifecycleStatus
{
    Unknown,
    PendingNew,
    Accepted,
    Rejected,
    PartiallyFilled,
    Filled,
    PendingCancel,
    Cancelled,
    Expired
}

public enum BrokerReconciliationBreakType
{
    MISSING_INTERNAL_FILL,
    MISSING_BROKER_FILL,
    DUPLICATE_EXECUTION,
    POSITION_QUANTITY_MISMATCH,
    POSITION_INSTRUMENT_MISMATCH,
    OPEN_ORDER_MISSING_INTERNAL,
    OPEN_ORDER_MISSING_BROKER,
    LEAVES_MISMATCH,
    TERMINAL_STATE_MISMATCH,
    STALE_BROKER_SNAPSHOT,
    SEQUENCE_GAP,
    UNRESOLVED_CANCEL_PENDING,
    UNKNOWN_ACCOUNT_SCOPE,
    UNACCEPTABLE_BROKER_AUTHORITY_SOURCE
}

public enum BrokerReconciliationSeverity
{
    Info,
    Warning,
    Blocking,
    Critical
}

public enum BrokerReconciliationResolutionStatus
{
    Open,
    Acknowledged,
    Resolved,
    Waived
}

public enum BrokerAuthorityReadinessDecision
{
    CAN_TRADE,
    BLOCK_NEW_ORDERS,
    EMERGENCY_STOP
}

public enum BrokerAuthorityOperationalGate
{
    GO,
    NO_GO
}

public enum BrokerAuthoritySourceRole
{
    ExecutionFeed,
    BrokerPositionSnapshot,
    BrokerOpenOrderSnapshot
}

public static class BrokerAuthoritySourcePolicy
{
    public static bool IsAcceptedQuality(BrokerAuthoritySourceRole role, BrokerSourceQuality quality)
        => role switch
        {
            BrokerAuthoritySourceRole.ExecutionFeed => quality is BrokerSourceQuality.AUTHORITATIVE or BrokerSourceQuality.RECONSTRUCTED,
            BrokerAuthoritySourceRole.BrokerPositionSnapshot => quality == BrokerSourceQuality.AUTHORITATIVE,
            BrokerAuthoritySourceRole.BrokerOpenOrderSnapshot => quality == BrokerSourceQuality.AUTHORITATIVE,
            _ => false
        };

    public static bool IsUsableFor(BrokerAuthoritySourceRole role, BrokerAuthoritySourceState source, TimeSpan maxAge, DateTimeOffset nowUtc)
        => IsAcceptedQuality(role, source.Quality) && !source.IsStale(maxAge, nowUtc);

    public static string AcceptedQualityDescription(BrokerAuthoritySourceRole role)
        => role switch
        {
            BrokerAuthoritySourceRole.ExecutionFeed => "AUTHORITATIVE or RECONSTRUCTED execution evidence",
            BrokerAuthoritySourceRole.BrokerPositionSnapshot => "AUTHORITATIVE broker position snapshot only",
            BrokerAuthoritySourceRole.BrokerOpenOrderSnapshot => "AUTHORITATIVE broker open-order snapshot only",
            _ => "unsupported broker authority source role"
        };
}

public sealed record BrokerAuthorityScope(
    string Fund,
    string Portfolio,
    string Book,
    string Strategy,
    string Environment,
    string Account,
    string Venue)
{
    public string ScopeKey => $"{Environment}|{Account}|{Venue}";
}

public sealed record BrokerAuthoritySourceState(
    string SourceName,
    BrokerSourceQuality Quality,
    DateTimeOffset AsOfUtc,
    string? SourceHash,
    string? Reason = null)
{
    public bool IsUsable(TimeSpan maxAge, DateTimeOffset nowUtc)
        => Quality is BrokerSourceQuality.AUTHORITATIVE or BrokerSourceQuality.RECONSTRUCTED
            && nowUtc - AsOfUtc <= maxAge;

    public bool IsStale(TimeSpan maxAge, DateTimeOffset nowUtc)
        => Quality == BrokerSourceQuality.STALE || nowUtc - AsOfUtc > maxAge;
}

public sealed record BrokerExecutionEvent(
    BrokerAuthorityScope Scope,
    InstrumentId? InstrumentId,
    string? Symbol,
    string? ClientOrderId,
    string? BrokerOrderId,
    string? BrokerExecutionId,
    BrokerExecutionEventType EventType,
    BrokerOrderLifecycleStatus OrderStatus,
    TradeSide? Side,
    decimal LastQuantity,
    decimal LastPrice,
    decimal LeavesQuantity,
    decimal CumulativeQuantity,
    DateTimeOffset EventTimeUtc,
    DateTimeOffset ReceivedAtUtc,
    BrokerSourceQuality SourceQuality,
    string SourceName,
    string SourceHash,
    bool PossDup = false,
    long? SequenceNumber = null,
    string? RawPayloadHash = null)
{
    public bool IsFillLike => EventType is BrokerExecutionEventType.Fill or BrokerExecutionEventType.PartialFill && LastQuantity > 0m;

    public bool IsAuthoritativeExecution
        => SourceQuality is BrokerSourceQuality.AUTHORITATIVE or BrokerSourceQuality.RECONSTRUCTED;

    public string ExecutionScopeKey => $"{Scope.ScopeKey}|{BrokerExecutionId}";

    public string ComparablePayload =>
        string.Join("|",
        [
            Scope.ScopeKey,
            InstrumentId?.Value.ToString("D") ?? "",
            Symbol?.ToUpperInvariant() ?? "",
            ClientOrderId ?? "",
            BrokerOrderId ?? "",
            BrokerExecutionId ?? "",
            EventType.ToString(),
            OrderStatus.ToString(),
            Side?.ToString() ?? "",
            LastQuantity.ToString("G29"),
            LastPrice.ToString("G29"),
            LeavesQuantity.ToString("G29"),
            CumulativeQuantity.ToString("G29"),
            EventTimeUtc.UtcDateTime.ToString("O")
        ]);
}

public sealed record BrokerOpenOrderSnapshot(
    BrokerAuthorityScope Scope,
    InstrumentId? InstrumentId,
    string? Symbol,
    string? ClientOrderId,
    string? BrokerOrderId,
    TradeSide? Side,
    decimal LeavesQuantity,
    decimal CumulativeQuantity,
    BrokerOrderLifecycleStatus Status,
    DateTimeOffset AsOfUtc,
    BrokerSourceQuality SourceQuality,
    string SourceName,
    string SourceHash)
{
    public string OrderScopeKey => $"{Scope.ScopeKey}|{ClientOrderId}|{BrokerOrderId}";
}

public sealed record BrokerPositionSnapshotEvidence(
    BrokerAuthorityScope Scope,
    BrokerPositionSnapshot Snapshot,
    string? Symbol,
    BrokerSourceQuality SourceQuality,
    string SourceName,
    string SourceHash);

public sealed record BrokerManualEvidence(
    BrokerAuthorityScope Scope,
    string EvidenceType,
    string? ClientOrderId,
    string? BrokerOrderId,
    string? BrokerExecutionId,
    InstrumentId? InstrumentId,
    string? Symbol,
    decimal? Quantity,
    decimal? Price,
    DateTimeOffset EvidenceTimeUtc,
    string SourceHash,
    string Notes);

public sealed record BrokerInternalWorkingOrderSnapshot(
    BrokerAuthorityScope Scope,
    ChildOrderId ChildOrderId,
    InstrumentId InstrumentId,
    string? Symbol,
    string ClientOrderId,
    string? BrokerOrderId,
    TradeSide Side,
    decimal LeavesQuantity,
    decimal CumulativeQuantity,
    BrokerOrderLifecycleStatus Status,
    DateTimeOffset AsOfUtc)
{
    public bool ReservesLeaves
        => Status is BrokerOrderLifecycleStatus.Accepted
            or BrokerOrderLifecycleStatus.PartiallyFilled
            or BrokerOrderLifecycleStatus.PendingCancel
            or BrokerOrderLifecycleStatus.PendingNew;
}

public sealed record BrokerTargetPosition(
    InstrumentId InstrumentId,
    string? Symbol,
    decimal TargetQuantity);

public sealed record BrokerRemainingDelta(
    InstrumentId InstrumentId,
    string? Symbol,
    decimal TargetPosition,
    decimal ReconciledCurrentPosition,
    decimal SignedReservedWorkingLeaves,
    decimal RemainingDelta);

public sealed record BrokerReconciliationBreak(
    Guid Id,
    Guid RunId,
    BrokerReconciliationBreakType Type,
    BrokerReconciliationSeverity Severity,
    bool Blocking,
    BrokerReconciliationResolutionStatus ResolutionStatus,
    BrokerAuthorityScope Scope,
    InstrumentId? InstrumentId,
    string? Symbol,
    DateTimeOffset AsOfUtc,
    IReadOnlyList<string> SourceHashes,
    IReadOnlyList<string> EvidenceRefs,
    string Description);

public sealed record BrokerReconciliationRun(
    Guid Id,
    BrokerAuthorityScope Scope,
    DateTimeOffset AsOfUtc,
    BrokerAuthoritySourceState ExecutionSource,
    BrokerAuthoritySourceState PositionSource,
    BrokerAuthoritySourceState OpenOrderSource,
    string InputHash,
    bool HasBlockingBreaks,
    bool HasCriticalBreaks);

public sealed record BrokerAuthorityReadiness(
    BrokerAuthorityOperationalGate Gate,
    BrokerAuthorityReadinessDecision Decision,
    string StatusCode,
    string Reason,
    bool PositionAuthorityReady,
    bool OpenOrderAuthorityReady,
    bool ExecutionAuthorityReady);

public sealed record BrokerReconciliationResult(
    BrokerReconciliationRun Run,
    IReadOnlyList<BrokerReconciliationBreak> Breaks,
    IReadOnlyList<BrokerRemainingDelta> RemainingDeltas,
    BrokerAuthorityReadiness Readiness);

