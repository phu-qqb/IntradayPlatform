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

$summary = Join-Path $Root "$artifactRoot/phase-lmax-r209-logout-reason-extraction-enrichment-summary.md"
Assert-True (Test-Path -LiteralPath $summary) 'LMAX_R209_FAIL_LOGOUT_DETAIL_CONTRACT_MISSING' 'R209 summary missing.'

$review = Read-Json "$artifactRoot/phase-lmax-r209-logout-reason-extraction-enrichment.json" 'LMAX_R209_FAIL_LOGOUT_DETAIL_CONTRACT_MISSING'
$audusd = Read-Json "$artifactRoot/phase-lmax-r209-audusd-logout-evidence-review.json" 'LMAX_R209_FAIL_AUDUSD_LOGOUT_EVIDENCE_MISSING'
$fields = Read-Json "$artifactRoot/phase-lmax-r209-logout-detail-field-contract.json" 'LMAX_R209_FAIL_LOGOUT_DETAIL_CONTRACT_MISSING'
$categories = Read-Json "$artifactRoot/phase-lmax-r209-logout-sanitized-category-contract.json" 'LMAX_R209_FAIL_LOGOUT_SANITIZED_CATEGORY_CONTRACT_MISSING'
$tests = Read-Json "$artifactRoot/phase-lmax-r209-fixture-test-evidence.json" 'LMAX_R209_FAIL_BUILD_OR_TESTS'
$eurgbp = Read-Json "$artifactRoot/phase-lmax-r209-eurgbp-success-preservation.json" 'LMAX_R209_FAIL_AUDUSD_LOGOUT_EVIDENCE_MISSING'
$usdjpy = Read-Json "$artifactRoot/phase-lmax-r209-usdjpy-not-proven-and-caveat-preservation.json" 'LMAX_R209_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED'
$decision = Read-Json "$artifactRoot/phase-lmax-r209-next-action-decision-gate.json" 'LMAX_R209_FAIL_LOGOUT_DETAIL_CONTRACT_MISSING'
$sanitization = Read-Json "$artifactRoot/phase-lmax-r209-sanitization-audit.json" 'LMAX_R209_FAIL_RAW_LOGOUT_OR_FIX_LEAK_RISK'
$forbidden = Read-Json "$artifactRoot/phase-lmax-r209-forbidden-actions-audit.json" 'LMAX_R209_FAIL_FORBIDDEN_ACTION_RISK'
$apiWorker = Read-Json "$artifactRoot/phase-lmax-r209-api-worker-fake-gateway-audit.json" 'LMAX_R209_FAIL_API_WORKER_GATEWAY_REGRESSION'
$next = Read-Json "$artifactRoot/phase-lmax-r209-next-phase-recommendation.json" 'LMAX_R209_FAIL_LOGOUT_DETAIL_CONTRACT_MISSING'
$validation = Read-Json "$artifactRoot/phase-lmax-r209-gate-validation.json" 'LMAX_R209_FAIL_BUILD_OR_TESTS'

$r207 = Read-Json "$artifactRoot/phase-lmax-r207-activation.json" 'LMAX_R209_FAIL_AUDUSD_LOGOUT_EVIDENCE_MISSING'
$r208 = Read-Json "$artifactRoot/phase-lmax-r208-partial-remaining-instruments-review.json" 'LMAX_R209_FAIL_AUDUSD_LOGOUT_EVIDENCE_MISSING'

Assert-True ($review.noExternal -eq $true -and $review.newExternalActivationAttempted -eq $false -and $review.socketTlsFixMarketDataRuntimeActionDetected -eq $false) 'LMAX_R209_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R209 must remain no-external.'
Assert-True ($forbidden.newExternalActivationAttempted -eq $false -and $forbidden.socketOpened -eq $false -and $forbidden.tlsOpened -eq $false -and $forbidden.fixOpened -eq $false) 'LMAX_R209_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R209 opened socket/TLS/FIX.'
Assert-True ($forbidden.liveMarketDataRequestSent -eq $false -and $forbidden.liveMarketDataResponseRead -eq $false) 'LMAX_R209_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R209 performed live MarketData action.'

Assert-True ($r207.classification -eq 'LMAX_R207_PASS_PARTIAL_REMAINING_INSTRUMENTS_SEQUENTIAL_ENTRIES_RUNTIME_ACTIVATION_SANITIZED') 'LMAX_R209_FAIL_AUDUSD_LOGOUT_EVIDENCE_MISSING' 'R207 evidence missing.'
Assert-True ($r208.classification -eq 'LMAX_R208_PASS_PARTIAL_REMAINING_INSTRUMENTS_REVIEW_LOGOUT_REASON_ENRICHMENT_RECOMMENDED_NO_EXTERNAL') 'LMAX_R209_FAIL_AUDUSD_LOGOUT_EVIDENCE_MISSING' 'R208 evidence missing.'

Assert-True ($audusd.audusdLogoutEvidenceReviewPresent -eq $true -and $audusd.selectedInstrument -eq 'AUDUSD') 'LMAX_R209_FAIL_AUDUSD_LOGOUT_EVIDENCE_MISSING' 'AUDUSD logout evidence review missing.'
Assert-True ($audusd.securityId -eq '4007' -and $audusd.securityIdSource -eq '8' -and $audusd.r207MarketDataResponseCategory -eq 'LogoutObserved') 'LMAX_R209_FAIL_AUDUSD_LOGOUT_EVIDENCE_MISSING' 'AUDUSD LogoutObserved evidence missing.'
Assert-True ($audusd.r207RawLogoutTextStored -eq $false -and $audusd.r207LogoutReasonRehydratable -eq $false) 'LMAX_R209_FAIL_RAW_LOGOUT_OR_FIX_LEAK_RISK' 'R207 logout raw text should not be stored/rehydrated.'

Assert-True ($fields.logoutDetailFieldContractReady -eq $true -and $fields.propagatedThroughRuntimeCliSurface -eq $true) 'LMAX_R209_FAIL_LOGOUT_DETAIL_CONTRACT_MISSING' 'Logout detail contract missing.'
foreach ($field in @('logoutObserved', 'logoutSourceCategory', 'logoutReasonSanitizedCategory', 'logoutTextPresentSanitized', 'logoutAfterInstrument', 'logoutAfterSecurityIdSanitized', 'logoutTimingCategory', 'logoutReasonExtractionSource')) {
    Assert-True ($fields.fields -contains $field) 'LMAX_R209_FAIL_LOGOUT_DETAIL_CONTRACT_MISSING' "Logout field missing: $field"
}
Assert-True ($fields.rawLogoutTextAllowed -eq $false -and $fields.rawFixAllowed -eq $false -and $fields.rawRejectTextAllowed -eq $false -and $fields.notAvailableDistinctFromPropagationFailure -eq $true) 'LMAX_R209_FAIL_RAW_LOGOUT_OR_FIX_LEAK_RISK' 'Logout detail contract allows raw values or collapses NotAvailable.'

Assert-True ($categories.logoutSanitizedCategoryContractReady -eq $true -and $categories.fixLogoutMsgType5Classified -eq $true) 'LMAX_R209_FAIL_LOGOUT_SANITIZED_CATEGORY_CONTRACT_MISSING' 'Logout category contract missing.'
foreach ($category in @('LogoutReasonNotAvailable', 'LogoutTextPresentSanitized', 'LogoutAfterMarketDataRequest', 'LogoutAfterAUDUSDRequest', 'LogoutTransportCloseObserved', 'LogoutSessionCloseObserved', 'LogoutPermissionOrEntitlementPlausible', 'LogoutInstrumentOrMappingPlausible', 'LogoutSequenceOrMultiRequestPlausible', 'LogoutOtherSanitized', 'LogoutEvidenceInconclusiveSafe')) {
    Assert-True ($categories.allowedCategories -contains $category) 'LMAX_R209_FAIL_LOGOUT_SANITIZED_CATEGORY_CONTRACT_MISSING' "Logout category missing: $category"
}
Assert-True ($categories.rawLogoutTextSerialized -eq $false) 'LMAX_R209_FAIL_RAW_LOGOUT_OR_FIX_LEAK_RISK' 'Raw logout text serialization risk.'

Assert-True ($tests.fixtureTestsPresent -eq $true -and $tests.focusedTests -eq 'PASS 70/70' -and $tests.unitTests -eq 'PASS 1839/1839') 'LMAX_R209_FAIL_BUILD_OR_TESTS' 'Fixture/unit test evidence missing.'
Assert-True ($tests.rawLogoutTextSerialized -eq $false -and $tests.rawFixSerialized -eq $false) 'LMAX_R209_FAIL_RAW_LOGOUT_OR_FIX_LEAK_RISK' 'Fixture evidence raw logout/FIX risk.'

Assert-True ($eurgbp.eurgbpSuccessPreserved -eq $true -and $eurgbp.marketDataResponseCategory -eq 'Succeeded' -and $eurgbp.entriesObserved -eq $true -and $eurgbp.sanitizedEntryCount -eq 2) 'LMAX_R209_FAIL_AUDUSD_LOGOUT_EVIDENCE_MISSING' 'EURGBP success weakened.'
Assert-True ($usdjpy.usdjpyNotProven -eq $true -and $usdjpy.usdjpyClassifiedAsFailed -eq $false -and $usdjpy.usdJpyCaveatPreserved -eq $true) 'LMAX_R209_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED' 'USDJPY misclassified or caveat weakened.'
Assert-True ($usdjpy.usdJpySecurityId -eq '4004' -and $usdjpy.usdJpySecurityIdSource -eq '8') 'LMAX_R209_FAIL_USDJPY_MISCLASSIFIED_OR_CAVEAT_WEAKENED' 'USDJPY SecurityID caveat weakened.'

Assert-True ($decision.nextActionDecisionPresent -eq $true -and $decision.audusdOnlyRetryReadinessRecommended -eq $true -and $decision.multiInstrumentLiveRetryAllowedBeforeAudusdIsolationOrReview -eq $false -and $decision.r210MustRemainNoExternal -eq $true) 'LMAX_R209_FAIL_LOGOUT_DETAIL_CONTRACT_MISSING' 'Next action decision missing or unsafe.'
Assert-True ($next.r210MustRemainNoExternal -eq $true -and $next.multiInstrumentLiveRetryAllowedBeforeAudusdIsolationOrReview -eq $false) 'LMAX_R209_FAIL_LOGOUT_DETAIL_CONTRACT_MISSING' 'Next recommendation allows multi-instrument retry.'

Assert-True ($sanitization.credentialValuesReturned -eq $false) 'LMAX_R209_FAIL_RAW_LOGOUT_OR_FIX_LEAK_RISK' 'credentialValuesReturned=false missing.'
Assert-True ($sanitization.rawLogoutTextSerialized -eq $false -and $sanitization.rawFixMessagesSerialized -eq $false -and $sanitization.rawRejectTextSerialized -eq $false) 'LMAX_R209_FAIL_RAW_LOGOUT_OR_FIX_LEAK_RISK' 'Raw logout/FIX/reject leak risk.'
Assert-True ($sanitization.rawMdReqIdSerialized -eq $false -and $sanitization.rawMarketDataPricesSerialized -eq $false -and $sanitization.rawBidAskValuesSerialized -eq $false -and $sanitization.rawMarketDataPayloadSerialized -eq $false) 'LMAX_R209_FAIL_RAW_MARKETDATA_OR_MDREQID_LEAK_RISK' 'Raw MDReqID/market-data leak risk.'
Assert-True ($sanitization.rawCredentialsSerialized -eq $false -and $sanitization.rawSessionIdsSerialized -eq $false -and $sanitization.rawCompIdsSerialized -eq $false -and $sanitization.rawEndpointValuesSerialized -eq $false -and $sanitization.rawTlsMaterialSerialized -eq $false) 'LMAX_R209_FAIL_RAW_LOGOUT_OR_FIX_LEAK_RISK' 'Secret/session/endpoint/TLS leak risk.'

Assert-True ($forbidden.ordersIntroduced -eq $false -and $forbidden.tradingEnabled -eq $false -and $forbidden.tradingStateMutated -eq $false) 'LMAX_R209_FAIL_FORBIDDEN_ACTION_RISK' 'Order/trading path introduced.'
Assert-True ($forbidden.schedulerStarted -eq $false -and $forbidden.pollingStarted -eq $false -and $forbidden.serviceStarted -eq $false) 'LMAX_R209_FAIL_FORBIDDEN_ACTION_RISK' 'Scheduler/polling/service introduced.'
Assert-True ($forbidden.replayIntroduced -eq $false -and $forbidden.shadowReplayIntroduced -eq $false) 'LMAX_R209_FAIL_FORBIDDEN_ACTION_RISK' 'Replay/shadow replay introduced.'
Assert-True ($apiWorker.fakeLmaxGatewayOnlyPreserved -eq $true -and $apiWorker.liveGatewayIntroducedInApiOrWorker -eq $false -and $apiWorker.apiWorkerStarted -eq $false) 'LMAX_R209_FAIL_API_WORKER_GATEWAY_REGRESSION' 'API/Worker FakeLmaxGatewayOnly regression.'

Assert-True ($validation.buildStatus -eq 'PASS' -and $validation.validator -eq 'LMAX_R209_VALIDATION_PASS' -and $validation.validatorStatus -eq 'PASS') 'LMAX_R209_FAIL_BUILD_OR_TESTS' 'Build/validator evidence missing.'

Write-Output 'LMAX_R209_VALIDATION_PASS'
