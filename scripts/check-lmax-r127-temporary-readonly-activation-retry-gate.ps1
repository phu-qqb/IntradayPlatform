$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'

function Fail($code, $message) {
    Write-Error "$code $message"
    exit 1
}

function Read-Json($name) {
    $path = Join-Path $artifactRoot $name
    if (-not (Test-Path -LiteralPath $path)) {
        Fail 'LMAX_R127_FAIL_BUILD_OR_TESTS' "Missing artifact: $name"
    }

    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$requiredArtifacts = @(
    'phase-lmax-r127-temporary-readonly-activation-retry-report.md',
    'phase-lmax-r127-temporary-readonly-activation-retry-summary.json',
    'phase-lmax-r127-operator-approval-note.md',
    'phase-lmax-r127-operator-approval.txt',
    'phase-lmax-r127-expected-operator-approval.txt',
    'phase-lmax-r127-preflight-result.json',
    'phase-lmax-r127-demo-endpoint-binding-evidence.json',
    'phase-lmax-r127-socket-connector-evidence.json',
    'phase-lmax-r127-tls-boundary-evidence.json',
    'phase-lmax-r127-fix-session-boundary-evidence.json',
    'phase-lmax-r127-marketdata-request-evidence.json',
    'phase-lmax-r127-marketdata-response-evidence.json',
    'phase-lmax-r127-approved-instrument-evidence.json',
    'phase-lmax-r127-usdjpy-caveat-preservation.json',
    'phase-lmax-r127-operational-invocation-trace.json',
    'phase-lmax-r127-boundary-evidence.json',
    'phase-lmax-r127-marketdata-sanitized-result.json',
    'phase-lmax-r127-forbidden-actions-audit.json',
    'phase-lmax-r127-api-worker-fake-gateway-audit.json',
    'phase-lmax-r127-shutdown-revert-evidence.json',
    'phase-lmax-r127-next-phase-recommendation.json',
    'phase-lmax-r127-gate-validation.json'
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $artifact))) {
        Fail 'LMAX_R127_FAIL_BUILD_OR_TESTS' "Missing artifact: $artifact"
    }
}

$approval = (Get-Content -LiteralPath (Join-Path $artifactRoot 'phase-lmax-r127-operator-approval.txt') -Raw).Trim()
$expectedApproval = (Get-Content -LiteralPath (Join-Path $artifactRoot 'phase-lmax-r127-expected-operator-approval.txt') -Raw).Trim()
if ($approval -ne $expectedApproval) {
    Fail 'LMAX_R127_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED' 'Operator approval is missing or mismatched.'
}

$r125 = Read-Json 'phase-lmax-r125-marketdata-response-reader-parser-binding-summary.json'
$summary = Read-Json 'phase-lmax-r127-temporary-readonly-activation-retry-summary.json'
$preflight = Read-Json 'phase-lmax-r127-preflight-result.json'
$endpoint = Read-Json 'phase-lmax-r127-demo-endpoint-binding-evidence.json'
$socket = Read-Json 'phase-lmax-r127-socket-connector-evidence.json'
$tls = Read-Json 'phase-lmax-r127-tls-boundary-evidence.json'
$fix = Read-Json 'phase-lmax-r127-fix-session-boundary-evidence.json'
$request = Read-Json 'phase-lmax-r127-marketdata-request-evidence.json'
$response = Read-Json 'phase-lmax-r127-marketdata-response-evidence.json'
$instruments = Read-Json 'phase-lmax-r127-approved-instrument-evidence.json'
$usdjpy = Read-Json 'phase-lmax-r127-usdjpy-caveat-preservation.json'
$trace = Read-Json 'phase-lmax-r127-operational-invocation-trace.json'
$boundary = Read-Json 'phase-lmax-r127-boundary-evidence.json'
$result = Read-Json 'phase-lmax-r127-marketdata-sanitized-result.json'
$forbidden = Read-Json 'phase-lmax-r127-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r127-api-worker-fake-gateway-audit.json'
$shutdown = Read-Json 'phase-lmax-r127-shutdown-revert-evidence.json'
$next = Read-Json 'phase-lmax-r127-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r127-gate-validation.json'

if ($r125.classification -ne 'LMAX_R125_PASS_MARKETDATA_RESPONSE_READER_PARSER_BINDING_READY_NO_EXTERNAL_ACTIVATION' -or
    -not $r125.marketDataResponseReaderReady -or
    -not $r125.marketDataResponseParserClassifierReady -or
    -not $r125.boundedReadWaitReady) {
    Fail 'LMAX_R127_FAIL_MARKETDATA_RESPONSE_BINDING_REGRESSION' 'R125 response reader/parser evidence missing or mismatched.'
}

if ($summary.classification -ne 'LMAX_R127_FAIL_MARKETDATA_RESPONSE_BOUNDARY') {
    Fail 'LMAX_R127_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE' 'Unexpected R127 classification.'
}

if (-not $summary.externalActivationAttempted -or $summary.attemptCount -ne 1 -or $trace.attemptCount -ne 1) {
    Fail 'LMAX_R127_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE' 'R127 must record exactly one external attempt.'
}

if (-not $summary.retryPhaseReservationPassed -or -not $preflight.retryPhaseReservationPassed) {
    Fail 'LMAX_R127_FAIL_BUILD_OR_TESTS' 'R127 retry phase reservation missing.'
}

if ($summary.toolUsed -ne 'QQ.Production.Intraday.Tools.LmaxReadOnlyActivation' -or
    $summary.adapterMode -ne 'real-bounded-executable-readonly' -or
    $preflight.adapterMode -ne 'real-bounded-executable-readonly') {
    Fail 'LMAX_R127_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE' 'Tool or adapter mode mismatch.'
}

if (-not $preflight.operatorApprovalMatched -or -not $preflight.r125SuccessEvidencePresent -or
    -not $preflight.executeOnceInvoked -or -not $preflight.singleAttemptOnly -or
    -not $preflight.noApiWorkerStartup -or -not $preflight.noServiceSchedulerPolling -or
    -not $preflight.noOrderTradingPath -or -not $preflight.noCredentialOutput) {
    Fail 'LMAX_R127_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED' 'Preflight safety evidence missing.'
}

if ($endpoint.endpointMode -ne 'Demo' -or -not $endpoint.endpointPresent -or -not $endpoint.hostPresent -or
    -not $endpoint.hostConcreteBinding -or $endpoint.hostWasPlaceholder -or -not $endpoint.portPresent -or
    -not $endpoint.portConcreteBinding -or -not $endpoint.productionExcluded -or -not $endpoint.endpointApproved -or
    $endpoint.rawEndpointSerialized) {
    Fail 'LMAX_R127_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE' 'Demo endpoint evidence failed.'
}

if (-not $socket.configuredSocketConnectorUsed -or -not $socket.connectReached -or -not $socket.tcpConnectionAttempted -or
    -not $socket.tcpSocketSucceeded -or $socket.boundaryStatus -ne 'Succeeded') {
    Fail 'LMAX_R127_FAIL_TCP_SOCKET_BOUNDARY' 'TCP/socket boundary did not succeed.'
}

if (-not $tls.tlsAttempted -or -not $tls.authenticateTlsReached -or -not $tls.tlsSucceeded -or
    $tls.tlsBoundaryStatus -ne 'Succeeded' -or $tls.tlsResultCategory -ne 'Succeeded' -or $tls.tlsRawMaterialSerialized) {
    Fail 'LMAX_R127_FAIL_TLS_BOUNDARY' 'TLS boundary did not succeed safely.'
}

if (-not $fix.fixLogonSessionAttempted -or -not $fix.socketConnectorOpenFixSessionReached -or
    -not $fix.fixLogonSessionSucceeded -or $fix.fixAcknowledgementCategory -ne 'FixLogonAcknowledged' -or
    -not $fix.fixAcknowledgementReaderParserClassifierUsed -or $fix.rawFixFrameOrMessageSerialized -or
    -not $fix.orderCapableFixFrameOrParserPathAbsent) {
    Fail 'LMAX_R127_FAIL_FIX_LOGON_OR_SESSION_BOUNDARY' 'FIX session boundary did not succeed safely.'
}

if (-not $request.fixSessionSucceededBeforeMarketData -or -not $summary.fixLogonSessionSucceeded) {
    Fail 'LMAX_R127_FAIL_MARKETDATA_ALLOWED_WITHOUT_FIX_SUCCESS' 'MarketDataRequest was not proven gated on FIX success.'
}

if (-not $request.marketDataRequestOperationBindingReady -or -not $request.marketDataRequestBuilderReady -or
    -not $request.marketDataRequestWriterReady -or -not $request.marketDataRequestAttempted -or
    $request.marketDataRequestResult -ne 'Succeeded') {
    Fail 'LMAX_R127_FAIL_MARKETDATA_REQUEST_BOUNDARY' 'MarketDataRequest boundary evidence missing.'
}

if (-not $summary.marketDataResponseReadAttempted -or -not $response.marketDataResponseReadAttempted -or
    -not $response.marketDataResponseReaderParserClassifierUsed -or -not $response.boundedReadWaitUsed) {
    Fail 'LMAX_R127_FAIL_MARKETDATA_RESPONSE_BINDING_REGRESSION' 'MarketDataResponse reader/parser was not reached after request success.'
}

if ($response.marketDataResponseResult -ne 'FailedValidation' -or $response.marketDataResponseCategory -ne 'SessionRejectObserved' -or
    $result.marketDataResponseCategory -ne 'SessionRejectObserved') {
    Fail 'LMAX_R127_FAIL_MARKETDATA_RESPONSE_BOUNDARY' 'MarketDataResponse category mismatch.'
}

if ($response.rawMarketDataPayloadSerialized -or $response.rawFixSerialized -or $request.rawFixSerialized -or $request.rawSessionValuesSerialized) {
    Fail 'LMAX_R127_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' 'Raw market-data/FIX/session material serialized.'
}

$expectedInstruments = @('GBPUSD', 'EURGBP', 'AUDUSD', 'USDJPY')
if (($instruments.approvedInstrumentsRequested -join ',') -ne ($expectedInstruments -join ',') -or
    -not $instruments.approvedInstrumentScopeExact -or -not $instruments.nonApprovedInstrumentsAbsent -or
    -not $summary.nonApprovedInstrumentsAbsent -or -not $request.nonApprovedInstrumentsAbsent) {
    Fail 'LMAX_R127_FAIL_NON_APPROVED_INSTRUMENT_ALLOWED' 'Approved instrument scope failed.'
}

if ($usdjpy.securityId -ne '4004' -or $usdjpy.securityIdSource -ne '8' -or -not $usdjpy.caveatPreserved -or
    -not $instruments.usdJpySecurityIdPreserved -or -not $instruments.usdJpySecurityIdSourcePreserved -or
    -not $instruments.usdJpyCaveatPreserved) {
    Fail 'LMAX_R127_FAIL_USDJPY_CAVEAT_WEAKENED' 'USDJPY mapping/caveat weakened.'
}

if ($boundary.'Credential/config' -ne 'Succeeded' -or $boundary.'TCP/socket' -ne 'Succeeded' -or
    $boundary.TLS -ne 'Succeeded' -or $boundary.'FIX logon/session' -ne 'Succeeded' -or
    $boundary.MarketDataRequest -ne 'Succeeded' -or $boundary.'MarketDataResponse/entries' -ne 'FailedValidation' -or
    $boundary.'Shutdown/revert' -ne 'Succeeded') {
    Fail 'LMAX_R127_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE' 'Boundary evidence mismatch.'
}

if ($trace.retryLoopUsed -or $trace.pollingLoopUsed -or -not $trace.callOnceInvoked -or
    -not $trace.invokeOnceInvoked -or -not $trace.executeOnceInvoked) {
    Fail 'LMAX_R127_FAIL_FORBIDDEN_ACTION_RISK' 'Invocation trace violates single-attempt/no-loop posture.'
}

if ($summary.credentialValuesReturned -or $summary.sensitiveValuesPrintedStoredSerialized -or
    $gate.credentialValuesReturned -or $gate.sensitiveValuesPrintedStoredSerialized) {
    Fail 'LMAX_R127_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' 'Credential values returned or sensitive values serialized.'
}

if ($forbidden.result -ne 'PASS' -or $forbidden.orders -or $forbidden.newOrderSingle -or $forbidden.cancelReplace -or
    $forbidden.tradingEnablement -or $forbidden.tradingStateMutation -or $forbidden.scheduler -or
    $forbidden.pollingLoop -or $forbidden.replay -or $forbidden.shadowReplay -or
    $forbidden.executionReportFillOrderLifecycleParsing -or $forbidden.nonApprovedInstruments) {
    Fail 'LMAX_R127_FAIL_FORBIDDEN_ACTION_RISK' 'Forbidden action audit failed.'
}

if ($apiWorker.result -ne 'PASS' -or -not $apiWorker.apiWorkerFakeLmaxGatewayOnly -or
    $apiWorker.apiStartupAttempted -or $apiWorker.workerStartupAttempted) {
    Fail 'LMAX_R127_FAIL_API_WORKER_GATEWAY_REGRESSION' 'API/Worker FakeLmaxGatewayOnly audit failed.'
}

if (-not $shutdown.shutdownRevertCompleted -or $shutdown.shutdownRevertStatus -ne 'Succeeded') {
    Fail 'LMAX_R127_FAIL_SHUTDOWN_REVERT_INCOMPLETE' 'Shutdown/revert evidence missing.'
}

if ($next.nextRecommendedPhase -ne 'Phase LMAX-R128 - Temporary Runtime Evidence Review and Controlled Read-Only Readiness Gate') {
    Fail 'LMAX_R127_FAIL_BUILD_OR_TESTS' 'Next phase recommendation missing.'
}

if ($gate.validatorResult -ne 'LMAX_R127_VALIDATION_PASS' -or $gate.buildResult -notlike 'PASS*' -or
    $gate.focusedTests -notlike 'PASS*' -or $gate.unitTests -notlike 'PASS*' -or $gate.integrationTests -notlike 'PASS*') {
    Fail 'LMAX_R127_FAIL_BUILD_OR_TESTS' 'Build/test/validator evidence missing.'
}

$reservationSource = Get-Content -LiteralPath (Join-Path $root 'src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxApprovedBoundedExecutableRetryPhaseReservations.cs') -Raw
if ($reservationSource -notmatch 'LMAX-R127') {
    Fail 'LMAX_R127_FAIL_BUILD_OR_TESTS' 'R127 retry reservation missing.'
}

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r127-*' -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$joined = $artifactText -join "`n"
$blockedPatterns = @(
    'password=',
    'username=',
    'SenderCompID=',
    'TargetCompID=',
    'BEGIN CERTIFICATE',
    'PRIVATE KEY'
)
foreach ($pattern in $blockedPatterns) {
    if ($joined.IndexOf($pattern, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        Fail 'LMAX_R127_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' "Sensitive or protocol material found in R127 artifacts: $pattern"
    }
}

Write-Host 'LMAX_R127_VALIDATION_PASS'
