param(
    [Parameter(Mandatory = $true)]
    [string]$PlanningManifestFile,
    [Parameter(Mandatory = $true)]
    [string]$SafetyGateManifestFile,
    [string]$PreflightManifestFile = ""
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

Write-Host "LMAX Read-Only Runtime Phase 6P Additional Snapshot Preflight Gate"
Write-Host "Local-only gate. No LMAX connection, no SecurityListRequest, no snapshots, no replay, no credentials, no orders, and no mutation."

$modelFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyAdditionalInstrumentSnapshotPreflight.cs"
$buildScript = Join-Path $PSScriptRoot "build-lmax-readonly-additional-instrument-snapshot-preflights.ps1"
$testFile = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlyAdditionalInstrumentSnapshotPreflightTests.cs"

foreach ($item in @(
    @{ Name = "Additional snapshot preflight model"; Path = $modelFile },
    @{ Name = "Additional snapshot preflight builder script"; Path = $buildScript },
    @{ Name = "Additional snapshot preflight tests"; Path = $testFile }
)) {
    if (Test-Path -LiteralPath $item.Path) { Add-Result "Files" "$($item.Name) exists" "PASS" $item.Path }
    else { Add-Result "Files" "$($item.Name) exists" "FAIL" "Missing: $($item.Path)" }
}

$planningPath = Resolve-LocalPath $PlanningManifestFile
$safetyPath = Resolve-LocalPath $SafetyGateManifestFile
$planningSummary = $null
$safetySummary = $null
$preflightSummary = $null

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
    foreach ($symbol in $expected.Keys) {
        $entry = @($planning.instruments | Where-Object { [string]$_.symbol -eq $symbol })
        if ($entry.Count -eq 1 -and [string]$entry[0].planningSecurityId -eq $expected[$symbol] -and [string]$entry[0].securityIdSource -eq "8" -and [bool]$entry[0].isApprovedForExternalRun -eq $false) {
            Add-Result "PlanningManifest" "$symbol planning value" "PASS" "$symbol=$($entry[0].planningSecurityId); source=8; non-executable."
        } else {
            Add-Result "PlanningManifest" "$symbol planning value" "FAIL" "Expected $($expected[$symbol]), source=8, IsApprovedForExternalRun=false."
        }
    }
    $planningSummary = [ordered]@{ path = $planningPath; manifestId = [string]$planning.manifestId; instrumentCount = @($planning.instruments).Count }
}

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
    if ([string]$safety.finalDecision -eq "PASS" -and -not [bool]$safety.anyEligibleForManualSnapshotAttempt -and -not [bool]$safety.allApprovedForExternalRun) {
        Add-Result "SafetyGateManifest" "Safety gate ready but non-executable" "PASS" "Phase 6O is PASS with no eligibility or external approval."
    } else {
        Add-Result "SafetyGateManifest" "Safety gate ready but non-executable" "FAIL" "Expected PASS, anyEligibleForManualSnapshotAttempt=false, allApprovedForExternalRun=false."
    }
    $safetySummary = [ordered]@{ path = $safetyPath; manifestId = [string]$safety.manifestId; instrumentCount = [int]$safety.instrumentCount; finalDecision = [string]$safety.finalDecision }
}

if ([string]::IsNullOrWhiteSpace($PreflightManifestFile)) {
    Add-Result "PreflightManifest" "Preflight manifest supplied" "WARN" "No preflight manifest supplied; source/planning checks only."
} else {
    $preflightPath = Resolve-LocalPath $PreflightManifestFile
    if (-not (Test-Path -LiteralPath $preflightPath)) {
        Add-Result "PreflightManifest" "Preflight manifest exists" "FAIL" "Missing: $preflightPath"
    } else {
        $preflightText = Get-Content -Raw -LiteralPath $preflightPath
        if ($preflightText -match '(?i)(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\b553=|\b554=|host=|user=|account)') {
            Add-Result "PreflightManifest" "No sensitive content" "FAIL" "Credential-shaped content found."
        } else {
            Add-Result "PreflightManifest" "No sensitive content" "PASS" "No credential-shaped content found."
        }
        $preflight = $preflightText | ConvertFrom-Json
        if ([string]$preflight.finalDecision -eq "PASS") { Add-Result "PreflightManifest" "Aggregate final decision" "PASS" "PASS means preflight design is safe, not executable." }
        else { Add-Result "PreflightManifest" "Aggregate final decision" "FAIL" "Expected PASS." }
        if (-not [bool]$preflight.anyCanRunExternalSnapshot -and -not [bool]$preflight.anyApprovedForExternalRun -and -not [bool]$preflight.anyEligibleForManualSnapshotAttempt) {
            Add-Result "PreflightManifest" "Aggregate non-executable flags" "PASS" "No external snapshot run, approval, or eligibility."
        } else {
            Add-Result "PreflightManifest" "Aggregate non-executable flags" "FAIL" "Executable flag appeared."
        }
        foreach ($flag in @("runtimeShadowReplaySubmit", "schedulerOrPolling", "orderSubmission", "tradingTablePersistence", "gatewayRegistration", "tradingMutation", "externalConnectionAttempted", "securityListRequestAttempted", "marketDataSnapshotAttempted", "replayAttempted")) {
            if ([bool]$preflight.$flag) { Add-Result "PreflightManifest" "Flag $flag" "FAIL" "$flag is true." }
        }
        foreach ($symbol in $expected.Keys) {
            $entry = @($preflight.results | Where-Object { [string]$_.symbol -eq $symbol })
            $request = @($preflight.requests | Where-Object { [string]$_.symbol -eq $symbol })
            if ($entry.Count -eq 1 -and $request.Count -eq 1 -and [string]$entry[0].finalDecision -eq "PASS" -and [string]$entry[0].planningSecurityId -eq $expected[$symbol] -and -not [bool]$entry[0].canRunExternalSnapshot -and -not [bool]$entry[0].eligibleForManualSnapshotAttempt -and -not [bool]$entry[0].isApprovedForExternalRun) {
                Add-Result "PreflightManifest" "$symbol preflight" "PASS" "$symbol PASS; canRunExternalSnapshot=false; eligibleForManualSnapshotAttempt=false; IsApprovedForExternalRun=false."
            } else {
                Add-Result "PreflightManifest" "$symbol preflight" "FAIL" "Expected one PASS preflight with all executable flags false."
            }
        }
        $preflightSummary = [ordered]@{
            path = $preflightPath
            manifestId = [string]$preflight.manifestId
            instrumentCount = [int]$preflight.instrumentCount
            passCount = [int]$preflight.passCount
            warningCount = [int]$preflight.warningCount
            failCount = [int]$preflight.failCount
            finalDecision = [string]$preflight.finalDecision
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
$reportPath = Join-Path $reportDir "phase6p-additional-snapshot-preflight-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    finalDecision = $decision
    phase = "6P"
    scope = "Manual Additional Instrument Snapshot Preflight Design, No External Run"
    planningManifest = $planningSummary
    safetyGateManifest = $safetySummary
    preflightManifest = $preflightSummary
    isApprovedForExternalRun = $false
    eligibleForManualSnapshotAttempt = $false
    canRunExternalSnapshot = $false
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
