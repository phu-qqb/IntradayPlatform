param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Read-Json {
    param([string]$Name)
    $path = Join-Path $script:ArtifactRoot $Name
    Assert-True (Test-Path -LiteralPath $path) "Missing artifact: $Name"
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$script:ArtifactRoot = Join-Path $Root 'artifacts/readiness/lmax-runtime-enablement'
$profile = 'UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument'

Assert-True (Test-Path -LiteralPath (Join-Path $ArtifactRoot 'phase-lmax-r199-mdreqid-repaired-activation-summary.md')) 'R199 summary missing.'

$activation = Read-Json 'phase-lmax-r199-mdreqid-repaired-activation.json'
$preflight = Read-Json 'phase-lmax-r199-preflight-result.json'
$selected = Read-Json 'phase-lmax-r199-selected-profile-evidence.json'
$mdReqId = Read-Json 'phase-lmax-r199-mdreqid-shape-evidence.json'
$state = Read-Json 'phase-lmax-r199-state-evidence.json'
$detail = Read-Json 'phase-lmax-r199-sessionreject-tag-detail-evidence.json'
$boundary = Read-Json 'phase-lmax-r199-boundary-evidence.json'
$request = Read-Json 'phase-lmax-r199-marketdata-request-evidence.json'
$response = Read-Json 'phase-lmax-r199-marketdata-response-evidence.json'
$sanitized = Read-Json 'phase-lmax-r199-sanitized-result.json'
$shutdown = Read-Json 'phase-lmax-r199-shutdown-revert-evidence.json'
$approvedUniverse = Read-Json 'phase-lmax-r199-approved-universe-preservation.json'
$usdJpy = Read-Json 'phase-lmax-r199-usdjpy-caveat-preservation.json'
$sanitization = Read-Json 'phase-lmax-r199-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r199-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r199-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r199-next-phase-recommendation.json'
$r197 = Read-Json 'phase-lmax-r197-mdreqid-value-repair.json'
$r198 = Read-Json 'phase-lmax-r198-mdreqid-repaired-retry-readiness.json'

Assert-True ($activation.classification -in @(
    'LMAX_R199_PASS_MDREQID_REPAIRED_RUNTIME_ACTIVATION_SANITIZED',
    'LMAX_R199_FAIL_SESSIONREJECT_WITH_TAG_DETAIL_REPORTED',
    'LMAX_R199_FAIL_SESSIONREJECT_WITH_SANITIZED_REASON_REPORTED',
    'LMAX_R199_FAIL_MARKETDATAREQUESTREJECT_WITH_SANITIZED_REASON_REPORTED',
    'LMAX_R199_FAIL_MARKETDATA_RESPONSE_BOUNDARY',
    'LMAX_R199_FAIL_STATE_REPORTING_FIELDS_MISSING_OR_INCONSISTENT',
    'LMAX_R199_FAIL_REJECT_DETAIL_EXTRACTION_MISSING',
    'LMAX_R199_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE',
    'LMAX_R199_FAIL_SHUTDOWN_REVERT_INCOMPLETE',
    'LMAX_R199_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED',
    'LMAX_R199_FAIL_MARKET_HOURS_CONSTRAINT_MISSING_OR_VIOLATED',
    'LMAX_R199_FAIL_PROFILE_NOT_SELECTED',
    'LMAX_R199_FAIL_MDREQID_REPAIR_CONTRACT_WEAKENED',
    'LMAX_R199_FAIL_RAW_MDREQID_LEAK_RISK',
    'LMAX_R199_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK',
    'LMAX_R199_FAIL_FORBIDDEN_ACTION_RISK',
    'LMAX_R199_FAIL_API_WORKER_GATEWAY_REGRESSION',
    'LMAX_R199_FAIL_BUILD_OR_TESTS')) 'R199 classification is not allowed.'
Assert-True ($activation.classification -eq 'LMAX_R199_PASS_MDREQID_REPAIRED_RUNTIME_ACTIVATION_SANITIZED') 'R199 classification mismatch.'
Assert-True ($r197.classification -eq 'LMAX_R197_PASS_MDREQID_VALUE_REPAIR_READY_NO_EXTERNAL') 'R197 evidence missing.'
Assert-True ($r198.classification -eq 'LMAX_R198_PASS_MDREQID_REPAIRED_RETRY_READINESS_NO_EXTERNAL') 'R198 readiness missing.'

Assert-True ($preflight.preflightPassed -eq $true) 'R199 preflight did not pass.'
Assert-True ($preflight.approvalPresent -eq $true) 'R199 approval missing.'
Assert-True ($preflight.approvalExactFreshMatch -eq $true) 'R199 approval mismatch.'
Assert-True ($preflight.marketHoursConfirmationPresent -eq $true) 'R199 market-hours confirmation missing.'
Assert-True ($preflight.marketHoursConfirmationConcrete -eq $true) 'R199 market-hours confirmation not concrete.'
Assert-True ($preflight.marketHoursWindow -eq 'Tuesday, May 19, 2026 at 12:26 Europe/Paris') 'R199 market-hours window mismatch.'

Assert-True ($activation.externalActivationAttempted -eq $true) 'R199 external activation was not attempted.'
Assert-True ($activation.attemptCount -eq 1) 'R199 attemptCount must be exactly one.'
Assert-True ($activation.preExternalReservationRejectionCrossedExternalBoundary -eq $false) 'Pre-external reservation rejection crossed external boundary.'
Assert-True ($forbidden.attemptCount -eq 1) 'R199 forbidden audit attemptCount mismatch.'

Assert-True ($selected.selectedProfile -eq $profile) 'R199 selected profile mismatch.'
Assert-True ($selected.profileExecuted -eq $true) 'R199 profile was not executed.'
Assert-True ($selected.selectedDiagnosticProfileCount -eq 1) 'R199 selected more than one diagnostic profile.'
Assert-True ($selected.gbpUsdOnly -eq $true) 'R199 is not GBPUSD-only.'
Assert-True ($selected.singleRequest -eq $true) 'R199 request is not single-request.'
Assert-True ($selected.repairedMdReqIdShapeUsed -eq $true) 'Repaired MDReqID shape not used.'
Assert-True ($selected.mdReqIdMaxLength -le 16) 'MDReqID max length weakened.'
Assert-True ($selected.mdReqIdAlphanumericOnly -eq $true) 'MDReqID alphanumeric contract weakened.'
Assert-True ($selected.mdReqIdNoPhaseLabel -eq $true) 'MDReqID phase-label contract weakened.'
Assert-True ($selected.mdReqIdNoUnderscore -eq $true) 'MDReqID underscore contract weakened.'
Assert-True ($selected.mdReqIdNoPunctuation -eq $true) 'MDReqID punctuation contract weakened.'
Assert-True ($selected.rawMdReqIdSerialized -eq $false) 'Raw MDReqID serialized.'
Assert-True ($mdReqId.repairedMdReqIdContractPreserved -eq $true) 'MDReqID contract not preserved.'
Assert-True ($mdReqId.rawMdReqIdSerialized -eq $false) 'Raw MDReqID serialized in shape evidence.'
Assert-True ($mdReqId.lengthLessThanOrEqual16 -eq $true) 'MDReqID length contract weakened.'
Assert-True ($mdReqId.containsPhaseLabel -eq $false) 'MDReqID phase label present.'
Assert-True ($mdReqId.containsUnderscore -eq $false) 'MDReqID underscore present.'
Assert-True ($mdReqId.containsPunctuation -eq $false) 'MDReqID punctuation present.'

Assert-True ($selected.securityId -eq '4002') 'SecurityID=4002 missing.'
Assert-True ($selected.securityIdSource -eq '8') 'SecurityIDSource=8 missing.'
Assert-True ($selected.subscriptionRequestType -eq '1') 'SubscriptionRequestType=1 missing.'
Assert-True ($selected.mdUpdateType -eq '0') 'MDUpdateType=0 missing.'
Assert-True ($selected.marketDepth -eq 1) 'MarketDepth=1 missing.'
Assert-True ($selected.noMdEntryTypes -eq 2) 'NoMDEntryTypes=2 missing.'
Assert-True ($selected.bidAndOfferTogether -eq $true) 'Bid+offer together missing.'
Assert-True ($selected.symbolTextPresent -eq $false) 'Symbol text included.'
Assert-True ($selected.internalSymbolPresent -eq $false) 'InternalSymbol included.'
Assert-True ($selected.snapshotOnlyPresent -eq $false) 'SnapshotOnly included.'
Assert-True ($selected.subscriptionRequestTypeZeroPresent -eq $false) 'SubscriptionRequestType=0 included.'

Assert-True ($state.marketDataRequestWriteAttempted -eq $true) 'Write attempted should be true.'
Assert-True ($state.marketDataRequestWriteSucceeded -eq $true) 'Write succeeded should be true.'
Assert-True ($state.marketDataRequestResponseReadAttempted -eq $true) 'Response read attempted should be true.'
Assert-True ($state.marketDataRequestReachedBoundedResponseClassification -eq $true) 'Bounded response classification should be true.'
Assert-True ($state.marketDataRequestSentLegacyFlag -eq $false) 'Legacy flag should remain compatibility-only false.'
Assert-True ($state.stateFieldsConsistent -eq $true) 'State fields are inconsistent.'
Assert-True ($state.stateFieldsContradictionDetected -eq $false) 'State contradiction detected.'

Assert-True ($boundary.tcpSocket -eq 'Succeeded') 'TCP/socket boundary missing.'
Assert-True ($boundary.tls -eq 'Succeeded') 'TLS boundary missing.'
Assert-True ($boundary.tlsStreamAvailableForFix -eq $true) 'TLS stream for FIX missing.'
Assert-True ($boundary.fixLogonSession -eq 'Succeeded') 'FIX logon/session boundary missing.'
Assert-True ($boundary.fixAcknowledgement -eq 'FixLogonAcknowledged') 'FIX acknowledgement missing.'
Assert-True ($boundary.marketDataRequest -eq 'WriteSucceededAndReachedBoundedResponseClassificationAfterFixSuccess') 'MarketDataRequest boundary mismatch.'
Assert-True ($boundary.marketDataResponse -eq 'ReachedSanitizedClassification') 'MarketDataResponse boundary mismatch.'
Assert-True ($boundary.marketDataResponseCategory -eq 'Succeeded') 'MarketDataResponse category mismatch.'
Assert-True ($boundary.marketDataRequestBeforeFixSuccess -eq $false) 'MarketDataRequest occurred before FIX success.'
Assert-True ($boundary.marketDataResponseReadBeforeMarketDataRequestSuccess -eq $false) 'MarketDataResponse read occurred before MarketDataRequest success.'

Assert-True ($request.requestSent -eq $true) 'MarketDataRequest should be sent.'
Assert-True ($request.writeAttempted -eq $true) 'MarketDataRequest write attempted missing.'
Assert-True ($request.writeSucceeded -eq $true) 'MarketDataRequest write succeeded missing.'
Assert-True ($request.rawFixSerialized -eq $false) 'Raw FIX serialized in request evidence.'
Assert-True ($request.rawMdReqIdSerialized -eq $false) 'Raw MDReqID serialized in request evidence.'

Assert-True ($response.responseReadAttempted -eq $true) 'MarketDataResponse read should be attempted.'
Assert-True ($response.boundedResponseClassificationReached -eq $true) 'Bounded response classification missing.'
Assert-True ($response.marketDataResponseCategory -eq 'Succeeded') 'Response category mismatch.'
Assert-True ($response.sessionRejectSanitizedReasonCategory -eq 'None') 'Session reject sanitized reason should be None.'
Assert-True ($response.sanitizedSessionRejectReasonCategory -eq 'None') 'Sanitized session reject reason should be None.'
Assert-True ($response.rejectReasonExtractionSource -eq 'NoRejectObserved') 'Reject extraction source mismatch.'
Assert-True ($response.rawRejectTextSerialized -eq $false) 'Raw reject text serialized.'
Assert-True ($response.rawFixSerialized -eq $false) 'Raw FIX serialized.'

Assert-True ($detail.rejectObserved -eq $false) 'Reject should not be observed in R199 success evidence.'
Assert-True ($detail.rejectReasonExtractionSource -eq 'NoRejectObserved') 'Detail extraction source mismatch.'
Assert-True ($detail.notAvailableDistinctFromPropagationFailure -eq $true) 'NotAvailable/propagation distinction missing.'
Assert-True ($detail.propagationFailureDetected -eq $false) 'Propagation failure detected.'
Assert-True ($detail.rawTagValuesSerialized -eq $false) 'Raw tag values serialized.'

Assert-True ($shutdown.shutdownRevertRequired -eq $true) 'Shutdown/revert should be required.'
Assert-True ($shutdown.shutdownRevertCompleted -eq $true) 'Shutdown/revert missing.'
Assert-True ($shutdown.shutdownRevertStatus -eq 'Succeeded') 'Shutdown/revert did not succeed.'
Assert-True ($shutdown.attemptStoppedAfterSingleBoundedRun -eq $true) 'Attempt did not stop after one bounded run.'

Assert-True ($approvedUniverse.approvedUniversePreserved -eq $true) 'Approved universe weakened.'
foreach ($symbol in @('GBPUSD', 'EURGBP', 'AUDUSD', 'USDJPY')) {
    Assert-True ($approvedUniverse.approvedInstruments -contains $symbol) "Approved instrument missing: $symbol"
}
Assert-True ($approvedUniverse.nonApprovedInstrumentRequested -eq $false) 'Non-approved instrument requested.'
Assert-True ($usdJpy.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat weakened.'
Assert-True ($usdJpy.usdJpySecurityId -eq '4004') 'USDJPY SecurityID weakened.'
Assert-True ($usdJpy.usdJpySecurityIdSource -eq '8') 'USDJPY SecurityIDSource weakened.'

Assert-True ($forbidden.externalActivationAttempted -eq $true) 'R199 forbidden audit did not record approved external attempt.'
Assert-True ($forbidden.externalAttemptApproved -eq $true) 'R199 external attempt was not approved.'
Assert-True ($forbidden.preExternalRejectedInvocationCrossedExternalBoundary -eq $false) 'Pre-external rejection crossed boundary.'
Assert-True ($forbidden.socketOpened -eq $true) 'R199 socket boundary missing.'
Assert-True ($forbidden.tlsOpened -eq $true) 'R199 TLS boundary missing.'
Assert-True ($forbidden.fixSessionOpened -eq $true) 'R199 FIX boundary missing.'
Assert-True ($forbidden.liveMarketDataRequestSent -eq $true) 'R199 MarketDataRequest boundary missing.'
Assert-True ($forbidden.liveMarketDataResponseRead -eq $true) 'R199 MarketDataResponse boundary missing.'
Assert-True ($forbidden.apiStarted -eq $false) 'API startup detected.'
Assert-True ($forbidden.workerStarted -eq $false) 'Worker startup detected.'
Assert-True ($forbidden.ordersIntroduced -eq $false) 'Order path introduced.'
Assert-True ($forbidden.tradingEnabled -eq $false) 'Trading enabled.'
Assert-True ($forbidden.schedulerStarted -eq $false) 'Scheduler started.'
Assert-True ($forbidden.pollingStarted -eq $false) 'Polling started.'
Assert-True ($forbidden.serviceStarted -eq $false) 'Service started.'
Assert-True ($forbidden.replayIntroduced -eq $false) 'Replay introduced.'
Assert-True ($forbidden.shadowReplayIntroduced -eq $false) 'Shadow replay introduced.'
Assert-True ($forbidden.productionAccountUsed -eq $false) 'Production account used.'
Assert-True ($apiWorker.fakeLmaxGatewayOnlyPreserved -eq $true) 'API/Worker FakeLmaxGatewayOnly regressed.'
Assert-True ($apiWorker.liveGatewayIntroducedInApiOrWorker -eq $false) 'API/Worker live gateway introduced.'

Assert-True ($sanitization.rawFixMessagesSerialized -eq $false) 'Raw FIX serialization risk detected.'
Assert-True ($sanitization.rawRejectTextSerialized -eq $false) 'Raw reject text serialization risk detected.'
Assert-True ($sanitization.rawMdReqIdSerialized -eq $false) 'Raw MDReqID serialization risk detected.'
Assert-True ($sanitization.rawCredentialsSerialized -eq $false) 'Raw credential serialization risk detected.'
Assert-True ($sanitization.rawSessionIdsSerialized -eq $false) 'Raw session ID serialization risk detected.'
Assert-True ($sanitization.rawCompIdsSerialized -eq $false) 'Raw CompID serialization risk detected.'
Assert-True ($sanitization.rawEndpointValuesSerialized -eq $false) 'Raw endpoint serialization risk detected.'
Assert-True ($sanitization.rawTlsMaterialSerialized -eq $false) 'Raw TLS serialization risk detected.'
Assert-True ($sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned=false missing.'
Assert-True ($sanitized.credentialValuesReturned -eq $false) 'Sanitized result credentialValuesReturned=false missing.'

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R200') 'Next phase recommendation missing.'
Assert-True ($next.mustRemainNoExternal -eq $true) 'R200 must remain no-external.'
Assert-True ($next.doNotRecommendBlindLiveRetry -eq $true) 'Next phase must not recommend a blind live retry.'

$artifactText = Get-ChildItem -LiteralPath $ArtifactRoot -Filter 'phase-lmax-r199-*' -File |
    Where-Object { $_.Name -notin @('phase-lmax-r199-operator-approval.txt', 'phase-lmax-r199-expected-operator-approval.txt', 'phase-lmax-r199-operator-approval-note.md') } |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combinedArtifactText = [string]::Join("`n", $artifactText)
foreach ($forbiddenToken in @('35=V', '35=Y', '35=3', '262=', '281=', '371=', '372=', '373=', '58=', [string][char]1, 'password=', 'username=', 'SenderCompId', 'TargetCompId', 'LMAX_READONLY_')) {
    Assert-True (-not $combinedArtifactText.Contains($forbiddenToken)) "Forbidden serialized token detected in R199 artifacts: $forbiddenToken"
}

$gate = Read-Json 'phase-lmax-r199-gate-validation.json'
Assert-True ($gate.build -like 'PASS*') 'Build evidence missing.'
Assert-True ($gate.focusedTests -like 'PASS*') 'Focused test evidence missing.'
Assert-True ($gate.validator -eq 'LMAX_R199_VALIDATION_PASS') 'Validator evidence missing.'

'LMAX_R199_VALIDATION_PASS'
