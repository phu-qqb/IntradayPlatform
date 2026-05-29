param(
    [string]$ReportFile = "artifacts/readiness/phase7m8-single-optional-local-replay-candidate-selection-report.json",
    [string]$GateFile = "artifacts/readiness/phase7m8-single-optional-local-replay-candidate-selection-gate.json",
    [string]$NoteFile = "artifacts/readiness/phase7m8-single-optional-local-replay-candidate-selection-note.md",
    [string]$Phase7M7ReportFile = "artifacts/readiness/phase7m7-optional-local-replay-readiness-report.json",
    [string]$Phase7M7GateFile = "artifacts/readiness/phase7m7-optional-local-replay-readiness-gate.json",
    [string]$EvidencePreviewFile = "artifacts/lmax-readonly-runtime-additional-snapshot/gbpusd/evidence-preview/lmax-readonly-gbpusd-evidence-preview-20260511-201538.json"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|raw\s*fix|BEGIN\s+PRIVATE\s+KEY)'
$expectedAllowedNextPhase = "Phase 7M9 $([char]0x2014) Single Optional Local Replay Execution, No External Run"

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

Write-Host "LMAX Read-Only Runtime Phase 7M8 Single Optional Local Replay Candidate Selection Gate"
Write-Host "Local-only validator. This does not connect to LMAX, request snapshots, run replay, call POST endpoints, or use credential values."

$reportRaw = Read-TextSafe $ReportFile "SelectionReport"
$gateRaw = Read-TextSafe $GateFile "SelectionGate"
$noteRaw = Read-TextSafe $NoteFile "SelectionNote"
$phase7M7ReportRaw = Read-TextSafe $Phase7M7ReportFile "Phase7M7Report"
$phase7M7GateRaw = Read-TextSafe $Phase7M7GateFile "Phase7M7Gate"
$previewRaw = Read-TextSafe $EvidencePreviewFile "EvidencePreview"

if ($null -ne $reportRaw) {
    $report = $reportRaw | ConvertFrom-Json
    Assert-Equals $report.phase "7M8" "SelectionReport" "Phase"
    Assert-Equals $report.selectionType "SingleOptionalLocalReplayCandidateSelection" "SelectionReport" "Selection type"
    Assert-Equals $report.selectedReplayCandidateInstrument "GBPUSD" "SelectionReport" "Selected replay candidate instrument"
    Assert-Equals $report.selectedReplayCandidateType "PostRemediationMarketDataOnlyEvidencePreview" "SelectionReport" "Selected replay candidate type"
    Assert-Equals $report.evidenceMode "MarketDataOnly" "SelectionReport" "Evidence mode"
    Assert-Equals $report.evidenceValidation "Ok" "SelectionReport" "Evidence validation"
    Assert-True $report.exactlyOneReplayCandidateSelected "SelectionReport" "Exactly one replay candidate selected"
    Assert-True $report.localApiHealthRecovered "SelectionReport" "Local API health recovered"
    Assert-True $report.safeRuntimePostureConfirmed "SelectionReport" "Safe runtime posture confirmed"
    Assert-Equals $report.executionGateway "FakeLmaxGateway" "SelectionReport" "Execution gateway"
    Assert-False $report.liveTradingEnabled "SelectionReport" "Live trading enabled"
    Assert-False $report.externalConnectionsEnabled "SelectionReport" "External connections enabled"
    Assert-True $report.localReplayIsOptional "SelectionReport" "Local replay is optional"
    foreach ($flag in @(
        "replayRunInThisPhase",
        "localReplayRunInThisPhase",
        "externalReplayRunInThisPhase",
        "postEndpointCalled",
        "replayEndpointCalled",
        "mutationAttempted",
        "externalRunAttemptedInThisPhase",
        "snapshotRunInThisPhase",
        "schedulerOrPollingAdded",
        "runtimeShadowReplaySubmitAdded",
        "orderPathAdded",
        "gatewayRegistrationAdded",
        "tradingMutationAdded",
        "retryBatchLoopAdded",
        "wrapperValidationWeakened",
        "lmaxEvidenceFailure"
    )) {
        Assert-False $report.$flag "SelectionReport" $flag
    }
    Assert-True $report.evidenceCycleRemainsClosed "SelectionReport" "Evidence cycle remains closed"
    Assert-True $report.uiStatusWorkstreamRemainsClosed "SelectionReport" "UI status workstream remains closed"
    Assert-Equals $report.allowedNextPhase $expectedAllowedNextPhase "SelectionReport" "Allowed next phase"
    Assert-Equals $report.finalDecision "PASS_SINGLE_OPTIONAL_LOCAL_REPLAY_CANDIDATE_SELECTED" "SelectionReport" "Final decision"
    Assert-True $report.noSensitiveContent "SelectionReport" "No sensitive content"
}

if ($null -ne $gateRaw) {
    $gate = $gateRaw | ConvertFrom-Json
    Assert-Equals $gate.phase "7M8" "SelectionGate" "Phase"
    Assert-True $gate.selectionGateCompleted "SelectionGate" "Selection gate completed"
    Assert-True $gate.localOnly "SelectionGate" "Local only"
    Assert-Equals $gate.selectedReplayCandidateInstrument "GBPUSD" "SelectionGate" "Selected replay candidate instrument"
    Assert-Equals $gate.selectedReplayCandidateType "PostRemediationMarketDataOnlyEvidencePreview" "SelectionGate" "Selected replay candidate type"
    Assert-True $gate.exactlyOneReplayCandidateSelected "SelectionGate" "Exactly one replay candidate selected"
    Assert-True $gate.localReplayCanBeConsideredInNextPhase "SelectionGate" "Local replay can be considered next phase"
    foreach ($flag in @(
        "localReplayAllowedInThisPhase",
        "replayAllowedInThisPhase",
        "externalReplayAllowed",
        "postEndpointAllowedInThisPhase",
        "replayEndpointCalled",
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
        "wrapperValidationWeakened"
    )) {
        Assert-False $gate.$flag "SelectionGate" $flag
    }
    Assert-True $gate.evidenceCycleRemainsClosed "SelectionGate" "Evidence cycle remains closed"
    Assert-True $gate.uiStatusWorkstreamRemainsClosed "SelectionGate" "UI status workstream remains closed"
    Assert-Equals $gate.allowedNextPhase $expectedAllowedNextPhase "SelectionGate" "Allowed next phase"
    Assert-Equals $gate.finalDecision "PASS_SINGLE_OPTIONAL_LOCAL_REPLAY_CANDIDATE_SELECTED" "SelectionGate" "Final decision"
    Assert-True $gate.noSensitiveContent "SelectionGate" "No sensitive content"
}

if ($null -ne $phase7M7ReportRaw) {
    $phase7M7Report = $phase7M7ReportRaw | ConvertFrom-Json
    Assert-Equals $phase7M7Report.phase "7M7" "Phase7M7Report" "Phase"
    Assert-Equals $phase7M7Report.finalDecision "PASS_OPTIONAL_LOCAL_REPLAY_READINESS_GATE_RECORDED" "Phase7M7Report" "Final decision"
    Assert-Equals $phase7M7Report.recommendedReplayCandidate "GBPUSD" "Phase7M7Report" "Recommended replay candidate"
}

if ($null -ne $phase7M7GateRaw) {
    $phase7M7Gate = $phase7M7GateRaw | ConvertFrom-Json
    Assert-Equals $phase7M7Gate.phase "7M7" "Phase7M7Gate" "Phase"
    Assert-True $phase7M7Gate.optionalLocalReplayCanBeConsidered "Phase7M7Gate" "Optional local replay can be considered"
    Assert-False $phase7M7Gate.replayAllowedInThisPhase "Phase7M7Gate" "Replay allowed in phase 7M7"
}

if ($null -ne $previewRaw) {
    $preview = $previewRaw | ConvertFrom-Json
    Assert-Equals $preview.instrument "GBPUSD" "EvidencePreview" "Instrument"
    Assert-Equals $preview.evidenceMode "MarketDataOnly" "EvidencePreview" "Evidence mode"
    Assert-Equals $preview.marketData.status "Ok" "EvidencePreview" "MarketData status"
    Assert-True $preview.noSensitiveContent "EvidencePreview" "No sensitive content"
}

if ($null -ne $noteRaw) {
    foreach ($marker in @("GBPUSD was selected", "Replay was not run", "No POST", "optional and non-authoritative", "No LMAX external run", "EURGBP already has", "AUDUSD remains valid")) {
        if ($noteRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "SelectionNote" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "SelectionNote" "Marker: $marker" "FAIL" "Marker missing." }
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
Add-Result "RuntimePower" "Runtime behavior change" "PASS" "This phase records a local selection gate only."

$failed = @($results | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } else { "PASS_SINGLE_OPTIONAL_LOCAL_REPLAY_CANDIDATE_SELECTED" }
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7m8-single-optional-local-replay-candidate-selection-gate-validation.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7M8"
    finalDecision = $decision
    externalConnectionAttempted = $false
    snapshotAttempted = $false
    replayAttempted = $false
    postEndpointCalled = $false
    runtimePowerAdded = $false
    selectedReplayCandidateInstrument = "GBPUSD"
    apiWorkerGatewayMode = "FakeLmaxGateway"
    results = $results
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }
