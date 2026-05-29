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
        Fail "Missing artifact: $Path" "EXEC_SIM_R003_FAIL_BUILD_OR_TESTS"
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
    "phase-exec-sim-r003-summary.md",
    "phase-exec-sim-r003-historical-quote-schema-contract.json",
    "phase-exec-sim-r003-quote-window-extraction-contract.json",
    "phase-exec-sim-r003-close-benchmark-construction-contract.json",
    "phase-exec-sim-r003-feed-quality-scoring-contract.json",
    "phase-exec-sim-r003-provider-capability-comparison.json",
    "phase-exec-sim-r003-polygon-readiness-requirements.json",
    "phase-exec-sim-r003-lmax-archive-readiness-requirements.json",
    "phase-exec-sim-r003-fixture-only-provider-readiness.json",
    "phase-exec-sim-r003-usd-pair-coverage-requirements.json",
    "phase-exec-sim-r003-direct-cross-signal-only-handling.json",
    "phase-exec-sim-r003-symbol-mapping-requirements.json",
    "phase-exec-sim-r003-close-benchmark-quality-gate.json",
    "phase-exec-sim-r003-feed-continuity-quality-gate.json",
    "phase-exec-sim-r003-readiness-statuses.json",
    "phase-exec-sim-r003-safe-failure-categories.json",
    "phase-exec-sim-r003-no-api-call-audit.json",
    "phase-exec-sim-r003-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r003-no-raw-payload-secret-leak-audit.json",
    "phase-exec-sim-r003-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r003-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r003-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r003-no-external-audit.json",
    "phase-exec-sim-r003-forbidden-actions-audit.json",
    "phase-exec-sim-r003-next-phase-recommendation.json",
    "phase-exec-sim-r003-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    $path = Join-Path $ArtifactsDir $artifact
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Required R003 artifact is missing: $artifact" "EXEC_SIM_R003_FAIL_BUILD_OR_TESTS"
    }
}

$schema = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r003-historical-quote-schema-contract.json")
$window = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r003-quote-window-extraction-contract.json")
$closeBenchmark = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r003-close-benchmark-construction-contract.json")
$feedQuality = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r003-feed-quality-scoring-contract.json")
$providers = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r003-provider-capability-comparison.json")
$polygon = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r003-polygon-readiness-requirements.json")
$lmaxArchive = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r003-lmax-archive-readiness-requirements.json")
$fixtureOnly = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r003-fixture-only-provider-readiness.json")
$coverage = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r003-usd-pair-coverage-requirements.json")
$directCross = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r003-direct-cross-signal-only-handling.json")
$closeGate = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r003-close-benchmark-quality-gate.json")
$feedGate = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r003-feed-continuity-quality-gate.json")
$statuses = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r003-readiness-statuses.json")
$failures = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r003-safe-failure-categories.json")
$apiAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r003-no-api-call-audit.json")
$runtimeAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r003-no-broker-marketdata-runtime-audit.json")
$leakAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r003-no-raw-payload-secret-leak-audit.json")
$orderAudit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r003-no-order-fill-report-route-audit.json")
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r003-usdjpy-caveat-preservation.json")
$lmaxBaseline = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r003-lmax-readonly-baseline-reference.json")
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r003-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r003-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r003-build-test-validator-evidence.json")

foreach ($field in @("QuoteProvider", "ProviderSymbol", "ExecutionTradableSymbol", "TimestampUtc", "Bid", "Ask", "Mid", "SpreadBps", "QuoteQualityStatus")) {
    Require-Contains $schema.requiredFields $field "Historical quote schema is missing $field." "EXEC_SIM_R003_FAIL_HISTORICAL_QUOTE_SCHEMA_MISSING"
}
Require-False $schema.rawPayloadSerializationAllowed "Historical quote schema allows raw payload serialization." "EXEC_SIM_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-False $schema.secretSerializationAllowed "Historical quote schema allows secret serialization." "EXEC_SIM_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED"

if ($window.windowStartOffsetMinutesBeforeClose -ne 13 -or $window.requiredCadenceWindowMinutes -ne 15) {
    Fail "Quote window extraction does not preserve T-minus-13 to 15-minute close cadence." "EXEC_SIM_R003_FAIL_HISTORICAL_QUOTE_SCHEMA_MISSING"
}

foreach ($method in @("LastValidQuoteBeforeClose", "LastValidMidBeforeClose", "BidAskClose", "InconclusiveSafe")) {
    Require-Contains $closeBenchmark.constructionMethods $method "Close benchmark construction is missing $method." "EXEC_SIM_R003_FAIL_CLOSE_BENCHMARK_QUALITY_GATE_MISSING"
}
foreach ($status in @("Available", "MissingBidAsk", "StaleAtClose", "NoQuoteNearClose", "SpreadTooWide", "InconclusiveSafe")) {
    Require-Contains $closeBenchmark.closeBenchmarkStatuses $status "Close benchmark statuses are missing $status." "EXEC_SIM_R003_FAIL_CLOSE_BENCHMARK_QUALITY_GATE_MISSING"
}

foreach ($metric in @("QuoteCountTMinus13ToClose", "QuoteCountLastMinute", "MaxGapSeconds", "LastQuoteAgeAtCloseSeconds", "MedianSpreadBps", "P95SpreadBps", "BidAskAvailabilityRatio", "BenchmarkAvailabilityRatio", "FeedQualityScore", "FeedQualityBucket")) {
    Require-Contains $feedQuality.requiredMetrics $metric "Feed quality scoring is missing $metric." "EXEC_SIM_R003_FAIL_FEED_QUALITY_GATE_MISSING"
}

$providerNames = @($providers.providers | ForEach-Object { $_.providerName })
foreach ($provider in @("Polygon", "LMAXArchive", "FixtureOnly")) {
    Require-Contains $providerNames $provider "Provider comparison is missing $provider." "EXEC_SIM_R003_FAIL_PROVIDER_COMPARISON_MISSING"
}

Require-True $polygon.docsBackedCandidateOnly "Polygon is not marked docs-backed candidate only." "EXEC_SIM_R003_FAIL_POLYGON_READINESS_MISSING"
Require-False $polygon.polygonApiCalled "Polygon API call was detected." "EXEC_SIM_R003_FAIL_API_CALL_DETECTED"
Require-False $polygon.externalApiCalled "External API call was detected in Polygon readiness." "EXEC_SIM_R003_FAIL_API_CALL_DETECTED"
if ($polygon.readinessStatus -ne "RequiresProviderApiKeyDesign") {
    Fail "Polygon readiness is not set to RequiresProviderApiKeyDesign." "EXEC_SIM_R003_FAIL_POLYGON_READINESS_MISSING"
}

if ($lmaxArchive.readinessStatus -ne "NotReadyUntilArchiveExists") {
    Fail "LMAXArchive is not NotReadyUntilArchiveExists." "EXEC_SIM_R003_FAIL_LMAX_ARCHIVE_READINESS_MISSING"
}
Require-False $lmaxArchive.historicalArchiveExists "LMAX historical archive unexpectedly exists in R003." "EXEC_SIM_R003_FAIL_LMAX_ARCHIVE_READINESS_MISSING"
Require-False $lmaxArchive.lmaxCalled "LMAX was called." "EXEC_SIM_R003_FAIL_API_CALL_DETECTED"

if ($fixtureOnly.readinessStatus -ne "ReadyForFixtureImportOnly") {
    Fail "FixtureOnly provider is not ready for deterministic fixture import." "EXEC_SIM_R003_FAIL_PROVIDER_COMPARISON_MISSING"
}

foreach ($symbol in @("AUDUSD", "EURUSD", "GBPUSD", "NZDUSD", "USDJPY", "USDCAD", "USDCHF", "USDMXN", "USDCNH", "USDNOK", "USDSEK", "USDZAR")) {
    Require-Contains $coverage.requiredExecutionSymbols $symbol "USD-pair coverage is missing $symbol." "EXEC_SIM_R003_FAIL_HISTORICAL_QUOTE_SCHEMA_MISSING"
}
Require-False $directCross.directCrossExecutionAllowed "Direct cross execution is allowed." "EXEC_SIM_R003_FAIL_HISTORICAL_QUOTE_SCHEMA_MISSING"
if ($directCross.directCrossHandling -ne "SignalOnlyRequiresNettingFirst") {
    Fail "Direct-cross handling is not signal-only/RequiresNettingFirst." "EXEC_SIM_R003_FAIL_HISTORICAL_QUOTE_SCHEMA_MISSING"
}

Require-True $closeGate.blocksMissingBidAsk "Close benchmark gate does not block missing bid/ask." "EXEC_SIM_R003_FAIL_CLOSE_BENCHMARK_QUALITY_GATE_MISSING"
Require-True $closeGate.blocksNoQuoteNearClose "Close benchmark gate does not block no quote near close." "EXEC_SIM_R003_FAIL_CLOSE_BENCHMARK_QUALITY_GATE_MISSING"
Require-True $feedGate.blocksMissingBidAsk "Feed quality gate does not block missing bid/ask." "EXEC_SIM_R003_FAIL_FEED_QUALITY_GATE_MISSING"
Require-True $feedGate.blocksMissingTimestamp "Feed quality gate does not block missing timestamp." "EXEC_SIM_R003_FAIL_FEED_QUALITY_GATE_MISSING"

foreach ($status in @("ReadyForHistoricalQuoteImportDesign", "ReadyForFixtureImportOnly", "RequiresProviderApiKeyDesign", "NotReadyUntilArchiveExists", "BlockedMissingBidAsk", "BlockedMissingTimestamp", "BlockedMissingCloseBenchmark", "InconclusiveSafe")) {
    Require-Contains $statuses.readinessStatuses $status "Readiness statuses are missing $status." "EXEC_SIM_R003_FAIL_HISTORICAL_QUOTE_SCHEMA_MISSING"
}
foreach ($category in @("MissingBidAsk", "MissingTimestamp", "MissingSymbolMapping", "MissingWindowCoverage", "QuoteGapNearClose", "StaleQuoteNearClose", "SpreadTooWide", "MissingCloseBenchmark", "DirectCrossExecutionDisabled", "RawPayloadLeakRisk", "SecretLeakRisk", "InconclusiveSafe")) {
    Require-Contains $failures.safeFailureCategories $category "Safe failure categories are missing $category." "EXEC_SIM_R003_FAIL_HISTORICAL_QUOTE_SCHEMA_MISSING"
}

Require-False $apiAudit.polygonApiCalled "Polygon API call detected in audit." "EXEC_SIM_R003_FAIL_API_CALL_DETECTED"
Require-False $apiAudit.lmaxCalled "LMAX call detected in audit." "EXEC_SIM_R003_FAIL_API_CALL_DETECTED"
Require-False $apiAudit.externalApiCalled "External API call detected in audit." "EXEC_SIM_R003_FAIL_API_CALL_DETECTED"
Require-False $runtimeAudit.brokerActivationDetected "Broker activation detected." "EXEC_SIM_R003_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.socketOpened "Socket opened." "EXEC_SIM_R003_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.tlsOpened "TLS opened." "EXEC_SIM_R003_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.fixOpened "FIX opened." "EXEC_SIM_R003_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.marketDataRequestSent "MarketDataRequest sent." "EXEC_SIM_R003_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.marketDataResponseRead "MarketDataResponse read." "EXEC_SIM_R003_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.apiWorkerLiveGatewayEnabled "API/Worker live gateway enabled." "EXEC_SIM_R003_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $runtimeAudit.schedulerServiceTimerPollingBackgroundJobIntroduced "Scheduler/service/timer/polling/background job introduced." "EXEC_SIM_R003_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $runtimeAudit.automaticExecutionIntroduced "Automatic execution introduced." "EXEC_SIM_R003_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
Require-False $leakAudit.secretLeakRisk "Secret leak risk appears." "EXEC_SIM_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-False $leakAudit.rawPayloadLeakRisk "Raw payload leak risk appears." "EXEC_SIM_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-False $orderAudit.ordersCreated "Order created." "EXEC_SIM_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $orderAudit.fillsCreated "Fill created." "EXEC_SIM_R003_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $orderAudit.executionReportsCreated "Execution report created." "EXEC_SIM_R003_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
Require-False $orderAudit.routesCreated "Route created." "EXEC_SIM_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $orderAudit.submissionsCreated "Submission created." "EXEC_SIM_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"

Require-True $usdjpy.caveatPreserved "USDJPY caveat is not preserved." "EXEC_SIM_R003_FAIL_USDJPY_CAVEAT_WEAKENED"
if ($usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8") {
    Fail "USDJPY caveat SecurityID/SecurityIDSource weakened." "EXEC_SIM_R003_FAIL_USDJPY_CAVEAT_WEAKENED"
}
if ($lmaxBaseline.audusdStatus -notmatch "inconclusive" -or ($lmaxBaseline.audusdStatus -match "failed" -and $lmaxBaseline.audusdStatus -notmatch "not failed")) {
    Fail "AUDUSD is incorrectly marked failed." "EXEC_SIM_R003_FAIL_AUDUSD_MISCLASSIFIED"
}
Require-False $lmaxBaseline.lmaxCalledInR003 "LMAX baseline was called in R003." "EXEC_SIM_R003_FAIL_API_CALL_DETECTED"

Require-False $noExternal.polygonApiCalled "No-external audit shows Polygon call." "EXEC_SIM_R003_FAIL_API_CALL_DETECTED"
Require-False $noExternal.lmaxCalled "No-external audit shows LMAX call." "EXEC_SIM_R003_FAIL_API_CALL_DETECTED"
Require-False $noExternal.externalApiCalled "No-external audit shows external API call." "EXEC_SIM_R003_FAIL_API_CALL_DETECTED"
Require-False $noExternal.socketTlsFixMarketDataRuntimeDetected "No-external audit shows runtime market-data action." "EXEC_SIM_R003_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
Require-False $noExternal.ordersFillsReportsRoutesSubmissionsCreated "No-external audit shows order/fill/report/route/submission." "EXEC_SIM_R003_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
Require-False $forbidden.forbiddenActionsDetected "Forbidden action detected." "EXEC_SIM_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED"

if (-not (Test-Path -LiteralPath $SourcePath)) {
    Fail "Source path missing: $SourcePath" "EXEC_SIM_R003_FAIL_BUILD_OR_TESTS"
}
$source = Get-Content -LiteralPath $SourcePath -Raw
foreach ($token in @("HttpClient", "GetAsync", "PostAsync", "SendAsync", "TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "FixSession", "ConnectAsync", "BackgroundService", "IHostedService", "PeriodicTimer")) {
    if ($source -match [regex]::Escape($token)) {
        Fail "Runtime/external action token detected in R003 source: $token" "EXEC_SIM_R003_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    }
}

if ($evidence.dotnetBuildNoRestore -ne "PASS") {
    Fail "dotnet build --no-restore evidence is not PASS." "EXEC_SIM_R003_FAIL_BUILD_OR_TESTS"
}
if ($evidence.focusedTests -notmatch "^PASS") {
    Fail "Focused R003 test evidence is not PASS." "EXEC_SIM_R003_FAIL_BUILD_OR_TESTS"
}
if ($evidence.unitTests -notmatch "^PASS") {
    Fail "Unit test evidence is not PASS." "EXEC_SIM_R003_FAIL_BUILD_OR_TESTS"
}

Write-Host "EXEC_SIM_R003_PASS_HISTORICAL_QUOTE_DATA_READINESS_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R003_PASS_POLYGON_LMAX_FEED_COMPARISON_CONTRACT_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R003_PASS_CLOSE_BENCHMARK_QUALITY_GATE_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R003_PASS_NO_API_CALL_FEED_READINESS_GATE_READY_NO_EXTERNAL"
exit 0
