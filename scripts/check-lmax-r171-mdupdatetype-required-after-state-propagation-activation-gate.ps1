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

$activation = Read-Json 'phase-lmax-r171-mdupdatetype-required-after-state-propagation-activation.json'
$preflight = Read-Json 'phase-lmax-r171-preflight-result.json'
$selected = Read-Json 'phase-lmax-r171-selected-profile-evidence.json'
$state = Read-Json 'phase-lmax-r171-state-propagation-evidence.json'
$nonSelected = Read-Json 'phase-lmax-r171-non-selected-profiles-evidence.json'
$boundary = Read-Json 'phase-lmax-r171-boundary-evidence.json'
$request = Read-Json 'phase-lmax-r171-marketdata-request-evidence.json'
$response = Read-Json 'phase-lmax-r171-marketdata-response-evidence.json'
$reporting = Read-Json 'phase-lmax-r171-sessionreject-sanitized-reason-reporting.json'
$universe = Read-Json 'phase-lmax-r171-approved-universe-preservation.json'
$usdJpy = Read-Json 'phase-lmax-r171-usdjpy-caveat-preservation.json'
$sanitized = Read-Json 'phase-lmax-r171-sanitized-result.json'
$shutdown = Read-Json 'phase-lmax-r171-shutdown-revert-evidence.json'
$sanitization = Read-Json 'phase-lmax-r171-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r171-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r171-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r171-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r171-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r168-r167-state-propagation-review.json',
    'phase-lmax-r169-state-propagation-repaired-retry-readiness.json',
    'phase-lmax-r169-gate-validation.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "Required prior evidence missing: $name"
}

$r168 = Read-Json 'phase-lmax-r168-r167-state-propagation-review.json'
$r169 = Read-Json 'phase-lmax-r169-state-propagation-repaired-retry-readiness.json'
Assert-True ($r168.classification -eq 'LMAX_R168_PASS_STATE_PROPAGATION_REPAIR_IMPLEMENTED_NO_EXTERNAL') 'R168 repair evidence missing.'
Assert-True ($r169.classification -eq 'LMAX_R169_PASS_STATE_PROPAGATION_REPAIRED_RETRY_READINESS_NO_EXTERNAL') 'R169 readiness evidence missing.'

Assert-True ($activation.classification -eq 'LMAX_R171_FAIL_STATE_REPORTING_FIELDS_MISSING_OR_INCONSISTENT') 'R171 classification mismatch.'
Assert-True ($activation.externalActivationAttempted -eq $true) 'R171 external activation was not recorded.'
Assert-True ($activation.attemptCount -eq 1) 'R171 attemptCount must be exactly 1.'
Assert-True ($activation.operatorApprovalExactMatch -eq $true) 'Operator approval mismatch.'
Assert-True ($activation.weekdayMarketHoursConfirmationConcreteTime -eq $true) 'Concrete market-hours confirmation missing.'
Assert-False $activation.weekdayMarketHoursConfirmationUsesPlaceholder 'Placeholder market-hours time used.'
Assert-True ($activation.operatorConfirmedMarketHoursWindow -eq 'Monday, May 18, 2026 at 18:31 Europe/Paris') 'Market-hours window mismatch.'

Assert-True ($preflight.preflightPassed -eq $true) 'Preflight did not pass.'
Assert-True ($preflight.externalBoundaryAllowed -eq $true) 'External boundary was not allowed after valid approval/time.'
Assert-True ($preflight.externalActivationAttempted -eq $true) 'Preflight did not record external activation.'
Assert-True ($preflight.attemptCount -eq 1) 'Preflight attempt count must be 1.'

Assert-True ($selected.selectedProfileEvidencePresent -eq $true) 'Selected profile evidence missing.'
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
Assert-False $selected.subscriptionRequestTypeZeroPresent 'SubscriptionRequestType=0 present.'

Assert-True ($state.statePropagationEvidencePresent -eq $true) 'State propagation evidence missing.'
foreach ($field in @(
    'marketDataRequestWriteAttempted',
    'marketDataRequestWriteSucceeded',
    'marketDataRequestResponseReadAttempted',
    'marketDataRequestReachedBoundedResponseClassification',
    'marketDataRequestSentLegacyFlag')) {
    Assert-True (@($state.requiredExplicitStateFields) -contains $field) "Required explicit state field missing: $field"
}
Assert-True ($state.marketDataBoundaryClassificationObserved -eq $true) 'Boundary classification evidence missing.'
Assert-False $state.stateFieldsConsistentWithBoundaryEvidence 'State fields should be recorded as inconsistent for R171.'
Assert-False $state.marketDataRequestWriteAttempted 'WriteAttempted unexpectedly true in the inconsistent evidence case.'
Assert-False $state.marketDataRequestWriteSucceeded 'WriteSucceeded unexpectedly true in the inconsistent evidence case.'
Assert-False $state.marketDataRequestResponseReadAttempted 'ResponseReadAttempted unexpectedly true in the inconsistent evidence case.'
Assert-False $state.marketDataRequestReachedBoundedResponseClassification 'ReachedBoundedResponseClassification unexpectedly true in the inconsistent evidence case.'

