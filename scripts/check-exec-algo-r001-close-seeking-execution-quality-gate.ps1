param(
    [string]$ArtifactDirectory = "artifacts/readiness/execution-algo"
)

$ErrorActionPreference = "Stop"

function Fail-Gate {
    param([string]$Classification, [string]$Message)
    Write-Error "$Classification`: $Message"
    exit 1
}

function Read-JsonArtifact {
    param([string]$Path, [string]$MissingClassification)
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail-Gate $MissingClassification "Missing required artifact: $Path"
    }

    try { return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json }
    catch { Fail-Gate $MissingClassification "Artifact is not valid JSON: $Path" }
}

function Require-True {
    param([bool]$Value, [string]$Classification, [string]$Message)
    if (-not $Value) { Fail-Gate $Classification $Message }
}

function Require-False {
    param([bool]$Value, [string]$Classification, [string]$Message)
    if ($Value) { Fail-Gate $Classification $Message }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repoRoot $ArtifactDirectory

$requiredArtifacts = @{
    "phase-exec-algo-r001-summary.md" = "EXEC_ALGO_R001_FAIL_ALGO_DESIGN_CONTRACT_MISSING"
    "phase-exec-algo-r001-existing-execution-inventory.json" = "EXEC_ALGO_R001_FAIL_ALGO_DESIGN_CONTRACT_MISSING"
    "phase-exec-algo-r001-close15m-benchmark-contract.json" = "EXEC_ALGO_R001_FAIL_CLOSE_BENCHMARK_CONTRACT_MISSING"
    "phase-exec-algo-r001-feed-continuity-contract.json" = "EXEC_ALGO_R001_FAIL_FEED_CONTINUITY_CONTRACT_MISSING"
    "phase-exec-algo-r001-feed-readiness-requirements.json" = "EXEC_ALGO_R001_FAIL_FEED_CONTINUITY_CONTRACT_MISSING"
    "phase-exec-algo-r001-execution-quality-contract.json" = "EXEC_ALGO_R001_FAIL_SLIPPAGE_TCA_CONTRACT_MISSING"
    "phase-exec-algo-r001-close-slippage-measurement-contract.json" = "EXEC_ALGO_R001_FAIL_SLIPPAGE_TCA_CONTRACT_MISSING"
    "phase-exec-algo-r001-spread-cost-contract.json" = "EXEC_ALGO_R001_FAIL_SLIPPAGE_TCA_CONTRACT_MISSING"
    "phase-exec-algo-r001-opportunity-nonfill-cost-contract.json" = "EXEC_ALGO_R001_FAIL_SLIPPAGE_TCA_CONTRACT_MISSING"
    "phase-exec-algo-r001-residual-risk-contract.json" = "EXEC_ALGO_R001_FAIL_SLIPPAGE_TCA_CONTRACT_MISSING"
    "phase-exec-algo-r001-algo-family-design-contract.json" = "EXEC_ALGO_R001_FAIL_ALGO_DESIGN_CONTRACT_MISSING"
    "phase-exec-algo-r001-close-seeking-15m-phases.json" = "EXEC_ALGO_R001_FAIL_ALGO_DESIGN_CONTRACT_MISSING"
    "phase-exec-algo-r001-blocked-wakett-patterns.json" = "EXEC_ALGO_R001_FAIL_WAKETT_FAILURE_PATTERNS_NOT_BLOCKED"
    "phase-exec-algo-r001-algo-selection-input-contract.json" = "EXEC_ALGO_R001_FAIL_ALGO_DESIGN_CONTRACT_MISSING"
    "phase-exec-algo-r001-algo-selection-decision-contract.json" = "EXEC_ALGO_R001_FAIL_ALGO_DESIGN_CONTRACT_MISSING"
    "phase-exec-algo-r001-cost-control-reason-categories.json" = "EXEC_ALGO_R001_FAIL_SLIPPAGE_TCA_CONTRACT_MISSING"
    "phase-exec-algo-r001-non-executable-algo-audit.json" = "EXEC_ALGO_R001_FAIL_ALGO_EXECUTABLE"
    "phase-exec-algo-r001-no-order-created-audit.json" = "EXEC_ALGO_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-exec-algo-r001-no-fill-no-execution-report-audit.json" = "EXEC_ALGO_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
    "phase-exec-algo-r001-lineage-preservation.json" = "EXEC_ALGO_R001_FAIL_QUBES_OR_PMS_LINEAGE_WEAKENED"
    "phase-exec-algo-r001-usdjpy-caveat-preservation.json" = "EXEC_ALGO_R001_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-exec-algo-r001-lmax-readonly-baseline-reference.json" = "EXEC_ALGO_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-exec-algo-r001-no-external-audit.json" = "EXEC_ALGO_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-exec-algo-r001-forbidden-actions-audit.json" = "EXEC_ALGO_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-exec-algo-r001-next-phase-recommendation.json" = "EXEC_ALGO_R001_FAIL_ALGO_DESIGN_CONTRACT_MISSING"
    "phase-exec-algo-r001-build-test-validator-evidence.json" = "EXEC_ALGO_R001_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $entry.Key))) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$summary = Get-Content -LiteralPath (Join-Path $artifactRoot "phase-exec-algo-r001-summary.md") -Raw
if ([string]::IsNullOrWhiteSpace($summary)) {
    Fail-Gate "EXEC_ALGO_R001_FAIL_ALGO_DESIGN_CONTRACT_MISSING" "Summary is empty."
}

$inventory = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r001-existing-execution-inventory.json") "EXEC_ALGO_R001_FAIL_ALGO_DESIGN_CONTRACT_MISSING"
$benchmark = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r001-close15m-benchmark-contract.json") "EXEC_ALGO_R001_FAIL_CLOSE_BENCHMARK_CONTRACT_MISSING"
$feed = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r001-feed-continuity-contract.json") "EXEC_ALGO_R001_FAIL_FEED_CONTINUITY_CONTRACT_MISSING"
$feedReadiness = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r001-feed-readiness-requirements.json") "EXEC_ALGO_R001_FAIL_FEED_CONTINUITY_CONTRACT_MISSING"
$quality = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r001-execution-quality-contract.json") "EXEC_ALGO_R001_FAIL_SLIPPAGE_TCA_CONTRACT_MISSING"
$slippage = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r001-close-slippage-measurement-contract.json") "EXEC_ALGO_R001_FAIL_SLIPPAGE_TCA_CONTRACT_MISSING"
$spread = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r001-spread-cost-contract.json") "EXEC_ALGO_R001_FAIL_SLIPPAGE_TCA_CONTRACT_MISSING"
$opportunity = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r001-opportunity-nonfill-cost-contract.json") "EXEC_ALGO_R001_FAIL_SLIPPAGE_TCA_CONTRACT_MISSING"
$residual = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r001-residual-risk-contract.json") "EXEC_ALGO_R001_FAIL_SLIPPAGE_TCA_CONTRACT_MISSING"
$families = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r001-algo-family-design-contract.json") "EXEC_ALGO_R001_FAIL_ALGO_DESIGN_CONTRACT_MISSING"
$phases = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r001-close-seeking-15m-phases.json") "EXEC_ALGO_R001_FAIL_ALGO_DESIGN_CONTRACT_MISSING"
$wakett = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r001-blocked-wakett-patterns.json") "EXEC_ALGO_R001_FAIL_WAKETT_FAILURE_PATTERNS_NOT_BLOCKED"
$selectionInput = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r001-algo-selection-input-contract.json") "EXEC_ALGO_R001_FAIL_ALGO_DESIGN_CONTRACT_MISSING"
$selectionDecision = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r001-algo-selection-decision-contract.json") "EXEC_ALGO_R001_FAIL_ALGO_DESIGN_CONTRACT_MISSING"
$reasons = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r001-cost-control-reason-categories.json") "EXEC_ALGO_R001_FAIL_SLIPPAGE_TCA_CONTRACT_MISSING"
$nonExecutable = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r001-non-executable-algo-audit.json") "EXEC_ALGO_R001_FAIL_ALGO_EXECUTABLE"
$orderAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r001-no-order-created-audit.json") "EXEC_ALGO_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$fillAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r001-no-fill-no-execution-report-audit.json") "EXEC_ALGO_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$lineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r001-lineage-preservation.json") "EXEC_ALGO_R001_FAIL_QUBES_OR_PMS_LINEAGE_WEAKENED"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r001-usdjpy-caveat-preservation.json") "EXEC_ALGO_R001_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r001-lmax-readonly-baseline-reference.json") "EXEC_ALGO_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r001-no-external-audit.json") "EXEC_ALGO_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r001-forbidden-actions-audit.json") "EXEC_ALGO_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-algo-r001-build-test-validator-evidence.json") "EXEC_ALGO_R001_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$inventory.existingModelsInventoried) "EXEC_ALGO_R001_FAIL_ALGO_DESIGN_CONTRACT_MISSING" "Inventory missing."
Require-True (@($inventory.executionModels).Count -gt 0) "EXEC_ALGO_R001_FAIL_ALGO_DESIGN_CONTRACT_MISSING" "Execution inventory empty."
Require-True (@($inventory.emsOmsOrderModels).Count -gt 0) "EXEC_ALGO_R001_FAIL_ALGO_DESIGN_CONTRACT_MISSING" "OMS inventory empty."
Require-True (@($inventory.fillModels).Count -gt 0) "EXEC_ALGO_R001_FAIL_ALGO_DESIGN_CONTRACT_MISSING" "Fill inventory empty."
Require-True (@($inventory.executionReportModels).Count -gt 0) "EXEC_ALGO_R001_FAIL_ALGO_DESIGN_CONTRACT_MISSING" "Report inventory empty."
Require-True (@($inventory.slippageTcaAdjacentModels).Count -gt 0) "EXEC_ALGO_R001_FAIL_SLIPPAGE_TCA_CONTRACT_MISSING" "Slippage/TCA inventory empty."

Require-True ([bool]$benchmark.contractCreated) "EXEC_ALGO_R001_FAIL_CLOSE_BENCHMARK_CONTRACT_MISSING" "Benchmark contract missing."
Require-True ([string]$benchmark.contractName -eq "Close15mBenchmark") "EXEC_ALGO_R001_FAIL_CLOSE_BENCHMARK_CONTRACT_MISSING" "Benchmark name mismatch."
foreach ($field in @("BarId", "BarWindowStartUtc", "BarWindowEndUtc", "TargetCloseTimestampUtc", "DecisionTimestampUtc", "KnownAtTimestampUtc", "TimeKnownBeforeClose", "CloseMid", "CloseBid", "CloseAsk", "CloseSpreadBps", "CloseSourceCategory", "BenchmarkAvailabilityStatus")) {
    Require-True (@($benchmark.requiredFields) -contains $field) "EXEC_ALGO_R001_FAIL_CLOSE_BENCHMARK_CONTRACT_MISSING" "Benchmark field missing: $field"
}
foreach ($method in @("LastValidQuoteBeforeClose", "LastValidMidBeforeClose", "BidAskClose", "FixtureClose", "InconclusiveSafe")) {
    Require-True (@($benchmark.closeConstructionMethods) -contains $method) "EXEC_ALGO_R001_FAIL_CLOSE_BENCHMARK_CONTRACT_MISSING" "Close construction method missing: $method"
}
Require-True ([bool]$benchmark.noLiveMarketDataAssumption) "EXEC_ALGO_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Benchmark assumes live market data."

Require-True ([bool]$feed.contractCreated) "EXEC_ALGO_R001_FAIL_FEED_CONTINUITY_CONTRACT_MISSING" "Feed contract missing."
foreach ($field in @("Bid", "Ask", "Mid", "Timestamp", "Instrument")) {
    Require-True (@($feed.requiredQuoteFields) -contains $field) "EXEC_ALGO_R001_FAIL_FEED_CONTINUITY_CONTRACT_MISSING" "Required quote field missing: $field"
}
Require-True ([int]$feed.minimumQuoteCountInWindow -gt 0) "EXEC_ALGO_R001_FAIL_FEED_CONTINUITY_CONTRACT_MISSING" "Minimum quote count missing."
Require-True ([int]$feed.minimumQuoteCountLastMinute -gt 0) "EXEC_ALGO_R001_FAIL_FEED_CONTINUITY_CONTRACT_MISSING" "Last-minute quote count missing."
Require-True ([bool]$feed.heartbeatRequired) "EXEC_ALGO_R001_FAIL_FEED_CONTINUITY_CONTRACT_MISSING" "Heartbeat requirement missing."
Require-True ([bool]$feed.sessionContinuityRequired) "EXEC_ALGO_R001_FAIL_FEED_CONTINUITY_CONTRACT_MISSING" "Session continuity missing."
foreach ($status in @("ReadyForCloseBenchmark", "MissingBidAsk", "StaleQuotes", "InsufficientQuotes", "SpreadTooWide", "NoQuoteNearClose", "InconclusiveSafe")) {
    Require-True (@($feedReadiness.feedReadinessStatuses) -contains $status) "EXEC_ALGO_R001_FAIL_FEED_CONTINUITY_CONTRACT_MISSING" "Feed status missing: $status"
}
Require-False ([bool]$feedReadiness.assumesLmaxContinuityReady) "EXEC_ALGO_R001_FAIL_FEED_CONTINUITY_CONTRACT_MISSING" "LMAX continuity assumed ready."
Require-True ([bool]$feedReadiness.requiresProofBeforeExecutionUse) "EXEC_ALGO_R001_FAIL_FEED_CONTINUITY_CONTRACT_MISSING" "Feed proof requirement missing."

Require-True ([bool]$quality.contractCreated) "EXEC_ALGO_R001_FAIL_SLIPPAGE_TCA_CONTRACT_MISSING" "Quality contract missing."
foreach ($component in @("CloseSlippageMeasurement", "SpreadCostEstimate", "OpportunityCostEstimate", "NonFillRiskEstimate", "ResidualRiskEstimate", "CompletionPolicy", "ExecutionAlgoSelectionInput", "ExecutionAlgoSelectionDecision", "ExecutionCostEstimate", "ExecutionComparisonShape")) {
    Require-True (@($quality.components) -contains $component) "EXEC_ALGO_R001_FAIL_SLIPPAGE_TCA_CONTRACT_MISSING" "Quality component missing: $component"
}
Require-True ([bool]$quality.designOnly) "EXEC_ALGO_R001_FAIL_ALGO_EXECUTABLE" "Quality contract not design-only."
Require-True ([bool]$quality.nonExecutable) "EXEC_ALGO_R001_FAIL_ALGO_EXECUTABLE" "Quality contract executable."
Require-True ([bool]$slippage.contractCreated) "EXEC_ALGO_R001_FAIL_SLIPPAGE_TCA_CONTRACT_MISSING" "Slippage contract missing."
foreach ($metric in @("close benchmark", "decision mid benchmark", "arrival mid benchmark", "expected spread cost", "expected market impact", "timing cost", "opportunity cost", "implementation shortfall vs 15m close", "expected close slippage bps", "max allowed close slippage bps", "fill ratio placeholder", "residual quantity placeholder", "completion status placeholder")) {
    Require-True (@($slippage.requiredMetrics) -contains $metric) "EXEC_ALGO_R001_FAIL_SLIPPAGE_TCA_CONTRACT_MISSING" "Slippage metric missing: $metric"
}
Require-True ([bool]$spread.blocksRepeatedBlindCrossing) "EXEC_ALGO_R001_FAIL_WAKETT_FAILURE_PATTERNS_NOT_BLOCKED" "Spread cost does not block repeated blind crossing."
Require-True ([bool]$opportunity.blocksPurePassiveLimitDefault) "EXEC_ALGO_R001_FAIL_WAKETT_FAILURE_PATTERNS_NOT_BLOCKED" "Opportunity/non-fill contract does not block pure passive default."
Require-True ([bool]$residual.doesNotAuthorizeMarketCrossingWithoutCostJustification) "EXEC_ALGO_R001_FAIL_WAKETT_FAILURE_PATTERNS_NOT_BLOCKED" "Residual contract authorizes blind crossing."

Require-True ([bool]$families.contractCreated) "EXEC_ALGO_R001_FAIL_ALGO_DESIGN_CONTRACT_MISSING" "Algo family contract missing."
foreach ($family in @("DoNotTrade", "ManualReview", "PassiveUntilUrgency", "CloseSeeking15m", "CloseSeeking15mAdaptive", "ControlledResidualCross", "ImmediatePaperBenchmark", "TWAPBenchmarkOnly", "VWAPBenchmarkOnly")) {
    Require-True (@($families.algoFamilies) -contains $family) "EXEC_ALGO_R001_FAIL_ALGO_DESIGN_CONTRACT_MISSING" "Algo family missing: $family"
}
Require-True ([bool]$families.allFamiliesDesignOnly) "EXEC_ALGO_R001_FAIL_ALGO_EXECUTABLE" "Algo families not design-only."
Require-True ([bool]$families.allFamiliesNonExecutable) "EXEC_ALGO_R001_FAIL_ALGO_EXECUTABLE" "Algo families executable."
Require-True ([bool]$families.allFamiliesNotAnOrder) "EXEC_ALGO_R001_FAIL_EXECUTABLE_ORDER_CREATED" "Algo families create orders."
Require-True ([bool]$families.allFamiliesNotSubmitted) "EXEC_ALGO_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Algo families submitted."
Require-True ([bool]$families.allFamiliesNoBrokerRoute) "EXEC_ALGO_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Algo families have broker route."

Require-True ([bool]$phases.closeSeeking15mPhasesCreated) "EXEC_ALGO_R001_FAIL_ALGO_DESIGN_CONTRACT_MISSING" "CloseSeeking phases missing."
Require-True (@($phases.phases).Count -eq 3) "EXEC_ALGO_R001_FAIL_ALGO_DESIGN_CONTRACT_MISSING" "Expected 3 CloseSeeking phases."
foreach ($input in @("residual quantity", "time to close", "expected spread cost", "expected opportunity cost", "expected close slippage", "fill probability", "max spread bps", "max close slippage bps", "max residual allowed at close", "minimum quote continuity requirement", "manual review threshold", "do-not-trade threshold")) {
    Require-True (@($phases.decisionInputs) -contains $input) "EXEC_ALGO_R001_FAIL_ALGO_DESIGN_CONTRACT_MISSING" "Decision input missing: $input"
}
Require-True ([bool]$phases.notAnOrder -and [bool]$phases.notSubmitted -and [bool]$phases.noBrokerRoute) "EXEC_ALGO_R001_FAIL_ALGO_EXECUTABLE" "CloseSeeking phases executable/order-like."

Require-True ([bool]$wakett.blockedWakettPatternsCreated) "EXEC_ALGO_R001_FAIL_WAKETT_FAILURE_PATTERNS_NOT_BLOCKED" "Wakett block contract missing."
Require-False ([bool]$wakett.pureLimitUntilCloseDefaultAllowed) "EXEC_ALGO_R001_FAIL_WAKETT_FAILURE_PATTERNS_NOT_BLOCKED" "Pure limit default allowed."
Require-False ([bool]$wakett.mechanicalFiveMarketSliceDefaultAllowed) "EXEC_ALGO_R001_FAIL_WAKETT_FAILURE_PATTERNS_NOT_BLOCKED" "Mechanical five-slice allowed."
Require-False ([bool]$wakett.blindMarketCrossingAllowedWithoutCostJustification) "EXEC_ALGO_R001_FAIL_WAKETT_FAILURE_PATTERNS_NOT_BLOCKED" "Blind crossing allowed."
foreach ($pattern in @("PureLimitUntilClose", "MechanicalMarketSlicesAroundClose", "BlindFiveMarketOrdersAroundClose", "BlindFiveMarketOrdersAtOneMinuteIntervals", "AlwaysMarketAtClose", "BlindMarketExecution")) {
    $match = @(@($wakett.patterns) | Where-Object { $_.name -eq $pattern -and $_.blockedAsUnsafeDefault -eq $true })
    Require-True ($match.Count -gt 0) "EXEC_ALGO_R001_FAIL_WAKETT_FAILURE_PATTERNS_NOT_BLOCKED" "Blocked Wakett pattern missing: $pattern"
}

Require-True ([bool]$selectionInput.contractCreated) "EXEC_ALGO_R001_FAIL_ALGO_DESIGN_CONTRACT_MISSING" "Selection input contract missing."
Require-True ([bool]$selectionInput.designOnly) "EXEC_ALGO_R001_FAIL_ALGO_EXECUTABLE" "Selection input not design-only."
Require-True ([bool]$selectionInput.doesNotCreateOrders) "EXEC_ALGO_R001_FAIL_EXECUTABLE_ORDER_CREATED" "Selection input creates orders."
Require-True ([bool]$selectionDecision.contractCreated) "EXEC_ALGO_R001_FAIL_ALGO_DESIGN_CONTRACT_MISSING" "Selection decision contract missing."
Require-True ([bool]$selectionDecision.designOnly) "EXEC_ALGO_R001_FAIL_ALGO_EXECUTABLE" "Selection decision not design-only."
Require-True ([bool]$selectionDecision.nonExecutable) "EXEC_ALGO_R001_FAIL_ALGO_EXECUTABLE" "Selection decision executable."
Require-True ([bool]$selectionDecision.notAnOrder) "EXEC_ALGO_R001_FAIL_EXECUTABLE_ORDER_CREATED" "Selection decision creates order."
Require-True ([bool]$selectionDecision.notSubmitted) "EXEC_ALGO_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Selection decision submitted."
Require-False ([bool]$selectionDecision.createsOmsOrder) "EXEC_ALGO_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Selection creates OMS order."
Require-False ([bool]$selectionDecision.createsBrokerOrder) "EXEC_ALGO_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Selection creates broker order."
Require-False ([bool]$selectionDecision.createsFill) "EXEC_ALGO_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Selection creates fill."
Require-False ([bool]$selectionDecision.createsExecutionReport) "EXEC_ALGO_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Selection creates report."
Require-False ([bool]$selectionDecision.submitsOrder) "EXEC_ALGO_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Selection submits order."

Require-True ([bool]$reasons.reasonCategoriesCreated) "EXEC_ALGO_R001_FAIL_SLIPPAGE_TCA_CONTRACT_MISSING" "Reason categories missing."
foreach ($reason in @("MissingCloseBenchmark", "MissingFeedContinuity", "MissingBidAsk", "StaleQuoteNearClose", "NoQuoteNearClose", "SpreadTooWide", "SlippageLimitExceeded", "NonFillRiskTooHigh", "OpportunityCostTooHigh", "ResidualTooLargeNearClose", "RequiresManualReview", "InconclusiveSafe")) {
    Require-True (@($reasons.safeReasonCategories) -contains $reason) "EXEC_ALGO_R001_FAIL_SLIPPAGE_TCA_CONTRACT_MISSING" "Reason category missing: $reason"
}
Require-True ([bool]$reasons.allReasonsSafeBlockOrReview) "EXEC_ALGO_R001_FAIL_SLIPPAGE_TCA_CONTRACT_MISSING" "Reasons not safe block/review."

Require-True ([bool]$nonExecutable.auditCreated) "EXEC_ALGO_R001_FAIL_ALGO_EXECUTABLE" "Non-executable audit missing."
Require-True ([bool]$nonExecutable.allAlgoFamiliesDesignOnly) "EXEC_ALGO_R001_FAIL_ALGO_EXECUTABLE" "Audit: not design-only."
Require-True ([bool]$nonExecutable.allAlgoFamiliesNonExecutable) "EXEC_ALGO_R001_FAIL_ALGO_EXECUTABLE" "Audit: executable."
Require-False ([bool]$nonExecutable.algoSelectionDecisionExecutable) "EXEC_ALGO_R001_FAIL_ALGO_EXECUTABLE" "Selection executable."
Require-False ([bool]$nonExecutable.scheduleChildrenCreated) "EXEC_ALGO_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Schedule children created."
Require-False ([bool]$nonExecutable.runnableAlgoCreated) "EXEC_ALGO_R001_FAIL_ALGO_EXECUTABLE" "Runnable algo created."

Require-True ([bool]$orderAudit.auditCreated) "EXEC_ALGO_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order audit missing."
Require-False ([bool]$orderAudit.ordersCreated) "EXEC_ALGO_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders created."
Require-False ([bool]$orderAudit.executableOrdersCreated) "EXEC_ALGO_R001_FAIL_EXECUTABLE_ORDER_CREATED" "Executable orders created."
Require-False ([bool]$orderAudit.omsOrdersCreated) "EXEC_ALGO_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "OMS orders created."
Require-False ([bool]$orderAudit.omsParentOrdersCreated) "EXEC_ALGO_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Parent orders created."
Require-False ([bool]$orderAudit.omsChildOrdersCreated) "EXEC_ALGO_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Child orders created."
Require-False ([bool]$orderAudit.brokerOrdersCreated) "EXEC_ALGO_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Broker orders created."
Require-False ([bool]$orderAudit.orderSubmissionIntroduced) "EXEC_ALGO_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order submission introduced."
Require-False ([bool]$orderAudit.brokerRouteCreated) "EXEC_ALGO_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Broker route created."

Require-True ([bool]$fillAudit.auditCreated) "EXEC_ALGO_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill/report audit missing."
Require-False ([bool]$fillAudit.fillsCreated) "EXEC_ALGO_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fills created."
Require-False ([bool]$fillAudit.realFillsCreated) "EXEC_ALGO_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Real fills created."
Require-False ([bool]$fillAudit.executionReportsCreated) "EXEC_ALGO_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Reports created."
Require-False ([bool]$fillAudit.brokerExecutionReportsCreated) "EXEC_ALGO_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Broker reports created."

Require-True ([bool]$lineage.lineagePreserved) "EXEC_ALGO_R001_FAIL_QUBES_OR_PMS_LINEAGE_WEAKENED" "Lineage missing."
Require-True ([bool]$lineage.pmsEmsOmsLineagePreserved) "EXEC_ALGO_R001_FAIL_QUBES_OR_PMS_LINEAGE_WEAKENED" "PMS lineage missing."
foreach ($field in @("CycleRunId", "QubesRunId", "PaperExecutionPlanId", "PaperCandidateId", "RebalanceIntentId", "RiskReviewId", "LotSizingId")) {
    Require-True (@($lineage.requiredLineage) -contains $field) "EXEC_ALGO_R001_FAIL_QUBES_OR_PMS_LINEAGE_WEAKENED" "Lineage field missing: $field"
}

Require-True ([bool]$usdjpy.usdjpyCaveatPreserved) "EXEC_ALGO_R001_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
Require-True ([string]$usdjpy.securityId -eq "4004") "EXEC_ALGO_R001_FAIL_USDJPY_CAVEAT_WEAKENED" "SecurityID wrong."
Require-True ([string]$usdjpy.securityIdSource -eq "8") "EXEC_ALGO_R001_FAIL_USDJPY_CAVEAT_WEAKENED" "SecurityIDSource wrong."
Require-False ([bool]$usdjpy.audusdMisclassifiedFailed) "EXEC_ALGO_R001_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD misclassified."
Require-False ([bool]$usdjpy.weakened) "EXEC_ALGO_R001_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY weakened."
Require-True ([bool]$lmax.referenceOnly) "EXEC_ALGO_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX not reference-only."
Require-False ([bool]$lmax.brokerCalled) "EXEC_ALGO_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker called."

Require-True ([bool]$noExternal.auditCreated) "EXEC_ALGO_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit missing."
Require-False ([bool]$noExternal.brokerActivation) "EXEC_ALGO_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker activation."
Require-False ([bool]$noExternal.socketTlsFixRuntimeAction) "EXEC_ALGO_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Socket/TLS/FIX runtime."
Require-False ([bool]$noExternal.liveMarketRuntimeAction) "EXEC_ALGO_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Live market runtime."
Require-False ([bool]$noExternal.marketDataRequestAttempted) "EXEC_ALGO_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "MarketData request."
Require-False ([bool]$noExternal.marketDataResponseRead) "EXEC_ALGO_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "MarketData response."
Require-False ([bool]$noExternal.apiWorkerLiveGatewayEnabled) "EXEC_ALGO_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API/Worker live gateway."
Require-False ([bool]$noExternal.schedulerPollingServiceTimerBackgroundJob) "EXEC_ALGO_R001_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler/service/polling."
Require-False ([bool]$noExternal.automaticExecution) "EXEC_ALGO_R001_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Automatic execution."
Require-False ([bool]$noExternal.ordersCreated) "EXEC_ALGO_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders created."
Require-False ([bool]$noExternal.fillsCreated) "EXEC_ALGO_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fills created."
Require-False ([bool]$noExternal.executionReportsCreated) "EXEC_ALGO_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Reports created."
Require-False ([bool]$noExternal.brokerExecutionReportsCreated) "EXEC_ALGO_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Broker reports created."
Require-False ([bool]$noExternal.routesCreated) "EXEC_ALGO_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Routes created."
Require-False ([bool]$noExternal.submissionsCreated) "EXEC_ALGO_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Submissions created."
Require-False ([bool]$noExternal.liveTradingPathIntroduced) "EXEC_ALGO_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Live trading path."
Require-False ([bool]$noExternal.paperLedgerCommit) "EXEC_ALGO_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Paper ledger commit."
Require-False ([bool]$noExternal.livePositionMutation) "EXEC_ALGO_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Live position mutation."
Require-False ([bool]$noExternal.brokerPositionMutation) "EXEC_ALGO_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker position mutation."
Require-False ([bool]$noExternal.productionLedgerMutation) "EXEC_ALGO_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Production ledger mutation."
Require-False ([bool]$noExternal.tradingStateMutation) "EXEC_ALGO_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Trading state mutation."
Require-False ([bool]$noExternal.replayOrShadowReplay) "EXEC_ALGO_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Replay introduced."
Require-False ([bool]$noExternal.secretOrRawPayloadSerialization) "EXEC_ALGO_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Secret/raw payload serialization."

Require-False ([bool]$forbidden.brokerActivationDetected) "EXEC_ALGO_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden broker activation."
Require-False ([bool]$forbidden.socketTlsFixMarketDataRuntimeDetected) "EXEC_ALGO_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden runtime detected."
Require-False ([bool]$forbidden.schedulerPollingServiceTimerBackgroundJobIntroduced) "EXEC_ALGO_R001_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Forbidden scheduler/service."
Require-False ([bool]$forbidden.automaticExecutionIntroduced) "EXEC_ALGO_R001_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Forbidden automation."
Require-False ([bool]$forbidden.orderSubmissionIntroduced) "EXEC_ALGO_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden order submission."
Require-False ([bool]$forbidden.executableOrderCreated) "EXEC_ALGO_R001_FAIL_EXECUTABLE_ORDER_CREATED" "Forbidden executable order."
Require-False ([bool]$forbidden.omsOrBrokerOrderCreated) "EXEC_ALGO_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden OMS/broker order."
Require-False ([bool]$forbidden.fillOrExecutionReportIntroduced) "EXEC_ALGO_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Forbidden fill/report."
Require-False ([bool]$forbidden.liveTradingPathIntroduced) "EXEC_ALGO_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden live trading."
Require-False ([bool]$forbidden.stateMutationIntroduced) "EXEC_ALGO_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden state mutation."
Require-False ([bool]$forbidden.paperLedgerCommitOccurred) "EXEC_ALGO_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden paper commit."

Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "EXEC_ALGO_R001_FAIL_BUILD_OR_TESTS" "Build/test evidence missing."
Require-True ([string]$evidence.dotnetBuildNoRestore -eq "PASS") "EXEC_ALGO_R001_FAIL_BUILD_OR_TESTS" "Build failed."
Require-True ([string]$evidence.focusedTests -like "PASS*") "EXEC_ALGO_R001_FAIL_BUILD_OR_TESTS" "Focused tests failed."
Require-True ([string]$evidence.unitTests -like "PASS*") "EXEC_ALGO_R001_FAIL_BUILD_OR_TESTS" "Unit tests failed."
Require-True ([string]$evidence.validator -eq "PASS") "EXEC_ALGO_R001_FAIL_BUILD_OR_TESTS" "Validator evidence missing."

Write-Host "EXEC_ALGO_R001_PASS_CLOSE_SEEKING_EXECUTION_FOUNDATION_READY_NO_EXTERNAL"
Write-Host "EXEC_ALGO_R001_PASS_CLOSE_SLIPPAGE_TCA_CONTRACT_READY_NO_EXTERNAL"
Write-Host "EXEC_ALGO_R001_PASS_FEED_CONTINUITY_REQUIREMENTS_READY_NO_EXTERNAL"
Write-Host "EXEC_ALGO_R001_PASS_NONEXECUTABLE_ALGO_DESIGN_READY_NO_EXTERNAL"
