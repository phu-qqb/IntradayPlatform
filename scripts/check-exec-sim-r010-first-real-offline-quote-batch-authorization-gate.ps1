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
        Fail "Missing artifact: $Path" "EXEC_SIM_R010_FAIL_BUILD_OR_TESTS"
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
    "phase-exec-sim-r010-summary.md",
    "phase-exec-sim-r010-first-batch-authorization-contract.json",
    "phase-exec-sim-r010-first-batch-authorization-request.json",
    "phase-exec-sim-r010-first-batch-preflight-contract.json",
    "phase-exec-sim-r010-required-symbols.json",
    "phase-exec-sim-r010-file-entry-requirements.json",
    "phase-exec-sim-r010-authorization-statuses.json",
    "phase-exec-sim-r010-authorization-result.json",
    "phase-exec-sim-r010-missing-input-diagnostics.json",
    "phase-exec-sim-r010-cost-guidance-preservation.json",
    "phase-exec-sim-r010-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r010-no-validation-import-backtest-execution-audit.json",
    "phase-exec-sim-r010-no-sanitized-quote-row-creation-audit.json",
    "phase-exec-sim-r010-no-polygon-api-call-audit.json",
    "phase-exec-sim-r010-no-lmax-call-audit.json",
    "phase-exec-sim-r010-no-external-api-call-audit.json",
    "phase-exec-sim-r010-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r010-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r010-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r010-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r010-no-external-audit.json",
    "phase-exec-sim-r010-forbidden-actions-audit.json",
    "phase-exec-sim-r010-next-phase-recommendation.json",
    "phase-exec-sim-r010-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsDir $artifact))) {
        Fail "Required R010 artifact is missing: $artifact" "EXEC_SIM_R010_FAIL_BUILD_OR_TESTS"
    }
}

$contract = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r010-first-batch-authorization-contract.json")
$request = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r010-first-batch-authorization-request.json")
$preflight = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r010-first-batch-preflight-contract.json")
$symbols = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r010-required-symbols.json")
$requirements = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r010-file-entry-requirements.json")
$statuses = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r010-authorization-statuses.json")
$result = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r010-authorization-result.json")
$diagnostics = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r010-missing-input-diagnostics.json")
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r010-cost-guidance-preservation.json")
$directCross = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r010-direct-cross-exclusion-preservation.json")
$noExecution = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r010-no-validation-import-backtest-execution-audit.json")
$noRows = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r010-no-sanitized-quote-row-creation-audit.json")
$polygon = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r010-no-polygon-api-call-audit.json")
$lmaxAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r010-no-lmax-call-audit.json")
$api = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r010-no-external-api-call-audit.json")
$runtime = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r010-no-broker-marketdata-runtime-audit.json")
$order = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r010-no-order-fill-report-route-audit.json")
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r010-usdjpy-caveat-preservation.json")
$lmaxBaseline = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r010-lmax-readonly-baseline-reference.json")
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r010-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r010-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r010-build-test-validator-evidence.json")

Require-True $contract.authorizationOnly "Authorization contract is not authorization-only." "EXEC_SIM_R010_FAIL_AUTHORIZATION_CONTRACT_MISSING"
Require-True $contract.noApiCall "Authorization contract does not forbid API calls." "EXEC_SIM_R010_FAIL_API_CALL_DETECTED"
Require-True $contract.noValidationRun "Authorization contract allows validation execution." "EXEC_SIM_R010_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"
Require-True $contract.noImport "Authorization contract allows import execution." "EXEC_SIM_R010_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"
Require-True $contract.noBacktest "Authorization contract allows backtest execution." "EXEC_SIM_R010_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"
Require-True $contract.noSanitizedQuoteRowsCreated "Authorization contract allows sanitized quote rows." "EXEC_SIM_R010_FAIL_SANITIZED_QUOTE_ROWS_CREATED"

foreach ($symbol in @("EURUSD", "USDJPY", "AUDUSD")) {
    Require-Contains $contract.requiredSymbols $symbol "Contract missing required symbol $symbol." "EXEC_SIM_R010_FAIL_AUTHORIZATION_CONTRACT_MISSING"
    Require-Contains $request.RequiredSymbols $symbol "Request missing required symbol $symbol." "EXEC_SIM_R010_FAIL_AUTHORIZATION_CONTRACT_MISSING"
}

if ($request.ProviderName -ne "PolygonOfflineFile" -or $request.DatasetType -ne "HistoricalBboQuotes") {
    Fail "Authorization request missing provider/dataset identity." "EXEC_SIM_R010_FAIL_AUTHORIZATION_CONTRACT_MISSING"
}
Require-True $request.AuthorizationOnly "Authorization request is not authorization-only." "EXEC_SIM_R010_FAIL_AUTHORIZATION_CONTRACT_MISSING"
Require-True $request.NoApiCall "Authorization request allows API calls." "EXEC_SIM_R010_FAIL_API_CALL_DETECTED"
Require-True $request.NoImport "Authorization request allows import." "EXEC_SIM_R010_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"
Require-True $request.NoBacktest "Authorization request allows backtest." "EXEC_SIM_R010_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"
Require-True $preflight.preflightContractReady "Preflight contract is not ready." "EXEC_SIM_R010_FAIL_AUTHORIZATION_CONTRACT_MISSING"
Require-True $preflight.noValidationImportBacktestTriggered "Preflight indicates validation/import/backtest triggered." "EXEC_SIM_R010_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"

foreach ($status in @("FirstBatchAuthorizationReadyNoExternal", "FirstBatchAuthorizationBlockedMissingFiles", "FirstBatchAuthorizationBlockedMissingManifests", "FirstBatchAuthorizationBlockedIncompleteMetadata", "FirstBatchAuthorizationBlockedUnsafeSecretRisk", "FirstBatchAuthorizationBlockedRawPayloadRisk", "FirstBatchAuthorizationBlockedUnsupportedSymbol", "FirstBatchAuthorizationBlockedDirectCrossExecution", "InconclusiveSafe")) {
    Require-Contains $statuses.authorizationStatuses $status "Authorization statuses missing $status." "EXEC_SIM_R010_FAIL_AUTHORIZATION_CONTRACT_MISSING"
}

foreach ($field in @("ProviderSymbol", "ExecutionTradableSymbol", "NormalizedPortfolioSymbol", "RequiresInversion", "QuoteFilePath", "ManifestPath or ManifestMetadata", "FileFormat", "TimeRangeStartUtc", "TimeRangeEndUtc", "RowCountDeclared", "ProvidedBySanitized", "ContainsSecrets", "ContainsRawProviderPayload", "DirectCrossExecutionDisabled", "EntryAuthorizationStatus")) {
    Require-Contains $requirements.fileEntryFields $field "File entry requirements missing $field." "EXEC_SIM_R010_FAIL_AUTHORIZATION_CONTRACT_MISSING"
}
Require-True $requirements.missingFileReferenceBlocksAuthorization "Missing file does not block authorization." "EXEC_SIM_R010_FAIL_MISSING_INPUT_DIAGNOSTICS_MISSING"
Require-True $requirements.missingManifestReferenceOrMetadataBlocksAuthorization "Missing manifest does not block authorization." "EXEC_SIM_R010_FAIL_MISSING_INPUT_DIAGNOSTICS_MISSING"

if ($result.Classification -ne "EXEC_SIM_R010_PASS_FIRST_REAL_OFFLINE_BATCH_AUTHORIZATION_READY_NO_EXTERNAL") {
    Fail "R010 result is not authorization-ready for supplied operator paths/manifests." "EXEC_SIM_R010_FAIL_AUTHORIZATION_CONTRACT_MISSING"
}
if ($result.AuthorizationStatus -ne "FirstBatchAuthorizationReadyNoExternal") {
    Fail "R010 result does not use ready authorization status." "EXEC_SIM_R010_FAIL_AUTHORIZATION_CONTRACT_MISSING"
}
Require-True $result.AuthorizationReady "Authorization is not ready despite supplied operator inputs." "EXEC_SIM_R010_FAIL_AUTHORIZATION_CONTRACT_MISSING"
Require-False $result.BlockedSafely "Authorization result is still blocked." "EXEC_SIM_R010_FAIL_AUTHORIZATION_CONTRACT_MISSING"
Require-True $result.FileAndManifestPresenceCheckedOnly "Authorization did not record presence-check-only behavior." "EXEC_SIM_R010_FAIL_AUTHORIZATION_CONTRACT_MISSING"
Require-False $result.QuoteFileContentsRead "Quote file contents were read." "EXEC_SIM_R010_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"
Require-False $result.ManifestContentsReadForValidation "Manifest contents were read for validation." "EXEC_SIM_R010_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"
Require-False $result.ValidationRunExecuted "Validation run executed." "EXEC_SIM_R010_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"
Require-False $result.ImportExecuted "Import executed." "EXEC_SIM_R010_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"
Require-False $result.BacktestExecuted "Backtest executed." "EXEC_SIM_R010_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"
Require-False $result.SanitizedQuoteRowsCreated "Sanitized quote rows created." "EXEC_SIM_R010_FAIL_SANITIZED_QUOTE_ROWS_CREATED"

Require-True $diagnostics.operatorFilePathsFound "Diagnostics do not show operator file paths present." "EXEC_SIM_R010_FAIL_MISSING_INPUT_DIAGNOSTICS_MISSING"
Require-True $diagnostics.operatorManifestPathsOrMetadataFound "Diagnostics do not show manifests present." "EXEC_SIM_R010_FAIL_MISSING_INPUT_DIAGNOSTICS_MISSING"
Require-False $diagnostics.r009TemplatesOnlyFound "Diagnostics still treat inputs as R009 templates only." "EXEC_SIM_R010_FAIL_MISSING_INPUT_DIAGNOSTICS_MISSING"
Require-False $diagnostics.safeStop "Diagnostics still indicate missing-input safe stop." "EXEC_SIM_R010_FAIL_MISSING_INPUT_DIAGNOSTICS_MISSING"
Require-True $diagnostics.validationImportBacktestNotAttempted "Diagnostics do not confirm no execution." "EXEC_SIM_R010_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"

if (@($result.MissingFileEntries).Count -ne 0 -or @($result.MissingManifestEntries).Count -ne 0) {
    Fail "Authorization-ready result still contains missing file or manifest entries." "EXEC_SIM_R010_FAIL_MISSING_INPUT_DIAGNOSTICS_MISSING"
}

$authorizedRows = @($result.AuthorizedFileEntries)
foreach ($expected in @(
    @{ Symbol = "EURUSD"; RowCount = 54694; Quote = "eurusd-20260519120000-20260519160000.ndjson"; Manifest = "eurusd-20260519120000-20260519160000.manifest.json" },
    @{ Symbol = "USDJPY"; RowCount = 59368; Quote = "usdjpy-20260519120000-20260519160000.ndjson"; Manifest = "usdjpy-20260519120000-20260519160000.manifest.json" },
    @{ Symbol = "AUDUSD"; RowCount = 60656; Quote = "audusd-20260519120000-20260519160000.ndjson"; Manifest = "audusd-20260519120000-20260519160000.manifest.json" }
)) {
    $entry = $authorizedRows | Where-Object { $_.ExecutionTradableSymbol -eq $expected.Symbol } | Select-Object -First 1
    if ($null -eq $entry) {
        Fail "Authorized file entries missing $($expected.Symbol)." "EXEC_SIM_R010_FAIL_AUTHORIZATION_CONTRACT_MISSING"
    }
    if ($entry.RowCountDeclared -ne $expected.RowCount) {
        Fail "Authorized $($expected.Symbol) row count does not match operator-observed metadata." "EXEC_SIM_R010_FAIL_AUTHORIZATION_CONTRACT_MISSING"
    }
    if ($entry.FileFormat -ne "NDJSON") {
        Fail "Authorized $($expected.Symbol) file format is not NDJSON." "EXEC_SIM_R010_FAIL_AUTHORIZATION_CONTRACT_MISSING"
    }
    Require-False $entry.ContainsSecrets "Authorized $($expected.Symbol) entry is marked as containing secrets." "EXEC_SIM_R010_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    Require-False $entry.ContainsRawProviderPayload "Authorized $($expected.Symbol) entry is marked as containing raw provider payload." "EXEC_SIM_R010_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    if ($entry.QuoteFilePath -notmatch [regex]::Escape($expected.Quote)) {
        Fail "Authorized $($expected.Symbol) quote file path does not match supplied path." "EXEC_SIM_R010_FAIL_AUTHORIZATION_CONTRACT_MISSING"
    }
    if ($entry.ManifestPath -notmatch [regex]::Escape($expected.Manifest)) {
        Fail "Authorized $($expected.Symbol) manifest path does not match supplied path." "EXEC_SIM_R010_FAIL_AUTHORIZATION_CONTRACT_MISSING"
    }
    if (-not (Test-Path -LiteralPath $entry.QuoteFilePath)) {
        Fail "Authorized $($expected.Symbol) quote file path is not present." "EXEC_SIM_R010_FAIL_AUTHORIZATION_CONTRACT_MISSING"
    }
    if (-not (Test-Path -LiteralPath $entry.ManifestPath)) {
        Fail "Authorized $($expected.Symbol) manifest path is not present." "EXEC_SIM_R010_FAIL_AUTHORIZATION_CONTRACT_MISSING"
    }
}

