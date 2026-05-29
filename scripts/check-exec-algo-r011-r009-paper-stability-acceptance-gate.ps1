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
        Fail "EXEC_ALGO_R011_FAIL_MISSING_ARTIFACT" "Missing required artifact: $path"
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
    "phase-exec-algo-r011-summary.md",
    "phase-exec-algo-r011-r054-backtest-review-reference.json",
    "phase-exec-algo-r011-r058-paper-preview-reference.json",
    "phase-exec-algo-r011-r009-contract-reference.json",
    "phase-exec-algo-r011-paper-only-stability-acceptance-contract.json",
    "phase-exec-algo-r011-paper-only-stability-acceptance-result.json",
    "phase-exec-algo-r011-next-stage-paper-only-requirements.json",
    "phase-exec-algo-r011-executable-promotion-blockers.json",
    "phase-exec-algo-r011-risk-operator-review-requirements.json",
    "phase-exec-algo-r011-data-expansion-requirements.json",
    "phase-exec-algo-r011-instrument-expansion-constraints.json",
    "phase-exec-algo-r011-more-data-recommendation-preservation.json",
    "phase-exec-algo-r011-no-executable-promotion-preservation.json",
    "phase-exec-algo-r011-rejected-wakett-preservation.json",
    "phase-exec-algo-r011-benchmark-only-preservation.json",
    "phase-exec-algo-r011-manual-review-do-not-trade-preservation.json",
    "phase-exec-algo-r011-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-algo-r011-legacy-compatibility-preservation.json",
    "phase-exec-algo-r011-usd-pair-normalization-preservation.json",
    "phase-exec-algo-r011-direct-cross-exclusion-preservation.json",
    "phase-exec-algo-r011-cost-guidance-preservation.json",
    "phase-exec-algo-r011-nonmajor-calibration-preservation.json",
    "phase-exec-algo-r011-non-executable-acceptance-audit.json",
    "phase-exec-algo-r011-no-new-backtest-audit.json",
    "phase-exec-algo-r011-no-new-simulation-audit.json",
    "phase-exec-algo-r011-no-tca-result-lines-audit.json",
    "phase-exec-algo-r011-no-executable-schedule-audit.json",
    "phase-exec-algo-r011-no-child-slices-audit.json",
    "phase-exec-algo-r011-no-child-orders-audit.json",
    "phase-exec-algo-r011-no-order-created-audit.json",
    "phase-exec-algo-r011-no-real-fill-audit.json",
    "phase-exec-algo-r011-no-execution-report-audit.json",
    "phase-exec-algo-r011-no-route-no-submission-audit.json",
    "phase-exec-algo-r011-no-paper-ledger-commit-audit.json",
    "phase-exec-algo-r011-no-polygon-api-call-audit.json",
    "phase-exec-algo-r011-no-lmax-call-audit.json",
    "phase-exec-algo-r011-no-external-api-call-audit.json",
    "phase-exec-algo-r011-no-broker-marketdata-runtime-audit.json",
    "phase-exec-algo-r011-usdjpy-caveat-preservation.json",
    "phase-exec-algo-r011-lmax-readonly-baseline-reference.json",
    "phase-exec-algo-r011-no-external-audit.json",
    "phase-exec-algo-r011-forbidden-actions-audit.json",
    "phase-exec-algo-r011-next-phase-recommendation.json",
    "phase-exec-algo-r011-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsRoot $artifact))) {
        Fail "EXEC_ALGO_R011_FAIL_MISSING_ARTIFACT" "Missing required artifact: $artifact"
    }
}

$r054 = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r011-r054-backtest-review-reference.json")
$r058 = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r011-r058-paper-preview-reference.json")
$contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r011-r009-contract-reference.json")
$acceptanceContract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r011-paper-only-stability-acceptance-contract.json")
$acceptance = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r011-paper-only-stability-acceptance-result.json")
$requirements = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r011-next-stage-paper-only-requirements.json")
$blockers = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r011-executable-promotion-blockers.json")
$risk = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r011-risk-operator-review-requirements.json")
$data = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r011-data-expansion-requirements.json")
$instruments = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r011-instrument-expansion-constraints.json")
$moreData = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r011-more-data-recommendation-preservation.json")
$noPromotion = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r011-no-executable-promotion-preservation.json")
$wakett = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r011-rejected-wakett-preservation.json")
$benchmarks = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r011-benchmark-only-preservation.json")
$safetyPolicies = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r011-manual-review-do-not-trade-preservation.json")
$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r011-canonical-quarter-hour-policy-preservation.json")
$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r011-legacy-compatibility-preservation.json")
$usdPair = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r011-usd-pair-normalization-preservation.json")
$directCross = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r011-direct-cross-exclusion-preservation.json")
$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r011-cost-guidance-preservation.json")
$nonmajor = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r011-nonmajor-calibration-preservation.json")
$usdjpy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r011-usdjpy-caveat-preservation.json")
$noExternal = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r011-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r011-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r011-build-test-validator-evidence.json")

if ($r054.PolicyDecision -ne "KeepR009PrimaryDesignOnlyCandidate" -or
    $r054.ParameterContractDecision -ne "R009StableForOperatorAcceptancePlanning" -or
    -not $r054.MoreDataRecommended -or
    $r054.ExecutablePromotionAuthorized -or
    -not $r054.ReusedOnly -or
    $r054.NewBacktestRun) {
    Fail "EXEC_ALGO_R011_FAIL_R054_REFERENCE_INVALID" "R054 reference is missing stability, more-data, or no-new-backtest constraints."
}

if ($r058.Decision -ne "R009StableForBroaderPaperOnlyEvaluation" -or
    $r058.AcceptanceScope -ne "BroaderPaperOnlyEvaluationExpansion" -or
    $r058.ReviewedRuns -ne 20 -or
    $r058.ReviewedPreviewLines -ne 140 -or
    -not $r058.ReadinessBindingsComplete -or
    $r058.HeldLines -ne 0 -or
    $r058.DirectCrossExecutableLines -ne 0 -or
    $r058.ExecutablePromotionAuthorized -or
    -not $r058.ReusedOnly) {
    Fail "EXEC_ALGO_R011_FAIL_R058_REFERENCE_INVALID" "R058 preview reference does not support paper-only stability acceptance."
}

if ($contract.ContractVersion -ne "0.3.0-design-only-candidate" -or
    $contract.Primary -ne "CloseSeeking15mAdaptive_BalancedAdaptive_v0" -or
    $contract.Secondary -ne "CloseSeeking15mAdaptive_ResidualAwareUrgency_v0" -or
    $contract.ConditionalResidualModule -ne "ControlledResidualCross_BalancedResidualCross_v0" -or
    -not $contract.DesignOnly -or
    -not $contract.PaperOnly -or
    -not $contract.NonExecutable -or
    -not $contract.NotAnOrder -or
    -not $contract.NotSubmitted -or
    -not $contract.NoBrokerRoute -or
    $contract.BrokerReady -or
    $contract.LiveReady -or
    $contract.ExecutablePromotionAuthorized) {
    Fail "EXEC_ALGO_R011_FAIL_R009_PROMOTED_TO_EXECUTABLE" "R009 contract reference is missing or executable."
}

if (-not $acceptanceContract.DesignOnly -or
    -not $acceptanceContract.PaperOnly -or
    -not $acceptanceContract.NonExecutable -or
    $acceptanceContract.ExecutablePromotionAuthorized -or
    $acceptanceContract.CreatesSchedules -or
    $acceptanceContract.CreatesChildSlices -or
    $acceptanceContract.CreatesOrders -or
    $acceptanceContract.CreatesFills -or
    $acceptanceContract.CreatesRoutesOrSubmissions -or
    $acceptanceContract.CommitsPaperLedger -or
    $acceptanceContract.MutatesState) {
    Fail "EXEC_ALGO_R011_FAIL_ACCEPTANCE_CONTRACT_WEAKENED" "Acceptance contract permits executable behavior."
}

$statuses = As-Array $acceptance.DecisionStatuses
foreach ($expected in @("R009StableForBroaderPaperOnlyEvaluation", "R009AcceptedForNextStagePaperOnlyExpansion", "ExecutablePromotionBlocked", "MoreDataRecommendedPreserved")) {
    if ($statuses -notcontains $expected) {
        Fail "EXEC_ALGO_R011_FAIL_ACCEPTANCE_STATUS_MISSING" "Missing acceptance status: $expected"
    }
}
foreach ($expectedClass in @(
    "EXEC_ALGO_R011_PASS_R009_PAPER_ONLY_STABILITY_ACCEPTED_NO_EXTERNAL",
    "EXEC_ALGO_R011_PASS_NEXT_STAGE_PAPER_ONLY_REQUIREMENTS_READY_NO_EXTERNAL",
    "EXEC_ALGO_R011_PASS_EXECUTABLE_PROMOTION_BLOCKERS_READY_NO_EXTERNAL",
    "EXEC_ALGO_R011_PASS_NO_EXECUTABLE_PROMOTION_NO_ORDER_GATE_READY_NO_EXTERNAL"
)) {
    if ((As-Array $acceptance.Classifications) -notcontains $expectedClass) {
        Fail "EXEC_ALGO_R011_FAIL_CLASSIFICATION_MISSING" "Missing classification: $expectedClass"
    }
}

if ($acceptance.R009Status -ne "StableForBroaderPaperOnlyEvaluation" -or
    -not $acceptance.AcceptedForNextStagePaperOnlyExpansion -or
    -not $acceptance.R054MoreDataRecommended -or
    $acceptance.R058AcceptanceScope -ne "BroaderPaperOnlyEvaluationExpansion" -or
    $acceptance.ExecutablePromotionAuthorized -or
    $acceptance.BrokerReady -or
    $acceptance.LiveReady) {
    Fail "EXEC_ALGO_R011_FAIL_ACCEPTANCE_RESULT_INVALID" "Acceptance result is incomplete or executable."
}

if (-not $requirements.NoExecutableSchedules -or
    -not $requirements.NoOrdersFillsRoutesSubmissions -or
    -not $requirements.NoPaperLedgerCommit -or
    -not $requirements.ManualNoExternalOnly) {
    Fail "EXEC_ALGO_R011_FAIL_NEXT_STAGE_REQUIREMENTS_WEAKENED" "Next-stage requirements do not preserve no-execution constraints."
}

$blockerText = (As-Array $blockers.Blockers) -join "`n"
foreach ($requiredBlocker in @(
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
    "More data recommended remains open",
    "Separate explicit executable gate required if ever considered"
)) {
    if ($blockerText -notlike "*$requiredBlocker*") {
        Fail "EXEC_ALGO_R011_FAIL_EXECUTABLE_BLOCKER_MISSING" "Missing executable blocker: $requiredBlocker"
    }
}
if (-not $blockers.ExecutablePromotionBlocked -or $blockers.AcceptanceIsExecutableApproval -or $blockers.ExecutablePromotionAuthorized) {
    Fail "EXEC_ALGO_R011_FAIL_EXECUTABLE_PROMOTION_BLOCKERS_WEAKENED" "Executable-promotion blockers are weakened."
}

if ($risk.RequiredScope -ne "R009DesignOnlyPreviewOnly" -or
    $risk.ApprovedForExecutableUse -or
    $risk.ApprovedForOrderCreation -or
    $risk.ApprovedForScheduleCreation -or
    $risk.ApprovedForChildSlices -or
    $risk.ApprovedForBrokerRouting -or
    $risk.ApprovedForSubmission -or
    $risk.ApprovedForFillOrExecutionReport -or
    $risk.ApprovedForPaperLedgerCommit -or
    $risk.ApprovedForStateMutation -or
    $risk.ApprovedForLiveTrading) {
    Fail "EXEC_ALGO_R011_FAIL_RISK_OPERATOR_SCOPE_WIDENED" "Risk/operator requirements authorize executable behavior."
}

if (-not $data.MoreDataRecommendedPreserved -or
    -not $data.CurrentCoreUsdPairUniverseRemainsPrimary -or
    -not $data.NonmajorEmScandiCnhDeferred) {
    Fail "EXEC_ALGO_R011_FAIL_DATA_EXPANSION_REQUIREMENTS" "Data expansion requirements are incomplete."
}

if (-not $instruments.USDPairOnlyAfterNetting -or
    -not $instruments.DirectCrossesSignalOnly -or
    -not $instruments.DirectCrossExecutionDisabled -or
    -not $instruments.USDCADRequiresInversion -or
    -not $instruments.USDCHFRequiresInversion -or
    -not $instruments.AUDUSDNotFailed -or
    -not $instruments.NonmajorEmScandiCnhDeferredUntilCalibration) {
    Fail "EXEC_ALGO_R011_FAIL_INSTRUMENT_CONSTRAINTS_WEAKENED" "Instrument constraints are weakened."
}

if ($instruments.USDJPY.NormalizedPortfolioSymbol -ne "JPYUSD" -or
    $instruments.USDJPY.ExecutionTradableSymbol -ne "USDJPY" -or
    -not $instruments.USDJPY.RequiresInversion -or
    $instruments.USDJPY.SecurityID -ne 4004 -or
    [string]$instruments.USDJPY.SecurityIDSource -ne "8") {
    Fail "EXEC_ALGO_R011_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat is missing or weakened."
}

if (-not $moreData.MoreDataRecommendedPreserved -or -not $moreData.ExecutablePromotionDiscussionStillBlocked) {
    Fail "EXEC_ALGO_R011_FAIL_MORE_DATA_PRESERVATION" "More-data recommendation is not preserved."
}
if ($noPromotion.ExecutablePromotionAuthorized -or $noPromotion.BrokerReady -or $noPromotion.LiveReady -or $noPromotion.AcceptanceTreatedAsExecutableApproval) {
    Fail "EXEC_ALGO_R011_FAIL_EXECUTABLE_PROMOTION_AUTHORIZED" "No-executable-promotion preservation is weakened."
}
if ($wakett.Wakett -ne "RejectedNegativeBaselineOnly" -or $wakett.RejectionWeakened -or $wakett.Promoted) {
    Fail "EXEC_ALGO_R011_FAIL_WAKETT_REJECTION_WEAKENED" "Wakett rejection is weakened."
}
if ($benchmarks.Promoted) {
    Fail "EXEC_ALGO_R011_FAIL_BENCHMARK_ONLY_PROMOTED" "Benchmark-only policies were promoted."
}
if ($safetyPolicies.ManualReview -ne "SafetyOnly" -or $safetyPolicies.DoNotTrade -ne "SafetyOnly" -or $safetyPolicies.Promoted) {
    Fail "EXEC_ALGO_R011_FAIL_SAFETY_POLICY_PROMOTED" "ManualReview/DoNotTrade preservation is weakened."
}
if (-not $canonical.FutureTimestampsUseCanonicalQuarterHour -or $canonical.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_ALGO_R011_FAIL_LEGACY_06_USED_AS_FUTURE_CANONICAL" "Canonical quarter-hour policy is weakened."
}
if (-not $legacy.LegacyTimestampsCompatibilityOnly -or $legacy.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_ALGO_R011_FAIL_LEGACY_06_USED_AS_FUTURE_CANONICAL" "Legacy compatibility policy is weakened."
}
if (-not $usdPair.USDPairOnlyAfterNetting -or -not $usdPair.AUDUSDNotFailed) {
    Fail "EXEC_ALGO_R011_FAIL_AUDUSD_MISCLASSIFIED" "USD-pair normalization or AUDUSD status is weakened."
}
if (-not $directCross.DirectCrossesSignalOnly -or -not $directCross.DirectCrossNettingFirst -or -not $directCross.DirectCrossExecutionDisabled -or $directCross.ExclusionWeakened) {
    Fail "EXEC_ALGO_R011_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct-cross exclusion is weakened."
}
if ($cost.FiveUsdPerMillionUniversalized -or $cost.FiveUsdPerMillion -ne "BestCaseMajorOnly" -or -not $cost.NonmajorCalibrationRequired) {
    Fail "EXEC_ALGO_R011_FAIL_COST_GUIDANCE_UNIVERSALIZED" "5 USD/million was universalized or nonmajor calibration weakened."
}
if (-not $nonmajor.NonmajorEmScandiCnhCalibrationRequired -or $nonmajor.NonmajorExecutionAuthorized) {
    Fail "EXEC_ALGO_R011_FAIL_NONMAJOR_CALIBRATION_WEAKENED" "Nonmajor calibration preservation is weakened."
}
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or -not $usdjpy.RequiresInversion -or $usdjpy.SecurityID -ne 4004 -or [string]$usdjpy.SecurityIDSource -ne "8" -or $usdjpy.CaveatWeakened) {
    Fail "EXEC_ALGO_R011_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat preservation is weakened."
}

foreach ($auditName in @(
    "phase-exec-algo-r011-non-executable-acceptance-audit.json",
    "phase-exec-algo-r011-no-new-backtest-audit.json",
    "phase-exec-algo-r011-no-new-simulation-audit.json",
    "phase-exec-algo-r011-no-tca-result-lines-audit.json",
    "phase-exec-algo-r011-no-executable-schedule-audit.json",
    "phase-exec-algo-r011-no-child-slices-audit.json",
    "phase-exec-algo-r011-no-child-orders-audit.json",
    "phase-exec-algo-r011-no-order-created-audit.json",
    "phase-exec-algo-r011-no-real-fill-audit.json",
    "phase-exec-algo-r011-no-execution-report-audit.json",
    "phase-exec-algo-r011-no-route-no-submission-audit.json",
    "phase-exec-algo-r011-no-paper-ledger-commit-audit.json",
    "phase-exec-algo-r011-no-polygon-api-call-audit.json",
    "phase-exec-algo-r011-no-lmax-call-audit.json",
    "phase-exec-algo-r011-no-external-api-call-audit.json",
    "phase-exec-algo-r011-no-broker-marketdata-runtime-audit.json"
)) {
    $audit = Read-Json (Join-Path $ArtifactsRoot $auditName)
    if (-not $audit.Passed -or $audit.Occurred) {
        Fail "EXEC_ALGO_R011_FAIL_FORBIDDEN_ACTION_DETECTED" "Audit failed: $auditName"
    }
}

if (-not $noExternal.NoExternal -or $noExternal.PolygonCalled -or $noExternal.LmaxCalled -or $noExternal.ExternalApiCalled -or $noExternal.DownloadsExecuted) {
    Fail "EXEC_ALGO_R011_FAIL_EXTERNAL_API_CALLED" "No-external audit failed."
}
if ($forbidden.ForbiddenActionsDetected -or
    $forbidden.BrokerActivation -or
    $forbidden.LiveMarketData -or
    $forbidden.SchedulerServicePolling -or
    $forbidden.NewPmsCycle -or
    $forbidden.BacktestOrSimulation -or
    $forbidden.TcaResultLinesCreated -or
    $forbidden.ExecutableSchedule -or
    $forbidden.ChildSlicesOrOrders -or
    $forbidden.OrdersFillsReportsRoutesSubmissions -or
    $forbidden.PaperLedgerCommit -or
    $forbidden.StateMutation -or
    $forbidden.R009ExecutablePromotion) {
    Fail "EXEC_ALGO_R011_FAIL_FORBIDDEN_ACTION_DETECTED" "Forbidden action audit failed."
}

if ($evidence.DotnetBuild -ne "Passed" -or
    $evidence.FocusedR011Tests -ne "Passed" -or
    $evidence.UnitTests -ne "Passed" -or
    $evidence.R011Validator -ne "Passed" -or
    -not $evidence.EvidenceComplete) {
    Fail "EXEC_ALGO_R011_FAIL_BUILD_TEST_VALIDATOR_EVIDENCE_MISSING" "Build/tests/validator evidence missing or incomplete."
}

Write-Host "EXEC-ALGO-R011 validation passed"
