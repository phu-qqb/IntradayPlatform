param(
    [string]$PatternAnalysisReportFile = "artifacts/readiness/phase7i4-usdjpy-repeated-failedsafe-pattern-analysis.json"
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

function Get-Hits($Paths, $Patterns) {
    $existing = @($Paths | Where-Object { Test-Path -LiteralPath $_ })
    if ($existing.Count -eq 0) { return @() }
    return @(Select-String -Path $existing -Pattern $Patterns -SimpleMatch -ErrorAction SilentlyContinue)
}

Write-Host "LMAX Read-Only Runtime Phase 7I4 USDJPY Repeated FailedSafe Pattern Analysis Gate"
Write-Host "Local-only. This gate does not connect to LMAX, request snapshots, replay evidence, schedule work, or use credentials."

$reportPath = Resolve-LocalPath $PatternAnalysisReportFile
if (Test-Path -LiteralPath $reportPath) {
    Add-Result "Analysis" "Report exists" "PASS" $reportPath
    $raw = Get-Content -LiteralPath $reportPath -Raw
    $safe = $raw -replace 'credentialProfileName|usernamePresent|passwordPresent|usernameLength|passwordLength','SAFE_METADATA'
    if ($safe -match $sensitivePattern) { Add-Result "Analysis" "No sensitive content" "FAIL" "Credential-shaped or raw FIX content found." } else { Add-Result "Analysis" "No sensitive content" "PASS" "No credential-shaped or raw FIX content." }
    $report = $raw | ConvertFrom-Json
} else {
    Add-Result "Analysis" "Report exists" "FAIL" "Missing $reportPath"
    $report = $null
}

if ($null -ne $report) {
    if ([string]$report.phase -eq "7I4" -and [string]$report.instrument -eq "USDJPY" -and [string]$report.securityId -eq "4004") { Add-Result "Analysis" "USDJPY identity" "PASS" "USDJPY / 4004." } else { Add-Result "Analysis" "USDJPY identity" "FAIL" "Unexpected identity." }
    if ([int]$report.attemptsAnalyzed -eq 2) { Add-Result "Analysis" "Two attempts analyzed" "PASS" "attemptsAnalyzed=2." } else { Add-Result "Analysis" "Two attempts analyzed" "FAIL" "Expected attemptsAnalyzed=2." }
    if ([bool]$report.repeatedFailurePattern -and [string]$report.repeatedFailureClass -eq "FailedSafeConnectionBeforeSessionEstablishment") { Add-Result "Analysis" "Repeated failure pattern" "PASS" $report.repeatedFailureClass } else { Add-Result "Analysis" "Repeated failure pattern" "FAIL" "Repeated pattern/class mismatch." }
    if ([bool]$report.bothAttemptsFailedBeforeLogon -and [bool]$report.bothAttemptsHadNoSnapshotRequest -and [bool]$report.bothAttemptsHadZeroRejects) { Add-Result "Analysis" "Both attempts failed before request/reject" "PASS" "Before logon; no snapshot request; zero rejects." } else { Add-Result "Analysis" "Both attempts failed before request/reject" "FAIL" "Unexpected attempt pattern." }
    if ([bool]$report.securityIdNotBlamed -and [bool]$report.tokyo600xSwitchDisallowed) { Add-Result "Analysis" "SecurityID/Tokyo switch guarded" "PASS" "4004 retained; Tokyo 600x disallowed." } else { Add-Result "Analysis" "SecurityID/Tokyo switch guarded" "FAIL" "SecurityID or Tokyo switch guard missing." }
    if ([bool]$report.externalRetryStopRecommended) { Add-Result "Analysis" "Stop external retries" "PASS" "externalRetryStopRecommended=true." } else { Add-Result "Analysis" "Stop external retries" "FAIL" "Expected retry stop recommendation." }
    if ([string]$report.finalDecision -eq "PASS_WITH_KNOWN_WARNINGS") { Add-Result "Analysis" "Safe warning decision" "PASS" $report.finalDecision } else { Add-Result "Analysis" "Safe warning decision" "FAIL" "Expected PASS_WITH_KNOWN_WARNINGS." }
    $disallowed = ($report.disallowedActions | Out-String)
    foreach ($required in @("No third USDJPY retry", "No AUDUSD run", "No batch", "No loop", "No SecurityID switch", "No Tokyo 600x switch", "No replay", "No MarketDataOnly preview fabrication")) {
        if ($disallowed -match [regex]::Escape($required)) { Add-Result "DisallowedActions" $required "PASS" "Present." } else { Add-Result "DisallowedActions" $required "FAIL" "Missing." }
    }
    $ruledOut = ($report.ruledOutCauses | Out-String)
    if ($ruledOut -match "Not MarketDataRequestReject" -and $ruledOut -match "Not proven invalid SecurityID" -and $ruledOut -match "Not Tokyo 600x requirement") {
        Add-Result "Analysis" "Ruled-out causes include reject/security guards" "PASS" "Reject/security guards present."
    } else {
        Add-Result "Analysis" "Ruled-out causes include reject/security guards" "FAIL" "Missing reject/security guard."
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
$decision = if ($failed.Count -gt 0) { "FAIL" } else { "PASS_WITH_KNOWN_WARNINGS" }
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7i4-usdjpy-repeated-failedsafe-pattern-analysis-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7I4"
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
