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
        Fail "EXEC_SIM_R057_FAIL_BUILD_OR_TESTS" "Missing required artifact: $path"
    }

    return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

$requiredArtifacts = @(
    "phase-exec-sim-r057-summary.md",
    "phase-exec-sim-r057-r056-review-reference.json",
    "phase-exec-sim-r057-r008-preview-reference.json",
    "phase-exec-sim-r057-r009-contract-reference.json",
    "phase-exec-sim-r057-broader-paper-only-evaluation-contract.json",
    "phase-exec-sim-r057-broader-paper-only-evaluation-plan.json",
    "phase-exec-sim-r057-fixture-requirements.json",
    "phase-exec-sim-r057-target-close-requirements.json",
    "phase-exec-sim-r057-readiness-requirements.json",
    "phase-exec-sim-r057-risk-operator-approval-requirements.json",
    "phase-exec-sim-r057-manual-noexternal-command-templates.md",
    "phase-exec-sim-r057-manual-noexternal-command-templates.json",
    "phase-exec-sim-r057-expected-run-artifacts.json",
    "phase-exec-sim-r057-aggregation-reporting-requirements.json",
    "phase-exec-sim-r057-success-criteria.json",
    "phase-exec-sim-r057-hold-criteria.json",
    "phase-exec-sim-r057-next-operator-action-package.json",
    "phase-exec-sim-r057-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-sim-r057-legacy-compatibility-preservation.json",
    "phase-exec-sim-r057-usd-pair-normalization-preservation.json",
    "phase-exec-sim-r057-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r057-cost-guidance-preservation.json",
    "phase-exec-sim-r057-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r057-no-broker-activation-audit.json",
    "phase-exec-sim-r057-no-live-marketdata-audit.json",
    "phase-exec-sim-r057-no-scheduler-service-polling-audit.json",
    "phase-exec-sim-r057-no-executable-schedule-audit.json",
    "phase-exec-sim-r057-no-child-slices-audit.json",
    "phase-exec-sim-r057-no-child-orders-audit.json",
    "phase-exec-sim-r057-no-order-created-audit.json",
    "phase-exec-sim-r057-no-real-fill-audit.json",
    "phase-exec-sim-r057-no-execution-report-audit.json",
    "phase-exec-sim-r057-no-route-no-submission-audit.json",
    "phase-exec-sim-r057-no-paper-ledger-commit-audit.json",
    "phase-exec-sim-r057-no-polygon-api-call-audit.json",
    "phase-exec-sim-r057-no-lmax-call-audit.json",
    "phase-exec-sim-r057-no-external-api-call-audit.json",
    "phase-exec-sim-r057-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r057-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r057-no-external-audit.json",
    "phase-exec-sim-r057-forbidden-actions-audit.json",
    "phase-exec-sim-r057-next-phase-recommendation.json",
    "phase-exec-sim-r057-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    $path = Join-Path $ArtifactsRoot $artifact
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "EXEC_SIM_R057_FAIL_BUILD_OR_TESTS" "Missing required artifact: $artifact"
    }
}

$contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r057-broader-paper-only-evaluation-contract.json")
$plan = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r057-broader-paper-only-evaluation-plan.json")
$fixture = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r057-fixture-requirements.json")
$targetClose = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r057-target-close-requirements.json")
$readiness = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r057-readiness-requirements.json")
$approval = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r057-risk-operator-approval-requirements.json")
$commands = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r057-manual-noexternal-command-templates.json")
$success = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r057-success-criteria.json")
$holds = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r057-hold-criteria.json")
$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r057-canonical-quarter-hour-policy-preservation.json")
$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r057-legacy-compatibility-preservation.json")
$usdPair = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r057-usd-pair-normalization-preservation.json")
$directCross = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r057-direct-cross-exclusion-preservation.json")
$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r057-cost-guidance-preservation.json")
$usdjpy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r057-usdjpy-caveat-preservation.json")
$forbidden = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r057-forbidden-actions-audit.json")
$noExternal = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r057-no-external-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r057-build-test-validator-evidence.json")

if ($noExternal.PolygonCalled -or $noExternal.LmaxCalled -or $noExternal.ExternalApiCalled -or $noExternal.FilesDownloaded) {
    Fail "EXEC_SIM_R057_FAIL_API_CALL_DETECTED" "No-external audit reports external activity."
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
    $forbidden.StateMutation -or
    $forbidden.CommandsExecuted) {
    Fail "EXEC_SIM_R057_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden action audit reports a blocked action."
}

if (-not $contract.NonExecutable -or -not $contract.NotAnOrder -or -not $contract.NoBrokerRoute -or -not $contract.NoPaperLedgerCommit -or $contract.ExecutablePromotionAuthorized) {
    Fail "EXEC_SIM_R057_FAIL_R009_PROMOTED_TO_EXECUTABLE" "Broader evaluation contract weakens non-executable constraints."
}

if ([int]$contract.MinimumTargetCloses -lt 20 -or [int]$contract.MinimumQubesFixtures -lt 20 -or [int]$plan.RecommendedMinimumTargetCloses -lt 20) {
    Fail "EXEC_SIM_R057_FAIL_OPERATOR_PACKAGE_MISSING" "Broader paper-only evaluation minimum size is missing or below 20 target closes."
}

if ($fixture.DirectCrossExecutionAllowed -or -not $fixture.NettingRequiredBeforePreview -or -not $fixture.USDPairExecutionPreviewOnly) {
    Fail "EXEC_SIM_R057_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Fixture requirements weaken direct-cross/netting constraints."
}

if ($targetClose.Legacy06UsedAsFutureCanonical -or -not $targetClose.TargetCloseSuppliedSeparately -or -not $targetClose.HoldIfNotCanonicalQuarterHour) {
    Fail "EXEC_SIM_R057_FAIL_LEGACY_06_USED_AS_FUTURE_CANONICAL" "Target-close requirements weaken canonical quarter-hour policy."
}

if (-not $readiness.HoldIfMissingQuoteWindow -or -not $readiness.HoldIfMissingCloseBenchmark -or -not $readiness.HoldIfMissingFeedQuality) {
    Fail "EXEC_SIM_R057_FAIL_OPERATOR_PACKAGE_MISSING" "Readiness requirements do not hold on missing bindings."
}

if ($approval.ApprovedForExecutableUse -or
    $approval.ApprovedForOrderCreation -or
    $approval.ApprovedForScheduleCreation -or
    $approval.ApprovedForChildSlices -or
    $approval.ApprovedForBrokerRouting -or
    $approval.ApprovedForSubmission -or
    $approval.ApprovedForFillOrExecutionReport -or
    $approval.ApprovedForPaperLedgerCommit -or
    $approval.ApprovedForStateMutation -or
    $approval.ApprovedForLiveTrading -or
    -not $approval.PreviewApprovalMustNotBeExecutableApproval) {
    Fail "EXEC_SIM_R057_FAIL_EXECUTABLE_APPROVAL_SCOPE_WIDENED" "Risk/operator approval requirements are wider than preview-only."
}

if ($commands.CommandsExecutedByR057) {
    Fail "EXEC_SIM_R057_FAIL_PMS_CYCLE_RUN" "Command templates report execution by R057."
}

foreach ($template in $commands.Templates) {
    if ($template.CommandLine -notmatch "--mode ManualNoExternal") {
        Fail "EXEC_SIM_R057_FAIL_OPERATOR_PACKAGE_MISSING" "Command template omits ManualNoExternal mode."
    }

    if ($template.CommandLine -notmatch "--no-paper-ledger-commit true") {
        Fail "EXEC_SIM_R057_FAIL_COMMAND_TEMPLATE_OMITS_NO_LEDGER_COMMIT" "Command template omits --no-paper-ledger-commit true."
    }

    if ($template.CommandLine -match "--mode no-external-paper-cycle" -or $template.CommandLine -match "\\s--output\\s") {
        Fail "EXEC_SIM_R057_FAIL_OPERATOR_PACKAGE_MISSING" "Command template uses deprecated mode or output argument."
    }

    if (-not $template.OperatorRunOnly) {
        Fail "EXEC_SIM_R057_FAIL_OPERATOR_PACKAGE_MISSING" "Command template is not marked operator-run only."
    }
}

if ($canonical.Legacy06UsedAsFutureCanonical -or -not $canonical.FutureTimestampsUseCanonicalQuarterHour -or $legacy.Legacy06UsedAsFutureCanonical -or -not $legacy.LegacyTimestampsCompatibilityOnly) {
    Fail "EXEC_SIM_R057_FAIL_LEGACY_06_USED_AS_FUTURE_CANONICAL" "Canonical/legacy preservation is weakened."
}

if ($directCross.DirectCrossExecutionEnabled -or -not $directCross.NettingFirst) {
    Fail "EXEC_SIM_R057_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct-cross exclusion preservation is weakened."
}

if ($cost.FiveUsdPerMillionUniversalized -or -not $cost.FiveUsdPerMillionBestCaseMajorOnly) {
    Fail "EXEC_SIM_R057_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million guidance is universalized."
}

if (-not $usdjpy.RequiresInversion -or $usdjpy.SecurityID -ne "4004" -or $usdjpy.SecurityIDSource -ne "8" -or $usdjpy.USDJPYCaveatWeakened) {
    Fail "EXEC_SIM_R057_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat is weakened."
}

if (-not $usdPair.AUDUSDNotFailed) {
    Fail "EXEC_SIM_R057_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD is misclassified."
}

if ($success.ExecutablePromotionAuthorized) {
    Fail "EXEC_SIM_R057_FAIL_R009_PROMOTED_TO_EXECUTABLE" "Success criteria authorize executable promotion."
}

if (-not $holds.HoldIsSafe -or -not $holds.HoldDoesNotAuthorizeExecution) {
    Fail "EXEC_SIM_R057_FAIL_OPERATOR_PACKAGE_MISSING" "Hold criteria do not preserve safe no-execution behavior."
}

if ($evidence.DotnetBuild -ne "Passed" -or $evidence.FocusedR057Tests -ne "Passed" -or $evidence.UnitTests -ne "Passed" -or $evidence.R057Validator -ne "Passed") {
    Fail "EXEC_SIM_R057_FAIL_BUILD_OR_TESTS" "Build/tests/validator evidence is missing or not passed."
}

Write-Output "EXEC_SIM_R057_PASS_BROADER_PAPER_ONLY_EVALUATION_PLAN_READY_NO_EXTERNAL"
Write-Output "EXEC_SIM_R057_PASS_OPERATOR_PACKAGE_READY_NO_EXTERNAL"
Write-Output "EXEC_SIM_R057_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
