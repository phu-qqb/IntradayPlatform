param(
    [string]$ArtifactsRoot = "artifacts/readiness/execution-algo"
)

$ErrorActionPreference = "Stop"

function Fail([string]$classification, [string]$message) {
    Write-Error "$classification $message"
    exit 1
}

function Read-Json([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "EXEC_ALGO_R012_FAIL_MISSING_ARTIFACT" "Missing required artifact: $path"
    }
    return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function As-Array($value) {
    if ($null -eq $value) { return @() }
    if ($value -is [System.Array]) { return $value }
    return @($value)
}

$requiredArtifacts = @(
    "phase-exec-algo-r012-summary.md",
    "phase-exec-algo-r012-r054-backtest-review-reference.json",
    "phase-exec-algo-r012-r058-paper-preview-reference.json",
    "phase-exec-algo-r012-r012-balanced-preview-reference.json",
    "phase-exec-algo-r012-r009-contract-reference.json",
    "phase-exec-algo-r012-paper-only-maturity-review-contract.json",
    "phase-exec-algo-r012-paper-only-maturity-review-result.json",
    "phase-exec-algo-r012-evidence-summary.json",
    "phase-exec-algo-r012-long-run-paper-only-expansion-plan.json",
    "phase-exec-algo-r012-long-run-paper-only-metrics.json",
    "phase-exec-algo-r012-executable-promotion-blockers.json",
    "phase-exec-algo-r012-risk-operator-review-requirements.json",
    "phase-exec-algo-r012-data-expansion-requirements.json",
    "phase-exec-algo-r012-instrument-expansion-constraints.json",
    "phase-exec-algo-r012-monitoring-reporting-requirements.json",
    "phase-exec-algo-r012-more-data-recommendation-preservation.json",
    "phase-exec-algo-r012-no-executable-promotion-preservation.json",
    "phase-exec-algo-r012-rejected-wakett-preservation.json",
    "phase-exec-algo-r012-benchmark-only-preservation.json",
    "phase-exec-algo-r012-manual-review-do-not-trade-preservation.json",
    "phase-exec-algo-r012-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-algo-r012-legacy-compatibility-preservation.json",
    "phase-exec-algo-r012-usd-pair-normalization-preservation.json",
    "phase-exec-algo-r012-direct-cross-exclusion-preservation.json",
    "phase-exec-algo-r012-cost-guidance-preservation.json",
    "phase-exec-algo-r012-nonmajor-calibration-preservation.json",
    "phase-exec-algo-r012-non-executable-acceptance-audit.json",
    "phase-exec-algo-r012-no-new-pms-cycle-audit.json",
    "phase-exec-algo-r012-no-new-backtest-audit.json",
    "phase-exec-algo-r012-no-new-simulation-audit.json",
    "phase-exec-algo-r012-no-tca-result-lines-audit.json",
    "phase-exec-algo-r012-no-executable-schedule-audit.json",
    "phase-exec-algo-r012-no-child-slices-audit.json",
    "phase-exec-algo-r012-no-child-orders-audit.json",
    "phase-exec-algo-r012-no-order-created-audit.json",
    "phase-exec-algo-r012-no-real-fill-audit.json",
    "phase-exec-algo-r012-no-execution-report-audit.json",
    "phase-exec-algo-r012-no-route-no-submission-audit.json",
    "phase-exec-algo-r012-no-paper-ledger-commit-audit.json",
    "phase-exec-algo-r012-no-polygon-api-call-audit.json",
    "phase-exec-algo-r012-no-lmax-call-audit.json",
    "phase-exec-algo-r012-no-external-api-call-audit.json",
    "phase-exec-algo-r012-no-broker-marketdata-runtime-audit.json",
    "phase-exec-algo-r012-usdjpy-caveat-preservation.json",
    "phase-exec-algo-r012-lmax-readonly-baseline-reference.json",
    "phase-exec-algo-r012-no-external-audit.json",
    "phase-exec-algo-r012-forbidden-actions-audit.json",
    "phase-exec-algo-r012-next-phase-recommendation.json",
    "phase-exec-algo-r012-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsRoot $artifact))) {
        Fail "EXEC_ALGO_R012_FAIL_MISSING_ARTIFACT" "Missing required artifact: $artifact"
    }
}

$r054 = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r012-r054-backtest-review-reference.json")
$r058 = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r012-r058-paper-preview-reference.json")
$r012 = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r012-r012-balanced-preview-reference.json")
$contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r012-r009-contract-reference.json")
$reviewContract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r012-paper-only-maturity-review-contract.json")
$result = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r012-paper-only-maturity-review-result.json")
$evidenceSummary = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r012-evidence-summary.json")
$plan = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r012-long-run-paper-only-expansion-plan.json")
$metrics = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r012-long-run-paper-only-metrics.json")
$blockers = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r012-executable-promotion-blockers.json")
$risk = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r012-risk-operator-review-requirements.json")
$data = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r012-data-expansion-requirements.json")
$instruments = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r012-instrument-expansion-constraints.json")
$monitoring = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r012-monitoring-reporting-requirements.json")
$moreData = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r012-more-data-recommendation-preservation.json")
$noPromotion = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r012-no-executable-promotion-preservation.json")
$wakett = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r012-rejected-wakett-preservation.json")
$benchmarks = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r012-benchmark-only-preservation.json")
$safetyPolicies = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r012-manual-review-do-not-trade-preservation.json")
$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r012-canonical-quarter-hour-policy-preservation.json")
$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r012-legacy-compatibility-preservation.json")
$usdPair = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r012-usd-pair-normalization-preservation.json")
$directCross = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r012-direct-cross-exclusion-preservation.json")
$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r012-cost-guidance-preservation.json")
$nonmajor = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r012-nonmajor-calibration-preservation.json")
$usdjpy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r012-usdjpy-caveat-preservation.json")
$noExternal = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r012-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r012-forbidden-actions-audit.json")
$buildEvidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r012-build-test-validator-evidence.json")

if ($r054.Dates -ne 20 -or $r054.Symbols -ne 7 -or $r054.CanonicalQuoteWindows -ne 3780 -or
    $r054.NonExecutableTcaResultLines -ne 41580 -or -not $r054.PrimaryStable -or
    -not $r054.MoreDataRecommended -or $r054.ExecutablePromotionAuthorized -or
    -not $r054.ReusedOnly -or $r054.NewBacktestRun) {
    Fail "EXEC_ALGO_R012_FAIL_R054_REFERENCE_INVALID" "R054 reference does not preserve historical stability or no-new-backtest constraints."
}

if ($r058.ReviewedRuns -ne 20 -or $r058.ReviewedPreviewLines -ne 140 -or
    -not $r058.ReadinessBindingsComplete -or $r058.HeldLines -ne 0 -or
    $r058.DirectCrossExecutableLines -ne 0 -or $r058.Decision -ne "R009StableForBroaderPaperOnlyEvaluation" -or
    $r058.ExecutablePromotionAuthorized -or -not $r058.ReusedOnly) {
    Fail "EXEC_ALGO_R012_FAIL_R058_REFERENCE_INVALID" "R058 reference does not support maturity review."
}

if ($r012.GeneratedFixtures -ne 30 -or $r012.AcceptedBatchEntries -ne 30 -or
    $r012.PaperExecutionPlanLines -ne 210 -or $r012.R009DesignOnlyPreviewLines -ne 210 -or
    $r012.ReadinessBindingsComplete -ne 210 -or $r012.HeldLines -ne 0 -or
    $r012.DirectCrossExecutableLines -ne 0 -or -not $r012.InversionsSafe -or
    -not $r012.USDJPYCaveatPreserved -or
    $r012.Decision -ne "AcceptBalancedBarRolePaperOnlyPreviewForMaturityReview" -or
    $r012.ExecutablePromotionAuthorized -or -not $r012.ReusedOnly) {
    Fail "EXEC_ALGO_R012_FAIL_R012_REFERENCE_INVALID" "R012 balanced preview reference is incomplete."
}

if ($contract.ContractVersion -ne "0.3.0-design-only-candidate" -or
    $contract.Primary -ne "CloseSeeking15mAdaptive_BalancedAdaptive_v0" -or
    $contract.Secondary -ne "CloseSeeking15mAdaptive_ResidualAwareUrgency_v0" -or
    $contract.ConditionalResidualModule -ne "ControlledResidualCross_BalancedResidualCross_v0" -or
    -not $contract.DesignOnly -or -not $contract.PaperOnly -or -not $contract.NonExecutable -or
    -not $contract.NotAnOrder -or -not $contract.NotSubmitted -or -not $contract.NoBrokerRoute -or
    $contract.BrokerReady -or $contract.LiveReady -or $contract.ExecutablePromotionAuthorized) {
    Fail "EXEC_ALGO_R012_FAIL_R009_PROMOTED_TO_EXECUTABLE" "R009 contract reference is executable or weakened."
}

if (-not $reviewContract.ReviewAndPlanningOnly -or $reviewContract.PmsCyclesRun -or
    $reviewContract.ManualNoExternalCommandsRun -or $reviewContract.NewBacktestRun -or
    $reviewContract.NewSimulationRun -or $reviewContract.TcaResultLinesCreated -or
    -not $reviewContract.DesignOnly -or -not $reviewContract.PaperOnly -or -not $reviewContract.NonExecutable -or
    $reviewContract.BrokerReady -or $reviewContract.LiveReady -or $reviewContract.ExecutablePromotionAuthorized) {
    Fail "EXEC_ALGO_R012_FAIL_REVIEW_CONTRACT_WEAKENED" "Maturity review contract permits execution or mutation."
}
foreach ($classification in @(
    "EXEC_ALGO_R012_PASS_R009_PAPER_ONLY_MATURITY_ACCEPTED_NO_EXTERNAL",
    "EXEC_ALGO_R012_PASS_LONG_RUN_PAPER_ONLY_PLAN_READY_NO_EXTERNAL",
    "EXEC_ALGO_R012_PASS_EXECUTABLE_PROMOTION_BLOCKERS_READY_NO_EXTERNAL",
    "EXEC_ALGO_R012_PASS_NO_EXECUTABLE_PROMOTION_NO_ORDER_GATE_READY_NO_EXTERNAL"
)) {
    if ((As-Array $reviewContract.Classifications) -notcontains $classification -or
        (As-Array $result.Classifications) -notcontains $classification) {
        Fail "EXEC_ALGO_R012_FAIL_CLASSIFICATION_MISSING" "Missing classification: $classification"
    }
}

$statuses = As-Array $result.DecisionStatuses
foreach ($status in @("R009StableForLongRunPaperOnlyExpansion", "R009AcceptedForLongRunPaperOnlyPlanning", "ExecutablePromotionBlocked", "MoreLongRunPaperOnlyDataRecommended")) {
    if ($statuses -notcontains $status) {
        Fail "EXEC_ALGO_R012_FAIL_MATURITY_STATUS_MISSING" "Missing decision status: $status"
    }
}
if ($result.R009MaturityStatus -ne "StableForLongRunPaperOnlyExpansion" -or
    -not $result.AcceptedForLongRunPaperOnlyPlanning -or
    -not $result.MoreDataRecommendedPreserved -or
    -not $result.MoreLongRunPaperOnlyDataRecommended -or
    $result.ExecutablePromotionAuthorized -or $result.BrokerReady -or $result.LiveReady) {
    Fail "EXEC_ALGO_R012_FAIL_MATURITY_RESULT_INVALID" "Maturity result is incomplete or executable."
}

if ($evidenceSummary.HistoricalTcaStability.NonExecutableTcaResultLines -ne 41580 -or
    $evidenceSummary.BroaderPaperPreviewStability.PreviewLinesReviewed -ne 140 -or
    $evidenceSummary.BalancedBarRolePreviewStability.PreviewLines -ne 210 -or
    $evidenceSummary.BalancedBarRolePreviewStability.OpeningBuildPreviewLines -ne 70 -or
    $evidenceSummary.BalancedBarRolePreviewStability.IntradayRebalancePreviewLines -ne 70 -or
    $evidenceSummary.BalancedBarRolePreviewStability.ClosingFlattenPreviewLines -ne 70 -or
    $evidenceSummary.ExecutablePromotionAuthorized) {
    Fail "EXEC_ALGO_R012_FAIL_EVIDENCE_SUMMARY_INVALID" "Evidence summary is incomplete."
}

if ($plan.MinimumTargetClosesBeforeExecutableDiscussion -lt 100 -or
    $plan.MinimumOpeningBuildTargetCloses -lt 30 -or
    $plan.MinimumIntradayRebalanceTargetCloses -lt 30 -or
    $plan.MinimumClosingFlattenTargetCloses -lt 30 -or
    -not $plan.MaintainZeroDirectCrossExecutableLines -or
    -not $plan.MaintainZeroHeldLinesWherePossible -or
    -not $plan.ExplicitHoldDiagnosticsRequired -or
    -not $plan.CompleteReadinessBindingsRequired -or
    $plan.ExecutablePromotionAuthorized) {
    Fail "EXEC_ALGO_R012_FAIL_LONG_RUN_PLAN_INVALID" "Long-run paper-only plan is incomplete or executable."
}

$metricText = (As-Array $metrics.Metrics) -join "`n"
foreach ($metric in @(
    "Preview line count",
    "Line coverage by symbol",
    "Line coverage by bar role",
    "Held line count and reasons",
    "Readiness binding completeness",
    "Direct-cross exclusion count",
    "Inversion stability",
    "USDJPY caveat preservation",
    "Manual review frequency",
    "Conditional residual module trigger preview frequency if available",
    "Missing evidence / missing readiness frequency",
    "No-order/no-fill/no-route/no-ledger audit status"
)) {
    if ($metricText -notlike "*$metric*") {
        Fail "EXEC_ALGO_R012_FAIL_LONG_RUN_METRIC_MISSING" "Missing metric: $metric"
    }
}

$blockerText = (As-Array $blockers.Blockers) -join "`n"
foreach ($blocker in @(
    "No broker integration authorized",
    "No live market data authorized",
    "No OMS order creation authorized",
    "No executable schedule authorized",
    "No child slices authorized",
    "No route/submission authorized",
    "No fills/execution reports authorized",
    "No paper ledger commit authorized",
    "No state mutation authorized",
    "No direct-cross execution authorized",
    "No nonmajor/EM/scandi/CNH execution without calibration",
    "More long-run paper-only data required",
    "Separate explicit executable gate required if ever considered"
)) {
    if ($blockerText -notlike "*$blocker*") {
        Fail "EXEC_ALGO_R012_FAIL_EXECUTABLE_BLOCKER_MISSING" "Missing blocker: $blocker"
    }
}
if (-not $blockers.ExecutablePromotionBlocked -or $blockers.AcceptanceIsExecutableApproval -or $blockers.ExecutablePromotionAuthorized) {
    Fail "EXEC_ALGO_R012_FAIL_EXECUTABLE_PROMOTION_BLOCKERS_WEAKENED" "Executable blockers are weakened."
}

if ($risk.RequiredScope -ne "R009DesignOnlyPreviewOnly" -or
    $risk.ApprovedForExecutableUse -or $risk.ApprovedForOrderCreation -or $risk.ApprovedForScheduleCreation -or
    $risk.ApprovedForChildSlices -or $risk.ApprovedForBrokerRouting -or $risk.ApprovedForSubmission -or
    $risk.ApprovedForFillOrExecutionReport -or $risk.ApprovedForPaperLedgerCommit -or
    $risk.ApprovedForStateMutation -or $risk.ApprovedForLiveTrading) {
    Fail "EXEC_ALGO_R012_FAIL_RISK_OPERATOR_SCOPE_WIDENED" "Risk/operator requirements authorize executable behavior."
}

if (-not $data.MoreLongRunPaperOnlyDataRecommended -or
    -not $data.CurrentCoreUsdPairUniverseRemainsPrimary -or
    -not $data.NonmajorEmScandiCnhDeferred) {
    Fail "EXEC_ALGO_R012_FAIL_DATA_REQUIREMENTS_INVALID" "Data expansion requirements are incomplete."
}

if (-not $instruments.USDPairOnlyAfterNetting -or
    -not $instruments.DirectCrossesSignalOnly -or -not $instruments.DirectCrossNettingFirst -or
    -not $instruments.DirectCrossExecutionDisabled -or -not $instruments.USDCADRequiresInversion -or
    -not $instruments.USDCHFRequiresInversion -or -not $instruments.AUDUSDNotFailed -or
    -not $instruments.NonmajorEmScandiCnhDeferredUntilCalibration) {
    Fail "EXEC_ALGO_R012_FAIL_INSTRUMENT_CONSTRAINTS_WEAKENED" "Instrument constraints are weakened."
}
if ($instruments.USDJPY.NormalizedPortfolioSymbol -ne "JPYUSD" -or
    $instruments.USDJPY.ExecutionTradableSymbol -ne "USDJPY" -or
    -not $instruments.USDJPY.RequiresInversion -or
    $instruments.USDJPY.SecurityID -ne 4004 -or
    [string]$instruments.USDJPY.SecurityIDSource -ne "8") {
    Fail "EXEC_ALGO_R012_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat is weakened."
}

if ($monitoring.SchedulerServicePollingAllowed -or $monitoring.LiveMonitoringAllowed -or -not $monitoring.PaperOnlyArtifactReportingOnly) {
    Fail "EXEC_ALGO_R012_FAIL_MONITORING_REQUIREMENTS_UNSAFE" "Monitoring/reporting requirements allow live or scheduled behavior."
}

if (-not $moreData.MoreDataRecommendationPreserved -or -not $moreData.MoreLongRunPaperOnlyDataRecommended -or -not $moreData.ExecutablePromotionDiscussionStillBlocked) {
    Fail "EXEC_ALGO_R012_FAIL_MORE_DATA_PRESERVATION" "More-data recommendation is not preserved."
}
if ($noPromotion.ExecutablePromotionAuthorized -or $noPromotion.BrokerReady -or $noPromotion.LiveReady -or $noPromotion.AcceptanceTreatedAsExecutableApproval) {
    Fail "EXEC_ALGO_R012_FAIL_EXECUTABLE_PROMOTION_AUTHORIZED" "No-executable-promotion preservation is weakened."
}
if ($wakett.Wakett -ne "RejectedNegativeBaselineOnly" -or $wakett.RejectionWeakened -or $wakett.Promoted) {
    Fail "EXEC_ALGO_R012_FAIL_WAKETT_REJECTION_WEAKENED" "Wakett rejection is weakened."
}
if ($benchmarks.Promoted) {
    Fail "EXEC_ALGO_R012_FAIL_BENCHMARK_ONLY_PROMOTED" "Benchmark-only policies were promoted."
}
if ($safetyPolicies.ManualReview -ne "SafetyOnly" -or $safetyPolicies.DoNotTrade -ne "SafetyOnly" -or $safetyPolicies.Promoted) {
    Fail "EXEC_ALGO_R012_FAIL_SAFETY_POLICY_PROMOTED" "ManualReview/DoNotTrade is weakened."
}
if (-not $canonical.FutureTimestampsUseCanonicalQuarterHour -or $canonical.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_ALGO_R012_FAIL_LEGACY_06_USED_AS_FUTURE_CANONICAL" "Canonical policy is weakened."
}
if (-not $legacy.LegacyTimestampsCompatibilityOnly -or $legacy.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_ALGO_R012_FAIL_LEGACY_06_USED_AS_FUTURE_CANONICAL" "Legacy compatibility is weakened."
}
if (-not $usdPair.USDPairOnlyAfterNetting -or -not $usdPair.AUDUSDNotFailed) {
    Fail "EXEC_ALGO_R012_FAIL_AUDUSD_MISCLASSIFIED" "USD-pair normalization or AUDUSD status is weakened."
}
if (-not $directCross.DirectCrossesSignalOnly -or -not $directCross.DirectCrossNettingFirst -or -not $directCross.DirectCrossExecutionDisabled -or $directCross.ExclusionWeakened) {
    Fail "EXEC_ALGO_R012_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct-cross exclusion is weakened."
}
if ($cost.FiveUsdPerMillionUniversalized -or $cost.FiveUsdPerMillion -ne "BestCaseMajorOnly" -or -not $cost.NonmajorCalibrationRequired) {
    Fail "EXEC_ALGO_R012_FAIL_COST_GUIDANCE_UNIVERSALIZED" "Cost guidance is weakened."
}
if (-not $nonmajor.NonmajorEmScandiCnhCalibrationRequired -or $nonmajor.NonmajorExecutionAuthorized) {
    Fail "EXEC_ALGO_R012_FAIL_NONMAJOR_CALIBRATION_WEAKENED" "Nonmajor calibration is weakened."
}
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or
    $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or -not $usdjpy.RequiresInversion -or
    $usdjpy.SecurityID -ne 4004 -or [string]$usdjpy.SecurityIDSource -ne "8" -or
    $usdjpy.CaveatWeakened) {
    Fail "EXEC_ALGO_R012_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat preservation is weakened."
}

foreach ($auditName in @(
    "phase-exec-algo-r012-non-executable-acceptance-audit.json",
    "phase-exec-algo-r012-no-new-pms-cycle-audit.json",
    "phase-exec-algo-r012-no-new-backtest-audit.json",
    "phase-exec-algo-r012-no-new-simulation-audit.json",
    "phase-exec-algo-r012-no-tca-result-lines-audit.json",
    "phase-exec-algo-r012-no-executable-schedule-audit.json",
    "phase-exec-algo-r012-no-child-slices-audit.json",
    "phase-exec-algo-r012-no-child-orders-audit.json",
    "phase-exec-algo-r012-no-order-created-audit.json",
    "phase-exec-algo-r012-no-real-fill-audit.json",
    "phase-exec-algo-r012-no-execution-report-audit.json",
    "phase-exec-algo-r012-no-route-no-submission-audit.json",
    "phase-exec-algo-r012-no-paper-ledger-commit-audit.json",
    "phase-exec-algo-r012-no-polygon-api-call-audit.json",
    "phase-exec-algo-r012-no-lmax-call-audit.json",
    "phase-exec-algo-r012-no-external-api-call-audit.json",
    "phase-exec-algo-r012-no-broker-marketdata-runtime-audit.json"
)) {
    $audit = Read-Json (Join-Path $ArtifactsRoot $auditName)
    if (-not $audit.Passed -or $audit.Occurred) {
        Fail "EXEC_ALGO_R012_FAIL_FORBIDDEN_ACTION_DETECTED" "Audit failed: $auditName"
    }
}

if (-not $noExternal.NoExternal -or $noExternal.PolygonCalled -or $noExternal.LmaxCalled -or $noExternal.ExternalApiCalled -or $noExternal.DownloadsExecuted) {
    Fail "EXEC_ALGO_R012_FAIL_EXTERNAL_API_CALLED" "No-external audit failed."
}
if ($forbidden.ForbiddenActionsDetected -or $forbidden.BrokerActivation -or $forbidden.LiveMarketData -or
    $forbidden.SchedulerServicePolling -or $forbidden.NewPmsCycle -or $forbidden.ManualNoExternalCommandsRun -or
    $forbidden.BacktestOrSimulation -or $forbidden.TcaResultLinesCreated -or $forbidden.ExecutableSchedule -or
    $forbidden.ChildSlicesOrOrders -or $forbidden.OrdersFillsReportsRoutesSubmissions -or
    $forbidden.PaperLedgerCommit -or $forbidden.StateMutation -or $forbidden.R009ExecutablePromotion) {
    Fail "EXEC_ALGO_R012_FAIL_FORBIDDEN_ACTION_DETECTED" "Forbidden action audit failed."
}

if ($buildEvidence.DotnetBuild -ne "Passed" -or
    $buildEvidence.FocusedR012Tests -ne "Passed" -or
    $buildEvidence.UnitTests -ne "Passed" -or
    $buildEvidence.R012Validator -ne "Passed" -or
    -not $buildEvidence.EvidenceComplete) {
    Fail "EXEC_ALGO_R012_FAIL_BUILD_TEST_VALIDATOR_EVIDENCE_MISSING" "Build/tests/validator evidence missing."
}

Write-Host "EXEC-ALGO-R012 validation passed"
