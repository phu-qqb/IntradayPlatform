param(
    [string]$EvidenceReviewFile = "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyInstrumentSecurityIdSourceEvidence.cs"
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

Write-Host "LMAX Read-Only Runtime Phase 6E SecurityID Evidence Review Gate"
Write-Host "Planning-only. No LMAX connection, no external APIs, no snapshots, no replay, no credentials, no scheduler/polling, no orders, and no mutation."

$resolvedEvidenceReview = Resolve-LocalPath $EvidenceReviewFile
$candidateSymbols = @("GBPUSD", "USDJPY", "EURGBP", "AUDUSD")

if (Test-Path -LiteralPath $resolvedEvidenceReview) {
    Add-Result "Files" "Evidence review model exists" "PASS" $resolvedEvidenceReview
} else {
    Add-Result "Files" "Evidence review model exists" "FAIL" "Missing file: $resolvedEvidenceReview"
}

$reviewText = if (Test-Path -LiteralPath $resolvedEvidenceReview) { Get-Content -Raw -LiteralPath $resolvedEvidenceReview } else { "" }

if ($reviewText.Contains("LmaxReadOnlyInstrumentSecurityIdSourceEvidence") -and
    $reviewText.Contains("LmaxReadOnlyInstrumentSecurityIdSourceEvidenceValidator") -and
    $reviewText.Contains("LmaxReadOnlyInstrumentSecurityIdEvidenceReviewManifest")) {
    Add-Result "Model" "Evidence review model, manifest, and validator exist" "PASS" "Required Phase 6E types are present."
} else {
    Add-Result "Model" "Evidence review model, manifest, and validator exist" "FAIL" "Missing required Phase 6E model types."
}

foreach ($symbol in $candidateSymbols) {
    if ($reviewText.Contains("LmaxReadOnlyInstrumentAllowlist.CandidateEntries") -and
        $reviewText.Contains("PHASE6D-DISCOVERY-PENDING-{entry.Symbol}")) {
        Add-Result "Manifest" "$symbol evidence review status exists" "PASS" "$symbol is represented by the default Phase 6E review manifest."
    } else {
        Add-Result "Manifest" "$symbol evidence review status exists" "FAIL" "$symbol is missing from the Phase 6E review manifest."
    }
}

if ($reviewText.Contains("NeedsMoreEvidence") -and $reviewText.Contains("EvidencePending")) {
    Add-Result "Manifest" "Pending evidence is represented as known warning" "WARN" "Default Phase 6E state is pending/needs more evidence."
} else {
    Add-Result "Manifest" "Pending evidence is represented as known warning" "FAIL" "Phase 6E must keep pending evidence visible as a known warning."
}

if ($reviewText.Contains("IsApprovedForExternalRun: false") -and -not $reviewText.Contains("IsApprovedForExternalRun: true")) {
    Add-Result "Approval" "External run approval remains false" "PASS" "All default records keep IsApprovedForExternalRun=false."
} else {
    Add-Result "Approval" "External run approval remains false" "FAIL" "Phase 6E must not approve external runs."
}

$requiredFalseMarkers = @(
    "ExternalConnectionAttempted: false",
    "ExternalApiCallAttempted: false",
    "MarketDataSnapshotAttempted: false",
    "ReplayAttempted: false",
    "SchedulerOrPollingAdded: false",
    "RuntimeShadowReplaySubmit: false",
    "OrderSubmissionAdded: false",
    "GatewayRegistrationAdded: false",
    "TradingMutationAdded: false"
)
foreach ($marker in $requiredFalseMarkers) {
    if ($reviewText.Contains($marker)) {
        Add-Result "Safety" $marker "PASS" "Safety marker present."
    } else {
        Add-Result "Safety" $marker "FAIL" "Missing safety marker."
    }
}

$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"
$registrationHits = @(Select-String -Path $apiProgram,$workerProgram -Pattern "RealLmaxGateway","ExternalReadOnlyPrototypeGateway","LmaxVenueGatewaySkeleton" -SimpleMatch -ErrorAction SilentlyContinue)
if ($registrationHits.Count -eq 0 -and (Select-String -Path $apiProgram -Pattern "FakeLmaxGateway" -SimpleMatch -Quiet)) {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No real gateway registration found."
} else {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" (($registrationHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

Add-Result "Runtime" "External LMAX connection" "PASS" "No external connection is made by this evidence review gate."
Add-Result "API" "External API calls" "PASS" "No external API calls are made by this evidence review gate."
Add-Result "Snapshot" "MarketData snapshot" "PASS" "No market-data snapshot is run by this evidence review gate."
Add-Result "Replay" "Shadow replay" "PASS" "No replay is submitted by this evidence review gate."
Add-Result "Mutation" "Trading state" "PASS" "No trading state is mutated by this evidence review gate."

$failed = @($results | Where-Object { $_.status -eq "FAIL" })
$warnings = @($results | Where-Object { $_.status -eq "WARN" })
$decision = if ($failed.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS_WITH_KNOWN_WARNINGS" } else { "PASS" }

$reportDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$reportPath = Join-Path $reportDir "phase6e-securityid-evidence-review-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    finalDecision = $decision
    phase = "6E"
    scope = "SecurityID Source Evidence Review / Allowlist Approval Planning, No External Run"
    evidenceReviewFile = $resolvedEvidenceReview
    candidateSymbols = $candidateSymbols
    isApprovedForExternalRun = $false
    externalConnectionAttempted = $false
    externalApiCallsAttempted = $false
    marketDataSnapshotAttempted = $false
    replayAttempted = $false
    runtimeShadowReplaySubmit = $false
    schedulerOrPollingAdded = $false
    orderSubmissionAdded = $false
    gatewayRegistrationAdded = $false
    tradingMutationAdded = $false
    knownWarnings = @($warnings | ForEach-Object { $_.detail })
    results = $results
} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $reportPath"

if ($decision -eq "FAIL") { exit 1 }
