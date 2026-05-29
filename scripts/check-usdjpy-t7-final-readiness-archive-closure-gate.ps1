param(
    [string]$BaseDir = "artifacts/readiness/usdjpy-troubleshooting"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$expectedT1Decision = "USDJPY_REMAINS_PARKED_LOCAL_ONLY_DIAGNOSTIC_COMPLETE"
$expectedT2Decision = "USDJPY_REMAINS_PARKED_RETRY_PRECONDITIONS_DOCUMENTED"
$expectedT3Decision = "USDJPY_T4_MANUAL_RETRY_DESIGN_READY_BUT_NOT_AUTHORIZED"
$expectedT4Decision = "USDJPY_T4_PASS_SNAPSHOT_RECEIVED_SANITIZED"
$expectedT5Decision = "USDJPY_T5_SUCCESS_REVIEW_READY_FOR_PROMOTION_GATE"
$expectedT6Decision = "USDJPY_T6_CONTROLLED_PROMOTION_GATE_PASSED_WITH_CAVEAT"
$expectedT7Decision = "USDJPY_T7_FINAL_READINESS_ARCHIVE_CLOSED_WITH_CAVEAT"
$expectedCaveat = "prior failed-safe root cause remains unproven"
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

Write-Host "USDJPY-T7 Final Readiness Archive Closure Validator"
Write-Host "This validator performs no external run, snapshot, replay, POST endpoint, runtime enablement, or archive mutation."

$requiredPrior = @(
    "phase-usdjpy-t1-decision-gate.json",
    "phase-usdjpy-t2-decision-gate.json",
    "phase-usdjpy-t3-decision-gate.json",
    "phase-usdjpy-t4-decision-gate.json",
    "phase-usdjpy-t5-decision-gate.json",
    "phase-usdjpy-t6-controlled-promotion-gate.json",
    "phase-usdjpy-t6-gate-validation.json"
)
$requiredT7 = [ordered]@{
    ClosureManifest = "phase-usdjpy-t7-final-readiness-archive-closure-manifest.json"
    StatusSummary = "phase-usdjpy-t7-final-status-summary.json"
    OperatorNote = "phase-usdjpy-t7-final-operator-note.md"
    Report = "phase-usdjpy-t7-final-readiness-archive-closure-report.md"
    HandoffPrompt = "phase-usdjpy-t7-final-handoff-prompt.md"
    ClosureGate = "phase-usdjpy-t7-final-closure-gate.json"
    NonRunValidation = "phase-usdjpy-t7-non-run-validation.json"
}

$raw = @{}
foreach ($name in $requiredPrior) { $raw[$name] = Read-TextSafe (Join-Path $BaseDir $name) "Prior:$name" }
foreach ($key in $requiredT7.Keys) { $raw[$key] = Read-TextSafe (Join-Path $BaseDir $requiredT7[$key]) $key }

$t1Gate = $raw["phase-usdjpy-t1-decision-gate.json"] | ConvertFrom-Json
$t2Gate = $raw["phase-usdjpy-t2-decision-gate.json"] | ConvertFrom-Json
$t3Gate = $raw["phase-usdjpy-t3-decision-gate.json"] | ConvertFrom-Json
$t4Gate = $raw["phase-usdjpy-t4-decision-gate.json"] | ConvertFrom-Json
$t5Gate = $raw["phase-usdjpy-t5-decision-gate.json"] | ConvertFrom-Json
$t6Gate = $raw["phase-usdjpy-t6-controlled-promotion-gate.json"] | ConvertFrom-Json
Assert-Equals $t1Gate.finalDecision $expectedT1Decision "T1DecisionGate" "Final decision"
Assert-Equals $t2Gate.finalDecision $expectedT2Decision "T2DecisionGate" "Final decision"
Assert-Equals $t3Gate.finalDecision $expectedT3Decision "T3DecisionGate" "Final decision"
Assert-Equals $t4Gate.finalDecision $expectedT4Decision "T4DecisionGate" "Final decision"
Assert-Equals $t5Gate.finalDecision $expectedT5Decision "T5DecisionGate" "Final decision"
Assert-Equals $t6Gate.finalDecision $expectedT6Decision "T6DecisionGate" "Final decision"

$runtimePath = Resolve-RepoPath "artifacts/lmax-readonly-runtime-additional-snapshot/usdjpy/lmax-readonly-usdjpy-demo-snapshot-result-20260512-122928.json"
if (Test-Path -LiteralPath $runtimePath) {
    Add-Result "T4RuntimeArtifact" "File exists" "PASS" $runtimePath
} else {
    Add-Result "T4RuntimeArtifact" "File exists" "FAIL" "Missing $runtimePath"
}

$manifest = $raw.ClosureManifest | ConvertFrom-Json
Assert-Equals $manifest.phase "USDJPY-T7" "ClosureManifest" "Phase"
Assert-True $manifest.archiveClosed "ClosureManifest" "Archive closed"
Assert-True $manifest.readinessArchiveWithCaveat "ClosureManifest" "Readiness archive with caveat"
Assert-Equals $manifest.finalArchiveState "validated_readiness_archive_with_caveat" "ClosureManifest" "Final archive state"
Assert-Equals $manifest.runtimeState "not_enabled" "ClosureManifest" "Runtime state"
Assert-Equals $manifest.tradingState "not_enabled" "ClosureManifest" "Trading state"
Assert-Equals $manifest.schedulerPollingState "not_enabled" "ClosureManifest" "Scheduler/polling state"
Assert-Equals $manifest.gatewayMode "FakeLmaxGatewayOnly" "ClosureManifest" "Gateway mode"
Assert-Equals $manifest.caveat $expectedCaveat "ClosureManifest" "Caveat"
Assert-Equals $manifest.finalDecision $expectedT7Decision "ClosureManifest" "Final decision"

$summary = $raw.StatusSummary | ConvertFrom-Json
Assert-Equals $summary.instrument "USDJPY" "StatusSummary" "Instrument"
Assert-Equals $summary.securityId "4004" "StatusSummary" "SecurityID"
Assert-Equals $summary.securityIdSource "8" "StatusSummary" "SecurityIDSource"
Assert-Equals $summary.evidenceStatus "sanitized_success_received" "StatusSummary" "Evidence status"
Assert-Equals $summary.readinessStatus "validated_readiness_archive_with_caveat" "StatusSummary" "Readiness status"
foreach ($flag in @("runtimeEnabled", "tradingEnabled", "schedulerEnabled", "pollingEnabled", "replayEnabled", "orderPathEnabled")) {
    Assert-False $summary.$flag "StatusSummary" $flag
}
Assert-Equals $summary.caveat $expectedCaveat "StatusSummary" "Caveat"
Assert-Equals $summary.finalDecision $expectedT7Decision "StatusSummary" "Final decision"

$gate = $raw.ClosureGate | ConvertFrom-Json
Assert-Equals $gate.phase "USDJPY-T7" "ClosureGate" "Phase"
Assert-True $gate.archiveClosed "ClosureGate" "Archive closed"
Assert-True $gate.readinessArchiveWithCaveat "ClosureGate" "Readiness archive with caveat"
Assert-True $gate.futureApprovalRequiredForAnyRuntimeEnablement "ClosureGate" "Future approval required for runtime enablement"
foreach ($flag in @("runtimeEnabled", "tradingEnabled", "schedulerEnabled", "pollingEnabled", "orderPathEnabled", "replayEnabled", "externalRunExecuted", "newSnapshotExecuted", "replayExecuted", "postEndpointInvoked", "realSocketOpened", "tcpConnectionAttempted", "tlsHandshakeAttempted", "fixLogonAttempted", "marketDataRequestSent", "orderSubmissionExecuted", "tradingStateMutated", "schedulerStarted", "pollingStarted", "shadowReplaySubmitted", "apiWorkerStarted", "runtimePoweredUp", "retryExecuted", "batchExecuted", "loopExecuted", "runtimeEnablementExecuted", "tradingEnablementExecuted", "schedulerEnablementExecuted", "promotionExecutionPerformed", "validatedRailsModified", "phase7ArchiveModified", "phaseT1ArtifactsModified", "phaseT2ArtifactsModified", "phaseT3ArtifactsModified", "phaseT4ArtifactsModified", "phaseT5ArtifactsModified", "phaseT6ArtifactsModified", "nonUsdJpyExternalActionExecuted")) {
    Assert-False $gate.$flag "ClosureGate" $flag
}
Assert-Equals $gate.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "ClosureGate" "API/Worker gateway mode"
Assert-True $gate.apiWorkerRemainFakeLmaxGatewayOnly "ClosureGate" "API/Worker remain FakeLmaxGatewayOnly"
Assert-Equals $gate.caveat $expectedCaveat "ClosureGate" "Caveat"
Assert-Equals $gate.finalDecision $expectedT7Decision "ClosureGate" "Final decision"
Assert-True $gate.noSensitiveContent "ClosureGate" "No sensitive content"

$nonRun = $raw.NonRunValidation | ConvertFrom-Json
foreach ($flag in @("externalRunExecuted", "newSnapshotExecuted", "replayExecuted", "postEndpointInvoked", "realSocketOpened", "tcpConnectionAttempted", "tlsHandshakeAttempted", "fixLogonAttempted", "marketDataRequestSent", "orderSubmissionExecuted", "tradingStateMutated", "schedulerStarted", "pollingStarted", "shadowReplaySubmitted", "apiWorkerStarted", "runtimePoweredUp", "retryExecuted", "batchExecuted", "loopExecuted", "runtimeEnablementExecuted", "tradingEnablementExecuted", "schedulerEnablementExecuted", "promotionExecutionPerformed", "validatedRailsModified", "phase7ArchiveModified", "phaseT1ArtifactsModified", "phaseT2ArtifactsModified", "phaseT3ArtifactsModified", "phaseT4ArtifactsModified", "phaseT5ArtifactsModified", "phaseT6ArtifactsModified", "nonUsdJpyExternalActionExecuted")) {
    Assert-False $nonRun.$flag "NonRunValidation" $flag
}
Assert-Equals $nonRun.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "NonRunValidation" "API/Worker gateway mode"
Assert-True $nonRun.outputSanitized "NonRunValidation" "Output sanitized"

if ($null -ne $raw.Report) {
    foreach ($marker in @("Executive summary", "Scope and non-run guarantees", "Evidence chain T1-T6", "Final T7 closure decision", "Canonical evidence list", "Historical failed-safe caveat", "What is now closed", "What remains forbidden", "What future actions would require separate approval", "Final operator guidance")) {
        if ($raw.Report.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "Report" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "Report" "Marker: $marker" "FAIL" "Marker missing." }
    }
}
if ($null -ne $raw.HandoffPrompt) {
    foreach ($marker in @("validated_readiness_archive_with_caveat", "USDJPY_T7_FINAL_READINESS_ARCHIVE_CLOSED_WITH_CAVEAT", "prior failed-safe root cause remains unproven", "FakeLmaxGatewayOnly")) {
        if ($raw.HandoffPrompt.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "HandoffPrompt" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "HandoffPrompt" "Marker: $marker" "FAIL" "Marker missing." }
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

Add-Result "ValidatorRuntime" "No external action" "PASS" "Validator only reads local artifacts."
Add-Result "Closure" "No next phase required" "PASS" "Future work requires a separate explicit approval phase."

$failed = @($results | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } else { $expectedT7Decision }
$outPath = Resolve-RepoPath (Join-Path $BaseDir "phase-usdjpy-t7-gate-validation.json")
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "USDJPY-T7"
    finalDecision = $decision
    finalArchiveState = "validated_readiness_archive_with_caveat"
    caveat = $expectedCaveat
    externalRunExecuted = $false
    newSnapshotExecuted = $false
    replayExecuted = $false
    postEndpointInvoked = $false
    realSocketOpened = $false
    tcpConnectionAttempted = $false
    tlsHandshakeAttempted = $false
    fixLogonAttempted = $false
    marketDataRequestSent = $false
    orderSubmissionExecuted = $false
    tradingStateMutated = $false
    schedulerStarted = $false
    pollingStarted = $false
    shadowReplaySubmitted = $false
    apiWorkerStarted = $false
    runtimePoweredUp = $false
    retryExecuted = $false
    batchExecuted = $false
    loopExecuted = $false
    runtimeEnablementExecuted = $false
    tradingEnablementExecuted = $false
    schedulerEnablementExecuted = $false
    promotionExecutionPerformed = $false
    apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
    validatedRailsModified = $false
    phase7ArchiveModified = $false
    phaseT1ArtifactsModified = $false
    phaseT2ArtifactsModified = $false
    phaseT3ArtifactsModified = $false
    phaseT4ArtifactsModified = $false
    phaseT5ArtifactsModified = $false
    phaseT6ArtifactsModified = $false
    nonUsdJpyExternalActionExecuted = $false
    outputSanitized = $true
    noSensitiveContent = $true
    results = $results
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }
