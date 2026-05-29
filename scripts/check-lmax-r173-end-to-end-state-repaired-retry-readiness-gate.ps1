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

$readiness = Read-Json 'phase-lmax-r173-end-to-end-state-repaired-retry-readiness.json'
$selected = Read-Json 'phase-lmax-r173-selected-profile-evidence.json'
$state = Read-Json 'phase-lmax-r173-state-propagation-evidence.json'
$nonSelected = Read-Json 'phase-lmax-r173-non-selected-profiles-evidence.json'
$reservation = Read-Json 'phase-lmax-r173-next-activation-phase-reservation-decision.json'
$preflight = Read-Json 'phase-lmax-r173-preflight-checklist.json'
$expected = Read-Json 'phase-lmax-r173-expected-evidence-contract.json'
$reporting = Read-Json 'phase-lmax-r173-reporting-contract-readiness.json'
$universe = Read-Json 'phase-lmax-r173-approved-universe-preservation.json'
$usdJpy = Read-Json 'phase-lmax-r173-usdjpy-caveat-preservation.json'
$sanitization = Read-Json 'phase-lmax-r173-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r173-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r173-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r173-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r173-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r172-r171-evidence-consistency-repair.json',
    'phase-lmax-r172-end-to-end-test-evidence.json',
    'phase-lmax-r172-gate-validation.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "R172 evidence missing: $name"
}

$r172 = Read-Json 'phase-lmax-r172-r171-evidence-consistency-repair.json'
$r172Tests = Read-Json 'phase-lmax-r172-end-to-end-test-evidence.json'
$r172Gate = Read-Json 'phase-lmax-r172-gate-validation.json'
Assert-True ($r172.classification -eq 'LMAX_R172_PASS_END_TO_END_STATE_PROPAGATION_REPAIR_IMPLEMENTED_NO_EXTERNAL') 'R172 repair classification missing.'
Assert-True ($r172.endToEndPropagationValidated -eq $true) 'R172 end-to-end propagation validation missing.'
Assert-True ($r172Tests.focusedTestsResult -match '^PASS') 'R172 focused test evidence missing.'
Assert-True ($r172Gate.validatorResult -eq 'LMAX_R172_VALIDATION_PASS') 'R172 validator evidence missing.'

$reservationSource = Get-Content (Join-Path $root 'src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxApprovedBoundedExecutableRetryPhaseReservations.cs') -Raw
Assert-True ($reservationSource.Contains('LMAX-R175')) 'LMAX-R175 is not explicitly reserved in source.'

Assert-True ($readiness.classification -eq 'LMAX_R173_PASS_END_TO_END_STATE_PROPAGATION_REPAIRED_RETRY_READINESS_NO_EXTERNAL') 'R173 classification mismatch.'
Assert-True ($readiness.noExternal -eq $true) 'R173 must be no-external.'
Assert-False $readiness.newExternalActivationAttempted 'New external activation attempt detected.'
Assert-False $readiness.runtimeActionPerformed 'Runtime action detected.'
Assert-True ($readiness.r172EvidenceReviewed -eq $true) 'R172 evidence review missing.'
Assert-True ($readiness.r172EndToEndPropagationValidated -eq $true) 'R172 propagation validation missing.'
Assert-True ($readiness.nextActivationPhase -eq 'LMAX-R175') 'Next activation phase must be R175.'
Assert-True ($readiness.nextActivationPhaseOddNumbered -eq $true) 'Next activation phase must be odd-numbered.'
Assert-True ($readiness.nextActivationPhaseExplicitlyReserved -eq $true) 'Next activation phase not explicitly reserved.'
Assert-True ($readiness.selectedFutureProfile -eq 'UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument') 'Selected future profile mismatch.'
Assert-True ($readiness.selectedDiagnosticProfileCount -eq 1) 'Multiple diagnostic profiles selected.'
Assert-False $readiness.liveRetryExecutedInR173 'R173 executed a live retry.'

Assert-True ($selected.selectedProfileEvidencePresent -eq $true) 'Selected profile evidence missing.'
Assert-True ($selected.selectedFutureProfile -eq 'UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument') 'Selected profile mismatch.'
Assert-True ($selected.gbpusdOnly -eq $true) 'Profile not GBPUSD-only.'
Assert-True ($selected.singleRequest -eq $true) 'Profile not single request.'
Assert-True ($selected.securityId -eq '4002') 'SecurityID=4002 missing.'
Assert-True ($selected.securityIdSource -eq '8') 'SecurityIDSource=8 missing.'
Assert-True ($selected.subscriptionRequestType -eq '1') 'SubscriptionRequestType=1 missing.'
Assert-True ($selected.mdUpdateType -eq '0') 'MDUpdateType=0 missing.'
Assert-True ($selected.marketDepth -eq 1) 'MarketDepth=1 missing.'
Assert-True ($selected.noMdEntryTypes -eq 2) 'NoMDEntryTypes=2 missing.'
Assert-True ($selected.bidAndOfferTogether -eq $true) 'Bid+Offer together missing.'
Assert-False $selected.symbolTextPresent 'Symbol text present.'
Assert-False $selected.internalSymbolPresent 'InternalSymbol present.'
Assert-False $selected.snapshotOnlyPresent 'SnapshotOnly present.'
Assert-False $selected.subscriptionRequestTypeZeroPresent 'SubscriptionRequestType=0 present.'

Assert-True ($state.statePropagationEvidencePresent -eq $true) 'State propagation evidence missing.'
Assert-True ($state.r172EndToEndWrapperRepairConfirmed -eq $true) 'R172 repair confirmation missing.'
Assert-True ($state.endToEndNoExternalTestPassed -eq $true) 'End-to-end test pass missing.'
foreach ($field in @(
    'marketDataRequestWriteAttempted',
    'marketDataRequestWriteSucceeded',
    'marketDataRequestResponseReadAttempted',
    'marketDataRequestReachedBoundedResponseClassification',
    'marketDataRequestSentLegacyFlag')) {
    Assert-True (@($state.requiredExplicitStateFields) -contains $field) "State field missing in readiness: $field"
    Assert-True (@($expected.requiredExplicitStateFields) -contains $field) "State field missing in expected evidence contract: $field"
}
Assert-False $state.explicitFieldsCollapsedIntoLegacyFlag 'Explicit fields are still collapsed into legacy flag.'
Assert-True ($state.legacyFlagCompatibilityOnly -eq $true) 'Legacy flag compatibility decision missing.'

Assert-True ($nonSelected.nonSelectedProfilesEvidencePresent -eq $true) 'Non-selected profile evidence missing.'
Assert-True ($nonSelected.priorProfilesPreservedAsEvidenceOnly -eq $true) 'Prior profiles not preserved as evidence.'
Assert-False $nonSelected.multipleDiagnosticProfilesSelected 'Multiple diagnostic profiles selected.'

Assert-True ($reservation.nextActivationPhaseReservationDecisionPresent -eq $true) 'Reservation decision missing.'
Assert-True ($reservation.nextActivationPhase -eq 'LMAX-R175') 'Reservation phase mismatch.'
Assert-True ($reservation.oddNumbered -eq $true) 'Reservation must be odd-numbered.'
Assert-True ($reservation.explicitlyReserved -eq $true) 'R175 not explicitly reserved.'
Assert-True ($reservation.reservationImplementedInCode -eq $true) 'Reservation not implemented.'
Assert-True ($reservation.reservationTested -eq $true) 'Reservation test evidence missing.'
Assert-False $reservation.evenNumberedActivationRetryAllowed 'Even-numbered activation retry allowed.'

Assert-True (Test-Path (Join-Path $artifactRoot 'phase-lmax-r173-r175-operator-approval-template.md')) 'R175 approval template missing.'
Assert-True ($preflight.preflightChecklistPresent -eq $true) 'Preflight checklist missing.'
Assert-True ($preflight.freshExactOperatorApprovalRequired -eq $true) 'Fresh approval requirement missing.'
Assert-True ($preflight.weekdayActiveFxMarketDataAvailabilityRequired -eq $true) 'Market-hours requirement missing.'
Assert-True ($preflight.exactlyOneBoundedAttemptRequired -eq $true) 'Single attempt requirement missing.'
Assert-False $preflight.r173ExecutedRetry 'R173 executed retry.'

Assert-True ($expected.expectedEvidenceContractPresent -eq $true) 'Expected evidence contract missing.'
Assert-False $expected.rawFixAllowed 'Raw FIX allowed.'
Assert-False $expected.rawRejectTextAllowed 'Raw reject text allowed.'
Assert-False $expected.rawCredentialOrSessionMaterialAllowed 'Raw credential/session material allowed.'

Assert-True ($reporting.reportingContractReadinessPresent -eq $true) 'Reporting readiness missing.'
Assert-True ($reporting.r133R135SanitizedReasonReportingPreserved -eq $true) 'R133/R135 reporting readiness missing.'
Assert-True ($reporting.r163R168R172ExplicitStateReportingRequired -eq $true) 'Explicit state reporting readiness missing.'
Assert-False $reporting.rawRejectTextSerialized 'Raw reject text serialized.'

Assert-True ($universe.approvedUniversePreserved -eq $true) 'Approved universe missing.'
Assert-True ((@($universe.approvedInstruments) -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved universe weakened.'
Assert-True ($universe.diagnosticRetryIsGbpusdOnly -eq $true) 'Diagnostic retry not GBPUSD-only.'
Assert-False $universe.nonApprovedInstrumentsAllowed 'Non-approved instruments allowed.'
Assert-True ($usdJpy.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat weakened.'
Assert-True ($usdJpy.usdJpySecurityId -eq '4004') 'USDJPY SecurityID weakened.'
Assert-True ($usdJpy.usdJpySecurityIdSource -eq '8') 'USDJPY SecurityIDSource weakened.'

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

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R175') 'Next phase recommendation must be R175.'
Assert-True ($next.r175RequiresFreshExplicitOperatorApproval -eq $true) 'R175 fresh approval requirement missing.'
Assert-True ($next.r175RequiresWeekdayActiveFxMarketDataAvailability -eq $true) 'R175 market-hours requirement missing.'
Assert-True ($next.r175RequiresExactlyOneBoundedAttemptOnly -eq $true) 'R175 single attempt requirement missing.'
Assert-True ($next.r176MustBeNoExternalReviewGate -eq $true) 'R176 review gate requirement missing.'
Assert-False $next.liveRetryExecutedInR173 'R173 executed live retry.'

Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.focusedTests -match '^PASS') 'Focused test evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R173_VALIDATION_PASS') 'Validator evidence missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r173-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R173 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R173_VALIDATION_PASS'
