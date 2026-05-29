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
    "phase-exec-sim-r028-summary.md",
    "phase-exec-sim-r028-manifest-validation-contract.json",
    "phase-exec-sim-r028-r027-authorized-files-used.json",
    "phase-exec-sim-r028-manifest-validation-results.json",
    "phase-exec-sim-r028-file-level-validation-results.json",
    "phase-exec-sim-r028-accepted-manifest-validation-outputs.json",
    "phase-exec-sim-r028-quarantined-manifest-validation-outputs.json",
    "phase-exec-sim-r028-missing-incomplete-manifest-diagnostics.json",
    "phase-exec-sim-r028-opening-build-manifest-validation.json",
    "phase-exec-sim-r028-closing-flatten-manifest-validation.json",
    "phase-exec-sim-r028-symbol-inversion-validation.json",
    "phase-exec-sim-r028-session-category-validation.json",
    "phase-exec-sim-r028-time-range-validation.json",
    "phase-exec-sim-r028-secret-raw-payload-validation.json",
    "phase-exec-sim-r028-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r028-cost-guidance-preservation.json",
    "phase-exec-sim-r028-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r028-no-row-level-validation-audit.json",
    "phase-exec-sim-r028-no-sanitized-quote-row-creation-audit.json",
    "phase-exec-sim-r028-no-quote-window-close-benchmark-feed-quality-audit.json",
    "phase-exec-sim-r028-no-backtest-simulation-audit.json",
    "phase-exec-sim-r028-no-tca-result-lines-audit.json",
    "phase-exec-sim-r028-no-polygon-api-call-audit.json",
    "phase-exec-sim-r028-no-lmax-call-audit.json",
    "phase-exec-sim-r028-no-external-api-call-audit.json",
    "phase-exec-sim-r028-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r028-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r028-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r028-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r028-no-external-audit.json",
    "phase-exec-sim-r028-forbidden-actions-audit.json",
    "phase-exec-sim-r028-next-phase-recommendation.json",
    "phase-exec-sim-r028-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsDir $artifact))) {
        Fail-Gate "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING" "Required R028 artifact missing: $artifact"
    }
}

$contract = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r028-manifest-validation-contract.json") "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING"
$authorized = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r028-r027-authorized-files-used.json") "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING"
$manifest = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r028-manifest-validation-results.json") "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING"
$file = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r028-file-level-validation-results.json") "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING"
$accepted = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r028-accepted-manifest-validation-outputs.json") "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING"
$quarantined = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r028-quarantined-manifest-validation-outputs.json") "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING"
$diagnostics = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r028-missing-incomplete-manifest-diagnostics.json") "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING"
$opening = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r028-opening-build-manifest-validation.json") "EXEC_SIM_R028_FAIL_INVERSION_OR_SESSION_METADATA_MISSING"
$closing = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r028-closing-flatten-manifest-validation.json") "EXEC_SIM_R028_FAIL_INVERSION_OR_SESSION_METADATA_MISSING"
$inversion = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r028-symbol-inversion-validation.json") "EXEC_SIM_R028_FAIL_INVERSION_OR_SESSION_METADATA_MISSING"
$session = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r028-session-category-validation.json") "EXEC_SIM_R028_FAIL_INVERSION_OR_SESSION_METADATA_MISSING"
$timeRange = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r028-time-range-validation.json") "EXEC_SIM_R028_FAIL_INVERSION_OR_SESSION_METADATA_MISSING"
$secret = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r028-secret-raw-payload-validation.json") "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING"
$direct = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r028-direct-cross-exclusion-preservation.json") "EXEC_SIM_R028_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED"
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r028-cost-guidance-preservation.json") "EXEC_SIM_R028_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$nonmajor = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r028-nonmajor-calibration-preservation.json") "EXEC_SIM_R028_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$row = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r028-no-row-level-validation-audit.json") "EXEC_SIM_R028_FAIL_ROW_LEVEL_VALIDATION_EXECUTED"
$sanitized = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r028-no-sanitized-quote-row-creation-audit.json") "EXEC_SIM_R028_FAIL_SANITIZED_QUOTE_ROWS_CREATED"
$windows = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r028-no-quote-window-close-benchmark-feed-quality-audit.json") "EXEC_SIM_R028_FAIL_QUOTE_WINDOW_OR_FEED_OUTPUT_CREATED"
$backtest = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r028-no-backtest-simulation-audit.json") "EXEC_SIM_R028_FAIL_BACKTEST_OR_SIMULATION_EXECUTED"
$tca = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r028-no-tca-result-lines-audit.json") "EXEC_SIM_R028_FAIL_TCA_RESULTS_PRODUCED"
$api = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r028-no-external-api-call-audit.json") "EXEC_SIM_R028_FAIL_API_CALL_DETECTED"
$runtime = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r028-no-broker-marketdata-runtime-audit.json") "EXEC_SIM_R028_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED"
$order = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r028-no-order-fill-report-route-audit.json") "EXEC_SIM_R028_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r028-usdjpy-caveat-preservation.json") "EXEC_SIM_R028_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r028-lmax-readonly-baseline-reference.json") "EXEC_SIM_R028_FAIL_API_CALL_DETECTED"
$noExternal = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r028-no-external-audit.json") "EXEC_SIM_R028_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r028-forbidden-actions-audit.json") "EXEC_SIM_R028_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-sim-r028-build-test-validator-evidence.json") "EXEC_SIM_R028_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$contract.manifestValidationContractCreated) "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING" "Manifest validation contract missing."
if ($contract.SourceAuthorizationPhase -ne "EXEC-SIM-R027") { Fail-Gate "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING" "R027 authorization not referenced." }
if ($contract.ExpectedProviderName -ne "PolygonOfflineFile" -or $contract.ExpectedProviderDatasetType -ne "HistoricalBboQuotes" -or $contract.ExpectedFileFormat -ne "NDJSON") {
    Fail-Gate "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING" "Provider, dataset, or format contract mismatch."
}
Require-Contains @($contract.ExpectedSessionWindowCategories) "OpeningBuild" "EXEC_SIM_R028_FAIL_INVERSION_OR_SESSION_METADATA_MISSING" "OpeningBuild expectation missing."
Require-Contains @($contract.ExpectedSessionWindowCategories) "ClosingFlatten" "EXEC_SIM_R028_FAIL_INVERSION_OR_SESSION_METADATA_MISSING" "ClosingFlatten expectation missing."
foreach ($status in @("ManifestValidationQuarantinedProviderMismatch","ManifestValidationQuarantinedDatasetMismatch","ManifestValidationQuarantinedFormatMismatch","ManifestValidationQuarantinedTimeRangeMismatch","ManifestValidationQuarantinedSessionCategoryMismatch","ManifestValidationQuarantinedSecretRisk","ManifestValidationQuarantinedRawPayloadRisk","ManifestValidationQuarantinedDirectCrossExecutionDisabled")) {
    Require-Contains @($contract.ValidationStatuses) $status "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING" "Missing quarantine status $status."
}
foreach ($property in @("NoRowLevelValidation","NoImport","NoPersistedSanitizedRows","NoQuoteWindows","NoCloseBenchmarks","NoFeedQualityResults","NoBacktest","NoSimulation","NoTcaResultLines","NoOrdersFillsReportsRoutes")) {
    Require-True ([bool]$contract.$property) "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING" "Contract safety flag missing: $property"
}

Require-True ([bool]$authorized.r027AuthorizedFilesUsedCreated) "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING" "R027 authorized files artifact missing."
if ($authorized.SourceAuthorizationPhase -ne "EXEC-SIM-R027" -or $authorized.EntryCountUsed -ne 14) { Fail-Gate "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING" "R027 authorized file count mismatch." }
Require-True ([bool]$authorized.ManifestContentsReadInR028) "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING" "Manifest read flag missing."
Require-False ([bool]$authorized.QuoteRowsReadInR028) "EXEC_SIM_R028_FAIL_ROW_LEVEL_VALIDATION_EXECUTED" "Quote rows were read."

if ($manifest.totalManifests -ne 14 -or $manifest.acceptedCount -ne 14 -or $manifest.quarantinedCount -ne 0) { Fail-Gate "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING" "Manifest validation result counts invalid." }
if ($manifest.openingBuildCount -ne 7 -or $manifest.closingFlattenCount -ne 7) { Fail-Gate "EXEC_SIM_R028_FAIL_INVERSION_OR_SESSION_METADATA_MISSING" "Opening/closing counts invalid." }
foreach ($property in @("allProviderNamesValid","allDatasetTypesValid","allFileFormatsValid","allSessionCategoriesValid","allTimeRangesValid","allHashesPresent","allComputedHashesMatchManifest","allRowCountsDeclared","allContainsSecretsFalse","allContainsRawProviderPayloadFalse")) {
    Require-True ([bool]$manifest.$property) "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING" "Manifest aggregate flag is false: $property"
}
Require-False ([bool]$manifest.rowLevelValidationExecuted) "EXEC_SIM_R028_FAIL_ROW_LEVEL_VALIDATION_EXECUTED" "Row-level validation executed."

if ($file.resultCount -ne 14 -or @($file.results).Count -ne 14) { Fail-Gate "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING" "File-level validation results missing." }
if ($accepted.acceptedCount -ne 14 -or @($accepted.acceptedOutputs).Count -ne 14) { Fail-Gate "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING" "Accepted outputs missing." }
if ($quarantined.quarantinedCount -ne 0) { Fail-Gate "EXEC_SIM_R028_PARTIAL_MANIFEST_VALIDATION_WITH_QUARANTINE_NO_EXTERNAL" "Unexpected quarantined outputs." }
if ($diagnostics.missingManifestCount -ne 0 -or $diagnostics.missingQuoteFileCount -ne 0 -or $diagnostics.unreadableManifestCount -ne 0 -or $diagnostics.incompleteCriticalFieldCount -ne 0) {
    Fail-Gate "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING" "Missing or incomplete manifest diagnostics detected."
}

$expectedSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")
foreach ($category in @("OpeningBuild", "ClosingFlatten")) {
    foreach ($symbol in $expectedSymbols) {
        $matches = @($file.results) | Where-Object { $_.Symbol -eq $symbol -and $_.SessionWindowCategory -eq $category }
        if (@($matches).Count -ne 1) { Fail-Gate "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING" "Missing file-level result for $symbol $category." }
        $entry = $matches | Select-Object -First 1
        Require-True ([bool]$entry.QuoteFileExists) "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING" "Quote file missing for $symbol $category."
        Require-True ([bool]$entry.ManifestExists) "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING" "Manifest missing for $symbol $category."
        Require-True ([bool]$entry.ManifestReadable) "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING" "Manifest unreadable for $symbol $category."
        if ($entry.ProviderName -ne "PolygonOfflineFile" -or $entry.ProviderDatasetType -ne "HistoricalBboQuotes" -or $entry.FileFormat -ne "NDJSON") { Fail-Gate "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING" "Provider/dataset/format mismatch for $symbol $category." }
        if ($category -eq "OpeningBuild" -and ($entry.TimeRangeStartUtc -ne "2026-05-19T08:00:00Z" -or $entry.TimeRangeEndUtc -ne "2026-05-19T12:00:00Z")) { Fail-Gate "EXEC_SIM_R028_FAIL_INVERSION_OR_SESSION_METADATA_MISSING" "OpeningBuild time range mismatch for $symbol." }
        if ($category -eq "ClosingFlatten" -and ($entry.TimeRangeStartUtc -ne "2026-05-19T16:00:00Z" -or $entry.TimeRangeEndUtc -ne "2026-05-19T20:00:00Z")) { Fail-Gate "EXEC_SIM_R028_FAIL_INVERSION_OR_SESSION_METADATA_MISSING" "ClosingFlatten time range mismatch for $symbol." }
        Require-True ([bool]$entry.FileHashPresent) "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING" "File hash missing for $symbol $category."
        Require-True ([bool]$entry.FileHashMatches) "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING" "File hash mismatch for $symbol $category."
        if ([int64]$entry.RowCountDeclared -le 0) { Fail-Gate "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING" "Row count declaration missing for $symbol $category." }
        Require-False ([bool]$entry.ContainsSecrets) "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING" "Secret flag true for $symbol $category."
        Require-False ([bool]$entry.ContainsRawProviderPayload) "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING" "Raw payload flag true for $symbol $category."
        if ($entry.ValidationStatus -ne "ManifestValidationAcceptedWithWarnings" -and $entry.ValidationStatus -ne "ManifestValidationAccepted") { Fail-Gate "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING" "Unexpected validation status for $symbol $category." }
        Require-False ([bool]$entry.QuoteRowsValidated) "EXEC_SIM_R028_FAIL_ROW_LEVEL_VALIDATION_EXECUTED" "Quote rows validated for $symbol $category."
        Require-False ([bool]$entry.QuoteWindowsCreated) "EXEC_SIM_R028_FAIL_QUOTE_WINDOW_OR_FEED_OUTPUT_CREATED" "Quote windows created for $symbol $category."
        Require-False ([bool]$entry.CloseBenchmarksCreated) "EXEC_SIM_R028_FAIL_QUOTE_WINDOW_OR_FEED_OUTPUT_CREATED" "Close benchmarks created for $symbol $category."
        Require-False ([bool]$entry.FeedQualityResultsCreated) "EXEC_SIM_R028_FAIL_QUOTE_WINDOW_OR_FEED_OUTPUT_CREATED" "Feed quality created for $symbol $category."
    }
}

Require-True ([bool]$opening.openingBuildManifestValidationCreated) "EXEC_SIM_R028_FAIL_INVERSION_OR_SESSION_METADATA_MISSING" "OpeningBuild validation missing."
Require-True ([bool]$opening.AllSevenSymbolsPresent) "EXEC_SIM_R028_FAIL_INVERSION_OR_SESSION_METADATA_MISSING" "OpeningBuild missing symbols."
if ($opening.ResultCount -ne 7 -or $opening.AcceptedCount -ne 7) { Fail-Gate "EXEC_SIM_R028_FAIL_INVERSION_OR_SESSION_METADATA_MISSING" "OpeningBuild counts invalid." }
Require-True ([bool]$closing.closingFlattenManifestValidationCreated) "EXEC_SIM_R028_FAIL_INVERSION_OR_SESSION_METADATA_MISSING" "ClosingFlatten validation missing."
Require-True ([bool]$closing.AllSevenSymbolsPresent) "EXEC_SIM_R028_FAIL_INVERSION_OR_SESSION_METADATA_MISSING" "ClosingFlatten missing symbols."
if ($closing.ResultCount -ne 7 -or $closing.AcceptedCount -ne 7) { Fail-Gate "EXEC_SIM_R028_FAIL_INVERSION_OR_SESSION_METADATA_MISSING" "ClosingFlatten counts invalid." }

