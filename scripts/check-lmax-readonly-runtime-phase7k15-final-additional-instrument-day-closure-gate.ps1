param(
    [string]$EvidencePackFile = "artifacts/readiness/phase7k15-final-additional-instrument-readonly-evidence-pack.json",
    [string]$DayClosureGateFile = "artifacts/readiness/phase7k15-final-additional-instrument-day-closure-gate.json",
    [string]$MarkdownReportFile = "artifacts/readiness/phase7k15-final-additional-instrument-readonly-evidence-pack.md"
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

Write-Host "LMAX Read-Only Runtime Phase 7K15 Final Additional-Instrument Day Closure Gate"
Write-Host "Local-only. This gate does not connect to LMAX, request snapshots, replay evidence, schedule work, or use credentials."

$packRaw = Read-TextSafe $EvidencePackFile "EvidencePack"
$gateRaw = Read-TextSafe $DayClosureGateFile "DayClosureGate"
$reportRaw = Read-TextSafe $MarkdownReportFile "MarkdownReport"
$pack = $null
$gate = $null

if ($null -ne $packRaw) {
    $pack = $packRaw | ConvertFrom-Json
    Assert-Equals $pack.phase "7K15" "EvidencePack" "Phase"
    Assert-Equals $pack.evidencePackType "FinalAdditionalInstrumentReadOnlyEvidencePack" "EvidencePack" "Evidence pack type"
    Assert-True $pack.dayClosure "EvidencePack" "Day closure"
    Assert-True $pack.externalAttemptCycleClosed "EvidencePack" "External attempt cycle closed"
    Assert-Equals $pack.portfolioDecision "StopExternalAttemptsForDay" "EvidencePack" "Portfolio decision"
    foreach ($instrument in @("GBPUSD", "EURGBP", "AUDUSD")) {
        if (@($pack.successfulReadOnlyEvidenceInstruments) -contains $instrument) {
            Add-Result "EvidencePack" "Successful evidence includes $instrument" "PASS" "Present."
        } else {
            Add-Result "EvidencePack" "Successful evidence includes $instrument" "FAIL" "Missing."
        }
    }
    if (@($pack.parkedInstruments) -contains "USDJPY") { Add-Result "EvidencePack" "Parked instruments include USDJPY" "PASS" "Present." } else { Add-Result "EvidencePack" "Parked instruments include USDJPY" "FAIL" "Missing." }
    Assert-True $pack.lmaxDemoReadOnlyEvidenceCompleteForCurrentCycle "EvidencePack" "Read-only evidence complete for current cycle"
    Assert-True $pack.marketDataOnlyEvidenceAvailable "EvidencePack" "MarketDataOnly evidence available"
    Assert-False $pack.orderSubmissionObserved "EvidencePack" "No order submission observed"
    Assert-False $pack.schedulerOrPollingObserved "EvidencePack" "No scheduler/polling observed"
    Assert-False $pack.runtimeShadowReplaySubmitObserved "EvidencePack" "No runtime shadow replay submit observed"
    Assert-False $pack.tradingMutationObserved "EvidencePack" "No trading mutation observed"
    Assert-False $pack.gatewayRegistrationObserved "EvidencePack" "No gateway registration observed"
    Assert-False $pack.credentialValuesReturned "EvidencePack" "Credential values not returned"
    Assert-True $pack.noSensitiveContent "EvidencePack" "No sensitive content"
    Assert-True $pack.apiWorkerRemainFakeLmaxGatewayOnly "EvidencePack" "API/Worker remain FakeLmaxGateway only"
    Assert-Equals $pack.knownLocalIssue "LocalhostApiHealthTimeoutAffectedOptionalReplayOnly" "EvidencePack" "Known local issue"
    Assert-Equals $pack.usdJpyStatus "ParkedSeparateTroubleshootingRail" "EvidencePack" "USDJPY status"
    Assert-Equals $pack.allowedNextPhase "Phase 7K16 - Final Operator Signoff and Readiness Documentation Update, No External Run" "EvidencePack" "Allowed next phase"
    Assert-Equals $pack.finalDecision "PASS_FINAL_ADDITIONAL_INSTRUMENT_EVIDENCE_PACK_CLOSED" "EvidencePack" "Final decision"
    if (@($pack.evidenceItems).Count -ge 10) { Add-Result "EvidencePack" "Evidence item count" "PASS" "$(@($pack.evidenceItems).Count) items." } else { Add-Result "EvidencePack" "Evidence item count" "FAIL" "Expected at least 10 evidence items." }
}

if ($null -ne $gateRaw) {
    $gate = $gateRaw | ConvertFrom-Json
    Assert-Equals $gate.phase "7K15" "DayClosureGate" "Phase"
    Assert-Equals $gate.dayClosureDecision "CloseAdditionalInstrumentExternalAttemptCycle" "DayClosureGate" "Day closure decision"
    Assert-True $gate.externalAttemptCycleClosed "DayClosureGate" "External attempt cycle closed"
    foreach ($flag in @(
        "anyInstrumentExternalRunAllowed",
        "externalAdditionalInstrumentAttemptsCurrentlyAllowed",
        "gbpusdControlRunAllowed",
        "eurgbpControlRunAllowed",
        "audusdRetryAllowed",
        "usdjpyRetryAllowed",
        "nextInstrumentRunAllowed",
        "directRunAuthorization",
        "futureExternalRunCanBeConsidered",
        "immediateNextExternalRunRecommended",
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
        Assert-False $gate.$flag "DayClosureGate" $flag
    }
    Assert-True $gate.apiWorkerRemainFakeLmaxGatewayOnly "DayClosureGate" "API/Worker remain FakeLmaxGateway only"
    Assert-Equals $gate.allowedNextPhase "Phase 7K16 - Final Operator Signoff and Readiness Documentation Update, No External Run" "DayClosureGate" "Allowed next phase"
    Assert-Equals $gate.finalDecision "PASS_FINAL_ADDITIONAL_INSTRUMENT_EVIDENCE_PACK_CLOSED" "DayClosureGate" "Final decision"
    if ([bool]$gate.noSensitiveContent) { Add-Result "DayClosureGate" "noSensitiveContent" "PASS" "true." } else { Add-Result "DayClosureGate" "noSensitiveContent" "FAIL" "Expected true." }
}

if ($null -ne $reportRaw) {
    foreach ($marker in @("GBPUSD initial", "EURGBP completed", "AUDUSD completed", "USDJPY remains parked", "No orders", "FakeLmaxGateway", "localhost API health timeout", "No more external attempts")) {
        if ($reportRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            Add-Result "MarkdownReport" "Marker: $marker" "PASS" "Marker found."
        } else {
            Add-Result "MarkdownReport" "Marker: $marker" "FAIL" "Marker missing."
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
$decision = if ($failed.Count -gt 0) { "FAIL" } else { "PASS_FINAL_ADDITIONAL_INSTRUMENT_EVIDENCE_PACK_CLOSED" }
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7k15-final-additional-instrument-day-closure-gate-validation.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7K15"
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
