param(
    [string]$ArtifactRoot = "artifacts/readiness/lmax-runtime-enablement"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Error "LMAX-R76 validation failed: $Message"
    exit 1
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing artifact: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

$allowedClassifications = @(
    "LMAX_R76_PASS_RUNTIME_EVIDENCE_REVIEW_CONTROLLED_READINESS_GATE",
    "LMAX_R76_FAIL_R75_EVIDENCE_INCOMPLETE",
    "LMAX_R76_FAIL_ATTEMPT_COUNT_INVALID",
    "LMAX_R76_FAIL_TCP_SOCKET_SUCCESS_NOT_PROVABLE",
    "LMAX_R76_FAIL_TLS_ATTEMPT_NOT_PROVABLE",
    "LMAX_R76_FAIL_SANITIZATION_RISK",
    "LMAX_R76_FAIL_CREDENTIAL_ENDPOINT_TLS_FIX_PROTECTION_RISK",
    "LMAX_R76_FAIL_API_WORKER_GATEWAY_REGRESSION",
    "LMAX_R76_FAIL_FORBIDDEN_ACTION_EVIDENCE",
    "LMAX_R76_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R76_FAIL_SHUTDOWN_REVERT_INCOMPLETE",
    "LMAX_R76_FAIL_BUILD_OR_TESTS"
)

$summary = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r76-runtime-evidence-review-summary.json")
$gate = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r76-gate-validation.json")
$r75Summary = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r75-temporary-readonly-activation-retry-summary.json")
$r75Trace = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r75-operational-invocation-trace.json")
$r75Boundary = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r75-boundary-evidence.json")
$r75Endpoint = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r75-demo-endpoint-binding-evidence.json")
$r75Socket = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r75-socket-connector-evidence.json")
$r75Tls = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r75-tls-boundary-evidence.json")
$r75MarketData = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r75-marketdata-sanitized-result.json")
$r75Shutdown = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r75-shutdown-revert-evidence.json")
$r75Gate = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r75-gate-validation.json")
$completeness = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r76-r75-evidence-completeness-review.json")
$singleAttempt = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r76-single-attempt-validation.json")
$tcp = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r76-tcp-socket-success-validation.json")
$tls = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r76-tls-boundary-attempt-validation.json")
$progression = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r76-boundary-progression-review.json")
$sanitization = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r76-sanitization-review.json")
$protection = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r76-credential-endpoint-tls-fix-protection-review.json")
$instruments = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r76-approved-instrument-scope-review.json")
$usdJpy = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r76-usdjpy-caveat-preservation.json")
$apiWorker = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r76-api-worker-fake-gateway-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r76-forbidden-actions-audit.json")
$shutdown = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r76-shutdown-revert-review.json")
$readiness = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r76-controlled-readiness-gate.json")
$next = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r76-next-phase-recommendation.json")

if ($summary.phase -ne "LMAX-R76") { Fail "Summary phase mismatch." }
if ($allowedClassifications -notcontains $summary.classification) { Fail "Final classification absent or not allowed." }
if ($summary.classification -ne "LMAX_R76_PASS_RUNTIME_EVIDENCE_REVIEW_CONTROLLED_READINESS_GATE") { Fail "R76 did not pass the readiness gate." }
if ($summary.classification -ne $gate.classification) { Fail "Gate classification mismatch." }
if ($summary.newActivationPerformed -ne $false -or $summary.reviewOnly -ne $true) { Fail "R76 performed a new activation or was not review-only." }

if ($r75Summary.classification -ne "LMAX_R75_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED" -or $r75Boundary.classification -ne "LMAX_R75_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED") {
    Fail "R75 success classification is missing or mismatched."
}
if ($completeness.result -ne "PASS" -or $completeness.requiredArtifactsPresent -ne $true -or $completeness.internallyConsistent -ne $true) { Fail "R75 evidence incomplete." }
if ($r75Summary.attemptCount -ne 1 -or $r75Trace.attemptCount -ne 1 -or $singleAttempt.attemptCountExactlyOne -ne $true) { Fail "Attempt count is not exactly one." }
if ($r75Summary.manualCliSurfaceUsed -ne $true -or $r75Summary.adapterModeRealBoundedExecutableReadOnlyUsed -ne $true) { Fail "Manual CLI/real-bounded adapter evidence missing." }
if ($r75Endpoint.concreteDemoEndpointBindingUsed -ne $true -or $r75Endpoint.endpointMode -ne "Demo" -or $r75Endpoint.hostWasPlaceholder -ne $false -or $r75Endpoint.productionExcluded -ne $true -or $r75Endpoint.endpointApproved -ne $true) { Fail "Concrete Demo endpoint evidence missing or invalid." }
if ($r75Socket.configuredSocketConnectorUsed -ne $true -or $r75Socket.connectReached -ne $true) { Fail "Configured socket connector evidence missing." }
if ($r75Summary.tcpSocketSucceeded -ne $true -or $r75Boundary.boundaryStatuses.tcpSocket -ne "Succeeded" -or $tcp.tcpSocketSuccessProven -ne $true) { Fail "TCP/socket success is not proven." }
if ($r75Summary.tlsAttempted -ne $true -or $r75Boundary.boundaryStatuses.tls -ne "Attempted" -or $r75Tls.tlsAttempted -ne $true -or $tls.tlsAttemptProven -ne $true) { Fail "TLS attempt is not proven." }
if ($r75Summary.socketConnectorAuthenticateTlsReached -ne $true -or $r75Tls.socketConnectorAuthenticateTlsReached -ne $true -or $tls.socketConnectorAuthenticateTlsEvidenceProven -ne $true) { Fail "socketConnector.AuthenticateTls evidence missing." }
if ($r75Summary.fixLogonSessionAttempted -ne $false -or $r75Trace.fixLogonAttempted -ne $false -or $progression.fixLogonSessionCorrectlyNotAttempted -ne $true) { Fail "FIX logon/session was attempted unexpectedly." }
if ($r75Summary.marketDataRequestAttempted -ne $false -or $r75Trace.marketDataRequestSent -ne $false -or $r75MarketData.marketDataRequestAttempted -ne $false -or $progression.marketDataRequestCorrectlyNotAttempted -ne $true) { Fail "MarketDataRequest was attempted unexpectedly." }

if ($sanitization.result -ne "PASS" -or $sanitization.allOutputsSanitized -ne $true) { Fail "Sanitization review failed." }
if ($summary.credentialValuesReturned -ne $false -or $protection.credentialValuesReturned -ne $false -or $r75Summary.credentialValuesReturned -ne $false) { Fail "credentialValuesReturned is not false." }
if ($summary.credentialEndpointTlsFixSensitiveValuesPrintedStoredSerialized -ne $false -or $protection.credentialEndpointTlsFixProtectionRisk -ne $false) { Fail "Credential/endpoint/TLS/FIX protection risk." }

$expectedInstruments = @("GBPUSD", "EURGBP", "AUDUSD", "USDJPY")
$actualInstruments = @($instruments.approvedInstruments)
if ($instruments.result -ne "PASS" -or $actualInstruments.Count -ne 4) { Fail "Approved instrument scope invalid." }
for ($i = 0; $i -lt $expectedInstruments.Count; $i++) {
    if ($actualInstruments[$i] -ne $expectedInstruments[$i]) { Fail "Approved instruments differ from expected order/scope." }
}
if ($usdJpy.result -ne "PASS" -or $usdJpy.securityId -ne "4004" -or $usdJpy.securityIdSource -ne "8" -or $usdJpy.caveatWeakened -ne $false) { Fail "USDJPY caveat missing or weakened." }
if ($protection.productionAccountOrConfigUsed -ne $false -or $forbidden.productionAccountAllowed -ne $false) { Fail "Production account/config used or allowed." }
if ($apiWorker.result -ne "PASS" -or $apiWorker.apiWorkerGateway -ne "FakeLmaxGatewayOnly" -or $apiWorker.apiWorkerGatewayChanged -ne $false) { Fail "API/Worker gateway regression." }
if ($forbidden.result -ne "PASS" -or $forbidden.ordersIntroducedOrTouched -ne $false -or $forbidden.schedulerPollingIntroduced -ne $false -or $forbidden.replayOrShadowReplayIntroduced -ne $false) { Fail "Forbidden action evidence found." }
if ($shutdown.result -ne "PASS" -or $shutdown.shutdownRevertCompleted -ne $true -or $r75Shutdown.shutdownRevertCompleted -ne $true) { Fail "Shutdown/revert evidence missing or incomplete." }
if ($r75Gate.validatorResult -ne "PASS" -or $gate.buildResult.status -ne "PASS" -or $gate.testResult.status -ne "PASS") { Fail "Build/test/validator evidence missing." }
if ($readiness.controlledReadinessDecision -ne "READY_FOR_NEXT_OPERATOR_APPROVED_FIX_SESSION_BOUNDARY_RETRY" -or $next.recommendedPhase -ne "LMAX-R77") { Fail "Next-phase recommendation is absent or invalid." }

$r76Artifacts = Get-ChildItem -LiteralPath $ArtifactRoot -Filter "phase-lmax-r76-*" -File
$artifactText = ($r76Artifacts | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
$forbiddenPatterns = @("35=D", "35=F", "35=H", "35=AE", "554=", "BEGIN PRIVATE KEY", "password=", "secret=", "token=", "lmax.com", "fix-marketdata")
foreach ($pattern in $forbiddenPatterns) {
    if ($artifactText.IndexOf($pattern, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        Fail "R76 artifacts contain forbidden sensitive pattern: $pattern"
    }
}

Write-Host "LMAX_R76_VALIDATION_PASS"
