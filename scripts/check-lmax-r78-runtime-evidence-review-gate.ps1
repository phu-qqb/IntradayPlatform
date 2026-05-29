param(
    [string]$ArtifactRoot = "artifacts/readiness/lmax-runtime-enablement"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Error "LMAX-R78 validation failed: $Message"
    exit 1
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing artifact: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

$allowedClassifications = @(
    "LMAX_R78_PASS_RUNTIME_EVIDENCE_REVIEW_CONTROLLED_READINESS_GATE",
    "LMAX_R78_FAIL_R77_EVIDENCE_INCOMPLETE",
    "LMAX_R78_FAIL_ATTEMPT_COUNT_INVALID",
    "LMAX_R78_FAIL_TCP_SOCKET_SUCCESS_NOT_PROVABLE",
    "LMAX_R78_FAIL_TLS_ATTEMPT_NOT_PROVABLE",
    "LMAX_R78_FAIL_FIX_NOT_ATTEMPTED_REVIEW_MISSING",
    "LMAX_R78_FAIL_SANITIZATION_RISK",
    "LMAX_R78_FAIL_CREDENTIAL_ENDPOINT_TLS_FIX_PROTECTION_RISK",
    "LMAX_R78_FAIL_API_WORKER_GATEWAY_REGRESSION",
    "LMAX_R78_FAIL_FORBIDDEN_ACTION_EVIDENCE",
    "LMAX_R78_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R78_FAIL_SHUTDOWN_REVERT_INCOMPLETE",
    "LMAX_R78_FAIL_BUILD_OR_TESTS"
)

$summary = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r78-runtime-evidence-review-summary.json")
$gate = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r78-gate-validation.json")
$r77Summary = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r77-temporary-readonly-activation-retry-summary.json")
$r77Trace = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r77-operational-invocation-trace.json")
$r77Boundary = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r77-boundary-evidence.json")
$r77Endpoint = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r77-demo-endpoint-binding-evidence.json")
$r77Socket = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r77-socket-connector-evidence.json")
$r77Tls = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r77-tls-boundary-evidence.json")
$r77Fix = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r77-fix-session-boundary-evidence.json")
$r77MarketData = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r77-marketdata-sanitized-result.json")
$r77Shutdown = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r77-shutdown-revert-evidence.json")
$r77Gate = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r77-gate-validation.json")
$completeness = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r78-r77-evidence-completeness-review.json")
$singleAttempt = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r78-single-attempt-validation.json")
$tcp = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r78-tcp-socket-success-validation.json")
$tls = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r78-tls-boundary-attempt-validation.json")
$fix = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r78-fix-not-attempted-review.json")
$progression = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r78-boundary-progression-review.json")
$sanitization = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r78-sanitization-review.json")
$protection = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r78-credential-endpoint-tls-fix-protection-review.json")
$instruments = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r78-approved-instrument-scope-review.json")
$usdJpy = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r78-usdjpy-caveat-preservation.json")
$apiWorker = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r78-api-worker-fake-gateway-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r78-forbidden-actions-audit.json")
$shutdown = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r78-shutdown-revert-review.json")
$readiness = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r78-controlled-readiness-gate.json")
$next = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r78-next-phase-recommendation.json")

if ($summary.phase -ne "LMAX-R78") { Fail "Summary phase mismatch." }
if ($allowedClassifications -notcontains $summary.classification) { Fail "Final classification absent or not allowed." }
if ($summary.classification -ne "LMAX_R78_PASS_RUNTIME_EVIDENCE_REVIEW_CONTROLLED_READINESS_GATE") { Fail "R78 did not pass the readiness gate." }
if ($summary.classification -ne $gate.classification) { Fail "Gate classification mismatch." }
if ($summary.newActivationPerformed -ne $false -or $summary.reviewOnly -ne $true) { Fail "R78 performed a new activation or was not review-only." }

if ($r77Summary.classification -ne "LMAX_R77_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED" -or $r77Boundary.classification -ne "LMAX_R77_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED") {
    Fail "R77 success classification is missing or mismatched."
}
if ($completeness.result -ne "PASS" -or $completeness.requiredArtifactsPresent -ne $true -or $completeness.internallyConsistent -ne $true) { Fail "R77 evidence incomplete." }
if ($r77Summary.attemptCount -ne 1 -or $r77Trace.attemptCount -ne 1 -or $singleAttempt.attemptCountExactlyOne -ne $true) { Fail "Attempt count is not exactly one." }
if ($r77Summary.manualCliSurfaceUsed -ne $true -or $r77Summary.adapterModeRealBoundedExecutableReadOnlyUsed -ne $true) { Fail "Manual CLI/real-bounded adapter evidence missing." }
if ($r77Endpoint.concreteDemoEndpointBindingUsed -ne $true -or $r77Endpoint.endpointMode -ne "Demo" -or $r77Endpoint.hostWasPlaceholder -ne $false -or $r77Endpoint.productionExcluded -ne $true -or $r77Endpoint.endpointApproved -ne $true) { Fail "Concrete Demo endpoint evidence missing or invalid." }
if ($r77Socket.configuredSocketConnectorUsed -ne $true -or $r77Socket.connectReached -ne $true) { Fail "Configured socket connector evidence missing." }
if ($r77Summary.tcpSocketSucceeded -ne $true -or $r77Boundary.boundaryStatuses.tcpSocket -ne "Succeeded" -or $tcp.tcpSocketSuccessProven -ne $true) { Fail "TCP/socket success is not proven." }
if ($r77Summary.tlsAttempted -ne $true -or $r77Boundary.boundaryStatuses.tls -ne "Attempted" -or $r77Tls.tlsAttempted -ne $true -or $tls.tlsAttemptProven -ne $true) { Fail "TLS attempt is not proven." }
if ($r77Summary.socketConnectorAuthenticateTlsReached -ne $true -or $r77Tls.socketConnectorAuthenticateTlsReached -ne $true -or $tls.socketConnectorAuthenticateTlsEvidenceProven -ne $true) { Fail "socketConnector.AuthenticateTls evidence missing." }
if ($r77Fix.fixLogonSessionAttempted -ne $false -or $r77Fix.fixBoundary -ne "NotAttempted" -or $r77Fix.fixBoundaryResultCategory -ne "NotAttemptedAfterTlsBoundaryAttempt" -or $fix.notAttemptedAfterTlsBoundaryAttemptRepresented -ne $true) { Fail "FIX NotAttemptedAfterTlsBoundaryAttempt is not represented." }
if ($r77Summary.marketDataRequestAttempted -ne $false -or $r77Trace.marketDataRequestSent -ne $false -or $r77MarketData.marketDataRequestAttempted -ne $false) { Fail "MarketDataRequest was attempted unexpectedly." }

if ($sanitization.result -ne "PASS" -or $sanitization.allOutputsSanitized -ne $true) { Fail "Sanitization review failed." }
if ($summary.credentialValuesReturned -ne $false -or $protection.credentialValuesReturned -ne $false -or $r77Summary.credentialValuesReturned -ne $false) { Fail "credentialValuesReturned is not false." }
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
if ($shutdown.result -ne "PASS" -or $shutdown.shutdownRevertCompleted -ne $true -or $r77Shutdown.shutdownRevertCompleted -ne $true) { Fail "Shutdown/revert evidence missing or incomplete." }
if ($r77Gate.validatorResult -ne "PASS" -or $gate.buildResult.status -ne "PASS" -or $gate.testResult.status -ne "PASS") { Fail "Build/test/validator evidence missing." }
if ($readiness.controlledReadinessDecision -ne "READY_FOR_TLS_TO_FIX_PROGRESSION_ROOT_CAUSE" -or $readiness.rootCauseReviewNeededBeforeNextActivation -ne $true -or $next.recommendedPhase -ne "LMAX-R79" -or $next.mustBeRootCauseReviewOnly -ne $true) {
    Fail "Next-phase recommendation is absent or invalid."
}

$r78Artifacts = Get-ChildItem -LiteralPath $ArtifactRoot -Filter "phase-lmax-r78-*" -File
$artifactText = ($r78Artifacts | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
$forbiddenPatterns = @("35=D", "35=F", "35=H", "35=AE", "554=", "BEGIN PRIVATE KEY", "password=", "secret=", "token=", "lmax.com", "fix-marketdata")
foreach ($pattern in $forbiddenPatterns) {
    if ($artifactText.IndexOf($pattern, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        Fail "R78 artifacts contain forbidden sensitive pattern: $pattern"
    }
}

Write-Host "LMAX_R78_VALIDATION_PASS"
