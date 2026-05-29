param(
    [string]$PortfolioGateFile = "artifacts/readiness/phase7k14-post-remediation-additional-instrument-portfolio-decision-gate.json",
    [string]$PortfolioSummaryFile = "artifacts/readiness/phase7k14-post-remediation-additional-instrument-portfolio-summary.json",
    [string]$PortfolioNoteFile = "artifacts/readiness/phase7k14-post-remediation-additional-instrument-portfolio-note.md"
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

Write-Host "LMAX Read-Only Runtime Phase 7K14 Portfolio Decision Gate"
Write-Host "Local-only. This gate does not connect to LMAX, request snapshots, replay evidence, schedule work, or use credentials."

$gateRaw = Read-TextSafe $PortfolioGateFile "PortfolioGate"
$summaryRaw = Read-TextSafe $PortfolioSummaryFile "PortfolioSummary"
$noteRaw = Read-TextSafe $PortfolioNoteFile "PortfolioNote"
$gate = $null

if ($null -ne $gateRaw) {
    $gate = $gateRaw | ConvertFrom-Json
    Assert-Equals $gate.phase "7K14" "PortfolioGate" "Phase"
    Assert-Equals $gate.portfolioDecision "StopExternalAttemptsForDay" "PortfolioGate" "Portfolio decision"
    Assert-True $gate.externalAttemptCycleClosed "PortfolioGate" "External attempt cycle closed"
    foreach ($instrument in @("GBPUSD", "EURGBP", "AUDUSD")) {
        if (@($gate.successfulReadOnlyEvidenceInstruments) -contains $instrument) {
            Add-Result "PortfolioGate" "Successful evidence includes $instrument" "PASS" "Present."
        } else {
            Add-Result "PortfolioGate" "Successful evidence includes $instrument" "FAIL" "Missing."
        }
    }
    Assert-Equals $gate.gbpusdEvidenceStatus "Completed" "PortfolioGate" "GBPUSD evidence status"
    Assert-Equals $gate.eurgbpEvidenceStatus "Completed" "PortfolioGate" "EURGBP evidence status"
    Assert-Equals $gate.audusdEvidenceStatus "Completed" "PortfolioGate" "AUDUSD evidence status"
    Assert-True $gate.audusdPostRemediationRecovered "PortfolioGate" "AUDUSD post-remediation recovered"
    Assert-True $gate.gbpusdPostRemediationControlRecovered "PortfolioGate" "GBPUSD post-remediation control recovered"
    Assert-Equals $gate.usdJpyStatus "ParkedSeparateTroubleshootingRail" "PortfolioGate" "USDJPY status"

    foreach ($flag in @(
        "usdJpyRetryRecommended",
        "usdJpyRetryAllowed",
        "eurgbpConfirmationRecommended",
        "audusdRetryRecommended",
        "nextInstrumentRunRecommended",
        "batchExecutionRecommended",
        "automaticRetryRecommended",
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
        "wrapperValidationWeakened",
        "securityIdSwitchRecommended",
        "tokyo600xSwitchRecommended",
        "orderPathEnabled",
        "schedulerOrPollingEnabled",
        "runtimeShadowReplaySubmitEnabled",
        "tradingMutationEnabled",
        "gatewayRegistrationEnabled"
    )) {
        Assert-False $gate.$flag "PortfolioGate" $flag
    }

    Assert-Equals $gate.allowedNextPhase "Phase 7K15 - Final Additional-Instrument Read-Only Evidence Pack and Day Closure, No External Run" "PortfolioGate" "Allowed next phase"
    Assert-Equals $gate.finalDecision "PASS_PORTFOLIO_DECISION_STOP_EXTERNAL_ATTEMPTS_FOR_DAY" "PortfolioGate" "Final decision"
    if ([bool]$gate.noSensitiveContent) { Add-Result "PortfolioGate" "noSensitiveContent" "PASS" "true." } else { Add-Result "PortfolioGate" "noSensitiveContent" "FAIL" "Expected true." }
}

if ($null -ne $summaryRaw) {
    $summary = $summaryRaw | ConvertFrom-Json
    Assert-Equals $summary.phase "7K14" "PortfolioSummary" "Phase"
    Assert-Equals $summary.finalDecision "PASS_PORTFOLIO_DECISION_STOP_EXTERNAL_ATTEMPTS_FOR_DAY" "PortfolioSummary" "Final decision"
    Assert-True $summary.recoveredEnvironmentSessionInterpretation.preRemediationFailuresLikelyExternalSessionEnvironment "PortfolioSummary" "Pre-remediation failures interpreted as external/session/environment"
    Assert-True $summary.recoveredEnvironmentSessionInterpretation.postRemediationGbpusdAndAudusdSuccessesSupportRecovery "PortfolioSummary" "GBPUSD/AUDUSD successes support recovery"
    Assert-False $summary.safetyPosture.orderPathEnabled "PortfolioSummary" "No order path"
    Assert-False $summary.safetyPosture.schedulerOrPollingEnabled "PortfolioSummary" "No scheduler/polling"
    Assert-False $summary.safetyPosture.runtimeShadowReplaySubmitEnabled "PortfolioSummary" "No runtime shadow replay submit"
    Assert-False $summary.safetyPosture.tradingMutationEnabled "PortfolioSummary" "No trading mutation"
    Assert-Equals $summary.safetyPosture.apiWorkerGatewayMode "FakeLmaxGateway" "PortfolioSummary" "API/Worker gateway mode"
}

if ($null -ne $noteRaw) {
    foreach ($marker in @("evidence set is enough for today", "More external attempts would add risk", "USDJPY remains", "package the evidence", "Do not run more snapshots")) {
        if ($noteRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            Add-Result "PortfolioNote" "Marker: $marker" "PASS" "Marker found."
        } else {
            Add-Result "PortfolioNote" "Marker: $marker" "FAIL" "Marker missing."
        }
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
$decision = if ($failed.Count -gt 0) { "FAIL" } else { "PASS_PORTFOLIO_DECISION_STOP_EXTERNAL_ATTEMPTS_FOR_DAY" }
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7k14-post-remediation-additional-instrument-portfolio-decision-gate-validation.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7K14"
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
