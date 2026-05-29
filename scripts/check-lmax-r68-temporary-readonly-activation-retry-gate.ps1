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
    "phase-lmax-r68-temporary-readonly-activation-retry-report.md",
    "phase-lmax-r68-temporary-readonly-activation-retry-summary.json",
    "phase-lmax-r68-operator-approval-note.md",
    "phase-lmax-r68-preflight-result.json",
    "phase-lmax-r68-demo-endpoint-binding-evidence.json",
    "phase-lmax-r68-socket-connector-evidence.json",
    "phase-lmax-r68-operational-invocation-trace.json",
    "phase-lmax-r68-boundary-evidence.json",
    "phase-lmax-r68-marketdata-sanitized-result.json",
    "phase-lmax-r68-forbidden-actions-audit.json",
    "phase-lmax-r68-api-worker-fake-gateway-audit.json",
    "phase-lmax-r68-usdjpy-caveat-preservation.json",
    "phase-lmax-r68-shutdown-revert-evidence.json",
    "phase-lmax-r68-next-phase-recommendation.json",
    "phase-lmax-r68-gate-validation.json"
)

foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $name))) {
        Fail "Missing required R68 artifact: $name"
    }
}

$allowed = @(
    "LMAX_R68_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED",
    "LMAX_R68_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED",
    "LMAX_R68_FAIL_R67_SUCCESS_EVIDENCE_MISSING",
    "LMAX_R68_FAIL_DEMO_ENDPOINT_BINDING_REGRESSION",
    "LMAX_R68_FAIL_PLACEHOLDER_HOST_USED",
    "LMAX_R68_FAIL_SOCKET_CONNECTOR_BINDING_REGRESSION",
    "LMAX_R68_FAIL_MANUAL_EXECUTION_SURFACE_REGRESSION",
    "LMAX_R68_FAIL_REAL_BOUNDED_ADAPTER_BINDING_REGRESSION",
    "LMAX_R68_FAIL_NO_EXTERNAL_ADAPTER_USED_INSTEAD_OF_REAL_BOUNDARY_ADAPTER",
    "LMAX_R68_FAIL_OPERATIONAL_CALLER_REGRESSION",
    "LMAX_R68_FAIL_INVOCATION_PATH_REGRESSION",
    "LMAX_R68_FAIL_EXECUTE_ONCE_NOT_INVOKED",
    "LMAX_R68_FAIL_PRE_EXTERNAL_APPROVAL_OR_COMPOSITION_REGRESSION",
    "LMAX_R68_FAIL_CREDENTIAL_CONFIG_MISSING",
    "LMAX_R68_FAIL_CREDENTIAL_CONFIG_NOT_APPROVED",
    "LMAX_R68_FAIL_CREDENTIAL_CONFIG_BINDING_REGRESSION",
    "LMAX_R68_FAIL_PRODUCTION_ACCOUNT_RISK",
    "LMAX_R68_FAIL_API_WORKER_GATEWAY_REGRESSION",
    "LMAX_R68_FAIL_LIVE_LAUNCHER_OR_BACKGROUND_SERVICE_RISK",
    "LMAX_R68_FAIL_FORBIDDEN_ACTION_RISK",
    "LMAX_R68_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R68_FAIL_PREFLIGHT_ABORTED",
    "LMAX_R68_FAIL_TCP_SOCKET_BOUNDARY",
    "LMAX_R68_FAIL_TLS_BOUNDARY",
    "LMAX_R68_FAIL_FIX_LOGON_OR_SESSION_BOUNDARY",
    "LMAX_R68_FAIL_MARKETDATA_REQUEST_BOUNDARY",
    "LMAX_R68_FAIL_MARKETDATA_RESPONSE_BOUNDARY",
    "LMAX_R68_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE",
    "LMAX_R68_FAIL_SHUTDOWN_REVERT_INCOMPLETE",
    "LMAX_R68_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK",
    "LMAX_R68_FAIL_BUILD_OR_TESTS"
)

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r68-temporary-readonly-activation-retry-summary.json")
$preflight = Read-Json (Join-Path $artifactRoot "phase-lmax-r68-preflight-result.json")
$endpoint = Read-Json (Join-Path $artifactRoot "phase-lmax-r68-demo-endpoint-binding-evidence.json")
$socket = Read-Json (Join-Path $artifactRoot "phase-lmax-r68-socket-connector-evidence.json")
$trace = Read-Json (Join-Path $artifactRoot "phase-lmax-r68-operational-invocation-trace.json")
$boundary = Read-Json (Join-Path $artifactRoot "phase-lmax-r68-boundary-evidence.json")
$marketData = Read-Json (Join-Path $artifactRoot "phase-lmax-r68-marketdata-sanitized-result.json")
$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r68-forbidden-actions-audit.json")
$apiWorker = Read-Json (Join-Path $artifactRoot "phase-lmax-r68-api-worker-fake-gateway-audit.json")
$usdJpy = Read-Json (Join-Path $artifactRoot "phase-lmax-r68-usdjpy-caveat-preservation.json")
$shutdown = Read-Json (Join-Path $artifactRoot "phase-lmax-r68-shutdown-revert-evidence.json")
$gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r68-gate-validation.json")
$r67Gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r67-gate-validation.json")
$approvalText = Get-Content -LiteralPath (Join-Path $artifactRoot "phase-lmax-r68-operator-approval-note.md") -Raw

if ($allowed -notcontains $summary.classification -or $allowed -notcontains $gate.classification) {
    Fail "R68 classification is absent or not allowed."
}

if ($summary.classification -ne "LMAX_R68_FAIL_PRE_EXTERNAL_APPROVAL_OR_COMPOSITION_REGRESSION" -or $gate.classification -ne $summary.classification) {
    Fail "Unexpected R68 classification: $($summary.classification)"
}

if ($r67Gate.classification -ne "LMAX_R67_PASS_DEMO_ENDPOINT_CONFIG_BINDING_READY_NO_EXTERNAL_ACTIVATION" -or $r67Gate.validatorResult -ne "PASS") {
    Fail "R67 success evidence is missing or not passing."
}

$expectedApproval = "I, Philippe, explicitly approve Phase LMAX-R68 for one temporary Demo read-only runtime market-data activation retry after the R67 Demo endpoint config binding fix for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority."
if ($approvalText.Trim() -ne $expectedApproval -or -not $preflight.operatorApprovalExactTextPresent) {
    Fail "Exact R68 operator approval text missing or mismatched."
}

if ($summary.concreteBlocker -ne "UnexpectedApprovedRetryPhase" -or $preflight.failureCode -ne "UnexpectedApprovedRetryPhase" -or $trace.preflightIssue.code -ne "UnexpectedApprovedRetryPhase") {
    Fail "R68 pre-external blocker trace is missing."
}

if ($preflight.actualValue -notmatch "LMAX-R68 rejected" -or $trace.preflightIssue.class -ne "LmaxReadOnlyActivationManualExecutionSurface" -or $trace.preflightIssue.method -ne "ValidateCommand") {
    Fail "R68 full preflight trace does not name the gate/class/method/actual value."
}

if (-not $summary.manualExecutionSurfaceUsed -or -not $summary.adapterModeRealBoundedExecutableReadOnlyUsed -or -not $trace.manualCliSurfaceUsed -or $trace.adapterMode -ne "real-bounded-executable-readonly") {
    Fail "Manual CLI surface or explicit real-bounded adapter mode was not used."
}

foreach ($obj in @($summary, $endpoint)) {
    if ($obj.endpointMode -ne "Demo" -or -not $obj.endpointPresent -or -not $obj.hostPresent -or -not $obj.hostConcreteBinding -or $obj.hostWasPlaceholder -or -not $obj.portPresent -or -not $obj.portConcreteBinding -or -not $obj.productionExcluded -or -not $obj.endpointApproved) {
        Fail "Concrete approved Demo endpoint binding evidence regressed."
    }
}

if ($endpoint.rawHostSerialized -or $endpoint.rawPortSerialized) {
    Fail "Raw endpoint value serialized in R68 endpoint evidence."
}

if ($summary.attemptCount -ne 0 -or $trace.attemptCount -ne 0 -or $gate.attemptCount -ne 0 -or $summary.externalActivationAttempted -or $boundary.externalActivationAttempted -or $gate.externalActivationAttempted) {
    Fail "R68 must record safe pre-external abort with attemptCount 0."
}

if ($summary.lmaxManualBoundedReadOnlyActivationCallerCallOnceInvoked -or $summary.lmaxBoundedReadOnlyActivationInvocationPathInvokeOnceInvoked -or $summary.lmaxTemporaryReadOnlyActivationExecutorExecuteOnceInvoked -or $trace.callOnceInvoked -or $trace.invokeOnceInvoked -or $trace.executeOnceInvoked) {
    Fail "R68 unexpectedly invoked CallOnce/InvokeOnce/ExecuteOnce after failed preflight."
}

if ($summary.configuredSocketConnectorUsed -or $summary.lmaxReadOnlyActivationManualTcpSocketConnectorConnectReached -or $socket.configuredSocketConnectorUsed -or $socket.connectReached -or $socket.socketOpened) {
    Fail "R68 unexpectedly reached the socket connector."
}

foreach ($key in @("credentialConfig", "tcpSocket", "tls", "fixLogonSession", "marketDataRequest", "marketDataResponseEntries")) {
    if ($boundary.boundaryStatuses.$key -ne "NotAttempted" -or $summary.boundaryStatuses.$key -ne "NotAttempted" -or $gate.boundaryStatuses.$key -ne "NotAttempted") {
        Fail "Boundary $key must be NotAttempted for R68 safe pre-external abort."
    }
}

if ($boundary.boundaryStatuses.shutdownRevert -ne "NotRequired" -or $shutdown.shutdownRevertStatus -ne "NotRequired" -or $shutdown.resourcesOpened) {
    Fail "Shutdown/revert evidence is inconsistent with no resources opened."
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

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r68-*" |
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
        Fail "Potential raw endpoint, credential, or sensitive FIX material found in R68 artifacts: $pattern"
    }
}

if ($gate.buildResult.status -ne "PASS" -or $gate.testResult.status -ne "PASS") {
    Fail "Build/test evidence is missing or not PASS."
}

Write-Host "LMAX R68 temporary read-only activation retry gate validation PASS"
