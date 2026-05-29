$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'
$phase = 'LMAX-R161'
$profileName = 'UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument'
$requiredApproval = 'I, Philippe, explicitly approve Phase LMAX-R161 for one temporary QQ Workspace Demo weekday market-hours read-only runtime market-data activation retry with the MDUpdateType-required GBPUSD-only MarketDataRequest profile UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument, using SecurityID=4002 and SecurityIDSource=8, SubscriptionRequestType=1, MDUpdateType=0, MarketDepth=1, NoMDEntryTypes=2 with bid and offer together, no Symbol text, no InternalSymbol, no SnapshotOnly, no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, exactly one bounded attempt, and immediate abort authority.'

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

$summary = Read-Json 'phase-lmax-r161-mdupdatetype-required-gbpusd-activation.json'
$preflight = Read-Json 'phase-lmax-r161-preflight-result.json'
$profile = Read-Json 'phase-lmax-r161-selected-mdupdatetype-profile-evidence.json'
$nonSelected = Read-Json 'phase-lmax-r161-non-selected-profiles-evidence.json'
$boundary = Read-Json 'phase-lmax-r161-boundary-evidence.json'
$request = Read-Json 'phase-lmax-r161-marketdata-request-evidence.json'
$response = Read-Json 'phase-lmax-r161-marketdata-response-evidence.json'
$reason = Read-Json 'phase-lmax-r161-sessionreject-sanitized-reason-reporting.json'
$universe = Read-Json 'phase-lmax-r161-approved-universe-preservation.json'
$usdJpy = Read-Json 'phase-lmax-r161-usdjpy-caveat-preservation.json'
$sanitized = Read-Json 'phase-lmax-r161-sanitized-result.json'
$shutdown = Read-Json 'phase-lmax-r161-shutdown-revert-evidence.json'
$sanitization = Read-Json 'phase-lmax-r161-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r161-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r161-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r161-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r161-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r158-mdupdatetype-required-repair.json',
    'phase-lmax-r159-mdupdatetype-required-retry-readiness.json',
    'phase-lmax-r161-operator-approval-note.md',
    'phase-lmax-r161-expected-operator-approval.txt')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "Required evidence missing: $name"
}

$approvalNote = Get-Content -LiteralPath (Join-Path $artifactRoot 'phase-lmax-r161-operator-approval-note.md') -Raw
$expectedApproval = (Get-Content -LiteralPath (Join-Path $artifactRoot 'phase-lmax-r161-expected-operator-approval.txt') -Raw).Trim()
Assert-True ($approvalNote.StartsWith($requiredApproval, [StringComparison]::Ordinal)) 'R161 operator approval is missing or mismatched.'
Assert-True ($expectedApproval -eq $requiredApproval) 'R161 expected approval is missing or mismatched.'

$r158 = Read-Json 'phase-lmax-r158-mdupdatetype-required-repair.json'
$r159 = Read-Json 'phase-lmax-r159-mdupdatetype-required-retry-readiness.json'
Assert-True ($r158.classification -eq 'LMAX_R158_PASS_MDUPDATETYPE_REQUIRED_REPAIR_READY_NO_EXTERNAL') 'R158 evidence missing.'
Assert-True ($r159.classification -eq 'LMAX_R159_PASS_MDUPDATETYPE_REQUIRED_RETRY_READINESS_NO_EXTERNAL') 'R159 evidence missing.'

Assert-True ($summary.phase -eq $phase) 'R161 summary phase mismatch.'
Assert-True ($summary.classification -eq 'LMAX_R161_FAIL_SESSIONREJECT_WITH_SANITIZED_REASON_REPORTED') 'R161 classification mismatch.'
Assert-True ($summary.externalActivationAttempted -eq $true) 'R161 external activation attempt missing.'
Assert-True ($summary.attemptCount -eq 1) 'R161 external attemptCount must equal 1.'
Assert-True ($preflight.operatorApprovalExactMatch -eq $true) 'Fresh exact approval preflight missing.'
Assert-True ($preflight.weekdayActiveFxMarketDataAvailabilityConfirmed -eq $true) 'R161 market-hours confirmation missing.'
Assert-True ($preflight.operatorConfirmedMarketHoursWindow -eq 'Monday, May 18, 2026 at 16:16 Europe/Paris') 'R161 market-hours window mismatch.'
Assert-True ($summary.operatorConfirmedMarketHoursWindow -eq 'Monday, May 18, 2026 at 16:16 Europe/Paris') 'R161 summary market-hours window mismatch.'

Assert-True ($profile.selectedMdUpdateTypeProfileEvidencePresent -eq $true) 'Selected MDUpdateType-required profile evidence missing.'
Assert-True ($profile.selectedMarketDataRequestProfile -eq $profileName) 'MDUpdateType-required GBPUSD profile not selected.'
Assert-True ($profile.selectedProfileCount -eq 1) 'More than one diagnostic profile selected.'
Assert-True ($profile.gbpusdOnly -eq $true) 'Diagnostic retry is not GBPUSD-only.'
Assert-True ((@($profile.diagnosticRequestSymbols) -join ',') -eq 'GBPUSD') 'Diagnostic request includes non-GBPUSD symbols.'
Assert-False $profile.eurgbpPresentInDiagnosticRequest 'EURGBP present in diagnostic request.'
Assert-False $profile.audusdPresentInDiagnosticRequest 'AUDUSD present in diagnostic request.'
Assert-False $profile.usdjpyPresentInDiagnosticRequest 'USDJPY present in diagnostic request.'
Assert-False $profile.nonApprovedInstrumentPresent 'Non-approved instrument present.'
Assert-True ($profile.oneRequestOnly -eq $true) 'Diagnostic retry must use one request only.'
Assert-True ($profile.identifierCombination -eq 'SecurityIdOnly') 'Identifier combination contract weakened.'
Assert-True ($profile.securityIdIncluded -eq $true) 'SecurityID component missing.'
Assert-True ($profile.securityId -eq '4002') 'GBPUSD SecurityID missing or weakened.'
Assert-True ($profile.securityIdSourceIncluded -eq $true) 'SecurityIDSource missing.'
Assert-True ($profile.securityIdSource -eq '8') 'SecurityIDSource missing or weakened.'
Assert-True ($profile.subscriptionRequestType -eq '1') 'SubscriptionRequestType=1 missing.'
Assert-True ($profile.snapshotPlusUpdatesSelected -eq $true) 'SnapshotPlusUpdates missing.'
Assert-False $profile.snapshotOnlySelected 'SnapshotOnly used.'
Assert-False $profile.subscriptionRequestTypeZeroPresent 'SubscriptionRequestType=0 used.'
Assert-True ($profile.mdUpdateTypeIncluded -eq $true) 'MDUpdateType missing.'
Assert-True ($profile.mdUpdateType -eq '0') 'MDUpdateType=0 missing.'
Assert-True ($profile.marketDepth -eq 1) 'MarketDepth must be one.'
Assert-True ($profile.noMdEntryTypes -eq 2) 'NoMDEntryTypes=2 missing.'
Assert-True ((@($profile.mdEntryTypes) -join ',') -eq 'Bid,Offer') 'MDEntryTypes must be bid/offer.'
Assert-True ($profile.bidAndOfferTogether -eq $true) 'Bid+Offer pair missing.'
Assert-False $profile.symbolTextIncluded 'Symbol text included.'
Assert-False $profile.internalSymbolIncluded 'InternalSymbol included.'
Assert-True ($profile.sanitizedRepeatingGroupFieldOrderContractReady -eq $true) 'Repeating-group/field-order evidence missing.'

Assert-True ($nonSelected.nonSelectedProfilesEvidencePresent -eq $true) 'Non-selected profile evidence missing.'
Assert-False $nonSelected.multipleDiagnosticProfilesSelected 'Multiple diagnostic profiles selected.'
foreach ($profileNameCandidate in @(
    'LegacySnapshotOnlySymbolAndSecurityBatch',
    'RepairedSnapshotPlusUpdatesSecurityIdOnlyNonBatched',
    'UltraMinimalSnapshotPlusUpdatesSecurityIdOnlyGbpusdSingleInstrument',
    'UltraMinimalSnapshotPlusUpdatesSymbolOnlyGbpusdSingleInstrument',
    'UltraMinimalSnapshotPlusUpdatesSymbolAndSecurityIdGbpusdSingleInstrument',
    'UltraMinimalSnapshotPlusUpdatesFreshLifecycleSymbolAndSecurityIdGbpusdSingleInstrument')) {
    $match = @($nonSelected.profiles | Where-Object { $_.profileName -eq $profileNameCandidate })
    Assert-True ($match.Count -eq 1) "Non-selected profile evidence missing for $profileNameCandidate."
    Assert-False $match[0].selectedForR161 "Unexpected non-selected profile selected: $profileNameCandidate."
}

Assert-True ($boundary.boundaryEvidence.'Credential/config' -eq 'Succeeded') 'Credential/config boundary not succeeded.'
Assert-True ($boundary.boundaryEvidence.'TCP/socket' -eq 'Succeeded') 'TCP/socket boundary not succeeded.'
Assert-True ($boundary.boundaryEvidence.TLS -eq 'Succeeded') 'TLS boundary not succeeded.'
Assert-True ($boundary.boundaryEvidence.'FIX logon/session' -eq 'Succeeded') 'FIX boundary not succeeded.'
Assert-True ($boundary.fixAcknowledgementCategory -eq 'FixLogonAcknowledged') 'FIX acknowledgement missing.'
Assert-True ($boundary.marketDataRequestAfterFixSuccess -eq $true) 'MarketDataRequest occurred before FIX success or evidence missing.'
Assert-True ($boundary.marketDataResponseReadAfterRequestSuccess -eq $true) 'MarketDataResponse read occurred before request success or evidence missing.'
Assert-True ($boundary.boundaryEvidence.'Shutdown/revert' -eq 'Succeeded') 'Shutdown/revert did not succeed.'

Assert-True ($request.marketDataRequestBoundaryReached -eq $true) 'MarketDataRequest boundary missing.'
Assert-True ($request.marketDataRequestOccurredAfterFixSuccess -eq $true) 'MarketDataRequest allowed without FIX success.'
Assert-True ($request.selectedMarketDataRequestProfile -eq $profileName) 'Request evidence missing selected profile.'
Assert-True ($request.gbpusdOnly -eq $true) 'Request evidence not GBPUSD-only.'
Assert-True ($request.oneRequestOnly -eq $true) 'Request evidence not single request.'
Assert-True ($request.securityId -eq '4002') 'Request evidence SecurityID missing.'
Assert-True ($request.securityIdSource -eq '8') 'Request evidence SecurityIDSource missing.'
Assert-True ($request.subscriptionRequestType -eq '1') 'Request evidence SubscriptionRequestType=1 missing.'
Assert-True ($request.mdUpdateType -eq '0') 'Request evidence MDUpdateType=0 missing.'
Assert-True ($request.marketDepth -eq 1) 'Request evidence MarketDepth=1 missing.'
Assert-True ($request.noMdEntryTypes -eq 2) 'Request evidence NoMDEntryTypes=2 missing.'
Assert-True ($request.bidAndOfferTogether -eq $true) 'Request evidence bid+offer missing.'
Assert-False $request.symbolTextIncluded 'Request evidence includes Symbol text.'
Assert-False $request.internalSymbolIncluded 'Request evidence includes InternalSymbol.'
Assert-False $request.snapshotOnlySelected 'Request evidence uses SnapshotOnly.'
Assert-False $request.nonApprovedInstrumentsRequested 'Non-approved instruments requested.'
Assert-False $request.rawFixSerialized 'Raw FIX serialized in request evidence.'

Assert-True ($response.marketDataResponseBoundaryReached -eq $true) 'MarketDataResponse boundary missing.'
Assert-True ($response.marketDataResponseReadAfterMarketDataRequestSuccess -eq $true) 'MarketDataResponse read without request success.'
Assert-True ($response.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'Expected sanitized SessionReject category missing.'
Assert-True ($response.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'CLI sanitized reason category missing.'
Assert-True ($response.sanitizedSessionRejectReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'Artifact sanitized reason category missing.'
Assert-True ($reason.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'Reason reporting CLI field missing.'
Assert-True ($reason.sanitizedSessionRejectReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'Reason reporting artifact field missing.'
Assert-True ($reason.cliReportingFieldPresent -eq $true) 'CLI reporting field not present.'
Assert-True ($reason.artifactReportingFieldPresent -eq $true) 'Artifact reporting field not present.'
Assert-False $response.entriesObserved 'Entries unexpectedly observed for reject classification.'
Assert-True ($response.sanitizedEntryCount -eq 0) 'Sanitized entry count must be zero.'

Assert-True ($universe.approvedUniversePreserved -eq $true) 'Approved universe preservation missing.'
Assert-True ((@($universe.approvedInstruments) -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved universe changed.'
Assert-True ($universe.diagnosticRequestIncludesOnlyGbpUsd -eq $true) 'Diagnostic request not GBPUSD-only.'
Assert-False $universe.nonApprovedInstrumentsRequested 'Non-approved instruments requested.'
Assert-True ($usdJpy.usdJpySecurityId -eq '4004') 'USDJPY SecurityID weakened.'
Assert-True ($usdJpy.usdJpySecurityIdSource -eq '8') 'USDJPY SecurityIDSource weakened.'
Assert-True ($usdJpy.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat weakened.'

Assert-True ($summary.credentialValuesReturned -eq $false) 'credentialValuesReturned must remain false.'
Assert-True ($sanitized.credentialValuesReturned -eq $false) 'Sanitized result credentialValuesReturned must remain false.'
Assert-True ($sanitization.credentialValuesReturned -eq $false) 'Sanitization audit credentialValuesReturned must remain false.'
Assert-False $sanitization.rawFixSerialized 'Raw FIX serialized.'
Assert-False $sanitization.rawRejectTextSerialized 'Raw reject text serialized.'
Assert-False $sanitization.rawCredentialsSerialized 'Raw credentials serialized.'
Assert-False $sanitization.rawUsernameSerialized 'Raw username serialized.'
Assert-False $sanitization.rawPasswordSerialized 'Raw password serialized.'
Assert-False $sanitization.rawSessionIdentifiersSerialized 'Raw session identifiers serialized.'
Assert-False $sanitization.rawCompIdsSerialized 'Raw CompIDs serialized.'
Assert-False $sanitization.rawEndpointSerialized 'Raw endpoint serialized.'
Assert-False $sanitization.rawTlsMaterialSerialized 'Raw TLS material serialized.'

Assert-True ($shutdown.shutdownRevertCompleted -eq $true) 'Shutdown/revert evidence missing.'
Assert-True ($shutdown.stopAfterAttemptHonored -eq $true) 'Stop-after-attempt evidence missing.'
Assert-False $shutdown.additionalAttemptStarted 'Additional attempt detected.'
Assert-True ($shutdown.attemptCount -eq 1) 'Shutdown evidence attemptCount invalid.'

Assert-True ($forbidden.audit -eq 'PASS') 'Forbidden actions audit did not pass.'
Assert-True ($forbidden.attemptCount -eq 1) 'Forbidden audit attemptCount invalid.'
Assert-False $forbidden.ordersIntroduced 'Order path introduced.'
Assert-False $forbidden.tradingEnabled 'Trading enabled.'
Assert-False $forbidden.tradingStateMutation 'Trading state mutation introduced.'
Assert-False $forbidden.schedulerIntroduced 'Scheduler introduced.'
Assert-False $forbidden.pollingLoopIntroduced 'Polling loop introduced.'
Assert-False $forbidden.serviceIntroduced 'Service introduced.'
Assert-False $forbidden.replayIntroduced 'Replay introduced.'
Assert-False $forbidden.shadowReplayIntroduced 'Shadow replay introduced.'
Assert-False $forbidden.apiStartupAttempted 'API startup attempted.'
Assert-False $forbidden.workerStartupAttempted 'Worker startup attempted.'
Assert-False $forbidden.productionAccountUsed 'Production account used.'
Assert-False $forbidden.additionalAttemptStarted 'Additional attempt detected.'

Assert-True ($apiWorker.audit -eq 'PASS') 'API/Worker audit did not pass.'
Assert-True ($apiWorker.apiWorkerGatewayMode -eq 'FakeLmaxGatewayOnly') 'API/Worker FakeLmaxGatewayOnly regressed.'
Assert-False $apiWorker.apiStartupAttempted 'API startup attempted.'
Assert-False $apiWorker.workerStartupAttempted 'Worker startup attempted.'

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R162') 'Next phase recommendation must be R162.'
Assert-True ($next.r162MustBeReviewGateOnlyNoExternal -eq $true) 'R162 no-external review gate recommendation missing.'
Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.focusedTests -match '^PASS') 'Focused test evidence missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit test evidence missing.'
Assert-True ($gate.integrationTests -match '^PASS') 'Integration test evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R161_VALIDATION_PASS') 'Validator result missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r161-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R161 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R161_VALIDATION_PASS'
