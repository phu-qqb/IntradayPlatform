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
    "phase-lmax-r69-temporary-readonly-activation-retry-report.md",
    "phase-lmax-r69-temporary-readonly-activation-retry-summary.json",
    "phase-lmax-r69-operator-approval-note.md",
    "phase-lmax-r69-preflight-result.json",
    "phase-lmax-r69-demo-endpoint-binding-evidence.json",
    "phase-lmax-r69-socket-connector-evidence.json",
    "phase-lmax-r69-operational-invocation-trace.json",
    "phase-lmax-r69-boundary-evidence.json",
    "phase-lmax-r69-marketdata-sanitized-result.json",
    "phase-lmax-r69-forbidden-actions-audit.json",
    "phase-lmax-r69-api-worker-fake-gateway-audit.json",
    "phase-lmax-r69-usdjpy-caveat-preservation.json",
    "phase-lmax-r69-shutdown-revert-evidence.json",
    "phase-lmax-r69-next-phase-recommendation.json",
    "phase-lmax-r69-gate-validation.json"
)

foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $name))) {
        Fail "Missing required R69 artifact: $name"
    }
}

$allowed = @(
    "LMAX_R69_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED",
    "LMAX_R69_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED",
    "LMAX_R69_FAIL_DEMO_ENDPOINT_BINDING_REGRESSION",
    "LMAX_R69_FAIL_PLACEHOLDER_HOST_USED",
    "LMAX_R69_FAIL_SOCKET_CONNECTOR_BINDING_REGRESSION",
    "LMAX_R69_FAIL_MANUAL_EXECUTION_SURFACE_REGRESSION",
    "LMAX_R69_FAIL_REAL_BOUNDED_ADAPTER_BINDING_REGRESSION",
    "LMAX_R69_FAIL_NO_EXTERNAL_ADAPTER_USED_INSTEAD_OF_REAL_BOUNDARY_ADAPTER",
    "LMAX_R69_FAIL_EXECUTE_ONCE_NOT_INVOKED",
    "LMAX_R69_FAIL_PRE_EXTERNAL_APPROVAL_OR_COMPOSITION_REGRESSION",
    "LMAX_R69_FAIL_PRODUCTION_ACCOUNT_RISK",
    "LMAX_R69_FAIL_API_WORKER_GATEWAY_REGRESSION",
    "LMAX_R69_FAIL_FORBIDDEN_ACTION_RISK",
    "LMAX_R69_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R69_FAIL_PREFLIGHT_ABORTED",
    "LMAX_R69_FAIL_TCP_SOCKET_BOUNDARY",
    "LMAX_R69_FAIL_TLS_BOUNDARY",
    "LMAX_R69_FAIL_FIX_LOGON_OR_SESSION_BOUNDARY",
    "LMAX_R69_FAIL_MARKETDATA_REQUEST_BOUNDARY",
    "LMAX_R69_FAIL_MARKETDATA_RESPONSE_BOUNDARY",
    "LMAX_R69_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE",
    "LMAX_R69_FAIL_SHUTDOWN_REVERT_INCOMPLETE",
    "LMAX_R69_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK",
    "LMAX_R69_FAIL_BUILD_OR_TESTS"
)

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r69-temporary-readonly-activation-retry-summary.json")
$preflight = Read-Json (Join-Path $artifactRoot "phase-lmax-r69-preflight-result.json")
$endpoint = Read-Json (Join-Path $artifactRoot "phase-lmax-r69-demo-endpoint-binding-evidence.json")
$socket = Read-Json (Join-Path $artifactRoot "phase-lmax-r69-socket-connector-evidence.json")
$trace = Read-Json (Join-Path $artifactRoot "phase-lmax-r69-operational-invocation-trace.json")
$boundary = Read-Json (Join-Path $artifactRoot "phase-lmax-r69-boundary-evidence.json")
$marketData = Read-Json (Join-Path $artifactRoot "phase-lmax-r69-marketdata-sanitized-result.json")
$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r69-forbidden-actions-audit.json")
$apiWorker = Read-Json (Join-Path $artifactRoot "phase-lmax-r69-api-worker-fake-gateway-audit.json")
$usdJpy = Read-Json (Join-Path $artifactRoot "phase-lmax-r69-usdjpy-caveat-preservation.json")
$shutdown = Read-Json (Join-Path $artifactRoot "phase-lmax-r69-shutdown-revert-evidence.json")
$gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r69-gate-validation.json")
$r67Gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r67-gate-validation.json")
$approvalText = Get-Content -LiteralPath (Join-Path $artifactRoot "phase-lmax-r69-operator-approval-note.md") -Raw

if ($allowed -notcontains $summary.classification -or $allowed -notcontains $gate.classification) {
    Fail "R69 classification is absent or not allowed."
}

if ($summary.classification -ne "LMAX_R69_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED" -or $gate.classification -ne $summary.classification) {
    Fail "Unexpected R69 classification: $($summary.classification)"
}

if ($r67Gate.classification -ne "LMAX_R67_PASS_DEMO_ENDPOINT_CONFIG_BINDING_READY_NO_EXTERNAL_ACTIVATION" -or $r67Gate.validatorResult -ne "PASS") {
    Fail "R67 success evidence is missing or not passing."
}

$expectedApproval = "I, Philippe, explicitly approve Phase LMAX-R69 for one temporary Demo read-only runtime market-data activation retry after the R67 Demo endpoint config binding fix for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority."
if ($approvalText.Trim() -ne $expectedApproval -or -not $preflight.operatorApprovalExactTextPresent) {
    Fail "Exact R69 operator approval text missing or mismatched."
}

if (-not $summary.retryPhaseReservationPassed -or -not $preflight.retryPhaseReservationPassed -or -not $gate.retryPhaseReservationPassed) {
    Fail "LMAX-R69 did not pass retry phase reservation."
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
    Fail "R69 must record exactly one external activation attempt."
}

if (-not $trace.callOnceInvoked -or -not $trace.invokeOnceInvoked -or -not $trace.executeOnceInvoked -or -not $trace.executorExecutionStarted -or -not $trace.executorValidationPassed) {
    Fail "R69 did not invoke the approved CallOnce/InvokeOnce/ExecuteOnce chain."
}

if (-not $socket.configuredSocketConnectorUsed -or -not $socket.connectReached -or $socket.socketClientExecutionDependencyMissing -or $socket.socketConnectorNotConfigured -or -not $socket.socketOpened) {
    Fail "Configured socket connector evidence is invalid."
}

if (-not $trace.tcpConnectionAttempted -or -not $trace.realSocketOpened) {
    Fail "R69 did not reach a real TCP/socket boundary."
}

if ($boundary.boundaryStatuses.credentialConfig -ne "ValidationOnly" -or $boundary.boundaryStatuses.tcpSocket -ne "Succeeded" -or $gate.boundaryStatuses.tcpSocket -ne "Succeeded") {
    Fail "R69 boundary statuses do not show Credential/config ValidationOnly and TCP/socket Succeeded."
}

foreach ($key in @("tls", "fixLogonSession", "marketDataRequest", "marketDataResponseEntries")) {
    if ($boundary.boundaryStatuses.$key -ne "NotAttempted" -or $summary.boundaryStatuses.$key -ne "NotAttempted" -or $gate.boundaryStatuses.$key -ne "NotAttempted") {
        Fail "Boundary $key must be NotAttempted for R69."
    }
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

foreach ($flag in @("ordersSubmitted", "newOrderSingleSent", "cancelReplaceSent", "tradingEnablementExecuted", "tradingStateMutated", "productionAccountUsed", "productionAccountAllowed", "apiStarted", "workerStarted", "hostedServiceStarted", "backgroundServiceStarted", "schedulerStarted", "pollingLoopStarted", "replayExecuted", "shadowReplayExecuted", "shadowReplaySubmitted", "nonApprovedInstrumentTouched", "credentialValuesRead", "credentialValuesReturned", "credentialEndpointValuesPrintedStoredSerialized", "rawSensitiveFixLogsStored", "rawEndpointValuesSerialized", "defaultGatewayRegistrationChanged", "runtimeEnablementPersisted", "defaultGlobalRealAdapterEnablement", "noExternalBoundaryDefaultRemoved")) {
    if ($forbidden.$flag) {
        Fail "Forbidden action flag is true: $flag"
    }
}

if ($apiWorker.result -ne "PASS" -or $apiWorker.apiWorkerGatewayMode -ne "FakeLmaxGatewayOnly" -or $apiWorker.manualExecutionSurfaceRegisteredInApi -or $apiWorker.manualExecutionSurfaceRegisteredInWorker -or $apiWorker.realBoundedAdapterRegisteredInApi -or $apiWorker.realBoundedAdapterRegisteredInWorker -or $apiWorker.demoEndpointBindingRegisteredInApi -or $apiWorker.demoEndpointBindingRegisteredInWorker -or $apiWorker.socketConnectorRegisteredInApi -or $apiWorker.socketConnectorRegisteredInWorker -or $apiWorker.appsettingsLiveEnablementIntroduced -or $apiWorker.defaultRuntimeGatewayChanged) {
    Fail "API/Worker FakeLmaxGatewayOnly audit failed."
}

$apiProgram = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/Program.cs") -Raw
$workerProgram = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Worker/Program.cs") -Raw
$appsettings = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/appsettings.json") -Raw
foreach ($text in @($apiProgram, $workerProgram, $appsettings)) {
    if ($text.Contains("QQ.Production.Intraday.Tools.LmaxReadOnlyActivation") -or $text.Contains("real-bounded-executable-readonly") -or $text.Contains("LmaxReadOnlyActivationManualTcpSocketConnector") -or $text.Contains("LmaxReadOnlyActivationManualDemoEndpointBinding")) {
        Fail "Manual CLI, real adapter mode, socket connector, or endpoint binding is wired into API/Worker/default startup."
    }
}

if (-not $apiProgram.Contains("FakeLmaxGateway")) {
    Fail "API FakeLmaxGateway evidence missing."
}

if (-not $usdJpy.caveatPreserved -or $usdJpy.securityId -ne "4004" -or $usdJpy.securityIdSource -ne "8" -or $usdJpy.caveat -ne "prior failed-safe root cause remains unproven") {
    Fail "USDJPY caveat evidence is missing or weakened."
}

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r69-*" |
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
        Fail "Potential raw endpoint, credential, or sensitive FIX material found in R69 artifacts: $pattern"
    }
}

if ($gate.buildResult.status -ne "PASS" -or $gate.testResult.status -ne "PASS") {
    Fail "Build/test evidence is missing or not PASS."
}

Write-Host "LMAX R69 temporary read-only activation retry gate validation PASS"
