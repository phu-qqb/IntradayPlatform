$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'
$phase = 'LMAX-R141'

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

$summary = Read-Json 'phase-lmax-r141-repaired-profile-activation.json'
$preflight = Read-Json 'phase-lmax-r141-preflight-result.json'
$profile = Read-Json 'phase-lmax-r141-repaired-profile-selection-evidence.json'
$boundary = Read-Json 'phase-lmax-r141-boundary-evidence.json'
$request = Read-Json 'phase-lmax-r141-marketdata-request-evidence.json'
$response = Read-Json 'phase-lmax-r141-marketdata-response-evidence.json'
$reason = Read-Json 'phase-lmax-r141-sessionreject-sanitized-reason-reporting.json'
$approved = Read-Json 'phase-lmax-r141-approved-instrument-evidence.json'
$usdJpy = Read-Json 'phase-lmax-r141-usdjpy-caveat-preservation.json'
$sanitized = Read-Json 'phase-lmax-r141-sanitized-result.json'
$shutdown = Read-Json 'phase-lmax-r141-shutdown-revert-evidence.json'
$sanitization = Read-Json 'phase-lmax-r141-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r141-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r141-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r141-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r141-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r137-weekday-market-hours-activation.json',
    'phase-lmax-r137-sessionreject-sanitized-reason-reporting.json',
    'phase-lmax-r138-sanitized-runtime-reject-review.json',
    'phase-lmax-r139-marketdatarequest-shape-repair.json',
    'phase-lmax-r140-repaired-marketdatarequest-retry-readiness.json',
    'phase-lmax-r141-operator-approval.txt',
    'phase-lmax-r141-expected-operator-approval.txt')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "Required evidence missing: $name"
}

$approval = (Get-Content -LiteralPath (Join-Path $artifactRoot 'phase-lmax-r141-operator-approval.txt') -Raw).Trim()
$expectedApproval = (Get-Content -LiteralPath (Join-Path $artifactRoot 'phase-lmax-r141-expected-operator-approval.txt') -Raw).Trim()
$requiredApproval = 'I, Philippe, explicitly approve Phase LMAX-R141 for one temporary QQ Workspace Demo weekday market-hours read-only runtime market-data activation retry with the repaired MarketDataRequest profile RepairedSnapshotPlusUpdatesSecurityIdOnlyNonBatched for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, exactly one bounded attempt, and immediate abort authority.'
Assert-True ($approval -eq $requiredApproval) 'R141 operator approval is missing or mismatched.'
Assert-True ($expectedApproval -eq $requiredApproval) 'R141 expected approval is missing or mismatched.'

Assert-True ($summary.phase -eq $phase) 'R141 summary phase mismatch.'
Assert-True ($summary.classification -eq 'LMAX_R141_FAIL_SESSIONREJECT_WITH_SANITIZED_REASON_REPORTED') 'R141 classification mismatch.'
Assert-True ($summary.externalActivationAttempted -eq $true) 'R141 external activation attempt missing.'
Assert-True ($summary.attemptCount -eq 1) 'R141 attemptCount must equal 1.'
Assert-True ($preflight.freshExactOperatorApprovalMatched -eq $true) 'Fresh exact approval preflight missing.'
Assert-True ($preflight.operatorConfirmedMarketHoursWindow.weekday -eq $true) 'R141 weekday market-hours evidence missing.'
Assert-True ($preflight.operatorConfirmedMarketHoursWindow.marketHoursConstraintRepresented -eq $true) 'R141 market-hours constraint missing.'

Assert-True ($profile.selectedMarketDataRequestProfile -eq 'RepairedSnapshotPlusUpdatesSecurityIdOnlyNonBatched') 'Repaired profile not selected.'
Assert-False $profile.legacyRejectedProfileSelected 'Legacy rejected profile selected.'
Assert-False $profile.snapshotOnlySelected 'Request shape remains SnapshotOnly.'
Assert-True ($profile.snapshotPlusUpdatesSelected -eq $true) 'Snapshot-plus-updates shape missing.'
Assert-True ($profile.securityIdOnlySelected -eq $true) 'SecurityID-only shape missing.'
Assert-False $profile.symbolTextIncluded 'Request shape includes Symbol text.'
Assert-True ($profile.nonBatchedOneRequestPerApprovedInstrument -eq $true) 'Non-batched request plan missing.'
Assert-False $profile.batchedAllApprovedInstruments 'Request still batches all approved instruments.'

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
Assert-True ($request.selectedMarketDataRequestProfile -eq 'RepairedSnapshotPlusUpdatesSecurityIdOnlyNonBatched') 'Request evidence missing repaired profile.'
Assert-False $request.nonApprovedInstrumentsRequested 'Non-approved instruments requested.'

Assert-True ($response.marketDataResponseBoundaryReached -eq $true) 'MarketDataResponse boundary missing.'
Assert-True ($response.marketDataResponseReadAfterMarketDataRequestSuccess -eq $true) 'MarketDataResponse read without request success.'
Assert-True ($response.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'Expected sanitized SessionReject category missing.'
Assert-True ($response.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'CLI sanitized reason category missing.'
Assert-True ($response.sanitizedSessionRejectReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'Artifact sanitized reason category missing.'
Assert-True ($reason.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'Reason reporting CLI field missing.'
Assert-True ($reason.sanitizedSessionRejectReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'Reason reporting artifact field missing.'
Assert-True ($reason.cliReportingFieldPresent -eq $true) 'CLI reporting field not present.'
Assert-True ($reason.artifactReportingFieldPresent -eq $true) 'Artifact reporting field not present.'

$approvedSymbols = @($approved.approvedInstruments)
Assert-True (($approvedSymbols -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved instrument scope changed.'
Assert-True ($approved.approvedInstrumentScopeExact -eq $true) 'Approved instrument scope not exact.'
Assert-True ($approved.nonApprovedInstrumentsAbsent -eq $true) 'Non-approved instruments present.'
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

Assert-True ($apiWorker.audit -eq 'PASS') 'API/Worker audit did not pass.'
Assert-True ($apiWorker.apiWorkerGatewayMode -eq 'FakeLmaxGatewayOnly') 'API/Worker FakeLmaxGatewayOnly regressed.'
Assert-False $apiWorker.apiStartupAttempted 'API startup attempted.'
Assert-False $apiWorker.workerStartupAttempted 'Worker startup attempted.'

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R142') 'Next phase recommendation must be R142.'
Assert-True ($next.r142MustBeReviewGateOnlyNoExternal -eq $true) 'R142 no-external review gate recommendation missing.'
Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.focusedTests -match '^PASS') 'Focused test evidence missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit test evidence missing.'
Assert-True ($gate.integrationTests -match '^PASS') 'Integration test evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R141_VALIDATION_PASS') 'Validator result missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r141-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R141 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R141_VALIDATION_PASS'
