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
    "phase-exec-sim-r021-summary.md",
    "phase-exec-sim-r021-expanded-batch-authorization-contract.json",
    "phase-exec-sim-r021-expanded-batch-authorization-request.json",
    "phase-exec-sim-r021-expanded-batch-preflight-contract.json",
    "phase-exec-sim-r021-authorization-result.json",
    "phase-exec-sim-r021-required-current-symbols.json",
    "phase-exec-sim-r021-expanded-major-symbols.json",
    "phase-exec-sim-r021-file-entry-requirements.json",
    "phase-exec-sim-r021-accepted-for-authorization-entries.json",
    "phase-exec-sim-r021-missing-input-diagnostics.json",
    "phase-exec-sim-r021-session-window-category-handling.json",
    "phase-exec-sim-r021-inversion-preservation.json",
    "phase-exec-sim-r021-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r021-cost-guidance-preservation.json",
    "phase-exec-sim-r021-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r021-no-validation-import-backtest-audit.json",
    "phase-exec-sim-r021-no-sanitized-row-audit.json",
    "phase-exec-sim-r021-no-polygon-api-call-audit.json",
    "phase-exec-sim-r021-no-lmax-call-audit.json",
    "phase-exec-sim-r021-no-external-api-call-audit.json",
    "phase-exec-sim-r021-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r021-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r021-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r021-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r021-no-external-audit.json",
    "phase-exec-sim-r021-forbidden-actions-audit.json",
    "phase-exec-sim-r021-next-phase-recommendation.json",
    "phase-exec-sim-r021-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsDir $artifact))) {
        Fail "Required R021 artifact is missing: $artifact" "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING"
    }
}

$contract = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r021-expanded-batch-authorization-contract.json") "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING"
$request = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r021-expanded-batch-authorization-request.json") "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING"
$preflight = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r021-expanded-batch-preflight-contract.json") "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING"
$result = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r021-authorization-result.json") "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING"
$current = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r021-required-current-symbols.json") "EXEC_SIM_R021_FAIL_SYMBOL_EXPANSION_PLAN_MISSING"
$expanded = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r021-expanded-major-symbols.json") "EXEC_SIM_R021_FAIL_SYMBOL_EXPANSION_PLAN_MISSING"
$requirements = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r021-file-entry-requirements.json") "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING"
$accepted = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r021-accepted-for-authorization-entries.json") "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING"
$diagnostics = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r021-missing-input-diagnostics.json") "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING"
$categories = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r021-session-window-category-handling.json") "EXEC_SIM_R021_FAIL_SESSION_WINDOW_REQUIREMENTS_MISSING"
$inversion = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r021-inversion-preservation.json") "EXEC_SIM_R021_FAIL_USDJPY_CAVEAT_WEAKENED"
$direct = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r021-direct-cross-exclusion-preservation.json") "EXEC_SIM_R021_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED"
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r021-cost-guidance-preservation.json") "EXEC_SIM_R021_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$nonmajor = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r021-nonmajor-calibration-preservation.json") "EXEC_SIM_R021_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$noValidation = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r021-no-validation-import-backtest-audit.json") "EXEC_SIM_R021_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
$noSanitized = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r021-no-sanitized-row-audit.json") "EXEC_SIM_R021_FAIL_DOWNLOAD_OR_IMPORT_EXECUTED"
$api = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r021-no-external-api-call-audit.json") "EXEC_SIM_R021_FAIL_API_CALL_DETECTED"
$runtime = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r021-no-broker-marketdata-runtime-audit.json") "EXEC_SIM_R021_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
$order = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r021-no-order-fill-report-route-audit.json") "EXEC_SIM_R021_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r021-usdjpy-caveat-preservation.json") "EXEC_SIM_R021_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r021-lmax-readonly-baseline-reference.json") "EXEC_SIM_R021_FAIL_API_CALL_DETECTED"
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r021-no-external-audit.json") "EXEC_SIM_R021_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r021-forbidden-actions-audit.json") "EXEC_SIM_R021_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r021-build-test-validator-evidence.json") "EXEC_SIM_R021_FAIL_BUILD_OR_TESTS"

Require-True $contract.expandedBatchAuthorizationContractCreated "Expanded batch authorization contract missing." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING"
if ($contract.SourceReadinessPhase -ne "EXEC-SIM-R020") { Fail "R020 readiness not referenced." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING" }
Require-True $contract.AuthorizationOnly "Contract is not authorization-only." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING"
Require-True $contract.NoValidation "Contract allows validation." "EXEC_SIM_R021_FAIL_DOWNLOAD_OR_IMPORT_EXECUTED"
Require-True $contract.NoImport "Contract allows import." "EXEC_SIM_R021_FAIL_DOWNLOAD_OR_IMPORT_EXECUTED"
Require-True $contract.NoSanitizedQuoteRowsCreated "Contract allows sanitized rows." "EXEC_SIM_R021_FAIL_DOWNLOAD_OR_IMPORT_EXECUTED"
Require-True $contract.NoBacktest "Contract allows backtest." "EXEC_SIM_R021_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
Require-True $contract.NoSimulation "Contract allows simulation." "EXEC_SIM_R021_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
if ($contract.AuthorizationStatus -ne "EXEC_SIM_R021_PASS_EXPANDED_OFFLINE_BATCH_AUTHORIZATION_READY_NO_EXTERNAL") {
    Fail "Authorization contract is not ready." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING"
}
if ($contract.ExpandedMajorUsdPairPreflightStatus -ne "EXEC_SIM_R021_PASS_EXPANDED_MAJOR_USD_PAIR_PREFLIGHT_READY_NO_EXTERNAL") {
    Fail "Expanded major USD-pair preflight status missing." "EXEC_SIM_R021_FAIL_SYMBOL_EXPANSION_PLAN_MISSING"
}
if ($contract.NoValidationImportBacktestStatus -ne "EXEC_SIM_R021_PASS_NO_VALIDATION_IMPORT_BACKTEST_GATE_READY_NO_EXTERNAL") {
    Fail "No-validation/import/backtest status missing." "EXEC_SIM_R021_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
}
if ($contract.AcceptedEntryCount -ne 7 -or $contract.MissingOrIncompleteEntryCount -ne 0) {
    Fail "Contract entry counts are not ready." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING"
}

foreach ($symbol in @("EURUSD", "USDJPY", "AUDUSD")) {
    if (@($contract.RequiredCurrentSymbols) -notcontains $symbol) { Fail "Required current symbol missing $symbol." "EXEC_SIM_R021_FAIL_SYMBOL_EXPANSION_PLAN_MISSING" }
}
foreach ($symbol in @("GBPUSD", "NZDUSD", "USDCAD", "USDCHF")) {
    if (@($contract.ExpandedMajorSymbols) -notcontains $symbol) { Fail "Expanded major symbol missing $symbol." "EXEC_SIM_R021_FAIL_SYMBOL_EXPANSION_PLAN_MISSING" }
}
foreach ($field in @("symbol", "sessionWindowCategory", "quoteFilePath", "manifestPath", "observedRows", "timeRangeStartUtc", "timeRangeEndUtc")) {
    if (@($contract.RequiredEntryFields) -notcontains $field -or @($requirements.requiredFields) -notcontains $field) { Fail "File entry requirement missing $field." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING" }
}

Require-True $request.authorizationRequestCreated "Authorization request missing." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING"
Require-True $request.operatorProvidedEntriesVisibleInRequest "Request missing operator entries." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING"
if ($request.operatorProvidedEntryCount -ne 7 -or @($request.fileEntries).Count -ne 7) { Fail "Request should contain seven visible file entries." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING" }
if ($request.requestStatus -ne "ReadyForAuthorizationNoExternal") { Fail "Request is not ready for no-external authorization." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING" }
Require-True $preflight.preflightContractCreated "Preflight contract missing." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING"
if ($preflight.preflightStatus -ne "ExpandedMajorUsdPairPreflightReadyNoExternal") { Fail "Preflight is not ready." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING" }
if ($preflight.AcceptedEntryCount -ne 7 -or $preflight.MissingOrIncompleteEntryCount -ne 0) { Fail "Preflight entry counts are not ready." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING" }
Require-True $result.authorizationResultCreated "Authorization result missing." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING"
if ($result.AuthorizationStatus -ne "EXEC_SIM_R021_PASS_EXPANDED_OFFLINE_BATCH_AUTHORIZATION_READY_NO_EXTERNAL") { Fail "Authorization result is not ready." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING" }
Require-True $result.AuthorizationReady "Authorization result is not ready." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING"
if ($result.AcceptedEntryCount -ne 7 -or $result.MissingOrIncompleteEntryCount -ne 0) { Fail "Authorization result entry counts are not ready." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING" }
Require-False $result.MayProceedToValidationImportOrBacktest "R021 authorizes validation/import/backtest." "EXEC_SIM_R021_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
Require-True $result.MayProceedToFutureManifestValidationGate "R021 does not authorize future manifest gate." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING"
Require-True $accepted.acceptedForAuthorizationEntriesCreated "Accepted entries artifact missing." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING"
if ($accepted.acceptedEntryCount -ne 7 -or @($accepted.acceptedEntries).Count -ne 7) { Fail "Accepted entries should contain seven entries." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING" }
Require-True $accepted.authorizationReady "Accepted entries are not authorization ready." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING"
Require-True $diagnostics.missingInputDiagnosticsCreated "Missing-input diagnostics missing." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING"
Require-False $diagnostics.NeedsOperatorFilePaths "Diagnostics still need file paths." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING"
Require-False $diagnostics.NeedsOperatorManifests "Diagnostics still need manifests." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING"
Require-False $diagnostics.NeedsOperatorSessionWindowCategories "Diagnostics still need session categories." "EXEC_SIM_R021_FAIL_SESSION_WINDOW_REQUIREMENTS_MISSING"
Require-False $diagnostics.NeedsOperatorUtcTimeRanges "Diagnostics still need UTC ranges." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING"
if (@($diagnostics.diagnostics).Count -ne 0 -or $diagnostics.MissingInputCount -ne 0) { Fail "Missing diagnostics should be empty." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING" }

foreach ($symbol in @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")) {
    $requestMatches = @($request.fileEntries) | Where-Object { $_.symbol -eq $symbol }
    $acceptedMatches = @($accepted.acceptedEntries) | Where-Object { $_.symbol -eq $symbol }
    if (@($requestMatches).Count -ne 1 -or @($acceptedMatches).Count -ne 1) { Fail "Missing authorized entry for $symbol." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING" }
    $acceptedEntry = $acceptedMatches | Select-Object -First 1
    Require-True $acceptedEntry.quoteFilePathSupplied "Quote file path not supplied for $symbol." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING"
    Require-True $acceptedEntry.manifestPathSupplied "Manifest path not supplied for $symbol." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING"
    Require-True $acceptedEntry.quoteFilePathExistsAtAuthorizationCheck "Quote file path not present for $symbol." "EXEC_SIM_R021_BLOCKED_OPERATOR_FILE_PATHS_OR_MANIFESTS_MISSING_NO_EXTERNAL"
    Require-True $acceptedEntry.manifestPathExistsAtAuthorizationCheck "Manifest path not present for $symbol." "EXEC_SIM_R021_BLOCKED_OPERATOR_FILE_PATHS_OR_MANIFESTS_MISSING_NO_EXTERNAL"
    Require-True $acceptedEntry.authorizationOnly "Entry is not authorization-only for $symbol." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING"
    Require-False $acceptedEntry.quoteRowsRead "Quote rows read for $symbol." "EXEC_SIM_R021_FAIL_DOWNLOAD_OR_IMPORT_EXECUTED"
    Require-False $acceptedEntry.rowContentsValidated "Row contents validated for $symbol." "EXEC_SIM_R021_FAIL_DOWNLOAD_OR_IMPORT_EXECUTED"
    if ($acceptedEntry.SessionWindowCategory -ne "IntradayRebalance") { Fail "Wrong session category for $symbol." "EXEC_SIM_R021_FAIL_SESSION_WINDOW_REQUIREMENTS_MISSING" }
    if ($acceptedEntry.TimeRangeStartUtc -ne "2026-05-19T12:00:00Z" -or $acceptedEntry.TimeRangeEndUtc -ne "2026-05-19T16:00:00Z") { Fail "Wrong time range for $symbol." "EXEC_SIM_R021_FAIL_READINESS_CONTRACT_MISSING" }
}

Require-True $current.requiredCurrentSymbolsCreated "Current symbols artifact missing." "EXEC_SIM_R021_FAIL_SYMBOL_EXPANSION_PLAN_MISSING"
Require-True $expanded.expandedMajorSymbolsCreated "Expanded symbols artifact missing." "EXEC_SIM_R021_FAIL_SYMBOL_EXPANSION_PLAN_MISSING"
Require-True $categories.sessionWindowCategoryHandlingCreated "Session window category handling missing." "EXEC_SIM_R021_FAIL_SESSION_WINDOW_REQUIREMENTS_MISSING"
foreach ($category in @("OpeningBuild", "IntradayRebalance", "ClosingFlatten", "Mixed", "Unknown")) {
    if (@($categories.allowedCategories) -notcontains $category) { Fail "Session category missing $category." "EXEC_SIM_R021_FAIL_SESSION_WINDOW_REQUIREMENTS_MISSING" }
}

Require-True $inversion.inversionPreservationCreated "Inversion preservation missing." "EXEC_SIM_R021_FAIL_USDJPY_CAVEAT_WEAKENED"
Require-True $inversion.usdJpyCaveatPreserved "USDJPY caveat weakened." "EXEC_SIM_R021_FAIL_USDJPY_CAVEAT_WEAKENED"
Require-False $inversion.audusdMisclassifiedFailed "AUDUSD misclassified failed." "EXEC_SIM_R021_FAIL_AUDUSD_MISCLASSIFIED"
Require-True $direct.directCrossExclusionPreserved "Direct-cross exclusion missing." "EXEC_SIM_R021_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED"
Require-False $direct.directCrossExecutionAllowedByDefault "Direct-cross execution allowed." "EXEC_SIM_R021_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED"
Require-False $direct.guidanceWeakened "Direct-cross guidance weakened." "EXEC_SIM_R021_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED"
if ($cost.bestCaseMajorTargetUsdPerMillion -ne 5) { Fail "5 USD/million guidance missing." "EXEC_SIM_R021_FAIL_5USD_PER_MILLION_UNIVERSALIZED" }
Require-True $cost.fiveUsdPerMillionBestCaseMajorOnly "5 USD/million not best-case major-only." "EXEC_SIM_R021_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-False $cost.fiveUsdPerMillionUniversalized "5 USD/million universalized." "EXEC_SIM_R021_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-True $nonmajor.RequiresLiquidityCalibration "Nonmajor calibration missing." "EXEC_SIM_R021_FAIL_5USD_PER_MILLION_UNIVERSALIZED"

Require-False $noValidation.quoteRowsValidated "Quote rows validated." "EXEC_SIM_R021_FAIL_DOWNLOAD_OR_IMPORT_EXECUTED"
Require-False $noValidation.quoteFilesValidated "Quote files validated." "EXEC_SIM_R021_FAIL_DOWNLOAD_OR_IMPORT_EXECUTED"
Require-False $noValidation.quotesImported "Quotes imported." "EXEC_SIM_R021_FAIL_DOWNLOAD_OR_IMPORT_EXECUTED"
Require-False $noValidation.newBacktestExecuted "Backtest executed." "EXEC_SIM_R021_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
Require-False $noValidation.newSimulationExecuted "Simulation executed." "EXEC_SIM_R021_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
Require-False $noValidation.tcaResultLinesCreated "TCA result lines created." "EXEC_SIM_R021_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
Require-False $noSanitized.sanitizedQuoteRowsCreated "Sanitized quote rows created." "EXEC_SIM_R021_FAIL_DOWNLOAD_OR_IMPORT_EXECUTED"
Require-False $noSanitized.quoteFixturesCreated "Quote fixtures created." "EXEC_SIM_R021_FAIL_DOWNLOAD_OR_IMPORT_EXECUTED"
Require-False $api.polygonApiCalled "Polygon API called." "EXEC_SIM_R021_FAIL_API_CALL_DETECTED"
Require-False $api.lmaxCalled "LMAX called." "EXEC_SIM_R021_FAIL_API_CALL_DETECTED"
Require-False $api.externalApiCalled "External API called." "EXEC_SIM_R021_FAIL_API_CALL_DETECTED"
Require-False $runtime.brokerActivationDetected "Broker activation detected." "EXEC_SIM_R021_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.socketOpened "Socket opened." "EXEC_SIM_R021_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.tlsOpened "TLS opened." "EXEC_SIM_R021_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.fixOpened "FIX opened." "EXEC_SIM_R021_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataRequestSent "MarketDataRequest sent." "EXEC_SIM_R021_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataResponseRead "MarketDataResponse read." "EXEC_SIM_R021_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.schedulerServiceTimerPollingBackgroundJobIntroduced "Scheduler/service introduced." "EXEC_SIM_R021_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $order.ordersCreated "Orders created." "EXEC_SIM_R021_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.fillEntitiesCreated "Fills created." "EXEC_SIM_R021_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $order.executionReportEntitiesCreated "Execution reports created." "EXEC_SIM_R021_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $order.routesCreated "Routes created." "EXEC_SIM_R021_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.submissionsCreated "Submissions created." "EXEC_SIM_R021_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

if ($usdjpy.PortfolioNormalizedSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY") { Fail "USDJPY symbol caveat weakened." "EXEC_SIM_R021_FAIL_USDJPY_CAVEAT_WEAKENED" }
Require-True $usdjpy.RequiresInversion "USDJPY inversion missing." "EXEC_SIM_R021_FAIL_USDJPY_CAVEAT_WEAKENED"
if ($usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8") { Fail "USDJPY caveat weakened." "EXEC_SIM_R021_FAIL_USDJPY_CAVEAT_WEAKENED" }
Require-False $usdjpy.audusdMisclassifiedFailed "AUDUSD misclassified failed." "EXEC_SIM_R021_FAIL_AUDUSD_MISCLASSIFIED"
Require-True $lmax.referenceOnly "LMAX reference weakened." "EXEC_SIM_R021_FAIL_API_CALL_DETECTED"
Require-False $lmax.lmaxCalledInR021 "LMAX called in R021." "EXEC_SIM_R021_FAIL_API_CALL_DETECTED"
if ($lmax.audusdStatus -notmatch "not failed") { Fail "AUDUSD incorrectly marked failed." "EXEC_SIM_R021_FAIL_AUDUSD_MISCLASSIFIED" }

Require-False $noExternal.polygonApiCalled "No-external audit shows Polygon call." "EXEC_SIM_R021_FAIL_API_CALL_DETECTED"
Require-False $noExternal.lmaxCalled "No-external audit shows LMAX call." "EXEC_SIM_R021_FAIL_API_CALL_DETECTED"
Require-False $noExternal.externalApiCalled "No-external audit shows external API call." "EXEC_SIM_R021_FAIL_API_CALL_DETECTED"
Require-False $noExternal.quoteFilesDownloaded "No-external audit shows download." "EXEC_SIM_R021_FAIL_DOWNLOAD_OR_IMPORT_EXECUTED"
Require-False $noExternal.quoteFilesValidated "No-external audit shows validation." "EXEC_SIM_R021_FAIL_DOWNLOAD_OR_IMPORT_EXECUTED"
Require-False $noExternal.quoteFilesImported "No-external audit shows import." "EXEC_SIM_R021_FAIL_DOWNLOAD_OR_IMPORT_EXECUTED"
Require-False $noExternal.sanitizedQuoteRowsCreated "No-external audit shows sanitized rows." "EXEC_SIM_R021_FAIL_DOWNLOAD_OR_IMPORT_EXECUTED"
Require-False $noExternal.newBacktestExecuted "No-external audit shows backtest." "EXEC_SIM_R021_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
Require-False $noExternal.newSimulationExecuted "No-external audit shows simulation." "EXEC_SIM_R021_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
Require-False $noExternal.tcaResultLinesCreated "No-external audit shows TCA results." "EXEC_SIM_R021_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
Require-False $noExternal.ordersFillsReportsRoutesSubmissionsCreated "No-external audit shows order-domain output." "EXEC_SIM_R021_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.liveBrokerProductionTradingStateMutated "State mutated." "EXEC_SIM_R021_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.paperLedgerStateCommitted "Paper ledger committed." "EXEC_SIM_R021_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $forbidden.forbiddenActionsDetected "Forbidden action detected." "EXEC_SIM_R021_FAIL_NEW_EXTERNAL_ACTION_DETECTED"

if ($evidence.dotnetBuildNoRestore -ne "PASS") { Fail "dotnet build evidence is not PASS." "EXEC_SIM_R021_FAIL_BUILD_OR_TESTS" }
if ($evidence.focusedTests -notmatch "^PASS") { Fail "Focused R021 test evidence is not PASS." "EXEC_SIM_R021_FAIL_BUILD_OR_TESTS" }
if ($evidence.unitTests -notmatch "^PASS") { Fail "Unit test evidence is not PASS." "EXEC_SIM_R021_FAIL_BUILD_OR_TESTS" }
if ($evidence.validator -notmatch "^PASS") { Fail "Validator evidence is not PASS." "EXEC_SIM_R021_FAIL_BUILD_OR_TESTS" }

Write-Host "EXEC_SIM_R021_PASS_EXPANDED_OFFLINE_BATCH_AUTHORIZATION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R021_PASS_EXPANDED_MAJOR_USD_PAIR_PREFLIGHT_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R021_PASS_NO_VALIDATION_IMPORT_BACKTEST_GATE_READY_NO_EXTERNAL"
exit 0
