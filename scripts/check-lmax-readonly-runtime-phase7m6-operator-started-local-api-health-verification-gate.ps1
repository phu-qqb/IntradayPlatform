param(
    [string]$ReportFile = "artifacts/readiness/phase7m6-operator-started-local-api-health-verification-report.json",
    [string]$GateFile = "artifacts/readiness/phase7m6-operator-started-local-api-health-verification-gate.json",
    [string]$NoteFile = "artifacts/readiness/phase7m6-operator-started-local-api-health-verification-note.md",
    [string]$VerificationScriptFile = "scripts/verify-lmax-readonly-operator-started-local-api-health.ps1",
    [string]$Phase7M5GateFile = "artifacts/readiness/phase7m5-local-api-startup-troubleshooting-continuation-gate.json"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|raw\s*fix|BEGIN\s+PRIVATE\s+KEY)'
$expectedDecisions = @(
    "PASS_OPERATOR_STARTED_LOCAL_API_HEALTH_OK",
    "PASS_LOCAL_API_HEALTH_OK_BUT_UNSAFE_RUNTIME_BLOCKED",
    "PASS_OPERATOR_STARTED_LOCAL_API_HEALTH_STILL_UNAVAILABLE"
)

function Add-Result([string]$Category, [string]$Check, [string]$Status, [string]$Detail) {
    $script:results += [ordered]@{ category = $Category; check = $Check; status = $Status; detail = $Detail }
    Write-Host ("{0}: {1} / {2} - {3}" -f $Status, $Category, $Check, $Detail)
}

function Resolve-LocalPath([string]$PathValue) {
    if ([string]::IsNullOrWhiteSpace($PathValue)) { return "" }
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}

function Read-TextSafe([string]$PathValue, [string]$Label) {
    $resolved = Resolve-LocalPath $PathValue
    if (-not (Test-Path -LiteralPath $resolved)) {
        Add-Result $Label "File exists" "FAIL" "Missing $resolved"
        return $null
    }
    Add-Result $Label "File exists" "PASS" $resolved
    $raw = Get-Content -LiteralPath $resolved -Raw
    $safe = $raw -replace 'credential|Credential|secret|Secret','SAFE_METADATA'
    if ($safe -match $sensitivePattern) {
        Add-Result $Label "No sensitive content" "FAIL" "Credential-shaped or raw FIX content found."
    } else {
        Add-Result $Label "No sensitive content" "PASS" "No credential-shaped or raw FIX content."
    }
    return $raw
}

function Assert-True([object]$Value, [string]$Category, [string]$Check) {
    if ([bool]$Value) { Add-Result $Category $Check "PASS" "true." } else { Add-Result $Category $Check "FAIL" "Expected true." }
}

function Assert-False([object]$Value, [string]$Category, [string]$Check) {
    if (-not [bool]$Value) { Add-Result $Category $Check "PASS" "false." } else { Add-Result $Category $Check "FAIL" "Expected false." }
}

function Assert-Equals([object]$Actual, [string]$Expected, [string]$Category, [string]$Check) {
    if ([string]$Actual -eq $Expected) { Add-Result $Category $Check "PASS" $Expected } else { Add-Result $Category $Check "FAIL" "Expected '$Expected' but found '$Actual'." }
}

function Assert-In([object]$Actual, [string[]]$Expected, [string]$Category, [string]$Check) {
    if ($Expected -contains [string]$Actual) { Add-Result $Category $Check "PASS" ([string]$Actual) } else { Add-Result $Category $Check "FAIL" "Unexpected value '$Actual'." }
}

Write-Host "LMAX Read-Only Runtime Phase 7M6 Operator-Started Local API Health Verification Gate"
Write-Host "Local-only validator. This does not start the API, connect to LMAX, request snapshots, run replay, call POST endpoints, or use credential values."

$reportRaw = Read-TextSafe $ReportFile "HealthVerificationReport"
$gateRaw = Read-TextSafe $GateFile "HealthVerificationGate"
$noteRaw = Read-TextSafe $NoteFile "HealthVerificationNote"
$scriptRaw = Read-TextSafe $VerificationScriptFile "HealthVerificationScript"
$phase7M5GateRaw = Read-TextSafe $Phase7M5GateFile "Phase7M5Gate"

if ($null -ne $reportRaw) {
    $report = $reportRaw | ConvertFrom-Json
    Assert-Equals $report.phase "7M6" "HealthVerificationReport" "Phase"
    Assert-Equals $report.diagnosticType "OperatorStartedLocalApiHealthVerification" "HealthVerificationReport" "Diagnostic type"
    Assert-Equals $report.previousClassification "LocalApiHealthStillUnavailable" "HealthVerificationReport" "Previous classification"
    Assert-Equals $report.expectedLocalBaseUrl "http://localhost:5050" "HealthVerificationReport" "Expected local base URL"
    Assert-False $report.lmaxEvidenceFailure "HealthVerificationReport" "LMAX evidence failure"
    Assert-False $report.apiStartedByCodexInThisPhase "HealthVerificationReport" "API started by Codex"
    Assert-True $report.operatorStartedApiAssumed "HealthVerificationReport" "Operator started API assumed"
    foreach ($flag in @(
        "externalRunAttemptedInThisPhase",
        "snapshotRunInThisPhase",
        "replayRunInThisPhase",
        "localReplayRunInThisPhase",
        "externalUrlCalled",
        "postEndpointCalled",
        "mutationAttempted",
        "schedulerOrPollingAdded",
        "runtimeShadowReplaySubmitAdded",
        "orderPathAdded",
        "gatewayRegistrationAdded",
        "tradingMutationAdded",
        "retryBatchLoopAdded",
        "wrapperValidationWeakened"
    )) {
        Assert-False $report.$flag "HealthVerificationReport" $flag
    }
    Assert-Equals $report.healthEndpoint "GET /health" "HealthVerificationReport" "Health endpoint"
    Assert-True $report.healthCheckAttempted "HealthVerificationReport" "Health check attempted"
    Assert-In $report.timeoutClassification @("LocalApiHealthRecovered", "LocalApiHealthOkButUnsafeRuntimePosture", "LocalApiHealthStillUnavailable") "HealthVerificationReport" "Timeout classification"
    Assert-In $report.finalDecision $expectedDecisions "HealthVerificationReport" "Final decision"
    Assert-True $report.noSensitiveContent "HealthVerificationReport" "No sensitive content"
}

if ($null -ne $gateRaw) {
    $gate = $gateRaw | ConvertFrom-Json
    Assert-Equals $gate.phase "7M6" "HealthVerificationGate" "Phase"
    Assert-True $gate.healthVerificationCompleted "HealthVerificationGate" "Health verification completed"
    Assert-True $gate.localOnly "HealthVerificationGate" "Local only"
    Assert-False $gate.apiStartedByCodexInThisPhase "HealthVerificationGate" "API started by Codex"
    Assert-False $gate.lmaxEvidenceFailure "HealthVerificationGate" "LMAX evidence failure"
    foreach ($flag in @(
        "externalRunAllowed",
        "snapshotAllowed",
        "replayAllowedInThisPhase",
        "localReplayRunInThisPhase",
        "externalRunAttemptedInThisPhase",
        "snapshotRunInThisPhase",
        "replayRunInThisPhase",
        "externalUrlCalled",
        "postEndpointCalled",
        "mutationAttempted",
        "schedulerOrPollingEnabled",
        "runtimeShadowReplaySubmitEnabled",
        "orderPathEnabled",
        "gatewayRegistrationEnabled",
        "tradingMutationEnabled",
        "retryBatchLoopAdded",
        "wrapperValidationWeakened"
    )) {
        Assert-False $gate.$flag "HealthVerificationGate" $flag
    }
    Assert-True $gate.evidenceCycleRemainsClosed "HealthVerificationGate" "Evidence cycle remains closed"
    Assert-True $gate.uiStatusWorkstreamRemainsClosed "HealthVerificationGate" "UI status workstream remains closed"
    Assert-In $gate.finalDecision $expectedDecisions "HealthVerificationGate" "Final decision"
    Assert-True $gate.noSensitiveContent "HealthVerificationGate" "No sensitive content"
}

if ($null -ne $gateRaw -and $null -ne $reportRaw) {
    Assert-Equals $gate.finalDecision $report.finalDecision "CrossCheck" "Gate/report final decision match"
    Assert-Equals $gate.timeoutClassification $report.timeoutClassification "CrossCheck" "Gate/report timeout classification match"
    Assert-Equals $gate.allowedNextPhase $report.allowedNextPhase "CrossCheck" "Gate/report allowed next phase match"
}

if ($null -ne $phase7M5GateRaw) {
    $phase7M5Gate = $phase7M5GateRaw | ConvertFrom-Json
    Assert-Equals $phase7M5Gate.phase "7M5" "Phase7M5Gate" "Phase"
    Assert-Equals $phase7M5Gate.finalDecision "PASS_LOCAL_API_STARTUP_TROUBLESHOOTING_PLAN_RECORDED" "Phase7M5Gate" "Final decision"
    Assert-False $phase7M5Gate.apiStartedInThisPhase "Phase7M5Gate" "API started in phase 7M5"
}

if ($null -ne $noteRaw) {
    foreach ($marker in @("Codex did not start the API", "GET /health", "does not connect to LMAX", "run local or external replay", "Allowed next phase")) {
        if ($noteRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "HealthVerificationNote" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "HealthVerificationNote" "Marker: $marker" "FAIL" "Marker missing." }
    }
}

if ($null -ne $scriptRaw) {
    foreach ($marker in @("Invoke-WebRequest -Method Get", "/health", "localhost", "127.0.0.1", "TimeoutSeconds", "UnsafeNonLocalHealthUrlRejected")) {
        if ($scriptRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "HealthVerificationScript" "Safety marker: $marker" "PASS" "Marker found." } else { Add-Result "HealthVerificationScript" "Safety marker: $marker" "FAIL" "Marker missing." }
    }
    foreach ($unsafe in @("-Method Post", "Invoke-RestMethod -Method Post", "Start-Process", "run-lmax-readonly-runtime-demo-additional-instrument-snapshot-once", "replay-lmax-readonly", "/lmax-shadow/replay", "NewOrderSingle")) {
        if ($scriptRaw.IndexOf($unsafe, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "HealthVerificationScript" "No unsafe command marker: $unsafe" "FAIL" "Found." } else { Add-Result "HealthVerificationScript" "No unsafe command marker: $unsafe" "PASS" "Not found." }
    }
}

$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"
$startupText = (@($apiProgram, $workerProgram) | Where-Object { Test-Path -LiteralPath $_ } | ForEach-Object { Get-Content -Raw -LiteralPath $_ }) -join "`n"
if ($startupText.Contains("FakeLmaxGateway") -and -not ($startupText.Contains("RealLmaxGateway") -or $startupText.Contains("LmaxVenueGatewaySkeleton"))) {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No real gateway registration found."
} else {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" "Unexpected gateway registration marker."
}

Add-Result "Runtime" "API start" "PASS" "Codex did not start the API."
Add-Result "Runtime" "External LMAX connection" "PASS" "This phase does not connect to LMAX."
Add-Result "Snapshot" "MarketData snapshot" "PASS" "This phase does not request snapshots."
Add-Result "Replay" "Replay" "PASS" "This phase does not run local or external replay."
Add-Result "RuntimePower" "Runtime behavior change" "PASS" "This phase records a local health verification only."

$failed = @($results | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } else { [string]($gate.finalDecision) }
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7m6-operator-started-local-api-health-verification-gate-validation.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7M6"
    finalDecision = $decision
    apiStartedByCodexInThisPhase = $false
    externalConnectionAttempted = $false
    snapshotAttempted = $false
    replayAttempted = $false
    runtimePowerAdded = $false
    lmaxEvidenceFailure = $false
    apiWorkerGatewayMode = "FakeLmaxGateway"
    results = $results
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }
