$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'

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

$review = Read-Json 'phase-lmax-r132-weekday-runtime-reject-review.json'
$reason = Read-Json 'phase-lmax-r132-r131-sanitized-reason-category-review.json'
$shape = Read-Json 'phase-lmax-r132-marketdata-request-shape-decision.json'
$permission = Read-Json 'phase-lmax-r132-permission-session-account-decision.json'
$mapping = Read-Json 'phase-lmax-r132-instrument-mapping-decision.json'
$gap = Read-Json 'phase-lmax-r132-parser-classifier-reporting-gap-review.json'
$next = Read-Json 'phase-lmax-r132-next-phase-recommendation.json'
$sanitization = Read-Json 'phase-lmax-r132-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r132-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r132-api-worker-fake-gateway-audit.json'
$gate = Read-Json 'phase-lmax-r132-gate-validation.json'

$r131 = Read-Json 'phase-lmax-r131-weekday-market-hours-activation.json'
$r131Boundary = Read-Json 'phase-lmax-r131-boundary-evidence.json'
$r131Response = Read-Json 'phase-lmax-r131-marketdata-response-evidence.json'
$r131SessionReject = Read-Json 'phase-lmax-r131-sessionreject-enriched-classification.json'

Assert-True ($review.phase -eq 'LMAX-R132') 'R132 review phase mismatch.'
Assert-True ($review.noExternal -eq $true) 'R132 must be no-external.'
Assert-False $review.externalActivationAttempted 'R132 external activation attempt detected.'
Assert-False $review.socketTlsFixMarketDataRuntimeActionAttempted 'R132 socket/TLS/FIX/MarketData runtime action detected.'

Assert-True ($r131.phase -eq 'LMAX-R131') 'R131 evidence is missing.'
Assert-True ($r131.attemptCount -eq 1) 'R131 attemptCount must be exactly 1.'
Assert-True ($r131.operatorConfirmedMarketHoursWindow.weekday -eq $true) 'R131 weekday market-hours evidence is missing.'
Assert-True ($r131Boundary.boundaryEvidence.'MarketDataResponse/entries' -eq 'FailedValidation') 'R131 MarketDataResponse boundary evidence is missing.'
Assert-True ($r131Response.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'R131 SessionRejectObservedWithSanitizedReason support is missing.'
Assert-True ($r131SessionReject.r129EnrichmentUsed -eq $true) 'R129 enriched classifier evidence is missing from R131.'

Assert-True ($review.sessionRejectObservedWithSanitizedReasonSupported -eq $true) 'SessionRejectObservedWithSanitizedReason was not reviewed as supported.'
Assert-True ($reason.sessionRejectCategory -eq 'SessionRejectObservedWithSanitizedReason') 'Sanitized reason category review is missing SessionReject category.'
Assert-True ($reason.sanitizedReasonCategoryAvailableInClassifier -eq $true) 'Classifier sanitized reason availability is missing.'
Assert-True ($reason.exactSanitizedReasonCategoryPresentInArtifacts -eq $false) 'R132 should record that exact sanitized reason is absent from R131 artifacts.'
Assert-True ($gap.reportingGapPresent -eq $true) 'Parser/classifier reporting gap review is missing.'
Assert-True ($gap.classifierSupportsSanitizedReasonCategory -eq $true) 'Classifier support for sanitized reason category is not represented.'
Assert-True ($gap.cliReportsExactSanitizedReasonCategory -eq $false) 'CLI exact sanitized reason reporting gap is not represented.'

Assert-True ($shape.marketDataRequestShapeIssueProven -eq $false) 'MarketDataRequest shape was over-classified as proven.'
Assert-True ($shape.doNotOverClassifyAsShapeBug -eq $true) 'MarketDataRequest over-classification guard is missing.'
Assert-True ($permission.permissionSessionAccountIssueProven -eq $false) 'Permission/session/account issue was over-classified as proven.'
Assert-True ($mapping.instrumentMappingIssueProven -eq $false) 'Instrument mapping issue was over-classified as proven.'

$approved = @($mapping.approvedInstrumentScope)
Assert-True (($approved -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved instrument scope was weakened.'
Assert-True ($mapping.nonApprovedInstrumentsAbsent -eq $true) 'Non-approved instruments are not rejected/absent.'
Assert-True ($mapping.usdJpySecurityId -eq '4004') 'USDJPY SecurityID was weakened.'
Assert-True ($mapping.usdJpySecurityIdSource -eq '8') 'USDJPY SecurityIDSource was weakened.'
Assert-True ($mapping.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat was weakened.'

Assert-True ($sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned must remain false.'
Assert-False $sanitization.rawCredentialsSerialized 'Raw credentials were serialized.'
Assert-False $sanitization.rawEndpointValuesSerialized 'Raw endpoint values were serialized.'
Assert-False $sanitization.rawTlsMaterialSerialized 'Raw TLS material was serialized.'
Assert-False $sanitization.rawFixMessagesSerialized 'Raw FIX messages were serialized.'
Assert-False $sanitization.rawCompIdsSerialized 'Raw CompIDs were serialized.'
Assert-False $sanitization.rawSessionIdsSerialized 'Raw session identifiers were serialized.'
Assert-False $sanitization.rawUsernamePasswordSerialized 'Raw username/password values were serialized.'
Assert-False $sanitization.rawRejectTextSerialized 'Raw reject text was serialized.'

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

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R133') 'Next phase recommendation is missing.'
Assert-True ($next.r133MustRemainNoExternal -eq $true) 'R133 no-external requirement is missing.'

Assert-True ($gate.buildResult -match '^PASS') 'Build evidence is missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit test evidence is missing.'
Assert-True ($gate.integrationTests -match '^PASS') 'Integration test evidence is missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R132_VALIDATION_PASS') 'Validator evidence is missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r132-*' -File |
    Where-Object { $_.Extension -in '.json', '.md' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX token serialized in R132 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R132_VALIDATION_PASS'
