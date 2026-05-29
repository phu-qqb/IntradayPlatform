param(
    [string]$ArtifactRoot = "artifacts/readiness/lmax-runtime-enablement"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Error "LMAX_R106_VALIDATION_FAIL: $Message"
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

$summary = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r106-fix-session-boundary-root-cause-summary.json")
$r105Fix = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r105-fix-session-boundary-evidence.json")
$attemptSequence = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r106-r105-attempt-sequence-review.json")
$rootCause = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r106-fix-session-acknowledgement-root-cause.json")
$reader = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r106-fix-session-acknowledgement-reader-review.json")
$parser = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r106-fix-session-parser-classifier-review.json")
$tlsStream = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r106-tls-stream-to-fix-ack-reader-review.json")
$marketData = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r106-marketdata-block-after-fix-failure-review.json")
$noExternal = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r106-no-external-boundary-attempted.json")
$forbidden = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r106-forbidden-actions-audit.json")
$apiWorker = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r106-api-worker-fake-gateway-audit.json")
$scheduler = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r106-no-scheduler-polling-service-audit.json")
$sanitize = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r106-credential-endpoint-tls-fix-sanitization-validation.json")
$usdjpy = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r106-usdjpy-caveat-preservation.json")
$next = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r106-next-phase-recommendation.json")
$gate = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r106-gate-validation.json")

foreach ($required in @(
    "phase-lmax-r105-temporary-readonly-activation-retry-summary.json",
    "phase-lmax-r105-boundary-evidence.json",
    "phase-lmax-r105-fix-logon-frame-write-evidence.json",
    "phase-lmax-r105-operational-invocation-trace.json",
    "phase-lmax-r105-gate-validation.json",
    "phase-lmax-r106-fix-session-boundary-root-cause-report.md",
    "phase-lmax-r106-r105-fix-boundary-review.json",
    "phase-lmax-r106-real-bounded-path-validation.json"
)) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactRoot $required))) {
        Fail "Missing required R105/R106 artifact: $required"
    }
}

Assert-Eq $summary.classification "LMAX_R106_PASS_FIX_SESSION_ACKNOWLEDGEMENT_ROOT_CAUSE_IDENTIFIED_NO_EXTERNAL_ACTIVATION" "Unexpected R106 classification."
Assert-True $summary.r106ReviewOnly "R106 must be review-only."
Assert-False $summary.externalActivationAttempted "R106 must not perform activation."
Assert-False $summary.tcpAttempted "R106 must not attempt TCP."
Assert-False $summary.tlsAttempted "R106 must not attempt TLS."
Assert-False $summary.fixAttempted "R106 must not attempt FIX."
Assert-False $summary.marketDataRequestAttempted "R106 must not attempt MarketDataRequest."
Assert-True $summary.r105AttemptSequenceReviewed "R105 attempt sequence must be reviewed."
Assert-Eq $summary.r105ExternalAttemptCount 1 "R105 must have exactly one external attempt."
Assert-Eq $summary.r105PreExternalRejectedAttemptCount 0 "R105 pre-external rejection attempt count must be zero."
Assert-True $summary.r105TcpSuccessProven "R105 TCP success must be proven."
Assert-True $summary.r105TlsSuccessProven "R105 TLS success must be proven."
Assert-True $summary.r105FixBoundaryReachedProven "R105 FIX boundary reach must be proven."
Assert-True $summary.r105FixLogonFrameWriteProven "R105 FIX frame write must be proven."
Assert-Eq $summary.rootCauseCategory "FixSessionAcknowledgementNotImplemented" "Root cause category mismatch."
Assert-Eq $summary.responsibleClass "LmaxReadOnlyActivationManualFixLogonFrameWriter" "Responsible class missing."
Assert-Eq $summary.responsibleOperation "WriteLogonFrame" "Responsible operation missing."
Assert-False $summary.fixAcknowledgementReaderExistsInApprovedPath "Approved path must acknowledge missing ack reader."
Assert-False $summary.fixSessionParserClassifierExistsInApprovedPath "Approved path must acknowledge missing parser/classifier."
Assert-True $summary.tlsStreamAvailableToFutureAckReader "TLS stream availability must be reviewed."
Assert-True $summary.safeNextFixNeeded "Safe next fix must be needed."
Assert-True $summary.marketDataRequestBlockedAfterFixFailure "MarketData must remain blocked."
Assert-False $summary.credentialValuesReturned "credentialValuesReturned must remain false."

