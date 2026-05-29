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
    "phase-exec-sim-r039-summary.md",
    "phase-exec-sim-r039-r038-session-date-reference.json",
    "phase-exec-sim-r039-offline-download-command-contract.json",
    "phase-exec-sim-r039-operator-download-commands.md",
    "phase-exec-sim-r039-operator-download-commands.json",
    "phase-exec-sim-r039-powershell-command-plan.ps1.txt",
    "phase-exec-sim-r039-expected-file-naming-plan.json",
    "phase-exec-sim-r039-expected-future-file-path-patterns.json",
    "phase-exec-sim-r039-operator-post-download-checklist.md",
    "phase-exec-sim-r039-operator-post-download-checklist.json",
    "phase-exec-sim-r039-r040-file-authorization-requirements.json",
    "phase-exec-sim-r039-symbol-provider-mapping-preservation.json",
    "phase-exec-sim-r039-inversion-preservation.json",
    "phase-exec-sim-r039-canonical-session-preservation.json",
    "phase-exec-sim-r039-legacy-compatibility-preservation.json",
    "phase-exec-sim-r039-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r039-cost-guidance-preservation.json",
    "phase-exec-sim-r039-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r039-no-download-audit.json",
    "phase-exec-sim-r039-no-validation-import-backtest-audit.json",
    "phase-exec-sim-r039-no-tca-result-lines-audit.json",
    "phase-exec-sim-r039-no-polygon-api-call-audit.json",
    "phase-exec-sim-r039-no-lmax-call-audit.json",
    "phase-exec-sim-r039-no-external-api-call-audit.json",
    "phase-exec-sim-r039-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r039-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r039-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r039-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r039-no-external-audit.json",
    "phase-exec-sim-r039-forbidden-actions-audit.json",
    "phase-exec-sim-r039-next-phase-recommendation.json",
    "phase-exec-sim-r039-build-test-validator-evidence.json"
)

foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $file))) { throw "Missing required R039 artifact: $file" }
}

$expectedWindows = @{
    "2025-10-14" = @("2025-10-14T18:15:00Z", "2025-10-15T01:00:00Z")
    "2025-10-15" = @("2025-10-15T18:15:00Z", "2025-10-16T01:00:00Z")
    "2025-10-16" = @("2025-10-16T18:15:00Z", "2025-10-17T01:00:00Z")
    "2025-10-17" = @("2025-10-17T18:15:00Z", "2025-10-18T01:00:00Z")
    "2025-10-20" = @("2025-10-20T18:15:00Z", "2025-10-21T01:00:00Z")
}
$expectedProviderSymbols = @("C:EUR-USD", "C:USD-JPY", "C:AUD-USD", "C:GBP-USD", "C:NZD-USD", "C:USD-CAD", "C:USD-CHF")

$ref = Read-JsonArtifact "phase-exec-sim-r039-r038-session-date-reference.json"
if ($ref.sourcePhase -ne "EXEC-SIM-R038") { throw "R038 reference missing." }
if ($ref.confirmedTimezone -ne "America/New_York") { throw "R038 timezone reference missing." }
if ($ref.canonicalSessionLocal -ne "14:15-21:00") { throw "Canonical session reference missing." }
if (@($ref.utcWindows).Count -ne 5) { throw "R038 UTC window reference missing." }
foreach ($window in @($ref.utcWindows)) {
    if (-not $expectedWindows.ContainsKey($window.localDate)) { throw "Unexpected referenced window date: $($window.localDate)" }
    $pair = $expectedWindows[$window.localDate]
    if ($window.fromUtc -ne $pair[0] -or $window.toUtc -ne $pair[1]) { throw "Unexpected referenced UTC range for $($window.localDate)." }
}
Assert-True $ref.noDownloadInR038 "R038 no-download reference missing."
Assert-True $ref.r039NoExternal "R039 no-external reference missing."

$contract = Read-JsonArtifact "phase-exec-sim-r039-offline-download-command-contract.json"
if ($contract.contractStatus -ne "OfflineDownloadCommandsReadyNoExternal") { throw "Command contract status missing." }
Assert-True $contract.commandGenerationOnly "Command generation only missing."
Assert-True $contract.operatorRunRequired "Operator-run requirement missing."
if ($contract.downloadScriptName -ne ".\scripts\download-polygon-fx-bbo-offline.ps1") { throw "Download script plan missing." }
Assert-False $contract.commandsExecutedInR039 "Generated commands executed."
Assert-False $contract.downloadExecutedInR039 "Download executed."
if ($contract.providerName -ne "PolygonOfflineFile" -or $contract.datasetType -ne "HistoricalBboQuotes" -or $contract.fileFormat -ne "NDJSON") { throw "Provider/dataset/format missing." }
if ($contract.symbolCountPerCommand -ne 7 -or $contract.windowCount -ne 5) { throw "Window or symbol count missing." }
if ($contract.expectedFutureQuoteFileCount -ne 35 -or $contract.expectedFutureManifestCount -ne 35) { throw "Expected future file counts missing." }
Assert-True $contract.noApiCall "NoApiCall missing."
Assert-True $contract.noValidation "NoValidation missing."
Assert-True $contract.noImport "NoImport missing."
Assert-True $contract.noBacktest "NoBacktest missing."
Assert-True $contract.noTcaResultLines "NoTcaResultLines missing."

$commands = Read-JsonArtifact "phase-exec-sim-r039-operator-download-commands.json"
Assert-True $commands.commandsAreTextOnly "Commands must be text only."
Assert-False $commands.commandsExecutedInR039 "Commands executed in R039."
if (@($commands.symbols).Count -ne 7) { throw "Expected seven provider symbols." }
foreach ($symbol in $expectedProviderSymbols) {
    if ($symbol -notin @($commands.symbols)) { throw "Missing provider symbol: $symbol" }
}
if (@($commands.commands).Count -ne 5) { throw "Expected five command blocks." }
foreach ($cmd in @($commands.commands)) {
    if (-not $expectedWindows.ContainsKey($cmd.localSessionDate)) { throw "Unexpected command window date: $($cmd.localSessionDate)" }
    $pair = $expectedWindows[$cmd.localSessionDate]
    if ($cmd.fromUtc -ne $pair[0] -or $cmd.toUtc -ne $pair[1]) { throw "Unexpected command UTC range for $($cmd.localSessionDate)." }
    if ($cmd.symbolCount -ne 7) { throw "Command does not include all seven symbols." }
    if ($cmd.script -ne ".\scripts\download-polygon-fx-bbo-offline.ps1") { throw "Command script mismatch." }
}
if ($commands.expectedFutureQuoteFileCount -ne 35 -or $commands.expectedFutureManifestCount -ne 35) { throw "Expected future file counts missing in commands." }
Assert-False $commands.downloadExecuted "Download executed."
Assert-False $commands.filesDownloaded "Files downloaded."

$planPath = Join-Path $artifactDir "phase-exec-sim-r039-powershell-command-plan.ps1.txt"
$planText = Get-Content -LiteralPath $planPath -Raw
foreach ($symbol in $expectedProviderSymbols) {
    if ($planText -notmatch [regex]::Escape($symbol)) { throw "Command plan missing provider symbol: $symbol" }
}
foreach ($date in $expectedWindows.Keys) {
    $pair = $expectedWindows[$date]
    if ($planText -notmatch [regex]::Escape($pair[0]) -or $planText -notmatch [regex]::Escape($pair[1])) { throw "Command plan missing UTC window: $date" }
}

$naming = Read-JsonArtifact "phase-exec-sim-r039-expected-file-naming-plan.json"
Assert-True $naming.guidanceOnly "Naming plan must be guidance only."
Assert-False $naming.filesCreated "Naming plan created files."
Assert-False $naming.downloadedFilesClaimedToExist "Naming plan claims files exist."
if (@($naming.quoteFileTemplates).Count -ne 7) { throw "Expected seven quote file templates." }
Assert-False $naming.rowCountsInvented "Row counts invented."
Assert-False $naming.hashesInvented "Hashes invented."

$pathPatterns = Read-JsonArtifact "phase-exec-sim-r039-expected-future-file-path-patterns.json"
Assert-True $pathPatterns.patternsOnly "Path plan must be patterns only."
Assert-False $pathPatterns.concreteFilesCreated "Concrete files created."
Assert-False $pathPatterns.concretePathsClaimedToExist "Concrete paths claimed to exist."
if ($pathPatterns.expectedFutureQuoteFileCount -ne 35 -or $pathPatterns.expectedFutureManifestCount -ne 35) { throw "Expected future file path counts missing." }
Assert-True $pathPatterns.r040MustSupplyConcretePaths "R040 concrete path requirement missing."

$checklist = Read-JsonArtifact "phase-exec-sim-r039-operator-post-download-checklist.json"
if (@($checklist.checklist).Count -lt 8) { throw "Post-download checklist incomplete." }
Assert-False $checklist.apiKeysSerialized "API keys serialized."
Assert-False $checklist.downloadExecutedInR039 "Checklist indicates download executed."

$r040 = Read-JsonArtifact "phase-exec-sim-r039-r040-file-authorization-requirements.json"
if ($r040.requiredQuoteFilePaths -ne 35 -or $r040.requiredManifestPaths -ne 35) { throw "R040 concrete file requirements missing." }
Assert-True $r040.r040MustNotValidateManifests "R040 manifest validation block missing."
Assert-True $r040.r040MustNotValidateRows "R040 row validation block missing."
Assert-True $r040.r040MustNotImport "R040 import block missing."
Assert-True $r040.r040MustNotBacktest "R040 backtest block missing."
Assert-False $r040.concretePathsProvidedInR039 "Concrete paths provided in R039."
Assert-False $r040.rowCountsInventedInR039 "Row counts invented in R039."
Assert-False $r040.hashesInventedInR039 "Hashes invented in R039."

$mapping = Read-JsonArtifact "phase-exec-sim-r039-symbol-provider-mapping-preservation.json"
Assert-True $mapping.allSevenSymbolsIncluded "All seven symbols not included."
Assert-False $mapping.directCrossesIncluded "Direct crosses included."
foreach ($symbol in $expectedProviderSymbols) {
    if ($symbol -notin @($mapping.mappings.providerSymbol)) { throw "Mapping missing provider symbol: $symbol" }
}

$inversion = Read-JsonArtifact "phase-exec-sim-r039-inversion-preservation.json"
Assert-False $inversion.inversionWeakened "Inversion weakened."
Assert-False $inversion.audusdMisclassifiedAsFailed "AUDUSD misclassified as failed."
if (-not (@($inversion.invertedSymbols) | Where-Object { $_.executionTradableSymbol -eq "USDJPY" -and $_.normalizedPortfolioSymbol -eq "JPYUSD" -and $_.requiresInversion -and $_.securityId -eq "4004" -and $_.securityIdSource -eq "8" })) { throw "USDJPY inversion/caveat missing." }
if (-not (@($inversion.invertedSymbols) | Where-Object { $_.executionTradableSymbol -eq "USDCAD" -and $_.normalizedPortfolioSymbol -eq "CADUSD" -and $_.requiresInversion })) { throw "USDCAD inversion missing." }
if (-not (@($inversion.invertedSymbols) | Where-Object { $_.executionTradableSymbol -eq "USDCHF" -and $_.normalizedPortfolioSymbol -eq "CHFUSD" -and $_.requiresInversion })) { throw "USDCHF inversion missing." }

$session = Read-JsonArtifact "phase-exec-sim-r039-canonical-session-preservation.json"
if ($session.canonicalSessionLocal -ne "14:15-21:00 America/New_York") { throw "Canonical session preservation missing." }
if (@($session.canonicalCloseMinuteSet) -join "," -ne "00,15,30,45") { throw "Canonical close minute set missing." }
if ($session.expected15mBarCount -ne 27) { throw "Expected 27 bars." }
Assert-True $session.fullSessionDownloadsOnly "R039 should only generate full-session downloads."

$legacy = Read-JsonArtifact "phase-exec-sim-r039-legacy-compatibility-preservation.json"
Assert-True $legacy.legacyOffsetSixLabelsCompatibilityOnly "Legacy offset labels must be compatibility-only."
Assert-True $legacy.futureCanonicalTimestampsUseQuarterHourCloses "Future canonical quarter-hour closes missing."
Assert-False $legacy.legacy06UsedAsFutureCanonical "Legacy :06 used as future canonical."
if ($legacy.canonicalCloseForLegacyMatchRule -ne "LegacyOutputTimestamp - 6 minutes") { throw "Legacy match alignment rule missing." }
if ($legacy.legacyNextBarExecutionCloseCanonicalRule -ne "LegacyOutputTimestamp + 9 minutes") { throw "Legacy next-bar canonical rule missing." }

$direct = Read-JsonArtifact "phase-exec-sim-r039-direct-cross-exclusion-preservation.json"
Assert-True $direct.directCrossesSignalOnly "Direct-cross signal-only missing."
Assert-True $direct.nettingFirst "Netting-first missing."
Assert-True $direct.executionUniverseUsdPairOnly "USD-pair-only execution missing."
Assert-True $direct.directCrossExecutionDisabled "Direct-cross execution disabled missing."
Assert-False $direct.weakened "Direct-cross exclusion weakened."

$cost = Read-JsonArtifact "phase-exec-sim-r039-cost-guidance-preservation.json"
Assert-True $cost.fiveUsdPerMillionBestCaseMajorOnly "5 USD/million best-case major-only missing."
Assert-False $cost.fiveUsdPerMillionUniversalized "5 USD/million universalized."

$nonmajor = Read-JsonArtifact "phase-exec-sim-r039-nonmajor-calibration-preservation.json"
Assert-True $nonmajor.nonmajorEmScandiCnhCalibrationRequired "Nonmajor calibration missing."
Assert-False $nonmajor.calibrationRequirementWeakened "Nonmajor calibration weakened."

$download = Read-JsonArtifact "phase-exec-sim-r039-no-download-audit.json"
Assert-True $download.downloadCommandsGeneratedAsTextOnly "Download command text generation missing."
Assert-False $download.downloadCommandsExecuted "Download commands executed."
Assert-False $download.filesDownloaded "Files downloaded."
Assert-False $download.externalDownloadAttempted "External download attempted."
Assert-False $download.downloadedFileExistenceClaimed "Downloaded file existence claimed."

$validation = Read-JsonArtifact "phase-exec-sim-r039-no-validation-import-backtest-audit.json"
Assert-False $validation.quoteRowsValidated "Quote rows validated."
Assert-False $validation.manifestsValidated "Manifests validated."
Assert-False $validation.quoteRowsImportedIntoDb "Quote rows imported."
Assert-False $validation.persistedSanitizedRowsCreated "Persisted sanitized rows created."
Assert-False $validation.simulationExecuted "Simulation executed."
Assert-False $validation.backtestExecuted "Backtest executed."

$tca = Read-JsonArtifact "phase-exec-sim-r039-no-tca-result-lines-audit.json"
Assert-False $tca.tcaResultLinesProduced "TCA result lines produced."
Assert-False $tca.newTcaResultLinesCreated "New TCA result lines created."

$polygon = Read-JsonArtifact "phase-exec-sim-r039-no-polygon-api-call-audit.json"
Assert-False $polygon.polygonApiCalled "Polygon API called."
Assert-False $polygon.downloadScriptExecuted "Download script executed."
Assert-False $polygon.apiAccessTested "API access tested."

$lmaxAudit = Read-JsonArtifact "phase-exec-sim-r039-no-lmax-call-audit.json"
Assert-False $lmaxAudit.lmaxCalled "LMAX called."
Assert-False $lmaxAudit.marketDataRequestSent "MarketDataRequest sent."
Assert-False $lmaxAudit.marketDataResponseRead "MarketDataResponse read."
Assert-False $lmaxAudit.brokerActivated "Broker activated."

$external = Read-JsonArtifact "phase-exec-sim-r039-no-external-api-call-audit.json"
Assert-False $external.externalApiCalled "External API called."
Assert-False $external.polygonApiCalled "Polygon API called."
Assert-False $external.lmaxCalled "LMAX called."
Assert-False $external.apiAccessTested "API access tested."

$runtime = Read-JsonArtifact "phase-exec-sim-r039-no-broker-marketdata-runtime-audit.json"
Assert-False $runtime.brokerActivationPerformed "Broker activation performed."
Assert-False $runtime.socketOpened "Socket opened."
Assert-False $runtime.tlsOpened "TLS opened."
Assert-False $runtime.fixOpened "FIX opened."
Assert-False $runtime.marketDataRuntimeStarted "MarketData runtime started."
Assert-False $runtime.marketDataRequestSent "MarketDataRequest sent."
Assert-False $runtime.marketDataResponseRead "MarketDataResponse read."
Assert-False $runtime.apiWorkerSchedulerServiceStarted "API/Worker/Scheduler/Service started."
Assert-False $runtime.timerPollingBackgroundJobIntroduced "Timer/polling/background job introduced."

$orders = Read-JsonArtifact "phase-exec-sim-r039-no-order-fill-report-route-audit.json"
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

$usdjpy = Read-JsonArtifact "phase-exec-sim-r039-usdjpy-caveat-preservation.json"
if ($usdjpy.normalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.executionTradableSymbol -ne "USDJPY" -or -not $usdjpy.requiresInversion -or $usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8") {
    throw "USDJPY caveat weakened."
}
Assert-False $usdjpy.caveatWeakened "USDJPY caveat weakened."

$lmax = Read-JsonArtifact "phase-exec-sim-r039-lmax-readonly-baseline-reference.json"
Assert-True $lmax.lmaxReferenceOnly "LMAX reference-only missing."
Assert-False $lmax.lmaxCalledInR039 "LMAX called in R039."
Assert-False $lmax.audusdMisclassifiedAsFailed "AUDUSD misclassified as failed."

$noExternal = Read-JsonArtifact "phase-exec-sim-r039-no-external-audit.json"
Assert-True $noExternal.noExternal "No-external missing."
Assert-False $noExternal.polygonApiCalled "Polygon API called."
Assert-False $noExternal.lmaxCalled "LMAX called."
Assert-False $noExternal.externalApiCalled "External API called."
Assert-False $noExternal.filesDownloaded "Files downloaded."
Assert-False $noExternal.downloadCommandsExecuted "Download commands executed."

$forbidden = Read-JsonArtifact "phase-exec-sim-r039-forbidden-actions-audit.json"
Assert-False $forbidden.forbiddenActionsDetected "Forbidden actions detected."
Assert-False $forbidden.downloadCommandsExecuted "Download commands executed."
Assert-False $forbidden.pythonOrScriptExecuted "Python/script executed."
Assert-False $forbidden.qubesExecutableRun "Qubes executable run."
Assert-False $forbidden.cppCudaWorkloadRun "C++/CUDA workload run."
Assert-False $forbidden.validationImportBacktestExecuted "Validation/import/backtest executed."
Assert-False $forbidden.tcaResultLinesProduced "TCA result lines produced."
Assert-False $forbidden.orderOrTradingPathIntroduced "Order/trading path introduced."
Assert-False $forbidden.schedulerServicePollingBackgroundJobIntroduced "Scheduler/service/polling/background job introduced."
Assert-False $forbidden.stateMutated "State mutated."
Assert-False $forbidden.rowCountsInvented "Row counts invented."
Assert-False $forbidden.hashesInvented "Hashes invented."
Assert-False $forbidden.downloadedFileExistenceClaimed "Downloaded file existence claimed."

$next = Read-JsonArtifact "phase-exec-sim-r039-next-phase-recommendation.json"
Assert-True $next.needsOperatorFileDownload "Needs operator file download missing."
Assert-True $next.r040ShouldAuthorizeConcreteDownloadedFiles "R040 authorization recommendation missing."
Assert-True $next.r040ShouldNotValidateImportBacktest "R040 safety recommendation missing."

$evidence = Read-JsonArtifact "phase-exec-sim-r039-build-test-validator-evidence.json"
if ($evidence.dotnetBuildNoRestore.status -notlike "PASS*") { throw "Build/test/validator evidence missing: dotnet build." }
if ($evidence.focusedR039Tests.status -notlike "PASS*") { throw "Build/test/validator evidence missing: focused R039 tests." }
if ($evidence.unitTests.status -notlike "PASS*") { throw "Build/test/validator evidence missing: unit tests." }

Write-Host "EXEC_SIM_R039_PASS_OFFLINE_DOWNLOAD_COMMANDS_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R039_PASS_OPERATOR_DOWNLOAD_PLAN_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R039_PASS_NO_DOWNLOAD_NO_BACKTEST_GATE_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R039_NEEDS_OPERATOR_FILE_DOWNLOAD_NO_EXTERNAL"
