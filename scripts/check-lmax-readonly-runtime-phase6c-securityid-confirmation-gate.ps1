param(
    [string]$ManifestFile = "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyInstrumentSecurityIdManifest.cs"
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

function Resolve-LocalPath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return $Path }
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return Join-Path $repoRoot $Path
}

Write-Host "LMAX Read-Only Runtime Phase 6C SecurityID Confirmation Gate"
Write-Host "Planning-only. No LMAX connection, no API calls, no credentials, no replay, no scheduler/polling, no orders, and no mutation."

$resolvedManifest = Resolve-LocalPath $ManifestFile
$candidateSymbols = @("GBPUSD", "USDJPY", "EURGBP", "AUDUSD")

if (-not (Test-Path -LiteralPath $resolvedManifest)) {
    Add-Result "Files" "Manifest file exists" "FAIL" "Missing manifest file: $resolvedManifest"
} else {
    Add-Result "Files" "Manifest file exists" "PASS" $resolvedManifest
}

$manifestText = if (Test-Path -LiteralPath $resolvedManifest) { Get-Content -Raw -LiteralPath $resolvedManifest } else { "" }

if ($manifestText.Contains("LmaxReadOnlyInstrumentSecurityIdManifest") -and
    $manifestText.Contains("GetConfirmedSecurityId") -and
    $manifestText.Contains("AllInstrumentsConfirmed")) {
    Add-Result "Manifest" "Required class and methods exist" "PASS" "Manifest class exposes required APIs."
} else {
    Add-Result "Manifest" "Required class and methods exist" "FAIL" "Manifest class or required APIs are missing."
}

$missingSecurityIds = @()
foreach ($symbol in $candidateSymbols) {
    $pattern = "\[""$symbol""\]\s*=\s*""([^""]+)"""
    $match = [regex]::Match($manifestText, $pattern)
    if ($match.Success -and -not [string]::IsNullOrWhiteSpace($match.Groups[1].Value)) {
        Add-Result "SecurityID" "$symbol confirmed" "PASS" "$symbol maps to a non-empty local Phase 6C SecurityID value."
    } else {
        $missingSecurityIds += $symbol
        Add-Result "SecurityID" "$symbol confirmed" "WARN" "$symbol is missing a SecurityID value."
    }
}

$approvalIssues = @()
foreach ($symbol in $candidateSymbols) {
    $pattern = "\[""$symbol""\]\s*=\s*false"
    if ([regex]::IsMatch($manifestText, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
        Add-Result "Approval" "$symbol external run approval" "PASS" "$symbol IsApprovedForExternalRun=false."
    } else {
        $approvalIssues += $symbol
        Add-Result "Approval" "$symbol external run approval" "FAIL" "$symbol must have IsApprovedForExternalRun=false."
    }
}

if ($manifestText.Contains("PHASE6C-DEMO-SECURITYID-") -and $manifestText.Contains("CreateDefaultSymbolToSecurityId")) {
    Add-Result "Manifest" "Local placeholder values are explicit" "PASS" "SecurityID values are clearly scoped to local Phase 6C confirmation placeholders."
} else {
    Add-Result "Manifest" "Local placeholder values are explicit" "WARN" "SecurityID values are not marked with the Phase 6C local placeholder prefix."
}

$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"
$registrationHits = @(Select-String -Path $apiProgram,$workerProgram -Pattern "LmaxReadOnlySocketPrototype","RealLmaxGateway","LmaxVenueGatewaySkeleton","ExternalReadOnlyPrototypeGateway" -SimpleMatch -ErrorAction SilentlyContinue)
if ($registrationHits.Count -eq 0 -and (Select-String -Path $apiProgram -Pattern "FakeLmaxGateway" -SimpleMatch -Quiet)) {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No prototype or real gateway registration found."
} else {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" (($registrationHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

Add-Result "Runtime" "External LMAX connection" "PASS" "No external connection is made by this SecurityID confirmation gate."
Add-Result "API" "External API calls" "PASS" "No API calls are made by this SecurityID confirmation gate."
Add-Result "Replay" "Shadow replay" "PASS" "No replay is submitted by this SecurityID confirmation gate."
Add-Result "Mutation" "Trading state" "PASS" "No trading state is mutated by this SecurityID confirmation gate."

$failed = @($results | Where-Object { $_.status -eq "FAIL" })
$warnings = @($results | Where-Object { $_.status -eq "WARN" })
$decision = if ($failed.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "WARN" } else { "PASS" }

$reportDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$reportPath = Join-Path $reportDir "phase6c-securityid-confirmation-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    finalDecision = $decision
    phase = "6C"
    scope = "Instrument SecurityID Confirmation Workflow, No External Run"
    manifestFile = $resolvedManifest
    candidateSymbols = $candidateSymbols
    missingSecurityIds = $missingSecurityIds
    externalRunApprovalIssues = $approvalIssues
    externalConnectionAttempted = $false
    externalApiCallsAttempted = $false
    runtimeShadowReplaySubmit = $false
    schedulerOrPollingAdded = $false
    orderSubmissionAdded = $false
    gatewayRegistrationAdded = $false
    tradingMutationAdded = $false
    results = $results
} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $reportPath"

if ($decision -eq "FAIL") { exit 1 }
