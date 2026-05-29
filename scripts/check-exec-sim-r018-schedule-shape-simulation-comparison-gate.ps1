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
    "phase-exec-sim-r018-summary.md",
    "phase-exec-sim-r018-r017-handoff-acceptance-reference.json",
    "phase-exec-sim-r018-schedule-shape-simulation-contract.json",
    "phase-exec-sim-r018-schedule-shape-simulation-run-result.json",
    "phase-exec-sim-r018-schedule-shape-simulation-result-lines.json",
    "phase-exec-sim-r018-accepted-shapes-simulated.json",
    "phase-exec-sim-r018-excluded-shapes-preserved.json",
    "phase-exec-sim-r018-per-shape-tca-reports.json",
    "phase-exec-sim-r018-opening-build-schedule-shape-tca.json",
    "phase-exec-sim-r018-intraday-rebalance-schedule-shape-tca.json",
    "phase-exec-sim-r018-closing-flatten-schedule-shape-tca.json",
    "phase-exec-sim-r018-session-aggregate-schedule-shape-tca.json",
    "phase-exec-sim-r018-per-instrument-schedule-shape-eurusd-report.json",
    "phase-exec-sim-r018-per-instrument-schedule-shape-usdjpy-report.json",
    "phase-exec-sim-r018-per-instrument-schedule-shape-audusd-report.json",
    "phase-exec-sim-r018-policy-shape-comparison-vs-r013-r015.json",
    "phase-exec-sim-r018-ranking-by-shape-and-bar-role-median-slippage.json",
    "phase-exec-sim-r018-ranking-by-shape-and-bar-role-p95-slippage.json",
    "phase-exec-sim-r018-ranking-by-shape-and-bar-role-fill-ratio.json",
    "phase-exec-sim-r018-ranking-by-shape-and-bar-role-residual.json",
    "phase-exec-sim-r018-ranking-by-shape-and-bar-role-spread-paid.json",
    "phase-exec-sim-r018-no-overnight-residual-penalty-report.json",
    "phase-exec-sim-r018-opening-build-previous-evening-preservation.json",
    "phase-exec-sim-r018-closing-flatten-no-overnight-preservation.json",
    "phase-exec-sim-r018-benchmark-only-preservation.json",
    "phase-exec-sim-r018-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r018-wakett-blocked-shape-preservation.json",
    "phase-exec-sim-r018-cost-guidance-preservation.json",
    "phase-exec-sim-r018-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r018-usd-pair-normalization-preservation.json",
    "phase-exec-sim-r018-no-executable-schedule-audit.json",
    "phase-exec-sim-r018-no-child-slices-audit.json",
    "phase-exec-sim-r018-no-child-orders-audit.json",
    "phase-exec-sim-r018-no-real-fill-audit.json",
    "phase-exec-sim-r018-no-execution-report-audit.json",
    "phase-exec-sim-r018-no-order-created-audit.json",
    "phase-exec-sim-r018-no-route-no-submission-audit.json",
    "phase-exec-sim-r018-no-polygon-api-call-audit.json",
    "phase-exec-sim-r018-no-lmax-call-audit.json",
    "phase-exec-sim-r018-no-external-api-call-audit.json",
    "phase-exec-sim-r018-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r018-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r018-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r018-no-external-audit.json",
    "phase-exec-sim-r018-forbidden-actions-audit.json",
    "phase-exec-sim-r018-next-phase-recommendation.json",
    "phase-exec-sim-r018-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsDir $artifact))) {
        Fail "Required R018 artifact is missing: $artifact" "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
    }
}

$reference = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-r017-handoff-acceptance-reference.json") "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
$contract = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-schedule-shape-simulation-contract.json") "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
$run = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-schedule-shape-simulation-run-result.json") "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
$lines = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-schedule-shape-simulation-result-lines.json") "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
$accepted = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-accepted-shapes-simulated.json") "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
$excluded = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-excluded-shapes-preserved.json") "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
$perShape = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-per-shape-tca-reports.json") "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
$opening = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-opening-build-schedule-shape-tca.json") "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
$intraday = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-intraday-rebalance-schedule-shape-tca.json") "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
$closing = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-closing-flatten-schedule-shape-tca.json") "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
$aggregate = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-session-aggregate-schedule-shape-tca.json") "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
$eurusd = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-per-instrument-schedule-shape-eurusd-report.json") "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
$usdjpyReport = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-per-instrument-schedule-shape-usdjpy-report.json") "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
$audusd = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-per-instrument-schedule-shape-audusd-report.json") "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
$comparison = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-policy-shape-comparison-vs-r013-r015.json") "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
$median = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-ranking-by-shape-and-bar-role-median-slippage.json") "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
$p95 = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-ranking-by-shape-and-bar-role-p95-slippage.json") "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
$fillRatio = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-ranking-by-shape-and-bar-role-fill-ratio.json") "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
$residual = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-ranking-by-shape-and-bar-role-residual.json") "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
$spread = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-ranking-by-shape-and-bar-role-spread-paid.json") "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
$penalty = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-no-overnight-residual-penalty-report.json") "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
$previousEvening = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-opening-build-previous-evening-preservation.json") "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
$closingPreservation = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-closing-flatten-no-overnight-preservation.json") "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
$benchmark = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-benchmark-only-preservation.json") "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
$direct = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-direct-cross-exclusion-preservation.json") "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
$wakett = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-wakett-blocked-shape-preservation.json") "EXEC_SIM_R018_FAIL_WAKETT_BLOCK_WEAKENED"
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-cost-guidance-preservation.json") "EXEC_SIM_R018_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$nonmajor = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-nonmajor-calibration-preservation.json") "EXEC_SIM_R018_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$normalization = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-usd-pair-normalization-preservation.json") "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
$schedule = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-no-executable-schedule-audit.json") "EXEC_SIM_R018_FAIL_EXECUTABLE_SCHEDULE_CREATED"
$slices = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-no-child-slices-audit.json") "EXEC_SIM_R018_FAIL_CHILD_SLICES_CREATED"
$childOrders = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-no-child-orders-audit.json") "EXEC_SIM_R018_FAIL_CHILD_ORDERS_CREATED"
$fill = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-no-real-fill-audit.json") "EXEC_SIM_R018_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$execReport = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-no-execution-report-audit.json") "EXEC_SIM_R018_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$order = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-no-order-created-audit.json") "EXEC_SIM_R018_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$route = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-no-route-no-submission-audit.json") "EXEC_SIM_R018_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$api = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-no-external-api-call-audit.json") "EXEC_SIM_R018_FAIL_API_CALL_DETECTED"
$runtime = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-no-broker-marketdata-runtime-audit.json") "EXEC_SIM_R018_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-usdjpy-caveat-preservation.json") "EXEC_SIM_R018_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-lmax-readonly-baseline-reference.json") "EXEC_SIM_R018_FAIL_API_CALL_DETECTED"
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-no-external-audit.json") "EXEC_SIM_R018_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-forbidden-actions-audit.json") "EXEC_SIM_R018_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r018-build-test-validator-evidence.json") "EXEC_SIM_R018_FAIL_BUILD_OR_TESTS"

Require-True $reference.r017HandoffAcceptanceConsumed "R017 acceptance not consumed." "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
Require-True $reference.heldOrRejectedShapesExcluded "Held/rejected shapes not excluded." "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
Require-True $reference.noExternal "R018 reference not no-external." "EXEC_SIM_R018_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-True $contract.scheduleShapeSimulationContractCreated "Simulation contract missing." "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
Require-True $contract.simulationMayProduceFixtureOnlyTcaLines "Contract missing fixture-only TCA lines." "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
Require-False $contract.simulationMayCreateOrders "Contract allows orders." "EXEC_SIM_R018_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $contract.simulationMayCreateFills "Contract allows fills." "EXEC_SIM_R018_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $contract.simulationMayCreateExecutionReports "Contract allows reports." "EXEC_SIM_R018_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $contract.simulationMayCreateExecutableSchedules "Contract allows executable schedules." "EXEC_SIM_R018_FAIL_EXECUTABLE_SCHEDULE_CREATED"
if ($run.SimulationStatus -ne "CompletedFixtureOnlyScheduleShapeComparison") { Fail "Run result missing/completed status invalid." "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING" }
Require-True $run.NoApiCall "Run result allows API." "EXEC_SIM_R018_FAIL_API_CALL_DETECTED"
Require-True $run.NoBrokerRuntime "Run result allows broker runtime." "EXEC_SIM_R018_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-True $run.NoOrderDomainOutput "Run result allows order-domain output." "EXEC_SIM_R018_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-True $run.NoExecutableSchedule "Run result created schedule." "EXEC_SIM_R018_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-True $run.NoChildSlices "Run result created child slices." "EXEC_SIM_R018_FAIL_CHILD_SLICES_CREATED"
Require-True $run.NoChildOrders "Run result created child orders." "EXEC_SIM_R018_FAIL_CHILD_ORDERS_CREATED"
Require-True $run.NoRealFill "Run result created fill." "EXEC_SIM_R018_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-True $run.NoExecutionReport "Run result created report." "EXEC_SIM_R018_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"

Require-True $lines.resultLinesCreated "Simulation result lines missing." "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
if ($lines.resultLineEntityType -ne "FixtureOnlyPaperTcaSimulationLine") { Fail "Result lines misrepresented." "EXEC_SIM_R018_FAIL_RESULT_LINES_MISREPRESENTED_AS_FILLS" }
Require-False $lines.resultLinesAreFills "Result lines represented as fills." "EXEC_SIM_R018_FAIL_RESULT_LINES_MISREPRESENTED_AS_FILLS"
Require-False $lines.resultLinesAreExecutionReports "Result lines represented as reports." "EXEC_SIM_R018_FAIL_RESULT_LINES_MISREPRESENTED_AS_FILLS"
Require-False $lines.resultLinesAreOrders "Result lines represented as orders." "EXEC_SIM_R018_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $lines.schedulePhasesAreChildSlices "Schedule phases represented as child slices." "EXEC_SIM_R018_FAIL_CHILD_SLICES_CREATED"
foreach ($line in @($lines.lines)) {
    Require-True $line.FixtureOnly "Line not fixture-only." "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
    Require-True $line.PaperOnly "Line not paper-only." "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
    Require-True $line.NonExecutable "Line executable." "EXEC_SIM_R018_FAIL_EXECUTABLE_SCHEDULE_CREATED"
    Require-False $line.IsFill "Line is fill." "EXEC_SIM_R018_FAIL_RESULT_LINES_MISREPRESENTED_AS_FILLS"
    Require-False $line.IsExecutionReport "Line is execution report." "EXEC_SIM_R018_FAIL_RESULT_LINES_MISREPRESENTED_AS_FILLS"
    Require-False $line.IsOrder "Line is order." "EXEC_SIM_R018_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    Require-False $line.IsChildSlice "Line is child slice." "EXEC_SIM_R018_FAIL_CHILD_SLICES_CREATED"
    Require-False $line.IsSubmitted "Line submitted." "EXEC_SIM_R018_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    Require-False $line.HasBrokerRoute "Line has broker route." "EXEC_SIM_R018_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
}

Require-True $accepted.acceptedShapesSimulatedArtifactCreated "Accepted shapes simulated missing." "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
foreach ($shape in @("OpeningBuild", "IntradayRebalance", "ClosingFlatten", "USDJPYInverted", "BenchmarkOnly")) {
    if (@($accepted.acceptedShapesSimulated) -notcontains $shape) { Fail "Accepted shape not simulated: $shape" "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING" }
}
Require-True $accepted.allSimulatedShapesFixtureOnly "Simulated shapes not fixture-only." "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
Require-True $accepted.allSimulatedShapesNonExecutable "Simulated shapes executable." "EXEC_SIM_R018_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-True $accepted.allSimulatedShapesHaveNoChildOrders "Simulated shapes have child orders." "EXEC_SIM_R018_FAIL_CHILD_ORDERS_CREATED"
Require-True $accepted.allSimulatedShapesHaveNoExecutableSlices "Simulated shapes have executable slices." "EXEC_SIM_R018_FAIL_CHILD_SLICES_CREATED"
Require-True $excluded.excludedShapesPreservedArtifactCreated "Excluded shapes artifact missing." "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
Require-False $excluded.directCrossShapeProducedAcceptedResultLine "Direct-cross produced accepted result." "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
Require-False $excluded.nonmajorMissingConventionShapeProducedAcceptedResultLine "Missing convention produced accepted result." "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
Require-False $excluded.wakettBlockedShapeProducedAcceptedResultLine "Wakett blocked produced accepted result." "EXEC_SIM_R018_FAIL_WAKETT_BLOCK_WEAKENED"
Require-False $excluded.closingFlattenUnsafeFeedShapeProducedAcceptedResultLine "Unsafe feed produced accepted result." "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
Require-True $excluded.ineligibleShapesDoNotProduceAcceptedSimulationResultLines "Ineligible shapes produced result lines." "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"

Require-True $perShape.perShapeTcaReportsCreated "Per-shape reports missing." "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
Require-True $opening.reportCreated "OpeningBuild report missing." "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
Require-False $opening.preSessionExecutionAllowed "Opening pre-session execution allowed." "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
Require-True $intraday.normalCloseSeekingBehaviorPreserved "Intraday close-seeking weakened." "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
Require-True $closing.MustEndFlat "Closing MustEndFlat missing." "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
Require-False $closing.OvernightAllowed "Closing allows overnight." "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
Require-False $closing.BlindMarketFallbackAllowed "Closing blind fallback allowed." "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
Require-True $aggregate.reportCreated "Session aggregate missing." "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
Require-True $aggregate.noOvernightResidualPenaltyIncluded "No-overnight penalty missing from aggregate." "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
Require-True $eurusd.instrumentReportCreated "EURUSD report missing." "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
Require-True $usdjpyReport.instrumentReportCreated "USDJPY report missing." "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
Require-True $usdjpyReport.RequiresInversion "USDJPY report inversion missing." "EXEC_SIM_R018_FAIL_USDJPY_CAVEAT_WEAKENED"
Require-True $audusd.instrumentReportCreated "AUDUSD report missing." "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
if ($audusd.audusdStatus -ne "not failed") { Fail "AUDUSD misclassified failed." "EXEC_SIM_R018_FAIL_AUDUSD_MISCLASSIFIED" }
Require-True $comparison.comparisonCreated "R013/R015 comparison missing." "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
Require-False $comparison.r013R015NumericMetricsInvented "R013/R015 numeric metrics invented." "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
Require-True $median.rankingCreated "Median ranking missing." "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
Require-True $p95.rankingCreated "P95 ranking missing." "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
Require-True $fillRatio.rankingCreated "Fill ratio ranking missing." "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
Require-True $fillRatio.fillRatioDoesNotCreateFillEntity "Fill ratio creates fill entity." "EXEC_SIM_R018_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-True $residual.rankingCreated "Residual ranking missing." "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
Require-True $residual.closingFlattenNoOvernightPenaltyIncluded "Residual ranking missing no-overnight penalty." "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
Require-True $spread.rankingCreated "Spread ranking missing." "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
Require-True $penalty.NoOvernightResidualPenaltyIncluded "No-overnight penalty report missing." "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"
Require-True $penalty.ClosingFlattenResidualCostlierThanIntradayResidual "Closing residual not costlier." "EXEC_SIM_R018_FAIL_TCA_REPORTS_MISSING"

Require-True $previousEvening.openingBuildPreviousEveningPreserved "Opening previous-evening weakened." "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
Require-False $previousEvening.PreSessionOrderAllowed "Opening pre-session order allowed." "EXEC_SIM_R018_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $previousEvening.OvernightExposureBeforeSessionStartAllowed "Opening overnight allowed." "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
Require-True $closingPreservation.MustEndFlat "Closing preservation MustEndFlat missing." "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
Require-False $closingPreservation.OvernightAllowed "Closing preservation overnight allowed." "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
Require-False $closingPreservation.BlindMarketFallbackAllowed "Closing preservation blind fallback allowed." "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
Require-True $benchmark.BenchmarkOnly "Benchmark-only preservation missing." "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
Require-False $benchmark.benchmarkOnlyCreatesFill "Benchmark-only creates fill." "EXEC_SIM_R018_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $direct.directCrossExecutionAllowedByDefault "Direct-cross execution allowed." "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
Require-False $direct.directCrossAcceptedResultLinesCreated "Direct-cross result lines created." "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
Require-False $direct.guidanceWeakened "Direct-cross guidance weakened." "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"
Require-False $wakett.wakettShapeSimulatedAsAcceptedExecutableOrDefaultShape "Wakett accepted as executable/default." "EXEC_SIM_R018_FAIL_WAKETT_BLOCK_WEAKENED"
Require-False $wakett.PureLimitUntilCloseDefaultAllowed "PureLimit default allowed." "EXEC_SIM_R018_FAIL_WAKETT_BLOCK_WEAKENED"
Require-False $wakett.MechanicalMarketSlicesAroundCloseAllowed "Mechanical slices allowed." "EXEC_SIM_R018_FAIL_WAKETT_BLOCK_WEAKENED"
Require-False $wakett.AlwaysMarketAtCloseAllowed "AlwaysMarketAtClose allowed." "EXEC_SIM_R018_FAIL_WAKETT_BLOCK_WEAKENED"
Require-False $wakett.wakettPatternBlockWeakened "Wakett block weakened." "EXEC_SIM_R018_FAIL_WAKETT_BLOCK_WEAKENED"
if ($cost.bestCaseMajorTargetUsdPerMillion -ne 5) { Fail "5 USD/million target missing." "EXEC_SIM_R018_FAIL_5USD_PER_MILLION_UNIVERSALIZED" }
Require-True $cost.fiveUsdPerMillionBestCaseMajorOnly "5 USD/million not best-case major-only." "EXEC_SIM_R018_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-False $cost.fiveUsdPerMillionUniversalized "5 USD/million universalized." "EXEC_SIM_R018_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-True $nonmajor.nonMajorEmScandiCnhRequireLiquidityCalibration "Nonmajor calibration missing." "EXEC_SIM_R018_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
if ($normalization.executionUniverse -ne "USD-pair-only") { Fail "USD-pair execution weakened." "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING" }
Require-False $normalization.normalizationWeakened "USD-pair normalization weakened." "EXEC_SIM_R018_FAIL_SIMULATION_CONTRACT_MISSING"

Require-False $schedule.executableScheduleCreated "Executable schedule created." "EXEC_SIM_R018_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-False $slices.childSlicesCreated "Child slices created." "EXEC_SIM_R018_FAIL_CHILD_SLICES_CREATED"
Require-False $slices.schedulePhasesRepresentedAsChildSlices "Phases represented as slices." "EXEC_SIM_R018_FAIL_CHILD_SLICES_CREATED"
Require-False $childOrders.childOrdersCreated "Child orders created." "EXEC_SIM_R018_FAIL_CHILD_ORDERS_CREATED"
Require-False $fill.realFillsCreated "Real fills created." "EXEC_SIM_R018_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $fill.fillEntitiesCreated "Fill entities created." "EXEC_SIM_R018_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $fill.simulationResultLinesRepresentedAsFills "Lines represented as fills." "EXEC_SIM_R018_FAIL_RESULT_LINES_MISREPRESENTED_AS_FILLS"
Require-False $execReport.executionReportEntitiesCreated "Execution report entities created." "EXEC_SIM_R018_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $execReport.simulationResultLinesRepresentedAsExecutionReports "Lines represented as reports." "EXEC_SIM_R018_FAIL_RESULT_LINES_MISREPRESENTED_AS_FILLS"
Require-False $order.ordersCreated "Orders created." "EXEC_SIM_R018_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.simulationResultLinesRepresentedAsOrders "Lines represented as orders." "EXEC_SIM_R018_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $route.routesCreated "Routes created." "EXEC_SIM_R018_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $route.submissionsCreated "Submissions created." "EXEC_SIM_R018_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $api.polygonApiCalled "Polygon API called." "EXEC_SIM_R018_FAIL_API_CALL_DETECTED"
Require-False $api.lmaxCalled "LMAX called." "EXEC_SIM_R018_FAIL_API_CALL_DETECTED"
Require-False $api.externalApiCalled "External API called." "EXEC_SIM_R018_FAIL_API_CALL_DETECTED"
Require-False $runtime.brokerActivationDetected "Broker activation detected." "EXEC_SIM_R018_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.socketOpened "Socket opened." "EXEC_SIM_R018_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.tlsOpened "TLS opened." "EXEC_SIM_R018_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.fixOpened "FIX opened." "EXEC_SIM_R018_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataRequestSent "MarketDataRequest sent." "EXEC_SIM_R018_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataResponseRead "MarketDataResponse read." "EXEC_SIM_R018_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.apiWorkerLiveGatewayEnabled "API/Worker live gateway enabled." "EXEC_SIM_R018_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.schedulerServiceTimerPollingBackgroundJobIntroduced "Scheduler introduced." "EXEC_SIM_R018_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $runtime.automaticExecutionIntroduced "Automatic execution introduced." "EXEC_SIM_R018_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"

if ($usdjpy.PortfolioNormalizedSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY") { Fail "USDJPY symbol caveat weakened." "EXEC_SIM_R018_FAIL_USDJPY_CAVEAT_WEAKENED" }
Require-True $usdjpy.RequiresInversion "USDJPY inversion missing." "EXEC_SIM_R018_FAIL_USDJPY_CAVEAT_WEAKENED"
if ($usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8") { Fail "USDJPY caveat weakened." "EXEC_SIM_R018_FAIL_USDJPY_CAVEAT_WEAKENED" }
Require-True $usdjpy.caveatPreserved "USDJPY caveat missing." "EXEC_SIM_R018_FAIL_USDJPY_CAVEAT_WEAKENED"
Require-False $usdjpy.weakened "USDJPY weakened." "EXEC_SIM_R018_FAIL_USDJPY_CAVEAT_WEAKENED"
Require-False $usdjpy.audusdMisclassifiedFailed "AUDUSD misclassified failed." "EXEC_SIM_R018_FAIL_AUDUSD_MISCLASSIFIED"
Require-True $lmax.referenceOnly "LMAX reference weakened." "EXEC_SIM_R018_FAIL_API_CALL_DETECTED"
Require-False $lmax.lmaxCalledInR018 "LMAX called in R018." "EXEC_SIM_R018_FAIL_API_CALL_DETECTED"
if ($lmax.audusdStatus -notmatch "not failed") { Fail "AUDUSD incorrectly marked failed." "EXEC_SIM_R018_FAIL_AUDUSD_MISCLASSIFIED" }

Require-False $noExternal.polygonApiCalled "No-external audit shows Polygon call." "EXEC_SIM_R018_FAIL_API_CALL_DETECTED"
Require-False $noExternal.lmaxCalled "No-external audit shows LMAX call." "EXEC_SIM_R018_FAIL_API_CALL_DETECTED"
Require-False $noExternal.externalApiCalled "No-external audit shows external API call." "EXEC_SIM_R018_FAIL_API_CALL_DETECTED"
Require-False $noExternal.executableScheduleCreated "No-external audit shows executable schedule." "EXEC_SIM_R018_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-False $noExternal.childSlicesCreated "No-external audit shows child slices." "EXEC_SIM_R018_FAIL_CHILD_SLICES_CREATED"
Require-False $noExternal.childOrdersCreated "No-external audit shows child orders." "EXEC_SIM_R018_FAIL_CHILD_ORDERS_CREATED"
Require-False $noExternal.ordersFillsReportsRoutesSubmissionsCreated "No-external audit shows order-domain output." "EXEC_SIM_R018_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.simulationResultLinesRepresentedAsFills "No-external audit shows lines as fills." "EXEC_SIM_R018_FAIL_RESULT_LINES_MISREPRESENTED_AS_FILLS"
Require-False $noExternal.simulationResultLinesRepresentedAsExecutionReports "No-external audit shows lines as reports." "EXEC_SIM_R018_FAIL_RESULT_LINES_MISREPRESENTED_AS_FILLS"
Require-False $noExternal.schedulePhasesRepresentedAsChildSlices "No-external audit shows phases as slices." "EXEC_SIM_R018_FAIL_CHILD_SLICES_CREATED"
Require-False $noExternal.liveBrokerProductionTradingStateMutated "State mutated." "EXEC_SIM_R018_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.paperLedgerStateCommitted "Paper ledger committed." "EXEC_SIM_R018_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $forbidden.forbiddenActionsDetected "Forbidden action detected." "EXEC_SIM_R018_FAIL_NEW_EXTERNAL_ACTION_DETECTED"

if ($evidence.dotnetBuildNoRestore -ne "PASS") { Fail "dotnet build evidence is not PASS." "EXEC_SIM_R018_FAIL_BUILD_OR_TESTS" }
if ($evidence.focusedTests -notmatch "^PASS") { Fail "Focused R018 test evidence is not PASS." "EXEC_SIM_R018_FAIL_BUILD_OR_TESTS" }
if ($evidence.unitTests -notmatch "^PASS") { Fail "Unit test evidence is not PASS." "EXEC_SIM_R018_FAIL_BUILD_OR_TESTS" }

Write-Host "EXEC_SIM_R018_PASS_SCHEDULE_SHAPE_SIMULATION_COMPARISON_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R018_PASS_SESSION_AWARE_SCHEDULE_SHAPE_TCA_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R018_PASS_NO_EXECUTABLE_SCHEDULE_NO_CHILD_ORDER_SIM_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R018_PASS_NO_REAL_FILL_NO_ORDER_GATE_READY_NO_EXTERNAL"
exit 0