$symbolRows = @($symbols.requiredSymbols)
if (-not ($symbolRows | Where-Object { $_.executionTradableSymbol -eq "USDJPY" -and $_.normalizedPortfolioSymbol -eq "JPYUSD" -and $_.requiresInversion -eq $true -and $_.securityId -eq "4004" -and $_.securityIdSource -eq "8" })) {
    Fail "USDJPY inversion or caveat weakened." "EXEC_SIM_R010_FAIL_USDJPY_CAVEAT_WEAKENED"
}
if (-not ($symbolRows | Where-Object { $_.executionTradableSymbol -eq "AUDUSD" -and $_.audusdStatus -match "not failed" })) {
    Fail "AUDUSD incorrectly marked failed." "EXEC_SIM_R010_FAIL_AUDUSD_MISCLASSIFIED"
}

if ($cost.bestCaseMajorTargetUsdPerMillion -ne 5) {
    Fail "Best-case major target is not 5 USD/million." "EXEC_SIM_R010_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
}
Require-True $cost.fiveUsdPerMillionBestCaseOnly "5 USD/million not marked best-case only." "EXEC_SIM_R010_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-False $cost.fiveUsdPerMillionUniversalized "5 USD/million universalized." "EXEC_SIM_R010_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-True $cost.nonMajorEmScandiCnhRequireLiquidityCalibration "Non-major calibration guidance missing." "EXEC_SIM_R010_FAIL_5USD_PER_MILLION_UNIVERSALIZED"

if ($directCross.executionUniverse -ne "USD-pair-only") {
    Fail "Execution universe is not USD-pair-only." "EXEC_SIM_R010_FAIL_DIRECT_CROSS_GUIDANCE_WEAKENED"
}
Require-True $directCross.rawQubesCrossesAreSignalInputsOnly "Direct-cross signal-only rule weakened." "EXEC_SIM_R010_FAIL_DIRECT_CROSS_GUIDANCE_WEAKENED"
Require-True $directCross.requiresNettingFirst "Direct-cross netting-first rule weakened." "EXEC_SIM_R010_FAIL_DIRECT_CROSS_GUIDANCE_WEAKENED"
Require-False $directCross.directCrossExecutionAllowedByDefault "Direct-cross execution allowed by default." "EXEC_SIM_R010_FAIL_DIRECT_CROSS_GUIDANCE_WEAKENED"
Require-True $directCross.directCrossExecutionDisabledByDefault "Direct-cross disabled default missing." "EXEC_SIM_R010_FAIL_DIRECT_CROSS_GUIDANCE_WEAKENED"
Require-False $directCross.guidanceWeakened "Direct-cross guidance weakened." "EXEC_SIM_R010_FAIL_DIRECT_CROSS_GUIDANCE_WEAKENED"

Require-False $noExecution.validationRunExecuted "Validation executed." "EXEC_SIM_R010_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"
Require-False $noExecution.importExecuted "Import executed." "EXEC_SIM_R010_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"
Require-False $noExecution.backtestExecuted "Backtest executed." "EXEC_SIM_R010_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"
Require-False $noExecution.filesProcessed "Files processed." "EXEC_SIM_R010_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"
Require-False $noRows.sanitizedQuoteRowsCreated "Sanitized quote rows created." "EXEC_SIM_R010_FAIL_SANITIZED_QUOTE_ROWS_CREATED"
Require-False $noRows.quoteWindowsCreated "Quote windows created." "EXEC_SIM_R010_FAIL_SANITIZED_QUOTE_ROWS_CREATED"
Require-False $noRows.closeBenchmarksCreated "Close benchmarks created." "EXEC_SIM_R010_FAIL_SANITIZED_QUOTE_ROWS_CREATED"

Require-False $polygon.polygonApiCalled "Polygon API called." "EXEC_SIM_R010_FAIL_API_CALL_DETECTED"
Require-False $lmaxAudit.lmaxCalled "LMAX called." "EXEC_SIM_R010_FAIL_API_CALL_DETECTED"
Require-False $api.externalApiCalled "External API called." "EXEC_SIM_R010_FAIL_API_CALL_DETECTED"
Require-False $runtime.brokerActivationDetected "Broker activation detected." "EXEC_SIM_R010_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.socketOpened "Socket opened." "EXEC_SIM_R010_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.tlsOpened "TLS opened." "EXEC_SIM_R010_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.fixOpened "FIX opened." "EXEC_SIM_R010_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataRequestSent "MarketDataRequest sent." "EXEC_SIM_R010_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataResponseRead "MarketDataResponse read." "EXEC_SIM_R010_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.apiWorkerLiveGatewayEnabled "API/Worker live gateway enabled." "EXEC_SIM_R010_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.schedulerServiceTimerPollingBackgroundJobIntroduced "Scheduler/service/timer/polling/background job introduced." "EXEC_SIM_R010_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $runtime.automaticExecutionIntroduced "Automatic execution introduced." "EXEC_SIM_R010_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $order.ordersCreated "Orders created." "EXEC_SIM_R010_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.executableOrdersCreated "Executable orders created." "EXEC_SIM_R010_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.fillsCreated "Fills created." "EXEC_SIM_R010_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $order.executionReportsCreated "Execution reports created." "EXEC_SIM_R010_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $order.routesCreated "Routes created." "EXEC_SIM_R010_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.submissionsCreated "Submissions created." "EXEC_SIM_R010_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

Require-True $usdjpy.caveatPreserved "USDJPY caveat not preserved." "EXEC_SIM_R010_FAIL_USDJPY_CAVEAT_WEAKENED"
if ($usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8") {
    Fail "USDJPY SecurityID/SecurityIDSource weakened." "EXEC_SIM_R010_FAIL_USDJPY_CAVEAT_WEAKENED"
}
if ($lmaxBaseline.audusdStatus -notmatch "inconclusive" -or ($lmaxBaseline.audusdStatus -match "failed" -and $lmaxBaseline.audusdStatus -notmatch "not failed")) {
    Fail "AUDUSD incorrectly marked failed." "EXEC_SIM_R010_FAIL_AUDUSD_MISCLASSIFIED"
}
Require-False $lmaxBaseline.lmaxCalledInR010 "LMAX baseline called in R010." "EXEC_SIM_R010_FAIL_API_CALL_DETECTED"

Require-False $noExternal.polygonApiCalled "No-external audit shows Polygon call." "EXEC_SIM_R010_FAIL_API_CALL_DETECTED"
Require-False $noExternal.lmaxCalled "No-external audit shows LMAX call." "EXEC_SIM_R010_FAIL_API_CALL_DETECTED"
Require-False $noExternal.externalApiCalled "No-external audit shows external API call." "EXEC_SIM_R010_FAIL_API_CALL_DETECTED"
Require-False $noExternal.socketTlsFixMarketDataRuntimeDetected "No-external audit shows market-data runtime." "EXEC_SIM_R010_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $noExternal.validationImportBacktestExecuted "No-external audit shows validation/import/backtest." "EXEC_SIM_R010_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"
Require-False $noExternal.sanitizedQuoteRowsCreated "No-external audit shows sanitized rows." "EXEC_SIM_R010_FAIL_SANITIZED_QUOTE_ROWS_CREATED"
Require-False $noExternal.ordersFillsReportsRoutesSubmissionsCreated "No-external audit shows order-domain action." "EXEC_SIM_R010_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.rawPayloadSerialized "No-external audit shows raw payload serialization." "EXEC_SIM_R010_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-False $noExternal.secretMaterialSerialized "No-external audit shows secret serialization." "EXEC_SIM_R010_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-False $forbidden.forbiddenActionsDetected "Forbidden action detected." "EXEC_SIM_R010_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-False $forbidden.validationImportBacktestExecuted "Forbidden audit shows validation/import/backtest." "EXEC_SIM_R010_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"
Require-False $forbidden.sanitizedQuoteRowsCreated "Forbidden audit shows sanitized rows." "EXEC_SIM_R010_FAIL_SANITIZED_QUOTE_ROWS_CREATED"

if ($evidence.dotnetBuildNoRestore -ne "PASS") {
    Fail "dotnet build --no-restore evidence is not PASS." "EXEC_SIM_R010_FAIL_BUILD_OR_TESTS"
}
if ($evidence.focusedTests -notmatch "^PASS") {
    Fail "Focused R010 tests evidence is not PASS." "EXEC_SIM_R010_FAIL_BUILD_OR_TESTS"
}
if ($evidence.unitTests -notmatch "^PASS") {
    Fail "Unit test evidence is not PASS." "EXEC_SIM_R010_FAIL_BUILD_OR_TESTS"
}

Write-Host "EXEC_SIM_R010_PASS_FIRST_REAL_OFFLINE_BATCH_AUTHORIZATION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R010_PASS_FIRST_BATCH_PREFLIGHT_CONTRACT_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R010_PASS_NO_VALIDATION_IMPORT_BACKTEST_EXECUTION_GATE_READY_NO_EXTERNAL"
exit 0
