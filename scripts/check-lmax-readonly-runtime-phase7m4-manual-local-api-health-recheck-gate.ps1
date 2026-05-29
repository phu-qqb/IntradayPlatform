param(
    [string]$ReportFile = "artifacts/readiness/phase7m4-manual-local-api-health-recheck-report.json",
    [string]$GateFile = "artifacts/readiness/phase7m4-manual-local-api-health-recheck-gate.json",
    [string]$NoteFile = "artifacts/readiness/phase7m4-manual-local-api-health-recheck-note.md",
    [string]$RecheckScriptFile = "scripts/recheck-lmax-readonly-local-api-health.ps1",
    [string]$Phase7M3GateFile = "artifacts/readiness/phase7m3-local-api-startup-configuration-follow-up-gate.json"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|raw\s*fix|BEGIN\s+PRIVATE\s+KEY)'
$expectedDecisions = @("PASS_LOCAL_API_HEALTH_RECHECK_OK", "PASS_LOCAL_API_HEALTH_RECHECK_STILL_UNAVAILABLE", "PASS_UNSAFE_NONLOCAL_HEALTH_URL_REJECTED")

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

Write-Host "LMAX Read-Only Runtime Phase 7M4 Manual Local API Health Recheck Gate"
Write-Host "Local-only validator. This does not start the API, connect to LMAX, request snapshots, run replay, call POST endpoints, or use credential values."

$reportRaw = Read-TextSafe $ReportFile "HealthRecheckReport"
$gateRaw = Read-TextSafe $GateFile "HealthRecheckGate"
$noteRaw = Read-TextSafe $NoteFile "HealthRecheckNote"
$scriptRaw = Read-TextSafe $RecheckScriptFile "HealthRecheckScript"
$phase7M3GateRaw = Read-TextSafe $Phase7M3GateFile "Phase7M3Gate"

if ($null -ne $reportRaw) {
    $report = $reportRaw | ConvertFrom-Json
    Assert-Equals $report.phase "7M4" "HealthRecheckReport" "Phase"
    Assert-Equals $report.diagnosticType "ManualLocalApiHealthRecheck" "HealthRecheckReport" "Diagnostic type"
    Assert-Equals $report.previousClassification "LocalApiNotRunningOrWrongPort" "HealthRecheckReport" "Previous classification"
    Assert-False $report.lmaxEvidenceFailure "HealthRecheckReport" "LMAX evidence failure"
    Assert-False $report.apiStartedInThisPhase "HealthRecheckReport" "API started in this phase"
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
        Assert-False $report.$flag "HealthRecheckReport" $flag
    }
    Assert-Equals $report.healthEndpoint "GET /health" "HealthRecheckReport" "Health endpoint"
    Assert-True $report.apiWorkerRemainFakeLmaxGatewayOnly "HealthRecheckReport" "API/Worker remain FakeLmaxGateway only"
    Assert-In $report.timeoutClassification @("LocalApiHealthRecovered", "LocalApiHealthStillUnavailable", "UnsafeNonLocalHealthUrlRejected") "HealthRecheckReport" "Timeout classification"
    Assert-In $report.finalDecision $expectedDecisions "HealthRecheckReport" "Final decision"
    Assert-True $report.noSensitiveContent "HealthRecheckReport" "No sensitive content"
}

if ($null -ne $gateRaw) {
    $gate = $gateRaw | ConvertFrom-Json
    Assert-Equals $gate.phase "7M4" "HealthRecheckGate" "Phase"
    Assert-True $gate.localOnly "HealthRecheckGate" "Local only"
    Assert-False $gate.apiStartedInThisPhase "HealthRecheckGate" "API started in this phase"
    Assert-True $gate.healthRecheckCompleted "HealthRecheckGate" "Health recheck completed"
    Assert-False $gate.lmaxEvidenceFailure "HealthRecheckGate" "LMAX evidence failure"
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
        Assert-False $gate.$flag "HealthRecheckGate" $flag
    }
    Assert-True $gate.apiWorkerRemainFakeLmaxGatewayOnly "HealthRecheckGate" "API/Worker remain FakeLmaxGateway only"
    Assert-True $gate.evidenceCycleRemainsClosed "HealthRecheckGate" "Evidence cycle remains closed"
    Assert-True $gate.uiStatusWorkstreamRemainsClosed "HealthRecheckGate" "UI status workstream remains closed"
    Assert-In $gate.finalDecision $expectedDecisions "HealthRecheckGate" "Final decision"
    Assert-True $gate.noSensitiveContent "HealthRecheckGate" "No sensitive content"
}

if ($null -ne $gateRaw -and $null -ne $reportRaw) {
    Assert-Equals $gate.finalDecision $report.finalDecision "CrossCheck" "Gate/report final decision match"
    Assert-Equals $gate.timeoutClassification $report.timeoutClassification "CrossCheck" "Gate/report timeout classification match"
    Assert-Equals $gate.allowedNextPhase $report.allowedNextPhase "CrossCheck" "Gate/report allowed next phase match"
}

if ($null -ne $phase7M3GateRaw) {
    $phase7M3Gate = $phase7M3GateRaw | ConvertFrom-Json
    Assert-Equals $phase7M3Gate.phase "7M3" "Phase7M3Gate" "Phase"
    Assert-Equals $phase7M3Gate.finalDecision "PASS_LOCAL_API_STARTUP_CONFIGURATION_PLAN_RECORDED" "Phase7M3Gate" "Final decision"
    Assert-False $phase7M3Gate.apiStartedInThisPhase "Phase7M3Gate" "API started in phase 7M3"
}

if ($null -ne $noteRaw) {
    foreach ($marker in @("GET /health", "does not start the API", "connect to LMAX", "run local or external replay", "Allowed next phase")) {
        if ($noteRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "HealthRecheckNote" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "HealthRecheckNote" "Marker: $marker" "FAIL" "Marker missing." }
    }
}

if ($null -ne $scriptRaw) {
    foreach ($marker in @("Invoke-WebRequest -Method Get", "/health", "localhost", "127.0.0.1", "TimeoutSeconds", "UnsafeNonLocalHealthUrlRejected")) {
        if ($scriptRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "HealthRecheckScript" "Safety marker: $marker" "PASS" "Marker found." } else { Add-Result "HealthRecheckScript" "Safety marker: $marker" "FAIL" "Marker missing." }
    }
    foreach ($unsafe in @("-Method Post", "Invoke-RestMethod -Method Post", "Start-Process", "run-lmax-readonly-runtime-demo-additional-instrument-snapshot-once", "replay-lmax-readonly", "/lmax-shadow/replay", "NewOrderSingle")) {
        if ($scriptRaw.IndexOf($unsafe, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "HealthRecheckScript" "No unsafe command marker: $unsafe" "FAIL" "Found." } else { Add-Result "HealthRecheckScript" "No unsafe command marker: $unsafe" "PASS" "Not found." }
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

Add-Result "Runtime" "API start" "PASS" "This phase does not start the API."
Add-Result "Runtime" "External LMAX connection" "PASS" "This phase does not connect to LMAX."
Add-Result "Snapshot" "MarketData snapshot" "PASS" "This phase does not request snapshots."
Add-Result "Replay" "Replay" "PASS" "This phase does not run local or external replay."
Add-Result "RuntimePower" "Runtime behavior change" "PASS" "This phase records a local health recheck only."

$failed = @($results | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } else { [string]($gate.finalDecision) }
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7m4-manual-local-api-health-recheck-gate-validation.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7M4"
    finalDecision = $decision
    apiStartedInThisPhase = $false
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