Require-True ([bool]$inversion.symbolInversionValidationCreated) "EXEC_SIM_R028_FAIL_INVERSION_OR_SESSION_METADATA_MISSING" "Inversion validation missing."
Require-True ([bool]$inversion.allSymbolsPresentInOpeningBuild) "EXEC_SIM_R028_FAIL_INVERSION_OR_SESSION_METADATA_MISSING" "OpeningBuild symbol coverage missing."
Require-True ([bool]$inversion.allSymbolsPresentInClosingFlatten) "EXEC_SIM_R028_FAIL_INVERSION_OR_SESSION_METADATA_MISSING" "ClosingFlatten symbol coverage missing."
Require-True ([bool]$inversion.usdJpyCaveatPreserved) "EXEC_SIM_R028_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
Require-True ([bool]$inversion.usdCadInversionPreserved) "EXEC_SIM_R028_FAIL_INVERSION_OR_SESSION_METADATA_MISSING" "USDCAD inversion missing."
Require-True ([bool]$inversion.usdChfInversionPreserved) "EXEC_SIM_R028_FAIL_INVERSION_OR_SESSION_METADATA_MISSING" "USDCHF inversion missing."
Require-True ([bool]$inversion.nonInvertedSymbolsPreserved) "EXEC_SIM_R028_FAIL_INVERSION_OR_SESSION_METADATA_MISSING" "Non-inverted mappings missing."
Require-False ([bool]$inversion.audusdMisclassifiedFailed) "EXEC_SIM_R028_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD misclassified."

Require-True ([bool]$session.sessionCategoryValidationCreated) "EXEC_SIM_R028_FAIL_INVERSION_OR_SESSION_METADATA_MISSING" "Session validation missing."
Require-True ([bool]$session.OpeningBuildEntriesValid) "EXEC_SIM_R028_FAIL_INVERSION_OR_SESSION_METADATA_MISSING" "OpeningBuild session entries invalid."
Require-True ([bool]$session.ClosingFlattenEntriesValid) "EXEC_SIM_R028_FAIL_INVERSION_OR_SESSION_METADATA_MISSING" "ClosingFlatten session entries invalid."
Require-True ([bool]$session.allSessionCategoriesValid) "EXEC_SIM_R028_FAIL_INVERSION_OR_SESSION_METADATA_MISSING" "Session category invalid."
Require-False ([bool]$session.rowLevelValidationExecuted) "EXEC_SIM_R028_FAIL_ROW_LEVEL_VALIDATION_EXECUTED" "Session validation used row-level validation."
Require-True ([bool]$timeRange.allManifestTimeRangesMatch) "EXEC_SIM_R028_FAIL_INVERSION_OR_SESSION_METADATA_MISSING" "Time ranges do not match."
Require-True ([bool]$secret.allContainsSecretsFalse) "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING" "Secret risk detected."
Require-True ([bool]$secret.allContainsRawProviderPayloadFalse) "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING" "Raw payload risk detected."
Require-False ([bool]$secret.secretRiskDetected) "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING" "Secret risk detected."
Require-False ([bool]$secret.rawPayloadRiskDetected) "EXEC_SIM_R028_FAIL_MANIFEST_VALIDATION_MISSING" "Raw payload risk detected."

Require-True ([bool]$direct.directCrossExclusionPreserved) "EXEC_SIM_R028_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct-cross exclusion missing."
Require-False ([bool]$direct.directCrossesInExecutionBatch) "EXEC_SIM_R028_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct crosses in batch."
Require-False ([bool]$direct.directCrossExecutionAllowedByDefault) "EXEC_SIM_R028_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct-cross execution allowed."
Require-False ([bool]$direct.guidanceWeakened) "EXEC_SIM_R028_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct-cross guidance weakened."
if ($cost.bestCaseMajorTargetUsdPerMillion -ne 5) { Fail-Gate "EXEC_SIM_R028_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million guidance missing." }
Require-True ([bool]$cost.fiveUsdPerMillionBestCaseMajorOnly) "EXEC_SIM_R028_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million not best-case major-only."
Require-False ([bool]$cost.fiveUsdPerMillionUniversalized) "EXEC_SIM_R028_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million universalized."
Require-True ([bool]$nonmajor.RequiresLiquidityCalibration) "EXEC_SIM_R028_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "Nonmajor calibration missing."
Require-False ([bool]$nonmajor.calibrationRequirementWeakened) "EXEC_SIM_R028_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "Nonmajor calibration weakened."

Require-False ([bool]$row.quoteRowsReadForValidation) "EXEC_SIM_R028_FAIL_ROW_LEVEL_VALIDATION_EXECUTED" "Quote rows read for validation."
Require-False ([bool]$row.quoteRowsParsed) "EXEC_SIM_R028_FAIL_ROW_LEVEL_VALIDATION_EXECUTED" "Quote rows parsed."
Require-False ([bool]$row.quoteRowsValidated) "EXEC_SIM_R028_FAIL_ROW_LEVEL_VALIDATION_EXECUTED" "Quote rows validated."
Require-False ([bool]$sanitized.sanitizedQuoteRowsCreated) "EXEC_SIM_R028_FAIL_SANITIZED_QUOTE_ROWS_CREATED" "Sanitized quote rows created."
Require-False ([bool]$sanitized.persistedSanitizedQuoteRowsCreated) "EXEC_SIM_R028_FAIL_SANITIZED_QUOTE_ROWS_CREATED" "Persisted sanitized quote rows created."
Require-False ([bool]$sanitized.quotesImported) "EXEC_SIM_R028_FAIL_SANITIZED_QUOTE_ROWS_CREATED" "Quotes imported."
Require-False ([bool]$windows.quoteWindowsCreated) "EXEC_SIM_R028_FAIL_QUOTE_WINDOW_OR_FEED_OUTPUT_CREATED" "Quote windows created."
Require-False ([bool]$windows.closeBenchmarksCreated) "EXEC_SIM_R028_FAIL_QUOTE_WINDOW_OR_FEED_OUTPUT_CREATED" "Close benchmarks created."
Require-False ([bool]$windows.feedQualityResultsCreated) "EXEC_SIM_R028_FAIL_QUOTE_WINDOW_OR_FEED_OUTPUT_CREATED" "Feed quality results created."
Require-False ([bool]$backtest.newBacktestExecuted) "EXEC_SIM_R028_FAIL_BACKTEST_OR_SIMULATION_EXECUTED" "Backtest executed."
Require-False ([bool]$backtest.newSimulationExecuted) "EXEC_SIM_R028_FAIL_BACKTEST_OR_SIMULATION_EXECUTED" "Simulation executed."
Require-False ([bool]$tca.tcaResultLinesProduced) "EXEC_SIM_R028_FAIL_TCA_RESULTS_PRODUCED" "TCA result lines produced."
Require-False ([bool]$tca.simulationResultLinesProduced) "EXEC_SIM_R028_FAIL_TCA_RESULTS_PRODUCED" "Simulation result lines produced."
Require-False ([bool]$tca.tcaReportsProduced) "EXEC_SIM_R028_FAIL_TCA_RESULTS_PRODUCED" "TCA reports produced."
Require-False ([bool]$api.polygonApiCalled) "EXEC_SIM_R028_FAIL_API_CALL_DETECTED" "Polygon API called."
Require-False ([bool]$api.lmaxCalled) "EXEC_SIM_R028_FAIL_API_CALL_DETECTED" "LMAX called."
Require-False ([bool]$api.externalApiCalled) "EXEC_SIM_R028_FAIL_API_CALL_DETECTED" "External API called."
Require-False ([bool]$runtime.brokerActivationDetected) "EXEC_SIM_R028_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "Broker activation detected."
Require-False ([bool]$runtime.socketOpened) "EXEC_SIM_R028_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "Socket opened."
Require-False ([bool]$runtime.tlsOpened) "EXEC_SIM_R028_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "TLS opened."
Require-False ([bool]$runtime.fixOpened) "EXEC_SIM_R028_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "FIX opened."
Require-False ([bool]$runtime.marketDataRequestSent) "EXEC_SIM_R028_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "MarketDataRequest sent."
Require-False ([bool]$runtime.marketDataResponseRead) "EXEC_SIM_R028_FAIL_BROKER_OR_MARKETDATA_RUNTIME_DETECTED" "MarketDataResponse read."
Require-False ([bool]$runtime.schedulerServiceTimerPollingBackgroundJobIntroduced) "EXEC_SIM_R028_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler/service introduced."
Require-False ([bool]$runtime.automaticExecutionIntroduced) "EXEC_SIM_R028_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Automatic execution introduced."
Require-False ([bool]$order.ordersCreated) "EXEC_SIM_R028_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders created."
Require-False ([bool]$order.executableOrdersCreated) "EXEC_SIM_R028_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Executable orders created."
Require-False ([bool]$order.childOrdersCreated) "EXEC_SIM_R028_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Child orders created."
Require-False ([bool]$order.fillEntitiesCreated) "EXEC_SIM_R028_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fills created."
Require-False ([bool]$order.executionReportEntitiesCreated) "EXEC_SIM_R028_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Execution reports created."
Require-False ([bool]$order.routesCreated) "EXEC_SIM_R028_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Routes created."
Require-False ([bool]$order.submissionsCreated) "EXEC_SIM_R028_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Submissions created."

Require-True ([bool]$usdjpy.usdjpyCaveatPreserved) "EXEC_SIM_R028_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
if ($usdjpy.PortfolioNormalizedSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or -not [bool]$usdjpy.RequiresInversion -or $usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8") {
    Fail-Gate "EXEC_SIM_R028_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat weakened."
}
Require-False ([bool]$usdjpy.audusdMisclassifiedFailed) "EXEC_SIM_R028_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD misclassified."
Require-True ([bool]$lmax.referenceOnly) "EXEC_SIM_R028_FAIL_API_CALL_DETECTED" "LMAX reference weakened."
Require-False ([bool]$lmax.lmaxCalledInR028) "EXEC_SIM_R028_FAIL_API_CALL_DETECTED" "LMAX called."
if ($lmax.audusdStatus -notmatch "not failed") { Fail-Gate "EXEC_SIM_R028_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD incorrectly marked failed." }

foreach ($property in @("polygonApiCalled","lmaxCalled","externalApiCalled","filesDownloaded","brokerRuntimeDetected","quoteRowsValidated","quoteFilesImported","sanitizedQuoteRowsCreated","quoteWindowsCreated","closeBenchmarksCreated","feedQualityResultsCreated","newBacktestExecuted","newSimulationExecuted","tcaResultLinesProduced","ordersFillsReportsRoutesSubmissionsCreated","livePaperBrokerProductionTradingStateMutated","paperLedgerStateCommitted")) {
    Require-False ([bool]$noExternal.$property) "EXEC_SIM_R028_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected $property."
}
Require-False ([bool]$forbidden.forbiddenActionsDetected) "EXEC_SIM_R028_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected."

if ($evidence.dotnetBuildNoRestore -notlike "PASS*" -or $evidence.focusedR028Tests -notlike "PASS*" -or $evidence.unitTestsIfFeasible -notlike "PASS*" -or $evidence.validator -notlike "PASS*") {
    Fail-Gate "EXEC_SIM_R028_FAIL_BUILD_OR_TESTS" "Build/test/validator evidence is missing or not passing."
}

Write-Host "EXEC_SIM_R028_PASS_HISTORICAL_WINDOW_MANIFEST_VALIDATION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R028_PASS_OPENING_CLOSING_FILE_LEVEL_PREFLIGHT_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R028_PASS_SYMBOL_INVERSION_SESSION_METADATA_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R028_PASS_NO_ROW_VALIDATION_NO_BACKTEST_GATE_READY_NO_EXTERNAL"
