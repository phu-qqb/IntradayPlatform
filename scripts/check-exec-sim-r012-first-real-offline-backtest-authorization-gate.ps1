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
        Fail "Missing artifact: $Path" "EXEC_SIM_R012_FAIL_BUILD_OR_TESTS"
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
    "phase-exec-sim-r012-summary.md",
    "phase-exec-sim-r012-backtest-authorization-contract.json",
    "phase-exec-sim-r012-backtest-authorization-request.json",
    "phase-exec-sim-r012-backtest-preflight-contract.json",
    "phase-exec-sim-r012-backtest-authorization-result.json",
    "phase-exec-sim-r012-accepted-files-authorized.json",
    "phase-exec-sim-r012-sanitized-import-readiness-authorized.json",
    "phase-exec-sim-r012-quote-window-readiness-authorized.json",
    "phase-exec-sim-r012-close-benchmark-readiness-authorized.json",
    "phase-exec-sim-r012-feed-quality-readiness-authorized.json",
    "phase-exec-sim-r012-quarantined-files-excluded.json",
    "phase-exec-sim-r012-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r012-expected-policy-list.json",
    "phase-exec-sim-r012-expected-tca-report-list.json",
    "phase-exec-sim-r012-cost-guidance-preservation.json",
    "phase-exec-sim-r012-no-backtest-execution-audit.json",
    "phase-exec-sim-r012-no-tca-policy-results-audit.json",
    "phase-exec-sim-r012-no-simulation-result-lines-audit.json",
    "phase-exec-sim-r012-no-polygon-api-call-audit.json",
    "phase-exec-sim-r012-no-lmax-call-audit.json",
    "phase-exec-sim-r012-no-external-api-call-audit.json",
    "phase-exec-sim-r012-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r012-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r012-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r012-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r012-no-external-audit.json",
    "phase-exec-sim-r012-forbidden-actions-audit.json",
    "phase-exec-sim-r012-next-phase-recommendation.json",
    "phase-exec-sim-r012-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsDir $artifact))) {
        Fail "Required R012 artifact is missing: $artifact" "EXEC_SIM_R012_FAIL_BUILD_OR_TESTS"
    }
}

$contract = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r012-backtest-authorization-contract.json")
$request = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r012-backtest-authorization-request.json")
$preflight = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r012-backtest-preflight-contract.json")
$result = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r012-backtest-authorization-result.json")
$accepted = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r012-accepted-files-authorized.json")
$importReady = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r012-sanitized-import-readiness-authorized.json")
$quoteWindow = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r012-quote-window-readiness-authorized.json")
$benchmark = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r012-close-benchmark-readiness-authorized.json")
$feed = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r012-feed-quality-readiness-authorized.json")
$quarantine = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r012-quarantined-files-excluded.json")
$directCross = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r012-direct-cross-exclusion-preservation.json")
$policies = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r012-expected-policy-list.json")
$reports = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r012-expected-tca-report-list.json")
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r012-cost-guidance-preservation.json")
$noBacktest = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r012-no-backtest-execution-audit.json")
$noTca = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r012-no-tca-policy-results-audit.json")
$noLines = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r012-no-simulation-result-lines-audit.json")
$api = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r012-no-external-api-call-audit.json")
$runtime = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r012-no-broker-marketdata-runtime-audit.json")
$order = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r012-no-order-fill-report-route-audit.json")
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r012-usdjpy-caveat-preservation.json")
$lmax = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r012-lmax-readonly-baseline-reference.json")
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r012-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r012-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r012-build-test-validator-evidence.json")

Require-True $contract.authorizationOnly "Authorization contract is not authorization-only." "EXEC_SIM_R012_FAIL_AUTHORIZATION_CONTRACT_MISSING"
Require-True $contract.requiresAcceptedFileManifests "Accepted files requirement missing." "EXEC_SIM_R012_FAIL_ACCEPTED_FILES_AUTHORIZATION_MISSING"
Require-True $contract.requiresSanitizedImportReadiness "Sanitized import readiness requirement missing." "EXEC_SIM_R012_FAIL_ACCEPTED_FILES_AUTHORIZATION_MISSING"
Require-True $contract.requiresQuoteWindowReadiness "Quote-window readiness requirement missing." "EXEC_SIM_R012_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_AUTHORIZATION_MISSING"
Require-True $contract.requiresCloseBenchmarkReadiness "Close benchmark readiness requirement missing." "EXEC_SIM_R012_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_AUTHORIZATION_MISSING"
Require-True $contract.requiresFeedQualityReadiness "Feed quality readiness requirement missing." "EXEC_SIM_R012_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_AUTHORIZATION_MISSING"
Require-True $contract.noBacktest "Contract allows backtest." "EXEC_SIM_R012_FAIL_BACKTEST_EXECUTED"
Require-True $contract.noTcaPolicyResults "Contract allows TCA results." "EXEC_SIM_R012_FAIL_TCA_RESULTS_PRODUCED"
Require-True $contract.noSimulationResultLines "Contract allows simulation result lines." "EXEC_SIM_R012_FAIL_TCA_RESULTS_PRODUCED"

Require-True $request.AuthorizationOnly "Request is not authorization-only." "EXEC_SIM_R012_FAIL_AUTHORIZATION_CONTRACT_MISSING"
Require-True $request.NoBacktest "Request allows backtest." "EXEC_SIM_R012_FAIL_BACKTEST_EXECUTED"
Require-True $request.NoTcaPolicyResults "Request allows TCA policy results." "EXEC_SIM_R012_FAIL_TCA_RESULTS_PRODUCED"
Require-True $request.NoSimulationResultLines "Request allows simulation result lines." "EXEC_SIM_R012_FAIL_TCA_RESULTS_PRODUCED"
Require-True $preflight.preflightReady "Backtest preflight missing/not ready." "EXEC_SIM_R012_FAIL_BACKTEST_PREFLIGHT_MISSING"
Require-True $result.AuthorizationReady "Authorization result not ready." "EXEC_SIM_R012_FAIL_AUTHORIZATION_CONTRACT_MISSING"
if ($result.AuthorizationStatus -ne "FirstRealOfflineBacktestAuthorizationReadyNoExternal") {
    Fail "Authorization result status is not ready." "EXEC_SIM_R012_FAIL_AUTHORIZATION_CONTRACT_MISSING"
}
if ($result.Classification -ne "EXEC_SIM_R012_PASS_FIRST_REAL_OFFLINE_BACKTEST_AUTHORIZATION_READY_NO_EXTERNAL") {
    Fail "Authorization classification is not expected success." "EXEC_SIM_R012_FAIL_AUTHORIZATION_CONTRACT_MISSING"
}

$expectedRows = @{ EURUSD = 54694; USDJPY = 59368; AUDUSD = 60656 }
foreach ($symbol in @("EURUSD", "USDJPY", "AUDUSD")) {
    $entry = @($accepted.acceptedFilesAuthorizedForFutureBacktest) | Where-Object { $_.ExecutionTradableSymbol -eq $symbol } | Select-Object -First 1
    if ($null -eq $entry) { Fail "Accepted file authorization missing $symbol." "EXEC_SIM_R012_FAIL_ACCEPTED_FILES_AUTHORIZATION_MISSING" }
    if ($entry.Rows -ne $expectedRows[$symbol] -or $entry.ValidationStatus -ne "AcceptedForSanitizedImport") {
        Fail "Accepted file authorization invalid for $symbol." "EXEC_SIM_R012_FAIL_ACCEPTED_FILES_AUTHORIZATION_MISSING"
    }
    $ready = @($importReady.authorizedReadiness) | Where-Object { $_.ExecutionTradableSymbol -eq $symbol } | Select-Object -First 1
    if ($null -eq $ready -or $ready.SanitizedImportReady -ne $true) { Fail "Sanitized import readiness missing $symbol." "EXEC_SIM_R012_FAIL_ACCEPTED_FILES_AUTHORIZATION_MISSING" }
    $qw = @($quoteWindow.authorizedQuoteWindowReadiness) | Where-Object { $_.ExecutionTradableSymbol -eq $symbol } | Select-Object -First 1
    if ($null -eq $qw -or $qw.FeedWindowStatus -ne "Ready") { Fail "Quote-window readiness missing $symbol." "EXEC_SIM_R012_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_AUTHORIZATION_MISSING" }
    $cb = @($benchmark.authorizedCloseBenchmarkReadiness) | Where-Object { $_.ExecutionTradableSymbol -eq $symbol } | Select-Object -First 1
    if ($null -eq $cb -or $cb.CloseBenchmarkStatus -ne "Available") { Fail "Close benchmark readiness missing $symbol." "EXEC_SIM_R012_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_AUTHORIZATION_MISSING" }
    $fq = @($feed.authorizedFeedQualityReadiness) | Where-Object { $_.ExecutionTradableSymbol -eq $symbol } | Select-Object -First 1
    if ($null -eq $fq -or @("Good", "Usable", "Excellent") -notcontains $fq.FeedQualityBucket) { Fail "Feed quality readiness missing $symbol." "EXEC_SIM_R012_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_AUTHORIZATION_MISSING" }
}
Require-True $accepted.allRequiredAcceptedFilesAuthorized "Not all accepted files authorized." "EXEC_SIM_R012_FAIL_ACCEPTED_FILES_AUTHORIZATION_MISSING"
Require-False $importReady.sanitizedQuoteRowsCreated "Sanitized quote rows created in R012." "EXEC_SIM_R012_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-False $importReady.importExecuted "Import executed in R012." "EXEC_SIM_R012_FAIL_BACKTEST_EXECUTED"
Require-False $quarantine.quarantinedFilesIncluded "Quarantined files included." "EXEC_SIM_R012_FAIL_ACCEPTED_FILES_AUTHORIZATION_MISSING"

if ($directCross.executionUniverse -ne "USD-pair-only") { Fail "Execution universe weakened." "EXEC_SIM_R012_FAIL_DIRECT_CROSS_GUIDANCE_WEAKENED" }
Require-False $directCross.directCrossExecutionAllowedByDefault "Direct-cross allowed by default." "EXEC_SIM_R012_FAIL_DIRECT_CROSS_GUIDANCE_WEAKENED"
Require-False $directCross.directCrossIncludedInBacktestAuthorization "Direct-cross included in authorization." "EXEC_SIM_R012_FAIL_DIRECT_CROSS_GUIDANCE_WEAKENED"
Require-False $directCross.guidanceWeakened "Direct-cross guidance weakened." "EXEC_SIM_R012_FAIL_DIRECT_CROSS_GUIDANCE_WEAKENED"

foreach ($policy in @("WakettPureLimitUntilClose", "WakettFiveMarketSlicesAroundClose", "PassiveUntilUrgency", "CloseSeeking15m", "CloseSeeking15mAdaptive", "ControlledResidualCross", "ImmediatePaperBenchmark", "TWAPBenchmarkOnly", "VWAPBenchmarkOnly", "ManualReview", "DoNotTrade")) {
    if (@($policies.expectedPoliciesForR013) -notcontains $policy) { Fail "Expected policy missing: $policy" "EXEC_SIM_R012_FAIL_BACKTEST_PREFLIGHT_MISSING" }
}
Require-False $policies.policyResultsProducedInR012 "Policy results produced in R012." "EXEC_SIM_R012_FAIL_TCA_RESULTS_PRODUCED"
foreach ($report in @("policy comparison", "median slippage ranking", "p95 slippage ranking", "fill ratio ranking", "residual ranking", "spread paid ranking")) {
    if (@($reports.expectedTcaReportsForR013) -notcontains $report) { Fail "Expected TCA report missing: $report" "EXEC_SIM_R012_FAIL_BACKTEST_PREFLIGHT_MISSING" }
}
Require-False $reports.tcaReportsProducedInR012 "TCA reports produced in R012." "EXEC_SIM_R012_FAIL_TCA_RESULTS_PRODUCED"

if ($cost.bestCaseMajorTargetUsdPerMillion -ne 5) { Fail "Best-case target is not 5 USD/million." "EXEC_SIM_R012_FAIL_5USD_PER_MILLION_UNIVERSALIZED" }
Require-True $cost.fiveUsdPerMillionBestCaseOnly "5 USD/million not best-case only." "EXEC_SIM_R012_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-False $cost.fiveUsdPerMillionUniversalized "5 USD/million universalized." "EXEC_SIM_R012_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-True $cost.nonMajorEmScandiCnhRequireLiquidityCalibration "Non-major calibration guidance missing." "EXEC_SIM_R012_FAIL_5USD_PER_MILLION_UNIVERSALIZED"

Require-False $noBacktest.backtestExecuted "Backtest executed." "EXEC_SIM_R012_FAIL_BACKTEST_EXECUTED"
Require-False $noBacktest.importedQuoteTcaBacktestExecuted "Imported quote TCA backtest executed." "EXEC_SIM_R012_FAIL_BACKTEST_EXECUTED"
Require-False $noBacktest.importedQuoteBacktestOutputProduced "Imported quote backtest output produced." "EXEC_SIM_R012_FAIL_BACKTEST_EXECUTED"
Require-False $noTca.tcaPolicyResultsProduced "TCA policy results produced." "EXEC_SIM_R012_FAIL_TCA_RESULTS_PRODUCED"
Require-False $noTca.importedQuoteTcaPolicyResultsProduced "Imported quote TCA policy results produced." "EXEC_SIM_R012_FAIL_TCA_RESULTS_PRODUCED"
Require-False $noLines.simulationResultLinesProduced "Simulation result lines produced." "EXEC_SIM_R012_FAIL_TCA_RESULTS_PRODUCED"
Require-False $api.polygonApiCalled "Polygon API called." "EXEC_SIM_R012_FAIL_API_CALL_DETECTED"
Require-False $api.lmaxCalled "LMAX called." "EXEC_SIM_R012_FAIL_API_CALL_DETECTED"
Require-False $api.externalApiCalled "External API called." "EXEC_SIM_R012_FAIL_API_CALL_DETECTED"
Require-False $runtime.brokerActivationDetected "Broker activation detected." "EXEC_SIM_R012_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.socketOpened "Socket opened." "EXEC_SIM_R012_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.tlsOpened "TLS opened." "EXEC_SIM_R012_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.fixOpened "FIX opened." "EXEC_SIM_R012_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataRequestSent "MarketDataRequest sent." "EXEC_SIM_R012_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataResponseRead "MarketDataResponse read." "EXEC_SIM_R012_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.schedulerServiceTimerPollingBackgroundJobIntroduced "Scheduler/service introduced." "EXEC_SIM_R012_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $order.ordersCreated "Orders created." "EXEC_SIM_R012_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.executableOrdersCreated "Executable orders created." "EXEC_SIM_R012_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.fillsCreated "Fills created." "EXEC_SIM_R012_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $order.executionReportsCreated "Execution reports created." "EXEC_SIM_R012_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $order.routesCreated "Routes created." "EXEC_SIM_R012_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.submissionsCreated "Submissions created." "EXEC_SIM_R012_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

Require-True $usdjpy.caveatPreserved "USDJPY caveat not preserved." "EXEC_SIM_R012_FAIL_USDJPY_CAVEAT_WEAKENED"
if ($usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8" -or $usdjpy.requiresInversion -ne $true) {
    Fail "USDJPY caveat/inversion weakened." "EXEC_SIM_R012_FAIL_USDJPY_CAVEAT_WEAKENED"
}
if ($lmax.audusdStatus -notmatch "not failed") { Fail "AUDUSD incorrectly marked failed." "EXEC_SIM_R012_FAIL_AUDUSD_MISCLASSIFIED" }
Require-False $lmax.lmaxCalledInR012 "LMAX called in R012." "EXEC_SIM_R012_FAIL_API_CALL_DETECTED"

Require-False $noExternal.polygonApiCalled "No-external audit shows Polygon call." "EXEC_SIM_R012_FAIL_API_CALL_DETECTED"
Require-False $noExternal.lmaxCalled "No-external audit shows LMAX call." "EXEC_SIM_R012_FAIL_API_CALL_DETECTED"
Require-False $noExternal.externalApiCalled "No-external audit shows external API call." "EXEC_SIM_R012_FAIL_API_CALL_DETECTED"
Require-False $noExternal.backtestExecuted "No-external audit shows backtest." "EXEC_SIM_R012_FAIL_BACKTEST_EXECUTED"
Require-False $noExternal.tcaPolicyResultsProduced "No-external audit shows TCA policy results." "EXEC_SIM_R012_FAIL_TCA_RESULTS_PRODUCED"
Require-False $noExternal.simulationResultLinesProduced "No-external audit shows simulation result lines." "EXEC_SIM_R012_FAIL_TCA_RESULTS_PRODUCED"
Require-False $noExternal.ordersFillsReportsRoutesSubmissionsCreated "No-external audit shows order-domain artifact." "EXEC_SIM_R012_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $forbidden.forbiddenActionsDetected "Forbidden action detected." "EXEC_SIM_R012_FAIL_NEW_EXTERNAL_ACTION_DETECTED"

if ($evidence.dotnetBuildNoRestore -ne "PASS") { Fail "dotnet build evidence is not PASS." "EXEC_SIM_R012_FAIL_BUILD_OR_TESTS" }
if ($evidence.focusedTests -notmatch "^PASS") { Fail "Focused R012 test evidence is not PASS." "EXEC_SIM_R012_FAIL_BUILD_OR_TESTS" }
if ($evidence.unitTests -notmatch "^PASS") { Fail "Unit test evidence is not PASS." "EXEC_SIM_R012_FAIL_BUILD_OR_TESTS" }

Write-Host "EXEC_SIM_R012_PASS_FIRST_REAL_OFFLINE_BACKTEST_AUTHORIZATION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R012_PASS_BACKTEST_PREFLIGHT_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R012_PASS_NO_BACKTEST_EXECUTION_GATE_READY_NO_EXTERNAL"
exit 0
