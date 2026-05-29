param(
    [string]$ApiBaseUrl = "http://localhost:5050",
    [int]$TimeoutSeconds = 3,
    [string]$Phase7M5ReportFile = "artifacts/readiness/phase7m5-local-api-startup-troubleshooting-continuation-report.json",
    [string]$Phase7M5GateFile = "artifacts/readiness/phase7m5-local-api-startup-troubleshooting-continuation-gate.json",
    [string]$ReportFile = "artifacts/readiness/phase7m6-operator-started-local-api-health-verification-report.json",
    [string]$GateFile = "artifacts/readiness/phase7m6-operator-started-local-api-health-verification-gate.json",
    [string]$NoteFile = "artifacts/readiness/phase7m6-operator-started-local-api-health-verification-note.md"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

function Resolve-LocalPath([string]$PathValue) {
    if ([string]::IsNullOrWhiteSpace($PathValue)) { return "" }
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}

function Read-JsonFile([string]$PathValue, [string]$Label) {
    $resolved = Resolve-LocalPath $PathValue
    if (-not (Test-Path -LiteralPath $resolved)) { throw "$Label not found: $resolved" }
    return Get-Content -LiteralPath $resolved -Raw | ConvertFrom-Json
}

function Write-JsonFile([string]$PathValue, [object]$Value) {
    $resolved = Resolve-LocalPath $PathValue
    New-Item -ItemType Directory -Path (Split-Path -Parent $resolved) -Force | Out-Null
    $Value | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $resolved -Encoding UTF8
}

function Write-TextFile([string]$PathValue, [string]$Value) {
    $resolved = Resolve-LocalPath $PathValue
    New-Item -ItemType Directory -Path (Split-Path -Parent $resolved) -Force | Out-Null
    Set-Content -LiteralPath $resolved -Value $Value -Encoding UTF8
}

function Test-TimeoutException([System.Exception]$Exception) {
    $message = [string]$Exception.Message
    if ($message -match '(?i)timed?\s*out|timeout|operation has timed out|request was canceled') { return $true }
    if ($null -ne $Exception.InnerException) { return Test-TimeoutException $Exception.InnerException }
    return $false
}

function Get-SafeHealthSummary($Health) {
    if ($null -eq $Health) { return $null }
    $summary = [ordered]@{}
    foreach ($name in @(
        "status",
        "executionGateway",
        "marketDataMode",
        "liveTradingEnabled",
        "externalConnectionsEnabled",
        "databaseReachable",
        "pendingMigrationsCount",
        "environment",
        "persistenceProvider"
    )) {
        if ($Health.PSObject.Properties.Name -contains $name) {
            $summary[$name] = $Health.$name
        }
    }
    if ($summary.Count -eq 0) { return $null }
    return $summary
}

Write-Host "Phase 7M6 operator-started local API health verification"
Write-Host "Local-only. This script does not start the API, call LMAX, run snapshots, run replay, call POST endpoints, or mutate state."

$phase7M5Report = Read-JsonFile $Phase7M5ReportFile "Phase 7M5 report"
$phase7M5Gate = Read-JsonFile $Phase7M5GateFile "Phase 7M5 gate"
if ([string]$phase7M5Report.finalDecision -ne "PASS_LOCAL_API_STARTUP_TROUBLESHOOTING_PLAN_RECORDED") { throw "Phase 7M5 report is not complete." }
if ([string]$phase7M5Gate.finalDecision -ne "PASS_LOCAL_API_STARTUP_TROUBLESHOOTING_PLAN_RECORDED") { throw "Phase 7M5 gate is not complete." }

$generatedAt = [DateTimeOffset]::UtcNow.ToString("o")
$expectedLocalBaseUrl = "http://localhost:5050"
$uri = $null
$urlSafe = $false
$unsafeReason = $null
$healthUri = $null

try {
    $uri = [Uri]$ApiBaseUrl
    $schemeAllowed = $uri.Scheme -in @("http", "https")
    $hostAllowed = $uri.Host -in @("localhost", "127.0.0.1")
    $pathAllowed = [string]::IsNullOrWhiteSpace($uri.AbsolutePath) -or $uri.AbsolutePath -eq "/"
    if (-not $schemeAllowed) { $unsafeReason = "ApiBaseUrlSchemeNotHttpOrHttps" }
    elseif (-not $hostAllowed) { $unsafeReason = "ApiBaseUrlHostNotLocalhost" }
    elseif (-not $pathAllowed) { $unsafeReason = "ApiBaseUrlMustNotIncludeNonRootPath" }
    else {
        $urlSafe = $true
        $port = if ($uri.IsDefaultPort) { if ($uri.Scheme -eq "https") { 443 } else { 80 } } else { $uri.Port }
        $healthUri = ([UriBuilder]::new($uri.Scheme, $uri.Host, $port, "/health")).Uri
    }
} catch {
    $unsafeReason = "ApiBaseUrlCouldNotBeParsed"
}

$healthCheckAttempted = $false
$healthCheckResult = "NotAttempted"
$healthCheckElapsedMs = $null
$healthStatusCode = $null
$healthResponseSafeSummary = $null
$executionGateway = $null
$marketDataMode = $null
$liveTradingEnabled = $null
$externalConnectionsEnabled = $null
$databaseReachable = $null
$pendingMigrationsCount = $null
$safeRuntimePostureConfirmed = $false
$timeoutClassification = $null

if (-not $urlSafe) {
    $timeoutClassification = "UnsafeNonLocalHealthUrlRejected"
} else {
    $healthCheckAttempted = $true
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $response = Invoke-WebRequest -Method Get -Uri $healthUri.AbsoluteUri -TimeoutSec $TimeoutSeconds -UseBasicParsing -Headers @{ "X-Operator-Id" = "local-admin" }
        $stopwatch.Stop()
        $healthCheckElapsedMs = [int]$stopwatch.ElapsedMilliseconds
        $healthStatusCode = [int]$response.StatusCode
        $healthCheckResult = "HttpStatus$healthStatusCode"

        $healthJson = $null
        if ($response.Content) {
            try { $healthJson = $response.Content | ConvertFrom-Json } catch { $healthJson = $null }
        }
        $healthResponseSafeSummary = Get-SafeHealthSummary $healthJson
        if ($null -ne $healthJson) {
            if ($healthJson.PSObject.Properties.Name -contains "executionGateway") { $executionGateway = $healthJson.executionGateway }
            if ($healthJson.PSObject.Properties.Name -contains "marketDataMode") { $marketDataMode = $healthJson.marketDataMode }
            if ($healthJson.PSObject.Properties.Name -contains "liveTradingEnabled") { $liveTradingEnabled = [bool]$healthJson.liveTradingEnabled }
            if ($healthJson.PSObject.Properties.Name -contains "externalConnectionsEnabled") { $externalConnectionsEnabled = [bool]$healthJson.externalConnectionsEnabled }
            if ($healthJson.PSObject.Properties.Name -contains "databaseReachable") { $databaseReachable = [bool]$healthJson.databaseReachable }
            if ($healthJson.PSObject.Properties.Name -contains "pendingMigrationsCount") { $pendingMigrationsCount = [int]$healthJson.pendingMigrationsCount }
        }

        $safeRuntime = $true
        if ($null -ne $executionGateway -and [string]$executionGateway -ne "FakeLmaxGateway") { $safeRuntime = $false }
        if ($null -ne $liveTradingEnabled -and [bool]$liveTradingEnabled) { $safeRuntime = $false }
        if ($null -ne $externalConnectionsEnabled -and [bool]$externalConnectionsEnabled) { $safeRuntime = $false }
        $safeRuntimePostureConfirmed = ($healthStatusCode -ge 200 -and $healthStatusCode -lt 300 -and $safeRuntime)

        if ($healthStatusCode -ge 200 -and $healthStatusCode -lt 300 -and $safeRuntime) {
            $timeoutClassification = "LocalApiHealthRecovered"
        } elseif ($healthStatusCode -ge 200 -and $healthStatusCode -lt 300 -and -not $safeRuntime) {
            $timeoutClassification = "LocalApiHealthOkButUnsafeRuntimePosture"
        } else {
            $timeoutClassification = "LocalApiHealthStillUnavailable"
        }
    } catch {
        $stopwatch.Stop()
        $healthCheckElapsedMs = [int]$stopwatch.ElapsedMilliseconds
        $healthCheckResult = if (Test-TimeoutException $_.Exception) { "Timeout" } else { "ConnectionFailed" }
        $timeoutClassification = "LocalApiHealthStillUnavailable"
    }
}

$finalDecision = switch ($timeoutClassification) {
    "LocalApiHealthRecovered" { "PASS_OPERATOR_STARTED_LOCAL_API_HEALTH_OK" }
    "LocalApiHealthOkButUnsafeRuntimePosture" { "PASS_LOCAL_API_HEALTH_OK_BUT_UNSAFE_RUNTIME_BLOCKED" }
    "UnsafeNonLocalHealthUrlRejected" { "PASS_UNSAFE_NONLOCAL_HEALTH_URL_REJECTED" }
    default { "PASS_OPERATOR_STARTED_LOCAL_API_HEALTH_STILL_UNAVAILABLE" }
}

$allowedNextPhase = switch ($timeoutClassification) {
    "LocalApiHealthRecovered" { "Phase 7M7 - Optional Local Replay Readiness Gate, No External Run" }
    "LocalApiHealthOkButUnsafeRuntimePosture" { "Phase 7M7 - Local Runtime Safety Configuration Fix Plan, No External Run" }
    "UnsafeNonLocalHealthUrlRejected" { "Phase 7M7 - Local API Base URL Configuration Fix Plan, No External Run" }
    default { "Phase 7M7 - Local API Startup Troubleshooting Continuation, No External Run" }
}

$recommendedNextAction = switch ($timeoutClassification) {
    "LocalApiHealthRecovered" { "Proceed only to a no-external-run optional local replay readiness gate; do not run replay in Phase 7M6." }
    "LocalApiHealthOkButUnsafeRuntimePosture" { "Block replay and correct local runtime safety configuration before any further health-dependent work." }
    "UnsafeNonLocalHealthUrlRejected" { "Correct the API base URL to a localhost or 127.0.0.1 root URL; no health call was made." }
    default { "Continue local API startup troubleshooting; do not run replay until local GET /health is healthy and safe." }
}

$report = [ordered]@{
    phase = "7M6"
    generatedAtUtc = $generatedAt
    diagnosticType = "OperatorStartedLocalApiHealthVerification"
    previousClassification = "LocalApiHealthStillUnavailable"
    expectedLocalBaseUrl = $expectedLocalBaseUrl
    lmaxEvidenceFailure = $false
    apiStartedByCodexInThisPhase = $false
    operatorStartedApiAssumed = $true
    externalRunAttemptedInThisPhase = $false
    snapshotRunInThisPhase = $false
    replayRunInThisPhase = $false
    localReplayRunInThisPhase = $false
    externalUrlCalled = $false
    onlyLocalhostHealthChecked = [bool]$urlSafe
    healthEndpoint = "GET /health"
    postEndpointCalled = $false
    mutationAttempted = $false
    schedulerOrPollingAdded = $false
    runtimeShadowReplaySubmitAdded = $false
    orderPathAdded = $false
    gatewayRegistrationAdded = $false
    tradingMutationAdded = $false
    retryBatchLoopAdded = $false
    wrapperValidationWeakened = $false
    healthCheckAttempted = $healthCheckAttempted
    healthCheckResult = $healthCheckResult
    healthCheckStatusCode = $healthStatusCode
    healthCheckElapsedMs = $healthCheckElapsedMs
    healthResponseSafeSummary = $healthResponseSafeSummary
    executionGateway = $executionGateway
    marketDataMode = $marketDataMode
    liveTradingEnabled = $liveTradingEnabled
    externalConnectionsEnabled = $externalConnectionsEnabled
    databaseReachable = $databaseReachable
    pendingMigrationsCount = $pendingMigrationsCount
    safeRuntimePostureConfirmed = $safeRuntimePostureConfirmed
    timeoutClassification = $timeoutClassification
    recommendedNextAction = $recommendedNextAction
    allowedNextPhase = $allowedNextPhase
    noSensitiveContent = $true
    finalDecision = $finalDecision
}

$gate = [ordered]@{
    phase = "7M6"
    generatedAtUtc = $generatedAt
    gateType = "OperatorStartedLocalApiHealthVerificationGate"
    reportPath = $ReportFile
    healthVerificationCompleted = $true
    localOnly = $true
    apiStartedByCodexInThisPhase = $false
    externalRunAllowed = $false
    snapshotAllowed = $false
    replayAllowedInThisPhase = $false
    localReplayRunInThisPhase = $false
    externalRunAttemptedInThisPhase = $false
    snapshotRunInThisPhase = $false
    replayRunInThisPhase = $false
    externalUrlCalled = $false
    postEndpointCalled = $false
    mutationAttempted = $false
    schedulerOrPollingEnabled = $false
    runtimeShadowReplaySubmitEnabled = $false
    orderPathEnabled = $false
    gatewayRegistrationEnabled = $false
    tradingMutationEnabled = $false
    retryBatchLoopAdded = $false
    wrapperValidationWeakened = $false
    lmaxEvidenceFailure = $false
    evidenceCycleRemainsClosed = $true
    uiStatusWorkstreamRemainsClosed = $true
    healthCheckResult = $healthCheckResult
    timeoutClassification = $timeoutClassification
    safeRuntimePostureConfirmed = $safeRuntimePostureConfirmed
    allowedNextPhase = $allowedNextPhase
    noSensitiveContent = $true
    finalDecision = $finalDecision
}

$note = @"
# Phase 7M6 - Operator-Started Local API Health Verification

This phase assumes the operator manually started or attempted to start the local API outside Codex. Codex did not start the API.

The verification performs only a safe local GET `/health` request against `http://localhost:5050`. It does not connect to LMAX, request a snapshot, run local or external replay, call POST endpoints, start scheduler/polling, submit orders, register a gateway, or mutate trading state.

## Result

- Health check attempted: $healthCheckAttempted
- Health check result: $healthCheckResult
- Timeout classification: $timeoutClassification
- Safe runtime posture confirmed: $safeRuntimePostureConfirmed
- Final decision: $finalDecision
- Allowed next phase: $allowedNextPhase

## Interpretation

$recommendedNextAction

The LMAX evidence cycle remains closed, the UI/status workstream remains closed, and replay remains out of scope for Phase 7M6.
"@

Write-JsonFile $ReportFile $report
Write-JsonFile $GateFile $gate
Write-TextFile $NoteFile $note

Write-Host "Health verification report: $(Resolve-LocalPath $ReportFile)"
Write-Host "Health verification gate: $(Resolve-LocalPath $GateFile)"
Write-Host "Health verification note: $(Resolve-LocalPath $NoteFile)"
Write-Host "FinalDecision: $finalDecision"
