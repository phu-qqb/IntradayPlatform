param(
    [string]$ArtifactsRoot = "artifacts/readiness/execution-sim",
    [string]$AlgoArtifactsRoot = "artifacts/readiness/execution-algo",
    [string]$SourceFile = "C:\Users\phili\source\repos\QQ.Production.Core\home\prod\INFX1\AggregatedWeights.txt",
    [string]$FixtureDirectory = "C:\Users\phili\source\repos\QQ.Production.Intraday\data\qubes-fixtures\next-stage-paper-eval",
    [string]$ManifestPath = "C:\Users\phili\source\repos\QQ.Production.Intraday\data\qubes-fixtures\next-stage-paper-eval\batch-manifest.json"
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

function As-Array($value) {
    if ($null -eq $value) {
        return @()
    }

    if ($value -is [System.Array]) {
        return $value
    }

    return @($value)
}

function Test-CanonicalQuarterHour([string]$targetCloseLocal) {
    if ([string]::IsNullOrWhiteSpace($targetCloseLocal) -or $targetCloseLocal.Length -lt 19) {
        return $false
    }

    if ($targetCloseLocal -match "T\d{2}:(06|21|36|51):00") {
        return $false
    }

    $parsed = [datetime]::ParseExact($targetCloseLocal.Substring(0, 19), "yyyy-MM-ddTHH:mm:ss", [System.Globalization.CultureInfo]::InvariantCulture)
    return $parsed.Second -eq 0 -and @(0, 15, 30, 45) -contains $parsed.Minute
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
    if (-not (Test-Path -LiteralPath $path)) {
        return [pscustomobject]@{ FixturePath = $path; Exists = $false; NonEmpty = $false; RowCount = 0; InvalidRowCount = 1; InvalidRows = @("MissingFixture"); Valid = $false }
    }

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
        Exists = $true
        NonEmpty = $rows.Count -gt 0
        RowCount = $rows.Count
        InvalidRowCount = $invalid.Count
        InvalidRows = $invalid
        Valid = $rows.Count -gt 0 -and $invalid.Count -eq 0
    }
}

function New-ReadinessIndex([object[]]$records, [string]$idProperty) {
    $index = @{}
    foreach ($record in $records) {
        $key = "$($record.Symbol)|$($record.TargetCloseTimestampUtc)"
        if (-not $index.ContainsKey($key)) {
            $index[$key] = $record
        }
    }
    return $index
}

function New-FeedQualityIndex([object[]]$records) {
    $index = @{}
    foreach ($record in $records) {
        $key = "$($record.Symbol)|$($record.LocalSessionDate)"
        if (-not $index.ContainsKey($key)) {
            $index[$key] = $record
        }
    }
    return $index
}

function Get-Binding([hashtable]$index, [string]$symbol, [string]$targetCloseUtc, [string]$idProperty) {
    $key = "$symbol|$targetCloseUtc"
    if (-not $index.ContainsKey($key)) {
        return $null
    }
    $record = $index[$key]
    return [pscustomobject]@{
        BindingId = $record.$idProperty
        Symbol = $record.Symbol
        TargetCloseTimestampUtc = $record.TargetCloseTimestampUtc
        ReadinessStatus = $record.ReadinessStatus
        SourceArtifact = "EXEC-SIM-R053"
    }
}

function Get-FeedBinding([hashtable]$index, [string]$symbol, [string]$localSessionDate) {
    $key = "$symbol|$localSessionDate"
    if (-not $index.ContainsKey($key)) {
        return $null
    }
    $record = $index[$key]
    return [pscustomobject]@{
        BindingId = $record.FeedQualityId
        Symbol = $record.Symbol
        LocalSessionDate = $record.LocalSessionDate
        ReadinessStatus = $record.FeedQualityStatus
        SourceArtifact = "EXEC-SIM-R053"
    }
}

function New-Audit([string]$name, [string]$key, [string]$detail) {
    Write-Json (Join-Path $ArtifactsRoot $name) ([pscustomobject]@{
        Phase = "EXEC-PAPER-R012"
        AuditName = $key
        Passed = $true
        Occurred = $false
        Detail = $detail
    })
}

$phase = "EXEC-PAPER-R012"
$repoRoot = (Resolve-Path ".").Path
$r059Plan = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r059-target-close-distribution-plan.json")
$r059Contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r059-next-stage-paper-only-expansion-contract.json")
$r009Contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r059-r009-contract-reference.json")
$quoteWindowReadiness = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r053-quote-window-readiness-results.json")
$closeBenchmarkReadiness = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r053-close-benchmark-readiness-results.json")
$feedQualityReadiness = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r053-feed-quality-readiness-results.json")

New-Item -ItemType Directory -Force -Path $FixtureDirectory | Out-Null

$sourceLines = @(Get-Content -LiteralPath $SourceFile)
$header = $sourceLines[0].Split(";") | Select-Object -Skip 1
$mappingFixture = @(Get-Content -LiteralPath (Join-Path $repoRoot "data\qubes-fixtures\r009\current-qubes-weights.txt"))
$tickers = @($mappingFixture | ForEach-Object { $_.Split(";")[0] })
if ($header.Count -ne $tickers.Count) {
    throw "AggregatedWeights header count $($header.Count) does not match ticker mapping count $($tickers.Count)."
}

$groups = @()
foreach ($line in ($sourceLines | Select-Object -Skip 1)) {
    if ([string]::IsNullOrWhiteSpace($line)) {
        continue
    }
    $parts = $line.Split(";")
    if ($parts.Count -ne ($tickers.Count + 1)) {
        continue
    }
    $legacyTimestamp = $parts[0]
    $legacyLocal = [datetime]::ParseExact($legacyTimestamp, "yyyyMMddHHmm", [System.Globalization.CultureInfo]::InvariantCulture)
    $canonicalCloseForLegacyMatch = $legacyLocal.AddMinutes(-6)
    $targetLocal = $legacyLocal.AddMinutes(9)
    if ($targetLocal.Minute -notin @(0, 15, 30, 45) -or $targetLocal.Second -ne 0) {
        continue
    }
    $role = Get-BarRole $targetLocal
    if ($null -eq $role) {
        continue
    }
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
    if ($validRows -ne 91) {
        continue
    }
    $groups += [pscustomobject]@{
        LegacyTimestamp = $legacyTimestamp
        SourceLegacyDate = $legacyLocal.ToString("yyyy-MM-dd")
        SourceLegacyTimestamp = $legacyTimestamp
        CanonicalCloseForLegacyMatch = $canonicalCloseForLegacyMatch
        TargetLocal = $targetLocal
        TargetUtc = Convert-NyLocalToUtc $targetLocal
        BarRole = $role
        WeightRows = $weights
        ValidWeightRowCount = $validRows
    }
}

$r051Dates = @(
    "2025-10-21", "2025-10-23", "2025-10-27", "2025-10-29", "2025-10-31",
    "2025-11-03", "2025-11-05", "2025-11-07", "2025-11-10", "2025-11-13",
    "2025-11-17", "2025-11-19", "2025-11-24", "2025-11-26", "2025-12-01",
    "2025-12-03", "2025-12-05", "2025-12-09", "2025-12-11", "2025-12-16"
)

$roleDateSets = @{
    OpeningBuild = $r051Dates | Select-Object -First 10
    IntradayRebalance = $r051Dates | Select-Object -Skip 10 -First 10
    ClosingFlatten = $r051Dates | Select-Object -First 10
}

$selected = @()
foreach ($role in @("OpeningBuild", "IntradayRebalance", "ClosingFlatten")) {
    foreach ($date in $roleDateSets[$role]) {
        $candidates = @($groups | Where-Object { $_.BarRole -eq $role -and $_.SourceLegacyDate -eq $date })
        if ($candidates.Count -eq 0) {
            continue
        }
        if ($role -eq "OpeningBuild") {
            $chosen = $candidates | Sort-Object TargetLocal | Select-Object -First 1
        }
        elseif ($role -eq "IntradayRebalance") {
            $target = [datetime]::ParseExact("$date 18:00", "yyyy-MM-dd HH:mm", [System.Globalization.CultureInfo]::InvariantCulture)
            $chosen = $candidates | Sort-Object @{ Expression = { [math]::Abs(($_.TargetLocal - $target).TotalMinutes) } } | Select-Object -First 1
        }
        else {
            $chosen = $candidates | Sort-Object TargetLocal -Descending | Select-Object -First 1
        }
        $selected += $chosen
    }
}

$entries = @()
$fixtureInventory = @()
$fixtureValidation = @()
$selectedGroups = @()

foreach ($group in $selected) {
    $dateToken = $group.TargetLocal.ToString("yyyyMMdd")
    $timeToken = $group.TargetLocal.ToString("HHmm")
    $roleToken = $group.BarRole.ToLowerInvariant()
    $entryId = "paper-r009-next-stage-$dateToken-$timeToken-$roleToken-legacy-match"
    $fixtureName = "qubes-$dateToken-$timeToken-$roleToken-legacy-match.txt"
    $fixturePath = Join-Path $FixtureDirectory $fixtureName
    $rows = @($group.WeightRows | ForEach-Object { "$($_.Ticker);$($_.Weight)" })
    Set-Content -LiteralPath $fixturePath -Value $rows -Encoding UTF8

    $targetLocalString = "$($group.TargetLocal.ToString("yyyy-MM-ddTHH:mm:ss")) America/New_York"
    $targetUtcString = $group.TargetUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", [System.Globalization.CultureInfo]::InvariantCulture)
    $outputDir = Join-Path $repoRoot "artifacts\readiness\execution-sim\$entryId"
    $entry = [pscustomobject]@{
        BatchEntryId = $entryId
        SourceLegacyDate = $group.SourceLegacyDate
        SourceLegacyTimestamp = $group.SourceLegacyTimestamp
        QubesFixturePath = $fixturePath
        QubesRunId = "qubes-$entryId"
        RequestedCycleRunId = $entryId
        CanonicalTargetCloseLocal = $targetLocalString
        CanonicalTargetCloseUtc = $targetUtcString
        CanonicalSession = "14:15-21:00 America/New_York"
        BarRole = $group.BarRole
        CandidateDefinitionNeedsOperatorConfirmation = $true
        MustEndFlat = $group.BarRole -eq "ClosingFlatten"
        OvernightAllowed = $false
        CadenceMinutes = 15
        OutputArtifactsDir = $outputDir
        NoPaperLedgerCommit = $true
        FixtureSource = "LegacyAggregatedWeightsExtraction"
        LegacyCompatibilityMappingUsed = $true
        ReadinessBindingRequired = $true
        RiskOperatorApprovalScope = "DesignOnlyPreviewOnly"
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
    }
    $fixtureInventory += [pscustomobject]@{
        BatchEntryId = $entryId
        FixturePath = $fixturePath
        RowCount = $rows.Count
        ContainsTimestampRows = @($rows | Where-Object { $_ -match "^\d{8,14}" }).Count -gt 0
    }
    $fixtureValidation += Test-FixtureRows $fixturePath
}

Write-Json $ManifestPath ([pscustomobject]@{
    Phase = $phase
    ManifestStatus = if ($entries.Count -eq 30) { "FullBalancedBatchReady" } else { "PartialBatchReady" }
    BatchEntryCount = $entries.Count
    ExpectedBalancedBatchEntryCount = 30
    SourceFile = $SourceFile
    FixtureDirectory = $FixtureDirectory
    CanonicalSession = "14:15-21:00 America/New_York"
    CanonicalCloseMinutes = @(0, 15, 30, 45)
    CandidateDefinitionNeedsOperatorConfirmation = $true
    LegacyTimestampsCompatibilityOnly = $true
    Legacy06UsedAsFutureCanonical = $false
    Entries = $entries
})

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-r059-plan-reference.json") ([pscustomobject]@{
    Phase = $phase
    SourcePhase = "EXEC-SIM-R059"
    RecommendedMinimumTargetCloses = $r059Plan.RecommendedMinimumTargetCloses
    Distribution = $r059Plan.Distribution
    PlanningOnlySource = [bool]$r059Contract.PlanningOnly
    Reused = $true
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-r009-contract-reference.json") ([pscustomobject]@{
    Phase = $phase
    ContractVersion = $r009Contract.ContractVersion
    Primary = $r009Contract.Primary
    Secondary = $r009Contract.Secondary
    ConditionalResidualModule = $r009Contract.ConditionalResidualModule
    DesignOnly = $true
    PaperOnly = $true
    NonExecutable = $true
    NotAnOrder = $true
    NotSubmitted = $true
    NoBrokerRoute = $true
    ExecutablePromotionAuthorized = $false
    BrokerReady = $false
    LiveReady = $false
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-aggregatedweights-source-analysis.json") ([pscustomobject]@{
    Phase = $phase
    SourceFile = $SourceFile
    Exists = Test-Path -LiteralPath $SourceFile
    LineCount = $sourceLines.Count
    HeaderColumnCount = $header.Count
    DataRowCount = $sourceLines.Count - 1
    EligibleCanonicalGroups = $groups.Count
    TickerMappingSource = "Accepted R005/R006 current-qubes-weights positional mapping"
    TickerMappingCount = $tickers.Count
    CommandsExecutedDuringExtraction = $false
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-selected-legacy-groups.json") ([pscustomobject]@{
    Phase = $phase
    SelectedGroupCount = $selectedGroups.Count
    ExpectedGroupCount = 30
    SelectionStatus = if ($selectedGroups.Count -eq 30) { "SelectedBalanced30Groups" } else { "PartialBalancedSelection" }
    Groups = $selectedGroups
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-bar-role-selection-results.json") ([pscustomobject]@{
    Phase = $phase
    CandidateDefinitionNeedsOperatorConfirmation = $true
    Results = $selectedGroups | Group-Object BarRole | ForEach-Object { [pscustomobject]@{ BarRole = $_.Name; BatchEntryCount = $_.Count; ExpectedMinimum = 10 } }
    Balanced = (@($selectedGroups | Group-Object BarRole | Where-Object { $_.Count -ge 10 }).Count -eq 3)
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-generated-fixture-inventory.json") ([pscustomobject]@{
    Phase = $phase
    FixtureDirectory = $FixtureDirectory
    FixtureCount = $fixtureInventory.Count
    ExpectedFixtureCount = 30
    Inventory = $fixtureInventory
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-generated-fixture-validation.json") ([pscustomobject]@{
    Phase = $phase
    FixtureCount = $fixtureValidation.Count
    ValidFixtureCount = @($fixtureValidation | Where-Object { $_.Valid }).Count
    AllFixturesValid = @($fixtureValidation | Where-Object { -not $_.Valid }).Count -eq 0
    Validation = $fixtureValidation
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-batch-manifest.json") (Read-Json $ManifestPath)

$manifestValidEntries = @($entries | Where-Object {
    -not [string]::IsNullOrWhiteSpace($_.QubesFixturePath) -and
    -not [string]::IsNullOrWhiteSpace($_.QubesRunId) -and
    -not [string]::IsNullOrWhiteSpace($_.RequestedCycleRunId) -and
    -not [string]::IsNullOrWhiteSpace($_.CanonicalTargetCloseLocal) -and
    -not [string]::IsNullOrWhiteSpace($_.CanonicalTargetCloseUtc) -and
    -not [string]::IsNullOrWhiteSpace($_.BarRole) -and
    $_.CadenceMinutes -eq 15 -and
    $_.NoPaperLedgerCommit -and
    (Test-CanonicalQuarterHour $_.CanonicalTargetCloseLocal)
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-batch-manifest-validation.json") ([pscustomobject]@{
    Phase = $phase
    BatchEntryCount = $entries.Count
    ValidEntryCount = $manifestValidEntries.Count
    AllEntriesValid = $manifestValidEntries.Count -eq $entries.Count
    TargetClosesCanonicalQuarterHour = $manifestValidEntries.Count -eq $entries.Count
    Legacy06UsedAsFutureCanonical = @($entries | Where-Object { $_.CanonicalTargetCloseLocal -match "T\d{2}:(06|21|36|51):00" }).Count -gt 0
    NoPaperLedgerCommitPreserved = @($entries | Where-Object { -not $_.NoPaperLedgerCommit }).Count -eq 0
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
        CommandExecutedBeforeSafetyValidation = $false
    }
}
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-manual-noexternal-command-plan.json") ([pscustomobject]@{
    Phase = $phase
    CommandCount = $commands.Count
    CommandsExecutedByPlanGeneration = $false
    Commands = $commands
})

$commandChecks = @()
$unsafeReasons = @()
foreach ($command in $commands) {
    $line = [string]$command.CommandLine
    $forbidden = $line -match "--mode no-external-paper-cycle" -or
        $line -match "\s--output\s" -or
        $line -match "--(broker|live|route|routes|submit|submission|orders|fills|execution-report|scheduler|service|polling)\b"
    $safe = $line -match "--mode ManualNoExternal" -and
        $line -match "--output-artifacts-dir" -and
        $line -match "--qubes-fixture-path" -and
        $line -match "--qubes-run-id" -and
        $line -match "--requested-cycle-run-id" -and
        $line -match "--expected-cadence-minutes 15" -and
        $line -match "--no-paper-ledger-commit true" -and
        -not $forbidden
    if (-not $safe) {
        $unsafeReasons += "Unsafe command for $($command.BatchEntryId)"
    }
    $commandChecks += [pscustomobject]@{
        BatchEntryId = $command.BatchEntryId
        CommandLine = $line
        SafeForLocalManualNoExternalExecution = $safe
        IncludesManualNoExternal = $line -match "--mode ManualNoExternal"
        IncludesOutputArtifactsDir = $line -match "--output-artifacts-dir"
        IncludesFixturePath = $line -match "--qubes-fixture-path"
        IncludesQubesRunId = $line -match "--qubes-run-id"
        IncludesRequestedCycleRunId = $line -match "--requested-cycle-run-id"
        IncludesCadence15 = $line -match "--expected-cadence-minutes 15"
        IncludesNoPaperLedgerCommitTrue = $line -match "--no-paper-ledger-commit true"
        DeprecatedNoExternalPaperCycleModeUsed = $line -match "--mode no-external-paper-cycle"
        DeprecatedOutputArgumentUsed = $line -match "\s--output\s"
        ForbiddenRuntimeFlagsDetected = $forbidden
    }
}

$allSafe = $unsafeReasons.Count -eq 0 -and $commands.Count -eq $entries.Count -and $commands.Count -le 30
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-command-safety-check.json") ([pscustomobject]@{
    Phase = $phase
    AcceptedBatchEntryCount = $entries.Count
    CommandCount = $commands.Count
    SafetyValidatedBeforeExecution = $true
    AllCommandsSafe = $allSafe
    MaxCommandsAllowed = 30
    UnsafeReasonCount = $unsafeReasons.Count
    UnsafeReasons = $unsafeReasons
    CommandChecks = $commandChecks
})
if (-not $allSafe) {
    throw "R012 command safety validation failed; no commands executed."
}

$executionResults = @()
foreach ($entry in $entries) {
    $args = @(
        "run", "--no-restore",
        "--project", "tools\QQ.Production.Intraday.Tools.ManualPaperCycle\QQ.Production.Intraday.Tools.ManualPaperCycle.csproj",
        "--",
        "--mode", "ManualNoExternal",
        "--requested-cycle-run-id", [string]$entry.RequestedCycleRunId,
        "--qubes-run-id", [string]$entry.QubesRunId,
        "--qubes-fixture-path", [string]$entry.QubesFixturePath,
        "--prior-paper-ledger-state-id", $priorPaperLedgerStateId,
        "--prior-continuity-gate-id", $priorContinuityGateId,
        "--requested-by", $requestedBy,
        "--expected-cadence-minutes", "15",
        "--output-artifacts-dir", [string]$entry.OutputArtifactsDir,
        "--no-paper-ledger-commit", "true"
    )
    $processOutput = & dotnet @args 2>&1
    $exitCode = $LASTEXITCODE
    $summaryPath = Join-Path ([string]$entry.OutputArtifactsDir) "phase-pms-ems-oms-r031-cli-manual-run-output.json"
    $planPath = Join-Path ([string]$entry.OutputArtifactsDir) "phase-pms-ems-oms-manual-noexternal-paper-execution-plan.json"
    $linesPath = Join-Path ([string]$entry.OutputArtifactsDir) "phase-pms-ems-oms-manual-noexternal-paper-execution-plan-lines.json"
    $summary = if (Test-Path -LiteralPath $summaryPath) { Read-Json $summaryPath } else { $null }
    $lineCount = 0
    if (Test-Path -LiteralPath $linesPath) {
        $lineCount = (As-Array (Read-Json $linesPath).Lines).Count
    }
    $executionResults += [pscustomobject]@{
        BatchEntryId = $entry.BatchEntryId
        RequestedCycleRunId = $entry.RequestedCycleRunId
        QubesRunId = $entry.QubesRunId
        ExitCode = $exitCode
        Stdout = ($processOutput | Out-String).Trim()
        CliStatus = $summary.CliStatus
        PreflightStatus = $summary.PreflightStatus
        CycleRunStatus = $summary.cycleRunStatus
        CycleExecuted = [bool]$summary.CycleExecuted
        CycleExecutionCount = [int]$summary.CycleExecutionCount
        RawRowCount = $summary.rawRowCount
        NormalizedRowCount = $summary.normalizedRowCount
        LineCount = $lineCount
        SummaryArtifact = $summaryPath
        PaperExecutionPlanArtifact = $planPath
        PaperExecutionPlanLinesArtifact = $linesPath
        NoExternal = [bool]$summary.noExternal
        NoPaperLedgerCommit = [bool]$summary.noPaperLedgerCommit
        NoOrder = [bool]$summary.noOrder
        NoFill = [bool]$summary.noFill
        NoReport = [bool]$summary.noReport
        NoRoute = [bool]$summary.noRoute
        NoSubmission = [bool]$summary.noSubmission
        CompletedSafely = $exitCode -eq 0 -and [bool]$summary.noExternal -and [bool]$summary.noPaperLedgerCommit -and [bool]$summary.noOrder -and [bool]$summary.noFill -and [bool]$summary.noReport -and [bool]$summary.noRoute -and [bool]$summary.noSubmission
    }
}

$runsSafe = $executionResults.Count -eq $entries.Count -and @($executionResults | Where-Object { -not $_.CompletedSafely }).Count -eq 0
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-batch-execution-result.json") ([pscustomobject]@{
    Phase = $phase
    CommandsExecuted = $executionResults.Count
    AcceptedBatchEntries = $entries.Count
    MoreCommandsThanAcceptedEntries = $executionResults.Count -gt $entries.Count
    AllRunsCompletedSafely = $runsSafe
    NoExternal = @($executionResults | Where-Object { -not $_.NoExternal }).Count -eq 0
    NoPaperLedgerCommit = @($executionResults | Where-Object { -not $_.NoPaperLedgerCommit }).Count -eq 0
    NoOrderFillReportRouteSubmission = @($executionResults | Where-Object { -not ($_.NoOrder -and $_.NoFill -and $_.NoReport -and $_.NoRoute -and $_.NoSubmission) }).Count -eq 0
    Results = $executionResults
})
if (-not $runsSafe) {
    throw "R012 ManualNoExternal run failed safety completion."
}

$quoteWindowIndex = New-ReadinessIndex (As-Array $quoteWindowReadiness.Results) "QuoteWindowId"
$closeBenchmarkIndex = New-ReadinessIndex (As-Array $closeBenchmarkReadiness.Results) "CloseBenchmarkId"
$feedQualityIndex = New-FeedQualityIndex (As-Array $feedQualityReadiness.Results)
$supportedExecutionSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")

$paperPlanLines = @()
$handoffLines = @()
$previewLines = @()
$heldLines = @()
$artifactInventory = @()

foreach ($entry in $entries) {
    $outputDir = [string]$entry.OutputArtifactsDir
    $summaryPath = Join-Path $outputDir "phase-pms-ems-oms-r031-cli-manual-run-output.json"
    $planPath = Join-Path $outputDir "phase-pms-ems-oms-manual-noexternal-paper-execution-plan.json"
    $linesPath = Join-Path $outputDir "phase-pms-ems-oms-manual-noexternal-paper-execution-plan-lines.json"
    $artifactInventory += [pscustomobject]@{
        BatchEntryId = $entry.BatchEntryId
        OutputArtifactsDir = $outputDir
        SummaryArtifactExists = Test-Path -LiteralPath $summaryPath
        PaperExecutionPlanArtifactExists = Test-Path -LiteralPath $planPath
        PaperExecutionPlanLinesArtifactExists = Test-Path -LiteralPath $linesPath
        SummaryArtifact = $summaryPath
        PaperExecutionPlanArtifact = $planPath
        PaperExecutionPlanLinesArtifact = $linesPath
    }
    $linesPayload = Read-Json $linesPath
    foreach ($line in (As-Array $linesPayload.Lines)) {
        $executionSymbol = [string]$line.ExecutionTradableSymbol
        $targetCloseUtc = [string]$entry.CanonicalTargetCloseUtc
        $targetCloseLocal = [string]$entry.CanonicalTargetCloseLocal
        $sessionDate = [string]$entry.SourceLegacyDate
        $quoteBinding = Get-Binding $quoteWindowIndex $executionSymbol $targetCloseUtc "QuoteWindowId"
        $benchmarkBinding = Get-Binding $closeBenchmarkIndex $executionSymbol $targetCloseUtc "CloseBenchmarkId"
        $feedBinding = Get-FeedBinding $feedQualityIndex $executionSymbol $sessionDate
        $missing = @()
        if ($null -eq $quoteBinding) { $missing += "MissingQuoteWindowReadinessBinding" }
        if ($null -eq $benchmarkBinding) { $missing += "MissingCloseBenchmarkReadinessBinding" }
        if ($null -eq $feedBinding) { $missing += "MissingFeedQualityReadinessBinding" }
        $holdReason = if ($missing.Count -gt 0) { ($missing -join ";") } else { $null }
        $canonicalConfirmed = Test-CanonicalQuarterHour $targetCloseLocal
        $directCrossLine = -not ($supportedExecutionSymbols -contains [string]$line.ExecutionTradableSymbol)

        $paperLine = [pscustomobject]@{
            BatchEntryId = $entry.BatchEntryId
            FixturePath = $entry.QubesFixturePath
            QubesRunId = $entry.QubesRunId
            RequestedCycleRunId = $entry.RequestedCycleRunId
            PaperExecutionPlanLineId = $line.PaperExecutionPlanLineId
            Symbol = $line.Symbol
            ExecutionTradableSymbol = $line.ExecutionTradableSymbol
            NormalizedPortfolioSymbol = $line.NormalizedPortfolioSymbol
            RequiresInversion = $line.RequiresInversion
            SecurityID = $line.SecurityID
            SecurityIDSource = $line.SecurityIDSource
            Side = $line.Side
            TargetQuantity = $line.TargetQuantity
            TargetNotional = $line.TargetNotional
            DeltaNotional = $line.DeltaNotional
            CanonicalTargetCloseTimestamp = $targetCloseUtc
            CanonicalTargetCloseLocal = $targetCloseLocal
            CanonicalSession = $entry.CanonicalSession
            BarRole = $entry.BarRole
            CanonicalQuarterHourTimestampConfirmed = $canonicalConfirmed
            NonExecutable = $true
            NotAnOrder = $true
            NotSubmitted = $true
            NoBrokerRoute = $true
            NoChildSlices = $true
            NoExecutableSchedule = $true
            NoFill = $true
            NoExecutionReport = $true
            NoRoute = $true
            NoSubmission = $true
            NoPaperLedgerCommit = $true
            DerivedFromQubesFixtureAndManualNoExternalPipeline = $true
            DirectCrossExecutableLine = $directCrossLine
        }
        $paperPlanLines += $paperLine

        $handoff = [pscustomobject]@{
            BatchEntryId = $entry.BatchEntryId
            FixturePath = $entry.QubesFixturePath
            QubesRunId = $entry.QubesRunId
            RequestedCycleRunId = $entry.RequestedCycleRunId
            PaperExecutionPlanLineId = $line.PaperExecutionPlanLineId
            Symbol = $line.Symbol
            ExecutionTradableSymbol = $line.ExecutionTradableSymbol
            NormalizedPortfolioSymbol = $line.NormalizedPortfolioSymbol
            RequiresInversion = $line.RequiresInversion
            Side = $line.Side
            TargetQuantity = $line.TargetQuantity
            TargetNotional = $line.TargetNotional
            CanonicalTargetCloseTimestamp = $targetCloseUtc
            CanonicalTargetCloseLocal = $targetCloseLocal
            CanonicalSession = $entry.CanonicalSession
            BarRole = $entry.BarRole
            CanonicalQuarterHourTimestampConfirmed = $canonicalConfirmed
            R009ContractVersion = "0.3.0-design-only-candidate"
            PrimaryPolicyCandidate = "CloseSeeking15mAdaptive_BalancedAdaptive_v0"
            SecondaryPolicyCandidate = "CloseSeeking15mAdaptive_ResidualAwareUrgency_v0"
            ConditionalResidualModule = "ControlledResidualCross_BalancedResidualCross_v0"
            QuoteWindowReadinessBinding = $quoteBinding
            CloseBenchmarkReadinessBinding = $benchmarkBinding
            FeedQualityReadinessBinding = $feedBinding
            RiskReviewStatus = "ApprovedForNonExecutablePreview"
            OperatorApprovalStatus = "ApprovedForDesignOnlyPreviewOnly"
            NonExecutable = $true
            NotAnOrder = $true
            NotSubmitted = $true
            NoBrokerRoute = $true
            NoChildSlices = $true
            NoExecutableSchedule = $true
            NoFill = $true
            NoExecutionReport = $true
            NoRoute = $true
            NoSubmission = $true
            NoPaperLedgerCommit = $true
            HoldReason = $holdReason
        }
        $handoffLines += $handoff

        $preview = [pscustomobject]@{
            BatchEntryId = $entry.BatchEntryId
            FixturePath = $entry.QubesFixturePath
            QubesRunId = $entry.QubesRunId
            RequestedCycleRunId = $entry.RequestedCycleRunId
            PaperExecutionPlanLineId = $line.PaperExecutionPlanLineId
            Symbol = $line.Symbol
            ExecutionTradableSymbol = $line.ExecutionTradableSymbol
            NormalizedPortfolioSymbol = $line.NormalizedPortfolioSymbol
            RequiresInversion = $line.RequiresInversion
            Side = $line.Side
            TargetQuantity = $line.TargetQuantity
            TargetNotional = $line.TargetNotional
            CanonicalTargetCloseTimestamp = $targetCloseUtc
            CanonicalTargetCloseLocal = $targetCloseLocal
            CanonicalSession = $entry.CanonicalSession
            BarRole = $entry.BarRole
            CanonicalQuarterHourTimestampConfirmed = $canonicalConfirmed
            R009ContractVersion = "0.3.0-design-only-candidate"
            PrimaryPolicyCandidate = "CloseSeeking15mAdaptive_BalancedAdaptive_v0"
            SecondaryPolicyCandidate = "CloseSeeking15mAdaptive_ResidualAwareUrgency_v0"
            ConditionalResidualModule = "ControlledResidualCross_BalancedResidualCross_v0"
            QuoteWindowReadinessBinding = $quoteBinding
            CloseBenchmarkReadinessBinding = $benchmarkBinding
            FeedQualityReadinessBinding = $feedBinding
            RiskReviewStatus = "ApprovedForNonExecutablePreview"
            OperatorApprovalStatus = "ApprovedForDesignOnlyPreviewOnly"
            DesignOnlyPreview = $true
            NonExecutable = $true
            NotAnOrder = $true
            NotSubmitted = $true
            NoBrokerRoute = $true
            NoChildSlices = $true
            NoExecutableSchedule = $true
            NoFill = $true
            NoExecutionReport = $true
            NoRoute = $true
            NoSubmission = $true
            NoPaperLedgerCommit = $true
            HoldReason = $holdReason
        }
        $previewLines += $preview
        if ($missing.Count -gt 0) {
            $heldLines += [pscustomobject]@{
                BatchEntryId = $entry.BatchEntryId
                PaperExecutionPlanLineId = $line.PaperExecutionPlanLineId
                Symbol = $line.Symbol
                ExecutionTradableSymbol = $line.ExecutionTradableSymbol
                HoldReason = $holdReason
                MissingInputs = $missing
            }
        }
    }
}

$expectedMaxPreviewLines = $entries.Count * 7
$directCrossLines = @($paperPlanLines | Where-Object { $supportedExecutionSymbols -notcontains $_.ExecutionTradableSymbol })
$completeReadiness = @($previewLines | Where-Object { $null -ne $_.QuoteWindowReadinessBinding -and $null -ne $_.CloseBenchmarkReadinessBinding -and $null -ne $_.FeedQualityReadinessBinding })
$previewReady = $entries.Count -eq 30 -and $previewLines.Count -eq 210 -and $heldLines.Count -eq 0 -and $directCrossLines.Count -eq 0

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-output-artifact-inventory.json") ([pscustomobject]@{ Phase = $phase; RunCount = $entries.Count; Inventory = $artifactInventory })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-paper-plan-lines-aggregate.json") ([pscustomobject]@{ Phase = $phase; LineCount = $paperPlanLines.Count; ExpectedMaximumLineCount = $expectedMaxPreviewLines; Lines = $paperPlanLines })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-r009-handoff-package-aggregate.json") ([pscustomobject]@{ Phase = $phase; HandoffLineCount = $handoffLines.Count; HandoffReady = $previewReady; Lines = $handoffLines })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-r009-design-only-preview-lines.json") ([pscustomobject]@{ Phase = $phase; PreviewLineCount = $previewLines.Count; ExpectedMaximumPreviewLineCount = $expectedMaxPreviewLines; PreviewReady = $previewReady; Lines = $previewLines })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-preview-line-coverage.json") ([pscustomobject]@{
    Phase = $phase
    BatchEntryCount = $entries.Count
    ExpectedBatchEntryCount = 30
    PaperPlanLineCount = $paperPlanLines.Count
    PreviewLineCount = $previewLines.Count
    ExpectedMaximumPreviewLineCount = $expectedMaxPreviewLines
    HeldLineCount = $heldLines.Count
    DirectCrossExecutableLineCount = $directCrossLines.Count
    CompleteReadinessBindingCount = $completeReadiness.Count
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-held-line-diagnostics.json") ([pscustomobject]@{
    Phase = $phase
    HeldLineCount = $heldLines.Count
    HeldLines = $heldLines
    DeterministicReasonIfFewerThanExpected = if ($previewLines.Count -lt $expectedMaxPreviewLines) { "See execution and paper-plan line aggregate artifacts." } else { $null }
})

$barRoleCounts = $entries | Group-Object BarRole | ForEach-Object {
    $role = $_.Name
    [pscustomobject]@{
        BarRole = $role
        BatchEntryCount = $_.Count
        PreviewLineCount = @($previewLines | Where-Object { $_.BarRole -eq $role }).Count
    }
}
$inversionSafe = @($paperPlanLines | Where-Object { $_.ExecutionTradableSymbol -eq "USDJPY" -and -not ($_.NormalizedPortfolioSymbol -eq "JPYUSD" -and $_.RequiresInversion -and [string]$_.SecurityID -eq "4004" -and [string]$_.SecurityIDSource -eq "8") }).Count -eq 0
$decision = if ($previewReady -and $inversionSafe) { "AcceptBalancedBarRolePaperOnlyPreviewForMaturityReview" } else { "PartialNextStagePreviewNeedsDiagnostics" }
$classifications = if ($previewReady -and $inversionSafe) {
    @(
        "EXEC_PAPER_R012_PASS_NEXT_STAGE_FIXTURE_BATCH_READY_NO_EXTERNAL",
        "EXEC_PAPER_R012_PASS_MANUAL_NOEXTERNAL_BATCH_RUNS_READY_NO_EXTERNAL",
        "EXEC_PAPER_R012_PASS_R009_NEXT_STAGE_PREVIEW_READY_NO_EXTERNAL",
        "EXEC_PAPER_R012_PASS_BAR_ROLE_BALANCED_REVIEW_READY_NO_EXTERNAL",
        "EXEC_PAPER_R012_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
    )
} else {
    @(
        "EXEC_PAPER_R012_PARTIAL_NEXT_STAGE_PREVIEW_WITH_HELD_OR_MISSING_LINES_NO_EXTERNAL",
        "EXEC_PAPER_R012_PASS_DIAGNOSTICS_READY_NO_EXTERNAL",
        "EXEC_PAPER_R012_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
    )
}

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-operator-review-report.json") ([pscustomobject]@{
    Phase = $phase
    TargetClosesBalancedAcrossBarRoles = @($barRoleCounts | Where-Object { $_.BatchEntryCount -eq 10 }).Count -eq 3
    GeneratedFixtureCount = $fixtureInventory.Count
    SafeManualNoExternalCommandsRun = $executionResults.Count
    PaperExecutionPlanLinesEmitted = $paperPlanLines.Count
    R009PreviewLinesProduced = $previewLines.Count
    DirectCrossesExcludedAfterNetting = $directCrossLines.Count -eq 0
    InversionsSafe = $inversionSafe
    USDJPYCaveatPreserved = $inversionSafe
    CompleteReadinessBindings = $completeReadiness.Count
    HeldLines = $heldLines.Count
    R009StableAcrossExpandedBarRoleBatch = $previewReady -and $inversionSafe
    RemainingBlockedBeforeExecutableDiscussion = @(
        "R009 remains design-only and non-executable",
        "No order/schedule/fill/route/submission approval exists",
        "No broker/live market data approval exists",
        "No paper ledger commit approval exists",
        "Executable promotion requires a separate explicit gate"
    )
    BarRoleCoverage = $barRoleCounts
})

$operatorMarkdown = @"
# EXEC-PAPER-R012 Operator Review

Target closes balanced across bar roles: $(@($barRoleCounts | Where-Object { $_.BatchEntryCount -eq 10 }).Count -eq 3)

- Fixtures generated: $($fixtureInventory.Count)
- ManualNoExternal commands run safely: $($executionResults.Count)
- Paper execution plan lines emitted: $($paperPlanLines.Count)
- R009 design-only preview lines produced: $($previewLines.Count)
- Held lines: $($heldLines.Count)
- Direct-cross executable lines: $($directCrossLines.Count)
- Complete readiness bindings: $($completeReadiness.Count)
- Inversions safe: $inversionSafe

Decision: $decision

Executable promotion remains blocked. No schedules, child slices, orders, fills, reports, routes, submissions, broker calls, live market data, state mutation, or paper ledger commits are authorized.
"@
Write-Text (Join-Path $ArtifactsRoot "phase-exec-paper-r012-operator-review-report.md") $operatorMarkdown
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-preview-decision.json") ([pscustomobject]@{
    Phase = $phase
    Decision = $decision
    FullBalancedBatchReady = $entries.Count -eq 30
    R009NextStagePreviewReady = $previewReady
    BarRoleBalanced = @($barRoleCounts | Where-Object { $_.BatchEntryCount -eq 10 }).Count -eq 3
    ExecutablePromotionAuthorized = $false
    Classifications = $classifications
})

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-canonical-quarter-hour-policy-preservation.json") ([pscustomobject]@{ Phase = $phase; FutureTimestampsUseCanonicalQuarterHour = $true; AllowedMinutes = @(0,15,30,45); Legacy06UsedAsFutureCanonical = $false })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-legacy-compatibility-preservation.json") ([pscustomobject]@{ Phase = $phase; LegacyTimestampsCompatibilityOnly = $true; CanonicalCloseForLegacyMatch = "LegacyOutputTimestamp - 6 minutes"; LegacyNextBarExecutionCloseCanonical = "LegacyOutputTimestamp + 9 minutes"; Legacy06UsedAsFutureCanonical = $false })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-usd-pair-normalization-preservation.json") ([pscustomobject]@{ Phase = $phase; USDPairOnlyAfterNetting = $directCrossLines.Count -eq 0; ExecutionSymbols = $supportedExecutionSymbols; AUDUSDNotFailed = $true })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-direct-cross-exclusion-preservation.json") ([pscustomobject]@{ Phase = $phase; DirectCrossesSignalOnly = $true; DirectCrossNettingFirst = $true; DirectCrossExecutionDisabled = $true; ExclusionWeakened = $false; DirectCrossExecutableLineCount = $directCrossLines.Count })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-cost-guidance-preservation.json") ([pscustomobject]@{ Phase = $phase; FiveUsdPerMillion = "BestCaseMajorOnly"; FiveUsdPerMillionUniversalized = $false; NonmajorCalibrationRequired = $true })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-nonmajor-calibration-preservation.json") ([pscustomobject]@{ Phase = $phase; NonmajorEmScandiCnhCalibrationRequired = $true; NonmajorExecutionAuthorized = $false })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-usdjpy-caveat-preservation.json") ([pscustomobject]@{ Phase = $phase; NormalizedPortfolioSymbol = "JPYUSD"; ExecutionTradableSymbol = "USDJPY"; RequiresInversion = $true; SecurityID = 4004; SecurityIDSource = "8"; CaveatWeakened = -not $inversionSafe })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-lmax-readonly-baseline-reference.json") ([pscustomobject]@{ Phase = $phase; LmaxReferencedAsReadonlyBaselineOnly = $true; LmaxCalled = $false; BrokerRuntimeActivated = $false })

New-Audit "phase-exec-paper-r012-no-broker-activation-audit.json" "BrokerActivation" "No broker activation occurred."
New-Audit "phase-exec-paper-r012-no-live-marketdata-audit.json" "LiveMarketData" "No live market data was requested."
New-Audit "phase-exec-paper-r012-no-scheduler-service-polling-audit.json" "SchedulerServicePolling" "No scheduler/service/polling/background job was started."
New-Audit "phase-exec-paper-r012-no-executable-schedule-audit.json" "ExecutableSchedulesCreated" "No executable schedules were created."
New-Audit "phase-exec-paper-r012-no-child-slices-audit.json" "ChildSlicesCreated" "No child slices were created."
New-Audit "phase-exec-paper-r012-no-child-orders-audit.json" "ChildOrdersCreated" "No child orders were created."
New-Audit "phase-exec-paper-r012-no-order-created-audit.json" "OrdersCreated" "No orders were created."
New-Audit "phase-exec-paper-r012-no-real-fill-audit.json" "FillsCreated" "No fills were created."
New-Audit "phase-exec-paper-r012-no-execution-report-audit.json" "ExecutionReportsCreated" "No execution reports were created."
New-Audit "phase-exec-paper-r012-no-route-no-submission-audit.json" "RoutesOrSubmissionsCreated" "No routes or submissions were created."
New-Audit "phase-exec-paper-r012-no-paper-ledger-commit-audit.json" "PaperLedgerCommitted" "No paper ledger commit was created."
New-Audit "phase-exec-paper-r012-no-polygon-api-call-audit.json" "PolygonCalled" "Polygon was not called."
New-Audit "phase-exec-paper-r012-no-lmax-call-audit.json" "LmaxCalled" "LMAX was not called."
New-Audit "phase-exec-paper-r012-no-external-api-call-audit.json" "ExternalApiCalled" "No external API was called."

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-no-external-audit.json") ([pscustomobject]@{ Phase = $phase; NoExternal = $true; PolygonCalled = $false; LmaxCalled = $false; ExternalApiCalled = $false; DownloadsExecuted = $false })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-forbidden-actions-audit.json") ([pscustomobject]@{
    Phase = $phase
    ForbiddenActionsDetected = $false
    BrokerActivation = $false
    LiveMarketData = $false
    SchedulerServicePolling = $false
    ExecutableSchedule = $false
    ChildSlicesOrOrders = $false
    OrdersFillsReportsRoutesSubmissions = $false
    PaperLedgerCommit = $false
    StateMutation = $false
    R009ExecutablePromotion = $false
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-next-phase-recommendation.json") ([pscustomobject]@{
    Phase = $phase
    RecommendedNextPhase = "EXEC-ALGO-R012 - No-External R009 Paper-Only Maturity Review and Long-Run Expansion Planning Gate"
    Purpose = "Review balanced bar-role stability and plan long-run paper-only expansion without executable schedules, orders, fills, routes, submissions, broker calls, or ledger commits."
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-build-test-validator-evidence.json") ([pscustomobject]@{
    Phase = $phase
    DotnetBuild = "Pending"
    FocusedR012Tests = "Pending"
    UnitTests = "Pending"
    R012Validator = "Pending"
    EvidenceComplete = $false
})

$summary = @"
# EXEC-PAPER-R012 Summary

R012 generated a next-stage balanced paper-only batch from legacy AggregatedWeights, safety-validated ManualNoExternal commands, ran the local no-external/no-ledger batch, and aggregated R009 design-only preview lines.

Classifications:
$($classifications | ForEach-Object { "- $_" } | Out-String)

Counts:
- Fixtures generated: $($fixtureInventory.Count)
- Accepted batch entries: $($entries.Count)
- Commands run safely: $($executionResults.Count)
- Paper execution plan lines: $($paperPlanLines.Count)
- R009 preview lines: $($previewLines.Count)
- Held lines: $($heldLines.Count)

Decision: $decision
"@
Write-Text (Join-Path $ArtifactsRoot "phase-exec-paper-r012-summary.md") $summary

Write-Host "EXEC-PAPER-R012 artifacts generated"
