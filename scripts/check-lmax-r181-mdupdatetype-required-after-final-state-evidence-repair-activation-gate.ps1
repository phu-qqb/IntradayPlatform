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

$activation = Read-Json 'phase-lmax-r181-mdupdatetype-required-after-final-state-evidence-repair-activation.json'
$preflight = Read-Json 'phase-lmax-r181-preflight-result.json'
$selected = Read-Json 'phase-lmax-r181-selected-profile-evidence.json'
$state = Read-Json 'phase-lmax-r181-state-propagation-evidence.json'
$boundary = Read-Json 'phase-lmax-r181-boundary-evidence.json'
$request = Read-Json 'phase-lmax-r181-marketdata-request-evidence.json'
$response = Read-Json 'phase-lmax-r181-marketdata-response-evidence.json'
$reporting = Read-Json 'phase-lmax-r181-sessionreject-sanitized-reason-reporting.json'
$universe = Read-Json 'phase-lmax-r181-approved-universe-preservation.json'
$usdJpy = Read-Json 'phase-lmax-r181-usdjpy-caveat-preservation.json'
$sanitized = Read-Json 'phase-lmax-r181-sanitized-result.json'
$shutdown = Read-Json 'phase-lmax-r181-shutdown-revert-evidence.json'
$sanitization = Read-Json 'phase-lmax-r181-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r181-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r181-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r181-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r181-gate-validation.json'

foreach ($name in @(
    'phase-lmax-r179-final-state-evidence-repaired-retry-readiness.json',
    'phase-lmax-r179-final-state-evidence-contract-readiness.json',
    'phase-lmax-r179-r181-expected-evidence-contract.json',
    'phase-lmax-r179-gate-validation.json')) {
    Assert-True (Test-Path (Join-Path $artifactRoot $name)) "R179 evidence missing: $name"
}

$r179 = Read-Json 'phase-lmax-r179-final-state-evidence-repaired-retry-readiness.json'
$r179Contract = Read-Json 'phase-lmax-r179-final-state-evidence-contract-readiness.json'
$r179Expected = Read-Json 'phase-lmax-r179-r181-expected-evidence-contract.json'
$r179Gate = Read-Json 'phase-lmax-r179-gate-validation.json'

Assert-True ($r179.classification -eq 'LMAX_R179_PASS_FINAL_STATE_EVIDENCE_REPAIRED_RETRY_READINESS_NO_EXTERNAL') 'R179 readiness classification missing.'
Assert-True ($r179.nextActivationPhase -eq 'LMAX-R181') 'R179 did not reserve R181.'
Assert-True ($r179Contract.futureArtifactsMustFailOnClassifiedResponseWithAllExplicitStateFieldsFalse -eq $true) 'R179 final-state all-false failure rule missing.'
Assert-False $r179Expected.classifiedMarketDataResponseAllFalseStateAllowed 'R179 expected contract allows all-false classified response.'
Assert-True ($r179Gate.validatorResult -eq 'LMAX_R179_VALIDATION_PASS') 'R179 validator evidence missing.'

Assert-True ($activation.phase -eq 'LMAX-R181') 'Activation phase mismatch.'
Assert-True ($activation.classification -eq 'LMAX_R181_FAIL_SESSIONREJECT_WITH_SANITIZED_REASON_REPORTED') 'Unexpected R181 classification.'
Assert-True ($activation.externalActivationAttempted -eq $true) 'R181 external activation was not recorded.'
Assert-True ($activation.attemptCount -eq 1) 'R181 attemptCount must be exactly 1.'
Assert-True ($activation.operatorApprovalPresent -eq $true) 'Operator approval missing.'
Assert-True ($activation.operatorApprovalExactMatch -eq $true) 'Operator approval exact match missing.'
Assert-True ($activation.marketHoursConfirmationPresent -eq $true) 'Market-hours confirmation missing.'
Assert-True ($activation.marketHoursWindow -eq 'Monday, May 18, 2026 at 20:10 Europe/Paris') 'Market-hours window mismatch.'
Assert-True ($activation.selectedProfile -eq 'UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument') 'Selected profile mismatch.'
Assert-True ($activation.selectedDiagnosticProfileCount -eq 1) 'Multiple diagnostic profiles selected.'
Assert-True ($activation.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'MarketDataResponse category mismatch.'
Assert-True ($activation.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'sessionRejectSanitizedReasonCategory mismatch.'
Assert-True ($activation.sanitizedSessionRejectReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'sanitizedSessionRejectReasonCategory mismatch.'
Assert-False $activation.entriesObserved 'Entries unexpectedly observed.'
Assert-True ($activation.entryCount -eq 0) 'Entry count must be 0.'
Assert-True ($activation.stateReportingFieldsConsistentWithBoundaryEvidence -eq $true) 'State reporting fields are inconsistent.'
Assert-True ($activation.shutdownRevert -eq 'Succeeded') 'Shutdown/revert did not succeed.'

Assert-True ($preflight.preflightPassed -eq $true) 'Preflight should pass.'
Assert-True ($preflight.operatorApprovalExactMatchPresent -eq $true) 'Exact approval should be present.'
Assert-True ($preflight.separateConcreteMarketHoursConfirmationPresent -eq $true) 'Concrete market-hours should be present.'
Assert-True ($preflight.operatorConfirmedMarketHoursWindow -eq 'Monday, May 18, 2026 at 20:10 Europe/Paris') 'Preflight market-hours window mismatch.'
Assert-True ($preflight.externalBoundaryCrossed -eq $true) 'External boundary not crossed after approved preflight.'
Assert-True ($preflight.attemptCount -eq 1) 'Preflight attempt count must be 1.'

Assert-True ($selected.selectedProfileEvidencePresent -eq $true) 'Selected profile evidence missing.'
Assert-True ($selected.selectedProfile -eq 'UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument') 'Profile not selected.'
Assert-True ($selected.selectedProfileExecuted -eq $true) 'Selected profile was not executed.'
Assert-True ($selected.gbpusdOnly -eq $true) 'Future retry is not GBPUSD-only.'
Assert-True ($selected.singleRequest -eq $true) 'Future retry has more than one request.'
Assert-True ($selected.securityId -eq '4002') 'SecurityID=4002 missing.'
Assert-True ($selected.securityIdSource -eq '8') 'SecurityIDSource=8 missing.'
Assert-True ($selected.subscriptionRequestType -eq '1') 'SubscriptionRequestType=1 missing.'
Assert-True ($selected.mdUpdateType -eq '0') 'MDUpdateType=0 missing.'
Assert-True ($selected.marketDepth -eq 1) 'MarketDepth=1 missing.'
Assert-True ($selected.noMdEntryTypes -eq 2) 'NoMDEntryTypes=2 missing.'
Assert-True ($selected.bidAndOfferTogether -eq $true) 'Bid+Offer together missing.'
Assert-False $selected.symbolTextPresent 'Symbol text present.'
Assert-False $selected.internalSymbolPresent 'InternalSymbol present.'
Assert-False $selected.snapshotOnlyPresent 'SnapshotOnly present.'
Assert-False $selected.subscriptionRequestTypeZeroPresent 'SubscriptionRequestType=0 present.'

Assert-True ($state.attemptExecuted -eq $true) 'State evidence indicates no executed attempt.'
Assert-True ($state.marketDataRequestWriteAttempted -eq $true) 'WriteAttempted missing.'
Assert-True ($state.marketDataRequestWriteSucceeded -eq $true) 'WriteSucceeded missing.'
Assert-True ($state.marketDataRequestResponseReadAttempted -eq $true) 'ResponseReadAttempted missing.'
Assert-True ($state.marketDataRequestReachedBoundedResponseClassification -eq $true) 'ReachedBoundedResponseClassification missing.'
Assert-False $state.marketDataRequestSentLegacyFlag 'Legacy sent flag expected false compatibility marker.'
Assert-True ($state.classifiedMarketDataResponseObserved -eq $true) 'Classified response not observed.'
Assert-False $state.classifiedMarketDataResponseAllFalseStateAllowed 'Classified all-false state must not be allowed.'
Assert-True ($state.stateFieldsConsistentWithBoundaryEvidence -eq $true) 'State fields inconsistent with boundary evidence.'
Assert-False $state.stateFieldsWereDerivedFromBoundaryEvidence 'State fields should be propagated, not derived from boundary evidence.'
Assert-False $state.stateFieldsContradictionDetected 'State contradiction detected.'

$allExplicitFalse = (-not $state.marketDataRequestWriteAttempted) -and
    (-not $state.marketDataRequestWriteSucceeded) -and
    (-not $state.marketDataRequestResponseReadAttempted) -and
    (-not $state.marketDataRequestReachedBoundedResponseClassification)
Assert-False (($response.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') -and $allExplicitFalse) 'Classified MarketDataResponse emitted all explicit state fields false.'

Assert-True ($boundary.externalBoundaryCrossed -eq $true) 'External boundary not crossed.'
Assert-True ($boundary.tcpSocket -eq 'Succeeded') 'TCP did not succeed.'
Assert-True ($boundary.tls -eq 'Succeeded') 'TLS did not succeed.'
Assert-True ($boundary.tlsStreamAvailableForFix -eq $true) 'TLS stream unavailable for FIX.'
Assert-True ($boundary.fixLogonSession -eq 'Succeeded') 'FIX did not succeed.'
Assert-True ($boundary.fixAcknowledgement -eq 'FixLogonAcknowledged') 'FIX acknowledgement missing.'
Assert-True ($boundary.marketDataRequest -eq 'WriteSucceededAndReachedBoundedResponseClassificationAfterFixSuccess') 'MarketDataRequest boundary mismatch.'
Assert-True ($boundary.marketDataResponseRead -eq 'ReachedSanitizedClassification') 'MarketDataResponse read boundary mismatch.'
Assert-True ($boundary.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'Boundary response category mismatch.'

Assert-True ($request.marketDataRequestAttempted -eq $true) 'MarketDataRequest not attempted.'
Assert-True ($request.marketDataRequestWriteAttempted -eq $true) 'Request write attempted missing.'
Assert-True ($request.marketDataRequestWriteSucceeded -eq $true) 'Request write succeeded missing.'
Assert-True ($request.marketDataRequestResponseReadAttempted -eq $true) 'Request response read attempted missing.'
Assert-True ($request.marketDataRequestReachedBoundedResponseClassification -eq $true) 'Request bounded classification missing.'
Assert-False $request.rawFixSerialized 'Raw FIX serialized.'

Assert-True ($response.marketDataResponseReadAttempted -eq $true) 'MarketDataResponse read not attempted.'
Assert-True ($response.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'Unexpected MarketDataResponse category.'
Assert-True ($response.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'Response sanitized reason mismatch.'
Assert-True ($response.sanitizedSessionRejectReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'Response sanitized reason reporting mismatch.'
Assert-False $response.entriesObserved 'Entries unexpectedly observed.'
Assert-True ($response.entryCount -eq 0) 'Entry count must be 0.'
Assert-False $response.rawRejectTextSerialized 'Raw reject text serialized.'

Assert-True ($reporting.r133R135ReportingPreserved -eq $true) 'R133/R135 reporting not preserved.'
Assert-True ($reporting.sessionRejectObserved -eq $true) 'SessionReject should be observed.'
Assert-True ($reporting.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'Reporting sanitized reason mismatch.'
Assert-True ($reporting.sanitizedSessionRejectReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'Reporting sanitized reason category mismatch.'
Assert-False $reporting.rawRejectTextSerialized 'Raw reject text serialized.'

Assert-True ($universe.approvedUniversePreserved -eq $true) 'Approved universe missing.'
Assert-True ((@($universe.approvedInstruments) -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved universe weakened.'
Assert-True ($universe.diagnosticRetryIsGbpusdOnly -eq $true) 'Diagnostic retry not GBPUSD-only.'
Assert-False $universe.nonApprovedInstrumentsAllowed 'Non-approved instruments allowed.'
Assert-True ($usdJpy.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat weakened.'
Assert-True ($usdJpy.usdJpySecurityId -eq '4004') 'USDJPY SecurityID weakened.'
Assert-True ($usdJpy.usdJpySecurityIdSource -eq '8') 'USDJPY SecurityIDSource weakened.'

Assert-True ($sanitized.classification -eq $activation.classification) 'Sanitized result classification mismatch.'
Assert-True ($sanitized.externalActivationAttempted -eq $true) 'Sanitized result missing external attempt.'
Assert-True ($sanitized.attemptCount -eq 1) 'Sanitized result attemptCount must be 1.'
Assert-True ($sanitized.credentialValuesReturned -eq $false) 'credentialValuesReturned must be false.'
Assert-True ($sanitized.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'Sanitized response category mismatch.'

Assert-True ($shutdown.shutdownRevertRequired -eq $true) 'Shutdown/revert should be required.'
Assert-True ($shutdown.shutdownRevertStatus -eq 'Succeeded') 'Shutdown/revert status mismatch.'
Assert-True ($shutdown.externalBoundaryCrossed -eq $true) 'Shutdown evidence should record external boundary.'

Assert-True ($sanitization.audit -eq 'PASS') 'Sanitization audit did not pass.'
Assert-True ($sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned must remain false.'
Assert-False $sanitization.rawFixSerialized 'Raw FIX serialized.'
Assert-False $sanitization.rawRejectTextSerialized 'Raw reject text serialized.'
Assert-False $sanitization.rawCredentialsSerialized 'Raw credentials serialized.'
Assert-False $sanitization.rawSessionIdentifiersSerialized 'Raw session IDs serialized.'
Assert-False $sanitization.rawCompIdsSerialized 'Raw CompIDs serialized.'
Assert-False $sanitization.rawEndpointSerialized 'Raw endpoint serialized.'
Assert-False $sanitization.rawTlsMaterialSerialized 'Raw TLS material serialized.'

Assert-True ($forbidden.audit -eq 'PASS') 'Forbidden actions audit did not pass.'
Assert-True ($forbidden.externalActivationAttempted -eq $true) 'External attempt not recorded.'
Assert-True ($forbidden.socketOpened -eq $true) 'Socket boundary not recorded.'
Assert-True ($forbidden.tlsOpened -eq $true) 'TLS boundary not recorded.'
Assert-True ($forbidden.fixOpened -eq $true) 'FIX boundary not recorded.'
Assert-True ($forbidden.marketDataRuntimeActionPerformed -eq $true) 'MarketData boundary not recorded.'
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

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R182') 'Next recommendation should be R182.'
Assert-True ($next.r182MustBeNoExternalReviewGate -eq $true) 'R182 must be no-external review gate.'
Assert-False $next.anotherLiveRetryRecommended 'Another live retry recommended.'

Assert-True ($gate.buildResult -match '^PASS') 'Build evidence missing.'
Assert-True ($gate.focusedTests -match '^PASS') 'Focused test evidence missing.'
Assert-True ($gate.unitTests -match '^PASS') 'Unit test evidence missing.'
Assert-True ($gate.integrationTests -match '^PASS') 'Integration test evidence missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R181_VALIDATION_PASS') 'Validator evidence missing.'

$artifactText = Get-ChildItem $artifactRoot -Filter 'phase-lmax-r181-*' -File |
    Where-Object { $_.Extension -in '.json', '.md', '.txt' -and $_.Name -notlike '*operator-approval*' } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
foreach ($forbiddenToken in @('password=', 'username=', '35=V', '35=A', '8=FIX.4.4', '58=', '')) {
    Assert-False ($joined.Contains($forbiddenToken)) "Potential raw sensitive/FIX/reject token serialized in R181 artifacts: $forbiddenToken"
}

Write-Output 'LMAX_R181_VALIDATION_PASS'
