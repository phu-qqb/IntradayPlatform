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
    "phase-lmax-r66-tcp-socket-runtime-boundary-root-cause-report.md",
    "phase-lmax-r66-tcp-socket-runtime-boundary-root-cause-summary.json",
    "phase-lmax-r66-r65-boundary-before-after-classification.json",
    "phase-lmax-r66-r65-tcp-evidence-review.json",
    "phase-lmax-r66-endpoint-config-sanitized-review.json",
    "phase-lmax-r66-tcp-failure-classification.json",
    "phase-lmax-r66-socket-exception-sanitized-evidence.json",
    "phase-lmax-r66-no-tls-fix-marketdata-attempted.json",
    "phase-lmax-r66-forbidden-actions-audit.json",
    "phase-lmax-r66-api-worker-fake-gateway-audit.json",
    "phase-lmax-r66-no-scheduler-polling-service-audit.json",
    "phase-lmax-r66-credential-sanitization-validation.json",
    "phase-lmax-r66-usdjpy-caveat-preservation.json",
    "phase-lmax-r66-next-phase-recommendation.json",
    "phase-lmax-r66-gate-validation.json"
)

foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $name))) {
        Fail "Missing required R66 artifact: $name"
    }
}

$allowed = @(
    "LMAX_R66_PASS_TCP_SOCKET_RUNTIME_BOUNDARY_ROOT_CAUSE_CLASSIFIED_NO_ADVANCE_TO_TLS",
    "LMAX_R66_PASS_TCP_SOCKET_ROOT_CAUSE_ENDPOINT_CONFIG_MISSING_NO_ADVANCE_TO_TLS",
    "LMAX_R66_PASS_TCP_SOCKET_ROOT_CAUSE_ENDPOINT_CONFIG_INVALID_NO_ADVANCE_TO_TLS",
    "LMAX_R66_PASS_TCP_SOCKET_ROOT_CAUSE_DNS_FAILURE_NO_ADVANCE_TO_TLS",
    "LMAX_R66_PASS_TCP_SOCKET_ROOT_CAUSE_CONNECTION_TIMEOUT_NO_ADVANCE_TO_TLS",
    "LMAX_R66_PASS_TCP_SOCKET_ROOT_CAUSE_CONNECTION_REFUSED_NO_ADVANCE_TO_TLS",
    "LMAX_R66_PASS_TCP_SOCKET_ROOT_CAUSE_NETWORK_UNREACHABLE_NO_ADVANCE_TO_TLS",
    "LMAX_R66_PASS_TCP_SOCKET_ROOT_CAUSE_FIREWALL_OR_PERMISSION_NO_ADVANCE_TO_TLS",
    "LMAX_R66_PASS_TCP_SOCKET_ROOT_CAUSE_EXTERNAL_OTHER_NO_ADVANCE_TO_TLS",
    "LMAX_R66_FAIL_TCP_SOCKET_ROOT_CAUSE_NOT_PROVABLE",
    "LMAX_R66_FAIL_ENDPOINT_CONFIG_SANITIZATION_MISSING",
    "LMAX_R66_FAIL_TLS_FIX_OR_MARKETDATA_ATTEMPTED",
    "LMAX_R66_FAIL_FORBIDDEN_ACTION_INTRODUCED",
    "LMAX_R66_FAIL_CREDENTIAL_VALUES_RETURNED_OR_SECRET_RISK",
    "LMAX_R66_FAIL_API_WORKER_GATEWAY_REGRESSION",
    "LMAX_R66_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R66_FAIL_BUILD_OR_TESTS"
)

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r66-tcp-socket-runtime-boundary-root-cause-summary.json")
$beforeAfter = Read-Json (Join-Path $artifactRoot "phase-lmax-r66-r65-boundary-before-after-classification.json")
$r65 = Read-Json (Join-Path $artifactRoot "phase-lmax-r66-r65-tcp-evidence-review.json")
$endpoint = Read-Json (Join-Path $artifactRoot "phase-lmax-r66-endpoint-config-sanitized-review.json")
$failure = Read-Json (Join-Path $artifactRoot "phase-lmax-r66-tcp-failure-classification.json")
$socket = Read-Json (Join-Path $artifactRoot "phase-lmax-r66-socket-exception-sanitized-evidence.json")
$noAdvance = Read-Json (Join-Path $artifactRoot "phase-lmax-r66-no-tls-fix-marketdata-attempted.json")
$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r66-forbidden-actions-audit.json")
$apiWorker = Read-Json (Join-Path $artifactRoot "phase-lmax-r66-api-worker-fake-gateway-audit.json")
$cred = Read-Json (Join-Path $artifactRoot "phase-lmax-r66-credential-sanitization-validation.json")
$usdJpy = Read-Json (Join-Path $artifactRoot "phase-lmax-r66-usdjpy-caveat-preservation.json")
$gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r66-gate-validation.json")
$r65Gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r65-gate-validation.json")

if ($allowed -notcontains $summary.classification -or $allowed -notcontains $gate.classification) {
    Fail "R66 final classification is absent or not allowed."
}

if ($summary.classification -ne "LMAX_R66_PASS_TCP_SOCKET_ROOT_CAUSE_ENDPOINT_CONFIG_INVALID_NO_ADVANCE_TO_TLS") {
    Fail "Unexpected R66 classification: $($summary.classification)"
}

if ($r65Gate.classification -ne "LMAX_R65_FAIL_TCP_SOCKET_BOUNDARY" -or $r65Gate.validatorResult -ne "PASS") {
    Fail "R65 FailedExternal evidence is missing or ignored."
}

if (-not $summary.r65FailedExternalEvidenceReviewed -or -not $r65.r65EvidenceReviewed -or $r65.r65TcpBoundary -ne "FailedExternal") {
    Fail "R65 TCP FailedExternal evidence was not reviewed."
}

if (-not $summary.tcpSocketFailureCategory -or -not $failure.socketFailureCategory -or $summary.tcpSocketFailureCategory -ne "ConfigInvalid" -or -not $failure.endpointConfigInvalid) {
    Fail "TCP/socket root cause is not classified as endpoint config invalid."
}

if ($endpoint.endpointMode -ne "Demo" -or -not $endpoint.endpointPresent -or -not $endpoint.hostPresent -or -not $endpoint.portPresent -or -not $endpoint.productionEndpointExcluded -or -not $endpoint.productionAccountExcluded) {
    Fail "Endpoint/config sanitized review is missing required fields."
}

if ($endpoint.hostRawValueStored -ne $false -or $cred.rawHostStored -ne $false) {
    Fail "Raw host/endpoint value appears to be stored."
}

if ($summary.newTcpDiagnosticPerformed -or $summary.externalActivationAttemptedDuringR66 -or $noAdvance.externalActivationAttemptedDuringR66 -or $noAdvance.newTcpDiagnosticPerformed) {
    Fail "R66 performed a new TCP diagnostic or external activation."
}

if ($summary.tlsAttempted -or $summary.fixLogonAttempted -or $summary.marketDataRequestAttempted -or $noAdvance.tlsAttemptedDuringR66 -or $noAdvance.fixLogonAttemptedDuringR66 -or $noAdvance.marketDataRequestAttemptedDuringR66) {
    Fail "R66 advanced to TLS/FIX/MarketData."
}

if ($noAdvance.pollingLoopUsed -or $noAdvance.retryLoopUsed -or $noAdvance.schedulerUsed) {
    Fail "R66 introduced polling/retry/scheduler behavior."
}

if ($summary.credentialValuesReturned -or $cred.credentialValuesReturned -or $cred.credentialValuesRead -or $cred.credentialValuesPrinted -or $cred.credentialValuesStored -or $cred.credentialValuesSerialized) {
    Fail "Credential values were returned/read/printed/stored/serialized."
}

if ($forbidden.result -ne "PASS") {
    Fail "Forbidden-action audit failed."
}

foreach ($flag in @("ordersSubmitted", "newOrderSingleSent", "cancelReplaceSent", "tradingEnablementExecuted", "tradingStateMutated", "productionAccountUsed", "productionAccountAllowed", "apiStarted", "workerStarted", "hostedServiceStarted", "backgroundServiceStarted", "schedulerStarted", "pollingLoopStarted", "replayExecuted", "shadowReplayExecuted", "shadowReplaySubmitted", "credentialValuesReturned", "credentialValuesPrintedStoredSerialized", "rawSensitiveFixLogsStored", "tlsAttempted", "fixLogonAttempted", "marketDataRequestAttempted")) {
    if ($forbidden.$flag) {
        Fail "Forbidden action flag is true: $flag"
    }
}

if ($apiWorker.result -ne "PASS" -or $apiWorker.apiWorkerGatewayMode -ne "FakeLmaxGatewayOnly" -or $apiWorker.manualExecutionSurfaceRegisteredInApi -or $apiWorker.manualExecutionSurfaceRegisteredInWorker -or $apiWorker.realBoundedAdapterRegisteredInApi -or $apiWorker.realBoundedAdapterRegisteredInWorker -or $apiWorker.socketConnectorRegisteredInApi -or $apiWorker.socketConnectorRegisteredInWorker -or $apiWorker.defaultRuntimeGatewayChanged) {
    Fail "API/Worker FakeLmaxGatewayOnly audit failed."
}

if (-not $usdJpy.caveatPreserved -or $usdJpy.securityId -ne "4004" -or $usdJpy.securityIdSource -ne "8" -or $usdJpy.caveat -ne "prior failed-safe root cause remains unproven") {
    Fail "USDJPY caveat is missing or weakened."
}

$factoryPath = Join-Path $Root "tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualExecutionSurfaceFactory.cs"
$connectorPath = Join-Path $Root "tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualTcpSocketConnector.cs"
if (-not (Test-Path -LiteralPath $factoryPath) -or -not (Test-Path -LiteralPath $connectorPath)) {
    Fail "Required manual CLI socket source is missing."
}

$factory = Get-Content -LiteralPath $factoryPath -Raw
$connector = Get-Content -LiteralPath $connectorPath -Raw
if ($factory -notmatch "new LmaxReadOnlySocketConnectionOptions" -or $factory -notmatch "DemoReadOnlyEndpoint" -or $connector -notmatch "ConnectAsync\(options\.SanitizedEndpointLabel") {
    Fail "Responsible endpoint factory/binding source proof is missing."
}

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r66-*" |
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
        Fail "Potential raw credential or sensitive FIX material found in R66 artifacts: $pattern"
    }
}

if ($gate.buildResult.status -ne "PASS" -or $gate.testResult.status -ne "PASS") {
    Fail "Build/test evidence is missing or not PASS."
}

Write-Host "LMAX R66 TCP/socket runtime boundary root-cause validation PASS"