Assert-Eq $r105Fix.fixResultCategory "FixSessionAcknowledgementNotImplemented" "R105 FIX evidence must acknowledge FixSessionAcknowledgementNotImplemented."
Assert-True $r105Fix.fixAttempted "R105 FIX must have been attempted."
Assert-True $r105Fix.openFixSessionReached "R105 OpenFixSession must have been reached."
Assert-True $r105Fix.sessionLogonOnlyFixFrameWriterUsed "R105 session/logon-only writer evidence missing."
Assert-False $r105Fix.rawFixFrameSerializedInArtifacts "R105 raw FIX frame must not be serialized."
Assert-True $r105Fix.marketDataBlockedAfterFixFailure "R105 MarketData block missing."

Assert-True $attemptSequence.preExternalReservationRejectionReviewed "R105 pre-external sequence must be reviewed."
Assert-Eq $attemptSequence.preExternalReservationRejectionAttemptCount 0 "Pre-external rejection must have attempt count zero."
Assert-False $attemptSequence.preExternalReservationRejectionExternalBoundaryOpened "Pre-external rejection must not open boundary."
Assert-Eq $attemptSequence.approvedExternalAttemptCountAfterReservationFix 1 "Exactly one approved external attempt required."
Assert-True $attemptSequence.exactlyOneExternalBoundaryAttempt "R105 exactly-one attempt invariant missing."

Assert-Eq $rootCause.rootCauseCategory "FixSessionAcknowledgementNotImplemented" "Root cause artifact category mismatch."
Assert-Eq $rootCause.returnedByClass "LmaxReadOnlyActivationManualFixLogonFrameWriter" "Root cause class missing."
Assert-Eq $rootCause.returnedByOperation "WriteLogonFrame" "Root cause operation missing."
Assert-False $rootCause.acknowledgementReadImplementedInApprovedPath "Approved ack read must be identified missing."
Assert-False $rootCause.acknowledgementClassificationImplementedInApprovedPath "Approved ack classification must be identified missing."
Assert-True $rootCause.safeNextFixRequired "Safe next fix requirement missing."

Assert-False $reader.approvedManualPathHasAckReader "Reader review must identify missing approved reader."
Assert-True $reader.legacyLabReaderExists "Legacy/lab reader availability review missing."
Assert-False $reader.legacyLabReaderApprovedForManualBoundedPath "Legacy/lab reader must not be approved for this path."
Assert-False $reader.directReuseRecommended "Direct legacy/lab reuse must not be recommended."

Assert-False $parser.approvedPathSessionParserClassifierExists "Parser/classifier review must identify missing approved parser/classifier."
Assert-True $parser.sessionLevelOnly "Future parser/classifier must be session-level only."
Assert-False $parser.orderFramesSupported "Future parser/classifier must not support order frames."
Assert-False $parser.rawFixSerialized "Raw FIX must not be serialized."

Assert-True $tlsStream.r105TlsSucceeded "TLS stream review must preserve R105 TLS success."
Assert-True $tlsStream.r105TlsStreamAvailableForFix "TLS stream must be available for future ack reader."
Assert-False $tlsStream.currentAckReaderBoundToStream "Current ack reader binding should be missing."
Assert-False $tlsStream.rawTlsMaterialSerialized "Raw TLS material must not be serialized."

Assert-False $marketData.marketDataRequestAttempted "MarketDataRequest must remain not attempted."
Assert-True $marketData.marketDataBlockedAfterFixFailure "MarketData block after FIX failure must be preserved."

Assert-False $noExternal.activationPerformed "R106 activation must not be performed."
Assert-False $noExternal.externalActivationAttempted "R106 external boundary must not be attempted."
Assert-False $noExternal.tcpSocketAttempted "R106 TCP must not be attempted."
Assert-False $noExternal.tlsAttempted "R106 TLS must not be attempted."
Assert-False $noExternal.fixAttempted "R106 FIX must not be attempted."
Assert-False $noExternal.marketDataRequestAttempted "R106 MarketDataRequest must not be attempted."
Assert-Eq $noExternal.boundaryStatuses.'Credential/config' "ValidationOnly" "R106 credential boundary must be ValidationOnly."
Assert-Eq $noExternal.boundaryStatuses.'TCP/socket' "NotAttempted" "R106 TCP boundary mismatch."
Assert-Eq $noExternal.boundaryStatuses.TLS "NotAttempted" "R106 TLS boundary mismatch."
Assert-Eq $noExternal.boundaryStatuses.'FIX logon/session' "NotAttempted" "R106 FIX boundary mismatch."
Assert-Eq $noExternal.boundaryStatuses.MarketDataRequest "NotAttempted" "R106 MarketDataRequest boundary mismatch."

Assert-Eq $forbidden.auditResult "PASS" "Forbidden action audit must pass."
Assert-False $forbidden.ordersSubmitted "Orders must not be submitted."
Assert-False $forbidden.tradingEnabled "Trading must not be enabled."
Assert-False $forbidden.schedulerStarted "Scheduler must not start."
Assert-False $forbidden.pollingLoopStarted "Polling must not start."
Assert-False $forbidden.replayStarted "Replay must not start."
Assert-False $forbidden.shadowReplayStarted "Shadow replay must not start."

Assert-Eq $apiWorker.auditResult "PASS" "API/Worker audit must pass."
Assert-Eq $apiWorker.apiGateway "FakeLmaxGatewayOnly" "API gateway must remain fake-only."
Assert-Eq $apiWorker.workerGateway "FakeLmaxGatewayOnly" "Worker gateway must remain fake-only."
Assert-False $apiWorker.apiWorkerLivePathEnabled "API/Worker live path must not be enabled."

Assert-Eq $scheduler.auditResult "PASS" "Scheduler/polling/service audit must pass."
Assert-False $scheduler.hostedBackgroundServiceIntroduced "Hosted/background service must not be introduced."
Assert-False $scheduler.schedulerIntroduced "Scheduler must not be introduced."
Assert-False $scheduler.pollingIntroduced "Polling must not be introduced."
Assert-False $scheduler.replayIntroduced "Replay must not be introduced."
Assert-False $scheduler.shadowReplayIntroduced "Shadow replay must not be introduced."

Assert-False $sanitize.credentialValuesReturned "credentialValuesReturned must remain false."
Assert-False $sanitize.rawCredentialsSerialized "Raw credentials must not be serialized."
Assert-False $sanitize.rawEndpointSerialized "Raw endpoints must not be serialized."
Assert-False $sanitize.rawTlsMaterialSerialized "Raw TLS material must not be serialized."
Assert-False $sanitize.rawFixMessagesSerialized "Raw FIX messages must not be serialized."
Assert-False $sanitize.rawFixLogsSerialized "Raw FIX logs must not be serialized."
Assert-True $sanitize.sanitizedBooleanAndCategoryEvidenceOnly "Sanitized evidence rule missing."

Assert-Eq $usdjpy.securityId "4004" "USDJPY SecurityID mismatch."
Assert-Eq $usdjpy.securityIdSource "8" "USDJPY SecurityIDSource mismatch."
Assert-True $usdjpy.caveatPreserved "USDJPY caveat must be preserved."

Assert-Eq $next.nextRecommendedPhase "LMAX-R107" "Next phase recommendation must be R107."
Assert-False $next.activationAllowedInNextPhaseByDefault "R107 should default to no-external binding fix."

if ([string]$gate.buildResult -notlike "PASS*") {
    Fail "Build evidence missing or not passing."
}

if ([string]$gate.fullTestResult -notlike "PASS*") {
    Fail "Full test evidence missing or not passing."
}

$writerSource = Get-Content -LiteralPath "tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualFixLogonFrameWriter.cs" -Raw
if ($writerSource -notmatch "FixSessionAcknowledgementNotImplemented" -or $writerSource -notmatch "ManualFixLogonFrameWriteSucceededSanitized") {
    Fail "Responsible writer source does not contain the expected sanitized acknowledgement blocker."
}

$artifactText = Get-ChildItem -LiteralPath $ArtifactRoot -Filter "phase-lmax-r106-*" -File |
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
        Fail "Forbidden sensitive FIX or credential pattern appeared in R106 artifacts: $pattern"
    }
}

Write-Output "LMAX_R106_VALIDATION_PASS"
