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
    "phase-exec-sim-r040-summary.md",
    "phase-exec-sim-r040-r039-download-plan-reference.json",
    "phase-exec-sim-r040-r038-session-date-reference.json",
    "phase-exec-sim-r040-additional-historical-file-authorization-contract.json",
    "phase-exec-sim-r040-additional-historical-file-authorization-request.json",
    "phase-exec-sim-r040-additional-historical-file-preflight-contract.json",
    "phase-exec-sim-r040-additional-historical-file-authorization-result.json",
    "phase-exec-sim-r040-expected-file-entry-list.json",
    "phase-exec-sim-r040-accepted-file-entries.json",
    "phase-exec-sim-r040-missing-file-diagnostics.json",
    "phase-exec-sim-r040-symbol-provider-mapping-preservation.json",
    "phase-exec-sim-r040-inversion-preservation.json",
    "phase-exec-sim-r040-canonical-session-preservation.json",
    "phase-exec-sim-r040-legacy-compatibility-preservation.json",
    "phase-exec-sim-r040-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r040-cost-guidance-preservation.json",
    "phase-exec-sim-r040-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r040-no-download-audit.json",
    "phase-exec-sim-r040-no-manifest-validation-audit.json",
    "phase-exec-sim-r040-no-row-validation-audit.json",
    "phase-exec-sim-r040-no-import-audit.json",
    "phase-exec-sim-r040-no-backtest-simulation-audit.json",
    "phase-exec-sim-r040-no-tca-result-lines-audit.json",
    "phase-exec-sim-r040-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r040-no-polygon-api-call-audit.json",
    "phase-exec-sim-r040-no-lmax-call-audit.json",
    "phase-exec-sim-r040-no-external-api-call-audit.json",
    "phase-exec-sim-r040-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r040-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r040-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r040-no-external-audit.json",
    "phase-exec-sim-r040-forbidden-actions-audit.json",
    "phase-exec-sim-r040-next-phase-recommendation.json",
    "phase-exec-sim-r040-build-test-validator-evidence.json"
)

foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $file))) { throw "Missing required R040 artifact: $file" }
}

$r039 = Read-JsonArtifact "phase-exec-sim-r040-r039-download-plan-reference.json"
if ($r039.sourcePhase -ne "EXEC-SIM-R039") { throw "R039 reference missing." }
if ($r039.expectedFutureQuoteFileCount -ne 35 -or $r039.expectedFutureManifestCount -ne 35) { throw "R039 expected file count reference missing." }
Assert-True $r039.operatorDownloadedFilesAfterR039 "Operator download-after-R039 reference missing."
Assert-False $r039.r040DownloadExecuted "R040 download executed."
Assert-True $r039.r040NoExternal "R040 no-external reference missing."

$r038 = Read-JsonArtifact "phase-exec-sim-r040-r038-session-date-reference.json"
if ($r038.sourcePhase -ne "EXEC-SIM-R038") { throw "R038 reference missing." }
if ($r038.confirmedTimezone -ne "America/New_York") { throw "R038 timezone reference missing." }
if ($r038.canonicalSessionLocal -ne "14:15-21:00") { throw "R038 canonical session reference missing." }
if (@($r038.canonicalCloseMinuteSet) -join "," -ne "00,15,30,45") { throw "Canonical close minute set missing." }
if (@($r038.utcWindows).Count -ne 5) { throw "R038 UTC windows missing." }
Assert-True $r038.legacyOffsetSixLabelsCompatibilityOnly "Legacy compatibility reference missing."
Assert-True $r038.futureCanonicalTimestampsUseQuarterHourCloses "Canonical quarter-hour reference missing."

$contract = Read-JsonArtifact "phase-exec-sim-r040-additional-historical-file-authorization-contract.json"
if ($contract.authorizationStatus -ne "AdditionalHistoricalFileAuthorizationReadyNoExternal") { throw "Authorization contract status missing." }
Assert-True $contract.authorizationOnly "Authorization-only missing."
Assert-True $contract.presenceCheckOnly "Presence-check-only missing."
Assert-False $contract.manifestContentRead "Manifest content read."
Assert-False $contract.quoteRowsRead "Quote rows read."
Assert-False $contract.manifestValidationExecuted "Manifest validation executed."
Assert-False $contract.rowValidationExecuted "Row validation executed."
if ($contract.providerName -ne "PolygonOfflineFile" -or $contract.datasetType -ne "HistoricalBboQuotes" -or $contract.fileFormat -ne "NDJSON") { throw "Provider/dataset/format missing." }
if ($contract.expectedEntryCount -ne 35 -or $contract.requiredQuoteFileCount -ne 35 -or $contract.requiredManifestCount -ne 35) { throw "Authorization contract count mismatch." }

$request = Read-JsonArtifact "phase-exec-sim-r040-additional-historical-file-authorization-request.json"
if (@($request.symbols).Count -ne 7 -or @($request.suffixes).Count -ne 5) { throw "Authorization request does not represent 35 entries." }
Assert-True $request.authorizationOnly "Request authorization-only missing."
Assert-True $request.noManifestValidation "Request no-manifest-validation missing."
Assert-True $request.noRowValidation "Request no-row-validation missing."
Assert-True $request.noImport "Request no-import missing."
Assert-True $request.noBacktest "Request no-backtest missing."

$preflight = Read-JsonArtifact "phase-exec-sim-r040-additional-historical-file-preflight-contract.json"
if ($preflight.preflightType -ne "PathPresenceOnly") { throw "Preflight must be path-presence-only." }
Assert-False $preflight.manifestContentReadAllowed "Manifest content read allowed."
Assert-False $preflight.quoteRowReadAllowed "Quote row read allowed."
Assert-False $preflight.hashComputationAllowed "Hash computation allowed."
Assert-False $preflight.rowCountComputationAllowed "Row count computation allowed."

$result = Read-JsonArtifact "phase-exec-sim-r040-additional-historical-file-authorization-result.json"
if ($result.expectedEntryCount -ne 35 -or $result.acceptedEntryCount -ne 35 -or $result.missingEntryCount -ne 0) { throw "Authorization result count mismatch." }
Assert-True $result.allQuotePathsPresent "Quote paths not all present."
Assert-True $result.allManifestPathsPresent "Manifest paths not all present."
Assert-True $result.authorizedForR041ManifestValidation "R041 authorization missing."
Assert-False $result.safeBlocked "Unexpected blocked result."
Assert-False $result.manifestValidationExecuted "Manifest validation executed."
Assert-False $result.rowValidationExecuted "Row validation executed."
Assert-True $result.noExternal "No-external result missing."

$expected = Read-JsonArtifact "phase-exec-sim-r040-expected-file-entry-list.json"
if ($expected.expectedEntryCount -ne 35) { throw "Expected file list count mismatch." }
if (@($expected.suffixes).Count -ne 5 -or @($expected.symbolTemplates).Count -ne 7) { throw "Expected file list does not represent 35 entries." }
foreach ($suffix in @($expected.suffixes)) {
    foreach ($symbol in @($expected.symbolTemplates)) {
        $quotePath = Join-Path $expected.incomingRoot "$($symbol.lowerSymbol)-$($suffix.suffix).ndjson"
        $manifestPath = Join-Path $expected.incomingRoot "$($symbol.lowerSymbol)-$($suffix.suffix).manifest.json"
        if (-not (Test-Path -LiteralPath $quotePath -PathType Leaf)) { throw "Missing expected quote path: $quotePath" }
        if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw "Missing expected manifest path: $manifestPath" }
    }
}

$accepted = Read-JsonArtifact "phase-exec-sim-r040-accepted-file-entries.json"
if ($accepted.acceptedEntryCount -ne 35 -or $accepted.acceptedQuoteFileCount -ne 35 -or $accepted.acceptedManifestCount -ne 35) { throw "Accepted entry count mismatch." }
if (@($accepted.acceptedGroups).Count -ne 5) { throw "Accepted groups missing." }
foreach ($group in @($accepted.acceptedGroups)) {
    if (@($group.acceptedLowerSymbols).Count -ne 7 -or $group.quoteFilesPresent -ne 7 -or $group.manifestFilesPresent -ne 7) { throw "Accepted group does not represent seven quote/manifests." }
}
Assert-True $accepted.authorizedForR041ManifestValidation "Accepted entries not authorized for R041."
Assert-False $accepted.manifestContentRead "Accepted artifact indicates manifest content read."
Assert-False $accepted.quoteRowsRead "Accepted artifact indicates quote rows read."

$missing = Read-JsonArtifact "phase-exec-sim-r040-missing-file-diagnostics.json"
if ($missing.missingEntryCount -ne 0 -or @($missing.missingQuoteFiles).Count -ne 0 -or @($missing.missingManifestFiles).Count -ne 0) { throw "Missing file diagnostics not empty." }
Assert-False $missing.blockedClassificationEmitted "Blocked classification emitted despite complete file set."

$mapping = Read-JsonArtifact "phase-exec-sim-r040-symbol-provider-mapping-preservation.json"
Assert-True $mapping.allSevenSymbolsRepresented "All seven symbols not represented."
Assert-False $mapping.directCrossesIncluded "Direct crosses included."
if (@($mapping.mappings).Count -ne 7) { throw "Symbol mappings count mismatch." }

$inversion = Read-JsonArtifact "phase-exec-sim-r040-inversion-preservation.json"
Assert-False $inversion.inversionWeakened "Inversion weakened."
Assert-False $inversion.audusdMisclassifiedAsFailed "AUDUSD misclassified as failed."
if (-not (@($inversion.invertedSymbols) | Where-Object { $_.executionTradableSymbol -eq "USDJPY" -and $_.normalizedPortfolioSymbol -eq "JPYUSD" -and $_.requiresInversion -and $_.securityId -eq "4004" -and $_.securityIdSource -eq "8" })) { throw "USDJPY inversion/caveat missing." }
if (-not (@($inversion.invertedSymbols) | Where-Object { $_.executionTradableSymbol -eq "USDCAD" -and $_.normalizedPortfolioSymbol -eq "CADUSD" -and $_.requiresInversion })) { throw "USDCAD inversion missing." }
if (-not (@($inversion.invertedSymbols) | Where-Object { $_.executionTradableSymbol -eq "USDCHF" -and $_.normalizedPortfolioSymbol -eq "CHFUSD" -and $_.requiresInversion })) { throw "USDCHF inversion missing." }

$session = Read-JsonArtifact "phase-exec-sim-r040-canonical-session-preservation.json"
if ($session.canonicalSessionLocal -ne "14:15-21:00 America/New_York") { throw "Canonical session preservation missing." }
if (@($session.canonicalCloseMinuteSet) -join "," -ne "00,15,30,45") { throw "Canonical close minute set missing." }
if ($session.expected15mBarCount -ne 27) { throw "Expected 27 bars." }
Assert-True $session.preserved "Canonical session preservation missing."

$legacy = Read-JsonArtifact "phase-exec-sim-r040-legacy-compatibility-preservation.json"
Assert-True $legacy.legacyOffsetSixLabelsCompatibilityOnly "Legacy offset labels must be compatibility-only."
Assert-True $legacy.futureCanonicalTimestampsUseQuarterHourCloses "Future canonical quarter-hour closes missing."
Assert-False $legacy.legacy06UsedAsFutureCanonical "Legacy :06 used as future canonical."
if ($legacy.canonicalCloseForLegacyMatchRule -ne "LegacyOutputTimestamp - 6 minutes") { throw "Legacy match alignment rule missing." }
if ($legacy.legacyNextBarExecutionCloseCanonicalRule -ne "LegacyOutputTimestamp + 9 minutes") { throw "Legacy next-bar canonical rule missing." }

$direct = Read-JsonArtifact "phase-exec-sim-r040-direct-cross-exclusion-preservation.json"
Assert-True $direct.directCrossesSignalOnly "Direct-cross signal-only missing."
Assert-True $direct.nettingFirst "Netting-first missing."
Assert-True $direct.executionUniverseUsdPairOnly "USD-pair-only execution missing."
Assert-True $direct.directCrossExecutionDisabled "Direct-cross execution disabled missing."
Assert-False $direct.weakened "Direct-cross exclusion weakened."

$cost = Read-JsonArtifact "phase-exec-sim-r040-cost-guidance-preservation.json"
Assert-True $cost.fiveUsdPerMillionBestCaseMajorOnly "5 USD/million best-case major-only missing."
Assert-False $cost.fiveUsdPerMillionUniversalized "5 USD/million universalized."

$nonmajor = Read-JsonArtifact "phase-exec-sim-r040-nonmajor-calibration-preservation.json"
Assert-True $nonmajor.nonmajorEmScandiCnhCalibrationRequired "Nonmajor calibration missing."
Assert-False $nonmajor.calibrationRequirementWeakened "Nonmajor calibration weakened."

$download = Read-JsonArtifact "phase-exec-sim-r040-no-download-audit.json"
Assert-False $download.filesDownloadedByR040 "R040 downloaded files."
Assert-True $download.operatorDownloadedBeforeR040 "Operator pre-download context missing."
Assert-False $download.externalDownloadAttempted "External download attempted."

$manifestAudit = Read-JsonArtifact "phase-exec-sim-r040-no-manifest-validation-audit.json"
Assert-False $manifestAudit.manifestContentsRead "Manifest contents read."
Assert-False $manifestAudit.manifestJsonParsed "Manifest JSON parsed."
Assert-False $manifestAudit.manifestHashesValidated "Manifest hashes validated."
Assert-False $manifestAudit.manifestRowCountsValidated "Manifest row counts validated."

$rowAudit = Read-JsonArtifact "phase-exec-sim-r040-no-row-validation-audit.json"
Assert-False $rowAudit.quoteRowsRead "Quote rows read."
Assert-False $rowAudit.quoteRowsValidated "Quote rows validated."
Assert-False $rowAudit.rowCountsComputed "Row counts computed."
Assert-False $rowAudit.hashesComputed "Hashes computed."

$importAudit = Read-JsonArtifact "phase-exec-sim-r040-no-import-audit.json"
Assert-False $importAudit.quoteRowsImportedIntoDb "Quote rows imported."
Assert-False $importAudit.persistedSanitizedRowsCreated "Persisted sanitized rows created."
Assert-False $importAudit.dbStateMutated "DB state mutated."

$backtest = Read-JsonArtifact "phase-exec-sim-r040-no-backtest-simulation-audit.json"
Assert-False $backtest.backtestExecuted "Backtest executed."
Assert-False $backtest.simulationExecuted "Simulation executed."

$tca = Read-JsonArtifact "phase-exec-sim-r040-no-tca-result-lines-audit.json"
Assert-False $tca.tcaResultLinesProduced "TCA result lines produced."
Assert-False $tca.newTcaResultLinesCreated "New TCA result lines created."

$orders = Read-JsonArtifact "phase-exec-sim-r040-no-order-fill-report-route-audit.json"
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

$polygon = Read-JsonArtifact "phase-exec-sim-r040-no-polygon-api-call-audit.json"
Assert-False $polygon.polygonApiCalled "Polygon API called."
Assert-False $polygon.downloadExecuted "Download executed."
Assert-False $polygon.apiAccessTested "API access tested."

$lmaxAudit = Read-JsonArtifact "phase-exec-sim-r040-no-lmax-call-audit.json"
Assert-False $lmaxAudit.lmaxCalled "LMAX called."
Assert-False $lmaxAudit.marketDataRequestSent "MarketDataRequest sent."
Assert-False $lmaxAudit.marketDataResponseRead "MarketDataResponse read."
Assert-False $lmaxAudit.brokerActivated "Broker activated."

$external = Read-JsonArtifact "phase-exec-sim-r040-no-external-api-call-audit.json"
Assert-False $external.externalApiCalled "External API called."
Assert-False $external.polygonApiCalled "Polygon API called."
Assert-False $external.lmaxCalled "LMAX called."
Assert-False $external.apiAccessTested "API access tested."

$runtime = Read-JsonArtifact "phase-exec-sim-r040-no-broker-marketdata-runtime-audit.json"
Assert-False $runtime.brokerActivationPerformed "Broker activation performed."
Assert-False $runtime.socketOpened "Socket opened."
Assert-False $runtime.tlsOpened "TLS opened."
Assert-False $runtime.fixOpened "FIX opened."
Assert-False $runtime.marketDataRuntimeStarted "MarketData runtime started."
Assert-False $runtime.marketDataRequestSent "MarketDataRequest sent."
Assert-False $runtime.marketDataResponseRead "MarketDataResponse read."
Assert-False $runtime.apiWorkerSchedulerServiceStarted "API/Worker/Scheduler/Service started."
Assert-False $runtime.timerPollingBackgroundJobIntroduced "Timer/polling/background job introduced."

$usdjpy = Read-JsonArtifact "phase-exec-sim-r040-usdjpy-caveat-preservation.json"
if ($usdjpy.normalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.executionTradableSymbol -ne "USDJPY" -or -not $usdjpy.requiresInversion -or $usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8") {
    throw "USDJPY caveat weakened."
}
Assert-False $usdjpy.caveatWeakened "USDJPY caveat weakened."

$lmax = Read-JsonArtifact "phase-exec-sim-r040-lmax-readonly-baseline-reference.json"
Assert-True $lmax.lmaxReferenceOnly "LMAX reference-only missing."
Assert-False $lmax.lmaxCalledInR040 "LMAX called in R040."
Assert-False $lmax.audusdMisclassifiedAsFailed "AUDUSD misclassified as failed."

$noExternal = Read-JsonArtifact "phase-exec-sim-r040-no-external-audit.json"
Assert-True $noExternal.noExternal "No-external missing."
Assert-False $noExternal.polygonApiCalled "Polygon API called."
Assert-False $noExternal.lmaxCalled "LMAX called."
Assert-False $noExternal.externalApiCalled "External API called."
Assert-False $noExternal.filesDownloadedByR040 "R040 downloaded files."

$forbidden = Read-JsonArtifact "phase-exec-sim-r040-forbidden-actions-audit.json"
Assert-False $forbidden.forbiddenActionsDetected "Forbidden actions detected."
Assert-False $forbidden.manifestValidationExecuted "Manifest validation executed."
Assert-False $forbidden.rowValidationExecuted "Row validation executed."
Assert-False $forbidden.validationImportBacktestExecuted "Validation/import/backtest executed."
Assert-False $forbidden.tcaResultLinesProduced "TCA result lines produced."
Assert-False $forbidden.orderOrTradingPathIntroduced "Order/trading path introduced."
Assert-False $forbidden.schedulerServicePollingBackgroundJobIntroduced "Scheduler/service/polling/background job introduced."
Assert-False $forbidden.stateMutated "State mutated."

$next = Read-JsonArtifact "phase-exec-sim-r040-next-phase-recommendation.json"
if ($next.authorizedEntryCountForR041 -ne 35) { throw "Next phase authorized count mismatch." }
Assert-True $next.r041ShouldValidateManifestFileLevelMetadata "R041 manifest validation recommendation missing."
Assert-True $next.r041ShouldNotValidateRows "R041 no-row-validation recommendation missing."
Assert-True $next.r041ShouldNotImportBacktestOrCreateOrderDomainOutput "R041 safety recommendation missing."

$evidence = Read-JsonArtifact "phase-exec-sim-r040-build-test-validator-evidence.json"
if ($evidence.dotnetBuildNoRestore.status -notlike "PASS*") { throw "Build/test/validator evidence missing: dotnet build." }
if ($evidence.focusedR040Tests.status -notlike "PASS*") { throw "Build/test/validator evidence missing: focused R040 tests." }
if ($evidence.unitTests.status -notlike "PASS*") { throw "Build/test/validator evidence missing: unit tests." }

Write-Host "EXEC_SIM_R040_PASS_ADDITIONAL_HISTORICAL_FILE_AUTHORIZATION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R040_PASS_CONFIRMED_SESSION_FILE_PREFLIGHT_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R040_PASS_NO_VALIDATION_NO_BACKTEST_GATE_READY_NO_EXTERNAL"
