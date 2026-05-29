param(
    [Parameter(Mandatory = $true)]
    [string]$PlanningManifestFile,
    [Parameter(Mandatory = $true)]
    [string]$SafetyGateManifestFile,
    [Parameter(Mandatory = $true)]
    [string]$RequestedByOperatorId,
    [Parameter(Mandatory = $true)]
    [string]$Reason,
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

Write-Host "LMAX read-only Phase 6P additional instrument snapshot preflight builder"
Write-Host "Local-only. No LMAX connection, no external API, no SecurityListRequest, no snapshot, no replay, no credentials, no scheduler/polling, no orders, and no mutation."

if ([string]::IsNullOrWhiteSpace($RequestedByOperatorId)) {
    Write-Host "FinalDecision: FAIL"
    Write-Host "RequestedByOperatorId is required."
    exit 1
}

if ([string]::IsNullOrWhiteSpace($Reason)) {
    Write-Host "FinalDecision: FAIL"
    Write-Host "Reason is required."
    exit 1
}

if ((Test-Sensitive $RequestedByOperatorId) -or (Test-Sensitive $Reason) -or (Test-AuthorizationLanguage $Reason) -or (Test-AuthorizationLanguage $RequestedByOperatorId)) {
    Write-Host "FinalDecision: FAIL"
    Write-Host "Operator id/reason contains sensitive-shaped or authorization language."
    exit 1
}

$planningPath = Resolve-LocalPath $PlanningManifestFile
$safetyPath = Resolve-LocalPath $SafetyGateManifestFile
if (-not (Test-Path -LiteralPath $planningPath)) {
    Write-Host "FinalDecision: FAIL"
    Write-Host "Planning manifest not found: $planningPath"
    exit 1
}
if (-not (Test-Path -LiteralPath $safetyPath)) {
    Write-Host "FinalDecision: FAIL"
    Write-Host "Safety gate manifest not found: $safetyPath"
    exit 1
}

$planningText = Get-Content -Raw -LiteralPath $planningPath
$safetyText = Get-Content -Raw -LiteralPath $safetyPath
if ((Test-Sensitive $planningText) -or (Test-Sensitive $safetyText)) {
    Write-Host "FinalDecision: FAIL"
    Write-Host "Input manifest contains credential-shaped or sensitive content."
    exit 1
}

$planning = $planningText | ConvertFrom-Json
$safety = $safetyText | ConvertFrom-Json
$stamp = [DateTimeOffset]::UtcNow.ToString("yyyyMMdd-HHmmss")
$manifestId = "lmax-readonly-additional-instrument-snapshot-preflights-$stamp"
$requests = @()
$results = @()

foreach ($symbol in $expected.Keys) {
    $planningEntry = @($planning.instruments | Where-Object { [string]$_.symbol -eq $symbol })
    $safetyEntry = @($safety.instruments | Where-Object { [string]$_.symbol -eq $symbol })
    $preflightId = "lmax-readonly-additional-snapshot-preflight-$symbol-$stamp"
    $item = if ($planningEntry.Count -eq 1) { $planningEntry[0] } else { $null }
    $gate = if ($safetyEntry.Count -eq 1) { $safetyEntry[0] } else { $null }

    $request = [ordered]@{
        preflightId = $preflightId
        createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
        requestedByOperatorId = $RequestedByOperatorId
        reason = $Reason
        symbol = $symbol
        slashSymbol = if ($item) { [string]$item.slashSymbol } else { $expected[$symbol].Slash }
        planningSecurityId = if ($item) { [string]$item.planningSecurityId } else { "" }
        securityIdSource = if ($item) { [string]$item.securityIdSource } else { "" }
        environmentName = if ($item) { [string]$item.environmentName } else { "" }
        venueProfileName = if ($item) { [string]$item.venueProfileName } else { "" }
        requestMode = "SnapshotPlusUpdates"
        symbolEncodingMode = "SecurityIdOnly"
        marketDepth = 1
        maxRuntimeSeconds = 30
        maxWaitSeconds = 30
        maxEventsPerRun = 25
        allowExternalConnections = $false
        confirmDemoReadOnly = $false
        allowOrderSubmission = $false
        schedulerEnabled = $false
        submitToShadowReplay = $false
        persistToTradingTables = $false
        isApprovedForExternalRun = $false
        eligibleForManualSnapshotAttempt = $false
        canRunExternalSnapshot = $false
        noSensitiveContent = $true
    }

    $safeValues = @(
        $request.preflightId
        $request.requestedByOperatorId
        $request.reason
        $request.symbol
        $request.slashSymbol
        $request.planningSecurityId
        $request.securityIdSource
        $request.environmentName
        $request.venueProfileName
        $request.requestMode
        $request.symbolEncodingMode
    ) -join " "
    $checks = @(
        New-Check "OperatorIdRequired" (-not [string]::IsNullOrWhiteSpace($request.requestedByOperatorId)) "Operator id is required."
        New-Check "ReasonRequired" (-not [string]::IsNullOrWhiteSpace($request.reason)) "Reason is required."
        New-Check "SymbolExistsInSafetyGateManifest" ($null -ne $gate) "$symbol exists in the Phase 6O safety gate manifest."
        New-Check "SlashSymbolMatchesPlanningManifest" ($null -ne $item -and [string]$request.slashSymbol -eq $expected[$symbol].Slash) "$symbol slash symbol matches planning manifest."
        New-Check "PlanningSecurityIdMatches" ($null -ne $item -and [string]$request.planningSecurityId -eq $expected[$symbol].SecurityId -and -not (Test-Placeholder ([string]$request.planningSecurityId))) "$symbol planning SecurityID matches Phase 6N."
        New-Check "SecurityIdSource8" ([string]$request.securityIdSource -eq "8") "SecurityIDSource must be 8."
        New-Check "DemoEnvironment" ([string]$request.environmentName -eq "Demo" -and [string]$planning.environmentName -eq "Demo") "Environment must be Demo."
        New-Check "DemoLondonVenueProfile" ([string]$request.venueProfileName -eq "DemoLondon" -and [string]$planning.venueProfileName -eq "DemoLondon") "Venue profile must be DemoLondon."
        New-Check "RequestModeSnapshotPlusUpdates" ([string]$request.requestMode -eq "SnapshotPlusUpdates") "Request mode must be SnapshotPlusUpdates."
        New-Check "SymbolEncodingModeSecurityIdOnly" ([string]$request.symbolEncodingMode -eq "SecurityIdOnly") "Symbol encoding mode must be SecurityIdOnly."
        New-Check "MarketDepthOne" ([int]$request.marketDepth -eq 1) "MarketDepth must be 1."
        New-Check "MaxRuntimeSecondsSafeCap" ([int]$request.maxRuntimeSeconds -ge 1 -and [int]$request.maxRuntimeSeconds -le 30) "MaxRuntimeSeconds must be 1..30."
        New-Check "MaxWaitSecondsSafeCap" ([int]$request.maxWaitSeconds -ge 1 -and [int]$request.maxWaitSeconds -le 30) "MaxWaitSeconds must be 1..30."
        New-Check "MaxEventsPerRunSafeCap" ([int]$request.maxEventsPerRun -ge 1 -and [int]$request.maxEventsPerRun -le 25) "MaxEventsPerRun must be 1..25."
        New-Check "AllowExternalConnectionsFalse" (-not [bool]$request.allowExternalConnections) "Phase 6P does not allow external connections."
        New-Check "AllowOrderSubmissionFalse" (-not [bool]$request.allowOrderSubmission) "Order submission must remain false."
        New-Check "SchedulerEnabledFalse" (-not [bool]$request.schedulerEnabled) "Scheduler must remain disabled."
        New-Check "SubmitToShadowReplayFalse" (-not [bool]$request.submitToShadowReplay) "Runtime shadow replay submit must remain false."
        New-Check "PersistToTradingTablesFalse" (-not [bool]$request.persistToTradingTables) "Trading-table persistence must remain false."
        New-Check "IsApprovedForExternalRunFalse" (-not [bool]$request.isApprovedForExternalRun) "IsApprovedForExternalRun must remain false."
        New-Check "EligibleForManualSnapshotAttemptFalse" (-not [bool]$request.eligibleForManualSnapshotAttempt) "eligibleForManualSnapshotAttempt must remain false."
        New-Check "CanRunExternalSnapshotFalse" (-not [bool]$request.canRunExternalSnapshot) "canRunExternalSnapshot must remain false."
        New-Check "NoSensitiveContentTrue" ([bool]$request.noSensitiveContent -and -not (Test-Sensitive $safeValues)) "noSensitiveContent must be true."
        New-Check "SafetyGatePassed" ($null -ne $gate -and [string]$gate.finalDecision -eq "PASS") "$symbol Phase 6O safety gate must be PASS."
        New-Check "SafetyGateNonExecutable" ($null -ne $gate -and -not [bool]$gate.isApprovedForExternalRun -and -not [bool]$gate.eligibleForManualSnapshotAttempt) "$symbol Phase 6O gate must remain non-executable."
    )
    if (Test-AuthorizationLanguage $safeValues) {
        $checks += New-Check "NoTradingOrExternalAuthorizationLanguage" $false "$symbol request contains order/trading/external-run/Production/UAT authorization language."
    }

    $final = if (@($checks | Where-Object { $_.decision -eq "FAIL" }).Count -gt 0) { "FAIL" } else { "PASS" }
    $requests += $request
    $results += [ordered]@{
        preflightId = $preflightId
        symbol = $symbol
        slashSymbol = $request.slashSymbol
        planningSecurityId = $request.planningSecurityId
        checks = $checks
        finalDecision = $final
        canRunExternalSnapshot = $false
        requiresFutureExplicitOperatorPrompt = $true
        isApprovedForExternalRun = $false
        eligibleForManualSnapshotAttempt = $false
        noSensitiveContent = $true
    }
}

$passCount = @($results | Where-Object { $_.finalDecision -eq "PASS" }).Count
$warningCount = @($results | Where-Object { $_.finalDecision -eq "PASS_WITH_KNOWN_WARNINGS" }).Count
$failCount = @($results | Where-Object { $_.finalDecision -eq "FAIL" }).Count
$finalDecision = if ($failCount -gt 0) { "FAIL" } elseif ($warningCount -gt 0) { "PASS_WITH_KNOWN_WARNINGS" } else { "PASS" }

$manifest = [ordered]@{
    manifestId = $manifestId
    createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    sourcePlanningManifestPath = $planningPath
    sourceSafetyGateManifestPath = $safetyPath
    requests = $requests
    results = $results
    instrumentCount = $results.Count
    passCount = $passCount
    warningCount = $warningCount
    failCount = $failCount
    anyCanRunExternalSnapshot = $false
    anyApprovedForExternalRun = $false
    anyEligibleForManualSnapshotAttempt = $false
    runtimeShadowReplaySubmit = $false
    schedulerOrPolling = $false
    orderSubmission = $false
    tradingTablePersistence = $false
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

$manifest | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host "FinalDecision: $finalDecision"
Write-Host "InstrumentCount: $($results.Count)"
Write-Host "PassCount: $passCount"
Write-Host "WarningCount: $warningCount"
Write-Host "FailCount: $failCount"
Write-Host "CanRunExternalSnapshot: false"
Write-Host "EligibleForManualSnapshotAttempt: false"
Write-Host "IsApprovedForExternalRun: false"
Write-Host "PreflightManifest: $outPath"
if ($finalDecision -eq "FAIL") { exit 1 }
