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
    "phase-exec-sim-r034-summary.md",
    "phase-exec-sim-r034-r033-readiness-reference.json",
    "phase-exec-sim-r034-accessible-source-roots-inventory.json",
    "phase-exec-sim-r034-expanded-legacy-code-discovery-scope.json",
    "phase-exec-sim-r034-aggregated-weights-source-evidence.json",
    "phase-exec-sim-r034-aggregated-weights-timestamp-analysis.json",
    "phase-exec-sim-r034-aggregated-weights-daily-session-pattern.json",
    "phase-exec-sim-r034-aggregated-weights-cadence-analysis.json",
    "phase-exec-sim-r034-aggregated-weights-weekday-analysis.json",
    "phase-exec-sim-r034-aggregated-weights-candidate-session-match.json",
    "phase-exec-sim-r034-legacy-python-discovery-contract.json",
    "phase-exec-sim-r034-legacy-python-files-inventory.json",
    "phase-exec-sim-r034-legacy-config-files-inventory.json",
    "phase-exec-sim-r034-session-time-discovery-results.json",
    "phase-exec-sim-r034-calendar-weekday-discovery-results.json",
    "phase-exec-sim-r034-bar-interval-discovery-results.json",
    "phase-exec-sim-r034-no-overnight-discovery-results.json",
    "phase-exec-sim-r034-previous-evening-target-discovery-results.json",
    "phase-exec-sim-r034-earliest-execution-discovery-results.json",
    "phase-exec-sim-r034-historical-date-range-discovery-results.json",
    "phase-exec-sim-r034-conflict-and-confidence-report.json",
    "phase-exec-sim-r034-session-authorization-contract.json",
    "phase-exec-sim-r034-session-authorization-result.json",
    "phase-exec-sim-r034-needs-operator-input.json",
    "phase-exec-sim-r034-weekdays-only-preservation.json",
    "phase-exec-sim-r034-trade-all-15m-bars-preservation.json",
    "phase-exec-sim-r034-no-overnight-preservation.json",
    "phase-exec-sim-r034-proxy-window-caveat-preservation.json",
    "phase-exec-sim-r034-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r034-cost-guidance-preservation.json",
    "phase-exec-sim-r034-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r034-no-python-execution-audit.json",
    "phase-exec-sim-r034-no-script-execution-audit.json",
    "phase-exec-sim-r034-no-qubes-executable-run-audit.json",
    "phase-exec-sim-r034-no-cpp-cuda-run-audit.json",
    "phase-exec-sim-r034-no-download-audit.json",
    "phase-exec-sim-r034-no-validation-import-backtest-audit.json",
    "phase-exec-sim-r034-no-tca-result-lines-audit.json",
    "phase-exec-sim-r034-no-polygon-api-call-audit.json",
    "phase-exec-sim-r034-no-lmax-call-audit.json",
    "phase-exec-sim-r034-no-external-api-call-audit.json",
    "phase-exec-sim-r034-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r034-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r034-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r034-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r034-no-external-audit.json",
    "phase-exec-sim-r034-forbidden-actions-audit.json",
    "phase-exec-sim-r034-next-phase-recommendation.json",
    "phase-exec-sim-r034-build-test-validator-evidence.json"
)

foreach ($file in $requiredFiles) {
    $path = Join-Path $artifactDir $file
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing required R034 artifact: $file"
    }
}

$contract = Read-JsonArtifact "phase-exec-sim-r034-legacy-python-discovery-contract.json"
Assert-True $contract.pythonFilesInspectedAsTextOnly "Python files must be inspected as text only."
Assert-False $contract.pythonCodeExecuted "Python code execution detected in discovery contract."
Assert-False $contract.legacyQubesCodeExecuted "Legacy Qubes execution detected in discovery contract."
Assert-False $contract.cppCodeExecuted "C++ execution detected in discovery contract."
Assert-False $contract.cudaCodeExecuted "CUDA execution detected in discovery contract."
Assert-False $contract.scriptsExecutedForDiscovery "Discovery scripts executed."
Assert-True $contract.noExternal "No-external flag missing in discovery contract."
Assert-False $contract.inventSessionTimesWithoutEvidenceAllowed "Session times may not be invented."
Assert-False $contract.inventDateRangesWithoutEvidenceAllowed "Date ranges may not be invented."

$roots = Read-JsonArtifact "phase-exec-sim-r034-accessible-source-roots-inventory.json"
Assert-True $roots.noExternal "Accessible roots inventory must preserve no-external."
if (@($roots.visibleSiblingRepositories).Count -lt 1) { throw "Expanded sibling repository inventory missing." }

$scope = Read-JsonArtifact "phase-exec-sim-r034-expanded-legacy-code-discovery-scope.json"
Assert-True $scope.textOnly "Expanded discovery scope must be text-only."
Assert-False $scope.pythonExecuted "Python executed in expanded scope."
Assert-False $scope.notebooksExecuted "Notebook executed in expanded scope."
Assert-False $scope.scriptsExecuted "Script executed in expanded scope."
Assert-False $scope.qubesExecutableRun "Qubes executable run in expanded scope."
Assert-False $scope.cppCudaWorkloadRun "C++/CUDA workload run in expanded scope."

$aggregatedSource = Read-JsonArtifact "phase-exec-sim-r034-aggregated-weights-source-evidence.json"
Assert-False $aggregatedSource.requestedLiteralPathAccessible "Literal operator path should be recorded as inaccessible from this Windows root."
Assert-True $aggregatedSource.resolvedLegacyCoreSnapshotPathAccessible "Resolved legacy Core snapshot AggregatedWeights path must be accessible."
Assert-True $aggregatedSource.fileAccessible "AggregatedWeights file accessibility ignored."
Assert-True $aggregatedSource.fileReadable "AggregatedWeights file readability ignored."
Assert-True $aggregatedSource.textOnlyRead "AggregatedWeights must be read as text only."
Assert-False $aggregatedSource.pythonExecuted "Python executed while reading AggregatedWeights."
Assert-False $aggregatedSource.scriptsExecuted "Script executed while reading AggregatedWeights."
Assert-False $aggregatedSource.qubesExecutableRun "Qubes executable run while reading AggregatedWeights."
Assert-False $aggregatedSource.cppExecutableRun "C++ executable run while reading AggregatedWeights."
Assert-False $aggregatedSource.cudaWorkloadRun "CUDA workload run while reading AggregatedWeights."
Assert-False $aggregatedSource.externalApiCalled "External API called while reading AggregatedWeights."
Assert-False $aggregatedSource.filesDownloaded "Files downloaded while reading AggregatedWeights."

$timestampAnalysis = Read-JsonArtifact "phase-exec-sim-r034-aggregated-weights-timestamp-analysis.json"
Assert-True $timestampAnalysis.fileAccessible "Timestamp analysis must record accessible AggregatedWeights file."
Assert-True $timestampAnalysis.fileReadable "Timestamp analysis must record readable AggregatedWeights file."
if ($timestampAnalysis.delimiterDetected -ne "semicolon") { throw "AggregatedWeights delimiter detection must be semicolon." }
if ($timestampAnalysis.timestampColumnIndex -ne 0) { throw "AggregatedWeights timestamp column index must be 0." }
if ($timestampAnalysis.timestampFormatDetected -ne "yyyyMMddHHmm") { throw "AggregatedWeights timestamp format must be yyyyMMddHHmm." }
if ($timestampAnalysis.totalRows -ne 1142) { throw "Unexpected AggregatedWeights total row count." }
if ($timestampAnalysis.parseableTimestampRows -ne 1141) { throw "Unexpected AggregatedWeights parseable timestamp count." }
if ($timestampAnalysis.unparseableTimestampRows -ne 1) { throw "Unexpected AggregatedWeights unparseable timestamp count." }
if ($timestampAnalysis.firstTimestampRaw -ne "202510101451") { throw "Unexpected first AggregatedWeights timestamp." }
if ($timestampAnalysis.lastTimestampRaw -ne "202512162051") { throw "Unexpected last AggregatedWeights timestamp." }
if ($timestampAnalysis.weekendTimestampCount -ne 0) { throw "AggregatedWeights contains weekend timestamps." }
if ($timestampAnalysis.cadenceDetected -ne "15m") { throw "AggregatedWeights cadence must be 15m." }
if ($timestampAnalysis.timezoneEvidence -ne "Ambiguous") { throw "AggregatedWeights timezone must remain ambiguous unless proven." }
Assert-True $timestampAnalysis.needsOperatorConfirmation "AggregatedWeights timestamp analysis must require operator confirmation."
Assert-False $timestampAnalysis.sessionTimesInvented "Session times invented from AggregatedWeights."
Assert-False $timestampAnalysis.dateRangesInvented "Date ranges invented from AggregatedWeights."

$dailyPattern = Read-JsonArtifact "phase-exec-sim-r034-aggregated-weights-daily-session-pattern.json"
if ($dailyPattern.dailyPatternStatus -ne "LegacyAggregatedWeightsDailyPatternDiscoveredNeedsOperatorConfirmation") { throw "Daily session pattern status missing or incorrect." }
if ($dailyPattern.uniqueTradingDateCount -ne 45) { throw "Unexpected AggregatedWeights unique trading date count." }
Assert-True $dailyPattern.needsOperatorConfirmation "Daily session pattern must need operator confirmation."
Assert-False $dailyPattern.exactSessionTimesInvented "Exact session times invented in daily pattern artifact."

$cadence = Read-JsonArtifact "phase-exec-sim-r034-aggregated-weights-cadence-analysis.json"
if ($cadence.cadenceDetected -ne "15m") { throw "Cadence analysis must detect 15m." }
if ($cadence.cadenceConfidence -ne "Strong") { throw "Cadence analysis confidence must be Strong." }
Assert-False $cadence.mixedCadenceDetected "Mixed cadence incorrectly detected."
Assert-False $cadence.h1OnlyCadenceSupported "H1-only cadence should not be supported by AggregatedWeights."
Assert-True $cadence.operatorConstraintPreserved.tradeAll15mBars "TradeAll15mBars not preserved in cadence artifact."
Assert-True $cadence.noCodeExecuted "Cadence analysis must record no code execution."

$weekday = Read-JsonArtifact "phase-exec-sim-r034-aggregated-weights-weekday-analysis.json"
Assert-True $weekday.weekdayOnlyConfirmedFromTimestampEvidence "Weekday-only timestamp evidence missing."
if ($weekday.weekendTimestampCount -ne 0) { throw "Weekend timestamps detected in weekday analysis." }
Assert-True $weekday.operatorConstraintPreserved.weekdaysOnly "WeekdaysOnly not preserved in weekday analysis."
Assert-False $weekday.operatorConstraintPreserved.overnightAllowed "OvernightAllowed=false not preserved in weekday analysis."

$candidateMatch = Read-JsonArtifact "phase-exec-sim-r034-aggregated-weights-candidate-session-match.json"
if ($candidateMatch.bestCandidateName -ne "AggregatedWeightsObservedLateDay15mPattern") { throw "AggregatedWeights candidate match ignored." }
if ($candidateMatch.bestConfigBackedCandidateName -ne "Europe/LondonNyOverlapPlusNyLateFamilyNeedsConfirmation") { throw "Expected config-backed candidate match missing." }
if ($candidateMatch.timezoneEvidence -ne "Ambiguous") { throw "Candidate match must preserve ambiguous timezone evidence." }
Assert-True $candidateMatch.needsOperatorConfirmation "Candidate match must need operator confirmation."
if ($candidateMatch.authorizationRecommendation -ne "NeedsOperatorSessionTimeConfirmation") { throw "Candidate match must recommend operator confirmation." }
Assert-False $candidateMatch.sessionTimesInvented "Session times invented in candidate match."
Assert-False $candidateMatch.dateRangesInvented "Date ranges invented in candidate match."

$inventory = Read-JsonArtifact "phase-exec-sim-r034-legacy-python-files-inventory.json"
Assert-True $inventory.legacyPythonFilesFound "Expanded inventory should find candidate Python files outside QQ.Production.Intraday."
if ($inventory.inventoryStatus -ne "LegacyPythonCandidatesFoundNeedsOperatorConfirmation") { throw "Missing LegacyPythonCandidatesFoundNeedsOperatorConfirmation inventory status." }
Assert-False $inventory.pythonCodeExecuted "Python executed during inventory."
Assert-False $inventory.filesDownloaded "Download detected during inventory."

$configInventory = Read-JsonArtifact "phase-exec-sim-r034-legacy-config-files-inventory.json"
Assert-True $configInventory.configCandidatesFound "Config candidates missing from expanded discovery."
Assert-True $configInventory.textOnly "Config inventory must be text-only."
Assert-False $configInventory.scriptsExecuted "Scripts executed during config inventory."
Assert-False $configInventory.externalApiCalled "External API called during config inventory."

$sessionResults = Read-JsonArtifact "phase-exec-sim-r034-session-time-discovery-results.json"
if ($sessionResults.evidenceStatus -ne "DiscoveredWithConflicts") { throw "Session time discovery must mark DiscoveredWithConflicts." }
if ($sessionResults.aggregatedWeightsEvidenceStatus -ne "LegacyAggregatedWeightsSessionPatternDiscoveredNoExternal") { throw "AggregatedWeights session pattern discovery status missing." }
if ($sessionResults.sessionTimeStatus -ne "LegacyAggregatedWeightsSessionTimesNeedOperatorConfirmation") { throw "Session time status must reflect AggregatedWeights confirmation need." }
Assert-True $sessionResults.needsOperatorSessionTimes "NeedsOperatorSessionTimes must be true."
Assert-False $sessionResults.exactSessionTimesInvented "Exact session times were invented."
if (@($sessionResults.results).Count -lt 2) { throw "Expanded session discovery results missing." }

$calendar = Read-JsonArtifact "phase-exec-sim-r034-calendar-weekday-discovery-results.json"
Assert-True $calendar.operatorProvidedConstraintsPreserved.weekdaysOnly "WeekdaysOnly=true not preserved."

$bars = Read-JsonArtifact "phase-exec-sim-r034-bar-interval-discovery-results.json"
if ($bars.operatorAndBusinessContextPreserved.barIntervalMinutes -ne 15) { throw "Bar interval must remain 15 minutes." }
Assert-True $bars.operatorAndBusinessContextPreserved.tradeAll15mBars "TradeAll15mBars=true not preserved."
Assert-True $bars.needsOperatorConfirmation "Conflicting bar interval conventions should need operator confirmation."

$overnight = Read-JsonArtifact "phase-exec-sim-r034-no-overnight-discovery-results.json"
Assert-True $overnight.operatorProvidedConstraintsPreserved.mustEndFlat "MustEndFlat=true not preserved."
Assert-False $overnight.operatorProvidedConstraintsPreserved.overnightAllowed "OvernightAllowed=false not preserved."

$previousEvening = Read-JsonArtifact "phase-exec-sim-r034-previous-evening-target-discovery-results.json"
Assert-False $previousEvening.exactPreviousEveningTimeInvented "Previous-evening target time was invented."
Assert-True $previousEvening.needsOperatorConfirmation "Previous-evening target should need confirmation."

$earliestExecution = Read-JsonArtifact "phase-exec-sim-r034-earliest-execution-discovery-results.json"
Assert-False $earliestExecution.preSessionExecutionAuthorized "Pre-session execution must not be authorized."
Assert-False $earliestExecution.exactEarliestExecutionTimeInvented "Earliest execution time was invented."

$dateRange = Read-JsonArtifact "phase-exec-sim-r034-historical-date-range-discovery-results.json"
Assert-True $dateRange.historicalDateRangesDiscovered "Expanded discovery should record candidate date ranges."
Assert-False $dateRange.exactDateRangesInvented "Date ranges were invented."
Assert-True $dateRange.needsOperatorDateRanges "NeedsOperatorDateRanges must be true."

$conflict = Read-JsonArtifact "phase-exec-sim-r034-conflict-and-confidence-report.json"
Assert-True $conflict.conflictsFound "Conflicting definitions should be recorded."
if ($conflict.authorizationRecommendation -ne "NeedsOperatorSessionTimeConfirmation") { throw "Conflict report must recommend operator confirmation." }

$authorization = Read-JsonArtifact "phase-exec-sim-r034-session-authorization-contract.json"
Assert-False ($null -ne $authorization.sessionTimezone) "Session timezone was invented."
Assert-False ($null -ne $authorization.sessionOpenLocal) "Session open was invented."
Assert-False ($null -ne $authorization.sessionCloseLocal) "Session close was invented."
if ($authorization.sourceTimestampEvidence -ne "AggregatedWeights.txt") { throw "Session authorization must reference AggregatedWeights timestamp evidence." }
Assert-True $authorization.weekdaysOnly "WeekdaysOnly=true missing in authorization contract."
Assert-True $authorization.tradeAll15mBars "TradeAll15mBars=true missing in authorization contract."
Assert-True $authorization.mustEndFlat "MustEndFlat=true missing in authorization contract."
Assert-False $authorization.overnightAllowed "OvernightAllowed=false missing in authorization contract."
Assert-False $authorization.exactSessionTimesInvented "Authorization invented exact session times."
Assert-False $authorization.exactDateRangesInvented "Authorization invented exact date ranges."
if ($authorization.sessionAuthorizationStatus -ne "LegacyAggregatedWeightsSessionTimesNeedOperatorConfirmation") { throw "Unexpected session authorization status." }

$result = Read-JsonArtifact "phase-exec-sim-r034-session-authorization-result.json"
Assert-False $result.authorized "Session times must not be authorized without evidence."
if ($result.patternDiscoveryStatus -ne "LegacyAggregatedWeightsSessionPatternDiscoveredNoExternal") { throw "AggregatedWeights pattern discovery result missing." }
if ($result.authorizationStatus -ne "LegacyAggregatedWeightsSessionTimesNeedOperatorConfirmation") { throw "Unexpected AggregatedWeights authorization status." }
Assert-True $result.needsOperatorConfirmation "Authorization result must require operator confirmation."
Assert-True $result.needsOperatorSessionTimes "Authorization result must need session times."
Assert-True $result.needsOperatorDateRanges "Authorization result must need date ranges."
Assert-False $result.pythonExecuted "Python execution detected in authorization result."
Assert-False $result.scriptsExecuted "Script execution detected in authorization result."
Assert-False $result.qubesExecutableRun "Qubes executable run detected in authorization result."
Assert-False $result.cppExecutableRun "C++ executable run detected in authorization result."
Assert-False $result.cudaWorkloadRun "CUDA workload run detected in authorization result."
Assert-False $result.backtestExecuted "Backtest detected in authorization result."
Assert-False $result.tcaResultLinesProduced "TCA result lines detected in authorization result."

$proxy = Read-JsonArtifact "phase-exec-sim-r034-proxy-window-caveat-preservation.json"
Assert-True $proxy.proxyWindowCaveatPreserved "Proxy window caveat missing."
Assert-False $proxy.exactTrueSessionTimesInvented "True session times invented in proxy caveat artifact."

$direct = Read-JsonArtifact "phase-exec-sim-r034-direct-cross-exclusion-preservation.json"
Assert-True $direct.directCrossesSignalOnly "Direct-cross signal-only preservation missing."
Assert-True $direct.directCrossExecutionDisabled "Direct-cross execution disabled preservation missing."
Assert-False $direct.weakened "Direct-cross exclusion weakened."

$cost = Read-JsonArtifact "phase-exec-sim-r034-cost-guidance-preservation.json"
Assert-True $cost.fiveUsdPerMillionBestCaseMajorOnly "5 USD/million best-case major-only guidance missing."
Assert-False $cost.fiveUsdPerMillionUniversalized "5 USD/million universalized."

$nonmajor = Read-JsonArtifact "phase-exec-sim-r034-nonmajor-calibration-preservation.json"
Assert-True $nonmajor.nonmajorEmScandiCnhCalibrationRequired "Nonmajor calibration requirement missing."
Assert-False $nonmajor.calibrationRequirementWeakened "Nonmajor calibration weakened."

$pythonAudit = Read-JsonArtifact "phase-exec-sim-r034-no-python-execution-audit.json"
Assert-False $pythonAudit.pythonCodeExecuted "Python executed."
Assert-False $pythonAudit.legacyQubesCodeExecuted "Legacy Qubes code executed."
Assert-False $pythonAudit.notebooksExecuted "Notebook executed."
Assert-False $pythonAudit.cppCodeExecuted "C++ code executed."
Assert-False $pythonAudit.cudaCodeExecuted "CUDA code executed."
Assert-False $pythonAudit.scriptsExecutedForDiscovery "Discovery scripts executed."

$scriptAudit = Read-JsonArtifact "phase-exec-sim-r034-no-script-execution-audit.json"
Assert-False $scriptAudit.scriptsExecutedForDiscovery "Scripts executed."
Assert-False $scriptAudit.notebooksExecuted "Notebook executed."

$qubesAudit = Read-JsonArtifact "phase-exec-sim-r034-no-qubes-executable-run-audit.json"
Assert-False $qubesAudit.qubesExecutableRun "Qubes executable run."
Assert-False $qubesAudit.prodGenerateSignalsBinaryFilesRun "PRODgenerateSignalsBinaryFiles run."
Assert-False $qubesAudit.prodGenerateTimeSeriesRun "PRODgenerateTimeSeries run."
Assert-False $qubesAudit.prodManagerRun "PRODmanager run."
Assert-False $qubesAudit.prodAnubisRun "PRODAnubis run."

$cppCudaAudit = Read-JsonArtifact "phase-exec-sim-r034-no-cpp-cuda-run-audit.json"
Assert-False $cppCudaAudit.cppExecutableRun "C++ executable run."
Assert-False $cppCudaAudit.cudaWorkloadRun "CUDA workload run."
Assert-False $cppCudaAudit.nativeExecutableRun "Native executable run."

$downloadAudit = Read-JsonArtifact "phase-exec-sim-r034-no-download-audit.json"
Assert-False $downloadAudit.filesDownloaded "Files downloaded."
Assert-False $downloadAudit.networkDownloadAttempted "Network download attempted."

$validationAudit = Read-JsonArtifact "phase-exec-sim-r034-no-validation-import-backtest-audit.json"
Assert-False $validationAudit.quoteRowsValidated "Quote rows validated."
Assert-False $validationAudit.quoteRowsImportedIntoDb "Quote rows imported into DB."
Assert-False $validationAudit.persistedSanitizedQuoteRowsCreated "Persisted sanitized rows created."
Assert-False $validationAudit.simulationExecuted "Simulation executed."
Assert-False $validationAudit.backtestExecuted "Backtest executed."

$tcaAudit = Read-JsonArtifact "phase-exec-sim-r034-no-tca-result-lines-audit.json"
Assert-False $tcaAudit.tcaResultLinesProduced "TCA result lines produced."
Assert-False $tcaAudit.newTcaResultLinesCreated "New TCA result lines created."

$polygonAudit = Read-JsonArtifact "phase-exec-sim-r034-no-polygon-api-call-audit.json"
Assert-False $polygonAudit.polygonApiCalled "Polygon API call detected."

$lmaxAudit = Read-JsonArtifact "phase-exec-sim-r034-no-lmax-call-audit.json"
Assert-False $lmaxAudit.lmaxCalled "LMAX call detected."
Assert-False $lmaxAudit.marketDataRequestSent "MarketDataRequest sent."
Assert-False $lmaxAudit.marketDataResponseRead "MarketDataResponse read."

$externalAudit = Read-JsonArtifact "phase-exec-sim-r034-no-external-api-call-audit.json"
Assert-False $externalAudit.externalApiCalled "External API call detected."

$runtimeAudit = Read-JsonArtifact "phase-exec-sim-r034-no-broker-marketdata-runtime-audit.json"
Assert-False $runtimeAudit.brokerActivationPerformed "Broker activation detected."
Assert-False $runtimeAudit.socketOpened "Socket opened."
Assert-False $runtimeAudit.tlsOpened "TLS opened."
Assert-False $runtimeAudit.fixOpened "FIX opened."
Assert-False $runtimeAudit.marketDataRuntimeStarted "MarketData runtime started."
Assert-False $runtimeAudit.apiWorkerSchedulerServiceStarted "API/Worker/Scheduler/Service started."
Assert-False $runtimeAudit.timerPollingBackgroundJobIntroduced "Timer/polling/background job introduced."

$orderAudit = Read-JsonArtifact "phase-exec-sim-r034-no-order-fill-report-route-audit.json"
Assert-False $orderAudit.executableScheduleCreated "Executable schedule created."
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

$usdjpy = Read-JsonArtifact "phase-exec-sim-r034-usdjpy-caveat-preservation.json"
if ($usdjpy.normalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.executionTradableSymbol -ne "USDJPY" -or -not $usdjpy.requiresInversion -or $usdjpy.securityId -ne "4004" -or $usdjpy.securityIdSource -ne "8") {
    throw "USDJPY caveat weakened."
}
Assert-False $usdjpy.caveatWeakened "USDJPY caveat weakened."

$lmax = Read-JsonArtifact "phase-exec-sim-r034-lmax-readonly-baseline-reference.json"
Assert-True $lmax.lmaxReferenceOnly "LMAX must remain reference-only."
Assert-False $lmax.lmaxCalledInR034 "LMAX was called in R034."
Assert-False $lmax.audusdMisclassifiedAsFailed "AUDUSD misclassified as failed."

$forbidden = Read-JsonArtifact "phase-exec-sim-r034-forbidden-actions-audit.json"
Assert-False $forbidden.forbiddenActionsDetected "Forbidden action detected."

$evidence = Read-JsonArtifact "phase-exec-sim-r034-build-test-validator-evidence.json"
if ($evidence.dotnetBuildNoRestore.status -notlike "PASS*") { throw "Build/test/validator evidence missing: dotnet build." }
if ($evidence.focusedR034Tests.status -notlike "PASS*") { throw "Build/test/validator evidence missing: focused R034 tests." }
if ($evidence.unitTests.status -notlike "PASS*") { throw "Build/test/validator evidence missing: unit tests." }

Write-Host "EXEC_SIM_R034_PASS_LEGACY_PYTHON_SESSION_DISCOVERY_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R034_PASS_SESSION_RULES_DISCOVERY_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R034_PASS_WEEKDAY_NO_OVERNIGHT_CONSTRAINTS_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R034_PASS_NO_EXECUTION_NO_BACKTEST_GATE_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R034_PASS_LEGACY_AGGREGATED_WEIGHTS_SESSION_PATTERN_DISCOVERED_NO_EXTERNAL"
Write-Host "EXEC_SIM_R034_NEEDS_OPERATOR_SESSION_TIME_CONFIRMATION_NO_EXTERNAL"
Write-Host "EXEC_SIM_R034_NEEDS_OPERATOR_DATE_RANGES_NO_EXTERNAL"
