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

Assert-True (Test-Path -LiteralPath (Join-Path $ArtifactRoot 'phase-lmax-r196-sessionreject-tag-detail-review-summary.md')) 'R196 summary missing.'

$review = Read-Json 'phase-lmax-r196-sessionreject-tag-detail-review.json'
$clean = Read-Json 'phase-lmax-r196-r195-clean-evidence-confirmation.json'
$mdReqIdConfirm = Read-Json 'phase-lmax-r196-mdreqid-262-reject-confirmation.json'
$reason = Read-Json 'phase-lmax-r196-sessionrejectreason-valueincorrect-confirmation.json'
$shape = Read-Json 'phase-lmax-r196-mdreqid-current-value-shape-review.json'
$repair = Read-Json 'phase-lmax-r196-mdreqid-repair-decision.json'
$secondary = Read-Json 'phase-lmax-r196-secondary-candidates-demotion.json'
$nextAction = Read-Json 'phase-lmax-r196-next-action-decision-gate.json'
$sanitization = Read-Json 'phase-lmax-r196-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r196-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r196-api-worker-fake-gateway-audit.json'
$universe = Read-Json 'phase-lmax-r196-approved-universe-preservation.json'
$usdJpy = Read-Json 'phase-lmax-r196-usdjpy-caveat-preservation.json'
$next = Read-Json 'phase-lmax-r196-next-phase-recommendation.json'
$r195 = Read-Json 'phase-lmax-r195-sessionreject-tag-detail-activation.json'
$r195Detail = Read-Json 'phase-lmax-r195-sessionreject-tag-detail-evidence.json'
$r195Gate = Read-Json 'phase-lmax-r195-gate-validation.json'

Assert-True ($review.classification -in @(
    'LMAX_R196_PASS_SESSIONREJECT_TAG_DETAIL_REVIEW_MDREQID_REPAIR_RECOMMENDED_NO_EXTERNAL',
    'LMAX_R196_PASS_SESSIONREJECT_TAG_DETAIL_REVIEW_INCONCLUSIVE_SAFE_NO_EXTERNAL',
    'LMAX_R196_FAIL_NEW_EXTERNAL_ACTION_DETECTED',
    'LMAX_R196_FAIL_R195_EVIDENCE_MISSING',
    'LMAX_R196_FAIL_REFMSGTYPE_MARKETDATAREQUEST_CONFIRMATION_MISSING',
    'LMAX_R196_FAIL_MDREQID_262_CONFIRMATION_MISSING',
    'LMAX_R196_FAIL_SESSIONREJECTREASON_VALUEINCORRECT_CONFIRMATION_MISSING',
    'LMAX_R196_FAIL_MDREQID_REVIEW_MISSING',
    'LMAX_R196_FAIL_NEXT_ACTION_DECISION_MISSING',
    'LMAX_R196_FAIL_RAW_FIX_OR_REJECT_LEAK_RISK',
    'LMAX_R196_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK',
    'LMAX_R196_FAIL_USDJPY_CAVEAT_WEAKENED',
    'LMAX_R196_FAIL_FORBIDDEN_ACTION_RISK',
    'LMAX_R196_FAIL_API_WORKER_GATEWAY_REGRESSION',
    'LMAX_R196_FAIL_BUILD_OR_VALIDATOR')) 'R196 classification is not allowed.'
Assert-True ($review.classification -eq 'LMAX_R196_PASS_SESSIONREJECT_TAG_DETAIL_REVIEW_MDREQID_REPAIR_RECOMMENDED_NO_EXTERNAL') 'R196 classification mismatch.'
Assert-True ($review.noExternal -eq $true) 'R196 must be no-external.'

Assert-True ($r195.classification -eq 'LMAX_R195_FAIL_SESSIONREJECT_WITH_TAG_DETAIL_REPORTED') 'R195 evidence missing.'
Assert-True ($r195.attemptCount -eq 1) 'R195 attemptCount must be exactly one.'
Assert-True ($r195Gate.validator -eq 'LMAX_R195_VALIDATION_PASS') 'R195 validator evidence missing.'
Assert-True ($r195Detail.sessionRejectRefTagIdSanitizedCategory -eq 'RefTagID_MDReqID_262') 'R195 RefTagID evidence mismatch.'
Assert-True ($r195Detail.sessionRejectReasonSanitizedCategory -eq 'SessionRejectReason_ValueIncorrect') 'R195 SessionRejectReason evidence mismatch.'
Assert-True ($r195Detail.sessionRejectRefMsgTypeSanitizedCategory -eq 'RefMsgType_MarketDataRequest') 'R195 RefMsgType evidence mismatch.'

Assert-True ($clean.r195AttemptCount -eq 1) 'R195 clean evidence attemptCount invalid.'
Assert-True ($clean.cleanEvidenceConfirmed -eq $true) 'R195 clean evidence confirmation missing.'
Assert-True ($clean.marketDataRequestWriteAttempted -eq $true) 'R195 write attempted missing.'
Assert-True ($clean.marketDataRequestWriteSucceeded -eq $true) 'R195 write succeeded missing.'
Assert-True ($clean.marketDataRequestResponseReadAttempted -eq $true) 'R195 response read attempted missing.'
Assert-True ($clean.marketDataRequestReachedBoundedResponseClassification -eq $true) 'R195 bounded classification missing.'
Assert-True ($clean.stateFieldsConsistent -eq $true) 'R195 state fields not clean.'
Assert-True ($clean.tcpTlsFixSucceeded -eq $true) 'R195 TCP/TLS/FIX success not confirmed.'
Assert-True ($clean.fixAcknowledgement -eq 'FixLogonAcknowledged') 'R195 FIX acknowledgement missing.'

Assert-True ($mdReqIdConfirm.refMsgTypeMarketDataRequestConfirmed -eq $true) 'RefMsgType MarketDataRequest confirmation missing.'
Assert-True ($mdReqIdConfirm.refMsgTypeSanitizedCategory -eq 'RefMsgType_MarketDataRequest') 'RefMsgType category mismatch.'
Assert-True ($mdReqIdConfirm.refTagIdMdReqId262Confirmed -eq $true) 'RefTagID MDReqID/262 confirmation missing.'
Assert-True ($mdReqIdConfirm.refTagIdSanitizedCategory -eq 'RefTagID_MDReqID_262') 'RefTagID category mismatch.'
Assert-True ($mdReqIdConfirm.rawRefTagIdSerialized -eq $false) 'Raw RefTagID serialized.'

Assert-True ($reason.sessionRejectReasonValueIncorrectConfirmed -eq $true) 'SessionRejectReason ValueIncorrect confirmation missing.'
Assert-True ($reason.sessionRejectReasonSanitizedCategory -eq 'SessionRejectReason_ValueIncorrect') 'SessionRejectReason category mismatch.'
Assert-True ($reason.rawSessionRejectReasonSerialized -eq $false) 'Raw SessionRejectReason serialized.'

Assert-True ($shape.rawMdReqIdSerialized -eq $false) 'Raw MDReqID serialized.'
Assert-True ($shape.currentGeneratorReviewed -like '*CommonBody*') 'MDReqID generator review missing.'
Assert-True ($shape.currentProfilePrefixSourceReviewed -like '*RequestIdPrefix*') 'MDReqID prefix review missing.'
Assert-True ($shape.currentSanitizedShape.prefixFamily -eq 'LMAX_READONLY_R158') 'MDReqID shape prefix family mismatch.'
Assert-True ($shape.currentSanitizedShape.suffixFamily -eq 'GuidN') 'MDReqID suffix family mismatch.'
Assert-True ($shape.currentSanitizedShape.containsStaleR158LabelInLaterRetries -eq $true) 'Stale R158 label risk missing.'
Assert-True ($shape.currentSanitizedShape.containsUnderscoreSeparators -eq $true) 'Underscore separator risk missing.'
Assert-True ($shape.currentSanitizedShape.containsPunctuation -eq $true) 'Punctuation risk missing.'
Assert-True ($shape.currentSanitizedShape.containsWhitespace -eq $false) 'Whitespace risk should be false.'
Assert-True ($shape.currentSanitizedShape.containsNonAscii -eq $false) 'Non-ASCII risk should be false.'
Assert-True ($shape.currentSanitizedShape.estimatedLength -eq 51) 'MDReqID estimated length mismatch.'
Assert-True ($shape.currentSanitizedShape.uniquePerRequest -eq $true) 'MDReqID uniqueness not confirmed.'
Assert-True ($shape.riskAssessment.excessiveLengthPlausible -eq $true) 'Excessive length risk missing.'
Assert-True ($shape.riskAssessment.unsupportedCharacterOrSeparatorPlausible -eq $true) 'Unsupported separator risk missing.'
Assert-True ($shape.riskAssessment.stalePhaseLabelPlausible -eq $true) 'Stale phase label risk missing.'
Assert-True ($shape.riskAssessment.duplicatePatternPlausible -eq $false) 'Duplicate pattern should be demoted.'
Assert-True ($shape.recommendedShape.short -eq $true) 'Short MDReqID recommendation missing.'
Assert-True ($shape.recommendedShape.alphanumericOnly -eq $true) 'Alphanumeric MDReqID recommendation missing.'
Assert-True ($shape.recommendedShape.uniquePerRequest -eq $true) 'Unique MDReqID recommendation missing.'
Assert-True ($shape.recommendedShape.noPhaseLabel -eq $true) 'No phase label recommendation missing.'
Assert-True ($shape.recommendedShape.noUnderscore -eq $true) 'No underscore recommendation missing.'
Assert-True ($shape.recommendedShape.targetMaxLength -le 16) 'Target max length too large.'

