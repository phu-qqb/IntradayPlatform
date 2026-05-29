param(
    [string]$ArtifactsDir = "artifacts/readiness/execution-sim"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message, [string]$Classification) {
    Write-Error "$Classification`: $Message"
    exit 1
}

function Read-Json([string]$Path, [string]$Classification) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing artifact: $Path" $Classification
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Require-True($Value, [string]$Message, [string]$Classification) {
    if ($Value -ne $true) { Fail $Message $Classification }
}

function Require-False($Value, [string]$Message, [string]$Classification) {
    if ($Value -ne $false) { Fail $Message $Classification }
}

$requiredArtifacts = @(
    "phase-exec-sim-r019-summary.md",
    "phase-exec-sim-r019-r018-result-review-contract.json",
    "phase-exec-sim-r019-operator-review-report.md",
    "phase-exec-sim-r019-operator-review-report.json",
    "phase-exec-sim-r019-shape-recommendations.json",
    "phase-exec-sim-r019-recommendations-by-bar-role.json",
    "phase-exec-sim-r019-recommendations-by-instrument.json",
    "phase-exec-sim-r019-ranking-review.json",
    "phase-exec-sim-r019-no-overnight-penalty-review.json",
    "phase-exec-sim-r019-opening-build-review.json",
    "phase-exec-sim-r019-closing-flatten-review.json",
    "phase-exec-sim-r019-benchmark-only-review.json",
    "phase-exec-sim-r019-excluded-shapes-review.json",
    "phase-exec-sim-r019-wakett-rejection-preservation.json",
    "phase-exec-sim-r019-direct-cross-rejection-preservation.json",
    "phase-exec-sim-r019-missing-convention-review.json",
    "phase-exec-sim-r019-future-offline-testing-requirements.json",
    "phase-exec-sim-r019-cost-guidance-preservation.json",
    "phase-exec-sim-r019-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r019-usd-pair-normalization-preservation.json",
    "phase-exec-sim-r019-no-new-simulation-audit.json",
    "phase-exec-sim-r019-no-new-backtest-audit.json",
    "phase-exec-sim-r019-no-new-result-lines-audit.json",
    "phase-exec-sim-r019-no-executable-schedule-audit.json",
    "phase-exec-sim-r019-no-child-slices-audit.json",
    "phase-exec-sim-r019-no-child-orders-audit.json",
    "phase-exec-sim-r019-no-real-fill-audit.json",
    "phase-exec-sim-r019-no-execution-report-audit.json",
    "phase-exec-sim-r019-no-order-created-audit.json",
    "phase-exec-sim-r019-no-route-no-submission-audit.json",
    "phase-exec-sim-r019-no-polygon-api-call-audit.json",
    "phase-exec-sim-r019-no-lmax-call-audit.json",
    "phase-exec-sim-r019-no-external-api-call-audit.json",
    "phase-exec-sim-r019-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r019-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r019-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r019-no-external-audit.json",
    "phase-exec-sim-r019-forbidden-actions-audit.json",
    "phase-exec-sim-r019-next-phase-recommendation.json",
    "phase-exec-sim-r019-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsDir $artifact))) {
        Fail "Required R019 artifact is missing: $artifact" "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
    }
}

$contract = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-r018-result-review-contract.json") "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
$report = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-operator-review-report.json") "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
$shapeRecommendations = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-shape-recommendations.json") "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
$byRole = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-recommendations-by-bar-role.json") "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
$byInstrument = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-recommendations-by-instrument.json") "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
$ranking = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-ranking-review.json") "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
$penalty = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-no-overnight-penalty-review.json") "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
$opening = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-opening-build-review.json") "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
$closing = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-closing-flatten-review.json") "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
$benchmark = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-benchmark-only-review.json") "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
$excluded = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-excluded-shapes-review.json") "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
$wakett = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-wakett-rejection-preservation.json") "EXEC_SIM_R019_FAIL_WAKETT_BLOCK_WEAKENED"
$direct = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-direct-cross-rejection-preservation.json") "EXEC_SIM_R019_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED"
$missingConvention = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-missing-convention-review.json") "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
$future = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-future-offline-testing-requirements.json") "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-cost-guidance-preservation.json") "EXEC_SIM_R019_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$nonmajor = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-nonmajor-calibration-preservation.json") "EXEC_SIM_R019_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$normalization = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-usd-pair-normalization-preservation.json") "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
$noSimulation = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-no-new-simulation-audit.json") "EXEC_SIM_R019_FAIL_NEW_SIMULATION_OR_BACKTEST_EXECUTED"
$noBacktest = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-no-new-backtest-audit.json") "EXEC_SIM_R019_FAIL_NEW_SIMULATION_OR_BACKTEST_EXECUTED"
$noLines = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-no-new-result-lines-audit.json") "EXEC_SIM_R019_FAIL_NEW_TCA_RESULTS_PRODUCED"
$schedule = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-no-executable-schedule-audit.json") "EXEC_SIM_R019_FAIL_EXECUTABLE_SCHEDULE_CREATED"
$slices = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-no-child-slices-audit.json") "EXEC_SIM_R019_FAIL_CHILD_SLICES_CREATED"
$childOrders = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-no-child-orders-audit.json") "EXEC_SIM_R019_FAIL_CHILD_SLICES_CREATED"
$fill = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-no-real-fill-audit.json") "EXEC_SIM_R019_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$execReport = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-no-execution-report-audit.json") "EXEC_SIM_R019_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$order = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-no-order-created-audit.json") "EXEC_SIM_R019_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$route = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-no-route-no-submission-audit.json") "EXEC_SIM_R019_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$api = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-no-external-api-call-audit.json") "EXEC_SIM_R019_FAIL_API_CALL_DETECTED"
$runtime = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-no-broker-marketdata-runtime-audit.json") "EXEC_SIM_R019_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-usdjpy-caveat-preservation.json") "EXEC_SIM_R019_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-lmax-readonly-baseline-reference.json") "EXEC_SIM_R019_FAIL_API_CALL_DETECTED"
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-no-external-audit.json") "EXEC_SIM_R019_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-forbidden-actions-audit.json") "EXEC_SIM_R019_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r019-build-test-validator-evidence.json") "EXEC_SIM_R019_FAIL_BUILD_OR_TESTS"

Require-True $contract.r018ReviewContractCreated "R018 review contract missing." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
Require-True $contract.reviewRecommendationOnly "Contract not review-only." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
Require-True $contract.noNewSimulation "Contract allows simulation." "EXEC_SIM_R019_FAIL_NEW_SIMULATION_OR_BACKTEST_EXECUTED"
Require-True $contract.noNewBacktest "Contract allows backtest." "EXEC_SIM_R019_FAIL_NEW_SIMULATION_OR_BACKTEST_EXECUTED"
Require-True $contract.noNewTcaResultLines "Contract allows new result lines." "EXEC_SIM_R019_FAIL_NEW_TCA_RESULTS_PRODUCED"
Require-False $contract.unsupportedNumericMetricsInvented "Unsupported numeric metrics invented." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
Require-True $report.operatorReviewReportCreated "Operator report missing." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
Require-True $report.noExecutableActionAuthorized "Report authorizes executable action." "EXEC_SIM_R019_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $report.newSimulationRun "Report ran simulation." "EXEC_SIM_R019_FAIL_NEW_SIMULATION_OR_BACKTEST_EXECUTED"
Require-False $report.newTcaResultLinesProduced "Report produced new TCA lines." "EXEC_SIM_R019_FAIL_NEW_TCA_RESULTS_PRODUCED"
Require-True $shapeRecommendations.shapeRecommendationsCreated "Shape recommendations missing." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
Require-False $shapeRecommendations.unsupportedNumericMetricsInvented "Shape recommendations invent metrics." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
foreach ($rec in @($shapeRecommendations.recommendations)) {
    Require-True $rec.IsDesignOnly "Recommendation not design-only." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
    Require-False $rec.IsExecutable "Recommendation executable." "EXEC_SIM_R019_FAIL_EXECUTABLE_SCHEDULE_CREATED"
    Require-False $rec.IsOrder "Recommendation order." "EXEC_SIM_R019_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    Require-False $rec.IsSubmitted "Recommendation submitted." "EXEC_SIM_R019_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    Require-False $rec.HasBrokerRoute "Recommendation has route." "EXEC_SIM_R019_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
}

Require-True $byRole.recommendationsByBarRoleCreated "Recommendations by role missing." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
Require-False $byRole.OpeningBuild.preSessionExecutionAuthorized "OpeningBuild pre-session execution authorized." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
Require-True $byRole.ClosingFlatten.MustEndFlat "Closing MustEndFlat missing." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
Require-False $byRole.ClosingFlatten.OvernightAllowed "Closing overnight allowed." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
Require-False $byRole.ClosingFlatten.blindMarketFallbackAuthorized "Closing blind fallback authorized." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
Require-True $byInstrument.recommendationsByInstrumentCreated "Recommendations by instrument missing." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
if ($byInstrument.USDJPY.PortfolioNormalizedSymbol -ne "JPYUSD" -or $byInstrument.USDJPY.ExecutionTradableSymbol -ne "USDJPY") { Fail "USDJPY instrument recommendation weakened." "EXEC_SIM_R019_FAIL_USDJPY_CAVEAT_WEAKENED" }
Require-True $byInstrument.USDJPY.RequiresInversion "USDJPY instrument inversion missing." "EXEC_SIM_R019_FAIL_USDJPY_CAVEAT_WEAKENED"
if ($byInstrument.AUDUSD.audusdStatus -ne "not failed") { Fail "AUDUSD misclassified failed." "EXEC_SIM_R019_FAIL_AUDUSD_MISCLASSIFIED" }
Require-True $ranking.rankingReviewCreated "Ranking review missing." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
Require-False $ranking.unsupportedNumericMetricsInvented "Ranking review invents metrics." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
Require-True $penalty.noOvernightPenaltyReviewCreated "No-overnight penalty review missing." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
Require-True $penalty.ClosingFlattenResidualCostlierThanIntradayResidual "Closing residual interpretation weakened." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
Require-True $opening.openingBuildReviewCreated "Opening review missing." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
Require-False $opening.PreSessionExecutionAuthorized "Opening review allows pre-session execution." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
Require-False $opening.OvernightExposureAuthorized "Opening review allows overnight." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
Require-True $closing.closingFlattenReviewCreated "Closing review missing." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
Require-True $closing.MustEndFlat "Closing review MustEndFlat missing." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
Require-False $closing.OvernightAllowed "Closing review allows overnight." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
Require-False $closing.BlindMarketFallbackAuthorized "Closing review blind fallback allowed." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
Require-True $benchmark.benchmarkOnlyReviewCreated "Benchmark-only review missing." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
if ($benchmark.RecommendationStatus -ne "KeepForBenchmarkOnlyComparison") { Fail "Benchmark-only recommendation incorrect." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING" }
Require-False $benchmark.benchmarkOnlyCreatesFill "Benchmark-only creates fill." "EXEC_SIM_R019_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-True $excluded.excludedShapesReviewCreated "Excluded shapes review missing." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
Require-True $excluded.excludedShapesDoNotAuthorizeExecution "Excluded shapes authorize execution." "EXEC_SIM_R019_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-True $excluded.excludedShapesDoNotCreateResultLines "Excluded shapes create result lines." "EXEC_SIM_R019_FAIL_NEW_TCA_RESULTS_PRODUCED"
Require-True $wakett.wakettRejectionPreserved "Wakett rejection missing." "EXEC_SIM_R019_FAIL_WAKETT_BLOCK_WEAKENED"
Require-False $wakett.AlwaysMarketAtCloseAllowed "AlwaysMarketAtClose allowed." "EXEC_SIM_R019_FAIL_WAKETT_BLOCK_WEAKENED"
Require-False $wakett.wakettBlockWeakened "Wakett block weakened." "EXEC_SIM_R019_FAIL_WAKETT_BLOCK_WEAKENED"
Require-True $direct.directCrossRejectionPreserved "Direct-cross rejection missing." "EXEC_SIM_R019_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED"
Require-False $direct.directCrossExecutionAllowedByDefault "Direct-cross execution allowed." "EXEC_SIM_R019_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED"
Require-False $direct.directCrossExclusionWeakened "Direct-cross exclusion weakened." "EXEC_SIM_R019_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED"
Require-True $missingConvention.missingConventionReviewCreated "Missing convention review missing." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
Require-False $missingConvention.MissingConventionShapesExecutable "Missing convention shape executable." "EXEC_SIM_R019_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-True $future.futureOfflineTestingRequirementsCreated "Future offline testing requirements missing." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"
Require-True $future.noBrokerExecutionLiveTrading "Future requirements allow broker/live trading." "EXEC_SIM_R019_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-True $future.requiresExplicitFutureGateBeforeNewSimulation "Future requirements miss new gate." "EXEC_SIM_R019_FAIL_NEW_SIMULATION_OR_BACKTEST_EXECUTED"

if ($cost.bestCaseMajorTargetUsdPerMillion -ne 5) { Fail "5 USD/million target missing." "EXEC_SIM_R019_FAIL_5USD_PER_MILLION_UNIVERSALIZED" }
Require-True $cost.fiveUsdPerMillionBestCaseMajorOnly "5 USD/million not best-case major-only." "EXEC_SIM_R019_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-False $cost.fiveUsdPerMillionUniversalized "5 USD/million universalized." "EXEC_SIM_R019_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-True $nonmajor.nonMajorEmScandiCnhRequireLiquidityCalibration "Nonmajor calibration missing." "EXEC_SIM_R019_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
if ($normalization.executionUniverse -ne "USD-pair-only") { Fail "USD-pair execution weakened." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING" }
Require-False $normalization.normalizationWeakened "USD-pair normalization weakened." "EXEC_SIM_R019_FAIL_RECOMMENDATIONS_MISSING"

Require-False $noSimulation.newSimulationExecuted "New simulation executed." "EXEC_SIM_R019_FAIL_NEW_SIMULATION_OR_BACKTEST_EXECUTED"
Require-False $noBacktest.newBacktestExecuted "New backtest executed." "EXEC_SIM_R019_FAIL_NEW_SIMULATION_OR_BACKTEST_EXECUTED"
Require-False $noLines.newTcaResultLinesProduced "New TCA result lines produced." "EXEC_SIM_R019_FAIL_NEW_TCA_RESULTS_PRODUCED"
Require-False $schedule.executableScheduleCreated "Executable schedule created." "EXEC_SIM_R019_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-False $slices.childSlicesCreated "Child slices created." "EXEC_SIM_R019_FAIL_CHILD_SLICES_CREATED"
Require-False $childOrders.childOrdersCreated "Child orders created." "EXEC_SIM_R019_FAIL_CHILD_SLICES_CREATED"
Require-False $fill.realFillsCreated "Real fills created." "EXEC_SIM_R019_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $fill.fillEntitiesCreated "Fill entities created." "EXEC_SIM_R019_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $execReport.executionReportEntitiesCreated "Execution reports created." "EXEC_SIM_R019_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $order.ordersCreated "Orders created." "EXEC_SIM_R019_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $route.routesCreated "Routes created." "EXEC_SIM_R019_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $route.submissionsCreated "Submissions created." "EXEC_SIM_R019_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $api.polygonApiCalled "Polygon API called." "EXEC_SIM_R019_FAIL_API_CALL_DETECTED"
Require-False $api.lmaxCalled "LMAX called." "EXEC_SIM_R019_FAIL_API_CALL_DETECTED"
Require-False $api.externalApiCalled "External API called." "EXEC_SIM_R019_FAIL_API_CALL_DETECTED"
Require-False $runtime.brokerActivationDetected "Broker activation detected." "EXEC_SIM_R019_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.socketOpened "Socket opened." "EXEC_SIM_R019_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.tlsOpened "TLS opened." "EXEC_SIM_R019_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.fixOpened "FIX opened." "EXEC_SIM_R019_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataRequestSent "MarketDataRequest sent." "EXEC_SIM_R019_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataResponseRead "MarketDataResponse read." "EXEC_SIM_R019_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.apiWorkerLiveGatewayEnabled "API/Worker live gateway enabled." "EXEC_SIM_R019_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.schedulerServiceTimerPollingBackgroundJobIntroduced "Scheduler introduced." "EXEC_SIM_R019_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $runtime.automaticExecutionIntroduced "Automatic execution introduced." "EXEC_SIM_R019_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"

if ($usdjpy.PortfolioNormalizedSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY") { Fail "USDJPY caveat weakened." "EXEC_SIM_R019_FAIL_USDJPY_CAVEAT_WEAKENED" }
Require-True $usdjpy.RequiresInversion "USDJPY inversion missing." "EXEC_SIM_R019_FAIL_USDJPY_CAVEAT_WEAKENED"
if ($usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8") { Fail "USDJPY caveat weakened." "EXEC_SIM_R019_FAIL_USDJPY_CAVEAT_WEAKENED" }
Require-True $usdjpy.caveatPreserved "USDJPY caveat missing." "EXEC_SIM_R019_FAIL_USDJPY_CAVEAT_WEAKENED"
Require-False $usdjpy.weakened "USDJPY weakened." "EXEC_SIM_R019_FAIL_USDJPY_CAVEAT_WEAKENED"
Require-False $usdjpy.audusdMisclassifiedFailed "AUDUSD misclassified failed." "EXEC_SIM_R019_FAIL_AUDUSD_MISCLASSIFIED"
Require-True $lmax.referenceOnly "LMAX reference weakened." "EXEC_SIM_R019_FAIL_API_CALL_DETECTED"
Require-False $lmax.lmaxCalledInR019 "LMAX called in R019." "EXEC_SIM_R019_FAIL_API_CALL_DETECTED"
if ($lmax.audusdStatus -notmatch "not failed") { Fail "AUDUSD incorrectly marked failed." "EXEC_SIM_R019_FAIL_AUDUSD_MISCLASSIFIED" }

Require-False $noExternal.polygonApiCalled "No-external audit shows Polygon call." "EXEC_SIM_R019_FAIL_API_CALL_DETECTED"
Require-False $noExternal.lmaxCalled "No-external audit shows LMAX call." "EXEC_SIM_R019_FAIL_API_CALL_DETECTED"
Require-False $noExternal.externalApiCalled "No-external audit shows external API call." "EXEC_SIM_R019_FAIL_API_CALL_DETECTED"
Require-False $noExternal.newSimulationExecuted "No-external audit shows simulation." "EXEC_SIM_R019_FAIL_NEW_SIMULATION_OR_BACKTEST_EXECUTED"
Require-False $noExternal.newBacktestExecuted "No-external audit shows backtest." "EXEC_SIM_R019_FAIL_NEW_SIMULATION_OR_BACKTEST_EXECUTED"
Require-False $noExternal.newTcaResultLinesProduced "No-external audit shows TCA lines." "EXEC_SIM_R019_FAIL_NEW_TCA_RESULTS_PRODUCED"
Require-False $noExternal.executableScheduleCreated "No-external audit shows schedule." "EXEC_SIM_R019_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-False $noExternal.childSlicesCreated "No-external audit shows child slices." "EXEC_SIM_R019_FAIL_CHILD_SLICES_CREATED"
Require-False $noExternal.ordersFillsReportsRoutesSubmissionsCreated "No-external audit shows order-domain output." "EXEC_SIM_R019_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.liveBrokerProductionTradingStateMutated "State mutated." "EXEC_SIM_R019_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.paperLedgerStateCommitted "Paper ledger committed." "EXEC_SIM_R019_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $forbidden.forbiddenActionsDetected "Forbidden action detected." "EXEC_SIM_R019_FAIL_NEW_EXTERNAL_ACTION_DETECTED"

if ($evidence.dotnetBuildNoRestore -ne "PASS") { Fail "dotnet build evidence is not PASS." "EXEC_SIM_R019_FAIL_BUILD_OR_TESTS" }
if ($evidence.focusedTests -notmatch "^PASS") { Fail "Focused R019 test evidence is not PASS." "EXEC_SIM_R019_FAIL_BUILD_OR_TESTS" }
if ($evidence.unitTests -notmatch "^PASS") { Fail "Unit test evidence is not PASS." "EXEC_SIM_R019_FAIL_BUILD_OR_TESTS" }

Write-Host "EXEC_SIM_R019_PASS_SCHEDULE_SHAPE_RESULT_REVIEW_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R019_PASS_OPERATOR_RECOMMENDATIONS_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R019_PASS_FUTURE_OFFLINE_TESTING_REQUIREMENTS_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R019_PASS_NO_NEW_SIMULATION_NO_ORDER_GATE_READY_NO_EXTERNAL"
exit 0
