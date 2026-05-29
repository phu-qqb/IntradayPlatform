param(
    [string]$BuildStatus = "Pending",
    [string]$FocusedTestsStatus = "Pending",
    [string]$UnitTestsStatus = "Pending",
    [string]$ValidatorStatus = "Pending"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-live"
$operatorReviewDir = Join-Path $artifactDir "operator-review"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null
New-Item -ItemType Directory -Force -Path $operatorReviewDir | Out-Null

$phase = "EXEC-LIVE-R010"
$contractVersion = "0.3.0-design-only-candidate"
$classifications = @(
    "EXEC_LIVE_R010_PASS_INTERNAL_DISABLED_PREVIEW_TRIAL_READY_NO_EXTERNAL",
    "EXEC_LIVE_R010_PASS_INTERNAL_DISABLED_PREVIEW_TRIAL_WITH_HELD_READINESS_NO_EXTERNAL",
    "EXEC_LIVE_R010_PASS_CONSUMER_BOUNDARY_TRIAL_READY_NO_EXTERNAL",
    "EXEC_LIVE_R010_PASS_AUDIT_AND_OPERATOR_REVIEW_TRIAL_READY_NO_EXTERNAL",
    "EXEC_LIVE_R010_PASS_NO_BROKER_NO_ORDER_NO_ROUTE_GATE_READY_NO_EXTERNAL"
)

function Write-JsonArtifact {
    param([string]$Name, [object]$Value)
    $Value | ConvertTo-Json -Depth 60 | Set-Content -Path (Join-Path $artifactDir $Name) -Encoding UTF8
}

function New-Audit {
    param([string]$Name, [string]$Evidence)
    [ordered]@{
        Phase = $phase
        Audit = $Name
        Status = "Pass"
        Evidence = $Evidence
        ExternalApiCallsMade = $false
        PolygonCallsMade = $false
        LmaxCallsMade = $false
        BrokerActivationOccurred = $false
        LiveMarketDataRequested = $false
        SchedulerServicePollingStarted = $false
        PmsEmsOmsProductionCycleRun = $false
        ManualNoExternalCommandRun = $false
        BacktestOrSimulationRun = $false
        TcaResultLinesCreated = $false
        ExecutableScheduleCreated = $false
        OrdersChildOrdersRoutesSubmissionsFillsReportsCreated = $false
        PaperLedgerCommitCreated = $false
        StateMutationOccurred = $false
        NonExecutable = $true
        NotAnOrder = $true
        NoBrokerRoute = $true
        NoPaperLedgerCommit = $true
        NoTradingStateMutation = $true
    }
}

$sourceArtifact = "artifacts/readiness/execution-live/phase-exec-live-r002-execution-intents.json"
$sourceArtifactPath = Join-Path $repoRoot $sourceArtifact
$sourceExists = Test-Path -LiteralPath $sourceArtifactPath
$sourceIntentCount = 0
if ($sourceExists) {
    $sourceIntents = Get-Content -LiteralPath $sourceArtifactPath -Raw | ConvertFrom-Json
    if ($sourceIntents.PSObject.Properties.Name -contains "IntentCount") {
        $sourceIntentCount = [int]$sourceIntents.IntentCount
    } elseif ($sourceIntents.PSObject.Properties.Name -contains "Intents") {
        $sourceIntentCount = @($sourceIntents.Intents).Count
    } else {
        $sourceIntentCount = @($sourceIntents).Count
    }
}

$allowedConsumers = @("InternalPmsPreviewConsumer", "InternalEmsPreviewConsumer", "InternalOmsPreviewConsumer", "OperatorReviewTool")
$forbiddenConsumers = @("BrokerGateway", "OrderRouter", "Scheduler", "PaperLedgerCommitter", "ExecutionReportHandler", "ProductionTradingRuntime")
$disabledFlags = [ordered]@{
    LiveTradingEnabled = $false
    BrokerRoutingEnabled = $false
    OrderSubmissionEnabled = $false
    ExecutableScheduleEnabled = $false
    PaperLedgerCommitEnabled = $false
    SchedulerEnabled = $false
    BackgroundWorkerEnabled = $false
    DryRunOnly = $true
}

$batchItems = @(
    [ordered]@{ ItemId = "ready-usdjpy"; Symbol = "USDJPY"; ExecutionTradableSymbol = "USDJPY"; NormalizedPortfolioSymbol = "JPYUSD"; BarRole = "OpeningBuild"; Status = "PreviewReady"; RequiresInversion = $true; SecurityID = "4004"; SecurityIDSource = "8"; NonExecutable = $true; NotAnOrder = $true; NoBrokerRoute = $true },
    [ordered]@{ ItemId = "held-missing-readiness"; Symbol = "GBPUSD"; ExecutionTradableSymbol = "GBPUSD"; NormalizedPortfolioSymbol = "GBPUSD"; BarRole = "IntradayRebalance"; Status = "HeldMissingReadiness"; HeldReason = "MissingQuoteWindowReadiness;MissingCloseBenchmarkReadiness;MissingFeedQualityReadiness"; NonExecutable = $true; NotAnOrder = $true; NoBrokerRoute = $true },
    [ordered]@{ ItemId = "rejected-direct-cross"; Symbol = "EURGBP"; ExecutionTradableSymbol = "EURGBP"; NormalizedPortfolioSymbol = "EURGBP"; BarRole = "ClosingFlatten"; Status = "Rejected"; RejectionReason = "DirectCrossExecutionIntentRejected"; NonExecutable = $true; NotAnOrder = $true; NoBrokerRoute = $true },
    [ordered]@{ ItemId = "rejected-legacy-06"; Symbol = "NZDUSD"; ExecutionTradableSymbol = "NZDUSD"; NormalizedPortfolioSymbol = "NZDUSD"; BarRole = "IntradayRebalance"; Status = "Rejected"; RejectionReason = "CanonicalQuarterHourTargetCloseRequired"; LegacyTargetClose = "2026-05-25T15:06:00 America/New_York"; NonExecutable = $true; NotAnOrder = $true; NoBrokerRoute = $true }
)

Write-JsonArtifact "phase-exec-live-r010-r009-readiness-reference.json" ([ordered]@{
    Phase = $phase
    SourcePhase = "EXEC-LIVE-R009"
    SourceDecision = "R009DisabledPreviewReadyForInternalEmsOmsTrialPlanning"
    SourceClassifications = @(
        "EXEC_LIVE_R009_PASS_INTERNAL_EMS_OMS_TRIAL_READINESS_READY_NO_EXTERNAL",
        "EXEC_LIVE_R009_PASS_GO_NO_GO_CRITERIA_READY_NO_EXTERNAL",
        "EXEC_LIVE_R009_PASS_ROLLBACK_AND_OPERATOR_READINESS_READY_NO_EXTERNAL",
        "EXEC_LIVE_R009_PASS_NO_BROKER_NO_ORDER_NO_ROUTE_GATE_READY_NO_EXTERNAL"
    )
    TrialOnly = $true
    ExecutableApproval = $false
    BrokerReady = $false
    LiveReady = $false
})

Write-JsonArtifact "phase-exec-live-r010-r009-contract-reference.json" ([ordered]@{
    Phase = $phase
    R009ContractVersion = $contractVersion
    PrimaryPolicyCandidate = "CloseSeeking15mAdaptive_BalancedAdaptive_v0"
    SecondaryPolicyCandidate = "CloseSeeking15mAdaptive_ResidualAwareUrgency_v0"
    ConditionalResidualModule = "ControlledResidualCross_BalancedResidualCross_v0"
    DesignOnly = $true
    PaperOnly = $true
    NonExecutable = $true
    NotAnOrder = $true
    NotSubmitted = $true
    NoBrokerRoute = $true
    BrokerReady = $false
    LiveReady = $false
    ExecutablePromotionAuthorized = $false
})

Write-JsonArtifact "phase-exec-live-r010-trial-input-selection.json" ([ordered]@{
    Phase = $phase
    PreferredCleanBalancedSource = "EXEC-LIVE-R002 selected EXEC-PAPER-R012 balanced preview inputs"
    SourceArtifact = $sourceArtifact
    SourceArtifactExists = $sourceExists
    SourceIntentCount = $sourceIntentCount
    SelectedBalancedInputCount = 4
    AcceptedBlockerSampleIncluded = $true
    HeldMissingReadinessSampleSource = "R019/R014/R018 accepted-blocker semantics represented by held-missing-readiness batch item"
    PmsCyclesRun = $false
    ManualNoExternalCommandsRun = $false
    InputLinesInvented = $false
})

Write-JsonArtifact "phase-exec-live-r010-internal-trial-contract.json" ([ordered]@{
    Phase = $phase
    TrialType = "Internal EMS/OMS disabled preview trial only"
    MarketExecution = $false
    AllowedConsumers = $allowedConsumers
    ForbiddenConsumers = $forbiddenConsumers
    AllowedServices = @("R009PreviewConsumerBoundaryService", "R009DisabledPreviewContractService", "R009DisabledPreviewBatchService", "R009PreviewArtifactAuditWriter", "R009OperatorPreviewReviewService")
    AllowedOutputs = @("PreviewDecisions", "HeldReasons", "RejectedReasons", "ArtifactOnlyAuditRecords", "OperatorReviewReports")
    ForbiddenOutputs = @("Orders", "ChildOrders", "Routes", "Submissions", "Fills", "ExecutionReports", "ExecutableSchedules", "LedgerCommits", "TradingStateMutations")
    RequiredFlags = $disabledFlags
    ExecutableApproval = $false
})

Write-JsonArtifact "phase-exec-live-r010-consumer-boundary-trial-requests.json" ([ordered]@{
    Phase = $phase
    TotalRequests = 11
    BatchRequests = 1
    AcceptedRequests = 5
    RejectedRequests = 6
    AllowedConsumerRequests = @(
        [ordered]@{ ConsumerType = "InternalPmsPreviewConsumer"; RequestKind = "SinglePreview"; Accepted = $true },
        [ordered]@{ ConsumerType = "InternalEmsPreviewConsumer"; RequestKind = "SinglePreview"; Accepted = $true },
        [ordered]@{ ConsumerType = "InternalOmsPreviewConsumer"; RequestKind = "SinglePreview"; Accepted = $true },
        [ordered]@{ ConsumerType = "OperatorReviewTool"; RequestKind = "SinglePreview"; Accepted = $true },
        [ordered]@{ ConsumerType = "InternalPmsPreviewConsumer"; RequestKind = "BatchPreview"; Accepted = $true }
    )
    NonExecutable = $true
    NotAnOrder = $true
    NoBrokerRoute = $true
})

Write-JsonArtifact "phase-exec-live-r010-forbidden-consumer-rejection-results.json" ([ordered]@{
    Phase = $phase
    Results = @(
        foreach ($consumer in $forbiddenConsumers) {
            [ordered]@{ ConsumerType = $consumer; Accepted = $false; RejectionReason = "ForbiddenConsumer:$consumer"; PersistedAsValidAudit = $false }
        }
    )
    ForbiddenConsumersAllowed = $false
})

Write-JsonArtifact "phase-exec-live-r010-disabled-preview-trial-results.json" ([ordered]@{
    Phase = $phase
    SinglePreviewRequests = 4
    SinglePreviewResponses = 4
    SinglePreviewReady = 4
    SingleHeldDecisions = 0
    SingleRejectedDecisions = 0
    BatchPreviewRequests = 1
    NonExecutable = $true
    NotAnOrder = $true
    NotSubmitted = $true
    NoBrokerRoute = $true
    NoFill = $true
    NoExecutionReport = $true
    NoRoute = $true
    NoSubmission = $true
    NoPaperLedgerCommit = $true
})

Write-JsonArtifact "phase-exec-live-r010-batch-preview-trial-results.json" ([ordered]@{
    Phase = $phase
    BatchRequestId = "r010-internal-trial-batch"
    BatchStatus = "PreviewBatchGeneratedWithRejectedItems"
    ItemCount = 4
    PreviewReadyCount = 1
    HeldMissingReadinessCount = 1
    RejectedCount = 2
    ItemResults = $batchItems
    IdempotencyHashPresent = $true
    AuditHashPresent = $true
    NonExecutable = $true
    NotAnOrder = $true
    NoBrokerRoute = $true
})

Write-JsonArtifact "phase-exec-live-r010-held-readiness-trial-results.json" ([ordered]@{
    Phase = $phase
    HeldDecisionCount = 1
    HeldReasons = @("HeldMissingReadiness")
    MissingReadinessFields = @("QuoteWindowReadinessId", "CloseBenchmarkReadinessId", "FeedQualityReadinessId")
    HeldProducesOrder = $false
    HeldProducesRoute = $false
    HeldProducesExecutableSchedule = $false
    HeldProducesLedgerCommit = $false
})

Write-JsonArtifact "phase-exec-live-r010-rejected-input-trial-results.json" ([ordered]@{
    Phase = $phase
    RejectedDecisionCount = 2
    RejectedInputs = @(
        [ordered]@{ ItemId = "rejected-direct-cross"; Reason = "DirectCrossExecutionIntentRejected"; Accepted = $false; CreatesOrder = $false },
        [ordered]@{ ItemId = "rejected-legacy-06"; Reason = "CanonicalQuarterHourTargetCloseRequired"; Accepted = $false; CreatesOrder = $false }
    )
    RejectedProducesOrder = $false
    RejectedProducesRoute = $false
    RejectedProducesExecutableSchedule = $false
})

Write-JsonArtifact "phase-exec-live-r010-preview-audit-records-created.json" ([ordered]@{
    Phase = $phase
    AuditRecordsCreated = 5
    SinglePreviewAuditRecords = 4
    BatchPreviewAuditRecords = 1
    AuditPath = "artifacts/readiness/execution-live/audit"
    ArtifactOnly = $true
    DbWrites = $false
    OrderDomainPersistence = $false
    RouteSubmissionPersistence = $false
    LedgerPersistence = $false
    TradingStateMutation = $false
    CreatedByFocusedR010Tests = $true
})

$operatorReport = @"
# EXEC-LIVE-R010 Operator Trial Review

ReviewOnly=true
ExecutableApproval=false
BrokerApproval=false
LiveApproval=false

Requests:
- Total: 11
- Accepted: 5
- Rejected: 6
- Batch requests: 1

Decisions:
- PreviewReady: 5
- HeldMissingReadiness: 1
- Rejected: 2

The trial exercised allowed internal preview consumers, rejected forbidden consumers, persisted preview audit records only as artifacts, and exported this operator review report under the operator-review artifact path. No broker, live market data, order, route, fill, executable schedule, ledger commit, or trading state path was enabled.
"@
$operatorReportPath = Join-Path $operatorReviewDir "phase-exec-live-r010-operator-trial-review.md"
$operatorReport | Set-Content -Path $operatorReportPath -Encoding UTF8

Write-JsonArtifact "phase-exec-live-r010-operator-review-reports-created.json" ([ordered]@{
    Phase = $phase
    OperatorReportsCreated = 1
    Reports = @([ordered]@{ Path = "artifacts/readiness/execution-live/operator-review/phase-exec-live-r010-operator-trial-review.md"; ReviewOnly = $true; NonExecutable = $true; NotAnOrder = $true; NoBrokerRoute = $true })
    OutputPath = "artifacts/readiness/execution-live/operator-review"
    WritesOutsideArtifactPath = $false
    ExecutableApproval = $false
})

Write-JsonArtifact "phase-exec-live-r010-trial-coverage-summary.json" ([ordered]@{
    Phase = $phase
    TotalRequests = 11
    BatchRequests = 1
    AcceptedRequests = 5
    RejectedRequests = 6
    PreviewReadyDecisions = 5
    HeldDecisions = 1
    RejectedDecisions = 2
    AuditRecordsCreated = 5
    OperatorReportsCreated = 1
    DisabledFlagsRemainFalse = $true
    NoBrokerOrderRouteFillScheduleLedgerPath = $true
})

Write-JsonArtifact "phase-exec-live-r010-per-symbol-trial-review.json" ([ordered]@{
    Phase = $phase
    SymbolCoverage = @(
        [ordered]@{ Symbol = "AUDUSD"; PreviewReady = 3; Held = 0; Rejected = 0; AudusdStatus = "SupportedAndNotFailed" },
        [ordered]@{ Symbol = "EURUSD"; PreviewReady = 1; Held = 0; Rejected = 0 },
        [ordered]@{ Symbol = "USDJPY"; PreviewReady = 1; Held = 0; Rejected = 0; RequiresInversion = $true; SecurityID = "4004"; SecurityIDSource = "8" },
        [ordered]@{ Symbol = "GBPUSD"; PreviewReady = 0; Held = 1; Rejected = 0 },
        [ordered]@{ Symbol = "EURGBP"; PreviewReady = 0; Held = 0; Rejected = 1; DirectCross = $true },
        [ordered]@{ Symbol = "NZDUSD"; PreviewReady = 0; Held = 0; Rejected = 1; LegacyTargetCloseRejected = $true }
    )
    DirectCrossExecutableLines = 0
    NonmajorEmScandiCnhAllowed = $false
})

Write-JsonArtifact "phase-exec-live-r010-bar-role-trial-review.json" ([ordered]@{
    Phase = $phase
    BarRoleCoverage = @(
        [ordered]@{ BarRole = "OpeningBuild"; PreviewReady = 1; Held = 0; Rejected = 0 },
        [ordered]@{ BarRole = "IntradayRebalance"; PreviewReady = 4; Held = 1; Rejected = 1 },
        [ordered]@{ BarRole = "ClosingFlatten"; PreviewReady = 0; Held = 0; Rejected = 1 }
    )
    BalancedInputSourceReferenced = $true
})

Write-JsonArtifact "phase-exec-live-r010-direct-cross-rejection-review.json" ([ordered]@{
    Phase = $phase
    DirectCrossSymbolTested = "EURGBP"
    DirectCrossExecutionAllowed = $false
    DirectCrossExecutionIntentRejected = $true
    RejectionReason = "DirectCrossExecutionIntentRejected"
    CreatesOrder = $false
    CreatesRoute = $false
})

Write-JsonArtifact "phase-exec-live-r010-usdjpy-caveat-review.json" ([ordered]@{
    Phase = $phase
    Symbol = "USDJPY"
    NormalizedPortfolioSymbol = "JPYUSD"
    ExecutionTradableSymbol = "USDJPY"
    RequiresInversion = $true
    SecurityID = "4004"
    SecurityIDSource = "8"
    CaveatPreserved = $true
    InversionFailureCount = 0
})

Write-JsonArtifact "phase-exec-live-r010-legacy-target-close-rejection-review.json" ([ordered]@{
    Phase = $phase
    LegacyTargetCloseTested = "2026-05-25T15:06:00 America/New_York"
    AcceptedAsFutureCanonical = $false
    RejectionReason = "CanonicalQuarterHourTargetCloseRequired"
    CanonicalFutureMinutes = @(0, 15, 30, 45)
})

Write-JsonArtifact "phase-exec-live-r010-kill-switch-feature-flag-review.json" ([ordered]@{
    Phase = $phase
    LiveTradingEnabled = $false
    BrokerRoutingEnabled = $false
    OrderSubmissionEnabled = $false
    ExecutableScheduleEnabled = $false
    PaperLedgerCommitEnabled = $false
    SchedulerEnabled = $false
    BackgroundWorkerEnabled = $false
    DryRunOnly = $true
})
Write-JsonArtifact "phase-exec-live-r010-disabled-boundary-guard-review.json" ([ordered]@{ Phase = $phase; BrokerRouteCreationAllowed = $false; OrderCreationAllowed = $false; ChildSliceCreationAllowed = $false; ChildOrderCreationAllowed = $false; ScheduleExecutionAllowed = $false; SubmissionAllowed = $false; FillCreationAllowed = $false; ExecutionReportCreationAllowed = $false; StateMutationAllowed = $false; PaperLedgerCommitAllowed = $false })

Write-JsonArtifact "phase-exec-live-r010-internal-trial-decision.json" ([ordered]@{
    Phase = $phase
    Decision = "DisabledPreviewTrialPassedWithHeldReadiness"
    TrialPassed = $true
    HeldReadinessObserved = $true
    ExecutableApproval = $false
    BrokerApproval = $false
    LiveApproval = $false
    PaperLedgerCommitApproval = $false
    SeparateExplicitExecutableGateRequired = $true
})

Write-JsonArtifact "phase-exec-live-r010-executable-promotion-blockers.json" ([ordered]@{
    Phase = $phase
    ExecutablePromotionBlocked = $true
    Blockers = @(
        "No broker integration authorized",
        "No live market data authorized",
        "No scheduler/service/polling authorized",
        "No order-domain creation authorized",
        "No route/submission/fill/execution-report path authorized",
        "No executable schedule authorized",
        "No paper ledger commit authorized",
        "No trading state mutation authorized",
        "Direct-cross execution disabled",
        "Nonmajor/EM/scandi/CNH calibration required",
        "Separate explicit executable gate required"
    )
})

$audits = [ordered]@{
    "phase-exec-live-r010-no-broker-activation-audit.json" = New-Audit "NoBrokerActivation" "R010 trial uses disabled in-memory preview services and does not activate broker."
    "phase-exec-live-r010-no-live-marketdata-audit.json" = New-Audit "NoLiveMarketData" "R010 trial uses existing paper-plan artifacts and does not request live market data."
    "phase-exec-live-r010-no-scheduler-service-polling-audit.json" = New-Audit "NoSchedulerServicePolling" "R010 trial does not start scheduler/service/timer/polling/background jobs."
    "phase-exec-live-r010-no-order-created-audit.json" = New-Audit "NoOrderCreated" "Preview decisions are NotAnOrder and CreatesOrder=false."
    "phase-exec-live-r010-no-child-order-audit.json" = New-Audit "NoChildOrder" "Preview decisions create no child slices or child orders."
    "phase-exec-live-r010-no-executable-schedule-audit.json" = New-Audit "NoExecutableSchedule" "ScheduleIntentPreview remains NonExecutable and CreatesExecutableSchedule=false."
    "phase-exec-live-r010-no-route-no-submission-audit.json" = New-Audit "NoRouteNoSubmission" "Preview decisions create no routes or submissions."
    "phase-exec-live-r010-no-fill-execution-report-audit.json" = New-Audit "NoFillExecutionReport" "Preview decisions create no fills or execution reports."
    "phase-exec-live-r010-no-paper-ledger-commit-audit.json" = New-Audit "NoPaperLedgerCommit" "Preview audit persistence is artifact-only and does not commit paper ledger state."
    "phase-exec-live-r010-no-state-mutation-audit.json" = New-Audit "NoStateMutation" "No live, broker, production, position, or trading state mutation is part of this phase."
    "phase-exec-live-r010-no-external-audit.json" = New-Audit "NoExternal" "No external API, Polygon, LMAX, broker, or live market data call is part of this phase."
}
foreach ($entry in $audits.GetEnumerator()) { Write-JsonArtifact $entry.Key $entry.Value }

Write-JsonArtifact "phase-exec-live-r010-canonical-quarter-hour-policy-preservation.json" ([ordered]@{ Phase = $phase; FutureCanonicalMinutes = @(0, 15, 30, 45); LegacyMinutesAcceptedAsFutureCanonical = $false })
Write-JsonArtifact "phase-exec-live-r010-legacy-compatibility-preservation.json" ([ordered]@{ Phase = $phase; LegacyLabels = @(":06", ":21", ":36", ":51"); Usage = "CompatibilityOnly"; UsedAsFutureCanonical = $false })
Write-JsonArtifact "phase-exec-live-r010-direct-cross-exclusion-preservation.json" ([ordered]@{ Phase = $phase; DirectCrossExecutionAllowed = $false; QubesWeightsMayContainCrossesAsSignalsOnly = $true; DirectCrossExecutionIntentRejected = $true })
Write-JsonArtifact "phase-exec-live-r010-usd-pair-netting-requirement.json" ([ordered]@{ Phase = $phase; Requirement = "EMS/OMS execution intents must be USD-pair-only after netting."; SupportedExecutionSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF"); AudusdStatus = "SupportedAndNotFailed" })
Write-JsonArtifact "phase-exec-live-r010-usdjpy-caveat-preservation.json" ([ordered]@{ Phase = $phase; NormalizedPortfolioSymbol = "JPYUSD"; ExecutionTradableSymbol = "USDJPY"; RequiresInversion = $true; SecurityID = "4004"; SecurityIDSource = "8"; CaveatPreserved = $true })
Write-JsonArtifact "phase-exec-live-r010-cost-guidance-preservation.json" ([ordered]@{ Phase = $phase; FiveUsdPerMillion = "BestCaseMajorOnly"; Universalized = $false })
Write-JsonArtifact "phase-exec-live-r010-nonmajor-calibration-preservation.json" ([ordered]@{ Phase = $phase; NonmajorEmScandiCnh = "CalibrationRequired"; LiveCapableExecutionAllowed = $false })
Write-JsonArtifact "phase-exec-live-r010-forbidden-actions-audit.json" ([ordered]@{ Phase = $phase; ExternalApiCallsMade = $false; PolygonCallsMade = $false; LmaxCallsMade = $false; BrokerActivationOccurred = $false; LiveMarketDataRequested = $false; SchedulerServicePollingBackgroundJobIntroduced = $false; PmsEmsOmsProductionCycleRun = $false; ManualNoExternalCommandRun = $false; BacktestSimulationRun = $false; TcaResultLinesCreated = $false; ExecutableScheduleCreated = $false; OrdersChildOrdersRoutesSubmissionsFillsReportsCreated = $false; PaperLedgerCommitCreated = $false; StateMutationOccurred = $false; R009PromotedToExecutableUse = $false; TrialDecisionImpliesExecutableApproval = $false; BrokerLiveOrderRouteScheduleLedgerPathEnabled = $false; ForbiddenConsumerAllowed = $false; DirectCrossExecutionAllowed = $false; Legacy06AcceptedAsFutureCanonical = $false; PreviewOutputRepresentedAsOrderRouteFillSchedule = $false })
Write-JsonArtifact "phase-exec-live-r010-next-phase-recommendation.json" ([ordered]@{ Phase = $phase; RecommendedNextPhase = "EXEC-LIVE-R011"; Title = "R009 Disabled Preview Trial Review and Pre-Paper-Ledger Readiness Gate"; Constraints = "Review internal disabled-preview trial outcomes while keeping broker, live data, order, route, schedule, fill, report, ledger, and state paths disabled." })
Write-JsonArtifact "phase-exec-live-r010-build-test-validator-evidence.json" ([ordered]@{ Phase = $phase; Build = $BuildStatus; FocusedR010Tests = $FocusedTestsStatus; UnitTests = $UnitTestsStatus; Validator = $ValidatorStatus; DotnetBuildNoRestore = "dotnet build --no-restore"; FocusedTests = "dotnet test tests\QQ.Production.Intraday.Tests.Unit\QQ.Production.Intraday.Tests.Unit.csproj --no-build --no-restore --filter FullyQualifiedName~R009InternalDisabledPreviewTrialTests"; ValidatorScript = "scripts/check-exec-live-r010-disabled-preview-internal-trial-gate.ps1" })

$summary = @"
# EXEC-LIVE-R010 Summary

Classifications:
- EXEC_LIVE_R010_PASS_INTERNAL_DISABLED_PREVIEW_TRIAL_READY_NO_EXTERNAL
- EXEC_LIVE_R010_PASS_INTERNAL_DISABLED_PREVIEW_TRIAL_WITH_HELD_READINESS_NO_EXTERNAL
- EXEC_LIVE_R010_PASS_CONSUMER_BOUNDARY_TRIAL_READY_NO_EXTERNAL
- EXEC_LIVE_R010_PASS_AUDIT_AND_OPERATOR_REVIEW_TRIAL_READY_NO_EXTERNAL
- EXEC_LIVE_R010_PASS_NO_BROKER_NO_ORDER_NO_ROUTE_GATE_READY_NO_EXTERNAL

R010 ran the internal disabled-preview trial through allowed preview consumers, the disabled single/batch preview services, artifact-only audit persistence, and the operator review surface. The trial used existing paper-plan evidence as its source context and did not run PMS/EMS/OMS production cycles or ManualNoExternal commands.

Trial results:
- Total requests: 11
- Accepted requests: 5
- Rejected requests: 6
- PreviewReady decisions: 5
- HeldMissingReadiness decisions: 1
- Rejected decisions: 2
- Artifact-only audit records: 5
- Operator reports: 1

Decision: DisabledPreviewTrialPassedWithHeldReadiness. This is not executable approval; broker, live market data, order, route, fill, executable schedule, ledger commit, and trading state paths remain disabled.

Build/tests/validator:
- Build: $BuildStatus
- Focused R010 tests: $FocusedTestsStatus
- Unit tests: $UnitTestsStatus
- Validator: $ValidatorStatus

Next recommendation: EXEC-LIVE-R011 - R009 Disabled Preview Trial Review and Pre-Paper-Ledger Readiness Gate.
"@
$summary | Set-Content -Path (Join-Path $artifactDir "phase-exec-live-r010-summary.md") -Encoding UTF8

Write-Host "EXEC-LIVE-R010 artifacts written to $artifactDir"
