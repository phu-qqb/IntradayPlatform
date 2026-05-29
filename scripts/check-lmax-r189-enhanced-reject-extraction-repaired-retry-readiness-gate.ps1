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

$readiness = Read-Json 'phase-lmax-r189-enhanced-reject-extraction-repaired-retry-readiness.json'
$selected = Read-Json 'phase-lmax-r189-selected-profile-evidence.json'
$extraction = Read-Json 'phase-lmax-r189-enhanced-reject-extraction-propagation-readiness.json'
$state = Read-Json 'phase-lmax-r189-final-state-evidence-contract-readiness.json'
$nonSelected = Read-Json 'phase-lmax-r189-non-selected-profiles-evidence.json'
$reservation = Read-Json 'phase-lmax-r189-next-activation-phase-reservation-decision.json'
$preflight = Read-Json 'phase-lmax-r189-r191-preflight-checklist.json'
$expected = Read-Json 'phase-lmax-r189-r191-expected-evidence-contract.json'
$reporting = Read-Json 'phase-lmax-r189-reporting-contract-readiness.json'
$approvedUniverse = Read-Json 'phase-lmax-r189-approved-universe-preservation.json'
$usdJpy = Read-Json 'phase-lmax-r189-usdjpy-caveat-preservation.json'
$sanitization = Read-Json 'phase-lmax-r189-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r189-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r189-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r189-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r189-gate-validation.json'
$r187 = Read-Json 'phase-lmax-r187-enhanced-reject-reason-activation.json'
$r188 = Read-Json 'phase-lmax-r188-enhanced-reject-extraction-propagation-review.json'

$allowedClassifications = @(
    'LMAX_R189_PASS_ENHANCED_REJECT_EXTRACTION_REPAIRED_RETRY_READINESS_NO_EXTERNAL',
    'LMAX_R189_FAIL_NEW_EXTERNAL_ACTION_DETECTED',
    'LMAX_R189_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID',
    'LMAX_R189_FAIL_PROFILE_SELECTION_MISSING',
    'LMAX_R189_FAIL_MDUPDATETYPE_PROFILE_CONTRACT_WEAKENED',
    'LMAX_R189_FAIL_ENHANCED_REJECT_EXTRACTION_PROPAGATION_READINESS_MISSING',
    'LMAX_R189_FAIL_REJECT_SUBCATEGORY_CONTRACT_MISSING',
    'LMAX_R189_FAIL_FINAL_STATE_EVIDENCE_CONTRACT_MISSING',
    'LMAX_R189_FAIL_APPROVAL_OR_MARKET_HOURS_CONSTRAINT_MISSING',
    'LMAX_R189_FAIL_USDJPY_CAVEAT_WEAKENED',
    'LMAX_R189_FAIL_RAW_FIX_OR_REJECT_LEAK_RISK',
    'LMAX_R189_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK',
    'LMAX_R189_FAIL_FORBIDDEN_ACTION_RISK',
    'LMAX_R189_FAIL_API_WORKER_GATEWAY_REGRESSION',
    'LMAX_R189_FAIL_BUILD_OR_TESTS')

Assert-True ($allowedClassifications -contains $readiness.classification) 'R189 classification is not allowed.'
Assert-True ($readiness.classification -eq 'LMAX_R189_PASS_ENHANCED_REJECT_EXTRACTION_REPAIRED_RETRY_READINESS_NO_EXTERNAL') 'R189 readiness classification mismatch.'
Assert-True ($readiness.noExternal -eq $true) 'R189 must be no-external.'

Assert-True ($r187.attemptCount -eq 1) 'R187 evidence missing or attemptCount invalid.'
Assert-True ($r188.classification -eq 'LMAX_R188_PASS_ENHANCED_REJECT_EXTRACTION_PROPAGATION_REPAIR_IMPLEMENTED_NO_EXTERNAL') 'R188 repair evidence missing.'

Assert-True ($reservation.nextActivationPhase -eq 'LMAX-R191') 'Next activation phase is not R191.'
Assert-True ($reservation.oddNumbered -eq $true) 'Next activation phase must be odd-numbered.'
Assert-True ($reservation.explicitlyReserved -eq $true) 'Next activation phase is not explicitly reserved.'
Assert-True ($reservation.liveRetryNotExecutedInR189 -eq $true) 'R189 must not execute a live retry.'
Assert-True ($readiness.nextActivationPhaseOddNumbered -eq $true) 'R189 odd-numbered reservation missing.'
Assert-True ($readiness.nextActivationPhaseExplicitlyReserved -eq $true) 'R189 explicit reservation missing.'

Assert-True ($selected.selectedFutureProfile -eq $profile) 'Selected future profile mismatch.'
Assert-True ($selected.futureManualRealBoundedPathSelectsExactlyThisProfile -eq $true) 'Future path does not select exactly the required profile.'
Assert-True ($selected.selectedDiagnosticProfileCount -eq 1) 'More than one diagnostic profile selected.'
Assert-True ($selected.gbpUsdOnly -eq $true) 'Future retry is not GBPUSD-only.'
Assert-True ($selected.singleRequest -eq $true) 'Future retry is not single-request.'
Assert-True ($selected.securityId -eq '4002') 'SecurityID=4002 missing.'
Assert-True ($selected.securityIdSource -eq '8') 'SecurityIDSource=8 missing.'
Assert-True ($selected.subscriptionRequestType -eq '1') 'SubscriptionRequestType=1 missing.'
Assert-True ($selected.mdUpdateType -eq '0') 'MDUpdateType=0 missing.'
Assert-True ($selected.marketDepth -eq 1) 'MarketDepth=1 missing.'
Assert-True ($selected.noMdEntryTypes -eq 2) 'NoMDEntryTypes=2 missing.'
Assert-True ($selected.bidAndOfferTogether -eq $true) 'Bid+offer together evidence missing.'
Assert-True ($selected.symbolTextPresent -eq $false) 'Symbol text is present.'
Assert-True ($selected.internalSymbolPresent -eq $false) 'InternalSymbol is present.'
Assert-True ($selected.snapshotOnlyPresent -eq $false) 'SnapshotOnly is present.'
Assert-True ($selected.subscriptionRequestTypeZeroPresent -eq $false) 'SubscriptionRequestType=0 is present.'
Assert-True ($nonSelected.priorDiagnosticOrLegacyProfileSelectedForFutureRetry -eq $false) 'Prior diagnostic/legacy profile selected.'

Assert-True ($state.finalStateEvidenceContractActive -eq $true) 'Final state evidence readiness missing.'
Assert-True ($state.classifiedMarketDataResponseAllExplicitStateFieldsFalseAllowed -eq $false) 'Expected evidence permits classified response with all state fields false.'
foreach ($field in @(
    'marketDataRequestWriteAttempted',
    'marketDataRequestWriteSucceeded',
    'marketDataRequestResponseReadAttempted',
    'marketDataRequestReachedBoundedResponseClassification',
    'marketDataRequestSentLegacyFlag')) {
    Assert-True ($state.requiredExplicitStateFields -contains $field) "Explicit state field missing: $field"
    Assert-True ($expected.requiredExplicitStateFields -contains $field) "Expected evidence state field missing: $field"
}

Assert-True ($extraction.r188RepairImplemented -eq $true) 'Enhanced reject extraction propagation readiness missing.'
foreach ($field in @('marketDataRejectSanitizedSubcategory', 'sessionRejectSanitizedSubcategory', 'rejectReasonExtractionSource')) {
    Assert-True ($extraction.requiredFutureFields -contains $field) "Enhanced extraction field missing: $field"
    Assert-True ($expected.requiredRejectExtractionFields -contains $field) "Expected evidence extraction field missing: $field"
}
Assert-True ($extraction.msgTypeYTag281SubcategoryRequired -eq $true) 'MsgType Y/tag 281 subcategory readiness missing.'
Assert-True ($extraction.msgType3Tags371372373SubcategoryRequired -eq $true) 'MsgType 3/tags 371/372/373 subcategory readiness missing.'
Assert-True ($extraction.rejectReasonNotAvailableMeansDetailedTagsAbsent -eq $true) 'RejectReasonNotAvailable distinction missing.'
Assert-True ($extraction.propagationFailureMustBeClassifiedSeparately -eq $true) 'Propagation failure distinction missing.'
Assert-True ($extraction.runtimeCliSummaryDidNotEmitR184SubcategoryAllowedAfterRepair -eq $false) 'RuntimeCliSummaryDidNotEmitR184Subcategory still allowed after repair.'
Assert-True ($expected.rejectReasonNotAvailableDistinctFromPropagationFailure -eq $true) 'Expected evidence cannot distinguish RejectReasonNotAvailable from propagation failure.'
Assert-True ($expected.propagationFailureMustNotBeReportedAsRejectReasonNotAvailable -eq $true) 'Expected evidence allows propagation failure to masquerade as RejectReasonNotAvailable.'

Assert-True ($reporting.r133R135BroadSanitizedReasonReportingReady -eq $true) 'R133/R135 broad sanitized reason readiness missing.'
Assert-True ($reporting.sessionRejectSanitizedReasonCategoryRequired -eq $true) 'sessionRejectSanitizedReasonCategory readiness missing.'
Assert-True ($reporting.sanitizedSessionRejectReasonCategoryRequired -eq $true) 'sanitizedSessionRejectReasonCategory readiness missing.'
Assert-True ($expected.broadReasonFieldsRequired -contains 'sessionRejectSanitizedReasonCategory') 'Expected evidence omits sessionRejectSanitizedReasonCategory.'
Assert-True ($expected.broadReasonFieldsRequired -contains 'sanitizedSessionRejectReasonCategory') 'Expected evidence omits sanitizedSessionRejectReasonCategory.'

