param(
    [string]$ArtifactsDir = "artifacts/readiness/execution-sim"
)

$ErrorActionPreference = "Stop"

function Fail-Gate {
    param([string]$Classification, [string]$Message)
    Write-Host $Classification
    throw $Message
}

function Read-Json {
    param([string]$Path, [string]$FailureClassification)
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail-Gate $FailureClassification "Required artifact is missing: $Path"
    }
    try {
        return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    }
    catch {
        Fail-Gate $FailureClassification "Artifact is not valid JSON: $Path"
    }
}

function Require-True {
    param([bool]$Value, [string]$FailureClassification, [string]$Message)
    if (-not $Value) { Fail-Gate $FailureClassification $Message }
}

function Require-False {
    param([bool]$Value, [string]$FailureClassification, [string]$Message)
    if ($Value) { Fail-Gate $FailureClassification $Message }
}

function Require-Contains {
    param([object[]]$Values, [string]$Expected, [string]$FailureClassification, [string]$Message)
    if ($Expected -notin $Values) { Fail-Gate $FailureClassification $Message }
}

$requiredArtifacts = @(
    "phase-exec-sim-r032-summary.md",
    "phase-exec-sim-r032-r031-tca-review-contract.json",
    "phase-exec-sim-r032-operator-review-report.md",
    "phase-exec-sim-r032-operator-review-report.json",
    "phase-exec-sim-r032-numeric-tca-summary.json",
    "phase-exec-sim-r032-opening-build-review.json",
    "phase-exec-sim-r032-closing-flatten-review.json",
    "phase-exec-sim-r032-opening-vs-closing-review.json",
    "phase-exec-sim-r032-intraday-vs-opening-closing-review.json",
    "phase-exec-sim-r032-policy-ranking-review.json",
    "phase-exec-sim-r032-per-symbol-review.json",
    "phase-exec-sim-r032-inverted-symbol-review.json",
    "phase-exec-sim-r032-wakett-vs-close-seeking-review.json",
    "phase-exec-sim-r032-close-seeking-adaptive-review.json",
    "phase-exec-sim-r032-controlled-residual-cross-review.json",
    "phase-exec-sim-r032-passive-until-urgency-review.json",
    "phase-exec-sim-r032-wakett-limit-residual-risk-review.json",
    "phase-exec-sim-r032-wakett-five-slices-spread-risk-review.json",
    "phase-exec-sim-r032-5usd-per-million-review.json",
    "phase-exec-sim-r032-no-overnight-residual-penalty-review.json",
    "phase-exec-sim-r032-sample-size-and-coverage-review.json",
    "phase-exec-sim-r032-session-time-calibration-decision.json",
    "phase-exec-sim-r032-data-expansion-decision.json",
    "phase-exec-sim-r032-parameter-refinement-decision.json",
    "phase-exec-sim-r032-design-only-shape-decision.json",
    "phase-exec-sim-r032-next-historical-window-recommendation.json",
    "phase-exec-sim-r032-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r032-cost-guidance-preservation.json",
    "phase-exec-sim-r032-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r032-no-new-simulation-audit.json",
    "phase-exec-sim-r032-no-new-backtest-audit.json",
    "phase-exec-sim-r032-no-new-tca-lines-audit.json",
    "phase-exec-sim-r032-no-db-import-audit.json",
    "phase-exec-sim-r032-no-persisted-sanitized-row-audit.json",
    "phase-exec-sim-r032-no-executable-schedule-audit.json",
    "phase-exec-sim-r032-no-child-slices-audit.json",
    "phase-exec-sim-r032-no-child-orders-audit.json",
    "phase-exec-sim-r032-no-real-fill-audit.json",
    "phase-exec-sim-r032-no-execution-report-audit.json",
    "phase-exec-sim-r032-no-order-created-audit.json",
    "phase-exec-sim-r032-no-route-no-submission-audit.json",
    "phase-exec-sim-r032-no-polygon-api-call-audit.json",
    "phase-exec-sim-r032-no-lmax-call-audit.json",
    "phase-exec-sim-r032-no-external-api-call-audit.json",
    "phase-exec-sim-r032-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r032-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r032-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r032-no-external-audit.json",
    "phase-exec-sim-r032-forbidden-actions-audit.json",
    "phase-exec-sim-r032-next-phase-recommendation.json",
    "phase-exec-sim-r032-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsDir $artifact))) {
        Fail-Gate "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Required R032 artifact missing: $artifact"
    }
}

$contract = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-r031-tca-review-contract.json") "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING"
$report = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-operator-review-report.json") "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING"
$numeric = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-numeric-tca-summary.json") "EXEC_SIM_R032_FAIL_NUMERIC_SUMMARY_MISSING"
$opening = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-opening-build-review.json") "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING"
$closing = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-closing-flatten-review.json") "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING"
$openingVsClosing = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-opening-vs-closing-review.json") "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING"
$intraday = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-intraday-vs-opening-closing-review.json") "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING"
$ranking = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-policy-ranking-review.json") "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING"
$perSymbol = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-per-symbol-review.json") "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING"
$inverted = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-inverted-symbol-review.json") "EXEC_SIM_R032_FAIL_USDJPY_CAVEAT_WEAKENED"
$wakettVs = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-wakett-vs-close-seeking-review.json") "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING"
$adaptive = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-close-seeking-adaptive-review.json") "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING"
$controlled = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-controlled-residual-cross-review.json") "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING"
$passive = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-passive-until-urgency-review.json") "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING"
$wakettLimit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-wakett-limit-residual-risk-review.json") "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING"
$wakettSlices = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-wakett-five-slices-spread-risk-review.json") "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING"
$fiveUsd = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-5usd-per-million-review.json") "EXEC_SIM_R032_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$penalty = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-no-overnight-residual-penalty-review.json") "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING"
$sample = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-sample-size-and-coverage-review.json") "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING"
$sessionTimes = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-session-time-calibration-decision.json") "EXEC_SIM_R032_FAIL_DECISION_ARTIFACTS_MISSING"
$data = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-data-expansion-decision.json") "EXEC_SIM_R032_FAIL_DECISION_ARTIFACTS_MISSING"
$parameters = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-parameter-refinement-decision.json") "EXEC_SIM_R032_FAIL_DECISION_ARTIFACTS_MISSING"
$design = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-design-only-shape-decision.json") "EXEC_SIM_R032_FAIL_DECISION_ARTIFACTS_MISSING"
$nextWindow = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-next-historical-window-recommendation.json") "EXEC_SIM_R032_FAIL_DECISION_ARTIFACTS_MISSING"
$direct = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-direct-cross-exclusion-preservation.json") "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING"
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-cost-guidance-preservation.json") "EXEC_SIM_R032_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$nonmajor = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-nonmajor-calibration-preservation.json") "EXEC_SIM_R032_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-usdjpy-caveat-preservation.json") "EXEC_SIM_R032_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-lmax-readonly-baseline-reference.json") "EXEC_SIM_R032_FAIL_API_CALL_DETECTED"
$noSimulation = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-no-new-simulation-audit.json") "EXEC_SIM_R032_FAIL_NEW_SIMULATION_OR_BACKTEST_EXECUTED"
$noBacktest = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-no-new-backtest-audit.json") "EXEC_SIM_R032_FAIL_NEW_SIMULATION_OR_BACKTEST_EXECUTED"
$noLines = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-no-new-tca-lines-audit.json") "EXEC_SIM_R032_FAIL_NEW_TCA_RESULTS_PRODUCED"
$noDb = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-no-db-import-audit.json") "EXEC_SIM_R032_FAIL_DB_IMPORT_OCCURRED"
$noRows = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-no-persisted-sanitized-row-audit.json") "EXEC_SIM_R032_FAIL_SANITIZED_QUOTE_ROWS_CREATED"
$noSchedule = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-no-executable-schedule-audit.json") "EXEC_SIM_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$noSlices = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-no-child-slices-audit.json") "EXEC_SIM_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$noChildOrders = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-no-child-orders-audit.json") "EXEC_SIM_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$noFill = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-no-real-fill-audit.json") "EXEC_SIM_R032_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$noReport = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-no-execution-report-audit.json") "EXEC_SIM_R032_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$noOrder = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-no-order-created-audit.json") "EXEC_SIM_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$noRoute = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-no-route-no-submission-audit.json") "EXEC_SIM_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$api = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-no-external-api-call-audit.json") "EXEC_SIM_R032_FAIL_API_CALL_DETECTED"
$runtime = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-no-broker-marketdata-runtime-audit.json") "EXEC_SIM_R032_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-no-external-audit.json") "EXEC_SIM_R032_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-forbidden-actions-audit.json") "EXEC_SIM_R032_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r032-build-test-validator-evidence.json") "EXEC_SIM_R032_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$contract.r031TcaReviewContractCreated) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "R031 review contract missing."
if ($contract.SourceSimulationPhase -ne "EXEC-SIM-R031") { Fail-Gate "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "R031 source reference missing." }
Require-True ([bool]$contract.ReviewDecisionOnly) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Review decision-only flag missing."
Require-True ([bool]$contract.NoExternalApiCalls) "EXEC_SIM_R032_FAIL_API_CALL_DETECTED" "Contract allows external API calls."
Require-True ([bool]$contract.NoDownload) "EXEC_SIM_R032_FAIL_DOWNLOAD_EXECUTED" "Contract allows download."
Require-True ([bool]$contract.NoRowValidation) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Contract allows row validation."
Require-True ([bool]$contract.NoDbImport) "EXEC_SIM_R032_FAIL_DB_IMPORT_OCCURRED" "Contract allows DB import."
Require-True ([bool]$contract.NoNewSimulation) "EXEC_SIM_R032_FAIL_NEW_SIMULATION_OR_BACKTEST_EXECUTED" "Contract allows new simulation."
Require-True ([bool]$contract.NoNewBacktest) "EXEC_SIM_R032_FAIL_NEW_SIMULATION_OR_BACKTEST_EXECUTED" "Contract allows new backtest."
Require-True ([bool]$contract.NoNewTcaResultLines) "EXEC_SIM_R032_FAIL_NEW_TCA_RESULTS_PRODUCED" "Contract allows new TCA lines."
Require-False ([bool]$contract.UnsupportedNumericMetricsInvented) "EXEC_SIM_R032_FAIL_NUMERIC_SUMMARY_MISSING" "Unsupported numeric metrics were invented."
Require-True ([bool]$contract.MissingMetricsMarkedMissingEvidence) "EXEC_SIM_R032_FAIL_NUMERIC_SUMMARY_MISSING" "Missing metrics policy absent."
Require-Contains @($contract.RecommendationStatuses) "TrueSessionTimesNeeded" "EXEC_SIM_R032_FAIL_DECISION_ARTIFACTS_MISSING" "Session-time status missing."
Require-Contains @($contract.RecommendationStatuses) "MoreDatesRecommended" "EXEC_SIM_R032_FAIL_DECISION_ARTIFACTS_MISSING" "More dates status missing."
Require-Contains @($contract.RecommendationStatuses) "ParameterRefinementDeferred" "EXEC_SIM_R032_FAIL_DECISION_ARTIFACTS_MISSING" "Parameter deferral status missing."

Require-True ([bool]$report.operatorReviewReportCreated) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Operator report missing."
Require-True ([bool]$report.NoExecutableActionAuthorized) "EXEC_SIM_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Executable action authorized."
Require-True ([bool]$report.NoExternal) "EXEC_SIM_R032_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external marker missing."
Require-True ([bool]$report.DirectCrossesRemainExcluded) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Direct-cross exclusion missing."
Require-True ([bool]$report.FiveUsdPerMillionBestCaseMajorOnly) "EXEC_SIM_R032_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million not best-case only."
Require-True ([bool]$report.NonmajorCalibrationRequired) "EXEC_SIM_R032_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "Nonmajor calibration missing."
Require-True ([bool]$report.UsdJpyCaveatPreserved) "EXEC_SIM_R032_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
if ($report.AudUsdStatus -ne "not failed") { Fail-Gate "EXEC_SIM_R032_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD was misclassified." }

Require-True ([bool]$numeric.numericTcaSummaryCreated) "EXEC_SIM_R032_FAIL_NUMERIC_SUMMARY_MISSING" "Numeric summary missing."
if ($numeric.SourceSimulationPhase -ne "EXEC-SIM-R031") { Fail-Gate "EXEC_SIM_R032_FAIL_NUMERIC_SUMMARY_MISSING" "Numeric summary did not source R031." }
Require-False ([bool]$numeric.UnsupportedNumericMetricsInvented) "EXEC_SIM_R032_FAIL_NUMERIC_SUMMARY_MISSING" "Numeric summary invented unsupported metrics."
if ($numeric.R031RunCounts.AuthorizedEntryCount -ne 14 -or $numeric.R031RunCounts.QuoteWindowCount -ne 224 -or $numeric.R031RunCounts.TcaResultLineCount -ne 2464) {
    Fail-Gate "EXEC_SIM_R032_FAIL_NUMERIC_SUMMARY_MISSING" "R031 run counts missing from numeric summary."
}
if ($numeric.R025IntradayRebalanceCounts.SymbolCount -ne 7 -or $numeric.R025IntradayRebalanceCounts.TcaResultLineCount -ne 1232) {
    Fail-Gate "EXEC_SIM_R032_FAIL_NUMERIC_SUMMARY_MISSING" "R025 intraday comparison counts missing."
}
Require-True ([bool]$numeric.FiveUsdPerMillionTargetComparison.BestCaseMajorOnly) "EXEC_SIM_R032_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million best-case missing."
Require-False ([bool]$numeric.FiveUsdPerMillionTargetComparison.Universalized) "EXEC_SIM_R032_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million universalized."
Require-True ([bool]$numeric.NoOvernightResidualPenaltyComparison.ClosingFlattenMateriallyMoreSensitive) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "No-overnight sensitivity review missing."

Require-True ([bool]$opening.openingBuildReviewCreated) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "OpeningBuild review missing."
Require-True ([bool]$opening.ProxySessionWindow) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "OpeningBuild proxy caveat missing."
Require-True ([bool]$opening.NeedsOperatorSessionTimes) "EXEC_SIM_R032_FAIL_DECISION_ARTIFACTS_MISSING" "OpeningBuild session-time need missing."
Require-True ([bool]$closing.closingFlattenReviewCreated) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "ClosingFlatten review missing."
Require-True ([bool]$closing.ProxySessionWindow) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "ClosingFlatten proxy caveat missing."
Require-True ([bool]$closing.NeedsOperatorSessionTimes) "EXEC_SIM_R032_FAIL_DECISION_ARTIFACTS_MISSING" "ClosingFlatten session-time need missing."
Require-True ([bool]$openingVsClosing.openingVsClosingReviewCreated) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Opening-vs-closing review missing."
Require-True ([bool]$openingVsClosing.ClosingFlattenHigherResidualPenaltySensitivity) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Closing residual sensitivity missing."
Require-True ([bool]$intraday.intradayVsOpeningClosingReviewCreated) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Intraday comparison missing."
Require-True ([bool]$intraday.NeedsOperatorSessionTimes) "EXEC_SIM_R032_FAIL_DECISION_ARTIFACTS_MISSING" "Intraday/session-time decision missing."

Require-True ([bool]$ranking.policyRankingReviewCreated) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Policy ranking review missing."
Require-False ([bool]$ranking.UnsupportedNumericMetricsInvented) "EXEC_SIM_R032_FAIL_NUMERIC_SUMMARY_MISSING" "Ranking review invented metrics."
if (@($ranking.NoOvernightResidualPenaltyRanking).Count -eq 0) { Fail-Gate "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "No-overnight penalty ranking missing." }
Require-True ([bool]$perSymbol.perSymbolReviewCreated) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Per-symbol review missing."
if (@($perSymbol.comparisons).Count -ne 7) { Fail-Gate "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Per-symbol review incomplete." }
if ($perSymbol.AudUsdStatus -ne "not failed") { Fail-Gate "EXEC_SIM_R032_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD was misclassified." }
foreach ($symbolReview in @($perSymbol.comparisons)) {
    Require-True ([bool]$symbolReview.OpeningBuildPresent) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "OpeningBuild missing for $($symbolReview.Symbol)."
    Require-True ([bool]$symbolReview.ClosingFlattenPresent) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "ClosingFlatten missing for $($symbolReview.Symbol)."
}

Require-True ([bool]$inverted.invertedSymbolReviewCreated) "EXEC_SIM_R032_FAIL_USDJPY_CAVEAT_WEAKENED" "Inverted review missing."
Require-True ([bool]$inverted.UsdJpyCaveatPreserved) "EXEC_SIM_R032_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat weakened."
Require-False ([bool]$inverted.AudUsdMisclassifiedFailed) "EXEC_SIM_R032_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD misclassified in inverted review."
if (@($inverted.InvertedSymbols).Count -ne 3) { Fail-Gate "EXEC_SIM_R032_FAIL_USDJPY_CAVEAT_WEAKENED" "Inverted symbol review incomplete." }
Require-True ([bool]$inverted.InvertedPairsBehavingSafelyInArtifactReview) "EXEC_SIM_R032_FAIL_USDJPY_CAVEAT_WEAKENED" "Inverted pair safety review missing."

Require-True ([bool]$wakettVs.wakettVsCloseSeekingReviewCreated) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Wakett vs CloseSeeking review missing."
Require-True ([bool]$wakettVs.WakettPureLimitUntilCloseResidualRiskHigh) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Wakett limit residual risk missing."
Require-True ([bool]$wakettVs.WakettFiveMarketSlicesSpreadPaidRiskHigh) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Wakett slices spread risk missing."
Require-True ([bool]$wakettVs.WakettPatternsRemainRejectedAsDefault) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Wakett blocks weakened."
Require-True ([bool]$adaptive.closeSeekingAdaptiveReviewCreated) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Adaptive review missing."
Require-True ([bool]$adaptive.ParameterRefinementDeferred) "EXEC_SIM_R032_FAIL_DECISION_ARTIFACTS_MISSING" "Adaptive parameter deferral missing."
Require-True ([bool]$adaptive.NeedsTrueSessionTimes) "EXEC_SIM_R032_FAIL_DECISION_ARTIFACTS_MISSING" "Adaptive session-time need missing."
Require-True ([bool]$controlled.controlledResidualCrossReviewCreated) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Controlled residual review missing."
Require-True ([bool]$controlled.ConditionalUseOnly) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Controlled residual conditionality missing."
Require-True ([bool]$controlled.EspeciallyRelevantForClosingFlatten) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Controlled residual ClosingFlatten relevance missing."
Require-True ([bool]$passive.passiveUntilUrgencyReviewCreated) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Passive review missing."
Require-True ([bool]$passive.InsufficientWhereResidualMatters) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Passive insufficiency missing."
Require-True ([bool]$wakettLimit.wakettLimitResidualRiskReviewCreated) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Wakett limit risk review missing."
Require-True ([bool]$wakettLimit.RejectUnsafePattern) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Wakett limit reject missing."
Require-True ([bool]$wakettSlices.wakettFiveSlicesSpreadRiskReviewCreated) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Wakett slices risk review missing."
Require-True ([bool]$wakettSlices.RejectUnsafePattern) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Wakett slices reject missing."

Require-True ([bool]$fiveUsd.fiveUsdPerMillionReviewCreated) "EXEC_SIM_R032_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million review missing."
Require-True ([bool]$fiveUsd.BestCaseMajorOnly) "EXEC_SIM_R032_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million best-case review missing."
Require-False ([bool]$fiveUsd.Universalized) "EXEC_SIM_R032_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million universalized."
Require-True ([bool]$penalty.noOvernightResidualPenaltyReviewCreated) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "No-overnight review missing."
Require-True ([bool]$penalty.MustEndFlat) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "MustEndFlat missing."
Require-False ([bool]$penalty.OvernightAllowed) "EXEC_SIM_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Overnight allowed."
Require-True ([bool]$penalty.ClosingFlattenResidualPenaltyMateriallyHigher) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Closing residual penalty sensitivity missing."
Require-True ([bool]$sample.sampleSizeAndCoverageReviewCreated) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Sample review missing."
Require-True ([bool]$sample.R031UsesOneDate) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "One-date caveat missing."
Require-True ([bool]$sample.OpeningBuildWindowIsProxy) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Opening proxy caveat missing."
Require-True ([bool]$sample.ClosingFlattenWindowIsProxy) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Closing proxy caveat missing."
Require-False ([bool]$sample.TrueModelSessionTimesConfirmed) "EXEC_SIM_R032_FAIL_DECISION_ARTIFACTS_MISSING" "True session times incorrectly confirmed."
Require-True ([bool]$sample.NeedsOperatorSessionTimes) "EXEC_SIM_R032_FAIL_DECISION_ARTIFACTS_MISSING" "NeedsOperatorSessionTimes missing."
Require-True ([bool]$sample.SampleTooSmallToConcludeProductionParameters) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Sample-size caveat weakened."

Require-True ([bool]$sessionTimes.sessionTimeCalibrationDecisionCreated) "EXEC_SIM_R032_FAIL_DECISION_ARTIFACTS_MISSING" "Session-time decision missing."
if ($sessionTimes.RecommendationStatus -ne "TrueSessionTimesNeeded" -or $sessionTimes.DecisionCategory -ne "ConfirmSessionTimes") { Fail-Gate "EXEC_SIM_R032_FAIL_DECISION_ARTIFACTS_MISSING" "Session-time decision mismatch." }
Require-False ([bool]$sessionTimes.TrueModelSessionTimesFoundInArtifacts) "EXEC_SIM_R032_FAIL_DECISION_ARTIFACTS_MISSING" "True model session times incorrectly found."
Require-True ([bool]$sessionTimes.NeedsOperatorSessionTimes) "EXEC_SIM_R032_FAIL_DECISION_ARTIFACTS_MISSING" "NeedsOperatorSessionTimes missing."
Require-True ([bool]$data.dataExpansionDecisionCreated) "EXEC_SIM_R032_FAIL_DECISION_ARTIFACTS_MISSING" "Data expansion decision missing."
if ($data.DecisionCategory -ne "ExpandMoreDates") { Fail-Gate "EXEC_SIM_R032_FAIL_DECISION_ARTIFACTS_MISSING" "Data expansion decision mismatch." }
Require-True ([bool]$data.MoreDatesRecommended) "EXEC_SIM_R032_FAIL_DECISION_ARTIFACTS_MISSING" "More dates not recommended."
Require-True ([bool]$parameters.parameterRefinementDecisionCreated) "EXEC_SIM_R032_FAIL_DECISION_ARTIFACTS_MISSING" "Parameter decision missing."
if ($parameters.RecommendationStatus -ne "ParameterRefinementDeferred") { Fail-Gate "EXEC_SIM_R032_FAIL_DECISION_ARTIFACTS_MISSING" "Parameter refinement not deferred." }
Require-True ([bool]$parameters.KeepCloseSeeking15mAdaptiveAsMainCandidate) "EXEC_SIM_R032_FAIL_DECISION_ARTIFACTS_MISSING" "Adaptive candidate not preserved."
Require-True ([bool]$design.designOnlyShapeDecisionCreated) "EXEC_SIM_R032_FAIL_DECISION_ARTIFACTS_MISSING" "Design-only decision missing."
Require-True ([bool]$design.IsDesignOnly) "EXEC_SIM_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Design-only marker missing."
Require-False ([bool]$design.IsExecutable) "EXEC_SIM_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Executable design authorized."
Require-True ([bool]$nextWindow.nextHistoricalWindowRecommendationCreated) "EXEC_SIM_R032_FAIL_DECISION_ARTIFACTS_MISSING" "Next historical-window recommendation missing."
Require-True ([bool]$nextWindow.NeedsOperatorSessionTimes) "EXEC_SIM_R032_FAIL_DECISION_ARTIFACTS_MISSING" "Next window session-time need missing."

Require-True ([bool]$direct.directCrossExclusionPreserved) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Direct-cross exclusion not preserved."
Require-False ([bool]$direct.directCrossIncluded) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Direct-cross included."
Require-False ([bool]$direct.directCrossExecutionAllowedByDefault) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Direct-cross execution allowed."
Require-False ([bool]$direct.directCrossExclusionWeakened) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Direct-cross exclusion weakened."
Require-True ([bool]$cost.fiveUsdPerMillionBestCaseMajorOnly) "EXEC_SIM_R032_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "Cost guidance weakened."
Require-False ([bool]$cost.fiveUsdPerMillionUniversalized) "EXEC_SIM_R032_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million universalized."
Require-True ([bool]$nonmajor.RequiresLiquidityCalibration) "EXEC_SIM_R032_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "Nonmajor calibration missing."
Require-False ([bool]$nonmajor.calibrationRequirementWeakened) "EXEC_SIM_R032_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "Nonmajor calibration weakened."
Require-True ([bool]$usdjpy.usdjpyCaveatPreserved) "EXEC_SIM_R032_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
if ($usdjpy.PortfolioNormalizedSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or -not [bool]$usdjpy.RequiresInversion -or $usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8") {
    Fail-Gate "EXEC_SIM_R032_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY inversion/caveat weakened."
}
Require-True ([bool]$lmax.referenceOnly) "EXEC_SIM_R032_FAIL_API_CALL_DETECTED" "LMAX not reference-only."
Require-False ([bool]$lmax.lmaxCalledInR032) "EXEC_SIM_R032_FAIL_API_CALL_DETECTED" "LMAX called."

Require-False ([bool]$noSimulation.newSimulationExecuted) "EXEC_SIM_R032_FAIL_NEW_SIMULATION_OR_BACKTEST_EXECUTED" "New simulation executed."
Require-False ([bool]$noBacktest.newBacktestExecuted) "EXEC_SIM_R032_FAIL_NEW_SIMULATION_OR_BACKTEST_EXECUTED" "New backtest executed."
Require-False ([bool]$noLines.newTcaResultLinesProduced) "EXEC_SIM_R032_FAIL_NEW_TCA_RESULTS_PRODUCED" "New TCA lines produced."
Require-False ([bool]$noDb.quotesImportedIntoDb) "EXEC_SIM_R032_FAIL_DB_IMPORT_OCCURRED" "Quotes imported into DB."
Require-False ([bool]$noRows.persistedSanitizedQuoteRowsCreated) "EXEC_SIM_R032_FAIL_SANITIZED_QUOTE_ROWS_CREATED" "Persisted sanitized rows created."
Require-False ([bool]$noSchedule.executableSchedulesCreated) "EXEC_SIM_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Executable schedules created."
Require-False ([bool]$noSlices.childSlicesCreated) "EXEC_SIM_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Child slices created."
Require-False ([bool]$noChildOrders.childOrdersCreated) "EXEC_SIM_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Child orders created."
Require-False ([bool]$noFill.realFillsCreated) "EXEC_SIM_R032_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Real fills created."
Require-False ([bool]$noReport.executionReportEntitiesCreated) "EXEC_SIM_R032_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Execution reports created."
Require-False ([bool]$noOrder.ordersCreated) "EXEC_SIM_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders created."
Require-False ([bool]$noRoute.routesCreated) "EXEC_SIM_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Routes created."
Require-False ([bool]$noRoute.submissionsCreated) "EXEC_SIM_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Submissions created."
Require-False ([bool]$api.externalApiCalled) "EXEC_SIM_R032_FAIL_API_CALL_DETECTED" "External API called."
Require-False ([bool]$api.polygonApiCalled) "EXEC_SIM_R032_FAIL_API_CALL_DETECTED" "Polygon API called."
Require-False ([bool]$api.lmaxCalled) "EXEC_SIM_R032_FAIL_API_CALL_DETECTED" "LMAX called."
Require-False ([bool]$runtime.brokerActivationDetected) "EXEC_SIM_R032_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "Broker activation detected."
Require-False ([bool]$runtime.socketOpened) "EXEC_SIM_R032_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "Socket opened."
Require-False ([bool]$runtime.tlsOpened) "EXEC_SIM_R032_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "TLS opened."
Require-False ([bool]$runtime.fixOpened) "EXEC_SIM_R032_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "FIX opened."
Require-False ([bool]$runtime.marketDataRequestSent) "EXEC_SIM_R032_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "MarketDataRequest sent."
Require-False ([bool]$runtime.marketDataResponseRead) "EXEC_SIM_R032_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "MarketDataResponse read."
Require-False ([bool]$runtime.schedulerServiceTimerPollingBackgroundJobIntroduced) "EXEC_SIM_R032_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler/timer/background job introduced."
Require-False ([bool]$noExternal.externalApiCalled) "EXEC_SIM_R032_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "External API called."
Require-False ([bool]$noExternal.filesDownloaded) "EXEC_SIM_R032_FAIL_DOWNLOAD_EXECUTED" "Files downloaded."
Require-False ([bool]$noExternal.quoteRowsValidated) "EXEC_SIM_R032_FAIL_REVIEW_REPORT_MISSING" "Quote rows validated."
Require-False ([bool]$noExternal.newSimulationOrBacktestExecuted) "EXEC_SIM_R032_FAIL_NEW_SIMULATION_OR_BACKTEST_EXECUTED" "New simulation/backtest executed."
Require-False ([bool]$noExternal.newTcaResultLinesProduced) "EXEC_SIM_R032_FAIL_NEW_TCA_RESULTS_PRODUCED" "New TCA lines produced."
Require-False ([bool]$noExternal.ordersFillsReportsRoutesSubmissionsCreated) "EXEC_SIM_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order-domain output created."
Require-False ([bool]$forbidden.forbiddenActionsDetected) "EXEC_SIM_R032_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden actions detected."

if ($evidence.dotnetBuildNoRestore -notlike "PASS*" -or $evidence.focusedTests -notlike "PASS*" -or $evidence.unitTests -notlike "PASS*" -or $evidence.validator -notlike "PASS*") {
    Fail-Gate "EXEC_SIM_R032_FAIL_BUILD_OR_TESTS" "Build/test/validator evidence is missing or not passing."
}

Write-Host "EXEC_SIM_R032_PASS_HISTORICAL_WINDOW_TCA_REVIEW_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R032_PASS_SESSION_POLICY_DECISION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R032_PASS_DATA_EXPANSION_AND_PARAMETER_DECISION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R032_PASS_NO_NEW_SIMULATION_NO_ORDER_GATE_READY_NO_EXTERNAL"
