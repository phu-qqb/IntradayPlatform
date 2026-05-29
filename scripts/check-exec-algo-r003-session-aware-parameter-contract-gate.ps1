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
    "phase-exec-algo-r003-summary.md",
    "phase-exec-algo-r003-r016-recommendation-reference.json",
    "phase-exec-algo-r003-session-aware-parameter-contract.json",
    "phase-exec-algo-r003-parameter-set-versioning.json",
    "phase-exec-algo-r003-opening-build-parameter-set.json",
    "phase-exec-algo-r003-intraday-rebalance-parameter-set.json",
    "phase-exec-algo-r003-closing-flatten-parameter-set.json",
    "phase-exec-algo-r003-parameter-validation-rules.json",
    "phase-exec-algo-r003-policy-recommendations-by-bar-role.json",
    "phase-exec-algo-r003-policy-fallback-ladder-by-bar-role.json",
    "phase-exec-algo-r003-blocked-policy-families.json",
    "phase-exec-algo-r003-manual-review-triggers.json",
    "phase-exec-algo-r003-feed-quality-requirements-by-bar-role.json",
    "phase-exec-algo-r003-close-benchmark-requirements-by-bar-role.json",
    "phase-exec-algo-r003-no-overnight-flatten-parameter-contract.json",
    "phase-exec-algo-r003-first-bar-previous-evening-planning-contract.json",
    "phase-exec-algo-r003-cost-guidance-by-bar-role.json",
    "phase-exec-algo-r003-nonmajor-calibration-preservation.json",
    "phase-exec-algo-r003-usd-pair-normalization-preservation.json",
    "phase-exec-algo-r003-direct-cross-exclusion-preservation.json",
    "phase-exec-algo-r003-wakett-pattern-block-preservation.json",
    "phase-exec-algo-r003-non-executable-parameter-contract-audit.json",
    "phase-exec-algo-r003-no-new-backtest-audit.json",
    "phase-exec-algo-r003-no-new-simulation-result-lines-audit.json",
    "phase-exec-algo-r003-no-polygon-api-call-audit.json",
    "phase-exec-algo-r003-no-lmax-call-audit.json",
    "phase-exec-algo-r003-no-external-api-call-audit.json",
    "phase-exec-algo-r003-no-broker-marketdata-runtime-audit.json",
    "phase-exec-algo-r003-no-real-fill-audit.json",
    "phase-exec-algo-r003-no-execution-report-audit.json",
    "phase-exec-algo-r003-no-order-created-audit.json",
    "phase-exec-algo-r003-no-route-no-submission-audit.json",
    "phase-exec-algo-r003-usdjpy-caveat-preservation.json",
    "phase-exec-algo-r003-lmax-readonly-baseline-reference.json",
    "phase-exec-algo-r003-no-external-audit.json",
    "phase-exec-algo-r003-forbidden-actions-audit.json",
    "phase-exec-algo-r003-next-phase-recommendation.json",
    "phase-exec-algo-r003-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsDir $artifact))) {
        Fail "Required R003 artifact is missing: $artifact" "EXEC_ALGO_R003_FAIL_BUILD_OR_TESTS"
    }
}

$reference = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-r016-recommendation-reference.json") "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"
$contract = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-session-aware-parameter-contract.json") "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"
$versioning = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-parameter-set-versioning.json") "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"
$opening = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-opening-build-parameter-set.json") "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"
$intraday = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-intraday-rebalance-parameter-set.json") "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"
$closing = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-closing-flatten-parameter-set.json") "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"
$rules = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-parameter-validation-rules.json") "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"
$policy = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-policy-recommendations-by-bar-role.json") "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"
$ladder = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-policy-fallback-ladder-by-bar-role.json") "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"
$blocked = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-blocked-policy-families.json") "EXEC_ALGO_R003_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
$manual = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-manual-review-triggers.json") "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"
$feed = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-feed-quality-requirements-by-bar-role.json") "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"
$benchmark = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-close-benchmark-requirements-by-bar-role.json") "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"
$flatten = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-no-overnight-flatten-parameter-contract.json") "EXEC_ALGO_R003_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
$firstBar = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-first-bar-previous-evening-planning-contract.json") "EXEC_ALGO_R003_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-cost-guidance-by-bar-role.json") "EXEC_ALGO_R003_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$nonMajor = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-nonmajor-calibration-preservation.json") "EXEC_ALGO_R003_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$normalization = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-usd-pair-normalization-preservation.json") "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"
$directCross = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-direct-cross-exclusion-preservation.json") "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"
$wakett = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-wakett-pattern-block-preservation.json") "EXEC_ALGO_R003_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
$nonExecutable = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-non-executable-parameter-contract-audit.json") "EXEC_ALGO_R003_FAIL_PARAMETER_SET_EXECUTABLE"
$noBacktest = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-no-new-backtest-audit.json") "EXEC_ALGO_R003_FAIL_NEW_BACKTEST_EXECUTED"
$noLines = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-no-new-simulation-result-lines-audit.json") "EXEC_ALGO_R003_FAIL_NEW_SIMULATION_RESULTS_CREATED"
$api = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-no-external-api-call-audit.json") "EXEC_ALGO_R003_FAIL_API_CALL_DETECTED"
$runtime = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-no-broker-marketdata-runtime-audit.json") "EXEC_ALGO_R003_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
$fill = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-no-real-fill-audit.json") "EXEC_ALGO_R003_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$report = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-no-execution-report-audit.json") "EXEC_ALGO_R003_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$order = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-no-order-created-audit.json") "EXEC_ALGO_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$route = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-no-route-no-submission-audit.json") "EXEC_ALGO_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-usdjpy-caveat-preservation.json") "EXEC_ALGO_R003_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-lmax-readonly-baseline-reference.json") "EXEC_ALGO_R003_FAIL_API_CALL_DETECTED"
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-no-external-audit.json") "EXEC_ALGO_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-forbidden-actions-audit.json") "EXEC_ALGO_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-algo-r003-build-test-validator-evidence.json") "EXEC_ALGO_R003_FAIL_BUILD_OR_TESTS"

if ($reference.sourceRecommendationPhase -ne "EXEC-SIM-R016") { Fail "R016 reference missing." "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING" }
Require-True $reference.r016RecommendationsReused "R016 recommendations not reused." "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"
Require-False $reference.r016MetricsInventedInR003 "R003 invented R016 metrics." "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"
Require-True $reference.noNewBacktest "R016 reference does not confirm no backtest." "EXEC_ALGO_R003_FAIL_NEW_BACKTEST_EXECUTED"

Require-True $contract.contractCreated "Parameter contract missing." "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"
if ($contract.parameterContractStatus -ne "ParameterContractReady") { Fail "Parameter contract status missing." "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING" }
if ($contract.appliesToExecutionUniverse -ne "USDPairOnly") { Fail "USD-pair-only execution weakened." "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING" }
foreach ($role in @("OpeningBuild", "IntradayRebalance", "ClosingFlatten")) {
    if (@($contract.barRoles) -notcontains $role) { Fail "Missing bar role $role." "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING" }
}
foreach ($category in @("ResidualCrossThreshold", "ManualReviewThreshold", "RequiredFeedQualityBucket", "RequiredCloseBenchmarkStatus", "CostBucketTarget", "LiquidityCalibrationStatus")) {
    if (@($contract.parameterCategories) -notcontains $category) { Fail "Missing parameter category $category." "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING" }
}
Require-True $contract.isDesignOnly "Contract not design-only." "EXEC_ALGO_R003_FAIL_PARAMETER_SET_EXECUTABLE"
Require-True $contract.isPaperOnly "Contract not paper-only." "EXEC_ALGO_R003_FAIL_PARAMETER_SET_EXECUTABLE"
Require-False $contract.isExecutable "Contract is executable." "EXEC_ALGO_R003_FAIL_PARAMETER_SET_EXECUTABLE"
Require-False $contract.createsExecutableConfiguration "Executable configuration created." "EXEC_ALGO_R003_FAIL_PARAMETER_SET_EXECUTABLE"
Require-False $contract.createsExecutionSchedule "Execution schedule created." "EXEC_ALGO_R003_FAIL_PARAMETER_SET_EXECUTABLE"
Require-False $contract.createsOrders "Orders created by contract." "EXEC_ALGO_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

Require-True $versioning.versioningCreated "Parameter versioning missing." "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"
if ($versioning.parameterSetVersion -ne "1.0.0-design-only") { Fail "Parameter version wrong." "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING" }
Require-False $versioning.isExecutable "Versioned set executable." "EXEC_ALGO_R003_FAIL_PARAMETER_SET_EXECUTABLE"

foreach ($set in @($opening, $intraday, $closing)) {
    Require-True $set.IsDesignOnly "Parameter set not design-only." "EXEC_ALGO_R003_FAIL_PARAMETER_SET_EXECUTABLE"
    Require-True $set.IsPaperOnly "Parameter set not paper-only." "EXEC_ALGO_R003_FAIL_PARAMETER_SET_EXECUTABLE"
    Require-False $set.IsExecutable "Parameter set executable." "EXEC_ALGO_R003_FAIL_PARAMETER_SET_EXECUTABLE"
    Require-False $set.IsSubmitted "Parameter set submitted." "EXEC_ALGO_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    Require-False $set.HasBrokerRoute "Parameter set has broker route." "EXEC_ALGO_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    Require-True $set.NotAnOrder "Parameter set not marked NotAnOrder." "EXEC_ALGO_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    Require-True $set.NoBrokerRoute "Parameter set not marked NoBrokerRoute." "EXEC_ALGO_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    Require-False $set.CreatesExecutableSchedule "Parameter set creates execution schedule." "EXEC_ALGO_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    Require-False $set.CreatesOrder "Parameter set creates order." "EXEC_ALGO_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
}

Require-True $opening.TargetMayBeKnownPreviousEvening "Opening target previous evening missing." "EXEC_ALGO_R003_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-True $opening.KnownAtTimestampSeparateFromEarliestExecutionTimestamp "Opening KnownAt/Earliest separation missing." "EXEC_ALGO_R003_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-True $opening.EarliestExecutionTimestampMustRemainSessionOpenOrExplicitAllowedStart "Opening earliest execution requirement missing." "EXEC_ALGO_R003_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-False $opening.OvernightExposureBeforeSessionStartAllowed "Opening allows overnight exposure." "EXEC_ALGO_R003_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-False $opening.OrdersBeforeSessionStartAllowed "Opening allows pre-session orders." "EXEC_ALGO_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $opening.ForceBlindCrossingBecauseKnownPreviousEvening "Opening allows blind crossing due to previous-evening knowledge." "EXEC_ALGO_R003_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"

Require-True $intraday.NormalCloseSeekingBehaviorPreserved "Intraday close-seeking behavior weakened." "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"
if ($intraday.ResidualPenaltyBucket -ne "Normal") { Fail "Intraday residual penalty not normal." "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING" }
Require-True $intraday.ControlledResidualCrossOnlyWhenOpportunityCostExceedsCrossingCost "Intraday controlled cross justification missing." "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"

Require-True $closing.MustEndFlat "Closing flatten missing MustEndFlat." "EXEC_ALGO_R003_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-False $closing.OvernightAllowed "Closing flatten allows overnight." "EXEC_ALGO_R003_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
if ($closing.ResidualPenaltyBucket -ne "NoOvernightCritical") { Fail "Closing residual penalty weakened." "EXEC_ALGO_R003_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED" }
if ($closing.MaxResidualAtClose -ne "StrictlyLowerThanIntradayRebalance") { Fail "Closing max residual not stricter than intraday." "EXEC_ALGO_R003_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED" }
Require-False $closing.PureLimitUntilCloseDefaultAllowed "Closing allows PureLimit default." "EXEC_ALGO_R003_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-False $closing.FiveMarketSlicesDefaultAllowed "Closing allows five slices." "EXEC_ALGO_R003_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-False $closing.AlwaysMarketAtCloseDefaultAllowed "Closing allows AlwaysMarketAtClose." "EXEC_ALGO_R003_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-False $closing.BlindMarketCrossingAllowedWithoutCostJustification "Closing allows blind crossing." "EXEC_ALGO_R003_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"

Require-True $rules.validationRulesCreated "Validation rules missing." "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"
Require-True $rules.allBarRolesHaveParameterSet "Not all bar roles have parameter set." "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"
Require-True $rules.allParameterSetsDesignOnly "Parameter sets not design-only." "EXEC_ALGO_R003_FAIL_PARAMETER_SET_EXECUTABLE"
Require-True $rules.allParameterSetsNonExecutable "Parameter sets executable." "EXEC_ALGO_R003_FAIL_PARAMETER_SET_EXECUTABLE"
Require-True $rules.closingFlattenMaxResidualStricterThanIntraday "Closing flatten residual rule missing." "EXEC_ALGO_R003_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-False $rules.openingBuildOvernightExposureAllowed "Opening overnight rule weakened." "EXEC_ALGO_R003_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-False $rules.fiveUsdPerMillionUniversalized "5 USD/million universalized." "EXEC_ALGO_R003_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-False $rules.audusdMarkedFailed "AUDUSD marked failed." "EXEC_ALGO_R003_FAIL_AUDUSD_MISCLASSIFIED"

Require-True $policy.designOnly "Policy recommendations not design-only." "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"
Require-True $policy.notExecutableConfiguration "Policy recommendations executable." "EXEC_ALGO_R003_FAIL_PARAMETER_SET_EXECUTABLE"
foreach ($level in @("Preferred", "Acceptable", "BenchmarkOnly", "ManualReview", "DoNotTrade")) {
    if (@($ladder.fallbackLadderLevels) -notcontains $level) { Fail "Fallback ladder missing $level." "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING" }
}
foreach ($blockedPolicy in @("PureLimitUntilCloseDefault", "MechanicalMarketSlicesAroundClose", "BlindFiveMarketOrdersAroundClose", "BlindFiveMarketOrdersAtOneMinuteIntervals", "AlwaysMarketAtClose", "BlindMarketCrossingWithoutCostJustification", "DirectCrossExecutionByDefault")) {
    if (@($blocked.blockedPolicyFamilies) -notcontains $blockedPolicy) { Fail "Blocked policy missing $blockedPolicy." "EXEC_ALGO_R003_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED" }
}
Require-False $blocked.wakettPatternBlockWeakened "Wakett pattern block weakened." "EXEC_ALGO_R003_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
Require-True $manual.manualReviewTriggerContractCreated "Manual review triggers missing." "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"
foreach ($trigger in @("MissingCloseBenchmark", "NoQuoteNearClose", "StaleQuoteNearClose", "ExtremeSpread", "ClosingFlattenResidualRisk", "RequiresLiquidityCalibration")) {
    if (@($manual.manualReviewTriggers) -notcontains $trigger) { Fail "Manual review trigger missing $trigger." "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING" }
}
Require-True $manual.manualReviewIsNotAutomaticExecution "Manual review treated as automatic execution." "EXEC_ALGO_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

if (@($feed.requirements).Count -ne 3) { Fail "Feed quality requirements by role missing." "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING" }
Require-True $feed.NoQuoteNearCloseTriggersManualReview "NoQuoteNearClose feed trigger missing." "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"
if (@($benchmark.requirements).Count -ne 3) { Fail "Close benchmark requirements by role missing." "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING" }
Require-True $benchmark.MissingCloseBenchmarkTriggersManualReview "Missing close benchmark trigger missing." "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"
Require-True $benchmark.MissingCloseBenchmarkBlocksBlindExecution "Missing close benchmark does not block blind execution." "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"

Require-True $flatten.MustEndFlat "No-overnight flatten MustEndFlat missing." "EXEC_ALGO_R003_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-False $flatten.OvernightAllowed "No-overnight flatten allows overnight." "EXEC_ALGO_R003_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
if ($flatten.ResidualPenaltyBucket -ne "NoOvernightCritical") { Fail "No-overnight residual penalty weakened." "EXEC_ALGO_R003_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED" }
Require-False $flatten.PureLimitUntilCloseDefaultAllowed "No-overnight flatten allows PureLimit default." "EXEC_ALGO_R003_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-False $flatten.AlwaysMarketAtCloseDefaultAllowed "No-overnight flatten allows AlwaysMarketAtClose." "EXEC_ALGO_R003_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-True $firstBar.FirstBarTargetKnownPreviousEveningSupported "First-bar previous evening handling missing." "EXEC_ALGO_R003_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-True $firstBar.KnownAtTimestampUtcSeparateFromEarliestExecutionTimestampUtc "First-bar KnownAt/Earliest separation missing." "EXEC_ALGO_R003_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-False $firstBar.PreviousEveningExecutionAllowed "First-bar allows previous-evening execution." "EXEC_ALGO_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $firstBar.OvernightExposureBeforeSessionStartAllowed "First-bar allows overnight exposure." "EXEC_ALGO_R003_FAIL_CLOSING_FLATTEN_REQUIREMENTS_WEAKENED"
Require-False $firstBar.OrdersBeforeSessionStartAllowed "First-bar allows orders before session." "EXEC_ALGO_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

if ($cost.bestCaseMajorTargetUsdPerMillion -ne 5) { Fail "Best-case major target not 5 USD/million." "EXEC_ALGO_R003_FAIL_5USD_PER_MILLION_UNIVERSALIZED" }
Require-True $cost.fiveUsdPerMillionBestCaseMajorOnly "5 USD/million not marked best-case major only." "EXEC_ALGO_R003_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-False $cost.fiveUsdPerMillionUniversalized "5 USD/million universalized." "EXEC_ALGO_R003_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
if (@($cost.costGuidanceByBarRole).Count -ne 3) { Fail "Cost guidance by bar role missing." "EXEC_ALGO_R003_FAIL_5USD_PER_MILLION_UNIVERSALIZED" }
Require-True $nonMajor.nonMajorEmScandiCnhRequireLiquidityCalibration "Non-major calibration missing." "EXEC_ALGO_R003_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-True $nonMajor.doNotExtrapolateEurusdUsdjpyAudusdResultsToNonMajor "Non-major extrapolation guard missing." "EXEC_ALGO_R003_FAIL_5USD_PER_MILLION_UNIVERSALIZED"

if ($normalization.executionUniverse -ne "USD-pair-only") { Fail "USD-pair execution universe weakened." "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING" }
Require-True $normalization.mandatoryNettingBeforeExecution "Mandatory netting weakened." "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"
Require-False $normalization.normalizationWeakened "USD-pair normalization weakened." "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"
Require-False $directCross.directCrossExecutionAllowedByDefault "Direct-cross default allowed." "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"
Require-False $directCross.directCrossIncludedAsExecutionInstrument "Direct-cross included as execution instrument." "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"
Require-False $directCross.guidanceWeakened "Direct-cross guidance weakened." "EXEC_ALGO_R003_FAIL_PARAMETER_CONTRACT_MISSING"
Require-False $wakett.PureLimitUntilCloseDefaultAllowed "PureLimit default allowed." "EXEC_ALGO_R003_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
Require-False $wakett.MechanicalMarketSlicesAroundCloseAllowed "Mechanical market slices allowed." "EXEC_ALGO_R003_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
Require-False $wakett.AlwaysMarketAtCloseAllowed "AlwaysMarketAtClose allowed." "EXEC_ALGO_R003_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
Require-False $wakett.BlindMarketCrossingWithoutCostJustificationAllowed "Blind market crossing allowed." "EXEC_ALGO_R003_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"
Require-False $wakett.wakettPatternBlockWeakened "Wakett block weakened." "EXEC_ALGO_R003_FAIL_WAKETT_PATTERN_BLOCK_WEAKENED"

Require-True $nonExecutable.allParameterSetsDesignOnly "Non-executable audit: not design-only." "EXEC_ALGO_R003_FAIL_PARAMETER_SET_EXECUTABLE"
Require-True $nonExecutable.allParameterSetsPaperOnly "Non-executable audit: not paper-only." "EXEC_ALGO_R003_FAIL_PARAMETER_SET_EXECUTABLE"
Require-True $nonExecutable.allParameterSetsNonExecutable "Non-executable audit: executable set." "EXEC_ALGO_R003_FAIL_PARAMETER_SET_EXECUTABLE"
Require-False $nonExecutable.parameterSetExecutable "Parameter set executable." "EXEC_ALGO_R003_FAIL_PARAMETER_SET_EXECUTABLE"
Require-False $nonExecutable.executableConfigurationCreated "Executable configuration created." "EXEC_ALGO_R003_FAIL_PARAMETER_SET_EXECUTABLE"
Require-False $nonExecutable.executionScheduleCreated "Execution schedule created." "EXEC_ALGO_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noBacktest.newBacktestExecuted "New backtest executed." "EXEC_ALGO_R003_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $noBacktest.newSimulationExecuted "New simulation executed." "EXEC_ALGO_R003_FAIL_NEW_SIMULATION_RESULTS_CREATED"
Require-False $noBacktest.newQuoteFilesImported "New quote files imported." "EXEC_ALGO_R003_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $noLines.newSimulationResultLinesCreated "New simulation result lines created." "EXEC_ALGO_R003_FAIL_NEW_SIMULATION_RESULTS_CREATED"
Require-False $noLines.fillEntitiesCreated "Fill entities created." "EXEC_ALGO_R003_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $noLines.executionReportEntitiesCreated "Execution report entities created." "EXEC_ALGO_R003_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $api.polygonApiCalled "Polygon API called." "EXEC_ALGO_R003_FAIL_API_CALL_DETECTED"
Require-False $api.lmaxCalled "LMAX called." "EXEC_ALGO_R003_FAIL_API_CALL_DETECTED"
Require-False $api.externalApiCalled "External API called." "EXEC_ALGO_R003_FAIL_API_CALL_DETECTED"
Require-False $runtime.brokerActivationDetected "Broker activation detected." "EXEC_ALGO_R003_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.socketOpened "Socket opened." "EXEC_ALGO_R003_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.tlsOpened "TLS opened." "EXEC_ALGO_R003_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.fixOpened "FIX opened." "EXEC_ALGO_R003_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataRequestSent "MarketDataRequest sent." "EXEC_ALGO_R003_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataResponseRead "MarketDataResponse read." "EXEC_ALGO_R003_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.schedulerServiceTimerPollingBackgroundJobIntroduced "Scheduler/service/timer/polling introduced." "EXEC_ALGO_R003_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $runtime.automaticExecutionIntroduced "Automatic execution introduced." "EXEC_ALGO_R003_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $fill.realFillsCreated "Real fills created." "EXEC_ALGO_R003_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $fill.fillEntitiesCreated "Fill entities created." "EXEC_ALGO_R003_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $report.executionReportEntitiesCreated "Execution report entities created." "EXEC_ALGO_R003_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $report.brokerExecutionReportsCreated "Broker execution reports created." "EXEC_ALGO_R003_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $order.ordersCreated "Orders created." "EXEC_ALGO_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.executableOrdersCreated "Executable orders created." "EXEC_ALGO_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.omsOrdersCreated "OMS orders created." "EXEC_ALGO_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $route.routesCreated "Routes created." "EXEC_ALGO_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $route.submissionsCreated "Submissions created." "EXEC_ALGO_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

Require-True $usdjpy.caveatPreserved "USDJPY caveat missing." "EXEC_ALGO_R003_FAIL_USDJPY_CAVEAT_WEAKENED"
Require-True $usdjpy.requiresInversion "USDJPY inversion missing." "EXEC_ALGO_R003_FAIL_USDJPY_CAVEAT_WEAKENED"
if ($usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8") { Fail "USDJPY SecurityID caveat weakened." "EXEC_ALGO_R003_FAIL_USDJPY_CAVEAT_WEAKENED" }
Require-False $usdjpy.weakened "USDJPY weakened." "EXEC_ALGO_R003_FAIL_USDJPY_CAVEAT_WEAKENED"
Require-False $usdjpy.audusdMisclassifiedFailed "AUDUSD misclassified failed." "EXEC_ALGO_R003_FAIL_AUDUSD_MISCLASSIFIED"
Require-True $lmax.referenceOnly "LMAX baseline not reference-only." "EXEC_ALGO_R003_FAIL_API_CALL_DETECTED"
Require-False $lmax.lmaxCalledInR003 "LMAX called in R003." "EXEC_ALGO_R003_FAIL_API_CALL_DETECTED"
if ($lmax.audusdStatus -notmatch "not failed") { Fail "AUDUSD incorrectly marked failed." "EXEC_ALGO_R003_FAIL_AUDUSD_MISCLASSIFIED" }

Require-False $noExternal.polygonApiCalled "No-external audit shows Polygon call." "EXEC_ALGO_R003_FAIL_API_CALL_DETECTED"
Require-False $noExternal.lmaxCalled "No-external audit shows LMAX call." "EXEC_ALGO_R003_FAIL_API_CALL_DETECTED"
Require-False $noExternal.externalApiCalled "No-external audit shows external API call." "EXEC_ALGO_R003_FAIL_API_CALL_DETECTED"
Require-False $noExternal.newBacktestExecuted "No-external audit shows new backtest." "EXEC_ALGO_R003_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $noExternal.newSimulationExecuted "No-external audit shows new simulation." "EXEC_ALGO_R003_FAIL_NEW_SIMULATION_RESULTS_CREATED"
Require-False $noExternal.newSimulationResultLinesCreated "No-external audit shows result lines." "EXEC_ALGO_R003_FAIL_NEW_SIMULATION_RESULTS_CREATED"
Require-False $noExternal.ordersFillsReportsRoutesSubmissionsCreated "No-external audit shows order-domain output." "EXEC_ALGO_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.liveBrokerProductionTradingStateMutated "No-external audit shows state mutation." "EXEC_ALGO_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.paperLedgerStateCommitted "No-external audit shows paper ledger commit." "EXEC_ALGO_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $forbidden.forbiddenActionsDetected "Forbidden action detected." "EXEC_ALGO_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED"

if ($evidence.dotnetBuildNoRestore -ne "PASS") { Fail "dotnet build evidence is not PASS." "EXEC_ALGO_R003_FAIL_BUILD_OR_TESTS" }
if ($evidence.focusedTests -notmatch "^PASS") { Fail "Focused R003 test evidence is not PASS." "EXEC_ALGO_R003_FAIL_BUILD_OR_TESTS" }
if ($evidence.unitTests -notmatch "^PASS") { Fail "Unit test evidence is not PASS." "EXEC_ALGO_R003_FAIL_BUILD_OR_TESTS" }

Write-Host "EXEC_ALGO_R003_PASS_SESSION_AWARE_PARAMETER_CONTRACT_READY_NO_EXTERNAL"
Write-Host "EXEC_ALGO_R003_PASS_BAR_ROLE_CLOSE_SEEKING_PARAMS_READY_NO_EXTERNAL"
Write-Host "EXEC_ALGO_R003_PASS_NO_OVERNIGHT_FLATTEN_PARAMETER_GATE_READY_NO_EXTERNAL"
Write-Host "EXEC_ALGO_R003_PASS_NONEXECUTABLE_PARAMETER_CONTRACT_READY_NO_EXTERNAL"
exit 0
