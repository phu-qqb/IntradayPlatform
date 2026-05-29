$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'
$phase = 'LMAX-R150'
$targetPhase = 'LMAX-R151'
$selectedProfile = 'UltraMinimalSnapshotPlusUpdatesSymbolAndSecurityIdGbpusdSingleInstrument'
$nonSelectedProfiles = @(
    'UltraMinimalSnapshotPlusUpdatesSecurityIdOnlyGbpusdSingleInstrument',
    'UltraMinimalSnapshotPlusUpdatesSymbolOnlyGbpusdSingleInstrument',
    'RepairedSnapshotPlusUpdatesSecurityIdOnlyNonBatched',
    'LegacySnapshotOnlySymbolAndSecurityBatch')

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

$summary = Read-Json 'phase-lmax-r150-identifier-repair-retry-readiness.json'
$selected = Read-Json 'phase-lmax-r150-selected-identifier-profile-evidence.json'
$nonSelected = Read-Json 'phase-lmax-r150-non-selected-profiles-evidence.json'
$reservation = Read-Json 'phase-lmax-r150-next-activation-phase-reservation-decision.json'
$preflight = Read-Json 'phase-lmax-r150-r151-preflight-checklist.json'
$contract = Read-Json 'phase-lmax-r150-r151-expected-evidence-contract.json'
$reporting = Read-Json 'phase-lmax-r150-reporting-contract-readiness.json'
$universe = Read-Json 'phase-lmax-r150-approved-universe-preservation.json'
$usdjpy = Read-Json 'phase-lmax-r150-usdjpy-caveat-preservation.json'
$sanitization = Read-Json 'phase-lmax-r150-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r150-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r150-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r150-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r150-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r147-ultraminimal-gbpusd-activation.json',
    'phase-lmax-r148-ultraminimal-gbpusd-reject-review.json',
    'phase-lmax-r148-identifier-combination-decision.json',
    'phase-lmax-r149-identifier-combination-repair.json',
    'phase-lmax-r149-next-diagnostic-profile-selection-decision.json',
    'phase-lmax-r149-symbol-securityid-profile-contract.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "Required predecessor evidence missing: $name"
}

$r147 = Read-Json 'phase-lmax-r147-ultraminimal-gbpusd-activation.json'
$r148 = Read-Json 'phase-lmax-r148-ultraminimal-gbpusd-reject-review.json'
$r148Identifier = Read-Json 'phase-lmax-r148-identifier-combination-decision.json'
$r149 = Read-Json 'phase-lmax-r149-identifier-combination-repair.json'
$r149Selection = Read-Json 'phase-lmax-r149-next-diagnostic-profile-selection-decision.json'
$r149SymbolSecurity = Read-Json 'phase-lmax-r149-symbol-securityid-profile-contract.json'

Assert-True ($summary.phase -eq $phase) 'R150 summary phase mismatch.'
Assert-True ($summary.classification -eq 'LMAX_R150_PASS_IDENTIFIER_REPAIR_RETRY_READINESS_NO_EXTERNAL') 'R150 classification mismatch.'
Assert-True ($summary.noExternal -eq $true) 'R150 must remain no-external.'
Assert-False $summary.externalActivationAttempted 'R150 external activation detected.'
Assert-False $summary.socketTlsFixMarketDataRuntimeActionAttempted 'R150 socket/TLS/FIX/MarketData runtime action detected.'
Assert-False $summary.liveMarketDataRequestSent 'R150 live MarketDataRequest detected.'
Assert-False $summary.liveMarketDataResponseRead 'R150 live MarketDataResponse read detected.'
Assert-False $summary.apiWorkerStarted 'R150 API/Worker startup detected.'
Assert-False $summary.schedulerServicePollingStarted 'R150 scheduler/service/polling startup detected.'
Assert-False $summary.ordersTradingReplayShadowReplayIntroduced 'R150 forbidden order/trading/replay path detected.'

Assert-True ($r147.attemptCount -eq 1) 'R147 attemptCount must equal 1.'
Assert-True ($r147.classification -eq 'LMAX_R147_FAIL_SESSIONREJECT_WITH_SANITIZED_REASON_REPORTED') 'R147 evidence classification missing.'
Assert-True ($r148.classification -eq 'LMAX_R148_PASS_ULTRAMINIMAL_REJECT_REVIEW_IDENTIFIER_REPAIR_RECOMMENDED_NO_EXTERNAL') 'R148 identifier repair review missing.'
Assert-True ($r148Identifier.recommended -eq $true) 'R148 identifier repair recommendation missing.'
Assert-True ($r149.classification -eq 'LMAX_R149_PASS_IDENTIFIER_COMBINATION_REPAIR_READY_NO_EXTERNAL') 'R149 identifier repair evidence missing.'
Assert-True ($r149Selection.selectedNextDiagnosticProfile -eq $selectedProfile) 'R149 next diagnostic profile selection mismatch.'
Assert-True ($r149Selection.selectedProfileCount -eq 1) 'R149 selected profile count must be one.'
Assert-True ($r149SymbolSecurity.profileName -eq $selectedProfile) 'R149 Symbol+SecurityID profile contract missing.'

Assert-True ($reservation.reservationDecisionPresent -eq $true) 'Next activation reservation decision missing.'
Assert-True ($reservation.nextRealActivationPhase -eq $targetPhase) 'Next activation phase must be R151.'
Assert-True ($reservation.oddNumbered -eq $true) 'Next activation phase must be odd-numbered.'
Assert-True ($reservation.explicitlyReserved -eq $true) 'Next activation phase must be explicitly reserved.'
Assert-False $reservation.r150ExecutedActivation 'R150 must not execute activation.'
Assert-True ($reservation.r151RequiresFreshExactOperatorApproval -eq $true) 'R151 fresh approval constraint missing.'
Assert-True ($reservation.r151RequiresWeekdayActiveFxMarketDataAvailability -eq $true) 'R151 market-hours constraint missing.'
Assert-True ($reservation.r151RequiresExactlyOneBoundedAttempt -eq $true) 'R151 exactly-one-attempt constraint missing.'
Assert-True ($reservation.r151MustStopAfterAttempt -eq $true) 'R151 stop-after-attempt constraint missing.'

Assert-True ($selected.selectedIdentifierProfileEvidencePresent -eq $true) 'Selected identifier profile evidence missing.'
Assert-True ($selected.selectedFutureProfile -eq $selectedProfile) 'Selected future profile mismatch.'
Assert-True ($selected.selectedProfileCount -eq 1) 'More than one diagnostic profile selected.'
Assert-True ($selected.gbpusdOnly -eq $true) 'Future diagnostic retry is not GBPUSD-only.'
Assert-True ((@($selected.diagnosticRequestSymbols) -join ',') -eq 'GBPUSD') 'Future diagnostic request has wrong symbols.'
Assert-True ($selected.identifierCombination -eq 'SymbolPlusSecurityId') 'Identifier combination contract weakened.'
Assert-True ($selected.symbolIncluded -eq $true) 'Symbol identifier component missing.'
Assert-True ($selected.securityIdIncluded -eq $true) 'SecurityID identifier component missing.'
Assert-True ($selected.securityId -eq '4002') 'GBPUSD SecurityID mismatch.'
Assert-True ($selected.securityIdSourceIncluded -eq $true) 'SecurityIDSource missing.'
Assert-True ($selected.securityIdSource -eq '8') 'GBPUSD SecurityIDSource mismatch.'
Assert-False $selected.internalSymbolIncluded 'InternalSymbol included.'
Assert-True ($selected.snapshotPlusUpdates -eq $true) 'SnapshotPlusUpdates missing.'
Assert-False $selected.snapshotOnly 'SnapshotOnly returned.'
Assert-True ($selected.mdUpdateTypeProfileControlled -eq $true) 'MDUpdateType profile control missing.'
Assert-False $selected.mdUpdateTypeIncluded 'MDUpdateType included.'
Assert-True ($selected.marketDepth -eq 1) 'MarketDepth must equal one.'
Assert-True ((@($selected.mdEntryTypes) -join ',') -eq 'Bid,Offer') 'MDEntryTypes must be bid/offer only.'
Assert-True ($selected.oneRequestOnly -eq $true) 'Future diagnostic retry must use one request only.'
Assert-True ($selected.repeatingGroupFieldOrderSanitizedEvidencePresent -eq $true) 'Repeating-group field-order evidence missing.'
Assert-False $selected.rawFixSerialized 'Raw FIX serialized in selected profile evidence.'

Assert-True ($nonSelected.nonSelectedProfilesEvidencePresent -eq $true) 'Non-selected profile evidence missing.'
Assert-False $nonSelected.multipleDiagnosticProfilesSelected 'Multiple diagnostic profiles selected.'
foreach ($profile in $nonSelectedProfiles) {
    $match = @($nonSelected.profiles | Where-Object { $_.profileName -eq $profile })
    Assert-True ($match.Count -eq 1) "Non-selected profile evidence missing for $profile."
    Assert-False $match[0].selectedForR151 "Non-selected profile was selected for R151: $profile."
}

Assert-True ($preflight.targetPhase -eq $targetPhase) 'R151 preflight target mismatch.'
Assert-True ($preflight.freshExactOperatorApprovalRequired -eq $true) 'Fresh approval template missing.'
Assert-True ($preflight.weekdayActiveFxMarketDataAvailabilityRequired -eq $true) 'Weekday market-hours preflight missing.'
Assert-True ($preflight.exactlyOneBoundedAttemptRequired -eq $true) 'One bounded attempt preflight missing.'
Assert-True ($preflight.selectedProfile -eq $selectedProfile) 'Preflight selected profile mismatch.'
Assert-True ($preflight.selectedProfileCount -eq 1) 'Preflight selected profile count must be one.'
Assert-True ($preflight.gbpusdOnly -eq $true) 'Preflight GBPUSD-only missing.'
Assert-True ($preflight.symbolPlusSecurityIdRequired -eq $true) 'Preflight Symbol+SecurityID missing.'
Assert-True ($preflight.securityId -eq '4002') 'Preflight SecurityID mismatch.'
Assert-True ($preflight.securityIdSource -eq '8') 'Preflight SecurityIDSource mismatch.'
Assert-True ($preflight.internalSymbolForbidden -eq $true) 'Preflight InternalSymbol ban missing.'
Assert-True ($preflight.snapshotPlusUpdatesRequired -eq $true) 'Preflight SnapshotPlusUpdates missing.'
Assert-True ($preflight.snapshotOnlyForbidden -eq $true) 'Preflight SnapshotOnly ban missing.'
Assert-True ($preflight.mdUpdateTypeOmittedOrProfileControlled -eq $true) 'Preflight MDUpdateType control missing.'
Assert-True ($preflight.marketDepth -eq 1) 'Preflight MarketDepth mismatch.'
Assert-True ($preflight.bidOfferOnly -eq $true) 'Preflight bid/offer-only missing.'
Assert-True ($preflight.oneRequestOnly -eq $true) 'Preflight one-request constraint missing.'

Assert-True ($contract.expectedEvidenceContractPresent -eq $true) 'R151 expected evidence contract missing.'
Assert-True ($contract.marketDataRequestMustOccurAfterFixSuccess -eq $true) 'MarketDataRequest/FIX ordering contract missing.'
Assert-True ($contract.marketDataResponseReadMustOccurAfterMarketDataRequestSuccess -eq $true) 'MarketDataResponse/Request ordering contract missing.'
Assert-True ($contract.credentialValuesReturned -eq $false) 'Expected evidence contract must keep credentialValuesReturned false.'
Assert-True ($reporting.reportingContractReadinessPresent -eq $true) 'Reporting readiness missing.'
Assert-True ($reporting.r133CliReportingContractActive -eq $true) 'R133 CLI reporting readiness missing.'
Assert-True ($reporting.r135FixtureReportingVerificationPresent -eq $true) 'R135 fixture reporting verification missing.'
Assert-True ($reporting.cliField -eq 'sessionRejectSanitizedReasonCategory') 'CLI reporting field mismatch.'
Assert-True ($reporting.artifactField -eq 'sanitizedSessionRejectReasonCategory') 'Artifact reporting field mismatch.'

Assert-True ($universe.approvedUniversePreserved -eq $true) 'Approved universe preservation missing.'
Assert-True ((@($universe.approvedInstruments) -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved universe changed.'
Assert-True ((@($universe.futureDiagnosticRequestSymbols) -join ',') -eq 'GBPUSD') 'Future diagnostic request must remain GBPUSD-only.'
Assert-False $universe.nonApprovedInstrumentsAllowed 'Non-approved instruments allowed.'
Assert-True ($usdjpy.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat weakened.'
Assert-True ($usdjpy.usdJpySecurityId -eq '4004') 'USDJPY SecurityID weakened.'
Assert-True ($usdjpy.usdJpySecurityIdSource -eq '8') 'USDJPY SecurityIDSource weakened.'

Assert-True ($summary.credentialValuesReturned -eq $false) 'credentialValuesReturned must remain false.'
Assert-True ($sanitization.audit -eq 'PASS') 'Sanitization audit did not pass.'
Assert-True ($sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned audit must remain false.'
Assert-False $sanitization.rawFixSerialized 'Raw FIX serialized.'
Assert-False $sanitization.rawRejectTextSerialized 'Raw reject text serialized.'
Assert-False $sanitization.rawCredentialsSerialized 'Raw credentials serialized.'
Assert-False $sanitization.rawUsernameSerialized 'Raw username serialized.'
Assert-False $sanitization.rawPasswordSerialized 'Raw password serialized.'
Assert-False $sanitization.rawSessionIdentifiersSerialized 'Raw session identifiers serialized.'
Assert-False $sanitization.rawCompIdsSerialized 'Raw CompIDs serialized.'
Assert-False $sanitization.rawEndpointSerialized 'Raw endpoint serialized.'
Assert-False $sanitization.rawTlsMaterialSerialized 'Raw TLS material serialized.'

Assert-True ($forbidden.audit -eq 'PASS') 'Forbidden actions audit did not pass.'
Assert-False $forbidden.externalActivationAttempted 'External activation detected.'
Assert-False $forbidden.socketTlsFixMarketDataRuntimeActionAttempted 'Socket/TLS/FIX/MarketData runtime action detected.'
Assert-False $forbidden.liveMarketDataRequestSent 'Live MarketDataRequest detected.'
Assert-False $forbidden.liveMarketDataResponseRead 'Live MarketDataResponse read detected.'
Assert-False $forbidden.ordersIntroduced 'Order path introduced.'
Assert-False $forbidden.tradingEnabled 'Trading enabled.'
Assert-False $forbidden.schedulerIntroduced 'Scheduler introduced.'
Assert-False $forbidden.pollingLoopIntroduced 'Polling loop introduced.'
Assert-False $forbidden.serviceIntroduced 'Service introduced.'
Assert-False $forbidden.replayIntroduced 'Replay introduced.'
Assert-False $forbidden.shadowReplayIntroduced 'Shadow replay introduced.'
Assert-False $forbidden.tradingStateMutationIntroduced 'Trading state mutation introduced.'
Assert-False $forbidden.productionAccountAllowed 'Production account allowed.'
Assert-True ($apiWorker.audit -eq 'PASS') 'API/Worker audit did not pass.'
Assert-True ($apiWorker.apiWorkerGatewayMode -eq 'FakeLmaxGatewayOnly') 'API/Worker FakeLmaxGatewayOnly regressed.'
Assert-False $apiWorker.apiWorkerStarted 'API/Worker startup detected.'
Assert-False $apiWorker.identifierRepairProfileSelectedForApiWorker 'Identifier profile selected for API/Worker.'
Assert-False $apiWorker.liveGatewayRegisteredForApiWorker 'Live gateway registered for API/Worker.'

Assert-True ($next.recommendedNextPhase -eq $targetPhase) 'Next phase recommendation must be R151.'
Assert-True ($next.r151MustRequireFreshExactOperatorApproval -eq $true) 'R151 approval recommendation missing.'
Assert-True ($next.r151MustUseExactlyOneBoundedAttempt -eq $true) 'R151 exactly-one-attempt recommendation missing.'
Assert-True ($next.r152MustBeNoExternalReviewGateOnly -eq $true) 'R152 review/gate recommendation missing.'
Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.focusedTests -match '^PASS') 'Focused test evidence missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit test evidence missing.'
Assert-True ($gate.integrationTests -match '^PASS') 'Integration test evidence missing.'
Assert-True ($gate.validatorResult -match '^LMAX_R150_VALIDATION_PASS') 'Validator evidence missing.'

$reservationSourcePath = Join-Path $root 'src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxApprovedBoundedExecutableRetryPhaseReservations.cs'
$factoryPath = Join-Path $root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualExecutionSurfaceFactory.cs'
$reservationSource = Get-Content $reservationSourcePath -Raw
$factory = Get-Content $factoryPath -Raw
Assert-True ($reservationSource.Contains($targetPhase)) 'R151 is not explicitly reserved in source.'
Assert-True ($factory.Contains($selectedProfile)) 'Future manual real-bounded path does not select selected identifier profile.'

$approvalTemplatePath = Join-Path $artifactRoot 'phase-lmax-r150-r151-operator-approval-template.md'
Assert-True (Test-Path $approvalTemplatePath) 'R151 approval template missing.'
$approvalTemplate = Get-Content $approvalTemplatePath -Raw
Assert-True ($approvalTemplate.Contains('I, Philippe, explicitly approve Phase LMAX-R151')) 'R151 approval template text missing.'
Assert-True ($approvalTemplate.Contains($selectedProfile)) 'R151 approval template does not name selected profile.'
Assert-True ($approvalTemplate.Contains('exactly one bounded attempt')) 'R151 approval template missing one-attempt constraint.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r150-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R150 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R150_VALIDATION_PASS'
