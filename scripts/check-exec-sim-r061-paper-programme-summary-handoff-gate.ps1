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
        Fail "EXEC_SIM_R061_FAIL_MISSING_ARTIFACT" "Missing required artifact: $path"
    }

    return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function As-Array($value) {
    if ($null -eq $value) { return @() }
    if ($value -is [System.Array]) { return $value }
    return @($value)
}

$requiredArtifacts = @(
    "phase-exec-sim-r061-summary.md",
    "phase-exec-sim-r061-r013-maturity-reference.json",
    "phase-exec-sim-r061-r018-final-readiness-reference.json",
    "phase-exec-sim-r061-r009-contract-reference.json",
    "phase-exec-sim-r061-programme-summary-report.md",
    "phase-exec-sim-r061-programme-summary-report.json",
    "phase-exec-sim-r061-evidence-chain-summary.json",
    "phase-exec-sim-r061-r009-current-status.json",
    "phase-exec-sim-r061-residual-readiness-blocker-summary.json",
    "phase-exec-sim-r061-what-r009-is-not.json",
    "phase-exec-sim-r061-next-operator-action-options.json",
    "phase-exec-sim-r061-executable-promotion-blockers.json",
    "phase-exec-sim-r061-readiness-completion-checklist.json",
    "phase-exec-sim-r061-paper-only-continuation-checklist.json",
    "phase-exec-sim-r061-source-of-truth-artifact-index.json",
    "phase-exec-sim-r061-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-sim-r061-legacy-compatibility-preservation.json",
    "phase-exec-sim-r061-usd-pair-normalization-preservation.json",
    "phase-exec-sim-r061-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r061-cost-guidance-preservation.json",
    "phase-exec-sim-r061-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r061-no-broker-activation-audit.json",
    "phase-exec-sim-r061-no-live-marketdata-audit.json",
    "phase-exec-sim-r061-no-scheduler-service-polling-audit.json",
    "phase-exec-sim-r061-no-new-pms-cycle-audit.json",
    "phase-exec-sim-r061-no-manualnoexternal-command-run-audit.json",
    "phase-exec-sim-r061-no-db-import-audit.json",
    "phase-exec-sim-r061-no-persisted-sanitized-row-audit.json",
    "phase-exec-sim-r061-no-new-backtest-audit.json",
    "phase-exec-sim-r061-no-new-simulation-audit.json",
    "phase-exec-sim-r061-no-tca-result-lines-audit.json",
    "phase-exec-sim-r061-no-executable-schedule-audit.json",
    "phase-exec-sim-r061-no-child-slices-audit.json",
    "phase-exec-sim-r061-no-child-orders-audit.json",
    "phase-exec-sim-r061-no-order-created-audit.json",
    "phase-exec-sim-r061-no-real-fill-audit.json",
    "phase-exec-sim-r061-no-execution-report-audit.json",
    "phase-exec-sim-r061-no-route-no-submission-audit.json",
    "phase-exec-sim-r061-no-paper-ledger-commit-audit.json",
    "phase-exec-sim-r061-no-polygon-api-call-audit.json",
    "phase-exec-sim-r061-no-lmax-call-audit.json",
    "phase-exec-sim-r061-no-external-api-call-audit.json",
    "phase-exec-sim-r061-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r061-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r061-no-external-audit.json",
    "phase-exec-sim-r061-forbidden-actions-audit.json",
    "phase-exec-sim-r061-next-phase-recommendation.json",
    "phase-exec-sim-r061-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsRoot $artifact))) {
        Fail "EXEC_SIM_R061_FAIL_MISSING_ARTIFACT" "Missing required artifact: $artifact"
    }
}

$r013Ref = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r061-r013-maturity-reference.json")
$r018Ref = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r061-r018-final-readiness-reference.json")
$contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r061-r009-contract-reference.json")
$report = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r061-programme-summary-report.json")
$evidenceChain = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r061-evidence-chain-summary.json")
$status = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r061-r009-current-status.json")
$blocker = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r061-residual-readiness-blocker-summary.json")
$whatNot = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r061-what-r009-is-not.json")
$actions = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r061-next-operator-action-options.json")
$executableBlockers = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r061-executable-promotion-blockers.json")
$sourceIndex = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r061-source-of-truth-artifact-index.json")
$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r061-canonical-quarter-hour-policy-preservation.json")
$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r061-legacy-compatibility-preservation.json")
$usdPair = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r061-usd-pair-normalization-preservation.json")
$directCross = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r061-direct-cross-exclusion-preservation.json")
$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r061-cost-guidance-preservation.json")
$nonmajor = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r061-nonmajor-calibration-preservation.json")
$usdjpy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r061-usdjpy-caveat-preservation.json")
$lmax = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r061-lmax-readonly-baseline-reference.json")
$noExternal = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r061-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r061-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r061-build-test-validator-evidence.json")

$expectedClassifications = @(
    "EXEC_SIM_R061_PASS_PAPER_ONLY_PROGRAMME_SUMMARY_READY_NO_EXTERNAL",
    "EXEC_SIM_R061_PASS_R009_HANDOFF_DOCUMENTATION_READY_NO_EXTERNAL",
    "EXEC_SIM_R061_PASS_RESIDUAL_READINESS_BLOCKER_DOCUMENTED_NO_EXTERNAL",
    "EXEC_SIM_R061_PASS_NO_EXECUTABLE_PROMOTION_NO_ORDER_GATE_READY_NO_EXTERNAL"
)
foreach ($classification in $expectedClassifications) {
    if ($classification -notin (As-Array $report.Classifications)) {
        Fail "EXEC_SIM_R061_FAIL_CLASSIFICATION_MISSING" "Missing classification: $classification"
    }
}

if ($r013Ref.SourcePhase -ne "EXEC-ALGO-R013" -or
    $r013Ref.MaturityStatus -ne "R009AcceptedForLongRunPaperOnlyEvaluationWithExplicitReadinessBlocker" -or
    $r013Ref.ReadinessCompleteLineCount -ne 644 -or
    $r013Ref.PreviewLineCount -ne 700 -or
    $r013Ref.RemainingHeldLineCount -ne 56 -or
    $r013Ref.ExplicitBlocker -ne "LocalMarketDataReadinessIncompleteFor56PreviewLines" -or
    $r013Ref.ExecutablePromotionAuthorized -or
    -not $r013Ref.ReusedOnly) {
    Fail "EXEC_SIM_R061_FAIL_R013_REFERENCE" "R013 maturity reference is invalid."
}

if ($r018Ref.SourcePhase -ne "EXEC-PAPER-R018" -or
    $r018Ref.AcceptedLocalFileEntries -ne 28 -or
    $r018Ref.ManifestValidationAccepted -ne 28 -or
    $r018Ref.RowValidationAccepted -ne 28 -or
    $r018Ref.FinalReboundLines -ne 4 -or
    $r018Ref.FinalReadinessCompletePreviewLines -ne 644 -or
    $r018Ref.FinalStillHeldLines -ne 56 -or
    $r018Ref.Decision -ne "R009LongRunPaperOnlyPartialMaturityWithExplicitReadinessBlocker" -or
    -not $r018Ref.ReusedOnly) {
    Fail "EXEC_SIM_R061_FAIL_R018_REFERENCE" "R018 final readiness reference is invalid."
}

if ($contract.ContractVersion -ne "0.3.0-design-only-candidate" -or
    $contract.PrimaryPolicyCandidate -ne "CloseSeeking15mAdaptive_BalancedAdaptive_v0" -or
    $contract.SecondaryPolicyCandidate -ne "CloseSeeking15mAdaptive_ResidualAwareUrgency_v0" -or
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
    Fail "EXEC_SIM_R061_FAIL_R009_CONTRACT" "R009 contract is executable or widened."
}

if ($status.R009Status -ne "R009AcceptedForLongRunPaperOnlyEvaluationWithExplicitReadinessBlocker" -or
    $status.PaperOnlyMaturity -ne "R009PaperOnlyMaturityPartialButUsable" -or
    -not $status.MatureEnoughForContinuedLongRunPaperOnlyEvaluation -or
    $status.FullReadinessCompletenessClaimed -or
    $status.ReadinessCompleteLineCount -ne 644 -or
    $status.PreviewLineCount -ne 700 -or
    $status.RemainingHeldLineCount -ne 56 -or
    $status.ResidualBlocker -ne "LocalMarketDataReadinessIncompleteFor56PreviewLines" -or
    -not $status.ResidualBlockerIsReadinessOnly -or
    $status.ResidualBlockerIsR009LogicFailure -or
    $status.ExecutablePromotionAuthorized) {
    Fail "EXEC_SIM_R061_FAIL_STATUS" "R009 status misrepresents partial readiness or executable status."
}

if ($blocker.ReadinessCompleteLineCount -ne 644 -or
    $blocker.PreviewLineCount -ne 700 -or
    $blocker.RemainingHeldLineCount -ne 56 -or
    $blocker.Blocker -ne "LocalMarketDataReadinessIncompleteFor56PreviewLines" -or
    -not $blocker.NotDirectCrossIssue -or
    -not $blocker.NotInversionFailure -or
    -not $blocker.NotUsdJpyCaveatFailure -or
    -not $blocker.NotR009LogicFailure -or
    -not $blocker.NotExecutablePathIssue) {
    Fail "EXEC_SIM_R061_FAIL_READINESS_BLOCKER" "Residual readiness blocker is omitted or misclassified."
}
foreach ($symbol in @("AUDUSD", "EURUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF", "USDJPY")) {
    if ($blocker.HeldBySymbol.$symbol -ne 8) {
        Fail "EXEC_SIM_R061_FAIL_HELD_SYMBOL_COUNTS" "Held count for $symbol should be 8."
    }
}
if ($blocker.HeldByBarRole.IntradayRebalance -ne 28 -or
    $blocker.HeldByBarRole.ClosingFlatten -ne 28 -or
    $blocker.HeldByBarRole.OpeningBuild -ne 0) {
    Fail "EXEC_SIM_R061_FAIL_HELD_BAR_ROLE_COUNTS" "Held bar-role counts are invalid."
}

if (-not $whatNot.NotExecutable -or
    -not $whatNot.NotBrokerReady -or
    -not $whatNot.NotLiveReady -or
    -not $whatNot.NotOrderGenerator -or
    -not $whatNot.NotScheduler -or
    -not $whatNot.NotRouteSubmissionFillSystem -or
    -not $whatNot.NotLedgerCommitter) {
    Fail "EXEC_SIM_R061_FAIL_WHAT_R009_IS_NOT" "What-R009-is-not handoff is incomplete."
}

if ($actions.RecommendedDefault -ne "A" -or
    -not $actions.ReadinessCompletionRecommended -or
    -not $actions.NoExecutablePromotion -or
    (As-Array $actions.Options).Count -ne 4) {
    Fail "EXEC_SIM_R061_FAIL_OPERATOR_ACTION_OPTIONS" "Next operator actions are incomplete."
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
    "ExplicitReadinessBlockerRemains",
    "SeparateExplicitExecutableGateRequiredIfEverConsidered"
)) {
    if ($requiredBlocker -notin (As-Array $executableBlockers.Blockers)) {
        Fail "EXEC_SIM_R061_FAIL_EXECUTABLE_BLOCKER_MISSING" "Missing executable blocker: $requiredBlocker"
    }
}
if (-not $executableBlockers.ExecutablePromotionBlocked) {
    Fail "EXEC_SIM_R061_FAIL_EXECUTABLE_PROMOTION_BLOCKED" "Executable promotion is not blocked."
}

$sourcePhases = (As-Array $sourceIndex.SourceOfTruthArtifacts | ForEach-Object { $_.Phase })
foreach ($sourcePhase in @("EXEC-ALGO-R013", "EXEC-PAPER-R018", "EXEC-PAPER-R014", "EXEC-PAPER-R012", "EXEC-SIM-R058", "EXEC-SIM-R054")) {
    if ($sourcePhase -notin $sourcePhases) {
        Fail "EXEC_SIM_R061_FAIL_SOURCE_INDEX" "Source-of-truth index missing $sourcePhase."
    }
}

$evidencePhases = (As-Array $evidenceChain.EvidenceChain | ForEach-Object { $_.SourcePhase })
foreach ($sourcePhase in @("EXEC-SIM-R054", "EXEC-SIM-R058", "EXEC-PAPER-R012", "EXEC-PAPER-R014", "EXEC-PAPER-R018", "EXEC-ALGO-R013")) {
    if ($sourcePhase -notin $evidencePhases) {
        Fail "EXEC_SIM_R061_FAIL_EVIDENCE_CHAIN" "Evidence chain missing $sourcePhase."
    }
}

if (-not $canonical.CanonicalQuarterHourRequired -or
    $canonical.LegacyTimestampsUsedAsFutureCanonical -or
    -not $legacy.LegacyTimestampConventionsCompatibilityOnly -or
    $legacy.UsedAsFutureCanonical) {
    Fail "EXEC_SIM_R061_FAIL_CANONICAL_TIMING" "Canonical quarter-hour policy or legacy compatibility is weakened."
}

if (-not $usdPair.UsdPairOnlyAfterNetting -or
    -not $usdPair.AudUsdNotFailed -or
    "AUDUSD" -notin (As-Array $usdPair.SupportedExecutionSymbols)) {
    Fail "EXEC_SIM_R061_FAIL_USD_PAIR_OR_AUDUSD" "USD-pair normalization or AUDUSD classification is invalid."
}

if (-not $directCross.DirectCrossesSignalOnly -or
    -not $directCross.NettingFirst -or
    -not $directCross.ExecutionDisabled -or
    $directCross.DirectCrossExecutableLines -ne 0) {
    Fail "EXEC_SIM_R061_FAIL_DIRECT_CROSS_EXCLUSION" "Direct-cross exclusion is weakened."
}

if (-not $cost.FiveUsdPerMillionBestCaseMajorOnly -or
    $cost.FiveUsdPerMillionUniversalized) {
    Fail "EXEC_SIM_R061_FAIL_COST_GUIDANCE" "5 USD/million guidance is universalized."
}

if (-not $nonmajor.NonmajorEmScandiCnhCalibrationRequired -or
    $nonmajor.NonmajorExecutionAuthorized) {
    Fail "EXEC_SIM_R061_FAIL_NONMAJOR_CALIBRATION" "Nonmajor calibration requirement is weakened."
}

if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or
    $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or
    -not $usdjpy.RequiresInversion -or
    $usdjpy.SecurityID -ne "4004" -or
    $usdjpy.SecurityIDSource -ne "8" -or
    $usdjpy.CaveatWeakened) {
    Fail "EXEC_SIM_R061_FAIL_USDJPY_CAVEAT" "USDJPY caveat is weakened."
}

if (-not $lmax.LmaxReferenceOnly -or $lmax.LmaxCalled -or $lmax.BrokerRuntimeActivated) {
    Fail "EXEC_SIM_R061_FAIL_LMAX_REFERENCE" "LMAX reference is not read-only."
}

if (-not $noExternal.NoExternal -or
    $noExternal.PolygonCalled -or
    $noExternal.LmaxCalled -or
    $noExternal.ExternalApiCalled -or
    $noExternal.DownloadsExecuted -or
    $noExternal.BrokerActivated -or
    $noExternal.LiveMarketDataRequested -or
    $noExternal.SchedulerServicePollingStarted) {
    Fail "EXEC_SIM_R061_FAIL_NO_EXTERNAL_AUDIT" "No-external audit failed."
}

if ($forbidden.ForbiddenActionsOccurred -or
    $forbidden.DownloadsExecuted -or
    $forbidden.PmsEmsOmsCycleRun -or
    $forbidden.ManualNoExternalCommandRun -or
    $forbidden.BacktestOrSimulationRun -or
    $forbidden.TcaResultLinesCreated -or
    $forbidden.ExecutableScheduleCreated -or
    $forbidden.ChildSlicesCreated -or
    $forbidden.ChildOrdersCreated -or
    $forbidden.OrdersCreated -or
    $forbidden.FillsCreated -or
    $forbidden.ExecutionReportsCreated -or
    $forbidden.RoutesCreated -or
    $forbidden.SubmissionsCreated -or
    $forbidden.PaperLedgerCommitCreated -or
    $forbidden.StateMutated -or
    $forbidden.R009PromotedToExecutable) {
    Fail "EXEC_SIM_R061_FAIL_FORBIDDEN_ACTIONS" "Forbidden action audit failed."
}

if ($report.NoExternalConfirmation.PolygonCalled -or
    $report.NoExternalConfirmation.LmaxCalled -or
    $report.NoExternalConfirmation.ExternalApiCalled -or
    $report.NoExternalConfirmation.DownloadsExecuted -or
    $report.NoExternalConfirmation.BrokerActivated -or
    $report.NoExternalConfirmation.LiveMarketDataRequested -or
    $report.NoExternalConfirmation.SchedulerServicePollingStarted -or
    $report.NoExternalConfirmation.PmsEmsOmsCycleRun -or
    $report.NoExternalConfirmation.ManualNoExternalCommandRun -or
    $report.NoExternalConfirmation.BacktestOrSimulationRun -or
    $report.NoExternalConfirmation.TcaResultLinesCreated -or
    $report.NoExternalConfirmation.OrdersFillsRoutesSubmissionsCreated -or
    $report.NoExternalConfirmation.PaperLedgerCommitCreated -or
    $report.NoExternalConfirmation.StateMutated -or
    $report.NoExternalConfirmation.R009PromotedToExecutable) {
    Fail "EXEC_SIM_R061_FAIL_REPORT_NO_EXTERNAL" "Programme report no-external confirmation failed."
}

foreach ($statusName in @($evidence.Build.Status, $evidence.FocusedTests.Status, $evidence.UnitTests.Status, $evidence.Validator.Status)) {
    if ($statusName -ne "Passed") {
        Fail "EXEC_SIM_R061_FAIL_BUILD_TEST_VALIDATOR_EVIDENCE" "Build/tests/validator evidence missing or not passed."
    }
}

Write-Output "EXEC_SIM_R061_PASS_PAPER_ONLY_PROGRAMME_SUMMARY_READY_NO_EXTERNAL"
Write-Output "EXEC_SIM_R061_PASS_R009_HANDOFF_DOCUMENTATION_READY_NO_EXTERNAL"
Write-Output "EXEC_SIM_R061_PASS_RESIDUAL_READINESS_BLOCKER_DOCUMENTED_NO_EXTERNAL"
Write-Output "EXEC_SIM_R061_PASS_NO_EXECUTABLE_PROMOTION_NO_ORDER_GATE_READY_NO_EXTERNAL"
