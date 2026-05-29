$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'
$phase = 'LMAX-R138'

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

$summary = Read-Json 'phase-lmax-r138-sanitized-runtime-reject-review.json'
$confirmation = Read-Json 'phase-lmax-r138-r137-sanitized-reason-confirmation.json'
$shape = Read-Json 'phase-lmax-r138-marketdatarequest-shape-review.json'
$candidateIssues = Read-Json 'phase-lmax-r138-marketdatarequest-candidate-issues.json'
$permission = Read-Json 'phase-lmax-r138-permission-entitlement-secondary-review.json'
$mapping = Read-Json 'phase-lmax-r138-instrument-mapping-secondary-review.json'
$decision = Read-Json 'phase-lmax-r138-repair-decision-gate.json'
$sanitization = Read-Json 'phase-lmax-r138-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r138-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r138-api-worker-fake-gateway-audit.json'
$gate = Read-Json 'phase-lmax-r138-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r137-weekday-market-hours-activation.json',
    'phase-lmax-r137-sessionreject-sanitized-reason-reporting.json',
    'phase-lmax-r137-marketdata-response-evidence.json',
    'phase-lmax-r133-sanitized-reason-category-contract.json',
    'phase-lmax-r135-sanitized-fixture-reclassification.json',
    'phase-lmax-r136-controlled-retry-readiness.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "Required predecessor evidence missing: $name"
}

$r137 = Read-Json 'phase-lmax-r137-weekday-market-hours-activation.json'
$r137Reason = Read-Json 'phase-lmax-r137-sessionreject-sanitized-reason-reporting.json'

Assert-True ($summary.phase -eq $phase) 'R138 summary phase mismatch.'
Assert-True ($summary.noExternal -eq $true) 'R138 must remain no-external.'
Assert-False $summary.externalActivationAttempted 'R138 external activation attempt detected.'
Assert-False $summary.socketTlsFixMarketDataRuntimeActionAttempted 'R138 runtime boundary action detected.'
Assert-True ($r137.attemptCount -eq 1) 'R137 external attemptCount must equal 1.'
Assert-True ($summary.r137ExternalAttemptCount -eq 1) 'R137 attempt count review missing or invalid.'
Assert-True ($summary.r137MarketDataResponseBoundaryReached -eq $true) 'R137 MarketDataResponse boundary review missing.'
Assert-True ($confirmation.marketDataResponseBoundaryReached -eq $true) 'R137 response boundary confirmation missing.'
Assert-True ($confirmation.cliReportingValue -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'R137 CLI sanitized reason category missing or unexpected.'
Assert-True ($confirmation.artifactReportingValue -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'R137 artifact sanitized reason category missing or unexpected.'
Assert-True ($r137Reason.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'R137 source CLI reason category missing or unexpected.'
Assert-True ($r137Reason.sanitizedSessionRejectReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'R137 source artifact reason category missing or unexpected.'

Assert-True ($shape.shapeReviewDecision -eq 'MarketDataRequestShapeRepairRecommended') 'MarketDataRequest shape review is missing.'
Assert-True ($shape.currentShapeSanitized.subscriptionProfile -eq 'SnapshotOnly') 'Current sanitized request profile review missing.'
Assert-True ($shape.currentShapeSanitized.symbolTextAlsoPresent -eq $true) 'Symbol/security-id shape review missing.'
Assert-True ($shape.localDocsEvidence.phase5hKnownRejectedProfilesAvoidSnapshotOnly -eq $true) 'Local docs snapshot-only review missing.'
Assert-True ($shape.localDocsEvidence.phase5hKnownRejectedProfilesAvoidTag55Shapes -eq $true) 'Local docs symbol-shape review missing.'
Assert-True ($candidateIssues.candidateIssuesReviewed -eq $true) 'Candidate issue review is missing.'
Assert-True (@($candidateIssues.candidateIssues).Count -ge 2) 'Candidate issue list is incomplete.'
Assert-True ($decision.repairDecisionGatePresent -eq $true) 'Repair decision gate is missing.'
Assert-True ($decision.decision -eq 'RecommendNoExternalMarketDataRequestShapeRepair') 'Repair decision is not shape repair.'
Assert-True ($permission.permissionEntitlementLeadingCause -eq $false) 'Permission/entitlement should remain secondary.'
Assert-True ($mapping.instrumentMappingLeadingCause -eq $false) 'Instrument mapping should remain secondary.'

$approved = @($summary.approvedInstruments)
Assert-True (($approved -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved instrument scope was weakened or changed.'
Assert-True ($summary.usdJpySecurityId -eq '4004') 'USDJPY SecurityID was weakened.'
Assert-True ($summary.usdJpySecurityIdSource -eq '8') 'USDJPY SecurityIDSource was weakened.'
Assert-True ($summary.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat was weakened.'
Assert-True ($mapping.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat review missing.'

Assert-True ($summary.credentialValuesReturned -eq $false) 'credentialValuesReturned must remain false.'
Assert-True ($sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned audit must remain false.'
Assert-False $sanitization.rawCredentialsSerialized 'Raw credentials were serialized.'
Assert-False $sanitization.rawSessionIdentifiersSerialized 'Raw session identifiers were serialized.'
Assert-False $sanitization.rawCompIdsSerialized 'Raw CompID values were serialized.'
Assert-False $sanitization.rawEndpointSerialized 'Raw endpoint values were serialized.'
Assert-False $sanitization.rawTlsMaterialSerialized 'Raw TLS material was serialized.'
Assert-False $sanitization.rawFixSerialized 'Raw FIX messages were serialized.'
Assert-False $sanitization.rawRejectTextSerialized 'Raw reject text was serialized.'

Assert-True ($forbidden.audit -eq 'PASS') 'Forbidden actions audit did not pass.'
Assert-False $forbidden.externalActivationAttempted 'R138 external activation detected.'
Assert-False $forbidden.socketTlsFixMarketDataRuntimeActionAttempted 'R138 runtime boundary action detected.'
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
Assert-True ($gate.focusedTests -match '^(PASS|NOT_REQUIRED)') 'Focused test evidence is missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit test evidence is missing.'
Assert-True ($gate.integrationTests -match '^PASS') 'Integration test evidence is missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R138_VALIDATION_PASS') 'Validator result evidence is missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r138-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R138 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R138_VALIDATION_PASS'
