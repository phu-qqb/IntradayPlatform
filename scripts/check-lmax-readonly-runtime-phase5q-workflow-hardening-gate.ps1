param(
    [string]$StabilitySummaryFile,
    [string]$WorkflowManifestFile
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

Write-Host "LMAX Read-Only Runtime Phase 5Q Workflow Hardening Gate"
Write-Host "Local-only. No external LMAX connection, no scheduler, no polling, and no runtime shadow replay submit."

$workflowValidator = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyMarketDataWorkflowManifest.cs"
$workflowTests = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlyMarketDataWorkflowValidatorTests.cs"
$workflowScript = Join-Path $repoRoot "scripts/run-lmax-readonly-marketdata-manual-workflow-review.ps1"
$prototypeFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlySocketPrototype.cs"
$mapperFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapper.cs"
$gitignore = Join-Path $repoRoot ".gitignore"

if ((Test-Contains $workflowValidator "LmaxReadOnlyMarketDataWorkflowValidator") -and
    (Test-Contains $workflowValidator "ReplayNotRequested") -and
    (Test-Contains $workflowValidator "EvidencePreviewNotMarketDataOnly") -and
    (Test-Contains $workflowValidator "ReplayObservationsPresent")) {
    Add-Result "Files" "Workflow manifest validator exists" "PASS" "Validator covers manifest, preview, optional replay, and safety flags."
} else {
    Add-Result "Files" "Workflow manifest validator exists" "FAIL" "Workflow validator or required markers are missing."
}

if ((Test-Contains $workflowScript "StabilitySummaryFile") -and
    (Test-Contains $workflowScript "RegeneratePreviews") -and
    (Test-Contains $workflowScript "ReplayEvidencePreviews") -and
    (Test-Contains $workflowScript "ConfirmLocalReplay") -and
    (Test-Contains $workflowScript "artifacts/lmax-readonly-runtime-demo-snapshot/workflow")) {
    Add-Result "Files" "Workflow review script exists" "PASS" "Script supports stability summaries, preview regeneration, explicit replay, and ignored manifest output."
} else {
    Add-Result "Files" "Workflow review script exists" "FAIL" "Workflow review script or required safety markers are missing."
}

if ((Test-Contains $workflowTests "Valid_manifest_without_replay_passes_with_warning") -and
    (Test-Contains $workflowTests "Unsafe_artifact_flags_fail") -and
    (Test-Contains $workflowTests "Non_market_data_preview_fails") -and
    (Test-Contains $workflowTests "Replay_observations_fail") -and
    (Test-Contains $workflowTests "Sentinel_secret_fails")) {
    Add-Result "Tests" "Workflow validator tests exist" "PASS" "Manifest pass, unsafe artifacts, non-market-data previews, replay observations, and secret tests are present."
} else {
    Add-Result "Tests" "Workflow validator tests exist" "FAIL" "Phase 5Q workflow test markers are missing."
}

if ((Test-Path -LiteralPath $gitignore) -and
    (Select-String -Path $gitignore -Pattern "artifacts/" -SimpleMatch -Quiet) -and
    (Test-Contains $workflowScript "artifacts/lmax-readonly-runtime-demo-snapshot/workflow")) {
    Add-Result "Artifacts" "Workflow artifact directory ignored" "PASS" "Workflow manifests are written under the ignored artifacts tree."
} else {
    Add-Result "Artifacts" "Workflow artifact directory ignored" "FAIL" "Could not confirm ignored workflow artifact output."
}

if (-not [string]::IsNullOrWhiteSpace($StabilitySummaryFile)) {
    $summaryPath = if ([IO.Path]::IsPathRooted($StabilitySummaryFile)) { $StabilitySummaryFile } else { Join-Path $repoRoot $StabilitySummaryFile }
    $workflowOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $workflowScript -StabilitySummaryFile $summaryPath 2>&1
    $workflowText = $workflowOutput | Out-String
    Write-Host $workflowText
    if ($LASTEXITCODE -eq 0 -and $workflowText -match "FinalDecision:\s*(PASS|PASS_WITH_WARNINGS)" -and $workflowText -match "ExternalConnectionAttempted:\s*false") {
        Add-Result "Workflow" "Provided stability summary reviews into manifest" "PASS" "Workflow review accepted $summaryPath without external connection."
    } else {
        Add-Result "Workflow" "Provided stability summary reviews into manifest" "FAIL" "Workflow review failed for $summaryPath"
    }
}

if (-not [string]::IsNullOrWhiteSpace($WorkflowManifestFile)) {
    $manifestPath = if ([IO.Path]::IsPathRooted($WorkflowManifestFile)) { $WorkflowManifestFile } else { Join-Path $repoRoot $WorkflowManifestFile }
    if (Test-Path -LiteralPath $manifestPath) {
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        if ([string]$manifest.finalDecision -in @("PASS", "PASS_WITH_WARNINGS") -and -not [bool]$manifest.runtimeShadowReplaySubmit) {
            Add-Result "Workflow" "Provided workflow manifest is safe" "PASS" "Manifest final decision is $($manifest.finalDecision) and runtimeShadowReplaySubmit=false."
        } else {
            Add-Result "Workflow" "Provided workflow manifest is safe" "FAIL" "Manifest final decision or runtime shadow submit flag is unsafe."
        }
    } else {
        Add-Result "Workflow" "Provided workflow manifest is safe" "FAIL" "Manifest file not found: $manifestPath"
    }
}

$workflowTextSource = if (Test-Path -LiteralPath $workflowScript) { Get-Content -LiteralPath $workflowScript -Raw } else { "" }
if ($workflowTextSource -notmatch "while\s*\(" -and
    $workflowTextSource -notmatch "Register-ScheduledTask|New-ScheduledTask|Start-Job|Start-ThreadJob|Timer|HostedService|Start-Sleep") {
    Add-Result "Safety" "No scheduler or automatic polling" "PASS" "No scheduler, background job, timer, hosted service, sleep loop, or polling marker found."
} else {
    Add-Result "Safety" "No scheduler or automatic polling" "FAIL" "Scheduler, background job, timer, sleep, hosted-service, or polling marker found."
}

$runtimeSubmitHits = @(Select-String -Path $prototypeFile,$mapperFile,$workflowValidator -Pattern "/lmax-shadow/replay","SubmitToShadowReplayAsync","ILmaxShadowReplayService","ReplayAsync" -SimpleMatch -ErrorAction SilentlyContinue)
if ($runtimeSubmitHits.Count -eq 0 -and (Test-Contains $workflowScript "ConfirmLocalReplay")) {
    Add-Result "Safety" "Runtime still does not submit to shadow replay" "PASS" "Runtime/prototype/mapper/validator files have no submit path; workflow replay is script-only and explicit."
} else {
    Add-Result "Safety" "Runtime still does not submit to shadow replay" "FAIL" (($runtimeSubmitHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$forbiddenOrder = @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder", "SendOrder", "OrderStatusRequest")
$orderHits = @()
foreach ($word in $forbiddenOrder) {
    $orderHits += @(Select-String -Path $prototypeFile,$workflowScript -Pattern $word -SimpleMatch -ErrorAction SilentlyContinue)
}
if ($orderHits.Count -eq 0) {
    Add-Result "Safety" "No order command surface" "PASS" "No order command surface found in prototype or workflow script."
} else {
    Add-Result "Safety" "No order command surface" "FAIL" (($orderHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$forbiddenMutation = @("IOrderRepository", "IFillRepository", "PositionRepository", "ModelRun", "RiskState", "Wallet", "SubmitToShadowReplayAsync")
$mutationHits = @()
foreach ($word in $forbiddenMutation) {
    $mutationHits += @(Select-String -Path $prototypeFile,$workflowScript,$mapperFile -Pattern $word -SimpleMatch -ErrorAction SilentlyContinue)
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

Add-Result "Runtime" "External socket attempts" "PASS" "No external snapshot attempts are made by this gate."

$failures = @($results | Where-Object { $_.status -eq "FAIL" })
$warnings = @($results | Where-Object { $_.status -eq "WARN" })
$decision = if ($failures.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS WITH KNOWN WARNINGS" } else { "PASS" }

$reportDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $reportDir "lmax-readonly-phase5q-workflow-hardening-gate-$stamp.json"
[ordered]@{
    gate = "LMAX Read-Only Runtime Phase 5Q Workflow Hardening Gate"
    finalDecision = $decision
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    checks = @($results)
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $reportPath"

if ($decision -eq "FAIL") { exit 1 }
