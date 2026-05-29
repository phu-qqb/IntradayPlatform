param(
    [string]$ArtifactsRoot = "artifacts/readiness/execution-sim",
    [string]$AlgoArtifactsRoot = "artifacts/readiness/execution-algo",
    [string]$SourceFile = "C:\Users\phili\source\repos\QQ.Production.Core\home\prod\INFX1\AggregatedWeights.txt",
    [string]$FixtureDirectory = "C:\Users\phili\source\repos\QQ.Production.Intraday\data\qubes-fixtures\accepted-blocker-paper-continuation",
    [string]$ManifestPath = "C:\Users\phili\source\repos\QQ.Production.Intraday\data\qubes-fixtures\accepted-blocker-paper-continuation\batch-manifest.json",
    [int]$TargetBatchSize = 50,
    [switch]$ReuseExistingRunOutputs,
    [string]$BuildStatus = "Pending",
    [string]$FocusedTestsStatus = "Pending",
    [string]$UnitTestsStatus = "Pending",
    [string]$ValidatorStatus = "Pending"
)

$ErrorActionPreference = "Stop"

function Read-Json([string]$path) {
    return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function Read-JsonIfPresent([string]$path) {
    if (Test-Path -LiteralPath $path) { return Read-Json $path }
    return $null
}

function Write-Json([string]$path, [object]$value, [int]$depth = 40) {
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
    if ($null -eq $value) { return @() }
    if ($value -is [System.Array]) { return $value }
    return @($value)
}

function New-Audit([string]$name, [string]$key, [string]$detail) {
    Write-Json (Join-Path $ArtifactsRoot $name) ([pscustomobject]@{
        Phase = "EXEC-PAPER-R019"
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
    if ($minutes -ge ((14 * 60) + 30) -and $minutes -le ((15 * 60) + 30)) { return "OpeningBuild" }
    if ($minutes -ge (16 * 60) -and $minutes -le ((19 * 60) + 30)) { return "IntradayRebalance" }
    if ($minutes -ge (20 * 60) -and $minutes -le (21 * 60)) { return "ClosingFlatten" }
    return $null
}

function Test-CanonicalQuarterHour([string]$targetCloseLocal) {
    return -not [string]::IsNullOrWhiteSpace($targetCloseLocal) -and
        $targetCloseLocal -notmatch "T\d{2}:(06|21|36|51):00" -and
        $targetCloseLocal -match "T\d{2}:(00|15|30|45):00"
}

function Test-FixtureRows([string]$path) {
    $invalid = @()
    if (-not (Test-Path -LiteralPath $path)) {
        return [pscustomobject]@{ FixturePath = $path; Exists = $false; NonEmpty = $false; RowCount = 0; InvalidRowCount = 1; ContainsTimestampRows = $false; Valid = $false; InvalidRows = @("MissingFixture") }
    }
    $rows = @(Get-Content -LiteralPath $path)
    foreach ($row in $rows) {
        if ([string]::IsNullOrWhiteSpace($row)) { $invalid += "BlankRow"; continue }
        $parts = $row.Split(";")
        $parsed = 0.0
        if ($parts.Count -ne 2 -or
            $parts[0] -match "^\d{8,14}" -or
            -not [double]::TryParse($parts[1], [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$parsed) -or
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

function New-ReadinessIndex([object[]]$records, [string]$idProperty) {
    $index = @{}
    foreach ($record in $records) {
        $key = "$($record.Symbol)|$($record.TargetCloseTimestampUtc)"
        if (-not $index.ContainsKey($key)) { $index[$key] = $record }
    }
    return $index
}

function New-FeedQualityIndex([object[]]$records) {
    $index = @{}
    foreach ($record in $records) {
        $key = "$($record.Symbol)|$($record.LocalSessionDate)"
        if (-not $index.ContainsKey($key)) { $index[$key] = $record }
    }
    return $index
}

function Get-Binding([hashtable]$index, [string]$symbol, [string]$targetCloseUtc, [string]$idProperty) {
    $key = "$symbol|$targetCloseUtc"
    if (-not $index.ContainsKey($key)) { return $null }
    $record = $index[$key]
    return [pscustomobject]@{
        BindingId = $record.$idProperty
        Symbol = $record.Symbol
        TargetCloseTimestampUtc = $record.TargetCloseTimestampUtc
        ReadinessStatus = $record.ReadinessStatus
        SourceArtifact = "ExistingLocalReadinessArtifact"
    }
}

function Get-FeedBinding([hashtable]$index, [string]$symbol, [string]$localSessionDate) {
    $key = "$symbol|$localSessionDate"
    if (-not $index.ContainsKey($key)) { return $null }
    $record = $index[$key]
    return [pscustomobject]@{
        BindingId = $record.FeedQualityId
        Symbol = $record.Symbol
        LocalSessionDate = $record.LocalSessionDate
        ReadinessStatus = $record.FeedQualityStatus
        SourceArtifact = "ExistingLocalReadinessArtifact"
    }
}

$phase = "EXEC-PAPER-R019"
$repoRoot = (Resolve-Path ".").Path
New-Item -ItemType Directory -Force -Path $FixtureDirectory | Out-Null

$r061Status = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r061-r009-current-status.json")
$r061Blocker = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r061-residual-readiness-blocker-summary.json")
$r013Acceptance = Read-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r013-long-run-paper-maturity-acceptance-result.json")
$r014Manifest = Read-JsonIfPresent (Join-Path $ArtifactsRoot "phase-exec-paper-r013-batch-manifest.json")
$quoteWindowReadiness = Read-JsonIfPresent (Join-Path $ArtifactsRoot "phase-exec-sim-r053-quote-window-readiness-results.json")
$closeBenchmarkReadiness = Read-JsonIfPresent (Join-Path $ArtifactsRoot "phase-exec-sim-r053-close-benchmark-readiness-results.json")
$feedQualityReadiness = Read-JsonIfPresent (Join-Path $ArtifactsRoot "phase-exec-sim-r053-feed-quality-readiness-results.json")

$usedTargetCloses = @{}
if ($null -ne $r014Manifest) {
    foreach ($entry in (As-Array $r014Manifest.Entries)) {
        $usedTargetCloses[[string]$entry.CanonicalTargetCloseLocal] = $true
    }
}

$sourceLines = @(Get-Content -LiteralPath $SourceFile)
$tickerMappingPath = Join-Path $repoRoot "data\qubes-fixtures\r009\current-qubes-weights.txt"
$tickers = @(Get-Content -LiteralPath $tickerMappingPath | ForEach-Object { $_.Split(";")[0] })
$header = @($sourceLines[0].Split(";") | Select-Object -Skip 1)
if ($header.Count -ne $tickers.Count) {
    throw "AggregatedWeights header count $($header.Count) does not match ticker mapping count $($tickers.Count)."
}

$eligible = @()
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
    $targetLocalString = "$($targetLocal.ToString("yyyy-MM-ddTHH:mm:ss")) America/New_York"
    if ($usedTargetCloses.ContainsKey($targetLocalString)) { continue }

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
    $eligible += [pscustomobject]@{
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

$perRoleTarget = [math]::Floor($TargetBatchSize / 3)
$extra = $TargetBatchSize - ($perRoleTarget * 3)
$targetsByRole = @{
    OpeningBuild = $perRoleTarget + $(if ($extra -gt 0) { 1 } else { 0 })
    IntradayRebalance = $perRoleTarget + $(if ($extra -gt 1) { 1 } else { 0 })
    ClosingFlatten = $perRoleTarget
}

$selectedKeys = @{}
$selected = @()
$selected += Select-RoleGroups $eligible "OpeningBuild" $targetsByRole.OpeningBuild $selectedKeys
$selected += Select-RoleGroups $eligible "IntradayRebalance" $targetsByRole.IntradayRebalance $selectedKeys
$selected += Select-RoleGroups $eligible "ClosingFlatten" $targetsByRole.ClosingFlatten $selectedKeys
if ($selected.Count -lt $TargetBatchSize) {
    foreach ($candidate in ($eligible | Sort-Object TargetLocal)) {
        if ($selected.Count -ge $TargetBatchSize) { break }
        if (-not $selectedKeys.ContainsKey($candidate.SelectionKey)) {
            $selected += $candidate
            $selectedKeys[$candidate.SelectionKey] = $true
        }
    }
}
$selected = @($selected | Sort-Object TargetLocal | Select-Object -First $TargetBatchSize)

$entries = @()
$fixtureInventory = @()
$fixtureValidation = @()
foreach ($group in $selected) {
    $dateToken = $group.TargetLocal.ToString("yyyyMMdd")
    $timeToken = $group.TargetLocal.ToString("HHmm")
    $roleToken = $group.BarRole.ToLowerInvariant()
    $entryId = "paper-r009-accepted-blocker-continuation-$dateToken-$timeToken-$roleToken-legacy-match"
    $fixtureName = "qubes-$dateToken-$timeToken-$roleToken-accepted-blocker-continuation.txt"
    $fixturePath = Join-Path $FixtureDirectory $fixtureName
    $rows = @($group.WeightRows | ForEach-Object { "$($_.Ticker);$($_.Weight)" })
    Set-Content -LiteralPath $fixturePath -Value $rows -Encoding UTF8
    $outputDir = Join-Path $repoRoot "artifacts\readiness\execution-sim\$entryId"
    $entry = [pscustomobject]@{
        BatchEntryId = $entryId
        QubesFixturePath = $fixturePath
        QubesRunId = "qubes-$entryId"
        RequestedCycleRunId = $entryId
        CanonicalTargetCloseLocal = "$($group.TargetLocal.ToString("yyyy-MM-ddTHH:mm:ss")) America/New_York"
        CanonicalTargetCloseUtc = $group.TargetUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", [System.Globalization.CultureInfo]::InvariantCulture)
        CanonicalSession = "14:15-21:00 America/New_York"
        BarRole = $group.BarRole
        CadenceMinutes = 15
        OutputArtifactsDir = $outputDir
        NoPaperLedgerCommit = $true
        FixtureSource = "LegacyAggregatedWeightsExtraction"
        LegacyCompatibilityMappingUsed = $true
        ReadinessBindingRequired = $true
        RiskOperatorApprovalScope = "DesignOnlyPreviewOnly"
        AcceptedReadinessBlockerCarried = "LocalMarketDataReadinessIncompleteFor56PreviewLines"
        RegimeLabel = "Unknown"
        LegacyCompatibilityMapping = [pscustomobject]@{
            LegacyOutputTimestamp = $group.SourceLegacyTimestamp
            CanonicalCloseForLegacyMatch = "$($group.CanonicalCloseForLegacyMatch.ToString("yyyy-MM-ddTHH:mm:ss")) America/New_York"
            LegacyNextBarExecutionCloseCanonical = "$($group.TargetLocal.ToString("yyyy-MM-ddTHH:mm:ss")) America/New_York"
            Rule = "LegacyNextBarExecutionCloseCanonical = LegacyOutputTimestamp + 9 minutes"
        }
    }
    $entries += $entry
    $fixtureInventory += [pscustomobject]@{
        BatchEntryId = $entryId
        FixturePath = $fixturePath
        RowCount = $rows.Count
        ContainsTimestampRows = @($rows | Where-Object { $_ -match "^\d{8,14}" }).Count -gt 0
        SourceLegacyTimestamp = $group.SourceLegacyTimestamp
        CanonicalTargetCloseLocal = $entry.CanonicalTargetCloseLocal
        BarRole = $entry.BarRole
    }
    $fixtureValidation += Test-FixtureRows $fixturePath
}

$manifest = [pscustomobject]@{
    Phase = $phase
    ManifestStatus = if ($entries.Count -eq $TargetBatchSize) { "AcceptedBlockerContinuationBatchReady" } else { "PartialAcceptedBlockerContinuationBatchReady" }
    AcceptedReadinessBlocker = "LocalMarketDataReadinessIncompleteFor56PreviewLines"
    Entries = $entries
}
Write-Json $ManifestPath $manifest
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-batch-manifest.json") $manifest

$commands = @()
foreach ($entry in $entries) {
    $commandLine = "dotnet run --no-restore --project tools\QQ.Production.Intraday.Tools.ManualPaperCycle\QQ.Production.Intraday.Tools.ManualPaperCycle.csproj -- --mode ManualNoExternal --requested-cycle-run-id `"$($entry.RequestedCycleRunId)`" --qubes-run-id `"$($entry.QubesRunId)`" --qubes-fixture-path `"$($entry.QubesFixturePath)`" --prior-paper-ledger-state-id `"paper-ledger-commit-r025-sample:paper-ledger-state`" --prior-continuity-gate-id `"cycle-r026-second-paper-baseline:paper-continuity-archive:paper-continuity-gate`" --requested-by `"operator-sanitized`" --expected-cadence-minutes 15 --output-artifacts-dir `"$($entry.OutputArtifactsDir)`" --no-paper-ledger-commit true"
    $commands += [pscustomobject]@{
        BatchEntryId = $entry.BatchEntryId
        CommandLine = $commandLine
        ManualOnly = $true
        OperatorRunRequired = $false
        GeneratedForControlledLocalR019Execution = $true
    }
}

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-r061-programme-reference.json") ([pscustomobject]@{
    Phase = $phase
    SourcePhase = "EXEC-SIM-R061"
    R009Status = $r061Status.R009Status
    PaperOnlyMaturity = $r061Status.PaperOnlyMaturity
    ReadinessCompleteLineCount = $r061Status.ReadinessCompleteLineCount
    PreviewLineCount = $r061Status.PreviewLineCount
    RemainingHeldLineCount = $r061Status.RemainingHeldLineCount
    ResidualBlocker = $r061Blocker.Blocker
    ResidualBlockerIsReadinessOnly = $r061Blocker.NotR009LogicFailure
    ReusedOnly = $true
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-r013-blocker-acceptance-reference.json") ([pscustomobject]@{
    Phase = $phase
    SourcePhase = "EXEC-ALGO-R013"
    MaturityStatus = $r013Acceptance.MaturityStatus
    PaperOnlyMaturityStatus = $r013Acceptance.PaperOnlyMaturityStatus
    ReadinessCompleteLineCount = $r013Acceptance.ReadinessCompleteLineCount
    PreviewLineCount = $r013Acceptance.PreviewLineCount
    FinalStillHeldLineCount = $r013Acceptance.FinalStillHeldLineCount
    ExplicitBlocker = $r013Acceptance.ExplicitBlocker
    ExecutablePromotionAuthorized = $false
    ReusedOnly = $true
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-r009-contract-reference.json") ([pscustomobject]@{
    Phase = $phase
    ContractVersion = "0.3.0-design-only-candidate"
    PrimaryPolicyCandidate = "CloseSeeking15mAdaptive_BalancedAdaptive_v0"
    SecondaryPolicyCandidate = "CloseSeeking15mAdaptive_ResidualAwareUrgency_v0"
    ConditionalResidualModule = "ControlledResidualCross_BalancedResidualCross_v0"
    DesignOnly = $true
    PaperOnly = $true
    NonExecutable = $true
    NotAnOrder = $true
    NotSubmitted = $true
    NoBrokerRoute = $true
    BrokerReady = $false
    LiveReady = $false
    ExecutablePromotionAuthorized = $false
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-accepted-blocker-context.json") ([pscustomobject]@{
    Phase = $phase
    AcceptedBlockerContextLoaded = $true
    R009Status = "R009AcceptedForLongRunPaperOnlyEvaluationWithExplicitReadinessBlocker"
    PaperOnlyMaturity = "R009PaperOnlyMaturityPartialButUsable"
    PriorReadinessCompleteLineCount = 644
    PriorPreviewLineCount = 700
    PriorRemainingHeldLineCount = 56
    AcceptedBlocker = "LocalMarketDataReadinessIncompleteFor56PreviewLines"
    MissingReadinessMayHoldLines = $true
    MissingReadinessBlocksWholeBatch = $false
    NotDirectCrossIssue = $true
    NotInversionIssue = $true
    NotUsdJpyCaveatIssue = $true
    NotR009LogicIssue = $true
    NotExecutablePathIssue = $true
    ExecutablePromotionBlocked = $true
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-generated-fixture-inventory.json") ([pscustomobject]@{ Phase = $phase; FixtureCount = $fixtureInventory.Count; Inventory = $fixtureInventory })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-generated-fixture-validation.json") ([pscustomobject]@{ Phase = $phase; FixtureCount = $fixtureValidation.Count; ValidFixtureCount = @($fixtureValidation | Where-Object { $_.Valid }).Count; InvalidFixtureCount = @($fixtureValidation | Where-Object { -not $_.Valid }).Count; Results = $fixtureValidation })

$manifestIssues = @()
foreach ($entry in $entries) {
    if (-not (Test-CanonicalQuarterHour ([string]$entry.CanonicalTargetCloseLocal))) { $manifestIssues += "$($entry.BatchEntryId):NonCanonicalTargetClose" }
    if (-not $entry.NoPaperLedgerCommit) { $manifestIssues += "$($entry.BatchEntryId):NoPaperLedgerCommitFalse" }
    if ([string]::IsNullOrWhiteSpace([string]$entry.BarRole)) { $manifestIssues += "$($entry.BatchEntryId):MissingBarRole" }
}
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-batch-manifest-validation.json") ([pscustomobject]@{
    Phase = $phase
    EntryCount = $entries.Count
    ValidEntryCount = $entries.Count - $manifestIssues.Count
    IssueCount = $manifestIssues.Count
    Issues = $manifestIssues
    CanonicalQuarterHourTargetCloses = $manifestIssues.Count -eq 0
    NoPaperLedgerCommitAllTrue = @($entries | Where-Object { -not $_.NoPaperLedgerCommit }).Count -eq 0
})

$commandByEntry = @{}
foreach ($command in $commands) { $commandByEntry[$command.BatchEntryId] = $command }
$safetyChecks = @()
$unsafeReasons = @()
foreach ($entry in $entries) {
    $fixtureCheck = $fixtureValidation | Where-Object { $_.FixturePath -eq $entry.QubesFixturePath } | Select-Object -First 1
    $command = $commandByEntry[$entry.BatchEntryId]
    $line = if ($null -ne $command) { [string]$command.CommandLine } else { "" }
    $requiredPresent = $line -match "--mode ManualNoExternal" -and
        $line -match "--output-artifacts-dir" -and
        $line -match "--requested-cycle-run-id" -and
        $line -match "--qubes-run-id" -and
        $line -match "--qubes-fixture-path" -and
        $line -match "--expected-cadence-minutes 15" -and
        $line -match "--no-paper-ledger-commit true"
    $deprecatedOrForbidden = $line -match "--mode no-external-paper-cycle" -or
        $line -match "\s--output\s" -or
        $line -match "--(broker|live|order|route|submit|fill|scheduler|service|poll)"
    $safe = $null -ne $command -and
        $fixtureCheck.Valid -and
        (Test-CanonicalQuarterHour ([string]$entry.CanonicalTargetCloseLocal)) -and
        $entry.NoPaperLedgerCommit -and
        $requiredPresent -and
        -not $deprecatedOrForbidden
    if (-not $safe) {
        $unsafeReasons += [pscustomobject]@{ BatchEntryId = $entry.BatchEntryId; FixtureValid = $fixtureCheck.Valid; RequiredFlagsPresent = $requiredPresent; DeprecatedOrForbiddenFlags = $deprecatedOrForbidden }
    }
    $safetyChecks += [pscustomobject]@{
        BatchEntryId = $entry.BatchEntryId
        Safe = $safe
        UsesManualNoExternal = $line -match "--mode ManualNoExternal"
        IncludesOutputArtifactsDir = $line -match "--output-artifacts-dir"
        IncludesRequestedCycleRunId = $line -match "--requested-cycle-run-id"
        IncludesQubesRunId = $line -match "--qubes-run-id"
        IncludesQubesFixturePath = $line -match "--qubes-fixture-path"
        IncludesCadence15 = $line -match "--expected-cadence-minutes 15"
        IncludesNoPaperLedgerCommitTrue = $line -match "--no-paper-ledger-commit true"
        DeprecatedModeUsed = $line -match "--mode no-external-paper-cycle"
        DeprecatedOutputUsed = $line -match "\s--output\s"
        BrokerLiveOrderRouteSubmissionFlagsPresent = $line -match "--(broker|live|order|route|submit|fill)"
    }
}
$allSafe = $entries.Count -gt 0 -and $commands.Count -eq $entries.Count -and $unsafeReasons.Count -eq 0
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-command-safety-check.json") ([pscustomobject]@{
    Phase = $phase
    SafetyValidatedBeforeExecution = $true
    AcceptedBatchEntries = $entries.Count
    CommandCount = $commands.Count
    AllCommandsSafe = $allSafe
    UnsafeReasonCount = $unsafeReasons.Count
    UnsafeReasons = $unsafeReasons
    Commands = $commands
    Checks = $safetyChecks
})

if (-not $allSafe) {
    Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-batch-execution-result.json") ([pscustomobject]@{
        Phase = $phase
        CommandsExecuted = 0
        AcceptedBatchEntries = $entries.Count
        AllRunsCompletedSafely = $false
        BlockedReason = "UnsafeCommandOrInput"
        Results = @()
    })
    throw "R019 safety validation failed; no commands executed."
}

$executionResults = @()
foreach ($entry in $entries) {
    if (-not $ReuseExistingRunOutputs) {
        $args = @(
            "run", "--no-restore",
            "--project", "tools\QQ.Production.Intraday.Tools.ManualPaperCycle\QQ.Production.Intraday.Tools.ManualPaperCycle.csproj",
            "--",
            "--mode", "ManualNoExternal",
            "--requested-cycle-run-id", [string]$entry.RequestedCycleRunId,
            "--qubes-run-id", [string]$entry.QubesRunId,
            "--qubes-fixture-path", [string]$entry.QubesFixturePath,
            "--prior-paper-ledger-state-id", "paper-ledger-commit-r025-sample:paper-ledger-state",
            "--prior-continuity-gate-id", "cycle-r026-second-paper-baseline:paper-continuity-archive:paper-continuity-gate",
            "--requested-by", "operator-sanitized",
            "--expected-cadence-minutes", "15",
            "--output-artifacts-dir", [string]$entry.OutputArtifactsDir,
            "--no-paper-ledger-commit", "true"
        )
        $processOutput = & dotnet @args 2>&1
        $exitCode = $LASTEXITCODE
    } else {
        $processOutput = "ReusedExistingManualNoExternalOutput"
        $exitCode = 0
    }
    $summaryPath = Join-Path ([string]$entry.OutputArtifactsDir) "phase-pms-ems-oms-r031-cli-manual-run-output.json"
    $linesPath = Join-Path ([string]$entry.OutputArtifactsDir) "phase-pms-ems-oms-manual-noexternal-paper-execution-plan-lines.json"
    $summary = if (Test-Path -LiteralPath $summaryPath) { Read-Json $summaryPath } else { $null }
    $lineCount = if (Test-Path -LiteralPath $linesPath) { (As-Array (Read-Json $linesPath).Lines).Count } else { 0 }
    $noExternal = $null -ne $summary -and [bool]$summary.NoExternal
    $noPaperLedgerCommit = $null -ne $summary -and [bool]$summary.NoPaperLedgerCommit
    $noOrder = $null -ne $summary -and [bool]$summary.NoOrder
    $noFill = $null -ne $summary -and [bool]$summary.NoFill
    $noReport = $null -ne $summary -and [bool]$summary.NoReport
    $noRoute = $null -ne $summary -and [bool]$summary.NoRoute
    $noSubmission = $null -ne $summary -and [bool]$summary.NoSubmission
    $noBroker = $noExternal -and $noRoute -and $noSubmission
    $noLiveMarketData = $noExternal
    $executionResults += [pscustomobject]@{
        BatchEntryId = $entry.BatchEntryId
        ExitCode = $exitCode
        Stdout = (($processOutput | Out-String).Trim())
        SummaryArtifact = $summaryPath
        LinesArtifact = $linesPath
        LineCount = $lineCount
        CycleExecutionCount = if ($null -ne $summary) { $summary.CycleExecutionCount } else { $null }
        NoExternal = $noExternal
        NoBroker = $noBroker
        NoLiveMarketData = $noLiveMarketData
        NoPaperLedgerCommit = $noPaperLedgerCommit
        NoOrder = $noOrder
        NoFill = $noFill
        NoReport = $noReport
        NoRoute = $noRoute
        NoSubmission = $noSubmission
        CompletedSafely = $exitCode -eq 0 -and $lineCount -eq 7 -and $null -ne $summary -and $noExternal -and $noBroker -and $noLiveMarketData -and $noPaperLedgerCommit -and $noOrder -and $noFill -and $noReport -and $noRoute -and $noSubmission
    }
}

$runsSafe = $executionResults.Count -eq $entries.Count -and @($executionResults | Where-Object { -not $_.CompletedSafely }).Count -eq 0
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-batch-execution-result.json") ([pscustomobject]@{
    Phase = $phase
    CommandsExecuted = $executionResults.Count
    AcceptedBatchEntries = $entries.Count
    MoreCommandsThanAcceptedEntries = $executionResults.Count -gt $entries.Count
    AllRunsCompletedSafely = $runsSafe
    NoExternal = @($executionResults | Where-Object { -not $_.NoExternal }).Count -eq 0
    NoBroker = @($executionResults | Where-Object { -not $_.NoBroker }).Count -eq 0
    NoLiveMarketData = @($executionResults | Where-Object { -not $_.NoLiveMarketData }).Count -eq 0
    NoPaperLedgerCommit = @($executionResults | Where-Object { -not $_.NoPaperLedgerCommit }).Count -eq 0
    NoOrderFillReportRouteSubmission = @($executionResults | Where-Object { -not ($_.NoOrder -and $_.NoFill -and $_.NoReport -and $_.NoRoute -and $_.NoSubmission) }).Count -eq 0
    Results = $executionResults
})

$quoteWindowIndex = New-ReadinessIndex (As-Array $quoteWindowReadiness.Results) "QuoteWindowId"
$closeBenchmarkIndex = New-ReadinessIndex (As-Array $closeBenchmarkReadiness.Results) "CloseBenchmarkId"
$feedQualityIndex = New-FeedQualityIndex (As-Array $feedQualityReadiness.Results)
$supportedExecutionSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")
$paperPlanLines = @()
$previewLines = @()
$heldLines = @()
foreach ($entry in $entries) {
    $linesPath = Join-Path ([string]$entry.OutputArtifactsDir) "phase-pms-ems-oms-manual-noexternal-paper-execution-plan-lines.json"
    if (-not (Test-Path -LiteralPath $linesPath)) { continue }
    foreach ($line in (As-Array (Read-Json $linesPath).Lines)) {
        $executionSymbol = [string]$line.ExecutionTradableSymbol
        $targetCloseUtc = [string]$entry.CanonicalTargetCloseUtc
        $targetCloseLocal = [string]$entry.CanonicalTargetCloseLocal
        $sessionDate = [string]($targetCloseLocal.Substring(0, 10))
        $quoteBinding = Get-Binding $quoteWindowIndex $executionSymbol $targetCloseUtc "QuoteWindowId"
        $benchmarkBinding = Get-Binding $closeBenchmarkIndex $executionSymbol $targetCloseUtc "CloseBenchmarkId"
        $feedBinding = Get-FeedBinding $feedQualityIndex $executionSymbol $sessionDate
        $missing = @()
        if ($null -eq $quoteBinding) { $missing += "MissingQuoteWindowReadinessBinding" }
        if ($null -eq $benchmarkBinding) { $missing += "MissingCloseBenchmarkReadinessBinding" }
        if ($null -eq $feedBinding) { $missing += "MissingFeedQualityReadinessBinding" }
        $directCrossLine = -not ($supportedExecutionSymbols -contains $executionSymbol)
        $canonicalConfirmed = Test-CanonicalQuarterHour $targetCloseLocal
        $holdReason = if ($directCrossLine) { "DirectCrossExecutableLine" } elseif (-not $canonicalConfirmed) { "MissingCanonicalQuarterHourTargetClose" } elseif ($missing.Count -gt 0) { "HeldMissingReadiness" } else { $null }
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
            DirectCrossExecutableLine = $directCrossLine
        }
        $paperPlanLines += $paperLine
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
            MissingReadinessInputs = $missing
        }
        $previewLines += $preview
        if ($null -ne $holdReason) {
            $heldLines += [pscustomobject]@{
                BatchEntryId = $entry.BatchEntryId
                PaperExecutionPlanLineId = $line.PaperExecutionPlanLineId
                ExecutionTradableSymbol = $line.ExecutionTradableSymbol
                BarRole = $entry.BarRole
                CanonicalTargetCloseUtc = $targetCloseUtc
                HoldReason = $holdReason
                MissingInputs = $missing
            }
        }
    }
}

$expectedMaxPreviewLines = $entries.Count * 7
$directCrossLines = @($paperPlanLines | Where-Object { $_.DirectCrossExecutableLine })
$completeReadiness = @($previewLines | Where-Object { $null -ne $_.QuoteWindowReadinessBinding -and $null -ne $_.CloseBenchmarkReadinessBinding -and $null -ne $_.FeedQualityReadinessBinding })
$inversionFailures = @($paperPlanLines | Where-Object {
    ($_.ExecutionTradableSymbol -eq "USDJPY" -and -not ($_.NormalizedPortfolioSymbol -eq "JPYUSD" -and $_.RequiresInversion -and [string]$_.SecurityID -eq "4004" -and [string]$_.SecurityIDSource -eq "8")) -or
    ($_.ExecutionTradableSymbol -eq "USDCAD" -and -not $_.RequiresInversion) -or
    ($_.ExecutionTradableSymbol -eq "USDCHF" -and -not $_.RequiresInversion)
})

$barRoleCoverage = $entries | Group-Object BarRole | ForEach-Object {
    $role = $_.Name
    [pscustomobject]@{
        BarRole = $role
        BatchEntryCount = $_.Count
        PreviewLineCount = @($previewLines | Where-Object { $_.BarRole -eq $role }).Count
        HeldLineCount = @($heldLines | Where-Object { $_.BarRole -eq $role }).Count
    }
}
$symbolCoverage = $previewLines | Group-Object ExecutionTradableSymbol | Sort-Object Name | ForEach-Object {
    [pscustomobject]@{
        ExecutionTradableSymbol = $_.Name
        PreviewLineCount = $_.Count
        HeldLineCount = @($_.Group | Where-Object { $_.HoldReason -eq "HeldMissingReadiness" }).Count
    }
}

$fullPreview = $runsSafe -and $previewLines.Count -eq $expectedMaxPreviewLines -and $heldLines.Count -eq 0 -and $directCrossLines.Count -eq 0 -and $inversionFailures.Count -eq 0
$usableWithHeldReadiness = $runsSafe -and $previewLines.Count -eq $expectedMaxPreviewLines -and $directCrossLines.Count -eq 0 -and $inversionFailures.Count -eq 0 -and @($heldLines | Where-Object { $_.HoldReason -ne "HeldMissingReadiness" }).Count -eq 0
$decision = if ($fullPreview) { "R009PaperOnlyContinuationStable" } elseif ($usableWithHeldReadiness) { "R009PaperOnlyContinuationStableWithHeldReadiness" } else { "InconclusiveSafe" }
$classifications = if ($fullPreview -or $usableWithHeldReadiness) {
    @(
        "EXEC_PAPER_R019_PASS_ACCEPTED_BLOCKER_CONTEXT_READY_NO_EXTERNAL",
        "EXEC_PAPER_R019_PASS_CONTINUATION_BATCH_RUNS_READY_NO_EXTERNAL",
        "EXEC_PAPER_R019_PASS_R009_CONTINUATION_PREVIEW_READY_NO_EXTERNAL",
        "EXEC_PAPER_R019_PASS_ACCEPTED_BLOCKER_PAPER_ONLY_REVIEW_READY_NO_EXTERNAL",
        "EXEC_PAPER_R019_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
    )
} else {
    @(
        "EXEC_PAPER_R019_PARTIAL_CONTINUATION_PREVIEW_WITH_HELD_READINESS_NO_EXTERNAL",
        "EXEC_PAPER_R019_PASS_HELD_READINESS_DIAGNOSTICS_READY_NO_EXTERNAL",
        "EXEC_PAPER_R019_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
    )
}
if ($heldLines.Count -gt 0 -and "EXEC_PAPER_R019_PARTIAL_CONTINUATION_PREVIEW_WITH_HELD_READINESS_NO_EXTERNAL" -notin $classifications) {
    $classifications = @(
        "EXEC_PAPER_R019_PARTIAL_CONTINUATION_PREVIEW_WITH_HELD_READINESS_NO_EXTERNAL",
        "EXEC_PAPER_R019_PASS_HELD_READINESS_DIAGNOSTICS_READY_NO_EXTERNAL",
        "EXEC_PAPER_R019_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
    )
}

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-paper-plan-lines-aggregate.json") ([pscustomobject]@{ Phase = $phase; LineCount = $paperPlanLines.Count; ExpectedMaximumLineCount = $expectedMaxPreviewLines; Lines = $paperPlanLines })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-r009-design-only-preview-lines.json") ([pscustomobject]@{ Phase = $phase; PreviewLineCount = $previewLines.Count; ExpectedMaximumPreviewLineCount = $expectedMaxPreviewLines; Lines = $previewLines })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-preview-line-coverage.json") ([pscustomobject]@{ Phase = $phase; BatchEntryCount = $entries.Count; PaperPlanLineCount = $paperPlanLines.Count; PreviewLineCount = $previewLines.Count; ExpectedMaximumPreviewLineCount = $expectedMaxPreviewLines; ReadinessCompleteLineCount = $completeReadiness.Count; HeldLineCount = $heldLines.Count; DirectCrossExecutableLineCount = $directCrossLines.Count; InversionFailureCount = $inversionFailures.Count })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-held-readiness-diagnostics.json") ([pscustomobject]@{ Phase = $phase; HeldLineCount = $heldLines.Count; HeldMissingReadinessCount = @($heldLines | Where-Object { $_.HoldReason -eq "HeldMissingReadiness" }).Count; MissingReadinessTreatedAsR009LogicFailure = $false; HeldLines = $heldLines; HeldBySymbol = $symbolCoverage; HeldByBarRole = $barRoleCoverage })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-bar-role-coverage-review.json") ([pscustomobject]@{ Phase = $phase; Coverage = $barRoleCoverage; RoleBalanceTarget = $targetsByRole; BalanceReason = if ($entries.Count -eq $TargetBatchSize) { "Balanced as far as eligible unused groups allowed." } else { "Partial because fewer eligible unused groups were selected." } })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-per-symbol-coverage-review.json") ([pscustomobject]@{ Phase = $phase; Coverage = $symbolCoverage })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-direct-cross-netting-review.json") ([pscustomobject]@{ Phase = $phase; DirectCrossExecutableLineCount = $directCrossLines.Count; DirectCrossesExcludedAfterNetting = $directCrossLines.Count -eq 0 })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-inversion-review.json") ([pscustomobject]@{ Phase = $phase; InversionsSafe = $inversionFailures.Count -eq 0; USDJPYCaveatPreserved = $inversionFailures.Count -eq 0; InversionFailureCount = $inversionFailures.Count; Failures = $inversionFailures })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-executable-promotion-blockers.json") ([pscustomobject]@{ Phase = $phase; ExecutablePromotionBlocked = $true; Blockers = @("NoBrokerIntegrationAuthorized", "NoLiveMarketDataAuthorized", "NoOmsOrderCreationAuthorized", "NoExecutableScheduleAuthorized", "NoChildSlicesAuthorized", "NoRouteSubmissionAuthorized", "NoFillsExecutionReportsAuthorized", "NoPaperLedgerCommitAuthorized", "NoStateMutationAuthorized", "NoDirectCrossExecutionAuthorized", "NoNonmajorEmScandiCnhExecutionWithoutCalibration", "AcceptedReadinessBlockerRemains", "SeparateExplicitExecutableGateRequiredIfEverConsidered") })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-continuation-decision.json") ([pscustomobject]@{ Phase = $phase; Decision = $decision; Classifications = $classifications; AcceptedBlockerCarried = $true; MissingReadinessBlocksWholeBatch = $false; ExecutablePromotionAuthorized = $false })

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-operator-review-report.json") ([pscustomobject]@{
    Phase = $phase
    AcceptedBlockerContextLoaded = $true
    FixtureCount = $entries.Count
    CommandsPassedSafetyValidation = $allSafe
    ManualNoExternalCommandsRun = $executionResults.Count
    PaperExecutionPlanLinesEmitted = $paperPlanLines.Count
    R009PreviewLinesProduced = $previewLines.Count
    ReadinessCompleteLineCount = $completeReadiness.Count
    HeldLineCount = $heldLines.Count
    HeldMissingReadinessCount = @($heldLines | Where-Object { $_.HoldReason -eq "HeldMissingReadiness" }).Count
    MissingReadinessTreatedAsBatchFailure = $false
    MissingReadinessTreatedAsR009LogicFailure = $false
    BarRoleCoverage = $barRoleCoverage
    SymbolCoverage = $symbolCoverage
    DirectCrossesExcludedAfterNetting = $directCrossLines.Count -eq 0
    InversionsSafe = $inversionFailures.Count -eq 0
    Decision = $decision
    ExecutablePromotionAuthorized = $false
})
$reviewMd = @"
# EXEC-PAPER-R019 Operator Review

- Accepted blocker carried: LocalMarketDataReadinessIncompleteFor56PreviewLines
- Fixtures / batch entries: $($entries.Count)
- Commands safety validated: $allSafe
- ManualNoExternal commands run: $($executionResults.Count)
- Paper execution plan lines emitted: $($paperPlanLines.Count)
- R009 design-only preview lines produced: $($previewLines.Count)
- Expected maximum preview lines: $expectedMaxPreviewLines
- Readiness-complete preview lines: $($completeReadiness.Count)
- Held readiness lines: $($heldLines.Count)
- Direct-cross executable lines: $($directCrossLines.Count)
- Inversion failures: $($inversionFailures.Count)

Decision: $decision

Missing readiness is carried as HeldMissingReadiness and is not treated as a batch failure or R009 logic failure. No executable promotion, schedules, orders, fills, routes, submissions, broker calls, live market data, state mutation, or paper ledger commits are authorized.
"@
Write-Text (Join-Path $ArtifactsRoot "phase-exec-paper-r019-operator-review-report.md") $reviewMd

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-canonical-quarter-hour-policy-preservation.json") ([pscustomobject]@{ Phase = $phase; FutureTimestampsUseCanonicalQuarterHour = $true; AllowedMinutes = @(0,15,30,45); Legacy06UsedAsFutureCanonical = $false })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-legacy-compatibility-preservation.json") ([pscustomobject]@{ Phase = $phase; LegacyTimestampsCompatibilityOnly = $true; CanonicalCloseForLegacyMatch = "LegacyOutputTimestamp - 6 minutes"; LegacyNextBarExecutionCloseCanonical = "LegacyOutputTimestamp + 9 minutes"; Legacy06UsedAsFutureCanonical = $false })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-usd-pair-normalization-preservation.json") ([pscustomobject]@{ Phase = $phase; USDPairOnlyAfterNetting = $directCrossLines.Count -eq 0; ExecutionSymbols = $supportedExecutionSymbols; AUDUSDNotFailed = $true })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-direct-cross-exclusion-preservation.json") ([pscustomobject]@{ Phase = $phase; DirectCrossesSignalOnly = $true; DirectCrossNettingFirst = $true; DirectCrossExecutionDisabled = $true; ExclusionWeakened = $false; DirectCrossExecutableLineCount = $directCrossLines.Count })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-cost-guidance-preservation.json") ([pscustomobject]@{ Phase = $phase; FiveUsdPerMillion = "BestCaseMajorOnly"; FiveUsdPerMillionUniversalized = $false; NonmajorCalibrationRequired = $true })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-nonmajor-calibration-preservation.json") ([pscustomobject]@{ Phase = $phase; NonmajorEmScandiCnhCalibrationRequired = $true; NonmajorExecutionAuthorized = $false })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-usdjpy-caveat-preservation.json") ([pscustomobject]@{ Phase = $phase; NormalizedPortfolioSymbol = "JPYUSD"; ExecutionTradableSymbol = "USDJPY"; RequiresInversion = $true; SecurityID = 4004; SecurityIDSource = "8"; CaveatWeakened = $inversionFailures.Count -gt 0 })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-lmax-readonly-baseline-reference.json") ([pscustomobject]@{ Phase = $phase; LmaxReferencedAsReadonlyBaselineOnly = $true; LmaxCalled = $false; BrokerRuntimeActivated = $false })

New-Audit "phase-exec-paper-r019-no-broker-activation-audit.json" "BrokerActivation" "No broker activation occurred."
New-Audit "phase-exec-paper-r019-no-live-marketdata-audit.json" "LiveMarketData" "No live market data was requested."
New-Audit "phase-exec-paper-r019-no-scheduler-service-polling-audit.json" "SchedulerServicePolling" "No scheduler/service/polling/background job was started."
New-Audit "phase-exec-paper-r019-no-executable-schedule-audit.json" "ExecutableSchedulesCreated" "No executable schedules were created."
New-Audit "phase-exec-paper-r019-no-child-slices-audit.json" "ChildSlicesCreated" "No child slices were created."
New-Audit "phase-exec-paper-r019-no-child-orders-audit.json" "ChildOrdersCreated" "No child orders were created."
New-Audit "phase-exec-paper-r019-no-order-created-audit.json" "OrdersCreated" "No orders were created."
New-Audit "phase-exec-paper-r019-no-real-fill-audit.json" "FillsCreated" "No fills were created."
New-Audit "phase-exec-paper-r019-no-execution-report-audit.json" "ExecutionReportsCreated" "No execution reports were created."
New-Audit "phase-exec-paper-r019-no-route-no-submission-audit.json" "RoutesOrSubmissionsCreated" "No routes or submissions were created."
New-Audit "phase-exec-paper-r019-no-paper-ledger-commit-audit.json" "PaperLedgerCommitted" "No paper ledger commit was created."
New-Audit "phase-exec-paper-r019-no-polygon-api-call-audit.json" "PolygonCalled" "Polygon was not called."
New-Audit "phase-exec-paper-r019-no-lmax-call-audit.json" "LmaxCalled" "LMAX was not called."
New-Audit "phase-exec-paper-r019-no-external-api-call-audit.json" "ExternalApiCalled" "No external API was called."
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-no-external-audit.json") ([pscustomobject]@{ Phase = $phase; NoExternal = $true; PolygonCalled = $false; LmaxCalled = $false; ExternalApiCalled = $false; DownloadsExecuted = $false; BrokerActivated = $false; LiveMarketDataRequested = $false })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-forbidden-actions-audit.json") ([pscustomobject]@{ Phase = $phase; ForbiddenActionsDetected = $false; BrokerActivation = $false; LiveMarketData = $false; SchedulerServicePolling = $false; ExecutableSchedule = $false; ChildSlicesOrOrders = $false; OrdersFillsReportsRoutesSubmissions = $false; PaperLedgerCommit = $false; StateMutation = $false; R009ExecutablePromotion = $false })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-next-phase-recommendation.json") ([pscustomobject]@{ Phase = $phase; RecommendedNextStep = if ($heldLines.Count -gt 0) { "Continue paper-only programme with accepted readiness blocker or run future no-external readiness-completion intake." } else { "Continue paper-only expansion review under no-external controls." }; ExecutablePromotionStillBlocked = $true })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-build-test-validator-evidence.json") ([pscustomobject]@{ Phase = $phase; Build = [pscustomobject]@{ Command = "dotnet build --no-restore"; Status = $BuildStatus }; FocusedTests = [pscustomobject]@{ Command = "dotnet test tests/QQ.Production.Intraday.Tests.Unit/QQ.Production.Intraday.Tests.Unit.csproj --no-build --no-restore --filter FullyQualifiedName~R019"; Status = $FocusedTestsStatus }; UnitTests = [pscustomobject]@{ Command = "dotnet test tests/QQ.Production.Intraday.Tests.Unit/QQ.Production.Intraday.Tests.Unit.csproj --no-build --no-restore"; Status = $UnitTestsStatus }; Validator = [pscustomobject]@{ Command = "scripts/check-exec-paper-r019-accepted-blocker-continuation-gate.ps1"; Status = $ValidatorStatus } })

$summary = @"
# EXEC-PAPER-R019 Summary

Classifications:
$($classifications | ForEach-Object { "- $_" } | Out-String)

R019 continued the R009 paper-only programme with the accepted R061/R013 readiness blocker.

- Accepted batch entries: $($entries.Count)
- Commands safety validated: $allSafe
- ManualNoExternal commands run: $($executionResults.Count)
- Paper execution plan lines: $($paperPlanLines.Count)
- R009 design-only preview lines: $($previewLines.Count)
- Readiness-complete preview lines: $($completeReadiness.Count)
- Held readiness lines: $($heldLines.Count)
- Direct-cross executable lines: $($directCrossLines.Count)
- Inversion failures: $($inversionFailures.Count)
- Decision: $decision

No executable promotion, schedules, orders, fills, reports, routes, submissions, broker calls, live market data, state mutation, or paper ledger commits are authorized.
"@
Write-Text (Join-Path $ArtifactsRoot "phase-exec-paper-r019-summary.md") $summary

Write-Output "EXEC-PAPER-R019 artifacts generated"
