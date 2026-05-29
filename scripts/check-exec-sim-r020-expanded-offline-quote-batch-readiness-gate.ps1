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
    "phase-exec-sim-r020-summary.md",
    "phase-exec-sim-r020-expanded-batch-readiness-contract.json",
    "phase-exec-sim-r020-expanded-batch-scope.json",
    "phase-exec-sim-r020-required-existing-symbols.json",
    "phase-exec-sim-r020-recommended-major-expansion-symbols.json",
    "phase-exec-sim-r020-deferred-calibration-symbols.json",
    "phase-exec-sim-r020-session-window-category-requirements.json",
    "phase-exec-sim-r020-historical-window-requirements.json",
    "phase-exec-sim-r020-manifest-requirements.json",
    "phase-exec-sim-r020-file-naming-guidance.json",
    "phase-exec-sim-r020-operator-download-guidance.md",
    "phase-exec-sim-r020-operator-download-guidance.json",
    "phase-exec-sim-r020-inversion-guidance.json",
    "phase-exec-sim-r020-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r020-cost-guidance-preservation.json",
    "phase-exec-sim-r020-readiness-statuses.json",
    "phase-exec-sim-r020-needs-operator-input.json",
    "phase-exec-sim-r020-no-download-audit.json",
    "phase-exec-sim-r020-no-validation-audit.json",
    "phase-exec-sim-r020-no-import-audit.json",
    "phase-exec-sim-r020-no-backtest-simulation-audit.json",
    "phase-exec-sim-r020-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r020-no-polygon-api-call-audit.json",
    "phase-exec-sim-r020-no-lmax-call-audit.json",
    "phase-exec-sim-r020-no-external-api-call-audit.json",
    "phase-exec-sim-r020-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r020-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r020-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r020-no-external-audit.json",
    "phase-exec-sim-r020-forbidden-actions-audit.json",
    "phase-exec-sim-r020-next-phase-recommendation.json",
    "phase-exec-sim-r020-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsDir $artifact))) {
        Fail "Required R020 artifact is missing: $artifact" "EXEC_SIM_R020_FAIL_READINESS_CONTRACT_MISSING"
    }
}

$contract = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r020-expanded-batch-readiness-contract.json") "EXEC_SIM_R020_FAIL_READINESS_CONTRACT_MISSING"
$scope = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r020-expanded-batch-scope.json") "EXEC_SIM_R020_FAIL_READINESS_CONTRACT_MISSING"
$existing = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r020-required-existing-symbols.json") "EXEC_SIM_R020_FAIL_SYMBOL_EXPANSION_PLAN_MISSING"
$major = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r020-recommended-major-expansion-symbols.json") "EXEC_SIM_R020_FAIL_SYMBOL_EXPANSION_PLAN_MISSING"
$deferred = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r020-deferred-calibration-symbols.json") "EXEC_SIM_R020_FAIL_SYMBOL_EXPANSION_PLAN_MISSING"
$session = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r020-session-window-category-requirements.json") "EXEC_SIM_R020_FAIL_SESSION_WINDOW_REQUIREMENTS_MISSING"
$historical = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r020-historical-window-requirements.json") "EXEC_SIM_R020_FAIL_SESSION_WINDOW_REQUIREMENTS_MISSING"
$manifest = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r020-manifest-requirements.json") "EXEC_SIM_R020_FAIL_READINESS_CONTRACT_MISSING"
$naming = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r020-file-naming-guidance.json") "EXEC_SIM_R020_FAIL_OPERATOR_GUIDANCE_MISSING"
$operatorGuidance = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r020-operator-download-guidance.json") "EXEC_SIM_R020_FAIL_OPERATOR_GUIDANCE_MISSING"
$inversion = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r020-inversion-guidance.json") "EXEC_SIM_R020_FAIL_READINESS_CONTRACT_MISSING"
$direct = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r020-direct-cross-exclusion-preservation.json") "EXEC_SIM_R020_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED"
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r020-cost-guidance-preservation.json") "EXEC_SIM_R020_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$statuses = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r020-readiness-statuses.json") "EXEC_SIM_R020_FAIL_READINESS_CONTRACT_MISSING"
$needsInput = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r020-needs-operator-input.json") "EXEC_SIM_R020_FAIL_READINESS_CONTRACT_MISSING"
$noDownload = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r020-no-download-audit.json") "EXEC_SIM_R020_FAIL_DOWNLOAD_OR_IMPORT_EXECUTED"
$noValidation = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r020-no-validation-audit.json") "EXEC_SIM_R020_FAIL_DOWNLOAD_OR_IMPORT_EXECUTED"
$noImport = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r020-no-import-audit.json") "EXEC_SIM_R020_FAIL_DOWNLOAD_OR_IMPORT_EXECUTED"
$noBacktest = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r020-no-backtest-simulation-audit.json") "EXEC_SIM_R020_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
$order = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r020-no-order-fill-report-route-audit.json") "EXEC_SIM_R020_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$api = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r020-no-external-api-call-audit.json") "EXEC_SIM_R020_FAIL_API_CALL_DETECTED"
$runtime = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r020-no-broker-marketdata-runtime-audit.json") "EXEC_SIM_R020_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r020-usdjpy-caveat-preservation.json") "EXEC_SIM_R020_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r020-lmax-readonly-baseline-reference.json") "EXEC_SIM_R020_FAIL_API_CALL_DETECTED"
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r020-no-external-audit.json") "EXEC_SIM_R020_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r020-forbidden-actions-audit.json") "EXEC_SIM_R020_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r020-build-test-validator-evidence.json") "EXEC_SIM_R020_FAIL_BUILD_OR_TESTS"

Require-True $contract.expandedBatchReadinessContractCreated "Expanded batch readiness contract missing." "EXEC_SIM_R020_FAIL_READINESS_CONTRACT_MISSING"
if ($contract.SourceRecommendationPhase -ne "EXEC-SIM-R019") { Fail "R019 recommendations not referenced." "EXEC_SIM_R020_FAIL_READINESS_CONTRACT_MISSING" }
Require-True $contract.AuthorizationOnly "Contract not authorization/readiness only." "EXEC_SIM_R020_FAIL_READINESS_CONTRACT_MISSING"
Require-True $contract.NoDownload "Contract allows downloads." "EXEC_SIM_R020_FAIL_DOWNLOAD_OR_IMPORT_EXECUTED"
Require-True $contract.NoValidation "Contract allows validation." "EXEC_SIM_R020_FAIL_DOWNLOAD_OR_IMPORT_EXECUTED"
Require-True $contract.NoImport "Contract allows import." "EXEC_SIM_R020_FAIL_DOWNLOAD_OR_IMPORT_EXECUTED"
Require-True $contract.NoBacktest "Contract allows backtest." "EXEC_SIM_R020_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
Require-True $contract.NoOrdersFillsReportsRoutes "Contract allows order-domain output." "EXEC_SIM_R020_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
foreach ($symbol in @("EURUSD", "USDJPY", "AUDUSD")) {
    if (@($contract.RequiredExistingSymbols) -notcontains $symbol) { Fail "Required existing symbol missing $symbol." "EXEC_SIM_R020_FAIL_SYMBOL_EXPANSION_PLAN_MISSING" }
}
foreach ($symbol in @("GBPUSD", "NZDUSD", "USDCAD", "USDCHF")) {
    if (@($contract.RequiredNewSymbols) -notcontains $symbol) { Fail "Recommended major symbol missing $symbol." "EXEC_SIM_R020_FAIL_SYMBOL_EXPANSION_PLAN_MISSING" }
}
foreach ($category in @("OpeningBuild", "IntradayRebalance", "ClosingFlatten")) {
    if (@($contract.RequiredWindowCategories) -notcontains $category) { Fail "Window category missing $category." "EXEC_SIM_R020_FAIL_SESSION_WINDOW_REQUIREMENTS_MISSING" }
}
if (@($contract.RequiredManifestFields) -notcontains "SessionWindowCategory") { Fail "Manifest missing session window category." "EXEC_SIM_R020_FAIL_READINESS_CONTRACT_MISSING" }

Require-True $scope.expandedBatchScopeCreated "Expanded scope missing." "EXEC_SIM_R020_FAIL_READINESS_CONTRACT_MISSING"
Require-True $scope.moreThanOneFourHourWindowRequired "More than one 4-hour window not required." "EXEC_SIM_R020_FAIL_SESSION_WINDOW_REQUIREMENTS_MISSING"
if ($scope.exactSessionTimesStatus -ne "NeedsOperatorInput") { Fail "NeedsOperatorInput missing for exact session times." "EXEC_SIM_R020_FAIL_READINESS_CONTRACT_MISSING" }
Require-True $existing.requiredExistingSymbolsCreated "Required existing symbols artifact missing." "EXEC_SIM_R020_FAIL_SYMBOL_EXPANSION_PLAN_MISSING"
Require-True $major.recommendedMajorExpansionSymbolsCreated "Major expansion symbols missing." "EXEC_SIM_R020_FAIL_SYMBOL_EXPANSION_PLAN_MISSING"
Require-True $major.majorUsdPairExpansionPlanReady "Major USD-pair plan not ready." "EXEC_SIM_R020_FAIL_SYMBOL_EXPANSION_PLAN_MISSING"
Require-True $deferred.deferredCalibrationSymbolsCreated "Deferred calibration symbols missing." "EXEC_SIM_R020_FAIL_SYMBOL_EXPANSION_PLAN_MISSING"
Require-True $deferred.requiresLiquidityCalibration "Deferred symbols do not require calibration." "EXEC_SIM_R020_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-True $session.sessionWindowCategoryRequirementsCreated "Session window requirements missing." "EXEC_SIM_R020_FAIL_SESSION_WINDOW_REQUIREMENTS_MISSING"
if (@($session.requiredCategories) -notcontains "OpeningBuild" -or @($session.requiredCategories) -notcontains "IntradayRebalance" -or @($session.requiredCategories) -notcontains "ClosingFlatten") { Fail "Session categories missing." "EXEC_SIM_R020_FAIL_SESSION_WINDOW_REQUIREMENTS_MISSING" }
if ($session.OpeningBuild.exactSessionTimeStatus -ne "NeedsOperatorInput") { Fail "NeedsOperatorInput missing for OpeningBuild session time." "EXEC_SIM_R020_FAIL_READINESS_CONTRACT_MISSING" }
Require-True $historical.historicalWindowRequirementsCreated "Historical window requirements missing." "EXEC_SIM_R020_FAIL_SESSION_WINDOW_REQUIREMENTS_MISSING"
Require-True $historical.requiresMoreThanOneFourHourWindow "Historical requirements allow one window only." "EXEC_SIM_R020_FAIL_SESSION_WINDOW_REQUIREMENTS_MISSING"
if ($historical.exactDateRangesStatus -ne "NeedsOperatorInput" -or $historical.exactSessionTimesStatus -ne "NeedsOperatorInput") { Fail "NeedsOperatorInput missing for date ranges/session times." "EXEC_SIM_R020_FAIL_READINESS_CONTRACT_MISSING" }
Require-True $manifest.manifestRequirementsCreated "Manifest requirements missing." "EXEC_SIM_R020_FAIL_READINESS_CONTRACT_MISSING"
if (@($manifest.requiredFields) -notcontains "SessionWindowCategory") { Fail "Manifest SessionWindowCategory missing." "EXEC_SIM_R020_FAIL_READINESS_CONTRACT_MISSING" }
Require-True $manifest.containsRawProviderPayloadMustBeFalse "Manifest raw-payload safety missing." "EXEC_SIM_R020_FAIL_READINESS_CONTRACT_MISSING"
Require-True $manifest.containsSecretsMustBeFalse "Manifest secret safety missing." "EXEC_SIM_R020_FAIL_READINESS_CONTRACT_MISSING"
Require-True $naming.fileNamingGuidanceCreated "File naming guidance missing." "EXEC_SIM_R020_FAIL_OPERATOR_GUIDANCE_MISSING"
if (@($naming.examples) -notcontains "usdcad-YYYYMMDDHHMMSS-YYYYMMDDHHMMSS.ndjson") { Fail "USDCAD file naming guidance missing." "EXEC_SIM_R020_FAIL_OPERATOR_GUIDANCE_MISSING" }
Require-True $operatorGuidance.operatorDownloadGuidanceCreated "Operator download guidance missing." "EXEC_SIM_R020_FAIL_OPERATOR_GUIDANCE_MISSING"
Require-True $operatorGuidance.codexMustNotCallPolygon "Operator guidance allows Codex Polygon call." "EXEC_SIM_R020_FAIL_OPERATOR_GUIDANCE_MISSING"
Require-True $operatorGuidance.apiKeyEnvironmentVariableOnly "API key env-var guidance missing." "EXEC_SIM_R020_FAIL_OPERATOR_GUIDANCE_MISSING"
Require-False $operatorGuidance.apiKeyInRepoAllowed "API key allowed in repo." "EXEC_SIM_R020_FAIL_OPERATOR_GUIDANCE_MISSING"
Require-False $operatorGuidance.rawUnsanitizedPayloadDumpsAllowed "Raw payload dumps allowed." "EXEC_SIM_R020_FAIL_OPERATOR_GUIDANCE_MISSING"

Require-True $inversion.inversionGuidanceCreated "Inversion guidance missing." "EXEC_SIM_R020_FAIL_READINESS_CONTRACT_MISSING"
Require-True $inversion.usdJpyCaveatPreserved "USDJPY caveat missing in inversion guidance." "EXEC_SIM_R020_FAIL_USDJPY_CAVEAT_WEAKENED"
Require-True $inversion.cadChfInversionGuidancePresent "USDCAD/USDCHF inversion guidance missing." "EXEC_SIM_R020_FAIL_READINESS_CONTRACT_MISSING"
Require-True $direct.directCrossExclusionPreserved "Direct-cross exclusion missing." "EXEC_SIM_R020_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED"
Require-False $direct.directCrossExecutionAllowedByDefault "Direct-cross execution allowed." "EXEC_SIM_R020_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED"
Require-False $direct.guidanceWeakened "Direct-cross guidance weakened." "EXEC_SIM_R020_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED"
if ($cost.bestCaseMajorTargetUsdPerMillion -ne 5) { Fail "5 USD/million target missing." "EXEC_SIM_R020_FAIL_5USD_PER_MILLION_UNIVERSALIZED" }
Require-True $cost.fiveUsdPerMillionBestCaseMajorOnly "5 USD/million not best-case major-only." "EXEC_SIM_R020_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-False $cost.fiveUsdPerMillionUniversalized "5 USD/million universalized." "EXEC_SIM_R020_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-True $cost.nonmajorEmScandiCnhRequireLiquidityCalibration "Nonmajor calibration missing." "EXEC_SIM_R020_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-True $statuses.readinessStatusesCreated "Readiness statuses missing." "EXEC_SIM_R020_FAIL_READINESS_CONTRACT_MISSING"
foreach ($status in @("ExpandedBatchReadinessReadyNoExternal", "ExpandedBatchNeedsOperatorDateRanges", "ExpandedBatchNeedsSessionTimes", "ExpandedBatchBlockedDirectCrossIncluded")) {
    if (@($statuses.statuses) -notcontains $status) { Fail "Readiness status missing $status." "EXEC_SIM_R020_FAIL_READINESS_CONTRACT_MISSING" }
}
Require-True $needsInput.needsOperatorInputCreated "NeedsOperatorInput artifact missing." "EXEC_SIM_R020_FAIL_READINESS_CONTRACT_MISSING"
Require-True $needsInput.NeedsOperatorInput "NeedsOperatorInput not marked." "EXEC_SIM_R020_FAIL_READINESS_CONTRACT_MISSING"

Require-False $noDownload.quoteFilesDownloaded "Quote files downloaded." "EXEC_SIM_R020_FAIL_DOWNLOAD_OR_IMPORT_EXECUTED"
Require-False $noValidation.quoteFilesValidated "Quote files validated." "EXEC_SIM_R020_FAIL_DOWNLOAD_OR_IMPORT_EXECUTED"
Require-False $noImport.quoteFilesImported "Quote files imported." "EXEC_SIM_R020_FAIL_DOWNLOAD_OR_IMPORT_EXECUTED"
Require-False $noBacktest.newBacktestExecuted "Backtest executed." "EXEC_SIM_R020_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
Require-False $noBacktest.newSimulationExecuted "Simulation executed." "EXEC_SIM_R020_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
Require-False $noBacktest.tcaResultLinesProduced "TCA result lines produced." "EXEC_SIM_R020_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
Require-False $order.ordersCreated "Orders created." "EXEC_SIM_R020_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.fillEntitiesCreated "Fills created." "EXEC_SIM_R020_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $order.executionReportEntitiesCreated "Execution reports created." "EXEC_SIM_R020_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $order.routesCreated "Routes created." "EXEC_SIM_R020_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $order.submissionsCreated "Submissions created." "EXEC_SIM_R020_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $api.polygonApiCalled "Polygon API called." "EXEC_SIM_R020_FAIL_API_CALL_DETECTED"
Require-False $api.lmaxCalled "LMAX called." "EXEC_SIM_R020_FAIL_API_CALL_DETECTED"
Require-False $api.externalApiCalled "External API called." "EXEC_SIM_R020_FAIL_API_CALL_DETECTED"
Require-False $runtime.brokerActivationDetected "Broker activation detected." "EXEC_SIM_R020_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.socketOpened "Socket opened." "EXEC_SIM_R020_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.tlsOpened "TLS opened." "EXEC_SIM_R020_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.fixOpened "FIX opened." "EXEC_SIM_R020_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataRequestSent "MarketDataRequest sent." "EXEC_SIM_R020_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.marketDataResponseRead "MarketDataResponse read." "EXEC_SIM_R020_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtime.schedulerServiceTimerPollingBackgroundJobIntroduced "Scheduler introduced." "EXEC_SIM_R020_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $runtime.automaticExecutionIntroduced "Automatic execution introduced." "EXEC_SIM_R020_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"

if ($usdjpy.PortfolioNormalizedSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY") { Fail "USDJPY symbol caveat weakened." "EXEC_SIM_R020_FAIL_USDJPY_CAVEAT_WEAKENED" }
Require-True $usdjpy.RequiresInversion "USDJPY inversion missing." "EXEC_SIM_R020_FAIL_USDJPY_CAVEAT_WEAKENED"
if ($usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8") { Fail "USDJPY caveat weakened." "EXEC_SIM_R020_FAIL_USDJPY_CAVEAT_WEAKENED" }
Require-False $usdjpy.audusdMisclassifiedFailed "AUDUSD misclassified failed." "EXEC_SIM_R020_FAIL_AUDUSD_MISCLASSIFIED"
Require-True $lmax.referenceOnly "LMAX reference weakened." "EXEC_SIM_R020_FAIL_API_CALL_DETECTED"
Require-False $lmax.lmaxCalledInR020 "LMAX called in R020." "EXEC_SIM_R020_FAIL_API_CALL_DETECTED"
if ($lmax.audusdStatus -notmatch "not failed") { Fail "AUDUSD incorrectly marked failed." "EXEC_SIM_R020_FAIL_AUDUSD_MISCLASSIFIED" }

Require-False $noExternal.polygonApiCalled "No-external audit shows Polygon call." "EXEC_SIM_R020_FAIL_API_CALL_DETECTED"
Require-False $noExternal.lmaxCalled "No-external audit shows LMAX call." "EXEC_SIM_R020_FAIL_API_CALL_DETECTED"
Require-False $noExternal.externalApiCalled "No-external audit shows external API call." "EXEC_SIM_R020_FAIL_API_CALL_DETECTED"
Require-False $noExternal.quoteFilesDownloaded "No-external audit shows download." "EXEC_SIM_R020_FAIL_DOWNLOAD_OR_IMPORT_EXECUTED"
Require-False $noExternal.quoteFilesValidated "No-external audit shows validation." "EXEC_SIM_R020_FAIL_DOWNLOAD_OR_IMPORT_EXECUTED"
Require-False $noExternal.quoteFilesImported "No-external audit shows import." "EXEC_SIM_R020_FAIL_DOWNLOAD_OR_IMPORT_EXECUTED"
Require-False $noExternal.newSimulationExecuted "No-external audit shows simulation." "EXEC_SIM_R020_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
Require-False $noExternal.newBacktestExecuted "No-external audit shows backtest." "EXEC_SIM_R020_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
Require-False $noExternal.ordersFillsReportsRoutesSubmissionsCreated "No-external audit shows order-domain output." "EXEC_SIM_R020_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.liveBrokerProductionTradingStateMutated "State mutated." "EXEC_SIM_R020_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.paperLedgerStateCommitted "Paper ledger committed." "EXEC_SIM_R020_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $forbidden.forbiddenActionsDetected "Forbidden action detected." "EXEC_SIM_R020_FAIL_NEW_EXTERNAL_ACTION_DETECTED"

if ($evidence.dotnetBuildNoRestore -ne "PASS") { Fail "dotnet build evidence is not PASS." "EXEC_SIM_R020_FAIL_BUILD_OR_TESTS" }
if ($evidence.focusedTests -notmatch "^PASS") { Fail "Focused R020 test evidence is not PASS." "EXEC_SIM_R020_FAIL_BUILD_OR_TESTS" }
if ($evidence.unitTests -notmatch "^PASS") { Fail "Unit test evidence is not PASS." "EXEC_SIM_R020_FAIL_BUILD_OR_TESTS" }

Write-Host "EXEC_SIM_R020_PASS_EXPANDED_OFFLINE_BATCH_READINESS_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R020_PASS_MAJOR_USD_PAIR_EXPANSION_PLAN_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R020_PASS_SESSION_WINDOW_REQUIREMENTS_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R020_PASS_NO_DOWNLOAD_NO_BACKTEST_GATE_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R020_NEEDS_OPERATOR_DATE_RANGES_OR_SESSION_TIMES_NO_EXTERNAL"
exit 0
