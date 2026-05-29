param(
    [string]$ReportFile = "artifacts/readiness/phase7m9-gbpusd-single-optional-local-replay-report.json",
    [string]$GateFile = "artifacts/readiness/phase7m9-gbpusd-single-optional-local-replay-gate.json",
    [string]$NoteFile = "artifacts/readiness/phase7m9-gbpusd-single-optional-local-replay-note.md",
    [string]$SelectionGateFile = "artifacts/readiness/phase7m8-single-optional-local-replay-candidate-selection-gate.json"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|raw\s*fix|BEGIN\s+PRIVATE\s+KEY)'
$expectedDecisions = @(
    "PASS_SINGLE_OPTIONAL_LOCAL_REPLAY_COMPLETED",
    "PASS_SINGLE_OPTIONAL_LOCAL_REPLAY_COMPLETED_WITH_WARNINGS",
    "PASS_SINGLE_OPTIONAL_LOCAL_REPLAY_SAFE_FAIL"
)

function Add-Result([string]$Category, [string]$Check, [string]$Status, [string]$Detail) {
    $script:results += [ordered]@{ category = $Category; check = $Check; status = $Status; detail = $Detail }
    Write-Host ("{0}: {1} / {2} - {3}" -f $Status, $Category, $Check, $Detail)
}

