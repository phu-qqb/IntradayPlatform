param(
    [string]$Root = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Error $Message
    exit 1
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing required artifact: $Path"
    }

    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

$artifactRoot = Join-Path $Root "artifacts/readiness/lmax-runtime-enablement"
$required = @(
    "phase-lmax-r71-temporary-readonly-activation-retry-report.md",
    "phase-lmax-r71-temporary-readonly-activation-retry-summary.json",
    "phase-lmax-r71-operator-approval-note.md",
    "phase-lmax-r71-preflight-result.json",
    "phase-lmax-r71-demo-endpoint-binding-evidence.json",
    "phase-lmax-r71-socket-connector-evidence.json",
    "phase-lmax-r71-tls-boundary-evidence.json",
    "phase-lmax-r71-operational-invocation-trace.json",
    "phase-lmax-r71-boundary-evidence.json",
    "phase-lmax-r71-marketdata-sanitized-result.json",
    "phase-lmax-r71-forbidden-actions-audit.json",
    "phase-lmax-r71-api-worker-fake-gateway-audit.json",
    "phase-lmax-r71-usdjpy-caveat-preservation.json",
    "phase-lmax-r71-shutdown-revert-evidence.json",
    "phase-lmax-r71-next-phase-recommendation.json",
    "phase-lmax-r71-gate-validation.json"
)

foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $name))) {
        Fail "Missing required R71 artifact: $name"
    }
}

$allowed = @(
    "LMAX_R71_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED",
    "LMAX_R71_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED",
    "LMAX_R71_FAIL_R70_SUCCESS_EVIDENCE_MISSING",
    "LMAX_R71_FAIL_DEMO_ENDPOINT_BINDING_REGRESSION",
    "LMAX_R71_FAIL_PLACEHOLDER_HOST_USED",
    "LMAX_R71_FAIL_SOCKET_CONNECTOR_BINDING_REGRESSION",
    "LMAX_R71_FAIL_MANUAL_EXECUTION_SURFACE_REGRESSION",
    "LMAX_R71_FAIL_REAL_BOUNDED_ADAPTER_BINDING_REGRESSION",
    "LMAX_R71_FAIL_NO_EXTERNAL_ADAPTER_USED_INSTEAD_OF_REAL_BOUNDARY_ADAPTER",
    "LMAX_R71_FAIL_EXECUTE_ONCE_NOT_INVOKED",
    "LMAX_R71_FAIL_PRE_EXTERNAL_APPROVAL_OR_COMPOSITION_REGRESSION",
    "LMAX_R71_FAIL_CREDENTIAL_CONFIG_MISSING",
    "LMAX_R71_FAIL_CREDENTIAL_CONFIG_NOT_APPROVED",
    "LMAX_R71_FAIL_CREDENTIAL_CONFIG_BINDING_REGRESSION",
    "LMAX_R71_FAIL_PRODUCTION_ACCOUNT_RISK",
    "LMAX_R71_FAIL_API_WORKER_GATEWAY_REGRESSION",
    "LMAX_R71_FAIL_LIVE_LAUNCHER_OR_BACKGROUND_SERVICE_RISK",
    "LMAX_R71_FAIL_FORBIDDEN_ACTION_RISK",
    "LMAX_R71_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R71_FAIL_PREFLIGHT_ABORTED",
    "LMAX_R71_FAIL_TCP_SOCKET_BOUNDARY",
    "LMAX_R71_FAIL_TLS_BOUNDARY",
    "LMAX_R71_FAIL_FIX_LOGON_OR_SESSION_BOUNDARY",
    "LMAX_R71_FAIL_MARKETDATA_REQUEST_BOUNDARY",
    "LMAX_R71_FAIL_MARKETDATA_RESPONSE_BOUNDARY",
    "LMAX_R71_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE",
    "LMAX_R71_FAIL_SHUTDOWN_REVERT_INCOMPLETE",
    "LMAX_R71_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK",
    "LMAX_R71_FAIL_BUILD_OR_TESTS"
)

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r71-temporary-readonly-activation-retry-summary.json")
$preflight = Read-Json (Join-Path $artifactRoot "phase-lmax-r71-preflight-result.json")
$endpoint = Read-Json (Join-Path $artifactRoot "phase-lmax-r71-demo-endpoint-binding-evidence.json")
$socket = Read-Json (Join-Path $artifactRoot "phase-lmax-r71-socket-connector-evidence.json")
$tls = Read-Json (Join-Path $artifactRoot "phase-lmax-r71-tls-boundary-evidence.json")
$trace = Read-Json (Join-Path $artifactRoot "phase-lmax-r71-operational-invocation-trace.json")
$boundary = Read-Json (Join-Path $artifactRoot "phase-lmax-r71-boundary-evidence.json")
$marketData = Read-Json (Join-Path $artifactRoot "phase-lmax-r71-marketdata-sanitized-result.json")
$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r71-forbidden-actions-audit.json")
$apiWorker = Read-Json (Join-Path $artifactRoot "phase-lmax-r71-api-worker-fake-gateway-audit.json")
$usdJpy = Read-Json (Join-Path $artifactRoot "phase-lmax-r71-usdjpy-caveat-preservation.json")
$shutdown = Read-Json (Join-Path $artifactRoot "phase-lmax-r71-shutdown-revert-evidence.json")
$gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r71-gate-validation.json")
$r70Gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r70-gate-validation.json")
$approvalText = Get-Content -LiteralPath (Join-Path $artifactRoot "phase-lmax-r71-operator-approval-note.md") -Raw

if ($allowed -notcontains $summary.classification -or $allowed -notcontains $gate.classification) {
    Fail "R71 classification is absent or not allowed."
}

if ($summary.classification -ne "LMAX_R71_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE" -or $gate.classification -ne $summary.classification) {
    Fail "Unexpected R71 classification: $($summary.classification)"
}

if ($r70Gate.classification -ne "LMAX_R70_PASS_RUNTIME_EVIDENCE_REVIEW_CONTROLLED_READINESS_GATE" -or $r70Gate.validatorResult -ne "PASS") {
    Fail "R70 success evidence is missing or not passing."
}

$expectedApproval = "I, Philippe, explicitly approve Phase LMAX-R71 for one temporary Demo read-only runtime market-data activation retry after the R70 TCP socket success evidence review for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority."
if ($approvalText.Trim() -ne $expectedApproval -or -not $preflight.operatorApprovalExactTextPresent) {
    Fail "Exact R71 operator approval text missing or mismatched."
}

if (-not $summary.retryPhaseReservationPassed -or -not $preflight.retryPhaseReservationPassed -or -not $gate.retryPhaseReservationPassed) {
    Fail "LMAX-R71 did not pass retry phase reservation."
}

foreach ($flag in @("manualExecutionSurfaceUsed", "adapterModeRealBoundedExecutableReadOnlyUsed", "concreteDemoEndpointBindingUsed", "configuredSocketConnectorUsed", "lmaxReadOnlyActivationManualTcpSocketConnectorConnectReached", "lmaxManualBoundedReadOnlyActivationCallerCallOnceInvoked", "lmaxBoundedReadOnlyActivationInvocationPathInvokeOnceInvoked", "lmaxTemporaryReadOnlyActivationExecutorExecuteOnceInvoked")) {
    if (-not $summary.$flag) {
        Fail "Summary invocation/adapter/socket flag is false: $flag"
    }
}

foreach ($obj in @($summary, $endpoint)) {
    if ($obj.endpointMode -ne "Demo" -or -not $obj.endpointPresent -or -not $obj.hostPresent -or -not $obj.hostConcreteBinding -or $obj.hostWasPlaceholder -or -not $obj.portPresent -or -not $obj.portConcreteBinding -or -not $obj.productionExcluded -or -not $obj.endpointApproved) {
        Fail "Concrete approved Demo endpoint binding evidence regressed."
    }
}

if ($endpoint.rawHostSerialized -or $endpoint.rawPortSerialized -or $endpoint.placeholderHostUsed) {
    Fail "Raw endpoint serialized or placeholder host used."
}

if ($summary.attemptCount -ne 1 -or $trace.attemptCount -ne 1 -or $gate.attemptCount -ne 1 -or -not $summary.externalActivationAttempted -or -not $boundary.externalActivationAttempted -or -not $gate.externalActivationAttempted) {
    Fail "R71 must record exactly one external activation attempt."
}

if (-not $trace.callOnceInvoked -or -not $trace.invokeOnceInvoked -or -not $trace.executeOnceInvoked -or -not $trace.executorExecutionStarted -or -not $trace.executorValidationPassed) {
    Fail "R71 did not invoke the approved CallOnce/InvokeOnce/ExecuteOnce chain."
}

if (-not $socket.configuredSocketConnectorUsed -or -not $socket.connectReached -or $socket.socketClientExecutionDependencyMissing -or $socket.socketConnectorNotConfigured -or -not $socket.socketOpened) {
    Fail "Configured socket connector evidence is invalid."
}

if (-not $summary.tcpSocketSucceeded -or $boundary.boundaryStatuses.tcpSocket -ne "Succeeded" -or -not $trace.tcpConnectionAttempted -or -not $trace.realSocketOpened) {
    Fail "R71 did not prove TCP/socket success."
}

if ($summary.tlsAttempted -or $tls.tlsAttempted -or $tls.tlsHandshakeAttempted -or $trace.tlsHandshakeAttempted) {
    Fail "R71 artifact says TLS was attempted, but classification expects no TLS attempt after TCP success."
}

if ($tls.tlsBoundaryResultCategory -ne "NotAttemptedAfterTcpSuccess" -or -not $tls.tcpSocketSucceededBeforeTls) {
    Fail "TLS boundary inconclusive category is missing."
}

if ($summary.fixLogonSessionAttempted -or $summary.marketDataRequestAttempted -or $trace.fixLogonAttempted -or $trace.marketDataRequestSent) {
    Fail "R71 unexpectedly attempted FIX or MarketDataRequest."
}

if ($boundary.boundaryStatuses.credentialConfig -ne "ValidationOnly" -or $boundary.boundaryStatuses.tcpSocket -ne "Succeeded" -or $boundary.boundaryStatuses.tls -ne "NotAttempted" -or $boundary.boundaryStatuses.fixLogonSession -ne "NotAttempted" -or $boundary.boundaryStatuses.marketDataRequest -ne "NotAttempted" -or $boundary.boundaryStatuses.marketDataResponseEntries -ne "NotAttempted") {
    Fail "R71 boundary status evidence is invalid."
}

if ($boundary.boundaryStatuses.shutdownRevert -ne "Succeeded" -or -not $shutdown.shutdownRevertCompleted) {
    Fail "Shutdown/revert evidence is missing or incomplete."
}

if ($summary.credentialValuesReturned -or $boundary.credentialValuesReturned -or $marketData.credentialValuesReturned -or $gate.credentialValuesReturned) {
    Fail "credentialValuesReturned must remain false."
}

if ($forbidden.result -ne "PASS") {
    Fail "Forbidden-action audit failed."
}

foreach ($flag in @("ordersSubmitted", "newOrderSingleSent", "cancelReplaceSent", "tradingEnablementExecuted", "tradingStateMutated", "productionAccountUsed", "productionAccountAllowed", "apiStarted", "workerStarted", "hostedServiceStarted", "backgroundServiceStarted", "schedulerStarted", "pollingLoopStarted", "replayExecuted", "shadowReplayExecuted", "shadowReplaySubmitted", "nonApprovedInstrumentTouched", "credentialValuesRead", "credentialValuesReturned", "credentialEndpointFixSensitiveValuesPrintedStoredSerialized", "rawSensitiveFixLogsStored", "rawEndpointValuesSerialized", "defaultGatewayRegistrationChanged", "runtimeEnablementPersisted", "defaultGlobalRealAdapterEnablement", "noExternalBoundaryDefaultRemoved")) {
    if ($forbidden.$flag) {
        Fail "Forbidden action flag is true: $flag"
    }
}

if ($apiWorker.result -ne "PASS" -or $apiWorker.apiWorkerGatewayMode -ne "FakeLmaxGatewayOnly" -or $apiWorker.manualExecutionSurfaceRegisteredInApi -or $apiWorker.manualExecutionSurfaceRegisteredInWorker -or $apiWorker.realBoundedAdapterRegisteredInApi -or $apiWorker.realBoundedAdapterRegisteredInWorker -or $apiWorker.demoEndpointBindingRegisteredInApi -or $apiWorker.demoEndpointBindingRegisteredInWorker -or $apiWorker.socketConnectorRegisteredInApi -or $apiWorker.socketConnectorRegisteredInWorker -or $apiWorker.tlsConnectorRegisteredInApi -or $apiWorker.tlsConnectorRegisteredInWorker -or $apiWorker.appsettingsLiveEnablementIntroduced -or $apiWorker.defaultRuntimeGatewayChanged) {
    Fail "API/Worker FakeLmaxGatewayOnly audit failed."
}

if (-not $usdJpy.caveatPreserved -or $usdJpy.securityId -ne "4004" -or $usdJpy.securityIdSource -ne "8" -or $usdJpy.caveat -ne "prior failed-safe root cause remains unproven") {
    Fail "USDJPY caveat evidence is missing or weakened."
}

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r71-*" |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$sensitivePatterns = @(
    "fix-marketdata\.london-demo\.lmax\.com",
    "fix-order\.london-demo\.lmax\.com",
    "password\s*[:=]",
    "passwd\s*[:=]",
    "token\s*[:=]",
    "554=",
    "-----BEGIN"
)
foreach ($pattern in $sensitivePatterns) {
    if ($artifactText -match $pattern) {
        Fail "Potential raw endpoint, credential, or sensitive FIX material found in R71 artifacts: $pattern"
    }
}

if ($gate.buildResult.status -ne "PASS" -or $gate.testResult.status -ne "PASS") {
    Fail "Build/test evidence is missing or not PASS."
}

Write-Host "LMAX R71 temporary read-only activation retry gate validation PASS"