Assert-True ($repair.mdReqIdRepairRecommended -eq $true) 'MDReqID repair decision missing.'
Assert-True ($repair.repairShouldBeNoExternal -eq $true) 'MDReqID repair must be no-external.'
Assert-True ($repair.liveRetryBeforeRepairAllowed -eq $false) 'Live retry allowed before repair.'
Assert-ContainsAll $repair.repairScope @(
    'MDReqID generator/value shape',
    'sanitized shape evidence',
    'tests for length, charset, uniqueness, and no raw MDReqID serialization',
    'readiness gate before any future retry') 'MDReqID repair scope incomplete.'

Assert-True ($secondary.fieldOrderRepeatingGroupDemoted -eq $true) 'Field-order candidate not demoted.'
Assert-True ($secondary.securityIdMappingDemoted -eq $true) 'SecurityID mapping candidate not demoted.'
Assert-True ($secondary.permissionEntitlementDemoted -eq $true) 'Permission candidate not demoted.'
Assert-True ($secondary.supportPackageDemoted -eq $true) 'Support package candidate not demoted.'

Assert-True ($nextAction.decision -eq 'MDReqIDValueRepairRecommended') 'Next action decision missing.'
Assert-True ($nextAction.nextPhase -eq 'LMAX-R197') 'Next phase should be R197.'
Assert-True ($nextAction.nextPhaseMustRemainNoExternal -eq $true) 'R197 must remain no-external.'
Assert-True ($nextAction.liveRetryAllowedNow -eq $false) 'R196 allows live retry now.'
Assert-True ($nextAction.blindLiveRetryBlocked -eq $true) 'Blind retry not blocked.'
Assert-True ($nextAction.requiresMdReqIdRepairBeforeFutureRetry -eq $true) 'MDReqID repair not required before retry.'
Assert-True ($next.liveRetryBeforeMdReqIdRepairAllowed -eq $false) 'Next recommendation allows retry before repair.'
Assert-True ($next.doNotRecommendBlindLiveRetry -eq $true) 'Next recommendation allows blind retry.'

Assert-True ($universe.approvedUniversePreserved -eq $true) 'Approved universe not preserved.'
Assert-ContainsAll $universe.approvedUniverse @('GBPUSD', 'EURGBP', 'AUDUSD', 'USDJPY') 'Approved universe weakened.'
Assert-True ($usdJpy.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat not preserved.'
Assert-True ($usdJpy.securityId -eq '4004') 'USDJPY SecurityID caveat weakened.'
Assert-True ($usdJpy.securityIdSource -eq '8') 'USDJPY SecurityIDSource caveat weakened.'

Assert-True ($forbidden.externalActivationAttempted -eq $false) 'R196 external activation detected.'
Assert-True ($forbidden.socketOpened -eq $false) 'R196 socket/TCP action detected.'
Assert-True ($forbidden.tlsOpened -eq $false) 'R196 TLS action detected.'
Assert-True ($forbidden.fixSessionOpened -eq $false) 'R196 FIX action detected.'
Assert-True ($forbidden.liveMarketDataRequestSent -eq $false) 'R196 live MarketDataRequest detected.'
Assert-True ($forbidden.liveMarketDataResponseRead -eq $false) 'R196 live MarketDataResponse detected.'
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
Assert-True ($sanitization.rawMdReqIdSerialized -eq $false) 'Raw MDReqID serialization risk detected.'
Assert-True ($sanitization.rawCredentialsSerialized -eq $false) 'Raw credential serialization risk detected.'
Assert-True ($sanitization.rawSessionIdsSerialized -eq $false) 'Raw session ID serialization risk detected.'
Assert-True ($sanitization.rawCompIdsSerialized -eq $false) 'Raw CompID serialization risk detected.'
Assert-True ($sanitization.rawEndpointValuesSerialized -eq $false) 'Raw endpoint serialization risk detected.'
Assert-True ($sanitization.rawTlsMaterialSerialized -eq $false) 'Raw TLS serialization risk detected.'
Assert-True ($sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned=false missing.'

$artifactText = Get-ChildItem -LiteralPath $ArtifactRoot -Filter 'phase-lmax-r196-*' -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combinedArtifactText = [string]::Join("`n", $artifactText)
foreach ($forbiddenToken in @('35=V', '35=Y', '35=3', '262=', '281=', '371=', '372=', '373=', '58=', [string][char]1, 'password=', 'username=', 'SenderCompId', 'TargetCompId')) {
    Assert-True (-not $combinedArtifactText.Contains($forbiddenToken)) "Forbidden serialized token detected in R196 artifacts: $forbiddenToken"
}

$gatePath = Join-Path $ArtifactRoot 'phase-lmax-r196-gate-validation.json'
Assert-True (Test-Path -LiteralPath $gatePath) 'R196 build/validator evidence missing.'
$gate = Get-Content -LiteralPath $gatePath -Raw | ConvertFrom-Json
Assert-True ($gate.build -like 'PASS*') 'Build evidence missing.'
Assert-True ($gate.validator -eq 'LMAX_R196_VALIDATION_PASS') 'Validator evidence missing.'

'LMAX_R196_VALIDATION_PASS'
