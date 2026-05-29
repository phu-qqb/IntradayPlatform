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
        Fail "EXEC_SIM_R060_FAIL_MISSING_ARTIFACT" "Missing required artifact: $path"
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
    "phase-exec-sim-r060-summary.md",
    "phase-exec-sim-r060-r012-maturity-reference.json",
    "phase-exec-sim-r060-long-run-paper-batch-planning-contract.json",
    "phase-exec-sim-r060-long-run-batch-packaging-requirements.json",
    "phase-exec-sim-r060-operator-run-package-requirements.json",
    "phase-exec-sim-r060-automation-safety-constraints.json",
    "phase-exec-sim-r060-batch-manifest-requirements.json",
    "phase-exec-sim-r060-fixture-requirements.json",
    "phase-exec-sim-r060-target-close-requirements.json",
    "phase-exec-sim-r060-readiness-binding-requirements.json",
    "phase-exec-sim-r060-risk-operator-approval-requirements.json",
    "phase-exec-sim-r060-reporting-monitoring-requirements.json",
    "phase-exec-sim-r060-aggregation-requirements.json",
    "phase-exec-sim-r060-stop-hold-criteria.json",
    "phase-exec-sim-r060-executable-promotion-blockers.json",
    "phase-exec-sim-r060-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-sim-r060-legacy-compatibility-preservation.json",
    "phase-exec-sim-r060-usd-pair-normalization-preservation.json",
    "phase-exec-sim-r060-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r060-cost-guidance-preservation.json",
    "phase-exec-sim-r060-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r060-no-broker-activation-audit.json",
    "phase-exec-sim-r060-no-live-marketdata-audit.json",
    "phase-exec-sim-r060-no-scheduler-service-polling-audit.json",
    "phase-exec-sim-r060-no-new-pms-cycle-audit.json",
    "phase-exec-sim-r060-no-manualnoexternal-command-run-audit.json",
    "phase-exec-sim-r060-no-new-backtest-audit.json",
    "phase-exec-sim-r060-no-new-simulation-audit.json",
    "phase-exec-sim-r060-no-tca-result-lines-audit.json",
    "phase-exec-sim-r060-no-executable-schedule-audit.json",
    "phase-exec-sim-r060-no-child-slices-audit.json",
    "phase-exec-sim-r060-no-child-orders-audit.json",
    "phase-exec-sim-r060-no-order-created-audit.json",
    "phase-exec-sim-r060-no-real-fill-audit.json",
    "phase-exec-sim-r060-no-execution-report-audit.json",
    "phase-exec-sim-r060-no-route-no-submission-audit.json",
    "phase-exec-sim-r060-no-paper-ledger-commit-audit.json",
    "phase-exec-sim-r060-no-polygon-api-call-audit.json",
    "phase-exec-sim-r060-no-lmax-call-audit.json",
    "phase-exec-sim-r060-no-external-api-call-audit.json",
    "phase-exec-sim-r060-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r060-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r060-no-external-audit.json",
    "phase-exec-sim-r060-forbidden-actions-audit.json",
    "phase-exec-sim-r060-next-phase-recommendation.json",
    "phase-exec-sim-r060-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsRoot $artifact))) {
        Fail "EXEC_SIM_R060_FAIL_MISSING_ARTIFACT" "Missing required artifact: $artifact"
    }
}

