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
        Fail "EXEC_PAPER_R012_FAIL_MISSING_ARTIFACT" "Missing required artifact: $path"
    }
    return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function As-Array($value) {
    if ($null -eq $value) { return @() }
    if ($value -is [System.Array]) { return $value }
    return @($value)
}

$requiredArtifacts = @(
    "phase-exec-paper-r012-summary.md",
    "phase-exec-paper-r012-r059-plan-reference.json",
    "phase-exec-paper-r012-r009-contract-reference.json",
    "phase-exec-paper-r012-aggregatedweights-source-analysis.json",
    "phase-exec-paper-r012-selected-legacy-groups.json",
    "phase-exec-paper-r012-bar-role-selection-results.json",
    "phase-exec-paper-r012-generated-fixture-inventory.json",
    "phase-exec-paper-r012-generated-fixture-validation.json",
    "phase-exec-paper-r012-batch-manifest.json",
    "phase-exec-paper-r012-batch-manifest-validation.json",
    "phase-exec-paper-r012-manual-noexternal-command-plan.json",
    "phase-exec-paper-r012-command-safety-check.json",
    "phase-exec-paper-r012-batch-execution-result.json",
    "phase-exec-paper-r012-output-artifact-inventory.json",
    "phase-exec-paper-r012-paper-plan-lines-aggregate.json",
    "phase-exec-paper-r012-r009-handoff-package-aggregate.json",
    "phase-exec-paper-r012-r009-design-only-preview-lines.json",
    "phase-exec-paper-r012-preview-line-coverage.json",
    "phase-exec-paper-r012-held-line-diagnostics.json",
    "phase-exec-paper-r012-operator-review-report.md",
    "phase-exec-paper-r012-operator-review-report.json",
    "phase-exec-paper-r012-preview-decision.json",
    "phase-exec-paper-r012-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-paper-r012-legacy-compatibility-preservation.json",
    "phase-exec-paper-r012-usd-pair-normalization-preservation.json",
    "phase-exec-paper-r012-direct-cross-exclusion-preservation.json",
    "phase-exec-paper-r012-cost-guidance-preservation.json",
    "phase-exec-paper-r012-nonmajor-calibration-preservation.json",
    "phase-exec-paper-r012-no-broker-activation-audit.json",
    "phase-exec-paper-r012-no-live-marketdata-audit.json",
    "phase-exec-paper-r012-no-scheduler-service-polling-audit.json",
    "phase-exec-paper-r012-no-executable-schedule-audit.json",
    "phase-exec-paper-r012-no-child-slices-audit.json",
    "phase-exec-paper-r012-no-child-orders-audit.json",
    "phase-exec-paper-r012-no-order-created-audit.json",
    "phase-exec-paper-r012-no-real-fill-audit.json",
    "phase-exec-paper-r012-no-execution-report-audit.json",
    "phase-exec-paper-r012-no-route-no-submission-audit.json",
    "phase-exec-paper-r012-no-paper-ledger-commit-audit.json",
    "phase-exec-paper-r012-no-polygon-api-call-audit.json",
    "phase-exec-paper-r012-no-lmax-call-audit.json",
    "phase-exec-paper-r012-no-external-api-call-audit.json",
    "phase-exec-paper-r012-usdjpy-caveat-preservation.json",
    "phase-exec-paper-r012-lmax-readonly-baseline-reference.json",
    "phase-exec-paper-r012-no-external-audit.json",
    "phase-exec-paper-r012-forbidden-actions-audit.json",
    "phase-exec-paper-r012-next-phase-recommendation.json",
    "phase-exec-paper-r012-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsRoot $artifact))) {
        Fail "EXEC_PAPER_R012_FAIL_MISSING_ARTIFACT" "Missing required artifact: $artifact"
    }
}

$r059 = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-r059-plan-reference.json")
$contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-r009-contract-reference.json")
$source = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-aggregatedweights-source-analysis.json")
$selected = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-selected-legacy-groups.json")
$barRoles = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-bar-role-selection-results.json")
$fixtureInventory = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-generated-fixture-inventory.json")
$fixtureValidation = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-generated-fixture-validation.json")
$manifest = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-batch-manifest.json")
$manifestValidation = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-batch-manifest-validation.json")
$commandPlan = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-manual-noexternal-command-plan.json")
$safety = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-command-safety-check.json")
$execution = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-batch-execution-result.json")
$artifactInventory = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-output-artifact-inventory.json")
$paperLines = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-paper-plan-lines-aggregate.json")
$handoff = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-r009-handoff-package-aggregate.json")
$preview = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-r009-design-only-preview-lines.json")
$coverage = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-preview-line-coverage.json")
$held = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-held-line-diagnostics.json")
$review = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-operator-review-report.json")
$decision = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-preview-decision.json")
$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-canonical-quarter-hour-policy-preservation.json")
$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-legacy-compatibility-preservation.json")
$usdPair = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-usd-pair-normalization-preservation.json")
$directCross = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-direct-cross-exclusion-preservation.json")
$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-cost-guidance-preservation.json")
$nonmajor = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-nonmajor-calibration-preservation.json")
$usdjpy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-usdjpy-caveat-preservation.json")
$noExternal = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r012-build-test-validator-evidence.json")

if ($r059.RecommendedMinimumTargetCloses -lt 30 -or -not $r059.PlanningOnlySource) {
    Fail "EXEC_PAPER_R012_FAIL_R059_REFERENCE_INVALID" "R059 plan reference is missing or not planning-only."
}

if ($contract.ContractVersion -ne "0.3.0-design-only-candidate" -or
    -not $contract.DesignOnly -or -not $contract.PaperOnly -or -not $contract.NonExecutable -or
    -not $contract.NotAnOrder -or -not $contract.NotSubmitted -or -not $contract.NoBrokerRoute -or
    $contract.ExecutablePromotionAuthorized -or $contract.BrokerReady -or $contract.LiveReady) {
    Fail "EXEC_PAPER_R012_FAIL_R009_PROMOTED_TO_EXECUTABLE" "R009 contract is executable or weakened."
}

if (-not $source.Exists -or $source.HeaderColumnCount -ne 91 -or $source.TickerMappingCount -ne 91 -or $source.CommandsExecutedDuringExtraction) {
    Fail "EXEC_PAPER_R012_FAIL_AGGREGATEDWEIGHTS_SOURCE_ANALYSIS" "AggregatedWeights source analysis is invalid."
}

if ($selected.SelectedGroupCount -ne 30 -or $selected.SelectionStatus -ne "SelectedBalanced30Groups") {
    Fail "EXEC_PAPER_R012_FAIL_SELECTED_GROUPS" "Expected 30 selected legacy groups."
}
if (-not $barRoles.Balanced) {
    Fail "EXEC_PAPER_R012_FAIL_BAR_ROLE_BALANCE" "Bar-role selection is not balanced."
}
foreach ($role in @("OpeningBuild", "IntradayRebalance", "ClosingFlatten")) {
    $entry = (As-Array $barRoles.Results) | Where-Object { $_.BarRole -eq $role } | Select-Object -First 1
    if ($null -eq $entry -or $entry.BatchEntryCount -ne 10) {
        Fail "EXEC_PAPER_R012_FAIL_BAR_ROLE_BALANCE" "Expected 10 entries for $role."
    }
}

if ($fixtureInventory.FixtureCount -ne 30 -or $fixtureInventory.ExpectedFixtureCount -ne 30) {
    Fail "EXEC_PAPER_R012_FAIL_FIXTURE_INVENTORY" "Expected 30 generated fixtures."
}
foreach ($fixture in (As-Array $fixtureInventory.Inventory)) {
    if ($fixture.RowCount -ne 91 -or $fixture.ContainsTimestampRows) {
        Fail "EXEC_PAPER_R012_FAIL_FIXTURE_ROWS_INVALID" "Generated fixture includes invalid rows: $($fixture.FixturePath)"
    }
}
if (-not $fixtureValidation.AllFixturesValid -or $fixtureValidation.ValidFixtureCount -ne 30) {
    Fail "EXEC_PAPER_R012_FAIL_FIXTURE_VALIDATION" "Generated fixture validation failed."
}

if ($manifest.BatchEntryCount -ne 30 -or
    $manifest.ManifestStatus -ne "FullBalancedBatchReady" -or
    -not $manifest.CandidateDefinitionNeedsOperatorConfirmation -or
    $manifest.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_PAPER_R012_FAIL_BATCH_MANIFEST" "Batch manifest is incomplete or uses legacy timestamps."
}
if (-not $manifestValidation.AllEntriesValid -or
    -not $manifestValidation.TargetClosesCanonicalQuarterHour -or
    $manifestValidation.Legacy06UsedAsFutureCanonical -or
    -not $manifestValidation.NoPaperLedgerCommitPreserved) {
    Fail "EXEC_PAPER_R012_FAIL_BATCH_MANIFEST_VALIDATION" "Batch manifest validation failed."
}

if ($commandPlan.CommandCount -ne 30 -or $commandPlan.CommandsExecutedByPlanGeneration) {
    Fail "EXEC_PAPER_R012_FAIL_COMMAND_PLAN" "Command plan is missing or executed commands before safety validation."
}
foreach ($command in (As-Array $commandPlan.Commands)) {
    $line = [string]$command.CommandLine
    if ($line -notmatch "--mode ManualNoExternal" -or
        $line -notmatch "--output-artifacts-dir" -or
        $line -notmatch "--requested-cycle-run-id" -or
        $line -notmatch "--qubes-run-id" -or
        $line -notmatch "--qubes-fixture-path" -or
        $line -notmatch "--expected-cadence-minutes 15" -or
        $line -notmatch "--no-paper-ledger-commit true" -or
        $line -match "--mode no-external-paper-cycle" -or
        $line -match "\s--output\s") {
        Fail "EXEC_PAPER_R012_FAIL_COMMAND_PLAN" "Unsafe or incomplete command plan entry."
    }
}

if (-not $safety.SafetyValidatedBeforeExecution -or
    -not $safety.AllCommandsSafe -or
    $safety.AcceptedBatchEntryCount -ne 30 -or
    $safety.CommandCount -ne 30 -or
    $safety.UnsafeReasonCount -ne 0) {
    Fail "EXEC_PAPER_R012_FAIL_COMMAND_SAFETY" "Command safety validation failed."
}

if ($execution.CommandsExecuted -ne 30 -or
    $execution.AcceptedBatchEntries -ne 30 -or
    $execution.MoreCommandsThanAcceptedEntries -or
    -not $execution.AllRunsCompletedSafely -or
    -not $execution.NoExternal -or
    -not $execution.NoPaperLedgerCommit -or
    -not $execution.NoOrderFillReportRouteSubmission) {
    Fail "EXEC_PAPER_R012_FAIL_BATCH_EXECUTION" "Batch execution result is unsafe."
}
foreach ($result in (As-Array $execution.Results)) {
    if ($result.ExitCode -ne 0 -or
        $result.Stdout -ne "CompletedNoExternal" -or
        $result.CycleExecutionCount -ne 1 -or
        $result.LineCount -ne 7 -or
        -not $result.NoExternal -or
        -not $result.NoPaperLedgerCommit -or
        -not $result.NoOrder -or
        -not $result.NoFill -or
        -not $result.NoReport -or
        -not $result.NoRoute -or
        -not $result.NoSubmission -or
        -not $result.CompletedSafely) {
        Fail "EXEC_PAPER_R012_FAIL_BATCH_EXECUTION" "Unsafe run result for $($result.BatchEntryId)."
    }
}

if ($artifactInventory.RunCount -ne 30) {
    Fail "EXEC_PAPER_R012_FAIL_OUTPUT_ARTIFACT_INVENTORY" "Output artifact inventory does not cover 30 runs."
}
foreach ($item in (As-Array $artifactInventory.Inventory)) {
    if (-not $item.SummaryArtifactExists -or -not $item.PaperExecutionPlanArtifactExists -or -not $item.PaperExecutionPlanLinesArtifactExists) {
        Fail "EXEC_PAPER_R012_FAIL_OUTPUT_ARTIFACT_INVENTORY" "Missing run output artifact for $($item.BatchEntryId)."
    }
}

if ($paperLines.LineCount -ne 210 -or $paperLines.ExpectedMaximumLineCount -ne 210) {
    Fail "EXEC_PAPER_R012_FAIL_PAPER_PLAN_LINES" "Expected 210 paper execution plan lines."
}
foreach ($line in (As-Array $paperLines.Lines)) {
    if (-not $line.NonExecutable -or -not $line.NotAnOrder -or -not $line.NotSubmitted -or -not $line.NoBrokerRoute -or
        -not $line.NoChildSlices -or -not $line.NoExecutableSchedule -or -not $line.NoFill -or
        -not $line.NoExecutionReport -or -not $line.NoRoute -or -not $line.NoSubmission -or -not $line.NoPaperLedgerCommit -or
        $line.DirectCrossExecutableLine -or -not $line.CanonicalQuarterHourTimestampConfirmed) {
        Fail "EXEC_PAPER_R012_FAIL_PREVIEW_LINES_UNSAFE" "Paper plan line is unsafe."
    }
}

if ($handoff.HandoffLineCount -ne 210 -or -not $handoff.HandoffReady) {
    Fail "EXEC_PAPER_R012_FAIL_HANDOFF_PACKAGE" "R009 handoff package is incomplete."
}
if ($preview.PreviewLineCount -ne 210 -or
    $preview.ExpectedMaximumPreviewLineCount -ne 210 -or
    -not $preview.PreviewReady) {
    Fail "EXEC_PAPER_R012_FAIL_PREVIEW_LINES" "R009 preview lines are incomplete."
}
foreach ($line in (As-Array $preview.Lines)) {
    if (-not $line.DesignOnlyPreview -or
        -not $line.NonExecutable -or -not $line.NotAnOrder -or -not $line.NotSubmitted -or -not $line.NoBrokerRoute -or
        -not $line.NoChildSlices -or -not $line.NoExecutableSchedule -or -not $line.NoFill -or
        -not $line.NoExecutionReport -or -not $line.NoRoute -or -not $line.NoSubmission -or -not $line.NoPaperLedgerCommit -or
        $null -eq $line.QuoteWindowReadinessBinding -or
        $null -eq $line.CloseBenchmarkReadinessBinding -or
        $null -eq $line.FeedQualityReadinessBinding -or
        -not [string]::IsNullOrWhiteSpace([string]$line.HoldReason)) {
        Fail "EXEC_PAPER_R012_FAIL_PREVIEW_LINES_UNSAFE" "Preview line is unsafe or missing readiness."
    }
}

if ($coverage.BatchEntryCount -ne 30 -or
    $coverage.PaperPlanLineCount -ne 210 -or
    $coverage.PreviewLineCount -ne 210 -or
    $coverage.HeldLineCount -ne 0 -or
    $coverage.DirectCrossExecutableLineCount -ne 0 -or
    $coverage.CompleteReadinessBindingCount -ne 210) {
    Fail "EXEC_PAPER_R012_FAIL_PREVIEW_COVERAGE" "Preview line coverage is incomplete."
}
if ($held.HeldLineCount -ne 0) {
    Fail "EXEC_PAPER_R012_FAIL_HELD_LINES" "Held lines are present."
}

if (-not $review.TargetClosesBalancedAcrossBarRoles -or
    $review.GeneratedFixtureCount -ne 30 -or
    $review.SafeManualNoExternalCommandsRun -ne 30 -or
    $review.PaperExecutionPlanLinesEmitted -ne 210 -or
    $review.R009PreviewLinesProduced -ne 210 -or
    -not $review.DirectCrossesExcludedAfterNetting -or
    -not $review.InversionsSafe -or
    -not $review.USDJPYCaveatPreserved -or
    $review.CompleteReadinessBindings -ne 210 -or
    $review.HeldLines -ne 0 -or
    -not $review.R009StableAcrossExpandedBarRoleBatch) {
    Fail "EXEC_PAPER_R012_FAIL_OPERATOR_REVIEW" "Operator review does not support full success."
}

if ($decision.Decision -ne "AcceptBalancedBarRolePaperOnlyPreviewForMaturityReview" -or
    -not $decision.FullBalancedBatchReady -or
    -not $decision.R009NextStagePreviewReady -or
    -not $decision.BarRoleBalanced -or
    $decision.ExecutablePromotionAuthorized) {
    Fail "EXEC_PAPER_R012_FAIL_PREVIEW_DECISION" "Preview decision is missing or executable."
}
foreach ($classification in @(
    "EXEC_PAPER_R012_PASS_NEXT_STAGE_FIXTURE_BATCH_READY_NO_EXTERNAL",
    "EXEC_PAPER_R012_PASS_MANUAL_NOEXTERNAL_BATCH_RUNS_READY_NO_EXTERNAL",
    "EXEC_PAPER_R012_PASS_R009_NEXT_STAGE_PREVIEW_READY_NO_EXTERNAL",
    "EXEC_PAPER_R012_PASS_BAR_ROLE_BALANCED_REVIEW_READY_NO_EXTERNAL",
    "EXEC_PAPER_R012_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
)) {
    if ((As-Array $decision.Classifications) -notcontains $classification) {
        Fail "EXEC_PAPER_R012_FAIL_CLASSIFICATION_MISSING" "Missing classification: $classification"
    }
}

if (-not $canonical.FutureTimestampsUseCanonicalQuarterHour -or $canonical.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_PAPER_R012_FAIL_LEGACY_06_USED_AS_FUTURE_CANONICAL" "Canonical quarter-hour policy is weakened."
}
if (-not $legacy.LegacyTimestampsCompatibilityOnly -or $legacy.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_PAPER_R012_FAIL_LEGACY_06_USED_AS_FUTURE_CANONICAL" "Legacy compatibility is weakened."
}
if (-not $usdPair.USDPairOnlyAfterNetting -or -not $usdPair.AUDUSDNotFailed) {
    Fail "EXEC_PAPER_R012_FAIL_AUDUSD_MISCLASSIFIED" "USD-pair normalization or AUDUSD status is weakened."
}
if (-not $directCross.DirectCrossesSignalOnly -or -not $directCross.DirectCrossNettingFirst -or -not $directCross.DirectCrossExecutionDisabled -or $directCross.ExclusionWeakened -or $directCross.DirectCrossExecutableLineCount -ne 0) {
    Fail "EXEC_PAPER_R012_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct-cross exclusion is weakened."
}
if ($cost.FiveUsdPerMillionUniversalized -or $cost.FiveUsdPerMillion -ne "BestCaseMajorOnly" -or -not $cost.NonmajorCalibrationRequired) {
    Fail "EXEC_PAPER_R012_FAIL_COST_GUIDANCE_UNIVERSALIZED" "Cost guidance is weakened."
}
if (-not $nonmajor.NonmajorEmScandiCnhCalibrationRequired -or $nonmajor.NonmajorExecutionAuthorized) {
    Fail "EXEC_PAPER_R012_FAIL_NONMAJOR_CALIBRATION_WEAKENED" "Nonmajor calibration is weakened."
}
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or
    $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or
    -not $usdjpy.RequiresInversion -or
    $usdjpy.SecurityID -ne 4004 -or
    [string]$usdjpy.SecurityIDSource -ne "8" -or
    $usdjpy.CaveatWeakened) {
    Fail "EXEC_PAPER_R012_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat is weakened."
}

foreach ($auditName in @(
    "phase-exec-paper-r012-no-broker-activation-audit.json",
    "phase-exec-paper-r012-no-live-marketdata-audit.json",
    "phase-exec-paper-r012-no-scheduler-service-polling-audit.json",
    "phase-exec-paper-r012-no-executable-schedule-audit.json",
    "phase-exec-paper-r012-no-child-slices-audit.json",
    "phase-exec-paper-r012-no-child-orders-audit.json",
    "phase-exec-paper-r012-no-order-created-audit.json",
    "phase-exec-paper-r012-no-real-fill-audit.json",
    "phase-exec-paper-r012-no-execution-report-audit.json",
    "phase-exec-paper-r012-no-route-no-submission-audit.json",
    "phase-exec-paper-r012-no-paper-ledger-commit-audit.json",
    "phase-exec-paper-r012-no-polygon-api-call-audit.json",
    "phase-exec-paper-r012-no-lmax-call-audit.json",
    "phase-exec-paper-r012-no-external-api-call-audit.json"
)) {
    $audit = Read-Json (Join-Path $ArtifactsRoot $auditName)
    if (-not $audit.Passed -or $audit.Occurred) {
        Fail "EXEC_PAPER_R012_FAIL_FORBIDDEN_ACTION_DETECTED" "Audit failed: $auditName"
    }
}

if (-not $noExternal.NoExternal -or $noExternal.PolygonCalled -or $noExternal.LmaxCalled -or $noExternal.ExternalApiCalled -or $noExternal.DownloadsExecuted) {
    Fail "EXEC_PAPER_R012_FAIL_EXTERNAL_API_CALLED" "No-external audit failed."
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
    Fail "EXEC_PAPER_R012_FAIL_FORBIDDEN_ACTION_DETECTED" "Forbidden action audit failed."
}

if ($evidence.DotnetBuild -ne "Passed" -or
    $evidence.FocusedR012Tests -ne "Passed" -or
    $evidence.UnitTests -ne "Passed" -or
    $evidence.R012Validator -ne "Passed" -or
    -not $evidence.EvidenceComplete) {
    Fail "EXEC_PAPER_R012_FAIL_BUILD_TEST_VALIDATOR_EVIDENCE_MISSING" "Build/tests/validator evidence missing or incomplete."
}

Write-Host "EXEC-PAPER-R012 validation passed"