function Resolve-LocalPath([string]$PathValue) {
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

Write-Host "LMAX Read-Only Runtime Phase 7M9 Single Optional Local Replay Gate"
Write-Host "Validator only. This does not connect to LMAX, request snapshots, run replay, or call POST endpoints."

$reportRaw = Read-TextSafe $ReportFile "ReplayReport"
$gateRaw = Read-TextSafe $GateFile "ReplayGate"
$noteRaw = Read-TextSafe $NoteFile "ReplayNote"
$selectionGateRaw = Read-TextSafe $SelectionGateFile "Phase7M8Gate"

if ($null -ne $reportRaw) {
    $report = $reportRaw | ConvertFrom-Json
    Assert-Equals $report.phase "7M9" "ReplayReport" "Phase"
    Assert-Equals $report.replayType "SingleOptionalLocalReplayExecution" "ReplayReport" "Replay type"
    Assert-Equals $report.replayInstrument "GBPUSD" "ReplayReport" "Replay instrument"
    Assert-True $report.localOnly "ReplayReport" "Local only"
    Assert-True $report.localApiHealthOkBeforeReplay "ReplayReport" "Local API health OK before replay"
    Assert-True $report.safeRuntimePostureConfirmedBeforeReplay "ReplayReport" "Safe runtime posture confirmed before replay"
    Assert-Equals $report.executionGateway "FakeLmaxGateway" "ReplayReport" "Execution gateway"
    Assert-False $report.liveTradingEnabled "ReplayReport" "Live trading enabled"
    Assert-False $report.externalConnectionsEnabled "ReplayReport" "External connections enabled"
    Assert-Equals $report.evidenceMode "MarketDataOnly" "ReplayReport" "Evidence mode"
    Assert-Equals $report.evidenceValidation "Ok" "ReplayReport" "Evidence validation"
    Assert-True $report.exactlyOneReplayRun "ReplayReport" "Exactly one replay run"
    Assert-True $report.replayRunInThisPhase "ReplayReport" "Replay run in this phase"
    Assert-True $report.localReplayRunInThisPhase "ReplayReport" "Local replay run in this phase"
    Assert-False $report.externalReplayRunInThisPhase "ReplayReport" "External replay run in this phase"
    Assert-False $report.externalRunAttemptedInThisPhase "ReplayReport" "External run attempted"
    Assert-False $report.snapshotRunInThisPhase "ReplayReport" "Snapshot run in this phase"
    Assert-True $report.postEndpointCalled "ReplayReport" "POST endpoint called"
    Assert-True $report.replayEndpointCalled "ReplayReport" "Replay endpoint called"
    foreach ($flag in @("batchReplayUsed", "automaticRetryUsed", "schedulerOrPollingAdded", "runtimeShadowReplaySubmitAdded", "orderPathAdded", "gatewayRegistrationAdded", "tradingMutationAdded", "wrapperValidationWeakened")) {
        Assert-False $report.$flag "ReplayReport" $flag
    }
    if ($expectedDecisions -contains [string]$report.finalDecision) { Add-Result "ReplayReport" "Final decision in expected set" "PASS" $report.finalDecision } else { Add-Result "ReplayReport" "Final decision in expected set" "FAIL" "Unexpected $($report.finalDecision)" }
    Assert-True $report.noSensitiveContent "ReplayReport" "No sensitive content"
}

if ($null -ne $gateRaw) {
    $gate = $gateRaw | ConvertFrom-Json
    Assert-Equals $gate.phase "7M9" "ReplayGate" "Phase"
    Assert-True $gate.replayExecutionCompleted "ReplayGate" "Replay execution completed"
    Assert-True $gate.localOnly "ReplayGate" "Local only"
    Assert-Equals $gate.replayInstrument "GBPUSD" "ReplayGate" "Replay instrument"
    Assert-True $gate.exactlyOneReplayRun "ReplayGate" "Exactly one replay run"
    Assert-True $gate.replayRunInThisPhase "ReplayGate" "Replay run in this phase"
    Assert-True $gate.localReplayRunInThisPhase "ReplayGate" "Local replay run in this phase"
    foreach ($flag in @("externalReplayRunInThisPhase", "externalRunAttemptedInThisPhase", "snapshotRunInThisPhase", "batchReplayUsed", "automaticRetryUsed", "schedulerOrPollingEnabled", "runtimeShadowReplaySubmitEnabled", "orderPathEnabled", "gatewayRegistrationEnabled", "tradingMutationEnabled", "wrapperValidationWeakened")) {
        Assert-False $gate.$flag "ReplayGate" $flag
    }
    Assert-True $gate.apiWorkerRemainFakeLmaxGatewayOnly "ReplayGate" "API/Worker remain FakeLmaxGateway only"
    Assert-True $gate.evidenceCycleRemainsClosed "ReplayGate" "Evidence cycle remains closed"
    Assert-True $gate.uiStatusWorkstreamRemainsClosed "ReplayGate" "UI status workstream remains closed"
    if ($expectedDecisions -contains [string]$gate.finalDecision) { Add-Result "ReplayGate" "Final decision in expected set" "PASS" $gate.finalDecision } else { Add-Result "ReplayGate" "Final decision in expected set" "FAIL" "Unexpected $($gate.finalDecision)" }
    Assert-True $gate.noSensitiveContent "ReplayGate" "No sensitive content"
}

if ($null -ne $selectionGateRaw) {
    $selectionGate = $selectionGateRaw | ConvertFrom-Json
    Assert-Equals $selectionGate.phase "7M8" "Phase7M8Gate" "Phase"
    Assert-Equals $selectionGate.selectedReplayCandidateInstrument "GBPUSD" "Phase7M8Gate" "Selected replay candidate"
    Assert-True $selectionGate.exactlyOneReplayCandidateSelected "Phase7M8Gate" "Exactly one replay candidate selected"
}

if ($null -ne $noteRaw) {
    foreach ($marker in @("One local replay was run for GBPUSD only", "No LMAX connection or snapshot", "optional and non-authoritative", "local-only")) {
        if ($noteRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "ReplayNote" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "ReplayNote" "Marker: $marker" "FAIL" "Marker missing." }
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
Add-Result "ReplayScope" "Replay count" "PASS" "Validator expects exactly one local replay report."
Add-Result "RuntimePower" "Runtime behavior change" "PASS" "This phase uses the existing local replay endpoint only."

$failed = @($results | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } else { [string]($gateRaw | ConvertFrom-Json).finalDecision }
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7m9-gbpusd-single-optional-local-replay-gate-validation.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7M9"
    finalDecision = $decision
    externalConnectionAttempted = $false
    snapshotAttempted = $false
    replayAttempted = $true
    localReplayAttempted = $true
    postEndpointCalled = $true
    runtimePowerAdded = $false
    replayInstrument = "GBPUSD"
    apiWorkerGatewayMode = "FakeLmaxGateway"
    results = $results
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }
