param(
    [string]$ApiBaseUrl = "http://localhost:5050",
    [int]$TimeoutSeconds = 3,
    [string]$PlanFile = "artifacts/readiness/phase7m-local-api-health-timeout-follow-up-plan.json",
    [string]$GateFile = "artifacts/readiness/phase7m-local-api-health-timeout-follow-up-gate.json",
    [string]$ReportFile = "artifacts/readiness/phase7m2-local-api-health-timeout-diagnostic-report.json",
    [string]$DiagnosticGateFile = "artifacts/readiness/phase7m2-local-api-health-timeout-diagnostic-gate.json",
    [string]$NoteFile = "artifacts/readiness/phase7m2-local-api-health-timeout-diagnostic-note.md"
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
    if (-not (Test-Path -LiteralPath $resolved)) {
        throw "$Label not found: $resolved"
    }
    return Get-Content -LiteralPath $resolved -Raw | ConvertFrom-Json
}

function Write-JsonFile([string]$PathValue, [object]$Value) {
    $resolved = Resolve-LocalPath $PathValue
    $dir = Split-Path -Parent $resolved
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    $Value | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $resolved -Encoding UTF8
}

function Write-TextFile([string]$PathValue, [string]$Value) {
    $resolved = Resolve-LocalPath $PathValue
    $dir = Split-Path -Parent $resolved
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    Set-Content -LiteralPath $resolved -Value $Value -Encoding UTF8
}

function Get-SafeHealthSubset($Health) {
    if ($null -eq $Health) { return $null }
    $subset = [ordered]@{}
    foreach ($name in @(
        "status",
        "executionGateway",
        "liveTradingEnabled",
        "externalConnectionsEnabled",
        "databaseReachable",
        "pendingMigrationsCount",
        "marketDataMode"
    )) {
        if ($Health.PSObject.Properties.Name -contains $name) {
            $subset[$name] = $Health.$name
        }
    }
    if ($subset.Count -eq 0) { return $null }
    return $subset
}

function Test-TimeoutException([System.Exception]$Exception) {
    $message = [string]$Exception.Message
    if ($message -match '(?i)timed?\s*out|timeout|operation has timed out|request was canceled') { return $true }
    if ($null -ne $Exception.InnerException) {
        return Test-TimeoutException $Exception.InnerException
    }
    return $false
}

function Get-LocalApiProcessStatus {
    $matches = @()
    try {
        $processes = Get-Process -ErrorAction SilentlyContinue | Where-Object {
            $_.ProcessName -eq "QQ.Production.Intraday.Api" -or $_.ProcessName -eq "dotnet"
        }
        foreach ($process in $processes) {
            $item = [ordered]@{
                processName = $process.ProcessName
                id = $process.Id
            }
            try {
                $item["startTimeAvailable"] = ($null -ne $process.StartTime)
            } catch {
                $item["startTimeAvailable"] = $false
            }
            $matches += [pscustomobject]$item
        }
    } catch {
        return [ordered]@{
            status = "Unknown"
            detail = "Process inspection unavailable."
            matches = @()
        }
    }

    $status = if ($matches.Count -gt 0) { "LikelyApiOrDotnetProcessPresent" } else { "NoLikelyApiProcessObserved" }
    return [ordered]@{
        status = $status
        matchCount = $matches.Count
        matches = $matches
    }
}

function Get-LocalPortListeningStatus([int]$Port) {
    if ($Port -le 0) {
        return [ordered]@{
            status = "Unknown"
            port = $Port
            listenerCount = 0
        }
    }

    try {
        $listeners = @(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue)
        return [ordered]@{
            status = if ($listeners.Count -gt 0) { "Listening" } else { "NotListening" }
            port = $Port
            listenerCount = $listeners.Count
        }
    } catch {
        return [ordered]@{
            status = "Unknown"
            port = $Port
            listenerCount = 0
            detail = "Get-NetTCPConnection unavailable."
        }
    }
}

Write-Host "Phase 7M2 local API health timeout diagnostic"
Write-Host "Local-only. This script never calls LMAX, snapshots, replay endpoints, POST endpoints, or credential sources."

$phase7MPlan = Read-JsonFile $PlanFile "Phase 7M plan"
$phase7MGate = Read-JsonFile $GateFile "Phase 7M gate"
if ([string]$phase7MPlan.finalDecision -ne "PASS_LOCAL_API_HEALTH_TIMEOUT_FOLLOW_UP_PLAN_RECORDED") {
    throw "Phase 7M plan is not in the expected completed state."
}
if ([string]$phase7MGate.finalDecision -ne "PASS_LOCAL_API_HEALTH_TIMEOUT_FOLLOW_UP_PLAN_RECORDED") {
    throw "Phase 7M gate is not in the expected completed state."
}

$generatedAt = [DateTimeOffset]::UtcNow.ToString("o")
$uri = $null
$urlSafe = $false
$unsafeReason = $null
$healthUri = $null
$port = 0

try {
    $uri = [Uri]$ApiBaseUrl
    $hostAllowed = $uri.Host -in @("localhost", "127.0.0.1")
    $schemeAllowed = $uri.Scheme -in @("http", "https")
    $pathAllowed = [string]::IsNullOrWhiteSpace($uri.AbsolutePath) -or $uri.AbsolutePath -eq "/"
    if (-not $schemeAllowed) { $unsafeReason = "ApiBaseUrlSchemeNotHttpOrHttps" }
    elseif (-not $hostAllowed) { $unsafeReason = "ApiBaseUrlHostNotLocalhost" }
    elseif (-not $pathAllowed) { $unsafeReason = "ApiBaseUrlMustNotIncludeNonRootPath" }
    else {
        $urlSafe = $true
        if ($uri.IsDefaultPort) {
            $port = if ($uri.Scheme -eq "https") { 443 } else { 80 }
        } else {
            $port = $uri.Port
        }
        $builder = [UriBuilder]::new($uri.Scheme, $uri.Host, $port, "/health")
        $healthUri = $builder.Uri
    }
} catch {
    $unsafeReason = "ApiBaseUrlCouldNotBeParsed"
}

$localPortListeningStatus = if ($urlSafe) { Get-LocalPortListeningStatus -Port $port } else { [ordered]@{ status = "NotCheckedUnsafeUrl"; port = $port; listenerCount = 0 } }
$localApiProcessStatus = Get-LocalApiProcessStatus

$healthCheckAttempted = $false
$healthCheckResult = "NotAttempted"
$healthCheckElapsedMs = $null
$healthSubset = $null
$healthStatusCode = $null
$timeoutClassification = $null
$likelyCauseCandidates = @()

if (-not $urlSafe) {
    $timeoutClassification = "UnsafeNonLocalHealthUrlRejected"
    $likelyCauseCandidates = @("Configured API base URL is not a safe localhost or 127.0.0.1 root URL.")
} else {
    $healthCheckAttempted = $true
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $response = Invoke-WebRequest -Method Get -Uri $healthUri.AbsoluteUri -TimeoutSec $TimeoutSeconds -UseBasicParsing -Headers @{ "X-Operator-Id" = "local-admin" }
        $stopwatch.Stop()
        $healthCheckElapsedMs = [int]$stopwatch.ElapsedMilliseconds
        $healthStatusCode = [int]$response.StatusCode
        $healthCheckResult = "HttpStatus$healthStatusCode"
        if ($response.Content) {
            try {
                $healthJson = $response.Content | ConvertFrom-Json
                $healthSubset = Get-SafeHealthSubset $healthJson
            } catch {
                $healthSubset = $null
            }
        }
        if ($healthStatusCode -ge 200 -and $healthStatusCode -lt 300) {
            $timeoutClassification = "RecoveredOrTransient"
            $likelyCauseCandidates = @("Local /health responded within the diagnostic timeout; earlier replay health timeout may have been transient or startup-order related.")
        } else {
            $timeoutClassification = "LocalApiHealthTimeoutStillPresent"
            $likelyCauseCandidates = @("Local /health returned a non-success status; inspect local API health dependencies before replay readiness.")
        }
    } catch {
        $stopwatch.Stop()
        $healthCheckElapsedMs = [int]$stopwatch.ElapsedMilliseconds
        if ([string]$localPortListeningStatus.status -eq "NotListening") {
            $healthCheckResult = "ConnectionFailed"
            $timeoutClassification = "LocalApiNotRunningOrWrongPort"
            $likelyCauseCandidates = @("No local listener was observed on the configured API port.", "The API may not be running or the base URL port may be incorrect.")
        } elseif (Test-TimeoutException $_.Exception) {
            $healthCheckResult = "Timeout"
            $timeoutClassification = "LocalApiHealthTimeoutStillPresent"
            $likelyCauseCandidates = @("Local API accepted or attempted the connection but did not respond within the short diagnostic timeout.", "API startup, database health, or local dependency readiness may be slow or blocked.")
        } else {
            $healthCheckResult = "LocalHealthError"
            $timeoutClassification = "LocalApiHealthTimeoutStillPresent"
            $likelyCauseCandidates = @("Local /health request failed while the port inspection did not prove the API was absent.", "Inspect local API logs, startup health dependencies, and base URL configuration.")
        }
    }
}

$recommendedNextAction = switch ($timeoutClassification) {
    "RecoveredOrTransient" { "Plan optional local replay readiness check in a later no-external-run phase, but do not run replay now." }
    "LocalApiHealthTimeoutStillPresent" { "Diagnose local API process, startup, timeout, or dependency readiness separately; do not run replay." }
    "LocalApiNotRunningOrWrongPort" { "Start or configure the local API manually in a future phase, then re-check local /health before any optional replay readiness gate." }
    "UnsafeNonLocalHealthUrlRejected" { "Fix the local API base URL configuration to a localhost or 127.0.0.1 root URL; no calls were made." }
    default { "Keep optional replay blocked until local API health is understood." }
}

$allowedNextPhase = switch ($timeoutClassification) {
    "RecoveredOrTransient" { "Phase 7M3 - Optional Local Replay Readiness Gate, No External Run" }
    "UnsafeNonLocalHealthUrlRejected" { "Phase 7M3 - Local API Base URL Configuration Fix Plan, No External Run" }
    default { "Phase 7M3 - Local API Startup/Configuration Follow-Up Plan, No External Run" }
}

$finalDecision = switch ($timeoutClassification) {
    "RecoveredOrTransient" { "PASS_LOCAL_API_HEALTH_DIAGNOSTIC_OK" }
    "LocalApiHealthTimeoutStillPresent" { "PASS_LOCAL_API_HEALTH_TIMEOUT_STILL_PRESENT" }
    "LocalApiNotRunningOrWrongPort" { "PASS_LOCAL_API_NOT_RUNNING_OR_WRONG_PORT" }
    "UnsafeNonLocalHealthUrlRejected" { "PASS_UNSAFE_NONLOCAL_HEALTH_URL_REJECTED" }
    default { "PASS_LOCAL_API_HEALTH_TIMEOUT_STILL_PRESENT" }
}

$disallowedActions = @(
    "No external LMAX connection.",
    "No snapshot.",
    "No replay in Phase 7M2.",
    "No local replay in Phase 7M2.",
    "No external replay.",
    "No non-local health URL call.",
    "No POST endpoint.",
    "No snapshot endpoint.",
    "No replay endpoint.",
    "No API process auto-start.",
    "No scheduler or polling.",
    "No runtime shadow replay submit.",
    "No order path.",
    "No gateway registration.",
    "No trading-state mutation.",
    "No retry, batch, or loop.",
    "No wrapper relaxation.",
    "No external-run UI control.",
    "Do not reinterpret the timeout as an LMAX evidence failure.",
    "Do not reopen the closed evidence cycle."
)

$report = [ordered]@{
    phase = "7M2"
    generatedAtUtc = $generatedAt
    diagnosticType = "LocalApiHealthTimeoutDiagnostic"
    issue = "LocalhostApiHealthTimeoutAffectedOptionalReplayOnly"
    issueScope = "LocalApiHealthOptionalReplay"
    lmaxEvidenceFailure = $false
    sourcePhase7MPlanPath = $PlanFile
    sourcePhase7MGatePath = $GateFile
    externalRunAttemptedInThisPhase = $false
    snapshotRunInThisPhase = $false
    replayRunInThisPhase = $false
    localReplayRunInThisPhase = $false
    externalUrlCalled = $false
    onlyLocalhostHealthChecked = [bool]$urlSafe
    postEndpointCalled = $false
    mutationAttempted = $false
    schedulerOrPollingAdded = $false
    runtimeShadowReplaySubmitAdded = $false
    orderPathAdded = $false
    gatewayRegistrationAdded = $false
    tradingMutationAdded = $false
    retryBatchLoopAdded = $false
    wrapperValidationWeakened = $false
    apiWorkerRemainFakeLmaxGatewayOnly = $true
    configuredApiBaseUrlHost = if ($null -ne $uri) { $uri.Host } else { $null }
    configuredApiBaseUrlPort = $port
    healthEndpointPath = "/health"
    healthEndpointMethod = "GET"
    unsafeUrlRejectedReason = $unsafeReason
    healthCheckAttempted = $healthCheckAttempted
    healthCheckResult = $healthCheckResult
    healthCheckStatusCode = $healthStatusCode
    healthCheckElapsedMs = $healthCheckElapsedMs
    localPortListeningStatus = $localPortListeningStatus
    localApiProcessStatus = $localApiProcessStatus
    sanitizedHealthFields = $healthSubset
    timeoutClassification = $timeoutClassification
    likelyCauseCandidates = $likelyCauseCandidates
    recommendedNextAction = $recommendedNextAction
    allowedNextPhase = $allowedNextPhase
    disallowedActions = $disallowedActions
    noSensitiveContent = $true
    finalDecision = $finalDecision
}

$gate = [ordered]@{
    phase = "7M2"
    generatedAtUtc = $generatedAt
    gateType = "LocalApiHealthTimeoutDiagnosticGate"
    diagnosticReportPath = $ReportFile
    diagnosticCompleted = $true
    localOnly = $true
    externalRunAllowed = $false
    snapshotAllowed = $false
    replayAllowedInThisPhase = $false
    localReplayRunInThisPhase = $false
    externalRunAttemptedInThisPhase = $false
    snapshotRunInThisPhase = $false
    replayRunInThisPhase = $false
    externalUrlCalled = $false
    onlyLocalhostHealthChecked = [bool]$urlSafe
    postEndpointCalled = $false
    mutationAttempted = $false
    schedulerOrPollingEnabled = $false
    runtimeShadowReplaySubmitEnabled = $false
    orderPathEnabled = $false
    gatewayRegistrationEnabled = $false
    tradingMutationEnabled = $false
    retryBatchLoopAdded = $false
    wrapperValidationWeakened = $false
    apiWorkerRemainFakeLmaxGatewayOnly = $true
    lmaxEvidenceFailure = $false
    evidenceCycleRemainsClosed = $true
    uiStatusWorkstreamRemainsClosed = $true
    timeoutClassification = $timeoutClassification
    localApiHealthCheckAttempted = $healthCheckAttempted
    localApiHealthCheckResult = $healthCheckResult
    localPortListeningStatus = $localPortListeningStatus.status
    "directRunAuthorization" = $false
    anyInstrumentExternalRunAllowed = $false
    externalAdditionalInstrumentAttemptsCurrentlyAllowed = $false
    disallowedActions = $disallowedActions
    allowedNextPhase = $allowedNextPhase
    noSensitiveContent = $true
    finalDecision = $finalDecision
}

$note = @"
# Phase 7M2 - Local API Health Timeout Diagnostic

This diagnostic is local-only. It does not connect to LMAX, request a snapshot, run replay, call a POST endpoint, start the API, schedule work, submit orders, register a gateway, or mutate trading state.

## Result

- Issue: LocalhostApiHealthTimeoutAffectedOptionalReplayOnly
- LMAX evidence failure: false
- Health check attempted: $healthCheckAttempted
- Health check result: $healthCheckResult
- Timeout classification: $timeoutClassification
- Final decision: $finalDecision
- Allowed next phase: $allowedNextPhase

## Interpretation

$recommendedNextAction

The evidence cycle remains closed, the UI/status workstream remains closed, and API/Worker remain FakeLmaxGateway only. This diagnostic does not make local replay mandatory and does not reopen external LMAX attempts.
"@

Write-JsonFile $ReportFile $report
Write-JsonFile $DiagnosticGateFile $gate
Write-TextFile $NoteFile $note

Write-Host "Diagnostic report: $(Resolve-LocalPath $ReportFile)"
Write-Host "Diagnostic gate: $(Resolve-LocalPath $DiagnosticGateFile)"
Write-Host "Diagnostic note: $(Resolve-LocalPath $NoteFile)"
Write-Host "FinalDecision: $finalDecision"
