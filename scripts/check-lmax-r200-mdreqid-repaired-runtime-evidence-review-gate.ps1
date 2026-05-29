param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Read-Json {
    param([string]$Name)
    $path = Join-Path $script:ArtifactRoot $Name
    Assert-True (Test-Path -LiteralPath $path) "Missing artifact: $Name"
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

function Assert-ContainsAll {
    param([object[]]$Values, [string[]]$Expected, [string]$Message)
    foreach ($item in $Expected) {
        Assert-True ($Values -contains $item) "$Message Missing: $item"
    }
}

$script:ArtifactRoot = Join-Path $Root 'artifacts/readiness/lmax-runtime-enablement'

Assert-True (Test-Path -LiteralPath (Join-Path $ArtifactRoot 'phase-lmax-r200-mdreqid-repaired-runtime-evidence-review-summary.md')) 'R200 summary missing.'

$review = Read-Json 'phase-lmax-r200-mdreqid-repaired-runtime-evidence-review.json'
$success = Read-Json 'phase-lmax-r200-r199-success-confirmation.json'
$mdReq = Read-Json 'phase-lmax-r200-mdreqid-repair-effectiveness.json'
$state = Read-Json 'phase-lmax-r200-state-evidence-confirmation.json'
$reject = Read-Json 'phase-lmax-r200-reject-absence-confirmation.json'
$entries = Read-Json 'phase-lmax-r200-entries-evidence-review.json'
$reporting = Read-Json 'phase-lmax-r200-cli-artifact-reporting-review.json'
$enablement = Read-Json 'phase-lmax-r200-controlled-readonly-enablement-decision.json'
$nextAction = Read-Json 'phase-lmax-r200-next-action-decision-gate.json'
$sanitization = Read-Json 'phase-lmax-r200-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r200-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r200-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r200-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r200-gate-validation.json'

$r199 = Read-Json 'phase-lmax-r199-mdreqid-repaired-activation.json'
$r199Selected = Read-Json 'phase-lmax-r199-selected-profile-evidence.json'
$r199State = Read-Json 'phase-lmax-r199-state-evidence.json'
$r199Reject = Read-Json 'phase-lmax-r199-sessionreject-tag-detail-evidence.json'
$r199Gate = Read-Json 'phase-lmax-r199-gate-validation.json'

Assert-True ($review.classification -in @(
    'LMAX_R200_PASS_MDREQID_REPAIRED_RUNTIME_EVIDENCE_REVIEW_CONTROLLED_READONLY_GATE_READY_NO_EXTERNAL',
    'LMAX_R200_PASS_MDREQID_REPAIRED_RUNTIME_SUCCESS_ENTRIES_REPORTING_GAP_RECOMMENDED_NO_EXTERNAL',
    'LMAX_R200_PASS_MDREQID_REPAIRED_RUNTIME_SUCCESS_ARCHIVE_RECOMMENDED_NO_EXTERNAL',
    'LMAX_R200_PASS_MDREQID_REPAIRED_RUNTIME_EVIDENCE_INCONCLUSIVE_SAFE_NO_EXTERNAL',
    'LMAX_R200_FAIL_NEW_EXTERNAL_ACTION_DETECTED',
    'LMAX_R200_FAIL_R199_EVIDENCE_MISSING',
    'LMAX_R200_FAIL_R199_ATTEMPT_COUNT_INVALID',
    'LMAX_R200_FAIL_SUCCESS_CONFIRMATION_MISSING',
    'LMAX_R200_FAIL_MDREQID_REPAIR_EFFECTIVENESS_REVIEW_MISSING',
    'LMAX_R200_FAIL_STATE_EVIDENCE_REVIEW_MISSING',
    'LMAX_R200_FAIL_ENTRIES_EVIDENCE_REVIEW_MISSING',
    'LMAX_R200_FAIL_UNSUPPORTED_ENTRIES_CLAIM',
    'LMAX_R200_FAIL_RAW_FIX_OR_REJECT_OR_MDREQID_LEAK_RISK',
    'LMAX_R200_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK',
    'LMAX_R200_FAIL_USDJPY_CAVEAT_WEAKENED',
    'LMAX_R200_FAIL_FORBIDDEN_ACTION_RISK',
    'LMAX_R200_FAIL_API_WORKER_GATEWAY_REGRESSION',
    'LMAX_R200_FAIL_BUILD_OR_VALIDATOR')) 'R200 classification is not allowed.'
Assert-True ($review.classification -eq 'LMAX_R200_PASS_MDREQID_REPAIRED_RUNTIME_SUCCESS_ENTRIES_REPORTING_GAP_RECOMMENDED_NO_EXTERNAL') 'R200 classification mismatch.'
Assert-True ($review.noExternal -eq $true) 'R200 must be no-external.'

Assert-True ($r199.classification -eq 'LMAX_R199_PASS_MDREQID_REPAIRED_RUNTIME_ACTIVATION_SANITIZED') 'R199 evidence missing.'
Assert-True ($r199.attemptCount -eq 1) 'R199 attemptCount must be exactly one.'
Assert-True ($r199Gate.validator -eq 'LMAX_R199_VALIDATION_PASS') 'R199 validator evidence missing.'
Assert-True ($r199Selected.selectedProfile -eq 'UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument') 'R199 selected profile mismatch.'
Assert-True ($r199Selected.repairedMdReqIdShapeUsed -eq $true) 'R199 repaired MDReqID shape missing.'

Assert-True ($success.r199EvidencePresent -eq $true) 'R199 success evidence missing.'
Assert-True ($success.r199AttemptCount -eq 1) 'R199 success attemptCount invalid.'
Assert-True ($success.preExternalReservationRejectionCrossedExternalBoundary -eq $false) 'Pre-external reservation rejection crossed boundary.'
Assert-True ($success.marketDataResponseCategory -eq 'Succeeded') 'R199 success category mismatch.'
Assert-True ($success.successConfirmed -eq $true) 'R199 success confirmation missing.'

Assert-True ($mdReq.mdReqIdRepairEffectivenessReviewed -eq $true) 'MDReqID repair effectiveness review missing.'
Assert-True ($mdReq.priorRefTagId -eq 'RefTagID_MDReqID_262') 'Prior MDReqID tag evidence missing.'
Assert-True ($mdReq.priorSessionRejectReason -eq 'SessionRejectReason_ValueIncorrect') 'Prior value-incorrect reason missing.'
Assert-True ($mdReq.r199MdReqIdRepairedShapePreserved -eq $true) 'R199 MDReqID repaired shape not preserved.'
Assert-True ($mdReq.r199RawMdReqIdSerialized -eq $false) 'Raw MDReqID serialized.'
Assert-True ($mdReq.sessionRejectObservedAfterRepair -eq $false) 'SessionReject still observed after repair.'
Assert-True ($mdReq.marketDataRequestRejectObservedAfterRepair -eq $false) 'MarketDataRequestReject observed after repair.'
Assert-True ($mdReq.marketDataResponseCategoryAfterRepair -eq 'Succeeded') 'MDReqID repair did not produce success category.'
Assert-True ($mdReq.rejectReasonExtractionSourceAfterRepair -eq 'NoRejectObserved') 'Reject source should be NoRejectObserved.'
Assert-True ($mdReq.repairResolvedPriorTag262RejectAtSanitizedBoundary -eq $true) 'MDReqID repair effectiveness not confirmed.'

Assert-True ($state.stateEvidenceConfirmed -eq $true) 'State evidence confirmation missing.'
Assert-True ($state.marketDataRequestWriteAttempted -eq $true) 'Write attempted missing.'
Assert-True ($state.marketDataRequestWriteSucceeded -eq $true) 'Write succeeded missing.'
Assert-True ($state.marketDataRequestResponseReadAttempted -eq $true) 'Response read attempted missing.'
Assert-True ($state.marketDataRequestReachedBoundedResponseClassification -eq $true) 'Bounded classification missing.'
Assert-True ($state.stateFieldsConsistent -eq $true) 'State evidence inconsistent.'
Assert-True ($r199State.stateFieldsConsistent -eq $true) 'R199 state evidence inconsistent.'

Assert-True ($reject.rejectAbsenceConfirmed -eq $true) 'Reject absence confirmation missing.'
Assert-True ($reject.marketDataResponseCategory -eq 'Succeeded') 'Reject absence success category mismatch.'
Assert-True ($reject.sessionRejectObserved -eq $false) 'SessionReject observed.'
Assert-True ($reject.marketDataRequestRejectObserved -eq $false) 'MarketDataRequestReject observed.'
Assert-True ($reject.rejectReasonExtractionSource -eq 'NoRejectObserved') 'Reject extraction source mismatch.'
Assert-True ($reject.propagationFailureDetected -eq $false) 'Reject propagation failure detected.'
Assert-True ($r199Reject.rejectObserved -eq $false) 'R199 reject evidence says reject observed.'

Assert-True ($entries.entriesEvidenceReviewed -eq $true) 'Entries evidence review missing.'
Assert-True ($entries.entriesObservedClaimed -eq $false) 'Entries claimed without safe evidence.'
Assert-True ($entries.entryCountClaimed -eq $false) 'Entry count claimed without safe evidence.'
Assert-True ($entries.entriesObservedSafeEvidenceAvailable -eq $false) 'Entries safe evidence unexpectedly claimed.'
Assert-True ($entries.entryCountSafeEvidenceAvailable -eq $false) 'Entry count safe evidence unexpectedly claimed.'
Assert-True ($entries.unsupportedEntriesClaimPresent -eq $false) 'Unsupported entries claim present.'
Assert-True ($entries.decision -eq 'EntriesCountReportingGap') 'Entries review decision mismatch.'
Assert-True ($entries.missingEntriesCountInterpretation -eq 'CliArtifactEmissionGap') 'Entries gap interpretation mismatch.'
Assert-True ($entries.trueZeroNoEntryResponseConfirmed -eq $false) 'True zero/no-entry response claimed without evidence.'

Assert-True ($reporting.cliArtifactReportingReviewed -eq $true) 'CLI/artifact reporting review missing.'
Assert-True ($reporting.r199CliEmittedBoundarySuccess -eq $true) 'Boundary success was not emitted.'
Assert-True ($reporting.r199CliEmittedRejectAbsence -eq $true) 'Reject absence was not emitted.'
Assert-True ($reporting.r199CliEmittedStateFields -eq $true) 'State fields were not emitted.'
Assert-True ($reporting.r199CliEmittedEntriesObserved -eq $false) 'Entries observed unexpectedly emitted.'
Assert-True ($reporting.r199CliEmittedEntryCount -eq $false) 'Entry count unexpectedly emitted.'
Assert-True ($reporting.r199ArtifactsClaimEntries -eq $false) 'R199 artifacts claim entries without safe evidence.'
Assert-True ($reporting.r199ArtifactsClaimEntryCount -eq $false) 'R199 artifacts claim entry count without safe evidence.'
Assert-True ($reporting.entriesCountReportingEnrichmentRecommended -eq $true) 'Entries reporting enrichment not recommended.'

Assert-True ($enablement.controlledReadonlyEnablementDecisionReviewed -eq $true) 'Controlled enablement decision missing.'
Assert-True ($enablement.controlledReadonlyEnablementReadyNow -eq $false) 'Controlled enablement should not be ready yet.'
Assert-True ($enablement.controlledEnablementRecommendedWithoutNoExternalGate -eq $false) 'Controlled enablement recommended without no-external gate.'

Assert-True ($nextAction.nextActionDecisionGatePresent -eq $true) 'Next action decision gate missing.'
Assert-True ($nextAction.decision -eq 'EntriesCountReportingEnrichmentRecommended') 'Next action decision mismatch.'
Assert-True ($nextAction.nextPhase -eq 'LMAX-R201') 'Next phase should be R201.'
Assert-True ($nextAction.nextPhaseMustRemainNoExternal -eq $true) 'R201 must remain no-external.'
Assert-True ($nextAction.liveRetryAllowedNow -eq $false) 'R200 allows live retry.'
Assert-True ($nextAction.controlledEnablementAllowedNow -eq $false) 'R200 allows controlled enablement now.'
Assert-True ($nextAction.entriesClaimedWithoutSafeEvidence -eq $false) 'Entries claimed without safe evidence.'

Assert-True ($forbidden.externalActivationAttempted -eq $false) 'R200 external activation detected.'
Assert-True ($forbidden.socketOpened -eq $false) 'R200 socket/TCP action detected.'
Assert-True ($forbidden.tlsOpened -eq $false) 'R200 TLS action detected.'
Assert-True ($forbidden.fixSessionOpened -eq $false) 'R200 FIX action detected.'
Assert-True ($forbidden.liveMarketDataRequestSent -eq $false) 'R200 live MarketDataRequest detected.'
Assert-True ($forbidden.liveMarketDataResponseRead -eq $false) 'R200 live MarketDataResponse detected.'
Assert-True ($forbidden.ordersIntroduced -eq $false) 'Order path introduced.'
Assert-True ($forbidden.tradingEnabled -eq $false) 'Trading enabled.'
Assert-True ($forbidden.schedulerStarted -eq $false) 'Scheduler started.'
Assert-True ($forbidden.pollingStarted -eq $false) 'Polling started.'
Assert-True ($forbidden.serviceStarted -eq $false) 'Service started.'
Assert-True ($forbidden.replayIntroduced -eq $false) 'Replay introduced.'
Assert-True ($forbidden.shadowReplayIntroduced -eq $false) 'Shadow replay introduced.'
Assert-True ($apiWorker.fakeLmaxGatewayOnlyPreserved -eq $true) 'API/Worker FakeLmaxGatewayOnly regressed.'
Assert-True ($apiWorker.liveGatewayIntroducedInApiOrWorker -eq $false) 'API/Worker live gateway introduced.'

Assert-True ($sanitization.rawFixMessagesSerialized -eq $false) 'Raw FIX serialization risk detected.'
Assert-True ($sanitization.rawRejectTextSerialized -eq $false) 'Raw reject text serialization risk detected.'
Assert-True ($sanitization.rawMdReqIdSerialized -eq $false) 'Raw MDReqID serialization risk detected.'
Assert-True ($sanitization.rawCredentialsSerialized -eq $false) 'Raw credential serialization risk detected.'
Assert-True ($sanitization.rawSessionIdsSerialized -eq $false) 'Raw session ID serialization risk detected.'
Assert-True ($sanitization.rawCompIdsSerialized -eq $false) 'Raw CompID serialization risk detected.'
Assert-True ($sanitization.rawEndpointValuesSerialized -eq $false) 'Raw endpoint serialization risk detected.'
Assert-True ($sanitization.rawTlsMaterialSerialized -eq $false) 'Raw TLS serialization risk detected.'
Assert-True ($sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned=false missing.'

$r199Universe = Read-Json 'phase-lmax-r199-approved-universe-preservation.json'
$r199UsdJpy = Read-Json 'phase-lmax-r199-usdjpy-caveat-preservation.json'
Assert-True ($r199Universe.approvedUniversePreserved -eq $true) 'Approved universe weakened.'
Assert-ContainsAll $r199Universe.approvedInstruments @('GBPUSD', 'EURGBP', 'AUDUSD', 'USDJPY') 'Approved universe missing instruments.'
Assert-True ($r199UsdJpy.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat weakened.'
Assert-True ($r199UsdJpy.usdJpySecurityId -eq '4004') 'USDJPY SecurityID caveat weakened.'
Assert-True ($r199UsdJpy.usdJpySecurityIdSource -eq '8') 'USDJPY SecurityIDSource caveat weakened.'

$artifactText = Get-ChildItem -LiteralPath $ArtifactRoot -Filter 'phase-lmax-r200-*' -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combinedArtifactText = [string]::Join("`n", $artifactText)
foreach ($forbiddenToken in @('35=V', '35=Y', '35=3', '262=', '281=', '371=', '372=', '373=', '58=', [string][char]1, 'password=', 'username=', 'SenderCompId', 'TargetCompId', 'LMAX_READONLY_')) {
    Assert-True (-not $combinedArtifactText.Contains($forbiddenToken)) "Forbidden serialized token detected in R200 artifacts: $forbiddenToken"
}

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R201') 'Next phase recommendation missing.'
Assert-True ($next.r201MustRemainNoExternal -eq $true) 'R201 must remain no-external.'
Assert-True ($next.liveRetryAllowedNow -eq $false) 'R200 allows live retry.'

Assert-True ($gate.build -like 'PASS*') 'Build evidence missing.'
Assert-True ($gate.validator -eq 'LMAX_R200_VALIDATION_PASS') 'Validator evidence missing.'

'LMAX_R200_VALIDATION_PASS'
