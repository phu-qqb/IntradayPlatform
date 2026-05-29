$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'
$phase = 'LMAX-R143'

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

$summary = Read-Json 'phase-lmax-r143-deep-docs-tag-crosscheck.json'
$current = Read-Json 'phase-lmax-r143-current-repaired-profile-tag-matrix.json'
$legacy = Read-Json 'phase-lmax-r143-legacy-rejected-profile-tag-matrix.json'
$docs = Read-Json 'phase-lmax-r143-lmax-docs-requirements-matrix.json'
$options = Read-Json 'phase-lmax-r143-candidate-corrected-profile-options.json'
$subscription = Read-Json 'phase-lmax-r143-subscriptionrequesttype-decision.json'
$update = Read-Json 'phase-lmax-r143-mdupdatetype-decision.json'
$depth = Read-Json 'phase-lmax-r143-marketdepth-mdentrytypes-decision.json'
$identifier = Read-Json 'phase-lmax-r143-relatedsym-identifier-decision.json'
$order = Read-Json 'phase-lmax-r143-repeating-group-field-order-decision.json'
$minimal = Read-Json 'phase-lmax-r143-ultraminimal-gbpusd-profile-decision.json'
$permission = Read-Json 'phase-lmax-r143-permission-entitlement-secondary-review.json'
$mapping = Read-Json 'phase-lmax-r143-instrument-mapping-secondary-review.json'
$decision = Read-Json 'phase-lmax-r143-next-repair-decision-gate.json'
$sanitization = Read-Json 'phase-lmax-r143-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r143-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r143-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r143-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r143-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r141-repaired-profile-activation.json',
    'phase-lmax-r141-marketdata-response-evidence.json',
    'phase-lmax-r142-repaired-runtime-reject-review.json',
    'phase-lmax-r142-docs-tag-crosscheck-decision.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "Required predecessor evidence missing: $name"
}

$r141 = Read-Json 'phase-lmax-r141-repaired-profile-activation.json'
$r141Response = Read-Json 'phase-lmax-r141-marketdata-response-evidence.json'
$r142 = Read-Json 'phase-lmax-r142-repaired-runtime-reject-review.json'

Assert-True ($summary.phase -eq $phase) 'R143 summary phase mismatch.'
Assert-True ($summary.classification -eq 'LMAX_R143_PASS_DEEP_DOCS_TAG_CROSSCHECK_ULTRAMINIMAL_PROFILE_REPAIR_RECOMMENDED_NO_EXTERNAL') 'R143 classification mismatch.'
Assert-True ($summary.noExternal -eq $true) 'R143 must remain no-external.'
Assert-False $summary.externalActivationAttempted 'R143 external activation detected.'
Assert-False $summary.socketTlsFixMarketDataRuntimeActionAttempted 'R143 runtime boundary action detected.'
Assert-False $summary.liveMarketDataRequestSent 'R143 live MarketDataRequest detected.'
Assert-False $summary.liveMarketDataResponseRead 'R143 live MarketDataResponse read detected.'
Assert-True ($r141.attemptCount -eq 1) 'R141 attemptCount must equal 1.'
Assert-True ($r141.selectedMarketDataRequestProfile -eq 'RepairedSnapshotPlusUpdatesSecurityIdOnlyNonBatched') 'R141 repaired profile evidence missing.'
Assert-True ($r141Response.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'R141 repaired reject evidence missing.'
Assert-True ($r142.classification -eq 'LMAX_R142_PASS_REPAIRED_REJECT_REVIEW_DEEP_DOCS_TAG_CROSSCHECK_RECOMMENDED_NO_EXTERNAL') 'R142 deep docs/tag recommendation missing.'

Assert-True ($summary.docsTagMatrixPresent -eq $true) 'Docs/tag matrix missing.'
Assert-True ($current.profile -eq 'RepairedSnapshotPlusUpdatesSecurityIdOnlyNonBatched') 'Current repaired profile tag matrix missing.'
Assert-True (@($current.sanitizedTagMatrix).Count -ge 8) 'Current repaired profile tag matrix incomplete.'
Assert-True ($legacy.legacyRejectedEvidence -eq $true) 'Legacy rejected profile tag matrix missing.'
Assert-True ($docs.requirementsMatrixPresent -eq $true) 'LMAX docs requirements matrix missing.'
Assert-True (@($docs.requirements).Count -ge 7) 'LMAX docs requirements matrix incomplete.'
Assert-True ($options.candidateProfileOptionsPresent -eq $true) 'Candidate corrected profile options missing.'
Assert-True (@($options.options).Count -ge 3) 'Candidate corrected profile options incomplete.'
Assert-True (@($options.options | Where-Object { $_.name -eq 'UltraMinimalGbpusdSnapshotPlusUpdatesSecurityIdOnly' -and $_.recommended -eq $true }).Count -eq 1) 'Recommended ultra-minimal profile option missing.'

Assert-True ($subscription.decision -eq 'KeepSnapshotPlusUpdatesForNextRepair') 'SubscriptionRequestType decision missing.'
Assert-True ($update.decision -eq 'MakeMdUpdateTypeProfileControlledOrOmitInUltraMinimalRepair') 'MDUpdateType decision missing.'
Assert-True ($depth.decision -eq 'PreserveMarketDepthOneAndBidOfferButCrossCheckGroupOrder') 'MarketDepth/MDEntryTypes decision missing.'
Assert-True ($identifier.decision -eq 'UseSingleInstrumentSecurityIdOnlyForNextRepair') 'RelatedSym/identifier decision missing.'
Assert-True ($order.decision -eq 'ValidateDocsAlignedSanitizedOrderInUltraMinimalRepair') 'Repeating group/order decision missing.'
Assert-True ($minimal.decision -eq 'RecommendUltraMinimalGbpusdProfileRepair') 'Ultra-minimal GBPUSD profile decision missing.'
Assert-True ($minimal.recommended -eq $true) 'Ultra-minimal GBPUSD repair not recommended.'
Assert-True ($permission.permissionEntitlementLeadingCause -eq $false) 'Permission/entitlement incorrectly leading.'
Assert-True ($mapping.instrumentMappingLeadingCause -eq $false) 'Instrument mapping incorrectly leading.'
Assert-True ($decision.decisionGatePresent -eq $true) 'Next repair decision gate missing.'
Assert-True ($decision.nextRepairDecision -eq 'UltraMinimalSingleInstrumentProfileRepairRecommended') 'Next repair decision mismatch.'

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
Assert-False $forbidden.externalActivationAttempted 'External activation detected in R143.'
Assert-False $forbidden.socketTlsFixMarketDataRuntimeActionAttempted 'Runtime boundary action detected in R143.'
Assert-False $forbidden.ordersIntroduced 'Order path introduced.'
Assert-False $forbidden.tradingEnabled 'Trading enabled.'
Assert-False $forbidden.schedulerIntroduced 'Scheduler introduced.'
Assert-False $forbidden.pollingLoopIntroduced 'Polling loop introduced.'
Assert-False $forbidden.serviceIntroduced 'Service introduced.'
Assert-False $forbidden.replayIntroduced 'Replay introduced.'
Assert-False $forbidden.shadowReplayIntroduced 'Shadow replay introduced.'
Assert-True ($apiWorker.audit -eq 'PASS') 'API/Worker audit did not pass.'
Assert-True ($apiWorker.apiWorkerGatewayMode -eq 'FakeLmaxGatewayOnly') 'API/Worker FakeLmaxGatewayOnly regressed.'

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R144') 'Next phase recommendation must be R144.'
Assert-True ($next.r144MustRemainNoExternal -eq $true) 'R144 no-external recommendation missing.'
Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.focusedTests -match '^(PASS|NOT_REQUIRED)') 'Focused test evidence missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit test evidence missing.'
Assert-True ($gate.integrationTests -match '^PASS') 'Integration test evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R143_VALIDATION_PASS') 'Validator result missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r143-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R143 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R143_VALIDATION_PASS'
