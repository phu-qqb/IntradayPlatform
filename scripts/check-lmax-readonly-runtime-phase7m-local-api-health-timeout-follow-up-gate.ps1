param(
    [string]$PlanFile = "artifacts/readiness/phase7m-local-api-health-timeout-follow-up-plan.json",
    [string]$GateFile = "artifacts/readiness/phase7m-local-api-health-timeout-follow-up-gate.json",
    [string]$NoteFile = "artifacts/readiness/phase7m-local-api-health-timeout-follow-up-note.md",
    [string]$Phase7K15EvidencePackFile = "artifacts/readiness/phase7k15-final-additional-instrument-readonly-evidence-pack.json",
    [string]$Phase7K16SignoffFile = "artifacts/readiness/phase7k16-final-operator-signoff.json",
    [string]$Phase7L3ClosureGateFile = "artifacts/readiness/phase7l3-readiness-ui-status-display-final-closure-gate.json"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|raw\s*fix|BEGIN\s+PRIVATE\s+KEY)'

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

Write-Host "LMAX Read-Only Runtime Phase 7M Local API Health Timeout Follow-Up Planning Gate"
Write-Host "Local-only. This validator does not connect to LMAX, request snapshots, run replay, schedule work, or use credential values."

$planRaw = Read-TextSafe $PlanFile "Plan"
$gateRaw = Read-TextSafe $GateFile "Gate"
$noteRaw = Read-TextSafe $NoteFile "Note"
$packRaw = Read-TextSafe $Phase7K15EvidencePackFile "Phase7K15EvidencePack"
$signoffRaw = Read-TextSafe $Phase7K16SignoffFile "Phase7K16Signoff"
$closureRaw = Read-TextSafe $Phase7L3ClosureGateFile "Phase7L3ClosureGate"

if ($null -ne $planRaw) {
    $plan = $planRaw | ConvertFrom-Json
    Assert-Equals $plan.phase "7M" "Plan" "Phase"
    Assert-Equals $plan.issue "LocalhostApiHealthTimeoutAffectedOptionalReplayOnly" "Plan" "Issue"
    Assert-Equals $plan.issueScope "LocalApiHealthOptionalReplay" "Plan" "Issue scope"
    Assert-False $plan.lmaxEvidenceFailure "Plan" "LMAX evidence failure"
    Assert-True $plan.evidenceCycleAlreadyClosed "Plan" "Evidence cycle already closed"
    Assert-True $plan.uiStatusWorkstreamClosed "Plan" "UI status workstream closed"
    foreach ($flag in @(
        "externalRunAttemptedInThisPhase",
        "snapshotRunInThisPhase",
        "replayRunInThisPhase",
        "schedulerOrPollingAdded",
        "runtimeShadowReplaySubmitAdded",
        "orderPathAdded",
        "gatewayRegistrationAdded",
        "tradingMutationAdded",
        "retryBatchLoopAdded",
        "wrapperValidationWeakened"
    )) {
        Assert-False $plan.$flag "Plan" $flag
    }
    Assert-True $plan.apiWorkerRemainFakeLmaxGatewayOnly "Plan" "API/Worker remain FakeLmaxGateway only"
    foreach ($item in @(
        "Verify the local API process is running when optional replay health is needed.",
        "Verify the /health route responds locally.",
        "Verify no external LMAX dependency is required for local replay.",
        "Verify local replay remains optional and never required for MarketDataOnly evidence validity.",
        "Verify no scheduler, polling, replay automation, retry loop, or batch mechanism is introduced."
    )) {
        Assert-Contains $plan.investigationPlan $item "Plan" "Investigation item"
    }
    foreach ($action in @("No external LMAX connection.", "No snapshot.", "No replay in Phase 7M.", "No scheduler or polling.", "No order path.", "Do not reinterpret the timeout as an LMAX evidence failure.")) {
        Assert-Contains $plan.disallowedActions $action "Plan" "Disallowed action: $action"
    }
    Assert-Equals $plan.allowedNextPhase "Phase 7M2 - Local API Health Timeout Diagnostic Script, No External Run" "Plan" "Allowed next phase"
    Assert-Equals $plan.finalDecision "PASS_LOCAL_API_HEALTH_TIMEOUT_FOLLOW_UP_PLAN_RECORDED" "Plan" "Final decision"
    Assert-True $plan.noSensitiveContent "Plan" "No sensitive content"
}

if ($null -ne $gateRaw) {
    $gate = $gateRaw | ConvertFrom-Json
    Assert-Equals $gate.phase "7M" "Gate" "Phase"
    Assert-Equals $gate.issue "LocalhostApiHealthTimeoutAffectedOptionalReplayOnly" "Gate" "Issue"
    Assert-True $gate.planningOnly "Gate" "Planning only"
    Assert-True $gate.localApiHealthFollowUpRequired "Gate" "Local API health follow-up required"
    Assert-False $gate.lmaxEvidenceFailure "Gate" "LMAX evidence failure"
    Assert-True $gate.evidenceCycleRemainsClosed "Gate" "Evidence cycle remains closed"
    Assert-True $gate.uiStatusWorkstreamRemainsClosed "Gate" "UI status workstream remains closed"
    Assert-False $gate.externalRunAllowed "Gate" "External run allowed"
    Assert-False $gate.snapshotAllowed "Gate" "Snapshot allowed"
    Assert-False $gate.replayAllowedInThisPhase "Gate" "Replay allowed in this phase"
    Assert-True $gate.localReplayInvestigationAllowedLater "Gate" "Local replay investigation allowed later"
    foreach ($flag in @(
        "anyInstrumentExternalRunAllowed",
        "externalAdditionalInstrumentAttemptsCurrentlyAllowed",
        "directRunAuthorization",
        "batchExecutionAllowed",
        "automaticRetryRecommended",
        "schedulerOrPollingEnabled",
        "runtimeShadowReplaySubmitEnabled",
        "orderPathEnabled",
        "gatewayRegistrationEnabled",
        "tradingMutationEnabled",
        "wrapperValidationWeakened",
        "externalRunAttemptedInThisPhase",
        "snapshotRunInThisPhase",
        "replayRunInThisPhase",
        "schedulerOrPollingAdded",
        "runtimeShadowReplaySubmitAdded",
        "orderPathAdded",
        "gatewayRegistrationAdded",
        "tradingMutationAdded",
        "retryBatchLoopAdded"
    )) {
        Assert-False $gate.$flag "Gate" $flag
    }
    Assert-True $gate.apiWorkerRemainFakeLmaxGatewayOnly "Gate" "API/Worker remain FakeLmaxGateway only"
    Assert-True $gate.noSensitiveContent "Gate" "No sensitive content"
    Assert-Equals $gate.allowedNextPhase "Phase 7M2 - Local API Health Timeout Diagnostic Script, No External Run" "Gate" "Allowed next phase"
    Assert-Equals $gate.finalDecision "PASS_LOCAL_API_HEALTH_TIMEOUT_FOLLOW_UP_PLAN_RECORDED" "Gate" "Final decision"
}

if ($null -ne $packRaw) {
    $pack = $packRaw | ConvertFrom-Json
    Assert-Equals $pack.phase "7K15" "Phase7K15EvidencePack" "Phase"
    Assert-True $pack.externalAttemptCycleClosed "Phase7K15EvidencePack" "External attempt cycle closed"
    Assert-Equals $pack.knownLocalIssue "LocalhostApiHealthTimeoutAffectedOptionalReplayOnly" "Phase7K15EvidencePack" "Known local issue"
}

if ($null -ne $signoffRaw) {
    $signoff = $signoffRaw | ConvertFrom-Json
    Assert-Equals $signoff.phase "7K16" "Phase7K16Signoff" "Phase"
    Assert-Equals $signoff.finalDecision "PASS_FINAL_OPERATOR_SIGNOFF_RECORDED" "Phase7K16Signoff" "Final decision"
    Assert-Equals $signoff.knownLocalIssue "LocalhostApiHealthTimeoutAffectedOptionalReplayOnly" "Phase7K16Signoff" "Known local issue"
    Assert-False $signoff.anyInstrumentExternalRunAllowed "Phase7K16Signoff" "Any instrument external run allowed"
}

if ($null -ne $closureRaw) {
    $closure = $closureRaw | ConvertFrom-Json
    Assert-Equals $closure.phase "7L3" "Phase7L3ClosureGate" "Phase"
    Assert-Equals $closure.finalDecision "PASS_READINESS_UI_STATUS_DISPLAY_FINAL_CLOSED" "Phase7L3ClosureGate" "Final decision"
    Assert-True $closure.phase7LWorkstreamClosed "Phase7L3ClosureGate" "Phase 7L workstream closed"
}

if ($null -ne $noteRaw) {
    foreach ($marker in @("local API health issue only", "optional replay", "does not invalidate", "does not reopen LMAX external attempts", "safe local diagnostic script", "Allowed next phase")) {
        if ($noteRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "Note" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "Note" "Marker: $marker" "FAIL" "Marker missing." }
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
Add-Result "Replay" "Replay" "PASS" "This phase does not run replay."
Add-Result "RuntimePower" "Runtime behavior change" "PASS" "This phase records a local planning gate only."

$failed = @($results | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } else { "PASS_LOCAL_API_HEALTH_TIMEOUT_FOLLOW_UP_PLAN_RECORDED" }
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7m-local-api-health-timeout-follow-up-gate-validation.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7M"
    finalDecision = $decision
    externalConnectionAttempted = $false
    snapshotAttempted = $false
    replayAttempted = $false
    runtimePowerAdded = $false
    issue = "LocalhostApiHealthTimeoutAffectedOptionalReplayOnly"
    apiWorkerGatewayMode = "FakeLmaxGateway"
    results = $results
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }
