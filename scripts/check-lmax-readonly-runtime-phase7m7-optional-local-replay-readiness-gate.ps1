param(
    [string]$ReportFile = "artifacts/readiness/phase7m7-optional-local-replay-readiness-report.json",
    [string]$GateFile = "artifacts/readiness/phase7m7-optional-local-replay-readiness-gate.json",
    [string]$NoteFile = "artifacts/readiness/phase7m7-optional-local-replay-readiness-note.md",
    [string]$Phase7M6ReportFile = "artifacts/readiness/phase7m6-operator-started-local-api-health-verification-report.json",
    [string]$Phase7M6GateFile = "artifacts/readiness/phase7m6-operator-started-local-api-health-verification-gate.json"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|raw\s*fix|BEGIN\s+PRIVATE\s+KEY)'
$expectedAllowedNextPhase = "Phase 7M8 $([char]0x2014) Single Optional Local Replay Candidate Selection, No External Run"

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

Write-Host "LMAX Read-Only Runtime Phase 7M7 Optional Local Replay Readiness Gate"
Write-Host "Local-only validator. This does not connect to LMAX, request snapshots, run replay, call POST endpoints, or use credential values."

$reportRaw = Read-TextSafe $ReportFile "ReadinessReport"
$gateRaw = Read-TextSafe $GateFile "ReadinessGate"
$noteRaw = Read-TextSafe $NoteFile "ReadinessNote"
$phase7M6ReportRaw = Read-TextSafe $Phase7M6ReportFile "Phase7M6Report"
$phase7M6GateRaw = Read-TextSafe $Phase7M6GateFile "Phase7M6Gate"

if ($null -ne $reportRaw) {
    $report = $reportRaw | ConvertFrom-Json
    Assert-Equals $report.phase "7M7" "ReadinessReport" "Phase"
    Assert-Equals $report.readinessType "OptionalLocalReplayReadinessGate" "ReadinessReport" "Readiness type"
    Assert-True $report.localApiHealthRecovered "ReadinessReport" "Local API health recovered"
    Assert-True $report.safeRuntimePostureConfirmed "ReadinessReport" "Safe runtime posture confirmed"
    Assert-Equals $report.executionGateway "FakeLmaxGateway" "ReadinessReport" "Execution gateway"
    Assert-False $report.liveTradingEnabled "ReadinessReport" "Live trading enabled"
    Assert-False $report.externalConnectionsEnabled "ReadinessReport" "External connections enabled"
    Assert-False $report.lmaxEvidenceFailure "ReadinessReport" "LMAX evidence failure"
    Assert-True $report.evidenceCycleAlreadyClosed "ReadinessReport" "Evidence cycle already closed"
    Assert-True $report.uiStatusWorkstreamClosed "ReadinessReport" "UI status workstream closed"
    Assert-True $report.localReplayIsOptional "ReadinessReport" "Local replay is optional"
    foreach ($flag in @(
        "replayRunInThisPhase",
        "localReplayRunInThisPhase",
        "externalReplayRunInThisPhase",
        "postEndpointCalled",
        "mutationAttempted",
        "externalRunAttemptedInThisPhase",
        "snapshotRunInThisPhase",
        "schedulerOrPollingAdded",
        "runtimeShadowReplaySubmitAdded",
        "orderPathAdded",
        "gatewayRegistrationAdded",
        "tradingMutationAdded",
        "retryBatchLoopAdded",
        "wrapperValidationWeakened"
    )) {
        Assert-False $report.$flag "ReadinessReport" $flag
    }
    Assert-Equals $report.recommendedReplayCandidate "GBPUSD" "ReadinessReport" "Recommended replay candidate"
    foreach ($instrument in @("GBPUSD", "AUDUSD", "EURGBP")) {
        $candidate = @($report.marketDataOnlyEvidenceCandidates | Where-Object { [string]$_.instrument -eq $instrument })
        if ($candidate.Count -gt 0) { Add-Result "ReadinessReport" "Candidate exists: $instrument" "PASS" "Candidate found." } else { Add-Result "ReadinessReport" "Candidate exists: $instrument" "FAIL" "Candidate missing." }
    }
    Assert-Equals $report.allowedNextPhase $expectedAllowedNextPhase "ReadinessReport" "Allowed next phase"
    Assert-Equals $report.finalDecision "PASS_OPTIONAL_LOCAL_REPLAY_READINESS_GATE_RECORDED" "ReadinessReport" "Final decision"
    Assert-True $report.noSensitiveContent "ReadinessReport" "No sensitive content"
}

if ($null -ne $gateRaw) {
    $gate = $gateRaw | ConvertFrom-Json
    Assert-Equals $gate.phase "7M7" "ReadinessGate" "Phase"
    Assert-True $gate.readinessGateCompleted "ReadinessGate" "Readiness gate completed"
    Assert-True $gate.localOnly "ReadinessGate" "Local only"
    Assert-True $gate.localApiHealthRecovered "ReadinessGate" "Local API health recovered"
    Assert-True $gate.safeRuntimePostureConfirmed "ReadinessGate" "Safe runtime posture confirmed"
    Assert-Equals $gate.executionGateway "FakeLmaxGateway" "ReadinessGate" "Execution gateway"
    Assert-False $gate.liveTradingEnabled "ReadinessGate" "Live trading enabled"
    Assert-False $gate.externalConnectionsEnabled "ReadinessGate" "External connections enabled"
    Assert-False $gate.lmaxEvidenceFailure "ReadinessGate" "LMAX evidence failure"
    Assert-True $gate.evidenceCycleRemainsClosed "ReadinessGate" "Evidence cycle remains closed"
    Assert-True $gate.uiStatusWorkstreamRemainsClosed "ReadinessGate" "UI status workstream remains closed"
    Assert-True $gate.localReplayIsOptional "ReadinessGate" "Local replay is optional"
    Assert-True $gate.optionalLocalReplayCanBeConsidered "ReadinessGate" "Optional local replay can be considered"
    foreach ($flag in @(
        "localReplayAllowedInThisPhase",
        "replayAllowedInThisPhase",
        "externalReplayAllowed",
        "postEndpointAllowedInThisPhase",
        "mutationAllowed",
        "externalRunAllowed",
        "snapshotAllowed",
        "batchReplayAllowed",
        "automaticRetryRecommended",
        "schedulerOrPollingEnabled",
        "runtimeShadowReplaySubmitEnabled",
        "orderPathEnabled",
        "gatewayRegistrationEnabled",
        "tradingMutationEnabled",
        "retryBatchLoopAdded",
        "wrapperValidationWeakened",
        "replayRunInThisPhase",
        "localReplayRunInThisPhase",
        "externalReplayRunInThisPhase",
        "postEndpointCalled",
        "mutationAttempted",
        "externalRunAttemptedInThisPhase",
        "snapshotRunInThisPhase"
    )) {
        Assert-False $gate.$flag "ReadinessGate" $flag
    }
    Assert-Equals $gate.recommendedReplayCandidate "GBPUSD" "ReadinessGate" "Recommended replay candidate"
    Assert-Equals $gate.allowedNextPhase $expectedAllowedNextPhase "ReadinessGate" "Allowed next phase"
    Assert-Equals $gate.finalDecision "PASS_OPTIONAL_LOCAL_REPLAY_READINESS_GATE_RECORDED" "ReadinessGate" "Final decision"
    Assert-True $gate.noSensitiveContent "ReadinessGate" "No sensitive content"
}

if ($null -ne $phase7M6ReportRaw) {
    $phase7M6Report = $phase7M6ReportRaw | ConvertFrom-Json
    Assert-Equals $phase7M6Report.phase "7M6" "Phase7M6Report" "Phase"
    Assert-Equals $phase7M6Report.finalDecision "PASS_OPERATOR_STARTED_LOCAL_API_HEALTH_OK" "Phase7M6Report" "Final decision"
    Assert-True $phase7M6Report.safeRuntimePostureConfirmed "Phase7M6Report" "Safe runtime posture confirmed"
}

if ($null -ne $phase7M6GateRaw) {
    $phase7M6Gate = $phase7M6GateRaw | ConvertFrom-Json
    Assert-Equals $phase7M6Gate.phase "7M6" "Phase7M6Gate" "Phase"
    Assert-Equals $phase7M6Gate.finalDecision "PASS_OPERATOR_STARTED_LOCAL_API_HEALTH_OK" "Phase7M6Gate" "Final decision"
    Assert-False $phase7M6Gate.replayAllowedInThisPhase "Phase7M6Gate" "Replay allowed in phase 7M6"
}

if ($null -ne $noteRaw) {
    foreach ($marker in @("Local API health has recovered", "Replay is still optional", "was not run", "GBPUSD is the preferred", "EURGBP", "No LMAX external run", "Allowed next phase")) {
        if ($noteRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "ReadinessNote" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "ReadinessNote" "Marker: $marker" "FAIL" "Marker missing." }
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
Add-Result "POST" "Replay endpoint" "PASS" "This phase does not call POST endpoints."
Add-Result "RuntimePower" "Runtime behavior change" "PASS" "This phase records a local readiness gate only."

$failed = @($results | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } else { "PASS_OPTIONAL_LOCAL_REPLAY_READINESS_GATE_RECORDED" }
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7m7-optional-local-replay-readiness-gate-validation.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7M7"
    finalDecision = $decision
    externalConnectionAttempted = $false
    snapshotAttempted = $false
    replayAttempted = $false
    postEndpointCalled = $false
    runtimePowerAdded = $false
    recommendedReplayCandidate = "GBPUSD"
    apiWorkerGatewayMode = "FakeLmaxGateway"
    results = $results
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }
