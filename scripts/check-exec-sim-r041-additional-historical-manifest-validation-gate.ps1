$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $root "artifacts/readiness/execution-sim"

function Read-JsonArtifact {
    param([Parameter(Mandatory=$true)][string]$Name)
    $path = Join-Path $artifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing required artifact: $Name" }
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

function Assert-False { param([bool]$Value, [string]$Message) if ($Value) { throw $Message } }
function Assert-True { param([bool]$Value, [string]$Message) if (-not $Value) { throw $Message } }

$requiredFiles = @(
    "phase-exec-sim-r041-summary.md",
    "phase-exec-sim-r041-r040-authorized-files-reference.json",
    "phase-exec-sim-r041-manifest-validation-contract.json",
    "phase-exec-sim-r041-authorized-files-used.json",
    "phase-exec-sim-r041-manifest-validation-results.json",
    "phase-exec-sim-r041-file-level-validation-results.json",
    "phase-exec-sim-r041-accepted-manifest-validation-outputs.json",
    "phase-exec-sim-r041-quarantined-manifest-validation-outputs.json",
    "phase-exec-sim-r041-missing-incomplete-manifest-diagnostics.json",
    "phase-exec-sim-r041-utc-window-coverage-validation.json",
    "phase-exec-sim-r041-symbol-coverage-validation.json",
    "phase-exec-sim-r041-symbol-provider-mapping-validation.json",
    "phase-exec-sim-r041-inversion-validation.json",
    "phase-exec-sim-r041-canonical-session-validation.json",
    "phase-exec-sim-r041-legacy-compatibility-preservation.json",
    "phase-exec-sim-r041-time-range-validation.json",
    "phase-exec-sim-r041-secret-raw-payload-validation.json",
    "phase-exec-sim-r041-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r041-cost-guidance-preservation.json",
    "phase-exec-sim-r041-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r041-no-row-level-validation-audit.json",
    "phase-exec-sim-r041-no-sanitized-quote-row-creation-audit.json",
    "phase-exec-sim-r041-no-quote-window-close-benchmark-feed-quality-audit.json",
    "phase-exec-sim-r041-no-backtest-simulation-audit.json",
    "phase-exec-sim-r041-no-tca-result-lines-audit.json",
    "phase-exec-sim-r041-no-polygon-api-call-audit.json",
    "phase-exec-sim-r041-no-lmax-call-audit.json",
    "phase-exec-sim-r041-no-external-api-call-audit.json",
    "phase-exec-sim-r041-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r041-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r041-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r041-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r041-no-external-audit.json",
    "phase-exec-sim-r041-forbidden-actions-audit.json",
    "phase-exec-sim-r041-next-phase-recommendation.json",
    "phase-exec-sim-r041-build-test-validator-evidence.json"
)

foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $file))) { throw "Missing required R041 artifact: $file" }
}

$ref = Read-JsonArtifact "phase-exec-sim-r041-r040-authorized-files-reference.json"
if ($ref.sourcePhase -ne "EXEC-SIM-R040") { throw "R040 reference missing." }
if ($ref.authorizedEntryCount -ne 35) { throw "R040 authorized file count mismatch." }
Assert-True $ref.acceptedForManifestValidation "R040 acceptance for manifest validation missing."
Assert-True $ref.r041NoExternal "R041 no-external reference missing."

$contract = Read-JsonArtifact "phase-exec-sim-r041-manifest-validation-contract.json"
if ($contract.validationScope -ne "ManifestAndFileLevelOnly") { throw "Manifest validation scope missing." }
Assert-True $contract.manifestJsonReadAllowed "Manifest JSON read should be allowed."
Assert-True $contract.quoteFileHashComputationAllowed "Quote file hash computation should be allowed."
Assert-False $contract.quoteRowContentReadAllowed "Quote row content read allowed."
Assert-False $contract.rowLevelValidationAllowed "Row-level validation allowed."
Assert-False $contract.quoteImportAllowed "Quote import allowed."
Assert-False $contract.sanitizedQuoteRowCreationAllowed "Sanitized row creation allowed."
Assert-False $contract.quoteWindowCreationAllowed "Quote window creation allowed."
Assert-False $contract.closeBenchmarkCreationAllowed "Close benchmark creation allowed."
Assert-False $contract.feedQualityCreationAllowed "Feed quality creation allowed."
Assert-False $contract.backtestSimulationAllowed "Backtest/simulation allowed."
Assert-False $contract.tcaResultLineCreationAllowed "TCA result line creation allowed."
if ($contract.expectedEntryCount -ne 35) { throw "Contract expected entry count mismatch." }

$used = Read-JsonArtifact "phase-exec-sim-r041-authorized-files-used.json"
if ($used.authorizedEntryCount -ne 35 -or @($used.entries).Count -ne 35) { throw "Authorized files used missing 35 entries." }

$results = Read-JsonArtifact "phase-exec-sim-r041-manifest-validation-results.json"
if ($results.expectedEntryCount -ne 35 -or $results.acceptedCount -ne 35 -or $results.quarantinedCount -ne 0) { throw "Manifest validation result count mismatch." }
if (@($results.results).Count -ne 35) { throw "Manifest validation does not represent 35 entries." }
foreach ($entry in @($results.results)) {
    Assert-True $entry.QuoteFileExists "Quote file missing in validation result."
    Assert-True $entry.ManifestExists "Manifest missing in validation result."
    Assert-True $entry.ManifestReadable "Manifest unreadable in validation result."
    if ($entry.ProviderName -ne "PolygonOfflineFile") { throw "Provider mismatch." }
    if ($entry.ProviderDatasetType -ne "HistoricalBboQuotes") { throw "Dataset mismatch." }
    if ($entry.FileFormat -ne "NDJSON") { throw "Format mismatch." }
    Assert-True $entry.FileHashPresent "File hash missing."
    Assert-True $entry.FileHashMatches "File hash mismatch."
    if ($null -eq $entry.RowCountDeclared) { throw "RowCountDeclared missing." }
    Assert-False $entry.ContainsSecrets "Secret flag detected."
    Assert-False $entry.ContainsRawProviderPayload "Raw provider payload flag detected."
    if ($entry.ValidationStatus -notin @("ManifestValidationAccepted", "ManifestValidationAcceptedWithWarnings")) { throw "Unexpected quarantine status: $($entry.ValidationStatus)" }
    Assert-True $entry.LegacyCompatibilityOnly "Legacy compatibility-only missing."
    if (@($entry.CanonicalCloseMinuteSet) -join "," -ne "00,15,30,45") { throw "Canonical close minutes missing." }
}

$fileLevel = Read-JsonArtifact "phase-exec-sim-r041-file-level-validation-results.json"
Assert-True $fileLevel.fileLevelOnly "File-level-only flag missing."
Assert-False $fileLevel.quoteRowContentRead "Quote row content read."
if (@($fileLevel.results).Count -ne 35) { throw "File-level results missing 35 entries." }

$accepted = Read-JsonArtifact "phase-exec-sim-r041-accepted-manifest-validation-outputs.json"
if ($accepted.acceptedCount -ne 35 -or @($accepted.accepted).Count -ne 35) { throw "Accepted manifest outputs missing 35 entries." }

$quarantine = Read-JsonArtifact "phase-exec-sim-r041-quarantined-manifest-validation-outputs.json"
if ($quarantine.quarantinedCount -ne 0 -or @($quarantine.quarantined).Count -ne 0) { throw "Unexpected quarantined manifests." }

$missing = Read-JsonArtifact "phase-exec-sim-r041-missing-incomplete-manifest-diagnostics.json"
if ($missing.missingQuoteFileCount -ne 0 -or $missing.missingManifestCount -ne 0 -or $missing.unreadableManifestCount -ne 0 -or $missing.rowCountDeclaredMissingCount -ne 0 -or $missing.fileHashMissingCount -ne 0) { throw "Missing/incomplete manifest diagnostics not clean." }

$utc = Read-JsonArtifact "phase-exec-sim-r041-utc-window-coverage-validation.json"
if ($utc.utcWindowCount -ne 5 -or @($utc.windows).Count -ne 5) { throw "UTC window coverage missing." }
Assert-True $utc.allWindowsRepresented "Not all UTC windows represented."
foreach ($window in @($utc.windows)) {
    Assert-True $window.allSevenSymbolsPresent "UTC window missing symbols."
    if ($window.symbolCount -ne 7 -or $window.acceptedCount -ne 7) { throw "UTC window count mismatch." }
}

$symbolCoverage = Read-JsonArtifact "phase-exec-sim-r041-symbol-coverage-validation.json"
if ($symbolCoverage.symbolCount -ne 7 -or @($symbolCoverage.symbols).Count -ne 7) { throw "Symbol coverage missing." }
Assert-True $symbolCoverage.allSymbolsRepresentedForEveryUtcWindow "Not all symbols represented for every UTC window."
foreach ($symbol in @($symbolCoverage.symbols)) {
    Assert-True $symbol.allFiveWindowsPresent "Symbol missing five windows."
    if ($symbol.windowCount -ne 5 -or $symbol.acceptedCount -ne 5) { throw "Symbol coverage count mismatch." }
}

$mapping = Read-JsonArtifact "phase-exec-sim-r041-symbol-provider-mapping-validation.json"
Assert-True $mapping.allMappingsValid "Symbol/provider mappings invalid."

$inversion = Read-JsonArtifact "phase-exec-sim-r041-inversion-validation.json"
Assert-True $inversion.inversionValid "Inversion invalid."
Assert-False $inversion.audusdMisclassifiedAsFailed "AUDUSD misclassified as failed."
if ($inversion.usdjpy.normalizedPortfolioSymbol -ne "JPYUSD" -or $inversion.usdjpy.executionTradableSymbol -ne "USDJPY" -or -not $inversion.usdjpy.requiresInversion -or $inversion.usdjpy.securityId -ne "4004" -or $inversion.usdjpy.securityIdSource -ne "8") { throw "USDJPY caveat weakened." }

$session = Read-JsonArtifact "phase-exec-sim-r041-canonical-session-validation.json"
if ($session.canonicalSessionLocal -ne "14:15-21:00 America/New_York") { throw "Canonical session missing." }
if (@($session.canonicalCloseMinuteSet) -join "," -ne "00,15,30,45") { throw "Canonical close minute set missing." }
Assert-True $session.futureCanonicalTimestampsUseQuarterHourCloses "Future canonical quarter-hour closes missing."
Assert-True $session.fullCanonicalSessionWindow "Full canonical session missing."
Assert-True $session.valid "Canonical session validation not valid."

$legacy = Read-JsonArtifact "phase-exec-sim-r041-legacy-compatibility-preservation.json"
Assert-True $legacy.legacyOffsetSixLabelsCompatibilityOnly "Legacy offset labels must be compatibility-only."
Assert-True $legacy.futureCanonicalTimestampsUseQuarterHourCloses "Future canonical quarter-hour closes missing."
Assert-False $legacy.legacy06UsedAsFutureCanonical "Legacy :06 used as future canonical."
if ($legacy.canonicalCloseForLegacyMatchRule -ne "LegacyOutputTimestamp - 6 minutes") { throw "Legacy match alignment rule missing." }
if ($legacy.legacyNextBarExecutionCloseCanonicalRule -ne "LegacyOutputTimestamp + 9 minutes") { throw "Legacy next-bar canonical rule missing." }

$timeRange = Read-JsonArtifact "phase-exec-sim-r041-time-range-validation.json"
Assert-True $timeRange.allTimeRangesMatch "Time ranges do not all match."
if ($timeRange.windowCount -ne 5) { throw "Time range window count mismatch." }

$secret = Read-JsonArtifact "phase-exec-sim-r041-secret-raw-payload-validation.json"
if ($secret.containsSecretsCount -ne 0 -or $secret.containsRawProviderPayloadCount -ne 0) { throw "Secret/raw payload risk detected." }
Assert-True $secret.allClear "Secret/raw payload validation not clear."

$direct = Read-JsonArtifact "phase-exec-sim-r041-direct-cross-exclusion-preservation.json"
Assert-True $direct.directCrossesSignalOnly "Direct-cross signal-only missing."
Assert-True $direct.nettingFirst "Netting-first missing."
Assert-True $direct.executionUniverseUsdPairOnly "USD-pair-only execution missing."
Assert-True $direct.directCrossExecutionDisabled "Direct-cross execution disabled missing."
Assert-False $direct.directCrossesIncluded "Direct crosses included."
Assert-False $direct.weakened "Direct-cross exclusion weakened."

$cost = Read-JsonArtifact "phase-exec-sim-r041-cost-guidance-preservation.json"
Assert-True $cost.fiveUsdPerMillionBestCaseMajorOnly "5 USD/million best-case major-only missing."
Assert-False $cost.fiveUsdPerMillionUniversalized "5 USD/million universalized."

$nonmajor = Read-JsonArtifact "phase-exec-sim-r041-nonmajor-calibration-preservation.json"
Assert-True $nonmajor.nonmajorEmScandiCnhCalibrationRequired "Nonmajor calibration missing."
Assert-False $nonmajor.calibrationRequirementWeakened "Nonmajor calibration weakened."

$rowAudit = Read-JsonArtifact "phase-exec-sim-r041-no-row-level-validation-audit.json"
Assert-False $rowAudit.quoteRowsRead "Quote rows read."
Assert-False $rowAudit.quoteRowsValidated "Quote rows validated."
Assert-False $rowAudit.rowCountsComputedFromQuoteRows "Row counts computed from quote rows."
Assert-True $rowAudit.hashesComputedAsFileBytesOnly "Hash computation should be file-byte only."

$sanitized = Read-JsonArtifact "phase-exec-sim-r041-no-sanitized-quote-row-creation-audit.json"
Assert-False $sanitized.sanitizedQuoteRowsCreated "Sanitized quote rows created."
Assert-False $sanitized.persistedSanitizedRowsCreated "Persisted sanitized rows created."
Assert-False $sanitized.quoteRowsImportedIntoDb "Quote rows imported."

$readinessAudit = Read-JsonArtifact "phase-exec-sim-r041-no-quote-window-close-benchmark-feed-quality-audit.json"
Assert-False $readinessAudit.quoteWindowsCreated "Quote windows created."
Assert-False $readinessAudit.closeBenchmarksCreated "Close benchmarks created."
Assert-False $readinessAudit.feedQualityResultsCreated "Feed quality results created."

$backtest = Read-JsonArtifact "phase-exec-sim-r041-no-backtest-simulation-audit.json"
Assert-False $backtest.backtestExecuted "Backtest executed."
Assert-False $backtest.simulationExecuted "Simulation executed."

$tca = Read-JsonArtifact "phase-exec-sim-r041-no-tca-result-lines-audit.json"
Assert-False $tca.tcaResultLinesProduced "TCA result lines produced."
Assert-False $tca.newTcaResultLinesCreated "New TCA result lines created."

$polygon = Read-JsonArtifact "phase-exec-sim-r041-no-polygon-api-call-audit.json"
Assert-False $polygon.polygonApiCalled "Polygon API called."
Assert-False $polygon.downloadExecuted "Download executed."
Assert-False $polygon.apiAccessTested "API access tested."

$lmaxAudit = Read-JsonArtifact "phase-exec-sim-r041-no-lmax-call-audit.json"
Assert-False $lmaxAudit.lmaxCalled "LMAX called."
Assert-False $lmaxAudit.marketDataRequestSent "MarketDataRequest sent."
Assert-False $lmaxAudit.marketDataResponseRead "MarketDataResponse read."
Assert-False $lmaxAudit.brokerActivated "Broker activated."

$external = Read-JsonArtifact "phase-exec-sim-r041-no-external-api-call-audit.json"
Assert-False $external.externalApiCalled "External API called."
Assert-False $external.polygonApiCalled "Polygon API called."
Assert-False $external.lmaxCalled "LMAX called."
Assert-False $external.apiAccessTested "API access tested."

$runtime = Read-JsonArtifact "phase-exec-sim-r041-no-broker-marketdata-runtime-audit.json"
Assert-False $runtime.brokerActivationPerformed "Broker activation performed."
Assert-False $runtime.socketOpened "Socket opened."
Assert-False $runtime.tlsOpened "TLS opened."
Assert-False $runtime.fixOpened "FIX opened."
Assert-False $runtime.marketDataRuntimeStarted "MarketData runtime started."
Assert-False $runtime.marketDataRequestSent "MarketDataRequest sent."
Assert-False $runtime.marketDataResponseRead "MarketDataResponse read."
Assert-False $runtime.apiWorkerSchedulerServiceStarted "API/Worker/Scheduler/Service started."
Assert-False $runtime.timerPollingBackgroundJobIntroduced "Timer/polling/background job introduced."

$orders = Read-JsonArtifact "phase-exec-sim-r041-no-order-fill-report-route-audit.json"
Assert-False $orders.executableSchedulesCreated "Executable schedules created."
Assert-False $orders.childSlicesCreated "Child slices created."
Assert-False $orders.childOrdersCreated "Child orders created."
Assert-False $orders.ordersCreated "Orders created."
Assert-False $orders.ordersSubmitted "Orders submitted."
Assert-False $orders.fillsCreated "Fills created."
Assert-False $orders.executionReportsCreated "Execution reports created."
Assert-False $orders.routesCreated "Routes created."
Assert-False $orders.submissionsCreated "Submissions created."
Assert-False $orders.stateMutated "State mutated."

$usdjpy = Read-JsonArtifact "phase-exec-sim-r041-usdjpy-caveat-preservation.json"
if ($usdjpy.normalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.executionTradableSymbol -ne "USDJPY" -or -not $usdjpy.requiresInversion -or $usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8") { throw "USDJPY caveat weakened." }
Assert-False $usdjpy.caveatWeakened "USDJPY caveat weakened."

$lmax = Read-JsonArtifact "phase-exec-sim-r041-lmax-readonly-baseline-reference.json"
Assert-True $lmax.lmaxReferenceOnly "LMAX reference-only missing."
Assert-False $lmax.lmaxCalledInR041 "LMAX called in R041."
Assert-False $lmax.audusdMisclassifiedAsFailed "AUDUSD misclassified as failed."

$noExternal = Read-JsonArtifact "phase-exec-sim-r041-no-external-audit.json"
Assert-True $noExternal.noExternal "No-external missing."
Assert-False $noExternal.polygonApiCalled "Polygon API called."
Assert-False $noExternal.lmaxCalled "LMAX called."
Assert-False $noExternal.externalApiCalled "External API called."
Assert-False $noExternal.filesDownloaded "Files downloaded."

$forbidden = Read-JsonArtifact "phase-exec-sim-r041-forbidden-actions-audit.json"
Assert-False $forbidden.forbiddenActionsDetected "Forbidden actions detected."
Assert-False $forbidden.rowLevelValidationExecuted "Row-level validation executed."
Assert-False $forbidden.sanitizedQuoteRowsCreated "Sanitized quote rows created."
Assert-False $forbidden.quoteWindowOrFeedOutputCreated "Quote-window/feed output created."
Assert-False $forbidden.validationImportBacktestExecuted "Validation/import/backtest executed."
Assert-False $forbidden.tcaResultLinesProduced "TCA result lines produced."
Assert-False $forbidden.orderOrTradingPathIntroduced "Order/trading path introduced."
Assert-False $forbidden.schedulerServicePollingBackgroundJobIntroduced "Scheduler service introduced."
Assert-False $forbidden.stateMutated "State mutated."

$next = Read-JsonArtifact "phase-exec-sim-r041-next-phase-recommendation.json"
if ($next.manifestAcceptedFileCount -ne 35) { throw "Next phase handoff accepted count mismatch." }
Assert-True $next.r042ShouldValidateQuoteRows "R042 row validation recommendation missing."
Assert-True $next.r042ShouldProduceQuoteWindowCloseBenchmarkFeedQualityReadiness "R042 readiness recommendation missing."
Assert-True $next.r042ShouldNotImportBacktestOrCreateOrderDomainOutput "R042 safety recommendation missing."

$evidence = Read-JsonArtifact "phase-exec-sim-r041-build-test-validator-evidence.json"
if ($evidence.dotnetBuildNoRestore.status -notlike "PASS*") { throw "Build/test/validator evidence missing: dotnet build." }
if ($evidence.focusedR041Tests.status -notlike "PASS*") { throw "Build/test/validator evidence missing: focused R041 tests." }
if ($evidence.unitTests.status -notlike "PASS*") { throw "Build/test/validator evidence missing: unit tests." }

Write-Host "EXEC_SIM_R041_PASS_ADDITIONAL_HISTORICAL_MANIFEST_VALIDATION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R041_PASS_UTC_WINDOW_AND_SYMBOL_COVERAGE_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R041_PASS_CANONICAL_SESSION_FILE_LEVEL_PREFLIGHT_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R041_PASS_NO_ROW_VALIDATION_NO_BACKTEST_GATE_READY_NO_EXTERNAL"
