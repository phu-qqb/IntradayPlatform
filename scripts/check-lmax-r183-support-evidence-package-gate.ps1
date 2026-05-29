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

$package = Read-Json 'phase-lmax-r183-support-evidence-package.json'
$r181Summary = Read-Json 'phase-lmax-r183-r181-clean-state-evidence-summary.json'
$r182Summary = Read-Json 'phase-lmax-r183-r182-decision-summary.json'
$eliminated = Read-Json 'phase-lmax-r183-eliminated-hypotheses-after-clean-state-reject.json'
$remaining = Read-Json 'phase-lmax-r183-remaining-candidate-causes.json'
$permission = Read-Json 'phase-lmax-r183-permission-entitlement-assessment.json'
$fieldOrder = Read-Json 'phase-lmax-r183-field-order-secondary-assessment.json'
$securityId = Read-Json 'phase-lmax-r183-securityid-mapping-secondary-assessment.json'
$retryGate = Read-Json 'phase-lmax-r183-retry-blocked-until-clarification-gate.json'
$sanitization = Read-Json 'phase-lmax-r183-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r183-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r183-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r183-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r183-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r181-mdupdatetype-required-after-final-state-evidence-repair-activation.json',
    'phase-lmax-r181-state-propagation-evidence.json',
    'phase-lmax-r181-boundary-evidence.json',
    'phase-lmax-r181-marketdata-response-evidence.json',
    'phase-lmax-r181-gate-validation.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "R181 clean-state evidence missing: $name"
}

foreach ($name in @(
    'phase-lmx-r182-r181-clean-state-reject.json',
    'phase-lmx-r182-candidate-cause-decision.json',
    'phase-lmx-r182-next-action-decision-gate.json',
    'phase-lmx-r182-gate-validation.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "R182 decision evidence missing or phase-lmx prefix not accepted: $name"
}

$r181 = Read-Json 'phase-lmax-r181-mdupdatetype-required-after-final-state-evidence-repair-activation.json'
$r181State = Read-Json 'phase-lmax-r181-state-propagation-evidence.json'
$r181Boundary = Read-Json 'phase-lmax-r181-boundary-evidence.json'
$r181Response = Read-Json 'phase-lmax-r181-marketdata-response-evidence.json'
$r181Gate = Read-Json 'phase-lmax-r181-gate-validation.json'
$r182 = Read-Json 'phase-lmx-r182-r181-clean-state-reject.json'
$r182Decision = Read-Json 'phase-lmx-r182-candidate-cause-decision.json'
$r182Next = Read-Json 'phase-lmx-r182-next-action-decision-gate.json'
$r182Gate = Read-Json 'phase-lmx-r182-gate-validation.json'

Assert-True ($r181.classification -eq 'LMAX_R181_FAIL_SESSIONREJECT_WITH_SANITIZED_REASON_REPORTED') 'R181 classification mismatch.'
Assert-True ($r181.attemptCount -eq 1) 'R181 attemptCount must be 1.'
Assert-True ($r181.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'R181 response category mismatch.'
Assert-True ($r181.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'R181 sanitized reason mismatch.'
Assert-True ($r181State.marketDataRequestWriteAttempted -eq $true) 'R181 write attempted missing.'
Assert-True ($r181State.marketDataRequestWriteSucceeded -eq $true) 'R181 write succeeded missing.'
Assert-True ($r181State.marketDataRequestResponseReadAttempted -eq $true) 'R181 response read attempted missing.'
Assert-True ($r181State.marketDataRequestReachedBoundedResponseClassification -eq $true) 'R181 bounded classification missing.'
Assert-False $r181State.marketDataRequestSentLegacyFlag 'R181 legacy flag expected false.'
Assert-False $r181State.stateFieldsContradictionDetected 'R181 state contradiction detected.'
Assert-True ($r181Boundary.fixAcknowledgement -eq 'FixLogonAcknowledged') 'R181 FIX acknowledgement missing.'
Assert-True ($r181Response.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'R181 response evidence missing.'
Assert-True ($r181Gate.validatorResult -eq 'LMAX_R181_VALIDATION_PASS') 'R181 validator evidence missing.'

Assert-True ($r182.classification -eq 'LMAX_R182_PASS_CLEAN_STATE_REJECT_PERMISSION_SUPPORT_PACKAGE_RECOMMENDED_NO_EXTERNAL') 'R182 classification mismatch.'
Assert-True ($r182.cleanStateEvidence -eq $true) 'R182 clean-state decision missing.'
Assert-True ($r182Decision.leadingCandidate -eq 'PermissionEntitlementOrExactLmaxSupportClarification') 'R182 leading candidate mismatch.'
Assert-True ($r182Next.permissionSupportPackageRecommended -eq $true) 'R182 did not recommend permission/support package.'
Assert-False $r182Next.liveRetryRecommended 'R182 recommended live retry.'
Assert-True ($r182Gate.validatorResult -eq 'LMAX_R182_VALIDATION_PASS') 'R182 validator evidence missing.'

Assert-True ($package.classification -eq 'LMAX_R183_PASS_RETRY_BLOCKED_UNTIL_SUPPORT_OR_DOCS_CLARIFICATION_NO_EXTERNAL') 'R183 classification mismatch.'
Assert-True ($package.noExternal -eq $true) 'R183 must be no-external.'
Assert-False $package.newExternalActivationAttempted 'New external action detected in R183.'
Assert-False $package.runtimeActionPerformed 'Runtime action detected in R183.'
Assert-True ($package.supportEvidencePackagePresent -eq $true) 'Support package missing.'
Assert-True ($package.r181CleanStateEvidenceReviewed -eq $true) 'R181 review missing.'
Assert-True ($package.r182DecisionReviewed -eq $true) 'R182 review missing.'
Assert-True ($package.r182HistoricalPrefixAccepted -eq 'phase-lmx-r182-*') 'R182 phase-lmx prefix acceptance missing.'
Assert-True ($package.leadingDecision -eq 'PermissionEntitlementOrExactLmaxSupportClarification') 'Leading decision mismatch.'
Assert-False $package.liveRetryRecommendedBeforeSupportOrDocsClarification 'Live retry recommended before clarification.'

Assert-True ($r181Summary.cleanStateEvidencePresent -eq $true) 'R181 clean-state summary missing.'
Assert-True ($r181Summary.marketDataRequestWriteAttempted -eq $true) 'R181 summary write attempted missing.'
Assert-True ($r181Summary.marketDataRequestWriteSucceeded -eq $true) 'R181 summary write succeeded missing.'
Assert-True ($r181Summary.marketDataRequestResponseReadAttempted -eq $true) 'R181 summary response read missing.'
Assert-True ($r181Summary.marketDataRequestReachedBoundedResponseClassification -eq $true) 'R181 summary bounded classification missing.'
Assert-True ($r181Summary.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'R181 summary response category missing.'

Assert-True ($r182Summary.r182DecisionEvidencePresent -eq $true) 'R182 decision summary missing.'
Assert-True ($r182Summary.acceptedHistoricalArtifactPrefix -eq 'phase-lmx-r182-*') 'R182 historical prefix not accepted.'
Assert-True ($r182Summary.leadingCandidate -eq 'PermissionEntitlementOrExactLmaxSupportClarification') 'R182 summary leading candidate mismatch.'
Assert-False $r182Summary.liveRetryRecommendedBeforeSupportOrDocsClarification 'R182 summary allows retry too early.'

Assert-True ($eliminated.eliminatedHypothesesPresent -eq $true) 'Eliminated hypotheses missing.'
foreach ($hypothesis in @(
    'market closed or out of hours',
    'TCP/socket boundary',
    'TLS boundary',
    'FIX logon/session boundary',
    'missing MarketDataRequest write evidence',
    'state propagation inconsistency',
    'batching',
    'multi-request sequencing',
    'non-GBPUSD instruments',
    'USDJPY mapping as direct request cause',
    'Symbol missing as sole cause',
    'SecurityID-only as sole cause',
    'stale lifecycle or MDReqID as sole cause',
    'omitted MDUpdateType',
    'SnapshotOnly as current profile issue')) {
    Assert-True (@($eliminated.eliminatedOrDemoted) -contains $hypothesis) "Missing eliminated hypothesis: $hypothesis"
}

Assert-True ($remaining.remainingCandidateCausesPresent -eq $true) 'Remaining candidate causes missing.'
Assert-False $remaining.liveRetryRecommendedBeforeClarification 'Remaining causes allow live retry too early.'
Assert-True ($permission.permissionEntitlementAssessmentPresent -eq $true) 'Permission assessment missing.'
Assert-True ($permission.assessment -eq 'Leading') 'Permission assessment not leading.'
Assert-True ($permission.supportClarificationRequired -eq $true) 'Support clarification not required.'
Assert-True ($permission.liveRetryBlockedUntilClarification -eq $true) 'Live retry not blocked.'
Assert-True ($fieldOrder.assessment -eq 'PlausibleSecondary') 'Field-order secondary assessment missing.'
Assert-False $fieldOrder.repairRecommendedNow 'Field-order repair recommended too early.'
Assert-True ($securityId.assessment -eq 'PlausibleSecondary') 'SecurityID secondary assessment missing.'
Assert-False $securityId.repairRecommendedNow 'SecurityID repair recommended too early.'

Assert-True (Test-Path (Join-Path $artifactRoot 'phase-lmax-r183-lmax-support-questions.md')) 'Support questions missing.'
Assert-True (Test-Path (Join-Path $artifactRoot 'phase-lmax-r183-lmax-support-message-draft.md')) 'Support message draft missing.'
Assert-True (Test-Path (Join-Path $artifactRoot 'phase-lmax-r183-operator-action-plan.md')) 'Operator action plan missing.'

$questionsText = Get-Content (Join-Path $artifactRoot 'phase-lmax-r183-lmax-support-questions.md') -Raw
foreach ($required in @('entitled', 'MsgType V', 'SecurityID', 'Symbol', 'SubscriptionRequestType', 'MDUpdateType', 'MarketDepth', 'NoRelatedSym', 'field-order', 'session')) {
    Assert-True ($questionsText.Contains($required)) "Support questions missing required topic: $required"
}

Assert-True ($retryGate.retryBlockedUntilClarificationGatePresent -eq $true) 'Retry-blocked gate missing.'
Assert-True ($retryGate.liveRetryBlocked -eq $true) 'Live retry not blocked.'
Assert-False $retryGate.blindRetryAllowed 'Blind retry allowed.'
Assert-False $retryGate.sameProfileRetryAllowed 'Same-profile retry allowed.'
Assert-True ($retryGate.resumeRequiresSupportOrDocsSpecificActionableChange -eq $true) 'Support/docs specific change not required.'

Assert-True ($sanitization.audit -eq 'PASS') 'Sanitization audit did not pass.'
Assert-True ($sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned must be false.'
Assert-False $sanitization.rawFixSerialized 'Raw FIX serialized.'
Assert-False $sanitization.rawRejectTextSerialized 'Raw reject text serialized.'
Assert-False $sanitization.rawCredentialsSerialized 'Raw credentials serialized.'
Assert-False $sanitization.rawSessionIdentifiersSerialized 'Raw session IDs serialized.'
Assert-False $sanitization.rawCompIdsSerialized 'Raw CompIDs serialized.'
Assert-False $sanitization.rawEndpointSerialized 'Raw endpoint serialized.'
Assert-False $sanitization.rawTlsMaterialSerialized 'Raw TLS material serialized.'

Assert-True ($forbidden.audit -eq 'PASS') 'Forbidden actions audit did not pass.'
Assert-False $forbidden.newExternalActivationAttempted 'New external activation attempted in R183.'
Assert-False $forbidden.socketOpened 'Socket opened in R183.'
Assert-False $forbidden.tlsOpened 'TLS opened in R183.'
Assert-False $forbidden.fixOpened 'FIX opened in R183.'
Assert-False $forbidden.marketDataRuntimeActionPerformed 'MarketData runtime action in R183.'
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

Assert-True ($next.liveRetryRecommendedNow -eq $false) 'Next recommendation allows live retry.'
Assert-True ($next.nextLiveRetryBlockedUntilSupportOrDocsClarification -eq $true) 'Next recommendation does not block live retry.'
Assert-True ($next.supportOrDocsSpecificActionableChangeRequired -eq $true) 'Support/docs actionable change not required.'

Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R183_VALIDATION_PASS') 'Validator evidence missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r183-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R183 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R183_VALIDATION_PASS'
