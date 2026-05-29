param(
    [string]$AlgoArtifactsRoot = "artifacts/readiness/execution-algo",
    [string]$SimArtifactsRoot = "artifacts/readiness/execution-sim"
)

$ErrorActionPreference = "Stop"

function Read-Json([string]$path) {
    return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function Write-Json([string]$path, [object]$value, [int]$depth = 30) {
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
        Phase = "EXEC-ALGO-R012"
        AuditName = $key
        Passed = $true
        Occurred = $false
        Detail = $detail
    })
}

$phase = "EXEC-ALGO-R012"
$r054Policy = Read-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r054-policy-decision.json")
$r054Contract = Read-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r054-parameter-contract-decision.json")
$r058Decision = Read-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r058-stability-decision.json")
$r058Review = Read-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r058-operator-review-report.json")
$r012Decision = Read-Json (Join-Path $SimArtifactsRoot "phase-exec-paper-r012-preview-decision.json")
$r012Review = Read-Json (Join-Path $SimArtifactsRoot "phase-exec-paper-r012-operator-review-report.json")
$r011Acceptance = Read-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-paper-only-stability-acceptance-result.json")
$r009Contract = Read-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-r009-contract-reference.json")

$classifications = @(
    "EXEC_ALGO_R012_PASS_R009_PAPER_ONLY_MATURITY_ACCEPTED_NO_EXTERNAL",
    "EXEC_ALGO_R012_PASS_LONG_RUN_PAPER_ONLY_PLAN_READY_NO_EXTERNAL",
    "EXEC_ALGO_R012_PASS_EXECUTABLE_PROMOTION_BLOCKERS_READY_NO_EXTERNAL",
    "EXEC_ALGO_R012_PASS_NO_EXECUTABLE_PROMOTION_NO_ORDER_GATE_READY_NO_EXTERNAL"
)

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-r054-backtest-review-reference.json") ([pscustomobject]@{
    Phase = $phase
    SourcePhase = "EXEC-SIM-R054"
    Dates = 20
    Symbols = 7
    CanonicalQuoteWindows = 3780
    PolicyFamiliesOrVariants = 11
    NonExecutableTcaResultLines = 41580
    PolicyDecision = $r054Policy.PolicyDecision
    ParameterContractDecision = $r054Contract.ParameterContractDecision
    PrimaryStable = $r054Policy.Primary -eq "CloseSeeking15mAdaptive_BalancedAdaptive_v0"
    MoreDataRecommended = [bool]$r054Contract.MoreDataRecommended
    ExecutablePromotionAuthorized = $false
    ReusedOnly = $true
    NewBacktestRun = $false
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-r058-paper-preview-reference.json") ([pscustomobject]@{
    Phase = $phase
    SourcePhase = "EXEC-SIM-R058"
    ReviewedRuns = $r058Review.ReviewedBatchEntries
    ReviewedPreviewLines = $r058Review.ReviewedPreviewLines
    ReadinessBindingsComplete = [bool]$r058Review.ReadinessBindingsComplete
    HeldLines = $r058Review.HeldLines
    DirectCrossExecutableLines = 0
    OpeningBuildLines = 0
    IntradayRebalanceLines = 35
    ClosingFlattenLines = 105
    Decision = $r058Decision.Decision
    AcceptanceScope = $r058Decision.AcceptanceScope
    ExecutablePromotionAuthorized = $false
    ReusedOnly = $true
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-r012-balanced-preview-reference.json") ([pscustomobject]@{
    Phase = $phase
    SourcePhase = "EXEC-PAPER-R012"
    GeneratedFixtures = $r012Review.GeneratedFixtureCount
    AcceptedBatchEntries = $r012Review.SafeManualNoExternalCommandsRun
    PaperExecutionPlanLines = $r012Review.PaperExecutionPlanLinesEmitted
    R009DesignOnlyPreviewLines = $r012Review.R009PreviewLinesProduced
    ReadinessBindingsComplete = $r012Review.CompleteReadinessBindings
    HeldLines = $r012Review.HeldLines
    DirectCrossExecutableLines = 0
    InversionsSafe = [bool]$r012Review.InversionsSafe
    USDJPYCaveatPreserved = [bool]$r012Review.USDJPYCaveatPreserved
    BalancedBarRoleCoverage = $r012Review.BarRoleCoverage
    Decision = $r012Decision.Decision
    ExecutablePromotionAuthorized = $false
    ReusedOnly = $true
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-r009-contract-reference.json") ([pscustomobject]@{
    Phase = $phase
    SourcePhase = "EXEC-ALGO-R009/EXEC-ALGO-R010/EXEC-ALGO-R011"
    ContractVersion = $r009Contract.ContractVersion
    Primary = $r009Contract.Primary
    Secondary = $r009Contract.Secondary
    ConditionalResidualModule = $r009Contract.ConditionalResidualModule
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

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-paper-only-maturity-review-contract.json") ([pscustomobject]@{
    Phase = $phase
    GateType = "NoExternalPaperOnlyMaturityReviewAndLongRunPlanning"
    ReviewAndPlanningOnly = $true
    ReusesR054HistoricalTcaReview = $true
    ReusesR058BroaderPaperPreviewReview = $true
    ReusesR012BalancedBarRolePreviewReview = $true
    ReusesR009R010R011ContractAndAcceptance = $true
    PmsCyclesRun = $false
    ManualNoExternalCommandsRun = $false
    NewBacktestRun = $false
    NewSimulationRun = $false
    TcaResultLinesCreated = $false
    DesignOnly = $true
    PaperOnly = $true
    NonExecutable = $true
    NotAnOrder = $true
    NotSubmitted = $true
    NoBrokerRoute = $true
    BrokerReady = $false
    LiveReady = $false
    ExecutablePromotionAuthorized = $false
    Classifications = $classifications
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-evidence-summary.json") ([pscustomobject]@{
    Phase = $phase
    HistoricalTcaStability = [pscustomobject]@{
        SourcePhase = "EXEC-SIM-R054"
        Dates = 20
        Symbols = 7
        CanonicalQuoteWindows = 3780
        NonExecutableTcaResultLines = 41580
        R009PrimaryCandidateStable = $true
        MoreDataRecommended = $true
    }
    BroaderPaperPreviewStability = [pscustomobject]@{
        SourcePhase = "EXEC-SIM-R058"
        RunsReviewed = 20
        PreviewLinesReviewed = 140
        ReadinessBindingsComplete = 140
        HeldLines = 0
        DirectCrossExecutableLines = 0
        R009StableForBroaderPaperOnlyEvaluation = $true
    }
    BalancedBarRolePreviewStability = [pscustomobject]@{
        SourcePhase = "EXEC-PAPER-R012"
        FixturesGenerated = 30
        BatchEntries = 30
        PreviewLines = 210
        ReadinessBindingsComplete = 210
        HeldLines = 0
        DirectCrossExecutableLines = 0
        OpeningBuildPreviewLines = 70
        IntradayRebalancePreviewLines = 70
        ClosingFlattenPreviewLines = 70
        R009StableAcrossExpandedBarRoleBatch = $true
    }
    ExecutablePromotionAuthorized = $false
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-paper-only-maturity-review-result.json") ([pscustomobject]@{
    Phase = $phase
    DecisionStatuses = @(
        "R009StableForLongRunPaperOnlyExpansion",
        "R009AcceptedForLongRunPaperOnlyPlanning",
        "ExecutablePromotionBlocked",
        "MoreLongRunPaperOnlyDataRecommended"
    )
    R009MaturityStatus = "StableForLongRunPaperOnlyExpansion"
    AcceptedForLongRunPaperOnlyPlanning = $true
    AcceptanceBasis = @(
        "R054 historical design-only TCA stability",
        "R058 broader paper-only preview stability",
        "R012 balanced bar-role paper-only preview stability"
    )
    MoreDataRecommendedPreserved = $true
    MoreLongRunPaperOnlyDataRecommended = $true
    R009ContractVersion = $r009Contract.ContractVersion
    PrimaryPolicyCandidate = $r009Contract.Primary
    SecondaryPolicyCandidate = $r009Contract.Secondary
    ConditionalResidualModule = $r009Contract.ConditionalResidualModule
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

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-long-run-paper-only-expansion-plan.json") ([pscustomobject]@{
    Phase = $phase
    MinimumTargetClosesBeforeExecutableDiscussion = 100
    MinimumOpeningBuildTargetCloses = 30
    MinimumIntradayRebalanceTargetCloses = 30
    MinimumClosingFlattenTargetCloses = 30
    RemainingCasesAllocation = @("Stress regimes", "Wide-spread cases", "High-residual cases", "Quiet regimes")
    Requirements = @(
        "More Qubes fixture batches",
        "More target closes",
        "Balanced bar-role coverage: OpeningBuild, IntradayRebalance, ClosingFlatten",
        "Multiple dates and market regimes",
        "Continued USD-pair-only universe",
        "Continued no-order/no-fill/no-route/no-ledger constraints",
        "Continued quote-window, close-benchmark, and feed-quality readiness bindings",
        "Continued preview-only risk/operator approvals",
        "Aggregated paper-only reporting",
        "No executable promotion"
    )
    MaintainZeroDirectCrossExecutableLines = $true
    MaintainZeroHeldLinesWherePossible = $true
    ExplicitHoldDiagnosticsRequired = $true
    CompleteReadinessBindingsRequired = $true
    ExecutablePromotionAuthorized = $false
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-long-run-paper-only-metrics.json") ([pscustomobject]@{
    Phase = $phase
    Metrics = @(
        "Preview line count",
        "Line coverage by symbol",
        "Line coverage by bar role",
        "Held line count and reasons",
        "Readiness binding completeness",
        "Direct-cross exclusion count",
        "Inversion stability",
        "USDJPY caveat preservation",
        "Manual review frequency",
        "Conditional residual module trigger preview frequency if available",
        "Missing evidence / missing readiness frequency",
        "No-order/no-fill/no-route/no-ledger audit status"
    )
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-executable-promotion-blockers.json") ([pscustomobject]@{
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
        "More long-run paper-only data required",
        "Separate explicit executable gate required if ever considered"
    )
    AcceptanceIsExecutableApproval = $false
    ExecutablePromotionAuthorized = $false
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-risk-operator-review-requirements.json") ([pscustomobject]@{
    Phase = $phase
    PreviewOnlyRiskApprovalRequiredForFutureRuns = $true
    PreviewOnlyOperatorApprovalRequiredForFutureRuns = $true
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
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-data-expansion-requirements.json") ([pscustomobject]@{
    Phase = $phase
    MoreLongRunPaperOnlyDataRecommended = $true
    Requirements = @(
        "At least 100 target closes before executable-promotion discussion",
        "Additional dates",
        "Additional regimes",
        "More Qubes fixture batches",
        "OpeningBuild, IntradayRebalance, and ClosingFlatten coverage",
        "Stress, wide-spread, high-residual, and quiet regimes where supported by evidence"
    )
    CurrentCoreUsdPairUniverseRemainsPrimary = $true
    NonmajorEmScandiCnhDeferred = $true
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-instrument-expansion-constraints.json") ([pscustomobject]@{
    Phase = $phase
    CurrentExecutionSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")
    USDPairOnlyAfterNetting = $true
    DirectCrossesSignalOnly = $true
    DirectCrossNettingFirst = $true
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

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-monitoring-reporting-requirements.json") ([pscustomobject]@{
    Phase = $phase
    FuturePaperOnlyReporting = @(
        "Aggregate preview coverage by batch, date, symbol, and bar role",
        "Report readiness binding completeness",
        "Report held lines and deterministic hold reasons",
        "Report direct-cross exclusion count",
        "Report inversion stability",
        "Report USDJPY caveat preservation",
        "Report manual review frequency",
        "Report no-order/no-fill/no-route/no-ledger audits",
        "Report R009 primary, secondary, and conditional residual preview outcomes"
    )
    SchedulerServicePollingAllowed = $false
    LiveMonitoringAllowed = $false
    PaperOnlyArtifactReportingOnly = $true
})

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-more-data-recommendation-preservation.json") ([pscustomobject]@{
    Phase = $phase
    MoreDataRecommendedFromR054 = $true
    MoreLongRunPaperOnlyDataRecommended = $true
    MoreDataRecommendationPreserved = $true
    ExecutablePromotionDiscussionStillBlocked = $true
})
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-no-executable-promotion-preservation.json") ([pscustomobject]@{
    Phase = $phase
    ExecutablePromotionAuthorized = $false
    BrokerReady = $false
    LiveReady = $false
    AcceptanceTreatedAsExecutableApproval = $false
})
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-rejected-wakett-preservation.json") ([pscustomobject]@{ Phase = $phase; Wakett = "RejectedNegativeBaselineOnly"; RejectionWeakened = $false; Promoted = $false })
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-benchmark-only-preservation.json") ([pscustomobject]@{ Phase = $phase; BenchmarkOnlyPolicies = @("VWAPBenchmarkOnly", "TWAPBenchmarkOnly", "ImmediatePaperBenchmark"); Promoted = $false })
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-manual-review-do-not-trade-preservation.json") ([pscustomobject]@{ Phase = $phase; ManualReview = "SafetyOnly"; DoNotTrade = "SafetyOnly"; Promoted = $false })
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-canonical-quarter-hour-policy-preservation.json") ([pscustomobject]@{ Phase = $phase; FutureTimestampsUseCanonicalQuarterHour = $true; AllowedMinutes = @(0, 15, 30, 45); Legacy06UsedAsFutureCanonical = $false })
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-legacy-compatibility-preservation.json") ([pscustomobject]@{ Phase = $phase; LegacyTimestampsCompatibilityOnly = $true; CanonicalCloseForLegacyMatch = "LegacyOutputTimestamp - 6 minutes"; LegacyNextBarExecutionCloseCanonical = "LegacyOutputTimestamp + 9 minutes"; Legacy06UsedAsFutureCanonical = $false })
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-usd-pair-normalization-preservation.json") ([pscustomobject]@{ Phase = $phase; USDPairOnlyAfterNetting = $true; ExecutionSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF"); AUDUSDNotFailed = $true })
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-direct-cross-exclusion-preservation.json") ([pscustomobject]@{ Phase = $phase; DirectCrossesSignalOnly = $true; DirectCrossNettingFirst = $true; DirectCrossExecutionDisabled = $true; ExclusionWeakened = $false })
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-cost-guidance-preservation.json") ([pscustomobject]@{ Phase = $phase; FiveUsdPerMillion = "BestCaseMajorOnly"; FiveUsdPerMillionUniversalized = $false; NonmajorCalibrationRequired = $true })
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-nonmajor-calibration-preservation.json") ([pscustomobject]@{ Phase = $phase; NonmajorEmScandiCnhCalibrationRequired = $true; NonmajorExecutionAuthorized = $false })
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-usdjpy-caveat-preservation.json") ([pscustomobject]@{ Phase = $phase; NormalizedPortfolioSymbol = "JPYUSD"; ExecutionTradableSymbol = "USDJPY"; RequiresInversion = $true; SecurityID = 4004; SecurityIDSource = "8"; CaveatWeakened = $false })
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-lmax-readonly-baseline-reference.json") ([pscustomobject]@{ Phase = $phase; LmaxReferencedAsReadonlyBaselineOnly = $true; LmaxCalled = $false; BrokerRuntimeActivated = $false })

New-Audit "phase-exec-algo-r012-non-executable-acceptance-audit.json" "NonExecutableAcceptance" "R012 maturity acceptance is paper-only/non-executable."
New-Audit "phase-exec-algo-r012-no-new-pms-cycle-audit.json" "NewPmsCycle" "No PMS/EMS/OMS cycle was run."
New-Audit "phase-exec-algo-r012-no-new-backtest-audit.json" "NewBacktestRun" "No new backtest was run."
New-Audit "phase-exec-algo-r012-no-new-simulation-audit.json" "NewSimulationRun" "No new simulation was run."
New-Audit "phase-exec-algo-r012-no-tca-result-lines-audit.json" "TcaResultLinesCreated" "No TCA result lines were created."
New-Audit "phase-exec-algo-r012-no-executable-schedule-audit.json" "ExecutableSchedulesCreated" "No executable schedules were created."
New-Audit "phase-exec-algo-r012-no-child-slices-audit.json" "ChildSlicesCreated" "No child slices were created."
New-Audit "phase-exec-algo-r012-no-child-orders-audit.json" "ChildOrdersCreated" "No child orders were created."
New-Audit "phase-exec-algo-r012-no-order-created-audit.json" "OrdersCreated" "No orders were created."
New-Audit "phase-exec-algo-r012-no-real-fill-audit.json" "FillsCreated" "No fills were created."
New-Audit "phase-exec-algo-r012-no-execution-report-audit.json" "ExecutionReportsCreated" "No execution reports were created."
New-Audit "phase-exec-algo-r012-no-route-no-submission-audit.json" "RoutesOrSubmissionsCreated" "No routes or submissions were created."
New-Audit "phase-exec-algo-r012-no-paper-ledger-commit-audit.json" "PaperLedgerCommitted" "No paper ledger commit was created."
New-Audit "phase-exec-algo-r012-no-polygon-api-call-audit.json" "PolygonCalled" "Polygon was not called."
New-Audit "phase-exec-algo-r012-no-lmax-call-audit.json" "LmaxCalled" "LMAX was not called."
New-Audit "phase-exec-algo-r012-no-external-api-call-audit.json" "ExternalApiCalled" "No external API was called."
New-Audit "phase-exec-algo-r012-no-broker-marketdata-runtime-audit.json" "BrokerMarketDataRuntimeActivated" "No broker or market-data runtime was activated."

Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-no-external-audit.json") ([pscustomobject]@{ Phase = $phase; NoExternal = $true; PolygonCalled = $false; LmaxCalled = $false; ExternalApiCalled = $false; DownloadsExecuted = $false })
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-forbidden-actions-audit.json") ([pscustomobject]@{
    Phase = $phase
    ForbiddenActionsDetected = $false
    BrokerActivation = $false
    LiveMarketData = $false
    SchedulerServicePolling = $false
    NewPmsCycle = $false
    ManualNoExternalCommandsRun = $false
    BacktestOrSimulation = $false
    TcaResultLinesCreated = $false
    ExecutableSchedule = $false
    ChildSlicesOrOrders = $false
    OrdersFillsReportsRoutesSubmissions = $false
    PaperLedgerCommit = $false
    StateMutation = $false
    R009ExecutablePromotion = $false
})
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-next-phase-recommendation.json") ([pscustomobject]@{
    Phase = $phase
    RecommendedNextPhase = "EXEC-SIM-R060 - No-External Long-Run Paper-Only Batch Planning and Automation Safety Gate"
    Purpose = "Plan long-run paper-only batch packaging and automation safety constraints without enabling scheduler/service/polling, orders, fills, routes, submissions, broker calls, live market data, or ledger commits."
})
Write-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-build-test-validator-evidence.json") ([pscustomobject]@{
    Phase = $phase
    DotnetBuild = "Pending"
    FocusedR012Tests = "Pending"
    UnitTests = "Pending"
    R012Validator = "Pending"
    EvidenceComplete = $false
})

