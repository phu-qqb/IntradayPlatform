param(
    [string]$PlanFile = "artifacts/readiness/phase7m3-local-api-startup-configuration-follow-up-plan.json",
    [string]$GateFile = "artifacts/readiness/phase7m3-local-api-startup-configuration-follow-up-gate.json",
    [string]$NoteFile = "artifacts/readiness/phase7m3-local-api-startup-configuration-follow-up-note.md",
    [string]$Phase7M2ReportFile = "artifacts/readiness/phase7m2-local-api-health-timeout-diagnostic-report.json",
    [string]$Phase7M2GateFile = "artifacts/readiness/phase7m2-local-api-health-timeout-diagnostic-gate.json"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|raw\s*fix|BEGIN\s+PRIVATE\s+KEY)'

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

function Assert-Contains($Values, [string]$Expected, [string]$Category, [string]$Check) {
    if (@($Values) -contains $Expected) { Add-Result $Category $Check "PASS" "Contains $Expected." } else { Add-Result $Category $Check "FAIL" "Missing $Expected." }
}

Write-Host "LMAX Read-Only Runtime Phase 7M3 Local API Startup/Configuration Follow-Up Gate"
Write-Host "Local-only validator. This does not start the API, connect to LMAX, request snapshots, run replay, call POST endpoints, or use credential values."

$planRaw = Read-TextSafe $PlanFile "Plan"
$gateRaw = Read-TextSafe $GateFile "Gate"
$noteRaw = Read-TextSafe $NoteFile "Note"
$phase7M2ReportRaw = Read-TextSafe $Phase7M2ReportFile "Phase7M2Report"
$phase7M2GateRaw = Read-TextSafe $Phase7M2GateFile "Phase7M2Gate"

if ($null -ne $planRaw) {
    $plan = $planRaw | ConvertFrom-Json
    Assert-Equals $plan.phase "7M3" "Plan" "Phase"
    Assert-Equals $plan.sourceClassification "LocalApiNotRunningOrWrongPort" "Plan" "Source classification"
    Assert-Equals $plan.issueScope "LocalApiStartupConfigurationOnly" "Plan" "Issue scope"
    Assert-False $plan.lmaxEvidenceFailure "Plan" "LMAX evidence failure"
    Assert-True $plan.evidenceCycleAlreadyClosed "Plan" "Evidence cycle already closed"
    Assert-True $plan.uiStatusWorkstreamClosed "Plan" "UI status workstream closed"
    Assert-True $plan.planningOnly "Plan" "Planning only"
    foreach ($flag in @(
        "apiStartedInThisPhase",
        "externalRunAttemptedInThisPhase",
        "snapshotRunInThisPhase",
        "replayRunInThisPhase",
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
        Assert-False $plan.$flag "Plan" $flag
    }
    Assert-True $plan.apiWorkerRemainFakeLmaxGatewayOnly "Plan" "API/Worker remain FakeLmaxGateway only"
    foreach ($item in @(
        "Identify the correct local API project: src/QQ.Production.Intraday.Api.",
        "Identify the expected local base URL and port: http://localhost:5050.",
        "Start the API manually only in a later operator-controlled step.",
        "Confirm /health with GET only.",
        "Do not call replay endpoints yet.",
        "Do not call POST endpoints.",
        "Confirm executionGateway remains FakeLmaxGateway.",
        "Confirm liveTradingEnabled=false.",
        "Confirm externalConnectionsEnabled=false."
    )) {
        Assert-Contains $plan.safeManualStartupChecklist $item "Plan" "Startup checklist item"
    }
    Assert-Equals $plan.allowedNextPhase "Phase 7M4 - Manual Local API Health Recheck, No Replay, No External Run" "Plan" "Allowed next phase"
    Assert-Equals $plan.finalDecision "PASS_LOCAL_API_STARTUP_CONFIGURATION_PLAN_RECORDED" "Plan" "Final decision"
    Assert-True $plan.noSensitiveContent "Plan" "No sensitive content"
}

if ($null -ne $gateRaw) {
    $gate = $gateRaw | ConvertFrom-Json
    Assert-Equals $gate.phase "7M3" "Gate" "Phase"
    Assert-True $gate.planningOnly "Gate" "Planning only"
    Assert-True $gate.apiStartupPlanned "Gate" "API startup planned"
    Assert-False $gate.apiStartedInThisPhase "Gate" "API started in this phase"
    Assert-True $gate.localHealthRecheckAllowedLater "Gate" "Local health recheck allowed later"
    foreach ($flag in @(
        "localReplayAllowedInThisPhase",
        "replayAllowedInThisPhase",
        "externalRunAllowed",
        "snapshotAllowed",
        "postEndpointAllowed",
        "mutationAllowed",
        "schedulerOrPollingEnabled",
        "runtimeShadowReplaySubmitEnabled",
        "orderPathEnabled",
        "gatewayRegistrationEnabled",
        "tradingMutationEnabled",
        "retryBatchLoopAdded",
        "wrapperValidationWeakened",
        "externalRunAttemptedInThisPhase",
        "snapshotRunInThisPhase",
        "replayRunInThisPhase",
        "postEndpointCalled",
        "mutationAttempted",
        "lmaxEvidenceFailure"
    )) {
        Assert-False $gate.$flag "Gate" $flag
    }
    Assert-True $gate.evidenceCycleRemainsClosed "Gate" "Evidence cycle remains closed"
    Assert-True $gate.uiStatusWorkstreamRemainsClosed "Gate" "UI status workstream remains closed"
    Assert-True $gate.apiWorkerRemainFakeLmaxGatewayOnly "Gate" "API/Worker remain FakeLmaxGateway only"
    Assert-Equals $gate.allowedNextPhase "Phase 7M4 - Manual Local API Health Recheck, No Replay, No External Run" "Gate" "Allowed next phase"
    Assert-Equals $gate.finalDecision "PASS_LOCAL_API_STARTUP_CONFIGURATION_PLAN_RECORDED" "Gate" "Final decision"
    Assert-True $gate.noSensitiveContent "Gate" "No sensitive content"
}

if ($null -ne $phase7M2ReportRaw) {
    $phase7M2Report = $phase7M2ReportRaw | ConvertFrom-Json
    Assert-Equals $phase7M2Report.phase "7M2" "Phase7M2Report" "Phase"
    Assert-Equals $phase7M2Report.timeoutClassification "LocalApiNotRunningOrWrongPort" "Phase7M2Report" "Timeout classification"
    Assert-Equals $phase7M2Report.finalDecision "PASS_LOCAL_API_NOT_RUNNING_OR_WRONG_PORT" "Phase7M2Report" "Final decision"
}

if ($null -ne $phase7M2GateRaw) {
    $phase7M2Gate = $phase7M2GateRaw | ConvertFrom-Json
    Assert-Equals $phase7M2Gate.phase "7M2" "Phase7M2Gate" "Phase"
    Assert-Equals $phase7M2Gate.timeoutClassification "LocalApiNotRunningOrWrongPort" "Phase7M2Gate" "Timeout classification"
    Assert-False $phase7M2Gate.replayAllowedInThisPhase "Phase7M2Gate" "Replay allowed in phase 7M2"
}

if ($null -ne $noteRaw) {
    foreach ($marker in @("not reachable", "local API startup/configuration issue only", "does not invalidate", "does not start the API", "GET-only", "Replay remains out of scope", "Allowed next phase")) {
        if ($noteRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "Note" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "Note" "Marker: $marker" "FAIL" "Marker missing." }
    }
}

$runApi = Join-Path $repoRoot "scripts/run-api.ps1"
$launchSettings = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Properties/launchSettings.json"
if (Test-Path -LiteralPath $runApi) {
    $runApiText = Get-Content -LiteralPath $runApi -Raw
    if ($runApiText.Contains("dotnet run --project .\src\QQ.Production.Intraday.Api")) { Add-Result "StartupHints" "run-api.ps1 API project hint" "PASS" "Expected project start hint found." } else { Add-Result "StartupHints" "run-api.ps1 API project hint" "FAIL" "Expected project start hint missing." }
}
if (Test-Path -LiteralPath $launchSettings) {
    $launchText = Get-Content -LiteralPath $launchSettings -Raw
    if ($launchText.Contains("http://localhost:5050")) { Add-Result "StartupHints" "launchSettings local URL" "PASS" "http://localhost:5050 found." } else { Add-Result "StartupHints" "launchSettings local URL" "FAIL" "http://localhost:5050 missing." }
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
Add-Result "RuntimePower" "Runtime behavior change" "PASS" "This phase records a local planning gate only."

$failed = @($results | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } else { "PASS_LOCAL_API_STARTUP_CONFIGURATION_PLAN_RECORDED" }
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7m3-local-api-startup-configuration-follow-up-gate-validation.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7M3"
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
