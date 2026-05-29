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

Assert-True (Test-Path -LiteralPath (Join-Path $ArtifactRoot 'phase-lmax-r198-mdreqid-repaired-retry-readiness-summary.md')) 'R198 summary missing.'

$readiness = Read-Json 'phase-lmax-r198-mdreqid-repaired-retry-readiness.json'
$selected = Read-Json 'phase-lmax-r198-selected-profile-evidence.json'
$mdReq = Read-Json 'phase-lmax-r198-mdreqid-repaired-shape-readiness.json'
$mdReqSan = Read-Json 'phase-lmax-r198-mdreqid-sanitization-readiness.json'
$reject = Read-Json 'phase-lmax-r198-enhanced-reject-reporting-readiness.json'
$state = Read-Json 'phase-lmax-r198-final-state-evidence-contract-readiness.json'
$nonSelected = Read-Json 'phase-lmax-r198-non-selected-profiles-evidence.json'
$reservation = Read-Json 'phase-lmax-r198-next-activation-phase-reservation-decision.json'
$preflight = Read-Json 'phase-lmax-r198-r199-preflight-checklist.json'
$expected = Read-Json 'phase-lmax-r198-r199-expected-evidence-contract.json'
$reporting = Read-Json 'phase-lmax-r198-reporting-contract-readiness.json'
$universe = Read-Json 'phase-lmax-r198-approved-universe-preservation.json'
$usdJpy = Read-Json 'phase-lmax-r198-usdjpy-caveat-preservation.json'
$sanitization = Read-Json 'phase-lmax-r198-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r198-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r198-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r198-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r198-gate-validation.json'

$r195 = Read-Json 'phase-lmax-r195-sessionreject-tag-detail-activation.json'
$r196 = Read-Json 'phase-lmax-r196-sessionreject-tag-detail-review.json'
$r197 = Read-Json 'phase-lmax-r197-mdreqid-value-repair.json'
$r197Shape = Read-Json 'phase-lmax-r197-mdreqid-repaired-shape-contract.json'
$r197Gate = Read-Json 'phase-lmax-r197-gate-validation.json'

Assert-True ($readiness.classification -in @(
    'LMAX_R198_PASS_MDREQID_REPAIRED_RETRY_READINESS_NO_EXTERNAL',
    'LMAX_R198_FAIL_NEW_EXTERNAL_ACTION_DETECTED',
    'LMAX_R198_FAIL_NEXT_ACTIVATION_PHASE_RESERVATION_INVALID',
    'LMAX_R198_FAIL_PROFILE_SELECTION_MISSING',
    'LMAX_R198_FAIL_MDREQID_REPAIR_READINESS_MISSING',
    'LMAX_R198_FAIL_MDREQID_SHAPE_CONTRACT_WEAKENED',
    'LMAX_R198_FAIL_RAW_MDREQID_LEAK_RISK',
    'LMAX_R198_FAIL_MDUPDATETYPE_PROFILE_CONTRACT_WEAKENED',
    'LMAX_R198_FAIL_REJECT_DETAIL_REPORTING_READINESS_MISSING',
    'LMAX_R198_FAIL_FINAL_STATE_EVIDENCE_CONTRACT_MISSING',
    'LMAX_R198_FAIL_APPROVAL_OR_MARKET_HOURS_CONSTRAINT_MISSING',
    'LMAX_R198_FAIL_USDJPY_CAVEAT_WEAKENED',
    'LMAX_R198_FAIL_RAW_FIX_OR_REJECT_LEAK_RISK',
    'LMAX_R198_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK',
    'LMAX_R198_FAIL_FORBIDDEN_ACTION_RISK',
    'LMAX_R198_FAIL_API_WORKER_GATEWAY_REGRESSION',
    'LMAX_R198_FAIL_BUILD_OR_TESTS')) 'R198 classification is not allowed.'
Assert-True ($readiness.classification -eq 'LMAX_R198_PASS_MDREQID_REPAIRED_RETRY_READINESS_NO_EXTERNAL') 'R198 classification mismatch.'
Assert-True ($readiness.noExternal -eq $true) 'R198 must be no-external.'

Assert-True ($r195.classification -eq 'LMAX_R195_FAIL_SESSIONREJECT_WITH_TAG_DETAIL_REPORTED') 'R195 evidence missing.'
Assert-True ($r195.attemptCount -eq 1) 'R195 attempt count invalid.'
Assert-True ($r196.classification -eq 'LMAX_R196_PASS_SESSIONREJECT_TAG_DETAIL_REVIEW_MDREQID_REPAIR_RECOMMENDED_NO_EXTERNAL') 'R196 evidence missing.'
Assert-True ($r197.classification -eq 'LMAX_R197_PASS_MDREQID_VALUE_REPAIR_READY_NO_EXTERNAL') 'R197 evidence missing.'
Assert-True ($r197Gate.validator -eq 'LMAX_R197_VALIDATION_PASS') 'R197 validator evidence missing.'
Assert-True ($r197Shape.contractReady -eq $true) 'R197 MDReqID shape contract missing.'

Assert-True ($reservation.reservedNextRealActivationPhase -eq 'LMAX-R199') 'Next activation phase must be LMAX-R199.'
Assert-True ($reservation.reservedNextRealActivationPhaseNumber -eq 199) 'Next activation phase number invalid.'
Assert-True ($reservation.oddNumbered -eq $true) 'Next activation phase must be odd-numbered.'
Assert-True ($reservation.explicitlyReserved -eq $true) 'Next activation phase not explicitly reserved.'
Assert-True ($reservation.evenNumberedActivationAllowed -eq $false) 'Even-numbered activation allowed.'
Assert-True ($readiness.nextActivationPhase -eq 'LMAX-R199') 'Readiness next activation phase mismatch.'
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

Assert-True ($mdReq.r197RepairEvidencePresent -eq $true) 'R197 repair evidence not carried into R198.'
Assert-True ($mdReq.mdReqIdRepairReadinessReady -eq $true) 'MDReqID repair readiness missing.'
Assert-True ($mdReq.appliesToFutureProfile -eq $profile) 'MDReqID readiness applies to wrong profile.'
Assert-True ($mdReq.maxLength -le 16) 'Future MDReqID shape is longer than 16.'
Assert-True ($mdReq.actualGeneratedLength -le 16) 'Generated MDReqID length is longer than 16.'
Assert-True ($mdReq.short -eq $true) 'Short MDReqID readiness missing.'
Assert-True ($mdReq.alphanumericOnly -eq $true) 'Future MDReqID shape contains non-alphanumeric characters.'
Assert-True ($mdReq.asciiOnly -eq $true) 'Future MDReqID shape is not ASCII-only.'
Assert-True ($mdReq.uniquePerRequest -eq $true) 'MDReqID uniqueness readiness missing.'
Assert-True ($mdReq.sessionSafe -eq $true) 'Session-safe MDReqID readiness missing.'
Assert-True ($mdReq.containsUnderscore -eq $false) 'Future MDReqID shape contains underscores.'
Assert-True ($mdReq.containsPunctuation -eq $false) 'Future MDReqID shape contains punctuation.'
Assert-True ($mdReq.phaseLabelIncluded -eq $false) 'Future MDReqID shape contains phase labels.'
Assert-True ($mdReq.forbiddenPhaseLabelsPresent -eq $false) 'Future MDReqID shape contains forbidden phase label family.'
Assert-True ($mdReq.rawMdReqIdSerialized -eq $false) 'Raw MDReqID serialized in readiness evidence.'
Assert-True ($mdReqSan.mdReqIdSanitizationReady -eq $true) 'MDReqID sanitization readiness missing.'
Assert-True ($mdReqSan.rawMdReqIdSerializationAllowed -eq $false) 'Raw MDReqID serialization allowed.'
Assert-True ($mdReqSan.rawMdReqIdSerialized -eq $false) 'Raw MDReqID serialization risk present.'
Assert-True ($mdReqSan.futureR199ArtifactsMustNotSerializeRawMdReqId -eq $true) 'Future R199 raw MDReqID block missing.'

Assert-True ($expected.requiredProfileContract.mdReqIdMaxLength -le 16) 'Expected MDReqID max length too long.'
Assert-True ($expected.requiredProfileContract.mdReqIdAlphanumericOnly -eq $true) 'Expected MDReqID alphanumeric contract missing.'
Assert-True ($expected.requiredProfileContract.mdReqIdNoPhaseLabel -eq $true) 'Expected MDReqID no phase label contract missing.'
Assert-True ($expected.requiredProfileContract.mdReqIdNoUnderscore -eq $true) 'Expected MDReqID no underscore contract missing.'
Assert-True ($expected.requiredProfileContract.mdReqIdNoPunctuation -eq $true) 'Expected MDReqID no punctuation contract missing.'
Assert-True ($expected.requiredProfileContract.rawMdReqIdSerialized -eq $false) 'Expected contract allows raw MDReqID serialization.'

Assert-True ($state.finalStateEvidenceContractReady -eq $true) 'Final-state evidence readiness missing.'
Assert-True ($state.classifiedMarketDataResponseCannotEmitAllExplicitStateFieldsFalse -eq $true) 'Classified response can emit all explicit state fields false.'
Assert-ContainsAll $state.requiredExplicitStateFields @(
    'marketDataRequestWriteAttempted',
    'marketDataRequestWriteSucceeded',
    'marketDataRequestResponseReadAttempted',
    'marketDataRequestReachedBoundedResponseClassification',
    'marketDataRequestSentLegacyFlag') 'Explicit state field contract incomplete.'
Assert-True ($expected.classifiedMarketDataResponseWithAllExplicitStateFieldsFalseAllowed -eq $false) 'Expected contract allows false state contradiction.'

Assert-True ($reject.enhancedSessionRejectTagDetailReportingReady -eq $true) 'Enhanced SessionReject tag-detail readiness missing.'
Assert-ContainsAll $reject.requiredFields @(
    'sessionRejectRefTagIdSanitizedCategory',
    'sessionRejectReasonSanitizedCategory',
    'sessionRejectRefMsgTypeSanitizedCategory',
    'rejectReasonExtractionSource') 'Enhanced SessionReject detail fields missing.'
Assert-True ($reject.broadSanitizedReasonReportingReady -eq $true) 'Broad sanitized reason readiness missing.'
Assert-ContainsAll $reject.broadRequiredFields @(
    'sessionRejectSanitizedReasonCategory',
    'sanitizedSessionRejectReasonCategory') 'Broad reject reporting fields missing.'
Assert-True ($reject.notAvailableDistinctFromPropagationFailure -eq $true) 'NotAvailable distinction missing.'

Assert-True ($preflight.freshExactOperatorApprovalRequired -eq $true) 'Fresh approval template missing.'
Assert-True ($preflight.concreteWeekdayMarketHoursConfirmationRequired -eq $true) 'Concrete weekday market-hours requirement missing.'
Assert-True ($preflight.placeholderTimeAllowed -eq $false) 'Placeholder time allowed.'
Assert-True ($preflight.exactlyOneBoundedAttemptRequired -eq $true) 'Exactly-one-bounded-attempt constraint missing.'
Assert-True ($preflight.rawMdReqIdSerializationAllowed -eq $false) 'Preflight allows raw MDReqID serialization.'
Assert-True ($preflight.rawFixOrRejectSerializationAllowed -eq $false) 'Preflight allows raw FIX/reject serialization.'
Assert-True ($preflight.ordersTradingSchedulerPollingReplayAllowed -eq $false) 'Preflight allows forbidden actions.'

