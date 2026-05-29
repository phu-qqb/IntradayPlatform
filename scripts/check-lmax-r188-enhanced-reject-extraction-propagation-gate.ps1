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

$review = Read-Json 'phase-lmax-r188-enhanced-reject-extraction-propagation-review.json'
$clean = Read-Json 'phase-lmax-r188-r187-clean-state-confirmation.json'
$broad = Read-Json 'phase-lmax-r188-r187-broad-reject-confirmation.json'
$rootCause = Read-Json 'phase-lmax-r188-r184-extraction-propagation-root-cause.json'
$cli = Read-Json 'phase-lmax-r188-cli-artifact-emission-review.json'
$repair = Read-Json 'phase-lmax-r188-repair-implementation-evidence.json'
$contract = Read-Json 'phase-lmax-r188-enhanced-reject-subcategory-contract.json'
$tests = Read-Json 'phase-lmax-r188-test-evidence.json'
$next = Read-Json 'phase-lmax-r188-next-action-decision-gate.json'
$sanitization = Read-Json 'phase-lmax-r188-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r188-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r188-api-worker-fake-gateway-audit.json'
$recommendation = Read-Json 'phase-lmax-r188-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r188-gate-validation.json'
$r187 = Read-Json 'phase-lmax-r187-enhanced-reject-reason-activation.json'

Assert-True ($review.classification -in @(
    'LMAX_R188_PASS_ENHANCED_REJECT_EXTRACTION_PROPAGATION_REPAIR_IMPLEMENTED_NO_EXTERNAL',
    'LMAX_R188_PASS_ENHANCED_REJECT_EXTRACTION_ABSENT_TAGS_SUPPORT_PACKAGE_RECOMMENDED_NO_EXTERNAL',
    'LMAX_R188_PASS_ENHANCED_REJECT_EXTRACTION_SPECIFIC_SUBCATEGORY_RECOVERED_NO_EXTERNAL',
    'LMAX_R188_PASS_ENHANCED_REJECT_EXTRACTION_INCONCLUSIVE_SAFE_NO_EXTERNAL',
    'LMAX_R188_FAIL_NEW_EXTERNAL_ACTION_DETECTED',
    'LMAX_R188_FAIL_R187_EVIDENCE_MISSING',
    'LMAX_R188_FAIL_R184_EXTRACTION_PROPAGATION_REVIEW_MISSING',
    'LMAX_R188_FAIL_ROOT_CAUSE_MISSING',
    'LMAX_R188_FAIL_ENHANCED_SUBCATEGORY_CONTRACT_MISSING',
    'LMAX_R188_FAIL_TEST_EVIDENCE_MISSING',
    'LMAX_R188_FAIL_RAW_FIX_OR_REJECT_LEAK_RISK',
    'LMAX_R188_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK',
    'LMAX_R188_FAIL_USDJPY_CAVEAT_WEAKENED',
    'LMAX_R188_FAIL_FORBIDDEN_ACTION_RISK',
    'LMAX_R188_FAIL_API_WORKER_GATEWAY_REGRESSION',
    'LMAX_R188_FAIL_BUILD_OR_TESTS')) 'R188 classification is not allowed.'
Assert-True ($review.classification -eq 'LMAX_R188_PASS_ENHANCED_REJECT_EXTRACTION_PROPAGATION_REPAIR_IMPLEMENTED_NO_EXTERNAL') 'R188 repair classification mismatch.'
Assert-True ($review.noExternal -eq $true) 'R188 must be no-external.'

Assert-True ($r187.attemptCount -eq 1) 'R187 evidence missing or attemptCount invalid.'
Assert-True ($clean.cleanStateConfirmed -eq $true) 'R187 clean state confirmation missing.'
Assert-True ($clean.marketDataRequestWriteAttempted -eq $true) 'R187 write attempted not confirmed.'
Assert-True ($clean.marketDataRequestWriteSucceeded -eq $true) 'R187 write succeeded not confirmed.'
Assert-True ($clean.marketDataRequestResponseReadAttempted -eq $true) 'R187 response read not confirmed.'
Assert-True ($clean.marketDataRequestReachedBoundedResponseClassification -eq $true) 'R187 bounded classification not confirmed.'
Assert-True ($clean.stateFieldsConsistent -eq $true) 'R187 clean state contradiction not reviewed.'

