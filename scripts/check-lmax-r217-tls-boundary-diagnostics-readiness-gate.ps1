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

Assert-True (Test-Path -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r217-tls-boundary-diagnostics-summary.md")) 'LMAX_R217_FAIL_TLS_DIAGNOSTICS_EVIDENCE_MISSING' 'R217 summary missing.'

$diagnostics = Read-Json "$artifactRoot/phase-lmax-r217-tls-boundary-diagnostics.json" 'LMAX_R217_FAIL_TLS_DIAGNOSTICS_EVIDENCE_MISSING'
$comparison = Read-Json "$artifactRoot/phase-lmax-r217-successful-vs-audusd-path-comparison.json" 'LMAX_R217_FAIL_TLS_DIAGNOSTICS_EVIDENCE_MISSING'
$provider = Read-Json "$artifactRoot/phase-lmax-r217-provider-composition-review.json" 'LMAX_R217_FAIL_PROVIDER_COMPOSITION_REVIEW_MISSING'
$reservation = Read-Json "$artifactRoot/phase-lmax-r217-reservation-adapter-path-review.json" 'LMAX_R217_FAIL_TLS_DIAGNOSTICS_EVIDENCE_MISSING'
$preflight = Read-Json "$artifactRoot/phase-lmax-r217-preflight-approval-packaging-review.json" 'LMAX_R217_FAIL_TLS_DIAGNOSTICS_EVIDENCE_MISSING'
$audusd = Read-Json "$artifactRoot/phase-lmax-r217-audusd-not-marketdata-tested-preservation.json" 'LMAX_R217_FAIL_AUDUSD_MISCLASSIFIED'
$baseline = Read-Json "$artifactRoot/phase-lmax-r217-gbpusd-eurgbp-baseline-preservation.json" 'LMAX_R217_FAIL_TLS_DIAGNOSTICS_EVIDENCE_MISSING'
$usdJpy = Read-Json "$artifactRoot/phase-lmax-r217-usdjpy-not-proven-and-caveat-preservation.json" 'LMAX_R217_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED'
$next = Read-Json "$artifactRoot/phase-lmax-r217-next-action-decision-gate.json" 'LMAX_R217_FAIL_TLS_DIAGNOSTICS_EVIDENCE_MISSING'
$sanitization = Read-Json "$artifactRoot/phase-lmax-r217-sanitization-audit.json" 'LMAX_R217_FAIL_RAW_TLS_OR_ENDPOINT_LEAK_RISK'
$forbidden = Read-Json "$artifactRoot/phase-lmax-r217-forbidden-actions-audit.json" 'LMAX_R217_FAIL_FORBIDDEN_ACTION_RISK'
$apiWorker = Read-Json "$artifactRoot/phase-lmax-r217-api-worker-fake-gateway-audit.json" 'LMAX_R217_FAIL_API_WORKER_GATEWAY_REGRESSION'
$recommendation = Read-Json "$artifactRoot/phase-lmax-r217-next-phase-recommendation.json" 'LMAX_R217_FAIL_TLS_DIAGNOSTICS_EVIDENCE_MISSING'
$gate = Read-Json "$artifactRoot/phase-lmax-r217-gate-validation.json" 'LMAX_R217_FAIL_BUILD_OR_VALIDATOR'

Assert-True ($diagnostics.classification -in @(
    'LMAX_R217_PASS_TLS_DIAGNOSTICS_AUDUSD_RETRY_READINESS_RECOMMENDED_NO_EXTERNAL',
    'LMAX_R217_PASS_TLS_DIAGNOSTICS_LOCAL_CONFIG_REPAIR_RECOMMENDED_NO_EXTERNAL',
    'LMAX_R217_PASS_TLS_DIAGNOSTICS_INCONCLUSIVE_SAFE_NO_EXTERNAL')) 'LMAX_R217_FAIL_TLS_DIAGNOSTICS_EVIDENCE_MISSING' 'R217 classification is not allowed.'

Assert-True ($diagnostics.noExternalDiagnosticsOnly -eq $true -and $diagnostics.externalActivationAttempted -eq $false -and $diagnostics.socketTlsFixMarketDataRuntimeActionAttempted -eq $false) 'LMAX_R217_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R217 must be no-external.'
Assert-True ($diagnostics.r211EvidencePresent -eq $true -and $diagnostics.r215EvidencePresent -eq $true -and $diagnostics.r216EvidencePresent -eq $true) 'LMAX_R217_FAIL_TLS_DIAGNOSTICS_EVIDENCE_MISSING' 'R211/R215/R216 evidence missing.'

Assert-True ($comparison.successfulBaselinesReviewed -eq $true) 'LMAX_R217_FAIL_TLS_DIAGNOSTICS_EVIDENCE_MISSING' 'Successful GBPUSD/EURGBP baseline comparison missing.'
Assert-True ($comparison.gbpusdR203.tlsSucceeded -eq $true -and $comparison.gbpusdR203.fixSucceeded -eq $true -and $comparison.gbpusdR203.entriesObserved -eq $true) 'LMAX_R217_FAIL_TLS_DIAGNOSTICS_EVIDENCE_MISSING' 'GBPUSD successful baseline missing.'
Assert-True ($comparison.eurgbpR207.tlsSucceeded -eq $true -and $comparison.eurgbpR207.fixSucceeded -eq $true -and $comparison.eurgbpR207.entriesObserved -eq $true) 'LMAX_R217_FAIL_TLS_DIAGNOSTICS_EVIDENCE_MISSING' 'EURGBP successful baseline missing.'
Assert-True ($comparison.audusdR211.tcpReached -eq $true -and $comparison.audusdR211.tlsSucceeded -eq $false -and $comparison.audusdR211.fixAttempted -eq $false -and $comparison.audusdR211.marketDataRequestSent -eq $false) 'LMAX_R217_FAIL_AUDUSD_MISCLASSIFIED' 'R211 AUDUSD evidence misrepresented.'
Assert-True ($comparison.audusdR215.tcpReached -eq $true -and $comparison.audusdR215.tlsSucceeded -eq $false -and $comparison.audusdR215.fixAttempted -eq $false -and $comparison.audusdR215.marketDataRequestSent -eq $false) 'LMAX_R217_FAIL_AUDUSD_MISCLASSIFIED' 'R215 AUDUSD evidence misrepresented.'

Assert-True ($provider.reviewPerformed -eq $true) 'LMAX_R217_FAIL_PROVIDER_COMPOSITION_REVIEW_MISSING' 'Provider composition review missing.'
Assert-True ($provider.sameExecutableActivationTool -eq $true -and $provider.sameAdapterMode -eq $true) 'LMAX_R217_FAIL_PROVIDER_COMPOSITION_REVIEW_MISSING' 'Executable/adapter comparison missing.'
Assert-True ($provider.sameTcpProviderComposition -eq $true -and $provider.sameTlsProviderComposition -eq $true -and $provider.sameCredentialConfigSource -eq $true -and $provider.sameEndpointBindingCategory -eq $true) 'LMAX_R217_FAIL_PROVIDER_COMPOSITION_REVIEW_MISSING' 'Provider composition comparison missing.'
Assert-True ($provider.sameApiWorkerFakeGatewayOnlyProtection -eq $true -and $provider.sameNoSchedulerPollingReplayPath -eq $true) 'LMAX_R217_FAIL_API_WORKER_GATEWAY_REGRESSION' 'Safety path comparison missing.'
Assert-True ($provider.audusdSelectionAppliedBeforeTcpTlsFix -eq $false -and $provider.providerCompositionDivergenceFound -eq $false) 'LMAX_R217_FAIL_PROVIDER_COMPOSITION_REVIEW_MISSING' 'AUDUSD selection incorrectly affects TCP/TLS/FIX.'

Assert-True ($reservation.reviewPerformed -eq $true -and $reservation.r203Reserved -eq $true -and $reservation.r207Reserved -eq $true -and $reservation.r211Reserved -eq $true -and $reservation.r215Reserved -eq $true) 'LMAX_R217_FAIL_TLS_DIAGNOSTICS_EVIDENCE_MISSING' 'Reservation review missing.'
Assert-True ($reservation.reservationAffectsProviderComposition -eq $false -and $reservation.adapterPathDivergenceFound -eq $false -and $reservation.narrowPhaseSelectionCanAffectTcpTlsBeforeFix -eq $false) 'LMAX_R217_FAIL_TLS_DIAGNOSTICS_EVIDENCE_MISSING' 'Reservation/adaptor path divergence found or unreviewed.'
Assert-True ($preflight.reviewPerformed -eq $true -and $preflight.packagingDifferenceRelevantToTlsFound -eq $false) 'LMAX_R217_FAIL_TLS_DIAGNOSTICS_EVIDENCE_MISSING' 'Preflight approval packaging review missing.'

Assert-True ($audusd.audusdMarketDataTested -eq $false -and $audusd.audusdClassifiedAsFailed -eq $false) 'LMAX_R217_FAIL_AUDUSD_MISCLASSIFIED' 'AUDUSD misclassified as tested or failed.'
Assert-True ($audusd.r211MarketDataRequestSent -eq $false -and $audusd.r215MarketDataRequestSent -eq $false) 'LMAX_R217_FAIL_AUDUSD_MISCLASSIFIED' 'Unsupported MarketDataRequest sent claim.'
Assert-True ($audusd.entriesConclusionClaimed -eq $false -and $audusd.rejectConclusionClaimed -eq $false -and $audusd.logoutConclusionClaimedFromTlsOnlyEvidence -eq $false) 'LMAX_R217_FAIL_AUDUSD_MISCLASSIFIED' 'Unsupported entries/reject/logout conclusion.'

Assert-True ($baseline.gbpusdR203SuccessPreserved -eq $true -and $baseline.eurgbpR207SuccessPreserved -eq $true -and $baseline.baselineWeakened -eq $false) 'LMAX_R217_FAIL_TLS_DIAGNOSTICS_EVIDENCE_MISSING' 'GBPUSD/EURGBP baseline weakened.'
Assert-True ($usdJpy.usdJpyNotProven -eq $true -and $usdJpy.usdJpyClassifiedAsFailed -eq $false -and $usdJpy.usdJpyCaveatPreserved -eq $true) 'LMAX_R217_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED' 'USDJPY misclassified or caveat weakened.'
Assert-True ($usdJpy.usdJpySecurityId -eq '4004' -and $usdJpy.usdJpySecurityIdSource -eq '8') 'LMAX_R217_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED' 'USDJPY caveat values missing.'

Assert-True ($next.requiresNoExternalR219ReadinessGate -eq $true -and $next.directLiveRetryRecommended -eq $false) 'LMAX_R217_FAIL_TLS_DIAGNOSTICS_EVIDENCE_MISSING' 'Next action allows direct live retry.'
Assert-True ($recommendation.r219MustRemainNoExternal -eq $true -and $recommendation.liveRetryBeforeR219Allowed -eq $false) 'LMAX_R217_FAIL_TLS_DIAGNOSTICS_EVIDENCE_MISSING' 'Next phase recommendation allows premature live retry.'

Assert-True ($sanitization.credentialValuesReturned -eq $false) 'LMAX_R217_FAIL_RAW_TLS_OR_ENDPOINT_LEAK_RISK' 'credentialValuesReturned=false missing.'
Assert-True ($sanitization.rawTlsMaterialSerialized -eq $false -and $sanitization.rawEndpointValuesSerialized -eq $false) 'LMAX_R217_FAIL_RAW_TLS_OR_ENDPOINT_LEAK_RISK' 'Raw TLS/endpoint leak risk.'
Assert-True ($sanitization.rawFixMessagesSerialized -eq $false -and $sanitization.rawLogoutTextSerialized -eq $false -and $sanitization.rawRejectTextSerialized -eq $false) 'LMAX_R217_FAIL_RAW_FIX_OR_LOGOUT_REJECT_LEAK_RISK' 'Raw FIX/logout/reject leak risk.'
Assert-True ($sanitization.rawMdReqIdSerialized -eq $false -and $sanitization.rawMarketDataPricesSerialized -eq $false -and $sanitization.rawMarketDataPayloadSerialized -eq $false) 'LMAX_R217_FAIL_RAW_FIX_OR_LOGOUT_REJECT_LEAK_RISK' 'Raw MDReqID/market-data leak risk.'
Assert-True ($sanitization.rawCredentialsSerialized -eq $false -and $sanitization.rawSessionIdsSerialized -eq $false -and $sanitization.rawCompIdsSerialized -eq $false) 'LMAX_R217_FAIL_RAW_TLS_OR_ENDPOINT_LEAK_RISK' 'Raw secret/session/CompID leak risk.'

Assert-True ($forbidden.externalActivationAttempted -eq $false -and $forbidden.socketOpened -eq $false -and $forbidden.tlsAttempted -eq $false -and $forbidden.fixOpened -eq $false) 'LMAX_R217_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R217 external boundary action detected.'
Assert-True ($forbidden.liveMarketDataRequestSent -eq $false -and $forbidden.liveMarketDataResponseRead -eq $false) 'LMAX_R217_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R217 MarketData runtime action detected.'
Assert-True ($forbidden.ordersIntroduced -eq $false -and $forbidden.tradingEnabled -eq $false -and $forbidden.tradingStateMutated -eq $false) 'LMAX_R217_FAIL_FORBIDDEN_ACTION_RISK' 'Order/trading path introduced.'
Assert-True ($forbidden.schedulerStarted -eq $false -and $forbidden.pollingStarted -eq $false -and $forbidden.serviceStarted -eq $false) 'LMAX_R217_FAIL_FORBIDDEN_ACTION_RISK' 'Scheduler/polling/service introduced.'
Assert-True ($forbidden.replayIntroduced -eq $false -and $forbidden.shadowReplayIntroduced -eq $false -and $forbidden.productionAccountUsed -eq $false) 'LMAX_R217_FAIL_FORBIDDEN_ACTION_RISK' 'Replay/shadow replay or production account introduced.'
Assert-True ($apiWorker.fakeLmaxGatewayOnlyPreserved -eq $true -and $apiWorker.liveGatewayIntroducedInApiOrWorker -eq $false -and $apiWorker.apiWorkerStarted -eq $false) 'LMAX_R217_FAIL_API_WORKER_GATEWAY_REGRESSION' 'API/Worker gateway regression.'

Assert-True ($gate.buildStatus -eq 'PASS' -and $gate.validator -eq 'LMAX_R217_VALIDATION_PASS') 'LMAX_R217_FAIL_BUILD_OR_VALIDATOR' 'Build/validator evidence missing.'

$artifactText = Get-ChildItem -LiteralPath (Join-Path $Root $artifactRoot) -Filter 'phase-lmax-r217-*' -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combinedArtifactText = [string]::Join("`n", $artifactText)
foreach ($forbiddenToken in @([string][char]1, 'password=', 'username=', 'SenderCompId', 'TargetCompId', 'bid=', 'ask=', 'fix-marketdata.', '.lmax.com')) {
    Assert-True (-not $combinedArtifactText.Contains($forbiddenToken)) 'LMAX_R217_FAIL_RAW_TLS_OR_ENDPOINT_LEAK_RISK' "Forbidden serialized token detected in R217 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R217_VALIDATION_PASS'
