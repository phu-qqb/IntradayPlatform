param(
    [string]$ArtifactFile = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()

function Add-Result([string]$Category, [string]$Check, [string]$Status, [string]$Detail) {
    $script:results += [ordered]@{ category = $Category; check = $Check; status = $Status; detail = $Detail }
    Write-Host ("{0}: {1} / {2} - {3}" -f $Status,$Category,$Check,$Detail)
}

function Resolve-LocalPath([string]$PathValue) {
    if ([string]::IsNullOrWhiteSpace($PathValue)) { return $PathValue }
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}

function Get-Hits($Paths, $Patterns) {
    $existing = @($Paths | Where-Object { Test-Path -LiteralPath $_ })
    if ($existing.Count -eq 0) { return @() }
    return @(Select-String -Path $existing -Pattern $Patterns -SimpleMatch -ErrorAction SilentlyContinue)
}

Write-Host "LMAX Read-Only Runtime Phase 6X GBPUSD Result Review Gate"
Write-Host "Local-only. This gate does not connect to LMAX, request snapshots, replay, or use credentials."

foreach ($required in @(
    @{ name = "GBPUSD result model"; path = "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyGbpusdManualSnapshotResult.cs" },
    @{ name = "GBPUSD result review script"; path = "scripts/review-lmax-readonly-gbpusd-snapshot-result.ps1" },
    @{ name = "Phase 6X tests"; path = "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlyGbpusdManualSnapshotResultTests.cs" }
)) {
    $path = Join-Path $repoRoot $required.path
    if (Test-Path -LiteralPath $path) {
        Add-Result "Files" "$($required.name) exists" "PASS" $path
    } else {
        Add-Result "Files" "$($required.name) exists" "FAIL" "Missing $path"
    }
}

if ([string]::IsNullOrWhiteSpace($ArtifactFile)) {
    Add-Result "Artifact" "GBPUSD result artifact supplied" "WARN" "No artifact supplied; source-only gate mode."
} else {
    $artifactPath = Resolve-LocalPath $ArtifactFile
    if (-not (Test-Path -LiteralPath $artifactPath)) {
        Add-Result "Artifact" "GBPUSD result artifact exists" "FAIL" "Missing $artifactPath"
    } else {
        $reviewScript = Join-Path $PSScriptRoot "review-lmax-readonly-gbpusd-snapshot-result.ps1"
        & $reviewScript -ArtifactFile $artifactPath
        $reviewReport = Join-Path $repoRoot "artifacts/readiness/phase6x-gbpusd-snapshot-result-review.json"
        $review = Get-Content -LiteralPath $reviewReport -Raw | ConvertFrom-Json
        if ($review.finalDecision -in @("PASS", "PASS_WITH_KNOWN_WARNINGS")) {
            Add-Result "Artifact" "GBPUSD result review decision" "PASS" "Review decision $($review.finalDecision)."
        } else {
            Add-Result "Artifact" "GBPUSD result review decision" "FAIL" "Review decision $($review.finalDecision)."
        }

        if ($review.status -eq "CompletedWithEmptyBook" -and $review.finalDecision -eq "PASS_WITH_KNOWN_WARNINGS") {
            Add-Result "Artifact" "CompletedWithEmptyBook accepted as warning" "WARN" "Empty-book snapshot is safe completed-with-warning."
        } elseif ($review.status -eq "CompletedWithEmptyBook") {
            Add-Result "Artifact" "CompletedWithEmptyBook accepted as warning" "FAIL" "Empty-book artifact was not classified as PASS_WITH_KNOWN_WARNINGS."
        }

        if (-not [bool]$review.orderSubmissionAttempted -and -not [bool]$review.shadowReplaySubmitAttempted -and -not [bool]$review.tradingMutationAttempted -and -not [bool]$review.schedulerStarted -and -not [bool]$review.credentialValuesReturned -and [bool]$review.noSensitiveContent -and $review.redactionStatus -eq "Redacted") {
            Add-Result "Artifact" "Unsafe flags remain false" "PASS" "No order, shadow replay submit, trading mutation, scheduler, credential leakage, or sensitive content."
        } else {
            Add-Result "Artifact" "Unsafe flags remain false" "FAIL" "Unsafe flag or redaction issue detected."
        }
    }
}

$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"
$startupFiles = @($apiProgram, $workerProgram)

$gatewayHits = Get-Hits $startupFiles @("RealLmaxGateway", "ExternalReadOnlyPrototypeGateway", "LmaxVenueGatewaySkeleton", "SecurityListRequest")
if ($gatewayHits.Count -eq 0 -and (Select-String -Path $apiProgram -Pattern "FakeLmaxGateway" -SimpleMatch -Quiet)) {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No real gateway registration found."
} else {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" (($gatewayHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

foreach ($scan in @(
    @{ category = "Scheduler"; check = "No scheduler/polling added"; patterns = @("PeriodicTimer", "System.Threading.Timer", "SecurityListPoll") },
    @{ category = "Replay"; check = "Runtime still does not submit to shadow replay"; patterns = @("SubmitToShadowReplay = true", "SubmittedToShadowReplay = true", "ReplaySubmitAsync") },
    @{ category = "Orders"; check = "No order surface"; patterns = @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder") },
    @{ category = "Mutation"; check = "No trading-state mutation references"; patterns = @("PersistTrade", "TradingState") }
)) {
    $hits = Get-Hits $startupFiles $scan.patterns
    if ($hits.Count -eq 0) {
        Add-Result $scan.category $scan.check "PASS" "No marker found in API/Worker startup."
    } else {
        Add-Result $scan.category $scan.check "FAIL" (($hits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
    }
}

Add-Result "Runtime" "External LMAX connection" "PASS" "This gate does not connect to LMAX."
Add-Result "Snapshot" "MarketData snapshot" "PASS" "This gate does not request snapshots."
Add-Result "Replay" "Manual replay" "PASS" "This gate does not replay evidence."
Add-Result "Credentials" "Credential values" "PASS" "This gate does not require or print credential values."

$failed = @($results | Where-Object status -eq "FAIL")
$warnings = @($results | Where-Object status -eq "WARN")
$decision = if ($failed.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS_WITH_KNOWN_WARNINGS" } else { "PASS" }

$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase6x-gbpusd-result-review-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "6X"
    finalDecision = $decision
    externalConnectionAttempted = $false
    snapshotAttempted = $false
    replayAttempted = $false
    orderSubmissionAttempted = $false
    shadowReplaySubmitAttempted = $false
    tradingMutationAttempted = $false
    schedulerStarted = $false
    results = $results
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }
