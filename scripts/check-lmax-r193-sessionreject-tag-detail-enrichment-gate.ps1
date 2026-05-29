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

$summary = Join-Path $ArtifactRoot 'phase-lmax-r193-sessionreject-tag-detail-enrichment-summary.md'
Assert-True (Test-Path -LiteralPath $summary) 'R193 summary missing.'

$review = Read-Json 'phase-lmax-r193-sessionreject-tag-detail-enrichment.json'
$refTag = Read-Json 'phase-lmax-r193-ref-tagid-category-contract.json'
$reason = Read-Json 'phase-lmax-r193-sessionrejectreason-category-contract.json'
$refMsgType = Read-Json 'phase-lmax-r193-refmsgtype-category-contract.json'
$reporting = Read-Json 'phase-lmax-r193-cli-artifact-reporting-contract.json'
$tests = Read-Json 'phase-lmax-r193-fixture-test-evidence.json'
$rehydration = Read-Json 'phase-lmax-r193-r191-rehydration-decision.json'
$nextAction = Read-Json 'phase-lmax-r193-next-action-decision-gate.json'
$sanitization = Read-Json 'phase-lmax-r193-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r193-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r193-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r193-next-phase-recommendation.json'
$universe = Read-Json 'phase-lmax-r193-approved-universe-preservation.json'
$usdJpy = Read-Json 'phase-lmax-r193-usdjpy-caveat-preservation.json'
$gate = Read-Json 'phase-lmax-r193-gate-validation.json'
$r191 = Read-Json 'phase-lmax-r191-enhanced-reject-reason-repaired-activation.json'
$r192 = Read-Json 'phase-lmax-r192-enhanced-reject-reason-review.json'

Assert-True ($review.classification -in @(
    'LMAX_R193_PASS_SESSIONREJECT_TAG_DETAIL_ENRICHMENT_READY_NO_EXTERNAL',
    'LMAX_R193_PASS_SESSIONREJECT_TAG_DETAIL_ENRICHMENT_RETRY_READINESS_RECOMMENDED_NO_EXTERNAL',
    'LMAX_R193_PASS_SESSIONREJECT_TAG_DETAIL_ENRICHMENT_SUPPORT_PACKAGE_RECOMMENDED_NO_EXTERNAL',
    'LMAX_R193_FAIL_NEW_EXTERNAL_ACTION_DETECTED',
    'LMAX_R193_FAIL_SESSIONREJECT_DETAIL_CONTRACT_MISSING',
    'LMAX_R193_FAIL_REFTAGID_CATEGORY_CONTRACT_MISSING',
    'LMAX_R193_FAIL_SESSIONREJECTREASON_CATEGORY_CONTRACT_MISSING',
    'LMAX_R193_FAIL_REPORTING_CONTRACT_MISSING',
    'LMAX_R193_FAIL_FIXTURE_TEST_EVIDENCE_MISSING',
    'LMAX_R193_FAIL_RAW_FIX_OR_REJECT_LEAK_RISK',
    'LMAX_R193_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK',
    'LMAX_R193_FAIL_USDJPY_CAVEAT_WEAKENED',
    'LMAX_R193_FAIL_FORBIDDEN_ACTION_RISK',
    'LMAX_R193_FAIL_API_WORKER_GATEWAY_REGRESSION',
    'LMAX_R193_FAIL_BUILD_OR_TESTS')) 'R193 classification is not allowed.'
Assert-True ($review.classification -eq 'LMAX_R193_PASS_SESSIONREJECT_TAG_DETAIL_ENRICHMENT_RETRY_READINESS_RECOMMENDED_NO_EXTERNAL') 'R193 classification mismatch.'
Assert-True ($review.noExternal -eq $true) 'R193 must be no-external.'

Assert-True ($r191.attemptCount -eq 1) 'R191 evidence missing or attemptCount invalid.'
Assert-True ($r191.classification -eq 'LMAX_R191_FAIL_SESSIONREJECT_WITH_SANITIZED_REASON_REPORTED') 'R191 classification mismatch.'
Assert-True ($r192.classification -eq 'LMAX_R192_PASS_ENHANCED_REJECT_REVIEW_SESSION_REJECT_TAG_DETAIL_ENRICHMENT_RECOMMENDED_NO_EXTERNAL') 'R192 decision evidence missing.'
Assert-True ($review.r191EvidenceReviewed -eq $true) 'R191 evidence review missing.'
Assert-True ($review.r192DecisionReviewed -eq $true) 'R192 decision review missing.'

Assert-ContainsAll $review.broadCategoriesPreserved @(
    'SessionRejectObservedWithSanitizedReason',
    'MalformedOrUnsupportedMarketDataRequestPlausible',
    'SessionRejectRefMsgTypeMarketDataRequest') 'Broad category preservation failed.'
Assert-ContainsAll $review.detailFieldsAdded @(
    'sessionRejectRefTagIdSanitizedCategory',
    'sessionRejectReasonSanitizedCategory',
    'sessionRejectRefMsgTypeSanitizedCategory',
    'rejectReasonExtractionSource') 'SessionReject detail field contract incomplete.'

Assert-True ($refTag.field -eq 'sessionRejectRefTagIdSanitizedCategory') 'RefTagID reporting field mismatch.'
Assert-ContainsAll $refTag.categories @(
    'RefTagID_MDReqID_262',
    'RefTagID_SubscriptionRequestType_263',
    'RefTagID_MarketDepth_264',
    'RefTagID_MDUpdateType_265',
    'RefTagID_NoMDEntryTypes_267',
    'RefTagID_MDEntryType_269',
    'RefTagID_NoRelatedSym_146',
    'RefTagID_SecurityID_48',
    'RefTagID_SecurityIDSource_22',
    'RefTagID_Symbol_55',
    'RefTagID_InstrumentComponent',
    'RefTagID_UnknownOrOtherSanitized',
    'RefTagID_NotAvailable') 'RefTagID category contract incomplete.'
Assert-True ($refTag.notAvailableMeans -like '*absent*') 'RefTagID NotAvailable meaning missing.'
Assert-True ($refTag.propagationFailureRepresentation -like '*validator failure*') 'RefTagID propagation failure distinction missing.'

Assert-True ($reason.field -eq 'sessionRejectReasonSanitizedCategory') 'SessionRejectReason reporting field mismatch.'
Assert-ContainsAll $reason.categories @(
    'SessionRejectReason_InvalidTagNumber',
    'SessionRejectReason_RequiredTagMissing',
    'SessionRejectReason_TagNotDefinedForMessageType',
    'SessionRejectReason_UndefinedTag',
    'SessionRejectReason_TagSpecifiedWithoutValue',
    'SessionRejectReason_ValueIncorrect',
    'SessionRejectReason_IncorrectDataFormat',
    'SessionRejectReason_DecryptionProblem',
    'SessionRejectReason_SignatureProblem',
    'SessionRejectReason_CompIDProblem',
    'SessionRejectReason_SendingTimeAccuracyProblem',
    'SessionRejectReason_InvalidMsgType',
    'SessionRejectReason_OtherSanitized',
    'SessionRejectReason_NotAvailable') 'SessionRejectReason category contract incomplete.'
Assert-True ($reason.notAvailableMeans -like '*absent*') 'SessionRejectReason NotAvailable meaning missing.'
Assert-True ($reason.propagationFailureRepresentation -like '*validator failure*') 'SessionRejectReason propagation failure distinction missing.'

Assert-True ($refMsgType.field -eq 'sessionRejectRefMsgTypeSanitizedCategory') 'RefMsgType reporting field mismatch.'
Assert-ContainsAll $refMsgType.categories @(
    'RefMsgType_MarketDataRequest',
    'RefMsgType_OtherSanitized',
    'RefMsgType_NotAvailable') 'RefMsgType category contract incomplete.'
Assert-True ($refMsgType.broadSubcategoryPreserved -eq 'SessionRejectRefMsgTypeMarketDataRequest') 'RefMsgType broad category not preserved.'

Assert-ContainsAll $reporting.cliFields @(
    'marketDataRejectSanitizedSubcategory',
    'sessionRejectSanitizedSubcategory',
    'rejectReasonExtractionSource',
    'sessionRejectRefTagIdSanitizedCategory',
    'sessionRejectReasonSanitizedCategory',
    'sessionRejectRefMsgTypeSanitizedCategory') 'CLI reporting contract incomplete.'
Assert-ContainsAll $reporting.artifactFields @(
    'sessionRejectRefTagIdSanitizedCategory',
    'sessionRejectReasonSanitizedCategory',
    'sessionRejectRefMsgTypeSanitizedCategory',
    'rejectReasonExtractionSource') 'Artifact reporting contract incomplete.'

Assert-True ($tests.focusedTests -like 'PASS*') 'Focused fixture test evidence missing.'
Assert-True ($tests.fixtureCoverage.msgType3RefMsgTypeMarketDataRequest -eq $true) 'MsgType3 RefMsgType fixture missing.'
Assert-True ($tests.fixtureCoverage.refTagIdSubscriptionRequestType263 -eq $true) 'RefTagID 263 fixture missing.'
Assert-True ($tests.fixtureCoverage.refTagIdMDUpdateType265 -eq $true) 'RefTagID 265 fixture missing.'
Assert-True ($tests.fixtureCoverage.refTagIdNoMDEntryTypes267 -eq $true) 'RefTagID 267 fixture missing.'
Assert-True ($tests.fixtureCoverage.refTagIdMDEntryType269 -eq $true) 'RefTagID 269 fixture missing.'
Assert-True ($tests.fixtureCoverage.refTagIdSecurityID48 -eq $true) 'RefTagID 48 fixture missing.'
Assert-True ($tests.fixtureCoverage.sessionRejectReasonRequiredTagMissing -eq $true) 'Required-tag-missing fixture missing.'
Assert-True ($tests.fixtureCoverage.sessionRejectReasonValueIncorrect -eq $true) 'Value-incorrect fixture missing.'
Assert-True ($tests.fixtureCoverage.missingRefTagIdAndSessionRejectReasonAsNotAvailable -eq $true) 'Missing detail NotAvailable fixture missing.'
Assert-True ($tests.fixtureCoverage.notAvailableDistinguishedFromPropagationFailure -eq $true) 'NotAvailable propagation distinction missing.'
Assert-True ($tests.fixtureCoverage.rawFixAndRejectTextNotSerialized -eq $true) 'Raw FIX/reject sanitization fixture missing.'
Assert-True ($tests.fixtureCoverage.cliContractEmitsDetailFields -eq $true) 'CLI field fixture missing.'
Assert-True ($tests.fixtureCoverage.executablePathPropagatesDetailFields -eq $true) 'Executable path propagation fixture missing.'

Assert-True ($rehydration.r191ExactRefTagIdRehydrated -eq $false) 'R191 RefTagID should not be rehydrated.'
Assert-True ($rehydration.r191ExactSessionRejectReasonRehydrated -eq $false) 'R191 SessionRejectReason should not be rehydrated.'
Assert-True ($rehydration.futureEvidenceDecision -like '*sanitized*') 'Future evidence decision missing.'

