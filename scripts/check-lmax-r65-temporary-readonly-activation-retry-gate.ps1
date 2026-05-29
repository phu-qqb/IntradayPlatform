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
    "phase-lmax-r65-temporary-readonly-activation-retry-report.md",
    "phase-lmax-r65-temporary-readonly-activation-retry-summary.json",
    "phase-lmax-r65-operator-approval-note.md",
    "phase-lmax-r65-preflight-result.json",
    "phase-lmax-r65-manual-execution-surface-evidence.json",
    "phase-lmax-r65-real-bounded-adapter-binding-evidence.json",
    "phase-lmax-r65-socket-connector-evidence.json",
    "phase-lmax-r65-operational-invocation-trace.json",
    "phase-lmax-r65-boundary-evidence.json",
    "phase-lmax-r65-marketdata-sanitized-result.json",
    "phase-lmax-r65-forbidden-actions-audit.json",
    "phase-lmax-r65-api-worker-fake-gateway-audit.json",
    "phase-lmax-r65-usdjpy-caveat-preservation.json",
    "phase-lmax-r65-shutdown-revert-evidence.json",
    "phase-lmax-r65-next-phase-recommendation.json",
    "phase-lmax-r65-gate-validation.json"
)

foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $name))) {
        Fail "Missing required R65 artifact: $name"
    }
}

$allowed = @(
    "LMAX_R65_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED",
    "LMAX_R65_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED",
    "LMAX_R65_FAIL_R64_SUCCESS_EVIDENCE_MISSING",
    "LMAX_R65_FAIL_MANUAL_EXECUTION_SURFACE_REGRESSION",
    "LMAX_R65_FAIL_REAL_BOUNDED_ADAPTER_BINDING_REGRESSION",
    "LMAX_R65_FAIL_SOCKET_CONNECTOR_BINDING_REGRESSION",
    "LMAX_R65_FAIL_NO_EXTERNAL_ADAPTER_USED_INSTEAD_OF_REAL_BOUNDARY_ADAPTER",
    "LMAX_R65_FAIL_OPERATIONAL_CALLER_REGRESSION",
    "LMAX_R65_FAIL_INVOCATION_PATH_REGRESSION",
    "LMAX_R65_FAIL_EXECUTE_ONCE_NOT_INVOKED",
    "LMAX_R65_FAIL_PRE_EXTERNAL_APPROVAL_OR_COMPOSITION_REGRESSION",
    "LMAX_R65_FAIL_CREDENTIAL_CONFIG_MISSING",
    "LMAX_R65_FAIL_CREDENTIAL_CONFIG_NOT_APPROVED",
    "LMAX_R65_FAIL_CREDENTIAL_CONFIG_BINDING_REGRESSION",
    "LMAX_R65_FAIL_PRODUCTION_ACCOUNT_RISK",
    "LMAX_R65_FAIL_API_WORKER_GATEWAY_REGRESSION",
    "LMAX_R65_FAIL_LIVE_LAUNCHER_OR_BACKGROUND_SERVICE_RISK",
    "LMAX_R65_FAIL_FORBIDDEN_ACTION_RISK",
    "LMAX_R65_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R65_FAIL_PREFLIGHT_ABORTED",
    "LMAX_R65_FAIL_CREDENTIAL_CONFIG_BOUNDARY",
    "LMAX_R65_FAIL_TCP_SOCKET_BOUNDARY",
    "LMAX_R65_FAIL_TLS_BOUNDARY",
    "LMAX_R65_FAIL_FIX_LOGON_OR_SESSION_BOUNDARY",
    "LMAX_R65_FAIL_MARKETDATA_REQUEST_BOUNDARY",
    "LMAX_R65_FAIL_MARKETDATA_RESPONSE_BOUNDARY",
    "LMAX_R65_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE",
    "LMAX_R65_FAIL_SHUTDOWN_REVERT_INCOMPLETE",
    "LMAX_R65_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK",
    "LMAX_R65_FAIL_BUILD_OR_TESTS"
)

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r65-temporary-readonly-activation-retry-summary.json")
$preflight = Read-Json (Join-Path $artifactRoot "phase-lmax-r65-preflight-result.json")
$surface = Read-Json (Join-Path $artifactRoot "phase-lmax-r65-manual-execution-surface-evidence.json")
$adapter = Read-Json (Join-Path $artifactRoot "phase-lmax-r65-real-bounded-adapter-binding-evidence.json")
$socket = Read-Json (Join-Path $artifactRoot "phase-lmax-r65-socket-connector-evidence.json")
$trace = Read-Json (Join-Path $artifactRoot "phase-lmax-r65-operational-invocation-trace.json")
$boundary = Read-Json (Join-Path $artifactRoot "phase-lmax-r65-boundary-evidence.json")
$marketData = Read-Json (Join-Path $artifactRoot "phase-lmax-r65-marketdata-sanitized-result.json")
$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r65-forbidden-actions-audit.json")
$apiWorker = Read-Json (Join-Path $artifactRoot "phase-lmax-r65-api-worker-fake-gateway-audit.json")
$usdJpy = Read-Json (Join-Path $artifactRoot "phase-lmax-r65-usdjpy-caveat-preservation.json")
$shutdown = Read-Json (Join-Path $artifactRoot "phase-lmax-r65-shutdown-revert-evidence.json")
$gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r65-gate-validation.json")
$r64 = Read-Json (Join-Path $artifactRoot "phase-lmax-r64-gate-validation.json")
$approvalText = Get-Content -LiteralPath (Join-Path $artifactRoot "phase-lmax-r65-operator-approval-note.md") -Raw

if ($allowed -notcontains $summary.classification -or $allowed -notcontains $gate.classification) {
    Fail "R65 classification is absent or not allowed."
}

if ($summary.classification -ne "LMAX_R65_FAIL_TCP_SOCKET_BOUNDARY" -or $gate.classification -ne $summary.classification) {
    Fail "Unexpected R65 classification: $($summary.classification)"
}

if ($r64.classification -ne "LMAX_R64_PASS_TCP_SOCKET_CONNECTOR_BINDING_READY_NO_EXTERNAL_ACTIVATION" -or $r64.validatorResult -ne "PASS") {
    Fail "R64 success evidence is missing or not passing."
}

$expectedApproval = "I, Philippe, explicitly approve Phase LMAX-R65 for one temporary Demo read-only runtime market-data activation retry after the R64 TCP socket connector binding fix for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority."
if ($approvalText.Trim() -ne $expectedApproval) {
    Fail "Exact R65 operator approval text missing or mismatched."
}

foreach ($flag in @("manualExecutionSurfaceUsed", "adapterModeRealBoundedExecutableReadOnlyUsed", "noExternalAdapterAvoided", "lmaxConcreteTemporaryReadOnlyRuntimeActivationAdapterUsed", "lmaxRealReadOnlyMarketDataTransportUsed", "configuredSocketConnectorUsed", "lmaxReadOnlyActivationManualTcpSocketConnectorConnectReached", "lmaxManualBoundedReadOnlyActivationCallerCallOnceInvoked", "lmaxBoundedReadOnlyActivationInvocationPathInvokeOnceInvoked", "lmaxTemporaryReadOnlyActivationExecutorExecuteOnceInvoked")) {
    if (-not $summary.$flag) {
        Fail "Summary invocation/adapter/socket flag is false: $flag"
    }
}

if ($summary.attemptCount -ne 1 -or $surface.attemptCount -ne 1 -or $trace.attemptCount -ne 1 -or $gate.attemptCount -ne 1) {
    Fail "R65 must record exactly one bounded attempt."
}

if (-not $summary.externalActivationAttempted -or -not $boundary.externalActivationAttempted -or -not $gate.externalActivationAttempted) {
    Fail "R65 must record that an external TCP boundary was attempted."
}

if ($adapter.adapterMode -ne "real-bounded-executable-readonly" -or -not $adapter.realAdapterBindingUsed -or $adapter.noExternalAdapterUsed -or -not $adapter.noExternalAdapterAvoided) {
    Fail "Real bounded adapter binding evidence is invalid."
}

if ($socket.socketClientExecutionDependencyMissing -ne $false -or $socket.socketConnectorNotConfigured -ne $false -or -not $socket.configuredSocketConnectorUsed -or -not $socket.connectorReached) {
    Fail "Socket connector binding evidence regressed."
}

if ($socket.socketConnectorDefaultGlobal -or -not $socket.noExternalDefaultPreserved) {
    Fail "Socket connector default/global safety regressed."
}

foreach ($flag in @("toolUsed", "callOnceInvoked", "invokeOnceInvoked", "executeOnceInvoked")) {
    if (-not $surface.$flag) {
        Fail "Manual execution surface evidence flag is false: $flag"
    }
}

if (-not $trace.executorExecutionStarted -or -not $trace.executorValidationPassed -or -not $trace.shutdownRevertCompleted) {
    Fail "Operational invocation trace does not prove executor start/validation/shutdown."
}

if ($preflight.result -ne "PASS" -or $preflight.failureBoundary -ne "TCP/socket") {
    Fail "Preflight did not pass to TCP/socket boundary."
}

if ($boundary.boundaryStatuses.credentialConfig -ne "ValidationOnly" -or $boundary.boundaryStatuses.tcpSocket -ne "FailedExternal") {
    Fail "R65 must show credential/config validation-only and TCP/socket FailedExternal."
}

foreach ($key in @("tls", "fixLogonSession", "marketDataRequest", "marketDataResponseEntries")) {
    if ($boundary.boundaryStatuses.$key -ne "NotAttempted" -or $summary.boundaryStatuses.$key -ne "NotAttempted" -or $gate.boundaryStatuses.$key -ne "NotAttempted") {
        Fail "Boundary $key must be NotAttempted after TCP/socket failure."
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

foreach ($flag in @("ordersSubmitted", "newOrderSingleSent", "cancelReplaceSent", "tradingEnablementExecuted", "tradingStateMutated", "productionAccountUsed", "productionAccountAllowed", "apiStarted", "workerStarted", "hostedServiceStarted", "backgroundServiceStarted", "schedulerStarted", "pollingLoopStarted", "replayExecuted", "shadowReplayExecuted", "shadowReplaySubmitted", "nonApprovedInstrumentTouched", "credentialValuesRead", "credentialValuesReturned", "credentialValuesPrintedStoredSerialized", "rawSensitiveFixLogsStored", "defaultGatewayRegistrationChanged", "runtimeEnablementPersisted", "defaultGlobalRealAdapterEnablement", "noExternalBoundaryDefaultRemoved")) {
    if ($forbidden.$flag) {
        Fail "Forbidden action flag is true: $flag"
    }
}

if ($apiWorker.result -ne "PASS" -or $apiWorker.apiWorkerGatewayMode -ne "FakeLmaxGatewayOnly" -or $apiWorker.manualExecutionSurfaceRegisteredInApi -or $apiWorker.manualExecutionSurfaceRegisteredInWorker -or $apiWorker.realBoundedAdapterRegisteredInApi -or $apiWorker.realBoundedAdapterRegisteredInWorker -or $apiWorker.socketConnectorRegisteredInApi -or $apiWorker.socketConnectorRegisteredInWorker -or $apiWorker.appsettingsLiveEnablementIntroduced -or $apiWorker.defaultRuntimeGatewayChanged) {
    Fail "API/Worker FakeLmaxGatewayOnly audit failed."
}

$apiProgram = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/Program.cs") -Raw
$workerProgram = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Worker/Program.cs") -Raw
$appsettings = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/appsettings.json") -Raw
foreach ($text in @($apiProgram, $workerProgram, $appsettings)) {
    if ($text.Contains("QQ.Production.Intraday.Tools.LmaxReadOnlyActivation") -or $text.Contains("real-bounded-executable-readonly") -or $text.Contains("LmaxReadOnlyActivationManualTcpSocketConnector")) {
        Fail "Manual CLI, real adapter mode, or socket connector is wired into API/Worker/default startup."
    }
}

if (-not $apiProgram.Contains("FakeLmaxGateway")) {
    Fail "API FakeLmaxGateway evidence missing."
}

if (-not $usdJpy.caveatPreserved -or $usdJpy.securityId -ne "4004" -or $usdJpy.securityIdSource -ne "8" -or $usdJpy.caveat -ne "prior failed-safe root cause remains unproven") {
    Fail "USDJPY caveat evidence is missing or weakened."
}

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r65-*" |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$sensitivePatterns = @(
    "password\s*[:=]",
    "passwd\s*[:=]",
    "token\s*[:=]",
    "secret\s*[:=]",
    "554=",
    "-----BEGIN"
)
foreach ($pattern in $sensitivePatterns) {
    if ($artifactText -match $pattern) {
        Fail "Potential raw credential or sensitive FIX material found in R65 artifacts: $pattern"
    }
}

if ($gate.buildResult.status -ne "PASS" -or $gate.testResult.status -ne "PASS") {
    Fail "Build/test evidence is missing or not PASS."
}

Write-Host "LMAX R65 temporary read-only activation retry gate validation PASS"
