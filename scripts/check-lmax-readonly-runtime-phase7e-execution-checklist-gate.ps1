param(
    [string]$ChecklistPackFile = ""
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

function Has-Item($Values, [string]$Pattern) {
    return @($Values | Where-Object { ([string]$_).IndexOf($Pattern, [StringComparison]::OrdinalIgnoreCase) -ge 0 }).Count -gt 0
}

Write-Host "LMAX Read-Only Runtime Phase 7E Execution Checklist Gate"
Write-Host "Local-only. This gate does not connect to LMAX, request snapshots, request SecurityList, replay evidence, schedule work, or use credentials."

$docPath = Join-Path $repoRoot "docs/LMAX_READONLY_GBPUSD_MARKET_HOURS_EXECUTION_CHECKLIST.md"
$builderPath = Join-Path $PSScriptRoot "build-lmax-readonly-gbpusd-market-hours-execution-checklist-pack.ps1"
$modelPath = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPack.cs"
$testsPath = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackTests.cs"

foreach ($required in @(
    @{ name = "Phase 7E checklist doc"; path = $docPath },
    @{ name = "Phase 7E checklist builder"; path = $builderPath },
    @{ name = "Phase 7E checklist model"; path = $modelPath },
    @{ name = "Phase 7E tests"; path = $testsPath }
)) {
    if (Test-Path -LiteralPath $required.path) {
        Add-Result "Files" "$($required.name) exists" "PASS" $required.path
    } else {
        Add-Result "Files" "$($required.name) exists" "FAIL" "Missing $($required.path)"
    }
}

if (Test-Path -LiteralPath $docPath) {
    $doc = Get-Content -LiteralPath $docPath -Raw
    if ($doc -match $sensitivePattern) { Add-Result "Doc" "No sensitive content" "FAIL" "Credential-shaped content found." } else { Add-Result "Doc" "No sensitive content" "PASS" "No credential-shaped content." }
    foreach ($marker in @(
        "DO NOT RUN UNTIL MARKET HOURS",
        "run-lmax-readonly-runtime-demo-gbpusd-snapshot-once.ps1",
        "Phase 7C review script",
        "Map evidence preview",
        "Build the Phase 7C closure manifest",
        "Run the Phase 7D next-instrument decision",
        "Ctrl+C",
        "FakeLmaxGateway",
        "No scheduler",
        "No polling",
        "No runtime shadow replay submit",
        "No orders",
        "No gateway registration",
        "No multi-instrument batch"
    )) {
        if ($doc.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            Add-Result "Doc" "Marker: $marker" "PASS" "Marker found."
        } else {
            Add-Result "Doc" "Marker: $marker" "FAIL" "Marker missing."
        }
    }
}

if (-not [string]::IsNullOrWhiteSpace($ChecklistPackFile)) {
    $pack = Read-JsonForGate $ChecklistPackFile "ChecklistPack"
    if ($null -ne $pack) {
        if ([string]$pack.symbol -eq "GBPUSD" -and [string]$pack.securityId -eq "4002" -and [string]$pack.securityIdSource -eq "8") {
            Add-Result "ChecklistPack" "GBPUSD identity" "PASS" "GBPUSD / 4002 / source 8."
        } else {
            Add-Result "ChecklistPack" "GBPUSD identity" "FAIL" "Unexpected selected instrument."
        }

        if ([string]$pack.manualCommandWarning -match "DO NOT RUN UNTIL MARKET HOURS" -and [string]$pack.requiredManualCommand -match "run-lmax-readonly-runtime-demo-gbpusd-snapshot-once.ps1") {
            Add-Result "ChecklistPack" "Command present and marked" "PASS" "Manual command is present and not-to-run warning is present."
        } else {
            Add-Result "ChecklistPack" "Command present and marked" "FAIL" "Manual command or market-hours warning missing."
        }

        if ([string]$pack.requiredManualCommand -match '(?i)(scheduler|polling|ReplaySubmitAsync|SubmitToShadowReplay|NewOrderSingle|OrderCancelRequest|OrderCancelReplaceRequest|OrderStatusRequest|TradeCaptureReportRequest|SubmitOrder|production|uat|batch)') {
            Add-Result "ChecklistPack" "Command has no forbidden runtime/order flags" "FAIL" "Forbidden runtime/order/scheduler marker found in command."
        } else {
            Add-Result "ChecklistPack" "Command has no forbidden runtime/order flags" "PASS" "Command contains only the GBPUSD manual wrapper flags."
        }

        if ((Has-Item $pack.postRunSequence "Phase 7C gate") -and (Has-Item $pack.postRunSequence "Phase 7D") -and (Has-Item $pack.postRunSequence "evidence preview") -and (Has-Item $pack.postRunSequence "closure manifest")) {
            Add-Result "ChecklistPack" "Post-run sequence" "PASS" "Phase 7C and 7D closure sequence documented."
        } else {
            Add-Result "ChecklistPack" "Post-run sequence" "FAIL" "Post-run Phase 7C/7D sequence incomplete."
        }

        if ((Has-Item $pack.duringRunMonitoring "Ctrl+C") -or (Has-Item $pack.duringRunMonitoring "close process")) {
            Add-Result "ChecklistPack" "Kill switch" "PASS" "Kill switch documented."
        } else {
            Add-Result "ChecklistPack" "Kill switch" "FAIL" "Kill switch missing."
        }

        if (-not [bool]$pack.canRunAutomatically -and -not [bool]$pack.schedulerOrPolling -and -not [bool]$pack.runtimeShadowReplaySubmit -and -not [bool]$pack.orderSubmission -and -not [bool]$pack.gatewayRegistration -and -not [bool]$pack.tradingMutation -and [string]$pack.apiWorkerGatewayMode -eq "FakeLmaxGateway" -and [bool]$pack.noSensitiveContent) {
            Add-Result "ChecklistPack" "Safety flags" "PASS" "No automatic run, scheduler, replay submit, order, gateway, mutation; FakeLmaxGateway."
        } else {
            Add-Result "ChecklistPack" "Safety flags" "FAIL" "Unsafe checklist pack flag found."
        }
    }
} else {
    Add-Result "ChecklistPack" "Checklist pack supplied" "WARN" "No checklist pack supplied; source/doc gate mode."
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
$decision = if ($failed.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS_WITH_KNOWN_WARNINGS" } else { "PASS" }

$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7e-execution-checklist-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7E"
    finalDecision = $decision
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
Write-Host "FinalDecision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }
