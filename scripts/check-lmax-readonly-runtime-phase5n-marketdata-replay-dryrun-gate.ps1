param(
    [string]$EvidencePreviewFile,
    [string]$BaseUrl = "http://localhost:5050",
    [switch]$Replay
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()

function Add-Result([string]$Category, [string]$Check, [string]$Status, [string]$Detail) {
    $script:results += [ordered]@{ category = $Category; check = $Check; status = $Status; detail = $Detail }
    Write-Host ("{0}: {1} / {2} - {3}" -f $Status, $Category, $Check, $Detail)
}

function Test-Contains([string]$Path, [string]$Pattern) {
    return (Test-Path -LiteralPath $Path) -and [bool](Select-String -Path $Path -Pattern $Pattern -SimpleMatch -Quiet)
}

function Test-ApiAvailable([string]$Url) {
    try {
        $uri = [Uri]$Url
        if ($uri.Host -notin @("localhost", "127.0.0.1")) { return $false }
        Invoke-RestMethod -Method GET -Uri "$Url/health" -TimeoutSec 3 | Out-Null
        return $true
    } catch {
        return $false
    }
}

Write-Host "LMAX Read-Only Runtime Phase 5N MarketDataOnly Replay Dry-Run Gate"
Write-Host "Local-only. No external LMAX connection and no runtime shadow replay submit."

$replayScript = Join-Path $repoRoot "scripts/replay-lmax-readonly-demo-snapshot-evidence-preview.ps1"
$phase5mScript = Join-Path $repoRoot "scripts/preview-lmax-readonly-demo-snapshot-evidence.ps1"
$mapperFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapper.cs"
$mapperTestFile = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapperTests.cs"
$shadowTestFile = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/LmaxShadowModeTests.cs"
$prototypeFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlySocketPrototype.cs"
$manualScript = Join-Path $repoRoot "scripts/run-lmax-readonly-runtime-demo-snapshot-prototype.ps1"

if ((Test-Contains $replayScript "EvidencePreviewFile") -and
    (Test-Contains $replayScript "MarketDataOnly") -and
    (Test-Contains $replayScript "/lmax-shadow/replay") -and
    (Test-Contains $replayScript "RuntimeShadowReplaySubmit: false")) {
    Add-Result "Files" "Manual replay dry-run script exists" "PASS" "Dedicated Phase 5N script uses the existing local shadow replay endpoint."
} else {
    Add-Result "Files" "Manual replay dry-run script exists" "FAIL" "Replay script or required safety markers are missing."
}

if ((Test-Contains $mapperTestFile "Phase5m_mapper_does_not_submit_replay_or_mutate_trading_state") -and
    (Test-Contains $shadowTestFile "Market_data_only_runtime_snapshot_preview_replays_with_zero_observations")) {
    Add-Result "Tests" "MarketDataOnly replay expectations covered" "PASS" "Mapper no-submit and MarketDataOnly zero-observation replay expectations are covered."
} else {
    Add-Result "Tests" "MarketDataOnly replay expectations covered" "FAIL" "Phase 5N test markers are missing."
}

if (-not [string]::IsNullOrWhiteSpace($EvidencePreviewFile)) {
    $previewPath = if ([IO.Path]::IsPathRooted($EvidencePreviewFile)) { $EvidencePreviewFile } else { Join-Path $repoRoot $EvidencePreviewFile }
    if (Test-Path -LiteralPath $previewPath) {
        $raw = Get-Content -LiteralPath $previewPath -Raw
        $evidence = $raw | ConvertFrom-Json
        $mode = if ($evidence.PSObject.Properties.Name -contains "evidenceMode") { [string]$evidence.evidenceMode } else { "" }
        $executionCount = if ($evidence.PSObject.Properties.Name -contains "executionReports") { @($evidence.executionReports).Count } else { -1 }
        $orderCount = if ($evidence.PSObject.Properties.Name -contains "orderStatuses") { @($evidence.orderStatuses).Count } else { -1 }
        $tradeCount = if ($evidence.PSObject.Properties.Name -contains "tradeCaptureReports") { @($evidence.tradeCaptureReports).Count } else { -1 }
        $rejectCount = if ($evidence.PSObject.Properties.Name -contains "protocolRejects") { @($evidence.protocolRejects).Count } else { -1 }
        if ($mode -eq "MarketDataOnly" -and $executionCount -eq 0 -and $orderCount -eq 0 -and $tradeCount -eq 0 -and $rejectCount -eq 0) {
            Add-Result "Preview" "Provided evidence preview is MarketDataOnly" "PASS" "Replay arrays are empty and market-data preview is eligible for zero-observation replay."
        } else {
            Add-Result "Preview" "Provided evidence preview is MarketDataOnly" "FAIL" "Mode=$mode execution=$executionCount order=$orderCount trade=$tradeCount reject=$rejectCount"
        }
    } else {
        Add-Result "Preview" "Provided evidence preview is MarketDataOnly" "FAIL" "Preview file not found: $previewPath"
    }
} else {
    Add-Result "Preview" "Provided evidence preview is MarketDataOnly" "WARN" "No -EvidencePreviewFile supplied; source-only gate checks ran."
}

$runtimeSubmitHits = @(Select-String -Path $prototypeFile,$manualScript,$mapperFile,$phase5mScript -Pattern "/lmax-shadow/replay","SubmitToShadowReplayAsync","ILmaxShadowReplayService","ReplayAsync" -SimpleMatch -ErrorAction SilentlyContinue)
if ($runtimeSubmitHits.Count -eq 0) {
    Add-Result "Safety" "Runtime does not submit to shadow replay" "PASS" "No runtime/prototype/mapper shadow replay submit path found."
} else {
    Add-Result "Safety" "Runtime does not submit to shadow replay" "FAIL" (($runtimeSubmitHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$forbiddenOrder = @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder", "SendOrder", "OrderStatusRequest")
$orderHits = @()
foreach ($word in $forbiddenOrder) {
    $orderHits += @(Select-String -Path $prototypeFile,$manualScript,$mapperFile,$replayScript -Pattern $word -SimpleMatch -ErrorAction SilentlyContinue)
}
if ($orderHits.Count -eq 0) {
    Add-Result "Safety" "No order command surface" "PASS" "No order command surface found in runtime prototype or Phase 5N files."
} else {
    Add-Result "Safety" "No order command surface" "FAIL" (($orderHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$forbiddenMutation = @("IOrderRepository", "IFillRepository", "PositionRepository", "ModelRun", "RiskState", "Wallet", "SubmitToShadowReplayAsync")
$mutationHits = @()
foreach ($word in $forbiddenMutation) {
    $mutationHits += @(Select-String -Path $prototypeFile,$manualScript,$mapperFile,$replayScript -Pattern $word -SimpleMatch -ErrorAction SilentlyContinue)
}
if ($mutationHits.Count -eq 0) {
    Add-Result "Safety" "No trading mutation dependency" "PASS" "No trading-state repository or runtime mutation dependency found."
} else {
    Add-Result "Safety" "No trading mutation dependency" "FAIL" (($mutationHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"
$apiText = Get-Content -LiteralPath $apiProgram -Raw
$workerText = Get-Content -LiteralPath $workerProgram -Raw
if ($apiText -match "AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>" -and
    $workerText -match "AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>" -and
    $apiText -notmatch "LmaxReadOnlyDemoMarketDataSocketClient|LmaxReadOnlySocketPrototypeTransport" -and
    $workerText -notmatch "LmaxReadOnlyDemoMarketDataSocketClient|LmaxReadOnlySocketPrototypeTransport" -and
    $apiText -notmatch "AddHostedService.*Lmax" -and
    $workerText -notmatch "AddHostedService.*Lmax") {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No prototype, real gateway, scheduler, or hosted service registration found."
} else {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" "FakeLmaxGateway registration missing or prototype/live registration found."
}

if ($Replay.IsPresent) {
    if ([string]::IsNullOrWhiteSpace($EvidencePreviewFile)) {
        Add-Result "Replay" "Manual replay dry-run" "FAIL" "-Replay requires -EvidencePreviewFile."
    } elseif (Test-ApiAvailable $BaseUrl) {
        $previewPath = if ([IO.Path]::IsPathRooted($EvidencePreviewFile)) { $EvidencePreviewFile } else { Join-Path $repoRoot $EvidencePreviewFile }
        powershell -NoProfile -ExecutionPolicy Bypass -File $replayScript -EvidencePreviewFile $previewPath -BaseUrl $BaseUrl | Tee-Object -Variable replayOutput | Out-Host
        if ($LASTEXITCODE -eq 0) {
            Add-Result "Replay" "Manual replay dry-run" "PASS" "MarketDataOnly preview replay completed with zero observations."
        } else {
            Add-Result "Replay" "Manual replay dry-run" "FAIL" "Replay dry-run failed."
        }
    } else {
        Add-Result "Replay" "Manual replay dry-run" "WARN" "API unavailable at $BaseUrl; replay dry-run skipped."
    }
} else {
    Add-Result "Replay" "Manual replay dry-run" "WARN" "Not requested. Pass -Replay with local API available to execute the manual dry-run."
}

Add-Result "Runtime" "External socket attempt" "PASS" "No external socket attempt is made by this gate."

$failures = @($results | Where-Object { $_.status -eq "FAIL" })
$warnings = @($results | Where-Object { $_.status -eq "WARN" })
$decision = if ($failures.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS WITH KNOWN WARNINGS" } else { "PASS" }

$reportDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $reportDir "lmax-readonly-phase5n-marketdata-replay-dryrun-gate-$stamp.json"
[ordered]@{
    gate = "LMAX Read-Only Runtime Phase 5N MarketDataOnly Replay Dry-Run Gate"
    finalDecision = $decision
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    checks = @($results)
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $reportPath"

if ($decision -eq "FAIL") { exit 1 }
