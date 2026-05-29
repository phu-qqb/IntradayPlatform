param(
    [string]$ArtifactsDir = "artifacts/readiness/execution-sim"
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
    "phase-exec-sim-r023-summary.md",
    "phase-exec-sim-r023-row-level-validation-contract.json",
    "phase-exec-sim-r023-r022-accepted-files-used.json",
    "phase-exec-sim-r023-row-level-validation-results.json",
    "phase-exec-sim-r023-row-count-comparison.json",
    "phase-exec-sim-r023-rejected-row-summary.json",
    "phase-exec-sim-r023-duplicate-out-of-order-handling.json",
    "phase-exec-sim-r023-eurusd-row-validation-result.json",
    "phase-exec-sim-r023-usdjpy-row-validation-result.json",
    "phase-exec-sim-r023-audusd-row-validation-result.json",
    "phase-exec-sim-r023-gbpusd-row-validation-result.json",
    "phase-exec-sim-r023-nzdusd-row-validation-result.json",
    "phase-exec-sim-r023-usdcad-row-validation-result.json",
    "phase-exec-sim-r023-usdchf-row-validation-result.json",
    "phase-exec-sim-r023-quote-window-readiness-results.json",
    "phase-exec-sim-r023-close-benchmark-readiness-results.json",
    "phase-exec-sim-r023-feed-quality-readiness-results.json",
    "phase-exec-sim-r023-sanitized-import-readiness-metadata.json",
    "phase-exec-sim-r023-session-category-warning-preservation.json",
    "phase-exec-sim-r023-symbol-inversion-validation.json",
    "phase-exec-sim-r023-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r023-cost-guidance-preservation.json",
    "phase-exec-sim-r023-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r023-no-db-import-audit.json",
    "phase-exec-sim-r023-no-persisted-sanitized-row-audit.json",
    "phase-exec-sim-r023-no-backtest-simulation-audit.json",
    "phase-exec-sim-r023-no-tca-result-lines-audit.json",
    "phase-exec-sim-r023-no-polygon-api-call-audit.json",
    "phase-exec-sim-r023-no-lmax-call-audit.json",
    "phase-exec-sim-r023-no-external-api-call-audit.json",
    "phase-exec-sim-r023-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r023-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r023-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r023-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r023-no-external-audit.json",
    "phase-exec-sim-r023-forbidden-actions-audit.json",
    "phase-exec-sim-r023-next-phase-recommendation.json",
    "phase-exec-sim-r023-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsDir $artifact))) {
        Fail "Required R023 artifact is missing: $artifact" "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING"
    }
}

$contract = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r023-row-level-validation-contract.json") "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING"
$used = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r023-r022-accepted-files-used.json") "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING"
$rows = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r023-row-level-validation-results.json") "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING"
$counts = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r023-row-count-comparison.json") "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING"
$rejected = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r023-rejected-row-summary.json") "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING"
$dupes = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r023-duplicate-out-of-order-handling.json") "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING"
$windows = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r023-quote-window-readiness-results.json") "EXEC_SIM_R023_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING"
$close = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r023-close-benchmark-readiness-results.json") "EXEC_SIM_R023_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING"
$feed = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r023-feed-quality-readiness-results.json") "EXEC_SIM_R023_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING"
$importReadiness = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r023-sanitized-import-readiness-metadata.json") "EXEC_SIM_R023_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING"
$sessionWarning = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r023-session-category-warning-preservation.json") "EXEC_SIM_R023_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING"
$inversion = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r023-symbol-inversion-validation.json") "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING"
$direct = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r023-direct-cross-exclusion-preservation.json") "EXEC_SIM_R023_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED"
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r023-cost-guidance-preservation.json") "EXEC_SIM_R023_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$nonmajor = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r023-nonmajor-calibration-preservation.json") "EXEC_SIM_R023_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$noDb = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r023-no-db-import-audit.json") "EXEC_SIM_R023_FAIL_DB_IMPORT_OCCURRED"
$noSanitized = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r023-no-persisted-sanitized-row-audit.json") "EXEC_SIM_R023_FAIL_SANITIZED_QUOTE_ROWS_CREATED"
$noBacktest = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r023-no-backtest-simulation-audit.json") "EXEC_SIM_R023_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
$noTca = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r023-no-tca-result-lines-audit.json") "EXEC_SIM_R023_FAIL_TCA_RESULTS_PRODUCED"
$api = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r023-no-external-api-call-audit.json") "EXEC_SIM_R023_FAIL_API_CALL_DETECTED"
$runtime = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r023-no-broker-marketdata-runtime-audit.json") "EXEC_SIM_R023_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
$order = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r023-no-order-fill-report-route-audit.json") "EXEC_SIM_R023_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r023-usdjpy-caveat-preservation.json") "EXEC_SIM_R023_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r023-lmax-readonly-baseline-reference.json") "EXEC_SIM_R023_FAIL_API_CALL_DETECTED"
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r023-no-external-audit.json") "EXEC_SIM_R023_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r023-forbidden-actions-audit.json") "EXEC_SIM_R023_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r023-build-test-validator-evidence.json") "EXEC_SIM_R023_FAIL_BUILD_OR_TESTS"

Require-True $contract.rowLevelValidationContractCreated "Row-level validation contract missing." "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING"
if ($contract.SourceManifestValidationPhase -ne "EXEC-SIM-R022") { Fail "R022 accepted results not referenced." "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING" }
if ($contract.CoverageMode -ne "AllAvailable15MinuteClosesWithinAuthorizedTimeRange") { Fail "Coverage mode missing." "EXEC_SIM_R023_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING" }
Require-True $contract.NoDbImport "Contract allows DB import." "EXEC_SIM_R023_FAIL_DB_IMPORT_OCCURRED"
Require-True $contract.NoPersistedSanitizedQuoteRows "Contract allows persisted sanitized rows." "EXEC_SIM_R023_FAIL_SANITIZED_QUOTE_ROWS_CREATED"
Require-True $contract.NoBacktest "Contract allows backtest." "EXEC_SIM_R023_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
Require-True $contract.NoSimulation "Contract allows simulation." "EXEC_SIM_R023_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
Require-True $contract.NoTcaResultLines "Contract allows TCA result lines." "EXEC_SIM_R023_FAIL_TCA_RESULTS_PRODUCED"
Require-True $contract.NoOrdersFillsReportsRoutes "Contract allows order-domain output." "EXEC_SIM_R023_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
if (@($contract.ExpectedSymbols).Count -ne 7) { Fail "Expected symbol list incomplete." "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING" }
if ($used.acceptedFileCount -ne 7) { Fail "R022 accepted files not consumed." "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING" }
if ($rows.resultCount -ne 7) { Fail "Row-level validation results missing." "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING" }
Require-True $counts.allObservedCountsMatchManifest "Observed row counts do not match manifests." "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING"

foreach ($symbol in @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")) {
    $matches = @($rows.results) | Where-Object { $_.Symbol -eq $symbol }
    if (@($matches).Count -ne 1) { Fail "Missing row validation result for $symbol." "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING" }
    $entry = $matches | Select-Object -First 1
    if ($entry.RowCountObserved -ne $entry.RowCountDeclared) { Fail "Row count mismatch for $symbol." "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING" }
    if ($entry.AcceptedRowCount -le 0) { Fail "No accepted rows for $symbol." "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING" }
    if ($entry.ValidationStatus -ne "RowValidationAcceptedWithRejectedRows" -and $entry.ValidationStatus -ne "RowValidationAccepted" -and $entry.ValidationStatus -ne "RowValidationAcceptedWithWarnings") { Fail "Invalid row validation status for $symbol." "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING" }
    if ($entry.InvalidBidAskRowCount -ne 0 -or $entry.AskLessThanBidRowCount -ne 0) { Fail "Invalid bid/ask rows for $symbol." "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING" }
    if ($entry.SymbolMismatchRowCount -ne 0) { Fail "Symbol mismatch rows for $symbol." "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING" }
    if ($entry.RawPayloadSerializedTrueRowCount -ne 0) { Fail "Raw payload serialized rows for $symbol." "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING" }
    Require-True $entry.MidSpreadSpreadBpsDerived "Mid/spread/spreadBps derivation missing for $symbol." "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING"
}

if ($rejected.TotalRejectedRowCount -ne 7 -or $rejected.MalformedJsonRowCount -ne 7) { Fail "Rejected row summary missing expected safe partial rejections." "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING" }
Require-False $rejected.rejectedRowsPersisted "Rejected rows were persisted." "EXEC_SIM_R023_FAIL_SANITIZED_QUOTE_ROWS_CREATED"
Require-True $dupes.deterministicHandling "Duplicate/out-of-order handling is not deterministic." "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING"
if ($dupes.duplicateTimestampTotal -le 0 -or $dupes.duplicateRowTotal -le 0) { Fail "Duplicate summary missing." "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING" }
if ($dupes.outOfOrderRowTotal -ne 0) { Fail "Unexpected out-of-order rows detected." "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING" }

Require-True $windows.quoteWindowReadinessResultsCreated "Quote-window readiness missing." "EXEC_SIM_R023_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING"
if ($windows.evaluatedWindowCount -ne 112) { Fail "Expected 112 quote windows." "EXEC_SIM_R023_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING" }
Require-True $close.closeBenchmarkReadinessResultsCreated "Close benchmark readiness missing." "EXEC_SIM_R023_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING"
if ($close.resultCount -ne 112) { Fail "Expected 112 close benchmark results." "EXEC_SIM_R023_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING" }
Require-True $feed.feedQualityReadinessResultsCreated "Feed quality readiness missing." "EXEC_SIM_R023_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING"
if ($feed.resultCount -ne 7) { Fail "Expected seven feed quality results." "EXEC_SIM_R023_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING" }
Require-True $importReadiness.sanitizedImportReadinessMetadataCreated "Sanitized import readiness metadata missing." "EXEC_SIM_R023_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING"
Require-True $importReadiness.metadataOnly "Sanitized import readiness is not metadata-only." "EXEC_SIM_R023_FAIL_SANITIZED_QUOTE_ROWS_CREATED"
Require-False $importReadiness.persistedSanitizedQuoteRowsCreated "Persisted sanitized rows created." "EXEC_SIM_R023_FAIL_SANITIZED_QUOTE_ROWS_CREATED"
Require-False $importReadiness.dbImportOccurred "DB import occurred." "EXEC_SIM_R023_FAIL_DB_IMPORT_OCCURRED"
Require-True $sessionWarning.sessionCategoryWarningPreserved "Session category warning not preserved." "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING"
if ($sessionWarning.SessionWindowCategorySource -ne "R021AuthorizationMetadata") { Fail "Session category source weakened." "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING" }
Require-False $sessionWarning.warningWeakened "Session warning weakened." "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING"
Require-True $inversion.symbolInversionValidationCreated "Symbol inversion validation missing." "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING"
Require-True $inversion.allSymbolsPresent "Not all symbols present in inversion validation." "EXEC_SIM_R023_FAIL_ROW_VALIDATION_MISSING"
Require-True $inversion.usdJpyCaveatPreserved "USDJPY caveat missing." "EXEC_SIM_R023_FAIL_USDJPY_CAVEAT_WEAKENED"
Require-False $inversion.audusdMisclassifiedFailed "AUDUSD misclassified failed." "EXEC_SIM_R023_FAIL_AUDUSD_MISCLASSIFIED"
Require-True $direct.directCrossExclusionPreserved "Direct-cross exclusion missing." "EXEC_SIM_R023_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED"
Require-False $direct.directCrossExecutionAllowedByDefault "Direct-cross execution allowed." "EXEC_SIM_R023_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED"
Require-False $direct.guidanceWeakened "Direct-cross guidance weakened." "EXEC_SIM_R023_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED"
if ($cost.bestCaseMajorTargetUsdPerMillion -ne 5) { Fail "5 USD/million guidance missing." "EXEC_SIM_R023_FAIL_5USD_PER_MILLION_UNIVERSALIZED" }
Require-True $cost.fiveUsdPerMillionBestCaseMajorOnly "5 USD/million not best-case major-only." "EXEC_SIM_R023_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-False $cost.fiveUsdPerMillionUniversalized "5 USD/million universalized." "EXEC_SIM_R023_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-True $nonmajor.RequiresLiquidityCalibration "Nonmajor calibration missing." "EXEC_SIM_R023_FAIL_5USD_PER_MILLION_UNIVERSALIZED"

Require-False $noDb.quotesImportedIntoDb "Quotes imported into DB." "EXEC_SIM_R023_FAIL_DB_IMPORT_OCCURRED"
Require-False $noDb.dbWriteOccurred "DB write occurred." "EXEC_SIM_R023_FAIL_DB_IMPORT_OCCURRED"
Require-False $noDb.paperLedgerStateCommitted "Paper ledger committed." "EXEC_SIM_R023_FAIL_DB_IMPORT_OCCURRED"
Require-False $noSanitized.persistedSanitizedQuoteRowsCreated "Persisted sanitized rows created." "EXEC_SIM_R023_FAIL_SANITIZED_QUOTE_ROWS_CREATED"
Require-False $noSanitized.sanitizedQuoteRowsCreatedForPersistence "Sanitized rows created for persistence." "EXEC_SIM_R023_FAIL_SANITIZED_QUOTE_ROWS_CREATED"
Require-False $noBacktest.newBacktestExecuted "Backtest executed." "EXEC_SIM_R023_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
Require-False $noBacktest.newSimulationExecuted "Simulation executed." "EXEC_SIM_R023_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
Require-False $noTca.tcaResultLinesProduced "TCA result lines produced." "EXEC_SIM_R023_FAIL_TCA_RESULTS_PRODUCED"
Require-False $noTca.simulationResultLinesProduced "Simulation result lines produced." "EXEC_SIM_R023_FAIL_TCA_RESULTS_PRODUCED"
Require-False $api.polygonApiCalled "Polygon API called." "EXEC_SIM_R023_FAIL_API_CALL_DETECTED"
Require-False $api.lmaxCalled "LMAX called." "EXEC_SIM_R023_FAIL_API_CALL_DETECTED"
Require-False $api.externalApiCalled "External API called." "EXEC_SIM_R023_FAIL_API_CALL_DETECTED"
Require-False $runtime.brokerActivationDetected "Broker activation detected." "EXEC_SIM_R023_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.socketOpened "Socket opened." "EXEC_SIM_R023_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.tlsOpened "TLS opened." "EXEC_SIM_R023_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.fixOpened "FIX opened." "EXEC_SIM_R023_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataRequestSent "MarketDataRequest sent." "EXEC_SIM_R023_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataResponseRead "MarketDataResponse read." "EXEC_SIM_R023_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.schedulerServiceTimerPollingBackgroundJobIntroduced "Scheduler/service introduced." "EXEC_SIM_R023_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $runtime.automaticExecutionIntroduced "Automatic execution introduced." "EXEC_SIM_R023_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $order.ordersCreated "Orders created." "EXEC_SIM_R023_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.fillEntitiesCreated "Fills created." "EXEC_SIM_R023_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $order.executionReportEntitiesCreated "Execution reports created." "EXEC_SIM_R023_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $order.routesCreated "Routes created." "EXEC_SIM_R023_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.submissionsCreated "Submissions created." "EXEC_SIM_R023_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

Require-True $usdjpy.usdjpyCaveatPreserved "USDJPY caveat weakened." "EXEC_SIM_R023_FAIL_USDJPY_CAVEAT_WEAKENED"
if ($usdjpy.PortfolioNormalizedSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or $usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8") { Fail "USDJPY caveat weakened." "EXEC_SIM_R023_FAIL_USDJPY_CAVEAT_WEAKENED" }
Require-False $usdjpy.audusdMisclassifiedFailed "AUDUSD misclassified failed." "EXEC_SIM_R023_FAIL_AUDUSD_MISCLASSIFIED"
Require-True $lmax.referenceOnly "LMAX reference weakened." "EXEC_SIM_R023_FAIL_API_CALL_DETECTED"
Require-False $lmax.lmaxCalledInR023 "LMAX called in R023." "EXEC_SIM_R023_FAIL_API_CALL_DETECTED"
if ($lmax.audusdStatus -notmatch "not failed") { Fail "AUDUSD incorrectly marked failed." "EXEC_SIM_R023_FAIL_AUDUSD_MISCLASSIFIED" }

Require-False $noExternal.polygonApiCalled "No-external audit shows Polygon call." "EXEC_SIM_R023_FAIL_API_CALL_DETECTED"
Require-False $noExternal.lmaxCalled "No-external audit shows LMAX call." "EXEC_SIM_R023_FAIL_API_CALL_DETECTED"
Require-False $noExternal.externalApiCalled "No-external audit shows external API call." "EXEC_SIM_R023_FAIL_API_CALL_DETECTED"
Require-False $noExternal.quotesImportedIntoDb "No-external audit shows DB import." "EXEC_SIM_R023_FAIL_DB_IMPORT_OCCURRED"
Require-False $noExternal.persistedSanitizedQuoteRowsCreated "No-external audit shows sanitized rows." "EXEC_SIM_R023_FAIL_SANITIZED_QUOTE_ROWS_CREATED"
Require-False $noExternal.newBacktestExecuted "No-external audit shows backtest." "EXEC_SIM_R023_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
Require-False $noExternal.newSimulationExecuted "No-external audit shows simulation." "EXEC_SIM_R023_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
Require-False $noExternal.tcaResultLinesProduced "No-external audit shows TCA results." "EXEC_SIM_R023_FAIL_TCA_RESULTS_PRODUCED"
Require-False $noExternal.ordersFillsReportsRoutesSubmissionsCreated "No-external audit shows order-domain output." "EXEC_SIM_R023_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.livePaperBrokerProductionTradingStateMutated "State mutated." "EXEC_SIM_R023_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.paperLedgerStateCommitted "Paper ledger committed." "EXEC_SIM_R023_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $forbidden.forbiddenActionsDetected "Forbidden action detected." "EXEC_SIM_R023_FAIL_NEW_EXTERNAL_ACTION_DETECTED"

if ($evidence.dotnetBuildNoRestore -ne "PASS") { Fail "dotnet build evidence is not PASS." "EXEC_SIM_R023_FAIL_BUILD_OR_TESTS" }
if ($evidence.focusedTests -notmatch "^PASS") { Fail "Focused R023 test evidence is not PASS." "EXEC_SIM_R023_FAIL_BUILD_OR_TESTS" }
if ($evidence.unitTests -notmatch "^PASS") { Fail "Unit test evidence is not PASS." "EXEC_SIM_R023_FAIL_BUILD_OR_TESTS" }
if ($evidence.validator -notmatch "^PASS") { Fail "Validator evidence is not PASS." "EXEC_SIM_R023_FAIL_BUILD_OR_TESTS" }

Write-Host "EXEC_SIM_R023_PARTIAL_ROW_VALIDATION_WITH_REJECTIONS_NO_EXTERNAL"
Write-Host "EXEC_SIM_R023_PASS_QUOTE_WINDOW_CLOSE_BENCHMARK_FEED_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R023_PASS_SANITIZED_IMPORT_READINESS_METADATA_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R023_PASS_NO_IMPORT_NO_BACKTEST_GATE_READY_NO_EXTERNAL"
exit 0
