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

$summary = Read-Json 'phase-lmax-r167-after-state-repair-activation.json'
$preflight = Read-Json 'phase-lmax-r167-preflight-result.json'
$profile = Read-Json 'phase-lmax-r167-selected-profile-evidence.json'
$state = Read-Json 'phase-lmax-r167-state-reporting-evidence.json'
$boundary = Read-Json 'phase-lmax-r167-boundary-evidence.json'
$response = Read-Json 'phase-lmax-r167-marketdata-response-evidence.json'
$shutdown = Read-Json 'phase-lmax-r167-shutdown-revert-evidence.json'
$sanitization = Read-Json 'phase-lmax-r167-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r167-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r167-api-worker-fake-gateway-audit.json'
$universe = Read-Json 'phase-lmax-r167-approved-universe-preservation.json'
$usdJpy = Read-Json 'phase-lmax-r167-usdjpy-caveat-preservation.json'
$next = Read-Json 'phase-lmax-r167-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r167-gate-validation.json'

Assert-True (Test-Path (Join-Path $artifactRoot 'phase-lmax-r167-expected-operator-approval.txt')) 'Expected approval file missing.'
Assert-True (Test-Path (Join-Path $artifactRoot 'phase-lmax-r167-operator-approval-note.md')) 'Operator approval note missing.'

Assert-True ($summary.phase -eq 'LMAX-R167') 'R167 summary phase mismatch.'
Assert-True ($summary.classification -eq 'LMAX_R167_FAIL_STATE_REPORTING_FIELDS_MISSING') 'R167 classification mismatch.'
Assert-True ($summary.externalActivationAttempted -eq $true) 'R167 external activation attempt missing.'
Assert-True ($summary.attemptCount -eq 1) 'R167 attemptCount must be exactly 1.'
Assert-True ($summary.operatorApprovalExactMatch -eq $true) 'R167 exact approval missing.'
Assert-True ($summary.weekdayMarketHoursConfirmationPresent -eq $true) 'R167 market-hours confirmation missing.'
Assert-True ($summary.operatorConfirmedMarketHoursWindow -eq 'Monday, May 18, 2026 at 17:11 Europe/Paris') 'R167 market-hours window mismatch.'

Assert-True ($preflight.preflightPassed -eq $true) 'R167 preflight should pass.'
Assert-True ($preflight.operatorApprovalExactMatch -eq $true) 'R167 preflight exact approval missing.'
Assert-True ($preflight.weekdayActiveFxMarketDataAvailabilityConfirmed -eq $true) 'R167 preflight market-hours confirmation missing.'
Assert-True ($preflight.singleBoundedAttemptObserved -eq $true) 'R167 single bounded attempt missing.'
Assert-True ($preflight.attemptCount -eq 1) 'R167 preflight attemptCount must be 1.'
Assert-False $preflight.blockedBeforeExternalBoundary 'R167 should not be preflight-blocked after approval.'

Assert-True ($profile.selectedProfileEvidencePresent -eq $true) 'Selected profile evidence missing.'
Assert-True ($profile.selectedProfile -eq 'UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument') 'Selected profile mismatch.'
Assert-True ($profile.gbpusdOnly -eq $true) 'Profile must be GBPUSD-only.'
Assert-True ($profile.singleRequest -eq $true) 'Profile must be single request.'
Assert-True ($profile.securityId -eq '4002') 'SecurityID missing.'
Assert-True ($profile.securityIdSource -eq '8') 'SecurityIDSource missing.'
Assert-True ($profile.subscriptionRequestType -eq '1') 'SubscriptionRequestType=1 missing.'
Assert-True ($profile.mdUpdateType -eq '0') 'MDUpdateType=0 missing.'
Assert-True ($profile.marketDepth -eq 1) 'MarketDepth=1 missing.'
Assert-True ($profile.noMdEntryTypes -eq 2) 'NoMDEntryTypes=2 missing.'
Assert-True ($profile.bidAndOfferTogether -eq $true) 'Bid+Offer together missing.'
Assert-False $profile.symbolTextPresent 'Symbol text present.'
Assert-False $profile.internalSymbolPresent 'InternalSymbol present.'
Assert-False $profile.snapshotOnlyPresent 'SnapshotOnly present.'

Assert-True ($state.stateReportingFieldsPresent -eq $true) 'State reporting fields missing.'
Assert-False $state.marketDataRequestSentLegacyFlag 'Legacy sent flag expected false in observed R167 evidence.'
Assert-False $state.marketDataRequestWriteAttempted 'Observed R167 write attempted flag should be false.'
Assert-False $state.marketDataRequestWriteSucceeded 'Observed R167 write succeeded flag should be false.'
Assert-False $state.marketDataRequestResponseReadAttempted 'Observed R167 response read attempted flag should be false.'
Assert-False $state.marketDataRequestReachedBoundedResponseClassification 'Observed R167 bounded classification flag should be false.'
Assert-True ($state.marketDataBoundaryClassificationObserved -eq $true) 'Boundary classification evidence missing.'
Assert-True ($state.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'MarketDataResponse category missing.'
Assert-False $state.stateReportingFieldsSemanticallyValid 'State reporting should be marked semantically invalid.'
Assert-True ($state.stateReportingFailureCategory -eq 'ExecutableSessionClientWrapperDidNotPropagateRequestStateFieldsPlausible') 'Unexpected state reporting failure category.'

Assert-True ($boundary.externalActivationAttempted -eq $true) 'External boundary not attempted.'
Assert-True ($boundary.attemptCount -eq 1) 'Boundary attemptCount must be 1.'
Assert-True ($boundary.boundaryEvidence.'TCP/socket' -eq 'Succeeded') 'TCP should succeed.'
Assert-True ($boundary.boundaryEvidence.TLS -eq 'Succeeded') 'TLS should succeed.'
Assert-True ($boundary.boundaryEvidence.'FIX logon/session' -eq 'Succeeded') 'FIX should succeed.'
Assert-True ($boundary.boundaryEvidence.'FIX acknowledgement' -eq 'FixLogonAcknowledged') 'FIX acknowledgement missing.'
Assert-True ($boundary.boundaryEvidence.MarketDataRequest -eq 'ReachedBoundedResponseClassificationAfterFixSuccess') 'MarketDataRequest boundary evidence missing.'
Assert-True ($boundary.marketDataBoundaryResultCategory -eq 'SessionRejectObservedWithSanitizedReason') 'MarketData boundary category missing.'

Assert-True ($response.marketDataResponseBoundaryReached -eq $true) 'MarketDataResponse boundary not reached.'
Assert-True ($response.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'MarketDataResponse category mismatch.'
Assert-True ($response.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'CLI sanitized reason missing.'
Assert-True ($response.sanitizedSessionRejectReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'Artifact sanitized reason missing.'
Assert-True ($response.entriesObserved -eq $false) 'Entries observed unexpectedly.'
Assert-True ($response.sanitizedEntryCount -eq 0) 'Sanitized entry count should be 0.'
Assert-False $response.rawRejectTextSerialized 'Raw reject text serialized.'
Assert-False $response.rawFixSerialized 'Raw FIX serialized.'

Assert-True ($shutdown.shutdownRevertCompleted -eq $true) 'Shutdown/revert evidence missing.'
Assert-True ($shutdown.attemptCount -eq 1) 'Shutdown evidence attemptCount must be 1.'
Assert-False $shutdown.additionalAttemptStarted 'Additional attempt detected.'

Assert-True ($sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned must remain false.'
Assert-False $sanitization.rawFixSerialized 'Raw FIX serialized.'
Assert-False $sanitization.rawRejectTextSerialized 'Raw reject text serialized.'
Assert-False $sanitization.rawCredentialsSerialized 'Raw credentials serialized.'
Assert-False $sanitization.rawSessionIdentifiersSerialized 'Raw session IDs serialized.'
Assert-False $sanitization.rawCompIdsSerialized 'Raw CompIDs serialized.'
Assert-False $sanitization.rawEndpointSerialized 'Raw endpoint serialized.'
Assert-False $sanitization.rawTlsMaterialSerialized 'Raw TLS material serialized.'

Assert-True ($forbidden.audit -eq 'PASS') 'Forbidden actions audit failed.'
Assert-False $forbidden.ordersIntroduced 'Orders introduced.'
Assert-False $forbidden.tradingEnabled 'Trading enabled.'
Assert-False $forbidden.schedulerIntroduced 'Scheduler introduced.'
Assert-False $forbidden.pollingLoopIntroduced 'Polling introduced.'
Assert-False $forbidden.serviceIntroduced 'Service introduced.'
Assert-False $forbidden.replayIntroduced 'Replay introduced.'
Assert-False $forbidden.shadowReplayIntroduced 'Shadow replay introduced.'
Assert-False $forbidden.apiStartupAttempted 'API startup attempted.'
Assert-False $forbidden.workerStartupAttempted 'Worker startup attempted.'

Assert-True ($apiWorker.audit -eq 'PASS') 'API/Worker audit failed.'
Assert-True ($apiWorker.apiWorkerGatewayMode -eq 'FakeLmaxGatewayOnly') 'API/Worker FakeLmaxGatewayOnly regressed.'
Assert-False $apiWorker.apiStartupAttempted 'API startup attempted.'
Assert-False $apiWorker.workerStartupAttempted 'Worker startup attempted.'

Assert-True ($universe.approvedUniversePreserved -eq $true) 'Approved universe weakened.'
Assert-True ((@($universe.approvedInstruments) -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved universe changed.'
Assert-False $universe.nonApprovedInstrumentsRequested 'Non-approved instruments requested.'
Assert-True ($usdJpy.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat weakened.'
Assert-True ($usdJpy.usdJpySecurityId -eq '4004') 'USDJPY SecurityID weakened.'
Assert-True ($usdJpy.usdJpySecurityIdSource -eq '8') 'USDJPY SecurityIDSource weakened.'

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R168') 'Next phase must be R168.'
Assert-True ($next.r168MustBeReviewGateOnlyNoExternal -eq $true) 'R168 must be no-external review gate.'
Assert-True ($next.doNotRecommendBlindRetry -eq $true) 'Blind retry must not be recommended.'
Assert-True ($next.liveRetryPerformed -eq $true) 'R167 live retry evidence missing.'
Assert-True ($next.attemptCount -eq 1) 'R167 next phase attempt count mismatch.'
Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.focusedTests -match '^PASS') 'Focused test evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R167_VALIDATION_PASS') 'Validator result missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r167-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R167 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R167_VALIDATION_PASS'