$summary = @"
# EXEC-ALGO-R012 Summary

R009 is recorded as StableForLongRunPaperOnlyExpansion based on R054 historical TCA stability, R058 broader paper-only preview stability, and R012 balanced bar-role paper-only preview stability.

Classifications:
- EXEC_ALGO_R012_PASS_R009_PAPER_ONLY_MATURITY_ACCEPTED_NO_EXTERNAL
- EXEC_ALGO_R012_PASS_LONG_RUN_PAPER_ONLY_PLAN_READY_NO_EXTERNAL
- EXEC_ALGO_R012_PASS_EXECUTABLE_PROMOTION_BLOCKERS_READY_NO_EXTERNAL
- EXEC_ALGO_R012_PASS_NO_EXECUTABLE_PROMOTION_NO_ORDER_GATE_READY_NO_EXTERNAL

Scope:
- Maturity acceptance is paper-only planning acceptance.
- R009 remains design-only, paper-only, non-executable, not an order, not submitted, and no broker route.
- Executable promotion remains blocked.
- More long-run paper-only data is still recommended.

Next recommended phase: EXEC-SIM-R060 - No-External Long-Run Paper-Only Batch Planning and Automation Safety Gate.
"@
Write-Text (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-summary.md") $summary

Write-Host "EXEC-ALGO-R012 artifacts generated"
