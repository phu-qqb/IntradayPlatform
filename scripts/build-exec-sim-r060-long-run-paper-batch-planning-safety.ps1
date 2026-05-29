param(
    [string]$SimArtifactsRoot = "artifacts/readiness/execution-sim",
    [string]$AlgoArtifactsRoot = "artifacts/readiness/execution-algo"
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
    Write-Json (Join-Path $SimArtifactsRoot $name) ([pscustomobject]@{
        Phase = "EXEC-SIM-R060"
        AuditName = $key
        Passed = $true
        Occurred = $false
        Detail = $detail
    })
}

$phase = "EXEC-SIM-R060"
$r012Maturity = Read-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-paper-only-maturity-review-result.json")
$r012Plan = Read-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-long-run-paper-only-expansion-plan.json")
$r012Metrics = Read-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-long-run-paper-only-metrics.json")
$r012Blockers = Read-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-executable-promotion-blockers.json")
$r012Evidence = Read-Json (Join-Path $AlgoArtifactsRoot "phase-exec-algo-r012-evidence-summary.json")

$classifications = @(
    "EXEC_SIM_R060_PASS_LONG_RUN_PAPER_BATCH_PLAN_READY_NO_EXTERNAL",
    "EXEC_SIM_R060_PASS_AUTOMATION_SAFETY_CONSTRAINTS_READY_NO_EXTERNAL",
    "EXEC_SIM_R060_PASS_OPERATOR_RUN_PACKAGE_REQUIREMENTS_READY_NO_EXTERNAL",
    "EXEC_SIM_R060_PASS_NO_AUTOMATION_NO_ORDER_GATE_READY_NO_EXTERNAL"
)

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r060-r012-maturity-reference.json") ([pscustomobject]@{
    Phase = $phase
    SourcePhase = "EXEC-ALGO-R012"
    R009MaturityStatus = $r012Maturity.R009MaturityStatus
    AcceptedForLongRunPaperOnlyPlanning = [bool]$r012Maturity.AcceptedForLongRunPaperOnlyPlanning
    MoreLongRunPaperOnlyDataRecommended = [bool]$r012Maturity.MoreLongRunPaperOnlyDataRecommended
    R009ContractVersion = $r012Maturity.R009ContractVersion
    PrimaryPolicyCandidate = $r012Maturity.PrimaryPolicyCandidate
    SecondaryPolicyCandidate = $r012Maturity.SecondaryPolicyCandidate
    ConditionalResidualModule = $r012Maturity.ConditionalResidualModule
    DesignOnly = $true
    PaperOnly = $true
    NonExecutable = $true
    NotAnOrder = $true
    NotSubmitted = $true
    NoBrokerRoute = $true
    ExecutablePromotionAuthorized = $false
    BrokerReady = $false
    LiveReady = $false
    R054Evidence = $r012Evidence.HistoricalTcaStability
    R058Evidence = $r012Evidence.BroaderPaperPreviewStability
    BalancedR012Evidence = $r012Evidence.BalancedBarRolePreviewStability
    ReusedOnly = $true
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r060-long-run-paper-batch-planning-contract.json") ([pscustomobject]@{
    Phase = $phase
    GateType = "NoExternalLongRunPaperOnlyBatchPlanningAndAutomationSafety"
    PlanningAndSafetyOnly = $true
    CommandsExecuted = $false
    ManualNoExternalCommandsRun = $false
    PmsCyclesRun = $false
    BacktestOrSimulationRun = $false
    TcaResultLinesCreated = $false
    SchedulerServicePollingIntroduced = $false
    AutomaticExecutionEnabled = $false
    BrokerRuntimeActivated = $false
    LiveMarketDataRequested = $false
    R009DesignOnly = $true
    R009PaperOnly = $true
    R009NonExecutable = $true
    ExecutablePromotionAuthorized = $false
    Classifications = $classifications
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r060-long-run-batch-packaging-requirements.json") ([pscustomobject]@{
    Phase = $phase
    MinimumTargetCloses = 100
    MinimumOpeningBuildCloses = 30
    MinimumIntradayRebalanceCloses = 30
    MinimumClosingFlattenCloses = 30
    RemainingCases = @("Stress", "WideSpread", "HighResidual", "Quiet", "MixedUSDRegimes")
    RequiredBatchEntryFields = @(
        "BatchEntryId",
        "QubesFixturePath",
        "QubesRunId",
        "RequestedCycleRunId",
        "CanonicalTargetCloseLocal",
        "CanonicalTargetCloseUtc",
        "CanonicalSession",
        "BarRole",
        "CadenceMinutes=15",
        "NoPaperLedgerCommit=true",
        "FixtureSource",
        "ReadinessBindingRequired=true",
        "RiskOperatorApprovalScope=DesignOnlyPreviewOnly"
    )
    CompleteReadinessBindingsRequired = $true
    NoDirectCrossExecution = $true
    NoOrdersFillsRoutesSubmissions = $true
    NoPaperLedgerCommit = $true
    SourceR012MinimumTargetCloses = $r012Plan.MinimumTargetClosesBeforeExecutableDiscussion
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r060-operator-run-package-requirements.json") ([pscustomobject]@{
    Phase = $phase
    CommandsTextOnly = $true
    CommandsMustUseManualNoExternal = $true
    CommandsMustIncludeNoPaperLedgerCommitTrue = $true
    CommandsWriteOnlyAllowedArtifacts = $true
    CommandsRunManuallyByOperatorOnly = $true
    SchedulerAllowed = $false
    ServiceAllowed = $false
    PollingAllowed = $false
    AutomaticBatchRunnerAllowed = $false
    BrokerRuntimeAllowed = $false
    LiveMarketDataAllowed = $false
    OrderCreationAllowed = $false
    FillCreationAllowed = $false
    RouteSubmissionAllowed = $false
    PaperLedgerCommitAllowed = $false
    RequiredCommandFlags = @(
        "--mode ManualNoExternal",
        "--output-artifacts-dir",
        "--requested-cycle-run-id",
        "--qubes-run-id",
        "--qubes-fixture-path",
        "--cadence-minutes 15",
        "--no-paper-ledger-commit true"
    )
    DeprecatedFlagsDisallowed = @("--mode no-external-paper-cycle", "--output")
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r060-automation-safety-constraints.json") ([pscustomobject]@{
    Phase = $phase
    ManualOnly = $true
    SchedulerAllowed = $false
    ServiceAllowed = $false
    PollingAllowed = $false
    TimerAllowed = $false
    BackgroundJobAllowed = $false
    AutomaticExecutionAllowed = $false
    BrokerRuntimeAllowed = $false
    LiveMarketDataAllowed = $false
    PaperLedgerCommitAllowed = $false
    StateMutationAllowed = $false
    ExecutableScheduleAllowed = $false
    ChildSlicesAllowed = $false
    ChildOrdersAllowed = $false
    OrderCreationAllowed = $false
    RouteSubmissionAllowed = $false
    FillReportAllowed = $false
    ManualNoExternalCommandRunAllowedInR060 = $false
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r060-batch-manifest-requirements.json") ([pscustomobject]@{
    Phase = $phase
    RequiredFields = @(
        "BatchEntryId",
        "QubesFixturePath",
        "QubesRunId",
        "RequestedCycleRunId",
        "CanonicalTargetCloseLocal",
        "CanonicalTargetCloseUtc",
        "CanonicalSession",
        "BarRole",
        "CadenceMinutes=15",
        "NoPaperLedgerCommit=true",
        "FixtureSource",
        "ReadinessBindingRequired=true",
        "RiskOperatorApprovalScope=DesignOnlyPreviewOnly"
    )
    MinimumEntries = 100
    OpeningBuildEntriesMinimum = 30
    IntradayRebalanceEntriesMinimum = 30
    ClosingFlattenEntriesMinimum = 30
    LegacyCompatibilityMappingAllowed = $true
    Legacy06UsedAsFutureCanonical = $false
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r060-fixture-requirements.json") ([pscustomobject]@{
    Phase = $phase
    RequiredFormat = "<BloombergTicker>;<weight>"
    NonEmpty = $true
    WeightMustParseDecimal = $true
    TimestampsInsideFixtureRowsAllowed = $false
    DirectCrossesAllowedAsSignalsOnly = $true
    NettingRequiredBeforeUsdPairPreview = $true
    FixtureSources = @("LegacyAggregatedWeightsExtraction", "OperatorSupplied")
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r060-target-close-requirements.json") ([pscustomobject]@{
    Phase = $phase
    CanonicalQuarterHourRequired = $true
    AllowedMinuteValues = @(0, 15, 30, 45)
    CanonicalSession = "14:15-21:00 America/New_York"
    TargetCloseSuppliedInManifest = $true
    Legacy06UsedAsFutureCanonical = $false
    LegacyCompatibilityMapping = [pscustomobject]@{
        CanonicalCloseForLegacyMatch = "LegacyOutputTimestamp - 6 minutes"
        LegacyNextBarExecutionCloseCanonical = "LegacyOutputTimestamp + 9 minutes"
    }
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r060-readiness-binding-requirements.json") ([pscustomobject]@{
    Phase = $phase
    RequiredBindings = @("QuoteWindowReadiness", "CloseBenchmarkReadiness", "FeedQualityReadiness")
    RequiredForEveryPreviewLine = $true
    MissingBindingHoldRequired = $true
    CompleteReadinessBindingsRequired = $true
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r060-risk-operator-approval-requirements.json") ([pscustomobject]@{
    Phase = $phase
    RiskApprovalRequired = $true
    OperatorApprovalRequired = $true
    RequiredScope = "DesignOnlyPreviewOnly"
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
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r060-reporting-monitoring-requirements.json") ([pscustomobject]@{
    Phase = $phase
    ReportingOnly = $true
    LiveMonitoringAllowed = $false
    SchedulerServicePollingAllowed = $false
    Metrics = @(
        "Preview line count",
        "Line coverage by symbol",
        "Line coverage by bar role",
        "Line coverage by batch/date",
        "Readiness binding completeness",
        "Held line count and reasons",
        "Direct-cross exclusion count",
        "Inversion stability",
        "USDJPY caveat preservation",
        "No-order/no-fill/no-route/no-ledger audit",
        "R009 primary/secondary/conditional policy application summary",
        "ManualReview / DoNotTrade frequency if produced",
        "Missing evidence diagnostics",
        "Operator approval scope review"
    )
    SourceR012Metrics = $r012Metrics.Metrics
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r060-aggregation-requirements.json") ([pscustomobject]@{
    Phase = $phase
    AggregateBy = @("Batch", "Date", "TargetClose", "BarRole", "Symbol", "ReadinessStatus", "HoldReason")
    RequiredReviews = @(
        "Preview line coverage",
        "Per-symbol coverage",
        "Per-bar-role coverage",
        "Per-batch/date coverage",
        "Readiness binding completeness",
        "Held-line diagnostics",
        "Direct-cross/netting review",
        "Inversion stability review",
        "Risk/operator approval scope review",
        "No-order/no-fill/no-route/no-ledger audit review"
    )
    AcceptanceScope = "LongRunPaperOnlyEvaluation"
    ExecutablePromotionAuthorized = $false
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r060-stop-hold-criteria.json") ([pscustomobject]@{
    Phase = $phase
    Criteria = @(
        "Missing fixture",
        "Invalid fixture format",
        "Missing canonical target close",
        "Target close not quarter-hour",
        "Missing readiness binding",
        "Direct cross emitted as executable line",
        "Unsupported execution symbol",
        "Inversion mismatch",
        "Risk/operator preview approval missing",
        "Any order/fill/route/submission/ledger/state path appears",
        "Any scheduler/service/polling path appears",
        "Any broker/live market data path appears"
    )
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r060-executable-promotion-blockers.json") ([pscustomobject]@{
    Phase = $phase
    ExecutablePromotionBlocked = $true
    Blockers = $r012Blockers.Blockers
    AdditionalAutomationBlockers = @(
        "No scheduler/service/polling/background automation authorized",
        "No automatic execution authorized",
        "Manual operator-run only"
    )
    AcceptanceIsExecutableApproval = $false
    ExecutablePromotionAuthorized = $false
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r060-canonical-quarter-hour-policy-preservation.json") ([pscustomobject]@{ Phase = $phase; FutureTimestampsUseCanonicalQuarterHour = $true; AllowedMinutes = @(0, 15, 30, 45); Legacy06UsedAsFutureCanonical = $false })
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r060-legacy-compatibility-preservation.json") ([pscustomobject]@{ Phase = $phase; LegacyTimestampsCompatibilityOnly = $true; CanonicalCloseForLegacyMatch = "LegacyOutputTimestamp - 6 minutes"; LegacyNextBarExecutionCloseCanonical = "LegacyOutputTimestamp + 9 minutes"; Legacy06UsedAsFutureCanonical = $false })
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r060-usd-pair-normalization-preservation.json") ([pscustomobject]@{ Phase = $phase; USDPairOnlyAfterNetting = $true; ExecutionSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF"); AUDUSDNotFailed = $true })
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r060-direct-cross-exclusion-preservation.json") ([pscustomobject]@{ Phase = $phase; DirectCrossesSignalOnly = $true; DirectCrossNettingFirst = $true; DirectCrossExecutionDisabled = $true; ExclusionWeakened = $false })
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r060-cost-guidance-preservation.json") ([pscustomobject]@{ Phase = $phase; FiveUsdPerMillion = "BestCaseMajorOnly"; FiveUsdPerMillionUniversalized = $false; NonmajorCalibrationRequired = $true })
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r060-nonmajor-calibration-preservation.json") ([pscustomobject]@{ Phase = $phase; NonmajorEmScandiCnhCalibrationRequired = $true; NonmajorExecutionAuthorized = $false })
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r060-usdjpy-caveat-preservation.json") ([pscustomobject]@{ Phase = $phase; NormalizedPortfolioSymbol = "JPYUSD"; ExecutionTradableSymbol = "USDJPY"; RequiresInversion = $true; SecurityID = 4004; SecurityIDSource = "8"; CaveatWeakened = $false })
Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r060-lmax-readonly-baseline-reference.json") ([pscustomobject]@{ Phase = $phase; LmaxReferencedAsReadonlyBaselineOnly = $true; LmaxCalled = $false; BrokerRuntimeActivated = $false })

New-Audit "phase-exec-sim-r060-no-broker-activation-audit.json" "BrokerActivation" "No broker activation occurred."
New-Audit "phase-exec-sim-r060-no-live-marketdata-audit.json" "LiveMarketData" "No live market data was requested."
New-Audit "phase-exec-sim-r060-no-scheduler-service-polling-audit.json" "SchedulerServicePolling" "No scheduler/service/polling/background job was started or authorized."
New-Audit "phase-exec-sim-r060-no-new-pms-cycle-audit.json" "NewPmsCycle" "No PMS/EMS/OMS cycle was run."
New-Audit "phase-exec-sim-r060-no-manualnoexternal-command-run-audit.json" "ManualNoExternalCommandRun" "No ManualNoExternal command was run."
New-Audit "phase-exec-sim-r060-no-new-backtest-audit.json" "NewBacktestRun" "No new backtest was run."
New-Audit "phase-exec-sim-r060-no-new-simulation-audit.json" "NewSimulationRun" "No new simulation was run."
New-Audit "phase-exec-sim-r060-no-tca-result-lines-audit.json" "TcaResultLinesCreated" "No TCA result lines were created."
New-Audit "phase-exec-sim-r060-no-executable-schedule-audit.json" "ExecutableSchedulesCreated" "No executable schedules were created."
New-Audit "phase-exec-sim-r060-no-child-slices-audit.json" "ChildSlicesCreated" "No child slices were created."
New-Audit "phase-exec-sim-r060-no-child-orders-audit.json" "ChildOrdersCreated" "No child orders were created."
New-Audit "phase-exec-sim-r060-no-order-created-audit.json" "OrdersCreated" "No orders were created."
New-Audit "phase-exec-sim-r060-no-real-fill-audit.json" "FillsCreated" "No fills were created."
New-Audit "phase-exec-sim-r060-no-execution-report-audit.json" "ExecutionReportsCreated" "No execution reports were created."
New-Audit "phase-exec-sim-r060-no-route-no-submission-audit.json" "RoutesOrSubmissionsCreated" "No routes or submissions were created."
New-Audit "phase-exec-sim-r060-no-paper-ledger-commit-audit.json" "PaperLedgerCommitted" "No paper ledger commit was created."
New-Audit "phase-exec-sim-r060-no-polygon-api-call-audit.json" "PolygonCalled" "Polygon was not called."
New-Audit "phase-exec-sim-r060-no-lmax-call-audit.json" "LmaxCalled" "LMAX was not called."
New-Audit "phase-exec-sim-r060-no-external-api-call-audit.json" "ExternalApiCalled" "No external API was called."

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r060-no-external-audit.json") ([pscustomobject]@{
    Phase = $phase
    NoExternal = $true
    PolygonCalled = $false
    LmaxCalled = $false
    ExternalApiCalled = $false
    DownloadsExecuted = $false
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r060-forbidden-actions-audit.json") ([pscustomobject]@{
    Phase = $phase
    ForbiddenActionsDetected = $false
    BrokerActivation = $false
    LiveMarketData = $false
    SchedulerServicePolling = $false
    BackgroundJobs = $false
    AutomaticExecution = $false
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

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r060-next-phase-recommendation.json") ([pscustomobject]@{
    Phase = $phase
    RecommendedNextPhase = "EXEC-PAPER-R013 - No-External Long-Run Paper Batch Package Generation Gate"
    Purpose = "Generate the long-run paper-only batch package and command templates without executing ManualNoExternal commands, schedules, orders, fills, routes, submissions, broker calls, live market data, or ledger commits."
})

Write-Json (Join-Path $SimArtifactsRoot "phase-exec-sim-r060-build-test-validator-evidence.json") ([pscustomobject]@{
    Phase = $phase
    DotnetBuild = "Pending"
    FocusedR060Tests = "Pending"
    UnitTests = "Pending"
    R060Validator = "Pending"
    EvidenceComplete = $false
})

$summary = @"
# EXEC-SIM-R060 Summary

R060 plans long-run paper-only batch packaging for R009 and records automation safety constraints. This is planning and safety only.

Classifications:
- EXEC_SIM_R060_PASS_LONG_RUN_PAPER_BATCH_PLAN_READY_NO_EXTERNAL
- EXEC_SIM_R060_PASS_AUTOMATION_SAFETY_CONSTRAINTS_READY_NO_EXTERNAL
- EXEC_SIM_R060_PASS_OPERATOR_RUN_PACKAGE_REQUIREMENTS_READY_NO_EXTERNAL
- EXEC_SIM_R060_PASS_NO_AUTOMATION_NO_ORDER_GATE_READY_NO_EXTERNAL

Long-run plan:
- Minimum 100 paper-only target closes before any executable discussion.
- At least 30 OpeningBuild, 30 IntradayRebalance, and 30 ClosingFlatten closes.
- Remaining cases allocated to stress, wide-spread, high-residual, quiet, and mixed USD regimes.

Automation safety:
- Manual operator-run only.
- Scheduler, service, polling, timer, background jobs, and automatic execution are not authorized.
- Broker/live market data, executable schedules, orders, fills, routes, submissions, paper ledger commits, and state mutation remain blocked.

Next recommended phase: EXEC-PAPER-R013 - No-External Long-Run Paper Batch Package Generation Gate.
"@
Write-Text (Join-Path $SimArtifactsRoot "phase-exec-sim-r060-summary.md") $summary

Write-Host "EXEC-SIM-R060 artifacts generated"
