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

$repair = Read-Json 'phase-lmax-r163-request-sent-flag-consistency-repair.json'
$contract = Read-Json 'phase-lmax-r163-state-field-contract.json'
$legacy = Read-Json 'phase-lmax-r163-legacy-flag-compatibility-decision.json'
$r161 = Read-Json 'phase-lmax-r163-r161-inconsistency-resolution.json'
$cli = Read-Json 'phase-lmax-r163-cli-reporting-evidence.json'
$artifact = Read-Json 'phase-lmax-r163-artifact-reporting-evidence.json'
$tests = Read-Json 'phase-lmax-r163-test-coverage-evidence.json'
$sanitization = Read-Json 'phase-lmax-r163-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r163-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r163-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r163-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r163-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r161-mdupdatetype-required-gbpusd-activation.json',
    'phase-lmax-r162-next-repair-decision-gate.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "Required prior evidence missing: $name"
}

Assert-True ($repair.classification -eq 'LMAX_R163_PASS_MARKETDATAREQUEST_SENT_FLAG_CONSISTENCY_REPAIR_NO_EXTERNAL') 'R163 classification mismatch.'
Assert-True ($repair.noExternal -eq $true) 'R163 must be no-external.'
Assert-False $repair.newExternalActivationAttempted 'New external activation attempt detected.'
Assert-False $repair.runtimeActionPerformed 'Runtime action detected.'
Assert-True ($repair.repairImplemented -eq $true) 'Repair not implemented.'
Assert-True ($repair.marketDataRequestSentAmbiguityResolved -eq $true) 'marketDataRequestSent ambiguity not resolved.'
Assert-True ($repair.requestWriteAndResponseClassificationSeparated -eq $true) 'Request write and response classification still collapsed.'
Assert-True ($repair.legacyFlagPreservedForCompatibility -eq $true) 'Legacy flag compatibility missing.'

Assert-True ($contract.stateFieldContractPresent -eq $true) 'State-field contract missing.'
foreach ($field in @(
    'marketDataRequestWriteAttempted',
    'marketDataRequestWriteSucceeded',
    'marketDataRequestResponseReadAttempted',
    'marketDataRequestReachedBoundedResponseClassification')) {
    Assert-True (@($contract.authoritativeFields) -contains $field) "Authoritative state field missing: $field"
}
Assert-True ($contract.legacyCompatibilityField -eq 'marketDataRequestSentLegacyFlag') 'Legacy compatibility field missing.'
Assert-True ($contract.requestWriteDistinctFromResponseClassification -eq $true) 'Request write not distinct from response classification.'
Assert-True ($contract.marketDataRequestSentAmbiguityResolved -eq $true) 'Sent ambiguity unresolved in contract.'

Assert-True ($legacy.legacyFlagCompatibilityDecisionPresent -eq $true) 'Legacy flag decision missing.'
Assert-True ($legacy.legacyFlagPreserved -eq $true) 'Legacy flag not preserved.'
Assert-True ($legacy.authoritativeForFutureEvidence -eq $false) 'Legacy flag must not be authoritative.'

Assert-True ($r161.r161InconsistencyResolutionPresent -eq $true) 'R161 inconsistency resolution missing.'
Assert-True ($r161.r161HistoricalMarketDataRequestSentFlag -eq $false) 'R161 historical flag mismatch.'
Assert-True ($r161.oldInconsistencyFixedForFutureRuns -eq $true) 'R161 inconsistency unresolved for future runs.'
Assert-False $r161.liveRetryRecommendedByR163 'R163 must not recommend live retry.'

Assert-True ($cli.cliReportingEvidencePresent -eq $true) 'CLI reporting evidence missing.'
foreach ($field in @(
    'marketDataRequestSent',
    'marketDataRequestSentLegacyFlag',
    'marketDataRequestWriteAttempted',
    'marketDataRequestWriteSucceeded',
    'marketDataRequestResponseReadAttempted',
    'marketDataRequestReachedBoundedResponseClassification')) {
    Assert-True (@($cli.reportedFields) -contains $field) "CLI reported field missing: $field"
}
Assert-False $cli.requestWriteAndResponseClassificationCollapsed 'CLI still collapses request write and response classification.'
Assert-False $cli.rawFixSerialized 'Raw FIX serialized in CLI evidence.'
Assert-False $cli.rawRejectTextSerialized 'Raw reject text serialized in CLI evidence.'

Assert-True ($artifact.artifactReportingEvidencePresent -eq $true) 'Artifact reporting evidence missing.'
Assert-False $artifact.requestWriteAndResponseClassificationCollapsed 'Artifacts still collapse request write and response classification.'
Assert-False $artifact.rawFixSerialized 'Raw FIX serialized in artifact evidence.'
Assert-False $artifact.rawRejectTextSerialized 'Raw reject text serialized in artifact evidence.'

Assert-True ($tests.testCoverageEvidencePresent -eq $true) 'Test coverage evidence missing.'
foreach ($case in @(
    'successful write plus reject response produces distinct write/read/classification flags',
    'no write attempted produces no response read or bounded classification',
    'write failure does not collapse into response classification',
    'pre-external blocked snapshot leaves external/write/read flags false',
    'legacy flag is preserved but non-authoritative',
    'raw FIX and raw reject text are not serialized')) {
    Assert-True (@($tests.coveredCases) -contains $case) "Required test coverage case missing: $case"
}
Assert-True ($tests.focusedTests -match '^PASS') 'Focused test evidence missing.'
Assert-True ($tests.unitTests -match '^PASS') 'Unit test evidence missing.'
Assert-True ($tests.integrationTests -match '^PASS') 'Integration test evidence missing.'

Assert-True ($sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned must remain false.'
Assert-False $sanitization.rawFixSerialized 'Raw FIX serialized.'
Assert-False $sanitization.rawRejectTextSerialized 'Raw reject text serialized.'
Assert-False $sanitization.rawCredentialsSerialized 'Raw credentials serialized.'
Assert-False $sanitization.rawSessionIdentifiersSerialized 'Raw session identifiers serialized.'
Assert-False $sanitization.rawCompIdsSerialized 'Raw CompIDs serialized.'
Assert-False $sanitization.rawEndpointSerialized 'Raw endpoint serialized.'
Assert-False $sanitization.rawTlsMaterialSerialized 'Raw TLS material serialized.'

Assert-True ($forbidden.audit -eq 'PASS') 'Forbidden actions audit did not pass.'
Assert-False $forbidden.newExternalActivationAttempted 'New external activation attempt detected.'
Assert-False $forbidden.socketOpened 'Socket opened.'
Assert-False $forbidden.tlsOpened 'TLS opened.'
Assert-False $forbidden.fixOpened 'FIX opened.'
Assert-False $forbidden.marketDataRuntimeActionPerformed 'MarketData runtime action performed.'
Assert-False $forbidden.ordersIntroduced 'Order path introduced.'
Assert-False $forbidden.tradingEnabled 'Trading enabled.'
Assert-False $forbidden.schedulerIntroduced 'Scheduler introduced.'
Assert-False $forbidden.pollingLoopIntroduced 'Polling introduced.'
Assert-False $forbidden.serviceIntroduced 'Service introduced.'
Assert-False $forbidden.replayIntroduced 'Replay introduced.'
Assert-False $forbidden.shadowReplayIntroduced 'Shadow replay introduced.'

Assert-True ($apiWorker.audit -eq 'PASS') 'API/Worker audit did not pass.'
Assert-True ($apiWorker.apiWorkerGatewayMode -eq 'FakeLmaxGatewayOnly') 'API/Worker FakeLmaxGatewayOnly regressed.'
Assert-False $apiWorker.apiStartupAttempted 'API startup attempted.'
Assert-False $apiWorker.workerStartupAttempted 'Worker startup attempted.'

$universe = Read-Json 'phase-lmax-r161-approved-universe-preservation.json'
$usdJpy = Read-Json 'phase-lmax-r161-usdjpy-caveat-preservation.json'
Assert-True ($universe.approvedUniversePreserved -eq $true) 'Approved universe weakened.'
Assert-True ((@($universe.approvedInstruments) -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved universe changed.'
Assert-True ($usdJpy.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat weakened.'
Assert-True ($usdJpy.usdJpySecurityId -eq '4004') 'USDJPY SecurityID weakened.'
Assert-True ($usdJpy.usdJpySecurityIdSource -eq '8') 'USDJPY SecurityIDSource weakened.'

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R164') 'Next phase recommendation must be R164.'
Assert-True ($next.r164MustRemainNoExternal -eq $true) 'R164 no-external requirement missing.'
Assert-False $next.liveRetryRecommended 'R163 must not recommend live retry.'
Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.focusedTests -match '^PASS') 'Focused tests evidence missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit tests evidence missing.'
Assert-True ($gate.integrationTests -match '^PASS') 'Integration tests evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R163_VALIDATION_PASS') 'Validator result missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r163-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R163 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R163_VALIDATION_PASS'
