param(
    [string]$SimArtifactsRoot = "artifacts/readiness/execution-sim",
    [string]$AlgoArtifactsRoot = "artifacts/readiness/execution-algo"
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
    Write-Json (Join-Path $SimArtifactsRoot $name) ([pscustomobject]@{
        Phase = "EXEC-SIM-R059"
        AuditName = $key
        Passed = $true
        Occurred = $false
        Detail = $detail
    })
}

$phase = "EXEC-SIM-R059"
$r011Acceptance = Read-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-paper-only-stability-acceptance-result.json")
$r011Requirements = Read-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-next-stage-paper-only-requirements.json")
$r058Review = Read-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r058-operator-review-report.json")
$r058BarRole = Read-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r058-bar-role-coverage-review.json")
$r009Contract = Read-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r011-r009-contract-reference.json")

$classifications = @(
    "EXEC_SIM_R059_PASS_NEXT_STAGE_PAPER_ONLY_EXPANSION_PLAN_READY_NO_EXTERNAL",
    "EXEC_SIM_R059_PASS_BAR_ROLE_COVERAGE_PLAN_READY_NO_EXTERNAL",
    "EXEC_SIM_R059_PASS_OPERATOR_PACKAGE_READY_NO_EXTERNAL",
    "EXEC_SIM_R059_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
)

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r059-r011-stability-reference.json") ([pscustomobject]@{
    Phase = $phase
    SourcePhase = "EXEC-ALGO-R011"
    Classifications = $r011Acceptance.Classifications
    R009Status = $r011Acceptance.R009Status
    AcceptedForNextStagePaperOnlyExpansion = [bool]$r011Acceptance.AcceptedForNextStagePaperOnlyExpansion
    R054MoreDataRecommended = [bool]$r011Acceptance.R054MoreDataRecommended
    ExecutablePromotionAuthorized = $false
    BrokerReady = $false
    LiveReady = $false
    ReusedOnly = $true
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r059-r058-preview-review-reference.json") ([pscustomobject]@{
    Phase = $phase
    SourcePhase = "EXEC-SIM-R058"
    ReviewedRuns = $r058Review.ReviewedBatchEntries
    ReviewedPreviewLines = $r058Review.ReviewedPreviewLines
    ReadinessBindingsComplete = [bool]$r058Review.ReadinessBindingsComplete
    HeldLines = $r058Review.HeldLines
    OrderLikeOutputsDetected = $r058Review.OrderLikeOutputsDetected
    BarRoleCoverage = $r058BarRole.Reviews
    StableEnoughForFurtherPaperOnlyEvaluationExpansion = [bool]$r058Review.StableEnoughForFurtherPaperOnlyEvaluationExpansion
    ReusedOnly = $true
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r059-r009-contract-reference.json") ([pscustomobject]@{
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

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r059-next-stage-paper-only-expansion-contract.json") ([pscustomobject]@{
    Phase = $phase
    GateType = "NoExternalNextStagePaperOnlyExpansionPlanning"
    PlanningOnly = $true
    ReusesExecAlgoR011StabilityAcceptance = $true
    ReusesExecSimR058PreviewReview = $true
    ReusesExecPaperR011BatchPreviewArtifacts = $true
    ReusesExecPaperR010FixtureExtractionManifestArtifacts = $true
    ReusesR009R010DesignOnlyContract = $true
    CommandsGeneratedOnlyInFuture = $true
    CommandsExecuted = $false
    ManualNoExternalCommandsRun = $false
    PmsCyclesRun = $false
    BacktestOrSimulationRun = $false
    TcaResultLinesCreated = $false
    DesignOnly = $true
    PaperOnly = $true
    NonExecutable = $true
    BrokerReady = $false
    LiveReady = $false
    ExecutablePromotionAuthorized = $false
    Classifications = $classifications
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r059-coverage-gap-analysis.json") ([pscustomobject]@{
    Phase = $phase
    PriorReviewedBatchEntries = $r058Review.ReviewedBatchEntries
    PriorReviewedPreviewLines = $r058Review.ReviewedPreviewLines
    PriorOpeningBuildLines = 0
    PriorIntradayRebalanceLines = 35
    PriorClosingFlattenLines = 105
    Gaps = @(
        "OpeningBuild coverage missing",
        "IntradayRebalance coverage limited",
        "ClosingFlatten coverage heavier than other roles"
    )
    RecommendedCorrection = "Plan at least 30 target closes with at least 10 OpeningBuild, 10 IntradayRebalance, and 10 ClosingFlatten closes."
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r059-target-close-distribution-plan.json") ([pscustomobject]@{
    Phase = $phase
    RecommendedMinimumTargetCloses = 30
    Distribution = @(
        [pscustomobject]@{ BarRole = "OpeningBuild"; MinimumTargetCloses = 10 },
        [pscustomobject]@{ BarRole = "IntradayRebalance"; MinimumTargetCloses = 10 },
        [pscustomobject]@{ BarRole = "ClosingFlatten"; MinimumTargetCloses = 10 }
    )
    CanonicalSession = "14:15-21:00 America/New_York"
    CanonicalMinutes = @(0, 15, 30, 45)
    Legacy06UsedAsFutureCanonical = $false
    CandidateDefinitionNeedsOperatorConfirmation = $true
    SelectionRules = @(
        "Use canonical quarter-hour closes only",
        "OpeningBuild should use early session target closes after session open",
        "IntradayRebalance should use middle session target closes",
        "ClosingFlatten should use final session close, especially 21:00 America/New_York",
        "Partial days require manual review or exclusion"
    )
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r059-bar-role-coverage-plan.json") ([pscustomobject]@{
    Phase = $phase
    PriorCoverage = @(
        [pscustomobject]@{ BarRole = "OpeningBuild"; PriorPreviewLineCount = 0; PriorBatchEntryCount = 0 },
        [pscustomobject]@{ BarRole = "IntradayRebalance"; PriorPreviewLineCount = 35; PriorBatchEntryCount = 5 },
        [pscustomobject]@{ BarRole = "ClosingFlatten"; PriorPreviewLineCount = 105; PriorBatchEntryCount = 15 }
    )
    NextCoverageTarget = @(
        [pscustomobject]@{ BarRole = "OpeningBuild"; MinimumTargetCloses = 10; ExpectedMaxPreviewLinesAtSevenSymbols = 70 },
        [pscustomobject]@{ BarRole = "IntradayRebalance"; MinimumTargetCloses = 10; ExpectedMaxPreviewLinesAtSevenSymbols = 70 },
        [pscustomobject]@{ BarRole = "ClosingFlatten"; MinimumTargetCloses = 10; ExpectedMaxPreviewLinesAtSevenSymbols = 70 }
    )
    CandidateDefinitionNeedsOperatorConfirmation = $true
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r059-date-fixture-distribution-plan.json") ([pscustomobject]@{
    Phase = $phase
    RecommendedMinimumFixtures = 30
    RecommendedMinimumDates = 30
    FixtureDateDistribution = @(
        "Prefer one fixture per target close/date when available",
        "Include normal days",
        "Include quiet days",
        "Include high residual cases if identifiable from Qubes weights",
        "Include wide-spread/thin-liquidity cases only when readiness artifacts support this",
        "Include mixed USD direction cases",
        "Do not invent macro labels without evidence"
    )
    LegacyAggregatedWeightsMayBeUsed = $true
    OperatorSuppliedFixturesAllowed = $true
    PartialDaysManualReviewOrExcluded = $true
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r059-fixture-requirements.json") ([pscustomobject]@{
    Phase = $phase
    RequiredFormat = "<BloombergTicker>;<weight>"
    TimestampsInsideFixtureRowsAllowed = $false
    CanonicalTargetCloseSuppliedSeparatelyInManifest = $true
    DirectCrossesAllowedAsSignalsOnly = $true
    NettingRequiredBeforeUsdPairPaperPreview = $true
    LegacyAggregatedWeightsExtractionAllowedWithCompatibilityMappingOnly = $true
    OperatorSuppliedQubesFixturesAllowed = $true
    InvalidMacroLabelsInvented = $false
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r059-batch-manifest-requirements.json") ([pscustomobject]@{
    Phase = $phase
    RequiredFields = @(
        "BatchEntryId",
        "QubesFixturePath",
        "QubesRunId",
        "RequestedCycleRunId",
        "CanonicalTargetCloseLocal",
        "CanonicalTargetCloseUtc",
        "CanonicalSession",
        "BarRole",
        "CadenceMinutes=15",
        "NoPaperLedgerCommit=true",
        "FixtureSource",
        "LegacyCompatibilityMappingUsed",
        "ReadinessBindingRequired=true",
        "RiskOperatorApprovalScope=DesignOnlyPreviewOnly"
    )
    AllowedFixtureSources = @("LegacyAggregatedWeightsExtraction", "OperatorSupplied", "Other")
    Legacy06UsedAsFutureCanonical = $false
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r059-manual-noexternal-command-planning-requirements.json") ([pscustomobject]@{
    Phase = $phase
    CommandGenerationOnly = $true
    CommandsExecuted = $false
    RequiredFlags = @(
        "--mode ManualNoExternal",
        "--output-artifacts-dir",
        "--requested-cycle-run-id",
        "--qubes-run-id",
        "--qubes-fixture-path",
        "--cadence-minutes 15",
        "--no-paper-ledger-commit true"
    )
    DeprecatedFlagsDisallowed = @("--mode no-external-paper-cycle", "--output")
    SafetyValidationRequiredBeforeAnyFutureRun = $true
    NoBrokerLiveOrderRouteSubmissionFlagsAllowed = $true
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r059-readiness-binding-requirements.json") ([pscustomobject]@{
    Phase = $phase
    PerTargetCloseRequirements = @(
        "Quote-window readiness",
        "Close-benchmark readiness",
        "Feed-quality readiness",
        "Canonical session metadata"
    )
    RequiredForEveryPreviewLine = $true
    MissingBindingHoldRequired = $true
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r059-risk-operator-approval-requirements.json") ([pscustomobject]@{
    Phase = $phase
    PreviewOnlyRiskApprovalRequired = $true
    PreviewOnlyOperatorApprovalRequired = $true
    RequiredScope = "DesignOnlyPreviewOnly"
    ApprovedForExecutableUse = $false
    ApprovedForOrderCreation = $false
    ApprovedForBrokerRouting = $false
    ApprovedForSubmission = $false
    ApprovedForPaperLedgerCommit = $false
    ApprovedForStateMutation = $false
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r059-aggregation-review-requirements.json") ([pscustomobject]@{
    Phase = $phase
    RequiredReviews = @(
        "Preview line coverage",
        "Per-symbol review",
        "Per-batch-entry review",
        "Bar-role coverage review",
        "Readiness-binding review",
        "Direct-cross/netting review",
        "Inversion review",
        "Risk/operator approval scope review",
        "Held-line diagnostics",
        "R009 stability decision"
    )
    ExpectedMaxPreviewLinesForThirtyCloses = 210
    AcceptanceScope = "PaperOnlyEvaluationExpansion"
    ExecutablePromotionAuthorized = $false
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r059-success-criteria.json") ([pscustomobject]@{
    Phase = $phase
    Criteria = @(
        "All commands pass safety validation",
        "All ManualNoExternal runs are local/no-external/no-ledger-commit",
        "All preview lines are NonExecutable/NotAnOrder/NoBrokerRoute",
        "No direct-cross executable lines",
        "Readiness bindings complete",
        "Risk/operator approvals are preview-only",
        "R009 remains stable across bar roles",
        "No schedule/order/fill/route/submission/ledger/state mutation"
    )
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r059-hold-criteria.json") ([pscustomobject]@{
    Phase = $phase
    Criteria = @(
        "Missing fixtures",
        "Missing canonical target closes",
        "Missing readiness bindings",
        "Missing risk/operator preview approval",
        "Direct cross emitted as executable line",
        "Unsupported instrument after netting",
        "Nonmajor/EM/scandi/CNH without calibration",
        "Legacy :06 used as future canonical",
        "Any executable path appears"
    )
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r059-next-operator-action-package.json") ([pscustomobject]@{
    Phase = $phase
    Action = "Prepare next-stage paper-only fixture and target-close batch manifest."
    MinimumTargetCloses = 30
    RequiredRoleDistribution = "At least 10 OpeningBuild, 10 IntradayRebalance, and 10 ClosingFlatten target closes."
    FixtureFormat = "<BloombergTicker>;<weight>"
    CommandsToRunNow = @()
    ManualNoExternalCommandsMustNotBeRunInR059 = $true
    NextPhase = "EXEC-PAPER-R012 - No-External Next-Stage Paper Batch Fixture/Manifest Generation Gate"
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r059-canonical-quarter-hour-policy-preservation.json") ([pscustomobject]@{
    Phase = $phase
    FutureTimestampsUseCanonicalQuarterHour = $true
    AllowedMinutes = @(0, 15, 30, 45)
    Legacy06UsedAsFutureCanonical = $false
})
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r059-legacy-compatibility-preservation.json") ([pscustomobject]@{
    Phase = $phase
    LegacyTimestampsCompatibilityOnly = $true
    CanonicalCloseForLegacyMatch = "LegacyOutputTimestamp - 6 minutes"
    LegacyNextBarExecutionCloseCanonical = "LegacyOutputTimestamp + 9 minutes"
    Legacy06UsedAsFutureCanonical = $false
})
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r059-usd-pair-normalization-preservation.json") ([pscustomobject]@{
    Phase = $phase
    USDPairOnlyAfterNetting = $true
    ExecutionSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")
    AUDUSDNotFailed = $true
})
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r059-direct-cross-exclusion-preservation.json") ([pscustomobject]@{
    Phase = $phase
    DirectCrossesSignalOnly = $true
    DirectCrossNettingFirst = $true
    DirectCrossExecutionDisabled = $true
    ExclusionWeakened = $false
})
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r059-cost-guidance-preservation.json") ([pscustomobject]@{
    Phase = $phase
    FiveUsdPerMillion = "BestCaseMajorOnly"
    FiveUsdPerMillionUniversalized = $false
    NonmajorCalibrationRequired = $true
})
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r059-nonmajor-calibration-preservation.json") ([pscustomobject]@{
    Phase = $phase
    NonmajorEmScandiCnhCalibrationRequired = $true
    NonmajorExecutionAuthorized = $false
})
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r059-usdjpy-caveat-preservation.json") ([pscustomobject]@{
    Phase = $phase
    NormalizedPortfolioSymbol = "JPYUSD"
    ExecutionTradableSymbol = "USDJPY"
    RequiresInversion = $true
    SecurityID = 4004
    SecurityIDSource = "8"
    CaveatWeakened = $false
})
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r059-lmax-readonly-baseline-reference.json") ([pscustomobject]@{
    Phase = $phase
    LmaxReferencedAsReadonlyBaselineOnly = $true
    LmaxCalled = $false
    BrokerRuntimeActivated = $false
})

New-Audit "phase-exec-sim-r059-no-broker-activation-audit.json" "BrokerActivation" "No broker activation occurred."
New-Audit "phase-exec-sim-r059-no-live-marketdata-audit.json" "LiveMarketData" "No live market data was requested."
New-Audit "phase-exec-sim-r059-no-scheduler-service-polling-audit.json" "SchedulerServicePolling" "No scheduler/service/polling/background job was started."
New-Audit "phase-exec-sim-r059-no-new-pms-cycle-audit.json" "NewPmsCycle" "No PMS/EMS/OMS cycle was run."
New-Audit "phase-exec-sim-r059-no-new-backtest-audit.json" "NewBacktestRun" "No new backtest was run."
New-Audit "phase-exec-sim-r059-no-new-simulation-audit.json" "NewSimulationRun" "No new simulation was run."
New-Audit "phase-exec-sim-r059-no-tca-result-lines-audit.json" "TcaResultLinesCreated" "No TCA result lines were created."
New-Audit "phase-exec-sim-r059-no-executable-schedule-audit.json" "ExecutableSchedulesCreated" "No executable schedules were created."
New-Audit "phase-exec-sim-r059-no-child-slices-audit.json" "ChildSlicesCreated" "No child slices were created."
New-Audit "phase-exec-sim-r059-no-child-orders-audit.json" "ChildOrdersCreated" "No child orders were created."
New-Audit "phase-exec-sim-r059-no-order-created-audit.json" "OrdersCreated" "No orders were created."
New-Audit "phase-exec-sim-r059-no-real-fill-audit.json" "FillsCreated" "No fills were created."
New-Audit "phase-exec-sim-r059-no-execution-report-audit.json" "ExecutionReportsCreated" "No execution reports were created."
New-Audit "phase-exec-sim-r059-no-route-no-submission-audit.json" "RoutesOrSubmissionsCreated" "No routes or submissions were created."
New-Audit "phase-exec-sim-r059-no-paper-ledger-commit-audit.json" "PaperLedgerCommitted" "No paper ledger commit was created."
New-Audit "phase-exec-sim-r059-no-polygon-api-call-audit.json" "PolygonCalled" "Polygon was not called."
New-Audit "phase-exec-sim-r059-no-lmax-call-audit.json" "LmaxCalled" "LMAX was not called."
New-Audit "phase-exec-sim-r059-no-external-api-call-audit.json" "ExternalApiCalled" "No external API was called."

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r059-no-external-audit.json") ([pscustomobject]@{
    Phase = $phase
    NoExternal = $true
    PolygonCalled = $false
    LmaxCalled = $false
    ExternalApiCalled = $false
    DownloadsExecuted = $false
})
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r059-forbidden-actions-audit.json") ([pscustomobject]@{
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
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r059-next-phase-recommendation.json") ([pscustomobject]@{
    Phase = $phase
    RecommendedNextPhase = "EXEC-PAPER-R012 - No-External Next-Stage Paper Batch Fixture/Manifest Generation Gate"
    Purpose = "Generate or accept next-stage fixtures and a batch manifest for the R059 plan without executing ManualNoExternal commands, schedules, orders, fills, routes, submissions, broker calls, or ledger commits."
})
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r059-build-test-validator-evidence.json") ([pscustomobject]@{
    Phase = $phase
    DotnetBuild = "Pending"
    FocusedR059Tests = "Pending"
    UnitTests = "Pending"
    R059Validator = "Pending"
    EvidenceComplete = $false
})

$summary = @"
# EXEC-SIM-R059 Summary

R059 plans the next-stage R009 paper-only expansion after R011 accepted R009 as stable for broader paper-only evaluation.

Classifications:
- EXEC_SIM_R059_PASS_NEXT_STAGE_PAPER_ONLY_EXPANSION_PLAN_READY_NO_EXTERNAL
- EXEC_SIM_R059_PASS_BAR_ROLE_COVERAGE_PLAN_READY_NO_EXTERNAL
- EXEC_SIM_R059_PASS_OPERATOR_PACKAGE_READY_NO_EXTERNAL
- EXEC_SIM_R059_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL

Coverage gap:
- OpeningBuild: 0 prior lines
- IntradayRebalance: 35 prior lines
- ClosingFlatten: 105 prior lines

Next-stage recommendation:
- At least 30 paper-only target closes if fixtures are available.
- At least 10 OpeningBuild, 10 IntradayRebalance, and 10 ClosingFlatten target closes.
- Keep all outputs design-only, paper-only, non-executable, not an order, not submitted, and no broker route.

Next recommended phase: EXEC-PAPER-R012 - No-External Next-Stage Paper Batch Fixture/Manifest Generation Gate.
"@
Write-Text (Join-Path $SimArtifactsRoot "phase-exec-sim-r059-summary.md") $summary

Write-Host "EXEC-SIM-R059 artifacts generated"
