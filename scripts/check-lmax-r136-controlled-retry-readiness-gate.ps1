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

$summary = Read-Json 'phase-lmax-r136-controlled-retry-readiness.json'
$checklist = Read-Json 'phase-lmax-r136-r137-preflight-checklist.json'
$reporting = Read-Json 'phase-lmax-r136-reporting-contract-readiness.json'
$evidence = Read-Json 'phase-lmax-r136-evidence-preservation.json'
$sanitization = Read-Json 'phase-lmax-r136-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r136-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r136-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r136-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r136-gate-validation.json'

$approvalTemplatePath = Join-Path $artifactRoot 'phase-lmax-r136-r137-operator-approval-template.md'
$activationPromptPath = Join-Path $artifactRoot 'phase-lmax-r136-r137-activation-prompt-compact.md'
Assert-True (Test-Path $approvalTemplatePath) 'R137 approval template is missing.'
Assert-True (Test-Path $activationPromptPath) 'R137 compact activation prompt is missing.'
$approvalTemplate = Get-Content $approvalTemplatePath -Raw
Assert-True ($approvalTemplate -match 'I, Philippe, explicitly approve Phase LMAX-R137') 'R137 exact approval text is missing.'
Assert-True ($approvalTemplate -match 'exactly one bounded attempt') 'R137 single-attempt approval constraint is missing.'
Assert-True ($approvalTemplate -match 'weekday market-hours') 'R137 market-hours approval constraint is missing.'

$requiredEvidence = @{
    'phase-lmax-r131-weekday-market-hours-activation.json' = 'LMAX_R131_FAIL_MARKETDATA_RESPONSE_BOUNDARY'
    'phase-lmax-r132-weekday-runtime-reject-review.json' = 'LMAX_R132_PASS_WEEKDAY_RUNTIME_REJECT_REVIEW_REASON_REPORTING_FIX_RECOMMENDED_NO_EXTERNAL'
    'phase-lmax-r133-sanitized-sessionreject-reporting-fix.json' = 'LMAX_R133_PASS_SANITIZED_SESSIONREJECT_REASON_REPORTING_FIX_NO_EXTERNAL'
    'phase-lmax-r134-r131-sanitized-reason-rehydration.json' = 'LMAX_R134_PASS_R131_SANITIZED_REASON_NOT_REHYDRATABLE_FIXTURE_RECLASSIFICATION_RECOMMENDED_NO_EXTERNAL'
    'phase-lmax-r135-sanitized-fixture-reclassification.json' = 'LMAX_R135_PASS_SANITIZED_FIXTURE_RECLASSIFICATION_RETRY_RECOMMENDED_NO_EXTERNAL'
}

foreach ($item in $requiredEvidence.GetEnumerator()) {
    $json = Read-Json $item.Key
    Assert-True ($json.classification -eq $item.Value) "Prior evidence missing or mismatched: $($item.Key)"
}

$r131 = Read-Json 'phase-lmax-r131-weekday-market-hours-activation.json'
Assert-True ($r131.attemptCount -eq 1) 'R131 attemptCount must remain 1.'

Assert-True ($summary.noExternal -eq $true) 'R136 must be no-external.'
Assert-False $summary.externalActivationAttempted 'R136 external activation attempt detected.'
Assert-False $summary.socketTlsFixMarketDataRuntimeActionAttempted 'R136 socket/TLS/FIX/MarketData runtime action detected.'
Assert-True ($summary.nextActivationPhasePrepared -eq 'LMAX-R137') 'R137 was not prepared.'
Assert-False $summary.r137ActivationExecuted 'R137 must not be executed in R136.'
Assert-True ($summary.r137RequiresFreshExactOperatorApproval -eq $true) 'R137 fresh approval requirement is missing.'
Assert-True ($summary.r137RequiresWeekdayActiveFxMarketDataAvailabilityWindow -eq $true) 'R137 market-hours requirement is missing.'
Assert-True ($summary.r137RequiresExactlyOneBoundedAttempt -eq $true) 'R137 single-attempt requirement is missing.'
Assert-True ($summary.r133CliReportingContractReady -eq $true) 'R133 CLI reporting readiness is missing.'
Assert-True ($summary.r133ArtifactReportingContractReady -eq $true) 'R133 artifact reporting readiness is missing.'
Assert-True ($summary.r135FixtureReportingVerificationReady -eq $true) 'R135 fixture/reporting verification is missing.'

Assert-True ($checklist.freshExactOperatorApprovalRequired -eq $true) 'R137 approval checklist requirement is missing.'
Assert-True ($checklist.weekdayActiveFxMarketDataAvailabilityWindowRequired -eq $true) 'R137 weekday market-hours checklist requirement is missing.'
Assert-True ($checklist.exactlyOneBoundedAttemptRequired -eq $true) 'R137 one-attempt checklist requirement is missing.'
Assert-True ($checklist.r133CliFieldRequired -eq 'sessionRejectSanitizedReasonCategory') 'R133 CLI field requirement missing from checklist.'
Assert-True ($checklist.r133ArtifactFieldRequired -eq 'sanitizedSessionRejectReasonCategory') 'R133 artifact field requirement missing from checklist.'

Assert-True ($reporting.r133ReportingContractReady -eq $true) 'Reporting contract readiness is missing.'
Assert-True ($reporting.r135FixtureReportingVerificationReady -eq $true) 'Fixture reporting readiness is missing.'
Assert-True ($reporting.futureLiveEvidenceCorrectlyReportable -eq $true) 'Future live evidence reportability is missing.'
Assert-False $reporting.rawRejectTextSerialized 'Raw reject text serialized in reporting readiness.'
Assert-False $reporting.rawFixSerialized 'Raw FIX serialized in reporting readiness.'

Assert-True ($evidence.r131EvidencePreserved -eq $true) 'R131 evidence preservation missing.'
Assert-True ($evidence.r132EvidencePreserved -eq $true) 'R132 evidence preservation missing.'
Assert-True ($evidence.r133EvidencePreserved -eq $true) 'R133 evidence preservation missing.'
Assert-True ($evidence.r134EvidencePreserved -eq $true) 'R134 evidence preservation missing.'
Assert-True ($evidence.r135EvidencePreserved -eq $true) 'R135 evidence preservation missing.'

Assert-True ($sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned must remain false.'
Assert-False $sanitization.rawCredentialsSerialized 'Raw credentials were serialized.'
Assert-False $sanitization.rawEndpointValuesSerialized 'Raw endpoint values were serialized.'
Assert-False $sanitization.rawTlsMaterialSerialized 'Raw TLS material was serialized.'
Assert-False $sanitization.rawFixMessagesSerialized 'Raw FIX messages were serialized.'
Assert-False $sanitization.rawCompIdsSerialized 'Raw CompIDs were serialized.'
Assert-False $sanitization.rawSessionIdsSerialized 'Raw session IDs were serialized.'
Assert-False $sanitization.rawUsernamePasswordSerialized 'Raw username/password values were serialized.'
Assert-False $sanitization.rawRejectTextSerialized 'Raw reject text was serialized.'

Assert-True (($summary.approvedInstruments -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved instrument scope was weakened.'
Assert-True ($summary.usdJpySecurityId -eq '4004') 'USDJPY SecurityID was weakened.'
Assert-True ($summary.usdJpySecurityIdSource -eq '8') 'USDJPY SecurityIDSource was weakened.'
Assert-True ($summary.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat was weakened.'

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

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R137') 'Next phase recommendation must be R137.'
Assert-True ($next.r137RequiresFreshExplicitApproval -eq $true) 'R137 fresh approval recommendation is missing.'
Assert-True ($next.r137MustPerformExactlyOneBoundedAttemptOnly -eq $true) 'R137 exact single-attempt recommendation is missing.'

Assert-True ($gate.buildResult -match '^PASS') 'Build evidence is missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit test evidence is missing.'
Assert-True ($gate.integrationTests -match '^PASS') 'Integration test evidence is missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R136_VALIDATION_PASS') 'Validator evidence is missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r136-*' -File |
    Where-Object { $_.Extension -in '.json', '.md' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX token serialized in R136 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R136_VALIDATION_PASS'
