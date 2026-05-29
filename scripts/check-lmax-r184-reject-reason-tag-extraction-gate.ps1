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
    param([string]$Path)

    Assert-True (Test-Path -LiteralPath $Path) "Missing artifact: $Path"
    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

$artifactRoot = Join-Path $Root 'artifacts/readiness/lmax-runtime-enablement'
$parserPath = Join-Path $Root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualMarketDataRequestOperation.cs'
$testsPath = Join-Path $Root 'tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlyActivationManualMarketDataRequestOperationTests.cs'
$gatePath = Join-Path $artifactRoot 'phase-lmax-r184-gate-validation.json'

$summary = Read-Json (Join-Path $artifactRoot 'phase-lmax-r184-reject-reason-tag-extraction.json')
$mdReject = Read-Json (Join-Path $artifactRoot 'phase-lmax-r184-marketdatarequestreject-281-contract.json')
$sessionReject = Read-Json (Join-Path $artifactRoot 'phase-lmax-r184-sessionreject-371-372-373-contract.json')
$subcategory = Read-Json (Join-Path $artifactRoot 'phase-lmax-r184-sanitized-reject-subcategory-contract.json')
$fixture = Read-Json (Join-Path $artifactRoot 'phase-lmax-r184-fixture-test-evidence.json')
$rehydration = Read-Json (Join-Path $artifactRoot 'phase-lmax-r184-r181-rehydration-decision.json')
$sanitization = Read-Json (Join-Path $artifactRoot 'phase-lmax-r184-sanitization-audit.json')
$forbidden = Read-Json (Join-Path $artifactRoot 'phase-lmax-r184-forbidden-actions-audit.json')
$apiWorker = Read-Json (Join-Path $artifactRoot 'phase-lmax-r184-api-worker-fake-gateway-audit.json')
$next = Read-Json (Join-Path $artifactRoot 'phase-lmax-r184-next-phase-recommendation.json')
$gate = Read-Json $gatePath

Assert-True ($summary.classification -in @(
    'LMAX_R184_PASS_REJECT_REASON_TAG_EXTRACTION_READY_NO_EXTERNAL',
    'LMAX_R184_PASS_REJECT_REASON_EXTRACTION_SUPPORT_PACKAGE_STILL_REQUIRED_NO_EXTERNAL')) 'R184 classification missing or invalid.'
Assert-True ($summary.noExternal -eq $true) 'R184 no-external confirmation missing.'
Assert-True ($forbidden.externalActivationAttempted -eq $false) 'R184 external activation attempt detected.'
Assert-True ($forbidden.socketOpened -eq $false) 'R184 socket action detected.'
Assert-True ($forbidden.tlsOpened -eq $false) 'R184 TLS action detected.'
Assert-True ($forbidden.fixSessionOpened -eq $false) 'R184 FIX action detected.'
Assert-True ($forbidden.liveMarketDataRequestSent -eq $false) 'R184 live MarketDataRequest detected.'
Assert-True ($forbidden.liveMarketDataResponseRead -eq $false) 'R184 live MarketDataResponse read detected.'

$parser = Get-Content -LiteralPath $parserPath -Raw
$tests = Get-Content -LiteralPath $testsPath -Raw

foreach ($token in @(
    'AllowedSanitizedRejectSubcategories',
    'SanitizedRejectSubcategory',
    'MarketDataRequestRejectUnknownSymbol',
    'MarketDataRequestRejectDuplicateMDReqID',
    'MarketDataRequestRejectUnsupportedSubscriptionRequestType',
    'MarketDataRequestRejectUnsupportedMarketDepth',
    'MarketDataRequestRejectUnsupportedMDUpdateType',
    'MarketDataRequestRejectUnsupportedMDEntryType',
    'MarketDataRequestRejectReasonOtherSanitized',
    'SessionRejectRefTagIdPresentSanitized',
    'SessionRejectRefMsgTypeMarketDataRequest',
    'SessionRejectReasonPresentSanitized',
    'RejectReasonNotAvailable')) {
    Assert-True ($parser.Contains($token)) "Parser missing sanitized reject subcategory token: $token"
}

Assert-True ($parser.Contains('"Y" => MarketDataRequestReject(fields)')) 'MsgType Y handling is not routed through MarketDataRequestReject.'
Assert-True ($parser.Contains('TryGetValue("281"')) 'Tag 281 extraction missing.'
Assert-True ($parser.Contains('TryGetValue("372"')) 'Tag 372 extraction missing.'
Assert-True ($parser.Contains('ContainsKey("371")')) 'Tag 371 extraction missing.'
Assert-True ($parser.Contains('ContainsKey("373")')) 'Tag 373 extraction missing.'

Assert-True ($mdReject.msgType -eq 'Y') 'MarketDataRequestReject MsgType Y contract missing.'
Assert-True ($mdReject.categories.'6' -eq 'MarketDataRequestRejectUnsupportedMDUpdateType') 'MDUpdateType 281 category missing.'
Assert-True ($sessionReject.msgType -eq '3') 'SessionReject MsgType 3 contract missing.'
Assert-True ($sessionReject.categories.refMsgTypeMarketDataRequest -eq 'SessionRejectRefMsgTypeMarketDataRequest') 'SessionReject RefMsgType V category missing.'
Assert-True ($subcategory.allowedSubcategories.Count -ge 11) 'Sanitized reject subcategory allowlist incomplete.'
Assert-True ($fixture.fixtureTestsAdded -eq $true) 'Sanitized fixture tests evidence missing.'
Assert-True ($tests.Contains('Marketdata_request_reject_281_is_sanitized_to_doc_backed_subcategories_without_raw_fix')) 'Tag 281 fixture test missing.'
Assert-True ($tests.Contains('Sessionreject_371_372_373_tags_are_sanitized_to_subcategories_without_raw_values')) 'SessionReject tag fixture test missing.'
Assert-True ($rehydration.r181ExactRejectReasonInferred -eq $false) 'R181 exact reject reason was improperly inferred.'

Assert-True ($sanitization.rawFixMessagesSerialized -eq $false) 'Raw FIX serialization risk detected.'
Assert-True ($sanitization.rawRejectTextSerialized -eq $false) 'Raw reject text serialization risk detected.'
Assert-True ($sanitization.rawCredentialsSerialized -eq $false) 'Raw credential serialization risk detected.'
Assert-True ($sanitization.rawSessionIdsSerialized -eq $false) 'Raw session id serialization risk detected.'
Assert-True ($sanitization.rawCompIdsSerialized -eq $false) 'Raw CompID serialization risk detected.'
Assert-True ($sanitization.rawEndpointValuesSerialized -eq $false) 'Raw endpoint serialization risk detected.'
Assert-True ($sanitization.rawTlsMaterialSerialized -eq $false) 'Raw TLS serialization risk detected.'
Assert-True ($sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned=false missing.'
Assert-True ($apiWorker.fakeLmaxGatewayOnlyPreserved -eq $true) 'API/Worker FakeLmaxGatewayOnly regressed.'
Assert-True ($next.liveRetryBlockedUntilReadinessGateAndFreshApproval -eq $true) 'Next recommendation allows live retry without readiness/fresh approval.'

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r184-*' |
    Where-Object { -not $_.PSIsContainer } |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combinedArtifactText = [string]::Join("`n", $artifactText)
foreach ($forbiddenToken in @('35=V', '35=Y', '35=3', '281=', '371=', '372=', '373=', '58=', [string][char]1, 'password=', 'username=', 'SenderCompId', 'TargetCompId')) {
    Assert-True (-not $combinedArtifactText.Contains($forbiddenToken)) "Forbidden serialized token detected in R184 artifacts: $forbiddenToken"
}

Assert-True ($gate.build -like 'PASS*') 'Build evidence missing.'
Assert-True ($gate.focusedTests -like 'PASS*') 'Focused test evidence missing.'
Assert-True ($gate.unitTests -like 'PASS*') 'Unit test evidence missing.'
Assert-True ($gate.integrationTests -like 'PASS*') 'Integration test evidence missing.'
Assert-True ($gate.validator -eq 'LMAX_R184_VALIDATION_PASS') 'Validator evidence missing.'

'LMAX_R184_VALIDATION_PASS'
