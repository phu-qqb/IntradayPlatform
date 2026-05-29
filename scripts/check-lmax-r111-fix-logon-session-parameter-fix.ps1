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

$summary = Read-Json 'phase-lmax-r111-fix-logon-session-parameter-summary.json'
$r110 = Read-Json 'phase-lmax-r110-fix-session-boundary-root-cause-summary.json'
$field = Read-Json 'phase-lmax-r111-fix-logon-field-mapping-validation.json'
$compId = Read-Json 'phase-lmax-r111-compid-credential-alignment-validation.json'
$credentialTags = Read-Json 'phase-lmax-r111-credential-tag-mapping-validation.json'
$sequence = Read-Json 'phase-lmax-r111-sequence-reset-policy-validation.json'
$safety = Read-Json 'phase-lmax-r111-session-logon-only-safety-validation.json'
$raw = Read-Json 'phase-lmax-r111-raw-fix-sanitization-validation.json'
$marketData = Read-Json 'phase-lmax-r111-marketdata-block-until-fix-success-validation.json'
$production = Read-Json 'phase-lmax-r111-production-config-exclusion-validation.json'
$noExternal = Read-Json 'phase-lmax-r111-no-external-boundary-attempted.json'
$forbidden = Read-Json 'phase-lmax-r111-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r111-api-worker-fake-gateway-audit.json'
$usdjpy = Read-Json 'phase-lmax-r111-usdjpy-caveat-preservation.json'
$gate = Read-Json 'phase-lmax-r111-gate-validation.json'

$requiredArtifacts = @(
    'phase-lmax-r111-fix-logon-session-parameter-fix-report.md',
    'phase-lmax-r111-r110-root-cause-before-after-classification.json',
    'phase-lmax-r111-real-bounded-path-validation.json',
    'phase-lmax-r111-no-scheduler-polling-service-audit.json',
    'phase-lmax-r111-next-phase-recommendation.json'
)

foreach ($artifact in $requiredArtifacts) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactRoot $artifact)) "Missing artifact: $artifact"
}

Assert-True ($r110.classification -eq 'LMAX_R110_PASS_FIX_LOGON_FIELD_OR_SESSION_PARAMETER_SUSPECT_NO_EXTERNAL_ACTIVATION') 'R110 success evidence is missing or mismatched.'
Assert-True ($summary.classification -eq 'LMAX_R111_PASS_FIX_LOGON_SESSION_PARAMETER_REQUIREMENTS_IDENTIFIED_SUPPORT_VERIFICATION_NEEDED_NO_EXTERNAL_ACTIVATION') 'Unexpected R111 classification.'
Assert-True ($summary.fixLogonSessionParameterMappingReady -eq $false) 'R111 must not claim full mapping readiness.'
Assert-True ($summary.supportVerificationNeeded -eq $true) 'Support verification must be explicit.'
Assert-True ($field.basicSessionEnvelopePresent -eq $true) 'FIX Logon field presence review is missing.'
Assert-True ($field.actualFrameUsesSanitizedPlaceholderLabels -eq $true) 'Current placeholder-label gap must be acknowledged.'
Assert-True ($field.fixLogonSessionParameterMappingReady -eq $false -and $field.supportVerificationNeeded -eq $true) 'FIX Logon session parameter mapping state is not correctly classified.'
Assert-True ($compId.compIdCredentialAlignmentRequiresSupportVerification -eq $true) 'CompID/credential support verification must be explicit.'
Assert-True ($credentialTags.credentialTagMappingReady -eq $false -and $credentialTags.credentialTagMappingRequiresSupportVerification -eq $true) 'Credential tag mapping state is not correctly classified.'
Assert-True ($credentialTags.unsupportedTagsInvented -eq $false) 'Unsupported credential tags must not be invented.'
Assert-True ($sequence.sequenceResetPolicyReviewed -eq $true) 'Sequence/reset policy review is missing.'
Assert-True ($safety.writerBuilderSessionLogonOnly -eq $true) 'Writer/builder must remain session/logon-only.'
Assert-True ($safety.orderFramesSupported -eq $false -and $safety.newOrderSingleSupported -eq $false -and $safety.cancelReplaceSupported -eq $false) 'Order-capable FIX support was introduced.'
Assert-True ($safety.executionReportParsingSupported -eq $false -and $safety.fillsSupported -eq $false -and $safety.orderLifecycleSupported -eq $false) 'Execution/fill/order lifecycle parsing was introduced.'
Assert-True ($raw.rawFixFrameSerialized -eq $false -and $raw.rawFixMessagesSerialized -eq $false -and $raw.rawCredentialsSerialized -eq $false) 'Raw FIX or credential material serialization risk.'
Assert-True ($raw.credentialValuesReturned -eq $false -and $summary.credentialValuesReturned -eq $false) 'credentialValuesReturned must remain false.'
Assert-True ($marketData.marketDataRequestBlockedUntilFixSuccess -eq $true -and $marketData.marketDataRequestAttemptedDuringR111 -eq $false) 'MarketDataRequest must remain blocked until FIX success.'
Assert-True ($production.productionAccountAllowed -eq $false -and $production.productionConfigAllowed -eq $false) 'Production account/config must remain excluded.'
Assert-True ($noExternal.activationAttempted -eq $false -and $noExternal.socketOpened -eq $false -and $noExternal.tlsAttempted -eq $false -and $noExternal.fixLogonAttempted -eq $false -and $noExternal.marketDataRequestAttempted -eq $false) 'R111 attempted an external boundary.'
Assert-True ($forbidden.result -eq 'PASS') 'Forbidden actions audit failed.'
Assert-True ($apiWorker.result -eq 'PASS' -and $apiWorker.apiWorkerFakeLmaxGatewayOnly -eq $true) 'API/Worker FakeLmaxGatewayOnly audit failed.'
Assert-True ($usdjpy.caveatPreserved -eq $true) 'USDJPY caveat is missing or weakened.'
Assert-True ($gate.buildResult -like 'PASS*') 'Build evidence is missing.'
Assert-True ($gate.testResult -like 'PASS*') 'Test evidence is missing.'

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r111-*' -File |
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
    'DemoReadOnlyTargetCompId'
)

foreach ($pattern in $forbiddenPatterns) {
    Assert-True ($joined.IndexOf($pattern, [StringComparison]::OrdinalIgnoreCase) -lt 0) "Sensitive or raw protocol material found in R111 artifacts: $pattern"
}

Write-Host 'LMAX_R111_VALIDATION_PASS'
