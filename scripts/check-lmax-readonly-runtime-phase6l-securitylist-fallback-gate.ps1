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

Write-Host "LMAX Read-Only Runtime Phase 6L SecurityList Fallback Gate"
Write-Host "Local-only gate. It analyzes sanitized discovery failure artifacts and does not connect to LMAX."

$modelFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlySecurityListDiscovery.cs"
$reviewScript = Join-Path $PSScriptRoot "review-lmax-readonly-runtime-securitylist-discovery-failure.ps1"
$testFile = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlySecurityListDiscoveryTests.cs"

foreach ($item in @(
    @{ Name = "SecurityList fallback decision model"; Path = $modelFile },
    @{ Name = "SecurityList failure review script"; Path = $reviewScript },
    @{ Name = "SecurityList fallback tests"; Path = $testFile }
)) {
    if (Test-Path -LiteralPath $item.Path) { Add-Result "Files" "$($item.Name) exists" "PASS" $item.Path }
    else { Add-Result "Files" "$($item.Name) exists" "FAIL" "Missing: $($item.Path)" }
}

$modelText = if (Test-Path -LiteralPath $modelFile) { Get-Content -Raw -LiteralPath $modelFile } else { "" }
foreach ($marker in @(
    "LmaxReadOnlySecurityListDiscoveryFallbackDecision",
    "LmaxReadOnlySecurityListDiscoveryFallbackDecisionValidator",
    "UseVendorSupportConfirmation",
    "BlockedPendingEvidence",
    "FailedSafeSecurityListUnknownReject",
    "ExternalRunAuthorized: false"
)) {
    if ($modelText.Contains($marker)) { Add-Result "Model" "Marker $marker" "PASS" "Fallback marker found." }
    else { Add-Result "Model" "Marker $marker" "FAIL" "Fallback marker missing." }
}

$reviewText = if (Test-Path -LiteralPath $reviewScript) { Get-Content -Raw -LiteralPath $reviewScript } else { "" }
foreach ($marker in @(
    "Local-only review",
    "No LMAX connection",
    "RecommendedFallbackDecision",
    "phase6l-securitylist-fallback-decision.json",
    "externalRunAuthorized = `$false"
)) {
    if ($reviewText.Contains($marker)) { Add-Result "ReviewScript" "Marker $marker" "PASS" "Review script marker found." }
    else { Add-Result "ReviewScript" "Marker $marker" "FAIL" "Review script marker missing." }
}

$artifactDiagnostics = $null
$fallbackDecision = $null
if ([string]::IsNullOrWhiteSpace($DiscoveryArtifactFile)) {
    Add-Result "Artifact" "Discovery artifact supplied" "WARN" "No artifact supplied; fallback tooling is present but evidence remains pending."
} else {
    $artifactPath = Resolve-LocalPath $DiscoveryArtifactFile
    if (-not (Test-Path -LiteralPath $artifactPath)) {
        Add-Result "Artifact" "Discovery artifact exists" "FAIL" "Missing: $artifactPath"
    } else {
        $artifactText = Get-Content -Raw -LiteralPath $artifactPath
        if ($artifactText -match '(?i)(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\b553=|\b554=|host=|user=|account)') {
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
            Add-Result "Artifact" "Safety flags remain false" "PASS" "Artifact remains non-authorizing."
        } else {
            Add-Result "Artifact" "Safety flags remain false" "FAIL" "Unsafe flags: $($unsafeFlags -join ', ')"
        }

        $reviewOutput = & $reviewScript -DiscoveryArtifactFile $artifactPath
        foreach ($line in $reviewOutput) { Write-Host $line }
        $decisionReportPath = Join-Path $repoRoot "artifacts/readiness/phase6l-securitylist-fallback-decision.json"
        if (Test-Path -LiteralPath $decisionReportPath) {
            $decisionReport = Get-Content -Raw -LiteralPath $decisionReportPath | ConvertFrom-Json
            $fallbackDecision = $decisionReport.fallbackDecision
            if ([string]$fallbackDecision.recommendedDecision -in @("UseVendorSupportConfirmation", "BlockedPendingEvidence", "UseOfficialLmaxDocument", "UseManualWebGuiInstrumentInfo", "ContinueSecurityListDiagnostics")) {
                Add-Result "Fallback" "Fallback decision is clear" "PASS" "Recommended=$($fallbackDecision.recommendedDecision)"
            } else {
                Add-Result "Fallback" "Fallback decision is clear" "FAIL" "Missing or unknown fallback decision."
            }
            if ([bool]$fallbackDecision.externalRunAuthorized -or [bool]$fallbackDecision.isApprovedForExternalRun) {
                Add-Result "Fallback" "Fallback remains non-authorizing" "FAIL" "Fallback attempted to authorize external run."
            } else {
                Add-Result "Fallback" "Fallback remains non-authorizing" "PASS" "No external run approval."
            }
        } else {
            Add-Result "Fallback" "Fallback report written" "FAIL" "Missing: $decisionReportPath"
        }

        $artifactDiagnostics = [ordered]@{
            path = $artifactPath
            finalStatus = if ($artifact.finalStatus) { [string]$artifact.finalStatus } else { [string]$artifact.status }
            requestProfile = [string]$artifact.requestProfile
            attemptCount = Get-JsonCollectionCount $artifact.attempts
            totalInstrumentCount = [int]$artifact.totalInstrumentCount
            candidateMatchCount = Get-JsonCollectionCount $artifact.candidateMatches
            unmatchedCandidateCount = Get-JsonCollectionCount $artifact.unmatchedCandidates
        }
        Add-Result "Artifact" "Failure artifact parsed" "PASS" "Status=$($artifactDiagnostics.finalStatus); Attempts=$($artifactDiagnostics.attemptCount); Matches=$($artifactDiagnostics.candidateMatchCount)."
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
Add-Result "Discovery" "SecurityListRequest attempted by gate" "PASS" "This gate does not run SecurityListRequest."
Add-Result "Snapshot" "MarketData snapshot" "PASS" "This gate does not request snapshots."
Add-Result "Replay" "Manual replay" "PASS" "This gate does not replay evidence."
Add-Result "Credentials" "Credential values" "PASS" "This gate does not require or print credential values."

$failed = @($results | Where-Object { $_.status -eq "FAIL" })
$warnings = @($results | Where-Object { $_.status -eq "WARN" })
$decision = if ($failed.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS_WITH_KNOWN_WARNINGS" } else { "PASS" }

$reportDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$reportPath = Join-Path $reportDir "phase6l-securitylist-fallback-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    finalDecision = $decision
    phase = "6L"
    scope = "SecurityList Unknown Reject Analysis / Fallback Decision, No External Run by Default"
    discoveryArtifact = $artifactDiagnostics
    fallbackDecision = $fallbackDecision
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
