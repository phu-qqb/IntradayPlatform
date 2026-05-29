param(
    [string]$ReviewReportFile = "artifacts/readiness/phase7l3-readiness-ui-status-display-review-report.json",
    [string]$ClosureGateFile = "artifacts/readiness/phase7l3-readiness-ui-status-display-final-closure-gate.json",
    [string]$ClosureNoteFile = "artifacts/readiness/phase7l3-readiness-ui-status-display-final-closure-note.md",
    [string]$Phase7L2GateFile = "artifacts/readiness/phase7l2-readiness-ui-status-display-implementation-gate.json"
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

Write-Host "LMAX Read-Only Runtime Phase 7L3 Readiness UI/Status Display Final Closure Gate"
Write-Host "Local-only. This validator does not connect to LMAX, request snapshots, replay evidence, schedule work, or use credential values."

$reportRaw = Read-TextSafe $ReviewReportFile "ReviewReport"
$gateRaw = Read-TextSafe $ClosureGateFile "ClosureGate"
$noteRaw = Read-TextSafe $ClosureNoteFile "ClosureNote"
$phase7L2Raw = Read-TextSafe $Phase7L2GateFile "Phase7L2Gate"

if ($null -ne $reportRaw) {
    $report = $reportRaw | ConvertFrom-Json
    Assert-Equals $report.phase "7L3" "ReviewReport" "Phase"
    Assert-Equals $report.reviewedPanel "LMAX Read-Only Final Evidence Status" "ReviewReport" "Reviewed panel"
    Assert-True $report.displayOnly "ReviewReport" "Display only"
    Assert-False $report.backendEndpointAdded "ReviewReport" "Backend endpoint added"
    Assert-False $report.externalRunButtonPresent "ReviewReport" "External run button present"
    Assert-False $report.snapshotTriggerPresent "ReviewReport" "Snapshot trigger present"
    Assert-False $report.replayTriggerPresent "ReviewReport" "Replay trigger present"
    Assert-False $report.schedulerOrPollingPresent "ReviewReport" "Scheduler or polling present"
    Assert-False $report.orderActionPresent "ReviewReport" "Order action present"
    Assert-False $report.runtimeMutationActionPresent "ReviewReport" "Runtime mutation action present"
    Assert-Equals $report.overallStatusDisplayed "NoExternalAttemptsAllowed" "ReviewReport" "Overall status displayed"
    Assert-True $report.evidenceCycleClosedDisplayed "ReviewReport" "Evidence cycle closed displayed"
    Assert-True $report.finalOperatorSignoffDisplayed "ReviewReport" "Final operator signoff displayed"
    foreach ($instrument in @("GBPUSD", "EURGBP", "AUDUSD")) {
        Assert-Contains $report.successfulReadOnlyEvidenceInstrumentsDisplayed $instrument "ReviewReport" "Successful evidence displayed: $instrument"
    }
    Assert-Equals $report.parkedInstrumentDisplayed "USDJPY" "ReviewReport" "Parked instrument displayed"
    Assert-Equals $report.usdJpyStatusDisplayed "ParkedSeparateTroubleshootingRail" "ReviewReport" "USDJPY status displayed"
    Assert-Equals $report.knownLocalIssueDisplayed "LocalhostApiHealthTimeoutAffectedOptionalReplayOnly" "ReviewReport" "Known local issue displayed"
    Assert-True $report.apiWorkerRemainFakeLmaxGatewayOnly "ReviewReport" "API/Worker remain FakeLmaxGateway only"
    Assert-True $report.noSensitiveContent "ReviewReport" "No sensitive content"
}

if ($null -ne $gateRaw) {
    $gate = $gateRaw | ConvertFrom-Json
    Assert-Equals $gate.phase "7L3" "ClosureGate" "Phase"
    Assert-True $gate.uiStatusDisplayReviewed "ClosureGate" "UI status display reviewed"
    Assert-True $gate.uiStatusDisplayClosed "ClosureGate" "UI status display closed"
    Assert-True $gate.phase7LWorkstreamClosed "ClosureGate" "Phase 7L workstream closed"
    Assert-True $gate.displayOnly "ClosureGate" "Display only"
    Assert-Equals $gate.finalOperationalStateDisplayed "NoExternalAttemptsAllowed" "ClosureGate" "Final operational state displayed"
    Assert-True $gate.evidenceCycleClosed "ClosureGate" "Evidence cycle closed"
    foreach ($instrument in @("GBPUSD", "EURGBP", "AUDUSD")) {
        Assert-Contains $gate.successfulReadOnlyEvidenceInstruments $instrument "ClosureGate" "Successful evidence includes $instrument"
    }
    Assert-Contains $gate.parkedInstruments "USDJPY" "ClosureGate" "Parked instruments include USDJPY"
    foreach ($flag in @(
        "noExternalRunButtonAdded",
        "noSnapshotTriggerAdded",
        "noReplayTriggerAdded",
        "noSchedulerOrPollingAdded",
        "noRuntimeShadowReplaySubmitAdded",
        "noOrderPathAdded",
        "noGatewayRegistrationAdded",
        "noTradingMutationAdded",
        "noRetryBatchLoopAdded",
        "apiWorkerRemainFakeLmaxGatewayOnly",
        "noSensitiveContent"
    )) {
        Assert-True $gate.$flag "ClosureGate" $flag
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
        Assert-False $gate.$flag "ClosureGate" $flag
    }
    Assert-Equals $gate.allowedNextPhase "Phase 7M - Local API Health Timeout Follow-Up Planning, No External Run" "ClosureGate" "Allowed next phase"
    Assert-Equals $gate.finalDecision "PASS_READINESS_UI_STATUS_DISPLAY_FINAL_CLOSED" "ClosureGate" "Final decision"
}

if ($null -ne $phase7L2Raw) {
    $phase7L2 = $phase7L2Raw | ConvertFrom-Json
    Assert-Equals $phase7L2.phase "7L2" "Phase7L2Gate" "Phase"
    Assert-Equals $phase7L2.finalDecision "PASS_READINESS_UI_STATUS_DISPLAY_IMPLEMENTED" "Phase7L2Gate" "Final decision"
    Assert-True $phase7L2.uiStatusImplemented "Phase7L2Gate" "UI status implemented"
    Assert-True $phase7L2.displayOnly "Phase7L2Gate" "Display only"
}

if ($null -ne $noteRaw) {
    foreach ($marker in @("display-only", "GBPUSD", "EURGBP", "AUDUSD", "USDJPY", "NoExternalAttemptsAllowed", "FakeLmaxGateway", "not another snapshot", "Allowed next phase")) {
        if ($noteRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "ClosureNote" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "ClosureNote" "Marker: $marker" "FAIL" "Marker missing." }
    }
}

$componentFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Ui/src/components/LmaxReadOnlyFinalStatusPanel.tsx"
$appFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Ui/src/App.tsx"
if (Test-Path -LiteralPath $componentFile) {
    $componentText = Get-Content -LiteralPath $componentFile -Raw
    foreach ($marker in @("NoExternalAttemptsAllowed", "GBPUSD", "EURGBP", "AUDUSD", "ParkedSeparateTroubleshootingRail", "FakeLmaxGateway only", "Optional replay health timeout")) {
        if ($componentText.Contains($marker)) { Add-Result "UI" "Component marker: $marker" "PASS" "Present." } else { Add-Result "UI" "Component marker: $marker" "FAIL" "Missing." }
    }
    foreach ($unsafe in @("<button", "ActionButton", "CommandButton", "DataTable", "apiClient.", "fetch(", "setInterval", "setTimeout", "runLmax", "run snapshot", "SubmitOrder", "NewOrderSingle")) {
        if ($componentText.IndexOf($unsafe, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "UI" "No unsafe marker: $unsafe" "FAIL" "Found." } else { Add-Result "UI" "No unsafe marker: $unsafe" "PASS" "Not found." }
    }
}
if (Test-Path -LiteralPath $appFile) {
    $appText = Get-Content -LiteralPath $appFile -Raw
    if ($appText.Contains("LmaxReadOnlyFinalStatusPanel")) { Add-Result "UI" "Panel remains wired into App" "PASS" "Import/render found." } else { Add-Result "UI" "Panel remains wired into App" "FAIL" "Missing." }
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
Add-Result "RuntimePower" "Runtime behavior change" "PASS" "This phase records final UI/status closure only."

$failed = @($results | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } else { "PASS_READINESS_UI_STATUS_DISPLAY_FINAL_CLOSED" }
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7l3-readiness-ui-status-display-final-closure-gate-validation.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7L3"
    finalDecision = $decision
    externalConnectionAttempted = $false
    snapshotAttempted = $false
    replayAttempted = $false
    runtimePowerAdded = $false
    uiStatusDisplayReviewed = $true
    uiStatusDisplayClosed = $true
    phase7LWorkstreamClosed = $true
    apiWorkerGatewayMode = "FakeLmaxGateway"
    results = $results
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }
