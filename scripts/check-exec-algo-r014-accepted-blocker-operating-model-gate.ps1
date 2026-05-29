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
        Fail "EXEC_ALGO_R014_FAIL_MISSING_ARTIFACT" "Missing required artifact: $path"
    }
    return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function As-Array($value) {
    if ($null -eq $value) { return @() }
    if ($value -is [System.Array]) { return $value }
    return @($value)
}

$requiredArtifacts = @(
    "phase-exec-algo-r014-summary.md",
    "phase-exec-algo-r014-r019-continuation-reference.json",
    "phase-exec-algo-r014-r013-accepted-blocker-reference.json",
    "phase-exec-algo-r014-r061-programme-reference.json",
    "phase-exec-algo-r014-r009-contract-reference.json",
    "phase-exec-algo-r014-accepted-blocker-operating-model-contract.json",
    "phase-exec-algo-r014-accepted-blocker-operating-model-result.json",
    "phase-exec-algo-r014-held-readiness-semantics.json",
    "phase-exec-algo-r014-line-status-model.json",
    "phase-exec-algo-r014-paper-only-continuation-rules.json",
    "phase-exec-algo-r014-safety-failure-rules.json",
    "phase-exec-algo-r014-reporting-requirements.json",
    "phase-exec-algo-r014-operator-action-options.json",
    "phase-exec-algo-r014-executable-promotion-blockers.json",
    "phase-exec-algo-r014-no-executable-promotion-preservation.json",
    "phase-exec-algo-r014-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-algo-r014-legacy-compatibility-preservation.json",
    "phase-exec-algo-r014-usd-pair-normalization-preservation.json",
    "phase-exec-algo-r014-direct-cross-exclusion-preservation.json",
    "phase-exec-algo-r014-cost-guidance-preservation.json",
    "phase-exec-algo-r014-nonmajor-calibration-preservation.json",
    "phase-exec-algo-r014-usdjpy-caveat-preservation.json",
    "phase-exec-algo-r014-no-broker-activation-audit.json",
    "phase-exec-algo-r014-no-live-marketdata-audit.json",
    "phase-exec-algo-r014-no-scheduler-service-polling-audit.json",
    "phase-exec-algo-r014-no-new-pms-cycle-audit.json",
    "phase-exec-algo-r014-no-manualnoexternal-command-run-audit.json",
    "phase-exec-algo-r014-no-db-import-audit.json",
    "phase-exec-algo-r014-no-persisted-sanitized-row-audit.json",
    "phase-exec-algo-r014-no-new-backtest-audit.json",
    "phase-exec-algo-r014-no-new-simulation-audit.json",
    "phase-exec-algo-r014-no-tca-result-lines-audit.json",
    "phase-exec-algo-r014-no-executable-schedule-audit.json",
    "phase-exec-algo-r014-no-child-slices-audit.json",
    "phase-exec-algo-r014-no-child-orders-audit.json",
    "phase-exec-algo-r014-no-order-created-audit.json",
    "phase-exec-algo-r014-no-real-fill-audit.json",
    "phase-exec-algo-r014-no-execution-report-audit.json",
    "phase-exec-algo-r014-no-route-no-submission-audit.json",
    "phase-exec-algo-r014-no-paper-ledger-commit-audit.json",
    "phase-exec-algo-r014-no-polygon-api-call-audit.json",
    "phase-exec-algo-r014-no-lmax-call-audit.json",
    "phase-exec-algo-r014-no-external-api-call-audit.json",
    "phase-exec-algo-r014-no-external-audit.json",
    "phase-exec-algo-r014-forbidden-actions-audit.json",
    "phase-exec-algo-r014-next-phase-recommendation.json",
    "phase-exec-algo-r014-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsRoot $artifact))) {
        Fail "EXEC_ALGO_R014_FAIL_MISSING_ARTIFACT" "Missing required artifact: $artifact"
    }
}

$r019 = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r014-r019-continuation-reference.json")
$r013 = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r014-r013-accepted-blocker-reference.json")
$r061 = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r014-r061-programme-reference.json")
$contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r014-r009-contract-reference.json")
$modelContract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r014-accepted-blocker-operating-model-contract.json")
$result = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r014-accepted-blocker-operating-model-result.json")
$semantics = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r014-held-readiness-semantics.json")
$lineStatus = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r014-line-status-model.json")
$continuation = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r014-paper-only-continuation-rules.json")
$safety = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r014-safety-failure-rules.json")
$reporting = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r014-reporting-requirements.json")
$operator = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r014-operator-action-options.json")
$execBlockers = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r014-executable-promotion-blockers.json")
$noPromotion = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r014-no-executable-promotion-preservation.json")
$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r014-canonical-quarter-hour-policy-preservation.json")
$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r014-legacy-compatibility-preservation.json")
$usdPair = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r014-usd-pair-normalization-preservation.json")
$directCross = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r014-direct-cross-exclusion-preservation.json")
$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r014-cost-guidance-preservation.json")
$nonmajor = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r014-nonmajor-calibration-preservation.json")
$usdjpy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r014-usdjpy-caveat-preservation.json")
$noExternal = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r014-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r014-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r014-build-test-validator-evidence.json")

if ($r019.SourcePhase -ne "EXEC-PAPER-R019" -or
    $r019.Decision -ne "R009PaperOnlyContinuationStableWithHeldReadiness" -or
    -not $r019.AcceptedBlockerCarried -or
    $r019.MissingReadinessBlocksWholeBatch -or
    $r019.ExecutablePromotionAuthorized -or
    $r019.FixtureCount -ne 50 -or
    $r019.ManualNoExternalCommandsRun -ne 50 -or
    $r019.R009PreviewLinesProduced -ne 350 -or
    $r019.ReadinessCompleteLineCount -ne 161 -or
    $r019.HeldLineCount -ne 189 -or
    $r019.HeldMissingReadinessCount -ne 189 -or
    $r019.MissingReadinessTreatedAsBatchFailure -or
    $r019.MissingReadinessTreatedAsR009LogicFailure -or
    -not $r019.DirectCrossesExcludedAfterNetting -or
    -not $r019.InversionsSafe) {
    Fail "EXEC_ALGO_R014_FAIL_R019_REFERENCE" "R019 continuation reference is invalid."
}

if ($r013.SourcePhase -ne "EXEC-ALGO-R013" -or
    $r013.ExplicitBlocker -ne "LocalMarketDataReadinessIncompleteFor56PreviewLines" -or
    $r013.BlockerType -ne "ReadinessOnly" -or
    -not $r013.NotR009LogicFailure -or
    -not $r013.NotExecutablePathIssue -or
    $r013.ExecutablePromotionAuthorized) {
    Fail "EXEC_ALGO_R014_FAIL_R013_REFERENCE" "R013 accepted-blocker reference is invalid."
}

if ($r061.SourcePhase -ne "EXEC-SIM-R061" -or
    $r061.ResidualBlocker -ne "LocalMarketDataReadinessIncompleteFor56PreviewLines" -or
    -not $r061.ResidualBlockerIsReadinessOnly -or
    $r061.ExecutablePromotionAuthorized) {
    Fail "EXEC_ALGO_R014_FAIL_R061_REFERENCE" "R061 programme reference is invalid."
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
    Fail "EXEC_ALGO_R014_FAIL_R009_CONTRACT" "R009 contract was widened or promoted."
}

if (-not $modelContract.GovernanceOnly -or
    $modelContract.CommandsRunInThisGate -or
    $modelContract.DownloadsAllowed -or
    $modelContract.ExternalApiAllowed -or
    $modelContract.BrokerRuntimeAllowed -or
    $modelContract.LiveMarketDataAllowed -or
    $modelContract.SchedulerServicePollingAllowed -or
    $modelContract.PmsEmsOmsCycleAllowed -or
    $modelContract.TcaResultLineCreationAllowed -or
    $modelContract.ExecutableScheduleAllowed -or
    $modelContract.OrderFillRouteSubmissionAllowed -or
    $modelContract.PaperLedgerCommitAllowed -or
    $modelContract.StateMutationAllowed -or
    $modelContract.R009ExecutablePromotionAllowed -or
    -not $modelContract.HeldReadinessAcceptedForPaperOnlyContinuation -or
    -not $modelContract.HeldReadinessMayNotAuthorizeOrders -or
    -not $modelContract.PartialReadinessMustNotBeRepresentedAsFull) {
    Fail "EXEC_ALGO_R014_FAIL_OPERATING_MODEL_CONTRACT" "Operating model contract is unsafe."
}

foreach ($classification in @(
    "EXEC_ALGO_R014_PASS_ACCEPTED_BLOCKER_OPERATING_MODEL_READY_NO_EXTERNAL",
    "EXEC_ALGO_R014_PASS_HELD_READINESS_SEMANTICS_READY_NO_EXTERNAL",
    "EXEC_ALGO_R014_PASS_EXECUTABLE_PROMOTION_BLOCKERS_READY_NO_EXTERNAL",
    "EXEC_ALGO_R014_PASS_NO_EXECUTABLE_PROMOTION_NO_ORDER_GATE_READY_NO_EXTERNAL"
)) {
    if ($classification -notin (As-Array $result.Classifications)) {
        Fail "EXEC_ALGO_R014_FAIL_CLASSIFICATION" "Missing expected classification: $classification"
    }
}

if (-not $result.OperatingModelReady -or
    "R009AcceptedBlockerPaperOnlyOperatingModelReady" -notin (As-Array $result.DecisionStatuses) -or
    "HeldReadinessAcceptedAsPaperOnlyCondition" -notin (As-Array $result.DecisionStatuses) -or
    "ExecutablePromotionBlocked" -notin (As-Array $result.DecisionStatuses) -or
    $result.R019PreviewLineCount -ne 350 -or
    $result.R019ReadinessCompleteLineCount -ne 161 -or
    $result.R019HeldReadinessLineCount -ne 189 -or
    -not $result.HeldReadinessAcceptedAsPaperOnlyCondition -or
    $result.HeldReadinessMisclassifiedAsR009Failure -or
    $result.HeldReadinessTreatedAsExecutablePermission -or
    $result.FullReadinessCompletenessClaimed -or
    -not $result.ExecutablePromotionBlocked -or
    $result.ExecutablePromotionAuthorized -or
    $result.BrokerReady -or
    $result.LiveReady) {
    Fail "EXEC_ALGO_R014_FAIL_OPERATING_MODEL_RESULT" "Operating model result is invalid."
}

if ($semantics.HeldReadinessStatus -ne "HeldMissingReadiness" -or
    $semantics.ReadinessMissingEqualsR009Failure -or
    $semantics.ReadinessMissingAuthorizesOrders -or
    -not $semantics.HeldLineRemainsNonExecutable) {
    Fail "EXEC_ALGO_R014_FAIL_HELD_READINESS_SEMANTICS" "HeldReadiness semantics are unsafe."
}

$statusNames = As-Array $lineStatus.Statuses | ForEach-Object { $_.Status }
foreach ($requiredStatus in @("PreviewReady", "HeldMissingReadiness", "HeldUnsupportedInstrument", "HeldDirectCrossNotNetted", "HeldInversionMismatch", "HeldRiskOperatorMissing", "InconclusiveSafe")) {
    if ($requiredStatus -notin $statusNames) {
        Fail "EXEC_ALGO_R014_FAIL_LINE_STATUS_MODEL" "Missing line status: $requiredStatus"
    }
}
if ($lineStatus.AnyStatusCanCreateOrder) {
    Fail "EXEC_ALGO_R014_FAIL_LINE_STATUS_EXECUTABLE" "Line status model can create orders."
}
foreach ($status in (As-Array $lineStatus.Statuses)) {
    if ($status.Executable) {
        Fail "EXEC_ALGO_R014_FAIL_LINE_STATUS_EXECUTABLE" "$($status.Status) is marked executable."
    }
}

if (-not $continuation.ContinuePaperOnlyBatchesWithHeldMissingReadiness -or
    -not $continuation.ReportHeldLinesExplicitly -or
    $continuation.MissingReadinessBlocksWholeBatch -or
    -not $continuation.SafetyFailureBlocksWholeBatch -or
    -not $continuation.DirectCrossFailureBlocksContinuation -or
    -not $continuation.InversionFailureBlocksContinuation -or
    -not $continuation.ExecutablePathHardFailure -or
    -not $continuation.NoOrderNoFillNoRouteNoLedger) {
    Fail "EXEC_ALGO_R014_FAIL_CONTINUATION_RULES" "Paper-only continuation rules are unsafe."
}

if (-not $safety.OrderFillRouteSubmissionScheduleLedgerStatePathHardFailure -or
    -not $safety.BrokerLiveSchedulerPathHardFailure -or
    -not $safety.Legacy06FutureCanonicalHardFailure -or
    -not $safety.R009ExecutablePromotionHardFailure -or
    $safety.HeldMissingReadinessHardFailure) {
    Fail "EXEC_ALGO_R014_FAIL_SAFETY_RULES" "Safety failure rules are unsafe."
}

foreach ($metric in @("TotalPreviewLines", "ReadinessCompleteLines", "HeldLines", "HeldByReason", "HeldBySymbol", "HeldByBarRole", "DirectCrossExecutableCount", "InversionFailures", "USDJPYCaveatStatus", "NoOrderNoFillNoRouteNoLedgerAudit")) {
    if ($metric -notin (As-Array $reporting.RequiredMetrics)) {
        Fail "EXEC_ALGO_R014_FAIL_REPORTING_REQUIREMENTS" "Missing reporting metric: $metric"
    }
}
if ($reporting.R019Baseline.TotalPreviewLines -ne 350 -or
    $reporting.R019Baseline.ReadinessCompleteLines -ne 161 -or
    $reporting.R019Baseline.HeldLines -ne 189 -or
    $reporting.R019Baseline.DirectCrossExecutableCount -ne 0 -or
    $reporting.R019Baseline.InversionFailures -ne 0) {
    Fail "EXEC_ALGO_R014_FAIL_REPORTING_BASELINE" "Reporting baseline is invalid."
}

if ((As-Array $operator.Options).Count -ne 4 -or
    $operator.RecommendedDefault -ne "A" -or
    -not $operator.ExecutablePromotionStillBlocked) {
    Fail "EXEC_ALGO_R014_FAIL_OPERATOR_OPTIONS" "Operator action options are incomplete."
}
foreach ($option in (As-Array $operator.Options)) {
    if ($option.ExecutionEnabled) {
        Fail "EXEC_ALGO_R014_FAIL_OPERATOR_OPTION_EXECUTION" "Operator option $($option.Option) enables execution."
    }
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
    "HeldReadinessDoesNotAuthorizeExecution",
    "SeparateExplicitExecutableGateRequiredIfEverConsidered"
)) {
    if ($requiredBlocker -notin (As-Array $execBlockers.Blockers)) {
        Fail "EXEC_ALGO_R014_FAIL_EXECUTABLE_BLOCKER_MISSING" "Missing executable blocker: $requiredBlocker"
    }
}
if (-not $execBlockers.ExecutablePromotionBlocked) {
    Fail "EXEC_ALGO_R014_FAIL_EXECUTABLE_BLOCKERS" "Executable promotion is not blocked."
}

if ($noPromotion.ExecutablePromotionAuthorized -or
    $noPromotion.BrokerReady -or
    $noPromotion.LiveReady -or
    $noPromotion.OrderCreationAuthorized -or
    $noPromotion.RouteSubmissionAuthorized -or
    $noPromotion.PaperLedgerCommitAuthorized) {
    Fail "EXEC_ALGO_R014_FAIL_EXECUTABLE_PROMOTION" "Executable promotion was authorized."
}

if (-not $canonical.FutureTimestampsUseCanonicalQuarterHour -or $canonical.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_ALGO_R014_FAIL_CANONICAL_POLICY" "Canonical quarter-hour policy was weakened."
}
if (-not $legacy.LegacyTimestampsCompatibilityOnly -or $legacy.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_ALGO_R014_FAIL_LEGACY_POLICY" "Legacy compatibility policy was weakened."
}
if (-not $usdPair.USDPairOnlyAfterNetting -or -not $usdPair.AUDUSDNotFailed -or "AUDUSD" -notin (As-Array $usdPair.ExecutionSymbols)) {
    Fail "EXEC_ALGO_R014_FAIL_USD_PAIR_OR_AUDUSD" "USD-pair normalization or AUDUSD classification is invalid."
}
if (-not $directCross.DirectCrossesSignalOnly -or
    -not $directCross.DirectCrossNettingFirst -or
    -not $directCross.DirectCrossExecutionDisabled -or
    $directCross.ExclusionWeakened) {
    Fail "EXEC_ALGO_R014_FAIL_DIRECT_CROSS_EXCLUSION" "Direct-cross exclusion was weakened."
}
if ($cost.FiveUsdPerMillion -ne "BestCaseMajorOnly" -or $cost.FiveUsdPerMillionUniversalized) {
    Fail "EXEC_ALGO_R014_FAIL_COST_GUIDANCE" "5 USD/million guidance was universalized."
}
if (-not $nonmajor.NonmajorEmScandiCnhCalibrationRequired -or $nonmajor.NonmajorExecutionAuthorized) {
    Fail "EXEC_ALGO_R014_FAIL_NONMAJOR_CALIBRATION" "Nonmajor calibration requirement was weakened."
}
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or
    $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or
    -not $usdjpy.RequiresInversion -or
    $usdjpy.SecurityID -ne 4004 -or
    $usdjpy.SecurityIDSource -ne "8" -or
    $usdjpy.CaveatWeakened) {
    Fail "EXEC_ALGO_R014_FAIL_USDJPY_CAVEAT" "USDJPY caveat was weakened."
}

if (-not $noExternal.NoExternal -or
    $noExternal.PolygonCalled -or
    $noExternal.LmaxCalled -or
    $noExternal.ExternalApiCalled -or
    $noExternal.DownloadsExecuted -or
    $noExternal.BrokerActivated -or
    $noExternal.LiveMarketDataRequested -or
    $noExternal.SchedulerServicePollingStarted) {
    Fail "EXEC_ALGO_R014_FAIL_NO_EXTERNAL_AUDIT" "No-external audit failed."
}

if ($forbidden.ForbiddenActionsDetected -or
    $forbidden.DownloadsExecuted -or
    $forbidden.BrokerActivation -or
    $forbidden.LiveMarketData -or
    $forbidden.SchedulerServicePolling -or
    $forbidden.PmsEmsOmsCycleRun -or
    $forbidden.ManualNoExternalCommandRun -or
    $forbidden.DbImport -or
    $forbidden.PersistedSanitizedRows -or
    $forbidden.BacktestOrSimulation -or
    $forbidden.TcaResultLines -or
    $forbidden.ExecutableSchedule -or
    $forbidden.ChildSlicesOrOrders -or
    $forbidden.OrdersFillsReportsRoutesSubmissions -or
    $forbidden.PaperLedgerCommit -or
    $forbidden.StateMutation -or
    $forbidden.R009ExecutablePromotion) {
    Fail "EXEC_ALGO_R014_FAIL_FORBIDDEN_ACTIONS" "Forbidden action audit failed."
}

foreach ($status in @($evidence.Build.Status, $evidence.FocusedTests.Status, $evidence.UnitTests.Status, $evidence.Validator.Status)) {
    if ($status -ne "Passed") {
        Fail "EXEC_ALGO_R014_FAIL_BUILD_TEST_VALIDATOR_EVIDENCE" "Build/tests/validator evidence missing or not passed."
    }
}

Write-Output "EXEC_ALGO_R014_PASS_ACCEPTED_BLOCKER_OPERATING_MODEL_READY_NO_EXTERNAL"
Write-Output "EXEC_ALGO_R014_PASS_HELD_READINESS_SEMANTICS_READY_NO_EXTERNAL"
Write-Output "EXEC_ALGO_R014_PASS_EXECUTABLE_PROMOTION_BLOCKERS_READY_NO_EXTERNAL"
Write-Output "EXEC_ALGO_R014_PASS_NO_EXECUTABLE_PROMOTION_NO_ORDER_GATE_READY_NO_EXTERNAL"
