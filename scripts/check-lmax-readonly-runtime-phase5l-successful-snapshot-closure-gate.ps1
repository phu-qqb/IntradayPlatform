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

Write-Host "LMAX Read-Only Runtime Phase 5L Successful Snapshot Closure Gate"
Write-Host "Local-only. This gate does not connect to LMAX and does not run a real external socket attempt."

$validatorFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyDemoSnapshotArtifactValidator.cs"
$validatorScript = Join-Path $repoRoot "scripts/validate-lmax-readonly-runtime-demo-snapshot-artifact.ps1"
$testFile = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlyDemoSnapshotArtifactValidatorTests.cs"
$prototypeFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlySocketPrototype.cs"
$manualScript = Join-Path $repoRoot "scripts/run-lmax-readonly-runtime-demo-snapshot-prototype.ps1"

if ((Test-Contains $validatorFile "LmaxReadOnlyDemoSnapshotArtifactValidator") -and
    (Test-Contains $validatorFile "credentialValuesReturned") -and
    (Test-Contains $validatorFile "orderSubmissionAttempted") -and
    (Test-Contains $validatorScript "ArtifactFile")) {
    Add-Result "Files" "Artifact validator exists" "PASS" "Code and local script validators are present."
} else {
    Add-Result "Files" "Artifact validator exists" "FAIL" "Validator code or script markers are missing."
}

if ((Test-Contains $testFile "Successful_sanitized_artifact_validates") -and
    (Test-Contains $testFile "Sentinel_secret_values_fail") -and
    (Test-Contains $testFile "Raw_fix_password_tag_fails")) {
    Add-Result "Tests" "Artifact validation scenarios covered" "PASS" "Success, unsafe flags, sentinel values, and FIX password tags are covered."
} else {
    Add-Result "Tests" "Artifact validation scenarios covered" "FAIL" "Phase 5L artifact validator test markers are missing."
}

$gitignore = Join-Path $repoRoot ".gitignore"
if ((Test-Path -LiteralPath $gitignore) -and (Select-String -Path $gitignore -Pattern "artifacts/" -SimpleMatch -Quiet)) {
    Add-Result "Artifacts" "Artifact directory ignored" "PASS" "The artifacts tree is ignored by git."
} else {
    Add-Result "Artifacts" "Artifact directory ignored" "FAIL" "The artifacts tree is not ignored."
}

if (-not [string]::IsNullOrWhiteSpace($ArtifactFile)) {
    $artifactPath = if ([IO.Path]::IsPathRooted($ArtifactFile)) { $ArtifactFile } else { Join-Path $repoRoot $ArtifactFile }
    powershell -NoProfile -ExecutionPolicy Bypass -File $validatorScript -ArtifactFile $artifactPath | Tee-Object -Variable validationOutput | Out-Host
    if ($LASTEXITCODE -eq 0) {
        Add-Result "Artifact" "Provided successful snapshot artifact validates" "PASS" "Validated: $artifactPath"
    } else {
        Add-Result "Artifact" "Provided successful snapshot artifact validates" "FAIL" "Validation failed: $artifactPath"
    }
} else {
    Add-Result "Artifact" "Provided successful snapshot artifact validates" "WARN" "No -ArtifactFile supplied; source-only closure checks ran."
}

$forbiddenOrder = @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder", "SendOrder", "TradeCapture", "OrderStatusRequest")
$orderHits = @()
foreach ($word in $forbiddenOrder) {
    $orderHits += @(Select-String -Path $prototypeFile,$manualScript -Pattern $word -SimpleMatch -ErrorAction SilentlyContinue)
}
if ($orderHits.Count -eq 0) {
    Add-Result "Safety" "No order/status/trade-capture surface" "PASS" "No order, trade-capture, or order-status surface found in runtime prototype files."
} else {
    Add-Result "Safety" "No order/status/trade-capture surface" "FAIL" (($orderHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$forbiddenMutation = @("IOrderRepository", "IFillRepository", "PositionRepository", "ModelRun", "RiskState", "Wallet", "SubmitToShadowReplayAsync")
$mutationHits = @()
foreach ($word in $forbiddenMutation) {
    $mutationHits += @(Select-String -Path $prototypeFile,$manualScript,$validatorFile,$validatorScript -Pattern $word -SimpleMatch -ErrorAction SilentlyContinue)
}
if ($mutationHits.Count -eq 0) {
    Add-Result "Safety" "No mutation or shadow submit dependency" "PASS" "No trading-state repository or shadow-submit surface found."
} else {
    Add-Result "Safety" "No mutation or shadow submit dependency" "FAIL" (($mutationHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
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

Add-Result "Manual run" "External socket attempt" "PASS" "No real external socket attempt is made by this gate."

$failures = @($results | Where-Object { $_.status -eq "FAIL" })
$warnings = @($results | Where-Object { $_.status -eq "WARN" })
$decision = if ($failures.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS WITH KNOWN WARNINGS" } else { "PASS" }

$reportDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $reportDir "lmax-readonly-phase5l-successful-snapshot-closure-gate-$stamp.json"
[ordered]@{
    gate = "LMAX Read-Only Runtime Phase 5L Successful Snapshot Closure Gate"
    finalDecision = $decision
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    checks = @($results)
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $reportPath"

if ($decision -eq "FAIL") { exit 1 }
