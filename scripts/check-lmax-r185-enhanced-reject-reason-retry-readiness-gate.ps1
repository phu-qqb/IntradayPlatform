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

$readiness = Read-Json 'phase-lmax-r185-enhanced-reject-reason-retry-readiness.json'
$selected = Read-Json 'phase-lmax-r185-selected-profile-evidence.json'
$enhanced = Read-Json 'phase-lmax-r185-enhanced-reject-extraction-readiness.json'
$state = Read-Json 'phase-lmax-r185-final-state-evidence-contract-readiness.json'
$nonSelected = Read-Json 'phase-lmax-r185-non-selected-profiles-evidence.json'
$reservation = Read-Json 'phase-lmax-r185-next-activation-phase-reservation-decision.json'
$preflight = Read-Json 'phase-lmax-r185-r187-preflight-checklist.json'
$expected = Read-Json 'phase-lmax-r185-r187-expected-evidence-contract.json'
$reporting = Read-Json 'phase-lmax-r185-reporting-contract-readiness.json'
$approvedUniverse = Read-Json 'phase-lmax-r185-approved-universe-preservation.json'
$usdJpy = Read-Json 'phase-lmax-r185-usdjpy-caveat-preservation.json'
$sanitization = Read-Json 'phase-lmax-r185-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r185-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r185-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r185-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r185-gate-validation.json'

$r181 = Read-Json 'phase-lmax-r181-mdupdatetype-required-after-final-state-evidence-repair-activation.json'
$r181State = Read-Json 'phase-lmax-r181-state-propagation-evidence.json'
$r182 = Read-Json 'phase-lmx-r182-r181-clean-state-reject.json'
$r182Decision = Read-Json 'phase-lmx-r182-candidate-cause-decision.json'
$r184 = Read-Json 'phase-lmax-r184-reject-reason-tag-extraction.json'
$r184Subcategory = Read-Json 'phase-lmax-r184-sanitized-reject-subcategory-contract.json'

Assert-True ($readiness.classification -eq 'LMAX_R185_PASS_ENHANCED_REJECT_REASON_RETRY_READINESS_NO_EXTERNAL') 'R185 classification mismatch.'
Assert-True ($readiness.noExternal -eq $true) 'R185 no-external confirmation missing.'

Assert-True ($forbidden.externalActivationAttempted -eq $false) 'R185 external activation attempt detected.'
Assert-True ($forbidden.socketOpened -eq $false) 'R185 socket action detected.'
Assert-True ($forbidden.tlsOpened -eq $false) 'R185 TLS action detected.'
Assert-True ($forbidden.fixSessionOpened -eq $false) 'R185 FIX action detected.'
Assert-True ($forbidden.liveMarketDataRequestSent -eq $false) 'R185 live MarketDataRequest detected.'
Assert-True ($forbidden.liveMarketDataResponseRead -eq $false) 'R185 live MarketDataResponse read detected.'

Assert-True ($reservation.nextActivationPhase -eq 'LMAX-R187') 'Next activation phase mismatch.'
Assert-True ($reservation.oddNumbered -eq $true) 'Next activation phase must be odd-numbered.'
Assert-True ($reservation.explicitlyReserved -eq $true) 'Next activation phase not explicitly reserved.'

Assert-True ($selected.selectedFutureProfile -eq $profile) 'Selected future profile mismatch.'
Assert-True ($readiness.selectedFutureProfile -eq $profile) 'Readiness selected future profile mismatch.'
Assert-True ($expected.requiredSelectedProfile -eq $profile) 'Expected evidence selected profile mismatch.'
Assert-True ($selected.selectedFutureProfileCount -eq 1) 'Selected future profile count is not exactly one.'
Assert-True ($nonSelected.moreThanOneDiagnosticProfileSelected -eq $false) 'Multiple diagnostic profiles selected.'

