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
        Fail "Missing artifact: $Path" "EXEC_SIM_R007_FAIL_BUILD_OR_TESTS"
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
    "phase-exec-sim-r007-summary.md",
    "phase-exec-sim-r007-validation-fixture-contract.json",
    "phase-exec-sim-r007-local-fixture-files-manifest.json",
    "phase-exec-sim-r007-accepted-file-manifests.json",
    "phase-exec-sim-r007-quarantined-file-manifests.json",
    "phase-exec-sim-r007-sanitized-import-readiness-outputs.json",
    "phase-exec-sim-r007-valid-eurusd-file-validation.json",
    "phase-exec-sim-r007-valid-usdjpy-file-validation.json",
    "phase-exec-sim-r007-valid-audusd-file-validation.json",
    "phase-exec-sim-r007-direct-cross-quarantine-evidence.json",
    "phase-exec-sim-r007-malformed-file-quarantine-evidence.json",
    "phase-exec-sim-r007-missing-timestamp-rejection-evidence.json",
    "phase-exec-sim-r007-missing-bidask-rejection-evidence.json",
    "phase-exec-sim-r007-invalid-bidask-rejection-evidence.json",
    "phase-exec-sim-r007-duplicate-file-idempotency-evidence.json",
    "phase-exec-sim-r007-secret-leak-quarantine-evidence.json",
    "phase-exec-sim-r007-raw-payload-quarantine-evidence.json",
    "phase-exec-sim-r007-quote-window-readiness-results.json",
    "phase-exec-sim-r007-close-benchmark-readiness-results.json",
    "phase-exec-sim-r007-feed-quality-readiness-results.json",
    "phase-exec-sim-r007-operator-validation-summary.md",
    "phase-exec-sim-r007-operator-validation-summary.json",
    "phase-exec-sim-r007-no-polygon-api-call-audit.json",
    "phase-exec-sim-r007-no-lmax-call-audit.json",
    "phase-exec-sim-r007-no-external-api-call-audit.json",
    "phase-exec-sim-r007-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r007-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r007-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r007-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r007-no-external-audit.json",
    "phase-exec-sim-r007-forbidden-actions-audit.json",
    "phase-exec-sim-r007-next-phase-recommendation.json",
    "phase-exec-sim-r007-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    $path = Join-Path $ArtifactsDir $artifact
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Required R007 artifact is missing: $artifact" "EXEC_SIM_R007_FAIL_BUILD_OR_TESTS"
    }
}

$contract = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r007-validation-fixture-contract.json")
$fixtures = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r007-local-fixture-files-manifest.json")
$accepted = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r007-accepted-file-manifests.json")
$quarantined = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r007-quarantined-file-manifests.json")
$sanitized = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r007-sanitized-import-readiness-outputs.json")
$eurusd = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r007-valid-eurusd-file-validation.json")
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r007-valid-usdjpy-file-validation.json")
$audusd = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r007-valid-audusd-file-validation.json")
$directCross = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r007-direct-cross-quarantine-evidence.json")
$malformed = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r007-malformed-file-quarantine-evidence.json")
$missingTimestamp = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r007-missing-timestamp-rejection-evidence.json")
$missingBidAsk = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r007-missing-bidask-rejection-evidence.json")
$invalidBidAsk = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r007-invalid-bidask-rejection-evidence.json")
$duplicate = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r007-duplicate-file-idempotency-evidence.json")
$secret = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r007-secret-leak-quarantine-evidence.json")
$raw = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r007-raw-payload-quarantine-evidence.json")
$window = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r007-quote-window-readiness-results.json")
$benchmark = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r007-close-benchmark-readiness-results.json")
$feed = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r007-feed-quality-readiness-results.json")
$summary = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r007-operator-validation-summary.json")
$polygonAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r007-no-polygon-api-call-audit.json")
$lmaxAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r007-no-lmax-call-audit.json")
$externalAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r007-no-external-api-call-audit.json")
$runtimeAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r007-no-broker-marketdata-runtime-audit.json")
$orderAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r007-no-order-fill-report-route-audit.json")
$usdjpyCaveat = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r007-usdjpy-caveat-preservation.json")
$lmaxBaseline = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r007-lmax-readonly-baseline-reference.json")
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r007-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r007-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r007-build-test-validator-evidence.json")

Require-True $contract.reusesR006IntakeContract "Validation fixture contract does not reuse R006 intake contract." "EXEC_SIM_R007_FAIL_VALIDATION_CONTRACT_MISSING"
Require-True $contract.reusesR006ManifestContract "Validation fixture contract does not reuse R006 manifest contract." "EXEC_SIM_R007_FAIL_VALIDATION_CONTRACT_MISSING"
Require-True $contract.localFixtureFilesOnly "Validation fixture contract is not local-fixture-only." "EXEC_SIM_R007_FAIL_VALIDATION_CONTRACT_MISSING"
Require-False $contract.runsImportBacktest "R007 ran import/backtest scope." "EXEC_SIM_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-False $contract.polygonApiCalled "Polygon API call detected in contract." "EXEC_SIM_R007_FAIL_API_CALL_DETECTED"
Require-False $contract.lmaxCalled "LMAX call detected in contract." "EXEC_SIM_R007_FAIL_API_CALL_DETECTED"
Require-False $contract.externalApiCalled "External API call detected in contract." "EXEC_SIM_R007_FAIL_API_CALL_DETECTED"
Require-Contains $contract.concreteFixtureFormats "NDJSON" "No concrete fixture format implemented." "EXEC_SIM_R007_FAIL_VALIDATION_CONTRACT_MISSING"
Require-Contains $contract.contractOnlyFixtureFormats "JSON" "JSON contract-only format missing." "EXEC_SIM_R007_FAIL_VALIDATION_CONTRACT_MISSING"
Require-Contains $contract.contractOnlyFixtureFormats "CSV" "CSV contract-only format missing." "EXEC_SIM_R007_FAIL_VALIDATION_CONTRACT_MISSING"

if (@($fixtures.files).Count -lt 12) {
    Fail "Local fixture files manifest does not include all required cases." "EXEC_SIM_R007_FAIL_VALIDATION_CONTRACT_MISSING"
}
foreach ($fixture in @($fixtures.files)) {
    if ($fixture.safeFixturePathCategory -notmatch "^tests/fixtures/execution-sim/r007/operator-provided/") {
        Fail "Fixture path is outside R007 local fixture area: $($fixture.safeFixturePathCategory)" "EXEC_SIM_R007_FAIL_VALIDATION_CONTRACT_MISSING"
    }
    if (-not (Test-Path -LiteralPath $fixture.safeFixturePathCategory)) {
        Fail "Local fixture file missing: $($fixture.safeFixturePathCategory)" "EXEC_SIM_R007_FAIL_VALIDATION_CONTRACT_MISSING"
    }
}

if (@($accepted.acceptedFileManifests).Count -lt 3) {
    Fail "Accepted file manifests missing valid USD-pair files." "EXEC_SIM_R007_FAIL_ACCEPTED_MANIFESTS_MISSING"
}
foreach ($symbol in @("EURUSD", "USDJPY", "AUDUSD")) {
    if (-not (@($accepted.acceptedFileManifests) | Where-Object { $_.executionTradableSymbol -eq $symbol -and $_.intakeStatus -eq "AcceptedForSanitizedImport" })) {
        Fail "Accepted manifest missing $symbol." "EXEC_SIM_R007_FAIL_ACCEPTED_MANIFESTS_MISSING"
    }
}
Require-False $accepted.rawPayloadSerialized "Accepted manifest serialized raw payload." "EXEC_SIM_R007_FAIL_SECRET_OR_RAW_PAYLOAD_LEAK_RISK"
Require-False $accepted.secretMaterialSerialized "Accepted manifest serialized secret material." "EXEC_SIM_R007_FAIL_SECRET_OR_RAW_PAYLOAD_LEAK_RISK"

if (@($quarantined.quarantinedFileManifests).Count -lt 8) {
    Fail "Quarantined file manifests missing required cases." "EXEC_SIM_R007_FAIL_QUARANTINED_MANIFESTS_MISSING"
}
foreach ($status in @("QuarantinedDirectCrossExecutionDisabled", "QuarantinedUnsupportedSymbol", "QuarantinedMalformedFile", "QuarantinedMissingTimestamp", "QuarantinedMissingBidAsk", "QuarantinedInvalidBidAsk", "QuarantinedSecretLeakRisk", "QuarantinedRawPayloadLeakRisk")) {
    if (-not (@($quarantined.quarantinedFileManifests) | Where-Object { $_.intakeStatus -eq $status })) {
        Fail "Quarantined manifests missing $status." "EXEC_SIM_R007_FAIL_QUARANTINED_MANIFESTS_MISSING"
    }
}
Require-False $quarantined.rawPayloadSerialized "Quarantined manifest serialized raw payload." "EXEC_SIM_R007_FAIL_SECRET_OR_RAW_PAYLOAD_LEAK_RISK"
Require-False $quarantined.secretMaterialSerialized "Quarantined manifest serialized secret material." "EXEC_SIM_R007_FAIL_SECRET_OR_RAW_PAYLOAD_LEAK_RISK"

if (@($sanitized.sanitizedImportReadinessOutputs).Count -lt 3) {
    Fail "Sanitized import-readiness outputs missing accepted files." "EXEC_SIM_R007_FAIL_SANITIZED_IMPORT_READINESS_MISSING"
}
Require-True $sanitized.quarantinedFilesProduceNoSanitizedImportReadyOutput "Quarantined files produced sanitized import-ready output." "EXEC_SIM_R007_FAIL_SANITIZED_IMPORT_READINESS_MISSING"
Require-False $sanitized.runsImportBacktest "R007 sanitized output ran import backtest." "EXEC_SIM_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
foreach ($output in @($sanitized.sanitizedImportReadinessOutputs)) {
    Require-True $output.sanitizedImportReady "Sanitized output is not import-ready." "EXEC_SIM_R007_FAIL_SANITIZED_IMPORT_READINESS_MISSING"
    Require-False $output.rawPayloadSerialized "Sanitized output serialized raw payload." "EXEC_SIM_R007_FAIL_SECRET_OR_RAW_PAYLOAD_LEAK_RISK"
    Require-False $output.secretMaterialSerialized "Sanitized output serialized secret material." "EXEC_SIM_R007_FAIL_SECRET_OR_RAW_PAYLOAD_LEAK_RISK"
}

foreach ($valid in @($eurusd, $usdjpy, $audusd)) {
    if ($valid.intakeStatus -ne "AcceptedForSanitizedImport" -or $valid.sanitizedImportReady -ne $true) {
        Fail "Valid file evidence is not accepted/import-ready." "EXEC_SIM_R007_FAIL_ACCEPTED_MANIFESTS_MISSING"
    }
    if ($valid.closeBenchmarkReadinessStatus -ne "Available" -or $valid.feedQualityReadinessStatus -ne "Good") {
        Fail "Valid file readiness evidence is incomplete." "EXEC_SIM_R007_FAIL_SANITIZED_IMPORT_READINESS_MISSING"
    }
    Require-False $valid.rawPayloadSerialized "Valid evidence serialized raw payload." "EXEC_SIM_R007_FAIL_SECRET_OR_RAW_PAYLOAD_LEAK_RISK"
    Require-False $valid.secretMaterialSerialized "Valid evidence serialized secret material." "EXEC_SIM_R007_FAIL_SECRET_OR_RAW_PAYLOAD_LEAK_RISK"
}

if ($directCross.intakeStatus -ne "QuarantinedDirectCrossExecutionDisabled" -or $directCross.directCrossExecutionAllowed -ne $false) {
    Fail "Direct-cross quarantine evidence missing or weakened." "EXEC_SIM_R007_FAIL_DIRECT_CROSS_QUARANTINE_MISSING"
}
foreach ($e in @($malformed, $missingTimestamp, $missingBidAsk, $invalidBidAsk)) {
    if ($e.sanitizedImportReady -ne $false) {
        Fail "Rejected/quarantined file is sanitized import-ready." "EXEC_SIM_R007_FAIL_QUARANTINED_MANIFESTS_MISSING"
    }
}
if ($duplicate.intakeStatus -ne "DuplicateReturned" -or $duplicate.duplicateHandledDeterministically -ne $true) {
    Fail "Duplicate file idempotency evidence missing." "EXEC_SIM_R007_FAIL_QUARANTINED_MANIFESTS_MISSING"
}
if ($secret.intakeStatus -ne "QuarantinedSecretLeakRisk" -or $secret.secretMaterialSerialized -ne $false) {
    Fail "Secret leak quarantine evidence missing or serialized secret material." "EXEC_SIM_R007_FAIL_SECRET_OR_RAW_PAYLOAD_LEAK_RISK"
}
if ($raw.intakeStatus -ne "QuarantinedRawPayloadLeakRisk" -or $raw.rawPayloadSerialized -ne $false) {
    Fail "Raw payload quarantine evidence missing or serialized raw payload." "EXEC_SIM_R007_FAIL_SECRET_OR_RAW_PAYLOAD_LEAK_RISK"
}

if (@($window.quoteWindowReadinessResults).Count -lt 3) {
    Fail "Quote-window readiness results missing." "EXEC_SIM_R007_FAIL_SANITIZED_IMPORT_READINESS_MISSING"
}
if (@($benchmark.closeBenchmarkReadinessResults).Count -lt 3) {
    Fail "Close benchmark readiness results missing." "EXEC_SIM_R007_FAIL_SANITIZED_IMPORT_READINESS_MISSING"
}
if (@($feed.feedQualityReadinessResults).Count -lt 3) {
    Fail "Feed quality readiness results missing." "EXEC_SIM_R007_FAIL_SANITIZED_IMPORT_READINESS_MISSING"
}
Require-True $window.quarantinedFilesProduceNoAcceptedQuoteWindowOutput "Quarantined files produced accepted quote-window output." "EXEC_SIM_R007_FAIL_SANITIZED_IMPORT_READINESS_MISSING"
Require-True $benchmark.quarantinedFilesProduceNoAcceptedCloseBenchmarkOutput "Quarantined files produced accepted close benchmark output." "EXEC_SIM_R007_FAIL_SANITIZED_IMPORT_READINESS_MISSING"
Require-True $feed.quarantinedFilesProduceNoAcceptedFeedQualityOutput "Quarantined files produced accepted feed quality output." "EXEC_SIM_R007_FAIL_SANITIZED_IMPORT_READINESS_MISSING"

if ($summary.acceptedFileCount -lt 3 -or $summary.quarantinedFileCount -lt 8 -or $summary.duplicateFileCount -lt 1) {
    Fail "Operator validation summary missing accepted/quarantined/duplicate counts." "EXEC_SIM_R007_FAIL_VALIDATION_CONTRACT_MISSING"
}
Require-False $summary.externalApiCalled "Operator summary shows external API call." "EXEC_SIM_R007_FAIL_API_CALL_DETECTED"
Require-False $summary.ordersFillsReportsRoutesSubmissionsCreated "Operator summary shows order-domain records." "EXEC_SIM_R007_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

Require-False $polygonAudit.polygonApiCalled "Polygon API call detected." "EXEC_SIM_R007_FAIL_API_CALL_DETECTED"
Require-False $polygonAudit.externalApiCalled "External API call detected in Polygon audit." "EXEC_SIM_R007_FAIL_API_CALL_DETECTED"
Require-False $polygonAudit.httpClientUsed "HTTP client usage detected." "EXEC_SIM_R007_FAIL_API_CALL_DETECTED"
Require-False $lmaxAudit.lmaxCalled "LMAX call detected." "EXEC_SIM_R007_FAIL_API_CALL_DETECTED"
Require-False $externalAudit.externalApiCalled "External API call detected." "EXEC_SIM_R007_FAIL_API_CALL_DETECTED"
Require-False $externalAudit.socketOpened "Socket opened in external audit." "EXEC_SIM_R007_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.brokerActivationDetected "Broker activation detected." "EXEC_SIM_R007_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.socketOpened "Socket opened." "EXEC_SIM_R007_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.tlsOpened "TLS opened." "EXEC_SIM_R007_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.fixOpened "FIX opened." "EXEC_SIM_R007_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.marketDataRequestSent "MarketDataRequest sent." "EXEC_SIM_R007_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.marketDataResponseRead "MarketDataResponse read." "EXEC_SIM_R007_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.apiWorkerLiveGatewayEnabled "API/Worker live gateway enabled." "EXEC_SIM_R007_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.schedulerServiceTimerPollingBackgroundJobIntroduced "Scheduler/service/timer/polling/background job introduced." "EXEC_SIM_R007_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $runtimeAudit.automaticExecutionIntroduced "Automatic execution introduced." "EXEC_SIM_R007_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $orderAudit.ordersCreated "Order created." "EXEC_SIM_R007_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $orderAudit.executableOrdersCreated "Executable order created." "EXEC_SIM_R007_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $orderAudit.fillsCreated "Fill created." "EXEC_SIM_R007_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $orderAudit.executionReportsCreated "Execution report created." "EXEC_SIM_R007_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $orderAudit.routesCreated "Route created." "EXEC_SIM_R007_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $orderAudit.submissionsCreated "Submission created." "EXEC_SIM_R007_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

Require-True $usdjpyCaveat.caveatPreserved "USDJPY caveat not preserved." "EXEC_SIM_R007_FAIL_USDJPY_CAVEAT_WEAKENED"
if ($usdjpyCaveat.securityId -ne "4004" -or $usdjpyCaveat.securityIdSource -ne "8") {
    Fail "USDJPY SecurityID/SecurityIDSource weakened." "EXEC_SIM_R007_FAIL_USDJPY_CAVEAT_WEAKENED"
}
if ($lmaxBaseline.audusdStatus -notmatch "inconclusive" -or ($lmaxBaseline.audusdStatus -match "failed" -and $lmaxBaseline.audusdStatus -notmatch "not failed")) {
    Fail "AUDUSD incorrectly marked failed." "EXEC_SIM_R007_FAIL_AUDUSD_MISCLASSIFIED"
}
Require-False $lmaxBaseline.lmaxCalledInR007 "LMAX baseline was called in R007." "EXEC_SIM_R007_FAIL_API_CALL_DETECTED"

Require-False $noExternal.polygonApiCalled "No-external audit shows Polygon API call." "EXEC_SIM_R007_FAIL_API_CALL_DETECTED"
Require-False $noExternal.lmaxCalled "No-external audit shows LMAX call." "EXEC_SIM_R007_FAIL_API_CALL_DETECTED"
Require-False $noExternal.externalApiCalled "No-external audit shows external API call." "EXEC_SIM_R007_FAIL_API_CALL_DETECTED"
Require-False $noExternal.socketTlsFixMarketDataRuntimeDetected "No-external audit shows runtime market-data action." "EXEC_SIM_R007_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $noExternal.ordersFillsReportsRoutesSubmissionsCreated "No-external audit shows order/fill/report/route/submission." "EXEC_SIM_R007_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $noExternal.rawPayloadSerialized "No-external audit serialized raw payload." "EXEC_SIM_R007_FAIL_SECRET_OR_RAW_PAYLOAD_LEAK_RISK"
Require-False $noExternal.secretMaterialSerialized "No-external audit serialized secret material." "EXEC_SIM_R007_FAIL_SECRET_OR_RAW_PAYLOAD_LEAK_RISK"
Require-False $forbidden.forbiddenActionsDetected "Forbidden action detected." "EXEC_SIM_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-False $forbidden.secretOrRawPayloadSerialized "Forbidden audit serialized secret/raw payload." "EXEC_SIM_R007_FAIL_SECRET_OR_RAW_PAYLOAD_LEAK_RISK"

if (-not (Test-Path -LiteralPath $SourcePath)) {
    Fail "Source path missing: $SourcePath" "EXEC_SIM_R007_FAIL_BUILD_OR_TESTS"
}
$source = Get-Content -LiteralPath $SourcePath -Raw
foreach ($token in @("HttpClient", "GetAsync", "PostAsync", "SendAsync", "WebSocket", "TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "FixSession", "ConnectAsync", "BackgroundService", "IHostedService", "PeriodicTimer")) {
    if ($source -match [regex]::Escape($token)) {
        Fail "Runtime/external action token detected in source: $token" "EXEC_SIM_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    }
}

if ($evidence.dotnetBuildNoRestore -ne "PASS") {
    Fail "dotnet build --no-restore evidence is not PASS." "EXEC_SIM_R007_FAIL_BUILD_OR_TESTS"
}
if ($evidence.focusedTests -notmatch "^PASS") {
    Fail "Focused R007 test evidence is not PASS." "EXEC_SIM_R007_FAIL_BUILD_OR_TESTS"
}
if ($evidence.unitTests -notmatch "^PASS") {
    Fail "Unit test evidence is not PASS." "EXEC_SIM_R007_FAIL_BUILD_OR_TESTS"
}

Write-Host "EXEC_SIM_R007_PASS_OPERATOR_QUOTE_FILE_VALIDATION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R007_PASS_ACCEPTED_QUARANTINED_MANIFESTS_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R007_PASS_SANITIZED_IMPORT_READINESS_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R007_PASS_NO_API_CALL_VALIDATION_GATE_READY_NO_EXTERNAL"
exit 0
