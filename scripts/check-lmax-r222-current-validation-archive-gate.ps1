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

Assert-True (Test-Path -LiteralPath (Join-Path $Root "$artifactRoot/phase-lmax-r222-current-validation-archive-summary.md")) 'LMAX_R222_FAIL_GBPUSD_EURGBP_SUCCESS_BASELINE_MISSING' 'R222 summary missing.'

$archive = Read-Json "$artifactRoot/phase-lmax-r222-current-validation-archive.json" 'LMAX_R222_FAIL_GBPUSD_EURGBP_SUCCESS_BASELINE_MISSING'
$gbpusd = Read-Json "$artifactRoot/phase-lmax-r222-gbpusd-success-confirmation.json" 'LMAX_R222_FAIL_GBPUSD_EURGBP_SUCCESS_BASELINE_MISSING'
$eurgbp = Read-Json "$artifactRoot/phase-lmax-r222-eurgbp-success-confirmation.json" 'LMAX_R222_FAIL_GBPUSD_EURGBP_SUCCESS_BASELINE_MISSING'
$audusd = Read-Json "$artifactRoot/phase-lmax-r222-audusd-paused-boundary-inconclusive.json" 'LMAX_R222_FAIL_AUDUSD_MISCLASSIFIED_AS_FAILED'
$usdJpy = Read-Json "$artifactRoot/phase-lmax-r222-usdjpy-not-proven-caveat-preserved.json" 'LMAX_R222_FAIL_USDJPY_CAVEAT_WEAKENED'
$scope = Read-Json "$artifactRoot/phase-lmax-r222-current-scope-decision.json" 'LMAX_R222_FAIL_SCOPE_OVEREXPANSION_RISK'
$future = Read-Json "$artifactRoot/phase-lmax-r222-future-scope-controls.json" 'LMAX_R222_FAIL_SCOPE_OVEREXPANSION_RISK'
$sanitization = Read-Json "$artifactRoot/phase-lmax-r222-sanitization-audit.json" 'LMAX_R222_FAIL_SCOPE_OVEREXPANSION_RISK'
$forbidden = Read-Json "$artifactRoot/phase-lmax-r222-forbidden-actions-audit.json" 'LMAX_R222_FAIL_FORBIDDEN_ACTION_RISK'
$apiWorker = Read-Json "$artifactRoot/phase-lmax-r222-api-worker-fake-gateway-audit.json" 'LMAX_R222_FAIL_API_WORKER_GATEWAY_REGRESSION'
$next = Read-Json "$artifactRoot/phase-lmax-r222-next-phase-recommendation.json" 'LMAX_R222_FAIL_SCOPE_OVEREXPANSION_RISK'
$gate = Read-Json "$artifactRoot/phase-lmax-r222-gate-validation.json" 'LMAX_R222_FAIL_BUILD_OR_VALIDATOR'

Assert-True ($archive.classification -in @(
    'LMAX_R222_PASS_CURRENT_LMAX_READONLY_RUNTIME_VALIDATION_ARCHIVED_NO_EXTERNAL',
    'LMAX_R222_PASS_AUDUSD_PAUSED_CURRENT_VALIDATION_ARCHIVED_NO_EXTERNAL')) 'LMAX_R222_FAIL_GBPUSD_EURGBP_SUCCESS_BASELINE_MISSING' 'R222 classification is not allowed.'
Assert-True ($archive.noExternalArchiveOnly -eq $true -and $archive.externalActivationAttempted -eq $false -and $archive.socketTlsFixMarketDataRuntimeActionAttempted -eq $false) 'LMAX_R222_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R222 must be no-external.'

Assert-True ($gbpusd.gbpusdSuccessConfirmed -eq $true -and $gbpusd.marketDataResponseCategory -eq 'Succeeded') 'LMAX_R222_FAIL_GBPUSD_EURGBP_SUCCESS_BASELINE_MISSING' 'GBPUSD success missing.'
Assert-True ($gbpusd.entriesObserved -eq $true -and $gbpusd.sanitizedEntryCount -eq 2) 'LMAX_R222_FAIL_GBPUSD_EURGBP_SUCCESS_BASELINE_MISSING' 'GBPUSD entries evidence missing.'
Assert-True ($gbpusd.rejectObserved -eq $false -and $gbpusd.shutdownRevertSucceeded -eq $true -and $gbpusd.cleanStateEvidence -eq $true) 'LMAX_R222_FAIL_GBPUSD_EURGBP_SUCCESS_BASELINE_MISSING' 'GBPUSD clean success evidence missing.'

Assert-True ($eurgbp.eurgbpSuccessConfirmed -eq $true -and $eurgbp.marketDataResponseCategory -eq 'Succeeded') 'LMAX_R222_FAIL_GBPUSD_EURGBP_SUCCESS_BASELINE_MISSING' 'EURGBP success missing.'
Assert-True ($eurgbp.entriesObserved -eq $true -and $eurgbp.sanitizedEntryCount -eq 2) 'LMAX_R222_FAIL_GBPUSD_EURGBP_SUCCESS_BASELINE_MISSING' 'EURGBP entries evidence missing.'
Assert-True ($eurgbp.rejectObserved -eq $false -and $eurgbp.shutdownRevertSucceeded -eq $true -and $eurgbp.cleanStateEvidence -eq $true) 'LMAX_R222_FAIL_GBPUSD_EURGBP_SUCCESS_BASELINE_MISSING' 'EURGBP clean success evidence missing.'

Assert-True ($archive.currentValidationSufficientForReadOnlyRuntimeProtocolProof -eq $true) 'LMAX_R222_FAIL_SCOPE_OVEREXPANSION_RISK' 'Current validation sufficiency missing.'
Assert-True ($scope.currentScopeSufficientForReadOnlyRuntimeProtocolProof -eq $true -and $scope.archiveComplete -eq $true) 'LMAX_R222_FAIL_SCOPE_OVEREXPANSION_RISK' 'Current scope archive decision missing.'
Assert-True ($scope.validatedInstruments -contains 'GBPUSD' -and $scope.validatedInstruments -contains 'EURGBP') 'LMAX_R222_FAIL_GBPUSD_EURGBP_SUCCESS_BASELINE_MISSING' 'Validated scope missing GBPUSD/EURGBP.'
Assert-True ($scope.audusdBlocksCurrentScopeClosure -eq $false -and $scope.usdjpyBlocksCurrentScopeClosure -eq $false) 'LMAX_R222_FAIL_SCOPE_OVEREXPANSION_RISK' 'AUDUSD/USDJPY incorrectly block current scope.'

Assert-True ($audusd.audusdPaused -eq $true -and $audusd.audusdClassifiedAsFailed -eq $false) 'LMAX_R222_FAIL_AUDUSD_MISCLASSIFIED_AS_FAILED' 'AUDUSD misclassified as failed.'
Assert-True ($audusd.audusdMarketDataTested -eq $false -and $audusd.r211.marketDataRequestSent -eq $false -and $audusd.r215.marketDataRequestSent -eq $false -and $audusd.r221.marketDataRequestSent -eq $false) 'LMAX_R222_FAIL_AUDUSD_MISCLASSIFIED_AS_FAILED' 'AUDUSD incorrectly classified as MarketData-tested.'
Assert-True ($audusd.continueRetriesWithoutConcreteChangeProductive -eq $false -and $audusd.futureWorkRequiresSeparateNoExternalReadinessGateAndConcreteReason -eq $true) 'LMAX_R222_FAIL_SCOPE_OVEREXPANSION_RISK' 'AUDUSD future controls missing.'

Assert-True ($usdJpy.usdJpyNotProven -eq $true -and $usdJpy.usdJpyClassifiedAsFailed -eq $false) 'LMAX_R222_FAIL_USDJPY_CAVEAT_WEAKENED' 'USDJPY misclassified.'
Assert-True ($usdJpy.usdJpyCaveatPreserved -eq $true -and $usdJpy.usdJpySecurityId -eq '4004' -and $usdJpy.usdJpySecurityIdSource -eq '8') 'LMAX_R222_FAIL_USDJPY_CAVEAT_WEAKENED' 'USDJPY caveat weakened.'
Assert-True ($usdJpy.usdJpyNotRequiredForCurrentValidationClosure -eq $true) 'LMAX_R222_FAIL_SCOPE_OVEREXPANSION_RISK' 'USDJPY incorrectly required for closure.'

Assert-True ($future.futureExpansionRequiresSeparateNoExternalReadinessGate -eq $true -and $future.directImmediateLiveRetryRecommended -eq $false) 'LMAX_R222_FAIL_SCOPE_OVEREXPANSION_RISK' 'Future scope controls missing.'
Assert-True ($future.futureAudusdWorkAllowedWithoutGate -eq $false -and $future.futureUsdjpyWorkAllowedWithoutGate -eq $false) 'LMAX_R222_FAIL_SCOPE_OVEREXPANSION_RISK' 'Future AUDUSD/USDJPY work allowed without gate.'
Assert-True ($future.apiWorkerLiveGatewayStillBlocked -eq $true -and $future.tradingOrdersStillBlocked -eq $true -and $future.schedulerPollingReplayStillBlocked -eq $true) 'LMAX_R222_FAIL_SCOPE_OVEREXPANSION_RISK' 'Future forbidden scope controls weakened.'
Assert-True ($next.immediateLiveRetryRecommended -eq $false -and $next.currentValidationArchiveComplete -eq $true) 'LMAX_R222_FAIL_SCOPE_OVEREXPANSION_RISK' 'Next recommendation allows immediate live retry or archive incomplete.'

Assert-True ($scope.apiWorkerLiveGatewayAuthorized -eq $false -and $scope.tradingOrOrdersAuthorized -eq $false -and $scope.schedulerPollingReplayAuthorized -eq $false) 'LMAX_R222_FAIL_SCOPE_OVEREXPANSION_RISK' 'Scope overexpanded.'
Assert-True ($apiWorker.fakeLmaxGatewayOnlyPreserved -eq $true -and $apiWorker.liveGatewayIntroducedInApiOrWorker -eq $false -and $apiWorker.apiWorkerStarted -eq $false) 'LMAX_R222_FAIL_API_WORKER_GATEWAY_REGRESSION' 'API/Worker gateway regression.'

Assert-True ($sanitization.credentialValuesReturned -eq $false) 'LMAX_R222_FAIL_SCOPE_OVEREXPANSION_RISK' 'credentialValuesReturned=false missing.'
Assert-True ($sanitization.rawFixMessagesSerialized -eq $false -and $sanitization.rawMarketDataPricesSerialized -eq $false -and $sanitization.rawMarketDataPayloadSerialized -eq $false) 'LMAX_R222_FAIL_SCOPE_OVEREXPANSION_RISK' 'Raw FIX/market-data leak risk.'
Assert-True ($sanitization.rawMdReqIdSerialized -eq $false -and $sanitization.rawEndpointValuesSerialized -eq $false -and $sanitization.rawTlsMaterialSerialized -eq $false) 'LMAX_R222_FAIL_SCOPE_OVEREXPANSION_RISK' 'Raw MDReqID/endpoint/TLS leak risk.'
Assert-True ($sanitization.rawCredentialsSerialized -eq $false -and $sanitization.rawSessionIdsSerialized -eq $false -and $sanitization.rawCompIdsSerialized -eq $false) 'LMAX_R222_FAIL_SCOPE_OVEREXPANSION_RISK' 'Secret/session/CompID leak risk.'
Assert-True ($sanitization.rawLogoutTextSerialized -eq $false -and $sanitization.rawRejectTextSerialized -eq $false) 'LMAX_R222_FAIL_SCOPE_OVEREXPANSION_RISK' 'Raw logout/reject leak risk.'

Assert-True ($forbidden.externalActivationAttempted -eq $false -and $forbidden.socketOpened -eq $false -and $forbidden.tlsAttempted -eq $false -and $forbidden.fixOpened -eq $false) 'LMAX_R222_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R222 external boundary action detected.'
Assert-True ($forbidden.marketDataRequestSent -eq $false -and $forbidden.marketDataResponseRead -eq $false) 'LMAX_R222_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R222 MarketData action detected.'
Assert-True ($forbidden.ordersIntroduced -eq $false -and $forbidden.tradingEnabled -eq $false -and $forbidden.tradingStateMutated -eq $false) 'LMAX_R222_FAIL_FORBIDDEN_ACTION_RISK' 'Order/trading path introduced.'
Assert-True ($forbidden.schedulerStarted -eq $false -and $forbidden.pollingStarted -eq $false -and $forbidden.serviceStarted -eq $false) 'LMAX_R222_FAIL_FORBIDDEN_ACTION_RISK' 'Scheduler/polling/service introduced.'
Assert-True ($forbidden.replayIntroduced -eq $false -and $forbidden.shadowReplayIntroduced -eq $false -and $forbidden.productionAccountUsed -eq $false) 'LMAX_R222_FAIL_FORBIDDEN_ACTION_RISK' 'Replay/shadow replay or production account introduced.'

Assert-True ($gate.buildStatus -eq 'PASS' -and $gate.validator -eq 'LMAX_R222_VALIDATION_PASS') 'LMAX_R222_FAIL_BUILD_OR_VALIDATOR' 'Build/validator evidence missing.'

$artifactText = Get-ChildItem -LiteralPath (Join-Path $Root $artifactRoot) -Filter 'phase-lmax-r222-*' -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combinedArtifactText = [string]::Join("`n", $artifactText)
foreach ($forbiddenToken in @([string][char]1, 'password=', 'username=', 'SenderCompId', 'TargetCompId', 'bid=', 'ask=', 'fix-marketdata.', '.lmax.com')) {
    Assert-True (-not $combinedArtifactText.Contains($forbiddenToken)) 'LMAX_R222_FAIL_SCOPE_OVEREXPANSION_RISK' "Forbidden serialized token detected in R222 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R222_VALIDATION_PASS'
