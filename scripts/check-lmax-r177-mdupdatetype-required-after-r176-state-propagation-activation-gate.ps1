$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'

function Read-Json($name) {
    $path = Join-Path $artifactRoot $name
    if (!(Test-Path $path)) { throw "Missing artifact: $name" }
    Get-Content $path -Raw | ConvertFrom-Json
}

function Assert-True($condition, $message) { if (-not $condition) { throw $message } }
function Assert-False($condition, $message) { if ($condition) { throw $message } }

$activation = Read-Json 'phase-lmax-r177-mdupdatetype-required-after-r176-state-propagation-activation.json'
$preflight = Read-Json 'phase-lmax-r177-preflight-result.json'
$selected = Read-Json 'phase-lmax-r177-selected-profile-evidence.json'
$state = Read-Json 'phase-lmax-r177-state-propagation-evidence.json'
$boundary = Read-Json 'phase-lmax-r177-boundary-evidence.json'
$request = Read-Json 'phase-lmax-r177-marketdata-request-evidence.json'
$response = Read-Json 'phase-lmax-r177-marketdata-response-evidence.json'
$reporting = Read-Json 'phase-lmax-r177-sessionreject-sanitized-reason-reporting.json'
$universe = Read-Json 'phase-lmax-r177-approved-universe-preservation.json'
$usdJpy = Read-Json 'phase-lmax-r177-usdjpy-caveat-preservation.json'
$sanitized = Read-Json 'phase-lmax-r177-sanitized-result.json'
$shutdown = Read-Json 'phase-lmax-r177-shutdown-revert-evidence.json'
$sanitization = Read-Json 'phase-lmax-r177-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r177-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r177-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r177-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r177-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r176-end-to-end-state-propagation-review.json',
    'phase-lmax-r176-gate-validation.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "Required R176 evidence missing: $name"
}

$r176 = Read-Json 'phase-lmax-r176-end-to-end-state-propagation-review.json'
Assert-True ($r176.classification -eq 'LMAX_R176_PASS_END_TO_END_STATE_PROPAGATION_REPAIR_READY_FOR_R177_NO_EXTERNAL') 'R176 readiness evidence missing.'

Assert-True ($activation.classification -eq 'LMAX_R177_FAIL_STATE_REPORTING_FIELDS_MISSING_OR_INCONSISTENT') 'R177 classification mismatch.'
Assert-True ($activation.externalActivationAttempted -eq $true) 'R177 external activation was not recorded.'
Assert-True ($activation.attemptCount -eq 1) 'R177 attemptCount must be exactly 1.'
Assert-True ($activation.operatorApprovalExactMatch -eq $true) 'Operator approval mismatch.'
Assert-True ($activation.weekdayMarketHoursConfirmationConcreteTime -eq $true) 'Concrete market-hours confirmation missing.'
Assert-False $activation.weekdayMarketHoursConfirmationUsesPlaceholder 'Placeholder market-hours time used.'
Assert-True ($activation.operatorConfirmedMarketHoursWindow -eq 'Monday, May 18, 2026 at 19:28 Europe/Paris') 'Market-hours window mismatch.'

Assert-True ($preflight.preflightPassed -eq $true) 'Preflight did not pass.'
Assert-True ($preflight.externalBoundaryAllowed -eq $true) 'External boundary was not allowed after valid approval/time.'
Assert-True ($preflight.externalActivationAttempted -eq $true) 'Preflight did not record external activation.'
Assert-True ($preflight.attemptCount -eq 1) 'Preflight attempt count must be 1.'

Assert-True ($selected.selectedProfile -eq 'UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument') 'Selected profile mismatch.'
Assert-True ($selected.selectedDiagnosticProfileCount -eq 1) 'Multiple diagnostic profiles selected.'
Assert-True ($selected.gbpusdOnly -eq $true) 'Diagnostic request not GBPUSD-only.'
Assert-True ($selected.singleRequest -eq $true) 'Diagnostic request not single request.'
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

Assert-True ($state.statePropagationEvidencePresent -eq $true) 'State propagation evidence missing.'
Assert-True ($state.marketDataBoundaryClassificationObserved -eq $true) 'Boundary classification evidence missing.'
Assert-False $state.stateFieldsConsistentWithBoundaryEvidence 'State fields should be recorded as inconsistent for R177.'
Assert-False $state.marketDataRequestWriteAttempted 'WriteAttempted unexpectedly true in inconsistent R177 evidence.'
Assert-False $state.marketDataRequestWriteSucceeded 'WriteSucceeded unexpectedly true in inconsistent R177 evidence.'
Assert-False $state.marketDataRequestResponseReadAttempted 'ResponseReadAttempted unexpectedly true in inconsistent R177 evidence.'
Assert-False $state.marketDataRequestReachedBoundedResponseClassification 'ReachedBoundedResponseClassification unexpectedly true in inconsistent R177 evidence.'

Assert-True ($boundary.tcpSocket -eq 'Succeeded') 'TCP boundary did not succeed.'
Assert-True ($boundary.tls -eq 'Succeeded') 'TLS boundary did not succeed.'
Assert-True ($boundary.fixLogonSession -eq 'Succeeded') 'FIX boundary did not succeed.'
Assert-True ($boundary.fixAcknowledgement -eq 'FixLogonAcknowledged') 'FIX acknowledgement missing.'
Assert-True ($boundary.marketDataRequest -eq 'ReachedBoundedResponseClassificationAfterFixSuccess') 'MarketDataRequest boundary evidence missing.'
Assert-True ($boundary.marketDataResponseRead -eq 'ReachedSanitizedClassification') 'MarketDataResponse classification evidence missing.'
Assert-False $boundary.marketDataAllowedWithoutFixSuccess 'MarketData was allowed without FIX success.'

Assert-True ($request.marketDataRequestAttemptedByBoundaryEvidence -eq $true) 'MarketDataRequest boundary evidence missing.'
Assert-False $request.rawFixSerialized 'Raw FIX serialized in request evidence.'
Assert-True ($response.marketDataResponseReadAttemptedByBoundaryEvidence -eq $true) 'MarketDataResponse read evidence missing.'
Assert-True ($response.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'Unexpected MarketDataResponse category.'
Assert-True ($response.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'Sanitized session reject reason missing.'
Assert-True ($response.sanitizedSessionRejectReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'Sanitized reject reason mirror missing.'
Assert-False $response.entriesObserved 'Entries should not be observed.'
Assert-True ($response.sanitizedEntryCount -eq 0) 'Entry count should be 0.'
Assert-True ($reporting.sessionRejectObservedWithSanitizedReason -eq $true) 'Session reject reporting missing.'
Assert-False $reporting.rawRejectTextSerialized 'Raw reject text serialized.'

Assert-True ($universe.approvedUniversePreserved -eq $true) 'Approved universe missing.'
Assert-True ((@($universe.approvedInstruments) -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved universe weakened.'
Assert-True ($universe.diagnosticRetryIsGbpusdOnly -eq $true) 'Diagnostic retry not GBPUSD-only.'
Assert-False $universe.nonApprovedInstrumentsAllowed 'Non-approved instruments allowed.'
Assert-True ($usdJpy.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat weakened.'
Assert-True ($usdJpy.usdJpySecurityId -eq '4004') 'USDJPY SecurityID weakened.'
Assert-True ($usdJpy.usdJpySecurityIdSource -eq '8') 'USDJPY SecurityIDSource weakened.'

Assert-True ($sanitized.credentialValuesReturned -eq $false) 'credentialValuesReturned must remain false.'
Assert-False $sanitized.rawFixSerialized 'Raw FIX serialized.'
Assert-False $sanitized.rawRejectTextSerialized 'Raw reject text serialized.'
Assert-False $sanitized.rawCredentialOrSessionMaterialSerialized 'Raw credential/session material serialized.'
Assert-True ($shutdown.shutdownRevertCompleted -eq $true) 'Shutdown/revert evidence missing.'
Assert-False $shutdown.secondAttemptRun 'Second attempt detected.'
Assert-True ($sanitization.audit -eq 'PASS') 'Sanitization audit did not pass.'
Assert-True ($forbidden.audit -eq 'PASS') 'Forbidden actions audit did not pass.'
Assert-True ($forbidden.attemptCount -eq 1) 'Forbidden audit attempt count must be 1.'
Assert-False $forbidden.apiStarted 'API started.'
Assert-False $forbidden.workerStarted 'Worker started.'
Assert-False $forbidden.ordersIntroduced 'Orders introduced.'
Assert-False $forbidden.tradingEnabled 'Trading enabled.'
Assert-False $forbidden.schedulerIntroduced 'Scheduler introduced.'
Assert-False $forbidden.pollingLoopIntroduced 'Polling introduced.'
Assert-False $forbidden.replayIntroduced 'Replay introduced.'
Assert-False $forbidden.shadowReplayIntroduced 'Shadow replay introduced.'
Assert-True ($apiWorker.audit -eq 'PASS') 'API/Worker audit did not pass.'
Assert-True ($apiWorker.apiWorkerGatewayMode -eq 'FakeLmaxGatewayOnly') 'API/Worker FakeLmaxGatewayOnly regressed.'

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R178') 'Next phase recommendation missing.'
Assert-True ($next.r178MustBeNoExternal -eq $true) 'R178 no-external requirement missing.'
Assert-False $next.liveRetryRecommendedNow 'Live retry recommended despite inconsistent evidence.'
Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r177-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R177 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R177_VALIDATION_PASS'
