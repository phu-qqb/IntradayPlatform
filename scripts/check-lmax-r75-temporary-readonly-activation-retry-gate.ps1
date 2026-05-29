param(
    [string]$ArtifactRoot = "artifacts/readiness/lmax-runtime-enablement"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Error "LMAX-R75 validation failed: $Message"
    exit 1
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing artifact: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

$allowedClassifications = @(
    "LMAX_R75_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED",
    "LMAX_R75_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED",
    "LMAX_R75_FAIL_R73_SUCCESS_EVIDENCE_MISSING",
    "LMAX_R75_FAIL_DEMO_ENDPOINT_BINDING_REGRESSION",
    "LMAX_R75_FAIL_PLACEHOLDER_HOST_USED",
    "LMAX_R75_FAIL_SOCKET_CONNECTOR_BINDING_REGRESSION",
    "LMAX_R75_FAIL_TLS_PROGRESSION_BINDING_REGRESSION",
    "LMAX_R75_FAIL_MANUAL_EXECUTION_SURFACE_REGRESSION",
    "LMAX_R75_FAIL_REAL_BOUNDED_ADAPTER_BINDING_REGRESSION",
    "LMAX_R75_FAIL_NO_EXTERNAL_ADAPTER_USED_INSTEAD_OF_REAL_BOUNDARY_ADAPTER",
    "LMAX_R75_FAIL_EXECUTE_ONCE_NOT_INVOKED",
    "LMAX_R75_FAIL_PRE_EXTERNAL_APPROVAL_OR_COMPOSITION_REGRESSION",
    "LMAX_R75_FAIL_CREDENTIAL_CONFIG_MISSING",
    "LMAX_R75_FAIL_CREDENTIAL_CONFIG_NOT_APPROVED",
    "LMAX_R75_FAIL_CREDENTIAL_CONFIG_BINDING_REGRESSION",
    "LMAX_R75_FAIL_PRODUCTION_ACCOUNT_RISK",
    "LMAX_R75_FAIL_API_WORKER_GATEWAY_REGRESSION",
    "LMAX_R75_FAIL_LIVE_LAUNCHER_OR_BACKGROUND_SERVICE_RISK",
    "LMAX_R75_FAIL_FORBIDDEN_ACTION_RISK",
    "LMAX_R75_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R75_FAIL_PREFLIGHT_ABORTED",
    "LMAX_R75_FAIL_TCP_SOCKET_BOUNDARY",
    "LMAX_R75_FAIL_TLS_BOUNDARY",
    "LMAX_R75_FAIL_FIX_LOGON_OR_SESSION_BOUNDARY",
    "LMAX_R75_FAIL_MARKETDATA_REQUEST_BOUNDARY",
    "LMAX_R75_FAIL_MARKETDATA_RESPONSE_BOUNDARY",
    "LMAX_R75_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE",
    "LMAX_R75_FAIL_SHUTDOWN_REVERT_INCOMPLETE",
    "LMAX_R75_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK",
    "LMAX_R75_FAIL_BUILD_OR_TESTS"
)

$approvalPath = Join-Path $ArtifactRoot "phase-lmax-r75-operator-approval-note.md"
if (-not (Test-Path -LiteralPath $approvalPath)) { Fail "Missing operator approval note." }
$approval = (Get-Content -LiteralPath $approvalPath -Raw).Trim()
$expected = "I, Philippe, explicitly approve Phase LMAX-R75 for one temporary Demo read-only runtime market-data activation retry after the R73 TLS progression binding fix for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority."
if ($approval -ne $expected) { Fail "Operator approval is missing or mismatched." }

$summary = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r75-temporary-readonly-activation-retry-summary.json")
$gate = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r75-gate-validation.json")
$r73 = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r73-gate-validation.json")
$preflight = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r75-preflight-result.json")
$endpoint = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r75-demo-endpoint-binding-evidence.json")
$socket = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r75-socket-connector-evidence.json")
$tls = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r75-tls-boundary-evidence.json")
$trace = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r75-operational-invocation-trace.json")
$boundary = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r75-boundary-evidence.json")
$marketData = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r75-marketdata-sanitized-result.json")
$forbidden = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r75-forbidden-actions-audit.json")
$apiWorker = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r75-api-worker-fake-gateway-audit.json")
$usdJpy = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r75-usdjpy-caveat-preservation.json")
$shutdown = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r75-shutdown-revert-evidence.json")
$next = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r75-next-phase-recommendation.json")

if ($summary.phase -ne "LMAX-R75") { Fail "Summary phase mismatch." }
if ($allowedClassifications -notcontains $summary.classification) { Fail "Final classification absent or not allowed." }
if ($summary.classification -ne $gate.classification) { Fail "Gate classification mismatch." }
if ($r73.classification -ne "LMAX_R73_PASS_TLS_PROGRESSION_BINDING_READY_NO_EXTERNAL_ACTIVATION" -or $r73.tlsBindingReady -ne $true) { Fail "R73 success evidence missing." }

if ($summary.externalActivationAttempted -ne $true -or $trace.externalActivationAttempted -ne $true) { Fail "External activation was not attempted exactly as expected." }
if ($summary.attemptCount -ne 1 -or $trace.attemptCount -ne 1) { Fail "Attempt count is not exactly one." }
if ($summary.retryPhaseReservationPassed -ne $true -or $preflight.retryPhaseReservationPassed -ne $true) { Fail "LMAX-R75 did not pass retry phase reservation." }
if ($summary.manualCliSurfaceUsed -ne $true -or $trace.manualCliSurfaceUsed -ne $true) { Fail "Manual CLI surface was not used." }
if ($summary.adapterModeRealBoundedExecutableReadOnlyUsed -ne $true -or $trace.adapterModeRealBoundedExecutableReadOnlyUsed -ne $true) { Fail "Explicit real-bounded adapter mode was not used." }
if ($trace.callOnceInvoked -ne $true -or $trace.invokeOnceInvoked -ne $true -or $trace.executeOnceInvoked -ne $true) { Fail "CallOnce/InvokeOnce/ExecuteOnce invocation chain regressed." }
if ($summary.concreteDemoEndpointBindingUsed -ne $true -or $endpoint.concreteDemoEndpointBindingUsed -ne $true) { Fail "Concrete Demo endpoint binding was not used." }
if ($endpoint.endpointMode -ne "Demo" -or $endpoint.hostConcreteBinding -ne $true -or $endpoint.hostWasPlaceholder -ne $false -or $endpoint.portConcreteBinding -ne $true -or $endpoint.productionExcluded -ne $true -or $endpoint.endpointApproved -ne $true) {
    Fail "Demo endpoint binding evidence invalid."
}
if ($summary.configuredSocketConnectorUsed -ne $true -or $socket.configuredSocketConnectorUsed -ne $true -or $socket.connectReached -ne $true) { Fail "Configured socket connector evidence missing." }
if ($summary.tcpSocketSucceeded -ne $true -or $socket.tcpSocketSucceeded -ne $true -or $trace.realSocketOpened -ne $true) { Fail "TCP/socket success evidence missing." }
if ($summary.tlsAttempted -ne $true -or $tls.tlsAttempted -ne $true -or $trace.tlsHandshakeAttempted -ne $true) { Fail "TLS was not attempted after the R73 binding fix." }
if ($summary.socketConnectorAuthenticateTlsReached -ne $true -or $tls.socketConnectorAuthenticateTlsReached -ne $true) { Fail "socketConnector.AuthenticateTls was not reached." }
if ($summary.fixLogonSessionAttempted -ne $false -or $trace.fixLogonAttempted -ne $false) { Fail "FIX logon/session was unexpectedly attempted." }
if ($summary.marketDataRequestAttempted -ne $false -or $trace.marketDataRequestSent -ne $false -or $marketData.marketDataRequestAttempted -ne $false) { Fail "MarketDataRequest was unexpectedly attempted." }
if ($shutdown.shutdownRevertCompleted -ne $true -or $shutdown.shutdownRevert -ne "Succeeded") { Fail "Shutdown/revert incomplete." }

if ($boundary.boundaryStatuses.credentialConfig -ne "ValidationOnly" -or
    $boundary.boundaryStatuses.tcpSocket -ne "Succeeded" -or
    $boundary.boundaryStatuses.tls -ne "Attempted" -or
    $boundary.boundaryStatuses.fixLogonSession -ne "NotAttempted" -or
    $boundary.boundaryStatuses.marketDataRequest -ne "NotAttempted" -or
    $boundary.boundaryStatuses.marketDataResponseEntries -ne "NotAttempted" -or
    $boundary.boundaryStatuses.shutdownRevert -ne "Succeeded") {
    Fail "Boundary evidence is invalid."
}

if ($summary.credentialValuesReturned -ne $false -or $trace.credentialValuesReturned -ne $false -or $boundary.credentialValuesReturned -ne $false) { Fail "credentialValuesReturned is not false." }
if ($summary.credentialEndpointTlsFixSensitiveValuesPrintedStoredSerialized -ne $false -or $boundary.credentialEndpointTlsFixSensitiveValuesPrintedStoredSerialized -ne $false) {
    Fail "Credential/endpoint/TLS/FIX-sensitive values were printed/stored/serialized."
}
if ($forbidden.result -ne "PASS") { Fail "Forbidden actions audit failed." }
if ($apiWorker.result -ne "PASS" -or $apiWorker.apiWorkerGateway -ne "FakeLmaxGatewayOnly" -or $apiWorker.apiWorkerGatewayChanged -ne $false) { Fail "API/Worker FakeLmaxGatewayOnly audit failed." }
if ($usdJpy.result -ne "PASS" -or $usdJpy.securityId -ne "4004" -or $usdJpy.securityIdSource -ne "8" -or $usdJpy.caveatWeakened -ne $false) { Fail "USDJPY caveat missing or weakened." }
if ($gate.buildResult.status -ne "PASS" -or $gate.testResult.status -ne "PASS") { Fail "Build/test evidence missing." }
if ($next.recommendedPhase -ne "LMAX-R76") { Fail "Next phase recommendation missing or invalid." }

$factoryPath = "tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualExecutionSurfaceFactory.cs"
$connectorPath = "tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualTcpSocketConnector.cs"
$factorySource = Get-Content -LiteralPath $factoryPath -Raw
$connectorSource = Get-Content -LiteralPath $connectorPath -Raw
if ($factorySource -notmatch "socketConnector\.AuthenticateTls") { Fail "R73 TLS progression binding missing from factory." }
if ($connectorSource -notmatch "AuthenticateTls" -or $connectorSource -notmatch "SslStream") { Fail "Manual TLS continuation missing from connector." }

$r75Artifacts = Get-ChildItem -LiteralPath $ArtifactRoot -Filter "phase-lmax-r75-*" -File
$artifactText = ($r75Artifacts | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
$forbiddenPatterns = @("35=D", "35=F", "35=H", "35=AE", "554=", "BEGIN PRIVATE KEY", "password=", "secret=", "token=", "lmax.com", "fix-marketdata")
foreach ($pattern in $forbiddenPatterns) {
    if ($artifactText.IndexOf($pattern, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        Fail "R75 artifacts contain forbidden sensitive pattern: $pattern"
    }
}

Write-Host "LMAX_R75_VALIDATION_PASS"
