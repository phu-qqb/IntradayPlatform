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
    "phase-exec-algo-r005-summary.md",
    "phase-exec-algo-r005-r004-preview-reference.json",
    "phase-exec-algo-r005-schedule-shape-contract.json",
    "phase-exec-algo-r005-schedule-phase-contract.json",
    "phase-exec-algo-r005-opening-build-schedule-shape.json",
    "phase-exec-algo-r005-intraday-rebalance-schedule-shape.json",
    "phase-exec-algo-r005-closing-flatten-schedule-shape.json",
    "phase-exec-algo-r005-closing-flatten-unsafe-feed-shape.json",
    "phase-exec-algo-r005-direct-cross-blocked-shape.json",
    "phase-exec-algo-r005-usdjpy-inverted-schedule-shape.json",
    "phase-exec-algo-r005-nonmajor-missing-convention-shape.json",
    "phase-exec-algo-r005-wakett-blocked-schedule-shapes.json",
    "phase-exec-algo-r005-benchmark-only-schedule-shapes.json",
    "phase-exec-algo-r005-schedule-shape-statuses.json",
    "phase-exec-algo-r005-blocked-schedule-shape-families.json",
    "phase-exec-algo-r005-no-overnight-flatten-schedule-preservation.json",
    "phase-exec-algo-r005-first-bar-previous-evening-schedule-preservation.json",
    "phase-exec-algo-r005-cost-guidance-preservation.json",
    "phase-exec-algo-r005-nonmajor-calibration-preservation.json",
    "phase-exec-algo-r005-usd-pair-normalization-preservation.json",
    "phase-exec-algo-r005-direct-cross-exclusion-preservation.json",
    "phase-exec-algo-r005-wakett-pattern-block-preservation.json",
    "phase-exec-algo-r005-non-executable-schedule-shape-audit.json",
    "phase-exec-algo-r005-no-executable-schedule-audit.json",
    "phase-exec-algo-r005-no-child-slices-audit.json",
    "phase-exec-algo-r005-no-child-orders-audit.json",
    "phase-exec-algo-r005-no-new-backtest-audit.json",
    "phase-exec-algo-r005-no-polygon-api-call-audit.json",
    "phase-exec-algo-r005-no-lmax-call-audit.json",
    "phase-exec-algo-r005-no-external-api-call-audit.json",
    "phase-exec-algo-r005-no-broker-marketdata-runtime-audit.json",
    "phase-exec-algo-r005-no-real-fill-audit.json",
    "phase-exec-algo-r005-no-execution-report-audit.json",
    "phase-exec-algo-r005-no-order-created-audit.json",
    "phase-exec-algo-r005-no-route-no-submission-audit.json",
    "phase-exec-algo-r005-usdjpy-caveat-preservation.json",
    "phase-exec-algo-r005-lmax-readonly-baseline-reference.json",
    "phase-exec-algo-r005-no-external-audit.json",
    "phase-exec-algo-r005-forbidden-actions-audit.json",
    "phase-exec-algo-r005-next-phase-recommendation.json",
    "phase-exec-algo-r005-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsDir $artifact))) {
        Fail "Required R005 artifact is missing: $artifact" "EXEC_ALGO_R005_FAIL_BUILD_OR_TESTS"
    }
}

$reference = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-r004-preview-reference.json") "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
$shapeContract = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-schedule-shape-contract.json") "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
$phaseContract = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-schedule-phase-contract.json") "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
$opening = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-opening-build-schedule-shape.json") "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
$intraday = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-intraday-rebalance-schedule-shape.json") "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
$closing = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-closing-flatten-schedule-shape.json") "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
$unsafe = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-closing-flatten-unsafe-feed-shape.json") "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
$direct = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-direct-cross-blocked-shape.json") "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
$usdjpyShape = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-usdjpy-inverted-schedule-shape.json") "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
$nonmajorShape = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-nonmajor-missing-convention-shape.json") "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
$wakettShape = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-wakett-blocked-schedule-shapes.json") "EXEC_ALGO_R005_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
$benchmarkShapes = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-benchmark-only-schedule-shapes.json") "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
$statuses = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-schedule-shape-statuses.json") "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
$blockedFamilies = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-blocked-schedule-shape-families.json") "EXEC_ALGO_R005_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
$overnight = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-no-overnight-flatten-schedule-preservation.json") "EXEC_ALGO_R005_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
$firstBar = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-first-bar-previous-evening-schedule-preservation.json") "EXEC_ALGO_R005_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-cost-guidance-preservation.json") "EXEC_ALGO_R005_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$nonmajor = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-nonmajor-calibration-preservation.json") "EXEC_ALGO_R005_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$normalization = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-usd-pair-normalization-preservation.json") "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
$directPreservation = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-direct-cross-exclusion-preservation.json") "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
$wakett = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-wakett-pattern-block-preservation.json") "EXEC_ALGO_R005_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
$nonExecutable = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-non-executable-schedule-shape-audit.json") "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
$schedule = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-no-executable-schedule-audit.json") "EXEC_ALGO_R005_FAIL_EXECUTABLE_SCHEDULE_CREATED"
$slices = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-no-child-slices-audit.json") "EXEC_ALGO_R005_FAIL_CHILD_SLICES_CREATED"
$childOrders = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-no-child-orders-audit.json") "EXEC_ALGO_R005_FAIL_CHILD_ORDERS_CREATED"
$noBacktest = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-no-new-backtest-audit.json") "EXEC_ALGO_R005_FAIL_NEW_BACKTEST_EXECUTED"
$api = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-no-external-api-call-audit.json") "EXEC_ALGO_R005_FAIL_API_CALL_DETECTED"
$runtime = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-no-broker-marketdata-runtime-audit.json") "EXEC_ALGO_R005_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
$fill = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-no-real-fill-audit.json") "EXEC_ALGO_R005_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$report = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-no-execution-report-audit.json") "EXEC_ALGO_R005_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$order = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-no-order-created-audit.json") "EXEC_ALGO_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$route = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-no-route-no-submission-audit.json") "EXEC_ALGO_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-usdjpy-caveat-preservation.json") "EXEC_ALGO_R005_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-lmax-readonly-baseline-reference.json") "EXEC_ALGO_R005_FAIL_API_CALL_DETECTED"
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-no-external-audit.json") "EXEC_ALGO_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-forbidden-actions-audit.json") "EXEC_ALGO_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r005-build-test-validator-evidence.json") "EXEC_ALGO_R005_FAIL_BUILD_OR_TESTS"

if ($reference.sourcePreviewPhase -ne "EXEC-ALGO-R004") { Fail "R004 preview reference missing." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING" }
Require-True $reference.r004PreviewsReferenced "R004 previews not referenced." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
Require-True $reference.scheduleShapeOnly "R005 is not schedule-shape-only." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
Require-True $reference.noSimulation "R005 reference does not confirm no simulation." "EXEC_ALGO_R005_FAIL_NEW_BACKTEST_EXECUTED"
Require-True $reference.noBacktest "R005 reference does not confirm no backtest." "EXEC_ALGO_R005_FAIL_NEW_BACKTEST_EXECUTED"

Require-True $shapeContract.scheduleShapeContractCreated "Schedule shape contract missing." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
Require-True $shapeContract.allScheduleShapesDesignOnly "Schedule shapes not design-only." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
Require-True $shapeContract.allScheduleShapesNonExecutable "Schedule shapes executable." "EXEC_ALGO_R005_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-True $shapeContract.allScheduleShapesNoChildOrders "Schedule shapes allow child orders." "EXEC_ALGO_R005_FAIL_CHILD_ORDERS_CREATED"
Require-True $shapeContract.allScheduleShapesNoExecutableSlices "Schedule shapes allow executable slices." "EXEC_ALGO_R005_FAIL_CHILD_SLICES_CREATED"
Require-False $shapeContract.createsExecutableSchedule "Shape contract creates executable schedule." "EXEC_ALGO_R005_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-False $shapeContract.createsOrderDomainObjects "Shape contract creates order-domain objects." "EXEC_ALGO_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-True $phaseContract.schedulePhaseContractCreated "Schedule phase contract missing." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
Require-True $phaseContract.schedulePhasesAreMetadataOnly "Schedule phases not metadata-only." "EXEC_ALGO_R005_FAIL_CHILD_SLICES_CREATED"
Require-True $phaseContract.schedulePhasesAreNotChildSlices "Schedule phases are child slices." "EXEC_ALGO_R005_FAIL_CHILD_SLICES_CREATED"
Require-True $phaseContract.schedulePhasesAreNotExecutable "Schedule phases executable." "EXEC_ALGO_R005_FAIL_EXECUTABLE_SCHEDULE_CREATED"

Require-True $opening.KnownAtTimestampMayBePreviousEvening "Opening previous-evening handling missing." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
Require-True $opening.EarliestExecutionTimestampMustRemainSessionStartOrExplicitAllowedStart "Opening earliest execution weakened." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
Require-False $opening.PreSessionOrderAllowed "Opening creates pre-session order." "EXEC_ALGO_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $opening.OvernightExposureBeforeSessionStartAllowed "Opening allows overnight exposure." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
if (@($opening.Phases).Count -ne 3) { Fail "Opening shape does not have three design phases." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING" }
Require-True $intraday.NormalCloseSeekingBehaviorPreserved "Intraday normal close-seeking weakened." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
Require-True $intraday.ThreePhaseCloseSeekingStructure "Intraday three-phase structure missing." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
if (@($intraday.Phases).Count -ne 3) { Fail "Intraday shape does not have three phases." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING" }
Require-True $closing.MustEndFlat "Closing shape missing MustEndFlat." "EXEC_ALGO_R005_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-False $closing.OvernightAllowed "Closing shape allows overnight." "EXEC_ALGO_R005_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
if ($closing.ResidualPenaltyBucket -ne "NoOvernightCritical") { Fail "Closing residual penalty weakened." "EXEC_ALGO_R005_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED" }
if ($closing.MaxResidualAtClose -ne "StrictlyLowerThanIntradayRebalance") { Fail "Closing residual threshold not strict." "EXEC_ALGO_R005_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED" }
Require-False $closing.BlindMarketFallbackAllowed "Closing allows blind market fallback." "EXEC_ALGO_R005_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-False $closing.FiveMarketSlicesDefaultAllowed "Closing allows five-market-slice default." "EXEC_ALGO_R005_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-False $closing.AlwaysMarketAtCloseDefaultAllowed "Closing allows AlwaysMarketAtClose." "EXEC_ALGO_R005_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"

foreach ($shape in @($opening, $intraday, $closing, $unsafe, $direct, $usdjpyShape, $nonmajorShape)) {
    Require-True $shape.IsDesignOnly "Shape not design-only." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
    Require-True $shape.IsPaperOnly "Shape not paper-only." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
    Require-False $shape.IsExecutable "Shape executable." "EXEC_ALGO_R005_FAIL_EXECUTABLE_SCHEDULE_CREATED"
    Require-False $shape.IsOrder "Shape is order." "EXEC_ALGO_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    Require-False $shape.IsSubmitted "Shape submitted." "EXEC_ALGO_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    Require-False $shape.HasBrokerRoute "Shape has broker route." "EXEC_ALGO_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    Require-False $shape.HasChildOrders "Shape has child orders." "EXEC_ALGO_R005_FAIL_CHILD_ORDERS_CREATED"
    Require-False $shape.HasExecutableSlices "Shape has executable slices." "EXEC_ALGO_R005_FAIL_CHILD_SLICES_CREATED"
}

if ($unsafe.ScheduleShapeStatus -ne "ScheduleShapeRequiresManualReview") { Fail "Unsafe feed shape not manual review." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING" }
Require-False $unsafe.BlindMarketFallbackAllowed "Unsafe feed allows blind fallback." "EXEC_ALGO_R005_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-False $unsafe.ExecutableScheduleCreated "Unsafe feed creates schedule." "EXEC_ALGO_R005_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-False $unsafe.ChildOrdersCreated "Unsafe feed creates child orders." "EXEC_ALGO_R005_FAIL_CHILD_ORDERS_CREATED"
if ($direct.ScheduleShapeStatus -ne "ScheduleShapeBlockedDirectCross") { Fail "Direct cross shape not blocked." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING" }
Require-True $direct.RequiresNettingFirst "Direct cross missing netting-first." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
Require-True $direct.DirectCrossSignalOnlyHandlingPreserved "Direct cross signal-only weakened." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"

if ($usdjpyShape.PortfolioNormalizedSymbol -ne "JPYUSD" -or $usdjpyShape.ExecutionTradableSymbol -ne "USDJPY") { Fail "USDJPY shape symbols weakened." "EXEC_ALGO_R005_FAIL_USDJPY_CAVEAT_WEAKENED" }
Require-True $usdjpyShape.RequiresInversion "USDJPY shape inversion missing." "EXEC_ALGO_R005_FAIL_USDJPY_CAVEAT_WEAKENED"
Require-True $usdjpyShape.UsdJpyCaveatPreserved "USDJPY shape caveat missing." "EXEC_ALGO_R005_FAIL_USDJPY_CAVEAT_WEAKENED"
if ($usdjpyShape.SecurityID -ne "4004" -or $usdjpyShape.SecurityIDSource -ne "8") { Fail "USDJPY SecurityID caveat weakened." "EXEC_ALGO_R005_FAIL_USDJPY_CAVEAT_WEAKENED" }
if ($nonmajorShape.ScheduleShapeStatus -ne "ScheduleShapeMissingInstrumentConvention") { Fail "Nonmajor/missing convention status missing." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING" }
Require-True $nonmajorShape.ManualReviewRequired "Nonmajor missing convention does not require manual review." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
Require-False $nonmajorShape.ScheduleReadyForExecution "Nonmajor missing convention ready for execution." "EXEC_ALGO_R005_FAIL_EXECUTABLE_SCHEDULE_CREATED"

Require-True $wakettShape.WakettPatternBlocked "Wakett schedule shapes not blocked." "EXEC_ALGO_R005_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
if ($wakettShape.ScheduleShapeStatus -ne "ScheduleShapeBlockedWakettPattern") { Fail "Wakett shape status wrong." "EXEC_ALGO_R005_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED" }
Require-False $wakettShape.ScheduleShapeReadyForExecution "Wakett shape ready for execution." "EXEC_ALGO_R005_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
Require-False $wakettShape.ExecutableScheduleCreated "Wakett shape creates schedule." "EXEC_ALGO_R005_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-False $wakettShape.ChildSlicesCreated "Wakett shape creates slices." "EXEC_ALGO_R005_FAIL_CHILD_SLICES_CREATED"
Require-False $wakettShape.ChildOrdersCreated "Wakett shape creates child orders." "EXEC_ALGO_R005_FAIL_CHILD_ORDERS_CREATED"
Require-True $benchmarkShapes.benchmarkOnlyScheduleShapesCreated "Benchmark-only shapes missing." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
Require-True $benchmarkShapes.allBenchmarkOnly "Benchmark-only shapes not benchmark-only." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
Require-True $benchmarkShapes.allNonExecutable "Benchmark-only shapes executable." "EXEC_ALGO_R005_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-True $benchmarkShapes.allNotAnOrder "Benchmark-only shapes are orders." "EXEC_ALGO_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-True $benchmarkShapes.allNoBrokerRoute "Benchmark-only shapes have route." "EXEC_ALGO_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

foreach ($status in @("ScheduleShapeReadyDesignOnly", "ScheduleShapeBenchmarkOnly", "ScheduleShapeRequiresManualReview", "ScheduleShapeBlockedDirectCross", "ScheduleShapeBlockedWakettPattern", "ScheduleShapeMissingInstrumentConvention")) {
    if (@($statuses.statuses) -notcontains $status) { Fail "Missing schedule status $status." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING" }
}
foreach ($family in @("PureLimitUntilCloseDefault", "MechanicalMarketSlicesAroundClose", "AlwaysMarketAtClose", "AnyScheduleShapeMarkedExecutable", "AnyScheduleShapeWithChildOrders")) {
    if (@($blockedFamilies.blockedFamilies) -notcontains $family) { Fail "Blocked family missing $family." "EXEC_ALGO_R005_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED" }
}
Require-False $blockedFamilies.wakettPatternBlockWeakened "Wakett block weakened." "EXEC_ALGO_R005_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
Require-False $blockedFamilies.directCrossExclusionWeakened "Direct-cross exclusion weakened." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"

Require-True $overnight.MustEndFlat "Overnight preservation missing MustEndFlat." "EXEC_ALGO_R005_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-False $overnight.OvernightAllowed "Overnight preservation allows overnight." "EXEC_ALGO_R005_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
if ($overnight.ResidualPenaltyBucket -ne "NoOvernightCritical") { Fail "Overnight preservation residual penalty weakened." "EXEC_ALGO_R005_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED" }
Require-False $overnight.ExecutableScheduleCreated "Overnight preservation creates schedule." "EXEC_ALGO_R005_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-False $overnight.ChildOrdersCreated "Overnight preservation creates child orders." "EXEC_ALGO_R005_FAIL_CHILD_ORDERS_CREATED"
Require-True $firstBar.KnownAtTimestampMayBePreviousEvening "First-bar previous evening missing." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
Require-False $firstBar.PreSessionOrderAllowed "First-bar allows pre-session order." "EXEC_ALGO_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $firstBar.OvernightExposureBeforeSessionStartAllowed "First-bar allows overnight." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
Require-False $firstBar.ExecutableScheduleCreated "First-bar creates schedule." "EXEC_ALGO_R005_FAIL_EXECUTABLE_SCHEDULE_CREATED"
if ($cost.bestCaseMajorTargetUsdPerMillion -ne 5) { Fail "5 USD/million target missing." "EXEC_ALGO_R005_FAIL_5USD_PER_MILLION_UNIVERSALIZED" }
Require-True $cost.fiveUsdPerMillionBestCaseMajorOnly "5 USD/million not best-case major only." "EXEC_ALGO_R005_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-False $cost.fiveUsdPerMillionUniversalized "5 USD/million universalized." "EXEC_ALGO_R005_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-True $nonmajor.nonMajorEmScandiCnhRequireLiquidityCalibration "Nonmajor calibration missing." "EXEC_ALGO_R005_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
if ($normalization.executionUniverse -ne "USD-pair-only") { Fail "USD-pair-only execution weakened." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING" }
Require-True $normalization.mandatoryNettingBeforeExecution "Mandatory netting weakened." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
Require-False $normalization.normalizationWeakened "Normalization weakened." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
Require-False $directPreservation.directCrossExecutionAllowedByDefault "Direct-cross execution allowed." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
Require-False $directPreservation.guidanceWeakened "Direct-cross guidance weakened." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
Require-False $wakett.PureLimitUntilCloseDefaultAllowed "PureLimit default allowed." "EXEC_ALGO_R005_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
Require-False $wakett.MechanicalMarketSlicesAroundCloseAllowed "Mechanical slices allowed." "EXEC_ALGO_R005_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
Require-False $wakett.AlwaysMarketAtCloseAllowed "AlwaysMarketAtClose allowed." "EXEC_ALGO_R005_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
Require-False $wakett.wakettPatternBlockWeakened "Wakett block weakened." "EXEC_ALGO_R005_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"

Require-True $nonExecutable.allScheduleShapesNonExecutable "Non-executable schedule audit failed." "EXEC_ALGO_R005_FAIL_SCHEDULE_SHAPE_CONTRACT_MISSING"
Require-True $nonExecutable.schedulePhasesAreNotChildSlices "Schedule phases are child slices." "EXEC_ALGO_R005_FAIL_CHILD_SLICES_CREATED"
Require-False $nonExecutable.executableScheduleCreated "Executable schedule created." "EXEC_ALGO_R005_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-False $nonExecutable.childSlicesCreated "Child slices created." "EXEC_ALGO_R005_FAIL_CHILD_SLICES_CREATED"
Require-False $nonExecutable.childOrdersCreated "Child orders created." "EXEC_ALGO_R005_FAIL_CHILD_ORDERS_CREATED"
Require-False $schedule.executableScheduleCreated "Executable schedule created." "EXEC_ALGO_R005_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-False $schedule.scheduleThatCanBeSubmittedCreated "Submittable schedule created." "EXEC_ALGO_R005_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-False $schedule.timerPollingSchedulerServiceIntroduced "Scheduler/service introduced." "EXEC_ALGO_R005_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $slices.childSlicesCreated "Child slices created." "EXEC_ALGO_R005_FAIL_CHILD_SLICES_CREATED"
Require-False $slices.executableChildSlicesCreated "Executable child slices created." "EXEC_ALGO_R005_FAIL_CHILD_SLICES_CREATED"
Require-False $slices.orderDomainSliceObjectsCreated "Order-domain slice objects created." "EXEC_ALGO_R005_FAIL_CHILD_SLICES_CREATED"
Require-False $childOrders.childOrdersCreated "Child orders created." "EXEC_ALGO_R005_FAIL_CHILD_ORDERS_CREATED"
Require-False $childOrders.omsChildOrdersCreated "OMS child orders created." "EXEC_ALGO_R005_FAIL_CHILD_ORDERS_CREATED"
Require-False $noBacktest.newBacktestExecuted "New backtest executed." "EXEC_ALGO_R005_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $noBacktest.newSimulationExecuted "New simulation executed." "EXEC_ALGO_R005_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $noBacktest.newSimulationResultLinesCreated "New simulation result lines created." "EXEC_ALGO_R005_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $api.polygonApiCalled "Polygon API called." "EXEC_ALGO_R005_FAIL_API_CALL_DETECTED"
Require-False $api.lmaxCalled "LMAX called." "EXEC_ALGO_R005_FAIL_API_CALL_DETECTED"
Require-False $api.externalApiCalled "External API called." "EXEC_ALGO_R005_FAIL_API_CALL_DETECTED"
Require-False $runtime.brokerActivationDetected "Broker activation detected." "EXEC_ALGO_R005_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.socketOpened "Socket opened." "EXEC_ALGO_R005_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataRequestSent "MarketDataRequest sent." "EXEC_ALGO_R005_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.schedulerServiceTimerPollingBackgroundJobIntroduced "Scheduler/service introduced." "EXEC_ALGO_R005_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $runtime.automaticExecutionIntroduced "Automatic execution introduced." "EXEC_ALGO_R005_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $fill.realFillsCreated "Real fills created." "EXEC_ALGO_R005_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $fill.fillEntitiesCreated "Fill entities created." "EXEC_ALGO_R005_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $report.executionReportEntitiesCreated "Execution report entities created." "EXEC_ALGO_R005_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $report.brokerExecutionReportsCreated "Broker reports created." "EXEC_ALGO_R005_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $order.ordersCreated "Orders created." "EXEC_ALGO_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.executableOrdersCreated "Executable orders created." "EXEC_ALGO_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.omsChildOrdersCreated "OMS child orders created." "EXEC_ALGO_R005_FAIL_CHILD_ORDERS_CREATED"
Require-False $route.routesCreated "Routes created." "EXEC_ALGO_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $route.submissionsCreated "Submissions created." "EXEC_ALGO_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

Require-True $usdjpy.caveatPreserved "USDJPY caveat missing." "EXEC_ALGO_R005_FAIL_USDJPY_CAVEAT_WEAKENED"
Require-True $usdjpy.requiresInversion "USDJPY inversion missing." "EXEC_ALGO_R005_FAIL_USDJPY_CAVEAT_WEAKENED"
if ($usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8") { Fail "USDJPY caveat weakened." "EXEC_ALGO_R005_FAIL_USDJPY_CAVEAT_WEAKENED" }
Require-False $usdjpy.weakened "USDJPY weakened." "EXEC_ALGO_R005_FAIL_USDJPY_CAVEAT_WEAKENED"
Require-False $usdjpy.audusdMisclassifiedFailed "AUDUSD misclassified failed." "EXEC_ALGO_R005_FAIL_AUDUSD_MISCLASSIFIED"
Require-True $lmax.referenceOnly "LMAX reference weakened." "EXEC_ALGO_R005_FAIL_API_CALL_DETECTED"
Require-False $lmax.lmaxCalledInR005 "LMAX called in R005." "EXEC_ALGO_R005_FAIL_API_CALL_DETECTED"
if ($lmax.audusdStatus -notmatch "not failed") { Fail "AUDUSD incorrectly marked failed." "EXEC_ALGO_R005_FAIL_AUDUSD_MISCLASSIFIED" }

Require-False $noExternal.polygonApiCalled "No-external audit shows Polygon call." "EXEC_ALGO_R005_FAIL_API_CALL_DETECTED"
Require-False $noExternal.lmaxCalled "No-external audit shows LMAX call." "EXEC_ALGO_R005_FAIL_API_CALL_DETECTED"
Require-False $noExternal.externalApiCalled "No-external audit shows external API call." "EXEC_ALGO_R005_FAIL_API_CALL_DETECTED"
Require-False $noExternal.newBacktestExecuted "No-external audit shows backtest." "EXEC_ALGO_R005_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $noExternal.newSimulationExecuted "No-external audit shows simulation." "EXEC_ALGO_R005_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $noExternal.newSimulationResultLinesCreated "No-external audit shows simulation lines." "EXEC_ALGO_R005_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $noExternal.executableScheduleCreated "No-external audit shows schedule." "EXEC_ALGO_R005_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-False $noExternal.childSlicesCreated "No-external audit shows child slices." "EXEC_ALGO_R005_FAIL_CHILD_SLICES_CREATED"
Require-False $noExternal.childOrdersCreated "No-external audit shows child orders." "EXEC_ALGO_R005_FAIL_CHILD_ORDERS_CREATED"
Require-False $noExternal.ordersFillsReportsRoutesSubmissionsCreated "No-external audit shows order-domain output." "EXEC_ALGO_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.liveBrokerProductionTradingStateMutated "No-external audit shows state mutation." "EXEC_ALGO_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.paperLedgerStateCommitted "No-external audit shows paper ledger commit." "EXEC_ALGO_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $forbidden.forbiddenActionsDetected "Forbidden action detected." "EXEC_ALGO_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED"

if ($evidence.dotnetBuildNoRestore -ne "PASS") { Fail "dotnet build evidence is not PASS." "EXEC_ALGO_R005_FAIL_BUILD_OR_TESTS" }
if ($evidence.focusedTests -notmatch "^PASS") { Fail "Focused R005 test evidence is not PASS." "EXEC_ALGO_R005_FAIL_BUILD_OR_TESTS" }
if ($evidence.unitTests -notmatch "^PASS") { Fail "Unit test evidence is not PASS." "EXEC_ALGO_R005_FAIL_BUILD_OR_TESTS" }

Write-Host "EXEC_ALGO_R005_PASS_SESSION_AWARE_SCHEDULE_SHAPE_CONTRACT_READY_NO_EXTERNAL"
Write-Host "EXEC_ALGO_R005_PASS_BAR_ROLE_SCHEDULE_SHAPES_READY_NO_EXTERNAL"
Write-Host "EXEC_ALGO_R005_PASS_NO_EXECUTABLE_SCHEDULE_NO_CHILD_SLICES_GATE_READY_NO_EXTERNAL"
Write-Host "EXEC_ALGO_R005_PASS_WAKETT_AND_DIRECT_CROSS_BLOCKS_READY_NO_EXTERNAL"
exit 0
