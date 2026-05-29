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
        Fail "Missing artifact: $Path" "EXEC_SIM_R013_FAIL_BUILD_OR_TESTS"
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
    "phase-exec-sim-r013-summary.md",
    "phase-exec-sim-r013-backtest-execution-contract.json",
    "phase-exec-sim-r013-backtest-run-result.json",
    "phase-exec-sim-r013-accepted-files-used.json",
    "phase-exec-sim-r013-imported-quote-fixtures.json",
    "phase-exec-sim-r013-quote-windows.json",
    "phase-exec-sim-r013-close-benchmarks.json",
    "phase-exec-sim-r013-feed-quality-results.json",
    "phase-exec-sim-r013-policy-results.json",
    "phase-exec-sim-r013-tca-reports.json",
    "phase-exec-sim-r013-per-instrument-eurusd-report.json",
    "phase-exec-sim-r013-per-instrument-usdjpy-report.json",
    "phase-exec-sim-r013-per-instrument-audusd-report.json",
    "phase-exec-sim-r013-policy-comparison-report.json",
    "phase-exec-sim-r013-policy-ranking-median-slippage.json",
    "phase-exec-sim-r013-policy-ranking-p95-slippage.json",
    "phase-exec-sim-r013-policy-ranking-fill-ratio.json",
    "phase-exec-sim-r013-policy-ranking-residual.json",
    "phase-exec-sim-r013-policy-ranking-spread-paid.json",
    "phase-exec-sim-r013-wakett-limit-baseline-report.json",
    "phase-exec-sim-r013-wakett-five-market-slices-report.json",
    "phase-exec-sim-r013-passive-until-urgency-report.json",
    "phase-exec-sim-r013-close-seeking-15m-report.json",
    "phase-exec-sim-r013-close-seeking-adaptive-report.json",
    "phase-exec-sim-r013-controlled-residual-cross-report.json",
    "phase-exec-sim-r013-benchmark-only-policy-report.json",
    "phase-exec-sim-r013-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r013-major-pair-5usd-bestcase-only.json",
    "phase-exec-sim-r013-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r013-no-polygon-api-call-audit.json",
    "phase-exec-sim-r013-no-lmax-call-audit.json",
    "phase-exec-sim-r013-no-external-api-call-audit.json",
    "phase-exec-sim-r013-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r013-no-real-fill-audit.json",
    "phase-exec-sim-r013-no-execution-report-audit.json",
    "phase-exec-sim-r013-no-order-created-audit.json",
    "phase-exec-sim-r013-no-route-no-submission-audit.json",
    "phase-exec-sim-r013-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r013-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r013-no-external-audit.json",
    "phase-exec-sim-r013-forbidden-actions-audit.json",
    "phase-exec-sim-r013-next-phase-recommendation.json",
    "phase-exec-sim-r013-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsDir $artifact))) {
        Fail "Required R013 artifact is missing: $artifact" "EXEC_SIM_R013_FAIL_BUILD_OR_TESTS"
    }
}

$contract = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-backtest-execution-contract.json")
$run = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-backtest-run-result.json")
$accepted = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-accepted-files-used.json")
$fixtures = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-imported-quote-fixtures.json")
$windows = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-quote-windows.json")
$benchmarks = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-close-benchmarks.json")
$feed = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-feed-quality-results.json")
$policies = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-policy-results.json")
$tca = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-tca-reports.json")
$comparison = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-policy-comparison-report.json")
$limit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-wakett-limit-baseline-report.json")
$slices = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-wakett-five-market-slices-report.json")
$adaptive = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-close-seeking-adaptive-report.json")
$controlled = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-controlled-residual-cross-report.json")
$benchmarkOnly = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-benchmark-only-policy-report.json")
$directCross = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-direct-cross-exclusion-preservation.json")
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-major-pair-5usd-bestcase-only.json")
$nonMajor = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-nonmajor-calibration-preservation.json")
$api = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-no-external-api-call-audit.json")
$polygon = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-no-polygon-api-call-audit.json")
$lmaxAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-no-lmax-call-audit.json")
$runtime = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-no-broker-marketdata-runtime-audit.json")
$fillAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-no-real-fill-audit.json")
$reportAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-no-execution-report-audit.json")
$orderAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-no-order-created-audit.json")
$routeAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-no-route-no-submission-audit.json")
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-usdjpy-caveat-preservation.json")
$lmax = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-lmax-readonly-baseline-reference.json")
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r013-build-test-validator-evidence.json")

Require-True $contract.noExternal "Contract is not no-external." "EXEC_SIM_R013_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-True $contract.usesAcceptedOfflineQuoteFiles "Contract does not use accepted files." "EXEC_SIM_R013_FAIL_BACKTEST_RESULT_MISSING"
Require-True $contract.importsIntoSanitizedFixtureOnlyQuoteWindows "Contract does not import fixture-only windows." "EXEC_SIM_R013_FAIL_BACKTEST_RESULT_MISSING"
Require-True $contract.resultsFixtureOnly "Results are not fixture-only." "EXEC_SIM_R013_FAIL_BACKTEST_RESULT_MISSING"
Require-True $contract.resultsPaperOnly "Results are not paper-only." "EXEC_SIM_R013_FAIL_BACKTEST_RESULT_MISSING"
Require-True $contract.resultsNonExecutable "Results are not non-executable." "EXEC_SIM_R013_FAIL_BACKTEST_RESULT_MISSING"
Require-False $contract.simulationResultLinesAreFills "Simulation result lines are represented as fills." "EXEC_SIM_R013_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $contract.fillEntitiesCreated "Fill entities created." "EXEC_SIM_R013_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $contract.executionReportEntitiesCreated "Execution report entities created." "EXEC_SIM_R013_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $contract.ordersCreated "Orders created." "EXEC_SIM_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $contract.routesCreated "Routes created." "EXEC_SIM_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $contract.submissionsCreated "Submissions created." "EXEC_SIM_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

if ($run.SimulationStatus -ne "CompletedFixtureOnlyPaperOnly") { Fail "Backtest run status is not completed fixture-only." "EXEC_SIM_R013_FAIL_BACKTEST_RESULT_MISSING" }
if ($run.SafetyStatus -ne "NoExternalNoRealFillNoOrder") { Fail "Backtest safety status is not clean." "EXEC_SIM_R013_FAIL_BACKTEST_RESULT_MISSING" }
Require-True $run.NoApiCall "Backtest result indicates API call." "EXEC_SIM_R013_FAIL_API_CALL_DETECTED"
Require-True $run.NoOrderDomainOutput "Backtest result indicates order-domain output." "EXEC_SIM_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

$expectedRows = @{ EURUSD = 54694; USDJPY = 59368; AUDUSD = 60656 }
foreach ($symbol in @("EURUSD", "USDJPY", "AUDUSD")) {
    $entry = @($accepted.acceptedFilesUsed) | Where-Object { $_.ExecutionTradableSymbol -eq $symbol } | Select-Object -First 1
    if ($null -eq $entry) { Fail "Accepted file missing for $symbol." "EXEC_SIM_R013_FAIL_BACKTEST_RESULT_MISSING" }
    if ($entry.Rows -ne $expectedRows[$symbol] -or $entry.ValidationStatus -ne "AcceptedForSanitizedImport") {
        Fail "Accepted file invalid for $symbol." "EXEC_SIM_R013_FAIL_BACKTEST_RESULT_MISSING"
    }
    $fixture = @($fixtures.importedQuoteFixtures) | Where-Object { $_.ExecutionTradableSymbol -eq $symbol } | Select-Object -First 1
    if ($null -eq $fixture -or $fixture.RowsImportedIntoFixtureWindow -le 0) { Fail "Imported quote fixture missing for $symbol." "EXEC_SIM_R013_FAIL_BACKTEST_RESULT_MISSING" }
    Require-True $fixture.FixtureOnly "Fixture row is not fixture-only for $symbol." "EXEC_SIM_R013_FAIL_BACKTEST_RESULT_MISSING"
    Require-True $fixture.PaperOnly "Fixture row is not paper-only for $symbol." "EXEC_SIM_R013_FAIL_BACKTEST_RESULT_MISSING"
    Require-False $fixture.RawPayloadSerialized "Raw payload serialized for $symbol." "EXEC_SIM_R013_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    $window = @($windows.quoteWindows) | Where-Object { $_.ExecutionTradableSymbol -eq $symbol } | Select-Object -First 1
    if ($null -eq $window -or $window.FeedWindowStatus -ne "Ready") { Fail "Quote window missing/not ready for $symbol." "EXEC_SIM_R013_FAIL_TCA_REPORT_MISSING" }
    $benchmark = @($benchmarks.closeBenchmarks) | Where-Object { $_.ExecutionTradableSymbol -eq $symbol } | Select-Object -First 1
    if ($null -eq $benchmark -or $benchmark.CloseBenchmarkStatus -ne "Available") { Fail "Close benchmark missing for $symbol." "EXEC_SIM_R013_FAIL_TCA_REPORT_MISSING" }
    $feedResult = @($feed.feedQualityResults) | Where-Object { $_.ExecutionTradableSymbol -eq $symbol } | Select-Object -First 1
    if ($null -eq $feedResult -or $feedResult.FeedQualityBucket -ne "Good") { Fail "Feed quality missing for $symbol." "EXEC_SIM_R013_FAIL_TCA_REPORT_MISSING" }
}
Require-False $accepted.quarantinedFilesUsed "Quarantined files used." "EXEC_SIM_R013_FAIL_BACKTEST_RESULT_MISSING"
Require-False $accepted.directCrossFilesUsed "Direct-cross files used." "EXEC_SIM_R013_FAIL_DIRECT_CROSS_GUIDANCE_WEAKENED"
Require-True $fixtures.sanitizedFixtureOnlyQuoteWindowsCreated "Sanitized fixture-only quote windows missing." "EXEC_SIM_R013_FAIL_BACKTEST_RESULT_MISSING"
Require-False $fixtures.orderDomainEntitiesCreated "Order-domain entities created by fixture import." "EXEC_SIM_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

foreach ($policy in @("WakettPureLimitUntilClose", "WakettFiveMarketSlicesAroundClose", "PassiveUntilUrgency", "CloseSeeking15m", "CloseSeeking15mAdaptive", "ControlledResidualCross", "ImmediatePaperBenchmark", "TWAPBenchmarkOnly", "VWAPBenchmarkOnly", "ManualReview", "DoNotTrade")) {
    $row = @($policies.policyResults) | Where-Object { $_.Policy -eq $policy } | Select-Object -First 1
    if ($null -eq $row) { Fail "Policy result missing: $policy" "EXEC_SIM_R013_FAIL_TCA_REPORT_MISSING" }
    Require-True $row.FixtureOnly "$policy result not fixture-only." "EXEC_SIM_R013_FAIL_TCA_REPORT_MISSING"
    Require-True $row.PaperOnly "$policy result not paper-only." "EXEC_SIM_R013_FAIL_TCA_REPORT_MISSING"
    Require-True $row.NonExecutable "$policy result not non-executable." "EXEC_SIM_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    Require-True $row.NotAnOrder "$policy result not marked NotAnOrder." "EXEC_SIM_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    Require-True $row.NoRealFill "$policy result not marked NoRealFill." "EXEC_SIM_R013_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
    Require-True $row.NoExecutionReport "$policy result not marked NoExecutionReport." "EXEC_SIM_R013_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
}
Require-False $policies.simulationResultLinesAreFills "Simulation result lines are fills." "EXEC_SIM_R013_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $policies.fillEntitiesCreated "Fill entities created." "EXEC_SIM_R013_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $policies.executionReportEntitiesCreated "Execution report entities created." "EXEC_SIM_R013_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"

if ($limit.BaselineType -ne "NegativeBaseline" -or $limit.ResidualAtClose -le 0.3 -or $limit.EstimatedNonFillCost -ne "High") {
    Fail "Wakett limit baseline does not show residual/non-fill cost." "EXEC_SIM_R013_FAIL_WAKETT_BASELINES_MISSING"
}
if ($slices.BaselineType -ne "NegativeBaseline" -or $slices.RepeatedSpreadCrossing -ne $true -or $slices.SpreadPaidBps -le 0.5) {
    Fail "Wakett five-market-slices baseline does not show repeated spread crossing." "EXEC_SIM_R013_FAIL_WAKETT_BASELINES_MISSING"
}
if ($adaptive.Policy -ne "CloseSeeking15mAdaptive") { Fail "CloseSeeking15mAdaptive report missing." "EXEC_SIM_R013_FAIL_CLOSE_SEEKING_RESULT_MISSING" }
if ($controlled.Policy -ne "ControlledResidualCross" -or $controlled.OpportunityCostExceedsCrossingCostRequired -ne $true) {
    Fail "ControlledResidualCross report missing or weakened." "EXEC_SIM_R013_FAIL_CLOSE_SEEKING_RESULT_MISSING"
}
Require-True $benchmarkOnly.benchmarkOnly "Benchmark-only policy report missing benchmark-only flag." "EXEC_SIM_R013_FAIL_TCA_REPORT_MISSING"
Require-True $benchmarkOnly.NonExecutable "Benchmark-only policy report executable." "EXEC_SIM_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

Require-True $tca.FixtureOnly "TCA reports not fixture-only." "EXEC_SIM_R013_FAIL_TCA_REPORT_MISSING"
Require-True $tca.PaperOnly "TCA reports not paper-only." "EXEC_SIM_R013_FAIL_TCA_REPORT_MISSING"
Require-True $tca.NonExecutable "TCA reports not non-executable." "EXEC_SIM_R013_FAIL_TCA_REPORT_MISSING"
if (@($tca.reportsProduced) -notcontains "policy comparison") { Fail "Policy comparison report missing." "EXEC_SIM_R013_FAIL_TCA_REPORT_MISSING" }
foreach ($ranking in @("phase-exec-sim-r013-policy-ranking-median-slippage.json", "phase-exec-sim-r013-policy-ranking-p95-slippage.json", "phase-exec-sim-r013-policy-ranking-fill-ratio.json", "phase-exec-sim-r013-policy-ranking-residual.json", "phase-exec-sim-r013-policy-ranking-spread-paid.json")) {
    $rankingJson = Read-Json (Join-Path $ArtifactsDir $ranking)
    $rankingRows = @($rankingJson.ranking)
    if ($rankingRows.Count -eq 0) { $rankingRows = @($rankingJson.rankings) }
    if ($rankingRows.Count -eq 0) { Fail "Policy ranking missing in $ranking." "EXEC_SIM_R013_FAIL_POLICY_RANKINGS_MISSING" }
}
if ($comparison.BestOverallTradeoffPolicy -ne "CloseSeeking15mAdaptive") { Fail "Policy comparison missing adaptive best-tradeoff conclusion." "EXEC_SIM_R013_FAIL_TCA_REPORT_MISSING" }

if ($cost.bestCaseMajorTargetUsdPerMillion -ne 5) { Fail "Best-case target is not 5 USD/million." "EXEC_SIM_R013_FAIL_5USD_PER_MILLION_UNIVERSALIZED" }
Require-True $cost.fiveUsdPerMillionBestCaseOnly "5 USD/million not best-case only." "EXEC_SIM_R013_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-False $cost.fiveUsdPerMillionUniversalized "5 USD/million universalized." "EXEC_SIM_R013_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-True $nonMajor.nonMajorEmScandiCnhRequireLiquidityCalibration "Non-major calibration guidance missing." "EXEC_SIM_R013_FAIL_5USD_PER_MILLION_UNIVERSALIZED"

if ($directCross.executionUniverse -ne "USD-pair-only") { Fail "Execution universe weakened." "EXEC_SIM_R013_FAIL_DIRECT_CROSS_GUIDANCE_WEAKENED" }
Require-False $directCross.directCrossExecutionAllowedByDefault "Direct-cross allowed by default." "EXEC_SIM_R013_FAIL_DIRECT_CROSS_GUIDANCE_WEAKENED"
Require-False $directCross.directCrossIncludedInBacktest "Direct-cross included in backtest." "EXEC_SIM_R013_FAIL_DIRECT_CROSS_GUIDANCE_WEAKENED"
Require-False $directCross.guidanceWeakened "Direct-cross guidance weakened." "EXEC_SIM_R013_FAIL_DIRECT_CROSS_GUIDANCE_WEAKENED"

Require-False $api.externalApiCalled "External API called." "EXEC_SIM_R013_FAIL_API_CALL_DETECTED"
Require-False $api.polygonApiCalled "Polygon API called." "EXEC_SIM_R013_FAIL_API_CALL_DETECTED"
Require-False $api.lmaxCalled "LMAX called." "EXEC_SIM_R013_FAIL_API_CALL_DETECTED"
Require-False $polygon.polygonApiCalled "Polygon API audit failed." "EXEC_SIM_R013_FAIL_API_CALL_DETECTED"
Require-False $lmaxAudit.lmaxCalled "LMAX audit failed." "EXEC_SIM_R013_FAIL_API_CALL_DETECTED"
Require-False $runtime.brokerActivationDetected "Broker activation detected." "EXEC_SIM_R013_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.socketOpened "Socket opened." "EXEC_SIM_R013_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.tlsOpened "TLS opened." "EXEC_SIM_R013_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.fixOpened "FIX opened." "EXEC_SIM_R013_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataRequestSent "MarketDataRequest sent." "EXEC_SIM_R013_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataResponseRead "MarketDataResponse read." "EXEC_SIM_R013_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.schedulerServiceTimerPollingBackgroundJobIntroduced "Scheduler/service introduced." "EXEC_SIM_R013_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $fillAudit.realFillsCreated "Real fills created." "EXEC_SIM_R013_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $fillAudit.fillEntitiesCreated "Fill entities created." "EXEC_SIM_R013_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $reportAudit.executionReportEntitiesCreated "Execution report entities created." "EXEC_SIM_R013_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $reportAudit.brokerExecutionReportsCreated "Broker execution reports created." "EXEC_SIM_R013_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $orderAudit.ordersCreated "Orders created." "EXEC_SIM_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $orderAudit.executableOrdersCreated "Executable orders created." "EXEC_SIM_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $routeAudit.routesCreated "Routes created." "EXEC_SIM_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $routeAudit.submissionsCreated "Submissions created." "EXEC_SIM_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

Require-True $usdjpy.caveatPreserved "USDJPY caveat not preserved." "EXEC_SIM_R013_FAIL_USDJPY_CAVEAT_WEAKENED"
if ($usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8" -or $usdjpy.requiresInversion -ne $true) {
    Fail "USDJPY caveat/inversion weakened." "EXEC_SIM_R013_FAIL_USDJPY_CAVEAT_WEAKENED"
}
if ($lmax.audusdStatus -notmatch "not failed") { Fail "AUDUSD incorrectly marked failed." "EXEC_SIM_R013_FAIL_AUDUSD_MISCLASSIFIED" }
Require-False $lmax.lmaxCalledInR013 "LMAX called in R013." "EXEC_SIM_R013_FAIL_API_CALL_DETECTED"

Require-False $noExternal.polygonApiCalled "No-external audit shows Polygon call." "EXEC_SIM_R013_FAIL_API_CALL_DETECTED"
Require-False $noExternal.lmaxCalled "No-external audit shows LMAX call." "EXEC_SIM_R013_FAIL_API_CALL_DETECTED"
Require-False $noExternal.externalApiCalled "No-external audit shows external API call." "EXEC_SIM_R013_FAIL_API_CALL_DETECTED"
Require-False $noExternal.brokerActivationDetected "No-external audit shows broker activation." "EXEC_SIM_R013_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $noExternal.socketTlsFixMarketDataRuntimeDetected "No-external audit shows runtime action." "EXEC_SIM_R013_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $noExternal.ordersFillsReportsRoutesSubmissionsCreated "No-external audit shows order-domain artifact." "EXEC_SIM_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.realFillsCreated "No-external audit shows real fills." "EXEC_SIM_R013_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $noExternal.executionReportEntitiesCreated "No-external audit shows execution report entities." "EXEC_SIM_R013_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $noExternal.liveBrokerProductionTradingStateMutated "No-external audit shows state mutation." "EXEC_SIM_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.paperLedgerStateCommitted "No-external audit shows paper ledger commit." "EXEC_SIM_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $forbidden.forbiddenActionsDetected "Forbidden action detected." "EXEC_SIM_R013_FAIL_NEW_EXTERNAL_ACTION_DETECTED"

if ($evidence.dotnetBuildNoRestore -ne "PASS") { Fail "dotnet build evidence is not PASS." "EXEC_SIM_R013_FAIL_BUILD_OR_TESTS" }
if ($evidence.focusedTests -notmatch "^PASS") { Fail "Focused R013 test evidence is not PASS." "EXEC_SIM_R013_FAIL_BUILD_OR_TESTS" }
if ($evidence.unitTests -notmatch "^PASS") { Fail "Unit test evidence is not PASS." "EXEC_SIM_R013_FAIL_BUILD_OR_TESTS" }

Write-Host "EXEC_SIM_R013_PASS_FIRST_REAL_OFFLINE_TCA_BACKTEST_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R013_PASS_WAKETT_VS_CLOSE_SEEKING_REAL_OFFLINE_REPORT_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R013_PASS_IMPORTED_QUOTE_POLICY_RANKINGS_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R013_PASS_NO_REAL_FILL_NO_ORDER_TCA_GATE_READY_NO_EXTERNAL"
exit 0
