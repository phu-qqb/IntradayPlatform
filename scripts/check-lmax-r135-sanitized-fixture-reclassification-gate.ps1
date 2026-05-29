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

$summary = Read-Json 'phase-lmax-r135-sanitized-fixture-reclassification.json'
$r131Fixture = Read-Json 'phase-lmax-r135-r131-equivalent-fixture-contract.json'
$coverage = Read-Json 'phase-lmax-r135-category-coverage-matrix.json'
$cli = Read-Json 'phase-lmax-r135-cli-reporting-contract-evidence.json'
$artifact = Read-Json 'phase-lmax-r135-artifact-reporting-contract-evidence.json'
$decision = Read-Json 'phase-lmax-r135-repair-decision-gate.json'
$sanitization = Read-Json 'phase-lmax-r135-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r135-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r135-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r135-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r135-gate-validation.json'

$r131 = Read-Json 'phase-lmax-r131-weekday-market-hours-activation.json'
$r134 = Read-Json 'phase-lmax-r134-r131-sanitized-reason-rehydration.json'
Assert-True ($r131.classification -eq 'LMAX_R131_FAIL_MARKETDATA_RESPONSE_BOUNDARY') 'R131 evidence is missing.'
Assert-True ($r131.attemptCount -eq 1) 'R131 attemptCount must remain 1.'
Assert-True ($r134.classification -eq 'LMAX_R134_PASS_R131_SANITIZED_REASON_NOT_REHYDRATABLE_FIXTURE_RECLASSIFICATION_RECOMMENDED_NO_EXTERNAL') 'R134 evidence is missing.'

Assert-True ($summary.noExternal -eq $true) 'R135 must be no-external.'
Assert-False $summary.externalActivationAttempted 'R135 external activation attempt detected.'
Assert-False $summary.socketTlsFixMarketDataRuntimeActionAttempted 'R135 socket/TLS/FIX/MarketData runtime action detected.'
Assert-True ($summary.fixtureHarnessReady -eq $true) 'Sanitized fixture harness is missing.'
Assert-True ($summary.sanitizedFixtureInputsOnly -eq $true) 'Fixture inputs must be sanitized only.'
Assert-True ($summary.allowedCategoryCoverageComplete -eq $true) 'Allowed category coverage is incomplete.'
Assert-True ($summary.r131EquivalentFixtureReady -eq $true) 'R131-equivalent fixture contract is missing.'
Assert-True ($summary.notRehydratableFromR131ArtifactsDistinctionPreserved -eq $true) 'NotRehydratableFromR131Artifacts distinction was erased.'
Assert-True ($summary.fixtureBehaviorDoesNotInferRealR131RootCause -eq $true) 'Fixture behavior was used to infer real R131 root cause.'

$expectedCategories = @(
    'SessionRejectReasonNotAvailable',
    'MarketClosedOrSessionUnavailablePlausible',
    'PermissionSessionAccountRejectPlausible',
    'InstrumentSecurityMappingRejectPlausible',
    'MalformedOrUnsupportedMarketDataRequestPlausible',
    'SessionRejectReasonOtherSanitized')
$observedCategories = @($coverage.categories | ForEach-Object { $_.sanitizedReasonCategory })
Assert-True (($observedCategories -join ',') -eq ($expectedCategories -join ',')) 'Allowed sanitized category coverage is incomplete or reordered.'
Assert-True (@($coverage.categories | Where-Object { $_.coveredByFocusedTest -ne $true }).Count -eq 0) 'One or more sanitized categories are not covered by focused tests.'

Assert-True ($r131Fixture.exactCategoryFromArchivedR131Evidence -eq 'NotRehydratableFromR131Artifacts') 'R131-equivalent fixture does not preserve old artifact limitation.'
Assert-True ($r131Fixture.realR131RootCauseInferred -eq $false) 'R131 root cause was inferred from fixture.'
Assert-False $r131Fixture.rawRejectTextSerialized 'Raw reject text serialized in R131-equivalent fixture.'
Assert-False $r131Fixture.rawFixSerialized 'Raw FIX serialized in R131-equivalent fixture.'

Assert-True ($cli.r133CliReportingContractVerified -eq $true) 'R133 CLI reporting contract is not verified.'
Assert-True ($cli.cliFieldName -eq 'sessionRejectSanitizedReasonCategory') 'R133 CLI reporting field is missing.'
Assert-True ($artifact.r133ArtifactReportingContractVerified -eq $true) 'R133 artifact reporting contract is not verified.'
Assert-True ($artifact.artifactFieldName -eq 'sanitizedSessionRejectReasonCategory') 'R133 artifact reporting field is missing.'

Assert-True ($decision.marketDataRequestShapeRepairRecommended -eq $false) 'MarketDataRequest shape repair was selected from fixture evidence.'
Assert-True ($decision.permissionEntitlementEvidencePackageRecommended -eq $false) 'Permission/entitlement package was selected from fixture evidence.'
Assert-True ($decision.instrumentMappingAuditRecommended -eq $false) 'Instrument mapping audit was selected from fixture evidence.'
Assert-True ($decision.controlledRetryReadinessNext -eq $true) 'Controlled retry readiness next step is missing.'

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

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R136') 'Next phase recommendation is missing.'
Assert-True ($next.r136MustRemainNoExternal -eq $true) 'R136 no-external requirement is missing.'

Assert-True ($gate.buildResult -match '^PASS') 'Build evidence is missing.'
Assert-True ($gate.focusedTests -match '^PASS') 'Focused test evidence is missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit test evidence is missing.'
Assert-True ($gate.integrationTests -match '^PASS') 'Integration test evidence is missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R135_VALIDATION_PASS') 'Validator evidence is missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r135-*' -File |
    Where-Object { $_.Extension -in '.json', '.md' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX token serialized in R135 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R135_VALIDATION_PASS'
