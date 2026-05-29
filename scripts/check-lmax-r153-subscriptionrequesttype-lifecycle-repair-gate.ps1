$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'
$phase = 'LMAX-R153'
$profileName = 'UltraMinimalSnapshotPlusUpdatesFreshLifecycleSymbolAndSecurityIdGbpusdSingleInstrument'

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

$summary = Read-Json 'phase-lmax-r153-subscriptionrequesttype-lifecycle-repair.json'
$contract = Read-Json 'phase-lmax-r153-lifecycle-profile-contract.json'
$selection = Read-Json 'phase-lmax-r153-selected-profile-decision.json'
$prior = Read-Json 'phase-lmax-r153-prior-profiles-preservation.json'
$gbpusd = Read-Json 'phase-lmax-r153-gbpusd-symbol-securityid-preservation.json'
$lifecycle = Read-Json 'phase-lmax-r153-snapshotplusupdates-lifecycle-evidence.json'
$secondary = Read-Json 'phase-lmax-r153-secondary-audits-preservation.json'
$universe = Read-Json 'phase-lmax-r153-approved-universe-preservation.json'
$usdjpy = Read-Json 'phase-lmax-r153-usdjpy-caveat-preservation.json'
$sanitization = Read-Json 'phase-lmax-r153-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r153-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r153-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r153-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r153-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r151-symbol-securityid-gbpusd-activation.json',
    'phase-lmax-r152-symbol-securityid-gbpusd-reject-review.json',
    'phase-lmax-r152-subscriptionrequesttype-lifecycle-decision.json',
    'phase-lmax-r152-next-repair-decision-gate.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "Required predecessor evidence missing: $name"
}

$r151 = Read-Json 'phase-lmax-r151-symbol-securityid-gbpusd-activation.json'
$r152 = Read-Json 'phase-lmax-r152-symbol-securityid-gbpusd-reject-review.json'
$r152Lifecycle = Read-Json 'phase-lmax-r152-subscriptionrequesttype-lifecycle-decision.json'

Assert-True ($r151.attemptCount -eq 1) 'R151 attemptCount must equal 1.'
Assert-True ($r151.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'R151 sanitized reason evidence missing.'
Assert-True ($r152.classification -eq 'LMAX_R152_PASS_SYMBOL_SECURITYID_REJECT_REVIEW_SUBSCRIPTION_LIFECYCLE_REPAIR_RECOMMENDED_NO_EXTERNAL') 'R152 evidence missing.'
Assert-True ($r152Lifecycle.recommended -eq $true) 'R152 lifecycle repair recommendation missing.'

Assert-True ($summary.phase -eq $phase) 'R153 summary phase mismatch.'
Assert-True ($summary.classification -eq 'LMAX_R153_PASS_SUBSCRIPTION_LIFECYCLE_REPAIR_READY_NO_EXTERNAL') 'R153 classification mismatch.'
Assert-True ($summary.noExternal -eq $true) 'R153 must remain no-external.'
Assert-False $summary.externalActivationAttempted 'R153 external activation detected.'
Assert-False $summary.socketTlsFixMarketDataRuntimeActionAttempted 'R153 socket/TLS/FIX/MarketData runtime action detected.'
Assert-False $summary.liveMarketDataRequestSent 'R153 live MarketDataRequest detected.'
Assert-False $summary.liveMarketDataResponseRead 'R153 live MarketDataResponse read detected.'
Assert-False $summary.apiWorkerStarted 'R153 API/Worker startup detected.'
Assert-False $summary.schedulerServicePollingStarted 'R153 scheduler/service/polling startup detected.'
Assert-False $summary.ordersTradingReplayShadowReplayIntroduced 'R153 order/trading/replay path detected.'
Assert-True (@($summary.createdLifecycleProfiles).Contains($profileName)) 'R153 lifecycle profile missing.'
Assert-True ($summary.selectedNextDiagnosticProfile -eq $profileName) 'R153 selected profile mismatch.'
Assert-True ($summary.selectedProfileCount -eq 1) 'R153 must select exactly one profile.'

Assert-True ($contract.lifecycleProfileContractPresent -eq $true) 'Lifecycle profile contract missing.'
Assert-True ($contract.profileName -eq $profileName) 'Lifecycle profile contract name mismatch.'
Assert-True ($contract.profileExists -eq $true) 'Lifecycle profile does not exist.'
Assert-True ($contract.subscriptionRequestType -eq 'SnapshotPlusUpdates') 'SnapshotPlusUpdates not preserved.'
Assert-True ($contract.freshLifecycleMdReqIdCategory -eq 'LMAX_READONLY_R153') 'Fresh lifecycle MDReqID category missing.'
Assert-True ($contract.gbpusdOnly -eq $true) 'Lifecycle profile is not GBPUSD-only.'
Assert-True ((@($contract.diagnosticRequestSymbols) -join ',') -eq 'GBPUSD') 'Lifecycle profile has wrong diagnostic symbols.'
Assert-True ($contract.identifierCombination -eq 'SymbolPlusSecurityId') 'Identifier combination weakened.'
Assert-True ($contract.symbolIncluded -eq $true) 'Symbol component missing.'
Assert-True ($contract.securityIdIncluded -eq $true) 'SecurityID component missing.'
Assert-True ($contract.securityId -eq '4002') 'GBPUSD SecurityID mismatch.'
Assert-True ($contract.securityIdSourceIncluded -eq $true) 'SecurityIDSource missing.'
Assert-True ($contract.securityIdSource -eq '8') 'GBPUSD SecurityIDSource mismatch.'
Assert-False $contract.internalSymbolIncluded 'InternalSymbol included.'
Assert-True ($contract.snapshotPlusUpdates -eq $true) 'SnapshotPlusUpdates missing.'
Assert-False $contract.snapshotOnly 'SnapshotOnly returned.'
Assert-True ($contract.mdUpdateTypeProfileControlled -eq $true) 'MDUpdateType profile control missing.'
Assert-False $contract.mdUpdateTypeIncluded 'MDUpdateType included.'
Assert-True ($contract.marketDepth -eq 1) 'MarketDepth must equal one.'
Assert-True ((@($contract.mdEntryTypes) -join ',') -eq 'Bid,Offer') 'MDEntryTypes must be bid/offer only.'
Assert-True ($contract.oneRequestOnly -eq $true) 'Lifecycle profile must use one request only.'
Assert-False $contract.rawFixSerialized 'Raw FIX serialized in lifecycle contract.'

Assert-True ($selection.selectedProfileDecisionPresent -eq $true) 'Selected profile decision missing.'
Assert-True ($selection.selectedNextDiagnosticProfile -eq $profileName) 'Selected next diagnostic profile mismatch.'
Assert-True ($selection.selectedProfileCount -eq 1) 'Selected profile count must be one.'
Assert-False $selection.multipleProfilesSelectedForNextRetry 'Multiple profiles selected.'
Assert-False $selection.multipleRequestsSelectedForNextRetry 'Multiple requests selected.'
Assert-True ($prior.priorProfilesPreserved -eq $true) 'Prior profile preservation missing.'
foreach ($priorProfile in @(
    'UltraMinimalSnapshotPlusUpdatesSecurityIdOnlyGbpusdSingleInstrument',
    'UltraMinimalSnapshotPlusUpdatesSymbolOnlyGbpusdSingleInstrument',
    'UltraMinimalSnapshotPlusUpdatesSymbolAndSecurityIdGbpusdSingleInstrument',
    'RepairedSnapshotPlusUpdatesSecurityIdOnlyNonBatched',
    'LegacySnapshotOnlySymbolAndSecurityBatch')) {
    $match = @($prior.profiles | Where-Object { $_.profileName -eq $priorProfile })
    Assert-True ($match.Count -eq 1) "Prior profile preservation missing for $priorProfile."
    Assert-False $match[0].selectedForNextDiagnosticRetry "Prior profile unexpectedly selected: $priorProfile."
}

Assert-True ($gbpusd.gbpusdSymbolSecurityIdPreserved -eq $true) 'GBPUSD Symbol+SecurityID preservation missing.'
Assert-True ($gbpusd.gbpusdOnly -eq $true) 'Diagnostic profile is not GBPUSD-only.'
Assert-False $gbpusd.eurgbpPresentInDiagnosticRequest 'EURGBP present in diagnostic request.'
Assert-False $gbpusd.audusdPresentInDiagnosticRequest 'AUDUSD present in diagnostic request.'
Assert-False $gbpusd.usdjpyPresentInDiagnosticRequest 'USDJPY present in diagnostic request.'
Assert-False $gbpusd.nonApprovedInstrumentPresent 'Non-approved instrument present.'
Assert-True ($gbpusd.identifierCombination -eq 'SymbolPlusSecurityId') 'Identifier combination weakened.'
Assert-True ($gbpusd.securityId -eq '4002') 'SecurityID mismatch.'
Assert-True ($gbpusd.securityIdSource -eq '8') 'SecurityIDSource mismatch.'
Assert-False $gbpusd.internalSymbolIncluded 'InternalSymbol included.'

Assert-True ($lifecycle.snapshotPlusUpdatesLifecycleEvidencePresent -eq $true) 'SnapshotPlusUpdates lifecycle evidence missing.'
Assert-True ($lifecycle.snapshotPlusUpdatesPreserved -eq $true) 'SnapshotPlusUpdates weakened.'
Assert-True ($lifecycle.snapshotOnlyAbsent -eq $true) 'SnapshotOnly returned.'
Assert-True ($lifecycle.freshSubscriptionLifecycleProfile -eq $true) 'Fresh lifecycle profile evidence missing.'
Assert-True ($lifecycle.subscriptionRequestTypeLifecycleRepairProfileControlled -eq $true) 'Lifecycle profile control missing.'
Assert-False $lifecycle.rawFixSerialized 'Raw FIX serialized in lifecycle evidence.'
Assert-True ($secondary.secondaryAuditsPreserved -eq $true) 'Secondary audits missing.'
Assert-True ($secondary.repeatingGroupFieldOrderSanitizedContractPresent -eq $true) 'Repeating group/order evidence missing.'
Assert-True ($secondary.mdReqIdMandatoryTagAuditIncluded -eq $true) 'MDReqID/mandatory tag audit missing.'
Assert-True ($secondary.marketDepthMdEntryTypesPreserved -eq $true) 'MarketDepth/MDEntryTypes preservation missing.'
Assert-True ($secondary.marketDepth -eq 1) 'MarketDepth must equal one.'
Assert-True ((@($secondary.mdEntryTypes) -join ',') -eq 'Bid,Offer') 'MDEntryTypes must be bid/offer only.'
Assert-True ($secondary.mdUpdateTypeProfileControlled -eq $true) 'MDUpdateType profile control missing.'
Assert-False $secondary.mdUpdateTypeIncluded 'MDUpdateType included.'
Assert-False $secondary.rawFixSerialized 'Raw FIX serialized in secondary evidence.'

Assert-True ($universe.approvedUniversePreserved -eq $true) 'Approved universe preservation missing.'
Assert-True ((@($universe.approvedInstruments) -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved universe changed.'
Assert-False $universe.nonApprovedInstrumentsAllowed 'Non-approved instruments allowed.'
Assert-True ($usdjpy.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat weakened.'
Assert-True ($usdjpy.usdJpySecurityId -eq '4004') 'USDJPY SecurityID weakened.'
Assert-True ($usdjpy.usdJpySecurityIdSource -eq '8') 'USDJPY SecurityIDSource weakened.'
Assert-True ($summary.credentialValuesReturned -eq $false) 'credentialValuesReturned must remain false.'

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
Assert-False $forbidden.externalActivationAttempted 'External activation detected.'
Assert-False $forbidden.socketTlsFixMarketDataRuntimeActionAttempted 'Runtime boundary action detected.'
Assert-False $forbidden.liveMarketDataRequestSent 'Live MarketDataRequest detected.'
Assert-False $forbidden.liveMarketDataResponseRead 'Live MarketDataResponse read detected.'
Assert-False $forbidden.ordersIntroduced 'Order path introduced.'
Assert-False $forbidden.tradingEnabled 'Trading enabled.'
Assert-False $forbidden.schedulerIntroduced 'Scheduler introduced.'
Assert-False $forbidden.pollingLoopIntroduced 'Polling loop introduced.'
Assert-False $forbidden.serviceIntroduced 'Service introduced.'
Assert-False $forbidden.replayIntroduced 'Replay introduced.'
Assert-False $forbidden.shadowReplayIntroduced 'Shadow replay introduced.'
Assert-False $forbidden.apiStartupAttempted 'API startup attempted.'
Assert-False $forbidden.workerStartupAttempted 'Worker startup attempted.'
Assert-True ($apiWorker.audit -eq 'PASS') 'API/Worker audit did not pass.'
Assert-True ($apiWorker.apiWorkerGatewayMode -eq 'FakeLmaxGatewayOnly') 'API/Worker FakeLmaxGatewayOnly regressed.'
Assert-False $apiWorker.apiStartupAttempted 'API startup attempted.'
Assert-False $apiWorker.workerStartupAttempted 'Worker startup attempted.'
Assert-False $apiWorker.lifecycleProfileSelectedForApiWorker 'Lifecycle profile selected for API/Worker.'

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R154') 'Next phase recommendation must be R154.'
Assert-True ($next.r154MustRemainNoExternal -eq $true) 'R154 no-external recommendation missing.'
Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.focusedTests -match '^PASS') 'Focused test evidence missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit test evidence missing.'
Assert-True ($gate.integrationTests -match '^PASS') 'Integration test evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R153_VALIDATION_PASS') 'Validator result missing.'

$operationPath = Join-Path $root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualMarketDataRequestOperation.cs'
$factoryPath = Join-Path $root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualExecutionSurfaceFactory.cs'
$operation = Get-Content $operationPath -Raw
$factory = Get-Content $factoryPath -Raw
Assert-True ($operation.Contains($profileName)) 'Lifecycle profile code missing.'
Assert-True ($operation.Contains('LMAX_READONLY_R153_')) 'R153 lifecycle MDReqID category missing from code.'
Assert-True ($factory.Contains($profileName)) 'Future manual real-bounded path does not select lifecycle profile.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r153-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R153 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R153_VALIDATION_PASS'
