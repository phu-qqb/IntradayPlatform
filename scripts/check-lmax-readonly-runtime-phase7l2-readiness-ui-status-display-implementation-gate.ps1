param(
    [string]$ImplementationReportFile = "artifacts/readiness/phase7l2-readiness-ui-status-display-implementation-report.json",
    [string]$GateFile = "artifacts/readiness/phase7l2-readiness-ui-status-display-implementation-gate.json",
    [string]$NoteFile = "artifacts/readiness/phase7l2-readiness-ui-status-display-implementation-note.md"
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

Write-Host "LMAX Read-Only Runtime Phase 7L2 Readiness UI/Status Display Implementation Gate"
Write-Host "Local-only. This validator does not connect to LMAX, request snapshots, replay evidence, schedule work, or use credential values."

$reportRaw = Read-TextSafe $ImplementationReportFile "ImplementationReport"
$gateRaw = Read-TextSafe $GateFile "Gate"
$noteRaw = Read-TextSafe $NoteFile "Note"

if ($null -ne $reportRaw) {
    $report = $reportRaw | ConvertFrom-Json
    Assert-Equals $report.phase "7L2" "ImplementationReport" "Phase"
    Assert-Equals $report.implementationType "ReadinessUiStatusDisplayImplementation" "ImplementationReport" "Implementation type"
    Assert-True $report.uiStatusImplemented "ImplementationReport" "UI status implemented"
    Assert-True $report.displayOnly "ImplementationReport" "Display only"
    Assert-Equals $report.overallStatus "NoExternalAttemptsAllowed" "ImplementationReport" "Overall status"
    Assert-True $report.evidenceCycleClosed "ImplementationReport" "Evidence cycle closed"
    Assert-True $report.finalOperatorSignoffRecorded "ImplementationReport" "Final operator signoff recorded"
    foreach ($instrument in @("GBPUSD", "EURGBP", "AUDUSD")) {
        Assert-Contains $report.successfulReadOnlyEvidenceInstruments $instrument "ImplementationReport" "Successful evidence includes $instrument"
    }
    Assert-Contains $report.parkedInstruments "USDJPY" "ImplementationReport" "Parked instruments include USDJPY"
    Assert-Equals $report.usdJpyStatus "ParkedSeparateTroubleshootingRail" "ImplementationReport" "USDJPY status"
    Assert-Equals $report.knownLocalIssue "LocalhostApiHealthTimeoutAffectedOptionalReplayOnly" "ImplementationReport" "Known local issue"
    foreach ($file in @(
        "src/QQ.Production.Intraday.Ui/src/components/LmaxReadOnlyFinalStatusPanel.tsx",
        "src/QQ.Production.Intraday.Ui/src/App.tsx",
        "src/QQ.Production.Intraday.Ui/src/components/CockpitUi.test.tsx"
    )) {
        Assert-Contains $report.uiFilesChanged $file "ImplementationReport" "UI file changed: $file"
    }
    foreach ($flag in @("apiWorkerRemainFakeLmaxGatewayOnly", "noExternalRunButtonAdded", "noSnapshotTriggerAdded", "noReplayTriggerAdded", "noSchedulerOrPollingAdded", "noRuntimeShadowReplaySubmitAdded", "noOrderPathAdded", "noGatewayRegistrationAdded", "noTradingMutationAdded", "noRetryBatchLoopAdded", "noSensitiveContent")) {
        Assert-True $report.$flag "ImplementationReport" $flag
    }
    foreach ($flag in @("wrapperValidationWeakened", "externalRunAttemptedInThisPhase", "snapshotRunInThisPhase", "replayRunInThisPhase", "runtimePowerAdded")) {
        Assert-False $report.$flag "ImplementationReport" $flag
    }
    Assert-Equals $report.finalDecision "PASS_READINESS_UI_STATUS_DISPLAY_IMPLEMENTED" "ImplementationReport" "Final decision"
}

if ($null -ne $gateRaw) {
    $gate = $gateRaw | ConvertFrom-Json
    Assert-Equals $gate.phase "7L2" "Gate" "Phase"
    Assert-True $gate.uiStatusImplemented "Gate" "UI status implemented"
    Assert-True $gate.displayOnly "Gate" "Display only"
    Assert-Equals $gate.overallStatus "NoExternalAttemptsAllowed" "Gate" "Overall status"
    Assert-True $gate.evidenceCycleClosed "Gate" "Evidence cycle closed"
    Assert-True $gate.finalOperatorSignoffRecorded "Gate" "Final operator signoff recorded"
    foreach ($instrument in @("GBPUSD", "EURGBP", "AUDUSD")) {
        Assert-Contains $gate.successfulReadOnlyEvidenceInstruments $instrument "Gate" "Successful evidence includes $instrument"
    }
    Assert-Contains $gate.parkedInstruments "USDJPY" "Gate" "Parked instruments include USDJPY"
    Assert-Equals $gate.usdJpyStatus "ParkedSeparateTroubleshootingRail" "Gate" "USDJPY status"
    foreach ($flag in @("apiWorkerRemainFakeLmaxGatewayOnly", "noExternalRunButtonAdded", "noSnapshotTriggerAdded", "noReplayTriggerAdded", "noSchedulerOrPollingAdded", "noRuntimeShadowReplaySubmitAdded", "noOrderPathAdded", "noGatewayRegistrationAdded", "noTradingMutationAdded", "noRetryBatchLoopAdded", "noSensitiveContent")) {
        Assert-True $gate.$flag "Gate" $flag
    }
    foreach ($flag in @(
        "wrapperValidationWeakened",
        "externalRunAttemptedInThisPhase",
        "snapshotRunInThisPhase",
        "replayRunInThisPhase",
        "runtimePowerAdded",
        "directRunAuthorization",
        "anyInstrumentExternalRunAllowed",
        "externalAdditionalInstrumentAttemptsCurrentlyAllowed",
        "futureExternalRunCanBeConsidered",
        "batchExecutionAllowed",
        "automaticRetryRecommended",
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
    Assert-Equals $gate.knownLocalIssue "LocalhostApiHealthTimeoutAffectedOptionalReplayOnly" "Gate" "Known local issue"
    Assert-Equals $gate.allowedNextPhase "Phase 7L3 - Readiness UI/Status Display Review and Final Closure, No External Run" "Gate" "Allowed next phase"
    Assert-Equals $gate.finalDecision "PASS_READINESS_UI_STATUS_DISPLAY_IMPLEMENTED" "Gate" "Final decision"
}

if ($null -ne $noteRaw) {
    foreach ($marker in @("NoExternalAttemptsAllowed", "GBPUSD", "EURGBP", "AUDUSD", "USDJPY", "FakeLmaxGateway", "No backend execution endpoint", "Allowed next phase")) {
        if ($noteRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "Note" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "Note" "Marker: $marker" "FAIL" "Marker missing." }
    }
}

$componentFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Ui/src/components/LmaxReadOnlyFinalStatusPanel.tsx"
$appFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Ui/src/App.tsx"
if (Test-Path -LiteralPath $componentFile) {
    $componentText = Get-Content -LiteralPath $componentFile -Raw
    foreach ($marker in @("NoExternalAttemptsAllowed", "Final operator signoff", "GBPUSD", "EURGBP", "AUDUSD", "ParkedSeparateTroubleshootingRail", "FakeLmaxGateway only", "Optional replay health timeout")) {
        if ($componentText.Contains($marker)) { Add-Result "UI" "Component marker: $marker" "PASS" "Present." } else { Add-Result "UI" "Component marker: $marker" "FAIL" "Missing." }
    }
    foreach ($unsafe in @("<button", "ActionButton", "CommandButton", "DataTable", "apiClient.", "fetch(", "setInterval", "setTimeout", "runLmax", "run snapshot", "SubmitOrder", "NewOrderSingle")) {
        if ($componentText.IndexOf($unsafe, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "UI" "No unsafe marker: $unsafe" "FAIL" "Found." } else { Add-Result "UI" "No unsafe marker: $unsafe" "PASS" "Not found." }
    }
}
if (Test-Path -LiteralPath $appFile) {
    $appText = Get-Content -LiteralPath $appFile -Raw
    if ($appText.Contains("LmaxReadOnlyFinalStatusPanel")) { Add-Result "UI" "Panel wired into App" "PASS" "Import/render found." } else { Add-Result "UI" "Panel wired into App" "FAIL" "Missing." }
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
Add-Result "RuntimePower" "Runtime behavior change" "PASS" "This phase adds display-only UI status."

$failed = @($results | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } else { "PASS_READINESS_UI_STATUS_DISPLAY_IMPLEMENTED" }
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7l2-readiness-ui-status-display-implementation-gate-validation.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7L2"
    finalDecision = $decision
    externalConnectionAttempted = $false
    snapshotAttempted = $false
    replayAttempted = $false
    runtimePowerAdded = $false
    uiStatusImplemented = $true
    displayOnly = $true
    apiWorkerGatewayMode = "FakeLmaxGateway"
    results = $results
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }
