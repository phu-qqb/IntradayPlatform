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
    "phase-exec-sim-r022-summary.md",
    "phase-exec-sim-r022-manifest-validation-contract.json",
    "phase-exec-sim-r022-authorized-files-used.json",
    "phase-exec-sim-r022-manifest-validation-results.json",
    "phase-exec-sim-r022-file-level-validation-results.json",
    "phase-exec-sim-r022-accepted-manifest-validation-outputs.json",
    "phase-exec-sim-r022-quarantined-manifest-validation-outputs.json",
    "phase-exec-sim-r022-missing-incomplete-manifest-diagnostics.json",
    "phase-exec-sim-r022-symbol-inversion-validation.json",
    "phase-exec-sim-r022-session-category-validation.json",
    "phase-exec-sim-r022-time-range-validation.json",
    "phase-exec-sim-r022-secret-raw-payload-validation.json",
    "phase-exec-sim-r022-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r022-cost-guidance-preservation.json",
    "phase-exec-sim-r022-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r022-no-row-level-validation-audit.json",
    "phase-exec-sim-r022-no-sanitized-quote-row-creation-audit.json",
    "phase-exec-sim-r022-no-quote-window-close-benchmark-feed-quality-audit.json",
    "phase-exec-sim-r022-no-backtest-simulation-audit.json",
    "phase-exec-sim-r022-no-tca-result-lines-audit.json",
    "phase-exec-sim-r022-no-polygon-api-call-audit.json",
    "phase-exec-sim-r022-no-lmax-call-audit.json",
    "phase-exec-sim-r022-no-external-api-call-audit.json",
    "phase-exec-sim-r022-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r022-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r022-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r022-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r022-no-external-audit.json",
    "phase-exec-sim-r022-forbidden-actions-audit.json",
    "phase-exec-sim-r022-next-phase-recommendation.json",
    "phase-exec-sim-r022-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsDir $artifact))) {
        Fail "Required R022 artifact is missing: $artifact" "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING"
    }
}

$contract = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r022-manifest-validation-contract.json") "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING"
$authorized = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r022-authorized-files-used.json") "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING"
$manifest = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r022-manifest-validation-results.json") "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING"
$file = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r022-file-level-validation-results.json") "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING"
$accepted = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r022-accepted-manifest-validation-outputs.json") "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING"
$quarantined = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r022-quarantined-manifest-validation-outputs.json") "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING"
$diagnostics = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r022-missing-incomplete-manifest-diagnostics.json") "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING"
$inversion = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r022-symbol-inversion-validation.json") "EXEC_SIM_R022_FAIL_INVERSION_OR_SESSION_METADATA_MISSING"
$session = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r022-session-category-validation.json") "EXEC_SIM_R022_FAIL_INVERSION_OR_SESSION_METADATA_MISSING"
$timeRange = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r022-time-range-validation.json") "EXEC_SIM_R022_FAIL_INVERSION_OR_SESSION_METADATA_MISSING"
$secret = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r022-secret-raw-payload-validation.json") "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING"
$direct = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r022-direct-cross-exclusion-preservation.json") "EXEC_SIM_R022_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED"
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r022-cost-guidance-preservation.json") "EXEC_SIM_R022_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$nonmajor = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r022-nonmajor-calibration-preservation.json") "EXEC_SIM_R022_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$row = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r022-no-row-level-validation-audit.json") "EXEC_SIM_R022_FAIL_ROW_LEVEL_VALIDATION_EXECUTED"
$sanitized = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r022-no-sanitized-quote-row-creation-audit.json") "EXEC_SIM_R022_FAIL_SANITIZED_QUOTE_ROWS_CREATED"
$windows = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r022-no-quote-window-close-benchmark-feed-quality-audit.json") "EXEC_SIM_R022_FAIL_ROW_LEVEL_VALIDATION_EXECUTED"
$backtest = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r022-no-backtest-simulation-audit.json") "EXEC_SIM_R022_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
$tca = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r022-no-tca-result-lines-audit.json") "EXEC_SIM_R022_FAIL_TCA_RESULTS_PRODUCED"
$api = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r022-no-external-api-call-audit.json") "EXEC_SIM_R022_FAIL_API_CALL_DETECTED"
$runtime = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r022-no-broker-marketdata-runtime-audit.json") "EXEC_SIM_R022_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
$order = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r022-no-order-fill-report-route-audit.json") "EXEC_SIM_R022_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r022-usdjpy-caveat-preservation.json") "EXEC_SIM_R022_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r022-lmax-readonly-baseline-reference.json") "EXEC_SIM_R022_FAIL_API_CALL_DETECTED"
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r022-no-external-audit.json") "EXEC_SIM_R022_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r022-forbidden-actions-audit.json") "EXEC_SIM_R022_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r022-build-test-validator-evidence.json") "EXEC_SIM_R022_FAIL_BUILD_OR_TESTS"

Require-True $contract.manifestValidationContractCreated "Manifest validation contract missing." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING"
if ($contract.SourceAuthorizationPhase -ne "EXEC-SIM-R021") { Fail "R021 authorization not referenced." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING" }
if ($contract.ExpectedProviderName -ne "PolygonOfflineFile") { Fail "Provider expectation missing." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING" }
if ($contract.ExpectedProviderDatasetType -ne "HistoricalBboQuotes") { Fail "Dataset expectation missing." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING" }
if ($contract.ExpectedFileFormat -ne "NDJSON") { Fail "File format expectation missing." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING" }
if ($contract.ExpectedSessionWindowCategory -ne "IntradayRebalance") { Fail "Session category expectation missing." "EXEC_SIM_R022_FAIL_INVERSION_OR_SESSION_METADATA_MISSING" }
foreach ($status in @("ManifestValidationQuarantinedProviderMismatch", "ManifestValidationQuarantinedDatasetMismatch", "ManifestValidationQuarantinedFormatMismatch", "ManifestValidationQuarantinedTimeRangeMismatch", "ManifestValidationQuarantinedSessionCategoryMismatch", "ManifestValidationQuarantinedSecretRisk", "ManifestValidationQuarantinedRawPayloadRisk", "ManifestValidationQuarantinedDirectCrossExecutionDisabled")) {
    if (@($contract.ValidationStatuses) -notcontains $status) { Fail "Quarantine status missing $status." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING" }
}
Require-True $contract.NoRowLevelValidation "Contract allows row-level validation." "EXEC_SIM_R022_FAIL_ROW_LEVEL_VALIDATION_EXECUTED"
Require-True $contract.NoImport "Contract allows import." "EXEC_SIM_R022_FAIL_SANITIZED_QUOTE_ROWS_CREATED"
Require-True $contract.NoBacktest "Contract allows backtest." "EXEC_SIM_R022_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
Require-True $contract.NoSimulation "Contract allows simulation." "EXEC_SIM_R022_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
Require-True $contract.NoTcaResultLines "Contract allows TCA result lines." "EXEC_SIM_R022_FAIL_TCA_RESULTS_PRODUCED"
Require-True $contract.NoOrdersFillsReportsRoutes "Contract allows order-domain output." "EXEC_SIM_R022_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

if ($authorized.authorizedEntryCount -ne 7) { Fail "Authorized files used count is not seven." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING" }
if ($manifest.totalManifests -ne 7 -or $manifest.acceptedCount -ne 7 -or $manifest.quarantinedCount -ne 0) { Fail "Manifest validation result counts invalid." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING" }
Require-True $manifest.allProviderNamesValid "Provider mismatch detected." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING"
Require-True $manifest.allDatasetTypesValid "Dataset mismatch detected." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING"
Require-True $manifest.allFileFormatsValid "Format mismatch detected." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING"
Require-True $manifest.allTimeRangesValid "Time range mismatch detected." "EXEC_SIM_R022_FAIL_INVERSION_OR_SESSION_METADATA_MISSING"
Require-True $manifest.allHashesPresent "Manifest hash missing." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING"
Require-True $manifest.allComputedHashesMatchManifest "Hash mismatch detected." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING"
Require-True $manifest.allRowCountsDeclared "Row count declared missing." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING"
Require-True $manifest.allContainsSecretsFalse "Secret risk detected." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING"
Require-True $manifest.allContainsRawProviderPayloadFalse "Raw payload risk detected." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING"
Require-False $manifest.rowLevelValidationExecuted "Row-level validation executed." "EXEC_SIM_R022_FAIL_ROW_LEVEL_VALIDATION_EXECUTED"
if ($file.resultCount -ne 7) { Fail "File-level validation results missing." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING" }
if ($accepted.acceptedCount -ne 7) { Fail "Accepted manifest outputs missing." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING" }
if ($quarantined.quarantinedCount -ne 0) { Fail "Unexpected quarantined outputs." "EXEC_SIM_R022_PARTIAL_MANIFEST_VALIDATION_WITH_QUARANTINE_NO_EXTERNAL" }
if ($diagnostics.missingManifestCount -ne 0 -or $diagnostics.missingQuoteFileCount -ne 0 -or $diagnostics.incompleteCriticalFieldCount -ne 0) { Fail "Missing/incomplete diagnostics detected." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING" }

foreach ($symbol in @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")) {
    $matches = @($file.results) | Where-Object { $_.Symbol -eq $symbol }
    if (@($matches).Count -ne 1) { Fail "Missing file-level result for $symbol." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING" }
    $entry = $matches | Select-Object -First 1
    Require-True $entry.QuoteFileExists "Quote file missing for $symbol." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING"
    Require-True $entry.ManifestExists "Manifest missing for $symbol." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING"
    Require-True $entry.ManifestReadable "Manifest not readable for $symbol." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING"
    if ($entry.ProviderName -ne "PolygonOfflineFile" -or $entry.ProviderDatasetType -ne "HistoricalBboQuotes" -or $entry.FileFormat -ne "NDJSON") { Fail "Provider/dataset/format mismatch for $symbol." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING" }
    if ($entry.SessionWindowCategory -ne "IntradayRebalance") { Fail "Session category mismatch for $symbol." "EXEC_SIM_R022_FAIL_INVERSION_OR_SESSION_METADATA_MISSING" }
    if ($entry.TimeRangeStartUtc -ne "2026-05-19T12:00:00Z" -or $entry.TimeRangeEndUtc -ne "2026-05-19T16:00:00Z") { Fail "Time range mismatch for $symbol." "EXEC_SIM_R022_FAIL_INVERSION_OR_SESSION_METADATA_MISSING" }
    Require-True $entry.FileHashPresent "File hash missing for $symbol." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING"
    Require-True $entry.FileHashMatches "File hash mismatch for $symbol." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING"
    if ($entry.RowCountDeclared -le 0) { Fail "Row count declaration missing for $symbol." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING" }
    Require-False $entry.ContainsSecrets "Secret flag true for $symbol." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING"
    Require-False $entry.ContainsRawProviderPayload "Raw payload flag true for $symbol." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING"
    if ($entry.ValidationStatus -ne "ManifestValidationAcceptedWithWarnings" -and $entry.ValidationStatus -ne "ManifestValidationAccepted") { Fail "Unexpected validation status for $symbol." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING" }
}

Require-True $inversion.symbolInversionValidationCreated "Symbol inversion validation missing." "EXEC_SIM_R022_FAIL_INVERSION_OR_SESSION_METADATA_MISSING"
Require-True $inversion.allSymbolsPresent "Not all symbols present." "EXEC_SIM_R022_FAIL_INVERSION_OR_SESSION_METADATA_MISSING"
Require-True $inversion.usdJpyCaveatPreserved "USDJPY caveat missing." "EXEC_SIM_R022_FAIL_USDJPY_CAVEAT_WEAKENED"
Require-False $inversion.audusdMisclassifiedFailed "AUDUSD misclassified failed." "EXEC_SIM_R022_FAIL_AUDUSD_MISCLASSIFIED"
Require-True $session.sessionCategoryValidationCreated "Session category validation missing." "EXEC_SIM_R022_FAIL_INVERSION_OR_SESSION_METADATA_MISSING"
Require-True $session.allEntriesIntradayRebalance "Session category not IntradayRebalance." "EXEC_SIM_R022_FAIL_INVERSION_OR_SESSION_METADATA_MISSING"
Require-False $session.rowLevelValidationExecuted "Session validation used row-level validation." "EXEC_SIM_R022_FAIL_ROW_LEVEL_VALIDATION_EXECUTED"
Require-True $timeRange.allManifestTimeRangesMatch "Time ranges do not match." "EXEC_SIM_R022_FAIL_INVERSION_OR_SESSION_METADATA_MISSING"
Require-True $secret.allContainsSecretsFalse "Secret risk detected." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING"
Require-True $secret.allContainsRawProviderPayloadFalse "Raw payload risk detected." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING"
Require-False $secret.secretRiskDetected "Secret risk detected." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING"
Require-False $secret.rawPayloadRiskDetected "Raw payload risk detected." "EXEC_SIM_R022_FAIL_MANIFEST_VALIDATION_MISSING"
Require-True $direct.directCrossExclusionPreserved "Direct-cross exclusion missing." "EXEC_SIM_R022_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED"
Require-False $direct.directCrossesInExecutionBatch "Direct crosses are in execution batch." "EXEC_SIM_R022_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED"
Require-False $direct.directCrossExecutionAllowedByDefault "Direct-cross execution allowed." "EXEC_SIM_R022_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED"
Require-False $direct.guidanceWeakened "Direct-cross guidance weakened." "EXEC_SIM_R022_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED"
if ($cost.bestCaseMajorTargetUsdPerMillion -ne 5) { Fail "5 USD/million guidance missing." "EXEC_SIM_R022_FAIL_5USD_PER_MILLION_UNIVERSALIZED" }
Require-True $cost.fiveUsdPerMillionBestCaseMajorOnly "5 USD/million not best-case major-only." "EXEC_SIM_R022_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-False $cost.fiveUsdPerMillionUniversalized "5 USD/million universalized." "EXEC_SIM_R022_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-True $nonmajor.RequiresLiquidityCalibration "Nonmajor calibration missing." "EXEC_SIM_R022_FAIL_5USD_PER_MILLION_UNIVERSALIZED"

Require-False $row.quoteRowsReadForValidation "Quote rows read for validation." "EXEC_SIM_R022_FAIL_ROW_LEVEL_VALIDATION_EXECUTED"
Require-False $row.quoteRowsParsed "Quote rows parsed." "EXEC_SIM_R022_FAIL_ROW_LEVEL_VALIDATION_EXECUTED"
Require-False $row.quoteRowsValidated "Quote rows validated." "EXEC_SIM_R022_FAIL_ROW_LEVEL_VALIDATION_EXECUTED"
Require-False $sanitized.sanitizedQuoteRowsCreated "Sanitized quote rows created." "EXEC_SIM_R022_FAIL_SANITIZED_QUOTE_ROWS_CREATED"
Require-False $sanitized.quotesImported "Quotes imported." "EXEC_SIM_R022_FAIL_SANITIZED_QUOTE_ROWS_CREATED"
Require-False $sanitized.quoteFixturesCreated "Quote fixtures created." "EXEC_SIM_R022_FAIL_SANITIZED_QUOTE_ROWS_CREATED"
Require-False $windows.quoteWindowsCreated "Quote windows created." "EXEC_SIM_R022_FAIL_ROW_LEVEL_VALIDATION_EXECUTED"
Require-False $windows.closeBenchmarksCreated "Close benchmarks created." "EXEC_SIM_R022_FAIL_ROW_LEVEL_VALIDATION_EXECUTED"
Require-False $windows.feedQualityResultsCreated "Feed quality results created." "EXEC_SIM_R022_FAIL_ROW_LEVEL_VALIDATION_EXECUTED"
Require-False $backtest.newBacktestExecuted "Backtest executed." "EXEC_SIM_R022_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
Require-False $backtest.newSimulationExecuted "Simulation executed." "EXEC_SIM_R022_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
Require-False $tca.tcaResultLinesProduced "TCA result lines produced." "EXEC_SIM_R022_FAIL_TCA_RESULTS_PRODUCED"
Require-False $tca.simulationResultLinesProduced "Simulation result lines produced." "EXEC_SIM_R022_FAIL_TCA_RESULTS_PRODUCED"
Require-False $tca.tcaReportsProduced "TCA reports produced." "EXEC_SIM_R022_FAIL_TCA_RESULTS_PRODUCED"
Require-False $api.polygonApiCalled "Polygon API called." "EXEC_SIM_R022_FAIL_API_CALL_DETECTED"
Require-False $api.lmaxCalled "LMAX called." "EXEC_SIM_R022_FAIL_API_CALL_DETECTED"
Require-False $api.externalApiCalled "External API called." "EXEC_SIM_R022_FAIL_API_CALL_DETECTED"
Require-False $runtime.brokerActivationDetected "Broker activation detected." "EXEC_SIM_R022_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.socketOpened "Socket opened." "EXEC_SIM_R022_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.tlsOpened "TLS opened." "EXEC_SIM_R022_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.fixOpened "FIX opened." "EXEC_SIM_R022_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataRequestSent "MarketDataRequest sent." "EXEC_SIM_R022_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataResponseRead "MarketDataResponse read." "EXEC_SIM_R022_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.schedulerServiceTimerPollingBackgroundJobIntroduced "Scheduler/service introduced." "EXEC_SIM_R022_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $runtime.automaticExecutionIntroduced "Automatic execution introduced." "EXEC_SIM_R022_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $order.ordersCreated "Orders created." "EXEC_SIM_R022_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.fillEntitiesCreated "Fills created." "EXEC_SIM_R022_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $order.executionReportEntitiesCreated "Execution reports created." "EXEC_SIM_R022_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $order.routesCreated "Routes created." "EXEC_SIM_R022_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.submissionsCreated "Submissions created." "EXEC_SIM_R022_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

Require-True $usdjpy.usdjpyCaveatPreserved "USDJPY caveat weakened." "EXEC_SIM_R022_FAIL_USDJPY_CAVEAT_WEAKENED"
if ($usdjpy.PortfolioNormalizedSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or $usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8") { Fail "USDJPY caveat weakened." "EXEC_SIM_R022_FAIL_USDJPY_CAVEAT_WEAKENED" }
Require-False $usdjpy.audusdMisclassifiedFailed "AUDUSD misclassified failed." "EXEC_SIM_R022_FAIL_AUDUSD_MISCLASSIFIED"
Require-True $lmax.referenceOnly "LMAX reference weakened." "EXEC_SIM_R022_FAIL_API_CALL_DETECTED"
Require-False $lmax.lmaxCalledInR022 "LMAX called in R022." "EXEC_SIM_R022_FAIL_API_CALL_DETECTED"
if ($lmax.audusdStatus -notmatch "not failed") { Fail "AUDUSD incorrectly marked failed." "EXEC_SIM_R022_FAIL_AUDUSD_MISCLASSIFIED" }

Require-False $noExternal.polygonApiCalled "No-external audit shows Polygon call." "EXEC_SIM_R022_FAIL_API_CALL_DETECTED"
Require-False $noExternal.lmaxCalled "No-external audit shows LMAX call." "EXEC_SIM_R022_FAIL_API_CALL_DETECTED"
Require-False $noExternal.externalApiCalled "No-external audit shows external API call." "EXEC_SIM_R022_FAIL_API_CALL_DETECTED"
Require-False $noExternal.quoteRowsValidated "No-external audit shows row validation." "EXEC_SIM_R022_FAIL_ROW_LEVEL_VALIDATION_EXECUTED"
Require-False $noExternal.quoteFilesImported "No-external audit shows import." "EXEC_SIM_R022_FAIL_SANITIZED_QUOTE_ROWS_CREATED"
Require-False $noExternal.sanitizedQuoteRowsCreated "No-external audit shows sanitized rows." "EXEC_SIM_R022_FAIL_SANITIZED_QUOTE_ROWS_CREATED"
Require-False $noExternal.quoteWindowsCreated "No-external audit shows quote windows." "EXEC_SIM_R022_FAIL_ROW_LEVEL_VALIDATION_EXECUTED"
Require-False $noExternal.closeBenchmarksCreated "No-external audit shows close benchmarks." "EXEC_SIM_R022_FAIL_ROW_LEVEL_VALIDATION_EXECUTED"
Require-False $noExternal.feedQualityResultsCreated "No-external audit shows feed quality." "EXEC_SIM_R022_FAIL_ROW_LEVEL_VALIDATION_EXECUTED"
Require-False $noExternal.newBacktestExecuted "No-external audit shows backtest." "EXEC_SIM_R022_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
Require-False $noExternal.newSimulationExecuted "No-external audit shows simulation." "EXEC_SIM_R022_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
Require-False $noExternal.tcaResultLinesProduced "No-external audit shows TCA results." "EXEC_SIM_R022_FAIL_TCA_RESULTS_PRODUCED"
Require-False $noExternal.ordersFillsReportsRoutesSubmissionsCreated "No-external audit shows order-domain output." "EXEC_SIM_R022_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.livePaperBrokerProductionTradingStateMutated "State mutated." "EXEC_SIM_R022_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.paperLedgerStateCommitted "Paper ledger committed." "EXEC_SIM_R022_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $forbidden.forbiddenActionsDetected "Forbidden action detected." "EXEC_SIM_R022_FAIL_NEW_EXTERNAL_ACTION_DETECTED"

if ($evidence.dotnetBuildNoRestore -ne "PASS") { Fail "dotnet build evidence is not PASS." "EXEC_SIM_R022_FAIL_BUILD_OR_TESTS" }
if ($evidence.focusedTests -notmatch "^PASS") { Fail "Focused R022 test evidence is not PASS." "EXEC_SIM_R022_FAIL_BUILD_OR_TESTS" }
if ($evidence.unitTests -notmatch "^PASS") { Fail "Unit test evidence is not PASS." "EXEC_SIM_R022_FAIL_BUILD_OR_TESTS" }
if ($evidence.validator -notmatch "^PASS") { Fail "Validator evidence is not PASS." "EXEC_SIM_R022_FAIL_BUILD_OR_TESTS" }

Write-Host "EXEC_SIM_R022_PASS_EXPANDED_MANIFEST_VALIDATION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R022_PASS_FILE_LEVEL_MANIFEST_PREFLIGHT_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R022_PASS_SYMBOL_INVERSION_SESSION_METADATA_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R022_PASS_NO_ROW_VALIDATION_NO_BACKTEST_GATE_READY_NO_EXTERNAL"
exit 0
