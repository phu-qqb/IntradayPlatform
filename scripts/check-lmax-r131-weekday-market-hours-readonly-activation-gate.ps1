$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'
$phase = 'LMAX-R131'
$expectedApproval = 'I, Philippe, explicitly approve Phase LMAX-R131 for one temporary QQ Workspace Demo weekday market-hours read-only runtime market-data activation retry after the R129 SessionReject reason enrichment and R130 readiness package for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, exactly one bounded attempt, and immediate abort authority.'

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

$approval = (Get-Content (Join-Path $artifactRoot 'phase-lmax-r131-operator-approval.txt') -Raw).Trim()
$expectedApprovalFile = (Get-Content (Join-Path $artifactRoot 'phase-lmax-r131-expected-operator-approval.txt') -Raw).Trim()
Assert-True ($approval -ceq $expectedApproval) 'R131 operator approval is missing or mismatched.'
Assert-True ($expectedApprovalFile -ceq $expectedApproval) 'R131 expected approval file is missing or mismatched.'

$summary = Read-Json 'phase-lmax-r131-weekday-market-hours-activation.json'
$preflight = Read-Json 'phase-lmax-r131-preflight-result.json'
$boundary = Read-Json 'phase-lmax-r131-boundary-evidence.json'
$request = Read-Json 'phase-lmax-r131-marketdata-request-evidence.json'
$response = Read-Json 'phase-lmax-r131-marketdata-response-evidence.json'
$sessionReject = Read-Json 'phase-lmax-r131-sessionreject-enriched-classification.json'
$instrument = Read-Json 'phase-lmax-r131-approved-instrument-evidence.json'
$usdJpy = Read-Json 'phase-lmax-r131-usdjpy-caveat-preservation.json'
$sanitization = Read-Json 'phase-lmax-r131-sanitized-result.json'
$shutdown = Read-Json 'phase-lmax-r131-shutdown-revert-evidence.json'
$forbidden = Read-Json 'phase-lmax-r131-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r131-api-worker-fake-gateway-audit.json'
$gate = Read-Json 'phase-lmax-r131-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r127-temporary-readonly-activation-retry-summary.json',
    'phase-lmax-r128-temporary-runtime-evidence-review-summary.json',
    'phase-lmax-r129-sessionreject-reason-enrichment.json',
    'phase-lmax-r130-weekday-market-hours-readiness.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "Required predecessor evidence missing: $name"
}

$r127 = Read-Json 'phase-lmax-r127-temporary-readonly-activation-retry-summary.json'
$r128 = Read-Json 'phase-lmax-r128-temporary-runtime-evidence-review-summary.json'
$r129 = Read-Json 'phase-lmax-r129-sessionreject-reason-enrichment.json'
$r130 = Read-Json 'phase-lmax-r130-weekday-market-hours-readiness.json'
Assert-True ($r127.attemptCount -eq 1) 'R127 attemptCount must remain 1.'
Assert-True ($r128.classification -eq 'LMAX_R128_PASS_RUNTIME_EVIDENCE_REVIEW_INCONCLUSIVE_SAFE_NO_EXTERNAL_ACTIVATION') 'R128 evidence classification is missing or mismatched.'
Assert-True ($r129.classification -eq 'LMAX_R129_PASS_SESSIONREJECT_REASON_ENRICHMENT_READY_NO_EXTERNAL') 'R129 enrichment evidence is missing or mismatched.'
Assert-True ($r130.classification -eq 'LMAX_R130_PASS_WEEKDAY_MARKET_HOURS_RETRY_READINESS_PACKAGE_NO_EXTERNAL') 'R130 readiness evidence is missing or mismatched.'

Assert-True ($summary.phase -eq $phase) 'R131 summary phase mismatch.'
Assert-True ($summary.externalActivationAttempted -eq $true) 'R131 external activation evidence is missing.'
Assert-True ($summary.attemptCount -eq 1) 'R131 must have exactly one external attempt.'
Assert-True ($preflight.operatorConfirmedWeekdayMarketHoursWindow -eq $true) 'Operator-confirmed weekday market-hours window is missing.'
Assert-True ($preflight.weekday -eq $true) 'R131 market-hours weekday condition is not represented.'
Assert-True ($preflight.r129EnrichedClassificationReady -eq $true) 'R129 enriched classification readiness is missing.'
Assert-True ($sessionReject.r129EnrichmentUsed -eq $true) 'R129 enriched SessionReject classification was not used.'

Assert-True ($boundary.boundaryEvidence.'FIX logon/session' -eq 'Succeeded') 'MarketDataRequest cannot be evaluated without FIX success.'
Assert-True ($request.fixSessionSucceededBeforeMarketData -eq $true) 'MarketDataRequest occurred without proven FIX success.'
Assert-True ($request.marketDataRequestAttempted -eq $true) 'MarketDataRequest evidence is missing.'
Assert-True ($request.marketDataRequestResult -eq 'Succeeded') 'MarketDataRequest did not complete successfully before response classification.'
Assert-True ($response.marketDataResponseReadAttempted -eq $true) 'MarketDataResponse read evidence is missing.'
Assert-True ($response.marketDataResponseReaderParserClassifierUsed -eq $true) 'MarketDataResponse parser/classifier evidence is missing.'
Assert-True ($response.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'R131 enriched SessionReject category is missing or mismatched.'

$approved = @($instrument.approvedInstruments)
Assert-True (($approved -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved instrument scope was weakened or changed.'
Assert-True ($instrument.nonApprovedInstrumentsRequested -eq $false) 'Non-approved instruments were requested.'
Assert-True ($instrument.sixEAbsent -eq $true) '6E must remain absent.'
Assert-True ($usdJpy.securityId -eq '4004') 'USDJPY SecurityID was weakened.'
Assert-True ($usdJpy.securityIdSource -eq '8') 'USDJPY SecurityIDSource was weakened.'
Assert-True ($usdJpy.caveatPreserved -eq $true) 'USDJPY caveat was weakened.'

Assert-True ($sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned must remain false.'
Assert-False $sanitization.rawCredentialsSerialized 'Raw credentials were serialized.'
Assert-False $sanitization.rawUsernamePasswordSerialized 'Raw username/password values were serialized.'
Assert-False $sanitization.rawCompIdSerialized 'Raw CompID values were serialized.'
Assert-False $sanitization.rawSessionIdSerialized 'Raw session identifiers were serialized.'
Assert-False $sanitization.rawEndpointSerialized 'Raw endpoint values were serialized.'
Assert-False $sanitization.rawTlsMaterialSerialized 'Raw TLS material was serialized.'
Assert-False $sanitization.rawFixSerialized 'Raw FIX messages were serialized.'
Assert-False $sanitization.rawSensitiveFixLogsSerialized 'Raw sensitive FIX logs were serialized.'
Assert-True ($shutdown.shutdownRevertCompleted -eq $true) 'Shutdown/revert evidence is missing.'

Assert-True ($forbidden.audit -eq 'PASS') 'Forbidden actions audit did not pass.'
Assert-False $forbidden.ordersIntroduced 'Order path was introduced.'
Assert-False $forbidden.tradingEnabled 'Trading was enabled.'
Assert-False $forbidden.schedulerIntroduced 'Scheduler was introduced.'
Assert-False $forbidden.pollingLoopIntroduced 'Polling loop was introduced.'
Assert-False $forbidden.serviceIntroduced 'Service was introduced.'
Assert-False $forbidden.replayIntroduced 'Replay was introduced.'
Assert-False $forbidden.shadowReplayIntroduced 'Shadow replay was introduced.'
Assert-True ($apiWorker.audit -eq 'PASS') 'API/Worker audit did not pass.'
Assert-True ($apiWorker.apiWorkerGatewayMode -eq 'FakeLmaxGatewayOnly') 'API/Worker FakeLmaxGatewayOnly regressed.'

Assert-True ($gate.buildResult -match '^PASS') 'Build evidence is missing.'
Assert-True ($gate.focusedTests -match '^PASS') 'Focused test evidence is missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit test evidence is missing.'
Assert-True ($gate.integrationTests -match '^PASS') 'Integration test evidence is missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R131_VALIDATION_PASS') 'Validator result evidence is missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r131-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX token serialized in R131 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R131_VALIDATION_PASS'
