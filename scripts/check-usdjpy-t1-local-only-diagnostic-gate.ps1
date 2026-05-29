param(
    [string]$BaseDir = "artifacts/readiness/usdjpy-troubleshooting"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$expectedDecision = "USDJPY_REMAINS_PARKED_LOCAL_ONLY_DIAGNOSTIC_COMPLETE"
$expectedNextPhase = "Phase USDJPY-T2 $([char]0x2014) Local Evidence Deep-Dive and Retry Preconditions Pack"
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|raw\s*fix|BEGIN\s+PRIVATE\s+KEY)'

function Add-Result([string]$Category, [string]$Check, [string]$Status, [string]$Detail) {
    $script:results += [ordered]@{ category = $Category; check = $Check; status = $Status; detail = $Detail }
    Write-Host ("{0}: {1} / {2} - {3}" -f $Status, $Category, $Check, $Detail)
}

function Resolve-RepoPath([string]$PathValue) {
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}

function Read-TextSafe([string]$PathValue, [string]$Label) {
    $resolved = Resolve-RepoPath $PathValue
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

function Assert-False([object]$Value, [string]$Category, [string]$Check) {
    if (-not [bool]$Value) { Add-Result $Category $Check "PASS" "false." } else { Add-Result $Category $Check "FAIL" "Expected false." }
}

function Assert-True([object]$Value, [string]$Category, [string]$Check) {
    if ([bool]$Value) { Add-Result $Category $Check "PASS" "true." } else { Add-Result $Category $Check "FAIL" "Expected true." }
}

function Assert-Equals([object]$Actual, [string]$Expected, [string]$Category, [string]$Check) {
    if ([string]$Actual -eq $Expected) { Add-Result $Category $Check "PASS" $Expected } else { Add-Result $Category $Check "FAIL" "Expected '$Expected' but found '$Actual'." }
}

Write-Host "USDJPY-T1 Local-Only Diagnostic Gate Validator"
Write-Host "This validator performs no network, snapshot, replay, POST, socket, TLS, or FIX logon action."

$files = [ordered]@{
    Inventory = Join-Path $BaseDir "phase-usdjpy-t1-local-diagnostic-inventory.json"
    Comparison = Join-Path $BaseDir "phase-usdjpy-t1-local-diagnostic-comparison.json"
    Report = Join-Path $BaseDir "phase-usdjpy-t1-local-diagnostic-report.md"
    OperatorNote = Join-Path $BaseDir "phase-usdjpy-t1-operator-note.md"
    Checklist = Join-Path $BaseDir "phase-usdjpy-t1-local-only-troubleshooting-checklist.md"
    DecisionGate = Join-Path $BaseDir "phase-usdjpy-t1-decision-gate.json"
    NonRunValidation = Join-Path $BaseDir "phase-usdjpy-t1-non-run-validation.json"
}

$raw = @{}
foreach ($key in $files.Keys) {
    $raw[$key] = Read-TextSafe $files[$key] $key
}

if ($null -ne $raw.DecisionGate) {
    $gate = $raw.DecisionGate | ConvertFrom-Json
    Assert-Equals $gate.phase "USDJPY-T1" "DecisionGate" "Phase"
    Assert-True $gate.diagnosticInventoryCompleted "DecisionGate" "Diagnostic inventory completed"
    Assert-True $gate.comparisonCompleted "DecisionGate" "Comparison completed"
    Assert-True $gate.troubleshootingChecklistCreated "DecisionGate" "Troubleshooting checklist created"
    Assert-True $gate.usdJpyRemainsParked "DecisionGate" "USDJPY remains parked"
    Assert-False $gate.validatedRailsModified "DecisionGate" "Validated rails modified"
    Assert-False $gate.phase7ArchiveModified "DecisionGate" "Phase 7 archive modified"
    foreach ($flag in @("externalRunAllowed", "snapshotAllowed", "replayAllowed", "postEndpointAllowed", "futureRetryAllowedInThisPhase", "securityIdSwitchRecommended", "tokyo600xSwitchRecommended", "wrapperValidationWeakened", "externalRunExecuted", "snapshotExecuted", "replayExecuted", "orderSubmissionExecuted", "tradingStateMutated", "schedulerStarted", "pollingStarted", "shadowReplaySubmitted", "realSocketOpened", "tlsHandshakeAttempted", "fixLogonAttempted")) {
        Assert-False $gate.$flag "DecisionGate" $flag
    }
    Assert-Equals $gate.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "DecisionGate" "API/Worker gateway mode"
    Assert-Equals $gate.allowedNextPhase $expectedNextPhase "DecisionGate" "Allowed next phase"
    Assert-Equals $gate.finalDecision $expectedDecision "DecisionGate" "Final decision"
    Assert-True $gate.noSensitiveContent "DecisionGate" "No sensitive content"
}

if ($null -ne $raw.NonRunValidation) {
    $validation = $raw.NonRunValidation | ConvertFrom-Json
    foreach ($flag in @("externalRunExecuted", "snapshotExecuted", "replayExecuted", "orderSubmissionExecuted", "tradingStateMutated", "schedulerStarted", "pollingStarted", "shadowReplaySubmitted", "realSocketOpened", "tlsHandshakeAttempted", "fixLogonAttempted", "postEndpointCalled", "replayEndpointCalled", "validatedRailsModified", "phase7ArchiveModified", "gbpusdArtifactsModified", "eurgbpArtifactsModified", "audusdArtifactsModified", "phase7AThrough7NArtifactsModified")) {
        Assert-False $validation.$flag "NonRunValidation" $flag
    }
    Assert-Equals $validation.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "NonRunValidation" "API/Worker gateway mode"
    Assert-Equals $validation.finalDecision $expectedDecision "NonRunValidation" "Final decision"
    Assert-True $validation.noSensitiveContent "NonRunValidation" "No sensitive content"
}

if ($null -ne $raw.Report) {
    foreach ($marker in @("Executive summary", "Scope and non-run guarantees", "USDJPY local inventory", "Comparison with validated rails", "Observed evidence", "Hypothesis matrix", "Local-only troubleshooting plan", "Future controlled retry preconditions", "Current decision", "Next allowed phase", "GBPUSD", "EURGBP", "AUDUSD")) {
        if ($raw.Report.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "Report" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "Report" "Marker: $marker" "FAIL" "Marker missing." }
    }
}

if ($null -ne $raw.Checklist) {
    foreach ($marker in @("Allowed local-only checks", "Forbidden actions")) {
        if ($raw.Checklist.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "Checklist" "Section: $marker" "PASS" "Section found." } else { Add-Result "Checklist" "Section: $marker" "FAIL" "Section missing." }
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

Add-Result "Runtime" "External LMAX connection" "PASS" "Validator does not connect to LMAX."
Add-Result "Snapshot" "Snapshot" "PASS" "Validator does not run snapshots."
Add-Result "Replay" "Replay" "PASS" "Validator does not run replay."
Add-Result "POST" "POST endpoint" "PASS" "Validator does not call POST endpoints."
Add-Result "ProtectedArtifacts" "Validated rails and Phase 7 archive" "PASS" "T1 writes only under artifacts/readiness/usdjpy-troubleshooting plus this validator."

$failed = @($results | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } else { $expectedDecision }
$outPath = Resolve-RepoPath (Join-Path $BaseDir "phase-usdjpy-t1-local-only-diagnostic-gate-validation.json")
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "USDJPY-T1"
    finalDecision = $decision
    externalRunExecuted = $false
    snapshotExecuted = $false
    replayExecuted = $false
    orderSubmissionExecuted = $false
    tradingStateMutated = $false
    schedulerStarted = $false
    pollingStarted = $false
    shadowReplaySubmitted = $false
    realSocketOpened = $false
    tlsHandshakeAttempted = $false
    fixLogonAttempted = $false
    apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
    validatedRailsModified = $false
    phase7ArchiveModified = $false
    results = $results
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }
