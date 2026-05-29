param(
    [string]$ArtifactsDir = "artifacts/readiness/execution-algo"
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
    "phase-exec-algo-r006-summary.md",
    "phase-exec-algo-r006-r005-schedule-shape-reference.json",
    "phase-exec-algo-r006-schedule-shape-operator-review-contract.json",
    "phase-exec-algo-r006-schedule-shape-operator-review-report.md",
    "phase-exec-algo-r006-schedule-shape-operator-review-report.json",
    "phase-exec-algo-r006-review-statuses.json",
    "phase-exec-algo-r006-review-actions.json",
    "phase-exec-algo-r006-simulation-handoff-contract.json",
    "phase-exec-algo-r006-simulation-handoff-statuses.json",
    "phase-exec-algo-r006-eligible-simulation-handoff-shapes.json",
    "phase-exec-algo-r006-ineligible-simulation-handoff-shapes.json",
    "phase-exec-algo-r006-accepted-simulation-handoff-examples.json",
    "phase-exec-algo-r006-held-manual-review-examples.json",
    "phase-exec-algo-r006-rejected-unsafe-shape-examples.json",
    "phase-exec-algo-r006-direct-cross-blocked-handoff.json",
    "phase-exec-algo-r006-benchmark-only-handoff.json",
    "phase-exec-algo-r006-opening-build-review-preservation.json",
    "phase-exec-algo-r006-closing-flatten-review-preservation.json",
    "phase-exec-algo-r006-usdjpy-inverted-handoff-preservation.json",
    "phase-exec-algo-r006-cost-guidance-preservation.json",
    "phase-exec-algo-r006-nonmajor-calibration-preservation.json",
    "phase-exec-algo-r006-usd-pair-normalization-preservation.json",
    "phase-exec-algo-r006-direct-cross-exclusion-preservation.json",
    "phase-exec-algo-r006-wakett-pattern-block-preservation.json",
    "phase-exec-algo-r006-non-executable-handoff-audit.json",
    "phase-exec-algo-r006-no-executable-schedule-audit.json",
    "phase-exec-algo-r006-no-child-slices-audit.json",
    "phase-exec-algo-r006-no-child-orders-audit.json",
    "phase-exec-algo-r006-no-new-backtest-audit.json",
    "phase-exec-algo-r006-no-polygon-api-call-audit.json",
    "phase-exec-algo-r006-no-lmax-call-audit.json",
    "phase-exec-algo-r006-no-external-api-call-audit.json",
    "phase-exec-algo-r006-no-broker-marketdata-runtime-audit.json",
    "phase-exec-algo-r006-no-real-fill-audit.json",
    "phase-exec-algo-r006-no-execution-report-audit.json",
    "phase-exec-algo-r006-no-order-created-audit.json",
    "phase-exec-algo-r006-no-route-no-submission-audit.json",
    "phase-exec-algo-r006-usdjpy-caveat-preservation.json",
    "phase-exec-algo-r006-lmax-readonly-baseline-reference.json",
    "phase-exec-algo-r006-no-external-audit.json",
    "phase-exec-algo-r006-forbidden-actions-audit.json",
    "phase-exec-algo-r006-next-phase-recommendation.json",
    "phase-exec-algo-r006-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsDir $artifact))) {
        Fail "Required R006 artifact is missing: $artifact" "EXEC_ALGO_R006_FAIL_BUILD_OR_TESTS"
    }
}

$reference = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-r005-schedule-shape-reference.json") "EXEC_ALGO_R006_FAIL_OPERATOR_REVIEW_MISSING"
$reviewContract = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-schedule-shape-operator-review-contract.json") "EXEC_ALGO_R006_FAIL_OPERATOR_REVIEW_MISSING"
$report = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-schedule-shape-operator-review-report.json") "EXEC_ALGO_R006_FAIL_OPERATOR_REVIEW_MISSING"
$statuses = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-review-statuses.json") "EXEC_ALGO_R006_FAIL_OPERATOR_REVIEW_MISSING"
$actions = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-review-actions.json") "EXEC_ALGO_R006_FAIL_OPERATOR_REVIEW_MISSING"
$handoffContract = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-simulation-handoff-contract.json") "EXEC_ALGO_R006_FAIL_SIMULATION_HANDOFF_MISSING"
$handoffStatuses = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-simulation-handoff-statuses.json") "EXEC_ALGO_R006_FAIL_SIMULATION_HANDOFF_MISSING"
$eligible = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-eligible-simulation-handoff-shapes.json") "EXEC_ALGO_R006_FAIL_SIMULATION_HANDOFF_MISSING"
$ineligible = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-ineligible-simulation-handoff-shapes.json") "EXEC_ALGO_R006_FAIL_SIMULATION_HANDOFF_MISSING"
$accepted = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-accepted-simulation-handoff-examples.json") "EXEC_ALGO_R006_FAIL_SIMULATION_HANDOFF_MISSING"
$held = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-held-manual-review-examples.json") "EXEC_ALGO_R006_FAIL_SIMULATION_HANDOFF_MISSING"
$rejected = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-rejected-unsafe-shape-examples.json") "EXEC_ALGO_R006_FAIL_SIMULATION_HANDOFF_MISSING"
$direct = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-direct-cross-blocked-handoff.json") "EXEC_ALGO_R006_FAIL_SIMULATION_HANDOFF_MISSING"
$benchmark = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-benchmark-only-handoff.json") "EXEC_ALGO_R006_FAIL_SIMULATION_HANDOFF_MISSING"
$opening = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-opening-build-review-preservation.json") "EXEC_ALGO_R006_FAIL_OPERATOR_REVIEW_MISSING"
$closing = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-closing-flatten-review-preservation.json") "EXEC_ALGO_R006_FAIL_OPERATOR_REVIEW_MISSING"
$usdjpyHandoff = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-usdjpy-inverted-handoff-preservation.json") "EXEC_ALGO_R006_FAIL_USDJPY_CAVEAT_WEAKENED"
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-cost-guidance-preservation.json") "EXEC_ALGO_R006_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$nonmajor = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-nonmajor-calibration-preservation.json") "EXEC_ALGO_R006_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$normalization = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-usd-pair-normalization-preservation.json") "EXEC_ALGO_R006_FAIL_OPERATOR_REVIEW_MISSING"
$directPreservation = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-direct-cross-exclusion-preservation.json") "EXEC_ALGO_R006_FAIL_OPERATOR_REVIEW_MISSING"
$wakett = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-wakett-pattern-block-preservation.json") "EXEC_ALGO_R006_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
$nonExecutable = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-non-executable-handoff-audit.json") "EXEC_ALGO_R006_FAIL_HANDOFF_EXECUTABLE"
$schedule = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-no-executable-schedule-audit.json") "EXEC_ALGO_R006_FAIL_EXECUTABLE_SCHEDULE_CREATED"
$slices = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-no-child-slices-audit.json") "EXEC_ALGO_R006_FAIL_CHILD_SLICES_CREATED"
$childOrders = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-no-child-orders-audit.json") "EXEC_ALGO_R006_FAIL_CHILD_ORDERS_CREATED"
$noBacktest = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-no-new-backtest-audit.json") "EXEC_ALGO_R006_FAIL_NEW_BACKTEST_EXECUTED"
$api = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-no-external-api-call-audit.json") "EXEC_ALGO_R006_FAIL_API_CALL_DETECTED"
$runtime = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-no-broker-marketdata-runtime-audit.json") "EXEC_ALGO_R006_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
$fill = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-no-real-fill-audit.json") "EXEC_ALGO_R006_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$execReport = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-no-execution-report-audit.json") "EXEC_ALGO_R006_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$order = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-no-order-created-audit.json") "EXEC_ALGO_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$route = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-no-route-no-submission-audit.json") "EXEC_ALGO_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-usdjpy-caveat-preservation.json") "EXEC_ALGO_R006_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-lmax-readonly-baseline-reference.json") "EXEC_ALGO_R006_FAIL_API_CALL_DETECTED"
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-no-external-audit.json") "EXEC_ALGO_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-forbidden-actions-audit.json") "EXEC_ALGO_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r006-build-test-validator-evidence.json") "EXEC_ALGO_R006_FAIL_BUILD_OR_TESTS"

Require-True $reference.r005ScheduleShapesReferenced "R005 shapes not referenced." "EXEC_ALGO_R006_FAIL_OPERATOR_REVIEW_MISSING"
Require-True $reference.reviewHandoffOnly "R006 not review/handoff-only." "EXEC_ALGO_R006_FAIL_OPERATOR_REVIEW_MISSING"
Require-True $reference.noSimulation "R006 reference does not confirm no simulation." "EXEC_ALGO_R006_FAIL_NEW_BACKTEST_EXECUTED"
Require-True $reference.noBacktest "R006 reference does not confirm no backtest." "EXEC_ALGO_R006_FAIL_NEW_BACKTEST_EXECUTED"
Require-True $reviewContract.operatorReviewContractCreated "Operator review contract missing." "EXEC_ALGO_R006_FAIL_OPERATOR_REVIEW_MISSING"
Require-False $reviewContract.isExecutable "Operator review executable." "EXEC_ALGO_R006_FAIL_HANDOFF_EXECUTABLE"
Require-False $reviewContract.isOrder "Operator review is order." "EXEC_ALGO_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $reviewContract.hasBrokerRoute "Operator review has broker route." "EXEC_ALGO_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $reviewContract.hasChildOrders "Operator review has child orders." "EXEC_ALGO_R006_FAIL_CHILD_ORDERS_CREATED"
Require-False $reviewContract.hasExecutableSlices "Operator review has executable slices." "EXEC_ALGO_R006_FAIL_CHILD_SLICES_CREATED"
Require-True $report.operatorReviewReportCreated "Operator review report missing." "EXEC_ALGO_R006_FAIL_OPERATOR_REVIEW_MISSING"
Require-True $report.scheduleShapesAreNotOrders "Report does not say schedule shapes are not orders." "EXEC_ALGO_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-True $report.schedulePhasesAreNotChildSlices "Report does not say phases are not child slices." "EXEC_ALGO_R006_FAIL_CHILD_SLICES_CREATED"
Require-False $report.executionAuthorized "Report authorizes execution." "EXEC_ALGO_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
foreach ($status in @("AcceptedForSimulationOnly", "HeldForManualReview", "RejectedDirectCross", "RejectedMissingConvention")) {
    if (@($statuses.statuses) -notcontains $status) { Fail "Review status missing $status." "EXEC_ALGO_R006_FAIL_OPERATOR_REVIEW_MISSING" }
}
foreach ($action in @("AcceptForSimulationHandoff", "HoldForManualReview", "RejectUnsafeShape", "RequestInstrumentConventionFix")) {
    if (@($actions.actions) -notcontains $action) { Fail "Review action missing $action." "EXEC_ALGO_R006_FAIL_OPERATOR_REVIEW_MISSING" }
}

