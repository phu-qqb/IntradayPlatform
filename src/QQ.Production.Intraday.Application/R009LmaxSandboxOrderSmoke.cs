namespace QQ.Production.Intraday.Application;

public enum R009SandboxOrderPathStatus
{
    Ready,
    Blocked
}

public enum R009SandboxSubmissionStatus
{
    NotSubmittedBlocked,
    Submitted,
    Acknowledged,
    Rejected
}

public sealed record R009LmaxSandboxConfigDiscovery(
    R009SandboxOrderPathStatus Status,
    string Environment,
    string BrokerVenue,
    bool SandboxConfigPresent,
    bool EnvironmentIsSandbox,
    bool BrokerVenueIsLmaxSandbox,
    bool ProductionVenueAllowed,
    bool ProductionCredentialsAllowed,
    bool SandboxCredentialsRequired,
    bool SandboxCredentialProfilePresent,
    bool ProductionCredentialsDetected,
    bool AllowSandboxOrderSubmission,
    bool SchedulerServicePollingBackgroundJobEnabled,
    int MaxSandboxOrderCount,
    decimal MaxSandboxNotional,
    IReadOnlyList<string> Reasons)
{
    public bool ReadyForSandboxSubmission =>
        Status == R009SandboxOrderPathStatus.Ready &&
        SandboxConfigPresent &&
        EnvironmentIsSandbox &&
        BrokerVenueIsLmaxSandbox &&
        !ProductionVenueAllowed &&
        !ProductionCredentialsAllowed &&
        SandboxCredentialsRequired &&
        SandboxCredentialProfilePresent &&
        !ProductionCredentialsDetected &&
        AllowSandboxOrderSubmission &&
        !SchedulerServicePollingBackgroundJobEnabled &&
        MaxSandboxOrderCount is > 0 and <= 3 &&
        MaxSandboxNotional > 0m;
}

public sealed record R009LmaxSandboxConfigContract(
    string Environment,
    string BrokerVenue,
    bool ProductionVenueAllowed,
    bool ProductionCredentialsAllowed,
    bool SandboxCredentialsRequired,
    bool SandboxOrderSubmissionEnabled,
    bool SandboxKillSwitchOpen,
    int MaxSandboxOrderCount,
    decimal MaxSandboxNotional,
    IReadOnlyList<string> AllowedSymbols,
    bool DirectCrossExecutionAllowed,
    bool NonmajorExecutionAllowed,
    bool PaperLedgerCommitAllowed,
    bool ProductionLedgerCommitAllowed,
    bool StateMutationAllowed)
{
    public static R009LmaxSandboxConfigContract Missing { get; } = new(
        Environment: "",
        BrokerVenue: "",
        ProductionVenueAllowed: false,
        ProductionCredentialsAllowed: false,
        SandboxCredentialsRequired: true,
        SandboxOrderSubmissionEnabled: false,
        SandboxKillSwitchOpen: false,
        MaxSandboxOrderCount: 0,
        MaxSandboxNotional: 0m,
        AllowedSymbols: R009LmaxSandboxOrderPathSmokeGate.WhitelistedSymbols,
        DirectCrossExecutionAllowed: false,
        NonmajorExecutionAllowed: false,
        PaperLedgerCommitAllowed: false,
        ProductionLedgerCommitAllowed: false,
        StateMutationAllowed: false);
}

public sealed record R009SandboxCredentialProfileValidation(
    R009SandboxOrderPathStatus Status,
    string CredentialProfileName,
    string CredentialSourceType,
    bool CredentialValuesRedacted,
    bool ProductionCredentialDetected,
    bool SandboxCredentialPresent,
    IReadOnlyDictionary<string, bool> CredentialVariablePresence,
    IReadOnlyList<string> MissingProfileNames,
    IReadOnlyList<string> Reasons);

public sealed record R009OperatorSandboxProfileAttestation(
    bool CurrentLmaxSetupOperatorAttestedSandbox,
    bool ExistingLmaxProfileIsSandbox,
    string SandboxClassificationSource,
    string BrokerVenue,
    bool EndpointValuesRedacted,
    bool ProductionEndpointDetected,
    bool ProductionRouteBlocked,
    bool ProductionLedgerBlocked,
    IReadOnlyList<string> Reasons);

public sealed record R009ExistingLmaxSandboxProfileClassification(
    R009SandboxOrderPathStatus Status,
    R009OperatorSandboxProfileAttestation Attestation,
    R009SandboxCredentialProfileValidation CredentialProfile,
    string Environment,
    string BrokerVenue,
    bool EnvironmentIsDemoOrSandbox,
    bool ExistingLmaxProfileIsSandbox,
    bool EndpointValuesRedacted,
    bool ProductionEndpointDetected,
    bool ProductionCredentialsDetected,
    bool SafeForSandboxGuardrailEvaluation,
    IReadOnlyList<string> MissingNonSecretConfigurationNames,
    IReadOnlyList<string> Reasons);

public sealed record R009SandboxFixLogonFailureDiagnosis(
    R009SandboxOrderPathStatus Status,
    bool PriorLogonConfirmed,
    bool PriorNewOrderSingleSent,
    bool CredentialValuesRedacted,
    bool SenderCompIdVariablePresent,
    bool TargetCompIdVariablePresent,
    bool LocalOrderTargetConfigured,
    bool LocalMarketDataTargetConfigured,
    bool GenericDemoTargetMayOverrideOrderTarget,
    string ExpectedLogonAckMessageType,
    IReadOnlyList<string> Findings,
    IReadOnlyList<string> RepairCandidates);

public sealed record R009SandboxFixSessionConfigInventory(
    string EnvironmentName,
    bool BeginStringConfigured,
    string BeginString,
    bool FixOrderHostConfigured,
    bool FixOrderPortConfigured,
    bool UseTlsConfigured,
    bool HeartbeatIntervalConfigured,
    int HeartbeatIntervalSeconds,
    bool ResetSeqNumFlagConfigured,
    string ResetSeqNumFlag,
    bool SenderCompIdVariablePresent,
    bool TargetCompIdVariablePresent,
    bool FixUsernameVariablePresent,
    bool FixPasswordVariablePresent,
    bool EndpointValuesRedacted,
    bool CredentialValuesRedacted,
    bool ProductionEndpointDetected);

public sealed record R009SandboxNonSecretSessionRepairResult(
    R009SandboxOrderPathStatus Status,
    bool RepairApplied,
    string RepairName,
    bool UsesLocalOrderTarget,
    bool AvoidsGenericTargetOverride,
    bool ProductionRouteBlocked,
    IReadOnlyList<string> MissingNonSecretFields,
    IReadOnlyList<string> Reasons);

public sealed record R009SandboxQuantityRejectionDiagnosis(
    R009SandboxOrderPathStatus Status,
    string SourceArtifact,
    string Symbol,
    string Side,
    string OrderType,
    decimal RejectedQuantity,
    decimal RejectedNotional,
    string RejectReason,
    string FixOrderQtyField,
    bool QuantityNotValidConfirmed,
    IReadOnlyList<string> Findings);

public sealed record R009SandboxLocalQuantityRuleDiscovery(
    R009SandboxOrderPathStatus Status,
    string Symbol,
    decimal? MinOrderQuantity,
    decimal? QuantityStep,
    decimal? ContractSize,
    int? QuantityPrecision,
    decimal? LabDefaultMaxDemoOrderQuantity,
    decimal? LabDefaultMaxDemoOrderNotionalUsd,
    string QuantityUnit,
    IReadOnlyList<string> SourceEvidencePaths,
    IReadOnlyList<string> MissingCalibrationFields,
    IReadOnlyList<string> Reasons);

public sealed record R009SandboxCalibratedQuantityResult(
    R009SandboxOrderPathStatus Status,
    string Symbol,
    decimal? CalibratedQuantity,
    string QuantityUnit,
    decimal? QuantityStep,
    int? QuantityPrecision,
    decimal MaxSandboxOrderCount,
    decimal MaxSandboxNotional,
    bool WithinSandboxQuantityCap,
    bool WithinSandboxNotionalCap,
    bool QuantityInventedWithoutLocalEvidence,
    string SourceEvidencePath,
    IReadOnlyList<string> Reasons);

public sealed record R009SandboxAcceptedFillReview(
    R009SandboxOrderPathStatus Status,
    string SourcePhase,
    string SourceArtifact,
    string Symbol,
    decimal RequestedQuantity,
    decimal FilledQuantity,
    decimal FillPrice,
    string FinalOrderStatus,
    string FinalExecType,
    bool SandboxOnly,
    bool ProductionOrderCreated,
    bool ProductionRouteCreated,
    bool ProductionFillOrReportCreated,
    bool ProductionLedgerMutation,
    bool ProductionStateMutation,
    bool CredentialValuesPersisted,
    IReadOnlyList<string> Findings);

public sealed record R009SandboxSymbolQuantityRule(
    string Symbol,
    decimal MinOrderQuantity,
    decimal QuantityStep,
    decimal ContractSize,
    decimal MaxDemoOrderQuantity,
    int QuantityPrecision,
    string SourceEvidencePath);

public sealed record R009SandboxQuantityControlContract(
    int MaxSandboxOrderCount,
    decimal MaxOrderQuantityPerSymbol,
    decimal MaxTotalSandboxQuantity,
    bool RejectBelowMin,
    bool RejectNonStepQuantities,
    bool RejectAboveSandboxCap,
    bool RejectUnknownSymbolQuantityRules,
    IReadOnlyList<R009SandboxSymbolQuantityRule> Rules);

public sealed record R009SandboxQuantityNormalizationResult(
    R009SandboxOrderPathStatus Status,
    string Symbol,
    decimal RequestedQuantity,
    decimal? NormalizedQuantity,
    bool RuleDiscovered,
    bool BelowMinRejected,
    bool NonStepQuantityRejected,
    bool AboveSandboxCapRejected,
    bool UnknownSymbolQuantityRuleRejected,
    IReadOnlyList<string> Reasons);

public sealed record R009SandboxUsdPairQuantityRuleInventory(
    string Phase,
    IReadOnlyList<string> SupportedSymbols,
    IReadOnlyList<R009SandboxPerSymbolQuantityCalibrationResult> Results,
    int LocallyValidatedCount,
    int SandboxValidatedCount,
    int QuantityRejectedCount,
    int MissingSkippedCount,
    bool QuantityRulesInvented);

public sealed record R009SandboxPerSymbolQuantityCalibrationResult(
    string Symbol,
    string QuantityRuleStatus,
    decimal CandidateQuantity,
    bool Attempted,
    bool Submitted,
    bool AcceptedOrAcked,
    bool Rejected,
    string? RejectReason,
    int FillCount,
    string SecurityID,
    string SecurityIDSource,
    bool SandboxOnly,
    bool ProductionOrderCreated,
    bool ProductionRouteCreated,
    bool ProductionLedgerMutation,
    IReadOnlyList<string> SourceEvidencePaths);

public sealed record R009SandboxQuantityCalibrationPlanValidation(
    R009SandboxOrderPathStatus Status,
    int MaxSandboxOrderCount,
    decimal MaxOrderQuantityPerSymbol,
    decimal MaxTotalSandboxQuantity,
    int PlannedOrderCount,
    decimal PlannedTotalQuantity,
    bool OneOrderPerSymbol,
    bool UnsupportedSymbolSubmitted,
    bool DirectCrossExecutionAllowed,
    bool NonWhitelistedSymbolAllowed,
    bool Legacy06AcceptedAsFutureCanonical,
    bool UsdjpyCaveatPreserved,
    bool AudusdMisclassified,
    IReadOnlyList<string> Reasons);

public sealed record R009SandboxR007FillReview(
    R009SandboxOrderPathStatus Status,
    int FillCount,
    decimal TotalFilledQuantity,
    IReadOnlyList<string> Symbols,
    bool SevenWhitelistedSymbolsFilled,
    bool QuantityPointOnePerSymbol,
    bool SandboxOnly,
    bool ProductionArtifactDetected,
    IReadOnlyList<string> Reasons);

public sealed record R009SandboxPositionReconciliationLine(
    string Symbol,
    string SourceSide,
    decimal R007FilledQuantity,
    decimal SignedPositionQuantity,
    string PositionSource,
    bool SandboxOnly);

public sealed record R009SandboxPositionReconciliation(
    R009SandboxOrderPathStatus Status,
    string PositionSource,
    IReadOnlyList<R009SandboxPositionReconciliationLine> Lines,
    decimal GrossOpenQuantity,
    bool ProductionPositionQueryUsed,
    IReadOnlyList<string> Reasons);

public sealed record R009SandboxFlattenOrderPlanLine(
    string Symbol,
    string FlattenSide,
    decimal FlattenQuantity,
    string SecurityID,
    string SecurityIDSource,
    bool RequiresInversion,
    string NormalizedPortfolioSymbol,
    bool SandboxOnly,
    bool ProductionOrder);

public sealed record R009SandboxFlattenOrderPlan(
    R009SandboxOrderPathStatus Status,
    IReadOnlyList<R009SandboxFlattenOrderPlanLine> Lines,
    int PlannedOrderCount,
    decimal PlannedTotalQuantity,
    bool OneFlattenOrderPerOpenPosition,
    bool DirectCrossExecutionAllowed,
    bool NonWhitelistedSymbolAllowed,
    IReadOnlyList<string> Reasons);

public sealed record R009SandboxFlattenGuardrailValidation(
    R009SandboxOrderPathStatus Status,
    int MaxSandboxFlattenOrderCount,
    decimal MaxFlattenQuantityPerSymbol,
    decimal MaxTotalFlattenQuantity,
    int PlannedOrderCount,
    decimal PlannedTotalQuantity,
    bool SandboxProfileConfirmed,
    bool CredentialValuesRedacted,
    bool ProductionRouteBlocked,
    bool ProductionLedgerBlocked,
    bool SchedulerBlocked,
    bool CanonicalTimestampPolicyPreserved,
    bool UsdjpyCaveatPreserved,
    bool AudusdMisclassified,
    IReadOnlyList<string> Reasons);

