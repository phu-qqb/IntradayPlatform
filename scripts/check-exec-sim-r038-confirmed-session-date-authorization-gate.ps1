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
    "phase-exec-sim-r038-summary.md",
    "phase-exec-sim-r038-r037-derivation-reference.json",
    "phase-exec-sim-r038-confirmed-session-authorization-contract.json",
    "phase-exec-sim-r038-confirmed-session-authorization-result.json",
    "phase-exec-sim-r038-historical-date-range-authorization-contract.json",
    "phase-exec-sim-r038-historical-date-range-authorization-result.json",
    "phase-exec-sim-r038-date-weekday-validation.json",
    "phase-exec-sim-r038-derived-utc-session-windows.json",
    "phase-exec-sim-r038-derived-session-bar-structure.json",
    "phase-exec-sim-r038-dst-handling-report.json",
    "phase-exec-sim-r038-download-planning-requirements.json",
    "phase-exec-sim-r038-future-file-naming-guidance.json",
    "phase-exec-sim-r038-future-authorization-entry-requirements.json",
    "phase-exec-sim-r038-legacy-compatibility-mapping-preservation.json",
    "phase-exec-sim-r038-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-sim-r038-partial-day-policy-preservation.json",
    "phase-exec-sim-r038-weekdays-only-preservation.json",
    "phase-exec-sim-r038-trade-all-15m-bars-preservation.json",
    "phase-exec-sim-r038-no-overnight-preservation.json",
    "phase-exec-sim-r038-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r038-cost-guidance-preservation.json",
    "phase-exec-sim-r038-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r038-no-download-audit.json",
    "phase-exec-sim-r038-no-validation-import-backtest-audit.json",
    "phase-exec-sim-r038-no-tca-result-lines-audit.json",
    "phase-exec-sim-r038-no-polygon-api-call-audit.json",
    "phase-exec-sim-r038-no-lmax-call-audit.json",
    "phase-exec-sim-r038-no-external-api-call-audit.json",
    "phase-exec-sim-r038-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r038-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r038-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r038-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r038-no-external-audit.json",
    "phase-exec-sim-r038-forbidden-actions-audit.json",
    "phase-exec-sim-r038-next-phase-recommendation.json",
    "phase-exec-sim-r038-build-test-validator-evidence.json"
)

foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $file))) { throw "Missing required R038 artifact: $file" }
}

$expectedDates = @("2025-10-14", "2025-10-15", "2025-10-16", "2025-10-17", "2025-10-20")
$expectedUtc = @{
    "2025-10-14" = @("2025-10-14T18:15:00Z", "2025-10-15T01:00:00Z")
    "2025-10-15" = @("2025-10-15T18:15:00Z", "2025-10-16T01:00:00Z")
    "2025-10-16" = @("2025-10-16T18:15:00Z", "2025-10-17T01:00:00Z")
    "2025-10-17" = @("2025-10-17T18:15:00Z", "2025-10-18T01:00:00Z")
    "2025-10-20" = @("2025-10-20T18:15:00Z", "2025-10-21T01:00:00Z")
}

$ref = Read-JsonArtifact "phase-exec-sim-r038-r037-derivation-reference.json"
if ($ref.sourcePhase -ne "EXEC-SIM-R037") { throw "R037 reference missing." }
if ($ref.confirmedTimezone -ne "America/New_York") { throw "R037 New York confirmation not referenced." }
if ($ref.legacyCloseTimestampConvention -ne "OffsetSixMinuteCloseLabels") { throw "Legacy close timestamp convention reference missing." }
if ($ref.standardCloseAlignmentRule -ne "LegacyCloseTimestamp - 6 minutes") { throw "Standard close alignment reference missing." }
if ($ref.legacyNextBarExecutionCloseCanonicalRule -ne "LegacyOutputTimestamp + 9 minutes") { throw "Legacy next-bar canonical rule reference missing." }
Assert-True $ref.legacyCompatibilityOnly "Legacy compatibility-only reference missing."
Assert-True $ref.dstAwareConversionRequired "DST-aware conversion requirement missing."
Assert-True $ref.noExternal "R037 no-external reference missing."

$sessionContract = Read-JsonArtifact "phase-exec-sim-r038-confirmed-session-authorization-contract.json"
if ($sessionContract.confirmedTimezone -ne "America/New_York") { throw "Confirmed session timezone missing." }
if ($sessionContract.canonicalSessionOpenLocal -ne "14:15") { throw "Canonical session open missing." }
if ($sessionContract.canonicalSessionCloseLocal -ne "21:00") { throw "Canonical session close missing." }
if (@($sessionContract.canonicalCloseMinuteSet) -join "," -ne "00,15,30,45") { throw "Canonical close minute set missing." }
if ($sessionContract.barIntervalMinutes -ne 15) { throw "Bar interval must be 15." }
Assert-True $sessionContract.weekdaysOnly "WeekdaysOnly missing."
Assert-True $sessionContract.tradeAll15mBars "TradeAll15mBars missing."
Assert-True $sessionContract.mustEndFlat "MustEndFlat missing."
Assert-False $sessionContract.overnightAllowed "OvernightAllowed must be false."
if ($sessionContract.partialDayPolicy -ne "ExcludeAndManualReview") { throw "Partial-day policy missing." }
if ($sessionContract.authorizationStatus -ne "ConfirmedSessionAuthorizedNoExternal") { throw "Confirmed session authorization missing." }
Assert-True $sessionContract.noDownload "NoDownload missing."
Assert-True $sessionContract.noBacktest "NoBacktest missing."

$sessionResult = Read-JsonArtifact "phase-exec-sim-r038-confirmed-session-authorization-result.json"
Assert-True $sessionResult.confirmedSessionAuthorized "Confirmed session not authorized."
if ($sessionResult.authorizationStatus -ne "ConfirmedSessionAuthorizedNoExternal") { throw "Confirmed session result status missing." }
Assert-True $sessionResult.canonicalQuarterHourPolicyPreserved "Canonical quarter-hour policy not preserved."

$dateContract = Read-JsonArtifact "phase-exec-sim-r038-historical-date-range-authorization-contract.json"
if (@($dateContract.confirmedDates).Count -ne 5) { throw "Expected exactly five confirmed dates." }
foreach ($date in $expectedDates) {
    if ($date -notin @($dateContract.confirmedDates)) { throw "Missing operator-supplied date: $date" }
}
foreach ($date in @($dateContract.confirmedDates)) {
    if ($date -notin $expectedDates) { throw "Invented date detected: $date" }
}
if (@($dateContract.requiredSymbols).Count -ne 7) { throw "Seven required symbols not preserved." }
if (@($dateContract.requiredSessionWindowCategories) -join "," -ne "OpeningBuild,IntradayRebalance,ClosingFlatten") { throw "Required session categories missing." }
Assert-True $dateContract.requiresOperatorDownload "RequiresOperatorDownload missing."
Assert-True $dateContract.noDownload "NoDownload missing in date contract."
Assert-True $dateContract.noBacktest "NoBacktest missing in date contract."
if ($dateContract.authorizationStatus -ne "HistoricalDateRangesAuthorizedNoExternal") { throw "Historical date range authorization status missing." }

$dateResult = Read-JsonArtifact "phase-exec-sim-r038-historical-date-range-authorization-result.json"
Assert-True $dateResult.historicalDateRangesAuthorized "Historical date ranges not authorized."
Assert-False $dateResult.datesInvented "Dates invented."
if ($dateResult.authorizationStatus -ne "HistoricalDateRangesAuthorizedNoExternal") { throw "Historical date range result status missing." }

$weekday = Read-JsonArtifact "phase-exec-sim-r038-date-weekday-validation.json"
if ($weekday.dateValidationStatus -ne "AllSuppliedDatesAreWeekdays") { throw "Weekday validation did not pass." }
if (@($weekday.suppliedDates | Where-Object { -not $_.isWeekday }).Count -ne 0) { throw "Not all dates are weekdays." }
if (@($weekday.weekendDatesFound).Count -ne 0) { throw "Weekend date detected." }
Assert-False $weekday.datesInvented "Weekday validation invented dates."

