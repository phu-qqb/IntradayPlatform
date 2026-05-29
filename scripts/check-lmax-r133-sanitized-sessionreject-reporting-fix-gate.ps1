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

$summary = Read-Json 'phase-lmax-r133-sanitized-sessionreject-reporting-fix.json'
$cli = Read-Json 'phase-lmax-r133-cli-reporting-evidence.json'
$artifact = Read-Json 'phase-lmax-r133-artifact-reporting-evidence.json'
$contract = Read-Json 'phase-lmax-r133-sanitized-reason-category-contract.json'
$sanitization = Read-Json 'phase-lmax-r133-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r133-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r133-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r133-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r133-gate-validation.json'

$r131 = Read-Json 'phase-lmax-r131-weekday-market-hours-activation.json'
$r132 = Read-Json 'phase-lmax-r132-weekday-runtime-reject-review.json'
Assert-True ($r131.classification -eq 'LMAX_R131_FAIL_MARKETDATA_RESPONSE_BOUNDARY') 'R131 evidence is missing or mismatched.'
Assert-True ($r131.attemptCount -eq 1) 'R131 attemptCount must remain 1.'
Assert-True ($r132.classification -eq 'LMAX_R132_PASS_WEEKDAY_RUNTIME_REJECT_REVIEW_REASON_REPORTING_FIX_RECOMMENDED_NO_EXTERNAL') 'R132 evidence is missing or mismatched.'

Assert-True ($summary.noExternal -eq $true) 'R133 must be no-external.'
Assert-False $summary.externalActivationAttempted 'R133 external activation attempt detected.'
Assert-False $summary.socketTlsFixMarketDataRuntimeActionAttempted 'R133 socket/TLS/FIX/MarketData runtime action detected.'
Assert-True ($summary.sanitizedReasonCategorySurfacedInCli -eq $true) 'Sanitized reason category is not surfaced in CLI evidence.'
Assert-True ($summary.sanitizedReasonCategorySurfacedInArtifacts -eq $true) 'Sanitized reason category is not surfaced in artifacts evidence.'
Assert-True ($cli.cliFieldName -eq 'sessionRejectSanitizedReasonCategory') 'CLI sanitized reason field is missing.'
Assert-True ($cli.cliSurfacesExactSanitizedReasonCategory -eq $true) 'CLI exact sanitized reason contract is missing.'
Assert-True ($artifact.artifactFieldName -eq 'sanitizedSessionRejectReasonCategory') 'Artifact sanitized reason field is missing.'
Assert-True ($artifact.artifactsCanStoreExactSanitizedReasonCategory -eq $true) 'Artifact exact sanitized reason contract is missing.'

$allowed = @(
    'SessionRejectReasonNotAvailable',
    'MarketClosedOrSessionUnavailablePlausible',
    'PermissionSessionAccountRejectPlausible',
    'InstrumentSecurityMappingRejectPlausible',
    'MalformedOrUnsupportedMarketDataRequestPlausible',
    'SessionRejectReasonOtherSanitized')
Assert-True ((@($contract.allowedSanitizedReasonCategories) -join ',') -eq ($allowed -join ',')) 'Allowed sanitized reason category contract changed.'
Assert-True ($contract.rawRejectTextAllowed -eq $false) 'Raw reject text must not be allowed.'
Assert-True ($contract.rawFixAllowed -eq $false) 'Raw FIX must not be allowed.'
Assert-True ($contract.credentialValuesReturned -eq $false) 'credentialValuesReturned must remain false.'

$program = Get-Content (Join-Path $root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/Program.cs') -Raw
$operation = Get-Content (Join-Path $root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualMarketDataRequestOperation.cs') -Raw
Assert-True ($program -match 'sessionRejectSanitizedReasonCategory') 'CLI output field is not present in Program.cs.'
Assert-True ($program -match 'SanitizedSessionRejectReasonCategory') 'CLI sanitized reason helper is not present in Program.cs.'
Assert-True ($operation -match 'response\.SanitizedReasonCategory') 'Operation does not carry sanitized reason category.'
Assert-False ($program -match '58=') 'Program.cs must not serialize raw reject tag 58.'

Assert-True ($sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned must remain false.'
Assert-False $sanitization.rawCredentialsSerialized 'Raw credentials were serialized.'
Assert-False $sanitization.rawEndpointValuesSerialized 'Raw endpoint values were serialized.'
Assert-False $sanitization.rawTlsMaterialSerialized 'Raw TLS material was serialized.'
Assert-False $sanitization.rawFixMessagesSerialized 'Raw FIX messages were serialized.'
Assert-False $sanitization.rawCompIdsSerialized 'Raw CompIDs were serialized.'
Assert-False $sanitization.rawSessionIdsSerialized 'Raw session identifiers were serialized.'
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
Assert-False $forbidden.pollingLoopIntroduced 'Polling loop was introduced.'
Assert-False $forbidden.serviceIntroduced 'Service was introduced.'
Assert-False $forbidden.replayIntroduced 'Replay was introduced.'
Assert-False $forbidden.shadowReplayIntroduced 'Shadow replay was introduced.'
Assert-True ($apiWorker.audit -eq 'PASS') 'API/Worker audit did not pass.'
Assert-True ($apiWorker.apiWorkerGatewayMode -eq 'FakeLmaxGatewayOnly') 'API/Worker FakeLmaxGatewayOnly regressed.'
Assert-True ($next.recommendedNextPhase -eq 'LMAX-R134') 'Next phase recommendation is missing.'

Assert-True ($gate.buildResult -match '^PASS') 'Build evidence is missing.'
Assert-True ($gate.focusedTests -match '^PASS') 'Focused test evidence is missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit test evidence is missing.'
Assert-True ($gate.integrationTests -match '^PASS') 'Integration test evidence is missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R133_VALIDATION_PASS') 'Validator evidence is missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r133-*' -File |
    Where-Object { $_.Extension -in '.json', '.md' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX token serialized in R133 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R133_VALIDATION_PASS'
