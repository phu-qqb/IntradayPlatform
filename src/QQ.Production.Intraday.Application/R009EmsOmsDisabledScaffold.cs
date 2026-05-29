using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QQ.Production.Intraday.Application;

public enum R009LiveIntentSide
{
    Buy,
    Sell
}

public enum R009LiveBarRole
{
    OpeningBuild,
    IntradayRebalance,
    ClosingFlatten
}

public enum R009ApprovalStatus
{
    Missing,
    ApprovedForDesignOnlyPreviewOnly
}

public enum R009LiveLineStatus
{
    PreviewReady,
    HeldMissingReadiness,
    HeldUnsupportedInstrument,
    HeldDirectCrossNotNetted,
    HeldInversionMismatch,
    HeldRiskOperatorMissing,
    InconclusiveSafe
}

public enum R009DisabledDecisionOutput
{
    DesignOnlyExecutionDecision,
    ExecutionPlanPreview,
    ScheduleIntentPreview,
    ResidualRiskAssessment,
    CostTradeoffAssessment,
    ManualReviewRecommendation,
    HoldReason
}

public enum R009DisabledPreviewRequestMode
{
    DisabledPreviewOnly
}

public enum R009DisabledPreviewSourceType
{
    ExecutionIntent,
    PaperPlanLineArtifact
}

public enum R009PreviewConsumerType
{
    InternalPmsPreviewConsumer,
    InternalEmsPreviewConsumer,
    InternalOmsPreviewConsumer,
    OperatorReviewTool,
    TestHarness,
    BrokerGateway,
    LiveMarketDataWorker,
    Scheduler,
    BackgroundWorker,
    OrderRouter,
    ExecutionReportHandler,
    PaperLedgerCommitter,
    ProductionTradingRuntime
}

public sealed record R009EmsOmsExecutionIntent(
    string ExecutionIntentId,
    string SourcePmsCycleId,
    string SourceQubesRunId,
    string SourceRebalanceIntentId,
    string SourceRiskReviewId,
    string Symbol,
    string ExecutionTradableSymbol,
    string NormalizedPortfolioSymbol,
    bool RequiresInversion,
    R009LiveIntentSide Side,
    decimal TargetQuantity,
    decimal TargetNotional,
    DateTimeOffset CanonicalTargetCloseUtc,
    string CanonicalTargetCloseLocal,
    string CanonicalSession,
    R009LiveBarRole BarRole,
    bool MustEndFlat,
    bool OvernightAllowed,
    string? QuoteWindowReadinessId,
    string? CloseBenchmarkReadinessId,
    string? FeedQualityReadinessId,
    string R009ContractVersion,
    R009ApprovalStatus OperatorApprovalStatus,
    R009ApprovalStatus RiskApprovalStatus,
    bool LiveTradingEnabled,
    bool BrokerRoutingEnabled,
    bool OrderSubmissionEnabled,
    bool NonExecutable,
    string? SecurityID = null,
    string? SecurityIDSource = null,
    decimal ExpectedSpreadCostBps = 0.8m,
    decimal MaxSpreadCostBps = 5.0m,
    decimal ResidualNotional = 0m,
    decimal ResidualOpportunityCostBps = 0m);

public sealed record R009LiveFeatureFlags(
    bool R009LiveTradingEnabled,
    bool R009BrokerRoutingEnabled,
    bool R009OrderSubmissionEnabled,
    bool R009ExecutableScheduleEnabled,
    bool R009PaperLedgerCommitEnabled,
    bool R009SchedulerEnabled,
    bool R009BackgroundWorkerEnabled,
    bool R009DryRunOnly)
{
    public static R009LiveFeatureFlags DisabledDefaults { get; } = new(
        R009LiveTradingEnabled: false,
        R009BrokerRoutingEnabled: false,
        R009OrderSubmissionEnabled: false,
        R009ExecutableScheduleEnabled: false,
        R009PaperLedgerCommitEnabled: false,
        R009SchedulerEnabled: false,
        R009BackgroundWorkerEnabled: false,
        R009DryRunOnly: true);
}

public sealed record R009DisabledBoundaryGuard(
    bool BrokerRouteCreationAllowed,
    bool OrderCreationAllowed,
    bool ChildSliceCreationAllowed,
    bool ChildOrderCreationAllowed,
    bool ScheduleExecutionAllowed,
    bool SubmissionAllowed,
    bool FillCreationAllowed,
    bool ExecutionReportCreationAllowed,
    bool StateMutationAllowed,
    bool PaperLedgerCommitAllowed)
{
    public static R009DisabledBoundaryGuard Disabled { get; } = new(
        BrokerRouteCreationAllowed: false,
        OrderCreationAllowed: false,
        ChildSliceCreationAllowed: false,
        ChildOrderCreationAllowed: false,
        ScheduleExecutionAllowed: false,
        SubmissionAllowed: false,
        FillCreationAllowed: false,
        ExecutionReportCreationAllowed: false,
        StateMutationAllowed: false,
        PaperLedgerCommitAllowed: false);
}

public sealed record R009PreTradeRiskGateResult(
    bool SupportedSymbol,
    bool UsdPairOnly,
    bool DirectCrossExcluded,
    bool InversionMetadataValid,
    bool CanonicalTargetClose,
    bool QuarterHourTargetClose,
    bool QuoteWindowReadinessPresent,
    bool CloseBenchmarkReadinessPresent,
    bool FeedQualityReadinessPresent,
    bool RiskApprovalPresent,
    bool OperatorApprovalPresent,
    bool OvernightDisallowed,
    bool MustEndFlat,
    bool SpreadCostGuardPassed,
    bool ControlledResidualCrossConditionPassed,
    bool KillSwitchSafe,
    bool Passed,
    IReadOnlyList<string> Reasons);

public sealed record R009IdempotencyAuditEnvelope(
    string ExecutionIntentId,
    string DecisionId,
    string R009DecisionHash,
    string InputHash,
    string ContractVersion,
    DateTimeOffset CreatedAtUtc,
    bool NoOrderDomainOutput,
    bool NoBrokerRoute,
    bool DryRunOnly);

public sealed record R009DisabledExecutionDecision(
    string DecisionId,
    string ExecutionIntentId,
    R009LiveLineStatus LineStatus,
    IReadOnlyList<R009DisabledDecisionOutput> Outputs,
    string PrimaryPolicyCandidate,
    string SecondaryPolicyCandidate,
    string ConditionalResidualModule,
    R009PreTradeRiskGateResult PreTradeRiskGate,
    string ResidualRiskAssessment,
    string CostTradeoffAssessment,
    string? ManualReviewRecommendation,
    string? HoldReason,
    bool ControlledResidualCrossSelected,
    bool ControlledResidualCrossAlwaysMarketAtClose,
    bool DesignOnly,
    bool PaperOnly,
    bool NonExecutable,
    bool NotAnOrder,
    bool NotSubmitted,
    bool NoBrokerRoute,
    bool NoChildSlices,
    bool NoChildOrders,
    bool NoExecutableSchedule,
    bool NoFill,
    bool NoExecutionReport,
    bool NoRoute,
    bool NoSubmission,
    bool NoPaperLedgerCommit,
    bool CreatesOrder,
    bool CreatesChildOrder,
    bool CreatesRoute,
    bool CreatesSubmission,
    bool CreatesFill,
    bool CreatesExecutionReport,
    bool CreatesExecutableSchedule,
    R009IdempotencyAuditEnvelope Audit);

public sealed record R009ReadinessBindingPreview(
    string? BindingId,
    string? Symbol,
    string? TargetCloseTimestampUtc,
    string? ReadinessStatus,
    string? SourceArtifact);

public sealed record R009PaperPlanPreviewLine(
    string BatchEntryId,
    string? FixturePath,
    string QubesRunId,
    string RequestedCycleRunId,
    string PaperExecutionPlanLineId,
    string Symbol,
    string ExecutionTradableSymbol,
    string NormalizedPortfolioSymbol,
    bool RequiresInversion,
    string Side,
    decimal? TargetQuantity,
    decimal? TargetNotional,
    string CanonicalTargetCloseTimestamp,
    string CanonicalTargetCloseLocal,
    string CanonicalSession,
    string BarRole,
    bool CanonicalQuarterHourTimestampConfirmed,
    string R009ContractVersion,
    R009ReadinessBindingPreview? QuoteWindowReadinessBinding,
    R009ReadinessBindingPreview? CloseBenchmarkReadinessBinding,
    R009ReadinessBindingPreview? FeedQualityReadinessBinding,
    string? RiskReviewStatus,
    string? OperatorApprovalStatus,
    bool NonExecutable,
    bool NotAnOrder,
    bool NotSubmitted,
    bool NoBrokerRoute);

public sealed record R009DisabledDecisionPreviewFlowResult(
    string SourceArtifact,
    IReadOnlyList<R009EmsOmsExecutionIntent> ExecutionIntents,
    IReadOnlyList<R009DisabledExecutionDecision> DecisionPreviews);

public sealed record R009DisabledPreviewSafetyFlags(
    bool DryRunOnly,
    bool LiveTradingEnabled,
    bool BrokerRoutingEnabled,
    bool OrderSubmissionEnabled,
    bool ExecutableScheduleEnabled,
    bool PaperLedgerCommitEnabled,
    bool SchedulerEnabled,
    bool BackgroundWorkerEnabled,
    bool NoBrokerRoute);

public sealed record R009DisabledPreviewRequest(
    string RequestId,
    R009DisabledPreviewRequestMode RequestMode,
    R009DisabledPreviewSourceType SourceType,
    R009EmsOmsExecutionIntent? ExecutionIntent,
    string? SourceArtifactPath,
    string R009ContractVersion,
    bool DryRunOnly,
    bool LiveTradingEnabled,
    bool BrokerRoutingEnabled,
    bool OrderSubmissionEnabled,
    bool ExecutableScheduleEnabled,
    bool PaperLedgerCommitEnabled,
    string OperatorApprovalScope,
    string RiskApprovalScope,
    bool NoBrokerRoute,
    IReadOnlyList<string>? RequestedOutputs = null);

public sealed record R009DisabledPreviewResponse(
    string RequestId,
    string DecisionPreviewId,
    string DecisionStatus,
    bool Accepted,
    IReadOnlyList<string> RejectionReasons,
    IReadOnlyList<R009DisabledExecutionDecision> DecisionPreviews,
    IReadOnlyList<string> HeldReasons,
    bool NonExecutable,
    bool NotAnOrder,
    bool NotSubmitted,
    bool NoBrokerRoute,
    bool NoFill,
    bool NoExecutionReport,
    bool NoRoute,
    bool NoSubmission,
    bool NoPaperLedgerCommit,
    R009DisabledPreviewSafetyFlags SafetyFlags,
    string IdempotencyHash,
    string AuditHash);

public sealed record R009DisabledPreviewBatchItem(
    string ItemId,
    R009DisabledPreviewSourceType SourceType,
    R009EmsOmsExecutionIntent? ExecutionIntent,
    R009PaperPlanPreviewLine? PaperPlanLine);

public sealed record R009DisabledPreviewBatchRequest(
    string BatchRequestId,
    R009DisabledPreviewRequestMode RequestMode,
    IReadOnlyList<R009DisabledPreviewBatchItem> Items,
    string R009ContractVersion,
    bool DryRunOnly,
    bool LiveTradingEnabled,
    bool BrokerRoutingEnabled,
    bool OrderSubmissionEnabled,
    bool ExecutableScheduleEnabled,
    bool PaperLedgerCommitEnabled,
    string OperatorApprovalScope,
    string RiskApprovalScope,
    bool NoBrokerRoute,
    int MaxBatchSize,
    IReadOnlyList<string>? RequestedOutputs = null);

public sealed record R009DisabledPreviewBatchValidationResult(
    bool IsValid,
    IReadOnlyList<string> RejectionReasons,
    int ItemCount,
    int MaxBatchSize);

public sealed record R009DisabledPreviewBatchItemResult(
    string ItemId,
    string Status,
    IReadOnlyList<string> RejectionReasons,
    R009DisabledPreviewResponse? PreviewResponse,
    string IdempotencyHash,
    string AuditHash);

public sealed record R009DisabledPreviewBatchResponse(
    string BatchRequestId,
    string BatchStatus,
    R009DisabledPreviewBatchValidationResult Validation,
    IReadOnlyList<R009DisabledPreviewBatchItemResult> ItemResults,
    int PreviewReadyCount,
    int HeldMissingReadinessCount,
    int RejectedCount,
    bool NonExecutable,
    bool NotAnOrder,
    bool NotSubmitted,
    bool NoBrokerRoute,
    bool NoFill,
    bool NoExecutionReport,
    bool NoRoute,
    bool NoSubmission,
    bool NoPaperLedgerCommit,
    string IdempotencyHash,
    string AuditHash);

public sealed record R009PreviewUsagePolicy(
    IReadOnlyList<string> AllowedUsages,
    IReadOnlyList<string> ForbiddenUsages,
    bool PreviewOutputIsOrderIntent,
    bool PreviewOutputIsRouteable,
    bool PreviewOutputIsExecutableSchedule,
    bool PreviewOutputIsFillReportInput,
    bool OperatorReviewDryRunOnly)
{
    public static R009PreviewUsagePolicy Default { get; } = new(
        AllowedUsages: new[]
        {
            "DisplayToOperator",
            "PersistAsReadinessArtifact",
            "ComparePolicies",
            "GenerateManualReviewNote",
            "FeedFutureNoExternalPaperOnlyEvaluation"
        },
        ForbiddenUsages: new[]
        {
            "ConvertToOrder",
            "ConvertToChildOrder",
            "ConvertToRouteSubmission",
            "CommitLedger",
            "TriggerBroker",
            "TriggerSchedulerWorker",
            "MutatePositionsState",
            "GenerateFillExecutionReport"
        },
        PreviewOutputIsOrderIntent: false,
        PreviewOutputIsRouteable: false,
        PreviewOutputIsExecutableSchedule: false,
        PreviewOutputIsFillReportInput: false,
        OperatorReviewDryRunOnly: true);
}

public sealed record R009PreviewBoundaryGuard(
    bool NonExecutable,
    bool NotAnOrder,
    bool NotSubmitted,
    bool NoBrokerRoute,
    bool NoRoute,
    bool NoSubmission,
    bool NoFill,
    bool NoExecutionReport,
    bool NoPaperLedgerCommit,
    bool NoStateMutation)
{
    public static R009PreviewBoundaryGuard Required { get; } = new(
        NonExecutable: true,
        NotAnOrder: true,
        NotSubmitted: true,
        NoBrokerRoute: true,
        NoRoute: true,
        NoSubmission: true,
        NoFill: true,
        NoExecutionReport: true,
        NoPaperLedgerCommit: true,
        NoStateMutation: true);
}

public sealed record R009PreviewConsumerRequestEnvelope(
    string ConsumerRequestId,
    R009PreviewConsumerType ConsumerType,
    string ConsumerName,
    IReadOnlyList<string> RequestedUsages,
    R009DisabledPreviewRequest? SinglePreviewRequest,
    R009DisabledPreviewBatchRequest? BatchPreviewRequest);

public sealed record R009PreviewBoundaryGuardResult(
    bool ConsumerAllowed,
    bool UsageAllowed,
    bool ResponseSafe,
    bool Accepted,
    IReadOnlyList<string> RejectionReasons);

public sealed record R009PreviewConsumerAuditRecord(
    string ConsumerRequestId,
    R009PreviewConsumerType ConsumerType,
    string AuditHash,
    DateTimeOffset CreatedAtUtc,
    bool NoOrderDomainOutput,
    bool NoBrokerRoute,
    bool NoStateMutation,
    bool DryRunOnly);

public sealed record R009PreviewConsumerResponseEnvelope(
    string ConsumerRequestId,
    R009PreviewConsumerType ConsumerType,
    R009PreviewConsumerRequestEnvelope OriginalRequest,
    bool Accepted,
    IReadOnlyList<string> RejectionReasons,
    R009DisabledPreviewResponse? SinglePreviewResponse,
    R009DisabledPreviewBatchResponse? BatchPreviewResponse,
    R009PreviewUsagePolicy UsagePolicy,
    R009PreviewBoundaryGuard BoundaryGuard,
    R009PreviewBoundaryGuardResult BoundaryGuardResult,
    R009PreviewConsumerAuditRecord AuditRecord,
    bool NonExecutable,
    bool NotAnOrder,
    bool NotSubmitted,
    bool NoBrokerRoute,
    bool NoRoute,
    bool NoSubmission,
    bool NoFill,
    bool NoExecutionReport,
    bool NoPaperLedgerCommit,
    bool NoStateMutation);

public sealed record R009PreviewRequestAuditRecord(
    string RequestId,
    string? BatchRequestId,
    string? DecisionPreviewId,
    R009PreviewConsumerType ConsumerType,
    R009DisabledPreviewRequestMode RequestMode,
    string R009ContractVersion,
    string InputHash,
    string? DecisionHash,
    string AuditHash,
    DateTimeOffset CreatedAtUtc,
    bool DryRunOnly,
    bool NonExecutable,
    bool NotAnOrder,
    bool NotSubmitted,
    bool NoBrokerRoute,
    bool NoOrderDomainPersistence,
    bool NoTradingStateMutation,
    bool NoPaperLedgerCommit,
    string RetentionCategory);

public sealed record R009PreviewResponseAuditRecord(
    string RequestId,
    string? BatchRequestId,
    string? DecisionPreviewId,
    R009PreviewConsumerType ConsumerType,
    R009DisabledPreviewRequestMode RequestMode,
    string R009ContractVersion,
    string InputHash,
    string? DecisionHash,
    string AuditHash,
    DateTimeOffset CreatedAtUtc,
    string DecisionStatus,
    bool Accepted,
    bool DryRunOnly,
    bool NonExecutable,
    bool NotAnOrder,
    bool NotSubmitted,
    bool NoBrokerRoute,
    bool NoOrderDomainPersistence,
    bool NoTradingStateMutation,
    bool NoPaperLedgerCommit,
    string RetentionCategory);

public sealed record R009PreviewBatchAuditRecord(
    string RequestId,
    string BatchRequestId,
    string? DecisionPreviewId,
    R009PreviewConsumerType ConsumerType,
    R009DisabledPreviewRequestMode RequestMode,
    string R009ContractVersion,
    string InputHash,
    string? DecisionHash,
    string AuditHash,
    DateTimeOffset CreatedAtUtc,
    string BatchStatus,
    int ItemCount,
    int PreviewReadyCount,
    int HeldMissingReadinessCount,
    int RejectedCount,
    bool DryRunOnly,
    bool NonExecutable,
    bool NotAnOrder,
    bool NotSubmitted,
    bool NoBrokerRoute,
    bool NoOrderDomainPersistence,
    bool NoTradingStateMutation,
    bool NoPaperLedgerCommit,
    string RetentionCategory);

public sealed record R009PreviewAuditEnvelope(
    string AuditEnvelopeId,
    string RequestId,
    string? BatchRequestId,
    R009PreviewRequestAuditRecord RequestAudit,
    R009PreviewResponseAuditRecord? ResponseAudit,
    R009PreviewBatchAuditRecord? BatchAudit,
    string AuditHash,
    bool ArtifactOnly,
    bool NoDbPersistence,
    bool NoOrderDomainPersistence,
    bool NoRouteSubmissionPersistence,
    bool NoLedgerPersistence,
    bool NoTradingStateMutation);

public sealed record R009PreviewAuditStoreContract(
    string StoreName,
    string RootPath,
    bool ArtifactOnly,
    bool DbRequired,
    bool ExternalServiceRequired,
    bool OrderDomainPersistenceAllowed,
    bool RouteSubmissionPersistenceAllowed,
    bool LedgerPersistenceAllowed,
    bool TradingStateMutationAllowed);

public sealed record R009PreviewAuditPersistenceResult(
    string Status,
    string? ArtifactPath,
    bool Persisted,
    bool ReplaySafe,
    bool Conflict,
    string? ExistingAuditHash,
    string? AuditHash,
    IReadOnlyList<string> Reasons);

public enum R009OperatorPreviewReviewMode
{
    ListAuditRecords,
    ShowAuditRecord,
    SummarizeBatch,
    ExportOperatorReport,
    Execute,
    Submit,
    Route,
    Fill,
    CommitLedger,
    ActivateBroker,
    StartScheduler,
    PromoteLive
}

public sealed record R009OperatorPreviewReviewRequest(
    string ReviewRequestId,
    R009OperatorPreviewReviewMode CommandMode,
    R009PreviewConsumerType ConsumerType,
    string? RequestId,
    string? BatchRequestId,
    string AuditRootPath,
    string OutputRootPath);

public sealed record R009OperatorAuditRecordReference(
    string RequestId,
    string? BatchRequestId,
    string? DecisionPreviewId,
    R009PreviewConsumerType ConsumerType,
    string AuditHash,
    string InputHash,
    string? DecisionHash,
    DateTimeOffset CreatedAtUtc,
    string ArtifactPath,
    bool NonExecutable,
    bool NotAnOrder,
    bool NoBrokerRoute,
    bool NoPaperLedgerCommit);

public sealed record R009OperatorHeldReasonSummary(
    string Reason,
    int Count,
    bool HeldNotOrder);

public sealed record R009OperatorRejectedReasonSummary(
    string Reason,
    int Count,
    bool RejectedNotOrder);

public sealed record R009OperatorPreviewSummary(
    int AuditRecordCount,
    int SinglePreviewAuditCount,
    int BatchPreviewAuditCount,
    int PreviewReadyCount,
    int HeldMissingReadinessCount,
    int RejectedCount,
    IReadOnlyList<R009OperatorHeldReasonSummary> HeldReasons,
    IReadOnlyList<R009OperatorRejectedReasonSummary> RejectedReasons,
    bool NonExecutable,
    bool NotAnOrder,
    bool NoBrokerRoute);

public sealed record R009OperatorReviewExport(
    string ExportId,
    string? ArtifactPath,
    bool Written,
    bool ReviewOnly,
    bool NonExecutable,
    bool NotAnOrder,
    bool NoBrokerRoute,
    bool NoPaperLedgerCommit);

public sealed record R009OperatorPreviewReviewResponse(
    string ReviewRequestId,
    R009OperatorPreviewReviewMode CommandMode,
    bool Accepted,
    IReadOnlyList<string> RejectionReasons,
    IReadOnlyList<R009OperatorAuditRecordReference> AuditRecords,
    R009OperatorPreviewSummary Summary,
    R009PreviewAuditEnvelope? SelectedAuditRecord,
    R009OperatorReviewExport? Export,
    bool NonExecutable,
    bool NotAnOrder,
    bool NotSubmitted,
    bool NoBrokerRoute,
    bool NoFill,
    bool NoExecutionReport,
    bool NoRoute,
    bool NoSubmission,
    bool NoPaperLedgerCommit,
    bool ReviewOnly,
    bool ExecutableApproval,
    bool BrokerApproval,
    bool LiveApproval);

public enum R009PaperLedgerPreviewStatus
{
    PaperLedgerPreviewReady,
    HeldLedgerPreview,
    RejectedLedgerPreview
}

public sealed record R009PaperLedgerPreviewRequest(
    string RequestId,
    string SourceDecisionPreviewId,
    string SourceAuditRecordId,
    R009PreviewConsumerType SourceConsumerType,
    string R009ContractVersion,
    bool PreviewOnly,
    bool PaperLedgerPreviewEnabled,
    bool PaperLedgerCommitEnabled,
    bool LedgerMutationAllowed,
    bool TradingStateMutationAllowed,
    bool OrderDomainInputAllowed,
    bool NonExecutable,
    bool NotAnOrder,
    bool NotSubmitted,
    bool NoBrokerRoute,
    bool BrokerRoutingEnabled = false,
    bool LiveTradingEnabled = false,
    bool ExecutableScheduleEnabled = false);

public sealed record R009PaperLedgerPreviewLine(
    string LineId,
    string SourceDecisionId,
    string ExecutionIntentId,
    string Symbol,
    string ExecutionTradableSymbol,
    string NormalizedPortfolioSymbol,
    bool RequiresInversion,
    R009PaperLedgerPreviewStatus Status,
    string? HoldReason,
    string? RejectionReason,
    bool NonExecutable,
    bool NotAnOrder,
    bool NotSubmitted,
    bool NoBrokerRoute,
    bool NoFill,
    bool NoExecutionReport,
    bool NoRoute,
    bool NoSubmission,
    bool NoPaperLedgerCommit,
    bool LedgerMutation,
    bool TradingStateMutation);

public sealed record R009HypotheticalPositionDeltaPreview(
    string LineId,
    string Symbol,
    decimal QuantityDelta,
    decimal NotionalDelta,
    bool HypotheticalOnly,
    bool LedgerMutation,
    bool TradingStateMutation);

public sealed record R009HypotheticalCashImpactPreview(
    string LineId,
    string Currency,
    decimal CashDelta,
    bool HypotheticalOnly,
    bool LedgerMutation,
    bool TradingStateMutation);

public sealed record R009HypotheticalExposurePreview(
    decimal GrossNotionalDelta,
    decimal NetNotionalDelta,
    IReadOnlyDictionary<string, decimal> NotionalBySymbol,
    bool HypotheticalOnly,
    bool LedgerMutation,
    bool TradingStateMutation);

public sealed record R009PaperLedgerPreviewResponse(
    string RequestId,
    string PaperLedgerPreviewId,
    R009PaperLedgerPreviewStatus PreviewStatus,
    bool Accepted,
    IReadOnlyList<string> RejectionReasons,
    IReadOnlyList<R009PaperLedgerPreviewLine> PreviewLines,
    IReadOnlyList<R009HypotheticalPositionDeltaPreview> HypotheticalPositionDeltas,
    IReadOnlyList<R009HypotheticalCashImpactPreview> HypotheticalCashImpacts,
    R009HypotheticalExposurePreview HypotheticalExposurePreview,
    string SourceDecisionHash,
    string InputHash,
    string PreviewHash,
    string AuditHash,
    bool PreviewOnly,
    bool PaperLedgerCommit,
    bool LedgerMutation,
    bool TradingStateMutation,
    bool NonExecutable,
    bool NotAnOrder,
    bool NotSubmitted,
    bool NoBrokerRoute,
    bool NoFill,
    bool NoExecutionReport,
    bool NoRoute,
    bool NoSubmission);

public sealed record R009PaperLedgerPreviewAuditRecord(
    string RequestId,
    string PaperLedgerPreviewId,
    string SourceDecisionPreviewId,
    string SourceAuditRecordId,
    R009PreviewConsumerType SourceConsumerType,
    string R009ContractVersion,
    string InputHash,
    string PreviewHash,
    string AuditHash,
    DateTimeOffset CreatedAtUtc,
    bool PreviewOnly,
    bool PaperLedgerCommit,
    bool LedgerMutation,
    bool TradingStateMutation,
    bool NoPaperLedgerTables,
    bool NoOrderDomainPersistence,
    bool NoRouteSubmissionPersistence,
    bool NoFillReportPersistence,
    string RetentionCategory);

public sealed record R009PaperLedgerPreviewArtifactEnvelope(
    string EnvelopeId,
    R009PaperLedgerPreviewRequest Request,
    R009PaperLedgerPreviewResponse Response,
    R009PaperLedgerPreviewAuditRecord AuditRecord,
    bool ArtifactOnly,
    bool NoDbPersistence,
    bool NoPaperLedgerTableWrites,
    bool NoOrderDomainPersistence,
    bool NoRouteSubmissionPersistence,
    bool NoFillReportPersistence,
    bool NoTradingStateMutation);

public sealed record R009PaperLedgerPreviewArtifactWriterContract(
    string RootPath,
    bool ArtifactOnly,
    bool DbRequired,
    bool PaperLedgerTableWritesAllowed,
    bool OrderDomainPersistenceAllowed,
    bool RouteSubmissionPersistenceAllowed,
    bool FillReportPersistenceAllowed,
    bool TradingStateMutationAllowed);

public sealed record R009PaperLedgerPreviewPersistenceResult(
    string Status,
    string? ArtifactPath,
    bool Persisted,
    bool ReplaySafe,
    bool Conflict,
    string? ExistingAuditHash,
    string? AuditHash,
    IReadOnlyList<string> Reasons);

public sealed record R009PaperLedgerPreviewBoundaryGuard(
    bool PaperLedgerPreviewEnabled,
    bool PaperLedgerCommitEnabled,
    bool LedgerMutationAllowed,
    bool TradingStateMutationAllowed,
    bool OrderDomainInputAllowed,
    bool BrokerRoutingEnabled,
    bool LiveTradingEnabled,
    bool ExecutableScheduleEnabled)
{
    public static R009PaperLedgerPreviewBoundaryGuard PreviewOnly { get; } = new(
        PaperLedgerPreviewEnabled: true,
        PaperLedgerCommitEnabled: false,
        LedgerMutationAllowed: false,
        TradingStateMutationAllowed: false,
        OrderDomainInputAllowed: false,
        BrokerRoutingEnabled: false,
        LiveTradingEnabled: false,
        ExecutableScheduleEnabled: false);
}

public sealed class R009PaperPlanExecutionIntentConverter
{
    public R009EmsOmsExecutionIntent Convert(R009PaperPlanPreviewLine line, string sourceArtifact)
    {
        var executionSymbol = NormalizeRequired(line.ExecutionTradableSymbol, nameof(line.ExecutionTradableSymbol));
        var canonicalTargetCloseUtc = DateTimeOffset.Parse(line.CanonicalTargetCloseTimestamp, null, System.Globalization.DateTimeStyles.AssumeUniversal);
        var riskApproval = string.Equals(line.RiskReviewStatus, "ApprovedForNonExecutablePreview", StringComparison.OrdinalIgnoreCase)
            ? R009ApprovalStatus.ApprovedForDesignOnlyPreviewOnly
            : R009ApprovalStatus.Missing;
        var operatorApproval = string.Equals(line.OperatorApprovalStatus, "ApprovedForDesignOnlyPreviewOnly", StringComparison.OrdinalIgnoreCase)
            ? R009ApprovalStatus.ApprovedForDesignOnlyPreviewOnly
            : R009ApprovalStatus.Missing;

        return new R009EmsOmsExecutionIntent(
            ExecutionIntentId: $"{NormalizeRequired(line.PaperExecutionPlanLineId, nameof(line.PaperExecutionPlanLineId))}:r009-disabled-intent",
            SourcePmsCycleId: string.IsNullOrWhiteSpace(line.RequestedCycleRunId) ? sourceArtifact : line.RequestedCycleRunId,
            SourceQubesRunId: string.IsNullOrWhiteSpace(line.QubesRunId) ? sourceArtifact : line.QubesRunId,
            SourceRebalanceIntentId: string.IsNullOrWhiteSpace(line.BatchEntryId) ? sourceArtifact : line.BatchEntryId,
            SourceRiskReviewId: $"{(string.IsNullOrWhiteSpace(line.BatchEntryId) ? sourceArtifact : line.BatchEntryId)}:risk-operator-preview",
            Symbol: NormalizeRequired(line.Symbol, nameof(line.Symbol)),
            ExecutionTradableSymbol: executionSymbol,
            NormalizedPortfolioSymbol: NormalizeRequired(line.NormalizedPortfolioSymbol, nameof(line.NormalizedPortfolioSymbol)),
            RequiresInversion: line.RequiresInversion,
            Side: string.Equals(line.Side, "Sell", StringComparison.OrdinalIgnoreCase) ? R009LiveIntentSide.Sell : R009LiveIntentSide.Buy,
            TargetQuantity: Math.Abs(line.TargetQuantity ?? 0m),
            TargetNotional: Math.Abs(line.TargetNotional ?? 0m),
            CanonicalTargetCloseUtc: canonicalTargetCloseUtc,
            CanonicalTargetCloseLocal: NormalizeRequired(line.CanonicalTargetCloseLocal, nameof(line.CanonicalTargetCloseLocal)),
            CanonicalSession: NormalizeRequired(line.CanonicalSession, nameof(line.CanonicalSession)),
            BarRole: ParseBarRole(line.BarRole),
            MustEndFlat: true,
            OvernightAllowed: false,
            QuoteWindowReadinessId: ReadyBindingIdOrNull(line.QuoteWindowReadinessBinding),
            CloseBenchmarkReadinessId: ReadyBindingIdOrNull(line.CloseBenchmarkReadinessBinding),
            FeedQualityReadinessId: ReadyBindingIdOrNull(line.FeedQualityReadinessBinding),
            R009ContractVersion: string.IsNullOrWhiteSpace(line.R009ContractVersion) ? R009DisabledEmsOmsExecutionAdapter.ContractVersion : line.R009ContractVersion,
            OperatorApprovalStatus: operatorApproval,
            RiskApprovalStatus: riskApproval,
            LiveTradingEnabled: false,
            BrokerRoutingEnabled: false,
            OrderSubmissionEnabled: false,
            NonExecutable: line.NonExecutable && line.NotAnOrder && line.NotSubmitted && line.NoBrokerRoute,
            SecurityID: executionSymbol.Equals("USDJPY", StringComparison.OrdinalIgnoreCase) ? "4004" : null,
            SecurityIDSource: executionSymbol.Equals("USDJPY", StringComparison.OrdinalIgnoreCase) ? "8" : null);
    }

    private static string NormalizeRequired(string value, string fieldName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{fieldName} is required.", fieldName)
            : value;

    private static string? ReadyBindingIdOrNull(R009ReadinessBindingPreview? binding)
        => binding is not null &&
            string.Equals(binding.ReadinessStatus, "Ready", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(binding.BindingId)
            ? binding.BindingId
            : null;

    private static R009LiveBarRole ParseBarRole(string value)
        => Enum.TryParse<R009LiveBarRole>(value, ignoreCase: true, out var role)
            ? role
            : R009LiveBarRole.IntradayRebalance;
}

public sealed class R009DisabledDecisionPreviewIntegrationService
{
    private readonly R009PaperPlanExecutionIntentConverter _converter;
    private readonly R009DisabledEmsOmsExecutionAdapter _adapter;

    public R009DisabledDecisionPreviewIntegrationService(
        R009PaperPlanExecutionIntentConverter? converter = null,
        R009DisabledEmsOmsExecutionAdapter? adapter = null)
    {
        _converter = converter ?? new R009PaperPlanExecutionIntentConverter();
        _adapter = adapter ?? new R009DisabledEmsOmsExecutionAdapter();
    }

    public R009DisabledDecisionPreviewFlowResult GenerateDecisionPreviews(
        IEnumerable<R009PaperPlanPreviewLine> paperPlanLines,
        string sourceArtifact,
        DateTimeOffset? createdAtUtc = null)
    {
        var intents = new List<R009EmsOmsExecutionIntent>();
        var decisions = new List<R009DisabledExecutionDecision>();

        foreach (var line in paperPlanLines)
        {
            var intent = _converter.Convert(line, sourceArtifact);
            intents.Add(intent);
            decisions.Add(_adapter.Decide(
                intent,
                R009LiveFeatureFlags.DisabledDefaults,
                R009DisabledBoundaryGuard.Disabled,
                createdAtUtc));
        }

        return new R009DisabledDecisionPreviewFlowResult(sourceArtifact, intents, decisions);
    }
}

public sealed class R009DisabledPreviewContractService
{
    private static readonly HashSet<string> ForbiddenOutputs = new(StringComparer.OrdinalIgnoreCase)
    {
        "Order",
        "ChildOrder",
        "Route",
        "Submission",
        "Fill",
        "ExecutionReport",
        "ExecutableSchedule"
    };

    private readonly R009DisabledDecisionPreviewIntegrationService _integrationService;
    private readonly R009DisabledEmsOmsExecutionAdapter _adapter;

    public R009DisabledPreviewContractService(
        R009DisabledDecisionPreviewIntegrationService? integrationService = null,
        R009DisabledEmsOmsExecutionAdapter? adapter = null)
    {
        _integrationService = integrationService ?? new R009DisabledDecisionPreviewIntegrationService();
        _adapter = adapter ?? new R009DisabledEmsOmsExecutionAdapter();
    }

    public R009DisabledPreviewResponse Preview(
        R009DisabledPreviewRequest request,
        IEnumerable<R009PaperPlanPreviewLine>? artifactPaperPlanLines = null,
        DateTimeOffset? createdAtUtc = null)
    {
        var reasons = ValidateRequest(request).ToList();
        IReadOnlyList<R009DisabledExecutionDecision> decisions = Array.Empty<R009DisabledExecutionDecision>();

        if (reasons.Count == 0)
        {
            decisions = request.SourceType == R009DisabledPreviewSourceType.ExecutionIntent
                ? new[] { _adapter.Decide(request.ExecutionIntent!, R009LiveFeatureFlags.DisabledDefaults, R009DisabledBoundaryGuard.Disabled, createdAtUtc) }
                : _integrationService.GenerateDecisionPreviews(artifactPaperPlanLines!, request.SourceArtifactPath!, createdAtUtc).DecisionPreviews;
        }

        var heldReasons = decisions
            .Where(x => !string.IsNullOrWhiteSpace(x.HoldReason))
            .Select(x => x.HoldReason!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var accepted = reasons.Count == 0;
        var status = accepted
            ? decisions.Any(x => x.LineStatus != R009LiveLineStatus.PreviewReady) ? "PreviewGeneratedWithHeldLines" : "PreviewGenerated"
            : "Rejected";
        var decisionPreviewId = $"{request.RequestId}:r009-disabled-preview-response";
        var idempotencyHash = Hash(string.Join("|", request.RequestId, request.SourceType, request.SourceArtifactPath, request.R009ContractVersion));
        var auditHash = Hash(string.Join("|", decisionPreviewId, status, decisions.Count, string.Join(";", reasons), string.Join(";", heldReasons)));

        return new R009DisabledPreviewResponse(
            request.RequestId,
            decisionPreviewId,
            status,
            accepted,
            reasons,
            decisions,
            heldReasons,
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true,
            NoFill: true,
            NoExecutionReport: true,
            NoRoute: true,
            NoSubmission: true,
            NoPaperLedgerCommit: true,
            new R009DisabledPreviewSafetyFlags(
                DryRunOnly: true,
                LiveTradingEnabled: false,
                BrokerRoutingEnabled: false,
                OrderSubmissionEnabled: false,
                ExecutableScheduleEnabled: false,
                PaperLedgerCommitEnabled: false,
                SchedulerEnabled: false,
                BackgroundWorkerEnabled: false,
                NoBrokerRoute: true),
            idempotencyHash,
            auditHash);
    }

    private static IEnumerable<string> ValidateRequest(R009DisabledPreviewRequest request)
    {
        if (request.RequestMode != R009DisabledPreviewRequestMode.DisabledPreviewOnly)
        {
            yield return "RequestModeMustBeDisabledPreviewOnly";
        }

        if (!string.Equals(request.R009ContractVersion, R009DisabledEmsOmsExecutionAdapter.ContractVersion, StringComparison.Ordinal))
        {
            yield return "R009ContractVersionMismatch";
        }

        if (!request.DryRunOnly) yield return "DryRunOnlyRequired";
        if (request.LiveTradingEnabled) yield return "LiveTradingMustRemainDisabled";
        if (request.BrokerRoutingEnabled) yield return "BrokerRoutingMustRemainDisabled";
        if (request.OrderSubmissionEnabled) yield return "OrderSubmissionMustRemainDisabled";
        if (request.ExecutableScheduleEnabled) yield return "ExecutableScheduleMustRemainDisabled";
        if (request.PaperLedgerCommitEnabled) yield return "PaperLedgerCommitMustRemainDisabled";
        if (!request.NoBrokerRoute) yield return "NoBrokerRouteRequired";

        if (!string.Equals(request.OperatorApprovalScope, "DesignOnlyPreviewOnly", StringComparison.Ordinal))
        {
            yield return "OperatorApprovalScopeMustBeDesignOnlyPreviewOnly";
        }

        if (!string.Equals(request.RiskApprovalScope, "DesignOnlyPreviewOnly", StringComparison.Ordinal))
        {
            yield return "RiskApprovalScopeMustBeDesignOnlyPreviewOnly";
        }

        foreach (var output in request.RequestedOutputs ?? Array.Empty<string>())
        {
            if (ForbiddenOutputs.Contains(output))
            {
                yield return $"ForbiddenOutputRequested:{output}";
            }
        }

        if (request.SourceType == R009DisabledPreviewSourceType.ExecutionIntent)
        {
            if (request.ExecutionIntent is null)
            {
                yield return "ExecutionIntentRequired";
            }
            else
            {
                if (request.ExecutionIntent.LiveTradingEnabled) yield return "ExecutionIntentLiveTradingMustRemainDisabled";
                if (request.ExecutionIntent.BrokerRoutingEnabled) yield return "ExecutionIntentBrokerRoutingMustRemainDisabled";
                if (request.ExecutionIntent.OrderSubmissionEnabled) yield return "ExecutionIntentOrderSubmissionMustRemainDisabled";
                if (!request.ExecutionIntent.NonExecutable) yield return "ExecutionIntentMustBeNonExecutable";
            }
        }
        else if (request.SourceType == R009DisabledPreviewSourceType.PaperPlanLineArtifact)
        {
            if (string.IsNullOrWhiteSpace(request.SourceArtifactPath))
            {
                yield return "SourceArtifactPathRequired";
            }
        }
        else
        {
            yield return "UnsupportedSourceType";
        }
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public sealed class R009OperatorPreviewReviewService
{
    private static readonly HashSet<R009OperatorPreviewReviewMode> AllowedModes = new()
    {
        R009OperatorPreviewReviewMode.ListAuditRecords,
        R009OperatorPreviewReviewMode.ShowAuditRecord,
        R009OperatorPreviewReviewMode.SummarizeBatch,
        R009OperatorPreviewReviewMode.ExportOperatorReport
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public R009OperatorPreviewReviewResponse Review(R009OperatorPreviewReviewRequest request)
    {
        var reasons = ValidateRequest(request).ToList();
        var records = reasons.Count == 0 ? LoadAuditRecords(request.AuditRootPath) : Array.Empty<(R009PreviewAuditEnvelope Envelope, string Path)>();
        var selected = reasons.Count == 0 ? SelectAuditRecord(request, records) : null;

        if (reasons.Count == 0 && request.CommandMode is R009OperatorPreviewReviewMode.ShowAuditRecord or R009OperatorPreviewReviewMode.SummarizeBatch && selected is null)
        {
            reasons.Add("AuditRecordNotFound");
        }

        if (reasons.Count == 0 && request.CommandMode == R009OperatorPreviewReviewMode.SummarizeBatch && selected?.Envelope.BatchAudit is null)
        {
            reasons.Add("BatchAuditRecordRequired");
        }

        var accepted = reasons.Count == 0;
        var visibleRecords = accepted
            ? BuildReferences(request.CommandMode == R009OperatorPreviewReviewMode.ListAuditRecords ? records : selected is null ? records : new[] { selected.Value })
            : Array.Empty<R009OperatorAuditRecordReference>();
        var summary = BuildSummary(accepted ? records.Select(x => x.Envelope).ToArray() : Array.Empty<R009PreviewAuditEnvelope>());
        var export = accepted && request.CommandMode == R009OperatorPreviewReviewMode.ExportOperatorReport
            ? ExportReport(request, records.Select(x => x.Envelope).ToArray(), summary)
            : null;

        return new R009OperatorPreviewReviewResponse(
            request.ReviewRequestId,
            request.CommandMode,
            accepted,
            reasons,
            visibleRecords,
            summary,
            accepted ? selected?.Envelope : null,
            export,
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true,
            NoFill: true,
            NoExecutionReport: true,
            NoRoute: true,
            NoSubmission: true,
            NoPaperLedgerCommit: true,
            ReviewOnly: true,
            ExecutableApproval: false,
            BrokerApproval: false,
            LiveApproval: false);
    }

    private static IEnumerable<string> ValidateRequest(R009OperatorPreviewReviewRequest request)
    {
        if (!AllowedModes.Contains(request.CommandMode))
        {
            yield return $"ForbiddenCommandMode:{request.CommandMode}";
        }

        if (!R009PreviewConsumerBoundaryService.IsAllowedConsumer(request.ConsumerType))
        {
            yield return $"ForbiddenConsumer:{request.ConsumerType}";
        }

        if (!IsArtifactAuditPath(request.AuditRootPath))
        {
            yield return "AuditReadPathMustBeArtifactsReadinessExecutionLiveAudit";
        }

        if (!IsOperatorReviewPath(request.OutputRootPath))
        {
            yield return "ReviewWritePathMustBeArtifactsReadinessExecutionLiveOperatorReview";
        }
    }

    private static (R009PreviewAuditEnvelope Envelope, string Path)? SelectAuditRecord(
        R009OperatorPreviewReviewRequest request,
        IReadOnlyList<(R009PreviewAuditEnvelope Envelope, string Path)> records)
    {
        if (!string.IsNullOrWhiteSpace(request.RequestId))
        {
            return records.FirstOrDefault(x => string.Equals(x.Envelope.RequestId, request.RequestId, StringComparison.Ordinal));
        }

        if (!string.IsNullOrWhiteSpace(request.BatchRequestId))
        {
            return records.FirstOrDefault(x => string.Equals(x.Envelope.BatchRequestId, request.BatchRequestId, StringComparison.Ordinal));
        }

        return records.FirstOrDefault();
    }

    private static IReadOnlyList<(R009PreviewAuditEnvelope Envelope, string Path)> LoadAuditRecords(string auditRootPath)
    {
        if (!Directory.Exists(auditRootPath))
        {
            return Array.Empty<(R009PreviewAuditEnvelope, string)>();
        }

        return Directory
            .EnumerateFiles(auditRootPath, "*.preview-audit.json", SearchOption.TopDirectoryOnly)
            .OrderBy(x => x, StringComparer.Ordinal)
            .Select(path => (Envelope: JsonSerializer.Deserialize<R009PreviewAuditEnvelope>(File.ReadAllText(path), JsonOptions), Path: path))
            .Where(x => x.Envelope is not null)
            .Select(x => (x.Envelope!, x.Path))
            .ToArray();
    }

    private static IReadOnlyList<R009OperatorAuditRecordReference> BuildReferences(
        IReadOnlyList<(R009PreviewAuditEnvelope Envelope, string Path)> records)
        => records.Select(x => new R009OperatorAuditRecordReference(
            x.Envelope.RequestId,
            x.Envelope.BatchRequestId,
            x.Envelope.RequestAudit.DecisionPreviewId,
            x.Envelope.RequestAudit.ConsumerType,
            x.Envelope.AuditHash,
            x.Envelope.RequestAudit.InputHash,
            x.Envelope.RequestAudit.DecisionHash,
            x.Envelope.RequestAudit.CreatedAtUtc,
            x.Path,
            NonExecutable: true,
            NotAnOrder: true,
            NoBrokerRoute: true,
            NoPaperLedgerCommit: true)).ToArray();

    private static R009OperatorPreviewSummary BuildSummary(IReadOnlyList<R009PreviewAuditEnvelope> records)
    {
        var previewReady = records.Sum(x => x.BatchAudit?.PreviewReadyCount ?? (IsAcceptedSinglePreview(x) ? 1 : 0));
        var held = records.Sum(x => x.BatchAudit?.HeldMissingReadinessCount ?? 0);
        var rejected = records.Sum(x => x.BatchAudit?.RejectedCount ?? 0);

        return new R009OperatorPreviewSummary(
            AuditRecordCount: records.Count,
            SinglePreviewAuditCount: records.Count(x => x.ResponseAudit is not null),
            BatchPreviewAuditCount: records.Count(x => x.BatchAudit is not null),
            PreviewReadyCount: previewReady,
            HeldMissingReadinessCount: held,
            RejectedCount: rejected,
            HeldReasons: held > 0
                ? new[] { new R009OperatorHeldReasonSummary("HeldMissingReadiness", held, HeldNotOrder: true) }
                : Array.Empty<R009OperatorHeldReasonSummary>(),
            RejectedReasons: rejected > 0
                ? new[] { new R009OperatorRejectedReasonSummary("Rejected", rejected, RejectedNotOrder: true) }
                : Array.Empty<R009OperatorRejectedReasonSummary>(),
            NonExecutable: true,
            NotAnOrder: true,
            NoBrokerRoute: true);
    }

    private static bool IsAcceptedSinglePreview(R009PreviewAuditEnvelope envelope)
        => envelope.ResponseAudit is not null &&
           string.Equals(envelope.ResponseAudit.DecisionStatus, "PreviewReady", StringComparison.Ordinal);

    private static R009OperatorReviewExport ExportReport(
        R009OperatorPreviewReviewRequest request,
        IReadOnlyList<R009PreviewAuditEnvelope> records,
        R009OperatorPreviewSummary summary)
    {
        Directory.CreateDirectory(request.OutputRootPath);
        var path = Path.Combine(request.OutputRootPath, $"{SanitizeFileName(request.ReviewRequestId)}.operator-review.md");
        var report = new StringBuilder()
            .AppendLine("# R009 Disabled Preview Operator Review")
            .AppendLine()
            .AppendLine($"ReviewRequestId: {request.ReviewRequestId}")
            .AppendLine($"AuditRecords: {summary.AuditRecordCount}")
            .AppendLine($"PreviewReady: {summary.PreviewReadyCount}")
            .AppendLine($"HeldMissingReadiness: {summary.HeldMissingReadinessCount}")
            .AppendLine($"Rejected: {summary.RejectedCount}")
            .AppendLine()
            .AppendLine("NonExecutable=true")
            .AppendLine("NotAnOrder=true")
            .AppendLine("NoBrokerRoute=true")
            .AppendLine("NoPaperLedgerCommit=true")
            .AppendLine("ExecutableApproval=false")
            .AppendLine("BrokerApproval=false")
            .AppendLine("LiveApproval=false")
            .AppendLine()
            .AppendLine("Records:")
            .AppendJoin(Environment.NewLine, records.Select(x => $"- {x.RequestId} auditHash={x.AuditHash}"))
            .ToString();
        File.WriteAllText(path, report);

        return new R009OperatorReviewExport(
            ExportId: $"{request.ReviewRequestId}:operator-review-export",
            ArtifactPath: path,
            Written: true,
            ReviewOnly: true,
            NonExecutable: true,
            NotAnOrder: true,
            NoBrokerRoute: true,
            NoPaperLedgerCommit: true);
    }

    private static bool IsArtifactAuditPath(string path)
        => Path.GetFullPath(path).Replace('\\', '/').Contains("artifacts/readiness/execution-live/audit", StringComparison.OrdinalIgnoreCase);

    private static bool IsOperatorReviewPath(string path)
        => Path.GetFullPath(path).Replace('\\', '/').Contains("artifacts/readiness/execution-live/operator-review", StringComparison.OrdinalIgnoreCase);

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(invalid.Contains(character) ? '-' : character);
        }

        return builder.ToString();
    }
}

public sealed class R009PaperLedgerPreviewService
{
    public R009PaperLedgerPreviewResponse Preview(
        R009PaperLedgerPreviewRequest request,
        IEnumerable<R009DisabledExecutionDecision> sourceDecisions,
        DateTimeOffset? createdAtUtc = null)
    {
        var decisions = sourceDecisions.ToArray();
        var reasons = ValidateRequest(request).ToList();
        reasons.AddRange(ValidateSourceDecisions(decisions));

        var previewId = $"{request.RequestId}:paper-ledger-preview";
        var sourceDecisionHash = Hash(string.Join("|", decisions.Select(x => x.Audit.R009DecisionHash).OrderBy(x => x, StringComparer.Ordinal)));
        var inputHash = Hash(string.Join("|", request.RequestId, request.SourceDecisionPreviewId, request.SourceAuditRecordId, sourceDecisionHash, request.R009ContractVersion));
        IReadOnlyList<R009PaperLedgerPreviewLine> lines = Array.Empty<R009PaperLedgerPreviewLine>();
        IReadOnlyList<R009HypotheticalPositionDeltaPreview> positionDeltas = Array.Empty<R009HypotheticalPositionDeltaPreview>();
        IReadOnlyList<R009HypotheticalCashImpactPreview> cashImpacts = Array.Empty<R009HypotheticalCashImpactPreview>();

        if (reasons.Count == 0)
        {
            lines = decisions.Select(ToLine).ToArray();
            positionDeltas = decisions
                .Where(x => x.LineStatus == R009LiveLineStatus.PreviewReady)
                .Select(ToPositionDelta)
                .ToArray();
            cashImpacts = decisions
                .Where(x => x.LineStatus == R009LiveLineStatus.PreviewReady)
                .Select(ToCashImpact)
                .ToArray();
        }

        var exposure = BuildExposure(positionDeltas);
        var status = reasons.Count > 0 || lines.Any(x => x.Status == R009PaperLedgerPreviewStatus.RejectedLedgerPreview)
            ? R009PaperLedgerPreviewStatus.RejectedLedgerPreview
            : lines.Any(x => x.Status == R009PaperLedgerPreviewStatus.HeldLedgerPreview)
                ? R009PaperLedgerPreviewStatus.HeldLedgerPreview
                : R009PaperLedgerPreviewStatus.PaperLedgerPreviewReady;
        var previewHash = Hash(string.Join("|", previewId, status, lines.Count, positionDeltas.Count, cashImpacts.Count));
        var auditHash = Hash(string.Join("|", inputHash, previewHash, string.Join(";", reasons), createdAtUtc?.ToString("O") ?? string.Empty));

        return new R009PaperLedgerPreviewResponse(
            request.RequestId,
            previewId,
            status,
            Accepted: reasons.Count == 0,
            RejectionReasons: reasons,
            PreviewLines: lines,
            HypotheticalPositionDeltas: positionDeltas,
            HypotheticalCashImpacts: cashImpacts,
            HypotheticalExposurePreview: exposure,
            sourceDecisionHash,
            inputHash,
            previewHash,
            auditHash,
            PreviewOnly: true,
            PaperLedgerCommit: false,
            LedgerMutation: false,
            TradingStateMutation: false,
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true,
            NoFill: true,
            NoExecutionReport: true,
            NoRoute: true,
            NoSubmission: true);
    }

    public R009PaperLedgerPreviewArtifactEnvelope CreateEnvelope(
        R009PaperLedgerPreviewRequest request,
        R009PaperLedgerPreviewResponse response,
        DateTimeOffset? createdAtUtc = null)
    {
        var now = createdAtUtc ?? DateTimeOffset.UtcNow;
        var audit = new R009PaperLedgerPreviewAuditRecord(
            request.RequestId,
            response.PaperLedgerPreviewId,
            request.SourceDecisionPreviewId,
            request.SourceAuditRecordId,
            request.SourceConsumerType,
            request.R009ContractVersion,
            response.InputHash,
            response.PreviewHash,
            response.AuditHash,
            now,
            PreviewOnly: true,
            PaperLedgerCommit: false,
            LedgerMutation: false,
            TradingStateMutation: false,
            NoPaperLedgerTables: true,
            NoOrderDomainPersistence: true,
            NoRouteSubmissionPersistence: true,
            NoFillReportPersistence: true,
            RetentionCategory: "PaperLedgerPreviewOnly");

        return new R009PaperLedgerPreviewArtifactEnvelope(
            EnvelopeId: $"{request.RequestId}:paper-ledger-preview-envelope",
            request,
            response,
            audit,
            ArtifactOnly: true,
            NoDbPersistence: true,
            NoPaperLedgerTableWrites: true,
            NoOrderDomainPersistence: true,
            NoRouteSubmissionPersistence: true,
            NoFillReportPersistence: true,
            NoTradingStateMutation: true);
    }

    private static IEnumerable<string> ValidateRequest(R009PaperLedgerPreviewRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId)) yield return "RequestIdRequired";
        if (string.IsNullOrWhiteSpace(request.SourceDecisionPreviewId)) yield return "SourceDecisionPreviewIdRequired";
        if (string.IsNullOrWhiteSpace(request.SourceAuditRecordId)) yield return "SourceAuditRecordIdRequired";
        if (!string.Equals(request.R009ContractVersion, R009DisabledEmsOmsExecutionAdapter.ContractVersion, StringComparison.Ordinal)) yield return "R009ContractVersionMismatch";
        if (!request.PreviewOnly) yield return "PreviewOnlyRequired";
        if (!request.PaperLedgerPreviewEnabled) yield return "PaperLedgerPreviewEnabledRequired";
        if (request.PaperLedgerCommitEnabled) yield return "PaperLedgerCommitMustRemainDisabled";
        if (request.LedgerMutationAllowed) yield return "LedgerMutationMustRemainDisabled";
        if (request.TradingStateMutationAllowed) yield return "TradingStateMutationMustRemainDisabled";
        if (request.OrderDomainInputAllowed) yield return "OrderDomainInputMustRemainDisabled";
        if (!request.NonExecutable) yield return "NonExecutableRequired";
        if (!request.NotAnOrder) yield return "NotAnOrderRequired";
        if (!request.NotSubmitted) yield return "NotSubmittedRequired";
        if (!request.NoBrokerRoute) yield return "NoBrokerRouteRequired";
        if (request.BrokerRoutingEnabled) yield return "BrokerRoutingMustRemainDisabled";
        if (request.LiveTradingEnabled) yield return "LiveTradingMustRemainDisabled";
        if (request.ExecutableScheduleEnabled) yield return "ExecutableScheduleMustRemainDisabled";
        if (!R009PreviewConsumerBoundaryService.IsAllowedConsumer(request.SourceConsumerType)) yield return $"ForbiddenConsumer:{request.SourceConsumerType}";
    }

    private static IEnumerable<string> ValidateSourceDecisions(IReadOnlyList<R009DisabledExecutionDecision> decisions)
    {
        if (decisions.Count == 0)
        {
            yield return "SourceDecisionRequired";
        }

        foreach (var decision in decisions)
        {
            if (decision.CreatesOrder || decision.CreatesChildOrder) yield return "OrderLikeSourceDecisionRejected";
            if (decision.CreatesRoute || decision.CreatesSubmission) yield return "RouteLikeSourceDecisionRejected";
            if (decision.CreatesFill || decision.CreatesExecutionReport) yield return "FillReportSourceDecisionRejected";
            if (decision.CreatesExecutableSchedule) yield return "ExecutableScheduleSourceDecisionRejected";
            if (!decision.NoPaperLedgerCommit) yield return "SourceDecisionAllowsPaperLedgerCommit";
            if (!decision.NonExecutable || !decision.NotAnOrder || !decision.NoBrokerRoute) yield return "SourceDecisionSafetyFlagsRequired";
        }
    }

    private static R009PaperLedgerPreviewLine ToLine(R009DisabledExecutionDecision decision)
    {
        var status = decision.LineStatus == R009LiveLineStatus.PreviewReady
            ? R009PaperLedgerPreviewStatus.PaperLedgerPreviewReady
            : decision.LineStatus == R009LiveLineStatus.HeldMissingReadiness
                ? R009PaperLedgerPreviewStatus.HeldLedgerPreview
                : R009PaperLedgerPreviewStatus.RejectedLedgerPreview;

        return new R009PaperLedgerPreviewLine(
            LineId: $"{decision.DecisionId}:paper-ledger-preview-line",
            decision.DecisionId,
            decision.ExecutionIntentId,
            ExtractSymbol(decision.ExecutionIntentId),
            ExtractSymbol(decision.ExecutionIntentId),
            ExtractSymbol(decision.ExecutionIntentId).Equals("USDJPY", StringComparison.OrdinalIgnoreCase) ? "JPYUSD" : ExtractSymbol(decision.ExecutionIntentId),
            ExtractSymbol(decision.ExecutionIntentId).Equals("USDJPY", StringComparison.OrdinalIgnoreCase),
            status,
            status == R009PaperLedgerPreviewStatus.HeldLedgerPreview ? decision.HoldReason : null,
            status == R009PaperLedgerPreviewStatus.RejectedLedgerPreview ? decision.HoldReason : null,
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true,
            NoFill: true,
            NoExecutionReport: true,
            NoRoute: true,
            NoSubmission: true,
            NoPaperLedgerCommit: true,
            LedgerMutation: false,
            TradingStateMutation: false);
    }

    private static R009HypotheticalPositionDeltaPreview ToPositionDelta(R009DisabledExecutionDecision decision)
        => new(
            LineId: $"{decision.DecisionId}:paper-ledger-preview-line",
            Symbol: ExtractSymbol(decision.ExecutionIntentId),
            QuantityDelta: 0m,
            NotionalDelta: 0m,
            HypotheticalOnly: true,
            LedgerMutation: false,
            TradingStateMutation: false);

    private static R009HypotheticalCashImpactPreview ToCashImpact(R009DisabledExecutionDecision decision)
        => new(
            LineId: $"{decision.DecisionId}:paper-ledger-preview-line",
            Currency: "USD",
            CashDelta: 0m,
            HypotheticalOnly: true,
            LedgerMutation: false,
            TradingStateMutation: false);

    private static R009HypotheticalExposurePreview BuildExposure(IReadOnlyList<R009HypotheticalPositionDeltaPreview> deltas)
        => new(
            GrossNotionalDelta: deltas.Sum(x => Math.Abs(x.NotionalDelta)),
            NetNotionalDelta: deltas.Sum(x => x.NotionalDelta),
            NotionalBySymbol: deltas
                .GroupBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.Sum(y => y.NotionalDelta), StringComparer.OrdinalIgnoreCase),
            HypotheticalOnly: true,
            LedgerMutation: false,
            TradingStateMutation: false);

    private static string ExtractSymbol(string executionIntentId)
    {
        var upper = executionIntentId.ToUpperInvariant();
        foreach (var symbol in R009DisabledEmsOmsExecutionAdapter.SupportedSymbols)
        {
            if (upper.Contains(symbol, StringComparison.Ordinal))
            {
                return symbol;
            }
        }

        return upper.Contains("EURGBP", StringComparison.Ordinal) ? "EURGBP" : "UNKNOWN";
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public sealed class R009PaperLedgerPreviewArtifactWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public R009PaperLedgerPreviewArtifactWriter(string rootPath)
    {
        RootPath = Path.GetFullPath(rootPath);
        Contract = new R009PaperLedgerPreviewArtifactWriterContract(
            RootPath,
            ArtifactOnly: true,
            DbRequired: false,
            PaperLedgerTableWritesAllowed: false,
            OrderDomainPersistenceAllowed: false,
            RouteSubmissionPersistenceAllowed: false,
            FillReportPersistenceAllowed: false,
            TradingStateMutationAllowed: false);
    }

    public string RootPath { get; }

    public R009PaperLedgerPreviewArtifactWriterContract Contract { get; }

    public R009PaperLedgerPreviewPersistenceResult Persist(R009PaperLedgerPreviewArtifactEnvelope envelope)
    {
        var reasons = Validate(envelope).ToList();
        if (reasons.Count > 0)
        {
            return new R009PaperLedgerPreviewPersistenceResult("Rejected", null, false, false, false, null, envelope.AuditRecord.AuditHash, reasons);
        }

        Directory.CreateDirectory(RootPath);
        var path = Path.Combine(RootPath, $"{SanitizeFileName(envelope.Request.RequestId)}.paper-ledger-preview.json");
        var payload = JsonSerializer.Serialize(envelope, JsonOptions);

        if (File.Exists(path))
        {
            var existing = JsonSerializer.Deserialize<R009PaperLedgerPreviewArtifactEnvelope>(File.ReadAllText(path), JsonOptions);
            if (existing is not null && string.Equals(existing.Response.InputHash, envelope.Response.InputHash, StringComparison.Ordinal))
            {
                return new R009PaperLedgerPreviewPersistenceResult("ReplaySafe", path, false, true, false, existing.AuditRecord.AuditHash, envelope.AuditRecord.AuditHash, Array.Empty<string>());
            }

            return new R009PaperLedgerPreviewPersistenceResult("Conflict", path, false, false, true, existing?.AuditRecord.AuditHash, envelope.AuditRecord.AuditHash, new[] { "SameRequestIdDifferentInputHash" });
        }

        File.WriteAllText(path, payload);
        return new R009PaperLedgerPreviewPersistenceResult("Persisted", path, true, false, false, null, envelope.AuditRecord.AuditHash, Array.Empty<string>());
    }

    private IEnumerable<string> Validate(R009PaperLedgerPreviewArtifactEnvelope envelope)
    {
        if (!IsAllowedPreviewArtifactPath(RootPath)) yield return "PaperLedgerPreviewArtifactPathRequired";
        if (!envelope.ArtifactOnly || !envelope.NoDbPersistence) yield return "ArtifactOnlyNoDbRequired";
        if (!envelope.NoPaperLedgerTableWrites) yield return "PaperLedgerTableWritesForbidden";
        if (!envelope.NoOrderDomainPersistence) yield return "OrderDomainPersistenceForbidden";
        if (!envelope.NoRouteSubmissionPersistence) yield return "RouteSubmissionPersistenceForbidden";
        if (!envelope.NoFillReportPersistence) yield return "FillReportPersistenceForbidden";
        if (!envelope.NoTradingStateMutation) yield return "TradingStateMutationForbidden";
        if (envelope.Response.PaperLedgerCommit || envelope.Response.LedgerMutation || envelope.Response.TradingStateMutation) yield return "PreviewResponseMustNotCommitOrMutate";
        if (!envelope.Response.PreviewOnly || !envelope.Response.NonExecutable || !envelope.Response.NotAnOrder || !envelope.Response.NoBrokerRoute) yield return "PreviewResponseSafetyFlagsRequired";
        if (envelope.AuditRecord.PaperLedgerCommit || envelope.AuditRecord.LedgerMutation || envelope.AuditRecord.TradingStateMutation) yield return "AuditRecordMustNotCommitOrMutate";
    }

    private static bool IsAllowedPreviewArtifactPath(string path)
        => Path.GetFullPath(path).Replace('\\', '/').Contains("artifacts/readiness/execution-live/paper-ledger-preview", StringComparison.OrdinalIgnoreCase);

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(invalid.Contains(character) ? '-' : character);
        }

        return builder.ToString();
    }
}

public sealed class R009DisabledPreviewBatchService
{
    public const int DefaultMaxBatchSize = 250;

    private static readonly HashSet<string> SupportedExecutionSymbols = new(StringComparer.OrdinalIgnoreCase)
    {
        "EURUSD",
        "USDJPY",
        "AUDUSD",
        "GBPUSD",
        "NZDUSD",
        "USDCAD",
        "USDCHF"
    };

    private static readonly HashSet<string> ForbiddenOutputs = new(StringComparer.OrdinalIgnoreCase)
    {
        "Order",
        "ChildOrder",
        "Route",
        "Submission",
        "Fill",
        "ExecutionReport",
        "ExecutableSchedule"
    };

    private readonly R009PaperPlanExecutionIntentConverter _converter;
    private readonly R009DisabledPreviewContractService _previewService;

    public R009DisabledPreviewBatchService(
        R009PaperPlanExecutionIntentConverter? converter = null,
        R009DisabledPreviewContractService? previewService = null)
    {
        _converter = converter ?? new R009PaperPlanExecutionIntentConverter();
        _previewService = previewService ?? new R009DisabledPreviewContractService();
    }

    public R009DisabledPreviewBatchResponse PreviewBatch(
        R009DisabledPreviewBatchRequest request,
        DateTimeOffset? createdAtUtc = null)
    {
        var batchValidation = ValidateBatchRequest(request);
        var itemResults = new List<R009DisabledPreviewBatchItemResult>();

        if (batchValidation.IsValid)
        {
            foreach (var item in request.Items)
            {
                itemResults.Add(PreviewItem(request, item, createdAtUtc));
            }
        }

        var previewReadyCount = itemResults.Count(x => x.Status == "PreviewReady");
        var heldMissingReadinessCount = itemResults.Count(x => x.Status == "HeldMissingReadiness");
        var rejectedCount = batchValidation.IsValid ? itemResults.Count(x => x.Status == "Rejected") : request.Items.Count;
        var batchStatus = batchValidation.IsValid
            ? rejectedCount == 0 ? "PreviewBatchGenerated" : "PreviewBatchGeneratedWithRejectedItems"
            : "Rejected";
        var idempotencyHash = Hash(string.Join("|", request.BatchRequestId, request.Items.Count, request.R009ContractVersion, request.MaxBatchSize));
        var auditHash = Hash(string.Join("|", request.BatchRequestId, batchStatus, previewReadyCount, heldMissingReadinessCount, rejectedCount, string.Join(";", batchValidation.RejectionReasons)));

        return new R009DisabledPreviewBatchResponse(
            request.BatchRequestId,
            batchStatus,
            batchValidation,
            itemResults,
            previewReadyCount,
            heldMissingReadinessCount,
            rejectedCount,
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true,
            NoFill: true,
            NoExecutionReport: true,
            NoRoute: true,
            NoSubmission: true,
            NoPaperLedgerCommit: true,
            idempotencyHash,
            auditHash);
    }

    private R009DisabledPreviewBatchItemResult PreviewItem(
        R009DisabledPreviewBatchRequest batchRequest,
        R009DisabledPreviewBatchItem item,
        DateTimeOffset? createdAtUtc)
    {
        var itemRejections = new List<string>();
        var intent = ResolveIntent(item, itemRejections);
        if (intent is not null)
        {
            itemRejections.AddRange(ValidateIntentSchema(intent));
        }

        if (itemRejections.Count > 0 || intent is null)
        {
            return RejectedItem(batchRequest.BatchRequestId, item.ItemId, itemRejections);
        }

        var request = new R009DisabledPreviewRequest(
            RequestId: $"{batchRequest.BatchRequestId}:{item.ItemId}",
            RequestMode: R009DisabledPreviewRequestMode.DisabledPreviewOnly,
            SourceType: R009DisabledPreviewSourceType.ExecutionIntent,
            ExecutionIntent: intent,
            SourceArtifactPath: null,
            R009ContractVersion: batchRequest.R009ContractVersion,
            DryRunOnly: true,
            LiveTradingEnabled: false,
            BrokerRoutingEnabled: false,
            OrderSubmissionEnabled: false,
            ExecutableScheduleEnabled: false,
            PaperLedgerCommitEnabled: false,
            OperatorApprovalScope: batchRequest.OperatorApprovalScope,
            RiskApprovalScope: batchRequest.RiskApprovalScope,
            NoBrokerRoute: true,
            RequestedOutputs: batchRequest.RequestedOutputs);
        var response = _previewService.Preview(request, createdAtUtc: createdAtUtc);
        var decision = response.DecisionPreviews.SingleOrDefault();
        var status = decision?.LineStatus == R009LiveLineStatus.PreviewReady
            ? "PreviewReady"
            : decision?.LineStatus == R009LiveLineStatus.HeldMissingReadiness
                ? "HeldMissingReadiness"
                : "Rejected";
        var reasons = status == "Rejected"
            ? response.RejectionReasons.Concat(decision?.PreTradeRiskGate.Reasons ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray()
            : response.RejectionReasons;

        return new R009DisabledPreviewBatchItemResult(
            item.ItemId,
            status,
            reasons,
            response,
            Hash($"{batchRequest.BatchRequestId}|{item.ItemId}|{intent.ExecutionIntentId}"),
            Hash($"{batchRequest.BatchRequestId}|{item.ItemId}|{status}|{string.Join(";", reasons)}"));
    }

    private R009EmsOmsExecutionIntent? ResolveIntent(R009DisabledPreviewBatchItem item, List<string> rejections)
    {
        if (item.SourceType == R009DisabledPreviewSourceType.ExecutionIntent)
        {
            if (item.ExecutionIntent is null)
            {
                rejections.Add("ExecutionIntentRequired");
                return null;
            }

            return item.ExecutionIntent;
        }

        if (item.SourceType == R009DisabledPreviewSourceType.PaperPlanLineArtifact)
        {
            if (item.PaperPlanLine is null)
            {
                rejections.Add("PaperPlanLineRequired");
                return null;
            }

            return _converter.Convert(item.PaperPlanLine, item.ItemId);
        }

        rejections.Add("UnsupportedSourceType");
        return null;
    }

    private static R009DisabledPreviewBatchValidationResult ValidateBatchRequest(R009DisabledPreviewBatchRequest request)
    {
        var reasons = new List<string>();
        var maxBatchSize = request.MaxBatchSize <= 0 ? DefaultMaxBatchSize : request.MaxBatchSize;

        if (request.RequestMode != R009DisabledPreviewRequestMode.DisabledPreviewOnly)
        {
            reasons.Add("RequestModeMustBeDisabledPreviewOnly");
        }

        if (!string.Equals(request.R009ContractVersion, R009DisabledEmsOmsExecutionAdapter.ContractVersion, StringComparison.Ordinal))
        {
            reasons.Add("R009ContractVersionMismatch");
        }

        if (!request.DryRunOnly) reasons.Add("DryRunOnlyRequired");
        if (request.LiveTradingEnabled) reasons.Add("LiveTradingMustRemainDisabled");
        if (request.BrokerRoutingEnabled) reasons.Add("BrokerRoutingMustRemainDisabled");
        if (request.OrderSubmissionEnabled) reasons.Add("OrderSubmissionMustRemainDisabled");
        if (request.ExecutableScheduleEnabled) reasons.Add("ExecutableScheduleMustRemainDisabled");
        if (request.PaperLedgerCommitEnabled) reasons.Add("PaperLedgerCommitMustRemainDisabled");
        if (!request.NoBrokerRoute) reasons.Add("NoBrokerRouteRequired");
        if (!string.Equals(request.OperatorApprovalScope, "DesignOnlyPreviewOnly", StringComparison.Ordinal)) reasons.Add("OperatorApprovalScopeMustBeDesignOnlyPreviewOnly");
        if (!string.Equals(request.RiskApprovalScope, "DesignOnlyPreviewOnly", StringComparison.Ordinal)) reasons.Add("RiskApprovalScopeMustBeDesignOnlyPreviewOnly");
        if (request.Items.Count == 0) reasons.Add("BatchMustContainAtLeastOneItem");
        if (request.Items.Count > maxBatchSize) reasons.Add("MaxBatchSizeExceeded");

        foreach (var output in request.RequestedOutputs ?? Array.Empty<string>())
        {
            if (ForbiddenOutputs.Contains(output))
            {
                reasons.Add($"ForbiddenOutputRequested:{output}");
            }
        }

        return new R009DisabledPreviewBatchValidationResult(reasons.Count == 0, reasons, request.Items.Count, maxBatchSize);
    }

    private static IEnumerable<string> ValidateIntentSchema(R009EmsOmsExecutionIntent intent)
    {
        if (intent.LiveTradingEnabled) yield return "ExecutionIntentLiveTradingMustRemainDisabled";
        if (intent.BrokerRoutingEnabled) yield return "ExecutionIntentBrokerRoutingMustRemainDisabled";
        if (intent.OrderSubmissionEnabled) yield return "ExecutionIntentOrderSubmissionMustRemainDisabled";
        if (!intent.NonExecutable) yield return "ExecutionIntentMustBeNonExecutable";
        if (IsDirectCross(intent.ExecutionTradableSymbol) || IsDirectCross(intent.Symbol)) yield return "DirectCrossExecutionIntentRejected";
        if (!SupportedExecutionSymbols.Contains(intent.ExecutionTradableSymbol)) yield return "UnsupportedInstrumentRejected";
        if (!IsCanonicalQuarterHour(intent.CanonicalTargetCloseUtc, intent.CanonicalTargetCloseLocal)) yield return "CanonicalQuarterHourTargetCloseRequired";
        if (!HasValidInversionMetadata(intent)) yield return "InversionMetadataInvalid";
    }

    private static R009DisabledPreviewBatchItemResult RejectedItem(string batchRequestId, string itemId, IReadOnlyList<string> reasons)
        => new(
            itemId,
            "Rejected",
            reasons,
            PreviewResponse: null,
            Hash($"{batchRequestId}|{itemId}|Rejected"),
            Hash($"{batchRequestId}|{itemId}|Rejected|{string.Join(";", reasons)}"));

    private static bool HasValidInversionMetadata(R009EmsOmsExecutionIntent intent)
    {
        if (intent.ExecutionTradableSymbol.Equals("USDJPY", StringComparison.OrdinalIgnoreCase))
        {
            return intent.NormalizedPortfolioSymbol.Equals("JPYUSD", StringComparison.OrdinalIgnoreCase) &&
                intent.RequiresInversion &&
                string.Equals(intent.SecurityID, "4004", StringComparison.Ordinal) &&
                string.Equals(intent.SecurityIDSource, "8", StringComparison.Ordinal);
        }

        if (intent.ExecutionTradableSymbol.Equals("USDCAD", StringComparison.OrdinalIgnoreCase) ||
            intent.ExecutionTradableSymbol.Equals("USDCHF", StringComparison.OrdinalIgnoreCase))
        {
            return intent.RequiresInversion;
        }

        return !intent.RequiresInversion;
    }

    private static bool IsCanonicalQuarterHour(DateTimeOffset targetCloseUtc, string targetCloseLocal)
        => targetCloseUtc.Second == 0 &&
            targetCloseUtc.Millisecond == 0 &&
            targetCloseUtc.Minute is 0 or 15 or 30 or 45 &&
            !targetCloseLocal.Contains(":06:", StringComparison.Ordinal) &&
            !targetCloseLocal.Contains(":21:", StringComparison.Ordinal) &&
            !targetCloseLocal.Contains(":36:", StringComparison.Ordinal) &&
            !targetCloseLocal.Contains(":51:", StringComparison.Ordinal);

    private static bool IsDirectCross(string symbol)
        => symbol.Length == 6 &&
            !symbol.Contains("USD", StringComparison.OrdinalIgnoreCase);

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public sealed class R009PreviewConsumerBoundaryService
{
    private static readonly HashSet<R009PreviewConsumerType> AllowedConsumers = new()
    {
        R009PreviewConsumerType.InternalPmsPreviewConsumer,
        R009PreviewConsumerType.InternalEmsPreviewConsumer,
        R009PreviewConsumerType.InternalOmsPreviewConsumer,
        R009PreviewConsumerType.OperatorReviewTool,
        R009PreviewConsumerType.TestHarness
    };

    private readonly R009DisabledPreviewContractService _singlePreviewService;
    private readonly R009DisabledPreviewBatchService _batchPreviewService;

    public R009PreviewConsumerBoundaryService(
        R009DisabledPreviewContractService? singlePreviewService = null,
        R009DisabledPreviewBatchService? batchPreviewService = null)
    {
        _singlePreviewService = singlePreviewService ?? new R009DisabledPreviewContractService();
        _batchPreviewService = batchPreviewService ?? new R009DisabledPreviewBatchService();
    }

    public R009PreviewConsumerResponseEnvelope RequestSinglePreview(
        R009PreviewConsumerRequestEnvelope envelope,
        IEnumerable<R009PaperPlanPreviewLine>? artifactPaperPlanLines = null,
        DateTimeOffset? createdAtUtc = null)
    {
        var preflight = ValidateConsumerAndUsage(envelope);
        R009DisabledPreviewResponse? response = null;
        var reasons = preflight.ToList();

        if (envelope.SinglePreviewRequest is null)
        {
            reasons.Add("SinglePreviewRequestRequired");
        }

        if (reasons.Count == 0)
        {
            response = _singlePreviewService.Preview(envelope.SinglePreviewRequest!, artifactPaperPlanLines, createdAtUtc);
            reasons.AddRange(ValidateSingleResponseSafety(response));
        }

        return BuildEnvelope(envelope, response, batchResponse: null, reasons, createdAtUtc);
    }

    public R009PreviewConsumerResponseEnvelope RequestBatchPreview(
        R009PreviewConsumerRequestEnvelope envelope,
        DateTimeOffset? createdAtUtc = null)
    {
        var preflight = ValidateConsumerAndUsage(envelope);
        R009DisabledPreviewBatchResponse? response = null;
        var reasons = preflight.ToList();

        if (envelope.BatchPreviewRequest is null)
        {
            reasons.Add("BatchPreviewRequestRequired");
        }

        if (reasons.Count == 0)
        {
            response = _batchPreviewService.PreviewBatch(envelope.BatchPreviewRequest!, createdAtUtc);
            reasons.AddRange(ValidateBatchResponseSafety(response));
        }

        return BuildEnvelope(envelope, singleResponse: null, response, reasons, createdAtUtc);
    }

    public static bool IsAllowedConsumer(R009PreviewConsumerType consumerType)
        => AllowedConsumers.Contains(consumerType);

    private static IEnumerable<string> ValidateConsumerAndUsage(R009PreviewConsumerRequestEnvelope envelope)
    {
        if (!AllowedConsumers.Contains(envelope.ConsumerType))
        {
            yield return $"ForbiddenConsumer:{envelope.ConsumerType}";
        }

        foreach (var usage in envelope.RequestedUsages)
        {
            if (!R009PreviewUsagePolicy.Default.AllowedUsages.Contains(usage, StringComparer.Ordinal))
            {
                yield return $"ForbiddenUsage:{usage}";
            }
        }
    }

    private static IEnumerable<string> ValidateSingleResponseSafety(R009DisabledPreviewResponse response)
    {
        if (!response.NonExecutable) yield return "ResponseMustBeNonExecutable";
        if (!response.NotAnOrder) yield return "ResponseMustBeNotAnOrder";
        if (!response.NotSubmitted) yield return "ResponseMustBeNotSubmitted";
        if (!response.NoBrokerRoute) yield return "ResponseMustHaveNoBrokerRoute";
        if (!response.NoRoute) yield return "ResponseMustHaveNoRoute";
        if (!response.NoSubmission) yield return "ResponseMustHaveNoSubmission";
        if (!response.NoFill) yield return "ResponseMustHaveNoFill";
        if (!response.NoExecutionReport) yield return "ResponseMustHaveNoExecutionReport";
        if (!response.NoPaperLedgerCommit) yield return "ResponseMustHaveNoPaperLedgerCommit";

        foreach (var decision in response.DecisionPreviews)
        {
            foreach (var reason in ValidateDecisionSafety(decision))
            {
                yield return reason;
            }
        }
    }

    private static IEnumerable<string> ValidateBatchResponseSafety(R009DisabledPreviewBatchResponse response)
    {
        if (!response.NonExecutable) yield return "BatchResponseMustBeNonExecutable";
        if (!response.NotAnOrder) yield return "BatchResponseMustBeNotAnOrder";
        if (!response.NotSubmitted) yield return "BatchResponseMustBeNotSubmitted";
        if (!response.NoBrokerRoute) yield return "BatchResponseMustHaveNoBrokerRoute";
        if (!response.NoRoute) yield return "BatchResponseMustHaveNoRoute";
        if (!response.NoSubmission) yield return "BatchResponseMustHaveNoSubmission";
        if (!response.NoFill) yield return "BatchResponseMustHaveNoFill";
        if (!response.NoExecutionReport) yield return "BatchResponseMustHaveNoExecutionReport";
        if (!response.NoPaperLedgerCommit) yield return "BatchResponseMustHaveNoPaperLedgerCommit";

        foreach (var item in response.ItemResults)
        {
            if (item.PreviewResponse is null)
            {
                continue;
            }

            foreach (var reason in ValidateSingleResponseSafety(item.PreviewResponse))
            {
                yield return reason;
            }
        }
    }

    private static IEnumerable<string> ValidateDecisionSafety(R009DisabledExecutionDecision decision)
    {
        if (decision.CreatesOrder) yield return "DecisionCreatesOrder";
        if (decision.CreatesChildOrder) yield return "DecisionCreatesChildOrder";
        if (decision.CreatesRoute) yield return "DecisionCreatesRoute";
        if (decision.CreatesSubmission) yield return "DecisionCreatesSubmission";
        if (decision.CreatesFill) yield return "DecisionCreatesFill";
        if (decision.CreatesExecutionReport) yield return "DecisionCreatesExecutionReport";
        if (decision.CreatesExecutableSchedule) yield return "DecisionCreatesExecutableSchedule";
        if (!decision.NoPaperLedgerCommit) yield return "DecisionAllowsPaperLedgerCommit";
    }

    private static R009PreviewConsumerResponseEnvelope BuildEnvelope(
        R009PreviewConsumerRequestEnvelope request,
        R009DisabledPreviewResponse? singleResponse,
        R009DisabledPreviewBatchResponse? batchResponse,
        IReadOnlyList<string> reasons,
        DateTimeOffset? createdAtUtc)
    {
        var accepted = reasons.Count == 0;
        var guard = R009PreviewBoundaryGuard.Required;
        var guardResult = new R009PreviewBoundaryGuardResult(
            ConsumerAllowed: AllowedConsumers.Contains(request.ConsumerType),
            UsageAllowed: !request.RequestedUsages.Except(R009PreviewUsagePolicy.Default.AllowedUsages, StringComparer.Ordinal).Any(),
            ResponseSafe: accepted,
            Accepted: accepted,
            RejectionReasons: reasons);
        var now = createdAtUtc ?? DateTimeOffset.UtcNow;
        var auditHash = Hash(string.Join("|", request.ConsumerRequestId, request.ConsumerType, accepted, string.Join(";", reasons)));
        var audit = new R009PreviewConsumerAuditRecord(
            request.ConsumerRequestId,
            request.ConsumerType,
            auditHash,
            now,
            NoOrderDomainOutput: true,
            NoBrokerRoute: true,
            NoStateMutation: true,
            DryRunOnly: true);

        return new R009PreviewConsumerResponseEnvelope(
            request.ConsumerRequestId,
            request.ConsumerType,
            request,
            accepted,
            reasons,
            singleResponse,
            batchResponse,
            R009PreviewUsagePolicy.Default,
            guard,
            guardResult,
            audit,
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true,
            NoRoute: true,
            NoSubmission: true,
            NoFill: true,
            NoExecutionReport: true,
            NoPaperLedgerCommit: true,
            NoStateMutation: true);
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public sealed class R009PreviewArtifactAuditWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public R009PreviewArtifactAuditWriter(string auditRootPath)
    {
        AuditRootPath = Path.GetFullPath(auditRootPath);
        StoreContract = new R009PreviewAuditStoreContract(
            StoreName: "R009PreviewArtifactAuditStore",
            RootPath: AuditRootPath,
            ArtifactOnly: true,
            DbRequired: false,
            ExternalServiceRequired: false,
            OrderDomainPersistenceAllowed: false,
            RouteSubmissionPersistenceAllowed: false,
            LedgerPersistenceAllowed: false,
            TradingStateMutationAllowed: false);
    }

    public string AuditRootPath { get; }

    public R009PreviewAuditStoreContract StoreContract { get; }

    public R009PreviewAuditPersistenceResult Persist(R009PreviewConsumerResponseEnvelope envelope)
    {
        var safetyReasons = ValidateEnvelope(envelope).ToList();
        if (safetyReasons.Count > 0)
        {
            return new R009PreviewAuditPersistenceResult(
                Status: "Rejected",
                ArtifactPath: null,
                Persisted: false,
                ReplaySafe: false,
                Conflict: false,
                ExistingAuditHash: null,
                AuditHash: null,
                Reasons: safetyReasons);
        }

        Directory.CreateDirectory(AuditRootPath);
        var auditEnvelope = CreateAuditEnvelope(envelope);
        var path = Path.Combine(AuditRootPath, $"{SanitizeFileName(auditEnvelope.RequestId)}.preview-audit.json");
        var payload = JsonSerializer.Serialize(auditEnvelope, JsonOptions);

        if (File.Exists(path))
        {
            var existing = JsonSerializer.Deserialize<R009PreviewAuditEnvelope>(File.ReadAllText(path), JsonOptions);
            if (existing is not null && string.Equals(existing.RequestAudit.InputHash, auditEnvelope.RequestAudit.InputHash, StringComparison.Ordinal))
            {
                return new R009PreviewAuditPersistenceResult(
                    Status: "ReplaySafe",
                    ArtifactPath: path,
                    Persisted: false,
                    ReplaySafe: true,
                    Conflict: false,
                    ExistingAuditHash: existing.AuditHash,
                    AuditHash: auditEnvelope.AuditHash,
                    Reasons: Array.Empty<string>());
            }

            return new R009PreviewAuditPersistenceResult(
                Status: "Conflict",
                ArtifactPath: path,
                Persisted: false,
                ReplaySafe: false,
                Conflict: true,
                ExistingAuditHash: existing?.AuditHash,
                AuditHash: auditEnvelope.AuditHash,
                Reasons: new[] { "SameRequestIdDifferentInputHash" });
        }

        File.WriteAllText(path, payload);
        return new R009PreviewAuditPersistenceResult(
            Status: "Persisted",
            ArtifactPath: path,
            Persisted: true,
            ReplaySafe: false,
            Conflict: false,
            ExistingAuditHash: null,
            AuditHash: auditEnvelope.AuditHash,
            Reasons: Array.Empty<string>());
    }

    private IEnumerable<string> ValidateEnvelope(R009PreviewConsumerResponseEnvelope envelope)
    {
        if (!IsArtifactAuditPath(AuditRootPath))
        {
            yield return "AuditPathMustBeArtifactsReadinessExecutionLiveAudit";
        }

        if (!envelope.Accepted && envelope.RejectionReasons.Any(x => x.StartsWith("ForbiddenConsumer:", StringComparison.Ordinal)))
        {
            yield return "ForbiddenConsumerCannotPersistAudit";
        }

        if (!envelope.Accepted && envelope.RejectionReasons.Any(x =>
            x.Contains("LiveTrading", StringComparison.Ordinal) ||
            x.Contains("BrokerRouting", StringComparison.Ordinal) ||
            x.Contains("OrderSubmission", StringComparison.Ordinal) ||
            x.Contains("ExecutableSchedule", StringComparison.Ordinal) ||
            x.Contains("PaperLedgerCommit", StringComparison.Ordinal)))
        {
            yield return "UnsafeRequestCannotPersistAudit";
        }

        if (envelope.OriginalRequest.SinglePreviewRequest is not null && RequestEnablesTradingPath(envelope.OriginalRequest.SinglePreviewRequest))
        {
            yield return "UnsafeRequestCannotPersistAudit";
        }

        if (envelope.OriginalRequest.BatchPreviewRequest is not null && RequestEnablesTradingPath(envelope.OriginalRequest.BatchPreviewRequest))
        {
            yield return "UnsafeRequestCannotPersistAudit";
        }

        if (!envelope.NonExecutable || !envelope.NotAnOrder || !envelope.NoBrokerRoute || !envelope.NoPaperLedgerCommit || !envelope.NoStateMutation)
        {
            yield return "EnvelopeSafetyFlagsRequired";
        }

        if (envelope.SinglePreviewResponse is not null && (!envelope.SinglePreviewResponse.NonExecutable ||
            !envelope.SinglePreviewResponse.NotAnOrder ||
            !envelope.SinglePreviewResponse.NoBrokerRoute ||
            !envelope.SinglePreviewResponse.NoPaperLedgerCommit))
        {
            yield return "SinglePreviewResponseSafetyFlagsRequired";
        }

        if (envelope.BatchPreviewResponse is not null && (!envelope.BatchPreviewResponse.NonExecutable ||
            !envelope.BatchPreviewResponse.NotAnOrder ||
            !envelope.BatchPreviewResponse.NoBrokerRoute ||
            !envelope.BatchPreviewResponse.NoPaperLedgerCommit))
        {
            yield return "BatchPreviewResponseSafetyFlagsRequired";
        }
    }

    private static bool RequestEnablesTradingPath(R009DisabledPreviewRequest request)
        => !request.DryRunOnly ||
           request.LiveTradingEnabled ||
           request.BrokerRoutingEnabled ||
           request.OrderSubmissionEnabled ||
           request.ExecutableScheduleEnabled ||
           request.PaperLedgerCommitEnabled ||
           !request.NoBrokerRoute;

    private static bool RequestEnablesTradingPath(R009DisabledPreviewBatchRequest request)
        => !request.DryRunOnly ||
           request.LiveTradingEnabled ||
           request.BrokerRoutingEnabled ||
           request.OrderSubmissionEnabled ||
           request.ExecutableScheduleEnabled ||
           request.PaperLedgerCommitEnabled ||
           !request.NoBrokerRoute;

    private static R009PreviewAuditEnvelope CreateAuditEnvelope(R009PreviewConsumerResponseEnvelope envelope)
    {
        var requestId = envelope.ConsumerRequestId;
        var batchRequestId = envelope.BatchPreviewResponse?.BatchRequestId;
        var decisionPreviewId = envelope.SinglePreviewResponse?.DecisionPreviewId ?? envelope.BatchPreviewResponse?.BatchRequestId;
        var inputHash = Hash(string.Join("|", requestId, envelope.ConsumerType, batchRequestId, envelope.SinglePreviewResponse?.IdempotencyHash, envelope.BatchPreviewResponse?.IdempotencyHash));
        var decisionHash = envelope.SinglePreviewResponse?.AuditHash ?? envelope.BatchPreviewResponse?.AuditHash;
        var createdAtUtc = envelope.AuditRecord.CreatedAtUtc;
        var requestAuditHash = Hash(string.Join("|", requestId, batchRequestId, envelope.ConsumerType, inputHash, "request"));
        var responseAuditHash = Hash(string.Join("|", requestId, batchRequestId, envelope.ConsumerType, decisionHash, envelope.Accepted, "response"));
        var requestAudit = new R009PreviewRequestAuditRecord(
            requestId,
            batchRequestId,
            decisionPreviewId,
            envelope.ConsumerType,
            R009DisabledPreviewRequestMode.DisabledPreviewOnly,
            R009DisabledEmsOmsExecutionAdapter.ContractVersion,
            inputHash,
            decisionHash,
            requestAuditHash,
            createdAtUtc,
            DryRunOnly: true,
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true,
            NoOrderDomainPersistence: true,
            NoTradingStateMutation: true,
            NoPaperLedgerCommit: true,
            RetentionCategory: "PreviewAuditOnly");
        var responseAudit = envelope.SinglePreviewResponse is null ? null : new R009PreviewResponseAuditRecord(
            requestId,
            batchRequestId,
            envelope.SinglePreviewResponse.DecisionPreviewId,
            envelope.ConsumerType,
            R009DisabledPreviewRequestMode.DisabledPreviewOnly,
            R009DisabledEmsOmsExecutionAdapter.ContractVersion,
            inputHash,
            envelope.SinglePreviewResponse.AuditHash,
            responseAuditHash,
            createdAtUtc,
            envelope.SinglePreviewResponse.DecisionStatus,
            envelope.SinglePreviewResponse.Accepted,
            DryRunOnly: true,
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true,
            NoOrderDomainPersistence: true,
            NoTradingStateMutation: true,
            NoPaperLedgerCommit: true,
            RetentionCategory: "PreviewAuditOnly");
        var batchAudit = envelope.BatchPreviewResponse is null ? null : new R009PreviewBatchAuditRecord(
            requestId,
            envelope.BatchPreviewResponse.BatchRequestId,
            decisionPreviewId,
            envelope.ConsumerType,
            R009DisabledPreviewRequestMode.DisabledPreviewOnly,
            R009DisabledEmsOmsExecutionAdapter.ContractVersion,
            inputHash,
            envelope.BatchPreviewResponse.AuditHash,
            responseAuditHash,
            createdAtUtc,
            envelope.BatchPreviewResponse.BatchStatus,
            envelope.BatchPreviewResponse.ItemResults.Count,
            envelope.BatchPreviewResponse.PreviewReadyCount,
            envelope.BatchPreviewResponse.HeldMissingReadinessCount,
            envelope.BatchPreviewResponse.RejectedCount,
            DryRunOnly: true,
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true,
            NoOrderDomainPersistence: true,
            NoTradingStateMutation: true,
            NoPaperLedgerCommit: true,
            RetentionCategory: "PreviewAuditOnly");
        var envelopeHash = Hash(string.Join("|", requestAuditHash, responseAudit?.AuditHash, batchAudit?.AuditHash));

        return new R009PreviewAuditEnvelope(
            AuditEnvelopeId: $"{requestId}:preview-audit",
            requestId,
            batchRequestId,
            requestAudit,
            responseAudit,
            batchAudit,
            envelopeHash,
            ArtifactOnly: true,
            NoDbPersistence: true,
            NoOrderDomainPersistence: true,
            NoRouteSubmissionPersistence: true,
            NoLedgerPersistence: true,
            NoTradingStateMutation: true);
    }

    private static bool IsArtifactAuditPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("artifacts/readiness/execution-live/audit", StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(invalid.Contains(ch) ? '_' : ch);
        }

        return builder.ToString();
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public sealed class R009DisabledEmsOmsExecutionAdapter
{
    public const string ContractVersion = "0.3.0-design-only-candidate";
    public const string PrimaryPolicyCandidate = "CloseSeeking15mAdaptive_BalancedAdaptive_v0";
    public const string SecondaryPolicyCandidate = "CloseSeeking15mAdaptive_ResidualAwareUrgency_v0";
    public const string ConditionalResidualModule = "ControlledResidualCross_BalancedResidualCross_v0";

    private static readonly HashSet<string> SupportedExecutionSymbols = new(StringComparer.OrdinalIgnoreCase)
    {
        "EURUSD",
        "USDJPY",
        "AUDUSD",
        "GBPUSD",
        "NZDUSD",
        "USDCAD",
        "USDCHF"
    };

    public R009DisabledExecutionDecision Decide(
        R009EmsOmsExecutionIntent intent,
        R009LiveFeatureFlags? featureFlags = null,
        R009DisabledBoundaryGuard? boundaryGuard = null,
        DateTimeOffset? createdAtUtc = null)
    {
        featureFlags ??= R009LiveFeatureFlags.DisabledDefaults;
        boundaryGuard ??= R009DisabledBoundaryGuard.Disabled;
        var now = createdAtUtc ?? DateTimeOffset.UtcNow;
        var reasons = new List<string>();

        var boundarySafe =
            !featureFlags.R009LiveTradingEnabled &&
            !featureFlags.R009BrokerRoutingEnabled &&
            !featureFlags.R009OrderSubmissionEnabled &&
            !featureFlags.R009ExecutableScheduleEnabled &&
            !featureFlags.R009PaperLedgerCommitEnabled &&
            !featureFlags.R009SchedulerEnabled &&
            !featureFlags.R009BackgroundWorkerEnabled &&
            featureFlags.R009DryRunOnly &&
            !boundaryGuard.BrokerRouteCreationAllowed &&
            !boundaryGuard.OrderCreationAllowed &&
            !boundaryGuard.ChildSliceCreationAllowed &&
            !boundaryGuard.ChildOrderCreationAllowed &&
            !boundaryGuard.ScheduleExecutionAllowed &&
            !boundaryGuard.SubmissionAllowed &&
            !boundaryGuard.FillCreationAllowed &&
            !boundaryGuard.ExecutionReportCreationAllowed &&
            !boundaryGuard.StateMutationAllowed &&
            !boundaryGuard.PaperLedgerCommitAllowed &&
            !intent.LiveTradingEnabled &&
            !intent.BrokerRoutingEnabled &&
            !intent.OrderSubmissionEnabled &&
            intent.NonExecutable;
        if (!boundarySafe)
        {
            reasons.Add("DisabledBoundaryGuardFailed");
        }

        var supportedSymbol = SupportedExecutionSymbols.Contains(intent.ExecutionTradableSymbol);
        if (!supportedSymbol)
        {
            reasons.Add(IsDirectCross(intent.ExecutionTradableSymbol) || IsDirectCross(intent.Symbol)
                ? "DirectCrossExecutionDisabled"
                : "UnsupportedInstrument");
        }

        var directCrossExcluded = !IsDirectCross(intent.ExecutionTradableSymbol) && !IsDirectCross(intent.Symbol);
        if (!directCrossExcluded)
        {
            reasons.Add("DirectCrossMustBeNettedBeforeExecutionIntent");
        }

        var inversionValid = HasValidInversionMetadata(intent);
        if (!inversionValid)
        {
            reasons.Add("InversionMetadataInvalid");
        }

        var canonical = intent.CanonicalTargetCloseUtc.Second == 0 &&
            intent.CanonicalTargetCloseUtc.Millisecond == 0 &&
            IsQuarterHour(intent.CanonicalTargetCloseUtc.Minute) &&
            !HasLegacyFutureCanonicalMinute(intent.CanonicalTargetCloseUtc.Minute) &&
            !HasLegacyLocalMinute(intent.CanonicalTargetCloseLocal);
        if (!canonical)
        {
            reasons.Add("CanonicalTargetCloseMustBeQuarterHour");
        }

        var quoteReady = !string.IsNullOrWhiteSpace(intent.QuoteWindowReadinessId);
        var closeReady = !string.IsNullOrWhiteSpace(intent.CloseBenchmarkReadinessId);
        var feedReady = !string.IsNullOrWhiteSpace(intent.FeedQualityReadinessId);
        if (!quoteReady) reasons.Add("MissingQuoteWindowReadiness");
        if (!closeReady) reasons.Add("MissingCloseBenchmarkReadiness");
        if (!feedReady) reasons.Add("MissingFeedQualityReadiness");

        var riskApproved = intent.RiskApprovalStatus == R009ApprovalStatus.ApprovedForDesignOnlyPreviewOnly;
        var operatorApproved = intent.OperatorApprovalStatus == R009ApprovalStatus.ApprovedForDesignOnlyPreviewOnly;
        if (!riskApproved) reasons.Add("MissingRiskApproval");
        if (!operatorApproved) reasons.Add("MissingOperatorApproval");
        if (intent.OvernightAllowed) reasons.Add("OvernightNotAllowed");
        if (!intent.MustEndFlat) reasons.Add("MustEndFlatRequired");
        var spreadGuard = intent.ExpectedSpreadCostBps <= intent.MaxSpreadCostBps;
        if (!spreadGuard) reasons.Add("SpreadCostGuardFailed");
        if (!string.Equals(intent.R009ContractVersion, ContractVersion, StringComparison.Ordinal))
        {
            reasons.Add("R009ContractVersionMismatch");
        }

        var residualCondition = intent.ResidualNotional > 0m &&
            intent.ResidualOpportunityCostBps > intent.ExpectedSpreadCostBps &&
            spreadGuard;

        var gate = new R009PreTradeRiskGateResult(
            SupportedSymbol: supportedSymbol,
            UsdPairOnly: supportedSymbol && directCrossExcluded,
            DirectCrossExcluded: directCrossExcluded,
            InversionMetadataValid: inversionValid,
            CanonicalTargetClose: canonical,
            QuarterHourTargetClose: canonical,
            QuoteWindowReadinessPresent: quoteReady,
            CloseBenchmarkReadinessPresent: closeReady,
            FeedQualityReadinessPresent: feedReady,
            RiskApprovalPresent: riskApproved,
            OperatorApprovalPresent: operatorApproved,
            OvernightDisallowed: !intent.OvernightAllowed,
            MustEndFlat: intent.MustEndFlat,
            SpreadCostGuardPassed: spreadGuard,
            ControlledResidualCrossConditionPassed: residualCondition,
            KillSwitchSafe: boundarySafe,
            Passed: boundarySafe && supportedSymbol && directCrossExcluded && inversionValid && canonical && quoteReady && closeReady && feedReady && riskApproved && operatorApproved && !intent.OvernightAllowed && intent.MustEndFlat && spreadGuard,
            Reasons: reasons);

        var lineStatus = ResolveLineStatus(intent, gate);
        var holdReason = lineStatus == R009LiveLineStatus.PreviewReady ? null : string.Join(";", reasons.Distinct(StringComparer.Ordinal));
        var outputs = BuildOutputs(lineStatus, holdReason);
        var controlledResidualSelected = gate.Passed && residualCondition;
        var decisionId = $"{intent.ExecutionIntentId}:r009-disabled-decision";
        var inputHash = Hash(string.Join("|", intent.ExecutionIntentId, intent.SourcePmsCycleId, intent.SourceQubesRunId, intent.ExecutionTradableSymbol, intent.CanonicalTargetCloseUtc.ToString("O"), intent.TargetQuantity, intent.TargetNotional));
        var decisionHash = Hash(string.Join("|", decisionId, lineStatus, holdReason, PrimaryPolicyCandidate, SecondaryPolicyCandidate, ConditionalResidualModule));

        return new R009DisabledExecutionDecision(
            decisionId,
            intent.ExecutionIntentId,
            lineStatus,
            outputs,
            PrimaryPolicyCandidate,
            SecondaryPolicyCandidate,
            ConditionalResidualModule,
            gate,
            controlledResidualSelected ? "Controlled residual cross condition is preview-eligible but remains disabled and non-executable." : "Residual completion remains preview-only or not selected.",
            $"ExpectedSpreadCostBps={intent.ExpectedSpreadCostBps}; MaxSpreadCostBps={intent.MaxSpreadCostBps}; DryRunOnly=true",
            lineStatus == R009LiveLineStatus.PreviewReady ? null : "Manual review required before any future paper-only retry.",
            holdReason,
            controlledResidualSelected,
            ControlledResidualCrossAlwaysMarketAtClose: false,
            DesignOnly: true,
            PaperOnly: true,
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true,
            NoChildSlices: true,
            NoChildOrders: true,
            NoExecutableSchedule: true,
            NoFill: true,
            NoExecutionReport: true,
            NoRoute: true,
            NoSubmission: true,
            NoPaperLedgerCommit: true,
            CreatesOrder: false,
            CreatesChildOrder: false,
            CreatesRoute: false,
            CreatesSubmission: false,
            CreatesFill: false,
            CreatesExecutionReport: false,
            CreatesExecutableSchedule: false,
            new R009IdempotencyAuditEnvelope(
                intent.ExecutionIntentId,
                decisionId,
                decisionHash,
                inputHash,
                ContractVersion,
                now,
                NoOrderDomainOutput: true,
                NoBrokerRoute: true,
                DryRunOnly: true));
    }

    public static IReadOnlyList<string> SupportedSymbols => SupportedExecutionSymbols.OrderBy(x => x, StringComparer.Ordinal).ToArray();

    private static R009LiveLineStatus ResolveLineStatus(R009EmsOmsExecutionIntent intent, R009PreTradeRiskGateResult gate)
    {
        if (!gate.KillSwitchSafe)
        {
            return R009LiveLineStatus.InconclusiveSafe;
        }

        if (!gate.SupportedSymbol)
        {
            return IsDirectCross(intent.ExecutionTradableSymbol) || IsDirectCross(intent.Symbol)
                ? R009LiveLineStatus.HeldDirectCrossNotNetted
                : R009LiveLineStatus.HeldUnsupportedInstrument;
        }

        if (!gate.DirectCrossExcluded)
        {
            return R009LiveLineStatus.HeldDirectCrossNotNetted;
        }

        if (!gate.InversionMetadataValid)
        {
            return R009LiveLineStatus.HeldInversionMismatch;
        }

        if (!gate.QuoteWindowReadinessPresent || !gate.CloseBenchmarkReadinessPresent || !gate.FeedQualityReadinessPresent)
        {
            return R009LiveLineStatus.HeldMissingReadiness;
        }

        if (!gate.RiskApprovalPresent || !gate.OperatorApprovalPresent)
        {
            return R009LiveLineStatus.HeldRiskOperatorMissing;
        }

        return gate.Passed ? R009LiveLineStatus.PreviewReady : R009LiveLineStatus.InconclusiveSafe;
    }

    private static IReadOnlyList<R009DisabledDecisionOutput> BuildOutputs(R009LiveLineStatus status, string? holdReason)
    {
        var outputs = new List<R009DisabledDecisionOutput>
        {
            R009DisabledDecisionOutput.DesignOnlyExecutionDecision,
            R009DisabledDecisionOutput.ExecutionPlanPreview,
            R009DisabledDecisionOutput.ScheduleIntentPreview,
            R009DisabledDecisionOutput.ResidualRiskAssessment,
            R009DisabledDecisionOutput.CostTradeoffAssessment
        };

        if (status != R009LiveLineStatus.PreviewReady)
        {
            outputs.Add(R009DisabledDecisionOutput.ManualReviewRecommendation);
        }

        if (!string.IsNullOrWhiteSpace(holdReason))
        {
            outputs.Add(R009DisabledDecisionOutput.HoldReason);
        }

        return outputs;
    }

    private static bool HasValidInversionMetadata(R009EmsOmsExecutionIntent intent)
    {
        if (intent.ExecutionTradableSymbol.Equals("USDJPY", StringComparison.OrdinalIgnoreCase))
        {
            return intent.NormalizedPortfolioSymbol.Equals("JPYUSD", StringComparison.OrdinalIgnoreCase) &&
                intent.RequiresInversion &&
                string.Equals(intent.SecurityID, "4004", StringComparison.Ordinal) &&
                string.Equals(intent.SecurityIDSource, "8", StringComparison.Ordinal);
        }

        if (intent.ExecutionTradableSymbol.Equals("USDCAD", StringComparison.OrdinalIgnoreCase) ||
            intent.ExecutionTradableSymbol.Equals("USDCHF", StringComparison.OrdinalIgnoreCase))
        {
            return intent.RequiresInversion;
        }

        return !intent.RequiresInversion;
    }

    private static bool IsDirectCross(string symbol)
        => symbol.Length == 6 &&
            !symbol.Contains("USD", StringComparison.OrdinalIgnoreCase);

    private static bool IsQuarterHour(int minute)
        => minute is 0 or 15 or 30 or 45;

    private static bool HasLegacyFutureCanonicalMinute(int minute)
        => minute is 6 or 21 or 36 or 51;

    private static bool HasLegacyLocalMinute(string localTimestamp)
        => localTimestamp.Contains(":06:", StringComparison.Ordinal) ||
            localTimestamp.Contains(":21:", StringComparison.Ordinal) ||
            localTimestamp.Contains(":36:", StringComparison.Ordinal) ||
            localTimestamp.Contains(":51:", StringComparison.Ordinal);

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
