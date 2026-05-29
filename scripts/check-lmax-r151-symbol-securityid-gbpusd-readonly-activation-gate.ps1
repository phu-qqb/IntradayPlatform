$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'
$phase = 'LMAX-R151'
$profileName = 'UltraMinimalSnapshotPlusUpdatesSymbolAndSecurityIdGbpusdSingleInstrument'
$requiredApproval = 'I, Philippe, explicitly approve Phase LMAX-R151 for one temporary QQ Workspace Demo weekday market-hours read-only runtime market-data activation retry with the Symbol+SecurityID GBPUSD-only MarketDataRequest profile UltraMinimalSnapshotPlusUpdatesSymbolAndSecurityIdGbpusdSingleInstrument, using SecurityID=4002 and SecurityIDSource=8, with no InternalSymbol, no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, exactly one bounded attempt, and immediate abort authority.'

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

$summary = Read-Json 'phase-lmax-r151-symbol-securityid-gbpusd-activation.json'
$preflight = Read-Json 'phase-lmax-r151-preflight-result.json'
$profile = Read-Json 'phase-lmax-r151-selected-identifier-profile-evidence.json'
$nonSelected = Read-Json 'phase-lmax-r151-non-selected-profiles-evidence.json'
$boundary = Read-Json 'phase-lmax-r151-boundary-evidence.json'
$request = Read-Json 'phase-lmax-r151-marketdata-request-evidence.json'
$response = Read-Json 'phase-lmax-r151-marketdata-response-evidence.json'
$reason = Read-Json 'phase-lmax-r151-sessionreject-sanitized-reason-reporting.json'
$universe = Read-Json 'phase-lmax-r151-approved-universe-preservation.json'
$usdJpy = Read-Json 'phase-lmax-r151-usdjpy-caveat-preservation.json'
$sanitized = Read-Json 'phase-lmax-r151-sanitized-result.json'
$shutdown = Read-Json 'phase-lmax-r151-shutdown-revert-evidence.json'
$sanitization = Read-Json 'phase-lmax-r151-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r151-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r151-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r151-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r151-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r147-ultraminimal-gbpusd-activation.json',
    'phase-lmax-r148-ultraminimal-gbpusd-reject-review.json',
    'phase-lmax-r149-identifier-combination-repair.json',
    'phase-lmax-r150-identifier-repair-retry-readiness.json',
    'phase-lmax-r151-operator-approval-note.md',
    'phase-lmax-r151-expected-operator-approval.txt')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "Required evidence missing: $name"
}

$approvalNote = Get-Content -LiteralPath (Join-Path $artifactRoot 'phase-lmax-r151-operator-approval-note.md') -Raw
$expectedApproval = (Get-Content -LiteralPath (Join-Path $artifactRoot 'phase-lmax-r151-expected-operator-approval.txt') -Raw).Trim()
Assert-True ($approvalNote.StartsWith($requiredApproval, [StringComparison]::Ordinal)) 'R151 operator approval is missing or mismatched.'
Assert-True ($expectedApproval -eq $requiredApproval) 'R151 expected approval is missing or mismatched.'

$r147 = Read-Json 'phase-lmax-r147-ultraminimal-gbpusd-activation.json'
$r148 = Read-Json 'phase-lmax-r148-ultraminimal-gbpusd-reject-review.json'
$r149 = Read-Json 'phase-lmax-r149-identifier-combination-repair.json'
$r150 = Read-Json 'phase-lmax-r150-identifier-repair-retry-readiness.json'

Assert-True ($r147.attemptCount -eq 1) 'R147 attemptCount must equal 1.'
Assert-True ($r148.classification -eq 'LMAX_R148_PASS_ULTRAMINIMAL_REJECT_REVIEW_IDENTIFIER_REPAIR_RECOMMENDED_NO_EXTERNAL') 'R148 evidence missing.'
Assert-True ($r149.classification -eq 'LMAX_R149_PASS_IDENTIFIER_COMBINATION_REPAIR_READY_NO_EXTERNAL') 'R149 evidence missing.'
Assert-True ($r150.classification -eq 'LMAX_R150_PASS_IDENTIFIER_REPAIR_RETRY_READINESS_NO_EXTERNAL') 'R150 evidence missing.'

Assert-True ($summary.phase -eq $phase) 'R151 summary phase mismatch.'
Assert-True ($summary.classification -eq 'LMAX_R151_FAIL_SESSIONREJECT_WITH_SANITIZED_REASON_REPORTED') 'R151 classification mismatch.'
Assert-True ($summary.externalActivationAttempted -eq $true) 'R151 external activation attempt missing.'
Assert-True ($summary.attemptCount -eq 1) 'R151 external attemptCount must equal 1.'
Assert-True ($summary.preExternalApprovalMismatchInvocationAttemptCount -eq 0) 'Pre-external mismatch invocation must have attemptCount 0.'
Assert-True ($preflight.operatorApprovalExactMatch -eq $true) 'Fresh exact approval preflight missing.'
Assert-True ($preflight.weekdayActiveFxMarketDataAvailabilityConfirmed -eq $true) 'R151 market-hours confirmation missing.'
Assert-True ($preflight.operatorConfirmedMarketHoursWindow -eq 'Monday, May 18, 2026 at 14:03 Europe/Paris') 'R151 market-hours window mismatch.'
Assert-True ($preflight.approvalMismatchInvocationCrossedExternalBoundary -eq $false) 'Approval mismatch invocation crossed an external boundary.'
Assert-True ($summary.operatorConfirmedMarketHoursWindow -eq 'Monday, May 18, 2026 at 14:03 Europe/Paris') 'R151 summary market-hours window mismatch.'

Assert-True ($profile.selectedIdentifierProfileEvidencePresent -eq $true) 'Selected identifier profile evidence missing.'
Assert-True ($profile.selectedMarketDataRequestProfile -eq $profileName) 'Symbol+SecurityID GBPUSD profile not selected.'
Assert-True ($profile.selectedProfileCount -eq 1) 'More than one diagnostic profile selected.'
Assert-True ($profile.gbpusdOnly -eq $true) 'Diagnostic retry is not GBPUSD-only.'
Assert-True ((@($profile.diagnosticRequestSymbols) -join ',') -eq 'GBPUSD') 'Diagnostic request includes non-GBPUSD symbols.'
Assert-False $profile.eurgbpPresentInDiagnosticRequest 'EURGBP present in diagnostic request.'
Assert-False $profile.audusdPresentInDiagnosticRequest 'AUDUSD present in diagnostic request.'
Assert-False $profile.usdjpyPresentInDiagnosticRequest 'USDJPY present in diagnostic request.'
Assert-False $profile.nonApprovedInstrumentPresent 'Non-approved instrument present.'
Assert-True ($profile.identifierCombination -eq 'SymbolPlusSecurityId') 'Identifier combination contract weakened.'
Assert-True ($profile.symbolIncluded -eq $true) 'Symbol identifier component missing.'
Assert-True ($profile.securityIdIncluded -eq $true) 'SecurityID component missing.'
Assert-True ($profile.securityId -eq '4002') 'GBPUSD SecurityID missing or weakened.'
Assert-True ($profile.securityIdSourceIncluded -eq $true) 'SecurityIDSource missing.'
Assert-True ($profile.securityIdSource -eq '8') 'SecurityIDSource missing or weakened.'
Assert-False $profile.internalSymbolIncluded 'InternalSymbol included.'
Assert-True ($profile.snapshotPlusUpdatesSelected -eq $true) 'SnapshotPlusUpdates missing.'
Assert-False $profile.snapshotOnlySelected 'SnapshotOnly used.'
Assert-True ($profile.mdUpdateTypeProfileControlled -eq $true) 'MDUpdateType profile control missing.'
Assert-False $profile.mdUpdateTypeIncluded 'MDUpdateType included despite omit/profile-control decision.'
Assert-True ($profile.marketDepth -eq 1) 'MarketDepth must be one.'
Assert-True ((@($profile.mdEntryTypes) -join ',') -eq 'Bid,Offer') 'MDEntryTypes must be bid/offer only.'
Assert-True ($profile.oneRequestOnly -eq $true) 'Diagnostic retry must use one request only.'
Assert-True ($profile.sanitizedRepeatingGroupFieldOrderContractReady -eq $true) 'Repeating-group/field-order evidence missing.'

Assert-True ($nonSelected.nonSelectedProfilesEvidencePresent -eq $true) 'Non-selected profile evidence missing.'
Assert-False $nonSelected.multipleDiagnosticProfilesSelected 'Multiple diagnostic profiles selected.'
foreach ($profileNameCandidate in @(
    'UltraMinimalSnapshotPlusUpdatesSecurityIdOnlyGbpusdSingleInstrument',
    'UltraMinimalSnapshotPlusUpdatesSymbolOnlyGbpusdSingleInstrument',
    'RepairedSnapshotPlusUpdatesSecurityIdOnlyNonBatched',
    'LegacySnapshotOnlySymbolAndSecurityBatch')) {
    $match = @($nonSelected.profiles | Where-Object { $_.profileName -eq $profileNameCandidate })
    Assert-True ($match.Count -eq 1) "Non-selected profile evidence missing for $profileNameCandidate."
    Assert-False $match[0].selectedForR151 "Unexpected non-selected profile selected: $profileNameCandidate."
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
Assert-False $forbidden.schedulerIntroduced 'Scheduler introduced.'
Assert-False $forbidden.pollingLoopIntroduced 'Polling loop introduced.'
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

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R152') 'Next phase recommendation must be R152.'
Assert-True ($next.r152MustBeReviewGateOnlyNoExternal -eq $true) 'R152 no-external review gate recommendation missing.'
Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.focusedTests -match '^PASS') 'Focused test evidence missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit test evidence missing.'
Assert-True ($gate.integrationTests -match '^PASS') 'Integration test evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R151_VALIDATION_PASS') 'Validator result missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r151-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R151 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R151_VALIDATION_PASS'
