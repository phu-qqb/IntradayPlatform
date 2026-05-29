$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'
$phase = 'LMAX-R152'
$profileName = 'UltraMinimalSnapshotPlusUpdatesSymbolAndSecurityIdGbpusdSingleInstrument'

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

$summary = Read-Json 'phase-lmax-r152-symbol-securityid-gbpusd-reject-review.json'
$profile = Read-Json 'phase-lmax-r152-r151-profile-confirmation.json'
$reject = Read-Json 'phase-lmax-r152-r151-sanitized-reject-confirmation.json'
$eliminated = Read-Json 'phase-lmax-r152-eliminated-hypotheses-after-r151.json'
$remaining = Read-Json 'phase-lmax-r152-remaining-candidate-issues.json'
$subscription = Read-Json 'phase-lmax-r152-subscriptionrequesttype-lifecycle-decision.json'
$groupOrder = Read-Json 'phase-lmax-r152-repeating-group-field-order-decision.json'
$mdReqId = Read-Json 'phase-lmax-r152-mdreqid-mandatory-tag-decision.json'
$depth = Read-Json 'phase-lmax-r152-marketdepth-mdentrytypes-decision.json'
$permission = Read-Json 'phase-lmax-r152-permission-entitlement-reassessment.json'
$support = Read-Json 'phase-lmax-r152-support-evidence-package-decision.json'
$decision = Read-Json 'phase-lmax-r152-next-repair-decision-gate.json'
$sanitization = Read-Json 'phase-lmax-r152-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r152-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r152-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r152-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r152-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r151-symbol-securityid-gbpusd-activation.json',
    'phase-lmax-r151-selected-identifier-profile-evidence.json',
    'phase-lmax-r151-marketdata-response-evidence.json',
    'phase-lmax-r151-gate-validation.json',
    'phase-lmax-r147-ultraminimal-gbpusd-activation.json',
    'phase-lmax-r148-ultraminimal-gbpusd-reject-review.json',
    'phase-lmax-r149-identifier-combination-repair.json',
    'phase-lmax-r150-identifier-repair-retry-readiness.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "Required predecessor evidence missing: $name"
}

$r151 = Read-Json 'phase-lmax-r151-symbol-securityid-gbpusd-activation.json'
$r151Profile = Read-Json 'phase-lmax-r151-selected-identifier-profile-evidence.json'
$r151Response = Read-Json 'phase-lmax-r151-marketdata-response-evidence.json'
$r151Gate = Read-Json 'phase-lmax-r151-gate-validation.json'

Assert-True ($summary.phase -eq $phase) 'R152 summary phase mismatch.'
Assert-True ($summary.classification -eq 'LMAX_R152_PASS_SYMBOL_SECURITYID_REJECT_REVIEW_SUBSCRIPTION_LIFECYCLE_REPAIR_RECOMMENDED_NO_EXTERNAL') 'R152 classification mismatch.'
Assert-True ($summary.noExternal -eq $true) 'R152 must remain no-external.'
Assert-False $summary.externalActivationAttempted 'R152 external activation detected.'
Assert-False $summary.socketTlsFixMarketDataRuntimeActionAttempted 'R152 socket/TLS/FIX/MarketData runtime action detected.'
Assert-False $summary.liveMarketDataRequestSent 'R152 live MarketDataRequest detected.'
Assert-False $summary.liveMarketDataResponseRead 'R152 live MarketDataResponse read detected.'
Assert-False $summary.apiWorkerStarted 'R152 API/Worker startup detected.'
Assert-False $summary.schedulerServicePollingStarted 'R152 scheduler/service/polling startup detected.'
Assert-False $summary.ordersTradingReplayShadowReplayIntroduced 'R152 forbidden order/trading/replay path detected.'

