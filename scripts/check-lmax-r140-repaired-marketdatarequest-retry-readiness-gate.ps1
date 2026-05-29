$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'
$phase = 'LMAX-R140'

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

$summary = Read-Json 'phase-lmax-r140-repaired-marketdatarequest-retry-readiness.json'
$selection = Read-Json 'phase-lmax-r140-repaired-profile-selection-evidence.json'
$legacy = Read-Json 'phase-lmax-r140-legacy-profile-not-selected-evidence.json'
$preflight = Read-Json 'phase-lmax-r140-r141-preflight-checklist.json'
$contract = Read-Json 'phase-lmax-r140-r141-expected-evidence-contract.json'
$reporting = Read-Json 'phase-lmax-r140-reporting-contract-readiness.json'
$approved = Read-Json 'phase-lmax-r140-approved-instrument-evidence.json'
$usdJpy = Read-Json 'phase-lmax-r140-usdjpy-caveat-preservation.json'
$sanitization = Read-Json 'phase-lmax-r140-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r140-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r140-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r140-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r140-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r137-weekday-market-hours-activation.json',
    'phase-lmax-r137-sessionreject-sanitized-reason-reporting.json',
    'phase-lmax-r138-sanitized-runtime-reject-review.json',
    'phase-lmax-r139-marketdatarequest-shape-repair.json',
    'phase-lmax-r139-repaired-profile-contract.json',
    'phase-lmax-r139-legacy-rejected-profile-evidence.json',
    'phase-lmax-r133-sanitized-reason-category-contract.json',
    'phase-lmax-r135-sanitized-fixture-reclassification.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "Required predecessor evidence missing: $name"
}

$r137 = Read-Json 'phase-lmax-r137-weekday-market-hours-activation.json'
$r137Reason = Read-Json 'phase-lmax-r137-sessionreject-sanitized-reason-reporting.json'
$r138 = Read-Json 'phase-lmax-r138-sanitized-runtime-reject-review.json'
$r139 = Read-Json 'phase-lmax-r139-marketdatarequest-shape-repair.json'
$r139Contract = Read-Json 'phase-lmax-r139-repaired-profile-contract.json'

Assert-True ($summary.phase -eq $phase) 'R140 summary phase mismatch.'
Assert-True ($summary.classification -eq 'LMAX_R140_PASS_REPAIRED_MARKETDATAREQUEST_RETRY_READINESS_NO_EXTERNAL') 'R140 classification mismatch.'
Assert-True ($summary.noExternal -eq $true) 'R140 must remain no-external.'
Assert-False $summary.externalActivationAttempted 'R140 external activation detected.'
Assert-False $summary.socketTlsFixMarketDataRuntimeActionAttempted 'R140 runtime boundary action detected.'
Assert-False $summary.liveMarketDataRequestSent 'R140 live MarketDataRequest was sent.'
Assert-False $summary.liveMarketDataResponseRead 'R140 live MarketDataResponse was read.'
Assert-True ($r137.attemptCount -eq 1) 'R137 external attemptCount must equal 1.'
Assert-True ($r137Reason.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'R137 sanitized reason evidence missing or unexpected.'
Assert-True ($r138.classification -eq 'LMAX_R138_PASS_RUNTIME_REJECT_REVIEW_MARKETDATAREQUEST_SHAPE_REPAIR_RECOMMENDED_NO_EXTERNAL') 'R138 repair recommendation evidence missing.'
Assert-True ($r139.classification -eq 'LMAX_R139_PASS_MARKETDATAREQUEST_SHAPE_REPAIR_READY_NO_EXTERNAL') 'R139 repair evidence missing.'
Assert-True ($r139Contract.profileName -eq 'RepairedSnapshotPlusUpdatesSecurityIdOnlyNonBatched') 'R139 repaired profile contract missing.'

Assert-True ($selection.repairedProfileSelectionEvidencePresent -eq $true) 'Repaired profile selection evidence missing.'
Assert-True ($selection.futureManualRealBoundedPathSelects -eq 'RepairedSnapshotPlusUpdatesSecurityIdOnlyNonBatched') 'Future retry does not select repaired profile.'
Assert-True ($selection.profileSelectedInManualExecutionSurfaceFactory -eq $true) 'Manual execution surface factory selection evidence missing.'
Assert-True ($selection.snapshotPlusUpdatesSemantics -eq $true) 'Future retry remains SnapshotOnly or lacks snapshot-plus-updates evidence.'
Assert-True ($selection.updateTypeCategoryPresent -eq $true) 'Update-type category evidence missing.'
Assert-True ($selection.securityIdOnly -eq $true) 'SecurityID-only evidence missing.'
Assert-True ($selection.symbolTextExcluded -eq $true) 'Future retry includes Symbol text.'
Assert-True ($selection.nonBatchedOneRequestPerApprovedInstrument -eq $true) 'Future retry still batches all instruments.'

Assert-True ($legacy.legacyProfilePreservedAsEvidenceOnly -eq $true) 'Legacy rejected profile evidence missing.'
Assert-False $legacy.legacyProfileSelectableForNextRetry 'Legacy rejected profile is still selectable for next retry.'
Assert-False $legacy.legacyProfileSelectedInManualExecutionSurfaceFactory 'Legacy rejected profile is selected in factory.'
Assert-False $legacy.nextRetryUsesLegacyRejectedProfile 'Next retry uses legacy rejected profile.'

Assert-True ($preflight.freshExactOperatorApprovalRequired -eq $true) 'R141 exact approval requirement missing.'
Assert-True ($preflight.weekdayActiveFxMarketDataAvailabilityRequired -eq $true) 'R141 market-hours requirement missing.'
Assert-True ($preflight.exactlyOneBoundedAttemptRequired -eq $true) 'R141 single-attempt requirement missing.'
Assert-True ($preflight.noRetryLoop -eq $true) 'R141 no-retry-loop requirement missing.'
Assert-True ($preflight.noPollingLoop -eq $true) 'R141 no-polling-loop requirement missing.'
Assert-True ($preflight.adapterModeRequired -eq 'real-bounded-executable-readonly') 'R141 adapter mode requirement missing.'
Assert-True ($preflight.repairedProfileRequired -eq 'RepairedSnapshotPlusUpdatesSecurityIdOnlyNonBatched') 'R141 repaired profile requirement missing.'
Assert-True ($preflight.stopAfterAttempt -eq $true) 'R141 stop-after-attempt requirement missing.'

$approvalTemplate = Get-Content -LiteralPath (Join-Path $artifactRoot 'phase-lmax-r140-r141-operator-approval-template.md') -Raw
Assert-True ($approvalTemplate.Contains('I, Philippe, explicitly approve Phase LMAX-R141 for one temporary QQ Workspace Demo weekday market-hours read-only runtime market-data activation retry with the repaired MarketDataRequest profile RepairedSnapshotPlusUpdatesSecurityIdOnlyNonBatched for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, exactly one bounded attempt, and immediate abort authority.')) 'R141 exact approval template is missing or mismatched.'

Assert-True ($contract.marketDataRequestProfileExpected -eq 'RepairedSnapshotPlusUpdatesSecurityIdOnlyNonBatched') 'R141 expected evidence contract lacks repaired profile.'
Assert-True ($contract.snapshotPlusUpdatesEvidenceRequired -eq $true) 'R141 expected evidence contract lacks snapshot-plus-updates.'
Assert-True ($contract.securityIdOnlyEvidenceRequired -eq $true) 'R141 expected evidence contract lacks SecurityID-only.'
Assert-True ($contract.nonBatchedEvidenceRequired -eq $true) 'R141 expected evidence contract lacks non-batched mode.'
Assert-True ($contract.sessionRejectSanitizedReasonCategoryCliFieldRequired -eq $true) 'CLI sanitized reason reporting evidence missing.'
Assert-True ($contract.sanitizedSessionRejectReasonCategoryArtifactFieldRequired -eq $true) 'Artifact sanitized reason reporting evidence missing.'

Assert-True ($reporting.reportingContractReady -eq $true) 'Reporting contract readiness missing.'
Assert-True ($reporting.r133EvidencePreserved -eq $true) 'R133 reporting evidence missing.'
Assert-True ($reporting.r135EvidencePreserved -eq $true) 'R135 fixture/reporting evidence missing.'
Assert-True ($reporting.cliReportingField -eq 'sessionRejectSanitizedReasonCategory') 'CLI reporting field missing.'
Assert-True ($reporting.artifactReportingField -eq 'sanitizedSessionRejectReasonCategory') 'Artifact reporting field missing.'

$approvedSymbols = @($approved.approvedInstruments)
Assert-True (($approvedSymbols -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved instrument scope was changed.'
Assert-True ($approved.approvedInstrumentScopeExact -eq $true) 'Approved instrument scope exact evidence missing.'
Assert-False $approved.nonApprovedInstrumentsAllowed 'Non-approved instruments are allowed.'
Assert-True ($approved.nonApprovedInstrumentsRejected -eq $true) 'Non-approved instrument rejection missing.'
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
Assert-False $forbidden.externalActivationAttempted 'External activation detected in R140.'
Assert-False $forbidden.socketTlsFixMarketDataRuntimeActionAttempted 'Runtime boundary action detected in R140.'
Assert-False $forbidden.socketOpened 'Socket opened in R140.'
Assert-False $forbidden.tlsAttempted 'TLS attempted in R140.'
Assert-False $forbidden.fixLogonAttempted 'FIX logon attempted in R140.'
Assert-False $forbidden.liveMarketDataRequestSent 'Live MarketDataRequest sent in R140.'
Assert-False $forbidden.liveMarketDataResponseRead 'Live MarketDataResponse read in R140.'
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

$factorySource = Get-Content -LiteralPath (Join-Path $root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualExecutionSurfaceFactory.cs') -Raw
Assert-True ($factorySource -match 'RepairedSnapshotPlusUpdatesSecurityIdOnlyNonBatched') 'Manual real-bounded factory does not select repaired profile.'
Assert-False ($factorySource -match '"SnapshotOrStatus"') 'Manual real-bounded factory still selects legacy SnapshotOrStatus profile.'

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R141') 'Next phase recommendation must be R141.'
Assert-True ($next.r141RequiresFreshExactOperatorApproval -eq $true) 'R141 exact approval recommendation missing.'
Assert-True ($next.r141RequiresWeekdayActiveFxMarketDataAvailability -eq $true) 'R141 market-hours recommendation missing.'
Assert-True ($next.r141ExactlyOneBoundedAttemptOnly -eq $true) 'R141 single-attempt recommendation missing.'
Assert-True ($gate.buildResult -match '^PASS') 'Build evidence is missing.'
Assert-True ($gate.focusedTests -match '^PASS') 'Focused test evidence is missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit test evidence is missing.'
Assert-True ($gate.integrationTests -match '^PASS') 'Integration test evidence is missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R140_VALIDATION_PASS') 'Validator result evidence is missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r140-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R140 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R140_VALIDATION_PASS'