Assert-True ($selected.gbpUsdOnly -eq $true) 'Future retry is not GBPUSD-only.'
Assert-True ($selected.singleRequest -eq $true) 'Future retry is not single-request.'
Assert-True ($selected.securityId -eq '4002') 'SecurityID 4002 missing.'
Assert-True ($selected.securityIdSource -eq '8') 'SecurityIDSource 8 missing.'
Assert-True ($selected.subscriptionRequestType -eq '1') 'SubscriptionRequestType=1 missing.'
Assert-True ($selected.mdUpdateType -eq '0') 'MDUpdateType=0 missing.'
Assert-True ($selected.marketDepth -eq 1) 'MarketDepth=1 missing.'
Assert-True ($selected.noMdEntryTypes -eq 2) 'NoMDEntryTypes=2 missing.'
Assert-True ($selected.bidAndOfferTogether -eq $true) 'Bid+offer together evidence missing.'
Assert-True ($selected.symbolTextPresent -eq $false) 'Symbol text must be absent.'
Assert-True ($selected.internalSymbolPresent -eq $false) 'InternalSymbol must be absent.'
Assert-True ($selected.snapshotOnlyPresent -eq $false) 'SnapshotOnly must be absent.'
Assert-True ($selected.subscriptionRequestTypeZeroPresent -eq $false) 'SubscriptionRequestType=0 must be absent.'

Assert-True ($state.finalStateEvidenceContractActive -eq $true) 'Final-state-evidence readiness missing.'
Assert-True ($state.classifiedMarketDataResponseCannotEmitAllExplicitStateFieldsFalse -eq $true) 'Classified-response consistency rule missing.'
Assert-True ($expected.classifiedResponseAllExplicitStateFieldsFalseAllowed -eq $false) 'Expected contract allows all explicit state fields false.'
foreach ($field in @(
    'marketDataRequestWriteAttempted',
    'marketDataRequestWriteSucceeded',
    'marketDataRequestResponseReadAttempted',
    'marketDataRequestReachedBoundedResponseClassification',
    'marketDataRequestSentLegacyFlag')) {
    Assert-True ($expected.requiredExplicitStateFields -contains $field) "Expected explicit state field missing: $field"
}

Assert-True ($enhanced.msgTypeYTag281ExtractionRequired -eq $true) 'MsgType Y/tag 281 extraction readiness missing.'
Assert-True ($enhanced.msgType3Tags371372373ExtractionRequired -eq $true) 'MsgType 3/tags 371/372/373 extraction readiness missing.'
Assert-True ($enhanced.broadMalformedOrUnsupportedCategoryPreserved -eq $true) 'Broad malformed/unsupported category preservation missing.'
foreach ($field in @('marketDataRejectSanitizedSubcategory', 'sessionRejectSanitizedSubcategory', 'rejectReasonExtractionSource')) {
    Assert-True ($expected.requiredRejectExtractionFields -contains $field) "Expected reject extraction field missing: $field"
    Assert-True ($enhanced.futureEvidenceFieldsRequired -contains $field) "Enhanced readiness field missing: $field"
}
Assert-True ($expected.msgTypeYTag281SanitizedSubcategoryRequired -eq $true) 'Expected contract omits MsgType Y/tag 281 sanitized subcategory.'
Assert-True ($expected.msgType3Tags371372373SanitizedSubcategoryRequired -eq $true) 'Expected contract omits MsgType 3/tags 371/372/373 sanitized subcategory.'
Assert-True ($r184.classification -eq 'LMAX_R184_PASS_REJECT_REASON_TAG_EXTRACTION_READY_NO_EXTERNAL') 'R184 evidence missing or invalid.'
Assert-True ($r184Subcategory.allowedSubcategories -contains 'MarketDataRequestRejectUnsupportedMDUpdateType') 'R184 tag 281 subcategory missing.'
Assert-True ($r184Subcategory.allowedSubcategories -contains 'SessionRejectRefMsgTypeMarketDataRequest') 'R184 SessionReject subcategory missing.'

Assert-True ($reporting.r133R135ReportingReadinessPreserved -eq $true) 'R133/R135 reporting readiness missing.'
Assert-True ($reporting.requiredReportingFields -contains 'sessionRejectSanitizedReasonCategory') 'sessionRejectSanitizedReasonCategory missing.'
Assert-True ($reporting.requiredReportingFields -contains 'sanitizedSessionRejectReasonCategory') 'sanitizedSessionRejectReasonCategory missing.'

Assert-True ($preflight.freshExactOperatorApprovalRequired -eq $true) 'Fresh approval template missing.'
Assert-True ($preflight.concreteWeekdayMarketHoursConfirmationRequired -eq $true) 'Concrete weekday market-hours requirement missing.'
Assert-True ($preflight.exactlyOneBoundedAttempt -eq $true) 'Exactly-one-bounded-attempt constraint missing.'
Assert-True ($reservation.stopAfterAttemptForR188ReviewGate -eq $true) 'Stop-after-attempt/R188 review gate requirement missing.'

Assert-True ($approvedUniverse.approvedUniversePreserved -eq $true) 'Approved universe preservation missing.'
Assert-True ($approvedUniverse.approvedInstruments.Count -eq 4) 'Approved universe count changed.'
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
Assert-True ($apiWorker.fakeLmaxGatewayOnlyPreserved -eq $true) 'API/Worker FakeLmaxGatewayOnly regressed.'
Assert-True ($next.r187MustRequireFreshExactOperatorApproval -eq $true) 'Next recommendation missing fresh approval requirement.'
Assert-True ($next.r187MustRequireSeparateConcreteWeekdayMarketHoursConfirmation -eq $true) 'Next recommendation missing market-hours requirement.'
Assert-True ($next.r187MustPerformExactlyOneBoundedAttemptOnly -eq $true) 'Next recommendation missing exactly-one-attempt requirement.'

Assert-True ($r181.attemptCount -eq 1) 'R181 attemptCount evidence missing.'
Assert-True ($r181.selectedProfile -eq $profile) 'R181 selected profile mismatch.'
Assert-True ($r181State.marketDataRequestWriteAttempted -eq $true) 'R181 clean write-attempted evidence missing.'
Assert-True ($r181State.marketDataRequestWriteSucceeded -eq $true) 'R181 clean write-succeeded evidence missing.'
Assert-True ($r181State.marketDataRequestResponseReadAttempted -eq $true) 'R181 clean response-read evidence missing.'
Assert-True ($r181State.marketDataRequestReachedBoundedResponseClassification -eq $true) 'R181 clean classification evidence missing.'
Assert-True ($r182.classification -like 'LMAX_R182_PASS*') 'R182 phase-lmx evidence missing.'
Assert-True ($r182Decision.candidateCauseDecisionPresent -eq $true) 'R182 candidate cause decision marker missing.'
Assert-True ($r182Decision.leadingCandidate -eq 'PermissionEntitlementOrExactLmaxSupportClarification') 'R182 decision evidence missing.'
Assert-True ($r182Decision.permissionEntitlementSupport.status -eq 'Leading') 'R182 permission/support leading decision missing.'

$artifactText = Get-ChildItem -LiteralPath $ArtifactRoot -Filter 'phase-lmax-r185-*' -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combinedArtifactText = [string]::Join("`n", $artifactText)
foreach ($forbiddenToken in @('35=V', '35=Y', '35=3', '281=', '371=', '372=', '373=', '58=', [string][char]1, 'password=', 'username=', 'SenderCompId', 'TargetCompId')) {
    Assert-True (-not $combinedArtifactText.Contains($forbiddenToken)) "Forbidden serialized token detected in R185 artifacts: $forbiddenToken"
}

Assert-True ($gate.build -like 'PASS*') 'Build evidence missing.'
Assert-True ($gate.focusedTests -like 'PASS*') 'Focused test evidence missing.'
Assert-True ($gate.unitTests -like 'PASS*') 'Unit test evidence missing.'
Assert-True ($gate.integrationTests -like 'PASS*') 'Integration test evidence missing.'
Assert-True ($gate.validator -eq 'LMAX_R185_VALIDATION_PASS') 'Validator evidence missing.'

'LMAX_R185_VALIDATION_PASS'
