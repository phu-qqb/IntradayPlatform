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

Assert-True (Test-Path -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r212-audusd-runtime-boundary-inconclusive-review-summary.md")) 'LMAX_R212_FAIL_TLS_BOUNDARY_REVIEW_MISSING' 'R212 summary missing.'

$review = Read-Json "$artifactRoot/phase-lmax-r212-audusd-runtime-boundary-inconclusive-review.json" 'LMAX_R212_FAIL_TLS_BOUNDARY_REVIEW_MISSING'
$tls = Read-Json "$artifactRoot/phase-lmax-r212-r211-tls-boundary-review.json" 'LMAX_R212_FAIL_TLS_BOUNDARY_REVIEW_MISSING'
$audusd = Read-Json "$artifactRoot/phase-lmax-r212-audusd-not-tested-confirmation.json" 'LMAX_R212_FAIL_AUDUSD_MISCLASSIFIED'
$state = Read-Json "$artifactRoot/phase-lmax-r212-state-evidence-consistency-confirmation.json" 'LMAX_R212_FAIL_TLS_BOUNDARY_REVIEW_MISSING'
$request = Read-Json "$artifactRoot/phase-lmax-r212-no-marketdatarequest-sent-confirmation.json" 'LMAX_R212_FAIL_MARKETDATAREQUEST_CLAIM_UNSUPPORTED'
$conclusion = Read-Json "$artifactRoot/phase-lmax-r212-no-reject-logout-entries-conclusion.json" 'LMAX_R212_FAIL_REJECT_LOGOUT_ENTRIES_CLAIM_UNSUPPORTED'
$baseline = Read-Json "$artifactRoot/phase-lmax-r212-gbpusd-eurgbp-baseline-preservation.json" 'LMAX_R212_FAIL_AUDUSD_MISCLASSIFIED'
$usdjpy = Read-Json "$artifactRoot/phase-lmax-r212-usdjpy-not-proven-and-caveat-preservation.json" 'LMAX_R212_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED'
$decision = Read-Json "$artifactRoot/phase-lmax-r212-next-action-decision-gate.json" 'LMAX_R212_FAIL_TLS_BOUNDARY_REVIEW_MISSING'
$sanitization = Read-Json "$artifactRoot/phase-lmax-r212-sanitization-audit.json" 'LMAX_R212_FAIL_RAW_TLS_OR_ENDPOINT_LEAK_RISK'
$forbidden = Read-Json "$artifactRoot/phase-lmax-r212-forbidden-actions-audit.json" 'LMAX_R212_FAIL_FORBIDDEN_ACTION_RISK'
$apiWorker = Read-Json "$artifactRoot/phase-lmax-r212-api-worker-fake-gateway-audit.json" 'LMAX_R212_FAIL_API_WORKER_GATEWAY_REGRESSION'
$next = Read-Json "$artifactRoot/phase-lmax-r212-next-phase-recommendation.json" 'LMAX_R212_FAIL_TLS_BOUNDARY_REVIEW_MISSING'
$validation = Read-Json "$artifactRoot/phase-lmax-r212-gate-validation.json" 'LMAX_R212_FAIL_BUILD_OR_VALIDATOR'

$r211 = Read-Json "$artifactRoot/phase-lmax-r211-activation.json" 'LMAX_R212_FAIL_R211_EVIDENCE_MISSING'
$r211Boundary = Read-Json "$artifactRoot/phase-lmax-r211-boundary-evidence.json" 'LMAX_R212_FAIL_R211_EVIDENCE_MISSING'
$r211State = Read-Json "$artifactRoot/phase-lmax-r211-state-evidence.json" 'LMAX_R212_FAIL_R211_EVIDENCE_MISSING'
$r211Request = Read-Json "$artifactRoot/phase-lmax-r211-marketdata-request-evidence.json" 'LMAX_R212_FAIL_R211_EVIDENCE_MISSING'
$r211Entries = Read-Json "$artifactRoot/phase-lmax-r211-entries-evidence.json" 'LMAX_R212_FAIL_R211_EVIDENCE_MISSING'

Assert-True ($review.noExternal -eq $true -and $review.newExternalActivationAttempted -eq $false -and $review.socketTlsFixMarketDataRuntimeActionDetected -eq $false) 'LMAX_R212_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R212 must remain no-external.'
Assert-True ($forbidden.newExternalActivationAttempted -eq $false -and $forbidden.socketOpened -eq $false -and $forbidden.tlsOpened -eq $false -and $forbidden.fixOpened -eq $false) 'LMAX_R212_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R212 opened socket/TLS/FIX.'
Assert-True ($forbidden.liveMarketDataRequestSent -eq $false -and $forbidden.liveMarketDataResponseRead -eq $false) 'LMAX_R212_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R212 performed live MarketData action.'

Assert-True ($r211.classification -eq 'LMAX_R211_FAIL_AUDUSD_RUNTIME_BOUNDARY_INCONCLUSIVE') 'LMAX_R212_FAIL_R211_EVIDENCE_MISSING' 'R211 classification missing.'
Assert-True ($r211.attemptCount -eq 1 -and $review.r211AttemptCount -eq 1) 'LMAX_R212_FAIL_R211_EVIDENCE_MISSING' 'R211 attemptCount must be 1.'
Assert-True ($r211.selectedInstrument -eq 'AUDUSD' -and $r211.marketDataResponseCategory -eq 'MarketDataNotAttempted') 'LMAX_R212_FAIL_AUDUSD_MISCLASSIFIED' 'R211 AUDUSD boundary evidence mismatch.'

Assert-True ($tls.tlsBoundaryReviewPresent -eq $true) 'LMAX_R212_FAIL_TLS_BOUNDARY_REVIEW_MISSING' 'TLS boundary review missing.'
Assert-True ($tls.tcpConnectionAttempted -eq $true -and $tls.realSocketOpened -eq $true -and $tls.tlsHandshakeAttempted -eq $true) 'LMAX_R212_FAIL_TLS_BOUNDARY_REVIEW_MISSING' 'TLS boundary attempt evidence missing.'
Assert-True ($tls.tlsSucceeded -eq $false -and $tls.tlsBoundaryStatus -eq 'Failed' -and $tls.tlsResultCategory -eq 'HandshakeException' -and $tls.transportResultCategory -eq 'TlsHandshakeBoundaryFailed') 'LMAX_R212_FAIL_TLS_BOUNDARY_REVIEW_MISSING' 'TLS sanitized category mismatch.'
Assert-True ($tls.fixLogonAttempted -eq $false -and $tls.marketDataBoundaryStatus -eq 'NotAttempted') 'LMAX_R212_FAIL_TLS_BOUNDARY_REVIEW_MISSING' 'FIX/MarketData unexpectedly attempted.'
Assert-True ($tls.rawTlsMaterialSerialized -eq $false -and $tls.rawEndpointValuesSerialized -eq $false) 'LMAX_R212_FAIL_RAW_TLS_OR_ENDPOINT_LEAK_RISK' 'Raw TLS/endpoint leak risk.'
Assert-True ($r211Boundary.tlsSucceeded -eq $false -and $r211Boundary.fixLogonAttempted -eq $false -and $r211Boundary.marketDataBoundaryStatus -eq 'NotAttempted') 'LMAX_R212_FAIL_TLS_BOUNDARY_REVIEW_MISSING' 'R211 boundary artifact mismatch.'

Assert-True ($audusd.audusdNotTested -eq $true -and $audusd.audusdClassifiedAsFailed -eq $false) 'LMAX_R212_FAIL_AUDUSD_MISCLASSIFIED' 'AUDUSD misclassified.'
Assert-True ($audusd.r211MarketDataResponseCategory -eq 'MarketDataNotAttempted') 'LMAX_R212_FAIL_AUDUSD_MISCLASSIFIED' 'R211 treated as AUDUSD MarketData evidence.'
Assert-True ($audusd.audusdLogoutConclusionSupported -eq $false -and $audusd.audusdRejectConclusionSupported -eq $false -and $audusd.audusdEntriesConclusionSupported -eq $false) 'LMAX_R212_FAIL_REJECT_LOGOUT_ENTRIES_CLAIM_UNSUPPORTED' 'Unsupported AUDUSD conclusion claimed.'

Assert-True ($request.marketDataRequestSent -eq $false -and $request.marketDataRequestWriteAttempted -eq $false -and $request.marketDataRequestWriteSucceeded -eq $false) 'LMAX_R212_FAIL_MARKETDATAREQUEST_CLAIM_UNSUPPORTED' 'MarketDataRequest sent/write claim unsupported.'
Assert-True ($r211Request.requestSent -eq $false -and $r211Request.writeAttempted -eq $false -and $r211Request.writeSucceeded -eq $false) 'LMAX_R212_FAIL_MARKETDATAREQUEST_CLAIM_UNSUPPORTED' 'R211 request artifact claims request sent.'

Assert-True ($state.stateEvidenceConsistencyReviewPresent -eq $true -and $state.stateFieldsConsistent -eq $true -and $state.stateFieldsContradictionDetected -eq $false) 'LMAX_R212_FAIL_TLS_BOUNDARY_REVIEW_MISSING' 'State evidence consistency review missing.'
Assert-True ($state.marketDataRequestWriteAttempted -eq $false -and $state.marketDataRequestWriteSucceeded -eq $false -and $state.marketDataRequestResponseReadAttempted -eq $false -and $state.marketDataRequestReachedBoundedResponseClassification -eq $false -and $state.marketDataRequestSentLegacyFlag -eq $false) 'LMAX_R212_FAIL_MARKETDATAREQUEST_CLAIM_UNSUPPORTED' 'State fields should be false for pre-MarketData failure.'
Assert-True ($state.allExplicitStateFieldsFalseConsistentBecausePreFixPreMarketDataFailure -eq $true -and $state.classifiedMarketDataResponseObserved -eq $false) 'LMAX_R212_FAIL_TLS_BOUNDARY_REVIEW_MISSING' 'State false rationale missing.'
Assert-True ($r211State.stateFieldsConsistent -eq $true -and $r211State.classifiedMarketDataResponseObserved -eq $false) 'LMAX_R212_FAIL_TLS_BOUNDARY_REVIEW_MISSING' 'R211 state artifact mismatch.'

Assert-True ($conclusion.noRejectLogoutEntriesConclusionReviewPresent -eq $true) 'LMAX_R212_FAIL_REJECT_LOGOUT_ENTRIES_CLAIM_UNSUPPORTED' 'No-conclusion review missing.'
Assert-True ($conclusion.rejectConclusionClaimed -eq $false -and $conclusion.logoutConclusionClaimed -eq $false -and $conclusion.entriesConclusionClaimed -eq $false) 'LMAX_R212_FAIL_REJECT_LOGOUT_ENTRIES_CLAIM_UNSUPPORTED' 'Unsupported reject/logout/entries conclusion claimed.'
Assert-True ($conclusion.entriesObservedClaimed -eq $false -and $conclusion.sanitizedEntryCountClaimed -eq $false -and $r211Entries.entriesCountClaimed -eq $false) 'LMAX_R212_FAIL_REJECT_LOGOUT_ENTRIES_CLAIM_UNSUPPORTED' 'Unsupported entries count claimed.'

Assert-True ($baseline.gbpusdBaselinePreserved -eq $true -and $baseline.gbpusdMarketDataResponseCategory -eq 'Succeeded' -and $baseline.gbpusdEntriesObserved -eq $true -and $baseline.gbpusdSanitizedEntryCount -eq 2) 'LMAX_R212_FAIL_AUDUSD_MISCLASSIFIED' 'GBPUSD baseline weakened.'
Assert-True ($baseline.eurgbpBaselinePreserved -eq $true -and $baseline.eurgbpMarketDataResponseCategory -eq 'Succeeded' -and $baseline.eurgbpEntriesObserved -eq $true -and $baseline.eurgbpSanitizedEntryCount -eq 2) 'LMAX_R212_FAIL_AUDUSD_MISCLASSIFIED' 'EURGBP baseline weakened.'
Assert-True ($usdjpy.usdjpyNotProven -eq $true -and $usdjpy.usdjpyClassifiedAsFailed -eq $false -and $usdjpy.usdJpyCaveatPreserved -eq $true) 'LMAX_R212_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED' 'USDJPY misclassified or caveat weakened.'
Assert-True ($usdjpy.usdJpySecurityId -eq '4004' -and $usdjpy.usdJpySecurityIdSource -eq '8' -and $usdjpy.approvedUniversePreserved -eq $true) 'LMAX_R212_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED' 'USDJPY SecurityID/Source or universe weakened.'

Assert-True ($decision.nextActionDecisionPresent -eq $true) 'LMAX_R212_FAIL_TLS_BOUNDARY_REVIEW_MISSING' 'Next action decision missing.'
Assert-True ($decision.audusdOnlyRetryReadinessRecommended -eq $true -and $decision.directLiveRetryAllowedFromR212 -eq $false -and $decision.r213MustRemainNoExternal -eq $true) 'LMAX_R212_FAIL_TLS_BOUNDARY_REVIEW_MISSING' 'Next action permits direct live retry or misses no-external gate.'
Assert-True ($next.directLiveRetryAllowedFromR212 -eq $false -and $next.futureLiveRetryRequiresFreshReadinessGate -eq $true -and $next.r213MustRemainNoExternal -eq $true) 'LMAX_R212_FAIL_TLS_BOUNDARY_REVIEW_MISSING' 'Next recommendation allows direct live retry.'

Assert-True ($sanitization.credentialValuesReturned -eq $false) 'LMAX_R212_FAIL_RAW_TLS_OR_ENDPOINT_LEAK_RISK' 'credentialValuesReturned=false missing.'
Assert-True ($sanitization.rawTlsMaterialSerialized -eq $false -and $sanitization.rawEndpointValuesSerialized -eq $false) 'LMAX_R212_FAIL_RAW_TLS_OR_ENDPOINT_LEAK_RISK' 'Raw TLS/endpoint leak risk.'
Assert-True ($sanitization.rawFixMessagesSerialized -eq $false -and $sanitization.rawLogoutTextSerialized -eq $false -and $sanitization.rawRejectTextSerialized -eq $false) 'LMAX_R212_FAIL_RAW_FIX_OR_LOGOUT_REJECT_LEAK_RISK' 'Raw FIX/logout/reject leak risk.'
Assert-True ($sanitization.rawMdReqIdSerialized -eq $false -and $sanitization.rawMarketDataPricesSerialized -eq $false -and $sanitization.rawBidAskValuesSerialized -eq $false -and $sanitization.rawMarketDataPayloadSerialized -eq $false) 'LMAX_R212_FAIL_RAW_FIX_OR_LOGOUT_REJECT_LEAK_RISK' 'Raw MDReqID/market-data leak risk.'
Assert-True ($sanitization.rawCredentialsSerialized -eq $false -and $sanitization.rawSessionIdsSerialized -eq $false -and $sanitization.rawCompIdsSerialized -eq $false) 'LMAX_R212_FAIL_RAW_TLS_OR_ENDPOINT_LEAK_RISK' 'Secret/session/CompID leak risk.'

Assert-True ($forbidden.ordersIntroduced -eq $false -and $forbidden.tradingEnabled -eq $false -and $forbidden.tradingStateMutated -eq $false) 'LMAX_R212_FAIL_FORBIDDEN_ACTION_RISK' 'Order/trading path introduced.'
Assert-True ($forbidden.schedulerStarted -eq $false -and $forbidden.pollingStarted -eq $false -and $forbidden.serviceStarted -eq $false) 'LMAX_R212_FAIL_FORBIDDEN_ACTION_RISK' 'Scheduler/polling/service introduced.'
Assert-True ($forbidden.replayIntroduced -eq $false -and $forbidden.shadowReplayIntroduced -eq $false -and $forbidden.productionAccountUsed -eq $false) 'LMAX_R212_FAIL_FORBIDDEN_ACTION_RISK' 'Replay/shadow replay or production account introduced.'
Assert-True ($apiWorker.fakeLmaxGatewayOnlyPreserved -eq $true -and $apiWorker.liveGatewayIntroducedInApiOrWorker -eq $false -and $apiWorker.apiWorkerStarted -eq $false) 'LMAX_R212_FAIL_API_WORKER_GATEWAY_REGRESSION' 'API/Worker FakeLmaxGatewayOnly regression.'

Assert-True ($validation.buildStatus -eq 'PASS' -and $validation.validator -eq 'LMAX_R212_VALIDATION_PASS' -and $validation.validatorStatus -eq 'PASS') 'LMAX_R212_FAIL_BUILD_OR_VALIDATOR' 'Build/validator evidence missing.'

Write-Output 'LMAX_R212_VALIDATION_PASS'
