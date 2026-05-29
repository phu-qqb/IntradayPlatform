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
    "phase-exec-sim-r037-summary.md",
    "phase-exec-sim-r037-r036-confirmation-reference.json",
    "phase-exec-sim-r037-session-window-derivation-contract.json",
    "phase-exec-sim-r037-legacy-timestamp-transformation.json",
    "phase-exec-sim-r037-dominant-pattern-window-derivation.json",
    "phase-exec-sim-r037-target-execution-close-derivation.json",
    "phase-exec-sim-r037-standard-quote-alignment-derivation.json",
    "phase-exec-sim-r037-america-new-york-timezone-derivation.json",
    "phase-exec-sim-r037-dst-handling-report.json",
    "phase-exec-sim-r037-derived-local-session-window-candidates.json",
    "phase-exec-sim-r037-derived-utc-session-window-candidates.json",
    "phase-exec-sim-r037-canonical-session-candidate.json",
    "phase-exec-sim-r037-partial-day-exclusion-application.json",
    "phase-exec-sim-r037-operator-confirmation-needed.json",
    "phase-exec-sim-r037-needs-date-ranges.json",
    "phase-exec-sim-r037-weekdays-only-preservation.json",
    "phase-exec-sim-r037-trade-all-15m-bars-preservation.json",
    "phase-exec-sim-r037-no-overnight-preservation.json",
    "phase-exec-sim-r037-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r037-cost-guidance-preservation.json",
    "phase-exec-sim-r037-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r037-no-python-execution-audit.json",
    "phase-exec-sim-r037-no-script-execution-audit.json",
    "phase-exec-sim-r037-no-qubes-executable-run-audit.json",
    "phase-exec-sim-r037-no-cpp-cuda-run-audit.json",
    "phase-exec-sim-r037-no-download-audit.json",
    "phase-exec-sim-r037-no-validation-import-backtest-audit.json",
    "phase-exec-sim-r037-no-tca-result-lines-audit.json",
    "phase-exec-sim-r037-no-polygon-api-call-audit.json",
    "phase-exec-sim-r037-no-lmax-call-audit.json",
    "phase-exec-sim-r037-no-external-api-call-audit.json",
    "phase-exec-sim-r037-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r037-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r037-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r037-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r037-no-external-audit.json",
    "phase-exec-sim-r037-forbidden-actions-audit.json",
    "phase-exec-sim-r037-next-phase-recommendation.json",
    "phase-exec-sim-r037-build-test-validator-evidence.json"
)

foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $file))) { throw "Missing required R037 artifact: $file" }
}

$ref = Read-JsonArtifact "phase-exec-sim-r037-r036-confirmation-reference.json"
if ($ref.confirmedTimezone -ne "America/New_York") { throw "R036 New York confirmation not referenced." }
if ($ref.legacyCloseTimestampConvention -ne "OffsetSixMinuteCloseLabels") { throw "Legacy close convention not referenced." }
if ($ref.targetExecutionCloseLegacyRule -ne "AggregatedWeightsTimestamp + 15 minutes") { throw "Target execution close rule not referenced." }
if ($ref.standardCloseAlignmentRule -ne "LegacyCloseTimestamp - 6 minutes") { throw "Standard close alignment rule not referenced." }
if ($ref.targetExecutionStandardCloseAlignmentRule -ne "AggregatedWeightsTimestamp + 9 minutes") { throw "Target standard close rule not referenced." }
if ($ref.strategySessionPattern -ne "SingleSessionPerStrategy") { throw "SingleSessionPerStrategy not referenced." }
Assert-True $ref.r037NoExternal "R037 no-external missing."
Assert-True $ref.r037NoExecution "R037 no-execution missing."

$contract = Read-JsonArtifact "phase-exec-sim-r037-session-window-derivation-contract.json"
if ($contract.confirmedTimezone -ne "America/New_York") { throw "Derivation contract timezone missing." }
if ($contract.legacyCloseTimestampConvention -ne "OffsetSixMinuteCloseLabels") { throw "Derivation contract legacy convention missing." }
if ($contract.authorizationStatus -ne "MultipleCandidateWindowsNeedOperatorConfirmationNoExternal") { throw "R037 must not authorize a single canonical session." }
Assert-True $contract.needsOperatorConfirmation "Operator confirmation should be needed."
Assert-True $contract.needsOperatorDateRanges "Date ranges should still be needed."
Assert-True $contract.noDownloadsAuthorized "Downloads authorized unexpectedly."
Assert-True $contract.noBacktestAuthorized "Backtest authorized unexpectedly."

$transform = Read-JsonArtifact "phase-exec-sim-r037-legacy-timestamp-transformation.json"
if ($transform.legacyCloseTimestampRule -ne "LegacyCloseTimestamp = AggregatedWeightsTimestamp") { throw "Legacy close timestamp rule missing." }
if ($transform.standardCloseAlignmentRule -ne "StandardCloseAlignmentTimestamp = LegacyCloseTimestamp - 6 minutes") { throw "Standard close alignment missing." }
if ($transform.targetExecutionCloseLegacyRule -ne "TargetExecutionCloseLegacy = AggregatedWeightsTimestamp + 15 minutes") { throw "Target execution close legacy missing." }
if ($transform.targetExecutionStandardCloseAlignmentRule -ne "TargetExecutionStandardCloseAlignment = AggregatedWeightsTimestamp + 9 minutes") { throw "Target execution standard close missing." }
Assert-True $transform.doNotReplaceLegacyCloseLabelWithStandardClose "Legacy labels may not be replaced."
Assert-True $transform.standardAlignmentIsTechnicalOnly "Standard alignment must be technical only."

$patterns = Read-JsonArtifact "phase-exec-sim-r037-dominant-pattern-window-derivation.json"
if (@($patterns.dominantPatterns).Count -ne 3) { throw "Expected three dominant pattern derivations." }
if (-not (@($patterns.dominantPatterns) | Where-Object { $_.legacyWeightTimestampWindow -eq "13:36-19:51" -and $_.targetExecutionStandardCloseAlignmentWindow -eq "13:45-20:00" })) { throw "13:36 pattern derivation missing." }
if (-not (@($patterns.dominantPatterns) | Where-Object { $_.legacyWeightTimestampWindow -eq "14:36-20:51" -and $_.targetExecutionStandardCloseAlignmentWindow -eq "14:45-21:00" })) { throw "14:36 pattern derivation missing." }
if (-not (@($patterns.dominantPatterns) | Where-Object { $_.legacyWeightTimestampWindow -eq "14:21-20:51" -and $_.targetExecutionStandardCloseAlignmentWindow -eq "14:30-21:00" })) { throw "14:21 pattern derivation missing." }

$target = Read-JsonArtifact "phase-exec-sim-r037-target-execution-close-derivation.json"
if ($target.targetExecutionCloseLegacyRule -ne "TargetExecutionCloseLegacy = AggregatedWeightsTimestamp + 15 minutes") { throw "Target close derivation missing." }
if ($target.targetExecutionStandardCloseAlignmentRule -ne "TargetExecutionStandardCloseAlignment = AggregatedWeightsTimestamp + 9 minutes") { throw "Target standard close derivation missing." }

$align = Read-JsonArtifact "phase-exec-sim-r037-standard-quote-alignment-derivation.json"
if ($align.standardCloseAlignmentRule -ne "StandardCloseAlignmentTimestamp = LegacyCloseTimestamp - 6 minutes") { throw "Standard alignment derivation missing." }
Assert-True $align.doNotReplaceLegacyCloseLabel "Standard alignment replaced legacy close label."

$tz = Read-JsonArtifact "phase-exec-sim-r037-america-new-york-timezone-derivation.json"
if ($tz.confirmedTimezone -ne "America/New_York") { throw "America/New_York derivation missing." }
Assert-True $tz.dstBehaviorPreserved "DST behavior not preserved."
Assert-False $tz.timezoneAssumedWithoutEvidence "Timezone assumed without evidence."

$dst = Read-JsonArtifact "phase-exec-sim-r037-dst-handling-report.json"
Assert-True $dst.observedDateRangeSpansDstTransition "DST transition should be detected."
Assert-False $dst.singleFixedUtcWindowValidForAllObservedDates "Single fixed UTC window must not be valid."
Assert-True $dst.derivePerDateUtcWindows "Per-date UTC windows should be required."

$local = Read-JsonArtifact "phase-exec-sim-r037-derived-local-session-window-candidates.json"
if (@($local.localCandidateWindows).Count -ne 3) { throw "Local candidate windows missing." }
if ($local.canonicalCandidateSelectionStatus -ne "NeedsOperatorCanonicalSessionConfirmation") { throw "Canonical local selection should need confirmation." }

$utc = Read-JsonArtifact "phase-exec-sim-r037-derived-utc-session-window-candidates.json"
if (@($utc.utcCandidateWindows).Count -ne 3) { throw "UTC candidate windows missing." }
Assert-False $utc.singleFixedUtcWindowAuthorized "Single fixed UTC window authorized unexpectedly."
if ($utc.status -ne "ConfirmedSessionWindowUtcDerivationReadyNoExternal") { throw "UTC derivation status missing." }

$canonical = Read-JsonArtifact "phase-exec-sim-r037-canonical-session-candidate.json"
Assert-False $canonical.canonicalSessionAuthorized "Canonical session authorized despite multiple candidates."
Assert-True $canonical.needsOperatorConfirmation "Canonical confirmation should be required."
if ($canonical.canonicalSessionCandidateStatus -ne "MultipleCandidateWindowsNeedOperatorConfirmationNoExternal") { throw "Canonical candidate status missing." }

$partial = Read-JsonArtifact "phase-exec-sim-r037-partial-day-exclusion-application.json"
Assert-True $partial.partialDaysExcludedFromCanonicalSessionDerivation "Partial days not excluded."
Assert-True $partial.partialDaysRequireManualReview "Partial days should require manual review."

$confirm = Read-JsonArtifact "phase-exec-sim-r037-operator-confirmation-needed.json"
Assert-True $confirm.needsOperatorCanonicalSessionConfirmation "Canonical session confirmation missing."

$dates = Read-JsonArtifact "phase-exec-sim-r037-needs-date-ranges.json"
Assert-True $dates.needsOperatorDateRanges "Date ranges should be needed."
Assert-False $dates.dateRangesInvented "Date ranges invented."
if (@($dates.requiredSymbols).Count -ne 7) { throw "Required symbols missing." }

$weekdays = Read-JsonArtifact "phase-exec-sim-r037-weekdays-only-preservation.json"
Assert-True $weekdays.weekdaysOnly "WeekdaysOnly missing."
Assert-False $weekdays.weakened "WeekdaysOnly weakened."

$bars = Read-JsonArtifact "phase-exec-sim-r037-trade-all-15m-bars-preservation.json"
Assert-True $bars.tradeAll15mBars "TradeAll15mBars missing."
if ($bars.barIntervalMinutes -ne 15) { throw "Bar interval must be 15." }
Assert-False $bars.weakened "TradeAll15mBars weakened."

$overnight = Read-JsonArtifact "phase-exec-sim-r037-no-overnight-preservation.json"
Assert-True $overnight.mustEndFlat "MustEndFlat missing."
Assert-False $overnight.overnightAllowed "OvernightAllowed=false missing."
Assert-False $overnight.weakened "No-overnight weakened."

$direct = Read-JsonArtifact "phase-exec-sim-r037-direct-cross-exclusion-preservation.json"
Assert-True $direct.directCrossesSignalOnly "Direct-cross signal-only missing."
Assert-True $direct.nettingFirst "Netting-first missing."
Assert-True $direct.directCrossExecutionDisabled "Direct-cross disabled missing."
Assert-False $direct.weakened "Direct-cross exclusion weakened."

$cost = Read-JsonArtifact "phase-exec-sim-r037-cost-guidance-preservation.json"
Assert-True $cost.fiveUsdPerMillionBestCaseMajorOnly "5 USD/million best-case major-only missing."
Assert-False $cost.fiveUsdPerMillionUniversalized "5 USD/million universalized."

$nonmajor = Read-JsonArtifact "phase-exec-sim-r037-nonmajor-calibration-preservation.json"
Assert-True $nonmajor.nonmajorEmScandiCnhCalibrationRequired "Nonmajor calibration missing."
Assert-False $nonmajor.calibrationRequirementWeakened "Nonmajor calibration weakened."

$python = Read-JsonArtifact "phase-exec-sim-r037-no-python-execution-audit.json"
Assert-False $python.pythonCodeExecuted "Python executed."
Assert-False $python.notebooksExecuted "Notebook executed."

$script = Read-JsonArtifact "phase-exec-sim-r037-no-script-execution-audit.json"
Assert-False $script.scriptsExecuted "Scripts executed."

$qubes = Read-JsonArtifact "phase-exec-sim-r037-no-qubes-executable-run-audit.json"
Assert-False $qubes.qubesExecutableRun "Qubes executable run."

$cppCuda = Read-JsonArtifact "phase-exec-sim-r037-no-cpp-cuda-run-audit.json"
Assert-False $cppCuda.cppExecutableRun "C++ executable run."
Assert-False $cppCuda.cudaWorkloadRun "CUDA workload run."

$download = Read-JsonArtifact "phase-exec-sim-r037-no-download-audit.json"
Assert-False $download.filesDownloaded "Files downloaded."
Assert-False $download.networkDownloadAttempted "Network download attempted."

$validation = Read-JsonArtifact "phase-exec-sim-r037-no-validation-import-backtest-audit.json"
Assert-False $validation.quoteRowsValidated "Quote rows validated."
Assert-False $validation.quoteRowsImportedIntoDb "Quote rows imported."
Assert-False $validation.persistedSanitizedRowsCreated "Persisted sanitized rows created."
Assert-False $validation.simulationExecuted "Simulation executed."
Assert-False $validation.backtestExecuted "Backtest executed."

$tca = Read-JsonArtifact "phase-exec-sim-r037-no-tca-result-lines-audit.json"
Assert-False $tca.tcaResultLinesProduced "TCA result lines produced."
Assert-False $tca.newTcaResultLinesCreated "New TCA result lines created."

$polygon = Read-JsonArtifact "phase-exec-sim-r037-no-polygon-api-call-audit.json"
Assert-False $polygon.polygonApiCalled "Polygon API called."

$lmaxAudit = Read-JsonArtifact "phase-exec-sim-r037-no-lmax-call-audit.json"
Assert-False $lmaxAudit.lmaxCalled "LMAX called."
Assert-False $lmaxAudit.marketDataRequestSent "MarketDataRequest sent."
Assert-False $lmaxAudit.marketDataResponseRead "MarketDataResponse read."

$external = Read-JsonArtifact "phase-exec-sim-r037-no-external-api-call-audit.json"
Assert-False $external.externalApiCalled "External API called."
Assert-False $external.polygonApiCalled "Polygon API called."
Assert-False $external.lmaxCalled "LMAX called."

$runtime = Read-JsonArtifact "phase-exec-sim-r037-no-broker-marketdata-runtime-audit.json"
Assert-False $runtime.brokerActivationPerformed "Broker activation performed."
Assert-False $runtime.socketOpened "Socket opened."
Assert-False $runtime.tlsOpened "TLS opened."
Assert-False $runtime.fixOpened "FIX opened."
Assert-False $runtime.marketDataRuntimeStarted "MarketData runtime started."
Assert-False $runtime.apiWorkerSchedulerServiceStarted "API/Worker/Scheduler/Service started."
Assert-False $runtime.timerPollingBackgroundJobIntroduced "Timer/polling/background job introduced."

$orders = Read-JsonArtifact "phase-exec-sim-r037-no-order-fill-report-route-audit.json"
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

$usdjpy = Read-JsonArtifact "phase-exec-sim-r037-usdjpy-caveat-preservation.json"
if ($usdjpy.normalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.executionTradableSymbol -ne "USDJPY" -or -not $usdjpy.requiresInversion -or $usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8") {
    throw "USDJPY caveat weakened."
}
Assert-False $usdjpy.caveatWeakened "USDJPY caveat weakened."

$lmax = Read-JsonArtifact "phase-exec-sim-r037-lmax-readonly-baseline-reference.json"
Assert-True $lmax.lmaxReferenceOnly "LMAX reference-only missing."
Assert-False $lmax.lmaxCalledInR037 "LMAX called in R037."
Assert-False $lmax.audusdMisclassifiedAsFailed "AUDUSD misclassified as failed."

$noExternal = Read-JsonArtifact "phase-exec-sim-r037-no-external-audit.json"
Assert-True $noExternal.noExternal "No-external missing."
Assert-False $noExternal.polygonApiCalled "Polygon API called."
Assert-False $noExternal.lmaxCalled "LMAX called."
Assert-False $noExternal.externalApiCalled "External API called."
Assert-False $noExternal.filesDownloaded "Files downloaded."

$forbidden = Read-JsonArtifact "phase-exec-sim-r037-forbidden-actions-audit.json"
Assert-False $forbidden.forbiddenActionsDetected "Forbidden actions detected."
Assert-False $forbidden.pythonOrScriptExecuted "Python/script executed."
Assert-False $forbidden.qubesExecutableRun "Qubes executable run."
Assert-False $forbidden.cppCudaWorkloadRun "C++/CUDA workload run."
Assert-False $forbidden.validationImportBacktestExecuted "Validation/import/backtest executed."
Assert-False $forbidden.tcaResultLinesProduced "TCA result lines produced."
Assert-False $forbidden.orderOrTradingPathIntroduced "Order/trading path introduced."
Assert-False $forbidden.schedulerServicePollingBackgroundJobIntroduced "Scheduler/service/polling/background job introduced."
Assert-False $forbidden.stateMutated "State mutated."

$evidence = Read-JsonArtifact "phase-exec-sim-r037-build-test-validator-evidence.json"
if ($evidence.dotnetBuildNoRestore.status -notlike "PASS*") { throw "Build/test/validator evidence missing: dotnet build." }
if ($evidence.focusedR037Tests.status -notlike "PASS*") { throw "Build/test/validator evidence missing: focused R037 tests." }
if ($evidence.unitTests.status -notlike "PASS*") { throw "Build/test/validator evidence missing: unit tests." }

Write-Host "EXEC_SIM_R037_PASS_CONFIRMED_SESSION_WINDOW_UTC_DERIVATION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R037_PASS_LEGACY_TIMESTAMP_TRANSFORMATION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R037_PASS_NO_EXECUTION_NO_BACKTEST_GATE_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R037_NEEDS_OPERATOR_CANONICAL_SESSION_CONFIRMATION_NO_EXTERNAL"
Write-Host "EXEC_SIM_R037_NEEDS_OPERATOR_DATE_RANGES_NO_EXTERNAL"
