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

function Assert-ContainsAll {
    param(
        [object[]]$Values,
        [string[]]$Expected,
        [string]$Message
    )

    foreach ($item in $Expected) {
        Assert-True ($Values -contains $item) "$Message Missing: $item"
    }
}

$script:ArtifactRoot = Join-Path $Root 'artifacts/readiness/lmax-runtime-enablement'

Assert-True (Test-Path -LiteralPath (Join-Path $ArtifactRoot 'phase-lmax-r194-sessionreject-tag-detail-retry-readiness-summary.md')) 'R194 summary missing.'

$readiness = Read-Json 'phase-lmax-r194-sessionreject-tag-detail-retry-readiness.json'
$selected = Read-Json 'phase-lmax-r194-selected-profile-evidence.json'
$detail = Read-Json 'phase-lmax-r194-sessionreject-detail-extraction-readiness.json'
$state = Read-Json 'phase-lmax-r194-final-state-evidence-contract-readiness.json'
$nonSelected = Read-Json 'phase-lmax-r194-non-selected-profiles-evidence.json'
$reservation = Read-Json 'phase-lmax-r194-next-activation-phase-reservation-decision.json'
$preflight = Read-Json 'phase-lmax-r194-r195-preflight-checklist.json'
$expected = Read-Json 'phase-lmax-r194-r195-expected-evidence-contract.json'
$reporting = Read-Json 'phase-lmax-r194-reporting-contract-readiness.json'
$universe = Read-Json 'phase-lmax-r194-approved-universe-preservation.json'
$usdJpy = Read-Json 'phase-lmax-r194-usdjpy-caveat-preservation.json'
$sanitization = Read-Json 'phase-lmax-r194-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r194-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r194-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r194-next-phase-recommendation.json'
$r191 = Read-Json 'phase-lmax-r191-enhanced-reject-reason-repaired-activation.json'
$r192 = Read-Json 'phase-lmax-r192-enhanced-reject-reason-review.json'
$r193 = Read-Json 'phase-lmax-r193-sessionreject-tag-detail-enrichment.json'
$r193Gate = Read-Json 'phase-lmax-r193-gate-validation.json'

Assert-True ($readiness.classification -in @(
    'LMAX_R194_PASS_SESSIONREJECT_TAG_DETAIL_RETRY_READINESS_NO_EXTERNAL',
    'LMAX_R194_FAIL_NEW_EXTERNAL_ACTION_DETECTED',
    'LMAX_R194_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID',
    'LMAX_R194_FAIL_PROFILE_SELECTION_MISSING',
    'LMAX_R194_FAIL_MDUPDATETYPE_PROFILE_CONTRACT_WEAKENED',
    'LMAX_R194_FAIL_SESSIONREJECT_DETAIL_EXTRACTION_READINESS_MISSING',
    'LMAX_R194_FAIL_REFTAG_OR_SESSIONREASON_CONTRACT_MISSING',
    'LMAX_R194_FAIL_FINAL_STATE_EVIDENCE_CONTRACT_MISSING',
    'LMAX_R194_FAIL_APPROVAL_OR_MARKET_HOURS_CONSTRAINT_MISSING',
    'LMAX_R194_FAIL_USDJPY_CAVEAT_WEAKENED',
    'LMAX_R194_FAIL_RAW_FIX_OR_REJECT_LEAK_RISK',
    'LMAX_R194_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK',
    'LMAX_R194_FAIL_FORBIDDEN_ACTION_RISK',
    'LMAX_R194_FAIL_API_WORKER_GATEWAY_REGRESSION',
    'LMAX_R194_FAIL_BUILD_OR_TESTS')) 'R194 classification is not allowed.'
Assert-True ($readiness.classification -eq 'LMAX_R194_PASS_SESSIONREJECT_TAG_DETAIL_RETRY_READINESS_NO_EXTERNAL') 'R194 classification mismatch.'
Assert-True ($readiness.noExternal -eq $true) 'R194 must be no-external.'

Assert-True ($r191.attemptCount -eq 1) 'R191 evidence missing or attemptCount invalid.'
Assert-True ($r191.classification -eq 'LMAX_R191_FAIL_SESSIONREJECT_WITH_SANITIZED_REASON_REPORTED') 'R191 evidence mismatch.'
Assert-True ($r192.classification -eq 'LMAX_R192_PASS_ENHANCED_REJECT_REVIEW_SESSION_REJECT_TAG_DETAIL_ENRICHMENT_RECOMMENDED_NO_EXTERNAL') 'R192 evidence missing.'
Assert-True ($r193.classification -eq 'LMAX_R193_PASS_SESSIONREJECT_TAG_DETAIL_ENRICHMENT_RETRY_READINESS_RECOMMENDED_NO_EXTERNAL') 'R193 evidence missing.'
Assert-True ($r193Gate.validator -eq 'LMAX_R193_VALIDATION_PASS') 'R193 validator evidence missing.'

Assert-True ($reservation.reservedNextRealActivationPhase -eq 'LMAX-R195') 'Next activation phase must be LMAX-R195.'
Assert-True ($reservation.reservedNextRealActivationPhaseNumber -eq 195) 'Next activation phase number invalid.'
Assert-True ($reservation.oddNumbered -eq $true) 'Next activation phase must be odd-numbered.'
Assert-True ($reservation.explicitlyReserved -eq $true) 'Next activation phase not explicitly reserved.'
Assert-True ($reservation.evenNumberedActivationAllowed -eq $false) 'Even-numbered activation allowed.'
Assert-True ($readiness.nextActivationPhase -eq 'LMAX-R195') 'Readiness next activation phase mismatch.'
Assert-True ($readiness.nextActivationPhaseOddNumbered -eq $true) 'Readiness odd-number confirmation missing.'
Assert-True ($readiness.nextActivationPhaseExplicitlyReserved -eq $true) 'Readiness reservation missing.'

$profile = 'UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument'
Assert-True ($selected.selectedFutureProfile -eq $profile) 'Selected profile evidence missing.'
Assert-True ($selected.manualRealBoundedPathSelection -eq $profile) 'Manual real-bounded path profile mismatch.'
Assert-True ($selected.selectedDiagnosticProfileCount -eq 1) 'More than one diagnostic profile selected.'
Assert-True ($selected.gbpusdOnly -eq $true) 'Future retry is not GBPUSD-only.'
Assert-True ($selected.singleRequest -eq $true) 'Future retry is not single-request.'
Assert-True ($selected.securityId -eq '4002') 'SecurityID=4002 missing.'
Assert-True ($selected.securityIdSource -eq '8') 'SecurityIDSource=8 missing.'
Assert-True ($selected.subscriptionRequestType -eq '1') 'SubscriptionRequestType=1 missing.'
Assert-True ($selected.mdUpdateType -eq '0') 'MDUpdateType=0 missing.'
Assert-True ($selected.marketDepth -eq '1') 'MarketDepth=1 missing.'
Assert-True ($selected.noMdEntryTypes -eq 2) 'NoMDEntryTypes=2 missing.'
Assert-True ($selected.bidAndOfferTogether -eq $true) 'Bid+offer together evidence missing.'
Assert-True ($selected.symbolTextIncluded -eq $false) 'Symbol text included.'
Assert-True ($selected.internalSymbolIncluded -eq $false) 'InternalSymbol included.'
Assert-True ($selected.snapshotOnlyIncluded -eq $false) 'SnapshotOnly included.'
Assert-True ($selected.subscriptionRequestTypeZeroIncluded -eq $false) 'SubscriptionRequestType=0 included.'
Assert-True ($nonSelected.priorDiagnosticOrLegacyProfileSelected -eq $false) 'Prior diagnostic or legacy profile selected.'
Assert-True ($nonSelected.moreThanOneDiagnosticProfileSelected -eq $false) 'Multiple diagnostic profiles selected.'

Assert-True ($state.finalStateEvidenceContractReady -eq $true) 'Final-state evidence readiness missing.'
Assert-True ($state.classifiedMarketDataResponseCannotEmitAllExplicitStateFieldsFalse -eq $true) 'Classified response can emit all explicit state fields false.'
Assert-ContainsAll $state.requiredExplicitStateFields @(
    'marketDataRequestWriteAttempted',
    'marketDataRequestWriteSucceeded',
    'marketDataRequestResponseReadAttempted',
    'marketDataRequestReachedBoundedResponseClassification',
    'marketDataRequestSentLegacyFlag') 'Explicit state field contract incomplete.'
Assert-True ($expected.classifiedMarketDataResponseWithAllExplicitStateFieldsFalseAllowed -eq $false) 'Expected contract allows false state contradiction.'

Assert-True ($detail.r193SessionRejectDetailExtractionReady -eq $true) 'SessionReject detail extraction readiness missing.'
Assert-ContainsAll $detail.requiredFutureFields @(
    'sessionRejectRefTagIdSanitizedCategory',
    'sessionRejectReasonSanitizedCategory',
    'sessionRejectRefMsgTypeSanitizedCategory',
    'rejectReasonExtractionSource') 'SessionReject detail future fields missing.'
Assert-ContainsAll $detail.requiredRefTagIdCategories @(
    'RefTagID_SubscriptionRequestType_263',
    'RefTagID_MDUpdateType_265',
    'RefTagID_NoMDEntryTypes_267',
    'RefTagID_MDEntryType_269',
    'RefTagID_SecurityID_48',
    'RefTagID_NotAvailable') 'RefTagID readiness categories missing.'
Assert-ContainsAll $detail.requiredSessionRejectReasonCategories @(
    'SessionRejectReason_RequiredTagMissing',
    'SessionRejectReason_ValueIncorrect',
    'SessionRejectReason_NotAvailable') 'SessionRejectReason readiness categories missing.'
Assert-ContainsAll $detail.requiredRefMsgTypeCategories @(
    'RefMsgType_MarketDataRequest',
    'RefMsgType_NotAvailable') 'RefMsgType readiness categories missing.'
Assert-True ($detail.notAvailableDistinguishedFromPropagationFailure -eq $true) 'NotAvailable not distinguished from propagation failure.'
Assert-True ($detail.propagationFailureMustFailValidation -eq $true) 'Propagation failure does not fail validation.'

Assert-ContainsAll $expected.requiredSessionRejectDetailFields @(
    'sessionRejectRefTagIdSanitizedCategory',
    'sessionRejectReasonSanitizedCategory',
    'sessionRejectRefMsgTypeSanitizedCategory',
    'rejectReasonExtractionSource') 'Expected detail evidence contract incomplete.'
Assert-True ($expected.notAvailableValuesAreValidOnlyWhenDetailedTagsAreAbsent -eq $true) 'NotAvailable meaning missing in expected contract.'
Assert-True ($expected.missingDetailFieldsRepresentPropagationFailure -eq $true) 'Missing detail fields not treated as propagation failure.'
Assert-True ($expected.propagationFailureMustFailValidation -eq $true) 'Propagation failure must fail validation.'

Assert-True ($reporting.broadSanitizedReasonReportingReady -eq $true) 'Broad sanitized reason reporting missing.'
Assert-ContainsAll $reporting.requiredBroadFields @(
    'sessionRejectSanitizedReasonCategory',
    'sanitizedSessionRejectReasonCategory') 'Broad sanitized reason fields missing.'
Assert-True ($reporting.sessionRejectDetailReportingReady -eq $true) 'SessionReject detail reporting missing.'
Assert-ContainsAll $reporting.requiredDetailFields @(
    'sessionRejectRefTagIdSanitizedCategory',
    'sessionRejectReasonSanitizedCategory',
    'sessionRejectRefMsgTypeSanitizedCategory',
    'rejectReasonExtractionSource') 'Reporting detail fields missing.'

Assert-True ($preflight.operatorApproval.freshExactApprovalRequired -eq $true) 'Fresh approval template missing.'
Assert-True ($preflight.operatorApproval.priorApprovalReusable -eq $false) 'Prior approval allowed.'
Assert-True ($preflight.marketHours.separateConcreteWeekdayActiveFxMarketDataWindowRequired -eq $true) 'Concrete weekday market-hours requirement missing.'
Assert-True ($preflight.marketHours.placeholderTimeAllowed -eq $false) 'Placeholder market-hours time allowed.'
Assert-True ($preflight.exactlyOneBoundedAttempt -eq $true) 'Exactly-one bounded attempt constraint missing.'
Assert-True ($preflight.stopAfterAttempt -eq $true) 'Stop-after-attempt missing.'

Assert-True ($universe.approvedUniversePreserved -eq $true) 'Approved universe not preserved.'
Assert-ContainsAll $universe.approvedUniverse @('GBPUSD', 'EURGBP', 'AUDUSD', 'USDJPY') 'Approved universe weakened.'
Assert-True ($usdJpy.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat weakened.'
Assert-True ($usdJpy.securityId -eq '4004') 'USDJPY SecurityID caveat weakened.'
Assert-True ($usdJpy.securityIdSource -eq '8') 'USDJPY SecurityIDSource caveat weakened.'

Assert-True ($forbidden.externalActivationAttempted -eq $false) 'R194 external activation detected.'
Assert-True ($forbidden.socketOpened -eq $false) 'R194 socket/TCP action detected.'
Assert-True ($forbidden.tlsOpened -eq $false) 'R194 TLS action detected.'
Assert-True ($forbidden.fixSessionOpened -eq $false) 'R194 FIX action detected.'
Assert-True ($forbidden.liveMarketDataRequestSent -eq $false) 'R194 live MarketDataRequest detected.'
Assert-True ($forbidden.liveMarketDataResponseRead -eq $false) 'R194 live MarketDataResponse detected.'
Assert-True ($forbidden.ordersIntroduced -eq $false) 'Order path introduced.'
Assert-True ($forbidden.tradingEnabled -eq $false) 'Trading enabled.'
Assert-True ($forbidden.schedulerStarted -eq $false) 'Scheduler started.'
Assert-True ($forbidden.pollingStarted -eq $false) 'Polling started.'
Assert-True ($forbidden.serviceStarted -eq $false) 'Service started.'
Assert-True ($forbidden.replayIntroduced -eq $false) 'Replay introduced.'
Assert-True ($forbidden.shadowReplayIntroduced -eq $false) 'Shadow replay introduced.'
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

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R195') 'Next phase recommendation missing.'
Assert-True ($next.requiresFreshExactOperatorApproval -eq $true) 'Next phase fresh approval missing.'
Assert-True ($next.requiresSeparateConcreteWeekdayMarketHoursConfirmation -eq $true) 'Next phase market-hours confirmation missing.'
Assert-True ($next.requiresExactlyOneBoundedAttempt -eq $true) 'Next phase bounded attempt missing.'
Assert-True ($next.stopAfterAttemptAndProceedToR196ReviewGateOnly -eq $true) 'R196 review gate stop requirement missing.'
Assert-True ($next.blindLiveRetryAllowed -eq $false) 'Blind live retry allowed.'

$operatorTemplatePath = Join-Path $ArtifactRoot 'phase-lmax-r194-r195-operator-approval-template.md'
$promptPath = Join-Path $ArtifactRoot 'phase-lmax-r194-r195-activation-prompt-compact.md'
Assert-True (Test-Path -LiteralPath $operatorTemplatePath) 'Fresh approval template missing.'
Assert-True (Test-Path -LiteralPath $promptPath) 'R195 compact activation prompt missing.'
$operatorTemplate = Get-Content -LiteralPath $operatorTemplatePath -Raw
Assert-True ($operatorTemplate.Contains('I, Philippe, explicitly approve Phase LMAX-R195')) 'R195 approval template mismatch.'
Assert-True ($operatorTemplate.Contains('weekday active FX market-data availability window for R195')) 'R195 market-hours confirmation template missing.'

$artifactText = Get-ChildItem -LiteralPath $ArtifactRoot -Filter 'phase-lmax-r194-*' -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combinedArtifactText = [string]::Join("`n", $artifactText)
foreach ($forbiddenToken in @('35=V', '35=Y', '35=3', '281=', '371=', '372=', '373=', '58=', [string][char]1, 'password=', 'username=', 'SenderCompId', 'TargetCompId')) {
    Assert-True (-not $combinedArtifactText.Contains($forbiddenToken)) "Forbidden serialized token detected in R194 artifacts: $forbiddenToken"
}

$gatePath = Join-Path $ArtifactRoot 'phase-lmax-r194-gate-validation.json'
Assert-True (Test-Path -LiteralPath $gatePath) 'R194 build/test/validator evidence missing.'
$gate = Get-Content -LiteralPath $gatePath -Raw | ConvertFrom-Json
Assert-True ($gate.build -like 'PASS*') 'Build evidence missing.'
Assert-True ($gate.focusedTests -like 'PASS*') 'Focused test evidence missing.'
Assert-True ($gate.unitTests -like 'PASS*') 'Unit test evidence missing.'
Assert-True ($gate.integrationTests -like 'PASS*') 'Integration test evidence missing.'
Assert-True ($gate.validator -eq 'LMAX_R194_VALIDATION_PASS') 'Validator evidence missing.'

'LMAX_R194_VALIDATION_PASS'
