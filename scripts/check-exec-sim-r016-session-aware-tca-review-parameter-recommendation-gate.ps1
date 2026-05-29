param(
    [string]$ArtifactsDir = "artifacts/readiness/execution-sim"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message, [string]$Classification) {
    Write-Error "$Classification`: $Message"
    exit 1
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing artifact: $Path" "EXEC_SIM_R016_FAIL_BUILD_OR_TESTS"
    }
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Require-False($Value, [string]$Message, [string]$Classification) {
    if ($Value -ne $false) { Fail $Message $Classification }
}

function Require-True($Value, [string]$Message, [string]$Classification) {
    if ($Value -ne $true) { Fail $Message $Classification }
}

$requiredArtifacts = @(
    "phase-exec-sim-r016-summary.md",
    "phase-exec-sim-r016-r015-session-aware-tca-review-summary.json",
    "phase-exec-sim-r016-parameter-recommendation-contract.json",
    "phase-exec-sim-r016-opening-build-parameter-recommendations.json",
    "phase-exec-sim-r016-intraday-rebalance-parameter-recommendations.json",
    "phase-exec-sim-r016-closing-flatten-parameter-recommendations.json",
    "phase-exec-sim-r016-policy-recommendations-by-bar-role.json",
    "phase-exec-sim-r016-policy-fallback-ladder-by-bar-role.json",
    "phase-exec-sim-r016-blocked-policy-families-by-bar-role.json",
    "phase-exec-sim-r016-manual-review-triggers-by-bar-role.json",
    "phase-exec-sim-r016-feed-quality-requirements-by-bar-role.json",
    "phase-exec-sim-r016-close-benchmark-requirements-by-bar-role.json",
    "phase-exec-sim-r016-no-overnight-flatten-parameter-requirements.json",
    "phase-exec-sim-r016-first-bar-previous-evening-planning-requirements.json",
    "phase-exec-sim-r016-cost-guidance-by-bar-role.json",
    "phase-exec-sim-r016-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r016-usd-pair-normalization-preservation.json",
    "phase-exec-sim-r016-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r016-wakett-pattern-block-preservation.json",
    "phase-exec-sim-r016-no-new-backtest-audit.json",
    "phase-exec-sim-r016-no-new-simulation-result-lines-audit.json",
    "phase-exec-sim-r016-no-polygon-api-call-audit.json",
    "phase-exec-sim-r016-no-lmax-call-audit.json",
    "phase-exec-sim-r016-no-external-api-call-audit.json",
    "phase-exec-sim-r016-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r016-no-real-fill-audit.json",
    "phase-exec-sim-r016-no-execution-report-audit.json",
    "phase-exec-sim-r016-no-order-created-audit.json",
    "phase-exec-sim-r016-no-route-no-submission-audit.json",
    "phase-exec-sim-r016-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r016-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r016-no-external-audit.json",
    "phase-exec-sim-r016-forbidden-actions-audit.json",
    "phase-exec-sim-r016-next-phase-recommendation.json",
    "phase-exec-sim-r016-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsDir $artifact))) {
        Fail "Required R016 artifact is missing: $artifact" "EXEC_SIM_R016_FAIL_BUILD_OR_TESTS"
    }
}

$review = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-r015-session-aware-tca-review-summary.json")
$contract = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-parameter-recommendation-contract.json")
$opening = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-opening-build-parameter-recommendations.json")
$intraday = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-intraday-rebalance-parameter-recommendations.json")
$closing = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-closing-flatten-parameter-recommendations.json")
$policy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-policy-recommendations-by-bar-role.json")
$ladder = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-policy-fallback-ladder-by-bar-role.json")
$blocked = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-blocked-policy-families-by-bar-role.json")
$manual = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-manual-review-triggers-by-bar-role.json")
$feed = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-feed-quality-requirements-by-bar-role.json")
$benchmark = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-close-benchmark-requirements-by-bar-role.json")
$flatten = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-no-overnight-flatten-parameter-requirements.json")
$firstBar = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-first-bar-previous-evening-planning-requirements.json")
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-cost-guidance-by-bar-role.json")
$nonMajor = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-nonmajor-calibration-preservation.json")
$normalization = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-usd-pair-normalization-preservation.json")
$directCross = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-direct-cross-exclusion-preservation.json")
$wakett = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-wakett-pattern-block-preservation.json")
$noBacktest = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-no-new-backtest-audit.json")
$noLines = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-no-new-simulation-result-lines-audit.json")
$api = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-no-external-api-call-audit.json")
$runtime = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-no-broker-marketdata-runtime-audit.json")
$fill = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-no-real-fill-audit.json")
$report = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-no-execution-report-audit.json")
$order = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-no-order-created-audit.json")
$route = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-no-route-no-submission-audit.json")
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-usdjpy-caveat-preservation.json")
$lmax = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-lmax-readonly-baseline-reference.json")
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r016-build-test-validator-evidence.json")

Require-True $review.reviewOnly "R016 review is not review-only." "EXEC_SIM_R016_FAIL_PARAMETER_RECOMMENDATIONS_MISSING"
Require-False $review.newBacktestExecuted "New backtest executed." "EXEC_SIM_R016_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $review.newSimulationResultLinesCreated "New simulation result lines created." "EXEC_SIM_R016_FAIL_NEW_SIMULATION_RESULTS_CREATED"
Require-False $review.r015NumericMetricsInventedInR016 "R016 invented R015 numeric metrics." "EXEC_SIM_R016_FAIL_PARAMETER_RECOMMENDATIONS_MISSING"
if ($review.sourcePhase -ne "EXEC-SIM-R015") { Fail "R015 review summary missing source phase." "EXEC_SIM_R016_FAIL_PARAMETER_RECOMMENDATIONS_MISSING" }

Require-True $contract.designOnly "Recommendation contract is not design-only." "EXEC_SIM_R016_FAIL_PARAMETER_RECOMMENDATIONS_MISSING"
Require-True $contract.notExecutableAlgoConfiguration "Contract appears executable." "EXEC_SIM_R016_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-True $contract.notProductionTradingConfiguration "Contract appears production trading config." "EXEC_SIM_R016_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $contract.createsOrders "Contract creates orders." "EXEC_SIM_R016_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $contract.createsFills "Contract creates fills." "EXEC_SIM_R016_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $contract.createsExecutionReports "Contract creates execution reports." "EXEC_SIM_R016_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
foreach ($role in @("OpeningBuild", "IntradayRebalance", "ClosingFlatten")) {
    if (@($contract.barRoles) -notcontains $role) { Fail "Contract missing role $role." "EXEC_SIM_R016_FAIL_PARAMETER_RECOMMENDATIONS_MISSING" }
}
foreach ($category in @("ResidualCrossThreshold", "ManualReviewThreshold", "RequiredFeedQualityBucket", "RequiredCloseBenchmarkStatus")) {
    if (@($contract.parameterCategories) -notcontains $category) { Fail "Contract missing category $category." "EXEC_SIM_R016_FAIL_PARAMETER_RECOMMENDATIONS_MISSING" }
}

Require-True $opening.targetMayBeKnownPreviousEvening "Opening previous-evening target missing." "EXEC_SIM_R016_FAIL_PARAMETER_RECOMMENDATIONS_MISSING"
Require-True $opening.EarliestExecutionTimestampUtcMustBeSessionStartOrExplicitAllowedStart "Opening earliest execution requirement missing." "EXEC_SIM_R016_FAIL_PARAMETER_RECOMMENDATIONS_MISSING"
Require-False $opening.overnightExposureBeforeSessionStartAllowed "Opening allows overnight exposure." "EXEC_SIM_R016_FAIL_PARAMETER_RECOMMENDATIONS_MISSING"
Require-False $opening.forceBlindCrossingBecauseKnownPreviousEvening "Opening allows blind crossing due to prior knowledge." "EXEC_SIM_R016_FAIL_PARAMETER_RECOMMENDATIONS_MISSING"
Require-True $intraday.normalCloseSeekingBehaviorPreserved "Intraday close-seeking behavior weakened." "EXEC_SIM_R016_FAIL_PARAMETER_RECOMMENDATIONS_MISSING"
if ($intraday.ResidualPenaltyBucket -ne "Normal") { Fail "Intraday residual penalty not normal." "EXEC_SIM_R016_FAIL_PARAMETER_RECOMMENDATIONS_MISSING" }
Require-True $intraday.ControlledResidualCrossOnlyWhenOpportunityCostExceedsCrossingCost "Intraday controlled cross justification missing." "EXEC_SIM_R016_FAIL_PARAMETER_RECOMMENDATIONS_MISSING"

Require-True $closing.MustEndFlat "Closing flatten missing MustEndFlat." "EXEC_SIM_R016_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-False $closing.OvernightAllowed "Closing flatten allows overnight." "EXEC_SIM_R016_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
if ($closing.ResidualPenaltyBucket -ne "NoOvernightCritical") { Fail "Closing residual penalty weakened." "EXEC_SIM_R016_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED" }
if ($closing.MaxResidualAtClose -ne "StrictlyLowerThanIntradayRebalance") { Fail "Closing residual threshold not stricter than intraday." "EXEC_SIM_R016_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED" }
Require-False $closing.PureLimitUntilCloseDefaultAllowed "Closing allows PureLimit default." "EXEC_SIM_R016_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-False $closing.FiveMarketSlicesDefaultAllowed "Closing allows five-slice default." "EXEC_SIM_R016_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-False $closing.AlwaysMarketAtCloseDefaultAllowed "Closing allows AlwaysMarketAtClose default." "EXEC_SIM_R016_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-False $closing.BlindMarketCrossingAllowedWithoutCostJustification "Closing allows blind crossing." "EXEC_SIM_R016_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"

Require-True $policy.designOnly "Policy recommendations are not design-only." "EXEC_SIM_R016_FAIL_PARAMETER_RECOMMENDATIONS_MISSING"
Require-True $policy.notExecutableConfiguration "Policy recommendations appear executable." "EXEC_SIM_R016_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
foreach ($level in @("Preferred", "Acceptable", "BenchmarkOnly", "ManualReview", "DoNotTrade")) {
    if (@($ladder.fallbackLadderLevels) -notcontains $level) { Fail "Fallback ladder missing $level." "EXEC_SIM_R016_FAIL_PARAMETER_RECOMMENDATIONS_MISSING" }
}
foreach ($blockedPolicy in @("PureLimitUntilCloseDefault", "MechanicalMarketSlicesAroundClose", "BlindFiveMarketOrdersAroundClose", "BlindFiveMarketOrdersAtOneMinuteIntervals", "AlwaysMarketAtClose", "BlindMarketCrossingWithoutCostJustification")) {
    if (@($blocked.blockedPolicyFamilies) -notcontains $blockedPolicy) { Fail "Blocked policy missing $blockedPolicy." "EXEC_SIM_R016_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED" }
}
Require-False $blocked.wakettPatternBlockWeakened "Wakett block weakened." "EXEC_SIM_R016_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
Require-True $manual.manualReviewIsNotAutomaticExecution "Manual review treated as automatic execution." "EXEC_SIM_R016_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
foreach ($trigger in @("UnsafeFeed", "MissingCloseBenchmark", "ExtremeSpread")) {
    if (@($manual.manualReviewTriggers) -notcontains $trigger) { Fail "Manual review trigger missing: $trigger." "EXEC_SIM_R016_FAIL_PARAMETER_RECOMMENDATIONS_MISSING" }
}

if (@($feed.requirements).Count -ne 3) { Fail "Feed quality requirements by role missing." "EXEC_SIM_R016_FAIL_PARAMETER_RECOMMENDATIONS_MISSING" }
Require-True $feed.NoQuoteNearCloseTriggersManualReview "NoQuoteNearClose trigger missing." "EXEC_SIM_R016_FAIL_PARAMETER_RECOMMENDATIONS_MISSING"
Require-True $feed.StaleQuoteNearCloseTriggersManualReview "StaleQuote trigger missing." "EXEC_SIM_R016_FAIL_PARAMETER_RECOMMENDATIONS_MISSING"
if (@($benchmark.requirements).Count -ne 3) { Fail "Close benchmark requirements by role missing." "EXEC_SIM_R016_FAIL_PARAMETER_RECOMMENDATIONS_MISSING" }
Require-True $benchmark.MissingCloseBenchmarkTriggersManualReview "Missing close benchmark trigger missing." "EXEC_SIM_R016_FAIL_PARAMETER_RECOMMENDATIONS_MISSING"
Require-True $flatten.MustEndFlat "No-overnight flatten parameters missing MustEndFlat." "EXEC_SIM_R016_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-False $flatten.OvernightAllowed "No-overnight flatten parameters allow overnight." "EXEC_SIM_R016_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
if ($flatten.ResidualPenaltyBucket -ne "NoOvernightCritical") { Fail "No-overnight flatten residual penalty weakened." "EXEC_SIM_R016_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED" }
Require-False $flatten.AlwaysMarketAtCloseDefaultAllowed "No-overnight flatten allows AlwaysMarketAtClose." "EXEC_SIM_R016_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-True $firstBar.firstBarTargetKnownPreviousEveningSupported "First-bar previous evening planning missing." "EXEC_SIM_R016_FAIL_PARAMETER_RECOMMENDATIONS_MISSING"
Require-True $firstBar.KnownAtTimestampUtcSeparateFromEarliestExecutionTimestampUtc "KnownAt/Earliest separation missing." "EXEC_SIM_R016_FAIL_PARAMETER_RECOMMENDATIONS_MISSING"
Require-False $firstBar.overnightExposureBeforeSessionStartAllowed "First-bar planning allows overnight exposure." "EXEC_SIM_R016_FAIL_PARAMETER_RECOMMENDATIONS_MISSING"
Require-False $firstBar.ordersBeforeSessionStartAllowed "First-bar planning allows orders before session start." "EXEC_SIM_R016_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

if ($cost.bestCaseMajorTargetUsdPerMillion -ne 5) { Fail "Best-case target is not 5." "EXEC_SIM_R016_FAIL_5USD_PER_MILLION_UNIVERSALIZED" }
Require-True $cost.fiveUsdPerMillionBestCaseMajorOnly "5 USD/million not best-case major only." "EXEC_SIM_R016_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-False $cost.fiveUsdPerMillionUniversalized "5 USD/million universalized." "EXEC_SIM_R016_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
if (@($cost.costGuidanceByBarRole).Count -ne 3) { Fail "Cost guidance by bar role missing." "EXEC_SIM_R016_FAIL_5USD_PER_MILLION_UNIVERSALIZED" }
Require-True $cost.nonMajorEmScandiCnhRequireLiquidityCalibration "Non-major calibration missing in cost guidance." "EXEC_SIM_R016_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-True $nonMajor.nonMajorEmScandiCnhRequireLiquidityCalibration "Non-major calibration preservation missing." "EXEC_SIM_R016_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-True $nonMajor.doNotExtrapolateEurusdUsdjpyAudusdResultsToNonMajor "Non-major extrapolation guard missing." "EXEC_SIM_R016_FAIL_5USD_PER_MILLION_UNIVERSALIZED"

if ($normalization.executionUniverse -ne "USD-pair-only") { Fail "USD-pair execution universe weakened." "EXEC_SIM_R016_FAIL_PARAMETER_RECOMMENDATIONS_MISSING" }
Require-True $normalization.mandatoryNettingBeforeExecution "Mandatory netting weakened." "EXEC_SIM_R016_FAIL_PARAMETER_RECOMMENDATIONS_MISSING"
Require-False $normalization.normalizationWeakened "USD-pair normalization weakened." "EXEC_SIM_R016_FAIL_PARAMETER_RECOMMENDATIONS_MISSING"
Require-False $directCross.directCrossExecutionAllowedByDefault "Direct-cross execution allowed." "EXEC_SIM_R016_FAIL_PARAMETER_RECOMMENDATIONS_MISSING"
Require-False $directCross.directCrossIncludedInRecommendationsAsExecutionInstrument "Direct-cross included in recommendations." "EXEC_SIM_R016_FAIL_PARAMETER_RECOMMENDATIONS_MISSING"
Require-False $directCross.guidanceWeakened "Direct-cross guidance weakened." "EXEC_SIM_R016_FAIL_PARAMETER_RECOMMENDATIONS_MISSING"
Require-False $wakett.PureLimitUntilCloseDefaultAllowed "PureLimit default allowed." "EXEC_SIM_R016_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
Require-False $wakett.MechanicalMarketSlicesAroundCloseAllowed "Mechanical slices allowed." "EXEC_SIM_R016_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
Require-False $wakett.AlwaysMarketAtCloseAllowed "AlwaysMarketAtClose allowed." "EXEC_SIM_R016_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
Require-False $wakett.wakettPatternBlockWeakened "Wakett pattern block weakened." "EXEC_SIM_R016_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"

Require-False $noBacktest.newBacktestExecuted "New backtest executed." "EXEC_SIM_R016_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $noBacktest.newQuoteFilesImported "New quote files imported." "EXEC_SIM_R016_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $noLines.newSimulationResultLinesCreated "New simulation result lines created." "EXEC_SIM_R016_FAIL_NEW_SIMULATION_RESULTS_CREATED"
Require-False $noLines.simulationResultLinesNamedAsFills "Simulation result lines named as fills." "EXEC_SIM_R016_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $api.polygonApiCalled "Polygon API called." "EXEC_SIM_R016_FAIL_API_CALL_DETECTED"
Require-False $api.lmaxCalled "LMAX called." "EXEC_SIM_R016_FAIL_API_CALL_DETECTED"
Require-False $api.externalApiCalled "External API called." "EXEC_SIM_R016_FAIL_API_CALL_DETECTED"
Require-False $runtime.brokerActivationDetected "Broker activation detected." "EXEC_SIM_R016_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.socketOpened "Socket opened." "EXEC_SIM_R016_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.tlsOpened "TLS opened." "EXEC_SIM_R016_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.fixOpened "FIX opened." "EXEC_SIM_R016_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataRequestSent "MarketDataRequest sent." "EXEC_SIM_R016_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataResponseRead "MarketDataResponse read." "EXEC_SIM_R016_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.schedulerServiceTimerPollingBackgroundJobIntroduced "Scheduler/service introduced." "EXEC_SIM_R016_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $fill.realFillsCreated "Real fills created." "EXEC_SIM_R016_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $fill.fillEntitiesCreated "Fill entities created." "EXEC_SIM_R016_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $report.executionReportEntitiesCreated "Execution report entities created." "EXEC_SIM_R016_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $report.brokerExecutionReportsCreated "Broker execution reports created." "EXEC_SIM_R016_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $order.ordersCreated "Orders created." "EXEC_SIM_R016_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.executableOrdersCreated "Executable orders created." "EXEC_SIM_R016_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $route.routesCreated "Routes created." "EXEC_SIM_R016_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $route.submissionsCreated "Submissions created." "EXEC_SIM_R016_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

Require-True $usdjpy.caveatPreserved "USDJPY caveat not preserved." "EXEC_SIM_R016_FAIL_USDJPY_CAVEAT_WEAKENED"
if ($usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8" -or $usdjpy.requiresInversion -ne $true) {
    Fail "USDJPY caveat/inversion weakened." "EXEC_SIM_R016_FAIL_USDJPY_CAVEAT_WEAKENED"
}
if ($lmax.audusdStatus -notmatch "not failed") { Fail "AUDUSD incorrectly marked failed." "EXEC_SIM_R016_FAIL_AUDUSD_MISCLASSIFIED" }
Require-False $lmax.lmaxCalledInR016 "LMAX called in R016." "EXEC_SIM_R016_FAIL_API_CALL_DETECTED"

Require-False $noExternal.polygonApiCalled "No-external audit shows Polygon call." "EXEC_SIM_R016_FAIL_API_CALL_DETECTED"
Require-False $noExternal.lmaxCalled "No-external audit shows LMAX call." "EXEC_SIM_R016_FAIL_API_CALL_DETECTED"
Require-False $noExternal.externalApiCalled "No-external audit shows external API call." "EXEC_SIM_R016_FAIL_API_CALL_DETECTED"
Require-False $noExternal.newBacktestExecuted "No-external audit shows new backtest." "EXEC_SIM_R016_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $noExternal.newSimulationResultLinesCreated "No-external audit shows new simulation result lines." "EXEC_SIM_R016_FAIL_NEW_SIMULATION_RESULTS_CREATED"
Require-False $noExternal.ordersFillsReportsRoutesSubmissionsCreated "No-external audit shows order-domain artifact." "EXEC_SIM_R016_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.liveBrokerProductionTradingStateMutated "No-external audit shows state mutation." "EXEC_SIM_R016_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.paperLedgerStateCommitted "No-external audit shows paper ledger commit." "EXEC_SIM_R016_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $forbidden.forbiddenActionsDetected "Forbidden action detected." "EXEC_SIM_R016_FAIL_NEW_EXTERNAL_ACTION_DETECTED"

if ($evidence.dotnetBuildNoRestore -ne "PASS") { Fail "dotnet build evidence is not PASS." "EXEC_SIM_R016_FAIL_BUILD_OR_TESTS" }
if ($evidence.focusedTests -notmatch "^PASS") { Fail "Focused R016 test evidence is not PASS." "EXEC_SIM_R016_FAIL_BUILD_OR_TESTS" }
if ($evidence.unitTests -notmatch "^PASS") { Fail "Unit test evidence is not PASS." "EXEC_SIM_R016_FAIL_BUILD_OR_TESTS" }

Write-Host "EXEC_SIM_R016_PASS_SESSION_AWARE_TCA_REVIEW_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R016_PASS_CLOSE_SEEKING_PARAMETER_RECOMMENDATIONS_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R016_PASS_BAR_ROLE_POLICY_RECOMMENDATIONS_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R016_PASS_NO_NEW_BACKTEST_NO_ORDER_GATE_READY_NO_EXTERNAL"
exit 0
