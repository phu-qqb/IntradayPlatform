param(
    [string]$PlanningGateFile = "artifacts/readiness/phase7j-audusd-next-instrument-planning-gate.json",
    [string]$PlanningNoteFile = "artifacts/readiness/phase7j-audusd-next-instrument-planning-note.md"
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
    $safe = $raw -replace 'credentialProfileName|usernamePresent|passwordPresent|usernameLength|passwordLength','SAFE_METADATA'
    if ($safe -match $sensitivePattern) { Add-Result $Label "No sensitive content" "FAIL" "Credential-shaped or raw FIX content found." } else { Add-Result $Label "No sensitive content" "PASS" "No credential-shaped or raw FIX content." }
    return $raw
}

function Get-Hits($Paths, $Patterns) {
    $existing = @($Paths | Where-Object { Test-Path -LiteralPath $_ })
    if ($existing.Count -eq 0) { return @() }
    return @(Select-String -Path $existing -Pattern $Patterns -SimpleMatch -ErrorAction SilentlyContinue)
}

Write-Host "LMAX Read-Only Runtime Phase 7J AUDUSD Next Instrument Planning Gate"
Write-Host "Local-only. This gate does not connect to LMAX, request snapshots, replay evidence, schedule work, or use credentials."

$gateRaw = Read-TextSafe $PlanningGateFile "PlanningGate"
$noteRaw = Read-TextSafe $PlanningNoteFile "PlanningNote"

if ($null -ne $gateRaw) {
    $gate = $gateRaw | ConvertFrom-Json
    if ([string]$gate.phase -eq "7J") { Add-Result "PlanningGate" "Phase" "PASS" "7J." } else { Add-Result "PlanningGate" "Phase" "FAIL" "Expected 7J." }
    if ([string]$gate.instrument -eq "AUDUSD" -and [string]$gate.securityId -eq "4007" -and [string]$gate.slashSymbol -eq "AUD/USD") { Add-Result "PlanningGate" "AUDUSD identity" "PASS" "AUDUSD / AUD/USD / 4007." } else { Add-Result "PlanningGate" "AUDUSD identity" "FAIL" "Unexpected AUDUSD identity." }
    if ([string]$gate.audusdFinalPreRunGateDecision -eq "PASS") { Add-Result "PlanningGate" "AUDUSD final pre-run gate PASS" "PASS" "PASS." } else { Add-Result "PlanningGate" "AUDUSD final pre-run gate PASS" "FAIL" "Expected PASS." }
    if (-not [bool]$gate.usdjpyRetryAllowed) { Add-Result "PlanningGate" "USDJPY retry blocked" "PASS" "usdjpyRetryAllowed=false." } else { Add-Result "PlanningGate" "USDJPY retry blocked" "FAIL" "USDJPY retry was allowed." }
    if (-not [bool]$gate.externalRunAttemptedInThisPhase -and -not [bool]$gate.snapshotRunInThisPhase -and -not [bool]$gate.replayRunInThisPhase -and -not [bool]$gate.audusdRunInThisPhase -and -not [bool]$gate.usdjpyRunInThisPhase -and -not [bool]$gate.controlSnapshotRunInThisPhase) {
        Add-Result "PlanningGate" "No runs in this phase" "PASS" "All phase execution flags false."
    } else {
        Add-Result "PlanningGate" "No runs in this phase" "FAIL" "Unexpected phase execution flag."
    }
    if (-not [bool]$gate.batchExecutionAllowed -and [bool]$gate.oneInstrumentAtATime) { Add-Result "PlanningGate" "One instrument/no batch" "PASS" "oneInstrumentAtATime=true; batchExecutionAllowed=false." } else { Add-Result "PlanningGate" "One instrument/no batch" "FAIL" "Unexpected batch/sequence flags." }
    if (-not [bool]$gate.wrapperValidationWeakened -and -not [bool]$gate.automaticRetryRecommended -and [bool]$gate.safeToProceedToFutureAudusdManualSnapshot) { Add-Result "PlanningGate" "Future AUDUSD planning safe" "PASS" "Wrapper unchanged; no auto retry; future AUDUSD planning allowed." } else { Add-Result "PlanningGate" "Future AUDUSD planning safe" "FAIL" "Unsafe planning flags." }
    $allowedNextPhase = [string]$gate.allowedNextPhase
    if ($allowedNextPhase.Contains("Phase 7J2") -and $allowedNextPhase.Contains("AUDUSD One-Instrument Manual Market-Hours Snapshot Attempt")) { Add-Result "PlanningGate" "Allowed next phase" "PASS" $gate.allowedNextPhase } else { Add-Result "PlanningGate" "Allowed next phase" "FAIL" "Unexpected allowed next phase." }
    $disallowed = ($gate.disallowedActions | Out-String)
    foreach ($required in @("No USDJPY retry", "No AUDUSD run in this phase", "No batch", "No loop", "No automatic retry", "No wrapper relaxation")) {
        if ($disallowed -match [regex]::Escape($required)) { Add-Result "DisallowedActions" $required "PASS" "Present." } else { Add-Result "DisallowedActions" $required "FAIL" "Missing." }
    }
    if ([string]$gate.finalDecision -eq "PASS") { Add-Result "PlanningGate" "Final decision" "PASS" "PASS." } else { Add-Result "PlanningGate" "Final decision" "FAIL" "Expected PASS." }
}

if ($null -ne $noteRaw) {
    foreach ($marker in @("USDJPY is parked", "AUDUSD is the next clean additional instrument", "This note is planning-only", "Phase 7J2")) {
        if ($noteRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "PlanningNote" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "PlanningNote" "Marker: $marker" "FAIL" "Marker missing." }
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
$decision = if ($failed.Count -gt 0) { "FAIL" } else { "PASS" }
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7j-audusd-next-instrument-planning-gate-validation.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7J"
    finalDecision = $decision
    externalConnectionAttempted = $false
    snapshotAttempted = $false
    replayAttempted = $false
    audusdRunAttempted = $false
    usdjpyRunAttempted = $false
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
