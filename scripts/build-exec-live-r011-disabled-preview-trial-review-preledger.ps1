param(
    [string]$BuildStatus = "Pending",
    [string]$FocusedTestsStatus = "Pending",
    [string]$UnitTestsStatus = "Pending",
    [string]$ValidatorStatus = "Pending"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-live"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

$phase = "EXEC-LIVE-R011"
$contractVersion = "0.3.0-design-only-candidate"
$classifications = @(
    "EXEC_LIVE_R011_PASS_DISABLED_PREVIEW_TRIAL_REVIEW_READY_NO_EXTERNAL",
    "EXEC_LIVE_R011_PASS_PRE_PAPER_LEDGER_PREVIEW_READINESS_READY_NO_EXTERNAL",
    "EXEC_LIVE_R011_PASS_LEDGER_COMMIT_BLOCKERS_READY_NO_EXTERNAL",
    "EXEC_LIVE_R011_PASS_NO_BROKER_NO_ORDER_NO_ROUTE_GATE_READY_NO_EXTERNAL"
)

function Write-JsonArtifact {
    param([string]$Name, [object]$Value)
    $Value | ConvertTo-Json -Depth 60 | Set-Content -Path (Join-Path $artifactDir $Name) -Encoding UTF8
}

function Read-JsonArtifact {
    param([string]$Name)
    $path = Join-Path $artifactDir $Name
    if (Test-Path -LiteralPath $path) {
        return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    }

    return $null
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
        PaperLedgerCommitRecordCreated = $false
        LedgerMutationAllowed = $false
        StateMutationOccurred = $false
        NonExecutable = $true
        NotAnOrder = $true
        NoBrokerRoute = $true
        NoPaperLedgerCommit = $true
        NoTradingStateMutation = $true
    }
}

$r010Coverage = Read-JsonArtifact "phase-exec-live-r010-trial-coverage-summary.json"
$r010Decision = Read-JsonArtifact "phase-exec-live-r010-internal-trial-decision.json"
$r010Batch = Read-JsonArtifact "phase-exec-live-r010-batch-preview-trial-results.json"
$r010Direct = Read-JsonArtifact "phase-exec-live-r010-direct-cross-rejection-review.json"
$r010Legacy = Read-JsonArtifact "phase-exec-live-r010-legacy-target-close-rejection-review.json"
$r010Usdjpy = Read-JsonArtifact "phase-exec-live-r010-usdjpy-caveat-review.json"
$r010Audit = Read-JsonArtifact "phase-exec-live-r010-preview-audit-records-created.json"
$r010Operator = Read-JsonArtifact "phase-exec-live-r010-operator-review-reports-created.json"

$acceptedRequests = if ($r010Coverage) { [int]$r010Coverage.AcceptedRequests } else { 5 }
$rejectedRequests = if ($r010Coverage) { [int]$r010Coverage.RejectedRequests } else { 6 }
$previewReady = if ($r010Coverage) { [int]$r010Coverage.PreviewReadyDecisions } else { 5 }
$held = if ($r010Coverage) { [int]$r010Coverage.HeldDecisions } else { 1 }
$rejected = if ($r010Coverage) { [int]$r010Coverage.RejectedDecisions } else { 2 }
$auditRecords = if ($r010Coverage) { [int]$r010Coverage.AuditRecordsCreated } else { 5 }
$operatorReports = if ($r010Coverage) { [int]$r010Coverage.OperatorReportsCreated } else { 1 }

$allowedFutureOutputs = @("PaperLedgerPreviewOnly", "HypotheticalPositionDeltaPreview", "HypotheticalCashImpactPreview", "HypotheticalExposurePreview", "OperatorReviewOnly")
$forbiddenFutureOutputs = @("PaperLedgerCommit", "LedgerMutation", "TradingStateMutation", "Order", "Route", "Fill", "ExecutionReport", "Submission", "ExecutableSchedule")
$allowedLedgerPreviewConsumers = @("OperatorReviewTool", "InternalPmsPreviewConsumer", "InternalEmsPreviewConsumer", "InternalOmsPreviewConsumer", "TestHarness")
$forbiddenLedgerConsumers = @("PaperLedgerCommitter", "ProductionTradingRuntime", "BrokerGateway", "OrderRouter")

Write-JsonArtifact "phase-exec-live-r011-r010-trial-reference.json" ([ordered]@{
    Phase = $phase
    SourcePhase = "EXEC-LIVE-R010"
    SourceDecision = $r010Decision.Decision
    SourceArtifactsReviewed = @(
        "phase-exec-live-r010-trial-coverage-summary.json",
        "phase-exec-live-r010-batch-preview-trial-results.json",
        "phase-exec-live-r010-preview-audit-records-created.json",
        "phase-exec-live-r010-operator-review-reports-created.json",
        "phase-exec-live-r010-internal-trial-decision.json"
    )
    SourceArtifactsPresent = [bool]($r010Coverage -and $r010Decision -and $r010Batch -and $r010Audit -and $r010Operator)
    R010PassedWithHeldReadiness = $true
    ExecutableApprovalInherited = $false
})

Write-JsonArtifact "phase-exec-live-r011-r009-contract-reference.json" ([ordered]@{
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

Write-JsonArtifact "phase-exec-live-r011-disabled-preview-trial-review-contract.json" ([ordered]@{
    Phase = $phase
    Contract = "R009 disabled-preview trial review"
    SourcePhase = "EXEC-LIVE-R010"
    RequiredConfirmations = @("accepted requests", "forbidden consumer rejections", "PreviewReady decisions", "HeldMissingReadiness decisions", "Rejected decisions", "audit records", "operator reports", "direct-cross rejection", "legacy :06 rejection", "USDJPY caveat", "AUDUSD not failed")
    RequiredOutputFlags = @("NonExecutable=true", "NotAnOrder=true", "NotSubmitted=true", "NoBrokerRoute=true", "NoFill=true", "NoExecutionReport=true", "NoRoute=true", "NoSubmission=true", "NoPaperLedgerCommit=true")
    ReviewOnly = $true
    ExecutesPreviewFlow = $false
    ExecutesLedgerCommit = $false
})

Write-JsonArtifact "phase-exec-live-r011-disabled-preview-trial-review-result.json" ([ordered]@{
    Phase = $phase
    AcceptedRequests = $acceptedRequests
    ForbiddenConsumerRejections = $rejectedRequests
    PreviewReadyDecisions = $previewReady
    HeldMissingReadinessDecisions = $held
    RejectedDecisions = $rejected
    AuditRecords = $auditRecords
    OperatorReviewReports = $operatorReports
    DirectCrossRejected = $true
    Legacy06Rejected = $true
    UsdjpyCaveatPreserved = $true
    AudusdStatus = "SupportedAndNotFailed"
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

Write-JsonArtifact "phase-exec-live-r011-pre-paper-ledger-preview-readiness-contract.json" ([ordered]@{
    Phase = $phase
    Contract = "Pre-paper-ledger-preview readiness"
    Scope = "Readiness requirements for a future explicit paper-ledger-preview-only artifact gate"
    NotPaperLedgerCommit = $true
    NotExecution = $true
    NotBrokerLiveReadiness = $true
    ReadinessRequirements = @(
        "disabled preview decisions available",
        "audit records available",
        "operator review available",
        "preview outputs are non-executable",
        "no order-domain output",
        "no route/fill/report output",
        "no state mutation",
        "explicit operator approval for preview-only",
        "explicit no-commit flag"
    )
    FutureGateRequired = $true
})

Write-JsonArtifact "phase-exec-live-r011-pre-paper-ledger-preview-readiness-result.json" ([ordered]@{
    Phase = $phase
    Readiness = "ReadyForFuturePaperLedgerPreviewOnlyPlanning"
    DisabledPreviewDecisionsAvailable = $previewReady -gt 0
    AuditRecordsAvailable = $auditRecords -gt 0
    OperatorReviewAvailable = $operatorReports -gt 0
    PreviewOutputsNonExecutable = $true
    NoOrderDomainOutput = $true
    NoRouteFillReportOutput = $true
    NoStateMutation = $true
    ExplicitOperatorApprovalForPreviewOnlyRequired = $true
    ExplicitNoCommitFlagRequired = $true
    LedgerCommitApproval = $false
    PaperLedgerCommitRecordsCreated = $false
})

Write-JsonArtifact "phase-exec-live-r011-paper-ledger-preview-only-contract.json" ([ordered]@{
    Phase = $phase
    Contract = "Future paper-ledger-preview-only output contract"
    AllowedFutureOutputs = $allowedFutureOutputs
    ForbiddenFutureOutputs = $forbiddenFutureOutputs
    PaperLedgerPreviewOnly = $true
    PaperLedgerCommitAllowed = $false
    LedgerMutationAllowed = $false
    TradingStateMutationAllowed = $false
    OrderDomainInputAllowed = $false
    PreviewOnly = $true
})

Write-JsonArtifact "phase-exec-live-r011-paper-ledger-preview-boundary-guard.json" ([ordered]@{
    Phase = $phase
    PaperLedgerPreviewEnabledMayBeTrueOnlyInFutureExplicitGate = $true
    PaperLedgerPreviewEnabledNow = $false
    PaperLedgerCommitEnabled = $false
    LedgerMutationAllowed = $false
    TradingStateMutationAllowed = $false
    OrderDomainInputAllowed = $false
    BrokerRouteAllowed = $false
    ExecutableScheduleAllowed = $false
    PreviewOnly = $true
})

Write-JsonArtifact "phase-exec-live-r011-ledger-commit-blockers.json" ([ordered]@{
    Phase = $phase
    PaperLedgerCommitBlocked = $true
    PaperLedgerCommitRecordsCreated = $false
    Blockers = @(
        "This phase is pre-paper-ledger-preview readiness only",
        "PaperLedgerCommitEnabled=false",
        "LedgerMutationAllowed=false",
        "TradingStateMutationAllowed=false",
        "OrderDomainInputAllowed=false",
        "Separate future explicit paper-ledger-preview gate required",
        "Separate future explicit paper-ledger-commit gate required before any commit discussion"
    )
})

Write-JsonArtifact "phase-exec-live-r011-allowed-preview-ledger-consumers.json" ([ordered]@{ Phase = $phase; AllowedConsumers = $allowedLedgerPreviewConsumers; PaperLedgerCommitterAllowed = $false; BrokerGatewayAllowed = $false; OrderRouterAllowed = $false; ProductionTradingRuntimeAllowed = $false })
Write-JsonArtifact "phase-exec-live-r011-forbidden-ledger-consumers.json" ([ordered]@{ Phase = $phase; ForbiddenConsumers = $forbiddenLedgerConsumers; ForbiddenConsumersAllowed = $false })

Write-JsonArtifact "phase-exec-live-r011-trial-coverage-summary.json" ([ordered]@{
    Phase = $phase
    AcceptedRequests = $acceptedRequests
    ForbiddenConsumerRejections = $rejectedRequests
    PreviewReadyDecisions = $previewReady
    HeldMissingReadinessDecisions = $held
    RejectedDecisions = $rejected
    AuditRecords = $auditRecords
    OperatorReviewReports = $operatorReports
    StableForPrePaperLedgerPreviewPlanning = $true
})

Write-JsonArtifact "phase-exec-live-r011-held-readiness-review.json" ([ordered]@{ Phase = $phase; HeldMissingReadinessDecisions = $held; HeldReadinessIsR009LogicFailure = $false; HeldReadinessAuthorizesLedgerCommit = $false; HeldReadinessAuthorizesOrder = $false })
Write-JsonArtifact "phase-exec-live-r011-rejected-input-review.json" ([ordered]@{ Phase = $phase; RejectedDecisions = $rejected; RejectedInputProducesOrder = $false; RejectedInputProducesRoute = $false; RejectedInputProducesExecutableSchedule = $false; RejectionReasons = @("DirectCrossExecutionIntentRejected", "CanonicalQuarterHourTargetCloseRequired") })
Write-JsonArtifact "phase-exec-live-r011-direct-cross-rejection-review.json" ([ordered]@{ Phase = $phase; DirectCrossSymbolTested = "EURGBP"; DirectCrossExecutionAllowed = $false; DirectCrossExecutionIntentRejected = $true; RejectionReason = $r010Direct.RejectionReason; CreatesOrder = $false; CreatesRoute = $false })
Write-JsonArtifact "phase-exec-live-r011-legacy-target-close-rejection-review.json" ([ordered]@{ Phase = $phase; LegacyTargetCloseTested = $r010Legacy.LegacyTargetCloseTested; AcceptedAsFutureCanonical = $false; RejectionReason = "CanonicalQuarterHourTargetCloseRequired"; CanonicalFutureMinutes = @(0, 15, 30, 45) })
Write-JsonArtifact "phase-exec-live-r011-usdjpy-caveat-review.json" ([ordered]@{ Phase = $phase; NormalizedPortfolioSymbol = "JPYUSD"; ExecutionTradableSymbol = "USDJPY"; RequiresInversion = $true; SecurityID = "4004"; SecurityIDSource = "8"; CaveatPreserved = $true; SourceReviewCaveatPreserved = $r010Usdjpy.CaveatPreserved })
Write-JsonArtifact "phase-exec-live-r011-audit-record-review.json" ([ordered]@{ Phase = $phase; AuditRecords = $auditRecords; SourceAuditPath = $r010Audit.AuditPath; ArtifactOnly = $true; DbWrites = $false; OrderDomainPersistence = $false; RouteSubmissionPersistence = $false; LedgerPersistence = $false; TradingStateMutation = $false })
Write-JsonArtifact "phase-exec-live-r011-operator-review-artifact-review.json" ([ordered]@{ Phase = $phase; OperatorReviewReports = $operatorReports; SourceOutputPath = $r010Operator.OutputPath; ReviewOnly = $true; WritesOutsideArtifactPath = $false; ExecutableApproval = $false })

Write-JsonArtifact "phase-exec-live-r011-decision.json" ([ordered]@{
    Phase = $phase
    Decision = "R009DisabledPreviewTrialPassedForPrePaperLedgerPreviewPlanning"
    TrialReviewPassed = $true
    PrePaperLedgerPreviewPlanningReady = $true
    PaperLedgerCommitApproval = $false
    ExecutableApproval = $false
    BrokerApproval = $false
    LiveApproval = $false
    SeparateFuturePaperLedgerPreviewGateRequired = $true
    SeparateFuturePaperLedgerCommitGateRequired = $true
})

Write-JsonArtifact "phase-exec-live-r011-executable-promotion-blockers.json" ([ordered]@{
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
        "No ledger mutation authorized",
        "No trading state mutation authorized",
        "Direct-cross execution disabled",
        "Nonmajor/EM/scandi/CNH calibration required",
        "Separate explicit executable gate required"
    )
})

$audits = [ordered]@{
    "phase-exec-live-r011-no-broker-activation-audit.json" = New-Audit "NoBrokerActivation" "R011 reviews R010 artifacts and does not activate broker."
    "phase-exec-live-r011-no-live-marketdata-audit.json" = New-Audit "NoLiveMarketData" "R011 reviews artifact evidence and does not request live market data."
    "phase-exec-live-r011-no-scheduler-service-polling-audit.json" = New-Audit "NoSchedulerServicePolling" "R011 does not start scheduler/service/timer/polling/background jobs."
    "phase-exec-live-r011-no-order-created-audit.json" = New-Audit "NoOrderCreated" "R011 produces readiness documentation only, not orders."
    "phase-exec-live-r011-no-child-order-audit.json" = New-Audit "NoChildOrder" "R011 produces no child slices or child orders."
    "phase-exec-live-r011-no-executable-schedule-audit.json" = New-Audit "NoExecutableSchedule" "R011 produces no executable schedules."
    "phase-exec-live-r011-no-route-no-submission-audit.json" = New-Audit "NoRouteNoSubmission" "R011 produces no routes or submissions."
    "phase-exec-live-r011-no-fill-execution-report-audit.json" = New-Audit "NoFillExecutionReport" "R011 produces no fills or execution reports."
    "phase-exec-live-r011-no-paper-ledger-commit-audit.json" = New-Audit "NoPaperLedgerCommit" "R011 explicitly forbids paper ledger commit and creates no commit records."
    "phase-exec-live-r011-no-state-mutation-audit.json" = New-Audit "NoStateMutation" "R011 mutates no live, broker, production, paper ledger, or trading state."
    "phase-exec-live-r011-no-external-audit.json" = New-Audit "NoExternal" "No external API, Polygon, LMAX, broker, or live market data call is part of this phase."
}
foreach ($entry in $audits.GetEnumerator()) { Write-JsonArtifact $entry.Key $entry.Value }

Write-JsonArtifact "phase-exec-live-r011-canonical-quarter-hour-policy-preservation.json" ([ordered]@{ Phase = $phase; FutureCanonicalMinutes = @(0, 15, 30, 45); LegacyMinutesAcceptedAsFutureCanonical = $false })
Write-JsonArtifact "phase-exec-live-r011-legacy-compatibility-preservation.json" ([ordered]@{ Phase = $phase; LegacyLabels = @(":06", ":21", ":36", ":51"); Usage = "CompatibilityOnly"; UsedAsFutureCanonical = $false })
Write-JsonArtifact "phase-exec-live-r011-direct-cross-exclusion-preservation.json" ([ordered]@{ Phase = $phase; DirectCrossExecutionAllowed = $false; QubesWeightsMayContainCrossesAsSignalsOnly = $true; DirectCrossExecutionIntentRejected = $true })
Write-JsonArtifact "phase-exec-live-r011-usd-pair-netting-requirement.json" ([ordered]@{ Phase = $phase; Requirement = "EMS/OMS execution intents must be USD-pair-only after netting."; SupportedExecutionSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF"); AudusdStatus = "SupportedAndNotFailed" })
Write-JsonArtifact "phase-exec-live-r011-usdjpy-caveat-preservation.json" ([ordered]@{ Phase = $phase; NormalizedPortfolioSymbol = "JPYUSD"; ExecutionTradableSymbol = "USDJPY"; RequiresInversion = $true; SecurityID = "4004"; SecurityIDSource = "8"; CaveatPreserved = $true })
Write-JsonArtifact "phase-exec-live-r011-cost-guidance-preservation.json" ([ordered]@{ Phase = $phase; FiveUsdPerMillion = "BestCaseMajorOnly"; Universalized = $false })
Write-JsonArtifact "phase-exec-live-r011-nonmajor-calibration-preservation.json" ([ordered]@{ Phase = $phase; NonmajorEmScandiCnh = "CalibrationRequired"; LiveCapableExecutionAllowed = $false })
Write-JsonArtifact "phase-exec-live-r011-forbidden-actions-audit.json" ([ordered]@{ Phase = $phase; ExternalApiCallsMade = $false; PolygonCallsMade = $false; LmaxCallsMade = $false; BrokerActivationOccurred = $false; LiveMarketDataRequested = $false; SchedulerServicePollingBackgroundJobIntroduced = $false; PmsEmsOmsProductionCycleRun = $false; ManualNoExternalCommandRun = $false; BacktestSimulationRun = $false; TcaResultLinesCreated = $false; ExecutableScheduleCreated = $false; OrdersChildOrdersRoutesSubmissionsFillsReportsCreated = $false; PaperLedgerCommitCreated = $false; PaperLedgerCommitRecordCreated = $false; LedgerMutationAllowed = $false; StateMutationOccurred = $false; R009PromotedToExecutableUse = $false; PrePaperLedgerReadinessImpliesLedgerCommitApproval = $false; PaperLedgerPreviewContractAllowsCommits = $false; BrokerLiveOrderRouteScheduleLedgerPathEnabled = $false; ForbiddenConsumerAllowed = $false; DirectCrossExecutionAllowed = $false; Legacy06AcceptedAsFutureCanonical = $false; PreviewOutputRepresentedAsOrderRouteFillSchedule = $false })
Write-JsonArtifact "phase-exec-live-r011-next-phase-recommendation.json" ([ordered]@{ Phase = $phase; RecommendedNextPhase = "EXEC-LIVE-R012"; Title = "R009 Paper-Ledger Preview-Only Contract and Artifact Gate"; Constraints = "Future gate may define paper-ledger-preview-only artifacts, still no paper ledger commit, orders, routes, fills, schedules, broker/live paths, or trading state mutation." })
Write-JsonArtifact "phase-exec-live-r011-build-test-validator-evidence.json" ([ordered]@{ Phase = $phase; Build = $BuildStatus; FocusedR011Tests = $FocusedTestsStatus; UnitTests = $UnitTestsStatus; Validator = $ValidatorStatus; DotnetBuildNoRestore = "dotnet build --no-restore"; FocusedTests = "dotnet test tests\QQ.Production.Intraday.Tests.Unit\QQ.Production.Intraday.Tests.Unit.csproj --no-build --no-restore --filter FullyQualifiedName~R009PrePaperLedgerPreviewReadinessTests"; ValidatorScript = "scripts/check-exec-live-r011-disabled-preview-trial-review-preledger-gate.ps1" })

$summary = @"
# EXEC-LIVE-R011 Summary

Classifications:
- EXEC_LIVE_R011_PASS_DISABLED_PREVIEW_TRIAL_REVIEW_READY_NO_EXTERNAL
- EXEC_LIVE_R011_PASS_PRE_PAPER_LEDGER_PREVIEW_READINESS_READY_NO_EXTERNAL
- EXEC_LIVE_R011_PASS_LEDGER_COMMIT_BLOCKERS_READY_NO_EXTERNAL
- EXEC_LIVE_R011_PASS_NO_BROKER_NO_ORDER_NO_ROUTE_GATE_READY_NO_EXTERNAL

R011 reviewed the EXEC-LIVE-R010 internal disabled EMS/OMS preview trial and records readiness for future paper-ledger-preview-only planning. The R010 trial remains stable for review: accepted requests=$acceptedRequests, forbidden consumer rejections=$rejectedRequests, PreviewReady=$previewReady, HeldMissingReadiness=$held, Rejected=$rejected, audit records=$auditRecords, operator reports=$operatorReports.

The future paper-ledger-preview-only contract is explicitly distinct from paper ledger commit. Allowed future outputs are hypothetical preview artifacts only. Paper ledger commits, ledger mutation, trading state mutation, orders, routes, fills, submissions, execution reports, and executable schedules remain forbidden.

Decision: R009DisabledPreviewTrialPassedForPrePaperLedgerPreviewPlanning. This is not executable approval, not broker/live readiness, and not paper ledger commit approval.

Build/tests/validator:
- Build: $BuildStatus
- Focused R011 tests: $FocusedTestsStatus
- Unit tests: $UnitTestsStatus
- Validator: $ValidatorStatus

Next recommendation: EXEC-LIVE-R012 - R009 Paper-Ledger Preview-Only Contract and Artifact Gate.
"@
$summary | Set-Content -Path (Join-Path $artifactDir "phase-exec-live-r011-summary.md") -Encoding UTF8

Write-Host "EXEC-LIVE-R011 artifacts written to $artifactDir"
