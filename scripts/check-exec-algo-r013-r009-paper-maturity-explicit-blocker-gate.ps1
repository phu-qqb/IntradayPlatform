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
        Fail "EXEC_ALGO_R013_FAIL_MISSING_ARTIFACT" "Missing required artifact: $path"
    }
    return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function As-Array($value) {
    if ($null -eq $value) { return @() }
    if ($value -is [System.Array]) { return $value }
    return @($value)
}

$requiredArtifacts = @(
    "phase-exec-algo-r013-summary.md",
    "phase-exec-algo-r013-r018-final-maturity-reference.json",
    "phase-exec-algo-r013-r009-contract-reference.json",
    "phase-exec-algo-r013-long-run-paper-maturity-acceptance-contract.json",
    "phase-exec-algo-r013-long-run-paper-maturity-acceptance-result.json",
    "phase-exec-algo-r013-readiness-completion-summary.json",
    "phase-exec-algo-r013-explicit-readiness-blocker-taxonomy.json",
    "phase-exec-algo-r013-remaining-held-line-summary.json",
    "phase-exec-algo-r013-non-r009-failure-confirmation.json",
    "phase-exec-algo-r013-next-stage-data-completion-requirements.json",
    "phase-exec-algo-r013-long-run-paper-continuation-requirements.json",
    "phase-exec-algo-r013-executable-promotion-blockers.json",
    "phase-exec-algo-r013-risk-operator-review-requirements.json",
    "phase-exec-algo-r013-no-executable-promotion-preservation.json",
    "phase-exec-algo-r013-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-algo-r013-legacy-compatibility-preservation.json",
    "phase-exec-algo-r013-usd-pair-normalization-preservation.json",
    "phase-exec-algo-r013-direct-cross-exclusion-preservation.json",
    "phase-exec-algo-r013-cost-guidance-preservation.json",
    "phase-exec-algo-r013-nonmajor-calibration-preservation.json",
    "phase-exec-algo-r013-usdjpy-caveat-preservation.json",
    "phase-exec-algo-r013-no-broker-activation-audit.json",
    "phase-exec-algo-r013-no-live-marketdata-audit.json",
    "phase-exec-algo-r013-no-scheduler-service-polling-audit.json",
    "phase-exec-algo-r013-no-new-pms-cycle-audit.json",
    "phase-exec-algo-r013-no-manualnoexternal-command-run-audit.json",
    "phase-exec-algo-r013-no-new-backtest-audit.json",
    "phase-exec-algo-r013-no-new-simulation-audit.json",
    "phase-exec-algo-r013-no-tca-result-lines-audit.json",
    "phase-exec-algo-r013-no-executable-schedule-audit.json",
    "phase-exec-algo-r013-no-child-slices-audit.json",
    "phase-exec-algo-r013-no-child-orders-audit.json",
    "phase-exec-algo-r013-no-order-created-audit.json",
    "phase-exec-algo-r013-no-real-fill-audit.json",
    "phase-exec-algo-r013-no-execution-report-audit.json",
    "phase-exec-algo-r013-no-route-no-submission-audit.json",
    "phase-exec-algo-r013-no-paper-ledger-commit-audit.json",
    "phase-exec-algo-r013-no-polygon-api-call-audit.json",
    "phase-exec-algo-r013-no-lmax-call-audit.json",
    "phase-exec-algo-r013-no-external-api-call-audit.json",
    "phase-exec-algo-r013-no-external-audit.json",
    "phase-exec-algo-r013-forbidden-actions-audit.json",
    "phase-exec-algo-r013-next-phase-recommendation.json",
    "phase-exec-algo-r013-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsRoot $artifact))) {
        Fail "EXEC_ALGO_R013_FAIL_MISSING_ARTIFACT" "Missing required artifact: $artifact"
    }
}

$r018 = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r013-r018-final-maturity-reference.json")
$contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r013-r009-contract-reference.json")
$acceptanceContract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r013-long-run-paper-maturity-acceptance-contract.json")
$acceptance = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r013-long-run-paper-maturity-acceptance-result.json")
$readiness = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r013-readiness-completion-summary.json")
$blocker = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r013-explicit-readiness-blocker-taxonomy.json")
$held = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r013-remaining-held-line-summary.json")
$nonFailure = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r013-non-r009-failure-confirmation.json")
$dataReq = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r013-next-stage-data-completion-requirements.json")
$continuation = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r013-long-run-paper-continuation-requirements.json")
$execBlockers = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r013-executable-promotion-blockers.json")
$risk = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r013-risk-operator-review-requirements.json")
$noPromotion = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r013-no-executable-promotion-preservation.json")
$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r013-canonical-quarter-hour-policy-preservation.json")
$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r013-legacy-compatibility-preservation.json")
$usdPair = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r013-usd-pair-normalization-preservation.json")
$directCross = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r013-direct-cross-exclusion-preservation.json")
$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r013-cost-guidance-preservation.json")
$nonmajor = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r013-nonmajor-calibration-preservation.json")
$usdjpy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r013-usdjpy-caveat-preservation.json")
$noExternal = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r013-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r013-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r013-build-test-validator-evidence.json")

if ($r018.SourcePhase -ne "EXEC-PAPER-R018" -or
    $r018.Decision -ne "R009LongRunPaperOnlyPartialMaturityWithExplicitReadinessBlocker" -or
    $r018.ReadinessCompleteLineCount -ne 644 -or
    $r018.PreviewLineCount -ne 700 -or
    $r018.FinalStillHeldLineCount -ne 56 -or
    $r018.ExecutablePromotionAuthorized) {
    Fail "EXEC_ALGO_R013_FAIL_R018_REFERENCE" "R018 final maturity reference is invalid."
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
    Fail "EXEC_ALGO_R013_FAIL_R009_CONTRACT" "R009 contract was widened or promoted."
}

if ($acceptanceContract.AcceptanceScope -ne "ContinuedLongRunPaperOnlyEvaluation" -or
    -not $acceptanceContract.MustRecordPartialReadiness -or
    -not $acceptanceContract.MustRecordExplicitReadinessBlocker -or
    $acceptanceContract.FullReadinessCompletenessClaimAllowed -or
    $acceptanceContract.ExecutableReadinessClaimAllowed -or
    $acceptanceContract.BrokerReadinessClaimAllowed -or
    $acceptanceContract.LiveReadinessClaimAllowed -or
    $acceptanceContract.OrderFillRouteScheduleLedgerAuthorizationAllowed -or
    -not $acceptanceContract.NonExecutableAcceptanceOnly) {
    Fail "EXEC_ALGO_R013_FAIL_ACCEPTANCE_CONTRACT" "Acceptance contract is unsafe."
}

if ($acceptance.MaturityStatus -ne "R009AcceptedForLongRunPaperOnlyEvaluationWithExplicitReadinessBlocker" -or
    $acceptance.PaperOnlyMaturityStatus -ne "R009PaperOnlyMaturityPartialButUsable" -or
    $acceptance.ReadinessCompleteLineCount -ne 644 -or
    $acceptance.PreviewLineCount -ne 700 -or
    $acceptance.FinalStillHeldLineCount -ne 56 -or
    $acceptance.ExplicitBlocker -ne "LocalMarketDataReadinessIncompleteFor56PreviewLines" -or
    -not $acceptance.ReadinessCompletionRecommended -or
    -not $acceptance.ExecutablePromotionBlocked -or
    $acceptance.ExecutablePromotionAuthorized) {
    Fail "EXEC_ALGO_R013_FAIL_ACCEPTANCE_RESULT" "Maturity acceptance result is invalid."
}
foreach ($classification in @(
    "EXEC_ALGO_R013_PASS_R009_LONG_RUN_PAPER_MATURITY_ACCEPTED_WITH_READINESS_BLOCKER_NO_EXTERNAL",
    "EXEC_ALGO_R013_PASS_EXPLICIT_READINESS_BLOCKER_RECORDED_NO_EXTERNAL",
    "EXEC_ALGO_R013_PASS_EXECUTABLE_PROMOTION_BLOCKERS_READY_NO_EXTERNAL",
    "EXEC_ALGO_R013_PASS_NO_EXECUTABLE_PROMOTION_NO_ORDER_GATE_READY_NO_EXTERNAL"
)) {
    if ((As-Array $acceptance.Classifications) -notcontains $classification) {
        Fail "EXEC_ALGO_R013_FAIL_CLASSIFICATION" "Missing expected classification: $classification"
    }
}

if ($readiness.PreviewLineCount -ne 700 -or
    $readiness.ReadinessCompleteLineCount -ne 644 -or
    $readiness.StillHeldLineCount -ne 56 -or
    $readiness.FullReadinessCompletenessClaimed) {
    Fail "EXEC_ALGO_R013_FAIL_READINESS_SUMMARY" "Readiness summary misrepresents partial maturity as full."
}

if ($blocker.Blocker -ne "LocalMarketDataReadinessIncompleteFor56PreviewLines" -or
    $blocker.BlockerType -ne "ReadinessOnly" -or
    -not $blocker.NotDirectCrossExecutionIssue -or
    -not $blocker.NotInversionIssue -or
    -not $blocker.NotUsdJpyCaveatIssue -or
    -not $blocker.NotR009LogicFailure -or
    -not $blocker.NotExecutablePathIssue) {
    Fail "EXEC_ALGO_R013_FAIL_BLOCKER_TAXONOMY" "Readiness blocker is omitted or misclassified."
}

if ($held.HeldLineCount -ne 56 -or -not $held.AllHeldLinesReadinessOnly) {
    Fail "EXEC_ALGO_R013_FAIL_HELD_SUMMARY" "Remaining held-line summary is invalid."
}

if ($nonFailure.R009LogicFailure -or
    $nonFailure.DirectCrossExecutionIssue -or
    $nonFailure.InversionFailure -or
    $nonFailure.UsdJpyCaveatFailure -or
    $nonFailure.ExecutablePathIssue -or
    $nonFailure.PreviewLinesRepresentedAsOrdersSchedulesFillsRoutes) {
    Fail "EXEC_ALGO_R013_FAIL_NON_R009_FAILURE_CONFIRMATION" "Residual readiness blocker was misclassified as a strategy/executable failure."
}

if (-not $dataReq.RequiredForFullReadinessCompleteness -or
    -not $dataReq.RequiredBeforeAnyFutureExecutableDiscussion -or
    -not $continuation.ContinueManualNoExternalOnly -or
    -not $continuation.RequireNoOrderNoFillNoRouteNoLedger) {
    Fail "EXEC_ALGO_R013_FAIL_CONTINUATION_REQUIREMENTS" "Continuation/data completion requirements are unsafe."
}

if (-not $execBlockers.ExecutablePromotionBlocked -or
    (As-Array $execBlockers.Blockers).Count -lt 10 -or
    -not $risk.MustNotAuthorizeExecution -or
    -not $risk.MustNotAuthorizeOrders -or
    -not $risk.MustNotAuthorizeRoutes -or
    -not $risk.MustNotAuthorizeLedgerCommit -or
    -not $risk.MustRecordResidualReadinessBlocker) {
    Fail "EXEC_ALGO_R013_FAIL_EXECUTABLE_BLOCKERS" "Executable blockers or risk/operator requirements are incomplete."
}

if ($noPromotion.ExecutablePromotionAuthorized -or
    $noPromotion.BrokerReady -or
    $noPromotion.LiveReady -or
    $noPromotion.OrderCreationAuthorized -or
    $noPromotion.RouteSubmissionAuthorized -or
    $noPromotion.PaperLedgerCommitAuthorized) {
    Fail "EXEC_ALGO_R013_FAIL_EXECUTABLE_PROMOTION" "Executable promotion was authorized."
}

if (-not $canonical.FutureTimestampsUseCanonicalQuarterHour -or $canonical.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_ALGO_R013_FAIL_CANONICAL_POLICY" "Canonical quarter-hour policy was weakened."
}
if (-not $legacy.LegacyTimestampsCompatibilityOnly -or $legacy.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_ALGO_R013_FAIL_LEGACY_POLICY" "Legacy compatibility policy was weakened."
}
if (-not $usdPair.USDPairOnlyAfterNetting -or $usdPair.DirectCrossExecutionAllowed) {
    Fail "EXEC_ALGO_R013_FAIL_USD_PAIR_POLICY" "USD-pair-only policy was weakened."
}
if (-not $directCross.DirectCrossesSignalOnly -or -not $directCross.DirectCrossExecutionDisabled) {
    Fail "EXEC_ALGO_R013_FAIL_DIRECT_CROSS_POLICY" "Direct-cross exclusion was weakened."
}
if ($cost.FiveUsdPerMillionUniversalized) {
    Fail "EXEC_ALGO_R013_FAIL_COST_UNIVERSALIZED" "5 USD/million was universalized."
}
if (-not $nonmajor.NonmajorEmScandiCnhCalibrationRequired -or $nonmajor.NonmajorExecutionAuthorized) {
    Fail "EXEC_ALGO_R013_FAIL_NONMAJOR_CALIBRATION" "Nonmajor calibration requirement was weakened."
}
if (-not $usdjpy.RequiresInversion -or $usdjpy.SecurityID -ne 4004 -or [string]$usdjpy.SecurityIDSource -ne "8" -or $usdjpy.CaveatWeakened) {
    Fail "EXEC_ALGO_R013_FAIL_USDJPY_CAVEAT" "USDJPY caveat was weakened."
}

foreach ($auditName in @(
    "phase-exec-algo-r013-no-broker-activation-audit.json",
    "phase-exec-algo-r013-no-live-marketdata-audit.json",
    "phase-exec-algo-r013-no-scheduler-service-polling-audit.json",
    "phase-exec-algo-r013-no-new-pms-cycle-audit.json",
    "phase-exec-algo-r013-no-manualnoexternal-command-run-audit.json",
    "phase-exec-algo-r013-no-new-backtest-audit.json",
    "phase-exec-algo-r013-no-new-simulation-audit.json",
    "phase-exec-algo-r013-no-tca-result-lines-audit.json",
    "phase-exec-algo-r013-no-executable-schedule-audit.json",
    "phase-exec-algo-r013-no-child-slices-audit.json",
    "phase-exec-algo-r013-no-child-orders-audit.json",
    "phase-exec-algo-r013-no-order-created-audit.json",
    "phase-exec-algo-r013-no-real-fill-audit.json",
    "phase-exec-algo-r013-no-execution-report-audit.json",
    "phase-exec-algo-r013-no-route-no-submission-audit.json",
    "phase-exec-algo-r013-no-paper-ledger-commit-audit.json",
    "phase-exec-algo-r013-no-polygon-api-call-audit.json",
    "phase-exec-algo-r013-no-lmax-call-audit.json",
    "phase-exec-algo-r013-no-external-api-call-audit.json"
)) {
    $audit = Read-Json (Join-Path $ArtifactsRoot $auditName)
    if (-not $audit.Passed -or $audit.Occurred) {
        Fail "EXEC_ALGO_R013_FAIL_AUDIT" "Forbidden action audit failed: $auditName"
    }
}

if (-not $noExternal.NoExternal -or
    $noExternal.PolygonCalled -or
    $noExternal.LmaxCalled -or
    $noExternal.ExternalApiCalled -or
    $noExternal.DownloadsExecuted -or
    $forbidden.ForbiddenActionsDetected -or
    $forbidden.DownloadsExecuted -or
    $forbidden.BrokerActivation -or
    $forbidden.LiveMarketData -or
    $forbidden.SchedulerServicePolling -or
    $forbidden.PmsEmsOmsCycleRun -or
    $forbidden.ManualNoExternalCommandRun -or
    $forbidden.DbImport -or
    $forbidden.PersistedSanitizedRows -or
    $forbidden.BacktestSimulationRun -or
    $forbidden.TcaResultLinesCreated -or
    $forbidden.ExecutableSchedule -or
    $forbidden.ChildSlicesOrOrders -or
    $forbidden.OrdersFillsReportsRoutesSubmissions -or
    $forbidden.PaperLedgerCommit -or
    $forbidden.StateMutation -or
    $forbidden.R009ExecutablePromotion -or
    $forbidden.PartialMaturityMisrepresentedAsFullReadiness -or
    $forbidden.ReadinessBlockerOmitted -or
    $forbidden.ReadinessBlockerMisclassifiedAsR009Failure) {
    Fail "EXEC_ALGO_R013_FAIL_FORBIDDEN_ACTION" "Forbidden action audit failed."
}

if ($evidence.DotnetBuild -ne "Passed" -or
    $evidence.FocusedR013Tests -ne "Passed" -or
    $evidence.UnitTests -ne "Passed" -or
    $evidence.R013Validator -ne "Passed" -or
    -not $evidence.EvidenceComplete) {
    Fail "EXEC_ALGO_R013_FAIL_BUILD_TEST_VALIDATOR_EVIDENCE" "Build/tests/validator evidence is missing."
}

Write-Output "EXEC_ALGO_R013_VALIDATION_PASSED"
