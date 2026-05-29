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
        Fail "Missing artifact: $Path" "EXEC_SIM_R005_FAIL_BUILD_OR_TESTS"
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

function Require-Contains($Collection, [string]$Value, [string]$Message, [string]$Classification) {
    if (@($Collection) -notcontains $Value) {
        Fail $Message $Classification
    }
}

$requiredArtifacts = @(
    "phase-exec-sim-r005-summary.md",
    "phase-exec-sim-r005-imported-fixture-simulation-contract.json",
    "phase-exec-sim-r005-imported-quote-windows.json",
    "phase-exec-sim-r005-close-benchmarks-from-imported-quotes.json",
    "phase-exec-sim-r005-feed-quality-from-imported-quotes.json",
    "phase-exec-sim-r005-imported-fixture-policy-results.json",
    "phase-exec-sim-r005-policy-comparison-report.json",
    "phase-exec-sim-r005-policy-ranking-median-slippage.json",
    "phase-exec-sim-r005-policy-ranking-p95-slippage.json",
    "phase-exec-sim-r005-policy-ranking-fill-ratio.json",
    "phase-exec-sim-r005-policy-ranking-residual.json",
    "phase-exec-sim-r005-policy-ranking-spread-paid.json",
    "phase-exec-sim-r005-worst-imported-fixture-scenarios-by-policy.json",
    "phase-exec-sim-r005-wakett-limit-imported-fixture-result.json",
    "phase-exec-sim-r005-wakett-five-market-slices-imported-fixture-result.json",
    "phase-exec-sim-r005-close-seeking-adaptive-imported-fixture-result.json",
    "phase-exec-sim-r005-controlled-residual-cross-imported-fixture-result.json",
    "phase-exec-sim-r005-direct-cross-blocking-evidence.json",
    "phase-exec-sim-r005-feed-quality-blocking-report.json",
    "phase-exec-sim-r005-spread-regime-report.json",
    "phase-exec-sim-r005-residual-risk-report.json",
    "phase-exec-sim-r005-quote-gap-staleness-report.json",
    "phase-exec-sim-r005-major-pair-5usd-bestcase-only.json",
    "phase-exec-sim-r005-nonmajor-em-cnh-calibration-required.json",
    "phase-exec-sim-r005-no-polygon-api-call-audit.json",
    "phase-exec-sim-r005-no-lmax-call-audit.json",
    "phase-exec-sim-r005-no-real-fill-audit.json",
    "phase-exec-sim-r005-no-execution-report-audit.json",
    "phase-exec-sim-r005-no-order-created-audit.json",
    "phase-exec-sim-r005-no-route-no-submission-audit.json",
    "phase-exec-sim-r005-lineage-preservation.json",
    "phase-exec-sim-r005-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r005-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r005-no-external-audit.json",
    "phase-exec-sim-r005-forbidden-actions-audit.json",
    "phase-exec-sim-r005-next-phase-recommendation.json",
    "phase-exec-sim-r005-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    $path = Join-Path $ArtifactsDir $artifact
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Required R005 artifact is missing: $artifact" "EXEC_SIM_R005_FAIL_BUILD_OR_TESTS"
    }
}

$contract = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-imported-fixture-simulation-contract.json")
$windows = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-imported-quote-windows.json")
$benchmarks = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-close-benchmarks-from-imported-quotes.json")
$feed = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-feed-quality-from-imported-quotes.json")
$policyResults = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-imported-fixture-policy-results.json")
$comparison = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-policy-comparison-report.json")
$median = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-policy-ranking-median-slippage.json")
$p95 = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-policy-ranking-p95-slippage.json")
$fill = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-policy-ranking-fill-ratio.json")
$residual = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-policy-ranking-residual.json")
$spread = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-policy-ranking-spread-paid.json")
$worst = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-worst-imported-fixture-scenarios-by-policy.json")
$wakettLimit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-wakett-limit-imported-fixture-result.json")
$wakettSlices = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-wakett-five-market-slices-imported-fixture-result.json")
$adaptive = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-close-seeking-adaptive-imported-fixture-result.json")
$controlled = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-controlled-residual-cross-imported-fixture-result.json")
$directCross = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-direct-cross-blocking-evidence.json")
$feedBlocks = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-feed-quality-blocking-report.json")
$majorCost = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-major-pair-5usd-bestcase-only.json")
$nonMajor = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-nonmajor-em-cnh-calibration-required.json")
$polygonAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-no-polygon-api-call-audit.json")
$lmaxAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-no-lmax-call-audit.json")
$fillAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-no-real-fill-audit.json")
$reportAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-no-execution-report-audit.json")
$orderAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-no-order-created-audit.json")
$routeAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-no-route-no-submission-audit.json")
$lineage = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-lineage-preservation.json")
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-usdjpy-caveat-preservation.json")
$lmaxBaseline = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-lmax-readonly-baseline-reference.json")
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r005-build-test-validator-evidence.json")

Require-True $contract.usesImportedSanitizedQuoteFixtures "Imported fixture simulation contract does not use imported sanitized quote fixtures." "EXEC_SIM_R005_FAIL_IMPORTED_FIXTURE_BACKTEST_MISSING"
Require-False $contract.usesSyntheticOnlyPaths "R005 is marked synthetic-only." "EXEC_SIM_R005_FAIL_IMPORTED_FIXTURE_BACKTEST_MISSING"
foreach ($policy in @("WakettPureLimitUntilClose", "WakettFiveMarketSlicesAroundClose", "CloseSeeking15mAdaptive", "ControlledResidualCross", "ManualReview", "DoNotTrade")) {
    Require-Contains $contract.policiesCompared $policy "Simulation contract is missing policy $policy." "EXEC_SIM_R005_FAIL_IMPORTED_FIXTURE_BACKTEST_MISSING"
}
Require-False $contract.polygonApiCalled "Polygon API call detected in contract." "EXEC_SIM_R005_FAIL_API_CALL_DETECTED"
Require-False $contract.lmaxCalled "LMAX call detected in contract." "EXEC_SIM_R005_FAIL_API_CALL_DETECTED"
Require-False $contract.externalApiCalled "External API call detected in contract." "EXEC_SIM_R005_FAIL_API_CALL_DETECTED"

if (@($windows.importedQuoteWindows).Count -lt 5) {
    Fail "Imported quote windows are missing required scenarios." "EXEC_SIM_R005_FAIL_IMPORTED_FIXTURE_BACKTEST_MISSING"
}
if (@($benchmarks.closeBenchmarks).Count -lt 5) {
    Fail "Close benchmarks from imported quotes are missing." "EXEC_SIM_R005_FAIL_TCA_REPORT_MISSING"
}
if (@($feed.feedQualityScores).Count -lt 3) {
    Fail "Feed quality from imported quotes is missing." "EXEC_SIM_R005_FAIL_TCA_REPORT_MISSING"
}
if ($policyResults.policyResultsSummary.lineCount -le 0) {
    Fail "Imported fixture policy results are missing." "EXEC_SIM_R005_FAIL_IMPORTED_FIXTURE_BACKTEST_MISSING"
}
foreach ($policy in @("WakettPureLimitUntilClose", "WakettFiveMarketSlicesAroundClose", "CloseSeeking15mAdaptive", "ControlledResidualCross")) {
    Require-Contains $policyResults.requiredPoliciesPresent $policy "Policy results missing $policy." "EXEC_SIM_R005_FAIL_IMPORTED_FIXTURE_BACKTEST_MISSING"
}

Require-True $comparison.usesImportedSanitizedQuoteFixtures "Policy comparison does not use imported quote fixtures." "EXEC_SIM_R005_FAIL_POLICY_COMPARISON_MISSING"
Require-True $comparison.usesUsdPairNormalization "USD-pair normalization not preserved." "EXEC_SIM_R005_FAIL_IMPORTED_FIXTURE_BACKTEST_MISSING"
Require-True $comparison.directCrossSignalsNotExecuted "Direct crosses are not blocked." "EXEC_SIM_R005_FAIL_IMPORTED_FIXTURE_BACKTEST_MISSING"
Require-False $comparison.fiveUsdPerMillionUniversalized "5 USD/million was universalized." "EXEC_SIM_R005_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-True $comparison.noOrdersCreated "Policy comparison created orders." "EXEC_SIM_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-True $comparison.noRealFillsCreated "Policy comparison created real fills." "EXEC_SIM_R005_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-True $comparison.noExecutionReportsCreated "Policy comparison created execution reports." "EXEC_SIM_R005_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-True $comparison.noRoutesCreated "Policy comparison created routes." "EXEC_SIM_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-True $comparison.noSubmissionsCreated "Policy comparison created submissions." "EXEC_SIM_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

foreach ($ranking in @($median, $p95, $fill, $residual, $spread)) {
    Require-True $ranking.rankingsProduced "A required policy ranking is missing." "EXEC_SIM_R005_FAIL_POLICY_COMPARISON_MISSING"
}
Require-True $worst.worstCasesProduced "Worst imported-fixture scenarios are missing." "EXEC_SIM_R005_FAIL_POLICY_COMPARISON_MISSING"

Require-True $wakettLimit.negativeBaseline "Wakett limit result is not marked negative baseline." "EXEC_SIM_R005_FAIL_WAKETT_BASELINES_MISSING"
if ($wakettLimit.residualAtClose -lt 0.5) {
    Fail "Wakett limit baseline does not show high residual." "EXEC_SIM_R005_FAIL_WAKETT_BASELINES_MISSING"
}
Require-True $wakettSlices.repeatedSpreadCrossing "Wakett five slices result does not show repeated spread crossing." "EXEC_SIM_R005_FAIL_WAKETT_BASELINES_MISSING"
if ($wakettSlices.spreadPaidBps -lt 5) {
    Fail "Wakett five slices result does not show high spread paid." "EXEC_SIM_R005_FAIL_WAKETT_BASELINES_MISSING"
}
Require-True $adaptive.balancesPassiveFillAndResidualControl "CloseSeeking15mAdaptive result missing trade-off evidence." "EXEC_SIM_R005_FAIL_CLOSE_SEEKING_RESULT_MISSING"
Require-True $controlled.activatesOnlyWhenOpportunityCostExceedsCrossingCost "ControlledResidualCross lacks opportunity-cost justification." "EXEC_SIM_R005_FAIL_CLOSE_SEEKING_RESULT_MISSING"
Require-False $directCross.directCrossExecutionAllowed "Direct-cross execution allowed in R005." "EXEC_SIM_R005_FAIL_IMPORTED_FIXTURE_BACKTEST_MISSING"
if ($directCross.blockReason -ne "DirectCrossExecutionDisabled") {
    Fail "Direct-cross blocking evidence has wrong reason." "EXEC_SIM_R005_FAIL_IMPORTED_FIXTURE_BACKTEST_MISSING"
}
if (@($feedBlocks.feedQualityBlocks).Count -lt 2) {
    Fail "Feed quality blocking report is incomplete." "EXEC_SIM_R005_FAIL_TCA_REPORT_MISSING"
}

Require-True $majorCost.fiveUsdPerMillionBestCaseOnly "5 USD/million is not best-case only." "EXEC_SIM_R005_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-False $majorCost.fiveUsdPerMillionUniversalized "5 USD/million is universalized." "EXEC_SIM_R005_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-True $nonMajor.nonMajorCalibrationRequired "Non-major calibration report is missing." "EXEC_SIM_R005_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-True $nonMajor.emCnhCalibrationRequired "EM/CNH calibration report is missing." "EXEC_SIM_R005_FAIL_5USD_PER_MILLION_UNIVERSALIZED"

Require-False $polygonAudit.polygonApiCalled "Polygon API call detected." "EXEC_SIM_R005_FAIL_API_CALL_DETECTED"
Require-False $polygonAudit.externalApiCalled "External API call detected." "EXEC_SIM_R005_FAIL_API_CALL_DETECTED"
Require-False $polygonAudit.httpClientUsed "HTTP client usage detected." "EXEC_SIM_R005_FAIL_API_CALL_DETECTED"
Require-False $lmaxAudit.lmaxCalled "LMAX call detected." "EXEC_SIM_R005_FAIL_API_CALL_DETECTED"
Require-False $fillAudit.realFillsCreated "Real fill created." "EXEC_SIM_R005_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $fillAudit.simulationResultsAreFills "Simulation results are classified as fills." "EXEC_SIM_R005_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $reportAudit.executionReportsCreated "Execution report created." "EXEC_SIM_R005_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $reportAudit.simulationResultsAreExecutionReports "Simulation results are classified as execution reports." "EXEC_SIM_R005_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $orderAudit.ordersCreated "Order created." "EXEC_SIM_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $orderAudit.executableOrdersCreated "Executable order created." "EXEC_SIM_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $routeAudit.routesCreated "Route created." "EXEC_SIM_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $routeAudit.submissionsCreated "Submission created." "EXEC_SIM_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-True $lineage.lineagePreserved "Lineage preservation is missing." "EXEC_SIM_R005_FAIL_IMPORTED_FIXTURE_BACKTEST_MISSING"

Require-True $usdjpy.caveatPreserved "USDJPY caveat is not preserved." "EXEC_SIM_R005_FAIL_USDJPY_CAVEAT_WEAKENED"
if ($usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8") {
    Fail "USDJPY caveat SecurityID/SecurityIDSource weakened." "EXEC_SIM_R005_FAIL_USDJPY_CAVEAT_WEAKENED"
}
if ($lmaxBaseline.audusdStatus -notmatch "inconclusive" -or ($lmaxBaseline.audusdStatus -match "failed" -and $lmaxBaseline.audusdStatus -notmatch "not failed")) {
    Fail "AUDUSD is incorrectly marked failed." "EXEC_SIM_R005_FAIL_AUDUSD_MISCLASSIFIED"
}
Require-False $lmaxBaseline.lmaxCalledInR005 "LMAX baseline was called in R005." "EXEC_SIM_R005_FAIL_API_CALL_DETECTED"

Require-False $noExternal.polygonApiCalled "No-external audit shows Polygon API call." "EXEC_SIM_R005_FAIL_API_CALL_DETECTED"
Require-False $noExternal.lmaxCalled "No-external audit shows LMAX call." "EXEC_SIM_R005_FAIL_API_CALL_DETECTED"
Require-False $noExternal.externalApiCalled "No-external audit shows external API call." "EXEC_SIM_R005_FAIL_API_CALL_DETECTED"
Require-False $noExternal.socketTlsFixMarketDataRuntimeDetected "No-external audit shows runtime market-data action." "EXEC_SIM_R005_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $noExternal.ordersFillsReportsRoutesSubmissionsCreated "No-external audit shows order/fill/report/route/submission." "EXEC_SIM_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $forbidden.forbiddenActionsDetected "Forbidden action detected." "EXEC_SIM_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED"

if (-not (Test-Path -LiteralPath $SourcePath)) {
    Fail "Source path missing: $SourcePath" "EXEC_SIM_R005_FAIL_BUILD_OR_TESTS"
}
$source = Get-Content -LiteralPath $SourcePath -Raw
foreach ($token in @("HttpClient", "GetAsync", "PostAsync", "SendAsync", "WebSocket", "TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "FixSession", "ConnectAsync", "BackgroundService", "IHostedService", "PeriodicTimer")) {
    if ($source -match [regex]::Escape($token)) {
        Fail "Runtime/external action token detected in R005 source: $token" "EXEC_SIM_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    }
}

if ($evidence.dotnetBuildNoRestore -ne "PASS") {
    Fail "dotnet build --no-restore evidence is not PASS." "EXEC_SIM_R005_FAIL_BUILD_OR_TESTS"
}
if ($evidence.focusedTests -notmatch "^PASS") {
    Fail "Focused R005 test evidence is not PASS." "EXEC_SIM_R005_FAIL_BUILD_OR_TESTS"
}
if ($evidence.unitTests -notmatch "^PASS") {
    Fail "Unit test evidence is not PASS." "EXEC_SIM_R005_FAIL_BUILD_OR_TESTS"
}

Write-Host "EXEC_SIM_R005_PASS_IMPORTED_QUOTE_FIXTURE_BACKTEST_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R005_PASS_IMPORTED_FIXTURE_TCA_REPORT_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R005_PASS_WAKETT_VS_CLOSE_SEEKING_IMPORTED_FIXTURE_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R005_PASS_NO_API_NO_REAL_FILL_NO_ORDER_GATE_READY_NO_EXTERNAL"
exit 0
