$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'
$phase = 'LMAX-R149'
$symbolOnlyProfile = 'UltraMinimalSnapshotPlusUpdatesSymbolOnlyGbpusdSingleInstrument'
$symbolSecurityProfile = 'UltraMinimalSnapshotPlusUpdatesSymbolAndSecurityIdGbpusdSingleInstrument'
$securityIdOnlyProfile = 'UltraMinimalSnapshotPlusUpdatesSecurityIdOnlyGbpusdSingleInstrument'

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

$summary = Read-Json 'phase-lmax-r149-identifier-combination-repair.json'
$prior = Read-Json 'phase-lmax-r149-securityidonly-prior-profile-preservation.json'
$symbolOnly = Read-Json 'phase-lmax-r149-symbolonly-profile-contract.json'
$symbolSecurity = Read-Json 'phase-lmax-r149-symbol-securityid-profile-contract.json'
$matrix = Read-Json 'phase-lmax-r149-identifier-combination-options-matrix.json'
$gbpusd = Read-Json 'phase-lmax-r149-gbpusd-only-evidence.json'
$subscription = Read-Json 'phase-lmax-r149-snapshotplusupdates-preservation.json'
$depth = Read-Json 'phase-lmax-r149-mdentrytypes-marketdepth-preservation.json'
$update = Read-Json 'phase-lmax-r149-mdupdatetype-profile-control-preservation.json'
$order = Read-Json 'phase-lmax-r149-repeating-group-field-order-evidence.json'
$universe = Read-Json 'phase-lmax-r149-approved-universe-preservation.json'
$usdjpy = Read-Json 'phase-lmax-r149-usdjpy-caveat-preservation.json'
$selection = Read-Json 'phase-lmax-r149-next-diagnostic-profile-selection-decision.json'
$sanitization = Read-Json 'phase-lmax-r149-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r149-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r149-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r149-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r149-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r147-ultraminimal-gbpusd-activation.json',
    'phase-lmax-r148-ultraminimal-gbpusd-reject-review.json',
    'phase-lmax-r148-identifier-combination-decision.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "Required predecessor evidence missing: $name"
}

$r147 = Read-Json 'phase-lmax-r147-ultraminimal-gbpusd-activation.json'
$r148 = Read-Json 'phase-lmax-r148-ultraminimal-gbpusd-reject-review.json'
$r148Identifier = Read-Json 'phase-lmax-r148-identifier-combination-decision.json'

Assert-True ($summary.phase -eq $phase) 'R149 summary phase mismatch.'
Assert-True ($summary.classification -eq 'LMAX_R149_PASS_IDENTIFIER_COMBINATION_REPAIR_READY_NO_EXTERNAL') 'R149 classification mismatch.'
Assert-True ($summary.noExternal -eq $true) 'R149 must remain no-external.'
Assert-False $summary.externalActivationAttempted 'R149 external activation detected.'
Assert-False $summary.socketTlsFixMarketDataRuntimeActionAttempted 'R149 runtime boundary action detected.'
Assert-False $summary.liveMarketDataRequestSent 'R149 live MarketDataRequest detected.'
Assert-False $summary.liveMarketDataResponseRead 'R149 live MarketDataResponse read detected.'
Assert-True ($r147.attemptCount -eq 1) 'R147 attemptCount must equal 1.'
Assert-True ($r148.classification -eq 'LMAX_R148_PASS_ULTRAMINIMAL_REJECT_REVIEW_IDENTIFIER_REPAIR_RECOMMENDED_NO_EXTERNAL') 'R148 evidence missing.'
Assert-True ($r148Identifier.recommended -eq $true) 'R148 identifier repair recommendation missing.'

Assert-True (@($summary.createdIdentifierProfiles).Contains($symbolOnlyProfile)) 'Symbol-only identifier profile missing.'
Assert-True (@($summary.createdIdentifierProfiles).Contains($symbolSecurityProfile)) 'Symbol+SecurityID identifier profile missing.'
Assert-True ($prior.priorProfilePreserved -eq $true) 'Prior SecurityID-only profile preservation missing.'
Assert-True ($prior.profileName -eq $securityIdOnlyProfile) 'Prior SecurityID-only profile name mismatch.'
Assert-False $prior.selectedForNextDiagnosticRetry 'Prior SecurityID-only profile should not be selected for next retry.'

Assert-True ($symbolOnly.profileExists -eq $true) 'Symbol-only profile contract missing.'
Assert-True ($symbolOnly.profileName -eq $symbolOnlyProfile) 'Symbol-only profile name mismatch.'
Assert-True ($symbolOnly.gbpusdOnly -eq $true) 'Symbol-only profile is not GBPUSD-only.'
Assert-True ((@($symbolOnly.diagnosticRequestSymbols) -join ',') -eq 'GBPUSD') 'Symbol-only profile has wrong symbols.'
Assert-True ($symbolOnly.identifierCombination -eq 'SymbolOnly') 'Symbol-only identifier category mismatch.'
Assert-False $symbolOnly.securityIdIncluded 'Symbol-only profile should not include SecurityID.'
Assert-False $symbolOnly.securityIdSourceIncluded 'Symbol-only profile should not include SecurityIDSource.'
Assert-False $symbolOnly.internalSymbolIncluded 'Symbol-only profile includes InternalSymbol.'
Assert-True ($symbolOnly.snapshotPlusUpdates -eq $true) 'Symbol-only SnapshotPlusUpdates missing.'
Assert-False $symbolOnly.snapshotOnly 'Symbol-only SnapshotOnly returned.'
Assert-True ($symbolOnly.mdUpdateTypeProfileControlled -eq $true) 'Symbol-only MDUpdateType control missing.'
Assert-False $symbolOnly.mdUpdateTypeIncluded 'Symbol-only MDUpdateType included.'
Assert-True ($symbolOnly.marketDepth -eq 1) 'Symbol-only MarketDepth must be one.'
Assert-True ((@($symbolOnly.mdEntryTypes) -join ',') -eq 'Bid,Offer') 'Symbol-only MDEntryTypes mismatch.'
Assert-False $symbolOnly.rawFixSerialized 'Symbol-only raw FIX serialized.'

Assert-True ($symbolSecurity.profileExists -eq $true) 'Symbol+SecurityID profile contract missing.'
Assert-True ($symbolSecurity.profileName -eq $symbolSecurityProfile) 'Symbol+SecurityID profile name mismatch.'
Assert-True ($symbolSecurity.selectedForNextDiagnosticRetryReadinessPath -eq $true) 'Symbol+SecurityID profile not selected for next readiness path.'
Assert-True ($symbolSecurity.gbpusdOnly -eq $true) 'Symbol+SecurityID profile is not GBPUSD-only.'
Assert-True ((@($symbolSecurity.diagnosticRequestSymbols) -join ',') -eq 'GBPUSD') 'Symbol+SecurityID profile has wrong symbols.'
Assert-True ($symbolSecurity.identifierCombination -eq 'SymbolPlusSecurityId') 'Symbol+SecurityID identifier category mismatch.'
Assert-True ($symbolSecurity.securityIdIncluded -eq $true) 'Symbol+SecurityID profile missing SecurityID.'
Assert-True ($symbolSecurity.securityId -eq '4002') 'Symbol+SecurityID profile SecurityID mismatch.'
Assert-True ($symbolSecurity.securityIdSourceIncluded -eq $true) 'Symbol+SecurityID profile missing SecurityIDSource.'
Assert-True ($symbolSecurity.securityIdSource -eq '8') 'Symbol+SecurityID profile SecurityIDSource mismatch.'
Assert-False $symbolSecurity.internalSymbolIncluded 'Symbol+SecurityID profile includes InternalSymbol.'
Assert-True ($symbolSecurity.snapshotPlusUpdates -eq $true) 'Symbol+SecurityID SnapshotPlusUpdates missing.'
Assert-False $symbolSecurity.snapshotOnly 'Symbol+SecurityID SnapshotOnly returned.'
Assert-True ($symbolSecurity.mdUpdateTypeProfileControlled -eq $true) 'Symbol+SecurityID MDUpdateType control missing.'
Assert-False $symbolSecurity.mdUpdateTypeIncluded 'Symbol+SecurityID MDUpdateType included.'
Assert-True ($symbolSecurity.marketDepth -eq 1) 'Symbol+SecurityID MarketDepth must be one.'
Assert-True ((@($symbolSecurity.mdEntryTypes) -join ',') -eq 'Bid,Offer') 'Symbol+SecurityID MDEntryTypes mismatch.'
Assert-False $symbolSecurity.rawFixSerialized 'Symbol+SecurityID raw FIX serialized.'

Assert-True ($matrix.identifierCombinationMatrixPresent -eq $true) 'Identifier-combination matrix missing.'
Assert-True (@($matrix.options).Count -eq 3) 'Identifier-combination matrix incomplete.'
Assert-True (@($matrix.options | Where-Object { $_.profileName -eq $symbolSecurityProfile -and $_.selectedForNextDiagnosticRetry -eq $true }).Count -eq 1) 'Selected identifier profile missing from matrix.'
Assert-True ($gbpusd.gbpusdOnlyEvidencePresent -eq $true) 'GBPUSD-only evidence missing.'
Assert-False $gbpusd.eurgbpPresentInDiagnosticRequest 'EURGBP present in diagnostic request.'
Assert-False $gbpusd.audusdPresentInDiagnosticRequest 'AUDUSD present in diagnostic request.'
Assert-False $gbpusd.usdjpyPresentInDiagnosticRequest 'USDJPY present in diagnostic request.'
Assert-False $gbpusd.nonApprovedInstrumentPresent 'Non-approved instrument present.'

Assert-True ($subscription.snapshotPlusUpdatesPreserved -eq $true) 'SnapshotPlusUpdates weakened.'
Assert-True ($subscription.snapshotOnlyAbsent -eq $true) 'SnapshotOnly returned.'
Assert-True ($depth.marketDepthPreserved -eq $true) 'MarketDepth evidence missing.'
Assert-True ($depth.marketDepth -eq 1) 'MarketDepth must be one.'
Assert-True ($depth.mdEntryTypesPreserved -eq $true) 'MDEntryTypes evidence missing.'
Assert-True ((@($depth.mdEntryTypes) -join ',') -eq 'Bid,Offer') 'MDEntryTypes must be bid/offer only.'
Assert-False $depth.ordersOrTradingEntryTypesPresent 'Order/trading entry type present.'
Assert-True ($update.mdUpdateTypeProfileControlPreserved -eq $true) 'MDUpdateType profile control missing.'
Assert-False $update.mdUpdateTypeIncluded 'MDUpdateType included.'
Assert-False $update.uncontrolledMdUpdateType 'Uncontrolled MDUpdateType detected.'
Assert-True ($order.repeatingGroupFieldOrderEvidencePresent -eq $true) 'Repeating-group field-order evidence missing.'
Assert-True ($order.sanitizedEvidenceOnly -eq $true) 'Repeating-group evidence must be sanitized.'
Assert-True ($order.singleRelatedSymGroup -eq $true) 'Single RelatedSym group missing.'
Assert-False $order.rawFixSerialized 'Raw FIX serialized in group evidence.'

Assert-True ($universe.approvedUniversePreserved -eq $true) 'Approved universe preservation missing.'
Assert-True ((@($universe.approvedInstruments) -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved universe changed.'
Assert-False $universe.nonApprovedInstrumentsAllowed 'Non-approved instruments allowed.'
Assert-True ($usdjpy.usdJpySecurityId -eq '4004') 'USDJPY SecurityID weakened.'
Assert-True ($usdjpy.usdJpySecurityIdSource -eq '8') 'USDJPY SecurityIDSource weakened.'
Assert-True ($usdjpy.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat weakened.'
Assert-True ($selection.nextDiagnosticProfileSelectionDecisionPresent -eq $true) 'Next profile selection decision missing.'
Assert-True ($selection.selectedNextDiagnosticProfile -eq $symbolSecurityProfile) 'Next diagnostic profile selection mismatch.'
Assert-True ($selection.selectedProfileCount -eq 1) 'Exactly one next diagnostic profile must be selected.'
Assert-False $selection.multipleProfilesSelectedForNextRetry 'Multiple profiles selected for next retry.'
Assert-False $selection.multipleRequestsSelectedForNextRetry 'Multiple requests selected for next retry.'

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
Assert-False $forbidden.externalActivationAttempted 'External activation detected in R149.'
Assert-False $forbidden.socketTlsFixMarketDataRuntimeActionAttempted 'Runtime boundary action detected in R149.'
Assert-False $forbidden.liveMarketDataRequestSent 'Live MarketDataRequest detected in R149.'
Assert-False $forbidden.liveMarketDataResponseRead 'Live MarketDataResponse read detected in R149.'
Assert-False $forbidden.ordersIntroduced 'Order path introduced.'
Assert-False $forbidden.tradingEnabled 'Trading enabled.'
Assert-False $forbidden.schedulerIntroduced 'Scheduler introduced.'
Assert-False $forbidden.pollingLoopIntroduced 'Polling loop introduced.'
Assert-False $forbidden.serviceIntroduced 'Service introduced.'
Assert-False $forbidden.replayIntroduced 'Replay introduced.'
Assert-False $forbidden.shadowReplayIntroduced 'Shadow replay introduced.'
Assert-True ($apiWorker.audit -eq 'PASS') 'API/Worker audit did not pass.'
Assert-True ($apiWorker.apiWorkerGatewayMode -eq 'FakeLmaxGatewayOnly') 'API/Worker FakeLmaxGatewayOnly regressed.'
Assert-False $apiWorker.identifierRepairProfileSelectedForApiWorker 'Identifier repair profile selected for API/Worker.'

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R150') 'Next phase recommendation must be R150.'
Assert-True ($next.r150MustRemainNoExternal -eq $true) 'R150 no-external recommendation missing.'
Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.focusedTests -match '^PASS') 'Focused test evidence missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit test evidence missing.'
Assert-True ($gate.integrationTests -match '^PASS') 'Integration test evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R149_VALIDATION_PASS') 'Validator result missing.'

$operationPath = Join-Path $root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualMarketDataRequestOperation.cs'
$factoryPath = Join-Path $root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualExecutionSurfaceFactory.cs'
$operation = Get-Content $operationPath -Raw
$factory = Get-Content $factoryPath -Raw
Assert-True ($operation.Contains($symbolOnlyProfile)) 'Symbol-only profile code missing.'
Assert-True ($operation.Contains($symbolSecurityProfile)) 'Symbol+SecurityID profile code missing.'
Assert-True ($factory.Contains($symbolSecurityProfile)) 'Future manual real-bounded path does not select selected identifier repair profile.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r149-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R149 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R149_VALIDATION_PASS'
