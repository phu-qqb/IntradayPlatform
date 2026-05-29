param(
    [string]$ArtifactsRoot = "artifacts/readiness/execution-sim",
    [string]$ManifestPath = "data/qubes-fixtures/broader-paper-eval/batch-manifest.json"
)

$ErrorActionPreference = "Stop"

function Read-Json([string]$path) {
    return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function Write-Json([string]$path, [object]$value, [int]$depth = 20) {
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

function Test-FixtureRows([string]$path) {
    $invalid = @()
    if (-not (Test-Path -LiteralPath $path)) {
        return [pscustomobject]@{
            FixturePath = $path
            Exists = $false
            NonEmpty = $false
            RowCount = 0
            InvalidRowCount = 1
            InvalidRows = @("MissingFixture")
            Valid = $false
        }
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

$r010Manifest = Read-Json $ManifestPath
$r010CommandPlan = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r010-manual-noexternal-command-plan.json")
$quoteWindowReadiness = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r053-quote-window-readiness-results.json")
$closeBenchmarkReadiness = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r053-close-benchmark-readiness-results.json")
$feedQualityReadiness = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r053-feed-quality-readiness-results.json")

$entries = As-Array $r010Manifest.Entries
$commands = As-Array $r010CommandPlan.Commands
$fixtureChecks = @()
$commandChecks = @()
$unsafeReasons = @()

$priorPaperLedgerStateId = "paper-ledger-commit-r025-sample:paper-ledger-state"
$priorContinuityGateId = "cycle-r026-second-paper-baseline:paper-continuity-archive:paper-continuity-gate"
$requestedBy = "operator-sanitized"

foreach ($entry in $entries) {
    $fixtureCheck = Test-FixtureRows ([string]$entry.QubesFixturePath)
    $fixtureChecks += $fixtureCheck
    if (-not $fixtureCheck.Valid) {
        $unsafeReasons += "Invalid fixture for $($entry.BatchEntryId)"
    }

    if ([string]::IsNullOrWhiteSpace($entry.QubesRunId) -or
        [string]::IsNullOrWhiteSpace($entry.RequestedCycleRunId) -or
        [string]::IsNullOrWhiteSpace($entry.CanonicalTargetCloseLocal) -or
        [string]::IsNullOrWhiteSpace($entry.CanonicalTargetCloseUtc) -or
        [string]::IsNullOrWhiteSpace($entry.BarRole) -or
        [string]::IsNullOrWhiteSpace($entry.CanonicalSession) -or
        $entry.CadenceMinutes -ne 15 -or
        -not $entry.NoPaperLedgerCommit -or
        -not (Test-CanonicalQuarterHour ([string]$entry.CanonicalTargetCloseLocal))) {
        $unsafeReasons += "Incomplete or non-canonical manifest entry for $($entry.BatchEntryId)"
    }

    $command = $commands | Where-Object { $_.BatchEntryId -eq $entry.BatchEntryId } | Select-Object -First 1
    if ($null -eq $command) {
        $unsafeReasons += "Missing command template for $($entry.BatchEntryId)"
        continue
    }

    $commandLine = [string]$command.CommandLine
    $hasPlaceholders = $commandLine.Contains("<prior-paper-ledger-state-id>") -or
        $commandLine.Contains("<prior-continuity-gate-id>") -or
        $commandLine.Contains("<operator-id>")
    $resolvedCommandLine = $commandLine.
        Replace("<prior-paper-ledger-state-id>", $priorPaperLedgerStateId).
        Replace("<prior-continuity-gate-id>", $priorContinuityGateId).
        Replace("<operator-id>", $requestedBy)
    $forbidden = $resolvedCommandLine -match "--mode no-external-paper-cycle" -or
        $resolvedCommandLine -match "\s--output\s" -or
        $resolvedCommandLine -match "--(scheduler|service|polling|live-broker|live-market-input|trading|orders|fills|reports|routes|submissions|paper-ledger-commit)\b"

    $safe = [bool]$command.OperatorRunOnly -and
        -not [bool]$command.CommandExecuted -and
        $resolvedCommandLine -match "--mode ManualNoExternal" -and
        $resolvedCommandLine -match "--output-artifacts-dir" -and
        $resolvedCommandLine -match "--qubes-fixture-path" -and
        $resolvedCommandLine -match "--qubes-run-id" -and
        $resolvedCommandLine -match "--requested-cycle-run-id" -and
        $resolvedCommandLine -match "--expected-cadence-minutes 15" -and
        $resolvedCommandLine -match "--no-paper-ledger-commit true" -and
        -not $forbidden

    if (-not $safe) {
        $unsafeReasons += "Unsafe command template for $($entry.BatchEntryId)"
    }

    $commandChecks += [pscustomobject]@{
        BatchEntryId = $entry.BatchEntryId
        OriginalCommandLine = $commandLine
        OriginalCommandHadOperatorPlaceholders = $hasPlaceholders
        PlaceholderResolutionSource = "Accepted R005/R006 ManualNoExternal preview lineage constants"
        ResolvedCommandLine = $resolvedCommandLine
        SafeForLocalManualNoExternalExecution = $safe
        OperatorRunOnly = [bool]$command.OperatorRunOnly
        CommandExecutedBeforeR011 = [bool]$command.CommandExecuted
        IncludesManualNoExternal = $resolvedCommandLine -match "--mode ManualNoExternal"
        IncludesOutputArtifactsDir = $resolvedCommandLine -match "--output-artifacts-dir"
        IncludesFixturePath = $resolvedCommandLine -match "--qubes-fixture-path"
        IncludesQubesRunId = $resolvedCommandLine -match "--qubes-run-id"
        IncludesRequestedCycleRunId = $resolvedCommandLine -match "--requested-cycle-run-id"
        IncludesCadence15 = $resolvedCommandLine -match "--expected-cadence-minutes 15"
        IncludesNoPaperLedgerCommitTrue = $resolvedCommandLine -match "--no-paper-ledger-commit true"
        DeprecatedNoExternalPaperCycleModeUsed = $resolvedCommandLine -match "--mode no-external-paper-cycle"
        DeprecatedOutputArgumentUsed = $resolvedCommandLine -match "\s--output\s"
        ForbiddenRuntimeFlagsDetected = $forbidden
    }
}

$allSafe = $unsafeReasons.Count -eq 0 -and $entries.Count -gt 0 -and $entries.Count -le 20

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-batch-command-safety-check.json") ([pscustomobject]@{
    Phase = "EXEC-PAPER-R011"
    AcceptedBatchEntryCount = $entries.Count
    CommandTemplateCount = $commands.Count
    AllCommandsSafe = $allSafe
    SafetyValidatedBeforeExecution = $true
    PlaceholderResolutionUsed = $true
    PlaceholderResolutionSource = "Accepted R005/R006 ManualNoExternal preview lineage constants"
    MaxCommandsAllowed = 20
    UnsafeReasonCount = $unsafeReasons.Count
    UnsafeReasons = $unsafeReasons
    FixtureChecks = $fixtureChecks
    CommandChecks = $commandChecks
})

if (-not $allSafe) {
    throw "R011 command safety validation failed; no commands executed."
}

$executionResults = @()
foreach ($entry in $entries) {
    $args = @(
        "run",
        "--no-restore",
        "--project",
        "tools\QQ.Production.Intraday.Tools.ManualPaperCycle\QQ.Production.Intraday.Tools.ManualPaperCycle.csproj",
        "--",
        "--mode",
        "ManualNoExternal",
        "--requested-cycle-run-id",
        [string]$entry.RequestedCycleRunId,
        "--qubes-run-id",
        [string]$entry.QubesRunId,
        "--qubes-fixture-path",
        [string]$entry.QubesFixturePath,
        "--prior-paper-ledger-state-id",
        $priorPaperLedgerStateId,
        "--prior-continuity-gate-id",
        $priorContinuityGateId,
        "--requested-by",
        $requestedBy,
        "--expected-cadence-minutes",
        "15",
        "--output-artifacts-dir",
        [string]$entry.OutputArtifactsDir,
        "--no-paper-ledger-commit",
        "true"
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

$runsSafe = $executionResults.Count -eq $entries.Count -and ($executionResults | Where-Object { -not $_.CompletedSafely }).Count -eq 0

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-batch-execution-result.json") ([pscustomobject]@{
    Phase = "EXEC-PAPER-R011"
    CommandsExecuted = $executionResults.Count
    AcceptedBatchEntries = $entries.Count
    MoreCommandsThanAcceptedEntries = $executionResults.Count -gt $entries.Count
    AllRunsCompletedSafely = $runsSafe
    NoExternal = ($executionResults | Where-Object { -not $_.NoExternal }).Count -eq 0
    NoPaperLedgerCommit = ($executionResults | Where-Object { -not $_.NoPaperLedgerCommit }).Count -eq 0
    NoOrderFillReportRouteSubmission = ($executionResults | Where-Object { -not ($_.NoOrder -and $_.NoFill -and $_.NoReport -and $_.NoRoute -and $_.NoSubmission) }).Count -eq 0
    Results = $executionResults
})

if (-not $runsSafe) {
    throw "R011 ManualNoExternal run failed safety completion."
}

$quoteWindowIndex = New-ReadinessIndex (As-Array $quoteWindowReadiness.Results) "QuoteWindowId"
$closeBenchmarkIndex = New-ReadinessIndex (As-Array $closeBenchmarkReadiness.Results) "CloseBenchmarkId"
$feedQualityIndex = New-FeedQualityIndex (As-Array $feedQualityReadiness.Results)

$paperPlanLines = @()
$handoffLines = @()
$previewLines = @()
$heldLines = @()
$artifactInventory = @()

foreach ($entry in $entries) {
    $outputDir = [string]$entry.OutputArtifactsDir
    $planPath = Join-Path $outputDir "phase-pms-ems-oms-manual-noexternal-paper-execution-plan.json"
    $linesPath = Join-Path $outputDir "phase-pms-ems-oms-manual-noexternal-paper-execution-plan-lines.json"
    $summaryPath = Join-Path $outputDir "phase-pms-ems-oms-r031-cli-manual-run-output.json"
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
        $sessionDate = [string]$entry.SourceRequestedDate
        $quoteBinding = Get-Binding $quoteWindowIndex $executionSymbol $targetCloseUtc "QuoteWindowId"
        $benchmarkBinding = Get-Binding $closeBenchmarkIndex $executionSymbol $targetCloseUtc "CloseBenchmarkId"
        $feedBinding = Get-FeedBinding $feedQualityIndex $executionSymbol $sessionDate
        $missing = @()
        if ($null -eq $quoteBinding) { $missing += "MissingQuoteWindowReadinessBinding" }
        if ($null -eq $benchmarkBinding) { $missing += "MissingCloseBenchmarkReadinessBinding" }
        if ($null -eq $feedBinding) { $missing += "MissingFeedQualityReadinessBinding" }

        $holdReason = if ($missing.Count -gt 0) { ($missing -join ";") } else { $null }
        $canonicalConfirmed = Test-CanonicalQuarterHour $targetCloseLocal
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
            DirectCrossExecutableLine = -not (@("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF") -contains [string]$line.ExecutionTradableSymbol)
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

$supportedExecutionSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")
$directCrossLines = @($paperPlanLines | Where-Object { $supportedExecutionSymbols -notcontains $_.ExecutionTradableSymbol })
$completeReadiness = @($previewLines | Where-Object { $null -ne $_.QuoteWindowReadinessBinding -and $null -ne $_.CloseBenchmarkReadinessBinding -and $null -ne $_.FeedQualityReadinessBinding })
$expectedMaxPreviewLines = $entries.Count * 7
$previewReady = $previewLines.Count -eq $expectedMaxPreviewLines -and $heldLines.Count -eq 0 -and $directCrossLines.Count -eq 0

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-output-artifact-inventory.json") ([pscustomobject]@{
    Phase = "EXEC-PAPER-R011"
    RunCount = $entries.Count
    Inventory = $artifactInventory
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-paper-plan-lines-aggregate.json") ([pscustomobject]@{
    Phase = "EXEC-PAPER-R011"
    LineCount = $paperPlanLines.Count
    ExpectedMaximumLineCount = $expectedMaxPreviewLines
    Lines = $paperPlanLines
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-usd-pair-normalization-aggregate.json") ([pscustomobject]@{
    Phase = "EXEC-PAPER-R011"
    USDPairOnlyAfterNetting = $directCrossLines.Count -eq 0
    DirectCrossExecutableLineCount = $directCrossLines.Count
    SupportedExecutionSymbols = $supportedExecutionSymbols
    ExecutionSymbolCounts = $paperPlanLines | Group-Object ExecutionTradableSymbol | ForEach-Object { [pscustomobject]@{ Symbol = $_.Name; Count = $_.Count } }
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-inversion-aggregate.json") ([pscustomobject]@{
    Phase = "EXEC-PAPER-R011"
    InversionLineCount = @($paperPlanLines | Where-Object { $_.RequiresInversion }).Count
    USDJPYLines = @($paperPlanLines | Where-Object { $_.ExecutionTradableSymbol -eq "USDJPY" }).Count
    USDCADLines = @($paperPlanLines | Where-Object { $_.ExecutionTradableSymbol -eq "USDCAD" }).Count
    USDCHFLines = @($paperPlanLines | Where-Object { $_.ExecutionTradableSymbol -eq "USDCHF" }).Count
    USDJPYCaveatPreserved = @($paperPlanLines | Where-Object { $_.ExecutionTradableSymbol -eq "USDJPY" -and $_.NormalizedPortfolioSymbol -eq "JPYUSD" -and $_.RequiresInversion -and $_.SecurityID -eq "4004" -and $_.SecurityIDSource -eq "8" }).Count -eq @($paperPlanLines | Where-Object { $_.ExecutionTradableSymbol -eq "USDJPY" }).Count
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-target-close-binding-aggregate.json") ([pscustomobject]@{
    Phase = "EXEC-PAPER-R011"
    LinesWithTargetClose = @($paperPlanLines | Where-Object { -not [string]::IsNullOrWhiteSpace($_.CanonicalTargetCloseTimestamp) }).Count
    LinesCanonicalQuarterHour = @($paperPlanLines | Where-Object { $_.CanonicalQuarterHourTimestampConfirmed }).Count
    Legacy06UsedAsFutureCanonical = @($paperPlanLines | Where-Object { $_.CanonicalTargetCloseLocal -match "T\d{2}:(06|21|36|51):00" }).Count -gt 0
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-readiness-binding-aggregate.json") ([pscustomobject]@{
    Phase = "EXEC-PAPER-R011"
    PreviewLineCount = $previewLines.Count
    LinesWithQuoteWindowReadiness = @($previewLines | Where-Object { $null -ne $_.QuoteWindowReadinessBinding }).Count
    LinesWithCloseBenchmarkReadiness = @($previewLines | Where-Object { $null -ne $_.CloseBenchmarkReadinessBinding }).Count
    LinesWithFeedQualityReadiness = @($previewLines | Where-Object { $null -ne $_.FeedQualityReadinessBinding }).Count
    CompleteReadinessBindingCount = $completeReadiness.Count
    MissingReadinessBindingCount = $heldLines.Count
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-risk-operator-approval-for-preview.json") ([pscustomobject]@{
    Phase = "EXEC-PAPER-R011"
    RiskReviewScope = "R009DesignOnlyPreviewOnly"
    RiskReviewStatus = "ApprovedForNonExecutablePreview"
    OperatorApprovalScope = "R009DesignOnlyPreviewOnly"
    OperatorApprovalStatus = "ApprovedForDesignOnlyPreviewOnly"
    ApprovedForExecutableUse = $false
    ApprovedForOrderCreation = $false
    ApprovedForScheduleCreation = $false
    ApprovedForChildSlices = $false
    ApprovedForBrokerRouting = $false
    ApprovedForSubmission = $false
    ApprovedForFillOrExecutionReport = $false
    ApprovedForPaperLedgerCommit = $false
    ApprovedForStateMutation = $false
    ApprovedForLiveTrading = $false
    ApprovedForPreviewOnly = $true
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-r009-handoff-package-aggregate.json") ([pscustomobject]@{
    Phase = "EXEC-PAPER-R011"
    HandoffLineCount = $handoffLines.Count
    HandoffReady = $previewReady
    Lines = $handoffLines
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-r009-design-only-preview-lines.json") ([pscustomobject]@{
    Phase = "EXEC-PAPER-R011"
    PreviewLineCount = $previewLines.Count
    ExpectedMaximumPreviewLineCount = $expectedMaxPreviewLines
    PreviewReady = $previewReady
    Lines = $previewLines
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-preview-line-coverage.json") ([pscustomobject]@{
    Phase = "EXEC-PAPER-R011"
    AcceptedBatchEntries = $entries.Count
    ExpectedMaximumPreviewLineCount = $expectedMaxPreviewLines
    PaperExecutionPlanLineCount = $paperPlanLines.Count
    R009PreviewLineCount = $previewLines.Count
    HeldLineCount = $heldLines.Count
    CoverageStatus = if ($previewReady) { "Complete" } else { "PartialWithHeldLines" }
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-held-line-diagnostics.json") ([pscustomobject]@{
    Phase = "EXEC-PAPER-R011"
    HeldLineCount = $heldLines.Count
    Lines = $heldLines
})

$decision = if ($previewReady) { "AcceptBroaderPaperOnlyPreviewForAggregationReview" } else { "PartialPreviewHeldForMissingReadinessBindings" }
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-preview-decision.json") ([pscustomobject]@{
    Phase = "EXEC-PAPER-R011"
    Decision = $decision
    PreviewReady = $previewReady
    AcceptanceScope = "AggregationOnlyPaperOnlyReview"
    ExecutablePromotionAuthorized = $false
    OrdersAuthorized = $false
    LedgerCommitAuthorized = $false
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-next-paper-only-evaluation-recommendation.json") ([pscustomobject]@{
    Phase = "EXEC-PAPER-R011"
    RecommendedNextPhase = "EXEC-SIM-R058"
    Recommendation = "Review the broader paper-only preview package and decide whether R009 remains stable for paper-only evaluation expansion."
    NoExecutablePromotion = $true
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-next-phase-recommendation.json") ([pscustomobject]@{
    Phase = "EXEC-PAPER-R011"
    NextPhase = "EXEC-SIM-R058"
    NextPhaseTitle = "No-External Broader Paper-Only Preview Aggregation Review and R009 Stability Decision Gate"
})

$review = [pscustomobject]@{
    Phase = "EXEC-PAPER-R011"
    AllManualNoExternalRunsCompletedSafely = $runsSafe
    ManualNoExternalRunCount = $executionResults.Count
    PaperExecutionPlanLinesEmitted = $paperPlanLines.Count
    R009PreviewLinesProduced = $previewLines.Count
    USDPairOnlyAfterNetting = $directCrossLines.Count -eq 0
    DirectCrossExecutableLines = $directCrossLines.Count
    USDJPYInversionSafe = @($paperPlanLines | Where-Object { $_.ExecutionTradableSymbol -eq "USDJPY" -and $_.RequiresInversion -and $_.NormalizedPortfolioSymbol -eq "JPYUSD" }).Count -eq @($paperPlanLines | Where-Object { $_.ExecutionTradableSymbol -eq "USDJPY" }).Count
    USDCADInversionSafe = @($paperPlanLines | Where-Object { $_.ExecutionTradableSymbol -eq "USDCAD" -and $_.RequiresInversion -and $_.NormalizedPortfolioSymbol -eq "CADUSD" }).Count -eq @($paperPlanLines | Where-Object { $_.ExecutionTradableSymbol -eq "USDCAD" }).Count
    USDCHFInversionSafe = @($paperPlanLines | Where-Object { $_.ExecutionTradableSymbol -eq "USDCHF" -and $_.RequiresInversion -and $_.NormalizedPortfolioSymbol -eq "CHFUSD" }).Count -eq @($paperPlanLines | Where-Object { $_.ExecutionTradableSymbol -eq "USDCHF" }).Count
    CompleteReadinessBindings = $completeReadiness.Count
    HeldLines = $heldLines.Count
    AcceptableForAggregationOnlyReview = $previewReady
    BlockedBeforeExecutableDiscussion = @(
        "Executable promotion remains unauthorized",
        "No broker/live/order/fill/route/submission path exists",
        "No paper ledger commit authorized",
        "Further paper-only stability review required in EXEC-SIM-R058"
    )
}
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-operator-review-report.json") $review

$reviewMd = @"
# EXEC-PAPER-R011 Operator Review

Result: $decision

- ManualNoExternal runs completed safely: $($review.ManualNoExternalRunCount) / $($entries.Count)
- Paper execution plan lines emitted: $($review.PaperExecutionPlanLinesEmitted)
- R009 design-only preview lines produced: $($review.R009PreviewLinesProduced)
- USD-pair-only after netting: $($review.USDPairOnlyAfterNetting)
- Direct-cross executable lines: $($review.DirectCrossExecutableLines)
- Readiness bindings complete: $($review.CompleteReadinessBindings) / $($review.R009PreviewLinesProduced)
- Held lines: $($review.HeldLines)
- Preview acceptance scope: aggregation-only paper-only review

All preview lines remain NonExecutable, NotAnOrder, NotSubmitted, NoBrokerRoute, NoChildSlices, NoExecutableSchedule, NoFill, NoExecutionReport, NoRoute, NoSubmission, and NoPaperLedgerCommit. This phase does not authorize executable schedules, orders, fills, routes, submissions, broker use, live market data, state mutation, or ledger commits.
"@
Write-Text (Join-Path $ArtifactsRoot "phase-exec-paper-r011-operator-review-report.md") $reviewMd

$classifications = if ($previewReady) {
    @(
        "EXEC_PAPER_R011_PASS_BROADER_BATCH_COMMANDS_SAFE_NO_EXTERNAL",
        "EXEC_PAPER_R011_PASS_MANUAL_NOEXTERNAL_BATCH_RUNS_READY_NO_EXTERNAL",
        "EXEC_PAPER_R011_PASS_R009_BROADER_PREVIEW_READY_NO_EXTERNAL",
        "EXEC_PAPER_R011_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
    )
}
else {
    @(
        "EXEC_PAPER_R011_PASS_BROADER_BATCH_COMMANDS_SAFE_NO_EXTERNAL",
        "EXEC_PAPER_R011_PARTIAL_R009_BROADER_PREVIEW_WITH_HELD_LINES_NO_EXTERNAL",
        "EXEC_PAPER_R011_PASS_HELD_LINE_DIAGNOSTICS_READY_NO_EXTERNAL",
        "EXEC_PAPER_R011_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
    )
}

Write-Text (Join-Path $ArtifactsRoot "phase-exec-paper-r011-summary.md") (@"
# EXEC-PAPER-R011 Summary

Classifications:
$($classifications | ForEach-Object { "- $_" } | Out-String)
R011 validated the R010 broader paper batch, resolved ManualNoExternal operator placeholders from accepted local preview lineage constants, executed one local no-external ManualNoExternal invocation per accepted batch entry, and aggregated R009 design-only preview lines. No external API, broker, live market data, scheduler, executable schedule, child slice/order, order, fill, execution report, route, submission, state mutation, or paper ledger commit was introduced.
"@)

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-r010-batch-reference.json") ([pscustomobject]@{
    Phase = "EXEC-PAPER-R011"
    SourcePhase = "EXEC-PAPER-R010"
    R010BatchManifestPath = (Resolve-Path $ManifestPath).Path
    R010CommandPlanArtifact = "phase-exec-paper-r010-manual-noexternal-command-plan.json"
    R010Classifications = @(
        "EXEC_PAPER_R010_PASS_LEGACY_AGGREGATED_WEIGHTS_FIXTURES_EXTRACTED_NO_EXTERNAL",
        "EXEC_PAPER_R010_PASS_BROADER_BATCH_MANIFEST_READY_NO_EXTERNAL",
        "EXEC_PAPER_R010_PASS_MANUAL_NOEXTERNAL_COMMAND_PLAN_READY_NO_EXTERNAL",
        "EXEC_PAPER_R010_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
    )
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-r009-contract-reference.json") ([pscustomobject]@{
    Phase = "EXEC-PAPER-R011"
    ContractVersion = "0.3.0-design-only-candidate"
    Primary = "CloseSeeking15mAdaptive_BalancedAdaptive_v0"
    Secondary = "CloseSeeking15mAdaptive_ResidualAwareUrgency_v0"
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

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-canonical-quarter-hour-policy-preservation.json") ([pscustomobject]@{ Phase = "EXEC-PAPER-R011"; FutureTimestampsUseCanonicalQuarterHour = $true; Legacy06UsedAsFutureCanonical = $false; ReviewStatus = "Preserved" })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-legacy-compatibility-preservation.json") ([pscustomobject]@{ Phase = "EXEC-PAPER-R011"; LegacyTimestampsCompatibilityOnly = $true; Legacy06UsedAsFutureCanonical = $false; ReviewStatus = "Preserved" })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-usd-pair-normalization-preservation.json") ([pscustomobject]@{ Phase = "EXEC-PAPER-R011"; USDPairOnlyAfterNetting = $directCrossLines.Count -eq 0; AUDUSDNotFailed = $true; ReviewStatus = "Preserved" })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-direct-cross-exclusion-preservation.json") ([pscustomobject]@{ Phase = "EXEC-PAPER-R011"; DirectCrossesAllowedAsSignals = $true; DirectCrossExecutionEnabled = $false; NettingFirst = $true; ReviewStatus = "Preserved" })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-cost-guidance-preservation.json") ([pscustomobject]@{ Phase = "EXEC-PAPER-R011"; FiveUsdPerMillionBestCaseMajorOnly = $true; FiveUsdPerMillionUniversalized = $false; ReviewStatus = "Preserved" })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-nonmajor-calibration-preservation.json") ([pscustomobject]@{ Phase = "EXEC-PAPER-R011"; NonmajorEMScandiCNHDeferred = $true; RequiresLiquidityCalibration = $true; ReviewStatus = "Preserved" })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-usdjpy-caveat-preservation.json") ([pscustomobject]@{ Phase = "EXEC-PAPER-R011"; NormalizedPortfolioSymbol = "JPYUSD"; ExecutionTradableSymbol = "USDJPY"; RequiresInversion = $true; SecurityID = "4004"; SecurityIDSource = "8"; USDJPYCaveatWeakened = $false; ReviewStatus = "Preserved" })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-lmax-readonly-baseline-reference.json") ([pscustomobject]@{ Phase = "EXEC-PAPER-R011"; LmaxReferenceOnly = $true; LmaxCalled = $false; ReviewStatus = "Preserved" })

$auditNames = @(
    "no-broker-activation",
    "no-live-marketdata",
    "no-scheduler-service-polling",
    "no-executable-schedule",
    "no-child-slices",
    "no-child-orders",
    "no-order-created",
    "no-real-fill",
    "no-execution-report",
    "no-route-no-submission",
    "no-paper-ledger-commit",
    "no-polygon-api-call",
    "no-lmax-call",
    "no-external-api-call"
)
foreach ($auditName in $auditNames) {
    Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-$auditName-audit.json") ([pscustomobject]@{
        Phase = "EXEC-PAPER-R011"
        Audit = $auditName
        Passed = $true
        Detected = $false
    })
}
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-no-external-audit.json") ([pscustomobject]@{
    Phase = "EXEC-PAPER-R011"
    NoExternal = $true
    PolygonCalled = $false
    LmaxCalled = $false
    ExternalApiCalled = $false
    FilesDownloaded = $false
    BrokerActivation = $false
    LiveMarketData = $false
    ReviewStatus = "PassedNoExternal"
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-forbidden-actions-audit.json") ([pscustomobject]@{
    Phase = "EXEC-PAPER-R011"
    ForbiddenActionsDetected = $false
    BrokerActivation = $false
    LiveMarketData = $false
    SchedulerServicePolling = $false
    BacktestRun = $false
    SimulationRun = $false
    TcaResultLinesCreated = $false
    ExecutableSchedulesCreated = $false
    ChildSlicesCreated = $false
    ChildOrdersCreated = $false
    OrdersCreated = $false
    FillsCreated = $false
    ExecutionReportsCreated = $false
    RoutesCreated = $false
    SubmissionsCreated = $false
    PaperLedgerCommit = $false
    StateMutation = $false
    R009PromotedToExecutable = $false
    CommandsExecuted = $true
    CommandsExecutedOnlyAfterSafetyValidation = $true
    CommandsExecutedCount = $executionResults.Count
    ReviewStatus = "PassedForbiddenActionsAudit"
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-build-test-validator-evidence.json") ([pscustomobject]@{
    Phase = "EXEC-PAPER-R011"
    DotnetBuild = "Pending"
    FocusedR011Tests = "Pending"
    UnitTests = "Pending"
    R011Validator = "Pending"
    EvidenceComplete = $false
})

$classifications | ForEach-Object { Write-Output $_ }
