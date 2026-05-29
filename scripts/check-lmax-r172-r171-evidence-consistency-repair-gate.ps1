$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'

function Read-Json($name) {
    $path = Join-Path $artifactRoot $name
    if (!(Test-Path $path)) { throw "Missing artifact: $name" }
    Get-Content $path -Raw | ConvertFrom-Json
}

function Assert-True($condition, $message) { if (-not $condition) { throw $message } }
function Assert-False($condition, $message) { if ($condition) { throw $message } }

$repair = Read-Json 'phase-lmax-r172-r171-evidence-consistency-repair.json'
$rootCause = Read-Json 'phase-lmax-r172-state-propagation-root-cause.json'
$mapping = Read-Json 'phase-lmax-r172-end-to-end-path-mapping-review.json'
$implementation = Read-Json 'phase-lmax-r172-repair-implementation-evidence.json'
$tests = Read-Json 'phase-lmax-r172-end-to-end-test-evidence.json'
$contract = Read-Json 'phase-lmax-r172-cli-artifact-field-contract.json'
$resolution = Read-Json 'phase-lmax-r172-r171-inconsistency-resolution.json'
$decision = Read-Json 'phase-lmax-r172-next-action-decision-gate.json'
$sanitization = Read-Json 'phase-lmax-r172-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r172-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r172-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r172-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r172-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r171-mdupdatetype-required-after-state-propagation-activation.json',
    'phase-lmax-r171-state-propagation-evidence.json',
    'phase-lmax-r171-gate-validation.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "R171 evidence missing: $name"
}

$r171 = Read-Json 'phase-lmax-r171-mdupdatetype-required-after-state-propagation-activation.json'
$r171State = Read-Json 'phase-lmax-r171-state-propagation-evidence.json'
Assert-True ($r171.attemptCount -eq 1) 'R171 attemptCount must equal 1.'
Assert-True ($r171.classification -eq 'LMAX_R171_FAIL_STATE_REPORTING_FIELDS_MISSING_OR_INCONSISTENT') 'R171 inconsistent state classification missing.'
Assert-True ($r171State.marketDataBoundaryClassificationObserved -eq $true) 'R171 boundary classification not recorded.'
Assert-False $r171State.stateFieldsConsistentWithBoundaryEvidence 'R171 contradiction not preserved.'

Assert-True ($repair.classification -eq 'LMAX_R172_PASS_END_TO_END_STATE_PROPAGATION_REPAIR_IMPLEMENTED_NO_EXTERNAL') 'R172 classification mismatch.'
Assert-True ($repair.noExternal -eq $true) 'R172 must be no-external.'
Assert-False $repair.newExternalActivationAttempted 'New external activation attempt detected.'
Assert-False $repair.runtimeActionPerformed 'Runtime action detected.'
Assert-True ($repair.r171EvidenceReviewed -eq $true) 'R171 evidence not reviewed.'
Assert-True ($repair.r171AttemptCount -eq 1) 'R171 attemptCount invalid.'
Assert-True ($repair.r171StateContradictionReviewed -eq $true) 'R171 state contradiction not reviewed.'
Assert-True ($repair.rootCauseIdentified -eq $true) 'Root cause missing.'
Assert-True ($repair.repairImplemented -eq $true) 'Repair implementation missing.'
Assert-True ($repair.endToEndPropagationValidated -eq $true) 'End-to-end propagation validation missing.'
Assert-False $repair.liveRetryRecommendedNow 'Live retry must remain blocked before readiness.'

Assert-True ($rootCause.rootCausePresent -eq $true) 'Root-cause artifact missing.'
Assert-True ($rootCause.rootCauseCategory -eq 'InnerRealBoundedProviderDependencyCodecWrappersDroppedStateFields') 'Unexpected root cause.'
foreach ($lossPoint in @(
    'LmaxRealMarketDataFrameProvider.ReadMarketData',
    'LmaxRealReadOnlyMarketDataDependency.ReadMarketData',
    'LmaxExecutableReadOnlyMarketDataRequestCodec.RequestReadOnlyMarketData')) {
    Assert-True (@($rootCause.lossPoints) -contains $lossPoint) "Loss point missing: $lossPoint"
}
Assert-True ($rootCause.repairImplemented -eq $true) 'Root-cause repair not marked implemented.'

Assert-True ($mapping.endToEndPathMappingReviewPresent -eq $true) 'End-to-end path mapping review missing.'
foreach ($component in @(
    'LmaxReadOnlyActivationManualMarketDataRequestOperation',
    'LmaxRealMarketDataFrameProvider',
    'LmaxRealReadOnlyMarketDataDependency',
    'LmaxExecutableReadOnlyMarketDataRequestCodec',
    'LmaxExecutableReadOnlyMarketDataSessionClient',
    'LmaxRealReadOnlyMarketDataTransport',
    'LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter',
    'Program.cs CLI output')) {
    Assert-True (@($mapping.reviewedPath) -contains $component) "Mapping review component missing: $component"
}
Assert-False $mapping.rawFixSerialized 'Raw FIX serialized in mapping review.'
Assert-False $mapping.rawRejectTextSerialized 'Raw reject text serialized in mapping review.'

Assert-True ($implementation.repairImplementationEvidencePresent -eq $true) 'Repair implementation evidence missing.'
Assert-True ($implementation.repairImplemented -eq $true) 'Repair not implemented.'
foreach ($field in @(
    'MarketDataRequestWriteAttempted',
    'MarketDataRequestWriteSucceeded',
    'MarketDataRequestResponseReadAttempted',
    'MarketDataRequestReachedBoundedResponseClassification')) {
    Assert-True (@($implementation.stateFieldsPropagated) -contains $field) "Propagated state field missing: $field"
}

Assert-True ($tests.endToEndNoExternalTestEvidencePresent -eq $true) 'End-to-end no-external test evidence missing.'
Assert-True ($tests.focusedTestsResult -match '^PASS') 'Focused tests did not pass.'
Assert-True ($tests.unitTestsResult -match '^PASS') 'Unit tests did not pass.'
Assert-True ($tests.integrationTestsResult -match '^PASS') 'Integration tests did not pass.'

Assert-True ($contract.cliArtifactFieldContractPresent -eq $true) 'CLI/artifact field contract missing.'
foreach ($field in @(
    'marketDataRequestWriteAttempted',
    'marketDataRequestWriteSucceeded',
    'marketDataRequestResponseReadAttempted',
    'marketDataRequestReachedBoundedResponseClassification')) {
    Assert-True (@($contract.authoritativeExplicitFields) -contains $field) "Authoritative explicit field missing: $field"
}
Assert-False $contract.explicitFieldsCollapsedIntoLegacyFlag 'Explicit state fields still collapsed into legacy flag.'
Assert-True ($contract.cliOutputUsesExplicitFields -eq $true) 'CLI output explicit-field readiness missing.'
Assert-True ($contract.artifactOutputRequiresExplicitFields -eq $true) 'Artifact explicit-field contract missing.'
Assert-False $contract.rawFixAllowed 'Raw FIX allowed by contract.'
Assert-False $contract.rawRejectTextAllowed 'Raw reject text allowed by contract.'

Assert-True ($resolution.r171InconsistencyResolutionPresent -eq $true) 'R171 inconsistency resolution missing.'
Assert-True ($resolution.r171WasNotCleanRequestShapeEvidence -eq $true) 'R171 should be marked not clean request-shape evidence.'
Assert-True ($resolution.futureRunsExpectedToPropagateStateFields -eq $true) 'Future propagation expectation missing.'
Assert-True ($resolution.requiresNoExternalReadinessBeforeFutureRetry -eq $true) 'No-external readiness requirement missing.'

Assert-True ($decision.nextActionDecisionGatePresent -eq $true) 'Next action decision gate missing.'
Assert-True ($decision.selectedDecision -eq 'NoExternalEndToEndStateRepairedRetryReadinessGate') 'Unexpected next action decision.'
Assert-True ($decision.nextPhase -eq 'LMAX-R173') 'Next phase should be R173.'
Assert-False $decision.liveRetryRecommendedNow 'Decision gate must not recommend immediate live retry.'
Assert-True ($decision.liveRetryBlockedUntilR173Readiness -eq $true) 'Live retry not blocked until R173.'

Assert-True ($sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned must remain false.'
Assert-False $sanitization.rawFixSerialized 'Raw FIX serialized.'
Assert-False $sanitization.rawRejectTextSerialized 'Raw reject text serialized.'
Assert-False $sanitization.rawCredentialsSerialized 'Raw credentials serialized.'
Assert-False $sanitization.rawSessionIdentifiersSerialized 'Raw session IDs serialized.'
Assert-False $sanitization.rawCompIdsSerialized 'Raw CompIDs serialized.'
Assert-False $sanitization.rawEndpointSerialized 'Raw endpoint serialized.'
Assert-False $sanitization.rawTlsMaterialSerialized 'Raw TLS material serialized.'

Assert-True ($forbidden.audit -eq 'PASS') 'Forbidden actions audit did not pass.'
Assert-False $forbidden.newExternalActivationAttempted 'New external activation attempt detected.'
Assert-False $forbidden.socketOpened 'Socket opened.'
Assert-False $forbidden.tlsOpened 'TLS opened.'
Assert-False $forbidden.fixOpened 'FIX opened.'
Assert-False $forbidden.marketDataRuntimeActionPerformed 'MarketData runtime action performed.'
Assert-False $forbidden.apiStarted 'API started.'
Assert-False $forbidden.workerStarted 'Worker started.'
Assert-False $forbidden.ordersIntroduced 'Orders introduced.'
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

$universe = Read-Json 'phase-lmax-r171-approved-universe-preservation.json'
$usdJpy = Read-Json 'phase-lmax-r171-usdjpy-caveat-preservation.json'
Assert-True ($universe.approvedUniversePreserved -eq $true) 'Approved universe weakened.'
Assert-True ((@($universe.approvedInstruments) -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved universe changed.'
Assert-True ($usdJpy.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat weakened.'
Assert-True ($usdJpy.usdJpySecurityId -eq '4004') 'USDJPY SecurityID weakened.'
Assert-True ($usdJpy.usdJpySecurityIdSource -eq '8') 'USDJPY SecurityIDSource weakened.'

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R173') 'Next phase recommendation missing.'
Assert-True ($next.r173MustRemainNoExternal -eq $true) 'R173 no-external requirement missing.'
Assert-False $next.liveRetryRecommendedNow 'Next recommendation must not allow immediate live retry.'
Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.focusedTests -match '^PASS') 'Focused test evidence missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit test evidence missing.'
Assert-True ($gate.integrationTests -match '^PASS') 'Integration test evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R172_VALIDATION_PASS') 'Validator evidence missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r172-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R172 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R172_VALIDATION_PASS'
