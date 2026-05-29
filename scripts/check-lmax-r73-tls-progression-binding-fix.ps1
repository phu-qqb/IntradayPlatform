param(
    [string]$ArtifactRoot = "artifacts/readiness/lmax-runtime-enablement"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Error "LMAX-R73 validation failed: $Message"
    exit 1
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing artifact: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

$allowedClassifications = @(
    "LMAX_R73_PASS_TLS_PROGRESSION_BINDING_READY_NO_EXTERNAL_ACTIVATION",
    "LMAX_R73_FAIL_TLS_BINDING_NOT_PROVABLE",
    "LMAX_R73_FAIL_TCP_TO_TLS_CONTINUATION_STILL_MISSING",
    "LMAX_R73_FAIL_TLS_DEFAULT_GLOBAL_RISK",
    "LMAX_R73_FAIL_EXTERNAL_BOUNDARY_ATTEMPTED",
    "LMAX_R73_FAIL_FORBIDDEN_ACTION_INTRODUCED",
    "LMAX_R73_FAIL_CREDENTIAL_VALUES_RETURNED_OR_SECRET_RISK",
    "LMAX_R73_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R73_FAIL_BUILD_OR_TESTS"
)

$summary = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r73-tls-progression-binding-fix-summary.json")
$gate = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r73-gate-validation.json")
$beforeAfter = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r73-r72-root-cause-before-after-classification.json")
$continuation = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r73-tcp-to-tls-continuation-binding-validation.json")
$tlsBinding = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r73-tls-handshake-operation-binding-validation.json")
$realBounded = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r73-real-bounded-path-validation.json")
$noExternal = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r73-no-external-boundary-attempted.json")
$forbidden = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r73-forbidden-actions-audit.json")
$apiWorker = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r73-api-worker-fake-gateway-audit.json")
$scheduler = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r73-no-scheduler-polling-service-audit.json")
$credential = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r73-credential-sanitization-validation.json")
$usdJpy = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r73-usdjpy-caveat-preservation.json")
$next = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r73-next-phase-recommendation.json")
$r72Summary = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r72-tls-progression-root-cause-summary.json")

if ($summary.phase -ne "LMAX-R73") { Fail "Summary phase mismatch." }
if ($allowedClassifications -notcontains $summary.classification) { Fail "Final classification absent or not allowed." }
if ($summary.classification -ne "LMAX_R73_PASS_TLS_PROGRESSION_BINDING_READY_NO_EXTERNAL_ACTIVATION") { Fail "R73 did not pass with TLS progression binding ready." }
if ($summary.classification -ne $gate.classification) { Fail "Gate classification mismatch." }
if ($r72Summary.classification -ne "LMAX_R72_PASS_TCP_TO_TLS_CONTINUATION_BINDING_MISSING_NO_EXTERNAL_ACTIVATION") { Fail "R72 root-cause evidence missing." }

if ($summary.tlsBindingReady -ne $true -or $tlsBinding.concreteTlsHandshakeOperationBindingProvable -ne $true) { Fail "Concrete TLS handshake operation binding is not provable." }
if ($summary.tcpToTlsContinuationReady -ne $true -or $continuation.tcpToTlsContinuationReady -ne $true) { Fail "TCP-to-TLS continuation remains missing." }
if ($summary.tlsClientExecutionDependencyMissingClearedForNextRetry -ne $true -or $tlsBinding.tlsClientExecutionDependencyMissingRemainsForNextRetry -ne $false) { Fail "TlsClientExecutionDependencyMissing remains true for next retry." }
if ($summary.tlsHandshakeFactoryNotConfiguredClearedForNextRetry -ne $true -or $tlsBinding.tlsHandshakeFactoryNotConfiguredRemainsForNextRetry -ne $false) { Fail "TlsHandshakeFactoryNotConfigured remains true for next retry." }
if ($beforeAfter.after.concreteTlsHandshakeOperationBound -ne $true -or $beforeAfter.after.tcpToTlsContinuationBound -ne $true) { Fail "Before/after proof does not show TLS binding fixed." }

$factoryPath = "tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualExecutionSurfaceFactory.cs"
$connectorPath = "tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualTcpSocketConnector.cs"
$apiPath = "src/QQ.Production.Intraday.Api/Program.cs"
$workerPath = "src/QQ.Production.Intraday.Worker/Program.cs"
$appSettingsPath = "src/QQ.Production.Intraday.Api/appsettings.json"
foreach ($path in @($factoryPath, $connectorPath, $apiPath, $workerPath, $appSettingsPath)) {
    if (-not (Test-Path -LiteralPath $path)) { Fail "Missing source file: $path" }
}

$factorySource = Get-Content -LiteralPath $factoryPath -Raw
$connectorSource = Get-Content -LiteralPath $connectorPath -Raw
$apiSource = Get-Content -LiteralPath $apiPath -Raw
$workerSource = Get-Content -LiteralPath $workerPath -Raw
$appSettings = Get-Content -LiteralPath $appSettingsPath -Raw

if ($factorySource -notmatch "new\s+LmaxRealReadOnlyTlsHandshakeClient\s*\(\s*socketConnector\.AuthenticateTls\s*,\s*socketConnector\.ShutdownRevert\s*\)") {
    Fail "Manual real-bounded factory does not bind socketConnector.AuthenticateTls into LmaxRealReadOnlyTlsHandshakeClient."
}

if ($factorySource -match "new\s+LmaxRealReadOnlyTlsHandshakeClient\s*\(\s*\)\s*;") {
    Fail "Manual real-bounded path still constructs the default not-configured TLS client."
}

if ($connectorSource -notmatch "AuthenticateTls" -or $connectorSource -notmatch "SslStream" -or $connectorSource -notmatch "AuthenticateAsClientAsync" -or $connectorSource -notmatch "TcpBoundaryNotOpened") {
    Fail "Manual TLS continuation implementation is not provable."
}

if ($factorySource -notmatch "CreateForManualTool\(\)\s*=>\s*CreateForManualTool\(NoExternalBoundaryMode\)") {
    Fail "No-external default mode is not preserved."
}

if ($summary.tlsGlobalDefault -ne $false -or $realBounded.tlsDefaultGlobal -ne $false -or $tlsBinding.tlsDefaultGlobal -ne $false) { Fail "TLS became global/default." }
if ($summary.noExternalDefaultPreserved -ne $true -or $realBounded.noExternalDefaultPreserved -ne $true) { Fail "No-external default was removed." }
if ($apiSource -match "LmaxReadOnlyActivationManualExecutionSurface|LmaxReadOnlyActivationManualTcpSocketConnector|AuthenticateTls" -or
    $workerSource -match "LmaxReadOnlyActivationManualExecutionSurface|LmaxReadOnlyActivationManualTcpSocketConnector|AuthenticateTls" -or
    $appSettings -match "LmaxReadOnlyActivationManualExecutionSurface|real-bounded-executable-readonly|AuthenticateTls") {
    Fail "Manual TLS binding became reachable from API/Worker/default startup."
}
if ($apiWorker.result -ne "PASS" -or $apiWorker.apiWorkerGateway -ne "FakeLmaxGatewayOnly" -or $apiWorker.apiWorkerGatewayChanged -ne $false) { Fail "API/Worker gateway audit failed." }
if ($scheduler.result -ne "PASS" -or $scheduler.hostedServiceIntroduced -ne $false -or $scheduler.schedulerIntroduced -ne $false -or $scheduler.pollingLoopIntroduced -ne $false) { Fail "Scheduler/polling/service introduced." }
if ($forbidden.result -ne "PASS" -or $forbidden.ordersIntroducedOrTouched -ne $false -or $forbidden.tradingStateMutationIntroduced -ne $false) { Fail "Forbidden action introduced." }

if ($noExternal.externalActivationAttempted -ne $false -or
    $noExternal.socketOpened -ne $false -or
    $noExternal.tcpSocketAttempted -ne $false -or
    $noExternal.tlsAttempted -ne $false -or
    $noExternal.fixLogonAttempted -ne $false -or
    $noExternal.marketDataRequestAttempted -ne $false) {
    Fail "External boundary attempted during R73."
}

if ($credential.credentialValuesReturned -ne $false -or
    $credential.credentialValuesRead -ne $false -or
    $credential.credentialValuesPrinted -ne $false -or
    $credential.credentialValuesStored -ne $false -or
    $credential.credentialValuesSerialized -ne $false) {
    Fail "Credential values returned or exposed."
}

if ($usdJpy.result -ne "PASS" -or $usdJpy.securityId -ne "4004" -or $usdJpy.securityIdSource -ne "8" -or $usdJpy.caveatWeakened -ne $false) { Fail "USDJPY caveat missing or weakened." }
if ($gate.buildResult.status -ne "PASS" -or $gate.testResult.status -ne "PASS") { Fail "Build/test evidence missing." }
if ($next.recommendedPhase -ne "LMAX-R75") { Fail "Next phase must be LMAX-R75 because retry phases are odd." }

$r73Artifacts = Get-ChildItem -LiteralPath $ArtifactRoot -Filter "phase-lmax-r73-*" -File
$artifactText = ($r73Artifacts | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
$forbiddenPatterns = @("35=D", "35=F", "35=H", "35=AE", "554=", "BEGIN PRIVATE KEY", "password=", "secret=", "token=")
foreach ($pattern in $forbiddenPatterns) {
    if ($artifactText.IndexOf($pattern, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        Fail "R73 artifacts contain forbidden sensitive pattern: $pattern"
    }
}

Write-Host "LMAX_R73_VALIDATION_PASS"
