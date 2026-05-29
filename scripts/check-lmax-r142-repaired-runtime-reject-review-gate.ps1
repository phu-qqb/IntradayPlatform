$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'
$phase = 'LMAX-R142'

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

$summary = Read-Json 'phase-lmax-r142-repaired-runtime-reject-review.json'
$profile = Read-Json 'phase-lmax-r142-r141-profile-confirmation.json'
$reject = Read-Json 'phase-lmax-r142-r141-sanitized-reject-confirmation.json'
$candidate = Read-Json 'phase-lmax-r142-remaining-marketdatarequest-candidate-issues.json'
$docs = Read-Json 'phase-lmax-r142-docs-tag-crosscheck-decision.json'
$minimal = Read-Json 'phase-lmax-r142-ultra-minimal-single-instrument-profile-decision.json'
$md = Read-Json 'phase-lmax-r142-mdentrytypes-marketdepth-mdupdatetype-decision.json'
$group = Read-Json 'phase-lmax-r142-repeating-group-order-decision.json'
$permission = Read-Json 'phase-lmax-r142-permission-entitlement-secondary-review.json'
$mapping = Read-Json 'phase-lmax-r142-instrument-mapping-secondary-review.json'
$decision = Read-Json 'phase-lmax-r142-next-repair-decision-gate.json'
$sanitization = Read-Json 'phase-lmax-r142-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r142-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r142-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r142-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r142-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r141-repaired-profile-activation.json',
    'phase-lmax-r141-repaired-profile-selection-evidence.json',
    'phase-lmax-r141-marketdata-response-evidence.json',
    'phase-lmax-r139-marketdatarequest-shape-repair.json',
    'phase-lmax-r140-repaired-marketdatarequest-retry-readiness.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "Required predecessor evidence missing: $name"
}

$r141 = Read-Json 'phase-lmax-r141-repaired-profile-activation.json'
$r141Profile = Read-Json 'phase-lmax-r141-repaired-profile-selection-evidence.json'
$r141Response = Read-Json 'phase-lmax-r141-marketdata-response-evidence.json'

Assert-True ($summary.phase -eq $phase) 'R142 summary phase mismatch.'
Assert-True ($summary.classification -eq 'LMAX_R142_PASS_REPAIRED_REJECT_REVIEW_DEEP_DOCS_TAG_CROSSCHECK_RECOMMENDED_NO_EXTERNAL') 'R142 classification mismatch.'
Assert-True ($summary.noExternal -eq $true) 'R142 must remain no-external.'
Assert-False $summary.externalActivationAttempted 'R142 external activation detected.'
Assert-False $summary.socketTlsFixMarketDataRuntimeActionAttempted 'R142 runtime boundary action detected.'
Assert-False $summary.liveMarketDataRequestSent 'R142 live MarketDataRequest detected.'
Assert-False $summary.liveMarketDataResponseRead 'R142 live MarketDataResponse read detected.'

Assert-True ($r141.attemptCount -eq 1) 'R141 attemptCount must equal 1.'
Assert-True ($summary.r141AttemptCount -eq 1) 'R141 attempt count review missing.'
Assert-True ($r141Profile.selectedMarketDataRequestProfile -eq 'RepairedSnapshotPlusUpdatesSecurityIdOnlyNonBatched') 'R141 repaired profile source evidence missing.'
Assert-True ($profile.r141ProfileConfirmationPresent -eq $true) 'R141 repaired profile confirmation missing.'
Assert-True ($profile.selectedMarketDataRequestProfile -eq 'RepairedSnapshotPlusUpdatesSecurityIdOnlyNonBatched') 'R141 selected profile mismatch.'
Assert-False $profile.legacyRejectedProfileSelected 'Legacy rejected profile selected.'
Assert-False $profile.snapshotOnlySelected 'SnapshotOnly selected in R141.'
Assert-True ($profile.snapshotPlusUpdatesSelected -eq $true) 'Snapshot-plus-updates evidence missing.'
Assert-True ($profile.securityIdOnlySelected -eq $true) 'SecurityID-only evidence missing.'
Assert-False $profile.symbolTextIncluded 'Symbol text included in R141.'
Assert-True ($profile.nonBatchedOneRequestPerApprovedInstrument -eq $true) 'Non-batched R141 evidence missing.'

Assert-True ($r141Response.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'R141 response category missing.'
Assert-True ($r141Response.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'R141 sanitized reason source evidence missing.'
Assert-True ($reject.r141SanitizedRejectConfirmationPresent -eq $true) 'R141 sanitized reject confirmation missing.'
Assert-True ($reject.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'R142 reject category mismatch.'
Assert-True ($reject.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'R142 CLI sanitized reason mismatch.'
Assert-True ($reject.sanitizedSessionRejectReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'R142 artifact sanitized reason mismatch.'
Assert-False $reject.firstRepairResolvedReject 'R142 incorrectly claims the first repair resolved the reject.'

Assert-True ($candidate.candidateIssuesReviewed -eq $true) 'Remaining candidate issue review missing.'
Assert-True (@($candidate.candidateIssues).Count -ge 5) 'Remaining candidate issue review incomplete.'
Assert-True ($docs.decision -eq 'RecommendDeepDocsTagCrossCheck') 'Docs/tag decision missing.'
Assert-True ($docs.recommended -eq $true) 'Deep docs/tag cross-check not recommended.'
Assert-True ($docs.repairDeferredUntilCrossCheck -eq $true) 'Repair should be deferred until cross-check.'
Assert-True ($minimal.decision -eq 'PossibleButDeferred') 'Ultra-minimal decision missing.'
Assert-False $minimal.recommendedAsImmediateR143 'Ultra-minimal profile should not be immediate R143 decision.'
Assert-True ($md.decision -eq 'CrossCheckBeforeRepair') 'MDEntryTypes/depth/update decision missing.'
Assert-True ($group.decision -eq 'CrossCheckBeforeRepair') 'Repeating group/order decision missing.'
Assert-True ($permission.permissionEntitlementLeadingCause -eq $false) 'Permission/entitlement incorrectly leading.'
Assert-True ($mapping.instrumentMappingLeadingCause -eq $false) 'Instrument mapping incorrectly leading.'
Assert-True ($decision.decisionGatePresent -eq $true) 'Next repair decision gate missing.'
Assert-True ($decision.nextRepairDecision -eq 'DeepDocsTagCrossCheckRecommended') 'Next repair decision mismatch.'

$approvedSymbols = @($summary.approvedInstruments)
Assert-True (($approvedSymbols -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved instrument scope changed.'
Assert-True ($summary.usdJpySecurityId -eq '4004') 'USDJPY SecurityID weakened.'
Assert-True ($summary.usdJpySecurityIdSource -eq '8') 'USDJPY SecurityIDSource weakened.'
Assert-True ($summary.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat weakened.'
Assert-True ($mapping.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat mapping review missing.'

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
Assert-False $forbidden.externalActivationAttempted 'External activation detected in R142.'
Assert-False $forbidden.socketTlsFixMarketDataRuntimeActionAttempted 'Runtime boundary action detected in R142.'
Assert-False $forbidden.ordersIntroduced 'Order path introduced.'
Assert-False $forbidden.tradingEnabled 'Trading enabled.'
Assert-False $forbidden.schedulerIntroduced 'Scheduler introduced.'
Assert-False $forbidden.pollingLoopIntroduced 'Polling loop introduced.'
Assert-False $forbidden.serviceIntroduced 'Service introduced.'
Assert-False $forbidden.replayIntroduced 'Replay introduced.'
Assert-False $forbidden.shadowReplayIntroduced 'Shadow replay introduced.'
Assert-True ($apiWorker.audit -eq 'PASS') 'API/Worker audit did not pass.'
Assert-True ($apiWorker.apiWorkerGatewayMode -eq 'FakeLmaxGatewayOnly') 'API/Worker FakeLmaxGatewayOnly regressed.'

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R143') 'Next phase recommendation must be R143.'
Assert-True ($next.r143MustRemainNoExternal -eq $true) 'R143 no-external recommendation missing.'
Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.focusedTests -match '^(PASS|NOT_REQUIRED)') 'Focused test evidence missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit test evidence missing.'
Assert-True ($gate.integrationTests -match '^PASS') 'Integration test evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R142_VALIDATION_PASS') 'Validator result missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r142-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R142 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R142_VALIDATION_PASS'
