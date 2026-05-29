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

$review = Read-Json 'phase-lmax-r164-post-repair-evidence-review.json'
$state = Read-Json 'phase-lmax-r164-r163-state-reporting-confirmation.json'
$decision = Read-Json 'phase-lmax-r164-next-action-decision-gate.json'
$retry = Read-Json 'phase-lmax-r164-retry-readiness-decision.json'
$fieldOrder = Read-Json 'phase-lmax-r164-field-order-repair-decision.json'
$mapping = Read-Json 'phase-lmax-r164-securityid-mapping-audit-decision.json'
$permission = Read-Json 'phase-lmax-r164-permission-support-decision.json'
$sanitization = Read-Json 'phase-lmax-r164-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r164-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r164-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r164-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r164-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r163-request-sent-flag-consistency-repair.json',
    'phase-lmax-r163-state-field-contract.json',
    'phase-lmax-r163-gate-validation.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "R163 evidence missing: $name"
}

$r163 = Read-Json 'phase-lmax-r163-request-sent-flag-consistency-repair.json'
$r163State = Read-Json 'phase-lmax-r163-state-field-contract.json'
$r163Gate = Read-Json 'phase-lmax-r163-gate-validation.json'
Assert-True ($r163.classification -eq 'LMAX_R163_PASS_MARKETDATAREQUEST_SENT_FLAG_CONSISTENCY_REPAIR_NO_EXTERNAL') 'R163 classification missing.'
Assert-True ($r163.marketDataRequestSentAmbiguityResolved -eq $true) 'R163 ambiguity repair not confirmed.'
Assert-True ($r163.requestWriteAndResponseClassificationSeparated -eq $true) 'R163 state separation not confirmed.'
Assert-True ($r163State.stateFieldContractPresent -eq $true) 'R163 state contract missing.'
Assert-True ($r163Gate.validatorResult -eq 'LMAX_R163_VALIDATION_PASS') 'R163 validator evidence missing.'

Assert-True ($review.classification -eq 'LMAX_R164_PASS_POST_REPAIR_REVIEW_RETRY_READINESS_RECOMMENDED_NO_EXTERNAL') 'R164 classification mismatch.'
Assert-True ($review.noExternal -eq $true) 'R164 must be no-external.'
Assert-False $review.newExternalActivationAttempted 'New external activation attempt detected.'
Assert-False $review.runtimeActionPerformed 'Runtime action detected.'
Assert-True ($review.r163EvidenceReviewed -eq $true) 'R163 evidence review missing.'
Assert-True ($review.r163StateReportingRepairConfirmed -eq $true) 'R163 state reporting confirmation missing.'
Assert-True ($review.futureRunsExposeExplicitStateFields -eq $true) 'Future state fields not confirmed.'
Assert-True ($review.selectedNextAction -eq 'NoExternalRetryReadiness') 'Unexpected next action decision.'
Assert-False $review.liveRetryRecommendedNow 'R164 must not recommend immediate live retry.'

Assert-True ($state.stateReportingConfirmationPresent -eq $true) 'State reporting confirmation missing.'
Assert-True ($state.marketDataRequestSentAmbiguityResolved -eq $true) 'marketDataRequestSent ambiguity unresolved.'
Assert-True ($state.requestWriteAndResponseClassificationSeparated -eq $true) 'Write/read/classification not separated.'
Assert-True ($state.legacyFlagCompatibilityOnly -eq $true) 'Legacy flag compatibility-only decision missing.'
foreach ($field in @(
    'marketDataRequestWriteAttempted',
    'marketDataRequestWriteSucceeded',
    'marketDataRequestResponseReadAttempted',
    'marketDataRequestReachedBoundedResponseClassification')) {
    Assert-True (@($state.futureAuthoritativeFields) -contains $field) "Future authoritative field missing: $field"
}

Assert-True ($decision.nextActionDecisionGatePresent -eq $true) 'Next action decision gate missing.'
Assert-True ($decision.selectedDecision -eq 'RetryReadinessWithSameDocsBackedProfileAfterStateReportingRepair') 'Unexpected selected decision.'
Assert-True ($decision.nextPhase -eq 'LMAX-R165') 'Next phase should be R165.'
Assert-False $decision.liveRetryRecommendedNow 'Decision gate must not recommend immediate live retry.'

Assert-True ($retry.retryReadinessDecisionPresent -eq $true) 'Retry readiness decision missing.'
Assert-True ($retry.decision -eq 'RecommendedNextNoExternalReadinessGate') 'Retry readiness decision unexpected.'
Assert-True ($retry.profileForReadiness -eq 'UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument') 'Unexpected readiness profile.'
Assert-True ($retry.profileContract.gbpusdOnly -eq $true) 'Readiness profile not GBPUSD-only.'
Assert-True ($retry.profileContract.singleRequest -eq $true) 'Readiness profile not single request.'
Assert-True ($retry.profileContract.securityId -eq '4002') 'GBPUSD SecurityID missing.'
Assert-True ($retry.profileContract.securityIdSource -eq '8') 'SecurityIDSource missing.'
Assert-True ($retry.profileContract.subscriptionRequestType -eq '1') 'SubscriptionRequestType=1 missing.'
Assert-True ($retry.profileContract.mdUpdateType -eq '0') 'MDUpdateType=0 missing.'
Assert-True ($retry.profileContract.bidAndOfferTogether -eq $true) 'Bid+Offer evidence missing.'
Assert-False $retry.profileContract.symbolTextPresent 'Symbol text present.'
Assert-False $retry.profileContract.internalSymbolPresent 'InternalSymbol present.'
Assert-False $retry.profileContract.snapshotOnlyPresent 'SnapshotOnly present.'
Assert-True ($retry.requiresFreshOperatorApprovalBeforeAnyExternalRetry -eq $true) 'Fresh approval requirement missing.'
Assert-True ($retry.requiresWeekdayActiveFxMarketDataAvailability -eq $true) 'Market-hours requirement missing.'
Assert-True ($retry.requiresExactlyOneBoundedAttempt -eq $true) 'Single-attempt requirement missing.'
Assert-True ($retry.mustIncludeRepairedStateFieldsInEvidence -eq $true) 'Repaired state field evidence requirement missing.'
Assert-False $retry.liveRetryExecutedInR164 'R164 executed live retry.'

Assert-True ($fieldOrder.fieldOrderRepairDecisionPresent -eq $true) 'Field-order decision missing.'
Assert-True ($fieldOrder.decision -eq 'Deferred') 'Field-order decision unexpected.'
Assert-False $fieldOrder.fieldOrderRepairRecommendedNow 'Field-order repair should be deferred.'
Assert-True ($mapping.securityIdMappingAuditDecisionPresent -eq $true) 'SecurityID mapping decision missing.'
Assert-True ($mapping.decision -eq 'Deferred') 'SecurityID mapping decision unexpected.'
Assert-False $mapping.securityIdMappingAuditRecommendedNow 'SecurityID mapping audit should be deferred.'
Assert-True ($permission.permissionSupportDecisionPresent -eq $true) 'Permission/support decision missing.'
Assert-True ($permission.decision -eq 'DeferredButPlausibleFallback') 'Permission/support decision unexpected.'
Assert-False $permission.permissionSupportPackageRecommendedNow 'Permission/support package should be deferred.'

Assert-True ($sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned must remain false.'
Assert-False $sanitization.rawFixSerialized 'Raw FIX serialized.'
Assert-False $sanitization.rawRejectTextSerialized 'Raw reject text serialized.'
Assert-False $sanitization.rawCredentialsSerialized 'Raw credentials serialized.'
Assert-False $sanitization.rawSessionIdentifiersSerialized 'Raw session identifiers serialized.'
Assert-False $sanitization.rawCompIdsSerialized 'Raw CompIDs serialized.'
Assert-False $sanitization.rawEndpointSerialized 'Raw endpoint serialized.'
Assert-False $sanitization.rawTlsMaterialSerialized 'Raw TLS material serialized.'

Assert-True ($forbidden.audit -eq 'PASS') 'Forbidden actions audit did not pass.'
Assert-False $forbidden.newExternalActivationAttempted 'New external activation attempt detected.'
Assert-False $forbidden.socketOpened 'Socket opened.'
Assert-False $forbidden.tlsOpened 'TLS opened.'
Assert-False $forbidden.fixOpened 'FIX opened.'
Assert-False $forbidden.marketDataRuntimeActionPerformed 'MarketData runtime action performed.'
Assert-False $forbidden.ordersIntroduced 'Order path introduced.'
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

$universe = Read-Json 'phase-lmax-r161-approved-universe-preservation.json'
$usdJpy = Read-Json 'phase-lmax-r161-usdjpy-caveat-preservation.json'
Assert-True ($universe.approvedUniversePreserved -eq $true) 'Approved universe weakened.'
Assert-True ((@($universe.approvedInstruments) -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved universe changed.'
Assert-True ($usdJpy.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat weakened.'
Assert-True ($usdJpy.usdJpySecurityId -eq '4004') 'USDJPY SecurityID weakened.'
Assert-True ($usdJpy.usdJpySecurityIdSource -eq '8') 'USDJPY SecurityIDSource weakened.'

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R165') 'Next phase recommendation must be R165.'
Assert-True ($next.r165MustRemainNoExternal -eq $true) 'R165 no-external requirement missing.'
Assert-False $next.liveRetryRecommendedNow 'R164 must not recommend immediate live retry.'
Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R164_VALIDATION_PASS') 'Validator evidence missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r164-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R164 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R164_VALIDATION_PASS'
