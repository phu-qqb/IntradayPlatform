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
    "phase-exec-sim-r017-summary.md",
    "phase-exec-sim-r017-r006-handoff-reference.json",
    "phase-exec-sim-r017-schedule-shape-simulation-acceptance-contract.json",
    "phase-exec-sim-r017-schedule-shape-simulation-preflight-contract.json",
    "phase-exec-sim-r017-acceptance-result.json",
    "phase-exec-sim-r017-accepted-simulation-handoff-shapes.json",
    "phase-exec-sim-r017-held-simulation-handoff-shapes.json",
    "phase-exec-sim-r017-rejected-simulation-handoff-shapes.json",
    "phase-exec-sim-r017-eligible-shape-summary.json",
    "phase-exec-sim-r017-ineligible-shape-summary.json",
    "phase-exec-sim-r017-expected-future-simulation-inputs.json",
    "phase-exec-sim-r017-expected-future-simulation-outputs.json",
    "phase-exec-sim-r017-no-overnight-preservation.json",
    "phase-exec-sim-r017-first-bar-previous-evening-preservation.json",
    "phase-exec-sim-r017-usd-pair-normalization-preservation.json",
    "phase-exec-sim-r017-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r017-wakett-pattern-block-preservation.json",
    "phase-exec-sim-r017-cost-guidance-preservation.json",
    "phase-exec-sim-r017-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r017-no-simulation-backtest-audit.json",
    "phase-exec-sim-r017-no-tca-results-audit.json",
    "phase-exec-sim-r017-no-simulation-result-lines-audit.json",
    "phase-exec-sim-r017-no-executable-schedule-audit.json",
    "phase-exec-sim-r017-no-child-slices-audit.json",
    "phase-exec-sim-r017-no-child-orders-audit.json",
    "phase-exec-sim-r017-no-polygon-api-call-audit.json",
    "phase-exec-sim-r017-no-lmax-call-audit.json",
    "phase-exec-sim-r017-no-external-api-call-audit.json",
    "phase-exec-sim-r017-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r017-no-real-fill-audit.json",
    "phase-exec-sim-r017-no-execution-report-audit.json",
    "phase-exec-sim-r017-no-order-created-audit.json",
    "phase-exec-sim-r017-no-route-no-submission-audit.json",
    "phase-exec-sim-r017-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r017-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r017-no-external-audit.json",
    "phase-exec-sim-r017-forbidden-actions-audit.json",
    "phase-exec-sim-r017-next-phase-recommendation.json",
    "phase-exec-sim-r017-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsDir $artifact))) {
        Fail "Required R017 artifact is missing: $artifact" "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
    }
}

$reference = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-r006-handoff-reference.json") "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
$contract = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-schedule-shape-simulation-acceptance-contract.json") "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
$preflight = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-schedule-shape-simulation-preflight-contract.json") "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
$result = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-acceptance-result.json") "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
$accepted = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-accepted-simulation-handoff-shapes.json") "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
$held = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-held-simulation-handoff-shapes.json") "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
$rejected = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-rejected-simulation-handoff-shapes.json") "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
$eligible = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-eligible-shape-summary.json") "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
$ineligible = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-ineligible-shape-summary.json") "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
$inputs = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-expected-future-simulation-inputs.json") "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
$outputs = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-expected-future-simulation-outputs.json") "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
$noOvernight = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-no-overnight-preservation.json") "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
$previousEvening = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-first-bar-previous-evening-preservation.json") "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
$normalization = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-usd-pair-normalization-preservation.json") "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
$direct = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-direct-cross-exclusion-preservation.json") "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
$wakett = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-wakett-pattern-block-preservation.json") "EXEC_SIM_R017_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-cost-guidance-preservation.json") "EXEC_SIM_R017_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$nonmajor = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-nonmajor-calibration-preservation.json") "EXEC_SIM_R017_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$noSimulation = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-no-simulation-backtest-audit.json") "EXEC_SIM_R017_FAIL_SIMULATION_OR_BACKTEST_EXECUTED"
$tca = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-no-tca-results-audit.json") "EXEC_SIM_R017_FAIL_TCA_RESULTS_PRODUCED"
$lines = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-no-simulation-result-lines-audit.json") "EXEC_SIM_R017_FAIL_SIMULATION_RESULT_LINES_CREATED"
$schedule = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-no-executable-schedule-audit.json") "EXEC_SIM_R017_FAIL_EXECUTABLE_SCHEDULE_CREATED"
$slices = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-no-child-slices-audit.json") "EXEC_SIM_R017_FAIL_CHILD_SLICES_CREATED"
$childOrders = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-no-child-orders-audit.json") "EXEC_SIM_R017_FAIL_CHILD_ORDERS_CREATED"
$api = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-no-external-api-call-audit.json") "EXEC_SIM_R017_FAIL_API_CALL_DETECTED"
$runtime = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-no-broker-marketdata-runtime-audit.json") "EXEC_SIM_R017_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
$fill = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-no-real-fill-audit.json") "EXEC_SIM_R017_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$execReport = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-no-execution-report-audit.json") "EXEC_SIM_R017_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$order = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-no-order-created-audit.json") "EXEC_SIM_R017_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$route = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-no-route-no-submission-audit.json") "EXEC_SIM_R017_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-usdjpy-caveat-preservation.json") "EXEC_SIM_R017_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-lmax-readonly-baseline-reference.json") "EXEC_SIM_R017_FAIL_API_CALL_DETECTED"
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-no-external-audit.json") "EXEC_SIM_R017_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-forbidden-actions-audit.json") "EXEC_SIM_R017_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r017-build-test-validator-evidence.json") "EXEC_SIM_R017_FAIL_BUILD_OR_TESTS"

Require-True $reference.r006HandoffArtifactsReferenced "R006 handoff artifacts not referenced." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-True $reference.authorizationAcceptanceOnly "R017 not authorization-only." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-True $reference.noSimulation "R017 reference does not confirm no simulation." "EXEC_SIM_R017_FAIL_SIMULATION_OR_BACKTEST_EXECUTED"
Require-True $reference.noBacktest "R017 reference does not confirm no backtest." "EXEC_SIM_R017_FAIL_SIMULATION_OR_BACKTEST_EXECUTED"
Require-True $reference.noTcaResultsProduced "R017 reference does not confirm no TCA results." "EXEC_SIM_R017_FAIL_TCA_RESULTS_PRODUCED"

Require-True $contract.acceptanceContractCreated "Acceptance contract missing." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
if ($contract.AcceptanceStatus -ne "ScheduleShapeSimulationHandoffAcceptedNoExternal") { Fail "Acceptance status incorrect." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING" }
if ($contract.IntendedNextPhase -ne "EXEC-SIM-R018") { Fail "Next phase incorrect." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING" }
Require-True $contract.IsAuthorizationOnly "Contract not authorization-only." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-True $contract.NoSimulationRun "Contract does not block simulation." "EXEC_SIM_R017_FAIL_SIMULATION_OR_BACKTEST_EXECUTED"
Require-True $contract.NoBacktestRun "Contract does not block backtest." "EXEC_SIM_R017_FAIL_SIMULATION_OR_BACKTEST_EXECUTED"
Require-True $contract.NoTcaResultsProduced "Contract does not block TCA results." "EXEC_SIM_R017_FAIL_TCA_RESULTS_PRODUCED"
Require-True $contract.NoOrdersFillsReportsRoutes "Contract does not block order-domain output." "EXEC_SIM_R017_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-True $contract.NoExternalApiCalls "Contract does not block external API." "EXEC_SIM_R017_FAIL_API_CALL_DETECTED"
Require-True $preflight.preflightContractCreated "Preflight contract missing." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-True $preflight.allRequiredEligibleShapesPresent "Missing eligible shapes." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-True $preflight.allAcceptedShapesNonExecutable "Accepted shape executable risk." "EXEC_SIM_R017_FAIL_SIMULATION_HANDOFF_EXECUTABLE"
Require-True $preflight.blockedShapesRemainExcluded "Blocked shapes not excluded." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-True $preflight.noSimulationRun "Preflight does not block simulation." "EXEC_SIM_R017_FAIL_SIMULATION_OR_BACKTEST_EXECUTED"
Require-True $preflight.noTcaResultsProduced "Preflight does not block TCA." "EXEC_SIM_R017_FAIL_TCA_RESULTS_PRODUCED"
Require-True $result.acceptanceResultCreated "Acceptance result missing." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-True $result.isAuthorizationOnly "Acceptance result not authorization only." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-True $result.noSimulationRun "Acceptance result ran simulation." "EXEC_SIM_R017_FAIL_SIMULATION_OR_BACKTEST_EXECUTED"
Require-True $result.noTcaResultsProduced "Acceptance result produced TCA." "EXEC_SIM_R017_FAIL_TCA_RESULTS_PRODUCED"
Require-True $result.noSimulationResultLinesCreated "Acceptance result produced simulation lines." "EXEC_SIM_R017_FAIL_SIMULATION_RESULT_LINES_CREATED"

Require-True $accepted.acceptedHandoffShapesCreated "Accepted shapes missing." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-True $accepted.allAcceptedShapesDesignOnly "Accepted shapes not design-only." "EXEC_SIM_R017_FAIL_SIMULATION_HANDOFF_EXECUTABLE"
Require-True $accepted.allAcceptedShapesPaperOnly "Accepted shapes not paper-only." "EXEC_SIM_R017_FAIL_SIMULATION_HANDOFF_EXECUTABLE"
Require-True $accepted.allAcceptedShapesNonExecutable "Accepted shapes executable." "EXEC_SIM_R017_FAIL_SIMULATION_HANDOFF_EXECUTABLE"
Require-True $accepted.allAcceptedShapesNotOrders "Accepted shapes are orders." "EXEC_SIM_R017_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-True $accepted.allAcceptedShapesNotSubmitted "Accepted shapes submitted." "EXEC_SIM_R017_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-True $accepted.allAcceptedShapesNoBrokerRoute "Accepted shapes have broker route." "EXEC_SIM_R017_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-True $accepted.allAcceptedShapesHaveNoChildOrders "Accepted shapes have child orders." "EXEC_SIM_R017_FAIL_CHILD_ORDERS_CREATED"
Require-True $accepted.allAcceptedShapesHaveNoExecutableSlices "Accepted shapes have executable slices." "EXEC_SIM_R017_FAIL_CHILD_SLICES_CREATED"
if (-not (@($accepted.acceptedShapes).BarRole -contains "OpeningBuild")) { Fail "OpeningBuild not accepted." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING" }
if (-not (@($accepted.acceptedShapes).BarRole -contains "IntradayRebalance")) { Fail "IntradayRebalance not accepted." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING" }
$closingAccepted = @($accepted.acceptedShapes) | Where-Object { $_.BarRole -eq "ClosingFlatten" } | Select-Object -First 1
if ($null -eq $closingAccepted) { Fail "ClosingFlatten not accepted." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING" }
Require-True $closingAccepted.MustEndFlat "Closing accepted without MustEndFlat." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-False $closingAccepted.OvernightAllowed "Closing accepted with overnight allowed." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
$usdjpyAccepted = @($accepted.acceptedShapes) | Where-Object { $_.ExecutionTradableSymbol -eq "USDJPY" } | Select-Object -First 1
if ($null -eq $usdjpyAccepted) { Fail "USDJPY inverted shape not accepted." "EXEC_SIM_R017_FAIL_USDJPY_CAVEAT_WEAKENED" }
if ($usdjpyAccepted.PortfolioNormalizedSymbol -ne "JPYUSD") { Fail "USDJPY normalized symbol weakened." "EXEC_SIM_R017_FAIL_USDJPY_CAVEAT_WEAKENED" }
Require-True $usdjpyAccepted.RequiresInversion "USDJPY inversion missing." "EXEC_SIM_R017_FAIL_USDJPY_CAVEAT_WEAKENED"
$benchmarkAccepted = @($accepted.acceptedShapes) | Where-Object { $_.BenchmarkOnly -eq $true } | Select-Object -First 1
if ($null -eq $benchmarkAccepted) { Fail "Benchmark-only shape not accepted." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING" }
Require-True $benchmarkAccepted.NonExecutable "Benchmark-only shape executable." "EXEC_SIM_R017_FAIL_SIMULATION_HANDOFF_EXECUTABLE"

Require-True $held.heldHandoffShapesCreated "Held shape artifact missing." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-False $held.heldShapesCreateExecutableSchedules "Held shape creates schedule." "EXEC_SIM_R017_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-False $held.heldShapesCreateOrderDomainObjects "Held shape creates order-domain object." "EXEC_SIM_R017_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-True $rejected.rejectedHandoffShapesCreated "Rejected shape artifact missing." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-False $rejected.rejectedShapesCreateExecutableSchedules "Rejected shape creates schedule." "EXEC_SIM_R017_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-False $rejected.rejectedShapesCreateOrderDomainObjects "Rejected shape creates order-domain object." "EXEC_SIM_R017_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-True $eligible.OpeningBuildEligible "OpeningBuild not eligible." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-True $eligible.IntradayRebalanceEligible "IntradayRebalance not eligible." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-True $eligible.ClosingFlattenEligible "ClosingFlatten not eligible." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-True $eligible.ClosingFlattenRequiresNoOvernightPreserved "Closing no-overnight not required." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-True $eligible.UsdJpyInvertedEligible "USDJPY inverted not eligible." "EXEC_SIM_R017_FAIL_USDJPY_CAVEAT_WEAKENED"
Require-True $eligible.BenchmarkOnlyEligible "Benchmark-only not eligible." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-True $ineligible.DirectCrossExcluded "Direct-cross not excluded." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-True $ineligible.NonmajorMissingConventionHeldOrExcluded "Missing convention not held/excluded." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-True $ineligible.WakettBlockedShapesExcluded "Wakett shapes not excluded." "EXEC_SIM_R017_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
Require-True $ineligible.ClosingFlattenUnsafeFeedHeldForManualReview "Unsafe feed not held." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-False $ineligible.anyIneligibleShapeAccepted "Ineligible shape accepted." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-False $ineligible.anyExecutableShapeAccepted "Executable shape accepted." "EXEC_SIM_R017_FAIL_SIMULATION_HANDOFF_EXECUTABLE"

Require-True $inputs.expectedFutureSimulationInputsCreated "Future simulation inputs missing." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
foreach ($required in @("OpeningBuild", "IntradayRebalance", "ClosingFlatten", "USDJPYInverted", "BenchmarkOnly")) {
    if (@($inputs.acceptedSimulationHandoffShapes) -notcontains $required) { Fail "Future input missing $required." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING" }
}
foreach ($symbol in @("EURUSD", "USDJPY", "AUDUSD")) {
    if (@($inputs.acceptedRealOfflineQuoteFiles) -notcontains $symbol) { Fail "Future input missing symbol $symbol." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING" }
}
Require-True $inputs.requiresExistingCloseBenchmarksAndFeedQualityReadiness "Future input missing readiness requirement." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-True $inputs.noNewQuoteImportInR017 "R017 allows quote import." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-True $outputs.expectedFutureSimulationOutputsCreated "Future simulation outputs missing." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
if (@($outputs.futureSimulationMustRemain) -notcontains "NoRealFill") { Fail "Future output safety missing NoRealFill." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING" }
Require-True $outputs.noOutputsProducedInR017 "R017 produced future outputs." "EXEC_SIM_R017_FAIL_TCA_RESULTS_PRODUCED"
Require-True $outputs.noTcaResultsProducedInR017 "R017 produced TCA results." "EXEC_SIM_R017_FAIL_TCA_RESULTS_PRODUCED"
Require-True $outputs.noSimulationResultLinesCreatedInR017 "R017 produced simulation lines." "EXEC_SIM_R017_FAIL_SIMULATION_RESULT_LINES_CREATED"

Require-True $noOvernight.MustEndFlat "No-overnight MustEndFlat missing." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-False $noOvernight.OvernightAllowed "Overnight allowed." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-False $noOvernight.BlindMarketFallbackAllowed "Blind fallback allowed." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-True $previousEvening.KnownAtTimestampMayBePreviousEvening "Previous-evening handling weakened." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-False $previousEvening.PreSessionOrderAllowed "Pre-session order allowed." "EXEC_SIM_R017_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $previousEvening.OvernightExposureBeforeSessionStartAllowed "Overnight exposure allowed." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
if ($normalization.executionUniverse -ne "USD-pair-only") { Fail "USD-pair execution weakened." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING" }
Require-False $normalization.normalizationWeakened "USD-pair normalization weakened." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-False $direct.directCrossExecutionAllowedByDefault "Direct-cross execution allowed." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-True $direct.directCrossBlockedShapeExcluded "Direct-cross blocked shape not excluded." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-False $direct.guidanceWeakened "Direct-cross guidance weakened." "EXEC_SIM_R017_FAIL_ACCEPTANCE_CONTRACT_MISSING"
Require-False $wakett.PureLimitUntilCloseDefaultAllowed "PureLimit default allowed." "EXEC_SIM_R017_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
Require-False $wakett.MechanicalMarketSlicesAroundCloseAllowed "Mechanical slices allowed." "EXEC_SIM_R017_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
Require-False $wakett.AlwaysMarketAtCloseAllowed "AlwaysMarketAtClose allowed." "EXEC_SIM_R017_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
Require-False $wakett.wakettPatternBlockWeakened "Wakett block weakened." "EXEC_SIM_R017_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
if ($cost.bestCaseMajorTargetUsdPerMillion -ne 5) { Fail "5 USD/million target missing." "EXEC_SIM_R017_FAIL_5USD_PER_MILLION_UNIVERSALIZED" }
Require-True $cost.fiveUsdPerMillionBestCaseMajorOnly "5 USD/million not best-case major-only." "EXEC_SIM_R017_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-False $cost.fiveUsdPerMillionUniversalized "5 USD/million universalized." "EXEC_SIM_R017_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-True $nonmajor.nonMajorEmScandiCnhRequireLiquidityCalibration "Nonmajor calibration missing." "EXEC_SIM_R017_FAIL_5USD_PER_MILLION_UNIVERSALIZED"

Require-False $noSimulation.newSimulationExecuted "Simulation executed." "EXEC_SIM_R017_FAIL_SIMULATION_OR_BACKTEST_EXECUTED"
Require-False $noSimulation.newBacktestExecuted "Backtest executed." "EXEC_SIM_R017_FAIL_SIMULATION_OR_BACKTEST_EXECUTED"
Require-False $tca.tcaResultsProduced "TCA results produced." "EXEC_SIM_R017_FAIL_TCA_RESULTS_PRODUCED"
Require-False $lines.simulationResultLinesCreated "Simulation result lines created." "EXEC_SIM_R017_FAIL_SIMULATION_RESULT_LINES_CREATED"
Require-False $schedule.executableScheduleCreated "Executable schedule created." "EXEC_SIM_R017_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-False $slices.childSlicesCreated "Child slices created." "EXEC_SIM_R017_FAIL_CHILD_SLICES_CREATED"
Require-False $slices.orderDomainSliceObjectsCreated "Order-domain slice object created." "EXEC_SIM_R017_FAIL_CHILD_SLICES_CREATED"
Require-False $childOrders.childOrdersCreated "Child orders created." "EXEC_SIM_R017_FAIL_CHILD_ORDERS_CREATED"
Require-False $api.polygonApiCalled "Polygon API called." "EXEC_SIM_R017_FAIL_API_CALL_DETECTED"
Require-False $api.lmaxCalled "LMAX called." "EXEC_SIM_R017_FAIL_API_CALL_DETECTED"
Require-False $api.externalApiCalled "External API called." "EXEC_SIM_R017_FAIL_API_CALL_DETECTED"
Require-False $runtime.brokerActivationDetected "Broker activation detected." "EXEC_SIM_R017_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.socketOpened "Socket opened." "EXEC_SIM_R017_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.tlsOpened "TLS opened." "EXEC_SIM_R017_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.fixOpened "FIX opened." "EXEC_SIM_R017_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataRequestSent "MarketDataRequest sent." "EXEC_SIM_R017_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataResponseRead "MarketDataResponse read." "EXEC_SIM_R017_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.schedulerServiceTimerPollingBackgroundJobIntroduced "Scheduler/service introduced." "EXEC_SIM_R017_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $runtime.automaticExecutionIntroduced "Automatic execution introduced." "EXEC_SIM_R017_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $fill.realFillsCreated "Real fills created." "EXEC_SIM_R017_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $fill.fillEntitiesCreated "Fill entities created." "EXEC_SIM_R017_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $execReport.executionReportEntitiesCreated "Execution reports created." "EXEC_SIM_R017_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $execReport.brokerExecutionReportsCreated "Broker execution reports created." "EXEC_SIM_R017_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $order.ordersCreated "Orders created." "EXEC_SIM_R017_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.executableOrdersCreated "Executable orders created." "EXEC_SIM_R017_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $route.routesCreated "Routes created." "EXEC_SIM_R017_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $route.submissionsCreated "Submissions created." "EXEC_SIM_R017_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

if ($usdjpy.PortfolioNormalizedSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY") { Fail "USDJPY symbol caveat weakened." "EXEC_SIM_R017_FAIL_USDJPY_CAVEAT_WEAKENED" }
Require-True $usdjpy.RequiresInversion "USDJPY inversion missing." "EXEC_SIM_R017_FAIL_USDJPY_CAVEAT_WEAKENED"
if ($usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8") { Fail "USDJPY caveat weakened." "EXEC_SIM_R017_FAIL_USDJPY_CAVEAT_WEAKENED" }
Require-True $usdjpy.caveatPreserved "USDJPY caveat not preserved." "EXEC_SIM_R017_FAIL_USDJPY_CAVEAT_WEAKENED"
Require-False $usdjpy.weakened "USDJPY weakened." "EXEC_SIM_R017_FAIL_USDJPY_CAVEAT_WEAKENED"
Require-False $usdjpy.audusdMisclassifiedFailed "AUDUSD misclassified failed." "EXEC_SIM_R017_FAIL_AUDUSD_MISCLASSIFIED"
Require-True $lmax.referenceOnly "LMAX reference weakened." "EXEC_SIM_R017_FAIL_API_CALL_DETECTED"
Require-False $lmax.lmaxCalledInR017 "LMAX called in R017." "EXEC_SIM_R017_FAIL_API_CALL_DETECTED"
if ($lmax.audusdStatus -notmatch "not failed") { Fail "AUDUSD incorrectly marked failed." "EXEC_SIM_R017_FAIL_AUDUSD_MISCLASSIFIED" }

Require-False $noExternal.polygonApiCalled "No-external audit shows Polygon call." "EXEC_SIM_R017_FAIL_API_CALL_DETECTED"
Require-False $noExternal.lmaxCalled "No-external audit shows LMAX call." "EXEC_SIM_R017_FAIL_API_CALL_DETECTED"
Require-False $noExternal.externalApiCalled "No-external audit shows external API call." "EXEC_SIM_R017_FAIL_API_CALL_DETECTED"
Require-False $noExternal.newSimulationExecuted "No-external audit shows simulation." "EXEC_SIM_R017_FAIL_SIMULATION_OR_BACKTEST_EXECUTED"
Require-False $noExternal.newBacktestExecuted "No-external audit shows backtest." "EXEC_SIM_R017_FAIL_SIMULATION_OR_BACKTEST_EXECUTED"
Require-False $noExternal.tcaResultsProduced "No-external audit shows TCA results." "EXEC_SIM_R017_FAIL_TCA_RESULTS_PRODUCED"
Require-False $noExternal.simulationResultLinesCreated "No-external audit shows simulation lines." "EXEC_SIM_R017_FAIL_SIMULATION_RESULT_LINES_CREATED"
Require-False $noExternal.executableScheduleCreated "No-external audit shows executable schedule." "EXEC_SIM_R017_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-False $noExternal.childSlicesCreated "No-external audit shows child slices." "EXEC_SIM_R017_FAIL_CHILD_SLICES_CREATED"
Require-False $noExternal.childOrdersCreated "No-external audit shows child orders." "EXEC_SIM_R017_FAIL_CHILD_ORDERS_CREATED"
Require-False $noExternal.ordersFillsReportsRoutesSubmissionsCreated "No-external audit shows order-domain output." "EXEC_SIM_R017_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.liveBrokerProductionTradingStateMutated "No-external audit shows state mutation." "EXEC_SIM_R017_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.paperLedgerStateCommitted "No-external audit shows paper ledger commit." "EXEC_SIM_R017_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $forbidden.forbiddenActionsDetected "Forbidden action detected." "EXEC_SIM_R017_FAIL_NEW_EXTERNAL_ACTION_DETECTED"

if ($evidence.dotnetBuildNoRestore -ne "PASS") { Fail "dotnet build evidence is not PASS." "EXEC_SIM_R017_FAIL_BUILD_OR_TESTS" }
if ($evidence.focusedTests -notmatch "^PASS") { Fail "Focused R017 test evidence is not PASS." "EXEC_SIM_R017_FAIL_BUILD_OR_TESTS" }
if ($evidence.unitTests -notmatch "^PASS") { Fail "Unit test evidence is not PASS." "EXEC_SIM_R017_FAIL_BUILD_OR_TESTS" }

Write-Host "EXEC_SIM_R017_PASS_SCHEDULE_SHAPE_HANDOFF_ACCEPTANCE_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R017_PASS_FUTURE_SIMULATION_PREFLIGHT_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R017_PASS_NO_SIMULATION_NO_TCA_RESULTS_GATE_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R017_PASS_NONEXECUTABLE_HANDOFF_ACCEPTED_NO_EXTERNAL"
exit 0
