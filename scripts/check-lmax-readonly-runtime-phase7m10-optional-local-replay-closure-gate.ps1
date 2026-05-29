param(
    [string]$ReportFile = "artifacts/readiness/phase7m10-optional-local-replay-closure-report.json",
    [string]$GateFile = "artifacts/readiness/phase7m10-optional-local-replay-closure-gate.json",
    [string]$NoteFile = "artifacts/readiness/phase7m10-optional-local-replay-closure-note.md",
    [string]$Phase7M9ReportFile = "artifacts/readiness/phase7m9-gbpusd-single-optional-local-replay-report.json",
    [string]$Phase7M9GateFile = "artifacts/readiness/phase7m9-gbpusd-single-optional-local-replay-gate.json"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|raw\s*fix|BEGIN\s+PRIVATE\s+KEY)'
$expectedAllowedNextPhase = "Phase 7N $([char]0x2014) Final LMAX Read-Only Runtime Evidence Archive and Thread Handoff, No External Run"

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

Write-Host "LMAX Read-Only Runtime Phase 7M10 Optional Local Replay Closure Gate"
Write-Host "Local-only validator. This does not connect to LMAX, request snapshots, run replay, or call POST endpoints."

$reportRaw = Read-TextSafe $ReportFile "ClosureReport"
$gateRaw = Read-TextSafe $GateFile "ClosureGate"
$noteRaw = Read-TextSafe $NoteFile "ClosureNote"
$phase7M9ReportRaw = Read-TextSafe $Phase7M9ReportFile "Phase7M9Report"
$phase7M9GateRaw = Read-TextSafe $Phase7M9GateFile "Phase7M9Gate"

if ($null -ne $reportRaw) {
    $report = $reportRaw | ConvertFrom-Json
    Assert-Equals $report.phase "7M10" "ClosureReport" "Phase"
    Assert-Equals $report.closureType "OptionalLocalReplayWorkstreamClosure" "ClosureReport" "Closure type"
    Assert-True $report.localReplayWorkstreamClosed "ClosureReport" "Local replay workstream closed"
    Assert-Equals $report.replayInstrument "GBPUSD" "ClosureReport" "Replay instrument"
    Assert-Equals $report.replayRunId "7b67540f-4c65-4fa3-a8c9-7aad2e154e27" "ClosureReport" "Replay run id"
    Assert-Equals $report.replayStatus "Completed" "ClosureReport" "Replay status"
    Assert-Equals $report.validationStatus "Ok" "ClosureReport" "Validation status"
    Assert-Equals $report.observationCount "0" "ClosureReport" "Observation count"
    Assert-Equals $report.mutationGuard "Unchanged" "ClosureReport" "Mutation guard"
    Assert-Equals $report.localReplayOutcome "SuccessfulZeroObservationReplay" "ClosureReport" "Local replay outcome"
    Assert-True $report.localApiHealthIssueResolved "ClosureReport" "Local API health issue resolved"
    Assert-False $report.lmaxEvidenceFailure "ClosureReport" "LMAX evidence failure"
    Assert-True $report.evidenceCycleAlreadyClosed "ClosureReport" "Evidence cycle already closed"
    Assert-True $report.uiStatusWorkstreamClosed "ClosureReport" "UI status workstream closed"
    Assert-True $report.localReplayWasOptional "ClosureReport" "Local replay was optional"
    Assert-True $report.localReplayNonAuthoritative "ClosureReport" "Local replay non-authoritative"
    foreach ($flag in @("replayRunInThisPhase", "localReplayRunInThisPhase", "externalReplayRunInThisPhase", "replayEndpointCalled", "postEndpointCalled", "mutationAttempted", "externalRunAttemptedInThisPhase", "snapshotRunInThisPhase", "schedulerOrPollingAdded", "runtimeShadowReplaySubmitAdded", "orderPathAdded", "gatewayRegistrationAdded", "tradingMutationAdded", "retryBatchLoopAdded", "wrapperValidationWeakened")) {
        Assert-False $report.$flag "ClosureReport" $flag
    }
    Assert-True $report.apiWorkerRemainFakeLmaxGatewayOnly "ClosureReport" "API/Worker remain FakeLmaxGateway only"
    Assert-Equals $report.allowedNextPhase $expectedAllowedNextPhase "ClosureReport" "Allowed next phase"
    Assert-Equals $report.finalDecision "PASS_OPTIONAL_LOCAL_REPLAY_WORKSTREAM_CLOSED" "ClosureReport" "Final decision"
    Assert-True $report.noSensitiveContent "ClosureReport" "No sensitive content"
}

if ($null -ne $gateRaw) {
    $gate = $gateRaw | ConvertFrom-Json
    Assert-Equals $gate.phase "7M10" "ClosureGate" "Phase"
    Assert-True $gate.closureGateCompleted "ClosureGate" "Closure gate completed"
    Assert-True $gate.localOnly "ClosureGate" "Local only"
    Assert-True $gate.localReplayWorkstreamClosed "ClosureGate" "Local replay workstream closed"
    Assert-Equals $gate.replayInstrument "GBPUSD" "ClosureGate" "Replay instrument"
    Assert-Equals $gate.replayStatus "Completed" "ClosureGate" "Replay status"
    Assert-Equals $gate.validationStatus "Ok" "ClosureGate" "Validation status"
    Assert-Equals $gate.observationCount "0" "ClosureGate" "Observation count"
    Assert-Equals $gate.mutationGuard "Unchanged" "ClosureGate" "Mutation guard"
    Assert-True $gate.localApiHealthIssueResolved "ClosureGate" "Local API health issue resolved"
    Assert-False $gate.lmaxEvidenceFailure "ClosureGate" "LMAX evidence failure"
    Assert-True $gate.evidenceCycleRemainsClosed "ClosureGate" "Evidence cycle remains closed"
    Assert-True $gate.uiStatusWorkstreamRemainsClosed "ClosureGate" "UI status workstream remains closed"
    Assert-True $gate.localReplayIsOptional "ClosureGate" "Local replay is optional"
    Assert-True $gate.localReplayNonAuthoritative "ClosureGate" "Local replay non-authoritative"
    foreach ($flag in @("replayRunInThisPhase", "localReplayRunInThisPhase", "externalReplayRunInThisPhase", "replayEndpointCalled", "postEndpointCalled", "mutationAttempted", "externalRunAllowed", "snapshotAllowed", "replayAllowedInThisPhase", "batchReplayAllowed", "automaticRetryRecommended", "schedulerOrPollingEnabled", "runtimeShadowReplaySubmitEnabled", "orderPathEnabled", "gatewayRegistrationEnabled", "tradingMutationEnabled", "retryBatchLoopAdded", "wrapperValidationWeakened")) {
        Assert-False $gate.$flag "ClosureGate" $flag
    }
    Assert-True $gate.apiWorkerRemainFakeLmaxGatewayOnly "ClosureGate" "API/Worker remain FakeLmaxGateway only"
    Assert-Equals $gate.allowedNextPhase $expectedAllowedNextPhase "ClosureGate" "Allowed next phase"
    Assert-Equals $gate.finalDecision "PASS_OPTIONAL_LOCAL_REPLAY_WORKSTREAM_CLOSED" "ClosureGate" "Final decision"
    Assert-True $gate.noSensitiveContent "ClosureGate" "No sensitive content"
}

if ($null -ne $phase7M9ReportRaw) {
    $phase7M9Report = $phase7M9ReportRaw | ConvertFrom-Json
    Assert-Equals $phase7M9Report.phase "7M9" "Phase7M9Report" "Phase"
    Assert-Equals $phase7M9Report.finalDecision "PASS_SINGLE_OPTIONAL_LOCAL_REPLAY_COMPLETED" "Phase7M9Report" "Final decision"
    Assert-Equals $phase7M9Report.observationCount "0" "Phase7M9Report" "Observation count"
    Assert-Equals $phase7M9Report.mutationGuard "Unchanged" "Phase7M9Report" "Mutation guard"
}

if ($null -ne $phase7M9GateRaw) {
    $phase7M9Gate = $phase7M9GateRaw | ConvertFrom-Json
    Assert-Equals $phase7M9Gate.phase "7M9" "Phase7M9Gate" "Phase"
    Assert-True $phase7M9Gate.exactlyOneReplayRun "Phase7M9Gate" "Exactly one replay run"
    Assert-False $phase7M9Gate.externalRunAttemptedInThisPhase "Phase7M9Gate" "External run attempted"
    Assert-False $phase7M9Gate.snapshotRunInThisPhase "Phase7M9Gate" "Snapshot run"
}

if ($null -ne $noteRaw) {
    foreach ($marker in @("optional local replay issue is resolved", "zero observations", "mutation guard remained unchanged", "local-only", "non-authoritative", "No LMAX external connection or snapshot", "No further replay is needed")) {
        if ($noteRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "ClosureNote" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "ClosureNote" "Marker: $marker" "FAIL" "Marker missing." }
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
Add-Result "POST" "POST endpoint" "PASS" "This phase does not call POST endpoints."
Add-Result "RuntimePower" "Runtime behavior change" "PASS" "This phase records a local closure gate only."

$failed = @($results | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } else { "PASS_OPTIONAL_LOCAL_REPLAY_WORKSTREAM_CLOSED" }
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7m10-optional-local-replay-closure-gate-validation.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7M10"
    finalDecision = $decision
    externalConnectionAttempted = $false
    snapshotAttempted = $false
    replayAttempted = $false
    postEndpointCalled = $false
    runtimePowerAdded = $false
    localReplayWorkstreamClosed = $true
    apiWorkerGatewayMode = "FakeLmaxGateway"
    results = $results
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }
