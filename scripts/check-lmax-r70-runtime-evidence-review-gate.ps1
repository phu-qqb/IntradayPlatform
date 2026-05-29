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
    "phase-lmax-r70-runtime-evidence-review-report.md",
    "phase-lmax-r70-runtime-evidence-review-summary.json",
    "phase-lmax-r70-r69-evidence-completeness-review.json",
    "phase-lmax-r70-tcp-socket-success-validation.json",
    "phase-lmax-r70-single-attempt-validation.json",
    "phase-lmax-r70-boundary-progression-review.json",
    "phase-lmax-r70-sanitization-review.json",
    "phase-lmax-r70-credential-endpoint-protection-review.json",
    "phase-lmax-r70-approved-instrument-scope-review.json",
    "phase-lmax-r70-usdjpy-caveat-preservation.json",
    "phase-lmax-r70-api-worker-fake-gateway-audit.json",
    "phase-lmax-r70-forbidden-actions-audit.json",
    "phase-lmax-r70-shutdown-revert-review.json",
    "phase-lmax-r70-controlled-readiness-gate.json",
    "phase-lmax-r70-next-phase-recommendation.json",
    "phase-lmax-r70-gate-validation.json"
)

foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $name))) {
        Fail "Missing required R70 artifact: $name"
    }
}

$allowed = @(
    "LMAX_R70_PASS_RUNTIME_EVIDENCE_REVIEW_CONTROLLED_READINESS_GATE",
    "LMAX_R70_FAIL_R69_EVIDENCE_INCOMPLETE",
    "LMAX_R70_FAIL_TCP_SOCKET_SUCCESS_NOT_PROVABLE",
    "LMAX_R70_FAIL_ATTEMPT_COUNT_INVALID",
    "LMAX_R70_FAIL_SANITIZATION_RISK",
    "LMAX_R70_FAIL_CREDENTIAL_OR_ENDPOINT_PROTECTION_RISK",
    "LMAX_R70_FAIL_API_WORKER_GATEWAY_REGRESSION",
    "LMAX_R70_FAIL_FORBIDDEN_ACTION_EVIDENCE",
    "LMAX_R70_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R70_FAIL_SHUTDOWN_REVERT_INCOMPLETE",
    "LMAX_R70_FAIL_BUILD_OR_TESTS"
)

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r70-runtime-evidence-review-summary.json")
$completeness = Read-Json (Join-Path $artifactRoot "phase-lmax-r70-r69-evidence-completeness-review.json")
$tcp = Read-Json (Join-Path $artifactRoot "phase-lmax-r70-tcp-socket-success-validation.json")
$attempt = Read-Json (Join-Path $artifactRoot "phase-lmax-r70-single-attempt-validation.json")
$boundary = Read-Json (Join-Path $artifactRoot "phase-lmax-r70-boundary-progression-review.json")
$sanitization = Read-Json (Join-Path $artifactRoot "phase-lmax-r70-sanitization-review.json")
$protection = Read-Json (Join-Path $artifactRoot "phase-lmax-r70-credential-endpoint-protection-review.json")
$instruments = Read-Json (Join-Path $artifactRoot "phase-lmax-r70-approved-instrument-scope-review.json")
$usdJpy = Read-Json (Join-Path $artifactRoot "phase-lmax-r70-usdjpy-caveat-preservation.json")
$apiWorker = Read-Json (Join-Path $artifactRoot "phase-lmax-r70-api-worker-fake-gateway-audit.json")
$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r70-forbidden-actions-audit.json")
$shutdown = Read-Json (Join-Path $artifactRoot "phase-lmax-r70-shutdown-revert-review.json")
$readiness = Read-Json (Join-Path $artifactRoot "phase-lmax-r70-controlled-readiness-gate.json")
$next = Read-Json (Join-Path $artifactRoot "phase-lmax-r70-next-phase-recommendation.json")
$gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r70-gate-validation.json")
$r69Summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r69-temporary-readonly-activation-retry-summary.json")
$r69Boundary = Read-Json (Join-Path $artifactRoot "phase-lmax-r69-boundary-evidence.json")
$r69Gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r69-gate-validation.json")

if ($allowed -notcontains $summary.classification -or $allowed -notcontains $gate.classification -or $allowed -notcontains $readiness.classification) {
    Fail "R70 classification is absent or not allowed."
}

if ($summary.classification -ne "LMAX_R70_PASS_RUNTIME_EVIDENCE_REVIEW_CONTROLLED_READINESS_GATE" -or $gate.classification -ne $summary.classification) {
    Fail "Unexpected R70 classification: $($summary.classification)"
}

if ($r69Summary.classification -ne "LMAX_R69_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED" -or $r69Gate.classification -ne $r69Summary.classification -or $r69Gate.validatorResult -ne "PASS") {
    Fail "R69 success classification is missing or mismatched."
}

if (-not $completeness.evidenceComplete -or -not $completeness.requiredArtifactsPresent -or -not $summary.r69EvidenceComplete -or -not $readiness.r69EvidenceComplete) {
    Fail "R69 evidence completeness review failed."
}

if (-not $tcp.tcpSocketSuccessProven -or $tcp.tcpSocketBoundary -ne "Succeeded" -or -not $tcp.realSocketOpened -or -not $tcp.configuredSocketConnectorUsed -or -not $tcp.connectReached -or -not $tcp.concreteDemoEndpointBindingUsed -or $tcp.hostWasPlaceholder) {
    Fail "TCP/socket success is not proven."
}

if ($r69Boundary.boundaryStatuses.tcpSocket -ne "Succeeded" -or -not $r69Boundary.realSocketOpened) {
    Fail "R69 TCP/socket success evidence is missing."
}

if (-not $attempt.attemptCountValid -or $attempt.attemptCount -ne 1 -or $r69Summary.attemptCount -ne 1 -or $r69Gate.attemptCount -ne 1) {
    Fail "Attempt count is invalid."
}

if ($attempt.retryLoopUsed -or $attempt.pollingLoopUsed -or $attempt.schedulerUsed) {
    Fail "Retry/polling/scheduler evidence invalid."
}

if ($boundary.credentialConfig -ne "ValidationOnly" -or $boundary.tcpSocket -ne "Succeeded" -or $boundary.tls -ne "NotAttempted" -or $boundary.fixLogonSession -ne "NotAttempted" -or $boundary.marketDataRequest -ne "NotAttempted" -or $boundary.marketDataResponseEntries -ne "NotAttempted" -or $boundary.shutdownRevert -ne "Succeeded") {
    Fail "Boundary progression review is invalid."
}

if ($r69Boundary.boundaryStatuses.tls -ne "NotAttempted" -or $r69Boundary.boundaryStatuses.fixLogonSession -ne "NotAttempted" -or $r69Boundary.boundaryStatuses.marketDataRequest -ne "NotAttempted" -or $r69Boundary.boundaryStatuses.marketDataResponseEntries -ne "NotAttempted") {
    Fail "R69 unexpectedly attempted TLS/FIX/MarketDataRequest."
}

if (-not $sanitization.sanitizationPassed -or $sanitization.rawCredentialsInArtifacts -or $sanitization.rawSensitiveFixLogsInArtifacts -or $sanitization.rawEndpointValuesSerialized -or $sanitization.credentialValuesReturned) {
    Fail "Sanitization review failed."
}

if (-not $protection.credentialEndpointProtectionPassed -or $protection.credentialValuesReturned -or $protection.credentialValuesPrinted -or $protection.credentialValuesStored -or $protection.credentialValuesSerialized -or $protection.endpointValuesPrinted -or $protection.endpointValuesStored -or $protection.endpointValuesSerialized) {
    Fail "Credential/endpoint protection review failed."
}

if ($protection.endpointMode -ne "Demo" -or -not $protection.endpointPresent -or -not $protection.hostPresent -or -not $protection.hostConcreteBinding -or $protection.hostWasPlaceholder -or -not $protection.portPresent -or -not $protection.portConcreteBinding -or -not $protection.productionExcluded -or -not $protection.endpointApproved) {
    Fail "Endpoint sanitized evidence is invalid."
}

$expectedInstruments = @("GBPUSD", "EURGBP", "AUDUSD", "USDJPY")
$actualInstruments = @($instruments.approvedInstruments)
if (-not $instruments.approvedInstrumentScopeValid -or $instruments.unapprovedInstrumentObserved -or $actualInstruments.Count -ne 4) {
    Fail "Approved instrument scope review failed."
}
foreach ($instrument in $expectedInstruments) {
    if ($actualInstruments -notcontains $instrument) {
        Fail "Approved instrument missing: $instrument"
    }
}

if (-not $usdJpy.caveatPreserved -or $usdJpy.securityId -ne "4004" -or $usdJpy.securityIdSource -ne "8" -or $usdJpy.caveat -ne "prior failed-safe root cause remains unproven") {
    Fail "USDJPY caveat is missing or weakened."
}

if ($apiWorker.result -ne "PASS" -or $apiWorker.apiWorkerGatewayMode -ne "FakeLmaxGatewayOnly" -or -not $apiWorker.apiWorkerUntouched -or $apiWorker.manualExecutionSurfaceRegisteredInApi -or $apiWorker.manualExecutionSurfaceRegisteredInWorker -or $apiWorker.realBoundedAdapterRegisteredInApi -or $apiWorker.realBoundedAdapterRegisteredInWorker -or $apiWorker.demoEndpointBindingRegisteredInApi -or $apiWorker.demoEndpointBindingRegisteredInWorker -or $apiWorker.socketConnectorRegisteredInApi -or $apiWorker.socketConnectorRegisteredInWorker -or $apiWorker.appsettingsLiveEnablementIntroduced -or $apiWorker.defaultRuntimeGatewayChanged) {
    Fail "API/Worker FakeLmaxGatewayOnly audit failed."
}

if ($forbidden.result -ne "PASS") {
    Fail "Forbidden-actions audit failed."
}

foreach ($flag in @("externalActivationAttemptedDuringR70", "socketOpenedDuringR70", "tlsAttemptedDuringR70", "fixLogonAttemptedDuringR70", "marketDataRequestAttemptedDuringR70", "ordersSubmitted", "newOrderSingleSent", "cancelReplaceSent", "tradingEnablementExecuted", "tradingStateMutated", "productionAccountUsed", "productionAccountAllowed", "apiStarted", "workerStarted", "hostedServiceStarted", "backgroundServiceStarted", "schedulerStarted", "pollingLoopStarted", "replayExecuted", "shadowReplayExecuted", "shadowReplaySubmitted", "credentialValuesReturned", "credentialEndpointValuesPrintedStoredSerialized", "rawSensitiveFixLogsStored", "rawEndpointValuesSerialized", "orderTradingPathTouched")) {
    if ($forbidden.$flag) {
        Fail "Forbidden R70 action flag is true: $flag"
    }
}

if (-not $shutdown.shutdownRevertComplete -or $shutdown.r69ShutdownRevert -ne "Succeeded" -or -not $shutdown.shutdownRevertEvidencePresent) {
    Fail "Shutdown/revert review is incomplete."
}

if ($readiness.controlledReadinessDecision -ne "READY_FOR_NEXT_OPERATOR_APPROVED_TLS_BOUNDARY_RETRY" -or -not $readiness.nextActivationRequiresSeparateOperatorApproval) {
    Fail "Controlled readiness decision is missing or unsafe."
}

if ($next.recommendedNextPhase -ne "LMAX-R71" -or [string]::IsNullOrWhiteSpace($next.title)) {
    Fail "Next-phase recommendation is absent."
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

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r70-*" |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$r69ArtifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r69-*" |
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
    if ($artifactText -match $pattern -or $r69ArtifactText -match $pattern) {
        Fail "Potential raw endpoint, credential, or sensitive FIX material found in reviewed artifacts: $pattern"
    }
}

if ($gate.buildResult.status -ne "PASS" -or $gate.testResult.status -ne "PASS") {
    Fail "Build/test evidence is missing or not PASS."
}

Write-Host "LMAX R70 runtime evidence review gate validation PASS"