Assert-True ($reporting.reportingContractReady -eq $true) 'Reporting contract readiness missing.'
Assert-True ($reporting.rawMdReqIdSerialized -eq $false) 'Reporting raw MDReqID serialization risk present.'
Assert-True ($reporting.finalStateEvidenceContractReady -eq $true) 'Reporting final-state contract missing.'
Assert-True ($reporting.enhancedSessionRejectDetailReportingReady -eq $true) 'Reporting SessionReject detail readiness missing.'
Assert-True ($reporting.broadSanitizedReasonReportingReady -eq $true) 'Reporting broad reason readiness missing.'

Assert-True ($universe.approvedUniversePreserved -eq $true) 'Approved universe not preserved.'
Assert-ContainsAll $universe.approvedUniverse @('GBPUSD', 'EURGBP', 'AUDUSD', 'USDJPY') 'Approved universe weakened.'
Assert-True ($usdJpy.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat not preserved.'
Assert-True ($usdJpy.securityId -eq '4004') 'USDJPY SecurityID caveat weakened.'
Assert-True ($usdJpy.securityIdSource -eq '8') 'USDJPY SecurityIDSource caveat weakened.'

Assert-True ($forbidden.externalActivationAttempted -eq $false) 'R198 external activation detected.'
Assert-True ($forbidden.socketOpened -eq $false) 'R198 socket/TCP action detected.'
Assert-True ($forbidden.tlsOpened -eq $false) 'R198 TLS action detected.'
Assert-True ($forbidden.fixSessionOpened -eq $false) 'R198 FIX action detected.'
Assert-True ($forbidden.liveMarketDataRequestSent -eq $false) 'R198 live MarketDataRequest detected.'
Assert-True ($forbidden.liveMarketDataResponseRead -eq $false) 'R198 live MarketDataResponse detected.'
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

$artifactText = Get-ChildItem -LiteralPath $ArtifactRoot -Filter 'phase-lmax-r198-*' -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combinedArtifactText = [string]::Join("`n", $artifactText)
foreach ($forbiddenToken in @('35=V', '35=Y', '35=3', '262=', '281=', '371=', '372=', '373=', '58=', [string][char]1, 'password=', 'username=', 'SenderCompId', 'TargetCompId')) {
    Assert-True (-not $combinedArtifactText.Contains($forbiddenToken)) "Forbidden serialized token detected in R198 artifacts: $forbiddenToken"
}
Assert-True (-not $combinedArtifactText.Contains('LMAX_READONLY_')) 'Legacy raw MDReqID prefix family leaked into R198 artifacts.'

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R199') 'Next phase recommendation missing.'
Assert-True ($next.r199RequiresFreshExactOperatorApproval -eq $true) 'R199 fresh approval requirement missing.'
Assert-True ($next.r199RequiresSeparateConcreteWeekdayMarketHoursConfirmation -eq $true) 'R199 market-hours confirmation requirement missing.'
Assert-True ($next.r199RequiresExactlyOneBoundedAttemptOnly -eq $true) 'R199 exactly-one attempt requirement missing.'
Assert-True ($next.r199MustStopAfterAttempt -eq $true) 'R199 stop-after-attempt requirement missing.'
Assert-True ($next.r200MustBeNoExternalReviewGateOnly -eq $true) 'R200 no-external review requirement missing.'
Assert-True ($next.liveRetryAllowedInR198 -eq $false) 'R198 allows live retry.'

Assert-True ($gate.build -like 'PASS*') 'Build evidence missing.'
Assert-True ($gate.focusedTests -like 'PASS*') 'Focused test evidence missing.'
Assert-True ($gate.unitTests -like 'PASS*') 'Unit test evidence missing.'
Assert-True ($gate.integrationTests -like 'PASS*') 'Integration test evidence missing.'
Assert-True ($gate.validator -eq 'LMAX_R198_VALIDATION_PASS') 'Validator evidence missing.'

'LMAX_R198_VALIDATION_PASS'
