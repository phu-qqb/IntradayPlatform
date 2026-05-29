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

$rehydration = Read-Json 'phase-lmax-r134-r131-sanitized-reason-rehydration.json'
$repair = Read-Json 'phase-lmax-r134-repair-decision-gate.json'
$shape = Read-Json 'phase-lmax-r134-marketdata-request-repair-decision.json'
$permission = Read-Json 'phase-lmax-r134-permission-entitlement-decision.json'
$mapping = Read-Json 'phase-lmax-r134-instrument-mapping-decision.json'
$feasibility = Read-Json 'phase-lmax-r134-reclassification-feasibility-review.json'
$sanitization = Read-Json 'phase-lmax-r134-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r134-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r134-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r134-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r134-gate-validation.json'

$r131 = Read-Json 'phase-lmax-r131-weekday-market-hours-activation.json'
$r131Reason = Read-Json 'phase-lmax-r131-sessionreject-enriched-classification.json'
$r132 = Read-Json 'phase-lmax-r132-weekday-runtime-reject-review.json'
$r133 = Read-Json 'phase-lmax-r133-sanitized-reason-category-contract.json'

Assert-True ($rehydration.noExternal -eq $true) 'R134 must be no-external.'
Assert-False $rehydration.externalActivationAttempted 'R134 external activation attempt detected.'
Assert-False $rehydration.socketTlsFixMarketDataRuntimeActionAttempted 'R134 socket/TLS/FIX/MarketData runtime action detected.'

Assert-True ($r131.classification -eq 'LMAX_R131_FAIL_MARKETDATA_RESPONSE_BOUNDARY') 'R131 evidence is missing or mismatched.'
Assert-True ($r131.attemptCount -eq 1) 'R131 attemptCount must be exactly 1.'
Assert-True ($r132.classification -eq 'LMAX_R132_PASS_WEEKDAY_RUNTIME_REJECT_REVIEW_REASON_REPORTING_FIX_RECOMMENDED_NO_EXTERNAL') 'R132 evidence is missing or mismatched.'
Assert-True ($r133.contractReady -eq $true) 'R133 reporting contract is missing.'
Assert-True ($r133.cliOutputField -eq 'sessionRejectSanitizedReasonCategory') 'R133 CLI reporting contract is missing.'
Assert-True ($r133.artifactOutputField -eq 'sanitizedSessionRejectReasonCategory') 'R133 artifact reporting contract is missing.'

Assert-True ($r131Reason.reportedSanitizedReasonCategory -eq 'SanitizedReasonCategoryNotPrintedByCli') 'R131 missing-category evidence is not represented.'
Assert-True ($rehydration.r131SanitizedReasonRehydratable -eq $false) 'R134 rehydration decision is missing or unsafe.'
Assert-True ($rehydration.exactSanitizedReasonCategory -eq 'NotRehydratableFromR131Artifacts') 'R134 exact sanitized reason decision is missing.'
Assert-True ($repair.repairDecision -eq 'sanitized_fixture_reclassification_harness_recommended') 'Repair decision gate is missing.'
Assert-True ($feasibility.fixtureReclassificationFeasibleNoExternal -eq $true) 'Fixture reclassification feasibility is missing.'
Assert-True ($feasibility.directRehydrationFromR131ArtifactsFeasible -eq $false) 'Direct rehydration feasibility must be false.'

Assert-True ($shape.marketDataRequestShapeIssueProven -eq $false) 'MarketDataRequest repair was over-selected.'
Assert-True ($permission.permissionSessionAccountIssueProven -eq $false) 'Permission/entitlement was over-selected.'
Assert-True ($mapping.instrumentMappingIssueProven -eq $false) 'Instrument mapping was over-selected.'
Assert-True (($mapping.approvedInstrumentScope -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved instrument scope was weakened.'
Assert-True ($mapping.usdJpySecurityId -eq '4004') 'USDJPY SecurityID was weakened.'
Assert-True ($mapping.usdJpySecurityIdSource -eq '8') 'USDJPY SecurityIDSource was weakened.'
Assert-True ($mapping.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat was weakened.'

Assert-True ($sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned must remain false.'
Assert-False $sanitization.rawCredentialsSerialized 'Raw credentials were serialized.'
Assert-False $sanitization.rawEndpointValuesSerialized 'Raw endpoint values were serialized.'
Assert-False $sanitization.rawTlsMaterialSerialized 'Raw TLS material was serialized.'
Assert-False $sanitization.rawFixMessagesSerialized 'Raw FIX messages were serialized.'
Assert-False $sanitization.rawCompIdsSerialized 'Raw CompIDs were serialized.'
Assert-False $sanitization.rawSessionIdsSerialized 'Raw session IDs were serialized.'
Assert-False $sanitization.rawUsernamePasswordSerialized 'Raw username/password values were serialized.'
Assert-False $sanitization.rawRejectTextSerialized 'Raw reject text was serialized.'

Assert-True ($forbidden.audit -eq 'PASS') 'Forbidden actions audit did not pass.'
Assert-False $forbidden.ordersIntroduced 'Order path was introduced.'
Assert-False $forbidden.tradingEnabled 'Trading was enabled.'
Assert-False $forbidden.schedulerIntroduced 'Scheduler was introduced.'
Assert-False $forbidden.pollingLoopIntroduced 'Polling was introduced.'
Assert-False $forbidden.serviceIntroduced 'Service was introduced.'
Assert-False $forbidden.replayIntroduced 'Replay was introduced.'
Assert-False $forbidden.shadowReplayIntroduced 'Shadow replay was introduced.'
Assert-True ($apiWorker.audit -eq 'PASS') 'API/Worker audit did not pass.'
Assert-True ($apiWorker.apiWorkerGatewayMode -eq 'FakeLmaxGatewayOnly') 'API/Worker FakeLmaxGatewayOnly regressed.'

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R135') 'Next phase recommendation is missing.'
Assert-True ($next.r135MustRemainNoExternal -eq $true) 'R135 no-external requirement is missing.'

Assert-True ($gate.buildResult -match '^PASS') 'Build evidence is missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit test evidence is missing.'
Assert-True ($gate.integrationTests -match '^PASS') 'Integration test evidence is missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R134_VALIDATION_PASS') 'Validator evidence is missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r134-*' -File |
    Where-Object { $_.Extension -in '.json', '.md' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX token serialized in R134 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R134_VALIDATION_PASS'
