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

$readiness = Read-Json 'phase-lmax-r179-final-state-evidence-repaired-retry-readiness.json'
$selected = Read-Json 'phase-lmax-r179-selected-profile-evidence.json'
$stateContract = Read-Json 'phase-lmax-r179-final-state-evidence-contract-readiness.json'
$nonSelected = Read-Json 'phase-lmax-r179-non-selected-profiles-evidence.json'
$reservation = Read-Json 'phase-lmax-r179-next-activation-phase-reservation-decision.json'
$preflight = Read-Json 'phase-lmax-r179-r181-preflight-checklist.json'
$expected = Read-Json 'phase-lmax-r179-r181-expected-evidence-contract.json'
$reporting = Read-Json 'phase-lmax-r179-reporting-contract-readiness.json'
$universe = Read-Json 'phase-lmax-r179-approved-universe-preservation.json'
$usdJpy = Read-Json 'phase-lmax-r179-usdjpy-caveat-preservation.json'
$sanitization = Read-Json 'phase-lmax-r179-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r179-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r179-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r179-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r179-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r178-r177-state-finalization-review.json',
    'phase-lmax-r178-final-emission-root-cause.json',
    'phase-lmax-r178-state-field-provenance-contract.json',
    'phase-lmax-r178-final-consistency-rule.json',
    'phase-lmax-r178-repair-implementation-evidence.json',
    'phase-lmax-r178-final-artifact-emission-test-evidence.json',
    'phase-lmax-r178-gate-validation.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "R178 evidence missing: $name"
}

$r178 = Read-Json 'phase-lmax-r178-r177-state-finalization-review.json'
$r178Consistency = Read-Json 'phase-lmax-r178-final-consistency-rule.json'
$r178Provenance = Read-Json 'phase-lmax-r178-state-field-provenance-contract.json'
$r178Tests = Read-Json 'phase-lmax-r178-final-artifact-emission-test-evidence.json'
$r178Gate = Read-Json 'phase-lmax-r178-gate-validation.json'

Assert-True ($r178.classification -eq 'LMAX_R178_PASS_FINAL_STATE_EVIDENCE_CONTRACT_REPAIR_IMPLEMENTED_NO_EXTERNAL') 'R178 classification mismatch.'
Assert-True ($r178.finalEvidenceContractValidated -eq $true) 'R178 final evidence contract validation missing.'
Assert-False $r178Consistency.observedResponseCategoryCanEmitAllFalseWithoutExplanation 'R178 consistency rule still allows classified all-false state.'
Assert-False $r178Consistency.legacyFlagAuthoritative 'R178 still treats legacy flag as authoritative.'
Assert-True ($r178Provenance.stateFieldProvenanceContractPresent -eq $true) 'R178 provenance contract missing.'
Assert-True ($r178Tests.rejectClassificationCannotEmitAllExplicitStateFieldsFalse -eq $true) 'R178 final artifact emission test evidence missing.'
Assert-True ($r178Gate.validatorResult -eq 'LMAX_R178_VALIDATION_PASS') 'R178 validator evidence missing.'

$reservationSource = Get-Content (Join-Path $root 'src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxApprovedBoundedExecutableRetryPhaseReservations.cs') -Raw
Assert-True ($reservationSource.Contains('"LMAX-R181"')) 'LMAX-R181 is not explicitly reserved in source.'
Assert-True ($reservationSource.Contains('LMAX-R181')) 'LMAX-R181 missing from reservation rule text.'

Assert-True ($readiness.classification -eq 'LMAX_R179_PASS_FINAL_STATE_EVIDENCE_REPAIRED_RETRY_READINESS_NO_EXTERNAL') 'R179 classification mismatch.'
Assert-True ($readiness.noExternal -eq $true) 'R179 must be no-external.'
Assert-False $readiness.newExternalActivationAttempted 'New external activation attempt detected.'
Assert-False $readiness.runtimeActionPerformed 'Runtime action detected.'
Assert-True ($readiness.r178EvidenceReviewed -eq $true) 'R178 evidence review missing.'
Assert-True ($readiness.r178FinalStateEvidenceContractValidated -eq $true) 'R178 final-state contract review missing.'
Assert-True ($readiness.nextActivationPhase -eq 'LMAX-R181') 'Next activation phase must be R181.'
Assert-True ($readiness.nextActivationPhaseOddNumbered -eq $true) 'Next activation phase must be odd-numbered.'
Assert-True ($readiness.nextActivationPhaseExplicitlyReserved -eq $true) 'Next activation phase not explicitly reserved.'
Assert-True ($readiness.selectedFutureProfile -eq 'UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument') 'Selected future profile mismatch.'
Assert-True ($readiness.selectedDiagnosticProfileCount -eq 1) 'Multiple diagnostic profiles selected.'
Assert-False $readiness.liveRetryExecutedInR179 'R179 executed a live retry.'

Assert-True ($selected.selectedProfileEvidencePresent -eq $true) 'Selected profile evidence missing.'
Assert-True ($selected.selectedFutureProfile -eq 'UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument') 'Selected profile mismatch.'
Assert-True ($selected.gbpusdOnly -eq $true) 'Future retry is not GBPUSD-only.'
Assert-True ($selected.singleRequest -eq $true) 'Future retry has more than one request.'
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

Assert-True ($stateContract.finalStateEvidenceContractReadinessPresent -eq $true) 'Final-state evidence readiness missing.'
Assert-True ($stateContract.r178RepairReviewed -eq $true) 'R178 repair review missing.'
Assert-True ($stateContract.r178ProviderClientSanitizerRepairConfirmed -eq $true) 'R178 provider-client repair confirmation missing.'
Assert-True ($stateContract.legacyFlagCompatibilityOnly -eq $true) 'Legacy flag compatibility-only decision missing.'
Assert-False $stateContract.classifiedMarketDataResponseAllFalseStateAllowed 'Classified MarketDataResponse with all false state is allowed.'
Assert-True ($stateContract.futureArtifactsMustFailOnClassifiedResponseWithAllExplicitStateFieldsFalse -eq $true) 'Future artifact all-false classified response failure rule missing.'
Assert-True ($stateContract.missingWriterEvidenceMustBePropagatedOrExplicitUnknown -eq $true) 'Missing writer evidence handling rule missing.'

foreach ($field in @(
    'marketDataRequestWriteAttempted',
    'marketDataRequestWriteSucceeded',
    'marketDataRequestResponseReadAttempted',
    'marketDataRequestReachedBoundedResponseClassification',
    'marketDataRequestSentLegacyFlag')) {
    Assert-True (@($stateContract.requiredExplicitStateFields) -contains $field) "State contract missing field: $field"
    Assert-True (@($expected.requiredExplicitStateFields) -contains $field) "Expected evidence contract missing field: $field"
}

Assert-True ($expected.expectedEvidenceContractPresent -eq $true) 'Expected evidence contract missing.'
Assert-True ($expected.stateFieldProvenanceOrFinalizationEvidenceRequired -eq $true) 'State provenance/finalization evidence requirement missing.'
Assert-False $expected.classifiedMarketDataResponseAllFalseStateAllowed 'Expected contract allows classified response with all false state.'
Assert-False $expected.legacySentFlagAuthoritative 'Expected contract treats legacy sent flag as authoritative.'
Assert-False $expected.rawFixAllowed 'Raw FIX allowed.'
Assert-False $expected.rawRejectTextAllowed 'Raw reject text allowed.'
Assert-False $expected.rawCredentialOrSessionMaterialAllowed 'Raw credential/session material allowed.'

Assert-True ($nonSelected.nonSelectedProfilesEvidencePresent -eq $true) 'Non-selected profile evidence missing.'
Assert-True ($nonSelected.priorProfilesPreservedAsEvidenceOnly -eq $true) 'Prior profiles not preserved as evidence.'
Assert-False $nonSelected.multipleDiagnosticProfilesSelected 'Multiple diagnostic profiles selected.'

Assert-True ($reservation.nextActivationPhaseReservationDecisionPresent -eq $true) 'Reservation decision missing.'
Assert-True ($reservation.nextActivationPhase -eq 'LMAX-R181') 'Reservation phase mismatch.'
Assert-True ($reservation.oddNumbered -eq $true) 'R181 must be odd-numbered.'
Assert-True ($reservation.explicitlyReserved -eq $true) 'R181 not explicitly reserved.'
Assert-True ($reservation.reservationImplementedInCode -eq $true) 'Reservation implementation missing.'
Assert-True ($reservation.reservationTested -eq $true) 'Reservation test evidence missing.'
Assert-False $reservation.evenNumberedActivationRetryAllowed 'Even-numbered activation retry allowed.'
Assert-False $reservation.liveRetryExecutedInR179 'R179 executed live retry.'

Assert-True (Test-Path (Join-Path $artifactRoot 'phase-lmax-r179-r181-operator-approval-template.md')) 'R181 approval template missing.'
Assert-True (Test-Path (Join-Path $artifactRoot 'phase-lmax-r179-r181-activation-prompt-compact.md')) 'R181 compact activation prompt missing.'
Assert-True ($preflight.preflightChecklistPresent -eq $true) 'Preflight checklist missing.'
Assert-True ($preflight.freshExactOperatorApprovalRequired -eq $true) 'Fresh approval requirement missing.'
Assert-True ($preflight.separateConcreteWeekdayMarketHoursConfirmationRequired -eq $true) 'Concrete market-hours requirement missing.'
Assert-True ($preflight.placeholderMarketHoursTimeRejected -eq $true) 'Placeholder market-hours rejection missing.'
Assert-True ($preflight.exactlyOneBoundedAttemptRequired -eq $true) 'Exactly-one attempt requirement missing.'
Assert-True ($preflight.immediateStopAfterAttemptRequired -eq $true) 'Immediate stop requirement missing.'
Assert-True ($preflight.r182ReviewGateOnlyAfterAttempt -eq $true) 'R182 review gate requirement missing.'
Assert-False $preflight.r179ExecutedRetry 'R179 executed retry.'

Assert-True ($reporting.reportingContractReadinessPresent -eq $true) 'Reporting readiness missing.'
Assert-True ($reporting.r133R135SanitizedReasonReportingPreserved -eq $true) 'R133/R135 reporting readiness missing.'
Assert-True ($reporting.sessionRejectSanitizedReasonCategoryRequired -eq $true) 'sessionRejectSanitizedReasonCategory readiness missing.'
Assert-True ($reporting.sanitizedSessionRejectReasonCategoryRequired -eq $true) 'sanitizedSessionRejectReasonCategory readiness missing.'
Assert-True ($reporting.r178FinalStateEvidenceReportingRequired -eq $true) 'R178 final state reporting readiness missing.'
Assert-True ($reporting.classifiedResponseCannotSilentlyEmitAllFalseState -eq $true) 'Classified-response consistency reporting rule missing.'
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

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R181') 'Next phase recommendation must be R181.'
Assert-True ($next.r181RequiresFreshExplicitOperatorApproval -eq $true) 'R181 fresh approval requirement missing.'
Assert-True ($next.r181RequiresSeparateConcreteWeekdayMarketHoursConfirmation -eq $true) 'R181 concrete market-hours requirement missing.'
Assert-True ($next.r181RequiresExactlyOneBoundedAttemptOnly -eq $true) 'R181 exactly-one bounded attempt requirement missing.'
Assert-True ($next.r182MustBeNoExternalReviewGate -eq $true) 'R182 no-external review gate requirement missing.'
Assert-False $next.liveRetryExecutedInR179 'R179 executed live retry.'

Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.focusedTests -match '^PASS') 'Focused test evidence missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit test evidence missing.'
Assert-True ($gate.integrationTests -match '^PASS') 'Integration test evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R179_VALIDATION_PASS') 'Validator evidence missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r179-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' -and $_.Name -notlike '*operator-approval-template*' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R179 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R179_VALIDATION_PASS'
