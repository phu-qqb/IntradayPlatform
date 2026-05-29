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

$review = Read-Json 'phase-lmax-r192-enhanced-reject-reason-review.json'
$clean = Read-Json 'phase-lmax-r192-r191-clean-state-confirmation.json'
$extraction = Read-Json 'phase-lmax-r192-r191-enhanced-extraction-confirmation.json'
$msgType3 = Read-Json 'phase-lmax-r192-sessionreject-msgtype3-analysis.json'
$detail = Read-Json 'phase-lmax-r192-ref-tagid-sessionrejectreason-detail-review.json'
$fieldOrder = Read-Json 'phase-lmax-r192-field-order-repeating-group-decision.json'
$securityId = Read-Json 'phase-lmax-r192-securityid-mapping-decision.json'
$support = Read-Json 'phase-lmax-r192-permission-support-decision.json'
$nextAction = Read-Json 'phase-lmax-r192-next-action-decision-gate.json'
$sanitization = Read-Json 'phase-lmax-r192-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r192-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r192-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r192-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r192-gate-validation.json'
$r191 = Read-Json 'phase-lmax-r191-enhanced-reject-reason-repaired-activation.json'

Assert-True ($review.classification -in @(
    'LMAX_R192_PASS_ENHANCED_REJECT_REVIEW_SESSION_REJECT_TAG_DETAIL_ENRICHMENT_RECOMMENDED_NO_EXTERNAL',
    'LMAX_R192_PASS_ENHANCED_REJECT_REVIEW_FIELD_ORDER_REPAIR_RECOMMENDED_NO_EXTERNAL',
    'LMAX_R192_PASS_ENHANCED_REJECT_REVIEW_SUPPORT_PACKAGE_RECOMMENDED_NO_EXTERNAL',
    'LMAX_R192_PASS_ENHANCED_REJECT_REVIEW_SECURITYID_MAPPING_AUDIT_RECOMMENDED_NO_EXTERNAL',
    'LMAX_R192_PASS_ENHANCED_REJECT_REVIEW_INCONCLUSIVE_SAFE_NO_EXTERNAL',
    'LMAX_R192_FAIL_NEW_EXTERNAL_ACTION_DETECTED',
    'LMAX_R192_FAIL_R191_EVIDENCE_MISSING',
    'LMAX_R192_FAIL_CLEAN_STATE_CONFIRMATION_MISSING',
    'LMAX_R192_FAIL_ENHANCED_EXTRACTION_CONFIRMATION_MISSING',
    'LMAX_R192_FAIL_SESSION_REJECT_ANALYSIS_MISSING',
    'LMAX_R192_FAIL_REF_TAG_OR_SESSION_REASON_REVIEW_MISSING',
    'LMAX_R192_FAIL_NEXT_ACTION_DECISION_MISSING',
    'LMAX_R192_FAIL_LIVE_RETRY_NOT_BLOCKED',
    'LMAX_R192_FAIL_RAW_FIX_OR_REJECT_LEAK_RISK',
    'LMAX_R192_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK',
    'LMAX_R192_FAIL_USDJPY_CAVEAT_WEAKENED',
    'LMAX_R192_FAIL_FORBIDDEN_ACTION_RISK',
    'LMAX_R192_FAIL_API_WORKER_GATEWAY_REGRESSION',
    'LMAX_R192_FAIL_BUILD_OR_VALIDATOR')) 'R192 classification is not allowed.'
Assert-True ($review.classification -eq 'LMAX_R192_PASS_ENHANCED_REJECT_REVIEW_SESSION_REJECT_TAG_DETAIL_ENRICHMENT_RECOMMENDED_NO_EXTERNAL') 'R192 decision classification mismatch.'
Assert-True ($review.noExternal -eq $true) 'R192 must be no-external.'

Assert-True ($r191.attemptCount -eq 1) 'R191 evidence missing or attemptCount invalid.'
Assert-True ($r191.classification -eq 'LMAX_R191_FAIL_SESSIONREJECT_WITH_SANITIZED_REASON_REPORTED') 'R191 classification mismatch.'
Assert-True ($review.r191EvidenceReviewed -eq $true) 'R191 evidence review missing.'

Assert-True ($clean.cleanStateConfirmed -eq $true) 'R191 clean state confirmation missing.'
Assert-True ($clean.attemptCount -eq 1) 'R191 clean state attemptCount invalid.'
Assert-True ($clean.marketDataRequestWriteAttempted -eq $true) 'R191 write attempted not confirmed.'
Assert-True ($clean.marketDataRequestWriteSucceeded -eq $true) 'R191 write succeeded not confirmed.'
Assert-True ($clean.marketDataRequestResponseReadAttempted -eq $true) 'R191 response read not confirmed.'
Assert-True ($clean.marketDataRequestReachedBoundedResponseClassification -eq $true) 'R191 bounded classification not confirmed.'
Assert-True ($clean.stateFieldsConsistent -eq $true) 'R191 state fields not clean.'

Assert-True ($extraction.enhancedExtractionConfirmed -eq $true) 'R191 enhanced extraction confirmation missing.'
Assert-True ($extraction.marketDataResponseCategory -eq 'SessionRejectObservedWithSanitizedReason') 'Enhanced extraction response category mismatch.'
Assert-True ($extraction.sessionRejectSanitizedReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'Enhanced extraction broad category mismatch.'
Assert-True ($extraction.sanitizedSessionRejectReasonCategory -eq 'MalformedOrUnsupportedMarketDataRequestPlausible') 'Enhanced extraction sanitized category mismatch.'
Assert-True ($extraction.marketDataRejectSanitizedSubcategory -eq 'RejectReasonNotAvailable') 'MarketData reject subcategory mismatch.'
Assert-True ($extraction.sessionRejectSanitizedSubcategory -eq 'SessionRejectRefMsgTypeMarketDataRequest') 'Session reject subcategory mismatch.'
Assert-True ($extraction.rejectReasonExtractionSource -eq 'MsgType3Tags371372373') 'Reject extraction source mismatch.'
Assert-True ($extraction.enhancedRejectExtractionMissing -eq $false) 'Enhanced extraction missing.'

Assert-True ($msgType3.msgType3SessionRejectConfirmed -eq $true) 'MsgType=3 session reject analysis missing.'
Assert-True ($msgType3.msgTypeYMarketDataRequestRejectConfirmed -eq $false) 'MsgType=Y should not be confirmed.'
Assert-True ($msgType3.marketDataRequestReject281Available -eq $false) 'Tag 281 should not be available.'
Assert-True ($msgType3.sessionRejectReferencesMarketDataRequest -eq $true) 'Session reject does not reference MarketDataRequest.'
Assert-True ($msgType3.refMsgTypeMarketDataRequestConfirmed -eq $true) 'RefMsgType=V not confirmed.'

Assert-True ($detail.refMsgTypeDetail -eq 'RefMsgTypeMarketDataRequestConfirmed') 'RefMsgType detail review missing.'
Assert-True ($detail.refTagIdPresentSafelySurfaced -eq $false) 'RefTagID should not be considered safely surfaced yet.'
Assert-True ($detail.refTagIdMappedToKnownMarketDataRequestTagCategory -eq $false) 'RefTagID should not be mapped yet.'
Assert-True ($detail.sessionRejectReasonPresentSafelySurfaced -eq $false) 'SessionRejectReason should not be considered safely surfaced yet.'
Assert-True ($detail.sessionRejectReasonMappedToKnownSessionRejectReasonCategory -eq $false) 'SessionRejectReason should not be mapped yet.'
Assert-True ($detail.detailSufficientForTargetedFieldOrderRepair -eq $false) 'Detail is not sufficient for field-order repair.'

Assert-True ($fieldOrder.fieldOrderRepairRecommendedNow -eq $false) 'Field-order repair should be deferred.'
Assert-True ($securityId.securityIdMappingAuditRecommendedNow -eq $false) 'SecurityID mapping audit should be deferred.'
Assert-True ($securityId.unknownSymbolStyleEvidenceObserved -eq $false) 'Unknown-symbol evidence should not be present.'
Assert-True ($support.supportPackageRecommendedNow -eq $false) 'Support package should be deferred pending tag detail enrichment.'
Assert-True ($support.permissionEntitlementStillPlausible -eq $true) 'Permission/support plausibility should remain.'

Assert-True ($nextAction.decision -eq 'SessionRejectTagDetailEnrichmentRecommended') 'Next action decision missing.'
Assert-True ($nextAction.liveRetryAllowedNow -eq $false) 'R192 next action allows live retry.'
Assert-True ($nextAction.blindLiveRetryBlocked -eq $true) 'Blind live retry not blocked.'
Assert-True ($nextAction.requiresNoExternalRepairOrSupportDocsBackedChangeBeforeFutureRetry -eq $true) 'Future retry not gated on repair/support change.'
Assert-True ($next.mustRemainNoExternal -eq $true) 'R193 must remain no-external.'
Assert-True ($next.doNotRecommendBlindLiveRetry -eq $true) 'Next recommendation allows blind live retry.'

Assert-True ($forbidden.externalActivationAttempted -eq $false) 'R192 external activation detected.'
Assert-True ($forbidden.socketOpened -eq $false) 'R192 socket/TCP action detected.'
Assert-True ($forbidden.tlsOpened -eq $false) 'R192 TLS action detected.'
Assert-True ($forbidden.fixSessionOpened -eq $false) 'R192 FIX action detected.'
Assert-True ($forbidden.liveMarketDataRequestSent -eq $false) 'R192 live MarketDataRequest detected.'
Assert-True ($forbidden.liveMarketDataResponseRead -eq $false) 'R192 live MarketDataResponse detected.'
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

$artifactText = Get-ChildItem -LiteralPath $ArtifactRoot -Filter 'phase-lmax-r192-*' -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combinedArtifactText = [string]::Join("`n", $artifactText)
foreach ($forbiddenToken in @('35=V', '35=Y', '35=3', '281=', '371=', '372=', '373=', '58=', [string][char]1, 'password=', 'username=', 'SenderCompId', 'TargetCompId')) {
    Assert-True (-not $combinedArtifactText.Contains($forbiddenToken)) "Forbidden serialized token detected in R192 artifacts: $forbiddenToken"
}

Assert-True ($gate.build -like 'PASS*') 'Build evidence missing.'
Assert-True ($gate.validator -eq 'LMAX_R192_VALIDATION_PASS') 'Validator evidence missing.'

'LMAX_R192_VALIDATION_PASS'
