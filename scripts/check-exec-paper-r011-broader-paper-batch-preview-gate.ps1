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
        Fail "EXEC_PAPER_R011_FAIL_BUILD_OR_TESTS" "Missing required artifact: $path"
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
    "phase-exec-paper-r011-summary.md",
    "phase-exec-paper-r011-r010-batch-reference.json",
    "phase-exec-paper-r011-r009-contract-reference.json",
    "phase-exec-paper-r011-batch-command-safety-check.json",
    "phase-exec-paper-r011-batch-execution-result.json",
    "phase-exec-paper-r011-output-artifact-inventory.json",
    "phase-exec-paper-r011-paper-plan-lines-aggregate.json",
    "phase-exec-paper-r011-usd-pair-normalization-aggregate.json",
    "phase-exec-paper-r011-inversion-aggregate.json",
    "phase-exec-paper-r011-target-close-binding-aggregate.json",
    "phase-exec-paper-r011-readiness-binding-aggregate.json",
    "phase-exec-paper-r011-risk-operator-approval-for-preview.json",
    "phase-exec-paper-r011-r009-handoff-package-aggregate.json",
    "phase-exec-paper-r011-r009-design-only-preview-lines.json",
    "phase-exec-paper-r011-preview-line-coverage.json",
    "phase-exec-paper-r011-held-line-diagnostics.json",
    "phase-exec-paper-r011-operator-review-report.md",
    "phase-exec-paper-r011-operator-review-report.json",
    "phase-exec-paper-r011-preview-decision.json",
    "phase-exec-paper-r011-next-paper-only-evaluation-recommendation.json",
    "phase-exec-paper-r011-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-paper-r011-legacy-compatibility-preservation.json",
    "phase-exec-paper-r011-usd-pair-normalization-preservation.json",
    "phase-exec-paper-r011-direct-cross-exclusion-preservation.json",
    "phase-exec-paper-r011-cost-guidance-preservation.json",
    "phase-exec-paper-r011-nonmajor-calibration-preservation.json",
    "phase-exec-paper-r011-no-broker-activation-audit.json",
    "phase-exec-paper-r011-no-live-marketdata-audit.json",
    "phase-exec-paper-r011-no-scheduler-service-polling-audit.json",
    "phase-exec-paper-r011-no-executable-schedule-audit.json",
    "phase-exec-paper-r011-no-child-slices-audit.json",
    "phase-exec-paper-r011-no-child-orders-audit.json",
    "phase-exec-paper-r011-no-order-created-audit.json",
    "phase-exec-paper-r011-no-real-fill-audit.json",
    "phase-exec-paper-r011-no-execution-report-audit.json",
    "phase-exec-paper-r011-no-route-no-submission-audit.json",
    "phase-exec-paper-r011-no-paper-ledger-commit-audit.json",
    "phase-exec-paper-r011-no-polygon-api-call-audit.json",
    "phase-exec-paper-r011-no-lmax-call-audit.json",
    "phase-exec-paper-r011-no-external-api-call-audit.json",
    "phase-exec-paper-r011-usdjpy-caveat-preservation.json",
    "phase-exec-paper-r011-lmax-readonly-baseline-reference.json",
    "phase-exec-paper-r011-no-external-audit.json",
    "phase-exec-paper-r011-forbidden-actions-audit.json",
    "phase-exec-paper-r011-next-phase-recommendation.json",
    "phase-exec-paper-r011-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsRoot $artifact))) {
        Fail "EXEC_PAPER_R011_FAIL_BUILD_OR_TESTS" "Missing required artifact: $artifact"
    }
}

$contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-r009-contract-reference.json")
$safety = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-batch-command-safety-check.json")
$execution = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-batch-execution-result.json")
$lines = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-paper-plan-lines-aggregate.json")
$usdPair = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-usd-pair-normalization-aggregate.json")
$inversion = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-inversion-aggregate.json")
$targetClose = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-target-close-binding-aggregate.json")
$readiness = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-readiness-binding-aggregate.json")
$riskApproval = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-risk-operator-approval-for-preview.json")
$handoff = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-r009-handoff-package-aggregate.json")
$preview = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-r009-design-only-preview-lines.json")
$coverage = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-preview-line-coverage.json")
$held = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-held-line-diagnostics.json")
$decision = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-preview-decision.json")
$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-canonical-quarter-hour-policy-preservation.json")
$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-legacy-compatibility-preservation.json")
$directCross = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-direct-cross-exclusion-preservation.json")
$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-cost-guidance-preservation.json")
$usdjpy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-usdjpy-caveat-preservation.json")
$noExternal = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-build-test-validator-evidence.json")

if (-not $safety.SafetyValidatedBeforeExecution -or -not $safety.AllCommandsSafe -or $safety.UnsafeReasonCount -ne 0) {
    Fail "EXEC_PAPER_R011_FAIL_COMMAND_RUN_WITHOUT_SAFETY_VALIDATION" "Batch commands were not proven safe before execution."
}

foreach ($check in (As-Array $safety.CommandChecks)) {
    if (-not $check.SafeForLocalManualNoExternalExecution -or
        -not $check.IncludesManualNoExternal -or
        -not $check.IncludesOutputArtifactsDir -or
        -not $check.IncludesFixturePath -or
        -not $check.IncludesQubesRunId -or
        -not $check.IncludesRequestedCycleRunId -or
        -not $check.IncludesCadence15 -or
        -not $check.IncludesNoPaperLedgerCommitTrue -or
        $check.DeprecatedNoExternalPaperCycleModeUsed -or
        $check.DeprecatedOutputArgumentUsed -or
        $check.ForbiddenRuntimeFlagsDetected) {
        Fail "EXEC_PAPER_R011_FAIL_UNSAFE_COMMAND_TEMPLATE" "Unsafe ManualNoExternal command check: $($check.BatchEntryId)"
    }
}

if (-not $execution.AllRunsCompletedSafely -or
    -not $execution.NoExternal -or
    -not $execution.NoPaperLedgerCommit -or
    -not $execution.NoOrderFillReportRouteSubmission -or
    $execution.MoreCommandsThanAcceptedEntries -or
    $execution.CommandsExecuted -ne $execution.AcceptedBatchEntries -or
    $execution.CommandsExecuted -gt 20) {
    Fail "EXEC_PAPER_R011_FAIL_BATCH_EXECUTION_SAFETY" "ManualNoExternal execution result is unsafe or inconsistent."
}

foreach ($result in (As-Array $execution.Results)) {
    if (-not $result.CompletedSafely -or
        $result.ExitCode -ne 0 -or
        -not $result.NoExternal -or
        -not $result.NoPaperLedgerCommit -or
        -not $result.NoOrder -or
        -not $result.NoFill -or
        -not $result.NoReport -or
        -not $result.NoRoute -or
        -not $result.NoSubmission -or
        $result.CycleExecutionCount -ne 1) {
        Fail "EXEC_PAPER_R011_FAIL_BATCH_EXECUTION_SAFETY" "Unsafe run result: $($result.BatchEntryId)"
    }
}

if (-not $contract.NonExecutable -or -not $contract.NotAnOrder -or -not $contract.NoBrokerRoute -or $contract.ExecutablePromotionAuthorized) {
    Fail "EXEC_PAPER_R011_FAIL_R009_PROMOTED_TO_EXECUTABLE" "R009 contract reference was promoted or weakened."
}

if ($noExternal.PolygonCalled -or $noExternal.LmaxCalled -or $noExternal.ExternalApiCalled -or $noExternal.BrokerActivation -or $noExternal.LiveMarketData -or -not $noExternal.NoExternal) {
    Fail "EXEC_PAPER_R011_FAIL_API_OR_BROKER_ACTIVITY" "No-external audit reports API, broker, or live market data activity."
}

if ($forbidden.ForbiddenActionsDetected -or
    $forbidden.BrokerActivation -or
    $forbidden.LiveMarketData -or
    $forbidden.SchedulerServicePolling -or
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
    $forbidden.R009PromotedToExecutable -or
    -not $forbidden.CommandsExecutedOnlyAfterSafetyValidation) {
    Fail "EXEC_PAPER_R011_FAIL_FORBIDDEN_ACTION_DETECTED" "Forbidden action audit reports a blocked action."
}

if ($coverage.AcceptedBatchEntries -ne 20 -or
    $coverage.ExpectedMaximumPreviewLineCount -ne 140 -or
    $coverage.PaperExecutionPlanLineCount -ne 140 -or
    $coverage.R009PreviewLineCount -ne 140) {
    Fail "EXEC_PAPER_R011_FAIL_PREVIEW_COVERAGE" "Preview coverage does not match 20 x 7 expected shape."
}

if ($lines.LineCount -ne 140 -or $preview.PreviewLineCount -ne 140 -or $handoff.HandoffLineCount -ne 140) {
    Fail "EXEC_PAPER_R011_FAIL_PREVIEW_COVERAGE" "Aggregate line counts are inconsistent."
}

foreach ($line in (As-Array $preview.Lines)) {
    if (-not $line.DesignOnlyPreview -or
        -not $line.NonExecutable -or
        -not $line.NotAnOrder -or
        -not $line.NotSubmitted -or
        -not $line.NoBrokerRoute -or
        -not $line.NoChildSlices -or
        -not $line.NoExecutableSchedule -or
        -not $line.NoFill -or
        -not $line.NoExecutionReport -or
        -not $line.NoRoute -or
        -not $line.NoSubmission -or
        -not $line.NoPaperLedgerCommit) {
        Fail "EXEC_PAPER_R011_FAIL_PREVIEW_LINE_REPRESENTED_AS_ORDER" "Preview line weakens non-executable flags: $($line.PaperExecutionPlanLineId)"
    }

    if ($line.CanonicalTargetCloseLocal -match "T\d{2}:(06|21|36|51):00") {
        Fail "EXEC_PAPER_R011_FAIL_LEGACY_06_USED_AS_FUTURE_CANONICAL" "Preview line uses legacy minute as future canonical: $($line.PaperExecutionPlanLineId)"
    }
}

if (-not $usdPair.USDPairOnlyAfterNetting -or $usdPair.DirectCrossExecutableLineCount -ne 0) {
    Fail "EXEC_PAPER_R011_FAIL_DIRECT_CROSS_EMITTED_AS_EXECUTABLE_LINE" "Direct crosses were emitted as executable lines."
}

if (-not $inversion.USDJPYCaveatPreserved -or $inversion.USDJPYLines -ne 20 -or $inversion.USDCADLines -ne 20 -or $inversion.USDCHFLines -ne 20) {
    Fail "EXEC_PAPER_R011_FAIL_USDJPY_CAVEAT_WEAKENED" "Inversion aggregate is missing expected USDJPY/USDCAD/USDCHF lines."
}

if ($targetClose.LinesWithTargetClose -ne 140 -or $targetClose.LinesCanonicalQuarterHour -ne 140 -or $targetClose.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_PAPER_R011_FAIL_LEGACY_06_USED_AS_FUTURE_CANONICAL" "Target close aggregate is missing canonical quarter-hour coverage."
}

if ($readiness.CompleteReadinessBindingCount -ne 140 -or
    $readiness.LinesWithQuoteWindowReadiness -ne 140 -or
    $readiness.LinesWithCloseBenchmarkReadiness -ne 140 -or
    $readiness.LinesWithFeedQualityReadiness -ne 140) {
    Fail "EXEC_PAPER_R011_FAIL_READINESS_BINDING_MISSING" "Readiness binding aggregate is incomplete."
}

if ($held.HeldLineCount -ne 0 -or $coverage.HeldLineCount -ne 0) {
    Fail "EXEC_PAPER_R011_FAIL_HELD_LINES_PRESENT" "Held-line diagnostics are non-empty for a claimed complete preview."
}

if (-not $riskApproval.ApprovedForPreviewOnly -or
    $riskApproval.ApprovedForExecutableUse -or
    $riskApproval.ApprovedForOrderCreation -or
    $riskApproval.ApprovedForScheduleCreation -or
    $riskApproval.ApprovedForChildSlices -or
    $riskApproval.ApprovedForBrokerRouting -or
    $riskApproval.ApprovedForSubmission -or
    $riskApproval.ApprovedForFillOrExecutionReport -or
    $riskApproval.ApprovedForPaperLedgerCommit -or
    $riskApproval.ApprovedForStateMutation -or
    $riskApproval.ApprovedForLiveTrading) {
    Fail "EXEC_PAPER_R011_FAIL_PREVIEW_APPROVAL_WIDENED" "Preview approval was widened beyond design-only preview."
}

if ($decision.ExecutablePromotionAuthorized -or $decision.OrdersAuthorized -or $decision.LedgerCommitAuthorized) {
    Fail "EXEC_PAPER_R011_FAIL_R009_PROMOTED_TO_EXECUTABLE" "Preview decision authorizes executable/order/ledger behavior."
}

if ($canonical.Legacy06UsedAsFutureCanonical -or -not $canonical.FutureTimestampsUseCanonicalQuarterHour -or $legacy.Legacy06UsedAsFutureCanonical -or -not $legacy.LegacyTimestampsCompatibilityOnly) {
    Fail "EXEC_PAPER_R011_FAIL_LEGACY_06_USED_AS_FUTURE_CANONICAL" "Canonical/legacy preservation is weakened."
}

if ($directCross.DirectCrossExecutionEnabled -or -not $directCross.NettingFirst) {
    Fail "EXEC_PAPER_R011_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct-cross preservation is weakened."
}

if ($cost.FiveUsdPerMillionUniversalized -or -not $cost.FiveUsdPerMillionBestCaseMajorOnly) {
    Fail "EXEC_PAPER_R011_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million guidance is universalized."
}

if (-not $usdjpy.RequiresInversion -or $usdjpy.SecurityID -ne "4004" -or $usdjpy.SecurityIDSource -ne "8" -or $usdjpy.USDJPYCaveatWeakened) {
    Fail "EXEC_PAPER_R011_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat preservation is weakened."
}

if ($evidence.DotnetBuild -ne "Passed" -or
    $evidence.FocusedR011Tests -ne "Passed" -or
    $evidence.UnitTests -ne "Passed" -or
    $evidence.R011Validator -ne "Passed" -or
    -not $evidence.EvidenceComplete) {
    Fail "EXEC_PAPER_R011_FAIL_BUILD_OR_TESTS" "Build/tests/validator evidence is missing or not passed."
}

Write-Output "EXEC_PAPER_R011_PASS_BROADER_BATCH_COMMANDS_SAFE_NO_EXTERNAL"
Write-Output "EXEC_PAPER_R011_PASS_MANUAL_NOEXTERNAL_BATCH_RUNS_READY_NO_EXTERNAL"
Write-Output "EXEC_PAPER_R011_PASS_R009_BROADER_PREVIEW_READY_NO_EXTERNAL"
Write-Output "EXEC_PAPER_R011_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
