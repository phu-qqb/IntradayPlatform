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
    'phase-lmax-r156-support-evidence-package.json',
    'phase-lmax-r156-lmax-support-questions.md',
    'phase-lmax-r156-lmax-support-message-draft.md',
    'phase-lmax-r156-gate-validation.json',
    'phase-lmax-r157-support-clarification-tracking-summary.md',
    'phase-lmax-r157-support-clarification-tracking.json',
    'phase-lmax-r157-required-lmax-answers-checklist.md',
    'phase-lmax-r157-retry-blocked-until-clarification-gate.json',
    'phase-lmax-r157-operator-pause-note.md',
    'phase-lmax-r157-support-message-final-review.md',
    'phase-lmax-r157-sanitization-audit.json',
    'phase-lmax-r157-forbidden-actions-audit.json',
    'phase-lmax-r157-api-worker-fake-gateway-audit.json',
    'phase-lmax-r157-next-phase-recommendation.json',
    'phase-lmax-r157-gate-validation.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "Required artifact missing: $name"
}

$r156 = Read-Json 'phase-lmax-r156-support-evidence-package.json'
$tracking = Read-Json 'phase-lmax-r157-support-clarification-tracking.json'
$block = Read-Json 'phase-lmax-r157-retry-blocked-until-clarification-gate.json'
$sanitization = Read-Json 'phase-lmax-r157-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r157-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r157-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r157-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r157-gate-validation.json'

Assert-True ($r156.classification -eq 'LMAX_R156_PASS_PERMISSION_ENTITLEMENT_CLARIFICATION_REQUIRED_NO_EXTERNAL') 'R156 support package classification missing.'
Assert-True ($r156.microRepairIterationStopped -eq $true) 'R156 micro-repair stop decision missing.'
Assert-False $r156.liveRetryRecommendedBeforeClarification 'R156 unexpectedly allowed live retry before clarification.'
Assert-True ($r156.credentialValuesReturned -eq $false) 'R156 credentialValuesReturned=false missing.'

Assert-True ($tracking.phase -eq 'LMAX-R157') 'R157 tracking phase mismatch.'
Assert-True ($tracking.classification -eq 'LMAX_R157_PASS_RETRY_BLOCKED_UNTIL_SUPPORT_CLARIFICATION_NO_EXTERNAL') 'R157 classification mismatch.'
Assert-True ($tracking.noExternal -eq $true) 'R157 must be no-external.'
Assert-True ($tracking.r156SupportEvidencePackagePreserved -eq $true) 'R156 package preservation missing.'
Assert-True ($tracking.supportClarificationResolved -eq $false) 'Support clarification should remain unresolved.'
Assert-True ($tracking.supportResponseReceived -eq $false) 'Support response should not be recorded yet.'
Assert-True ($tracking.specificActionableChangeFromSupportOrDocsAvailable -eq $false) 'Unexpected actionable support/docs change recorded.'
Assert-True ($tracking.retryBlockedUntilClarification -eq $true) 'Retry block missing.'
Assert-False $tracking.liveRetryRecommendedNow 'Live retry recommended now.'
Assert-True ($tracking.credentialValuesReturned -eq $false) 'credentialValuesReturned=false missing from tracking.'

$checklist = Get-Content -LiteralPath (Join-Path $artifactRoot 'phase-lmax-r157-required-lmax-answers-checklist.md') -Raw
foreach ($requiredText in @(
    'Market-data entitlement confirmed',
    'MarketDataRequest MsgType V allowed',
    'Exact identifier format for GBPUSD',
    'SubscriptionRequestType',
    'SnapshotPlusUpdates',
    'SnapshotOnly',
    'MDUpdateType',
    'MarketDepth',
    'NoMDEntryTypes',
    'MDEntryType',
    'NoRelatedSym',
    'repeating-group',
    'separate session, account, or permission',
    'No live retry is allowed')) {
    Assert-True ($checklist.Contains($requiredText)) "Required answer checklist missing: $requiredText"
}

Assert-True ($block.retryBlockGatePresent -eq $true) 'Retry block gate missing.'
Assert-True ($block.retryBlocked -eq $true) 'Retry is not blocked.'
Assert-True ($block.supportOrDocsClarificationRequired -eq $true) 'Support/docs clarification requirement missing.'
Assert-True ($block.specificActionableChangeRequired -eq $true) 'Specific actionable change requirement missing.'
Assert-False $block.nextLiveRetryAllowedNow 'Next live retry allowed unexpectedly.'
Assert-True ($block.nextLiveRetryRecommendationBlocked -eq $true) 'Next live retry recommendation not blocked.'
Assert-True ($block.microRepairIterationBlocked -eq $true) 'Micro-repair iteration not blocked.'

$pauseNote = Get-Content -LiteralPath (Join-Path $artifactRoot 'phase-lmax-r157-operator-pause-note.md') -Raw
Assert-True ($pauseNote.Contains('LMAX read-only runtime retries are paused')) 'Operator pause note missing pause statement.'
Assert-True ($pauseNote.Contains('Do not run another live activation retry')) 'Operator pause note missing retry block statement.'

$supportReview = Get-Content -LiteralPath (Join-Path $artifactRoot 'phase-lmax-r157-support-message-final-review.md') -Raw
Assert-True ($supportReview.Contains('ready for operator review/submission')) 'Support final review status missing.'
Assert-True ($supportReview.Contains('does not include raw FIX')) 'Support final review sanitization statement missing.'
Assert-True ($supportReview.Contains('bring back only sanitized support/docs conclusions')) 'Support final review tracking instruction missing.'

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
Assert-True ($forbidden.noExternal -eq $true) 'Forbidden audit must be no-external.'
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

Assert-True ($next.recommendedNextPhase -eq 'PAUSED_PENDING_LMAX_SUPPORT_OR_DOCS_CLARIFICATION') 'Next phase must be paused pending clarification.'
Assert-False $next.liveRetryRecommended 'Live retry recommended.'
Assert-True ($next.liveRetryBlockedUntilSpecificActionableChange -eq $true) 'Live retry block missing from next recommendation.'
Assert-False $next.specificActionableSupportOrDocsChangeAvailable 'Unexpected actionable change present.'
Assert-False $next.r158RecommendedNow 'Unexpected next numbered phase recommendation present.'
Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R157_VALIDATION_PASS') 'Validator evidence missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r157-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R157 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R157_VALIDATION_PASS'
