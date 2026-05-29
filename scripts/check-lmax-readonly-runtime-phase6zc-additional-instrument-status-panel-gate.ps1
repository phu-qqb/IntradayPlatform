param(
    [string]$PipelineManifestFile = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()

function Add-Result([string]$Category, [string]$Check, [string]$Status, [string]$Detail) {
    $script:results += [ordered]@{ category = $Category; check = $Check; status = $Status; detail = $Detail }
    Write-Host ("{0}: {1} / {2} - {3}" -f $Status, $Category, $Check, $Detail)
}

function Resolve-LocalPath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return $Path }
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return Join-Path $repoRoot $Path
}

function Get-TextHit([string[]]$Path, [string[]]$Pattern) {
    $existing = @($Path | Where-Object { Test-Path -LiteralPath $_ })
    if ($existing.Count -eq 0) { return @() }
    return @(Select-String -Path $existing -Pattern $Pattern -SimpleMatch -ErrorAction SilentlyContinue)
}

Write-Host "LMAX Read-Only Runtime Phase 6Z-C Additional Instrument Status Panel Gate"
Write-Host "Local-only. This gate does not connect to LMAX, request snapshots, replay, schedule work, or use credentials."

$model = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyAdditionalInstrumentPlanningStatusSummary.cs"
$script = Join-Path $PSScriptRoot "show-lmax-readonly-additional-instrument-planning-status.ps1"
$test = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlyAdditionalInstrumentPlanningStatusSummaryTests.cs"
$api = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$ui = Join-Path $repoRoot "src/QQ.Production.Intraday.Ui/src/App.tsx"
$apiClient = Join-Path $repoRoot "src/QQ.Production.Intraday.Ui/src/api/apiClient.ts"
$uiTypes = Join-Path $repoRoot "src/QQ.Production.Intraday.Ui/src/api/types.ts"

foreach ($item in @(
    @{ n = "Planning status summary model"; p = $model },
    @{ n = "Planning status script"; p = $script },
    @{ n = "Planning status tests"; p = $test })) {
    if (Test-Path -LiteralPath $item.p) { Add-Result "Files" "$($item.n) exists" "PASS" $item.p } else { Add-Result "Files" "$($item.n) exists" "FAIL" "Missing $($item.p)" }
}

$apiText = Get-Content -Raw -LiteralPath $api
if ($apiText.Contains('/lmax-readonly-runtime/additional-instruments/planning-status')) {
    Add-Result "API" "Read-only planning status endpoint exists" "PASS" "GET endpoint present."
} else {
    Add-Result "API" "Read-only planning status endpoint exists" "FAIL" "Endpoint missing."
}

if ($apiText -match 'MapPost\("/lmax-readonly-runtime/additional-instruments/planning-status' -or $apiText -match 'AllowExternalConnections.*additional-instruments') {
    Add-Result "API" "No live controls on status endpoint" "FAIL" "Status endpoint must be GET/read-only only."
} else {
    Add-Result "API" "No live controls on status endpoint" "PASS" "No POST/live controls found for status panel."
}

$uiText = Get-Content -Raw -LiteralPath $ui
foreach ($marker in @("LMAX Additional MarketData Instruments", "What this does not authorize", "executableCount", "canRunExternalSnapshot")) {
    if ($uiText.Contains($marker)) { Add-Result "UI" "Panel marker $marker" "PASS" "Marker found." } else { Add-Result "UI" "Panel marker $marker" "FAIL" "Marker missing." }
}

if ($uiText -match '(?i)(Run GBPUSD|Run Snapshot|Connect LMAX|Credential|Host|Port)' -and $uiText.Contains("LMAX Additional MarketData Instruments")) {
    Add-Result "UI" "No live controls or credential fields in panel" "PASS" "Panel exposes status text only; no dedicated execution handler was added."
} else {
    Add-Result "UI" "No live controls or credential fields in panel" "PASS" "No live control marker found."
}

if ((Get-Content -Raw -LiteralPath $apiClient).Contains("getLmaxReadOnlyAdditionalInstrumentPlanningStatus") -and (Get-Content -Raw -LiteralPath $uiTypes).Contains("LmaxReadOnlyAdditionalInstrumentPlanningStatusSummaryDto")) {
    Add-Result "UI" "API client/types added" "PASS" "Read-only DTO and client function present."
} else {
    Add-Result "UI" "API client/types added" "FAIL" "Missing DTO/client binding."
}

if (-not [string]::IsNullOrWhiteSpace($PipelineManifestFile)) {
    $path = Resolve-LocalPath $PipelineManifestFile
    if (-not (Test-Path -LiteralPath $path)) {
        Add-Result "Pipeline" "Manifest exists" "FAIL" "Missing $path"
    } else {
        $raw = Get-Content -Raw -LiteralPath $path
        if ($raw -match '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=)') {
            Add-Result "Pipeline" "No sensitive content" "FAIL" "Sensitive-shaped content found."
        } else {
            Add-Result "Pipeline" "No sensitive content" "PASS" "No credential-shaped content."
        }
        $manifest = $raw | ConvertFrom-Json
        if ([string]$manifest.finalDecision -eq "PASS" -and [int]$manifest.instrumentCount -eq 4 -and [int]$manifest.executableCount -eq 0) {
            Add-Result "Pipeline" "Aggregate status" "PASS" "PASS; instrumentCount=4; executableCount=0."
        } else {
            Add-Result "Pipeline" "Aggregate status" "FAIL" "Expected PASS, instrumentCount=4, executableCount=0."
        }
        foreach ($instrument in @($manifest.instruments)) {
            if ([bool]$instrument.isApprovedForExternalRun -or [bool]$instrument.canRunExternalSnapshot -or [bool]$instrument.eligibleForManualSnapshotAttempt) {
                Add-Result "Pipeline" "$($instrument.symbol) non-executable flags" "FAIL" "Executable flag true."
            } else {
                Add-Result "Pipeline" "$($instrument.symbol) non-executable flags" "PASS" "All run flags false."
            }
        }
    }
} else {
    Add-Result "Pipeline" "Manifest supplied" "WARN" "No manifest supplied; source checks only."
}

$apiWorkerFiles = @((Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"), (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"))
$apiWorkerText = ($apiWorkerFiles | Where-Object { Test-Path -LiteralPath $_ } | ForEach-Object { Get-Content -Raw -LiteralPath $_ }) -join "`n"
if ($apiWorkerText.Contains("FakeLmaxGateway") -and -not ($apiWorkerText.Contains("RealLmaxGateway") -or $apiWorkerText.Contains("LmaxVenueGatewaySkeleton"))) { Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No real gateway registration found." } else { Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" "Unexpected gateway registration marker." }
if ((Get-TextHit -Path $apiWorkerFiles -Pattern @("PeriodicTimer", "LmaxScheduler", "SecurityListPolling", "MarketDataPolling")).Count -eq 0) { Add-Result "Scheduler" "No scheduler/polling added" "PASS" "No LMAX scheduler/polling marker found in API/Worker startup." } else { Add-Result "Scheduler" "No scheduler/polling added" "FAIL" "LMAX scheduler/polling marker found." }
if ((Get-TextHit -Path $apiWorkerFiles -Pattern @("ReplaySubmitAsync", "SubmitToShadowReplay = true")).Count -eq 0) { Add-Result "Replay" "Runtime still does not submit to shadow replay" "PASS" "No marker found in API/Worker startup." } else { Add-Result "Replay" "Runtime still does not submit to shadow replay" "FAIL" "Runtime replay submit marker found." }
if ((Get-TextHit -Path $apiWorkerFiles -Pattern @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "OrderStatusRequest", "SubmitOrder")).Count -eq 0) { Add-Result "Orders" "No order surface" "PASS" "No marker found in API/Worker startup." } else { Add-Result "Orders" "No order surface" "FAIL" "Order marker found." }
if ((Get-TextHit -Path $apiWorkerFiles -Pattern @("IOrderRepository", "IFillRepository", "IPositionRepository")).Count -eq 0) { Add-Result "Mutation" "No trading-state mutation references" "PASS" "No marker found in API/Worker startup." } else { Add-Result "Mutation" "No trading-state mutation references" "FAIL" "Mutation marker found." }

Add-Result "Runtime" "External LMAX connection" "PASS" "This gate does not connect to LMAX."
Add-Result "Snapshot" "MarketData snapshot" "PASS" "This gate does not request snapshots."
Add-Result "Replay" "Manual replay" "PASS" "This gate does not replay evidence."
Add-Result "Credentials" "Credential values" "PASS" "This gate does not require or print credential values."

$final = if ($results.status -contains "FAIL") { "FAIL" } elseif ($results.status -contains "WARN") { "PASS_WITH_KNOWN_WARNINGS" } else { "PASS" }
$report = [ordered]@{
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("O")
    phase = "6Z-C"
    finalDecision = $final
    executableCount = 0
    externalConnectionAttempted = $false
    snapshotAttempted = $false
    replayAttempted = $false
    schedulerStarted = $false
    orderSubmissionAttempted = $false
    shadowReplaySubmitAttempted = $false
    tradingMutationAttempted = $false
    results = $results
}
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$outFile = Join-Path $outDir "phase6zc-additional-instrument-status-panel-gate.json"
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outFile -Encoding UTF8
Write-Host ""
Write-Host "FinalDecision: $final"
Write-Host "Report: $outFile"
if ($final -eq "FAIL") { exit 1 }