$utc = Read-JsonArtifact "phase-exec-sim-r038-derived-utc-session-windows.json"
if ($utc.confirmedTimezone -ne "America/New_York") { throw "UTC windows timezone missing." }
Assert-True $utc.timezoneAwareConversionUsed "Timezone-aware conversion missing."
if (@($utc.derivedWindows).Count -ne 5) { throw "Expected five derived UTC windows." }
foreach ($window in @($utc.derivedWindows)) {
    if ($window.localSessionDate -notin $expectedDates) { throw "Unexpected UTC window date: $($window.localSessionDate)" }
    $pair = $expectedUtc[$window.localSessionDate]
    if ($window.utcSessionStart -ne $pair[0] -or $window.utcSessionEnd -ne $pair[1]) { throw "Unexpected UTC window for $($window.localSessionDate)." }
    if ($window.localTimezone -ne "America/New_York") { throw "Window timezone missing." }
    if ($window.dstOffset -ne "EDT UTC-04:00") { throw "Unexpected DST offset for supplied October 2025 date." }
    Assert-True $window.utcEndDateMayDiffer "UTC end date should differ for these windows."
    if ($window.expected15mBarCount -ne 27) { throw "Expected 27 fifteen-minute bars." }
    if ($window.firstCanonicalBarCloseLocal -notlike "*T14:30:00") { throw "First canonical close should be 14:30 local." }
    if ($window.lastCanonicalBarCloseLocal -notlike "*T21:00:00") { throw "Last canonical close should be 21:00 local." }
}
if ($utc.status -ne "UtcWindowsDerivedNoExternal") { throw "UTC window derivation status missing." }

$bars = Read-JsonArtifact "phase-exec-sim-r038-derived-session-bar-structure.json"
if ($bars.firstCanonicalBarCloseLocal -ne "14:30") { throw "First canonical bar close local missing." }
if ($bars.lastCanonicalBarCloseLocal -ne "21:00") { throw "Last canonical bar close local missing." }
if ($bars.expected15mBarCount -ne 27) { throw "Bar count must be 27." }
if (@($bars.canonicalCloseMinuteSet) -join "," -ne "00,15,30,45") { throw "Expected canonical close minutes missing." }
if (@($bars.expectedCanonical15mCloseSequenceLocal).Count -ne 27) { throw "Expected canonical close sequence missing." }

$dst = Read-JsonArtifact "phase-exec-sim-r038-dst-handling-report.json"
if ($dst.confirmedTimezone -ne "America/New_York") { throw "DST report timezone missing." }
Assert-True $dst.dstAwareConversionUsed "DST-aware conversion missing."
Assert-False $dst.offsetHardCoded "UTC offset hard-coded."
if ($dst.expectedOffsetForSuppliedDates -ne "EDT UTC-04:00") { throw "Supplied date offset should be EDT UTC-04:00." }

$download = Read-JsonArtifact "phase-exec-sim-r038-download-planning-requirements.json"
if ($download.providerName -ne "PolygonOfflineFile") { throw "Provider requirement missing." }
if ($download.providerDatasetType -ne "HistoricalBboQuotes") { throw "Dataset requirement missing." }
if ($download.fileFormat -ne "NDJSON") { throw "File format requirement missing." }
if (@($download.requiredSymbols).Count -ne 7) { throw "Download planning must keep seven symbols." }
if (@($download.confirmedDates).Count -ne 5) { throw "Download planning must keep five dates." }
Assert-True $download.requiresOperatorRunDownload "Operator-run download requirement missing."
Assert-True $download.noDownloadExecuted "Download executed unexpectedly."
Assert-True $download.readyForOfflineDownloadPlanning "Download planning readiness missing."
Assert-True $download.needsOperatorFileDownload "Needs operator file download missing."
Assert-True $download.noFilePathsCreatedAsExisting "R038 must not authorize existing file paths."

$names = Read-JsonArtifact "phase-exec-sim-r038-future-file-naming-guidance.json"
Assert-True $names.guidanceOnly "File naming guidance must be guidance only."
Assert-False $names.concreteFilesCreated "Concrete files created."
Assert-False $names.concretePathsAssertedToExist "Concrete paths asserted to exist."

$authReq = Read-JsonArtifact "phase-exec-sim-r038-future-authorization-entry-requirements.json"
Assert-False $authReq.filePathsProvidedNow "Future auth must not provide file paths now."
Assert-False $authReq.authorizationEntriesCreatedNow "Future auth entries must not be created now."
if (-not (@($authReq.requiredFieldsForFutureFileAuthorization) -contains "ManifestPath")) { throw "Future manifest requirements missing." }

$legacy = Read-JsonArtifact "phase-exec-sim-r038-legacy-compatibility-mapping-preservation.json"
Assert-True $legacy.legacyOutputTimestampCompatibilityOnly "Legacy output timestamp must be compatibility-only."
if ($legacy.canonicalCloseForLegacyMatchRule -ne "LegacyOutputTimestamp - 6 minutes") { throw "Legacy match alignment rule missing." }
if ($legacy.legacyNextBarExecutionCloseCanonicalRule -ne "LegacyOutputTimestamp + 9 minutes") { throw "Legacy next-bar canonical rule missing." }
Assert-False $legacy.legacy06UsedAsFutureCanonical "Legacy :06 used as future canonical."

$quarter = Read-JsonArtifact "phase-exec-sim-r038-canonical-quarter-hour-policy-preservation.json"
Assert-True $quarter.futurePmsQubesExecutionTcaTimestampsUseCanonicalQuarterHourCloses "Future canonical quarter-hour closes missing."
Assert-False $quarter.legacy06UsedAsFutureCanonical "Legacy :06 used as future canonical."
Assert-True $quarter.preserved "Canonical quarter-hour preservation missing."

$partial = Read-JsonArtifact "phase-exec-sim-r038-partial-day-policy-preservation.json"
if ($partial.partialDayPolicy -ne "ExcludeAndManualReview") { throw "Partial-day policy missing." }
Assert-True $partial.partialDaysExcludedFromCanonicalDerivation "Partial days not excluded."
Assert-True $partial.partialDaysRequireManualReview "Partial days should require manual review."

$weekdays = Read-JsonArtifact "phase-exec-sim-r038-weekdays-only-preservation.json"
Assert-True $weekdays.weekdaysOnly "WeekdaysOnly missing."
Assert-False $weekdays.weakened "WeekdaysOnly weakened."

$tradeBars = Read-JsonArtifact "phase-exec-sim-r038-trade-all-15m-bars-preservation.json"
Assert-True $tradeBars.tradeAll15mBars "TradeAll15mBars missing."
if ($tradeBars.barIntervalMinutes -ne 15) { throw "Bar interval must be 15." }
Assert-False $tradeBars.weakened "TradeAll15mBars weakened."

$overnight = Read-JsonArtifact "phase-exec-sim-r038-no-overnight-preservation.json"
Assert-True $overnight.mustEndFlat "MustEndFlat missing."
Assert-False $overnight.overnightAllowed "OvernightAllowed=false missing."
Assert-False $overnight.weakened "No-overnight weakened."

$direct = Read-JsonArtifact "phase-exec-sim-r038-direct-cross-exclusion-preservation.json"
Assert-True $direct.directCrossesSignalOnly "Direct-cross signal-only missing."
Assert-True $direct.nettingFirst "Netting-first missing."
Assert-True $direct.directCrossExecutionDisabled "Direct-cross disabled missing."
Assert-False $direct.weakened "Direct-cross exclusion weakened."

$cost = Read-JsonArtifact "phase-exec-sim-r038-cost-guidance-preservation.json"
Assert-True $cost.fiveUsdPerMillionBestCaseMajorOnly "5 USD/million best-case major-only missing."
Assert-False $cost.fiveUsdPerMillionUniversalized "5 USD/million universalized."

$nonmajor = Read-JsonArtifact "phase-exec-sim-r038-nonmajor-calibration-preservation.json"
Assert-True $nonmajor.nonmajorEmScandiCnhCalibrationRequired "Nonmajor calibration missing."
Assert-False $nonmajor.calibrationRequirementWeakened "Nonmajor calibration weakened."

$noDownload = Read-JsonArtifact "phase-exec-sim-r038-no-download-audit.json"
Assert-False $noDownload.filesDownloaded "Files downloaded."
Assert-False $noDownload.networkDownloadAttempted "Network download attempted."

$validation = Read-JsonArtifact "phase-exec-sim-r038-no-validation-import-backtest-audit.json"
Assert-False $validation.quoteRowsValidated "Quote rows validated."
Assert-False $validation.quoteRowsImportedIntoDb "Quote rows imported."
Assert-False $validation.persistedSanitizedRowsCreated "Persisted sanitized rows created."
Assert-False $validation.simulationExecuted "Simulation executed."
Assert-False $validation.backtestExecuted "Backtest executed."

$tca = Read-JsonArtifact "phase-exec-sim-r038-no-tca-result-lines-audit.json"
Assert-False $tca.tcaResultLinesProduced "TCA result lines produced."
Assert-False $tca.newTcaResultLinesCreated "New TCA result lines created."

$polygon = Read-JsonArtifact "phase-exec-sim-r038-no-polygon-api-call-audit.json"
Assert-False $polygon.polygonApiCalled "Polygon API called."

$lmaxAudit = Read-JsonArtifact "phase-exec-sim-r038-no-lmax-call-audit.json"
Assert-False $lmaxAudit.lmaxCalled "LMAX called."
Assert-False $lmaxAudit.marketDataRequestSent "MarketDataRequest sent."
Assert-False $lmaxAudit.marketDataResponseRead "MarketDataResponse read."

$external = Read-JsonArtifact "phase-exec-sim-r038-no-external-api-call-audit.json"
Assert-False $external.externalApiCalled "External API called."
Assert-False $external.polygonApiCalled "Polygon API called."
Assert-False $external.lmaxCalled "LMAX called."

$runtime = Read-JsonArtifact "phase-exec-sim-r038-no-broker-marketdata-runtime-audit.json"
Assert-False $runtime.brokerActivationPerformed "Broker activation performed."
Assert-False $runtime.socketOpened "Socket opened."
Assert-False $runtime.tlsOpened "TLS opened."
Assert-False $runtime.fixOpened "FIX opened."
Assert-False $runtime.marketDataRuntimeStarted "MarketData runtime started."
Assert-False $runtime.apiWorkerSchedulerServiceStarted "API/Worker/Scheduler/Service started."
Assert-False $runtime.timerPollingBackgroundJobIntroduced "Timer/polling/background job introduced."

$orders = Read-JsonArtifact "phase-exec-sim-r038-no-order-fill-report-route-audit.json"
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

$usdjpy = Read-JsonArtifact "phase-exec-sim-r038-usdjpy-caveat-preservation.json"
if ($usdjpy.normalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.executionTradableSymbol -ne "USDJPY" -or -not $usdjpy.requiresInversion -or $usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8") {
    throw "USDJPY caveat weakened."
}
Assert-False $usdjpy.caveatWeakened "USDJPY caveat weakened."

$lmax = Read-JsonArtifact "phase-exec-sim-r038-lmax-readonly-baseline-reference.json"
Assert-True $lmax.lmaxReferenceOnly "LMAX reference-only missing."
Assert-False $lmax.lmaxCalledInR038 "LMAX called in R038."
Assert-False $lmax.audusdMisclassifiedAsFailed "AUDUSD misclassified as failed."

$noExternal = Read-JsonArtifact "phase-exec-sim-r038-no-external-audit.json"
Assert-True $noExternal.noExternal "No-external missing."
Assert-False $noExternal.polygonApiCalled "Polygon API called."
Assert-False $noExternal.lmaxCalled "LMAX called."
Assert-False $noExternal.externalApiCalled "External API called."
Assert-False $noExternal.filesDownloaded "Files downloaded."

$forbidden = Read-JsonArtifact "phase-exec-sim-r038-forbidden-actions-audit.json"
Assert-False $forbidden.forbiddenActionsDetected "Forbidden actions detected."
Assert-False $forbidden.pythonOrScriptExecuted "Python/script executed."
Assert-False $forbidden.qubesExecutableRun "Qubes executable run."
Assert-False $forbidden.cppCudaWorkloadRun "C++/CUDA workload run."
Assert-False $forbidden.validationImportBacktestExecuted "Validation/import/backtest executed."
Assert-False $forbidden.tcaResultLinesProduced "TCA result lines produced."
Assert-False $forbidden.orderOrTradingPathIntroduced "Order/trading path introduced."
Assert-False $forbidden.schedulerServicePollingBackgroundJobIntroduced "Scheduler/service/polling/background job introduced."
Assert-False $forbidden.stateMutated "State mutated."

$evidence = Read-JsonArtifact "phase-exec-sim-r038-build-test-validator-evidence.json"
if ($evidence.dotnetBuildNoRestore.status -notlike "PASS*") { throw "Build/test/validator evidence missing: dotnet build." }
if ($evidence.focusedR038Tests.status -notlike "PASS*") { throw "Build/test/validator evidence missing: focused R038 tests." }
if ($evidence.unitTests.status -notlike "PASS*") { throw "Build/test/validator evidence missing: unit tests." }

Write-Host "EXEC_SIM_R038_PASS_CONFIRMED_SESSION_AUTHORIZATION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R038_PASS_HISTORICAL_DATE_RANGE_AUTHORIZATION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R038_PASS_UTC_WINDOW_DERIVATION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R038_PASS_NO_DOWNLOAD_NO_BACKTEST_GATE_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R038_NEEDS_OPERATOR_FILE_DOWNLOAD_NO_EXTERNAL"
