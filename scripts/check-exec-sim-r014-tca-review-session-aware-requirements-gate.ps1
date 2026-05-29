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
        Fail "Missing artifact: $Path" "EXEC_SIM_R014_FAIL_BUILD_OR_TESTS"
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Require-False($Value, [string]$Message, [string]$Classification) {
    if ($Value -ne $false) {
        Fail $Message $Classification
    }
}

function Require-True($Value, [string]$Message, [string]$Classification) {
    if ($Value -ne $true) {
        Fail $Message $Classification
    }
}

$requiredArtifacts = @(
    "phase-exec-sim-r014-summary.md",
    "phase-exec-sim-r014-r013-tca-review-summary.json",
    "phase-exec-sim-r014-session-model-contract.json",
    "phase-exec-sim-r014-bar-role-contract.json",
    "phase-exec-sim-r014-opening-build-requirements.json",
    "phase-exec-sim-r014-intraday-rebalance-requirements.json",
    "phase-exec-sim-r014-closing-flatten-requirements.json",
    "phase-exec-sim-r014-no-overnight-flat-constraint.json",
    "phase-exec-sim-r014-first-bar-known-previous-evening.json",
    "phase-exec-sim-r014-session-boundary-turnover-requirements.json",
    "phase-exec-sim-r014-session-aware-policy-parameters.json",
    "phase-exec-sim-r014-session-aware-tca-buckets.json",
    "phase-exec-sim-r014-future-session-aware-simulation-requirements.json",
    "phase-exec-sim-r014-wakett-patterns-session-aware-risk.json",
    "phase-exec-sim-r014-cost-guidance-by-bar-role.json",
    "phase-exec-sim-r014-usd-pair-normalization-preservation.json",
    "phase-exec-sim-r014-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r014-no-new-backtest-audit.json",
    "phase-exec-sim-r014-no-polygon-api-call-audit.json",
    "phase-exec-sim-r014-no-lmax-call-audit.json",
    "phase-exec-sim-r014-no-external-api-call-audit.json",
    "phase-exec-sim-r014-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r014-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r014-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r014-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r014-no-external-audit.json",
    "phase-exec-sim-r014-forbidden-actions-audit.json",
    "phase-exec-sim-r014-next-phase-recommendation.json",
    "phase-exec-sim-r014-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsDir $artifact))) {
        Fail "Required R014 artifact is missing: $artifact" "EXEC_SIM_R014_FAIL_BUILD_OR_TESTS"
    }
}

$review = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r014-r013-tca-review-summary.json")
$session = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r014-session-model-contract.json")
$barRole = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r014-bar-role-contract.json")
$opening = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r014-opening-build-requirements.json")
$intraday = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r014-intraday-rebalance-requirements.json")
$closing = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r014-closing-flatten-requirements.json")
$noOvernight = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r014-no-overnight-flat-constraint.json")
$firstBar = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r014-first-bar-known-previous-evening.json")
$turnover = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r014-session-boundary-turnover-requirements.json")
$parameters = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r014-session-aware-policy-parameters.json")
$buckets = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r014-session-aware-tca-buckets.json")
$future = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r014-future-session-aware-simulation-requirements.json")
$wakett = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r014-wakett-patterns-session-aware-risk.json")
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r014-cost-guidance-by-bar-role.json")
$normalization = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r014-usd-pair-normalization-preservation.json")
$directCross = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r014-direct-cross-exclusion-preservation.json")
$noBacktest = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r014-no-new-backtest-audit.json")
$api = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r014-no-external-api-call-audit.json")
$runtime = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r014-no-broker-marketdata-runtime-audit.json")
$order = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r014-no-order-fill-report-route-audit.json")
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r014-usdjpy-caveat-preservation.json")
$lmax = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r014-lmax-readonly-baseline-reference.json")
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r014-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r014-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r014-build-test-validator-evidence.json")

Require-True $review.reviewOnly "R013 review summary is not review-only." "EXEC_SIM_R014_FAIL_SESSION_MODEL_MISSING"
Require-False $review.newBacktestExecuted "R014 executed a new backtest." "EXEC_SIM_R014_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $review.newSimulationResultLinesCreated "R014 created new simulation result lines." "EXEC_SIM_R014_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $review.r013NumericMetricsInventedInR014 "R014 invented R013 numeric metrics." "EXEC_SIM_R014_FAIL_SESSION_MODEL_MISSING"
if ($review.sourcePhase -ne "EXEC-SIM-R013") { Fail "R013 review summary does not reference R013." "EXEC_SIM_R014_FAIL_SESSION_MODEL_MISSING" }

if ($session.BarIntervalMinutes -ne 15) { Fail "Session interval is not 15 minutes." "EXEC_SIM_R014_FAIL_SESSION_MODEL_MISSING" }
Require-False $session.OvernightAllowed "Session allows overnight." "EXEC_SIM_R014_FAIL_NO_OVERNIGHT_CONSTRAINT_MISSING"
Require-True $session.MustEndFlat "Session does not require end flat." "EXEC_SIM_R014_FAIL_NO_OVERNIGHT_CONSTRAINT_MISSING"
foreach ($role in @("OpeningBuild", "IntradayRebalance", "ClosingFlatten")) {
    if (@($barRole.BarRoles) -notcontains $role) { Fail "Bar role missing: $role" "EXEC_SIM_R014_FAIL_SESSION_MODEL_MISSING" }
}
foreach ($field in @("KnownAtTimestampUtc", "EarliestExecutionTimestampUtc", "ExpectedTurnoverBucket", "ResidualPenaltyBucket")) {
    if (@($barRole.fields) -notcontains $field) { Fail "Bar role field missing: $field" "EXEC_SIM_R014_FAIL_SESSION_MODEL_MISSING" }
}
if (@($barRole.blockedAlgoFamilies) -notcontains "MechanicalFiveMarketSlicesAroundClose") { Fail "Five market slices block missing." "EXEC_SIM_R014_FAIL_SESSION_MODEL_MISSING" }
if (@($barRole.blockedAlgoFamilies) -notcontains "AlwaysMarketAtCloseDefault") { Fail "AlwaysMarketAtClose block missing." "EXEC_SIM_R014_FAIL_SESSION_MODEL_MISSING" }

Require-True $opening.targetMayBeKnownPreviousEvening "OpeningBuild previous evening target missing." "EXEC_SIM_R014_FAIL_FIRST_BAR_KNOWN_PREVIOUS_EVENING_MISSING"
Require-True $opening.EarliestExecutionTimestampUtcMustNotPrecedeAllowedSessionStart "OpeningBuild earliest execution boundary missing." "EXEC_SIM_R014_FAIL_FIRST_BAR_KNOWN_PREVIOUS_EVENING_MISSING"
Require-True $opening.KnownAtTimestampDoesNotAuthorizeOvernightExposure "OpeningBuild overnight exposure guard missing." "EXEC_SIM_R014_FAIL_FIRST_BAR_KNOWN_PREVIOUS_EVENING_MISSING"
Require-False $opening.overnightExposureBeforeSessionStartAllowed "OpeningBuild allows overnight exposure." "EXEC_SIM_R014_FAIL_FIRST_BAR_KNOWN_PREVIOUS_EVENING_MISSING"
Require-True $firstBar.EarliestExecutionTimestampUtcSeparateFromKnownAtTimestampUtc "KnownAt/EarliestExecution separation missing." "EXEC_SIM_R014_FAIL_FIRST_BAR_KNOWN_PREVIOUS_EVENING_MISSING"
Require-False $firstBar.overnightExposureCreationAllowed "First-bar handling allows overnight exposure." "EXEC_SIM_R014_FAIL_FIRST_BAR_KNOWN_PREVIOUS_EVENING_MISSING"

if ($intraday.BarRole -ne "IntradayRebalance") { Fail "Intraday requirements missing." "EXEC_SIM_R014_FAIL_SESSION_MODEL_MISSING" }
Require-True $intraday.normalCloseSeeking15mBehaviorPreserved "Intraday close-seeking behavior not preserved." "EXEC_SIM_R014_FAIL_SESSION_MODEL_MISSING"
if ($intraday.ResidualPenaltyBucket -ne "Normal") { Fail "Intraday residual penalty is not normal." "EXEC_SIM_R014_FAIL_SESSION_MODEL_MISSING" }

if ($closing.TargetPosition -ne "Flat" -or $closing.TargetPositionValue -ne 0) { Fail "ClosingFlatten target is not flat." "EXEC_SIM_R014_FAIL_NO_OVERNIGHT_CONSTRAINT_MISSING" }
Require-True $closing.MustEndFlat "ClosingFlatten does not require flat." "EXEC_SIM_R014_FAIL_NO_OVERNIGHT_CONSTRAINT_MISSING"
Require-False $closing.OvernightAllowed "ClosingFlatten allows overnight." "EXEC_SIM_R014_FAIL_NO_OVERNIGHT_CONSTRAINT_MISSING"
if ($closing.ResidualPenaltyBucket -ne "NoOvernightCritical") { Fail "Closing residual penalty is not no-overnight critical." "EXEC_SIM_R014_FAIL_NO_OVERNIGHT_CONSTRAINT_MISSING" }
if ($closing.CompletionPriority -ne "MustCompleteFlat") { Fail "Closing completion priority is not MustCompleteFlat." "EXEC_SIM_R014_FAIL_NO_OVERNIGHT_CONSTRAINT_MISSING" }
Require-True $closing.NoBlindFiveMarketSlicesAtClose "ClosingFlatten allows blind five slices." "EXEC_SIM_R014_FAIL_NO_OVERNIGHT_CONSTRAINT_MISSING"
Require-False $closing.AlwaysMarketAtCloseDefaultAllowed "ClosingFlatten allows AlwaysMarketAtClose default." "EXEC_SIM_R014_FAIL_NO_OVERNIGHT_CONSTRAINT_MISSING"

Require-False $noOvernight.OvernightAllowed "No-overnight constraint allows overnight." "EXEC_SIM_R014_FAIL_NO_OVERNIGHT_CONSTRAINT_MISSING"
if ($noOvernight.RequiredEndOfSessionPosition -ne "Flat") { Fail "Required end-of-session position is not flat." "EXEC_SIM_R014_FAIL_NO_OVERNIGHT_CONSTRAINT_MISSING" }
Require-True $noOvernight.ManualEscalationRequiredIfResidual "Manual escalation for residual missing." "EXEC_SIM_R014_FAIL_NO_OVERNIGHT_CONSTRAINT_MISSING"
Require-True $noOvernight.NoBlindFiveMarketSlicesAtClose "No-overnight constraint allows blind five slices." "EXEC_SIM_R014_FAIL_NO_OVERNIGHT_CONSTRAINT_MISSING"

$openingTurnover = @($turnover.barRoleTurnoverExpectations) | Where-Object { $_.BarRole -eq "OpeningBuild" } | Select-Object -First 1
$closingTurnover = @($turnover.barRoleTurnoverExpectations) | Where-Object { $_.BarRole -eq "ClosingFlatten" } | Select-Object -First 1
if ($null -eq $openingTurnover -or $openingTurnover.ExpectedTurnoverBucket -ne "High") { Fail "Opening turnover bucket missing/high not set." "EXEC_SIM_R014_FAIL_SESSION_MODEL_MISSING" }
if ($null -eq $closingTurnover -or $closingTurnover.ExpectedTurnoverBucket -ne "VeryHigh") { Fail "Closing turnover bucket missing/very high not set." "EXEC_SIM_R014_FAIL_SESSION_MODEL_MISSING" }
Require-True $turnover.firstAndLastBarsMustBeReportedSeparately "First/last bar separate reporting missing." "EXEC_SIM_R014_FAIL_SESSION_AWARE_TCA_BUCKETS_MISSING"

if ($parameters.ClosingFlattenPolicyParameters.ResidualPenaltyBucket -ne "NoOvernightCritical") { Fail "Closing policy parameter residual penalty missing." "EXEC_SIM_R014_FAIL_NO_OVERNIGHT_CONSTRAINT_MISSING" }
Require-False $parameters.AlwaysMarketAtCloseDefaultAllowed "Session-aware parameters allow AlwaysMarketAtClose default." "EXEC_SIM_R014_FAIL_NO_OVERNIGHT_CONSTRAINT_MISSING"
foreach ($bucket in @("OpeningBuildTca", "IntradayRebalanceTca", "ClosingFlattenTca", "SessionAggregateTca")) {
    if (@($buckets.tcaBuckets) -notcontains $bucket) { Fail "TCA bucket missing: $bucket" "EXEC_SIM_R014_FAIL_SESSION_AWARE_TCA_BUCKETS_MISSING" }
}
if (@($buckets.ResidualPenaltyBucket) -notcontains "NoOvernightCritical") { Fail "NoOvernightCritical TCA bucket missing." "EXEC_SIM_R014_FAIL_SESSION_AWARE_TCA_BUCKETS_MISSING" }
Require-False $buckets.firstLastBarAggregationWithoutSeparateBucketAllowed "First/last aggregation allowed without separate bucket." "EXEC_SIM_R014_FAIL_SESSION_AWARE_TCA_BUCKETS_MISSING"

Require-True $future.mustSeparateFirstMiddleLastBars "Future simulation does not separate first/middle/last bars." "EXEC_SIM_R014_FAIL_SESSION_AWARE_TCA_BUCKETS_MISSING"
Require-True $future.mustNotAggregateFirstAndLastBarsWithNormalIntradayWithoutSeparateBuckets "Future simulation allows unsafe aggregation." "EXEC_SIM_R014_FAIL_SESSION_AWARE_TCA_BUCKETS_MISSING"
Require-True $future.mustReportFiveUsdPerMillionPlausibilityByBarRole "Future simulation does not report 5 USD by role." "EXEC_SIM_R014_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-True $wakett.wakettPatternsRemainBlockedAsDefaultPolicies "Wakett session-aware risk missing." "EXEC_SIM_R014_FAIL_SESSION_MODEL_MISSING"
Require-True $wakett.PureLimitUntilClose.blockedAsDefault "PureLimit default block missing." "EXEC_SIM_R014_FAIL_SESSION_MODEL_MISSING"
Require-True $wakett.MechanicalMarketSlicesAroundClose.blockedAsDefault "FiveMarketSlices default block missing." "EXEC_SIM_R014_FAIL_SESSION_MODEL_MISSING"
Require-False $wakett.AlwaysMarketAtCloseDefaultAllowed "AlwaysMarketAtClose allowed." "EXEC_SIM_R014_FAIL_SESSION_MODEL_MISSING"

if ($cost.bestCaseMajorTargetUsdPerMillion -ne 5) { Fail "Best-case target is not 5 USD/million." "EXEC_SIM_R014_FAIL_5USD_PER_MILLION_UNIVERSALIZED" }
Require-True $cost.fiveUsdPerMillionBestCaseMajorOnly "5 USD/million not best-case major only." "EXEC_SIM_R014_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-False $cost.fiveUsdPerMillionUniversalized "5 USD/million universalized." "EXEC_SIM_R014_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-True $cost.nonMajorEmScandiCnhRequireLiquidityCalibration "Non-major calibration guidance missing." "EXEC_SIM_R014_FAIL_5USD_PER_MILLION_UNIVERSALIZED"

if ($normalization.executionUniverse -ne "USD-pair-only") { Fail "USD-pair execution universe weakened." "EXEC_SIM_R014_FAIL_SESSION_MODEL_MISSING" }
Require-True $normalization.mandatoryNettingBeforeExecution "Mandatory netting weakened." "EXEC_SIM_R014_FAIL_SESSION_MODEL_MISSING"
Require-False $normalization.normalizationWeakened "USD-pair normalization weakened." "EXEC_SIM_R014_FAIL_SESSION_MODEL_MISSING"
Require-False $directCross.directCrossExecutionAllowedByDefault "Direct-cross execution allowed by default." "EXEC_SIM_R014_FAIL_SESSION_MODEL_MISSING"
Require-False $directCross.directCrossIncludedInRequirementsAsExecutionInstrument "Direct-cross included as execution instrument." "EXEC_SIM_R014_FAIL_SESSION_MODEL_MISSING"
Require-False $directCross.guidanceWeakened "Direct-cross guidance weakened." "EXEC_SIM_R014_FAIL_SESSION_MODEL_MISSING"

Require-False $noBacktest.newBacktestExecuted "New backtest executed." "EXEC_SIM_R014_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $noBacktest.newQuoteFilesImported "New quote files imported." "EXEC_SIM_R014_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $noBacktest.newSimulationResultLinesCreated "New simulation result lines created." "EXEC_SIM_R014_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $api.polygonApiCalled "Polygon API called." "EXEC_SIM_R014_FAIL_API_CALL_DETECTED"
Require-False $api.lmaxCalled "LMAX called." "EXEC_SIM_R014_FAIL_API_CALL_DETECTED"
Require-False $api.externalApiCalled "External API called." "EXEC_SIM_R014_FAIL_API_CALL_DETECTED"
Require-False $runtime.brokerActivationDetected "Broker activation detected." "EXEC_SIM_R014_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.socketOpened "Socket opened." "EXEC_SIM_R014_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.tlsOpened "TLS opened." "EXEC_SIM_R014_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.fixOpened "FIX opened." "EXEC_SIM_R014_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataRequestSent "MarketDataRequest sent." "EXEC_SIM_R014_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataResponseRead "MarketDataResponse read." "EXEC_SIM_R014_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.schedulerServiceTimerPollingBackgroundJobIntroduced "Scheduler/service introduced." "EXEC_SIM_R014_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $order.ordersCreated "Orders created." "EXEC_SIM_R014_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.executableOrdersCreated "Executable orders created." "EXEC_SIM_R014_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.fillsCreated "Fills created." "EXEC_SIM_R014_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $order.executionReportsCreated "Execution reports created." "EXEC_SIM_R014_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $order.routesCreated "Routes created." "EXEC_SIM_R014_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.submissionsCreated "Submissions created." "EXEC_SIM_R014_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

Require-True $usdjpy.caveatPreserved "USDJPY caveat not preserved." "EXEC_SIM_R014_FAIL_USDJPY_CAVEAT_WEAKENED"
if ($usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8" -or $usdjpy.requiresInversion -ne $true) {
    Fail "USDJPY caveat/inversion weakened." "EXEC_SIM_R014_FAIL_USDJPY_CAVEAT_WEAKENED"
}
if ($lmax.audusdStatus -notmatch "not failed") { Fail "AUDUSD incorrectly marked failed." "EXEC_SIM_R014_FAIL_AUDUSD_MISCLASSIFIED" }
Require-False $lmax.lmaxCalledInR014 "LMAX called in R014." "EXEC_SIM_R014_FAIL_API_CALL_DETECTED"

Require-False $noExternal.polygonApiCalled "No-external audit shows Polygon call." "EXEC_SIM_R014_FAIL_API_CALL_DETECTED"
Require-False $noExternal.lmaxCalled "No-external audit shows LMAX call." "EXEC_SIM_R014_FAIL_API_CALL_DETECTED"
Require-False $noExternal.externalApiCalled "No-external audit shows external API call." "EXEC_SIM_R014_FAIL_API_CALL_DETECTED"
Require-False $noExternal.newBacktestExecuted "No-external audit shows new backtest." "EXEC_SIM_R014_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $noExternal.newSimulationResultLinesCreated "No-external audit shows new simulation result lines." "EXEC_SIM_R014_FAIL_NEW_BACKTEST_EXECUTED"
Require-False $noExternal.ordersFillsReportsRoutesSubmissionsCreated "No-external audit shows order-domain artifact." "EXEC_SIM_R014_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.liveBrokerProductionTradingStateMutated "No-external audit shows state mutation." "EXEC_SIM_R014_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.paperLedgerStateCommitted "No-external audit shows paper ledger commit." "EXEC_SIM_R014_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $forbidden.forbiddenActionsDetected "Forbidden action detected." "EXEC_SIM_R014_FAIL_NEW_EXTERNAL_ACTION_DETECTED"

if ($evidence.dotnetBuildNoRestore -ne "PASS") { Fail "dotnet build evidence is not PASS." "EXEC_SIM_R014_FAIL_BUILD_OR_TESTS" }
if ($evidence.focusedTests -notmatch "^PASS") { Fail "Focused R014 test evidence is not PASS." "EXEC_SIM_R014_FAIL_BUILD_OR_TESTS" }
if ($evidence.unitTests -notmatch "^PASS") { Fail "Unit test evidence is not PASS." "EXEC_SIM_R014_FAIL_BUILD_OR_TESTS" }

Write-Host "EXEC_SIM_R014_PASS_FIRST_REAL_TCA_REVIEW_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R014_PASS_SESSION_AWARE_EXECUTION_REQUIREMENTS_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R014_PASS_NO_OVERNIGHT_FLAT_CONSTRAINT_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R014_PASS_NO_NEW_BACKTEST_NO_ORDER_GATE_READY_NO_EXTERNAL"
exit 0
