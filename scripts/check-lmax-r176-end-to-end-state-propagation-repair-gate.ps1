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

$review = Read-Json 'phase-lmax-r176-end-to-end-state-propagation-review.json'
$rootCause = Read-Json 'phase-lmax-r176-state-field-loss-root-cause.json'
$mapping = Read-Json 'phase-lmx-r176-executable-path-mapping-review.json'
$repair = Read-Json 'phase-lmx-r176-repair-implementation-evidence.json'
$tests = Read-Json 'phase-lmx-r176-end-to-end-test-evidence.json'
$contract = Read-Json 'phase-lmx-r176-cli-artifact-field-contract.json'
$resolution = Read-Json 'phase-lmx-r176-r175-inconsistency-resolution.json'
$decision = Read-Json 'phase-lmx-r176-next-action-decision-gate.json'
$sanitization = Read-Json 'phase-lmx-r176-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmx-r176-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmx-r176-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmx-r176-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r176-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r175-mdupdatetype-required-after-end-to-end-state-propagation-activation.json',
    'phase-lmax-r175-gate-validation.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "Required R175 evidence missing: $name"
}

$r175 = Read-Json 'phase-lmax-r175-mdupdatetype-required-after-end-to-end-state-propagation-activation.json'
Assert-True ($r175.attemptCount -eq 1) 'R175 attempt count must be exactly 1.'
Assert-True ($r175.stateReportingFieldsConsistentWithBoundaryEvidence -eq $false) 'R175 contradiction not represented.'

Assert-True ($review.noExternal -eq $true) 'R176 must be no-external.'
Assert-False $review.liveActivationPerformed 'R176 live activation detected.'
Assert-True ($review.classification -eq 'LMAX_R176_PASS_END_TO_END_STATE_PROPAGATION_REPAIR_READY_FOR_R177_NO_EXTERNAL') 'Unexpected R176 classification.'
Assert-True ($review.r175ContradictionReviewed -eq $true) 'R175 contradiction not reviewed.'
Assert-True ($review.rootCauseIdentified -eq $true) 'Root cause missing.'
Assert-True ($review.repairImplemented -eq $true) 'Repair implementation missing.'
Assert-True ($review.nextRetryPrepared -eq $true) 'Next retry preparation missing.'

Assert-True ($rootCause.responsibleComponent -eq 'LmaxRealReadOnlyMarketDataFrameBoundaryProvider.Sanitize') 'Unexpected root cause component.'
foreach ($field in @(
    'MarketDataRequestWriteAttempted',
    'MarketDataRequestWriteSucceeded',
    'MarketDataRequestResponseReadAttempted',
    'MarketDataRequestReachedBoundedResponseClassification')) {
    Assert-True (@($rootCause.lostFields) -contains $field) "Root cause missing lost field: $field"
}

Assert-True ($mapping.mappingReviewed -eq $true) 'Executable path mapping review missing.'
Assert-True (@($mapping.path) -contains 'LmaxRealReadOnlyMarketDataFrameBoundaryProvider') 'Mapping review missing boundary provider.'
Assert-True ($mapping.programCliOutputUsesExplicitFields -eq $true) 'CLI explicit field output not confirmed.'

Assert-True ($repair.repairImplemented -eq $true) 'Repair implementation evidence missing.'
Assert-True ($repair.stateFieldsCopiedByBoundaryProviderSanitizer -eq $true) 'Boundary provider state copy evidence missing.'
Assert-True ($repair.r177Reserved -eq $true) 'R177 reservation missing.'
Assert-False $repair.liveRetryExecutedInR176 'R176 live retry detected.'

Assert-True ($tests.focusedTests -eq 'PASS 77/77') 'Focused test evidence missing.'
Assert-True ($tests.writeAttemptedTrueWhenRequestWriteAttempted -eq $true) 'writeAttempted test evidence missing.'
Assert-True ($tests.writeSucceededTrueWhenWriteSucceeds -eq $true) 'writeSucceeded test evidence missing.'
Assert-True ($tests.responseReadAttemptedTrueWhenBoundedReadRuns -eq $true) 'responseReadAttempted test evidence missing.'
Assert-True ($tests.boundedResponseClassificationTrueWhenClassifierProducesCategory -eq $true) 'classification test evidence missing.'
Assert-True ($tests.preExternalBlockedPathKeepsFieldsFalse -eq $true) 'pre-external blocked test evidence missing.'

Assert-True ($contract.cliArtifactFieldContractReady -eq $true) 'CLI/artifact field contract missing.'
foreach ($field in @(
    'marketDataRequestWriteAttempted',
    'marketDataRequestWriteSucceeded',
    'marketDataRequestResponseReadAttempted',
    'marketDataRequestReachedBoundedResponseClassification')) {
    Assert-True (@($contract.authoritativeExplicitStateFields) -contains $field) "Explicit state field missing from contract: $field"
}
Assert-True ($contract.legacyFlagAuthoritative -eq $false) 'Legacy flag remains authoritative.'
Assert-True ($contract.artifactsMustUseExplicitFieldsForR177Evidence -eq $true) 'R177 explicit field contract missing.'

Assert-True ($resolution.r175InconsistencyReviewed -eq $true) 'R175 inconsistency resolution missing.'
Assert-True ($resolution.r175WasNotCleanRequestShapeEvidence -eq $true) 'R175 clean evidence caveat missing.'

Assert-True ($decision.nextActivationPhase -eq 'LMAX-R177') 'R177 decision missing.'
Assert-True ($decision.nextActivationPhaseOddNumbered -eq $true) 'R177 odd-number evidence missing.'
Assert-True ($decision.nextActivationPhaseExplicitlyReserved -eq $true) 'R177 explicit reservation missing.'
Assert-True ($decision.selectedProfile -eq 'UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument') 'Selected R177 profile mismatch.'
Assert-True ($decision.selectedProfileCount -eq 1) 'Selected profile count must be exactly 1.'
Assert-False $decision.liveRetryRecommendedWithoutFreshApproval 'Live retry allowed without fresh approval.'
Assert-True ($decision.r178ReviewGateAfterAttempt -eq $true) 'R178 review gate missing.'

Assert-True ($sanitization.audit -eq 'PASS') 'Sanitization audit did not pass.'
Assert-True ($sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned must be false.'
Assert-False $sanitization.rawFixSerialized 'Raw FIX serialized.'
Assert-False $sanitization.rawRejectTextSerialized 'Raw reject text serialized.'
Assert-False $sanitization.rawCredentialsSerialized 'Raw credentials serialized.'
Assert-False $sanitization.rawSessionIdentifiersSerialized 'Raw session IDs serialized.'
Assert-False $sanitization.rawCompIdsSerialized 'Raw CompIDs serialized.'
Assert-False $sanitization.rawEndpointSerialized 'Raw endpoint serialized.'
Assert-False $sanitization.rawTlsMaterialSerialized 'Raw TLS material serialized.'

Assert-True ($forbidden.audit -eq 'PASS') 'Forbidden actions audit did not pass.'
Assert-False $forbidden.externalActivationAttempted 'External activation detected in R176.'
Assert-False $forbidden.socketOpened 'Socket opened in R176.'
Assert-False $forbidden.tlsOpened 'TLS opened in R176.'
Assert-False $forbidden.fixOpened 'FIX opened in R176.'
Assert-False $forbidden.marketDataRuntimeActionPerformed 'MarketData runtime action performed in R176.'
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

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R177') 'Next phase recommendation missing.'
Assert-True ($next.freshExactOperatorApprovalRequired -eq $true) 'Fresh approval requirement missing.'
Assert-True ($next.concreteWeekdayMarketHoursConfirmationRequired -eq $true) 'Market-hours requirement missing.'
Assert-True ($next.exactlyOneBoundedAttemptOnly -eq $true) 'Exactly-one attempt requirement missing.'
Assert-True ($next.stopAfterAttemptForR178ReviewGate -eq $true) 'R178 stop/review requirement missing.'

$reservationSource = Get-Content (Join-Path $root 'src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxApprovedBoundedExecutableRetryPhaseReservations.cs') -Raw
Assert-True ($reservationSource.Contains('"LMAX-R177"')) 'R177 source reservation missing.'

$boundaryProviderSource = Get-Content (Join-Path $root 'src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxRealReadOnlyMarketDataFrameBoundaryProvider.cs') -Raw
foreach ($field in @(
    'MarketDataRequestWriteAttempted = result.MarketDataRequestWriteAttempted',
    'MarketDataRequestWriteSucceeded = result.MarketDataRequestWriteSucceeded',
    'MarketDataRequestResponseReadAttempted = result.MarketDataRequestResponseReadAttempted',
    'MarketDataRequestReachedBoundedResponseClassification = result.MarketDataRequestReachedBoundedResponseClassification')) {
    Assert-True ($boundaryProviderSource.Contains($field)) "Boundary provider repair missing: $field"
}

Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.focusedTests -eq 'PASS 77/77') 'Focused test evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R176_VALIDATION_PASS') 'Validator evidence missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lm*x-r176-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R176 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R176_VALIDATION_PASS'
