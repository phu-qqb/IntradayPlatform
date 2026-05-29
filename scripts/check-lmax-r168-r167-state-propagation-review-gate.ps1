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

$review = Read-Json 'phase-lmax-r168-r167-state-propagation-review.json'
$rootCause = Read-Json 'phase-lmax-r168-state-field-loss-root-cause.json'
$mapping = Read-Json 'phase-lmax-r168-executable-path-mapping-review.json'
$decision = Read-Json 'phase-lmax-r168-repair-decision-gate.json'
$testPlan = Read-Json 'phase-lmax-r168-test-plan.json'
$sanitization = Read-Json 'phase-lmax-r168-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r168-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r168-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r168-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r168-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r167-after-state-repair-activation.json',
    'phase-lmax-r167-state-reporting-evidence.json',
    'phase-lmax-r167-gate-validation.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "R167 evidence missing: $name"
}

$r167 = Read-Json 'phase-lmax-r167-after-state-repair-activation.json'
$r167State = Read-Json 'phase-lmax-r167-state-reporting-evidence.json'
Assert-True ($r167.attemptCount -eq 1) 'R167 attemptCount must equal 1.'
Assert-True ($r167.classification -eq 'LMAX_R167_FAIL_STATE_REPORTING_FIELDS_MISSING') 'R167 state-field failure evidence missing.'
Assert-True ($r167State.marketDataBoundaryClassificationObserved -eq $true) 'R167 boundary classification evidence missing.'
Assert-True ($r167State.stateReportingFieldsSemanticallyValid -eq $false) 'R167 contradiction not preserved.'

Assert-True ($review.classification -eq 'LMAX_R168_PASS_STATE_PROPAGATION_REPAIR_IMPLEMENTED_NO_EXTERNAL') 'R168 classification mismatch.'
Assert-True ($review.noExternal -eq $true) 'R168 must be no-external.'
Assert-False $review.newExternalActivationAttempted 'New external activation attempt detected.'
Assert-False $review.runtimeActionPerformed 'Runtime action detected.'
Assert-True ($review.r167EvidenceReviewed -eq $true) 'R167 evidence review missing.'
Assert-True ($review.r167AttemptCount -eq 1) 'R167 attemptCount invalid.'
Assert-True ($review.r167ContradictionReviewed -eq $true) 'R167 state-field contradiction not reviewed.'
Assert-True ($review.r167BoundaryClassificationObserved -eq $true) 'R167 boundary classification missing.'
Assert-True ($review.r167StateFieldsAllFalse -eq $true) 'R167 false state fields not reviewed.'
Assert-True ($review.rootCauseIdentified -eq $true) 'State-field loss root cause missing.'
Assert-True ($review.repairImplemented -eq $true) 'R168 repair implementation missing.'
Assert-False $review.liveRetryRecommendedNow 'Live retry must remain blocked before readiness gate.'

Assert-True ($rootCause.rootCausePresent -eq $true) 'Root cause artifact missing.'
Assert-True ($rootCause.rootCauseCategory -eq 'ExecutableSessionClientWrapperDroppedCodecStateFields') 'Unexpected root cause.'
Assert-True ($rootCause.losingComponent -eq 'LmaxExecutableReadOnlyMarketDataSessionClient.RequestReadOnlyMarketData') 'Losing component not identified.'
Assert-True ($rootCause.repairImplemented -eq $true) 'Root-cause repair not confirmed.'

Assert-True ($mapping.executablePathMappingReviewPresent -eq $true) 'Executable path mapping review missing.'
foreach ($component in @(
    'LmaxReadOnlyActivationManualMarketDataRequestOperation',
    'LmaxReadOnlyActivationManualTcpSocketConnector',
    'LmaxExecutableReadOnlyMarketDataSessionClient',
    'LmaxRealReadOnlyMarketDataTransport',
    'LmaxTemporaryReadOnlyRuntimeAdapterPath',
    'LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter',
    'Program.cs CLI/artifact emission')) {
    Assert-True (@($mapping.reviewedComponents) -contains $component) "Executable path review missing component: $component"
}
Assert-False $mapping.rawFixSerialized 'Raw FIX serialized in mapping review.'
Assert-False $mapping.rawRejectTextSerialized 'Raw reject text serialized in mapping review.'

Assert-True ($decision.repairDecisionGatePresent -eq $true) 'Repair decision gate missing.'
Assert-True ($decision.selectedDecision -eq 'NoExternalStatePropagationRepairImplemented') 'Unexpected repair decision.'
Assert-True ($decision.repairImplementedInR168 -eq $true) 'Repair not marked implemented.'
Assert-True ($decision.nextPhase -eq 'LMAX-R169') 'Next phase should be R169.'
Assert-True ($decision.liveRetryBlockedUntilReadinessGate -eq $true) 'Live retry not blocked until readiness.'
Assert-False $decision.liveRetryRecommendedNow 'Decision gate must not allow immediate live retry.'

Assert-True ($testPlan.testsOrRepairPlanPresent -eq $true) 'Tests/repair plan missing.'
Assert-True ($testPlan.repairImplemented -eq $true) 'Test plan does not reflect implemented repair.'
Assert-True ($testPlan.focusedTestTarget -eq 'LmaxExecutableReadOnlyMarketDataSessionClientTests') 'Focused test target missing.'
Assert-True ($testPlan.focusedTestsResult -match '^PASS') 'Focused test evidence missing.'
Assert-True ($testPlan.unitTestsResult -match '^PASS') 'Unit test evidence missing.'

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

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R169') 'Next phase recommendation must be R169.'
Assert-True ($next.r169MustRemainNoExternal -eq $true) 'R169 no-external requirement missing.'
Assert-False $next.liveRetryRecommendedNow 'Next recommendation must not allow immediate live retry.'
Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.focusedTests -match '^PASS') 'Focused test evidence missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit test evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R168_VALIDATION_PASS') 'Validator evidence missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r168-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R168 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R168_VALIDATION_PASS'
