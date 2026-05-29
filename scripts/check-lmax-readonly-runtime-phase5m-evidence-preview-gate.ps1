param(
    [string]$ArtifactFile
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

Write-Host "LMAX Read-Only Runtime Phase 5M Evidence Preview Gate"
Write-Host "Local-only. This gate does not connect to LMAX and does not submit to shadow replay."

$mapperFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapper.cs"
$mapperTestFile = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapperTests.cs"
$previewScript = Join-Path $repoRoot "scripts/preview-lmax-readonly-demo-snapshot-evidence.ps1"
$artifactValidatorFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyDemoSnapshotArtifactValidator.cs"
$artifactValidatorScript = Join-Path $repoRoot "scripts/validate-lmax-readonly-runtime-demo-snapshot-artifact.ps1"
$prototypeFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlySocketPrototype.cs"
$manualScript = Join-Path $repoRoot "scripts/run-lmax-readonly-runtime-demo-snapshot-prototype.ps1"

if ((Test-Contains $mapperFile "LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapper") -and
    (Test-Contains $mapperFile "MarketDataOnly") -and
    (Test-Contains $mapperFile "RuntimeDemoReadOnlySnapshotPreview") -and
    (Test-Contains $mapperFile "ShadowReplaySubmitAttempted")) {
    Add-Result "Files" "Evidence preview mapper exists" "PASS" "Phase 5M mapper and MarketDataOnly preview markers are present."
} else {
    Add-Result "Files" "Evidence preview mapper exists" "FAIL" "Mapper file or required markers are missing."
}

if ((Test-Contains $previewScript "ArtifactFile") -and
    (Test-Contains $previewScript "validate-lmax-readonly-runtime-demo-snapshot-artifact.ps1") -and
    (Test-Contains $previewScript "validate-lmax-lab-evidence-file.ps1") -and
    (Test-Contains $previewScript "ShadowReplaySubmitAttempted: false")) {
    Add-Result "Files" "Preview script exists" "PASS" "Local preview script validates the artifact and evidence contract."
} else {
    Add-Result "Files" "Preview script exists" "FAIL" "Preview script or required validation markers are missing."
}

if ((Test-Contains $mapperTestFile "Successful_artifact_maps_to_market_data_only_evidence_preview") -and
    (Test-Contains $mapperTestFile "Existing_fixture_validator_accepts_mapped_preview") -and
    (Test-Contains $mapperTestFile "Unsafe_credential_values_returned_artifact_fails_before_mapping") -and
    (Test-Contains $mapperTestFile "Unsafe_order_submission_artifact_fails_before_mapping")) {
    Add-Result "Tests" "Evidence preview mapper scenarios covered" "PASS" "Success mapping, validator acceptance, and unsafe pre-map blockers are covered."
} else {
    Add-Result "Tests" "Evidence preview mapper scenarios covered" "FAIL" "Phase 5M mapper test markers are missing."
}

$gitignore = Join-Path $repoRoot ".gitignore"
if ((Test-Path -LiteralPath $gitignore) -and (Select-String -Path $gitignore -Pattern "artifacts/" -SimpleMatch -Quiet)) {
    Add-Result "Artifacts" "Preview artifact directory ignored" "PASS" "The artifacts tree is ignored by git."
} else {
    Add-Result "Artifacts" "Preview artifact directory ignored" "FAIL" "The artifacts tree is not ignored."
}

if (-not [string]::IsNullOrWhiteSpace($ArtifactFile)) {
    $artifactPath = if ([IO.Path]::IsPathRooted($ArtifactFile)) { $ArtifactFile } else { Join-Path $repoRoot $ArtifactFile }
    powershell -NoProfile -ExecutionPolicy Bypass -File $previewScript -ArtifactFile $artifactPath | Tee-Object -Variable previewOutput | Out-Host
    if ($LASTEXITCODE -eq 0) {
        Add-Result "Preview" "Provided artifact maps and validates" "PASS" "Mapped MarketDataOnly evidence preview for $artifactPath"
    } else {
        Add-Result "Preview" "Provided artifact maps and validates" "FAIL" "Preview mapping failed for $artifactPath"
    }
} else {
    Add-Result "Preview" "Provided artifact maps and validates" "WARN" "No -ArtifactFile supplied; source-only gate checks ran."
}

$forbiddenOrder = @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder", "SendOrder", "OrderStatusRequest")
$orderHits = @()
foreach ($word in $forbiddenOrder) {
    $orderHits += @(Select-String -Path $prototypeFile,$manualScript,$mapperFile -Pattern $word -SimpleMatch -ErrorAction SilentlyContinue)
}
if ($orderHits.Count -eq 0) {
    Add-Result "Safety" "No order command surface" "PASS" "No order command surface found in runtime prototype or Phase 5M preview files."
} else {
    Add-Result "Safety" "No order command surface" "FAIL" (($orderHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$forbiddenMutation = @("IOrderRepository", "IFillRepository", "PositionRepository", "ModelRun", "RiskState", "Wallet", "SubmitToShadowReplayAsync")
$mutationHits = @()
foreach ($word in $forbiddenMutation) {
    $mutationHits += @(Select-String -Path $prototypeFile,$manualScript,$mapperFile,$previewScript,$artifactValidatorFile,$artifactValidatorScript -Pattern $word -SimpleMatch -ErrorAction SilentlyContinue)
}
if ($mutationHits.Count -eq 0) {
    Add-Result "Safety" "No mutation or shadow-submit dependency" "PASS" "No trading-state repository or shadow-submit surface found."
} else {
    Add-Result "Safety" "No mutation or shadow-submit dependency" "FAIL" (($mutationHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
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
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No prototype, real gateway, or hosted service registration found."
} else {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" "FakeLmaxGateway registration missing or prototype/live registration found."
}

Add-Result "Runtime" "External socket attempt" "PASS" "No real external socket attempt is made by this gate."
Add-Result "Runtime" "Shadow replay submit" "PASS" "No shadow replay submit is made by this gate."

$failures = @($results | Where-Object { $_.status -eq "FAIL" })
$warnings = @($results | Where-Object { $_.status -eq "WARN" })
$decision = if ($failures.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS WITH KNOWN WARNINGS" } else { "PASS" }

$reportDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $reportDir "lmax-readonly-phase5m-evidence-preview-gate-$stamp.json"
[ordered]@{
    gate = "LMAX Read-Only Runtime Phase 5M Evidence Preview Gate"
    finalDecision = $decision
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    checks = @($results)
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $reportPath"

if ($decision -eq "FAIL") { exit 1 }
