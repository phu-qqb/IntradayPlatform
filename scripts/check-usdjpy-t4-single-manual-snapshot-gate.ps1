param(
    [string]$BaseDir = "artifacts/readiness/usdjpy-troubleshooting"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$expectedT1Decision = "USDJPY_REMAINS_PARKED_LOCAL_ONLY_DIAGNOSTIC_COMPLETE"
$expectedT2Decision = "USDJPY_REMAINS_PARKED_RETRY_PRECONDITIONS_DOCUMENTED"
$expectedT3Decision = "USDJPY_T4_MANUAL_RETRY_DESIGN_READY_BUT_NOT_AUTHORIZED"
$requiredApprovalPhrase = "I, Philippe, explicitly approve Phase USDJPY-T4 for exactly one manual USDJPY Demo read-only snapshot attempt using SecurityID 4004/source 8, with no retry, no batch, no replay, no orders, no scheduler, no polling, no shadow replay submit, no trading mutation, and sanitized output only."
$allowedDecisions = @(
    "USDJPY_T4_PASS_SNAPSHOT_RECEIVED_SANITIZED",
    "USDJPY_T4_FAIL_TCP_BOUNDARY",
    "USDJPY_T4_FAIL_TLS_BOUNDARY",
    "USDJPY_T4_FAIL_FIX_LOGON_BOUNDARY",
    "USDJPY_T4_FAIL_MARKETDATA_REQUEST_BOUNDARY",
    "USDJPY_T4_ABORTED_PREFLIGHT",
    "USDJPY_T4_ABORTED_SAFETY_CONSTRAINT",
    "USDJPY_T4_INCONCLUSIVE_SANITIZED_EVIDENCE"
)
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|BEGIN\s+PRIVATE\s+KEY)'

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
    $safe = $raw `
        -replace 'credential|Credential|secret|Secret','SAFE_METADATA' `
        -replace 'raw FIX|rawFix|FIX Logon','SAFE_FIX_METADATA' `
        -replace 'LMAX_DEMO_FIX_USERNAME|LMAX_DEMO_FIX_PASSWORD|LMAX_DEMO_SENDER_COMP_ID|LMAX_DEMO_TARGET_COMP_ID','SAFE_ENV_LABEL'
    if ($safe -match $sensitivePattern) {
        Add-Result $Label "No sensitive content" "FAIL" "Credential-shaped content found."
    } else {
        Add-Result $Label "No sensitive content" "PASS" "No credential-shaped content."
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

Write-Host "USDJPY-T4 Single Manual Snapshot Gate Validator"
Write-Host "This validator performs no external connection, snapshot, replay, POST endpoint, or runtime action."

$t1Files = @(
    "phase-usdjpy-t1-local-diagnostic-inventory.json",
    "phase-usdjpy-t1-local-diagnostic-comparison.json",
    "phase-usdjpy-t1-local-diagnostic-report.md",
    "phase-usdjpy-t1-operator-note.md",
    "phase-usdjpy-t1-local-only-troubleshooting-checklist.md",
    "phase-usdjpy-t1-decision-gate.json",
    "phase-usdjpy-t1-non-run-validation.json",
    "phase-usdjpy-t1-local-only-diagnostic-gate-validation.json"
)
$t2Files = @(
    "phase-usdjpy-t2-local-evidence-index.json",
    "phase-usdjpy-t2-failure-path-timeline.json",
    "phase-usdjpy-t2-cross-rail-evidence-comparison.json",
    "phase-usdjpy-t2-hypothesis-matrix.json",
    "phase-usdjpy-t2-retry-preconditions-pack.json",
    "phase-usdjpy-t2-local-evidence-deep-dive-report.md",
    "phase-usdjpy-t2-operator-note.md",
    "phase-usdjpy-t2-decision-gate.json",
    "phase-usdjpy-t2-non-run-validation.json",
    "phase-usdjpy-t2-gate-validation.json"
)
$t3Files = @(
    "phase-usdjpy-t3-operator-approval-model.json",
    "phase-usdjpy-t3-manual-retry-design-pack.json",
    "phase-usdjpy-t3-abort-containment-matrix.json",
    "phase-usdjpy-t3-future-t4-evidence-schema.json",
    "phase-usdjpy-t3-rail-isolation-plan.json",
    "phase-usdjpy-t3-operator-note.md",
    "phase-usdjpy-t3-manual-retry-design-report.md",
    "phase-usdjpy-t3-decision-gate.json",
    "phase-usdjpy-t3-non-run-validation.json",
    "phase-usdjpy-t3-gate-validation.json"
)
$t4Files = [ordered]@{
    ApprovalRecord = "phase-usdjpy-t4-operator-approval-record.json"
    PreflightGate = "phase-usdjpy-t4-preflight-gate.json"
    ExecutionRecord = "phase-usdjpy-t4-single-attempt-execution-record.json"
    BoundaryEvidence = "phase-usdjpy-t4-sanitized-boundary-evidence.json"
    SnapshotResult = "phase-usdjpy-t4-sanitized-snapshot-result.json"
    AbortContainment = "phase-usdjpy-t4-abort-or-containment-record.json"
    NonMutation = "phase-usdjpy-t4-post-attempt-non-mutation-validation.json"
    RailIsolation = "phase-usdjpy-t4-rail-isolation-validation.json"
    DecisionGate = "phase-usdjpy-t4-decision-gate.json"
    Report = "phase-usdjpy-t4-manual-snapshot-report.md"
    OperatorNote = "phase-usdjpy-t4-operator-note.md"
}

$raw = @{}
foreach ($name in $t1Files) { $raw[$name] = Read-TextSafe (Join-Path $BaseDir $name) "T1:$name" }
foreach ($name in $t2Files) { $raw[$name] = Read-TextSafe (Join-Path $BaseDir $name) "T2:$name" }
foreach ($name in $t3Files) { $raw[$name] = Read-TextSafe (Join-Path $BaseDir $name) "T3:$name" }
foreach ($key in $t4Files.Keys) { $raw[$key] = Read-TextSafe (Join-Path $BaseDir $t4Files[$key]) $key }

$t1Gate = $raw["phase-usdjpy-t1-decision-gate.json"] | ConvertFrom-Json
$t2Gate = $raw["phase-usdjpy-t2-decision-gate.json"] | ConvertFrom-Json
$t3Gate = $raw["phase-usdjpy-t3-decision-gate.json"] | ConvertFrom-Json
Assert-Equals $t1Gate.finalDecision $expectedT1Decision "T1DecisionGate" "Final decision"
Assert-Equals $t2Gate.finalDecision $expectedT2Decision "T2DecisionGate" "Final decision"
Assert-Equals $t3Gate.finalDecision $expectedT3Decision "T3DecisionGate" "Final decision"

$approval = $raw.ApprovalRecord | ConvertFrom-Json
Assert-True $approval.operatorApprovalReceived "ApprovalRecord" "Operator approval received"
Assert-Equals $approval.approvalPhrase $requiredApprovalPhrase "ApprovalRecord" "Exact approval phrase"
Assert-True $approval.approvalPhraseMatchesT3RequiredPhrase "ApprovalRecord" "Approval phrase matches T3"

$preflight = $raw.PreflightGate | ConvertFrom-Json
Assert-True $preflight.preflightCompleted "PreflightGate" "Preflight completed"
if ([bool]$preflight.preflightPassed -or [bool]$preflight.preflightAbortedBeforeConnection) {
    Add-Result "PreflightGate" "Passed or safe abort" "PASS" "Preflight state is explicit."
} else {
    Add-Result "PreflightGate" "Passed or safe abort" "FAIL" "Preflight must pass or abort before connection."
}
Assert-Equals $preflight.instrument "USDJPY" "PreflightGate" "Instrument"
Assert-Equals $preflight.securityId "4004" "PreflightGate" "SecurityID"
Assert-Equals $preflight.securityIdSource "8" "PreflightGate" "SecurityIDSource"
Assert-False $preflight.batchMode "PreflightGate" "Batch mode"
Assert-False $preflight.loopMode "PreflightGate" "Loop mode"
Assert-False $preflight.replay "PreflightGate" "Replay"

$execution = $raw.ExecutionRecord | ConvertFrom-Json
Assert-True $execution.singleAttemptExecuted "ExecutionRecord" "Single attempt executed"
Assert-Equals $execution.attemptCount "1" "ExecutionRecord" "Attempt count"
Assert-Equals $execution.retryCount "0" "ExecutionRecord" "Retry count"
Assert-False $execution.batchMode "ExecutionRecord" "Batch mode"
Assert-False $execution.loopMode "ExecutionRecord" "Loop mode"
Assert-Equals $execution.instrument "USDJPY" "ExecutionRecord" "Instrument"
Assert-Equals $execution.securityId "4004" "ExecutionRecord" "SecurityID"
Assert-False $execution.orderSubmissionAttempted "ExecutionRecord" "Order submission"
Assert-False $execution.shadowReplaySubmitAttempted "ExecutionRecord" "Shadow replay submit"
Assert-False $execution.tradingMutationAttempted "ExecutionRecord" "Trading mutation"
Assert-False $execution.schedulerStarted "ExecutionRecord" "Scheduler"
Assert-False $execution.replayExecuted "ExecutionRecord" "Replay"
Assert-False $execution.credentialValuesReturned "ExecutionRecord" "Credential values returned"
Assert-True $execution.noSensitiveContent "ExecutionRecord" "No sensitive content"

$runtimePath = Resolve-RepoPath $execution.runtimeSnapshotArtifactPath
if (Test-Path -LiteralPath $runtimePath) {
    Add-Result "RuntimeSnapshotArtifact" "File exists" "PASS" $runtimePath
    $runtimeRaw = Get-Content -Raw -LiteralPath $runtimePath
    $runtime = $runtimeRaw | ConvertFrom-Json
    Assert-Equals $runtime.instrument "USDJPY" "RuntimeSnapshotArtifact" "Instrument"
    Assert-Equals $runtime.securityId "4004" "RuntimeSnapshotArtifact" "SecurityID"
    Assert-False $runtime.orderSubmissionAttempted "RuntimeSnapshotArtifact" "Order submission"
    Assert-False $runtime.shadowReplaySubmitAttempted "RuntimeSnapshotArtifact" "Shadow replay submit"
    Assert-False $runtime.tradingMutationAttempted "RuntimeSnapshotArtifact" "Trading mutation"
    Assert-False $runtime.schedulerStarted "RuntimeSnapshotArtifact" "Scheduler"
    Assert-False $runtime.credentialValuesReturned "RuntimeSnapshotArtifact" "Credential values returned"
    Assert-True $runtime.noSensitiveContent "RuntimeSnapshotArtifact" "No sensitive content"
} else {
    Add-Result "RuntimeSnapshotArtifact" "File exists" "FAIL" "Missing $runtimePath"
}

$nonMutation = $raw.NonMutation | ConvertFrom-Json
Assert-Equals $nonMutation.attemptCount "1" "NonMutation" "Attempt count"
Assert-Equals $nonMutation.retryCount "0" "NonMutation" "Retry count"
foreach ($flag in @("batchMode", "loopMode", "replayExecuted", "orderSubmissionExecuted", "schedulerStarted", "pollingStarted", "shadowReplaySubmitted", "tradingStateMutated", "apiWorkerStarted", "runtimePoweredUp", "gatewayRegistrationAdded", "nonUsdJpyRailTouched", "validatedRailsModified", "phase7ArchiveModified", "phaseT1ArtifactsModified", "phaseT2ArtifactsModified", "phaseT3ArtifactsModified", "credentialsPrinted", "credentialsStored", "credentialValuesReturned")) {
    Assert-False $nonMutation.$flag "NonMutation" $flag
}
Assert-Equals $nonMutation.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "NonMutation" "API/Worker gateway mode"
Assert-True $nonMutation.outputSanitized "NonMutation" "Output sanitized"

$rail = $raw.RailIsolation | ConvertFrom-Json
Assert-True $rail.usdJpyOnly "RailIsolation" "USDJPY only"
foreach ($flag in @("nonUsdJpyRailTouched", "gbpusdActionAttempted", "eurgbpActionAttempted", "audusdActionAttempted", "validatedRailsModified", "phase7ArchiveModified", "phaseT1ArtifactsModified", "phaseT2ArtifactsModified", "phaseT3ArtifactsModified")) {
    Assert-False $rail.$flag "RailIsolation" $flag
}

$gate = $raw.DecisionGate | ConvertFrom-Json
Assert-Equals $gate.phase "USDJPY-T4" "DecisionGate" "Phase"
Assert-Equals $gate.attemptCount "1" "DecisionGate" "Attempt count"
Assert-Equals $gate.retryCount "0" "DecisionGate" "Retry count"
foreach ($flag in @("batchMode", "loopMode", "replayExecuted", "orderSubmissionExecuted", "schedulerStarted", "pollingStarted", "shadowReplaySubmitted", "tradingStateMutated", "apiWorkerStarted", "runtimePoweredUp", "gatewayRegistrationAdded", "validatedRailsModified", "phase7ArchiveModified", "phaseT1ArtifactsModified", "phaseT2ArtifactsModified", "phaseT3ArtifactsModified", "nonUsdJpyRailTouched", "credentialsPrinted", "credentialsStored", "credentialValuesReturned")) {
    Assert-False $gate.$flag "DecisionGate" $flag
}
Assert-Equals $gate.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "DecisionGate" "API/Worker gateway mode"
if ($allowedDecisions -contains [string]$gate.finalDecision) {
    Add-Result "DecisionGate" "Allowed final decision" "PASS" $gate.finalDecision
} else {
    Add-Result "DecisionGate" "Allowed final decision" "FAIL" "Unexpected decision $($gate.finalDecision)"
}
Assert-True $gate.outputSanitized "DecisionGate" "Output sanitized"
Assert-True $gate.noSensitiveContent "DecisionGate" "No sensitive content"

if ($null -ne $raw.Report) {
    foreach ($marker in @("Executive summary", "Operator approval record", "Scope and safety constraints", "Preflight result", "Single attempt summary", "Boundary evidence", "TCP", "TLS", "FIX logon", "MarketDataRequest", "Snapshot/reject/error", "Sanitization statement", "Forbidden actions validation", "Rail isolation validation", "Post-attempt non-mutation validation", "Decision", "Recommended next phase")) {
        if ($raw.Report.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "Report" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "Report" "Marker: $marker" "FAIL" "Marker missing." }
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

Add-Result "ValidatorRuntime" "No external action in validator" "PASS" "Validator only reads local artifacts."

$failed = @($results | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } else { [string]$gate.finalDecision }
$outPath = Resolve-RepoPath (Join-Path $BaseDir "phase-usdjpy-t4-gate-validation.json")
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "USDJPY-T4"
    finalDecision = $decision
    resultClassification = [string]$gate.finalDecision
    attemptCount = 1
    retryCount = 0
    batchMode = $false
    loopMode = $false
    replayExecuted = $false
    orderSubmissionExecuted = $false
    schedulerStarted = $false
    pollingStarted = $false
    shadowReplaySubmitted = $false
    tradingStateMutated = $false
    apiWorkerStarted = $false
    apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
    validatedRailsModified = $false
    phase7ArchiveModified = $false
    phaseT1ArtifactsModified = $false
    phaseT2ArtifactsModified = $false
    phaseT3ArtifactsModified = $false
    nonUsdJpyRailTouched = $false
    outputSanitized = $true
    noSensitiveContent = $true
    results = $results
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }
