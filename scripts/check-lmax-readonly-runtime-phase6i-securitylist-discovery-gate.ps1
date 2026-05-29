param(
    [string]$DiscoveryArtifactFile = ""
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

function Get-TextHit([string[]]$Path, [string[]]$Pattern) {
    $existing = @($Path | Where-Object { Test-Path -LiteralPath $_ })
    if ($existing.Count -eq 0) { return @() }
    return @(Select-String -Path $existing -Pattern $Pattern -SimpleMatch -ErrorAction SilentlyContinue)
}

Write-Host "LMAX Read-Only Runtime Phase 6I SecurityList Discovery Gate"
Write-Host "Local-only gate. It does not connect to LMAX, does not run SecurityListRequest, does not run snapshots, and does not replay."

$modelFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlySecurityListDiscovery.cs"
$manualScript = Join-Path $PSScriptRoot "run-lmax-readonly-runtime-demo-securitylist-discovery.ps1"
$testFile = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlySecurityListDiscoveryTests.cs"

foreach ($item in @(
    @{ Name = "SecurityList discovery model"; Path = $modelFile },
    @{ Name = "Manual SecurityList discovery script"; Path = $manualScript },
    @{ Name = "SecurityList discovery tests"; Path = $testFile }
)) {
    if (Test-Path -LiteralPath $item.Path) {
        Add-Result "Files" "$($item.Name) exists" "PASS" $item.Path
    } else {
        Add-Result "Files" "$($item.Name) exists" "FAIL" "Missing: $($item.Path)"
    }
}

$scriptText = if (Test-Path -LiteralPath $manualScript) { Get-Content -Raw -LiteralPath $manualScript } else { "" }
foreach ($marker in @("AllowExternalConnections", "ConfirmDemoReadOnly", "SecurityListRequest", "No market-data snapshots", "orderSubmissionAttempted = `$false", "shadowReplaySubmitAttempted = `$false", "tradingMutationAttempted = `$false", "schedulerStarted = `$false")) {
    if ($scriptText.Contains($marker)) {
        Add-Result "ManualScript" "Marker $marker" "PASS" "Manual safety marker found."
    } else {
        Add-Result "ManualScript" "Marker $marker" "FAIL" "Manual safety marker missing."
    }
}

$modelText = if (Test-Path -LiteralPath $modelFile) { Get-Content -Raw -LiteralPath $modelFile } else { "" }
foreach ($marker in @("LmaxReadOnlySecurityListDiscovery", "LmaxReadOnlySecurityListDiscoveryRedactor", "FailedSafeSecurityListRequestRejected", "FailedSafeBusinessReject", "FailedSafeSessionReject", "FailedSafeSecurityListTimeout", "IsApprovedForExternalRun: false")) {
    if ($modelText.Contains($marker)) {
        Add-Result "Model" "Marker $marker" "PASS" "Model marker found."
    } else {
        Add-Result "Model" "Marker $marker" "FAIL" "Model marker missing."
    }
}

$testText = if (Test-Path -LiteralPath $testFile) { Get-Content -Raw -LiteralPath $testFile } else { "" }
foreach ($marker in @("Parse_security_list_matches_all_candidates", "Conflicting_security_ids_are_detected", "Redactor_removes_sentinel_credential_values", "Artifact_contains_no_credential_values", "Api_and_worker_remain_fake_gateway_only")) {
    if ($testText.Contains($marker)) {
        Add-Result "Tests" "Test marker $marker" "PASS" "Unit test coverage marker found."
    } else {
        Add-Result "Tests" "Test marker $marker" "FAIL" "Unit test coverage marker missing."
    }
}

$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"
$apiWorker = @($apiProgram, $workerProgram)
$registrationHits = Get-TextHit $apiWorker @("RealLmaxGateway", "ExternalReadOnlyPrototypeGateway", "LmaxVenueGatewaySkeleton", "SecurityListRequest")
if ($registrationHits.Count -eq 0 -and (Select-String -Path $apiProgram -Pattern "FakeLmaxGateway" -SimpleMatch -Quiet)) {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No SecurityList or real gateway registration found."
} else {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" (($registrationHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$schedulerHits = Get-TextHit $apiWorker @("PeriodicTimer", "System.Threading.Timer", "SecurityListPoll")
if ($schedulerHits.Count -eq 0) {
    Add-Result "Scheduler" "No scheduler/polling added" "PASS" "No scheduler/polling marker found in API/Worker startup."
} else {
    Add-Result "Scheduler" "No scheduler/polling added" "FAIL" (($schedulerHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$replayHits = Get-TextHit $apiWorker @("SubmitToShadowReplay = true", "SubmittedToShadowReplay = true", "ReplaySubmitAsync")
if ($replayHits.Count -eq 0) {
    Add-Result "Replay" "Runtime does not submit to shadow replay" "PASS" "No runtime replay submit marker found in API/Worker startup."
} else {
    Add-Result "Replay" "Runtime does not submit to shadow replay" "FAIL" (($replayHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$orderHits = Get-TextHit $apiWorker @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder")
if ($orderHits.Count -eq 0) {
    Add-Result "Orders" "No order surface" "PASS" "No order marker found in API/Worker startup."
} else {
    Add-Result "Orders" "No order surface" "FAIL" (($orderHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$mutationHits = Get-TextHit $apiWorker @("PersistTrade", "TradingState")
if ($mutationHits.Count -eq 0) {
    Add-Result "Mutation" "No trading-state mutation references" "PASS" "No trading mutation marker found in API/Worker startup."
} else {
    Add-Result "Mutation" "No trading-state mutation references" "FAIL" (($mutationHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$artifactSummary = $null
if ([string]::IsNullOrWhiteSpace($DiscoveryArtifactFile)) {
    Add-Result "Artifact" "Discovery artifact supplied" "WARN" "No discovery artifact supplied yet; expected before manual operator discovery."
} else {
    $artifactPath = Resolve-LocalPath $DiscoveryArtifactFile
    if (-not (Test-Path -LiteralPath $artifactPath)) {
        Add-Result "Artifact" "Discovery artifact exists" "FAIL" "Missing: $artifactPath"
    } else {
        $artifactText = Get-Content -Raw -LiteralPath $artifactPath
        $artifact = $artifactText | ConvertFrom-Json
        $candidateCount = @($artifact.candidateMatches).Count
        $unmatchedCount = @($artifact.unmatchedCandidates).Count
        $unsafe = $false
        if ($artifactText -match '(?i)(password|secret|token|apikey|privatekey|bearer|\b553=|\b554=|host=|user=|account)') { $unsafe = $true }
        if ([bool]$artifact.credentialValuesReturned -or [bool]$artifact.orderSubmissionAttempted -or [bool]$artifact.shadowReplaySubmitAttempted -or [bool]$artifact.tradingMutationAttempted -or [bool]$artifact.schedulerStarted -or [bool]$artifact.isApprovedForExternalRun) { $unsafe = $true }
        if ($unsafe) {
            Add-Result "Artifact" "Discovery artifact safe content" "FAIL" "Artifact contains unsafe content or unsafe flag."
        } elseif ($candidateCount -eq 4 -and $unmatchedCount -eq 0) {
            Add-Result "Artifact" "Discovery artifact candidate matches" "PASS" "All four candidates matched."
        } else {
            Add-Result "Artifact" "Discovery artifact candidate matches" "WARN" "CandidateMatches=$candidateCount; Unmatched=$unmatchedCount."
        }
        $artifactSummary = [ordered]@{
            path = $artifactPath
            status = [string]$artifact.status
            totalInstrumentCount = [int]$artifact.totalInstrumentCount
            candidateMatchCount = $candidateCount
            unmatchedCandidateCount = $unmatchedCount
        }
    }
}

Add-Result "Runtime" "External LMAX connection" "PASS" "This gate does not connect to LMAX."
Add-Result "Snapshot" "MarketData snapshot" "PASS" "This gate does not request market data snapshots."
Add-Result "Replay" "Manual replay" "PASS" "This gate does not replay evidence."
Add-Result "Credentials" "Credential values" "PASS" "This gate does not require or print credential values."

$failed = @($results | Where-Object { $_.status -eq "FAIL" })
$warnings = @($results | Where-Object { $_.status -eq "WARN" })
$decision = if ($failed.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS_WITH_KNOWN_WARNINGS" } else { "PASS" }

$reportDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$reportPath = Join-Path $reportDir "phase6i-securitylist-discovery-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    finalDecision = $decision
    phase = "6I"
    scope = "Manual FIX SecurityListRequest Discovery, Demo-only / Read-only / No External Run Approval"
    discoveryArtifact = $artifactSummary
    isApprovedForExternalRun = $false
    externalConnectionAttempted = $false
    securityListRequestAttemptedByGate = $false
    marketDataSnapshotAttempted = $false
    replayAttempted = $false
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
