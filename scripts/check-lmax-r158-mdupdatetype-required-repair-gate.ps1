$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'
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

foreach ($name in @(
    'phase-lmax-r158-mdupdatetype-required-repair-summary.md',
    'phase-lmax-r158-mdupdatetype-required-repair.json',
    'phase-lmax-r158-official-docs-marketdatarequest-requirements-matrix.json',
    'phase-lmax-r158-new-profile-contract.json',
    'phase-lmax-r158-mdupdate-required-evidence.json',
    'phase-lmax-r158-securityidonly-docs-backed-evidence.json',
    'phase-lmax-r158-bid-offer-entrytypes-evidence.json',
    'phase-lmax-r158-prior-profiles-preservation.json',
    'phase-lmax-r158-approved-universe-preservation.json',
    'phase-lmax-r158-usdjpy-caveat-preservation.json',
    'phase-lmax-r158-sanitization-audit.json',
    'phase-lmax-r158-forbidden-actions-audit.json',
    'phase-lmax-r158-api-worker-fake-gateway-audit.json',
    'phase-lmax-r158-next-phase-recommendation.json',
    'phase-lmax-r158-gate-validation.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "Required artifact missing: $name"
}

$summary = Read-Json 'phase-lmax-r158-mdupdatetype-required-repair.json'
$matrix = Read-Json 'phase-lmax-r158-official-docs-marketdatarequest-requirements-matrix.json'
$contract = Read-Json 'phase-lmax-r158-new-profile-contract.json'
$mdUpdate = Read-Json 'phase-lmax-r158-mdupdate-required-evidence.json'
$securityIdEvidence = Read-Json 'phase-lmax-r158-securityidonly-docs-backed-evidence.json'
$entryTypes = Read-Json 'phase-lmax-r158-bid-offer-entrytypes-evidence.json'
$prior = Read-Json 'phase-lmax-r158-prior-profiles-preservation.json'
$universe = Read-Json 'phase-lmax-r158-approved-universe-preservation.json'
$usdJpy = Read-Json 'phase-lmax-r158-usdjpy-caveat-preservation.json'
$sanitization = Read-Json 'phase-lmax-r158-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r158-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r158-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r158-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r158-gate-validation.json'

Assert-True ($summary.classification -eq 'LMAX_R158_PASS_MDUPDATETYPE_REQUIRED_REPAIR_READY_NO_EXTERNAL') 'R158 classification mismatch.'
Assert-True ($summary.noExternal -eq $true) 'R158 must be no-external.'
Assert-True ($summary.newProfileCreated -eq $true) 'New MDUpdateType-required profile missing.'
Assert-True ($summary.newProfileName -eq $profileName) 'New profile name mismatch.'
Assert-True ($summary.selectedFutureProfile -eq $profileName) 'Selected future profile mismatch.'
Assert-True ($summary.selectedFutureProfileCount -eq 1) 'Selected future profile count must be exactly one.'
Assert-True ($summary.docsBackedRepair -eq $true) 'Docs-backed repair evidence missing.'
Assert-False $summary.liveRetryExecuted 'Live retry must not be executed in R158.'
Assert-True ($summary.credentialValuesReturned -eq $false) 'credentialValuesReturned=false missing.'

Assert-True ($matrix.officialDocsRequirementsMatrixPresent -eq $true) 'Official docs matrix missing.'
Assert-True ($matrix.message.requiredValue -eq 'V') 'MsgType V requirement missing.'
$subscription = @($matrix.fields | Where-Object { $_.tag -eq '263' })[0]
$mdUpdateField = @($matrix.fields | Where-Object { $_.tag -eq '265' })[0]
$marketDepth = @($matrix.fields | Where-Object { $_.tag -eq '264' })[0]
$entryTypeCount = @($matrix.fields | Where-Object { $_.tag -eq '267' })[0]
$entryType = @($matrix.fields | Where-Object { $_.tag -eq '269' })[0]
$relatedSym = @($matrix.fields | Where-Object { $_.tag -eq '146' })[0]
$securityField = @($matrix.fields | Where-Object { $_.tag -eq '48' })[0]
$source = @($matrix.fields | Where-Object { $_.tag -eq '22' })[0]
Assert-True ($subscription.r158ProfileValue -eq '1') 'SubscriptionRequestType=1 missing from docs matrix.'
Assert-True ($mdUpdateField.r158ProfileValue -eq '0') 'MDUpdateType=0 missing from docs matrix.'
Assert-True ($marketDepth.r158ProfileValue -eq '1') 'MarketDepth=1 missing from docs matrix.'
Assert-True ($entryTypeCount.r158ProfileValue -eq '2') 'NoMDEntryTypes=2 missing from docs matrix.'
Assert-True ((@($entryType.r158ProfileValues) -join ',') -eq '0,1') 'Bid+Offer MDEntryTypes missing from docs matrix.'
Assert-True ($entryType.bidAndOfferTogetherRequired -eq $true) 'Bid+offer pair requirement missing.'
Assert-True ($relatedSym.r158ProfileValue -eq '1') 'NoRelatedSym=1 missing from docs matrix.'
Assert-True ($securityField.r158ProfileValue -eq '4002') 'SecurityID=4002 missing from docs matrix.'
Assert-True ($source.r158ProfileValue -eq '8') 'SecurityIDSource=8 missing from docs matrix.'
Assert-False $matrix.rawDocsExcerptSerialized 'Raw docs excerpt should not be serialized.'

Assert-True ($contract.profileContractPresent -eq $true) 'New profile contract missing.'
Assert-True ($contract.profileName -eq $profileName) 'Profile contract name mismatch.'
Assert-True ($contract.selectedFutureProfileCount -eq 1) 'Profile contract selected count mismatch.'
Assert-True ($contract.gbpusdOnly -eq $true) 'Profile must be GBPUSD-only.'
Assert-True ((@($contract.diagnosticRequestSymbols) -join ',') -eq 'GBPUSD') 'Diagnostic request is not GBPUSD-only.'
Assert-True ($contract.exactlyOneRequest -eq $true) 'Profile must use exactly one request.'
Assert-True ($contract.subscriptionRequestType -eq '1') 'SubscriptionRequestType=1 missing.'
Assert-False $contract.snapshotOnly 'SnapshotOnly must be absent.'
Assert-True ($contract.mdUpdateTypeRequired -eq $true) 'MDUpdateType required flag missing.'
Assert-True ($contract.mdUpdateType -eq '0') 'MDUpdateType=0 missing.'
Assert-True ($contract.marketDepth -eq 1) 'MarketDepth=1 missing.'
Assert-True ($contract.noMdEntryTypes -eq 2) 'NoMDEntryTypes=2 missing.'
Assert-True ((@($contract.mdEntryTypes) -join ',') -eq 'Bid,Offer') 'Bid+offer pair missing.'
Assert-True ($contract.bidAndOfferRepresentedTogether -eq $true) 'Bid+offer not represented together.'
Assert-True ($contract.noRelatedSym -eq 1) 'NoRelatedSym=1 missing.'
Assert-True ($contract.securityId -eq '4002') 'SecurityID=4002 missing.'
Assert-True ($contract.securityIdSource -eq '8') 'SecurityIDSource=8 missing.'
Assert-False $contract.symbolTextPresent 'Symbol text present.'
Assert-False $contract.internalSymbolPresent 'InternalSymbol present.'
Assert-False $contract.rawFixSerialized 'Raw FIX serialized in profile contract.'

Assert-True ($mdUpdate.mdUpdateTypeRequiredEvidencePresent -eq $true) 'MDUpdateType required evidence missing.'
Assert-True ($mdUpdate.subscriptionRequestType -eq '1') 'MDUpdate evidence missing SubscriptionRequestType=1.'
Assert-True ($mdUpdate.mdUpdateTypeRequiredWhenSubscriptionRequestTypeIsOne -eq $true) 'MDUpdateType required condition missing.'
Assert-True ($mdUpdate.requiredMdUpdateTypeValue -eq '0') 'Required MDUpdateType=0 missing.'
Assert-True ($mdUpdate.r155OmittedMdUpdateType -eq $true) 'R155 omission evidence missing.'
Assert-True ($mdUpdate.r158IncludesMdUpdateTypeZero -eq $true) 'R158 MDUpdateType inclusion evidence missing.'

Assert-True ($securityIdEvidence.securityIdOnly -eq $true) 'SecurityID-only contract weakened.'
Assert-True ($securityIdEvidence.securityId -eq '4002') 'SecurityID-only evidence missing SecurityID=4002.'
Assert-True ($securityIdEvidence.securityIdSource -eq '8') 'SecurityID-only evidence missing SecurityIDSource=8.'
Assert-False $securityIdEvidence.symbolTextPresent 'SecurityID-only evidence contains Symbol.'
Assert-False $securityIdEvidence.internalSymbolPresent 'SecurityID-only evidence contains InternalSymbol.'

Assert-True ($entryTypes.bidOfferEntryTypesEvidencePresent -eq $true) 'Bid/offer evidence missing.'
Assert-True ($entryTypes.noMdEntryTypes -eq 2) 'NoMDEntryTypes must be 2.'
Assert-True ($entryTypes.bidAndOfferRepresentedTogether -eq $true) 'Bid+offer pair missing.'
Assert-False $entryTypes.bidOnlyProfileSelected 'Bid-only profile selected.'
Assert-False $entryTypes.offerOnlyProfileSelected 'Offer-only profile selected.'

Assert-True ($prior.priorProfilesPreservedAsEvidenceOnly -eq $true) 'Prior profile preservation missing.'
Assert-False $prior.priorProfilesSelectedForFutureRetry 'Prior profiles should not be selected for future retry.'
Assert-True ($universe.approvedUniversePreserved -eq $true) 'Approved universe preservation missing.'
Assert-True ((@($universe.approvedInstruments) -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved universe changed.'
Assert-False $universe.nonApprovedInstrumentsAllowed 'Non-approved instruments allowed.'
Assert-True ($usdJpy.usdJpySecurityId -eq '4004') 'USDJPY SecurityID weakened.'
Assert-True ($usdJpy.usdJpySecurityIdSource -eq '8') 'USDJPY SecurityIDSource weakened.'
Assert-True ($usdJpy.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat weakened.'

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

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R159') 'Next phase recommendation must be R159.'
Assert-True ($next.r159MustBeNoExternal -eq $true) 'R159 no-external requirement missing.'
Assert-True ($next.r159MustPrepareButNotExecuteRetry -eq $true) 'R159 prepare-only requirement missing.'
Assert-True ($next.selectedFutureProfile -eq $profileName) 'Next recommendation selected profile mismatch.'
Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.focusedTests -match '^PASS') 'Focused test evidence missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit test evidence missing.'
Assert-True ($gate.integrationTests -match '^PASS') 'Integration test evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R158_VALIDATION_PASS') 'Validator evidence missing.'

$operation = Get-Content -LiteralPath (Join-Path $root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualMarketDataRequestOperation.cs') -Raw
$factory = Get-Content -LiteralPath (Join-Path $root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualExecutionSurfaceFactory.cs') -Raw
Assert-True ($operation.Contains($profileName)) 'New profile missing from MarketDataRequest operation code.'
Assert-True ($operation.Contains('IncludeMdUpdateType: true')) 'MDUpdateType inclusion missing from operation code.'
Assert-True ($operation.Contains('IncludeSymbolText: false')) 'SecurityID-only no-Symbol contract missing from operation code.'
Assert-True ($operation.Contains('LMAX_READONLY_R158_')) 'R158 MDReqID category missing from operation code.'
Assert-True ($factory.Contains($profileName)) 'Future manual real-bounded path does not select R158 profile.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r158-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R158 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R158_VALIDATION_PASS'
