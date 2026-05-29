param(
    [Parameter(Mandatory = $true)]
    [string]$PlanningManifestFile,
    [string]$SafetyGateManifestFile = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$expected = [ordered]@{
    GBPUSD = "4002"
    EURGBP = "4003"
    USDJPY = "4004"
    AUDUSD = "4007"
}

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

Write-Host "LMAX Read-Only Runtime Phase 6O Per-Instrument Safety Gate"
Write-Host "Local-only gate. No LMAX connection, no SecurityListRequest, no snapshots, no replay, no credentials, no orders, and no mutation."

$modelFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyPerInstrumentSafetyGate.cs"
$buildScript = Join-Path $PSScriptRoot "build-lmax-readonly-additional-instrument-safety-gates.ps1"
$testFile = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlyPerInstrumentSafetyGateTests.cs"

foreach ($item in @(
    @{ Name = "Per-instrument safety gate model"; Path = $modelFile },
    @{ Name = "Safety gate builder script"; Path = $buildScript },
    @{ Name = "Safety gate tests"; Path = $testFile }
)) {
    if (Test-Path -LiteralPath $item.Path) { Add-Result "Files" "$($item.Name) exists" "PASS" $item.Path }
    else { Add-Result "Files" "$($item.Name) exists" "FAIL" "Missing: $($item.Path)" }
}

$planningPath = Resolve-LocalPath $PlanningManifestFile
$planningSummary = $null
if (-not (Test-Path -LiteralPath $planningPath)) {
    Add-Result "PlanningManifest" "Planning manifest exists" "FAIL" "Missing: $planningPath"
} else {
    $planningText = Get-Content -Raw -LiteralPath $planningPath
    if ($planningText -match '(?i)(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\b553=|\b554=|host=|user=|account)') {
        Add-Result "PlanningManifest" "No sensitive content" "FAIL" "Credential-shaped content found."
    } else {
        Add-Result "PlanningManifest" "No sensitive content" "PASS" "No credential-shaped content found."
    }
    $planning = $planningText | ConvertFrom-Json
    if ([string]$planning.environmentName -eq "Demo" -and [string]$planning.venueProfileName -eq "DemoLondon") {
        Add-Result "PlanningManifest" "Profile scope" "PASS" "Demo/DemoLondon."
    } else {
        Add-Result "PlanningManifest" "Profile scope" "FAIL" "Expected Demo/DemoLondon."
    }
    foreach ($flag in @("isApprovedForExternalRun", "externalConnectionAttempted", "externalApiCallAttempted", "securityListRequestAttempted", "marketDataSnapshotAttempted", "replayAttempted", "runtimeShadowReplaySubmit", "schedulerOrPollingAdded", "orderSubmissionAdded", "gatewayRegistrationAdded", "tradingMutationAdded")) {
        if ([bool]$planning.$flag) { Add-Result "PlanningManifest" "Flag $flag" "FAIL" "$flag is true." }
    }
    foreach ($symbol in $expected.Keys) {
        $entry = @($planning.instruments | Where-Object { [string]$_.symbol -eq $symbol })
        if ($entry.Count -eq 1 -and [string]$entry[0].planningSecurityId -eq $expected[$symbol] -and [string]$entry[0].securityIdSource -eq "8" -and [bool]$entry[0].isApprovedForExternalRun -eq $false -and [string]$entry[0].decision -eq "AcceptedForPlanning") {
            Add-Result "PlanningManifest" "$symbol planning value" "PASS" "$symbol=$($entry[0].planningSecurityId); source=8; non-executable."
        } else {
            Add-Result "PlanningManifest" "$symbol planning value" "FAIL" "Expected $($expected[$symbol]), source=8, AcceptedForPlanning, IsApprovedForExternalRun=false."
        }
    }
    $planningSummary = [ordered]@{
        path = $planningPath
        manifestId = [string]$planning.manifestId
        instrumentCount = @($planning.instruments).Count
    }
}

$safetySummary = $null
if ([string]::IsNullOrWhiteSpace($SafetyGateManifestFile)) {
    Add-Result "SafetyGateManifest" "Safety gate manifest supplied" "WARN" "No safety gate manifest supplied; source and planning checks only."
} else {
    $safetyPath = Resolve-LocalPath $SafetyGateManifestFile
    if (-not (Test-Path -LiteralPath $safetyPath)) {
        Add-Result "SafetyGateManifest" "Safety gate manifest exists" "FAIL" "Missing: $safetyPath"
    } else {
        $safetyText = Get-Content -Raw -LiteralPath $safetyPath
        if ($safetyText -match '(?i)(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\b553=|\b554=|host=|user=|account)') {
            Add-Result "SafetyGateManifest" "No sensitive content" "FAIL" "Credential-shaped content found."
        } else {
            Add-Result "SafetyGateManifest" "No sensitive content" "PASS" "No credential-shaped content found."
        }
        $safety = $safetyText | ConvertFrom-Json
        if ([string]$safety.finalDecision -eq "PASS") { Add-Result "SafetyGateManifest" "Aggregate final decision" "PASS" "PASS means planning data is safe, not executable." }
        else { Add-Result "SafetyGateManifest" "Aggregate final decision" "FAIL" "Expected PASS." }
        if ([bool]$safety.allApprovedForExternalRun -eq $false -and [bool]$safety.anyEligibleForManualSnapshotAttempt -eq $false) {
            Add-Result "SafetyGateManifest" "Aggregate non-executable flags" "PASS" "No external approval and no manual snapshot eligibility."
        } else {
            Add-Result "SafetyGateManifest" "Aggregate non-executable flags" "FAIL" "External approval or manual snapshot eligibility appeared."
        }
        foreach ($flag in @("runtimeShadowReplaySubmit", "schedulerOrPolling", "orderSubmission", "gatewayRegistration", "tradingMutation", "externalConnectionAttempted", "securityListRequestAttempted", "marketDataSnapshotAttempted", "replayAttempted")) {
            if ([bool]$safety.$flag) { Add-Result "SafetyGateManifest" "Flag $flag" "FAIL" "$flag is true." }
        }
        foreach ($symbol in $expected.Keys) {
            $entry = @($safety.instruments | Where-Object { [string]$_.symbol -eq $symbol })
            if ($entry.Count -eq 1 -and [string]$entry[0].finalDecision -eq "PASS" -and [bool]$entry[0].isApprovedForExternalRun -eq $false -and [bool]$entry[0].eligibleForManualSnapshotAttempt -eq $false) {
                Add-Result "SafetyGateManifest" "$symbol safety gate" "PASS" "$symbol PASS; IsApprovedForExternalRun=false; eligibleForManualSnapshotAttempt=false."
            } else {
                Add-Result "SafetyGateManifest" "$symbol safety gate" "FAIL" "Expected exactly one PASS gate with both executable flags false."
            }
        }
        $safetySummary = [ordered]@{
            path = $safetyPath
            manifestId = [string]$safety.manifestId
            instrumentCount = [int]$safety.instrumentCount
            passCount = [int]$safety.passCount
            warningCount = [int]$safety.warningCount
            failCount = [int]$safety.failCount
            finalDecision = [string]$safety.finalDecision
        }
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
Add-Result "Discovery" "SecurityListRequest" "PASS" "This gate does not run SecurityListRequest."
Add-Result "Snapshot" "MarketData snapshot" "PASS" "This gate does not request snapshots."
Add-Result "Replay" "Manual replay" "PASS" "This gate does not replay evidence."
Add-Result "Credentials" "Credential values" "PASS" "This gate does not require or print credential values."

$failed = @($results | Where-Object { $_.status -eq "FAIL" })
$warnings = @($results | Where-Object { $_.status -eq "WARN" })
$decision = if ($failed.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS_WITH_KNOWN_WARNINGS" } else { "PASS" }

$reportDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$reportPath = Join-Path $reportDir "phase6o-per-instrument-safety-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    finalDecision = $decision
    phase = "6O"
    scope = "Per-Instrument Safety Gate Design, No External Run"
    planningManifest = $planningSummary
    safetyGateManifest = $safetySummary
    isApprovedForExternalRun = $false
    eligibleForManualSnapshotAttempt = $false
    externalConnectionAttempted = $false
    securityListRequestAttempted = $false
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
