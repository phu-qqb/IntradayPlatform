param(
    [string]$DiscoveryArtifactFile = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()

function Add-Result([string]$Category, [string]$Check, [string]$Status, [string]$Detail) {
    $script:results += [ordered]@{ category = $Category; check = $Check; status = $Status; detail = $Detail }
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

function Get-JsonCollectionCount($Value) {
    if ($null -eq $Value) { return 0 }
    if ($Value -is [array]) { return $Value.Count }
    if (@($Value.PSObject.Properties | Where-Object { $_.MemberType -eq "NoteProperty" }).Count -eq 0) { return 0 }
    return 1
}

Write-Host "LMAX Read-Only Runtime Phase 6J SecurityList Diagnostics Gate"
Write-Host "Local-only gate. It validates diagnostics/profile compatibility and does not connect to LMAX."

$modelFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlySecurityListDiscovery.cs"
$manualScript = Join-Path $PSScriptRoot "run-lmax-readonly-runtime-demo-securitylist-discovery.ps1"
$testFile = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlySecurityListDiscoveryTests.cs"

foreach ($item in @(
    @{ Name = "SecurityList diagnostics model"; Path = $modelFile },
    @{ Name = "Manual SecurityList discovery script"; Path = $manualScript },
    @{ Name = "SecurityList diagnostics tests"; Path = $testFile }
)) {
    if (Test-Path -LiteralPath $item.Path) { Add-Result "Files" "$($item.Name) exists" "PASS" $item.Path }
    else { Add-Result "Files" "$($item.Name) exists" "FAIL" "Missing: $($item.Path)" }
}

$modelText = if (Test-Path -LiteralPath $modelFile) { Get-Content -Raw -LiteralPath $modelFile } else { "" }
foreach ($marker in @(
    "LmaxReadOnlySecurityListDiscoveryArtifactValidator",
    "LmaxReadOnlySecurityListFailureDiagnostics",
    "LmaxReadOnlySecurityListRequestProfileDefinition",
    "FailedSafeSecurityListUnsupportedRequestType",
    "FailedSafeSecurityListUnsupportedSecurityRequestType",
    "FailedSafeSecurityListUnsupportedSymbolFilter",
    "GetAutoSequenceProfiles",
    "IsApprovedForExternalRun: false"
)) {
    if ($modelText.Contains($marker)) { Add-Result "Model" "Marker $marker" "PASS" "Diagnostics/profile marker found." }
    else { Add-Result "Model" "Marker $marker" "FAIL" "Diagnostics/profile marker missing." }
}

$scriptText = if (Test-Path -LiteralPath $manualScript) { Get-Content -Raw -LiteralPath $manualScript } else { "" }
foreach ($marker in @("AutoSequence", "AllowKnownRejectedDiagnostics", "attempts", "requestIdHash", "No market-data snapshots", "orderSubmissionAttempted = `$false", "shadowReplaySubmitAttempted = `$false", "tradingMutationAttempted = `$false", "schedulerStarted = `$false")) {
    if ($scriptText.Contains($marker)) { Add-Result "ManualScript" "Marker $marker" "PASS" "Manual safety/profile marker found." }
    else { Add-Result "ManualScript" "Marker $marker" "FAIL" "Manual safety/profile marker missing." }
}

$artifactDiagnostics = $null
if ([string]::IsNullOrWhiteSpace($DiscoveryArtifactFile)) {
    Add-Result "Artifact" "Failed discovery artifact supplied" "WARN" "No artifact supplied; diagnostics are ready but no failure artifact was validated."
} else {
    $artifactPath = Resolve-LocalPath $DiscoveryArtifactFile
    if (-not (Test-Path -LiteralPath $artifactPath)) {
        Add-Result "Artifact" "Failed discovery artifact exists" "FAIL" "Missing: $artifactPath"
    } else {
        $artifactText = Get-Content -Raw -LiteralPath $artifactPath
        if ($artifactText -match '(?i)(password|secret|token|apikey|privatekey|bearer|\b553=|\b554=|host=|user=|account)') {
            Add-Result "Artifact" "No sensitive content" "FAIL" "Artifact contains sensitive-shaped content."
        } else {
            Add-Result "Artifact" "No sensitive content" "PASS" "No credential-shaped content found."
        }

        $artifact = $artifactText | ConvertFrom-Json
        $unsafeFlags = @()
        foreach ($flag in @("credentialValuesReturned", "orderSubmissionAttempted", "shadowReplaySubmitAttempted", "tradingMutationAttempted", "schedulerStarted", "isApprovedForExternalRun")) {
            if ([bool]$artifact.$flag) { $unsafeFlags += $flag }
        }
        if ($unsafeFlags.Count -eq 0 -and [bool]$artifact.noSensitiveContent) {
            Add-Result "Artifact" "Safety flags remain false" "PASS" "Artifact is a safe failure artifact."
        } else {
            Add-Result "Artifact" "Safety flags remain false" "FAIL" "Unsafe flags: $($unsafeFlags -join ', ')"
        }

        $artifactDiagnostics = [ordered]@{
            path = $artifactPath
            status = [string]$artifact.status
            requestProfile = [string]$artifact.requestProfile
            logonSucceeded = [bool]$artifact.logonSucceeded
            securityListRequestAttempted = [bool]$artifact.securityListRequestAttempted
            logoutSucceeded = [bool]$artifact.logoutSucceeded
            totalInstrumentCount = [int]$artifact.totalInstrumentCount
            candidateMatchCount = Get-JsonCollectionCount $artifact.candidateMatches
            unmatchedCandidateCount = Get-JsonCollectionCount $artifact.unmatchedCandidates
        }
        Add-Result "Artifact" "Failure artifact parsed" "PASS" "Status=$($artifactDiagnostics.status); Profile=$($artifactDiagnostics.requestProfile); Matches=$($artifactDiagnostics.candidateMatchCount)."
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

foreach ($scan in @(
    @{ Category = "Scheduler"; Check = "No scheduler/polling added"; Patterns = @("PeriodicTimer", "System.Threading.Timer", "SecurityListPoll") },
    @{ Category = "Replay"; Check = "Runtime does not submit to shadow replay"; Patterns = @("SubmitToShadowReplay = true", "SubmittedToShadowReplay = true", "ReplaySubmitAsync") },
    @{ Category = "Orders"; Check = "No order surface"; Patterns = @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder") },
    @{ Category = "Mutation"; Check = "No trading-state mutation references"; Patterns = @("PersistTrade", "TradingState") }
)) {
    $hits = Get-TextHit $apiWorker $scan.Patterns
    if ($hits.Count -eq 0) { Add-Result $scan.Category $scan.Check "PASS" "No marker found in API/Worker startup." }
    else { Add-Result $scan.Category $scan.Check "FAIL" (($hits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ") }
}

Add-Result "Runtime" "External LMAX connection" "PASS" "This gate does not connect to LMAX."
Add-Result "Snapshot" "MarketData snapshot" "PASS" "This gate does not request snapshots."
Add-Result "Replay" "Manual replay" "PASS" "This gate does not replay evidence."
Add-Result "Credentials" "Credential values" "PASS" "This gate does not require or print credential values."

$failed = @($results | Where-Object { $_.status -eq "FAIL" })
$warnings = @($results | Where-Object { $_.status -eq "WARN" })
$decision = if ($failed.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS_WITH_KNOWN_WARNINGS" } else { "PASS" }

$reportDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$reportPath = Join-Path $reportDir "phase6j-securitylist-diagnostics-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    finalDecision = $decision
    phase = "6J"
    scope = "SecurityList Failure Diagnostics / Request Profile Compatibility, No External Run by Default"
    discoveryArtifact = $artifactDiagnostics
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
