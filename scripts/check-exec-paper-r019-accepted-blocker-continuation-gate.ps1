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
        Fail "EXEC_PAPER_R019_FAIL_MISSING_ARTIFACT" "Missing required artifact: $path"
    }
    return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function As-Array($value) {
    if ($null -eq $value) { return @() }
    if ($value -is [System.Array]) { return $value }
    return @($value)
}

$requiredArtifacts = @(
    "phase-exec-paper-r019-summary.md",
    "phase-exec-paper-r019-r061-programme-reference.json",
    "phase-exec-paper-r019-r013-blocker-acceptance-reference.json",
    "phase-exec-paper-r019-r009-contract-reference.json",
    "phase-exec-paper-r019-accepted-blocker-context.json",
    "phase-exec-paper-r019-generated-fixture-inventory.json",
    "phase-exec-paper-r019-generated-fixture-validation.json",
    "phase-exec-paper-r019-batch-manifest.json",
    "phase-exec-paper-r019-batch-manifest-validation.json",
    "phase-exec-paper-r019-command-safety-check.json",
    "phase-exec-paper-r019-batch-execution-result.json",
    "phase-exec-paper-r019-paper-plan-lines-aggregate.json",
    "phase-exec-paper-r019-r009-design-only-preview-lines.json",
    "phase-exec-paper-r019-preview-line-coverage.json",
    "phase-exec-paper-r019-held-readiness-diagnostics.json",
    "phase-exec-paper-r019-bar-role-coverage-review.json",
    "phase-exec-paper-r019-per-symbol-coverage-review.json",
    "phase-exec-paper-r019-direct-cross-netting-review.json",
    "phase-exec-paper-r019-inversion-review.json",
    "phase-exec-paper-r019-operator-review-report.md",
    "phase-exec-paper-r019-operator-review-report.json",
    "phase-exec-paper-r019-continuation-decision.json",
    "phase-exec-paper-r019-executable-promotion-blockers.json",
    "phase-exec-paper-r019-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-paper-r019-legacy-compatibility-preservation.json",
    "phase-exec-paper-r019-usd-pair-normalization-preservation.json",
    "phase-exec-paper-r019-direct-cross-exclusion-preservation.json",
    "phase-exec-paper-r019-cost-guidance-preservation.json",
    "phase-exec-paper-r019-nonmajor-calibration-preservation.json",
    "phase-exec-paper-r019-no-broker-activation-audit.json",
    "phase-exec-paper-r019-no-live-marketdata-audit.json",
    "phase-exec-paper-r019-no-scheduler-service-polling-audit.json",
    "phase-exec-paper-r019-no-executable-schedule-audit.json",
    "phase-exec-paper-r019-no-child-slices-audit.json",
    "phase-exec-paper-r019-no-child-orders-audit.json",
    "phase-exec-paper-r019-no-order-created-audit.json",
    "phase-exec-paper-r019-no-real-fill-audit.json",
    "phase-exec-paper-r019-no-execution-report-audit.json",
    "phase-exec-paper-r019-no-route-no-submission-audit.json",
    "phase-exec-paper-r019-no-paper-ledger-commit-audit.json",
    "phase-exec-paper-r019-no-polygon-api-call-audit.json",
    "phase-exec-paper-r019-no-lmax-call-audit.json",
    "phase-exec-paper-r019-no-external-api-call-audit.json",
    "phase-exec-paper-r019-usdjpy-caveat-preservation.json",
    "phase-exec-paper-r019-lmax-readonly-baseline-reference.json",
    "phase-exec-paper-r019-no-external-audit.json",
    "phase-exec-paper-r019-forbidden-actions-audit.json",
    "phase-exec-paper-r019-next-phase-recommendation.json",
    "phase-exec-paper-r019-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsRoot $artifact))) {
        Fail "EXEC_PAPER_R019_FAIL_MISSING_ARTIFACT" "Missing required artifact: $artifact"
    }
}

$r061 = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-r061-programme-reference.json")
$r013 = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-r013-blocker-acceptance-reference.json")
$contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-r009-contract-reference.json")
$context = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-accepted-blocker-context.json")
$fixtureInventory = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-generated-fixture-inventory.json")
$fixtureValidation = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-generated-fixture-validation.json")
$manifest = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-batch-manifest.json")
$manifestValidation = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-batch-manifest-validation.json")
$safety = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-command-safety-check.json")
$execution = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-batch-execution-result.json")
$paperLines = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-paper-plan-lines-aggregate.json")
$previewLines = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-r009-design-only-preview-lines.json")
$coverage = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-preview-line-coverage.json")
$held = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-held-readiness-diagnostics.json")
$barRole = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-bar-role-coverage-review.json")
$symbol = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-per-symbol-coverage-review.json")
$directCross = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-direct-cross-netting-review.json")
$inversion = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-inversion-review.json")
$review = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-operator-review-report.json")
$decision = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-continuation-decision.json")
$blockers = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-executable-promotion-blockers.json")
$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-canonical-quarter-hour-policy-preservation.json")
$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-legacy-compatibility-preservation.json")
$usdPair = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-usd-pair-normalization-preservation.json")
$directCrossPreservation = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-direct-cross-exclusion-preservation.json")
$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-cost-guidance-preservation.json")
$nonmajor = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-nonmajor-calibration-preservation.json")
$usdjpy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-usdjpy-caveat-preservation.json")
$lmax = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-lmax-readonly-baseline-reference.json")
$noExternal = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r019-build-test-validator-evidence.json")

if ($r061.SourcePhase -ne "EXEC-SIM-R061" -or
    $r061.R009Status -ne "R009AcceptedForLongRunPaperOnlyEvaluationWithExplicitReadinessBlocker" -or
    $r061.ResidualBlocker -ne "LocalMarketDataReadinessIncompleteFor56PreviewLines" -or
    -not $r061.ResidualBlockerIsReadinessOnly) {
    Fail "EXEC_PAPER_R019_FAIL_R061_CONTEXT" "R061 accepted-blocker context is invalid."
}

if ($r013.SourcePhase -ne "EXEC-ALGO-R013" -or
    $r013.ExplicitBlocker -ne "LocalMarketDataReadinessIncompleteFor56PreviewLines" -or
    $r013.ExecutablePromotionAuthorized) {
    Fail "EXEC_PAPER_R019_FAIL_R013_ACCEPTANCE" "R013 blocker acceptance reference is invalid."
}

if ($contract.ContractVersion -ne "0.3.0-design-only-candidate" -or
    -not $contract.DesignOnly -or
    -not $contract.PaperOnly -or
    -not $contract.NonExecutable -or
    -not $contract.NotAnOrder -or
    -not $contract.NotSubmitted -or
    -not $contract.NoBrokerRoute -or
    $contract.BrokerReady -or
    $contract.LiveReady -or
    $contract.ExecutablePromotionAuthorized) {
    Fail "EXEC_PAPER_R019_FAIL_R009_CONTRACT" "R009 contract is executable or widened."
}

if (-not $context.AcceptedBlockerContextLoaded -or
    $context.AcceptedBlocker -ne "LocalMarketDataReadinessIncompleteFor56PreviewLines" -or
    -not $context.MissingReadinessMayHoldLines -or
    $context.MissingReadinessBlocksWholeBatch -or
    -not $context.NotDirectCrossIssue -or
    -not $context.NotInversionIssue -or
    -not $context.NotUsdJpyCaveatIssue -or
    -not $context.NotR009LogicIssue -or
    -not $context.NotExecutablePathIssue -or
    -not $context.ExecutablePromotionBlocked) {
    Fail "EXEC_PAPER_R019_FAIL_ACCEPTED_BLOCKER_CONTEXT" "Accepted readiness blocker is omitted or misclassified."
}

if ($fixtureInventory.FixtureCount -ne 50 -or
    $fixtureValidation.FixtureCount -ne 50 -or
    $fixtureValidation.ValidFixtureCount -ne 50 -or
    $fixtureValidation.InvalidFixtureCount -ne 0) {
    Fail "EXEC_PAPER_R019_FAIL_FIXTURES" "Fixture generation/validation counts are invalid."
}
foreach ($result in (As-Array $fixtureValidation.Results)) {
    if (-not $result.Valid -or
        -not $result.Exists -or
        -not $result.NonEmpty -or
        $result.RowCount -ne 91 -or
        $result.ContainsTimestampRows -or
        $result.InvalidRowCount -ne 0) {
        Fail "EXEC_PAPER_R019_FAIL_FIXTURE_FORMAT" "Invalid fixture row format in $($result.FixturePath)."
    }
}

if ($manifest.Entries.Count -ne 50 -or
    $manifest.AcceptedReadinessBlocker -ne "LocalMarketDataReadinessIncompleteFor56PreviewLines" -or
    $manifestValidation.EntryCount -ne 50 -or
    $manifestValidation.IssueCount -ne 0 -or
    -not $manifestValidation.CanonicalQuarterHourTargetCloses -or
    -not $manifestValidation.NoPaperLedgerCommitAllTrue) {
    Fail "EXEC_PAPER_R019_FAIL_MANIFEST" "Batch manifest validation failed."
}

foreach ($entry in (As-Array $manifest.Entries)) {
    if ($entry.CanonicalTargetCloseLocal -match "T\d{2}:(06|21|36|51):00" -or
        $entry.CanonicalTargetCloseLocal -notmatch "T\d{2}:(00|15|30|45):00" -or
        -not $entry.NoPaperLedgerCommit -or
        [string]::IsNullOrWhiteSpace([string]$entry.BarRole) -or
        $entry.AcceptedReadinessBlockerCarried -ne "LocalMarketDataReadinessIncompleteFor56PreviewLines") {
        Fail "EXEC_PAPER_R019_FAIL_MANIFEST_ENTRY" "Invalid manifest entry $($entry.BatchEntryId)."
    }
}

if (-not $safety.SafetyValidatedBeforeExecution -or
    $safety.AcceptedBatchEntries -ne 50 -or
    $safety.CommandCount -ne 50 -or
    -not $safety.AllCommandsSafe -or
    $safety.UnsafeReasonCount -ne 0) {
    Fail "EXEC_PAPER_R019_FAIL_COMMAND_SAFETY" "Command safety validation failed."
}
foreach ($check in (As-Array $safety.Checks)) {
    if (-not $check.Safe -or
        -not $check.UsesManualNoExternal -or
        -not $check.IncludesOutputArtifactsDir -or
        -not $check.IncludesRequestedCycleRunId -or
        -not $check.IncludesQubesRunId -or
        -not $check.IncludesQubesFixturePath -or
        -not $check.IncludesCadence15 -or
        -not $check.IncludesNoPaperLedgerCommitTrue -or
        $check.DeprecatedModeUsed -or
        $check.DeprecatedOutputUsed -or
        $check.BrokerLiveOrderRouteSubmissionFlagsPresent) {
        Fail "EXEC_PAPER_R019_FAIL_COMMAND_FLAGS" "Unsafe command flags for $($check.BatchEntryId)."
    }
}
foreach ($command in (As-Array $safety.Commands)) {
    if ([string]$command.CommandLine -notmatch "--no-paper-ledger-commit true" -or
        [string]$command.CommandLine -notmatch "--mode ManualNoExternal" -or
        [string]$command.CommandLine -match "--mode no-external-paper-cycle" -or
        [string]$command.CommandLine -match "\s--output\s" -or
        [string]$command.CommandLine -match "--(broker|live|order|route|submit|fill|scheduler|service|poll)") {
        Fail "EXEC_PAPER_R019_FAIL_COMMAND_TEMPLATE" "Command template unsafe for $($command.BatchEntryId)."
    }
}

if ($execution.CommandsExecuted -ne 50 -or
    $execution.AcceptedBatchEntries -ne 50 -or
    $execution.MoreCommandsThanAcceptedEntries -or
    -not $execution.AllRunsCompletedSafely -or
    -not $execution.NoExternal -or
    -not $execution.NoBroker -or
    -not $execution.NoLiveMarketData -or
    -not $execution.NoPaperLedgerCommit -or
    -not $execution.NoOrderFillReportRouteSubmission) {
    Fail "EXEC_PAPER_R019_FAIL_EXECUTION_RESULT" "Batch execution result is unsafe."
}
foreach ($result in (As-Array $execution.Results)) {
    if ($result.ExitCode -ne 0 -or
        $result.LineCount -ne 7 -or
        -not $result.CompletedSafely -or
        -not $result.NoExternal -or
        -not $result.NoBroker -or
        -not $result.NoLiveMarketData -or
        -not $result.NoPaperLedgerCommit -or
        -not $result.NoOrder -or
        -not $result.NoFill -or
        -not $result.NoReport -or
        -not $result.NoRoute -or
        -not $result.NoSubmission) {
        Fail "EXEC_PAPER_R019_FAIL_RUN_RESULT" "Unsafe run result for $($result.BatchEntryId)."
    }
}

if ($paperLines.LineCount -ne 350 -or
    $previewLines.PreviewLineCount -ne 350 -or
    $coverage.BatchEntryCount -ne 50 -or
    $coverage.PaperPlanLineCount -ne 350 -or
    $coverage.PreviewLineCount -ne 350 -or
    $coverage.ExpectedMaximumPreviewLineCount -ne 350 -or
    $coverage.ReadinessCompleteLineCount -lt 0 -or
    $coverage.HeldLineCount -lt 0 -or
    ($coverage.ReadinessCompleteLineCount + $coverage.HeldLineCount) -ne 350 -or
    $coverage.DirectCrossExecutableLineCount -ne 0 -or
    $coverage.InversionFailureCount -ne 0) {
    Fail "EXEC_PAPER_R019_FAIL_PREVIEW_COVERAGE" "Preview coverage counts are invalid."
}

foreach ($line in (As-Array $previewLines.Lines)) {
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
        -not $line.NoPaperLedgerCommit -or
        $line.CanonicalTargetCloseLocal -match "T\d{2}:(06|21|36|51):00") {
        Fail "EXEC_PAPER_R019_FAIL_PREVIEW_LINE_FLAGS" "Preview line is executable or non-canonical."
    }
}

if ($held.HeldLineCount -ne $coverage.HeldLineCount -or
    $held.HeldMissingReadinessCount -ne $held.HeldLineCount -or
    $held.MissingReadinessTreatedAsR009LogicFailure) {
    Fail "EXEC_PAPER_R019_FAIL_HELD_READINESS" "Held readiness diagnostics are invalid or misclassified."
}
foreach ($heldLine in (As-Array $held.HeldLines)) {
    if ($heldLine.HoldReason -ne "HeldMissingReadiness") {
        Fail "EXEC_PAPER_R019_FAIL_HELD_REASON" "Held line reason is not HeldMissingReadiness."
    }
}

if ($barRole.Coverage.Count -lt 1 -or $symbol.Coverage.Count -ne 7) {
    Fail "EXEC_PAPER_R019_FAIL_COVERAGE_REVIEWS" "Bar-role or per-symbol coverage review is incomplete."
}

if ($directCross.DirectCrossExecutableLineCount -ne 0 -or
    -not $directCross.DirectCrossesExcludedAfterNetting -or
    -not $inversion.InversionsSafe -or
    -not $inversion.USDJPYCaveatPreserved -or
    $inversion.InversionFailureCount -ne 0) {
    Fail "EXEC_PAPER_R019_FAIL_DIRECT_CROSS_OR_INVERSION" "Direct-cross or inversion review failed."
}

if ($review.Decision -ne "R009PaperOnlyContinuationStableWithHeldReadiness" -or
    $review.ManualNoExternalCommandsRun -ne 50 -or
    $review.R009PreviewLinesProduced -ne 350 -or
    $review.HeldLineCount -ne $coverage.HeldLineCount -or
    $review.MissingReadinessTreatedAsBatchFailure -or
    $review.MissingReadinessTreatedAsR009LogicFailure -or
    $review.ExecutablePromotionAuthorized) {
    Fail "EXEC_PAPER_R019_FAIL_OPERATOR_REVIEW" "Operator review does not preserve accepted-blocker semantics."
}

if ($decision.Decision -ne "R009PaperOnlyContinuationStableWithHeldReadiness" -or
    -not $decision.AcceptedBlockerCarried -or
    $decision.MissingReadinessBlocksWholeBatch -or
    $decision.ExecutablePromotionAuthorized) {
    Fail "EXEC_PAPER_R019_FAIL_CONTINUATION_DECISION" "Continuation decision is invalid."
}
if ("EXEC_PAPER_R019_PARTIAL_CONTINUATION_PREVIEW_WITH_HELD_READINESS_NO_EXTERNAL" -notin (As-Array $decision.Classifications) -or
    "EXEC_PAPER_R019_PASS_HELD_READINESS_DIAGNOSTICS_READY_NO_EXTERNAL" -notin (As-Array $decision.Classifications) -or
    "EXEC_PAPER_R019_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL" -notin (As-Array $decision.Classifications)) {
    Fail "EXEC_PAPER_R019_FAIL_CLASSIFICATIONS" "Expected partial held-readiness classifications are missing."
}

foreach ($requiredBlocker in @(
    "NoBrokerIntegrationAuthorized",
    "NoLiveMarketDataAuthorized",
    "NoOmsOrderCreationAuthorized",
    "NoExecutableScheduleAuthorized",
    "NoChildSlicesAuthorized",
    "NoRouteSubmissionAuthorized",
    "NoFillsExecutionReportsAuthorized",
    "NoPaperLedgerCommitAuthorized",
    "NoStateMutationAuthorized",
    "NoDirectCrossExecutionAuthorized",
    "NoNonmajorEmScandiCnhExecutionWithoutCalibration",
    "AcceptedReadinessBlockerRemains",
    "SeparateExplicitExecutableGateRequiredIfEverConsidered"
)) {
    if ($requiredBlocker -notin (As-Array $blockers.Blockers)) {
        Fail "EXEC_PAPER_R019_FAIL_EXECUTABLE_BLOCKER_MISSING" "Missing executable blocker: $requiredBlocker"
    }
}
if (-not $blockers.ExecutablePromotionBlocked) {
    Fail "EXEC_PAPER_R019_FAIL_EXECUTABLE_PROMOTION_BLOCKERS" "Executable promotion is not blocked."
}

if (-not $canonical.FutureTimestampsUseCanonicalQuarterHour -or
    $canonical.Legacy06UsedAsFutureCanonical -or
    -not $legacy.LegacyTimestampsCompatibilityOnly -or
    $legacy.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_PAPER_R019_FAIL_CANONICAL_TIMING" "Canonical quarter-hour policy is weakened."
}
if (-not $usdPair.USDPairOnlyAfterNetting -or
    -not $usdPair.AUDUSDNotFailed -or
    "AUDUSD" -notin (As-Array $usdPair.ExecutionSymbols)) {
    Fail "EXEC_PAPER_R019_FAIL_USD_PAIR_OR_AUDUSD" "USD-pair normalization or AUDUSD classification is invalid."
}
if (-not $directCrossPreservation.DirectCrossesSignalOnly -or
    -not $directCrossPreservation.DirectCrossNettingFirst -or
    -not $directCrossPreservation.DirectCrossExecutionDisabled -or
    $directCrossPreservation.ExclusionWeakened -or
    $directCrossPreservation.DirectCrossExecutableLineCount -ne 0) {
    Fail "EXEC_PAPER_R019_FAIL_DIRECT_CROSS_PRESERVATION" "Direct-cross exclusion is weakened."
}
if ($cost.FiveUsdPerMillionUniversalized -or
    $cost.FiveUsdPerMillion -ne "BestCaseMajorOnly" -or
    -not $cost.NonmajorCalibrationRequired) {
    Fail "EXEC_PAPER_R019_FAIL_COST_GUIDANCE" "5 USD/million guidance is universalized."
}
if (-not $nonmajor.NonmajorEmScandiCnhCalibrationRequired -or $nonmajor.NonmajorExecutionAuthorized) {
    Fail "EXEC_PAPER_R019_FAIL_NONMAJOR_CALIBRATION" "Nonmajor calibration requirement is weakened."
}
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or
    $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or
    -not $usdjpy.RequiresInversion -or
    $usdjpy.SecurityID -ne 4004 -or
    $usdjpy.SecurityIDSource -ne "8" -or
    $usdjpy.CaveatWeakened) {
    Fail "EXEC_PAPER_R019_FAIL_USDJPY_CAVEAT" "USDJPY caveat is weakened."
}
if (-not $lmax.LmaxReferencedAsReadonlyBaselineOnly -or $lmax.LmaxCalled -or $lmax.BrokerRuntimeActivated) {
    Fail "EXEC_PAPER_R019_FAIL_LMAX_REFERENCE" "LMAX reference is not read-only."
}
if (-not $noExternal.NoExternal -or
    $noExternal.PolygonCalled -or
    $noExternal.LmaxCalled -or
    $noExternal.ExternalApiCalled -or
    $noExternal.DownloadsExecuted -or
    $noExternal.BrokerActivated -or
    $noExternal.LiveMarketDataRequested) {
    Fail "EXEC_PAPER_R019_FAIL_NO_EXTERNAL_AUDIT" "No-external audit failed."
}
if ($forbidden.ForbiddenActionsDetected -or
    $forbidden.BrokerActivation -or
    $forbidden.LiveMarketData -or
    $forbidden.SchedulerServicePolling -or
    $forbidden.ExecutableSchedule -or
    $forbidden.ChildSlicesOrOrders -or
    $forbidden.OrdersFillsReportsRoutesSubmissions -or
    $forbidden.PaperLedgerCommit -or
    $forbidden.StateMutation -or
    $forbidden.R009ExecutablePromotion) {
    Fail "EXEC_PAPER_R019_FAIL_FORBIDDEN_ACTIONS" "Forbidden action audit failed."
}

foreach ($status in @($evidence.Build.Status, $evidence.FocusedTests.Status, $evidence.UnitTests.Status, $evidence.Validator.Status)) {
    if ($status -ne "Passed") {
        Fail "EXEC_PAPER_R019_FAIL_BUILD_TEST_VALIDATOR_EVIDENCE" "Build/tests/validator evidence missing or not passed."
    }
}

Write-Output "EXEC_PAPER_R019_PARTIAL_CONTINUATION_PREVIEW_WITH_HELD_READINESS_NO_EXTERNAL"
Write-Output "EXEC_PAPER_R019_PASS_HELD_READINESS_DIAGNOSTICS_READY_NO_EXTERNAL"
Write-Output "EXEC_PAPER_R019_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
