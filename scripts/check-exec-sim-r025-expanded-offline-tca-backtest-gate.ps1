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

function Require-Symbol {
    param(
        [object[]]$Items,
        [string]$ExecutionTradableSymbol,
        [string]$NormalizedPortfolioSymbol,
        [bool]$RequiresInversion,
        [string]$FailureClassification
    )
    $matches = @($Items | Where-Object { $_.ExecutionTradableSymbol -eq $ExecutionTradableSymbol })
    if ($matches.Count -ne 1) { Fail-Gate $FailureClassification "Missing $ExecutionTradableSymbol." }
    $item = $matches[0]
    if ($item.NormalizedPortfolioSymbol -ne $NormalizedPortfolioSymbol) { Fail-Gate $FailureClassification "Normalized symbol mismatch for $ExecutionTradableSymbol." }
    if ([bool]$item.RequiresInversion -ne $RequiresInversion) { Fail-Gate $FailureClassification "RequiresInversion mismatch for $ExecutionTradableSymbol." }
    return $item
}

$requiredArtifacts = @(
    "phase-exec-sim-r025-summary.md",
    "phase-exec-sim-r025-expanded-backtest-execution-contract.json",
    "phase-exec-sim-r025-expanded-backtest-run-result.json",
    "phase-exec-sim-r025-r024-authorization-reference.json",
    "phase-exec-sim-r025-r023-row-validation-reference.json",
    "phase-exec-sim-r025-accepted-rows-used-rejected-rows-excluded.json",
    "phase-exec-sim-r025-quote-windows.json",
    "phase-exec-sim-r025-close-benchmarks.json",
    "phase-exec-sim-r025-feed-quality-results.json",
    "phase-exec-sim-r025-tca-result-line-contract.json",
    "phase-exec-sim-r025-tca-result-lines.json",
    "phase-exec-sim-r025-per-symbol-eurusd-report.json",
    "phase-exec-sim-r025-per-symbol-usdjpy-report.json",
    "phase-exec-sim-r025-per-symbol-audusd-report.json",
    "phase-exec-sim-r025-per-symbol-gbpusd-report.json",
    "phase-exec-sim-r025-per-symbol-nzdusd-report.json",
    "phase-exec-sim-r025-per-symbol-usdcad-report.json",
    "phase-exec-sim-r025-per-symbol-usdchf-report.json",
    "phase-exec-sim-r025-expanded-policy-comparison-report.json",
    "phase-exec-sim-r025-ranking-median-slippage.json",
    "phase-exec-sim-r025-ranking-p95-slippage.json",
    "phase-exec-sim-r025-ranking-fill-ratio.json",
    "phase-exec-sim-r025-ranking-residual.json",
    "phase-exec-sim-r025-ranking-spread-paid.json",
    "phase-exec-sim-r025-wakett-limit-baseline-report.json",
    "phase-exec-sim-r025-wakett-five-market-slices-report.json",
    "phase-exec-sim-r025-passive-until-urgency-report.json",
    "phase-exec-sim-r025-close-seeking-15m-report.json",
    "phase-exec-sim-r025-close-seeking-adaptive-report.json",
    "phase-exec-sim-r025-controlled-residual-cross-report.json",
    "phase-exec-sim-r025-benchmark-only-policy-report.json",
    "phase-exec-sim-r025-expanded-major-symbol-comparison.json",
    "phase-exec-sim-r025-inversion-preservation.json",
    "phase-exec-sim-r025-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r025-cost-guidance-preservation.json",
    "phase-exec-sim-r025-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r025-no-db-import-audit.json",
    "phase-exec-sim-r025-no-persisted-sanitized-row-audit.json",
    "phase-exec-sim-r025-no-executable-schedule-audit.json",
    "phase-exec-sim-r025-no-child-slices-audit.json",
    "phase-exec-sim-r025-no-child-orders-audit.json",
    "phase-exec-sim-r025-no-real-fill-audit.json",
    "phase-exec-sim-r025-no-execution-report-audit.json",
    "phase-exec-sim-r025-no-order-created-audit.json",
    "phase-exec-sim-r025-no-route-no-submission-audit.json",
    "phase-exec-sim-r025-no-polygon-api-call-audit.json",
    "phase-exec-sim-r025-no-lmax-call-audit.json",
    "phase-exec-sim-r025-no-external-api-call-audit.json",
    "phase-exec-sim-r025-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r025-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r025-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r025-no-external-audit.json",
    "phase-exec-sim-r025-forbidden-actions-audit.json",
    "phase-exec-sim-r025-next-phase-recommendation.json",
    "phase-exec-sim-r025-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsDir $artifact))) {
        Fail-Gate "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "Required R025 artifact missing: $artifact"
    }
}

