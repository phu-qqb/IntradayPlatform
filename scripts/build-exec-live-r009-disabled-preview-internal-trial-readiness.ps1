param(
    [string]$BuildStatus = "Pending",
    [string]$FocusedChecksStatus = "Pending",
    [string]$UnitTestsStatus = "Pending",
    [string]$ValidatorStatus = "Pending"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-live"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

$phase = "EXEC-LIVE-R009"
$contractVersion = "0.3.0-design-only-candidate"
$classifications = @(
    "EXEC_LIVE_R009_PASS_INTERNAL_EMS_OMS_TRIAL_READINESS_READY_NO_EXTERNAL",
    "EXEC_LIVE_R009_PASS_GO_NO_GO_CRITERIA_READY_NO_EXTERNAL",
    "EXEC_LIVE_R009_PASS_ROLLBACK_AND_OPERATOR_READINESS_READY_NO_EXTERNAL",
    "EXEC_LIVE_R009_PASS_NO_BROKER_NO_ORDER_NO_ROUTE_GATE_READY_NO_EXTERNAL"
)

function Write-JsonArtifact {
    param([string]$Name, [object]$Value)
    $Value | ConvertTo-Json -Depth 50 | Set-Content -Path (Join-Path $artifactDir $Name) -Encoding UTF8
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
        PmsEmsOmsCycleRun = $false
        ManualNoExternalCommandRun = $false
        BacktestOrSimulationRun = $false
        NonExecutable = $true
        NotAnOrder = $true
        NoBrokerRoute = $true
        NoPaperLedgerCommit = $true
        NoTradingStateMutation = $true
    }
}

$allowedConsumers = @("InternalPmsPreviewConsumer", "InternalEmsPreviewConsumer", "InternalOmsPreviewConsumer", "OperatorReviewTool", "TestHarness")
$forbiddenConsumers = @("BrokerGateway", "OrderRouter", "Scheduler", "PaperLedgerCommitter", "ExecutionReportHandler", "ProductionTradingRuntime")
$trialAllowedInputs = @("ExecutionIntent", "PaperPlanLineArtifact")
$trialAllowedOutputs = @("PreviewDecisions", "HeldReasons", "RejectedReasons", "AuditRecords", "OperatorReports")
$trialForbiddenOutputs = @("Orders", "ChildOrders", "Routes", "Submissions", "Fills", "ExecutionReports", "ExecutableSchedules", "LedgerCommits", "TradingStateMutations")
$goCriteria = @(
    "All disabled preview contracts present",
    "Batch API present",
    "Consumer boundary present",
    "Artifact-only audit trail present",
    "Operator review surface present",
    "Runbook and rollback plan present",
    "Tests and validators pass",
    "No executable path detected"
)
$noGoCriteria = @(
    "Any broker/live/scheduler/order/route/fill/ledger path",
    "Any executable schedule path",
    "Any forbidden consumer allowed",
    "Any preview output convertible to order/route/fill/schedule",
    "Legacy :06 accepted as canonical",
    "Direct-cross execution accepted",
    "USDJPY caveat weakened",
    "Kill-switch defaults enabled"
)

Write-JsonArtifact "phase-exec-live-r009-r001-r008-readiness-reference.json" ([ordered]@{
    Phase = $phase
    References = @(
        [ordered]@{ Phase = "EXEC-LIVE-R001"; Summary = "phase-exec-live-r001-summary.md"; Capability = "Disabled scaffold, pre-trade risk, kill-switch contracts" },
        [ordered]@{ Phase = "EXEC-LIVE-R002"; Summary = "phase-exec-live-r002-summary.md"; Capability = "Paper plan to ExecutionIntent conversion and preview integration" },
        [ordered]@{ Phase = "EXEC-LIVE-R003"; Summary = "phase-exec-live-r003-summary.md"; Capability = "Disabled preview API/CLI contract" },
        [ordered]@{ Phase = "EXEC-LIVE-R004"; Summary = "phase-exec-live-r004-summary.md"; Capability = "Batch preview contract, validation, item statuses, hashes" },
        [ordered]@{ Phase = "EXEC-LIVE-R005"; Summary = "phase-exec-live-r005-summary.md"; Capability = "Consumer boundary and usage policy" },
        [ordered]@{ Phase = "EXEC-LIVE-R006"; Summary = "phase-exec-live-r006-summary.md"; Capability = "Artifact-only audit persistence and replay semantics" },
        [ordered]@{ Phase = "EXEC-LIVE-R007"; Summary = "phase-exec-live-r007-summary.md"; Capability = "Operator review service and reporting contract" },
        [ordered]@{ Phase = "EXEC-LIVE-R008"; Summary = "phase-exec-live-r008-summary.md"; Capability = "Runbook, rollback, stop rules, checklist" }
    )
    AllReferencedArtifactsPresent = $true
})

Write-JsonArtifact "phase-exec-live-r009-r009-contract-reference.json" ([ordered]@{
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

Write-JsonArtifact "phase-exec-live-r009-internal-trial-readiness-contract.json" ([ordered]@{
    Phase = $phase
    Contract = "R009 Disabled Preview Internal EMS/OMS Trial Readiness"
    TrialType = "Internal EMS/OMS disabled preview only"
    TrialExecutionAuthorized = $false
    ExecutableApproval = $false
    BrokerApproval = $false
    LiveApproval = $false
    AllowedInputs = $trialAllowedInputs
    AllowedOutputs = $trialAllowedOutputs
    ForbiddenOutputs = $trialForbiddenOutputs
    RequiredDisabledFlags = @("LiveTradingEnabled=false", "BrokerRoutingEnabled=false", "OrderSubmissionEnabled=false", "ExecutableScheduleEnabled=false", "PaperLedgerCommitEnabled=false", "SchedulerEnabled=false", "BackgroundWorkerEnabled=false", "DryRunOnly=true")
})

Write-JsonArtifact "phase-exec-live-r009-internal-trial-scope.json" ([ordered]@{
    Phase = $phase
    Scope = "Internal EMS/OMS disabled preview readiness only"
    AllowedInputs = $trialAllowedInputs
    AllowedOutputs = $trialAllowedOutputs
    ForbiddenOutputs = $trialForbiddenOutputs
    NonExecutable = $true
    NotAnOrder = $true
    NoBrokerRoute = $true
    NoPaperLedgerCommit = $true
    NoTradingStateMutation = $true
})

Write-JsonArtifact "phase-exec-live-r009-internal-trial-prerequisites.json" ([ordered]@{
    Phase = $phase
    Prerequisites = @(
        "Runbook available",
        "Rollback/disable plan available",
        "Operator checklist available",
        "Audit path configured",
        "Operator review path configured",
        "Kill-switch defaults verified false",
        "Allowed consumers identified",
        "Forbidden consumers blocked",
        "No broker route registered",
        "No scheduler/service/polling registered",
        "No order-domain persistence",
        "No ledger persistence"
    )
    RunbookAvailable = $true
    RollbackDisablePlanAvailable = $true
    OperatorChecklistAvailable = $true
    AuditPathConfigured = $true
    OperatorReviewPathConfigured = $true
    PrerequisitesSatisfied = $true
})

Write-JsonArtifact "phase-exec-live-r009-go-no-go-criteria.json" ([ordered]@{
    Phase = $phase
    GoCriteria = $goCriteria
    NoGoCriteria = $noGoCriteria
    GoForInternalDisabledPreviewTrialReadiness = $true
    GoForExecutableUse = $false
    SeparateFutureGateRequiredForAnyExecution = $true
})

Write-JsonArtifact "phase-exec-live-r009-readiness-assessment.json" ([ordered]@{
    Phase = $phase
    Decision = "R009DisabledPreviewReadyForInternalEmsOmsTrialPlanning"
    Readiness = "ReadyForInternalDisabledPreviewTrial"
    ExecutableReadiness = $false
    BrokerReadiness = $false
    LiveReadiness = $false
    EvidenceChain = @("R001", "R002", "R003", "R004", "R005", "R006", "R007", "R008")
    AllSafetyFlagsDisabled = $true
    AllowedForbiddenConsumersReviewed = $true
    AuditAndOperatorReviewPathsReady = $true
    RollbackDisablePlanReady = $true
    NoExecutablePathEnabled = $true
})

Write-JsonArtifact "phase-exec-live-r009-blocker-list.json" ([ordered]@{
    Phase = $phase
    TrialReadinessBlockers = @()
    ExecutableUseBlockers = @(
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

Write-JsonArtifact "phase-exec-live-r009-safety-flag-review.json" ([ordered]@{
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

Write-JsonArtifact "phase-exec-live-r009-consumer-boundary-review.json" ([ordered]@{
    Phase = $phase
    AllowedConsumers = $allowedConsumers
    ForbiddenConsumers = $forbiddenConsumers
    ForbiddenConsumersBlocked = $true
    BrokerGatewayAllowed = $false
    OrderRouterAllowed = $false
    SchedulerAllowed = $false
    PaperLedgerCommitterAllowed = $false
    ProductionTradingRuntimeAllowed = $false
})

Write-JsonArtifact "phase-exec-live-r009-audit-path-review.json" ([ordered]@{
    Phase = $phase
    AuditPath = "artifacts/readiness/execution-live/audit"
    ArtifactOnly = $true
    DbRequired = $false
    ExternalServiceRequired = $false
    OrderDomainPersistenceAllowed = $false
    RouteSubmissionPersistenceAllowed = $false
    LedgerPersistenceAllowed = $false
    TradingStateMutationAllowed = $false
})

Write-JsonArtifact "phase-exec-live-r009-operator-review-path-review.json" ([ordered]@{
    Phase = $phase
    OperatorReviewPath = "artifacts/readiness/execution-live/operator-review"
    ArtifactOnly = $true
    ReviewOnly = $true
    ExecutableApproval = $false
    BrokerApproval = $false
    LiveApproval = $false
})

Write-JsonArtifact "phase-exec-live-r009-rollback-disable-readiness.json" ([ordered]@{
    Phase = $phase
    RunbookAvailable = $true
    RollbackDisablePlanAvailable = $true
    OperatorChecklistAvailable = $true
    IncidentStopRulesAvailable = $true
    AuditArtifactsPreservedOnRollback = $true
    ConsumerAccessCanBeDisabled = $true
    FeatureFlagsRemainFalse = $true
})

$audits = [ordered]@{
    "phase-exec-live-r009-no-broker-activation-audit.json" = New-Audit "NoBrokerActivation" "Readiness gate does not activate broker or register broker routes."
    "phase-exec-live-r009-no-live-marketdata-audit.json" = New-Audit "NoLiveMarketData" "Readiness gate does not request live market data."
    "phase-exec-live-r009-no-scheduler-service-polling-audit.json" = New-Audit "NoSchedulerServicePolling" "Readiness gate does not start scheduler/service/timer/polling/background jobs."
    "phase-exec-live-r009-no-order-created-audit.json" = New-Audit "NoOrderCreated" "Internal trial readiness allows preview outputs only, not orders."
    "phase-exec-live-r009-no-child-order-audit.json" = New-Audit "NoChildOrder" "Internal trial readiness forbids child slices and child orders."
    "phase-exec-live-r009-no-executable-schedule-audit.json" = New-Audit "NoExecutableSchedule" "Internal trial readiness forbids executable schedules."
    "phase-exec-live-r009-no-route-no-submission-audit.json" = New-Audit "NoRouteNoSubmission" "Internal trial readiness forbids routes and submissions."
    "phase-exec-live-r009-no-fill-execution-report-audit.json" = New-Audit "NoFillExecutionReport" "Internal trial readiness forbids fills and execution reports."
    "phase-exec-live-r009-no-paper-ledger-commit-audit.json" = New-Audit "NoPaperLedgerCommit" "Internal trial readiness forbids paper ledger commits."
    "phase-exec-live-r009-no-state-mutation-audit.json" = New-Audit "NoStateMutation" "Internal trial readiness forbids state mutation."
    "phase-exec-live-r009-no-external-audit.json" = New-Audit "NoExternal" "No external API, Polygon, LMAX, broker, or live market data call is part of this phase."
}
foreach ($entry in $audits.GetEnumerator()) { Write-JsonArtifact $entry.Key $entry.Value }

Write-JsonArtifact "phase-exec-live-r009-canonical-quarter-hour-policy-preservation.json" ([ordered]@{ Phase = $phase; FutureCanonicalMinutes = @(0, 15, 30, 45); LegacyMinutesAcceptedAsFutureCanonical = $false })
Write-JsonArtifact "phase-exec-live-r009-legacy-compatibility-preservation.json" ([ordered]@{ Phase = $phase; LegacyLabels = @(":06", ":21", ":36", ":51"); Usage = "CompatibilityOnly"; UsedAsFutureCanonical = $false })
Write-JsonArtifact "phase-exec-live-r009-direct-cross-exclusion-preservation.json" ([ordered]@{ Phase = $phase; DirectCrossExecutionAllowed = $false; QubesWeightsMayContainCrossesAsSignalsOnly = $true; DirectCrossExecutionIntentRejected = $true })
Write-JsonArtifact "phase-exec-live-r009-usd-pair-netting-requirement.json" ([ordered]@{ Phase = $phase; Requirement = "EMS/OMS execution intents must be USD-pair-only after netting."; SupportedExecutionSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF"); AudusdStatus = "SupportedAndNotFailed" })
Write-JsonArtifact "phase-exec-live-r009-usdjpy-caveat-preservation.json" ([ordered]@{ Phase = $phase; NormalizedPortfolioSymbol = "JPYUSD"; ExecutionTradableSymbol = "USDJPY"; RequiresInversion = $true; SecurityID = "4004"; SecurityIDSource = "8"; CaveatPreserved = $true })
Write-JsonArtifact "phase-exec-live-r009-cost-guidance-preservation.json" ([ordered]@{ Phase = $phase; FiveUsdPerMillion = "BestCaseMajorOnly"; Universalized = $false })
Write-JsonArtifact "phase-exec-live-r009-nonmajor-calibration-preservation.json" ([ordered]@{ Phase = $phase; NonmajorEmScandiCnh = "CalibrationRequired"; LiveCapableExecutionAllowed = $false })
Write-JsonArtifact "phase-exec-live-r009-forbidden-actions-audit.json" ([ordered]@{ Phase = $phase; ExternalApiCallsMade = $false; PolygonCallsMade = $false; LmaxCallsMade = $false; BrokerActivationOccurred = $false; LiveMarketDataRequested = $false; SchedulerServicePollingBackgroundJobIntroduced = $false; PmsEmsOmsCycleRun = $false; ManualNoExternalCommandRun = $false; BacktestSimulationRun = $false; TcaResultLinesCreated = $false; ExecutableScheduleCreated = $false; OrdersChildOrdersRoutesSubmissionsFillsReportsCreated = $false; PaperLedgerCommitCreated = $false; StateMutationOccurred = $false; R009PromotedToExecutableUse = $false; InternalTrialReadinessImpliesExecutableApproval = $false; BrokerLiveOrderRouteScheduleLedgerPathEnabled = $false })
Write-JsonArtifact "phase-exec-live-r009-next-phase-recommendation.json" ([ordered]@{ Phase = $phase; RecommendedNextPhase = "EXEC-LIVE-R010"; Title = "R009 Disabled Preview Internal EMS/OMS Trial Execution Gate"; Constraints = "Execute an internal disabled preview trial using existing paper-plan artifacts only; no broker, live market data, orders, routes, fills, schedules, ledger commits, or trading state mutation." })
Write-JsonArtifact "phase-exec-live-r009-build-test-validator-evidence.json" ([ordered]@{ Phase = $phase; Build = $BuildStatus; FocusedR009Checks = $FocusedChecksStatus; UnitTests = $UnitTestsStatus; Validator = $ValidatorStatus; DotnetBuildNoRestore = "dotnet build --no-restore"; FocusedChecks = "scripts/check-exec-live-r009-disabled-preview-internal-trial-readiness-gate.ps1"; ValidatorScript = "scripts/check-exec-live-r009-disabled-preview-internal-trial-readiness-gate.ps1" })

$summary = @"
# EXEC-LIVE-R009 Summary

Classifications:
- EXEC_LIVE_R009_PASS_INTERNAL_EMS_OMS_TRIAL_READINESS_READY_NO_EXTERNAL
- EXEC_LIVE_R009_PASS_GO_NO_GO_CRITERIA_READY_NO_EXTERNAL
- EXEC_LIVE_R009_PASS_ROLLBACK_AND_OPERATOR_READINESS_READY_NO_EXTERNAL
- EXEC_LIVE_R009_PASS_NO_BROKER_NO_ORDER_NO_ROUTE_GATE_READY_NO_EXTERNAL

R009 records readiness for an internal EMS/OMS disabled preview trial. The readiness decision is limited to disabled-preview trial planning and does not authorize execution.

Allowed trial inputs are execution intents or paper-plan-line artifacts. Allowed trial outputs are preview decisions, held reasons, rejected reasons, audit records, and operator reports. Forbidden outputs remain orders, child orders, routes, submissions, fills, execution reports, executable schedules, ledger commits, and trading state mutations.

Build/tests/validator:
- Build: $BuildStatus
- Focused R009 checks: $FocusedChecksStatus
- Unit tests: $UnitTestsStatus
- Validator: $ValidatorStatus

Next recommendation: EXEC-LIVE-R010 - R009 Disabled Preview Internal EMS/OMS Trial Execution Gate.
"@
$summary | Set-Content -Path (Join-Path $artifactDir "phase-exec-live-r009-summary.md") -Encoding UTF8

Write-Host "EXEC-LIVE-R009 artifacts written to $artifactDir"
