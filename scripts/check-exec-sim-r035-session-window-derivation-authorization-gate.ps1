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
    "phase-exec-sim-r035-summary.md",
    "phase-exec-sim-r035-r034-aggregated-weights-reference.json",
    "phase-exec-sim-r035-timestamp-semantics-analysis.json",
    "phase-exec-sim-r035-generation-lag-analysis.json",
    "phase-exec-sim-r035-effective-close-derivation.json",
    "phase-exec-sim-r035-derived-daily-session-windows.json",
    "phase-exec-sim-r035-dominant-session-patterns.json",
    "phase-exec-sim-r035-timezone-ambiguity-report.json",
    "phase-exec-sim-r035-config-candidate-comparison.json",
    "phase-exec-sim-r035-session-window-authorization-contract.json",
    "phase-exec-sim-r035-session-window-authorization-result.json",
    "phase-exec-sim-r035-needs-operator-confirmation.json",
    "phase-exec-sim-r035-needs-operator-date-ranges.json",
    "phase-exec-sim-r035-weekdays-only-preservation.json",
    "phase-exec-sim-r035-trade-all-15m-bars-preservation.json",
    "phase-exec-sim-r035-no-overnight-preservation.json",
    "phase-exec-sim-r035-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r035-cost-guidance-preservation.json",
    "phase-exec-sim-r035-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r035-no-python-execution-audit.json",
    "phase-exec-sim-r035-no-script-execution-audit.json",
    "phase-exec-sim-r035-no-qubes-executable-run-audit.json",
    "phase-exec-sim-r035-no-cpp-cuda-run-audit.json",
    "phase-exec-sim-r035-no-download-audit.json",
    "phase-exec-sim-r035-no-validation-import-backtest-audit.json",
    "phase-exec-sim-r035-no-tca-result-lines-audit.json",
    "phase-exec-sim-r035-no-polygon-api-call-audit.json",
    "phase-exec-sim-r035-no-lmax-call-audit.json",
    "phase-exec-sim-r035-no-external-api-call-audit.json",
    "phase-exec-sim-r035-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r035-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r035-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r035-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r035-no-external-audit.json",
    "phase-exec-sim-r035-forbidden-actions-audit.json",
    "phase-exec-sim-r035-next-phase-recommendation.json",
    "phase-exec-sim-r035-build-test-validator-evidence.json"
)

foreach ($file in $requiredFiles) {
    $path = Join-Path $artifactDir $file
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing required R035 artifact: $file"
    }
}

$reference = Read-JsonArtifact "phase-exec-sim-r035-r034-aggregated-weights-reference.json"
Assert-True $reference.reusedR034EvidenceOnly "R035 must reuse R034 evidence."
Assert-True $reference.r035NoExternal "R035 no-external flag missing."
Assert-True $reference.r035NoExecution "R035 no-execution flag missing."
if ($reference.timestampFormatDetected -ne "yyyyMMddHHmm") { throw "R034 timestamp format reference missing." }
if ($reference.cadenceDetected -ne "15m") { throw "R034 15m cadence reference missing." }
if ($reference.timezoneEvidenceFromR034 -ne "Ambiguous") { throw "R034 timezone ambiguity was not preserved." }

$semantics = Read-JsonArtifact "phase-exec-sim-r035-timestamp-semantics-analysis.json"
if ($semantics.dominantSemanticsCandidate -ne "ProducedAtTimestampMinus6Minutes") { throw "Dominant timestamp semantics must be produced-at minus 6 minutes." }
if ($semantics.status -ne "AggregatedWeightsTimestampSemanticsDerivedNoExternal") { throw "Timestamp semantics status missing." }
Assert-False $semantics.timezoneAssumed "R035 assumed timezone without evidence."
Assert-False $semantics.sessionTimesAuthorized "R035 authorized session times unexpectedly."
Assert-True $semantics.needsOperatorConfirmation "R035 semantics must need operator confirmation."
$lag6 = @($semantics.hypothesesEvaluated | Where-Object { $_.candidateGenerationLagMinutes -eq 6 })[0]
if ($null -eq $lag6) { throw "Generation lag 6 hypothesis missing." }
Assert-True $lag6.minuteAlignmentToStandard15mClose "Lag 6 must align to standard 15m closes."
if ($lag6.alignmentScore -ne 1.0) { throw "Lag 6 alignment score must be 1.0." }

$lag = Read-JsonArtifact "phase-exec-sim-r035-generation-lag-analysis.json"
if ($lag.dominantLagImpliedByMinutePattern -ne 6) { throw "Dominant lag must be 6 minutes." }
if ($lag.effectiveTimestampDerivationRule -ne "effectiveCloseTimestamp = AggregatedWeightsTimestamp - 6 minutes") { throw "Unexpected effective timestamp derivation rule." }
Assert-False $lag.dateRangesInvented "Date ranges invented in lag analysis."
Assert-False $lag.timezoneAssumed "Timezone assumed in lag analysis."

$effective = Read-JsonArtifact "phase-exec-sim-r035-effective-close-derivation.json"
if ($effective.dominantEffectiveTimestampDerivationRule -ne "effectiveCloseTimestamp = aggregatedWeightsProducedAtTimestamp - 6 minutes") { throw "Effective close derivation rule missing." }
Assert-False $effective.exactSessionAuthorized "Effective close derivation must not authorize exact session."
if (@($effective.derivedExamples | Where-Object { $_.candidateGenerationLagMinutes -eq 6 }).Count -lt 3) { throw "Required lag-6 derived examples missing." }

$windows = Read-JsonArtifact "phase-exec-sim-r035-derived-daily-session-windows.json"
if ($windows.derivationStatus -ne "LegacySessionWindowCandidatesDerivedNoExternal") { throw "Derived session window status missing." }
if ($windows.dominantLagMinutesUsedForPreferredCandidates -ne 6) { throw "Derived windows must use lag 6 for preferred candidates." }
if (@($windows.derivedCandidateWindows).Count -lt 3) { throw "Derived session windows missing." }
Assert-False $windows.timezoneAssumed "Timezone assumed in derived windows."
Assert-False $windows.sessionWindowsAuthorized "Session windows authorized unexpectedly."

$patterns = Read-JsonArtifact "phase-exec-sim-r035-dominant-session-patterns.json"
if ($patterns.preferredTimestampSemantics -ne "ProducedAtTimestampMinus6Minutes") { throw "Dominant session patterns must use lag 6 semantics." }
Assert-True $patterns.fullWeekday24hCandidateWeakened "Full 24h candidate must be weakened."
Assert-True $patterns.h1Or60mCandidateWeakened "H1/60m candidate must be weakened."
if ($patterns.authorizationStatus -ne "LegacySessionWindowNeedsOperatorConfirmationNoExternal") { throw "Dominant patterns must need operator confirmation." }

$timezone = Read-JsonArtifact "phase-exec-sim-r035-timezone-ambiguity-report.json"
if ($timezone.timezoneEvidence -ne "Ambiguous") { throw "Timezone ambiguity report must preserve Ambiguous." }
Assert-False $timezone.timezoneAssumed "Timezone was assumed."
Assert-False $timezone.utcAssumed "UTC assumed without evidence."
Assert-False $timezone.europeLondonAssumed "Europe/London assumed without evidence."
Assert-False $timezone.gmtStandardTimeAssumed "GMT Standard Time assumed without evidence."
if ($timezone.status -ne "NeedsOperatorTimezoneConfirmationNoExternal") { throw "Timezone confirmation status missing." }

$comparison = Read-JsonArtifact "phase-exec-sim-r035-config-candidate-comparison.json"
if ($comparison.bestConfigBackedCandidate -ne "Europe/London ny_overlap plus ny_late") { throw "Expected config-backed candidate comparison missing." }
Assert-False $comparison.timezoneAssumed "Timezone assumed in config candidate comparison."
Assert-True $comparison.needsOperatorConfirmation "Config candidate comparison must need operator confirmation."

$contract = Read-JsonArtifact "phase-exec-sim-r035-session-window-authorization-contract.json"
if ($contract.timestampSemanticsHypothesis -ne "ProducedAtTimestampMinus6Minutes") { throw "Authorization contract timestamp semantics missing." }
if ($contract.candidateGenerationLagMinutes -ne 6) { throw "Authorization contract lag must be 6." }
Assert-False ($null -ne $contract.sessionTimezone) "Authorization contract assumed timezone."
Assert-True $contract.weekdaysOnly "WeekdaysOnly=true missing."
Assert-True $contract.tradeAll15mBars "TradeAll15mBars=true missing."
Assert-True $contract.mustEndFlat "MustEndFlat=true missing."
Assert-False $contract.overnightAllowed "OvernightAllowed=false missing."
Assert-False $contract.exactSessionAuthorized "Exact session authorized without sufficient evidence."
Assert-False $contract.timezoneAssumedWithoutEvidence "Timezone assumed without evidence."
Assert-False $contract.dateRangesInvented "Date ranges invented."
if ($contract.sessionAuthorizationStatus -ne "LegacySessionWindowNeedsOperatorConfirmationNoExternal") { throw "Authorization contract status must need confirmation." }

$result = Read-JsonArtifact "phase-exec-sim-r035-session-window-authorization-result.json"
Assert-False $result.authorized "Session windows authorized unexpectedly."
Assert-True $result.nearAuthorization "R035 should record near-authorization candidate strength."
if ($result.authorizationStatus -ne "LegacySessionWindowNeedsOperatorConfirmationNoExternal") { throw "Authorization result must need confirmation." }
if ($result.timestampSemanticsStatus -ne "AggregatedWeightsTimestampSemanticsDerivedNoExternal") { throw "Timestamp semantics result missing." }
if ($result.candidateWindowStatus -ne "LegacySessionWindowCandidatesDerivedNoExternal") { throw "Candidate window result missing." }
Assert-False $result.timezoneAssumed "Timezone assumed in authorization result."
Assert-False $result.dateRangesInvented "Date ranges invented in authorization result."
Assert-True $result.needsOperatorSessionWindowConfirmation "Session window confirmation missing."
Assert-True $result.needsOperatorTimezoneConfirmation "Timezone confirmation missing."
Assert-True $result.needsOperatorDateRanges "Date range confirmation missing."
Assert-False $result.pythonExecuted "Python executed."
Assert-False $result.scriptsExecuted "Scripts executed."
Assert-False $result.qubesExecutableRun "Qubes executable run."
Assert-False $result.cppExecutableRun "C++ executable run."
Assert-False $result.cudaWorkloadRun "CUDA workload run."
Assert-False $result.downloadExecuted "Download executed."
Assert-False $result.validationImportBacktestExecuted "Validation/import/backtest executed."
Assert-False $result.tcaResultLinesProduced "TCA result lines produced."
Assert-False $result.ordersFillsReportsRoutesCreated "Orders/fills/reports/routes created."
Assert-False $result.stateMutated "State mutated."

$needsConfirmation = Read-JsonArtifact "phase-exec-sim-r035-needs-operator-confirmation.json"
Assert-True $needsConfirmation.needsOperatorConfirmation "Needs-operator-confirmation artifact must require confirmation."
Assert-False $needsConfirmation.exactSessionAuthorized "Needs-confirmation artifact must not authorize exact session."

$needsDates = Read-JsonArtifact "phase-exec-sim-r035-needs-operator-date-ranges.json"
Assert-True $needsDates.needsOperatorDateRanges "Needs date ranges missing."
Assert-False $needsDates.dateRangesInvented "Date ranges invented."

$weekdays = Read-JsonArtifact "phase-exec-sim-r035-weekdays-only-preservation.json"
Assert-True $weekdays.weekdaysOnly "WeekdaysOnly not preserved."
Assert-False $weekdays.weakened "WeekdaysOnly preservation weakened."

$bars = Read-JsonArtifact "phase-exec-sim-r035-trade-all-15m-bars-preservation.json"
Assert-True $bars.tradeAll15mBars "TradeAll15mBars not preserved."
if ($bars.barIntervalMinutes -ne 15) { throw "Bar interval must remain 15." }
Assert-False $bars.weakened "TradeAll15mBars weakened."

$overnight = Read-JsonArtifact "phase-exec-sim-r035-no-overnight-preservation.json"
Assert-True $overnight.mustEndFlat "MustEndFlat not preserved."
Assert-False $overnight.overnightAllowed "OvernightAllowed=false not preserved."
Assert-False $overnight.weakened "No-overnight preservation weakened."

$direct = Read-JsonArtifact "phase-exec-sim-r035-direct-cross-exclusion-preservation.json"
Assert-True $direct.directCrossesSignalOnly "Direct crosses not signal-only."
Assert-True $direct.nettingFirst "Netting-first not preserved."
Assert-True $direct.directCrossExecutionDisabled "Direct cross execution not disabled."
Assert-False $direct.weakened "Direct-cross exclusion weakened."

$cost = Read-JsonArtifact "phase-exec-sim-r035-cost-guidance-preservation.json"
Assert-True $cost.fiveUsdPerMillionBestCaseMajorOnly "5 USD/million best-case major-only missing."
Assert-False $cost.fiveUsdPerMillionUniversalized "5 USD/million universalized."
Assert-False $cost.weakened "Cost guidance weakened."

$nonmajor = Read-JsonArtifact "phase-exec-sim-r035-nonmajor-calibration-preservation.json"
Assert-True $nonmajor.nonmajorEmScandiCnhCalibrationRequired "Nonmajor calibration missing."
Assert-False $nonmajor.calibrationRequirementWeakened "Nonmajor calibration weakened."

$pythonAudit = Read-JsonArtifact "phase-exec-sim-r035-no-python-execution-audit.json"
Assert-False $pythonAudit.pythonCodeExecuted "Python executed."
Assert-False $pythonAudit.notebooksExecuted "Notebook executed."
Assert-False $pythonAudit.legacyQubesCodeExecuted "Legacy Qubes code executed."

$scriptAudit = Read-JsonArtifact "phase-exec-sim-r035-no-script-execution-audit.json"
Assert-False $scriptAudit.scriptsExecuted "Scripts executed."

$qubesAudit = Read-JsonArtifact "phase-exec-sim-r035-no-qubes-executable-run-audit.json"
Assert-False $qubesAudit.qubesExecutableRun "Qubes executable run."
Assert-False $qubesAudit.prodGenerateSignalsBinaryFilesRun "PRODgenerateSignalsBinaryFiles run."
Assert-False $qubesAudit.prodGenerateTimeSeriesRun "PRODgenerateTimeSeries run."
Assert-False $qubesAudit.prodManagerRun "PRODmanager run."
Assert-False $qubesAudit.prodAnubisRun "PRODAnubis run."

$cppCudaAudit = Read-JsonArtifact "phase-exec-sim-r035-no-cpp-cuda-run-audit.json"
Assert-False $cppCudaAudit.cppExecutableRun "C++ executable run."
Assert-False $cppCudaAudit.cudaWorkloadRun "CUDA workload run."
Assert-False $cppCudaAudit.nativeExecutableRun "Native executable run."

$downloadAudit = Read-JsonArtifact "phase-exec-sim-r035-no-download-audit.json"
Assert-False $downloadAudit.filesDownloaded "Files downloaded."
Assert-False $downloadAudit.networkDownloadAttempted "Network download attempted."

$validationAudit = Read-JsonArtifact "phase-exec-sim-r035-no-validation-import-backtest-audit.json"
Assert-False $validationAudit.quoteRowsValidated "Quote rows validated."
Assert-False $validationAudit.quoteRowsImportedIntoDb "Quote rows imported into DB."
Assert-False $validationAudit.persistedSanitizedRowsCreated "Persisted sanitized rows created."
Assert-False $validationAudit.simulationExecuted "Simulation executed."
Assert-False $validationAudit.backtestExecuted "Backtest executed."

$tcaAudit = Read-JsonArtifact "phase-exec-sim-r035-no-tca-result-lines-audit.json"
Assert-False $tcaAudit.tcaResultLinesProduced "TCA result lines produced."
Assert-False $tcaAudit.newTcaResultLinesCreated "New TCA result lines created."

$polygonAudit = Read-JsonArtifact "phase-exec-sim-r035-no-polygon-api-call-audit.json"
Assert-False $polygonAudit.polygonApiCalled "Polygon API called."

$lmaxAudit = Read-JsonArtifact "phase-exec-sim-r035-no-lmax-call-audit.json"
Assert-False $lmaxAudit.lmaxCalled "LMAX called."
Assert-False $lmaxAudit.marketDataRequestSent "MarketDataRequest sent."
Assert-False $lmaxAudit.marketDataResponseRead "MarketDataResponse read."
Assert-False $lmaxAudit.brokerActivationPerformed "Broker activation performed."

$externalAudit = Read-JsonArtifact "phase-exec-sim-r035-no-external-api-call-audit.json"
Assert-False $externalAudit.externalApiCalled "External API called."
Assert-False $externalAudit.polygonApiCalled "Polygon API called."
Assert-False $externalAudit.lmaxCalled "LMAX called."

$runtimeAudit = Read-JsonArtifact "phase-exec-sim-r035-no-broker-marketdata-runtime-audit.json"
Assert-False $runtimeAudit.brokerActivationPerformed "Broker activation performed."
Assert-False $runtimeAudit.socketOpened "Socket opened."
Assert-False $runtimeAudit.tlsOpened "TLS opened."
Assert-False $runtimeAudit.fixOpened "FIX opened."
Assert-False $runtimeAudit.marketDataRuntimeStarted "MarketData runtime started."
Assert-False $runtimeAudit.marketDataRequestSent "MarketDataRequest sent."
Assert-False $runtimeAudit.marketDataResponseRead "MarketDataResponse read."
Assert-False $runtimeAudit.apiWorkerSchedulerServiceStarted "API/Worker/Scheduler/Service started."
Assert-False $runtimeAudit.timerPollingBackgroundJobIntroduced "Timer/polling/background job introduced."

$orderAudit = Read-JsonArtifact "phase-exec-sim-r035-no-order-fill-report-route-audit.json"
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

$usdjpy = Read-JsonArtifact "phase-exec-sim-r035-usdjpy-caveat-preservation.json"
if ($usdjpy.normalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.executionTradableSymbol -ne "USDJPY" -or -not $usdjpy.requiresInversion -or $usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8") {
    throw "USDJPY caveat weakened."
}
Assert-False $usdjpy.caveatWeakened "USDJPY caveat weakened."

$lmax = Read-JsonArtifact "phase-exec-sim-r035-lmax-readonly-baseline-reference.json"
Assert-True $lmax.lmaxReferenceOnly "LMAX reference-only missing."
Assert-False $lmax.lmaxCalledInR035 "LMAX called in R035."
Assert-False $lmax.audusdMisclassifiedAsFailed "AUDUSD misclassified as failed."

$noExternal = Read-JsonArtifact "phase-exec-sim-r035-no-external-audit.json"
Assert-True $noExternal.noExternal "No-external flag missing."
Assert-False $noExternal.polygonApiCalled "Polygon API called."
Assert-False $noExternal.lmaxCalled "LMAX called."
Assert-False $noExternal.externalApiCalled "External API called."
Assert-False $noExternal.filesDownloaded "Files downloaded."

$forbidden = Read-JsonArtifact "phase-exec-sim-r035-forbidden-actions-audit.json"
Assert-False $forbidden.forbiddenActionsDetected "Forbidden actions detected."
Assert-False $forbidden.pythonOrScriptExecuted "Python/script executed."
Assert-False $forbidden.qubesExecutableRun "Qubes executable run."
Assert-False $forbidden.cppCudaWorkloadRun "C++/CUDA workload run."
Assert-False $forbidden.validationImportBacktestExecuted "Validation/import/backtest executed."
Assert-False $forbidden.tcaResultLinesProduced "TCA lines produced."
Assert-False $forbidden.orderOrTradingPathIntroduced "Order/trading path introduced."
Assert-False $forbidden.stateMutated "State mutated."

$evidence = Read-JsonArtifact "phase-exec-sim-r035-build-test-validator-evidence.json"
if ($evidence.dotnetBuildNoRestore.status -notlike "PASS*") { throw "Build/test/validator evidence missing: dotnet build." }
if ($evidence.focusedR035Tests.status -notlike "PASS*") { throw "Build/test/validator evidence missing: focused R035 tests." }
if ($evidence.unitTests.status -notlike "PASS*") { throw "Build/test/validator evidence missing: unit tests." }

Write-Host "EXEC_SIM_R035_PASS_AGGREGATED_WEIGHTS_TIMESTAMP_SEMANTICS_DERIVED_NO_EXTERNAL"
Write-Host "EXEC_SIM_R035_PASS_LEGACY_SESSION_WINDOW_CANDIDATES_DERIVED_NO_EXTERNAL"
Write-Host "EXEC_SIM_R035_PASS_NO_EXECUTION_NO_BACKTEST_GATE_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R035_NEEDS_OPERATOR_SESSION_WINDOW_CONFIRMATION_NO_EXTERNAL"
Write-Host "EXEC_SIM_R035_NEEDS_OPERATOR_TIMEZONE_CONFIRMATION_NO_EXTERNAL"
Write-Host "EXEC_SIM_R035_NEEDS_OPERATOR_DATE_RANGES_NO_EXTERNAL"
