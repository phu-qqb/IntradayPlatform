$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'
$profileName = 'UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument'
$targetPhase = 'LMAX-R161'

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

foreach ($name in @(
    'phase-lmax-r158-mdupdatetype-required-repair.json',
    'phase-lmax-r158-new-profile-contract.json',
    'phase-lmax-r158-gate-validation.json',
    'phase-lmax-r133-gate-validation.json',
    'phase-lmax-r135-sanitized-fixture-reclassification.json',
    'phase-lmax-r159-mdupdatetype-required-retry-readiness-summary.md',
    'phase-lmax-r159-mdupdatetype-required-retry-readiness.json',
    'phase-lmax-r159-selected-mdupdatetype-profile-evidence.json',
    'phase-lmax-r159-non-selected-profiles-evidence.json',
    'phase-lmax-r159-next-activation-phase-reservation-decision.json',
    'phase-lmax-r159-r161-operator-approval-template.md',
    'phase-lmax-r159-r161-activation-prompt-compact.md',
    'phase-lmax-r159-r161-preflight-checklist.json',
    'phase-lmax-r159-r161-expected-evidence-contract.json',
    'phase-lmax-r159-reporting-contract-readiness.json',
    'phase-lmax-r159-approved-universe-preservation.json',
    'phase-lmax-r159-usdjpy-caveat-preservation.json',
    'phase-lmax-r159-sanitization-audit.json',
    'phase-lmax-r159-forbidden-actions-audit.json',
    'phase-lmax-r159-api-worker-fake-gateway-audit.json',
    'phase-lmax-r159-next-phase-recommendation.json',
    'phase-lmax-r159-gate-validation.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "Required artifact missing: $name"
}

$r158 = Read-Json 'phase-lmax-r158-mdupdatetype-required-repair.json'
$r158Profile = Read-Json 'phase-lmax-r158-new-profile-contract.json'
$summary = Read-Json 'phase-lmax-r159-mdupdatetype-required-retry-readiness.json'
$selected = Read-Json 'phase-lmax-r159-selected-mdupdatetype-profile-evidence.json'
$nonSelected = Read-Json 'phase-lmax-r159-non-selected-profiles-evidence.json'
$reservation = Read-Json 'phase-lmax-r159-next-activation-phase-reservation-decision.json'
$preflight = Read-Json 'phase-lmax-r159-r161-preflight-checklist.json'
$expected = Read-Json 'phase-lmax-r159-r161-expected-evidence-contract.json'
$reporting = Read-Json 'phase-lmax-r159-reporting-contract-readiness.json'
$universe = Read-Json 'phase-lmax-r159-approved-universe-preservation.json'
$usdJpy = Read-Json 'phase-lmax-r159-usdjpy-caveat-preservation.json'
$sanitization = Read-Json 'phase-lmax-r159-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r159-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r159-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r159-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r159-gate-validation.json'

Assert-True ($r158.classification -eq 'LMAX_R158_PASS_MDUPDATETYPE_REQUIRED_REPAIR_READY_NO_EXTERNAL') 'R158 evidence missing.'
Assert-True ($r158.newProfileName -eq $profileName) 'R158 profile evidence mismatch.'
Assert-True ($r158Profile.mdUpdateType -eq '0') 'R158 profile contract missing MDUpdateType=0.'

Assert-True ($summary.classification -eq 'LMAX_R159_PASS_MDUPDATETYPE_REQUIRED_RETRY_READINESS_NO_EXTERNAL') 'R159 classification mismatch.'
Assert-True ($summary.noExternal -eq $true) 'R159 must be no-external.'
Assert-True ($summary.nextActivationPhase -eq $targetPhase) 'Next activation phase must be R161.'
Assert-True ($summary.nextActivationPhaseOddNumbered -eq $true) 'R161 must be odd-numbered.'
Assert-True ($summary.nextActivationPhaseExplicitlyReserved -eq $true) 'R161 explicit reservation missing.'
Assert-True ($summary.selectedFutureProfile -eq $profileName) 'Selected future profile mismatch.'
Assert-True ($summary.selectedFutureProfileCount -eq 1) 'Selected future profile count must be one.'
Assert-True ($summary.futureRetryRequiresFreshExactOperatorApproval -eq $true) 'Fresh approval constraint missing.'
Assert-True ($summary.futureRetryRequiresWeekdayActiveFxMarketDataAvailability -eq $true) 'Market-hours constraint missing.'
Assert-True ($summary.futureRetryRequiresExactlyOneBoundedAttempt -eq $true) 'One-attempt constraint missing.'
Assert-True ($summary.futureRetryMustStopAfterAttempt -eq $true) 'Stop-after-attempt constraint missing.'
Assert-False $summary.liveRetryExecuted 'R159 must not execute a retry.'
Assert-True ($summary.credentialValuesReturned -eq $false) 'credentialValuesReturned=false missing.'

Assert-True ($selected.selectedProfileEvidencePresent -eq $true) 'Selected profile evidence missing.'
Assert-True ($selected.selectedFutureProfile -eq $profileName) 'Selected profile unexpected.'
Assert-True ($selected.selectedFutureProfileCount -eq 1) 'Multiple diagnostic profiles selected.'
Assert-True ($selected.gbpusdOnly -eq $true) 'Future retry is not GBPUSD-only.'
Assert-True ((@($selected.diagnosticRequestSymbols) -join ',') -eq 'GBPUSD') 'Diagnostic symbols are not GBPUSD-only.'
Assert-True ($selected.exactlyOneRequest -eq $true) 'Future retry must use exactly one request.'
Assert-True ($selected.securityId -eq '4002') 'SecurityID=4002 missing.'
Assert-True ($selected.securityIdSource -eq '8') 'SecurityIDSource=8 missing.'
Assert-True ($selected.subscriptionRequestType -eq '1') 'SubscriptionRequestType=1 missing.'
Assert-True ($selected.snapshotPlusUpdates -eq $true) 'SnapshotPlusUpdates missing.'
Assert-False $selected.snapshotOnly 'SnapshotOnly present.'
Assert-True ($selected.mdUpdateType -eq '0') 'MDUpdateType=0 missing.'
Assert-True ($selected.marketDepth -eq 1) 'MarketDepth=1 missing.'
Assert-True ($selected.noMdEntryTypes -eq 2) 'NoMDEntryTypes=2 missing.'
Assert-True ((@($selected.mdEntryTypes) -join ',') -eq 'Bid,Offer') 'Bid+offer pair missing.'
Assert-True ($selected.bidAndOfferTogether -eq $true) 'Bid+offer together evidence missing.'
Assert-False $selected.symbolTextPresent 'Symbol text present.'
Assert-False $selected.internalSymbolPresent 'InternalSymbol present.'
Assert-False $selected.rawFixSerialized 'Raw FIX serialized in selected profile evidence.'

Assert-True ($nonSelected.nonSelectedProfilesEvidencePresent -eq $true) 'Non-selected profiles evidence missing.'
Assert-False $nonSelected.multipleDiagnosticProfilesSelected 'Multiple diagnostic profiles selected.'
foreach ($profile in $nonSelected.profiles) {
    Assert-False $profile.selectedForR161 "Prior profile selected unexpectedly: $($profile.profileName)"
    Assert-True ($profile.preservedAsEvidence -eq $true) "Prior profile not preserved as evidence: $($profile.profileName)"
}

Assert-True ($reservation.nextRealActivationPhase -eq $targetPhase) 'Reservation target mismatch.'
Assert-True ($reservation.nextRealActivationPhaseOddNumbered -eq $true) 'Next activation phase is not odd-numbered.'
Assert-True ($reservation.nextRealActivationPhaseExplicitlyReserved -eq $true) 'Next activation phase is not explicitly reserved.'
Assert-True ($reservation.r160ActivationNotUsed -eq $true) 'R160 activation should not be used.'
Assert-True ($reservation.r161RequiresFreshExactOperatorApproval -eq $true) 'R161 fresh approval missing.'
Assert-True ($reservation.r161RequiresWeekdayActiveFxMarketDataAvailability -eq $true) 'R161 market-hours constraint missing.'
Assert-True ($reservation.r161RequiresExactlyOneBoundedAttempt -eq $true) 'R161 one-attempt constraint missing.'

Assert-True ($preflight.targetPhase -eq $targetPhase) 'Preflight target mismatch.'
Assert-True ($preflight.freshExactOperatorApprovalRequired -eq $true) 'Preflight approval missing.'
Assert-True ($preflight.weekdayActiveFxMarketDataAvailabilityRequired -eq $true) 'Preflight market-hours missing.'
Assert-True ($preflight.exactlyOneBoundedAttemptRequired -eq $true) 'Preflight one-attempt missing.'
Assert-True ($preflight.selectedProfile -eq $profileName) 'Preflight selected profile mismatch.'
Assert-True ($preflight.securityId -eq '4002') 'Preflight SecurityID missing.'
Assert-True ($preflight.securityIdSource -eq '8') 'Preflight SecurityIDSource missing.'
Assert-True ($preflight.subscriptionRequestType -eq '1') 'Preflight SubscriptionRequestType missing.'
Assert-True ($preflight.mdUpdateType -eq '0') 'Preflight MDUpdateType missing.'
Assert-True ($preflight.marketDepth -eq 1) 'Preflight MarketDepth missing.'
Assert-True ($preflight.noMdEntryTypes -eq 2) 'Preflight NoMDEntryTypes missing.'
Assert-True ($preflight.bidAndOfferTogether -eq $true) 'Preflight bid+offer missing.'
Assert-False $preflight.symbolTextAllowed 'Preflight allows Symbol text.'
Assert-False $preflight.internalSymbolAllowed 'Preflight allows InternalSymbol.'
Assert-False $preflight.snapshotOnlyAllowed 'Preflight allows SnapshotOnly.'

$approvalTemplate = Get-Content -LiteralPath (Join-Path $artifactRoot 'phase-lmax-r159-r161-operator-approval-template.md') -Raw
Assert-True ($approvalTemplate.Contains('I, Philippe, explicitly approve Phase LMAX-R161')) 'R161 approval template missing exact opening.'
Assert-True ($approvalTemplate.Contains($profileName)) 'R161 approval template missing selected profile.'
Assert-True ($approvalTemplate.Contains('MDUpdateType=0')) 'R161 approval template missing MDUpdateType=0.'
Assert-True ($approvalTemplate.Contains('exactly one bounded attempt')) 'R161 approval template missing one-attempt constraint.'

Assert-True ($expected.targetPhase -eq $targetPhase) 'Expected evidence target mismatch.'
Assert-True ($expected.requiredSelectedProfile -eq $profileName) 'Expected evidence selected profile mismatch.'
Assert-True ($expected.requiredAttemptCount -eq 1) 'Expected evidence attempt count mismatch.'
Assert-False $expected.rawFixSerializationAllowed 'Raw FIX serialization allowed unexpectedly.'
Assert-False $expected.rawRejectTextSerializationAllowed 'Raw reject text serialization allowed unexpectedly.'
Assert-True ($expected.credentialValuesReturnedExpected -eq $false) 'Expected evidence credentialValuesReturned mismatch.'

Assert-True ($reporting.reportingContractReadinessPresent -eq $true) 'Reporting readiness missing.'
Assert-True ($reporting.r133CliReportingFieldActive -eq $true) 'R133 CLI reporting readiness missing.'
Assert-True ($reporting.r135FixtureCoveragePreserved -eq $true) 'R135 fixture readiness missing.'
Assert-True ($reporting.cliField -eq 'sessionRejectSanitizedReasonCategory') 'CLI reporting field mismatch.'
Assert-True ($reporting.artifactField -eq 'sanitizedSessionRejectReasonCategory') 'Artifact reporting field mismatch.'
Assert-False $reporting.rawRejectTextSerializationAllowed 'Reporting allows raw reject text.'
Assert-False $reporting.rawFixSerializationAllowed 'Reporting allows raw FIX.'

Assert-True ($universe.approvedUniversePreserved -eq $true) 'Approved universe preservation missing.'
Assert-True ((@($universe.approvedInstruments) -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved universe changed.'
Assert-False $universe.nonApprovedInstrumentsAllowed 'Non-approved instruments allowed.'
Assert-True ($usdJpy.usdJpySecurityId -eq '4004') 'USDJPY SecurityID weakened.'
Assert-True ($usdJpy.usdJpySecurityIdSource -eq '8') 'USDJPY SecurityIDSource weakened.'
Assert-True ($usdJpy.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat weakened.'

Assert-True ($sanitization.audit -eq 'PASS') 'Sanitization audit did not pass.'
Assert-True ($sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned=false missing.'
Assert-False $sanitization.rawFixSerialized 'Raw FIX serialized.'
Assert-False $sanitization.rawRejectTextSerialized 'Raw reject text serialized.'
Assert-False $sanitization.rawCredentialsSerialized 'Raw credentials serialized.'
Assert-False $sanitization.rawSessionIdentifiersSerialized 'Raw session identifiers serialized.'
Assert-False $sanitization.rawCompIdsSerialized 'Raw CompIDs serialized.'
Assert-False $sanitization.rawEndpointSerialized 'Raw endpoint serialized.'
Assert-False $sanitization.rawTlsMaterialSerialized 'Raw TLS material serialized.'

Assert-True ($forbidden.audit -eq 'PASS') 'Forbidden actions audit did not pass.'
Assert-True ($forbidden.noExternal -eq $true) 'Forbidden audit must be no-external.'
Assert-False $forbidden.externalActivationAttempted 'External activation attempt detected.'
Assert-False $forbidden.socketTlsFixMarketDataRuntimeActionDetected 'Socket/TLS/FIX/MarketData runtime action detected.'
Assert-False $forbidden.ordersIntroduced 'Order path introduced.'
Assert-False $forbidden.tradingEnabled 'Trading enabled.'
Assert-False $forbidden.schedulerIntroduced 'Scheduler introduced.'
Assert-False $forbidden.pollingLoopIntroduced 'Polling introduced.'
Assert-False $forbidden.serviceIntroduced 'Service introduced.'
Assert-False $forbidden.replayIntroduced 'Replay introduced.'
Assert-False $forbidden.shadowReplayIntroduced 'Shadow replay introduced.'
Assert-False $forbidden.apiStartupAttempted 'API startup attempted.'
Assert-False $forbidden.workerStartupAttempted 'Worker startup attempted.'
Assert-False $forbidden.productionAccountUsed 'Production account used.'

Assert-True ($apiWorker.audit -eq 'PASS') 'API/Worker audit did not pass.'
Assert-True ($apiWorker.apiWorkerGatewayMode -eq 'FakeLmaxGatewayOnly') 'API/Worker FakeLmaxGatewayOnly regressed.'
Assert-False $apiWorker.apiStartupAttempted 'API startup attempted.'
Assert-False $apiWorker.workerStartupAttempted 'Worker startup attempted.'

Assert-True ($next.recommendedNextPhase -eq $targetPhase) 'Next phase recommendation must be R161.'
Assert-True ($next.r161MustRequireFreshExplicitOperatorApproval -eq $true) 'R161 fresh approval recommendation missing.'
Assert-True ($next.r161MustRequireWeekdayActiveFxMarketDataAvailability -eq $true) 'R161 market-hours recommendation missing.'
Assert-True ($next.r161MustUseExactlyOneBoundedAttempt -eq $true) 'R161 one-attempt recommendation missing.'
Assert-True ($next.r161MustUseSelectedProfile -eq $profileName) 'R161 selected profile recommendation mismatch.'
Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.focusedTests -match '^PASS') 'Focused test evidence missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit test evidence missing.'
Assert-True ($gate.integrationTests -match '^PASS') 'Integration test evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R159_VALIDATION_PASS') 'Validator evidence missing.'

$reservationSource = Get-Content -LiteralPath (Join-Path $root 'src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxApprovedBoundedExecutableRetryPhaseReservations.cs') -Raw
$reservationTests = Get-Content -LiteralPath (Join-Path $root 'tests/QQ.Production.Intraday.Tests.Unit/LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapterTests.cs') -Raw
$factorySource = Get-Content -LiteralPath (Join-Path $root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualExecutionSurfaceFactory.cs') -Raw
Assert-True ($reservationSource.Contains($targetPhase)) 'R161 is not explicitly reserved in source.'
Assert-True ($reservationTests.Contains($targetPhase)) 'R161 reservation test coverage missing.'
Assert-True ($factorySource.Contains($profileName)) 'Future manual real-bounded path does not select R158 profile.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r159-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R159 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R159_VALIDATION_PASS'
