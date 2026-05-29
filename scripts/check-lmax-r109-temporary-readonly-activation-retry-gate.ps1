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

$summary = Read-Json 'phase-lmax-r109-temporary-readonly-activation-retry-summary.json'
$endpoint = Read-Json 'phase-lmax-r109-demo-endpoint-binding-evidence.json'
$tls = Read-Json 'phase-lmax-r109-tls-boundary-evidence.json'
$fixAck = Read-Json 'phase-lmax-r109-fix-session-acknowledgement-evidence.json'
$fixBoundary = Read-Json 'phase-lmax-r109-fix-session-boundary-evidence.json'
$marketData = Read-Json 'phase-lmax-r109-marketdata-request-evidence.json'
$trace = Read-Json 'phase-lmax-r109-operational-invocation-trace.json'
$forbidden = Read-Json 'phase-lmax-r109-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r109-api-worker-fake-gateway-audit.json'
$usdjpy = Read-Json 'phase-lmax-r109-usdjpy-caveat-preservation.json'
$gate = Read-Json 'phase-lmax-r109-gate-validation.json'

$requiredArtifacts = @(
    'phase-lmax-r109-temporary-readonly-activation-retry-report.md',
    'phase-lmax-r109-operator-approval-note.md',
    'phase-lmax-r109-preflight-result.json',
    'phase-lmax-r109-socket-connector-evidence.json',
    'phase-lmax-r109-fix-credential-material-evidence.json',
    'phase-lmax-r109-fix-logon-frame-write-evidence.json',
    'phase-lmax-r109-marketdata-sanitized-result.json',
    'phase-lmax-r109-shutdown-revert-evidence.json',
    'phase-lmax-r109-next-phase-recommendation.json'
)

foreach ($artifact in $requiredArtifacts) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactRoot $artifact)) "Missing artifact: $artifact"
}

Assert-True ($summary.classification -eq 'LMAX_R109_FAIL_FIX_LOGON_OR_SESSION_BOUNDARY') 'Unexpected R109 classification.'
Assert-True ($summary.externalActivationAttempted -eq $true) 'R109 must record the approved external activation attempt.'
Assert-True ($summary.attemptCount -eq 1) 'R109 must record exactly one attempt.'
Assert-True ($summary.retryPhaseReservationPassed -eq $true) 'R109 retry phase reservation must pass.'
Assert-True ($summary.toolUsed -eq 'QQ.Production.Intraday.Tools.LmaxReadOnlyActivation') 'Unexpected activation tool.'
Assert-True ($summary.adapterMode -eq 'real-bounded-executable-readonly') 'Unexpected adapter mode.'
Assert-True ($endpoint.endpointMode -eq 'Demo') 'Endpoint mode must be Demo.'
Assert-True ($endpoint.endpointApproved -eq $true -and $endpoint.hostConcreteBinding -eq $true -and $endpoint.hostWasPlaceholder -eq $false) 'Demo endpoint binding evidence is incomplete.'
Assert-True ($endpoint.productionExcluded -eq $true) 'Production endpoint/config must be excluded.'
Assert-True ($tls.tlsSucceeded -eq $true -and $tls.tlsResultCategory -eq 'Succeeded') 'TLS must be recorded as succeeded.'
Assert-True ($tls.tlsRawMaterialSerialized -eq $false) 'TLS material must not be serialized.'
Assert-True ($trace.fixLogonAttempted -eq $true) 'FIX logon/session must be recorded as attempted.'
Assert-True ($fixAck.fixAcknowledgementReaderParserClassifierUsed -eq $true) 'FIX acknowledgement reader/parser/classifier use must be recorded.'
Assert-True ($fixAck.fixAcknowledgementCategory -eq 'FixLogoutReceived') 'Expected sanitized FIX acknowledgement category is missing.'
Assert-True ($fixAck.orderMessageParsingSupported -eq $false -and $fixAck.executionReportParsingSupported -eq $false) 'FIX parser must remain session-level only.'
Assert-True ($fixAck.rawFixMessageSerialized -eq $false) 'Raw FIX messages must not be serialized.'
Assert-True ($fixBoundary.fixBoundaryStatus -eq 'FailedValidation') 'FIX boundary must be FailedValidation for R109.'
Assert-True ($marketData.marketDataRequestAttempted -eq $false) 'MarketDataRequest must remain blocked after FIX failure.'
Assert-True ($summary.credentialValuesReturned -eq $false) 'credentialValuesReturned must remain false.'
Assert-True ($summary.sensitiveValuesPrintedStoredSerialized -eq $false) 'Sensitive values must not be printed/stored/serialized.'
Assert-True ($forbidden.result -eq 'PASS') 'Forbidden action audit failed.'
Assert-True ($apiWorker.result -eq 'PASS' -and $apiWorker.apiWorkerFakeLmaxGatewayOnly -eq $true) 'API/Worker FakeLmaxGatewayOnly audit failed.'
Assert-True ($usdjpy.caveatPreserved -eq $true) 'USDJPY caveat is missing or weakened.'
Assert-True ($gate.buildResult -like 'PASS*') 'Build evidence is missing.'
Assert-True ($gate.unitTests -like 'PASS*' -and $gate.integrationTests -like 'PASS*') 'Test evidence is missing.'

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r109-*' -File |
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
    Assert-True ($joined.IndexOf($pattern, [StringComparison]::OrdinalIgnoreCase) -lt 0) "Sensitive or raw protocol material found in R109 artifacts: $pattern"
}

Write-Host 'LMAX_R109_VALIDATION_PASS'
