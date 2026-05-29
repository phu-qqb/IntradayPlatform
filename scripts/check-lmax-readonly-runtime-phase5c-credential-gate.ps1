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

Write-Host "LMAX Read-Only Runtime Phase 5C Credential Gate"
Write-Host "Local-only. No LMAX connection is attempted by this gate; after Phase 5D the isolated prototype may contain manual-only socket code."

$credentialFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyCredentialProfile.cs"
$prototypeFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlySocketPrototype.cs"
$credentialScript = Join-Path $repoRoot "scripts/check-lmax-readonly-runtime-demo-credentials.ps1"
$prototypeScript = Join-Path $repoRoot "scripts/run-lmax-readonly-runtime-demo-snapshot-prototype.ps1"

if ((Test-Path $credentialFile) -and (Test-Path $credentialScript)) {
    Add-Result "Files" "Credential resolver files exist" "PASS" "Credential availability resolver and check script are present."
} else {
    Add-Result "Files" "Credential resolver files exist" "FAIL" "Missing credential resolver file or script."
}

$credentialText = Get-Content -LiteralPath $credentialFile -Raw
foreach ($required in @("LmaxReadOnlyCredentialProfileResolverEnvironment", "LmaxReadOnlyCredentialAvailabilityResult", "LmaxReadOnlyCredentialRedactionPolicy", "CredentialValuesReturned")) {
    if ($credentialText.IndexOf($required, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        Add-Result "Source" "Required credential redaction surface" "FAIL" "Missing $required."
    }
}
if (-not (@($results | Where-Object { $_.check -eq "Required credential redaction surface" -and $_.status -eq "FAIL" }).Count)) {
    Add-Result "Source" "Required credential redaction surface" "PASS" "Availability result and redaction policy are present."
}

$forbidden = @("TcpClient", "Socket(", "SslStream", "QuickFIX", "ClientWebSocket", "NetworkStream", "NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder", "Lmax.ConnectivityLab")
$hits = @()
foreach ($word in $forbidden) {
    $hits += @(Select-String -Path $credentialFile,$credentialScript -Pattern $word -SimpleMatch -ErrorAction SilentlyContinue)
}
if ($hits.Count -eq 0) {
    Add-Result "Safety" "No socket/order/lab surface in credential boundary" "PASS" "Phase 5C credential files contain no forbidden socket/order/lab implementation surface."
} else {
    Add-Result "Safety" "No socket/order/lab surface in credential boundary" "FAIL" (($hits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$orderHits = @()
foreach ($word in @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder")) {
    $orderHits += @(Select-String -Path $prototypeFile,$prototypeScript -Pattern $word -SimpleMatch -ErrorAction SilentlyContinue)
}
if ($orderHits.Count -eq 0) {
    Add-Result "Safety" "No order surface in prototype" "PASS" "Prototype files contain no order-submission surface."
} else {
    Add-Result "Safety" "No order surface in prototype" "FAIL" (($orderHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"
$apiText = Get-Content -LiteralPath $apiProgram -Raw
$workerText = Get-Content -LiteralPath $workerProgram -Raw
if ($apiText -match "AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>" -and $workerText -match "AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>" -and $apiText -notmatch "LmaxReadOnlySocketPrototypeTransport" -and $workerText -notmatch "LmaxReadOnlySocketPrototypeTransport") {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No prototype or real LMAX gateway registration found."
} else {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" "FakeLmaxGateway registration missing or prototype registration found."
}

try {
    & powershell -NoProfile -ExecutionPolicy Bypass -File $credentialScript *> $null
    Add-Result "Script" "Credential check requires confirmation" "FAIL" "Credential check ran without explicit confirmation."
} catch {
    Add-Result "Script" "Credential check requires confirmation" "PASS" "Credential check refuses without -ConfirmCredentialAvailabilityCheck."
}

if ($CheckCredentialAvailability.IsPresent) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File $credentialScript -ConfirmCredentialAvailabilityCheck
    if ($LASTEXITCODE -eq 0) {
        Add-Result "Script" "Credential availability checked" "PASS" "Credential labels are present; output is redacted."
    } else {
        Add-Result "Script" "Credential availability checked" "WARN" "Credential labels are missing; output lists labels only."
    }
} else {
    Add-Result "Script" "Credential availability checked" "PASS" "Skipped by default; no credential availability read was requested."
}

$failures = @($results | Where-Object { $_.status -eq "FAIL" })
$warnings = @($results | Where-Object { $_.status -eq "WARN" })
$decision = if ($failures.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS WITH KNOWN WARNINGS" } else { "PASS" }

$reportDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Force -Path $reportDir | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $reportDir "lmax-readonly-phase5c-credential-gate-$stamp.json"
[ordered]@{
    gate = "LMAX Read-Only Runtime Phase 5C Credential Gate"
    finalDecision = $decision
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    checks = @($results)
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $reportPath"

if ($decision -eq "FAIL") { exit 1 }
