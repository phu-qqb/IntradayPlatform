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
    "phase-exec-sim-r026-summary.md",
    "phase-exec-sim-r026-r025-tca-review-contract.json",
    "phase-exec-sim-r026-operator-review-report.md",
    "phase-exec-sim-r026-operator-review-report.json",
    "phase-exec-sim-r026-numeric-tca-summary.json",
    "phase-exec-sim-r026-policy-ranking-review.json",
    "phase-exec-sim-r026-per-symbol-review.json",
    "phase-exec-sim-r026-wakett-vs-close-seeking-review.json",
    "phase-exec-sim-r026-close-seeking-adaptive-review.json",
    "phase-exec-sim-r026-controlled-residual-cross-review.json",
    "phase-exec-sim-r026-passive-until-urgency-review.json",
    "phase-exec-sim-r026-wakett-limit-residual-risk-review.json",
    "phase-exec-sim-r026-wakett-five-slices-spread-risk-review.json",
    "phase-exec-sim-r026-inverted-symbol-review.json",
    "phase-exec-sim-r026-5usd-per-million-review.json",
    "phase-exec-sim-r026-sample-size-and-coverage-review.json",
    "phase-exec-sim-r026-data-expansion-decision.json",
    "phase-exec-sim-r026-parameter-refinement-decision.json",
    "phase-exec-sim-r026-next-historical-window-recommendation.json",
    "phase-exec-sim-r026-opening-closing-window-recommendation.json",
    "phase-exec-sim-r026-more-instrument-coverage-recommendation.json",
    "phase-exec-sim-r026-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r026-cost-guidance-preservation.json",
    "phase-exec-sim-r026-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r026-no-new-simulation-audit.json",
    "phase-exec-sim-r026-no-new-backtest-audit.json",
    "phase-exec-sim-r026-no-new-tca-lines-audit.json",
    "phase-exec-sim-r026-no-db-import-audit.json",
    "phase-exec-sim-r026-no-persisted-sanitized-row-audit.json",
    "phase-exec-sim-r026-no-executable-schedule-audit.json",
    "phase-exec-sim-r026-no-child-slices-audit.json",
    "phase-exec-sim-r026-no-child-orders-audit.json",
    "phase-exec-sim-r026-no-real-fill-audit.json",
    "phase-exec-sim-r026-no-execution-report-audit.json",
    "phase-exec-sim-r026-no-order-created-audit.json",
    "phase-exec-sim-r026-no-route-no-submission-audit.json",
    "phase-exec-sim-r026-no-polygon-api-call-audit.json",
    "phase-exec-sim-r026-no-lmax-call-audit.json",
    "phase-exec-sim-r026-no-external-api-call-audit.json",
    "phase-exec-sim-r026-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r026-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r026-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r026-no-external-audit.json",
    "phase-exec-sim-r026-forbidden-actions-audit.json",
    "phase-exec-sim-r026-next-phase-recommendation.json",
    "phase-exec-sim-r026-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsDir $artifact))) {
        Fail-Gate "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING" "Required R026 artifact missing: $artifact"
    }
}

$contract = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-r025-tca-review-contract.json") "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING"
$report = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-operator-review-report.json") "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING"
$numeric = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-numeric-tca-summary.json") "EXEC_SIM_R026_FAIL_NUMERIC_SUMMARY_MISSING"
$ranking = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-policy-ranking-review.json") "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING"
$perSymbol = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-per-symbol-review.json") "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING"
$wakettVs = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-wakett-vs-close-seeking-review.json") "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING"
$adaptive = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-close-seeking-adaptive-review.json") "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING"
$controlled = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-controlled-residual-cross-review.json") "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING"
$passive = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-passive-until-urgency-review.json") "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING"
$wakettLimit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-wakett-limit-residual-risk-review.json") "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING"
$wakettSlices = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-wakett-five-slices-spread-risk-review.json") "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING"
$inverted = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-inverted-symbol-review.json") "EXEC_SIM_R026_FAIL_USDJPY_CAVEAT_WEAKENED"
$fiveUsd = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-5usd-per-million-review.json") "EXEC_SIM_R026_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$sample = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-sample-size-and-coverage-review.json") "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING"
$data = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-data-expansion-decision.json") "EXEC_SIM_R026_FAIL_DECISION_ARTIFACTS_MISSING"
$parameters = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-parameter-refinement-decision.json") "EXEC_SIM_R026_FAIL_DECISION_ARTIFACTS_MISSING"
$historical = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-next-historical-window-recommendation.json") "EXEC_SIM_R026_FAIL_DECISION_ARTIFACTS_MISSING"
$openingClosing = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-opening-closing-window-recommendation.json") "EXEC_SIM_R026_FAIL_DECISION_ARTIFACTS_MISSING"
$moreInstruments = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-more-instrument-coverage-recommendation.json") "EXEC_SIM_R026_FAIL_DECISION_ARTIFACTS_MISSING"
$direct = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-direct-cross-exclusion-preservation.json") "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING"
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-cost-guidance-preservation.json") "EXEC_SIM_R026_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$nonmajor = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-nonmajor-calibration-preservation.json") "EXEC_SIM_R026_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-usdjpy-caveat-preservation.json") "EXEC_SIM_R026_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-lmax-readonly-baseline-reference.json") "EXEC_SIM_R026_FAIL_API_CALL_DETECTED"
$noSimulation = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-no-new-simulation-audit.json") "EXEC_SIM_R026_FAIL_NEW_SIMULATION_OR_BACKTEST_EXECUTED"
$noBacktest = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-no-new-backtest-audit.json") "EXEC_SIM_R026_FAIL_NEW_SIMULATION_OR_BACKTEST_EXECUTED"
$noLines = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-no-new-tca-lines-audit.json") "EXEC_SIM_R026_FAIL_NEW_TCA_RESULTS_PRODUCED"
$noDb = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-no-db-import-audit.json") "EXEC_SIM_R026_FAIL_DB_IMPORT_OCCURRED"
$noRows = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-no-persisted-sanitized-row-audit.json") "EXEC_SIM_R026_FAIL_SANITIZED_QUOTE_ROWS_CREATED"
$noSchedule = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-no-executable-schedule-audit.json") "EXEC_SIM_R026_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$noSlices = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-no-child-slices-audit.json") "EXEC_SIM_R026_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$noChildOrders = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-no-child-orders-audit.json") "EXEC_SIM_R026_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$noFill = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-no-real-fill-audit.json") "EXEC_SIM_R026_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$noReport = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-no-execution-report-audit.json") "EXEC_SIM_R026_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$noOrder = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-no-order-created-audit.json") "EXEC_SIM_R026_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$noRoute = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-no-route-no-submission-audit.json") "EXEC_SIM_R026_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$api = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-no-external-api-call-audit.json") "EXEC_SIM_R026_FAIL_API_CALL_DETECTED"
$runtime = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-no-broker-marketdata-runtime-audit.json") "EXEC_SIM_R026_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-no-external-audit.json") "EXEC_SIM_R026_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-forbidden-actions-audit.json") "EXEC_SIM_R026_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r026-build-test-validator-evidence.json") "EXEC_SIM_R026_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$contract.r025TcaReviewContractCreated) "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING" "Review contract missing."
if ($contract.SourceSimulationPhase -ne "EXEC-SIM-R025") { Fail-Gate "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING" "R025 source reference missing." }
Require-True ([bool]$contract.ReviewDecisionOnly) "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING" "Review decision-only flag missing."
Require-True ([bool]$contract.NoExternalApiCalls) "EXEC_SIM_R026_FAIL_API_CALL_DETECTED" "Contract allows external API calls."
Require-True ([bool]$contract.NoNewSimulation) "EXEC_SIM_R026_FAIL_NEW_SIMULATION_OR_BACKTEST_EXECUTED" "Contract allows new simulation."
Require-True ([bool]$contract.NoNewBacktest) "EXEC_SIM_R026_FAIL_NEW_SIMULATION_OR_BACKTEST_EXECUTED" "Contract allows new backtest."
Require-True ([bool]$contract.NoNewTcaResultLines) "EXEC_SIM_R026_FAIL_NEW_TCA_RESULTS_PRODUCED" "Contract allows new TCA lines."
Require-False ([bool]$contract.UnsupportedNumericMetricsInvented) "EXEC_SIM_R026_FAIL_NUMERIC_SUMMARY_MISSING" "Unsupported numeric metrics were invented."
Require-Contains @($contract.RecommendationStatuses) "MoreHistoricalWindowsRecommended" "EXEC_SIM_R026_FAIL_DECISION_ARTIFACTS_MISSING" "Historical window recommendation status missing."
Require-Contains @($contract.RecommendationStatuses) "OpeningClosingWindowsRecommended" "EXEC_SIM_R026_FAIL_DECISION_ARTIFACTS_MISSING" "Opening/closing recommendation status missing."
Require-Contains @($contract.RecommendationStatuses) "ParameterRefinementRecommended" "EXEC_SIM_R026_FAIL_DECISION_ARTIFACTS_MISSING" "Parameter refinement status missing."

Require-True ([bool]$report.operatorReviewReportCreated) "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING" "Operator report missing."
Require-True ([bool]$report.NoExecutableActionAuthorized) "EXEC_SIM_R026_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Executable action authorized."
Require-True ([bool]$report.DirectCrossesRemainExcluded) "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING" "Direct cross exclusion not preserved."
Require-True ([bool]$report.FiveUsdPerMillionBestCaseMajorOnly) "EXEC_SIM_R026_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million not best-case major-only."
if ($report.AudUsdStatus -ne "not failed") { Fail-Gate "EXEC_SIM_R026_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD was misclassified." }

Require-True ([bool]$numeric.numericTcaSummaryCreated) "EXEC_SIM_R026_FAIL_NUMERIC_SUMMARY_MISSING" "Numeric summary missing."
if ($numeric.NumericMetricsSource -ne "EXEC-SIM-R025 artifacts") { Fail-Gate "EXEC_SIM_R026_FAIL_NUMERIC_SUMMARY_MISSING" "Numeric summary did not source R025 artifacts." }
Require-False ([bool]$numeric.UnsupportedNumericMetricsInvented) "EXEC_SIM_R026_FAIL_NUMERIC_SUMMARY_MISSING" "Numeric summary invented unsupported metrics."
if ($numeric.R025RunCounts.SymbolCount -ne 7 -or $numeric.R025RunCounts.QuoteWindowCount -ne 112 -or $numeric.R025RunCounts.TcaResultLineCount -ne 1232) {
    Fail-Gate "EXEC_SIM_R026_FAIL_NUMERIC_SUMMARY_MISSING" "R025 run counts missing from numeric summary."
}
if ($numeric.Scope.HistoricalWindowCount -ne 1 -or $numeric.Scope.SampleSizeConclusion -ne "TooSmallForProductionParameterConclusion") {
    Fail-Gate "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING" "Sample-size caveat missing."
}
if ($numeric.BestWorstExecutableLikeCandidates.P95Slippage.bestPolicy -ne "ControlledResidualCross") {
    Fail-Gate "EXEC_SIM_R026_FAIL_NUMERIC_SUMMARY_MISSING" "Executable-like p95 best policy review mismatch."
}
Require-True ([bool]$numeric.FiveUsdPerMillionTargetComparison.BestCaseMajorOnly) "EXEC_SIM_R026_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million best-case flag missing."
Require-False ([bool]$numeric.FiveUsdPerMillionTargetComparison.Universalized) "EXEC_SIM_R026_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million was universalized."
Require-False ([bool]$numeric.FiveUsdPerMillionTargetComparison.DemonstratedAsUniversalInR025) "EXEC_SIM_R026_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "R025 was claimed universal."

Require-True ([bool]$ranking.policyRankingReviewCreated) "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING" "Policy ranking review missing."
Require-False ([bool]$ranking.UnsupportedNumericMetricsInvented) "EXEC_SIM_R026_FAIL_NUMERIC_SUMMARY_MISSING" "Ranking review invented metrics."
Require-True ([bool]$perSymbol.perSymbolReviewCreated) "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING" "Per-symbol review missing."
if (@($perSymbol.comparisons).Count -ne 7) { Fail-Gate "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING" "Per-symbol review incomplete." }
if ($perSymbol.AudUsdStatus -ne "not failed") { Fail-Gate "EXEC_SIM_R026_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD was misclassified in per-symbol review." }

Require-True ([bool]$wakettVs.wakettVsCloseSeekingReviewCreated) "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING" "Wakett vs CloseSeeking review missing."
Require-True ([bool]$wakettVs.CloseSeeking15mAdaptiveOutperformedWakettBaselines) "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING" "Adaptive outperformance review missing."
Require-True ([bool]$wakettVs.DirectCrossesRemainExcluded) "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING" "Direct cross exclusion weakened."
Require-True ([bool]$wakettVs.WakettPatternsRemainRejectedAsDefault) "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING" "Wakett default block weakened."
Require-True ([bool]$adaptive.closeSeekingAdaptiveReviewCreated) "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING" "Adaptive review missing."
if ($adaptive.RecommendationStatus -ne "KeepForFurtherOfflineTesting") { Fail-Gate "EXEC_SIM_R026_FAIL_DECISION_ARTIFACTS_MISSING" "Adaptive recommendation mismatch." }
Require-True ([bool]$controlled.ConditionalUseOnly) "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING" "Controlled residual conditionality missing."
Require-True ([bool]$passive.InsufficientWhereResidualMatters) "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING" "Passive insufficiency review missing."
Require-True ([bool]$wakettLimit.ResidualRiskHigh) "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING" "Wakett limit residual risk missing."
Require-True ([bool]$wakettSlices.SpreadPaidRiskHigh) "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING" "Wakett slices spread risk missing."

Require-True ([bool]$inverted.invertedSymbolReviewCreated) "EXEC_SIM_R026_FAIL_USDJPY_CAVEAT_WEAKENED" "Inverted symbol review missing."
Require-True ([bool]$inverted.UsdJpyCaveatPreserved) "EXEC_SIM_R026_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat weakened."
Require-False ([bool]$inverted.AudUsdMisclassifiedFailed) "EXEC_SIM_R026_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD misclassified in inverted review."
Require-True ([bool]$fiveUsd.fiveUsdPerMillionReviewCreated) "EXEC_SIM_R026_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million review missing."
Require-True ([bool]$fiveUsd.BestCaseMajorOnly) "EXEC_SIM_R026_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million best-case review missing."
Require-False ([bool]$fiveUsd.Universalized) "EXEC_SIM_R026_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million universalized."

Require-True ([bool]$sample.sampleSizeAndCoverageReviewCreated) "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING" "Sample coverage review missing."
Require-True ([bool]$sample.SampleTooSmallToConcludeProductionParameters) "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING" "Sample caveat weakened."
Require-False ([bool]$sample.OpeningBuildCovered) "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING" "OpeningBuild coverage overstated."
Require-False ([bool]$sample.ClosingFlattenCovered) "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING" "ClosingFlatten coverage overstated."
Require-True ([bool]$data.dataExpansionDecisionCreated) "EXEC_SIM_R026_FAIL_DECISION_ARTIFACTS_MISSING" "Data expansion decision missing."
if ($data.RecommendationStatus -ne "MoreHistoricalWindowsRecommended") { Fail-Gate "EXEC_SIM_R026_FAIL_DECISION_ARTIFACTS_MISSING" "Historical expansion decision missing." }
Require-True ([bool]$data.OpeningClosingWindowsRecommended) "EXEC_SIM_R026_FAIL_DECISION_ARTIFACTS_MISSING" "Opening/closing decision missing."
Require-True ([bool]$parameters.parameterRefinementDecisionCreated) "EXEC_SIM_R026_FAIL_DECISION_ARTIFACTS_MISSING" "Parameter refinement decision missing."
if ($parameters.RecommendationStatus -ne "ParameterRefinementRecommended") { Fail-Gate "EXEC_SIM_R026_FAIL_DECISION_ARTIFACTS_MISSING" "Parameter refinement recommendation missing." }
Require-True ([bool]$historical.nextHistoricalWindowRecommendationCreated) "EXEC_SIM_R026_FAIL_DECISION_ARTIFACTS_MISSING" "Historical window recommendation missing."
Require-True ([bool]$openingClosing.openingClosingWindowRecommendationCreated) "EXEC_SIM_R026_FAIL_DECISION_ARTIFACTS_MISSING" "Opening/closing recommendation missing."
Require-True ([bool]$moreInstruments.moreInstrumentCoverageRecommendationCreated) "EXEC_SIM_R026_FAIL_DECISION_ARTIFACTS_MISSING" "Instrument coverage recommendation missing."
Require-True ([bool]$moreInstruments.NonmajorEmScandiCnhCalibrationRequired) "EXEC_SIM_R026_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "Nonmajor calibration weakened."

Require-True ([bool]$direct.directCrossExclusionPreserved) "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING" "Direct cross exclusion not preserved."
Require-False ([bool]$direct.directCrossExecutionAllowedByDefault) "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING" "Direct cross execution allowed."
Require-False ([bool]$direct.directCrossExclusionWeakened) "EXEC_SIM_R026_FAIL_REVIEW_REPORT_MISSING" "Direct cross exclusion weakened."
Require-True ([bool]$cost.fiveUsdPerMillionBestCaseMajorOnly) "EXEC_SIM_R026_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "Cost guidance weakened."
Require-False ([bool]$cost.fiveUsdPerMillionUniversalized) "EXEC_SIM_R026_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million universalized."
Require-True ([bool]$nonmajor.nonMajorEmScandiCnhRequireLiquidityCalibration) "EXEC_SIM_R026_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "Nonmajor calibration missing."
Require-False ([bool]$nonmajor.calibrationRequirementWeakened) "EXEC_SIM_R026_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "Nonmajor calibration weakened."
Require-True ([bool]$usdjpy.usdjpyCaveatPreserved) "EXEC_SIM_R026_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
if ($usdjpy.PortfolioNormalizedSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or -not [bool]$usdjpy.RequiresInversion) {
    Fail-Gate "EXEC_SIM_R026_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY inversion mapping weakened."
}
Require-False ([bool]$usdjpy.audusdMisclassifiedFailed) "EXEC_SIM_R026_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD misclassified."
Require-True ([bool]$lmax.referenceOnly) "EXEC_SIM_R026_FAIL_API_CALL_DETECTED" "LMAX reference-only flag missing."
Require-False ([bool]$lmax.lmaxCalledInR026) "EXEC_SIM_R026_FAIL_API_CALL_DETECTED" "LMAX was called."

Require-False ([bool]$noSimulation.newSimulationExecuted) "EXEC_SIM_R026_FAIL_NEW_SIMULATION_OR_BACKTEST_EXECUTED" "New simulation executed."
Require-False ([bool]$noBacktest.newBacktestExecuted) "EXEC_SIM_R026_FAIL_NEW_SIMULATION_OR_BACKTEST_EXECUTED" "New backtest executed."
Require-False ([bool]$noLines.newTcaResultLinesProduced) "EXEC_SIM_R026_FAIL_NEW_TCA_RESULTS_PRODUCED" "New TCA lines produced."
Require-False ([bool]$noDb.quotesImportedIntoDb) "EXEC_SIM_R026_FAIL_DB_IMPORT_OCCURRED" "DB import occurred."
Require-False ([bool]$noRows.persistedSanitizedQuoteRowsCreated) "EXEC_SIM_R026_FAIL_SANITIZED_QUOTE_ROWS_CREATED" "Persisted sanitized quote rows created."
Require-False ([bool]$noSchedule.executableSchedulesCreated) "EXEC_SIM_R026_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Executable schedules created."
Require-False ([bool]$noSlices.childSlicesCreated) "EXEC_SIM_R026_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Child slices created."
Require-False ([bool]$noChildOrders.childOrdersCreated) "EXEC_SIM_R026_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Child orders created."
Require-False ([bool]$noFill.realFillsCreated) "EXEC_SIM_R026_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Real fills created."
Require-False ([bool]$noReport.executionReportEntitiesCreated) "EXEC_SIM_R026_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Execution reports created."
Require-False ([bool]$noOrder.ordersCreated) "EXEC_SIM_R026_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders created."
Require-False ([bool]$noRoute.routesCreated) "EXEC_SIM_R026_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Routes created."
Require-False ([bool]$noRoute.submissionsCreated) "EXEC_SIM_R026_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Submissions created."
Require-False ([bool]$api.polygonApiCalled) "EXEC_SIM_R026_FAIL_API_CALL_DETECTED" "Polygon API called."
Require-False ([bool]$api.lmaxCalled) "EXEC_SIM_R026_FAIL_API_CALL_DETECTED" "LMAX called."
Require-False ([bool]$api.externalApiCalled) "EXEC_SIM_R026_FAIL_API_CALL_DETECTED" "External API called."
Require-False ([bool]$runtime.brokerActivationDetected) "EXEC_SIM_R026_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "Broker activation detected."
Require-False ([bool]$runtime.marketDataRequestSent) "EXEC_SIM_R026_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "MarketDataRequest sent."
Require-False ([bool]$runtime.marketDataResponseRead) "EXEC_SIM_R026_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "MarketDataResponse read."
Require-False ([bool]$runtime.schedulerServiceTimerPollingBackgroundJobIntroduced) "EXEC_SIM_R026_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler/service introduced."
Require-False ([bool]$noExternal.externalApiCalled) "EXEC_SIM_R026_FAIL_API_CALL_DETECTED" "External API detected."
Require-False ([bool]$noExternal.newSimulationExecuted) "EXEC_SIM_R026_FAIL_NEW_SIMULATION_OR_BACKTEST_EXECUTED" "New simulation detected."
Require-False ([bool]$noExternal.newBacktestExecuted) "EXEC_SIM_R026_FAIL_NEW_SIMULATION_OR_BACKTEST_EXECUTED" "New backtest detected."
Require-False ([bool]$noExternal.newTcaResultLinesProduced) "EXEC_SIM_R026_FAIL_NEW_TCA_RESULTS_PRODUCED" "New TCA lines detected."
Require-False ([bool]$noExternal.ordersFillsReportsRoutesSubmissionsCreated) "EXEC_SIM_R026_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order-domain output detected."
Require-False ([bool]$forbidden.forbiddenActionsDetected) "EXEC_SIM_R026_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected."

if ($evidence.dotnetBuildNoRestore -notlike "PASS*" -or $evidence.focusedR026Tests -notlike "PASS*" -or $evidence.unitTestsIfFeasible -notlike "PASS*" -or $evidence.validator -notlike "PASS*") {
    Fail-Gate "EXEC_SIM_R026_FAIL_BUILD_OR_TESTS" "Build/test/validator evidence is missing or not passing."
}

Write-Host "EXEC_SIM_R026_PASS_EXPANDED_TCA_RESULT_REVIEW_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R026_PASS_DATA_EXPANSION_DECISION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R026_PASS_PARAMETER_REFINEMENT_DECISION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R026_PASS_NO_NEW_SIMULATION_NO_ORDER_GATE_READY_NO_EXTERNAL"
