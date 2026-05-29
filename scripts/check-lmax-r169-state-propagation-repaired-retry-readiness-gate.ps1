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

$readiness = Read-Json 'phase-lmax-r169-state-propagation-repaired-retry-readiness.json'
$selected = Read-Json 'phase-lmax-r169-selected-profile-evidence.json'
$state = Read-Json 'phase-lmax-r169-state-propagation-readiness.json'
$nonSelected = Read-Json 'phase-lmax-r169-non-selected-profiles-evidence.json'
$reservation = Read-Json 'phase-lmax-r169-next-activation-phase-reservation-decision.json'
$preflight = Read-Json 'phase-lmax-r169-r171-preflight-checklist.json'
$expected = Read-Json 'phase-lmax-r169-r171-expected-evidence-contract.json'
$reporting = Read-Json 'phase-lmax-r169-reporting-contract-readiness.json'
$universe = Read-Json 'phase-lmax-r169-approved-universe-preservation.json'
$usdJpy = Read-Json 'phase-lmax-r169-usdjpy-caveat-preservation.json'
$sanitization = Read-Json 'phase-lmax-r169-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r169-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r169-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r169-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r169-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r168-r167-state-propagation-review.json',
    'phase-lmax-r168-state-field-loss-root-cause.json',
    'phase-lmax-r168-gate-validation.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "R168 evidence missing: $name"
}

$r168 = Read-Json 'phase-lmax-r168-r167-state-propagation-review.json'
$r168Gate = Read-Json 'phase-lmax-r168-gate-validation.json'
Assert-True ($r168.classification -eq 'LMAX_R168_PASS_STATE_PROPAGATION_REPAIR_IMPLEMENTED_NO_EXTERNAL') 'R168 repair evidence missing.'
Assert-True ($r168.repairImplemented -eq $true) 'R168 repair not confirmed.'
Assert-True ($r168Gate.validatorResult -eq 'LMAX_R168_VALIDATION_PASS') 'R168 validator evidence missing.'

$reservationSource = Get-Content (Join-Path $root 'src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxApprovedBoundedExecutableRetryPhaseReservations.cs') -Raw
Assert-True ($reservationSource.Contains('LMAX-R171')) 'LMAX-R171 is not explicitly reserved in source.'

Assert-True ($readiness.classification -eq 'LMAX_R169_PASS_STATE_PROPAGATION_REPAIRED_RETRY_READINESS_NO_EXTERNAL') 'R169 classification mismatch.'
Assert-True ($readiness.noExternal -eq $true) 'R169 must be no-external.'
Assert-False $readiness.newExternalActivationAttempted 'New external activation attempt detected.'
Assert-False $readiness.runtimeActionPerformed 'Runtime action detected.'
Assert-True ($readiness.r168EvidenceReviewed -eq $true) 'R168 evidence review missing.'
Assert-True ($readiness.nextActivationPhase -eq 'LMAX-R171') 'Next activation phase must be R171.'
Assert-True ($readiness.nextActivationPhaseOddNumbered -eq $true) 'Next activation phase must be odd-numbered.'
Assert-True ($readiness.nextActivationPhaseExplicitlyReserved -eq $true) 'Next activation phase not explicitly reserved.'
Assert-True ($readiness.selectedFutureProfile -eq 'UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument') 'Unexpected future profile.'
Assert-True ($readiness.selectedDiagnosticProfileCount -eq 1) 'More than one diagnostic profile selected.'
Assert-True ($readiness.requiresFreshExactOperatorApproval -eq $true) 'Fresh approval requirement missing.'
Assert-True ($readiness.requiresWeekdayActiveFxMarketDataAvailability -eq $true) 'Weekday market-hours requirement missing.'
Assert-True ($readiness.requiresExactlyOneBoundedAttempt -eq $true) 'Single bounded attempt requirement missing.'
Assert-True ($readiness.requiresImmediateStopAfterAttempt -eq $true) 'Immediate stop requirement missing.'

Assert-True ($selected.selectedProfileEvidencePresent -eq $true) 'Selected profile evidence missing.'
Assert-True ($selected.selectedFutureProfile -eq 'UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument') 'Selected profile mismatch.'
Assert-True ($selected.selectedDiagnosticProfileCount -eq 1) 'Multiple diagnostic profiles selected.'
Assert-True ($selected.gbpusdOnly -eq $true) 'Future retry is not GBPUSD-only.'
Assert-True ($selected.singleRequest -eq $true) 'Future retry has more than one request.'
Assert-True ($selected.securityId -eq '4002') 'SecurityID=4002 missing.'
Assert-True ($selected.securityIdSource -eq '8') 'SecurityIDSource=8 missing.'
Assert-True ($selected.subscriptionRequestType -eq '1') 'SubscriptionRequestType=1 missing.'
Assert-True ($selected.mdUpdateType -eq '0') 'MDUpdateType=0 missing.'
Assert-True ($selected.marketDepth -eq 1) 'MarketDepth=1 missing.'
Assert-True ($selected.noMdEntryTypes -eq 2) 'NoMDEntryTypes=2 missing.'
Assert-True ($selected.bidAndOfferTogether -eq $true) 'Bid+Offer together evidence missing.'
Assert-False $selected.symbolTextPresent 'Symbol text present.'
Assert-False $selected.internalSymbolPresent 'InternalSymbol present.'
Assert-False $selected.snapshotOnlyPresent 'SnapshotOnly present.'
Assert-False $selected.subscriptionRequestTypeZeroPresent 'SubscriptionRequestType=0 present.'

Assert-True ($state.statePropagationReadinessPresent -eq $true) 'R168 state propagation readiness missing.'
Assert-True ($state.r168RepairConfirmed -eq $true) 'R168 repair not confirmed in readiness.'
foreach ($field in @(
    'marketDataRequestWriteAttempted',
    'marketDataRequestWriteSucceeded',
    'marketDataRequestResponseReadAttempted',
    'marketDataRequestReachedBoundedResponseClassification',
    'marketDataRequestSentLegacyFlag')) {
    Assert-True (@($state.explicitStateFieldsRequiredInNextRun) -contains $field) "State field missing from readiness: $field"
    Assert-True (@($expected.requiredExplicitStateFields) -contains $field) "State field missing from expected evidence contract: $field"
}
Assert-True ($state.futureEvidenceMustNotCollapseWriteReadAndClassification -eq $true) 'Write/read/classification separation missing.'

Assert-True ($nonSelected.nonSelectedProfilesEvidencePresent -eq $true) 'Non-selected profile evidence missing.'
Assert-True ($nonSelected.priorProfilesPreservedAsEvidenceOnly -eq $true) 'Prior profile preservation missing.'
Assert-False $nonSelected.multipleDiagnosticProfilesSelected 'Multiple diagnostic profiles selected.'

Assert-True ($reservation.nextActivationPhaseReservationDecisionPresent -eq $true) 'Reservation decision missing.'
Assert-True ($reservation.nextActivationPhase -eq 'LMAX-R171') 'Reservation phase mismatch.'
Assert-True ($reservation.oddNumbered -eq $true) 'Reservation is not odd-numbered.'
Assert-True ($reservation.explicitlyReserved -eq $true) 'R171 not explicitly reserved.'
Assert-True ($reservation.reservationImplementedInCode -eq $true) 'Reservation implementation missing.'
Assert-True ($reservation.reservationTested -eq $true) 'Reservation test evidence missing.'
Assert-False $reservation.evenNumberedActivationRetryAllowed 'Even-numbered activation retry allowed.'
Assert-False $reservation.r170ActivationReserved 'R170 activation should not be reserved.'

Assert-True (Test-Path (Join-Path $artifactRoot 'phase-lmax-r169-r171-operator-approval-template.md')) 'Fresh approval template missing.'
Assert-True ($preflight.preflightChecklistPresent -eq $true) 'Preflight checklist missing.'
Assert-False $preflight.r169ExecutedRetry 'R169 executed retry.'
Assert-True ($expected.expectedEvidenceContractPresent -eq $true) 'Expected evidence contract missing.'
Assert-False $expected.rawFixAllowed 'Raw FIX allowed.'
Assert-False $expected.rawRejectTextAllowed 'Raw reject text allowed.'
Assert-False $expected.rawCredentialOrSessionMaterialAllowed 'Raw credential/session material allowed.'

Assert-True ($reporting.reportingContractReadinessPresent -eq $true) 'Reporting readiness missing.'
Assert-True ($reporting.r133R135SanitizedReasonReportingPreserved -eq $true) 'R133/R135 reporting readiness missing.'
Assert-True ($reporting.sessionRejectSanitizedReasonCategoryRequiredWhenSessionRejectObserved -eq $true) 'sessionRejectSanitizedReasonCategory readiness missing.'
Assert-True ($reporting.sanitizedSessionRejectReasonCategoryRequiredWhenSessionRejectObserved -eq $true) 'sanitizedSessionRejectReasonCategory readiness missing.'
Assert-True ($reporting.r163R168ExplicitStateReportingRequired -eq $true) 'R163/R168 explicit state reporting readiness missing.'

Assert-True ($universe.approvedUniversePreserved -eq $true) 'Approved universe preservation missing.'
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

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R171') 'Next phase recommendation missing.'
Assert-True ($next.r171RequiresFreshExplicitOperatorApproval -eq $true) 'R171 fresh approval missing.'
Assert-True ($next.r171RequiresWeekdayActiveFxMarketDataAvailability -eq $true) 'R171 market-hours constraint missing.'
Assert-True ($next.r171RequiresExactlyOneBoundedAttemptOnly -eq $true) 'R171 single attempt constraint missing.'
Assert-True ($next.r172MustBeNoExternalReviewGate -eq $true) 'R172 no-external review requirement missing.'
Assert-False $next.liveRetryExecutedInR169 'R169 executed live retry.'

Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.focusedTests -match '^PASS') 'Focused test evidence missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit test evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R169_VALIDATION_PASS') 'Validator evidence missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r169-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R169 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R169_VALIDATION_PASS'
