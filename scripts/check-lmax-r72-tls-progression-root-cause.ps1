param(
    [string]$ArtifactRoot = "artifacts/readiness/lmax-runtime-enablement"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Error "LMAX-R72 validation failed: $Message"
    exit 1
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing artifact: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

$allowedClassifications = @(
    "LMAX_R72_PASS_TLS_PROGRESSION_ROOT_CAUSE_IDENTIFIED_NO_EXTERNAL_ACTIVATION",
    "LMAX_R72_PASS_TLS_PROGRESS_STOP_AFTER_TCP_BY_DESIGN_NO_EXTERNAL_ACTIVATION",
    "LMAX_R72_PASS_TLS_CLIENT_BINDING_MISSING_NO_EXTERNAL_ACTIVATION",
    "LMAX_R72_PASS_TLS_HANDSHAKE_OPERATION_BINDING_MISSING_NO_EXTERNAL_ACTIVATION",
    "LMAX_R72_PASS_TCP_TO_TLS_CONTINUATION_BINDING_MISSING_NO_EXTERNAL_ACTIVATION",
    "LMAX_R72_PASS_TLS_PROVIDER_PRESENT_NOT_SEQUENCED_NO_EXTERNAL_ACTIVATION",
    "LMAX_R72_PASS_TLS_PROGRESSION_BINDING_READY_NO_EXTERNAL_ACTIVATION",
    "LMAX_R72_FAIL_TLS_PROGRESSION_ROOT_CAUSE_NOT_PROVABLE",
    "LMAX_R72_FAIL_TLS_PROVIDER_CLIENT_REVIEW_MISSING",
    "LMAX_R72_FAIL_TCP_TO_TLS_TRACE_MISSING",
    "LMAX_R72_FAIL_EXTERNAL_BOUNDARY_ATTEMPTED",
    "LMAX_R72_FAIL_TLS_FIX_OR_MARKETDATA_ATTEMPTED",
    "LMAX_R72_FAIL_FORBIDDEN_ACTION_INTRODUCED",
    "LMAX_R72_FAIL_CREDENTIAL_VALUES_RETURNED_OR_SECRET_RISK",
    "LMAX_R72_FAIL_API_WORKER_GATEWAY_REGRESSION",
    "LMAX_R72_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R72_FAIL_BUILD_OR_TESTS"
)

$summary = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r72-tls-progression-root-cause-summary.json")
$gate = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r72-gate-validation.json")
$r71Boundary = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r71-boundary-evidence.json")
$r71Trace = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r71-operational-invocation-trace.json")
$r71TcpReview = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r72-r71-tcp-success-review.json")
$rootCause = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r72-tls-not-attempted-root-cause.json")
$progression = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r72-tcp-to-tls-progression-trace.json")
$tlsReview = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r72-tls-provider-client-binding-review.json")
$noExternal = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r72-no-external-boundary-attempted.json")
$forbidden = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r72-forbidden-actions-audit.json")
$apiWorker = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r72-api-worker-fake-gateway-audit.json")
$scheduler = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r72-no-scheduler-polling-service-audit.json")
$credential = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r72-credential-sanitization-validation.json")
$usdJpy = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r72-usdjpy-caveat-preservation.json")
$next = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r72-next-phase-recommendation.json")

if ($summary.phase -ne "LMAX-R72") { Fail "Summary phase mismatch." }
if ($allowedClassifications -notcontains $summary.classification) { Fail "Final classification is absent or not allowed." }
if ($summary.classification -ne $gate.classification) { Fail "Gate classification does not match summary." }
if ($summary.newActivationPerformed -ne $false -or $summary.externalActivationAttemptedInR72 -ne $false) { Fail "R72 attempted activation." }

if ($r71Boundary.classification -ne "LMAX_R71_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE") { Fail "R71 classification evidence missing or mismatched." }
if ($r71Boundary.boundaryStatuses.tcpSocket -ne "Succeeded") { Fail "R71 TCP success evidence is missing." }
if ($r71Boundary.boundaryStatuses.tls -ne "NotAttempted") { Fail "R71 TLS NotAttempted evidence is missing." }
if ($r71Boundary.sanitizedErrorCategory -ne "TcpSocketSucceededButTlsBoundaryNotAttempted") { Fail "TLS NotAttemptedAfterTcpSuccess is not acknowledged." }
if ($r71Trace.attemptCount -ne 1 -or $r71Trace.tcpConnectionAttempted -ne $true -or $r71Trace.tlsHandshakeAttempted -ne $false) { Fail "R71 operational invocation trace does not prove TCP succeeded without TLS." }
if ($r71TcpReview.tcpSuccessProven -ne $true) { Fail "R71 TCP success review missing." }

if ([string]::IsNullOrWhiteSpace($rootCause.rootCause) -or [string]::IsNullOrWhiteSpace($rootCause.responsibleFactory)) { Fail "Root cause does not identify why TLS was not attempted." }
if ($rootCause.tlsProviderPresent -ne $true -or $rootCause.tlsClientPresent -ne $true) { Fail "TLS provider/client review missing from root cause." }
if ($rootCause.tlsClientConcreteHandshakeConfiguredForManualRealBoundedPath -ne $false) { Fail "Root cause must show TLS concrete handshake is not bound." }
if ($rootCause.tcpToTlsContinuationConfiguredForManualRealBoundedPath -ne $false) { Fail "Root cause must show TCP-to-TLS continuation is missing." }
if ($tlsReview.manualRealBoundedPathSuppliesConcreteTlsHandshakeOperation -ne $false) { Fail "TLS provider/client binding review is inconsistent." }
if ($tlsReview.manualRealBoundedPathSuppliesTcpSocketStreamContinuation -ne $false) { Fail "TCP-to-TLS continuation review is inconsistent." }
if ($progression.manualRealBoundedPathBreak.concreteHandshakeOperationSupplied -ne $false) { Fail "TCP-to-TLS progression trace missing concrete handshake gap." }
if ($progression.manualRealBoundedPathBreak.socketStreamContinuationSupplied -ne $false) { Fail "TCP-to-TLS progression trace missing continuation gap." }

$factoryPath = "tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualExecutionSurfaceFactory.cs"
$transportPath = "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxRealReadOnlyMarketDataTransport.cs"
$clientsPath = "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxRealReadOnlyProviderClients.cs"
foreach ($sourcePath in @($factoryPath, $transportPath, $clientsPath)) {
    if (-not (Test-Path -LiteralPath $sourcePath)) { Fail "Missing source file: $sourcePath" }
}

$factorySource = Get-Content -LiteralPath $factoryPath -Raw
$transportSource = Get-Content -LiteralPath $transportPath -Raw
$clientsSource = Get-Content -LiteralPath $clientsPath -Raw
if ($factorySource -notmatch "new\s+LmaxRealReadOnlyTlsHandshakeClient\s*\(\s*\)") { Fail "Factory no longer shows the reviewed default TLS client binding." }
if ($transportSource -notmatch "OpenReadOnlyTlsBoundary") { Fail "Transport TLS sequencing proof missing." }
if ($clientsSource -notmatch "TlsHandshakeFactoryNotConfigured") { Fail "TLS default-not-configured proof missing." }

if ($noExternal.externalActivationAttempted -ne $false -or $noExternal.tcpSocketAttempted -ne $false -or $noExternal.tlsAttempted -ne $false -or $noExternal.fixLogonAttempted -ne $false -or $noExternal.marketDataRequestAttempted -ne $false) {
    Fail "R72 attempted an external boundary."
}

if ($forbidden.result -ne "PASS") { Fail "Forbidden action audit failed." }
if ($apiWorker.result -ne "PASS" -or $apiWorker.apiWorkerGateway -ne "FakeLmaxGatewayOnly" -or $apiWorker.apiWorkerGatewayChanged -ne $false) { Fail "API/Worker FakeLmaxGatewayOnly audit failed." }
if ($scheduler.result -ne "PASS" -or $scheduler.schedulerIntroduced -ne $false -or $scheduler.pollingLoopIntroduced -ne $false) { Fail "Scheduler/polling/service audit failed." }
if ($credential.credentialValuesReturned -ne $false -or $credential.credentialValuesPrinted -ne $false -or $credential.credentialValuesStored -ne $false -or $credential.credentialValuesSerialized -ne $false) { Fail "Credential sanitization failed." }
if ($usdJpy.result -ne "PASS" -or $usdJpy.securityId -ne "4004" -or $usdJpy.securityIdSource -ne "8" -or $usdJpy.caveatWeakened -ne $false) { Fail "USDJPY caveat missing or weakened." }
if ($gate.buildResult.status -ne "PASS" -or $gate.testResult.status -ne "PASS") { Fail "Build/test evidence is missing." }
if ($next.recommendedPhase -ne "LMAX-R73") { Fail "Next-phase recommendation is absent or unexpected." }

$r72Artifacts = Get-ChildItem -LiteralPath $ArtifactRoot -Filter "phase-lmax-r72-*" -File
$artifactText = ($r72Artifacts | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
$forbiddenPatterns = @("35=D", "35=F", "35=H", "35=AE", "554=", "BEGIN PRIVATE KEY", "password=", "secret=", "token=")
foreach ($pattern in $forbiddenPatterns) {
    if ($artifactText.IndexOf($pattern, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        Fail "R72 artifacts contain forbidden sensitive pattern: $pattern"
    }
}

Write-Host "LMAX_R72_VALIDATION_PASS"