Require-True $handoffContract.simulationHandoffContractCreated "Simulation handoff contract missing." "EXEC_ALGO_R006_FAIL_SIMULATION_HANDOFF_MISSING"
Require-True $handoffContract.isForSimulationOnly "Handoff not simulation-only." "EXEC_ALGO_R006_FAIL_HANDOFF_EXECUTABLE"
Require-False $handoffContract.isExecutable "Handoff executable." "EXEC_ALGO_R006_FAIL_HANDOFF_EXECUTABLE"
Require-False $handoffContract.isOrder "Handoff is order." "EXEC_ALGO_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $handoffContract.hasBrokerRoute "Handoff has broker route." "EXEC_ALGO_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $handoffContract.hasChildOrders "Handoff has child orders." "EXEC_ALGO_R006_FAIL_CHILD_ORDERS_CREATED"
Require-False $handoffContract.hasExecutableSlices "Handoff has executable slices." "EXEC_ALGO_R006_FAIL_CHILD_SLICES_CREATED"
foreach ($status in @("SimulationHandoffReadyNoExternal", "SimulationHandoffHeldForManualReview", "SimulationHandoffBlockedDirectCross", "SimulationHandoffBlockedMissingConvention")) {
    if (@($handoffStatuses.statuses) -notcontains $status) { Fail "Handoff status missing $status." "EXEC_ALGO_R006_FAIL_SIMULATION_HANDOFF_MISSING" }
}
Require-True $eligible.eligibleHandoffShapesCreated "Eligible handoff shapes missing." "EXEC_ALGO_R006_FAIL_SIMULATION_HANDOFF_MISSING"
Require-True $eligible.allEligibleShapesSimulationOnly "Eligible handoffs not simulation-only." "EXEC_ALGO_R006_FAIL_HANDOFF_EXECUTABLE"
Require-True $eligible.allEligibleShapesNonExecutable "Eligible handoffs executable." "EXEC_ALGO_R006_FAIL_HANDOFF_EXECUTABLE"
Require-True $ineligible.ineligibleHandoffShapesCreated "Ineligible handoff shapes missing." "EXEC_ALGO_R006_FAIL_SIMULATION_HANDOFF_MISSING"
Require-False $ineligible.anyExecutableShapeEligible "Executable shape eligible." "EXEC_ALGO_R006_FAIL_HANDOFF_EXECUTABLE"
Require-True $accepted.acceptedExamplesCreated "Accepted handoff examples missing." "EXEC_ALGO_R006_FAIL_SIMULATION_HANDOFF_MISSING"
Require-True $held.heldExamplesCreated "Held manual review examples missing." "EXEC_ALGO_R006_FAIL_SIMULATION_HANDOFF_MISSING"
Require-False $held.heldExamplesCreateExecutableSchedules "Held examples create schedule." "EXEC_ALGO_R006_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-True $rejected.rejectedExamplesCreated "Rejected examples missing." "EXEC_ALGO_R006_FAIL_SIMULATION_HANDOFF_MISSING"
Require-False $rejected.rejectedExamplesCreateOrderDomainObjects "Rejected examples create order-domain objects." "EXEC_ALGO_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-True $direct.directCrossBlockedHandoffCreated "Direct-cross blocked handoff missing." "EXEC_ALGO_R006_FAIL_SIMULATION_HANDOFF_MISSING"
if ($direct.SimulationHandoffStatus -ne "SimulationHandoffBlockedDirectCross") { Fail "Direct-cross handoff not blocked." "EXEC_ALGO_R006_FAIL_SIMULATION_HANDOFF_MISSING" }
Require-True $direct.RequiresNettingFirst "Direct-cross handoff missing netting-first." "EXEC_ALGO_R006_FAIL_SIMULATION_HANDOFF_MISSING"
Require-True $direct.DirectCrossSignalOnlyHandlingPreserved "Direct-cross handling weakened." "EXEC_ALGO_R006_FAIL_SIMULATION_HANDOFF_MISSING"
Require-True $benchmark.benchmarkOnlyHandoffCreated "Benchmark-only handoff missing." "EXEC_ALGO_R006_FAIL_SIMULATION_HANDOFF_MISSING"
Require-True $benchmark.BenchmarkOnly "Benchmark handoff not benchmark-only." "EXEC_ALGO_R006_FAIL_SIMULATION_HANDOFF_MISSING"
Require-False $benchmark.IsExecutable "Benchmark handoff executable." "EXEC_ALGO_R006_FAIL_HANDOFF_EXECUTABLE"

