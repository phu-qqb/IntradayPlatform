$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $root "artifacts/readiness/execution-sim"

function Read-JsonArtifact {
    param([Parameter(Mandatory=$true)][string]$Name)
    $path = Join-Path $artifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing required artifact: $Name"
    }
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

function Assert-False {
    param([bool]$Value, [string]$Message)
    if ($Value) { throw $Message }
}

function Assert-True {
    param([bool]$Value, [string]$Message)
    if (-not $Value) { throw $Message }
}

$requiredFiles = @(
    "phase-exec-sim-r036-summary.md",
    "phase-exec-sim-r036-r035-derivation-reference.json",
    "phase-exec-sim-r036-operator-session-confirmation-contract.json",
    "phase-exec-sim-r036-timestamp-semantics-confirmation.json",
    "phase-exec-sim-r036-timezone-confirmation.json",
    "phase-exec-sim-r036-session-window-confirmation.json",
    "phase-exec-sim-r036-partial-day-treatment-confirmation.json",
    "phase-exec-sim-r036-historical-date-range-intake.json",
    "phase-exec-sim-r036-confirmation-result.json",
    "phase-exec-sim-r036-needs-operator-input.json",
    "phase-exec-sim-r036-weekdays-only-preservation.json",
    "phase-exec-sim-r036-trade-all-15m-bars-preservation.json",
    "phase-exec-sim-r036-no-overnight-preservation.json",
    "phase-exec-sim-r036-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r036-cost-guidance-preservation.json",
    "phase-exec-sim-r036-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r036-no-python-execution-audit.json",
    "phase-exec-sim-r036-no-script-execution-audit.json",
    "phase-exec-sim-r036-no-qubes-executable-run-audit.json",
    "phase-exec-sim-r036-no-cpp-cuda-run-audit.json",
    "phase-exec-sim-r036-no-download-audit.json",
    "phase-exec-sim-r036-no-validation-import-backtest-audit.json",
    "phase-exec-sim-r036-no-tca-result-lines-audit.json",
    "phase-exec-sim-r036-no-polygon-api-call-audit.json",
    "phase-exec-sim-r036-no-lmax-call-audit.json",
    "phase-exec-sim-r036-no-external-api-call-audit.json",
    "phase-exec-sim-r036-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r036-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r036-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r036-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r036-no-external-audit.json",
    "phase-exec-sim-r036-forbidden-actions-audit.json",
    "phase-exec-sim-r036-next-phase-recommendation.json",
    "phase-exec-sim-r036-build-test-validator-evidence.json"
)

foreach ($file in $requiredFiles) {
    $path = Join-Path $artifactDir $file
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing required R036 artifact: $file"
    }
}

$reference = Read-JsonArtifact "phase-exec-sim-r036-r035-derivation-reference.json"
Assert-True $reference.r036NoExternal "R036 no-external flag missing."
Assert-True $reference.r036NoExecution "R036 no-execution flag missing."
if ($reference.legacyCloseTimestampConvention -ne "OffsetSixMinuteCloseLabels") { throw "Legacy offset-six close convention missing from R035 reference." }
if ($reference.targetExecutionCloseLegacyRule -ne "TargetExecutionCloseLegacy = AggregatedWeightsTimestamp + 15 minutes") { throw "Legacy target execution close rule missing." }
if ($reference.standardCloseAlignmentRule -ne "StandardCloseAlignmentTimestamp = LegacyCloseTimestamp - 6 minutes") { throw "Standard close alignment rule missing." }
if ($reference.confirmedTimezone -ne "America/New_York") { throw "New York timezone confirmation missing from R035 reference." }
if ($reference.strategySessionPattern -ne "SingleSessionPerStrategy") { throw "SingleSessionPerStrategy missing from R035 reference." }

$contract = Read-JsonArtifact "phase-exec-sim-r036-operator-session-confirmation-contract.json"
Assert-True $contract.requiresExplicitOperatorInput "R036 must require explicit operator input."
Assert-True $contract.operatorInputReceived "R036 operator input should be recorded."
Assert-False $contract.allowsInferenceAuthorization "R036 must not allow inference authorization."
Assert-True $contract.timestampSemanticsConfirmationRequired "Timestamp confirmation required missing."
Assert-True $contract.timestampSemanticsConfirmed "Timestamp semantics should be confirmed."
Assert-True $contract.timezoneConfirmationRequired "Timezone confirmation required missing."
Assert-True $contract.timezoneConfirmed "Timezone should be confirmed."
Assert-True $contract.sessionWindowConfirmationRequired "Session window confirmation required missing."
Assert-True $contract.sessionStructureConfirmed "Session structure should be confirmed."
Assert-True $contract.partialDayTreatmentConfirmationRequired "Partial-day confirmation required missing."
Assert-True $contract.partialDayTreatmentConfirmed "Partial-day treatment should be confirmed."
Assert-True $contract.historicalDateRangeIntakeRequired "Date range intake required missing."
Assert-False $contract.historicalDateRangesProvided "Historical date ranges should remain missing."
if ($contract.legacyCloseTimestampConvention -ne "OffsetSixMinuteCloseLabels") { throw "Legacy close timestamp convention missing." }
if (@($contract.legacyCloseMinuteSet).Count -ne 4 -or -not (@($contract.legacyCloseMinuteSet) -contains "06") -or -not (@($contract.legacyCloseMinuteSet) -contains "21") -or -not (@($contract.legacyCloseMinuteSet) -contains "36") -or -not (@($contract.legacyCloseMinuteSet) -contains "51")) { throw "Legacy close minute set missing." }
if ($contract.targetExecutionCloseLegacyRule -ne "TargetExecutionCloseLegacy = AggregatedWeightsTimestamp + 15 minutes") { throw "TargetExecutionCloseLegacy rule missing." }
if ($contract.standardCloseAlignmentRule -ne "StandardCloseAlignmentTimestamp = LegacyCloseTimestamp - 6 minutes") { throw "Standard close alignment rule missing." }
if ($contract.confirmedTimezone -ne "America/New_York") { throw "America/New_York timezone missing." }
if ($contract.strategySessionPattern -ne "SingleSessionPerStrategy") { throw "SingleSessionPerStrategy missing." }
Assert-True $contract.noDownloadsAuthorized "Downloads authorized unexpectedly."
Assert-True $contract.noBacktestAuthorized "Backtest authorized unexpectedly."
Assert-True $contract.noTimezoneInvented "Timezone invention guard missing."
Assert-True $contract.noSessionWindowInvented "Session window invention guard missing."
Assert-True $contract.noDateRangesInvented "Date range invention guard missing."

$timestamp = Read-JsonArtifact "phase-exec-sim-r036-timestamp-semantics-confirmation.json"
if ($timestamp.legacyCloseTimestampConvention -ne "OffsetSixMinuteCloseLabels") { throw "Legacy close timestamp convention missing in timestamp confirmation." }
if ($timestamp.aggregatedWeightsTimestampRole -ne "LegacyWeightTimestampAndLegacyCloseLabel") { throw "AggregatedWeights timestamp role not confirmed correctly." }
Assert-True $timestamp.closeTimestampsUseLegacyOffsetSixMinuteCadence "Legacy offset-six close cadence missing."
if ($timestamp.targetExecutionCloseLegacyRule -ne "TargetExecutionCloseLegacy = AggregatedWeightsTimestamp + 15 minutes") { throw "Target execution close rule missing in timestamp confirmation." }
if ($timestamp.targetExecutionCloseLegacyOffsetMinutes -ne 15) { throw "Target execution close offset must be +15 minutes." }
if ($timestamp.standardCloseAlignmentRule -ne "StandardCloseAlignmentTimestamp = LegacyCloseTimestamp - 6 minutes") { throw "Standard close alignment rule missing in timestamp confirmation." }
if ($timestamp.standardCloseAlignmentOffsetMinutes -ne -6) { throw "Standard close alignment offset must be -6 minutes." }
Assert-True $timestamp.doNotReplaceLegacyCloseLabelWithStandardClose "Legacy close labels must not be replaced by standard close timestamps."
Assert-True $timestamp.doNotClassifySolelyAsProducedAt "AggregatedWeights must not be classified solely as ProducedAt."
Assert-True $timestamp.preserveLegacyCloseTimestamp "LegacyCloseTimestamp preservation missing."
Assert-True $timestamp.preserveStandardCloseAlignmentTimestamp "StandardCloseAlignmentTimestamp preservation missing."
Assert-True $timestamp.operatorConfirmed "Timestamp semantics should be confirmed."
Assert-True $timestamp.authorizationReady "Timestamp semantics should be authorization-ready."
Assert-False $timestamp.timestampSemanticsInvented "Timestamp semantics invented."
if ($timestamp.confirmationStatus -ne "TimestampSemanticsConfirmedNoExternal") { throw "Timestamp semantics confirmed status missing." }

$timezone = Read-JsonArtifact "phase-exec-sim-r036-timezone-confirmation.json"
if ($timezone.confirmedTimezone -ne "America/New_York") { throw "Confirmed timezone must be America/New_York." }
if ($timezone.timezoneEvidenceSource -ne "OperatorConfirmed") { throw "Timezone evidence source must be OperatorConfirmed." }
if ($timezone.timezoneCanonicalForm -ne "IANA") { throw "Timezone canonical form must be IANA." }
Assert-True $timezone.dstBehaviorPreserved "DST behavior must be preserved for America/New_York."
Assert-True $timezone.operatorConfirmed "Timezone should be confirmed."
Assert-True $timezone.authorizationReady "Timezone should be authorization-ready."
Assert-False $timezone.timezoneInvented "Timezone invented."
if ($timezone.confirmationStatus -ne "TimezoneConfirmedNoExternal") { throw "Timezone confirmed status missing." }

$session = Read-JsonArtifact "phase-exec-sim-r036-session-window-confirmation.json"
if ($session.confirmedSessionPattern -ne "SingleSessionPerStrategy") { throw "Session pattern must be SingleSessionPerStrategy." }
if ($session.strategySessionPattern -ne "SingleSessionPerStrategy") { throw "Strategy session pattern must be SingleSessionPerStrategy." }
Assert-True $session.observedFileContainsSingleStrategy "Observed file should be recorded as single strategy."
Assert-False $session.multipleObservedPatternsAreMultipleSessionsByDefault "Observed patterns must not imply multiple sessions by default."
Assert-False ($null -ne $session.confirmedSessionOpenLocal) "Session open invented."
Assert-False ($null -ne $session.confirmedSessionCloseLocal) "Session close invented."
Assert-False ($null -ne $session.confirmedFirstBarCloseLocal) "First bar close invented."
Assert-False ($null -ne $session.confirmedLastBarCloseLocal) "Last bar close invented."
Assert-True $session.canonicalSessionDerivationDeferredToR037 "Canonical session derivation should be deferred to R037."
if ($session.barIntervalMinutes -ne 15) { throw "Bar interval must remain 15." }
Assert-True $session.weekdaysOnly "WeekdaysOnly not preserved."
Assert-True $session.tradeAll15mBars "TradeAll15mBars not preserved."
Assert-True $session.mustEndFlat "MustEndFlat not preserved."
Assert-False $session.overnightAllowed "OvernightAllowed=false not preserved."
Assert-True $session.previousEveningFirstTargetAvailabilityExpected "Previous-evening first target expectation missing."
Assert-False $session.previousEveningFirstTargetAvailabilityExactTimeConfirmed "Previous-evening exact time should remain unconfirmed."
Assert-True $session.operatorConfirmed "Session structure should be confirmed."
Assert-True $session.authorizationReadyForR037Derivation "Session derivation should be ready for R037."
Assert-False $session.sessionWindowInvented "Session window invented."
if ($session.confirmationStatus -ne "SessionWindowConfirmedNoExternal") { throw "Session window confirmed status missing." }

$partial = Read-JsonArtifact "phase-exec-sim-r036-partial-day-treatment-confirmation.json"
if ($partial.partialDaysExpected -ne "Unknown") { throw "PartialDaysExpected must be Unknown." }
Assert-True $partial.partialDaysExcluded "Partial days should be excluded."
Assert-True $partial.partialDaysExcludedForCanonicalSession "Partial days should be excluded for canonical session derivation."
Assert-True $partial.partialDaysRequireManualReview "Partial days should require manual review while unconfirmed."
Assert-True $partial.operatorConfirmed "Partial-day treatment should be confirmed."
Assert-False $partial.partialDayTreatmentInvented "Partial-day treatment invented."
if ($partial.treatmentStatus -ne "PartialDayTreatmentConfirmedNoExternal") { throw "Partial-day confirmed status missing." }

$dates = Read-JsonArtifact "phase-exec-sim-r036-historical-date-range-intake.json"
if (@($dates.requiredSymbols).Count -ne 7) { throw "Required seven-symbol universe missing." }
if (@($dates.requestedDateRanges).Count -ne 0) { throw "Date ranges invented." }
if ($dates.minimumDateCount -ne 5) { throw "Minimum date count must be 5." }
Assert-True $dates.includeOpeningBuild "OpeningBuild requirement missing."
Assert-True $dates.includeIntradayRebalance "IntradayRebalance requirement missing."
Assert-True $dates.includeClosingFlatten "ClosingFlatten requirement missing."
Assert-True $dates.requiresUtcRanges "UTC range requirement missing."
Assert-True $dates.requiresOperatorProvidedFiles "Operator-provided file requirement missing."
Assert-False $dates.operatorConfirmed "Date ranges incorrectly marked confirmed."
Assert-False $dates.dateRangesInvented "Date ranges invented."
Assert-False $dates.downloadAuthorized "Download authorized unexpectedly."
Assert-False $dates.historicalDateRangeIntakeReady "Historical date range intake should not be ready."
if ($dates.dateRangeStatus -ne "NeedsOperatorDateRanges") { throw "Date range needs-input status missing." }

$result = Read-JsonArtifact "phase-exec-sim-r036-confirmation-result.json"
Assert-True $result.operatorConfirmationProvided "Operator confirmation should be detected."
Assert-True $result.timestampSemanticsConfirmed "Timestamp semantics should be confirmed."
Assert-True $result.timezoneConfirmed "Timezone should be confirmed."
Assert-True $result.sessionWindowConfirmed "Session window should be confirmed."
Assert-True $result.sessionStructureConfirmed "Session structure should be confirmed."
Assert-True $result.partialDayTreatmentConfirmed "Partial-day treatment should be confirmed."
Assert-False $result.historicalDateRangesProvided "Historical date ranges incorrectly provided."
Assert-True $result.authorizationReadyForR037 "R037 session derivation should be ready."
Assert-False $result.historicalDateRangeIntakeReady "Historical date range intake should not be ready."
if ($result.legacyCloseTimestampConvention -ne "OffsetSixMinuteCloseLabels") { throw "Legacy close timestamp convention missing in result." }
if ($result.targetExecutionCloseLegacyRule -ne "TargetExecutionCloseLegacy = AggregatedWeightsTimestamp + 15 minutes") { throw "Target execution close rule missing in result." }
if ($result.standardCloseAlignmentRule -ne "StandardCloseAlignmentTimestamp = LegacyCloseTimestamp - 6 minutes") { throw "Standard close alignment rule missing in result." }
if ($result.confirmedTimezone -ne "America/New_York") { throw "America/New_York missing in result." }
if ($result.strategySessionPattern -ne "SingleSessionPerStrategy") { throw "SingleSessionPerStrategy missing in result." }
Assert-True $result.partialDaysExcludedForCanonicalSession "Partial days must be excluded for canonical session."
Assert-True $result.partialDaysRequireManualReview "Partial days must require manual review."
Assert-False $result.timezoneInvented "Timezone invented."
Assert-False $result.sessionWindowInvented "Session window invented."
Assert-False $result.dateRangesInvented "Date ranges invented."
Assert-True $result.noExternal "No-external missing in confirmation result."
Assert-False $result.downloadExecuted "Download executed."
Assert-False $result.pythonExecuted "Python executed."
Assert-False $result.scriptsExecuted "Scripts executed."
Assert-False $result.qubesExecutableRun "Qubes executable run."
Assert-False $result.cppExecutableRun "C++ executable run."
Assert-False $result.cudaWorkloadRun "CUDA workload run."
Assert-False $result.validationImportBacktestExecuted "Validation/import/backtest executed."
Assert-False $result.tcaResultLinesProduced "TCA lines produced."
Assert-False $result.ordersFillsReportsRoutesCreated "Orders/fills/reports/routes created."
Assert-False $result.stateMutated "State mutated."

$needs = Read-JsonArtifact "phase-exec-sim-r036-needs-operator-input.json"
Assert-True $needs.needsOperatorInput "Needs operator input missing."
Assert-True $needs.timestampSemanticsInputReceived "Timestamp semantics input should be received."
Assert-True $needs.timezoneInputReceived "Timezone input should be received."
Assert-True $needs.sessionStructureInputReceived "Session structure input should be received."
Assert-True $needs.partialDayTreatmentInputReceived "Partial-day treatment input should be received."
Assert-False $needs.dateRangeInputReceived "Date range input should remain missing."
Assert-True $needs.noValuesInvented "No-values-invented guard missing."
if (@($needs.items).Count -lt 1) { throw "Needs operator input items missing." }

$weekdays = Read-JsonArtifact "phase-exec-sim-r036-weekdays-only-preservation.json"
Assert-True $weekdays.weekdaysOnly "WeekdaysOnly missing."
Assert-False $weekdays.weakened "WeekdaysOnly weakened."

$bars = Read-JsonArtifact "phase-exec-sim-r036-trade-all-15m-bars-preservation.json"
Assert-True $bars.tradeAll15mBars "TradeAll15mBars missing."
if ($bars.barIntervalMinutes -ne 15) { throw "Bar interval must be 15." }
Assert-False $bars.weakened "TradeAll15mBars weakened."

$overnight = Read-JsonArtifact "phase-exec-sim-r036-no-overnight-preservation.json"
Assert-True $overnight.mustEndFlat "MustEndFlat missing."
Assert-False $overnight.overnightAllowed "OvernightAllowed=false missing."
Assert-False $overnight.weakened "No-overnight weakened."

$direct = Read-JsonArtifact "phase-exec-sim-r036-direct-cross-exclusion-preservation.json"
Assert-True $direct.directCrossesSignalOnly "Direct-cross signal-only missing."
Assert-True $direct.nettingFirst "Netting-first missing."
Assert-True $direct.directCrossExecutionDisabled "Direct-cross disabled missing."
Assert-False $direct.weakened "Direct-cross exclusion weakened."

$cost = Read-JsonArtifact "phase-exec-sim-r036-cost-guidance-preservation.json"
Assert-True $cost.fiveUsdPerMillionBestCaseMajorOnly "5 USD/million best-case major-only missing."
Assert-False $cost.fiveUsdPerMillionUniversalized "5 USD/million universalized."
Assert-False $cost.weakened "Cost guidance weakened."

$nonmajor = Read-JsonArtifact "phase-exec-sim-r036-nonmajor-calibration-preservation.json"
Assert-True $nonmajor.nonmajorEmScandiCnhCalibrationRequired "Nonmajor calibration missing."
Assert-False $nonmajor.calibrationRequirementWeakened "Nonmajor calibration weakened."

$pythonAudit = Read-JsonArtifact "phase-exec-sim-r036-no-python-execution-audit.json"
Assert-False $pythonAudit.pythonCodeExecuted "Python executed."
Assert-False $pythonAudit.notebooksExecuted "Notebook executed."

$scriptAudit = Read-JsonArtifact "phase-exec-sim-r036-no-script-execution-audit.json"
Assert-False $scriptAudit.scriptsExecuted "Scripts executed."

$qubesAudit = Read-JsonArtifact "phase-exec-sim-r036-no-qubes-executable-run-audit.json"
Assert-False $qubesAudit.qubesExecutableRun "Qubes executable run."
Assert-False $qubesAudit.prodGenerateSignalsBinaryFilesRun "PRODgenerateSignalsBinaryFiles run."
Assert-False $qubesAudit.prodGenerateTimeSeriesRun "PRODgenerateTimeSeries run."
Assert-False $qubesAudit.prodManagerRun "PRODmanager run."
Assert-False $qubesAudit.prodAnubisRun "PRODAnubis run."

$cppCudaAudit = Read-JsonArtifact "phase-exec-sim-r036-no-cpp-cuda-run-audit.json"
Assert-False $cppCudaAudit.cppExecutableRun "C++ executable run."
Assert-False $cppCudaAudit.cudaWorkloadRun "CUDA workload run."
Assert-False $cppCudaAudit.nativeExecutableRun "Native executable run."

$downloadAudit = Read-JsonArtifact "phase-exec-sim-r036-no-download-audit.json"
Assert-False $downloadAudit.filesDownloaded "Files downloaded."
Assert-False $downloadAudit.networkDownloadAttempted "Network download attempted."

$validationAudit = Read-JsonArtifact "phase-exec-sim-r036-no-validation-import-backtest-audit.json"
Assert-False $validationAudit.quoteRowsValidated "Quote rows validated."
Assert-False $validationAudit.quoteRowsImportedIntoDb "Quote rows imported."
Assert-False $validationAudit.persistedSanitizedRowsCreated "Persisted sanitized rows created."
Assert-False $validationAudit.simulationExecuted "Simulation executed."
Assert-False $validationAudit.backtestExecuted "Backtest executed."

$tcaAudit = Read-JsonArtifact "phase-exec-sim-r036-no-tca-result-lines-audit.json"
Assert-False $tcaAudit.tcaResultLinesProduced "TCA result lines produced."
Assert-False $tcaAudit.newTcaResultLinesCreated "New TCA result lines created."

$polygonAudit = Read-JsonArtifact "phase-exec-sim-r036-no-polygon-api-call-audit.json"
Assert-False $polygonAudit.polygonApiCalled "Polygon API called."

$lmaxAudit = Read-JsonArtifact "phase-exec-sim-r036-no-lmax-call-audit.json"
Assert-False $lmaxAudit.lmaxCalled "LMAX called."
Assert-False $lmaxAudit.marketDataRequestSent "MarketDataRequest sent."
Assert-False $lmaxAudit.marketDataResponseRead "MarketDataResponse read."
Assert-False $lmaxAudit.brokerActivationPerformed "Broker activation performed."

$externalAudit = Read-JsonArtifact "phase-exec-sim-r036-no-external-api-call-audit.json"
Assert-False $externalAudit.externalApiCalled "External API called."
Assert-False $externalAudit.polygonApiCalled "Polygon API called."
Assert-False $externalAudit.lmaxCalled "LMAX called."

$runtimeAudit = Read-JsonArtifact "phase-exec-sim-r036-no-broker-marketdata-runtime-audit.json"
Assert-False $runtimeAudit.brokerActivationPerformed "Broker activation performed."
Assert-False $runtimeAudit.socketOpened "Socket opened."
Assert-False $runtimeAudit.tlsOpened "TLS opened."
Assert-False $runtimeAudit.fixOpened "FIX opened."
Assert-False $runtimeAudit.marketDataRuntimeStarted "MarketData runtime started."
Assert-False $runtimeAudit.marketDataRequestSent "MarketDataRequest sent."
Assert-False $runtimeAudit.marketDataResponseRead "MarketDataResponse read."
Assert-False $runtimeAudit.apiWorkerSchedulerServiceStarted "API/Worker/Scheduler/Service started."
Assert-False $runtimeAudit.timerPollingBackgroundJobIntroduced "Timer/polling/background job introduced."

$orderAudit = Read-JsonArtifact "phase-exec-sim-r036-no-order-fill-report-route-audit.json"
Assert-False $orderAudit.executableSchedulesCreated "Executable schedules created."
Assert-False $orderAudit.childSlicesCreated "Child slices created."
Assert-False $orderAudit.childOrdersCreated "Child orders created."
Assert-False $orderAudit.ordersCreated "Orders created."
Assert-False $orderAudit.ordersSubmitted "Orders submitted."
Assert-False $orderAudit.fillsCreated "Fills created."
Assert-False $orderAudit.executionReportsCreated "Execution reports created."
Assert-False $orderAudit.routesCreated "Routes created."
Assert-False $orderAudit.submissionsCreated "Submissions created."
Assert-False $orderAudit.stateMutated "State mutated."
Assert-False $orderAudit.paperLedgerCommitted "Paper ledger committed."

$usdjpy = Read-JsonArtifact "phase-exec-sim-r036-usdjpy-caveat-preservation.json"
if ($usdjpy.normalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.executionTradableSymbol -ne "USDJPY" -or -not $usdjpy.requiresInversion -or $usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8") {
    throw "USDJPY caveat weakened."
}
Assert-False $usdjpy.caveatWeakened "USDJPY caveat weakened."

$lmax = Read-JsonArtifact "phase-exec-sim-r036-lmax-readonly-baseline-reference.json"
Assert-True $lmax.lmaxReferenceOnly "LMAX reference-only missing."
Assert-False $lmax.lmaxCalledInR036 "LMAX called in R036."
Assert-False $lmax.audusdMisclassifiedAsFailed "AUDUSD misclassified as failed."

$noExternal = Read-JsonArtifact "phase-exec-sim-r036-no-external-audit.json"
Assert-True $noExternal.noExternal "No-external missing."
Assert-False $noExternal.polygonApiCalled "Polygon API called."
Assert-False $noExternal.lmaxCalled "LMAX called."
Assert-False $noExternal.externalApiCalled "External API called."
Assert-False $noExternal.filesDownloaded "Files downloaded."

$forbidden = Read-JsonArtifact "phase-exec-sim-r036-forbidden-actions-audit.json"
Assert-False $forbidden.forbiddenActionsDetected "Forbidden actions detected."
Assert-False $forbidden.pythonOrScriptExecuted "Python/script executed."
Assert-False $forbidden.qubesExecutableRun "Qubes executable run."
Assert-False $forbidden.cppCudaWorkloadRun "C++/CUDA workload run."
Assert-False $forbidden.validationImportBacktestExecuted "Validation/import/backtest executed."
Assert-False $forbidden.tcaResultLinesProduced "TCA result lines produced."
Assert-False $forbidden.orderOrTradingPathIntroduced "Order/trading path introduced."
Assert-False $forbidden.stateMutated "State mutated."

$evidence = Read-JsonArtifact "phase-exec-sim-r036-build-test-validator-evidence.json"
if ($evidence.dotnetBuildNoRestore.status -notlike "PASS*") { throw "Build/test/validator evidence missing: dotnet build." }
if ($evidence.focusedR036Tests.status -notlike "PASS*") { throw "Build/test/validator evidence missing: focused R036 tests." }
if ($evidence.unitTests.status -notlike "PASS*") { throw "Build/test/validator evidence missing: unit tests." }

Write-Host "EXEC_SIM_R036_PASS_OPERATOR_SESSION_CONFIRMATION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R036_PASS_TIMESTAMP_SEMANTICS_CONFIRMED_NO_EXTERNAL"
Write-Host "EXEC_SIM_R036_PASS_NO_DOWNLOAD_NO_BACKTEST_GATE_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R036_NEEDS_OPERATOR_DATE_RANGES_NO_EXTERNAL"
