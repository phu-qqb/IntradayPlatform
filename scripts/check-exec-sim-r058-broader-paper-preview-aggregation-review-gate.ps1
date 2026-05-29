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
        Fail "EXEC_SIM_R058_FAIL_BUILD_OR_TESTS" "Missing required artifact: $path"
    }

    return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
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

$requiredArtifacts = @(
    "phase-exec-sim-r058-summary.md",
    "phase-exec-sim-r058-r011-preview-reference.json",
    "phase-exec-sim-r058-r009-contract-reference.json",
    "phase-exec-sim-r058-preview-aggregation-review-contract.json",
    "phase-exec-sim-r058-operator-review-report.md",
    "phase-exec-sim-r058-operator-review-report.json",
    "phase-exec-sim-r058-preview-line-coverage-review.json",
    "phase-exec-sim-r058-per-symbol-preview-review.json",
    "phase-exec-sim-r058-per-batch-entry-preview-review.json",
    "phase-exec-sim-r058-aggregate-preview-review.json",
    "phase-exec-sim-r058-readiness-binding-stability-review.json",
    "phase-exec-sim-r058-inversion-stability-review.json",
    "phase-exec-sim-r058-direct-cross-netting-review.json",
    "phase-exec-sim-r058-risk-operator-approval-scope-review.json",
    "phase-exec-sim-r058-bar-role-coverage-review.json",
    "phase-exec-sim-r058-r009-policy-selection-stability-review.json",
    "phase-exec-sim-r058-held-line-diagnostics.json",
    "phase-exec-sim-r058-stability-decision.json",
    "phase-exec-sim-r058-next-paper-only-evaluation-recommendation.json",
    "phase-exec-sim-r058-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-sim-r058-legacy-compatibility-preservation.json",
    "phase-exec-sim-r058-usd-pair-normalization-preservation.json",
    "phase-exec-sim-r058-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r058-cost-guidance-preservation.json",
    "phase-exec-sim-r058-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r058-no-broker-activation-audit.json",
    "phase-exec-sim-r058-no-live-marketdata-audit.json",
    "phase-exec-sim-r058-no-scheduler-service-polling-audit.json",
    "phase-exec-sim-r058-no-new-pms-cycle-audit.json",
    "phase-exec-sim-r058-no-new-backtest-audit.json",
    "phase-exec-sim-r058-no-new-simulation-audit.json",
    "phase-exec-sim-r058-no-tca-result-lines-audit.json",
    "phase-exec-sim-r058-no-executable-schedule-audit.json",
    "phase-exec-sim-r058-no-child-slices-audit.json",
    "phase-exec-sim-r058-no-child-orders-audit.json",
    "phase-exec-sim-r058-no-order-created-audit.json",
    "phase-exec-sim-r058-no-real-fill-audit.json",
    "phase-exec-sim-r058-no-execution-report-audit.json",
    "phase-exec-sim-r058-no-route-no-submission-audit.json",
    "phase-exec-sim-r058-no-paper-ledger-commit-audit.json",
    "phase-exec-sim-r058-no-polygon-api-call-audit.json",
    "phase-exec-sim-r058-no-lmax-call-audit.json",
    "phase-exec-sim-r058-no-external-api-call-audit.json",
    "phase-exec-sim-r058-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r058-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r058-no-external-audit.json",
    "phase-exec-sim-r058-forbidden-actions-audit.json",
    "phase-exec-sim-r058-next-phase-recommendation.json",
    "phase-exec-sim-r058-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    $path = Join-Path $ArtifactsRoot $artifact
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "EXEC_SIM_R058_FAIL_BUILD_OR_TESTS" "Missing required artifact: $artifact"
    }
}

$r011Reference = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-r011-preview-reference.json")
$contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-r009-contract-reference.json")
$reviewContract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-preview-aggregation-review-contract.json")
$operatorReview = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-operator-review-report.json")
$coverage = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-preview-line-coverage-review.json")
$perSymbol = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-per-symbol-preview-review.json")
$perBatch = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-per-batch-entry-preview-review.json")
$aggregate = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-aggregate-preview-review.json")
$readiness = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-readiness-binding-stability-review.json")
$inversion = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-inversion-stability-review.json")
$directCross = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-direct-cross-netting-review.json")
$approval = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-risk-operator-approval-scope-review.json")
$barRole = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-bar-role-coverage-review.json")
$policy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-r009-policy-selection-stability-review.json")
$held = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-held-line-diagnostics.json")
$decision = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-stability-decision.json")
$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-canonical-quarter-hour-policy-preservation.json")
$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-legacy-compatibility-preservation.json")
$usdPair = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-usd-pair-normalization-preservation.json")
$directCrossPreservation = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-direct-cross-exclusion-preservation.json")
$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-cost-guidance-preservation.json")
$usdjpy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-usdjpy-caveat-preservation.json")
$noExternal = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-build-test-validator-evidence.json")

if ($r011Reference.PreviewLineCount -ne 140 -or
    $r011Reference.BatchEntryCount -ne 20 -or
    -not $r011Reference.ReusedOnly -or
    $r011Reference.NewPmsCycleRun) {
    Fail "EXEC_SIM_R058_FAIL_R011_REFERENCE_INVALID" "R011 preview reference is missing, partial, or reports a new PMS cycle."
}

if (-not $contract.NonExecutable -or
    -not $contract.NotAnOrder -or
    -not $contract.NoBrokerRoute -or
    $contract.ExecutablePromotionAuthorized) {
    Fail "EXEC_SIM_R058_FAIL_R009_PROMOTED_TO_EXECUTABLE" "R009 contract reference is executable or weakened."
}

if (-not $reviewContract.ReviewOnly -or
    -not $reviewContract.ReusesR011PreviewLines -or
    -not $reviewContract.RequiresNoNewPmsCycle -or
    -not $reviewContract.RequiresNoBacktestOrSimulation -or
    -not $reviewContract.RequiresNoOrderDomainOutputs -or
    $reviewContract.ExecutablePromotionAuthorized) {
    Fail "EXEC_SIM_R058_FAIL_REVIEW_CONTRACT_WEAKENED" "R058 review contract is not a no-execution review-only contract."
}

if ($coverage.ReviewedBatchEntries -ne 20 -or
    $coverage.ReviewedPreviewLines -ne 140 -or
    $coverage.FewerThanExpectedPreviewLines -or
    $coverage.CoverageStatus -ne "Complete") {
    Fail "EXEC_SIM_R058_FAIL_PREVIEW_LINE_COVERAGE" "Fewer than 140 preview lines or 20 batch entries were reviewed without valid reason."
}

if ($perSymbol.SymbolCount -ne 7) {
    Fail "EXEC_SIM_R058_FAIL_PER_SYMBOL_REVIEW" "Per-symbol review does not cover seven supported execution symbols."
}
foreach ($symbolReview in (As-Array $perSymbol.Reviews)) {
    if ($symbolReview.PreviewLineCount -ne 20 -or
        $symbolReview.BatchCoverageCount -ne 20 -or
        $symbolReview.HeldLineCount -ne 0 -or
        -not $symbolReview.StableForPaperOnlyReview) {
        Fail "EXEC_SIM_R058_FAIL_PER_SYMBOL_REVIEW" "Per-symbol review is incomplete for $($symbolReview.Symbol)."
    }
}

if ($perBatch.BatchEntryCount -ne 20) {
    Fail "EXEC_SIM_R058_FAIL_PER_BATCH_REVIEW" "Per-batch review does not cover 20 batch entries."
}
foreach ($batchReview in (As-Array $perBatch.Reviews)) {
    if ($batchReview.PreviewLineCount -ne 7 -or
        -not $batchReview.CanonicalQuarterHourTimestampConfirmed -or
        -not $batchReview.ReadinessComplete -or
        $batchReview.HeldLineCount -ne 0 -or
        -not $batchReview.NonExecutableFlagsPreserved) {
        Fail "EXEC_SIM_R058_FAIL_PER_BATCH_REVIEW" "Per-batch review is incomplete for $($batchReview.BatchEntryId)."
    }
}

if (-not $aggregate.ManualNoExternalRunsCompletedSafely -or
    $aggregate.ManualNoExternalRunCount -ne 20 -or
    $aggregate.PreviewLinesReviewed -ne 140 -or
    -not $aggregate.USDPairOnlyAfterNetting -or
    $aggregate.DirectCrossExecutableLines -ne 0 -or
    $aggregate.CompleteReadinessBindings -ne 140 -or
    $aggregate.HeldLines -ne 0 -or
    $aggregate.NonExecutableViolations -ne 0 -or
    -not $aggregate.StableForBroaderPaperOnlyEvaluation) {
    Fail "EXEC_SIM_R058_FAIL_AGGREGATE_REVIEW" "Aggregate review is incomplete or unsafe."
}

if ($readiness.QuoteWindowReadinessBindings -ne 140 -or
    $readiness.CloseBenchmarkReadinessBindings -ne 140 -or
    $readiness.FeedQualityReadinessBindings -ne 140 -or
    $readiness.CompleteReadinessBindingCount -ne 140 -or
    $readiness.MissingReadinessBindingCount -ne 0 -or
    -not $readiness.Stable) {
    Fail "EXEC_SIM_R058_FAIL_READINESS_BINDING_STABILITY" "Readiness binding stability review is incomplete."
}

if (-not $inversion.Stable -or
    $inversion.USDJPYLines -ne 20 -or
    $inversion.USDCADLines -ne 20 -or
    $inversion.USDCHFLines -ne 20 -or
    -not $inversion.USDJPYCaveatPreserved) {
    Fail "EXEC_SIM_R058_FAIL_USDJPY_CAVEAT_WEAKENED" "Inversion stability review is incomplete."
}

if (-not $directCross.Stable -or
    -not $directCross.USDPairOnlyAfterNetting -or
    $directCross.DirectCrossExecutableLines -ne 0) {
    Fail "EXEC_SIM_R058_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct-cross/netting review is unsafe."
}

if (-not $approval.ApprovedForPreviewOnly -or
    $approval.ScopeWidened -or
    $approval.ApprovedForExecutableUse -or
    $approval.ApprovedForOrderCreation -or
    $approval.ApprovedForBrokerRouting -or
    $approval.ApprovedForPaperLedgerCommit) {
    Fail "EXEC_SIM_R058_FAIL_PREVIEW_APPROVAL_WIDENED" "Risk/operator approval scope widened beyond preview-only."
}

if (-not $barRole.IncludesClosingFlatten -or -not $barRole.IncludesIntradayRebalance) {
    Fail "EXEC_SIM_R058_FAIL_BAR_ROLE_COVERAGE" "Bar-role review does not cover closing flatten and intraday rebalance cases."
}

if (-not $policy.PrimaryPresentOnAllLines -or
    -not $policy.SecondaryPresentOnAllLines -or
    -not $policy.ConditionalPresentOnAllLines -or
    -not $policy.StableForPaperOnlyEvaluationExpansion -or
    $policy.ExecutablePromotionAuthorized) {
    Fail "EXEC_SIM_R058_FAIL_R009_POLICY_STABILITY" "R009 policy selection review is incomplete or executable."
}

if ($held.HeldLineCount -ne 0) {
    Fail "EXEC_SIM_R058_FAIL_HELD_LINES_PRESENT" "Held-line diagnostics are non-empty."
}

if ($decision.Decision -ne "R009StableForBroaderPaperOnlyEvaluation" -or
    -not $decision.R009StableForBroaderPaperOnlyEvaluation -or
    $decision.ExecutablePromotionAuthorized -or
    $decision.OrdersAuthorized -or
    $decision.RoutesAuthorized -or
    $decision.PaperLedgerCommitAuthorized -or
    $decision.StateMutationAuthorized) {
    Fail "EXEC_SIM_R058_FAIL_STABILITY_DECISION_INVALID" "Stability decision is missing or authorizes executable behavior."
}

if ($operatorReview.ReviewedPreviewLines -ne 140 -or
    $operatorReview.ReviewedBatchEntries -ne 20 -or
    -not $operatorReview.All20BroaderRunsCompletedSafely -or
    -not $operatorReview.USDPairOnlyAfterNetting -or
    -not $operatorReview.DirectCrossesExcluded -or
    -not $operatorReview.ReadinessBindingsComplete -or
    -not $operatorReview.InversionMappingsSafe -or
    -not $operatorReview.USDJPYCaveatPreserved -or
    $operatorReview.HeldLines -ne 0 -or
    $operatorReview.OrderLikeOutputsDetected -ne 0 -or
    -not $operatorReview.StableEnoughForFurtherPaperOnlyEvaluationExpansion) {
    Fail "EXEC_SIM_R058_FAIL_OPERATOR_REVIEW_INVALID" "Operator review does not answer the required safety/stability questions."
}

if ($canonical.Legacy06UsedAsFutureCanonical -or
    -not $canonical.FutureTimestampsUseCanonicalQuarterHour -or
    $legacy.Legacy06UsedAsFutureCanonical -or
    -not $legacy.LegacyTimestampsCompatibilityOnly) {
    Fail "EXEC_SIM_R058_FAIL_LEGACY_06_USED_AS_FUTURE_CANONICAL" "Canonical/legacy timestamp preservation is weakened."
}

if (-not $usdPair.USDPairOnlyAfterNetting -or -not $usdPair.AUDUSDNotFailed) {
    Fail "EXEC_SIM_R058_FAIL_AUDUSD_MISCLASSIFIED" "USD-pair normalization or AUDUSD classification is weakened."
}

if ($directCrossPreservation.DirectCrossExecutionEnabled -or -not $directCrossPreservation.NettingFirst) {
    Fail "EXEC_SIM_R058_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct-cross exclusion preservation is weakened."
}

if ($cost.FiveUsdPerMillionUniversalized -or -not $cost.FiveUsdPerMillionBestCaseMajorOnly) {
    Fail "EXEC_SIM_R058_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million guidance is universalized."
}

if (-not $usdjpy.RequiresInversion -or
    $usdjpy.SecurityID -ne "4004" -or
    $usdjpy.SecurityIDSource -ne "8" -or
    $usdjpy.USDJPYCaveatWeakened) {
    Fail "EXEC_SIM_R058_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat preservation is weakened."
}

if ($noExternal.PolygonCalled -or
    $noExternal.LmaxCalled -or
    $noExternal.ExternalApiCalled -or
    $noExternal.BrokerActivation -or
    $noExternal.LiveMarketData -or
    -not $noExternal.NoExternal) {
    Fail "EXEC_SIM_R058_FAIL_EXTERNAL_OR_BROKER_ACTIVITY" "No-external audit reports external or broker activity."
}

if ($forbidden.ForbiddenActionsDetected -or
    $forbidden.BrokerActivation -or
    $forbidden.LiveMarketData -or
    $forbidden.SchedulerServicePolling -or
    $forbidden.NewPmsCycleRun -or
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
    $forbidden.StateMutation -or
    $forbidden.R009PromotedToExecutable) {
    Fail "EXEC_SIM_R058_FAIL_FORBIDDEN_ACTION_DETECTED" "Forbidden action audit reports a blocked action."
}

if ($evidence.DotnetBuild -ne "Passed" -or
    $evidence.FocusedR058Tests -ne "Passed" -or
    $evidence.UnitTests -ne "Passed" -or
    $evidence.R058Validator -ne "Passed" -or
    -not $evidence.EvidenceComplete) {
    Fail "EXEC_SIM_R058_FAIL_BUILD_OR_TESTS" "Build/tests/validator evidence is missing or not passed."
}

Write-Output "EXEC_SIM_R058_PASS_BROADER_PAPER_PREVIEW_AGGREGATION_REVIEW_READY_NO_EXTERNAL"
Write-Output "EXEC_SIM_R058_PASS_R009_STABILITY_DECISION_READY_NO_EXTERNAL"
Write-Output "EXEC_SIM_R058_PASS_R009_STABLE_FOR_BROADER_PAPER_ONLY_EVALUATION_NO_EXTERNAL"
Write-Output "EXEC_SIM_R058_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
