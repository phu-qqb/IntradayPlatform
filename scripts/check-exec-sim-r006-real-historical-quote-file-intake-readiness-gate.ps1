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
        Fail "Missing artifact: $Path" "EXEC_SIM_R006_FAIL_BUILD_OR_TESTS"
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
    "phase-exec-sim-r006-summary.md",
    "phase-exec-sim-r006-offline-file-intake-contract.json",
    "phase-exec-sim-r006-quote-file-manifest-contract.json",
    "phase-exec-sim-r006-intake-logical-locations.json",
    "phase-exec-sim-r006-supported-file-formats.json",
    "phase-exec-sim-r006-file-level-validation-rules.json",
    "phase-exec-sim-r006-row-level-validation-rules.json",
    "phase-exec-sim-r006-intake-statuses.json",
    "phase-exec-sim-r006-quarantine-handling.json",
    "phase-exec-sim-r006-idempotency-and-duplicate-file-handling.json",
    "phase-exec-sim-r006-symbol-mapping-readiness.json",
    "phase-exec-sim-r006-direct-cross-quarantine-handling.json",
    "phase-exec-sim-r006-quote-window-readiness-checks.json",
    "phase-exec-sim-r006-close-benchmark-readiness-checks.json",
    "phase-exec-sim-r006-feed-quality-readiness-checks.json",
    "phase-exec-sim-r006-operator-file-workflow.md",
    "phase-exec-sim-r006-operator-file-workflow.json",
    "phase-exec-sim-r006-no-api-key-secret-handling.json",
    "phase-exec-sim-r006-raw-payload-sanitization-requirements.json",
    "phase-exec-sim-r006-no-polygon-api-call-audit.json",
    "phase-exec-sim-r006-no-lmax-call-audit.json",
    "phase-exec-sim-r006-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r006-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r006-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r006-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r006-no-external-audit.json",
    "phase-exec-sim-r006-forbidden-actions-audit.json",
    "phase-exec-sim-r006-next-phase-recommendation.json",
    "phase-exec-sim-r006-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    $path = Join-Path $ArtifactsDir $artifact
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Required R006 artifact is missing: $artifact" "EXEC_SIM_R006_FAIL_BUILD_OR_TESTS"
    }
}

$intake = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r006-offline-file-intake-contract.json")
$manifest = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r006-quote-file-manifest-contract.json")
$locations = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r006-intake-logical-locations.json")
$formats = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r006-supported-file-formats.json")
$fileRules = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r006-file-level-validation-rules.json")
$rowRules = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r006-row-level-validation-rules.json")
$statuses = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r006-intake-statuses.json")
$quarantine = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r006-quarantine-handling.json")
$idempotency = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r006-idempotency-and-duplicate-file-handling.json")
$symbols = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r006-symbol-mapping-readiness.json")
$directCross = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r006-direct-cross-quarantine-handling.json")
$windowChecks = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r006-quote-window-readiness-checks.json")
$benchmarkChecks = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r006-close-benchmark-readiness-checks.json")
$feedChecks = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r006-feed-quality-readiness-checks.json")
$workflow = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r006-operator-file-workflow.json")
$secret = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r006-no-api-key-secret-handling.json")
$raw = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r006-raw-payload-sanitization-requirements.json")
$polygonAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r006-no-polygon-api-call-audit.json")
$lmaxAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r006-no-lmax-call-audit.json")
$runtimeAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r006-no-broker-marketdata-runtime-audit.json")
$orderAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r006-no-order-fill-report-route-audit.json")
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r006-usdjpy-caveat-preservation.json")
$lmaxBaseline = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r006-lmax-readonly-baseline-reference.json")
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r006-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r006-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r006-build-test-validator-evidence.json")

foreach ($provider in @("PolygonOfflineFile", "FixtureOnly", "LMAXArchiveFuture")) {
    Require-Contains $intake.providerIdentities $provider "Offline intake contract is missing provider identity $provider." "EXEC_SIM_R006_FAIL_FILE_INTAKE_CONTRACT_MISSING"
}
Require-True $intake.operatorProvidedFilesOnly "Intake contract is not operator-provided-files-only." "EXEC_SIM_R006_FAIL_FILE_INTAKE_CONTRACT_MISSING"
Require-False $intake.polygonApiCalled "Polygon API call detected in intake contract." "EXEC_SIM_R006_FAIL_API_CALL_DETECTED"
Require-False $intake.lmaxCalled "LMAX call detected in intake contract." "EXEC_SIM_R006_FAIL_API_CALL_DETECTED"
Require-False $intake.externalApiCalled "External API call detected in intake contract." "EXEC_SIM_R006_FAIL_API_CALL_DETECTED"
Require-False $intake.rawPayloadDumpAllowed "Raw payload dump allowed in intake contract." "EXEC_SIM_R006_FAIL_RAW_PAYLOAD_OR_SECRET_LEAK_RISK"

foreach ($field in @("QuoteFileManifestId", "ProviderName", "ProviderSymbol", "FilePath", "FileFormat", "FileHash", "ContainsRawProviderPayload", "ContainsSecrets", "IntakeStatus")) {
    Require-Contains $manifest.requiredFields $field "Manifest contract missing $field." "EXEC_SIM_R006_FAIL_FILE_MANIFEST_CONTRACT_MISSING"
}
Require-False $manifest.secretsAllowed "Manifest allows secrets." "EXEC_SIM_R006_FAIL_RAW_PAYLOAD_OR_SECRET_LEAK_RISK"
Require-False $manifest.rawProviderPayloadAllowed "Manifest allows raw provider payloads." "EXEC_SIM_R006_FAIL_RAW_PAYLOAD_OR_SECRET_LEAK_RISK"

foreach ($location in @("data/offline-quotes/polygon/incoming/", "data/offline-quotes/polygon/quarantine/", "data/offline-quotes/polygon/accepted/", "data/offline-quotes/polygon/sanitized/", "data/offline-quotes/polygon/processed/")) {
    Require-Contains $locations.logicalLocations $location "Intake logical locations missing $location." "EXEC_SIM_R006_FAIL_FILE_INTAKE_CONTRACT_MISSING"
}
foreach ($format in @("JSON", "NDJSON", "CSV")) {
    Require-Contains $formats.supportedFileFormats $format "Supported formats missing $format." "EXEC_SIM_R006_FAIL_FILE_INTAKE_CONTRACT_MISSING"
}

foreach ($rule in @("file exists", "manifest exists", "provider is supported", "symbol is mapped", "file hash computed", "duplicate hash handled deterministically", "no secrets detected", "no raw payload dump emitted into artifacts")) {
    Require-Contains $fileRules.fileLevelValidationRules $rule "File-level validation rules missing $rule." "EXEC_SIM_R006_FAIL_VALIDATION_RULES_MISSING"
}
foreach ($rule in @("timestamp parseable", "bid finite positive", "ask finite positive", "ask greater than or equal to bid", "invalid rows counted and rejected")) {
    Require-Contains $rowRules.rowLevelValidationRules $rule "Row-level validation rules missing $rule." "EXEC_SIM_R006_FAIL_VALIDATION_RULES_MISSING"
}
foreach ($status in @("AcceptedForSanitizedImport", "QuarantinedMalformedFile", "QuarantinedUnsupportedSymbol", "QuarantinedDirectCrossExecutionDisabled", "QuarantinedMissingTimestamp", "QuarantinedMissingBidAsk", "QuarantinedInvalidBidAsk", "QuarantinedSecretLeakRisk", "QuarantinedRawPayloadLeakRisk", "DuplicateReturned", "InconclusiveSafe")) {
    Require-Contains $statuses.intakeStatuses $status "Intake statuses missing $status." "EXEC_SIM_R006_FAIL_QUARANTINE_HANDLING_MISSING"
}

if ($quarantine.quarantineHandling.directCross -ne "QuarantinedDirectCrossExecutionDisabled") {
    Fail "Quarantine handling for direct cross is missing." "EXEC_SIM_R006_FAIL_QUARANTINE_HANDLING_MISSING"
}
Require-True $idempotency.fileHashComputed "Idempotency file hash is not computed." "EXEC_SIM_R006_FAIL_QUARANTINE_HANDLING_MISSING"
Require-True $idempotency.duplicateHandledDeterministically "Duplicate handling is not deterministic." "EXEC_SIM_R006_FAIL_QUARANTINE_HANDLING_MISSING"
if ($idempotency.duplicateHashStatus -ne "DuplicateReturned") {
    Fail "Duplicate hash status is not DuplicateReturned." "EXEC_SIM_R006_FAIL_QUARANTINE_HANDLING_MISSING"
}
if ($symbols.executionUniverse -ne "USD-pair-only") {
    Fail "Symbol mapping readiness does not preserve USD-pair-only universe." "EXEC_SIM_R006_FAIL_FILE_INTAKE_CONTRACT_MISSING"
}
Require-False $directCross.directCrossExecutionAllowed "Direct-cross execution allowed." "EXEC_SIM_R006_FAIL_QUARANTINE_HANDLING_MISSING"
if ($directCross.quarantineStatus -ne "QuarantinedDirectCrossExecutionDisabled") {
    Fail "Direct-cross quarantine status missing." "EXEC_SIM_R006_FAIL_QUARANTINE_HANDLING_MISSING"
}

Require-Contains $windowChecks.quoteWindowReadinessChecks "quote count last minute sufficient" "Quote-window readiness checks missing last-minute count." "EXEC_SIM_R006_FAIL_VALIDATION_RULES_MISSING"
Require-Contains $benchmarkChecks.closeBenchmarkStatuses "Available" "Close benchmark readiness statuses missing Available." "EXEC_SIM_R006_FAIL_VALIDATION_RULES_MISSING"
Require-Contains $feedChecks.feedQualityReadinessChecks "FeedQualityBucket" "Feed quality readiness checks missing FeedQualityBucket." "EXEC_SIM_R006_FAIL_VALIDATION_RULES_MISSING"
Require-Contains $workflow.workflowSteps "Obtain quote files outside this system" "Operator workflow missing external preparation step." "EXEC_SIM_R006_FAIL_OPERATOR_WORKFLOW_MISSING"
Require-Contains $workflow.workflowSteps "Place files in data/offline-quotes/polygon/incoming/" "Operator workflow missing incoming location step." "EXEC_SIM_R006_FAIL_OPERATOR_WORKFLOW_MISSING"

Require-False $secret.apiKeysAllowedInFiles "API keys allowed in files." "EXEC_SIM_R006_FAIL_RAW_PAYLOAD_OR_SECRET_LEAK_RISK"
Require-False $secret.secretsAllowedInManifests "Secrets allowed in manifests." "EXEC_SIM_R006_FAIL_RAW_PAYLOAD_OR_SECRET_LEAK_RISK"
Require-False $secret.credentialsSerialized "Credentials serialized." "EXEC_SIM_R006_FAIL_RAW_PAYLOAD_OR_SECRET_LEAK_RISK"
Require-False $raw.rawProviderPayloadDumpsAllowed "Raw provider payload dumps allowed." "EXEC_SIM_R006_FAIL_RAW_PAYLOAD_OR_SECRET_LEAK_RISK"
Require-False $raw.rawFixSerialized "Raw FIX serialized." "EXEC_SIM_R006_FAIL_RAW_PAYLOAD_OR_SECRET_LEAK_RISK"

Require-False $polygonAudit.polygonApiCalled "Polygon API call detected." "EXEC_SIM_R006_FAIL_API_CALL_DETECTED"
Require-False $polygonAudit.externalApiCalled "External API call detected." "EXEC_SIM_R006_FAIL_API_CALL_DETECTED"
Require-False $polygonAudit.httpClientUsed "HTTP client usage detected." "EXEC_SIM_R006_FAIL_API_CALL_DETECTED"
Require-False $lmaxAudit.lmaxCalled "LMAX call detected." "EXEC_SIM_R006_FAIL_API_CALL_DETECTED"
Require-False $runtimeAudit.brokerActivationDetected "Broker activation detected." "EXEC_SIM_R006_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.socketOpened "Socket opened." "EXEC_SIM_R006_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.tlsOpened "TLS opened." "EXEC_SIM_R006_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.fixOpened "FIX opened." "EXEC_SIM_R006_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.marketDataRequestSent "MarketDataRequest sent." "EXEC_SIM_R006_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.marketDataResponseRead "MarketDataResponse read." "EXEC_SIM_R006_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.apiWorkerLiveGatewayEnabled "API/Worker live gateway enabled." "EXEC_SIM_R006_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.schedulerServiceTimerPollingBackgroundJobIntroduced "Scheduler/service/timer/polling/background job introduced." "EXEC_SIM_R006_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $runtimeAudit.automaticExecutionIntroduced "Automatic execution introduced." "EXEC_SIM_R006_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $orderAudit.ordersCreated "Order created." "EXEC_SIM_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $orderAudit.fillsCreated "Fill created." "EXEC_SIM_R006_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $orderAudit.executionReportsCreated "Execution report created." "EXEC_SIM_R006_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $orderAudit.routesCreated "Route created." "EXEC_SIM_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $orderAudit.submissionsCreated "Submission created." "EXEC_SIM_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

Require-True $usdjpy.caveatPreserved "USDJPY caveat is not preserved." "EXEC_SIM_R006_FAIL_USDJPY_CAVEAT_WEAKENED"
if ($usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8") {
    Fail "USDJPY caveat SecurityID/SecurityIDSource weakened." "EXEC_SIM_R006_FAIL_USDJPY_CAVEAT_WEAKENED"
}
if ($lmaxBaseline.audusdStatus -notmatch "inconclusive" -or ($lmaxBaseline.audusdStatus -match "failed" -and $lmaxBaseline.audusdStatus -notmatch "not failed")) {
    Fail "AUDUSD is incorrectly marked failed." "EXEC_SIM_R006_FAIL_AUDUSD_MISCLASSIFIED"
}
Require-False $lmaxBaseline.lmaxCalledInR006 "LMAX baseline was called in R006." "EXEC_SIM_R006_FAIL_API_CALL_DETECTED"

Require-False $noExternal.polygonApiCalled "No-external audit shows Polygon API call." "EXEC_SIM_R006_FAIL_API_CALL_DETECTED"
Require-False $noExternal.lmaxCalled "No-external audit shows LMAX call." "EXEC_SIM_R006_FAIL_API_CALL_DETECTED"
Require-False $noExternal.externalApiCalled "No-external audit shows external API call." "EXEC_SIM_R006_FAIL_API_CALL_DETECTED"
Require-False $noExternal.socketTlsFixMarketDataRuntimeDetected "No-external audit shows runtime market-data action." "EXEC_SIM_R006_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $noExternal.ordersFillsReportsRoutesSubmissionsCreated "No-external audit shows order/fill/report/route/submission." "EXEC_SIM_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $forbidden.forbiddenActionsDetected "Forbidden action detected." "EXEC_SIM_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED"

if (-not (Test-Path -LiteralPath $SourcePath)) {
    Fail "Source path missing: $SourcePath" "EXEC_SIM_R006_FAIL_BUILD_OR_TESTS"
}
$source = Get-Content -LiteralPath $SourcePath -Raw
foreach ($token in @("HttpClient", "GetAsync", "PostAsync", "SendAsync", "WebSocket", "TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "FixSession", "ConnectAsync", "BackgroundService", "IHostedService", "PeriodicTimer")) {
    if ($source -match [regex]::Escape($token)) {
        Fail "Runtime/external action token detected in R006 source: $token" "EXEC_SIM_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    }
}

if ($evidence.dotnetBuildNoRestore -ne "PASS") {
    Fail "dotnet build --no-restore evidence is not PASS." "EXEC_SIM_R006_FAIL_BUILD_OR_TESTS"
}
if ($evidence.focusedTests -notmatch "^PASS") {
    Fail "Focused R006 test evidence is not PASS." "EXEC_SIM_R006_FAIL_BUILD_OR_TESTS"
}
if ($evidence.unitTests -notmatch "^PASS") {
    Fail "Unit test evidence is not PASS." "EXEC_SIM_R006_FAIL_BUILD_OR_TESTS"
}

Write-Host "EXEC_SIM_R006_PASS_REAL_HISTORICAL_QUOTE_FILE_INTAKE_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R006_PASS_OPERATOR_OFFLINE_FILE_WORKFLOW_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R006_PASS_QUOTE_FILE_VALIDATION_AND_QUARANTINE_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R006_PASS_NO_API_CALL_INTAKE_GATE_READY_NO_EXTERNAL"
exit 0
