param(
    [string]$PlanningManifestFile = ""
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

Write-Host "LMAX Read-Only Runtime Phase 6N Planning Values Gate"
Write-Host "Local-only gate. No LMAX connection, no SecurityListRequest, no snapshots, no replay, no credentials, no orders, and no mutation."

$modelFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyInstrumentSecurityIdPlanningManifest.cs"
$applyScript = Join-Path $PSScriptRoot "apply-lmax-readonly-securityid-planning-values.ps1"
$testFile = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlyInstrumentSecurityIdPlanningManifestTests.cs"

foreach ($item in @(
    @{ Name = "Planning manifest model"; Path = $modelFile },
    @{ Name = "Planning value apply script"; Path = $applyScript },
    @{ Name = "Planning manifest tests"; Path = $testFile }
)) {
    if (Test-Path -LiteralPath $item.Path) { Add-Result "Files" "$($item.Name) exists" "PASS" $item.Path }
    else { Add-Result "Files" "$($item.Name) exists" "FAIL" "Missing: $($item.Path)" }
}

$modelText = if (Test-Path -LiteralPath $modelFile) { Get-Content -Raw -LiteralPath $modelFile } else { "" }
foreach ($marker in @("LmaxReadOnlyInstrumentSecurityIdPlanningManifest", "PlanningSecurityId", "SecurityIdSource", "IsApprovedForExternalRun", "DemoLondon")) {
    if ($modelText.Contains($marker)) { Add-Result "Model" "Marker $marker" "PASS" "Planning manifest marker found." }
    else { Add-Result "Model" "Marker $marker" "FAIL" "Planning manifest marker missing." }
}

$manifestSummary = $null
if ([string]::IsNullOrWhiteSpace($PlanningManifestFile)) {
    Add-Result "Manifest" "Planning manifest supplied" "WARN" "No manifest supplied; source checks only."
} else {
    $manifestPath = Resolve-LocalPath $PlanningManifestFile
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        Add-Result "Manifest" "Planning manifest exists" "FAIL" "Missing: $manifestPath"
    } else {
        $manifestText = Get-Content -Raw -LiteralPath $manifestPath
        if ($manifestText -match '(?i)(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\b553=|\b554=|host=|user=|account)') {
            Add-Result "Manifest" "No sensitive content" "FAIL" "Credential-shaped content found."
        } else {
            Add-Result "Manifest" "No sensitive content" "PASS" "No credential-shaped content found."
        }

        $manifest = $manifestText | ConvertFrom-Json
        $stringValues = @(
            [string]$manifest.manifestId
            [string]$manifest.environmentName
            [string]$manifest.venueProfileName
            @($manifest.instruments | ForEach-Object {
                @(
                    [string]$_.symbol
                    [string]$_.slashSymbol
                    [string]$_.planningSecurityId
                    [string]$_.evidenceSource
                    [string]$_.evidenceReference
                    [string]$_.confirmationRecordId
                    [string]$_.decision
                    [string]$_.environmentName
                    [string]$_.venueProfileName
                )
            })
        ) -join " "
        if ($stringValues -match '(?i)(newordersingle|ordercancelrequest|ordercancelreplacerequest|tradecapture|orderstatus|order submission|submit order|external run authorized|approve external|approved for external|production|uat|execution authorized|run authorized)') {
            Add-Result "Manifest" "No authorization language" "FAIL" "Order/trading/external-run authorization language found in manifest values."
        } else {
            Add-Result "Manifest" "No authorization language" "PASS" "No order/trading authorization language found in manifest values."
        }
        if ([string]$manifest.environmentName -eq "Demo" -and [string]$manifest.venueProfileName -eq "DemoLondon") {
            Add-Result "Manifest" "Profile scope" "PASS" "Demo/DemoLondon."
        } else {
            Add-Result "Manifest" "Profile scope" "FAIL" "Expected Demo/DemoLondon."
        }

        $unsafeFlags = @()
        foreach ($flag in @("isApprovedForExternalRun", "externalConnectionAttempted", "externalApiCallAttempted", "securityListRequestAttempted", "marketDataSnapshotAttempted", "replayAttempted", "runtimeShadowReplaySubmit", "schedulerOrPollingAdded", "orderSubmissionAdded", "gatewayRegistrationAdded", "tradingMutationAdded")) {
            if ([bool]$manifest.$flag) { $unsafeFlags += $flag }
        }
        if ($unsafeFlags.Count -eq 0 -and [bool]$manifest.noSensitiveContent) {
            Add-Result "Manifest" "Manifest safety flags" "PASS" "All runtime/external approval flags are false."
        } else {
            Add-Result "Manifest" "Manifest safety flags" "FAIL" "Unsafe flags: $($unsafeFlags -join ', ')"
        }

        foreach ($symbol in $expected.Keys) {
            $entry = @($manifest.instruments | Where-Object { [string]$_.symbol -eq $symbol })
            if ($entry.Count -ne 1) {
                Add-Result "Manifest" "$symbol entry" "FAIL" "Expected exactly one planning entry."
                continue
            }
            $item = $entry[0]
            if ([string]$item.planningSecurityId -eq $expected[$symbol] -and [string]$item.securityIdSource -eq "8" -and [bool]$item.isApprovedForExternalRun -eq $false -and [string]$item.decision -eq "AcceptedForPlanning") {
                Add-Result "Manifest" "$symbol planning value" "PASS" "$symbol=$($item.planningSecurityId); SecurityIDSource=8; non-executable."
            } else {
                Add-Result "Manifest" "$symbol planning value" "FAIL" "Expected $($expected[$symbol]), source=8, AcceptedForPlanning, IsApprovedForExternalRun=false."
            }
        }

        $manifestSummary = [ordered]@{
            path = $manifestPath
            manifestId = [string]$manifest.manifestId
            environmentName = [string]$manifest.environmentName
            venueProfileName = [string]$manifest.venueProfileName
            instrumentCount = @($manifest.instruments).Count
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
$reportPath = Join-Path $reportDir "phase6n-planning-values-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    finalDecision = $decision
    phase = "6N"
    scope = "Apply Accepted SecurityID Planning Values to Planning Manifest, still IsApprovedForExternalRun=false"
    planningManifest = $manifestSummary
    isApprovedForExternalRun = $false
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
