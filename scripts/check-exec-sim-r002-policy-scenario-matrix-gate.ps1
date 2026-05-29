param(
    [string]$ArtifactDirectory = "artifacts/readiness/execution-sim"
)

$ErrorActionPreference = "Stop"

function Fail-Gate {
    param([string]$Classification, [string]$Message)
    Write-Error "$Classification`: $Message"
    exit 1
}

function Read-JsonArtifact {
    param([string]$Path, [string]$MissingClassification)
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail-Gate $MissingClassification "Missing required artifact: $Path"
    }

    try { return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json }
    catch { Fail-Gate $MissingClassification "Artifact is not valid JSON: $Path" }
}

function Require-True {
    param([bool]$Value, [string]$Classification, [string]$Message)
    if (-not $Value) { Fail-Gate $Classification $Message }
}

function Require-False {
    param([bool]$Value, [string]$Classification, [string]$Message)
    if ($Value) { Fail-Gate $Classification $Message }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repoRoot $ArtifactDirectory

$requiredArtifacts = @{
    "phase-exec-sim-r002-summary.md" = "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING"
    "phase-exec-sim-r002-scenario-matrix-contract.json" = "EXEC_SIM_R002_FAIL_SCENARIO_MATRIX_MISSING"
    "phase-exec-sim-r002-scenario-matrix.json" = "EXEC_SIM_R002_FAIL_SCENARIO_MATRIX_MISSING"
    "phase-exec-sim-r002-instrument-liquidity-buckets.json" = "EXEC_SIM_R002_FAIL_SCENARIO_MATRIX_MISSING"
    "phase-exec-sim-r002-usd-pair-normalization-preservation.json" = "EXEC_SIM_R002_FAIL_SCENARIO_MATRIX_MISSING"
    "phase-exec-sim-r002-direct-cross-signal-only-handling.json" = "EXEC_SIM_R002_FAIL_SCENARIO_MATRIX_MISSING"
    "phase-exec-sim-r002-policy-comparison-report.json" = "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING"
    "phase-exec-sim-r002-policy-ranking-median-slippage.json" = "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING"
    "phase-exec-sim-r002-policy-ranking-p95-slippage.json" = "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING"
    "phase-exec-sim-r002-policy-ranking-fill-ratio.json" = "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING"
    "phase-exec-sim-r002-policy-ranking-residual.json" = "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING"
    "phase-exec-sim-r002-policy-ranking-spread-paid.json" = "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING"
    "phase-exec-sim-r002-worst-case-scenarios-by-policy.json" = "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING"
    "phase-exec-sim-r002-wakett-limit-baseline-comparison.json" = "EXEC_SIM_R002_FAIL_WAKETT_BASELINES_MISSING"
    "phase-exec-sim-r002-wakett-five-market-slices-comparison.json" = "EXEC_SIM_R002_FAIL_WAKETT_BASELINES_MISSING"
    "phase-exec-sim-r002-close-seeking-adaptive-comparison.json" = "EXEC_SIM_R002_FAIL_CLOSE_SEEKING_RESULT_MISSING"
    "phase-exec-sim-r002-controlled-residual-cross-comparison.json" = "EXEC_SIM_R002_FAIL_CLOSE_SEEKING_RESULT_MISSING"
    "phase-exec-sim-r002-feed-quality-blocking-report.json" = "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING"
    "phase-exec-sim-r002-spread-regime-report.json" = "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING"
    "phase-exec-sim-r002-residual-risk-report.json" = "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING"
    "phase-exec-sim-r002-quote-gap-staleness-report.json" = "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING"
    "phase-exec-sim-r002-major-pair-5usd-bestcase-only.json" = "EXEC_SIM_R002_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
    "phase-exec-sim-r002-nonmajor-em-cnh-calibration-required.json" = "EXEC_SIM_R002_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
    "phase-exec-sim-r002-no-real-fill-audit.json" = "EXEC_SIM_R002_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
    "phase-exec-sim-r002-no-execution-report-audit.json" = "EXEC_SIM_R002_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
    "phase-exec-sim-r002-no-order-created-audit.json" = "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-exec-sim-r002-no-route-no-submission-audit.json" = "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-exec-sim-r002-lineage-preservation.json" = "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING"
    "phase-exec-sim-r002-usdjpy-caveat-preservation.json" = "EXEC_SIM_R002_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-exec-sim-r002-lmax-readonly-baseline-reference.json" = "EXEC_SIM_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-exec-sim-r002-no-external-audit.json" = "EXEC_SIM_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-exec-sim-r002-forbidden-actions-audit.json" = "EXEC_SIM_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-exec-sim-r002-next-phase-recommendation.json" = "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING"
    "phase-exec-sim-r002-build-test-validator-evidence.json" = "EXEC_SIM_R002_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $entry.Key))) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$summary = Get-Content -LiteralPath (Join-Path $artifactRoot "phase-exec-sim-r002-summary.md") -Raw
if ([string]::IsNullOrWhiteSpace($summary)) {
    Fail-Gate "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING" "Summary is empty."
}

$contract = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-scenario-matrix-contract.json") "EXEC_SIM_R002_FAIL_SCENARIO_MATRIX_MISSING"
$matrix = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-scenario-matrix.json") "EXEC_SIM_R002_FAIL_SCENARIO_MATRIX_MISSING"
$buckets = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-instrument-liquidity-buckets.json") "EXEC_SIM_R002_FAIL_SCENARIO_MATRIX_MISSING"
$normalization = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-usd-pair-normalization-preservation.json") "EXEC_SIM_R002_FAIL_SCENARIO_MATRIX_MISSING"
$directCross = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-direct-cross-signal-only-handling.json") "EXEC_SIM_R002_FAIL_SCENARIO_MATRIX_MISSING"
$comparison = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-policy-comparison-report.json") "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING"
$median = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-policy-ranking-median-slippage.json") "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING"
$p95 = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-policy-ranking-p95-slippage.json") "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING"
$fillRanking = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-policy-ranking-fill-ratio.json") "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING"
$residualRanking = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-policy-ranking-residual.json") "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING"
$spreadRanking = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-policy-ranking-spread-paid.json") "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING"
$worstCases = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-worst-case-scenarios-by-policy.json") "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING"
$wakettLimit = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-wakett-limit-baseline-comparison.json") "EXEC_SIM_R002_FAIL_WAKETT_BASELINES_MISSING"
$wakettSlices = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-wakett-five-market-slices-comparison.json") "EXEC_SIM_R002_FAIL_WAKETT_BASELINES_MISSING"
$adaptive = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-close-seeking-adaptive-comparison.json") "EXEC_SIM_R002_FAIL_CLOSE_SEEKING_RESULT_MISSING"
$controlled = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-controlled-residual-cross-comparison.json") "EXEC_SIM_R002_FAIL_CLOSE_SEEKING_RESULT_MISSING"
$feed = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-feed-quality-blocking-report.json") "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING"
$spread = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-spread-regime-report.json") "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING"
$residual = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-residual-risk-report.json") "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING"
$gap = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-quote-gap-staleness-report.json") "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING"
$major5 = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-major-pair-5usd-bestcase-only.json") "EXEC_SIM_R002_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$nonMajor = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-nonmajor-em-cnh-calibration-required.json") "EXEC_SIM_R002_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$fillAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-no-real-fill-audit.json") "EXEC_SIM_R002_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$reportAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-no-execution-report-audit.json") "EXEC_SIM_R002_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$orderAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-no-order-created-audit.json") "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$routeAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-no-route-no-submission-audit.json") "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$lineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-lineage-preservation.json") "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-usdjpy-caveat-preservation.json") "EXEC_SIM_R002_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-lmax-readonly-baseline-reference.json") "EXEC_SIM_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-no-external-audit.json") "EXEC_SIM_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-forbidden-actions-audit.json") "EXEC_SIM_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r002-build-test-validator-evidence.json") "EXEC_SIM_R002_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$contract.contractCreated) "EXEC_SIM_R002_FAIL_SCENARIO_MATRIX_MISSING" "Scenario matrix contract missing."
foreach ($dimension in @("InstrumentLiquidityBucket", "SpreadRegime", "ResidualSize", "DriftRegime", "FeedQualityRegime", "TimeToClosePhase", "Policy")) {
    Require-True (@($contract.dimensions) -contains $dimension) "EXEC_SIM_R002_FAIL_SCENARIO_MATRIX_MISSING" "Dimension missing: $dimension"
}
foreach ($metric in @("FillRatio", "ResidualAtClose", "SlippageVsCloseBps", "SlippageVsCloseUsdPerMillion", "SpreadPaidBps", "SpreadPaidUsdPerMillion", "EstimatedOpportunityCost", "EstimatedNonFillCost", "BlockReason")) {
    Require-True (@($contract.requiredTcaMetrics) -contains $metric) "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING" "Metric missing: $metric"
}
Require-True ([bool]$contract.fixtureOnly -and [bool]$contract.paperOnly -and [bool]$contract.nonExecutable -and [bool]$contract.noExternal) "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Contract not fixture-only/no-external/non-executable."

Require-True ([bool]$matrix.scenarioMatrixCreated) "EXEC_SIM_R002_FAIL_SCENARIO_MATRIX_MISSING" "Scenario matrix missing."
Require-True ([int]$matrix.lineCount -ge 20) "EXEC_SIM_R002_FAIL_SCENARIO_MATRIX_MISSING" "Scenario matrix too small."
foreach ($bucket in @("MajorUsdPair", "NonMajorUsdPair", "EmCnhHighCalibration", "MissingConvention", "DirectCrossSignalOnly")) {
    Require-True (@($matrix.dimensionsCovered.instrumentLiquidityBucket) -contains $bucket) "EXEC_SIM_R002_FAIL_SCENARIO_MATRIX_MISSING" "Bucket dimension missing: $bucket"
}
foreach ($spreadRegime in @("TightSpread", "NormalSpread", "WideSpread", "ExtremeSpread")) {
    Require-True (@($matrix.dimensionsCovered.spreadRegime) -contains $spreadRegime) "EXEC_SIM_R002_FAIL_SCENARIO_MATRIX_MISSING" "Spread dimension missing: $spreadRegime"
}
foreach ($feedRegime in @("GoodFeed", "MinorGap", "MajorGap", "NoQuoteNearClose", "StaleQuoteNearClose")) {
    Require-True (@($matrix.dimensionsCovered.feedQuality) -contains $feedRegime) "EXEC_SIM_R002_FAIL_SCENARIO_MATRIX_MISSING" "Feed dimension missing: $feedRegime"
}
Require-True ([bool]$matrix.allOutputsFixtureOnly -and [bool]$matrix.allOutputsPaperOnly -and [bool]$matrix.allOutputsNonExecutable) "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Matrix outputs executable."

Require-True ([bool]$buckets.bucketsCreated) "EXEC_SIM_R002_FAIL_SCENARIO_MATRIX_MISSING" "Buckets missing."
foreach ($symbol in @("AUDUSD", "EURUSD", "GBPUSD", "NZDUSD", "USDJPY", "USDCAD", "USDCHF")) {
    Require-True (@($buckets.majorUsdPair) -contains $symbol) "EXEC_SIM_R002_FAIL_SCENARIO_MATRIX_MISSING" "Major bucket missing: $symbol"
}
Require-True ([bool]$buckets.nonMajorCalibrationRequired -and [bool]$buckets.emCnhCalibrationRequired) "EXEC_SIM_R002_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "Calibration not required for non-major/EM."
Require-False ([bool]$buckets.directCrossExecutionAllowedByDefault) "EXEC_SIM_R002_FAIL_SCENARIO_MATRIX_MISSING" "Direct cross allowed by default."

Require-True ([bool]$normalization.usdPairNormalizationPreserved -and [bool]$normalization.usesExecutionAlgoR002Normalization) "EXEC_SIM_R002_FAIL_SCENARIO_MATRIX_MISSING" "USD-pair normalization not preserved."
Require-True ([bool]$normalization.executionUniverseUsdPairOnly -and [bool]$normalization.rawCrossesAreSignalInputsOnly) "EXEC_SIM_R002_FAIL_SCENARIO_MATRIX_MISSING" "Execution universe not USD-pair signal-only."
Require-False ([bool]$normalization.ordersCreated) "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Normalization creates orders."
Require-False ([bool]$normalization.routesCreated) "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Normalization creates routes."

Require-True ([bool]$directCross.directCrossSignalOnlyHandlingCreated) "EXEC_SIM_R002_FAIL_SCENARIO_MATRIX_MISSING" "Direct-cross handling missing."
Require-False ([bool]$directCross.directCrossExecutionAllowedByDefault) "EXEC_SIM_R002_FAIL_SCENARIO_MATRIX_MISSING" "Direct-cross default enabled."
Require-True ([bool]$directCross.requiresMandatoryNettingFirst) "EXEC_SIM_R002_FAIL_SCENARIO_MATRIX_MISSING" "Netting-first missing."
Require-False ([bool]$directCross.directCrossesSimulatedAsExecutionInstruments) "EXEC_SIM_R002_FAIL_SCENARIO_MATRIX_MISSING" "Direct crosses simulated as execution instruments."

Require-True ([bool]$comparison.policyComparisonReportCreated) "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING" "Policy comparison missing."
foreach ($policy in @("WakettPureLimitUntilClose", "WakettFiveMarketSlicesAroundClose", "CloseSeeking15mAdaptive", "ControlledResidualCross", "ManualReview", "DoNotTrade")) {
    Require-True (@($comparison.policiesCompared) -contains $policy) "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING" "Policy missing: $policy"
}
Require-False ([bool]$comparison.ordersCreated) "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Comparison creates orders."
Require-False ([bool]$comparison.fillsCreated) "EXEC_SIM_R002_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Comparison creates fills."
Require-False ([bool]$comparison.executionReportsCreated) "EXEC_SIM_R002_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Comparison creates reports."
Require-False ([bool]$comparison.routesCreated) "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Comparison creates routes."
Require-False ([bool]$comparison.submissionsCreated) "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Comparison creates submissions."

foreach ($ranking in @($median, $p95, $fillRanking, $residualRanking, $spreadRanking)) {
    Require-True ([bool]$ranking.rankingCreated) "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING" "Ranking missing: $($ranking.metric)"
    Require-True (@($ranking.rankings).Count -gt 0) "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING" "Ranking empty: $($ranking.metric)"
}
Require-True ([bool]$worstCases.worstCaseScenariosCreated) "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING" "Worst cases missing."

Require-True ([bool]$wakettLimit.comparisonCreated -and [bool]$wakettLimit.negativeBaseline) "EXEC_SIM_R002_FAIL_WAKETT_BASELINES_MISSING" "Wakett limit comparison missing."
Require-False ([bool]$wakettLimit.productionDefaultAllowed) "EXEC_SIM_R002_FAIL_WAKETT_BASELINES_MISSING" "Wakett limit allowed as default."
Require-True ([decimal]$wakettLimit.residualAtClose -ge 0.75) "EXEC_SIM_R002_FAIL_WAKETT_BASELINES_MISSING" "Wakett limit residual not high."
Require-True ([bool]$wakettSlices.comparisonCreated -and [bool]$wakettSlices.negativeBaseline) "EXEC_SIM_R002_FAIL_WAKETT_BASELINES_MISSING" "Wakett slices comparison missing."
Require-False ([bool]$wakettSlices.productionDefaultAllowed) "EXEC_SIM_R002_FAIL_WAKETT_BASELINES_MISSING" "Wakett slices allowed as default."
Require-True ([bool]$wakettSlices.mechanicalSpreadCrossing -and [decimal]$wakettSlices.spreadPaidBps -ge 5) "EXEC_SIM_R002_FAIL_WAKETT_BASELINES_MISSING" "Wakett slices spread crossing not captured."
Require-True ([bool]$adaptive.comparisonCreated -and [bool]$adaptive.balancesPassiveFillAndResidualControl) "EXEC_SIM_R002_FAIL_CLOSE_SEEKING_RESULT_MISSING" "Adaptive comparison missing."
Require-True ([bool]$controlled.comparisonCreated -and [bool]$controlled.opportunityCostExceedsCrossingCost -and [bool]$controlled.helpsOnlyWhenOpportunityCostExceedsCrossingCost) "EXEC_SIM_R002_FAIL_CLOSE_SEEKING_RESULT_MISSING" "Controlled residual comparison missing."
Require-False ([bool]$controlled.blindMarketCrossing) "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Blind market crossing allowed."

Require-True ([bool]$feed.reportCreated) "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING" "Feed report missing."
Require-False ([bool]$feed.unsafeFeedCanExecute) "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Unsafe feed can execute."
Require-True ([bool]$spread.reportCreated -and [bool]$spread.spreadTooWideBlocksOrManualReviewsCrossing) "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING" "Spread regime report missing."
Require-True ([bool]$residual.reportCreated -and [bool]$residual.controlledResidualCrossRequiresCostJustification) "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING" "Residual report missing."
Require-True ([bool]$gap.reportCreated -and [bool]$gap.quoteGapAndStalenessHandled) "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING" "Gap/staleness report missing."
Require-False ([bool]$gap.executionAllowedWhenNoQuoteNearClose) "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Execution allowed with no quote."
Require-False ([bool]$gap.executionAllowedWhenStaleQuoteNearClose) "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Execution allowed with stale quote."

Require-True ([bool]$major5.fiveUsdPerMillionBestCaseOnly) "EXEC_SIM_R002_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/m not best-case only."
Require-False ([bool]$major5.fiveUsdPerMillionUniversalized) "EXEC_SIM_R002_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/m universalized."
Require-True ([int]$major5.baseCaseMajorTargetUsdPerMillionLow -ge 10 -and [int]$major5.baseCaseMajorTargetUsdPerMillionHigh -le 15) "EXEC_SIM_R002_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "Base case major target wrong."
Require-True ([bool]$nonMajor.reportCreated -and [bool]$nonMajor.nonMajorCalibrationRequired -and [bool]$nonMajor.emCnhCalibrationRequired -and [bool]$nonMajor.scandiCalibrationRequired -and [bool]$nonMajor.cnhCalibrationRequired) "EXEC_SIM_R002_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "Non-major/EM/CNH calibration missing."
Require-False ([bool]$nonMajor.usesMajorBestCaseFiveUsdPerMillion) "EXEC_SIM_R002_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "Non-major uses major best-case 5 USD/m."

Require-True ([bool]$fillAudit.auditCreated) "EXEC_SIM_R002_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "No-fill audit missing."
Require-False ([bool]$fillAudit.simulationOutputsAreRealFills) "EXEC_SIM_R002_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Simulation outputs are fills."
Require-False ([bool]$fillAudit.realFillsCreated) "EXEC_SIM_R002_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Real fills created."
Require-False ([bool]$fillAudit.fillDomainEntityCreated) "EXEC_SIM_R002_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill entity created."
Require-True ([bool]$reportAudit.auditCreated) "EXEC_SIM_R002_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "No-report audit missing."
Require-False ([bool]$reportAudit.executionReportsCreated) "EXEC_SIM_R002_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Reports created."
Require-False ([bool]$reportAudit.brokerExecutionReportsCreated) "EXEC_SIM_R002_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Broker reports created."
Require-True ([bool]$orderAudit.auditCreated) "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No-order audit missing."
Require-False ([bool]$orderAudit.ordersCreated) "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders created."
Require-False ([bool]$orderAudit.executableOrdersCreated) "EXEC_SIM_R002_FAIL_EXECUTABLE_ORDER_CREATED" "Executable orders created."
Require-False ([bool]$orderAudit.omsOrdersCreated) "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "OMS orders created."
Require-False ([bool]$orderAudit.brokerOrdersCreated) "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Broker orders created."
Require-True ([bool]$routeAudit.auditCreated) "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No-route audit missing."
Require-False ([bool]$routeAudit.routesCreated) "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Routes created."
Require-False ([bool]$routeAudit.submissionsCreated) "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Submissions created."

Require-True ([bool]$lineage.lineagePreserved -and [bool]$lineage.execAlgoR002LineagePreserved -and [bool]$lineage.execSimR001LineagePreserved) "EXEC_SIM_R002_FAIL_POLICY_COMPARISON_MISSING" "Lineage missing."
Require-True ([bool]$usdjpy.usdjpyCaveatPreserved) "EXEC_SIM_R002_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
Require-True ([string]$usdjpy.securityId -eq "4004" -and [string]$usdjpy.securityIdSource -eq "8") "EXEC_SIM_R002_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID caveat wrong."
Require-False ([bool]$usdjpy.weakened) "EXEC_SIM_R002_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat weakened."
Require-False ([bool]$usdjpy.audusdMisclassifiedFailed) "EXEC_SIM_R002_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD misclassified."
Require-True ([bool]$lmax.referenceOnly) "EXEC_SIM_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX not reference-only."
Require-False ([bool]$lmax.brokerCalled) "EXEC_SIM_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker called."
Require-False ([bool]$lmax.lmaxCalled) "EXEC_SIM_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX called."
Require-False ([bool]$lmax.liveMarketDataRequested) "EXEC_SIM_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Live market data requested."

Require-False ([bool]$noExternal.brokerActivation) "EXEC_SIM_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker activation."
Require-False ([bool]$noExternal.socketTlsFixRuntimeAction) "EXEC_SIM_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Socket/TLS/FIX runtime."
Require-False ([bool]$noExternal.liveMarketRuntimeAction) "EXEC_SIM_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Live market runtime."
Require-False ([bool]$noExternal.marketDataRequestAttempted) "EXEC_SIM_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "MarketDataRequest."
Require-False ([bool]$noExternal.marketDataResponseRead) "EXEC_SIM_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "MarketDataResponse."
Require-False ([bool]$noExternal.apiWorkerLiveGatewayEnabled) "EXEC_SIM_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API/Worker live gateway."
Require-False ([bool]$noExternal.schedulerPollingServiceTimerBackgroundJob) "EXEC_SIM_R002_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler/service/polling."
Require-False ([bool]$noExternal.automaticExecution) "EXEC_SIM_R002_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Automatic execution."
Require-False ([bool]$noExternal.ordersCreated) "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders."
Require-False ([bool]$noExternal.executableOrdersCreated) "EXEC_SIM_R002_FAIL_EXECUTABLE_ORDER_CREATED" "Executable orders."
Require-False ([bool]$noExternal.fillsCreated) "EXEC_SIM_R002_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fills."
Require-False ([bool]$noExternal.executionReportsCreated) "EXEC_SIM_R002_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Reports."
Require-False ([bool]$noExternal.brokerExecutionReportsCreated) "EXEC_SIM_R002_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Broker reports."
Require-False ([bool]$noExternal.routesCreated) "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Routes."
Require-False ([bool]$noExternal.submissionsCreated) "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Submissions."
Require-False ([bool]$noExternal.liveTradingPathIntroduced) "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Live trading."
Require-False ([bool]$noExternal.paperLedgerCommit) "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Paper ledger commit."
Require-False ([bool]$noExternal.livePositionMutation) "EXEC_SIM_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Live position mutation."
Require-False ([bool]$noExternal.brokerPositionMutation) "EXEC_SIM_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker position mutation."
Require-False ([bool]$noExternal.productionLedgerMutation) "EXEC_SIM_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Production ledger mutation."
Require-False ([bool]$noExternal.tradingStateMutation) "EXEC_SIM_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Trading state mutation."
Require-False ([bool]$noExternal.replayOrShadowReplay) "EXEC_SIM_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Replay/shadow replay."
Require-False ([bool]$noExternal.secretOrRawPayloadSerialization) "EXEC_SIM_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Secret/raw serialization."

Require-False ([bool]$forbidden.brokerActivationDetected) "EXEC_SIM_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden broker activation."
Require-False ([bool]$forbidden.socketTlsFixMarketDataRuntimeDetected) "EXEC_SIM_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden socket/TLS/FIX/MarketData."
Require-False ([bool]$forbidden.liveMarketDataRequestOrResponseDetected) "EXEC_SIM_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden live MarketData."
Require-False ([bool]$forbidden.schedulerPollingServiceTimerBackgroundJobIntroduced) "EXEC_SIM_R002_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Forbidden scheduler/service."
Require-False ([bool]$forbidden.automaticExecutionIntroduced) "EXEC_SIM_R002_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Forbidden automatic execution."
Require-False ([bool]$forbidden.orderSubmissionIntroduced) "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden submission."
Require-False ([bool]$forbidden.executableOrderCreated) "EXEC_SIM_R002_FAIL_EXECUTABLE_ORDER_CREATED" "Forbidden executable order."
Require-False ([bool]$forbidden.omsOrBrokerOrderCreated) "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden order."
Require-False ([bool]$forbidden.fillOrExecutionReportIntroduced) "EXEC_SIM_R002_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Forbidden fill/report."
Require-False ([bool]$forbidden.routeOrSubmissionIntroduced) "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden route/submission."
Require-False ([bool]$forbidden.stateMutationIntroduced) "EXEC_SIM_R002_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden state mutation."
Require-False ([bool]$forbidden.paperLedgerCommitOccurred) "EXEC_SIM_R002_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden paper ledger commit."

Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "EXEC_SIM_R002_FAIL_BUILD_OR_TESTS" "Build/test evidence missing."
Require-True ([string]$evidence.dotnetBuildNoRestore -eq "PASS") "EXEC_SIM_R002_FAIL_BUILD_OR_TESTS" "Build failed."
Require-True ([string]$evidence.focusedTests -like "PASS*") "EXEC_SIM_R002_FAIL_BUILD_OR_TESTS" "Focused tests failed."
Require-True ([string]$evidence.unitTests -like "PASS*") "EXEC_SIM_R002_FAIL_BUILD_OR_TESTS" "Unit tests failed."
Require-True ([string]$evidence.validator -eq "PASS") "EXEC_SIM_R002_FAIL_BUILD_OR_TESTS" "Validator evidence missing."

Write-Host "EXEC_SIM_R002_PASS_SCENARIO_MATRIX_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R002_PASS_POLICY_COMPARISON_REPORT_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R002_PASS_WAKETT_VS_CLOSE_SEEKING_TCA_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R002_PASS_COST_BUCKET_CALIBRATION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R002_PASS_NO_REAL_FILL_NO_ORDER_SIMULATION_READY_NO_EXTERNAL"
