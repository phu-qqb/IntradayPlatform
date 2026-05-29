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
    try { return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json }
    catch { Fail-Gate $FailureClassification "Artifact is not valid JSON: $Path" }
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
    "phase-exec-sim-r030-summary.md",
    "phase-exec-sim-r030-historical-window-backtest-authorization-contract.json",
    "phase-exec-sim-r030-historical-window-backtest-authorization-request.json",
    "phase-exec-sim-r030-historical-window-backtest-preflight-contract.json",
    "phase-exec-sim-r030-historical-window-backtest-authorization-result.json",
    "phase-exec-sim-r030-r029-row-validation-reference.json",
    "phase-exec-sim-r030-authorized-session-window-entries.json",
    "phase-exec-sim-r030-opening-build-entries-authorized.json",
    "phase-exec-sim-r030-closing-flatten-entries-authorized.json",
    "phase-exec-sim-r030-accepted-rejected-row-summary.json",
    "phase-exec-sim-r030-quote-window-readiness-authorized.json",
    "phase-exec-sim-r030-close-benchmark-readiness-authorized.json",
    "phase-exec-sim-r030-feed-quality-readiness-authorized.json",
    "phase-exec-sim-r030-sanitized-import-readiness-authorized.json",
    "phase-exec-sim-r030-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r030-inversion-preservation.json",
    "phase-exec-sim-r030-expected-r031-policy-list.json",
    "phase-exec-sim-r030-expected-r031-report-list.json",
    "phase-exec-sim-r030-cost-guidance-preservation.json",
    "phase-exec-sim-r030-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r030-no-row-revalidation-audit.json",
    "phase-exec-sim-r030-no-db-import-audit.json",
    "phase-exec-sim-r030-no-sanitized-quote-row-creation-audit.json",
    "phase-exec-sim-r030-no-backtest-simulation-audit.json",
    "phase-exec-sim-r030-no-tca-result-lines-audit.json",
    "phase-exec-sim-r030-no-executable-schedule-audit.json",
    "phase-exec-sim-r030-no-child-slices-audit.json",
    "phase-exec-sim-r030-no-child-orders-audit.json",
    "phase-exec-sim-r030-no-polygon-api-call-audit.json",
    "phase-exec-sim-r030-no-lmax-call-audit.json",
    "phase-exec-sim-r030-no-external-api-call-audit.json",
    "phase-exec-sim-r030-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r030-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r030-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r030-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r030-no-external-audit.json",
    "phase-exec-sim-r030-forbidden-actions-audit.json",
    "phase-exec-sim-r030-next-phase-recommendation.json",
    "phase-exec-sim-r030-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsDir $artifact))) {
        Fail-Gate "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING" "Required R030 artifact missing: $artifact"
    }
}

$contract = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-historical-window-backtest-authorization-contract.json") "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING"
$request = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-historical-window-backtest-authorization-request.json") "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING"
$preflight = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-historical-window-backtest-preflight-contract.json") "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING"
$result = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-historical-window-backtest-authorization-result.json") "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING"
$reference = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-r029-row-validation-reference.json") "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING"
$entries = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-authorized-session-window-entries.json") "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING"
$opening = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-opening-build-entries-authorized.json") "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING"
$closing = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-closing-flatten-entries-authorized.json") "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING"
$rowsSummary = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-accepted-rejected-row-summary.json") "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING"
$quoteWindow = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-quote-window-readiness-authorized.json") "EXEC_SIM_R030_FAIL_QUOTE_WINDOW_OR_FEED_READINESS_MISSING"
$close = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-close-benchmark-readiness-authorized.json") "EXEC_SIM_R030_FAIL_QUOTE_WINDOW_OR_FEED_READINESS_MISSING"
$feed = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-feed-quality-readiness-authorized.json") "EXEC_SIM_R030_FAIL_QUOTE_WINDOW_OR_FEED_READINESS_MISSING"
$import = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-sanitized-import-readiness-authorized.json") "EXEC_SIM_R030_FAIL_QUOTE_WINDOW_OR_FEED_READINESS_MISSING"
$direct = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-direct-cross-exclusion-preservation.json") "EXEC_SIM_R030_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED"
$inversion = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-inversion-preservation.json") "EXEC_SIM_R030_FAIL_USDJPY_CAVEAT_WEAKENED"
$policies = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-expected-r031-policy-list.json") "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING"
$reports = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-expected-r031-report-list.json") "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING"
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-cost-guidance-preservation.json") "EXEC_SIM_R030_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$nonmajor = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-nonmajor-calibration-preservation.json") "EXEC_SIM_R030_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$noRows = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-no-row-revalidation-audit.json") "EXEC_SIM_R030_FAIL_ROW_REVALIDATION_EXECUTED"
$noDb = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-no-db-import-audit.json") "EXEC_SIM_R030_FAIL_DB_IMPORT_OCCURRED"
$noSanitized = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-no-sanitized-quote-row-creation-audit.json") "EXEC_SIM_R030_FAIL_SANITIZED_QUOTE_ROWS_CREATED"
$noBacktest = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-no-backtest-simulation-audit.json") "EXEC_SIM_R030_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
$noTca = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-no-tca-result-lines-audit.json") "EXEC_SIM_R030_FAIL_TCA_RESULTS_PRODUCED"
$noSchedule = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-no-executable-schedule-audit.json") "EXEC_SIM_R030_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$noSlices = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-no-child-slices-audit.json") "EXEC_SIM_R030_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$noChildOrders = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-no-child-orders-audit.json") "EXEC_SIM_R030_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$api = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-no-external-api-call-audit.json") "EXEC_SIM_R030_FAIL_API_CALL_DETECTED"
$runtime = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-no-broker-marketdata-runtime-audit.json") "EXEC_SIM_R030_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
$order = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-no-order-fill-report-route-audit.json") "EXEC_SIM_R030_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-usdjpy-caveat-preservation.json") "EXEC_SIM_R030_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-lmax-readonly-baseline-reference.json") "EXEC_SIM_R030_FAIL_API_CALL_DETECTED"
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-no-external-audit.json") "EXEC_SIM_R030_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-forbidden-actions-audit.json") "EXEC_SIM_R030_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r030-build-test-validator-evidence.json") "EXEC_SIM_R030_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$contract.historicalWindowBacktestAuthorizationContractCreated) "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING" "Authorization contract missing."
if ($contract.SourceRowValidationPhase -ne "EXEC-SIM-R029") { Fail-Gate "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING" "R029 source not referenced." }
foreach ($property in @("AuthorizationOnly","NoApiCall","NoDownload","NoRowRevalidation","NoBacktest","NoSimulation","NoImport","NoPersistedSanitizedRows","NoTcaResultLines","NoExecutableSchedules","NoChildSlicesOrOrders","NoOrdersFillsReportsRoutes")) {
    Require-True ([bool]$contract.$property) "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING" "Contract safety flag missing: $property"
}

Require-True ([bool]$request.historicalWindowBacktestAuthorizationRequestCreated) "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING" "Authorization request missing."
if ($request.IntendedNextPhase -ne "EXEC-SIM-R031") { Fail-Gate "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING" "Wrong next phase." }
if (@($request.Symbols).Count -ne 7 -or @($request.SessionWindowCategories).Count -ne 2) { Fail-Gate "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING" "Request symbols/categories missing." }
Require-True ([bool]$request.AuthorizationOnly) "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING" "Request is not authorization-only."
Require-True ([bool]$request.NoApiCall) "EXEC_SIM_R030_FAIL_API_CALL_DETECTED" "Request allows API call."
Require-True ([bool]$request.NoBacktest) "EXEC_SIM_R030_FAIL_BACKTEST_OR_SIMULATION_EXECUTED" "Request allows backtest."
Require-True ([bool]$request.NoImport) "EXEC_SIM_R030_FAIL_DB_IMPORT_OCCURRED" "Request allows import."
Require-True ([bool]$request.NoTcaResultLines) "EXEC_SIM_R030_FAIL_TCA_RESULTS_PRODUCED" "Request allows TCA lines."
Require-True ([bool]$preflight.historicalWindowBacktestPreflightContractCreated) "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING" "Preflight contract missing."
Require-True ([bool]$preflight.RowRevalidationDeferred) "EXEC_SIM_R030_FAIL_ROW_REVALIDATION_EXECUTED" "Row revalidation not deferred."
Require-True ([bool]$preflight.BacktestExecutionDeferredToR031) "EXEC_SIM_R030_FAIL_BACKTEST_OR_SIMULATION_EXECUTED" "Backtest not deferred."

Require-True ([bool]$result.historicalWindowBacktestAuthorizationResultCreated) "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING" "Authorization result missing."
if ($result.AuthorizationStatus -ne "HistoricalWindowBacktestAuthorizationReadyNoExternal" -or $result.AuthorizedEntryCount -ne 14 -or $result.BlockedEntryCount -ne 0) { Fail-Gate "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING" "Authorization result counts/status invalid." }
Require-Contains @($result.Classifications) "EXEC_SIM_R030_PASS_HISTORICAL_WINDOW_BACKTEST_AUTHORIZATION_READY_NO_EXTERNAL" "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING" "Authorization pass classification missing."
Require-Contains @($result.Classifications) "EXEC_SIM_R030_PASS_OPENING_CLOSING_BACKTEST_PREFLIGHT_READY_NO_EXTERNAL" "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING" "Opening/closing preflight classification missing."
Require-Contains @($result.Classifications) "EXEC_SIM_R030_PASS_NO_REVALIDATION_NO_BACKTEST_GATE_READY_NO_EXTERNAL" "EXEC_SIM_R030_FAIL_BACKTEST_OR_SIMULATION_EXECUTED" "No-revalidation/no-backtest classification missing."
Require-True ([bool]$result.ReadyForR031) "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING" "R031 readiness missing."

Require-True ([bool]$reference.r029RowValidationReferenceCreated) "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING" "R029 reference missing."
if ($reference.RowValidationResultCount -ne 14 -or $reference.QuoteWindowReadinessCount -ne 224 -or $reference.CloseBenchmarkReadinessCount -ne 224 -or $reference.FeedQualityReadinessCount -ne 14) { Fail-Gate "EXEC_SIM_R030_FAIL_QUOTE_WINDOW_OR_FEED_READINESS_MISSING" "R029 readiness counts invalid." }
Require-False ([bool]$reference.RowRevalidationExecutedInR030) "EXEC_SIM_R030_FAIL_ROW_REVALIDATION_EXECUTED" "Rows revalidated in R030."

if ($entries.AuthorizedEntryCount -ne 14 -or @($entries.Entries).Count -ne 14) { Fail-Gate "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING" "Authorized entries missing." }
if ($opening.AuthorizedEntryCount -ne 7 -or $closing.AuthorizedEntryCount -ne 7) { Fail-Gate "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING" "Opening/closing entries missing." }
foreach ($entry in @($entries.Entries)) {
    if ($entry.AcceptedRowCount -le 0) { Fail-Gate "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING" "Authorized entry has no accepted rows." }
    if ($entry.RejectedRowCount -ne 0) { Fail-Gate "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING" "Authorized entry has rejected rows." }
    if ($entry.QuoteWindowReadinessCount -le 0 -or $entry.CloseBenchmarkReadinessCount -le 0 -or $entry.FeedQualityReadinessCount -ne 1) { Fail-Gate "EXEC_SIM_R030_FAIL_QUOTE_WINDOW_OR_FEED_READINESS_MISSING" "Readiness missing for authorized entry." }
    Require-True ([bool]$entry.SanitizedImportReadinessMetadataPresent) "EXEC_SIM_R030_FAIL_QUOTE_WINDOW_OR_FEED_READINESS_MISSING" "Sanitized import-readiness missing for authorized entry."
    Require-False ([bool]$entry.QuarantinedFileIncluded) "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING" "Quarantined file included."
    Require-False ([bool]$entry.DirectCrossIncluded) "EXEC_SIM_R030_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct cross included."
}

Require-True ([bool]$rowsSummary.acceptedRejectedRowSummaryCreated) "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING" "Accepted/rejected summary missing."
Require-True ([bool]$rowsSummary.AllRejectedRowCountsZero) "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING" "Rejected rows not zero."
if ($rowsSummary.TotalRejectedRows -ne 0) { Fail-Gate "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING" "Rejected rows detected." }
if ($quoteWindow.AuthorizedEntryCount -ne 14 -or $quoteWindow.TotalQuoteWindowReadinessRecords -ne 224) { Fail-Gate "EXEC_SIM_R030_FAIL_QUOTE_WINDOW_OR_FEED_READINESS_MISSING" "Quote-window authorization missing." }
if ($close.AuthorizedEntryCount -ne 14 -or $close.TotalCloseBenchmarkReadinessRecords -ne 224) { Fail-Gate "EXEC_SIM_R030_FAIL_QUOTE_WINDOW_OR_FEED_READINESS_MISSING" "Close benchmark authorization missing." }
if ($feed.AuthorizedEntryCount -ne 14 -or $feed.TotalFeedQualityReadinessRecords -ne 14) { Fail-Gate "EXEC_SIM_R030_FAIL_QUOTE_WINDOW_OR_FEED_READINESS_MISSING" "Feed-quality authorization missing." }
if ($import.AuthorizedEntryCount -ne 14) { Fail-Gate "EXEC_SIM_R030_FAIL_QUOTE_WINDOW_OR_FEED_READINESS_MISSING" "Sanitized import-readiness authorization missing." }
Require-True ([bool]$import.MetadataOnly) "EXEC_SIM_R030_FAIL_SANITIZED_QUOTE_ROWS_CREATED" "Import readiness is not metadata-only."
Require-False ([bool]$import.DbImportOccurred) "EXEC_SIM_R030_FAIL_DB_IMPORT_OCCURRED" "DB import occurred."
Require-False ([bool]$import.PersistedSanitizedQuoteRowsCreated) "EXEC_SIM_R030_FAIL_SANITIZED_QUOTE_ROWS_CREATED" "Persisted sanitized rows created."

Require-True ([bool]$direct.directCrossExclusionPreserved) "EXEC_SIM_R030_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct-cross exclusion missing."
Require-False ([bool]$direct.directCrossesInExecutionBatch) "EXEC_SIM_R030_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct crosses in batch."
Require-False ([bool]$direct.directCrossExecutionAllowedByDefault) "EXEC_SIM_R030_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct-cross execution allowed."
Require-False ([bool]$direct.guidanceWeakened) "EXEC_SIM_R030_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct-cross guidance weakened."
Require-True ([bool]$inversion.inversionPreservationCreated) "EXEC_SIM_R030_FAIL_USDJPY_CAVEAT_WEAKENED" "Inversion preservation missing."
if ($inversion.UsdJpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $inversion.UsdJpy.ExecutionTradableSymbol -ne "USDJPY" -or -not [bool]$inversion.UsdJpy.RequiresInversion -or $inversion.UsdJpy.SecurityID -ne "4004" -or $inversion.UsdJpy.SecurityIDSource -ne "8") { Fail-Gate "EXEC_SIM_R030_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat weakened." }
if ($inversion.UsdCad.NormalizedPortfolioSymbol -ne "CADUSD" -or -not [bool]$inversion.UsdCad.RequiresInversion) { Fail-Gate "EXEC_SIM_R030_FAIL_USDJPY_CAVEAT_WEAKENED" "USDCAD inversion missing." }
if ($inversion.UsdChf.NormalizedPortfolioSymbol -ne "CHFUSD" -or -not [bool]$inversion.UsdChf.RequiresInversion) { Fail-Gate "EXEC_SIM_R030_FAIL_USDJPY_CAVEAT_WEAKENED" "USDCHF inversion missing." }
Require-False ([bool]$inversion.AudUsdMisclassifiedFailed) "EXEC_SIM_R030_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD misclassified."

foreach ($policy in @("WakettPureLimitUntilClose","WakettFiveMarketSlicesAroundClose","PassiveUntilUrgency","CloseSeeking15m","CloseSeeking15mAdaptive","ControlledResidualCross","ImmediatePaperBenchmark","TWAPBenchmarkOnly","VWAPBenchmarkOnly","ManualReview","DoNotTrade")) {
    Require-Contains @($policies.Policies) $policy "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING" "Expected R031 policy missing: $policy"
}
Require-True ([bool]$reports.IncludesOpeningBuildReport) "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING" "OpeningBuild report expectation missing."
Require-True ([bool]$reports.IncludesClosingFlattenReport) "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING" "ClosingFlatten report expectation missing."
Require-True ([bool]$reports.IncludesNoOvernightResidualPenaltyReport) "EXEC_SIM_R030_FAIL_AUTHORIZATION_MISSING" "No-overnight report expectation missing."
if ($cost.bestCaseMajorTargetUsdPerMillion -ne 5) { Fail-Gate "EXEC_SIM_R030_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million guidance missing." }
Require-True ([bool]$cost.fiveUsdPerMillionBestCaseMajorOnly) "EXEC_SIM_R030_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million not best-case major-only."
Require-False ([bool]$cost.fiveUsdPerMillionUniversalized) "EXEC_SIM_R030_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million universalized."
Require-True ([bool]$nonmajor.RequiresLiquidityCalibration) "EXEC_SIM_R030_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "Nonmajor calibration missing."
Require-False ([bool]$nonmajor.calibrationRequirementWeakened) "EXEC_SIM_R030_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "Nonmajor calibration weakened."

Require-False ([bool]$noRows.quoteRowsRevalidated) "EXEC_SIM_R030_FAIL_ROW_REVALIDATION_EXECUTED" "Quote rows revalidated."
Require-False ([bool]$noRows.quoteRowsReadInR030) "EXEC_SIM_R030_FAIL_ROW_REVALIDATION_EXECUTED" "Quote rows read in R030."
Require-False ([bool]$noDb.quotesImportedIntoDb) "EXEC_SIM_R030_FAIL_DB_IMPORT_OCCURRED" "DB import occurred."
Require-False ([bool]$noSanitized.sanitizedQuoteRowsCreated) "EXEC_SIM_R030_FAIL_SANITIZED_QUOTE_ROWS_CREATED" "Sanitized rows created."
Require-False ([bool]$noBacktest.backtestExecuted) "EXEC_SIM_R030_FAIL_BACKTEST_OR_SIMULATION_EXECUTED" "Backtest executed."
Require-False ([bool]$noBacktest.simulationExecuted) "EXEC_SIM_R030_FAIL_BACKTEST_OR_SIMULATION_EXECUTED" "Simulation executed."
Require-False ([bool]$noTca.tcaResultLinesProduced) "EXEC_SIM_R030_FAIL_TCA_RESULTS_PRODUCED" "TCA result lines produced."
Require-False ([bool]$noSchedule.executableSchedulesCreated) "EXEC_SIM_R030_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Executable schedules created."
Require-False ([bool]$noSlices.childSlicesCreated) "EXEC_SIM_R030_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Child slices created."
Require-False ([bool]$noChildOrders.childOrdersCreated) "EXEC_SIM_R030_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Child orders created."
Require-False ([bool]$api.polygonApiCalled) "EXEC_SIM_R030_FAIL_API_CALL_DETECTED" "Polygon API called."
Require-False ([bool]$api.lmaxCalled) "EXEC_SIM_R030_FAIL_API_CALL_DETECTED" "LMAX called."
Require-False ([bool]$api.externalApiCalled) "EXEC_SIM_R030_FAIL_API_CALL_DETECTED" "External API called."
Require-False ([bool]$api.filesDownloaded) "EXEC_SIM_R030_FAIL_DOWNLOAD_EXECUTED" "Files downloaded."
Require-False ([bool]$runtime.brokerActivationDetected) "EXEC_SIM_R030_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "Broker activation detected."
Require-False ([bool]$runtime.socketOpened) "EXEC_SIM_R030_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "Socket opened."
Require-False ([bool]$runtime.tlsOpened) "EXEC_SIM_R030_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "TLS opened."
Require-False ([bool]$runtime.fixOpened) "EXEC_SIM_R030_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "FIX opened."
Require-False ([bool]$runtime.marketDataRequestSent) "EXEC_SIM_R030_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "MarketDataRequest sent."
Require-False ([bool]$runtime.marketDataResponseRead) "EXEC_SIM_R030_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "MarketDataResponse read."
Require-False ([bool]$runtime.schedulerServiceTimerPollingBackgroundJobIntroduced) "EXEC_SIM_R030_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler/service introduced."
Require-False ([bool]$runtime.automaticExecutionIntroduced) "EXEC_SIM_R030_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Automatic execution introduced."
Require-False ([bool]$order.ordersCreated) "EXEC_SIM_R030_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders created."
Require-False ([bool]$order.fillEntitiesCreated) "EXEC_SIM_R030_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fills created."
Require-False ([bool]$order.executionReportEntitiesCreated) "EXEC_SIM_R030_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Execution reports created."
Require-False ([bool]$order.routesCreated) "EXEC_SIM_R030_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Routes created."
Require-False ([bool]$order.submissionsCreated) "EXEC_SIM_R030_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Submissions created."

Require-True ([bool]$usdjpy.usdjpyCaveatPreserved) "EXEC_SIM_R030_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
if ($usdjpy.PortfolioNormalizedSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or -not [bool]$usdjpy.RequiresInversion -or $usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8") { Fail-Gate "EXEC_SIM_R030_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat weakened." }
Require-False ([bool]$usdjpy.audusdMisclassifiedFailed) "EXEC_SIM_R030_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD misclassified."
Require-True ([bool]$lmax.referenceOnly) "EXEC_SIM_R030_FAIL_API_CALL_DETECTED" "LMAX reference weakened."
Require-False ([bool]$lmax.lmaxCalledInR030) "EXEC_SIM_R030_FAIL_API_CALL_DETECTED" "LMAX called."
if ($lmax.audusdStatus -notmatch "not failed") { Fail-Gate "EXEC_SIM_R030_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD incorrectly marked failed." }

foreach ($property in @("polygonApiCalled","lmaxCalled","externalApiCalled","filesDownloaded","brokerRuntimeDetected","quoteRowsRevalidated","quotesImportedIntoDb","persistedSanitizedQuoteRowsCreated","backtestExecuted","simulationExecuted","tcaResultLinesProduced","executableSchedulesCreated","childSlicesCreated","childOrdersCreated","ordersFillsReportsRoutesSubmissionsCreated","livePaperBrokerProductionTradingStateMutated","paperLedgerStateCommitted")) {
    Require-False ([bool]$noExternal.$property) "EXEC_SIM_R030_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected $property."
}
Require-False ([bool]$forbidden.forbiddenActionsDetected) "EXEC_SIM_R030_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected."

if ($evidence.dotnetBuildNoRestore -notlike "PASS*" -or $evidence.focusedR030Tests -notlike "PASS*" -or $evidence.unitTestsIfFeasible -notlike "PASS*" -or $evidence.validator -notlike "PASS*") {
    Fail-Gate "EXEC_SIM_R030_FAIL_BUILD_OR_TESTS" "Build/test/validator evidence is missing or not passing."
}

Write-Host "EXEC_SIM_R030_PASS_HISTORICAL_WINDOW_BACKTEST_AUTHORIZATION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R030_PASS_OPENING_CLOSING_BACKTEST_PREFLIGHT_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R030_PASS_NO_REVALIDATION_NO_BACKTEST_GATE_READY_NO_EXTERNAL"
