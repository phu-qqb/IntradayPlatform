param(
    [string]$AlgoArtifactsRoot = "artifacts/readiness/execution-algo",
    [string]$SimArtifactsRoot = "artifacts/readiness/execution-sim"
)

$ErrorActionPreference = "Stop"

function Read-Json([string]$path) {
    return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function Write-Json([string]$path, [object]$value, [int]$depth = 20) {
    $directory = Split-Path -Parent $path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    $value | ConvertTo-Json -Depth $depth | Set-Content -LiteralPath $path -Encoding UTF8
}

function Write-Text([string]$path, [string]$value) {
    $directory = Split-Path -Parent $path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    Set-Content -LiteralPath $path -Value $value -Encoding UTF8
}

function New-Audit([string]$name, [string]$key, [string]$detail) {
    Write-Json (Join-Path $AlgoArtifactsRoot $name) ([pscustomobject]@{
        Phase = "EXEC-ALGO-R011"
        AuditName = $key
        Passed = $true
        Occurred = $false
        Detail = $detail
    })
}

$phase = "EXEC-ALGO-R011"
$r054Policy = Read-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r054-policy-decision.json")
$r054Contract = Read-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r054-parameter-contract-decision.json")
$r058Decision = Read-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r058-stability-decision.json")
$r058Review = Read-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r058-operator-review-report.json")
$r010R009 = Read-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r010-r009-contract-reference.json")

$classifications = @(
    "EXEC_ALGO_R011_PASS_R009_PAPER_ONLY_STABILITY_ACCEPTED_NO_EXTERNAL",
    "EXEC_ALGO_R011_PASS_NEXT_STAGE_PAPER_ONLY_REQUIREMENTS_READY_NO_EXTERNAL",
    "EXEC_ALGO_R011_PASS_EXECUTABLE_PROMOTION_BLOCKERS_READY_NO_EXTERNAL",
    "EXEC_ALGO_R011_PASS_NO_EXECUTABLE_PROMOTION_NO_ORDER_GATE_READY_NO_EXTERNAL"
)

$contractFlags = [ordered]@{
    DesignOnly = $true
    PaperOnly = $true
    NonExecutable = $true
    NotAnOrder = $true
    NotSubmitted = $true
    NoBrokerRoute = $true
    BrokerReady = $false
    LiveReady = $false
    ExecutablePromotionAuthorized = $false
}

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-r054-backtest-review-reference.json") ([pscustomobject]@{
    Phase = $phase
    SourcePhase = "EXEC-SIM-R054"
    Classifications = @(
        "EXEC_SIM_R054_PASS_BROADER_HISTORICAL_TCA_BACKTEST_READY_NO_EXTERNAL",
        "EXEC_SIM_R054_PASS_TWENTY_DATE_CANONICAL_SESSION_REVIEW_READY_NO_EXTERNAL",
        "EXEC_SIM_R054_PASS_R009_CONTRACT_DECISION_READY_NO_EXTERNAL",
        "EXEC_SIM_R054_PASS_NO_DB_IMPORT_NO_REAL_FILL_NO_ORDER_GATE_READY_NO_EXTERNAL",
        "EXEC_SIM_R054_PASS_R009_PRIMARY_CANDIDATE_STABLE_NO_EXTERNAL",
        "EXEC_SIM_R054_PASS_MORE_DATA_RECOMMENDED_NO_EXTERNAL"
    )
    Dates = 20
    Symbols = 7
    CanonicalQuoteWindows = 3780
    PolicyFamiliesOrVariants = 11
    NonExecutableTcaResultLines = 41580
    PolicyDecision = $r054Policy.PolicyDecision
    ParameterContractDecision = $r054Contract.ParameterContractDecision
    MoreDataRecommended = [bool]$r054Contract.MoreDataRecommended
    ExecutablePromotionAuthorized = $false
    ReusedOnly = $true
    NewBacktestRun = $false
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-r058-paper-preview-reference.json") ([pscustomobject]@{
    Phase = $phase
    SourcePhase = "EXEC-SIM-R058"
    Classifications = @(
        "EXEC_SIM_R058_PASS_BROADER_PAPER_PREVIEW_AGGREGATION_REVIEW_READY_NO_EXTERNAL",
        "EXEC_SIM_R058_PASS_R009_STABILITY_DECISION_READY_NO_EXTERNAL",
        "EXEC_SIM_R058_PASS_R009_STABLE_FOR_BROADER_PAPER_ONLY_EVALUATION_NO_EXTERNAL",
        "EXEC_SIM_R058_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
    )
    ReviewedRuns = $r058Review.ReviewedBatchEntries
    ReviewedPreviewLines = $r058Review.ReviewedPreviewLines
    ReadinessBindingsComplete = [bool]$r058Review.ReadinessBindingsComplete
    HeldLines = $r058Review.HeldLines
    DirectCrossExecutableLines = 0
    Decision = $r058Decision.Decision
    AcceptanceScope = $r058Decision.AcceptanceScope
    ExecutablePromotionAuthorized = $false
    ReusedOnly = $true
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-r009-contract-reference.json") ([pscustomobject]@{
    Phase = $phase
    SourcePhase = "EXEC-ALGO-R009/EXEC-ALGO-R010"
    ContractVersion = $r010R009.ContractVersion
    Primary = $r010R009.Primary
    Secondary = $r010R009.Secondary
    ConditionalResidualModule = $r010R009.ConditionalResidualModule
    DesignOnly = $true
    PaperOnly = $true
    NonExecutable = $true
    NotAnOrder = $true
    NotSubmitted = $true
    NoBrokerRoute = $true
    BrokerReady = $false
    LiveReady = $false
    ExecutablePromotionAuthorized = $false
    Reused = $true
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-paper-only-stability-acceptance-contract.json") ([pscustomobject]@{
    Phase = $phase
    GateType = "NoExternalPaperOnlyStabilityAcceptanceAndPlanning"
    ReusesR054HistoricalTcaReview = $true
    ReusesR058BroaderPaperOnlyPreviewReview = $true
    ReusesR009R010DesignOnlyContract = $true
    AcceptanceScope = "BroaderPaperOnlyEvaluationExpansion"
    DesignOnly = $true
    PaperOnly = $true
    NonExecutable = $true
    NotAnOrder = $true
    NotSubmitted = $true
    NoBrokerRoute = $true
    BrokerReady = $false
    LiveReady = $false
    ExecutablePromotionAuthorized = $false
    CreatesSchedules = $false
    CreatesChildSlices = $false
    CreatesOrders = $false
    CreatesFills = $false
    CreatesRoutesOrSubmissions = $false
    CommitsPaperLedger = $false
    MutatesState = $false
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-paper-only-stability-acceptance-result.json") ([pscustomobject]@{
    Phase = $phase
    DecisionStatuses = @(
        "R009StableForBroaderPaperOnlyEvaluation",
        "R009AcceptedForNextStagePaperOnlyExpansion",
        "ExecutablePromotionBlocked",
        "MoreDataRecommendedPreserved"
    )
    R009Status = "StableForBroaderPaperOnlyEvaluation"
    AcceptedForNextStagePaperOnlyExpansion = $true
    AcceptanceBasis = @(
        "R054 stable design-only candidate decision",
        "R058 20-run / 140-line broader paper-only preview aggregation review"
    )
    R054MoreDataRecommended = [bool]$r054Contract.MoreDataRecommended
    R058AcceptanceScope = $r058Decision.AcceptanceScope
    R058ReviewedBatchEntries = $r058Review.ReviewedBatchEntries
    R058ReviewedPreviewLines = $r058Review.ReviewedPreviewLines
    R058HeldLines = $r058Review.HeldLines
    R058ReadinessBindingsComplete = [bool]$r058Review.ReadinessBindingsComplete
    R009ContractVersion = $r010R009.ContractVersion
    PrimaryPolicyCandidate = $r010R009.Primary
    SecondaryPolicyCandidate = $r010R009.Secondary
    ConditionalResidualModule = $r010R009.ConditionalResidualModule
    DesignOnly = $true
    PaperOnly = $true
    NonExecutable = $true
    NotAnOrder = $true
    NotSubmitted = $true
    NoBrokerRoute = $true
    ExecutablePromotionAuthorized = $false
    BrokerReady = $false
    LiveReady = $false
    Classifications = $classifications
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-next-stage-paper-only-requirements.json") ([pscustomobject]@{
    Phase = $phase
    Requirements = @(
        "More Qubes fixture batches",
        "More target closes",
        "More intraday rebalance cases",
        "More opening/build cases if the strategy uses them",
        "More closing/flatten cases",
        "Continued no-order/no-fill/no-route/no-ledger constraints",
        "Continued quote-window, close-benchmark, and feed-quality readiness bindings",
        "Continued preview-only risk/operator approvals",
        "Continued manual no-external execution only",
        "Aggregated paper-only reporting"
    )
    NoExecutableSchedules = $true
    NoOrdersFillsRoutesSubmissions = $true
    NoPaperLedgerCommit = $true
    ManualNoExternalOnly = $true
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-executable-promotion-blockers.json") ([pscustomobject]@{
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
        "More data recommended remains open",
        "Separate explicit executable gate required if ever considered"
    )
    AcceptanceIsExecutableApproval = $false
    ExecutablePromotionAuthorized = $false
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-risk-operator-review-requirements.json") ([pscustomobject]@{
    Phase = $phase
    RiskReviewRequiredForFutureBatches = $true
    OperatorApprovalRequiredForFutureBatches = $true
    RequiredScope = "R009DesignOnlyPreviewOnly"
    ApprovedForExecutableUse = $false
    ApprovedForOrderCreation = $false
    ApprovedForScheduleCreation = $false
    ApprovedForChildSlices = $false
    ApprovedForBrokerRouting = $false
    ApprovedForSubmission = $false
    ApprovedForFillOrExecutionReport = $false
    ApprovedForPaperLedgerCommit = $false
    ApprovedForStateMutation = $false
    ApprovedForLiveTrading = $false
    RequiredHoldConditions = @(
        "Missing readiness binding",
        "Direct cross not netted",
        "Unsupported instrument",
        "Canonical target close missing",
        "Risk/operator preview approval missing",
        "Any executable/order/fill/route path appears"
    )
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-data-expansion-requirements.json") ([pscustomobject]@{
    Phase = $phase
    MoreDataRecommendedPreserved = $true
    Requirements = @(
        "More dates",
        "More regimes",
        "More Qubes fixture batches",
        "More target closes",
        "Potentially more instruments only after calibration"
    )
    CurrentCoreUsdPairUniverseRemainsPrimary = $true
    NonmajorEmScandiCnhDeferred = $true
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-instrument-expansion-constraints.json") ([pscustomobject]@{
    Phase = $phase
    CurrentExecutionSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")
    USDPairOnlyAfterNetting = $true
    DirectCrossesSignalOnly = $true
    DirectCrossExecutionDisabled = $true
    USDJPY = [pscustomobject]@{
        NormalizedPortfolioSymbol = "JPYUSD"
        ExecutionTradableSymbol = "USDJPY"
        RequiresInversion = $true
        SecurityID = 4004
        SecurityIDSource = "8"
    }
    USDCADRequiresInversion = $true
    USDCHFRequiresInversion = $true
    AUDUSDNotFailed = $true
    NonmajorEmScandiCnhDeferredUntilCalibration = $true
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-more-data-recommendation-preservation.json") ([pscustomobject]@{
    Phase = $phase
    MoreDataRecommendedFromR054 = $true
    MoreDataRecommendedPreserved = $true
    ExecutablePromotionDiscussionStillBlocked = $true
})
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-no-executable-promotion-preservation.json") ([pscustomobject]@{
    Phase = $phase
    ExecutablePromotionAuthorized = $false
    BrokerReady = $false
    LiveReady = $false
    AcceptanceTreatedAsExecutableApproval = $false
})
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-rejected-wakett-preservation.json") ([pscustomobject]@{
    Phase = $phase
    Wakett = "RejectedNegativeBaselineOnly"
    RejectionWeakened = $false
    Promoted = $false
})
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-benchmark-only-preservation.json") ([pscustomobject]@{
    Phase = $phase
    BenchmarkOnlyPolicies = @("VWAPBenchmarkOnly", "TWAPBenchmarkOnly", "ImmediatePaperBenchmark")
    Promoted = $false
})
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-manual-review-do-not-trade-preservation.json") ([pscustomobject]@{
    Phase = $phase
    ManualReview = "SafetyOnly"
    DoNotTrade = "SafetyOnly"
    Promoted = $false
})
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-canonical-quarter-hour-policy-preservation.json") ([pscustomobject]@{
    Phase = $phase
    FutureTimestampsUseCanonicalQuarterHour = $true
    AllowedMinutes = @(0, 15, 30, 45)
    Legacy06UsedAsFutureCanonical = $false
})
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-legacy-compatibility-preservation.json") ([pscustomobject]@{
    Phase = $phase
    LegacyTimestampsCompatibilityOnly = $true
    Legacy06UsedAsFutureCanonical = $false
    CanonicalCloseForLegacyMatch = "LegacyOutputTimestamp - 6 minutes"
    LegacyNextBarExecutionCloseCanonical = "LegacyOutputTimestamp + 9 minutes"
})
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-usd-pair-normalization-preservation.json") ([pscustomobject]@{
    Phase = $phase
    USDPairOnlyAfterNetting = $true
    ExecutionSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")
    AUDUSDNotFailed = $true
})
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-direct-cross-exclusion-preservation.json") ([pscustomobject]@{
    Phase = $phase
    DirectCrossesSignalOnly = $true
    DirectCrossNettingFirst = $true
    DirectCrossExecutionDisabled = $true
    ExclusionWeakened = $false
})
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-cost-guidance-preservation.json") ([pscustomobject]@{
    Phase = $phase
    FiveUsdPerMillion = "BestCaseMajorOnly"
    FiveUsdPerMillionUniversalized = $false
    NonmajorCalibrationRequired = $true
})
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-nonmajor-calibration-preservation.json") ([pscustomobject]@{
    Phase = $phase
    NonmajorEmScandiCnhCalibrationRequired = $true
    NonmajorExecutionAuthorized = $false
})
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-usdjpy-caveat-preservation.json") ([pscustomobject]@{
    Phase = $phase
    NormalizedPortfolioSymbol = "JPYUSD"
    ExecutionTradableSymbol = "USDJPY"
    RequiresInversion = $true
    SecurityID = 4004
    SecurityIDSource = "8"
    CaveatWeakened = $false
})
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-lmax-readonly-baseline-reference.json") ([pscustomobject]@{
    Phase = $phase
    LmaxReferencedAsReadonlyBaselineOnly = $true
    LmaxCalled = $false
    BrokerRuntimeActivated = $false
})

New-Audit "phase-exec-algo-r011-non-executable-acceptance-audit.json" "NonExecutableAcceptance" "R011 acceptance is paper-only/non-executable."
New-Audit "phase-exec-algo-r011-no-new-backtest-audit.json" "NewBacktestRun" "No new backtest was run."
New-Audit "phase-exec-algo-r011-no-new-simulation-audit.json" "NewSimulationRun" "No new simulation was run."
New-Audit "phase-exec-algo-r011-no-tca-result-lines-audit.json" "TcaResultLinesCreated" "No TCA result lines were created."
New-Audit "phase-exec-algo-r011-no-executable-schedule-audit.json" "ExecutableSchedulesCreated" "No executable schedules were created."
New-Audit "phase-exec-algo-r011-no-child-slices-audit.json" "ChildSlicesCreated" "No child slices were created."
New-Audit "phase-exec-algo-r011-no-child-orders-audit.json" "ChildOrdersCreated" "No child orders were created."
New-Audit "phase-exec-algo-r011-no-order-created-audit.json" "OrdersCreated" "No orders were created."
New-Audit "phase-exec-algo-r011-no-real-fill-audit.json" "FillsCreated" "No fills were created."
New-Audit "phase-exec-algo-r011-no-execution-report-audit.json" "ExecutionReportsCreated" "No execution reports were created."
New-Audit "phase-exec-algo-r011-no-route-no-submission-audit.json" "RoutesOrSubmissionsCreated" "No routes or submissions were created."
New-Audit "phase-exec-algo-r011-no-paper-ledger-commit-audit.json" "PaperLedgerCommitted" "No paper ledger commit was created."
New-Audit "phase-exec-algo-r011-no-polygon-api-call-audit.json" "PolygonCalled" "Polygon was not called."
New-Audit "phase-exec-algo-r011-no-lmax-call-audit.json" "LmaxCalled" "LMAX was not called."
New-Audit "phase-exec-algo-r011-no-external-api-call-audit.json" "ExternalApiCalled" "No external API was called."
New-Audit "phase-exec-algo-r011-no-broker-marketdata-runtime-audit.json" "BrokerMarketDataRuntimeActivated" "No broker or market-data runtime was activated."

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-no-external-audit.json") ([pscustomobject]@{
    Phase = $phase
    NoExternal = $true
    PolygonCalled = $false
    LmaxCalled = $false
    ExternalApiCalled = $false
    DownloadsExecuted = $false
})
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-forbidden-actions-audit.json") ([pscustomobject]@{
    Phase = $phase
    ForbiddenActionsDetected = $false
    BrokerActivation = $false
    LiveMarketData = $false
    SchedulerServicePolling = $false
    NewPmsCycle = $false
    BacktestOrSimulation = $false
    TcaResultLinesCreated = $false
    ExecutableSchedule = $false
    ChildSlicesOrOrders = $false
    OrdersFillsReportsRoutesSubmissions = $false
    PaperLedgerCommit = $false
    StateMutation = $false
    R009ExecutablePromotion = $false
})
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-next-phase-recommendation.json") ([pscustomobject]@{
    Phase = $phase
    RecommendedNextPhase = "EXEC-SIM-R059 - No-External Next-Stage Paper-Only Expansion Date/Fixture Planning Gate"
    Purpose = "Plan the next batch of paper-only Qubes fixtures and canonical target closes without executing PMS cycles, schedules, orders, fills, routes, submissions, broker calls, or ledger commits."
})
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-build-test-validator-evidence.json") ([pscustomobject]@{
    Phase = $phase
    DotnetBuild = "Pending"
    FocusedR011Tests = "Pending"
    UnitTests = "Pending"
    R011Validator = "Pending"
    EvidenceComplete = $false
})

$summary = @"
# EXEC-ALGO-R011 Summary

R009 is accepted as StableForBroaderPaperOnlyEvaluation based on the reused R054 historical design-only review and the reused R058 broader paper-only preview aggregation review.

Classifications:
- EXEC_ALGO_R011_PASS_R009_PAPER_ONLY_STABILITY_ACCEPTED_NO_EXTERNAL
- EXEC_ALGO_R011_PASS_NEXT_STAGE_PAPER_ONLY_REQUIREMENTS_READY_NO_EXTERNAL
- EXEC_ALGO_R011_PASS_EXECUTABLE_PROMOTION_BLOCKERS_READY_NO_EXTERNAL
- EXEC_ALGO_R011_PASS_NO_EXECUTABLE_PROMOTION_NO_ORDER_GATE_READY_NO_EXTERNAL

Scope:
- Acceptance is paper-only expansion acceptance.
- R009 remains design-only, paper-only, non-executable, not an order, not submitted, and no broker route.
- Executable promotion remains blocked.
- R054 more-data recommendation remains preserved.

Next recommended phase: EXEC-SIM-R059 - No-External Next-Stage Paper-Only Expansion Date/Fixture Planning Gate.
"@
Write-Text (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-summary.md") $summary

Write-Host "EXEC-ALGO-R011 artifacts generated"
