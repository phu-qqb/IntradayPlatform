$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'

function Read-Json($name) {
    $path = Join-Path $artifactRoot $name
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing artifact: $name"
    }

    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

function Assert-True($condition, $message) {
    if (-not $condition) {
        throw $message
    }
}

$summary = Read-Json 'phase-lmax-r113-fix-logon-support-input-summary.json'
$r112 = Read-Json 'phase-lmax-r112-fix-logon-session-parameter-support-summary.json'
$carryforward = Read-Json 'phase-lmax-r113-r112-requirements-carryforward-review.json'
$availability = Read-Json 'phase-lmax-r113-support-input-availability.json'
$requirements = Read-Json 'phase-lmax-r113-lmax-marketdata-logon-requirements-review.json'
$confirmed = Read-Json 'phase-lmax-r113-confirmed-parameter-categories.json'
$unknown = Read-Json 'phase-lmax-r113-unknown-parameter-categories.json'
$usernamePassword = Read-Json 'phase-lmax-r113-username-password-tag-requirement-review.json'
$sequence = Read-Json 'phase-lmax-r113-sequence-reset-policy-review.json'
$marketDataGate = Read-Json 'phase-lmax-r113-marketdata-logon-before-request-gate-review.json'
$binding = Read-Json 'phase-lmax-r113-parameter-binding-readiness-decision.json'
$noExternal = Read-Json 'phase-lmax-r113-no-external-boundary-attempted.json'
$forbidden = Read-Json 'phase-lmax-r113-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r113-api-worker-fake-gateway-audit.json'
$sanitize = Read-Json 'phase-lmax-r113-credential-fix-sanitization-validation.json'
$usdjpy = Read-Json 'phase-lmax-r113-usdjpy-caveat-preservation.json'
$next = Read-Json 'phase-lmax-r113-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r113-gate-validation.json'

$requiredArtifacts = @(
    'phase-lmax-r113-fix-logon-support-input-review-report.md',
    'phase-lmax-r113-confirmed-parameter-categories.json',
    'phase-lmax-r113-unknown-parameter-categories.json'
)

foreach ($artifact in $requiredArtifacts) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactRoot $artifact)) "Missing artifact: $artifact"
}

Assert-True ($r112.classification -eq 'LMAX_R112_PASS_FIX_LOGON_SESSION_PARAMETER_SUPPORT_PACK_READY_NO_EXTERNAL_ACTIVATION') 'R112 success evidence is missing or mismatched.'
Assert-True ($carryforward.r112SuccessEvidenceMatched -eq $true) 'R112 carryforward review is missing.'
Assert-True ($summary.classification -eq 'LMAX_R113_PASS_FIX_LOGON_SUPPORT_INPUT_REVIEW_READY_FOR_PARAMETER_BINDING_NO_EXTERNAL_ACTIVATION') 'Unexpected R113 classification.'
Assert-True ($availability.supportConfigInputAvailable -eq $true -and $availability.marketDataPackagePresent -eq $true) 'Market Data API support input is not reviewed.'
Assert-True ($availability.marketDataHtmlPresent -eq $true -and $availability.marketDataQuickFixDictionaryPresent -eq $true) 'Market Data docs/dictionary availability missing.'
Assert-True ($availability.tradingApiUsedForOrderEnablement -eq $false -and $forbidden.tradingApiUsedForOrderEnablement -eq $false) 'Trading API was used to justify order/trading enablement.'
Assert-True ($requirements.marketDataApiDocumentationReviewed -eq $true) 'Market Data API documentation review missing.'
Assert-True ($requirements.beginStringFix44 -eq $true -and $summary.beginStringFix44Confirmed -eq $true) 'FIX.4.4 BeginString not confirmed.'
Assert-True ($requirements.encryptMethodRequired -eq $true -and $requirements.encryptMethodNoneCategoryRequired -eq $true) 'EncryptMethod requirement not confirmed.'
Assert-True ($requirements.heartBtIntRequired -eq $true) 'HeartBtInt requirement not confirmed.'
Assert-True ($requirements.resetSeqNumFlagYRequiredForMarketData -eq $true -and $sequence.resetSeqNumFlagYRequiredForMarketData -eq $true) 'ResetSeqNumFlag=Y market-data policy is not reviewed.'
Assert-True ($usernamePassword.usernameTagRequired -eq $true -and $summary.usernameTagRequired -eq $true) 'Tag 553 requirement is not represented.'
Assert-True ($usernamePassword.passwordTagRequired -eq $true -and $summary.passwordTagRequired -eq $true) 'Tag 554 requirement is not represented.'
Assert-True ($confirmed.confirmedParameterCategories.Username.confirmed -eq $true -and $confirmed.confirmedParameterCategories.Password.confirmed -eq $true) 'Username/password confirmed categories missing.'
Assert-True ($unknown.unsupportedTagsInvented -eq $false -and $summary.unsupportedTagsInvented -eq $false) 'Unsupported tags were invented as facts.'
Assert-True ($marketDataGate.marketDataRequestBlockedUntilLogonAck -eq $true -and $marketDataGate.marketDataRequestAllowedWithoutFixLogonAck -eq $false) 'MarketDataRequest can be attempted without FIX Logon acknowledgement.'
Assert-True ($binding.readyToBindUsernamePasswordTagsInMemoryOnly -eq $true -and $binding.readyToBindRawValuesIntoArtifacts -eq $false) 'Parameter binding readiness decision is missing or unsafe.'
Assert-True ($noExternal.activationAttempted -eq $false -and $noExternal.tcpAttempted -eq $false -and $noExternal.tlsAttempted -eq $false -and $noExternal.fixLogonAttempted -eq $false -and $noExternal.marketDataRequestAttempted -eq $false) 'R113 attempted an external boundary.'
Assert-True ($forbidden.result -eq 'PASS') 'Forbidden actions audit failed.'
Assert-True ($forbidden.ordersSubmitted -eq $false -and $forbidden.tradingStateMutated -eq $false -and $forbidden.newOrderSingleIntroduced -eq $false) 'Order/trading path was introduced.'
Assert-True ($apiWorker.result -eq 'PASS' -and $apiWorker.apiWorkerFakeLmaxGatewayOnly -eq $true -and $apiWorker.liveGatewayDefaultIntroduced -eq $false) 'API/Worker FakeLmaxGatewayOnly audit failed.'
Assert-True ($sanitize.credentialValuesReturned -eq $false -and $summary.credentialValuesReturned -eq $false) 'credentialValuesReturned must remain false.'
Assert-True ($sanitize.usernameValueSerialized -eq $false -and $sanitize.passwordValueSerialized -eq $false) 'Username/password values were serialized.'
Assert-True ($sanitize.rawCredentialsPrintedStoredSerialized -eq $false -and $sanitize.rawFixMessagesPrintedStoredSerialized -eq $false -and $sanitize.rawSensitiveFixLogsPrintedStoredSerialized -eq $false) 'Raw credential or FIX material was serialized.'
Assert-True ($usdjpy.caveatPreserved -eq $true) 'USDJPY caveat is missing or weakened.'
Assert-True ($next.nextRecommendedPhase -eq 'Phase LMAX-R114 - Targeted FIX Logon Username/Password Tag Binding Fix') 'Next phase recommendation is missing or wrong.'
Assert-True ($gate.buildResult -like 'PASS*') 'Build evidence is missing.'
Assert-True ($gate.testResult -like 'PASS*') 'Test evidence is missing.'

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r113-*' -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$joined = $artifactText -join "`n"
$forbiddenPatterns = @(
    'password=',
    'username=',
    'session token',
    'BEGIN CERTIFICATE',
    'PRIVATE KEY',
    '35=A',
    '35=D',
    '35=F',
    '35=G',
    '35=8',
    '553=',
    '554=',
    'DemoReadOnlySenderCompId',
    'DemoReadOnlyTargetCompId',
    'lmax.com',
    'fix-marketdata',
    'raw Logout text:'
)

foreach ($pattern in $forbiddenPatterns) {
    Assert-True ($joined.IndexOf($pattern, [StringComparison]::OrdinalIgnoreCase) -lt 0) "Sensitive or raw protocol material found in R113 artifacts: $pattern"
}

Write-Host 'LMAX_R113_VALIDATION_PASS'
