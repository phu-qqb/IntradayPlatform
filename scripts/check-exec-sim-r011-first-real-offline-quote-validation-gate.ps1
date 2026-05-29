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
        Fail "Missing artifact: $Path" "EXEC_SIM_R011_FAIL_BUILD_OR_TESTS"
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
    "phase-exec-sim-r011-summary.md",
    "phase-exec-sim-r011-validation-contract.json",
    "phase-exec-sim-r011-authorized-files-used.json",
    "phase-exec-sim-r011-file-level-validation-results.json",
    "phase-exec-sim-r011-row-level-validation-results.json",
    "phase-exec-sim-r011-accepted-file-manifests.json",
    "phase-exec-sim-r011-quarantined-file-manifests.json",
    "phase-exec-sim-r011-rejected-row-summary.json",
    "phase-exec-sim-r011-sanitized-import-readiness-outputs.json",
    "phase-exec-sim-r011-eurusd-validation-result.json",
    "phase-exec-sim-r011-usdjpy-validation-result.json",
    "phase-exec-sim-r011-audusd-validation-result.json",
    "phase-exec-sim-r011-quote-window-readiness-results.json",
    "phase-exec-sim-r011-close-benchmark-readiness-results.json",
    "phase-exec-sim-r011-feed-quality-readiness-results.json",
    "phase-exec-sim-r011-row-count-comparison.json",
    "phase-exec-sim-r011-duplicate-out-of-order-handling.json",
    "phase-exec-sim-r011-cost-guidance-preservation.json",
    "phase-exec-sim-r011-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r011-no-backtest-execution-audit.json",
    "phase-exec-sim-r011-no-imported-tca-policy-results-audit.json",
    "phase-exec-sim-r011-no-polygon-api-call-audit.json",
    "phase-exec-sim-r011-no-lmax-call-audit.json",
    "phase-exec-sim-r011-no-external-api-call-audit.json",
    "phase-exec-sim-r011-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r011-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r011-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r011-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r011-no-external-audit.json",
    "phase-exec-sim-r011-forbidden-actions-audit.json",
    "phase-exec-sim-r011-next-phase-recommendation.json",
    "phase-exec-sim-r011-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsDir $artifact))) {
        Fail "Required R011 artifact is missing: $artifact" "EXEC_SIM_R011_FAIL_BUILD_OR_TESTS"
    }
}

$contract = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r011-validation-contract.json")
$authorized = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r011-authorized-files-used.json")
$fileLevel = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r011-file-level-validation-results.json")
$rowLevel = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r011-row-level-validation-results.json")
$accepted = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r011-accepted-file-manifests.json")
$quarantined = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r011-quarantined-file-manifests.json")
$rejected = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r011-rejected-row-summary.json")
$readiness = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r011-sanitized-import-readiness-outputs.json")
$quoteWindow = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r011-quote-window-readiness-results.json")
$benchmark = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r011-close-benchmark-readiness-results.json")
$feed = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r011-feed-quality-readiness-results.json")
$counts = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r011-row-count-comparison.json")
$duplicates = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r011-duplicate-out-of-order-handling.json")
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r011-cost-guidance-preservation.json")
$directCross = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r011-direct-cross-exclusion-preservation.json")
$noBacktest = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r011-no-backtest-execution-audit.json")
$noTca = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r011-no-imported-tca-policy-results-audit.json")
$api = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r011-no-external-api-call-audit.json")
$runtime = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r011-no-broker-marketdata-runtime-audit.json")
$order = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r011-no-order-fill-report-route-audit.json")
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r011-usdjpy-caveat-preservation.json")
$lmax = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r011-lmax-readonly-baseline-reference.json")
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r011-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r011-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r011-build-test-validator-evidence.json")

Require-True $contract.reusesR010AuthorizationArtifacts "R010 authorization artifacts are not consumed." "EXEC_SIM_R011_FAIL_VALIDATION_RESULTS_MISSING"
Require-True $contract.localFileReadsOnly "Contract is not local-file-only." "EXEC_SIM_R011_FAIL_API_CALL_DETECTED"
Require-True $contract.noPolygonApiCall "Contract allows Polygon calls." "EXEC_SIM_R011_FAIL_API_CALL_DETECTED"
Require-True $contract.noLmaxCall "Contract allows LMAX calls." "EXEC_SIM_R011_FAIL_API_CALL_DETECTED"
Require-True $contract.noImportBacktest "Contract allows import/backtest." "EXEC_SIM_R011_FAIL_BACKTEST_EXECUTED"
Require-True $contract.noSanitizedQuoteRowsCreated "Contract allows sanitized quote rows." "EXEC_SIM_R011_FAIL_SANITIZED_IMPORT_READINESS_MISSING"

if ($authorized.sourceAuthorizationPhase -ne "EXEC-SIM-R010") {
    Fail "Authorized files artifact does not reference R010." "EXEC_SIM_R011_FAIL_VALIDATION_RESULTS_MISSING"
}
Require-False $authorized.directCrossExecutionIncluded "Direct-cross execution included in validation batch." "EXEC_SIM_R011_FAIL_DIRECT_CROSS_GUIDANCE_WEAKENED"

$expectedRows = @{ EURUSD = 54694; USDJPY = 59368; AUDUSD = 60656 }
foreach ($symbol in @("EURUSD", "USDJPY", "AUDUSD")) {
    $file = @($fileLevel.results) | Where-Object { $_.ExecutionTradableSymbol -eq $symbol } | Select-Object -First 1
    if ($null -eq $file) { Fail "File-level validation missing $symbol." "EXEC_SIM_R011_FAIL_VALIDATION_RESULTS_MISSING" }
    Require-True $file.FileExists "$symbol file missing." "EXEC_SIM_R011_FAIL_VALIDATION_RESULTS_MISSING"
    Require-True $file.ManifestExists "$symbol manifest missing." "EXEC_SIM_R011_FAIL_VALIDATION_RESULTS_MISSING"
    Require-True $file.HashMatchesManifest "$symbol hash mismatch." "EXEC_SIM_R011_FAIL_VALIDATION_RESULTS_MISSING"
    Require-True $file.RowCountMatches "$symbol row count mismatch." "EXEC_SIM_R011_FAIL_VALIDATION_RESULTS_MISSING"
    Require-False $file.ContainsSecrets "$symbol contains secrets." "EXEC_SIM_R011_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    Require-False $file.ContainsRawProviderPayload "$symbol contains raw payload." "EXEC_SIM_R011_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    if ($file.ObservedRows -ne $expectedRows[$symbol]) { Fail "$symbol observed row count unexpected." "EXEC_SIM_R011_FAIL_VALIDATION_RESULTS_MISSING" }
    if ($file.ValidationStatus -ne "AcceptedForSanitizedImport") { Fail "$symbol was not accepted for sanitized import readiness." "EXEC_SIM_R011_FAIL_SANITIZED_IMPORT_READINESS_MISSING" }

    $row = @($rowLevel.results) | Where-Object { $_.ExecutionTradableSymbol -eq $symbol } | Select-Object -First 1
    if ($null -eq $row) { Fail "Row-level validation missing $symbol." "EXEC_SIM_R011_FAIL_VALIDATION_RESULTS_MISSING" }
    if ($row.AcceptedRows -ne $expectedRows[$symbol] -or $row.RejectedRows -ne 0) { Fail "$symbol row-level accepted/rejected count unexpected." "EXEC_SIM_R011_FAIL_VALIDATION_RESULTS_MISSING" }
    foreach ($property in @("ParseInvalidRows", "MissingTimestampRows", "MissingBidRows", "MissingAskRows", "InvalidBidAskRows", "RawPayloadSerializedRows", "ProviderMismatchRows", "SymbolMismatchRows", "DerivedMidSpreadMismatchRows")) {
        if ($row.$property -ne 0) { Fail "$symbol has row validation failure $property." "EXEC_SIM_R011_FAIL_VALIDATION_RESULTS_MISSING" }
    }
    Require-True $row.TimestampParsingValidated "$symbol timestamp parsing not validated." "EXEC_SIM_R011_FAIL_VALIDATION_RESULTS_MISSING"
    Require-True $row.BidAskValidationPerformed "$symbol bid/ask validation not performed." "EXEC_SIM_R011_FAIL_VALIDATION_RESULTS_MISSING"
    Require-True $row.AskGreaterThanOrEqualBidValidated "$symbol ask >= bid not validated." "EXEC_SIM_R011_FAIL_VALIDATION_RESULTS_MISSING"
    Require-True $row.MidSpreadSpreadBpsDerivationValidated "$symbol mid/spread derivation not validated." "EXEC_SIM_R011_FAIL_VALIDATION_RESULTS_MISSING"
}

if (@($accepted.acceptedFileManifests).Count -ne 3) {
    Fail "Accepted manifests missing." "EXEC_SIM_R011_FAIL_VALIDATION_RESULTS_MISSING"
}
if (@($quarantined.quarantinedFileManifests).Count -ne 0) {
    Fail "Unexpected quarantined manifests for accepted first batch." "EXEC_SIM_R011_FAIL_VALIDATION_RESULTS_MISSING"
}
Require-True $quarantined.quarantinePathExistsByContract "Quarantine handling missing." "EXEC_SIM_R011_FAIL_VALIDATION_RESULTS_MISSING"
Require-True $rejected.bomNormalizedRowsAreNotRejected "BOM normalization not documented." "EXEC_SIM_R011_FAIL_VALIDATION_RESULTS_MISSING"
Require-False $readiness.sanitizedQuoteRowsCreated "Sanitized quote rows created." "EXEC_SIM_R011_FAIL_SANITIZED_IMPORT_READINESS_MISSING"
Require-False $readiness.importExecuted "Import executed." "EXEC_SIM_R011_FAIL_SANITIZED_IMPORT_READINESS_MISSING"
foreach ($output in @($readiness.outputs)) {
    Require-True $output.SanitizedImportReady "Sanitized import readiness output not ready." "EXEC_SIM_R011_FAIL_SANITIZED_IMPORT_READINESS_MISSING"
}

foreach ($q in @($quoteWindow.results)) {
    if ($q.FeedWindowStatus -ne "Ready" -or $q.QuoteCount -le 0 -or $q.QuoteCountLastMinute -le 0) {
        Fail "Quote-window readiness missing or not ready." "EXEC_SIM_R011_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING"
    }
}
foreach ($b in @($benchmark.results)) {
    if ($b.CloseBenchmarkStatus -ne "Available" -or $b.CloseConstructionMethod -ne "LastValidQuoteBeforeClose") {
        Fail "Close benchmark readiness missing or unavailable." "EXEC_SIM_R011_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING"
    }
}
foreach ($f in @($feed.results)) {
    if ($f.FeedQualityBucket -ne "Good" -or $f.GapNearCloseFlag -ne $false -or $f.StaleNearCloseFlag -ne $false -or $f.SpreadWideNearCloseFlag -ne $false) {
        Fail "Feed quality readiness missing or unsafe." "EXEC_SIM_R011_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING"
    }
}
foreach ($c in @($counts.comparisons)) {
    Require-True $c.RowCountMatches "Row count comparison mismatch." "EXEC_SIM_R011_FAIL_VALIDATION_RESULTS_MISSING"
    Require-False $c.RowCountMismatchClassified "Unexpected row count mismatch classified." "EXEC_SIM_R011_FAIL_VALIDATION_RESULTS_MISSING"
}
foreach ($d in @($duplicates.results)) {
    if ($d.DuplicateHandlingStatus -ne "RecordedDeterministically" -or $d.OutOfOrderRows -ne 0) {
        Fail "Duplicate/out-of-order handling is not deterministic." "EXEC_SIM_R011_FAIL_VALIDATION_RESULTS_MISSING"
    }
}

if ($cost.bestCaseMajorTargetUsdPerMillion -ne 5) {
    Fail "Best-case major target is not 5 USD/million." "EXEC_SIM_R011_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
}
Require-True $cost.fiveUsdPerMillionBestCaseOnly "5 USD/million not marked best-case only." "EXEC_SIM_R011_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-False $cost.fiveUsdPerMillionUniversalized "5 USD/million universalized." "EXEC_SIM_R011_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-True $cost.nonMajorEmScandiCnhRequireLiquidityCalibration "Non-major calibration guidance missing." "EXEC_SIM_R011_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
if ($directCross.executionUniverse -ne "USD-pair-only") { Fail "Execution universe weakened." "EXEC_SIM_R011_FAIL_DIRECT_CROSS_GUIDANCE_WEAKENED" }
Require-False $directCross.directCrossExecutionAllowedByDefault "Direct-cross execution allowed by default." "EXEC_SIM_R011_FAIL_DIRECT_CROSS_GUIDANCE_WEAKENED"
Require-False $directCross.directCrossExecutionIncludedInValidationBatch "Direct-cross included in validation batch." "EXEC_SIM_R011_FAIL_DIRECT_CROSS_GUIDANCE_WEAKENED"
Require-False $directCross.guidanceWeakened "Direct-cross guidance weakened." "EXEC_SIM_R011_FAIL_DIRECT_CROSS_GUIDANCE_WEAKENED"

Require-False $noBacktest.backtestExecuted "Backtest executed." "EXEC_SIM_R011_FAIL_BACKTEST_EXECUTED"
Require-False $noBacktest.importedQuoteTcaBacktestExecuted "Imported quote TCA backtest executed." "EXEC_SIM_R011_FAIL_BACKTEST_EXECUTED"
Require-False $noBacktest.simulationPolicyResultsProduced "Simulation policy results produced." "EXEC_SIM_R011_FAIL_BACKTEST_EXECUTED"
Require-False $noTca.importedQuoteTcaPolicyResultsProduced "Imported quote TCA policy results produced." "EXEC_SIM_R011_FAIL_BACKTEST_EXECUTED"
Require-False $api.polygonApiCalled "Polygon API called." "EXEC_SIM_R011_FAIL_API_CALL_DETECTED"
Require-False $api.lmaxCalled "LMAX called." "EXEC_SIM_R011_FAIL_API_CALL_DETECTED"
Require-False $api.externalApiCalled "External API called." "EXEC_SIM_R011_FAIL_API_CALL_DETECTED"
Require-False $runtime.brokerActivationDetected "Broker activation detected." "EXEC_SIM_R011_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.socketOpened "Socket opened." "EXEC_SIM_R011_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.tlsOpened "TLS opened." "EXEC_SIM_R011_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.fixOpened "FIX opened." "EXEC_SIM_R011_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataRequestSent "MarketDataRequest sent." "EXEC_SIM_R011_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataResponseRead "MarketDataResponse read." "EXEC_SIM_R011_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.schedulerServiceTimerPollingBackgroundJobIntroduced "Scheduler/service/polling introduced." "EXEC_SIM_R011_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $order.ordersCreated "Orders created." "EXEC_SIM_R011_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.executableOrdersCreated "Executable orders created." "EXEC_SIM_R011_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.fillsCreated "Fills created." "EXEC_SIM_R011_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $order.executionReportsCreated "Execution reports created." "EXEC_SIM_R011_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $order.routesCreated "Routes created." "EXEC_SIM_R011_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.submissionsCreated "Submissions created." "EXEC_SIM_R011_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

Require-True $usdjpy.caveatPreserved "USDJPY caveat not preserved." "EXEC_SIM_R011_FAIL_USDJPY_CAVEAT_WEAKENED"
if ($usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8" -or $usdjpy.requiresInversion -ne $true) {
    Fail "USDJPY caveat/inversion weakened." "EXEC_SIM_R011_FAIL_USDJPY_CAVEAT_WEAKENED"
}
if ($lmax.audusdStatus -notmatch "not failed") {
    Fail "AUDUSD incorrectly marked failed." "EXEC_SIM_R011_FAIL_AUDUSD_MISCLASSIFIED"
}
Require-False $lmax.lmaxCalledInR011 "LMAX called in R011." "EXEC_SIM_R011_FAIL_API_CALL_DETECTED"

Require-False $noExternal.polygonApiCalled "No-external audit shows Polygon call." "EXEC_SIM_R011_FAIL_API_CALL_DETECTED"
Require-False $noExternal.lmaxCalled "No-external audit shows LMAX call." "EXEC_SIM_R011_FAIL_API_CALL_DETECTED"
Require-False $noExternal.externalApiCalled "No-external audit shows external API call." "EXEC_SIM_R011_FAIL_API_CALL_DETECTED"
Require-False $noExternal.socketTlsFixMarketDataRuntimeDetected "No-external audit shows runtime." "EXEC_SIM_R011_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $noExternal.backtestExecuted "No-external audit shows backtest." "EXEC_SIM_R011_FAIL_BACKTEST_EXECUTED"
Require-False $noExternal.importedQuoteTcaPolicyResultsProduced "No-external audit shows TCA policy results." "EXEC_SIM_R011_FAIL_BACKTEST_EXECUTED"
Require-False $noExternal.ordersFillsReportsRoutesSubmissionsCreated "No-external audit shows order-domain artifact." "EXEC_SIM_R011_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $forbidden.forbiddenActionsDetected "Forbidden action detected." "EXEC_SIM_R011_FAIL_NEW_EXTERNAL_ACTION_DETECTED"

if ($evidence.dotnetBuildNoRestore -ne "PASS") {
    Fail "dotnet build --no-restore evidence is not PASS." "EXEC_SIM_R011_FAIL_BUILD_OR_TESTS"
}
if ($evidence.focusedTests -notmatch "^PASS") {
    Fail "Focused R011 tests evidence is not PASS." "EXEC_SIM_R011_FAIL_BUILD_OR_TESTS"
}
if ($evidence.unitTests -notmatch "^PASS") {
    Fail "Unit tests evidence is not PASS." "EXEC_SIM_R011_FAIL_BUILD_OR_TESTS"
}

Write-Host "EXEC_SIM_R011_PASS_FIRST_REAL_OFFLINE_QUOTE_VALIDATION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R011_PASS_ACCEPTED_SANITIZED_IMPORT_READINESS_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R011_PASS_QUOTE_WINDOW_CLOSE_BENCHMARK_FEED_QUALITY_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R011_PASS_NO_BACKTEST_NO_API_GATE_READY_NO_EXTERNAL"
exit 0
