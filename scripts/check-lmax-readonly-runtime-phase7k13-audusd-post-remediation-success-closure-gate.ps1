param(
    [string]$ClosureGateFile = "artifacts/readiness/phase7k13-audusd-post-remediation-success-closure-gate.json",
    [string]$ClosureSummaryFile = "artifacts/readiness/phase7k13-audusd-post-remediation-success-closure-summary.json",
    [string]$ClosureNoteFile = "artifacts/readiness/phase7k13-audusd-post-remediation-success-closure-note.md"
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

Write-Host "LMAX Read-Only Runtime Phase 7K13 AUDUSD Post-Remediation Success Closure Gate"
Write-Host "Local-only. This gate does not connect to LMAX, request snapshots, replay evidence, schedule work, or use credentials."

$gateRaw = Read-TextSafe $ClosureGateFile "ClosureGate"
$summaryRaw = Read-TextSafe $ClosureSummaryFile "ClosureSummary"
$noteRaw = Read-TextSafe $ClosureNoteFile "ClosureNote"
$gate = $null

if ($null -ne $gateRaw) {
    $gate = $gateRaw | ConvertFrom-Json
    Assert-Equals $gate.phase "7K13" "ClosureGate" "Phase"
    Assert-Equals $gate.instrument "AUDUSD" "ClosureGate" "Instrument"
    Assert-Equals $gate.securityId "4007" "ClosureGate" "SecurityID"
    Assert-Equals $gate.securityIdSource "8" "ClosureGate" "SecurityIDSource"
    Assert-Equals $gate.environment "Demo" "ClosureGate" "Environment"
    Assert-Equals $gate.venueProfile "DemoLondon" "ClosureGate" "Venue profile"
    Assert-Equals $gate.requestMode "SnapshotPlusUpdates" "ClosureGate" "Request mode"
    Assert-Equals $gate.symbolEncodingMode "SecurityIdOnly" "ClosureGate" "Encoding mode"
    if ([int]$gate.marketDepth -eq 1) { Add-Result "ClosureGate" "Market depth" "PASS" "1." } else { Add-Result "ClosureGate" "Market depth" "FAIL" "Expected 1." }
    Assert-True $gate.audusdRecovered "ClosureGate" "AUDUSD recovered"
    Assert-Equals $gate.audusdSnapshotStatus "Completed" "ClosureGate" "AUDUSD snapshot status"
    Assert-True $gate.audusdSnapshotReceived "ClosureGate" "AUDUSD snapshot received"
    if ([int]$gate.audusdEntryCount -eq 2) { Add-Result "ClosureGate" "AUDUSD entry count" "PASS" "2." } else { Add-Result "ClosureGate" "AUDUSD entry count" "FAIL" "Expected 2." }
    Assert-Equals $gate.audusdEvidenceMode "MarketDataOnly" "ClosureGate" "AUDUSD evidence mode"
    Assert-Equals $gate.audusdEvidenceValidation "Ok" "ClosureGate" "AUDUSD evidence validation"
    Assert-Equals $gate.audusdClosureDecision "PASS" "ClosureGate" "AUDUSD closure decision"
    Assert-True $gate.postRemediationAdditionalInstrumentHealthy "ClosureGate" "Post-remediation additional instrument healthy"
    Assert-True $gate.gbpusdPostRemediationControlRecovered "ClosureGate" "GBPUSD post-remediation control recovered"
    Assert-True $gate.priorCrossInstrumentFailureResolvedForAudusd "ClosureGate" "Prior cross-instrument failure resolved for AUDUSD"
    Assert-True $gate.invalidSecurityIdNotProven "ClosureGate" "Invalid SecurityID not proven"
    Assert-True $gate.tokyo600xNotJustified "ClosureGate" "Tokyo 600x not justified"
    Assert-False $gate.marketDataRequestRejectObserved "ClosureGate" "No MarketDataRequestReject observed"
    Assert-True $gate.usdJpyRemainsParked "ClosureGate" "USDJPY remains parked"

    foreach ($flag in @(
        "directRunAuthorization",
        "anyInstrumentExternalRunAllowed",
        "externalAdditionalInstrumentAttemptsCurrentlyAllowed",
        "externalRunAttemptedInThisPhase",
        "snapshotRunInThisPhase",
        "replayRunInThisPhase",
        "audusdRunInThisPhase",
        "usdjpyRunInThisPhase",
        "gbpUsdRunInThisPhase",
        "eurGbpRunInThisPhase",
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
        Assert-False $gate.$flag "ClosureGate" $flag
    }

    Assert-Equals $gate.allowedNextPhase "Phase 7K14 - Post-Remediation Additional Instrument Portfolio Decision Gate, No External Run" "ClosureGate" "Allowed next phase"
    Assert-Equals $gate.finalDecision "PASS_AUDUSD_POST_REMEDIATION_SUCCESS_CLOSED" "ClosureGate" "Final decision"
    if ([bool]$gate.noSensitiveContent) { Add-Result "ClosureGate" "noSensitiveContent" "PASS" "true." } else { Add-Result "ClosureGate" "noSensitiveContent" "FAIL" "Expected true." }
}

if ($null -ne $summaryRaw) {
    $summary = $summaryRaw | ConvertFrom-Json
    Assert-Equals $summary.phase "7K13" "ClosureSummary" "Phase"
    Assert-Equals $summary.finalDecision "PASS_AUDUSD_POST_REMEDIATION_SUCCESS_CLOSED" "ClosureSummary" "Final decision"
    Assert-True $summary.conclusion.externalSessionLayerRecoveredForGbpusdAndAudusd "ClosureSummary" "External/session layer recovered for GBPUSD and AUDUSD"
    Assert-True $summary.conclusion.audusdDemoLondon4007ReadOnlyMarketDataEvidenceSucceeded "ClosureSummary" "AUDUSD DemoLondon 4007 evidence succeeded"
    Assert-True $summary.conclusion.noInstrumentLevelRejectObserved "ClosureSummary" "No instrument-level reject observed"
    Assert-True $summary.conclusion.noTokyo600xJustification "ClosureSummary" "No Tokyo 600x justification"
    Assert-True $summary.conclusion.usdJpyRemainsParkedForSeparateFutureTroubleshooting "ClosureSummary" "USDJPY remains parked"
}

if ($null -ne $noteRaw) {
    foreach ($marker in @("AUDUSD is now recovered", "likely external/session/environment availability", "No trading functionality was enabled", "API/Worker remain FakeLmaxGateway only", "USDJPY remains", "portfolio-level no-external-run decision")) {
        if ($noteRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "ClosureNote" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "ClosureNote" "Marker: $marker" "FAIL" "Marker missing." }
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
    if ($hits.Count -eq 0) { Add-Result $scan.category $scan.check "PASS" "No marker found in API/Worker startup." } else { Add-Result $scan.category $scan.check "FAIL" (($hits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ") }
}

Add-Result "Runtime" "External LMAX connection" "PASS" "This gate does not connect to LMAX."
Add-Result "Snapshot" "MarketData snapshot" "PASS" "This gate does not request snapshots."
Add-Result "Replay" "Automatic replay" "PASS" "This gate does not replay evidence."
Add-Result "Credentials" "Credential values" "PASS" "This gate does not require or print credential values."

$failed = @($results | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } else { "PASS_AUDUSD_POST_REMEDIATION_SUCCESS_CLOSED" }
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7k13-audusd-post-remediation-success-closure-gate-validation.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7K13"
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
