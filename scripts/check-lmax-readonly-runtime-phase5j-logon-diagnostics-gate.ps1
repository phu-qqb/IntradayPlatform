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

Write-Host "LMAX Read-Only Runtime Phase 5J Logon Diagnostics Gate"
Write-Host "Local-only. This gate does not connect to LMAX and does not run a real external socket attempt."

$prototypeFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlySocketPrototype.cs"
$testFile = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlySocketPrototypeTests.cs"
$scriptFile = Join-Path $repoRoot "scripts/run-lmax-readonly-runtime-demo-snapshot-prototype.ps1"

$markers = @(
    "LmaxReadOnlyFixLogonDiagnostics",
    "LmaxReadOnlyFixSessionProfileComparison",
    "FailedSafeLogonLogoutReceived",
    "FailedSafeLogonRejectReceived",
    "FailedSafeLogonProfileMismatchSuspected",
    "FailedSafeLogonTargetCompIdSuspected",
    "FailedSafeLogonSenderCompIdSuspected",
    "FailedSafeLogonCredentialsSuspected",
    "FailedSafeLogonUnknown",
    "phase5j-logon-diagnostics-v1",
    "FirstInboundLogoutText",
    "LogonWaitDurationMs"
)
$missing = @($markers | Where-Object { -not (Test-Contains $prototypeFile $_) })
if ($missing.Count -eq 0) {
    Add-Result "Diagnostics" "Logon model and classifications exist" "PASS" "Phase 5J sanitized logon diagnostics and failure classifications are present."
} else {
    Add-Result "Diagnostics" "Logon model and classifications exist" "FAIL" ("Missing: " + ($missing -join ", "))
}

if ((Test-Contains $testFile "LogonLogoutReceived") -and
    (Test-Contains $testFile "LogonSessionRejectReceived") -and
    (Test-Contains $testFile "Logon_logout_before_confirmation_has_sanitized_diagnostics") -and
    (Test-Contains $testFile "Session_profile_comparison_uses_only_safe_labels")) {
    Add-Result "Tests" "Logon diagnostics scenarios covered" "PASS" "Unit test markers cover logout/reject before logon confirmation and profile comparison."
} else {
    Add-Result "Tests" "Logon diagnostics scenarios covered" "FAIL" "Phase 5J logon diagnostics test markers are missing."
}

if ((Test-Contains $scriptFile "ShowSanitizedLogonDiagnostics") -and
    (Test-Contains $scriptFile "New-LogonDiagnostics") -and
    (Test-Contains $scriptFile "FailedSafeLogonLogoutReceived")) {
    Add-Result "Script" "Manual sanitized logon diagnostics option exists" "PASS" "Manual prototype can print sanitized logon diagnostics when explicitly requested."
} else {
    Add-Result "Script" "Manual sanitized logon diagnostics option exists" "FAIL" "Manual script logon diagnostics markers are missing."
}

$forbiddenOrder = @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder", "SendOrder")
$orderHits = @()
foreach ($word in $forbiddenOrder) {
    $orderHits += @(Select-String -Path $prototypeFile,$scriptFile -Pattern $word -SimpleMatch -ErrorAction SilentlyContinue)
}
if ($orderHits.Count -eq 0) {
    Add-Result "Safety" "No order surface" "PASS" "No order-submission words found in Phase 5J prototype files."
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

Add-Result "Manual run" "External socket attempt" "PASS" "No real external socket attempt is made by this gate."

$failures = @($results | Where-Object { $_.status -eq "FAIL" })
$warnings = @($results | Where-Object { $_.status -eq "WARN" })
$decision = if ($failures.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS WITH KNOWN WARNINGS" } else { "PASS" }

$reportDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $reportDir "lmax-readonly-phase5j-logon-diagnostics-gate-$stamp.json"
[ordered]@{
    gate = "LMAX Read-Only Runtime Phase 5J Logon Diagnostics Gate"
    finalDecision = $decision
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    checks = @($results)
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $reportPath"

if ($decision -eq "FAIL") { exit 1 }
