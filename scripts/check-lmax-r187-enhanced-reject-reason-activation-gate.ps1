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

$activation = Read-Json 'phase-lmax-r187-enhanced-reject-reason-activation.json'
$preflight = Read-Json 'phase-lmax-r187-preflight-result.json'
$selected = Read-Json 'phase-lmax-r187-selected-profile-evidence.json'
$extraction = Read-Json 'phase-lmax-r187-enhanced-reject-extraction-evidence.json'
$state = Read-Json 'phase-lmax-r187-state-evidence.json'
$boundary = Read-Json 'phase-lmax-r187-boundary-evidence.json'
$request = Read-Json 'phase-lmax-r187-marketdata-request-evidence.json'
$response = Read-Json 'phase-lmax-r187-marketdata-response-evidence.json'
$sanitized = Read-Json 'phase-lmax-r187-sanitized-result.json'
$shutdown = Read-Json 'phase-lmax-r187-shutdown-revert-evidence.json'
$sanitization = Read-Json 'phase-lmax-r187-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r187-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r187-api-worker-fake-gateway-audit.json'
$approvedUniverse = Read-Json 'phase-lmax-r187-approved-universe-preservation.json'
$usdJpy = Read-Json 'phase-lmax-r187-usdjpy-caveat-preservation.json'
$next = Read-Json 'phase-lmax-r187-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r187-gate-validation.json'
$r185 = Read-Json 'phase-lmax-r185-enhanced-reject-reason-retry-readiness.json'

Assert-True ($activation.classification -in @(
    'LMAX_R187_PASS_ENHANCED_REJECT_REASON_RUNTIME_ACTIVATION_SANITIZED',
    'LMAX_R187_FAIL_SESSIONREJECT_WITH_SANITIZED_REASON_REPORTED',
    'LMAX_R187_FAIL_MARKETDATAREQUESTREJECT_WITH_SANITIZED_REASON_REPORTED',
    'LMAX_R187_FAIL_MARKETDATA_RESPONSE_BOUNDARY',
    'LMAX_R187_FAIL_STATE_REPORTING_FIELDS_MISSING_OR_INCONSISTENT',
    'LMAX_R187_FAIL_REJECT_REASON_EXTRACTION_MISSING',
    'LMAX_R187_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE',
    'LMAX_R187_FAIL_SHUTDOWN_REVERT_INCOMPLETE',
    'LMAX_R187_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED',
    'LMAX_R187_FAIL_MARKET_HOURS_CONSTRAINT_MISSING_OR_VIOLATED',
    'LMAX_R187_FAIL_PROFILE_NOT_SELECTED',
    'LMAX_R187_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK',
    'LMAX_R187_FAIL_FORBIDDEN_ACTION_RISK',
    'LMAX_R187_FAIL_API_WORKER_GATEWAY_REGRESSION',
    'LMAX_R187_FAIL_BUILD_OR_TESTS')) 'R187 classification is not allowed.'

Assert-True ($activation.classification -eq 'LMAX_R187_FAIL_REJECT_REASON_EXTRACTION_MISSING') 'R187 should classify the completed attempt as enhanced reject extraction missing.'
Assert-True ($preflight.preflightPassed -eq $true) 'R187 preflight did not pass.'
Assert-True ($preflight.approvalExactFreshMatch -eq $true) 'R187 approval was not exact/fresh.'
Assert-True ($preflight.marketHoursConfirmationConcrete -eq $true) 'R187 concrete market-hours confirmation missing.'
Assert-True ($preflight.marketHoursWindow -eq 'Tuesday, May 19, 2026 at 10:17 Europe/Paris') 'R187 market-hours window mismatch.'
Assert-True ($preflight.externalBoundaryAllowed -eq $true) 'R187 external boundary was not allowed after approved preflight.'
Assert-True ($preflight.preExternalStoppedInvocationCount -eq 1) 'R187 should record one pre-external stopped invocation.'
Assert-True ($preflight.preExternalStoppedInvocationCrossedExternalBoundary -eq $false) 'R187 pre-external stopped invocation crossed a boundary.'

Assert-True ($activation.externalActivationAttempted -eq $true) 'R187 external activation was not attempted.'
Assert-True ($activation.attemptCount -eq 1) 'R187 attemptCount must be exactly one.'
Assert-True ($activation.preExternalStoppedInvocationCrossedExternalBoundary -eq $false) 'R187 pre-external stopped invocation boundary flag invalid.'

Assert-True ($selected.selectedProfile -eq $profile) 'R187 selected profile evidence mismatch.'
Assert-True ($selected.profileExecuted -eq $true) 'R187 profile was not executed.'
Assert-True ($selected.selectedDiagnosticProfileCount -eq 1) 'R187 selected more than one diagnostic profile.'
Assert-True ($r185.selectedFutureProfile -eq $profile) 'R185 selected future profile evidence missing.'
Assert-True ($selected.gbpUsdOnly -eq $true) 'GBPUSD-only profile readiness missing.'
Assert-True ($selected.singleRequest -eq $true) 'Single-request profile readiness missing.'
Assert-True ($selected.securityId -eq '4002') 'SecurityID=4002 missing.'
Assert-True ($selected.securityIdSource -eq '8') 'SecurityIDSource=8 missing.'
Assert-True ($selected.subscriptionRequestType -eq '1') 'SubscriptionRequestType=1 missing.'
Assert-True ($selected.mdUpdateType -eq '0') 'MDUpdateType=0 missing.'
Assert-True ($selected.marketDepth -eq 1) 'MarketDepth=1 missing.'
Assert-True ($selected.noMdEntryTypes -eq 2) 'NoMDEntryTypes=2 missing.'
Assert-True ($selected.bidAndOfferTogether -eq $true) 'Bid+offer together missing.'
Assert-True ($selected.symbolTextPresent -eq $false) 'Symbol text should be absent.'
Assert-True ($selected.internalSymbolPresent -eq $false) 'InternalSymbol should be absent.'
Assert-True ($selected.snapshotOnlyPresent -eq $false) 'SnapshotOnly should be absent.'
Assert-True ($selected.subscriptionRequestTypeZeroPresent -eq $false) 'SubscriptionRequestType=0 should be absent.'

foreach ($field in @(
    'marketDataRequestWriteAttempted',
    'marketDataRequestWriteSucceeded',
    'marketDataRequestResponseReadAttempted',
    'marketDataRequestReachedBoundedResponseClassification',
    'marketDataRequestSentLegacyFlag')) {
    Assert-True ($state.PSObject.Properties.Name -contains $field) "State field missing: $field"
}
Assert-True ($state.marketDataRequestWriteAttempted -eq $true) 'Write attempted should be true after completed runtime path.'
Assert-True ($state.marketDataRequestWriteSucceeded -eq $true) 'Write succeeded should be true after completed runtime path.'
Assert-True ($state.marketDataRequestResponseReadAttempted -eq $true) 'Response read attempted should be true after completed runtime path.'
Assert-True ($state.marketDataRequestReachedBoundedResponseClassification -eq $true) 'Bounded response classification should be true after completed runtime path.'
Assert-True ($state.marketDataRequestSentLegacyFlag -eq $false) 'Legacy flag should remain compatibility-only false.'
Assert-True ($state.classifiedMarketDataResponseObserved -eq $true) 'Classified MarketDataResponse evidence missing.'
Assert-True ($state.stateFieldsConsistent -eq $true) 'State fields are inconsistent.'
Assert-True ($state.stateFieldsContradictionDetected -eq $false) 'State contradiction detected.'

