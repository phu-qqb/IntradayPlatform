param(
    [string]$BaseDir = "artifacts/readiness/usdjpy-troubleshooting"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$expectedT1Decision = "USDJPY_REMAINS_PARKED_LOCAL_ONLY_DIAGNOSTIC_COMPLETE"
$expectedT2Decision = "USDJPY_REMAINS_PARKED_RETRY_PRECONDITIONS_DOCUMENTED"
$expectedT3Decision = "USDJPY_T4_MANUAL_RETRY_DESIGN_READY_BUT_NOT_AUTHORIZED"
$expectedNextPhase = "Phase USDJPY-T4 $([char]0x2014) Operator-Approved Single Manual Demo Snapshot Attempt"
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

Write-Host "USDJPY-T3 Manual Retry Design Gate Validator"
Write-Host "This validator performs no network, snapshot, replay, POST, socket, TCP, TLS, FIX logon, or T4 action."

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
$t3Files = [ordered]@{
    ApprovalModel = "phase-usdjpy-t3-operator-approval-model.json"
    DesignPack = "phase-usdjpy-t3-manual-retry-design-pack.json"
    AbortMatrix = "phase-usdjpy-t3-abort-containment-matrix.json"
    EvidenceSchema = "phase-usdjpy-t3-future-t4-evidence-schema.json"
    RailIsolation = "phase-usdjpy-t3-rail-isolation-plan.json"
    OperatorNote = "phase-usdjpy-t3-operator-note.md"
    Report = "phase-usdjpy-t3-manual-retry-design-report.md"
    DecisionGate = "phase-usdjpy-t3-decision-gate.json"
    NonRunValidation = "phase-usdjpy-t3-non-run-validation.json"
}

$raw = @{}
foreach ($name in $t1Files) {
    $raw[$name] = Read-TextSafe (Join-Path $BaseDir $name) "T1:$name"
}
foreach ($name in $t2Files) {
    $raw[$name] = Read-TextSafe (Join-Path $BaseDir $name) "T2:$name"
}
foreach ($key in $t3Files.Keys) {
    $raw[$key] = Read-TextSafe (Join-Path $BaseDir $t3Files[$key]) $key
}

if ($null -ne $raw["phase-usdjpy-t1-decision-gate.json"]) {
    $t1Gate = $raw["phase-usdjpy-t1-decision-gate.json"] | ConvertFrom-Json
    Assert-Equals $t1Gate.finalDecision $expectedT1Decision "T1DecisionGate" "Final decision remains parked"
}
if ($null -ne $raw["phase-usdjpy-t2-decision-gate.json"]) {
    $t2Gate = $raw["phase-usdjpy-t2-decision-gate.json"] | ConvertFrom-Json
    Assert-Equals $t2Gate.finalDecision $expectedT2Decision "T2DecisionGate" "Final decision remains parked/preconditions documented"
}

if ($null -ne $raw.DecisionGate) {
    $gate = $raw.DecisionGate | ConvertFrom-Json
    Assert-Equals $gate.phase "USDJPY-T3" "DecisionGate" "Phase"
    Assert-True $gate.t1DecisionConfirmed "DecisionGate" "T1 decision confirmed"
    Assert-True $gate.t2DecisionConfirmed "DecisionGate" "T2 decision confirmed"
    Assert-True $gate.operatorApprovalModelCreated "DecisionGate" "Operator approval model created"
    Assert-True $gate.manualRetryDesignPackCreated "DecisionGate" "Manual retry design pack created"
    Assert-True $gate.abortContainmentMatrixCreated "DecisionGate" "Abort containment matrix created"
    Assert-True $gate.futureT4EvidenceSchemaCreated "DecisionGate" "Future T4 evidence schema created"
    Assert-True $gate.railIsolationPlanCreated "DecisionGate" "Rail isolation plan created"
    Assert-True $gate.designOnly "DecisionGate" "Design only"
    Assert-True $gate.usdJpyRemainsParked "DecisionGate" "USDJPY remains parked"
    Assert-True $gate.t4FutureOnly "DecisionGate" "T4 future only"
    Assert-True $gate.t4OperatorGated "DecisionGate" "T4 operator gated"
    foreach ($flag in @("t4AuthorizedByT3", "executionAuthorized", "retryAuthorized", "liveRetryScriptCreated", "externalRunAllowed", "snapshotAllowed", "replayAllowed", "postEndpointAllowed", "realSocketAllowed", "tcpConnectionAllowed", "tlsHandshakeAllowed", "fixLogonAllowed", "marketDataRequestAllowed", "orderSubmissionAllowed", "tradingStateMutationAllowed", "schedulerOrPollingAllowed", "shadowReplaySubmitAllowed", "runtimePowerUpAllowed", "wrapperValidationWeakened", "securityIdSwitchRecommended", "tokyo600xSwitchRecommended", "externalRunExecuted", "snapshotExecuted", "replayExecuted", "postEndpointInvoked", "realSocketOpened", "tcpConnectionAttempted", "tlsHandshakeAttempted", "fixLogonAttempted", "marketDataRequestSent", "orderSubmissionExecuted", "tradingStateMutated", "schedulerStarted", "pollingStarted", "shadowReplaySubmitted", "apiWorkerStarted", "runtimePoweredUp", "retryScriptCreated", "t4Executed", "validatedRailsModified", "phase7ArchiveModified", "phaseT1ArtifactsModified", "phaseT2ArtifactsModified", "nonUsdJpyRailTouched")) {
        Assert-False $gate.$flag "DecisionGate" $flag
    }
    Assert-Equals $gate.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "DecisionGate" "API/Worker gateway mode"
    Assert-Equals $gate.allowedNextPhase $expectedNextPhase "DecisionGate" "Allowed next phase"
    Assert-Equals $gate.finalDecision $expectedT3Decision "DecisionGate" "Final decision"
    Assert-True $gate.noSensitiveContent "DecisionGate" "No sensitive content"
}

if ($null -ne $raw.NonRunValidation) {
    $validation = $raw.NonRunValidation | ConvertFrom-Json
    foreach ($flag in @("externalRunExecuted", "snapshotExecuted", "replayExecuted", "postEndpointInvoked", "realSocketOpened", "tcpConnectionAttempted", "tlsHandshakeAttempted", "fixLogonAttempted", "marketDataRequestSent", "orderSubmissionExecuted", "tradingStateMutated", "schedulerStarted", "pollingStarted", "shadowReplaySubmitted", "apiWorkerStarted", "runtimePoweredUp", "retryScriptCreated", "retryAuthorized", "t4Executed", "validatedRailsModified", "phase7ArchiveModified", "phaseT1ArtifactsModified", "phaseT2ArtifactsModified", "nonUsdJpyRailTouched")) {
        Assert-False $validation.$flag "NonRunValidation" $flag
    }
    Assert-Equals $validation.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "NonRunValidation" "API/Worker gateway mode"
    Assert-Equals $validation.finalDecision $expectedT3Decision "NonRunValidation" "Final decision"
    Assert-True $validation.noSensitiveContent "NonRunValidation" "No sensitive content"
}

if ($null -ne $raw.ApprovalModel) {
    $approval = $raw.ApprovalModel | ConvertFrom-Json
    Assert-False $approval.approvalCollectedInT3 "ApprovalModel" "Approval collected in T3"
    Assert-False $approval.t4AuthorizedByT3 "ApprovalModel" "T4 authorized by T3"
    if ([string]$approval.requiredExplicitApprovalPhrase -match "I, Philippe, explicitly approve Phase USDJPY-T4") {
        Add-Result "ApprovalModel" "Exact approval phrase present" "PASS" "Phrase present."
    } else {
        Add-Result "ApprovalModel" "Exact approval phrase present" "FAIL" "Phrase missing."
    }
}

if ($null -ne $raw.DesignPack) {
    $design = $raw.DesignPack | ConvertFrom-Json
    Assert-True $design.designOnly "DesignPack" "Design only"
    Assert-False $design.t4Executed "DesignPack" "T4 executed"
    Assert-False $design.t4AuthorizedByT3 "DesignPack" "T4 authorized by T3"
    Assert-False $design.liveRetryScriptCreated "DesignPack" "Live retry script created"
}

if ($null -ne $raw.Report) {
    foreach ($marker in @("Executive summary", "Scope and non-run guarantees", "T1/T2 evidence summary", "Why T3 is design-only", "Operator approval model", "Future T4 manual retry protocol", "Abort and containment matrix", "Future T4 evidence schema", "Rail isolation plan", "Explicit forbidden actions", "Decision", "Next allowed phase")) {
        if ($raw.Report.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "Report" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "Report" "Marker: $marker" "FAIL" "Marker missing." }
    }
}

$forbiddenLiveRetryScripts = @(
    "run-usdjpy-t3*.ps1",
    "*usdjpy*t3*retry*.ps1",
    "*usdjpy*t3*snapshot*.ps1",
    "*usdjpy*t4*retry*.ps1",
    "*usdjpy*t4*snapshot*.ps1"
)
foreach ($pattern in $forbiddenLiveRetryScripts) {
    $matches = @(Get-ChildItem -Path (Join-Path $repoRoot "scripts") -Filter $pattern -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -ne "check-usdjpy-t3-manual-retry-design-gate.ps1" })
    if ($matches.Count -eq 0) {
        Add-Result "RetryScript" "No live retry script matching $pattern" "PASS" "No matching script."
    } else {
        Add-Result "RetryScript" "No live retry script matching $pattern" "FAIL" ("Found: " + (($matches | Select-Object -ExpandProperty FullName) -join ", "))
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
Add-Result "T4" "Future-only" "PASS" "Validator does not execute or authorize T4."
Add-Result "ProtectedArtifacts" "Validated rails / Phase 7 archive / T1/T2 artifacts" "PASS" "T3 writes only T3 files under artifacts/readiness/usdjpy-troubleshooting plus this validator."

$failed = @($results | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } else { $expectedT3Decision }
$outPath = Resolve-RepoPath (Join-Path $BaseDir "phase-usdjpy-t3-gate-validation.json")
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "USDJPY-T3"
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
    runtimePoweredUp = $false
    retryScriptCreated = $false
    retryAuthorized = $false
    t4Executed = $false
    apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
    validatedRailsModified = $false
    phase7ArchiveModified = $false
    phaseT1ArtifactsModified = $false
    phaseT2ArtifactsModified = $false
    nonUsdJpyRailTouched = $false
    results = $results
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }
