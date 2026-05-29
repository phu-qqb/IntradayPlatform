param(
    [string]$ArtifactsDir = "artifacts/readiness/execution-sim",
    [string]$SourcePath = "src/QQ.Production.Intraday.Application/ExecutionSimCloseSeekingFoundation.cs"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message, [string]$Classification) {
    Write-Error "$Classification`: $Message"
    exit 1
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing artifact: $Path" "EXEC_SIM_R008_FAIL_BUILD_OR_TESTS"
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
    "phase-exec-sim-r008-summary.md",
    "phase-exec-sim-r008-backtest-dry-run-contract.json",
    "phase-exec-sim-r008-backtest-dry-run-result.json",
    "phase-exec-sim-r008-accepted-manifests-used.json",
    "phase-exec-sim-r008-quarantined-manifests-excluded.json",
    "phase-exec-sim-r008-imported-quote-fixtures-used.json",
    "phase-exec-sim-r008-quote-windows-created.json",
    "phase-exec-sim-r008-close-benchmarks-created.json",
    "phase-exec-sim-r008-feed-quality-results.json",
    "phase-exec-sim-r008-policy-results.json",
    "phase-exec-sim-r008-tca-reports.json",
    "phase-exec-sim-r008-per-instrument-eurusd-report.json",
    "phase-exec-sim-r008-per-instrument-usdjpy-report.json",
    "phase-exec-sim-r008-per-instrument-audusd-report.json",
    "phase-exec-sim-r008-policy-comparison-report.json",
    "phase-exec-sim-r008-policy-ranking-median-slippage.json",
    "phase-exec-sim-r008-policy-ranking-p95-slippage.json",
    "phase-exec-sim-r008-policy-ranking-fill-ratio.json",
    "phase-exec-sim-r008-policy-ranking-residual.json",
    "phase-exec-sim-r008-policy-ranking-spread-paid.json",
    "phase-exec-sim-r008-wakett-limit-baseline-report.json",
    "phase-exec-sim-r008-wakett-five-market-slices-report.json",
    "phase-exec-sim-r008-close-seeking-adaptive-report.json",
    "phase-exec-sim-r008-controlled-residual-cross-report.json",
    "phase-exec-sim-r008-direct-cross-exclusion-evidence.json",
    "phase-exec-sim-r008-major-pair-5usd-bestcase-only.json",
    "phase-exec-sim-r008-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r008-no-polygon-api-call-audit.json",
    "phase-exec-sim-r008-no-lmax-call-audit.json",
    "phase-exec-sim-r008-no-external-api-call-audit.json",
    "phase-exec-sim-r008-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r008-no-real-fill-audit.json",
    "phase-exec-sim-r008-no-execution-report-audit.json",
    "phase-exec-sim-r008-no-order-created-audit.json",
    "phase-exec-sim-r008-no-route-no-submission-audit.json",
    "phase-exec-sim-r008-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r008-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r008-no-external-audit.json",
    "phase-exec-sim-r008-forbidden-actions-audit.json",
    "phase-exec-sim-r008-next-phase-recommendation.json",
    "phase-exec-sim-r008-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    $path = Join-Path $ArtifactsDir $artifact
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Required R008 artifact is missing: $artifact" "EXEC_SIM_R008_FAIL_BUILD_OR_TESTS"
    }
}

$contract = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-backtest-dry-run-contract.json")
$result = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-backtest-dry-run-result.json")
$accepted = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-accepted-manifests-used.json")
$quarantined = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-quarantined-manifests-excluded.json")
$fixtures = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-imported-quote-fixtures-used.json")
$windows = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-quote-windows-created.json")
$benchmarks = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-close-benchmarks-created.json")
$feeds = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-feed-quality-results.json")
$policy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-policy-results.json")
$tca = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-tca-reports.json")
$eurusd = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-per-instrument-eurusd-report.json")
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-per-instrument-usdjpy-report.json")
$audusd = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-per-instrument-audusd-report.json")
$comparison = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-policy-comparison-report.json")
$median = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-policy-ranking-median-slippage.json")
$p95 = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-policy-ranking-p95-slippage.json")
$fill = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-policy-ranking-fill-ratio.json")
$residual = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-policy-ranking-residual.json")
$spread = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-policy-ranking-spread-paid.json")
$wakettLimit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-wakett-limit-baseline-report.json")
$wakettFive = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-wakett-five-market-slices-report.json")
$adaptive = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-close-seeking-adaptive-report.json")
$controlled = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-controlled-residual-cross-report.json")
$directCross = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-direct-cross-exclusion-evidence.json")
$fiveUsd = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-major-pair-5usd-bestcase-only.json")
$nonMajor = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-nonmajor-calibration-preservation.json")
$polygonAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-no-polygon-api-call-audit.json")
$lmaxAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-no-lmax-call-audit.json")
$externalAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-no-external-api-call-audit.json")
$runtimeAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-no-broker-marketdata-runtime-audit.json")
$fillAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-no-real-fill-audit.json")
$reportAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-no-execution-report-audit.json")
$orderAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-no-order-created-audit.json")
$routeAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-no-route-no-submission-audit.json")
$usdjpyCaveat = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-usdjpy-caveat-preservation.json")
$lmaxBaseline = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-lmax-readonly-baseline-reference.json")
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r008-build-test-validator-evidence.json")

Require-True $contract.reusesR007AcceptedManifests "Contract does not reuse R007 accepted manifests." "EXEC_SIM_R008_FAIL_BACKTEST_DRYRUN_MISSING"
Require-True $contract.reusesR007QuarantinedManifests "Contract does not reuse R007 quarantined manifests." "EXEC_SIM_R008_FAIL_ACCEPTED_QUARANTINED_USAGE_MISSING"
Require-True $contract.reusesR004SanitizedImportPath "Contract does not reuse R004 import path." "EXEC_SIM_R008_FAIL_BACKTEST_DRYRUN_MISSING"
Require-True $contract.reusesR005ImportedQuoteTcaBacktestFlow "Contract does not reuse R005 TCA flow." "EXEC_SIM_R008_FAIL_TCA_REPORT_MISSING"
Require-True $contract.acceptedFilesOnly "Contract is not accepted-files-only." "EXEC_SIM_R008_FAIL_ACCEPTED_QUARANTINED_USAGE_MISSING"
Require-True $contract.quarantinedFilesExcluded "Contract does not exclude quarantined files." "EXEC_SIM_R008_FAIL_ACCEPTED_QUARANTINED_USAGE_MISSING"
Require-False $contract.polygonApiCalled "Polygon API call detected in contract." "EXEC_SIM_R008_FAIL_API_CALL_DETECTED"
Require-False $contract.lmaxCalled "LMAX call detected in contract." "EXEC_SIM_R008_FAIL_API_CALL_DETECTED"
Require-False $contract.externalApiCalled "External API call detected in contract." "EXEC_SIM_R008_FAIL_API_CALL_DETECTED"

if ($result.simulationStatus -ne "CompletedFixtureOnlyDryRun" -or $result.safetyStatus -ne "NoExternalNoRealFillNoOrder") {
    Fail "Backtest dry-run result status is not safe/completed." "EXEC_SIM_R008_FAIL_BACKTEST_DRYRUN_MISSING"
}
Require-False $result.quarantinedFilesFeedBacktest "Quarantined files feed the backtest." "EXEC_SIM_R008_FAIL_ACCEPTED_QUARANTINED_USAGE_MISSING"
Require-False $result.polygonApiCalled "Polygon API call detected in result." "EXEC_SIM_R008_FAIL_API_CALL_DETECTED"
Require-False $result.lmaxCalled "LMAX call detected in result." "EXEC_SIM_R008_FAIL_API_CALL_DETECTED"
Require-False $result.externalApiCalled "External API call detected in result." "EXEC_SIM_R008_FAIL_API_CALL_DETECTED"

foreach ($symbol in @("EURUSD", "USDJPY", "AUDUSD")) {
    if (-not (@($accepted.acceptedManifestsUsed) | Where-Object { $_.executionTradableSymbol -eq $symbol -and $_.usedInDryRun -eq $true })) {
        Fail "Accepted manifest not used for $symbol." "EXEC_SIM_R008_FAIL_ACCEPTED_QUARANTINED_USAGE_MISSING"
    }
    if (-not (@($fixtures.importedQuoteFixturesUsed) | Where-Object { $_.executionTradableSymbol -eq $symbol -and $_.fixtureOnly -eq $true })) {
        Fail "Imported quote fixture missing for $symbol." "EXEC_SIM_R008_FAIL_BACKTEST_DRYRUN_MISSING"
    }
    if (-not (@($windows.quoteWindowsCreated) | Where-Object { $_.executionTradableSymbol -eq $symbol })) {
        Fail "Quote window missing for $symbol." "EXEC_SIM_R008_FAIL_BACKTEST_DRYRUN_MISSING"
    }
    if (-not (@($benchmarks.closeBenchmarksCreated) | Where-Object { $_.executionTradableSymbol -eq $symbol -and $_.closeBenchmarkStatus -eq "Available" })) {
        Fail "Close benchmark missing for $symbol." "EXEC_SIM_R008_FAIL_BACKTEST_DRYRUN_MISSING"
    }
    if (-not (@($feeds.feedQualityResults) | Where-Object { $_.executionTradableSymbol -eq $symbol -and $_.feedQualityBucket -eq "Good" })) {
        Fail "Feed quality result missing for $symbol." "EXEC_SIM_R008_FAIL_BACKTEST_DRYRUN_MISSING"
    }
}

Require-False $quarantined.quarantinedFilesFeedBacktest "Quarantined usage artifact allows quarantined files into backtest." "EXEC_SIM_R008_FAIL_ACCEPTED_QUARANTINED_USAGE_MISSING"
Require-True $quarantined.directCrossExcluded "Direct cross not excluded." "EXEC_SIM_R008_FAIL_DIRECT_CROSS_EXCLUSION_MISSING"
Require-True $quarantined.missingConventionExcluded "Missing convention not excluded." "EXEC_SIM_R008_FAIL_ACCEPTED_QUARANTINED_USAGE_MISSING"
Require-True $quarantined.secretRiskExcluded "Secret-risk file not excluded." "EXEC_SIM_R008_FAIL_ACCEPTED_QUARANTINED_USAGE_MISSING"
Require-True $quarantined.rawPayloadRiskExcluded "Raw-payload-risk file not excluded." "EXEC_SIM_R008_FAIL_ACCEPTED_QUARANTINED_USAGE_MISSING"

foreach ($p in @("WakettPureLimitUntilClose", "WakettFiveMarketSlicesAroundClose", "CloseSeeking15mAdaptive", "ControlledResidualCross")) {
    if (-not (@($policy.policyResults) | Where-Object { $_.policy -eq $p })) {
        if ($p -match "Wakett") { Fail "Wakett baseline missing: $p" "EXEC_SIM_R008_FAIL_WAKETT_BASELINES_MISSING" }
        else { Fail "Close seeking result missing: $p" "EXEC_SIM_R008_FAIL_CLOSE_SEEKING_RESULT_MISSING" }
    }
}
if (@($tca.tcaReports).Count -lt 3 -or $tca.containsSlippageVsClose -ne $true -or $tca.containsSpreadPaid -ne $true) {
    Fail "TCA reports missing required metrics." "EXEC_SIM_R008_FAIL_TCA_REPORT_MISSING"
}
Require-True $tca.noRealFill "TCA report creates real fill." "EXEC_SIM_R008_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-True $tca.noExecutionReport "TCA report creates execution report." "EXEC_SIM_R008_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"

foreach ($report in @($eurusd, $usdjpy, $audusd)) {
    if ($report.closeBenchmarkStatus -ne "Available" -or $report.feedQualityBucket -ne "Good") {
        Fail "Per-instrument report is missing readiness status." "EXEC_SIM_R008_FAIL_TCA_REPORT_MISSING"
    }
}
if ($usdjpy.normalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.requiresInversion -ne $true) {
    Fail "USDJPY inversion context weakened in per-instrument report." "EXEC_SIM_R008_FAIL_USDJPY_CAVEAT_WEAKENED"
}
if ($audusd.audusdStatus -notmatch "inconclusive" -or ($audusd.audusdStatus -match "failed" -and $audusd.audusdStatus -notmatch "not failed")) {
    Fail "AUDUSD per-instrument report marks AUDUSD failed." "EXEC_SIM_R008_FAIL_AUDUSD_MISCLASSIFIED"
}

