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
        Fail "EXEC_PAPER_R014_FAIL_MISSING_ARTIFACT" "Missing required artifact: $path"
    }
    return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function As-Array($value) {
    if ($null -eq $value) { return @() }
    if ($value -is [System.Array]) { return $value }
    return @($value)
}

$requiredArtifacts = @(
    "phase-exec-paper-r014-summary.md",
    "phase-exec-paper-r014-r013-package-reference.json",
    "phase-exec-paper-r014-r009-contract-reference.json",
    "phase-exec-paper-r014-long-run-command-safety-check.json",
    "phase-exec-paper-r014-batch-execution-result.json",
    "phase-exec-paper-r014-output-artifact-inventory.json",
    "phase-exec-paper-r014-paper-plan-lines-aggregate.json",
    "phase-exec-paper-r014-usd-pair-normalization-aggregate.json",
    "phase-exec-paper-r014-inversion-aggregate.json",
    "phase-exec-paper-r014-target-close-binding-aggregate.json",
    "phase-exec-paper-r014-readiness-binding-aggregate.json",
    "phase-exec-paper-r014-risk-operator-approval-for-preview.json",
    "phase-exec-paper-r014-r009-handoff-package-aggregate.json",
    "phase-exec-paper-r014-r009-design-only-preview-lines.json",
    "phase-exec-paper-r014-preview-line-coverage.json",
    "phase-exec-paper-r014-bar-role-coverage-review.json",
    "phase-exec-paper-r014-per-symbol-coverage-review.json",
    "phase-exec-paper-r014-held-line-diagnostics.json",
    "phase-exec-paper-r014-direct-cross-netting-review.json",
    "phase-exec-paper-r014-inversion-review.json",
    "phase-exec-paper-r014-operator-review-report.md",
    "phase-exec-paper-r014-operator-review-report.json",
    "phase-exec-paper-r014-long-run-maturity-decision.json",
    "phase-exec-paper-r014-next-phase-recommendation.json",
    "phase-exec-paper-r014-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-paper-r014-legacy-compatibility-preservation.json",
    "phase-exec-paper-r014-usd-pair-normalization-preservation.json",
    "phase-exec-paper-r014-direct-cross-exclusion-preservation.json",
    "phase-exec-paper-r014-cost-guidance-preservation.json",
    "phase-exec-paper-r014-nonmajor-calibration-preservation.json",
    "phase-exec-paper-r014-no-broker-activation-audit.json",
    "phase-exec-paper-r014-no-live-marketdata-audit.json",
    "phase-exec-paper-r014-no-scheduler-service-polling-audit.json",
    "phase-exec-paper-r014-no-executable-schedule-audit.json",
    "phase-exec-paper-r014-no-child-slices-audit.json",
    "phase-exec-paper-r014-no-child-orders-audit.json",
    "phase-exec-paper-r014-no-order-created-audit.json",
    "phase-exec-paper-r014-no-real-fill-audit.json",
    "phase-exec-paper-r014-no-execution-report-audit.json",
    "phase-exec-paper-r014-no-route-no-submission-audit.json",
    "phase-exec-paper-r014-no-paper-ledger-commit-audit.json",
    "phase-exec-paper-r014-no-polygon-api-call-audit.json",
    "phase-exec-paper-r014-no-lmax-call-audit.json",
    "phase-exec-paper-r014-no-external-api-call-audit.json",
    "phase-exec-paper-r014-usdjpy-caveat-preservation.json",
    "phase-exec-paper-r014-lmax-readonly-baseline-reference.json",
    "phase-exec-paper-r014-no-external-audit.json",
    "phase-exec-paper-r014-forbidden-actions-audit.json",
    "phase-exec-paper-r014-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsRoot $artifact))) {
        Fail "EXEC_PAPER_R014_FAIL_MISSING_ARTIFACT" "Missing required artifact: $artifact"
    }
}

$r013 = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-r013-package-reference.json")
$contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-r009-contract-reference.json")
$safety = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-long-run-command-safety-check.json")
$execution = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-batch-execution-result.json")
$inventory = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-output-artifact-inventory.json")
$paperLines = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-paper-plan-lines-aggregate.json")
$normalization = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-usd-pair-normalization-aggregate.json")
$inversionAggregate = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-inversion-aggregate.json")
$targetClose = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-target-close-binding-aggregate.json")
$readiness = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-readiness-binding-aggregate.json")
$riskApproval = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-risk-operator-approval-for-preview.json")
$handoff = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-r009-handoff-package-aggregate.json")
$preview = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-r009-design-only-preview-lines.json")
$coverage = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-preview-line-coverage.json")
$barRole = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-bar-role-coverage-review.json")
$symbolCoverage = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-per-symbol-coverage-review.json")
$held = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-held-line-diagnostics.json")
$directCross = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-direct-cross-netting-review.json")
$inversion = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-inversion-review.json")
$operatorReview = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-operator-review-report.json")
$decision = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-long-run-maturity-decision.json")
$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-canonical-quarter-hour-policy-preservation.json")
$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-legacy-compatibility-preservation.json")
$usdPair = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-usd-pair-normalization-preservation.json")
$directCrossPreservation = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-direct-cross-exclusion-preservation.json")
$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-cost-guidance-preservation.json")
$nonmajor = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-nonmajor-calibration-preservation.json")
$usdjpy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-usdjpy-caveat-preservation.json")
$noExternal = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r014-build-test-validator-evidence.json")

if ($r013.BatchEntryCount -ne 100 -or
    $r013.CommandTemplateCount -ne 100 -or
    $r013.CommandsExecutedInR013 -or
    $r013.ExpectedMaximumPreviewLines -ne 700 -or
    $r013.ManifestStatus -ne "FullLongRunBatchReady") {
    Fail "EXEC_PAPER_R014_FAIL_R013_REFERENCE_INVALID" "R013 package reference is missing expected 100-entry package data."
}

if ($contract.ContractVersion -ne "0.3.0-design-only-candidate" -or
    -not $contract.DesignOnly -or
    -not $contract.PaperOnly -or
    -not $contract.NonExecutable -or
    -not $contract.NotAnOrder -or
    -not $contract.NotSubmitted -or
    -not $contract.NoBrokerRoute -or
    $contract.ExecutablePromotionAuthorized -or
    $contract.BrokerReady -or
    $contract.LiveReady) {
    Fail "EXEC_PAPER_R014_FAIL_R009_CONTRACT_PROMOTED" "R009 contract is missing design-only non-executable constraints."
}

if (-not $safety.SafetyValidatedBeforeExecution -or
    -not $safety.AllCommandsSafe -or
    $safety.CommandCount -ne 100 -or
    $safety.AcceptedBatchEntries -ne 100 -or
    $safety.UnsafeReasonCount -ne 0) {
    Fail "EXEC_PAPER_R014_FAIL_COMMAND_SAFETY" "Commands were not validated safe before execution."
}

foreach ($check in (As-Array $safety.Checks)) {
    if (-not $check.Safe -or
        -not $check.UsesManualNoExternal -or
        -not $check.IncludesOutputArtifactsDir -or
        -not $check.IncludesNoPaperLedgerCommitTrue -or
        -not $check.IncludesCadence15 -or
        $check.DeprecatedModeUsed -or
        $check.DeprecatedOutputUsed -or
        $check.BrokerLiveOrderRouteSubmissionFlagsPresent -or
        -not $check.FixtureValid -or
        -not $check.CanonicalTargetCloseConfirmed) {
        Fail "EXEC_PAPER_R014_FAIL_UNSAFE_COMMAND_OR_INPUT" "Unsafe command or input detected for batch entry $($check.BatchEntryId)."
    }
}

if ($execution.CommandsExecuted -ne 100 -or
    $execution.AcceptedBatchEntries -ne 100 -or
    $execution.MoreCommandsThanAcceptedEntries -or
    -not $execution.AllRunsCompletedSafely -or
    -not $execution.NoExternal -or
    -not $execution.NoBroker -or
    -not $execution.NoLiveMarketData -or
    -not $execution.NoPaperLedgerCommit -or
    -not $execution.NoOrderFillReportRouteSubmission) {
    Fail "EXEC_PAPER_R014_FAIL_BATCH_EXECUTION_UNSAFE" "Batch execution result is unsafe or incomplete."
}

foreach ($result in (As-Array $execution.Results)) {
    if ($result.ExitCode -ne 0 -or
        $result.LineCount -ne 7 -or
        $result.CycleExecutionCount -ne 1 -or
        -not $result.NoExternal -or
        -not $result.NoBroker -or
        -not $result.NoLiveMarketData -or
        -not $result.NoPaperLedgerCommit -or
        -not $result.NoOrder -or
        -not $result.NoFill -or
        -not $result.NoReport -or
        -not $result.NoRoute -or
        -not $result.NoSubmission -or
        -not $result.CompletedSafely) {
        Fail "EXEC_PAPER_R014_FAIL_RUN_RESULT_UNSAFE" "Unsafe run result detected for batch entry $($result.BatchEntryId)."
    }
}

if ($inventory.RunCount -ne 100 -or
    @((As-Array $inventory.Inventory) | Where-Object { -not $_.SummaryArtifactExists }).Count -ne 0 -or
    @((As-Array $inventory.Inventory) | Where-Object { -not $_.PaperExecutionPlanArtifactExists }).Count -ne 0 -or
    @((As-Array $inventory.Inventory) | Where-Object { -not $_.PaperExecutionPlanLinesArtifactExists }).Count -ne 0) {
    Fail "EXEC_PAPER_R014_FAIL_OUTPUT_INVENTORY" "Expected output artifacts are missing."
}

if ($paperLines.LineCount -ne 700) {
    Fail "EXEC_PAPER_R014_FAIL_PAPER_PLAN_LINE_COUNT" "Expected 700 paper plan lines."
}

if (-not $normalization.USDPairOnlyAfterNetting -or $normalization.DirectCrossExecutableLineCount -ne 0) {
    Fail "EXEC_PAPER_R014_FAIL_USD_PAIR_NORMALIZATION" "USD-pair normalization or direct-cross exclusion failed."
}

if (-not $inversionAggregate.InversionsSafe -or
    $inversionAggregate.InversionFailureCount -ne 0 -or
    -not $inversion.InversionsSafe -or
    -not $inversion.USDJPYCaveatPreserved) {
    Fail "EXEC_PAPER_R014_FAIL_INVERSION_REVIEW" "Inversion review failed."
}

if ($targetClose.BoundLineCount -ne 700 -or
    -not $targetClose.CanonicalQuarterHourConfirmed) {
    Fail "EXEC_PAPER_R014_FAIL_TARGET_CLOSE_BINDING" "Target close binding is invalid."
}

if ($coverage.BatchEntryCount -ne 100 -or
    $coverage.PaperPlanLineCount -ne 700 -or
    $coverage.PreviewLineCount -ne 700 -or
    $coverage.ExpectedMaximumPreviewLineCount -ne 700 -or
    $coverage.DirectCrossExecutableLineCount -ne 0) {
    Fail "EXEC_PAPER_R014_FAIL_PREVIEW_LINE_COVERAGE" "Preview line coverage is invalid."
}

if ($readiness.PreviewLineCount -ne 700 -or
    ($readiness.CompleteReadinessBindingCount + $readiness.MissingReadinessBindingCount) -ne 700) {
    Fail "EXEC_PAPER_R014_FAIL_READINESS_BINDING_COUNT" "Readiness binding counts are inconsistent."
}

if ($coverage.HeldLineCount -ne $held.HeldLineCount -or $held.HeldLineCount -ne $readiness.MissingReadinessBindingCount) {
    Fail "EXEC_PAPER_R014_FAIL_HELD_LINE_DIAGNOSTICS" "Held-line diagnostics do not match missing readiness bindings."
}

foreach ($line in (As-Array $held.HeldLines)) {
    if ([string]::IsNullOrWhiteSpace([string]$line.HoldReason) -or [string]$line.HoldReason -notmatch "Missing.*ReadinessBinding") {
        Fail "EXEC_PAPER_R014_FAIL_HELD_LINE_REASON" "Held line missing explicit readiness hold reason."
    }
}

$roles = @{}
foreach ($role in (As-Array $barRole.Coverage)) { $roles[[string]$role.BarRole] = $role }
if ($roles["OpeningBuild"].PreviewLineCount -ne 224 -or
    $roles["IntradayRebalance"].PreviewLineCount -ne 266 -or
    $roles["ClosingFlatten"].PreviewLineCount -ne 210) {
    Fail "EXEC_PAPER_R014_FAIL_BAR_ROLE_COVERAGE" "Bar-role preview coverage does not match R013 package."
}

foreach ($symbol in (As-Array $symbolCoverage.Coverage)) {
    if ($symbol.PreviewLineCount -ne 100) {
        Fail "EXEC_PAPER_R014_FAIL_SYMBOL_COVERAGE" "Expected 100 preview lines for symbol $($symbol.ExecutionTradableSymbol)."
    }
}

if (-not $directCross.DirectCrossesExcludedAfterNetting -or $directCross.DirectCrossExecutableLineCount -ne 0) {
    Fail "EXEC_PAPER_R014_FAIL_DIRECT_CROSS_EXECUTABLE_LINE" "Direct crosses were emitted as executable lines."
}

if ($riskApproval.RiskReviewStatus -ne "ApprovedForNonExecutablePreview" -or
    $riskApproval.OperatorApprovalStatus -ne "ApprovedForDesignOnlyPreviewOnly" -or
    $riskApproval.Scope -ne "R009DesignOnlyPreviewOnly" -or
    $riskApproval.ApprovedForExecutableUse -or
    $riskApproval.ApprovedForOrderCreation -or
    $riskApproval.ApprovedForBrokerRouting -or
    $riskApproval.ApprovedForPaperLedgerCommit) {
    Fail "EXEC_PAPER_R014_FAIL_PREVIEW_APPROVAL_SCOPE" "Risk/operator approval scope was widened beyond design-only preview."
}

$previewLines = As-Array $preview.Lines
if ($previewLines.Count -ne 700) {
    Fail "EXEC_PAPER_R014_FAIL_PREVIEW_LINES_MISSING" "Expected 700 R009 preview lines."
}
foreach ($line in $previewLines) {
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
        Fail "EXEC_PAPER_R014_FAIL_PREVIEW_LINE_EXECUTABLE" "Preview line is represented as executable/order/fill/route/submission."
    }
    if ([string]$line.CanonicalTargetCloseLocal -match "T\d{2}:(06|21|36|51):00") {
        Fail "EXEC_PAPER_R014_FAIL_LEGACY_TIMESTAMP_CANONICAL" "Legacy :06/:21/:36/:51 timestamp used as future canonical."
    }
}

$handoffLines = As-Array $handoff.Lines
if ($handoffLines.Count -ne 700) {
    Fail "EXEC_PAPER_R014_FAIL_HANDOFF_LINES_MISSING" "Expected 700 handoff lines."
}

if (-not $operatorReview.AllCommandsPassedSafetyValidation -or
    $operatorReview.ManualNoExternalCommandsRun -ne 100 -or
    $operatorReview.PaperExecutionPlanLinesEmitted -ne 700 -or
    $operatorReview.R009PreviewLinesProduced -ne 700 -or
    -not $operatorReview.DirectCrossesExcludedAfterNetting -or
    -not $operatorReview.InversionsSafe -or
    $operatorReview.HeldLines -ne $coverage.HeldLineCount) {
    Fail "EXEC_PAPER_R014_FAIL_OPERATOR_REVIEW" "Operator review is missing expected long-run paper-only result."
}

$expectedPartial = @(
    "EXEC_PAPER_R014_PASS_LONG_RUN_COMMANDS_SAFE_NO_EXTERNAL",
    "EXEC_PAPER_R014_PARTIAL_LONG_RUN_PREVIEW_WITH_HELD_OR_MISSING_LINES_NO_EXTERNAL",
    "EXEC_PAPER_R014_PASS_DIAGNOSTICS_READY_NO_EXTERNAL",
    "EXEC_PAPER_R014_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
)
$expectedFull = @(
    "EXEC_PAPER_R014_PASS_LONG_RUN_COMMANDS_SAFE_NO_EXTERNAL",
    "EXEC_PAPER_R014_PASS_LONG_RUN_MANUALNOEXTERNAL_RUNS_READY_NO_EXTERNAL",
    "EXEC_PAPER_R014_PASS_R009_LONG_RUN_PREVIEW_READY_NO_EXTERNAL",
    "EXEC_PAPER_R014_PASS_LONG_RUN_PAPER_MATURITY_REVIEW_READY_NO_EXTERNAL",
    "EXEC_PAPER_R014_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
)
$actual = As-Array $decision.Classifications
$expected = if ($coverage.HeldLineCount -eq 0) { $expectedFull } else { $expectedPartial }
foreach ($classification in $expected) {
    if ($actual -notcontains $classification) {
        Fail "EXEC_PAPER_R014_FAIL_CLASSIFICATION" "Missing expected classification: $classification"
    }
}
if ($decision.ExecutablePromotionAuthorized) {
    Fail "EXEC_PAPER_R014_FAIL_EXECUTABLE_PROMOTION" "R009 was promoted to executable use."
}

if (-not $canonical.FutureTimestampsUseCanonicalQuarterHour -or $canonical.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_PAPER_R014_FAIL_CANONICAL_POLICY" "Canonical quarter-hour policy was weakened."
}
if (-not $legacy.LegacyTimestampsCompatibilityOnly -or $legacy.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_PAPER_R014_FAIL_LEGACY_POLICY" "Legacy compatibility policy was weakened."
}
if (-not $usdPair.USDPairOnlyAfterNetting -or $usdPair.DirectCrossExecutionAllowed) {
    Fail "EXEC_PAPER_R014_FAIL_USD_PAIR_PRESERVATION" "USD-pair/direct-cross preservation was weakened."
}
if (-not $directCrossPreservation.DirectCrossesSignalOnly -or -not $directCrossPreservation.DirectCrossExecutionDisabled -or $directCrossPreservation.ExclusionWeakened) {
    Fail "EXEC_PAPER_R014_FAIL_DIRECT_CROSS_PRESERVATION" "Direct-cross exclusion was weakened."
}
if ($cost.FiveUsdPerMillionUniversalized) {
    Fail "EXEC_PAPER_R014_FAIL_COST_UNIVERSALIZED" "5 USD/million cost guidance was universalized."
}
if (-not $nonmajor.NonmajorEmScandiCnhCalibrationRequired -or $nonmajor.NonmajorExecutionAuthorized) {
    Fail "EXEC_PAPER_R014_FAIL_NONMAJOR_CALIBRATION" "Nonmajor calibration requirement was weakened."
}
if (-not $usdjpy.RequiresInversion -or $usdjpy.SecurityID -ne 4004 -or [string]$usdjpy.SecurityIDSource -ne "8") {
    Fail "EXEC_PAPER_R014_FAIL_USDJPY_CAVEAT" "USDJPY caveat was weakened."
}

foreach ($auditName in @(
    "phase-exec-paper-r014-no-broker-activation-audit.json",
    "phase-exec-paper-r014-no-live-marketdata-audit.json",
    "phase-exec-paper-r014-no-scheduler-service-polling-audit.json",
    "phase-exec-paper-r014-no-executable-schedule-audit.json",
    "phase-exec-paper-r014-no-child-slices-audit.json",
    "phase-exec-paper-r014-no-child-orders-audit.json",
    "phase-exec-paper-r014-no-order-created-audit.json",
    "phase-exec-paper-r014-no-real-fill-audit.json",
    "phase-exec-paper-r014-no-execution-report-audit.json",
    "phase-exec-paper-r014-no-route-no-submission-audit.json",
    "phase-exec-paper-r014-no-paper-ledger-commit-audit.json",
    "phase-exec-paper-r014-no-polygon-api-call-audit.json",
    "phase-exec-paper-r014-no-lmax-call-audit.json",
    "phase-exec-paper-r014-no-external-api-call-audit.json"
)) {
    $audit = Read-Json (Join-Path $ArtifactsRoot $auditName)
    if (-not $audit.Passed -or $audit.Occurred) {
        Fail "EXEC_PAPER_R014_FAIL_AUDIT" "Forbidden action audit failed: $auditName"
    }
}

if (-not $noExternal.NoExternal -or
    $noExternal.PolygonCalled -or
    $noExternal.LmaxCalled -or
    $noExternal.ExternalApiCalled -or
    $noExternal.DownloadsExecuted -or
    $forbidden.ForbiddenActionsDetected -or
    $forbidden.BrokerActivation -or
    $forbidden.LiveMarketData -or
    $forbidden.SchedulerServicePolling -or
    $forbidden.ExecutableSchedule -or
    $forbidden.ChildSlicesOrOrders -or
    $forbidden.OrdersFillsReportsRoutesSubmissions -or
    $forbidden.PaperLedgerCommit -or
    $forbidden.StateMutation -or
    $forbidden.R009ExecutablePromotion) {
    Fail "EXEC_PAPER_R014_FAIL_NO_EXTERNAL_AUDIT" "No-external or forbidden-action audit failed."
}

if ($evidence.DotnetBuild -ne "Passed" -or
    $evidence.FocusedR014Tests -ne "Passed" -or
    $evidence.UnitTests -ne "Passed" -or
    $evidence.R014Validator -ne "Passed" -or
    -not $evidence.EvidenceComplete) {
    Fail "EXEC_PAPER_R014_FAIL_BUILD_TEST_VALIDATOR_EVIDENCE" "Build/tests/validator evidence is missing."
}

Write-Output "EXEC_PAPER_R014_VALIDATION_PASSED"
