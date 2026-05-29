param(
    [string]$ArtifactRoot = "artifacts/readiness/lmax-runtime-enablement"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Error "LMAX_R107_VALIDATION_FAIL: $Message"
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

$summary = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r107-fix-session-acknowledgement-reader-binding-summary.json")
$beforeAfter = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r107-r106-root-cause-before-after-classification.json")
$reader = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r107-fix-acknowledgement-reader-validation.json")
$parser = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r107-fix-session-parser-classifier-validation.json")
$sessionSafety = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r107-session-only-fix-ack-safety-validation.json")
$orderExclusion = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r107-order-message-exclusion-validation.json")
$rawFix = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r107-raw-fix-sanitization-validation.json")
$marketData = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r107-marketdata-block-until-fix-session-success-validation.json")
$realBounded = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r107-real-bounded-path-validation.json")
$noExternal = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r107-no-external-boundary-attempted.json")
$forbidden = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r107-forbidden-actions-audit.json")
$apiWorker = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r107-api-worker-fake-gateway-audit.json")
$scheduler = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r107-no-scheduler-polling-service-audit.json")
$sanitize = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r107-credential-endpoint-tls-fix-sanitization-validation.json")
$usdjpy = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r107-usdjpy-caveat-preservation.json")
$next = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r107-next-phase-recommendation.json")
$gate = Read-Json (Join-Path $ArtifactRoot "phase-lmax-r107-gate-validation.json")

foreach ($required in @(
    "phase-lmax-r106-fix-session-boundary-root-cause-summary.json",
    "phase-lmax-r106-fix-session-acknowledgement-root-cause.json",
    "phase-lmax-r107-fix-session-acknowledgement-reader-binding-fix-report.md"
)) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactRoot $required))) {
        Fail "Missing required R106/R107 artifact: $required"
    }
}

Assert-Eq $summary.classification "LMAX_R107_PASS_FIX_SESSION_ACKNOWLEDGEMENT_READER_BINDING_READY_NO_EXTERNAL_ACTIVATION" "Unexpected R107 classification."
Assert-True $summary.fixSessionAcknowledgementNotImplementedClearedForFuturePath "FixSessionAcknowledgementNotImplemented must be cleared for future path."
Assert-True $summary.approvedFixAcknowledgementReaderReady "Approved FIX acknowledgement reader must be ready."
Assert-True $summary.approvedFixSessionParserClassifierReady "Approved FIX parser/classifier must be ready."
Assert-True $summary.readerParserSessionLevelOnly "Reader/parser must be session-level only."
Assert-True $summary.syntheticLogonAcknowledgementClassifiesAsSessionSuccess "Synthetic Logon acknowledgement success classification missing."
Assert-True $summary.nonSuccessSessionFramesClassifySafely "Non-success session classification missing."
Assert-False $summary.orderMessagesSupported "Order messages must not be supported."
Assert-False $summary.executionReportsSupported "Execution reports must not be supported."
Assert-False $summary.fillsSupported "Fills must not be supported."
Assert-False $summary.orderLifecycleSupported "Order lifecycle must not be supported."
Assert-False $summary.newOrderSingleSupported "NewOrderSingle must not be supported."
Assert-False $summary.cancelReplaceSupported "Cancel/replace must not be supported."
Assert-False $summary.tradingMutationSupported "Trading mutation must not be supported."
Assert-False $summary.rawFixMessagesPrintedStoredSerialized "Raw FIX must not be printed, stored, or serialized."
Assert-False $summary.credentialValuesReturned "credentialValuesReturned must remain false."
Assert-True $summary.marketDataRequestBlockedUntilFixSessionSuccess "MarketDataRequest must remain blocked until FIX session success."
Assert-False $summary.externalActivationAttempted "R107 must not perform activation."
Assert-False $summary.tcpAttempted "R107 must not attempt TCP."
Assert-False $summary.tlsAttempted "R107 must not attempt TLS."
Assert-False $summary.fixAttempted "R107 must not attempt FIX."
Assert-False $summary.marketDataRequestAttempted "R107 must not attempt MarketDataRequest."

Assert-True $beforeAfter.afterR107BlockerClearedForFutureApprovedPath "Before/after blocker must be cleared."
Assert-False $beforeAfter.runtimeActivationAttemptedDuringR107 "R107 must not activate."
Assert-True $beforeAfter.freshApprovalRequiredForActivationRetry "Future activation must require fresh approval."

