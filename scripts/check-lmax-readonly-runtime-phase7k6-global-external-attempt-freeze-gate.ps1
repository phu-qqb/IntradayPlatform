param(
    [string]$GlobalFreezeGateFile = "artifacts/readiness/phase7k6-global-external-attempt-freeze-gate.json",
    [string]$DiagnosticSummaryFile = "artifacts/readiness/phase7k6-global-external-attempt-freeze-summary.json",
    [string]$FreezeNoteFile = "artifacts/readiness/phase7k6-global-external-attempt-freeze-note.md"
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

Write-Host "LMAX Read-Only Runtime Phase 7K6 Global External Attempt Freeze Gate"
Write-Host "Local-only. This gate does not connect to LMAX, request snapshots, replay evidence, schedule work, or use credentials."

$gateRaw = Read-TextSafe $GlobalFreezeGateFile "GlobalFreezeGate"
$summaryRaw = Read-TextSafe $DiagnosticSummaryFile "DiagnosticSummary"
$noteRaw = Read-TextSafe $FreezeNoteFile "FreezeNote"

if ($null -ne $gateRaw) {
    $gate = $gateRaw | ConvertFrom-Json
    if ([string]$gate.phase -eq "7K6") { Add-Result "GlobalFreezeGate" "Phase" "PASS" "7K6." } else { Add-Result "GlobalFreezeGate" "Phase" "FAIL" "Expected 7K6." }
    if ([string]$gate.freezeReason -eq "KnownGoodControlFailedBeforeLogonAfterPriorSuccess") { Add-Result "GlobalFreezeGate" "Freeze reason" "PASS" $gate.freezeReason } else { Add-Result "GlobalFreezeGate" "Freeze reason" "FAIL" "Unexpected freeze reason." }
    if ([bool]$gate.globalExternalAttemptFreeze) { Add-Result "GlobalFreezeGate" "Global freeze" "PASS" "true." } else { Add-Result "GlobalFreezeGate" "Global freeze" "FAIL" "Expected true." }
    if (-not [bool]$gate.anyInstrumentExternalRunAllowed -and -not [bool]$gate.externalAdditionalInstrumentAttemptsCurrentlyAllowed -and -not [bool]$gate.gbpusdControlRunAllowed -and -not [bool]$gate.eurgbpControlRunAllowed -and -not [bool]$gate.audusdRetryAllowed -and -not [bool]$gate.usdjpyRetryAllowed -and -not [bool]$gate.nextInstrumentRunAllowed -and -not [bool]$gate.futureExternalRunCanBeConsidered) {
        Add-Result "GlobalFreezeGate" "All external attempts frozen" "PASS" "All future run permissions false."
    } else {
        Add-Result "GlobalFreezeGate" "All external attempts frozen" "FAIL" "Unexpected future run permission."
    }
    if ([bool]$gate.operatorEnvironmentChecklistPreviouslyCompleted -and -not [bool]$gate.knownGoodControlRecovered -and [bool]$gate.broaderEnvironmentSessionIssueStillSuspected -and [string]$gate.failedKnownGoodControlInstrument -eq "GBPUSD" -and [string]$gate.failedKnownGoodControlSecurityId -eq "4002") {
        Add-Result "GlobalFreezeGate" "Known-good control interpretation" "PASS" "GBPUSD 4002 failed before logon after prior success."
    } else {
        Add-Result "GlobalFreezeGate" "Known-good control interpretation" "FAIL" "Unexpected known-good control fields."
    }
    if (-not [bool]$gate.externalRunAttemptedInThisPhase -and -not [bool]$gate.snapshotRunInThisPhase -and -not [bool]$gate.replayRunInThisPhase -and -not [bool]$gate.controlRunInThisPhase) {
        Add-Result "GlobalFreezeGate" "No execution in phase" "PASS" "All phase execution flags false."
    } else {
        Add-Result "GlobalFreezeGate" "No execution in phase" "FAIL" "Unexpected execution flag."
    }
    if (-not [bool]$gate.batchExecutionAllowed -and -not [bool]$gate.automaticRetryRecommended -and -not [bool]$gate.wrapperValidationWeakened -and -not [bool]$gate.securityIdSwitchRecommended -and -not [bool]$gate.tokyo600xSwitchRecommended) {
        Add-Result "GlobalFreezeGate" "No retry/batch/wrapper/security switch" "PASS" "All false."
    } else {
        Add-Result "GlobalFreezeGate" "No retry/batch/wrapper/security switch" "FAIL" "Unsafe recommendation flag."
    }
    if (-not [bool]$gate.orderPathEnabled -and -not [bool]$gate.schedulerOrPollingEnabled -and -not [bool]$gate.runtimeShadowReplaySubmitEnabled -and -not [bool]$gate.tradingMutationEnabled -and -not [bool]$gate.gatewayRegistrationEnabled) {
        Add-Result "GlobalFreezeGate" "No runtime power enabled" "PASS" "All runtime power flags false."
    } else {
        Add-Result "GlobalFreezeGate" "No runtime power enabled" "FAIL" "Unexpected runtime power flag."
    }
    if ([string]$gate.finalDecision -eq "PASS_GLOBAL_FREEZE_RECORDED") { Add-Result "GlobalFreezeGate" "Final decision" "PASS" $gate.finalDecision } else { Add-Result "GlobalFreezeGate" "Final decision" "FAIL" "Expected PASS_GLOBAL_FREEZE_RECORDED." }
    if ([string]$gate.allowedNextPhase -eq "Phase 7K7 - External Session Remediation Plan, No External Run") { Add-Result "GlobalFreezeGate" "Allowed next phase" "PASS" $gate.allowedNextPhase } else { Add-Result "GlobalFreezeGate" "Allowed next phase" "FAIL" "Unexpected allowed next phase." }
    $disallowed = ($gate.disallowedActions | Out-String)
    foreach ($required in @("No external run", "No GBPUSD control retry", "No EURGBP control run", "No AUDUSD retry", "No USDJPY retry", "No next instrument run", "No batch", "No loop", "No automatic retry", "No wrapper relaxation", "No SecurityID switch", "No Tokyo 600x switch")) {
        if ($disallowed -match [regex]::Escape($required)) { Add-Result "DisallowedActions" $required "PASS" "Present." } else { Add-Result "DisallowedActions" $required "FAIL" "Missing." }
    }
}

if ($null -ne $summaryRaw) {
    $summary = $summaryRaw | ConvertFrom-Json
    if ([string]$summary.phase -eq "7K6") { Add-Result "DiagnosticSummary" "Phase" "PASS" "7K6." } else { Add-Result "DiagnosticSummary" "Phase" "FAIL" "Expected 7K6." }
    if (@($summary.successfulEarlierAttempts).Count -eq 2 -and @($summary.laterFailedAttempts).Count -eq 4) { Add-Result "DiagnosticSummary" "Attempt counts" "PASS" "2 earlier successes / 4 later failures." } else { Add-Result "DiagnosticSummary" "Attempt counts" "FAIL" "Unexpected attempt counts." }
    if ([bool]$summary.allLaterFailuresBeforeLogon -and [bool]$summary.allLaterFailuresHadNoSnapshotRequest -and [bool]$summary.allLaterFailuresHadZeroRejects) { Add-Result "DiagnosticSummary" "Later failure pattern" "PASS" "Before logon/no request/zero rejects." } else { Add-Result "DiagnosticSummary" "Later failure pattern" "FAIL" "Unexpected later failure pattern." }
    if (-not [bool]$summary.instrumentLevelRejectsObserved -and -not [bool]$summary.marketDataRequestRejectObserved -and [bool]$summary.invalidSecurityIdNotProven -and [bool]$summary.tokyo600xNotJustified -and [bool]$summary.environmentSessionLayerSuspected) {
        Add-Result "DiagnosticSummary" "No instrument/SecurityID overclaim" "PASS" "Environment/session layer suspected."
    } else {
        Add-Result "DiagnosticSummary" "No instrument/SecurityID overclaim" "FAIL" "Unexpected summary classification."
    }
    if ([string]$summary.recommendedOperationalState -eq "ExternalAttemptsFrozen") { Add-Result "DiagnosticSummary" "Recommended operational state" "PASS" $summary.recommendedOperationalState } else { Add-Result "DiagnosticSummary" "Recommended operational state" "FAIL" "Unexpected state." }
}

if ($null -ne $noteRaw) {
    foreach ($marker in @("Earlier GBPUSD and EURGBP snapshots proved", "broader environment, session, or endpoint availability issue", "does not prove invalid SecurityIDs", "No more external attempts are allowed", "Phase 7K7")) {
        if ($noteRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "FreezeNote" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "FreezeNote" "Marker: $marker" "FAIL" "Marker missing." }
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
$decision = if ($failed.Count -gt 0) { "FAIL" } else { "PASS_GLOBAL_FREEZE_RECORDED" }
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7k6-global-external-attempt-freeze-gate-validation.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7K6"
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
