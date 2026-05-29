param(
    [string]$DiagnosticReportFile = "artifacts/readiness/phase7k-cross-instrument-post-success-connection-layer-pattern-analysis.json",
    [string]$DecisionGateFile = "artifacts/readiness/phase7k-cross-instrument-additional-instrument-external-attempt-stop-gate.json",
    [string]$OperatorNoteFile = "artifacts/readiness/phase7k-cross-instrument-operator-environment-note.md"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=|raw\s*fix)'

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
    $safe = $raw -replace 'credentialProfileName|credential labels|credential source|credentialValuesReturned|usernamePresent|passwordPresent|usernameLength|passwordLength|password','SAFE_METADATA'
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

Write-Host "LMAX Read-Only Runtime Phase 7K Cross-Instrument Connection Layer Pattern Gate"
Write-Host "Local-only. This gate does not connect to LMAX, request snapshots, replay evidence, schedule work, or use credentials."

$reportRaw = Read-TextSafe $DiagnosticReportFile "DiagnosticReport"
$gateRaw = Read-TextSafe $DecisionGateFile "DecisionGate"
$noteRaw = Read-TextSafe $OperatorNoteFile "OperatorNote"

if ($null -ne $reportRaw) {
    $report = $reportRaw | ConvertFrom-Json
    if ([string]$report.phase -eq "7K") { Add-Result "DiagnosticReport" "Phase" "PASS" "7K." } else { Add-Result "DiagnosticReport" "Phase" "FAIL" "Expected 7K." }
    if ([int]$report.successfulAttemptsAnalyzed -eq 2) { Add-Result "DiagnosticReport" "Successful attempts analyzed" "PASS" "2." } else { Add-Result "DiagnosticReport" "Successful attempts analyzed" "FAIL" "Expected 2." }
    if ([int]$report.failedAttemptsAnalyzed -eq 3) { Add-Result "DiagnosticReport" "Failed attempts analyzed" "PASS" "3." } else { Add-Result "DiagnosticReport" "Failed attempts analyzed" "FAIL" "Expected 3." }
    if ([bool]$report.repeatedCrossInstrumentFailurePattern) { Add-Result "DiagnosticReport" "Repeated cross-instrument pattern" "PASS" "true." } else { Add-Result "DiagnosticReport" "Repeated cross-instrument pattern" "FAIL" "Expected true." }
    if ([bool]$report.allFailuresBeforeLogon) { Add-Result "DiagnosticReport" "All failures before logon" "PASS" "true." } else { Add-Result "DiagnosticReport" "All failures before logon" "FAIL" "Expected true." }
    if ([bool]$report.allFailuresHadNoSnapshotRequest) { Add-Result "DiagnosticReport" "All failures had no snapshot request" "PASS" "true." } else { Add-Result "DiagnosticReport" "All failures had no snapshot request" "FAIL" "Expected true." }
    if ([bool]$report.allFailuresHadZeroRejects) { Add-Result "DiagnosticReport" "All failures had zero rejects" "PASS" "true." } else { Add-Result "DiagnosticReport" "All failures had zero rejects" "FAIL" "Expected true." }
    if (-not [bool]$report.instrumentLevelRejectsObserved -and -not [bool]$report.securityIdSwitchRecommended -and -not [bool]$report.tokyo600xSwitchRecommended -and [bool]$report.externalRetryStopRecommended) {
        Add-Result "DiagnosticReport" "No instrument/SecurityID overclaim" "PASS" "No reject observed; no SecurityID or Tokyo switch; stop recommended."
    } else {
        Add-Result "DiagnosticReport" "No instrument/SecurityID overclaim" "FAIL" "Unexpected reject or switch recommendation."
    }
    if ([string]$report.broaderFailureClass -eq "CrossInstrumentFailedSafeConnectionBeforeSessionEstablishment") { Add-Result "DiagnosticReport" "Failure class" "PASS" $report.broaderFailureClass } else { Add-Result "DiagnosticReport" "Failure class" "FAIL" "Unexpected class." }
    if ([string]$report.finalDecision -eq "PASS_WITH_ACTION_REQUIRED") { Add-Result "DiagnosticReport" "Final decision" "PASS" $report.finalDecision } else { Add-Result "DiagnosticReport" "Final decision" "FAIL" "Expected PASS_WITH_ACTION_REQUIRED." }
}

if ($null -ne $gateRaw) {
    $gate = $gateRaw | ConvertFrom-Json
    if ([string]$gate.phase -eq "7K") { Add-Result "DecisionGate" "Phase" "PASS" "7K." } else { Add-Result "DecisionGate" "Phase" "FAIL" "Expected 7K." }
    if (-not [bool]$gate.externalAdditionalInstrumentAttemptsCurrentlyAllowed -and -not [bool]$gate.anyInstrumentExternalRunAllowed -and -not [bool]$gate.audusdRetryAllowed -and -not [bool]$gate.usdjpyRetryAllowed -and -not [bool]$gate.gbpusdControlRunAllowed -and -not [bool]$gate.eurgbpControlRunAllowed) {
        Add-Result "DecisionGate" "All external attempts stopped" "PASS" "All future run permissions false."
    } else {
        Add-Result "DecisionGate" "All external attempts stopped" "FAIL" "Unexpected future run permission."
    }
    if ([bool]$gate.operatorEnvironmentTroubleshootingRequired) { Add-Result "DecisionGate" "Operator troubleshooting required" "PASS" "true." } else { Add-Result "DecisionGate" "Operator troubleshooting required" "FAIL" "Expected true." }
    if (-not [bool]$gate.externalRunAttemptedInThisPhase -and -not [bool]$gate.snapshotRunInThisPhase -and -not [bool]$gate.replayRunInThisPhase) {
        Add-Result "DecisionGate" "No run/snapshot/replay in phase" "PASS" "All phase execution flags false."
    } else {
        Add-Result "DecisionGate" "No run/snapshot/replay in phase" "FAIL" "Unexpected phase execution flag."
    }
    if (-not [bool]$gate.batchExecutionAllowed -and -not [bool]$gate.automaticRetryRecommended -and -not [bool]$gate.wrapperValidationWeakened) {
        Add-Result "DecisionGate" "No batch/retry/wrapper relaxation" "PASS" "All false."
    } else {
        Add-Result "DecisionGate" "No batch/retry/wrapper relaxation" "FAIL" "Unsafe gate flag."
    }
    if (-not [bool]$gate.orderPathEnabled -and -not [bool]$gate.schedulerOrPollingEnabled -and -not [bool]$gate.runtimeShadowReplaySubmitEnabled -and -not [bool]$gate.tradingMutationEnabled -and -not [bool]$gate.gatewayRegistrationEnabled) {
        Add-Result "DecisionGate" "No runtime power enabled" "PASS" "All runtime power flags false."
    } else {
        Add-Result "DecisionGate" "No runtime power enabled" "FAIL" "Unexpected runtime power flag."
    }
    if ([string]$gate.finalDecision -eq "PASS_WITH_ACTION_REQUIRED") { Add-Result "DecisionGate" "Final decision" "PASS" $gate.finalDecision } else { Add-Result "DecisionGate" "Final decision" "FAIL" "Expected PASS_WITH_ACTION_REQUIRED." }
    $disallowed = ($gate.disallowedActions | Out-String)
    foreach ($required in @("No USDJPY retry", "No AUDUSD retry", "No GBPUSD/EURGBP control run", "No next instrument", "No batch", "No loop", "No automatic retry", "No wrapper relaxation", "No SecurityID switch", "No Tokyo 600x switch")) {
        if ($disallowed -match [regex]::Escape($required)) { Add-Result "DisallowedActions" $required "PASS" "Present." } else { Add-Result "DisallowedActions" $required "FAIL" "Missing." }
    }
}

if ($null -ne $noteRaw) {
    foreach ($marker in @("Earlier GBPUSD and EURGBP", "USDJPY failed twice and AUDUSD failed once", "does not prove", "No further external additional-instrument attempts", "Phase 7K2")) {
        if ($noteRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "OperatorNote" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "OperatorNote" "Marker: $marker" "FAIL" "Marker missing." }
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
$decision = if ($failed.Count -gt 0) { "FAIL" } else { "PASS_WITH_ACTION_REQUIRED" }
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7k-cross-instrument-connection-layer-pattern-gate-validation.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7K"
    finalDecision = $decision
    externalConnectionAttempted = $false
    snapshotAttempted = $false
    replayAttempted = $false
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
