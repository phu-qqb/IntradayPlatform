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
        Fail "Missing artifact: $Path" "EXEC_SIM_R009_FAIL_BUILD_OR_TESTS"
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Read-Text([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing artifact: $Path" "EXEC_SIM_R009_FAIL_BUILD_OR_TESTS"
    }

    return Get-Content -LiteralPath $Path -Raw
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

function Require-ContainsText([string]$Text, [string]$Needle, [string]$Message, [string]$Classification) {
    if ($Text -notmatch [regex]::Escape($Needle)) {
        Fail $Message $Classification
    }
}

$requiredArtifacts = @(
    "phase-exec-sim-r009-summary.md",
    "phase-exec-sim-r009-operator-runbook.md",
    "phase-exec-sim-r009-operator-runbook.json",
    "phase-exec-sim-r009-first-data-batch-checklist.md",
    "phase-exec-sim-r009-first-data-batch-checklist.json",
    "phase-exec-sim-r009-manifest-template.json",
    "phase-exec-sim-r009-valid-manifest-example.json",
    "phase-exec-sim-r009-invalid-manifest-examples.json",
    "phase-exec-sim-r009-validation-interpretation-guide.md",
    "phase-exec-sim-r009-tca-interpretation-guide.md",
    "phase-exec-sim-r009-operator-decision-guide.md",
    "phase-exec-sim-r009-troubleshooting-guide.md",
    "phase-exec-sim-r009-forbidden-actions-checklist.md",
    "phase-exec-sim-r009-handoff-checklist.md",
    "phase-exec-sim-r009-direct-cross-guidance.json",
    "phase-exec-sim-r009-cost-bucket-guidance.json",
    "phase-exec-sim-r009-symbol-coverage-guidance.json",
    "phase-exec-sim-r009-no-api-call-audit.json",
    "phase-exec-sim-r009-no-lmax-call-audit.json",
    "phase-exec-sim-r009-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r009-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r009-no-validation-import-backtest-execution-audit.json",
    "phase-exec-sim-r009-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r009-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r009-no-external-audit.json",
    "phase-exec-sim-r009-forbidden-actions-audit.json",
    "phase-exec-sim-r009-next-phase-recommendation.json",
    "phase-exec-sim-r009-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsDir $artifact))) {
        Fail "Required R009 artifact is missing: $artifact" "EXEC_SIM_R009_FAIL_BUILD_OR_TESTS"
    }
}

$runbookMd = Read-Text (Join-Path $ArtifactsDir "phase-exec-sim-r009-operator-runbook.md")
$runbook = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r009-operator-runbook.json")
$batchMd = Read-Text (Join-Path $ArtifactsDir "phase-exec-sim-r009-first-data-batch-checklist.md")
$batch = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r009-first-data-batch-checklist.json")
$manifestTemplate = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r009-manifest-template.json")
$validManifest = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r009-valid-manifest-example.json")
$invalidManifests = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r009-invalid-manifest-examples.json")
$validationGuide = Read-Text (Join-Path $ArtifactsDir "phase-exec-sim-r009-validation-interpretation-guide.md")
$tcaGuide = Read-Text (Join-Path $ArtifactsDir "phase-exec-sim-r009-tca-interpretation-guide.md")
$decisionGuide = Read-Text (Join-Path $ArtifactsDir "phase-exec-sim-r009-operator-decision-guide.md")
$troubleshooting = Read-Text (Join-Path $ArtifactsDir "phase-exec-sim-r009-troubleshooting-guide.md")
$forbiddenChecklist = Read-Text (Join-Path $ArtifactsDir "phase-exec-sim-r009-forbidden-actions-checklist.md")
$handoff = Read-Text (Join-Path $ArtifactsDir "phase-exec-sim-r009-handoff-checklist.md")
$directCross = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r009-direct-cross-guidance.json")
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r009-cost-bucket-guidance.json")
$symbols = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r009-symbol-coverage-guidance.json")
$apiAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r009-no-api-call-audit.json")
$lmaxAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r009-no-lmax-call-audit.json")
$runtimeAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r009-no-broker-marketdata-runtime-audit.json")
$orderAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r009-no-order-fill-report-route-audit.json")
$executionAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r009-no-validation-import-backtest-execution-audit.json")
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r009-usdjpy-caveat-preservation.json")
$lmaxBaseline = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r009-lmax-readonly-baseline-reference.json")
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r009-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r009-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r009-build-test-validator-evidence.json")

Require-ContainsText $runbookMd "This runbook does not authorize API calls" "Runbook does not state no API calls." "EXEC_SIM_R009_FAIL_OPERATOR_RUNBOOK_MISSING"
Require-ContainsText $runbookMd "Do not call Polygon, LMAX, or any external API" "Runbook does not forbid Polygon/LMAX/external API calls." "EXEC_SIM_R009_FAIL_API_CALL_DETECTED"
Require-ContainsText $runbookMd "Do not execute trades" "Runbook does not forbid trades." "EXEC_SIM_R009_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-ContainsText $runbookMd "Do not use CLI automation" "Runbook does not forbid automation." "EXEC_SIM_R009_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-True $runbook.statesNoApiCalls "Runbook JSON does not state no API calls." "EXEC_SIM_R009_FAIL_OPERATOR_RUNBOOK_MISSING"
Require-True $runbook.statesNoBrokerOrLmaxCalls "Runbook JSON does not state no broker/LMAX calls." "EXEC_SIM_R009_FAIL_OPERATOR_RUNBOOK_MISSING"
Require-True $runbook.statesNoOrdersFillsReportsRoutesSubmissions "Runbook JSON does not state no order-domain actions." "EXEC_SIM_R009_FAIL_OPERATOR_RUNBOOK_MISSING"
Require-True $runbook.statesNoAutomaticExecution "Runbook JSON does not state no automatic execution." "EXEC_SIM_R009_FAIL_OPERATOR_RUNBOOK_MISSING"
Require-True $runbook.statesNoValidationImportBacktestExecutionInR009 "Runbook JSON does not state no R009 validation/import/backtest execution." "EXEC_SIM_R009_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"

Require-ContainsText $batchMd "EURUSD historical BBO quote file" "First data batch checklist missing EURUSD." "EXEC_SIM_R009_FAIL_DATA_BATCH_CHECKLIST_MISSING"
Require-ContainsText $batchMd "USDJPY historical BBO quote file" "First data batch checklist missing USDJPY." "EXEC_SIM_R009_FAIL_DATA_BATCH_CHECKLIST_MISSING"
Require-ContainsText $batchMd "AUDUSD historical BBO quote file" "First data batch checklist missing AUDUSD." "EXEC_SIM_R009_FAIL_DATA_BATCH_CHECKLIST_MISSING"
foreach ($symbol in @("EURUSD", "USDJPY", "AUDUSD")) {
    if (@($batch.minimumRequiredFiles) -notcontains $symbol) {
        Fail "First data batch JSON missing $symbol." "EXEC_SIM_R009_FAIL_DATA_BATCH_CHECKLIST_MISSING"
    }
}
Require-True $batch.requiredCoverage.eachWindowTMinus13ToClose "First data batch checklist missing T-minus-13-to-close coverage." "EXEC_SIM_R009_FAIL_DATA_BATCH_CHECKLIST_MISSING"
Require-True $batch.noDataBatchProcessedInR009 "First data batch artifact indicates processing in R009." "EXEC_SIM_R009_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"

if ($manifestTemplate.ProviderName -ne "PolygonOfflineFile" -or $manifestTemplate.ProviderDatasetType -ne "HistoricalBboQuotes") {
    Fail "Manifest template missing provider/dataset type." "EXEC_SIM_R009_FAIL_OPERATOR_RUNBOOK_MISSING"
}
Require-False $manifestTemplate.ContainsRawProviderPayload "Manifest template allows raw payload." "EXEC_SIM_R009_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-False $manifestTemplate.ContainsSecrets "Manifest template allows secrets." "EXEC_SIM_R009_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
if ($validManifest.example.ProviderSymbol -ne "C:EUR-USD" -or $validManifest.example.ExecutionTradableSymbol -ne "EURUSD") {
    Fail "Valid manifest example missing EURUSD shape." "EXEC_SIM_R009_FAIL_OPERATOR_RUNBOOK_MISSING"
}
Require-True $invalidManifests.noInvalidExampleIsExecutable "Invalid manifest examples are executable." "EXEC_SIM_R009_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

foreach ($status in @("AcceptedForSanitizedImport", "QuarantinedMalformedFile", "QuarantinedUnsupportedSymbol", "QuarantinedDirectCrossExecutionDisabled", "QuarantinedMissingTimestamp", "QuarantinedMissingBidAsk", "QuarantinedInvalidBidAsk", "QuarantinedSecretLeakRisk", "QuarantinedRawPayloadLeakRisk", "DuplicateReturned", "InconclusiveSafe")) {
    Require-ContainsText $validationGuide $status "Validation interpretation guide missing $status." "EXEC_SIM_R009_FAIL_OPERATOR_RUNBOOK_MISSING"
}
foreach ($term in @("Slippage vs close", "USD per million", "Fill ratio", "Spread paid", "Wakett baselines", "CloseSeeking15mAdaptive", "ControlledResidualCross")) {
    Require-ContainsText $tcaGuide $term "TCA interpretation guide missing $term." "EXEC_SIM_R009_FAIL_TCA_GUIDE_MISSING"
}
Require-ContainsText $decisionGuide "Do not approve" "Operator decision guide missing do-not-approve guidance." "EXEC_SIM_R009_FAIL_OPERATOR_RUNBOOK_MISSING"
Require-ContainsText $troubleshooting "Missing timestamp" "Troubleshooting guide missing missing timestamp case." "EXEC_SIM_R009_FAIL_OPERATOR_RUNBOOK_MISSING"
Require-ContainsText $troubleshooting "TCA report missing policy results" "Troubleshooting guide missing TCA missing policy results case." "EXEC_SIM_R009_FAIL_TCA_GUIDE_MISSING"
Require-ContainsText $forbiddenChecklist "No Polygon API call" "Forbidden checklist missing Polygon API." "EXEC_SIM_R009_FAIL_OPERATOR_RUNBOOK_MISSING"
Require-ContainsText $forbiddenChecklist "No validation/import/backtest execution in R009" "Forbidden checklist missing no R009 execution." "EXEC_SIM_R009_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"
Require-ContainsText $handoff "Next phase explicitly authorized" "Handoff checklist missing next phase authorization." "EXEC_SIM_R009_FAIL_OPERATOR_RUNBOOK_MISSING"

Require-True $directCross.rawDirectCrossesAreSignalInputsOnly "Direct-cross guidance does not preserve signal-only." "EXEC_SIM_R009_FAIL_DIRECT_CROSS_GUIDANCE_WEAKENED"
Require-True $directCross.requiresNettingFirst "Direct-cross guidance does not require netting first." "EXEC_SIM_R009_FAIL_DIRECT_CROSS_GUIDANCE_WEAKENED"
Require-False $directCross.directCrossExecutionAllowedByDefault "Direct-cross execution allowed by default." "EXEC_SIM_R009_FAIL_DIRECT_CROSS_GUIDANCE_WEAKENED"
Require-True $directCross.futureEnablementRequiresExplicitCostComparisonGate "Direct-cross future gate missing." "EXEC_SIM_R009_FAIL_DIRECT_CROSS_GUIDANCE_WEAKENED"
Require-False $directCross.guidanceWeakened "Direct-cross guidance weakened." "EXEC_SIM_R009_FAIL_DIRECT_CROSS_GUIDANCE_WEAKENED"

if ($cost.bestCaseMajorTargetUsdPerMillion -ne 5) {
    Fail "Best-case major cost target is not 5 USD/million." "EXEC_SIM_R009_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
}
Require-True $cost.fiveUsdPerMillionBestCaseOnly "5 USD/million is not marked best-case only." "EXEC_SIM_R009_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-False $cost.fiveUsdPerMillionUniversalized "5 USD/million universalized." "EXEC_SIM_R009_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
Require-True $cost.nonMajorEmScandiCnhRequireLiquidityCalibration "Non-major/EM/scandi/CNH calibration guidance missing." "EXEC_SIM_R009_FAIL_5USD_PER_MILLION_UNIVERSALIZED"

if ($symbols.executionUniverse -ne "USD-pair-only") {
    Fail "Symbol coverage guidance weakens USD-pair universe." "EXEC_SIM_R009_FAIL_DIRECT_CROSS_GUIDANCE_WEAKENED"
}
Require-True $symbols.rawQubesCrossesSignalOnly "Raw Qubes cross signal-only guidance missing." "EXEC_SIM_R009_FAIL_DIRECT_CROSS_GUIDANCE_WEAKENED"
Require-True $symbols.directCrossExecutionDisabledByDefault "Direct-cross execution disabled guidance missing." "EXEC_SIM_R009_FAIL_DIRECT_CROSS_GUIDANCE_WEAKENED"

Require-False $apiAudit.polygonApiCalled "Polygon API called." "EXEC_SIM_R009_FAIL_API_CALL_DETECTED"
Require-False $apiAudit.lmaxCalled "LMAX called in API audit." "EXEC_SIM_R009_FAIL_API_CALL_DETECTED"
Require-False $apiAudit.externalApiCalled "External API called." "EXEC_SIM_R009_FAIL_API_CALL_DETECTED"
Require-False $lmaxAudit.lmaxCalled "LMAX called." "EXEC_SIM_R009_FAIL_API_CALL_DETECTED"
Require-False $runtimeAudit.brokerActivationDetected "Broker activation detected." "EXEC_SIM_R009_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.socketOpened "Socket opened." "EXEC_SIM_R009_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.tlsOpened "TLS opened." "EXEC_SIM_R009_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.fixOpened "FIX opened." "EXEC_SIM_R009_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.marketDataRequestSent "MarketDataRequest sent." "EXEC_SIM_R009_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.marketDataResponseRead "MarketDataResponse read." "EXEC_SIM_R009_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.apiWorkerLiveGatewayEnabled "API/Worker live gateway enabled." "EXEC_SIM_R009_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.schedulerServiceTimerPollingBackgroundJobIntroduced "Scheduler/service/timer/polling/background job introduced." "EXEC_SIM_R009_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $runtimeAudit.automaticExecutionIntroduced "Automatic execution introduced." "EXEC_SIM_R009_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $orderAudit.ordersCreated "Order created." "EXEC_SIM_R009_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $orderAudit.executableOrdersCreated "Executable order created." "EXEC_SIM_R009_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $orderAudit.fillsCreated "Fill created." "EXEC_SIM_R009_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $orderAudit.executionReportsCreated "Execution report created." "EXEC_SIM_R009_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $orderAudit.routesCreated "Route created." "EXEC_SIM_R009_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $orderAudit.submissionsCreated "Submission created." "EXEC_SIM_R009_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

Require-False $executionAudit.newQuoteFileValidationRunExecuted "New quote file validation run executed in R009." "EXEC_SIM_R009_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"
Require-False $executionAudit.newImportExecuted "New import executed in R009." "EXEC_SIM_R009_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"
Require-False $executionAudit.newBacktestExecuted "New backtest executed in R009." "EXEC_SIM_R009_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"
Require-False $executionAudit.newDataBatchProcessed "New data batch processed in R009." "EXEC_SIM_R009_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"
Require-True $executionAudit.staticRunbookFocused "R009 is not marked static/runbook-focused." "EXEC_SIM_R009_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"

Require-True $usdjpy.caveatPreserved "USDJPY caveat not preserved." "EXEC_SIM_R009_FAIL_USDJPY_CAVEAT_WEAKENED"
if ($usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8") {
    Fail "USDJPY SecurityID/SecurityIDSource weakened." "EXEC_SIM_R009_FAIL_USDJPY_CAVEAT_WEAKENED"
}
if ($lmaxBaseline.audusdStatus -notmatch "inconclusive" -or ($lmaxBaseline.audusdStatus -match "failed" -and $lmaxBaseline.audusdStatus -notmatch "not failed")) {
    Fail "AUDUSD incorrectly marked failed." "EXEC_SIM_R009_FAIL_AUDUSD_MISCLASSIFIED"
}
Require-False $lmaxBaseline.lmaxCalledInR009 "LMAX baseline called in R009." "EXEC_SIM_R009_FAIL_API_CALL_DETECTED"

Require-False $noExternal.polygonApiCalled "No-external audit shows Polygon API call." "EXEC_SIM_R009_FAIL_API_CALL_DETECTED"
Require-False $noExternal.lmaxCalled "No-external audit shows LMAX call." "EXEC_SIM_R009_FAIL_API_CALL_DETECTED"
Require-False $noExternal.externalApiCalled "No-external audit shows external API call." "EXEC_SIM_R009_FAIL_API_CALL_DETECTED"
Require-False $noExternal.socketTlsFixMarketDataRuntimeDetected "No-external audit shows runtime market data." "EXEC_SIM_R009_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $noExternal.ordersFillsReportsRoutesSubmissionsCreated "No-external audit shows order-domain action." "EXEC_SIM_R009_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.validationImportBacktestExecuted "No-external audit shows R009 execution." "EXEC_SIM_R009_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"
Require-False $noExternal.newDataBatchProcessed "No-external audit shows new data batch processed." "EXEC_SIM_R009_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"
Require-False $forbidden.forbiddenActionsDetected "Forbidden action detected." "EXEC_SIM_R009_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-False $forbidden.validationImportBacktestExecuted "Forbidden audit shows R009 execution." "EXEC_SIM_R009_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"
Require-False $forbidden.newDataBatchProcessed "Forbidden audit shows new data batch processed." "EXEC_SIM_R009_FAIL_VALIDATION_IMPORT_BACKTEST_EXECUTED"

if ($evidence.dotnetBuildNoRestore -ne "PASS") {
    Fail "dotnet build --no-restore evidence is not PASS." "EXEC_SIM_R009_FAIL_BUILD_OR_TESTS"
}
if ($evidence.focusedTests -notmatch "^PASS") {
    Fail "Focused R009 tests evidence is not PASS." "EXEC_SIM_R009_FAIL_BUILD_OR_TESTS"
}
if ($evidence.unitTests -notmatch "^PASS") {
    Fail "Unit test evidence is not PASS." "EXEC_SIM_R009_FAIL_BUILD_OR_TESTS"
}

Write-Host "EXEC_SIM_R009_PASS_OPERATOR_RUNBOOK_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R009_PASS_FIRST_DATA_BATCH_HANDOFF_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R009_PASS_TCA_INTERPRETATION_GUIDE_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R009_PASS_NO_API_NO_BACKTEST_EXECUTION_GATE_READY_NO_EXTERNAL"
exit 0
