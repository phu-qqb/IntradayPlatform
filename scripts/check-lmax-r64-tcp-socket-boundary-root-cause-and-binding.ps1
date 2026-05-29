$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root "artifacts/readiness/lmax-runtime-enablement"

function Fail($message) {
    Write-Error "LMAX-R64 validation failed: $message"
    exit 1
}

function Read-Json($name) {
    $path = Join-Path $artifactRoot $name
    if (!(Test-Path $path)) {
        Fail "Missing artifact $name"
    }

    Get-Content -Raw $path | ConvertFrom-Json
}

$summary = Read-Json "phase-lmax-r64-tcp-socket-boundary-root-cause-summary.json"
$binding = Read-Json "phase-lmax-r64-socket-connector-binding-validation.json"
$boundary = Read-Json "phase-lmax-r64-no-external-boundary-attempted.json"
$gate = Read-Json "phase-lmax-r64-gate-validation.json"
$apiWorker = Read-Json "phase-lmax-r64-api-worker-fake-gateway-audit.json"
$forbidden = Read-Json "phase-lmax-r64-forbidden-actions-audit.json"
$caveat = Read-Json "phase-lmax-r64-usdjpy-caveat-preservation.json"

if ($summary.classification -ne "LMAX_R64_PASS_TCP_SOCKET_CONNECTOR_BINDING_READY_NO_EXTERNAL_ACTIVATION") {
    Fail "Final classification is absent or not allowed."
}

if ($summary.socketClientExecutionDependencyMissingClearedForNextRetry -ne $true) {
    Fail "SocketClientExecutionDependencyMissing remains true for next retry."
}

if ($summary.socketConnectorNotConfiguredClearedForNextRetry -ne $true) {
    Fail "SocketConnectorNotConfigured remains true for next retry."
}

if ($binding.realSocketConnectorBindingReady -ne $true) {
    Fail "Real socket connector binding is not provable."
}

if ($binding.bindingIsFakedByWeakeningValidation -ne $false) {
    Fail "Socket binding appears to be faked by weakening validation."
}

if ($binding.socketConnectorDefaultGlobal -ne $false -or $summary.realSocketConnectorDefaultGlobal -ne $false) {
    Fail "Real socket connector became global/default."
}

if ($binding.noExternalDefaultPreserved -ne $true -or $summary.noExternalDefaultPreserved -ne $true) {
    Fail "No-external-boundary default is not preserved."
}

if ($boundary.externalActivationAttempted -ne $false -or $boundary.attemptCount -ne 0) {
    Fail "External activation was attempted during R64."
}

if ($boundary.tcpSocket -ne "NotAttempted" -and $boundary.tcpSocket -ne "ValidationOnly") {
    Fail "TCP status is not NotAttempted or ValidationOnly during R64."
}

foreach ($name in @("tls", "fixLogonSession", "marketDataRequest", "marketDataResponseEntries")) {
    if ($boundary.$name -ne "NotAttempted") {
        Fail "$name boundary was attempted during R64."
    }
}

if ($summary.credentialValuesReturned -ne $false -or $boundary.credentialValuesReturned -ne $false -or $gate.credentialValuesReturned -ne $false) {
    Fail "credentialValuesReturned is not false."
}

if ($forbidden.result -ne "PASS") {
    Fail "Forbidden-action audit did not pass."
}

if ($apiWorker.result -ne "PASS" -or $apiWorker.apiGateway -ne "FakeLmaxGatewayOnly" -or $apiWorker.workerGateway -ne "FakeLmaxGatewayOnly") {
    Fail "API/Worker FakeLmaxGatewayOnly audit failed."
}

if ($caveat.result -ne "PASS" -or $caveat.securityId -ne "4004" -or $caveat.securityIdSource -ne "8" -or $caveat.weakened -ne $false) {
    Fail "USDJPY caveat is missing or weakened."
}

$factoryPath = Join-Path $root "tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualExecutionSurfaceFactory.cs"
$connectorPath = Join-Path $root "tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualTcpSocketConnector.cs"
$programPath = Join-Path $root "tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/Program.cs"
$apiProgram = Join-Path $root "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgram = Join-Path $root "src/QQ.Production.Intraday.Worker/Program.cs"
$appsettings = Join-Path $root "src/QQ.Production.Intraday.Api/appsettings.json"

foreach ($path in @($factoryPath, $connectorPath, $programPath, $apiProgram, $workerProgram, $appsettings)) {
    if (!(Test-Path $path)) {
        Fail "Missing source file $path"
    }
}

$factory = Get-Content -Raw $factoryPath
$connector = Get-Content -Raw $connectorPath
$program = Get-Content -Raw $programPath
$api = Get-Content -Raw $apiProgram
$worker = Get-Content -Raw $workerProgram
$settings = Get-Content -Raw $appsettings

if ($factory -notmatch "new LmaxReadOnlyActivationManualTcpSocketConnector\(\)" -or $factory -notmatch "socketConnector\.Connect") {
    Fail "Factory does not bind the manual socket connector into the socket client."
}

if ($connector -notmatch "TcpClient" -or $connector -notmatch "ConnectAsync" -or $connector -notmatch "LmaxApprovedBoundedExecutableRetryPhaseReservations\.IsApproved") {
    Fail "Manual socket connector does not prove a real bounded retry connector."
}

if ($api -notmatch "FakeLmaxGateway" -or $worker -notmatch "FakeLmaxGateway") {
    Fail "API/Worker gateway registration changed away from FakeLmaxGatewayOnly."
}

foreach ($text in @($api, $worker, $settings)) {
    if ($text -match "LmaxReadOnlyActivationManualTcpSocketConnector" -or $text -match "QQ\.Production\.Intraday\.Tools\.LmaxReadOnlyActivation") {
        Fail "Manual CLI/socket connector is reachable from API/Worker/default startup."
    }
}

if ($settings -match '"Enabled"\s*:\s*true' -or $settings -match '"AllowExternalConnections"\s*:\s*true' -or $settings -match '"AllowLiveTrading"\s*:\s*true') {
    Fail "Appsettings/default live enablement was introduced."
}

$artifactText = Get-ChildItem $artifactRoot -Filter "phase-lmax-r64-*" | Get-Content -Raw
if ($artifactText -match "password\s*[:=]" -or $artifactText -match "secret\s*[:=]" -or $artifactText -match "554=" -or $artifactText -match "35=D" -or $artifactText -match "35=F") {
    Fail "Raw credentials or sensitive FIX material appear in artifacts."
}

if ($gate.buildResult -eq "PENDING" -or $gate.fullTestsResult -eq "PENDING") {
    Fail "Build/test evidence is missing."
}

Write-Host "LMAX-R64 validator PASS"
