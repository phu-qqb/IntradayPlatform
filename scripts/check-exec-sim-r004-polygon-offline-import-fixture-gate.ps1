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
        Fail "Missing artifact: $Path" "EXEC_SIM_R004_FAIL_BUILD_OR_TESTS"
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
    "phase-exec-sim-r004-summary.md",
    "phase-exec-sim-r004-offline-polygon-import-contract.json",
    "phase-exec-sim-r004-supported-file-formats.json",
    "phase-exec-sim-r004-sanitized-quote-fixture-row-schema.json",
    "phase-exec-sim-r004-provider-field-mapping.json",
    "phase-exec-sim-r004-symbol-mapping-contract.json",
    "phase-exec-sim-r004-direct-cross-import-handling.json",
    "phase-exec-sim-r004-import-validation-rules.json",
    "phase-exec-sim-r004-import-result-statuses.json",
    "phase-exec-sim-r004-safe-failure-categories.json",
    "phase-exec-sim-r004-valid-fixture-import-evidence.json",
    "phase-exec-sim-r004-invalid-fixture-rejection-evidence.json",
    "phase-exec-sim-r004-quote-window-extraction-evidence.json",
    "phase-exec-sim-r004-close-benchmark-construction-evidence.json",
    "phase-exec-sim-r004-feed-quality-scoring-evidence.json",
    "phase-exec-sim-r004-raw-payload-sanitization-evidence.json",
    "phase-exec-sim-r004-no-polygon-api-call-audit.json",
    "phase-exec-sim-r004-no-lmax-call-audit.json",
    "phase-exec-sim-r004-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r004-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r004-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r004-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r004-no-external-audit.json",
    "phase-exec-sim-r004-forbidden-actions-audit.json",
    "phase-exec-sim-r004-next-phase-recommendation.json",
    "phase-exec-sim-r004-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    $path = Join-Path $ArtifactsDir $artifact
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Required R004 artifact is missing: $artifact" "EXEC_SIM_R004_FAIL_BUILD_OR_TESTS"
    }
}

$contract = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r004-offline-polygon-import-contract.json")
$formats = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r004-supported-file-formats.json")
$schema = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r004-sanitized-quote-fixture-row-schema.json")
$mapping = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r004-provider-field-mapping.json")
$symbols = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r004-symbol-mapping-contract.json")
$directCross = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r004-direct-cross-import-handling.json")
$rules = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r004-import-validation-rules.json")
$statuses = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r004-import-result-statuses.json")
$failures = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r004-safe-failure-categories.json")
$valid = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r004-valid-fixture-import-evidence.json")
$invalid = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r004-invalid-fixture-rejection-evidence.json")
$window = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r004-quote-window-extraction-evidence.json")
$benchmark = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r004-close-benchmark-construction-evidence.json")
$feed = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r004-feed-quality-scoring-evidence.json")
$sanitize = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r004-raw-payload-sanitization-evidence.json")
$polygonAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r004-no-polygon-api-call-audit.json")
$lmaxAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r004-no-lmax-call-audit.json")
$runtimeAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r004-no-broker-marketdata-runtime-audit.json")
$orderAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r004-no-order-fill-report-route-audit.json")
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r004-usdjpy-caveat-preservation.json")
$lmaxBaseline = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r004-lmax-readonly-baseline-reference.json")
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r004-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r004-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r004-build-test-validator-evidence.json")

Require-True $contract.localFilesOnly "Offline Polygon import contract is not local-files-only." "EXEC_SIM_R004_FAIL_IMPORT_CONTRACT_MISSING"
Require-False $contract.polygonApiCalled "Polygon API call detected in import contract." "EXEC_SIM_R004_FAIL_API_CALL_DETECTED"
Require-False $contract.lmaxCalled "LMAX call detected in import contract." "EXEC_SIM_R004_FAIL_API_CALL_DETECTED"
Require-False $contract.externalApiCalled "External API call detected in import contract." "EXEC_SIM_R004_FAIL_API_CALL_DETECTED"
Require-False $contract.rawProviderPayloadDumpAllowed "Raw provider payload dump is allowed." "EXEC_SIM_R004_FAIL_RAW_PAYLOAD_OR_SECRET_LEAK_RISK"
foreach ($format in @("JSON", "NDJSON", "CSV")) {
    Require-Contains $contract.supportedFormats $format "Offline import contract is missing format $format." "EXEC_SIM_R004_FAIL_IMPORT_CONTRACT_MISSING"
    Require-Contains $formats.supportedLocalFixtureFileFormats $format "Supported file formats artifact is missing $format." "EXEC_SIM_R004_FAIL_IMPORT_CONTRACT_MISSING"
}

foreach ($field in @("QuoteFixtureRowId", "QuoteProvider", "ProviderSymbol", "ExecutionTradableSymbol", "NormalizedPortfolioSymbol", "RequiresInversion", "TimestampUtc", "Bid", "Ask", "Mid", "Spread", "SpreadBps", "RawPayloadSerialized")) {
    Require-Contains $schema.requiredFields $field "Sanitized quote schema is missing $field." "EXEC_SIM_R004_FAIL_SANITIZED_QUOTE_SCHEMA_MISSING"
}
Require-False $schema.rawPayloadSerialized "Sanitized quote schema serializes raw payload." "EXEC_SIM_R004_FAIL_RAW_PAYLOAD_OR_SECRET_LEAK_RISK"
Require-False $schema.secretSerialized "Sanitized quote schema serializes secrets." "EXEC_SIM_R004_FAIL_RAW_PAYLOAD_OR_SECRET_LEAK_RISK"

if ($mapping.mapping.'provider timestamp' -ne "TimestampUtc" -or $mapping.mapping.'provider bid' -ne "Bid" -or $mapping.mapping.'provider ask' -ne "Ask") {
    Fail "Provider field mapping is incomplete." "EXEC_SIM_R004_FAIL_IMPORT_CONTRACT_MISSING"
}
Require-True $mapping.mapsVenueToSanitizedAvailabilityOnly "Venue is not mapped to sanitized availability only." "EXEC_SIM_R004_FAIL_RAW_PAYLOAD_OR_SECRET_LEAK_RISK"
Require-False $mapping.rawProviderPayloadDumpAllowed "Provider field mapping permits raw payload dumps." "EXEC_SIM_R004_FAIL_RAW_PAYLOAD_OR_SECRET_LEAK_RISK"

$mappedSymbols = @($symbols.mappings | ForEach-Object { $_.executionTradableSymbol })
foreach ($symbol in @("EURUSD", "GBPUSD", "AUDUSD", "NZDUSD", "USDJPY", "USDCAD", "USDCHF", "USDMXN", "USDCNH", "USDNOK", "USDSEK", "USDZAR")) {
    Require-Contains $mappedSymbols $symbol "Symbol mapping is missing $symbol." "EXEC_SIM_R004_FAIL_SYMBOL_MAPPING_MISSING"
}
Require-False $symbols.directCrossExecutionAllowed "Symbol mapping allows direct cross execution." "EXEC_SIM_R004_FAIL_DIRECT_CROSS_HANDLING_MISSING"
Require-False $directCross.directCrossExecutionAllowed "Direct cross import handling allows execution." "EXEC_SIM_R004_FAIL_DIRECT_CROSS_HANDLING_MISSING"
if ($directCross.blockedStatus -ne "ImportBlockedDirectCrossExecutionDisabled") {
    Fail "Direct-cross handling does not use ImportBlockedDirectCrossExecutionDisabled." "EXEC_SIM_R004_FAIL_DIRECT_CROSS_HANDLING_MISSING"
}

foreach ($rule in @("timestamp must be present and parseable", "bid must be finite positive", "ask must be finite positive", "ask must be greater than or equal to bid", "direct cross must be blocked as execution symbol", "out-of-order quotes are sorted by TimestampUtc")) {
    Require-Contains $rules.validationRules $rule "Import validation rules are missing: $rule." "EXEC_SIM_R004_FAIL_IMPORT_CONTRACT_MISSING"
}
foreach ($status in @("ImportReady", "ImportCompletedWithRejectedRows", "ImportBlockedDirectCrossExecutionDisabled", "ImportBlockedMissingTimestamp", "ImportBlockedMissingBidAsk", "ImportBlockedInvalidBidAsk", "ImportBlockedRawPayloadLeakRisk", "InconclusiveSafe")) {
    Require-Contains $statuses.importResultStatuses $status "Import result statuses are missing $status." "EXEC_SIM_R004_FAIL_IMPORT_CONTRACT_MISSING"
}
foreach ($category in @("MissingTimestamp", "MissingBid", "MissingAsk", "InvalidBidAsk", "UnsupportedSymbol", "DirectCrossExecutionDisabled", "MissingInstrumentConvention", "DuplicateRows", "OutOfOrderRows", "NoQuoteNearClose", "StaleQuoteNearClose", "SpreadTooWide", "RawPayloadLeakRisk", "SecretLeakRisk", "InconclusiveSafe")) {
    Require-Contains $failures.safeFailureCategories $category "Safe failure categories are missing $category." "EXEC_SIM_R004_FAIL_IMPORT_CONTRACT_MISSING"
}

if (@($valid.validFixtures).Count -lt 2) {
    Fail "Valid fixture import evidence is missing EURUSD/USDJPY cases." "EXEC_SIM_R004_FAIL_BUILD_OR_TESTS"
}
Require-False $valid.polygonApiCalled "Polygon API call detected in valid fixture evidence." "EXEC_SIM_R004_FAIL_API_CALL_DETECTED"
Require-False $valid.lmaxCalled "LMAX call detected in valid fixture evidence." "EXEC_SIM_R004_FAIL_API_CALL_DETECTED"
if (@($invalid.invalidFixtures).Count -lt 5) {
    Fail "Invalid fixture rejection evidence is incomplete." "EXEC_SIM_R004_FAIL_BUILD_OR_TESTS"
}
Require-True $invalid.invalidRowsDoNotBecomeValidFixtures "Invalid rows may become valid fixtures." "EXEC_SIM_R004_FAIL_IMPORT_CONTRACT_MISSING"

if ($window.windowStartOffsetMinutesBeforeClose -ne 13 -or $window.quoteCount -le 0 -or $window.quoteCountLastMinute -le 0) {
    Fail "Quote-window extraction evidence is incomplete." "EXEC_SIM_R004_FAIL_BUILD_OR_TESTS"
}
if ($benchmark.closeBenchmarkStatus -ne "Available" -or $benchmark.closeConstructionMethod -ne "BidAskClose") {
    Fail "Close benchmark construction evidence is missing available imported benchmark." "EXEC_SIM_R004_FAIL_CLOSE_BENCHMARK_IMPORT_EVIDENCE_MISSING"
}
if ($benchmark.gapNearCloseStatus -ne "NoQuoteNearClose" -or $benchmark.staleNearCloseStatus -ne "StaleAtClose" -or $benchmark.wideSpreadNearCloseStatus -ne "SpreadTooWide") {
    Fail "Close benchmark safe status evidence is incomplete." "EXEC_SIM_R004_FAIL_CLOSE_BENCHMARK_IMPORT_EVIDENCE_MISSING"
}
if ($feed.quoteCountTMinus13ToClose -le 0 -or $feed.feedQualityScore -le 0 -or [string]::IsNullOrWhiteSpace($feed.feedQualityBucket)) {
    Fail "Feed quality scoring evidence is incomplete." "EXEC_SIM_R004_FAIL_FEED_QUALITY_IMPORT_EVIDENCE_MISSING"
}

Require-False $sanitize.rawProviderPayloadSerialized "Raw provider payload was serialized." "EXEC_SIM_R004_FAIL_RAW_PAYLOAD_OR_SECRET_LEAK_RISK"
Require-False $sanitize.rawProviderPayloadDumpAllowed "Raw provider payload dumps are allowed." "EXEC_SIM_R004_FAIL_RAW_PAYLOAD_OR_SECRET_LEAK_RISK"
Require-False $sanitize.credentialsSerialized "Credentials were serialized." "EXEC_SIM_R004_FAIL_RAW_PAYLOAD_OR_SECRET_LEAK_RISK"
Require-False $sanitize.secretSerialized "Secrets were serialized." "EXEC_SIM_R004_FAIL_RAW_PAYLOAD_OR_SECRET_LEAK_RISK"

Require-False $polygonAudit.polygonApiCalled "Polygon API call detected." "EXEC_SIM_R004_FAIL_API_CALL_DETECTED"
Require-False $polygonAudit.externalApiCalled "External API call detected." "EXEC_SIM_R004_FAIL_API_CALL_DETECTED"
Require-False $polygonAudit.httpClientUsed "HTTP client usage detected." "EXEC_SIM_R004_FAIL_API_CALL_DETECTED"
Require-False $lmaxAudit.lmaxCalled "LMAX call detected." "EXEC_SIM_R004_FAIL_API_CALL_DETECTED"
Require-False $runtimeAudit.brokerActivationDetected "Broker activation detected." "EXEC_SIM_R004_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.socketOpened "Socket opened." "EXEC_SIM_R004_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.tlsOpened "TLS opened." "EXEC_SIM_R004_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.fixOpened "FIX opened." "EXEC_SIM_R004_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.marketDataRequestSent "MarketDataRequest sent." "EXEC_SIM_R004_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.marketDataResponseRead "MarketDataResponse read." "EXEC_SIM_R004_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.apiWorkerLiveGatewayEnabled "API/Worker live gateway enabled." "EXEC_SIM_R004_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.schedulerServiceTimerPollingBackgroundJobIntroduced "Scheduler/service/timer/polling/background job introduced." "EXEC_SIM_R004_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $runtimeAudit.automaticExecutionIntroduced "Automatic execution introduced." "EXEC_SIM_R004_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $orderAudit.ordersCreated "Order created." "EXEC_SIM_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $orderAudit.fillsCreated "Fill created." "EXEC_SIM_R004_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $orderAudit.executionReportsCreated "Execution report created." "EXEC_SIM_R004_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $orderAudit.routesCreated "Route created." "EXEC_SIM_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $orderAudit.submissionsCreated "Submission created." "EXEC_SIM_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

Require-True $usdjpy.caveatPreserved "USDJPY caveat is not preserved." "EXEC_SIM_R004_FAIL_USDJPY_CAVEAT_WEAKENED"
if ($usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8") {
    Fail "USDJPY caveat SecurityID/SecurityIDSource weakened." "EXEC_SIM_R004_FAIL_USDJPY_CAVEAT_WEAKENED"
}
if ($lmaxBaseline.audusdStatus -notmatch "inconclusive" -or ($lmaxBaseline.audusdStatus -match "failed" -and $lmaxBaseline.audusdStatus -notmatch "not failed")) {
    Fail "AUDUSD is incorrectly marked failed." "EXEC_SIM_R004_FAIL_AUDUSD_MISCLASSIFIED"
}
Require-False $lmaxBaseline.lmaxCalledInR004 "LMAX baseline was called in R004." "EXEC_SIM_R004_FAIL_API_CALL_DETECTED"

Require-False $noExternal.polygonApiCalled "No-external audit shows Polygon API call." "EXEC_SIM_R004_FAIL_API_CALL_DETECTED"
Require-False $noExternal.lmaxCalled "No-external audit shows LMAX call." "EXEC_SIM_R004_FAIL_API_CALL_DETECTED"
Require-False $noExternal.externalApiCalled "No-external audit shows external API call." "EXEC_SIM_R004_FAIL_API_CALL_DETECTED"
Require-False $noExternal.socketTlsFixMarketDataRuntimeDetected "No-external audit shows runtime market-data action." "EXEC_SIM_R004_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $noExternal.ordersFillsReportsRoutesSubmissionsCreated "No-external audit shows order/fill/report/route/submission." "EXEC_SIM_R004_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $forbidden.forbiddenActionsDetected "Forbidden action detected." "EXEC_SIM_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED"

if (-not (Test-Path -LiteralPath $SourcePath)) {
    Fail "Source path missing: $SourcePath" "EXEC_SIM_R004_FAIL_BUILD_OR_TESTS"
}
$source = Get-Content -LiteralPath $SourcePath -Raw
foreach ($token in @("HttpClient", "GetAsync", "PostAsync", "SendAsync", "WebSocket", "TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "FixSession", "ConnectAsync", "BackgroundService", "IHostedService", "PeriodicTimer")) {
    if ($source -match [regex]::Escape($token)) {
        Fail "Runtime/external action token detected in R004 source: $token" "EXEC_SIM_R004_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    }
}

if ($evidence.dotnetBuildNoRestore -ne "PASS") {
    Fail "dotnet build --no-restore evidence is not PASS." "EXEC_SIM_R004_FAIL_BUILD_OR_TESTS"
}
if ($evidence.focusedTests -notmatch "^PASS") {
    Fail "Focused R004 test evidence is not PASS." "EXEC_SIM_R004_FAIL_BUILD_OR_TESTS"
}
if ($evidence.unitTests -notmatch "^PASS") {
    Fail "Unit test evidence is not PASS." "EXEC_SIM_R004_FAIL_BUILD_OR_TESTS"
}

Write-Host "EXEC_SIM_R004_PASS_POLYGON_OFFLINE_IMPORT_CONTRACT_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R004_PASS_SANITIZED_QUOTE_FIXTURE_IMPORT_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R004_PASS_CLOSE_BENCHMARK_FROM_IMPORTED_QUOTES_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R004_PASS_NO_API_CALL_IMPORT_GATE_READY_NO_EXTERNAL"
exit 0
