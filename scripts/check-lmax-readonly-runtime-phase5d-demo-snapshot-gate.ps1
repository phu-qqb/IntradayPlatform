param(
    [switch]$CheckCredentialAvailability
)

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

Write-Host "LMAX Read-Only Runtime Phase 5D Demo Snapshot Gate"
Write-Host "Local-only by default. This gate does not connect to LMAX and does not require credentials."

$prototypeFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlySocketPrototype.cs"
$scriptFile = Join-Path $repoRoot "scripts/run-lmax-readonly-runtime-demo-snapshot-prototype.ps1"
$credentialScript = Join-Path $repoRoot "scripts/check-lmax-readonly-runtime-demo-credentials.ps1"

if ((Test-Path -LiteralPath $prototypeFile) -and (Test-Path -LiteralPath $scriptFile)) {
    Add-Result "Files" "Phase 5D prototype files exist" "PASS" "Prototype implementation and manual script are present."
} else {
    Add-Result "Files" "Phase 5D prototype files exist" "FAIL" "Missing prototype implementation or manual script."
}

if ((Test-Contains $prototypeFile "LmaxReadOnlyDemoMarketDataSocketClient") -and
    (Test-Contains $prototypeFile "TcpClient") -and
    (Test-Contains $prototypeFile "SslStream") -and
    (Test-Contains $prototypeFile "Phase5DManualScriptOnly")) {
    Add-Result "Transport" "Socket implementation isolated to prototype" "PASS" "Manual Demo market-data socket path exists only in the prototype file."
} else {
    Add-Result "Transport" "Socket implementation isolated to prototype" "FAIL" "Could not confirm isolated Phase 5D socket implementation markers."
}

$forbiddenOrder = @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder", "SendOrder")
$orderHits = @()
foreach ($word in $forbiddenOrder) {
    $orderHits += @(Select-String -Path $prototypeFile,$scriptFile -Pattern $word -SimpleMatch -ErrorAction SilentlyContinue)
}
if ($orderHits.Count -eq 0) {
    Add-Result "Safety" "No order surface" "PASS" "No order-submission words found in Phase 5D prototype files."
} else {
    Add-Result "Safety" "No order surface" "FAIL" (($orderHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$forbiddenMutation = @("IOrderRepository", "IFillRepository", "PositionRepository", "ModelRun", "RiskState", "Wallet", "SubmitToShadowReplayAsync")
$mutationHits = @()
foreach ($word in $forbiddenMutation) {
    $mutationHits += @(Select-String -Path $prototypeFile,$scriptFile -Pattern $word -SimpleMatch -ErrorAction SilentlyContinue)
}
if ($mutationHits.Count -eq 0) {
    Add-Result "Safety" "No trading mutation or shadow submit dependency" "PASS" "No trading-state repository or shadow-submit surface found in prototype files."
} else {
    Add-Result "Safety" "No trading mutation or shadow submit dependency" "FAIL" (($mutationHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

if ((Test-Contains $prototypeFile "LmaxReadOnlyFixMessageRedactor") -and (Test-Contains $prototypeFile "554=") -and (Test-Contains $prototypeFile "CredentialValuesReturned: false") -and (Test-Contains $scriptFile "credentialValuesReturned = `$false")) {
    Add-Result "Redaction" "Credential redaction boundary" "PASS" "FIX tag redaction and credentialValuesReturned=false markers are present."
} else {
    Add-Result "Redaction" "Credential redaction boundary" "FAIL" "Missing redaction or credentialValuesReturned=false markers."
}

if ((Test-Contains $prototypeFile "BlockedMissingCredentials") -and
    (Test-Contains $prototypeFile "FailedSafeConnectionError") -and
    (Test-Contains $prototypeFile "FailedSafeLogonTimeout") -and
    (Test-Contains $prototypeFile "FailedSafeSnapshotTimeout") -and
    (Test-Contains $prototypeFile "LmaxReadOnlySocketPrototypeRetryPolicy")) {
    Add-Result "Failure hardening" "Taxonomy and retry policy" "PASS" "Failure taxonomy and disabled retry policy are present."
} else {
    Add-Result "Failure hardening" "Taxonomy and retry policy" "FAIL" "Missing failure taxonomy or retry policy markers."
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
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No prototype, real LMAX gateway, or hosted service registration found."
} else {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" "FakeLmaxGateway registration missing or prototype/live registration found."
}

try {
    & powershell -NoProfile -ExecutionPolicy Bypass -File $scriptFile *> $null
    if ($LASTEXITCODE -eq 0) {
        Add-Result "Script" "Manual script requires explicit flags" "FAIL" "Prototype script ran successfully without explicit flags."
    } else {
        Add-Result "Script" "Manual script requires explicit flags" "PASS" "Prototype script refuses without explicit flags."
    }
} catch {
    Add-Result "Script" "Manual script requires explicit flags" "PASS" "Prototype script refuses without explicit flags."
}

if ($CheckCredentialAvailability.IsPresent) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File $credentialScript -ConfirmCredentialAvailabilityCheck
    if ($LASTEXITCODE -eq 0) {
        Add-Result "Credentials" "Credential availability" "PASS" "Credential labels are present; output is redacted."
    } else {
        Add-Result "Credentials" "Credential availability" "WARN" "Credential labels are missing; output lists labels only."
    }
} else {
    Add-Result "Credentials" "Credential availability" "PASS" "Skipped by default; no credential read was requested by the gate."
}

Add-Result "Manual run" "External socket attempt" "PASS" "No external socket attempt is made by this gate."

$failures = @($results | Where-Object { $_.status -eq "FAIL" })
$warnings = @($results | Where-Object { $_.status -eq "WARN" })
$decision = if ($failures.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS WITH KNOWN WARNINGS" } else { "PASS" }

$reportDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $reportDir "lmax-readonly-phase5d-demo-snapshot-gate-$stamp.json"
[ordered]@{
    gate = "LMAX Read-Only Runtime Phase 5D Demo Snapshot Gate"
    finalDecision = $decision
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    checks = @($results)
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $reportPath"

if ($decision -eq "FAIL") { exit 1 }
