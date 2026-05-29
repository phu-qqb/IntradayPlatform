param(
    [string]$DiffAuditReportFile = "artifacts/readiness/phase7i5-usdjpy-local-connection-session-config-diff-audit.json"
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

Write-Host "LMAX Read-Only Runtime Phase 7I5 USDJPY Local Config Diff Audit Gate"
Write-Host "Local-only. This gate does not connect to LMAX, request snapshots, replay evidence, schedule work, or use credentials."

$reportPath = Resolve-LocalPath $DiffAuditReportFile
if (Test-Path -LiteralPath $reportPath) {
    Add-Result "Audit" "Report exists" "PASS" $reportPath
    $raw = Get-Content -LiteralPath $reportPath -Raw
    $safe = $raw -replace 'credentialProfileName|usernamePresent|passwordPresent|usernameLength|passwordLength','SAFE_METADATA'
    if ($safe -match $sensitivePattern) { Add-Result "Audit" "No sensitive content" "FAIL" "Credential-shaped or raw FIX content found." } else { Add-Result "Audit" "No sensitive content" "PASS" "No credential-shaped or raw FIX content." }
    $report = $raw | ConvertFrom-Json
} else {
    Add-Result "Audit" "Report exists" "FAIL" "Missing $reportPath"
    $report = $null
}

if ($null -ne $report) {
    if ([string]$report.phase -eq "7I5" -and [string]$report.instrument -eq "USDJPY") { Add-Result "Audit" "Phase/instrument" "PASS" "7I5 / USDJPY." } else { Add-Result "Audit" "Phase/instrument" "FAIL" "Unexpected phase/instrument." }
    if ([int]$report.failedAttemptsAnalyzed -eq 2) { Add-Result "Audit" "Two failed attempts analyzed" "PASS" "failedAttemptsAnalyzed=2." } else { Add-Result "Audit" "Two failed attempts analyzed" "FAIL" "Expected failedAttemptsAnalyzed=2." }
    $successful = @($report.comparedSuccessfulInstruments)
    if ("GBPUSD" -in $successful -and "EURGBP" -in $successful) { Add-Result "Audit" "Successful comparisons include GBPUSD/EURGBP" "PASS" ($successful -join ", ") } else { Add-Result "Audit" "Successful comparisons include GBPUSD/EURGBP" "FAIL" "Missing comparison instrument." }
    if (-not [bool]$report.externalRunAttemptedInThisPhase -and -not [bool]$report.snapshotRunInThisPhase -and -not [bool]$report.replayRunInThisPhase) { Add-Result "Audit" "No run/snapshot/replay in phase" "PASS" "All phase attempt flags false." } else { Add-Result "Audit" "No run/snapshot/replay in phase" "FAIL" "Unexpected phase attempt flag." }
    if (-not [bool]$report.wrapperValidationWeakened -and -not [bool]$report.securityIdSwitchRecommended -and -not [bool]$report.tokyo600xSwitchRecommended -and -not [bool]$report.thirdRetryRecommended) { Add-Result "Audit" "No unsafe recommendations" "PASS" "Wrapper/security/retry recommendations safe." } else { Add-Result "Audit" "No unsafe recommendations" "FAIL" "Unsafe recommendation flag present." }
    if ([string]$report.finalDecision -in @("PASS","PASS_WITH_KNOWN_WARNINGS","PASS_WITH_ACTION_REQUIRED")) { Add-Result "Audit" "Accepted final decision" "PASS" $report.finalDecision } else { Add-Result "Audit" "Accepted final decision" "FAIL" "Unexpected final decision." }
    if ([string]$report.classification -in @("NoMaterialLocalConfigDiffFound_ExternalSessionIssueStillSuspected","MaterialInvocationPathDifferenceFound","MaterialGateFieldDifferenceFound","MaterialVenueProfileDifferenceFound","MaterialCredentialSourceDifferenceFound","MaterialArtifactSelectionDifferenceFound","MaterialConfigMetadataDifferenceFound")) { Add-Result "Audit" "Known classification" "PASS" $report.classification } else { Add-Result "Audit" "Known classification" "FAIL" "Unexpected classification." }
    $disallowed = ($report.disallowedActions | Out-String)
    foreach ($required in @("No third USDJPY retry", "No AUDUSD run", "No batch", "No loop", "No SecurityID switch", "No Tokyo 600x switch")) {
        if ($disallowed -match [regex]::Escape($required)) { Add-Result "DisallowedActions" $required "PASS" "Present." } else { Add-Result "DisallowedActions" $required "FAIL" "Missing." }
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
$decision = if ($failed.Count -gt 0) { "FAIL" } else { [string]$report.finalDecision }
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7i5-usdjpy-local-connection-session-config-diff-audit-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7I5"
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
