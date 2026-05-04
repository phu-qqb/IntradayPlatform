using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Application;

public sealed record VenueOrderRequest(ChildOrderId ChildOrderId, VenueId VenueId, InstrumentId InstrumentId, ClientOrderId ClientOrderId, OrderSide Side, OrderType OrderType, TimeInForce TimeInForce, decimal BaseQuantity, decimal VenueQuantity);
public sealed record VenueCancelRequest(ChildOrderId ChildOrderId, VenueId VenueId, ClientOrderId ClientOrderId);
public sealed record VenueOpenOrder(ChildOrderId ChildOrderId, VenueId VenueId, string BrokerOrderId, decimal LeavesQuantity);
public sealed record VenueExecutionResult(IReadOnlyList<ExecutionReport> Reports);

public interface IVenueExecutionGateway
{
    Task<VenueExecutionResult> SendOrderAsync(VenueOrderRequest request, CancellationToken cancellationToken);
    Task<VenueExecutionResult> CancelOrderAsync(VenueCancelRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<VenueOpenOrder>> GetOpenOrdersAsync(VenueId venueId, CancellationToken cancellationToken);
}

public interface IBrokerPositionProvider
{
    Task<IReadOnlyList<BrokerPositionSnapshot>> GetPositionsAsync(BrokerAccountId brokerAccountId, CancellationToken cancellationToken);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class FixedClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;
}

public sealed record OperatorIdentity(OperatorAuditActorType ActorType, string ActorId, string ActorDisplayName);

public interface IOperatorContext
{
    OperatorIdentity Current { get; }
    string? CorrelationId { get; }
    string? RequestId { get; }
}

public sealed class StaticOperatorContext(
    OperatorAuditActorType actorType = OperatorAuditActorType.System,
    string actorId = "system",
    string actorDisplayName = "System",
    string? correlationId = null,
    string? requestId = null) : IOperatorContext
{
    public OperatorIdentity Current { get; } = new(actorType, actorId, actorDisplayName);
    public string? CorrelationId { get; } = correlationId;
    public string? RequestId { get; } = requestId;
}

public sealed record OperatorAuditEventFilter(
    int Limit,
    OperatorAuditSeverity? Severity = null,
    OperatorAuditEventType? EventType = null,
    string? EntityType = null,
    string? EntityId = null,
    string? CorrelationId = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null);

public sealed record OperatorAuditRecordRequest(
    OperatorAuditEventType EventType,
    OperatorAuditSeverity Severity,
    OperatorAuditResult Result,
    string Source,
    string Description,
    string? EntityType = null,
    string? EntityId = null,
    string? Reason = null,
    object? Before = null,
    object? After = null,
    object? Metadata = null,
    string? CorrelationId = null,
    string? CausationId = null,
    string? RequestId = null,
    OperatorIdentity? Actor = null);

public interface IOperatorAuditRepository
{
    Task AddAsync(OperatorAuditEvent auditEvent, CancellationToken cancellationToken);
    Task<OperatorAuditEvent?> GetAsync(OperatorAuditEventId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<OperatorAuditEvent>> GetRecentAsync(OperatorAuditEventFilter filter, CancellationToken cancellationToken);
    Task<IReadOnlyList<OperatorAuditEvent>> GetByEntityAsync(string entityType, string entityId, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<OperatorAuditEvent>> GetByCorrelationIdAsync(string correlationId, int limit, CancellationToken cancellationToken);
}

public interface IOperatorAuditService
{
    Task<OperatorAuditEvent?> RecordAsync(OperatorAuditRecordRequest request, CancellationToken cancellationToken);
    Task<OperatorAuditEvent?> RecordSucceededAsync(OperatorAuditEventType eventType, string source, string description, string? entityType = null, string? entityId = null, object? metadata = null, CancellationToken cancellationToken = default);
    Task<OperatorAuditEvent?> RecordFailedAsync(OperatorAuditEventType eventType, string source, string description, string? reason = null, string? entityType = null, string? entityId = null, object? metadata = null, CancellationToken cancellationToken = default);
    Task<OperatorAuditEvent?> RecordBlockedAsync(OperatorAuditEventType eventType, string source, string description, string? reason = null, string? entityType = null, string? entityId = null, object? metadata = null, CancellationToken cancellationToken = default);
    Task<OperatorAuditEvent?> GetAsync(OperatorAuditEventId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<OperatorAuditEvent>> GetRecentAsync(OperatorAuditEventFilter filter, CancellationToken cancellationToken);
    Task<IReadOnlyList<OperatorAuditEvent>> GetByEntityAsync(string entityType, string entityId, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<OperatorAuditEvent>> GetByCorrelationIdAsync(string correlationId, int limit, CancellationToken cancellationToken);
}

public sealed record ExceptionCaseFilter(
    int Limit,
    ExceptionCaseStatus? Status = null,
    ExceptionCaseSeverity? Severity = null,
    ExceptionCaseType? Type = null,
    ExceptionCaseSource? Source = null,
    string? AssignedTo = null,
    string? Instrument = null,
    string? EntityType = null,
    string? EntityId = null,
    string? CorrelationId = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null);

public sealed record CreateExceptionCaseRequest(
    ExceptionCaseSeverity Severity,
    ExceptionCaseType Type,
    ExceptionCaseSource Source,
    string Title,
    string Description,
    string? EntityType = null,
    string? EntityId = null,
    InstrumentId? InstrumentId = null,
    string? Symbol = null,
    string? AssignedTo = null,
    object? Metadata = null);

public interface IExceptionCaseRepository
{
    Task AddCaseAsync(ExceptionCase exceptionCase, ExceptionCaseAction action, ExceptionCaseLink? link, CancellationToken cancellationToken);
    Task UpdateCaseAsync(ExceptionCase exceptionCase, ExceptionCaseAction action, CancellationToken cancellationToken);
    Task AddNoteAsync(ExceptionCaseNote note, ExceptionCaseAction action, CancellationToken cancellationToken);
    Task<ExceptionCase?> GetCaseAsync(ExceptionCaseId id, CancellationToken cancellationToken);
    Task<ExceptionCaseLink?> GetLinkAsync(string sourceEntityType, string sourceEntityId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExceptionCase>> GetCasesAsync(ExceptionCaseFilter filter, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExceptionCaseAction>> GetActionsAsync(ExceptionCaseId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExceptionCaseNote>> GetNotesAsync(ExceptionCaseId id, CancellationToken cancellationToken);
}

public interface IExceptionCaseService
{
    Task<ExceptionCase?> CreateOrUpdateFromReconciliationBreakAsync(ReconciliationRun run, ReconciliationBreak reconciliationBreak, CancellationToken cancellationToken);
    Task<ExceptionCase?> CreateOrUpdateFromEodBreakAsync(EodReconciliationRun run, EodReconciliationBreak reconciliationBreak, CancellationToken cancellationToken);
    Task<ExceptionCase> CreateManualCaseAsync(CreateExceptionCaseRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExceptionCase>> GetCasesAsync(ExceptionCaseFilter filter, CancellationToken cancellationToken);
    Task<ExceptionCase?> GetCaseAsync(ExceptionCaseId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExceptionCaseAction>> GetActionsAsync(ExceptionCaseId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExceptionCaseNote>> GetNotesAsync(ExceptionCaseId id, CancellationToken cancellationToken);
    Task<ExceptionCase> AcknowledgeAsync(ExceptionCaseId id, string? reason, CancellationToken cancellationToken);
    Task<ExceptionCase> AssignAsync(ExceptionCaseId id, string assignedTo, CancellationToken cancellationToken);
    Task<ExceptionCase> MarkInvestigatingAsync(ExceptionCaseId id, string? reason, CancellationToken cancellationToken);
    Task<ExceptionCase> ResolveAsync(ExceptionCaseId id, string reason, CancellationToken cancellationToken);
    Task<ExceptionCase> MarkFalsePositiveAsync(ExceptionCaseId id, string reason, CancellationToken cancellationToken);
    Task<ExceptionCase> WaiveAsync(ExceptionCaseId id, string reason, CancellationToken cancellationToken);
    Task<ExceptionCase> ReopenAsync(ExceptionCaseId id, string? reason, CancellationToken cancellationToken);
    Task<ExceptionCaseNote> AddNoteAsync(ExceptionCaseId id, string note, CancellationToken cancellationToken);
}

public interface IIntradayRepository
{
    Task<PlatformState> LoadStateAsync(CancellationToken cancellationToken);
    Task<ModelRun?> GetNextUnprocessedModelRunAsync(CancellationToken cancellationToken);
    Task<ModelRun?> GetModelRunAsync(ModelRunId modelRunId, CancellationToken cancellationToken);
    Task AddModelRunAsync(ModelRun modelRun, IReadOnlyList<TargetWeight> weights, CancellationToken cancellationToken);
    Task MarkModelRunProcessedAsync(ModelRunId modelRunId, ModelRunStatus status, CancellationToken cancellationToken);
    Task SaveReconciliationAsync(ReconciliationRun run, IReadOnlyList<ReconciliationBreak> breaks, CancellationToken cancellationToken);
    Task SaveTargetAndDriftAsync(TargetPosition targetPosition, DriftSnapshot driftSnapshot, CancellationToken cancellationToken);
    Task AddTradeIntentAsync(TradeIntent intent, CancellationToken cancellationToken);
    Task AddRiskDecisionAsync(RiskDecision decision, IReadOnlyList<RiskDecisionDetail>? details, CancellationToken cancellationToken);
    Task AddOrdersAsync(ParentOrder parentOrder, ChildOrder childOrder, CancellationToken cancellationToken);
    Task AddExecutionReportAsync(ExecutionReport report, CancellationToken cancellationToken);
    Task<bool> TryAddFillAsync(Fill fill, CancellationToken cancellationToken);
    Task AddPositionLedgerEventAsync(PositionLedgerEvent ledgerEvent, CancellationToken cancellationToken);
    Task SetKillSwitchAsync(bool isActive, string? reason, CancellationToken cancellationToken);
    Task UpsertRiskLimitSetAsync(RiskLimitSet riskLimitSet, CancellationToken cancellationToken);
    Task UpsertRiskLimitAsync(RiskLimit riskLimit, CancellationToken cancellationToken);
    Task UpsertInstrumentRiskLimitAsync(InstrumentRiskLimit instrumentRiskLimit, CancellationToken cancellationToken);
    Task UpsertVenueRiskLimitAsync(VenueRiskLimit venueRiskLimit, CancellationToken cancellationToken);
    Task UpsertTradingWindowAsync(TradingWindow tradingWindow, CancellationToken cancellationToken);
    Task UpsertInstrumentAsync(Instrument instrument, CancellationToken cancellationToken);
    Task UpsertVenueAsync(Venue venue, CancellationToken cancellationToken);
}

public interface IMarketDataSnapshotRepository
{
    Task AddAsync(MarketDataSnapshot snapshot, CancellationToken cancellationToken);
    Task AddRangeAsync(IReadOnlyList<MarketDataSnapshot> snapshots, CancellationToken cancellationToken);
    Task<MarketDataSnapshot?> GetLatestAsync(InstrumentId instrumentId, VenueId venueId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MarketDataSnapshot>> GetRangeAsync(InstrumentId instrumentId, VenueId venueId, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken);
}

public interface IMarketDataBarRepository
{
    Task<BarUpsertResult> UpsertAsync(MarketDataBar bar, CancellationToken cancellationToken);
    Task<MarketDataBar?> GetAsync(InstrumentId instrumentId, VenueId venueId, BarTimeframe timeframe, DateTimeOffset barStartUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<MarketDataBar>> GetRangeAsync(InstrumentId instrumentId, VenueId venueId, BarTimeframe timeframe, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken);
}

public interface IBarBuildRunRepository
{
    Task AddAsync(BarBuildRun run, CancellationToken cancellationToken);
    Task MarkCompletedAsync(BarBuildRunId runId, int barsCreated, int barsUpdated, CancellationToken cancellationToken);
    Task MarkFailedAsync(BarBuildRunId runId, string errorMessage, CancellationToken cancellationToken);
}

public interface IMarketDataProvider
{
    Task<IReadOnlyList<MarketDataSnapshot>> GetSnapshotsAsync(Instrument instrument, Venue venue, DateTimeOffset startUtc, TimeSpan interval, int count, decimal bid, decimal ask, decimal bidStep, decimal askStep, CancellationToken cancellationToken);
}

public interface IBarBuilderService
{
    Task<BarBuildResult> BuildBarsAsync(VenueId venueId, BarTimeframe timeframe, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken);
    Task<BarBuildResult> BuildLatestFifteenMinuteBarsAsync(VenueId venueId, CancellationToken cancellationToken);
}

public interface IModelWeightBatchRepository
{
    Task<ModelWeightBatch?> GetBatchAsync(ModelWeightBatchId batchId, CancellationToken cancellationToken);
    Task<ModelWeightBatch?> GetBatchByExternalIdAsync(ModelWeightSourceSystem sourceSystem, string externalBatchId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ModelWeightBatch>> GetRecentBatchesAsync(int limit, ModelWeightBatchStatus? status, ModelWeightSourceSystem? sourceSystem, string? modelName, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<ModelWeightBatch>> GetReadyBatchesAsync(int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<ModelWeightRow>> GetRowsAsync(ModelWeightBatchId batchId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ModelWeightValidationIssue>> GetValidationIssuesAsync(ModelWeightBatchId batchId, CancellationToken cancellationToken);
    Task AddBatchAsync(ModelWeightBatch batch, IReadOnlyList<ModelWeightRow> rows, CancellationToken cancellationToken);
    Task UpdateBatchAsync(ModelWeightBatch batch, CancellationToken cancellationToken);
    Task AddValidationIssuesAsync(ModelWeightBatchId batchId, IReadOnlyList<ModelWeightValidationIssue> issues, bool replaceExisting, CancellationToken cancellationToken);
    Task MarkPromotedAsync(ModelWeightBatchId batchId, ModelRunId modelRunId, DateTimeOffset promotedAtUtc, CancellationToken cancellationToken);
}

public interface IModelWeightPromotionService
{
    Task<ModelWeightPromotionResult> ValidateBatchAsync(ModelWeightBatchId batchId, CancellationToken cancellationToken);
    Task<ModelWeightPromotionResult> PromoteBatchAsync(ModelWeightBatchId batchId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ModelWeightPromotionResult>> PromoteReadyBatchesAsync(int limit, CancellationToken cancellationToken);
}

public interface IFakeModelWeightGenerator
{
    Task<ModelWeightBatch> CreateFakeBatchAsync(CreateFakeModelWeightBatchRequest request, CancellationToken cancellationToken);
}

public interface ILmaxEodReportRepository
{
    Task AddImportRunAsync(LmaxReportImportRun run, CancellationToken cancellationToken);
    Task UpdateImportRunAsync(LmaxReportImportRun run, CancellationToken cancellationToken);
    Task AddValidationIssuesAsync(IReadOnlyList<LmaxReportValidationIssue> issues, CancellationToken cancellationToken);
    Task AddIndividualTradesAsync(IReadOnlyList<LmaxIndividualTrade> trades, CancellationToken cancellationToken);
    Task AddTradeSummariesAsync(IReadOnlyList<LmaxTradeSummary> summaries, CancellationToken cancellationToken);
    Task AddCurrencyWalletsAsync(IReadOnlyList<LmaxCurrencyWallet> wallets, CancellationToken cancellationToken);
    Task<IReadOnlyList<LmaxReportImportRun>> GetImportRunsAsync(int limit, DateOnly? reportDate, LmaxReportType? reportType, CancellationToken cancellationToken);
    Task<IReadOnlyList<LmaxReportValidationIssue>> GetValidationIssuesAsync(int limit, LmaxReportImportRunId? importRunId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LmaxIndividualTrade>> GetIndividualTradesAsync(DateOnly? reportDate, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<LmaxTradeSummary>> GetTradeSummariesAsync(DateOnly? reportDate, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<LmaxCurrencyWallet>> GetCurrencyWalletsAsync(DateOnly? reportDate, int limit, CancellationToken cancellationToken);
    Task AddEodReconciliationAsync(EodReconciliationRun run, IReadOnlyList<EodReconciliationBreak> breaks, CancellationToken cancellationToken);
    Task<IReadOnlyList<EodReconciliationRun>> GetEodReconciliationRunsAsync(DateOnly? reportDate, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<EodReconciliationBreak>> GetEodReconciliationBreaksAsync(DateOnly? reportDate, int limit, CancellationToken cancellationToken);
}

public interface ILmaxEodReportImportService
{
    Task<LmaxReportImportResult> ImportIndividualTradesAsync(string filePath, DateOnly reportDate, string venueName, string brokerAccountCode, CancellationToken cancellationToken);
    Task<LmaxReportImportResult> ImportTradesSummaryAsync(string filePath, DateOnly reportDate, string venueName, string brokerAccountCode, CancellationToken cancellationToken);
    Task<LmaxReportImportResult> ImportCurrencyWalletsAsync(string filePath, DateOnly reportDate, string venueName, string brokerAccountCode, CancellationToken cancellationToken);
    Task<LmaxReportImportResult> ImportReportSetAsync(string individualTradesPath, string tradesSummaryPath, string currencyWalletsPath, DateOnly reportDate, string venueName, string brokerAccountCode, CancellationToken cancellationToken);
}

public interface ILmaxReportPairConsistencyService
{
    Task<IReadOnlyList<LmaxReportValidationIssue>> CheckAsync(LmaxReportImportRunId importRunId, DateOnly reportDate, VenueId venueId, BrokerAccountId brokerAccountId, CancellationToken cancellationToken);
}

public interface IEodReconciliationService
{
    Task<EodReconciliationResult> RunAsync(DateOnly reportDate, string venueName, string brokerAccountCode, CancellationToken cancellationToken);
}

public interface IEodPnlSummaryService
{
    Task<EodPnlSummary?> GetSummaryAsync(DateOnly reportDate, string venueName, string brokerAccountCode, CancellationToken cancellationToken);
}

public interface IFakeLmaxEodReportGenerator
{
    Task<FakeLmaxEodReportGenerationResult> GenerateAsync(DateOnly reportDate, string venueName, string brokerAccountCode, LmaxEodMutationMode mutationMode, CancellationToken cancellationToken);
}

public interface IRiskControlService
{
    Task<IReadOnlyList<RiskLimitSet>> GetRiskLimitSetsAsync(CancellationToken cancellationToken);
    Task<RiskLimitSet?> GetRiskLimitSetAsync(Guid id, CancellationToken cancellationToken);
    Task<RiskLimitSet?> GetActiveRiskLimitSetAsync(string fundCode, string? modelName, CancellationToken cancellationToken);
    Task<RiskLimitSet> CreateDraftRiskLimitSetAsync(string fundCode, string? modelName, string name, string? description, string reason, CancellationToken cancellationToken);
    Task<RiskLimitSet> CloneRiskLimitSetAsync(Guid id, string reason, CancellationToken cancellationToken);
    Task<RiskLimitSet> ActivateRiskLimitSetAsync(Guid id, string reason, CancellationToken cancellationToken);
    Task<RiskLimitSet> RetireRiskLimitSetAsync(Guid id, string reason, CancellationToken cancellationToken);
    Task<RiskLimit> UpdateRiskLimitAsync(Guid id, decimal value, string? unit, string reason, CancellationToken cancellationToken);
    Task<InstrumentRiskLimit> UpdateInstrumentRiskLimitAsync(Guid id, decimal? maxTradeNotionalUsd, decimal? maxExposureUsd, decimal? minTradeQuantity, int? maxOrdersPerDay, bool? isTradingEnabled, string reason, CancellationToken cancellationToken);
    Task<VenueRiskLimit> UpdateVenueRiskLimitAsync(Guid id, decimal? maxTradeNotionalUsd, decimal? maxDailyTurnoverUsd, int? maxOrdersPerMinute, bool? isVenueEnabled, string reason, CancellationToken cancellationToken);
    Task<TradingWindow> UpdateTradingWindowAsync(Guid id, TimeOnly? opensAtUtc, TimeOnly? closesAtUtc, TimeOnly? noNewOrdersAfterUtc, TimeOnly? flattenAtUtc, bool? tradingEnabled, string reason, CancellationToken cancellationToken);
    Task<Instrument> UpdateInstrumentControlsAsync(InstrumentId id, bool? isTradingEnabled, bool? isReportImportEnabled, bool? isMarketDataEnabled, string reason, CancellationToken cancellationToken);
    Task<Venue> UpdateVenueControlsAsync(VenueId id, bool? isTradingEnabled, bool? isReportImportEnabled, bool? isMarketDataEnabled, string reason, CancellationToken cancellationToken);
}

public sealed record BarUpsertResult(bool Created);
public sealed record BarBuildResult(BarBuildRunId RunId, int BarsCreated, int BarsUpdated, BarBuildRunStatus Status, string? ErrorMessage = null);
public sealed record CreateFakeModelWeightRowRequest(string RawSecurityId, string Symbol, decimal Weight);
public sealed record CreateFakeModelWeightBatchRequest(
    string? ExternalBatchId,
    ModelWeightSourceSystem SourceSystem,
    string FundCode,
    string ModelName,
    DateTimeOffset? AsOfUtc,
    DateTimeOffset? EffectiveAtUtc,
    int FrequencyMinutes,
    decimal NavUsd,
    TargetQuantityMode TargetQuantityMode,
    ModelWeightBatchStatus Status,
    IReadOnlyList<CreateFakeModelWeightRowRequest> Weights);
public sealed record ModelWeightPromotionResult(
    ModelWeightBatchId? BatchId,
    ModelWeightBatchStatus? Status,
    ModelRunId? PromotedModelRunId,
    ModelRunId? ModelRunId,
    int ValidationIssueCount,
    IReadOnlyList<ModelWeightValidationIssue> Issues,
    string Message,
    bool Succeeded,
    bool AlreadyPromoted);
public sealed record LmaxReportImportResult(LmaxReportImportRunId ImportRunId, LmaxReportImportStatus Status, int RowCount, int BlockingIssueCount, IReadOnlyList<LmaxReportValidationIssue> Issues, string Message);
public sealed record EodReconciliationResult(Guid RunId, DateOnly ReportDate, int BreakCount, int BlockingBreakCount, IReadOnlyList<EodReconciliationBreak> Breaks);
public sealed record FakeLmaxEodReportGenerationResult(DateOnly ReportDate, string IndividualTradesPath, string TradesSummaryPath, string CurrencyWalletsPath, int IndividualTradeCount, int TradeSummaryCount, int CurrencyWalletCount, LmaxEodMutationMode MutationMode);

public enum ReferenceDataIntegrityIssueType
{
    DuplicateFund,
    DuplicateBrokerAccount,
    DuplicateInstrument,
    DuplicateVenue,
    DuplicateVenueInstrumentMapping,
    DuplicateRiskLimitSet,
    DuplicateRiskLimit,
    DuplicateInstrumentRiskLimit,
    DuplicateVenueRiskLimit,
    DuplicateTradingWindow,
    DuplicateKillSwitchState,
    MissingRequiredReferenceData,
    AmbiguousReferenceData,
    DisabledRequiredReferenceData
}

public enum ReferenceDataIntegritySeverity { Info, Warning, Blocking }
public enum ReferenceDataIntegrityStatus { Open, Acknowledged, Resolved }

public sealed record ReferenceDataIntegrityIssue(
    Guid Id,
    ReferenceDataIntegrityIssueType Type,
    ReferenceDataIntegritySeverity Severity,
    ReferenceDataIntegrityStatus Status,
    string Key,
    string Description,
    DateTimeOffset CreatedAtUtc);

public sealed record ReferenceDataIntegrityResult(
    DateTimeOffset CheckedAtUtc,
    int BlockingIssueCount,
    int WarningIssueCount,
    IReadOnlyList<ReferenceDataIntegrityIssue> Issues);

public interface IReferenceDataIntegrityService
{
    Task<ReferenceDataIntegrityResult> CheckAsync(CancellationToken cancellationToken);
}

public sealed class BarBuilderOptions
{
    public int FifteenMinuteMinimumObservationCount { get; set; } = 3;
    public bool CreateNoDataBars { get; set; }
    public string Source { get; set; } = "LocalSnapshotStore";
    public string BuilderVersion { get; set; } = "bar-builder-v1";
}

public sealed class PlatformState
{
    public List<Fund> Funds { get; } = [];
    public List<BrokerAccount> BrokerAccounts { get; } = [];
    public List<Instrument> Instruments { get; } = [];
    public List<InstrumentAlias> InstrumentAliases { get; } = [];
    public List<Venue> Venues { get; } = [];
    public List<VenueInstrumentMapping> VenueInstrumentMappings { get; } = [];
    public List<NavSnapshot> NavSnapshots { get; } = [];
    public List<ModelRun> ModelRuns { get; } = [];
    public List<TargetWeight> TargetWeights { get; } = [];
    public List<ModelWeightBatch> ModelWeightBatches { get; } = [];
    public List<ModelWeightRow> ModelWeightRows { get; } = [];
    public List<ModelWeightValidationIssue> ModelWeightValidationIssues { get; } = [];
    public List<MarketDataSnapshot> MarketData { get; } = [];
    public List<TargetPosition> TargetPositions { get; } = [];
    public List<DriftSnapshot> DriftSnapshots { get; } = [];
    public List<MarketDataBar> MarketDataBars { get; } = [];
    public List<BarBuildRun> BarBuildRuns { get; } = [];
    public List<PositionLedgerEvent> PositionLedger { get; } = [];
    public List<ReconciliationRun> ReconciliationRuns { get; } = [];
    public List<ReconciliationBreak> ReconciliationBreaks { get; } = [];
    public List<TradeIntent> TradeIntents { get; } = [];
    public List<RiskDecision> RiskDecisions { get; } = [];
    public List<RiskDecisionDetail> RiskDecisionDetails { get; } = [];
    public List<ParentOrder> ParentOrders { get; } = [];
    public List<ChildOrder> ChildOrders { get; } = [];
    public List<ExecutionReport> ExecutionReports { get; } = [];
    public List<Fill> Fills { get; } = [];
    public List<RiskLimitSet> RiskLimitSets { get; } = [];
    public List<RiskLimit> RiskLimits { get; } = [];
    public List<InstrumentRiskLimit> InstrumentRiskLimits { get; } = [];
    public List<VenueRiskLimit> VenueRiskLimits { get; } = [];
    public List<TradingWindow> TradingWindows { get; } = [];
    public List<KillSwitchState> KillSwitchStates { get; } = [];
    public List<LmaxReportImportRun> LmaxReportImportRuns { get; } = [];
    public List<LmaxReportValidationIssue> LmaxReportValidationIssues { get; } = [];
    public List<LmaxIndividualTrade> LmaxIndividualTrades { get; } = [];
    public List<LmaxTradeSummary> LmaxTradeSummaries { get; } = [];
    public List<LmaxCurrencyWallet> LmaxCurrencyWallets { get; } = [];
    public List<EodReconciliationRun> EodReconciliationRuns { get; } = [];
    public List<EodReconciliationBreak> EodReconciliationBreaks { get; } = [];
    public List<ExceptionCase> ExceptionCases { get; } = [];
    public List<ExceptionCaseAction> ExceptionCaseActions { get; } = [];
    public List<ExceptionCaseNote> ExceptionCaseNotes { get; } = [];
    public List<ExceptionCaseLink> ExceptionCaseLinks { get; } = [];
    public List<OperatorAuditEvent> OperatorAuditEvents { get; } = [];
    public KillSwitchState KillSwitch { get; set; } = new(Guid.NewGuid(), false, null, DateTimeOffset.UnixEpoch);
}

public sealed class OperatorAuditService(IOperatorAuditRepository repository, IOperatorContext context, IClock clock) : IOperatorAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = false };
    private static readonly string[] SecretKeyFragments = ["password", "secret", "token", "apikey", "api_key", "fixpassword"];

    public async Task<OperatorAuditEvent?> RecordAsync(OperatorAuditRecordRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var actor = request.Actor ?? context.Current;
            var auditEvent = new OperatorAuditEvent(
                OperatorAuditEventId.New(),
                clock.UtcNow,
                actor.ActorType,
                actor.ActorId,
                actor.ActorDisplayName,
                request.EventType,
                request.Severity,
                request.Result,
                request.EntityType,
                request.EntityId,
                request.CorrelationId ?? context.CorrelationId,
                request.CausationId,
                request.RequestId ?? context.RequestId,
                request.Source,
                request.Description,
                request.Reason,
                SerializeSanitized(request.Before),
                SerializeSanitized(request.After),
                SerializeSanitized(request.Metadata));

            await repository.AddAsync(auditEvent, cancellationToken);
            return auditEvent;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Operator audit write failed: {ex.Message}");
            return null;
        }
    }

    public Task<OperatorAuditEvent?> RecordSucceededAsync(OperatorAuditEventType eventType, string source, string description, string? entityType = null, string? entityId = null, object? metadata = null, CancellationToken cancellationToken = default)
        => RecordAsync(new(eventType, OperatorAuditSeverity.Info, OperatorAuditResult.Succeeded, source, description, entityType, entityId, Metadata: metadata), cancellationToken);

    public Task<OperatorAuditEvent?> RecordFailedAsync(OperatorAuditEventType eventType, string source, string description, string? reason = null, string? entityType = null, string? entityId = null, object? metadata = null, CancellationToken cancellationToken = default)
        => RecordAsync(new(eventType, OperatorAuditSeverity.Critical, OperatorAuditResult.Failed, source, description, entityType, entityId, reason, Metadata: metadata), cancellationToken);

    public Task<OperatorAuditEvent?> RecordBlockedAsync(OperatorAuditEventType eventType, string source, string description, string? reason = null, string? entityType = null, string? entityId = null, object? metadata = null, CancellationToken cancellationToken = default)
        => RecordAsync(new(eventType, OperatorAuditSeverity.Warning, OperatorAuditResult.Blocked, source, description, entityType, entityId, reason, Metadata: metadata), cancellationToken);

    public Task<IReadOnlyList<OperatorAuditEvent>> GetRecentAsync(OperatorAuditEventFilter filter, CancellationToken cancellationToken)
        => repository.GetRecentAsync(filter, cancellationToken);

    public Task<OperatorAuditEvent?> GetAsync(OperatorAuditEventId id, CancellationToken cancellationToken)
        => repository.GetAsync(id, cancellationToken);

    public Task<IReadOnlyList<OperatorAuditEvent>> GetByEntityAsync(string entityType, string entityId, int limit, CancellationToken cancellationToken)
        => repository.GetByEntityAsync(entityType, entityId, limit, cancellationToken);

    public Task<IReadOnlyList<OperatorAuditEvent>> GetByCorrelationIdAsync(string correlationId, int limit, CancellationToken cancellationToken)
        => repository.GetByCorrelationIdAsync(correlationId, limit, cancellationToken);

    public static string? SerializeSanitized(object? value)
    {
        if (value is null) return null;
        var sanitized = Sanitize(value);
        return sanitized is null ? null : JsonSerializer.Serialize(sanitized, JsonOptions);
    }

    public static object? Sanitize(object? value)
    {
        if (value is null) return null;
        var json = JsonSerializer.SerializeToNode(value, JsonOptions);
        SanitizeNode(json);
        return json;
    }

    private static void SanitizeNode(System.Text.Json.Nodes.JsonNode? node)
    {
        if (node is System.Text.Json.Nodes.JsonObject obj)
        {
            foreach (var property in obj.ToList())
            {
                if (IsSecretKey(property.Key))
                {
                    obj[property.Key] = "***";
                }
                else
                {
                    SanitizeNode(property.Value);
                }
            }
        }
        else if (node is System.Text.Json.Nodes.JsonArray array)
        {
            foreach (var item in array)
            {
                SanitizeNode(item);
            }
        }
    }

    private static bool IsSecretKey(string key)
        => SecretKeyFragments.Any(fragment => key.Replace("-", "", StringComparison.Ordinal).Contains(fragment, StringComparison.OrdinalIgnoreCase));
}

public sealed class RiskControlService(IIntradayRepository repository, IOperatorAuditService audit, IOperatorContext context, IClock clock) : IRiskControlService
{
    public async Task<IReadOnlyList<RiskLimitSet>> GetRiskLimitSetsAsync(CancellationToken cancellationToken)
        => (await repository.LoadStateAsync(cancellationToken)).RiskLimitSets.OrderByDescending(x => x.IsActive).ThenByDescending(x => x.Version).ToList();

    public async Task<RiskLimitSet?> GetRiskLimitSetAsync(Guid id, CancellationToken cancellationToken)
        => (await repository.LoadStateAsync(cancellationToken)).RiskLimitSets.FirstOrDefault(x => x.Id == id);

    public async Task<RiskLimitSet?> GetActiveRiskLimitSetAsync(string fundCode, string? modelName, CancellationToken cancellationToken)
    {
        var state = await repository.LoadStateAsync(cancellationToken);
        var fund = ResolveFund(state, fundCode);
        return fund is null ? null : SelectActive(state, fund.Id, modelName);
    }

    public async Task<RiskLimitSet> CreateDraftRiskLimitSetAsync(string fundCode, string? modelName, string name, string? description, string reason, CancellationToken cancellationToken)
    {
        RequireReason(reason);
        var state = await repository.LoadStateAsync(cancellationToken);
        var fund = ResolveFund(state, fundCode) ?? throw new DomainRuleViolationException($"Fund '{fundCode}' was not found.");
        var now = clock.UtcNow;
        var version = state.RiskLimitSets.Where(x => x.FundId == fund.Id && string.Equals(x.ModelName, modelName, StringComparison.OrdinalIgnoreCase)).Select(x => x.Version).DefaultIfEmpty(0).Max() + 1;
        var draft = new RiskLimitSet(Guid.NewGuid(), fund.Id, true, 2_000_000m, TimeSpan.FromHours(24), TimeSpan.FromMinutes(30), 0.0001m, 0.1m, modelName, name, version, RiskLimitSetStatus.Draft, false, null, null, now, context.Current.ActorId, null, null, null, null, description);
        await repository.UpsertRiskLimitSetAsync(draft, cancellationToken);
        await AuditRiskChangeAsync(OperatorAuditEventType.RiskLimitSetCreated, "Risk limit set draft created.", "RiskLimitSet", draft.Id.ToString("D"), reason, null, draft, new { draft.Name, draft.Version, draft.ModelName }, cancellationToken);
        return draft;
    }

    public async Task<RiskLimitSet> CloneRiskLimitSetAsync(Guid id, string reason, CancellationToken cancellationToken)
    {
        RequireReason(reason);
        var state = await repository.LoadStateAsync(cancellationToken);
        var source = state.RiskLimitSets.FirstOrDefault(x => x.Id == id) ?? throw new DomainRuleViolationException("Risk limit set not found.");
        var now = clock.UtcNow;
        var version = state.RiskLimitSets.Where(x => x.FundId == source.FundId && string.Equals(x.ModelName, source.ModelName, StringComparison.OrdinalIgnoreCase)).Select(x => x.Version).DefaultIfEmpty(0).Max() + 1;
        var clone = source with { Id = Guid.NewGuid(), Version = version, Name = $"{source.Name} Draft v{version}", Status = RiskLimitSetStatus.Draft, IsActive = false, CreatedAtUtc = now, CreatedBy = context.Current.ActorId, ActivatedAtUtc = null, ActivatedBy = null, RetiredAtUtc = null, RetiredBy = null };
        await repository.UpsertRiskLimitSetAsync(clone, cancellationToken);
        foreach (var limit in state.RiskLimits.Where(x => x.RiskLimitSetId == source.Id).ToList())
        {
            await repository.UpsertRiskLimitAsync(limit with { Id = Guid.NewGuid(), RiskLimitSetId = clone.Id }, cancellationToken);
        }
        foreach (var limit in state.InstrumentRiskLimits.Where(x => x.RiskLimitSetId == source.Id).ToList())
        {
            await repository.UpsertInstrumentRiskLimitAsync(limit with { Id = Guid.NewGuid(), RiskLimitSetId = clone.Id }, cancellationToken);
        }
        foreach (var limit in state.VenueRiskLimits.Where(x => x.RiskLimitSetId == source.Id).ToList())
        {
            await repository.UpsertVenueRiskLimitAsync(limit with { Id = Guid.NewGuid(), RiskLimitSetId = clone.Id }, cancellationToken);
        }
        await AuditRiskChangeAsync(OperatorAuditEventType.RiskLimitSetCloned, "Risk limit set cloned to draft.", "RiskLimitSet", clone.Id.ToString("D"), reason, source, clone, new { sourceRiskLimitSetId = source.Id, clone.Version }, cancellationToken);
        return clone;
    }

    public async Task<RiskLimitSet> ActivateRiskLimitSetAsync(Guid id, string reason, CancellationToken cancellationToken)
    {
        RequireReason(reason);
        var state = await repository.LoadStateAsync(cancellationToken);
        var draft = state.RiskLimitSets.FirstOrDefault(x => x.Id == id) ?? throw new DomainRuleViolationException("Risk limit set not found.");
        if (draft.Status == RiskLimitSetStatus.Archived) throw new DomainRuleViolationException("Archived risk limit sets are read-only.");
        var now = clock.UtcNow;
        foreach (var active in state.RiskLimitSets.Where(x => x.Id != draft.Id && x.FundId == draft.FundId && string.Equals(x.ModelName, draft.ModelName, StringComparison.OrdinalIgnoreCase) && x.IsActive).ToList())
        {
            await repository.UpsertRiskLimitSetAsync(active with { IsActive = false, Status = RiskLimitSetStatus.Retired, RetiredAtUtc = now, RetiredBy = context.Current.ActorId, EffectiveToUtc = now }, cancellationToken);
        }
        var activated = draft with { IsActive = true, Status = RiskLimitSetStatus.Active, ActivatedAtUtc = now, ActivatedBy = context.Current.ActorId, EffectiveFromUtc = now, RetiredAtUtc = null, RetiredBy = null, EffectiveToUtc = null };
        await repository.UpsertRiskLimitSetAsync(activated, cancellationToken);
        await AuditRiskChangeAsync(OperatorAuditEventType.RiskLimitSetActivated, "Risk limit set activated.", "RiskLimitSet", activated.Id.ToString("D"), reason, draft, activated, new { activated.Name, activated.Version }, cancellationToken);
        return activated;
    }

    public async Task<RiskLimitSet> RetireRiskLimitSetAsync(Guid id, string reason, CancellationToken cancellationToken)
    {
        RequireReason(reason);
        var state = await repository.LoadStateAsync(cancellationToken);
        var current = state.RiskLimitSets.FirstOrDefault(x => x.Id == id) ?? throw new DomainRuleViolationException("Risk limit set not found.");
        if (current.Status == RiskLimitSetStatus.Archived) throw new DomainRuleViolationException("Archived risk limit sets are read-only.");
        var retired = current with { IsActive = false, Status = RiskLimitSetStatus.Retired, RetiredAtUtc = clock.UtcNow, RetiredBy = context.Current.ActorId, EffectiveToUtc = clock.UtcNow };
        await repository.UpsertRiskLimitSetAsync(retired, cancellationToken);
        await AuditRiskChangeAsync(OperatorAuditEventType.RiskLimitSetRetired, "Risk limit set retired.", "RiskLimitSet", retired.Id.ToString("D"), reason, current, retired, new { retired.Name, retired.Version }, cancellationToken);
        return retired;
    }

    public async Task<RiskLimit> UpdateRiskLimitAsync(Guid id, decimal value, string? unit, string reason, CancellationToken cancellationToken)
    {
        RequireReason(reason);
        var state = await repository.LoadStateAsync(cancellationToken);
        var current = state.RiskLimits.FirstOrDefault(x => x.Id == id) ?? throw new DomainRuleViolationException("Risk limit not found.");
        EnsureDraft(state, current.RiskLimitSetId);
        var updated = current with { Value = value, Unit = string.IsNullOrWhiteSpace(unit) ? current.Unit : unit };
        await repository.UpsertRiskLimitAsync(updated, cancellationToken);
        await AuditRiskChangeAsync(OperatorAuditEventType.RiskLimitUpdated, "Risk limit updated.", "RiskLimit", id.ToString("D"), reason, current, updated, null, cancellationToken);
        return updated;
    }

    public async Task<InstrumentRiskLimit> UpdateInstrumentRiskLimitAsync(Guid id, decimal? maxTradeNotionalUsd, decimal? maxExposureUsd, decimal? minTradeQuantity, int? maxOrdersPerDay, bool? isTradingEnabled, string reason, CancellationToken cancellationToken)
    {
        RequireReason(reason);
        var state = await repository.LoadStateAsync(cancellationToken);
        var current = state.InstrumentRiskLimits.FirstOrDefault(x => x.Id == id) ?? throw new DomainRuleViolationException("Instrument risk limit not found.");
        EnsureDraft(state, current.RiskLimitSetId);
        var updated = current with { MaxTradeNotionalUsd = maxTradeNotionalUsd ?? current.MaxTradeNotionalUsd, MaxExposureUsd = maxExposureUsd ?? current.MaxExposureUsd, MinTradeQuantity = minTradeQuantity ?? current.MinTradeQuantity, MaxOrdersPerDay = maxOrdersPerDay ?? current.MaxOrdersPerDay, IsTradingEnabled = isTradingEnabled ?? current.IsTradingEnabled };
        await repository.UpsertInstrumentRiskLimitAsync(updated, cancellationToken);
        await AuditRiskChangeAsync(OperatorAuditEventType.InstrumentRiskLimitUpdated, "Instrument risk limit updated.", "InstrumentRiskLimit", id.ToString("D"), reason, current, updated, null, cancellationToken);
        return updated;
    }

    public async Task<VenueRiskLimit> UpdateVenueRiskLimitAsync(Guid id, decimal? maxTradeNotionalUsd, decimal? maxDailyTurnoverUsd, int? maxOrdersPerMinute, bool? isVenueEnabled, string reason, CancellationToken cancellationToken)
    {
        RequireReason(reason);
        var state = await repository.LoadStateAsync(cancellationToken);
        var current = state.VenueRiskLimits.FirstOrDefault(x => x.Id == id) ?? throw new DomainRuleViolationException("Venue risk limit not found.");
        EnsureDraft(state, current.RiskLimitSetId);
        var updated = current with { MaxTradeNotionalUsd = maxTradeNotionalUsd ?? current.MaxTradeNotionalUsd, MaxDailyTurnoverUsd = maxDailyTurnoverUsd ?? current.MaxDailyTurnoverUsd, MaxOrdersPerMinute = maxOrdersPerMinute ?? current.MaxOrdersPerMinute, IsVenueEnabled = isVenueEnabled ?? current.IsVenueEnabled };
        await repository.UpsertVenueRiskLimitAsync(updated, cancellationToken);
        await AuditRiskChangeAsync(OperatorAuditEventType.VenueRiskLimitUpdated, "Venue risk limit updated.", "VenueRiskLimit", id.ToString("D"), reason, current, updated, null, cancellationToken);
        return updated;
    }

    public async Task<TradingWindow> UpdateTradingWindowAsync(Guid id, TimeOnly? opensAtUtc, TimeOnly? closesAtUtc, TimeOnly? noNewOrdersAfterUtc, TimeOnly? flattenAtUtc, bool? tradingEnabled, string reason, CancellationToken cancellationToken)
    {
        RequireReason(reason);
        var state = await repository.LoadStateAsync(cancellationToken);
        var current = state.TradingWindows.FirstOrDefault(x => x.Id == id) ?? throw new DomainRuleViolationException("Trading window not found.");
        var updated = current with { OpensAtUtc = opensAtUtc ?? current.OpensAtUtc, ClosesAtUtc = closesAtUtc ?? current.ClosesAtUtc, NoNewOrdersAfterUtc = noNewOrdersAfterUtc ?? current.NoNewOrdersAfterUtc, FlattenAtUtc = flattenAtUtc ?? current.FlattenAtUtc, TradingEnabled = tradingEnabled ?? current.TradingEnabled, UpdatedAtUtc = clock.UtcNow };
        await repository.UpsertTradingWindowAsync(updated, cancellationToken);
        await AuditRiskChangeAsync(OperatorAuditEventType.TradingWindowUpdated, "Trading window updated.", "TradingWindow", id.ToString("D"), reason, current, updated, null, cancellationToken);
        return updated;
    }

    public async Task<Instrument> UpdateInstrumentControlsAsync(InstrumentId id, bool? isTradingEnabled, bool? isReportImportEnabled, bool? isMarketDataEnabled, string reason, CancellationToken cancellationToken)
    {
        RequireReason(reason);
        var state = await repository.LoadStateAsync(cancellationToken);
        var current = state.Instruments.FirstOrDefault(x => x.Id == id) ?? throw new DomainRuleViolationException("Instrument not found.");
        var updated = current with { IsTradingEnabled = isTradingEnabled ?? current.IsTradingEnabled, IsReportImportEnabled = isReportImportEnabled ?? current.IsReportImportEnabled, IsMarketDataEnabled = isMarketDataEnabled ?? current.IsMarketDataEnabled };
        await repository.UpsertInstrumentAsync(updated, cancellationToken);
        await AuditRiskChangeAsync(OperatorAuditEventType.InstrumentControlUpdated, "Instrument controls updated.", "Instrument", id.Value.ToString("D"), reason, current, updated, null, cancellationToken);
        return updated;
    }

    public async Task<Venue> UpdateVenueControlsAsync(VenueId id, bool? isTradingEnabled, bool? isReportImportEnabled, bool? isMarketDataEnabled, string reason, CancellationToken cancellationToken)
    {
        RequireReason(reason);
        var state = await repository.LoadStateAsync(cancellationToken);
        var current = state.Venues.FirstOrDefault(x => x.Id == id) ?? throw new DomainRuleViolationException("Venue not found.");
        var updated = current with { IsTradingEnabled = isTradingEnabled ?? current.IsTradingEnabled, IsReportImportEnabled = isReportImportEnabled ?? current.IsReportImportEnabled, IsMarketDataEnabled = isMarketDataEnabled ?? current.IsMarketDataEnabled };
        await repository.UpsertVenueAsync(updated, cancellationToken);
        await AuditRiskChangeAsync(OperatorAuditEventType.VenueControlUpdated, "Venue controls updated.", "Venue", id.Value.ToString("D"), reason, current, updated, null, cancellationToken);
        return updated;
    }

    private Task AuditRiskChangeAsync(OperatorAuditEventType eventType, string description, string entityType, string entityId, string reason, object? before, object? after, object? metadata, CancellationToken cancellationToken)
        => audit.RecordAsync(new OperatorAuditRecordRequest(eventType, OperatorAuditSeverity.Info, OperatorAuditResult.Succeeded, "Api", description, entityType, entityId, reason, before, after, metadata), cancellationToken);

    private static Fund? ResolveFund(PlatformState state, string fundCode)
        => state.Funds.FirstOrDefault(x => x.Name.Equals(fundCode, StringComparison.OrdinalIgnoreCase) && x.IsEnabled)
            ?? (fundCode.Equals("QQ_MASTER", StringComparison.OrdinalIgnoreCase) ? state.Funds.FirstOrDefault(x => x.IsEnabled) : null);

    private static RiskLimitSet? SelectActive(PlatformState state, FundId fundId, string? modelName)
        => state.RiskLimitSets.Where(x => x.FundId == fundId && x.IsActive && x.Status == RiskLimitSetStatus.Active && (string.IsNullOrWhiteSpace(modelName) || string.Equals(x.ModelName, modelName, StringComparison.OrdinalIgnoreCase))).OrderByDescending(x => x.Version).FirstOrDefault();

    private static void EnsureDraft(PlatformState state, Guid riskLimitSetId)
    {
        var set = state.RiskLimitSets.FirstOrDefault(x => x.Id == riskLimitSetId) ?? throw new DomainRuleViolationException("Risk limit set not found.");
        if (set.Status != RiskLimitSetStatus.Draft || set.IsActive)
        {
            throw new DomainRuleViolationException("Only draft inactive risk limit sets can be edited. Clone the active set first.");
        }
    }

    private static void RequireReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainRuleViolationException("A reason is required for risk configuration changes.");
        }
    }
}

public sealed class InMemoryOperatorAuditRepository(PlatformState state) : IOperatorAuditRepository
{
    private readonly object _sync = new();

    public Task AddAsync(OperatorAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        lock (_sync) state.OperatorAuditEvents.Add(auditEvent);
        return Task.CompletedTask;
    }

    public Task<OperatorAuditEvent?> GetAsync(OperatorAuditEventId id, CancellationToken cancellationToken)
    {
        lock (_sync) return Task.FromResult(state.OperatorAuditEvents.FirstOrDefault(x => x.Id == id));
    }

    public Task<IReadOnlyList<OperatorAuditEvent>> GetRecentAsync(OperatorAuditEventFilter filter, CancellationToken cancellationToken)
    {
        lock (_sync) return Task.FromResult<IReadOnlyList<OperatorAuditEvent>>(ApplyFilter(state.OperatorAuditEvents, filter).ToList());
    }

    public Task<IReadOnlyList<OperatorAuditEvent>> GetByEntityAsync(string entityType, string entityId, int limit, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyList<OperatorAuditEvent>>(state.OperatorAuditEvents
                .Where(x => string.Equals(x.EntityType, entityType, StringComparison.OrdinalIgnoreCase) && string.Equals(x.EntityId, entityId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.OccurredAtUtc)
                .Take(Math.Clamp(limit, 1, 500))
                .ToList());
        }
    }

    public Task<IReadOnlyList<OperatorAuditEvent>> GetByCorrelationIdAsync(string correlationId, int limit, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyList<OperatorAuditEvent>>(state.OperatorAuditEvents
                .Where(x => string.Equals(x.CorrelationId, correlationId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.OccurredAtUtc)
                .Take(Math.Clamp(limit, 1, 500))
                .ToList());
        }
    }

    private static IEnumerable<OperatorAuditEvent> ApplyFilter(IEnumerable<OperatorAuditEvent> events, OperatorAuditEventFilter filter)
    {
        var query = events;
        if (filter.Severity is not null) query = query.Where(x => x.Severity == filter.Severity.Value);
        if (filter.EventType is not null) query = query.Where(x => x.EventType == filter.EventType.Value);
        if (!string.IsNullOrWhiteSpace(filter.EntityType)) query = query.Where(x => string.Equals(x.EntityType, filter.EntityType, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.EntityId)) query = query.Where(x => string.Equals(x.EntityId, filter.EntityId, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.CorrelationId)) query = query.Where(x => string.Equals(x.CorrelationId, filter.CorrelationId, StringComparison.OrdinalIgnoreCase));
        if (filter.FromUtc is not null) query = query.Where(x => x.OccurredAtUtc >= filter.FromUtc);
        if (filter.ToUtc is not null) query = query.Where(x => x.OccurredAtUtc <= filter.ToUtc);
        return query.OrderByDescending(x => x.OccurredAtUtc).Take(Math.Clamp(filter.Limit, 1, 500));
    }
}

public sealed class InMemoryExceptionCaseRepository(PlatformState state) : IExceptionCaseRepository
{
    private readonly object _sync = new();

    public Task AddCaseAsync(ExceptionCase exceptionCase, ExceptionCaseAction action, ExceptionCaseLink? link, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (!state.ExceptionCases.Any(x => x.Id == exceptionCase.Id)) state.ExceptionCases.Add(exceptionCase);
            if (link is not null && !state.ExceptionCaseLinks.Any(x => x.SourceEntityType == link.SourceEntityType && x.SourceEntityId == link.SourceEntityId)) state.ExceptionCaseLinks.Add(link);
            state.ExceptionCaseActions.Add(action);
        }

        return Task.CompletedTask;
    }

    public Task UpdateCaseAsync(ExceptionCase exceptionCase, ExceptionCaseAction action, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var index = state.ExceptionCases.FindIndex(x => x.Id == exceptionCase.Id);
            if (index < 0) throw new DomainRuleViolationException("Exception case was not found.");
            state.ExceptionCases[index] = exceptionCase;
            state.ExceptionCaseActions.Add(action);
        }

        return Task.CompletedTask;
    }

    public Task AddNoteAsync(ExceptionCaseNote note, ExceptionCaseAction action, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            state.ExceptionCaseNotes.Add(note);
            state.ExceptionCaseActions.Add(action);
        }

        return Task.CompletedTask;
    }

    public Task<ExceptionCase?> GetCaseAsync(ExceptionCaseId id, CancellationToken cancellationToken)
    {
        lock (_sync) return Task.FromResult(state.ExceptionCases.FirstOrDefault(x => x.Id == id));
    }

    public Task<ExceptionCaseLink?> GetLinkAsync(string sourceEntityType, string sourceEntityId, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult(state.ExceptionCaseLinks.FirstOrDefault(x =>
                string.Equals(x.SourceEntityType, sourceEntityType, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.SourceEntityId, sourceEntityId, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public Task<IReadOnlyList<ExceptionCase>> GetCasesAsync(ExceptionCaseFilter filter, CancellationToken cancellationToken)
    {
        lock (_sync) return Task.FromResult<IReadOnlyList<ExceptionCase>>(ApplyFilter(state.ExceptionCases, filter).ToList());
    }

    public Task<IReadOnlyList<ExceptionCaseAction>> GetActionsAsync(ExceptionCaseId id, CancellationToken cancellationToken)
    {
        lock (_sync) return Task.FromResult<IReadOnlyList<ExceptionCaseAction>>(state.ExceptionCaseActions.Where(x => x.CaseId == id).OrderBy(x => x.OccurredAtUtc).ToList());
    }

    public Task<IReadOnlyList<ExceptionCaseNote>> GetNotesAsync(ExceptionCaseId id, CancellationToken cancellationToken)
    {
        lock (_sync) return Task.FromResult<IReadOnlyList<ExceptionCaseNote>>(state.ExceptionCaseNotes.Where(x => x.CaseId == id).OrderBy(x => x.CreatedAtUtc).ToList());
    }

    private static IEnumerable<ExceptionCase> ApplyFilter(IEnumerable<ExceptionCase> cases, ExceptionCaseFilter filter)
    {
        var query = cases;
        if (filter.Status is not null) query = query.Where(x => x.Status == filter.Status.Value);
        if (filter.Severity is not null) query = query.Where(x => x.Severity == filter.Severity.Value);
        if (filter.Type is not null) query = query.Where(x => x.Type == filter.Type.Value);
        if (filter.Source is not null) query = query.Where(x => x.Source == filter.Source.Value);
        if (!string.IsNullOrWhiteSpace(filter.AssignedTo)) query = query.Where(x => string.Equals(x.AssignedTo, filter.AssignedTo, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.Instrument)) query = query.Where(x => string.Equals(x.Symbol, filter.Instrument, StringComparison.OrdinalIgnoreCase) || string.Equals(x.InstrumentId?.Value.ToString("D"), filter.Instrument, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.EntityType)) query = query.Where(x => string.Equals(x.EntityType, filter.EntityType, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.EntityId)) query = query.Where(x => string.Equals(x.EntityId, filter.EntityId, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.CorrelationId)) query = query.Where(x => string.Equals(x.CorrelationId, filter.CorrelationId, StringComparison.OrdinalIgnoreCase));
        if (filter.FromUtc is not null) query = query.Where(x => x.CreatedAtUtc >= filter.FromUtc);
        if (filter.ToUtc is not null) query = query.Where(x => x.CreatedAtUtc <= filter.ToUtc);
        return query.OrderByDescending(x => x.UpdatedAtUtc).Take(Math.Clamp(filter.Limit, 1, 500));
    }
}

public sealed class ExceptionCaseService(IExceptionCaseRepository repository, IOperatorAuditService audit, IOperatorContext context, IClock clock, IIntradayRepository intradayRepository) : IExceptionCaseService
{
    public async Task<ExceptionCase?> CreateOrUpdateFromReconciliationBreakAsync(ReconciliationRun run, ReconciliationBreak reconciliationBreak, CancellationToken cancellationToken)
    {
        if (!ShouldCreateCase(reconciliationBreak.Severity)) return null;
        var state = await intradayRepository.LoadStateAsync(cancellationToken);
        var symbol = reconciliationBreak.InstrumentId is null ? null : state.Instruments.FirstOrDefault(x => x.Id == reconciliationBreak.InstrumentId.Value)?.Symbol;
        return await CreateFromSourceAsync(
            "ReconciliationBreak",
            reconciliationBreak.Id.ToString("D"),
            MapSeverity(reconciliationBreak.Severity),
            MapType(reconciliationBreak.Type, false),
            ExceptionCaseSource.IntradayReconciliation,
            $"Intraday {reconciliationBreak.Type}",
            reconciliationBreak.Description,
            reconciliationBreak.InstrumentId,
            symbol,
            new { run.Id, run.ModelRunId, run.Phase, reconciliationBreak.Type, reconciliationBreak.Severity },
            cancellationToken);
    }

    public async Task<ExceptionCase?> CreateOrUpdateFromEodBreakAsync(EodReconciliationRun run, EodReconciliationBreak reconciliationBreak, CancellationToken cancellationToken)
    {
        if (!ShouldCreateCase(reconciliationBreak.Severity)) return null;
        var state = await intradayRepository.LoadStateAsync(cancellationToken);
        var symbol = reconciliationBreak.InstrumentId is null ? null : state.Instruments.FirstOrDefault(x => x.Id == reconciliationBreak.InstrumentId.Value)?.Symbol;
        return await CreateFromSourceAsync(
            "EodReconciliationBreak",
            reconciliationBreak.Id.ToString("D"),
            MapSeverity(reconciliationBreak.Severity),
            MapType(reconciliationBreak.Type, true),
            ExceptionCaseSource.EodReconciliation,
            $"EOD {reconciliationBreak.Type}",
            reconciliationBreak.Description,
            reconciliationBreak.InstrumentId,
            symbol,
            new { run.Id, run.ReportDate, reconciliationBreak.Type, reconciliationBreak.Severity, reconciliationBreak.BrokerExecutionId, reconciliationBreak.InternalFillId },
            cancellationToken);
    }

    public async Task<ExceptionCase> CreateManualCaseAsync(CreateExceptionCaseRequest request, CancellationToken cancellationToken)
    {
        RequireText(request.Title, "Exception case title is required.");
        RequireText(request.Description, "Exception case description is required.");
        var now = clock.UtcNow;
        var actor = context.Current;
        var exceptionCase = new ExceptionCase(
            ExceptionCaseId.New(),
            now,
            now,
            ExceptionCaseStatus.Open,
            request.Severity,
            request.Type,
            request.Source,
            request.Title.Trim(),
            request.Description.Trim(),
            request.EntityType,
            request.EntityId,
            request.InstrumentId,
            request.Symbol,
            context.CorrelationId,
            request.AssignedTo,
            null,
            null,
            null,
            null,
            null,
            null,
            OperatorAuditService.SerializeSanitized(request.Metadata));
        var action = NewAction(exceptionCase.Id, ExceptionCaseActionType.Created, null, exceptionCase.Status, null, null, request.Metadata);
        await repository.AddCaseAsync(exceptionCase, action, null, cancellationToken);
        await AuditAsync(OperatorAuditEventType.ExceptionCaseCreated, OperatorAuditResult.Succeeded, OperatorAuditSeverity.Info, "Manual exception case created.", exceptionCase, null, request.Metadata, cancellationToken);
        return exceptionCase;
    }

    public Task<IReadOnlyList<ExceptionCase>> GetCasesAsync(ExceptionCaseFilter filter, CancellationToken cancellationToken)
        => repository.GetCasesAsync(filter, cancellationToken);

    public Task<ExceptionCase?> GetCaseAsync(ExceptionCaseId id, CancellationToken cancellationToken)
        => repository.GetCaseAsync(id, cancellationToken);

    public Task<IReadOnlyList<ExceptionCaseAction>> GetActionsAsync(ExceptionCaseId id, CancellationToken cancellationToken)
        => repository.GetActionsAsync(id, cancellationToken);

    public Task<IReadOnlyList<ExceptionCaseNote>> GetNotesAsync(ExceptionCaseId id, CancellationToken cancellationToken)
        => repository.GetNotesAsync(id, cancellationToken);

    public Task<ExceptionCase> AcknowledgeAsync(ExceptionCaseId id, string? reason, CancellationToken cancellationToken)
        => TransitionAsync(id, ExceptionCaseStatus.Acknowledged, ExceptionCaseActionType.Acknowledged, OperatorAuditEventType.ExceptionCaseAcknowledged, reason, false, cancellationToken);

    public async Task<ExceptionCase> AssignAsync(ExceptionCaseId id, string assignedTo, CancellationToken cancellationToken)
    {
        RequireText(assignedTo, "AssignedTo is required.");
        var current = await RequireCaseAsync(id, cancellationToken);
        EnsureCanEdit(current);
        var updated = current with { AssignedTo = assignedTo.Trim(), UpdatedAtUtc = clock.UtcNow };
        var action = NewAction(id, ExceptionCaseActionType.Assigned, current.Status, updated.Status, $"Assigned to {assignedTo.Trim()}.", null, new { assignedTo = assignedTo.Trim() });
        await repository.UpdateCaseAsync(updated, action, cancellationToken);
        await AuditAsync(OperatorAuditEventType.ExceptionCaseAssigned, OperatorAuditResult.Succeeded, OperatorAuditSeverity.Info, "Exception case assigned.", updated, null, new { previousStatus = current.Status, updated.Status, assignedTo = assignedTo.Trim() }, cancellationToken);
        return updated;
    }

    public Task<ExceptionCase> MarkInvestigatingAsync(ExceptionCaseId id, string? reason, CancellationToken cancellationToken)
        => TransitionAsync(id, ExceptionCaseStatus.Investigating, ExceptionCaseActionType.MarkedInvestigating, OperatorAuditEventType.ExceptionCaseInvestigating, reason, false, cancellationToken);

    public Task<ExceptionCase> ResolveAsync(ExceptionCaseId id, string reason, CancellationToken cancellationToken)
        => TransitionAsync(id, ExceptionCaseStatus.Resolved, ExceptionCaseActionType.Resolved, OperatorAuditEventType.ExceptionCaseResolved, reason, true, cancellationToken);

    public Task<ExceptionCase> MarkFalsePositiveAsync(ExceptionCaseId id, string reason, CancellationToken cancellationToken)
        => TransitionAsync(id, ExceptionCaseStatus.FalsePositive, ExceptionCaseActionType.MarkedFalsePositive, OperatorAuditEventType.ExceptionCaseFalsePositive, reason, true, cancellationToken);

    public Task<ExceptionCase> WaiveAsync(ExceptionCaseId id, string reason, CancellationToken cancellationToken)
        => TransitionAsync(id, ExceptionCaseStatus.Waived, ExceptionCaseActionType.Waived, OperatorAuditEventType.ExceptionCaseWaived, reason, true, cancellationToken);

    public Task<ExceptionCase> ReopenAsync(ExceptionCaseId id, string? reason, CancellationToken cancellationToken)
        => TransitionAsync(id, ExceptionCaseStatus.Open, ExceptionCaseActionType.Reopened, OperatorAuditEventType.ExceptionCaseReopened, reason, false, cancellationToken);

    public async Task<ExceptionCaseNote> AddNoteAsync(ExceptionCaseId id, string note, CancellationToken cancellationToken)
    {
        RequireText(note, "Exception note is required.");
        var current = await RequireCaseAsync(id, cancellationToken);
        var actor = context.Current;
        var now = clock.UtcNow;
        var exceptionNote = new ExceptionCaseNote(ExceptionCaseNoteId.New(), id, now, actor.ActorDisplayName, note.Trim(), context.CorrelationId);
        var action = NewAction(id, ExceptionCaseActionType.NoteAdded, current.Status, current.Status, null, note.Trim(), null);
        await repository.AddNoteAsync(exceptionNote, action, cancellationToken);
        await AuditAsync(OperatorAuditEventType.ExceptionCaseNoteAdded, OperatorAuditResult.Succeeded, OperatorAuditSeverity.Info, "Exception case note added.", current, null, new { note = note.Trim() }, cancellationToken);
        return exceptionNote;
    }

    private async Task<ExceptionCase?> CreateFromSourceAsync(string sourceEntityType, string sourceEntityId, ExceptionCaseSeverity severity, ExceptionCaseType type, ExceptionCaseSource source, string title, string description, InstrumentId? instrumentId, string? symbol, object metadata, CancellationToken cancellationToken)
    {
        var existingLink = await repository.GetLinkAsync(sourceEntityType, sourceEntityId, cancellationToken);
        if (existingLink is not null)
        {
            var existing = await repository.GetCaseAsync(existingLink.CaseId, cancellationToken);
            if (existing is not null) return existing;
        }

        var now = clock.UtcNow;
        var exceptionCase = new ExceptionCase(ExceptionCaseId.New(), now, now, ExceptionCaseStatus.Open, severity, type, source, title, description, sourceEntityType, sourceEntityId, instrumentId, symbol, context.CorrelationId, null, null, null, null, null, null, null, OperatorAuditService.SerializeSanitized(metadata));
        var action = NewAction(exceptionCase.Id, ExceptionCaseActionType.Created, null, exceptionCase.Status, null, null, metadata);
        var link = new ExceptionCaseLink(Guid.NewGuid(), exceptionCase.Id, sourceEntityType, sourceEntityId, now);
        await repository.AddCaseAsync(exceptionCase, action, link, cancellationToken);
        await AuditAsync(OperatorAuditEventType.ExceptionCaseCreated, OperatorAuditResult.Succeeded, severity is ExceptionCaseSeverity.Critical or ExceptionCaseSeverity.Blocking ? OperatorAuditSeverity.Warning : OperatorAuditSeverity.Info, "Exception case created from break.", exceptionCase, null, metadata, cancellationToken);
        return exceptionCase;
    }

    private async Task<ExceptionCase> TransitionAsync(ExceptionCaseId id, ExceptionCaseStatus nextStatus, ExceptionCaseActionType actionType, OperatorAuditEventType eventType, string? reason, bool requireReason, CancellationToken cancellationToken)
    {
        if (requireReason) RequireText(reason, "A reason is required for this exception case action.");
        var current = await RequireCaseAsync(id, cancellationToken);
        EnsureTransition(current.Status, nextStatus);
        var now = clock.UtcNow;
        var actor = context.Current;
        var updated = current with
        {
            Status = nextStatus,
            UpdatedAtUtc = now,
            AcknowledgedAtUtc = nextStatus == ExceptionCaseStatus.Acknowledged ? now : current.AcknowledgedAtUtc,
            AcknowledgedBy = nextStatus == ExceptionCaseStatus.Acknowledged ? actor.ActorDisplayName : current.AcknowledgedBy,
            ResolvedAtUtc = nextStatus is ExceptionCaseStatus.Resolved or ExceptionCaseStatus.FalsePositive or ExceptionCaseStatus.Waived or ExceptionCaseStatus.Closed ? now : current.ResolvedAtUtc,
            ResolvedBy = nextStatus is ExceptionCaseStatus.Resolved or ExceptionCaseStatus.FalsePositive or ExceptionCaseStatus.Waived or ExceptionCaseStatus.Closed ? actor.ActorDisplayName : current.ResolvedBy,
            ResolutionReason = nextStatus is ExceptionCaseStatus.Resolved or ExceptionCaseStatus.FalsePositive ? reason?.Trim() : current.ResolutionReason,
            WaiverReason = nextStatus == ExceptionCaseStatus.Waived ? reason?.Trim() : current.WaiverReason
        };
        var action = NewAction(id, actionType, current.Status, nextStatus, reason, null, new { previousStatus = current.Status, newStatus = nextStatus });
        await repository.UpdateCaseAsync(updated, action, cancellationToken);
        await AuditAsync(eventType, OperatorAuditResult.Succeeded, AuditSeverityFor(updated), $"Exception case {nextStatus}.", updated, reason, new { previousStatus = current.Status, newStatus = nextStatus, reason }, cancellationToken);
        return updated;
    }

    private async Task<ExceptionCase> RequireCaseAsync(ExceptionCaseId id, CancellationToken cancellationToken)
        => await repository.GetCaseAsync(id, cancellationToken) ?? throw new DomainRuleViolationException("Exception case was not found.");

    private ExceptionCaseAction NewAction(ExceptionCaseId id, ExceptionCaseActionType actionType, ExceptionCaseStatus? fromStatus, ExceptionCaseStatus? toStatus, string? reason, string? note, object? metadata)
    {
        var actor = context.Current;
        return new ExceptionCaseAction(ExceptionCaseActionId.New(), id, actionType, actor.ActorId, actor.ActorDisplayName, clock.UtcNow, fromStatus, toStatus, string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), string.IsNullOrWhiteSpace(note) ? null : note.Trim(), OperatorAuditService.SerializeSanitized(metadata), context.CorrelationId);
    }

    private Task AuditAsync(OperatorAuditEventType eventType, OperatorAuditResult result, OperatorAuditSeverity severity, string description, ExceptionCase exceptionCase, string? reason, object? metadata, CancellationToken cancellationToken)
        => audit.RecordAsync(new OperatorAuditRecordRequest(eventType, severity, result, "ExceptionCaseService", description, "ExceptionCase", exceptionCase.Id.Value.ToString("D"), reason, Metadata: metadata), cancellationToken);

    private static void EnsureTransition(ExceptionCaseStatus current, ExceptionCaseStatus next)
    {
        if (current == next) return;
        var allowed = next switch
        {
            ExceptionCaseStatus.Acknowledged => current == ExceptionCaseStatus.Open,
            ExceptionCaseStatus.Investigating => current is ExceptionCaseStatus.Open or ExceptionCaseStatus.Acknowledged,
            ExceptionCaseStatus.Resolved or ExceptionCaseStatus.FalsePositive or ExceptionCaseStatus.Waived or ExceptionCaseStatus.Closed => current is ExceptionCaseStatus.Open or ExceptionCaseStatus.Acknowledged or ExceptionCaseStatus.Investigating,
            ExceptionCaseStatus.Open => current is ExceptionCaseStatus.Resolved or ExceptionCaseStatus.FalsePositive or ExceptionCaseStatus.Waived or ExceptionCaseStatus.Closed,
            _ => false
        };
        if (!allowed) throw new DomainRuleViolationException($"Invalid exception case transition from {current} to {next}.");
    }

    private static void EnsureCanEdit(ExceptionCase exceptionCase)
    {
        if (exceptionCase.Status == ExceptionCaseStatus.Closed)
        {
            throw new DomainRuleViolationException("Closed exception cases cannot be edited.");
        }
    }

    private static void RequireText(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new DomainRuleViolationException(message);
    }

    private static bool ShouldCreateCase(ReconciliationBreakSeverity severity)
        => severity is ReconciliationBreakSeverity.Blocking or ReconciliationBreakSeverity.Warning;

    private static ExceptionCaseSeverity MapSeverity(ReconciliationBreakSeverity severity)
        => severity switch
        {
            ReconciliationBreakSeverity.Blocking => ExceptionCaseSeverity.Blocking,
            ReconciliationBreakSeverity.Warning => ExceptionCaseSeverity.Warning,
            _ => ExceptionCaseSeverity.Info
        };

    private static ExceptionCaseType MapType(ReconciliationBreakType type, bool eod)
        => type switch
        {
            ReconciliationBreakType.InternalBrokerPositionMismatch => ExceptionCaseType.PositionMismatch,
            ReconciliationBreakType.InternalFillMissingInBrokerReport => ExceptionCaseType.InternalFillMissingInBrokerReport,
            ReconciliationBreakType.BrokerFillMissingInternally => ExceptionCaseType.BrokerFillMissingInternally,
            ReconciliationBreakType.QuantityMismatch => ExceptionCaseType.QuantityMismatch,
            ReconciliationBreakType.PriceMismatch => ExceptionCaseType.PriceMismatch,
            ReconciliationBreakType.SideMismatch => ExceptionCaseType.SideMismatch,
            ReconciliationBreakType.InstrumentMismatch => ExceptionCaseType.InstrumentMismatch,
            _ => eod ? ExceptionCaseType.EodBreak : ExceptionCaseType.IntradayBreak
        };

    private static OperatorAuditSeverity AuditSeverityFor(ExceptionCase exceptionCase)
        => exceptionCase.Severity is ExceptionCaseSeverity.Critical or ExceptionCaseSeverity.Blocking ? OperatorAuditSeverity.Warning : OperatorAuditSeverity.Info;
}

public sealed class InMemoryIntradayRepository(PlatformState state) : IIntradayRepository
{
    private readonly object _sync = new();

    public Task<PlatformState> LoadStateAsync(CancellationToken cancellationToken) => Task.FromResult(state);

    public Task<ModelRun?> GetNextUnprocessedModelRunAsync(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult(state.ModelRuns.OrderBy(x => x.ReceivedAtUtc).FirstOrDefault(x => !x.IsProcessed));
        }
    }

    public Task<ModelRun?> GetModelRunAsync(ModelRunId modelRunId, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult(state.ModelRuns.FirstOrDefault(x => x.Id == modelRunId));
        }
    }

    public Task AddModelRunAsync(ModelRun modelRun, IReadOnlyList<TargetWeight> weights, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (state.ModelRuns.Any(x => x.Id == modelRun.Id))
            {
                return Task.CompletedTask;
            }

            state.ModelRuns.Add(modelRun);
            state.TargetWeights.AddRange(weights);
        }

        return Task.CompletedTask;
    }

    public Task MarkModelRunProcessedAsync(ModelRunId modelRunId, ModelRunStatus status, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var index = state.ModelRuns.FindIndex(x => x.Id == modelRunId);
            if (index >= 0)
            {
                var run = state.ModelRuns[index];
                state.ModelRuns[index] = run with { IsProcessed = true, Status = status };
            }
        }

        return Task.CompletedTask;
    }

    public Task SaveReconciliationAsync(ReconciliationRun run, IReadOnlyList<ReconciliationBreak> breaks, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (state.ReconciliationRuns.Any(x => x.ModelRunId == run.ModelRunId && x.Phase == run.Phase))
            {
                return Task.CompletedTask;
            }

            state.ReconciliationRuns.Add(run);
            state.ReconciliationBreaks.AddRange(breaks);
        }

        return Task.CompletedTask;
    }

    public Task SaveTargetAndDriftAsync(TargetPosition targetPosition, DriftSnapshot driftSnapshot, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (!state.TargetPositions.Any(x => x.ModelRunId == targetPosition.ModelRunId && x.InstrumentId == targetPosition.InstrumentId))
            {
                state.TargetPositions.Add(targetPosition);
            }

            if (!state.DriftSnapshots.Any(x => x.ModelRunId == driftSnapshot.ModelRunId && x.InstrumentId == driftSnapshot.InstrumentId))
            {
                state.DriftSnapshots.Add(driftSnapshot);
            }
        }

        return Task.CompletedTask;
    }

    public Task AddTradeIntentAsync(TradeIntent intent, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (!state.TradeIntents.Any(x => x.Id == intent.Id))
            {
                state.TradeIntents.Add(intent);
            }
        }

        return Task.CompletedTask;
    }

    public Task AddRiskDecisionAsync(RiskDecision decision, IReadOnlyList<RiskDecisionDetail>? details, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (state.RiskDecisions.Any(x => x.TradeIntentId == decision.TradeIntentId))
            {
                return Task.CompletedTask;
            }

            state.RiskDecisions.Add(decision);
            if (details is not null)
            {
                state.RiskDecisionDetails.AddRange(details);
            }
        }

        return Task.CompletedTask;
    }

    public Task AddOrdersAsync(ParentOrder parentOrder, ChildOrder childOrder, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (state.ParentOrders.Any(x => x.ClientOrderId == parentOrder.ClientOrderId) || state.ChildOrders.Any(x => x.ClientOrderId == childOrder.ClientOrderId))
            {
                return Task.CompletedTask;
            }

            state.ParentOrders.Add(parentOrder);
            state.ChildOrders.Add(childOrder);
        }

        return Task.CompletedTask;
    }

    public Task AddExecutionReportAsync(ExecutionReport report, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (state.ExecutionReports.Any(x => x.Id == report.Id))
            {
                return Task.CompletedTask;
            }

            state.ExecutionReports.Add(report);
            var childIndex = state.ChildOrders.FindIndex(x => x.Id == report.ChildOrderId);
            if (childIndex >= 0)
            {
                var machine = new OrderStateMachine();
                var child = state.ChildOrders[childIndex];
                var childStatus = machine.Transition(child.Status, report.ExecutionReportType);
                state.ChildOrders[childIndex] = child with { Status = childStatus };

                var parentIndex = state.ParentOrders.FindIndex(x => x.Id == child.ParentOrderId);
                if (parentIndex >= 0)
                {
                    var parent = state.ParentOrders[parentIndex];
                    var parentStatus = report.ExecutionReportType switch
                    {
                        ExecutionReportType.OrderReject => OrderStatus.Rejected,
                        ExecutionReportType.Fill => OrderStatus.Filled,
                        ExecutionReportType.PartialFill => OrderStatus.PartiallyFilled,
                        ExecutionReportType.Expired when parent.Status == OrderStatus.PartiallyFilled => OrderStatus.PartiallyFilled,
                        ExecutionReportType.Expired => OrderStatus.Expired,
                        _ => parent.Status
                    };
                    state.ParentOrders[parentIndex] = parent with { Status = parentStatus };
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task<bool> TryAddFillAsync(Fill fill, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (state.Fills.Any(x => x.VenueId == fill.VenueId && x.BrokerExecutionId == fill.BrokerExecutionId))
            {
                return Task.FromResult(false);
            }

            state.Fills.Add(fill);
            return Task.FromResult(true);
        }
    }

    public Task AddPositionLedgerEventAsync(PositionLedgerEvent ledgerEvent, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            state.PositionLedger.Add(ledgerEvent);
        }

        return Task.CompletedTask;
    }

    public Task SetKillSwitchAsync(bool isActive, string? reason, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            state.KillSwitch = new KillSwitchState(state.KillSwitch.Id, isActive, reason, DateTimeOffset.UtcNow);
        }

        return Task.CompletedTask;
    }

    public Task UpsertRiskLimitSetAsync(RiskLimitSet riskLimitSet, CancellationToken cancellationToken)
    {
        lock (_sync) Upsert(state.RiskLimitSets, riskLimitSet, x => x.Id == riskLimitSet.Id);
        return Task.CompletedTask;
    }

    public Task UpsertRiskLimitAsync(RiskLimit riskLimit, CancellationToken cancellationToken)
    {
        lock (_sync) Upsert(state.RiskLimits, riskLimit, x => x.Id == riskLimit.Id);
        return Task.CompletedTask;
    }

    public Task UpsertInstrumentRiskLimitAsync(InstrumentRiskLimit instrumentRiskLimit, CancellationToken cancellationToken)
    {
        lock (_sync) Upsert(state.InstrumentRiskLimits, instrumentRiskLimit, x => x.Id == instrumentRiskLimit.Id);
        return Task.CompletedTask;
    }

    public Task UpsertVenueRiskLimitAsync(VenueRiskLimit venueRiskLimit, CancellationToken cancellationToken)
    {
        lock (_sync) Upsert(state.VenueRiskLimits, venueRiskLimit, x => x.Id == venueRiskLimit.Id);
        return Task.CompletedTask;
    }

    public Task UpsertTradingWindowAsync(TradingWindow tradingWindow, CancellationToken cancellationToken)
    {
        lock (_sync) Upsert(state.TradingWindows, tradingWindow, x => x.Id == tradingWindow.Id);
        return Task.CompletedTask;
    }

    public Task UpsertInstrumentAsync(Instrument instrument, CancellationToken cancellationToken)
    {
        lock (_sync) Upsert(state.Instruments, instrument, x => x.Id == instrument.Id);
        return Task.CompletedTask;
    }

    public Task UpsertVenueAsync(Venue venue, CancellationToken cancellationToken)
    {
        lock (_sync) Upsert(state.Venues, venue, x => x.Id == venue.Id);
        return Task.CompletedTask;
    }

    private static void Upsert<T>(List<T> rows, T row, Func<T, bool> predicate)
    {
        var index = rows.FindIndex(x => predicate(x));
        if (index >= 0) rows[index] = row;
        else rows.Add(row);
    }
}

public sealed class InMemoryMarketDataSnapshotRepository(PlatformState state) : IMarketDataSnapshotRepository
{
    private readonly object _sync = new();

    public Task AddAsync(MarketDataSnapshot snapshot, CancellationToken cancellationToken)
        => AddRangeAsync([snapshot], cancellationToken);

    public Task AddRangeAsync(IReadOnlyList<MarketDataSnapshot> snapshots, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            foreach (var snapshot in snapshots)
            {
                snapshot.Validate();
                if (!state.MarketData.Any(x => x.Id == snapshot.Id))
                {
                    state.MarketData.Add(snapshot);
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task<MarketDataSnapshot?> GetLatestAsync(InstrumentId instrumentId, VenueId venueId, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult(state.MarketData.Where(x => x.InstrumentId == instrumentId && x.VenueId == venueId).OrderBy(x => x.SourceTimestampUtc).ThenBy(x => x.ReceivedAtUtc).LastOrDefault());
        }
    }

    public Task<IReadOnlyList<MarketDataSnapshot>> GetRangeAsync(InstrumentId instrumentId, VenueId venueId, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyList<MarketDataSnapshot>>(state.MarketData
                .Where(x => x.InstrumentId == instrumentId && x.VenueId == venueId && x.SourceTimestampUtc >= startUtc && x.SourceTimestampUtc < endUtc)
                .OrderBy(x => x.SourceTimestampUtc)
                .ToList());
        }
    }
}

public sealed class InMemoryMarketDataBarRepository(PlatformState state) : IMarketDataBarRepository
{
    private readonly object _sync = new();

    public Task<BarUpsertResult> UpsertAsync(MarketDataBar bar, CancellationToken cancellationToken)
    {
        bar.Validate();
        lock (_sync)
        {
            var index = state.MarketDataBars.FindIndex(x => x.InstrumentId == bar.InstrumentId && x.VenueId == bar.VenueId && x.Timeframe == bar.Timeframe && x.BarStartUtc == bar.BarStartUtc);
            if (index >= 0)
            {
                state.MarketDataBars[index] = bar with { Id = state.MarketDataBars[index].Id };
                return Task.FromResult(new BarUpsertResult(false));
            }

            state.MarketDataBars.Add(bar);
            return Task.FromResult(new BarUpsertResult(true));
        }
    }

    public Task<MarketDataBar?> GetAsync(InstrumentId instrumentId, VenueId venueId, BarTimeframe timeframe, DateTimeOffset barStartUtc, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult(state.MarketDataBars.FirstOrDefault(x => x.InstrumentId == instrumentId && x.VenueId == venueId && x.Timeframe == timeframe && x.BarStartUtc == barStartUtc));
        }
    }

    public Task<IReadOnlyList<MarketDataBar>> GetRangeAsync(InstrumentId instrumentId, VenueId venueId, BarTimeframe timeframe, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyList<MarketDataBar>>(state.MarketDataBars
                .Where(x => x.InstrumentId == instrumentId && x.VenueId == venueId && x.Timeframe == timeframe && x.BarStartUtc >= startUtc && x.BarStartUtc < endUtc)
                .OrderBy(x => x.BarStartUtc)
                .ToList());
        }
    }
}

public sealed class InMemoryBarBuildRunRepository(PlatformState state, IClock clock) : IBarBuildRunRepository
{
    private readonly object _sync = new();

    public Task AddAsync(BarBuildRun run, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            state.BarBuildRuns.Add(run);
        }

        return Task.CompletedTask;
    }

    public Task MarkCompletedAsync(BarBuildRunId runId, int barsCreated, int barsUpdated, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var index = state.BarBuildRuns.FindIndex(x => x.Id == runId);
            if (index >= 0)
            {
                var run = state.BarBuildRuns[index];
                state.BarBuildRuns[index] = run with { Status = BarBuildRunStatus.Completed, CompletedAtUtc = clock.UtcNow, BarsCreated = barsCreated, BarsUpdated = barsUpdated };
            }
        }

        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(BarBuildRunId runId, string errorMessage, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var index = state.BarBuildRuns.FindIndex(x => x.Id == runId);
            if (index >= 0)
            {
                var run = state.BarBuildRuns[index];
                state.BarBuildRuns[index] = run with { Status = BarBuildRunStatus.Failed, CompletedAtUtc = clock.UtcNow, ErrorMessage = errorMessage };
            }
        }

        return Task.CompletedTask;
    }
}

public sealed class InMemoryModelWeightBatchRepository(PlatformState state) : IModelWeightBatchRepository
{
    private readonly object _sync = new();

    public Task<ModelWeightBatch?> GetBatchAsync(ModelWeightBatchId batchId, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult(state.ModelWeightBatches.FirstOrDefault(x => x.Id == batchId));
        }
    }

    public Task<ModelWeightBatch?> GetBatchByExternalIdAsync(ModelWeightSourceSystem sourceSystem, string externalBatchId, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult(state.ModelWeightBatches.FirstOrDefault(x => x.SourceSystem == sourceSystem && x.ExternalBatchId == externalBatchId));
        }
    }

    public Task<IReadOnlyList<ModelWeightBatch>> GetRecentBatchesAsync(int limit, ModelWeightBatchStatus? status, ModelWeightSourceSystem? sourceSystem, string? modelName, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var query = state.ModelWeightBatches.AsEnumerable();
            if (status is not null) query = query.Where(x => x.Status == status);
            if (sourceSystem is not null) query = query.Where(x => x.SourceSystem == sourceSystem);
            if (!string.IsNullOrWhiteSpace(modelName)) query = query.Where(x => x.ModelName.Equals(modelName, StringComparison.OrdinalIgnoreCase));
            if (fromUtc is not null) query = query.Where(x => x.AsOfUtc >= fromUtc.Value);
            if (toUtc is not null) query = query.Where(x => x.AsOfUtc < toUtc.Value);
            return Task.FromResult<IReadOnlyList<ModelWeightBatch>>(query.OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(limit, 1, 500)).ToList());
        }
    }

    public Task<IReadOnlyList<ModelWeightBatch>> GetReadyBatchesAsync(int limit, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyList<ModelWeightBatch>>(state.ModelWeightBatches
                .Where(x => x.Status is ModelWeightBatchStatus.Ready or ModelWeightBatchStatus.Accepted)
                .OrderBy(x => x.AsOfUtc)
                .Take(Math.Clamp(limit, 1, 500))
                .ToList());
        }
    }

    public Task<IReadOnlyList<ModelWeightRow>> GetRowsAsync(ModelWeightBatchId batchId, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyList<ModelWeightRow>>(state.ModelWeightRows.Where(x => x.BatchId == batchId).OrderBy(x => x.CreatedAtUtc).ToList());
        }
    }

    public Task<IReadOnlyList<ModelWeightValidationIssue>> GetValidationIssuesAsync(ModelWeightBatchId batchId, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyList<ModelWeightValidationIssue>>(state.ModelWeightValidationIssues.Where(x => x.BatchId == batchId).OrderBy(x => x.CreatedAtUtc).ToList());
        }
    }

    public Task AddBatchAsync(ModelWeightBatch batch, IReadOnlyList<ModelWeightRow> rows, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (state.ModelWeightBatches.Any(x => x.SourceSystem == batch.SourceSystem && x.ExternalBatchId == batch.ExternalBatchId))
            {
                return Task.CompletedTask;
            }

            state.ModelWeightBatches.Add(batch);
            state.ModelWeightRows.AddRange(rows);
        }

        return Task.CompletedTask;
    }

    public Task UpdateBatchAsync(ModelWeightBatch batch, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var index = state.ModelWeightBatches.FindIndex(x => x.Id == batch.Id);
            if (index >= 0)
            {
                state.ModelWeightBatches[index] = batch;
            }
        }

        return Task.CompletedTask;
    }

    public Task AddValidationIssuesAsync(ModelWeightBatchId batchId, IReadOnlyList<ModelWeightValidationIssue> issues, bool replaceExisting, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (replaceExisting)
            {
                state.ModelWeightValidationIssues.RemoveAll(x => x.BatchId == batchId);
            }

            state.ModelWeightValidationIssues.AddRange(issues);
        }

        return Task.CompletedTask;
    }

    public Task MarkPromotedAsync(ModelWeightBatchId batchId, ModelRunId modelRunId, DateTimeOffset promotedAtUtc, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var index = state.ModelWeightBatches.FindIndex(x => x.Id == batchId);
            if (index >= 0)
            {
                var batch = state.ModelWeightBatches[index];
                state.ModelWeightBatches[index] = batch with { Status = ModelWeightBatchStatus.Promoted, PromotedAtUtc = promotedAtUtc, PromotedModelRunId = modelRunId, Message = "Promoted to model run." };
            }
        }

        return Task.CompletedTask;
    }
}

public sealed class BarBuilderService(
    PlatformState state,
    IMarketDataSnapshotRepository snapshotRepository,
    IMarketDataBarRepository barRepository,
    IBarBuildRunRepository buildRunRepository,
    IClock clock,
    BarBuilderOptions options) : IBarBuilderService
{
    public async Task<BarBuildResult> BuildBarsAsync(VenueId venueId, BarTimeframe timeframe, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken)
    {
        var run = new BarBuildRun(BarBuildRunId.New(), timeframe, clock.UtcNow, null, options.Source, options.BuilderVersion, BarBuildRunStatus.Started, null, 0, 0);
        await buildRunRepository.AddAsync(run, cancellationToken);
        var created = 0;
        var updated = 0;

        try
        {
            if (timeframe != BarTimeframe.FifteenMinutes)
            {
                throw new NotSupportedException("Only 15-minute bar building is implemented.");
            }

            foreach (var instrument in state.Instruments.Where(x => x.IsEnabled))
            {
                foreach (var (barStart, barEnd) in BarIntervalAlignment.EnumerateIntervals(startUtc, endUtc, timeframe))
                {
                    var snapshots = await snapshotRepository.GetRangeAsync(instrument.Id, venueId, barStart, barEnd, cancellationToken);
                    if (snapshots.Count == 0 && !options.CreateNoDataBars)
                    {
                        continue;
                    }

                    var bar = BuildBar(instrument.Id, venueId, timeframe, barStart, barEnd, snapshots, run.Id);
                    var result = await barRepository.UpsertAsync(bar, cancellationToken);
                    if (result.Created) created++;
                    else updated++;
                }
            }

            await buildRunRepository.MarkCompletedAsync(run.Id, created, updated, cancellationToken);
            return new BarBuildResult(run.Id, created, updated, BarBuildRunStatus.Completed);
        }
        catch (Exception ex)
        {
            await buildRunRepository.MarkFailedAsync(run.Id, ex.Message, cancellationToken);
            return new BarBuildResult(run.Id, created, updated, BarBuildRunStatus.Failed, ex.Message);
        }
    }

    public Task<BarBuildResult> BuildLatestFifteenMinuteBarsAsync(VenueId venueId, CancellationToken cancellationToken)
    {
        var end = BarIntervalAlignment.GetBarStart(clock.UtcNow, BarTimeframe.FifteenMinutes);
        return BuildBarsAsync(venueId, BarTimeframe.FifteenMinutes, end.AddMinutes(-15), end, cancellationToken);
    }

    private MarketDataBar BuildBar(InstrumentId instrumentId, VenueId venueId, BarTimeframe timeframe, DateTimeOffset barStart, DateTimeOffset barEnd, IReadOnlyList<MarketDataSnapshot> snapshots, BarBuildRunId runId)
    {
        var ordered = snapshots.OrderBy(x => x.SourceTimestampUtc).ToList();
        var isComplete = barEnd <= clock.UtcNow;
        var quality = ordered.Count switch
        {
            0 => isComplete ? BarQualityStatus.NoData : BarQualityStatus.Incomplete,
            _ when !isComplete => BarQualityStatus.Incomplete,
            _ when ordered.Count < options.FifteenMinuteMinimumObservationCount => BarQualityStatus.SparseData,
            _ => BarQualityStatus.Complete
        };

        decimal First(Func<MarketDataSnapshot, decimal> selector) => ordered.Count == 0 ? 0m : selector(ordered[0]);
        decimal Last(Func<MarketDataSnapshot, decimal> selector) => ordered.Count == 0 ? 0m : selector(ordered[^1]);
        decimal Max(Func<MarketDataSnapshot, decimal> selector) => ordered.Count == 0 ? 0m : ordered.Max(selector);
        decimal Min(Func<MarketDataSnapshot, decimal> selector) => ordered.Count == 0 ? 0m : ordered.Min(selector);
        decimal Avg(Func<MarketDataSnapshot, decimal> selector) => ordered.Count == 0 ? 0m : ordered.Sum(selector) / ordered.Count;

        return new MarketDataBar(
            MarketDataBarId.New(),
            instrumentId,
            venueId,
            timeframe,
            barStart,
            barEnd,
            options.Source,
            First(x => x.Bid), Max(x => x.Bid), Min(x => x.Bid), Last(x => x.Bid),
            First(x => x.Ask), Max(x => x.Ask), Min(x => x.Ask), Last(x => x.Ask),
            First(x => x.Mid), Max(x => x.Mid), Min(x => x.Mid), Last(x => x.Mid),
            First(x => x.Spread), Max(x => x.Spread), Min(x => x.Spread), Last(x => x.Spread), Avg(x => x.Spread),
            ordered.Count,
            ordered.FirstOrDefault()?.SourceTimestampUtc,
            ordered.LastOrDefault()?.SourceTimestampUtc,
            isComplete,
            quality,
            runId,
            options.BuilderVersion,
            clock.UtcNow);
    }
}

public sealed class ReferenceDataIntegrityService(IIntradayRepository repository, IClock clock) : IReferenceDataIntegrityService
{
    public async Task<ReferenceDataIntegrityResult> CheckAsync(CancellationToken cancellationToken)
    {
        var state = await repository.LoadStateAsync(cancellationToken);
        var now = clock.UtcNow;
        var issues = new List<ReferenceDataIntegrityIssue>();

        AddDuplicateIssues(issues, state.Funds.Where(x => x.IsEnabled), x => x.Name, ReferenceDataIntegrityIssueType.DuplicateFund, "enabled fund", now);
        AddDuplicateIssues(issues, state.BrokerAccounts.Where(x => x.IsEnabled), x => $"{x.FundId.Value:N}|{x.AccountCode}", ReferenceDataIntegrityIssueType.DuplicateBrokerAccount, "enabled broker account", now);
        AddDuplicateIssues(issues, state.Instruments.Where(x => x.IsEnabled), x => $"{x.Symbol}|{x.AssetClass}", ReferenceDataIntegrityIssueType.DuplicateInstrument, "enabled instrument", now);
        AddDuplicateIssues(issues, state.Venues.Where(x => x.IsEnabled), x => x.Name, ReferenceDataIntegrityIssueType.DuplicateVenue, "enabled venue", now);
        AddDuplicateIssues(issues, state.VenueInstrumentMappings.Where(x => x.IsEnabled), x => $"{x.VenueId.Value:N}|{x.InstrumentId.Value:N}", ReferenceDataIntegrityIssueType.DuplicateVenueInstrumentMapping, "enabled venue/instrument mapping", now);
        AddDuplicateIssues(issues, state.VenueInstrumentMappings.Where(x => x.IsEnabled), x => $"{x.VenueId.Value:N}|{x.VenueSymbol}", ReferenceDataIntegrityIssueType.DuplicateVenueInstrumentMapping, "enabled venue symbol mapping", now);
        AddDuplicateIssues(issues, state.RiskLimitSets.Where(x => x.IsActive && x.Status == RiskLimitSetStatus.Active), x => $"{x.FundId.Value:N}|{x.ModelName}", ReferenceDataIntegrityIssueType.DuplicateRiskLimitSet, "active risk limit set", now);
        AddDuplicateIssues(issues, state.RiskLimits, x => $"{x.RiskLimitSetId:N}|{x.Name}", ReferenceDataIntegrityIssueType.DuplicateRiskLimit, "risk limit", now);
        AddDuplicateIssues(issues, state.InstrumentRiskLimits.Where(x => x.IsEnabled), x => $"{x.RiskLimitSetId:N}|{x.InstrumentId.Value:N}", ReferenceDataIntegrityIssueType.DuplicateInstrumentRiskLimit, "enabled instrument risk limit", now);
        AddDuplicateIssues(issues, state.VenueRiskLimits.Where(x => x.IsEnabled), x => $"{x.RiskLimitSetId:N}|{x.VenueId.Value:N}", ReferenceDataIntegrityIssueType.DuplicateVenueRiskLimit, "enabled venue risk limit", now);
        AddDuplicateIssues(issues, state.TradingWindows.Where(x => x.IsEnabled), x => $"{x.FundId.Value:N}|{x.ModelName}|{x.DayOfWeek}", ReferenceDataIntegrityIssueType.DuplicateTradingWindow, "enabled trading window", now);
        AddAmbiguousCurrentKillSwitchIssue(issues, state.KillSwitchStates, now);

        var fund = RequireExactlyOne(issues, state.Funds.Where(x => x.Name == "QQ Intraday Fund").ToList(), x => x.IsEnabled, "QQ Intraday Fund", ReferenceDataIntegrityIssueType.DuplicateFund, now);
        var instrument = RequireExactlyOne(issues, state.Instruments.Where(x => x.Symbol == "EURUSD" && x.AssetClass == AssetClass.FxSpot).ToList(), x => x.IsEnabled, "EURUSD/FxSpot", ReferenceDataIntegrityIssueType.DuplicateInstrument, now);
        var venue = RequireExactlyOne(issues, state.Venues.Where(x => x.Name == "LMAX").ToList(), x => x.IsEnabled, "LMAX", ReferenceDataIntegrityIssueType.DuplicateVenue, now);

        if (fund is not null)
        {
            RequireExactlyOne(issues, state.BrokerAccounts.Where(x => x.FundId == fund.Id).ToList(), x => x.IsEnabled, $"BrokerAccount:{fund.Id.Value:N}", ReferenceDataIntegrityIssueType.DuplicateBrokerAccount, now);
            RequireExactlyOne(issues, state.RiskLimitSets.Where(x => x.FundId == fund.Id && x.ModelName == "IntradayFxModel" && x.IsActive && x.Status == RiskLimitSetStatus.Active).ToList(), _ => true, $"RiskLimitSet:{fund.Id.Value:N}:IntradayFxModel", ReferenceDataIntegrityIssueType.DuplicateRiskLimitSet, now);
            RequireAtLeastOne(issues, state.TradingWindows.Where(x => x.FundId == fund.Id && x.ModelName == "IntradayFxModel" && x.IsEnabled), "TradingWindow:IntradayFxModel", now);
        }

        if (venue is not null && instrument is not null)
        {
            RequireExactlyOne(issues, state.VenueInstrumentMappings.Where(x => x.VenueId == venue.Id && x.InstrumentId == instrument.Id).ToList(), x => x.IsEnabled, "LMAX:EURUSD", ReferenceDataIntegrityIssueType.DuplicateVenueInstrumentMapping, now);
        }

        if (state.KillSwitchStates.Count == 0 && state.KillSwitch.UpdatedAtUtc == DateTimeOffset.UnixEpoch)
        {
            issues.Add(NewIssue(ReferenceDataIntegrityIssueType.MissingRequiredReferenceData, "KillSwitchState", "No kill-switch state exists.", now));
        }

        return new ReferenceDataIntegrityResult(
            now,
            issues.Count(x => x.Severity == ReferenceDataIntegritySeverity.Blocking),
            issues.Count(x => x.Severity == ReferenceDataIntegritySeverity.Warning),
            issues);
    }

    private static void AddDuplicateIssues<T>(List<ReferenceDataIntegrityIssue> issues, IEnumerable<T> values, Func<T, string> keySelector, ReferenceDataIntegrityIssueType type, string label, DateTimeOffset now)
    {
        foreach (var group in values.GroupBy(keySelector, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1))
        {
            issues.Add(NewIssue(type, group.Key, $"Duplicate {label} rows exist for key '{group.Key}'.", now));
        }
    }

    private static T? RequireExactlyOne<T>(List<ReferenceDataIntegrityIssue> issues, IReadOnlyList<T> values, Func<T, bool> enabledSelector, string key, ReferenceDataIntegrityIssueType duplicateType, DateTimeOffset now)
    {
        if (values.Count == 0)
        {
            issues.Add(NewIssue(ReferenceDataIntegrityIssueType.MissingRequiredReferenceData, key, $"Required reference data is missing for '{key}'.", now));
            return default;
        }

        var enabled = values.Where(enabledSelector).ToList();
        if (enabled.Count == 0)
        {
            issues.Add(NewIssue(ReferenceDataIntegrityIssueType.DisabledRequiredReferenceData, key, $"Required reference data exists but is disabled for '{key}'.", now));
            return default;
        }

        if (enabled.Count > 1)
        {
            issues.Add(NewIssue(duplicateType, key, $"Required reference data is ambiguous for '{key}'.", now));
            return default;
        }

        return enabled[0];
    }

    private static void RequireAtLeastOne<T>(List<ReferenceDataIntegrityIssue> issues, IEnumerable<T> values, string key, DateTimeOffset now)
    {
        if (!values.Any())
        {
            issues.Add(NewIssue(ReferenceDataIntegrityIssueType.MissingRequiredReferenceData, key, $"Required reference data is missing for '{key}'.", now));
        }
    }

    private static void AddAmbiguousCurrentKillSwitchIssue(List<ReferenceDataIntegrityIssue> issues, IReadOnlyList<KillSwitchState> states, DateTimeOffset now)
    {
        if (states.Count <= 1)
        {
            return;
        }

        var latestTimestamp = states.Max(x => x.UpdatedAtUtc);
        if (states.Count(x => x.UpdatedAtUtc == latestTimestamp) > 1)
        {
            issues.Add(NewIssue(ReferenceDataIntegrityIssueType.DuplicateKillSwitchState, "KillSwitchState:Current", "Multiple kill-switch rows share the latest timestamp, so the current kill-switch state is ambiguous.", now));
        }
    }

    private static ReferenceDataIntegrityIssue NewIssue(ReferenceDataIntegrityIssueType type, string key, string description, DateTimeOffset now)
        => new(Guid.NewGuid(), type, ReferenceDataIntegritySeverity.Blocking, ReferenceDataIntegrityStatus.Open, key, description, now);
}

public sealed class FakeModelWeightGenerator(IModelWeightBatchRepository repository, IClock clock) : IFakeModelWeightGenerator
{
    public async Task<ModelWeightBatch> CreateFakeBatchAsync(CreateFakeModelWeightBatchRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var asOf = request.AsOfUtc ?? now;
        var effective = request.EffectiveAtUtc ?? asOf;
        var rows = request.Weights;
        var externalBatchId = string.IsNullOrWhiteSpace(request.ExternalBatchId)
            ? $"fake_intraday_fx_{asOf:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}"[..42]
            : request.ExternalBatchId.Trim();
        var contentHash = ModelWeightHash.Compute(request.SourceSystem, externalBatchId, request.FundCode, request.ModelName, asOf, effective, request.FrequencyMinutes, request.NavUsd, request.TargetQuantityMode, rows);

        var existing = await repository.GetBatchByExternalIdAsync(request.SourceSystem, externalBatchId, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.ContentHash, contentHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new DomainRuleViolationException("A model weight batch with the same source system and external batch id already exists with different content.");
            }

            return existing;
        }

        var status = request.Status == ModelWeightBatchStatus.Draft ? ModelWeightBatchStatus.Draft : ModelWeightBatchStatus.Ready;
        var batch = new ModelWeightBatch(
            ModelWeightBatchId.New(),
            externalBatchId,
            request.SourceSystem,
            string.IsNullOrWhiteSpace(request.FundCode) ? "QQ_MASTER" : request.FundCode,
            null,
            string.IsNullOrWhiteSpace(request.ModelName) ? "IntradayFxModel" : request.ModelName,
            asOf,
            effective,
            request.FrequencyMinutes,
            request.NavUsd,
            request.TargetQuantityMode,
            status,
            rows.Count,
            contentHash,
            now,
            status == ModelWeightBatchStatus.Ready ? now : null,
            null,
            null,
            null,
            null,
            "Local fake model weight batch.");
        var modelRows = rows.Select(x => new ModelWeightRow(ModelWeightRowId.New(), batch.Id, x.RawSecurityId, x.Symbol, null, x.Weight, now)).ToList();
        await repository.AddBatchAsync(batch, modelRows, cancellationToken);
        return batch;
    }
}

public sealed class ModelWeightPromotionService(
    IModelWeightBatchRepository batchRepository,
    IIntradayRepository intradayRepository,
    IReferenceDataIntegrityService referenceDataIntegrityService,
    IClock clock) : IModelWeightPromotionService
{
    public async Task<ModelWeightPromotionResult> ValidateBatchAsync(ModelWeightBatchId batchId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var batch = await batchRepository.GetBatchAsync(batchId, cancellationToken);
        if (batch is null)
        {
            return new ModelWeightPromotionResult(batchId, null, null, null, 1, [Issue(batchId, ModelWeightValidationIssueType.MissingBatch, "Model weight batch was not found.", now)], "Model weight batch was not found.", false, false);
        }

        if (batch.Status == ModelWeightBatchStatus.Promoted && batch.PromotedModelRunId is not null)
        {
            return new ModelWeightPromotionResult(batch.Id, batch.Status, batch.PromotedModelRunId, batch.PromotedModelRunId, 0, [], "Batch is already promoted.", true, true);
        }

        await batchRepository.UpdateBatchAsync(batch with { Status = ModelWeightBatchStatus.Validating, Message = "Validating model weight batch." }, cancellationToken);
        var issues = await BuildValidationIssuesAsync(batch, now, cancellationToken);
        await batchRepository.AddValidationIssuesAsync(batch.Id, issues, replaceExisting: true, cancellationToken);
        var blocking = issues.Count(x => x.Severity == ModelWeightValidationSeverity.Blocking);
        var finalStatus = blocking > 0 ? ModelWeightBatchStatus.Rejected : ModelWeightBatchStatus.Accepted;
        var acceptedAt = blocking > 0 ? batch.AcceptedAtUtc : now;
        var rejectedAt = blocking > 0 ? now : batch.RejectedAtUtc;
        var message = blocking > 0 ? $"Validation failed with {blocking} blocking issue(s)." : "Validation accepted.";
        await batchRepository.UpdateBatchAsync(batch with { Status = finalStatus, AcceptedAtUtc = acceptedAt, RejectedAtUtc = rejectedAt, Message = message }, cancellationToken);

        return new ModelWeightPromotionResult(batch.Id, finalStatus, batch.PromotedModelRunId, batch.PromotedModelRunId, issues.Count, issues, message, blocking == 0, false);
    }

    public async Task<ModelWeightPromotionResult> PromoteBatchAsync(ModelWeightBatchId batchId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var batch = await batchRepository.GetBatchAsync(batchId, cancellationToken);
        if (batch is null)
        {
            return new ModelWeightPromotionResult(batchId, null, null, null, 1, [Issue(batchId, ModelWeightValidationIssueType.MissingBatch, "Model weight batch was not found.", now)], "Model weight batch was not found.", false, false);
        }

        if (batch.Status == ModelWeightBatchStatus.Promoted && batch.PromotedModelRunId is not null)
        {
            return new ModelWeightPromotionResult(batch.Id, batch.Status, batch.PromotedModelRunId, batch.PromotedModelRunId, 0, await batchRepository.GetValidationIssuesAsync(batch.Id, cancellationToken), "Batch is already promoted; returning existing model run id.", true, true);
        }

        var validation = await ValidateBatchAsync(batch.Id, cancellationToken);
        if (!validation.Succeeded)
        {
            return validation;
        }

        batch = await batchRepository.GetBatchAsync(batchId, cancellationToken) ?? batch;
        var rows = await batchRepository.GetRowsAsync(batch.Id, cancellationToken);
        var state = await intradayRepository.LoadStateAsync(cancellationToken);
        var fund = ResolveFund(state, batch.FundCode);
        if (fund is null)
        {
            var issue = Issue(batch.Id, ModelWeightValidationIssueType.InvalidFund, $"Fund code '{batch.FundCode}' is not valid.", now);
            await batchRepository.AddValidationIssuesAsync(batch.Id, [issue], replaceExisting: true, cancellationToken);
            await batchRepository.UpdateBatchAsync(batch with { Status = ModelWeightBatchStatus.Rejected, RejectedAtUtc = now, Message = issue.Message }, cancellationToken);
            return new ModelWeightPromotionResult(batch.Id, ModelWeightBatchStatus.Rejected, null, null, 1, [issue], issue.Message, false, false);
        }

        var run = new ModelRun(
            ModelRunId.New(),
            fund.Id,
            batch.ModelName,
            batch.AsOfUtc,
            now,
            batch.EffectiveAtUtc,
            batch.FrequencyMinutes,
            batch.NavUsd,
            ModelRunStatus.Received,
            batch.ContentHash ?? ModelWeightHash.Compute(batch.SourceSystem, batch.ExternalBatchId, batch.FundCode, batch.ModelName, batch.AsOfUtc, batch.EffectiveAtUtc, batch.FrequencyMinutes, batch.NavUsd, batch.TargetQuantityMode, rows.Select(x => new CreateFakeModelWeightRowRequest(x.RawSecurityId, x.Symbol, x.Weight)).ToList()),
            "db-weight-source",
            false,
            batch.TargetQuantityMode);
        var weights = rows.Select(row =>
        {
            var instrument = state.Instruments.Single(x => x.Symbol.Equals(row.Symbol, StringComparison.OrdinalIgnoreCase) && x.IsEnabled);
            return new TargetWeight(run.Id, instrument.Id, row.Weight, row.RawSecurityId);
        }).ToList();

        await intradayRepository.AddModelRunAsync(run, weights, cancellationToken);
        await batchRepository.MarkPromotedAsync(batch.Id, run.Id, now, cancellationToken);
        return new ModelWeightPromotionResult(batch.Id, ModelWeightBatchStatus.Promoted, run.Id, run.Id, 0, [], "Promoted to model run. Processing remains explicit.", true, false);
    }

    public async Task<IReadOnlyList<ModelWeightPromotionResult>> PromoteReadyBatchesAsync(int limit, CancellationToken cancellationToken)
    {
        var batches = await batchRepository.GetReadyBatchesAsync(Math.Clamp(limit, 1, 500), cancellationToken);
        var results = new List<ModelWeightPromotionResult>();
        foreach (var batch in batches)
        {
            results.Add(await PromoteBatchAsync(batch.Id, cancellationToken));
        }

        return results;
    }

    private async Task<IReadOnlyList<ModelWeightValidationIssue>> BuildValidationIssuesAsync(ModelWeightBatch batch, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var issues = new List<ModelWeightValidationIssue>();
        if (batch.Status is not (ModelWeightBatchStatus.Ready or ModelWeightBatchStatus.Accepted or ModelWeightBatchStatus.Validating))
        {
            issues.Add(Issue(batch.Id, ModelWeightValidationIssueType.BatchNotReady, $"Batch status {batch.Status} is not promotable.", now));
        }

        if (string.IsNullOrWhiteSpace(batch.ModelName)) issues.Add(Issue(batch.Id, ModelWeightValidationIssueType.InvalidModelName, "Model name is required.", now));
        if (batch.AsOfUtc.Offset != TimeSpan.Zero || batch.EffectiveAtUtc.Offset != TimeSpan.Zero || batch.EffectiveAtUtc < batch.AsOfUtc) issues.Add(Issue(batch.Id, ModelWeightValidationIssueType.InvalidTimestamp, "As-of and effective timestamps must be UTC and effective must not precede as-of.", now));
        if (batch.NavUsd <= 0) issues.Add(Issue(batch.Id, ModelWeightValidationIssueType.InvalidNav, "NAV must be positive.", now));
        if (batch.FrequencyMinutes <= 0) issues.Add(Issue(batch.Id, ModelWeightValidationIssueType.InvalidFrequency, "Frequency minutes must be positive.", now));
        if (!Enum.IsDefined(batch.TargetQuantityMode)) issues.Add(Issue(batch.Id, ModelWeightValidationIssueType.InvalidTargetQuantityMode, "Target quantity mode is invalid.", now));

        var state = await intradayRepository.LoadStateAsync(cancellationToken);
        if (ResolveFund(state, batch.FundCode) is null)
        {
            issues.Add(Issue(batch.Id, ModelWeightValidationIssueType.InvalidFund, $"Fund code '{batch.FundCode}' did not resolve to the local seeded fund.", now));
        }

        var integrity = await referenceDataIntegrityService.CheckAsync(cancellationToken);
        if (integrity.BlockingIssueCount > 0)
        {
            issues.Add(Issue(batch.Id, ModelWeightValidationIssueType.ReferenceDataInvalid, $"Reference data integrity has {integrity.BlockingIssueCount} blocking issue(s).", now));
        }

        var rows = (await batchRepository.GetRowsAsync(batch.Id, cancellationToken)).ToList();
        if (rows.Count == 0) issues.Add(Issue(batch.Id, ModelWeightValidationIssueType.MissingRows, "At least one model weight row is required.", now));
        if (batch.ExpectedRowCount is not null && batch.ExpectedRowCount.Value != rows.Count) issues.Add(Issue(batch.Id, ModelWeightValidationIssueType.RowCountMismatch, $"Expected {batch.ExpectedRowCount.Value} row(s), found {rows.Count}.", now));

        AddDuplicateRowIssues(issues, batch.Id, rows, x => x.Symbol, "symbol", now);
        AddDuplicateRowIssues(issues, batch.Id, rows, x => x.RawSecurityId, "raw security id", now);

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (string.IsNullOrWhiteSpace(row.RawSecurityId) || string.IsNullOrWhiteSpace(row.Symbol))
            {
                issues.Add(Issue(batch.Id, ModelWeightValidationIssueType.InvalidWeight, "Raw security id and symbol are required.", now, row.Id, index + 1));
                continue;
            }

            var instruments = state.Instruments.Where(x => x.Symbol.Equals(row.Symbol, StringComparison.OrdinalIgnoreCase)).ToList();
            if (instruments.Count == 0)
            {
                issues.Add(Issue(batch.Id, ModelWeightValidationIssueType.UnknownInstrument, $"Symbol '{row.Symbol}' does not resolve to an instrument.", now, row.Id, index + 1));
            }
            else if (!instruments.Any(x => x.IsEnabled))
            {
                issues.Add(Issue(batch.Id, ModelWeightValidationIssueType.DisabledInstrument, $"Symbol '{row.Symbol}' resolves only to disabled instruments.", now, row.Id, index + 1));
            }
        }

        return issues;
    }

    private static Fund? ResolveFund(PlatformState state, string fundCode)
        => state.Funds.FirstOrDefault(x => x.Name.Equals(fundCode, StringComparison.OrdinalIgnoreCase) && x.IsEnabled)
            ?? (fundCode.Equals("QQ_MASTER", StringComparison.OrdinalIgnoreCase) ? state.Funds.FirstOrDefault(x => x.IsEnabled) : null);

    private static void AddDuplicateRowIssues(List<ModelWeightValidationIssue> issues, ModelWeightBatchId batchId, IReadOnlyList<ModelWeightRow> rows, Func<ModelWeightRow, string> selector, string label, DateTimeOffset now)
    {
        foreach (var duplicate in rows.GroupBy(selector, StringComparer.OrdinalIgnoreCase).Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Count() > 1))
        {
            issues.Add(Issue(batchId, ModelWeightValidationIssueType.DuplicateSecurity, $"Duplicate {label} '{duplicate.Key}' exists in the batch.", now));
        }
    }

    private static ModelWeightValidationIssue Issue(ModelWeightBatchId batchId, ModelWeightValidationIssueType type, string message, DateTimeOffset now, ModelWeightRowId? rowId = null, int? rowNumber = null)
        => new(Guid.NewGuid(), batchId, type, ModelWeightValidationSeverity.Blocking, message, rowId, rowNumber, now);
}

public static class ModelWeightHash
{
    public static string Compute(ModelWeightSourceSystem sourceSystem, string externalBatchId, string fundCode, string modelName, DateTimeOffset asOfUtc, DateTimeOffset effectiveAtUtc, int frequencyMinutes, decimal navUsd, TargetQuantityMode targetQuantityMode, IReadOnlyList<CreateFakeModelWeightRowRequest> rows)
    {
        var builder = new StringBuilder();
        builder.Append(sourceSystem).Append('|').Append(externalBatchId).Append('|').Append(fundCode).Append('|').Append(modelName).Append('|')
            .Append(asOfUtc.ToUniversalTime().ToString("O")).Append('|').Append(effectiveAtUtc.ToUniversalTime().ToString("O")).Append('|')
            .Append(frequencyMinutes).Append('|').Append(navUsd.ToString("0.##########")).Append('|').Append(targetQuantityMode);
        foreach (var row in rows.OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.RawSecurityId, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append('|').Append(row.RawSecurityId).Append(':').Append(row.Symbol).Append(':').Append(row.Weight.ToString("0.##########"));
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }
}

public sealed record RiskContext(Fund Fund, Venue Venue, Instrument Instrument, ModelRun ModelRun, MarketDataSnapshot MarketData, decimal CurrentBaseQuantity, bool PositionsMatch, decimal ExistingGrossExposureUsd, DateTimeOffset Now);

public sealed class RiskEngine
{
    public RiskDecision Evaluate(TradeIntent intent, RiskContext context, RiskLimitSet limitSet, InstrumentRiskLimit instrumentLimit, VenueRiskLimit venueLimit, TradingWindow tradingWindow, KillSwitchState killSwitch)
        => EvaluateDetailed(intent, context, limitSet, instrumentLimit, venueLimit, tradingWindow, killSwitch).Decision;

    public (RiskDecision Decision, IReadOnlyList<RiskDecisionDetail> Details) EvaluateDetailed(TradeIntent intent, RiskContext context, RiskLimitSet limitSet, InstrumentRiskLimit instrumentLimit, VenueRiskLimit venueLimit, TradingWindow tradingWindow, KillSwitchState killSwitch)
    {
        var reject = RiskRejectReason.None;
        var status = RiskDecisionStatus.Approved;
        var notional = Math.Abs(intent.RequestedBaseQuantity * context.MarketData.Mid);
        var details = new List<RiskDecisionDetail>();

        if (!limitSet.GlobalTradingEnabled) reject = RiskRejectReason.GlobalTradingDisabled;
        else if (killSwitch.IsActive) reject = RiskRejectReason.KillSwitchActive;
        else if (!context.Fund.IsEnabled) reject = RiskRejectReason.FundDisabled;
        else if (!context.Venue.IsEnabled || !context.Venue.IsTradingEnabled || !venueLimit.IsVenueEnabled) reject = RiskRejectReason.VenueDisabled;
        else if (!context.Instrument.IsEnabled || !context.Instrument.IsTradingEnabled || !instrumentLimit.IsTradingEnabled) reject = RiskRejectReason.InstrumentDisabled;
        else if (!context.PositionsMatch) reject = RiskRejectReason.PositionMismatch;
        else if (context.Now - context.ModelRun.AsOfUtc > limitSet.MaxModelRunAge) reject = RiskRejectReason.StaleModelRun;
        else if (context.MarketData.IsStale(limitSet.MaxMarketDataAge, context.Now)) reject = RiskRejectReason.StaleMarketData;
        else if (intent.RequestedBaseQuantity <= 0 || intent.RequestedVenueQuantity <= 0) reject = RiskRejectReason.InvalidQuantity;
        else if (intent.RequestedBaseQuantity < instrumentLimit.MinTradeQuantity) reject = RiskRejectReason.InvalidQuantity;
        else if (notional > instrumentLimit.MaxTradeNotionalUsd || notional > venueLimit.MaxTradeNotionalUsd) reject = RiskRejectReason.MaxTradeNotionalExceeded;
        else if (Math.Abs(context.CurrentBaseQuantity * context.MarketData.Mid) + notional > instrumentLimit.MaxExposureUsd) reject = RiskRejectReason.MaxInstrumentExposureExceeded;
        else if (context.ExistingGrossExposureUsd + notional > limitSet.MaxGrossExposureUsd) reject = RiskRejectReason.MaxGrossExposureExceeded;
        else if (!IsTradingWindowOpen(tradingWindow, context.Now)) reject = RiskRejectReason.TradingWindowClosed;

        if (reject != RiskRejectReason.None)
        {
            status = reject is RiskRejectReason.PositionMismatch or RiskRejectReason.KillSwitchActive ? RiskDecisionStatus.Blocked : RiskDecisionStatus.Rejected;
        }

        var decision = new RiskDecision(Guid.NewGuid(), intent.Id, status, reject, reject.ToString(), context.Now, limitSet.Id, intent.ModelRunId, intent.InstrumentId, context.Venue.Id);
        details.Add(Detail(decision.Id, "GlobalTradingEnabled", limitSet.GlobalTradingEnabled, limitSet.GlobalTradingEnabled ? "Global trading enabled." : "Global trading disabled.", context.Now));
        details.Add(Detail(decision.Id, "KillSwitch", !killSwitch.IsActive, killSwitch.IsActive ? "Kill switch is active." : "Kill switch is off.", context.Now));
        details.Add(Detail(decision.Id, "InstrumentTradingEnabled", context.Instrument.IsEnabled && context.Instrument.IsTradingEnabled && instrumentLimit.IsTradingEnabled, "Instrument trading flag and limit flag checked.", context.Now));
        details.Add(Detail(decision.Id, "VenueTradingEnabled", context.Venue.IsEnabled && context.Venue.IsTradingEnabled && venueLimit.IsVenueEnabled, "Venue trading flag and limit flag checked.", context.Now));
        details.Add(Detail(decision.Id, "PositionMatch", context.PositionsMatch, "Internal and broker positions matched within tolerance before risk.", context.Now));
        details.Add(Compare(decision.Id, "ModelStalenessSeconds", (decimal)(context.Now - context.ModelRun.AsOfUtc).TotalSeconds, (decimal)limitSet.MaxModelRunAge.TotalSeconds, "seconds", RiskRejectReason.StaleModelRun, context.Now));
        details.Add(Compare(decision.Id, "MarketDataStalenessSeconds", (decimal)(context.Now - context.MarketData.ReceivedAtUtc).TotalSeconds, (decimal)limitSet.MaxMarketDataAge.TotalSeconds, "seconds", RiskRejectReason.StaleMarketData, context.Now));
        details.Add(Compare(decision.Id, "MinInstrumentTradeQuantity", intent.RequestedBaseQuantity, instrumentLimit.MinTradeQuantity, "baseQuantity", RiskRejectReason.InvalidQuantity, context.Now, greaterThanOrEqual: true));
        details.Add(Compare(decision.Id, "MaxTradeNotionalUsd", notional, Math.Min(instrumentLimit.MaxTradeNotionalUsd, venueLimit.MaxTradeNotionalUsd), "USD", RiskRejectReason.MaxTradeNotionalExceeded, context.Now));
        details.Add(Compare(decision.Id, "MaxInstrumentExposureUsd", Math.Abs(context.CurrentBaseQuantity * context.MarketData.Mid) + notional, instrumentLimit.MaxExposureUsd, "USD", RiskRejectReason.MaxInstrumentExposureExceeded, context.Now));
        details.Add(Compare(decision.Id, "MaxGrossExposureUsd", context.ExistingGrossExposureUsd + notional, limitSet.MaxGrossExposureUsd, "USD", RiskRejectReason.MaxGrossExposureExceeded, context.Now));
        details.Add(Detail(decision.Id, "TradingWindow", IsTradingWindowOpen(tradingWindow, context.Now), "Trading window and no-new-orders cutoff checked.", context.Now));
        return (decision, details);
    }

    private static RiskDecisionDetail Detail(Guid decisionId, string name, bool passed, string message, DateTimeOffset now)
        => new(Guid.NewGuid(), decisionId, name, passed ? RiskDecisionCheckStatus.Passed : RiskDecisionCheckStatus.Failed, passed ? null : RiskRejectReason.RiskConfigMissing, null, null, null, message, now);

    private static RiskDecisionDetail Compare(Guid decisionId, string name, decimal observed, decimal limit, string unit, RiskRejectReason reason, DateTimeOffset now, bool greaterThanOrEqual = false)
    {
        var passed = greaterThanOrEqual ? observed >= limit : observed <= limit;
        var comparator = greaterThanOrEqual ? ">=" : "<=";
        var message = passed
            ? $"{name} passed: observed {observed:0.##########} {comparator} limit {limit:0.##########}."
            : $"{name} exceeded: observed {observed:0.##########} {(greaterThanOrEqual ? "<" : ">")} limit {limit:0.##########}.";
        return new RiskDecisionDetail(Guid.NewGuid(), decisionId, name, passed ? RiskDecisionCheckStatus.Passed : RiskDecisionCheckStatus.Failed, passed ? null : reason, observed, limit, unit, message, now);
    }

    private static bool IsTradingWindowOpen(TradingWindow window, DateTimeOffset now)
    {
        if (!window.IsEnabled || !window.TradingEnabled || now.DayOfWeek != window.DayOfWeek)
        {
            return false;
        }

        var time = TimeOnly.FromTimeSpan(now.UtcDateTime.TimeOfDay);
        return time >= window.OpensAtUtc && time <= window.ClosesAtUtc && time <= window.NoNewOrdersAfterUtc;
    }
}

public enum ProcessModelRunStatus { Processed, Blocked, AlreadyProcessed, NoActionRequired, Failed }
public enum ProcessModelRunBlockedReason
{
    None,
    StaleModelRun,
    StaleMarketData,
    PositionMismatch,
    UnknownCurrentPosition,
    RiskRejected,
    RiskBlocked,
    TradingWindowClosed,
    KillSwitchActive,
    NoMarketData,
    NoTargetWeights,
    NoDrift,
    ReferenceDataInvalid,
    ReferenceDataAmbiguous,
    Other
}

public sealed record ProcessModelRunResult(
    ModelRunId? ModelRunId,
    bool Processed,
    ProcessModelRunStatus Status,
    ProcessModelRunBlockedReason? BlockedReason,
    string? Message,
    int TradeIntentCount,
    int RiskDecisionCount,
    int OrderCount,
    int ExecutionReportCount,
    int FillCount,
    int ReconciliationBreakCount,
    bool IsAlreadyProcessed,
    DateTimeOffset CompletedAtUtc)
{
    public bool Blocked => Status == ProcessModelRunStatus.Blocked;

    public static ProcessModelRunResult NoWork(DateTimeOffset now)
        => new(null, false, ProcessModelRunStatus.NoActionRequired, null, "No unprocessed model runs.", 0, 0, 0, 0, 0, 0, false, now);
}

public sealed class ProcessModelRunService(IIntradayRepository repository, IVenueExecutionGateway venueGateway, IBrokerPositionProvider brokerPositionProvider, IClock clock, IReferenceDataIntegrityService referenceDataIntegrityService, IExceptionCaseService? exceptionCaseService = null)
{
    public async Task<ProcessModelRunResult> ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        var run = await repository.GetNextUnprocessedModelRunAsync(cancellationToken);
        if (run is null)
        {
            return ProcessModelRunResult.NoWork(clock.UtcNow);
        }

        return await ProcessAsync(run.Id, cancellationToken);
    }

    public async Task<ProcessModelRunResult> ProcessAsync(ModelRunId modelRunId, CancellationToken cancellationToken = default)
    {
        var state = await repository.LoadStateAsync(cancellationToken);
        var run = state.ModelRuns.FirstOrDefault(x => x.Id == modelRunId);
        if (run is null)
        {
            return BuildResult(state, modelRunId, false, ProcessModelRunStatus.Failed, ProcessModelRunBlockedReason.Other, "Model run not found.", false, clock.UtcNow);
        }

        if (run.IsProcessed)
        {
            return BuildResult(state, modelRunId, false, ProcessModelRunStatus.AlreadyProcessed, null, "Model run already processed.", true, clock.UtcNow);
        }

        var now = clock.UtcNow;
        var integrity = await referenceDataIntegrityService.CheckAsync(cancellationToken);
        if (integrity.BlockingIssueCount > 0)
        {
            return BuildResult(state, run.Id, false, ProcessModelRunStatus.Blocked, DetermineIntegrityBlockedReason(integrity), $"Reference data integrity check failed with {integrity.BlockingIssueCount} blocking issue(s).", false, now);
        }

        var previousBlock = GetExistingBlock(state, run.Id, now);
        if (previousBlock is not null)
        {
            return previousBlock;
        }

        var fund = state.Funds.SingleOrDefault(x => x.Id == run.FundId && x.IsEnabled);
        var brokerAccount = fund is null ? null : state.BrokerAccounts.SingleOrDefault(x => x.FundId == fund.Id && x.IsEnabled);
        var venue = state.Venues.SingleOrDefault(x => x.Name == "LMAX" && x.IsEnabled);
        var riskLimitSet = fund is null ? null : state.RiskLimitSets
            .Where(x => x.FundId == fund.Id && x.ModelName == run.ModelName && x.IsActive && x.Status == RiskLimitSetStatus.Active)
            .OrderByDescending(x => x.Version)
            .FirstOrDefault()
            ?? state.RiskLimitSets
                .Where(x => x.FundId == fund.Id && x.IsActive && x.Status == RiskLimitSetStatus.Active)
                .OrderByDescending(x => x.Version)
                .FirstOrDefault();
        if (fund is null || brokerAccount is null || venue is null || riskLimitSet is null)
        {
            return BuildResult(state, run.Id, false, ProcessModelRunStatus.Blocked, ProcessModelRunBlockedReason.ReferenceDataInvalid, "Required enabled reference data is missing.", false, now);
        }
        var tradingWindow = state.TradingWindows.FirstOrDefault(x => x.FundId == fund.Id && x.ModelName == run.ModelName && x.DayOfWeek == now.DayOfWeek)
            ?? state.TradingWindows.FirstOrDefault(x => x.FundId == fund.Id && x.ModelName == run.ModelName)
            ?? state.TradingWindows.FirstOrDefault(x => x.FundId == fund.Id)
            ?? new TradingWindow(Guid.Empty, fund.Id, run.ModelName, "UTC", now.DayOfWeek, TimeOnly.MaxValue, TimeOnly.MinValue, TimeOnly.MinValue, null, false, false);
        var targetWeights = state.TargetWeights.Where(x => x.ModelRunId == run.Id).ToList();
        if (targetWeights.Count == 0)
        {
            return BuildResult(state, run.Id, false, ProcessModelRunStatus.Blocked, ProcessModelRunBlockedReason.NoTargetWeights, "No target weights exist for the model run.", false, now);
        }

        var brokerPositions = await brokerPositionProvider.GetPositionsAsync(brokerAccount.Id, cancellationToken);
        var internalPositions = BuildInternalPositions(state, fund.Id, now);
        var reconciliation = Reconcile(run.Id, ReconciliationPhase.PreTrade, targetWeights.Select(x => x.InstrumentId).ToList(), internalPositions, brokerPositions, riskLimitSet.PositionToleranceBaseQuantity, now);
        await repository.SaveReconciliationAsync(reconciliation.Run, reconciliation.Breaks, cancellationToken);
        await CreateExceptionCasesAsync(reconciliation.Run, reconciliation.Breaks, cancellationToken);
        if (reconciliation.Run.HasBlockingBreaks)
        {
            state = await repository.LoadStateAsync(cancellationToken);
            return BuildResult(state, run.Id, false, ProcessModelRunStatus.Blocked, ProcessModelRunBlockedReason.PositionMismatch, "Trading blocked by pre-trade reconciliation breaks.", false, now);
        }

        var calculator = new TargetPositionCalculator();
        var riskEngine = new RiskEngine();

        foreach (var weight in targetWeights)
        {
            var instrument = state.Instruments.Single(x => x.Id == weight.InstrumentId);
            var mapping = state.VenueInstrumentMappings
                .Where(x => x.InstrumentId == instrument.Id && x.VenueId == venue.Id && x.IsEnabled)
                .OrderBy(x => x.Id.Value)
                .FirstOrDefault();
            if (mapping is null)
            {
                return BuildResult(state, run.Id, false, ProcessModelRunStatus.Blocked, ProcessModelRunBlockedReason.Other, $"No enabled venue mapping exists for {instrument.Symbol}.", false, now);
            }

            var marketData = state.MarketData.Where(x => x.InstrumentId == instrument.Id && x.VenueId == venue.Id).MaxBy(x => x.ReceivedAtUtc);
            if (marketData is null)
            {
                return BuildResult(state, run.Id, false, ProcessModelRunStatus.Blocked, ProcessModelRunBlockedReason.NoMarketData, $"No market data exists for {instrument.Symbol}.", false, now);
            }

            var target = calculator.Calculate(run, weight, marketData, mapping);
            var currentBase = internalPositions.GetValueOrDefault(instrument.Id, 0m);
            var currentVenue = currentBase / mapping.ContractSize;
            var driftBase = target.TargetBaseQuantity - currentBase;
            var driftVenue = target.TargetVenueQuantity - currentVenue;
            var drift = new DriftSnapshot(run.Id, instrument.Id, target.TargetBaseQuantity, currentBase, driftBase, target.TargetVenueQuantity, currentVenue, driftVenue);
            await repository.SaveTargetAndDriftAsync(target, drift, cancellationToken);

            if (Math.Abs(drift.DriftVenueQuantity) < riskLimitSet.MinDriftVenueQuantity)
            {
                continue;
            }

            var intent = new TradeIntent(
                TradeIntentId.New(),
                run.Id,
                fund.Id,
                instrument.Id,
                drift.DriftBaseQuantity > 0 ? TradeSide.Buy : TradeSide.Sell,
                Math.Abs(drift.DriftBaseQuantity),
                Math.Abs(drift.DriftVenueQuantity),
                "Model drift",
                TradeIntentStatus.Created,
                now);

            await repository.AddTradeIntentAsync(intent, cancellationToken);
            var instrumentLimit = state.InstrumentRiskLimits
                .Where(x => x.RiskLimitSetId == riskLimitSet.Id && x.InstrumentId == instrument.Id && x.IsEnabled)
                .OrderBy(x => x.Id)
                .FirstOrDefault();
            var venueLimit = state.VenueRiskLimits
                .Where(x => x.RiskLimitSetId == riskLimitSet.Id && x.VenueId == venue.Id && x.IsEnabled)
                .OrderBy(x => x.Id)
                .FirstOrDefault();
            if (instrumentLimit is null || venueLimit is null)
            {
                state = await repository.LoadStateAsync(cancellationToken);
                return BuildResult(state, run.Id, false, ProcessModelRunStatus.Blocked, ProcessModelRunBlockedReason.ReferenceDataInvalid, "Risk configuration is missing for the requested instrument or venue.", false, now);
            }

            var riskContext = new RiskContext(fund, venue, instrument, run, marketData, currentBase, true, CalculateGrossExposure(state, fund.Id, marketData.Mid), now);
            var (decision, details) = riskEngine.EvaluateDetailed(intent, riskContext, riskLimitSet, instrumentLimit, venueLimit, tradingWindow, state.KillSwitch);
            await repository.AddRiskDecisionAsync(decision, details, cancellationToken);
            if (decision.Status != RiskDecisionStatus.Approved)
            {
                state = await repository.LoadStateAsync(cancellationToken);
                return BuildResult(state, run.Id, false, ProcessModelRunStatus.Blocked, MapBlockedReason(decision.RejectReason), BuildRiskMessage(decision.RejectReason), false, now);
            }

            var parent = new ParentOrder(ParentOrderId.New(), intent.Id, new ClientOrderId($"P-{run.Id.Value:N}-{state.ParentOrders.Count + 1}"), intent.Side == TradeSide.Buy ? OrderSide.Buy : OrderSide.Sell, intent.RequestedBaseQuantity, ExecutionAlgo.MarketImmediate, OrderStatus.Created, now);
            var child = new ChildOrder(ChildOrderId.New(), parent.Id, venue.Id, new ClientOrderId($"C-{run.Id.Value:N}-{state.ChildOrders.Count + 1}"), parent.Side, OrderType.Market, TimeInForce.IOC, intent.RequestedBaseQuantity, intent.RequestedVenueQuantity, OrderStatus.PendingNew, now);
            await repository.AddOrdersAsync(parent, child, cancellationToken);

            var result = await venueGateway.SendOrderAsync(new VenueOrderRequest(child.Id, venue.Id, instrument.Id, child.ClientOrderId, child.Side, child.OrderType, child.TimeInForce, child.BaseQuantity, child.VenueQuantity), cancellationToken);
            foreach (var report in result.Reports)
            {
                await repository.AddExecutionReportAsync(report, cancellationToken);
                if (report.ExecutionReportType is ExecutionReportType.Fill or ExecutionReportType.PartialFill && report.BrokerExecutionId is not null && report.LastQuantity > 0)
                {
                    var side = child.Side == OrderSide.Buy ? TradeSide.Buy : TradeSide.Sell;
                    var fill = new Fill(FillId.New(), report.BrokerExecutionId, child.Id, instrument.Id, venue.Id, side, report.LastQuantity * mapping.ContractSize, report.LastQuantity, report.LastPrice, report.ReceivedAtUtc, report.ReceivedAtUtc);
                    if (await repository.TryAddFillAsync(fill, cancellationToken))
                    {
                        var signed = side == TradeSide.Buy ? fill.BaseQuantity : -fill.BaseQuantity;
                        await repository.AddPositionLedgerEventAsync(new PositionLedgerEvent(Guid.NewGuid(), fund.Id, instrument.Id, PositionLedgerEventType.Fill, signed, fill.BrokerExecutionId, now), cancellationToken);
                    }
                }
            }
        }

        state = await repository.LoadStateAsync(cancellationToken);
        internalPositions = BuildInternalPositions(state, fund.Id, now);
        brokerPositions = await brokerPositionProvider.GetPositionsAsync(brokerAccount.Id, cancellationToken);
        var postTrade = Reconcile(run.Id, ReconciliationPhase.PostTrade, targetWeights.Select(x => x.InstrumentId).ToList(), internalPositions, brokerPositions, riskLimitSet.PositionToleranceBaseQuantity, now);
        await repository.SaveReconciliationAsync(postTrade.Run, postTrade.Breaks, cancellationToken);
        await CreateExceptionCasesAsync(postTrade.Run, postTrade.Breaks, cancellationToken);

        if (postTrade.Run.HasBlockingBreaks)
        {
            state = await repository.LoadStateAsync(cancellationToken);
            return BuildResult(state, run.Id, false, ProcessModelRunStatus.Blocked, ProcessModelRunBlockedReason.PositionMismatch, "Trading completed but post-trade reconciliation has blocking breaks.", false, now);
        }

        await repository.MarkModelRunProcessedAsync(run.Id, ModelRunStatus.Processed, cancellationToken);
        state = await repository.LoadStateAsync(cancellationToken);
        var noDrift = state.TradeIntents.All(x => x.ModelRunId != run.Id);
        return BuildResult(state, run.Id, true, noDrift ? ProcessModelRunStatus.NoActionRequired : ProcessModelRunStatus.Processed, noDrift ? ProcessModelRunBlockedReason.NoDrift : null, noDrift ? "No rebalance drift exceeded the configured threshold." : "Processed.", false, now);
    }

    private async Task CreateExceptionCasesAsync(ReconciliationRun run, IReadOnlyList<ReconciliationBreak> breaks, CancellationToken cancellationToken)
    {
        if (exceptionCaseService is null || breaks.Count == 0)
        {
            return;
        }

        foreach (var reconciliationBreak in breaks)
        {
            await exceptionCaseService.CreateOrUpdateFromReconciliationBreakAsync(run, reconciliationBreak, cancellationToken);
        }
    }

    private static Dictionary<InstrumentId, decimal> BuildInternalPositions(PlatformState state, FundId fundId, DateTimeOffset now)
        => state.PositionLedger
            .Where(x => x.FundId == fundId && x.CreatedAtUtc <= now)
            .GroupBy(x => x.InstrumentId)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.BaseQuantityDelta));

    private static decimal CalculateGrossExposure(PlatformState state, FundId fundId, decimal price)
        => Math.Abs(state.PositionLedger.Where(x => x.FundId == fundId).Sum(x => x.BaseQuantityDelta) * price);

    private static (ReconciliationRun Run, IReadOnlyList<ReconciliationBreak> Breaks) Reconcile(ModelRunId modelRunId, ReconciliationPhase phase, IReadOnlyList<InstrumentId> instrumentIds, Dictionary<InstrumentId, decimal> internalPositions, IReadOnlyList<BrokerPositionSnapshot> brokerPositions, decimal tolerance, DateTimeOffset now)
    {
        var breaks = new List<ReconciliationBreak>();
        foreach (var instrumentId in instrumentIds.Distinct())
        {
            var internalPosition = internalPositions.GetValueOrDefault(instrumentId, 0m);
            var brokerPosition = brokerPositions.FirstOrDefault(x => x.InstrumentId == instrumentId)?.BaseQuantity ?? 0m;
            if (Math.Abs(internalPosition - brokerPosition) > tolerance)
            {
                breaks.Add(new ReconciliationBreak(Guid.NewGuid(), Guid.Empty, ReconciliationBreakType.InternalBrokerPositionMismatch, ReconciliationBreakSeverity.Blocking, ReconciliationBreakStatus.Open, instrumentId, $"Internal {internalPosition} vs broker {brokerPosition}."));
            }
        }

        var run = new ReconciliationRun(Guid.NewGuid(), modelRunId, phase, now, breaks.Any(x => x.Severity == ReconciliationBreakSeverity.Blocking));
        return (run, breaks.Select(x => x with { ReconciliationRunId = run.Id }).ToList());
    }

    private static ProcessModelRunResult? GetExistingBlock(PlatformState state, ModelRunId modelRunId, DateTimeOffset now)
    {
        var riskDecision = state.RiskDecisions
            .Where(x => state.TradeIntents.Any(t => t.Id == x.TradeIntentId && t.ModelRunId == modelRunId))
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefault(x => x.Status != RiskDecisionStatus.Approved);
        if (riskDecision is not null)
        {
            return BuildResult(state, modelRunId, false, ProcessModelRunStatus.Blocked, MapBlockedReason(riskDecision.RejectReason), BuildRiskMessage(riskDecision.RejectReason), false, now);
        }

        if (state.ReconciliationRuns.Any(x => x.ModelRunId == modelRunId && x.Phase == ReconciliationPhase.PreTrade && x.HasBlockingBreaks))
        {
            return BuildResult(state, modelRunId, false, ProcessModelRunStatus.Blocked, ProcessModelRunBlockedReason.PositionMismatch, "Trading blocked by pre-trade reconciliation breaks.", false, now);
        }

        return null;
    }

    private static ProcessModelRunBlockedReason DetermineIntegrityBlockedReason(ReferenceDataIntegrityResult check)
        => check.Issues.Any(x => x.Severity == ReferenceDataIntegritySeverity.Blocking
            && x.Type is ReferenceDataIntegrityIssueType.AmbiguousReferenceData
                or ReferenceDataIntegrityIssueType.DuplicateFund
                or ReferenceDataIntegrityIssueType.DuplicateBrokerAccount
                or ReferenceDataIntegrityIssueType.DuplicateInstrument
                or ReferenceDataIntegrityIssueType.DuplicateVenue
                or ReferenceDataIntegrityIssueType.DuplicateVenueInstrumentMapping
                or ReferenceDataIntegrityIssueType.DuplicateRiskLimitSet
                or ReferenceDataIntegrityIssueType.DuplicateRiskLimit
                or ReferenceDataIntegrityIssueType.DuplicateInstrumentRiskLimit
                or ReferenceDataIntegrityIssueType.DuplicateVenueRiskLimit
                or ReferenceDataIntegrityIssueType.DuplicateTradingWindow
                or ReferenceDataIntegrityIssueType.DuplicateKillSwitchState)
            ? ProcessModelRunBlockedReason.ReferenceDataAmbiguous
            : ProcessModelRunBlockedReason.ReferenceDataInvalid;

    private static ProcessModelRunResult BuildResult(PlatformState state, ModelRunId? modelRunId, bool processed, ProcessModelRunStatus status, ProcessModelRunBlockedReason? blockedReason, string? message, bool alreadyProcessed, DateTimeOffset now)
    {
        if (modelRunId is null)
        {
            return new ProcessModelRunResult(null, processed, status, blockedReason, message, 0, 0, 0, 0, 0, 0, alreadyProcessed, now);
        }

        var intents = state.TradeIntents.Where(x => x.ModelRunId == modelRunId).ToList();
        var intentIds = intents.Select(x => x.Id).ToHashSet();
        var parentOrders = state.ParentOrders.Where(x => intentIds.Contains(x.TradeIntentId)).ToList();
        var parentIds = parentOrders.Select(x => x.Id).ToHashSet();
        var childOrders = state.ChildOrders.Where(x => parentIds.Contains(x.ParentOrderId)).ToList();
        var childIds = childOrders.Select(x => x.Id).ToHashSet();
        var reconciliationRunIds = state.ReconciliationRuns.Where(x => x.ModelRunId == modelRunId).Select(x => x.Id).ToHashSet();

        return new ProcessModelRunResult(
            modelRunId,
            processed,
            status,
            blockedReason,
            message,
            intents.Count,
            state.RiskDecisions.Count(x => intentIds.Contains(x.TradeIntentId)),
            parentOrders.Count,
            state.ExecutionReports.Count(x => childIds.Contains(x.ChildOrderId)),
            state.Fills.Count(x => childIds.Contains(x.ChildOrderId)),
            state.ReconciliationBreaks.Count(x => reconciliationRunIds.Contains(x.ReconciliationRunId)),
            alreadyProcessed,
            now);
    }

    private static ProcessModelRunBlockedReason MapBlockedReason(RiskRejectReason reason)
        => reason switch
        {
            RiskRejectReason.StaleModelRun => ProcessModelRunBlockedReason.StaleModelRun,
            RiskRejectReason.StaleMarketData => ProcessModelRunBlockedReason.StaleMarketData,
            RiskRejectReason.PositionMismatch => ProcessModelRunBlockedReason.PositionMismatch,
            RiskRejectReason.UnknownCurrentPosition => ProcessModelRunBlockedReason.UnknownCurrentPosition,
            RiskRejectReason.TradingWindowClosed => ProcessModelRunBlockedReason.TradingWindowClosed,
            RiskRejectReason.NoNewOrdersAfter => ProcessModelRunBlockedReason.TradingWindowClosed,
            RiskRejectReason.KillSwitchActive => ProcessModelRunBlockedReason.KillSwitchActive,
            RiskRejectReason.RiskConfigMissing => ProcessModelRunBlockedReason.ReferenceDataInvalid,
            _ => ProcessModelRunBlockedReason.RiskRejected
        };

    private static string BuildRiskMessage(RiskRejectReason reason)
        => reason switch
        {
            RiskRejectReason.StaleModelRun => "Model run is stale.",
            RiskRejectReason.StaleMarketData => "Market data is stale.",
            RiskRejectReason.TradingWindowClosed => "Trading window is closed.",
            RiskRejectReason.NoNewOrdersAfter => "No-new-orders cutoff has passed.",
            RiskRejectReason.KillSwitchActive => "Kill switch is active.",
            RiskRejectReason.PositionMismatch => "Positions do not match.",
            RiskRejectReason.RiskConfigMissing => "Risk configuration is missing.",
            _ => $"Risk rejected the trade: {reason}."
        };
}

public static class SeedData
{
    public static PlatformState Create(DateTimeOffset? nowOverride = null)
    {
        var now = nowOverride ?? DateTimeOffset.UtcNow;
        var fundId = new FundId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var accountId = new BrokerAccountId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        var instrumentId = new InstrumentId(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"));
        var venueId = new VenueId(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"));
        var runId = new ModelRunId(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"));
        var limitSetId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var venueInstrumentId = new VenueInstrumentId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var instrumentRiskLimitId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var venueRiskLimitId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var tradingWindowId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var intradayTradingWindowId = Guid.Parse("44444444-4444-4444-4444-444444444445");
        var killSwitchId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var startOfDayEventId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var seedMarketDataSnapshotId = new MarketDataSnapshotId(Guid.Parse("77777777-7777-7777-7777-777777777777"));
        var state = new PlatformState();

        state.Funds.Add(new Fund(fundId, "QQ Intraday Fund", Currency.Usd));
        state.BrokerAccounts.Add(new BrokerAccount(accountId, fundId, "LMAX_DEMO_LOCAL", true, "LMAX_DEMO_LOCAL"));
        state.Instruments.Add(new Instrument(instrumentId, "EURUSD", AssetClass.FxSpot, Currency.Eur, Currency.Usd, 5, 1));
        state.Venues.Add(new Venue(venueId, "LMAX", VenueType.Simulator));
        state.VenueInstrumentMappings.Add(new VenueInstrumentMapping(venueInstrumentId, venueId, instrumentId, "EURUSD", "EUR/USD", 10000m, 0.1m, 0.1m, 0.00001m));
        state.InstrumentAliases.Add(new InstrumentAlias(new InstrumentAliasId(Guid.Parse("12111111-1111-1111-1111-111111114001")), instrumentId, "LMAX_REPORT", "EUR/USD", "4001", true, now));
        state.NavSnapshots.Add(new NavSnapshot(fundId, 1_000_000m, NavSource.Seed, now));
        state.MarketData.Add(new MarketDataSnapshot(seedMarketDataSnapshotId, instrumentId, venueId, 1.09995m, 1.10005m, null, "Seed", now, now) { IsSynthetic = true, CreatedAtUtc = now });
        state.PositionLedger.Add(new PositionLedgerEvent(startOfDayEventId, fundId, instrumentId, PositionLedgerEventType.StartOfDay, 0m, "SOD", now.AddHours(-1)));
        state.RiskLimitSets.Add(new RiskLimitSet(limitSetId, fundId, true, 2_000_000m, TimeSpan.FromHours(24), TimeSpan.FromMinutes(30), 0.0001m, 0.1m, "IntradayFxModel", "Default Conservative Intraday Risk", 1, RiskLimitSetStatus.Active, true, now, null, now, "seed", now, "seed"));
        state.RiskLimits.AddRange([
            new RiskLimit(Guid.Parse("99999999-9999-9999-9999-999999999901"), limitSetId, "MaxTradeNotionalUsd", 500_000m, "USD"),
            new RiskLimit(Guid.Parse("99999999-9999-9999-9999-999999999902"), limitSetId, "MaxGrossExposureUsd", 2_000_000m, "USD"),
            new RiskLimit(Guid.Parse("99999999-9999-9999-9999-999999999903"), limitSetId, "MaxNetExposureUsd", 2_000_000m, "USD"),
            new RiskLimit(Guid.Parse("99999999-9999-9999-9999-999999999904"), limitSetId, "MaxDailyTurnoverUsd", 1_000_000m, "USD"),
            new RiskLimit(Guid.Parse("99999999-9999-9999-9999-999999999905"), limitSetId, "MaxOrdersPerMinute", 10m, "count"),
            new RiskLimit(Guid.Parse("99999999-9999-9999-9999-999999999906"), limitSetId, "MaxSlippageBps", 5m, "bps"),
            new RiskLimit(Guid.Parse("99999999-9999-9999-9999-999999999907"), limitSetId, "MinRebalanceBaseQuantity", 0.1m, "baseQuantity"),
            new RiskLimit(Guid.Parse("99999999-9999-9999-9999-999999999908"), limitSetId, "PositionMatchToleranceBaseQuantity", 0.0001m, "baseQuantity"),
            new RiskLimit(Guid.Parse("99999999-9999-9999-9999-999999999909"), limitSetId, "ModelStalenessSeconds", 86400m, "seconds"),
            new RiskLimit(Guid.Parse("99999999-9999-9999-9999-999999999910"), limitSetId, "MarketDataStalenessSeconds", 1800m, "seconds")
        ]);
        state.InstrumentRiskLimits.Add(new InstrumentRiskLimit(instrumentRiskLimitId, limitSetId, instrumentId, 500_000m, 1_500_000m));
        state.VenueRiskLimits.Add(new VenueRiskLimit(venueRiskLimitId, limitSetId, venueId, 500_000m));
        state.TradingWindows.Add(new TradingWindow(tradingWindowId, fundId, "Sample FX Intraday", "UTC", now.DayOfWeek, TimeOnly.MinValue, new TimeOnly(23, 59, 59), new TimeOnly(23, 59, 59), null));
        state.TradingWindows.Add(new TradingWindow(intradayTradingWindowId, fundId, "IntradayFxModel", "UTC", now.DayOfWeek, TimeOnly.MinValue, new TimeOnly(23, 59, 59), new TimeOnly(23, 59, 59), null));
        state.KillSwitch = new KillSwitchState(killSwitchId, false, null, now);
        state.KillSwitchStates.Add(state.KillSwitch);
        state.ModelRuns.Add(new ModelRun(runId, fundId, "Sample FX Intraday", now.AddMinutes(-1), now, now, 15, 1_000_000m, ModelRunStatus.Received, "sample", "sample.csv", false));
        state.TargetWeights.Add(new TargetWeight(runId, instrumentId, -0.10m, "EURUSD"));
        return state;
    }
}