Assert-True ($r151.classification -eq 'LMAX_R151_FAIL_SESSIONREJECT_WITH_SANITIZED_REASON_REPORTED') 'R151 evidence missing.'
Assert-True ($r151.attemptCount -eq 1) 'R151 attemptCount must equal 1.'
Assert-True ($r151.selectedProfile -eq $profileName) 'R151 selected profile mismatch.'
Assert-True ($r151Response.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'R151 sanitized reject category missing.'
Assert-True ($r151Response.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'R151 sanitized reason unexpected.'
Assert-True ($r151Gate.validatorResult -eq 'LMAX_R151_VALIDATION_PASS') 'R151 validator evidence missing.'

Assert-True ($profile.r151ProfileConfirmationPresent -eq $true) 'R151 profile confirmation missing.'
Assert-True ($profile.selectedProfile -eq $profileName) 'R152 profile confirmation selected profile mismatch.'
Assert-True ($profile.selectedProfileCount -eq 1) 'R151 selected profile count must be one.'
Assert-True ($profile.gbpusdOnly -eq $true) 'R151 profile was not GBPUSD-only.'
Assert-True ((@($profile.diagnosticRequestSymbols) -join ',') -eq 'GBPUSD') 'R151 diagnostic symbols mismatch.'
Assert-True ($profile.identifierCombination -eq 'SymbolPlusSecurityId') 'Identifier combination confirmation missing.'
Assert-True ($profile.symbolIncluded -eq $true) 'Symbol component missing.'
Assert-True ($profile.securityId -eq '4002') 'SecurityID mismatch.'
Assert-True ($profile.securityIdSource -eq '8') 'SecurityIDSource mismatch.'
Assert-False $profile.internalSymbolIncluded 'InternalSymbol included.'
Assert-True ($profile.snapshotPlusUpdates -eq $true) 'SnapshotPlusUpdates missing.'
Assert-False $profile.snapshotOnly 'SnapshotOnly returned.'
Assert-True ($profile.mdUpdateTypeProfileControlled -eq $true) 'MDUpdateType control missing.'
Assert-False $profile.mdUpdateTypeIncluded 'MDUpdateType included.'
Assert-True ($profile.marketDepth -eq 1) 'MarketDepth mismatch.'
Assert-True ((@($profile.mdEntryTypes) -join ',') -eq 'Bid,Offer') 'MDEntryTypes mismatch.'
Assert-True ($profile.oneRequestOnly -eq $true) 'R151 must be one request only.'
Assert-False $profile.rawFixSerialized 'Raw FIX serialized in profile confirmation.'

Assert-True ($reject.r151SanitizedRejectConfirmationPresent -eq $true) 'R151 sanitized reject confirmation missing.'
Assert-True ($reject.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'R151 reject category mismatch.'
Assert-True ($reject.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'R151 CLI sanitized reason mismatch.'
Assert-True ($reject.sanitizedSessionRejectReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'R151 artifact sanitized reason mismatch.'
Assert-False $reject.entriesObserved 'Entries unexpectedly observed.'
Assert-True ($reject.sanitizedEntryCount -eq 0) 'Sanitized entry count must be zero.'
Assert-False $reject.rawRejectTextSerialized 'Raw reject text serialized.'
Assert-False $reject.rawFixSerialized 'Raw FIX serialized.'

Assert-True ($eliminated.eliminatedHypothesesReviewPresent -eq $true) 'Eliminated hypotheses review missing.'
Assert-True (@($eliminated.eliminatedOrDemoted).Count -ge 8) 'Eliminated hypotheses review incomplete.'
Assert-True (@($eliminated.eliminatedOrDemoted | Where-Object { $_.hypothesis -eq 'SecurityID-only as sole leading issue' }).Count -eq 1) 'SecurityID-only demotion missing.'
Assert-True (@($eliminated.eliminatedOrDemoted | Where-Object { $_.hypothesis -eq 'missing Symbol as sole leading issue' }).Count -eq 1) 'Missing Symbol demotion missing.'

Assert-True ($remaining.remainingCandidateIssueReviewPresent -eq $true) 'Remaining candidate issue review missing.'
Assert-True (@($remaining.remainingCandidates | Where-Object { $_.issue -eq 'subscriptionrequesttype_lifecycle' -and $_.status -eq 'leading' }).Count -eq 1) 'Subscription lifecycle leading candidate missing.'
Assert-True (@($remaining.remainingCandidates | Where-Object { $_.issue -eq 'permission_entitlement' }).Count -eq 1) 'Permission/entitlement candidate missing.'

Assert-True ($subscription.decisionPresent -eq $true) 'SubscriptionRequestType/lifecycle decision missing.'
Assert-True ($subscription.decision -eq 'RecommendNoExternalSubscriptionRequestTypeLifecycleRepair') 'SubscriptionRequestType/lifecycle decision mismatch.'
Assert-True ($subscription.recommended -eq $true) 'Subscription lifecycle repair not recommended.'
Assert-True ($subscription.leading -eq $true) 'Subscription lifecycle must be leading.'
Assert-True ($subscription.noExternal -eq $true) 'Subscription lifecycle decision must be no-external.'
Assert-True ($groupOrder.decisionPresent -eq $true) 'Repeating group/order decision missing.'
Assert-True ($groupOrder.decision -eq 'SecondaryAuditInsideLifecycleRepair') 'Repeating group/order decision mismatch.'
Assert-True ($mdReqId.decisionPresent -eq $true) 'MDReqID/mandatory-tag decision missing.'
Assert-True ($mdReqId.decision -eq 'SecondaryAuditInsideLifecycleRepair') 'MDReqID/mandatory-tag decision mismatch.'
Assert-True ($depth.decisionPresent -eq $true) 'MarketDepth/MDEntryTypes decision missing.'
Assert-True ($depth.decision -eq 'SecondaryReviewNotNextStandaloneRepair') 'MarketDepth/MDEntryTypes decision mismatch.'

Assert-True ($permission.permissionEntitlementReassessmentPresent -eq $true) 'Permission/entitlement reassessment missing.'
Assert-True ($permission.decision -eq 'PlausibleSecondaryNotLeading') 'Permission/entitlement reassessment mismatch.'
Assert-True ($permission.elevatedAfterRepeatedIsolatedRejects -eq $true) 'Permission/entitlement was not reassessed upward.'
Assert-False $permission.recommendedAsNextPhase 'Permission/entitlement should not be next phase.'
Assert-True ($support.decisionPresent -eq $true) 'Support evidence package decision missing.'
Assert-False $support.recommended 'Support package should not be recommended yet.'

Assert-True ($decision.nextRepairDecisionGatePresent -eq $true) 'Next repair decision gate missing.'
Assert-True ($decision.selectedNextRepairPath -eq 'SubscriptionRequestTypeLifecycleRepair') 'Next repair decision mismatch.'
Assert-True ($decision.selectedNextPhase -eq 'LMAX-R153') 'Next phase decision mismatch.'
Assert-True ($decision.r153MustRemainNoExternal -eq $true) 'R153 no-external constraint missing.'

Assert-True ((@($summary.approvedUniverse) -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved universe changed.'
Assert-True ($summary.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat weakened.'
Assert-True ($summary.credentialValuesReturned -eq $false) 'credentialValuesReturned must remain false.'
Assert-False $summary.rawFixSerialized 'Raw FIX serialized.'
Assert-False $summary.rawRejectTextSerialized 'Raw reject text serialized.'

Assert-True ($sanitization.audit -eq 'PASS') 'Sanitization audit did not pass.'
Assert-True ($sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned audit must remain false.'
Assert-False $sanitization.rawFixSerialized 'Raw FIX serialized.'
Assert-False $sanitization.rawRejectTextSerialized 'Raw reject text serialized.'
Assert-False $sanitization.rawCredentialsSerialized 'Raw credentials serialized.'
Assert-False $sanitization.rawUsernameSerialized 'Raw username serialized.'
Assert-False $sanitization.rawPasswordSerialized 'Raw password serialized.'
Assert-False $sanitization.rawSessionIdentifiersSerialized 'Raw session identifiers serialized.'
Assert-False $sanitization.rawCompIdsSerialized 'Raw CompIDs serialized.'
Assert-False $sanitization.rawEndpointSerialized 'Raw endpoint serialized.'
Assert-False $sanitization.rawTlsMaterialSerialized 'Raw TLS material serialized.'

Assert-True ($forbidden.audit -eq 'PASS') 'Forbidden actions audit did not pass.'
Assert-False $forbidden.externalActivationAttempted 'External activation detected in R152.'
Assert-False $forbidden.socketTlsFixMarketDataRuntimeActionAttempted 'Runtime boundary action detected in R152.'
Assert-False $forbidden.liveMarketDataRequestSent 'Live MarketDataRequest detected in R152.'
Assert-False $forbidden.liveMarketDataResponseRead 'Live MarketDataResponse read detected in R152.'
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

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R153') 'Next phase recommendation must be R153.'
Assert-True ($next.r153MustRemainNoExternal -eq $true) 'R153 no-external recommendation missing.'
Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.focusedTests -match '^PASS') 'Focused test evidence missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit test evidence missing.'
Assert-True ($gate.integrationTests -match '^PASS') 'Integration test evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R152_VALIDATION_PASS') 'Validator result missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r152-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R152 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R152_VALIDATION_PASS'
