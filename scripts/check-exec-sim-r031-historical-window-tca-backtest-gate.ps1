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
    "phase-exec-sim-r031-summary.md",
    "phase-exec-sim-r031-historical-window-backtest-execution-contract.json",
    "phase-exec-sim-r031-historical-window-backtest-run-result.json",
    "phase-exec-sim-r031-r030-authorization-reference.json",
    "phase-exec-sim-r031-r029-row-validation-reference.json",
    "phase-exec-sim-r031-accepted-rows-used-rejected-rows-excluded.json",
    "phase-exec-sim-r031-quote-windows.json",
    "phase-exec-sim-r031-close-benchmarks.json",
    "phase-exec-sim-r031-feed-quality-results.json",
    "phase-exec-sim-r031-tca-result-line-contract.json",
    "phase-exec-sim-r031-tca-result-lines.json",
    "phase-exec-sim-r031-opening-build-tca-report.json",
    "phase-exec-sim-r031-closing-flatten-tca-report.json",
    "phase-exec-sim-r031-opening-vs-closing-comparison.json",
    "phase-exec-sim-r031-per-symbol-session-eurusd-report.json",
    "phase-exec-sim-r031-per-symbol-session-usdjpy-report.json",
    "phase-exec-sim-r031-per-symbol-session-audusd-report.json",
    "phase-exec-sim-r031-per-symbol-session-gbpusd-report.json",
    "phase-exec-sim-r031-per-symbol-session-nzdusd-report.json",
    "phase-exec-sim-r031-per-symbol-session-usdcad-report.json",
    "phase-exec-sim-r031-per-symbol-session-usdchf-report.json",
    "phase-exec-sim-r031-policy-comparison-report.json",
    "phase-exec-sim-r031-ranking-median-slippage.json",
    "phase-exec-sim-r031-ranking-p95-slippage.json",
    "phase-exec-sim-r031-ranking-fill-ratio.json",
    "phase-exec-sim-r031-ranking-residual.json",
    "phase-exec-sim-r031-ranking-spread-paid.json",
    "phase-exec-sim-r031-no-overnight-residual-penalty-report.json",
    "phase-exec-sim-r031-wakett-limit-baseline-report.json",
    "phase-exec-sim-r031-wakett-five-market-slices-report.json",
    "phase-exec-sim-r031-passive-until-urgency-report.json",
    "phase-exec-sim-r031-close-seeking-15m-report.json",
    "phase-exec-sim-r031-close-seeking-adaptive-report.json",
    "phase-exec-sim-r031-controlled-residual-cross-report.json",
    "phase-exec-sim-r031-benchmark-only-policy-report.json",
    "phase-exec-sim-r031-inversion-preservation.json",
    "phase-exec-sim-r031-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r031-cost-guidance-preservation.json",
    "phase-exec-sim-r031-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r031-no-db-import-audit.json",
    "phase-exec-sim-r031-no-persisted-sanitized-row-audit.json",
    "phase-exec-sim-r031-no-executable-schedule-audit.json",
    "phase-exec-sim-r031-no-child-slices-audit.json",
    "phase-exec-sim-r031-no-child-orders-audit.json",
    "phase-exec-sim-r031-no-real-fill-audit.json",
    "phase-exec-sim-r031-no-execution-report-audit.json",
    "phase-exec-sim-r031-no-order-created-audit.json",
    "phase-exec-sim-r031-no-route-no-submission-audit.json",
    "phase-exec-sim-r031-no-polygon-api-call-audit.json",
    "phase-exec-sim-r031-no-lmax-call-audit.json",
    "phase-exec-sim-r031-no-external-api-call-audit.json",
    "phase-exec-sim-r031-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r031-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r031-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r031-no-external-audit.json",
    "phase-exec-sim-r031-forbidden-actions-audit.json",
    "phase-exec-sim-r031-next-phase-recommendation.json",
    "phase-exec-sim-r031-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsDir $artifact))) {
        Fail-Gate "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "Required R031 artifact missing: $artifact"
    }
}

$contract = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-historical-window-backtest-execution-contract.json") "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING"
$run = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-historical-window-backtest-run-result.json") "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING"
$r030 = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-r030-authorization-reference.json") "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING"
$r029 = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-r029-row-validation-reference.json") "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING"
$rows = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-accepted-rows-used-rejected-rows-excluded.json") "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING"
$windows = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-quote-windows.json") "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING"
$close = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-close-benchmarks.json") "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING"
$feed = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-feed-quality-results.json") "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING"
$lineContract = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-tca-result-line-contract.json") "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING"
$lines = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-tca-result-lines.json") "EXEC_SIM_R031_FAIL_TCA_REPORTS_MISSING"
$opening = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-opening-build-tca-report.json") "EXEC_SIM_R031_FAIL_TCA_REPORTS_MISSING"
$closing = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-closing-flatten-tca-report.json") "EXEC_SIM_R031_FAIL_TCA_REPORTS_MISSING"
$comparison = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-opening-vs-closing-comparison.json") "EXEC_SIM_R031_FAIL_TCA_REPORTS_MISSING"
$policy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-policy-comparison-report.json") "EXEC_SIM_R031_FAIL_TCA_REPORTS_MISSING"
$wakettLimit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-wakett-limit-baseline-report.json") "EXEC_SIM_R031_FAIL_WAKETT_BASELINES_MISSING"
$wakettSlices = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-wakett-five-market-slices-report.json") "EXEC_SIM_R031_FAIL_WAKETT_BASELINES_MISSING"
$closeSeeking = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-close-seeking-15m-report.json") "EXEC_SIM_R031_FAIL_CLOSE_SEEKING_RESULTS_MISSING"
$adaptive = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-close-seeking-adaptive-report.json") "EXEC_SIM_R031_FAIL_CLOSE_SEEKING_RESULTS_MISSING"
$controlled = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-controlled-residual-cross-report.json") "EXEC_SIM_R031_FAIL_CLOSE_SEEKING_RESULTS_MISSING"
$benchmark = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-benchmark-only-policy-report.json") "EXEC_SIM_R031_FAIL_TCA_REPORTS_MISSING"
$penalty = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-no-overnight-residual-penalty-report.json") "EXEC_SIM_R031_FAIL_TCA_REPORTS_MISSING"
$inversion = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-inversion-preservation.json") "EXEC_SIM_R031_FAIL_USDJPY_CAVEAT_WEAKENED"
$direct = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-direct-cross-exclusion-preservation.json") "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING"
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-cost-guidance-preservation.json") "EXEC_SIM_R031_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$nonmajor = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-nonmajor-calibration-preservation.json") "EXEC_SIM_R031_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$noDb = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-no-db-import-audit.json") "EXEC_SIM_R031_FAIL_DB_IMPORT_OCCURRED"
$noRows = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-no-persisted-sanitized-row-audit.json") "EXEC_SIM_R031_FAIL_SANITIZED_QUOTE_ROWS_CREATED"
$noSchedule = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-no-executable-schedule-audit.json") "EXEC_SIM_R031_FAIL_EXECUTABLE_SCHEDULE_CREATED"
$noSlices = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-no-child-slices-audit.json") "EXEC_SIM_R031_FAIL_CHILD_SLICES_OR_ORDERS_CREATED"
$noChildOrders = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-no-child-orders-audit.json") "EXEC_SIM_R031_FAIL_CHILD_SLICES_OR_ORDERS_CREATED"
$noFill = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-no-real-fill-audit.json") "EXEC_SIM_R031_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$noReport = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-no-execution-report-audit.json") "EXEC_SIM_R031_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$noOrder = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-no-order-created-audit.json") "EXEC_SIM_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$noRoute = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-no-route-no-submission-audit.json") "EXEC_SIM_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$api = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-no-external-api-call-audit.json") "EXEC_SIM_R031_FAIL_API_CALL_DETECTED"
$runtime = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-no-broker-marketdata-runtime-audit.json") "EXEC_SIM_R031_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-usdjpy-caveat-preservation.json") "EXEC_SIM_R031_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-lmax-readonly-baseline-reference.json") "EXEC_SIM_R031_FAIL_API_CALL_DETECTED"
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-no-external-audit.json") "EXEC_SIM_R031_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-forbidden-actions-audit.json") "EXEC_SIM_R031_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-build-test-validator-evidence.json") "EXEC_SIM_R031_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$contract.historicalWindowBacktestExecutionContractCreated) "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "Execution contract missing."
if ($contract.SourceAuthorizationPhase -ne "EXEC-SIM-R030" -or $contract.SourceRowValidationPhase -ne "EXEC-SIM-R029") { Fail-Gate "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "Source phase reference mismatch." }
if ($contract.ProviderName -ne "PolygonOfflineFile" -or $contract.DatasetType -ne "HistoricalBboQuotes") { Fail-Gate "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "Provider/dataset mismatch." }
Require-Contains @($contract.SessionWindowCategories) "OpeningBuild" "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "OpeningBuild category missing."
Require-Contains @($contract.SessionWindowCategories) "ClosingFlatten" "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "ClosingFlatten category missing."
Require-True ([bool]$contract.AcceptedRowSetOnly) "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "Accepted row set only flag missing."
Require-True ([bool]$contract.RejectedRowsExcluded) "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "Rejected rows excluded flag missing."
Require-True ([bool]$contract.NoApiCall) "EXEC_SIM_R031_FAIL_API_CALL_DETECTED" "Contract allows API call."
Require-True ([bool]$contract.NoDbImport) "EXEC_SIM_R031_FAIL_DB_IMPORT_OCCURRED" "Contract allows DB import."
Require-True ([bool]$contract.NoPersistedSanitizedRows) "EXEC_SIM_R031_FAIL_SANITIZED_QUOTE_ROWS_CREATED" "Contract allows persisted sanitized rows."
Require-True ([bool]$contract.NoOrderDomainOutput) "EXEC_SIM_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Contract allows order-domain output."

Require-True ([bool]$run.historicalWindowBacktestRunResultCreated) "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "Run result missing."
if ($run.AuthorizedEntryCount -ne 14 -or $run.OpeningBuildEntryCount -ne 7 -or $run.ClosingFlattenEntryCount -ne 7 -or $run.SymbolCount -ne 7 -or $run.PolicyCount -ne 11 -or $run.QuoteWindowCount -ne 224 -or $run.CloseBenchmarkCount -ne 224 -or $run.FeedQualityResultCount -ne 14 -or $run.TcaResultLineCount -ne 2464) {
    Fail-Gate "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "Run result counts are not complete."
}
Require-Contains @($run.Classifications) "EXEC_SIM_R031_PASS_HISTORICAL_WINDOW_TCA_BACKTEST_READY_NO_EXTERNAL" "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "Success classification missing."
Require-True ([bool]$run.NoApiCall) "EXEC_SIM_R031_FAIL_API_CALL_DETECTED" "Run used API call."
Require-True ([bool]$run.NoBrokerRuntime) "EXEC_SIM_R031_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "Run used broker runtime."
Require-True ([bool]$run.NoDbImport) "EXEC_SIM_R031_FAIL_DB_IMPORT_OCCURRED" "Run imported DB rows."
Require-True ([bool]$run.NoPersistedSanitizedRows) "EXEC_SIM_R031_FAIL_SANITIZED_QUOTE_ROWS_CREATED" "Run persisted sanitized rows."
Require-True ([bool]$run.NoOrderDomainOutput) "EXEC_SIM_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Run created order-domain output."

Require-True ([bool]$r030.r030AuthorizationReferenceCreated) "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "R030 reference missing."
Require-True ([bool]$r030.ReadyForR031) "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "R030 did not authorize R031."
Require-False ([bool]$r030.R030BacktestExecuted) "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "R030 execution flag changed."
Require-True ([bool]$r029.r029RowValidationReferenceCreated) "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "R029 reference missing."
if ($r029.RowValidationResultCount -ne 14 -or $r029.TotalRejectedRowCount -ne 0 -or $r029.QuoteWindowReadinessCount -ne 224 -or $r029.CloseBenchmarkReadinessCount -ne 224 -or $r029.FeedQualityReadinessCount -ne 14) {
    Fail-Gate "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "R029 row validation reference counts mismatch."
}
Require-False ([bool]$r029.RowRevalidationExecutedInR031) "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "Rows were revalidated in R031."

Require-True ([bool]$rows.acceptedRowsUsedRejectedRowsExcludedCreated) "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "Accepted/rejected row usage missing."
Require-True ([bool]$rows.AcceptedRowSetOnly) "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "Accepted row set only missing."
Require-True ([bool]$rows.RejectedRowsExcluded) "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "Rejected rows excluded missing."
if ($rows.totalRejectedRowCount -ne 0 -or $rows.entryCount -ne 14 -or @($rows.perEntry).Count -ne 14) { Fail-Gate "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "Accepted/rejected row usage counts mismatch." }
Require-False ([bool]$rows.PersistedSanitizedQuoteRowsCreated) "EXEC_SIM_R031_FAIL_SANITIZED_QUOTE_ROWS_CREATED" "Persisted sanitized rows created."
Require-False ([bool]$rows.DbImportOccurred) "EXEC_SIM_R031_FAIL_DB_IMPORT_OCCURRED" "DB import occurred."

Require-True ([bool]$windows.quoteWindowsCreated) "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "Quote windows missing."
Require-True ([bool]$windows.BuiltFromAcceptedRows) "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "Quote windows not built from accepted rows."
if ($windows.resultCount -ne 224) { Fail-Gate "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "Quote-window count mismatch." }
Require-True ([bool]$close.closeBenchmarksCreated) "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "Close benchmarks missing."
Require-True ([bool]$close.BuiltFromAcceptedRows) "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "Close benchmarks not built from accepted rows."
if ($close.resultCount -ne 224) { Fail-Gate "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "Close-benchmark count mismatch." }
Require-True ([bool]$feed.feedQualityResultsCreated) "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "Feed quality missing."
Require-True ([bool]$feed.ComputedFromAcceptedRows) "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "Feed quality not computed from accepted rows."
if ($feed.resultCount -ne 14) { Fail-Gate "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "Feed-quality count mismatch." }

Require-True ([bool]$lineContract.tcaResultLineContractCreated) "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "TCA result line contract missing."
Require-True ([bool]$lineContract.FixtureOnly) "EXEC_SIM_R031_FAIL_TCA_REPORTS_MISSING" "Line contract not fixture-only."
Require-True ([bool]$lineContract.PaperOnly) "EXEC_SIM_R031_FAIL_TCA_REPORTS_MISSING" "Line contract not paper-only."
Require-True ([bool]$lineContract.NonExecutable) "EXEC_SIM_R031_FAIL_EXECUTABLE_SCHEDULE_CREATED" "Line contract executable."
Require-False ([bool]$lineContract.IsFill) "EXEC_SIM_R031_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Line contract represented as fill."
Require-False ([bool]$lineContract.IsExecutionReport) "EXEC_SIM_R031_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Line contract represented as execution report."
Require-False ([bool]$lineContract.IsOrder) "EXEC_SIM_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Line contract represented as order."
Require-False ([bool]$lineContract.IsChildSlice) "EXEC_SIM_R031_FAIL_CHILD_SLICES_OR_ORDERS_CREATED" "Line contract represented as child slice."
Require-False ([bool]$lineContract.HasBrokerRoute) "EXEC_SIM_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Line contract has broker route."
Require-True ([bool]$lines.tcaResultLinesCreated) "EXEC_SIM_R031_FAIL_TCA_REPORTS_MISSING" "TCA result lines missing."
if ($lines.ResultLineCount -ne 2464 -or @($lines.lines).Count -ne 2464) { Fail-Gate "EXEC_SIM_R031_FAIL_TCA_REPORTS_MISSING" "TCA result line count mismatch." }

foreach ($line in @($lines.lines)) {
    Require-True ([bool]$line.FixtureOnly) "EXEC_SIM_R031_FAIL_TCA_REPORTS_MISSING" "A line is not fixture-only."
    Require-True ([bool]$line.PaperOnly) "EXEC_SIM_R031_FAIL_TCA_REPORTS_MISSING" "A line is not paper-only."
    Require-True ([bool]$line.NonExecutable) "EXEC_SIM_R031_FAIL_EXECUTABLE_SCHEDULE_CREATED" "A line is executable."
    Require-False ([bool]$line.IsFill) "EXEC_SIM_R031_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "A line is represented as fill."
    Require-False ([bool]$line.IsExecutionReport) "EXEC_SIM_R031_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "A line is represented as execution report."
    Require-False ([bool]$line.IsOrder) "EXEC_SIM_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "A line is represented as order."
    Require-False ([bool]$line.IsChildSlice) "EXEC_SIM_R031_FAIL_CHILD_SLICES_OR_ORDERS_CREATED" "A line is represented as child slice."
    Require-False ([bool]$line.IsSubmitted) "EXEC_SIM_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "A line is submitted."
    Require-False ([bool]$line.HasBrokerRoute) "EXEC_SIM_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "A line has broker route."
}

Require-True ([bool]$opening.openingBuildTcaReportCreated) "EXEC_SIM_R031_FAIL_TCA_REPORTS_MISSING" "OpeningBuild report missing."
Require-True ([bool]$opening.PreviousEveningPlanningAllowed) "EXEC_SIM_R031_FAIL_TCA_REPORTS_MISSING" "OpeningBuild planning preservation missing."
Require-False ([bool]$opening.PreSessionExecutionAuthorized) "EXEC_SIM_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "OpeningBuild pre-session execution authorized."
Require-False ([bool]$opening.OvernightExposureAuthorized) "EXEC_SIM_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "OpeningBuild overnight exposure authorized."
Require-True ([bool]$closing.closingFlattenTcaReportCreated) "EXEC_SIM_R031_FAIL_TCA_REPORTS_MISSING" "ClosingFlatten report missing."
Require-True ([bool]$closing.MustEndFlat) "EXEC_SIM_R031_FAIL_TCA_REPORTS_MISSING" "ClosingFlatten MustEndFlat missing."
Require-False ([bool]$closing.OvernightAllowed) "EXEC_SIM_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "ClosingFlatten overnight allowed."
Require-True ([bool]$closing.NoOvernightCritical) "EXEC_SIM_R031_FAIL_TCA_REPORTS_MISSING" "ClosingFlatten no-overnight critical missing."
Require-True ([bool]$comparison.openingVsClosingComparisonCreated) "EXEC_SIM_R031_FAIL_TCA_REPORTS_MISSING" "Opening vs closing comparison missing."

foreach ($symbol in @("eurusd", "usdjpy", "audusd", "gbpusd", "nzdusd", "usdcad", "usdchf")) {
    $report = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-per-symbol-session-$symbol-report.json") "EXEC_SIM_R031_FAIL_TCA_REPORTS_MISSING"
    Require-True ([bool]$report.OpeningBuildPresent) "EXEC_SIM_R031_FAIL_TCA_REPORTS_MISSING" "$symbol OpeningBuild missing."
    Require-True ([bool]$report.ClosingFlattenPresent) "EXEC_SIM_R031_FAIL_TCA_REPORTS_MISSING" "$symbol ClosingFlatten missing."
}

Require-True ([bool]$policy.policyComparisonReportCreated) "EXEC_SIM_R031_FAIL_TCA_REPORTS_MISSING" "Policy comparison missing."
if ($policy.PolicyCount -ne 11 -or $policy.ResultLineCount -ne 2464) { Fail-Gate "EXEC_SIM_R031_FAIL_TCA_REPORTS_MISSING" "Policy comparison count mismatch." }
foreach ($rankingName in @("median-slippage", "p95-slippage", "fill-ratio", "residual", "spread-paid")) {
    $ranking = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r031-ranking-$rankingName.json") "EXEC_SIM_R031_FAIL_TCA_REPORTS_MISSING"
    Require-True ([bool]$ranking.rankingCreated) "EXEC_SIM_R031_FAIL_TCA_REPORTS_MISSING" "$rankingName ranking missing."
}
Require-True ([bool]$penalty.noOvernightResidualPenaltyReportCreated) "EXEC_SIM_R031_FAIL_TCA_REPORTS_MISSING" "No-overnight residual penalty report missing."
Require-True ([bool]$penalty.MustEndFlat) "EXEC_SIM_R031_FAIL_TCA_REPORTS_MISSING" "MustEndFlat missing in penalty report."
Require-False ([bool]$penalty.OvernightAllowed) "EXEC_SIM_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Overnight allowed in penalty report."

Require-True ([bool]$wakettLimit.reportCreated) "EXEC_SIM_R031_FAIL_WAKETT_BASELINES_MISSING" "Wakett limit report missing."
Require-True ([bool]$wakettLimit.BlockedAsProductionDefault) "EXEC_SIM_R031_FAIL_WAKETT_BASELINES_MISSING" "Wakett limit block missing."
Require-True ([bool]$wakettSlices.reportCreated) "EXEC_SIM_R031_FAIL_WAKETT_BASELINES_MISSING" "Wakett slices report missing."
Require-True ([bool]$wakettSlices.BlockedAsProductionDefault) "EXEC_SIM_R031_FAIL_WAKETT_BASELINES_MISSING" "Wakett slices block missing."
Require-True ([bool]$closeSeeking.reportCreated) "EXEC_SIM_R031_FAIL_CLOSE_SEEKING_RESULTS_MISSING" "CloseSeeking15m report missing."
Require-True ([bool]$adaptive.reportCreated) "EXEC_SIM_R031_FAIL_CLOSE_SEEKING_RESULTS_MISSING" "CloseSeeking adaptive report missing."
Require-True ([bool]$adaptive.MainFurtherTestingCandidate) "EXEC_SIM_R031_FAIL_CLOSE_SEEKING_RESULTS_MISSING" "Adaptive candidate marker missing."
Require-True ([bool]$controlled.reportCreated) "EXEC_SIM_R031_FAIL_CLOSE_SEEKING_RESULTS_MISSING" "Controlled residual cross report missing."
Require-True ([bool]$controlled.ConditionalOnOpportunityCostExceedingCrossingCost) "EXEC_SIM_R031_FAIL_CLOSE_SEEKING_RESULTS_MISSING" "Controlled residual condition missing."
Require-True ([bool]$benchmark.benchmarkOnlyPolicyReportCreated) "EXEC_SIM_R031_FAIL_TCA_REPORTS_MISSING" "Benchmark-only report missing."

Require-True ([bool]$inversion.inversionPreservationCreated) "EXEC_SIM_R031_FAIL_USDJPY_CAVEAT_WEAKENED" "Inversion preservation missing."
Require-True ([bool]$inversion.usdJpyCaveatPreserved) "EXEC_SIM_R031_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat weakened."
Require-False ([bool]$inversion.audusdMisclassifiedFailed) "EXEC_SIM_R031_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD misclassified."
$usdjpyValidation = @($inversion.validations | Where-Object { $_.ExecutionTradableSymbol -eq "USDJPY" })[0]
if ($null -eq $usdjpyValidation -or $usdjpyValidation.NormalizedPortfolioSymbol -ne "JPYUSD" -or -not [bool]$usdjpyValidation.RequiresInversion -or $usdjpyValidation.SecurityID -ne "4004" -or $usdjpyValidation.SecurityIDSource -ne "8") {
    Fail-Gate "EXEC_SIM_R031_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY inversion/caveat mismatch."
}
foreach ($pair in @(@("USDCAD","CADUSD"), @("USDCHF","CHFUSD"))) {
    $item = @($inversion.validations | Where-Object { $_.ExecutionTradableSymbol -eq $pair[0] })[0]
    if ($null -eq $item -or $item.NormalizedPortfolioSymbol -ne $pair[1] -or -not [bool]$item.RequiresInversion) {
        Fail-Gate "EXEC_SIM_R031_FAIL_USDJPY_CAVEAT_WEAKENED" "$($pair[0]) inversion mismatch."
    }
}
Require-True ([bool]$direct.directCrossExclusionPreserved) "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "Direct-cross exclusion missing."
Require-False ([bool]$direct.directCrossIncluded) "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "Direct-cross included."
Require-True ([bool]$direct.mandatoryNettingBeforeExecution) "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "Netting-first weakened."
Require-False ([bool]$direct.guidanceWeakened) "EXEC_SIM_R031_FAIL_BACKTEST_RESULT_MISSING" "Direct-cross guidance weakened."
Require-True ([bool]$cost.fiveUsdPerMillionBestCaseMajorOnly) "EXEC_SIM_R031_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million not best-case major-only."
Require-False ([bool]$cost.fiveUsdPerMillionUniversalized) "EXEC_SIM_R031_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million universalized."
Require-True ([bool]$nonmajor.RequiresLiquidityCalibration) "EXEC_SIM_R031_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "Nonmajor calibration weakened."

Require-False ([bool]$noDb.quotesImportedIntoDb) "EXEC_SIM_R031_FAIL_DB_IMPORT_OCCURRED" "Quotes imported into DB."
Require-False ([bool]$noRows.persistedSanitizedQuoteRowsCreated) "EXEC_SIM_R031_FAIL_SANITIZED_QUOTE_ROWS_CREATED" "Persisted sanitized rows created."
Require-False ([bool]$noSchedule.executableSchedulesCreated) "EXEC_SIM_R031_FAIL_EXECUTABLE_SCHEDULE_CREATED" "Executable schedules created."
Require-False ([bool]$noSlices.childSlicesCreated) "EXEC_SIM_R031_FAIL_CHILD_SLICES_OR_ORDERS_CREATED" "Child slices created."
Require-False ([bool]$noChildOrders.childOrdersCreated) "EXEC_SIM_R031_FAIL_CHILD_SLICES_OR_ORDERS_CREATED" "Child orders created."
Require-False ([bool]$noFill.realFillsCreated) "EXEC_SIM_R031_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Real fills created."
Require-False ([bool]$noReport.executionReportEntitiesCreated) "EXEC_SIM_R031_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Execution reports created."
Require-False ([bool]$noOrder.ordersCreated) "EXEC_SIM_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders created."
Require-False ([bool]$noRoute.routesCreated) "EXEC_SIM_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Routes created."
Require-False ([bool]$noRoute.submissionsCreated) "EXEC_SIM_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Submissions created."
Require-False ([bool]$api.externalApiCalled) "EXEC_SIM_R031_FAIL_API_CALL_DETECTED" "External API called."
Require-False ([bool]$api.polygonApiCalled) "EXEC_SIM_R031_FAIL_API_CALL_DETECTED" "Polygon API called."
Require-False ([bool]$api.lmaxCalled) "EXEC_SIM_R031_FAIL_API_CALL_DETECTED" "LMAX called."
Require-False ([bool]$runtime.brokerActivationDetected) "EXEC_SIM_R031_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "Broker activation detected."
Require-False ([bool]$runtime.socketOpened) "EXEC_SIM_R031_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "Socket opened."
Require-False ([bool]$runtime.tlsOpened) "EXEC_SIM_R031_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "TLS opened."
Require-False ([bool]$runtime.fixOpened) "EXEC_SIM_R031_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "FIX opened."
Require-False ([bool]$runtime.marketDataRequestSent) "EXEC_SIM_R031_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "MarketDataRequest sent."
Require-False ([bool]$runtime.marketDataResponseRead) "EXEC_SIM_R031_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "MarketDataResponse read."
Require-False ([bool]$runtime.schedulerServiceTimerPollingBackgroundJobIntroduced) "EXEC_SIM_R031_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler/timer/background job introduced."
Require-True ([bool]$usdjpy.usdjpyCaveatPreserved) "EXEC_SIM_R031_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat preservation missing."
Require-True ([bool]$lmax.referenceOnly) "EXEC_SIM_R031_FAIL_API_CALL_DETECTED" "LMAX not reference-only."
Require-False ([bool]$lmax.lmaxCalledInR031) "EXEC_SIM_R031_FAIL_API_CALL_DETECTED" "LMAX called in R031."
Require-False ([bool]$noExternal.externalApiCalled) "EXEC_SIM_R031_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "External API called."
Require-False ([bool]$noExternal.filesDownloaded) "EXEC_SIM_R031_FAIL_DOWNLOAD_EXECUTED" "Files downloaded."
Require-False ([bool]$noExternal.brokerRuntimeActionDetected) "EXEC_SIM_R031_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "Broker runtime action detected."
Require-False ([bool]$noExternal.ordersFillsReportsRoutesSubmissionsCreated) "EXEC_SIM_R031_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order-domain output created."
Require-False ([bool]$forbidden.forbiddenActionsDetected) "EXEC_SIM_R031_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden actions detected."

if ($evidence.dotnetBuildNoRestore -notlike "PASS*" -or $evidence.focusedTests -notlike "PASS*" -or $evidence.unitTests -notlike "PASS*" -or $evidence.validator -notlike "PASS*") {
    Fail-Gate "EXEC_SIM_R031_FAIL_BUILD_OR_TESTS" "Build/test/validator evidence is missing or not passing."
}

Write-Host "EXEC_SIM_R031_PASS_HISTORICAL_WINDOW_TCA_BACKTEST_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R031_PASS_OPENING_CLOSING_TCA_COMPARISON_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R031_PASS_SESSION_WINDOW_POLICY_RANKINGS_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R031_PASS_NO_DB_IMPORT_NO_REAL_FILL_NO_ORDER_GATE_READY_NO_EXTERNAL"
