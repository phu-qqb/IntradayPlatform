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
    "phase-exec-sim-r029-summary.md",
    "phase-exec-sim-r029-row-level-validation-contract.json",
    "phase-exec-sim-r029-r028-accepted-files-used.json",
    "phase-exec-sim-r029-row-level-validation-results.json",
    "phase-exec-sim-r029-row-count-comparison.json",
    "phase-exec-sim-r029-rejected-row-summary.json",
    "phase-exec-sim-r029-duplicate-out-of-order-handling.json",
    "phase-exec-sim-r029-opening-build-row-validation-results.json",
    "phase-exec-sim-r029-closing-flatten-row-validation-results.json",
    "phase-exec-sim-r029-eurusd-row-validation-result.json",
    "phase-exec-sim-r029-usdjpy-row-validation-result.json",
    "phase-exec-sim-r029-audusd-row-validation-result.json",
    "phase-exec-sim-r029-gbpusd-row-validation-result.json",
    "phase-exec-sim-r029-nzdusd-row-validation-result.json",
    "phase-exec-sim-r029-usdcad-row-validation-result.json",
    "phase-exec-sim-r029-usdchf-row-validation-result.json",
    "phase-exec-sim-r029-quote-window-readiness-results.json",
    "phase-exec-sim-r029-opening-build-quote-window-readiness.json",
    "phase-exec-sim-r029-closing-flatten-quote-window-readiness.json",
    "phase-exec-sim-r029-close-benchmark-readiness-results.json",
    "phase-exec-sim-r029-opening-build-close-benchmark-readiness.json",
    "phase-exec-sim-r029-closing-flatten-close-benchmark-readiness.json",
    "phase-exec-sim-r029-feed-quality-readiness-results.json",
    "phase-exec-sim-r029-opening-build-feed-quality-readiness.json",
    "phase-exec-sim-r029-closing-flatten-feed-quality-readiness.json",
    "phase-exec-sim-r029-sanitized-import-readiness-metadata.json",
    "phase-exec-sim-r029-session-category-metadata-source-preservation.json",
    "phase-exec-sim-r029-symbol-inversion-validation.json",
    "phase-exec-sim-r029-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r029-cost-guidance-preservation.json",
    "phase-exec-sim-r029-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r029-no-db-import-audit.json",
    "phase-exec-sim-r029-no-persisted-sanitized-row-audit.json",
    "phase-exec-sim-r029-no-backtest-simulation-audit.json",
    "phase-exec-sim-r029-no-tca-result-lines-audit.json",
    "phase-exec-sim-r029-no-polygon-api-call-audit.json",
    "phase-exec-sim-r029-no-lmax-call-audit.json",
    "phase-exec-sim-r029-no-external-api-call-audit.json",
    "phase-exec-sim-r029-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r029-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r029-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r029-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r029-no-external-audit.json",
    "phase-exec-sim-r029-forbidden-actions-audit.json",
    "phase-exec-sim-r029-next-phase-recommendation.json",
    "phase-exec-sim-r029-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsDir $artifact))) {
        Fail-Gate "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "Required R029 artifact missing: $artifact"
    }
}

$contract = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-row-level-validation-contract.json") "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING"
$used = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-r028-accepted-files-used.json") "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING"
$rows = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-row-level-validation-results.json") "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING"
$counts = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-row-count-comparison.json") "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING"
$rejected = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-rejected-row-summary.json") "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING"
$dupes = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-duplicate-out-of-order-handling.json") "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING"
$openingRows = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-opening-build-row-validation-results.json") "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING"
$closingRows = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-closing-flatten-row-validation-results.json") "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING"
$windows = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-quote-window-readiness-results.json") "EXEC_SIM_R029_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING"
$openingWindows = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-opening-build-quote-window-readiness.json") "EXEC_SIM_R029_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING"
$closingWindows = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-closing-flatten-quote-window-readiness.json") "EXEC_SIM_R029_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING"
$close = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-close-benchmark-readiness-results.json") "EXEC_SIM_R029_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING"
$openingClose = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-opening-build-close-benchmark-readiness.json") "EXEC_SIM_R029_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING"
$closingClose = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-closing-flatten-close-benchmark-readiness.json") "EXEC_SIM_R029_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING"
$feed = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-feed-quality-readiness-results.json") "EXEC_SIM_R029_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING"
$openingFeed = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-opening-build-feed-quality-readiness.json") "EXEC_SIM_R029_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING"
$closingFeed = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-closing-flatten-feed-quality-readiness.json") "EXEC_SIM_R029_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING"
$importReadiness = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-sanitized-import-readiness-metadata.json") "EXEC_SIM_R029_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING"
$sessionSource = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-session-category-metadata-source-preservation.json") "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING"
$inversion = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-symbol-inversion-validation.json") "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING"
$direct = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-direct-cross-exclusion-preservation.json") "EXEC_SIM_R029_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED"
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-cost-guidance-preservation.json") "EXEC_SIM_R029_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$nonmajor = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-nonmajor-calibration-preservation.json") "EXEC_SIM_R029_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$noDb = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-no-db-import-audit.json") "EXEC_SIM_R029_FAIL_DB_IMPORT_OCCURRED"
$noSanitized = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-no-persisted-sanitized-row-audit.json") "EXEC_SIM_R029_FAIL_SANITIZED_QUOTE_ROWS_CREATED"
$noBacktest = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-no-backtest-simulation-audit.json") "EXEC_SIM_R029_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
$noTca = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-no-tca-result-lines-audit.json") "EXEC_SIM_R029_FAIL_TCA_RESULTS_PRODUCED"
$api = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-no-external-api-call-audit.json") "EXEC_SIM_R029_FAIL_API_CALL_DETECTED"
$runtime = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-no-broker-marketdata-runtime-audit.json") "EXEC_SIM_R029_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
$order = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-no-order-fill-report-route-audit.json") "EXEC_SIM_R029_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-usdjpy-caveat-preservation.json") "EXEC_SIM_R029_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-lmax-readonly-baseline-reference.json") "EXEC_SIM_R029_FAIL_API_CALL_DETECTED"
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-no-external-audit.json") "EXEC_SIM_R029_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-forbidden-actions-audit.json") "EXEC_SIM_R029_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r029-build-test-validator-evidence.json") "EXEC_SIM_R029_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$contract.rowLevelValidationContractCreated) "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "Row-level validation contract missing."
if ($contract.SourceManifestValidationPhase -ne "EXEC-SIM-R028") { Fail-Gate "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "R028 accepted files not referenced." }
if ($contract.CoverageMode -ne "AllAvailable15MinuteClosesWithinAuthorizedTimeRange") { Fail-Gate "EXEC_SIM_R029_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING" "Coverage mode missing." }
foreach ($symbol in @("EURUSD","USDJPY","AUDUSD","GBPUSD","NZDUSD","USDCAD","USDCHF")) { Require-Contains @($contract.ExpectedSymbols) $symbol "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "Missing expected symbol $symbol." }
Require-Contains @($contract.ExpectedSessionWindowCategories) "OpeningBuild" "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "OpeningBuild expected category missing."
Require-Contains @($contract.ExpectedSessionWindowCategories) "ClosingFlatten" "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "ClosingFlatten expected category missing."
Require-Contains @($contract.ValidationStatuses) "RowValidationQuarantinedRawPayloadRisk" "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "Raw payload rejection/classification missing."
foreach ($property in @("NoDbImport","NoPersistedSanitizedQuoteRows","NoBacktest","NoSimulation","NoTcaResultLines","NoOrdersFillsReportsRoutes")) {
    Require-True ([bool]$contract.$property) "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "Contract safety flag missing: $property"
}

Require-True ([bool]$used.r028AcceptedFilesUsedCreated) "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "R028 accepted files artifact missing."
if ($used.acceptedFileCount -ne 14 -or @($used.Entries).Count -ne 14) { Fail-Gate "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "R028 accepted file count mismatch." }
Require-True ([bool]$used.QuoteRowsReadForValidation) "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "Rows were not read for R029 validation."
Require-False ([bool]$used.DbImportOccurred) "EXEC_SIM_R029_FAIL_DB_IMPORT_OCCURRED" "DB import occurred."
Require-False ([bool]$used.PersistedSanitizedQuoteRowsCreated) "EXEC_SIM_R029_FAIL_SANITIZED_QUOTE_ROWS_CREATED" "Persisted sanitized rows created."

Require-True ([bool]$rows.rowLevelValidationResultsCreated) "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "Row validation results missing."
if ($rows.resultCount -ne 14 -or $rows.openingBuildResultCount -ne 7 -or $rows.closingFlattenResultCount -ne 7) { Fail-Gate "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "Row validation result counts mismatch." }
Require-Contains @($rows.classifications) "EXEC_SIM_R029_PASS_OPENING_CLOSING_QUOTE_WINDOW_READY_NO_EXTERNAL" "EXEC_SIM_R029_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING" "Quote-window pass classification missing."
Require-Contains @($rows.classifications) "EXEC_SIM_R029_PASS_CLOSE_BENCHMARK_FEED_QUALITY_READY_NO_EXTERNAL" "EXEC_SIM_R029_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING" "Feed-quality pass classification missing."
Require-Contains @($rows.classifications) "EXEC_SIM_R029_PASS_NO_IMPORT_NO_BACKTEST_GATE_READY_NO_EXTERNAL" "EXEC_SIM_R029_FAIL_BACKTEST_OR_SIMULATION_EXECUTED" "No-import/no-backtest classification missing."

foreach ($category in @("OpeningBuild","ClosingFlatten")) {
    foreach ($symbol in @("EURUSD","USDJPY","AUDUSD","GBPUSD","NZDUSD","USDCAD","USDCHF")) {
        $matches = @($rows.results) | Where-Object { $_.Symbol -eq $symbol -and $_.SessionWindowCategory -eq $category }
        if (@($matches).Count -ne 1) { Fail-Gate "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "Missing row validation result for $symbol $category." }
        $entry = $matches | Select-Object -First 1
        if ($entry.RowCountObserved -ne $entry.RowCountDeclared) { Fail-Gate "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "Row count mismatch for $symbol $category." }
        if ($entry.AcceptedRowCount -le 0) { Fail-Gate "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "No accepted rows for $symbol $category." }
        if ($entry.ValidationStatus -ne "RowValidationAccepted" -and $entry.ValidationStatus -ne "RowValidationAcceptedWithWarnings" -and $entry.ValidationStatus -ne "RowValidationAcceptedWithRejectedRows") { Fail-Gate "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "Invalid row validation status for $symbol $category." }
        if ($entry.InvalidBidAskRowCount -ne 0 -or $entry.AskLessThanBidRowCount -ne 0) { Fail-Gate "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "Invalid bid/ask rows for $symbol $category." }
        if ($entry.SymbolMismatchRowCount -ne 0) { Fail-Gate "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "Symbol mismatch rows for $symbol $category." }
        if ($entry.RawPayloadSerializedTrueRowCount -ne 0) { Fail-Gate "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "Raw payload serialized rows for $symbol $category." }
        Require-True ([bool]$entry.MidSpreadSpreadBpsDerived) "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "Mid/spread/spreadBps derivation missing for $symbol $category."
    }
}

Require-True ([bool]$counts.rowCountComparisonCreated) "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "Row-count comparison missing."
if ($counts.comparisonCount -ne 14) { Fail-Gate "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "Row-count comparison count mismatch." }
Require-True ([bool]$counts.allObservedCountsMatchManifest) "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "Observed row counts do not match manifests."
Require-True ([bool]$rejected.rejectedRowSummaryCreated) "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "Rejected row summary missing."
Require-False ([bool]$rejected.rejectedRowsPersisted) "EXEC_SIM_R029_FAIL_SANITIZED_QUOTE_ROWS_CREATED" "Rejected rows persisted."
if ($rejected.InvalidBidAskRowCount -ne 0 -or $rejected.SymbolMismatchRowCount -ne 0 -or $rejected.RawPayloadSerializedTrueRowCount -ne 0) { Fail-Gate "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "Unsafe rejected rows detected." }
Require-True ([bool]$dupes.duplicateOutOfOrderHandlingCreated) "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "Duplicate/out-of-order handling missing."
Require-True ([bool]$dupes.deterministicHandling) "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "Duplicate/out-of-order handling not deterministic."

if ($openingRows.resultCount -ne 7 -or $closingRows.resultCount -ne 7) { Fail-Gate "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "Opening/closing row validation artifacts missing." }
Require-True ([bool]$windows.quoteWindowReadinessResultsCreated) "EXEC_SIM_R029_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING" "Quote-window readiness missing."
if ($windows.CoverageMode -ne "AllAvailable15MinuteClosesWithinAuthorizedTimeRange" -or $windows.evaluatedWindowCount -ne 224) { Fail-Gate "EXEC_SIM_R029_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING" "Quote-window coverage mismatch." }
if ($openingWindows.evaluatedWindowCount -ne 112 -or $closingWindows.evaluatedWindowCount -ne 112) { Fail-Gate "EXEC_SIM_R029_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING" "Opening/closing quote-window counts mismatch." }
Require-True ([bool]$close.closeBenchmarkReadinessResultsCreated) "EXEC_SIM_R029_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING" "Close benchmark readiness missing."
if ($close.resultCount -ne 224 -or $openingClose.resultCount -ne 112 -or $closingClose.resultCount -ne 112) { Fail-Gate "EXEC_SIM_R029_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING" "Close benchmark counts mismatch." }
Require-True ([bool]$feed.feedQualityReadinessResultsCreated) "EXEC_SIM_R029_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING" "Feed-quality readiness missing."
if ($feed.resultCount -ne 14 -or $openingFeed.resultCount -ne 7 -or $closingFeed.resultCount -ne 7) { Fail-Gate "EXEC_SIM_R029_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING" "Feed-quality counts mismatch." }

Require-True ([bool]$importReadiness.sanitizedImportReadinessMetadataCreated) "EXEC_SIM_R029_FAIL_QUOTE_WINDOW_OR_FEED_QUALITY_MISSING" "Sanitized import-readiness metadata missing."
Require-True ([bool]$importReadiness.metadataOnly) "EXEC_SIM_R029_FAIL_SANITIZED_QUOTE_ROWS_CREATED" "Import-readiness output is not metadata-only."
Require-False ([bool]$importReadiness.persistedSanitizedQuoteRowsCreated) "EXEC_SIM_R029_FAIL_SANITIZED_QUOTE_ROWS_CREATED" "Persisted sanitized rows created."
Require-False ([bool]$importReadiness.dbImportOccurred) "EXEC_SIM_R029_FAIL_DB_IMPORT_OCCURRED" "DB import occurred."
Require-True ([bool]$sessionSource.sessionCategoryMetadataSourcePreservationCreated) "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "Session category source preservation missing."
Require-True ([bool]$sessionSource.sessionCategoryWarningPreserved) "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "Session category warning not preserved."
if ($sessionSource.SessionWindowCategorySource -ne "R027AuthorizationMetadata") { Fail-Gate "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "Session category source weakened." }
Require-False ([bool]$sessionSource.warningWeakened) "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "Session warning weakened."

Require-True ([bool]$inversion.symbolInversionValidationCreated) "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "Symbol inversion validation missing."
Require-True ([bool]$inversion.allSymbolsPresent) "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "Not all symbols present."
Require-True ([bool]$inversion.usdJpyCaveatPreserved) "EXEC_SIM_R029_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
Require-True ([bool]$inversion.usdCadInversionPreserved) "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "USDCAD inversion missing."
Require-True ([bool]$inversion.usdChfInversionPreserved) "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "USDCHF inversion missing."
Require-True ([bool]$inversion.nonInvertedSymbolsPreserved) "EXEC_SIM_R029_FAIL_ROW_VALIDATION_MISSING" "Non-inverted mappings missing."
Require-False ([bool]$inversion.audusdMisclassifiedFailed) "EXEC_SIM_R029_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD misclassified."
Require-True ([bool]$direct.directCrossExclusionPreserved) "EXEC_SIM_R029_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct-cross exclusion missing."
Require-False ([bool]$direct.directCrossesInExecutionBatch) "EXEC_SIM_R029_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct crosses included."
Require-False ([bool]$direct.directCrossExecutionAllowedByDefault) "EXEC_SIM_R029_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct-cross execution allowed."
Require-False ([bool]$direct.guidanceWeakened) "EXEC_SIM_R029_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct-cross guidance weakened."
if ($cost.bestCaseMajorTargetUsdPerMillion -ne 5) { Fail-Gate "EXEC_SIM_R029_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million guidance missing." }
Require-True ([bool]$cost.fiveUsdPerMillionBestCaseMajorOnly) "EXEC_SIM_R029_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million not best-case major-only."
Require-False ([bool]$cost.fiveUsdPerMillionUniversalized) "EXEC_SIM_R029_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million universalized."
Require-True ([bool]$nonmajor.RequiresLiquidityCalibration) "EXEC_SIM_R029_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "Nonmajor calibration missing."
Require-False ([bool]$nonmajor.calibrationRequirementWeakened) "EXEC_SIM_R029_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "Nonmajor calibration weakened."

Require-False ([bool]$noDb.quotesImportedIntoDb) "EXEC_SIM_R029_FAIL_DB_IMPORT_OCCURRED" "Quotes imported into DB."
Require-False ([bool]$noDb.dbWriteOccurred) "EXEC_SIM_R029_FAIL_DB_IMPORT_OCCURRED" "DB write occurred."
Require-False ([bool]$noDb.paperLedgerStateCommitted) "EXEC_SIM_R029_FAIL_DB_IMPORT_OCCURRED" "Paper ledger committed."
Require-False ([bool]$noSanitized.persistedSanitizedQuoteRowsCreated) "EXEC_SIM_R029_FAIL_SANITIZED_QUOTE_ROWS_CREATED" "Persisted sanitized rows created."
Require-False ([bool]$noSanitized.sanitizedQuoteRowsCreatedForPersistence) "EXEC_SIM_R029_FAIL_SANITIZED_QUOTE_ROWS_CREATED" "Sanitized rows created for persistence."
Require-False ([bool]$noBacktest.newBacktestExecuted) "EXEC_SIM_R029_FAIL_BACKTEST_OR_SIMULATION_EXECUTED" "Backtest executed."
Require-False ([bool]$noBacktest.newSimulationExecuted) "EXEC_SIM_R029_FAIL_BACKTEST_OR_SIMULATION_EXECUTED" "Simulation executed."
Require-False ([bool]$noTca.tcaResultLinesProduced) "EXEC_SIM_R029_FAIL_TCA_RESULTS_PRODUCED" "TCA result lines produced."
Require-False ([bool]$noTca.simulationResultLinesProduced) "EXEC_SIM_R029_FAIL_TCA_RESULTS_PRODUCED" "Simulation result lines produced."
Require-False ([bool]$api.polygonApiCalled) "EXEC_SIM_R029_FAIL_API_CALL_DETECTED" "Polygon API called."
Require-False ([bool]$api.lmaxCalled) "EXEC_SIM_R029_FAIL_API_CALL_DETECTED" "LMAX called."
Require-False ([bool]$api.externalApiCalled) "EXEC_SIM_R029_FAIL_API_CALL_DETECTED" "External API called."
Require-False ([bool]$api.filesDownloaded) "EXEC_SIM_R029_FAIL_DOWNLOAD_EXECUTED" "Files downloaded."
Require-False ([bool]$runtime.brokerActivationDetected) "EXEC_SIM_R029_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "Broker activation detected."
Require-False ([bool]$runtime.socketOpened) "EXEC_SIM_R029_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "Socket opened."
Require-False ([bool]$runtime.tlsOpened) "EXEC_SIM_R029_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "TLS opened."
Require-False ([bool]$runtime.fixOpened) "EXEC_SIM_R029_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "FIX opened."
Require-False ([bool]$runtime.marketDataRequestSent) "EXEC_SIM_R029_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "MarketDataRequest sent."
Require-False ([bool]$runtime.marketDataResponseRead) "EXEC_SIM_R029_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "MarketDataResponse read."
Require-False ([bool]$runtime.schedulerServiceTimerPollingBackgroundJobIntroduced) "EXEC_SIM_R029_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler/service introduced."
Require-False ([bool]$runtime.automaticExecutionIntroduced) "EXEC_SIM_R029_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Automatic execution introduced."
Require-False ([bool]$order.ordersCreated) "EXEC_SIM_R029_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders created."
Require-False ([bool]$order.executableOrdersCreated) "EXEC_SIM_R029_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Executable orders created."
Require-False ([bool]$order.childOrdersCreated) "EXEC_SIM_R029_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Child orders created."
Require-False ([bool]$order.fillEntitiesCreated) "EXEC_SIM_R029_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fills created."
Require-False ([bool]$order.executionReportEntitiesCreated) "EXEC_SIM_R029_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Execution reports created."
Require-False ([bool]$order.routesCreated) "EXEC_SIM_R029_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Routes created."
Require-False ([bool]$order.submissionsCreated) "EXEC_SIM_R029_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Submissions created."

Require-True ([bool]$usdjpy.usdjpyCaveatPreserved) "EXEC_SIM_R029_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
if ($usdjpy.PortfolioNormalizedSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or -not [bool]$usdjpy.RequiresInversion -or $usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8") { Fail-Gate "EXEC_SIM_R029_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat weakened." }
Require-False ([bool]$usdjpy.audusdMisclassifiedFailed) "EXEC_SIM_R029_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD misclassified."
Require-True ([bool]$lmax.referenceOnly) "EXEC_SIM_R029_FAIL_API_CALL_DETECTED" "LMAX reference weakened."
Require-False ([bool]$lmax.lmaxCalledInR029) "EXEC_SIM_R029_FAIL_API_CALL_DETECTED" "LMAX called."
if ($lmax.audusdStatus -notmatch "not failed") { Fail-Gate "EXEC_SIM_R029_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD incorrectly marked failed." }

foreach ($property in @("polygonApiCalled","lmaxCalled","externalApiCalled","filesDownloaded","brokerRuntimeDetected","quotesImportedIntoDb","persistedSanitizedQuoteRowsCreated","newBacktestExecuted","newSimulationExecuted","tcaResultLinesProduced","ordersFillsReportsRoutesSubmissionsCreated","livePaperBrokerProductionTradingStateMutated","paperLedgerStateCommitted")) {
    Require-False ([bool]$noExternal.$property) "EXEC_SIM_R029_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected $property."
}
Require-False ([bool]$forbidden.forbiddenActionsDetected) "EXEC_SIM_R029_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected."

if ($evidence.dotnetBuildNoRestore -notlike "PASS*" -or $evidence.focusedR029Tests -notlike "PASS*" -or $evidence.unitTestsIfFeasible -notlike "PASS*" -or $evidence.validator -notlike "PASS*") {
    Fail-Gate "EXEC_SIM_R029_FAIL_BUILD_OR_TESTS" "Build/test/validator evidence is missing or not passing."
}

Write-Host "EXEC_SIM_R029_PASS_HISTORICAL_WINDOW_ROW_VALIDATION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R029_PASS_OPENING_CLOSING_QUOTE_WINDOW_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R029_PASS_CLOSE_BENCHMARK_FEED_QUALITY_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R029_PASS_NO_IMPORT_NO_BACKTEST_GATE_READY_NO_EXTERNAL"
