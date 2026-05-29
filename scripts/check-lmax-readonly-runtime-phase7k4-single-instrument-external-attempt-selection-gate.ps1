param(
    [string]$SelectionGateFile = "artifacts/readiness/phase7k4-single-instrument-external-attempt-selection-gate.json",
    [string]$SelectionNoteFile = "artifacts/readiness/phase7k4-single-instrument-external-attempt-selection-note.md"
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
    $safe = $raw -replace 'credential|Credential','SAFE_METADATA'
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

Write-Host "LMAX Read-Only Runtime Phase 7K4 Single-Instrument External Attempt Selection Gate"
Write-Host "Local-only. This gate does not connect to LMAX, request snapshots, replay evidence, schedule work, or use credentials."

$gateRaw = Read-TextSafe $SelectionGateFile "SelectionGate"
$noteRaw = Read-TextSafe $SelectionNoteFile "SelectionNote"

if ($null -ne $gateRaw) {
    $gate = $gateRaw | ConvertFrom-Json
    if ([string]$gate.phase -eq "7K4") { Add-Result "SelectionGate" "Phase" "PASS" "7K4." } else { Add-Result "SelectionGate" "Phase" "FAIL" "Expected 7K4." }
    if ([bool]$gate.checklistComplete -and [string]$gate.phase7k3GateDecision -eq "PASS_OPERATOR_CHECKLIST_RECORDED") { Add-Result "SelectionGate" "Phase 7K3 complete" "PASS" "Checklist complete." } else { Add-Result "SelectionGate" "Phase 7K3 complete" "FAIL" "Expected completed Phase 7K3 gate." }
    if ([string]$gate.selectedFutureAttemptInstrument -eq "GBPUSD" -and [string]$gate.selectedFutureAttemptType -eq "KnownGoodControlSnapshot") { Add-Result "SelectionGate" "Selected candidate" "PASS" "GBPUSD known-good control." } else { Add-Result "SelectionGate" "Selected candidate" "FAIL" "Expected GBPUSD known-good control." }
    if ([string]$gate.selectedInstrumentSecurityId -eq "4002" -and [string]$gate.selectedInstrumentSecurityIdSource -eq "8") { Add-Result "SelectionGate" "Selected instrument identity" "PASS" "GBPUSD / 4002 / source 8." } else { Add-Result "SelectionGate" "Selected instrument identity" "FAIL" "Unexpected selected identity." }
    if ([string]$gate.selectedInstrumentEnvironment -eq "Demo" -and [string]$gate.selectedInstrumentVenueProfile -eq "DemoLondon" -and [string]$gate.selectedInstrumentRequestMode -eq "SnapshotPlusUpdates" -and [string]$gate.selectedInstrumentSymbolEncodingMode -eq "SecurityIdOnly" -and [int]$gate.selectedInstrumentMarketDepth -eq 1) {
        Add-Result "SelectionGate" "Selected instrument runtime profile" "PASS" "Demo/DemoLondon/SnapshotPlusUpdates/SecurityIdOnly/depth 1."
    } else {
        Add-Result "SelectionGate" "Selected instrument runtime profile" "FAIL" "Unexpected runtime profile."
    }
    if ([string]$gate.selectedInstrumentFinalPreRunGateDecision -eq "PASS" -and -not [string]::IsNullOrWhiteSpace([string]$gate.selectedInstrumentFinalPreRunGatePath) -and (Test-Path -LiteralPath (Resolve-LocalPath ([string]$gate.selectedInstrumentFinalPreRunGatePath)))) {
        Add-Result "SelectionGate" "Selected final pre-run gate" "PASS" $gate.selectedInstrumentFinalPreRunGatePath
    } else {
        Add-Result "SelectionGate" "Selected final pre-run gate" "FAIL" "Missing or non-PASS final pre-run gate."
    }
    if ([bool]$gate.exactlyOneFutureCandidateSelected -and [bool]$gate.futureExternalRunCanBeConsidered) { Add-Result "SelectionGate" "Exactly one candidate/future consideration" "PASS" "true/true." } else { Add-Result "SelectionGate" "Exactly one candidate/future consideration" "FAIL" "Expected true/true." }
    if (-not [bool]$gate.externalRunAttemptedInThisPhase -and -not [bool]$gate.snapshotRunInThisPhase -and -not [bool]$gate.replayRunInThisPhase -and -not [bool]$gate.controlRunInThisPhase -and -not [bool]$gate.audusdRunInThisPhase -and -not [bool]$gate.usdjpyRunInThisPhase -and -not [bool]$gate.gbpUsdRunInThisPhase) {
        Add-Result "SelectionGate" "No runs in phase" "PASS" "All phase execution flags false."
    } else {
        Add-Result "SelectionGate" "No runs in phase" "FAIL" "Unexpected phase execution flag."
    }
    if (-not [bool]$gate.externalAdditionalInstrumentAttemptsCurrentlyAllowed -and -not [bool]$gate.anyInstrumentExternalRunAllowed) { Add-Result "SelectionGate" "No direct run authorization" "PASS" "External run flags false." } else { Add-Result "SelectionGate" "No direct run authorization" "FAIL" "Unexpected direct run authorization." }
    if (-not [bool]$gate.batchExecutionAllowed -and -not [bool]$gate.automaticRetryRecommended -and -not [bool]$gate.wrapperValidationWeakened -and -not [bool]$gate.securityIdSwitchRecommended -and -not [bool]$gate.tokyo600xSwitchRecommended) {
        Add-Result "SelectionGate" "No batch/retry/wrapper/security switch" "PASS" "All false."
    } else {
        Add-Result "SelectionGate" "No batch/retry/wrapper/security switch" "FAIL" "Unsafe planning flag."
    }
    if (-not [bool]$gate.orderPathEnabled -and -not [bool]$gate.schedulerOrPollingEnabled -and -not [bool]$gate.runtimeShadowReplaySubmitEnabled -and -not [bool]$gate.tradingMutationEnabled -and -not [bool]$gate.gatewayRegistrationEnabled) {
        Add-Result "SelectionGate" "No runtime power enabled" "PASS" "All runtime power flags false."
    } else {
        Add-Result "SelectionGate" "No runtime power enabled" "FAIL" "Unexpected runtime power flag."
    }
    if ([string]$gate.finalDecision -eq "PASS_SELECTION_RECORDED") { Add-Result "SelectionGate" "Final decision" "PASS" $gate.finalDecision } else { Add-Result "SelectionGate" "Final decision" "FAIL" "Expected PASS_SELECTION_RECORDED." }
    $allowedNextPhase = [string]$gate.allowedNextPhase
    if ($allowedNextPhase.Contains("Phase 7K5") -and $allowedNextPhase.Contains("GBPUSD Known-Good Control Manual Market-Hours Snapshot Attempt")) { Add-Result "SelectionGate" "Allowed next phase" "PASS" $gate.allowedNextPhase } else { Add-Result "SelectionGate" "Allowed next phase" "FAIL" "Unexpected allowed next phase." }
    $disallowed = ($gate.disallowedActions | Out-String)
    foreach ($required in @("No external run in Phase 7K4", "No GBPUSD control run in Phase 7K4", "No EURGBP control run", "No USDJPY retry", "No AUDUSD retry", "No batch", "No loop", "No automatic retry", "No wrapper relaxation", "No SecurityID switch", "No Tokyo 600x switch")) {
        if ($disallowed -match [regex]::Escape($required)) { Add-Result "DisallowedActions" $required "PASS" "Present." } else { Add-Result "DisallowedActions" $required "FAIL" "Missing." }
    }
}

if ($null -ne $noteRaw) {
    foreach ($marker in @("Phase 7K3 operator checklist completion is recorded", "Selected candidate: GBPUSD", "does not authorize an external run", "Phase 7K5")) {
        if ($noteRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "SelectionNote" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "SelectionNote" "Marker: $marker" "FAIL" "Marker missing." }
    }
}

$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"
$startupFiles = @($apiProgram, $workerProgram)
$startupText = ($startupFiles | Where-Object { Test-Path -LiteralPath $_ } | ForEach-Object { Get-Content -Raw -LiteralPath $_ }) -join "`n"
if ($startupText.Contains("FakeLmaxGateway") -and -not ($startupText.Contains("RealLmaxGateway") -or $startupText.Contains("LmaxVenueGatewaySkeleton"))) { Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No real gateway registration found." } else { Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" "Unexpected gateway registration marker." }

foreach ($scan in @(
    @{ category = "Scheduler"; check = "No scheduler/polling added"; patterns = @("PeriodicTimer", "System.Threading.Timer", "LmaxScheduler", "SecurityListPolling", "MarketDataPolling", "SnapshotPolling") },
    @{ category = "Replay"; check = "Runtime still does not submit to shadow replay"; patterns = @("SubmitToShadowReplay = true", "SubmittedToShadowReplay = true", "ReplaySubmitAsync") },
    @{ category = "Orders"; check = "No order surface"; patterns = @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "OrderStatusRequest", "TradeCaptureReportRequest", "SubmitOrder") },
    @{ category = "Mutation"; check = "No trading-state mutation references"; patterns = @("PersistTrade", "TradingState", "IOrderRepository", "IFillRepository", "IPositionRepository", "PersistLiveFix") }
)) {
    $hits = Get-Hits $startupFiles $scan.patterns
    if ($hits.Count -eq 0) { Add-Result $scan.category $scan.check "PASS" "No marker found in API/Worker startup." } else { Add-Result $scan.category $scan.check "FAIL" (($hits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ") }
}

Add-Result "Runtime" "External LMAX connection" "PASS" "This gate does not connect to LMAX."
Add-Result "Snapshot" "MarketData snapshot" "PASS" "This gate does not request snapshots."
Add-Result "Replay" "Automatic replay" "PASS" "This gate does not replay evidence."
Add-Result "Credentials" "Credential values" "PASS" "This gate does not require or print credential values."

$failed = @($results | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } else { "PASS_SELECTION_RECORDED" }
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7k4-single-instrument-external-attempt-selection-gate-validation.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7K4"
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
    results = $results
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }
