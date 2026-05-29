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
    "phase-exec-algo-r004-summary.md",
    "phase-exec-algo-r004-r003-parameter-contract-reference.json",
    "phase-exec-algo-r004-parameter-application-preview-contract.json",
    "phase-exec-algo-r004-paper-execution-plan-line-input-shape.json",
    "phase-exec-algo-r004-opening-build-application-preview.json",
    "phase-exec-algo-r004-intraday-rebalance-application-preview.json",
    "phase-exec-algo-r004-closing-flatten-application-preview.json",
    "phase-exec-algo-r004-closing-flatten-unsafe-feed-preview.json",
    "phase-exec-algo-r004-direct-cross-blocked-preview.json",
    "phase-exec-algo-r004-usdjpy-inverted-preview.json",
    "phase-exec-algo-r004-nonmajor-missing-convention-preview.json",
    "phase-exec-algo-r004-wakett-pattern-blocked-preview.json",
    "phase-exec-algo-r004-applied-parameter-preview-fields.json",
    "phase-exec-algo-r004-policy-fallback-application.json",
    "phase-exec-algo-r004-manual-review-trigger-application.json",
    "phase-exec-algo-r004-feed-quality-requirement-application.json",
    "phase-exec-algo-r004-close-benchmark-requirement-application.json",
    "phase-exec-algo-r004-no-overnight-flatten-application.json",
    "phase-exec-algo-r004-first-bar-previous-evening-application.json",
    "phase-exec-algo-r004-cost-guidance-preservation.json",
    "phase-exec-algo-r004-nonmajor-calibration-preservation.json",
    "phase-exec-algo-r004-usd-pair-normalization-preservation.json",
    "phase-exec-algo-r004-direct-cross-exclusion-preservation.json",
    "phase-exec-algo-r004-wakett-pattern-block-preservation.json",
    "phase-exec-algo-r004-non-executable-preview-audit.json",
    "phase-exec-algo-r004-no-executable-schedule-audit.json",
    "phase-exec-algo-r004-no-child-slices-audit.json",
    "phase-exec-algo-r004-no-new-backtest-audit.json",
    "phase-exec-algo-r004-no-polygon-api-call-audit.json",
    "phase-exec-algo-r004-no-lmax-call-audit.json",
    "phase-exec-algo-r004-no-external-api-call-audit.json",
    "phase-exec-algo-r004-no-broker-marketdata-runtime-audit.json",
    "phase-exec-algo-r004-no-real-fill-audit.json",
    "phase-exec-algo-r004-no-execution-report-audit.json",
    "phase-exec-algo-r004-no-order-created-audit.json",
    "phase-exec-algo-r004-no-route-no-submission-audit.json",
    "phase-exec-algo-r004-usdjpy-caveat-preservation.json",
    "phase-exec-algo-r004-lmax-readonly-baseline-reference.json",
    "phase-exec-algo-r004-no-external-audit.json",
    "phase-exec-algo-r004-forbidden-actions-audit.json",
    "phase-exec-algo-r004-next-phase-recommendation.json",
    "phase-exec-algo-r004-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsDir $artifact))) {
        Fail "Required R004 artifact is missing: $artifact" "EXEC_ALGO_R004_FAIL_BUILD_OR_TESTS"
    }
}

$reference = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-r003-parameter-contract-reference.json") "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
$contract = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-parameter-application-preview-contract.json") "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
$input = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-paper-execution-plan-line-input-shape.json") "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
$opening = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-opening-build-application-preview.json") "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
$intraday = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-intraday-rebalance-application-preview.json") "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
$closing = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-closing-flatten-application-preview.json") "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
$unsafe = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-closing-flatten-unsafe-feed-preview.json") "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
$direct = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-direct-cross-blocked-preview.json") "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
$usdjpyPreview = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-usdjpy-inverted-preview.json") "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
$nonmajorPreview = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-nonmajor-missing-convention-preview.json") "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
$wakettPreview = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-wakett-pattern-blocked-preview.json") "EXEC_ALGO_R004_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
$fields = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-applied-parameter-preview-fields.json") "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
$fallback = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-policy-fallback-application.json") "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
$manual = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-manual-review-trigger-application.json") "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
$feed = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-feed-quality-requirement-application.json") "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
$benchmark = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-close-benchmark-requirement-application.json") "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
$overnight = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-no-overnight-flatten-application.json") "EXEC_ALGO_R004_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
$firstBar = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-first-bar-previous-evening-application.json") "EXEC_ALGO_R004_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-cost-guidance-preservation.json") "EXEC_ALGO_R004_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$nonmajor = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-nonmajor-calibration-preservation.json") "EXEC_ALGO_R004_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$normalization = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-usd-pair-normalization-preservation.json") "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
$directPreservation = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-direct-cross-exclusion-preservation.json") "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
$wakett = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-wakett-pattern-block-preservation.json") "EXEC_ALGO_R004_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
$nonExecutable = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-non-executable-preview-audit.json") "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
$schedule = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-no-executable-schedule-audit.json") "EXEC_ALGO_R004_FAIL_EXECUTABLE_SCHEDULE_CREATED"
$slices = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-no-child-slices-audit.json") "EXEC_ALGO_R004_FAIL_CHILD_SLICES_CREATED"
$noBacktest = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-no-new-backtest-audit.json") "EXEC_ALGO_R004_FAIL_NEW_BACKTEST_EXECUTED"
$api = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-no-external-api-call-audit.json") "EXEC_ALGO_R004_FAIL_API_CALL_DETECTED"
$runtime = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-no-broker-marketdata-runtime-audit.json") "EXEC_ALGO_R004_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
$fill = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-no-real-fill-audit.json") "EXEC_ALGO_R004_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$report = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-no-execution-report-audit.json") "EXEC_ALGO_R004_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$order = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-no-order-created-audit.json") "EXEC_ALGO_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$route = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-no-route-no-submission-audit.json") "EXEC_ALGO_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-usdjpy-caveat-preservation.json") "EXEC_ALGO_R004_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-lmax-readonly-baseline-reference.json") "EXEC_ALGO_R004_FAIL_API_CALL_DETECTED"
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-no-external-audit.json") "EXEC_ALGO_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-forbidden-actions-audit.json") "EXEC_ALGO_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r004-build-test-validator-evidence.json") "EXEC_ALGO_R004_FAIL_BUILD_OR_TESTS"

if ($reference.sourceParameterPhase -ne "EXEC-ALGO-R003") { Fail "R003 parameter reference missing." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING" }
Require-True $reference.r003ParameterContractReferenced "R003 parameter contract not referenced." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
Require-True $reference.contractOnlyPreview "R004 is not contract-only preview." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
Require-True $reference.noSimulation "R004 reference does not confirm no simulation." "EXEC_ALGO_R004_FAIL_NEW_BACKTEST_EXECUTED"
Require-True $reference.noBacktest "R004 reference does not confirm no backtest." "EXEC_ALGO_R004_FAIL_NEW_BACKTEST_EXECUTED"

Require-True $contract.parameterApplicationPreviewContractCreated "Preview contract missing." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
Require-True $contract.allPreviewsDesignOnly "Previews not design-only." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
Require-True $contract.allPreviewsPaperOnly "Previews not paper-only." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
Require-True $contract.allPreviewsNonExecutable "Previews executable." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
Require-False $contract.createsExecutableSchedule "Preview contract creates executable schedule." "EXEC_ALGO_R004_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-False $contract.createsChildSlices "Preview contract creates child slices." "EXEC_ALGO_R004_FAIL_CHILD_SLICES_CREATED"
Require-False $contract.createsOrders "Preview contract creates orders." "EXEC_ALGO_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-True $input.inputShapeCreated "Input shape missing." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
Require-True $input.previewOnly "Input shape not preview-only." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
Require-False $input.createsOrder "Input shape creates order." "EXEC_ALGO_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

Require-True $opening.PreviousEveningPlanningAllowed "Opening previous-evening planning missing." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
Require-True $opening.KnownAtTimestampSeparateFromEarliestExecutionTimestamp "Opening KnownAt/Earliest separation missing." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
Require-False $opening.OvernightExposureBeforeSessionStartAllowed "Opening allows overnight exposure." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
Require-False $opening.PreSessionOrderAllowed "Opening creates pre-session order." "EXEC_ALGO_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
if ($opening.PreviewStatus -ne "ParameterPreviewReady") { Fail "Opening preview not ready." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING" }

Require-True $intraday.NormalCloseSeekingBehaviorPreserved "Intraday normal close-seeking weakened." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
Require-True $intraday.ControlledResidualCrossOnlyWhenOpportunityCostExceedsCrossingCost "Intraday controlled cross justification missing." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
if ($intraday.PreviewStatus -ne "ParameterPreviewReady") { Fail "Intraday preview not ready." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING" }

Require-True $closing.MustEndFlat "Closing preview missing MustEndFlat." "EXEC_ALGO_R004_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-False $closing.OvernightAllowed "Closing preview allows overnight." "EXEC_ALGO_R004_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
if ($closing.ResidualPenaltyBucket -ne "NoOvernightCritical") { Fail "Closing residual penalty weakened." "EXEC_ALGO_R004_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED" }
Require-False $closing.BlindMarketScheduleCreated "Closing created blind market schedule." "EXEC_ALGO_R004_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-False $closing.FiveMarketSlicesDefaultAllowed "Closing allows five-market-slice default." "EXEC_ALGO_R004_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-False $closing.AlwaysMarketAtCloseDefaultAllowed "Closing allows AlwaysMarketAtClose." "EXEC_ALGO_R004_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-False $closing.PureLimitUntilCloseDefaultAllowed "Closing allows PureLimit default." "EXEC_ALGO_R004_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"

foreach ($preview in @($opening, $intraday, $closing, $unsafe, $direct, $usdjpyPreview, $nonmajorPreview, $wakettPreview)) {
    Require-True $preview.IsDesignOnly "Preview not design-only." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
    Require-True $preview.IsPaperOnly "Preview not paper-only." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
    Require-False $preview.IsExecutable "Preview executable." "EXEC_ALGO_R004_FAIL_EXECUTABLE_SCHEDULE_CREATED"
    Require-False $preview.IsOrder "Preview is order." "EXEC_ALGO_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    Require-False $preview.IsSubmitted "Preview submitted." "EXEC_ALGO_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    Require-False $preview.HasBrokerRoute "Preview has broker route." "EXEC_ALGO_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
}

if ($unsafe.PreviewStatus -ne "ParameterPreviewRequiresManualReview") { Fail "Unsafe feed preview does not require manual review." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING" }
Require-False $unsafe.BlindExecutionCreated "Unsafe feed creates blind execution." "EXEC_ALGO_R004_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-False $unsafe.ExecutableScheduleCreated "Unsafe feed creates executable schedule." "EXEC_ALGO_R004_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-False $unsafe.ChildSlicesCreated "Unsafe feed creates child slices." "EXEC_ALGO_R004_FAIL_CHILD_SLICES_CREATED"

Require-True $direct.RequiresNettingFirst "Direct-cross preview missing netting-first." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
Require-True $direct.DirectCrossExecutionDisabled "Direct-cross preview not disabled." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
if ($direct.PreviewStatus -ne "ParameterPreviewBlockedDirectCross") { Fail "Direct-cross preview not blocked." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING" }

if ($usdjpyPreview.PortfolioNormalizedSymbol -ne "JPYUSD" -or $usdjpyPreview.ExecutionTradableSymbol -ne "USDJPY") {
    Fail "USDJPY inverted preview weakened." "EXEC_ALGO_R004_FAIL_USDJPY_CAVEAT_WEAKENED"
}
Require-True $usdjpyPreview.RequiresInversion "USDJPY preview missing inversion." "EXEC_ALGO_R004_FAIL_USDJPY_CAVEAT_WEAKENED"
Require-True $usdjpyPreview.UsdJpyCaveatPreserved "USDJPY preview caveat missing." "EXEC_ALGO_R004_FAIL_USDJPY_CAVEAT_WEAKENED"
if ($usdjpyPreview.SecurityID -ne "4004" -or $usdjpyPreview.SecurityIDSource -ne "8") { Fail "USDJPY preview SecurityID caveat weakened." "EXEC_ALGO_R004_FAIL_USDJPY_CAVEAT_WEAKENED" }

if ($nonmajorPreview.PreviewStatus -ne "ParameterPreviewMissingInstrumentConvention") { Fail "Missing convention preview wrong status." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING" }
Require-False $nonmajorPreview.ExecutablePreviewCreated "Missing convention creates executable preview." "EXEC_ALGO_R004_FAIL_EXECUTABLE_SCHEDULE_CREATED"

Require-True $wakettPreview.WakettPatternBlocked "Wakett blocked preview missing." "EXEC_ALGO_R004_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
Require-False $wakettPreview.PureLimitUntilCloseDefaultAllowed "Wakett preview allows PureLimit default." "EXEC_ALGO_R004_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
Require-False $wakettPreview.MechanicalMarketSlicesAroundCloseAllowed "Wakett preview allows mechanical slices." "EXEC_ALGO_R004_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
Require-False $wakettPreview.AlwaysMarketAtCloseAllowed "Wakett preview allows AlwaysMarketAtClose." "EXEC_ALGO_R004_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
Require-False $wakettPreview.ExecutableScheduleCreated "Wakett preview creates executable schedule." "EXEC_ALGO_R004_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-False $wakettPreview.ChildSlicesCreated "Wakett preview creates child slices." "EXEC_ALGO_R004_FAIL_CHILD_SLICES_CREATED"

Require-True $fields.appliedParameterPreviewFieldsCreated "Applied parameter fields missing." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
Require-False $fields.createsExecutableConfig "Applied fields create executable config." "EXEC_ALGO_R004_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-True $fallback.fallbackApplicationCreated "Fallback application missing." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
Require-False $fallback.createsOrder "Fallback creates order." "EXEC_ALGO_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-True $manual.manualReviewTriggerApplicationCreated "Manual review application missing." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
Require-True $manual.manualReviewIsNotAutomaticExecution "Manual review treated as automatic execution." "EXEC_ALGO_R004_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $manual.manualReviewCreatesExecutableSchedule "Manual review creates schedule." "EXEC_ALGO_R004_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-False $feed.unsafeFeedCreatesBlindExecution "Unsafe feed creates blind execution." "EXEC_ALGO_R004_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-False $benchmark.missingCloseBenchmarkCreatesBlindExecution "Missing benchmark creates blind execution." "EXEC_ALGO_R004_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-True $overnight.MustEndFlat "No-overnight application missing MustEndFlat." "EXEC_ALGO_R004_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-False $overnight.OvernightAllowed "No-overnight application allows overnight." "EXEC_ALGO_R004_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-False $overnight.ExecutableScheduleCreated "No-overnight application creates schedule." "EXEC_ALGO_R004_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-False $overnight.ChildSlicesCreated "No-overnight application creates child slices." "EXEC_ALGO_R004_FAIL_CHILD_SLICES_CREATED"
Require-True $firstBar.PreviousEveningPlanningAllowed "First-bar previous evening planning missing." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
Require-True $firstBar.KnownAtTimestampSeparateFromEarliestExecutionTimestamp "First-bar KnownAt/Earliest separation missing." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
Require-False $firstBar.OvernightExposureBeforeSessionStartAllowed "First-bar allows overnight exposure." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
Require-False $firstBar.PreSessionOrderAllowed "First-bar creates pre-session order." "EXEC_ALGO_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

if ($cost.bestCaseMajorTargetUsdPerMillion -ne 5) { Fail "5 USD/million target missing." "EXEC_ALGO_R004_FAIL_5USD_PER_MILLION_UNIVERSALIZED" }
Require-True $cost.fiveUsdPerMillionBestCaseMajorOnly "5 USD/million not best-case major only." "EXEC_ALGO_R004_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-False $cost.fiveUsdPerMillionUniversalized "5 USD/million universalized." "EXEC_ALGO_R004_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-True $nonmajor.nonMajorEmScandiCnhRequireLiquidityCalibration "Non-major calibration missing." "EXEC_ALGO_R004_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-True $nonmajor.missingConventionPreviewRequiresManualReview "Missing convention does not require manual review." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
if ($normalization.executionUniverse -ne "USD-pair-only") { Fail "USD-pair execution weakened." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING" }
Require-True $normalization.mandatoryNettingBeforeExecution "Mandatory netting weakened." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
Require-False $normalization.normalizationWeakened "USD-pair normalization weakened." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
Require-False $directPreservation.directCrossExecutionAllowedByDefault "Direct-cross execution allowed by default." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
Require-True $directPreservation.directCrossSignalOnlyHandlingPreserved "Direct-cross signal-only handling weakened." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
Require-False $directPreservation.guidanceWeakened "Direct-cross guidance weakened." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
Require-False $wakett.PureLimitUntilCloseDefaultAllowed "PureLimit default allowed." "EXEC_ALGO_R004_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
Require-False $wakett.MechanicalMarketSlicesAroundCloseAllowed "Mechanical slices allowed." "EXEC_ALGO_R004_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
Require-False $wakett.AlwaysMarketAtCloseAllowed "AlwaysMarketAtClose allowed." "EXEC_ALGO_R004_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
Require-False $wakett.wakettPatternBlockWeakened "Wakett pattern block weakened." "EXEC_ALGO_R004_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"

Require-True $nonExecutable.allPreviewsNonExecutable "Non-executable audit failed." "EXEC_ALGO_R004_FAIL_PARAMETER_APPLICATION_PREVIEW_MISSING"
Require-False $nonExecutable.executableAlgoConfigurationCreated "Executable algo configuration created." "EXEC_ALGO_R004_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-False $schedule.executableScheduleCreated "Executable schedule created." "EXEC_ALGO_R004_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-False $schedule.scheduleThatCanBeSubmittedCreated "Submittable schedule created." "EXEC_ALGO_R004_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-False $schedule.timerPollingSchedulerServiceIntroduced "Scheduler/service introduced." "EXEC_ALGO_R004_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $slices.childSlicesCreated "Child slices created." "EXEC_ALGO_R004_FAIL_CHILD_SLICES_CREATED"
Require-False $slices.childOrdersCreated "Child orders created." "EXEC_ALGO_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $slices.slicesThatCanBeSubmittedCreated "Submittable slices created." "EXEC_ALGO_R004_FAIL_CHILD_SLICES_CREATED"
Require-False $noBacktest.newBacktestExecuted "New backtest executed." "EXEC_ALGO_R004_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $noBacktest.newSimulationExecuted "New simulation executed." "EXEC_ALGO_R004_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $noBacktest.newSimulationResultLinesCreated "New simulation result lines created." "EXEC_ALGO_R004_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $api.polygonApiCalled "Polygon API called." "EXEC_ALGO_R004_FAIL_API_CALL_DETECTED"
Require-False $api.lmaxCalled "LMAX called." "EXEC_ALGO_R004_FAIL_API_CALL_DETECTED"
Require-False $api.externalApiCalled "External API called." "EXEC_ALGO_R004_FAIL_API_CALL_DETECTED"
Require-False $runtime.brokerActivationDetected "Broker activation detected." "EXEC_ALGO_R004_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.socketOpened "Socket opened." "EXEC_ALGO_R004_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.tlsOpened "TLS opened." "EXEC_ALGO_R004_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.fixOpened "FIX opened." "EXEC_ALGO_R004_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataRequestSent "MarketDataRequest sent." "EXEC_ALGO_R004_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataResponseRead "MarketDataResponse read." "EXEC_ALGO_R004_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.schedulerServiceTimerPollingBackgroundJobIntroduced "Scheduler/service introduced." "EXEC_ALGO_R004_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $runtime.automaticExecutionIntroduced "Automatic execution introduced." "EXEC_ALGO_R004_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $fill.realFillsCreated "Real fills created." "EXEC_ALGO_R004_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $fill.fillEntitiesCreated "Fill entities created." "EXEC_ALGO_R004_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $report.executionReportEntitiesCreated "Execution report entities created." "EXEC_ALGO_R004_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $report.brokerExecutionReportsCreated "Broker reports created." "EXEC_ALGO_R004_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $order.ordersCreated "Orders created." "EXEC_ALGO_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.executableOrdersCreated "Executable orders created." "EXEC_ALGO_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.omsChildOrdersCreated "OMS child orders created." "EXEC_ALGO_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $route.routesCreated "Routes created." "EXEC_ALGO_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $route.submissionsCreated "Submissions created." "EXEC_ALGO_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

Require-True $usdjpy.caveatPreserved "USDJPY caveat missing." "EXEC_ALGO_R004_FAIL_USDJPY_CAVEAT_WEAKENED"
Require-True $usdjpy.requiresInversion "USDJPY inversion missing." "EXEC_ALGO_R004_FAIL_USDJPY_CAVEAT_WEAKENED"
if ($usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8") { Fail "USDJPY caveat weakened." "EXEC_ALGO_R004_FAIL_USDJPY_CAVEAT_WEAKENED" }
Require-False $usdjpy.weakened "USDJPY weakened." "EXEC_ALGO_R004_FAIL_USDJPY_CAVEAT_WEAKENED"
Require-False $usdjpy.audusdMisclassifiedFailed "AUDUSD misclassified failed." "EXEC_ALGO_R004_FAIL_AUDUSD_MISCLASSIFIED"
Require-True $lmax.referenceOnly "LMAX reference weakened." "EXEC_ALGO_R004_FAIL_API_CALL_DETECTED"
Require-False $lmax.lmaxCalledInR004 "LMAX called in R004." "EXEC_ALGO_R004_FAIL_API_CALL_DETECTED"
if ($lmax.audusdStatus -notmatch "not failed") { Fail "AUDUSD incorrectly marked failed." "EXEC_ALGO_R004_FAIL_AUDUSD_MISCLASSIFIED" }

Require-False $noExternal.polygonApiCalled "No-external audit shows Polygon call." "EXEC_ALGO_R004_FAIL_API_CALL_DETECTED"
Require-False $noExternal.lmaxCalled "No-external audit shows LMAX call." "EXEC_ALGO_R004_FAIL_API_CALL_DETECTED"
Require-False $noExternal.externalApiCalled "No-external audit shows external API call." "EXEC_ALGO_R004_FAIL_API_CALL_DETECTED"
Require-False $noExternal.newBacktestExecuted "No-external audit shows backtest." "EXEC_ALGO_R004_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $noExternal.newSimulationExecuted "No-external audit shows simulation." "EXEC_ALGO_R004_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $noExternal.newSimulationResultLinesCreated "No-external audit shows simulation lines." "EXEC_ALGO_R004_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $noExternal.executableScheduleCreated "No-external audit shows schedule." "EXEC_ALGO_R004_FAIL_EXECUTABLE_SCHEDULE_CREATED"
Require-False $noExternal.childSlicesCreated "No-external audit shows child slices." "EXEC_ALGO_R004_FAIL_CHILD_SLICES_CREATED"
Require-False $noExternal.ordersFillsReportsRoutesSubmissionsCreated "No-external audit shows order-domain output." "EXEC_ALGO_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.liveBrokerProductionTradingStateMutated "No-external audit shows state mutation." "EXEC_ALGO_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.paperLedgerStateCommitted "No-external audit shows paper ledger commit." "EXEC_ALGO_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $forbidden.forbiddenActionsDetected "Forbidden action detected." "EXEC_ALGO_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED"

if ($evidence.dotnetBuildNoRestore -ne "PASS") { Fail "dotnet build evidence is not PASS." "EXEC_ALGO_R004_FAIL_BUILD_OR_TESTS" }
if ($evidence.focusedTests -notmatch "^PASS") { Fail "Focused R004 test evidence is not PASS." "EXEC_ALGO_R004_FAIL_BUILD_OR_TESTS" }
if ($evidence.unitTests -notmatch "^PASS") { Fail "Unit test evidence is not PASS." "EXEC_ALGO_R004_FAIL_BUILD_OR_TESTS" }

Write-Host "EXEC_ALGO_R004_PASS_PARAMETER_APPLICATION_PREVIEW_READY_NO_EXTERNAL"
Write-Host "EXEC_ALGO_R004_PASS_BAR_ROLE_APPLICATION_PREVIEWS_READY_NO_EXTERNAL"
Write-Host "EXEC_ALGO_R004_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
Write-Host "EXEC_ALGO_R004_PASS_POLICY_BLOCKS_AND_MANUAL_REVIEW_PREVIEW_READY_NO_EXTERNAL"
exit 0
