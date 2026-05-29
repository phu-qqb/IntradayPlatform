param(
    [string]$ArtifactsDir = "artifacts/readiness/execution-sim"
)

$ErrorActionPreference = "Stop"

function Fail-Gate {
    param([string]$Classification, [string]$Message)
    Write-Host $Classification
    throw $Message
}

function Read-Json {
    param([string]$Path, [string]$FailureClassification)
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail-Gate $FailureClassification "Required artifact is missing: $Path"
    }
    try {
        return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    }
    catch {
        Fail-Gate $FailureClassification "Artifact is not valid JSON: $Path"
    }
}

function Require-True {
    param([bool]$Value, [string]$FailureClassification, [string]$Message)
    if (-not $Value) { Fail-Gate $FailureClassification $Message }
}

function Require-False {
    param([bool]$Value, [string]$FailureClassification, [string]$Message)
    if ($Value) { Fail-Gate $FailureClassification $Message }
}

function Require-Contains {
    param([object[]]$Values, [string]$Expected, [string]$FailureClassification, [string]$Message)
    if ($Expected -notin $Values) { Fail-Gate $FailureClassification $Message }
}

$requiredArtifacts = @(
    "phase-exec-sim-r027-summary.md",
    "phase-exec-sim-r027-r026-data-expansion-decision-reference.json",
    "phase-exec-sim-r027-historical-window-expansion-authorization-contract.json",
    "phase-exec-sim-r027-historical-window-expansion-request.json",
    "phase-exec-sim-r027-historical-window-expansion-preflight-contract.json",
    "phase-exec-sim-r027-authorization-result.json",
    "phase-exec-sim-r027-required-symbols.json",
    "phase-exec-sim-r027-required-session-window-categories.json",
    "phase-exec-sim-r027-file-entry-requirements.json",
    "phase-exec-sim-r027-accepted-for-authorization-entries.json",
    "phase-exec-sim-r027-missing-input-diagnostics.json",
    "phase-exec-sim-r027-inversion-preservation.json",
    "phase-exec-sim-r027-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r027-cost-guidance-preservation.json",
    "phase-exec-sim-r027-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r027-no-download-audit.json",
    "phase-exec-sim-r027-no-row-validation-audit.json",
    "phase-exec-sim-r027-no-db-import-audit.json",
    "phase-exec-sim-r027-no-sanitized-row-audit.json",
    "phase-exec-sim-r027-no-backtest-simulation-audit.json",
    "phase-exec-sim-r027-no-tca-result-lines-audit.json",
    "phase-exec-sim-r027-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r027-no-polygon-api-call-audit.json",
    "phase-exec-sim-r027-no-lmax-call-audit.json",
    "phase-exec-sim-r027-no-external-api-call-audit.json",
    "phase-exec-sim-r027-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r027-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r027-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r027-no-external-audit.json",
    "phase-exec-sim-r027-forbidden-actions-audit.json",
    "phase-exec-sim-r027-next-phase-recommendation.json",
    "phase-exec-sim-r027-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsDir $artifact))) {
        Fail-Gate "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "Required R027 artifact missing: $artifact"
    }
}

$reference = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r027-r026-data-expansion-decision-reference.json") "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING"
$contract = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r027-historical-window-expansion-authorization-contract.json") "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING"
$request = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r027-historical-window-expansion-request.json") "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING"
$preflight = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r027-historical-window-expansion-preflight-contract.json") "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING"
$result = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r027-authorization-result.json") "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING"
$symbols = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r027-required-symbols.json") "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING"
$categories = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r027-required-session-window-categories.json") "EXEC_SIM_R027_FAIL_SESSION_WINDOW_REQUIREMENTS_MISSING"
$requirements = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r027-file-entry-requirements.json") "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING"
$accepted = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r027-accepted-for-authorization-entries.json") "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING"
$diagnostics = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r027-missing-input-diagnostics.json") "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING"
$inversion = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r027-inversion-preservation.json") "EXEC_SIM_R027_FAIL_USDJPY_CAVEAT_WEAKENED"
$direct = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r027-direct-cross-exclusion-preservation.json") "EXEC_SIM_R027_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED"
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r027-cost-guidance-preservation.json") "EXEC_SIM_R027_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$nonmajor = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r027-nonmajor-calibration-preservation.json") "EXEC_SIM_R027_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$noDownload = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r027-no-download-audit.json") "EXEC_SIM_R027_FAIL_DOWNLOAD_EXECUTED"
$noRows = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r027-no-row-validation-audit.json") "EXEC_SIM_R027_FAIL_ROW_VALIDATION_EXECUTED"
$noDb = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r027-no-db-import-audit.json") "EXEC_SIM_R027_FAIL_DB_IMPORT_OCCURRED"
$noSanitized = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r027-no-sanitized-row-audit.json") "EXEC_SIM_R027_FAIL_DB_IMPORT_OCCURRED"
$noBacktest = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r027-no-backtest-simulation-audit.json") "EXEC_SIM_R027_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
$noTca = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r027-no-tca-result-lines-audit.json") "EXEC_SIM_R027_FAIL_TCA_RESULTS_PRODUCED"
$noOrder = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r027-no-order-fill-report-route-audit.json") "EXEC_SIM_R027_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$api = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r027-no-external-api-call-audit.json") "EXEC_SIM_R027_FAIL_API_CALL_DETECTED"
$runtime = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r027-no-broker-marketdata-runtime-audit.json") "EXEC_SIM_R027_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r027-usdjpy-caveat-preservation.json") "EXEC_SIM_R027_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r027-lmax-readonly-baseline-reference.json") "EXEC_SIM_R027_FAIL_API_CALL_DETECTED"
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r027-no-external-audit.json") "EXEC_SIM_R027_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r027-forbidden-actions-audit.json") "EXEC_SIM_R027_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r027-build-test-validator-evidence.json") "EXEC_SIM_R027_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$reference.r026DataExpansionDecisionReferenceCreated) "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "R026 reference missing."
if ($reference.SourceDecisionPhase -ne "EXEC-SIM-R026") { Fail-Gate "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "R026 source phase mismatch." }
Require-True ([bool]$reference.R026OpeningClosingWindowsRecommended) "EXEC_SIM_R027_FAIL_SESSION_WINDOW_REQUIREMENTS_MISSING" "R026 opening/closing decision missing."

Require-True ([bool]$contract.historicalWindowExpansionAuthorizationContractCreated) "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "Authorization contract missing."
Require-True ([bool]$contract.AuthorizationOnly) "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "Authorization-only flag missing."
Require-True ([bool]$contract.NoExternalApiCalls) "EXEC_SIM_R027_FAIL_API_CALL_DETECTED" "Contract allows external APIs."
Require-True ([bool]$contract.NoDownload) "EXEC_SIM_R027_FAIL_DOWNLOAD_EXECUTED" "Contract allows download."
Require-True ([bool]$contract.NoRowValidation) "EXEC_SIM_R027_FAIL_ROW_VALIDATION_EXECUTED" "Contract allows row validation."
Require-True ([bool]$contract.NoDbImport) "EXEC_SIM_R027_FAIL_DB_IMPORT_OCCURRED" "Contract allows DB import."
Require-True ([bool]$contract.NoBacktest) "EXEC_SIM_R027_FAIL_BACKTEST_OR_SIMULATION_EXECUTED" "Contract allows backtest."
Require-True ([bool]$contract.NoSimulation) "EXEC_SIM_R027_FAIL_BACKTEST_OR_SIMULATION_EXECUTED" "Contract allows simulation."
Require-True ([bool]$contract.NoTcaResultLines) "EXEC_SIM_R027_FAIL_TCA_RESULTS_PRODUCED" "Contract allows TCA lines."

Require-True ([bool]$request.historicalWindowExpansionRequestCreated) "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "Authorization request missing."
Require-True ([bool]$request.OperatorFileEntriesSupplied) "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "Operator entries were not supplied."
Require-False ([bool]$request.OperatorPlaceholdersSupplied) "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "Placeholder input remained in request."
if ($request.OperatorFileEntryCount -ne 14 -or $request.IntendedNextPhaseIfFilesSupplied -ne "EXEC-SIM-R028") {
    Fail-Gate "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "Request shape is inconsistent."
}
Require-True ([bool]$preflight.historicalWindowExpansionPreflightContractCreated) "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "Preflight contract missing."
Require-True ([bool]$preflight.PathPresenceCheckOnlyWhenSupplied) "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "Path presence rule missing."
Require-True ([bool]$preflight.ManifestContentValidationDeferredToR028) "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "R028 manifest deferral missing."
Require-True ([bool]$preflight.QuoteRowValidationDeferredPastR028) "EXEC_SIM_R027_FAIL_ROW_VALIDATION_EXECUTED" "Row validation deferral missing."

Require-True ([bool]$result.authorizationResultCreated) "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "Authorization result missing."
if ($result.AuthorizationStatus -ne "HistoricalWindowExpansionAuthorizationReadyNoExternal") { Fail-Gate "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "Expected authorization-ready status." }
if ($result.AdditionalStatus -ne "HistoricalWindowExpansionSessionWindowPreflightReadyNoExternal") { Fail-Gate "EXEC_SIM_R027_FAIL_SESSION_WINDOW_REQUIREMENTS_MISSING" "Expected session-window preflight status." }
Require-Contains @($result.Classifications) "EXEC_SIM_R027_PASS_HISTORICAL_WINDOW_EXPANSION_AUTHORIZATION_READY_NO_EXTERNAL" "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "Authorization pass classification missing."
Require-Contains @($result.Classifications) "EXEC_SIM_R027_PASS_SESSION_WINDOW_EXPANSION_PREFLIGHT_READY_NO_EXTERNAL" "EXEC_SIM_R027_FAIL_SESSION_WINDOW_REQUIREMENTS_MISSING" "Session preflight classification missing."
Require-Contains @($result.Classifications) "EXEC_SIM_R027_PASS_NO_DOWNLOAD_NO_BACKTEST_GATE_READY_NO_EXTERNAL" "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "No-download/no-backtest classification missing."
if ($result.AuthorizedEntryCount -ne 14 -or $result.BlockedEntryCount -ne 0) { Fail-Gate "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "Authorization result counts mismatch." }
Require-False ([bool]$result.SafeBlocked) "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "Authorization is unexpectedly blocked."
Require-True ([bool]$result.ReadyForR028) "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "R028 readiness missing."

Require-True ([bool]$symbols.requiredSymbolsCreated) "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "Required symbols missing."
if ($symbols.RequiredSymbolCount -ne 7 -or @($symbols.Symbols).Count -ne 7) { Fail-Gate "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "Required symbol count mismatch." }
if ($symbols.AudUsdStatus -ne "not failed") { Fail-Gate "EXEC_SIM_R027_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD misclassified." }
Require-True ([bool]$categories.requiredSessionWindowCategoriesCreated) "EXEC_SIM_R027_FAIL_SESSION_WINDOW_REQUIREMENTS_MISSING" "Session window categories missing."
Require-Contains @($categories.RequiredCategories) "OpeningBuild" "EXEC_SIM_R027_FAIL_SESSION_WINDOW_REQUIREMENTS_MISSING" "OpeningBuild requirement missing."
Require-Contains @($categories.RequiredCategories) "ClosingFlatten" "EXEC_SIM_R027_FAIL_SESSION_WINDOW_REQUIREMENTS_MISSING" "ClosingFlatten requirement missing."
Require-Contains @($categories.OptionalSupportedCategories) "IntradayRebalance" "EXEC_SIM_R027_FAIL_SESSION_WINDOW_REQUIREMENTS_MISSING" "Intraday support missing."
Require-Contains @($categories.OptionalSupportedCategories) "Mixed" "EXEC_SIM_R027_FAIL_SESSION_WINDOW_REQUIREMENTS_MISSING" "Mixed support missing."
Require-Contains @($categories.OptionalSupportedCategories) "Unknown" "EXEC_SIM_R027_FAIL_SESSION_WINDOW_REQUIREMENTS_MISSING" "Unknown support missing."
Require-True ([bool]$categories.NeedsOperatorDateRangesOrSessionTimes) "EXEC_SIM_R027_FAIL_SESSION_WINDOW_REQUIREMENTS_MISSING" "Needs operator input flag missing."

Require-True ([bool]$requirements.fileEntryRequirementsCreated) "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "File entry requirements missing."
foreach ($field in @("Symbol","ProviderSymbol","ExecutionTradableSymbol","NormalizedPortfolioSymbol","RequiresInversion","QuoteFilePath","ManifestPath","FileFormat","ProviderName","ProviderDatasetType","TimeRangeStartUtc","TimeRangeEndUtc","SessionWindowCategory","ContainsSecrets","ContainsRawProviderPayload")) {
    Require-Contains @($requirements.RequiredFields) $field "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "Missing required file-entry field $field."
}
if ($requirements.FileFormat -ne "NDJSON" -or $requirements.ProviderName -ne "PolygonOfflineFile" -or $requirements.ProviderDatasetType -ne "HistoricalBboQuotes") {
    Fail-Gate "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "File entry provider/format requirements mismatch."
}
Require-True ([bool]$requirements.ContainsSecretsMustBeFalse) "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "Secret flag rule missing."
Require-True ([bool]$requirements.ContainsRawProviderPayloadMustBeFalse) "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "Raw payload rule missing."
Require-False ([bool]$requirements.DirectCrossesAllowed) "EXEC_SIM_R027_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct crosses allowed."

Require-True ([bool]$accepted.acceptedForAuthorizationEntriesCreated) "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "Accepted entries artifact missing."
if ($accepted.AcceptedEntryCount -ne 14 -or @($accepted.Entries).Count -ne 14) { Fail-Gate "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "Accepted authorization entries missing." }
foreach ($entry in @($accepted.Entries)) {
    Require-True ([bool]$entry.QuoteFileExists) "EXEC_SIM_R027_BLOCKED_OPERATOR_FILE_PATHS_OR_MANIFESTS_MISSING_NO_EXTERNAL" "Authorized quote file path is missing."
    Require-True ([bool]$entry.ManifestExists) "EXEC_SIM_R027_BLOCKED_OPERATOR_FILE_PATHS_OR_MANIFESTS_MISSING_NO_EXTERNAL" "Authorized manifest path is missing."
    Require-True ([bool]$entry.PathPresenceCheckedOnly) "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "Path-presence-only flag missing."
    Require-False ([bool]$entry.QuoteRowsRead) "EXEC_SIM_R027_FAIL_ROW_VALIDATION_EXECUTED" "Quote rows were read."
    Require-False ([bool]$entry.ManifestContentRead) "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "Manifest contents were read."
    Require-False ([bool]$entry.ContainsSecrets) "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "Secret risk accepted."
    Require-False ([bool]$entry.ContainsRawProviderPayload) "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "Raw payload risk accepted."
}
Require-True ([bool]$diagnostics.missingInputDiagnosticsCreated) "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "Missing diagnostics missing."
if ($diagnostics.MissingDiagnosticsCount -ne 0 -or @($diagnostics.Diagnostics).Count -ne 0) { Fail-Gate "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "Unexpected missing diagnostics for authorized entries." }
Require-False ([bool]$diagnostics.MissingFilePathsBlockSafely) "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "Missing file path flag should be false."
Require-False ([bool]$diagnostics.MissingManifestPathsBlockSafely) "EXEC_SIM_R027_FAIL_AUTHORIZATION_MISSING" "Missing manifest flag should be false."
Require-False ([bool]$diagnostics.MissingDateRangesOrSessionTimesNeedOperatorInput) "EXEC_SIM_R027_FAIL_SESSION_WINDOW_REQUIREMENTS_MISSING" "Missing date/session flag should be false."

Require-True ([bool]$inversion.inversionPreservationCreated) "EXEC_SIM_R027_FAIL_USDJPY_CAVEAT_WEAKENED" "Inversion preservation missing."
if ($inversion.UsdJpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $inversion.UsdJpy.ExecutionTradableSymbol -ne "USDJPY" -or -not [bool]$inversion.UsdJpy.RequiresInversion) {
    Fail-Gate "EXEC_SIM_R027_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY inversion mapping weakened."
}
if ($inversion.UsdCad.NormalizedPortfolioSymbol -ne "CADUSD" -or -not [bool]$inversion.UsdCad.RequiresInversion) {
    Fail-Gate "EXEC_SIM_R027_FAIL_USDJPY_CAVEAT_WEAKENED" "USDCAD inversion mapping missing."
}
if ($inversion.UsdChf.NormalizedPortfolioSymbol -ne "CHFUSD" -or -not [bool]$inversion.UsdChf.RequiresInversion) {
    Fail-Gate "EXEC_SIM_R027_FAIL_USDJPY_CAVEAT_WEAKENED" "USDCHF inversion mapping missing."
}
Require-False ([bool]$inversion.AudUsdMisclassifiedFailed) "EXEC_SIM_R027_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD misclassified in inversion preservation."
Require-True ([bool]$direct.directCrossExclusionPreserved) "EXEC_SIM_R027_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct cross exclusion missing."
Require-False ([bool]$direct.directCrossEntriesAccepted) "EXEC_SIM_R027_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct cross entries accepted."
Require-False ([bool]$direct.directCrossExecutionAllowedByDefault) "EXEC_SIM_R027_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct cross execution allowed."
Require-False ([bool]$direct.directCrossExclusionWeakened) "EXEC_SIM_R027_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct cross exclusion weakened."
Require-True ([bool]$cost.fiveUsdPerMillionBestCaseMajorOnly) "EXEC_SIM_R027_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million best-case guidance missing."
Require-False ([bool]$cost.fiveUsdPerMillionUniversalized) "EXEC_SIM_R027_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million universalized."
Require-True ([bool]$nonmajor.nonMajorEmScandiCnhRequireLiquidityCalibration) "EXEC_SIM_R027_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "Nonmajor calibration missing."
Require-False ([bool]$nonmajor.calibrationRequirementWeakened) "EXEC_SIM_R027_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "Nonmajor calibration weakened."

Require-False ([bool]$noDownload.filesDownloaded) "EXEC_SIM_R027_FAIL_DOWNLOAD_EXECUTED" "Files downloaded."
Require-False ([bool]$noRows.quoteRowsValidated) "EXEC_SIM_R027_FAIL_ROW_VALIDATION_EXECUTED" "Rows validated."
Require-False ([bool]$noDb.quotesImportedIntoDb) "EXEC_SIM_R027_FAIL_DB_IMPORT_OCCURRED" "DB import occurred."
Require-False ([bool]$noSanitized.persistedSanitizedQuoteRowsCreated) "EXEC_SIM_R027_FAIL_DB_IMPORT_OCCURRED" "Sanitized rows created."
Require-False ([bool]$noBacktest.backtestExecuted) "EXEC_SIM_R027_FAIL_BACKTEST_OR_SIMULATION_EXECUTED" "Backtest executed."
Require-False ([bool]$noBacktest.simulationExecuted) "EXEC_SIM_R027_FAIL_BACKTEST_OR_SIMULATION_EXECUTED" "Simulation executed."
Require-False ([bool]$noTca.tcaResultLinesProduced) "EXEC_SIM_R027_FAIL_TCA_RESULTS_PRODUCED" "TCA result lines produced."
Require-False ([bool]$noOrder.ordersCreated) "EXEC_SIM_R027_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders created."
Require-False ([bool]$noOrder.fillsCreated) "EXEC_SIM_R027_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fills created."
Require-False ([bool]$noOrder.executionReportsCreated) "EXEC_SIM_R027_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Execution reports created."
Require-False ([bool]$noOrder.routesCreated) "EXEC_SIM_R027_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Routes created."
Require-False ([bool]$api.polygonApiCalled) "EXEC_SIM_R027_FAIL_API_CALL_DETECTED" "Polygon API called."
Require-False ([bool]$api.lmaxCalled) "EXEC_SIM_R027_FAIL_API_CALL_DETECTED" "LMAX called."
Require-False ([bool]$api.externalApiCalled) "EXEC_SIM_R027_FAIL_API_CALL_DETECTED" "External API called."
Require-False ([bool]$runtime.brokerActivationDetected) "EXEC_SIM_R027_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "Broker activation detected."
Require-False ([bool]$runtime.marketDataRequestSent) "EXEC_SIM_R027_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "MarketDataRequest sent."
Require-False ([bool]$runtime.marketDataResponseRead) "EXEC_SIM_R027_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "MarketDataResponse read."
Require-False ([bool]$runtime.schedulerServiceTimerPollingBackgroundJobIntroduced) "EXEC_SIM_R027_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler/service introduced."
Require-True ([bool]$usdjpy.usdjpyCaveatPreserved) "EXEC_SIM_R027_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
Require-False ([bool]$usdjpy.audusdMisclassifiedFailed) "EXEC_SIM_R027_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD misclassified."
Require-True ([bool]$lmax.referenceOnly) "EXEC_SIM_R027_FAIL_API_CALL_DETECTED" "LMAX reference-only missing."
Require-False ([bool]$lmax.lmaxCalledInR027) "EXEC_SIM_R027_FAIL_API_CALL_DETECTED" "LMAX called."
Require-False ([bool]$noExternal.filesDownloaded) "EXEC_SIM_R027_FAIL_DOWNLOAD_EXECUTED" "No-external audit detected download."
Require-False ([bool]$noExternal.externalApiCalled) "EXEC_SIM_R027_FAIL_API_CALL_DETECTED" "No-external audit detected external API."
Require-False ([bool]$noExternal.backtestExecuted) "EXEC_SIM_R027_FAIL_BACKTEST_OR_SIMULATION_EXECUTED" "No-external audit detected backtest."
Require-False ([bool]$noExternal.tcaResultLinesProduced) "EXEC_SIM_R027_FAIL_TCA_RESULTS_PRODUCED" "No-external audit detected TCA lines."
Require-False ([bool]$noExternal.ordersFillsReportsRoutesSubmissionsCreated) "EXEC_SIM_R027_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No-external audit detected order-domain output."
Require-False ([bool]$forbidden.forbiddenActionsDetected) "EXEC_SIM_R027_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected."

if ($evidence.dotnetBuildNoRestore -notlike "PASS*" -or $evidence.focusedR027Tests -notlike "PASS*" -or $evidence.unitTestsIfFeasible -notlike "PASS*" -or $evidence.validator -notlike "PASS*") {
    Fail-Gate "EXEC_SIM_R027_FAIL_BUILD_OR_TESTS" "Build/test/validator evidence is missing or not passing."
}

Write-Host "EXEC_SIM_R027_PASS_HISTORICAL_WINDOW_EXPANSION_AUTHORIZATION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R027_PASS_SESSION_WINDOW_EXPANSION_PREFLIGHT_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R027_PASS_NO_DOWNLOAD_NO_BACKTEST_GATE_READY_NO_EXTERNAL"
