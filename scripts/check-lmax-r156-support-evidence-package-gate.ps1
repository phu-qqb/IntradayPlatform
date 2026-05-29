$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'

function Read-Json($name) {
    $path = Join-Path $artifactRoot $name
    if (!(Test-Path $path)) {
        throw "Missing artifact: $name"
    }

    Get-Content $path -Raw | ConvertFrom-Json
}

function Assert-True($condition, $message) {
    if (-not $condition) {
        throw $message
    }
}

function Assert-False($condition, $message) {
    if ($condition) {
        throw $message
    }
}

foreach ($name in @(
    'phase-lmax-r147-ultraminimal-gbpusd-activation.json',
    'phase-lmax-r151-symbol-securityid-gbpusd-activation.json',
    'phase-lmax-r155-subscription-lifecycle-gbpusd-activation.json',
    'phase-lmax-r156-support-evidence-package-summary.md',
    'phase-lmax-r156-support-evidence-package.json',
    'phase-lmax-r156-repeated-isolated-rejects-evidence.json',
    'phase-lmax-r156-eliminated-hypotheses.json',
    'phase-lmax-r156-permission-entitlement-reassessment.json',
    'phase-lmax-r156-lmax-support-questions.md',
    'phase-lmax-r156-lmax-support-message-draft.md',
    'phase-lmax-r156-next-action-decision.json',
    'phase-lmax-r156-sanitization-audit.json',
    'phase-lmax-r156-forbidden-actions-audit.json',
    'phase-lmax-r156-api-worker-fake-gateway-audit.json',
    'phase-lmax-r156-next-phase-recommendation.json',
    'phase-lmax-r156-gate-validation.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "Required artifact missing: $name"
}

$r147 = Read-Json 'phase-lmax-r147-ultraminimal-gbpusd-activation.json'
$r151 = Read-Json 'phase-lmax-r151-symbol-securityid-gbpusd-activation.json'
$r155 = Read-Json 'phase-lmax-r155-subscription-lifecycle-gbpusd-activation.json'
$summary = Read-Json 'phase-lmax-r156-support-evidence-package.json'
$repeated = Read-Json 'phase-lmax-r156-repeated-isolated-rejects-evidence.json'
$eliminated = Read-Json 'phase-lmax-r156-eliminated-hypotheses.json'
$permission = Read-Json 'phase-lmax-r156-permission-entitlement-reassessment.json'
$decision = Read-Json 'phase-lmax-r156-next-action-decision.json'
$sanitization = Read-Json 'phase-lmax-r156-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r156-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r156-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r156-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r156-gate-validation.json'

foreach ($prior in @($r147, $r151, $r155)) {
    Assert-True ($prior.attemptCount -eq 1) "$($prior.phase) attemptCount must equal 1."
    Assert-True ($prior.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') "$($prior.phase) sanitized reject category missing."
    Assert-True ($prior.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') "$($prior.phase) sanitized reason missing."
    Assert-True ($prior.sanitizedSessionRejectReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') "$($prior.phase) artifact sanitized reason missing."
    Assert-True ($prior.gbpusdOnly -eq $true) "$($prior.phase) must be GBPUSD-only evidence."
    Assert-True ($prior.entriesObserved -eq $false) "$($prior.phase) should not have entries observed."
    Assert-True ($prior.sanitizedEntryCount -eq 0) "$($prior.phase) sanitized entry count must be zero."
    Assert-True ($prior.credentialValuesReturned -eq $false) "$($prior.phase) credentialValuesReturned must be false."
    Assert-False $prior.rawSensitiveValuesSerialized "$($prior.phase) serialized sensitive values."
}

Assert-True ($summary.phase -eq 'LMAX-R156') 'R156 support package phase mismatch.'
Assert-True ($summary.classification -eq 'LMAX_R156_PASS_PERMISSION_ENTITLEMENT_CLARIFICATION_REQUIRED_NO_EXTERNAL') 'R156 classification mismatch.'
Assert-True ($summary.noExternal -eq $true) 'R156 must be no-external.'
Assert-True ($summary.repeatedSanitizedCategory -eq 'SessionRejectObservedWithSanitizedReason') 'Repeated sanitized category missing.'
Assert-True ($summary.repeatedSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'Repeated sanitized reason missing.'
Assert-True ($summary.microRepairIterationStopped -eq $true) 'Micro-repair stop decision missing.'
Assert-False $summary.liveRetryRecommendedBeforeClarification 'Live retry recommended before support clarification.'
Assert-True ($summary.credentialValuesReturned -eq $false) 'credentialValuesReturned=false missing from summary.'
Assert-False $summary.rawFixSerialized 'Raw FIX serialized in summary.'
Assert-False $summary.rawRejectTextSerialized 'Raw reject text serialized in summary.'
Assert-False $summary.rawCredentialsSerialized 'Raw credentials serialized in summary.'
Assert-False $summary.rawEndpointSessionCompIdTlsSerialized 'Raw endpoint/session/CompID/TLS serialized in summary.'

Assert-True ($repeated.repeatedRejectSummaryPresent -eq $true) 'Repeated reject summary missing.'
Assert-True ($repeated.allAttemptsReachedFixAcknowledgement -eq $true) 'FIX acknowledgement proof missing.'
Assert-True ($repeated.allAttemptsReachedMarketDataResponseClassification -eq $true) 'MarketDataResponse classification proof missing.'
Assert-True ($repeated.allAttemptsReturnedSameSanitizedReasonCategory -eq $true) 'Common sanitized reason proof missing.'
Assert-True (@($repeated.evidence).Count -eq 3) 'Expected three repeated isolated reject evidence entries.'

Assert-True ($eliminated.eliminatedHypothesesReviewPresent -eq $true) 'Eliminated hypotheses review missing.'
foreach ($hypothesis in @(
    'Market closed or out of hours',
    'TCP/TLS/FIX/logon issue',
    'Batching',
    'Multi-request sequencing',
    'USDJPY mapping',
    'Non-GBPUSD instruments',
    'SecurityID-only as sole issue',
    'Missing Symbol as sole issue',
    'Stale MDReqID lifecycle as sole issue',
    'SnapshotOnly as current issue',
    'MDUpdateType presence')) {
    $match = @($eliminated.eliminatedOrDemoted | Where-Object { $_.hypothesis -eq $hypothesis })
    Assert-True ($match.Count -eq 1) "Eliminated/demoted hypothesis missing: $hypothesis"
}

Assert-True ($permission.permissionEntitlementReassessmentPresent -eq $true) 'Permission/entitlement reassessment missing.'
Assert-True ($permission.decision -eq 'LeadingOrCoLeadingClarificationRequired') 'Permission/entitlement was not elevated.'
Assert-True ($permission.supportClarificationRequiredBeforeNextLiveRetry -eq $true) 'Support clarification before retry missing.'
Assert-False $permission.docsBackedSpecificRepairAvailableNow 'Unexpected specific docs-backed repair asserted.'

$questionsText = Get-Content -LiteralPath (Join-Path $artifactRoot 'phase-lmax-r156-lmax-support-questions.md') -Raw
$messageText = Get-Content -LiteralPath (Join-Path $artifactRoot 'phase-lmax-r156-lmax-support-message-draft.md') -Raw
foreach ($requiredText in @(
    'entitled for market data',
    'MarketDataRequest MsgType V',
    'identifier format',
    'SubscriptionRequestType',
    'SnapshotPlusUpdates',
    'SnapshotOnly',
    'MDUpdateType',
    'MarketDepth',
    'NoMDEntryTypes',
    'NoRelatedSym',
    'session type')) {
    Assert-True ($questionsText.Contains($requiredText)) "Support question missing: $requiredText"
}
Assert-True ($messageText.Contains('read-only Demo FIX market-data workflow')) 'Support message context missing.'
Assert-True ($messageText.Contains('No market-data entries were observed')) 'Support message result missing.'
Assert-True ($messageText.Contains('did not serialize or include raw FIX')) 'Support message sanitization statement missing.'

Assert-True ($decision.nextActionDecisionPresent -eq $true) 'Next action decision missing.'
Assert-True ($decision.permissionEntitlementClarificationLeadingOrCoLeading -eq $true) 'Next action does not elevate permission/support clarification.'
Assert-False $decision.specificDocsBackedRepairAvailableNow 'Unexpected docs-backed repair decision.'
Assert-False $decision.anotherLiveRetryRecommendedNow 'Another live retry should not be recommended now.'

Assert-True ($sanitization.audit -eq 'PASS') 'Sanitization audit did not pass.'
Assert-True ($sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned=false missing.'
Assert-False $sanitization.rawFixSerialized 'Raw FIX serialized.'
Assert-False $sanitization.rawRejectTextSerialized 'Raw reject text serialized.'
Assert-False $sanitization.rawCredentialsSerialized 'Raw credentials serialized.'
Assert-False $sanitization.rawSessionIdentifiersSerialized 'Raw session identifiers serialized.'
Assert-False $sanitization.rawCompIdsSerialized 'Raw CompIDs serialized.'
Assert-False $sanitization.rawEndpointSerialized 'Raw endpoint serialized.'
Assert-False $sanitization.rawTlsMaterialSerialized 'Raw TLS material serialized.'

Assert-True ($forbidden.audit -eq 'PASS') 'Forbidden actions audit did not pass.'
Assert-True ($forbidden.noExternal -eq $true) 'R156 forbidden audit must be no-external.'
Assert-False $forbidden.externalActivationAttempted 'New external activation attempt detected.'
Assert-False $forbidden.socketTlsFixMarketDataRuntimeActionDetected 'Socket/TLS/FIX/MarketData runtime action detected.'
Assert-False $forbidden.ordersIntroduced 'Order path introduced.'
Assert-False $forbidden.tradingEnabled 'Trading enabled.'
Assert-False $forbidden.schedulerIntroduced 'Scheduler introduced.'
Assert-False $forbidden.pollingLoopIntroduced 'Polling loop introduced.'
Assert-False $forbidden.serviceIntroduced 'Service introduced.'
Assert-False $forbidden.replayIntroduced 'Replay introduced.'
Assert-False $forbidden.shadowReplayIntroduced 'Shadow replay introduced.'
Assert-False $forbidden.apiStartupAttempted 'API startup attempted.'
Assert-False $forbidden.workerStartupAttempted 'Worker startup attempted.'
Assert-False $forbidden.productionAccountUsed 'Production account used.'

Assert-True ($apiWorker.audit -eq 'PASS') 'API/Worker audit did not pass.'
Assert-True ($apiWorker.apiWorkerGatewayMode -eq 'FakeLmaxGatewayOnly') 'API/Worker FakeLmaxGatewayOnly regressed.'
Assert-False $apiWorker.apiStartupAttempted 'API startup attempted.'
Assert-False $apiWorker.workerStartupAttempted 'Worker startup attempted.'

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R157') 'Next phase recommendation must be R157.'
Assert-True ($next.r157MustBeNoExternal -eq $true) 'R157 no-external recommendation missing.'
Assert-False $next.anotherLiveRetryRecommendedBeforeSupportOrDocsClarification 'Live retry recommended before clarification.'
Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R156_VALIDATION_PASS') 'Validator evidence missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r156-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R156 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R156_VALIDATION_PASS'
