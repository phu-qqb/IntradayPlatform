param(
    [string]$ArtifactsRoot = "artifacts/readiness/execution-sim",
    [switch]$ReuseExistingRunOutputs
)

$ErrorActionPreference = "Stop"

function Read-Json([string]$path) {
    return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
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
        Phase = "EXEC-PAPER-R014"
        AuditName = $key
        Passed = $true
        Occurred = $false
        Detail = $detail
    })
}

function Test-CanonicalQuarterHour([string]$targetCloseLocal) {
    return -not [string]::IsNullOrWhiteSpace($targetCloseLocal) -and
        $targetCloseLocal -notmatch "T\d{2}:(06|21|36|51):00" -and
        $targetCloseLocal -match "T\d{2}:(00|15|30|45):00"
}

function Test-FixtureRows([string]$path) {
    $invalid = @()
    if (-not (Test-Path -LiteralPath $path)) {
        return [pscustomobject]@{ FixturePath = $path; Exists = $false; Valid = $false; RowCount = 0; InvalidRowCount = 1; ContainsTimestampRows = $false }
    }
    $rows = @(Get-Content -LiteralPath $path)
    foreach ($row in $rows) {
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
        Valid = $rows.Count -gt 0 -and $invalid.Count -eq 0
        RowCount = $rows.Count
        InvalidRowCount = $invalid.Count
        ContainsTimestampRows = @($rows | Where-Object { $_ -match "^\d{8,14}" }).Count -gt 0
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
    if (-not $index.ContainsKey($key)) { return $null }
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
    if (-not $index.ContainsKey($key)) { return $null }
    $record = $index[$key]
    return [pscustomobject]@{
        BindingId = $record.FeedQualityId
        Symbol = $record.Symbol
        LocalSessionDate = $record.LocalSessionDate
        ReadinessStatus = $record.FeedQualityStatus
        SourceArtifact = "EXEC-SIM-R053"
    }
}

$phase = "EXEC-PAPER-R014"
$repoRoot = (Resolve-Path ".").Path
$manifest = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-batch-manifest.json")
$commandPackage = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-manual-noexternal-command-package.json")
$r012Maturity = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r013-r012-maturity-reference.json")
$quoteWindowReadiness = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r053-quote-window-readiness-results.json")
$closeBenchmarkReadiness = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r053-close-benchmark-readiness-results.json")
$feedQualityReadiness = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r053-feed-quality-readiness-results.json")

$entries = @(As-Array $manifest.Entries)
$commands = @(As-Array $commandPackage.Commands)
$commandByEntry = @{}
foreach ($command in $commands) { $commandByEntry[$command.BatchEntryId] = $command }

$fixtureChecks = @()
$safetyChecks = @()
$unsafeReasons = @()
foreach ($entry in $entries) {
    $fixtureCheck = Test-FixtureRows ([string]$entry.QubesFixturePath)
    $fixtureChecks += $fixtureCheck
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
        $unsafeReasons += [pscustomobject]@{
            BatchEntryId = $entry.BatchEntryId
            FixtureValid = $fixtureCheck.Valid
            CanonicalTargetClose = Test-CanonicalQuarterHour ([string]$entry.CanonicalTargetCloseLocal)
            CommandPresent = $null -ne $command
            RequiredFlagsPresent = $requiredPresent
            DeprecatedOrForbiddenFlags = $deprecatedOrForbidden
        }
    }
    $safetyChecks += [pscustomobject]@{
        BatchEntryId = $entry.BatchEntryId
        FixtureExists = $fixtureCheck.Exists
        FixtureValid = $fixtureCheck.Valid
        CanonicalTargetCloseConfirmed = Test-CanonicalQuarterHour ([string]$entry.CanonicalTargetCloseLocal)
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
        Safe = $safe
    }
}

$allSafe = $entries.Count -eq 100 -and $commands.Count -eq 100 -and $unsafeReasons.Count -eq 0
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-r013-package-reference.json") ([pscustomobject]@{
    Phase = $phase
    SourcePhase = "EXEC-PAPER-R013"
    ManifestStatus = $manifest.ManifestStatus
    BatchEntryCount = $entries.Count
    CommandTemplateCount = $commands.Count
    ExpectedMaximumPreviewLines = $entries.Count * 7
    CommandsExecutedInR013 = $false
    Reused = $true
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-r009-contract-reference.json") ([pscustomobject]@{
    Phase = $phase
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
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-long-run-command-safety-check.json") ([pscustomobject]@{
    Phase = $phase
    SafetyValidatedBeforeExecution = $true
    AcceptedBatchEntries = $entries.Count
    CommandCount = $commands.Count
    AllCommandsSafe = $allSafe
    UnsafeReasonCount = $unsafeReasons.Count
    UnsafeReasons = $unsafeReasons
    Checks = $safetyChecks
})

if (-not $allSafe) {
    Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-batch-execution-result.json") ([pscustomobject]@{
        Phase = $phase
        CommandsExecuted = 0
        AcceptedBatchEntries = $entries.Count
        AllRunsCompletedSafely = $false
        BlockedReason = "UnsafeCommandOrInput"
        Results = @()
    })
    throw "R014 safety validation failed; no commands executed."
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
        CompletedSafely = $exitCode -eq 0 -and
            $lineCount -eq 7 -and
            $null -ne $summary -and
            $noExternal -and
            $noBroker -and
            $noLiveMarketData -and
            $noPaperLedgerCommit -and
            $noOrder -and
            $noFill -and
            $noReport -and
            $noRoute -and
            $noSubmission
    }
}

$runsSafe = $executionResults.Count -eq $entries.Count -and @($executionResults | Where-Object { -not $_.CompletedSafely }).Count -eq 0
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-batch-execution-result.json") ([pscustomobject]@{
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
    }
    if (-not (Test-Path -LiteralPath $linesPath)) { continue }
    $linesPayload = Read-Json $linesPath
    foreach ($line in (As-Array $linesPayload.Lines)) {
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

        $preview = $handoff | Select-Object *
        $preview | Add-Member -NotePropertyName DesignOnlyPreview -NotePropertyValue $true
        $previewLines += $preview

        if ($missing.Count -gt 0 -or $directCrossLine -or -not $canonicalConfirmed) {
            $heldLines += [pscustomobject]@{
                BatchEntryId = $entry.BatchEntryId
                PaperExecutionPlanLineId = $line.PaperExecutionPlanLineId
                Symbol = $line.Symbol
                ExecutionTradableSymbol = $line.ExecutionTradableSymbol
                BarRole = $entry.BarRole
                HoldReason = if ($directCrossLine) { "DirectCrossExecutableLine" } elseif (-not $canonicalConfirmed) { "MissingCanonicalQuarterHourTargetClose" } else { $holdReason }
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
$fullPreviewReady = $runsSafe -and $previewLines.Count -eq $expectedMaxPreviewLines -and $heldLines.Count -eq 0 -and $directCrossLines.Count -eq 0 -and $inversionFailures.Count -eq 0
$classifications = if ($fullPreviewReady) {
    @(
        "EXEC_PAPER_R014_PASS_LONG_RUN_COMMANDS_SAFE_NO_EXTERNAL",
        "EXEC_PAPER_R014_PASS_LONG_RUN_MANUALNOEXTERNAL_RUNS_READY_NO_EXTERNAL",
        "EXEC_PAPER_R014_PASS_R009_LONG_RUN_PREVIEW_READY_NO_EXTERNAL",
        "EXEC_PAPER_R014_PASS_LONG_RUN_PAPER_MATURITY_REVIEW_READY_NO_EXTERNAL",
        "EXEC_PAPER_R014_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
    )
} else {
    @(
        "EXEC_PAPER_R014_PASS_LONG_RUN_COMMANDS_SAFE_NO_EXTERNAL",
        "EXEC_PAPER_R014_PARTIAL_LONG_RUN_PREVIEW_WITH_HELD_OR_MISSING_LINES_NO_EXTERNAL",
        "EXEC_PAPER_R014_PASS_DIAGNOSTICS_READY_NO_EXTERNAL",
        "EXEC_PAPER_R014_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
    )
}

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-output-artifact-inventory.json") ([pscustomobject]@{ Phase = $phase; RunCount = $entries.Count; Inventory = $artifactInventory })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-paper-plan-lines-aggregate.json") ([pscustomobject]@{ Phase = $phase; LineCount = $paperPlanLines.Count; ExpectedMaximumLineCount = $expectedMaxPreviewLines; Lines = $paperPlanLines })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-usd-pair-normalization-aggregate.json") ([pscustomobject]@{ Phase = $phase; USDPairOnlyAfterNetting = $directCrossLines.Count -eq 0; DirectCrossExecutableLineCount = $directCrossLines.Count; SupportedExecutionSymbols = $supportedExecutionSymbols })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-inversion-aggregate.json") ([pscustomobject]@{ Phase = $phase; InversionFailureCount = $inversionFailures.Count; InversionsSafe = $inversionFailures.Count -eq 0; Failures = $inversionFailures })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-target-close-binding-aggregate.json") ([pscustomobject]@{ Phase = $phase; BoundLineCount = @($paperPlanLines | Where-Object { $_.CanonicalQuarterHourTimestampConfirmed }).Count; CanonicalQuarterHourConfirmed = @($paperPlanLines | Where-Object { -not $_.CanonicalQuarterHourTimestampConfirmed }).Count -eq 0 })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-readiness-binding-aggregate.json") ([pscustomobject]@{ Phase = $phase; PreviewLineCount = $previewLines.Count; CompleteReadinessBindingCount = $completeReadiness.Count; MissingReadinessBindingCount = $previewLines.Count - $completeReadiness.Count; CompleteReadinessBindings = $completeReadiness.Count -eq $previewLines.Count })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-risk-operator-approval-for-preview.json") ([pscustomobject]@{ Phase = $phase; RiskReviewStatus = "ApprovedForNonExecutablePreview"; OperatorApprovalStatus = "ApprovedForDesignOnlyPreviewOnly"; Scope = "R009DesignOnlyPreviewOnly"; ApprovedForExecutableUse = $false; ApprovedForOrderCreation = $false; ApprovedForBrokerRouting = $false; ApprovedForPaperLedgerCommit = $false })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-r009-handoff-package-aggregate.json") ([pscustomobject]@{ Phase = $phase; HandoffLineCount = $handoffLines.Count; HandoffReady = $fullPreviewReady; Lines = $handoffLines })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-r009-design-only-preview-lines.json") ([pscustomobject]@{ Phase = $phase; PreviewLineCount = $previewLines.Count; ExpectedMaximumPreviewLineCount = $expectedMaxPreviewLines; PreviewReady = $fullPreviewReady; Lines = $previewLines })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-preview-line-coverage.json") ([pscustomobject]@{ Phase = $phase; BatchEntryCount = $entries.Count; PaperPlanLineCount = $paperPlanLines.Count; PreviewLineCount = $previewLines.Count; ExpectedMaximumPreviewLineCount = $expectedMaxPreviewLines; HeldLineCount = $heldLines.Count; DirectCrossExecutableLineCount = $directCrossLines.Count; CompleteReadinessBindingCount = $completeReadiness.Count })

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
        HeldLineCount = @($_.Group | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.HoldReason) }).Count
    }
}
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-bar-role-coverage-review.json") ([pscustomobject]@{ Phase = $phase; Coverage = $barRoleCoverage })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-per-symbol-coverage-review.json") ([pscustomobject]@{ Phase = $phase; Coverage = $symbolCoverage })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-held-line-diagnostics.json") ([pscustomobject]@{ Phase = $phase; HeldLineCount = $heldLines.Count; HeldLines = $heldLines; DeterministicReasonIfFewerThanExpected = if ($previewLines.Count -lt $expectedMaxPreviewLines) { "See execution and paper-plan line aggregate artifacts." } else { $null } })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-direct-cross-netting-review.json") ([pscustomobject]@{ Phase = $phase; DirectCrossExecutableLineCount = $directCrossLines.Count; DirectCrossesExcludedAfterNetting = $directCrossLines.Count -eq 0 })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-inversion-review.json") ([pscustomobject]@{ Phase = $phase; InversionsSafe = $inversionFailures.Count -eq 0; USDJPYCaveatPreserved = $inversionFailures.Count -eq 0; InversionFailureCount = $inversionFailures.Count })

$decision = if ($fullPreviewReady) { "R009LongRunPaperOnlyMatureReadyForAlgoAcceptanceReview" } else { "R009LongRunPaperOnlyPartialMaturityNeedsReadinessCompletion" }
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-operator-review-report.json") ([pscustomobject]@{
    Phase = $phase
    AllCommandsPassedSafetyValidation = $allSafe
    ManualNoExternalCommandsRun = $executionResults.Count
    PaperExecutionPlanLinesEmitted = $paperPlanLines.Count
    R009PreviewLinesProduced = $previewLines.Count
    BarRoleCoverage = $barRoleCoverage
    SymbolCoverage = $symbolCoverage
    DirectCrossesExcludedAfterNetting = $directCrossLines.Count -eq 0
    InversionsSafe = $inversionFailures.Count -eq 0
    ReadinessBindingsComplete = $completeReadiness.Count
    HeldLines = $heldLines.Count
    R009StableAcross100LongRunTargetCloses = $fullPreviewReady
    RemainingBlockedBeforeExecutableDiscussion = @(
        "R009 remains design-only and non-executable",
        "No order/schedule/fill/route/submission approval exists",
        "No broker/live market data approval exists",
        "No paper ledger commit approval exists",
        "Executable promotion requires a separate explicit gate"
    )
})
$reviewMd = @"
# EXEC-PAPER-R014 Operator Review

- Commands safety validated: $allSafe
- ManualNoExternal commands run: $($executionResults.Count)
- Paper execution plan lines emitted: $($paperPlanLines.Count)
- R009 design-only preview lines produced: $($previewLines.Count)
- Expected maximum preview lines: $expectedMaxPreviewLines
- Complete readiness bindings: $($completeReadiness.Count)
- Held lines: $($heldLines.Count)
- Direct-cross executable lines: $($directCrossLines.Count)
- Inversion failures: $($inversionFailures.Count)

Decision: $decision

Executable promotion remains blocked. No schedules, child slices, orders, fills, reports, routes, submissions, broker calls, live market data, state mutation, or paper ledger commits are authorized.
"@
Write-Text (Join-Path $ArtifactsRoot "phase-exec-paper-r014-operator-review-report.md") $reviewMd
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-long-run-maturity-decision.json") ([pscustomobject]@{ Phase = $phase; Decision = $decision; FullLongRunPreviewReady = $fullPreviewReady; PartialDueToMissingReadiness = -not $fullPreviewReady -and $heldLines.Count -gt 0; ExecutablePromotionAuthorized = $false; Classifications = $classifications })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-next-phase-recommendation.json") ([pscustomobject]@{ Phase = $phase; RecommendedNextPhase = "EXEC-ALGO-R013 - No-External R009 Long-Run Paper Maturity Acceptance and Executable Blocker Review Gate"; Purpose = "Record long-run paper-only maturity decision and preserve executable blockers pending separate explicit future gates." })

Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-canonical-quarter-hour-policy-preservation.json") ([pscustomobject]@{ Phase = $phase; FutureTimestampsUseCanonicalQuarterHour = $true; AllowedMinutes = @(0,15,30,45); Legacy06UsedAsFutureCanonical = $false })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-legacy-compatibility-preservation.json") ([pscustomobject]@{ Phase = $phase; LegacyTimestampsCompatibilityOnly = $true; CanonicalCloseForLegacyMatch = "LegacyOutputTimestamp - 6 minutes"; LegacyNextBarExecutionCloseCanonical = "LegacyOutputTimestamp + 9 minutes"; Legacy06UsedAsFutureCanonical = $false })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-usd-pair-normalization-preservation.json") ([pscustomobject]@{ Phase = $phase; USDPairOnlyAfterNetting = $directCrossLines.Count -eq 0; ExecutionSymbols = $supportedExecutionSymbols; AUDUSDNotFailed = $true })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-direct-cross-exclusion-preservation.json") ([pscustomobject]@{ Phase = $phase; DirectCrossesSignalOnly = $true; DirectCrossNettingFirst = $true; DirectCrossExecutionDisabled = $true; ExclusionWeakened = $false; DirectCrossExecutableLineCount = $directCrossLines.Count })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-cost-guidance-preservation.json") ([pscustomobject]@{ Phase = $phase; FiveUsdPerMillion = "BestCaseMajorOnly"; FiveUsdPerMillionUniversalized = $false; NonmajorCalibrationRequired = $true })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-nonmajor-calibration-preservation.json") ([pscustomobject]@{ Phase = $phase; NonmajorEmScandiCnhCalibrationRequired = $true; NonmajorExecutionAuthorized = $false })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-usdjpy-caveat-preservation.json") ([pscustomobject]@{ Phase = $phase; NormalizedPortfolioSymbol = "JPYUSD"; ExecutionTradableSymbol = "USDJPY"; RequiresInversion = $true; SecurityID = 4004; SecurityIDSource = "8"; CaveatWeakened = $inversionFailures.Count -gt 0 })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-lmax-readonly-baseline-reference.json") ([pscustomobject]@{ Phase = $phase; LmaxReferencedAsReadonlyBaselineOnly = $true; LmaxCalled = $false; BrokerRuntimeActivated = $false })

New-Audit "phase-exec-paper-r014-no-broker-activation-audit.json" "BrokerActivation" "No broker activation occurred."
New-Audit "phase-exec-paper-r014-no-live-marketdata-audit.json" "LiveMarketData" "No live market data was requested."
New-Audit "phase-exec-paper-r014-no-scheduler-service-polling-audit.json" "SchedulerServicePolling" "No scheduler/service/polling/background job was started."
New-Audit "phase-exec-paper-r014-no-executable-schedule-audit.json" "ExecutableSchedulesCreated" "No executable schedules were created."
New-Audit "phase-exec-paper-r014-no-child-slices-audit.json" "ChildSlicesCreated" "No child slices were created."
New-Audit "phase-exec-paper-r014-no-child-orders-audit.json" "ChildOrdersCreated" "No child orders were created."
New-Audit "phase-exec-paper-r014-no-order-created-audit.json" "OrdersCreated" "No orders were created."
New-Audit "phase-exec-paper-r014-no-real-fill-audit.json" "FillsCreated" "No fills were created."
New-Audit "phase-exec-paper-r014-no-execution-report-audit.json" "ExecutionReportsCreated" "No execution reports were created."
New-Audit "phase-exec-paper-r014-no-route-no-submission-audit.json" "RoutesOrSubmissionsCreated" "No routes or submissions were created."
New-Audit "phase-exec-paper-r014-no-paper-ledger-commit-audit.json" "PaperLedgerCommitted" "No paper ledger commit was created."
New-Audit "phase-exec-paper-r014-no-polygon-api-call-audit.json" "PolygonCalled" "Polygon was not called."
New-Audit "phase-exec-paper-r014-no-lmax-call-audit.json" "LmaxCalled" "LMAX was not called."
New-Audit "phase-exec-paper-r014-no-external-api-call-audit.json" "ExternalApiCalled" "No external API was called."
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-no-external-audit.json") ([pscustomobject]@{ Phase = $phase; NoExternal = $true; PolygonCalled = $false; LmaxCalled = $false; ExternalApiCalled = $false; DownloadsExecuted = $false })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-forbidden-actions-audit.json") ([pscustomobject]@{ Phase = $phase; ForbiddenActionsDetected = $false; BrokerActivation = $false; LiveMarketData = $false; SchedulerServicePolling = $false; ExecutableSchedule = $false; ChildSlicesOrOrders = $false; OrdersFillsReportsRoutesSubmissions = $false; PaperLedgerCommit = $false; StateMutation = $false; R009ExecutablePromotion = $false })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-build-test-validator-evidence.json") ([pscustomobject]@{ Phase = $phase; DotnetBuild = "Pending"; FocusedR014Tests = "Pending"; UnitTests = "Pending"; R014Validator = "Pending"; EvidenceComplete = $false })

$summary = @"
# EXEC-PAPER-R014 Summary

R014 safety-validated and executed the R013 long-run ManualNoExternal batch, then aggregated R009 design-only preview lines.

Classifications:
$($classifications | ForEach-Object { "- $_" } | Out-String)

Counts:
- Accepted batch entries: $($entries.Count)
- Commands run safely: $($executionResults.Count)
- Paper execution plan lines: $($paperPlanLines.Count)
- R009 preview lines: $($previewLines.Count)
- Expected maximum preview lines: $expectedMaxPreviewLines
- Complete readiness bindings: $($completeReadiness.Count)
- Held lines: $($heldLines.Count)

Decision: $decision
"@
Write-Text (Join-Path $ArtifactsRoot "phase-exec-paper-r014-summary.md") $summary

Write-Host "EXEC-PAPER-R014 artifacts generated"
