$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'
$phase = 'LMAX-R154'
$targetPhase = 'LMAX-R155'
$profileName = 'UltraMinimalSnapshotPlusUpdatesFreshLifecycleSymbolAndSecurityIdGbpusdSingleInstrument'

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

$summary = Read-Json 'phase-lmax-r154-subscription-lifecycle-diagnostic-retry-readiness.json'
$profile = Read-Json 'phase-lmax-r154-r153-profile-selection-evidence.json'
$prior = Read-Json 'phase-lmax-r154-prior-profiles-preservation.json'
$reservation = Read-Json 'phase-lmax-r154-next-activation-phase-reservation-decision.json'
$preflight = Read-Json 'phase-lmax-r154-r155-preflight-checklist.json'
$contract = Read-Json 'phase-lmax-r154-r155-expected-evidence-contract.json'
$reporting = Read-Json 'phase-lmax-r154-reporting-contract-readiness.json'
$universe = Read-Json 'phase-lmax-r154-approved-universe-preservation.json'
$usdjpy = Read-Json 'phase-lmax-r154-usdjpy-caveat-preservation.json'
$sanitization = Read-Json 'phase-lmax-r154-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r154-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r154-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r154-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r154-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r153-subscriptionrequesttype-lifecycle-repair.json',
    'phase-lmax-r153-lifecycle-profile-contract.json',
    'phase-lmax-r153-selected-profile-decision.json',
    'phase-lmax-r153-gate-validation.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "Required predecessor evidence missing: $name"
}

$r153 = Read-Json 'phase-lmax-r153-subscriptionrequesttype-lifecycle-repair.json'
$r153Contract = Read-Json 'phase-lmax-r153-lifecycle-profile-contract.json'
$r153Selection = Read-Json 'phase-lmax-r153-selected-profile-decision.json'
$r153Gate = Read-Json 'phase-lmax-r153-gate-validation.json'

Assert-True ($r153.classification -eq 'LMAX_R153_PASS_SUBSCRIPTION_LIFECYCLE_REPAIR_READY_NO_EXTERNAL') 'R153 repair evidence missing.'
Assert-True ($r153Contract.profileName -eq $profileName) 'R153 lifecycle profile contract mismatch.'
Assert-True ($r153Selection.selectedNextDiagnosticProfile -eq $profileName) 'R153 selected profile mismatch.'
Assert-True ($r153Selection.selectedProfileCount -eq 1) 'R153 selected profile count must be one.'
Assert-True ($r153Gate.validatorResult -eq 'LMAX_R153_VALIDATION_PASS') 'R153 validator evidence missing.'

Assert-True ($summary.phase -eq $phase) 'R154 summary phase mismatch.'
Assert-True ($summary.classification -eq 'LMAX_R154_PASS_SUBSCRIPTION_LIFECYCLE_DIAGNOSTIC_RETRY_READINESS_NO_EXTERNAL') 'R154 classification mismatch.'
Assert-True ($summary.noExternal -eq $true) 'R154 must remain no-external.'
Assert-False $summary.externalActivationAttempted 'R154 external activation detected.'
Assert-False $summary.socketTlsFixMarketDataRuntimeActionAttempted 'R154 socket/TLS/FIX/MarketData runtime action detected.'
Assert-False $summary.liveMarketDataRequestSent 'R154 live MarketDataRequest detected.'
Assert-False $summary.liveMarketDataResponseRead 'R154 live MarketDataResponse read detected.'
Assert-False $summary.apiWorkerStarted 'R154 API/Worker startup detected.'
Assert-False $summary.schedulerServicePollingStarted 'R154 scheduler/service/polling startup detected.'
Assert-False $summary.ordersTradingReplayShadowReplayIntroduced 'R154 order/trading/replay path detected.'
Assert-True ($summary.nextActivationPhase -eq $targetPhase) 'R154 next activation phase mismatch.'
Assert-True ($summary.nextActivationPhaseOddNumbered -eq $true) 'R155 must be odd-numbered.'
Assert-True ($summary.nextActivationPhaseExplicitlyReserved -eq $true) 'R155 explicit reservation missing.'
Assert-True ($summary.selectedFutureProfile -eq $profileName) 'R154 selected future profile mismatch.'
Assert-True ($summary.selectedProfileCount -eq 1) 'R154 must select exactly one future profile.'

Assert-True ($profile.r153ProfileSelectionPreserved -eq $true) 'R153 profile selection preservation missing.'
Assert-True ($profile.selectedFutureProfile -eq $profileName) 'Profile selection evidence mismatch.'
Assert-True ($profile.selectedProfileCount -eq 1) 'Profile selection count must be one.'
Assert-True ($profile.gbpusdOnly -eq $true) 'Future diagnostic retry is not GBPUSD-only.'
Assert-True ((@($profile.diagnosticRequestSymbols) -join ',') -eq 'GBPUSD') 'Future diagnostic symbols mismatch.'
Assert-True ($profile.identifierCombination -eq 'SymbolPlusSecurityId') 'Identifier combination weakened.'
Assert-True ($profile.symbolIncluded -eq $true) 'Symbol component missing.'
Assert-True ($profile.securityId -eq '4002') 'SecurityID mismatch.'
Assert-True ($profile.securityIdSource -eq '8') 'SecurityIDSource mismatch.'
Assert-False $profile.internalSymbolIncluded 'InternalSymbol included.'
Assert-True ($profile.snapshotPlusUpdates -eq $true) 'SnapshotPlusUpdates missing.'
Assert-False $profile.snapshotOnly 'SnapshotOnly returned.'
Assert-True ($profile.freshLifecycleMdReqIdCategory -eq 'LMAX_READONLY_R153') 'Fresh lifecycle MDReqID category missing.'
Assert-True ($profile.mdUpdateTypeProfileControlled -eq $true) 'MDUpdateType profile control missing.'
Assert-False $profile.mdUpdateTypeIncluded 'MDUpdateType included.'
Assert-True ($profile.marketDepth -eq 1) 'MarketDepth must equal one.'
Assert-True ((@($profile.mdEntryTypes) -join ',') -eq 'Bid,Offer') 'MDEntryTypes must be bid/offer only.'
Assert-True ($profile.oneRequestOnly -eq $true) 'Future diagnostic retry must use one request only.'
Assert-True ($profile.repeatingGroupFieldOrderSanitizedEvidencePresent -eq $true) 'Repeating-group/field-order evidence missing.'
Assert-False $profile.rawFixSerialized 'Raw FIX serialized in profile evidence.'

Assert-True ($prior.priorProfilesPreserved -eq $true) 'Prior profiles preservation missing.'
Assert-False $prior.priorProfilesSelectedForR155 'Prior profiles should not be selected for R155.'
Assert-True (@($prior.profiles).Contains('UltraMinimalSnapshotPlusUpdatesSymbolAndSecurityIdGbpusdSingleInstrument')) 'R151 profile preservation missing.'
Assert-True (@($prior.profiles).Contains('LegacySnapshotOnlySymbolAndSecurityBatch')) 'Legacy profile preservation missing.'

Assert-True ($reservation.reservationDecisionPresent -eq $true) 'Next activation reservation decision missing.'
Assert-True ($reservation.nextRealActivationPhase -eq $targetPhase) 'Next real activation phase must be R155.'
Assert-True ($reservation.oddNumbered -eq $true) 'Next real activation phase must be odd.'
Assert-True ($reservation.explicitlyReserved -eq $true) 'Next real activation phase must be explicitly reserved.'
Assert-False $reservation.r154ExecutedActivation 'R154 must not execute activation.'
Assert-True ($reservation.r155RequiresFreshExactOperatorApproval -eq $true) 'R155 fresh approval constraint missing.'
Assert-True ($reservation.r155RequiresWeekdayActiveFxMarketDataAvailability -eq $true) 'R155 market-hours constraint missing.'
Assert-True ($reservation.r155RequiresExactlyOneBoundedAttempt -eq $true) 'R155 one-attempt constraint missing.'
Assert-True ($reservation.r155MustStopAfterAttempt -eq $true) 'R155 stop-after-attempt constraint missing.'

Assert-True ($preflight.targetPhase -eq $targetPhase) 'R155 preflight target mismatch.'
Assert-True ($preflight.freshExactOperatorApprovalRequired -eq $true) 'Fresh approval checklist missing.'
Assert-True ($preflight.weekdayActiveFxMarketDataAvailabilityRequired -eq $true) 'Market-hours checklist missing.'
Assert-True ($preflight.exactlyOneBoundedAttemptRequired -eq $true) 'One-attempt checklist missing.'
Assert-True ($preflight.selectedProfile -eq $profileName) 'Preflight selected profile mismatch.'
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

Assert-True ($contract.expectedEvidenceContractPresent -eq $true) 'Expected evidence contract missing.'
Assert-True ($contract.marketDataRequestMustOccurAfterFixSuccess -eq $true) 'MarketDataRequest/FIX order contract missing.'
Assert-True ($contract.marketDataResponseReadMustOccurAfterMarketDataRequestSuccess -eq $true) 'MarketDataResponse/request order contract missing.'
Assert-True ($contract.credentialValuesReturned -eq $false) 'credentialValuesReturned contract must be false.'
Assert-True ($reporting.reportingContractReadinessPresent -eq $true) 'Reporting readiness missing.'
Assert-True ($reporting.r133CliReportingContractActive -eq $true) 'R133 reporting readiness missing.'
Assert-True ($reporting.r135FixtureReportingVerificationPresent -eq $true) 'R135 reporting verification missing.'
Assert-True ($reporting.cliField -eq 'sessionRejectSanitizedReasonCategory') 'CLI reporting field mismatch.'
Assert-True ($reporting.artifactField -eq 'sanitizedSessionRejectReasonCategory') 'Artifact reporting field mismatch.'

Assert-True ($universe.approvedUniversePreserved -eq $true) 'Approved universe preservation missing.'
Assert-True ((@($universe.approvedInstruments) -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved universe changed.'
Assert-True ((@($universe.futureDiagnosticRequestSymbols) -join ',') -eq 'GBPUSD') 'Future diagnostic symbols mismatch.'
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
Assert-False $forbidden.socketTlsFixMarketDataRuntimeActionAttempted 'Runtime boundary action detected.'
Assert-False $forbidden.liveMarketDataRequestSent 'Live MarketDataRequest detected.'
Assert-False $forbidden.liveMarketDataResponseRead 'Live MarketDataResponse read detected.'
Assert-False $forbidden.ordersIntroduced 'Order path introduced.'
Assert-False $forbidden.tradingEnabled 'Trading enabled.'
Assert-False $forbidden.schedulerIntroduced 'Scheduler introduced.'
Assert-False $forbidden.pollingLoopIntroduced 'Polling loop introduced.'
Assert-False $forbidden.serviceIntroduced 'Service introduced.'
Assert-False $forbidden.replayIntroduced 'Replay introduced.'
Assert-False $forbidden.shadowReplayIntroduced 'Shadow replay introduced.'
Assert-False $forbidden.apiStartupAttempted 'API startup attempted.'
Assert-False $forbidden.workerStartupAttempted 'Worker startup attempted.'
Assert-True ($apiWorker.audit -eq 'PASS') 'API/Worker audit did not pass.'
Assert-True ($apiWorker.apiWorkerGatewayMode -eq 'FakeLmaxGatewayOnly') 'API/Worker FakeLmaxGatewayOnly regressed.'
Assert-False $apiWorker.apiStartupAttempted 'API startup attempted.'
Assert-False $apiWorker.workerStartupAttempted 'Worker startup attempted.'
Assert-False $apiWorker.lifecycleProfileSelectedForApiWorker 'Lifecycle profile selected for API/Worker.'

Assert-True ($next.recommendedNextPhase -eq $targetPhase) 'Next phase recommendation must be R155.'
Assert-True ($next.r155MustRequireFreshExactOperatorApproval -eq $true) 'R155 approval recommendation missing.'
Assert-True ($next.r155MustUseExactlyOneBoundedAttempt -eq $true) 'R155 one-attempt recommendation missing.'
Assert-True ($next.r156MustBeNoExternalReviewGateOnly -eq $true) 'R156 review/gate recommendation missing.'
Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.focusedTests -match '^PASS') 'Focused test evidence missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit test evidence missing.'
Assert-True ($gate.integrationTests -match '^PASS') 'Integration test evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R154_VALIDATION_PASS') 'Validator evidence missing.'

$reservationSourcePath = Join-Path $root 'src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxApprovedBoundedExecutableRetryPhaseReservations.cs'
$factoryPath = Join-Path $root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualExecutionSurfaceFactory.cs'
$reservationSource = Get-Content $reservationSourcePath -Raw
$factory = Get-Content $factoryPath -Raw
Assert-True ($reservationSource.Contains($targetPhase)) 'R155 is not explicitly reserved in source.'
Assert-True ($factory.Contains($profileName)) 'Future manual real-bounded path does not select lifecycle profile.'

$approvalTemplatePath = Join-Path $artifactRoot 'phase-lmax-r154-r155-operator-approval-template.md'
Assert-True (Test-Path $approvalTemplatePath) 'R155 approval template missing.'
$approvalTemplate = Get-Content $approvalTemplatePath -Raw
Assert-True ($approvalTemplate.Contains('I, Philippe, explicitly approve Phase LMAX-R155')) 'R155 approval template text missing.'
Assert-True ($approvalTemplate.Contains($profileName)) 'R155 approval template does not name selected profile.'
Assert-True ($approvalTemplate.Contains('exactly one bounded attempt')) 'R155 approval template missing one-attempt constraint.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r154-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R154 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R154_VALIDATION_PASS'
