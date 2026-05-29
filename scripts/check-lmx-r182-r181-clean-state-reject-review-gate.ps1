$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'

function Read-Json($name) {
    $path = Join-Path $artifactRoot $name
    if (!(Test-Path $path)) { throw "Missing artifact: $name" }
    Get-Content $path -Raw | ConvertFrom-Json
}

function Assert-True($condition, $message) { if (-not $condition) { throw $message } }
function Assert-False($condition, $message) { if ($condition) { throw $message } }

$review = Read-Json 'phase-lmx-r182-r181-clean-state-reject.json'
$state = Read-Json 'phase-lmx-r182-r181-state-field-confirmation.json'
$boundary = Read-Json 'phase-lmx-r182-boundary-evidence-review.json'
$decision = Read-Json 'phase-lmx-r182-candidate-cause-decision.json'
$nextAction = Read-Json 'phase-lmx-r182-next-action-decision-gate.json'
$sanitization = Read-Json 'phase-lmx-r182-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmx-r182-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmx-r182-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmx-r182-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmx-r182-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r181-mdupdatetype-required-after-final-state-evidence-repair-activation.json',
    'phase-lmax-r181-state-propagation-evidence.json',
    'phase-lmax-r181-boundary-evidence.json',
    'phase-lmax-r181-marketdata-response-evidence.json',
    'phase-lmax-r181-gate-validation.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "R181 evidence missing: $name"
}

$r181 = Read-Json 'phase-lmax-r181-mdupdatetype-required-after-final-state-evidence-repair-activation.json'
$r181State = Read-Json 'phase-lmax-r181-state-propagation-evidence.json'
$r181Boundary = Read-Json 'phase-lmax-r181-boundary-evidence.json'
$r181Response = Read-Json 'phase-lmax-r181-marketdata-response-evidence.json'
$r181Gate = Read-Json 'phase-lmax-r181-gate-validation.json'

Assert-True ($r181.classification -eq 'LMAX_R181_FAIL_SESSIONREJECT_WITH_SANITIZED_REASON_REPORTED') 'R181 classification mismatch.'
Assert-True ($r181.externalActivationAttempted -eq $true) 'R181 external attempt missing.'
Assert-True ($r181.attemptCount -eq 1) 'R181 attemptCount must be 1.'
Assert-True ($r181.selectedProfile -eq 'UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument') 'R181 selected profile mismatch.'
Assert-True ($r181.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'R181 response category mismatch.'
Assert-True ($r181.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'R181 sanitized reason mismatch.'
Assert-True ($r181.sanitizedSessionRejectReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'R181 sanitized reason reporting mismatch.'
Assert-False $r181.entriesObserved 'R181 entries unexpectedly observed.'
Assert-True ($r181.entryCount -eq 0) 'R181 entry count must be 0.'
Assert-True ($r181.shutdownRevert -eq 'Succeeded') 'R181 shutdown/revert missing.'
Assert-True ($r181Gate.validatorResult -eq 'LMAX_R181_VALIDATION_PASS') 'R181 validator evidence missing.'

Assert-True ($r181State.marketDataRequestWriteAttempted -eq $true) 'R181 write attempted missing.'
Assert-True ($r181State.marketDataRequestWriteSucceeded -eq $true) 'R181 write succeeded missing.'
Assert-True ($r181State.marketDataRequestResponseReadAttempted -eq $true) 'R181 response read attempted missing.'
Assert-True ($r181State.marketDataRequestReachedBoundedResponseClassification -eq $true) 'R181 bounded classification missing.'
Assert-False $r181State.marketDataRequestSentLegacyFlag 'R181 legacy sent flag expected false compatibility marker.'
Assert-True ($r181State.classifiedMarketDataResponseObserved -eq $true) 'R181 classified response missing.'
Assert-True ($r181State.stateFieldsConsistentWithBoundaryEvidence -eq $true) 'R181 state fields inconsistent.'
Assert-False $r181State.stateFieldsContradictionDetected 'R181 state contradiction detected.'
Assert-True ($r181Boundary.fixAcknowledgement -eq 'FixLogonAcknowledged') 'R181 FIX acknowledgement missing.'
Assert-True ($r181Boundary.marketDataRequest -eq 'WriteSucceededAndReachedBoundedResponseClassificationAfterFixSuccess') 'R181 MarketDataRequest boundary missing.'
Assert-True ($r181Response.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'R181 response evidence category mismatch.'

Assert-True ($review.classification -eq 'LMAX_R182_PASS_CLEAN_STATE_REJECT_PERMISSION_SUPPORT_PACKAGE_RECOMMENDED_NO_EXTERNAL') 'R182 classification mismatch.'
Assert-True ($review.noExternal -eq $true) 'R182 must be no-external.'
Assert-False $review.newExternalActivationAttempted 'New external action detected in R182.'
Assert-False $review.runtimeActionPerformed 'Runtime action detected in R182.'
Assert-True ($review.r181EvidenceReviewed -eq $true) 'R181 evidence review missing.'
Assert-True ($review.r181AttemptCount -eq 1) 'R181 attempt count review invalid.'
Assert-True ($review.cleanStateEvidence -eq $true) 'Clean-state evidence missing.'
Assert-True ($review.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'R182 reviewed response category mismatch.'
Assert-True ($review.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'R182 reviewed sanitized reason mismatch.'

Assert-True ($state.stateFieldConfirmationPresent -eq $true) 'State field confirmation missing.'
Assert-True ($state.stateFieldPropagationEndToEndConfirmed -eq $true) 'End-to-end state propagation not confirmed.'
Assert-True ($state.marketDataRequestWriteAttempted -eq $true) 'R182 state write attempted missing.'
Assert-True ($state.marketDataRequestWriteSucceeded -eq $true) 'R182 state write succeeded missing.'
Assert-True ($state.marketDataRequestResponseReadAttempted -eq $true) 'R182 state response read missing.'
Assert-True ($state.marketDataRequestReachedBoundedResponseClassification -eq $true) 'R182 state classification missing.'
Assert-False $state.allExplicitStateFieldsFalse 'All explicit state fields are false.'
Assert-True ($state.stateFieldsConsistentWithBoundaryEvidence -eq $true) 'State fields inconsistent with boundary evidence.'
Assert-False $state.stateFieldsContradictionDetected 'State contradiction not resolved.'

Assert-True ($boundary.boundaryEvidenceReviewPresent -eq $true) 'Boundary evidence review missing.'
Assert-True ($boundary.boundarySequenceClean -eq $true) 'Boundary sequence not clean.'
Assert-True ($boundary.tcpSocket -eq 'Succeeded') 'TCP boundary not reviewed as succeeded.'
Assert-True ($boundary.tls -eq 'Succeeded') 'TLS boundary not reviewed as succeeded.'
Assert-True ($boundary.fixLogonSession -eq 'Succeeded') 'FIX boundary not reviewed as succeeded.'
Assert-True ($boundary.fixAcknowledgement -eq 'FixLogonAcknowledged') 'FIX acknowledgement not reviewed.'
Assert-True ($boundary.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'Boundary response category not reviewed.'

Assert-True ($decision.candidateCauseDecisionPresent -eq $true) 'Candidate cause decision missing.'
Assert-True ($decision.leadingCandidate -eq 'PermissionEntitlementOrExactLmaxSupportClarification') 'Leading candidate mismatch.'
Assert-True ($decision.permissionEntitlementSupport.status -eq 'Leading') 'Permission/support not leading.'
Assert-True ($decision.fieldOrderRepeatingGroup.status -eq 'PlausibleSecondary') 'Field-order decision missing.'
Assert-True ($decision.securityIdMapping.status -eq 'PlausibleSecondary') 'SecurityID mapping decision missing.'
Assert-True ($decision.sameProfileRetry.status -eq 'NotRecommended') 'Same-profile retry should not be recommended.'

Assert-True ($nextAction.nextRepairDecisionGatePresent -eq $true) 'Next repair decision gate missing.'
Assert-True ($nextAction.permissionSupportPackageRecommended -eq $true) 'Permission/support package not recommended.'
Assert-False $nextAction.liveRetryRecommended 'Live retry recommended too early.'
Assert-False $nextAction.sameProfileRetryReadinessRecommended 'Same-profile retry readiness recommended unexpectedly.'
Assert-True ($nextAction.recommendedNextPhase -eq 'LMAX-R183') 'Next action phase mismatch.'
Assert-True ($nextAction.r183MustRemainNoExternal -eq $true) 'R183 no-external requirement missing.'

Assert-True ($sanitization.audit -eq 'PASS') 'Sanitization audit did not pass.'
Assert-True ($sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned must be false.'
Assert-False $sanitization.rawFixSerialized 'Raw FIX serialized.'
Assert-False $sanitization.rawRejectTextSerialized 'Raw reject text serialized.'
Assert-False $sanitization.rawCredentialsSerialized 'Raw credentials serialized.'
Assert-False $sanitization.rawSessionIdentifiersSerialized 'Raw session IDs serialized.'
Assert-False $sanitization.rawCompIdsSerialized 'Raw CompIDs serialized.'
Assert-False $sanitization.rawEndpointSerialized 'Raw endpoint serialized.'
Assert-False $sanitization.rawTlsMaterialSerialized 'Raw TLS material serialized.'

Assert-True ($forbidden.audit -eq 'PASS') 'Forbidden action audit did not pass.'
Assert-False $forbidden.newExternalActivationAttempted 'New external activation attempted in R182.'
Assert-False $forbidden.socketOpened 'Socket opened in R182.'
Assert-False $forbidden.tlsOpened 'TLS opened in R182.'
Assert-False $forbidden.fixOpened 'FIX opened in R182.'
Assert-False $forbidden.marketDataRuntimeActionPerformed 'MarketData runtime action in R182.'
Assert-False $forbidden.apiStarted 'API started.'
Assert-False $forbidden.workerStarted 'Worker started.'
Assert-False $forbidden.ordersIntroduced 'Orders introduced.'
Assert-False $forbidden.tradingEnabled 'Trading enabled.'
Assert-False $forbidden.schedulerIntroduced 'Scheduler introduced.'
Assert-False $forbidden.pollingLoopIntroduced 'Polling introduced.'
Assert-False $forbidden.serviceIntroduced 'Service introduced.'
Assert-False $forbidden.replayIntroduced 'Replay introduced.'
Assert-False $forbidden.shadowReplayIntroduced 'Shadow replay introduced.'

Assert-True ($apiWorker.audit -eq 'PASS') 'API/Worker audit did not pass.'
Assert-True ($apiWorker.apiWorkerGatewayMode -eq 'FakeLmaxGatewayOnly') 'API/Worker FakeLmaxGatewayOnly regressed.'
Assert-False $apiWorker.apiStartupAttempted 'API startup attempted.'
Assert-False $apiWorker.workerStartupAttempted 'Worker startup attempted.'

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R183') 'Next phase recommendation missing.'
Assert-True ($next.r183MustRemainNoExternal -eq $true) 'R183 no-external requirement missing.'
Assert-False $next.liveRetryRecommendedNow 'Live retry recommended now.'
Assert-True ($next.supportOrDocsSpecificChangeRequiredBeforeAnotherLiveRetry -eq $true) 'Support/docs requirement before retry missing.'

Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R182_VALIDATION_PASS') 'Validator evidence missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmx-r182-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R182 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R182_VALIDATION_PASS'
