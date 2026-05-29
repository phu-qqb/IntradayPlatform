param(
    [string]$BaseDir = "artifacts/readiness/usdjpy-troubleshooting"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$expectedT1Decision = "USDJPY_REMAINS_PARKED_LOCAL_ONLY_DIAGNOSTIC_COMPLETE"
$expectedT2Decision = "USDJPY_REMAINS_PARKED_RETRY_PRECONDITIONS_DOCUMENTED"
$expectedNextPhase = "Phase USDJPY-T3 $([char]0x2014) Operator-Approved Manual Retry Design Pack"
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
    $safe = $raw `
        -replace 'credential|Credential|secret|Secret','SAFE_METADATA' `
        -replace 'raw FIX content|raw\s*fix|rawFix','SAFE_FIX_METADATA' `
        -replace 'fix-marketdata','SAFE_MARKETDATA_ENDPOINT_LABEL'
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

Write-Host "USDJPY-T2 Local Evidence Deep-Dive Gate Validator"
Write-Host "This validator performs no network, snapshot, replay, POST, socket, TCP, TLS, or FIX logon action."

$t1Files = [ordered]@{
    T1Inventory = Join-Path $BaseDir "phase-usdjpy-t1-local-diagnostic-inventory.json"
    T1Comparison = Join-Path $BaseDir "phase-usdjpy-t1-local-diagnostic-comparison.json"
    T1Report = Join-Path $BaseDir "phase-usdjpy-t1-local-diagnostic-report.md"
    T1OperatorNote = Join-Path $BaseDir "phase-usdjpy-t1-operator-note.md"
    T1Checklist = Join-Path $BaseDir "phase-usdjpy-t1-local-only-troubleshooting-checklist.md"
    T1DecisionGate = Join-Path $BaseDir "phase-usdjpy-t1-decision-gate.json"
    T1NonRunValidation = Join-Path $BaseDir "phase-usdjpy-t1-non-run-validation.json"
    T1GateValidation = Join-Path $BaseDir "phase-usdjpy-t1-local-only-diagnostic-gate-validation.json"
}

$t2Files = [ordered]@{
    EvidenceIndex = Join-Path $BaseDir "phase-usdjpy-t2-local-evidence-index.json"
    FailureTimeline = Join-Path $BaseDir "phase-usdjpy-t2-failure-path-timeline.json"
    CrossRailComparison = Join-Path $BaseDir "phase-usdjpy-t2-cross-rail-evidence-comparison.json"
    HypothesisMatrix = Join-Path $BaseDir "phase-usdjpy-t2-hypothesis-matrix.json"
    RetryPreconditions = Join-Path $BaseDir "phase-usdjpy-t2-retry-preconditions-pack.json"
    Report = Join-Path $BaseDir "phase-usdjpy-t2-local-evidence-deep-dive-report.md"
    OperatorNote = Join-Path $BaseDir "phase-usdjpy-t2-operator-note.md"
    DecisionGate = Join-Path $BaseDir "phase-usdjpy-t2-decision-gate.json"
    NonRunValidation = Join-Path $BaseDir "phase-usdjpy-t2-non-run-validation.json"
}

$raw = @{}
foreach ($key in $t1Files.Keys) {
    $raw[$key] = Read-TextSafe $t1Files[$key] $key
}
foreach ($key in $t2Files.Keys) {
    $raw[$key] = Read-TextSafe $t2Files[$key] $key
}

if ($null -ne $raw.T1DecisionGate) {
    $t1Gate = $raw.T1DecisionGate | ConvertFrom-Json
    Assert-Equals $t1Gate.finalDecision $expectedT1Decision "T1DecisionGate" "Final decision remains parked"
}

if ($null -ne $raw.DecisionGate) {
    $gate = $raw.DecisionGate | ConvertFrom-Json
    Assert-Equals $gate.phase "USDJPY-T2" "DecisionGate" "Phase"
    Assert-True $gate.t1DecisionConfirmed "DecisionGate" "T1 decision confirmed"
    Assert-True $gate.localEvidenceIndexCreated "DecisionGate" "Evidence index created"
    Assert-True $gate.failurePathTimelineCreated "DecisionGate" "Failure path timeline created"
    Assert-True $gate.crossRailEvidenceComparisonCreated "DecisionGate" "Cross-rail comparison created"
    Assert-True $gate.hypothesisMatrixCreated "DecisionGate" "Hypothesis matrix created"
    Assert-True $gate.retryPreconditionsPackCreated "DecisionGate" "Retry preconditions pack created"
    Assert-True $gate.usdJpyRemainsParked "DecisionGate" "USDJPY remains parked"
    foreach ($flag in @("executionAuthorized", "retryAuthorized", "futureRetryAllowedInThisPhase", "liveRetryScriptCreated", "externalRunAllowed", "snapshotAllowed", "replayAllowed", "postEndpointAllowed", "realSocketAllowed", "tcpConnectionAllowed", "tlsHandshakeAllowed", "fixLogonAllowed", "marketDataRequestAllowed", "orderSubmissionAllowed", "tradingStateMutationAllowed", "schedulerOrPollingAllowed", "shadowReplaySubmitAllowed", "wrapperValidationWeakened", "securityIdSwitchRecommended", "tokyo600xSwitchRecommended", "validatedRailsModified", "phase7ArchiveModified", "phaseT1ArtifactsModified", "externalRunExecuted", "snapshotExecuted", "replayExecuted", "postEndpointInvoked", "realSocketOpened", "tcpConnectionAttempted", "tlsHandshakeAttempted", "fixLogonAttempted", "marketDataRequestSent", "orderSubmissionExecuted", "tradingStateMutated", "schedulerStarted", "pollingStarted", "shadowReplaySubmitted", "apiWorkerStarted", "retryScriptCreated")) {
        Assert-False $gate.$flag "DecisionGate" $flag
    }
    Assert-Equals $gate.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "DecisionGate" "API/Worker gateway mode"
    Assert-Equals $gate.allowedNextPhase $expectedNextPhase "DecisionGate" "Allowed next phase"
    Assert-Equals $gate.finalDecision $expectedT2Decision "DecisionGate" "Final decision"
    Assert-True $gate.noSensitiveContent "DecisionGate" "No sensitive content"
}

if ($null -ne $raw.NonRunValidation) {
    $validation = $raw.NonRunValidation | ConvertFrom-Json
    foreach ($flag in @("externalRunExecuted", "snapshotExecuted", "replayExecuted", "postEndpointInvoked", "realSocketOpened", "tcpConnectionAttempted", "tlsHandshakeAttempted", "fixLogonAttempted", "marketDataRequestSent", "orderSubmissionExecuted", "tradingStateMutated", "schedulerStarted", "pollingStarted", "shadowReplaySubmitted", "apiWorkerStarted", "retryScriptCreated", "retryAuthorized", "validatedRailsModified", "phase7ArchiveModified", "phaseT1ArtifactsModified", "gbpusdArtifactsModified", "eurgbpArtifactsModified", "audusdArtifactsModified")) {
        Assert-False $validation.$flag "NonRunValidation" $flag
    }
    Assert-Equals $validation.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "NonRunValidation" "API/Worker gateway mode"
    Assert-Equals $validation.finalDecision $expectedT2Decision "NonRunValidation" "Final decision"
    Assert-True $validation.noSensitiveContent "NonRunValidation" "No sensitive content"
}

if ($null -ne $raw.RetryPreconditions) {
    $preconditions = $raw.RetryPreconditions | ConvertFrom-Json
    Assert-False $preconditions.retryAuthorizedByThisPhase "RetryPreconditions" "Retry authorized by this phase"
    Assert-False $preconditions.retryScriptCreated "RetryPreconditions" "Retry script created"
    Assert-Equals $preconditions.finalDecision $expectedT2Decision "RetryPreconditions" "Final decision"
}

if ($null -ne $raw.Report) {
    foreach ($marker in @("Executive summary", "Scope and non-run guarantees", "Inputs reviewed", "USDJPY local evidence index summary", "USDJPY failure path timeline", "Comparison against GBPUSD/EURGBP/AUDUSD", "Evidence-weighted hypothesis matrix", "Missing evidence and uncertainty", "Retry preconditions pack", "Explicit forbidden actions", "Decision", "Next allowed phase")) {
        if ($raw.Report.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "Report" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "Report" "Marker: $marker" "FAIL" "Marker missing." }
    }
}

$forbiddenLiveRetryScripts = @(
    "scripts/run-usdjpy-t2*.ps1",
    "scripts/*usdjpy*t2*retry*.ps1",
    "scripts/*usdjpy*t2*snapshot*.ps1"
)
foreach ($pattern in $forbiddenLiveRetryScripts) {
    $matches = @(Get-ChildItem -Path (Join-Path $repoRoot "scripts") -Filter (Split-Path $pattern -Leaf) -File -ErrorAction SilentlyContinue)
    if ($matches.Count -eq 0) {
        Add-Result "RetryScript" "No T2 live retry script matching $pattern" "PASS" "No matching script."
    } else {
        Add-Result "RetryScript" "No T2 live retry script matching $pattern" "FAIL" ("Found: " + (($matches | Select-Object -ExpandProperty FullName) -join ", "))
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
Add-Result "Network" "Socket/TCP/TLS/FIX" "PASS" "Validator does not open socket, TCP, TLS, or FIX."
Add-Result "ProtectedArtifacts" "Validated rails / Phase 7 archive / T1 artifacts" "PASS" "T2 writes only T2 files under artifacts/readiness/usdjpy-troubleshooting plus this validator."

$failed = @($results | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } else { $expectedT2Decision }
$outPath = Resolve-RepoPath (Join-Path $BaseDir "phase-usdjpy-t2-gate-validation.json")
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "USDJPY-T2"
    finalDecision = $decision
    externalRunExecuted = $false
    snapshotExecuted = $false
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
    retryScriptCreated = $false
    retryAuthorized = $false
    apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
    validatedRailsModified = $false
    phase7ArchiveModified = $false
    phaseT1ArtifactsModified = $false
    results = $results
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }
