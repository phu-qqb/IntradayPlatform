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

$summary = Read-Json 'phase-lmax-r110-fix-session-boundary-root-cause-summary.json'
$r109Summary = Read-Json 'phase-lmax-r109-temporary-readonly-activation-retry-summary.json'
$r109Ack = Read-Json 'phase-lmax-r109-fix-session-acknowledgement-evidence.json'
$fieldReview = Read-Json 'phase-lmax-r110-fix-logon-field-presence-review.json'
$sessionReview = Read-Json 'phase-lmax-r110-fix-session-parameter-review.json'
$logoutReview = Read-Json 'phase-lmax-r110-fix-logout-reason-sanitized-review.json'
$marketData = Read-Json 'phase-lmax-r110-marketdata-block-after-fix-failure-review.json'
$noExternal = Read-Json 'phase-lmax-r110-no-external-boundary-attempted.json'
$forbidden = Read-Json 'phase-lmax-r110-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r110-api-worker-fake-gateway-audit.json'
$usdjpy = Read-Json 'phase-lmax-r110-usdjpy-caveat-preservation.json'
$sanitization = Read-Json 'phase-lmax-r110-credential-endpoint-tls-fix-sanitization-validation.json'
$gate = Read-Json 'phase-lmax-r110-gate-validation.json'

$requiredArtifacts = @(
    'phase-lmax-r110-fix-session-boundary-root-cause-report.md',
    'phase-lmax-r110-r109-boundary-before-after-classification.json',
    'phase-lmax-r110-r109-fix-logout-review.json',
    'phase-lmax-r110-fix-compid-credential-alignment-review.json',
    'phase-lmax-r110-fix-sequence-reset-policy-review.json',
    'phase-lmax-r110-fix-readonly-session-safety-review.json',
    'phase-lmax-r110-real-bounded-path-validation.json',
    'phase-lmax-r110-no-scheduler-polling-service-audit.json',
    'phase-lmax-r110-next-phase-recommendation.json'
)

foreach ($artifact in $requiredArtifacts) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactRoot $artifact)) "Missing artifact: $artifact"
}

Assert-True ($r109Summary.classification -eq 'LMAX_R109_FAIL_FIX_LOGON_OR_SESSION_BOUNDARY') 'R109 FIX boundary evidence is missing or mismatched.'
Assert-True ($r109Ack.fixAcknowledgementCategory -eq 'FixLogoutReceived') 'FixLogoutReceived must be acknowledged.'
Assert-True ($summary.fixLogoutReceivedAcknowledged -eq $true) 'R110 summary must acknowledge FixLogoutReceived.'
Assert-True ($summary.externalActivationAttempted -eq $false) 'R110 must not attempt external activation.'
Assert-True ($summary.r109TcpSuccessProven -eq $true -and $summary.r109TlsSuccessProven -eq $true) 'R109 TCP/TLS success must be proven.'
Assert-True ($summary.r109FixLogonAttemptProven -eq $true) 'R109 FIX Logon attempt must be proven.'
Assert-True ($summary.r109FixAcknowledgementReaderParserClassifierProven -eq $true) 'R109 FIX acknowledgement reader evidence must be proven.'
Assert-True ($fieldReview.fieldsPresentByBooleanOnly.BeginString -eq $true) 'FIX Logon field presence review is incomplete.'
Assert-True ($fieldReview.fieldsPresentByBooleanOnly.MsgTypeLogon -eq $true) 'FIX Logon MsgType presence review is incomplete.'
Assert-True ($fieldReview.fieldsPresentByBooleanOnly.UsernameOrCredentialIdentifierTag -eq $false) 'Credential tag presence review must be explicit.'
Assert-True ($fieldReview.rawFixFrameSerialized -eq $false) 'Raw FIX frame must not be serialized.'
Assert-True ($sessionReview.safeNextFixPossible -eq $true) 'FIX session parameter review must identify safe next fix possibility.'
Assert-True ($logoutReview.fixLogoutReceived -eq $true) 'Logout reason review must acknowledge logout.'
Assert-True ($logoutReview.logoutReasonSanitizedCategoryCaptured -eq $false) 'Logout reason availability must be explicitly represented.'
Assert-True ($marketData.marketDataBlockedUntilFixSessionSuccess -eq $true -and $marketData.marketDataRequestAttemptedAfterFixFailure -eq $false) 'MarketDataRequest must remain blocked after FIX failure.'
Assert-True ($noExternal.activationAttempted -eq $false -and $noExternal.socketOpened -eq $false -and $noExternal.tlsAttempted -eq $false -and $noExternal.fixLogonAttempted -eq $false -and $noExternal.marketDataRequestAttempted -eq $false) 'R110 attempted an external boundary.'
Assert-True ($summary.credentialValuesReturned -eq $false -and $sanitization.credentialValuesReturned -eq $false) 'credentialValuesReturned must remain false.'
Assert-True ($forbidden.result -eq 'PASS') 'Forbidden actions audit failed.'
Assert-True ($apiWorker.result -eq 'PASS' -and $apiWorker.apiWorkerFakeLmaxGatewayOnly -eq $true) 'API/Worker FakeLmaxGatewayOnly audit failed.'
Assert-True ($usdjpy.caveatPreserved -eq $true) 'USDJPY caveat is missing or weakened.'
Assert-True ($gate.buildResult -like 'PASS*') 'Build evidence is missing.'
Assert-True ($gate.testResult -like 'PASS*') 'Test evidence is missing.'

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r110-*' -File |
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
    '554='
)

foreach ($pattern in $forbiddenPatterns) {
    Assert-True ($joined.IndexOf($pattern, [StringComparison]::OrdinalIgnoreCase) -lt 0) "Sensitive or raw protocol material found in R110 artifacts: $pattern"
}

Write-Host 'LMAX_R110_VALIDATION_PASS'
