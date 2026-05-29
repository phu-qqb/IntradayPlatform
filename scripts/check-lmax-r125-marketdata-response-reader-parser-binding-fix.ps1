param(
    [string]$ArtifactsRoot = "artifacts/readiness/lmax-runtime-enablement"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Code, [string]$Message) {
    Write-Error "$Code $Message"
    exit 1
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "LMAX_R125_FAIL_BUILD_OR_TESTS" "Missing required artifact: $Path"
    }

    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

$required = @(
    "phase-lmax-r125-marketdata-response-reader-parser-binding-fix-report.md",
    "phase-lmax-r125-marketdata-response-reader-parser-binding-summary.json",
    "phase-lmax-r125-before-after-classification.json",
    "phase-lmax-r125-response-reader-validation.json",
    "phase-lmax-r125-parser-classifier-validation.json",
    "phase-lmax-r125-bounded-read-wait-validation.json",
    "phase-lmax-r125-approved-instrument-scope-validation.json",
    "phase-lmax-r125-usdjpy-caveat-preservation.json",
    "phase-lmax-r125-non-approved-instrument-rejection-validation.json",
    "phase-lmax-r125-readonly-safety-validation.json",
    "phase-lmax-r125-raw-fix-sanitization-validation.json",
    "phase-lmax-r125-no-external-boundary-attempted.json",
    "phase-lmax-r125-forbidden-actions-audit.json",
    "phase-lmax-r125-api-worker-fake-gateway-audit.json",
    "phase-lmax-r125-next-phase-recommendation.json",
    "phase-lmax-r125-gate-validation.json"
)

foreach ($file in $required) {
    $path = Join-Path $ArtifactsRoot $file
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "LMAX_R125_FAIL_BUILD_OR_TESTS" "Missing required artifact: $file"
    }
}

$summary = Read-Json (Join-Path $ArtifactsRoot "phase-lmax-r125-marketdata-response-reader-parser-binding-summary.json")
$reader = Read-Json (Join-Path $ArtifactsRoot "phase-lmax-r125-response-reader-validation.json")
$parser = Read-Json (Join-Path $ArtifactsRoot "phase-lmax-r125-parser-classifier-validation.json")
$bounded = Read-Json (Join-Path $ArtifactsRoot "phase-lmax-r125-bounded-read-wait-validation.json")
$scope = Read-Json (Join-Path $ArtifactsRoot "phase-lmax-r125-approved-instrument-scope-validation.json")
$usdJpy = Read-Json (Join-Path $ArtifactsRoot "phase-lmax-r125-usdjpy-caveat-preservation.json")
$rejection = Read-Json (Join-Path $ArtifactsRoot "phase-lmax-r125-non-approved-instrument-rejection-validation.json")
$safety = Read-Json (Join-Path $ArtifactsRoot "phase-lmax-r125-readonly-safety-validation.json")
$sanitize = Read-Json (Join-Path $ArtifactsRoot "phase-lmax-r125-raw-fix-sanitization-validation.json")
$external = Read-Json (Join-Path $ArtifactsRoot "phase-lmax-r125-no-external-boundary-attempted.json")
$forbidden = Read-Json (Join-Path $ArtifactsRoot "phase-lmax-r125-forbidden-actions-audit.json")
$apiWorker = Read-Json (Join-Path $ArtifactsRoot "phase-lmax-r125-api-worker-fake-gateway-audit.json")
$gate = Read-Json (Join-Path $ArtifactsRoot "phase-lmax-r125-gate-validation.json")
$next = Read-Json (Join-Path $ArtifactsRoot "phase-lmax-r125-next-phase-recommendation.json")

if ($summary.classification -ne "LMAX_R125_PASS_MARKETDATA_RESPONSE_READER_PARSER_BINDING_READY_NO_EXTERNAL_ACTIVATION") {
    Fail "LMAX_R125_FAIL_MARKETDATA_RESPONSE_READER_NOT_PROVABLE" "Unexpected classification."
}

if (-not $reader.readerReady -or -not $summary.marketDataResponseReaderReady) {
    Fail "LMAX_R125_FAIL_MARKETDATA_RESPONSE_READER_NOT_PROVABLE" "Response reader readiness is not provable."
}

if (-not $parser.parserClassifierReady -or -not $summary.marketDataResponseParserClassifierReady) {
    Fail "LMAX_R125_FAIL_MARKETDATA_RESPONSE_PARSER_NOT_PROVABLE" "Response parser/classifier readiness is not provable."
}

if (-not $bounded.boundedReadWaitReady -or -not $bounded.finiteTimeoutRequired -or -not $bounded.singleBoundedReadPerRequestSuccess -or -not $summary.boundedReadWaitFinite) {
    Fail "LMAX_R125_FAIL_BOUNDED_READ_WAIT_NOT_PROVABLE" "Finite bounded read/wait is not provable."
}

if ($bounded.pollingLoopIntroduced -or $bounded.schedulerIntroduced -or $bounded.hostedServiceIntroduced) {
    Fail "LMAX_R125_FAIL_UNBOUNDED_POLLING_RISK" "Polling/scheduler/service risk detected."
}

if (-not $reader.reachableOnlyAfterMarketDataRequestSuccess -or -not $summary.marketDataResponseReadBlockedUntilRequestSuccess) {
    Fail "LMAX_R125_FAIL_MARKETDATA_RESPONSE_ALLOWED_WITHOUT_REQUEST_SUCCESS" "Response read is not gated on request success."
}

if (-not $reader.marketDataRequestRequiresFixSuccess -or -not $summary.marketDataRequestBlockedUntilFixSuccess) {
    Fail "LMAX_R125_FAIL_MARKETDATA_ALLOWED_WITHOUT_FIX_SUCCESS" "MarketDataRequest is not gated on FIX success."
}

$expectedCategories = @(
    "MarketDataSnapshotObserved",
    "MarketDataIncrementalObserved",
    "MarketDataRejectObserved",
    "MarketDataNoEntriesObserved",
    "MarketDataReadTimeout",
    "MarketDataMalformedFrame",
    "MarketDataUnknownFailure",
    "MarketDataResponseNotAttempted"
)
foreach ($category in $expectedCategories) {
    if ($parser.supportedSanitizedCategories -notcontains $category) {
        Fail "LMAX_R125_FAIL_MARKETDATA_RESPONSE_PARSER_NOT_PROVABLE" "Missing sanitized category: $category"
    }
}

$expectedInstruments = @("GBPUSD", "EURGBP", "AUDUSD", "USDJPY")
if (@($scope.approvedInstruments).Count -ne 4) {
    Fail "LMAX_R125_FAIL_NON_APPROVED_INSTRUMENT_ALLOWED" "Approved instrument count mismatch."
}
foreach ($symbol in $expectedInstruments) {
    if ($scope.approvedInstruments -notcontains $symbol -or $summary.approvedInstruments -notcontains $symbol) {
        Fail "LMAX_R125_FAIL_NON_APPROVED_INSTRUMENT_ALLOWED" "Missing approved instrument: $symbol"
    }
}

if (-not $scope.approvedInstrumentScopeExact -or -not $scope.nonApprovedInstrumentsRejected -or -not $rejection.nonApprovedInstrumentsRejected) {
    Fail "LMAX_R125_FAIL_NON_APPROVED_INSTRUMENT_ALLOWED" "Non-approved instrument rejection is not provable."
}

if ($usdJpy.securityId -ne "4004" -or $usdJpy.securityIdSource -ne "8" -or -not $usdJpy.caveatPreserved -or $usdJpy.mappingWeakened) {
    Fail "LMAX_R125_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY mapping/caveat weakened."
}

if ($parser.orderFramesSupported -or $parser.newOrderSingleSupported -or $parser.cancelReplaceSupported -or $parser.executionReportFillOrderLifecycleParsingSupported -or
    $safety.ordersSupported -or $safety.tradingEnablementIntroduced -or $safety.tradingStateMutationIntroduced) {
    Fail "LMAX_R125_FAIL_MARKETDATA_PARSER_SUPPORTS_TRADING_OR_ORDERS" "Order/trading support detected."
}

if ($sanitize.rawFixSerialized -or $sanitize.rawFixMessagesPrinted -or $sanitize.rawSensitiveFixLogsSerialized -or
    $sanitize.rawCredentialsSerialized -or $sanitize.rawEndpointSerialized -or $sanitize.rawTlsMaterialSerialized -or
    $sanitize.rawSessionIdentifierSerialized -or $sanitize.credentialValuesReturned -or $summary.credentialValuesReturned) {
    Fail "LMAX_R125_FAIL_RAW_FIX_SERIALIZATION_RISK" "Raw FIX/secret/session serialization risk detected."
}

if ($external.tcpSocket -ne "NotAttempted" -or $external.tls -ne "NotAttempted" -or
    $external.fixLogonSession -notin @("ValidationOnly", "NotAttempted") -or
    $external.marketDataRequest -notin @("ValidationOnly", "NotAttempted") -or
    $summary.externalActivationAttempted -or $summary.tcpSocketAttempted -or $summary.tlsAttempted -or
    $summary.fixLogonAttempted -or $summary.liveMarketDataRequestAttempted -or $summary.liveMarketDataResponseAttempted) {
    Fail "LMAX_R125_FAIL_EXTERNAL_BOUNDARY_ATTEMPTED" "External boundary was attempted during R125."
}

if ($forbidden.audit -ne "PASS" -or $forbidden.externalBoundaryAttempted -or $forbidden.scheduler -or $forbidden.pollingLoop -or $forbidden.replay -or $forbidden.shadowReplay) {
    Fail "LMAX_R125_FAIL_FORBIDDEN_ACTION_INTRODUCED" "Forbidden action audit failed."
}

if ($apiWorker.audit -ne "PASS" -or $apiWorker.apiGateway -ne "FakeLmaxGatewayOnly" -or $apiWorker.workerGateway -ne "FakeLmaxGatewayOnly" -or $apiWorker.apiWorkerReachable) {
    Fail "LMAX_R125_FAIL_API_WORKER_GATEWAY_REGRESSION" "API/Worker FakeLmaxGatewayOnly regression."
}

if (-not $gate.buildEvidencePresent -or -not $gate.testEvidencePresent -or $summary.buildResult -ne "PASS" -or -not ($summary.focusedTests -like "PASS*")) {
    Fail "LMAX_R125_FAIL_BUILD_OR_TESTS" "Build/test evidence missing."
}

if ($next.recommendedNextPhase -ne "LMAX-R127") {
    Fail "LMAX_R125_FAIL_BUILD_OR_TESTS" "Next phase recommendation missing or incorrect."
}

$artifactText = Get-ChildItem -LiteralPath $ArtifactsRoot -Filter "phase-lmax-r125-*" -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combined = [string]::Join("`n", $artifactText)
$blockedPatterns = @(
    "BEGIN PRIVATE KEY",
    "BEGIN CERTIFICATE",
    "password=",
    "username=",
    "sessionToken"
)
foreach ($pattern in $blockedPatterns) {
    if ($combined.IndexOf($pattern, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
        Fail "LMAX_R125_FAIL_CREDENTIAL_VALUES_RETURNED_OR_SECRET_RISK" "Blocked sensitive pattern detected in R125 artifacts."
    }
}

Write-Output "LMAX_R125_VALIDATION_PASS"
