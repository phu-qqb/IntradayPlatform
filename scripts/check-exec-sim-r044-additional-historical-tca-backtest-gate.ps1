param(
    [string]$ArtifactsRoot = "artifacts/readiness/execution-sim"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Error "EXEC-SIM-R044 validation failed: $Message"
    exit 1
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing artifact: $Path"
    }
    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Assert-FalseField($Object, [string]$Field, [string]$Message) {
    if ($null -eq $Object.$Field) {
        Fail "Missing field $Field in $Message"
    }
    if ([bool]$Object.$Field) {
        Fail $Message
    }
}

function Assert-TrueField($Object, [string]$Field, [string]$Message) {
    if ($null -eq $Object.$Field) {
        Fail "Missing field $Field in $Message"
    }
    if (-not [bool]$Object.$Field) {
        Fail $Message
    }
}

$requiredFiles = @(
    "phase-exec-sim-r044-summary.md",
    "phase-exec-sim-r044-additional-historical-backtest-execution-contract.json",
    "phase-exec-sim-r044-additional-historical-backtest-run-result.json",
    "phase-exec-sim-r044-r043-authorization-reference.json",
    "phase-exec-sim-r044-r042-row-validation-reference.json",
    "phase-exec-sim-r044-accepted-rows-used-rejected-rows-excluded.json",
    "phase-exec-sim-r044-duplicate-handling-preservation.json",
    "phase-exec-sim-r044-quote-windows.json",
    "phase-exec-sim-r044-close-benchmarks.json",
    "phase-exec-sim-r044-feed-quality-results.json",
    "phase-exec-sim-r044-tca-result-line-contract.json",
    "phase-exec-sim-r044-tca-result-lines.json",
    "phase-exec-sim-r044-result-line-count-and-coverage.json",
    "phase-exec-sim-r044-per-date-tca-reports.json",
    "phase-exec-sim-r044-per-symbol-tca-reports.json",
    "phase-exec-sim-r044-per-symbol-date-tca-reports.json",
    "phase-exec-sim-r044-canonical-session-aggregate-report.json",
    "phase-exec-sim-r044-policy-comparison-report.json",
    "phase-exec-sim-r044-ranking-median-slippage.json",
    "phase-exec-sim-r044-ranking-p95-slippage.json",
    "phase-exec-sim-r044-ranking-fill-ratio.json",
    "phase-exec-sim-r044-ranking-residual.json",
    "phase-exec-sim-r044-ranking-spread-paid.json",
    "phase-exec-sim-r044-no-overnight-residual-penalty-report.json",
    "phase-exec-sim-r044-wakett-limit-baseline-report.json",
    "phase-exec-sim-r044-wakett-five-market-slices-report.json",
    "phase-exec-sim-r044-passive-until-urgency-report.json",
    "phase-exec-sim-r044-close-seeking-15m-report.json",
    "phase-exec-sim-r044-close-seeking-adaptive-report.json",
    "phase-exec-sim-r044-controlled-residual-cross-report.json",
    "phase-exec-sim-r044-benchmark-only-policy-report.json",
    "phase-exec-sim-r044-comparison-vs-r025-r031.json",
    "phase-exec-sim-r044-inversion-preservation.json",
    "phase-exec-sim-r044-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-sim-r044-legacy-compatibility-preservation.json",
    "phase-exec-sim-r044-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r044-cost-guidance-preservation.json",
    "phase-exec-sim-r044-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r044-no-db-import-audit.json",
    "phase-exec-sim-r044-no-persisted-sanitized-row-audit.json",
    "phase-exec-sim-r044-no-executable-schedule-audit.json",
    "phase-exec-sim-r044-no-child-slices-audit.json",
    "phase-exec-sim-r044-no-child-orders-audit.json",
    "phase-exec-sim-r044-no-real-fill-audit.json",
    "phase-exec-sim-r044-no-execution-report-audit.json",
    "phase-exec-sim-r044-no-order-created-audit.json",
    "phase-exec-sim-r044-no-route-no-submission-audit.json",
    "phase-exec-sim-r044-no-polygon-api-call-audit.json",
    "phase-exec-sim-r044-no-lmax-call-audit.json",
    "phase-exec-sim-r044-no-external-api-call-audit.json",
    "phase-exec-sim-r044-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r044-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r044-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r044-no-external-audit.json",
    "phase-exec-sim-r044-forbidden-actions-audit.json",
    "phase-exec-sim-r044-next-phase-recommendation.json",
    "phase-exec-sim-r044-build-test-validator-evidence.json"
)

foreach ($file in $requiredFiles) {
    $path = Join-Path $ArtifactsRoot $file
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing required artifact $file"
    }
}

$contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-additional-historical-backtest-execution-contract.json")
if ($contract.AdditionalHistoricalBacktestRunId -ne "EXEC-SIM-R044-ADDITIONAL-HISTORICAL-TCA-BACKTEST-RUN") {
    Fail "Backtest execution contract id mismatch"
}
if ($contract.ProviderName -ne "PolygonOfflineFile" -or $contract.DatasetType -ne "HistoricalBboQuotes") {
    Fail "Backtest execution contract provider/dataset mismatch"
}
Assert-TrueField $contract "AcceptedRowSetOnly" "Contract does not require accepted row set only"
Assert-TrueField $contract "RejectedRowsExcluded" "Contract does not exclude rejected rows"
Assert-TrueField $contract "NoApiCall" "Contract does not prohibit API calls"
Assert-TrueField $contract "NoDbImport" "Contract does not prohibit DB import"
Assert-TrueField $contract "NoPersistedSanitizedRows" "Contract does not prohibit persisted sanitized rows"
Assert-TrueField $contract "NoOrderDomainOutput" "Contract does not prohibit order-domain output"

$run = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-additional-historical-backtest-run-result.json")
if ($run.TcaResultLineCount -ne 10395 -or $run.ExpectedTcaResultLineCount -ne 10395) {
    Fail "TCA result line count mismatch"
}
if ($run.QuoteWindowCount -ne 945 -or $run.CloseBenchmarkCount -ne 945 -or $run.FeedQualityRecordCount -ne 35 -or $run.PolicyCount -ne 11) {
    Fail "Run result coverage counts are incorrect"
}
if (-not ($run.Classifications -contains "EXEC_SIM_R044_PASS_ADDITIONAL_HISTORICAL_TCA_BACKTEST_READY_NO_EXTERNAL")) {
    Fail "Success classification missing from run result"
}
Assert-TrueField $run "NoApiCall" "Run result does not prohibit API calls"
Assert-TrueField $run "NoDbImport" "Run result does not prohibit DB import"
Assert-TrueField $run "NoPersistedSanitizedRows" "Run result does not prohibit persisted sanitized rows"
Assert-TrueField $run "NoOrderDomainOutput" "Run result does not prohibit order-domain output"

$auth = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-r043-authorization-reference.json")
if ($auth.AuthorizationReady -ne $true -or $auth.AuthorizedFileEntries -ne 35 -or $auth.AuthorizedQuoteWindows -ne 945 -or $auth.AuthorizedCloseBenchmarks -ne 945 -or $auth.AuthorizedFeedQualityRecords -ne 35) {
    Fail "R043 authorization reference is incomplete"
}

$rows = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-r042-row-validation-reference.json")
if ($rows.FileEntryCount -ne 35 -or $rows.TotalRejectedRows -ne 0 -or $rows.RowsRevalidatedInR044 -ne $false) {
    Fail "R042 row validation reference is invalid"
}

$rowUsage = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-accepted-rows-used-rejected-rows-excluded.json")
Assert-TrueField $rowUsage "AcceptedRowSetOnly" "Accepted rows only was not preserved"
Assert-TrueField $rowUsage "RejectedRowsExcluded" "Rejected rows were not excluded"
Assert-FalseField $rowUsage "PersistedSanitizedRowsCreated" "Persisted sanitized quote rows were created"
Assert-FalseField $rowUsage "DbImportExecuted" "DB import executed"

$duplicates = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-duplicate-handling-preservation.json")
if ($duplicates.Preserved -ne $true -or $duplicates.DuplicateHandling -ne "DeterministicAcknowledged" -or $duplicates.OutOfOrderRowCount -ne 0) {
    Fail "Duplicate handling preservation is missing or invalid"
}

$quoteWindows = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-quote-windows.json")
if ($quoteWindows.QuoteWindowCount -ne 945 -or $quoteWindows.Windows.Count -ne 945) {
    Fail "Quote windows are missing"
}
$closeBenchmarks = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-close-benchmarks.json")
if ($closeBenchmarks.CloseBenchmarkCount -ne 945 -or $closeBenchmarks.Benchmarks.Count -ne 945) {
    Fail "Close benchmarks are missing"
}
$feed = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-feed-quality-results.json")
if ($feed.FeedQualityRecordCount -ne 35 -or $feed.Results.Count -ne 35) {
    Fail "Feed quality results are missing"
}

$lineContract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-tca-result-line-contract.json")
Assert-TrueField $lineContract "FixtureOnlyRequired" "TCA result line contract does not require fixture-only lines"
Assert-TrueField $lineContract "PaperOnlyRequired" "TCA result line contract does not require paper-only lines"
Assert-TrueField $lineContract "NonExecutableRequired" "TCA result line contract does not require non-executable lines"

$lines = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-tca-result-lines.json")
if ($lines.ResultLineCount -ne 10395 -or $lines.ExpectedResultLineCount -ne 10395 -or $lines.Lines.Count -ne 10395) {
    Fail "TCA result lines are missing or count mismatch"
}
if (($lines.Lines | Where-Object { $_.FixtureOnly -ne $true -or $_.PaperOnly -ne $true -or $_.NonExecutable -ne $true }).Count -ne 0) {
    Fail "At least one TCA result line is not fixture-only/paper-only/non-executable"
}
if (($lines.Lines | Where-Object { $_.IsFill -ne $false -or $_.IsExecutionReport -ne $false -or $_.IsOrder -ne $false -or $_.IsChildSlice -ne $false -or $_.IsSubmitted -ne $false -or $_.HasBrokerRoute -ne $false }).Count -ne 0) {
    Fail "At least one TCA result line is represented as fill/order/execution report/route/submission"
}
foreach ($policy in @("WakettPureLimitUntilClose","WakettFiveMarketSlicesAroundClose","PassiveUntilUrgency","CloseSeeking15m","CloseSeeking15mAdaptive","ControlledResidualCross","ImmediatePaperBenchmark","TWAPBenchmarkOnly","VWAPBenchmarkOnly","ManualReview","DoNotTrade")) {
    if (($lines.Lines | Where-Object { $_.PolicyFamily -eq $policy }).Count -ne 945) {
        Fail "Policy $policy does not have 945 TCA result lines"
    }
}
if (($lines.Lines | Where-Object { $_.PolicyFamily -eq "TWAPBenchmarkOnly" -and ($_.BenchmarkOnly -ne $true -or $_.NonExecutable -ne $true) }).Count -ne 0) {
    Fail "TWAPBenchmarkOnly is not benchmark-only/non-executable"
}
if (($lines.Lines | Where-Object { $_.PolicyFamily -eq "VWAPBenchmarkOnly" -and ($_.BenchmarkOnly -ne $true -or $_.NonExecutable -ne $true) }).Count -ne 0) {
    Fail "VWAPBenchmarkOnly is not benchmark-only/non-executable"
}

$coverage = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-result-line-count-and-coverage.json")
if ($coverage.CountMatchesExpected -ne $true -or $coverage.ActualResultLineCount -ne 10395 -or $coverage.QuoteWindowCount -ne 945 -or $coverage.PolicyCount -ne 11) {
    Fail "Result line count/coverage artifact is invalid"
}

$dateReports = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-per-date-tca-reports.json")
if ($dateReports.Reports.Count -ne 5) { Fail "Per-date TCA reports missing" }
$symbolReports = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-per-symbol-tca-reports.json")
if ($symbolReports.Reports.Count -ne 7) { Fail "Per-symbol TCA reports missing" }
$audusd = $symbolReports.Reports | Where-Object { $_.Symbol -eq "AUDUSD" }
if (-not $audusd -or $audusd.AudUsdNotFailed -ne $true) { Fail "AUDUSD is missing or misclassified as failed" }
$symbolDateReports = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-per-symbol-date-tca-reports.json")
if ($symbolDateReports.Reports.Count -ne 35) { Fail "Per-symbol/date TCA reports missing" }

$aggregate = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-canonical-session-aggregate-report.json")
if ($aggregate.Report.ResultLineCount -ne 10395) { Fail "Canonical-session aggregate report missing result lines" }
$policyComparison = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-policy-comparison-report.json")
if ($policyComparison.Reports.Count -ne 11) { Fail "Policy comparison report missing policies" }

foreach ($rankingName in @("ranking-median-slippage","ranking-p95-slippage","ranking-fill-ratio","ranking-residual","ranking-spread-paid")) {
    $ranking = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-$rankingName.json")
    if ($ranking.Ranking.Count -ne 11) {
        Fail "Ranking $rankingName is incomplete"
    }
}

$wakettLimit = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-wakett-limit-baseline-report.json")
if ($wakettLimit.PolicyFamily -ne "WakettPureLimitUntilClose") { Fail "Wakett pure limit baseline report missing" }
$wakettFive = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-wakett-five-market-slices-report.json")
if ($wakettFive.PolicyFamily -ne "WakettFiveMarketSlicesAroundClose") { Fail "Wakett five-market-slices report missing" }
$closeSeeking = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-close-seeking-15m-report.json")
if ($closeSeeking.PolicyFamily -ne "CloseSeeking15m") { Fail "CloseSeeking15m report missing" }
$closeSeekingAdaptive = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-close-seeking-adaptive-report.json")
if ($closeSeekingAdaptive.PolicyFamily -ne "CloseSeeking15mAdaptive") { Fail "CloseSeeking adaptive report missing" }
$controlled = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-controlled-residual-cross-report.json")
if ($controlled.PolicyFamily -ne "ControlledResidualCross") { Fail "ControlledResidualCross report missing" }
$benchmarkOnly = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-benchmark-only-policy-report.json")
if ($benchmarkOnly.NonExecutable -ne $true -or $benchmarkOnly.Reports.Count -ne 2) { Fail "Benchmark-only policy report missing or executable" }

$inversion = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-inversion-preservation.json")
$usdjpy = $inversion.Results | Where-Object { $_.Symbol -eq "USDJPY" }
if (-not $usdjpy -or $usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or $usdjpy.RequiresInversion -ne $true -or "$($usdjpy.SecurityID)" -ne "4004" -or "$($usdjpy.SecurityIDSource)" -ne "8") {
    Fail "USDJPY caveat was weakened"
}
$usdcad = $inversion.Results | Where-Object { $_.Symbol -eq "USDCAD" }
$usdchf = $inversion.Results | Where-Object { $_.Symbol -eq "USDCHF" }
if (-not $usdcad -or $usdcad.RequiresInversion -ne $true -or $usdcad.NormalizedPortfolioSymbol -ne "CADUSD") { Fail "USDCAD inversion weakened" }
if (-not $usdchf -or $usdchf.RequiresInversion -ne $true -or $usdchf.NormalizedPortfolioSymbol -ne "CHFUSD") { Fail "USDCHF inversion weakened" }

$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-canonical-quarter-hour-policy-preservation.json")
if ($canonical.Legacy06UsedAsFutureCanonical -ne $false -or $canonical.Preserved -ne $true) { Fail "Canonical quarter-hour policy weakened" }
$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-legacy-compatibility-preservation.json")
if ($legacy.LegacyCompatibilityOnly -ne $true -or $legacy.Legacy06UsedAsFutureCanonical -ne $false) { Fail "Legacy :06 used as future canonical" }
$direct = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-direct-cross-exclusion-preservation.json")
if ($direct.DirectCrossExecutionDisabled -ne $true -or $direct.DirectCrossesIncluded -ne $false -or $direct.ExecutionUniverse -ne "USD-pair-only") { Fail "Direct-cross exclusion weakened" }
$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-cost-guidance-preservation.json")
if ($cost.FiveUsdPerMillionGuidance -ne "BestCaseMajorOnly" -or $cost.Universalized -ne $false) { Fail "5 USD/million guidance universalized" }

$noDb = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-no-db-import-audit.json")
Assert-FalseField $noDb "DbImportExecuted" "DB import occurred"
$noSanitized = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-no-persisted-sanitized-row-audit.json")
Assert-FalseField $noSanitized "PersistedSanitizedRowsCreated" "Persisted sanitized rows were created"
$noSchedule = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-no-executable-schedule-audit.json")
Assert-FalseField $noSchedule "ExecutableSchedulesCreated" "Executable schedules created"
$noChildSlices = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-no-child-slices-audit.json")
Assert-FalseField $noChildSlices "ChildSlicesCreated" "Child slices created"
$noChildOrders = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-no-child-orders-audit.json")
Assert-FalseField $noChildOrders "ChildOrdersCreated" "Child orders created"
$noFill = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-no-real-fill-audit.json")
Assert-FalseField $noFill "RealFillsCreated" "Real fills created"
Assert-FalseField $noFill "TcaResultLinesRepresentFills" "TCA result lines represented as fills"
$noReport = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-no-execution-report-audit.json")
Assert-FalseField $noReport "ExecutionReportsCreated" "Execution reports created"
Assert-FalseField $noReport "TcaResultLinesRepresentExecutionReports" "TCA result lines represented as execution reports"
$noOrder = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-no-order-created-audit.json")
Assert-FalseField $noOrder "OrdersCreated" "Orders created"
Assert-FalseField $noOrder "TcaResultLinesRepresentOrders" "TCA result lines represented as orders"
$noRoute = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-no-route-no-submission-audit.json")
Assert-FalseField $noRoute "RoutesCreated" "Routes created"
Assert-FalseField $noRoute "SubmissionsCreated" "Submissions created"
$noPolygon = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-no-polygon-api-call-audit.json")
Assert-FalseField $noPolygon "PolygonApiCalled" "Polygon API called"
$noLmax = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-no-lmax-call-audit.json")
Assert-FalseField $noLmax "LmaxCalled" "LMAX called"
$noExternal = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-no-external-api-call-audit.json")
Assert-FalseField $noExternal "ExternalApiCalled" "External API called"
$noRuntime = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-no-broker-marketdata-runtime-audit.json")
Assert-FalseField $noRuntime "BrokerMarketDataRuntimeStarted" "Broker/MarketData runtime started"

$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r044-build-test-validator-evidence.json")
if ($evidence.DotnetBuild.Status -notin @("PASS", "PASS_WITH_WARNINGS")) { Fail "Build evidence missing or failing" }
if ($evidence.FocusedR044StaticChecks.Status -ne "PASS") { Fail "Focused R044 static checks missing or failing" }
if ($evidence.UnitTests.Status -notin @("PASS", "PASS_WITH_WARNINGS", "NOT_FEASIBLE")) { Fail "Unit test evidence missing" }

Write-Host "EXEC-SIM-R044 validator passed."
Write-Host "Classifications:"
Write-Host "EXEC_SIM_R044_PASS_ADDITIONAL_HISTORICAL_TCA_BACKTEST_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R044_PASS_CANONICAL_SESSION_POLICY_COMPARISON_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R044_PASS_FIVE_DATE_SEVEN_SYMBOL_TCA_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R044_PASS_NO_DB_IMPORT_NO_REAL_FILL_NO_ORDER_GATE_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R044_PASS_DUPLICATE_AWARE_TCA_BACKTEST_READY_NO_EXTERNAL"
