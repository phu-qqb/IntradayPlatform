param(
    [string]$RetryReadinessFile = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()

function Add-Result([string]$Category, [string]$Check, [string]$Status, [string]$Detail) {
    $script:results += [ordered]@{ category = $Category; check = $Check; status = $Status; detail = $Detail }
    Write-Host ("{0}: {1} / {2} - {3}" -f $Status,$Category,$Check,$Detail)
}

function Resolve-LocalPath([string]$PathValue) {
    if ([string]::IsNullOrWhiteSpace($PathValue)) { return $PathValue }
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}

function Get-Hits($Paths, $Patterns) {
    $existing = @($Paths | Where-Object { Test-Path -LiteralPath $_ })
    if ($existing.Count -eq 0) { return @() }
    return @(Select-String -Path $existing -Pattern $Patterns -SimpleMatch -ErrorAction SilentlyContinue)
}

Write-Host "LMAX Read-Only Runtime Phase 6Y GBPUSD Market-Hours Retry Gate"
Write-Host "Local-only. This gate does not connect to LMAX, request snapshots, replay, schedule work, or use credentials."

foreach ($required in @(
    @{ name = "Market-hours retry doc"; path = "docs/LMAX_READONLY_GBPUSD_MARKET_HOURS_RETRY_PLAN.md" },
    @{ name = "Market-hours retry model"; path = "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyGbpusdMarketHoursRetryReadiness.cs" },
    @{ name = "Market-hours retry prep script"; path = "scripts/prepare-lmax-readonly-gbpusd-market-hours-retry.ps1" },
    @{ name = "Market-hours retry tests"; path = "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlyGbpusdMarketHoursRetryReadinessTests.cs" }
)) {
    $path = Join-Path $repoRoot $required.path
    if (Test-Path -LiteralPath $path) {
        Add-Result "Files" "$($required.name) exists" "PASS" $path
    } else {
        Add-Result "Files" "$($required.name) exists" "FAIL" "Missing $path"
    }
}

if ([string]::IsNullOrWhiteSpace($RetryReadinessFile)) {
    Add-Result "RetryReadiness" "Retry readiness supplied" "WARN" "No retry readiness artifact supplied; source-only gate mode."
} else {
    $path = Resolve-LocalPath $RetryReadinessFile
    if (-not (Test-Path -LiteralPath $path)) {
        Add-Result "RetryReadiness" "Retry readiness exists" "FAIL" "Missing $path"
    } else {
        $raw = Get-Content -LiteralPath $path -Raw
        $readiness = $raw | ConvertFrom-Json
        $scanText = $raw -replace "NoOrderSubmission","" -replace "NoRuntimeShadowReplaySubmit","" -replace "NoTradingMutation",""
        if ($scanText -match '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=)') {
            Add-Result "RetryReadiness" "No sensitive content" "FAIL" "Credential-shaped content found."
        } else {
            Add-Result "RetryReadiness" "No sensitive content" "PASS" "No credential-shaped content."
        }

        if ([string]$readiness.symbol -eq "GBPUSD" -and [string]$readiness.securityId -eq "4002" -and [string]$readiness.securityIdSource -eq "8") {
            Add-Result "RetryReadiness" "GBPUSD SecurityID scope" "PASS" "GBPUSD / 4002 / SecurityIDSource=8."
        } else {
            Add-Result "RetryReadiness" "GBPUSD SecurityID scope" "FAIL" "Retry readiness must be GBPUSD 4002 / source 8."
        }

        if ([string]$readiness.previousResultStatus -eq "CompletedWithEmptyBook" -and [bool]$readiness.previousAttemptWasOutsideMarketHours) {
            Add-Result "RetryReadiness" "Previous empty-book context" "PASS" "Previous result is Saturday/out-of-market CompletedWithEmptyBook."
        } else {
            Add-Result "RetryReadiness" "Previous empty-book context" "FAIL" "Expected previous CompletedWithEmptyBook outside market hours."
        }

        if ([bool]$readiness.retryAllowedOnlyDuringMarketHours -and [bool]$readiness.retryIsManualOnly -and [int]$readiness.retryAttemptCount -eq 1 -and -not [bool]$readiness.canRunAutomatically) {
            Add-Result "RetryReadiness" "Manual market-hours single retry only" "PASS" "Manual-only, market-hours only, one attempt, no automatic run."
        } else {
            Add-Result "RetryReadiness" "Manual market-hours single retry only" "FAIL" "Retry readiness must not authorize automation or repeated retry."
        }

        if ([bool]$readiness.noScheduler -and [bool]$readiness.noPolling -and [bool]$readiness.noRuntimeShadowReplaySubmit -and [bool]$readiness.noOrderSubmission -and [bool]$readiness.noTradingMutation -and [string]$readiness.apiWorkerGatewayMode -eq "FakeLmaxGateway" -and [bool]$readiness.noSensitiveContent) {
            Add-Result "RetryReadiness" "Safety flags" "PASS" "No scheduler, polling, runtime shadow replay submit, orders, mutation, or real gateway."
        } else {
            Add-Result "RetryReadiness" "Safety flags" "FAIL" "Unsafe retry readiness flags detected."
        }

        if ([string]$readiness.decision -eq "PASS" -and [string]$readiness.futureCommandTemplate -match "DO NOT RUN FROM THIS SCRIPT") {
            Add-Result "RetryReadiness" "Decision and command template" "PASS" "PASS with non-executable future command template."
        } else {
            Add-Result "RetryReadiness" "Decision and command template" "FAIL" "Expected PASS and DO NOT RUN FROM THIS SCRIPT marker."
        }
    }
}

$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"
$startupFiles = @($apiProgram, $workerProgram)
$gatewayHits = Get-Hits $startupFiles @("RealLmaxGateway", "ExternalReadOnlyPrototypeGateway", "LmaxVenueGatewaySkeleton", "SecurityListRequest")
if ($gatewayHits.Count -eq 0 -and (Select-String -Path $apiProgram -Pattern "FakeLmaxGateway" -SimpleMatch -Quiet)) {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No real gateway registration found."
} else {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" (($gatewayHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

foreach ($scan in @(
    @{ category = "Scheduler"; check = "No scheduler/polling added"; patterns = @("PeriodicTimer", "System.Threading.Timer", "IHostedService", "BackgroundService", "SecurityListPoll") },
    @{ category = "Replay"; check = "Runtime still does not submit to shadow replay"; patterns = @("SubmitToShadowReplay = true", "SubmittedToShadowReplay = true", "ReplaySubmitAsync") },
    @{ category = "Orders"; check = "No order surface"; patterns = @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "OrderStatusRequest", "SubmitOrder") },
    @{ category = "Mutation"; check = "No trading-state mutation references"; patterns = @("PersistTrade", "TradingState") }
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
Add-Result "Replay" "Manual replay" "PASS" "This gate does not replay evidence."
Add-Result "Credentials" "Credential values" "PASS" "This gate does not require or print credential values."

$failed = @($results | Where-Object status -eq "FAIL")
$warnings = @($results | Where-Object status -eq "WARN")
$decision = if ($failed.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS_WITH_KNOWN_WARNINGS" } else { "PASS" }

$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase6y-market-hours-retry-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "6Y"
    finalDecision = $decision
    externalConnectionAttempted = $false
    snapshotAttempted = $false
    replayAttempted = $false
    schedulerStarted = $false
    pollingStarted = $false
    orderSubmissionAttempted = $false
    shadowReplaySubmitAttempted = $false
    tradingMutationAttempted = $false
    results = $results
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }
