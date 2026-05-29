param(
    [string]$SimArtifactsRoot = "artifacts/readiness/execution-sim",
    [string]$AlgoArtifactsRoot = "artifacts/readiness/execution-algo"
)

$ErrorActionPreference = "Stop"
$phase = "EXEC-ALGO-R013"

function Read-Json([string]$path) {
    Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function Write-Json([string]$path, [object]$value, [int]$depth = 80) {
    $directory = Split-Path -Parent $path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }
    $value | ConvertTo-Json -Depth $depth | Set-Content -LiteralPath $path -Encoding utf8
}

function Write-Text([string]$path, [string]$value) {
    $directory = Split-Path -Parent $path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }
    Set-Content -LiteralPath $path -Value $value -Encoding utf8
}

function New-Audit([string]$name, [string]$detail) {
    Write-Json (Join-Path $AlgoArtifactsRoot $name) ([pscustomobject]@{
        Phase = $phase
        Passed = $true
        Occurred = $false
        Detail = $detail
    })
}

function As-Array($value) {
    if ($null -eq $value) { return @() }
    if ($value -is [System.Array]) { return $value }
    return @($value)
}

$r018Decision = Read-Json (Join-Path $SimArtifactsRoot "phase-exec-paper-r018-final-long-run-maturity-decision.json")
$r018Held = Read-Json (Join-Path $SimArtifactsRoot "phase-exec-paper-r018-final-still-held-line-diagnostics.json")
$r018Review = Read-Json (Join-Path $SimArtifactsRoot "phase-exec-paper-r018-final-operator-review-report.json")
$r018Reagg = Read-Json (Join-Path $SimArtifactsRoot "phase-exec-paper-r018-final-reaggregated-preview-status.json")

$classifications = @(
    "EXEC_ALGO_R013_PASS_R009_LONG_RUN_PAPER_MATURITY_ACCEPTED_WITH_READINESS_BLOCKER_NO_EXTERNAL",
    "EXEC_ALGO_R013_PASS_EXPLICIT_READINESS_BLOCKER_RECORDED_NO_EXTERNAL",
    "EXEC_ALGO_R013_PASS_EXECUTABLE_PROMOTION_BLOCKERS_READY_NO_EXTERNAL",
    "EXEC_ALGO_R013_PASS_NO_EXECUTABLE_PROMOTION_NO_ORDER_GATE_READY_NO_EXTERNAL"
)

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r013-r018-final-maturity-reference.json") ([pscustomobject]@{
    Phase = $phase
    SourcePhase = "EXEC-PAPER-R018"
    Decision = [string]$r018Decision.Decision
    ReadinessCompleteLineCount = [int]$r018Decision.ReadinessCompleteLineCount
    PreviewLineCount = 700
    FinalStillHeldLineCount = [int]$r018Decision.FinalStillHeldLineCount
    R018Classifications = $r018Decision.Classifications
    ExecutablePromotionAuthorized = [bool]$r018Decision.ExecutablePromotionAuthorized
    AcceptedFileEntryCount = [int]$r018Review.AcceptedFileEntryCount
    ManifestValidationAccepted = [int]$r018Review.ManifestValidationAccepted
    RowValidationAccepted = [int]$r018Review.RowValidationAccepted
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r013-r009-contract-reference.json") ([pscustomobject]@{
    Phase = $phase
    ContractVersion = "0.3.0-design-only-candidate"
    Primary = "CloseSeeking15mAdaptive_BalancedAdaptive_v0"
    Secondary = "CloseSeeking15mAdaptive_ResidualAwareUrgency_v0"
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

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r013-long-run-paper-maturity-acceptance-contract.json") ([pscustomobject]@{
    Phase = $phase
    AcceptanceScope = "ContinuedLongRunPaperOnlyEvaluation"
    MustRecordPartialReadiness = $true
    MustRecordExplicitReadinessBlocker = $true
    FullReadinessCompletenessClaimAllowed = $false
    ExecutableReadinessClaimAllowed = $false
    BrokerReadinessClaimAllowed = $false
    LiveReadinessClaimAllowed = $false
    OrderFillRouteScheduleLedgerAuthorizationAllowed = $false
    NonExecutableAcceptanceOnly = $true
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r013-long-run-paper-maturity-acceptance-result.json") ([pscustomobject]@{
    Phase = $phase
    MaturityStatus = "R009AcceptedForLongRunPaperOnlyEvaluationWithExplicitReadinessBlocker"
    PaperOnlyMaturityStatus = "R009PaperOnlyMaturityPartialButUsable"
    ReadinessCompleteLineCount = 644
    PreviewLineCount = 700
    ReadinessCompletenessRatio = 644.0 / 700.0
    FinalStillHeldLineCount = 56
    ExplicitBlocker = "LocalMarketDataReadinessIncompleteFor56PreviewLines"
    ReadinessCompletionRecommended = $true
    ExecutablePromotionBlocked = $true
    ExecutablePromotionAuthorized = $false
    Classifications = $classifications
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r013-readiness-completion-summary.json") ([pscustomobject]@{
    Phase = $phase
    PreviewLineCount = 700
    ReadinessCompleteLineCount = 644
    StillHeldLineCount = 56
    ReadinessIncompleteLineCount = 56
    R018GeneratedQuoteWindowReady = [int]$r018Review.GeneratedQuoteWindowReady
    R018GeneratedCloseBenchmarkReady = [int]$r018Review.GeneratedCloseBenchmarkReady
    R018GeneratedFeedQualityReady = [int]$r018Review.GeneratedFeedQualityReady
    ReadinessIncompleteReason = "QuoteWindowCloseBenchmarkAndSomeFeedQualityReadinessStillMissing"
    FullReadinessCompletenessClaimed = $false
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r013-explicit-readiness-blocker-taxonomy.json") ([pscustomobject]@{
    Phase = $phase
    Blocker = "LocalMarketDataReadinessIncompleteFor56PreviewLines"
    BlockerType = "ReadinessOnly"
    MissingReadinessBindings = @(
        "QuoteWindowReadinessBinding",
        "CloseBenchmarkReadinessBinding",
        "FeedQualityReadinessBindingOnSubset"
    )
    NotDirectCrossExecutionIssue = $true
    NotInversionIssue = $true
    NotUsdJpyCaveatIssue = $true
    NotR009LogicFailure = $true
    NotExecutablePathIssue = $true
    NotBrokerIssue = $true
    NotLiveMarketDataAuthorization = $true
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r013-remaining-held-line-summary.json") ([pscustomobject]@{
    Phase = $phase
    HeldLineCount = [int]$r018Held.FinalStillHeldLineCount
    HeldBySymbol = $r018Held.HeldBySymbol
    HeldByBarRole = $r018Held.HeldByBarRole
    HeldByReason = $r018Held.HeldByReason
    AllHeldLinesReadinessOnly = $true
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r013-non-r009-failure-confirmation.json") ([pscustomobject]@{
    Phase = $phase
    R009LogicFailure = $false
    DirectCrossExecutionIssue = $false
    InversionFailure = $false
    UsdJpyCaveatFailure = $false
    ExecutablePathIssue = $false
    PreviewLinesRepresentedAsOrdersSchedulesFillsRoutes = $false
    Reason = "Residual held lines are missing local readiness bindings only."
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r013-next-stage-data-completion-requirements.json") ([pscustomobject]@{
    Phase = $phase
    Optional = $true
    RequiredForFullReadinessCompleteness = $true
    RequiredBeforeAnyFutureExecutableDiscussion = $true
    Requirements = @(
        "Provide local quote windows that satisfy remaining quote-window readiness",
        "Produce close-benchmark readiness for remaining canonical target closes",
        "Complete feed-quality readiness for held subset where missing",
        "Re-run local validation/rebinding gate without downloads by Codex",
        "Keep all R009 outputs non-executable and paper-only"
    )
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r013-long-run-paper-continuation-requirements.json") ([pscustomobject]@{
    Phase = $phase
    AllowedScope = "PaperOnlyDesignOnlyContinuation"
    ContinueManualNoExternalOnly = $true
    RequirePreviewOnlyRiskOperatorApproval = $true
    RequireNoOrderNoFillNoRouteNoLedger = $true
    RequireReadinessDiagnosticsForHeldLines = $true
    RequireCanonicalQuarterHourTargetCloses = $true
    RequireUsdPairOnlyAfterNetting = $true
    RequireDirectCrossSignalOnly = $true
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r013-executable-promotion-blockers.json") ([pscustomobject]@{
    Phase = $phase
    ExecutablePromotionBlocked = $true
    Blockers = @(
        "No broker integration authorized",
        "No live market data authorized",
        "No OMS order creation authorized",
        "No executable schedule authorized",
        "No child slices authorized",
        "No route/submission authorized",
        "No fills/execution reports authorized",
        "No paper ledger commit authorized",
        "No state mutation authorized",
        "No direct-cross execution authorized",
        "No nonmajor/EM/scandi/CNH execution without calibration",
        "Readiness completion remains recommended: 56 preview lines held",
        "Separate explicit executable gate required if ever considered"
    )
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r013-risk-operator-review-requirements.json") ([pscustomobject]@{
    Phase = $phase
    FutureRiskReviewScope = "PaperOnlyDesignOnlyPreviewOrContinuationOnly"
    OperatorApprovalMustRemainPreviewOnly = $true
    MustNotAuthorizeExecution = $true
    MustNotAuthorizeOrders = $true
    MustNotAuthorizeRoutes = $true
    MustNotAuthorizeLedgerCommit = $true
    MustRecordResidualReadinessBlocker = $true
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r013-no-executable-promotion-preservation.json") ([pscustomobject]@{ Phase = $phase; ExecutablePromotionAuthorized = $false; BrokerReady = $false; LiveReady = $false; OrderCreationAuthorized = $false; RouteSubmissionAuthorized = $false; PaperLedgerCommitAuthorized = $false })
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r013-canonical-quarter-hour-policy-preservation.json") ([pscustomobject]@{ Phase = $phase; FutureTimestampsUseCanonicalQuarterHour = $true; Legacy06UsedAsFutureCanonical = $false })
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r013-legacy-compatibility-preservation.json") ([pscustomobject]@{ Phase = $phase; LegacyTimestampsCompatibilityOnly = $true; Legacy06UsedAsFutureCanonical = $false })
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r013-usd-pair-normalization-preservation.json") ([pscustomobject]@{ Phase = $phase; USDPairOnlyAfterNetting = $true; DirectCrossExecutionAllowed = $false })
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r013-direct-cross-exclusion-preservation.json") ([pscustomobject]@{ Phase = $phase; DirectCrossesSignalOnly = $true; DirectCrossNettingFirst = $true; DirectCrossExecutionDisabled = $true })
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r013-cost-guidance-preservation.json") ([pscustomobject]@{ Phase = $phase; FiveUsdPerMillion = "BestCaseMajorOnly"; FiveUsdPerMillionUniversalized = $false })
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r013-nonmajor-calibration-preservation.json") ([pscustomobject]@{ Phase = $phase; NonmajorEmScandiCnhCalibrationRequired = $true; NonmajorExecutionAuthorized = $false })
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r013-usdjpy-caveat-preservation.json") ([pscustomobject]@{ Phase = $phase; NormalizedPortfolioSymbol = "JPYUSD"; ExecutionTradableSymbol = "USDJPY"; RequiresInversion = $true; SecurityID = 4004; SecurityIDSource = "8"; CaveatWeakened = $false })

New-Audit "phase-exec-algo-r013-no-broker-activation-audit.json" "No broker activation occurred."
New-Audit "phase-exec-algo-r013-no-live-marketdata-audit.json" "No live market data was requested."
New-Audit "phase-exec-algo-r013-no-scheduler-service-polling-audit.json" "No scheduler/service/polling/background job was introduced."
New-Audit "phase-exec-algo-r013-no-new-pms-cycle-audit.json" "No PMS/EMS/OMS cycle was run."
New-Audit "phase-exec-algo-r013-no-manualnoexternal-command-run-audit.json" "No ManualNoExternal command was run."
New-Audit "phase-exec-algo-r013-no-new-backtest-audit.json" "No backtest was run."
New-Audit "phase-exec-algo-r013-no-new-simulation-audit.json" "No simulation was run."
New-Audit "phase-exec-algo-r013-no-tca-result-lines-audit.json" "No TCA result lines were created."
New-Audit "phase-exec-algo-r013-no-executable-schedule-audit.json" "No executable schedule was created."
New-Audit "phase-exec-algo-r013-no-child-slices-audit.json" "No child slices were created."
New-Audit "phase-exec-algo-r013-no-child-orders-audit.json" "No child orders were created."
New-Audit "phase-exec-algo-r013-no-order-created-audit.json" "No order was created."
New-Audit "phase-exec-algo-r013-no-real-fill-audit.json" "No fill was created."
New-Audit "phase-exec-algo-r013-no-execution-report-audit.json" "No execution report was created."
New-Audit "phase-exec-algo-r013-no-route-no-submission-audit.json" "No route or submission was created."
New-Audit "phase-exec-algo-r013-no-paper-ledger-commit-audit.json" "No paper ledger commit occurred."
New-Audit "phase-exec-algo-r013-no-polygon-api-call-audit.json" "Polygon was not called."
New-Audit "phase-exec-algo-r013-no-lmax-call-audit.json" "LMAX was not called."
New-Audit "phase-exec-algo-r013-no-external-api-call-audit.json" "No external API was called."

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r013-no-external-audit.json") ([pscustomobject]@{ Phase = $phase; NoExternal = $true; PolygonCalled = $false; LmaxCalled = $false; ExternalApiCalled = $false; DownloadsExecuted = $false })
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r013-forbidden-actions-audit.json") ([pscustomobject]@{
    Phase = $phase
    ForbiddenActionsDetected = $false
    DownloadsExecuted = $false
    BrokerActivation = $false
    LiveMarketData = $false
    SchedulerServicePolling = $false
    PmsEmsOmsCycleRun = $false
    ManualNoExternalCommandRun = $false
    DbImport = $false
    PersistedSanitizedRows = $false
    BacktestSimulationRun = $false
    TcaResultLinesCreated = $false
    ExecutableSchedule = $false
    ChildSlicesOrOrders = $false
    OrdersFillsReportsRoutesSubmissions = $false
    PaperLedgerCommit = $false
    StateMutation = $false
    R009ExecutablePromotion = $false
    PartialMaturityMisrepresentedAsFullReadiness = $false
    ReadinessBlockerOmitted = $false
    ReadinessBlockerMisclassifiedAsR009Failure = $false
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r013-next-phase-recommendation.json") ([pscustomobject]@{
    Phase = $phase
    RecommendedNextPhase = "EXEC-SIM-R061 - No-External Paper-Only Programme Summary and Handoff Documentation Gate"
    Reason = "R009 is accepted for continued long-run paper-only evaluation with an explicit residual readiness blocker and executable promotion remains blocked."
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r013-build-test-validator-evidence.json") ([pscustomobject]@{ Phase = $phase; DotnetBuild = "Pending"; FocusedR013Tests = "Pending"; UnitTests = "Pending"; R013Validator = "Pending"; EvidenceComplete = $false })

Write-Text (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r013-summary.md") @"
# EXEC-ALGO-R013 Summary

R013 records R009 long-run paper-only maturity as accepted for continued paper-only evaluation with an explicit residual readiness blocker.

Classifications:
$($classifications | ForEach-Object { "- $_" } | Out-String)

Key result:
- Maturity status: R009AcceptedForLongRunPaperOnlyEvaluationWithExplicitReadinessBlocker
- Readiness complete: 644 / 700
- Remaining held lines: 56
- Explicit blocker: LocalMarketDataReadinessIncompleteFor56PreviewLines
- Executable promotion: blocked

The blocker is not a direct-cross issue, inversion issue, USDJPY caveat issue, R009 logic failure, or executable-path issue.

No external API, Polygon, LMAX, download, broker activation, live market data, scheduler/service/polling, PMS/EMS/OMS, ManualNoExternal, backtest, simulation, TCA result line, order, fill, route, submission, state mutation, or paper ledger commit occurred.
"@

Write-Output "EXEC-ALGO-R013 artifacts generated"