Assert-True $reader.approvedFixAcknowledgementReaderReady "Reader validation missing."
Assert-Eq $reader.readerBindingName "LmaxReadOnlyActivationManualFixSessionAcknowledgementReader" "Wrong reader binding."
Assert-False $reader.externalBoundaryAttemptedDuringValidation "Reader validation must be no-external."
Assert-False $reader.readsLiveStreamDuringR107 "R107 must not read live stream."
Assert-True $reader.testableWithInMemorySyntheticFramesOnly "Reader tests must be in-memory only."
Assert-False $reader.rawFixSerialized "Reader must not serialize raw FIX."
Assert-False $reader.credentialValuesReturned "Reader must not return credentials."

Assert-True $parser.approvedFixSessionParserClassifierReady "Parser/classifier validation missing."
Assert-True $parser.sessionLevelOnly "Parser/classifier must be session-level only."
Assert-True $parser.successRequiresValidSessionLevelLogonAcknowledgement "Success must require valid acknowledgement."
Assert-False $parser.successCanBeFakedWithoutAcknowledgement "FIX success must not be fakeable."
Assert-True $parser.nonSuccessSessionFramesClassifySafely "Non-success classifier missing."
Assert-False $parser.rawFixSerialized "Parser/classifier must not serialize raw FIX."

Assert-True $sessionSafety.readerParserSessionLevelOnly "Session safety missing."
Assert-False $sessionSafety.orderMessagesSupported "Order messages must be unsupported."
Assert-False $sessionSafety.executionReportsSupported "Execution reports must be unsupported."
Assert-False $sessionSafety.fillsSupported "Fills must be unsupported."
Assert-False $sessionSafety.orderLifecycleSupported "Order lifecycle must be unsupported."
Assert-False $sessionSafety.tradingMutationSupported "Trading mutation must be unsupported."
Assert-False $sessionSafety.marketDataFramesParsedByAckReader "Ack reader must not parse market-data frames."

Assert-False $orderExclusion.orderMessagesSupported "Order exclusion failed."
Assert-False $orderExclusion.newOrderSingleSupported "NewOrderSingle support introduced."
Assert-False $orderExclusion.cancelReplaceSupported "Cancel/replace support introduced."
Assert-False $orderExclusion.executionReportsSupported "Execution report support introduced."
Assert-False $orderExclusion.fillsSupported "Fill support introduced."
Assert-False $orderExclusion.orderLifecycleSupported "Order lifecycle support introduced."
Assert-True $orderExclusion.approvedAckReaderRejectsNonSessionOperationalMessages "Non-session operational rejection missing."

Assert-False $rawFix.rawFixMessagesPrintedStoredSerialized "Raw FIX messages must not be printed/stored/serialized."
Assert-False $rawFix.rawFixFramesInArtifacts "Raw FIX frames must not be in artifacts."
Assert-False $rawFix.rawFixFieldsPrinted "Raw FIX fields must not be printed."
Assert-False $rawFix.sensitiveFixLogsStored "Sensitive FIX logs must not be stored."
Assert-False $rawFix.syntheticFixtureFramesSerializedToArtifacts "Synthetic raw fixtures must not be serialized to artifacts."

Assert-True $marketData.marketDataRequestBlockedUntilFixSessionSuccess "MarketData block missing."
Assert-False $marketData.marketDataRequestCanRunWithoutFixSessionSuccess "MarketData must not run without FIX success."
Assert-True $marketData.fixSessionSuccessRequiresAcknowledgement "FIX success must require acknowledgement."
Assert-False $marketData.r107MarketDataRequestAttempted "R107 MarketDataRequest must not be attempted."

Assert-True $realBounded.ackReaderBoundOnlyInManualRealBoundedPath "Ack reader must be manual real-bounded only."
Assert-False $realBounded.apiWorkerReachable "Ack reader must not be API/Worker reachable."
Assert-False $realBounded.globalDefaultEnabled "Global default live enablement must remain false."
Assert-True $realBounded.noExternalDefaultPreserved "No-external default must be preserved."
Assert-True $realBounded.productionExcluded "Production must be excluded."

Assert-False $noExternal.activationPerformed "R107 activation must not be performed."
Assert-False $noExternal.externalActivationAttempted "R107 external boundary must not be attempted."
Assert-False $noExternal.tcpSocketAttempted "R107 TCP must not be attempted."
Assert-False $noExternal.tlsAttempted "R107 TLS must not be attempted."
Assert-False $noExternal.liveFixFrameWritten "R107 must not write live FIX frames."
Assert-False $noExternal.fixLogonAttempted "R107 FIX logon must not be attempted."
Assert-False $noExternal.marketDataRequestAttempted "R107 MarketDataRequest must not be attempted."
Assert-Eq $noExternal.boundaryStatuses.'Credential/config' "ValidationOnly" "R107 credential/config boundary mismatch."
Assert-Eq $noExternal.boundaryStatuses.'TCP/socket' "NotAttempted" "R107 TCP boundary mismatch."
Assert-Eq $noExternal.boundaryStatuses.TLS "NotAttempted" "R107 TLS boundary mismatch."
Assert-Eq $noExternal.boundaryStatuses.'FIX logon/session' "ValidationOnly" "R107 FIX boundary mismatch."
Assert-Eq $noExternal.boundaryStatuses.MarketDataRequest "NotAttempted" "R107 MarketDataRequest boundary mismatch."

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
Assert-False $scheduler.hostedBackgroundServiceIntroduced "Hosted service must not be introduced."
Assert-False $scheduler.schedulerIntroduced "Scheduler must not be introduced."
Assert-False $scheduler.pollingIntroduced "Polling must not be introduced."
Assert-False $scheduler.replayIntroduced "Replay must not be introduced."
Assert-False $scheduler.shadowReplayIntroduced "Shadow replay must not be introduced."

Assert-False $sanitize.credentialValuesReturned "credentialValuesReturned must remain false."
Assert-False $sanitize.rawCredentialsSerialized "Raw credentials must not be serialized."
Assert-False $sanitize.rawEndpointSerialized "Raw endpoint must not be serialized."
Assert-False $sanitize.rawTlsMaterialSerialized "Raw TLS material must not be serialized."
Assert-False $sanitize.rawFixMessagesSerialized "Raw FIX messages must not be serialized."
Assert-False $sanitize.rawFixLogsSerialized "Raw FIX logs must not be serialized."
Assert-True $sanitize.sanitizedBooleanAndCategoryEvidenceOnly "Sanitized evidence requirement missing."

Assert-Eq $usdjpy.securityId "4004" "USDJPY SecurityID mismatch."
Assert-Eq $usdjpy.securityIdSource "8" "USDJPY SecurityIDSource mismatch."
Assert-True $usdjpy.caveatPreserved "USDJPY caveat must be preserved."

Assert-Eq $next.nextRecommendedPhase "LMAX-R109" "Next phase recommendation must be R109."
Assert-True $next.useR109NotR108 "Activation retry phase must be odd."
Assert-True $next.freshApprovalRequiredForAnyFutureActivation "Fresh approval must be required."

if ([string]$gate.buildResult -notlike "PASS*") {
    Fail "Build evidence missing or not passing."
}

if ([string]$gate.focusedTestResult -notlike "PASS*") {
    Fail "Focused test evidence missing or not passing."
}

if ([string]$gate.fullTestResult -notlike "PASS*") {
    Fail "Full test evidence missing or not passing."
}

$writerSource = Get-Content -LiteralPath "tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualFixLogonFrameWriter.cs" -Raw
if ($writerSource -match "FixSessionAcknowledgementNotImplemented") {
    Fail "FixSessionAcknowledgementNotImplemented remains in the future approved manual writer path."
}

foreach ($requiredSource in @(
    "LmaxReadOnlyActivationManualFixSessionAcknowledgementReader",
    "FixLogonAcknowledged",
    "FixReadTimeout",
    "FixReadRemoteClosed",
    "OrderMessagesSupported: false",
    "ExecutionReportsSupported: false",
    "FillsSupported: false",
    "OrderLifecycleSupported: false"
)) {
    if ($writerSource -notmatch [regex]::Escape($requiredSource)) {
        Fail "Required reader binding source evidence missing: $requiredSource"
    }
}

$apiSource = Get-Content -LiteralPath "src/QQ.Production.Intraday.Api/Program.cs" -Raw
$workerSource = Get-Content -LiteralPath "src/QQ.Production.Intraday.Worker/Program.cs" -Raw
if ($apiSource -match "LmaxReadOnlyActivationManualFixSessionAcknowledgementReader" -or
    $workerSource -match "LmaxReadOnlyActivationManualFixSessionAcknowledgementReader") {
    Fail "Ack reader must not be reachable from API/Worker startup."
}

$artifactText = Get-ChildItem -LiteralPath $ArtifactRoot -Filter "phase-lmax-r107-*" -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }

$forbiddenPatterns = @(
    "8=FIX",
    "35=",
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
        Fail "Forbidden sensitive FIX or credential pattern appeared in R107 artifacts: $pattern"
    }
}

Write-Output "LMAX_R107_VALIDATION_PASS"
