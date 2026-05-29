param(
    [string]$CompletionRecordFile = "artifacts/readiness/phase7k8-external-session-remediation-completion-record.json",
    [string]$CompletionGateFile = "artifacts/readiness/phase7k8-external-session-remediation-completion-gate.json",
    [string]$CompletionNoteFile = "artifacts/readiness/phase7k8-external-session-remediation-completion-note.md"
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

Write-Host "LMAX Read-Only Runtime Phase 7K8 External Session Remediation Completion Gate"
Write-Host "Local-only. This gate does not connect to LMAX, request snapshots, replay evidence, schedule work, or use credentials."

$recordRaw = Read-TextSafe $CompletionRecordFile "CompletionRecord"
$gateRaw = Read-TextSafe $CompletionGateFile "CompletionGate"
$noteRaw = Read-TextSafe $CompletionNoteFile "CompletionNote"

if ($null -ne $recordRaw) {
    $record = $recordRaw | ConvertFrom-Json
    if ([string]$record.phase -eq "7K8") { Add-Result "CompletionRecord" "Phase" "PASS" "7K8." } else { Add-Result "CompletionRecord" "Phase" "FAIL" "Expected 7K8." }
    if ([string]$record.remediationMode -eq "OperatorRecordedLocalOnly") { Add-Result "CompletionRecord" "Mode" "PASS" $record.remediationMode } else { Add-Result "CompletionRecord" "Mode" "FAIL" "Unexpected mode." }
    if (-not [bool]$record.externalRunAttemptedInThisPhase -and -not [bool]$record.snapshotRunInThisPhase -and -not [bool]$record.replayRunInThisPhase -and -not [bool]$record.anyInstrumentRunInThisPhase) {
        Add-Result "CompletionRecord" "No execution in phase" "PASS" "All execution flags false."
    } else {
        Add-Result "CompletionRecord" "No execution in phase" "FAIL" "Unexpected execution flag."
    }
    if (-not [bool]$record.credentialValuesStored -and -not [bool]$record.credentialValuesPrinted -and [bool]$record.noSensitiveContent) {
        Add-Result "CompletionRecord" "No credential values stored/printed" "PASS" "Safe credential flags."
    } else {
        Add-Result "CompletionRecord" "No credential values stored/printed" "FAIL" "Unsafe credential flag."
    }
    if (@($record.remediationItems).Count -eq 12) { Add-Result "CompletionRecord" "Remediation item count" "PASS" "12." } else { Add-Result "CompletionRecord" "Remediation item count" "FAIL" "Expected 12." }
    foreach ($item in @($record.remediationItems)) {
        if ([string]$item.status -in @("ConfirmedByOperator", "PendingOperatorConfirmation") -and [bool]$item.noSecretMaterialIncluded) {
            Add-Result "RemediationItems" ([string]$item.name) "PASS" ([string]$item.status)
        } else {
            Add-Result "RemediationItems" ([string]$item.name) "FAIL" "Invalid status or secret-material flag."
        }
    }
    if ([bool]$record.remediationCompletionRecorded) {
        if ([bool]$record.freezeLiftCanBeConsidered -and [bool]$record.confirmNoCredentialValuesProvided -and [bool]$record.operatorNamePresent -and [bool]$record.reasonPresent -and [string]$record.finalDecision -eq "PASS_REMEDIATION_COMPLETION_RECORDED") {
            Add-Result "CompletionRecord" "Completed-mode contract" "PASS" "Completion recorded and freeze lift can be considered later."
        } else {
            Add-Result "CompletionRecord" "Completed-mode contract" "FAIL" "Completed mode missing required fields."
        }
    } else {
        if (-not [bool]$record.freezeLiftCanBeConsidered -and [string]$record.finalDecision -eq "PASS_WITH_ACTION_REQUIRED") {
            Add-Result "CompletionRecord" "Incomplete-mode contract" "PASS" "Completion not recorded; freeze lift cannot be considered."
        } else {
            Add-Result "CompletionRecord" "Incomplete-mode contract" "FAIL" "Incomplete mode should remain action-required."
        }
    }
}

if ($null -ne $gateRaw) {
    $gate = $gateRaw | ConvertFrom-Json
    if ([string]$gate.phase -eq "7K8") { Add-Result "CompletionGate" "Phase" "PASS" "7K8." } else { Add-Result "CompletionGate" "Phase" "FAIL" "Expected 7K8." }
    if ([bool]$gate.globalExternalAttemptFreezeRemains -and -not [bool]$gate.freezeLifted -and -not [bool]$gate.directRunAuthorization) {
        Add-Result "CompletionGate" "Freeze remains/no direct auth" "PASS" "Freeze remains; no direct run authorization."
    } else {
        Add-Result "CompletionGate" "Freeze remains/no direct auth" "FAIL" "Unexpected freeze lift or direct auth."
    }
    if (-not [bool]$gate.anyInstrumentExternalRunAllowed -and -not [bool]$gate.externalAdditionalInstrumentAttemptsCurrentlyAllowed -and -not [bool]$gate.gbpusdControlRunAllowed -and -not [bool]$gate.eurgbpControlRunAllowed -and -not [bool]$gate.audusdRetryAllowed -and -not [bool]$gate.usdjpyRetryAllowed -and -not [bool]$gate.nextInstrumentRunAllowed -and -not [bool]$gate.futureExternalRunCanBeConsidered) {
        Add-Result "CompletionGate" "All external attempts remain blocked" "PASS" "All run permissions false."
    } else {
        Add-Result "CompletionGate" "All external attempts remain blocked" "FAIL" "Unexpected run permission."
    }
    if (-not [bool]$gate.automaticRetryRecommended -and -not [bool]$gate.batchExecutionAllowed -and -not [bool]$gate.wrapperValidationWeakened -and -not [bool]$gate.securityIdSwitchRecommended -and -not [bool]$gate.tokyo600xSwitchRecommended) {
        Add-Result "CompletionGate" "No retry/batch/wrapper/security switch" "PASS" "All false."
    } else {
        Add-Result "CompletionGate" "No retry/batch/wrapper/security switch" "FAIL" "Unsafe recommendation flag."
    }
    if (-not [bool]$gate.orderPathEnabled -and -not [bool]$gate.schedulerOrPollingEnabled -and -not [bool]$gate.runtimeShadowReplaySubmitEnabled -and -not [bool]$gate.tradingMutationEnabled -and -not [bool]$gate.gatewayRegistrationEnabled) {
        Add-Result "CompletionGate" "No runtime power enabled" "PASS" "All runtime power flags false."
    } else {
        Add-Result "CompletionGate" "No runtime power enabled" "FAIL" "Unexpected runtime power flag."
    }
    if ([bool]$gate.remediationCompletionRecorded) {
        if ([bool]$gate.freezeLiftCanBeConsidered -and [string]$gate.finalDecision -eq "PASS_REMEDIATION_COMPLETION_RECORDED" -and [string]$gate.allowedNextPhase -eq "Phase 7K9 - Freeze Lift Decision Gate and Known-Good Control Candidate Selection, No External Run") {
            Add-Result "CompletionGate" "Completed-mode decision" "PASS" "Phase 7K9 allowed next."
        } else {
            Add-Result "CompletionGate" "Completed-mode decision" "FAIL" "Completed gate fields invalid."
        }
    } else {
        if (-not [bool]$gate.freezeLiftCanBeConsidered -and [string]$gate.finalDecision -eq "PASS_WITH_ACTION_REQUIRED") {
            Add-Result "CompletionGate" "Incomplete-mode decision" "PASS" "Still action required."
        } else {
            Add-Result "CompletionGate" "Incomplete-mode decision" "FAIL" "Incomplete gate fields invalid."
        }
    }
}

if ($null -ne $noteRaw) {
    foreach ($marker in @("Phase 7K8 records", "global external attempt freeze remains active", "does not lift the freeze", "Phase 7K9")) {
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
$decision = if ($failed.Count -gt 0) { "FAIL" } elseif ($null -ne $gate -and [bool]$gate.remediationCompletionRecorded) { "PASS_REMEDIATION_COMPLETION_RECORDED" } else { "PASS_WITH_ACTION_REQUIRED" }
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7k8-external-session-remediation-completion-gate-validation.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7K8"
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
