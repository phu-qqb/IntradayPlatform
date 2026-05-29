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
        Fail "EXEC_SIM_R059_FAIL_MISSING_ARTIFACT" "Missing required artifact: $path"
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
    "phase-exec-sim-r059-summary.md",
    "phase-exec-sim-r059-r011-stability-reference.json",
    "phase-exec-sim-r059-r058-preview-review-reference.json",
    "phase-exec-sim-r059-r009-contract-reference.json",
    "phase-exec-sim-r059-next-stage-paper-only-expansion-contract.json",
    "phase-exec-sim-r059-coverage-gap-analysis.json",
    "phase-exec-sim-r059-target-close-distribution-plan.json",
    "phase-exec-sim-r059-bar-role-coverage-plan.json",
    "phase-exec-sim-r059-date-fixture-distribution-plan.json",
    "phase-exec-sim-r059-fixture-requirements.json",
    "phase-exec-sim-r059-batch-manifest-requirements.json",
    "phase-exec-sim-r059-manual-noexternal-command-planning-requirements.json",
    "phase-exec-sim-r059-readiness-binding-requirements.json",
    "phase-exec-sim-r059-risk-operator-approval-requirements.json",
    "phase-exec-sim-r059-aggregation-review-requirements.json",
    "phase-exec-sim-r059-success-criteria.json",
    "phase-exec-sim-r059-hold-criteria.json",
    "phase-exec-sim-r059-next-operator-action-package.json",
    "phase-exec-sim-r059-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-sim-r059-legacy-compatibility-preservation.json",
    "phase-exec-sim-r059-usd-pair-normalization-preservation.json",
    "phase-exec-sim-r059-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r059-cost-guidance-preservation.json",
    "phase-exec-sim-r059-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r059-no-broker-activation-audit.json",
    "phase-exec-sim-r059-no-live-marketdata-audit.json",
    "phase-exec-sim-r059-no-scheduler-service-polling-audit.json",
    "phase-exec-sim-r059-no-new-pms-cycle-audit.json",
    "phase-exec-sim-r059-no-new-backtest-audit.json",
    "phase-exec-sim-r059-no-new-simulation-audit.json",
    "phase-exec-sim-r059-no-tca-result-lines-audit.json",
    "phase-exec-sim-r059-no-executable-schedule-audit.json",
    "phase-exec-sim-r059-no-child-slices-audit.json",
    "phase-exec-sim-r059-no-child-orders-audit.json",
    "phase-exec-sim-r059-no-order-created-audit.json",
    "phase-exec-sim-r059-no-real-fill-audit.json",
    "phase-exec-sim-r059-no-execution-report-audit.json",
    "phase-exec-sim-r059-no-route-no-submission-audit.json",
    "phase-exec-sim-r059-no-paper-ledger-commit-audit.json",
    "phase-exec-sim-r059-no-polygon-api-call-audit.json",
    "phase-exec-sim-r059-no-lmax-call-audit.json",
    "phase-exec-sim-r059-no-external-api-call-audit.json",
    "phase-exec-sim-r059-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r059-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r059-no-external-audit.json",
    "phase-exec-sim-r059-forbidden-actions-audit.json",
    "phase-exec-sim-r059-next-phase-recommendation.json",
    "phase-exec-sim-r059-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsRoot $artifact))) {
        Fail "EXEC_SIM_R059_FAIL_MISSING_ARTIFACT" "Missing required artifact: $artifact"
    }
}

$r011 = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r059-r011-stability-reference.json")
$r058 = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r059-r058-preview-review-reference.json")
$contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r059-r009-contract-reference.json")
$planningContract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r059-next-stage-paper-only-expansion-contract.json")
$gap = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r059-coverage-gap-analysis.json")
$targetPlan = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r059-target-close-distribution-plan.json")
$barRolePlan = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r059-bar-role-coverage-plan.json")
$dateFixture = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r059-date-fixture-distribution-plan.json")
$fixtureReq = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r059-fixture-requirements.json")
$manifestReq = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r059-batch-manifest-requirements.json")
$commandReq = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r059-manual-noexternal-command-planning-requirements.json")
$readinessReq = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r059-readiness-binding-requirements.json")
$riskReq = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r059-risk-operator-approval-requirements.json")
$aggregationReq = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r059-aggregation-review-requirements.json")
$success = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r059-success-criteria.json")
$hold = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r059-hold-criteria.json")
$operatorAction = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r059-next-operator-action-package.json")
$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r059-canonical-quarter-hour-policy-preservation.json")
$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r059-legacy-compatibility-preservation.json")
$usdPair = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r059-usd-pair-normalization-preservation.json")
$directCross = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r059-direct-cross-exclusion-preservation.json")
$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r059-cost-guidance-preservation.json")
$nonmajor = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r059-nonmajor-calibration-preservation.json")
$usdjpy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r059-usdjpy-caveat-preservation.json")
$noExternal = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r059-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r059-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r059-build-test-validator-evidence.json")

if ($r011.R009Status -ne "StableForBroaderPaperOnlyEvaluation" -or
    -not $r011.AcceptedForNextStagePaperOnlyExpansion -or
    $r011.ExecutablePromotionAuthorized -or
    $r011.BrokerReady -or
    $r011.LiveReady -or
    -not $r011.ReusedOnly) {
    Fail "EXEC_SIM_R059_FAIL_R011_REFERENCE_INVALID" "R011 stability reference is missing or executable."
}

if ($r058.ReviewedRuns -ne 20 -or
    $r058.ReviewedPreviewLines -ne 140 -or
    -not $r058.ReadinessBindingsComplete -or
    $r058.HeldLines -ne 0 -or
    $r058.OrderLikeOutputsDetected -ne 0 -or
    -not $r058.StableEnoughForFurtherPaperOnlyEvaluationExpansion -or
    -not $r058.ReusedOnly) {
    Fail "EXEC_SIM_R059_FAIL_R058_REFERENCE_INVALID" "R058 preview review reference is incomplete."
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
    Fail "EXEC_SIM_R059_FAIL_R009_PROMOTED_TO_EXECUTABLE" "R009 contract reference is missing or executable."
}

if (-not $planningContract.PlanningOnly -or
    $planningContract.CommandsExecuted -or
    $planningContract.ManualNoExternalCommandsRun -or
    $planningContract.PmsCyclesRun -or
    $planningContract.BacktestOrSimulationRun -or
    $planningContract.TcaResultLinesCreated -or
    -not $planningContract.DesignOnly -or
    -not $planningContract.PaperOnly -or
    -not $planningContract.NonExecutable -or
    $planningContract.BrokerReady -or
    $planningContract.LiveReady -or
    $planningContract.ExecutablePromotionAuthorized) {
    Fail "EXEC_SIM_R059_FAIL_PLANNING_CONTRACT_WEAKENED" "R059 planning contract permits execution or mutation."
}
foreach ($expectedClass in @(
    "EXEC_SIM_R059_PASS_NEXT_STAGE_PAPER_ONLY_EXPANSION_PLAN_READY_NO_EXTERNAL",
    "EXEC_SIM_R059_PASS_BAR_ROLE_COVERAGE_PLAN_READY_NO_EXTERNAL",
    "EXEC_SIM_R059_PASS_OPERATOR_PACKAGE_READY_NO_EXTERNAL",
    "EXEC_SIM_R059_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
)) {
    if ((As-Array $planningContract.Classifications) -notcontains $expectedClass) {
        Fail "EXEC_SIM_R059_FAIL_CLASSIFICATION_MISSING" "Missing classification: $expectedClass"
    }
}

if ($gap.PriorOpeningBuildLines -ne 0 -or
    $gap.PriorIntradayRebalanceLines -ne 35 -or
    $gap.PriorClosingFlattenLines -ne 105) {
    Fail "EXEC_SIM_R059_FAIL_COVERAGE_GAP_ANALYSIS" "Coverage gap analysis does not match R058 bar-role coverage."
}
foreach ($gapText in @("OpeningBuild coverage missing", "IntradayRebalance coverage limited", "ClosingFlatten coverage heavier than other roles")) {
    if (((As-Array $gap.Gaps) -join "`n") -notlike "*$gapText*") {
        Fail "EXEC_SIM_R059_FAIL_COVERAGE_GAP_ANALYSIS" "Missing coverage gap: $gapText"
    }
}

if ($targetPlan.RecommendedMinimumTargetCloses -lt 30 -or
    $targetPlan.Legacy06UsedAsFutureCanonical -or
    -not $targetPlan.CandidateDefinitionNeedsOperatorConfirmation) {
    Fail "EXEC_SIM_R059_FAIL_TARGET_CLOSE_PLAN" "Target-close plan is incomplete or uses legacy timestamps."
}
$targetDistribution = As-Array $targetPlan.Distribution
foreach ($role in @("OpeningBuild", "IntradayRebalance", "ClosingFlatten")) {
    $entry = $targetDistribution | Where-Object { $_.BarRole -eq $role } | Select-Object -First 1
    if ($null -eq $entry -or $entry.MinimumTargetCloses -lt 10) {
        Fail "EXEC_SIM_R059_FAIL_BAR_ROLE_COVERAGE_PLAN" "Target distribution missing at least 10 closes for $role."
    }
}

$nextCoverage = As-Array $barRolePlan.NextCoverageTarget
foreach ($role in @("OpeningBuild", "IntradayRebalance", "ClosingFlatten")) {
    $entry = $nextCoverage | Where-Object { $_.BarRole -eq $role } | Select-Object -First 1
    if ($null -eq $entry -or $entry.MinimumTargetCloses -lt 10) {
        Fail "EXEC_SIM_R059_FAIL_BAR_ROLE_COVERAGE_PLAN" "Bar-role coverage plan missing at least 10 closes for $role."
    }
}
if (-not $barRolePlan.CandidateDefinitionNeedsOperatorConfirmation) {
    Fail "EXEC_SIM_R059_FAIL_BAR_ROLE_COVERAGE_PLAN" "Bar-role candidate definitions should require operator confirmation."
}

if ($dateFixture.RecommendedMinimumFixtures -lt 30 -or
    $dateFixture.RecommendedMinimumDates -lt 30 -or
    -not $dateFixture.LegacyAggregatedWeightsMayBeUsed -or
    -not $dateFixture.OperatorSuppliedFixturesAllowed) {
    Fail "EXEC_SIM_R059_FAIL_DATE_FIXTURE_PLAN" "Date/fixture distribution plan is incomplete."
}

if ($fixtureReq.RequiredFormat -ne "<BloombergTicker>;<weight>" -or
    $fixtureReq.TimestampsInsideFixtureRowsAllowed -or
    -not $fixtureReq.CanonicalTargetCloseSuppliedSeparatelyInManifest -or
    -not $fixtureReq.DirectCrossesAllowedAsSignalsOnly -or
    -not $fixtureReq.NettingRequiredBeforeUsdPairPaperPreview -or
    -not $fixtureReq.LegacyAggregatedWeightsExtractionAllowedWithCompatibilityMappingOnly -or
    -not $fixtureReq.OperatorSuppliedQubesFixturesAllowed) {
    Fail "EXEC_SIM_R059_FAIL_FIXTURE_REQUIREMENTS" "Fixture requirements are incomplete."
}

$manifestFields = (As-Array $manifestReq.RequiredFields) -join "`n"
foreach ($field in @(
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
    "LegacyCompatibilityMappingUsed",
    "ReadinessBindingRequired=true",
    "RiskOperatorApprovalScope=DesignOnlyPreviewOnly"
)) {
    if ($manifestFields -notlike "*$field*") {
        Fail "EXEC_SIM_R059_FAIL_MANIFEST_REQUIREMENTS" "Missing manifest required field: $field"
    }
}
if ($manifestReq.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_SIM_R059_FAIL_LEGACY_06_USED_AS_FUTURE_CANONICAL" "Manifest requirements allow legacy :06 future canonical timestamps."
}

if (-not $commandReq.CommandGenerationOnly -or
    $commandReq.CommandsExecuted -or
    -not $commandReq.SafetyValidationRequiredBeforeAnyFutureRun -or
    -not $commandReq.NoBrokerLiveOrderRouteSubmissionFlagsAllowed) {
    Fail "EXEC_SIM_R059_FAIL_COMMAND_PLANNING_REQUIREMENTS" "ManualNoExternal command planning requirements are unsafe."
}
$requiredFlags = (As-Array $commandReq.RequiredFlags) -join "`n"
foreach ($flag in @("--mode ManualNoExternal", "--output-artifacts-dir", "--requested-cycle-run-id", "--qubes-run-id", "--qubes-fixture-path", "--cadence-minutes 15", "--no-paper-ledger-commit true")) {
    if ($requiredFlags -notlike "*$flag*") {
        Fail "EXEC_SIM_R059_FAIL_COMMAND_PLANNING_REQUIREMENTS" "Missing command planning flag: $flag"
    }
}

if (-not $readinessReq.RequiredForEveryPreviewLine -or -not $readinessReq.MissingBindingHoldRequired) {
    Fail "EXEC_SIM_R059_FAIL_READINESS_REQUIREMENTS" "Readiness requirements do not hold missing bindings."
}
if (-not $riskReq.PreviewOnlyRiskApprovalRequired -or
    -not $riskReq.PreviewOnlyOperatorApprovalRequired -or
    $riskReq.RequiredScope -ne "DesignOnlyPreviewOnly" -or
    $riskReq.ApprovedForExecutableUse -or
    $riskReq.ApprovedForOrderCreation -or
    $riskReq.ApprovedForBrokerRouting -or
    $riskReq.ApprovedForSubmission -or
    $riskReq.ApprovedForPaperLedgerCommit -or
    $riskReq.ApprovedForStateMutation) {
    Fail "EXEC_SIM_R059_FAIL_RISK_OPERATOR_REQUIREMENTS" "Risk/operator requirements authorize executable behavior."
}

if ($aggregationReq.ExpectedMaxPreviewLinesForThirtyCloses -ne 210 -or
    $aggregationReq.ExecutablePromotionAuthorized) {
    Fail "EXEC_SIM_R059_FAIL_AGGREGATION_REVIEW_REQUIREMENTS" "Aggregation/review requirements are incomplete or executable."
}

$successText = (As-Array $success.Criteria) -join "`n"
foreach ($criterion in @(
    "All commands pass safety validation",
    "All ManualNoExternal runs are local/no-external/no-ledger-commit",
    "All preview lines are NonExecutable/NotAnOrder/NoBrokerRoute",
    "No direct-cross executable lines",
    "Readiness bindings complete",
    "Risk/operator approvals are preview-only",
    "R009 remains stable across bar roles",
    "No schedule/order/fill/route/submission/ledger/state mutation"
)) {
    if ($successText -notlike "*$criterion*") {
        Fail "EXEC_SIM_R059_FAIL_SUCCESS_CRITERIA" "Missing success criterion: $criterion"
    }
}

$holdText = (As-Array $hold.Criteria) -join "`n"
foreach ($criterion in @(
    "Missing fixtures",
    "Missing canonical target closes",
    "Missing readiness bindings",
    "Missing risk/operator preview approval",
    "Direct cross emitted as executable line",
    "Unsupported instrument after netting",
    "Nonmajor/EM/scandi/CNH without calibration",
    "Legacy :06 used as future canonical",
    "Any executable path appears"
)) {
    if ($holdText -notlike "*$criterion*") {
        Fail "EXEC_SIM_R059_FAIL_HOLD_CRITERIA" "Missing hold criterion: $criterion"
    }
}

if ($operatorAction.MinimumTargetCloses -lt 30 -or
    -not $operatorAction.ManualNoExternalCommandsMustNotBeRunInR059 -or
    (As-Array $operatorAction.CommandsToRunNow).Count -ne 0) {
    Fail "EXEC_SIM_R059_FAIL_OPERATOR_PACKAGE" "Operator package is missing or tries to run commands."
}

if (-not $canonical.FutureTimestampsUseCanonicalQuarterHour -or $canonical.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_SIM_R059_FAIL_LEGACY_06_USED_AS_FUTURE_CANONICAL" "Canonical quarter-hour policy is weakened."
}
if (-not $legacy.LegacyTimestampsCompatibilityOnly -or $legacy.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_SIM_R059_FAIL_LEGACY_06_USED_AS_FUTURE_CANONICAL" "Legacy compatibility preservation is weakened."
}
if (-not $usdPair.USDPairOnlyAfterNetting -or -not $usdPair.AUDUSDNotFailed) {
    Fail "EXEC_SIM_R059_FAIL_AUDUSD_MISCLASSIFIED" "USD-pair normalization or AUDUSD status is weakened."
}
if (-not $directCross.DirectCrossesSignalOnly -or -not $directCross.DirectCrossNettingFirst -or -not $directCross.DirectCrossExecutionDisabled -or $directCross.ExclusionWeakened) {
    Fail "EXEC_SIM_R059_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct-cross exclusion is weakened."
}
if ($cost.FiveUsdPerMillionUniversalized -or $cost.FiveUsdPerMillion -ne "BestCaseMajorOnly" -or -not $cost.NonmajorCalibrationRequired) {
    Fail "EXEC_SIM_R059_FAIL_COST_GUIDANCE_UNIVERSALIZED" "5 USD/million was universalized or nonmajor calibration weakened."
}
if (-not $nonmajor.NonmajorEmScandiCnhCalibrationRequired -or $nonmajor.NonmajorExecutionAuthorized) {
    Fail "EXEC_SIM_R059_FAIL_NONMAJOR_CALIBRATION_WEAKENED" "Nonmajor calibration preservation is weakened."
}
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or
    $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or
    -not $usdjpy.RequiresInversion -or
    $usdjpy.SecurityID -ne 4004 -or
    [string]$usdjpy.SecurityIDSource -ne "8" -or
    $usdjpy.CaveatWeakened) {
    Fail "EXEC_SIM_R059_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat preservation is weakened."
}

foreach ($auditName in @(
    "phase-exec-sim-r059-no-broker-activation-audit.json",
    "phase-exec-sim-r059-no-live-marketdata-audit.json",
    "phase-exec-sim-r059-no-scheduler-service-polling-audit.json",
    "phase-exec-sim-r059-no-new-pms-cycle-audit.json",
    "phase-exec-sim-r059-no-new-backtest-audit.json",
    "phase-exec-sim-r059-no-new-simulation-audit.json",
    "phase-exec-sim-r059-no-tca-result-lines-audit.json",
    "phase-exec-sim-r059-no-executable-schedule-audit.json",
    "phase-exec-sim-r059-no-child-slices-audit.json",
    "phase-exec-sim-r059-no-child-orders-audit.json",
    "phase-exec-sim-r059-no-order-created-audit.json",
    "phase-exec-sim-r059-no-real-fill-audit.json",
    "phase-exec-sim-r059-no-execution-report-audit.json",
    "phase-exec-sim-r059-no-route-no-submission-audit.json",
    "phase-exec-sim-r059-no-paper-ledger-commit-audit.json",
    "phase-exec-sim-r059-no-polygon-api-call-audit.json",
    "phase-exec-sim-r059-no-lmax-call-audit.json",
    "phase-exec-sim-r059-no-external-api-call-audit.json"
)) {
    $audit = Read-Json (Join-Path $ArtifactsRoot $auditName)
    if (-not $audit.Passed -or $audit.Occurred) {
        Fail "EXEC_SIM_R059_FAIL_FORBIDDEN_ACTION_DETECTED" "Audit failed: $auditName"
    }
}

if (-not $noExternal.NoExternal -or $noExternal.PolygonCalled -or $noExternal.LmaxCalled -or $noExternal.ExternalApiCalled -or $noExternal.DownloadsExecuted) {
    Fail "EXEC_SIM_R059_FAIL_EXTERNAL_API_CALLED" "No-external audit failed."
}
if ($forbidden.ForbiddenActionsDetected -or
    $forbidden.BrokerActivation -or
    $forbidden.LiveMarketData -or
    $forbidden.SchedulerServicePolling -or
    $forbidden.NewPmsCycle -or
    $forbidden.ManualNoExternalCommandsRun -or
    $forbidden.BacktestOrSimulation -or
    $forbidden.TcaResultLinesCreated -or
    $forbidden.ExecutableSchedule -or
    $forbidden.ChildSlicesOrOrders -or
    $forbidden.OrdersFillsReportsRoutesSubmissions -or
    $forbidden.PaperLedgerCommit -or
    $forbidden.StateMutation -or
    $forbidden.R009ExecutablePromotion) {
    Fail "EXEC_SIM_R059_FAIL_FORBIDDEN_ACTION_DETECTED" "Forbidden action audit failed."
}

if ($evidence.DotnetBuild -ne "Passed" -or
    $evidence.FocusedR059Tests -ne "Passed" -or
    $evidence.UnitTests -ne "Passed" -or
    $evidence.R059Validator -ne "Passed" -or
    -not $evidence.EvidenceComplete) {
    Fail "EXEC_SIM_R059_FAIL_BUILD_TEST_VALIDATOR_EVIDENCE_MISSING" "Build/tests/validator evidence missing or incomplete."
}

Write-Host "EXEC-SIM-R059 validation passed"
