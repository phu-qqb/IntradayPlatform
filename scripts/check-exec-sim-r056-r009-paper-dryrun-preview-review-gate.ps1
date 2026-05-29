param(
    [string]$ArtifactsRoot = "artifacts/readiness/execution-sim"
)

$ErrorActionPreference = "Stop"

function Fail([string]$classification, [string]$message) {
    Write-Error "$classification $message"
    exit 1
}

function Read-Json([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "EXEC_SIM_R056_FAIL_BUILD_OR_TESTS" "Missing required artifact: $path"
    }

    return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

$requiredArtifacts = @(
    "phase-exec-sim-r056-summary.md",
    "phase-exec-sim-r056-r008-preview-reference.json",
    "phase-exec-sim-r056-r009-contract-reference.json",
    "phase-exec-sim-r056-paper-dryrun-preview-review-contract.json",
    "phase-exec-sim-r056-operator-review-report.md",
    "phase-exec-sim-r056-operator-review-report.json",
    "phase-exec-sim-r056-preview-line-coverage-review.json",
    "phase-exec-sim-r056-per-symbol-preview-review.json",
    "phase-exec-sim-r056-aggregate-preview-review.json",
    "phase-exec-sim-r056-closing-flatten-no-overnight-review.json",
    "phase-exec-sim-r056-readiness-binding-review.json",
    "phase-exec-sim-r056-risk-operator-approval-scope-review.json",
    "phase-exec-sim-r056-direct-cross-netting-review.json",
    "phase-exec-sim-r056-inversion-review.json",
    "phase-exec-sim-r056-r009-policy-selection-review.json",
    "phase-exec-sim-r056-hold-condition-review.json",
    "phase-exec-sim-r056-preview-decision.json",
    "phase-exec-sim-r056-next-paper-only-evaluation-recommendation.json",
    "phase-exec-sim-r056-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-sim-r056-legacy-compatibility-preservation.json",
    "phase-exec-sim-r056-usd-pair-normalization-preservation.json",
    "phase-exec-sim-r056-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r056-cost-guidance-preservation.json",
    "phase-exec-sim-r056-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r056-no-broker-activation-audit.json",
    "phase-exec-sim-r056-no-live-marketdata-audit.json",
    "phase-exec-sim-r056-no-scheduler-service-polling-audit.json",
    "phase-exec-sim-r056-no-executable-schedule-audit.json",
    "phase-exec-sim-r056-no-child-slices-audit.json",
    "phase-exec-sim-r056-no-child-orders-audit.json",
    "phase-exec-sim-r056-no-order-created-audit.json",
    "phase-exec-sim-r056-no-real-fill-audit.json",
    "phase-exec-sim-r056-no-execution-report-audit.json",
    "phase-exec-sim-r056-no-route-no-submission-audit.json",
    "phase-exec-sim-r056-no-paper-ledger-commit-audit.json",
    "phase-exec-sim-r056-no-polygon-api-call-audit.json",
    "phase-exec-sim-r056-no-lmax-call-audit.json",
    "phase-exec-sim-r056-no-external-api-call-audit.json",
    "phase-exec-sim-r056-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r056-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r056-no-external-audit.json",
    "phase-exec-sim-r056-forbidden-actions-audit.json",
    "phase-exec-sim-r056-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    $path = Join-Path $ArtifactsRoot $artifact
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "EXEC_SIM_R056_FAIL_BUILD_OR_TESTS" "Missing required artifact: $artifact"
    }
}

$coverage = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r056-preview-line-coverage-review.json")
$perSymbol = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r056-per-symbol-preview-review.json")
$aggregate = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r056-aggregate-preview-review.json")
$readiness = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r056-readiness-binding-review.json")
$approvalScope = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r056-risk-operator-approval-scope-review.json")
$directCross = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r056-direct-cross-netting-review.json")
$inversion = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r056-inversion-review.json")
$policy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r056-r009-policy-selection-review.json")
$decision = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r056-preview-decision.json")
$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r056-canonical-quarter-hour-policy-preservation.json")
$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r056-legacy-compatibility-preservation.json")
$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r056-cost-guidance-preservation.json")
$usdPair = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r056-usd-pair-normalization-preservation.json")
$forbidden = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r056-forbidden-actions-audit.json")
$noExternal = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r056-no-external-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r056-build-test-validator-evidence.json")

if ($noExternal.PolygonCalled -or $noExternal.LmaxCalled -or $noExternal.ExternalApiCalled -or $noExternal.FilesDownloaded) {
    Fail "EXEC_SIM_R056_FAIL_API_CALL_DETECTED" "No-external audit reports external activity."
}

if ($forbidden.ForbiddenActionsDetected -or
    $forbidden.BrokerActivation -or
    $forbidden.LiveMarketData -or
    $forbidden.SchedulerServicePolling -or
    $forbidden.PMSCycleRun -or
    $forbidden.BacktestRun -or
    $forbidden.SimulationRun -or
    $forbidden.TcaResultLinesCreated -or
    $forbidden.ExecutableSchedulesCreated -or
    $forbidden.ChildSlicesCreated -or
    $forbidden.ChildOrdersCreated -or
    $forbidden.OrdersCreated -or
    $forbidden.FillsCreated -or
    $forbidden.ExecutionReportsCreated -or
    $forbidden.RoutesCreated -or
    $forbidden.SubmissionsCreated -or
    $forbidden.PaperLedgerCommit -or
    $forbidden.StateMutation) {
    Fail "EXEC_SIM_R056_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden action audit reports a blocked action."
}

if (-not $coverage.AllPreviewLinesCovered -or [int]$coverage.ActualPreviewLineCount -lt 7) {
    Fail "EXEC_SIM_R056_FAIL_PREVIEW_LINES_MISSING" "R009 preview line coverage is missing or fewer than 7 lines."
}

if ($coverage.PreviewLinesRepresentOrders -or $coverage.PreviewLinesRepresentSchedules -or $coverage.PreviewLinesRepresentFills -or $coverage.PreviewLinesRepresentRoutes) {
    Fail "EXEC_SIM_R056_FAIL_PREVIEW_REPRESENTED_AS_ORDER_PATH" "Preview lines are represented as an order/schedule/fill/route path."
}

foreach ($line in $perSymbol.Reviews) {
    if (-not $line.NonExecutable -or -not $line.NotAnOrder -or -not $line.NotSubmitted -or -not $line.NoBrokerRoute -or -not $line.NoChildSlices -or -not $line.NoExecutableSchedule -or -not $line.NoFill -or -not $line.NoExecutionReport -or -not $line.NoRoute -or -not $line.NoSubmission -or -not $line.NoPaperLedgerCommit) {
        Fail "EXEC_SIM_R056_FAIL_PREVIEW_REPRESENTED_AS_ORDER_PATH" "Line $($line.PaperExecutionPlanLineId) does not preserve all non-executable/no-order flags."
    }

    if (-not $line.CanonicalQuarterHourTimestampConfirmed -or $line.CanonicalTargetCloseLocal -match ":06|:21|:36|:51") {
        Fail "EXEC_SIM_R056_FAIL_LEGACY_06_USED_AS_FUTURE_CANONICAL" "Line $($line.PaperExecutionPlanLineId) weakens canonical timestamp policy."
    }
}

if (-not $aggregate.AllReadinessBindingsPresent -or $readiness.QuoteWindowReadinessBindings -ne "7/7" -or $readiness.CloseBenchmarkReadinessBindings -ne "7/7" -or $readiness.FeedQualityReadinessBindings -ne "7/7") {
    Fail "EXEC_SIM_R056_FAIL_PREVIEW_LINES_MISSING" "Readiness binding review is incomplete."
}

if ($approvalScope.ScopeWidenedBeyondDesignOnlyPreview -or
    $approvalScope.ApprovedForExecutableUse -or
    $approvalScope.ApprovedForOrderCreation -or
    $approvalScope.ApprovedForBrokerRouting -or
    $approvalScope.ApprovedForPaperLedgerCommit -or
    $decision.ExecutableApproval -or
    $decision.OrderApproval -or
    $decision.BrokerRoutingApproval -or
    $decision.PaperLedgerCommitApproval) {
    Fail "EXEC_SIM_R056_FAIL_EXECUTABLE_APPROVAL_SCOPE_WIDENED" "Preview approval was widened beyond design-only preview."
}

if ($policy.ExecutablePromotionAuthorized -or $policy.WakettPromoted -or $policy.BenchmarkPoliciesPromoted -or $policy.ManualReviewDoNotTradePromoted) {
    Fail "EXEC_SIM_R056_FAIL_R009_PROMOTED_TO_EXECUTABLE" "Policy selection review promotes an excluded policy or executable use."
}

if ($directCross.DirectCrossExecutionEnabled -or -not $directCross.DirectCrossesExcluded -or -not $directCross.NettingFirstPreserved) {
    Fail "EXEC_SIM_R056_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct-cross exclusion is weakened."
}

if ($cost.FiveUsdPerMillionUniversalized -or -not $cost.FiveUsdPerMillionBestCaseMajorOnly) {
    Fail "EXEC_SIM_R056_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million cost guidance is universalized."
}

if (-not $inversion.USDJPYCaveatPreserved) {
    Fail "EXEC_SIM_R056_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat is weakened."
}

if (-not $usdPair.AUDUSDNotFailed) {
    Fail "EXEC_SIM_R056_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD is misclassified."
}

if ($legacy.Legacy06UsedAsFutureCanonical -or -not $legacy.LegacyTimestampsCompatibilityOnly -or -not $canonical.AppliesToFutureTimestamps) {
    Fail "EXEC_SIM_R056_FAIL_LEGACY_06_USED_AS_FUTURE_CANONICAL" "Legacy compatibility or canonical quarter-hour policy is weakened."
}

if ($evidence.DotnetBuild -ne "Passed" -or $evidence.FocusedR056Tests -ne "Passed" -or $evidence.UnitTests -ne "Passed" -or $evidence.R056Validator -ne "Passed") {
    Fail "EXEC_SIM_R056_FAIL_BUILD_OR_TESTS" "Build/tests/validator evidence is missing or not passed."
}

Write-Output "EXEC_SIM_R056_PASS_R009_PAPER_DRYRUN_PREVIEW_REVIEW_READY_NO_EXTERNAL"
Write-Output "EXEC_SIM_R056_PASS_PREVIEW_ACCEPTED_FOR_BROADER_PAPER_ONLY_EVALUATION_NO_EXTERNAL"
Write-Output "EXEC_SIM_R056_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
