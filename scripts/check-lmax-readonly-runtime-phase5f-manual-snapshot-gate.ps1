param()

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()

function Add-Result([string]$Category, [string]$Check, [string]$Status, [string]$Detail) {
    $script:results += [ordered]@{
        category = $Category
        check = $Check
        status = $Status
        detail = $Detail
    }
    Write-Host ("{0}: {1} / {2} - {3}" -f $Status, $Category, $Check, $Detail)
}

function Test-Contains([string]$Path, [string]$Pattern) {
    return (Test-Path -LiteralPath $Path) -and [bool](Select-String -Path $Path -Pattern $Pattern -SimpleMatch -Quiet)
}

Write-Host "LMAX Read-Only Runtime Phase 5F Manual Snapshot Gate"
Write-Host "Local-only by default. This gate masks credential labels and does not run a real external socket attempt."

$prototypeFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlySocketPrototype.cs"
$testFile = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlySocketPrototypeTests.cs"
$scriptFile = Join-Path $repoRoot "scripts/run-lmax-readonly-runtime-demo-snapshot-prototype.ps1"
$gitIgnore = Join-Path $repoRoot ".gitignore"

if ((Test-Path -LiteralPath $prototypeFile) -and (Test-Path -LiteralPath $scriptFile)) {
    Add-Result "Files" "Phase 5F prototype files exist" "PASS" "Prototype implementation and manual script are present."
} else {
    Add-Result "Files" "Phase 5F prototype files exist" "FAIL" "Prototype implementation or manual script is missing."
}

if ((Test-Contains $scriptFile "AllowExternalConnections") -and (Test-Contains $scriptFile "ConfirmDemoReadOnly") -and (Test-Contains $scriptFile "Reason")) {
    Add-Result "Script" "Manual flags required" "PASS" "Manual script exposes required explicit operator flags."
} else {
    Add-Result "Script" "Manual flags required" "FAIL" "Required manual flags were not found."
}

if ((Test-Contains $scriptFile "artifacts/lmax-readonly-runtime-demo-snapshot") -and (Test-Contains $gitIgnore "artifacts/")) {
    Add-Result "Artifacts" "Sanitized artifact directory ignored" "PASS" "Result artifacts are written below the ignored artifacts tree."
} else {
    Add-Result "Artifacts" "Sanitized artifact directory ignored" "FAIL" "Could not confirm ignored sanitized artifact output."
}

if ((Test-Contains $prototypeFile "LmaxReadOnlySocketPrototypeSanitizedArtifactWriter") -and
    (Test-Contains $testFile "Sanitized_artifact_writer_never_writes_sentinel_credentials") -and
    (Test-Contains $prototypeFile "privateKey") -and
    (Test-Contains $prototypeFile "authorization")) {
    Add-Result "Redaction" "Sanitized capture and tests exist" "PASS" "Artifact writer and redaction tests/markers are present."
} else {
    Add-Result "Redaction" "Sanitized capture and tests exist" "FAIL" "Sanitized artifact writer, redaction markers, or tests are missing."
}

$forbiddenOrder = @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder", "SendOrder")
$orderHits = @()
foreach ($word in $forbiddenOrder) {
    $orderHits += @(Select-String -Path $prototypeFile,$scriptFile -Pattern $word -SimpleMatch -ErrorAction SilentlyContinue)
}
if ($orderHits.Count -eq 0) {
    Add-Result "Safety" "No order surface" "PASS" "No order-submission words found in Phase 5F prototype files."
} else {
    Add-Result "Safety" "No order surface" "FAIL" (($orderHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$forbiddenMutation = @("IOrderRepository", "IFillRepository", "PositionRepository", "ModelRun", "RiskState", "Wallet", "SubmitToShadowReplayAsync")
$mutationHits = @()
foreach ($word in $forbiddenMutation) {
    $mutationHits += @(Select-String -Path $prototypeFile,$scriptFile -Pattern $word -SimpleMatch -ErrorAction SilentlyContinue)
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

$credentialLabels = @(
    "LMAX_DEMO_FIX_USERNAME",
    "LMAX_DEMO_FIX_PASSWORD",
    "LMAX_DEMO_SENDER_COMP_ID",
    "LMAX_DEMO_TARGET_COMP_ID"
)
$savedProcessCredentials = @{}
foreach ($label in $credentialLabels) {
    $savedProcessCredentials[$label] = [Environment]::GetEnvironmentVariable($label, "Process")
    [Environment]::SetEnvironmentVariable($label, "", "Process")
}
try {
    $output = & powershell -NoProfile -ExecutionPolicy Bypass -File $scriptFile -AllowExternalConnections -ConfirmDemoReadOnly -Reason "phase 5f masked credential gate check" 2>&1
    $joined = ($output | Out-String)
    if ($LASTEXITCODE -ne 0 -and
        $joined -match "BlockedMissingCredentials" -and
        $joined -match '"externalConnectionAttempted":\s*false' -and
        $joined -match '"logonAttempted":\s*false' -and
        $joined -match '"orderSubmissionAttempted":\s*false') {
        Add-Result "Script" "Masked missing credentials block before connection" "PASS" "Manual script blocked before connection with credential labels masked."
    } else {
        Add-Result "Script" "Masked missing credentials block before connection" "FAIL" "Manual script did not return the expected masked missing-credential blocked result."
    }
} finally {
    foreach ($label in $credentialLabels) {
        [Environment]::SetEnvironmentVariable($label, $savedProcessCredentials[$label], "Process")
    }
}

Add-Result "Manual run" "External socket attempt" "PASS" "No real external socket attempt is made by this gate."

$failures = @($results | Where-Object { $_.status -eq "FAIL" })
$warnings = @($results | Where-Object { $_.status -eq "WARN" })
$decision = if ($failures.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS WITH KNOWN WARNINGS" } else { "PASS" }

$reportDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $reportDir "lmax-readonly-phase5f-manual-snapshot-gate-$stamp.json"
[ordered]@{
    gate = "LMAX Read-Only Runtime Phase 5F Manual Snapshot Gate"
    finalDecision = $decision
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    checks = @($results)
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $reportPath"

if ($decision -eq "FAIL") { exit 1 }
