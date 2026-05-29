$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'
$phase = 'LMAX-R144'
$profileName = 'UltraMinimalSnapshotPlusUpdatesSecurityIdOnlyGbpusdSingleInstrument'

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

$summary = Read-Json 'phase-lmax-r144-ultraminimal-gbpusd-profile-repair.json'
$contract = Read-Json 'phase-lmax-r144-ultraminimal-profile-contract.json'
$gbpusd = Read-Json 'phase-lmax-r144-gbpusd-only-evidence.json'
$security = Read-Json 'phase-lmax-r144-securityidonly-evidence.json'
$subscription = Read-Json 'phase-lmax-r144-snapshotplusupdates-evidence.json'
$depth = Read-Json 'phase-lmax-r144-mdentrytypes-marketdepth-evidence.json'
$update = Read-Json 'phase-lmax-r144-mdupdatetype-profile-control-evidence.json'
$order = Read-Json 'phase-lmax-r144-repeating-group-field-order-evidence.json'
$prior = Read-Json 'phase-lmax-r144-prior-profiles-preservation.json'
$universe = Read-Json 'phase-lmax-r144-approved-instrument-universe-preservation.json'
$usdjpy = Read-Json 'phase-lmax-r144-usdjpy-caveat-preservation.json'
$sanitization = Read-Json 'phase-lmax-r144-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r144-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r144-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r144-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r144-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r141-repaired-profile-activation.json',
    'phase-lmax-r142-repaired-runtime-reject-review.json',
    'phase-lmax-r143-deep-docs-tag-crosscheck.json',
    'phase-lmax-r143-ultraminimal-gbpusd-profile-decision.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "Required predecessor evidence missing: $name"
}

$r141 = Read-Json 'phase-lmax-r141-repaired-profile-activation.json'
$r142 = Read-Json 'phase-lmax-r142-repaired-runtime-reject-review.json'
$r143 = Read-Json 'phase-lmax-r143-deep-docs-tag-crosscheck.json'
$r143Minimal = Read-Json 'phase-lmax-r143-ultraminimal-gbpusd-profile-decision.json'

Assert-True ($summary.phase -eq $phase) 'R144 summary phase mismatch.'
Assert-True ($summary.classification -eq 'LMAX_R144_PASS_ULTRAMINIMAL_GBPUSD_PROFILE_REPAIR_READY_NO_EXTERNAL') 'R144 classification mismatch.'
Assert-True ($summary.noExternal -eq $true) 'R144 must remain no-external.'
Assert-False $summary.externalActivationAttempted 'R144 external activation detected.'
Assert-False $summary.socketTlsFixMarketDataRuntimeActionAttempted 'R144 runtime boundary action detected.'
Assert-False $summary.liveMarketDataRequestSent 'R144 live MarketDataRequest detected.'
Assert-False $summary.liveMarketDataResponseRead 'R144 live MarketDataResponse read detected.'
Assert-True ($r141.attemptCount -eq 1) 'R141 attemptCount must equal 1.'
Assert-True ($r141.selectedMarketDataRequestProfile -eq 'RepairedSnapshotPlusUpdatesSecurityIdOnlyNonBatched') 'R141 repaired profile evidence missing.'
Assert-True ($r142.classification -eq 'LMAX_R142_PASS_REPAIRED_REJECT_REVIEW_DEEP_DOCS_TAG_CROSSCHECK_RECOMMENDED_NO_EXTERNAL') 'R142 evidence missing.'
Assert-True ($r143.classification -eq 'LMAX_R143_PASS_DEEP_DOCS_TAG_CROSSCHECK_ULTRAMINIMAL_PROFILE_REPAIR_RECOMMENDED_NO_EXTERNAL') 'R143 evidence missing.'
Assert-True ($r143Minimal.recommended -eq $true) 'R143 ultra-minimal recommendation missing.'

Assert-True ($contract.profileExists -eq $true) 'Ultra-minimal GBPUSD profile is missing.'
Assert-True ($contract.profileName -eq $profileName) 'Ultra-minimal profile name mismatch.'
Assert-True ($summary.profileAdded -eq $profileName) 'Profile added evidence missing.'
Assert-True ($contract.selectedForNextDiagnosticRetryReadinessPath -eq $true) 'Ultra-minimal profile is not selected for next diagnostic readiness path.'
Assert-False $contract.selectedForApiWorker 'Ultra-minimal profile must not be selected for API/Worker.'

Assert-True ($gbpusd.gbpusdOnly -eq $true) 'Profile is not GBPUSD-only.'
Assert-True ((@($gbpusd.diagnosticRequestSymbols) -join ',') -eq 'GBPUSD') 'Diagnostic request includes symbols other than GBPUSD.'
Assert-False $gbpusd.eurgbpPresentInDiagnosticRequest 'EURGBP present in diagnostic request.'
Assert-False $gbpusd.audusdPresentInDiagnosticRequest 'AUDUSD present in diagnostic request.'
Assert-False $gbpusd.usdjpyPresentInDiagnosticRequest 'USDJPY present in diagnostic request.'
Assert-False $gbpusd.nonApprovedInstrumentPresent 'Non-approved instrument present.'
Assert-True ($gbpusd.multiRequestActivationSequencingAvoided -eq $true) 'Multi-request sequencing was not avoided.'

Assert-True ($security.identifierMode -eq 'SecurityIdOnly') 'SecurityID-only contract missing.'
Assert-True ($security.securityIdPresent -eq $true) 'SecurityID missing.'
Assert-True ($security.securityIdSourcePresent -eq $true) 'SecurityIDSource missing.'
Assert-True ($security.securityIdSource -eq '8') 'SecurityIDSource weakened.'
Assert-True ($security.gbpusdSecurityId -eq '4002') 'GBPUSD SecurityID mismatch.'
Assert-False $security.symbolTextPresent 'Symbol text present.'
Assert-False $security.internalSymbolPresent 'InternalSymbol present.'

Assert-True ($subscription.snapshotPlusUpdatesSemantics -eq $true) 'SnapshotPlusUpdates semantics missing.'
Assert-True ($subscription.snapshotOnlyAbsent -eq $true) 'SnapshotOnly returned.'
Assert-True ($subscription.legacySnapshotOnlyProfileNotSelected -eq $true) 'Legacy SnapshotOnly profile selected.'
Assert-True ($depth.marketDepthPresent -eq $true) 'MarketDepth evidence missing.'
Assert-True ($depth.marketDepth -eq 1) 'MarketDepth must be one.'
Assert-True ($depth.mdEntryTypesPresent -eq $true) 'MDEntryTypes evidence missing.'
Assert-True ((@($depth.mdEntryTypes) -join ',') -eq 'Bid,Offer') 'MDEntryTypes must be bid/offer only.'
Assert-False $depth.ordersOrTradingEntryTypesPresent 'Order/trading entry type present.'
Assert-True ($update.mdUpdateTypeProfileControlled -eq $true) 'MDUpdateType profile control missing.'
Assert-False $update.mdUpdateTypeIncluded 'MDUpdateType should be omitted for ultra-minimal profile unless docs require it.'
Assert-False $update.uncontrolledMdUpdateType 'Uncontrolled MDUpdateType detected.'
Assert-True ($order.sanitizedEvidenceOnly -eq $true) 'Repeating group/order evidence must be sanitized only.'
Assert-False $order.rawFixSerialized 'Raw FIX serialized in field-order evidence.'
Assert-True ($order.singleRelatedSymGroup -eq $true) 'Single RelatedSym group evidence missing.'
Assert-True ($order.identifierFieldsInsideRelatedSymGroup -eq $true) 'Identifier field group evidence missing.'
Assert-True (@($order.fieldOrderContract).Count -ge 8) 'Field-order contract incomplete.'

Assert-True ($prior.priorProfilesPreserved -eq $true) 'Prior profile preservation missing.'
Assert-True ($prior.legacyRejectedProfile -eq 'LegacySnapshotOnlySymbolAndSecurityBatch') 'Legacy profile preservation missing.'
Assert-True ($prior.repairedProfile -eq 'RepairedSnapshotPlusUpdatesSecurityIdOnlyNonBatched') 'Repaired profile preservation missing.'
Assert-False $prior.legacyRejectedProfileSelectedForNextDiagnosticRetry 'Legacy profile selected for next retry.'
Assert-False $prior.repairedProfileSelectedForNextDiagnosticRetry 'R139 repaired profile selected for next diagnostic retry.'
Assert-True ($universe.approvedInstrumentUniversePreserved -eq $true) 'Approved instrument universe preservation missing.'
Assert-True ((@($universe.approvedInstruments) -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved instrument universe changed.'
Assert-False $universe.nonApprovedInstrumentsAllowed 'Non-approved instruments allowed.'
Assert-True ($usdjpy.usdJpyPreservedInApprovedUniverse -eq $true) 'USDJPY missing from approved universe.'
Assert-True ($usdjpy.usdJpySecurityId -eq '4004') 'USDJPY SecurityID weakened.'
Assert-True ($usdjpy.usdJpySecurityIdSource -eq '8') 'USDJPY SecurityIDSource weakened.'
Assert-True ($usdjpy.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat weakened.'

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
Assert-False $forbidden.externalActivationAttempted 'External activation detected in R144.'
Assert-False $forbidden.socketTlsFixMarketDataRuntimeActionAttempted 'Runtime boundary action detected in R144.'
Assert-False $forbidden.liveMarketDataRequestSent 'Live MarketDataRequest detected in R144.'
Assert-False $forbidden.liveMarketDataResponseRead 'Live MarketDataResponse read detected in R144.'
Assert-False $forbidden.ordersIntroduced 'Order path introduced.'
Assert-False $forbidden.tradingEnabled 'Trading enabled.'
Assert-False $forbidden.schedulerIntroduced 'Scheduler introduced.'
Assert-False $forbidden.pollingLoopIntroduced 'Polling loop introduced.'
Assert-False $forbidden.serviceIntroduced 'Service introduced.'
Assert-False $forbidden.replayIntroduced 'Replay introduced.'
Assert-False $forbidden.shadowReplayIntroduced 'Shadow replay introduced.'
Assert-True ($apiWorker.audit -eq 'PASS') 'API/Worker audit did not pass.'
Assert-True ($apiWorker.apiWorkerGatewayMode -eq 'FakeLmaxGatewayOnly') 'API/Worker FakeLmaxGatewayOnly regressed.'
Assert-False $apiWorker.ultraMinimalProfileSelectedForApiWorker 'Ultra-minimal profile selected for API/Worker.'

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R145') 'Next phase recommendation must be R145.'
Assert-True ($next.r145MustRemainNoExternal -eq $true) 'R145 no-external recommendation missing.'
Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.focusedTests -match '^PASS') 'Focused test evidence missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit test evidence missing.'
Assert-True ($gate.integrationTests -match '^PASS') 'Integration test evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R144_VALIDATION_PASS') 'Validator result missing.'

$operationPath = Join-Path $root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualMarketDataRequestOperation.cs'
$factoryPath = Join-Path $root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualExecutionSurfaceFactory.cs'
$operation = Get-Content $operationPath -Raw
$factory = Get-Content $factoryPath -Raw
Assert-True ($operation.Contains($profileName)) 'Ultra-minimal profile code missing.'
Assert-True ($operation.Contains('GbpusdOnlyDiagnosticProfile')) 'GBPUSD-only diagnostic profile switch missing.'
Assert-True ($operation.Contains('MdUpdateTypeProfileControlled')) 'MDUpdateType profile control code missing.'
Assert-True ($factory.Contains($profileName)) 'Future manual real-bounded path does not select ultra-minimal profile.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r144-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R144 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R144_VALIDATION_PASS'
