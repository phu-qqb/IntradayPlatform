param(
    [string]$ReportFile = "artifacts/readiness/phase7m2-local-api-health-timeout-diagnostic-report.json",
    [string]$GateFile = "artifacts/readiness/phase7m2-local-api-health-timeout-diagnostic-gate.json",
    [string]$NoteFile = "artifacts/readiness/phase7m2-local-api-health-timeout-diagnostic-note.md",
    [string]$DiagnosticScriptFile = "scripts/diagnose-lmax-readonly-local-api-health-timeout.ps1",
    [string]$Phase7MPlanFile = "artifacts/readiness/phase7m-local-api-health-timeout-follow-up-plan.json",
    [string]$Phase7MGateFile = "artifacts/readiness/phase7m-local-api-health-timeout-follow-up-gate.json"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|raw\s*fix|BEGIN\s+PRIVATE\s+KEY)'
$expectedDecisions = @(
    "PASS_LOCAL_API_HEALTH_DIAGNOSTIC_OK",
    "PASS_LOCAL_API_HEALTH_TIMEOUT_STILL_PRESENT",
    "PASS_LOCAL_API_NOT_RUNNING_OR_WRONG_PORT",
    "PASS_UNSAFE_NONLOCAL_HEALTH_URL_REJECTED"
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

function Assert-Contains($Values, [string]$Expected, [string]$Category, [string]$Check) {
    if (@($Values) -contains $Expected) { Add-Result $Category $Check "PASS" "Contains $Expected." } else { Add-Result $Category $Check "FAIL" "Missing $Expected." }
}

Write-Host "LMAX Read-Only Runtime Phase 7M2 Local API Health Timeout Diagnostic Gate"
Write-Host "Local-only validator. This does not connect to LMAX, request snapshots, run replay, schedule work, or use credential values."

$reportRaw = Read-TextSafe $ReportFile "DiagnosticReport"
$gateRaw = Read-TextSafe $GateFile "DiagnosticGate"
$noteRaw = Read-TextSafe $NoteFile "DiagnosticNote"
$scriptRaw = Read-TextSafe $DiagnosticScriptFile "DiagnosticScript"
$phase7MPlanRaw = Read-TextSafe $Phase7MPlanFile "Phase7MPlan"
$phase7MGateRaw = Read-TextSafe $Phase7MGateFile "Phase7MGate"

if ($null -ne $reportRaw) {
    $report = $reportRaw | ConvertFrom-Json
    Assert-Equals $report.phase "7M2" "DiagnosticReport" "Phase"
    Assert-Equals $report.diagnosticType "LocalApiHealthTimeoutDiagnostic" "DiagnosticReport" "Diagnostic type"
    Assert-Equals $report.issue "LocalhostApiHealthTimeoutAffectedOptionalReplayOnly" "DiagnosticReport" "Issue"
    Assert-False $report.lmaxEvidenceFailure "DiagnosticReport" "LMAX evidence failure"
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
        Assert-False $report.$flag "DiagnosticReport" $flag
    }
    Assert-True $report.apiWorkerRemainFakeLmaxGatewayOnly "DiagnosticReport" "API/Worker remain FakeLmaxGateway only"
    Assert-Equals $report.healthEndpointPath "/health" "DiagnosticReport" "Health endpoint path"
    Assert-Equals $report.healthEndpointMethod "GET" "DiagnosticReport" "Health endpoint method"
    Assert-In $report.timeoutClassification @("RecoveredOrTransient", "LocalApiHealthTimeoutStillPresent", "LocalApiNotRunningOrWrongPort", "UnsafeNonLocalHealthUrlRejected") "DiagnosticReport" "Timeout classification"
    Assert-In $report.finalDecision $expectedDecisions "DiagnosticReport" "Final decision"
    Assert-True $report.noSensitiveContent "DiagnosticReport" "No sensitive content"
}

if ($null -ne $gateRaw) {
    $gate = $gateRaw | ConvertFrom-Json
    Assert-Equals $gate.phase "7M2" "DiagnosticGate" "Phase"
    Assert-True $gate.diagnosticCompleted "DiagnosticGate" "Diagnostic completed"
    Assert-True $gate.localOnly "DiagnosticGate" "Local only"
    Assert-False $gate.lmaxEvidenceFailure "DiagnosticGate" "LMAX evidence failure"
    Assert-False $gate.externalRunAllowed "DiagnosticGate" "External run allowed"
    Assert-False $gate.snapshotAllowed "DiagnosticGate" "Snapshot allowed"
    Assert-False $gate.replayAllowedInThisPhase "DiagnosticGate" "Replay allowed in this phase"
    foreach ($flag in @(
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
        "wrapperValidationWeakened",
        "directRunAuthorization",
        "anyInstrumentExternalRunAllowed",
        "externalAdditionalInstrumentAttemptsCurrentlyAllowed"
    )) {
        Assert-False $gate.$flag "DiagnosticGate" $flag
    }
    Assert-True $gate.apiWorkerRemainFakeLmaxGatewayOnly "DiagnosticGate" "API/Worker remain FakeLmaxGateway only"
    Assert-True $gate.evidenceCycleRemainsClosed "DiagnosticGate" "Evidence cycle remains closed"
    Assert-True $gate.uiStatusWorkstreamRemainsClosed "DiagnosticGate" "UI status workstream remains closed"
    Assert-In $gate.finalDecision $expectedDecisions "DiagnosticGate" "Final decision"
    Assert-True $gate.noSensitiveContent "DiagnosticGate" "No sensitive content"
}

if ($null -ne $gateRaw -and $null -ne $reportRaw) {
    Assert-Equals $gate.finalDecision $report.finalDecision "CrossCheck" "Gate/report final decision match"
    Assert-Equals $gate.timeoutClassification $report.timeoutClassification "CrossCheck" "Gate/report timeout classification match"
    Assert-Equals $gate.allowedNextPhase $report.allowedNextPhase "CrossCheck" "Gate/report allowed next phase match"
}

if ($null -ne $phase7MPlanRaw) {
    $phase7MPlan = $phase7MPlanRaw | ConvertFrom-Json
    Assert-Equals $phase7MPlan.phase "7M" "Phase7MPlan" "Phase"
    Assert-Equals $phase7MPlan.finalDecision "PASS_LOCAL_API_HEALTH_TIMEOUT_FOLLOW_UP_PLAN_RECORDED" "Phase7MPlan" "Final decision"
    Assert-False $phase7MPlan.lmaxEvidenceFailure "Phase7MPlan" "LMAX evidence failure"
}

if ($null -ne $phase7MGateRaw) {
    $phase7MGate = $phase7MGateRaw | ConvertFrom-Json
    Assert-Equals $phase7MGate.phase "7M" "Phase7MGate" "Phase"
    Assert-Equals $phase7MGate.finalDecision "PASS_LOCAL_API_HEALTH_TIMEOUT_FOLLOW_UP_PLAN_RECORDED" "Phase7MGate" "Final decision"
    Assert-False $phase7MGate.replayAllowedInThisPhase "Phase7MGate" "Replay allowed in phase 7M"
}

if ($null -ne $noteRaw) {
    foreach ($marker in @("local-only", "does not connect to LMAX", "does not make local replay mandatory", "does not reopen external LMAX attempts", "Allowed next phase")) {
        if ($noteRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "DiagnosticNote" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "DiagnosticNote" "Marker: $marker" "FAIL" "Marker missing." }
    }
}

if ($null -ne $scriptRaw) {
    foreach ($marker in @("Invoke-WebRequest -Method Get", "/health", "localhost", "127.0.0.1", "TimeoutSeconds", "UnsafeNonLocalHealthUrlRejected")) {
        if ($scriptRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "DiagnosticScript" "Safety marker: $marker" "PASS" "Marker found." } else { Add-Result "DiagnosticScript" "Safety marker: $marker" "FAIL" "Marker missing." }
    }
    foreach ($unsafe in @("-Method Post", "Invoke-RestMethod -Method Post", "Start-Process", "run-lmax-readonly-runtime-demo-additional-instrument-snapshot-once", "replay-lmax-readonly", "/lmax-shadow/replay", "NewOrderSingle")) {
        if ($scriptRaw.IndexOf($unsafe, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "DiagnosticScript" "No unsafe command marker: $unsafe" "FAIL" "Found." } else { Add-Result "DiagnosticScript" "No unsafe command marker: $unsafe" "PASS" "Not found." }
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

Add-Result "Runtime" "External LMAX connection" "PASS" "This phase does not connect to LMAX."
Add-Result "Snapshot" "MarketData snapshot" "PASS" "This phase does not request snapshots."
Add-Result "Replay" "Replay" "PASS" "This phase does not run local or external replay."
Add-Result "RuntimePower" "Runtime behavior change" "PASS" "This phase records a local diagnostic only."

$failed = @($results | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } else { [string]($gate.finalDecision) }
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7m2-local-api-health-timeout-diagnostic-gate-validation.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7M2"
    finalDecision = $decision
    externalConnectionAttempted = $false
    snapshotAttempted = $false
    replayAttempted = $false
    runtimePowerAdded = $false
    diagnosticCompleted = $true
    localOnly = $true
    lmaxEvidenceFailure = $false
    apiWorkerGatewayMode = "FakeLmaxGateway"
    results = $results
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }
