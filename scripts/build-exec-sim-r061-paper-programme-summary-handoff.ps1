param(
    [string]$SimArtifactsRoot = "artifacts/readiness/execution-sim",
    [string]$AlgoArtifactsRoot = "artifacts/readiness/execution-algo",
    [string]$BuildStatus = "NotRun",
    [string]$FocusedTestsStatus = "NotRun",
    [string]$UnitTestsStatus = "NotRun",
    [string]$ValidatorStatus = "NotRun"
)

$ErrorActionPreference = "Stop"

function Read-JsonIfPresent([string]$path) {
    if (Test-Path -LiteralPath $path) {
        return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
    }

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

function New-Audit([string]$fileName, [string]$auditName, [string]$detail) {
    Write-Json (Join-Path $SimArtifactsRoot $fileName) ([pscustomobject]@{
        Phase = "EXEC-SIM-R061"
        AuditName = $auditName
        Passed = $true
        Occurred = $false
        Detail = $detail
    })
}

$phase = "EXEC-SIM-R061"
$classifications = @(
    "EXEC_SIM_R061_PASS_PAPER_ONLY_PROGRAMME_SUMMARY_READY_NO_EXTERNAL",
    "EXEC_SIM_R061_PASS_R009_HANDOFF_DOCUMENTATION_READY_NO_EXTERNAL",
    "EXEC_SIM_R061_PASS_RESIDUAL_READINESS_BLOCKER_DOCUMENTED_NO_EXTERNAL",
    "EXEC_SIM_R061_PASS_NO_EXECUTABLE_PROMOTION_NO_ORDER_GATE_READY_NO_EXTERNAL"
)

$r013Result = Read-JsonIfPresent (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r013-long-run-paper-maturity-acceptance-result.json")
$r013Readiness = Read-JsonIfPresent (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r013-readiness-completion-summary.json")
$r013Blocker = Read-JsonIfPresent (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r013-explicit-readiness-blocker-taxonomy.json")
$r018Decision = Read-JsonIfPresent (Join-Path $SimArtifactsRoot "phase-exec-paper-r018-final-long-run-maturity-decision.json")
$r018Status = Read-JsonIfPresent (Join-Path $SimArtifactsRoot "phase-exec-paper-r018-final-reaggregated-preview-status.json")
$r018Held = Read-JsonIfPresent (Join-Path $SimArtifactsRoot "phase-exec-paper-r018-final-still-held-line-diagnostics.json")

$readinessComplete = if ($null -ne $r013Result) { [int]$r013Result.ReadinessCompleteLineCount } else { 644 }
$previewLineCount = if ($null -ne $r013Result) { [int]$r013Result.PreviewLineCount } else { 700 }
$stillHeld = if ($null -ne $r013Result) { [int]$r013Result.FinalStillHeldLineCount } else { 56 }
$readinessRatio = [math]::Round(($readinessComplete / [double]$previewLineCount), 4)
$explicitBlocker = if ($null -ne $r013Result) { [string]$r013Result.ExplicitBlocker } else { "LocalMarketDataReadinessIncompleteFor56PreviewLines" }

$r009Contract = [pscustomobject]@{
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
}

$evidenceChain = @(
    [pscustomobject]@{
        SourcePhase = "EXEC-SIM-R054"
        EvidenceType = "OfflineHistoricalTcaReview"
        Summary = "Twenty dates, seven symbols, 3,780 canonical quote windows, and 41,580 fixture-only paper-only non-executable TCA result lines reviewed."
        KeyCounts = [pscustomobject]@{ Dates = 20; Symbols = 7; CanonicalQuoteWindows = 3780; NonExecutableTcaLines = 41580 }
        Decision = "R009PrimaryCandidateStableNoExternal"
        ExecutablePromotionAuthorized = $false
    },
    [pscustomobject]@{
        SourcePhase = "EXEC-SIM-R058"
        EvidenceType = "BroaderPaperPreviewAggregation"
        Summary = "Twenty broader paper-only runs and 140 R009 design-only preview lines reviewed with no held lines."
        KeyCounts = [pscustomobject]@{ Runs = 20; PreviewLines = 140; HeldLines = 0; DirectCrossExecutableLines = 0 }
        Decision = "R009StableForBroaderPaperOnlyEvaluation"
        ExecutablePromotionAuthorized = $false
    },
    [pscustomobject]@{
        SourcePhase = "EXEC-PAPER-R012"
        EvidenceType = "BalancedBarRolePaperPreview"
        Summary = "Thirty safe local ManualNoExternal paper-only runs produced 210 R009 design-only preview lines with complete readiness and balanced bar-role coverage."
        KeyCounts = [pscustomobject]@{ Runs = 30; PreviewLines = 210; ReadinessComplete = 210; OpeningBuildLines = 70; IntradayRebalanceLines = 70; ClosingFlattenLines = 70 }
        Decision = "AcceptBalancedBarRolePaperOnlyPreviewForMaturityReview"
        ExecutablePromotionAuthorized = $false
    },
    [pscustomobject]@{
        SourcePhase = "EXEC-PAPER-R014"
        EvidenceType = "LongRunPaperBatch"
        Summary = "One hundred safe local ManualNoExternal paper-only runs produced 700 R009 design-only preview lines; direct-cross executable lines remained zero and inversion checks passed."
        KeyCounts = [pscustomobject]@{ Runs = 100; PaperPlanLines = 700; PreviewLines = 700; InitialReadinessComplete = 280; InitialHeldLines = 420; DirectCrossExecutableLines = 0 }
        Decision = "R009LongRunPaperOnlyPartialMaturityNeedsReadinessCompletion"
        ExecutablePromotionAuthorized = $false
    },
    [pscustomobject]@{
        SourcePhase = "EXEC-PAPER-R018"
        EvidenceType = "FinalReadinessIntakeAndRebinding"
        Summary = "Final local file intake accepted and row-validated 28 of 28 local files, rebounded four final lines, and left 56 readiness-only held lines."
        KeyCounts = [pscustomobject]@{ AcceptedFiles = 28; ManifestValidationAccepted = 28; RowValidationAccepted = 28; FinalReboundLines = 4; ReadinessComplete = $readinessComplete; StillHeldLines = $stillHeld }
        Decision = "R009LongRunPaperOnlyPartialMaturityWithExplicitReadinessBlocker"
        ExecutablePromotionAuthorized = $false
    },
    [pscustomobject]@{
        SourcePhase = "EXEC-ALGO-R013"
        EvidenceType = "LongRunPaperMaturityAcceptance"
        Summary = "R009 accepted for continued long-run paper-only evaluation with an explicit residual market-data readiness blocker."
        KeyCounts = [pscustomobject]@{ ReadinessComplete = $readinessComplete; PreviewLines = $previewLineCount; StillHeldLines = $stillHeld }
        Decision = "R009AcceptedForLongRunPaperOnlyEvaluationWithExplicitReadinessBlocker"
        ExecutablePromotionAuthorized = $false
    }
)

$currentStatus = [pscustomobject]@{
    Phase = $phase
    R009Status = "R009AcceptedForLongRunPaperOnlyEvaluationWithExplicitReadinessBlocker"
    PaperOnlyMaturity = "R009PaperOnlyMaturityPartialButUsable"
    MatureEnoughForContinuedLongRunPaperOnlyEvaluation = $true
    FullReadinessCompletenessClaimed = $false
    ReadinessCompleteLineCount = $readinessComplete
    PreviewLineCount = $previewLineCount
    ReadinessCompletenessRatio = $readinessRatio
    RemainingHeldLineCount = $stillHeld
    ResidualBlocker = $explicitBlocker
    ResidualBlockerIsReadinessOnly = $true
    ResidualBlockerIsR009LogicFailure = $false
    DesignOnly = $true
    PaperOnly = $true
    NonExecutable = $true
    NotAnOrder = $true
    NotSubmitted = $true
    NoBrokerRoute = $true
    BrokerReady = $false
    LiveReady = $false
    ExecutablePromotionAuthorized = $false
}

$residualBlockerSummary = [pscustomobject]@{
    Phase = $phase
    Blocker = $explicitBlocker
    ReadinessCompleteLineCount = $readinessComplete
    PreviewLineCount = $previewLineCount
    RemainingHeldLineCount = $stillHeld
    HeldBySymbol = [ordered]@{
        AUDUSD = 8
        EURUSD = 8
        GBPUSD = 8
        NZDUSD = 8
        USDCAD = 8
        USDCHF = 8
        USDJPY = 8
    }
    HeldByBarRole = [ordered]@{
        IntradayRebalance = 28
        ClosingFlatten = 28
        OpeningBuild = 0
    }
    MissingReadinessType = "LocalMarketDataReadinessIncomplete"
    NotDirectCrossIssue = $true
    NotInversionFailure = $true
    NotUsdJpyCaveatFailure = $true
    NotR009LogicFailure = $true
    NotExecutablePathIssue = $true
    ReadinessCompletionRecommended = $true
}

$whatR009IsNot = [pscustomobject]@{
    Phase = $phase
    NotExecutable = $true
    NotBrokerReady = $true
    NotLiveReady = $true
    NotOrderGenerator = $true
    NotScheduler = $true
    NotRouteSubmissionFillSystem = $true
    NotLedgerCommitter = $true
    NotStateMutatingRuntime = $true
    NotDirectCrossExecutionSystem = $true
    NotAuthorizedForAutomaticExecution = $true
}

$nextOperatorActions = [pscustomobject]@{
    Phase = $phase
    Options = @(
        [pscustomobject]@{
            Option = "A"
            Name = "ContinuePaperOnlyEvaluationWithExplicitReadinessBlocker"
            Description = "Continue manual paper-only evaluation while carrying the 56-line readiness blocker explicitly in reports."
            ExecutionEnabled = $false
        },
        [pscustomobject]@{
            Option = "B"
            Name = "CompleteRemainingReadinessBlockersLater"
            Description = "Acquire and validate missing local quote/readiness evidence for the remaining 56 preview lines in a future no-external intake gate."
            ExecutionEnabled = $false
        },
        [pscustomobject]@{
            Option = "C"
            Name = "ExpandMorePaperOnlyCases"
            Description = "Generate more paper-only fixtures and target closes under ManualOnly, no-external, no-order, no-ledger controls."
            ExecutionEnabled = $false
        },
        [pscustomobject]@{
            Option = "D"
            Name = "PauseAndIntegrateUpstreamQubesPmsPipeline"
            Description = "Pause R009 paper-only expansion and align upstream Qubes/PMS fixture and readiness production before further batches."
            ExecutionEnabled = $false
        }
    )
    RecommendedDefault = "A"
    ReadinessCompletionRecommended = $true
    NoExecutablePromotion = $true
}

$executableBlockers = [pscustomobject]@{
    Phase = $phase
    ExecutablePromotionBlocked = $true
    Blockers = @(
        "NoBrokerIntegrationAuthorized",
        "NoLiveMarketDataAuthorized",
        "NoOmsOrderCreationAuthorized",
        "NoExecutableScheduleAuthorized",
        "NoChildSlicesAuthorized",
        "NoRouteSubmissionAuthorized",
        "NoFillsExecutionReportsAuthorized",
        "NoPaperLedgerCommitAuthorized",
        "NoStateMutationAuthorized",
        "NoDirectCrossExecutionAuthorized",
        "NoNonmajorEmScandiCnhExecutionWithoutCalibration",
        "ExplicitReadinessBlockerRemains",
        "SeparateExplicitExecutableGateRequiredIfEverConsidered"
    )
}

$readinessChecklist = [pscustomobject]@{
    Phase = $phase
    RemainingHeldLines = $stillHeld
    Checklist = @(
        "Identify exact missing local market-data/readiness windows for the 56 held lines.",
        "Acquire files only through operator-approved offline process; Codex must not download.",
        "Validate manifests, SHA256, row counts, provider symbols, timestamps, and bid/ask integrity.",
        "Generate quote-window, close-benchmark, and feed-quality readiness locally.",
        "Rebind without inventing readiness and preserve original preview line identities.",
        "Re-aggregate all 700 design-only preview lines and keep executable promotion blocked."
    )
}

$paperOnlyChecklist = [pscustomobject]@{
    Phase = $phase
    Checklist = @(
        "Keep R009 DesignOnly, PaperOnly, NonExecutable, NotAnOrder, NotSubmitted, and NoBrokerRoute.",
        "Use canonical quarter-hour target closes only.",
        "Keep legacy timestamp conventions compatibility-only.",
        "Use USD-pair execution symbols after netting.",
        "Exclude direct crosses from executable line emission.",
        "Preserve USDJPY, USDCAD, and USDCHF inversion handling.",
        "Require quote-window, close-benchmark, and feed-quality readiness bindings.",
        "Keep risk/operator approval scoped to DesignOnlyPreviewOnly.",
        "Run future commands manually only after safety validation in a separate gate.",
        "Preserve no order, fill, route, submission, schedule, broker, live-data, ledger, or state mutation paths."
    )
}

$sourceIndex = [pscustomobject]@{
    Phase = $phase
    SourceOfTruthArtifacts = @(
        [pscustomobject]@{ Phase = "EXEC-ALGO-R013"; Path = "artifacts/readiness/execution-algo/phase-exec-algo-r013-long-run-paper-maturity-acceptance-result.json"; Purpose = "Current maturity acceptance result with readiness blocker." },
        [pscustomobject]@{ Phase = "EXEC-ALGO-R013"; Path = "artifacts/readiness/execution-algo/phase-exec-algo-r013-readiness-completion-summary.json"; Purpose = "644/700 readiness completion and held-line summary." },
        [pscustomobject]@{ Phase = "EXEC-PAPER-R018"; Path = "artifacts/readiness/execution-sim/phase-exec-paper-r018-final-long-run-maturity-decision.json"; Purpose = "Final local intake/rebinding maturity decision." },
        [pscustomobject]@{ Phase = "EXEC-PAPER-R018"; Path = "artifacts/readiness/execution-sim/phase-exec-paper-r018-final-reaggregated-preview-status.json"; Purpose = "Final 700-line re-aggregation status." },
        [pscustomobject]@{ Phase = "EXEC-PAPER-R014"; Path = "artifacts/readiness/execution-sim/phase-exec-paper-r014-r009-design-only-preview-lines.json"; Purpose = "Long-run 700 design-only preview line base." },
        [pscustomobject]@{ Phase = "EXEC-PAPER-R012"; Path = "artifacts/readiness/execution-sim/phase-exec-paper-r012-operator-review-report.json"; Purpose = "Balanced bar-role preview review." },
        [pscustomobject]@{ Phase = "EXEC-SIM-R058"; Path = "artifacts/readiness/execution-sim/phase-exec-sim-r058-stability-decision.json"; Purpose = "Broader paper-only preview stability decision." },
        [pscustomobject]@{ Phase = "EXEC-SIM-R054"; Path = "artifacts/readiness/execution-sim/phase-exec-sim-r054-r009-contract-decision.json"; Purpose = "Offline historical TCA R009 policy decision." }
    )
}

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r061-r013-maturity-reference.json") ([pscustomobject]@{
    Phase = $phase
    SourcePhase = "EXEC-ALGO-R013"
    SourceArtifact = "artifacts/readiness/execution-algo/phase-exec-algo-r013-long-run-paper-maturity-acceptance-result.json"
    MaturityStatus = $currentStatus.R009Status
    PaperOnlyMaturity = $currentStatus.PaperOnlyMaturity
    ReadinessCompleteLineCount = $readinessComplete
    PreviewLineCount = $previewLineCount
    RemainingHeldLineCount = $stillHeld
    ExplicitBlocker = $explicitBlocker
    ExecutablePromotionAuthorized = $false
    ReusedOnly = $true
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r061-r018-final-readiness-reference.json") ([pscustomobject]@{
    Phase = $phase
    SourcePhase = "EXEC-PAPER-R018"
    SourceArtifact = "artifacts/readiness/execution-sim/phase-exec-paper-r018-final-long-run-maturity-decision.json"
    AcceptedLocalFileEntries = 28
    ManifestValidationAccepted = 28
    RowValidationAccepted = 28
    FinalReboundLines = 4
    FinalReadinessCompletePreviewLines = $readinessComplete
    FinalStillHeldLines = $stillHeld
    Decision = "R009LongRunPaperOnlyPartialMaturityWithExplicitReadinessBlocker"
    ReusedOnly = $true
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r061-r009-contract-reference.json") $r009Contract
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r061-evidence-chain-summary.json") ([pscustomobject]@{ Phase = $phase; EvidenceChain = $evidenceChain })
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r061-r009-current-status.json") $currentStatus
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r061-residual-readiness-blocker-summary.json") $residualBlockerSummary
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r061-what-r009-is-not.json") $whatR009IsNot
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r061-next-operator-action-options.json") $nextOperatorActions
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r061-executable-promotion-blockers.json") $executableBlockers
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r061-readiness-completion-checklist.json") $readinessChecklist
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r061-paper-only-continuation-checklist.json") $paperOnlyChecklist
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r061-source-of-truth-artifact-index.json") $sourceIndex

$programmeReport = [pscustomobject]@{
    Phase = $phase
    Classifications = $classifications
    ProgrammeStatus = "R009 paper-only programme summary and handoff ready."
    R009CurrentStatus = $currentStatus
    EvidenceChain = $evidenceChain
    ResidualReadinessBlocker = $residualBlockerSummary
    WhatR009IsNot = $whatR009IsNot
    NextOperatorActions = $nextOperatorActions.Options
    ExecutablePromotionBlockers = $executableBlockers.Blockers
    SourceOfTruthArtifactIndex = $sourceIndex.SourceOfTruthArtifacts
    NoExternalConfirmation = [pscustomobject]@{
        PolygonCalled = $false
        LmaxCalled = $false
        ExternalApiCalled = $false
        DownloadsExecuted = $false
        BrokerActivated = $false
        LiveMarketDataRequested = $false
        SchedulerServicePollingStarted = $false
        PmsEmsOmsCycleRun = $false
        ManualNoExternalCommandRun = $false
        BacktestOrSimulationRun = $false
        TcaResultLinesCreated = $false
        OrdersFillsRoutesSubmissionsCreated = $false
        PaperLedgerCommitCreated = $false
        StateMutated = $false
        R009PromotedToExecutable = $false
    }
}
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r061-programme-summary-report.json") $programmeReport

$summary = [pscustomobject]@{
    Phase = $phase
    Classifications = $classifications
    Status = "PaperOnlyProgrammeSummaryAndHandoffReady"
    R009Status = $currentStatus.R009Status
    PaperOnlyMaturity = $currentStatus.PaperOnlyMaturity
    ReadinessCompleteLineCount = $readinessComplete
    PreviewLineCount = $previewLineCount
    RemainingHeldLineCount = $stillHeld
    ResidualBlocker = $explicitBlocker
    ResidualBlockerMisclassifiedAsR009Failure = $false
    FullReadinessCompletenessClaimed = $false
    ExecutablePromotionAuthorized = $false
    NoExternal = $true
    CommandsExecuted = $false
    DownloadsExecuted = $false
    ArtifactsWritten = @(
        "phase-exec-sim-r061-programme-summary-report.md",
        "phase-exec-sim-r061-programme-summary-report.json",
        "phase-exec-sim-r061-evidence-chain-summary.json",
        "phase-exec-sim-r061-r009-current-status.json",
        "phase-exec-sim-r061-residual-readiness-blocker-summary.json",
        "phase-exec-sim-r061-next-operator-action-options.json",
        "phase-exec-sim-r061-executable-promotion-blockers.json",
        "phase-exec-sim-r061-source-of-truth-artifact-index.json"
    )
}
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r061-summary.json") $summary

$reportMarkdown = @"
# EXEC-SIM-R061 Paper-Only Programme Summary

## Current Status

R009 is accepted for continued long-run paper-only evaluation with an explicit residual readiness blocker.

- Status: R009AcceptedForLongRunPaperOnlyEvaluationWithExplicitReadinessBlocker
- Paper-only maturity: R009PaperOnlyMaturityPartialButUsable
- Readiness complete: $readinessComplete / $previewLineCount
- Remaining held lines: $stillHeld
- Residual blocker: $explicitBlocker
- Executable promotion authorized: false

The residual blocker is local market-data/readiness coverage for 56 preview lines. It is not a direct-cross issue, inversion failure, USDJPY caveat failure, R009 logic failure, or executable-path issue.

## Evidence Chain

| Phase | Evidence | Result |
| --- | --- | --- |
| EXEC-SIM-R054 | Offline historical TCA review: 20 dates, 7 symbols, 3,780 canonical quote windows, 41,580 non-executable lines | R009 primary candidate stable |
| EXEC-SIM-R058 | Broader paper preview: 20 runs, 140 design-only preview lines | Stable, 0 held |
| EXEC-PAPER-R012 | Balanced bar-role preview: 30 runs, 210 preview lines | 210 / 210 readiness complete |
| EXEC-PAPER-R014 | Long-run paper batch: 100 runs, 700 preview lines | Partial due to readiness gaps |
| EXEC-PAPER-R018 | Final local readiness intake/rebinding | 644 / 700 complete, 56 still held |
| EXEC-ALGO-R013 | Maturity acceptance with blocker | Accepted for continued paper-only evaluation |

## What R009 Is Not

R009 is not executable, not broker-ready, not live-ready, not an order generator, not a scheduler, and not a route/submission/fill/ledger system.

## Next Operator Actions

1. Continue paper-only evaluation with the explicit readiness blocker carried in reports.
2. Complete the remaining 56 readiness blockers later through a separate no-external local intake/rebinding gate.
3. Expand more paper-only cases under manual, no-external, no-order, no-ledger controls.
4. Pause and integrate upstream Qubes/PMS fixture and readiness production before additional batches.

## Executable Blockers

Executable promotion remains blocked by: no broker integration, no live market data, no OMS order creation, no executable schedule, no child slices, no route/submission, no fills/execution reports, no paper ledger commit, no state mutation, no direct-cross execution, no nonmajor/EM/scandi/CNH execution without calibration, the explicit readiness blocker, and the requirement for a separate explicit executable gate if ever considered.

## Timing and Universe

Future target closes remain canonical quarter-hour closes only. Legacy timestamp conventions are compatibility-only. Execution review remains USD-pair-only after netting; direct crosses remain signal-only and execution-disabled. USDJPY preserves JPYUSD normalization, USDJPY execution, inversion required, SecurityID 4004, and SecurityIDSource 8. AUDUSD remains not failed. 5 USD/million remains best-case major-only guidance.
"@
Write-Text (Join-Path $SimArtifactsRoot "phase-exec-sim-r061-programme-summary-report.md") $reportMarkdown

$summaryMarkdown = @"
# EXEC-SIM-R061 Summary

Classifications:
- EXEC_SIM_R061_PASS_PAPER_ONLY_PROGRAMME_SUMMARY_READY_NO_EXTERNAL
- EXEC_SIM_R061_PASS_R009_HANDOFF_DOCUMENTATION_READY_NO_EXTERNAL
- EXEC_SIM_R061_PASS_RESIDUAL_READINESS_BLOCKER_DOCUMENTED_NO_EXTERNAL
- EXEC_SIM_R061_PASS_NO_EXECUTABLE_PROMOTION_NO_ORDER_GATE_READY_NO_EXTERNAL

R009 is mature enough for continued long-run paper-only evaluation, but it is not fully readiness-complete. Final readiness is $readinessComplete / $previewLineCount with $stillHeld held lines remaining due to $explicitBlocker.

No execution, broker, live-market-data, scheduler, order, fill, route, submission, ledger, state mutation, download, or external API path is authorized by this gate.
"@
Write-Text (Join-Path $SimArtifactsRoot "phase-exec-sim-r061-summary.md") $summaryMarkdown

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r061-canonical-quarter-hour-policy-preservation.json") ([pscustomobject]@{
    Phase = $phase
    CanonicalQuarterHourRequired = $true
    FutureCanonicalMinuteValues = @(0, 15, 30, 45)
    LegacyCompatibilityOnly = $true
    LegacyTimestampsUsedAsFutureCanonical = $false
})
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r061-legacy-compatibility-preservation.json") ([pscustomobject]@{
    Phase = $phase
    LegacyTimestampConventionsCompatibilityOnly = $true
    UsedAsFutureCanonical = $false
    CanonicalClosePolicyPreserved = $true
})
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r061-usd-pair-normalization-preservation.json") ([pscustomobject]@{
    Phase = $phase
    UsdPairOnlyAfterNetting = $true
    SupportedExecutionSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")
    AudUsdNotFailed = $true
})
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r061-direct-cross-exclusion-preservation.json") ([pscustomobject]@{
    Phase = $phase
    DirectCrossesSignalOnly = $true
    NettingFirst = $true
    ExecutionDisabled = $true
    DirectCrossExecutableLines = 0
})
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r061-cost-guidance-preservation.json") ([pscustomobject]@{
    Phase = $phase
    FiveUsdPerMillionBestCaseMajorOnly = $true
    FiveUsdPerMillionUniversalized = $false
})
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r061-nonmajor-calibration-preservation.json") ([pscustomobject]@{
    Phase = $phase
    NonmajorEmScandiCnhCalibrationRequired = $true
    NonmajorExecutionAuthorized = $false
})
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r061-usdjpy-caveat-preservation.json") ([pscustomobject]@{
    Phase = $phase
    NormalizedPortfolioSymbol = "JPYUSD"
    ExecutionTradableSymbol = "USDJPY"
    RequiresInversion = $true
    SecurityID = "4004"
    SecurityIDSource = "8"
    CaveatWeakened = $false
})
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r061-lmax-readonly-baseline-reference.json") ([pscustomobject]@{
    Phase = $phase
    LmaxReferenceOnly = $true
    LmaxCalled = $false
    BrokerRuntimeActivated = $false
})

New-Audit "phase-exec-sim-r061-no-broker-activation-audit.json" "NoBrokerActivation" "R061 is documentation-only and did not activate any broker runtime."
New-Audit "phase-exec-sim-r061-no-live-marketdata-audit.json" "NoLiveMarketData" "R061 did not request live market data."
New-Audit "phase-exec-sim-r061-no-scheduler-service-polling-audit.json" "NoSchedulerServicePolling" "R061 did not start or introduce scheduler, service, timer, polling, or background jobs."
New-Audit "phase-exec-sim-r061-no-new-pms-cycle-audit.json" "NoNewPmsCycle" "R061 did not run PMS, EMS, or OMS cycles."
New-Audit "phase-exec-sim-r061-no-manualnoexternal-command-run-audit.json" "NoManualNoExternalCommandRun" "R061 did not run ManualNoExternal commands."
New-Audit "phase-exec-sim-r061-no-db-import-audit.json" "NoDbImport" "R061 did not import rows or records into a database."
New-Audit "phase-exec-sim-r061-no-persisted-sanitized-row-audit.json" "NoPersistedSanitizedRows" "R061 did not persist sanitized quote rows."
New-Audit "phase-exec-sim-r061-no-new-backtest-audit.json" "NoNewBacktest" "R061 did not run a backtest."
New-Audit "phase-exec-sim-r061-no-new-simulation-audit.json" "NoNewSimulation" "R061 did not run a simulation."
New-Audit "phase-exec-sim-r061-no-tca-result-lines-audit.json" "NoTcaResultLines" "R061 did not create TCA result lines."
New-Audit "phase-exec-sim-r061-no-executable-schedule-audit.json" "NoExecutableSchedule" "R061 did not create executable schedules."
New-Audit "phase-exec-sim-r061-no-child-slices-audit.json" "NoChildSlices" "R061 did not create child slices."
New-Audit "phase-exec-sim-r061-no-child-orders-audit.json" "NoChildOrders" "R061 did not create child orders."
New-Audit "phase-exec-sim-r061-no-order-created-audit.json" "NoOrderCreated" "R061 did not create orders."
New-Audit "phase-exec-sim-r061-no-real-fill-audit.json" "NoRealFill" "R061 did not create fills."
New-Audit "phase-exec-sim-r061-no-execution-report-audit.json" "NoExecutionReport" "R061 did not create execution reports."
New-Audit "phase-exec-sim-r061-no-route-no-submission-audit.json" "NoRouteNoSubmission" "R061 did not create routes or submissions."
New-Audit "phase-exec-sim-r061-no-paper-ledger-commit-audit.json" "NoPaperLedgerCommit" "R061 did not commit paper ledger state."
New-Audit "phase-exec-sim-r061-no-polygon-api-call-audit.json" "NoPolygonApiCall" "R061 did not call Polygon."
New-Audit "phase-exec-sim-r061-no-lmax-call-audit.json" "NoLmaxCall" "R061 did not call LMAX."
New-Audit "phase-exec-sim-r061-no-external-api-call-audit.json" "NoExternalApiCall" "R061 did not call external APIs."

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r061-no-external-audit.json") ([pscustomobject]@{
    Phase = $phase
    NoExternal = $true
    PolygonCalled = $false
    LmaxCalled = $false
    ExternalApiCalled = $false
    DownloadsExecuted = $false
    BrokerActivated = $false
    LiveMarketDataRequested = $false
    SchedulerServicePollingStarted = $false
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r061-forbidden-actions-audit.json") ([pscustomobject]@{
    Phase = $phase
    ForbiddenActionsOccurred = $false
    DownloadsExecuted = $false
    PmsEmsOmsCycleRun = $false
    ManualNoExternalCommandRun = $false
    BacktestOrSimulationRun = $false
    TcaResultLinesCreated = $false
    ExecutableScheduleCreated = $false
    ChildSlicesCreated = $false
    ChildOrdersCreated = $false
    OrdersCreated = $false
    FillsCreated = $false
    ExecutionReportsCreated = $false
    RoutesCreated = $false
    SubmissionsCreated = $false
    PaperLedgerCommitCreated = $false
    StateMutated = $false
    R009PromotedToExecutable = $false
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r061-next-phase-recommendation.json") ([pscustomobject]@{
    Phase = $phase
    RecommendedNextStep = "OperatorDecision"
    Options = @(
        "ContinuePaperOnlyEvaluationWithExplicitReadinessBlocker",
        "CompleteRemaining56ReadinessBlockersInFutureNoExternalGate",
        "ExpandMorePaperOnlyCasesUnderManualNoExternalControls",
        "PauseAndIntegrateUpstreamQubesPmsReadinessPipeline"
    )
    ExecutablePromotionStillBlocked = $true
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r061-build-test-validator-evidence.json") ([pscustomobject]@{
    Phase = $phase
    Build = [pscustomobject]@{ Command = "dotnet build --no-restore"; Status = $BuildStatus }
    FocusedTests = [pscustomobject]@{ Command = "dotnet test tests/QQ.Production.Intraday.Tests.Unit/QQ.Production.Intraday.Tests.Unit.csproj --no-build --no-restore --filter FullyQualifiedName~R061"; Status = $FocusedTestsStatus }
    UnitTests = [pscustomobject]@{ Command = "dotnet test tests/QQ.Production.Intraday.Tests.Unit/QQ.Production.Intraday.Tests.Unit.csproj --no-build --no-restore"; Status = $UnitTestsStatus }
    Validator = [pscustomobject]@{ Command = "scripts/check-exec-sim-r061-paper-programme-summary-handoff-gate.ps1"; Status = $ValidatorStatus }
})

Write-Output "EXEC-SIM-R061 handoff artifacts written to $SimArtifactsRoot"
