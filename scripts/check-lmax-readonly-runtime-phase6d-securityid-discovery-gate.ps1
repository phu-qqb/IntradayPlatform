param(
    [string]$ManifestFile = "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyInstrumentSecurityIdDiscoveryManifest.cs"
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

Write-Host "LMAX Read-Only Runtime Phase 6D SecurityID Discovery Gate"
Write-Host "Planning-only. No LMAX connection, no API calls, no credentials, no replay, no scheduler/polling, no orders, and no mutation."

$resolvedManifest = Resolve-LocalPath $ManifestFile
$candidateSymbols = @("GBPUSD", "USDJPY", "EURGBP", "AUDUSD")

if (-not (Test-Path -LiteralPath $resolvedManifest)) {
    Add-Result "Files" "Discovery manifest exists" "FAIL" "Missing manifest file: $resolvedManifest"
} else {
    Add-Result "Files" "Discovery manifest exists" "PASS" $resolvedManifest
}

$manifestText = if (Test-Path -LiteralPath $resolvedManifest) { Get-Content -Raw -LiteralPath $resolvedManifest } else { "" }

if ($manifestText.Contains("LmaxReadOnlyInstrumentSecurityIdDiscoveryManifest") -and
    $manifestText.Contains("LmaxReadOnlyInstrumentSecurityIdDiscoveryManifestValidator")) {
    Add-Result "Manifest" "Discovery manifest and validator exist" "PASS" "Manifest and validator types are present."
} else {
    Add-Result "Manifest" "Discovery manifest and validator exist" "FAIL" "Manifest or validator type is missing."
}

$missingSecurityIds = @()
$approvalBlockedByManifest = $manifestText.Contains("IsApprovedForExternalRun: false") -and
    -not $manifestText.Contains("IsApprovedForExternalRun: true")
foreach ($symbol in $candidateSymbols) {
    if ($manifestText.Contains("PHASE6D-DISCOVERY-PENDING-$symbol")) {
        Add-Result "SecurityID" "$symbol candidate value exists" "PASS" "$symbol has a Phase 6D local placeholder candidate SecurityID."
    } else {
        $missingSecurityIds += $symbol
        Add-Result "SecurityID" "$symbol candidate value exists" "WARN" "$symbol is missing a Phase 6D candidate SecurityID."
    }

    if ($approvalBlockedByManifest) {
        Add-Result "Approval" "$symbol external run approval" "PASS" "$symbol keeps IsApprovedForExternalRun=false."
    } else {
        Add-Result "Approval" "$symbol external run approval" "FAIL" "$symbol must keep IsApprovedForExternalRun=false."
    }
}

$requiredFalseMarkers = @(
    "ExternalConnectionAttempted: false",
    "ExternalApiCallAttempted: false",
    "SchedulerOrPollingAdded: false",
    "RuntimeShadowReplaySubmit: false",
    "OrderSubmissionAdded: false",
    "GatewayRegistrationAdded: false",
    "TradingMutationAdded: false"
)
foreach ($marker in $requiredFalseMarkers) {
    if ($manifestText.Contains($marker)) {
        Add-Result "Safety" $marker "PASS" "Safety marker present."
    } else {
        Add-Result "Safety" $marker "FAIL" "Missing safety marker."
    }
}

$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"
$registrationHits = @(Select-String -Path $apiProgram,$workerProgram -Pattern "LmaxReadOnlySocketPrototype","RealLmaxGateway","LmaxVenueGatewaySkeleton","ExternalReadOnlyPrototypeGateway" -SimpleMatch -ErrorAction SilentlyContinue)
if ($registrationHits.Count -eq 0 -and (Select-String -Path $apiProgram -Pattern "FakeLmaxGateway" -SimpleMatch -Quiet)) {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No prototype or real gateway registration found."
} else {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" (($registrationHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

Add-Result "Runtime" "External LMAX connection" "PASS" "No external connection is made by this SecurityID discovery gate."
Add-Result "API" "External API calls" "PASS" "No API calls are made by this SecurityID discovery gate."
Add-Result "Replay" "Shadow replay" "PASS" "No replay is submitted by this SecurityID discovery gate."
Add-Result "Mutation" "Trading state" "PASS" "No trading state is mutated by this SecurityID discovery gate."

$failed = @($results | Where-Object { $_.status -eq "FAIL" })
$warnings = @($results | Where-Object { $_.status -eq "WARN" })
$decision = if ($failed.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "WARN" } else { "PASS" }

$reportDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$reportPath = Join-Path $reportDir "phase6d-securityid-discovery-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    finalDecision = $decision
    phase = "6D"
    scope = "SecurityID Discovery Planning, No External Run"
    manifestFile = $resolvedManifest
    candidateSymbols = $candidateSymbols
    missingSecurityIds = $missingSecurityIds
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
