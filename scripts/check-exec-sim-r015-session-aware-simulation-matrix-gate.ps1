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
        Fail "Missing artifact: $Path" "EXEC_SIM_R015_FAIL_BUILD_OR_TESTS"
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
    "phase-exec-sim-r015-summary.md",
    "phase-exec-sim-r015-session-aware-simulation-contract.json",
    "phase-exec-sim-r015-bar-role-scenarios.json",
    "phase-exec-sim-r015-opening-build-scenarios.json",
    "phase-exec-sim-r015-intraday-rebalance-scenarios.json",
    "phase-exec-sim-r015-closing-flatten-scenarios.json",
    "phase-exec-sim-r015-session-aware-policy-results.json",
    "phase-exec-sim-r015-session-aware-tca-buckets.json",
    "phase-exec-sim-r015-opening-build-tca-report.json",
    "phase-exec-sim-r015-intraday-rebalance-tca-report.json",
    "phase-exec-sim-r015-closing-flatten-tca-report.json",
    "phase-exec-sim-r015-session-aggregate-tca-report.json",
    "phase-exec-sim-r015-per-instrument-bar-role-eurusd-report.json",
    "phase-exec-sim-r015-per-instrument-bar-role-usdjpy-report.json",
    "phase-exec-sim-r015-per-instrument-bar-role-audusd-report.json",
    "phase-exec-sim-r015-policy-ranking-by-bar-role-median-slippage.json",
    "phase-exec-sim-r015-policy-ranking-by-bar-role-p95-slippage.json",
    "phase-exec-sim-r015-policy-ranking-by-bar-role-fill-ratio.json",
    "phase-exec-sim-r015-policy-ranking-by-bar-role-residual.json",
    "phase-exec-sim-r015-policy-ranking-by-bar-role-spread-paid.json",
    "phase-exec-sim-r015-no-overnight-residual-penalty-report.json",
    "phase-exec-sim-r015-wakett-session-aware-risk-report.json",
    "phase-exec-sim-r015-close-seeking-session-aware-comparison.json",
    "phase-exec-sim-r015-opening-build-known-previous-evening-preservation.json",
    "phase-exec-sim-r015-no-overnight-flat-constraint-preservation.json",
    "phase-exec-sim-r015-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r015-major-pair-5usd-bestcase-by-bar-role.json",
    "phase-exec-sim-r015-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r015-no-polygon-api-call-audit.json",
    "phase-exec-sim-r015-no-lmax-call-audit.json",
    "phase-exec-sim-r015-no-external-api-call-audit.json",
    "phase-exec-sim-r015-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r015-no-real-fill-audit.json",
    "phase-exec-sim-r015-no-execution-report-audit.json",
    "phase-exec-sim-r015-no-order-created-audit.json",
    "phase-exec-sim-r015-no-route-no-submission-audit.json",
    "phase-exec-sim-r015-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r015-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r015-no-external-audit.json",
    "phase-exec-sim-r015-forbidden-actions-audit.json",
    "phase-exec-sim-r015-next-phase-recommendation.json",
    "phase-exec-sim-r015-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsDir $artifact))) {
        Fail "Required R015 artifact is missing: $artifact" "EXEC_SIM_R015_FAIL_BUILD_OR_TESTS"
    }
}

$contract = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r015-session-aware-simulation-contract.json")
$scenarios = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r015-bar-role-scenarios.json")
$opening = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r015-opening-build-scenarios.json")
$intraday = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r015-intraday-rebalance-scenarios.json")
$closing = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r015-closing-flatten-scenarios.json")
$policy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r015-session-aware-policy-results.json")
$buckets = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r015-session-aware-tca-buckets.json")
$closingTca = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r015-closing-flatten-tca-report.json")
$aggregate = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r015-session-aggregate-tca-report.json")
$penalty = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r015-no-overnight-residual-penalty-report.json")
$wakett = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r015-wakett-session-aware-risk-report.json")
$closeSeeking = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r015-close-seeking-session-aware-comparison.json")
$known = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r015-opening-build-known-previous-evening-preservation.json")
$flat = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r015-no-overnight-flat-constraint-preservation.json")
$directCross = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r015-direct-cross-exclusion-preservation.json")
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r015-major-pair-5usd-bestcase-by-bar-role.json")
$nonMajor = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r015-nonmajor-calibration-preservation.json")
$api = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r015-no-external-api-call-audit.json")
$runtime = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r015-no-broker-marketdata-runtime-audit.json")
$fill = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r015-no-real-fill-audit.json")
$report = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r015-no-execution-report-audit.json")
$order = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r015-no-order-created-audit.json")
$route = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r015-no-route-no-submission-audit.json")
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r015-usdjpy-caveat-preservation.json")
$lmax = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r015-lmax-readonly-baseline-reference.json")
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r015-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r015-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r015-build-test-validator-evidence.json")

Require-True $contract.noExternal "Contract is not no-external." "EXEC_SIM_R015_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-True $contract.outputsFixtureOnly "Contract outputs are not fixture-only." "EXEC_SIM_R015_FAIL_SESSION_AWARE_MATRIX_MISSING"
Require-True $contract.outputsPaperOnly "Contract outputs are not paper-only." "EXEC_SIM_R015_FAIL_SESSION_AWARE_MATRIX_MISSING"
Require-True $contract.outputsNonExecutable "Contract outputs are not non-executable." "EXEC_SIM_R015_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $contract.simulationResultLinesAreFills "Simulation lines represented as fills." "EXEC_SIM_R015_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $contract.fillEntitiesCreated "Fill entities created." "EXEC_SIM_R015_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $contract.executionReportEntitiesCreated "Execution report entities created." "EXEC_SIM_R015_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
foreach ($role in @("OpeningBuild", "IntradayRebalance", "ClosingFlatten")) {
    if (@($contract.barRoles) -notcontains $role) { Fail "Contract missing role $role." "EXEC_SIM_R015_FAIL_SESSION_AWARE_MATRIX_MISSING" }
}

Require-False $scenarios.directCrossExecutionIncluded "Direct cross included in scenario matrix." "EXEC_SIM_R015_FAIL_SESSION_AWARE_MATRIX_MISSING"
foreach ($role in @("OpeningBuild", "IntradayRebalance", "ClosingFlatten")) {
    $row = @($scenarios.barRoles) | Where-Object { $_.BarRole -eq $role } | Select-Object -First 1
    if ($null -eq $row) { Fail "Bar role scenario missing: $role" "EXEC_SIM_R015_FAIL_SESSION_AWARE_MATRIX_MISSING" }
}

Require-True $opening.KnownAtTimestampAndEarliestExecutionAreDistinct "Opening KnownAt/EarliestExecution weakened." "EXEC_SIM_R015_FAIL_OPENING_BUILD_PREVIOUS_EVENING_WEAKENED"
Require-False $opening.ExecutionBeforeSessionStartAllowed "Opening execution before session start allowed." "EXEC_SIM_R015_FAIL_OPENING_BUILD_PREVIOUS_EVENING_WEAKENED"
foreach ($row in @($opening.scenarios)) {
    if ($row.CurrentPosition -ne "Flat" -or $row.ExpectedTurnoverBucket -ne "High" -or $row.OvernightExposureAuthorized -ne $false) {
        Fail "Opening scenario shape invalid." "EXEC_SIM_R015_FAIL_OPENING_BUILD_PREVIOUS_EVENING_WEAKENED"
    }
}
foreach ($row in @($intraday.scenarios)) {
    if ($row.ResidualPenaltyBucket -ne "Normal") { Fail "Intraday residual penalty not normal." "EXEC_SIM_R015_FAIL_SESSION_AWARE_MATRIX_MISSING" }
}
foreach ($row in @($closing.scenarios)) {
    if ($row.TargetPosition -ne "Flat" -or $row.MustEndFlat -ne $true -or $row.OvernightAllowed -ne $false -or $row.ResidualPenaltyBucket -ne "NoOvernightCritical") {
        Fail "Closing flatten scenario weakened." "EXEC_SIM_R015_FAIL_NO_OVERNIGHT_CONSTRAINT_WEAKENED"
    }
}
Require-True $closing.PureLimitUntilCloseDefaultBlocked "Closing allows PureLimit default." "EXEC_SIM_R015_FAIL_CLOSING_FLATTEN_UNSAFE_DEFAULT_ALLOWED"
Require-True $closing.FiveMarketSlicesDefaultBlocked "Closing allows five-slice default." "EXEC_SIM_R015_FAIL_CLOSING_FLATTEN_UNSAFE_DEFAULT_ALLOWED"
Require-True $closing.AlwaysMarketAtCloseDefaultBlocked "Closing allows AlwaysMarketAtClose default." "EXEC_SIM_R015_FAIL_CLOSING_FLATTEN_UNSAFE_DEFAULT_ALLOWED"

foreach ($bucket in @("OpeningBuildTca", "IntradayRebalanceTca", "ClosingFlattenTca", "SessionAggregateTca")) {
    if (@($buckets.tcaBuckets) -notcontains $bucket) { Fail "TCA bucket missing: $bucket" "EXEC_SIM_R015_FAIL_BAR_ROLE_TCA_BUCKETS_MISSING" }
}
if (@($buckets.ResidualPenaltyBucket) -notcontains "NoOvernightCritical") { Fail "NoOvernightCritical bucket missing." "EXEC_SIM_R015_FAIL_BAR_ROLE_TCA_BUCKETS_MISSING" }
Require-True $buckets.firstLastBarsReportedSeparately "First/last bars not separately reported." "EXEC_SIM_R015_FAIL_BAR_ROLE_TCA_BUCKETS_MISSING"

foreach ($file in @("phase-exec-sim-r015-per-instrument-bar-role-eurusd-report.json", "phase-exec-sim-r015-per-instrument-bar-role-usdjpy-report.json", "phase-exec-sim-r015-per-instrument-bar-role-audusd-report.json")) {
    $instrument = Read-Json (Join-Path $ArtifactsDir $file)
    if (@($instrument.barRoleReports).Count -ne 3) { Fail "Per-instrument bar-role report incomplete: $file" "EXEC_SIM_R015_FAIL_SESSION_AWARE_MATRIX_MISSING" }
    Require-True $instrument.FixtureOnly "Instrument report is not fixture-only: $file" "EXEC_SIM_R015_FAIL_SESSION_AWARE_MATRIX_MISSING"
    Require-True $instrument.PaperOnly "Instrument report is not paper-only: $file" "EXEC_SIM_R015_FAIL_SESSION_AWARE_MATRIX_MISSING"
}
foreach ($ranking in @("phase-exec-sim-r015-policy-ranking-by-bar-role-median-slippage.json", "phase-exec-sim-r015-policy-ranking-by-bar-role-p95-slippage.json", "phase-exec-sim-r015-policy-ranking-by-bar-role-fill-ratio.json", "phase-exec-sim-r015-policy-ranking-by-bar-role-residual.json", "phase-exec-sim-r015-policy-ranking-by-bar-role-spread-paid.json")) {
    $rankingJson = Read-Json (Join-Path $ArtifactsDir $ranking)
    if (@($rankingJson.rankingsByBarRole).Count -lt 3) { Fail "Policy ranking by bar role missing/incomplete: $ranking" "EXEC_SIM_R015_FAIL_BAR_ROLE_TCA_BUCKETS_MISSING" }
}

Require-True $policy.allExpectedPoliciesComparedByBarRole "Expected policies not compared by role." "EXEC_SIM_R015_FAIL_SESSION_AWARE_MATRIX_MISSING"
Require-False $policy.simulationResultLinesAreFills "Simulation lines are fills." "EXEC_SIM_R015_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $policy.fillEntitiesCreated "Fill entities created." "EXEC_SIM_R015_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $policy.executionReportEntitiesCreated "Execution report entities created." "EXEC_SIM_R015_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
foreach ($row in @($policy.policyResults)) {
    Require-True $row.FixtureOnly "Policy row not fixture-only." "EXEC_SIM_R015_FAIL_SESSION_AWARE_MATRIX_MISSING"
    Require-True $row.PaperOnly "Policy row not paper-only." "EXEC_SIM_R015_FAIL_SESSION_AWARE_MATRIX_MISSING"
    Require-True $row.NonExecutable "Policy row executable." "EXEC_SIM_R015_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    Require-True $row.NotAnOrder "Policy row not marked NotAnOrder." "EXEC_SIM_R015_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    Require-True $row.NoRealFill "Policy row not marked NoRealFill." "EXEC_SIM_R015_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
    Require-True $row.NoExecutionReport "Policy row not marked NoExecutionReport." "EXEC_SIM_R015_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
}

Require-True $closingTca.MustEndFlat "Closing TCA does not require flat." "EXEC_SIM_R015_FAIL_NO_OVERNIGHT_CONSTRAINT_WEAKENED"
if ($closingTca.ResidualPenaltyBucket -ne "NoOvernightCritical") { Fail "Closing TCA residual penalty weakened." "EXEC_SIM_R015_FAIL_NO_OVERNIGHT_CONSTRAINT_WEAKENED" }
Require-True $closingTca.FiveMarketSlicesStillBlocked "Closing TCA allows five slices." "EXEC_SIM_R015_FAIL_CLOSING_FLATTEN_UNSAFE_DEFAULT_ALLOWED"
Require-True $closingTca.AlwaysMarketAtCloseDefaultBlocked "Closing TCA allows AlwaysMarketAtClose." "EXEC_SIM_R015_FAIL_CLOSING_FLATTEN_UNSAFE_DEFAULT_ALLOWED"
Require-True $aggregate.OpeningAndClosingNotCalibratedLikeIntraday "Session aggregate does not distinguish boundary bars." "EXEC_SIM_R015_FAIL_SESSION_AWARE_MATRIX_MISSING"

Require-False $penalty.OvernightAllowed "No-overnight penalty report allows overnight." "EXEC_SIM_R015_FAIL_NO_OVERNIGHT_CONSTRAINT_WEAKENED"
Require-True $penalty.MustEndFlat "No-overnight penalty report missing MustEndFlat." "EXEC_SIM_R015_FAIL_NO_OVERNIGHT_CONSTRAINT_WEAKENED"
Require-True $penalty.ClosingFlattenResidualMoreExpensive "Closing residual penalty missing." "EXEC_SIM_R015_FAIL_NO_OVERNIGHT_CONSTRAINT_WEAKENED"
Require-True $wakett.PureLimitUntilClose.blockedAsDefault "Wakett limit not blocked." "EXEC_SIM_R015_FAIL_CLOSING_FLATTEN_UNSAFE_DEFAULT_ALLOWED"
Require-True $wakett.WakettFiveMarketSlicesAroundClose.blockedAsDefault "Wakett five slices not blocked." "EXEC_SIM_R015_FAIL_CLOSING_FLATTEN_UNSAFE_DEFAULT_ALLOWED"
Require-False $wakett.AlwaysMarketAtCloseDefaultAllowed "AlwaysMarketAtClose allowed." "EXEC_SIM_R015_FAIL_CLOSING_FLATTEN_UNSAFE_DEFAULT_ALLOWED"
Require-True $closeSeeking.barRoleSpecificThresholdsRequired "CloseSeeking role-specific thresholds missing." "EXEC_SIM_R015_FAIL_SESSION_AWARE_MATRIX_MISSING"

Require-True $known.KnownAtTimestampUtcMayBePreviousEvening "Opening previous-evening target missing." "EXEC_SIM_R015_FAIL_OPENING_BUILD_PREVIOUS_EVENING_WEAKENED"
Require-True $known.EarliestExecutionTimestampUtcSeparateFromKnownAtTimestampUtc "KnownAt/Earliest separation missing." "EXEC_SIM_R015_FAIL_OPENING_BUILD_PREVIOUS_EVENING_WEAKENED"
Require-False $known.OvernightExposureBeforeSessionStartAllowed "Opening known previous evening authorizes overnight exposure." "EXEC_SIM_R015_FAIL_OPENING_BUILD_PREVIOUS_EVENING_WEAKENED"
Require-False $flat.OvernightAllowed "Flat constraint allows overnight." "EXEC_SIM_R015_FAIL_NO_OVERNIGHT_CONSTRAINT_WEAKENED"
Require-True $flat.MustEndFlat "Flat constraint missing MustEndFlat." "EXEC_SIM_R015_FAIL_NO_OVERNIGHT_CONSTRAINT_WEAKENED"

Require-False $directCross.directCrossExecutionAllowedByDefault "Direct-cross execution allowed." "EXEC_SIM_R015_FAIL_SESSION_AWARE_MATRIX_MISSING"
Require-False $directCross.directCrossIncludedInSessionMatrix "Direct-cross included in matrix." "EXEC_SIM_R015_FAIL_SESSION_AWARE_MATRIX_MISSING"
Require-False $directCross.guidanceWeakened "Direct-cross guidance weakened." "EXEC_SIM_R015_FAIL_SESSION_AWARE_MATRIX_MISSING"
if ($cost.bestCaseMajorTargetUsdPerMillion -ne 5) { Fail "Best-case major target is not 5." "EXEC_SIM_R015_FAIL_5USD_PER_MILLION_UNIVERSALIZED" }
Require-True $cost.fiveUsdPerMillionBestCaseMajorOnly "5 USD/million not best-case major only." "EXEC_SIM_R015_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-False $cost.fiveUsdPerMillionUniversalized "5 USD/million universalized." "EXEC_SIM_R015_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
if (@($cost.reportedByBarRole).Count -ne 3) { Fail "5 USD/million not reported by bar role." "EXEC_SIM_R015_FAIL_5USD_PER_MILLION_UNIVERSALIZED" }
Require-True $nonMajor.nonMajorEmScandiCnhRequireLiquidityCalibration "Non-major calibration guidance missing." "EXEC_SIM_R015_FAIL_5USD_PER_MILLION_UNIVERSALIZED"

Require-False $api.polygonApiCalled "Polygon API called." "EXEC_SIM_R015_FAIL_API_CALL_DETECTED"
Require-False $api.lmaxCalled "LMAX called." "EXEC_SIM_R015_FAIL_API_CALL_DETECTED"
Require-False $api.externalApiCalled "External API called." "EXEC_SIM_R015_FAIL_API_CALL_DETECTED"
Require-False $runtime.brokerActivationDetected "Broker activation detected." "EXEC_SIM_R015_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.socketOpened "Socket opened." "EXEC_SIM_R015_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.tlsOpened "TLS opened." "EXEC_SIM_R015_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.fixOpened "FIX opened." "EXEC_SIM_R015_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataRequestSent "MarketDataRequest sent." "EXEC_SIM_R015_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataResponseRead "MarketDataResponse read." "EXEC_SIM_R015_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.schedulerServiceTimerPollingBackgroundJobIntroduced "Scheduler/service introduced." "EXEC_SIM_R015_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $fill.simulationResultLinesAreFills "Simulation result lines are fills." "EXEC_SIM_R015_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $fill.realFillsCreated "Real fills created." "EXEC_SIM_R015_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $fill.fillEntitiesCreated "Fill entities created." "EXEC_SIM_R015_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $report.executionReportEntitiesCreated "Execution report entities created." "EXEC_SIM_R015_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $report.brokerExecutionReportsCreated "Broker execution reports created." "EXEC_SIM_R015_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $order.ordersCreated "Orders created." "EXEC_SIM_R015_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.executableOrdersCreated "Executable orders created." "EXEC_SIM_R015_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $route.routesCreated "Routes created." "EXEC_SIM_R015_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $route.submissionsCreated "Submissions created." "EXEC_SIM_R015_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

Require-True $usdjpy.caveatPreserved "USDJPY caveat not preserved." "EXEC_SIM_R015_FAIL_USDJPY_CAVEAT_WEAKENED"
if ($usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8" -or $usdjpy.requiresInversion -ne $true) {
    Fail "USDJPY caveat/inversion weakened." "EXEC_SIM_R015_FAIL_USDJPY_CAVEAT_WEAKENED"
}
if ($lmax.audusdStatus -notmatch "not failed") { Fail "AUDUSD incorrectly marked failed." "EXEC_SIM_R015_FAIL_AUDUSD_MISCLASSIFIED" }
Require-False $lmax.lmaxCalledInR015 "LMAX called in R015." "EXEC_SIM_R015_FAIL_API_CALL_DETECTED"

Require-False $noExternal.polygonApiCalled "No-external audit shows Polygon call." "EXEC_SIM_R015_FAIL_API_CALL_DETECTED"
Require-False $noExternal.lmaxCalled "No-external audit shows LMAX call." "EXEC_SIM_R015_FAIL_API_CALL_DETECTED"
Require-False $noExternal.externalApiCalled "No-external audit shows external API call." "EXEC_SIM_R015_FAIL_API_CALL_DETECTED"
Require-False $noExternal.ordersFillsReportsRoutesSubmissionsCreated "No-external audit shows order-domain artifact." "EXEC_SIM_R015_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.realFillsCreated "No-external audit shows real fills." "EXEC_SIM_R015_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $noExternal.executionReportEntitiesCreated "No-external audit shows execution reports." "EXEC_SIM_R015_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $noExternal.liveBrokerProductionTradingStateMutated "No-external audit shows state mutation." "EXEC_SIM_R015_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.paperLedgerStateCommitted "No-external audit shows paper ledger commit." "EXEC_SIM_R015_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $forbidden.forbiddenActionsDetected "Forbidden action detected." "EXEC_SIM_R015_FAIL_NEW_EXTERNAL_ACTION_DETECTED"

if ($evidence.dotnetBuildNoRestore -ne "PASS") { Fail "dotnet build evidence is not PASS." "EXEC_SIM_R015_FAIL_BUILD_OR_TESTS" }
if ($evidence.focusedTests -notmatch "^PASS") { Fail "Focused R015 test evidence is not PASS." "EXEC_SIM_R015_FAIL_BUILD_OR_TESTS" }
if ($evidence.unitTests -notmatch "^PASS") { Fail "Unit test evidence is not PASS." "EXEC_SIM_R015_FAIL_BUILD_OR_TESTS" }

Write-Host "EXEC_SIM_R015_PASS_SESSION_AWARE_SIMULATION_MATRIX_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R015_PASS_BAR_ROLE_TCA_BUCKETS_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R015_PASS_NO_OVERNIGHT_FLATTENING_REQUIREMENTS_SIMULATED_NO_EXTERNAL"
Write-Host "EXEC_SIM_R015_PASS_NO_REAL_FILL_NO_ORDER_SESSION_SIM_GATE_READY_NO_EXTERNAL"
exit 0
