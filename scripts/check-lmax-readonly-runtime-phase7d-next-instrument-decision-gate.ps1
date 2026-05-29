param(
    [string]$DecisionFile = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=|raw\s*fix|sendercompid|targetcompid)'

function Add-Result([string]$Category, [string]$Check, [string]$Status, [string]$Detail) {
    $script:results += [ordered]@{ category = $Category; check = $Check; status = $Status; detail = $Detail }
    Write-Host ("{0}: {1} / {2} - {3}" -f $Status, $Category, $Check, $Detail)
}

function Resolve-LocalPath([string]$PathValue) {
    if ([string]::IsNullOrWhiteSpace($PathValue)) { return "" }
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}

function Read-JsonForGate([string]$PathValue, [string]$Label) {
    $resolved = Resolve-LocalPath $PathValue
    if ([string]::IsNullOrWhiteSpace($resolved) -or -not (Test-Path -LiteralPath $resolved)) {
        Add-Result $Label "File exists" "FAIL" "Missing $resolved"
        return $null
    }
    Add-Result $Label "File exists" "PASS" $resolved
    $raw = Get-Content -LiteralPath $resolved -Raw
    if ($raw -match $script:sensitivePattern) {
        Add-Result $Label "No sensitive content" "FAIL" "Credential-shaped or raw FIX content found."
    } else {
        Add-Result $Label "No sensitive content" "PASS" "No credential-shaped or raw FIX content."
    }
    return ($raw | ConvertFrom-Json)
}

function Get-Hits($Paths, $Patterns) {
    $existing = @($Paths | Where-Object { Test-Path -LiteralPath $_ })
    if ($existing.Count -eq 0) { return @() }
    return @(Select-String -Path $existing -Pattern $Patterns -SimpleMatch -ErrorAction SilentlyContinue)
}

Write-Host "LMAX Read-Only Runtime Phase 7D Next Instrument Decision Gate"
Write-Host "Local-only. This gate does not connect to LMAX, request snapshots, request SecurityList, replay evidence, schedule work, or use credentials."

foreach ($required in @(
    @{ name = "Phase 7D decision model"; path = "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyPostGbpusdNextInstrumentDecision.cs" },
    @{ name = "Phase 7D decision script"; path = "scripts/decide-lmax-readonly-next-instrument-after-gbpusd.ps1" },
    @{ name = "Phase 7D tests"; path = "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlyPostGbpusdNextInstrumentDecisionTests.cs" }
)) {
    $path = Join-Path $repoRoot $required.path
    if (Test-Path -LiteralPath $path) {
        Add-Result "Files" "$($required.name) exists" "PASS" $path
    } else {
        Add-Result "Files" "$($required.name) exists" "FAIL" "Missing $path"
    }
}

if ([string]::IsNullOrWhiteSpace($DecisionFile)) {
    Add-Result "Decision" "Decision artifact supplied" "WARN" "No decision artifact supplied; source-only gate mode."
} else {
    $decision = Read-JsonForGate $DecisionFile "Decision"
    if ($null -ne $decision) {
        if ([string]$decision.currentInstrument -eq "GBPUSD" -and [int]$decision.sequenceOrder -eq 1) {
            Add-Result "Decision" "Current instrument" "PASS" "GBPUSD at sequence order 1."
        } else {
            Add-Result "Decision" "Current instrument" "FAIL" "Decision must be for GBPUSD at sequence order 1."
        }

        if ([string]$decision.decision -in @("PendingGbpusdMarketHoursAttempt", "ProceedToEurgbpPlanning", "RetryGbpusdAtLaterMarketHours", "BlockSequenceForDiagnostics", "StopManualWorkflow")) {
            $status = if ([string]$decision.decision -in @("PendingGbpusdMarketHoursAttempt", "RetryGbpusdAtLaterMarketHours", "BlockSequenceForDiagnostics")) { "WARN" } else { "PASS" }
            Add-Result "Decision" "Decision outcome" $status "Decision is $($decision.decision)."
        } else {
            Add-Result "Decision" "Decision outcome" "FAIL" "Unknown decision outcome $($decision.decision)."
        }

        if ([string]$decision.decision -eq "ProceedToEurgbpPlanning" -and [string]$decision.nextCandidateInstrument -ne "EURGBP") {
            Add-Result "Decision" "Next candidate" "FAIL" "Proceed decision must select EURGBP."
        } elseif ([string]$decision.decision -ne "ProceedToEurgbpPlanning" -and -not [string]::IsNullOrWhiteSpace([string]$decision.nextCandidateInstrument)) {
            Add-Result "Decision" "Next candidate" "FAIL" "Next candidate must be empty unless proceeding to EURGBP."
        } else {
            Add-Result "Decision" "Next candidate" "PASS" "Next candidate is consistent with decision."
        }

        if (-not [bool]$decision.canRunExternalSnapshot -and -not [bool]$decision.isApprovedForExternalRun -and -not [bool]$decision.eligibleForManualSnapshotAttempt -and -not [bool]$decision.batchExecutionAllowed -and [int]$decision.executableCount -eq 0) {
            Add-Result "Decision" "Non-executable flags" "PASS" "Run flags false; batch disabled; executableCount=0."
        } else {
            Add-Result "Decision" "Non-executable flags" "FAIL" "Unsafe executable or batch flag found."
        }

        if (-not [bool]$decision.schedulerOrPolling -and -not [bool]$decision.runtimeShadowReplaySubmit -and -not [bool]$decision.orderSubmission -and -not [bool]$decision.gatewayRegistration -and -not [bool]$decision.tradingMutation -and [string]$decision.apiWorkerGatewayMode -eq "FakeLmaxGateway" -and [bool]$decision.noSensitiveContent) {
            Add-Result "Decision" "Aggregate safety flags" "PASS" "No scheduler/replay/order/gateway/mutation; FakeLmaxGateway; sanitized."
        } else {
            Add-Result "Decision" "Aggregate safety flags" "FAIL" "Unsafe aggregate decision flag found."
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
    if ($hits.Count -eq 0) {
        Add-Result $scan.category $scan.check "PASS" "No marker found in API/Worker startup."
    } else {
        Add-Result $scan.category $scan.check "FAIL" (($hits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
    }
}

Add-Result "Runtime" "External LMAX connection" "PASS" "This gate does not connect to LMAX."
Add-Result "Snapshot" "MarketData snapshot" "PASS" "This gate does not request snapshots."
Add-Result "SecurityList" "SecurityListRequest" "PASS" "This gate does not request SecurityList."
Add-Result "Replay" "Manual replay" "PASS" "This gate does not replay evidence."
Add-Result "Credentials" "Credential values" "PASS" "This gate does not require or print credential values."

$failed = @($results | Where-Object status -eq "FAIL")
$warnings = @($results | Where-Object status -eq "WARN")
$decisionFinal = if ($failed.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS_WITH_KNOWN_WARNINGS" } else { "PASS" }

$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7d-next-instrument-decision-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7D"
    finalDecision = $decisionFinal
    externalConnectionAttempted = $false
    snapshotAttempted = $false
    replayAttempted = $false
    orderSubmissionAttempted = $false
    shadowReplaySubmitAttempted = $false
    tradingMutationAttempted = $false
    schedulerStarted = $false
    apiWorkerGatewayMode = "FakeLmaxGateway"
    results = $results
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decisionFinal"
Write-Host "Report: $outPath"
if ($decisionFinal -eq "FAIL") { exit 1 }
