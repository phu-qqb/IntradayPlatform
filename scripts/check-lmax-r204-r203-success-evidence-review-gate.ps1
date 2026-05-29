param(
    [string]$Root = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'

function Fail([string]$Classification, [string]$Message) {
    Write-Error "$Classification $Message"
    exit 1
}

function Assert-True($Condition, [string]$Classification, [string]$Message) {
    if (-not $Condition) {
        Fail $Classification $Message
    }
}

function Read-Json([string]$RelativePath, [string]$Classification) {
    $path = Join-Path $Root $RelativePath
    Assert-True (Test-Path -LiteralPath $path) $Classification "Missing $RelativePath"
    return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$artifactRoot = 'artifacts/readiness/lmax-runtime-enablement'

$review = Read-Json "$artifactRoot/phase-lmax-r204-r203-success-evidence-review.json" 'LMAX_R204_FAIL_SUCCESS_CONFIRMATION_MISSING'
$entries = Read-Json "$artifactRoot/phase-lmax-r204-entries-evidence-confirmation.json" 'LMAX_R204_FAIL_ENTRIES_EVIDENCE_UNSUPPORTED'
$mdReqId = Read-Json "$artifactRoot/phase-lmax-r204-mdreqid-repair-effectiveness.json" 'LMAX_R204_FAIL_SUCCESS_CONFIRMATION_MISSING'
$state = Read-Json "$artifactRoot/phase-lmax-r204-final-state-evidence-confirmation.json" 'LMAX_R204_FAIL_SUCCESS_CONFIRMATION_MISSING'
$reject = Read-Json "$artifactRoot/phase-lmax-r204-reject-absence-confirmation.json" 'LMAX_R204_FAIL_SUCCESS_CONFIRMATION_MISSING'
$shutdown = Read-Json "$artifactRoot/phase-lmax-r204-shutdown-revert-confirmation.json" 'LMAX_R204_FAIL_SUCCESS_CONFIRMATION_MISSING'
$archive = Read-Json "$artifactRoot/phase-lmax-r204-controlled-readonly-archive-gate.json" 'LMAX_R204_FAIL_SCOPE_OVEREXPANSION_RISK'
$decision = Read-Json "$artifactRoot/phase-lmax-r204-next-action-decision-gate.json" 'LMAX_R204_FAIL_SCOPE_OVEREXPANSION_RISK'
$sanitization = Read-Json "$artifactRoot/phase-lmax-r204-sanitization-audit.json" 'LMAX_R204_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK'
$forbidden = Read-Json "$artifactRoot/phase-lmax-r204-forbidden-actions-audit.json" 'LMAX_R204_FAIL_FORBIDDEN_ACTION_RISK'
$apiWorker = Read-Json "$artifactRoot/phase-lmax-r204-api-worker-fake-gateway-audit.json" 'LMAX_R204_FAIL_API_WORKER_GATEWAY_REGRESSION'
$next = Read-Json "$artifactRoot/phase-lmax-r204-next-phase-recommendation.json" 'LMAX_R204_FAIL_SCOPE_OVEREXPANSION_RISK'

Assert-True (Test-Path -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r204-r203-success-evidence-review-summary.md")) 'LMAX_R204_FAIL_SUCCESS_CONFIRMATION_MISSING' 'R204 summary missing.'
Assert-True (Test-Path -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r204-scope-limits-and-non-expansion-note.md")) 'LMAX_R204_FAIL_SCOPE_OVEREXPANSION_RISK' 'Scope limits note missing.'

Assert-True ($review.noExternal -eq $true) 'LMAX_R204_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R204 must remain no-external.'
Assert-True ($review.newExternalActivationAttempted -eq $false) 'LMAX_R204_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'New external activation detected.'
Assert-True ($review.socketTlsFixMarketDataRuntimeActionDetected -eq $false) 'LMAX_R204_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'Runtime boundary action detected.'
Assert-True ($forbidden.socketOpened -eq $false -and $forbidden.tlsOpened -eq $false -and $forbidden.fixOpened -eq $false) 'LMAX_R204_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'Socket/TLS/FIX action detected.'
Assert-True ($forbidden.liveMarketDataRequestSent -eq $false -and $forbidden.liveMarketDataResponseRead -eq $false) 'LMAX_R204_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'Live market-data action detected.'

$r203Activation = Read-Json "$artifactRoot/phase-lmax-r203-entries-reporting-activation.json" 'LMAX_R204_FAIL_R203_EVIDENCE_MISSING'
$r203Entries = Read-Json "$artifactRoot/phase-lmax-r203-entries-evidence.json" 'LMAX_R204_FAIL_R203_EVIDENCE_MISSING'
$r203Gate = Read-Json "$artifactRoot/phase-lmax-r203-gate-validation.json" 'LMAX_R204_FAIL_R203_EVIDENCE_MISSING'

Assert-True ($r203Activation.classification -eq 'LMAX_R203_PASS_ENTRIES_REPORTING_RUNTIME_ACTIVATION_SANITIZED') 'LMAX_R204_FAIL_R203_EVIDENCE_MISSING' 'R203 pass classification missing.'
Assert-True ($r203Activation.attemptCount -eq 1 -and $review.r203AttemptCount -eq 1) 'LMAX_R204_FAIL_R203_ATTEMPT_COUNT_INVALID' 'R203 attemptCount must be 1.'
Assert-True ($r203Activation.marketDataResponseCategory -eq 'Succeeded' -and $review.r203MarketDataResponseCategory -eq 'Succeeded') 'LMAX_R204_FAIL_SUCCESS_CONFIRMATION_MISSING' 'R203 success confirmation missing.'
Assert-True ($review.r203ArchivedAsFirstSuccessfulSanitizedGbpusdRuntimeMarketDataEvidence -eq $true) 'LMAX_R204_FAIL_SUCCESS_CONFIRMATION_MISSING' 'R203 archive confirmation missing.'

Assert-True ($entries.entriesEvidenceConfirmed -eq $true) 'LMAX_R204_FAIL_ENTRIES_EVIDENCE_UNSUPPORTED' 'Entries evidence confirmation missing.'
Assert-True ($entries.entriesObserved -eq $true -and $r203Entries.marketDataEntriesObserved -eq $true) 'LMAX_R204_FAIL_ENTRIES_EVIDENCE_UNSUPPORTED' 'Entries observed unsupported.'
Assert-True ($entries.sanitizedEntryCount -eq 2 -and $r203Entries.marketDataSanitizedEntryCount -eq 2) 'LMAX_R204_FAIL_ENTRIES_EVIDENCE_UNSUPPORTED' 'Sanitized entry count unsupported.'
Assert-True ($entries.entriesEvidenceCategory -eq 'EntriesObservedWithSanitizedCount') 'LMAX_R204_FAIL_ENTRIES_EVIDENCE_UNSUPPORTED' 'Entries category mismatch.'
Assert-True ($entries.entriesReportingSource -eq 'MarketDataResponseParserClassifierEntryCount') 'LMAX_R204_FAIL_ENTRIES_EVIDENCE_UNSUPPORTED' 'Entries source mismatch.'
Assert-True ($entries.entriesClaimSupportedBySafeR203Evidence -eq $true) 'LMAX_R204_FAIL_ENTRIES_EVIDENCE_UNSUPPORTED' 'Entries claim lacks safe evidence.'

Assert-True ($mdReqId.mdReqIdRepairEffectivenessConfirmed -eq $true) 'LMAX_R204_FAIL_SUCCESS_CONFIRMATION_MISSING' 'MDReqID repair effectiveness missing.'
Assert-True ($mdReqId.priorRejectRefTagId -eq 'RefTagID_MDReqID_262' -and $mdReqId.priorRejectReason -eq 'SessionRejectReason_ValueIncorrect') 'LMAX_R204_FAIL_SUCCESS_CONFIRMATION_MISSING' 'Prior MDReqID reject evidence missing.'
Assert-True ($mdReqId.r203MarketDataResponseCategory -eq 'Succeeded' -and $mdReqId.r203RejectedForMdReqId -eq $false) 'LMAX_R204_FAIL_SUCCESS_CONFIRMATION_MISSING' 'MDReqID repair did not resolve prior reject.'

Assert-True ($state.finalStateEvidenceConfirmed -eq $true) 'LMAX_R204_FAIL_SUCCESS_CONFIRMATION_MISSING' 'Final-state confirmation missing.'
Assert-True ($state.marketDataRequestWriteAttempted -eq $true -and $state.marketDataRequestWriteSucceeded -eq $true -and $state.marketDataRequestResponseReadAttempted -eq $true -and $state.marketDataRequestReachedBoundedResponseClassification -eq $true) 'LMAX_R204_FAIL_SUCCESS_CONFIRMATION_MISSING' 'State evidence incomplete.'
Assert-True ($state.marketDataRequestSentLegacyFlag -eq $false -and $state.stateFieldsConsistent -eq $true) 'LMAX_R204_FAIL_SUCCESS_CONFIRMATION_MISSING' 'State evidence inconsistent.'

Assert-True ($reject.rejectAbsenceConfirmed -eq $true) 'LMAX_R204_FAIL_SUCCESS_CONFIRMATION_MISSING' 'Reject absence confirmation missing.'
Assert-True ($reject.sessionRejectObserved -eq $false -and $reject.marketDataRequestRejectObserved -eq $false -and $reject.rejectReasonExtractionSource -eq 'NoRejectObserved') 'LMAX_R204_FAIL_SUCCESS_CONFIRMATION_MISSING' 'Reject absence mismatch.'

Assert-True ($shutdown.shutdownRevertConfirmed -eq $true -and $shutdown.r203ShutdownRevertCompleted -eq $true -and $shutdown.r203ShutdownRevertStatus -eq 'Succeeded') 'LMAX_R204_FAIL_SUCCESS_CONFIRMATION_MISSING' 'Shutdown/revert confirmation missing.'
Assert-True ($shutdown.r203StoppedAfterSingleBoundedAttempt -eq $true) 'LMAX_R204_FAIL_SUCCESS_CONFIRMATION_MISSING' 'R203 did not stop after one attempt.'

Assert-True ($archive.archiveGateReady -eq $true -and $archive.controlledReadOnlyEnablementAuthorized -eq $false) 'LMAX_R204_FAIL_SCOPE_OVEREXPANSION_RISK' 'Archive gate missing or over-authorized.'
Assert-True ($archive.apiWorkerLiveGatewayAuthorized -eq $false -and $archive.tradingOrOrdersAuthorized -eq $false -and $archive.schedulerPollingServiceAuthorized -eq $false -and $archive.replayOrShadowReplayAuthorized -eq $false) 'LMAX_R204_FAIL_SCOPE_OVEREXPANSION_RISK' 'Scope over-expansion risk.'
Assert-True ($archive.remainingInstrumentExpansionRequiresNoExternalReadinessGate -eq $true -and $archive.futureLiveExpansionAllowedWithoutGate -eq $false) 'LMAX_R204_FAIL_SCOPE_OVEREXPANSION_RISK' 'Live expansion allowed without readiness gate.'
Assert-True ($decision.liveExpansionRecommendedNow -eq $false -and $decision.liveExpansionRequiresNoExternalReadinessGate -eq $true) 'LMAX_R204_FAIL_SCOPE_OVEREXPANSION_RISK' 'Next action allows live expansion too early.'

$scopeNote = Get-Content -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r204-scope-limits-and-non-expansion-note.md") -Raw
foreach ($requiredText in @('does not prove EURGBP', 'does not prove AUDUSD', 'does not prove USDJPY', 'does not authorize API/Worker', 'does not authorize orders', 'does not authorize scheduler')) {
    Assert-True ($scopeNote.Contains($requiredText)) 'LMAX_R204_FAIL_SCOPE_OVEREXPANSION_RISK' "Scope limit missing: $requiredText"
}

Assert-True ($entries.rawPricesSerialized -eq $false -and $entries.rawBidAskValuesSerialized -eq $false -and $entries.rawMarketDataPayloadSerialized -eq $false) 'LMAX_R204_FAIL_RAW_MARKETDATA_OR_FIX_LEAK_RISK' 'Entries artifact contains raw market-data risk.'
Assert-True ($sanitization.rawMarketDataPricesSerialized -eq $false -and $sanitization.rawBidAskValuesSerialized -eq $false -and $sanitization.rawMarketDataPayloadSerialized -eq $false) 'LMAX_R204_FAIL_RAW_MARKETDATA_OR_FIX_LEAK_RISK' 'Raw market-data serialization risk.'
Assert-True ($sanitization.rawFixMessagesSerialized -eq $false) 'LMAX_R204_FAIL_RAW_MARKETDATA_OR_FIX_LEAK_RISK' 'Raw FIX serialization risk.'
Assert-True ($sanitization.rawRejectTextSerialized -eq $false -and $sanitization.rawMdReqIdSerialized -eq $false) 'LMAX_R204_FAIL_RAW_MDREQID_OR_REJECT_LEAK_RISK' 'Raw reject or MDReqID serialization risk.'
Assert-True ($sanitization.rawCredentialsSerialized -eq $false -and $sanitization.rawSessionIdsSerialized -eq $false -and $sanitization.rawCompIdsSerialized -eq $false -and $sanitization.rawEndpointValuesSerialized -eq $false -and $sanitization.rawTlsMaterialSerialized -eq $false) 'LMAX_R204_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' 'Secret/session/endpoint/TLS serialization risk.'
Assert-True ($sanitization.credentialValuesReturned -eq $false) 'LMAX_R204_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' 'credentialValuesReturned=false missing.'

$r203Universe = Read-Json "$artifactRoot/phase-lmax-r203-approved-universe-preservation.json" 'LMAX_R204_FAIL_USDJPY_CAVEAT_WEAKENED'
$r203UsdJpy = Read-Json "$artifactRoot/phase-lmax-r203-usdjpy-caveat-preservation.json" 'LMAX_R204_FAIL_USDJPY_CAVEAT_WEAKENED'
Assert-True ($r203Universe.approvedUniversePreserved -eq $true -and $r203Universe.approvedInstruments -contains 'GBPUSD' -and $r203Universe.approvedInstruments -contains 'EURGBP' -and $r203Universe.approvedInstruments -contains 'AUDUSD' -and $r203Universe.approvedInstruments -contains 'USDJPY') 'LMAX_R204_FAIL_USDJPY_CAVEAT_WEAKENED' 'Approved universe weakened.'
Assert-True ($r203UsdJpy.usdJpyCaveatPreserved -eq $true -and $r203UsdJpy.usdJpySecurityId -eq '4004' -and $r203UsdJpy.usdJpySecurityIdSource -eq '8') 'LMAX_R204_FAIL_USDJPY_CAVEAT_WEAKENED' 'USDJPY caveat weakened.'

Assert-True ($forbidden.ordersIntroduced -eq $false -and $forbidden.tradingEnabled -eq $false -and $forbidden.tradingStateMutated -eq $false) 'LMAX_R204_FAIL_FORBIDDEN_ACTION_RISK' 'Order/trading path introduced.'
Assert-True ($forbidden.schedulerStarted -eq $false -and $forbidden.pollingStarted -eq $false -and $forbidden.serviceStarted -eq $false) 'LMAX_R204_FAIL_FORBIDDEN_ACTION_RISK' 'Scheduler/polling/service introduced.'
Assert-True ($forbidden.replayIntroduced -eq $false -and $forbidden.shadowReplayIntroduced -eq $false) 'LMAX_R204_FAIL_FORBIDDEN_ACTION_RISK' 'Replay/shadow replay introduced.'
Assert-True ($apiWorker.fakeLmaxGatewayOnlyPreserved -eq $true -and $apiWorker.liveGatewayIntroducedInApiOrWorker -eq $false -and $apiWorker.apiWorkerStarted -eq $false) 'LMAX_R204_FAIL_API_WORKER_GATEWAY_REGRESSION' 'API/Worker FakeLmaxGatewayOnly regression.'

Assert-True ($r203Gate.validator -eq 'LMAX_R203_VALIDATION_PASS') 'LMAX_R204_FAIL_R203_EVIDENCE_MISSING' 'R203 validator evidence missing.'
Assert-True ($review.buildEvidence -like '*PASS*' -and $review.validatorEvidence -eq 'LMAX_R204_VALIDATION_PASS') 'LMAX_R204_FAIL_BUILD_OR_VALIDATOR' 'Build/validator evidence missing.'
Assert-True ($next.r205MustRemainNoExternal -eq $true -and $next.liveExpansionAllowedBeforeR205 -eq $false) 'LMAX_R204_FAIL_SCOPE_OVEREXPANSION_RISK' 'Next recommendation allows premature live expansion.'

Write-Output 'LMAX_R204_VALIDATION_PASS'
