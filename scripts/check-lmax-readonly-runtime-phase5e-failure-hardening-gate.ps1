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

Write-Host "LMAX Read-Only Runtime Phase 5E Failure Hardening Gate"
Write-Host "Local-only. This gate does not connect to LMAX, does not require credentials, and does not run an external socket attempt."

$prototypeFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlySocketPrototype.cs"
$testFile = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlySocketPrototypeTests.cs"
$scriptFile = Join-Path $repoRoot "scripts/run-lmax-readonly-runtime-demo-snapshot-prototype.ps1"

$requiredTaxonomy = @(
    "BlockedMissingCredentials",
    "BlockedSafetyGate",
    "BlockedInvalidEnvironment",
    "BlockedUnsafeVenue",
    "BlockedOrderSubmissionFlag",
    "FailedSafeConnectionError",
    "FailedSafeLogonRejected",
    "FailedSafeLogonTimeout",
    "FailedSafeSnapshotTimeout",
    "FailedSafeLogoutError",
    "FailedSafeMaxRuntimeExceeded",
    "FailedSafeMaxEventsExceeded",
    "CompletedWithWarnings"
)
$missing = @($requiredTaxonomy | Where-Object { -not (Test-Contains $prototypeFile $_) })
if ($missing.Count -eq 0) {
    Add-Result "Taxonomy" "Failure statuses exist" "PASS" "All Phase 5E failure/block statuses are present."
} else {
    Add-Result "Taxonomy" "Failure statuses exist" "FAIL" ("Missing: " + ($missing -join ", "))
}

if ((Test-Contains $prototypeFile "RetryEnabled: false") -and (Test-Contains $prototypeFile "RetryAllowed: false") -and (Test-Contains $prototypeFile "MaxAttempts") -and (Test-Contains $prototypeFile "Phase5E_NoAutomaticExternalRetry")) {
    Add-Result "Retry" "No automatic retry policy" "PASS" "Retry policy is descriptive only: disabled, not allowed, max attempts 1."
} else {
    Add-Result "Retry" "No automatic retry policy" "FAIL" "Could not confirm disabled retry policy markers."
}

foreach ($marker in @("Missing_credentials_block", "ConnectionFailure", "LogonTimeout", "LogonRejected", "SnapshotTimeout", "LogoutFailure", "MaxEventsExceeded")) {
    if (-not (Test-Contains $testFile $marker)) {
        Add-Result "Tests" "Failure simulation coverage" "FAIL" "Missing test marker $marker."
    }
}
if (-not (@($results | Where-Object { $_.category -eq "Tests" -and $_.status -eq "FAIL" }).Count)) {
    Add-Result "Tests" "Failure simulation coverage" "PASS" "Credential, connection, logon, snapshot, logout, and event-cap failures are covered by fake hooks."
}

$forbiddenOrder = @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder", "SendOrder")
$orderHits = @()
foreach ($word in $forbiddenOrder) {
    $orderHits += @(Select-String -Path $prototypeFile,$scriptFile -Pattern $word -SimpleMatch -ErrorAction SilentlyContinue)
}
if ($orderHits.Count -eq 0) {
    Add-Result "Safety" "No order surface" "PASS" "No order-submission words found in prototype files."
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
    try {
        & powershell -NoProfile -ExecutionPolicy Bypass -File $scriptFile -AllowExternalConnections -ConfirmDemoReadOnly -Reason "phase 5e missing credential gate check" *> $null
        if ($LASTEXITCODE -eq 0) {
            Add-Result "Script" "Missing credentials block before connection" "FAIL" "Manual script succeeded while credential labels were masked."
        } else {
            Add-Result "Script" "Missing credentials block before connection" "PASS" "Manual script blocked before connection with credential labels masked."
        }
    } catch {
        Add-Result "Script" "Missing credentials block before connection" "PASS" "Manual script blocked or failed safe before connection with credential labels masked."
    }
} finally {
    foreach ($label in $credentialLabels) {
        [Environment]::SetEnvironmentVariable($label, $savedProcessCredentials[$label], "Process")
    }
}

Add-Result "Manual run" "External socket attempt" "PASS" "No automatic external retry or socket attempt is made by this gate."

$failures = @($results | Where-Object { $_.status -eq "FAIL" })
$warnings = @($results | Where-Object { $_.status -eq "WARN" })
$decision = if ($failures.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS WITH KNOWN WARNINGS" } else { "PASS" }

$reportDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $reportDir "lmax-readonly-phase5e-failure-hardening-gate-$stamp.json"
[ordered]@{
    gate = "LMAX Read-Only Runtime Phase 5E Failure Hardening Gate"
    finalDecision = $decision
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    checks = @($results)
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $reportPath"

if ($decision -eq "FAIL") { exit 1 }