if (@($comparison.policiesCompared).Count -lt 11 -or $comparison.noRealFill -ne $true -or $comparison.noOrder -ne $true) {
    Fail "Policy comparison report missing or unsafe." "EXEC_SIM_R008_FAIL_TCA_REPORT_MISSING"
}
foreach ($ranking in @($median, $p95, $fill, $residual, $spread)) {
    if (@($ranking.ranking).Count -eq 0) {
        Fail "Policy ranking missing." "EXEC_SIM_R008_FAIL_TCA_REPORT_MISSING"
    }
}
Require-True $wakettLimit.appears "Wakett limit baseline missing." "EXEC_SIM_R008_FAIL_WAKETT_BASELINES_MISSING"
Require-True $wakettFive.appears "Wakett five-market-slices baseline missing." "EXEC_SIM_R008_FAIL_WAKETT_BASELINES_MISSING"
Require-True $adaptive.appears "CloseSeeking15mAdaptive report missing." "EXEC_SIM_R008_FAIL_CLOSE_SEEKING_RESULT_MISSING"
Require-True $controlled.appears "ControlledResidualCross report missing." "EXEC_SIM_R008_FAIL_CLOSE_SEEKING_RESULT_MISSING"
Require-True $controlled.opportunityCostExceedsSpreadCost "ControlledResidualCross lacks cost justification." "EXEC_SIM_R008_FAIL_CLOSE_SEEKING_RESULT_MISSING"
Require-False $directCross.directCrossExecutionAllowed "Direct-cross execution allowed." "EXEC_SIM_R008_FAIL_DIRECT_CROSS_EXCLUSION_MISSING"
Require-False $directCross.feedsDryRunBacktest "Direct-cross feeds dry run." "EXEC_SIM_R008_FAIL_DIRECT_CROSS_EXCLUSION_MISSING"
Require-False $directCross.policyResultsContainDirectCross "Policy results contain direct cross." "EXEC_SIM_R008_FAIL_DIRECT_CROSS_EXCLUSION_MISSING"

Require-True $fiveUsd.fiveUsdPerMillionBestCaseOnly "5 USD/million not best-case only." "EXEC_SIM_R008_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-False $fiveUsd.fiveUsdPerMillionUniversalized "5 USD/million universalized." "EXEC_SIM_R008_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-True $nonMajor.nonMajorCalibrationPreserved "Non-major calibration not preserved." "EXEC_SIM_R008_FAIL_5USD_PER_MILLION_UNIVERSALIZED"

Require-False $polygonAudit.polygonApiCalled "Polygon API call detected." "EXEC_SIM_R008_FAIL_API_CALL_DETECTED"
Require-False $polygonAudit.externalApiCalled "External API call detected in Polygon audit." "EXEC_SIM_R008_FAIL_API_CALL_DETECTED"
Require-False $polygonAudit.httpClientUsed "HTTP client usage detected." "EXEC_SIM_R008_FAIL_API_CALL_DETECTED"
Require-False $lmaxAudit.lmaxCalled "LMAX call detected." "EXEC_SIM_R008_FAIL_API_CALL_DETECTED"
Require-False $externalAudit.externalApiCalled "External API call detected." "EXEC_SIM_R008_FAIL_API_CALL_DETECTED"
Require-False $runtimeAudit.brokerActivationDetected "Broker activation detected." "EXEC_SIM_R008_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.socketOpened "Socket opened." "EXEC_SIM_R008_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.tlsOpened "TLS opened." "EXEC_SIM_R008_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.fixOpened "FIX opened." "EXEC_SIM_R008_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.marketDataRequestSent "MarketDataRequest sent." "EXEC_SIM_R008_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.marketDataResponseRead "MarketDataResponse read." "EXEC_SIM_R008_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.apiWorkerLiveGatewayEnabled "API/Worker live gateway enabled." "EXEC_SIM_R008_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.schedulerServiceTimerPollingBackgroundJobIntroduced "Scheduler/service/timer/polling/background job introduced." "EXEC_SIM_R008_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $runtimeAudit.automaticExecutionIntroduced "Automatic execution introduced." "EXEC_SIM_R008_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"

Require-False $fillAudit.realFillsCreated "Real fill created." "EXEC_SIM_R008_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $fillAudit.simulationResultLinesAreRealFills "Simulation lines are real fills." "EXEC_SIM_R008_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $reportAudit.executionReportsCreated "Execution report created." "EXEC_SIM_R008_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $reportAudit.brokerExecutionReportsCreated "Broker execution report created." "EXEC_SIM_R008_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $orderAudit.ordersCreated "Order created." "EXEC_SIM_R008_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $orderAudit.executableOrdersCreated "Executable order created." "EXEC_SIM_R008_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $routeAudit.routesCreated "Route created." "EXEC_SIM_R008_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $routeAudit.submissionsCreated "Submission created." "EXEC_SIM_R008_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

Require-True $usdjpyCaveat.caveatPreserved "USDJPY caveat not preserved." "EXEC_SIM_R008_FAIL_USDJPY_CAVEAT_WEAKENED"
if ($usdjpyCaveat.securityId -ne "4004" -or $usdjpyCaveat.securityIdSource -ne "8") {
    Fail "USDJPY SecurityID/SecurityIDSource weakened." "EXEC_SIM_R008_FAIL_USDJPY_CAVEAT_WEAKENED"
}
if ($lmaxBaseline.audusdStatus -notmatch "inconclusive" -or ($lmaxBaseline.audusdStatus -match "failed" -and $lmaxBaseline.audusdStatus -notmatch "not failed")) {
    Fail "AUDUSD incorrectly marked failed." "EXEC_SIM_R008_FAIL_AUDUSD_MISCLASSIFIED"
}
Require-False $lmaxBaseline.lmaxCalledInR008 "LMAX baseline was called in R008." "EXEC_SIM_R008_FAIL_API_CALL_DETECTED"

Require-False $noExternal.polygonApiCalled "No-external audit shows Polygon API call." "EXEC_SIM_R008_FAIL_API_CALL_DETECTED"
Require-False $noExternal.lmaxCalled "No-external audit shows LMAX call." "EXEC_SIM_R008_FAIL_API_CALL_DETECTED"
Require-False $noExternal.externalApiCalled "No-external audit shows external API call." "EXEC_SIM_R008_FAIL_API_CALL_DETECTED"
Require-False $noExternal.socketTlsFixMarketDataRuntimeDetected "No-external audit shows runtime market-data action." "EXEC_SIM_R008_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $noExternal.ordersFillsReportsRoutesSubmissionsCreated "No-external audit shows order/fill/report/route/submission." "EXEC_SIM_R008_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $forbidden.forbiddenActionsDetected "Forbidden action detected." "EXEC_SIM_R008_FAIL_NEW_EXTERNAL_ACTION_DETECTED"

if (-not (Test-Path -LiteralPath $SourcePath)) {
    Fail "Source path missing: $SourcePath" "EXEC_SIM_R008_FAIL_BUILD_OR_TESTS"
}
$source = Get-Content -LiteralPath $SourcePath -Raw
foreach ($token in @("HttpClient", "GetAsync", "PostAsync", "SendAsync", "WebSocket", "TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "FixSession", "ConnectAsync", "BackgroundService", "IHostedService", "PeriodicTimer")) {
    if ($source -match [regex]::Escape($token)) {
        Fail "Runtime/external action token detected in source: $token" "EXEC_SIM_R008_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    }
}

if ($evidence.dotnetBuildNoRestore -ne "PASS") {
    Fail "dotnet build --no-restore evidence is not PASS." "EXEC_SIM_R008_FAIL_BUILD_OR_TESTS"
}
if ($evidence.focusedTests -notmatch "^PASS") {
    Fail "Focused R008 test evidence is not PASS." "EXEC_SIM_R008_FAIL_BUILD_OR_TESTS"
}
if ($evidence.unitTests -notmatch "^PASS") {
    Fail "Unit test evidence is not PASS." "EXEC_SIM_R008_FAIL_BUILD_OR_TESTS"
}

Write-Host "EXEC_SIM_R008_PASS_OPERATOR_QUOTE_FILE_BACKTEST_DRYRUN_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R008_PASS_IMPORTED_QUOTE_TCA_DRYRUN_REPORT_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R008_PASS_ACCEPTED_QUARANTINED_FILE_USAGE_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R008_PASS_NO_API_NO_REAL_FILL_NO_ORDER_GATE_READY_NO_EXTERNAL"
exit 0