Assert-True ($broad.broadRejectConfirmed -eq $true) 'R187 broad reject confirmation missing.'
Assert-True ($broad.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'R187 broad response category mismatch.'
Assert-True ($broad.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'R187 broad sanitized reason mismatch.'
Assert-True ($broad.sanitizedSessionRejectReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'R187 sanitized session reason mismatch.'

Assert-True ($rootCause.r184ParserCapabilityPresent -eq $true) 'R184 extraction propagation review missing.'
Assert-True ($rootCause.msgTypeYTag281ExtractionPresent -eq $true) 'MsgType Y/tag 281 handling missing.'
Assert-True ($rootCause.msgType3Tags371372373ExtractionPresent -eq $true) 'MsgType 3/tags 371/372/373 handling missing.'
Assert-True ($rootCause.extractedButLostInResultMapping -eq $true) 'Root cause for missing enhanced subcategory missing.'
Assert-True ($rootCause.propagatedButNotEmittedByCli -eq $true) 'CLI emission root cause missing.'

Assert-True ($cli.cliNowEmitsMarketDataRejectSanitizedSubcategory -eq $true) 'CLI marketDataRejectSanitizedSubcategory emission missing.'
Assert-True ($cli.cliNowEmitsSessionRejectSanitizedSubcategory -eq $true) 'CLI sessionRejectSanitizedSubcategory emission missing.'
Assert-True ($cli.cliNowEmitsRejectReasonExtractionSource -eq $true) 'CLI rejectReasonExtractionSource emission missing.'
Assert-True ($cli.runtimeCliSummaryDidNotEmitR184SubcategoryCanStillOccurWhenClassifierProducedSubcategory -eq $false) 'RuntimeCliSummaryDidNotEmitR184Subcategory can still occur after classifier subcategory.'

Assert-True ($repair.repairImplemented -eq $true) 'Repair implementation evidence missing.'
foreach ($layer in @(
    'LmaxReadOnlyActivationManualMarketDataRequestOperation',
    'LmaxExecutableReadOnlyMarketDataSessionClient',
    'LmaxExecutableReadOnlyRealDependencyProviders',
    'LmaxExecutableReadOnlyRealLowLevelDependencies',
    'LmaxExecutableReadOnlyLowLevelSessionStack',
    'LmaxRealReadOnlyMarketDataFrameBoundaryProvider',
    'LmaxRealReadOnlyProviderClients',
    'LmaxRealReadOnlyMarketDataTransport',
    'LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter',
    'Program.cs')) {
    Assert-True ($repair.modifiedPropagationLayers -contains $layer) "Repair layer missing: $layer"
}

Assert-True ($contract.marketDataRejectSanitizedSubcategoryField -eq 'marketDataRejectSanitizedSubcategory') 'Enhanced subcategory contract missing marketData field.'
Assert-True ($contract.sessionRejectSanitizedSubcategoryField -eq 'sessionRejectSanitizedSubcategory') 'Enhanced subcategory contract missing session field.'
Assert-True ($contract.rejectReasonExtractionSourceField -eq 'rejectReasonExtractionSource') 'Enhanced subcategory contract missing source field.'
Assert-True ($contract.msgTypeYTag281Categories -contains 'MarketDataRequestRejectUnsupportedMDUpdateType') 'MsgType Y/tag 281 categories missing.'
Assert-True ($contract.msgType3Tags371372373Categories -contains 'SessionRejectRefMsgTypeMarketDataRequest') 'MsgType 3 categories missing.'
Assert-True ($contract.msgTypeYTag281Categories -contains 'RejectReasonNotAvailable') 'RejectReasonNotAvailable handling missing for MsgType Y.'
Assert-True ($contract.msgType3Tags371372373Categories -contains 'RejectReasonNotAvailable') 'RejectReasonNotAvailable handling missing for MsgType 3.'
Assert-True ($contract.broadMalformedOrUnsupportedCategoryPreserved -eq $true) 'Broad category preservation missing.'

foreach ($field in @(
    'msgTypeYTag281PropagationTest',
    'msgType3Tags371372373PropagationTest',
    'rejectReasonNotAvailableHandlingTest',
    'cliArtifactContractTest',
    'endToEndWrapperPropagationTest')) {
    Assert-True (-not [string]::IsNullOrWhiteSpace($tests.$field)) "Test evidence missing: $field"
}
Assert-True ($tests.focusedTests -like 'PASS*') 'Focused test evidence missing.'
Assert-True ($tests.unitTests -like 'PASS*') 'Unit test evidence missing.'
Assert-True ($tests.integrationTests -like 'PASS*') 'Integration test evidence missing.'

Assert-True ($next.liveRetryAllowedNow -eq $false) 'R188 next action allows live retry too early.'
Assert-True ($next.requiresReadinessGateBeforeAnyFutureRetry -eq $true) 'R188 must require readiness gate before retry.'
Assert-True ($recommendation.mustRemainNoExternal -eq $true) 'R189 must remain no-external.'
Assert-True ($recommendation.doNotRecommendBlindLiveRetry -eq $true) 'R188 recommendation allows blind live retry.'

Assert-True ($forbidden.externalActivationAttempted -eq $false) 'R188 external activation detected.'
Assert-True ($forbidden.socketOpened -eq $false) 'R188 socket/TCP action detected.'
Assert-True ($forbidden.tlsOpened -eq $false) 'R188 TLS action detected.'
Assert-True ($forbidden.fixSessionOpened -eq $false) 'R188 FIX action detected.'
Assert-True ($forbidden.liveMarketDataRequestSent -eq $false) 'R188 live MarketDataRequest detected.'
Assert-True ($forbidden.liveMarketDataResponseRead -eq $false) 'R188 live MarketDataResponse detected.'
Assert-True ($forbidden.ordersIntroduced -eq $false) 'Order path introduced.'
Assert-True ($forbidden.tradingEnabled -eq $false) 'Trading enabled.'
Assert-True ($forbidden.schedulerStarted -eq $false) 'Scheduler started.'
Assert-True ($forbidden.pollingStarted -eq $false) 'Polling started.'
Assert-True ($forbidden.serviceStarted -eq $false) 'Service started.'
Assert-True ($forbidden.replayIntroduced -eq $false) 'Replay introduced.'
Assert-True ($forbidden.shadowReplayIntroduced -eq $false) 'Shadow replay introduced.'
Assert-True ($apiWorker.fakeLmaxGatewayOnlyPreserved -eq $true) 'API/Worker FakeLmaxGatewayOnly regressed.'

Assert-True ($sanitization.rawFixMessagesSerialized -eq $false) 'Raw FIX serialization risk detected.'
Assert-True ($sanitization.rawRejectTextSerialized -eq $false) 'Raw reject text serialization risk detected.'
Assert-True ($sanitization.rawCredentialsSerialized -eq $false) 'Raw credential serialization risk detected.'
Assert-True ($sanitization.rawSessionIdsSerialized -eq $false) 'Raw session ID serialization risk detected.'
Assert-True ($sanitization.rawCompIdsSerialized -eq $false) 'Raw CompID serialization risk detected.'
Assert-True ($sanitization.rawEndpointValuesSerialized -eq $false) 'Raw endpoint serialization risk detected.'
Assert-True ($sanitization.rawTlsMaterialSerialized -eq $false) 'Raw TLS serialization risk detected.'
Assert-True ($sanitization.rawTagValuesSerialized -eq $false) 'Raw tag value serialization risk detected.'
Assert-True ($sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned=false missing.'

$artifactText = Get-ChildItem -LiteralPath $ArtifactRoot -Filter 'phase-lmax-r188-*' -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combinedArtifactText = [string]::Join("`n", $artifactText)
foreach ($forbiddenToken in @('35=V', '35=Y', '35=3', '281=', '371=', '372=', '373=', '58=', [string][char]1, 'password=', 'username=', 'SenderCompId', 'TargetCompId')) {
    Assert-True (-not $combinedArtifactText.Contains($forbiddenToken)) "Forbidden serialized token detected in R188 artifacts: $forbiddenToken"
}

Assert-True ($gate.build -like 'PASS*') 'Build evidence missing.'
Assert-True ($gate.focusedTests -like 'PASS*') 'Focused test evidence missing.'
Assert-True ($gate.unitTests -like 'PASS*') 'Unit test evidence missing.'
Assert-True ($gate.integrationTests -like 'PASS*') 'Integration test evidence missing.'
Assert-True ($gate.validator -eq 'LMAX_R188_VALIDATION_PASS') 'Validator evidence missing.'

'LMAX_R188_VALIDATION_PASS'