public sealed record R009SandboxPostFlattenReconciliation(
    R009SandboxOrderPathStatus Status,
    int R007OpenPositionCount,
    int FlattenSubmittedCount,
    int FlattenFilledCount,
    decimal ExpectedResidualQuantity,
    bool FlatByFillReportDerivedAudit,
    bool ProductionMutationDetected,
    IReadOnlyList<string> ResidualDiagnostics);

public enum R009SandboxOmsState
{
    SandboxIntentCreated,
    SandboxRiskChecked,
    SandboxRouteCreated,
    SandboxSubmitted,
    SandboxAcked,
    SandboxRejected,
    SandboxPartiallyFilled,
    SandboxFilled,
    SandboxFlattenIntentCreated,
    SandboxFlattenSubmitted,
    SandboxFlattenFilled,
    SandboxFlatConfirmed,
    SandboxResidualDetected,
    SandboxCancelled,
    SandboxTerminal
}

public sealed record R009SandboxOmsStateTransition(
    R009SandboxOmsState From,
    R009SandboxOmsState To,
    string Trigger,
    bool SandboxOrderState,
    bool ProductionOrderStateForbidden,
    bool LedgerStateForbidden,
    bool ReconciliationState,
    bool IdempotencyState);

public sealed record R009SandboxOmsStateModel(
    IReadOnlyList<R009SandboxOmsStateTransition> Transitions,
    bool ProductionOrderStateForbidden,
    bool ProductionLedgerStateForbidden,
    bool SupportsReconciliationState,
    bool SupportsIdempotencyState);

public sealed record R009SandboxIdempotencyContract(
    string SandboxOrderIntentId,
    string SandboxRouteId,
    string SandboxSubmissionId,
    string ClOrdId,
    string IdempotencyKey,
    bool DuplicateClOrdIdRejected,
    bool SameIntentReplaySafe,
    bool SameIntentDifferentQuantityConflict,
    bool AlreadyFlattenedPositionRequiresExplicitNewSandboxApproval,
    bool NoProductionOrderFallback);

public sealed record R009SandboxDuplicatePreventionResult(
    R009SandboxOrderPathStatus Status,
    bool DuplicateClOrdIdRejected,
    bool SameIntentReplaySafe,
    bool SameIntentDifferentQuantityConflict,
    bool AlreadyFlattenedReplayBlocked,
    bool NoDuplicateSubmissionForSameIdempotencyKey,
    bool NoProductionOrderFallback,
    IReadOnlyList<string> Reasons);

public sealed record R009SandboxOmsHandoffContract(
    R009SandboxOrderPathStatus Status,
    IReadOnlyList<R009SandboxOmsStateTransition> AllowedTransitions,
    IReadOnlyList<string> ForbiddenTransitions,
    bool SandboxLifecycleAccepted,
    bool ProductionOmsStateMutationAllowed,
    bool PaperLedgerCommitAllowed,
    bool ProductionLedgerCommitAllowed,
    bool TradingStateMutationAllowed,
    IReadOnlyList<string> Reasons);

public sealed record R009SandboxStateTransitionMap(
    IReadOnlyList<string> EvidencePhases,
    IReadOnlyDictionary<R009SandboxOmsState, string> EvidenceByState,
    IReadOnlyList<R009SandboxOmsStateTransition> AllowedTransitions,
    IReadOnlyList<string> ForbiddenTransitions,
    bool ProductionStateForbidden,
    bool LedgerStateForbidden);

public sealed record R009SandboxPaperLedgerSeparationContract(
    bool PaperLedgerCommitAllowed,
    bool ProductionLedgerCommitAllowed,
    bool TradingStateMutationAllowed,
    bool SandboxFillCanBeReferencedForReview,
    bool SandboxFillCanMutateLedger,
    bool SandboxFillCanMutateProductionState,
    bool PaperLedgerPreviewOnlyPreserved,
    string BoundaryStatement);

public sealed record R009SandboxDuplicatePreventionHandoff(
    R009SandboxOrderPathStatus Status,
    bool DuplicateClOrdIdPreventionPreserved,
    bool SameIntentReplaySafe,
    bool SameIntentDifferentQuantityConflict,
    bool AlreadyFlattenedProtectionPreserved,
    bool NoDuplicateSubmissionForSameIdempotencyKey,
    bool NoProductionOrderFallback,
    IReadOnlyList<string> Reasons);

public sealed record R009SandboxPriceControlContract(
    bool MarketOrdersAllowedForSandboxSmoke,
    bool LimitOrdersRequireExplicitSandboxLimitPrice,
    bool LiveMarketDataRequestAllowed,
    bool ProductionAggressivePricingAllowed,
    string AllowedMarketOrderReason);

public sealed record R009SandboxMarketabilityControlReview(
    R009SandboxOrderPathStatus Status,
    string OrderType,
    bool UsesLiveMarketData,
    bool RequiresLimitPrice,
    bool ExplicitSandboxLimitPriceAvailable,
    bool PriceSensitiveOrderBlocked,
    IReadOnlyList<string> Reasons);

public sealed record R009LmaxSandboxConfigValidation(
    R009SandboxOrderPathStatus Status,
    R009LmaxSandboxConfigContract Contract,
    R009SandboxCredentialProfileValidation CredentialProfile,
    bool ExplicitSandboxConfig,
    bool ProductionRouteBlocked,
    bool ProductionLedgerBlocked,
    bool SchedulerBlocked,
    bool SafeForOneBoundedSandboxOrder,
    IReadOnlyList<string> Reasons);

public sealed record R009SandboxGuardrailContract(
    string Environment,
    string BrokerVenue,
    bool ProductionVenueAllowed,
    bool ProductionCredentialsAllowed,
    bool SandboxCredentialsRequired,
    int MaxSandboxOrderCount,
    decimal MaxSandboxNotional,
    IReadOnlyList<string> WhitelistedSymbols,
    bool DirectCrossExecutionAllowed,
    bool NonMajorExecutionAllowed,
    bool Legacy06AcceptedAsFutureCanonical,
    bool OperatorSandboxApprovalRequired,
    bool KillSwitchRequiredBeforeEachSubmission,
    bool IdempotentSubmissionRequired)
{
    public static R009SandboxGuardrailContract Default { get; } = new(
        Environment: "Sandbox",
        BrokerVenue: "LMAXSandbox",
        ProductionVenueAllowed: false,
        ProductionCredentialsAllowed: false,
        SandboxCredentialsRequired: true,
        MaxSandboxOrderCount: 1,
        MaxSandboxNotional: 100m,
        WhitelistedSymbols: R009LmaxSandboxOrderPathSmokeGate.WhitelistedSymbols,
        DirectCrossExecutionAllowed: false,
        NonMajorExecutionAllowed: false,
        Legacy06AcceptedAsFutureCanonical: false,
        OperatorSandboxApprovalRequired: true,
        KillSwitchRequiredBeforeEachSubmission: true,
        IdempotentSubmissionRequired: true);
}

public sealed record R009SandboxExecutionIntent(
    string ExecutionIntentId,
    string SourceDecisionPreviewId,
    string Symbol,
    string ExecutionTradableSymbol,
    string NormalizedPortfolioSymbol,
    bool RequiresInversion,
    string? SecurityID,
    string? SecurityIDSource,
    string Side,
    decimal TargetQuantity,
    decimal TargetNotional,
    DateTimeOffset CanonicalTargetCloseUtc,
    string BarRole,
    bool ReadinessPresent,
    bool ReadinessWaivedForSandboxSmokeTest,
    bool OperatorSandboxApproval,
    bool KillSwitchOpenForSandboxOnly,
    string R009DecisionStatus,
    bool SandboxOnly,
    bool ProductionOrder,
    bool IsLiveProduction,
    bool NoProductionLedgerCommit);

public sealed record R009SandboxOrderIntent(
    string SandboxOrderIntentId,
    string ExecutionIntentId,
    string Symbol,
    string ExecutionTradableSymbol,
    string NormalizedPortfolioSymbol,
    bool RequiresInversion,
    string? SecurityID,
    string? SecurityIDSource,
    string Side,
    decimal Quantity,
    decimal Notional,
    DateTimeOffset CanonicalTargetCloseUtc,
    string BrokerVenue,
    bool SandboxOnly,
    bool ProductionOrder,
    bool IsLiveProduction,
    bool NoProductionLedgerCommit,
    bool IdempotentSubmissionRequired);

public sealed record R009SandboxRoute(
    string RouteId,
    string SandboxOrderIntentId,
    string BrokerVenue,
    string Environment,
    bool SandboxOnly,
    bool ProductionRoute,
    bool NonSandboxBrokerRoute,
    bool ProductionCredentialsUsed);

public sealed record R009SandboxSubmission(
    string SubmissionId,
    string RouteId,
    R009SandboxSubmissionStatus Status,
    bool SandboxOnly,
    bool ProductionSubmission,
    int SubmittedOrderCount,
    decimal SubmittedNotional,
    string? AckOrRejectReason);

public sealed record R009SandboxExecutionReport(
    string ExecutionReportId,
    string SubmissionId,
    string Status,
    bool SandboxOnly,
    bool ProductionExecutionReport,
    string? BrokerExecutionId,
    string? RejectReason);

public sealed record R009SandboxFill(
    string FillId,
    string ExecutionReportId,
    string Symbol,
    decimal Quantity,
    decimal Notional,
    bool SandboxOnly,
    bool ProductionFill);

public sealed record R009SandboxReconciliationResult(
    string ReconciliationId,
    string SandboxOrderIntentId,
    string SubmissionId,
    bool IntendedMatchesSubmitted,
    bool AckOrRejectCaptured,
    bool ExecutionReportCaptured,
    bool FillCaptured,
    bool ProductionLedgerMutation,
    bool ProductionStateMutation,
    bool SandboxOnly);

public sealed record R009SandboxAuditRecord(
    string AuditId,
    string ExecutionIntentId,
    string SandboxOrderIntentId,
    string Environment,
    string BrokerVenue,
    bool SandboxOnly,
    bool ProductionOrderCreated,
    bool ProductionRouteCreated,
    bool ProductionFillOrReportCreated,
    bool ProductionLedgerCommit,
    bool ProductionStateMutation,
    string AuditHash);

public sealed record R009PretradeSandboxRiskCheck(
    R009SandboxOrderPathStatus Status,
    bool EnvironmentIsSandbox,
    bool BrokerVenueIsLmaxSandbox,
    bool SandboxCredentialsPresent,
    bool SymbolWhitelisted,
    bool DirectCrossRejected,
    bool CanonicalQuarterHourTargetClose,
    bool ReadinessPresentOrWaived,
    bool OperatorSandboxApprovalPresent,
    bool MaxOrderCountSatisfied,
    bool MaxNotionalSatisfied,
    bool KillSwitchOpenForSandboxOnly,
    bool NoProductionRoute,
    bool NoProductionLedger,
    bool NoScheduler,
    IReadOnlyList<string> Reasons);

public sealed record R009SandboxOrderIntentResult(
    R009PretradeSandboxRiskCheck RiskCheck,
    R009SandboxOrderIntent? OrderIntent,
    R009SandboxRoute? Route,
    R009SandboxSubmission Submission,
    R009SandboxReconciliationResult Reconciliation);

public sealed record R011PmsPaperR015SourceLine(
    string CycleRunId,
    string IntentPreviewId,
    string OmsPreviewStateId,
    string Symbol,
    string Direction,
    decimal DeltaWeight,
    decimal DeltaNotional);

public sealed record R011SideDerivationEvidence(
    R009SandboxOrderPathStatus Status,
    string PortfolioSide,
    string ExecutionSide,
    string SourceDirection,
    decimal SourceDeltaNotional,
    string SourceField,
    bool RequiresInversion,
    IReadOnlyList<string> Reasons);

public sealed record R011BrokerSymbolMapping(
    R009SandboxOrderPathStatus Status,
    string SourceSymbol,
    string ExecutionTradableSymbol,
    string NormalizedPortfolioSymbol,
    bool RequiresInversion,
    string? SecurityID,
    string? SecurityIDSource,
    bool Whitelisted,
    bool DirectCrossRejected,
    bool AudusdMisclassified,
    bool UsdjpyCaveatPreserved,
    IReadOnlyList<string> Reasons);

public sealed record R011ExecAlgoFieldCompletion(
    R009SandboxOrderPathStatus Status,
    string R009ContractVersion,
    IReadOnlyList<string> SelectedPolicies,
    string BrokerVenue,
    string Environment,
    decimal SandboxQuantity,
    DateTimeOffset CanonicalTargetCloseUtc,
    string IdempotencyKey,
    bool SandboxOnly,
    bool ProductionAllowed,
    bool PaperLedgerCommitAllowed,
    IReadOnlyList<string> CompletedFields,
    IReadOnlyList<string> MissingFields,
    IReadOnlyList<string> Reasons);

public sealed record R011PmsPaperR015SandboxMappingResult(
    R009SandboxOrderPathStatus Status,
    R011PmsPaperR015SourceLine SourceLine,
    R011SideDerivationEvidence SideDerivation,
    R011BrokerSymbolMapping BrokerSymbolMapping,
    R011ExecAlgoFieldCompletion FieldCompletion,
    R009SandboxExecutionIntent? ExecutionIntent,
    IReadOnlyList<string> Reasons);

public sealed class R009LmaxSandboxOrderPathSmokeGate
{
    public static IReadOnlyList<string> WhitelistedSymbols { get; } =
    [
        "EURUSD",
        "USDJPY",
        "AUDUSD",
        "GBPUSD",
        "NZDUSD",
        "USDCAD",
        "USDCHF"
    ];

    public R009LmaxSandboxConfigDiscovery DiscoverSandboxConfig(IReadOnlyDictionary<string, string?> values)
    {
        var environment = Get(values, "LmaxSandbox:Environment") ?? Get(values, "LmaxSandbox:EnvironmentName") ?? "";
        var brokerVenue = Get(values, "LmaxSandbox:BrokerVenue") ?? "";
        var credentialProfile = Get(values, "LmaxSandbox:SandboxCredentialProfileName") ?? Get(values, "LmaxSandbox:CredentialProfileName") ?? "";
        var maxOrderCount = ParseInt(Get(values, "LmaxSandbox:MaxSandboxOrderCount"));
        var maxNotional = ParseDecimal(Get(values, "LmaxSandbox:MaxSandboxNotional"));
        var reasons = new List<string>();

        var sandboxConfigPresent = values.Keys.Any(x => x.StartsWith("LmaxSandbox:", StringComparison.OrdinalIgnoreCase));
        var environmentIsSandbox = string.Equals(environment, "Sandbox", StringComparison.OrdinalIgnoreCase);
        var brokerVenueIsSandbox = string.Equals(brokerVenue, "LMAXSandbox", StringComparison.OrdinalIgnoreCase);
        var productionVenueAllowed = ParseBool(Get(values, "LmaxSandbox:ProductionVenueAllowed"));
        var productionCredentialsAllowed = ParseBool(Get(values, "LmaxSandbox:ProductionCredentialsAllowed"));
        var sandboxCredentialsRequired = ParseBool(Get(values, "LmaxSandbox:SandboxCredentialsRequired"));
        var allowSandboxOrderSubmission = ParseBool(Get(values, "LmaxSandbox:AllowSandboxOrderSubmission"));
        var schedulerEnabled = ParseBool(Get(values, "LmaxSandbox:SchedulerEnabled")) || ParseBool(Get(values, "LmaxSandbox:BackgroundWorkerEnabled"));
        var productionCredentialsDetected =
            ContainsProductionLabel(credentialProfile) ||
            ContainsProductionLabel(Get(values, "LmaxSandbox:Username")) ||
            ContainsProductionLabel(Get(values, "LmaxSandbox:SenderCompId"));

        if (!sandboxConfigPresent) reasons.Add("LmaxSandboxConfigMissing");
        if (!environmentIsSandbox) reasons.Add("EnvironmentMustBeSandbox");
        if (!brokerVenueIsSandbox) reasons.Add("BrokerVenueMustBeLMAXSandbox");
        if (productionVenueAllowed) reasons.Add("ProductionVenueAllowedMustBeFalse");
        if (productionCredentialsAllowed) reasons.Add("ProductionCredentialsAllowedMustBeFalse");
        if (!sandboxCredentialsRequired) reasons.Add("SandboxCredentialsRequiredMustBeTrue");
        if (string.IsNullOrWhiteSpace(credentialProfile)) reasons.Add("SandboxCredentialProfileMissing");
        if (productionCredentialsDetected) reasons.Add("ProductionCredentialLabelDetected");
        if (!allowSandboxOrderSubmission) reasons.Add("SandboxOrderSubmissionNotExplicitlyAllowed");
        if (schedulerEnabled) reasons.Add("SchedulerOrBackgroundWorkerEnabled");
        if (maxOrderCount is <= 0 or > 3) reasons.Add("MaxSandboxOrderCountMustBe1To3");
        if (maxNotional <= 0m) reasons.Add("MaxSandboxNotionalMissingOrNonPositive");

        var status = reasons.Count == 0 ? R009SandboxOrderPathStatus.Ready : R009SandboxOrderPathStatus.Blocked;
        return new R009LmaxSandboxConfigDiscovery(
            Status: status,
            Environment: environment,
            BrokerVenue: brokerVenue,
            SandboxConfigPresent: sandboxConfigPresent,
            EnvironmentIsSandbox: environmentIsSandbox,
            BrokerVenueIsLmaxSandbox: brokerVenueIsSandbox,
            ProductionVenueAllowed: productionVenueAllowed,
            ProductionCredentialsAllowed: productionCredentialsAllowed,
            SandboxCredentialsRequired: sandboxCredentialsRequired,
            SandboxCredentialProfilePresent: !string.IsNullOrWhiteSpace(credentialProfile),
            ProductionCredentialsDetected: productionCredentialsDetected,
            AllowSandboxOrderSubmission: allowSandboxOrderSubmission,
            SchedulerServicePollingBackgroundJobEnabled: schedulerEnabled,
            MaxSandboxOrderCount: maxOrderCount,
            MaxSandboxNotional: maxNotional,
            Reasons: reasons);
    }

    public R009LmaxSandboxConfigValidation ValidateSandboxConfigContract(
        R009LmaxSandboxConfigContract contract,
        string credentialProfileName,
        string credentialSourceType,
        bool sandboxCredentialPresent)
    {
        var credentialValidation = ValidateCredentialProfile(credentialProfileName, credentialSourceType, sandboxCredentialPresent);
        var reasons = new List<string>();

        var explicitSandboxConfig =
            string.Equals(contract.Environment, "Sandbox", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(contract.BrokerVenue, "LMAXSandbox", StringComparison.OrdinalIgnoreCase);

        if (!explicitSandboxConfig) reasons.Add("ExplicitSandboxConfigRequired");
        if (contract.ProductionVenueAllowed) reasons.Add("ProductionVenueAllowedMustBeFalse");
        if (contract.ProductionCredentialsAllowed) reasons.Add("ProductionCredentialsAllowedMustBeFalse");
        if (!contract.SandboxCredentialsRequired) reasons.Add("SandboxCredentialsRequiredMustBeTrue");
        if (!contract.SandboxOrderSubmissionEnabled) reasons.Add("SandboxOrderSubmissionEnabledRequired");
        if (!contract.SandboxKillSwitchOpen) reasons.Add("SandboxKillSwitchMustBeOpen");
        if (contract.MaxSandboxOrderCount != 1) reasons.Add("MaxSandboxOrderCountMustEqualOne");
        if (contract.MaxSandboxNotional <= 0m || contract.MaxSandboxNotional > 100m) reasons.Add("MaxSandboxNotionalMustBeTinyAndPositive");
        if (contract.DirectCrossExecutionAllowed) reasons.Add("DirectCrossExecutionAllowedMustBeFalse");
        if (contract.NonmajorExecutionAllowed) reasons.Add("NonmajorExecutionAllowedMustBeFalse");
        if (contract.PaperLedgerCommitAllowed) reasons.Add("PaperLedgerCommitAllowedMustBeFalse");
        if (contract.ProductionLedgerCommitAllowed) reasons.Add("ProductionLedgerCommitAllowedMustBeFalse");
        if (contract.StateMutationAllowed) reasons.Add("StateMutationAllowedMustBeFalse");
        if (!R009LmaxSandboxOrderPathSmokeGate.WhitelistedSymbols.All(symbol => contract.AllowedSymbols.Contains(symbol, StringComparer.OrdinalIgnoreCase))) reasons.Add("AllowedSymbolsMustContainUsdPairWhitelist");
        if (credentialValidation.Status == R009SandboxOrderPathStatus.Blocked) reasons.AddRange(credentialValidation.Reasons);

        var productionRouteBlocked = !contract.ProductionVenueAllowed && !contract.ProductionCredentialsAllowed;
        var productionLedgerBlocked = !contract.PaperLedgerCommitAllowed && !contract.ProductionLedgerCommitAllowed && !contract.StateMutationAllowed;
        var schedulerBlocked = true;
        var status = reasons.Count == 0 ? R009SandboxOrderPathStatus.Ready : R009SandboxOrderPathStatus.Blocked;

        return new R009LmaxSandboxConfigValidation(
            Status: status,
            Contract: contract,
            CredentialProfile: credentialValidation,
            ExplicitSandboxConfig: explicitSandboxConfig,
            ProductionRouteBlocked: productionRouteBlocked,
            ProductionLedgerBlocked: productionLedgerBlocked,
            SchedulerBlocked: schedulerBlocked,
            SafeForOneBoundedSandboxOrder: status == R009SandboxOrderPathStatus.Ready,
            Reasons: reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public R009SandboxCredentialProfileValidation ValidateCredentialProfile(
        string credentialProfileName,
        string credentialSourceType,
        bool sandboxCredentialPresent)
    {
        var reasons = new List<string>();
        if (string.IsNullOrWhiteSpace(credentialProfileName)) reasons.Add("SandboxCredentialProfileMissing");
        if (string.IsNullOrWhiteSpace(credentialSourceType)) reasons.Add("CredentialSourceTypeMissing");
        if (!sandboxCredentialPresent) reasons.Add("SandboxCredentialPresentFalse");
        if (ContainsProductionLabel(credentialProfileName)) reasons.Add("ProductionCredentialDetected");
        if (ContainsProductionLabel(credentialSourceType)) reasons.Add("ProductionCredentialDetected");

        var status = reasons.Count == 0 ? R009SandboxOrderPathStatus.Ready : R009SandboxOrderPathStatus.Blocked;
        return new R009SandboxCredentialProfileValidation(
            Status: status,
            CredentialProfileName: credentialProfileName,
            CredentialSourceType: credentialSourceType,
            CredentialValuesRedacted: true,
            ProductionCredentialDetected: reasons.Contains("ProductionCredentialDetected", StringComparer.OrdinalIgnoreCase),
            SandboxCredentialPresent: sandboxCredentialPresent,
            CredentialVariablePresence: new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase),
            MissingProfileNames: string.IsNullOrWhiteSpace(credentialProfileName) ? new[] { "LmaxSandbox:CredentialProfileName" } : Array.Empty<string>(),
            Reasons: reasons);
    }

    public R009SandboxCredentialProfileValidation ValidateSandboxCredentialEnvironmentVariables(
        IReadOnlyList<string> requiredVariableNames,
        IReadOnlyDictionary<string, bool> variablePresence)
    {
        var reasons = new List<string>();
        if (requiredVariableNames.Count == 0) reasons.Add("SandboxCredentialVariableNamesMissing");

        var presence = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var variableName in requiredVariableNames)
        {
            var present = variablePresence.TryGetValue(variableName, out var exists) && exists;
            presence[variableName] = present;
            if (!present) reasons.Add($"MissingCredentialVariable:{variableName}");
            if (ContainsProductionLabel(variableName)) reasons.Add("ProductionCredentialDetected");
        }

        var allPresent = requiredVariableNames.Count > 0 && presence.Values.All(x => x);
        var status = reasons.Count == 0 ? R009SandboxOrderPathStatus.Ready : R009SandboxOrderPathStatus.Blocked;
        return new R009SandboxCredentialProfileValidation(
            Status: status,
            CredentialProfileName: "LMAX_DEMO_ENV_VARS",
            CredentialSourceType: "EnvVars",
            CredentialValuesRedacted: true,
            ProductionCredentialDetected: reasons.Contains("ProductionCredentialDetected", StringComparer.OrdinalIgnoreCase),
            SandboxCredentialPresent: allPresent,
            CredentialVariablePresence: presence,
            MissingProfileNames: presence.Where(x => !x.Value).Select(x => x.Key).ToArray(),
            Reasons: reasons);
    }

    public R009ExistingLmaxSandboxProfileClassification ClassifyOperatorAttestedExistingLmaxProfile(
        bool currentLmaxSetupOperatorAttestedSandbox,
        IReadOnlyDictionary<string, string?> localConfigValues,
        R009SandboxCredentialProfileValidation credentialProfile)
    {
        var environment = Get(localConfigValues, "LmaxConnectivityLab:EnvironmentName") ?? "";
        var brokerVenue = "ExistingLmaxDemoProfile";
        var fixOrderHost = Get(localConfigValues, "LmaxConnectivityLab:FixOrderHost");
        var fixOrderPort = Get(localConfigValues, "LmaxConnectivityLab:FixOrderPort");
        var fixOrderTarget = Get(localConfigValues, "LmaxConnectivityLab:FixOrderTargetCompId") ?? Get(localConfigValues, "LmaxConnectivityLab:FixTargetCompId");
        var accountCode = Get(localConfigValues, "LmaxConnectivityLab:AccountCode");
        var reasons = new List<string>();
        var missing = new List<string>();

        var environmentIsDemoOrSandbox =
            string.Equals(environment, "Demo", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(environment, "Sandbox", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(environment, "UAT", StringComparison.OrdinalIgnoreCase);
        var productionEndpointDetected = ContainsProductionLabel(fixOrderHost) || ContainsProductionLabel(accountCode) || ContainsProductionLabel(environment);
        var existingProfileIsSandbox =
            currentLmaxSetupOperatorAttestedSandbox &&
            environmentIsDemoOrSandbox &&
            !productionEndpointDetected &&
            credentialProfile.Status == R009SandboxOrderPathStatus.Ready &&
            credentialProfile.CredentialSourceType.Equals("EnvVars", StringComparison.OrdinalIgnoreCase);

        if (!currentLmaxSetupOperatorAttestedSandbox) reasons.Add("OperatorSandboxAttestationRequired");
        if (!environmentIsDemoOrSandbox) reasons.Add("EnvironmentMustBeDemoSandboxOrUat");
        if (productionEndpointDetected) reasons.Add("ProductionEndpointDetected");
        if (credentialProfile.Status == R009SandboxOrderPathStatus.Blocked) reasons.AddRange(credentialProfile.Reasons);
        if (string.IsNullOrWhiteSpace(fixOrderHost)) missing.Add("LmaxConnectivityLab:FixOrderHost");
        if (string.IsNullOrWhiteSpace(fixOrderPort)) missing.Add("LmaxConnectivityLab:FixOrderPort");
        if (string.IsNullOrWhiteSpace(fixOrderTarget)) missing.Add("LmaxConnectivityLab:FixOrderTargetCompId");
        if (missing.Count > 0) reasons.AddRange(missing.Select(x => $"MissingNonSecretConfig:{x}"));

        var attestation = new R009OperatorSandboxProfileAttestation(
            CurrentLmaxSetupOperatorAttestedSandbox: currentLmaxSetupOperatorAttestedSandbox,
            ExistingLmaxProfileIsSandbox: existingProfileIsSandbox,
            SandboxClassificationSource: "OperatorAttestationAndDemoCredentialProfile",
            BrokerVenue: brokerVenue,
            EndpointValuesRedacted: true,
            ProductionEndpointDetected: productionEndpointDetected,
            ProductionRouteBlocked: !productionEndpointDetected,
            ProductionLedgerBlocked: true,
            Reasons: reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());

        var status = reasons.Count == 0 ? R009SandboxOrderPathStatus.Ready : R009SandboxOrderPathStatus.Blocked;
        return new R009ExistingLmaxSandboxProfileClassification(
            Status: status,
            Attestation: attestation,
            CredentialProfile: credentialProfile,
            Environment: environment,
            BrokerVenue: brokerVenue,
            EnvironmentIsDemoOrSandbox: environmentIsDemoOrSandbox,
            ExistingLmaxProfileIsSandbox: existingProfileIsSandbox,
            EndpointValuesRedacted: true,
            ProductionEndpointDetected: productionEndpointDetected,
            ProductionCredentialsDetected: credentialProfile.ProductionCredentialDetected,
            SafeForSandboxGuardrailEvaluation: status == R009SandboxOrderPathStatus.Ready,
            MissingNonSecretConfigurationNames: missing.ToArray(),
            Reasons: reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public R009SandboxFixLogonFailureDiagnosis DiagnoseFixTradingLogonFailure(
        bool priorLogonConfirmed,
        bool priorNewOrderSingleSent,
        bool senderCompIdVariablePresent,
        bool targetCompIdVariablePresent,
        bool localOrderTargetConfigured,
        bool localMarketDataTargetConfigured)
    {
        var findings = new List<string>();
        var repairs = new List<string>();
        var genericTargetMayOverride = targetCompIdVariablePresent && localOrderTargetConfigured && localMarketDataTargetConfigured;

        if (!priorLogonConfirmed) findings.Add("PriorFixTradingLogonNotConfirmed");
        if (!priorNewOrderSingleSent) findings.Add("PriorNewOrderSingleNotSent");
        if (genericTargetMayOverride)
        {
            findings.Add("GenericDemoTargetCompIdMayOverrideLocalOrderTargetCompId");
            repairs.Add("PreferLocalFixOrderTargetCompIdForTradingSession");
        }

        if (!senderCompIdVariablePresent) findings.Add("MissingSenderCompIdVariable");
        if (!targetCompIdVariablePresent) findings.Add("MissingTargetCompIdVariablePresence");
        if (!localOrderTargetConfigured) findings.Add("MissingLocalFixOrderTargetCompId");

        var status = findings.Contains("MissingSenderCompIdVariable", StringComparer.OrdinalIgnoreCase) ||
                     findings.Contains("MissingLocalFixOrderTargetCompId", StringComparer.OrdinalIgnoreCase)
            ? R009SandboxOrderPathStatus.Blocked
            : R009SandboxOrderPathStatus.Ready;

        return new R009SandboxFixLogonFailureDiagnosis(
            Status: status,
            PriorLogonConfirmed: priorLogonConfirmed,
            PriorNewOrderSingleSent: priorNewOrderSingleSent,
            CredentialValuesRedacted: true,
            SenderCompIdVariablePresent: senderCompIdVariablePresent,
            TargetCompIdVariablePresent: targetCompIdVariablePresent,
            LocalOrderTargetConfigured: localOrderTargetConfigured,
            LocalMarketDataTargetConfigured: localMarketDataTargetConfigured,
            GenericDemoTargetMayOverrideOrderTarget: genericTargetMayOverride,
            ExpectedLogonAckMessageType: "35=A",
            Findings: findings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            RepairCandidates: repairs.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public R009SandboxNonSecretSessionRepairResult CreateNonSecretSessionRepairResult(
        bool localOrderTargetConfigured,
        bool productionRouteBlocked)
    {
        var missing = new List<string>();
        var reasons = new List<string>();
        if (!localOrderTargetConfigured) missing.Add("LmaxConnectivityLab:FixOrderTargetCompId");
        if (!productionRouteBlocked) reasons.Add("ProductionRouteMustRemainBlocked");
        reasons.AddRange(missing.Select(x => $"MissingNonSecretConfig:{x}"));

        var status = reasons.Count == 0 ? R009SandboxOrderPathStatus.Ready : R009SandboxOrderPathStatus.Blocked;
        return new R009SandboxNonSecretSessionRepairResult(
            Status: status,
            RepairApplied: status == R009SandboxOrderPathStatus.Ready,
            RepairName: "UseLocalFixOrderTargetCompIdAndDoNotOverrideWithGenericDemoTarget",
            UsesLocalOrderTarget: status == R009SandboxOrderPathStatus.Ready,
            AvoidsGenericTargetOverride: status == R009SandboxOrderPathStatus.Ready,
            ProductionRouteBlocked: productionRouteBlocked,
            MissingNonSecretFields: missing.ToArray(),
            Reasons: reasons.ToArray());
    }

    public R009SandboxQuantityRejectionDiagnosis DiagnoseQuantityRejection(
        string sourceArtifact,
        string symbol,
        string side,
        string orderType,
        decimal rejectedQuantity,
        decimal rejectedNotional,
        string rejectReason)
    {
        var findings = new List<string>();
        var quantityNotValid = string.Equals(rejectReason, "QUANTITY_NOT_VALID", StringComparison.OrdinalIgnoreCase);

        if (quantityNotValid) findings.Add("QuantityNotValidConfirmed");
        if (rejectedQuantity <= 0m) findings.Add("RejectedQuantityMissingOrNonPositive");
        if (!WhitelistedSymbols.Contains(symbol, StringComparer.OrdinalIgnoreCase)) findings.Add("RejectedSymbolNotWhitelisted");

        var status = quantityNotValid && rejectedQuantity > 0m && WhitelistedSymbols.Contains(symbol, StringComparer.OrdinalIgnoreCase)
            ? R009SandboxOrderPathStatus.Ready
            : R009SandboxOrderPathStatus.Blocked;

        return new R009SandboxQuantityRejectionDiagnosis(
            Status: status,
            SourceArtifact: sourceArtifact,
            Symbol: symbol,
            Side: side,
            OrderType: orderType,
            RejectedQuantity: rejectedQuantity,
            RejectedNotional: rejectedNotional,
            RejectReason: rejectReason,
            FixOrderQtyField: "38",
            QuantityNotValidConfirmed: quantityNotValid,
            Findings: findings.ToArray());
    }

    public R009SandboxLocalQuantityRuleDiscovery DiscoverLocalQuantityRule(
        string symbol,
        decimal? minOrderQuantity,
        decimal? quantityStep,
        decimal? contractSize,
        int? quantityPrecision,
        decimal? labDefaultMaxDemoOrderQuantity,
        decimal? labDefaultMaxDemoOrderNotionalUsd,
        IReadOnlyList<string> sourceEvidencePaths)
    {
        var missing = new List<string>();
        var reasons = new List<string>();

        if (!WhitelistedSymbols.Contains(symbol, StringComparer.OrdinalIgnoreCase)) reasons.Add("SymbolNotWhitelisted");
        if (minOrderQuantity is null or <= 0m) missing.Add("VenueInstrumentMapping.MinOrderQuantity");
        if (quantityStep is null or <= 0m) missing.Add("VenueInstrumentMapping.QuantityStep");
        if (contractSize is null or <= 0m) missing.Add("VenueInstrumentMapping.ContractSize");
        if (quantityPrecision is null or < 0) missing.Add("Instrument.QuantityPrecision");
        if (labDefaultMaxDemoOrderQuantity is null or <= 0m) missing.Add("LmaxConnectivityLabOptions.MaxDemoOrderQuantity");
        if (labDefaultMaxDemoOrderNotionalUsd is null or <= 0m) missing.Add("LmaxConnectivityLabOptions.MaxDemoOrderNotionalUsd");
        if (sourceEvidencePaths.Count == 0) missing.Add("LocalQuantityEvidencePath");

        reasons.AddRange(missing.Select(x => $"MissingCalibrationField:{x}"));
        var status = reasons.Count == 0 ? R009SandboxOrderPathStatus.Ready : R009SandboxOrderPathStatus.Blocked;

        return new R009SandboxLocalQuantityRuleDiscovery(
            Status: status,
            Symbol: symbol,
            MinOrderQuantity: minOrderQuantity,
            QuantityStep: quantityStep,
            ContractSize: contractSize,
            QuantityPrecision: quantityPrecision,
            LabDefaultMaxDemoOrderQuantity: labDefaultMaxDemoOrderQuantity,
            LabDefaultMaxDemoOrderNotionalUsd: labDefaultMaxDemoOrderNotionalUsd,
            QuantityUnit: "LMAXVenueOrderQtyContractUnit",
            SourceEvidencePaths: sourceEvidencePaths,
            MissingCalibrationFields: missing.ToArray(),
            Reasons: reasons.ToArray());
    }

    public R009SandboxCalibratedQuantityResult CalibrateSandboxQuantity(
        R009SandboxLocalQuantityRuleDiscovery discovery,
        decimal maxSandboxOrderCount,
        decimal maxSandboxNotional)
    {
        var reasons = new List<string>();
        if (discovery.Status == R009SandboxOrderPathStatus.Blocked) reasons.AddRange(discovery.Reasons);

        var calibrated = discovery.MinOrderQuantity;
        var withinQuantityCap = calibrated.HasValue &&
                                discovery.LabDefaultMaxDemoOrderQuantity.HasValue &&
                                calibrated.Value <= discovery.LabDefaultMaxDemoOrderQuantity.Value;
        var withinNotionalCap = maxSandboxNotional > 0m;
        var source = discovery.SourceEvidencePaths.FirstOrDefault() ?? "";

        if (!withinQuantityCap) reasons.Add("CalibratedQuantityExceedsLocalDemoQuantityCap");
        if (!withinNotionalCap) reasons.Add("MaxSandboxNotionalMissingOrNonPositive");
        if (calibrated is null) reasons.Add("CalibratedQuantityMissing");

        var invented = discovery.Status == R009SandboxOrderPathStatus.Blocked || string.IsNullOrWhiteSpace(source);
        if (invented) reasons.Add("QuantityInventedWithoutLocalEvidence");

        var status = reasons.Count == 0 ? R009SandboxOrderPathStatus.Ready : R009SandboxOrderPathStatus.Blocked;
        return new R009SandboxCalibratedQuantityResult(
            Status: status,
            Symbol: discovery.Symbol,
            CalibratedQuantity: calibrated,
            QuantityUnit: discovery.QuantityUnit,
            QuantityStep: discovery.QuantityStep,
            QuantityPrecision: discovery.QuantityPrecision,
            MaxSandboxOrderCount: maxSandboxOrderCount,
            MaxSandboxNotional: maxSandboxNotional,
            WithinSandboxQuantityCap: withinQuantityCap,
            WithinSandboxNotionalCap: withinNotionalCap,
            QuantityInventedWithoutLocalEvidence: invented,
            SourceEvidencePath: source,
            Reasons: reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public R009SandboxAcceptedFillReview ReviewAcceptedFill(
        string sourceArtifact,
        string symbol,
        decimal requestedQuantity,
        decimal filledQuantity,
        decimal fillPrice,
        string finalOrderStatus,
        string finalExecType,
        bool sandboxOnly,
        bool productionOrderCreated,
        bool productionRouteCreated,
        bool productionFillOrReportCreated,
        bool productionLedgerMutation,
        bool productionStateMutation,
        bool credentialValuesPersisted)
    {
        var findings = new List<string>();
        if (sandboxOnly) findings.Add("SandboxOnlyFill");
        if (string.Equals(finalOrderStatus, "Filled", StringComparison.OrdinalIgnoreCase)) findings.Add("FinalStatusFilled");
        if (string.Equals(finalExecType, "Trade", StringComparison.OrdinalIgnoreCase)) findings.Add("FinalExecTypeTrade");
        if (filledQuantity == requestedQuantity) findings.Add("FilledQuantityMatchesRequestedQuantity");
        if (fillPrice > 0m) findings.Add("PositiveFillPriceCaptured");

        var unsafeState =
            !sandboxOnly ||
            productionOrderCreated ||
            productionRouteCreated ||
            productionFillOrReportCreated ||
            productionLedgerMutation ||
            productionStateMutation ||
            credentialValuesPersisted ||
            !string.Equals(finalOrderStatus, "Filled", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(finalExecType, "Trade", StringComparison.OrdinalIgnoreCase);

        return new R009SandboxAcceptedFillReview(
            Status: unsafeState ? R009SandboxOrderPathStatus.Blocked : R009SandboxOrderPathStatus.Ready,
            SourcePhase: "EXEC-SANDBOX-R005",
            SourceArtifact: sourceArtifact,
            Symbol: symbol,
            RequestedQuantity: requestedQuantity,
            FilledQuantity: filledQuantity,
            FillPrice: fillPrice,
            FinalOrderStatus: finalOrderStatus,
            FinalExecType: finalExecType,
            SandboxOnly: sandboxOnly,
            ProductionOrderCreated: productionOrderCreated,
            ProductionRouteCreated: productionRouteCreated,
            ProductionFillOrReportCreated: productionFillOrReportCreated,
            ProductionLedgerMutation: productionLedgerMutation,
            ProductionStateMutation: productionStateMutation,
            CredentialValuesPersisted: credentialValuesPersisted,
            Findings: findings.ToArray());
    }

    public R009SandboxQuantityNormalizationResult NormalizeSandboxQuantity(
        string symbol,
        decimal requestedQuantity,
        R009SandboxQuantityControlContract contract)
    {
        var reasons = new List<string>();
        var rule = contract.Rules.FirstOrDefault(x => string.Equals(x.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
        var ruleDiscovered = rule is not null;
        var belowMin = false;
        var nonStep = false;
        var aboveCap = requestedQuantity > contract.MaxOrderQuantityPerSymbol;

        if (!ruleDiscovered)
        {
            reasons.Add("UnknownSymbolQuantityRule");
        }
        else
        {
            belowMin = requestedQuantity < rule!.MinOrderQuantity;
            nonStep = requestedQuantity % rule.QuantityStep != 0m;
            if (belowMin) reasons.Add("QuantityBelowMinOrderQuantity");
            if (nonStep) reasons.Add("QuantityNotAlignedToStep");
            if (requestedQuantity > rule.MaxDemoOrderQuantity) reasons.Add("QuantityAboveLocalDemoQuantityCap");
        }

        if (aboveCap) reasons.Add("QuantityAboveSandboxPerSymbolCap");

        var status = reasons.Count == 0 ? R009SandboxOrderPathStatus.Ready : R009SandboxOrderPathStatus.Blocked;
        return new R009SandboxQuantityNormalizationResult(
            Status: status,
            Symbol: symbol,
            RequestedQuantity: requestedQuantity,
            NormalizedQuantity: status == R009SandboxOrderPathStatus.Ready ? requestedQuantity : null,
            RuleDiscovered: ruleDiscovered,
            BelowMinRejected: belowMin,
            NonStepQuantityRejected: nonStep,
            AboveSandboxCapRejected: aboveCap,
            UnknownSymbolQuantityRuleRejected: !ruleDiscovered,
            Reasons: reasons.ToArray());
    }

    public R009SandboxMarketabilityControlReview ReviewMarketability(
        R009SandboxPriceControlContract contract,
        string orderType,
        bool explicitSandboxLimitPriceAvailable)
    {
        var reasons = new List<string>();
        var isMarket = string.Equals(orderType, "Market", StringComparison.OrdinalIgnoreCase);
        var isLimit = string.Equals(orderType, "Limit", StringComparison.OrdinalIgnoreCase);

        if (contract.LiveMarketDataRequestAllowed) reasons.Add("LiveMarketDataRequestMustRemainFalse");
        if (contract.ProductionAggressivePricingAllowed) reasons.Add("ProductionAggressivePricingMustRemainFalse");
        if (isMarket && !contract.MarketOrdersAllowedForSandboxSmoke) reasons.Add("SandboxMarketOrdersNotAllowed");
        if (isLimit && contract.LimitOrdersRequireExplicitSandboxLimitPrice && !explicitSandboxLimitPriceAvailable) reasons.Add("ExplicitSandboxLimitPriceRequired");
        if (!isMarket && !isLimit) reasons.Add("UnsupportedOrderType");

        return new R009SandboxMarketabilityControlReview(
            Status: reasons.Count == 0 ? R009SandboxOrderPathStatus.Ready : R009SandboxOrderPathStatus.Blocked,
            OrderType: orderType,
            UsesLiveMarketData: false,
            RequiresLimitPrice: isLimit,
            ExplicitSandboxLimitPriceAvailable: explicitSandboxLimitPriceAvailable,
            PriceSensitiveOrderBlocked: reasons.Contains("ExplicitSandboxLimitPriceRequired", StringComparer.OrdinalIgnoreCase),
            Reasons: reasons.ToArray());
    }

    public R009SandboxUsdPairQuantityRuleInventory BuildUsdPairQuantityRuleInventory(
        IReadOnlyList<R009SandboxPerSymbolQuantityCalibrationResult> results)
    {
        var normalized = WhitelistedSymbols
            .Select(symbol => results.FirstOrDefault(x => string.Equals(x.Symbol, symbol, StringComparison.OrdinalIgnoreCase)) ??
                              new R009SandboxPerSymbolQuantityCalibrationResult(
                                  Symbol: symbol,
                                  QuantityRuleStatus: "RuleMissingSkipped",
                                  CandidateQuantity: 0m,
                                  Attempted: false,
                                  Submitted: false,
                                  AcceptedOrAcked: false,
                                  Rejected: false,
                                  RejectReason: null,
                                  FillCount: 0,
                                  SecurityID: "",
                                  SecurityIDSource: "",
                                  SandboxOnly: true,
                                  ProductionOrderCreated: false,
                                  ProductionRouteCreated: false,
                                  ProductionLedgerMutation: false,
                                  SourceEvidencePaths: Array.Empty<string>()))
            .ToArray();

        return new R009SandboxUsdPairQuantityRuleInventory(
            Phase: "EXEC-SANDBOX-R007",
            SupportedSymbols: WhitelistedSymbols,
            Results: normalized,
            LocallyValidatedCount: normalized.Count(x => string.Equals(x.QuantityRuleStatus, "RuleValidatedLocal", StringComparison.OrdinalIgnoreCase)),
            SandboxValidatedCount: normalized.Count(x => string.Equals(x.QuantityRuleStatus, "RuleValidatedSandboxAccepted", StringComparison.OrdinalIgnoreCase)),
            QuantityRejectedCount: normalized.Count(x => string.Equals(x.QuantityRuleStatus, "RuleRejectedSandboxQuantity", StringComparison.OrdinalIgnoreCase)),
            MissingSkippedCount: normalized.Count(x => string.Equals(x.QuantityRuleStatus, "RuleMissingSkipped", StringComparison.OrdinalIgnoreCase)),
            QuantityRulesInvented: false);
    }

    public R009SandboxQuantityCalibrationPlanValidation ValidateUsdPairQuantityCalibrationPlan(
        IReadOnlyList<R009SandboxPerSymbolQuantityCalibrationResult> results,
        int maxSandboxOrderCount,
        decimal maxOrderQuantityPerSymbol,
        decimal maxTotalSandboxQuantity,
        DateTimeOffset canonicalTargetCloseUtc)
    {
        var reasons = new List<string>();
        var submitted = results.Where(x => x.Submitted).ToArray();
        var plannedCount = submitted.Length;
        var plannedTotal = submitted.Sum(x => x.CandidateQuantity);
        var duplicateSymbols = submitted
            .GroupBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToArray();
        var unsupportedSubmitted = submitted.Any(x => !WhitelistedSymbols.Contains(x.Symbol, StringComparer.OrdinalIgnoreCase));
        var directCrossAllowed = submitted.Any(x => !WhitelistedSymbols.Contains(x.Symbol, StringComparer.OrdinalIgnoreCase));
        var nonWhitelistedAllowed = unsupportedSubmitted;
        var legacyAccepted = !IsCanonicalQuarterHour(canonicalTargetCloseUtc);
        var usdjpy = results.FirstOrDefault(x => string.Equals(x.Symbol, "USDJPY", StringComparison.OrdinalIgnoreCase));
        var usdjpyPreserved = usdjpy is null ||
                               (string.Equals(usdjpy.SecurityID, "4004", StringComparison.Ordinal) &&
                                string.Equals(usdjpy.SecurityIDSource, "8", StringComparison.Ordinal));
        var audusdMisclassified = results.Any(x => string.Equals(x.Symbol, "AUDUSD", StringComparison.OrdinalIgnoreCase) &&
                                                   string.Equals(x.QuantityRuleStatus, "Unsupported", StringComparison.OrdinalIgnoreCase));

        if (plannedCount > maxSandboxOrderCount) reasons.Add("MaxSandboxOrderCountExceeded");
        if (submitted.Any(x => x.CandidateQuantity > maxOrderQuantityPerSymbol)) reasons.Add("MaxOrderQuantityPerSymbolExceeded");
        if (plannedTotal > maxTotalSandboxQuantity) reasons.Add("MaxTotalSandboxQuantityExceeded");
        if (duplicateSymbols.Length > 0) reasons.Add("MoreThanOneOrderPerSymbol");
        if (unsupportedSubmitted) reasons.Add("UnsupportedSymbolSubmitted");
        if (directCrossAllowed) reasons.Add("DirectCrossExecutionAllowed");
        if (nonWhitelistedAllowed) reasons.Add("NonWhitelistedSymbolAllowed");
        if (legacyAccepted) reasons.Add("CanonicalQuarterHourTargetCloseRequired");
        if (!usdjpyPreserved) reasons.Add("USDJPYCaveatRequired");
        if (audusdMisclassified) reasons.Add("AUDUSDMisclassified");

        return new R009SandboxQuantityCalibrationPlanValidation(
            Status: reasons.Count == 0 ? R009SandboxOrderPathStatus.Ready : R009SandboxOrderPathStatus.Blocked,
            MaxSandboxOrderCount: maxSandboxOrderCount,
            MaxOrderQuantityPerSymbol: maxOrderQuantityPerSymbol,
            MaxTotalSandboxQuantity: maxTotalSandboxQuantity,
            PlannedOrderCount: plannedCount,
            PlannedTotalQuantity: plannedTotal,
            OneOrderPerSymbol: duplicateSymbols.Length == 0,
            UnsupportedSymbolSubmitted: unsupportedSubmitted,
            DirectCrossExecutionAllowed: directCrossAllowed,
            NonWhitelistedSymbolAllowed: nonWhitelistedAllowed,
            Legacy06AcceptedAsFutureCanonical: legacyAccepted,
            UsdjpyCaveatPreserved: usdjpyPreserved,
            AudusdMisclassified: audusdMisclassified,
            Reasons: reasons.ToArray());
    }

    public R009SandboxR007FillReview ReviewR007SandboxFills(
        IReadOnlyList<R009SandboxPerSymbolQuantityCalibrationResult> r007Results)
    {
        var reasons = new List<string>();
        var filled = r007Results.Where(x => x.FillCount > 0).ToArray();
        var symbols = filled.Select(x => x.Symbol).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var allWhitelisted = symbols.All(x => WhitelistedSymbols.Contains(x, StringComparer.OrdinalIgnoreCase));
        var sevenWhitelisted = symbols.Length == WhitelistedSymbols.Count && allWhitelisted;
        var quantityPointOne = filled.All(x => x.CandidateQuantity == 0.1m);
        var sandboxOnly = filled.All(x => x.SandboxOnly);
        var productionDetected = filled.Any(x => x.ProductionOrderCreated || x.ProductionRouteCreated || x.ProductionLedgerMutation);

        if (!sevenWhitelisted) reasons.Add("ExpectedSevenWhitelistedFills");
        if (!quantityPointOne) reasons.Add("ExpectedQuantityPointOnePerSymbol");
        if (!sandboxOnly) reasons.Add("ExpectedSandboxOnlyFills");
        if (productionDetected) reasons.Add("ProductionArtifactDetected");

        return new R009SandboxR007FillReview(
            Status: reasons.Count == 0 ? R009SandboxOrderPathStatus.Ready : R009SandboxOrderPathStatus.Blocked,
            FillCount: filled.Length,
            TotalFilledQuantity: filled.Sum(x => x.CandidateQuantity),
            Symbols: symbols,
            SevenWhitelistedSymbolsFilled: sevenWhitelisted,
            QuantityPointOnePerSymbol: quantityPointOne,
            SandboxOnly: sandboxOnly,
            ProductionArtifactDetected: productionDetected,
            Reasons: reasons.ToArray());
    }

    public R009SandboxPositionReconciliation BuildFillReportDerivedPositionReconciliation(
        IReadOnlyList<R009SandboxPerSymbolQuantityCalibrationResult> r007Results)
    {
        var lines = r007Results
            .Where(x => x.FillCount > 0)
            .Select(x => new R009SandboxPositionReconciliationLine(
                Symbol: x.Symbol,
                SourceSide: "Buy",
                R007FilledQuantity: x.CandidateQuantity,
                SignedPositionQuantity: x.CandidateQuantity,
                PositionSource: "FillReportDerived",
                SandboxOnly: x.SandboxOnly))
            .ToArray();

        var reasons = new List<string>();
        if (lines.Length == 0) reasons.Add("NoR007FillDerivedPositions");
        if (lines.Any(x => !WhitelistedSymbols.Contains(x.Symbol, StringComparer.OrdinalIgnoreCase))) reasons.Add("NonWhitelistedPositionDetected");
        if (lines.Any(x => !x.SandboxOnly)) reasons.Add("NonSandboxPositionDetected");

        return new R009SandboxPositionReconciliation(
            Status: reasons.Count == 0 ? R009SandboxOrderPathStatus.Ready : R009SandboxOrderPathStatus.Blocked,
            PositionSource: "FillReportDerived",
            Lines: lines,
            GrossOpenQuantity: lines.Sum(x => Math.Abs(x.SignedPositionQuantity)),
            ProductionPositionQueryUsed: false,
            Reasons: reasons.ToArray());
    }

    public R009SandboxFlattenOrderPlan PlanSandboxFlattenOrders(
        IReadOnlyList<R009SandboxPositionReconciliationLine> positions,
        IReadOnlyDictionary<string, (string SecurityId, bool RequiresInversion, string NormalizedPortfolioSymbol)> symbolMetadata)
    {
        var reasons = new List<string>();
        var lines = new List<R009SandboxFlattenOrderPlanLine>();
        foreach (var position in positions.Where(x => x.SignedPositionQuantity != 0m))
        {
            if (!WhitelistedSymbols.Contains(position.Symbol, StringComparer.OrdinalIgnoreCase))
            {
                reasons.Add("NonWhitelistedSymbolInFlattenPlan");
                continue;
            }

            if (!symbolMetadata.TryGetValue(position.Symbol, out var metadata))
            {
                reasons.Add($"MissingSymbolMetadata:{position.Symbol}");
                continue;
            }

            lines.Add(new R009SandboxFlattenOrderPlanLine(
                Symbol: position.Symbol,
                FlattenSide: position.SignedPositionQuantity > 0m ? "Sell" : "Buy",
                FlattenQuantity: Math.Abs(position.SignedPositionQuantity),
                SecurityID: metadata.SecurityId,
                SecurityIDSource: "8",
                RequiresInversion: metadata.RequiresInversion,
                NormalizedPortfolioSymbol: metadata.NormalizedPortfolioSymbol,
                SandboxOnly: true,
                ProductionOrder: false));
        }

        var duplicateSymbols = lines.GroupBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1);
        var directCrossAllowed = lines.Any(x => !WhitelistedSymbols.Contains(x.Symbol, StringComparer.OrdinalIgnoreCase));
        if (duplicateSymbols) reasons.Add("MoreThanOneFlattenOrderPerSymbol");
        if (directCrossAllowed) reasons.Add("DirectCrossExecutionAllowed");
        if (lines.Any(x => x.FlattenQuantity > 0.1m)) reasons.Add("FlattenQuantityExceedsPerSymbolCap");
        if (lines.Any(x => !x.SandboxOnly || x.ProductionOrder)) reasons.Add("ProductionOrderForbidden");

        return new R009SandboxFlattenOrderPlan(
            Status: reasons.Count == 0 ? R009SandboxOrderPathStatus.Ready : R009SandboxOrderPathStatus.Blocked,
            Lines: lines,
            PlannedOrderCount: lines.Count,
            PlannedTotalQuantity: lines.Sum(x => x.FlattenQuantity),
            OneFlattenOrderPerOpenPosition: !duplicateSymbols && lines.Count == positions.Count(x => x.SignedPositionQuantity != 0m),
            DirectCrossExecutionAllowed: directCrossAllowed,
            NonWhitelistedSymbolAllowed: directCrossAllowed,
            Reasons: reasons.ToArray());
    }

    public R009SandboxFlattenGuardrailValidation ValidateSandboxFlattenGuardrails(
        R009SandboxFlattenOrderPlan plan,
        int maxSandboxFlattenOrderCount,
        decimal maxFlattenQuantityPerSymbol,
        decimal maxTotalFlattenQuantity,
        bool sandboxProfileConfirmed,
        bool credentialValuesRedacted,
        bool productionRouteBlocked,
        bool productionLedgerBlocked,
        bool schedulerBlocked,
        DateTimeOffset canonicalTargetCloseUtc)
    {
        var reasons = new List<string>();
        var usdjpy = plan.Lines.FirstOrDefault(x => string.Equals(x.Symbol, "USDJPY", StringComparison.OrdinalIgnoreCase));
        var usdjpyPreserved = usdjpy is null ||
                               (string.Equals(usdjpy.NormalizedPortfolioSymbol, "JPYUSD", StringComparison.OrdinalIgnoreCase) &&
                                usdjpy.RequiresInversion &&
                                string.Equals(usdjpy.SecurityID, "4004", StringComparison.Ordinal) &&
                                string.Equals(usdjpy.SecurityIDSource, "8", StringComparison.Ordinal));
        var audusdMisclassified = plan.Lines.Any(x => string.Equals(x.Symbol, "AUDUSD", StringComparison.OrdinalIgnoreCase) && x.ProductionOrder);

        if (plan.Status == R009SandboxOrderPathStatus.Blocked) reasons.AddRange(plan.Reasons);
        if (plan.PlannedOrderCount > maxSandboxFlattenOrderCount) reasons.Add("MaxSandboxFlattenOrderCountExceeded");
        if (plan.Lines.Any(x => x.FlattenQuantity > maxFlattenQuantityPerSymbol)) reasons.Add("MaxFlattenQuantityPerSymbolExceeded");
        if (plan.PlannedTotalQuantity > maxTotalFlattenQuantity) reasons.Add("MaxTotalFlattenQuantityExceeded");
        if (!sandboxProfileConfirmed) reasons.Add("SandboxProfileRequired");
        if (!credentialValuesRedacted) reasons.Add("CredentialValuesMustBeRedacted");
        if (!productionRouteBlocked) reasons.Add("ProductionRouteMustRemainBlocked");
        if (!productionLedgerBlocked) reasons.Add("ProductionLedgerMustRemainBlocked");
        if (!schedulerBlocked) reasons.Add("SchedulerMustRemainBlocked");
        if (!IsCanonicalQuarterHour(canonicalTargetCloseUtc)) reasons.Add("CanonicalQuarterHourTargetCloseRequired");
        if (!usdjpyPreserved) reasons.Add("USDJPYCaveatRequired");
        if (audusdMisclassified) reasons.Add("AUDUSDMisclassified");

        return new R009SandboxFlattenGuardrailValidation(
            Status: reasons.Count == 0 ? R009SandboxOrderPathStatus.Ready : R009SandboxOrderPathStatus.Blocked,
            MaxSandboxFlattenOrderCount: maxSandboxFlattenOrderCount,
            MaxFlattenQuantityPerSymbol: maxFlattenQuantityPerSymbol,
            MaxTotalFlattenQuantity: maxTotalFlattenQuantity,
            PlannedOrderCount: plan.PlannedOrderCount,
            PlannedTotalQuantity: plan.PlannedTotalQuantity,
            SandboxProfileConfirmed: sandboxProfileConfirmed,
            CredentialValuesRedacted: credentialValuesRedacted,
            ProductionRouteBlocked: productionRouteBlocked,
            ProductionLedgerBlocked: productionLedgerBlocked,
            SchedulerBlocked: schedulerBlocked,
            CanonicalTimestampPolicyPreserved: IsCanonicalQuarterHour(canonicalTargetCloseUtc),
            UsdjpyCaveatPreserved: usdjpyPreserved,
            AudusdMisclassified: audusdMisclassified,
            Reasons: reasons.ToArray());
    }

    public R009SandboxPostFlattenReconciliation ReconcilePostFlatten(
        R009SandboxPositionReconciliation preFlatten,
        IReadOnlyList<R009SandboxPerSymbolQuantityCalibrationResult> flattenResults)
    {
        var filledFlattenQuantity = flattenResults.Where(x => x.FillCount > 0).Sum(x => x.CandidateQuantity);
        var expectedResidual = preFlatten.GrossOpenQuantity - filledFlattenQuantity;
        var residualDiagnostics = new List<string>();
        if (expectedResidual != 0m) residualDiagnostics.Add("ResidualQuantityRemaining");
        if (flattenResults.Any(x => x.ProductionOrderCreated || x.ProductionRouteCreated || x.ProductionLedgerMutation)) residualDiagnostics.Add("ProductionMutationDetected");

        return new R009SandboxPostFlattenReconciliation(
            Status: residualDiagnostics.Count == 0 ? R009SandboxOrderPathStatus.Ready : R009SandboxOrderPathStatus.Blocked,
            R007OpenPositionCount: preFlatten.Lines.Count,
            FlattenSubmittedCount: flattenResults.Count(x => x.Submitted),
            FlattenFilledCount: flattenResults.Count(x => x.FillCount > 0),
            ExpectedResidualQuantity: expectedResidual,
            FlatByFillReportDerivedAudit: expectedResidual == 0m,
            ProductionMutationDetected: flattenResults.Any(x => x.ProductionOrderCreated || x.ProductionRouteCreated || x.ProductionLedgerMutation),
            ResidualDiagnostics: residualDiagnostics.ToArray());
    }

    public R009SandboxOmsStateModel BuildSandboxOmsStateModel()
    {
        R009SandboxOmsStateTransition T(
            R009SandboxOmsState from,
            R009SandboxOmsState to,
            string trigger,
            bool reconciliation = false,
            bool idempotency = false)
            => new(
                From: from,
                To: to,
                Trigger: trigger,
                SandboxOrderState: true,
                ProductionOrderStateForbidden: true,
                LedgerStateForbidden: true,
                ReconciliationState: reconciliation,
                IdempotencyState: idempotency);

        var transitions = new[]
        {
            T(R009SandboxOmsState.SandboxIntentCreated, R009SandboxOmsState.SandboxRiskChecked, "SandboxPreTradeRiskPassed", idempotency: true),
            T(R009SandboxOmsState.SandboxRiskChecked, R009SandboxOmsState.SandboxRouteCreated, "SandboxRouteAllowed", idempotency: true),
            T(R009SandboxOmsState.SandboxRouteCreated, R009SandboxOmsState.SandboxSubmitted, "SandboxNewOrderSingleSentAfterLogonConfirmed", idempotency: true),
            T(R009SandboxOmsState.SandboxSubmitted, R009SandboxOmsState.SandboxAcked, "SandboxExecutionReportAck"),
            T(R009SandboxOmsState.SandboxSubmitted, R009SandboxOmsState.SandboxRejected, "SandboxExecutionReportReject"),
            T(R009SandboxOmsState.SandboxAcked, R009SandboxOmsState.SandboxPartiallyFilled, "SandboxPartialFill"),
            T(R009SandboxOmsState.SandboxAcked, R009SandboxOmsState.SandboxFilled, "SandboxFullFill"),
            T(R009SandboxOmsState.SandboxFilled, R009SandboxOmsState.SandboxFlattenIntentCreated, "SandboxPositionOpenForFlatten", reconciliation: true),
            T(R009SandboxOmsState.SandboxFlattenIntentCreated, R009SandboxOmsState.SandboxFlattenSubmitted, "SandboxFlattenNewOrderSingleSentAfterLogonConfirmed", idempotency: true),
            T(R009SandboxOmsState.SandboxFlattenSubmitted, R009SandboxOmsState.SandboxFlattenFilled, "SandboxFlattenFill"),
            T(R009SandboxOmsState.SandboxFlattenFilled, R009SandboxOmsState.SandboxFlatConfirmed, "FillReportDerivedResidualZero", reconciliation: true),
            T(R009SandboxOmsState.SandboxFlattenFilled, R009SandboxOmsState.SandboxResidualDetected, "FillReportDerivedResidualNonZero", reconciliation: true),
            T(R009SandboxOmsState.SandboxFlatConfirmed, R009SandboxOmsState.SandboxTerminal, "SandboxLifecycleTerminal", reconciliation: true),
            T(R009SandboxOmsState.SandboxRejected, R009SandboxOmsState.SandboxTerminal, "SandboxRejectTerminal"),
            T(R009SandboxOmsState.SandboxCancelled, R009SandboxOmsState.SandboxTerminal, "SandboxCancelTerminal")
        };

        return new R009SandboxOmsStateModel(
            Transitions: transitions,
            ProductionOrderStateForbidden: true,
            ProductionLedgerStateForbidden: true,
            SupportsReconciliationState: true,
            SupportsIdempotencyState: true);
    }

    public R009SandboxIdempotencyContract BuildSandboxIdempotencyContract(
        string sandboxOrderIntentId,
        string sandboxRouteId,
        string sandboxSubmissionId,
        string clOrdId)
    {
        var idempotencyKey = string.Join("|", sandboxOrderIntentId, sandboxRouteId, sandboxSubmissionId, clOrdId);
        return new R009SandboxIdempotencyContract(
            SandboxOrderIntentId: sandboxOrderIntentId,
            SandboxRouteId: sandboxRouteId,
            SandboxSubmissionId: sandboxSubmissionId,
            ClOrdId: clOrdId,
            IdempotencyKey: idempotencyKey,
            DuplicateClOrdIdRejected: true,
            SameIntentReplaySafe: true,
            SameIntentDifferentQuantityConflict: true,
            AlreadyFlattenedPositionRequiresExplicitNewSandboxApproval: true,
            NoProductionOrderFallback: true);
    }

    public R009SandboxDuplicatePreventionResult ValidateDuplicatePrevention(
        R009SandboxIdempotencyContract contract,
        bool duplicateClOrdIdAttempted,
        bool sameIntentReplay,
        bool sameIntentDifferentQuantity,
        bool alreadyFlattenedReplayAttempted,
        bool explicitNewSandboxApprovalForSecondFlatten,
        bool productionOrderFallback)
    {
        var reasons = new List<string>();
        var duplicateRejected = !duplicateClOrdIdAttempted || contract.DuplicateClOrdIdRejected;
        var replaySafe = !sameIntentReplay || contract.SameIntentReplaySafe;
        var quantityConflict = !sameIntentDifferentQuantity || contract.SameIntentDifferentQuantityConflict;
        var alreadyFlattenedBlocked = !alreadyFlattenedReplayAttempted ||
                                      (contract.AlreadyFlattenedPositionRequiresExplicitNewSandboxApproval && !explicitNewSandboxApprovalForSecondFlatten);
        var noDuplicateSubmission = duplicateRejected && replaySafe;
        var noFallback = !productionOrderFallback && contract.NoProductionOrderFallback;

        if (!duplicateRejected) reasons.Add("DuplicateClOrdIdAllowed");
        if (!replaySafe) reasons.Add("SameIntentReplayNotSafe");
        if (!quantityConflict) reasons.Add("SameIntentDifferentQuantityNotConflict");
        if (!alreadyFlattenedBlocked) reasons.Add("AlreadyFlattenedReplayAllowedWithoutApproval");
        if (!noDuplicateSubmission) reasons.Add("DuplicateSubmissionForIdempotencyKeyAllowed");
        if (!noFallback) reasons.Add("ProductionOrderFallbackAllowed");

        return new R009SandboxDuplicatePreventionResult(
            Status: reasons.Count == 0 ? R009SandboxOrderPathStatus.Ready : R009SandboxOrderPathStatus.Blocked,
            DuplicateClOrdIdRejected: duplicateRejected,
            SameIntentReplaySafe: replaySafe,
            SameIntentDifferentQuantityConflict: quantityConflict,
            AlreadyFlattenedReplayBlocked: alreadyFlattenedBlocked,
            NoDuplicateSubmissionForSameIdempotencyKey: noDuplicateSubmission,
            NoProductionOrderFallback: noFallback,
            Reasons: reasons.ToArray());
    }

    public R009SandboxPaperLedgerSeparationContract BuildSandboxPaperLedgerSeparationContract()
    {
        return new R009SandboxPaperLedgerSeparationContract(
            PaperLedgerCommitAllowed: false,
            ProductionLedgerCommitAllowed: false,
            TradingStateMutationAllowed: false,
            SandboxFillCanBeReferencedForReview: true,
            SandboxFillCanMutateLedger: false,
            SandboxFillCanMutateProductionState: false,
            PaperLedgerPreviewOnlyPreserved: true,
            BoundaryStatement: "Sandbox order/fill lifecycle evidence is review evidence only; it is not a paper-ledger commit, production-ledger commit, or trading-state mutation.");
    }

    public R009SandboxOmsHandoffContract BuildSandboxOmsHandoffContract(
        R009SandboxOmsStateModel model,
        bool sandboxLifecycleAccepted)
    {
        var reasons = new List<string>();
        if (!sandboxLifecycleAccepted) reasons.Add("SandboxLifecycleAcceptanceRequired");
        if (!model.ProductionOrderStateForbidden) reasons.Add("ProductionOrderStateMustRemainForbidden");
        if (!model.ProductionLedgerStateForbidden) reasons.Add("ProductionLedgerStateMustRemainForbidden");
        if (!model.SupportsReconciliationState) reasons.Add("ReconciliationStateRequired");
        if (!model.SupportsIdempotencyState) reasons.Add("IdempotencyStateRequired");

        return new R009SandboxOmsHandoffContract(
            Status: reasons.Count == 0 ? R009SandboxOrderPathStatus.Ready : R009SandboxOrderPathStatus.Blocked,
            AllowedTransitions: model.Transitions,
            ForbiddenTransitions: BuildForbiddenSandboxOmsTransitions(),
            SandboxLifecycleAccepted: sandboxLifecycleAccepted,
            ProductionOmsStateMutationAllowed: false,
            PaperLedgerCommitAllowed: false,
            ProductionLedgerCommitAllowed: false,
            TradingStateMutationAllowed: false,
            Reasons: reasons.ToArray());
    }

    public R009SandboxStateTransitionMap BuildSandboxStateTransitionMap(R009SandboxOmsStateModel model)
    {
        var evidenceByState = new Dictionary<R009SandboxOmsState, string>
        {
            [R009SandboxOmsState.SandboxIntentCreated] = "EXEC-SANDBOX-R009 repeatability open/flatten intents",
            [R009SandboxOmsState.SandboxRiskChecked] = "EXEC-SANDBOX-R009 repeatability guardrail validation",
            [R009SandboxOmsState.SandboxRouteCreated] = "EXEC-SANDBOX-R007/R008/R009 sandbox route artifacts",
            [R009SandboxOmsState.SandboxSubmitted] = "EXEC-SANDBOX-R007/R008/R009 sandbox submission artifacts",
            [R009SandboxOmsState.SandboxAcked] = "EXEC-SANDBOX-R007/R008/R009 accepted/acked reports",
            [R009SandboxOmsState.SandboxRejected] = "EXEC-SANDBOX-R004/R005 rejection evidence",
            [R009SandboxOmsState.SandboxPartiallyFilled] = "State reserved; no partial fill observed in R007/R008/R009",
            [R009SandboxOmsState.SandboxFilled] = "EXEC-SANDBOX-R007/R009 open fill reports",
            [R009SandboxOmsState.SandboxFlattenIntentCreated] = "EXEC-SANDBOX-R008/R009 flatten intents",
            [R009SandboxOmsState.SandboxFlattenSubmitted] = "EXEC-SANDBOX-R008/R009 flatten submission artifacts",
            [R009SandboxOmsState.SandboxFlattenFilled] = "EXEC-SANDBOX-R008/R009 flatten fill reports",
            [R009SandboxOmsState.SandboxFlatConfirmed] = "EXEC-SANDBOX-R008/R009 residual quantity 0.0",
            [R009SandboxOmsState.SandboxResidualDetected] = "State reserved for residual diagnostics",
            [R009SandboxOmsState.SandboxCancelled] = "State reserved; cancellation not used in R010 handoff",
            [R009SandboxOmsState.SandboxTerminal] = "EXEC-SANDBOX-R009 lifecycle decision"
        };

        return new R009SandboxStateTransitionMap(
            EvidencePhases: new[] { "EXEC-SANDBOX-R007", "EXEC-SANDBOX-R008", "EXEC-SANDBOX-R009" },
            EvidenceByState: evidenceByState,
            AllowedTransitions: model.Transitions,
            ForbiddenTransitions: BuildForbiddenSandboxOmsTransitions(),
            ProductionStateForbidden: true,
            LedgerStateForbidden: true);
    }

    public R009SandboxDuplicatePreventionHandoff BuildDuplicatePreventionHandoff(
        R009SandboxDuplicatePreventionResult result)
    {
        var reasons = new List<string>();
        if (!result.DuplicateClOrdIdRejected) reasons.Add("DuplicateClOrdIdPreventionWeakened");
        if (!result.SameIntentReplaySafe) reasons.Add("SameIntentReplayUnsafe");
        if (!result.SameIntentDifferentQuantityConflict) reasons.Add("SameIntentDifferentQuantityConflictMissing");
        if (!result.AlreadyFlattenedReplayBlocked) reasons.Add("AlreadyFlattenedProtectionWeakened");
        if (!result.NoDuplicateSubmissionForSameIdempotencyKey) reasons.Add("DuplicateSubmissionAllowed");
        if (!result.NoProductionOrderFallback) reasons.Add("ProductionOrderFallbackAllowed");

        return new R009SandboxDuplicatePreventionHandoff(
            Status: reasons.Count == 0 ? R009SandboxOrderPathStatus.Ready : R009SandboxOrderPathStatus.Blocked,
            DuplicateClOrdIdPreventionPreserved: result.DuplicateClOrdIdRejected,
            SameIntentReplaySafe: result.SameIntentReplaySafe,
            SameIntentDifferentQuantityConflict: result.SameIntentDifferentQuantityConflict,
            AlreadyFlattenedProtectionPreserved: result.AlreadyFlattenedReplayBlocked,
            NoDuplicateSubmissionForSameIdempotencyKey: result.NoDuplicateSubmissionForSameIdempotencyKey,
            NoProductionOrderFallback: result.NoProductionOrderFallback,
            Reasons: reasons.ToArray());
    }

    private static IReadOnlyList<string> BuildForbiddenSandboxOmsTransitions()
    {
        return new[]
        {
            "SandboxFillToPaperLedgerCommit",
            "SandboxFillToProductionLedgerCommit",
            "SandboxFillToProductionTradingStateMutation",
            "SandboxRouteToProductionRoute",
            "SandboxOrderToProductionOrder",
            "SandboxFlatStateAuditToLedgerMutation",
            "SandboxTerminalToLiveProductionPromotion"
        };
    }

    public R009SandboxOrderIntentResult TryCreateSandboxOrderIntent(
        R009SandboxExecutionIntent intent,
        R009LmaxSandboxConfigDiscovery discovery,
        R009SandboxGuardrailContract guardrail,
        int requestedOrderCount)
    {
        var risk = ValidatePreTrade(intent, discovery, guardrail, requestedOrderCount);
        if (risk.Status == R009SandboxOrderPathStatus.Blocked)
        {
            return BlockedResult(intent, risk);
        }

        var order = new R009SandboxOrderIntent(
            SandboxOrderIntentId: $"{intent.ExecutionIntentId}:lmax-sandbox-order-intent",
            ExecutionIntentId: intent.ExecutionIntentId,
            Symbol: intent.Symbol,
            ExecutionTradableSymbol: intent.ExecutionTradableSymbol,
            NormalizedPortfolioSymbol: intent.NormalizedPortfolioSymbol,
            RequiresInversion: intent.RequiresInversion,
            SecurityID: intent.SecurityID,
            SecurityIDSource: intent.SecurityIDSource,
            Side: intent.Side,
            Quantity: intent.TargetQuantity,
            Notional: intent.TargetNotional,
            CanonicalTargetCloseUtc: intent.CanonicalTargetCloseUtc,
            BrokerVenue: guardrail.BrokerVenue,
            SandboxOnly: true,
            ProductionOrder: false,
            IsLiveProduction: false,
            NoProductionLedgerCommit: true,
            IdempotentSubmissionRequired: true);

        var route = new R009SandboxRoute(
            RouteId: $"{order.SandboxOrderIntentId}:route",
            SandboxOrderIntentId: order.SandboxOrderIntentId,
            BrokerVenue: guardrail.BrokerVenue,
            Environment: guardrail.Environment,
            SandboxOnly: true,
            ProductionRoute: false,
            NonSandboxBrokerRoute: false,
            ProductionCredentialsUsed: false);

        var submission = new R009SandboxSubmission(
            SubmissionId: $"{route.RouteId}:submission",
            RouteId: route.RouteId,
            Status: R009SandboxSubmissionStatus.NotSubmittedBlocked,
            SandboxOnly: true,
            ProductionSubmission: false,
            SubmittedOrderCount: 0,
            SubmittedNotional: 0m,
            AckOrRejectReason: "SubmissionRequiresExplicitRuntimeSandboxConfigAndOperatorExecutionStep");

        var reconciliation = new R009SandboxReconciliationResult(
            ReconciliationId: $"{submission.SubmissionId}:reconciliation",
            SandboxOrderIntentId: order.SandboxOrderIntentId,
            SubmissionId: submission.SubmissionId,
            IntendedMatchesSubmitted: false,
            AckOrRejectCaptured: false,
            ExecutionReportCaptured: false,
            FillCaptured: false,
            ProductionLedgerMutation: false,
            ProductionStateMutation: false,
            SandboxOnly: true);

        return new R009SandboxOrderIntentResult(risk, order, route, submission, reconciliation);
    }

    public R011PmsPaperR015SandboxMappingResult MapPmsPaperR015LineToSandboxIntent(
        R011PmsPaperR015SourceLine sourceLine,
        DateTimeOffset canonicalTargetCloseUtc,
        decimal sandboxQuantity,
        string r009ContractVersion,
        string brokerVenue,
        string environment)
    {
        var side = DeriveR011Side(sourceLine);
        var mapping = MapR011BrokerSymbol(sourceLine.Symbol);
        var reasons = new List<string>();
        reasons.AddRange(side.Reasons);
        reasons.AddRange(mapping.Reasons);

        if (!IsCanonicalQuarterHour(canonicalTargetCloseUtc)) reasons.Add("CanonicalQuarterHourTargetCloseRequired");
        if (sandboxQuantity != 0.1m) reasons.Add("SandboxQuantityMustEqualValidatedPointOne");
        if (!string.Equals(brokerVenue, "ExistingLmaxDemoProfile", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(brokerVenue, "LMAXSandbox", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("BrokerVenueMustBeExistingLmaxDemoProfileOrLMAXSandbox");
        }

        if (!string.Equals(environment, "Demo", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(environment, "Sandbox", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("EnvironmentMustBeDemoOrSandbox");
        }

        if (string.IsNullOrWhiteSpace(r009ContractVersion)) reasons.Add("R009ContractVersionMissing");

        var idempotencyKey = $"exec-sandbox-r011|{sourceLine.CycleRunId}|{sourceLine.IntentPreviewId}|{sourceLine.Symbol}|{canonicalTargetCloseUtc:yyyyMMddHHmm}|{sandboxQuantity:0.########}";
        var status = reasons.Count == 0 ? R009SandboxOrderPathStatus.Ready : R009SandboxOrderPathStatus.Blocked;
        var completedFields = new[]
        {
            "R009ContractVersion",
            "SelectedPolicies",
            "SandboxAccountProfile",
            "BrokerSymbol",
            "ExecutionTradableSymbol",
            "NormalizedPortfolioSymbol",
            "RequiresInversion",
            "SandboxQuantityRule",
            "CanonicalTargetCloseUtc",
            "IdempotencyKey",
            "SandboxOnly",
            "ProductionAllowed=false",
            "PaperLedgerCommitAllowed=false"
        };

        var missing = reasons
            .Where(x => x.Contains("Missing", StringComparison.OrdinalIgnoreCase) || x.Contains("Required", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var completion = new R011ExecAlgoFieldCompletion(
            Status: status,
            R009ContractVersion: r009ContractVersion,
            SelectedPolicies:
            [
                "CloseSeeking15mAdaptive_BalancedAdaptive_v0",
                "CloseSeeking15mAdaptive_ResidualAwareUrgency_v0",
                "ControlledResidualCross_BalancedResidualCross_v0"
            ],
            BrokerVenue: brokerVenue,
            Environment: environment,
            SandboxQuantity: sandboxQuantity,
            CanonicalTargetCloseUtc: canonicalTargetCloseUtc,
            IdempotencyKey: idempotencyKey,
            SandboxOnly: true,
            ProductionAllowed: false,
            PaperLedgerCommitAllowed: false,
            CompletedFields: completedFields,
            MissingFields: missing,
            Reasons: reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());

        R009SandboxExecutionIntent? intent = null;
        if (status == R009SandboxOrderPathStatus.Ready)
        {
            intent = new R009SandboxExecutionIntent(
                ExecutionIntentId: $"{sourceLine.IntentPreviewId}:exec-sandbox-r011-r009-intent",
                SourceDecisionPreviewId: sourceLine.OmsPreviewStateId,
                Symbol: sourceLine.Symbol,
                ExecutionTradableSymbol: mapping.ExecutionTradableSymbol,
                NormalizedPortfolioSymbol: mapping.NormalizedPortfolioSymbol,
                RequiresInversion: mapping.RequiresInversion,
                SecurityID: mapping.SecurityID,
                SecurityIDSource: mapping.SecurityIDSource,
                Side: side.ExecutionSide,
                TargetQuantity: sandboxQuantity,
                TargetNotional: sandboxQuantity,
                CanonicalTargetCloseUtc: canonicalTargetCloseUtc,
                BarRole: "IntradayRebalance",
                ReadinessPresent: true,
                ReadinessWaivedForSandboxSmokeTest: false,
                OperatorSandboxApproval: true,
                KillSwitchOpenForSandboxOnly: true,
                R009DecisionStatus: "PreviewReady",
                SandboxOnly: true,
                ProductionOrder: false,
                IsLiveProduction: false,
                NoProductionLedgerCommit: true);
        }

        return new R011PmsPaperR015SandboxMappingResult(
            Status: status,
            SourceLine: sourceLine,
            SideDerivation: side,
            BrokerSymbolMapping: mapping,
            FieldCompletion: completion,
            ExecutionIntent: intent,
            Reasons: reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public R011SideDerivationEvidence DeriveR011Side(R011PmsPaperR015SourceLine sourceLine)
    {
        var reasons = new List<string>();
        var directionSide = sourceLine.Direction.Trim().ToUpperInvariant() switch
        {
            "INCREASE" => "Buy",
            "DECREASE" => "Sell",
            _ => ""
        };
        if (string.IsNullOrWhiteSpace(directionSide)) reasons.Add("MissingExecAlgoSide");

        var deltaSide = sourceLine.DeltaNotional switch
        {
            > 0m => "Buy",
            < 0m => "Sell",
            _ => ""
        };
        if (string.IsNullOrWhiteSpace(deltaSide)) reasons.Add("ZeroDeltaNotionalHasNoExecutableSide");
        if (!string.IsNullOrWhiteSpace(directionSide) &&
            !string.IsNullOrWhiteSpace(deltaSide) &&
            !string.Equals(directionSide, deltaSide, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("DirectionAndDeltaNotionalSideConflict");
        }

        var mapping = MapR011BrokerSymbol(sourceLine.Symbol);
        var portfolioSide = !string.IsNullOrWhiteSpace(directionSide) ? directionSide : deltaSide;
        var executionSide = mapping.RequiresInversion ? OppositeSide(portfolioSide) : portfolioSide;
        var status = reasons.Count == 0 ? R009SandboxOrderPathStatus.Ready : R009SandboxOrderPathStatus.Blocked;
        return new R011SideDerivationEvidence(
            Status: status,
            PortfolioSide: portfolioSide,
            ExecutionSide: executionSide,
            SourceDirection: sourceLine.Direction,
            SourceDeltaNotional: sourceLine.DeltaNotional,
            SourceField: "PMS-PAPER-R015 SourceDirections + SourceDeltaNotionals",
            RequiresInversion: mapping.RequiresInversion,
            Reasons: reasons);
    }

    public R011BrokerSymbolMapping MapR011BrokerSymbol(string symbol)
    {
        var normalized = symbol.ToUpperInvariant();
        var reasons = new List<string>();
        var known = R011SymbolMappings.TryGetValue(normalized, out var row);
        if (!known) reasons.Add("SymbolNotWhitelistedOrDirectCross");

        var executionSymbol = row?.ExecutionTradableSymbol ?? normalized;
        var portfolioSymbol = row?.NormalizedPortfolioSymbol ?? normalized;
        var requiresInversion = row?.RequiresInversion ?? false;
        var securityId = row?.SecurityID;
        var securityIdSource = row?.SecurityIDSource;
        var whitelisted = WhitelistedSymbols.Contains(executionSymbol, StringComparer.OrdinalIgnoreCase);
        if (!whitelisted) reasons.Add("SymbolNotWhitelistedOrDirectCross");

        var audusdMisclassified = string.Equals(normalized, "AUDUSD", StringComparison.OrdinalIgnoreCase) &&
            (!known || !string.Equals(executionSymbol, "AUDUSD", StringComparison.OrdinalIgnoreCase) || requiresInversion);
        if (audusdMisclassified) reasons.Add("AUDUSDMisclassified");

        var usdjpy = string.Equals(executionSymbol, "USDJPY", StringComparison.OrdinalIgnoreCase);
        var usdjpyCaveatPreserved = !usdjpy ||
            (string.Equals(portfolioSymbol, "JPYUSD", StringComparison.OrdinalIgnoreCase) &&
             requiresInversion &&
             string.Equals(securityId, "4004", StringComparison.Ordinal) &&
             string.Equals(securityIdSource, "8", StringComparison.Ordinal));
        if (!usdjpyCaveatPreserved) reasons.Add("USDJPYCaveatRequired");

        return new R011BrokerSymbolMapping(
            Status: reasons.Count == 0 ? R009SandboxOrderPathStatus.Ready : R009SandboxOrderPathStatus.Blocked,
            SourceSymbol: symbol,
            ExecutionTradableSymbol: executionSymbol,
            NormalizedPortfolioSymbol: portfolioSymbol,
            RequiresInversion: requiresInversion,
            SecurityID: securityId,
            SecurityIDSource: securityIdSource,
            Whitelisted: whitelisted,
            DirectCrossRejected: !whitelisted,
            AudusdMisclassified: audusdMisclassified,
            UsdjpyCaveatPreserved: usdjpyCaveatPreserved,
            Reasons: reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public R009PretradeSandboxRiskCheck ValidatePreTrade(
        R009SandboxExecutionIntent intent,
        R009LmaxSandboxConfigDiscovery discovery,
        R009SandboxGuardrailContract guardrail,
        int requestedOrderCount)
    {
        var reasons = new List<string>();
        var environmentIsSandbox = discovery.EnvironmentIsSandbox && string.Equals(guardrail.Environment, "Sandbox", StringComparison.OrdinalIgnoreCase);
        var brokerVenueIsSandbox = discovery.BrokerVenueIsLmaxSandbox && string.Equals(guardrail.BrokerVenue, "LMAXSandbox", StringComparison.OrdinalIgnoreCase);
        var sandboxCredentialsPresent = discovery.SandboxCredentialsRequired && discovery.SandboxCredentialProfilePresent && !discovery.ProductionCredentialsDetected;
        var symbolWhitelisted = WhitelistedSymbols.Contains(intent.ExecutionTradableSymbol, StringComparer.OrdinalIgnoreCase);
        var directCrossRejected = !symbolWhitelisted;
        var canonicalClose = IsCanonicalQuarterHour(intent.CanonicalTargetCloseUtc);
        var readinessPresentOrWaived = intent.ReadinessPresent || intent.ReadinessWaivedForSandboxSmokeTest;
        var maxOrderCountSatisfied = requestedOrderCount > 0 && requestedOrderCount <= guardrail.MaxSandboxOrderCount && requestedOrderCount <= discovery.MaxSandboxOrderCount;
        var maxNotionalSatisfied = intent.TargetNotional > 0m && intent.TargetNotional <= guardrail.MaxSandboxNotional && intent.TargetNotional <= discovery.MaxSandboxNotional;
        var noProductionRoute = !guardrail.ProductionVenueAllowed && !intent.ProductionOrder && !intent.IsLiveProduction;
        var noProductionLedger = intent.NoProductionLedgerCommit;
        var noScheduler = !discovery.SchedulerServicePollingBackgroundJobEnabled;

        if (!discovery.ReadyForSandboxSubmission) reasons.AddRange(discovery.Reasons);
        if (!environmentIsSandbox) reasons.Add("EnvironmentMustBeSandbox");
        if (!brokerVenueIsSandbox) reasons.Add("BrokerVenueMustBeLMAXSandbox");
        if (!sandboxCredentialsPresent) reasons.Add("SandboxCredentialsRequired");
        if (!symbolWhitelisted) reasons.Add("SymbolNotWhitelistedOrDirectCross");
        if (!canonicalClose) reasons.Add("CanonicalQuarterHourTargetCloseRequired");
        if (!readinessPresentOrWaived) reasons.Add("ReadinessRequiredOrExplicitSandboxWaiverRequired");
        if (!intent.OperatorSandboxApproval) reasons.Add("OperatorSandboxApprovalRequired");
        if (!maxOrderCountSatisfied) reasons.Add("MaxSandboxOrderCountExceeded");
        if (!maxNotionalSatisfied) reasons.Add("MaxSandboxNotionalExceeded");
        if (!intent.KillSwitchOpenForSandboxOnly) reasons.Add("KillSwitchMustBeOpenForSandboxOnly");
        if (!noProductionRoute) reasons.Add("ProductionRouteForbidden");
        if (!noProductionLedger) reasons.Add("ProductionLedgerCommitForbidden");
        if (!noScheduler) reasons.Add("SchedulerForbidden");
        if (!string.Equals(intent.R009DecisionStatus, "PreviewReady", StringComparison.OrdinalIgnoreCase)) reasons.Add("R009DecisionMustBePreviewReady");
        if (string.Equals(intent.ExecutionTradableSymbol, "USDJPY", StringComparison.OrdinalIgnoreCase) && !ValidUsdjpyCaveat(intent)) reasons.Add("USDJPYCaveatRequired");

        var status = reasons.Count == 0 ? R009SandboxOrderPathStatus.Ready : R009SandboxOrderPathStatus.Blocked;
        return new R009PretradeSandboxRiskCheck(
            Status: status,
            EnvironmentIsSandbox: environmentIsSandbox,
            BrokerVenueIsLmaxSandbox: brokerVenueIsSandbox,
            SandboxCredentialsPresent: sandboxCredentialsPresent,
            SymbolWhitelisted: symbolWhitelisted,
            DirectCrossRejected: directCrossRejected,
            CanonicalQuarterHourTargetClose: canonicalClose,
            ReadinessPresentOrWaived: readinessPresentOrWaived,
            OperatorSandboxApprovalPresent: intent.OperatorSandboxApproval,
            MaxOrderCountSatisfied: maxOrderCountSatisfied,
            MaxNotionalSatisfied: maxNotionalSatisfied,
            KillSwitchOpenForSandboxOnly: intent.KillSwitchOpenForSandboxOnly,
            NoProductionRoute: noProductionRoute,
            NoProductionLedger: noProductionLedger,
            NoScheduler: noScheduler,
            Reasons: reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static R009SandboxOrderIntentResult BlockedResult(R009SandboxExecutionIntent intent, R009PretradeSandboxRiskCheck risk)
    {
        var submission = new R009SandboxSubmission(
            SubmissionId: $"{intent.ExecutionIntentId}:blocked-submission",
            RouteId: $"{intent.ExecutionIntentId}:blocked-route",
            Status: R009SandboxSubmissionStatus.NotSubmittedBlocked,
            SandboxOnly: true,
            ProductionSubmission: false,
            SubmittedOrderCount: 0,
            SubmittedNotional: 0m,
            AckOrRejectReason: string.Join(";", risk.Reasons));

        var reconciliation = new R009SandboxReconciliationResult(
            ReconciliationId: $"{submission.SubmissionId}:reconciliation",
            SandboxOrderIntentId: $"{intent.ExecutionIntentId}:blocked-order-intent",
            SubmissionId: submission.SubmissionId,
            IntendedMatchesSubmitted: false,
            AckOrRejectCaptured: false,
            ExecutionReportCaptured: false,
            FillCaptured: false,
            ProductionLedgerMutation: false,
            ProductionStateMutation: false,
            SandboxOnly: true);

        return new R009SandboxOrderIntentResult(risk, null, null, submission, reconciliation);
    }

    private static bool IsCanonicalQuarterHour(DateTimeOffset targetClose) =>
        targetClose.Second == 0 &&
        targetClose.Millisecond == 0 &&
        targetClose.Minute is 0 or 15 or 30 or 45;

    private static string OppositeSide(string side) =>
        string.Equals(side, "Buy", StringComparison.OrdinalIgnoreCase) ? "Sell" :
        string.Equals(side, "Sell", StringComparison.OrdinalIgnoreCase) ? "Buy" :
        side;

    private static bool ValidUsdjpyCaveat(R009SandboxExecutionIntent intent) =>
        string.Equals(intent.NormalizedPortfolioSymbol, "JPYUSD", StringComparison.OrdinalIgnoreCase) &&
        intent.RequiresInversion &&
        string.Equals(intent.SecurityID, "4004", StringComparison.Ordinal) &&
        string.Equals(intent.SecurityIDSource, "8", StringComparison.Ordinal);

    private static string? Get(IReadOnlyDictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out var value) ? value : null;

    private static bool ParseBool(string? value) =>
        bool.TryParse(value, out var parsed) && parsed;

    private static int ParseInt(string? value) =>
        int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

    private static decimal ParseDecimal(string? value) =>
        decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;

    private static bool ContainsProductionLabel(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (value.Contains("prod", StringComparison.OrdinalIgnoreCase) ||
         value.Contains("production", StringComparison.OrdinalIgnoreCase) ||
         value.Contains("live", StringComparison.OrdinalIgnoreCase));

    private sealed record R011SymbolMapping(
        string ExecutionTradableSymbol,
        string NormalizedPortfolioSymbol,
        bool RequiresInversion,
        string SecurityID,
        string SecurityIDSource);

    private static IReadOnlyDictionary<string, R011SymbolMapping> R011SymbolMappings { get; } =
        new Dictionary<string, R011SymbolMapping>(StringComparer.OrdinalIgnoreCase)
        {
            ["EURUSD"] = new("EURUSD", "EURUSD", false, "4001", "8"),
            ["AUDUSD"] = new("AUDUSD", "AUDUSD", false, "4007", "8"),
            ["GBPUSD"] = new("GBPUSD", "GBPUSD", false, "4002", "8"),
            ["NZDUSD"] = new("NZDUSD", "NZDUSD", false, "100613", "8"),
            ["USDCAD"] = new("USDCAD", "CADUSD", true, "4013", "8"),
            ["USDCHF"] = new("USDCHF", "CHFUSD", true, "4010", "8"),
            ["USDJPY"] = new("USDJPY", "JPYUSD", true, "4004", "8"),
            ["JPYUSD"] = new("USDJPY", "JPYUSD", true, "4004", "8")
        };
}
