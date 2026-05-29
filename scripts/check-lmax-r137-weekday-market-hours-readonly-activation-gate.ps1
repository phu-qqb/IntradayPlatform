$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'
$phase = 'LMAX-R137'
$expectedApproval = 'I, Philippe, explicitly approve Phase LMAX-R137 for one temporary QQ Workspace Demo weekday market-hours read-only runtime market-data activation retry with sanitized SessionReject reason reporting after R133/R135/R136 readiness for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, exactly one bounded attempt, and immediate abort authority.'

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

$approval = (Get-Content (Join-Path $artifactRoot 'phase-lmax-r137-operator-approval.txt') -Raw).Trim()
$expectedApprovalFile = (Get-Content (Join-Path $artifactRoot 'phase-lmax-r137-expected-operator-approval.txt') -Raw).Trim()
Assert-True ($approval -ceq $expectedApproval) 'R137 operator approval is missing or mismatched.'
Assert-True ($expectedApprovalFile -ceq $expectedApproval) 'R137 expected approval file is missing or mismatched.'

$summary = Read-Json 'phase-lmax-r137-weekday-market-hours-activation.json'
$preflight = Read-Json 'phase-lmax-r137-preflight-result.json'
$boundary = Read-Json 'phase-lmax-r137-boundary-evidence.json'
$request = Read-Json 'phase-lmax-r137-marketdata-request-evidence.json'
$response = Read-Json 'phase-lmax-r137-marketdata-response-evidence.json'
$sessionReject = Read-Json 'phase-lmax-r137-sessionreject-sanitized-reason-reporting.json'
$instrument = Read-Json 'phase-lmax-r137-approved-instrument-evidence.json'
$usdJpy = Read-Json 'phase-lmax-r137-usdjpy-caveat-preservation.json'
$sanitization = Read-Json 'phase-lmax-r137-sanitized-result.json'
$shutdown = Read-Json 'phase-lmax-r137-shutdown-revert-evidence.json'
$forbidden = Read-Json 'phase-lmax-r137-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r137-api-worker-fake-gateway-audit.json'
$gate = Read-Json 'phase-lmax-r137-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r131-weekday-market-hours-activation.json',
    'phase-lmax-r132-weekday-runtime-reject-review.json',
    'phase-lmax-r133-sanitized-reason-category-contract.json',
    'phase-lmax-r134-r131-sanitized-reason-rehydration.json',
    'phase-lmax-r135-sanitized-fixture-reclassification.json',
    'phase-lmax-r136-controlled-retry-readiness.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "Required predecessor evidence missing: $name"
}

$r131 = Read-Json 'phase-lmax-r131-weekday-market-hours-activation.json'
$r133 = Read-Json 'phase-lmax-r133-sanitized-reason-category-contract.json'
$r135 = Read-Json 'phase-lmax-r135-sanitized-fixture-reclassification.json'
$r136 = Read-Json 'phase-lmax-r136-controlled-retry-readiness.json'
Assert-True ($r131.attemptCount -eq 1) 'R131 attemptCount must remain 1.'
Assert-True ($r133.cliOutputField -eq 'sessionRejectSanitizedReasonCategory') 'R133 CLI reporting readiness is missing.'
Assert-True ($r133.artifactOutputField -eq 'sanitizedSessionRejectReasonCategory') 'R133 artifact reporting readiness is missing.'
Assert-True ($r135.classification -eq 'LMAX_R135_PASS_SANITIZED_FIXTURE_RECLASSIFICATION_RETRY_RECOMMENDED_NO_EXTERNAL') 'R135 fixture reporting readiness is missing.'
Assert-True ($r136.classification -eq 'LMAX_R136_PASS_CONTROLLED_RETRY_READINESS_AFTER_SANITIZED_REASON_REPORTING_FIX_NO_EXTERNAL') 'R136 readiness evidence is missing or mismatched.'

Assert-True ($summary.phase -eq $phase) 'R137 summary phase mismatch.'
Assert-True ($summary.externalActivationAttempted -eq $true) 'R137 external activation evidence is missing.'
Assert-True ($summary.attemptCount -eq 1) 'R137 must have exactly one external attempt.'
Assert-True ($summary.preExternalInvocationAttemptCount -eq 0) 'Pre-external reservation stop must not count as an external attempt.'
Assert-True ($preflight.freshExactOperatorApprovalMatched -eq $true) 'Fresh exact R137 approval was not represented.'
Assert-True ($preflight.operatorConfirmedMarketHoursWindow.weekday -eq $true) 'R137 weekday market-hours evidence is missing.'
Assert-True ($preflight.operatorConfirmedMarketHoursWindow.marketHoursConstraintRepresented -eq $true) 'R137 market-hours constraint is missing.'

Assert-True ($boundary.boundaryEvidence.'FIX logon/session' -eq 'Succeeded') 'MarketDataRequest cannot occur without FIX success.'
Assert-True ($request.fixSessionSucceededBeforeMarketData -eq $true) 'MarketDataRequest occurred without proven FIX success.'
Assert-True ($request.marketDataRequestAttempted -eq $true) 'MarketDataRequest evidence is missing.'
Assert-True ($request.marketDataRequestResult -eq 'Succeeded') 'MarketDataRequest did not complete before response classification.'
Assert-True ($response.marketDataResponseReadAttempted -eq $true) 'MarketDataResponse read evidence is missing.'
Assert-True ($response.marketDataResponseReaderParserClassifierUsed -eq $true) 'MarketDataResponse parser/classifier evidence is missing.'
Assert-True ($response.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'R137 SessionReject category is missing or mismatched.'
Assert-True ($sessionReject.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'CLI sanitized SessionReject reason category is missing.'
Assert-True ($sessionReject.sanitizedSessionRejectReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'Artifact sanitized SessionReject reason category is missing.'
Assert-True ($response.sessionRejectSanitizedReasonCategory -eq $sessionReject.sessionRejectSanitizedReasonCategory) 'Response and CLI sanitized reason categories diverged.'
Assert-True ($sanitization.sanitizedSessionRejectReasonCategory -eq $sessionReject.sanitizedSessionRejectReasonCategory) 'Sanitized result reason category is missing.'

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
Assert-False $sanitization.rawRejectTextSerialized 'Raw reject text was serialized.'
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
Assert-True ($gate.validatorResult -eq 'LMAX_R137_VALIDATION_PASS') 'Validator result evidence is missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r137-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R137 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R137_VALIDATION_PASS'
