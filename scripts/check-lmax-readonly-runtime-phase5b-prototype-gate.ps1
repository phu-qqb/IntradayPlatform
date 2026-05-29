param(
    [switch]$AllowExternalPrototypeRun
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

function Test-FileContains([string]$Path, [string]$Pattern) {
    if (-not (Test-Path -LiteralPath $Path)) { return $false }
    return [bool](Select-String -Path $Path -Pattern $Pattern -SimpleMatch -Quiet)
}

Write-Host "LMAX Read-Only Runtime Phase 5B Prototype Gate"
Write-Host "Local-only by default. This gate does not connect to LMAX or require credentials. After Phase 5D, the prototype may contain an isolated manual socket path."

$prototypeFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlySocketPrototype.cs"
$scriptFile = Join-Path $repoRoot "scripts/run-lmax-readonly-runtime-demo-snapshot-prototype.ps1"
if ((Test-Path -LiteralPath $prototypeFile) -and (Test-Path -LiteralPath $scriptFile)) {
    Add-Result "Files" "Prototype files exist" "PASS" "Dedicated prototype transport and manual script are present."
} else {
    Add-Result "Files" "Prototype files exist" "FAIL" "Missing prototype transport or manual script."
}

if ((Test-FileContains $prototypeFile "Phase5DManualScriptOnly") -and (Test-FileContains $prototypeFile "ExternalConnectionAttempted: false")) {
    Add-Result "Safety" "Prototype manual boundary is explicit" "PASS" "Phase 5D manual-script-only marker is present and blocked paths keep externalConnectionAttempted=false."
} else {
    Add-Result "Safety" "Prototype manual boundary is explicit" "FAIL" "Could not find expected manual-boundary markers."
}

$forbiddenOrder = @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder", "SendOrder")
$orderHits = @()
foreach ($word in $forbiddenOrder) {
    $orderHits += @(Select-String -Path $prototypeFile,$scriptFile -Pattern $word -SimpleMatch -ErrorAction SilentlyContinue)
}
if ($orderHits.Count -eq 0) {
    Add-Result "Safety" "No order surface" "PASS" "No order-submission words found in Phase 5B prototype files."
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

$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerFiles = @(Get-ChildItem -Path (Join-Path $repoRoot "src") -Recurse -Include "*.cs" | Where-Object { $_.FullName -like "*Worker*" })
$registrationHits = @(Select-String -Path $apiProgram -Pattern "LmaxReadOnlySocketPrototype|LmaxReadOnlySocketPrototypeTransport" -SimpleMatch -ErrorAction SilentlyContinue)
foreach ($file in $workerFiles) {
    $registrationHits += @(Select-String -Path $file.FullName -Pattern "LmaxReadOnlySocketPrototype|LmaxReadOnlySocketPrototypeTransport" -SimpleMatch -ErrorAction SilentlyContinue)
}
if ($registrationHits.Count -eq 0) {
    Add-Result "Registration" "No API/Worker prototype registration" "PASS" "Prototype is not registered into API/Worker DI or hosted services."
} else {
    Add-Result "Registration" "No API/Worker prototype registration" "FAIL" (($registrationHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

if (Test-FileContains $apiProgram "FakeLmaxGateway") {
    Add-Result "Registration" "API remains FakeLmaxGateway" "PASS" "FakeLmaxGateway registration remains present."
} else {
    Add-Result "Registration" "API remains FakeLmaxGateway" "FAIL" "Could not confirm FakeLmaxGateway in API Program.cs."
}

if ($AllowExternalPrototypeRun) {
    Add-Result "Manual run" "External prototype run" "WARN" "External run flag was provided, but Phase 5B script still fails closed pending credential resolver hardening."
} else {
    Add-Result "Manual run" "External prototype run" "PASS" "No external prototype run attempted by default."
}

$failures = @($results | Where-Object { $_.status -eq "FAIL" })
$warnings = @($results | Where-Object { $_.status -eq "WARN" })
$decision = if ($failures.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS WITH KNOWN WARNINGS" } else { "PASS" }

$reportDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $reportDir "lmax-readonly-phase5b-prototype-gate-$stamp.json"
$report = [ordered]@{
    gate = "LMAX Read-Only Runtime Phase 5B Prototype Gate"
    finalDecision = $decision
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    checks = @($results)
}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $reportPath"

if ($decision -eq "FAIL") { exit 1 }
