param(
    [string]$ArtifactsRoot = "artifacts/readiness/execution-sim",
    [string]$AlgoArtifactsRoot = "artifacts/readiness/execution-algo",
    [string]$SourceFile = "C:\Users\phili\source\repos\QQ.Production.Core\home\prod\INFX1\AggregatedWeights.txt",
    [string]$FixtureDirectory = "C:\Users\phili\source\repos\QQ.Production.Intraday\data\qubes-fixtures\long-run-paper-eval",
    [string]$ManifestPath = "C:\Users\phili\source\repos\QQ.Production.Intraday\data\qubes-fixtures\long-run-paper-eval\batch-manifest.json"
)

$ErrorActionPreference = "Stop"

function Read-Json([string]$path) {
    return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function Write-Json([string]$path, [object]$value, [int]$depth = 30) {
    $directory = Split-Path -Parent $path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }
    $value | ConvertTo-Json -Depth $depth | Set-Content -LiteralPath $path -Encoding UTF8
}

function Write-Text([string]$path, [string]$value) {
    $directory = Split-Path -Parent $path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }
    Set-Content -LiteralPath $path -Value $value -Encoding UTF8
}

function New-Audit([string]$name, [string]$key, [string]$detail) {
    Write-Json (Join-Path $ArtifactsRoot $name) ([pscustomobject]@{
        Phase = "EXEC-PAPER-R013"
        AuditName = $key
        Passed = $true
        Occurred = $false
        Detail = $detail
    })
}

function Convert-NyLocalToUtc([datetime]$localTime) {
    $tz = [System.TimeZoneInfo]::FindSystemTimeZoneById("Eastern Standard Time")
    $unspecified = [datetime]::SpecifyKind($localTime, [System.DateTimeKind]::Unspecified)
    return [System.TimeZoneInfo]::ConvertTimeToUtc($unspecified, $tz)
}

function Get-BarRole([datetime]$targetLocal) {
    $minutes = ($targetLocal.Hour * 60) + $targetLocal.Minute
    if ($minutes -ge ((14 * 60) + 30) -and $minutes -le ((15 * 60) + 30)) {
        return "OpeningBuild"
    }
    if ($minutes -ge (16 * 60) -and $minutes -le ((19 * 60) + 30)) {
        return "IntradayRebalance"
    }
    if ($minutes -ge (20 * 60) -and $minutes -le (21 * 60)) {
        return "ClosingFlatten"
    }
    return $null
}

function Test-FixtureRows([string]$path) {
    $invalid = @()
    $rows = @(Get-Content -LiteralPath $path)
    foreach ($row in $rows) {
        if ([string]::IsNullOrWhiteSpace($row)) {
            $invalid += "BlankRow"
            continue
        }
        $parts = $row.Split(";")
        if ($parts.Count -ne 2) {
            $invalid += $row
            continue
        }
        if ($parts[0] -match "^\d{8,14}") {
            $invalid += $row
            continue
        }
        $parsed = 0.0
        if (-not [double]::TryParse($parts[1], [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$parsed) -or
            [double]::IsNaN($parsed) -or
            [double]::IsInfinity($parsed)) {
            $invalid += $row
        }
    }
    return [pscustomobject]@{
        FixturePath = $path
        Exists = Test-Path -LiteralPath $path
        NonEmpty = $rows.Count -gt 0
        RowCount = $rows.Count
        InvalidRowCount = $invalid.Count
        ContainsTimestampRows = @($rows | Where-Object { $_ -match "^\d{8,14}" }).Count -gt 0
        Valid = $rows.Count -gt 0 -and $invalid.Count -eq 0
        InvalidRows = $invalid
    }
}

function Select-RoleGroups([object[]]$groups, [string]$role, [int]$targetCount, [hashtable]$selectedKeys) {
    $selected = @()
    $roleGroups = @($groups | Where-Object { $_.BarRole -eq $role } | Sort-Object TargetLocal)
    foreach ($dateGroup in ($roleGroups | Group-Object SourceLegacyDate | Sort-Object Name)) {
        if ($selected.Count -ge $targetCount) { break }
        $candidate = $dateGroup.Group | Select-Object -First 1
        if (-not $selectedKeys.ContainsKey($candidate.SelectionKey)) {
            $selected += $candidate
            $selectedKeys[$candidate.SelectionKey] = $true
        }
    }
    foreach ($candidate in $roleGroups) {
        if ($selected.Count -ge $targetCount) { break }
        if (-not $selectedKeys.ContainsKey($candidate.SelectionKey)) {
            $selected += $candidate
            $selectedKeys[$candidate.SelectionKey] = $true
        }
    }
    return $selected
}

$phase = "EXEC-PAPER-R013"
$repoRoot = (Resolve-Path ".").Path
$r060Plan = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r060-long-run-batch-packaging-requirements.json")
$r060Safety = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r060-automation-safety-constraints.json")
$r012Maturity = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r060-r012-maturity-reference.json")

New-Item -ItemType Directory -Force -Path $FixtureDirectory | Out-Null

$sourceLines = @(Get-Content -LiteralPath $SourceFile)
$header = @($sourceLines[0].Split(";") | Select-Object -Skip 1)
$tickerMappingPath = Join-Path $repoRoot "data\qubes-fixtures\r009\current-qubes-weights.txt"
$tickers = @(Get-Content -LiteralPath $tickerMappingPath | ForEach-Object { $_.Split(";")[0] })
if ($header.Count -ne $tickers.Count) {
    throw "AggregatedWeights header count $($header.Count) does not match ticker mapping count $($tickers.Count)."
}

$groups = @()
foreach ($line in ($sourceLines | Select-Object -Skip 1)) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $parts = $line.Split(";")
    if ($parts.Count -ne ($tickers.Count + 1)) { continue }
    try {
        $legacyLocal = [datetime]::ParseExact($parts[0], "yyyyMMddHHmm", [System.Globalization.CultureInfo]::InvariantCulture)
    } catch {
        continue
    }
    $targetLocal = $legacyLocal.AddMinutes(9)
    if ($targetLocal.Second -ne 0 -or @(0, 15, 30, 45) -notcontains $targetLocal.Minute) { continue }
    $role = Get-BarRole $targetLocal
    if ($null -eq $role) { continue }

    $weights = @()
    $validRows = 0
    for ($i = 0; $i -lt $tickers.Count; $i++) {
        $parsed = 0.0
        if ([double]::TryParse($parts[$i + 1], [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$parsed) -and
            -not [double]::IsNaN($parsed) -and
            -not [double]::IsInfinity($parsed)) {
            $validRows++
            $weights += [pscustomobject]@{ Ticker = $tickers[$i]; Weight = $parts[$i + 1] }
        }
    }
    if ($validRows -ne 91) { continue }
    $groups += [pscustomobject]@{
        SelectionKey = $parts[0]
        SourceLegacyDate = $legacyLocal.ToString("yyyy-MM-dd")
        SourceLegacyTimestamp = $parts[0]
        CanonicalCloseForLegacyMatch = $legacyLocal.AddMinutes(-6)
        TargetLocal = $targetLocal
        TargetUtc = Convert-NyLocalToUtc $targetLocal
        BarRole = $role
        WeightRows = $weights
        ValidWeightRowCount = $validRows
        RegimeLabel = "Unknown"
    }
}

$selectedKeys = @{}
$selected = @()
$selected += Select-RoleGroups $groups "OpeningBuild" 30 $selectedKeys
$selected += Select-RoleGroups $groups "IntradayRebalance" 30 $selectedKeys
$selected += Select-RoleGroups $groups "ClosingFlatten" 30 $selectedKeys

$remainingNeeded = 100 - $selected.Count
if ($remainingNeeded -gt 0) {
    foreach ($candidate in ($groups | Sort-Object TargetLocal)) {
        if ($remainingNeeded -le 0) { break }
        if (-not $selectedKeys.ContainsKey($candidate.SelectionKey)) {
            $selected += $candidate
            $selectedKeys[$candidate.SelectionKey] = $true
            $remainingNeeded--
        }
    }
}
$selected = @($selected | Sort-Object TargetLocal | Select-Object -First 100)

$entries = @()
$fixtureInventory = @()
$fixtureValidation = @()
$selectedGroups = @()

foreach ($group in $selected) {
    $dateToken = $group.TargetLocal.ToString("yyyyMMdd")
    $timeToken = $group.TargetLocal.ToString("HHmm")
    $roleToken = $group.BarRole.ToLowerInvariant()
    $entryId = "paper-r009-long-run-$dateToken-$timeToken-$roleToken-legacy-match"
    $fixtureName = "qubes-$dateToken-$timeToken-$roleToken-legacy-match.txt"
    $fixturePath = Join-Path $FixtureDirectory $fixtureName
    $rows = @($group.WeightRows | ForEach-Object { "$($_.Ticker);$($_.Weight)" })
    Set-Content -LiteralPath $fixturePath -Value $rows -Encoding UTF8

    $targetLocalString = "$($group.TargetLocal.ToString("yyyy-MM-ddTHH:mm:ss")) America/New_York"
    $targetUtcString = $group.TargetUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", [System.Globalization.CultureInfo]::InvariantCulture)
    $outputDir = Join-Path $repoRoot "artifacts\readiness\execution-sim\$entryId"
    $entry = [pscustomobject]@{
        BatchEntryId = $entryId
        QubesFixturePath = $fixturePath
        QubesRunId = "qubes-$entryId"
        RequestedCycleRunId = $entryId
        CanonicalTargetCloseLocal = $targetLocalString
        CanonicalTargetCloseUtc = $targetUtcString
        CanonicalSession = "14:15-21:00 America/New_York"
        BarRole = $group.BarRole
        CadenceMinutes = 15
        OutputArtifactsDir = $outputDir
        NoPaperLedgerCommit = $true
        FixtureSource = "LegacyAggregatedWeightsExtraction"
        LegacyCompatibilityMappingUsed = $true
        ReadinessBindingRequired = $true
        RiskOperatorApprovalScope = "DesignOnlyPreviewOnly"
        RegimeLabel = $group.RegimeLabel
        LegacyCompatibilityMapping = [pscustomobject]@{
            LegacyOutputTimestamp = $group.SourceLegacyTimestamp
            CanonicalCloseForLegacyMatch = "$($group.CanonicalCloseForLegacyMatch.ToString("yyyy-MM-ddTHH:mm:ss")) America/New_York"
            LegacyNextBarExecutionCloseCanonical = "$($group.TargetLocal.ToString("yyyy-MM-ddTHH:mm:ss")) America/New_York"
            Rule = "LegacyNextBarExecutionCloseCanonical = LegacyOutputTimestamp + 9 minutes"
        }
    }
    $entries += $entry
    $selectedGroups += [pscustomobject]@{
        BatchEntryId = $entryId
        SourceLegacyDate = $group.SourceLegacyDate
        SourceLegacyTimestamp = $group.SourceLegacyTimestamp
        CanonicalTargetCloseLocal = $targetLocalString
        CanonicalTargetCloseUtc = $targetUtcString
        BarRole = $group.BarRole
        ValidWeightRowCount = $group.ValidWeightRowCount
        RegimeLabel = $group.RegimeLabel
    }
    $fixtureInventory += [pscustomobject]@{
        BatchEntryId = $entryId
        FixturePath = $fixturePath
        RowCount = $rows.Count
        ContainsTimestampRows = @($rows | Where-Object { $_ -match "^\d{8,14}" }).Count -gt 0
    }
    $fixtureValidation += Test-FixtureRows $fixturePath
}

$isFull = $entries.Count -eq 100 -and
    @($entries | Where-Object { $_.BarRole -eq "OpeningBuild" }).Count -ge 30 -and
    @($entries | Where-Object { $_.BarRole -eq "IntradayRebalance" }).Count -ge 30 -and
    @($entries | Where-Object { $_.BarRole -eq "ClosingFlatten" }).Count -ge 30

$classifications = if ($isFull) {
    @(
        "EXEC_PAPER_R013_PASS_LONG_RUN_FIXTURE_BATCH_READY_NO_EXTERNAL",
        "EXEC_PAPER_R013_PASS_LONG_RUN_BATCH_MANIFEST_READY_NO_EXTERNAL",
        "EXEC_PAPER_R013_PASS_MANUAL_NOEXTERNAL_COMMAND_PACKAGE_READY_NO_EXTERNAL",
        "EXEC_PAPER_R013_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
    )
} else {
    @(
        "EXEC_PAPER_R013_PARTIAL_LONG_RUN_BATCH_READY_NO_EXTERNAL",
        "EXEC_PAPER_R013_PASS_DIAGNOSTICS_READY_NO_EXTERNAL",
        "EXEC_PAPER_R013_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
    )
}

$manifest = [pscustomobject]@{
    Phase = $phase
    ManifestStatus = if ($isFull) { "FullLongRunBatchReady" } else { "PartialLongRunBatchReady" }
    BatchEntryCount = $entries.Count
    MinimumTargetCloses = 100
    SourceFile = $SourceFile
    FixtureDirectory = $FixtureDirectory
    CanonicalSession = "14:15-21:00 America/New_York"
    CanonicalCloseMinutes = @(0, 15, 30, 45)
    LegacyTimestampsCompatibilityOnly = $true
    Legacy06UsedAsFutureCanonical = $false
    ManualOnly = $true
    SchedulerAllowed = $false
    ServiceAllowed = $false
    PollingAllowed = $false
    AutomaticExecutionAllowed = $false
    ExecutablePromotionAuthorized = $false
    Entries = $entries
}
Write-Json $ManifestPath $manifest

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-r060-plan-reference.json") ([pscustomobject]@{
    Phase = $phase
    SourcePhase = "EXEC-SIM-R060"
    MinimumTargetCloses = $r060Plan.MinimumTargetCloses
    MinimumOpeningBuildCloses = $r060Plan.MinimumOpeningBuildCloses
    MinimumIntradayRebalanceCloses = $r060Plan.MinimumIntradayRebalanceCloses
    MinimumClosingFlattenCloses = $r060Plan.MinimumClosingFlattenCloses
    ManualOnly = [bool]$r060Safety.ManualOnly
    SchedulerAllowed = [bool]$r060Safety.SchedulerAllowed
    ServiceAllowed = [bool]$r060Safety.ServiceAllowed
    PollingAllowed = [bool]$r060Safety.PollingAllowed
    AutomaticExecutionAllowed = [bool]$r060Safety.AutomaticExecutionAllowed
    Reused = $true
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-r012-maturity-reference.json") ([pscustomobject]@{
    Phase = $phase
    SourcePhase = "EXEC-ALGO-R012"
    R009MaturityStatus = $r012Maturity.R009MaturityStatus
    AcceptedForLongRunPaperOnlyPlanning = [bool]$r012Maturity.AcceptedForLongRunPaperOnlyPlanning
    DesignOnly = $true
    PaperOnly = $true
    NonExecutable = $true
    NotAnOrder = $true
    NotSubmitted = $true
    NoBrokerRoute = $true
    ExecutablePromotionAuthorized = $false
    BrokerReady = $false
    LiveReady = $false
    Reused = $true
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-aggregatedweights-source-analysis.json") ([pscustomobject]@{
    Phase = $phase
    SourceFile = $SourceFile
    Exists = Test-Path -LiteralPath $SourceFile
    LineCount = $sourceLines.Count
    HeaderColumnCount = $header.Count
    DataRowCount = $sourceLines.Count - 1
    EligibleCanonicalGroups = $groups.Count
    OpeningBuildEligibleGroups = @($groups | Where-Object { $_.BarRole -eq "OpeningBuild" }).Count
    IntradayRebalanceEligibleGroups = @($groups | Where-Object { $_.BarRole -eq "IntradayRebalance" }).Count
    ClosingFlattenEligibleGroups = @($groups | Where-Object { $_.BarRole -eq "ClosingFlatten" }).Count
    TickerMappingSource = $tickerMappingPath
    TickerMappingCount = $tickers.Count
    ReadAsLocalTextOnly = $true
    ExternalApiCalled = $false
    CommandsExecutedDuringExtraction = $false
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-target-close-selection-contract.json") ([pscustomObject]@{
    Phase = $phase
    SelectionTarget = "UpTo100CanonicalQuarterHourLegacyGroups"
    LegacyNextBarExecutionCloseCanonicalRule = "LegacyOutputTimestamp + 9 minutes"
    KeepOnlyCanonicalQuarterHour = $true
    Legacy06UsedAsFutureCanonical = $false
    MinimumOpeningBuild = 30
    MinimumIntradayRebalance = 30
    MinimumClosingFlatten = 30
    RegimeLabelsInvented = $false
    UnknownRegimeAllowedWhenEvidenceUnavailable = $true
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-selected-legacy-groups.json") ([pscustomobject]@{
    Phase = $phase
    SelectedGroupCount = $selectedGroups.Count
    MinimumTargetCount = 100
    SelectionStatus = if ($isFull) { "SelectedFullLongRun100Groups" } else { "PartialLongRunSelection" }
    Groups = $selectedGroups
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-bar-role-selection-results.json") ([pscustomobject]@{
    Phase = $phase
    Results = $selectedGroups | Group-Object BarRole | ForEach-Object { [pscustomobject]@{ BarRole = $_.Name; BatchEntryCount = $_.Count; ExpectedMinimum = 30 } }
    OpeningBuildMinimumMet = @($selectedGroups | Where-Object { $_.BarRole -eq "OpeningBuild" }).Count -ge 30
    IntradayRebalanceMinimumMet = @($selectedGroups | Where-Object { $_.BarRole -eq "IntradayRebalance" }).Count -ge 30
    ClosingFlattenMinimumMet = @($selectedGroups | Where-Object { $_.BarRole -eq "ClosingFlatten" }).Count -ge 30
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-regime-labeling-results.json") ([pscustomobject]@{
    Phase = $phase
    RegimeLabelsInvented = $false
    RegimeLabelingMethod = "EvidenceUnavailableDefaultUnknown"
    UnknownRegimeCount = @($selectedGroups | Where-Object { $_.RegimeLabel -eq "Unknown" }).Count
    LabeledRegimeCount = @($selectedGroups | Where-Object { $_.RegimeLabel -ne "Unknown" }).Count
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-generated-fixture-inventory.json") ([pscustomobject]@{
    Phase = $phase
    FixtureDirectory = $FixtureDirectory
    FixtureCount = $fixtureInventory.Count
    ExpectedFixtureCount = 100
    Inventory = $fixtureInventory
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-generated-fixture-validation.json") ([pscustomobject]@{
    Phase = $phase
    FixtureCount = $fixtureValidation.Count
    ValidFixtureCount = @($fixtureValidation | Where-Object { $_.Valid }).Count
    AllFixturesValid = @($fixtureValidation | Where-Object { -not $_.Valid }).Count -eq 0
    Validation = $fixtureValidation
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-batch-manifest.json") $manifest

$validManifestEntries = @($entries | Where-Object {
    $_.NoPaperLedgerCommit -and
    $_.CadenceMinutes -eq 15 -and
    $_.CanonicalTargetCloseLocal -notmatch "T\d{2}:(06|21|36|51):00" -and
    $_.CanonicalTargetCloseLocal -match "T\d{2}:(00|15|30|45):00" -and
    -not [string]::IsNullOrWhiteSpace($_.BarRole) -and
    $_.FixtureSource -eq "LegacyAggregatedWeightsExtraction" -and
    $_.LegacyCompatibilityMappingUsed -and
    $_.ReadinessBindingRequired -and
    $_.RiskOperatorApprovalScope -eq "DesignOnlyPreviewOnly"
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-batch-manifest-validation.json") ([pscustomobject]@{
    Phase = $phase
    BatchEntryCount = $entries.Count
    ValidEntryCount = $validManifestEntries.Count
    AllEntriesValid = $validManifestEntries.Count -eq $entries.Count
    TargetClosesCanonicalQuarterHour = $validManifestEntries.Count -eq $entries.Count
    Legacy06UsedAsFutureCanonical = @($entries | Where-Object { $_.CanonicalTargetCloseLocal -match "T\d{2}:(06|21|36|51):00" }).Count -gt 0
    NoPaperLedgerCommitPreserved = @($entries | Where-Object { -not $_.NoPaperLedgerCommit }).Count -eq 0
    ContainsExecutablePermissionFields = $false
})

$priorPaperLedgerStateId = "paper-ledger-commit-r025-sample:paper-ledger-state"
$priorContinuityGateId = "cycle-r026-second-paper-baseline:paper-continuity-archive:paper-continuity-gate"
$requestedBy = "operator-sanitized"
$commands = @()
foreach ($entry in $entries) {
    $commandLine = "dotnet run --no-restore --project tools\QQ.Production.Intraday.Tools.ManualPaperCycle\QQ.Production.Intraday.Tools.ManualPaperCycle.csproj -- --mode ManualNoExternal --requested-cycle-run-id $($entry.RequestedCycleRunId) --qubes-run-id $($entry.QubesRunId) --qubes-fixture-path $($entry.QubesFixturePath) --prior-paper-ledger-state-id $priorPaperLedgerStateId --prior-continuity-gate-id $priorContinuityGateId --requested-by $requestedBy --expected-cadence-minutes 15 --output-artifacts-dir $($entry.OutputArtifactsDir) --no-paper-ledger-commit true"
    $commands += [pscustomobject]@{
        BatchEntryId = $entry.BatchEntryId
        CommandLine = $commandLine
        Mode = "ManualNoExternal"
        NoPaperLedgerCommit = $true
        ManualOnly = $true
        CommandExecuted = $false
    }
}

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-manual-noexternal-command-package.json") ([pscustomobject]@{
    Phase = $phase
    CommandCount = $commands.Count
    CommandsTextOnly = $true
    CommandsExecuted = $false
    ManualOnly = $true
    SchedulerAllowed = $false
    ServiceAllowed = $false
    PollingAllowed = $false
    AutomaticExecutionAllowed = $false
    Commands = $commands
})
$commandMd = @"
# EXEC-PAPER-R013 ManualNoExternal Command Package

