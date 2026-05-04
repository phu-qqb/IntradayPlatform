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

public readonly record struct MarketDataSnapshotId(Guid Value)
{
    public static MarketDataSnapshotId New() => new(Guid.NewGuid());
}

public readonly record struct MarketDataBarId(Guid Value)
{
    public static MarketDataBarId New() => new(Guid.NewGuid());
}

public readonly record struct BarBuildRunId(Guid Value)
{
    public static BarBuildRunId New() => new(Guid.NewGuid());
}

public readonly record struct ModelWeightBatchId(Guid Value)
{
    public static ModelWeightBatchId New() => new(Guid.NewGuid());
}

public readonly record struct ModelWeightRowId(Guid Value)
{
    public static ModelWeightRowId New() => new(Guid.NewGuid());
}

public readonly record struct InstrumentAliasId(Guid Value)
{
    public static InstrumentAliasId New() => new(Guid.NewGuid());
}

public readonly record struct LmaxReportImportRunId(Guid Value)
{
    public static LmaxReportImportRunId New() => new(Guid.NewGuid());
}

public readonly record struct LmaxIndividualTradeId(Guid Value)
{
    public static LmaxIndividualTradeId New() => new(Guid.NewGuid());
}

public readonly record struct LmaxTradeSummaryId(Guid Value)
{
    public static LmaxTradeSummaryId New() => new(Guid.NewGuid());
}

public readonly record struct LmaxCurrencyWalletId(Guid Value)
{
    public static LmaxCurrencyWalletId New() => new(Guid.NewGuid());
}

public readonly record struct OperatorAuditEventId(Guid Value)
{
    public static OperatorAuditEventId New() => new(Guid.NewGuid());
}

public readonly record struct ExceptionCaseId(Guid Value)
{
    public static ExceptionCaseId New() => new(Guid.NewGuid());
}

public readonly record struct ExceptionCaseActionId(Guid Value)
{
    public static ExceptionCaseActionId New() => new(Guid.NewGuid());
}

public readonly record struct ExceptionCaseNoteId(Guid Value)
{
    public static ExceptionCaseNoteId New() => new(Guid.NewGuid());
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
public sealed record BrokerAccount(BrokerAccountId Id, FundId FundId, string AccountCode, bool IsEnabled = true, string? ExternalAccountId = null);
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
    bool IsEnabled = true,
    bool IsTradingEnabled = true,
    bool IsReportImportEnabled = true,
    bool IsMarketDataEnabled = true);

public sealed record Venue(VenueId Id, string Name, VenueType VenueType, bool IsEnabled = true, bool IsTradingEnabled = true, bool IsReportImportEnabled = true, bool IsMarketDataEnabled = true);

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

public sealed record InstrumentAlias(
    InstrumentAliasId Id,
    InstrumentId InstrumentId,
    string Source,
    string ExternalSymbol,
    string? ExternalInstrumentId,
    bool IsEnabled,
    DateTimeOffset CreatedAtUtc);

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

public enum ModelWeightBatchStatus { Draft, Ready, Validating, Accepted, Rejected, Promoted }
public enum ModelWeightSourceSystem { Fake, Qubes, GeneticAlgorithm, Manual, Other }
public enum ModelWeightValidationIssueType
{
    MissingBatch,
    BatchNotReady,
    InvalidFund,
    InvalidModelName,
    InvalidTimestamp,
    InvalidNav,
    InvalidFrequency,
    InvalidTargetQuantityMode,
    MissingRows,
    RowCountMismatch,
    DuplicateSecurity,
    UnknownInstrument,
    DisabledInstrument,
    InvalidWeight,
    DuplicateExternalBatchId,
    ConflictingExternalBatch,
    AlreadyPromoted,
    ReferenceDataInvalid,
    Other
}

public enum ModelWeightValidationSeverity { Info, Warning, Blocking }

public sealed record ModelWeightBatch(
    ModelWeightBatchId Id,
    string ExternalBatchId,
    ModelWeightSourceSystem SourceSystem,
    string FundCode,
    FundId? FundId,
    string ModelName,
    DateTimeOffset AsOfUtc,
    DateTimeOffset EffectiveAtUtc,
    int FrequencyMinutes,
    decimal NavUsd,
    TargetQuantityMode TargetQuantityMode,
    ModelWeightBatchStatus Status,
    int? ExpectedRowCount,
    string? ContentHash,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReadyAtUtc,
    DateTimeOffset? AcceptedAtUtc,
    DateTimeOffset? PromotedAtUtc,
    DateTimeOffset? RejectedAtUtc,
    ModelRunId? PromotedModelRunId,
    string? Message);

public sealed record ModelWeightRow(
    ModelWeightRowId Id,
    ModelWeightBatchId BatchId,
    string RawSecurityId,
    string Symbol,
    InstrumentId? InstrumentId,
    decimal Weight,
    DateTimeOffset CreatedAtUtc);

public sealed record ModelWeightValidationIssue(
    Guid Id,
    ModelWeightBatchId BatchId,
    ModelWeightValidationIssueType IssueType,
    ModelWeightValidationSeverity Severity,
    string Message,
    ModelWeightRowId? RowId,
    int? RowNumber,
    DateTimeOffset CreatedAtUtc);

public enum LmaxReportType { IndividualTrades, TradesSummary, CurrencyWallets, ReportSet }
public enum LmaxReportImportStatus { Created, Validating, Imported, Rejected, Failed, Archived }
public enum LmaxReportValidationSeverity { Info, Warning, Blocking }
public enum LmaxReportValidationIssueType
{
    MissingFile,
    InvalidHeader,
    InvalidRow,
    InvalidTimestamp,
    InvalidDate,
    InvalidSymbol,
    UnknownInstrument,
    DisabledInstrument,
    DuplicateExecutionId,
    DuplicateTradeUti,
    DuplicateSummaryRow,
    DuplicateCurrencyWallet,
    InvalidQuantity,
    InvalidPrice,
    InvalidCommission,
    InvalidNotional,
    InvalidCurrency,
    InvalidRateToBaseCcy,
    WalletBalanceMismatch,
    AccountIdMismatch,
    ReportDateMismatch,
    ReferenceDataInvalid,
    SummaryMismatch,
    Other
}

public enum LmaxEodMutationMode
{
    None,
    DropOneExecution,
    AddUnknownExecution,
    ChangeExecutionQuantity,
    ChangeExecutionPrice,
    ChangeExecutionSide,
    DropOneSummaryRow,
    ChangeSummaryCommission,
    ChangeSummaryNotional,
    ChangeWalletBalance,
    ChangeWalletRate,
    DropCurrencyWallet
}

public sealed record LmaxReportImportRun(
    LmaxReportImportRunId Id,
    LmaxReportType ReportType,
    DateOnly ReportDate,
    VenueId VenueId,
    BrokerAccountId BrokerAccountId,
    LmaxReportImportStatus Status,
    string? FileName,
    string? FilePath,
    string? FileHash,
    int? RowCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? ArchivedPath,
    string? RejectedPath,
    string? Message);

public sealed record LmaxReportValidationIssue(
    Guid Id,
    LmaxReportImportRunId ImportRunId,
    LmaxReportValidationIssueType IssueType,
    LmaxReportValidationSeverity Severity,
    string Message,
    int? RowNumber,
    string? RawLine,
    DateTimeOffset CreatedAtUtc);

public sealed record LmaxIndividualTrade(
    LmaxIndividualTradeId Id,
    LmaxReportImportRunId ImportRunId,
    DateOnly ReportDate,
    VenueId VenueId,
    BrokerAccountId BrokerAccountId,
    string ExecutionId,
    string? MtfExecutionId,
    DateTimeOffset TimestampUtc,
    decimal TradeQuantity,
    decimal TradePrice,
    DateOnly TradeDate,
    string? LmaxInstrumentId,
    string LmaxSymbol,
    InstrumentId? InstrumentId,
    string? InstructionId,
    string? OrderId,
    decimal? StopPrice,
    decimal? LimitPrice,
    DateTimeOffset? OrderPlacementTimestampUtc,
    string OrderType,
    string? RemoteVenue,
    string? UserPlacingOrder,
    decimal? TotalProfitLoss,
    decimal TotalCommission,
    string AccountId,
    decimal UnitsBoughtSold,
    decimal NotionalValue,
    string TradeUti,
    string? RawLine,
    DateTimeOffset CreatedAtUtc);

public sealed record LmaxTradeSummary(
    LmaxTradeSummaryId Id,
    LmaxReportImportRunId ImportRunId,
    DateOnly ReportDate,
    VenueId VenueId,
    BrokerAccountId BrokerAccountId,
    DateTimeOffset DateTimeUtc,
    string Instrument,
    InstrumentId? InstrumentId,
    string Type,
    string Currency,
    decimal Contracts,
    decimal AveragePrice,
    decimal CommissionRounded,
    decimal NotionalValue,
    string LmaxSymbol,
    string? UserPlacingOrder,
    decimal CommissionFullPrecision,
    string AccountId,
    string? RawLine,
    DateTimeOffset CreatedAtUtc);

public sealed record LmaxCurrencyWallet(
    LmaxCurrencyWalletId Id,
    LmaxReportImportRunId ImportRunId,
    DateOnly ReportDate,
    VenueId VenueId,
    BrokerAccountId BrokerAccountId,
    string Currency,
    decimal BalanceNetDeposits,
    decimal Adjustments,
    decimal InterAccountTransfers,
    decimal ProfitLoss,
    decimal Commission,
    decimal Dividends,
    decimal Financing,
    decimal WalletBalance,
    decimal RateToBaseCcy,
    string BaseCurrency,
    decimal BalanceNetDepositsBaseUsd,
    decimal AdjustmentsBaseUsd,
    decimal InterAccountTransfersBaseUsd,
    decimal ProfitLossBaseUsd,
    decimal CommissionBaseUsd,
    decimal DividendsBaseUsd,
    decimal FinancingBaseUsd,
    decimal WalletBalanceBaseUsd,
    string AccountId,
    string? RawLine,
    DateTimeOffset CreatedAtUtc);

public sealed record EodReconciliationRun(Guid Id, DateOnly ReportDate, VenueId VenueId, BrokerAccountId BrokerAccountId, DateTimeOffset CreatedAtUtc, bool HasBlockingBreaks);
public sealed record EodReconciliationBreak(Guid Id, Guid RunId, ReconciliationBreakType Type, ReconciliationBreakSeverity Severity, ReconciliationBreakStatus Status, InstrumentId? InstrumentId, string Description, string? BrokerExecutionId, string? InternalFillId, DateTimeOffset CreatedAtUtc);

public enum ExceptionCaseStatus { Open, Acknowledged, Investigating, Resolved, FalsePositive, Waived, Closed }
public enum ExceptionCaseSeverity { Info, Warning, Blocking, Critical }
public enum ExceptionCaseType
{
    PositionMismatch,
    InternalFillMissingInBrokerReport,
    BrokerFillMissingInternally,
    QuantityMismatch,
    PriceMismatch,
    SideMismatch,
    InstrumentMismatch,
    ReferenceDataIssue,
    RiskBlock,
    StaleMarketData,
    StaleModelRun,
    EodBreak,
    IntradayBreak,
    SystemHealth,
    Other
}

public enum ExceptionCaseSource { IntradayReconciliation, EodReconciliation, RiskEngine, ReferenceDataIntegrity, SystemHealth, Operator, Other }
public enum ExceptionCaseActionType { Created, Acknowledged, Assigned, MarkedInvestigating, Resolved, MarkedFalsePositive, Waived, Reopened, NoteAdded }

public sealed record ExceptionCase(
    ExceptionCaseId Id,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    ExceptionCaseStatus Status,
    ExceptionCaseSeverity Severity,
    ExceptionCaseType Type,
    ExceptionCaseSource Source,
    string Title,
    string Description,
    string? EntityType,
    string? EntityId,
    InstrumentId? InstrumentId,
    string? Symbol,
    string? CorrelationId,
    string? AssignedTo,
    DateTimeOffset? AcknowledgedAtUtc,
    string? AcknowledgedBy,
    DateTimeOffset? ResolvedAtUtc,
    string? ResolvedBy,
    string? ResolutionReason,
    string? WaiverReason,
    string? MetadataJson);

public sealed record ExceptionCaseAction(
    ExceptionCaseActionId Id,
    ExceptionCaseId CaseId,
    ExceptionCaseActionType ActionType,
    string ActorId,
    string ActorDisplayName,
    DateTimeOffset OccurredAtUtc,
    ExceptionCaseStatus? FromStatus,
    ExceptionCaseStatus? ToStatus,
    string? Reason,
    string? Note,
    string? MetadataJson,
    string? CorrelationId);

public sealed record ExceptionCaseNote(
    ExceptionCaseNoteId Id,
    ExceptionCaseId CaseId,
    DateTimeOffset CreatedAtUtc,
    string CreatedBy,
    string Note,
    string? CorrelationId);

public sealed record ExceptionCaseLink(
    Guid Id,
    ExceptionCaseId CaseId,
    string SourceEntityType,
    string SourceEntityId,
    DateTimeOffset CreatedAtUtc);

public enum OperatorAuditEventType
{
    ModelWeightBatchCreated,
    ModelWeightBatchValidated,
    ModelWeightBatchPromoted,
    ModelRunCreated,
    ModelRunProcessed,
    ModelRunBlocked,
    OrderCreated,
    OrderFilled,
    KillSwitchActivated,
    KillSwitchCleared,
    ReferenceDataIntegrityChecked,
    EodReportGenerated,
    EodReportImported,
    EodReconciliationRun,
    EodBreakCreated,
    PnlSummaryCalculated,
    LmaxLabCommandRun,
    SafetyStartupValidation,
    ExceptionCaseCreated,
    ExceptionCaseAcknowledged,
    ExceptionCaseAssigned,
    ExceptionCaseInvestigating,
    ExceptionCaseResolved,
    ExceptionCaseFalsePositive,
    ExceptionCaseWaived,
    ExceptionCaseReopened,
    ExceptionCaseNoteAdded,
    RiskLimitSetCreated,
    RiskLimitSetCloned,
    RiskLimitSetActivated,
    RiskLimitSetRetired,
    RiskLimitUpdated,
    InstrumentRiskLimitUpdated,
    VenueRiskLimitUpdated,
    TradingWindowUpdated,
    InstrumentControlUpdated,
    VenueControlUpdated,
    Unknown
}

public enum OperatorAuditSeverity { Info, Warning, Critical }
public enum OperatorAuditActorType { System, Operator, Worker, Api, ConnectivityLab, Unknown }
public enum OperatorAuditResult { Started, Succeeded, Failed, Blocked, NoActionRequired }

public sealed record OperatorAuditEvent(
    OperatorAuditEventId Id,
    DateTimeOffset OccurredAtUtc,
    OperatorAuditActorType ActorType,
    string ActorId,
    string ActorDisplayName,
    OperatorAuditEventType EventType,
    OperatorAuditSeverity Severity,
    OperatorAuditResult Result,
    string? EntityType,
    string? EntityId,
    string? CorrelationId,
    string? CausationId,
    string? RequestId,
    string Source,
    string Description,
    string? Reason,
    string? BeforeJson,
    string? AfterJson,
    string? MetadataJson);

public sealed record EodPnlCurrencyRow(
    string Currency,
    decimal WalletBalance,
    decimal RateToBaseCcy,
    decimal WalletBalanceBaseUsd,
    decimal ProfitLoss,
    decimal ProfitLossBaseUsd,
    decimal Commission,
    decimal CommissionBaseUsd,
    decimal Dividends,
    decimal DividendsBaseUsd,
    decimal Financing,
    decimal FinancingBaseUsd);

public sealed record EodPnlSummary(
    DateOnly ReportDate,
    string VenueName,
    string BrokerAccountCode,
    decimal TotalWalletBalanceUsd,
    decimal TotalProfitLossUsd,
    decimal TotalCommissionUsd,
    decimal TotalDividendsUsd,
    decimal TotalFinancingUsd,
    decimal TotalNetPnlUsd,
    IReadOnlyList<EodPnlCurrencyRow> CurrencyRows);

public sealed record TargetPosition(
    ModelRunId ModelRunId,
    InstrumentId InstrumentId,
    decimal TargetNotionalUsd,
    decimal TargetBaseQuantity,
    decimal TargetVenueQuantity,
    TargetQuantityMode TargetQuantityMode);

public sealed record MarketDataSnapshot(
    MarketDataSnapshotId Id,
    InstrumentId InstrumentId,
    VenueId VenueId,
    decimal Bid,
    decimal Ask,
    decimal? ExplicitMid,
    string Source,
    DateTimeOffset SourceTimestampUtc,
    DateTimeOffset ReceivedAtUtc)
{
    public decimal Mid => ExplicitMid ?? (Bid + Ask) / 2m;
    public decimal Spread => Ask - Bid;
    public long? SequenceNumber { get; init; }
    public bool IsSynthetic { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = ReceivedAtUtc;

    public MarketDataSnapshot(
        InstrumentId instrumentId,
        VenueId venueId,
        decimal bid,
        decimal ask,
        decimal? explicitMid,
        DateTimeOffset receivedAtUtc)
        : this(MarketDataSnapshotId.New(), instrumentId, venueId, bid, ask, explicitMid, "Seed", receivedAtUtc, receivedAtUtc)
    {
        IsSynthetic = true;
    }

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

public enum BarTimeframe { OneMinute, FifteenMinutes, OneHour, OneDay }
public enum BarQualityStatus { Complete, Incomplete, NoData, SparseData, StaleData, OutlierDetected, ManuallyCorrected }
public enum BarBuildRunStatus { Started, Completed, Failed }

public sealed record MarketDataBar(
    MarketDataBarId Id,
    InstrumentId InstrumentId,
    VenueId VenueId,
    BarTimeframe Timeframe,
    DateTimeOffset BarStartUtc,
    DateTimeOffset BarEndUtc,
    string Source,
    decimal BidOpen,
    decimal BidHigh,
    decimal BidLow,
    decimal BidClose,
    decimal AskOpen,
    decimal AskHigh,
    decimal AskLow,
    decimal AskClose,
    decimal MidOpen,
    decimal MidHigh,
    decimal MidLow,
    decimal MidClose,
    decimal SpreadOpen,
    decimal SpreadHigh,
    decimal SpreadLow,
    decimal SpreadClose,
    decimal SpreadAverage,
    int ObservationCount,
    DateTimeOffset? FirstSnapshotUtc,
    DateTimeOffset? LastSnapshotUtc,
    bool IsComplete,
    BarQualityStatus QualityStatus,
    BarBuildRunId? BuildRunId,
    string BuilderVersion,
    DateTimeOffset CreatedAtUtc)
{
    public void Validate()
    {
        if (BarStartUtc.Offset != TimeSpan.Zero || BarEndUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleViolationException("Market data bars must use UTC timestamps.");
        }

        if (BarEndUtc <= BarStartUtc)
        {
            throw new DomainRuleViolationException("BarEndUtc must be greater than BarStartUtc.");
        }
    }
}

public sealed record BarBuildRun(
    BarBuildRunId Id,
    BarTimeframe Timeframe,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string Source,
    string BuilderVersion,
    BarBuildRunStatus Status,
    string? ErrorMessage,
    int BarsCreated,
    int BarsUpdated);

public static class BarIntervalAlignment
{
    public static TimeSpan Duration(BarTimeframe timeframe) => timeframe switch
    {
        BarTimeframe.OneMinute => TimeSpan.FromMinutes(1),
        BarTimeframe.FifteenMinutes => TimeSpan.FromMinutes(15),
        BarTimeframe.OneHour => TimeSpan.FromHours(1),
        BarTimeframe.OneDay => TimeSpan.FromDays(1),
        _ => throw new ArgumentOutOfRangeException(nameof(timeframe), timeframe, "Unsupported timeframe.")
    };

    public static DateTimeOffset GetBarStart(DateTimeOffset timestampUtc, BarTimeframe timeframe)
    {
        EnsureUtc(timestampUtc);
        var durationTicks = Duration(timeframe).Ticks;
        var ticks = timestampUtc.UtcDateTime.Ticks - timestampUtc.UtcDateTime.Ticks % durationTicks;
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }

    public static IReadOnlyList<(DateTimeOffset StartUtc, DateTimeOffset EndUtc)> EnumerateIntervals(DateTimeOffset startUtc, DateTimeOffset endUtc, BarTimeframe timeframe)
    {
        EnsureUtc(startUtc);
        EnsureUtc(endUtc);
        if (endUtc <= startUtc)
        {
            return [];
        }

        var alignedStart = GetBarStart(startUtc, timeframe);
        var duration = Duration(timeframe);
        var intervals = new List<(DateTimeOffset StartUtc, DateTimeOffset EndUtc)>();
        for (var cursor = alignedStart; cursor < endUtc; cursor = cursor.Add(duration))
        {
            intervals.Add((cursor, cursor.Add(duration)));
        }

        return intervals;
    }

    private static void EnsureUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleViolationException("Bar timestamps must be UTC.");
        }
    }
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
    SideMismatch,
    InstrumentMismatch,
    ClientOrderIdMismatch,
    BrokerOrderIdMismatch,
    CommissionMismatch,
    NotionalMismatch,
    PositionDeltaMismatch,
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

public enum RiskLimitSetStatus { Draft, Active, Retired, Archived }

public sealed record RiskLimitSet(
    Guid Id,
    FundId FundId,
    bool GlobalTradingEnabled,
    decimal MaxGrossExposureUsd,
    TimeSpan MaxModelRunAge,
    TimeSpan MaxMarketDataAge,
    decimal PositionToleranceBaseQuantity,
    decimal MinDriftVenueQuantity,
    string? ModelName = "IntradayFxModel",
    string Name = "Default Conservative Intraday Risk",
    int Version = 1,
    RiskLimitSetStatus Status = RiskLimitSetStatus.Active,
    bool IsActive = true,
    DateTimeOffset? EffectiveFromUtc = null,
    DateTimeOffset? EffectiveToUtc = null,
    DateTimeOffset? CreatedAtUtc = null,
    string? CreatedBy = "seed",
    DateTimeOffset? ActivatedAtUtc = null,
    string? ActivatedBy = "seed",
    DateTimeOffset? RetiredAtUtc = null,
    string? RetiredBy = null,
    string? Description = "Seeded conservative local-only risk profile.");

public sealed record RiskLimit(Guid Id, Guid RiskLimitSetId, string Name, decimal Value, string Unit = "decimal", string Scope = "Global", bool IsEnabled = true);
public sealed record InstrumentRiskLimit(Guid Id, Guid RiskLimitSetId, InstrumentId InstrumentId, decimal MaxTradeNotionalUsd, decimal MaxExposureUsd, bool IsEnabled = true, decimal MinTradeQuantity = 0m, int MaxOrdersPerDay = 100, bool IsTradingEnabled = true);
public sealed record VenueRiskLimit(Guid Id, Guid RiskLimitSetId, VenueId VenueId, decimal MaxTradeNotionalUsd, bool IsEnabled = true, decimal MaxDailyTurnoverUsd = 1_000_000m, int MaxOrdersPerMinute = 10, bool IsVenueEnabled = true);
public sealed record TradingWindow(Guid Id, FundId FundId, string ModelName, string TimeZoneId, DayOfWeek DayOfWeek, TimeOnly OpensAtUtc, TimeOnly ClosesAtUtc, TimeOnly NoNewOrdersAfterUtc, TimeOnly? FlattenAtUtc, bool IsEnabled = true, bool TradingEnabled = true, string ScheduleName = "Default Intraday", int Version = 1, DateTimeOffset? CreatedAtUtc = null, DateTimeOffset? UpdatedAtUtc = null);
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
    TradingWindowClosed,
    NoNewOrdersAfter,
    RiskConfigMissing
}

public sealed record RiskDecision(Guid Id, TradeIntentId TradeIntentId, RiskDecisionStatus Status, RiskRejectReason RejectReason, string Explanation, DateTimeOffset CreatedAtUtc, Guid? RiskLimitSetId = null, ModelRunId? ModelRunId = null, InstrumentId? InstrumentId = null, VenueId? VenueId = null);
public enum RiskDecisionCheckStatus { Passed, Failed, Blocked, Informational }
public sealed record RiskDecisionDetail(Guid Id, Guid RiskDecisionId, string CheckName, RiskDecisionCheckStatus Status, RiskRejectReason? RejectReason, decimal? ObservedValue, decimal? LimitValue, string? Unit, string Message, DateTimeOffset CreatedAtUtc);

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
