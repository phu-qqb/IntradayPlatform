param(
    [string]$ArtifactRoot = "artifacts/readiness/lmax-runtime-enablement"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Error "LMAX_R105_VALIDATION_FAIL: $Message"
    exit 1
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing artifact: $Path"
    }

    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Assert-True($Value, [string]$Message) {
    if ($Value -ne $true) {
        Fail $Message
    }
}

function Assert-False($Value, [string]$Message) {
    if ($Value -ne $false) {
        Fail $Message
    }
}

function Assert-Eq($Actual, $Expected, [string]$Message) {
    if ([string]$Actual -ne [string]$Expected) {
        Fail "$Message Expected=[$Expected] Actual=[$Actual]"
    }
}

if (-not (Test-Path -LiteralPath $ArtifactRoot)) {
    Fail "Artifact root missing: $ArtifactRoot"
}

$summary = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r105-temporary-readonly-activation-retry-summary.json")
$preflight = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r105-preflight-result.json")
$endpoint = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r105-demo-endpoint-binding-evidence.json")
$socket = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r105-socket-connector-evidence.json")
$tls = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r105-tls-classification-evidence.json")
$fixCredential = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r105-fix-credential-material-evidence.json")
$fixWrite = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r105-fix-logon-frame-write-evidence.json")
$fixBoundary = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r105-fix-session-boundary-evidence.json")
$marketData = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r105-marketdata-request-evidence.json")
$boundary = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r105-boundary-evidence.json")
$forbidden = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r105-forbidden-actions-audit.json")
$apiWorker = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r105-api-worker-fake-gateway-audit.json")
$usdjpy = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r105-usdjpy-caveat-preservation.json")
$reservation = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r105-retry-phase-reservation-evidence.json")
$next = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r105-next-phase-recommendation.json")
$gate = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r105-gate-validation.json")

foreach ($required in @(
    "phase-lmax-r104-workspace-operator-approval-summary.json",
    "phase-lmax-r104-gate-validation.json",
    "phase-lmax-r105-operator-approval-note.md",
    "phase-lmax-r105-approval.txt",
    "phase-lmax-r105-expected-approval.txt",
    "phase-lmax-r105-temporary-readonly-activation-retry-report.md"
)) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactRoot $required))) {
        Fail "Missing required carryforward or approval artifact: $required"
    }
}

Assert-Eq $summary.classification "LMAX_R105_FAIL_FIX_LOGON_OR_SESSION_BOUNDARY" "Unexpected R105 classification."
Assert-True $summary.operatorApprovalMatched "Operator approval must match."
Assert-True $summary.externalActivationAttempted "R105 must record exactly one approved external activation attempt."
Assert-Eq $summary.attemptCount 1 "R105 attempt count must be exactly one."
Assert-True $summary.retryPhaseReservationPassed "R105 retry phase reservation must pass."
Assert-True $summary.manualCliUsed "Manual CLI must be used."
Assert-Eq $summary.adapterModeUsed "real-bounded-executable-readonly" "Wrong adapter mode."
Assert-True $summary.executeOnceInvoked "Execute-once path must be invoked."
Assert-True $summary.endpointApproved "Endpoint must be approved."
Assert-False $summary.credentialValuesReturned "credentialValuesReturned must remain false."
Assert-False $summary.sensitiveValuesPrintedStoredSerialized "Sensitive values must not be printed, stored, or serialized."

Assert-True $preflight.r104SuccessEvidencePresent "R104 success evidence must be present."
Assert-True $preflight.operatorApprovalMatched "R105 approval mismatch."
Assert-False $preflight.priorApprovalsReused "Prior approvals must not be reused."
Assert-True $preflight.retryPhaseReservationPassed "R105 reservation must be passed."
Assert-True $preflight.r101DecisionPhaseStillNotActivationReserved "R101 must not be activation-reserved."
Assert-True $preflight.r103DecisionPhaseStillNotActivationReserved "R103 must not be activation-reserved."

Assert-Eq $endpoint.endpointMode "Demo" "Endpoint mode must be Demo."
Assert-True $endpoint.endpointPresent "Endpoint must be present."
Assert-True $endpoint.hostPresent "Host presence must be represented only as sanitized boolean."
Assert-True $endpoint.hostConcreteBinding "Host must be concrete."
Assert-False $endpoint.hostWasPlaceholder "Host must not be placeholder."
Assert-True $endpoint.portPresent "Port presence must be represented only as sanitized boolean."
Assert-True $endpoint.portConcreteBinding "Port must be concrete."
Assert-True $endpoint.productionExcluded "Production must be excluded."
Assert-True $endpoint.endpointApproved "Endpoint must be approved."
Assert-False $endpoint.rawEndpointSerialized "Raw endpoint values must not be serialized."

Assert-True $socket.configuredSocketConnectorUsed "Configured socket connector must be used."
Assert-True $socket.connectReached "Connect must be reached."
Assert-True $socket.tcpConnectionAttempted "TCP must be attempted during the approved single attempt."
Assert-True $socket.tcpSocketSucceeded "TCP must succeed for this R105 evidence."

Assert-True $tls.tlsAttempted "TLS must be attempted after TCP success."
Assert-True $tls.tlsSucceeded "TLS must succeed in R105 evidence."
Assert-Eq $tls.tlsBoundaryStatus "Succeeded" "TLS boundary status mismatch."
Assert-Eq $tls.tlsResultCategory "Succeeded" "TLS result category mismatch."
Assert-Eq $tls.tlsFailureCategory "None" "TLS failure category must be None after success."
Assert-False $tls.tlsTimedOut "TLS timeout must be false."
Assert-Eq $tls.tlsExceptionCategory "None" "TLS exception category must be None after success."
Assert-True $tls.tlsStreamAvailableForFix "TLS stream must be available for FIX."
Assert-False $tls.tlsRawMaterialSerialized "Raw TLS material must not be serialized."

Assert-True $fixCredential.inMemoryOnlyFixCredentialMaterialAllowed "In-memory FIX credential material must be allowed for approved path."
Assert-True $fixCredential.inMemoryOnlyFixCredentialMaterialLoaded "In-memory FIX credential material must be loaded after TLS success."
Assert-False $fixCredential.credentialValuesReturned "FIX credential values must not be returned."
Assert-False $fixCredential.rawSecretSerialized "Raw secrets must not be serialized."

Assert-True $fixWrite.fixLogonBuilderReady "FIX Logon builder must be ready."
Assert-True $fixWrite.fixFrameWriterReady "FIX frame writer must be ready."
Assert-True $fixWrite.sessionLogonOnlyFixFrameWriterUsed "Session/logon-only writer must be used."
Assert-True $fixWrite.sessionOnly "Writer must be session-only."
Assert-False $fixWrite.orderFramesSupported "Order frames must not be supported."
Assert-False $fixWrite.newOrderSingleSupported "NewOrderSingle must not be supported."
Assert-False $fixWrite.cancelReplaceSupported "Cancel/replace must not be supported."
Assert-False $fixWrite.rawFixFrameSerializedInArtifacts "Raw FIX frame must not be serialized in artifacts."

Assert-True $fixBoundary.fixAttempted "FIX must be attempted after TLS success."
Assert-True $fixBoundary.openFixSessionReached "OpenFixSession must be reached."
Assert-True $fixBoundary.tlsSucceededBeforeFixAttempt "FIX must only follow TLS success."
Assert-Eq $fixBoundary.fixBoundaryStatus "FailedValidation" "FIX boundary status mismatch."
Assert-Eq $fixBoundary.fixResultCategory "FixSessionAcknowledgementNotImplemented" "FIX result category mismatch."
Assert-True $fixBoundary.marketDataBlockedAfterFixFailure "MarketData must remain blocked after FIX failure."
Assert-False $fixBoundary.rawFixFrameSerializedInArtifacts "Raw FIX frame must not be serialized."
Assert-False $fixBoundary.rawFixMessagesSerialized "Raw FIX messages must not be serialized."

Assert-False $marketData.marketDataRequestAttempted "MarketDataRequest must not be attempted after FIX failure."
Assert-Eq $marketData.marketDataRequestResultCategory "NotAttemptedAfterFixBoundaryFailure" "Unexpected MarketDataRequest category."
Assert-True $marketData.marketDataRequestBlockedUntilFixSessionSuccess "MarketDataRequest must remain blocked until FIX success."

Assert-Eq $boundary.boundaryStatuses.'Credential/config' "Succeeded" "Credential/config boundary mismatch."
Assert-Eq $boundary.boundaryStatuses.'TCP/socket' "Succeeded" "TCP boundary mismatch."
Assert-Eq $boundary.boundaryStatuses.TLS "Succeeded" "TLS boundary mismatch."
Assert-Eq $boundary.boundaryStatuses.'FIX logon/session' "FailedValidation" "FIX boundary mismatch."
Assert-Eq $boundary.boundaryStatuses.MarketDataRequest "NotAttempted" "MarketDataRequest boundary mismatch."
Assert-Eq $boundary.boundaryStatuses.'MarketDataResponse/entries' "NotAttempted" "MarketDataResponse boundary mismatch."
Assert-Eq $boundary.boundaryStatuses.'Shutdown/revert' "Succeeded" "Shutdown boundary mismatch."

Assert-Eq $forbidden.auditResult "PASS" "Forbidden action audit must pass."
Assert-False $forbidden.ordersSubmitted "Orders must not be submitted."
Assert-False $forbidden.tradingEnabled "Trading must not be enabled."
Assert-False $forbidden.schedulerStarted "Scheduler must not start."
Assert-False $forbidden.pollingLoopStarted "Polling must not start."
Assert-False $forbidden.replayStarted "Replay must not start."
Assert-False $forbidden.shadowReplayStarted "Shadow replay must not start."
Assert-True $forbidden.orderCapableFixFramePathAbsent "Order-capable FIX path must be absent."

Assert-Eq $apiWorker.auditResult "PASS" "API/Worker fake gateway audit must pass."
Assert-Eq $apiWorker.apiGateway "FakeLmaxGatewayOnly" "API gateway must remain fake-only."
Assert-Eq $apiWorker.workerGateway "FakeLmaxGatewayOnly" "Worker gateway must remain fake-only."
Assert-False $apiWorker.apiWorkerLivePathEnabled "API/Worker live path must not be enabled."

Assert-True $reservation.r105WorkspaceRetryPhaseReserved "R105 workspace retry phase must be reserved."
Assert-False $reservation.r101DecisionPhaseReservedForActivation "R101 must not be activation-reserved."
Assert-False $reservation.r103PreflightPhaseReservedForActivation "R103 must not be activation-reserved."
Assert-False $reservation.globalLiveDefaultEnabled "Global live default must remain disabled."

Assert-Eq $usdjpy.securityId "4004" "USDJPY SecurityID mismatch."
Assert-Eq $usdjpy.securityIdSource "8" "USDJPY SecurityIDSource mismatch."
Assert-True $usdjpy.caveatPreserved "USDJPY caveat must be preserved."

Assert-Eq $next.nextRecommendedPhase "LMAX-R106" "Next phase recommendation must be R106."
Assert-False $next.activationAllowedInNextPhase "R106 should be root-cause/review only."

Assert-Eq $gate.buildResult "PASS_EXISTING_NU1903_WARNINGS_ONLY" "Build evidence missing."
if ([string]$gate.fullTestResult -notlike "PASS*") {
    Fail "Full test evidence missing or not passing."
}

$artifactText = Get-ChildItem -LiteralPath $ArtifactRoot -Filter "phase-lmax-r105-*" -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }

$forbiddenPatterns = @(
    "8=FIX",
    "35=A",
    "553=",
    "554=",
    "Username=",
    "Password=",
    "BeginString=",
    "SenderCompID=",
    "TargetCompID="
)

foreach ($pattern in $forbiddenPatterns) {
    if ($artifactText -match [regex]::Escape($pattern)) {
        Fail "Forbidden sensitive or trading pattern appeared in R105 artifacts: $pattern"
    }
}

$reservationSource = Get-Content -LiteralPath "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxApprovedBoundedExecutableRetryPhaseReservations.cs" -Raw
if ($reservationSource -notmatch "WorkspaceApprovedRetryPhases" -or $reservationSource -notmatch "LMAX-R105") {
    Fail "R105 narrow workspace retry reservation is not present in source."
}

Write-Output "LMAX_R105_VALIDATION_PASS"
