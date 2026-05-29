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

Assert-True (Test-Path -LiteralPath (Join-Path $ArtifactRoot 'phase-lmax-r197-mdreqid-value-repair-summary.md')) 'R197 summary missing.'

$repair = Read-Json 'phase-lmax-r197-mdreqid-value-repair.json'
$shape = Read-Json 'phase-lmax-r197-mdreqid-repaired-shape-contract.json'
$unique = Read-Json 'phase-lmax-r197-mdreqid-uniqueness-evidence.json'
$mdReqSan = Read-Json 'phase-lmax-r197-mdreqid-sanitization-evidence.json'
$profile = Read-Json 'phase-lmax-r197-profile-preservation-evidence.json'
$reject = Read-Json 'phase-lmax-r197-enhanced-reject-reporting-preservation.json'
$state = Read-Json 'phase-lmax-r197-final-state-evidence-contract-preservation.json'
$universe = Read-Json 'phase-lmax-r197-approved-universe-preservation.json'
$usdJpy = Read-Json 'phase-lmax-r197-usdjpy-caveat-preservation.json'
$sanitization = Read-Json 'phase-lmax-r197-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r197-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r197-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r197-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r197-gate-validation.json'
$r196 = Read-Json 'phase-lmax-r196-sessionreject-tag-detail-review.json'
$r196Decision = Read-Json 'phase-lmax-r196-mdreqid-repair-decision.json'

Assert-True ($repair.classification -in @(
    'LMAX_R197_PASS_MDREQID_VALUE_REPAIR_READY_NO_EXTERNAL',
    'LMAX_R197_FAIL_NEW_EXTERNAL_ACTION_DETECTED',
    'LMAX_R197_FAIL_MDREQID_REPAIR_MISSING',
    'LMAX_R197_FAIL_MDREQID_SHAPE_CONTRACT_WEAKENED',
    'LMAX_R197_FAIL_MDREQID_UNIQUENESS_MISSING',
    'LMAX_R197_FAIL_RAW_MDREQID_LEAK_RISK',
    'LMAX_R197_FAIL_PROFILE_CONTRACT_WEAKENED',
    'LMAX_R197_FAIL_REJECT_DETAIL_REPORTING_WEAKENED',
    'LMAX_R197_FAIL_FINAL_STATE_EVIDENCE_CONTRACT_WEAKENED',
    'LMAX_R197_FAIL_USDJPY_CAVEAT_WEAKENED',
    'LMAX_R197_FAIL_RAW_FIX_OR_REJECT_LEAK_RISK',
    'LMAX_R197_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK',
    'LMAX_R197_FAIL_FORBIDDEN_ACTION_RISK',
    'LMAX_R197_FAIL_API_WORKER_GATEWAY_REGRESSION',
    'LMAX_R197_FAIL_BUILD_OR_TESTS')) 'R197 classification is not allowed.'
Assert-True ($repair.classification -eq 'LMAX_R197_PASS_MDREQID_VALUE_REPAIR_READY_NO_EXTERNAL') 'R197 classification mismatch.'
Assert-True ($repair.noExternal -eq $true) 'R197 must be no-external.'
Assert-True ($repair.repairImplemented -eq $true) 'MDReqID repair implementation evidence missing.'

Assert-True ($r196.classification -eq 'LMAX_R196_PASS_SESSIONREJECT_TAG_DETAIL_REVIEW_MDREQID_REPAIR_RECOMMENDED_NO_EXTERNAL') 'R196 evidence missing.'
Assert-True ($r196Decision.mdReqIdRepairRecommended -eq $true) 'R196 MDReqID repair decision missing.'

Assert-True ($shape.contractReady -eq $true) 'Repaired MDReqID shape contract missing.'
Assert-True ($shape.appliesToProfile -eq 'UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument') 'MDReqID contract applies to wrong profile.'
Assert-True ($shape.maxLength -le 16) 'Future MDReqID shape is longer than 16.'
Assert-True ($shape.actualGeneratedLength -le 16) 'Generated MDReqID length is longer than 16.'
Assert-True ($shape.alphanumericOnly -eq $true) 'Future MDReqID shape contains non-alphanumeric characters.'
Assert-True ($shape.asciiOnly -eq $true) 'Future MDReqID shape is not ASCII-only.'
Assert-True ($shape.containsUnderscore -eq $false) 'Future MDReqID shape contains underscores.'
Assert-True ($shape.containsPunctuation -eq $false) 'Future MDReqID shape contains punctuation.'
Assert-True ($shape.containsWhitespace -eq $false) 'Future MDReqID shape contains whitespace.'
Assert-True ($shape.phaseLabelIncluded -eq $false) 'Future MDReqID shape contains a phase label.'
Assert-True ($shape.forbiddenPhaseLabelsPresent -eq $false) 'Future MDReqID shape contains forbidden phase label family.'
Assert-True ($shape.uniquePerRequest -eq $true) 'MDReqID uniqueness not preserved.'
Assert-True ($shape.exactValueSerialized -eq $false) 'Raw MDReqID value serialized in shape evidence.'
Assert-True ($shape.rawMdReqIdSerialized -eq $false) 'Raw MDReqID serialized in shape evidence.'

Assert-True ($unique.uniquenessEvidencePresent -eq $true) 'Uniqueness evidence missing.'
Assert-True ($unique.uniquenessPreserved -eq $true) 'MDReqID uniqueness missing.'
Assert-True ($unique.sampleCountTested -ge 20) 'Uniqueness sample too small.'
Assert-True ($unique.distinctCountObserved -eq $unique.sampleCountTested) 'Duplicate MDReqID observed in uniqueness evidence.'
Assert-True ($unique.duplicateObserved -eq $false) 'Duplicate MDReqID observed.'
Assert-True ($unique.rawMdReqIdsSerialized -eq $false) 'Raw MDReqID values serialized in uniqueness evidence.'

Assert-True ($profile.selectedProfile -eq 'UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument') 'Selected profile changed unexpectedly.'
Assert-True ($profile.selectedProfileChanged -eq $false) 'Selected profile changed unexpectedly.'
Assert-True ($profile.gbpusdOnly -eq $true) 'GBPUSD-only contract weakened.'
Assert-True ($profile.singleRequest -eq $true) 'Single-request contract weakened.'
Assert-True ($profile.securityId -eq '4002') 'SecurityID=4002 weakened.'
Assert-True ($profile.securityIdSource -eq '8') 'SecurityIDSource=8 weakened.'
Assert-True ($profile.subscriptionRequestType -eq '1') 'SubscriptionRequestType=1 weakened.'
Assert-True ($profile.mdUpdateType -eq '0') 'MDUpdateType=0 weakened.'
Assert-True ($profile.marketDepth -eq '1') 'MarketDepth=1 weakened.'
Assert-True ($profile.noMdEntryTypes -eq 2) 'NoMDEntryTypes=2 weakened.'
Assert-True ($profile.bidAndOfferTogether -eq $true) 'Bid+offer together weakened.'
Assert-True ($profile.symbolTextIncluded -eq $false) 'Symbol text appears.'
Assert-True ($profile.internalSymbolIncluded -eq $false) 'InternalSymbol appears.'
Assert-True ($profile.snapshotOnlyIncluded -eq $false) 'SnapshotOnly appears.'
Assert-True ($profile.subscriptionRequestTypeZeroIncluded -eq $false) 'SubscriptionRequestType=0 appears.'

Assert-True ($reject.enhancedSessionRejectTagDetailExtractionPreserved -eq $true) 'Enhanced SessionReject tag detail extraction weakened.'
Assert-ContainsAll $reject.requiredFieldsPreserved @(
    'sessionRejectRefTagIdSanitizedCategory',
    'sessionRejectReasonSanitizedCategory',
    'sessionRejectRefMsgTypeSanitizedCategory',
    'rejectReasonExtractionSource') 'Enhanced reject reporting fields missing.'
Assert-True ($reject.broadSanitizedReasonReportingPreserved -eq $true) 'Broad reject reporting weakened.'
Assert-True ($reject.notAvailableDistinctFromPropagationFailure -eq $true) 'NotAvailable distinction weakened.'

Assert-True ($state.finalStateEvidenceContractPreserved -eq $true) 'Final-state evidence contract weakened.'
Assert-True ($state.classifiedMarketDataResponseCannotEmitAllExplicitStateFieldsFalse -eq $true) 'Final-state contradiction rule weakened.'
Assert-ContainsAll $state.requiredExplicitStateFields @(
    'marketDataRequestWriteAttempted',
    'marketDataRequestWriteSucceeded',
    'marketDataRequestResponseReadAttempted',
    'marketDataRequestReachedBoundedResponseClassification',
    'marketDataRequestSentLegacyFlag') 'Explicit state fields missing.'

Assert-True ($universe.approvedUniversePreserved -eq $true) 'Approved universe weakened.'
Assert-ContainsAll $universe.approvedUniverse @('GBPUSD', 'EURGBP', 'AUDUSD', 'USDJPY') 'Approved universe missing instruments.'
Assert-True ($usdJpy.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat weakened.'
Assert-True ($usdJpy.securityId -eq '4004') 'USDJPY SecurityID caveat weakened.'
Assert-True ($usdJpy.securityIdSource -eq '8') 'USDJPY SecurityIDSource caveat weakened.'

Assert-True ($forbidden.externalActivationAttempted -eq $false) 'R197 external activation detected.'
Assert-True ($forbidden.socketOpened -eq $false) 'R197 socket/TCP action detected.'
Assert-True ($forbidden.tlsOpened -eq $false) 'R197 TLS action detected.'
Assert-True ($forbidden.fixSessionOpened -eq $false) 'R197 FIX action detected.'
Assert-True ($forbidden.liveMarketDataRequestSent -eq $false) 'R197 live MarketDataRequest detected.'
Assert-True ($forbidden.liveMarketDataResponseRead -eq $false) 'R197 live MarketDataResponse detected.'
Assert-True ($forbidden.ordersIntroduced -eq $false) 'Order path introduced.'
Assert-True ($forbidden.tradingEnabled -eq $false) 'Trading enabled.'
Assert-True ($forbidden.schedulerStarted -eq $false) 'Scheduler started.'
Assert-True ($forbidden.pollingStarted -eq $false) 'Polling started.'
Assert-True ($forbidden.serviceStarted -eq $false) 'Service started.'
Assert-True ($forbidden.replayIntroduced -eq $false) 'Replay introduced.'
Assert-True ($forbidden.shadowReplayIntroduced -eq $false) 'Shadow replay introduced.'

Assert-True ($apiWorker.fakeLmaxGatewayOnlyPreserved -eq $true) 'API/Worker FakeLmaxGatewayOnly regressed.'
Assert-True ($apiWorker.liveGatewayIntroducedInApiOrWorker -eq $false) 'API/Worker live gateway introduced.'

foreach ($audit in @($sanitization, $mdReqSan)) {
    Assert-True ($audit.rawFixMessagesSerialized -eq $false) 'Raw FIX serialization risk detected.'
    Assert-True ($audit.rawRejectTextSerialized -eq $false) 'Raw reject text serialization risk detected.'
    Assert-True ($audit.rawMdReqIdSerialized -eq $false) 'Raw MDReqID serialization risk detected.'
    Assert-True ($audit.rawCredentialsSerialized -eq $false -or $audit.credentialsSerialized -eq $false) 'Raw credential serialization risk detected.'
    Assert-True ($audit.rawSessionIdsSerialized -eq $false -or $audit.sessionIdsSerialized -eq $false) 'Raw session ID serialization risk detected.'
    Assert-True ($audit.rawCompIdsSerialized -eq $false -or $audit.compIdsSerialized -eq $false) 'Raw CompID serialization risk detected.'
    Assert-True ($audit.rawEndpointValuesSerialized -eq $false -or $audit.endpointValuesSerialized -eq $false) 'Raw endpoint serialization risk detected.'
    Assert-True ($audit.rawTlsMaterialSerialized -eq $false -or $audit.tlsMaterialSerialized -eq $false) 'Raw TLS serialization risk detected.'
}
Assert-True ($sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned=false missing.'

$operationPath = Join-Path $Root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualMarketDataRequestOperation.cs'
$operation = Get-Content -LiteralPath $operationPath -Raw
Assert-True ($operation.Contains('BuildMdReqId')) 'MDReqID repair missing from operation code.'
Assert-True ($operation.Contains('[..14]')) 'Short MDReqID fragment selection missing from operation code.'
Assert-True ($operation.Contains('ToUpperInvariant')) 'Uppercase alphanumeric MDReqID shape missing from operation code.'

$testPath = Join-Path $Root 'tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlyActivationManualMarketDataRequestOperationTests.cs'
$tests = Get-Content -LiteralPath $testPath -Raw
Assert-True ($tests.Contains('R197_mdreqid_value_repair_preserves_short_alphanumeric_unique_shape_no_externally')) 'R197 MDReqID uniqueness/shape test missing.'
Assert-True ($tests.Contains('AssertRepairedMdReqIdShape')) 'R197 MDReqID shape assertions missing.'
Assert-True ($tests.Contains('JsonSerializer.Serialize(frame)')) 'Raw MDReqID serialization test missing.'

$artifactText = Get-ChildItem -LiteralPath $ArtifactRoot -Filter 'phase-lmax-r197-*' -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combinedArtifactText = [string]::Join("`n", $artifactText)
foreach ($forbiddenToken in @('35=V', '35=Y', '35=3', '262=', '281=', '371=', '372=', '373=', '58=', [string][char]1, 'password=', 'username=', 'SenderCompId', 'TargetCompId')) {
    Assert-True (-not $combinedArtifactText.Contains($forbiddenToken)) "Forbidden serialized token detected in R197 artifacts: $forbiddenToken"
}
Assert-True (-not $combinedArtifactText.Contains('LMAX_READONLY_')) 'Legacy raw MDReqID prefix family leaked into R197 artifacts.'

Assert-True ($next.recommendedNextPhase -eq 'LMAX-R198') 'Next phase recommendation missing.'
Assert-True ($next.r198MustRemainNoExternal -eq $true) 'R198 no-external requirement missing.'
Assert-True ($next.r198ShouldPrepareButNotExecuteRetry -eq $true) 'R198 prepare-only requirement missing.'
Assert-True ($next.liveRetryAllowedNow -eq $false) 'R197 allows immediate live retry.'

Assert-True ($gate.build -like 'PASS*') 'Build evidence missing.'
Assert-True ($gate.focusedTests -like 'PASS*') 'Focused test evidence missing.'
Assert-True ($gate.unitTests -like 'PASS*') 'Unit test evidence missing.'
Assert-True ($gate.integrationTests -like 'PASS*') 'Integration test evidence missing.'
Assert-True ($gate.validator -eq 'LMAX_R197_VALIDATION_PASS') 'Validator evidence missing.'

'LMAX_R197_VALIDATION_PASS'