Assert-True ($preflight.freshExactOperatorApprovalRequired -eq $true) 'Fresh approval template/constraint missing.'
Assert-True ($preflight.separateConcreteWeekdayMarketHoursConfirmationRequired -eq $true) 'Concrete weekday market-hours requirement missing.'
Assert-True ($preflight.placeholderMarketHoursForbidden -eq $true) 'Placeholder market-hours rejection missing.'
Assert-True ($preflight.exactlyOneBoundedAttemptRequired -eq $true) 'Exactly-one-bounded-attempt constraint missing.'
Assert-True ($preflight.adapterMode -eq 'real-bounded-executable-readonly') 'Adapter mode mismatch.'
Assert-True ($preflight.stopAfterAttempt -eq $true) 'Stop-after-attempt constraint missing.'

$approvalTemplatePath = Join-Path $ArtifactRoot 'phase-lmax-r189-r191-operator-approval-template.md'
Assert-True (Test-Path -LiteralPath $approvalTemplatePath) 'Fresh approval template is missing.'
$approvalTemplate = Get-Content -LiteralPath $approvalTemplatePath -Raw
Assert-True ($approvalTemplate.Contains('I, Philippe, explicitly approve Phase LMAX-R191')) 'R191 approval template text missing.'
Assert-True ($approvalTemplate.Contains('weekday active FX market-data availability window for R191')) 'R191 market-hours confirmation template missing.'

Assert-True ($approvedUniverse.approvedUniversePreserved -eq $true) 'Approved universe preservation missing.'
foreach ($symbol in @('GBPUSD', 'EURGBP', 'AUDUSD', 'USDJPY')) {
    Assert-True ($approvedUniverse.approvedInstruments -contains $symbol) "Approved instrument missing: $symbol"
}
Assert-True ($usdJpy.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat weakened.'
Assert-True ($usdJpy.usdJpySecurityId -eq '4004') 'USDJPY SecurityID weakened.'
Assert-True ($usdJpy.usdJpySecurityIdSource -eq '8') 'USDJPY SecurityIDSource weakened.'

Assert-True ($forbidden.externalActivationAttempted -eq $false) 'R189 external activation detected.'
Assert-True ($forbidden.socketOpened -eq $false) 'R189 socket/TCP action detected.'
Assert-True ($forbidden.tlsOpened -eq $false) 'R189 TLS action detected.'
Assert-True ($forbidden.fixSessionOpened -eq $false) 'R189 FIX action detected.'
Assert-True ($forbidden.liveMarketDataRequestSent -eq $false) 'R189 live MarketDataRequest detected.'
Assert-True ($forbidden.liveMarketDataResponseRead -eq $false) 'R189 live MarketDataResponse detected.'
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
Assert-True ($sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned=false missing.'

Assert-True ($next.recommendedNextPhase -like 'LMAX-R191*') 'Next phase recommendation is missing.'
Assert-True ($next.mustRequireFreshExplicitOperatorApproval -eq $true) 'Next phase fresh approval requirement missing.'
Assert-True ($next.mustRequireSeparateConcreteWeekdayMarketHoursConfirmation -eq $true) 'Next phase concrete market-hours requirement missing.'
Assert-True ($next.mustRequireExactlyOneBoundedAttempt -eq $true) 'Next phase exactly-one-attempt requirement missing.'
Assert-True ($next.doNotExecuteInR189 -eq $true) 'R189 recommendation allows execution.'

$artifactText = Get-ChildItem -LiteralPath $ArtifactRoot -Filter 'phase-lmax-r189-*' -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combinedArtifactText = [string]::Join("`n", $artifactText)
foreach ($forbiddenToken in @('35=V', '35=Y', '35=3', '281=', '371=', '372=', '373=', '58=', [string][char]1, 'password=', 'username=', 'SenderCompId', 'TargetCompId')) {
    Assert-True (-not $combinedArtifactText.Contains($forbiddenToken)) "Forbidden serialized token detected in R189 artifacts: $forbiddenToken"
}

Assert-True ($gate.build -like 'PASS*') 'Build evidence missing.'
Assert-True ($gate.focusedTests -like 'PASS*') 'Focused test evidence missing.'
Assert-True ($gate.unitTests -like 'PASS*') 'Unit test evidence missing.'
Assert-True ($gate.integrationTests -like 'PASS*') 'Integration test evidence missing.'
Assert-True ($gate.validator -eq 'LMAX_R189_VALIDATION_PASS') 'Validator evidence missing.'

'LMAX_R189_VALIDATION_PASS'
