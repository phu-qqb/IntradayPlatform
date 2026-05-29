param(
    [Parameter(Mandatory = $true)]
    [string]$PlanningManifestFile,
    [string]$OutputDirectory = "artifacts/lmax-readonly-runtime-securityid-planning",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$expected = [ordered]@{
    GBPUSD = @{ Slash = "GBP/USD"; SecurityId = "4002" }
    EURGBP = @{ Slash = "EUR/GBP"; SecurityId = "4003" }
    USDJPY = @{ Slash = "USD/JPY"; SecurityId = "4004" }
    AUDUSD = @{ Slash = "AUD/USD"; SecurityId = "4007" }
}

function Resolve-LocalPath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return Join-Path $repoRoot $Path
}

function New-Check([string]$Name, [bool]$Pass, [string]$Detail) {
    [ordered]@{
        name = $Name
        decision = if ($Pass) { "PASS" } else { "FAIL" }
        detail = $Detail
    }
}

function Test-Placeholder([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $true }
    return $Value -match "(?i)^(PHASE6C-|PHASE6D-|TBD)|<REAL_DEMO_SECURITY_ID>"
}

function Test-Sensitive([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    return $Value -match "(?i)(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\b553=|\b554=|host=|user=|account)"
}

function Test-AuthorizationLanguage([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    return $Value -match "(?i)(newordersingle|ordercancelrequest|ordercancelreplacerequest|tradecapture|orderstatus|order submission|submit order|external run authorized|approve external|approved for external|production|uat|execution authorized|run authorized)"
}

Write-Host "LMAX read-only Phase 6O per-instrument safety gate builder"
Write-Host "Local-only. No LMAX connection, no external API, no SecurityListRequest, no snapshot, no replay, no credentials, no scheduler/polling, no orders, and no mutation."

$planningPath = Resolve-LocalPath $PlanningManifestFile
if (-not (Test-Path -LiteralPath $planningPath)) {
    Write-Host "FinalDecision: FAIL"
    Write-Host "Planning manifest not found: $planningPath"
    exit 1
}

$planningText = Get-Content -Raw -LiteralPath $planningPath
if (Test-Sensitive $planningText) {
    Write-Host "FinalDecision: FAIL"
    Write-Host "Planning manifest contains credential-shaped or sensitive content."
    exit 1
}

$planning = $planningText | ConvertFrom-Json
$gateResults = @()
foreach ($symbol in $expected.Keys) {
    $entry = @($planning.instruments | Where-Object { [string]$_.symbol -eq $symbol })
    if ($entry.Count -ne 1) {
        $checks = @(New-Check "HasAcceptedSecurityIdPlanningValue" $false "$symbol must have exactly one planning entry.")
        $gateResults += [ordered]@{
            symbol = $symbol
            slashSymbol = $expected[$symbol].Slash
            planningSecurityId = ""
            securityIdSource = ""
            environmentName = [string]$planning.environmentName
            venueProfileName = [string]$planning.venueProfileName
            evidenceDecision = "Missing"
            isApprovedForExternalRun = $false
            eligibleForManualSnapshotAttempt = $false
            noSensitiveContent = $false
            checks = $checks
            finalDecision = "FAIL"
        }
        continue
    }

    $item = $entry[0]
    $safeValues = @(
        [string]$item.symbol
        [string]$item.slashSymbol
        [string]$item.planningSecurityId
        [string]$item.securityIdSource
        [string]$item.evidenceSource
        [string]$item.evidenceReference
        [string]$item.confirmationRecordId
        [string]$item.decision
        [string]$item.environmentName
        [string]$item.venueProfileName
    ) -join " "
    $checks = @(
        New-Check "HasAcceptedSecurityIdPlanningValue" ((-not (Test-Placeholder ([string]$item.planningSecurityId))) -and [string]$item.planningSecurityId -eq $expected[$symbol].SecurityId -and [string]$item.decision -eq "AcceptedForPlanning") "$symbol has accepted planning SecurityID $($item.planningSecurityId)."
        New-Check "HasSecurityIdSource8" ([string]$item.securityIdSource -eq "8") "$symbol uses SecurityIDSource=8."
        New-Check "HasDemoEnvironment" ([string]$item.environmentName -eq "Demo" -and [string]$planning.environmentName -eq "Demo") "$symbol is scoped to Demo."
        New-Check "HasDemoLondonVenueProfile" ([string]$item.venueProfileName -eq "DemoLondon" -and [string]$planning.venueProfileName -eq "DemoLondon") "$symbol is scoped to DemoLondon."
        New-Check "IsMarketDataOnly" $true "$symbol remains MarketDataOnly planning."
        New-Check "IsNotApprovedForExternalRun" (-not [bool]$item.isApprovedForExternalRun) "$symbol remains IsApprovedForExternalRun=false."
        New-Check "NoOrderCapability" $true "No order capability is part of this per-instrument gate."
        New-Check "NoRuntimeShadowReplaySubmit" (-not [bool]$planning.runtimeShadowReplaySubmit) "Runtime shadow replay submit remains false."
        New-Check "NoSchedulerOrPolling" (-not [bool]$planning.schedulerOrPollingAdded) "Scheduler and polling remain false."
        New-Check "NoTradingMutation" (-not [bool]$planning.tradingMutationAdded) "Trading-state mutation remains false."
        New-Check "RequiresFutureExplicitOperatorPrompt" $true "A later explicit operator prompt is required before any manual snapshot attempt can be considered."
    )
    if (-not [bool]$item.noSensitiveContent -or (Test-Sensitive $safeValues)) {
        $checks += New-Check "NoSensitiveContent" $false "$symbol contains sensitive-shaped content or noSensitiveContent=false."
    }
    if (Test-AuthorizationLanguage $safeValues) {
        $checks += New-Check "NoTradingOrExternalAuthorizationLanguage" $false "$symbol contains order/trading/external-run/Production/UAT authorization language."
    }
    $final = if (@($checks | Where-Object { $_.decision -eq "FAIL" }).Count -gt 0) { "FAIL" } else { "PASS" }
    $gateResults += [ordered]@{
        symbol = $symbol
        slashSymbol = [string]$item.slashSymbol
        planningSecurityId = [string]$item.planningSecurityId
        securityIdSource = [string]$item.securityIdSource
        environmentName = [string]$item.environmentName
        venueProfileName = [string]$item.venueProfileName
        evidenceDecision = [string]$item.decision
        isApprovedForExternalRun = $false
        eligibleForManualSnapshotAttempt = $false
        noSensitiveContent = [bool]$item.noSensitiveContent
        checks = $checks
        finalDecision = $final
    }
}

$passCount = @($gateResults | Where-Object { $_.finalDecision -eq "PASS" }).Count
$warningCount = @($gateResults | Where-Object { $_.finalDecision -eq "PASS_WITH_KNOWN_WARNINGS" }).Count
$failCount = @($gateResults | Where-Object { $_.finalDecision -eq "FAIL" }).Count
$finalDecision = if ($failCount -gt 0) { "FAIL" } elseif ($warningCount -gt 0) { "PASS_WITH_KNOWN_WARNINGS" } else { "PASS" }
$stamp = [DateTimeOffset]::UtcNow.ToString("yyyyMMdd-HHmmss")
$manifestId = "lmax-readonly-additional-instrument-safety-gates-$stamp"
$manifest = [ordered]@{
    manifestId = $manifestId
    createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    sourcePlanningManifestPath = $planningPath
    instruments = $gateResults
    instrumentCount = $gateResults.Count
    passCount = $passCount
    warningCount = $warningCount
    failCount = $failCount
    allApprovedForExternalRun = $false
    anyEligibleForManualSnapshotAttempt = $false
    runtimeShadowReplaySubmit = $false
    schedulerOrPolling = $false
    orderSubmission = $false
    gatewayRegistration = $false
    tradingMutation = $false
    externalConnectionAttempted = $false
    securityListRequestAttempted = $false
    marketDataSnapshotAttempted = $false
    replayAttempted = $false
    noSensitiveContent = ($failCount -eq 0)
    finalDecision = $finalDecision
}

$outDir = Resolve-LocalPath $OutputDirectory
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "$manifestId.json"
if ((Test-Path -LiteralPath $outPath) -and -not $Force.IsPresent) {
    Write-Host "FinalDecision: FAIL"
    Write-Host "Output file exists. Use -Force to overwrite: $outPath"
    exit 1
}

$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host "FinalDecision: $finalDecision"
Write-Host "InstrumentCount: $($gateResults.Count)"
Write-Host "PassCount: $passCount"
Write-Host "WarningCount: $warningCount"
Write-Host "FailCount: $failCount"
Write-Host "IsApprovedForExternalRun: false"
Write-Host "EligibleForManualSnapshotAttempt: false"
Write-Host "SafetyGateManifest: $outPath"
if ($finalDecision -eq "FAIL") { exit 1 }
