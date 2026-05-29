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

$review = Read-Json "$artifactRoot/phase-lmax-r208-partial-remaining-instruments-review.json" 'LMAX_R208_FAIL_NEXT_ACTION_DECISION_MISSING'
$gbpusd = Read-Json "$artifactRoot/phase-lmax-r208-gbpusd-baseline-confirmation.json" 'LMAX_R208_FAIL_R207_EVIDENCE_MISSING'
$eurgbp = Read-Json "$artifactRoot/phase-lmax-r208-eurgbp-success-confirmation.json" 'LMAX_R208_FAIL_EURGBP_SUCCESS_CONFIRMATION_MISSING'
$audusd = Read-Json "$artifactRoot/phase-lmax-r208-audusd-logout-review.json" 'LMAX_R208_FAIL_AUDUSD_LOGOUT_REVIEW_MISSING'
$usdjpy = Read-Json "$artifactRoot/phase-lmax-r208-usdjpy-not-proven-review.json" 'LMAX_R208_FAIL_USDJPY_MISCLASSIFIED'
$audusdDecision = Read-Json "$artifactRoot/phase-lmax-r208-audusd-next-action-decision.json" 'LMAX_R208_FAIL_NEXT_ACTION_DECISION_MISSING'
$caveat = Read-Json "$artifactRoot/phase-lmax-r208-usdjpy-caveat-preservation.json" 'LMAX_R208_FAIL_USDJPY_CAVEAT_WEAKENED'
$decision = Read-Json "$artifactRoot/phase-lmax-r208-next-action-decision-gate.json" 'LMAX_R208_FAIL_NEXT_ACTION_DECISION_MISSING'
$sanitization = Read-Json "$artifactRoot/phase-lmax-r208-sanitization-audit.json" 'LMAX_R208_FAIL_RAW_MARKETDATA_OR_FIX_LEAK_RISK'
$forbidden = Read-Json "$artifactRoot/phase-lmax-r208-forbidden-actions-audit.json" 'LMAX_R208_FAIL_FORBIDDEN_ACTION_RISK'
$apiWorker = Read-Json "$artifactRoot/phase-lmax-r208-api-worker-fake-gateway-audit.json" 'LMAX_R208_FAIL_API_WORKER_GATEWAY_REGRESSION'
$next = Read-Json "$artifactRoot/phase-lmax-r208-next-phase-recommendation.json" 'LMAX_R208_FAIL_NEXT_ACTION_DECISION_MISSING'
$validation = Read-Json "$artifactRoot/phase-lmax-r208-gate-validation.json" 'LMAX_R208_FAIL_BUILD_OR_VALIDATOR'

Assert-True (Test-Path -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r208-partial-remaining-instruments-review-summary.md")) 'LMAX_R208_FAIL_NEXT_ACTION_DECISION_MISSING' 'R208 summary missing.'
Assert-True (Test-Path -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r208-scope-limits-note.md")) 'LMAX_R208_FAIL_NEXT_ACTION_DECISION_MISSING' 'Scope limits note missing.'

Assert-True ($review.noExternal -eq $true) 'LMAX_R208_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R208 must remain no-external.'
Assert-True ($review.newExternalActivationAttempted -eq $false -and $review.socketTlsFixMarketDataRuntimeActionDetected -eq $false) 'LMAX_R208_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R208 performed external/runtime action.'
Assert-True ($forbidden.newExternalActivationAttempted -eq $false -and $forbidden.socketOpened -eq $false -and $forbidden.tlsOpened -eq $false -and $forbidden.fixOpened -eq $false) 'LMAX_R208_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R208 opened socket/TLS/FIX.'
Assert-True ($forbidden.liveMarketDataRequestSent -eq $false -and $forbidden.liveMarketDataResponseRead -eq $false) 'LMAX_R208_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R208 performed live MarketData action.'

$r203 = Read-Json "$artifactRoot/phase-lmax-r203-entries-reporting-activation.json" 'LMAX_R208_FAIL_R207_EVIDENCE_MISSING'
$r207 = Read-Json "$artifactRoot/phase-lmax-r207-activation.json" 'LMAX_R208_FAIL_R207_EVIDENCE_MISSING'
$r207Instruments = Read-Json "$artifactRoot/phase-lmax-r207-instrument-evidence.json" 'LMAX_R208_FAIL_R207_EVIDENCE_MISSING'

Assert-True ($r203.classification -eq 'LMAX_R203_PASS_ENTRIES_REPORTING_RUNTIME_ACTIVATION_SANITIZED') 'LMAX_R208_FAIL_R207_EVIDENCE_MISSING' 'R203 baseline evidence missing.'
Assert-True ($gbpusd.gbpusdBaselineConfirmed -eq $true -and $gbpusd.marketDataResponseCategory -eq 'Succeeded' -and $gbpusd.entriesObserved -eq $true -and $gbpusd.sanitizedEntryCount -eq 2) 'LMAX_R208_FAIL_R207_EVIDENCE_MISSING' 'GBPUSD baseline confirmation missing.'

Assert-True ($r207.classification -eq 'LMAX_R207_PASS_PARTIAL_REMAINING_INSTRUMENTS_SEQUENTIAL_ENTRIES_RUNTIME_ACTIVATION_SANITIZED') 'LMAX_R208_FAIL_R207_EVIDENCE_MISSING' 'R207 partial success evidence missing.'
Assert-True ($r207.attemptCount -eq 1 -and $review.r207AttemptCount -eq 1) 'LMAX_R208_FAIL_R207_EVIDENCE_MISSING' 'R207 attemptCount must be 1.'

$r207EurGbp = $r207Instruments.instruments | Where-Object { $_.selectedInstrument -eq 'EURGBP' } | Select-Object -First 1
$r207AudUsd = $r207Instruments.instruments | Where-Object { $_.selectedInstrument -eq 'AUDUSD' } | Select-Object -First 1
$r207UsdJpy = $r207Instruments.instruments | Where-Object { $_.selectedInstrument -eq 'USDJPY' } | Select-Object -First 1
Assert-True ($null -ne $r207EurGbp -and $null -ne $r207AudUsd -and $null -ne $r207UsdJpy) 'LMAX_R208_FAIL_R207_EVIDENCE_MISSING' 'R207 per-instrument evidence missing.'

Assert-True ($eurgbp.eurgbpSuccessConfirmed -eq $true -and $eurgbp.securityId -eq '4003' -and $eurgbp.securityIdSource -eq '8') 'LMAX_R208_FAIL_EURGBP_SUCCESS_CONFIRMATION_MISSING' 'EURGBP success confirmation missing.'
Assert-True ($eurgbp.marketDataResponseCategory -eq 'Succeeded' -and $eurgbp.entriesObserved -eq $true -and $eurgbp.sanitizedEntryCount -eq 2) 'LMAX_R208_FAIL_EURGBP_SUCCESS_CONFIRMATION_MISSING' 'EURGBP entries success missing.'
Assert-True ($r207EurGbp.marketDataResponseCategory -eq 'Succeeded' -and $r207EurGbp.marketDataEntriesObserved -eq $true -and $r207EurGbp.marketDataSanitizedEntryCount -eq 2) 'LMAX_R208_FAIL_EURGBP_SUCCESS_CONFIRMATION_MISSING' 'R207 EURGBP evidence mismatch.'

Assert-True ($audusd.audusdLogoutReviewPresent -eq $true -and $audusd.securityId -eq '4007' -and $audusd.securityIdSource -eq '8') 'LMAX_R208_FAIL_AUDUSD_LOGOUT_REVIEW_MISSING' 'AUDUSD logout review missing.'
Assert-True ($audusd.marketDataResponseCategory -eq 'LogoutObserved' -and $audusd.logoutObserved -eq $true) 'LMAX_R208_FAIL_AUDUSD_LOGOUT_REVIEW_MISSING' 'AUDUSD LogoutObserved evidence missing.'
Assert-True ($audusd.sanitizedLogoutReasonAvailable -eq $false -and $audusd.sanitizedLogoutReasonCategory -eq 'LogoutReasonNotAvailable') 'LMAX_R208_FAIL_AUDUSD_LOGOUT_REVIEW_MISSING' 'AUDUSD logout reason availability review missing.'
Assert-True ($audusd.leadingDecision -eq 'LogoutReasonExtractionReportingEnrichment') 'LMAX_R208_FAIL_NEXT_ACTION_DECISION_MISSING' 'AUDUSD leading decision missing.'

Assert-True ($usdjpy.usdjpyNotProvenReviewPresent -eq $true -and $usdjpy.securityId -eq '4004' -and $usdjpy.securityIdSource -eq '8') 'LMAX_R208_FAIL_USDJPY_MISCLASSIFIED' 'USDJPY not-proven review missing.'
Assert-True ($usdjpy.usableMarketDataEvidenceAvailable -eq $false -and $usdjpy.classifiedAsFailed -eq $false -and $usdjpy.failureClaimed -eq $false) 'LMAX_R208_FAIL_USDJPY_MISCLASSIFIED' 'USDJPY incorrectly classified as failed.'
Assert-True ($usdjpy.usdJpyCaveatPreserved -eq $true -and $caveat.usdJpyCaveatPreserved -eq $true -and $caveat.usdJpySecurityId -eq '4004' -and $caveat.usdJpySecurityIdSource -eq '8') 'LMAX_R208_FAIL_USDJPY_CAVEAT_WEAKENED' 'USDJPY caveat weakened.'

Assert-True ($audusdDecision.audusdNextActionDecisionPresent -eq $true -and $audusdDecision.logoutReasonExtractionReportingEnrichmentRecommended -eq $true) 'LMAX_R208_FAIL_NEXT_ACTION_DECISION_MISSING' 'AUDUSD next action decision missing.'
Assert-True ($audusdDecision.audusdOnlyRetryReadinessRecommendedNow -eq $false -and $audusdDecision.multiInstrumentLiveRetryAllowedBeforeAudusdIsolationOrReview -eq $false) 'LMAX_R208_FAIL_NEXT_ACTION_DECISION_MISSING' 'Next action allows premature retry.'
Assert-True ($decision.nextActionDecisionPresent -eq $true -and $decision.r209MustRemainNoExternal -eq $true -and $decision.anotherMultiInstrumentLiveRetryAllowed -eq $false -and $decision.liveRetryBeforeLogoutReasonEnrichmentAllowed -eq $false) 'LMAX_R208_FAIL_NEXT_ACTION_DECISION_MISSING' 'Decision gate missing or unsafe.'
Assert-True ($next.r209MustRemainNoExternal -eq $true -and $next.multiInstrumentLiveRetryAllowedBeforeAudusdIsolationOrReview -eq $false) 'LMAX_R208_FAIL_NEXT_ACTION_DECISION_MISSING' 'Next recommendation allows multi-instrument retry too early.'

Assert-True ($sanitization.credentialValuesReturned -eq $false) 'LMAX_R208_FAIL_RAW_MARKETDATA_OR_FIX_LEAK_RISK' 'credentialValuesReturned=false missing.'
Assert-True ($sanitization.rawMarketDataPricesSerialized -eq $false -and $sanitization.rawBidAskValuesSerialized -eq $false -and $sanitization.rawMarketDataPayloadSerialized -eq $false) 'LMAX_R208_FAIL_RAW_MARKETDATA_OR_FIX_LEAK_RISK' 'Raw market-data leak risk.'
Assert-True ($sanitization.rawFixMessagesSerialized -eq $false -and $sanitization.rawRejectTextSerialized -eq $false -and $sanitization.rawLogoutTextSerialized -eq $false) 'LMAX_R208_FAIL_RAW_LOGOUT_OR_REJECT_LEAK_RISK' 'Raw FIX/reject/logout leak risk.'
Assert-True ($sanitization.rawMdReqIdSerialized -eq $false -and $sanitization.rawCredentialsSerialized -eq $false -and $sanitization.rawSessionIdsSerialized -eq $false -and $sanitization.rawCompIdsSerialized -eq $false -and $sanitization.rawEndpointValuesSerialized -eq $false -and $sanitization.rawTlsMaterialSerialized -eq $false) 'LMAX_R208_FAIL_RAW_MARKETDATA_OR_FIX_LEAK_RISK' 'Secret/session/endpoint/TLS/MDReqID leak risk.'

Assert-True ($caveat.approvedUniversePreserved -eq $true -and $caveat.approvedInstruments -contains 'GBPUSD' -and $caveat.approvedInstruments -contains 'EURGBP' -and $caveat.approvedInstruments -contains 'AUDUSD' -and $caveat.approvedInstruments -contains 'USDJPY') 'LMAX_R208_FAIL_USDJPY_CAVEAT_WEAKENED' 'Approved universe weakened.'

Assert-True ($forbidden.ordersIntroduced -eq $false -and $forbidden.tradingEnabled -eq $false -and $forbidden.tradingStateMutated -eq $false) 'LMAX_R208_FAIL_FORBIDDEN_ACTION_RISK' 'Order/trading path introduced.'
Assert-True ($forbidden.schedulerStarted -eq $false -and $forbidden.pollingStarted -eq $false -and $forbidden.serviceStarted -eq $false) 'LMAX_R208_FAIL_FORBIDDEN_ACTION_RISK' 'Scheduler/polling/service introduced.'
Assert-True ($forbidden.replayIntroduced -eq $false -and $forbidden.shadowReplayIntroduced -eq $false) 'LMAX_R208_FAIL_FORBIDDEN_ACTION_RISK' 'Replay/shadow replay introduced.'
Assert-True ($apiWorker.fakeLmaxGatewayOnlyPreserved -eq $true -and $apiWorker.liveGatewayIntroducedInApiOrWorker -eq $false -and $apiWorker.apiWorkerStarted -eq $false) 'LMAX_R208_FAIL_API_WORKER_GATEWAY_REGRESSION' 'API/Worker FakeLmaxGatewayOnly regression.'

$scopeNote = Get-Content -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r208-scope-limits-note.md") -Raw
foreach ($required in @('does not prove', 'AUDUSD live success', 'USDJPY live success or failure', 'does not authorize API/Worker', 'multi-instrument live retry')) {
    Assert-True ($scopeNote.Contains($required)) 'LMAX_R208_FAIL_NEXT_ACTION_DECISION_MISSING' "Scope limit missing: $required"
}

Assert-True ($validation.buildStatus -eq 'PASS' -and $validation.validator -eq 'LMAX_R208_VALIDATION_PASS' -and $validation.validatorStatus -eq 'PASS') 'LMAX_R208_FAIL_BUILD_OR_VALIDATOR' 'Build/validator evidence missing.'

Write-Output 'LMAX_R208_VALIDATION_PASS'