Assert-True ($boundary.credentialConfig -eq 'Succeeded') 'Credential/config boundary missing.'
Assert-True ($boundary.tcpSocket -eq 'Succeeded') 'TCP/socket boundary missing.'
Assert-True ($boundary.tls -eq 'Succeeded') 'TLS boundary missing.'
Assert-True ($boundary.tlsStreamAvailableForFix -eq $true) 'TLS stream for FIX missing.'
Assert-True ($boundary.fixLogonSession -eq 'Succeeded') 'FIX logon/session boundary missing.'
Assert-True ($boundary.fixAcknowledgement -eq 'FixLogonAcknowledged') 'FIX acknowledgement missing.'
Assert-True ($boundary.marketDataRequest -eq 'WriteSucceededAndReachedBoundedResponseClassificationAfterFixSuccess') 'MarketDataRequest boundary evidence mismatch.'
Assert-True ($boundary.marketDataResponse -eq 'ReachedSanitizedClassification') 'MarketDataResponse boundary evidence mismatch.'
Assert-True ($boundary.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'MarketDataResponse category mismatch.'
Assert-True ($boundary.boundaryBlockedBeforeExternal -eq $false) 'Boundary should not be pre-external blocked.'
Assert-True ($boundary.marketDataRequestBeforeFixSuccess -eq $false) 'MarketDataRequest occurred before FIX success.'
Assert-True ($boundary.marketDataResponseReadBeforeMarketDataRequestSuccess -eq $false) 'MarketDataResponse read occurred before MarketDataRequest success.'

Assert-True ($request.requestSent -eq $true) 'MarketDataRequest should be sent.'
Assert-True ($request.writeAttempted -eq $true) 'MarketDataRequest write attempted missing.'
Assert-True ($request.writeSucceeded -eq $true) 'MarketDataRequest write succeeded missing.'
Assert-True ($request.rawFixSerialized -eq $false) 'Raw FIX serialized in request evidence.'

Assert-True ($response.responseReadAttempted -eq $true) 'MarketDataResponse read should be attempted.'
Assert-True ($response.boundedResponseClassificationReached -eq $true) 'Bounded response classification missing.'
Assert-True ($response.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'MarketDataResponse category should be SessionRejectObservedWithSanitizedReason.'
Assert-True ($response.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'Session reject sanitized reason mismatch.'
Assert-True ($response.sanitizedSessionRejectReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'Sanitized session reject reason mismatch.'
Assert-True ($response.entriesObserved -eq $false) 'Entries should not be observed.'
Assert-True ($response.entryCount -eq 0) 'Entry count should be zero.'
Assert-True ($response.rawRejectTextSerialized -eq $false) 'Raw reject text serialized in response evidence.'
Assert-True ($response.rawFixSerialized -eq $false) 'Raw FIX serialized in response evidence.'

Assert-True ($extraction.r184EnhancedRejectExtractionAvailable -eq $true) 'R184 extraction readiness missing.'
Assert-True ($extraction.runtimeExtractionAttempted -eq $true) 'Runtime extraction should be attempted for completed R187 path.'
Assert-True ($extraction.runtimeExtractionBlockedPreExternal -eq $false) 'Runtime extraction should not be marked pre-external blocked.'
foreach ($field in @('marketDataRejectSanitizedSubcategory', 'sessionRejectSanitizedSubcategory', 'rejectReasonExtractionSource')) {
    Assert-True ($extraction.PSObject.Properties.Name -contains $field) "Enhanced reject extraction field missing: $field"
    Assert-True ($response.PSObject.Properties.Name -contains $field) "Response evidence reject extraction field missing: $field"
}
Assert-True ($extraction.marketDataRejectSanitizedSubcategory -eq 'RejectReasonNotAvailable') 'MarketData reject subcategory should be unavailable for this runtime output.'
Assert-True ($extraction.sessionRejectSanitizedSubcategory -eq 'RejectReasonNotAvailable') 'Session reject subcategory should be unavailable for this runtime output.'
Assert-True ($extraction.rejectReasonExtractionSource -eq 'RuntimeCliSummaryDidNotEmitR184Subcategory') 'Reject extraction source mismatch.'
Assert-True ($extraction.enhancedRejectExtractionMissing -eq $true) 'Enhanced reject extraction missing flag must be true.'
Assert-True ($extraction.futureRuntimeEvidenceRequiresMarketDataRequestReject281Subcategory -eq $true) 'Future MsgType Y/tag 281 extraction requirement missing.'
Assert-True ($extraction.futureRuntimeEvidenceRequiresSessionReject371372373Subcategory -eq $true) 'Future MsgType 3/tags 371/372/373 extraction requirement missing.'
Assert-True ($extraction.broadMalformedOrUnsupportedCategoryPreserved -eq $true) 'Broad malformed/unsupported category not preserved.'

Assert-True ($forbidden.externalActivationAttempted -eq $true) 'R187 forbidden audit did not record the approved external attempt.'
Assert-True ($forbidden.socketOpened -eq $true) 'R187 socket boundary missing.'
Assert-True ($forbidden.tlsOpened -eq $true) 'R187 TLS boundary missing.'
Assert-True ($forbidden.fixSessionOpened -eq $true) 'R187 FIX boundary missing.'
Assert-True ($forbidden.liveMarketDataRequestSent -eq $true) 'R187 MarketDataRequest boundary missing.'
Assert-True ($forbidden.liveMarketDataResponseRead -eq $true) 'R187 MarketDataResponse boundary missing.'
Assert-True ($forbidden.marketDataRuntimeActionPerformed -eq $true) 'R187 runtime market data action missing.'
Assert-True ($forbidden.apiStarted -eq $false) 'API startup detected.'
Assert-True ($forbidden.workerStarted -eq $false) 'Worker startup detected.'
Assert-True ($forbidden.ordersIntroduced -eq $false) 'Order path introduced.'
Assert-True ($forbidden.tradingEnabled -eq $false) 'Trading enabled.'
Assert-True ($forbidden.schedulerStarted -eq $false) 'Scheduler started.'
Assert-True ($forbidden.pollingStarted -eq $false) 'Polling started.'
Assert-True ($forbidden.serviceStarted -eq $false) 'Service started.'
Assert-True ($forbidden.replayIntroduced -eq $false) 'Replay introduced.'
Assert-True ($forbidden.shadowReplayIntroduced -eq $false) 'Shadow replay introduced.'

Assert-True ($shutdown.shutdownRevertRequired -eq $true) 'Shutdown/revert should be required after external attempt.'
Assert-True ($shutdown.shutdownRevertCompleted -eq $true) 'Shutdown/revert evidence missing.'
Assert-True ($shutdown.shutdownRevertStatus -eq 'Succeeded') 'Shutdown/revert did not succeed.'

Assert-True ($approvedUniverse.approvedUniversePreserved -eq $true) 'Approved universe weakened.'
foreach ($symbol in @('GBPUSD', 'EURGBP', 'AUDUSD', 'USDJPY')) {
    Assert-True ($approvedUniverse.approvedInstruments -contains $symbol) "Approved instrument missing: $symbol"
}
Assert-True ($usdJpy.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat weakened.'
Assert-True ($usdJpy.usdJpySecurityId -eq '4004') 'USDJPY SecurityID weakened.'
Assert-True ($usdJpy.usdJpySecurityIdSource -eq '8') 'USDJPY SecurityIDSource weakened.'

Assert-True ($sanitization.rawFixMessagesSerialized -eq $false) 'Raw FIX serialization risk detected.'
Assert-True ($sanitization.rawRejectTextSerialized -eq $false) 'Raw reject text serialization risk detected.'
Assert-True ($sanitization.rawCredentialsSerialized -eq $false) 'Raw credential serialization risk detected.'
Assert-True ($sanitization.rawSessionIdsSerialized -eq $false) 'Raw session ID serialization risk detected.'
Assert-True ($sanitization.rawCompIdsSerialized -eq $false) 'Raw CompID serialization risk detected.'
Assert-True ($sanitization.rawEndpointValuesSerialized -eq $false) 'Raw endpoint serialization risk detected.'
Assert-True ($sanitization.rawTlsMaterialSerialized -eq $false) 'Raw TLS serialization risk detected.'
Assert-True ($sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned=false missing.'
Assert-True ($sanitized.credentialValuesReturned -eq $false) 'Sanitized result credentialValuesReturned=false missing.'
Assert-True ($apiWorker.fakeLmaxGatewayOnlyPreserved -eq $true) 'API/Worker FakeLmaxGatewayOnly regressed.'
Assert-True ($next.mustRemainNoExternal -eq $true) 'R188 must remain no-external.'
Assert-True ($next.doNotRecommendBlindLiveRetry -eq $true) 'Next phase must not recommend a blind live retry.'
Assert-True ($next.doNotTreatAsCleanEnhancedRejectSubcategoryEvidence -eq $true) 'R187 must not be treated as clean enhanced subcategory evidence.'

$artifactText = Get-ChildItem -LiteralPath $ArtifactRoot -Filter 'phase-lmax-r187-*' -File |
    Where-Object { $_.Name -notin @('phase-lmax-r187-operator-approval.txt', 'phase-lmax-r187-expected-operator-approval.txt') } |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combinedArtifactText = [string]::Join("`n", $artifactText)
foreach ($forbiddenToken in @('35=V', '35=Y', '35=3', '281=', '371=', '372=', '373=', '58=', [string][char]1, 'password=', 'username=', 'SenderCompId', 'TargetCompId')) {
    Assert-True (-not $combinedArtifactText.Contains($forbiddenToken)) "Forbidden serialized token detected in R187 artifacts: $forbiddenToken"
}

Assert-True ($gate.build -like 'PASS*') 'Build evidence missing.'
Assert-True ($gate.focusedTests -like 'PASS*') 'Focused test evidence missing.'
Assert-True ($gate.unitTests -like 'PASS*') 'Unit test evidence missing.'
Assert-True ($gate.integrationTests -like 'PASS*') 'Integration test evidence missing.'
Assert-True ($gate.validator -eq 'LMAX_R187_VALIDATION_PASS') 'Validator evidence missing.'

'LMAX_R187_VALIDATION_PASS'