Require-True $opening.KnownAtTimestampMayBePreviousEvening "Opening previous-evening handling weakened." "EXEC_ALGO_R006_FAIL_OPERATOR_REVIEW_MISSING"
Require-False $opening.PreSessionOrderAllowed "Opening allows pre-session order." "EXEC_ALGO_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $opening.OvernightExposureBeforeSessionStartAllowed "Opening allows overnight." "EXEC_ALGO_R006_FAIL_OPERATOR_REVIEW_MISSING"
Require-True $closing.MustEndFlat "Closing MustEndFlat missing." "EXEC_ALGO_R006_FAIL_OPERATOR_REVIEW_MISSING"
Require-False $closing.OvernightAllowed "Closing allows overnight." "EXEC_ALGO_R006_FAIL_OPERATOR_REVIEW_MISSING"
if ($closing.ResidualPenaltyBucket -ne "NoOvernightCritical") { Fail "Closing residual penalty weakened." "EXEC_ALGO_R006_FAIL_OPERATOR_REVIEW_MISSING" }
Require-False $closing.BlindMarketFallbackAllowed "Closing blind fallback allowed." "EXEC_ALGO_R006_FAIL_OPERATOR_REVIEW_MISSING"
if ($usdjpyHandoff.PortfolioNormalizedSymbol -ne "JPYUSD" -or $usdjpyHandoff.ExecutionTradableSymbol -ne "USDJPY") { Fail "USDJPY handoff symbols weakened." "EXEC_ALGO_R006_FAIL_USDJPY_CAVEAT_WEAKENED" }
Require-True $usdjpyHandoff.RequiresInversion "USDJPY handoff inversion missing." "EXEC_ALGO_R006_FAIL_USDJPY_CAVEAT_WEAKENED"
if ($usdjpyHandoff.SecurityID -ne "4004" -or $usdjpyHandoff.SecurityIDSource -ne "8") { Fail "USDJPY handoff caveat weakened." "EXEC_ALGO_R006_FAIL_USDJPY_CAVEAT_WEAKENED" }
Require-False $usdjpyHandoff.IsExecutable "USDJPY handoff executable." "EXEC_ALGO_R006_FAIL_HANDOFF_EXECUTABLE"
if ($cost.bestCaseMajorTargetUsdPerMillion -ne 5) { Fail "5 USD/million target missing." "EXEC_ALGO_R006_FAIL_5USD_PER_MILLION_UNIVERSALIZED" }
Require-True $cost.fiveUsdPerMillionBestCaseMajorOnly "5 USD/million not best-case major only." "EXEC_ALGO_R006_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-False $cost.fiveUsdPerMillionUniversalized "5 USD/million universalized." "EXEC_ALGO_R006_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-True $nonmajor.nonMajorEmScandiCnhRequireLiquidityCalibration "Nonmajor calibration missing." "EXEC_ALGO_R006_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
if ($normalization.executionUniverse -ne "USD-pair-only") { Fail "USD-pair execution weakened." "EXEC_ALGO_R006_FAIL_OPERATOR_REVIEW_MISSING" }
Require-False $normalization.normalizationWeakened "USD-pair normalization weakened." "EXEC_ALGO_R006_FAIL_OPERATOR_REVIEW_MISSING"
Require-False $directPreservation.directCrossExecutionAllowedByDefault "Direct-cross execution allowed." "EXEC_ALGO_R006_FAIL_OPERATOR_REVIEW_MISSING"
Require-False $directPreservation.guidanceWeakened "Direct-cross guidance weakened." "EXEC_ALGO_R006_FAIL_OPERATOR_REVIEW_MISSING"
Require-False $wakett.PureLimitUntilCloseDefaultAllowed "PureLimit default allowed." "EXEC_ALGO_R006_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
Require-False $wakett.MechanicalMarketSlicesAroundCloseAllowed "Mechanical slices allowed." "EXEC_ALGO_R006_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
Require-False $wakett.AlwaysMarketAtCloseAllowed "AlwaysMarketAtClose allowed." "EXEC_ALGO_R006_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
Require-False $wakett.wakettPatternBlockWeakened "Wakett block weakened." "EXEC_ALGO_R006_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"

Require-True $nonExecutable.allHandoffsSimulationOnly "Handoffs not simulation-only." "EXEC_ALGO_R006_FAIL_HANDOFF_EXECUTABLE"
Require-True $nonExecutable.allHandoffsNonExecutable "Handoffs executable." "EXEC_ALGO_R006_FAIL_HANDOFF_EXECUTABLE"
Require-False $nonExecutable.simulationHandoffExecutable "Simulation handoff executable." "EXEC_ALGO_R006_FAIL_HANDOFF_EXECUTABLE"
Require-False $nonExecutable.simulationHandoffCreatesOrder "Simulation handoff creates order." "EXEC_ALGO_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $nonExecutable.simulationHandoffCreatesFill "Simulation handoff creates fill." "EXEC_ALGO_R006_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $schedule.executableScheduleCreated "Executable schedule created." "EXEC_ALGO_R006_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-False $schedule.scheduleThatCanBeSubmittedCreated "Submittable schedule created." "EXEC_ALGO_R006_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-False $slices.childSlicesCreated "Child slices created." "EXEC_ALGO_R006_FAIL_CHILD_SLICES_CREATED"
Require-False $slices.orderDomainSliceObjectsCreated "Order-domain slice objects created." "EXEC_ALGO_R006_FAIL_CHILD_SLICES_CREATED"
Require-False $childOrders.childOrdersCreated "Child orders created." "EXEC_ALGO_R006_FAIL_CHILD_ORDERS_CREATED"
Require-False $childOrders.omsChildOrdersCreated "OMS child orders created." "EXEC_ALGO_R006_FAIL_CHILD_ORDERS_CREATED"
Require-False $noBacktest.newBacktestExecuted "New backtest executed." "EXEC_ALGO_R006_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $noBacktest.newSimulationExecuted "New simulation executed." "EXEC_ALGO_R006_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $api.polygonApiCalled "Polygon API called." "EXEC_ALGO_R006_FAIL_API_CALL_DETECTED"
Require-False $api.lmaxCalled "LMAX called." "EXEC_ALGO_R006_FAIL_API_CALL_DETECTED"
Require-False $api.externalApiCalled "External API called." "EXEC_ALGO_R006_FAIL_API_CALL_DETECTED"
Require-False $runtime.brokerActivationDetected "Broker activation detected." "EXEC_ALGO_R006_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.socketOpened "Socket opened." "EXEC_ALGO_R006_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.tlsOpened "TLS opened." "EXEC_ALGO_R006_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.fixOpened "FIX opened." "EXEC_ALGO_R006_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataRequestSent "MarketDataRequest sent." "EXEC_ALGO_R006_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataResponseRead "MarketDataResponse read." "EXEC_ALGO_R006_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.schedulerServiceTimerPollingBackgroundJobIntroduced "Scheduler/service introduced." "EXEC_ALGO_R006_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $runtime.automaticExecutionIntroduced "Automatic execution introduced." "EXEC_ALGO_R006_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $fill.realFillsCreated "Real fills created." "EXEC_ALGO_R006_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $fill.fillEntitiesCreated "Fill entities created." "EXEC_ALGO_R006_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $execReport.executionReportEntitiesCreated "Execution report entities created." "EXEC_ALGO_R006_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $execReport.brokerExecutionReportsCreated "Broker reports created." "EXEC_ALGO_R006_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $order.ordersCreated "Orders created." "EXEC_ALGO_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.executableOrdersCreated "Executable orders created." "EXEC_ALGO_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.omsChildOrdersCreated "OMS child orders created." "EXEC_ALGO_R006_FAIL_CHILD_ORDERS_CREATED"
Require-False $route.routesCreated "Routes created." "EXEC_ALGO_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $route.submissionsCreated "Submissions created." "EXEC_ALGO_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

Require-True $usdjpy.caveatPreserved "USDJPY caveat missing." "EXEC_ALGO_R006_FAIL_USDJPY_CAVEAT_WEAKENED"
Require-True $usdjpy.requiresInversion "USDJPY inversion missing." "EXEC_ALGO_R006_FAIL_USDJPY_CAVEAT_WEAKENED"
if ($usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8") { Fail "USDJPY caveat weakened." "EXEC_ALGO_R006_FAIL_USDJPY_CAVEAT_WEAKENED" }
Require-False $usdjpy.weakened "USDJPY weakened." "EXEC_ALGO_R006_FAIL_USDJPY_CAVEAT_WEAKENED"
Require-False $usdjpy.audusdMisclassifiedFailed "AUDUSD misclassified failed." "EXEC_ALGO_R006_FAIL_AUDUSD_MISCLASSIFIED"
Require-True $lmax.referenceOnly "LMAX reference weakened." "EXEC_ALGO_R006_FAIL_API_CALL_DETECTED"
Require-False $lmax.lmaxCalledInR006 "LMAX called in R006." "EXEC_ALGO_R006_FAIL_API_CALL_DETECTED"
if ($lmax.audusdStatus -notmatch "not failed") { Fail "AUDUSD incorrectly marked failed." "EXEC_ALGO_R006_FAIL_AUDUSD_MISCLASSIFIED" }

Require-False $noExternal.polygonApiCalled "No-external audit shows Polygon call." "EXEC_ALGO_R006_FAIL_API_CALL_DETECTED"
Require-False $noExternal.lmaxCalled "No-external audit shows LMAX call." "EXEC_ALGO_R006_FAIL_API_CALL_DETECTED"
Require-False $noExternal.externalApiCalled "No-external audit shows external API call." "EXEC_ALGO_R006_FAIL_API_CALL_DETECTED"
Require-False $noExternal.newBacktestExecuted "No-external audit shows backtest." "EXEC_ALGO_R006_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $noExternal.newSimulationExecuted "No-external audit shows simulation." "EXEC_ALGO_R006_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $noExternal.newSimulationResultLinesCreated "No-external audit shows simulation lines." "EXEC_ALGO_R006_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $noExternal.executableScheduleCreated "No-external audit shows schedule." "EXEC_ALGO_R006_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-False $noExternal.childSlicesCreated "No-external audit shows child slices." "EXEC_ALGO_R006_FAIL_CHILD_SLICES_CREATED"
Require-False $noExternal.childOrdersCreated "No-external audit shows child orders." "EXEC_ALGO_R006_FAIL_CHILD_ORDERS_CREATED"
Require-False $noExternal.ordersFillsReportsRoutesSubmissionsCreated "No-external audit shows order-domain output." "EXEC_ALGO_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.liveBrokerProductionTradingStateMutated "No-external audit shows state mutation." "EXEC_ALGO_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.paperLedgerStateCommitted "No-external audit shows paper ledger commit." "EXEC_ALGO_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $forbidden.forbiddenActionsDetected "Forbidden action detected." "EXEC_ALGO_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED"

if ($evidence.dotnetBuildNoRestore -ne "PASS") { Fail "dotnet build evidence is not PASS." "EXEC_ALGO_R006_FAIL_BUILD_OR_TESTS" }
if ($evidence.focusedTests -notmatch "^PASS") { Fail "Focused R006 test evidence is not PASS." "EXEC_ALGO_R006_FAIL_BUILD_OR_TESTS" }
if ($evidence.unitTests -notmatch "^PASS") { Fail "Unit test evidence is not PASS." "EXEC_ALGO_R006_FAIL_BUILD_OR_TESTS" }

Write-Host "EXEC_ALGO_R006_PASS_SCHEDULE_SHAPE_OPERATOR_REVIEW_READY_NO_EXTERNAL"
Write-Host "EXEC_ALGO_R006_PASS_SIMULATION_HANDOFF_READY_NO_EXTERNAL"
Write-Host "EXEC_ALGO_R006_PASS_NO_EXECUTABLE_HANDOFF_NO_ORDER_GATE_READY_NO_EXTERNAL"
Write-Host "EXEC_ALGO_R006_PASS_SCHEDULE_SHAPE_REVIEW_REPORT_READY_NO_EXTERNAL"
exit 0
