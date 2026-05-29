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

Assert-True (Test-Path -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r216-audusd-boundary-evidence-review-summary.md")) 'LMAX_R216_FAIL_TLS_BOUNDARY_REVIEW_MISSING' 'R216 summary missing.'

$review = Read-Json "$artifactRoot/phase-lmax-r216-audusd-boundary-evidence-review.json" 'LMAX_R216_FAIL_TLS_BOUNDARY_REVIEW_MISSING'
$r215 = Read-Json "$artifactRoot/phase-lmax-r216-r215-tls-boundary-confirmation.json" 'LMAX_R216_FAIL_R215_EVIDENCE_MISSING'
$comparison = Read-Json "$artifactRoot/phase-lmax-r216-r211-r215-tls-comparison.json" 'LMAX_R216_FAIL_TLS_BOUNDARY_REVIEW_MISSING'
$baseline = Read-Json "$artifactRoot/phase-lmax-r216-successful-tls-fix-baseline-comparison.json" 'LMAX_R216_FAIL_TLS_BOUNDARY_REVIEW_MISSING'
$notTested = Read-Json "$artifactRoot/phase-lmax-r216-audusd-not-marketdata-tested-confirmation.json" 'LMAX_R216_FAIL_AUDUSD_MISCLASSIFIED'
$pathReview = Read-Json "$artifactRoot/phase-lmax-r216-audusd-only-path-vs-tls-config-review.json" 'LMAX_R216_FAIL_AUDUSD_PATH_TLS_CONFIG_REVIEW_MISSING'
$next = Read-Json "$artifactRoot/phase-lmax-r216-next-action-decision-gate.json" 'LMAX_R216_FAIL_NEXT_ACTION_DECISION_MISSING'
$successBaseline = Read-Json "$artifactRoot/phase-lmax-r216-gbpusd-eurgbp-success-baseline-preservation.json" 'LMAX_R216_FAIL_AUDUSD_MISCLASSIFIED'
$usdJpy = Read-Json "$artifactRoot/phase-lmax-r216-usdjpy-not-proven-and-caveat-preservation.json" 'LMAX_R216_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED'
$sanitization = Read-Json "$artifactRoot/phase-lmax-r216-sanitization-audit.json" 'LMAX_R216_FAIL_RAW_TLS_OR_ENDPOINT_LEAK_RISK'
$forbidden = Read-Json "$artifactRoot/phase-lmax-r216-forbidden-actions-audit.json" 'LMAX_R216_FAIL_FORBIDDEN_ACTION_RISK'
$apiWorker = Read-Json "$artifactRoot/phase-lmax-r216-api-worker-fake-gateway-audit.json" 'LMAX_R216_FAIL_API_WORKER_GATEWAY_REGRESSION'
$recommendation = Read-Json "$artifactRoot/phase-lmax-r216-next-phase-recommendation.json" 'LMAX_R216_FAIL_NEXT_ACTION_DECISION_MISSING'
$gate = Read-Json "$artifactRoot/phase-lmax-r216-gate-validation.json" 'LMAX_R216_FAIL_BUILD_OR_VALIDATOR'

Assert-True ($review.classification -in @(
    'LMAX_R216_PASS_AUDUSD_BOUNDARY_REVIEW_TLS_DIAGNOSTICS_RECOMMENDED_NO_EXTERNAL',
    'LMAX_R216_PASS_AUDUSD_BOUNDARY_REVIEW_AUDUSD_RETRY_READINESS_RECOMMENDED_NO_EXTERNAL',
    'LMAX_R216_PASS_AUDUSD_BOUNDARY_REVIEW_INCONCLUSIVE_SAFE_NO_EXTERNAL')) 'LMAX_R216_FAIL_TLS_BOUNDARY_REVIEW_MISSING' 'R216 classification is not allowed.'

Assert-True ($review.noExternalReviewOnly -eq $true -and $review.externalActivationAttempted -eq $false -and $review.socketTlsFixMarketDataRuntimeActionAttempted -eq $false) 'LMAX_R216_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R216 must be no-external.'

Assert-True ($r215.r215EvidencePresent -eq $true -and $r215.attemptCount -eq 1) 'LMAX_R216_FAIL_R215_EVIDENCE_MISSING' 'R215 evidence missing or attemptCount invalid.'
Assert-True ($r215.selectedInstrument -eq 'AUDUSD' -and $r215.securityId -eq '4007' -and $r215.securityIdSource -eq '8') 'LMAX_R216_FAIL_R215_EVIDENCE_MISSING' 'R215 AUDUSD evidence mismatch.'
Assert-True ($r215.tcpConnectionAttempted -eq $true -and $r215.realSocketOpened -eq $true -and $r215.tlsHandshakeAttempted -eq $true) 'LMAX_R216_FAIL_TLS_BOUNDARY_REVIEW_MISSING' 'R215 TCP/TLS boundary confirmation missing.'
Assert-True ($r215.tlsSucceeded -eq $false -and $r215.tlsFailureCategory -eq 'HandshakeException') 'LMAX_R216_FAIL_TLS_BOUNDARY_REVIEW_MISSING' 'R215 TLS failure category missing.'
Assert-True ($r215.fixLogonAttempted -eq $false -and $r215.marketDataRequestSent -eq $false -and $r215.marketDataResponseReadOrClassified -eq $false) 'LMAX_R216_FAIL_AUDUSD_MISCLASSIFIED' 'R215 was incorrectly treated as FIX/MarketData evidence.'

Assert-True ($comparison.samePreFixTlsFailurePattern -eq $true) 'LMAX_R216_FAIL_TLS_BOUNDARY_REVIEW_MISSING' 'R211/R215 TLS comparison missing.'
Assert-True ($comparison.r211.audusdOnly -eq $true -and $comparison.r215.audusdOnly -eq $true) 'LMAX_R216_FAIL_TLS_BOUNDARY_REVIEW_MISSING' 'R211/R215 AUDUSD-only comparison missing.'
Assert-True ($comparison.r211.tlsFailureCategory -eq 'HandshakeException' -and $comparison.r215.tlsFailureCategory -eq 'HandshakeException') 'LMAX_R216_FAIL_TLS_BOUNDARY_REVIEW_MISSING' 'R211/R215 TLS failure category comparison missing.'
Assert-True ($comparison.entriesRejectLogoutConclusionSupported -eq $false) 'LMAX_R216_FAIL_AUDUSD_MISCLASSIFIED' 'Unsupported entries/reject/logout conclusion claimed.'

Assert-True ($baseline.successfulTlsFixBaselinesReviewed -eq $true) 'LMAX_R216_FAIL_TLS_BOUNDARY_REVIEW_MISSING' 'Successful TLS/FIX baseline comparison missing.'
Assert-True ($baseline.r203.tlsSucceeded -eq $true -and $baseline.r203.fixSucceeded -eq $true) 'LMAX_R216_FAIL_TLS_BOUNDARY_REVIEW_MISSING' 'R203 successful TLS/FIX baseline missing.'
Assert-True ($baseline.r207.tlsSucceeded -eq $true -and $baseline.r207.fixSucceeded -eq $true) 'LMAX_R216_FAIL_TLS_BOUNDARY_REVIEW_MISSING' 'R207 successful TLS/FIX baseline missing.'

Assert-True ($notTested.audusdMarketDataTestedByR215 -eq $false -and $notTested.audusdClassifiedAsFailed -eq $false) 'LMAX_R216_FAIL_AUDUSD_MISCLASSIFIED' 'AUDUSD misclassified as tested or failed.'
Assert-True ($notTested.marketDataRequestSentInR215 -eq $false -and $notTested.marketDataResponseReadInR215 -eq $false) 'LMAX_R216_FAIL_AUDUSD_MISCLASSIFIED' 'Unsupported MarketDataRequest/Response claim.'
Assert-True ($notTested.entriesConclusionSupported -eq $false -and $notTested.rejectConclusionSupported -eq $false -and $notTested.logoutConclusionSupported -eq $false) 'LMAX_R216_FAIL_AUDUSD_MISCLASSIFIED' 'Unsupported entries/reject/logout conclusion.'

Assert-True ($pathReview.reviewPerformed -eq $true -and $pathReview.audusdOnlyPathCanAffectTcpOrTlsParametersBeforeFix -eq $false) 'LMAX_R216_FAIL_AUDUSD_PATH_TLS_CONFIG_REVIEW_MISSING' 'AUDUSD path vs TLS config review missing.'
Assert-True ($pathReview.r215ReservationPresent -eq $true -and $pathReview.r215AudusdOnlyRequestGuardPresent -eq $true) 'LMAX_R216_FAIL_AUDUSD_PATH_TLS_CONFIG_REVIEW_MISSING' 'R215 reservation/guard review missing.'
Assert-True ($pathReview.rawEndpointValuesSerialized -eq $false -and $pathReview.rawTlsMaterialSerialized -eq $false) 'LMAX_R216_FAIL_RAW_TLS_OR_ENDPOINT_LEAK_RISK' 'Path review serialized endpoint/TLS raw values.'

Assert-True ($next.directLiveRetryRecommended -eq $false -and $next.requiresNoExternalReadinessGateBeforeAnyRetry -eq $true) 'LMAX_R216_FAIL_NEXT_ACTION_DECISION_MISSING' 'Next action allows direct live retry.'
Assert-True ($recommendation.r217MustRemainNoExternal -eq $true -and $recommendation.liveRetryBeforeR217Allowed -eq $false) 'LMAX_R216_FAIL_NEXT_ACTION_DECISION_MISSING' 'Next phase recommendation allows premature live retry.'

Assert-True ($successBaseline.gbpusdR203SuccessPreserved -eq $true -and $successBaseline.eurgbpR207SuccessPreserved -eq $true) 'LMAX_R216_FAIL_AUDUSD_MISCLASSIFIED' 'GBPUSD/EURGBP baseline weakened.'
Assert-True ($usdJpy.usdJpyNotProven -eq $true -and $usdJpy.usdJpyClassifiedAsFailed -eq $false -and $usdJpy.usdJpyCaveatPreserved -eq $true) 'LMAX_R216_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED' 'USDJPY misclassified or caveat weakened.'
Assert-True ($usdJpy.usdJpySecurityId -eq '4004' -and $usdJpy.usdJpySecurityIdSource -eq '8') 'LMAX_R216_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED' 'USDJPY caveat values missing.'

Assert-True ($sanitization.credentialValuesReturned -eq $false) 'LMAX_R216_FAIL_RAW_TLS_OR_ENDPOINT_LEAK_RISK' 'credentialValuesReturned=false missing.'
Assert-True ($sanitization.rawTlsMaterialSerialized -eq $false -and $sanitization.rawEndpointValuesSerialized -eq $false) 'LMAX_R216_FAIL_RAW_TLS_OR_ENDPOINT_LEAK_RISK' 'Raw TLS/endpoint leak risk.'
Assert-True ($sanitization.rawFixMessagesSerialized -eq $false -and $sanitization.rawLogoutTextSerialized -eq $false -and $sanitization.rawRejectTextSerialized -eq $false) 'LMAX_R216_FAIL_RAW_FIX_OR_LOGOUT_REJECT_LEAK_RISK' 'Raw FIX/logout/reject leak risk.'
Assert-True ($sanitization.rawMdReqIdSerialized -eq $false -and $sanitization.rawMarketDataPricesSerialized -eq $false -and $sanitization.rawMarketDataPayloadSerialized -eq $false) 'LMAX_R216_FAIL_RAW_FIX_OR_LOGOUT_REJECT_LEAK_RISK' 'Raw MDReqID/market-data leak risk.'
Assert-True ($sanitization.rawCredentialsSerialized -eq $false -and $sanitization.rawSessionIdsSerialized -eq $false -and $sanitization.rawCompIdsSerialized -eq $false) 'LMAX_R216_FAIL_RAW_TLS_OR_ENDPOINT_LEAK_RISK' 'Raw secret/session/CompID leak risk.'

Assert-True ($forbidden.externalActivationAttempted -eq $false -and $forbidden.socketOpened -eq $false -and $forbidden.tlsAttempted -eq $false -and $forbidden.fixOpened -eq $false) 'LMAX_R216_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R216 external boundary action detected.'
Assert-True ($forbidden.liveMarketDataRequestSent -eq $false -and $forbidden.liveMarketDataResponseRead -eq $false) 'LMAX_R216_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R216 MarketData runtime action detected.'
Assert-True ($forbidden.ordersIntroduced -eq $false -and $forbidden.tradingEnabled -eq $false -and $forbidden.tradingStateMutated -eq $false) 'LMAX_R216_FAIL_FORBIDDEN_ACTION_RISK' 'Order/trading path introduced.'
Assert-True ($forbidden.schedulerStarted -eq $false -and $forbidden.pollingStarted -eq $false -and $forbidden.serviceStarted -eq $false) 'LMAX_R216_FAIL_FORBIDDEN_ACTION_RISK' 'Scheduler/polling/service introduced.'
Assert-True ($forbidden.replayIntroduced -eq $false -and $forbidden.shadowReplayIntroduced -eq $false -and $forbidden.productionAccountUsed -eq $false) 'LMAX_R216_FAIL_FORBIDDEN_ACTION_RISK' 'Replay/shadow replay or production account introduced.'
Assert-True ($apiWorker.fakeLmaxGatewayOnlyPreserved -eq $true -and $apiWorker.liveGatewayIntroducedInApiOrWorker -eq $false -and $apiWorker.apiWorkerStarted -eq $false) 'LMAX_R216_FAIL_API_WORKER_GATEWAY_REGRESSION' 'API/Worker gateway regression.'

Assert-True ($gate.buildStatus -eq 'PASS' -and $gate.validator -eq 'LMAX_R216_VALIDATION_PASS') 'LMAX_R216_FAIL_BUILD_OR_VALIDATOR' 'Build/validator evidence missing.'

$artifactText = Get-ChildItem -LiteralPath (Join-Path $Root $artifactRoot) -Filter 'phase-lmax-r216-*' -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combinedArtifactText = [string]::Join("`n", $artifactText)
foreach ($forbiddenToken in @([string][char]1, 'password=', 'username=', 'SenderCompId', 'TargetCompId', 'bid=', 'ask=', 'fix-marketdata.', '.lmax.com')) {
    Assert-True (-not $combinedArtifactText.Contains($forbiddenToken)) 'LMAX_R216_FAIL_RAW_TLS_OR_ENDPOINT_LEAK_RISK' "Forbidden serialized token detected in R216 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R216_VALIDATION_PASS'
