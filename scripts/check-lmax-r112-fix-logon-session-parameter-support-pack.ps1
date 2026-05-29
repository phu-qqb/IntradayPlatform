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

$summary = Read-Json 'phase-lmax-r112-fix-logon-session-parameter-support-summary.json'
$r111 = Read-Json 'phase-lmax-r111-fix-logon-session-parameter-summary.json'
$carryforward = Read-Json 'phase-lmax-r112-r111-requirements-carryforward-review.json'
$matrix = Read-Json 'phase-lmax-r112-fix-logon-parameter-requirements-matrix.json'
$known = Read-Json 'phase-lmax-r112-fix-logon-known-present-fields.json'
$unknown = Read-Json 'phase-lmax-r112-fix-logon-unknown-required-fields.json'
$gap = Read-Json 'phase-lmax-r112-credential-session-material-gap-review.json'
$sequence = Read-Json 'phase-lmax-r112-sequence-reset-policy-support-review.json'
$readonly = Read-Json 'phase-lmax-r112-readonly-marketdata-session-requirements-review.json'
$noExternal = Read-Json 'phase-lmax-r112-no-external-boundary-attempted.json'
$forbidden = Read-Json 'phase-lmax-r112-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r112-api-worker-fake-gateway-audit.json'
$sanitize = Read-Json 'phase-lmax-r112-credential-fix-sanitization-validation.json'
$usdjpy = Read-Json 'phase-lmax-r112-usdjpy-caveat-preservation.json'
$next = Read-Json 'phase-lmax-r112-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r112-gate-validation.json'

$requiredArtifacts = @(
    'phase-lmax-r112-fix-logon-session-parameter-support-pack-report.md',
    'phase-lmax-r112-support-verification-checklist.md',
    'phase-lmax-r112-support-question-template.md'
)

foreach ($artifact in $requiredArtifacts) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactRoot $artifact)) "Missing artifact: $artifact"
}

Assert-True ($r111.classification -eq 'LMAX_R111_PASS_FIX_LOGON_SESSION_PARAMETER_REQUIREMENTS_IDENTIFIED_SUPPORT_VERIFICATION_NEEDED_NO_EXTERNAL_ACTIVATION') 'R111 success evidence is missing or mismatched.'
Assert-True ($carryforward.r111SuccessEvidenceMatched -eq $true) 'R111 carryforward review is missing.'
Assert-True ($summary.classification -eq 'LMAX_R112_PASS_FIX_LOGON_SESSION_PARAMETER_SUPPORT_PACK_READY_NO_EXTERNAL_ACTIVATION') 'Unexpected R112 classification.'
Assert-True ($summary.decision -eq 'SupportVerificationRequiredBeforeParameterBindingOrRetry') 'Support/config verification decision is missing.'
Assert-True ($matrix.matrix.Count -ge 12) 'Requirements matrix is missing or too small.'
Assert-True ($matrix.unsupportedTagsInvented -eq $false -and $summary.unsupportedTagsInvented -eq $false) 'Unsupported LMAX tags were invented.'
Assert-True ($known.fieldsAlreadyPresentByBooleanReview.BeginString -eq $true) 'Known present fields split is missing.'
Assert-True ($known.structurallyPresentButCurrentlySanitizedOrPlaceholder.SenderCompID -eq $true) 'Sanitized/placeholder session identifier split is missing.'
Assert-True ($unknown.unknownRequiredFieldCategories.Count -ge 8) 'Unknown required fields split is missing.'
Assert-True ($gap.supportVerificationRequired -eq $true) 'Credential/session material gap review is missing.'
Assert-True ($gap.credentialValuesReturned -eq $false -and $summary.credentialValuesReturned -eq $false) 'credentialValuesReturned must remain false.'
Assert-True ($sequence.sequenceResetPolicyReviewed -eq $true -and $sequence.suspectButNotProvenRootCause -eq $true) 'Sequence/reset policy support review is missing.'
Assert-True ($readonly.marketDataRequestBlockedWithoutFixSuccess -eq $true) 'MarketDataRequest must remain blocked without FIX success.'
Assert-True ($noExternal.activationAttempted -eq $false -and $noExternal.tcpAttempted -eq $false -and $noExternal.tlsAttempted -eq $false -and $noExternal.fixLogonAttempted -eq $false -and $noExternal.marketDataRequestAttempted -eq $false) 'R112 attempted an external boundary.'
Assert-True ($forbidden.result -eq 'PASS') 'Forbidden actions audit failed.'
Assert-True ($forbidden.ordersSubmitted -eq $false -and $forbidden.tradingStateMutated -eq $false) 'Order/trading path was introduced.'
Assert-True ($apiWorker.result -eq 'PASS' -and $apiWorker.apiWorkerFakeLmaxGatewayOnly -eq $true -and $apiWorker.liveGatewayDefaultIntroduced -eq $false) 'API/Worker FakeLmaxGatewayOnly audit failed.'
Assert-True ($sanitize.credentialValuesReturned -eq $false) 'Credential values returned.'
Assert-True ($sanitize.rawCredentialsPrintedStoredSerialized -eq $false -and $sanitize.rawFixMessagesPrintedStoredSerialized -eq $false -and $sanitize.rawSensitiveFixLogsPrintedStoredSerialized -eq $false) 'Raw credential or FIX material was serialized.'
Assert-True ($usdjpy.caveatPreserved -eq $true) 'USDJPY caveat is missing or weakened.'
Assert-True ($next.nextRecommendedPhase -eq 'Phase LMAX-R113 - FIX Logon Session Parameter Support Input Review') 'Next phase recommendation is missing or wrong.'
Assert-True ($gate.buildResult -like 'PASS*') 'Build evidence is missing.'
Assert-True ($gate.testResult -like 'PASS*') 'Test evidence is missing.'

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r112-*' -File |
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
    Assert-True ($joined.IndexOf($pattern, [StringComparison]::OrdinalIgnoreCase) -lt 0) "Sensitive or raw protocol material found in R112 artifacts: $pattern"
}

Write-Host 'LMAX_R112_VALIDATION_PASS'
