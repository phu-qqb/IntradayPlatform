param(
    [string]$StabilitySummaryFile
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

Write-Host "LMAX Read-Only Runtime Phase 5P Stability Readiness Gate"
Write-Host "Local-only. No LMAX connection, no runtime prototype call, no scheduler, and no runtime shadow replay submit."

$reviewScript = Join-Path $repoRoot "scripts/review-lmax-readonly-runtime-phase5o-stability-results.ps1"
$closureValidator = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyDemoSnapshotStabilityClosureValidator.cs"
$closureTests = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlyDemoSnapshotStabilityClosureValidatorTests.cs"
$phase5pDoc = Join-Path $repoRoot "docs/LMAX_READONLY_RUNTIME_PHASE5P_STABILITY_DECISION.md"
$prototypeFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlySocketPrototype.cs"
$stabilityScript = Join-Path $repoRoot "scripts/run-lmax-readonly-runtime-demo-snapshot-stability-check.ps1"
$mapperFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapper.cs"
$previewScript = Join-Path $repoRoot "scripts/preview-lmax-readonly-demo-snapshot-evidence.ps1"
$artifactValidatorScript = Join-Path $repoRoot "scripts/validate-lmax-readonly-runtime-demo-snapshot-artifact.ps1"

if ((Test-Contains $closureValidator "LmaxReadOnlyDemoSnapshotStabilityClosureValidator") -and
    (Test-Contains $closureValidator "PassWithWarnings") -and
    (Test-Contains $closureValidator "ReferencedArtifactMissing") -and
    (Test-Contains $closureValidator "EvidencePreviewModeNotMarketDataOnly")) {
    Add-Result "Files" "Closure validator exists" "PASS" "Phase 5P closure validator includes summary, artifact, and preview checks."
} else {
    Add-Result "Files" "Closure validator exists" "FAIL" "Closure validator or required markers are missing."
}

if ((Test-Contains $reviewScript "StabilitySummaryFile") -and
    (Test-Contains $reviewScript "validate-lmax-readonly-runtime-demo-snapshot-artifact.ps1") -and
    (Test-Contains $reviewScript "validate-lmax-lab-evidence-file.ps1") -and
    (Test-Contains $reviewScript "RuntimeShadowReplaySubmit: false")) {
    Add-Result "Files" "Review script exists" "PASS" "Review script validates summaries and referenced artifacts/previews without runtime submit."
} else {
    Add-Result "Files" "Review script exists" "FAIL" "Review script or required safety markers are missing."
}

if ((Test-Contains $closureTests "Successful_three_of_three_summary_validates_pass") -and
    (Test-Contains $closureTests "Failed_attempt_fails_closure") -and
    (Test-Contains $closureTests "Unsafe_root_flags_fail_closure") -and
    (Test-Contains $closureTests "Sentinel_secret_in_summary_fails_closure") -and
    (Test-Contains $closureTests "Referenced_unsafe_artifact_fails")) {
    Add-Result "Tests" "Closure validator tests exist" "PASS" "Success, failed attempts, unsafe flags, sentinel secrets, missing refs, and unsafe artifacts are covered."
} else {
    Add-Result "Tests" "Closure validator tests exist" "FAIL" "Phase 5P closure test markers are missing."
}

if ((Test-Contains $phase5pDoc "Phase 5P") -and
    (Test-Contains $phase5pDoc "3/3") -and
    (Test-Contains $phase5pDoc "does not authorize") -and
    (Test-Contains $phase5pDoc "FakeLmaxGateway")) {
    Add-Result "Docs" "Readiness decision doc exists" "PASS" "Phase 5P decision document is present."
} else {
    Add-Result "Docs" "Readiness decision doc exists" "FAIL" "Phase 5P decision document or required markers are missing."
}

if (-not [string]::IsNullOrWhiteSpace($StabilitySummaryFile)) {
    $summaryPath = if ([IO.Path]::IsPathRooted($StabilitySummaryFile)) { $StabilitySummaryFile } else { Join-Path $repoRoot $StabilitySummaryFile }
    powershell -NoProfile -ExecutionPolicy Bypass -File $reviewScript -StabilitySummaryFile $summaryPath | Tee-Object -Variable reviewOutput | Out-Host
    if ($LASTEXITCODE -eq 0 -and (($reviewOutput | Out-String) -match "Decision:\s*(PASS|PASS_WITH_WARNINGS)")) {
        Add-Result "Review" "Provided stability summary validates" "PASS" "Phase 5P review accepted $summaryPath"
    } else {
        Add-Result "Review" "Provided stability summary validates" "FAIL" "Phase 5P review failed for $summaryPath"
    }
} else {
    Add-Result "Review" "Provided stability summary validates" "WARN" "No -StabilitySummaryFile supplied; source-only gate checks ran."
}

$combinedText = (Get-Content -LiteralPath $stabilityScript -Raw)
if ($combinedText -notmatch "while\s*\(" -and
    $combinedText -notmatch "Register-ScheduledTask|New-ScheduledTask|Start-Job|Start-ThreadJob|Timer|HostedService") {
    Add-Result "Safety" "No scheduler or automatic polling" "PASS" "No scheduler, background job, timer, hosted service, or polling loop found in stability script."
} else {
    Add-Result "Safety" "No scheduler or automatic polling" "FAIL" "Scheduler, background job, timer, hosted service, or polling marker found."
}

$runtimeSubmitHits = @(Select-String -Path $prototypeFile,$mapperFile,$previewScript,$artifactValidatorScript,$reviewScript -Pattern "/lmax-shadow/replay","SubmitToShadowReplayAsync","ILmaxShadowReplayService","ReplayAsync" -SimpleMatch -ErrorAction SilentlyContinue)
if ($runtimeSubmitHits.Count -eq 0) {
    Add-Result "Safety" "Runtime still does not submit to shadow replay" "PASS" "No runtime/prototype/mapper/review shadow replay submit path found."
} else {
    Add-Result "Safety" "Runtime still does not submit to shadow replay" "FAIL" (($runtimeSubmitHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$forbiddenOrder = @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder", "SendOrder", "OrderStatusRequest", "TradeCapture")
$orderHits = @()
foreach ($word in $forbiddenOrder) {
    $orderHits += @(Select-String -Path $prototypeFile,$stabilityScript -Pattern $word -SimpleMatch -ErrorAction SilentlyContinue)
}
if ($orderHits.Count -eq 0) {
    Add-Result "Safety" "No order command surface" "PASS" "No order command surface found in prototype or stability files."
} else {
    Add-Result "Safety" "No order command surface" "FAIL" (($orderHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$forbiddenMutation = @("IOrderRepository", "IFillRepository", "PositionRepository", "ModelRun", "RiskState", "Wallet", "SubmitToShadowReplayAsync")
$mutationHits = @()
foreach ($word in $forbiddenMutation) {
    $mutationHits += @(Select-String -Path $prototypeFile,$stabilityScript,$reviewScript,$mapperFile,$previewScript -Pattern $word -SimpleMatch -ErrorAction SilentlyContinue)
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
$reportPath = Join-Path $reportDir "lmax-readonly-phase5p-stability-readiness-gate-$stamp.json"
[ordered]@{
    gate = "LMAX Read-Only Runtime Phase 5P Stability Readiness Gate"
    finalDecision = $decision
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    checks = @($results)
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $reportPath"

if ($decision -eq "FAIL") { exit 1 }
