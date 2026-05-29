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

$summary = Read-Json "$artifactRoot/phase-lmax-r201-entries-reporting-enrichment.json" 'LMAX_R201_FAIL_ENTRIES_FIELD_CONTRACT_MISSING'
$rehydration = Read-Json "$artifactRoot/phase-lmax-r201-r199-entries-evidence-rehydration-review.json" 'LMAX_R201_FAIL_ENTRIES_REHYDRATION_REVIEW_MISSING'
$fieldContract = Read-Json "$artifactRoot/phase-lmax-r201-entries-evidence-field-contract.json" 'LMAX_R201_FAIL_ENTRIES_FIELD_CONTRACT_MISSING'
$cliContract = Read-Json "$artifactRoot/phase-lmax-r201-cli-artifact-entries-reporting-contract.json" 'LMAX_R201_FAIL_ENTRIES_FIELD_CONTRACT_MISSING'
$parserReview = Read-Json "$artifactRoot/phase-lmax-r201-parser-result-model-review.json" 'LMAX_R201_FAIL_ENTRIES_FIELD_CONTRACT_MISSING'
$tests = Read-Json "$artifactRoot/phase-lmax-r201-test-evidence.json" 'LMAX_R201_FAIL_BUILD_OR_TESTS'
$success = Read-Json "$artifactRoot/phase-lmax-r201-r199-success-preservation.json" 'LMAX_R201_FAIL_R199_SUCCESS_EVIDENCE_MISSING'
$sanitization = Read-Json "$artifactRoot/phase-lmax-r201-sanitization-audit.json" 'LMAX_R201_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK'
$forbidden = Read-Json "$artifactRoot/phase-lmax-r201-forbidden-actions-audit.json" 'LMAX_R201_FAIL_FORBIDDEN_ACTION_RISK'
$apiWorker = Read-Json "$artifactRoot/phase-lmax-r201-api-worker-fake-gateway-audit.json" 'LMAX_R201_FAIL_API_WORKER_GATEWAY_REGRESSION'
$decision = Read-Json "$artifactRoot/phase-lmax-r201-next-action-decision-gate.json" 'LMAX_R201_FAIL_ENTRIES_FIELD_CONTRACT_MISSING'
$next = Read-Json "$artifactRoot/phase-lmax-r201-next-phase-recommendation.json" 'LMAX_R201_FAIL_ENTRIES_FIELD_CONTRACT_MISSING'

Assert-True ($summary.noExternal -eq $true) 'LMAX_R201_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R201 must remain no-external.'
Assert-True ($summary.newExternalActivationAttempted -eq $false) 'LMAX_R201_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'New external activation detected.'
Assert-True ($summary.socketTlsFixMarketDataRuntimeActionDetected -eq $false) 'LMAX_R201_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'Runtime boundary action detected.'
Assert-True ($forbidden.socketOpened -eq $false -and $forbidden.tlsOpened -eq $false -and $forbidden.fixOpened -eq $false) 'LMAX_R201_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'Socket/TLS/FIX action detected.'
Assert-True ($forbidden.liveMarketDataRequestSent -eq $false -and $forbidden.liveMarketDataResponseRead -eq $false) 'LMAX_R201_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'Live market-data action detected.'

Assert-True (Test-Path -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r199-mdreqid-repaired-activation.json")) 'LMAX_R201_FAIL_R199_SUCCESS_EVIDENCE_MISSING' 'R199 success evidence missing.'
Assert-True (Test-Path -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r200-entries-evidence-review.json")) 'LMAX_R201_FAIL_ENTRIES_REHYDRATION_REVIEW_MISSING' 'R200 entries reporting gap decision missing.'
Assert-True ($success.r199SuccessPreserved -eq $true -and $success.marketDataResponseCategory -eq 'Succeeded') 'LMAX_R201_FAIL_R199_SUCCESS_EVIDENCE_MISSING' 'R199 success semantics missing.'
Assert-True ($success.rejectReasonExtractionSource -eq 'NoRejectObserved') 'LMAX_R201_FAIL_R199_SUCCESS_EVIDENCE_MISSING' 'R199 reject absence not preserved.'

Assert-True ($rehydration.r199EvidenceReviewed -eq $true) 'LMAX_R201_FAIL_ENTRIES_REHYDRATION_REVIEW_MISSING' 'Entries rehydration review missing.'
Assert-True ($rehydration.entriesObservedClaimed -eq $false -and $rehydration.sanitizedEntryCountClaimed -eq $false) 'LMAX_R201_FAIL_UNSUPPORTED_ENTRIES_CLAIM' 'Entries/count claimed without safe R199 evidence.'
Assert-True ($rehydration.rehydrationDecision -eq 'EntriesEvidenceNotRehydratable') 'LMAX_R201_FAIL_ENTRIES_REHYDRATION_REVIEW_MISSING' 'R199 rehydration decision missing.'

$requiredFields = @(
    'marketDataEntriesObserved',
    'marketDataSanitizedEntryCount',
    'marketDataEntriesEvidenceCategory',
    'marketDataEntriesReportingSource',
    'marketDataEntriesNotAvailableReason'
)
foreach ($field in $requiredFields) {
    Assert-True ($fieldContract.fields -contains $field) 'LMAX_R201_FAIL_ENTRIES_FIELD_CONTRACT_MISSING' "Missing field contract $field."
    Assert-True ($cliContract.cliNowEmits -contains $field) 'LMAX_R201_FAIL_ENTRIES_FIELD_CONTRACT_MISSING' "CLI contract missing $field."
}

foreach ($category in @('EntriesObservedWithSanitizedCount', 'NoEntriesObserved', 'EntriesEvidenceNotRehydratable', 'EntriesEvidenceInconclusiveSafe')) {
    Assert-True ($fieldContract.categories -contains $category) 'LMAX_R201_FAIL_ENTRIES_FIELD_CONTRACT_MISSING' "Missing entries category $category."
}

Assert-True ($parserReview.parserReviewed -eq $true -and $parserReview.resultModelEnriched -eq $true) 'LMAX_R201_FAIL_ENTRIES_FIELD_CONTRACT_MISSING' 'Parser/result model review missing.'
Assert-True ($parserReview.futureParserResultReportingReady -eq $true) 'LMAX_R201_FAIL_ENTRIES_FIELD_CONTRACT_MISSING' 'Future parser/result reporting not ready.'

if ($summary.codeChanged -eq $true) {
    Assert-True ($tests.focusedTests.status -eq 'PASS') 'LMAX_R201_FAIL_BUILD_OR_TESTS' 'Focused tests missing or failed.'
    Assert-True ($tests.unitTests.status -eq 'PASS') 'LMAX_R201_FAIL_BUILD_OR_TESTS' 'Unit tests missing or failed.'
}
Assert-True ($tests.build.status -eq 'PASS') 'LMAX_R201_FAIL_BUILD_OR_TESTS' 'Build evidence missing.'
Assert-True ($tests.integrationTests.status -eq 'PASS') 'LMAX_R201_FAIL_BUILD_OR_TESTS' 'Integration tests missing or failed.'

Assert-True ($sanitization.credentialValuesReturned -eq $false) 'LMAX_R201_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' 'credentialValuesReturned=false missing.'
Assert-True ($sanitization.rawMarketDataPricesSerialized -eq $false -and $sanitization.rawBidAskValuesSerialized -eq $false) 'LMAX_R201_FAIL_RAW_MARKETDATA_OR_FIX_LEAK_RISK' 'Raw market-data price leak risk.'
Assert-True ($sanitization.rawFixMessagesSerialized -eq $false) 'LMAX_R201_FAIL_RAW_MARKETDATA_OR_FIX_LEAK_RISK' 'Raw FIX serialization risk.'
Assert-True ($sanitization.rawRejectTextSerialized -eq $false -and $sanitization.rawMdReqIdSerialized -eq $false) 'LMAX_R201_FAIL_RAW_MDREQID_OR_REJECT_LEAK_RISK' 'Raw reject or MDReqID serialization risk.'
Assert-True ($sanitization.rawCredentialsSerialized -eq $false -and $sanitization.rawSessionIdsSerialized -eq $false -and $sanitization.rawCompIdsSerialized -eq $false) 'LMAX_R201_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' 'Secret/session serialization risk.'
Assert-True ($sanitization.rawEndpointValuesSerialized -eq $false -and $sanitization.rawTlsValuesSerialized -eq $false) 'LMAX_R201_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' 'Endpoint/TLS serialization risk.'

Assert-True ($summary.approvedUniverse -contains 'GBPUSD' -and $summary.approvedUniverse -contains 'EURGBP' -and $summary.approvedUniverse -contains 'AUDUSD' -and $summary.approvedUniverse -contains 'USDJPY') 'LMAX_R201_FAIL_USDJPY_CAVEAT_WEAKENED' 'Approved universe weakened.'
Assert-True ($summary.usdJpyCaveatPreserved -eq $true -and $summary.usdJpySecurityId -eq '4004' -and $summary.usdJpySecurityIdSource -eq '8') 'LMAX_R201_FAIL_USDJPY_CAVEAT_WEAKENED' 'USDJPY caveat weakened.'

Assert-True ($forbidden.ordersIntroduced -eq $false -and $forbidden.tradingEnabled -eq $false -and $forbidden.tradingStateMutated -eq $false) 'LMAX_R201_FAIL_FORBIDDEN_ACTION_RISK' 'Order/trading path introduced.'
Assert-True ($forbidden.schedulerStarted -eq $false -and $forbidden.pollingStarted -eq $false -and $forbidden.serviceStarted -eq $false) 'LMAX_R201_FAIL_FORBIDDEN_ACTION_RISK' 'Scheduler/polling/service introduced.'
Assert-True ($forbidden.replayIntroduced -eq $false -and $forbidden.shadowReplayIntroduced -eq $false) 'LMAX_R201_FAIL_FORBIDDEN_ACTION_RISK' 'Replay/shadow replay introduced.'
Assert-True ($apiWorker.fakeLmaxGatewayOnlyPreserved -eq $true -and $apiWorker.apiWorkerLiveGatewayRegression -eq $false -and $apiWorker.apiWorkerStarted -eq $false) 'LMAX_R201_FAIL_API_WORKER_GATEWAY_REGRESSION' 'API/Worker FakeLmaxGatewayOnly regression.'

Assert-True ($decision.nextAllowsBlindLiveRetry -eq $false) 'LMAX_R201_FAIL_FORBIDDEN_ACTION_RISK' 'Next action allows blind live retry.'
Assert-True ($decision.requiresNoExternalReadinessGateBeforeAnyFutureActivation -eq $true) 'LMAX_R201_FAIL_FORBIDDEN_ACTION_RISK' 'No-external readiness gate missing.'
Assert-True ($next.r202MustRemainNoExternal -eq $true) 'LMAX_R201_FAIL_FORBIDDEN_ACTION_RISK' 'R202 no-external constraint missing.'

Write-Output 'LMAX_R201_VALIDATION_PASS'
