$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'
$phase = 'LMAX-R145'
$profileName = 'UltraMinimalSnapshotPlusUpdatesSecurityIdOnlyGbpusdSingleInstrument'

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

$summary = Read-Json 'phase-lmax-r145-ultraminimal-gbpusd-retry-readiness.json'
$selection = Read-Json 'phase-lmax-r145-ultraminimal-profile-selection-evidence.json'
$prior = Read-Json 'phase-lmax-r145-prior-profiles-not-selected-evidence.json'
$reservation = Read-Json 'phase-lmax-r145-next-activation-phase-reservation-decision.json'
$preflight = Read-Json 'phase-lmax-r145-r147-preflight-checklist.json'
$contract = Read-Json 'phase-lmax-r145-r147-expected-evidence-contract.json'
$reporting = Read-Json 'phase-lmax-r145-reporting-contract-readiness.json'
$universe = Read-Json 'phase-lmax-r145-approved-instrument-universe-preservation.json'
$usdjpy = Read-Json 'phase-lmax-r145-usdjpy-caveat-preservation.json'
$sanitization = Read-Json 'phase-lmax-r145-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r145-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r145-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r145-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r145-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r141-repaired-profile-activation.json',
    'phase-lmax-r142-repaired-runtime-reject-review.json',
    'phase-lmax-r143-deep-docs-tag-crosscheck.json',
    'phase-lmax-r144-ultraminimal-gbpusd-profile-repair.json',
    'phase-lmax-r144-ultraminimal-profile-contract.json',
    'phase-lmax-r133-sanitized-sessionreject-reporting-fix.json',
    'phase-lmax-r135-sanitized-fixture-reclassification.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "Required predecessor evidence missing: $name"
}

$r141 = Read-Json 'phase-lmax-r141-repaired-profile-activation.json'
$r142 = Read-Json 'phase-lmax-r142-repaired-runtime-reject-review.json'
$r143 = Read-Json 'phase-lmax-r143-deep-docs-tag-crosscheck.json'
$r144 = Read-Json 'phase-lmax-r144-ultraminimal-gbpusd-profile-repair.json'
$r144Contract = Read-Json 'phase-lmax-r144-ultraminimal-profile-contract.json'

Assert-True ($summary.phase -eq $phase) 'R145 summary phase mismatch.'
Assert-True ($summary.classification -eq 'LMAX_R145_PASS_ULTRAMINIMAL_GBPUSD_RETRY_READINESS_NO_EXTERNAL') 'R145 classification mismatch.'
Assert-True ($summary.noExternal -eq $true) 'R145 must remain no-external.'
Assert-False $summary.externalActivationAttempted 'R145 external activation detected.'
Assert-False $summary.socketTlsFixMarketDataRuntimeActionAttempted 'R145 runtime boundary action detected.'
Assert-False $summary.liveMarketDataRequestSent 'R145 live MarketDataRequest detected.'
Assert-False $summary.liveMarketDataResponseRead 'R145 live MarketDataResponse read detected.'
Assert-True ($r141.attemptCount -eq 1) 'R141 attemptCount must equal 1.'
Assert-True ($r142.classification -eq 'LMAX_R142_PASS_REPAIRED_REJECT_REVIEW_DEEP_DOCS_TAG_CROSSCHECK_RECOMMENDED_NO_EXTERNAL') 'R142 evidence missing.'
Assert-True ($r143.classification -eq 'LMAX_R143_PASS_DEEP_DOCS_TAG_CROSSCHECK_ULTRAMINIMAL_PROFILE_REPAIR_RECOMMENDED_NO_EXTERNAL') 'R143 evidence missing.'
Assert-True ($r144.classification -eq 'LMAX_R144_PASS_ULTRAMINIMAL_GBPUSD_PROFILE_REPAIR_READY_NO_EXTERNAL') 'R144 evidence missing.'
Assert-True ($r144Contract.profileName -eq $profileName) 'R144 ultra-minimal profile contract missing.'

Assert-True ($selection.profileSelectionEvidencePresent -eq $true) 'Ultra-minimal profile selection evidence missing.'
Assert-True ($selection.futureDiagnosticRetryProfile -eq $profileName) 'Future diagnostic retry profile mismatch.'
Assert-True ($summary.futureDiagnosticRetryProfile -eq $profileName) 'Future diagnostic retry does not select ultra-minimal profile.'
Assert-True ($selection.futureManualRealBoundedPathCanSelectProfile -eq $true) 'Future manual real-bounded path cannot select ultra-minimal profile.'
Assert-True ($selection.selectedForNextDiagnosticRetryReadinessPath -eq $true) 'Ultra-minimal profile not selected for next diagnostic readiness.'
Assert-False $selection.selectedForApiWorker 'Ultra-minimal profile must not be selected for API/Worker.'
Assert-True ($selection.gbpusdOnly -eq $true) 'Future diagnostic retry is not GBPUSD-only.'
Assert-True ((@($selection.diagnosticRequestSymbols) -join ',') -eq 'GBPUSD') 'Diagnostic retry includes non-GBPUSD symbols.'
Assert-True ($selection.securityId -eq '4002') 'GBPUSD SecurityID weakened.'
Assert-True ($selection.securityIdSource -eq '8') 'SecurityIDSource weakened.'
Assert-False $selection.symbolTextPresent 'Symbol text present.'
Assert-False $selection.internalSymbolPresent 'InternalSymbol present.'
Assert-True ($selection.snapshotPlusUpdates -eq $true) 'SnapshotPlusUpdates missing.'
Assert-False $selection.snapshotOnly 'SnapshotOnly returned.'
Assert-True ($selection.mdUpdateTypeProfileControlled -eq $true) 'MDUpdateType profile control missing.'
Assert-False $selection.mdUpdateTypeIncluded 'MDUpdateType included despite omit/profile-control decision.'
Assert-True ($selection.marketDepth -eq 1) 'MarketDepth must be one.'
Assert-True ((@($selection.mdEntryTypes) -join ',') -eq 'Bid,Offer') 'MDEntryTypes must be bid/offer only.'
Assert-True ($selection.oneRequestOnly -eq $true) 'Future diagnostic retry must be one request only.'
Assert-True ($summary.sanitizedRepeatingGroupFieldOrderContractReady -eq $true) 'Repeating-group/field-order readiness missing.'

Assert-True ($prior.priorProfilesPreserved -eq $true) 'Prior profile preservation missing.'
Assert-False $prior.legacyRejectedProfileSelectedForNextDiagnosticRetry 'Legacy profile selected for next diagnostic retry.'
Assert-False $prior.repairedProfileSelectedForNextDiagnosticRetry 'R139 repaired profile selected for next diagnostic retry.'
Assert-True ($prior.ultraMinimalProfileSelectedForNextDiagnosticRetry -eq $true) 'Ultra-minimal profile non-selection evidence mismatch.'

Assert-True ($reservation.nextRealActivationPhase -eq 'LMAX-R147') 'Next activation phase must be R147.'
Assert-False $reservation.r146ReservedForActivation 'R146 must not be reserved for activation.'
Assert-True ($reservation.r146MustRemainNoExternalIfUsed -eq $true) 'R146 no-external condition missing.'
Assert-True ($reservation.nextRealActivationPhaseOddNumbered -eq $true) 'Next activation phase must be odd-numbered.'
Assert-True ($reservation.nextRealActivationPhaseExplicitlyReserved -eq $true) 'Next activation phase must be explicitly reserved.'
Assert-True ($reservation.requiresFreshExactOperatorApproval -eq $true) 'Fresh approval requirement missing.'
Assert-True ($reservation.exactlyOneBoundedAttemptRequired -eq $true) 'Single-attempt requirement missing.'
Assert-True ($reservation.weekdayActiveFxMarketDataAvailabilityRequired -eq $true) 'Market-hours requirement missing.'
Assert-True (Test-Path (Join-Path $artifactRoot 'phase-lmax-r145-r147-operator-approval-template.md')) 'R147 approval template missing.'
Assert-True ($preflight.targetActivationPhase -eq 'LMAX-R147') 'R147 preflight target mismatch.'
Assert-True ($preflight.activationAllowedInR145 -eq $false) 'R145 must not allow activation.'
Assert-True (@($preflight.preflightChecklist).Count -ge 12) 'R147 preflight checklist incomplete.'
Assert-True ($contract.targetActivationPhase -eq 'LMAX-R147') 'R147 expected evidence contract target mismatch.'
Assert-True ($contract.expectedProfileEvidence.selectedMarketDataRequestProfile -eq $profileName) 'Expected evidence profile mismatch.'
Assert-True ((@($contract.expectedProfileEvidence.diagnosticRequestSymbols) -join ',') -eq 'GBPUSD') 'Expected evidence is not GBPUSD-only.'

Assert-True ($reporting.reportingContractReady -eq $true) 'R133/R135 reporting readiness missing.'
Assert-True ($reporting.r133CliReportingField -eq 'sessionRejectSanitizedReasonCategory') 'CLI reporting field missing.'
Assert-True ($reporting.r133ArtifactReportingField -eq 'sanitizedSessionRejectReasonCategory') 'Artifact reporting field missing.'
Assert-True ($reporting.r135FixtureReportingVerified -eq $true) 'R135 fixture reporting verification missing.'
Assert-False $reporting.rawRejectTextSerialized 'Raw reject text serialized in reporting readiness.'
Assert-False $reporting.rawFixSerialized 'Raw FIX serialized in reporting readiness.'

Assert-True ($universe.approvedInstrumentUniversePreserved -eq $true) 'Approved universe preservation missing.'
Assert-True ((@($universe.approvedInstruments) -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved universe changed.'
Assert-True ($universe.diagnosticRetryIncludesOnlyGbpUsd -eq $true) 'Diagnostic retry scope not GBPUSD-only.'
Assert-False $universe.nonApprovedInstrumentsAllowed 'Non-approved instruments allowed.'
Assert-True ($usdjpy.usdJpyPreservedInApprovedUniverse -eq $true) 'USDJPY not preserved in approved universe.'
Assert-True ($usdjpy.usdJpySecurityId -eq '4004') 'USDJPY SecurityID weakened.'
Assert-True ($usdjpy.usdJpySecurityIdSource -eq '8') 'USDJPY SecurityIDSource weakened.'
Assert-True ($usdjpy.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat weakened.'

Assert-True ($summary.credentialValuesReturned -eq $false) 'credentialValuesReturned must remain false.'
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
Assert-False $forbidden.externalActivationAttempted 'External activation detected in R145.'
Assert-False $forbidden.socketTlsFixMarketDataRuntimeActionAttempted 'Runtime boundary action detected in R145.'
Assert-False $forbidden.liveMarketDataRequestSent 'Live MarketDataRequest detected in R145.'
Assert-False $forbidden.liveMarketDataResponseRead 'Live MarketDataResponse read detected in R145.'
Assert-False $forbidden.ordersIntroduced 'Order path introduced.'
Assert-False $forbidden.tradingEnabled 'Trading enabled.'
Assert-False $forbidden.schedulerIntroduced 'Scheduler introduced.'
Assert-False $forbidden.pollingLoopIntroduced 'Polling loop introduced.'
Assert-False $forbidden.serviceIntroduced 'Service introduced.'
Assert-False $forbidden.replayIntroduced 'Replay introduced.'
Assert-False $forbidden.shadowReplayIntroduced 'Shadow replay introduced.'
Assert-True ($apiWorker.audit -eq 'PASS') 'API/Worker audit did not pass.'
Assert-True ($apiWorker.apiWorkerGatewayMode -eq 'FakeLmaxGatewayOnly') 'API/Worker FakeLmaxGatewayOnly regressed.'
Assert-False $apiWorker.ultraMinimalProfileSelectedForApiWorker 'Ultra-minimal profile selected for API/Worker.'

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R147') 'Next phase recommendation must be R147.'
Assert-True ($next.r147RequiresFreshExactOperatorApproval -eq $true) 'R147 fresh approval requirement missing.'
Assert-True ($next.r147RequiresWeekdayActiveFxMarketDataAvailability -eq $true) 'R147 market-hours requirement missing.'
Assert-True ($next.r147RequiresExactlyOneBoundedAttempt -eq $true) 'R147 single-attempt requirement missing.'
Assert-True ($next.r147MustStopAfterAttemptForR148ReviewGate -eq $true) 'R147 stop-after-attempt requirement missing.'
Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.focusedTests -match '^PASS') 'Focused test evidence missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit test evidence missing.'
Assert-True ($gate.integrationTests -match '^PASS') 'Integration test evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R145_VALIDATION_PASS') 'Validator result missing.'

$reservationPath = Join-Path $root 'src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxApprovedBoundedExecutableRetryPhaseReservations.cs'
$factoryPath = Join-Path $root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualExecutionSurfaceFactory.cs'
$reservationCode = Get-Content $reservationPath -Raw
$factoryCode = Get-Content $factoryPath -Raw
Assert-True ($reservationCode.Contains('"LMAX-R147"')) 'R147 explicit reservation missing from code.'
Assert-True ($factoryCode.Contains($profileName)) 'Future manual real-bounded path does not select ultra-minimal profile.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r145-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R145 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R145_VALIDATION_PASS'