Assert-True ($nonSelected.nonSelectedProfilesEvidencePresent -eq $true) 'Non-selected profile evidence missing.'
Assert-False $nonSelected.multipleDiagnosticProfilesSelected 'Multiple diagnostic profiles selected.'

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

Assert-True ($sanitized.classification -eq 'LMAX_R171_FAIL_STATE_REPORTING_FIELDS_MISSING_OR_INCONSISTENT') 'Sanitized result classification mismatch.'
Assert-True ($sanitized.credentialValuesReturned -eq $false) 'credentialValuesReturned must remain false.'
Assert-False $sanitized.rawFixSerialized 'Raw FIX serialized.'
Assert-False $sanitized.rawRejectTextSerialized 'Raw reject text serialized.'
Assert-False $sanitized.rawCredentialOrSessionMaterialSerialized 'Raw credential/session material serialized.'
Assert-True ($shutdown.shutdownRevertCompleted -eq $true) 'Shutdown/revert evidence missing.'
Assert-True ($shutdown.externalStateCreated -eq $true) 'External state was expected after boundary crossing.'
Assert-False $shutdown.secondAttemptRun 'Second attempt detected.'

Assert-True ($sanitization.audit -eq 'PASS') 'Sanitization audit did not pass.'
Assert-True ($sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned missing false.'
Assert-False $sanitization.rawFixSerialized 'Raw FIX serialized.'
Assert-False $sanitization.rawRejectTextSerialized 'Raw reject text serialized.'
Assert-False $sanitization.rawCredentialsSerialized 'Raw credentials serialized.'
Assert-False $sanitization.rawSessionIdentifiersSerialized 'Raw session IDs serialized.'
Assert-False $sanitization.rawCompIdsSerialized 'Raw CompIDs serialized.'
Assert-False $sanitization.rawEndpointSerialized 'Raw endpoint serialized.'
Assert-False $sanitization.rawTlsMaterialSerialized 'Raw TLS material serialized.'

Assert-True ($forbidden.audit -eq 'PASS') 'Forbidden actions audit did not pass.'
Assert-True ($forbidden.externalActivationAttempted -eq $true) 'Expected one approved external attempt missing.'
Assert-True ($forbidden.attemptCount -eq 1) 'Forbidden audit attempt count must be 1.'
Assert-True ($forbidden.socketOpened -eq $true) 'Socket boundary evidence missing.'
Assert-True ($forbidden.tlsOpened -eq $true) 'TLS boundary evidence missing.'
Assert-True ($forbidden.fixOpened -eq $true) 'FIX boundary evidence missing.'
Assert-True ($forbidden.marketDataRuntimeActionPerformed -eq $true) 'MarketData runtime action evidence missing.'
Assert-False $forbidden.apiStarted 'API started.'
Assert-False $forbidden.workerStarted 'Worker started.'
Assert-False $forbidden.ordersIntroduced 'Orders introduced.'
Assert-False $forbidden.tradingEnabled 'Trading enabled.'
Assert-False $forbidden.schedulerIntroduced 'Scheduler introduced.'
Assert-False $forbidden.pollingLoopIntroduced 'Polling introduced.'
Assert-False $forbidden.serviceIntroduced 'Service introduced.'
Assert-False $forbidden.replayIntroduced 'Replay introduced.'
Assert-False $forbidden.shadowReplayIntroduced 'Shadow replay introduced.'

Assert-True ($apiWorker.audit -eq 'PASS') 'API/Worker audit did not pass.'
Assert-True ($apiWorker.apiWorkerGatewayMode -eq 'FakeLmaxGatewayOnly') 'API/Worker FakeLmaxGatewayOnly regressed.'
Assert-False $apiWorker.apiStartupAttempted 'API startup attempted.'
Assert-False $apiWorker.workerStartupAttempted 'Worker startup attempted.'

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R172') 'Next phase recommendation missing.'
Assert-True ($next.r172MustBeNoExternal -eq $true) 'R172 no-external requirement missing.'
Assert-False $next.liveRetryRecommendedNow 'Live retry recommended despite inconsistent evidence.'
Assert-True ($next.requiresStatePropagationConsistencyReviewBeforeAnyFutureRetry -eq $true) 'State consistency review requirement missing.'

Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R171_VALIDATION_PASS') 'Validator evidence missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r171-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R171 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R171_VALIDATION_PASS'