Assert-True ($nextAction.decision -eq 'RetryReadinessRecommendedAfterSessionRejectTagDetailEnrichment') 'Next action decision missing.'
Assert-True ($nextAction.liveRetryAllowedNow -eq $false) 'R193 allows live retry now.'
Assert-True ($nextAction.nextPhaseMustRemainNoExternal -eq $true) 'R194 must remain no-external.'
Assert-True ($nextAction.blindLiveRetryBlocked -eq $true) 'Blind retry not blocked.'
Assert-True ($next.mustRemainNoExternal -eq $true) 'Next recommendation must remain no-external.'
Assert-True ($next.liveRetryAllowedInR194 -eq $false) 'R194 must not allow live retry.'

Assert-True ($universe.approvedUniversePreserved -eq $true) 'Approved universe not preserved.'
Assert-ContainsAll $universe.approvedUniverse @('GBPUSD', 'EURGBP', 'AUDUSD', 'USDJPY') 'Approved universe weakened.'
Assert-True ($usdJpy.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat not preserved.'
Assert-True ($usdJpy.securityId -eq '4004') 'USDJPY SecurityID caveat weakened.'
Assert-True ($usdJpy.securityIdSource -eq '8') 'USDJPY SecurityIDSource caveat weakened.'

Assert-True ($forbidden.externalActivationAttempted -eq $false) 'R193 external activation detected.'
Assert-True ($forbidden.socketOpened -eq $false) 'R193 socket/TCP action detected.'
Assert-True ($forbidden.tlsOpened -eq $false) 'R193 TLS action detected.'
Assert-True ($forbidden.fixSessionOpened -eq $false) 'R193 FIX action detected.'
Assert-True ($forbidden.liveMarketDataRequestSent -eq $false) 'R193 live MarketDataRequest detected.'
Assert-True ($forbidden.liveMarketDataResponseRead -eq $false) 'R193 live MarketDataResponse detected.'
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

$artifactText = Get-ChildItem -LiteralPath $ArtifactRoot -Filter 'phase-lmax-r193-*' -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combinedArtifactText = [string]::Join("`n", $artifactText)
foreach ($forbiddenToken in @('35=V', '35=Y', '35=3', '281=', '371=', '372=', '373=', '58=', [string][char]1, 'password=', 'username=', 'SenderCompId', 'TargetCompId')) {
    Assert-True (-not $combinedArtifactText.Contains($forbiddenToken)) "Forbidden serialized token detected in R193 artifacts: $forbiddenToken"
}

$program = Get-Content -LiteralPath (Join-Path $Root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/Program.cs') -Raw
Assert-True ($program.Contains('sessionRejectRefTagIdSanitizedCategory=')) 'CLI RefTagID detail field missing.'
Assert-True ($program.Contains('sessionRejectReasonSanitizedCategory=')) 'CLI SessionRejectReason detail field missing.'
Assert-True ($program.Contains('sessionRejectRefMsgTypeSanitizedCategory=')) 'CLI RefMsgType detail field missing.'

$operation = Get-Content -LiteralPath (Join-Path $Root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualMarketDataRequestOperation.cs') -Raw
foreach ($category in @(
    'RefTagID_SubscriptionRequestType_263',
    'RefTagID_MDUpdateType_265',
    'RefTagID_NoMDEntryTypes_267',
    'RefTagID_MDEntryType_269',
    'RefTagID_SecurityID_48',
    'SessionRejectReason_RequiredTagMissing',
    'SessionRejectReason_ValueIncorrect',
    'RefMsgType_MarketDataRequest')) {
    Assert-True ($operation.Contains($category)) "Parser category missing: $category"
}

Assert-True ($gate.build -like 'PASS*') 'Build evidence missing.'
Assert-True ($gate.focusedTests -like 'PASS*') 'Focused test evidence missing.'
Assert-True ($gate.unitTests -like 'PASS*') 'Unit test evidence missing.'
Assert-True ($gate.integrationTests -like 'PASS*') 'Integration test evidence missing.'
Assert-True ($gate.validator -eq 'LMAX_R193_VALIDATION_PASS') 'Validator evidence missing.'

'LMAX_R193_VALIDATION_PASS'
