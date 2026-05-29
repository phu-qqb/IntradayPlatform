param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Read-Json {
    param([string]$Name)

    $path = Join-Path $script:ArtifactRoot $Name
    Assert-True (Test-Path -LiteralPath $path) "Missing artifact: $Name"
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$script:ArtifactRoot = Join-Path $Root 'artifacts/readiness/lmax-runtime-enablement'
$profile = 'UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument'

Assert-True (Test-Path -LiteralPath (Join-Path $ArtifactRoot 'phase-lmax-r195-sessionreject-tag-detail-activation-summary.md')) 'R195 summary missing.'

$activation = Read-Json 'phase-lmax-r195-sessionreject-tag-detail-activation.json'
$preflight = Read-Json 'phase-lmax-r195-preflight-result.json'
$selected = Read-Json 'phase-lmax-r195-selected-profile-evidence.json'
$state = Read-Json 'phase-lmax-r195-state-evidence.json'
$detail = Read-Json 'phase-lmax-r195-sessionreject-tag-detail-evidence.json'
$boundary = Read-Json 'phase-lmax-r195-boundary-evidence.json'
$request = Read-Json 'phase-lmax-r195-marketdata-request-evidence.json'
$response = Read-Json 'phase-lmax-r195-marketdata-response-evidence.json'
$sanitized = Read-Json 'phase-lmax-r195-sanitized-result.json'
$shutdown = Read-Json 'phase-lmax-r195-shutdown-revert-evidence.json'
$approvedUniverse = Read-Json 'phase-lmax-r195-approved-universe-preservation.json'
$usdJpy = Read-Json 'phase-lmax-r195-usdjpy-caveat-preservation.json'
$sanitization = Read-Json 'phase-lmax-r195-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r195-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r195-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r195-next-phase-recommendation.json'
$r193 = Read-Json 'phase-lmax-r193-sessionreject-tag-detail-enrichment.json'
$r194 = Read-Json 'phase-lmax-r194-sessionreject-tag-detail-retry-readiness.json'

Assert-True ($activation.classification -in @(
    'LMAX_R195_PASS_SESSIONREJECT_TAG_DETAIL_RUNTIME_ACTIVATION_SANITIZED',
    'LMAX_R195_FAIL_SESSIONREJECT_WITH_TAG_DETAIL_REPORTED',
    'LMAX_R195_FAIL_SESSIONREJECT_TAG_DETAIL_NOT_AVAILABLE',
    'LMAX_R195_FAIL_MARKETDATAREQUESTREJECT_WITH_SANITIZED_REASON_REPORTED',
    'LMAX_R195_FAIL_REJECT_DETAIL_EXTRACTION_MISSING',
    'LMAX_R195_FAIL_STATE_REPORTING_FIELDS_MISSING_OR_INCONSISTENT',
    'LMAX_R195_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE',
    'LMAX_R195_FAIL_SHUTDOWN_REVERT_INCOMPLETE',
    'LMAX_R195_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED',
    'LMAX_R195_FAIL_MARKET_HOURS_CONSTRAINT_MISSING_OR_VIOLATED',
    'LMAX_R195_FAIL_PROFILE_NOT_SELECTED',
    'LMAX_R195_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK',
    'LMAX_R195_FAIL_FORBIDDEN_ACTION_RISK',
    'LMAX_R195_FAIL_API_WORKER_GATEWAY_REGRESSION',
    'LMAX_R195_FAIL_BUILD_OR_TESTS')) 'R195 classification is not allowed.'
Assert-True ($activation.classification -eq 'LMAX_R195_FAIL_SESSIONREJECT_WITH_TAG_DETAIL_REPORTED') 'R195 classification mismatch.'
Assert-True ($r193.classification -eq 'LMAX_R193_PASS_SESSIONREJECT_TAG_DETAIL_ENRICHMENT_RETRY_READINESS_RECOMMENDED_NO_EXTERNAL') 'R193 evidence missing.'
Assert-True ($r194.classification -eq 'LMAX_R194_PASS_SESSIONREJECT_TAG_DETAIL_RETRY_READINESS_NO_EXTERNAL') 'R194 readiness missing.'

Assert-True ($preflight.preflightPassed -eq $true) 'R195 preflight did not pass.'
Assert-True ($preflight.approvalPresent -eq $true) 'R195 approval missing.'
Assert-True ($preflight.approvalExactFreshMatch -eq $true) 'R195 approval mismatch.'
Assert-True ($preflight.marketHoursConfirmationPresent -eq $true) 'R195 market-hours confirmation missing.'
Assert-True ($preflight.marketHoursConfirmationConcrete -eq $true) 'R195 market-hours confirmation not concrete.'
Assert-True ($preflight.marketHoursWindow -eq 'Tuesday, May 19, 2026 at 11:38 Europe/Paris') 'R195 market-hours window mismatch.'
Assert-True ($preflight.externalBoundaryAllowed -eq $true) 'R195 external boundary not allowed after preflight.'

Assert-True ($activation.externalActivationAttempted -eq $true) 'R195 external activation was not attempted.'
Assert-True ($activation.attemptCount -eq 1) 'R195 attemptCount must be exactly one.'
Assert-True ($forbidden.attemptCount -eq 1) 'R195 forbidden audit attemptCount mismatch.'

Assert-True ($selected.selectedProfile -eq $profile) 'R195 selected profile mismatch.'
Assert-True ($selected.profileExecuted -eq $true) 'R195 profile was not executed.'
Assert-True ($selected.selectedDiagnosticProfileCount -eq 1) 'R195 selected more than one diagnostic profile.'
Assert-True ($selected.gbpUsdOnly -eq $true) 'R195 future retry is not GBPUSD-only.'
Assert-True ($selected.singleRequest -eq $true) 'R195 request is not single-request.'
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

foreach ($field in @(
    'marketDataRequestWriteAttempted',
    'marketDataRequestWriteSucceeded',
    'marketDataRequestResponseReadAttempted',
    'marketDataRequestReachedBoundedResponseClassification',
    'marketDataRequestSentLegacyFlag')) {
    Assert-True ($state.PSObject.Properties.Name -contains $field) "State field missing: $field"
}
Assert-True ($state.marketDataRequestWriteAttempted -eq $true) 'Write attempted should be true.'
Assert-True ($state.marketDataRequestWriteSucceeded -eq $true) 'Write succeeded should be true.'
Assert-True ($state.marketDataRequestResponseReadAttempted -eq $true) 'Response read attempted should be true.'
Assert-True ($state.marketDataRequestReachedBoundedResponseClassification -eq $true) 'Bounded response classification should be true.'
Assert-True ($state.marketDataRequestSentLegacyFlag -eq $false) 'Legacy flag should remain compatibility-only false.'
Assert-True ($state.classifiedMarketDataResponseObserved -eq $true) 'Classified response evidence missing.'
Assert-True ($state.stateFieldsConsistent -eq $true) 'State fields are inconsistent.'
Assert-True ($state.stateFieldsContradictionDetected -eq $false) 'State contradiction detected.'

Assert-True ($boundary.tcpSocket -eq 'Succeeded') 'TCP/socket boundary missing.'
Assert-True ($boundary.tls -eq 'Succeeded') 'TLS boundary missing.'
Assert-True ($boundary.tlsStreamAvailableForFix -eq $true) 'TLS stream for FIX missing.'
Assert-True ($boundary.fixLogonSession -eq 'Succeeded') 'FIX logon/session boundary missing.'
Assert-True ($boundary.fixAcknowledgement -eq 'FixLogonAcknowledged') 'FIX acknowledgement missing.'
Assert-True ($boundary.marketDataRequest -eq 'WriteSucceededAndReachedBoundedResponseClassificationAfterFixSuccess') 'MarketDataRequest boundary mismatch.'
Assert-True ($boundary.marketDataResponse -eq 'ReachedSanitizedClassification') 'MarketDataResponse boundary mismatch.'
Assert-True ($boundary.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'MarketDataResponse category mismatch.'
Assert-True ($boundary.marketDataRequestBeforeFixSuccess -eq $false) 'MarketDataRequest occurred before FIX success.'
Assert-True ($boundary.marketDataResponseReadBeforeMarketDataRequestSuccess -eq $false) 'MarketDataResponse read occurred before MarketDataRequest success.'

Assert-True ($request.requestSent -eq $true) 'MarketDataRequest should be sent.'
Assert-True ($request.writeAttempted -eq $true) 'MarketDataRequest write attempted missing.'
Assert-True ($request.writeSucceeded -eq $true) 'MarketDataRequest write succeeded missing.'
Assert-True ($request.rawFixSerialized -eq $false) 'Raw FIX serialized in request evidence.'

Assert-True ($response.responseReadAttempted -eq $true) 'MarketDataResponse read should be attempted.'
Assert-True ($response.boundedResponseClassificationReached -eq $true) 'Bounded response classification missing.'
Assert-True ($response.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'Response category mismatch.'
Assert-True ($response.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'Session reject sanitized reason mismatch.'
Assert-True ($response.sanitizedSessionRejectReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'Sanitized session reject reason mismatch.'
Assert-True ($response.marketDataRejectSanitizedSubcategory -eq 'RejectReasonNotAvailable') 'MarketData reject subcategory mismatch.'
Assert-True ($response.sessionRejectSanitizedSubcategory -eq 'SessionRejectRefMsgTypeMarketDataRequest') 'Session reject subcategory mismatch.'
Assert-True ($response.rejectReasonExtractionSource -eq 'MsgType3Tags371372373') 'Reject extraction source mismatch.'
Assert-True ($response.sessionRejectRefTagIdSanitizedCategory -eq 'RefTagID_MDReqID_262') 'RefTagID detail mismatch.'
Assert-True ($response.sessionRejectReasonSanitizedCategory -eq 'SessionRejectReason_ValueIncorrect') 'SessionRejectReason detail mismatch.'
Assert-True ($response.sessionRejectRefMsgTypeSanitizedCategory -eq 'RefMsgType_MarketDataRequest') 'RefMsgType detail mismatch.'
Assert-True ($response.entriesObserved -eq $false) 'Entries should not be observed.'
Assert-True ($response.entryCount -eq 0) 'Entry count should be zero.'
Assert-True ($response.rawRejectTextSerialized -eq $false) 'Raw reject text serialized.'
Assert-True ($response.rawFixSerialized -eq $false) 'Raw FIX serialized.'

Assert-True ($detail.sessionRejectTagDetailReported -eq $true) 'SessionReject tag detail not reported.'
Assert-True ($detail.sessionRejectRefTagIdSanitizedCategory -eq 'RefTagID_MDReqID_262') 'Detail RefTagID mismatch.'
Assert-True ($detail.sessionRejectReasonSanitizedCategory -eq 'SessionRejectReason_ValueIncorrect') 'Detail SessionRejectReason mismatch.'
Assert-True ($detail.sessionRejectRefMsgTypeSanitizedCategory -eq 'RefMsgType_MarketDataRequest') 'Detail RefMsgType mismatch.'
Assert-True ($detail.rejectReasonExtractionSource -eq 'MsgType3Tags371372373') 'Detail extraction source mismatch.'
Assert-True ($detail.notAvailableUsedForAbsentOrUnsafeOnly -eq $true) 'NotAvailable meaning missing.'
Assert-True ($detail.notAvailableDistinctFromPropagationFailure -eq $true) 'NotAvailable/propagation distinction missing.'
Assert-True ($detail.propagationFailureDetected -eq $false) 'Propagation failure detected.'
Assert-True ($detail.rawTagValuesSerialized -eq $false) 'Raw tag values serialized.'
Assert-True ($activation.rejectDetailExtractionMissing -eq $false) 'Reject detail extraction missing.'

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

Assert-True ($forbidden.externalActivationAttempted -eq $true) 'R195 forbidden audit did not record approved external attempt.'
Assert-True ($forbidden.externalAttemptApproved -eq $true) 'R195 external attempt was not approved.'
Assert-True ($forbidden.socketOpened -eq $true) 'R195 socket boundary missing.'
Assert-True ($forbidden.tlsOpened -eq $true) 'R195 TLS boundary missing.'
Assert-True ($forbidden.fixSessionOpened -eq $true) 'R195 FIX boundary missing.'
Assert-True ($forbidden.liveMarketDataRequestSent -eq $true) 'R195 MarketDataRequest boundary missing.'
Assert-True ($forbidden.liveMarketDataResponseRead -eq $true) 'R195 MarketDataResponse boundary missing.'
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
Assert-True ($sanitization.rawCredentialsSerialized -eq $false) 'Raw credential serialization risk detected.'
Assert-True ($sanitization.rawSessionIdsSerialized -eq $false) 'Raw session ID serialization risk detected.'
Assert-True ($sanitization.rawCompIdsSerialized -eq $false) 'Raw CompID serialization risk detected.'
Assert-True ($sanitization.rawEndpointValuesSerialized -eq $false) 'Raw endpoint serialization risk detected.'
Assert-True ($sanitization.rawTlsMaterialSerialized -eq $false) 'Raw TLS serialization risk detected.'
Assert-True ($sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned=false missing.'
Assert-True ($sanitized.credentialValuesReturned -eq $false) 'Sanitized result credentialValuesReturned=false missing.'

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R196') 'Next phase recommendation missing.'
Assert-True ($next.mustRemainNoExternal -eq $true) 'R196 must remain no-external.'
Assert-True ($next.doNotRecommendBlindLiveRetry -eq $true) 'Next phase must not recommend a blind live retry.'

$artifactText = Get-ChildItem -LiteralPath $ArtifactRoot -Filter 'phase-lmax-r195-*' -File |
    Where-Object { $_.Name -notin @('phase-lmax-r195-operator-approval.txt', 'phase-lmax-r195-expected-operator-approval.txt') } |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combinedArtifactText = [string]::Join("`n", $artifactText)
foreach ($forbiddenToken in @('35=V', '35=Y', '35=3', '281=', '371=', '372=', '373=', '58=', [string][char]1, 'password=', 'username=', 'SenderCompId', 'TargetCompId')) {
    Assert-True (-not $combinedArtifactText.Contains($forbiddenToken)) "Forbidden serialized token detected in R195 artifacts: $forbiddenToken"
}

$gatePath = Join-Path $ArtifactRoot 'phase-lmax-r195-gate-validation.json'
Assert-True (Test-Path -LiteralPath $gatePath) 'R195 build/test/validator evidence missing.'
$gate = Get-Content -LiteralPath $gatePath -Raw | ConvertFrom-Json
Assert-True ($gate.build -like 'PASS*') 'Build evidence missing.'
Assert-True ($gate.focusedTests -like 'PASS*') 'Focused test evidence missing.'
Assert-True ($gate.validator -eq 'LMAX_R195_VALIDATION_PASS') 'Validator evidence missing.'

'LMAX_R195_VALIDATION_PASS'