$contract = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-expanded-backtest-execution-contract.json") "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING"
$run = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-expanded-backtest-run-result.json") "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING"
$r024 = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-r024-authorization-reference.json") "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING"
$r023 = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-r023-row-validation-reference.json") "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING"
$rows = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-accepted-rows-used-rejected-rows-excluded.json") "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING"
$windows = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-quote-windows.json") "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING"
$close = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-close-benchmarks.json") "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING"
$feed = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-feed-quality-results.json") "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING"
$lineContract = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-tca-result-line-contract.json") "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING"
$lines = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-tca-result-lines.json") "EXEC_SIM_R025_FAIL_TCA_REPORTS_MISSING"
$policy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-expanded-policy-comparison-report.json") "EXEC_SIM_R025_FAIL_TCA_REPORTS_MISSING"
$wakettLimit = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-wakett-limit-baseline-report.json") "EXEC_SIM_R025_FAIL_WAKETT_BASELINES_MISSING"
$wakettSlices = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-wakett-five-market-slices-report.json") "EXEC_SIM_R025_FAIL_WAKETT_BASELINES_MISSING"
$closeSeeking = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-close-seeking-15m-report.json") "EXEC_SIM_R025_FAIL_CLOSE_SEEKING_RESULTS_MISSING"
$adaptive = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-close-seeking-adaptive-report.json") "EXEC_SIM_R025_FAIL_CLOSE_SEEKING_RESULTS_MISSING"
$controlled = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-controlled-residual-cross-report.json") "EXEC_SIM_R025_FAIL_CLOSE_SEEKING_RESULTS_MISSING"
$benchmark = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-benchmark-only-policy-report.json") "EXEC_SIM_R025_FAIL_TCA_REPORTS_MISSING"
$expandedMajor = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-expanded-major-symbol-comparison.json") "EXEC_SIM_R025_FAIL_TCA_REPORTS_MISSING"
$inversion = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-inversion-preservation.json") "EXEC_SIM_R025_FAIL_USDJPY_CAVEAT_WEAKENED"
$direct = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-direct-cross-exclusion-preservation.json") "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING"
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-cost-guidance-preservation.json") "EXEC_SIM_R025_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$nonmajor = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-nonmajor-calibration-preservation.json") "EXEC_SIM_R025_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$noDb = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-no-db-import-audit.json") "EXEC_SIM_R025_FAIL_DB_IMPORT_OCCURRED"
$noRows = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-no-persisted-sanitized-row-audit.json") "EXEC_SIM_R025_FAIL_SANITIZED_QUOTE_ROWS_CREATED"
$noSchedule = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-no-executable-schedule-audit.json") "EXEC_SIM_R025_FAIL_EXECUTABLE_SCHEDULE_CREATED"
$noSlices = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-no-child-slices-audit.json") "EXEC_SIM_R025_FAIL_CHILD_SLICES_OR_ORDERS_CREATED"
$noChildOrders = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-no-child-orders-audit.json") "EXEC_SIM_R025_FAIL_CHILD_SLICES_OR_ORDERS_CREATED"
$noFill = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-no-real-fill-audit.json") "EXEC_SIM_R025_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$noReport = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-no-execution-report-audit.json") "EXEC_SIM_R025_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$noOrder = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-no-order-created-audit.json") "EXEC_SIM_R025_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$noRoute = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-no-route-no-submission-audit.json") "EXEC_SIM_R025_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$api = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-no-external-api-call-audit.json") "EXEC_SIM_R025_FAIL_API_CALL_DETECTED"
$runtime = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-no-broker-marketdata-runtime-audit.json") "EXEC_SIM_R025_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-usdjpy-caveat-preservation.json") "EXEC_SIM_R025_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-lmax-readonly-baseline-reference.json") "EXEC_SIM_R025_FAIL_API_CALL_DETECTED"
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-no-external-audit.json") "EXEC_SIM_R025_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-forbidden-actions-audit.json") "EXEC_SIM_R025_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r025-build-test-validator-evidence.json") "EXEC_SIM_R025_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$contract.expandedBacktestExecutionContractCreated) "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "Execution contract missing."
if ($contract.SourceAuthorizationPhase -ne "EXEC-SIM-R024" -or $contract.SourceRowValidationPhase -ne "EXEC-SIM-R023") { Fail-Gate "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "Source phase reference mismatch." }
if ($contract.ProviderName -ne "PolygonOfflineFile" -or $contract.DatasetType -ne "HistoricalBboQuotes") { Fail-Gate "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "Provider/dataset mismatch." }
if ($contract.SessionWindowCategory -ne "IntradayRebalance") { Fail-Gate "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "Session window category mismatch." }
Require-True ([bool]$contract.AcceptedRowSetOnly) "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "Accepted row set only flag missing."
Require-True ([bool]$contract.RejectedRowsExcluded) "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "Rejected rows excluded flag missing."
Require-True ([bool]$contract.NoApiCall) "EXEC_SIM_R025_FAIL_API_CALL_DETECTED" "Contract allows API call."
Require-True ([bool]$contract.NoDbImport) "EXEC_SIM_R025_FAIL_DB_IMPORT_OCCURRED" "Contract allows DB import."
Require-True ([bool]$contract.NoPersistedSanitizedRows) "EXEC_SIM_R025_FAIL_SANITIZED_QUOTE_ROWS_CREATED" "Contract allows persisted sanitized rows."
Require-True ([bool]$contract.NoOrderDomainOutput) "EXEC_SIM_R025_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Contract allows order-domain output."

Require-True ([bool]$run.expandedBacktestRunResultCreated) "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "Run result missing."
if ($run.SymbolCount -ne 7 -or $run.PolicyCount -ne 11 -or $run.QuoteWindowCount -ne 112 -or $run.CloseBenchmarkCount -ne 112 -or $run.FeedQualityResultCount -ne 7 -or $run.TcaResultLineCount -ne 1232) {
    Fail-Gate "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "Run result counts are not complete."
}
Require-True ([bool]$run.NoApiCall) "EXEC_SIM_R025_FAIL_API_CALL_DETECTED" "Run used API call."
Require-True ([bool]$run.NoBrokerRuntime) "EXEC_SIM_R025_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "Run used broker runtime."
Require-True ([bool]$run.NoDbImport) "EXEC_SIM_R025_FAIL_DB_IMPORT_OCCURRED" "Run imported DB rows."
Require-True ([bool]$run.NoPersistedSanitizedRows) "EXEC_SIM_R025_FAIL_SANITIZED_QUOTE_ROWS_CREATED" "Run persisted sanitized rows."
Require-True ([bool]$run.NoOrderDomainOutput) "EXEC_SIM_R025_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Run created order-domain output."

Require-True ([bool]$r024.r024AuthorizationReferenceCreated) "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "R024 reference missing."
Require-Contains @($r024.R024Classifications) "EXEC_SIM_R024_PASS_EXPANDED_BACKTEST_AUTHORIZATION_READY_NO_EXTERNAL" "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "R024 pass classification missing."
Require-False ([bool]$r024.R024BacktestExecuted) "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "R024 execution flag changed."
Require-True ([bool]$r023.r023RowValidationReferenceCreated) "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "R023 reference missing."
if ($r023.RowValidationResultCount -ne 7 -or $r023.TotalRejectedRowCount -ne 7) { Fail-Gate "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "R023 row validation reference counts mismatch." }
Require-True ([bool]$r023.RejectedMalformedRowsExcludedFromBacktest) "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "Rejected malformed rows not excluded."

Require-True ([bool]$rows.acceptedRowsUsedRejectedRowsExcludedCreated) "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "Accepted/rejected row usage missing."
Require-True ([bool]$rows.AcceptedRowSetOnly) "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "Accepted row set only missing."
Require-True ([bool]$rows.RejectedRowsExcluded) "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "Rejected rows excluded missing."
Require-False ([bool]$rows.PersistedSanitizedQuoteRowsCreated) "EXEC_SIM_R025_FAIL_SANITIZED_QUOTE_ROWS_CREATED" "Persisted sanitized rows created."
Require-False ([bool]$rows.DbImportOccurred) "EXEC_SIM_R025_FAIL_DB_IMPORT_OCCURRED" "DB import occurred."
if ($rows.totalRejectedRowCount -ne 7 -or @($rows.perSymbol).Count -ne 7) { Fail-Gate "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "Accepted/rejected row usage counts mismatch." }

Require-True ([bool]$windows.quoteWindowsCreated) "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "Quote windows missing."
Require-True ([bool]$windows.BuiltFromAcceptedRows) "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "Quote windows not built from accepted rows."
if ($windows.resultCount -ne 112) { Fail-Gate "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "Quote-window count mismatch." }
Require-True ([bool]$close.closeBenchmarksCreated) "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "Close benchmarks missing."
Require-True ([bool]$close.BuiltFromAcceptedRows) "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "Close benchmarks not built from accepted rows."
if ($close.resultCount -ne 112) { Fail-Gate "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "Close-benchmark count mismatch." }
Require-True ([bool]$feed.feedQualityResultsCreated) "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "Feed quality missing."
Require-True ([bool]$feed.ComputedFromAcceptedRows) "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "Feed quality not computed from accepted rows."
if ($feed.resultCount -ne 7) { Fail-Gate "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "Feed-quality count mismatch." }

Require-True ([bool]$lineContract.tcaResultLineContractCreated) "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "TCA result line contract missing."
Require-True ([bool]$lineContract.FixtureOnly) "EXEC_SIM_R025_FAIL_TCA_REPORTS_MISSING" "Line contract not fixture-only."
Require-True ([bool]$lineContract.PaperOnly) "EXEC_SIM_R025_FAIL_TCA_REPORTS_MISSING" "Line contract not paper-only."
Require-True ([bool]$lineContract.NonExecutable) "EXEC_SIM_R025_FAIL_EXECUTABLE_SCHEDULE_CREATED" "Line contract executable."
Require-False ([bool]$lineContract.IsFill) "EXEC_SIM_R025_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Line contract represented as fill."
Require-False ([bool]$lineContract.IsExecutionReport) "EXEC_SIM_R025_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Line contract represented as execution report."
Require-False ([bool]$lineContract.IsOrder) "EXEC_SIM_R025_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Line contract represented as order."
Require-False ([bool]$lineContract.IsChildSlice) "EXEC_SIM_R025_FAIL_CHILD_SLICES_OR_ORDERS_CREATED" "Line contract represented as child slice."
Require-False ([bool]$lineContract.HasBrokerRoute) "EXEC_SIM_R025_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Line contract has broker route."
Require-True ([bool]$lines.tcaResultLinesCreated) "EXEC_SIM_R025_FAIL_TCA_REPORTS_MISSING" "TCA result lines missing."
if ($lines.ResultLineCount -ne 1232 -or @($lines.lines).Count -ne 1232) { Fail-Gate "EXEC_SIM_R025_FAIL_TCA_REPORTS_MISSING" "TCA result line count mismatch." }

foreach ($line in @($lines.lines)) {
    Require-True ([bool]$line.FixtureOnly) "EXEC_SIM_R025_FAIL_TCA_REPORTS_MISSING" "A line is not fixture-only."
    Require-True ([bool]$line.PaperOnly) "EXEC_SIM_R025_FAIL_TCA_REPORTS_MISSING" "A line is not paper-only."
    Require-True ([bool]$line.NonExecutable) "EXEC_SIM_R025_FAIL_EXECUTABLE_SCHEDULE_CREATED" "A line is executable."
    Require-False ([bool]$line.IsFill) "EXEC_SIM_R025_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "A TCA line is represented as a fill."
    Require-False ([bool]$line.IsExecutionReport) "EXEC_SIM_R025_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "A TCA line is represented as an execution report."
    Require-False ([bool]$line.IsOrder) "EXEC_SIM_R025_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "A TCA line is represented as an order."
    Require-False ([bool]$line.IsChildSlice) "EXEC_SIM_R025_FAIL_CHILD_SLICES_OR_ORDERS_CREATED" "A TCA line is represented as a child slice."
    Require-False ([bool]$line.IsSubmitted) "EXEC_SIM_R025_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "A TCA line is submitted."
    Require-False ([bool]$line.HasBrokerRoute) "EXEC_SIM_R025_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "A TCA line has broker route."
}

$expectedSymbols = @(
    @{ Execution = "EURUSD"; Normalized = "EURUSD"; Inversion = $false },
    @{ Execution = "USDJPY"; Normalized = "JPYUSD"; Inversion = $true },
    @{ Execution = "AUDUSD"; Normalized = "AUDUSD"; Inversion = $false },
    @{ Execution = "GBPUSD"; Normalized = "GBPUSD"; Inversion = $false },
    @{ Execution = "NZDUSD"; Normalized = "NZDUSD"; Inversion = $false },
    @{ Execution = "USDCAD"; Normalized = "CADUSD"; Inversion = $true },
    @{ Execution = "USDCHF"; Normalized = "CHFUSD"; Inversion = $true }
)

foreach ($symbol in $expectedSymbols) {
    $matchingLines = @($lines.lines | Where-Object { $_.ExecutionTradableSymbol -eq $symbol.Execution })
    if ($matchingLines.Count -ne 176) { Fail-Gate "EXEC_SIM_R025_FAIL_TCA_REPORTS_MISSING" "TCA result line coverage mismatch for $($symbol.Execution)." }
    $report = Read-Json (Join-Path $ArtifactsDir ("phase-exec-sim-r025-per-symbol-{0}-report.json" -f $symbol.Execution.ToLowerInvariant())) "EXEC_SIM_R025_FAIL_TCA_REPORTS_MISSING"
    Require-True ([bool]$report.reportCreated) "EXEC_SIM_R025_FAIL_TCA_REPORTS_MISSING" "Per-symbol report missing for $($symbol.Execution)."
    if ($report.ExecutionTradableSymbol -ne $symbol.Execution -or $report.NormalizedPortfolioSymbol -ne $symbol.Normalized -or [bool]$report.RequiresInversion -ne $symbol.Inversion) {
        Fail-Gate "EXEC_SIM_R025_FAIL_USDJPY_CAVEAT_WEAKENED" "Symbol mapping mismatch for $($symbol.Execution)."
    }
    if ($report.ResultLineCount -ne 176) { Fail-Gate "EXEC_SIM_R025_FAIL_TCA_REPORTS_MISSING" "Per-symbol line count mismatch for $($symbol.Execution)." }
    Require-True ([bool]$report.NoOrderDomainOutput) "EXEC_SIM_R025_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Per-symbol report has order-domain risk."
}

Require-True ([bool]$policy.expandedPolicyComparisonReportCreated) "EXEC_SIM_R025_FAIL_TCA_REPORTS_MISSING" "Policy comparison missing."
if ($policy.PolicyCount -ne 11 -or @($policy.comparisons).Count -ne 11) { Fail-Gate "EXEC_SIM_R025_FAIL_TCA_REPORTS_MISSING" "Policy comparison count mismatch." }
foreach ($policyName in @("WakettPureLimitUntilClose", "WakettFiveMarketSlicesAroundClose", "PassiveUntilUrgency", "CloseSeeking15m", "CloseSeeking15mAdaptive", "ControlledResidualCross", "ImmediatePaperBenchmark", "TWAPBenchmarkOnly", "VWAPBenchmarkOnly", "ManualReview", "DoNotTrade")) {
    if (@($policy.comparisons | Where-Object { $_.PolicyFamily -eq $policyName }).Count -ne 1) {
        Fail-Gate "EXEC_SIM_R025_FAIL_TCA_REPORTS_MISSING" "Policy comparison missing $policyName."
    }
}

foreach ($rankingArtifact in @("phase-exec-sim-r025-ranking-median-slippage.json", "phase-exec-sim-r025-ranking-p95-slippage.json", "phase-exec-sim-r025-ranking-fill-ratio.json", "phase-exec-sim-r025-ranking-residual.json", "phase-exec-sim-r025-ranking-spread-paid.json")) {
    $ranking = Read-Json (Join-Path $ArtifactsDir $rankingArtifact) "EXEC_SIM_R025_FAIL_TCA_REPORTS_MISSING"
    Require-True ([bool]$ranking.rankingCreated) "EXEC_SIM_R025_FAIL_TCA_REPORTS_MISSING" "Ranking missing: $rankingArtifact"
    if (@($ranking.rankings).Count -ne 11) { Fail-Gate "EXEC_SIM_R025_FAIL_TCA_REPORTS_MISSING" "Ranking count mismatch: $rankingArtifact" }
}

Require-True ([bool]$wakettLimit.NegativeBaseline) "EXEC_SIM_R025_FAIL_WAKETT_BASELINES_MISSING" "Wakett limit not negative baseline."
Require-True ([bool]$wakettLimit.BlockedAsProductionDefault) "EXEC_SIM_R025_FAIL_WAKETT_BASELINES_MISSING" "Wakett limit not blocked as default."
Require-True ([bool]$wakettLimit.ShowsResidualNonFillOpportunityCostRisk) "EXEC_SIM_R025_FAIL_WAKETT_BASELINES_MISSING" "Wakett limit residual/non-fill risk missing."
Require-True ([bool]$wakettSlices.NegativeBaseline) "EXEC_SIM_R025_FAIL_WAKETT_BASELINES_MISSING" "Wakett slices not negative baseline."
Require-True ([bool]$wakettSlices.BlockedAsProductionDefault) "EXEC_SIM_R025_FAIL_WAKETT_BASELINES_MISSING" "Wakett slices not blocked as default."
Require-True ([bool]$wakettSlices.ShowsRepeatedSpreadCrossingRisk) "EXEC_SIM_R025_FAIL_WAKETT_BASELINES_MISSING" "Wakett slices spread-crossing risk missing."
Require-True ([bool]$closeSeeking.reportCreated) "EXEC_SIM_R025_FAIL_CLOSE_SEEKING_RESULTS_MISSING" "CloseSeeking15m report missing."
Require-True ([bool]$adaptive.RemainsCandidateWhereFeedAndSpreadAreGood) "EXEC_SIM_R025_FAIL_CLOSE_SEEKING_RESULTS_MISSING" "Adaptive candidate marker missing."
Require-True ([bool]$controlled.ConditionalOnOpportunityCostExceedingCrossingCost) "EXEC_SIM_R025_FAIL_CLOSE_SEEKING_RESULTS_MISSING" "Controlled residual marker missing."
Require-True ([bool]$benchmark.benchmarkOnlyPolicyReportCreated) "EXEC_SIM_R025_FAIL_TCA_REPORTS_MISSING" "Benchmark-only report missing."
foreach ($bench in @($benchmark.policies)) {
    Require-True ([bool]$bench.BenchmarkOnly) "EXEC_SIM_R025_FAIL_TCA_REPORTS_MISSING" "Benchmark-only policy not marked benchmark-only."
    Require-True ([bool]$bench.NonExecutable) "EXEC_SIM_R025_FAIL_EXECUTABLE_SCHEDULE_CREATED" "Benchmark-only policy executable."
}
Require-True ([bool]$expandedMajor.expandedMajorSymbolComparisonCreated) "EXEC_SIM_R025_FAIL_TCA_REPORTS_MISSING" "Expanded-major comparison missing."
if (@($expandedMajor.comparisons).Count -ne 7) { Fail-Gate "EXEC_SIM_R025_FAIL_TCA_REPORTS_MISSING" "Expanded-major comparison count mismatch." }

Require-True ([bool]$inversion.usdJpyCaveatPreserved) "EXEC_SIM_R025_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
Require-False ([bool]$inversion.audusdMisclassifiedFailed) "EXEC_SIM_R025_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD misclassified failed."
$unused = Require-Symbol @($inversion.validations) "USDJPY" "JPYUSD" $true "EXEC_SIM_R025_FAIL_USDJPY_CAVEAT_WEAKENED"
$unused = Require-Symbol @($inversion.validations) "USDCAD" "CADUSD" $true "EXEC_SIM_R025_FAIL_USDJPY_CAVEAT_WEAKENED"
$unused = Require-Symbol @($inversion.validations) "USDCHF" "CHFUSD" $true "EXEC_SIM_R025_FAIL_USDJPY_CAVEAT_WEAKENED"
Require-True ([bool]$direct.directCrossExclusionPreserved) "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "Direct-cross exclusion missing."
Require-False ([bool]$direct.directCrossIncluded) "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "Direct-cross included."
Require-False ([bool]$direct.directCrossExecutionAllowedByDefault) "EXEC_SIM_R025_FAIL_BACKTEST_RESULT_MISSING" "Direct-cross execution allowed."
if ($cost.bestCaseMajorTargetUsdPerMillion -ne 5) { Fail-Gate "EXEC_SIM_R025_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million marker missing." }
Require-True ([bool]$cost.fiveUsdPerMillionBestCaseMajorOnly) "EXEC_SIM_R025_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million not best-case major-only."
Require-False ([bool]$cost.fiveUsdPerMillionUniversalized) "EXEC_SIM_R025_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million universalized."
Require-True ([bool]$nonmajor.RequiresLiquidityCalibration) "EXEC_SIM_R025_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "Nonmajor calibration missing."

Require-False ([bool]$noDb.quotesImportedIntoDb) "EXEC_SIM_R025_FAIL_DB_IMPORT_OCCURRED" "DB import occurred."
Require-False ([bool]$noDb.dbWriteOccurred) "EXEC_SIM_R025_FAIL_DB_IMPORT_OCCURRED" "DB write occurred."
Require-False ([bool]$noRows.persistedSanitizedQuoteRowsCreated) "EXEC_SIM_R025_FAIL_SANITIZED_QUOTE_ROWS_CREATED" "Persisted sanitized rows created."
Require-False ([bool]$noSchedule.executableSchedulesCreated) "EXEC_SIM_R025_FAIL_EXECUTABLE_SCHEDULE_CREATED" "Executable schedules created."
Require-False ([bool]$noSlices.childSlicesCreated) "EXEC_SIM_R025_FAIL_CHILD_SLICES_OR_ORDERS_CREATED" "Child slices created."
Require-False ([bool]$noChildOrders.childOrdersCreated) "EXEC_SIM_R025_FAIL_CHILD_SLICES_OR_ORDERS_CREATED" "Child orders created."
Require-False ([bool]$noFill.realFillsCreated) "EXEC_SIM_R025_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Real fills created."
Require-False ([bool]$noFill.fillEntitiesCreated) "EXEC_SIM_R025_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill entities created."
Require-False ([bool]$noReport.executionReportEntitiesCreated) "EXEC_SIM_R025_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Execution reports created."
Require-False ([bool]$noOrder.ordersCreated) "EXEC_SIM_R025_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders created."
Require-False ([bool]$noOrder.executableOrdersCreated) "EXEC_SIM_R025_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Executable orders created."
Require-False ([bool]$noRoute.routesCreated) "EXEC_SIM_R025_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Routes created."
Require-False ([bool]$noRoute.submissionsCreated) "EXEC_SIM_R025_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Submissions created."
Require-False ([bool]$api.polygonApiCalled) "EXEC_SIM_R025_FAIL_API_CALL_DETECTED" "Polygon API called."
Require-False ([bool]$api.lmaxCalled) "EXEC_SIM_R025_FAIL_API_CALL_DETECTED" "LMAX called."
Require-False ([bool]$api.externalApiCalled) "EXEC_SIM_R025_FAIL_API_CALL_DETECTED" "External API called."
Require-False ([bool]$runtime.brokerActivationDetected) "EXEC_SIM_R025_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "Broker activation detected."
Require-False ([bool]$runtime.marketDataRequestSent) "EXEC_SIM_R025_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "MarketDataRequest sent."
Require-False ([bool]$runtime.marketDataResponseRead) "EXEC_SIM_R025_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "MarketDataResponse read."
Require-False ([bool]$runtime.schedulerServiceTimerPollingBackgroundJobIntroduced) "EXEC_SIM_R025_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler/service/timer/polling/background job introduced."
Require-True ([bool]$usdjpy.usdjpyCaveatPreserved) "EXEC_SIM_R025_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat artifact missing."
if ($usdjpy.PortfolioNormalizedSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or -not [bool]$usdjpy.RequiresInversion) { Fail-Gate "EXEC_SIM_R025_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat weakened." }
Require-False ([bool]$usdjpy.audusdMisclassifiedFailed) "EXEC_SIM_R025_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD caveat artifact misclassified."
Require-True ([bool]$lmax.referenceOnly) "EXEC_SIM_R025_FAIL_API_CALL_DETECTED" "LMAX not reference-only."
Require-False ([bool]$lmax.lmaxCalledInR025) "EXEC_SIM_R025_FAIL_API_CALL_DETECTED" "LMAX called."
Require-False ([bool]$noExternal.polygonApiCalled) "EXEC_SIM_R025_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Polygon API called."
Require-False ([bool]$noExternal.lmaxCalled) "EXEC_SIM_R025_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX called."
Require-False ([bool]$noExternal.externalApiCalled) "EXEC_SIM_R025_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "External API called."
Require-False ([bool]$noExternal.brokerRuntimeActionDetected) "EXEC_SIM_R025_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "Broker runtime detected."
Require-False ([bool]$noExternal.quotesImportedIntoDb) "EXEC_SIM_R025_FAIL_DB_IMPORT_OCCURRED" "No-external audit DB import."
Require-False ([bool]$noExternal.persistedSanitizedQuoteRowsCreated) "EXEC_SIM_R025_FAIL_SANITIZED_QUOTE_ROWS_CREATED" "No-external audit persisted sanitized rows."
Require-False ([bool]$noExternal.ordersFillsReportsRoutesSubmissionsCreated) "EXEC_SIM_R025_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No-external audit order-domain output."
Require-False ([bool]$forbidden.forbiddenActionsDetected) "EXEC_SIM_R025_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden actions detected."

if ($evidence.dotnetBuildNoRestore -ne "PASS") { Fail-Gate "EXEC_SIM_R025_FAIL_BUILD_OR_TESTS" "dotnet build evidence missing or not PASS." }
if ($evidence.focusedTests -notlike "PASS*") { Fail-Gate "EXEC_SIM_R025_FAIL_BUILD_OR_TESTS" "Focused R025 test evidence missing or not PASS." }
if ($evidence.unitTests -notlike "PASS*") { Fail-Gate "EXEC_SIM_R025_FAIL_BUILD_OR_TESTS" "Unit test evidence missing or not PASS." }
if ($evidence.validator -notlike "PASS*") { Fail-Gate "EXEC_SIM_R025_FAIL_BUILD_OR_TESTS" "Validator evidence missing or not PASS." }

Write-Host "EXEC_SIM_R025_PASS_EXPANDED_OFFLINE_TCA_BACKTEST_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R025_PASS_SEVEN_SYMBOL_POLICY_COMPARISON_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R025_PASS_EXPANDED_MAJOR_USD_PAIR_TCA_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R025_PASS_NO_DB_IMPORT_NO_REAL_FILL_NO_ORDER_GATE_READY_NO_EXTERNAL"
