$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'
$phase = 'LMAX-R139'

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

$summary = Read-Json 'phase-lmax-r139-marketdatarequest-shape-repair.json'
$contract = Read-Json 'phase-lmax-r139-repaired-profile-contract.json'
$legacy = Read-Json 'phase-lmax-r139-legacy-rejected-profile-evidence.json'
$nonbatched = Read-Json 'phase-lmax-r139-nonbatched-request-plan.json'
$securityIdOnly = Read-Json 'phase-lmax-r139-securityidonly-shape-evidence.json'
$snapshotUpdates = Read-Json 'phase-lmax-r139-snapshot-updates-shape-evidence.json'
$approved = Read-Json 'phase-lmax-r139-approved-instrument-evidence.json'
$usdJpy = Read-Json 'phase-lmax-r139-usdjpy-caveat-preservation.json'
$sanitization = Read-Json 'phase-lmax-r139-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r139-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r139-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r139-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r139-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r137-weekday-market-hours-activation.json',
    'phase-lmax-r137-sessionreject-sanitized-reason-reporting.json',
    'phase-lmax-r138-sanitized-runtime-reject-review.json',
    'phase-lmax-r138-repair-decision-gate.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "Required predecessor evidence missing: $name"
}

$r137 = Read-Json 'phase-lmax-r137-weekday-market-hours-activation.json'
$r137Reason = Read-Json 'phase-lmax-r137-sessionreject-sanitized-reason-reporting.json'
$r138 = Read-Json 'phase-lmax-r138-sanitized-runtime-reject-review.json'

Assert-True ($summary.phase -eq $phase) 'R139 summary phase mismatch.'
Assert-True ($summary.classification -eq 'LMAX_R139_PASS_MARKETDATAREQUEST_SHAPE_REPAIR_READY_NO_EXTERNAL') 'R139 classification mismatch.'
Assert-True ($summary.noExternal -eq $true) 'R139 must remain no-external.'
Assert-False $summary.externalActivationAttempted 'R139 external activation detected.'
Assert-False $summary.socketTlsFixMarketDataRuntimeActionAttempted 'R139 runtime boundary action detected.'
Assert-False $summary.liveMarketDataRequestSent 'R139 live MarketDataRequest was sent.'
Assert-False $summary.liveMarketDataResponseRead 'R139 live MarketDataResponse was read.'
Assert-True ($r137.attemptCount -eq 1) 'R137 external attemptCount must equal 1.'
Assert-True ($r137Reason.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'R137 sanitized reason evidence missing or unexpected.'
Assert-True ($r138.classification -eq 'LMAX_R138_PASS_RUNTIME_REJECT_REVIEW_MARKETDATAREQUEST_SHAPE_REPAIR_RECOMMENDED_NO_EXTERNAL') 'R138 repair recommendation evidence missing.'

Assert-True ($summary.repairedProfileReady -eq $true) 'Repaired MarketDataRequest profile is missing.'
Assert-True ($contract.profileName -eq 'RepairedSnapshotPlusUpdatesSecurityIdOnlyNonBatched') 'Repaired profile name mismatch.'
Assert-True ($contract.profileSelectedForFutureManualRealBoundedPath -eq $true) 'Repaired profile is not selected for the future manual real-bounded path.'
Assert-True ($contract.subscriptionProfile -eq 'SnapshotPlusUpdates') 'Repaired profile still uses rejected snapshot-only shape.'
Assert-True ($contract.snapshotOnlyProfileAvoided -eq $true) 'Snapshot-only avoidance missing.'
Assert-True ($snapshotUpdates.snapshotPlusUpdatesSelected -eq $true) 'Snapshot-plus-updates evidence missing.'
Assert-True ($snapshotUpdates.snapshotOnlyAvoided -eq $true) 'Snapshot-only profile still selected.'
Assert-True ($contract.updateTypeCategoryPresent -eq $true) 'Update-type category evidence missing.'
Assert-True ($contract.instrumentIdentification -eq 'SecurityIdOnly') 'Repaired profile does not use SecurityID-only identification.'
Assert-False $contract.symbolTextIncluded 'Repaired profile still requires Symbol text.'
Assert-True ($securityIdOnly.securityIdOnlyShapeReady -eq $true) 'SecurityID-only shape evidence missing.'
Assert-False $securityIdOnly.symbolTextPresent 'Symbol text remains present in repaired shape evidence.'
Assert-True ($contract.nonBatchedSingleInstrumentRequests -eq $true) 'Repaired profile still batches all approved instruments.'
Assert-True ($nonbatched.nonBatchedModeReady -eq $true) 'Non-batched request plan missing.'
Assert-True ($nonbatched.futureRequestPlan -eq 'one request per approved instrument') 'Future request plan must be one per approved instrument.'

Assert-True ($legacy.legacyProfileRepresented -eq $true) 'Legacy rejected profile evidence missing.'
Assert-True ($legacy.selectedForFutureManualRealBoundedPath -eq $false) 'Legacy rejected profile must not be selected for the future path.'
Assert-True ($legacy.legacyShapeCategories.subscriptionProfile -eq 'SnapshotOnly') 'Legacy rejected snapshot-only evidence missing.'
Assert-True ($legacy.legacyShapeCategories.symbolTextAlsoPresent -eq $true) 'Legacy symbol-text evidence missing.'
Assert-True ($legacy.legacyShapeCategories.batchedAllApprovedInstruments -eq $true) 'Legacy batch evidence missing.'

$approvedSymbols = @($approved.approvedInstruments)
Assert-True (($approvedSymbols -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved instrument scope was changed.'
Assert-True ($approved.approvedInstrumentScopeExact -eq $true) 'Approved instrument scope exact evidence missing.'
Assert-True ($approved.nonApprovedInstrumentsRejected -eq $true) 'Non-approved instrument rejection missing.'
Assert-True ($nonbatched.nonApprovedInstrumentsAllowed -eq $false) 'Non-approved instruments are allowed.'
Assert-True ($usdJpy.usdJpySecurityId -eq '4004') 'USDJPY SecurityID was weakened.'
Assert-True ($usdJpy.usdJpySecurityIdSource -eq '8') 'USDJPY SecurityIDSource was weakened.'
Assert-True ($usdJpy.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat was weakened.'

Assert-True ($summary.credentialValuesReturned -eq $false) 'credentialValuesReturned must remain false.'
Assert-True ($sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned audit must remain false.'
Assert-False $sanitization.rawFixSerialized 'Raw FIX messages were serialized.'
Assert-False $sanitization.rawRejectTextSerialized 'Raw reject text was serialized.'
Assert-False $sanitization.rawCredentialsSerialized 'Raw credentials were serialized.'
Assert-False $sanitization.rawUsernameSerialized 'Raw username was serialized.'
Assert-False $sanitization.rawPasswordSerialized 'Raw password was serialized.'
Assert-False $sanitization.rawSessionIdentifiersSerialized 'Raw session identifiers were serialized.'
Assert-False $sanitization.rawCompIdsSerialized 'Raw CompID values were serialized.'
Assert-False $sanitization.rawEndpointSerialized 'Raw endpoint values were serialized.'
Assert-False $sanitization.rawTlsMaterialSerialized 'Raw TLS material was serialized.'

Assert-True ($forbidden.audit -eq 'PASS') 'Forbidden actions audit did not pass.'
Assert-False $forbidden.externalActivationAttempted 'External activation detected in R139.'
Assert-False $forbidden.socketTlsFixMarketDataRuntimeActionAttempted 'Runtime boundary action detected in R139.'
Assert-False $forbidden.socketOpened 'Socket opened in R139.'
Assert-False $forbidden.tlsAttempted 'TLS attempted in R139.'
Assert-False $forbidden.fixLogonAttempted 'FIX logon attempted in R139.'
Assert-False $forbidden.liveMarketDataRequestSent 'Live MarketDataRequest sent in R139.'
Assert-False $forbidden.liveMarketDataResponseRead 'Live MarketDataResponse read in R139.'
Assert-False $forbidden.ordersIntroduced 'Order path introduced.'
Assert-False $forbidden.tradingEnabled 'Trading enabled.'
Assert-False $forbidden.schedulerIntroduced 'Scheduler introduced.'
Assert-False $forbidden.pollingLoopIntroduced 'Polling loop introduced.'
Assert-False $forbidden.serviceIntroduced 'Service introduced.'
Assert-False $forbidden.replayIntroduced 'Replay introduced.'
Assert-False $forbidden.shadowReplayIntroduced 'Shadow replay introduced.'

Assert-True ($apiWorker.audit -eq 'PASS') 'API/Worker audit did not pass.'
Assert-True ($apiWorker.apiWorkerGatewayMode -eq 'FakeLmaxGatewayOnly') 'API/Worker FakeLmaxGatewayOnly regressed.'
Assert-False $apiWorker.apiStartupCanReachManualRealBoundedPath 'API startup can reach manual real-bounded path.'
Assert-False $apiWorker.workerStartupCanReachManualRealBoundedPath 'Worker startup can reach manual real-bounded path.'
Assert-True ($apiWorker.noExternalDefaultPreserved -eq $true) 'No-external default was not preserved.'

$operationSource = Get-Content -LiteralPath (Join-Path $root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualMarketDataRequestOperation.cs') -Raw
$factorySource = Get-Content -LiteralPath (Join-Path $root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualExecutionSurfaceFactory.cs') -Raw
Assert-True ($operationSource -match 'RepairedSnapshotPlusUpdatesSecurityIdOnlyNonBatched') 'Repaired profile source is missing.'
Assert-True ($operationSource -match 'LegacySnapshotOnlySymbolAndSecurityBatch') 'Legacy rejected profile source is missing.'
Assert-True ($operationSource -match 'IncludeSymbolText:\s*false') 'Repaired profile still appears to include Symbol text.'
Assert-True ($operationSource -match 'NonBatchedSingleInstrumentRequests:\s*true') 'Repaired profile non-batched setting missing.'
Assert-True ($factorySource -match 'RepairedSnapshotPlusUpdatesSecurityIdOnlyNonBatched') 'Manual real-bounded factory does not select the repaired profile.'

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R140') 'Next phase recommendation must be R140.'
Assert-True ($next.r140MustRemainNoExternal -eq $true) 'R140 no-external recommendation missing.'
Assert-True ($gate.buildResult -match '^PASS') 'Build evidence is missing.'
Assert-True ($gate.focusedTests -match '^PASS') 'Focused test evidence is missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit test evidence is missing.'
Assert-True ($gate.integrationTests -match '^PASS') 'Integration test evidence is missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R139_VALIDATION_PASS') 'Validator result evidence is missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r139-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R139 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R139_VALIDATION_PASS'
