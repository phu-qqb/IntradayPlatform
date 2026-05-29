param()

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

Write-Host "LMAX Read-Only Runtime Phase 5O Repeated Snapshot Stability Gate"
Write-Host "Local-only. This gate does not make external attempts, does not poll, and does not submit runtime shadow replay."

$stabilityScript = Join-Path $repoRoot "scripts/run-lmax-readonly-runtime-demo-snapshot-stability-check.ps1"
$prototypeScript = Join-Path $repoRoot "scripts/run-lmax-readonly-runtime-demo-snapshot-prototype.ps1"
$artifactValidatorScript = Join-Path $repoRoot "scripts/validate-lmax-readonly-runtime-demo-snapshot-artifact.ps1"
$previewScript = Join-Path $repoRoot "scripts/preview-lmax-readonly-demo-snapshot-evidence.ps1"
$replayScript = Join-Path $repoRoot "scripts/replay-lmax-readonly-demo-snapshot-evidence-preview.ps1"
$validatorFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyDemoSnapshotStabilitySummaryValidator.cs"
$validatorTestFile = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlyDemoSnapshotStabilitySummaryValidatorTests.cs"
$prototypeFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlySocketPrototype.cs"
$mapperFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapper.cs"
$gitignore = Join-Path $repoRoot ".gitignore"

if ((Test-Contains $stabilityScript "ConfirmRepeatedManualSnapshots") -and
    (Test-Contains $stabilityScript "AttemptCount") -and
    (Test-Contains $stabilityScript "DelaySeconds") -and
    (Test-Contains $stabilityScript "ContinueOnFailedSafe") -and
    (Test-Contains $stabilityScript "ReplayEvidencePreviews")) {
    Add-Result "Files" "Stability script exists with explicit controls" "PASS" "Manual batch confirmation, attempt cap, delay cap, failed-safe continuation, and optional replay controls are present."
} else {
    Add-Result "Files" "Stability script exists with explicit controls" "FAIL" "Stability script or required explicit control markers are missing."
}

if ((Test-Contains $stabilityScript "run-lmax-readonly-runtime-demo-snapshot-prototype.ps1") -and
    (Test-Contains $stabilityScript "validate-lmax-readonly-runtime-demo-snapshot-artifact.ps1") -and
    (Test-Contains $stabilityScript "preview-lmax-readonly-demo-snapshot-evidence.ps1")) {
    Add-Result "Flow" "Stability script reuses manual prototype and validation path" "PASS" "Each attempt calls the existing prototype, validates successful artifacts, and creates MarketDataOnly evidence previews."
} else {
    Add-Result "Flow" "Stability script reuses manual prototype and validation path" "FAIL" "Stability script does not show the expected prototype/validator/preview flow."
}

if ((Test-Contains $validatorFile "LmaxReadOnlyDemoSnapshotStabilitySummaryValidator") -and
    (Test-Contains $validatorFile "MaxAttemptCount = 5") -and
    (Test-Contains $validatorFile "MaxDelaySeconds = 10") -and
    (Test-Contains $validatorFile "credentialValuesReturned") -and
    (Test-Contains $validatorFile "orderSubmissionAttempted") -and
    (Test-Contains $validatorFile "shadowReplaySubmitAttempted") -and
    (Test-Contains $validatorFile "tradingMutationAttempted")) {
    Add-Result "Validation" "Stability summary validator exists" "PASS" "Validator enforces caps, redaction, and no order/replay/mutation flags."
} else {
    Add-Result "Validation" "Stability summary validator exists" "FAIL" "Validator file or required safety markers are missing."
}

if ((Test-Contains $validatorTestFile "Successful_stability_summary_validates") -and
    (Test-Contains $validatorTestFile "Failed_safe_attempts_can_be_aggregated") -and
    (Test-Contains $validatorTestFile "Attempt_count_above_cap_fails") -and
    (Test-Contains $validatorTestFile "Unsafe_attempt_flags_fail") -and
    (Test-Contains $validatorTestFile "Sentinel_secret_values_fail")) {
    Add-Result "Tests" "Stability validator tests exist" "PASS" "Summary aggregation, caps, unsafe flags, and secret redaction tests are present."
} else {
    Add-Result "Tests" "Stability validator tests exist" "FAIL" "Phase 5O validator test markers are missing."
}

if ((Test-Path -LiteralPath $gitignore) -and
    (Select-String -Path $gitignore -Pattern "artifacts/" -SimpleMatch -Quiet) -and
    (Test-Contains $stabilityScript "artifacts/lmax-readonly-runtime-demo-snapshot/stability")) {
    Add-Result "Artifacts" "Stability artifact directory ignored" "PASS" "Stability summaries are written under the ignored artifacts tree."
} else {
    Add-Result "Artifacts" "Stability artifact directory ignored" "FAIL" "Could not confirm ignored stability artifact output."
}

$stabilityText = if (Test-Path -LiteralPath $stabilityScript) { Get-Content -LiteralPath $stabilityScript -Raw } else { "" }
if ($stabilityText -notmatch "while\s*\(" -and
    $stabilityText -notmatch "Register-ScheduledTask|New-ScheduledTask|Start-Job|Start-ThreadJob|Timer|HostedService" -and
    $stabilityText.Contains('Start-Sleep -Seconds $DelaySeconds')) {
    Add-Result "Safety" "No scheduler or automatic polling" "PASS" "Only explicit bounded delay between planned manual attempts was found."
} else {
    Add-Result "Safety" "No scheduler or automatic polling" "FAIL" "Scheduler, polling, job, timer, hosted-service, or missing bounded-delay marker found."
}

$forbiddenOrder = @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder", "SendOrder", "OrderStatusRequest", "TradeCapture")
$orderHits = @()
foreach ($word in $forbiddenOrder) {
    $orderHits += @(Select-String -Path $prototypeFile,$prototypeScript,$stabilityScript -Pattern $word -SimpleMatch -ErrorAction SilentlyContinue)
}
if ($orderHits.Count -eq 0) {
    Add-Result "Safety" "No order command surface" "PASS" "No order command surface found in prototype or stability files."
} else {
    Add-Result "Safety" "No order command surface" "FAIL" (($orderHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$runtimeSubmitHits = @(Select-String -Path $prototypeFile,$prototypeScript,$mapperFile,$previewScript,$artifactValidatorScript -Pattern "/lmax-shadow/replay","SubmitToShadowReplayAsync","ILmaxShadowReplayService","ReplayAsync" -SimpleMatch -ErrorAction SilentlyContinue)
if ($runtimeSubmitHits.Count -eq 0 -and (Test-Contains $stabilityScript "ReplayEvidencePreviews")) {
    Add-Result "Safety" "Runtime still does not submit to shadow replay" "PASS" "Runtime/prototype/mapper files have no submit path; optional replay is script-only and explicit."
} else {
    Add-Result "Safety" "Runtime still does not submit to shadow replay" "FAIL" (($runtimeSubmitHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$forbiddenMutation = @("IOrderRepository", "IFillRepository", "PositionRepository", "ModelRun", "RiskState", "Wallet", "SubmitToShadowReplayAsync")
$mutationHits = @()
foreach ($word in $forbiddenMutation) {
    $mutationHits += @(Select-String -Path $prototypeFile,$prototypeScript,$stabilityScript,$mapperFile,$previewScript,$replayScript -Pattern $word -SimpleMatch -ErrorAction SilentlyContinue)
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

try {
    $refusalOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $stabilityScript 2>&1
    $refusalExit = $LASTEXITCODE
    $joined = ($refusalOutput | Out-String)
    if ($refusalExit -ne 0 -and $joined -match "AllowExternalConnections|ConfirmRepeatedManualSnapshots|Reason|AttemptCount") {
        Add-Result "Script" "Stability script refuses without explicit flags" "PASS" "No-flag invocation failed closed before any external attempt."
    } else {
        Add-Result "Script" "Stability script refuses without explicit flags" "FAIL" "No-flag invocation did not fail closed with expected manual control diagnostics."
    }
} catch {
    $message = $_.Exception.Message
    if ($message -match "AllowExternalConnections|ConfirmRepeatedManualSnapshots|Reason|AttemptCount") {
        Add-Result "Script" "Stability script refuses without explicit flags" "PASS" "No-flag invocation failed closed before any external attempt."
    } else {
        Add-Result "Script" "Stability script refuses without explicit flags" "FAIL" "Unexpected no-flag invocation failure: $message"
    }
}

Add-Result "Runtime" "External socket attempts" "PASS" "No external snapshot attempts are made by this gate."

$failures = @($results | Where-Object { $_.status -eq "FAIL" })
$warnings = @($results | Where-Object { $_.status -eq "WARN" })
$decision = if ($failures.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS WITH KNOWN WARNINGS" } else { "PASS" }

$reportDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $reportDir "lmax-readonly-phase5o-stability-gate-$stamp.json"
[ordered]@{
    gate = "LMAX Read-Only Runtime Phase 5O Repeated Snapshot Stability Gate"
    finalDecision = $decision
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    checks = @($results)
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $reportPath"

if ($decision -eq "FAIL") { exit 1 }
