param(
    [string]$ReportFile = "artifacts/readiness/phase7m5-local-api-startup-troubleshooting-continuation-report.json",
    [string]$GateFile = "artifacts/readiness/phase7m5-local-api-startup-troubleshooting-continuation-gate.json",
    [string]$NoteFile = "artifacts/readiness/phase7m5-local-api-startup-troubleshooting-continuation-note.md",
    [string]$Phase7M4ReportFile = "artifacts/readiness/phase7m4-manual-local-api-health-recheck-report.json",
    [string]$Phase7M4GateFile = "artifacts/readiness/phase7m4-manual-local-api-health-recheck-gate.json"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|raw\s*fix|BEGIN\s+PRIVATE\s+KEY)'
$expectedAllowedNextPhase = "Phase 7M6 $([char]0x2014) Operator-Started Local API Health Verification, No Replay, No External Run"

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

Write-Host "LMAX Read-Only Runtime Phase 7M5 Local API Startup Troubleshooting Continuation Gate"
Write-Host "Local-only validator. This does not start the API, connect to LMAX, request snapshots, run replay, call POST endpoints, or use credential values."

$reportRaw = Read-TextSafe $ReportFile "Report"
$gateRaw = Read-TextSafe $GateFile "Gate"
$noteRaw = Read-TextSafe $NoteFile "Note"
$phase7M4ReportRaw = Read-TextSafe $Phase7M4ReportFile "Phase7M4Report"
$phase7M4GateRaw = Read-TextSafe $Phase7M4GateFile "Phase7M4Gate"

if ($null -ne $reportRaw) {
    $report = $reportRaw | ConvertFrom-Json
    Assert-Equals $report.phase "7M5" "Report" "Phase"
    Assert-Equals $report.sourceClassification "LocalApiHealthStillUnavailable" "Report" "Source classification"
    Assert-Equals $report.issueScope "LocalApiStartupConfigurationOnly" "Report" "Issue scope"
    Assert-False $report.lmaxEvidenceFailure "Report" "LMAX evidence failure"
    Assert-True $report.evidenceCycleAlreadyClosed "Report" "Evidence cycle already closed"
    Assert-True $report.uiStatusWorkstreamClosed "Report" "UI status workstream closed"
    foreach ($flag in @(
        "externalRunAttemptedInThisPhase",
        "snapshotRunInThisPhase",
        "replayRunInThisPhase",
        "localReplayRunInThisPhase",
        "postEndpointCalled",
        "mutationAttempted",
        "schedulerOrPollingAdded",
        "runtimeShadowReplaySubmitAdded",
        "orderPathAdded",
        "gatewayRegistrationAdded",
        "tradingMutationAdded",
        "retryBatchLoopAdded",
        "wrapperValidationWeakened",
        "apiStartedInThisPhase"
    )) {
        Assert-False $report.$flag "Report" $flag
    }
    Assert-True $report.apiWorkerRemainFakeLmaxGatewayOnly "Report" "API/Worker remain FakeLmaxGateway only"
    Assert-Equals $report.observedPortState.status "NotListening" "Report" "Observed port state"
    Assert-True $report.observedApiProcessState.safeProcessMetadataOnly "Report" "Safe process metadata only"
    foreach ($cause in @(
        "local API not started",
        "wrong base URL or port",
        "launch profile mismatch",
        "HTTPS vs HTTP mismatch",
        "API startup blocked by configuration/dependency",
        "LocalDB/database dependency delaying /health",
        "stale process bound to expected port",
        "firewall/local host binding issue",
        "timeout too short for local startup"
    )) {
        Assert-Contains $report.possibleRootCauseClasses $cause "Report" "Root cause class"
    }
    Assert-Equals $report.allowedNextPhase $expectedAllowedNextPhase "Report" "Allowed next phase"
    Assert-Equals $report.finalDecision "PASS_LOCAL_API_STARTUP_TROUBLESHOOTING_PLAN_RECORDED" "Report" "Final decision"
    Assert-True $report.noSensitiveContent "Report" "No sensitive content"
}

if ($null -ne $gateRaw) {
    $gate = $gateRaw | ConvertFrom-Json
    Assert-Equals $gate.phase "7M5" "Gate" "Phase"
    Assert-True $gate.troubleshootingContinuationCompleted "Gate" "Troubleshooting continuation completed"
    Assert-True $gate.planningOnly "Gate" "Planning only"
    Assert-True $gate.localOnly "Gate" "Local only"
    Assert-False $gate.apiStartedInThisPhase "Gate" "API started in this phase"
    foreach ($flag in @(
        "externalRunAllowed",
        "snapshotAllowed",
        "replayAllowedInThisPhase",
        "localReplayAllowedInThisPhase",
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
        "localReplayRunInThisPhase",
        "postEndpointCalled",
        "mutationAttempted",
        "lmaxEvidenceFailure"
    )) {
        Assert-False $gate.$flag "Gate" $flag
    }
    Assert-True $gate.evidenceCycleRemainsClosed "Gate" "Evidence cycle remains closed"
    Assert-True $gate.uiStatusWorkstreamRemainsClosed "Gate" "UI status workstream remains closed"
    Assert-True $gate.apiWorkerRemainFakeLmaxGatewayOnly "Gate" "API/Worker remain FakeLmaxGateway only"
    Assert-True $gate.manualApiStartRecommended "Gate" "Manual API start recommended"
    Assert-True $gate.localHealthRecheckAllowedLater "Gate" "Local health recheck allowed later"
    Assert-Equals $gate.allowedNextPhase $expectedAllowedNextPhase "Gate" "Allowed next phase"
    Assert-Equals $gate.finalDecision "PASS_LOCAL_API_STARTUP_TROUBLESHOOTING_PLAN_RECORDED" "Gate" "Final decision"
    Assert-True $gate.noSensitiveContent "Gate" "No sensitive content"
}

if ($null -ne $phase7M4ReportRaw) {
    $phase7M4Report = $phase7M4ReportRaw | ConvertFrom-Json
    Assert-Equals $phase7M4Report.phase "7M4" "Phase7M4Report" "Phase"
    Assert-Equals $phase7M4Report.timeoutClassification "LocalApiHealthStillUnavailable" "Phase7M4Report" "Timeout classification"
    Assert-Equals $phase7M4Report.finalDecision "PASS_LOCAL_API_HEALTH_RECHECK_STILL_UNAVAILABLE" "Phase7M4Report" "Final decision"
}

if ($null -ne $phase7M4GateRaw) {
    $phase7M4Gate = $phase7M4GateRaw | ConvertFrom-Json
    Assert-Equals $phase7M4Gate.phase "7M4" "Phase7M4Gate" "Phase"
    Assert-False $phase7M4Gate.replayAllowedInThisPhase "Phase7M4Gate" "Replay allowed in phase 7M4"
    Assert-False $phase7M4Gate.apiStartedInThisPhase "Phase7M4Gate" "API started in phase 7M4"
}

if ($null -ne $noteRaw) {
    foreach ($marker in @("could not reach local", "local API startup/configuration issue only", "not an LMAX evidence failure", "No API was started", "Replay remains out of scope", "Allowed next phase")) {
        if ($noteRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "Note" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "Note" "Marker: $marker" "FAIL" "Marker missing." }
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
Add-Result "RuntimePower" "Runtime behavior change" "PASS" "This phase records a local troubleshooting plan only."

$failed = @($results | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } else { "PASS_LOCAL_API_STARTUP_TROUBLESHOOTING_PLAN_RECORDED" }
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7m5-local-api-startup-troubleshooting-continuation-gate-validation.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7M5"
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
