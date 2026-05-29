$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'
$writerPath = Join-Path $root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualFixLogonFrameWriter.cs'
$apiPath = Join-Path $root 'src/QQ.Production.Intraday.Api/Program.cs'
$workerPath = Join-Path $root 'src/QQ.Production.Intraday.Worker/Program.cs'

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

$summary = Read-Json 'phase-lmax-r114-fix-logon-username-password-tag-binding-summary.json'
$r113 = Read-Json 'phase-lmax-r113-fix-logon-support-input-summary.json'
$beforeAfter = Read-Json 'phase-lmax-r114-r113-requirements-before-after-classification.json'
$binding = Read-Json 'phase-lmax-r114-username-password-tag-binding-validation.json'
$credential = Read-Json 'phase-lmax-r114-in-memory-credential-source-validation.json'
$sanitize = Read-Json 'phase-lmax-r114-raw-secret-sanitization-validation.json'
$fields = Read-Json 'phase-lmax-r114-logon-frame-field-presence-validation.json'
$reset = Read-Json 'phase-lmax-r114-reset-sequence-policy-validation.json'
$sessionOnly = Read-Json 'phase-lmax-r114-session-logon-only-safety-validation.json'
$orders = Read-Json 'phase-lmax-r114-order-message-exclusion-validation.json'
$marketData = Read-Json 'phase-lmax-r114-marketdata-block-until-logon-success-validation.json'
$production = Read-Json 'phase-lmax-r114-production-config-exclusion-validation.json'
$pathValidation = Read-Json 'phase-lmax-r114-real-bounded-path-validation.json'
$noExternal = Read-Json 'phase-lmax-r114-no-external-boundary-attempted.json'
$forbidden = Read-Json 'phase-lmax-r114-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r114-api-worker-fake-gateway-audit.json'
$usdjpy = Read-Json 'phase-lmax-r114-usdjpy-caveat-preservation.json'
$next = Read-Json 'phase-lmax-r114-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r114-gate-validation.json'

Assert-True ($r113.classification -eq 'LMAX_R113_PASS_FIX_LOGON_SUPPORT_INPUT_REVIEW_READY_FOR_PARAMETER_BINDING_NO_EXTERNAL_ACTIVATION') 'R113 success evidence is missing or mismatched.'
Assert-True ($r113.usernameTagRequired -eq $true -and $beforeAfter.before.usernameTagRequired -eq $true) 'Tag 553 requirement is not represented.'
Assert-True ($r113.passwordTagRequired -eq $true -and $beforeAfter.before.passwordTagRequired -eq $true) 'Tag 554 requirement is not represented.'
Assert-True ($summary.classification -eq 'LMAX_R114_PASS_FIX_LOGON_USERNAME_PASSWORD_TAG_BINDING_READY_NO_EXTERNAL_ACTIVATION') 'Unexpected R114 classification.'
Assert-True ($summary.usernameTag553BindingReady -eq $true -and $binding.usernameTagBoundFromApprovedInMemoryCredentialMaterial -eq $true) 'Username tag binding is not provable.'
Assert-True ($summary.passwordTag554BindingReady -eq $true -and $binding.passwordTagBoundFromApprovedInMemoryCredentialMaterial -eq $true) 'Password tag binding is not provable.'
Assert-True ($credential.valuesSourcedFromApprovedInMemoryDemoReadOnlyCredentialMaterial -eq $true) 'Credential values are not proven to come from approved in-memory Demo/read-only material.'
Assert-True ($sanitize.usernameValueSerialized -eq $false -and $sanitize.passwordValueSerialized -eq $false) 'Username/password values were serialized.'
Assert-True ($summary.credentialValuesReturned -eq $false -and $sanitize.credentialValuesReturned -eq $false) 'credentialValuesReturned must remain false.'
Assert-True ($sanitize.rawFixFrameSerialized -eq $false -and $sanitize.rawFixMessagesPrintedStoredSerialized -eq $false) 'Raw FIX frame/messages were serialized.'
Assert-True ($fields.usernameTagPresent -eq $true -and $fields.passwordTagPresent -eq $true) 'Username/password tag field presence missing.'
Assert-True ($fields.resetSeqNumFlagYPresent -eq $true -and $reset.resetSeqNumFlagYPresent -eq $true) 'ResetSeqNumFlag=Y policy regressed.'
Assert-True ($sessionOnly.writerBuilderSessionLogonOnly -eq $true) 'Writer/builder must remain session/logon-only.'
Assert-True ($sessionOnly.orderFramesSupported -eq $false -and $sessionOnly.newOrderSingleSupported -eq $false -and $sessionOnly.cancelReplaceSupported -eq $false) 'Order-capable FIX writer was introduced.'
Assert-True ($sessionOnly.executionReportParsingSupported -eq $false -and $sessionOnly.fillsSupported -eq $false -and $sessionOnly.orderLifecycleSupported -eq $false) 'Execution/fill/order lifecycle parsing was introduced.'
Assert-True ($orders.orderMessagesSupported -eq $false -and $orders.tradingPathIntroduced -eq $false) 'Order/trading path was introduced.'
Assert-True ($marketData.marketDataRequestBlockedUntilLogonAck -eq $true -and $marketData.marketDataRequestAllowedWithoutLogonAck -eq $false) 'MarketDataRequest can be attempted without FIX Logon acknowledgement.'
Assert-True ($production.productionAccountAllowed -eq $false -and $production.productionConfigAllowed -eq $false) 'Production account/config was allowed.'
Assert-True ($pathValidation.noExternalDefaultPreserved -eq $true -and $pathValidation.apiWorkerReachable -eq $false) 'No-external/default or path validation regressed.'
Assert-True ($noExternal.activationAttempted -eq $false -and $noExternal.tcpAttempted -eq $false -and $noExternal.tlsAttempted -eq $false -and $noExternal.fixLogonAttempted -eq $false -and $noExternal.marketDataRequestAttempted -eq $false) 'R114 attempted an external boundary.'
Assert-True ($forbidden.result -eq 'PASS') 'Forbidden actions audit failed.'
Assert-True ($apiWorker.result -eq 'PASS' -and $apiWorker.apiWorkerFakeLmaxGatewayOnly -eq $true -and $apiWorker.liveGatewayDefaultIntroduced -eq $false) 'API/Worker FakeLmaxGatewayOnly audit failed.'
Assert-True ($usdjpy.caveatPreserved -eq $true) 'USDJPY caveat is missing or weakened.'
Assert-True ($next.nextRecommendedPhase -eq 'Phase LMAX-R115 - Operator-Approved Single Temporary QQ Workspace Demo Read-Only Activation Retry After FIX Username/Password Tag Binding Fix') 'Next phase recommendation is missing or wrong.'
Assert-True ($gate.buildResult -like 'PASS*') 'Build evidence is missing.'
Assert-True ($gate.testResult -like 'PASS*') 'Test evidence is missing.'

$writerSource = Get-Content -LiteralPath $writerPath -Raw
Assert-True ($writerSource -match 'Field\("553", credentialTags\.Username\)') 'Builder does not bind username tag from in-memory material.'
Assert-True ($writerSource -match 'Field\("554", credentialTags\.Password\)') 'Builder does not bind password tag from in-memory material.'
Assert-True ($writerSource -match 'LMAX_DEMO_FIX_USERNAME' -and $writerSource -match 'LMAX_DEMO_FIX_PASSWORD') 'Builder does not use approved Demo/read-only credential labels.'
Assert-True ($writerSource -notmatch 'NewOrderSingleSupported: true') 'NewOrderSingle support introduced.'
Assert-True ($writerSource -notmatch 'CancelReplaceSupported: true') 'Cancel/replace support introduced.'

$apiWorkerSource = (Get-Content -LiteralPath $apiPath -Raw) + "`n" + (Get-Content -LiteralPath $workerPath -Raw)
Assert-True ($apiWorkerSource -notmatch 'LmaxReadOnlyActivationManualFixLogonFrameBuilder') 'Manual builder is reachable from API/Worker startup.'
Assert-True ($apiWorkerSource -match 'FakeLmaxGateway') 'API/Worker FakeLmaxGatewayOnly evidence missing.'

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r114-*' -File |
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
    'r114-synthetic-user',
    'r114-synthetic-pass',
    'DemoReadOnlySenderCompId',
    'DemoReadOnlyTargetCompId',
    'lmax.com',
    'fix-marketdata'
)

foreach ($pattern in $forbiddenPatterns) {
    Assert-True ($joined.IndexOf($pattern, [StringComparison]::OrdinalIgnoreCase) -lt 0) "Sensitive or raw protocol material found in R114 artifacts: $pattern"
}

Write-Host 'LMAX_R114_VALIDATION_PASS'
