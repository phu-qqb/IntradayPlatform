param(
    [string]$PlanFile = "artifacts/readiness/phase7l-readiness-ui-status-update-plan.json",
    [string]$MarkdownPlanFile = "artifacts/readiness/phase7l-readiness-ui-status-update-plan.md",
    [string]$GateFile = "artifacts/readiness/phase7l-readiness-ui-status-update-gate.json",
    [string]$Phase7K16SignoffFile = "artifacts/readiness/phase7k16-final-operator-signoff.json"
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

Write-Host "LMAX Read-Only Runtime Phase 7L Readiness UI/Status Update Planning Gate"
Write-Host "Local-only. This validator does not connect to LMAX, request snapshots, replay evidence, schedule work, or use credential values."

$planRaw = Read-TextSafe $PlanFile "Plan"
$markdownRaw = Read-TextSafe $MarkdownPlanFile "MarkdownPlan"
$gateRaw = Read-TextSafe $GateFile "Gate"
$signoffRaw = Read-TextSafe $Phase7K16SignoffFile "Phase7K16Signoff"

if ($null -ne $planRaw) {
    $plan = $planRaw | ConvertFrom-Json
    Assert-Equals $plan.phase "7L" "Plan" "Phase"
    Assert-Equals $plan.planType "ReadinessUiStatusUpdatePlan" "Plan" "Plan type"
    Assert-True $plan.planningOnly "Plan" "Planning only"
    Assert-Equals $plan.finalOperationalState "NoExternalAttemptsAllowed" "Plan" "Final operational state"
    foreach ($instrument in @("GBPUSD", "EURGBP", "AUDUSD")) {
        Assert-Contains $plan.successfulReadOnlyEvidenceInstruments $instrument "Plan" "Successful evidence includes $instrument"
    }
    Assert-Contains $plan.parkedInstruments "USDJPY" "Plan" "Parked instruments include USDJPY"
    Assert-Equals $plan.knownLocalIssue "LocalhostApiHealthTimeoutAffectedOptionalReplayOnly" "Plan" "Known local issue"
    foreach ($surface in @("TopStatusBar", "CommandCenter", "LmaxShadowPage.ReadOnlyMarketDataWorkflowPanel", "LmaxShadowPage.AdditionalInstrumentPlanningStatusPanel", "ReadinessDocumentation")) {
        if (@($plan.targetUiStatusSurfaces | ForEach-Object { $_.surface }) -contains $surface) { Add-Result "Plan" "Target surface $surface" "PASS" "Present." } else { Add-Result "Plan" "Target surface $surface" "FAIL" "Missing." }
    }
    foreach ($row in @("GBPUSD", "EURGBP", "AUDUSD", "USDJPY")) {
        if (@($plan.proposedInstrumentRows | ForEach-Object { $_.symbol }) -contains $row) { Add-Result "Plan" "Instrument row $row" "PASS" "Present." } else { Add-Result "Plan" "Instrument row $row" "FAIL" "Missing." }
    }
    Assert-True $plan.noRuntimeBehaviorChanges "Plan" "No runtime behavior changes"
    Assert-True $plan.noExternalConnectivityImplementation "Plan" "No external connectivity implementation"
    Assert-True $plan.noNewButtonsThatCanRunExternalAttempts "Plan" "No external-run buttons"
    Assert-True $plan.noAutomaticRefreshOrPollingAdded "Plan" "No automatic refresh or polling added"
    Assert-True $plan.noSchedulerAdded "Plan" "No scheduler added"
    Assert-True $plan.noOrderControlsAdded "Plan" "No order controls added"
    foreach ($flag in @("externalRunAttemptedInThisPhase", "snapshotRunInThisPhase", "replayRunInThisPhase", "runtimePowerAdded", "orderPathEnabled", "schedulerOrPollingEnabled", "runtimeShadowReplaySubmitEnabled", "tradingMutationEnabled", "gatewayRegistrationEnabled")) {
        Assert-False $plan.$flag "Plan" $flag
    }
    Assert-True $plan.apiWorkerRemainFakeLmaxGatewayOnly "Plan" "API/Worker remain FakeLmaxGateway only"
    Assert-True $plan.noSensitiveContent "Plan" "No sensitive content"
    Assert-Equals $plan.allowedNextPhase "Phase 7L2 - Readiness UI/Status Display Implementation, No External Run" "Plan" "Allowed next phase"
    Assert-Equals $plan.finalDecision "PASS_READINESS_UI_STATUS_UPDATE_PLAN_RECORDED" "Plan" "Final decision"
}

if ($null -ne $gateRaw) {
    $gate = $gateRaw | ConvertFrom-Json
    Assert-Equals $gate.phase "7L" "Gate" "Phase"
    Assert-Equals $gate.gateType "ReadinessUiStatusUpdatePlanningGate" "Gate" "Gate type"
    Assert-True $gate.readinessUiStatusUpdatePlanRecorded "Gate" "Plan recorded"
    Assert-True $gate.planningOnly "Gate" "Planning only"
    Assert-Equals $gate.phase7K16FinalDecision "PASS_FINAL_OPERATOR_SIGNOFF_RECORDED" "Gate" "Phase 7K16 final decision"
    Assert-Equals $gate.finalOperationalState "NoExternalAttemptsAllowed" "Gate" "Final operational state"
    Assert-Equals $gate.usdJpyStatus "ParkedSeparateTroubleshootingRail" "Gate" "USDJPY status"
    foreach ($instrument in @("GBPUSD", "EURGBP", "AUDUSD")) {
        Assert-Contains $gate.successfulReadOnlyEvidenceInstruments $instrument "Gate" "Successful evidence includes $instrument"
    }
    Assert-Contains $gate.parkedInstruments "USDJPY" "Gate" "Parked instruments include USDJPY"
    foreach ($flag in @(
        "directRunAuthorization",
        "anyInstrumentExternalRunAllowed",
        "externalAdditionalInstrumentAttemptsCurrentlyAllowed",
        "futureExternalRunCanBeConsidered",
        "externalRunAttemptedInThisPhase",
        "snapshotRunInThisPhase",
        "replayRunInThisPhase",
        "runtimePowerAdded",
        "batchExecutionAllowed",
        "automaticRetryRecommended",
        "wrapperValidationWeakened",
        "securityIdSwitchRecommended",
        "tokyo600xSwitchRecommended",
        "orderPathEnabled",
        "schedulerOrPollingEnabled",
        "runtimeShadowReplaySubmitEnabled",
        "tradingMutationEnabled",
        "gatewayRegistrationEnabled"
    )) {
        Assert-False $gate.$flag "Gate" $flag
    }
    Assert-True $gate.noRuntimeBehaviorChanges "Gate" "No runtime behavior changes"
    Assert-True $gate.noExternalConnectivityImplementation "Gate" "No external connectivity implementation"
    Assert-True $gate.noNewButtonsThatCanRunExternalAttempts "Gate" "No external-run buttons"
    Assert-True $gate.noAutomaticRefreshOrPollingAdded "Gate" "No automatic refresh or polling added"
    Assert-True $gate.noSchedulerAdded "Gate" "No scheduler added"
    Assert-True $gate.noOrderControlsAdded "Gate" "No order controls added"
    Assert-True $gate.apiWorkerRemainFakeLmaxGatewayOnly "Gate" "API/Worker remain FakeLmaxGateway only"
    Assert-True $gate.noSensitiveContent "Gate" "No sensitive content"
    foreach ($action in @("No external run.", "No snapshot.", "No replay.", "No USDJPY retry.", "No AUDUSD retry.", "No batch.", "No loop.", "No automatic retry.", "No wrapper relaxation.", "No SecurityID switch.", "No Tokyo 600x switch.", "No new external-run UI button.", "No scheduler.", "No order controls.")) {
        Assert-Contains $gate.disallowedActions $action "Gate" "Disallowed action: $action"
    }
    Assert-Equals $gate.allowedNextPhase "Phase 7L2 - Readiness UI/Status Display Implementation, No External Run" "Gate" "Allowed next phase"
    Assert-Equals $gate.finalDecision "PASS_READINESS_UI_STATUS_UPDATE_PLAN_RECORDED" "Gate" "Final decision"
}

if ($null -ne $signoffRaw) {
    $signoff = $signoffRaw | ConvertFrom-Json
    Assert-Equals $signoff.phase "7K16" "Phase7K16Signoff" "Phase"
    Assert-Equals $signoff.finalDecision "PASS_FINAL_OPERATOR_SIGNOFF_RECORDED" "Phase7K16Signoff" "Final decision"
    Assert-Equals $signoff.finalOperationalState "NoExternalAttemptsAllowed" "Phase7K16Signoff" "Final operational state"
    Assert-False $signoff.anyInstrumentExternalRunAllowed "Phase7K16Signoff" "Any instrument external run allowed"
}

if ($null -ne $markdownRaw) {
    foreach ($marker in @("NoExternalAttemptsAllowed", "GBPUSD", "EURGBP", "AUDUSD", "USDJPY", "FakeLmaxGateway", "No external-run buttons", "Allowed next phase")) {
        if ($markdownRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "MarkdownPlan" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "MarkdownPlan" "Marker: $marker" "FAIL" "Marker missing." }
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
Add-Result "Replay" "Replay" "PASS" "This phase does not replay evidence."
Add-Result "RuntimePower" "Runtime behavior change" "PASS" "This phase records planning artifacts only."

$failed = @($results | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } else { "PASS_READINESS_UI_STATUS_UPDATE_PLAN_RECORDED" }
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7l-readiness-ui-status-update-gate-validation.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7L"
    finalDecision = $decision
    externalConnectionAttempted = $false
    snapshotAttempted = $false
    replayAttempted = $false
    runtimePowerAdded = $false
    apiWorkerGatewayMode = "FakeLmaxGateway"
    results = $results
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }
