param(
    [string]$DecisionGateFile = "artifacts/readiness/phase7i6-usdjpy-operator-troubleshooting-decision-gate.json",
    [string]$TroubleshootingNoteFile = "artifacts/readiness/phase7i6-usdjpy-operator-troubleshooting-note.md"
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

function Test-NoSensitiveContent([string]$PathValue, [string]$Label) {
    $resolved = Resolve-LocalPath $PathValue
    if (-not (Test-Path -LiteralPath $resolved)) {
        Add-Result $Label "File exists" "FAIL" "Missing $resolved"
        return $null
    }

    Add-Result $Label "File exists" "PASS" $resolved
    $raw = Get-Content -LiteralPath $resolved -Raw
    $safe = $raw -replace 'credentialProfileName|usernamePresent|passwordPresent|usernameLength|passwordLength','SAFE_METADATA'
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

Write-Host "LMAX Read-Only Runtime Phase 7I6 USDJPY Operator Troubleshooting Decision Gate"
Write-Host "Local-only. This gate does not connect to LMAX, request snapshots, replay evidence, schedule work, or use credentials."

$gateRaw = Test-NoSensitiveContent $DecisionGateFile "DecisionGate"
$noteRaw = Test-NoSensitiveContent $TroubleshootingNoteFile "TroubleshootingNote"

if ($null -ne $gateRaw) {
    $gate = $gateRaw | ConvertFrom-Json
    if ([string]$gate.phase -eq "7I6" -and [string]$gate.instrument -eq "USDJPY" -and [string]$gate.securityId -eq "4004") { Add-Result "DecisionGate" "Phase/instrument" "PASS" "7I6 / USDJPY / 4004." } else { Add-Result "DecisionGate" "Phase/instrument" "FAIL" "Unexpected phase/instrument." }
    if ([string]$gate.finalDecision -eq "PASS_WITH_ACTION_REQUIRED") { Add-Result "DecisionGate" "Final decision" "PASS" $gate.finalDecision } else { Add-Result "DecisionGate" "Final decision" "FAIL" "Expected PASS_WITH_ACTION_REQUIRED." }
    if ([int]$gate.repeatedFailuresAnalyzed -eq 2) { Add-Result "DecisionGate" "Repeated failures analyzed" "PASS" "2." } else { Add-Result "DecisionGate" "Repeated failures analyzed" "FAIL" "Expected 2." }
    if ([string]$gate.localConfigDiffClassification -eq "NoMaterialLocalConfigDiffFound_ExternalSessionIssueStillSuspected") { Add-Result "DecisionGate" "Local diff classification" "PASS" $gate.localConfigDiffClassification } else { Add-Result "DecisionGate" "Local diff classification" "FAIL" "Unexpected classification." }
    if (-not [bool]$gate.externalRunAttemptedInThisPhase -and -not [bool]$gate.snapshotRunInThisPhase -and -not [bool]$gate.replayRunInThisPhase -and -not [bool]$gate.controlSnapshotRunInThisPhase -and -not [bool]$gate.audusdRunInThisPhase) {
        Add-Result "DecisionGate" "No run/snapshot/replay/control/AUDUSD in phase" "PASS" "All phase execution flags false."
    } else {
        Add-Result "DecisionGate" "No run/snapshot/replay/control/AUDUSD in phase" "FAIL" "Unexpected execution flag."
    }
    if (-not [bool]$gate.wrapperValidationWeakened -and -not [bool]$gate.securityIdSwitchRecommended -and -not [bool]$gate.tokyo600xSwitchRecommended -and -not [bool]$gate.automaticRetryRecommended -and -not [bool]$gate.thirdRetryCurrentlyAllowed) {
        Add-Result "DecisionGate" "No unsafe recommendations" "PASS" "Wrapper/security/retry flags safe."
    } else {
        Add-Result "DecisionGate" "No unsafe recommendations" "FAIL" "Unsafe recommendation flag present."
    }
    if ([bool]$gate.operatorTroubleshootingRequired) { Add-Result "DecisionGate" "Operator troubleshooting required" "PASS" "true." } else { Add-Result "DecisionGate" "Operator troubleshooting required" "FAIL" "Expected true." }
    $disallowed = ($gate.disallowedActions | Out-String)
    foreach ($required in @("No third USDJPY retry currently", "No AUDUSD run in USDJPY troubleshooting phase", "No batch", "No loop", "No automatic retry", "No wrapper relaxation", "No SecurityID switch", "No Tokyo 600x switch", "No replay without MarketDataOnly evidence")) {
        if ($disallowed -match [regex]::Escape($required)) { Add-Result "DisallowedActions" $required "PASS" "Present." } else { Add-Result "DisallowedActions" $required "FAIL" "Missing." }
    }
}

if ($null -ne $noteRaw) {
    foreach ($marker in @("USDJPY failed twice", "No MarketDataRequest was sent", "It does not prove SecurityID", "No third USDJPY retry is currently allowed", "Phase 7I7")) {
        if ($noteRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "TroubleshootingNote" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "TroubleshootingNote" "Marker: $marker" "FAIL" "Marker missing." }
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
$outPath = Join-Path $outDir "phase7i6-usdjpy-operator-troubleshooting-decision-gate-validation.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7I6"
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
