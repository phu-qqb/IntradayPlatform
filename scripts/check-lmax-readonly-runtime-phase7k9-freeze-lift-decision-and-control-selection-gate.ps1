param(
    [string]$SelectionGateFile = "artifacts/readiness/phase7k9-freeze-lift-decision-and-known-good-control-selection-gate.json",
    [string]$SelectionNoteFile = "artifacts/readiness/phase7k9-freeze-lift-decision-and-known-good-control-selection-note.md"
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

function Get-Hits($Paths, $Patterns) {
    $existing = @($Paths | Where-Object { Test-Path -LiteralPath $_ })
    if ($existing.Count -eq 0) { return @() }
    return @(Select-String -Path $existing -Pattern $Patterns -SimpleMatch -ErrorAction SilentlyContinue)
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

Write-Host "LMAX Read-Only Runtime Phase 7K9 Freeze Lift Decision and Control Selection Gate"
Write-Host "Local-only. This gate does not connect to LMAX, request snapshots, replay evidence, schedule work, or use credentials."

$gateRaw = Read-TextSafe $SelectionGateFile "SelectionGate"
$noteRaw = Read-TextSafe $SelectionNoteFile "SelectionNote"
$gate = $null

if ($null -ne $gateRaw) {
    $gate = $gateRaw | ConvertFrom-Json

    Assert-Equals $gate.phase "7K9" "SelectionGate" "Phase"
    Assert-True $gate.remediationCompletionRecorded "SelectionGate" "Remediation completion recorded"
    Assert-True $gate.freezeLiftCanBeConsidered "SelectionGate" "Freeze lift can be considered"
    Assert-Equals $gate.freezeLiftDecision "LiftForSingleFutureKnownGoodControlOnly" "SelectionGate" "Freeze lift decision"
    Assert-True $gate.globalExternalAttemptFreezeLiftedForFutureSelection "SelectionGate" "Freeze lifted only for future selection"
    Assert-False $gate.directRunAuthorization "SelectionGate" "No direct run authorization"
    Assert-False $gate.anyInstrumentExternalRunAllowed "SelectionGate" "No instrument external run allowed"
    Assert-False $gate.externalAdditionalInstrumentAttemptsCurrentlyAllowed "SelectionGate" "No additional instrument external attempt allowed"

    Assert-Equals $gate.selectedFutureAttemptInstrument "GBPUSD" "SelectionGate" "Selected future attempt instrument"
    Assert-Equals $gate.selectedFutureAttemptType "KnownGoodControlSnapshot" "SelectionGate" "Selected future attempt type"
    Assert-Equals $gate.selectedInstrumentSecurityId "4002" "SelectionGate" "Selected instrument SecurityID"
    Assert-Equals $gate.selectedInstrumentSecurityIdSource "8" "SelectionGate" "Selected instrument SecurityIDSource"
    Assert-Equals $gate.selectedInstrumentEnvironment "Demo" "SelectionGate" "Selected environment"
    Assert-Equals $gate.selectedInstrumentVenueProfile "DemoLondon" "SelectionGate" "Selected venue profile"
    Assert-Equals $gate.selectedInstrumentRequestMode "SnapshotPlusUpdates" "SelectionGate" "Selected request mode"
    Assert-Equals $gate.selectedInstrumentSymbolEncodingMode "SecurityIdOnly" "SelectionGate" "Selected encoding mode"
    if ([int]$gate.selectedInstrumentMarketDepth -eq 1) { Add-Result "SelectionGate" "Selected market depth" "PASS" "1." } else { Add-Result "SelectionGate" "Selected market depth" "FAIL" "Expected 1." }
    Assert-Equals $gate.selectedInstrumentFinalPreRunGateDecision "PASS" "SelectionGate" "Selected final pre-run gate decision"
    Assert-True $gate.exactlyOneFutureCandidateSelected "SelectionGate" "Exactly one future candidate"
    Assert-True $gate.futureExternalRunCanBeConsidered "SelectionGate" "Future external run can be considered"

    foreach ($flag in @(
        "externalRunAttemptedInThisPhase",
        "snapshotRunInThisPhase",
        "replayRunInThisPhase",
        "controlRunInThisPhase",
        "gbpUsdRunInThisPhase",
        "eurGbpRunInThisPhase",
        "audusdRunInThisPhase",
        "usdjpyRunInThisPhase",
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
        Assert-False $gate.$flag "SelectionGate" $flag
    }

    Assert-Equals $gate.allowedNextPhase "Phase 7K10 - GBPUSD Post-Remediation Known-Good Control Manual Market-Hours Snapshot Attempt" "SelectionGate" "Allowed next phase"
    Assert-Equals $gate.finalDecision "PASS_FREEZE_LIFT_SELECTION_RECORDED" "SelectionGate" "Final decision"
    if ([bool]$gate.noSensitiveContent) { Add-Result "SelectionGate" "noSensitiveContent" "PASS" "true." } else { Add-Result "SelectionGate" "noSensitiveContent" "FAIL" "Expected true." }

    $requiredFutureFlags = @($gate.requiredFutureOperatorFlags)
    foreach ($flag in @("-AllowExternalConnections", "-ConfirmDemoReadOnly", "human-provided -Reason")) {
        if ($requiredFutureFlags -contains $flag) { Add-Result "SelectionGate" "Required future flag: $flag" "PASS" "Present." } else { Add-Result "SelectionGate" "Required future flag: $flag" "FAIL" "Missing." }
    }

    $disallowedText = (@($gate.disallowedActions) -join "`n").ToLowerInvariant()
    foreach ($marker in @(
        "no external run in phase 7k9",
        "no gbpusd control run in phase 7k9",
        "no usdjpy retry",
        "no audusd retry",
        "no batch",
        "no loop",
        "no automatic retry",
        "no wrapper relaxation",
        "no securityid switch",
        "no tokyo 600x switch"
    )) {
        if ($disallowedText.Contains($marker)) { Add-Result "SelectionGate" "Disallowed action: $marker" "PASS" "Present." } else { Add-Result "SelectionGate" "Disallowed action: $marker" "FAIL" "Missing." }
    }
}

if ($null -ne $noteRaw) {
    foreach ($marker in @("Phase 7K8 remediation completion is recorded", "does not authorize or run an external connection", "Selected future candidate: GBPUSD", "USDJPY and AUDUSD remain parked", "Phase 7K10")) {
        if ($noteRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "SelectionNote" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "SelectionNote" "Marker: $marker" "FAIL" "Marker missing." }
    }
}

$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"
$startupFiles = @($apiProgram, $workerProgram)
$startupText = ($startupFiles | Where-Object { Test-Path -LiteralPath $_ } | ForEach-Object { Get-Content -Raw -LiteralPath $_ }) -join "`n"
if ($startupText.Contains("FakeLmaxGateway") -and -not ($startupText.Contains("RealLmaxGateway") -or $startupText.Contains("LmaxVenueGatewaySkeleton"))) {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No real gateway registration found."
} else {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" "Unexpected gateway registration marker."
}

foreach ($scan in @(
    @{ category = "Scheduler"; check = "No scheduler/polling added"; patterns = @("PeriodicTimer", "System.Threading.Timer", "LmaxScheduler", "SecurityListPolling", "MarketDataPolling", "SnapshotPolling") },
    @{ category = "Replay"; check = "Runtime still does not submit to shadow replay"; patterns = @("SubmitToShadowReplay = true", "SubmittedToShadowReplay = true", "ReplaySubmitAsync") },
    @{ category = "Orders"; check = "No order surface"; patterns = @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "OrderStatusRequest", "TradeCaptureReportRequest", "SubmitOrder") },
    @{ category = "Mutation"; check = "No trading-state mutation references"; patterns = @("PersistTrade", "TradingState", "IOrderRepository", "IFillRepository", "IPositionRepository", "PersistLiveFix") }
)) {
    $hits = Get-Hits $startupFiles $scan.patterns
    if ($hits.Count -eq 0) {
        Add-Result $scan.category $scan.check "PASS" "No marker found in API/Worker startup."
    } else {
        Add-Result $scan.category $scan.check "FAIL" (($hits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
    }
}

Add-Result "Runtime" "External LMAX connection" "PASS" "This gate does not connect to LMAX."
Add-Result "Snapshot" "MarketData snapshot" "PASS" "This gate does not request snapshots."
Add-Result "Replay" "Automatic replay" "PASS" "This gate does not replay evidence."
Add-Result "Credentials" "Credential values" "PASS" "This gate does not require or print credential values."

$failed = @($results | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } else { "PASS_FREEZE_LIFT_SELECTION_RECORDED" }
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7k9-freeze-lift-decision-and-control-selection-gate-validation.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7K9"
    finalDecision = $decision
    externalConnectionAttempted = $false
    snapshotAttempted = $false
    replayAttempted = $false
    anyInstrumentRunAttempted = $false
    schedulerStarted = $false
    orderSubmissionAttempted = $false
    shadowReplaySubmitAttempted = $false
    tradingMutationAttempted = $false
    apiWorkerGatewayMode = "FakeLmaxGateway"
    selectedFutureAttemptInstrument = if ($null -ne $gate) { $gate.selectedFutureAttemptInstrument } else { $null }
    results = $results
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }
