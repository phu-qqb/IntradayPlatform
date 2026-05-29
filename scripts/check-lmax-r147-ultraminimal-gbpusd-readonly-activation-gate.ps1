$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'
$phase = 'LMAX-R147'
$profileName = 'UltraMinimalSnapshotPlusUpdatesSecurityIdOnlyGbpusdSingleInstrument'
$requiredApproval = 'I, Philippe, explicitly approve Phase LMAX-R147 for one temporary QQ Workspace Demo weekday market-hours read-only runtime market-data activation retry with the ultra-minimal GBPUSD-only MarketDataRequest profile UltraMinimalSnapshotPlusUpdatesSecurityIdOnlyGbpusdSingleInstrument, using SecurityID=4002 and SecurityIDSource=8, with no Symbol text, no InternalSymbol, no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, exactly one bounded attempt, and immediate abort authority.'

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

$summary = Read-Json 'phase-lmax-r147-ultraminimal-gbpusd-activation.json'
$preflight = Read-Json 'phase-lmax-r147-preflight-result.json'
$profile = Read-Json 'phase-lmax-r147-ultraminimal-profile-selection-evidence.json'
$boundary = Read-Json 'phase-lmax-r147-boundary-evidence.json'
$request = Read-Json 'phase-lmax-r147-marketdata-request-evidence.json'
$response = Read-Json 'phase-lmax-r147-marketdata-response-evidence.json'
$reason = Read-Json 'phase-lmax-r147-sessionreject-sanitized-reason-reporting.json'
$universe = Read-Json 'phase-lmax-r147-approved-universe-preservation.json'
$usdJpy = Read-Json 'phase-lmax-r147-usdjpy-caveat-preservation.json'
$sanitized = Read-Json 'phase-lmax-r147-sanitized-result.json'
$shutdown = Read-Json 'phase-lmax-r147-shutdown-revert-evidence.json'
$sanitization = Read-Json 'phase-lmax-r147-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r147-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r147-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r147-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r147-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r141-repaired-profile-activation.json',
    'phase-lmax-r142-repaired-runtime-reject-review.json',
    'phase-lmax-r143-deep-docs-tag-crosscheck.json',
    'phase-lmax-r144-ultraminimal-gbpusd-profile-repair.json',
    'phase-lmax-r145-ultraminimal-gbpusd-retry-readiness.json',
    'phase-lmax-r147-operator-approval-note.md',
    'phase-lmax-r147-expected-operator-approval.txt')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "Required evidence missing: $name"
}

$approvalNote = (Get-Content -LiteralPath (Join-Path $artifactRoot 'phase-lmax-r147-operator-approval-note.md') -Raw)
$expectedApproval = (Get-Content -LiteralPath (Join-Path $artifactRoot 'phase-lmax-r147-expected-operator-approval.txt') -Raw).Trim()
Assert-True ($approvalNote.StartsWith($requiredApproval, [StringComparison]::Ordinal)) 'R147 operator approval is missing or mismatched.'
Assert-True ($expectedApproval -eq $requiredApproval) 'R147 expected approval is missing or mismatched.'

$r141 = Read-Json 'phase-lmax-r141-repaired-profile-activation.json'
$r142 = Read-Json 'phase-lmax-r142-repaired-runtime-reject-review.json'
$r143 = Read-Json 'phase-lmax-r143-deep-docs-tag-crosscheck.json'
$r144 = Read-Json 'phase-lmax-r144-ultraminimal-gbpusd-profile-repair.json'
$r145 = Read-Json 'phase-lmax-r145-ultraminimal-gbpusd-retry-readiness.json'

Assert-True ($r141.attemptCount -eq 1) 'R141 attemptCount must equal 1.'
Assert-True ($r142.classification -eq 'LMAX_R142_PASS_REPAIRED_REJECT_REVIEW_DEEP_DOCS_TAG_CROSSCHECK_RECOMMENDED_NO_EXTERNAL') 'R142 evidence missing.'
Assert-True ($r143.classification -eq 'LMAX_R143_PASS_DEEP_DOCS_TAG_CROSSCHECK_ULTRAMINIMAL_PROFILE_REPAIR_RECOMMENDED_NO_EXTERNAL') 'R143 evidence missing.'
Assert-True ($r144.classification -eq 'LMAX_R144_PASS_ULTRAMINIMAL_GBPUSD_PROFILE_REPAIR_READY_NO_EXTERNAL') 'R144 evidence missing.'
Assert-True ($r145.classification -eq 'LMAX_R145_PASS_ULTRAMINIMAL_GBPUSD_RETRY_READINESS_NO_EXTERNAL') 'R145 evidence missing.'

Assert-True ($summary.phase -eq $phase) 'R147 summary phase mismatch.'
Assert-True ($summary.classification -eq 'LMAX_R147_FAIL_SESSIONREJECT_WITH_SANITIZED_REASON_REPORTED') 'R147 classification mismatch.'
Assert-True ($summary.externalActivationAttempted -eq $true) 'R147 external activation attempt missing.'
Assert-True ($summary.attemptCount -eq 1) 'R147 attemptCount must equal 1.'
Assert-True ($preflight.operatorApprovalExactMatch -eq $true) 'Fresh exact approval preflight missing.'
Assert-True ($preflight.weekdayActiveFxMarketDataAvailabilityConfirmed -eq $true) 'R147 market-hours confirmation missing.'
Assert-True ($preflight.operatorConfirmedMarketHoursWindow -eq 'Monday, May 18, 2026 at 13:16 Europe/Paris') 'R147 market-hours window mismatch.'
Assert-True ($summary.operatorConfirmedMarketHoursWindow -eq 'Monday, May 18, 2026 at 13:16 Europe/Paris') 'R147 summary market-hours window mismatch.'

Assert-True ($profile.selectedMarketDataRequestProfile -eq $profileName) 'Ultra-minimal profile not selected.'
Assert-False $profile.legacyRejectedProfileSelected 'Legacy rejected profile selected.'
Assert-False $profile.repairedProfileSelected 'R139 repaired profile selected.'
Assert-True ($profile.gbpusdOnly -eq $true) 'Diagnostic retry is not GBPUSD-only.'
Assert-True ((@($profile.diagnosticRequestSymbols) -join ',') -eq 'GBPUSD') 'Diagnostic request includes non-GBPUSD symbols.'
Assert-False $profile.eurgbpPresentInDiagnosticRequest 'EURGBP present in diagnostic request.'
Assert-False $profile.audusdPresentInDiagnosticRequest 'AUDUSD present in diagnostic request.'
Assert-False $profile.usdjpyPresentInDiagnosticRequest 'USDJPY present in diagnostic request.'
Assert-False $profile.nonApprovedInstrumentPresent 'Non-approved instrument present.'
Assert-True ($profile.securityId -eq '4002') 'GBPUSD SecurityID missing or weakened.'
Assert-True ($profile.securityIdSource -eq '8') 'SecurityIDSource missing or weakened.'
Assert-False $profile.symbolTextIncluded 'Symbol text included.'
Assert-False $profile.internalSymbolIncluded 'InternalSymbol included.'
Assert-True ($profile.snapshotPlusUpdatesSelected -eq $true) 'SnapshotPlusUpdates missing.'
Assert-False $profile.snapshotOnlySelected 'SnapshotOnly used.'
Assert-True ($profile.mdUpdateTypeProfileControlled -eq $true) 'MDUpdateType profile control missing.'
Assert-False $profile.mdUpdateTypeIncluded 'MDUpdateType included despite omit/profile-control decision.'
Assert-True ($profile.marketDepth -eq 1) 'MarketDepth must be one.'
Assert-True ((@($profile.mdEntryTypes) -join ',') -eq 'Bid,Offer') 'MDEntryTypes must be bid/offer only.'
Assert-True ($profile.oneRequestOnly -eq $true) 'Diagnostic retry must use one request only.'
Assert-True ($profile.sanitizedRepeatingGroupFieldOrderContractReady -eq $true) 'Repeating-group/field-order evidence missing.'

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
Assert-True ($request.selectedMarketDataRequestProfile -eq $profileName) 'Request evidence missing ultra-minimal profile.'
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

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R148') 'Next phase recommendation must be R148.'
Assert-True ($next.r148MustBeReviewGateOnlyNoExternal -eq $true) 'R148 no-external review gate recommendation missing.'
Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.focusedTests -match '^PASS') 'Focused test evidence missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit test evidence missing.'
Assert-True ($gate.integrationTests -match '^PASS') 'Integration test evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R147_VALIDATION_PASS') 'Validator result missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r147-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R147 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R147_VALIDATION_PASS'