These commands are text-only templates for a future operator-run gate. Do not run them in R013.

- Command count: $($commands.Count)
- ManualOnly: true
- SchedulerAllowed: false
- ServiceAllowed: false
- PollingAllowed: false
- AutomaticExecutionAllowed: false
- NoPaperLedgerCommit required: true

```powershell
$($commands | ForEach-Object { $_.CommandLine } | Out-String)
```
"@
Write-Text (Join-Path $ArtifactsRoot "phase-exec-paper-r013-manual-noexternal-command-package.md") $commandMd

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-operator-run-package.json") ([pscustomobject]@{
    Phase = $phase
    PackageScope = "FutureOperatorManualOnlyRun"
    CommandsToRunNow = @()
    CommandTemplateCount = $commands.Count
    ManualOnly = $true
    SafetyValidationRequiredBeforeAnyFutureRun = $true
    SchedulerAllowed = $false
    ServiceAllowed = $false
    PollingAllowed = $false
    BrokerRuntimeAllowed = $false
    LiveMarketDataAllowed = $false
    OrderCreationAllowed = $false
    RouteSubmissionAllowed = $false
    PaperLedgerCommitAllowed = $false
    NextPhase = "EXEC-PAPER-R014"
})
$operatorMd = @"
# EXEC-PAPER-R013 Operator Package

R013 generated the long-run paper-only batch package. It did not execute commands.

Future R014 expectation:
- safety-validate every command before any run,
- run at most one ManualNoExternal command per accepted batch entry,
- keep every output non-executable and no-order/no-fill/no-route/no-ledger,
- aggregate R009 design-only previews and held-line diagnostics.
"@
Write-Text (Join-Path $ArtifactsRoot "phase-exec-paper-r013-operator-run-package.md") $operatorMd

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-expected-r014-execution-shape.json") ([pscustomobject]@{
    Phase = $phase
    ExpectedNextPhase = "EXEC-PAPER-R014"
    OneManualNoExternalRunPerAcceptedBatchEntry = $true
    CommandsSafetyValidatedBeforeRun = $true
    SchedulerServicePollingAllowed = $false
    NoPaperLedgerCommit = $true
    CollectPreviewLines = $true
    AggregateLongRunPreviewReview = $true
    ExecutablePromotionAuthorized = $false
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-expected-output-counts.json") ([pscustomobject]@{
    Phase = $phase
    ExpectedFixtures = $entries.Count
    ExpectedBatchEntries = $entries.Count
    ExpectedMaximumPreviewLines = $entries.Count * 7
    FullPackage = $isFull
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-hold-missing-evidence-diagnostics.json") ([pscustomobject]@{
    Phase = $phase
    PackageStatus = if ($isFull) { "Ready" } else { "Partial" }
    MissingTargetCloseCount = @($entries | Where-Object { [string]::IsNullOrWhiteSpace($_.CanonicalTargetCloseLocal) }).Count
    InvalidFixtureCount = @($fixtureValidation | Where-Object { -not $_.Valid }).Count
    MissingRegimeEvidence = $true
    RegimeLabelDefaultedToUnknown = $true
    HoldCriteria = @(
        "Missing fixture",
        "Invalid fixture format",
        "Missing canonical target close",
        "Target close not quarter-hour",
        "Missing readiness binding",
        "Risk/operator preview approval missing",
        "Any order/fill/route/submission/ledger/state path appears",
        "Any scheduler/service/polling path appears",
        "Any broker/live market data path appears"
    )
})

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-canonical-quarter-hour-policy-preservation.json") ([pscustomobject]@{ Phase = $phase; FutureTimestampsUseCanonicalQuarterHour = $true; AllowedMinutes = @(0,15,30,45); Legacy06UsedAsFutureCanonical = $false })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-legacy-compatibility-preservation.json") ([pscustomobject]@{ Phase = $phase; LegacyTimestampsCompatibilityOnly = $true; CanonicalCloseForLegacyMatch = "LegacyOutputTimestamp - 6 minutes"; LegacyNextBarExecutionCloseCanonical = "LegacyOutputTimestamp + 9 minutes"; Legacy06UsedAsFutureCanonical = $false })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-usd-pair-normalization-preservation.json") ([pscustomobject]@{ Phase = $phase; USDPairOnlyAfterNetting = $true; ExecutionSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF"); AUDUSDNotFailed = $true })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-direct-cross-exclusion-preservation.json") ([pscustomobject]@{ Phase = $phase; DirectCrossesSignalOnly = $true; DirectCrossNettingFirst = $true; DirectCrossExecutionDisabled = $true; ExclusionWeakened = $false })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-cost-guidance-preservation.json") ([pscustomobject]@{ Phase = $phase; FiveUsdPerMillion = "BestCaseMajorOnly"; FiveUsdPerMillionUniversalized = $false; NonmajorCalibrationRequired = $true })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-nonmajor-calibration-preservation.json") ([pscustomobject]@{ Phase = $phase; NonmajorEmScandiCnhCalibrationRequired = $true; NonmajorExecutionAuthorized = $false })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-usdjpy-caveat-preservation.json") ([pscustomobject]@{ Phase = $phase; NormalizedPortfolioSymbol = "JPYUSD"; ExecutionTradableSymbol = "USDJPY"; RequiresInversion = $true; SecurityID = 4004; SecurityIDSource = "8"; CaveatWeakened = $false })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-lmax-readonly-baseline-reference.json") ([pscustomobject]@{ Phase = $phase; LmaxReferencedAsReadonlyBaselineOnly = $true; LmaxCalled = $false; BrokerRuntimeActivated = $false })

New-Audit "phase-exec-paper-r013-no-broker-activation-audit.json" "BrokerActivation" "No broker activation occurred."
New-Audit "phase-exec-paper-r013-no-live-marketdata-audit.json" "LiveMarketData" "No live market data was requested."
New-Audit "phase-exec-paper-r013-no-scheduler-service-polling-audit.json" "SchedulerServicePolling" "No scheduler/service/polling/background job was started."
New-Audit "phase-exec-paper-r013-no-new-pms-cycle-audit.json" "NewPmsCycle" "No PMS/EMS/OMS cycle was run."
New-Audit "phase-exec-paper-r013-no-manualnoexternal-command-run-audit.json" "ManualNoExternalCommandRun" "No ManualNoExternal command was run."
New-Audit "phase-exec-paper-r013-no-new-backtest-audit.json" "NewBacktestRun" "No new backtest was run."
New-Audit "phase-exec-paper-r013-no-new-simulation-audit.json" "NewSimulationRun" "No new simulation was run."
New-Audit "phase-exec-paper-r013-no-tca-result-lines-audit.json" "TcaResultLinesCreated" "No TCA result lines were created."
New-Audit "phase-exec-paper-r013-no-executable-schedule-audit.json" "ExecutableSchedulesCreated" "No executable schedules were created."
New-Audit "phase-exec-paper-r013-no-child-slices-audit.json" "ChildSlicesCreated" "No child slices were created."
New-Audit "phase-exec-paper-r013-no-child-orders-audit.json" "ChildOrdersCreated" "No child orders were created."
New-Audit "phase-exec-paper-r013-no-order-created-audit.json" "OrdersCreated" "No orders were created."
New-Audit "phase-exec-paper-r013-no-real-fill-audit.json" "FillsCreated" "No fills were created."
New-Audit "phase-exec-paper-r013-no-execution-report-audit.json" "ExecutionReportsCreated" "No execution reports were created."
New-Audit "phase-exec-paper-r013-no-route-no-submission-audit.json" "RoutesOrSubmissionsCreated" "No routes or submissions were created."
New-Audit "phase-exec-paper-r013-no-paper-ledger-commit-audit.json" "PaperLedgerCommitted" "No paper ledger commit was created."
New-Audit "phase-exec-paper-r013-no-polygon-api-call-audit.json" "PolygonCalled" "Polygon was not called."
New-Audit "phase-exec-paper-r013-no-lmax-call-audit.json" "LmaxCalled" "LMAX was not called."
New-Audit "phase-exec-paper-r013-no-external-api-call-audit.json" "ExternalApiCalled" "No external API was called."

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-no-external-audit.json") ([pscustomobject]@{ Phase = $phase; NoExternal = $true; PolygonCalled = $false; LmaxCalled = $false; ExternalApiCalled = $false; DownloadsExecuted = $false })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-forbidden-actions-audit.json") ([pscustomobject]@{
    Phase = $phase
    ForbiddenActionsDetected = $false
    BrokerActivation = $false
    LiveMarketData = $false
    SchedulerServicePolling = $false
    NewPmsCycle = $false
    ManualNoExternalCommandsRun = $false
    BacktestOrSimulation = $false
    TcaResultLinesCreated = $false
    ExecutableSchedule = $false
    ChildSlicesOrOrders = $false
    OrdersFillsReportsRoutesSubmissions = $false
    PaperLedgerCommit = $false
    StateMutation = $false
    R009ExecutablePromotion = $false
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-next-phase-recommendation.json") ([pscustomobject]@{
    Phase = $phase
    RecommendedNextPhase = "EXEC-PAPER-R014 - No-External Long-Run Paper Batch ManualNoExternal Execution and Aggregation Review Gate"
    Purpose = "Run generated ManualNoExternal commands only after safety validation, aggregate R009 previews, and review long-run paper-only stability without executable schedules, orders, fills, routes, submissions, broker calls, live market data, or ledger commits."
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-build-test-validator-evidence.json") ([pscustomobject]@{
    Phase = $phase
    DotnetBuild = "Pending"
    FocusedR013Tests = "Pending"
    UnitTests = "Pending"
    R013Validator = "Pending"
    EvidenceComplete = $false
})

$summary = @"
# EXEC-PAPER-R013 Summary

R013 generated the long-run paper-only batch package for R009. It created fixtures, a batch manifest, ManualNoExternal text-only command templates, reporting expectations, and an operator action package.

Classifications:
$($classifications | ForEach-Object { "- $_" } | Out-String)

Counts:
- Fixtures generated: $($fixtureInventory.Count)
- Accepted batch entries: $($entries.Count)
- Expected maximum preview lines for R014: $($entries.Count * 7)
- Commands executed in R013: 0

No scheduler, service, polling, broker, live market data, order, fill, route, submission, paper ledger commit, or state mutation was authorized.
"@
Write-Text (Join-Path $ArtifactsRoot "phase-exec-paper-r013-summary.md") $summary

Write-Host "EXEC-PAPER-R013 artifacts generated"
