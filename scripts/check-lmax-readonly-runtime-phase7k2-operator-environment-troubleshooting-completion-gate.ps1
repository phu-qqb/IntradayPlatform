param(
    [string]$ChecklistRecordFile = "artifacts/readiness/phase7k2-operator-environment-troubleshooting-checklist-record.json",
    [string]$DecisionGateFile = "artifacts/readiness/phase7k2-operator-environment-troubleshooting-completion-gate.json",
    [string]$CompletionNoteFile = "artifacts/readiness/phase7k2-operator-environment-troubleshooting-completion-note.md"
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
    $safe = $raw -replace 'credentialPresenceCheckedWithoutValues|Credential labels|credential labels|credential values|credentialValues|credentialValuesStored|credentialValuesPrinted|credentials|Credential','SAFE_METADATA'
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

Write-Host "LMAX Read-Only Runtime Phase 7K2 Operator Environment Troubleshooting Completion Gate"
Write-Host "Local-only. This gate does not connect to LMAX, request snapshots, replay evidence, schedule work, or use credentials."

$recordRaw = Read-TextSafe $ChecklistRecordFile "ChecklistRecord"
$gateRaw = Read-TextSafe $DecisionGateFile "DecisionGate"
$noteRaw = Read-TextSafe $CompletionNoteFile "CompletionNote"

if ($null -ne $recordRaw) {
    $record = $recordRaw | ConvertFrom-Json
    if ([string]$record.phase -eq "7K2") { Add-Result "ChecklistRecord" "Phase" "PASS" "7K2." } else { Add-Result "ChecklistRecord" "Phase" "FAIL" "Expected 7K2." }
    if ([string]$record.checklistMode -eq "OperatorRecordedLocalOnly") { Add-Result "ChecklistRecord" "Mode" "PASS" $record.checklistMode } else { Add-Result "ChecklistRecord" "Mode" "FAIL" "Unexpected mode." }
    if (-not [bool]$record.externalRunAttemptedInThisPhase -and -not [bool]$record.snapshotRunInThisPhase -and -not [bool]$record.replayRunInThisPhase -and -not [bool]$record.controlSnapshotRunInThisPhase -and -not [bool]$record.anyInstrumentRunInThisPhase) {
        Add-Result "ChecklistRecord" "No execution in phase" "PASS" "All phase run flags false."
    } else {
        Add-Result "ChecklistRecord" "No execution in phase" "FAIL" "Unexpected execution flag."
    }
    if (-not [bool]$record.credentialValuesStored -and -not [bool]$record.credentialValuesPrinted -and [bool]$record.noSensitiveContent) {
        Add-Result "ChecklistRecord" "No credential values stored/printed" "PASS" "Safe credential flags."
    } else {
        Add-Result "ChecklistRecord" "No credential values stored/printed" "FAIL" "Unsafe credential flag."
    }
    if (-not [bool]$record.checklistComplete -and -not [bool]$record.futureExternalRunCanBeConsidered) {
        Add-Result "ChecklistRecord" "Default incomplete checklist" "PASS" "checklistComplete=false; futureExternalRunCanBeConsidered=false."
    } else {
        Add-Result "ChecklistRecord" "Default incomplete checklist" "FAIL" "Expected incomplete/default blocked checklist."
    }
    $requiredItems = @(
        "localNetworkStateChecked",
        "vpnProxyFirewallStateChecked",
        "dnsEndpointResolutionCheckedUsingSafeNonSecretMethod",
        "localMachineClockTimeSyncChecked",
        "localSocketProcessResourceStateChecked",
        "localApiOrLabProcessLocksChecked",
        "demoEndpointAvailabilityWindowChecked",
        "credentialPresenceCheckedWithoutValues",
        "previousSessionExhaustionOrExternalSessionLockConsidered",
        "operatorReviewedPhase7KNote"
    )
    foreach ($itemName in $requiredItems) {
        $item = @($record.checklistItems | Where-Object { [string]$_.name -eq $itemName })
        if ($item.Count -eq 1 -and [string]$item[0].status -eq "PendingOperatorConfirmation") {
            Add-Result "ChecklistItems" $itemName "PASS" "PendingOperatorConfirmation."
        } else {
            Add-Result "ChecklistItems" $itemName "FAIL" "Missing or not pending by default."
        }
    }
    if ([string]$record.finalDecision -eq "PASS_WITH_ACTION_REQUIRED") { Add-Result "ChecklistRecord" "Final decision" "PASS" $record.finalDecision } else { Add-Result "ChecklistRecord" "Final decision" "FAIL" "Expected PASS_WITH_ACTION_REQUIRED." }
}

if ($null -ne $gateRaw) {
    $gate = $gateRaw | ConvertFrom-Json
    if ([string]$gate.phase -eq "7K2") { Add-Result "DecisionGate" "Phase" "PASS" "7K2." } else { Add-Result "DecisionGate" "Phase" "FAIL" "Expected 7K2." }
    if (-not [bool]$gate.checklistComplete -and -not [bool]$gate.futureExternalRunCanBeConsidered) { Add-Result "DecisionGate" "Checklist incomplete/default blocked" "PASS" "false/false." } else { Add-Result "DecisionGate" "Checklist incomplete/default blocked" "FAIL" "Unexpected checklist/future flag." }
    if (-not [bool]$gate.externalAdditionalInstrumentAttemptsCurrentlyAllowed -and -not [bool]$gate.anyInstrumentExternalRunAllowed -and -not [bool]$gate.audusdRetryAllowed -and -not [bool]$gate.usdjpyRetryAllowed -and -not [bool]$gate.gbpusdControlRunAllowed -and -not [bool]$gate.eurgbpControlRunAllowed -and -not [bool]$gate.nextInstrumentRunAllowed) {
        Add-Result "DecisionGate" "All external attempts blocked" "PASS" "All run permissions false."
    } else {
        Add-Result "DecisionGate" "All external attempts blocked" "FAIL" "Unexpected run permission."
    }
    if (-not [bool]$gate.automaticRetryRecommended -and -not [bool]$gate.batchExecutionAllowed -and -not [bool]$gate.wrapperValidationWeakened -and -not [bool]$gate.securityIdSwitchRecommended -and -not [bool]$gate.tokyo600xSwitchRecommended) {
        Add-Result "DecisionGate" "No retry/batch/wrapper/security switch" "PASS" "All false."
    } else {
        Add-Result "DecisionGate" "No retry/batch/wrapper/security switch" "FAIL" "Unsafe planning flag."
    }
    if (-not [bool]$gate.orderPathEnabled -and -not [bool]$gate.schedulerOrPollingEnabled -and -not [bool]$gate.runtimeShadowReplaySubmitEnabled -and -not [bool]$gate.tradingMutationEnabled -and -not [bool]$gate.gatewayRegistrationEnabled) {
        Add-Result "DecisionGate" "No runtime power enabled" "PASS" "All runtime power flags false."
    } else {
        Add-Result "DecisionGate" "No runtime power enabled" "FAIL" "Unexpected runtime power flag."
    }
    if ([string]$gate.finalDecision -eq "PASS_WITH_ACTION_REQUIRED") { Add-Result "DecisionGate" "Final decision" "PASS" $gate.finalDecision } else { Add-Result "DecisionGate" "Final decision" "FAIL" "Expected PASS_WITH_ACTION_REQUIRED." }
    $allowedNextPhase = [string]$gate.allowedNextPhase
    if ($allowedNextPhase.Contains("Phase 7K3") -and $allowedNextPhase.Contains("Operator-Confirmed Environment Checklist Record")) {
        Add-Result "DecisionGate" "Allowed next phase" "PASS" $gate.allowedNextPhase
    } else {
        Add-Result "DecisionGate" "Allowed next phase" "FAIL" "Unexpected allowed next phase."
    }
    $disallowed = ($gate.disallowedActions | Out-String)
    foreach ($required in @("No USDJPY retry", "No AUDUSD retry", "No GBPUSD/EURGBP control run", "No next instrument", "No batch", "No loop", "No automatic retry", "No wrapper relaxation", "No SecurityID switch", "No Tokyo 600x switch", "No replay without MarketDataOnly evidence", "No order path", "No scheduler or polling", "No runtime shadow replay submit", "No trading-state mutation", "No gateway registration")) {
        if ($disallowed -match [regex]::Escape($required)) { Add-Result "DisallowedActions" $required "PASS" "Present." } else { Add-Result "DisallowedActions" $required "FAIL" "Missing." }
    }
}

if ($null -ne $noteRaw) {
    foreach ($marker in @("Phase 7K2 records the local checklist gate", "PendingOperatorConfirmation", "External runs remain blocked", "Phase 7K3")) {
        if ($noteRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "CompletionNote" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "CompletionNote" "Marker: $marker" "FAIL" "Marker missing." }
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
$outPath = Join-Path $outDir "phase7k2-operator-environment-troubleshooting-completion-gate-validation.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7K2"
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
