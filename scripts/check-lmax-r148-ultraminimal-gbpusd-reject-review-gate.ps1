$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'
$phase = 'LMAX-R148'

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

$summary = Read-Json 'phase-lmax-r148-ultraminimal-gbpusd-reject-review.json'
$profile = Read-Json 'phase-lmax-r148-r147-profile-confirmation.json'
$reject = Read-Json 'phase-lmax-r148-r147-sanitized-reject-confirmation.json'
$eliminated = Read-Json 'phase-lmax-r148-eliminated-hypotheses-after-r147.json'
$remaining = Read-Json 'phase-lmax-r148-remaining-candidate-issues.json'
$identifier = Read-Json 'phase-lmax-r148-identifier-combination-decision.json'
$subscription = Read-Json 'phase-lmax-r148-subscriptionrequesttype-lifecycle-decision.json'
$order = Read-Json 'phase-lmax-r148-repeating-group-field-order-decision.json'
$mdreq = Read-Json 'phase-lmax-r148-mdreqid-mandatory-tag-decision.json'
$permission = Read-Json 'phase-lmax-r148-permission-entitlement-reassessment.json'
$support = Read-Json 'phase-lmax-r148-support-evidence-package-decision.json'
$decision = Read-Json 'phase-lmax-r148-next-repair-decision-gate.json'
$sanitization = Read-Json 'phase-lmax-r148-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r148-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r148-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r148-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r148-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r147-ultraminimal-gbpusd-activation.json',
    'phase-lmax-r147-ultraminimal-profile-selection-evidence.json',
    'phase-lmax-r147-marketdata-response-evidence.json',
    'phase-lmax-r147-boundary-evidence.json',
    'phase-lmax-r144-ultraminimal-gbpusd-profile-repair.json',
    'phase-lmax-r145-ultraminimal-gbpusd-retry-readiness.json',
    'phase-lmax-r143-deep-docs-tag-crosscheck.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "Required predecessor evidence missing: $name"
}

$r147 = Read-Json 'phase-lmax-r147-ultraminimal-gbpusd-activation.json'
$r147Profile = Read-Json 'phase-lmax-r147-ultraminimal-profile-selection-evidence.json'
$r147Reject = Read-Json 'phase-lmax-r147-marketdata-response-evidence.json'

Assert-True ($summary.phase -eq $phase) 'R148 summary phase mismatch.'
Assert-True ($summary.classification -eq 'LMAX_R148_PASS_ULTRAMINIMAL_REJECT_REVIEW_IDENTIFIER_REPAIR_RECOMMENDED_NO_EXTERNAL') 'R148 classification mismatch.'
Assert-True ($summary.noExternal -eq $true) 'R148 must remain no-external.'
Assert-False $summary.externalActivationAttempted 'R148 external activation detected.'
Assert-False $summary.socketTlsFixMarketDataRuntimeActionAttempted 'R148 runtime boundary action detected.'
Assert-False $summary.liveMarketDataRequestSent 'R148 live MarketDataRequest detected.'
Assert-False $summary.liveMarketDataResponseRead 'R148 live MarketDataResponse read detected.'

Assert-True ($r147.attemptCount -eq 1) 'R147 attemptCount must equal 1.'
Assert-True ($summary.r147AttemptCount -eq 1) 'R147 attemptCount review invalid.'
Assert-True ($r147.selectedProfile -eq 'UltraMinimalSnapshotPlusUpdatesSecurityIdOnlyGbpusdSingleInstrument') 'R147 selected profile evidence missing.'
Assert-True ($r147Profile.selectedMarketDataRequestProfile -eq 'UltraMinimalSnapshotPlusUpdatesSecurityIdOnlyGbpusdSingleInstrument') 'R147 profile selection evidence missing.'
Assert-True ($profile.r147ProfileConfirmationPresent -eq $true) 'R147 profile confirmation missing.'
Assert-True ($profile.r147SelectedProfile -eq 'UltraMinimalSnapshotPlusUpdatesSecurityIdOnlyGbpusdSingleInstrument') 'R147 ultra-minimal profile confirmation mismatch.'
Assert-True ($profile.gbpusdOnly -eq $true) 'R147 profile not confirmed GBPUSD-only.'
Assert-True ((@($profile.diagnosticRequestSymbols) -join ',') -eq 'GBPUSD') 'R147 diagnostic request symbols mismatch.'
Assert-False $profile.symbolTextIncluded 'R147 Symbol text unexpectedly present.'
Assert-False $profile.internalSymbolIncluded 'R147 InternalSymbol unexpectedly present.'
Assert-True ($profile.snapshotPlusUpdatesSelected -eq $true) 'R147 SnapshotPlusUpdates confirmation missing.'
Assert-False $profile.snapshotOnlySelected 'R147 SnapshotOnly unexpectedly selected.'
Assert-False $profile.mdUpdateTypeIncluded 'R147 MDUpdateType unexpectedly included.'
Assert-True ($profile.marketDepth -eq 1) 'R147 MarketDepth confirmation missing.'
Assert-True ((@($profile.mdEntryTypes) -join ',') -eq 'Bid,Offer') 'R147 MDEntryTypes confirmation mismatch.'
Assert-True ($profile.oneRequestOnly -eq $true) 'R147 one-request confirmation missing.'

Assert-True ($r147Reject.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'R147 sanitized reject evidence missing.'
Assert-True ($r147Reject.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'R147 sanitized reason unexpected.'
Assert-True ($reject.r147SanitizedRejectConfirmationPresent -eq $true) 'R147 sanitized reject confirmation missing.'
Assert-True ($reject.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'R147 reject category confirmation mismatch.'
Assert-True ($reject.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'R147 CLI sanitized reason confirmation mismatch.'
Assert-True ($reject.sanitizedSessionRejectReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'R147 artifact sanitized reason confirmation mismatch.'
Assert-False $reject.rawRejectTextSerialized 'Raw reject text serialized in reject confirmation.'
Assert-False $reject.rawFixSerialized 'Raw FIX serialized in reject confirmation.'

Assert-True ($eliminated.eliminatedHypothesesReviewPresent -eq $true) 'Eliminated hypotheses review missing.'
Assert-True (@($eliminated.eliminatedOrDemoted).Count -ge 6) 'Eliminated hypotheses review incomplete.'
Assert-True (@($eliminated.eliminatedOrDemoted | Where-Object { $_.hypothesis -eq 'all_in_one_batching' }).Count -eq 1) 'Batching hypothesis not reviewed.'
Assert-True (@($eliminated.eliminatedOrDemoted | Where-Object { $_.hypothesis -eq 'symbol_text_or_internalsymbol_presence' }).Count -eq 1) 'Symbol/InternalSymbol hypothesis not reviewed.'
Assert-True (@($eliminated.eliminatedOrDemoted | Where-Object { $_.hypothesis -eq 'mdupdatetype_presence' }).Count -eq 1) 'MDUpdateType hypothesis not reviewed.'

Assert-True ($remaining.remainingCandidateIssueReviewPresent -eq $true) 'Remaining candidate issue review missing.'
Assert-True (@($remaining.remainingCandidates).Count -ge 5) 'Remaining candidate issue review incomplete.'
Assert-True (@($remaining.remainingCandidates | Where-Object { $_.issue -eq 'identifier_combination' -and $_.status -eq 'leading' }).Count -eq 1) 'Identifier combination not leading.'

Assert-True ($identifier.decision -eq 'RecommendIdentifierCombinationRepair') 'Identifier-combination decision missing.'
Assert-True ($identifier.recommended -eq $true) 'Identifier-combination repair not recommended.'
Assert-True ($identifier.noExternalRepairOnly -eq $true) 'Identifier repair must be no-external.'
Assert-True (@($identifier.candidateNextProfiles).Count -ge 2) 'Identifier candidate profiles missing.'
Assert-True ($subscription.decision -eq 'SecondaryDeferUntilIdentifierRepairReviewed') 'Subscription/lifecycle decision missing.'
Assert-True ($subscription.recommendedAsR149Primary -eq $false) 'Subscription/lifecycle incorrectly primary.'
Assert-True ($order.decision -eq 'SecondaryCrossCheckWithinIdentifierRepair') 'Repeating-group/order decision missing.'
Assert-True ($mdreq.decision -eq 'SecondaryAuditInsideIdentifierRepair') 'MDReqID/mandatory-tag decision missing.'
Assert-True ($permission.decision -eq 'SecondaryNotLeading') 'Permission/entitlement reassessment missing.'
Assert-True ($permission.permissionEntitlementLeadingCause -eq $false) 'Permission/entitlement incorrectly leading.'
Assert-True ($support.decision -eq 'NotYetRecommended') 'Support package decision missing.'
Assert-True ($support.supportPackageRecommendedAsR149Primary -eq $false) 'Support package incorrectly primary.'
Assert-True ($decision.decisionGatePresent -eq $true) 'Next repair decision gate missing.'
Assert-True ($decision.nextRepairDecision -eq 'IdentifierCombinationRepairRecommended') 'Next repair decision mismatch.'
Assert-True ($decision.recommendedNextPhase -eq 'LMAX-R149') 'Recommended next phase mismatch.'
Assert-True ($decision.r149MustRemainNoExternal -eq $true) 'R149 no-external condition missing.'

Assert-True ((@($summary.approvedUniverse) -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved universe weakened.'
Assert-True ($summary.usdJpySecurityId -eq '4004') 'USDJPY SecurityID weakened.'
Assert-True ($summary.usdJpySecurityIdSource -eq '8') 'USDJPY SecurityIDSource weakened.'
Assert-True ($summary.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat weakened.'
Assert-True ($summary.credentialValuesReturned -eq $false) 'credentialValuesReturned must remain false.'
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
Assert-False $forbidden.externalActivationAttempted 'External activation detected in R148.'
Assert-False $forbidden.socketTlsFixMarketDataRuntimeActionAttempted 'Runtime boundary action detected in R148.'
Assert-False $forbidden.liveMarketDataRequestSent 'Live MarketDataRequest detected in R148.'
Assert-False $forbidden.liveMarketDataResponseRead 'Live MarketDataResponse read detected in R148.'
Assert-False $forbidden.ordersIntroduced 'Order path introduced.'
Assert-False $forbidden.tradingEnabled 'Trading enabled.'
Assert-False $forbidden.schedulerIntroduced 'Scheduler introduced.'
Assert-False $forbidden.pollingLoopIntroduced 'Polling loop introduced.'
Assert-False $forbidden.serviceIntroduced 'Service introduced.'
Assert-False $forbidden.replayIntroduced 'Replay introduced.'
Assert-False $forbidden.shadowReplayIntroduced 'Shadow replay introduced.'
Assert-True ($apiWorker.audit -eq 'PASS') 'API/Worker audit did not pass.'
Assert-True ($apiWorker.apiWorkerGatewayMode -eq 'FakeLmaxGatewayOnly') 'API/Worker FakeLmaxGatewayOnly regressed.'

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R149') 'Next phase recommendation must be R149.'
Assert-True ($next.r149MustRemainNoExternal -eq $true) 'R149 no-external recommendation missing.'
Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.focusedTests -match '^(PASS|NOT_REQUIRED)') 'Focused test evidence missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit test evidence missing.'
Assert-True ($gate.integrationTests -match '^PASS') 'Integration test evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R148_VALIDATION_PASS') 'Validator result missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r148-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R148 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R148_VALIDATION_PASS'
