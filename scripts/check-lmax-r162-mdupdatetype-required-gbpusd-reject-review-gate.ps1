$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'
$phase = 'LMAX-R162'
$profileName = 'UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument'

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

$review = Read-Json 'phase-lmax-r162-mdupdatetype-required-gbpusd-reject-review.json'
$profile = Read-Json 'phase-lmax-r162-r161-profile-confirmation.json'
$reject = Read-Json 'phase-lmax-r162-r161-sanitized-reject-confirmation.json'
$flagAudit = Read-Json 'phase-lmax-r162-marketdatarequestsent-flag-inconsistency-audit.json'
$boundary = Read-Json 'phase-lmax-r162-boundary-evidence-consistency-review.json'
$eliminated = Read-Json 'phase-lmax-r162-eliminated-hypotheses-after-r161.json'
$remaining = Read-Json 'phase-lmax-r162-remaining-candidate-issues.json'
$fieldOrder = Read-Json 'phase-lmax-r162-field-order-repeating-group-decision.json'
$mapping = Read-Json 'phase-lmax-r162-securityid-mapping-decision.json'
$permission = Read-Json 'phase-lmax-r162-permission-entitlement-reassessment.json'
$support = Read-Json 'phase-lmax-r162-support-evidence-package-decision.json'
$decision = Read-Json 'phase-lmax-r162-next-repair-decision-gate.json'
$sanitization = Read-Json 'phase-lmax-r162-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r162-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r162-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r162-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r162-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r161-mdupdatetype-required-gbpusd-activation.json',
    'phase-lmax-r161-selected-mdupdatetype-profile-evidence.json',
    'phase-lmax-r161-marketdata-response-evidence.json',
    'phase-lmax-r161-gate-validation.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "R161 evidence missing: $name"
}

$r161 = Read-Json 'phase-lmax-r161-mdupdatetype-required-gbpusd-activation.json'
Assert-True ($r161.classification -eq 'LMAX_R161_FAIL_SESSIONREJECT_WITH_SANITIZED_REASON_REPORTED') 'R161 classification evidence missing.'
Assert-True ($r161.attemptCount -eq 1) 'R161 attemptCount invalid.'
Assert-True ($r161.selectedProfile -eq $profileName) 'R161 selected profile mismatch.'
Assert-True ($r161.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'R161 sanitized reject category missing.'
Assert-True ($r161.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'R161 CLI sanitized reason mismatch.'
Assert-True ($r161.sanitizedSessionRejectReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'R161 artifact sanitized reason mismatch.'

Assert-True ($review.phase -eq $phase) 'R162 review phase mismatch.'
Assert-True ($review.classification -eq 'LMAX_R162_PASS_R161_REJECT_REVIEW_REPORTING_FLAG_REPAIR_RECOMMENDED_NO_EXTERNAL') 'R162 classification mismatch.'
Assert-True ($review.noExternal -eq $true) 'R162 must be no-external.'
Assert-False $review.newExternalActivationAttempted 'New external activation attempt detected.'
Assert-False $review.runtimeActionPerformed 'Runtime action detected.'
Assert-True ($review.r161EvidenceReviewed -eq $true) 'R161 evidence review missing.'
Assert-True ($review.r161AttemptCount -eq 1) 'R161 attempt count invalid in review.'
Assert-True ($review.r161SelectedProfile -eq $profileName) 'R161 profile mismatch in review.'
Assert-True ($review.r161SanitizedReason -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'R161 sanitized reason mismatch in review.'
Assert-False $review.mdUpdateTypeRequiredRepairResolvedReject 'R162 must confirm MDUpdateType repair did not resolve reject.'
Assert-True ($review.marketDataRequestSentFlagInconsistencyPresent -eq $true) 'marketDataRequestSent inconsistency audit missing.'
Assert-False $review.liveRetryRecommended 'R162 must not recommend live retry.'

Assert-True ($profile.r161ProfileConfirmationPresent -eq $true) 'R161 profile confirmation missing.'
Assert-True ($profile.profileMatched -eq $true) 'R161 intended profile not confirmed.'
Assert-True ($profile.r161SelectedProfile -eq $profileName) 'R161 selected profile unexpected.'
Assert-True ($profile.gbpusdOnly -eq $true) 'R161 profile not GBPUSD-only.'
Assert-True ($profile.singleRequest -eq $true) 'R161 profile not single request.'
Assert-True ($profile.securityId -eq '4002') 'GBPUSD SecurityID missing.'
Assert-True ($profile.securityIdSource -eq '8') 'SecurityIDSource missing.'
Assert-True ($profile.subscriptionRequestType -eq '1') 'SubscriptionRequestType=1 missing.'
Assert-True ($profile.mdUpdateType -eq '0') 'MDUpdateType=0 missing.'
Assert-True ($profile.bidAndOfferTogether -eq $true) 'Bid+Offer evidence missing.'
Assert-False $profile.symbolTextPresent 'Symbol text present.'
Assert-False $profile.internalSymbolPresent 'InternalSymbol present.'
Assert-False $profile.snapshotOnlyPresent 'SnapshotOnly present.'

Assert-True ($reject.r161SanitizedRejectConfirmationPresent -eq $true) 'R161 sanitized reject confirmation missing.'
Assert-True ($reject.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'R161 reject category missing.'
Assert-True ($reject.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'R161 CLI sanitized reason missing.'
Assert-True ($reject.sanitizedSessionRejectReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'R161 artifact sanitized reason missing.'
Assert-False $reject.rawRejectTextSerialized 'Raw reject text serialized.'
Assert-False $reject.rawFixSerialized 'Raw FIX serialized.'

Assert-True ($flagAudit.marketDataRequestSentInconsistencyAuditPresent -eq $true) 'marketDataRequestSent inconsistency audit missing.'
Assert-True ($flagAudit.marketDataRequestSentSanitizedFlag -eq $false) 'Expected marketDataRequestSent=false evidence missing.'
Assert-True ($flagAudit.inconsistencyDetected -eq $true) 'Expected inconsistency not detected.'
Assert-True ($flagAudit.classification -eq 'ReportingFlagOrRequestWriteStateCaptureBugPlausible') 'Unexpected inconsistency classification.'

Assert-True ($boundary.boundaryEvidenceConsistencyReviewPresent -eq $true) 'Boundary evidence consistency review missing.'
Assert-True ($boundary.fixAcknowledgement -eq 'FixLogonAcknowledged') 'FIX acknowledgement missing.'
Assert-True ($boundary.marketDataRequestAfterFixSuccess -eq $true) 'MarketDataRequest-after-FIX evidence missing.'
Assert-True ($boundary.marketDataRequestSentSanitizedFlag -eq $false) 'Boundary review missing sent=false flag.'
Assert-True ($boundary.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'Boundary review missing response category.'
Assert-False $boundary.boundaryEvidenceInternallyConsistent 'Boundary review must flag inconsistency.'
Assert-True ($boundary.nextEvidenceRepairRequired -eq $true) 'Boundary evidence repair decision missing.'

Assert-True ($eliminated.eliminatedHypothesesReviewPresent -eq $true) 'Eliminated hypotheses review missing.'
Assert-True (@($eliminated.eliminatedOrDemoted).Count -gt 5) 'Eliminated hypotheses review too thin.'
Assert-True ($remaining.remainingCandidateIssueReviewPresent -eq $true) 'Remaining candidate issue review missing.'
Assert-True (@($remaining.leadingCandidates).Count -ge 1) 'Leading candidate issue missing.'

Assert-True ($fieldOrder.decision -eq 'SecondaryDeferred') 'Field order decision missing or unexpected.'
Assert-False $fieldOrder.fieldOrderRepeatingGroupRepairRecommendedNow 'Field-order repair should be deferred.'
Assert-True ($mapping.decision -eq 'SecondaryAuditDeferred') 'SecurityID mapping decision missing.'
Assert-False $mapping.securityIdMappingAuditRecommendedNow 'SecurityID mapping audit should be deferred.'
Assert-True ($permission.permissionEntitlementReassessmentPresent -eq $true) 'Permission/entitlement reassessment missing.'
Assert-True ($permission.decision -eq 'PlausibleSecondaryCoLeadingAfterEvidenceRepair') 'Permission/entitlement reassessment unexpected.'
Assert-True ($support.supportEvidencePackageDecisionPresent -eq $true) 'Support evidence package decision missing.'
Assert-False $support.supportPackageRecommendedNow 'Support package should be deferred in R162.'

Assert-True ($decision.nextRepairDecisionGatePresent -eq $true) 'Next repair decision gate missing.'
Assert-True ($decision.selectedDecision -eq 'ReportingFlagAndBoundaryEvidenceConsistencyRepair') 'Next repair decision unexpected.'
Assert-True ($decision.nextPhase -eq 'LMAX-R163') 'Next phase must be R163.'
Assert-False $decision.liveRetryRecommended 'Live retry must not be recommended.'

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
Assert-False $forbidden.socketOpened 'Socket opened in R162.'
Assert-False $forbidden.tlsOpened 'TLS opened in R162.'
Assert-False $forbidden.fixOpened 'FIX opened in R162.'
Assert-False $forbidden.marketDataRuntimeActionPerformed 'MarketData runtime action performed in R162.'
Assert-False $forbidden.ordersIntroduced 'Order path introduced.'
Assert-False $forbidden.tradingEnabled 'Trading enabled.'
Assert-False $forbidden.schedulerIntroduced 'Scheduler introduced.'
Assert-False $forbidden.pollingLoopIntroduced 'Polling loop introduced.'
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

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R163') 'Next phase recommendation must be R163.'
Assert-True ($next.r163MustRemainNoExternal -eq $true) 'R163 no-external recommendation missing.'
Assert-False $next.liveRetryRecommended 'Next recommendation must not allow live retry.'
Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R162_VALIDATION_PASS') 'Validator result missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r162-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R162 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R162_VALIDATION_PASS'