$maturity = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r060-r012-maturity-reference.json")
$contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r060-long-run-paper-batch-planning-contract.json")
$packaging = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r060-long-run-batch-packaging-requirements.json")
$operatorPackage = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r060-operator-run-package-requirements.json")
$automation = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r060-automation-safety-constraints.json")
$manifest = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r060-batch-manifest-requirements.json")
$fixture = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r060-fixture-requirements.json")
$targetClose = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r060-target-close-requirements.json")
$readiness = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r060-readiness-binding-requirements.json")
$risk = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r060-risk-operator-approval-requirements.json")
$reporting = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r060-reporting-monitoring-requirements.json")
$aggregation = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r060-aggregation-requirements.json")
$hold = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r060-stop-hold-criteria.json")
$blockers = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r060-executable-promotion-blockers.json")
$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r060-canonical-quarter-hour-policy-preservation.json")
$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r060-legacy-compatibility-preservation.json")
$usdPair = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r060-usd-pair-normalization-preservation.json")
$directCross = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r060-direct-cross-exclusion-preservation.json")
$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r060-cost-guidance-preservation.json")
$nonmajor = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r060-nonmajor-calibration-preservation.json")
$usdjpy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r060-usdjpy-caveat-preservation.json")
$noExternal = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r060-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r060-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r060-build-test-validator-evidence.json")

if ($maturity.R009MaturityStatus -ne "StableForLongRunPaperOnlyExpansion" -or
    -not $maturity.AcceptedForLongRunPaperOnlyPlanning -or
    -not $maturity.MoreLongRunPaperOnlyDataRecommended -or
    -not $maturity.DesignOnly -or
    -not $maturity.PaperOnly -or
    -not $maturity.NonExecutable -or
    -not $maturity.NotAnOrder -or
    -not $maturity.NotSubmitted -or
    -not $maturity.NoBrokerRoute -or
    $maturity.ExecutablePromotionAuthorized -or
    $maturity.BrokerReady -or
    $maturity.LiveReady -or
    -not $maturity.ReusedOnly) {
    Fail "EXEC_SIM_R060_FAIL_R012_MATURITY_REFERENCE_INVALID" "R012 maturity reference is missing or executable."
}

if (-not $contract.PlanningAndSafetyOnly -or
    $contract.CommandsExecuted -or
    $contract.ManualNoExternalCommandsRun -or
    $contract.PmsCyclesRun -or
    $contract.BacktestOrSimulationRun -or
    $contract.TcaResultLinesCreated -or
    $contract.SchedulerServicePollingIntroduced -or
    $contract.AutomaticExecutionEnabled -or
    $contract.BrokerRuntimeActivated -or
    $contract.LiveMarketDataRequested -or
    -not $contract.R009DesignOnly -or
    -not $contract.R009PaperOnly -or
    -not $contract.R009NonExecutable -or
    $contract.ExecutablePromotionAuthorized) {
    Fail "EXEC_SIM_R060_FAIL_PLANNING_CONTRACT_WEAKENED" "Planning contract permits execution, automation, or runtime paths."
}

foreach ($expectedClass in @(
    "EXEC_SIM_R060_PASS_LONG_RUN_PAPER_BATCH_PLAN_READY_NO_EXTERNAL",
    "EXEC_SIM_R060_PASS_AUTOMATION_SAFETY_CONSTRAINTS_READY_NO_EXTERNAL",
    "EXEC_SIM_R060_PASS_OPERATOR_RUN_PACKAGE_REQUIREMENTS_READY_NO_EXTERNAL",
    "EXEC_SIM_R060_PASS_NO_AUTOMATION_NO_ORDER_GATE_READY_NO_EXTERNAL"
)) {
    if ((As-Array $contract.Classifications) -notcontains $expectedClass) {
        Fail "EXEC_SIM_R060_FAIL_CLASSIFICATION_MISSING" "Missing classification: $expectedClass"
    }
}

if ($packaging.MinimumTargetCloses -lt 100 -or
    $packaging.MinimumOpeningBuildCloses -lt 30 -or
    $packaging.MinimumIntradayRebalanceCloses -lt 30 -or
    $packaging.MinimumClosingFlattenCloses -lt 30 -or
    -not $packaging.CompleteReadinessBindingsRequired -or
    -not $packaging.NoDirectCrossExecution -or
    -not $packaging.NoOrdersFillsRoutesSubmissions -or
    -not $packaging.NoPaperLedgerCommit) {
    Fail "EXEC_SIM_R060_FAIL_BATCH_PACKAGING_REQUIREMENTS" "Long-run batch packaging requirements are incomplete."
}

$requiredBatchFields = (As-Array $packaging.RequiredBatchEntryFields) -join "`n"
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
    "ReadinessBindingRequired=true",
    "RiskOperatorApprovalScope=DesignOnlyPreviewOnly"
)) {
    if ($requiredBatchFields -notlike "*$field*") {
        Fail "EXEC_SIM_R060_FAIL_BATCH_PACKAGING_REQUIREMENTS" "Missing batch entry field: $field"
    }
}

if (-not $operatorPackage.CommandsTextOnly -or
    -not $operatorPackage.CommandsMustUseManualNoExternal -or
    -not $operatorPackage.CommandsMustIncludeNoPaperLedgerCommitTrue -or
    -not $operatorPackage.CommandsWriteOnlyAllowedArtifacts -or
    -not $operatorPackage.CommandsRunManuallyByOperatorOnly -or
    $operatorPackage.SchedulerAllowed -or
    $operatorPackage.ServiceAllowed -or
    $operatorPackage.PollingAllowed -or
    $operatorPackage.AutomaticBatchRunnerAllowed -or
    $operatorPackage.BrokerRuntimeAllowed -or
    $operatorPackage.LiveMarketDataAllowed -or
    $operatorPackage.OrderCreationAllowed -or
    $operatorPackage.FillCreationAllowed -or
    $operatorPackage.RouteSubmissionAllowed -or
    $operatorPackage.PaperLedgerCommitAllowed) {
    Fail "EXEC_SIM_R060_FAIL_OPERATOR_PACKAGE_REQUIREMENTS" "Operator package requirements are unsafe."
}

$requiredFlags = (As-Array $operatorPackage.RequiredCommandFlags) -join "`n"
foreach ($flag in @("--mode ManualNoExternal", "--output-artifacts-dir", "--requested-cycle-run-id", "--qubes-run-id", "--qubes-fixture-path", "--cadence-minutes 15", "--no-paper-ledger-commit true")) {
    if ($requiredFlags -notlike "*$flag*") {
        Fail "EXEC_SIM_R060_FAIL_OPERATOR_PACKAGE_REQUIREMENTS" "Missing command flag: $flag"
    }
}

if (-not $automation.ManualOnly -or
    $automation.SchedulerAllowed -or
    $automation.ServiceAllowed -or
    $automation.PollingAllowed -or
    $automation.TimerAllowed -or
    $automation.BackgroundJobAllowed -or
    $automation.AutomaticExecutionAllowed -or
    $automation.BrokerRuntimeAllowed -or
    $automation.LiveMarketDataAllowed -or
    $automation.PaperLedgerCommitAllowed -or
    $automation.StateMutationAllowed -or
    $automation.ExecutableScheduleAllowed -or
    $automation.ChildSlicesAllowed -or
    $automation.ChildOrdersAllowed -or
    $automation.OrderCreationAllowed -or
    $automation.RouteSubmissionAllowed -or
    $automation.FillReportAllowed -or
    $automation.ManualNoExternalCommandRunAllowedInR060) {
    Fail "EXEC_SIM_R060_FAIL_AUTOMATION_SAFETY_WEAKENED" "Automation safety constraints are missing or weakened."
}

if ($manifest.MinimumEntries -lt 100 -or
    $manifest.OpeningBuildEntriesMinimum -lt 30 -or
    $manifest.IntradayRebalanceEntriesMinimum -lt 30 -or
    $manifest.ClosingFlattenEntriesMinimum -lt 30 -or
    $manifest.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_SIM_R060_FAIL_MANIFEST_REQUIREMENTS" "Manifest requirements are incomplete or use legacy timestamps."
}

if ($fixture.RequiredFormat -ne "<BloombergTicker>;<weight>" -or
    -not $fixture.NonEmpty -or
    -not $fixture.WeightMustParseDecimal -or
    $fixture.TimestampsInsideFixtureRowsAllowed -or
    -not $fixture.DirectCrossesAllowedAsSignalsOnly -or
    -not $fixture.NettingRequiredBeforeUsdPairPreview) {
    Fail "EXEC_SIM_R060_FAIL_FIXTURE_REQUIREMENTS" "Fixture requirements are incomplete."
}

if (-not $targetClose.CanonicalQuarterHourRequired -or
    $targetClose.Legacy06UsedAsFutureCanonical -or
    -not $targetClose.TargetCloseSuppliedInManifest) {
    Fail "EXEC_SIM_R060_FAIL_LEGACY_06_USED_AS_FUTURE_CANONICAL" "Target-close requirements are incomplete or legacy-based."
}

if (-not $readiness.RequiredForEveryPreviewLine -or
    -not $readiness.MissingBindingHoldRequired -or
    -not $readiness.CompleteReadinessBindingsRequired) {
    Fail "EXEC_SIM_R060_FAIL_READINESS_REQUIREMENTS" "Readiness requirements are incomplete."
}

if (-not $risk.RiskApprovalRequired -or
    -not $risk.OperatorApprovalRequired -or
    $risk.RequiredScope -ne "DesignOnlyPreviewOnly" -or
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
    Fail "EXEC_SIM_R060_FAIL_RISK_OPERATOR_SCOPE_WEAKENED" "Risk/operator requirements authorize execution."
}

if (-not $reporting.ReportingOnly -or
    $reporting.LiveMonitoringAllowed -or
    $reporting.SchedulerServicePollingAllowed) {
    Fail "EXEC_SIM_R060_FAIL_REPORTING_REQUIREMENTS" "Reporting requirements allow live monitoring or automation."
}

if ($aggregation.AcceptanceScope -ne "LongRunPaperOnlyEvaluation" -or
    $aggregation.ExecutablePromotionAuthorized) {
    Fail "EXEC_SIM_R060_FAIL_AGGREGATION_REQUIREMENTS" "Aggregation requirements are executable or wrong scope."
}

$holdText = (As-Array $hold.Criteria) -join "`n"
foreach ($criterion in @(
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
)) {
    if ($holdText -notlike "*$criterion*") {
        Fail "EXEC_SIM_R060_FAIL_STOP_HOLD_CRITERIA" "Missing stop/hold criterion: $criterion"
    }
}

if (-not $blockers.ExecutablePromotionBlocked -or
    $blockers.AcceptanceIsExecutableApproval -or
    $blockers.ExecutablePromotionAuthorized) {
    Fail "EXEC_SIM_R060_FAIL_EXECUTABLE_PROMOTION_BLOCKERS" "Executable blockers are missing or weakened."
}

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
    if (((As-Array $blockers.Blockers) -join "`n") -notlike "*$blocker*") {
        Fail "EXEC_SIM_R060_FAIL_EXECUTABLE_PROMOTION_BLOCKERS" "Missing blocker: $blocker"
    }
}

if (-not $canonical.FutureTimestampsUseCanonicalQuarterHour -or $canonical.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_SIM_R060_FAIL_LEGACY_06_USED_AS_FUTURE_CANONICAL" "Canonical quarter-hour policy is weakened."
}
if (-not $legacy.LegacyTimestampsCompatibilityOnly -or $legacy.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_SIM_R060_FAIL_LEGACY_06_USED_AS_FUTURE_CANONICAL" "Legacy compatibility preservation is weakened."
}
if (-not $usdPair.USDPairOnlyAfterNetting -or -not $usdPair.AUDUSDNotFailed) {
    Fail "EXEC_SIM_R060_FAIL_AUDUSD_MISCLASSIFIED" "USD-pair normalization or AUDUSD status is weakened."
}
if (-not $directCross.DirectCrossesSignalOnly -or -not $directCross.DirectCrossNettingFirst -or -not $directCross.DirectCrossExecutionDisabled -or $directCross.ExclusionWeakened) {
    Fail "EXEC_SIM_R060_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct-cross exclusion is weakened."
}
if ($cost.FiveUsdPerMillionUniversalized -or $cost.FiveUsdPerMillion -ne "BestCaseMajorOnly" -or -not $cost.NonmajorCalibrationRequired) {
    Fail "EXEC_SIM_R060_FAIL_COST_GUIDANCE_UNIVERSALIZED" "5 USD/million was universalized or nonmajor calibration weakened."
}
if (-not $nonmajor.NonmajorEmScandiCnhCalibrationRequired -or $nonmajor.NonmajorExecutionAuthorized) {
    Fail "EXEC_SIM_R060_FAIL_NONMAJOR_CALIBRATION_WEAKENED" "Nonmajor calibration preservation is weakened."
}
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or
    $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or
    -not $usdjpy.RequiresInversion -or
    $usdjpy.SecurityID -ne 4004 -or
    [string]$usdjpy.SecurityIDSource -ne "8" -or
    $usdjpy.CaveatWeakened) {
    Fail "EXEC_SIM_R060_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat preservation is weakened."
}

foreach ($auditName in @(
    "phase-exec-sim-r060-no-broker-activation-audit.json",
    "phase-exec-sim-r060-no-live-marketdata-audit.json",
    "phase-exec-sim-r060-no-scheduler-service-polling-audit.json",
    "phase-exec-sim-r060-no-new-pms-cycle-audit.json",
    "phase-exec-sim-r060-no-manualnoexternal-command-run-audit.json",
    "phase-exec-sim-r060-no-new-backtest-audit.json",
    "phase-exec-sim-r060-no-new-simulation-audit.json",
    "phase-exec-sim-r060-no-tca-result-lines-audit.json",
    "phase-exec-sim-r060-no-executable-schedule-audit.json",
    "phase-exec-sim-r060-no-child-slices-audit.json",
    "phase-exec-sim-r060-no-child-orders-audit.json",
    "phase-exec-sim-r060-no-order-created-audit.json",
    "phase-exec-sim-r060-no-real-fill-audit.json",
    "phase-exec-sim-r060-no-execution-report-audit.json",
    "phase-exec-sim-r060-no-route-no-submission-audit.json",
    "phase-exec-sim-r060-no-paper-ledger-commit-audit.json",
    "phase-exec-sim-r060-no-polygon-api-call-audit.json",
    "phase-exec-sim-r060-no-lmax-call-audit.json",
    "phase-exec-sim-r060-no-external-api-call-audit.json"
)) {
    $audit = Read-Json (Join-Path $ArtifactsRoot $auditName)
    if (-not $audit.Passed -or $audit.Occurred) {
        Fail "EXEC_SIM_R060_FAIL_FORBIDDEN_ACTION_DETECTED" "Audit failed: $auditName"
    }
}

if (-not $noExternal.NoExternal -or $noExternal.PolygonCalled -or $noExternal.LmaxCalled -or $noExternal.ExternalApiCalled -or $noExternal.DownloadsExecuted) {
    Fail "EXEC_SIM_R060_FAIL_EXTERNAL_API_CALLED" "No-external audit failed."
}
if ($forbidden.ForbiddenActionsDetected -or
    $forbidden.BrokerActivation -or
    $forbidden.LiveMarketData -or
    $forbidden.SchedulerServicePolling -or
    $forbidden.BackgroundJobs -or
    $forbidden.AutomaticExecution -or
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
    Fail "EXEC_SIM_R060_FAIL_FORBIDDEN_ACTION_DETECTED" "Forbidden action audit failed."
}

if ($evidence.DotnetBuild -ne "Passed" -or
    $evidence.FocusedR060Tests -ne "Passed" -or
    $evidence.UnitTests -ne "Passed" -or
    $evidence.R060Validator -ne "Passed" -or
    -not $evidence.EvidenceComplete) {
    Fail "EXEC_SIM_R060_FAIL_BUILD_TEST_VALIDATOR_EVIDENCE_MISSING" "Build/tests/validator evidence missing or incomplete."
}

Write-Host "EXEC-SIM-R060 validation passed"
