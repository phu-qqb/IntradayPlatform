param(
    [string]$ArtifactDirectory = "artifacts/readiness/execution-sim"
)

$ErrorActionPreference = "Stop"

function Fail-Gate {
    param([string]$Classification, [string]$Message)
    Write-Error "$Classification`: $Message"
    exit 1
}

function Read-JsonArtifact {
    param([string]$Path, [string]$MissingClassification)
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail-Gate $MissingClassification "Missing required artifact: $Path"
    }

    try { return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json }
    catch { Fail-Gate $MissingClassification "Artifact is not valid JSON: $Path" }
}

function Require-True {
    param([bool]$Value, [string]$Classification, [string]$Message)
    if (-not $Value) { Fail-Gate $Classification $Message }
}

function Require-False {
    param([bool]$Value, [string]$Classification, [string]$Message)
    if ($Value) { Fail-Gate $Classification $Message }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repoRoot $ArtifactDirectory

$requiredArtifacts = @{
    "phase-exec-sim-r001-summary.md" = "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING"
    "phase-exec-sim-r001-quote-path-fixture-contract.json" = "EXEC_SIM_R001_FAIL_QUOTE_PATH_CONTRACT_MISSING"
    "phase-exec-sim-r001-close-benchmark-fixture-contract.json" = "EXEC_SIM_R001_FAIL_QUOTE_PATH_CONTRACT_MISSING"
    "phase-exec-sim-r001-simulation-policy-contract.json" = "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING"
    "phase-exec-sim-r001-simulation-result-contract.json" = "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING"
    "phase-exec-sim-r001-tca-report-contract.json" = "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING"
    "phase-exec-sim-r001-quote-path-scenarios.json" = "EXEC_SIM_R001_FAIL_QUOTE_PATH_CONTRACT_MISSING"
    "phase-exec-sim-r001-policy-comparison-results.json" = "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING"
    "phase-exec-sim-r001-wakett-limit-baseline-result.json" = "EXEC_SIM_R001_FAIL_WAKETT_BASELINES_MISSING"
    "phase-exec-sim-r001-wakett-five-market-slices-result.json" = "EXEC_SIM_R001_FAIL_WAKETT_BASELINES_MISSING"
    "phase-exec-sim-r001-close-seeking-adaptive-result.json" = "EXEC_SIM_R001_FAIL_CLOSE_SEEKING_RESULT_MISSING"
    "phase-exec-sim-r001-controlled-residual-cross-result.json" = "EXEC_SIM_R001_FAIL_CLOSE_SEEKING_RESULT_MISSING"
    "phase-exec-sim-r001-slippage-vs-close-report.json" = "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING"
    "phase-exec-sim-r001-spread-cost-report.json" = "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING"
    "phase-exec-sim-r001-opportunity-nonfill-cost-report.json" = "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING"
    "phase-exec-sim-r001-residual-risk-report.json" = "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING"
    "phase-exec-sim-r001-fill-ratio-report.json" = "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING"
    "phase-exec-sim-r001-cost-bucket-calibration-contract.json" = "EXEC_SIM_R001_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
    "phase-exec-sim-r001-major-pair-5usd-per-million-scenario.json" = "EXEC_SIM_R001_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
    "phase-exec-sim-r001-nonmajor-calibration-note.json" = "EXEC_SIM_R001_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
    "phase-exec-sim-r001-no-real-fill-audit.json" = "EXEC_SIM_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
    "phase-exec-sim-r001-no-execution-report-audit.json" = "EXEC_SIM_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
    "phase-exec-sim-r001-no-order-created-audit.json" = "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-exec-sim-r001-no-route-no-submission-audit.json" = "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-exec-sim-r001-lineage-preservation.json" = "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING"
    "phase-exec-sim-r001-usdjpy-caveat-preservation.json" = "EXEC_SIM_R001_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-exec-sim-r001-lmax-readonly-baseline-reference.json" = "EXEC_SIM_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-exec-sim-r001-no-external-audit.json" = "EXEC_SIM_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-exec-sim-r001-forbidden-actions-audit.json" = "EXEC_SIM_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-exec-sim-r001-next-phase-recommendation.json" = "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING"
    "phase-exec-sim-r001-build-test-validator-evidence.json" = "EXEC_SIM_R001_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $entry.Key))) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$summary = Get-Content -LiteralPath (Join-Path $artifactRoot "phase-exec-sim-r001-summary.md") -Raw
if ([string]::IsNullOrWhiteSpace($summary)) {
    Fail-Gate "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING" "Summary is empty."
}

$quotePath = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r001-quote-path-fixture-contract.json") "EXEC_SIM_R001_FAIL_QUOTE_PATH_CONTRACT_MISSING"
$benchmark = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r001-close-benchmark-fixture-contract.json") "EXEC_SIM_R001_FAIL_QUOTE_PATH_CONTRACT_MISSING"
$policy = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r001-simulation-policy-contract.json") "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING"
$resultContract = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r001-simulation-result-contract.json") "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING"
$tca = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r001-tca-report-contract.json") "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING"
$scenarios = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r001-quote-path-scenarios.json") "EXEC_SIM_R001_FAIL_QUOTE_PATH_CONTRACT_MISSING"
$comparison = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r001-policy-comparison-results.json") "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING"
$wakettLimit = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r001-wakett-limit-baseline-result.json") "EXEC_SIM_R001_FAIL_WAKETT_BASELINES_MISSING"
$wakettSlices = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r001-wakett-five-market-slices-result.json") "EXEC_SIM_R001_FAIL_WAKETT_BASELINES_MISSING"
$adaptive = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r001-close-seeking-adaptive-result.json") "EXEC_SIM_R001_FAIL_CLOSE_SEEKING_RESULT_MISSING"
$controlled = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r001-controlled-residual-cross-result.json") "EXEC_SIM_R001_FAIL_CLOSE_SEEKING_RESULT_MISSING"
$slippage = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r001-slippage-vs-close-report.json") "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING"
$spread = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r001-spread-cost-report.json") "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING"
$opp = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r001-opportunity-nonfill-cost-report.json") "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING"
$residual = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r001-residual-risk-report.json") "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING"
$fillRatio = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r001-fill-ratio-report.json") "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING"
$costBucket = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r001-cost-bucket-calibration-contract.json") "EXEC_SIM_R001_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$major5 = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r001-major-pair-5usd-per-million-scenario.json") "EXEC_SIM_R001_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$nonMajor = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r001-nonmajor-calibration-note.json") "EXEC_SIM_R001_FAIL_5USD_PER_MILLION_UNIVERSALIZED"
$fillAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r001-no-real-fill-audit.json") "EXEC_SIM_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$reportAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r001-no-execution-report-audit.json") "EXEC_SIM_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$orderAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r001-no-order-created-audit.json") "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$routeAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r001-no-route-no-submission-audit.json") "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$lineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r001-lineage-preservation.json") "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r001-usdjpy-caveat-preservation.json") "EXEC_SIM_R001_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r001-lmax-readonly-baseline-reference.json") "EXEC_SIM_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r001-no-external-audit.json") "EXEC_SIM_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r001-forbidden-actions-audit.json") "EXEC_SIM_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-exec-sim-r001-build-test-validator-evidence.json") "EXEC_SIM_R001_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$quotePath.contractCreated) "EXEC_SIM_R001_FAIL_QUOTE_PATH_CONTRACT_MISSING" "Quote path fixture contract missing."
foreach ($field in @("InstrumentId", "ExecutionTradableSymbol", "TimestampUtc", "Bid", "Ask", "Mid", "SpreadBps", "QuoteAge", "FeedStatus", "BarWindowStartUtc", "BarWindowEndUtc", "TargetCloseTimestampUtc", "KnownAtTimestampUtc")) {
    Require-True (@($quotePath.requiredFields) -contains $field) "EXEC_SIM_R001_FAIL_QUOTE_PATH_CONTRACT_MISSING" "Quote field missing: $field"
}
Require-True ([int]$quotePath.scenarioCount -ge 8) "EXEC_SIM_R001_FAIL_QUOTE_PATH_CONTRACT_MISSING" "Quote scenarios missing."
Require-True ([bool]$quotePath.window.fixtureOnly -and [bool]$quotePath.window.noExternal -and [bool]$quotePath.window.noLiveMarketData) "EXEC_SIM_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Quote path not no-external fixture."
Require-False ([bool]$quotePath.rawBrokerPayloadsSerialized) "EXEC_SIM_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Raw broker payload serialized."
Require-False ([bool]$quotePath.rawMarketDataFixturePayloadsSerialized) "EXEC_SIM_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Raw fixture payload serialized."

Require-True ([bool]$benchmark.contractCreated) "EXEC_SIM_R001_FAIL_QUOTE_PATH_CONTRACT_MISSING" "Close benchmark fixture missing."
Require-True (@($benchmark.availabilityStatuses) -contains "CloseUnavailable") "EXEC_SIM_R001_FAIL_QUOTE_PATH_CONTRACT_MISSING" "Missing close-unavailable handling."
Require-True (@($benchmark.availabilityStatuses) -contains "InconclusiveSafe") "EXEC_SIM_R001_FAIL_QUOTE_PATH_CONTRACT_MISSING" "Missing inconclusive-safe handling."
Require-True ([bool]$benchmark.fixtureOnly -and [bool]$benchmark.noExternal) "EXEC_SIM_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Benchmark not no-external fixture."

Require-True ([bool]$policy.contractCreated) "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING" "Simulation policy contract missing."
Require-True ([bool]$policy.allPoliciesFixtureOnly -and [bool]$policy.allPoliciesPaperOnly -and [bool]$policy.allPoliciesNonExecutable) "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Simulation policy executable."
Require-True ([bool]$policy.allPoliciesNotAnOrder -and [bool]$policy.allPoliciesNotSubmitted -and [bool]$policy.allPoliciesNoBrokerRoute) "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Policy can create order/route/submission."
Require-True ([bool]$policy.allPoliciesNoRealFill -and [bool]$policy.allPoliciesNoExecutionReport) "EXEC_SIM_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Policy can create fill/report."
Require-False ([bool]$policy.runnableExecutionAlgoCreated) "EXEC_SIM_R001_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Runnable execution algo created."
Require-False ([bool]$policy.executionScheduleCreated) "EXEC_SIM_R001_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Execution schedule created."

Require-True ([bool]$resultContract.contractCreated) "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING" "Simulation result contract missing."
Require-True ([bool]$resultContract.fixtureOnly -and [bool]$resultContract.paperOnly -and [bool]$resultContract.nonExecutable) "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Simulation result executable."
Require-False ([bool]$resultContract.simulationResultLinesAreFills) "EXEC_SIM_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Simulation line is fill."
Require-False ([bool]$resultContract.simulationResultLinesAreExecutionReports) "EXEC_SIM_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Simulation line is report."
Require-False ([bool]$resultContract.simulationResultLinesAreBrokerRecords) "EXEC_SIM_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Simulation line is broker record."

Require-True ([bool]$tca.contractCreated) "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING" "TCA contract missing."
foreach ($metric in @("FillRatio", "PassiveFillRatio", "AggressiveFillRatio", "ResidualAtClose", "SlippageVsCloseBps", "SpreadPaidBps", "EstimatedOpportunityCost", "EstimatedNonFillCost", "EstimatedResidualCost")) {
    Require-True (@($tca.metrics) -contains $metric) "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING" "TCA metric missing: $metric"
}
Require-True ([bool]$tca.includesSlippageVsClose -and [bool]$tca.includesSpreadPaid -and [bool]$tca.includesResidualAtClose -and [bool]$tca.includesFillRatio) "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING" "Core TCA metrics missing."
Require-False ([bool]$tca.noOrdersCreated -eq $false) "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "TCA creates orders."
Require-True ([bool]$scenarios.scenarioFixtureCreated -and [int]$scenarios.scenarioCount -ge 8) "EXEC_SIM_R001_FAIL_QUOTE_PATH_CONTRACT_MISSING" "Scenario fixture missing."
foreach ($scenario in @("NormalLiquid", "WideSpreadNearClose", "QuoteGapNearClose", "StaleQuoteNearClose", "FavorableDrift", "AdverseDrift", "LowPassiveFillProbability", "ResidualHighNearClose")) {
    $match = @(@($scenarios.scenarios) | Where-Object { $_.scenario -eq $scenario })
    Require-True ($match.Count -gt 0) "EXEC_SIM_R001_FAIL_QUOTE_PATH_CONTRACT_MISSING" "Scenario missing: $scenario"
}

Require-True ([bool]$comparison.comparisonCreated) "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING" "Policy comparison missing."
Require-True ([bool]$comparison.allResultsFixtureOnly -and [bool]$comparison.allResultsPaperOnly -and [bool]$comparison.allResultsNonExecutable) "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Comparison results executable."
Require-False ([bool]$comparison.ordersCreated) "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Comparison creates orders."
Require-False ([bool]$comparison.fillsCreated) "EXEC_SIM_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Comparison creates fills."
Require-False ([bool]$comparison.executionReportsCreated) "EXEC_SIM_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Comparison creates reports."
Require-False ([bool]$comparison.routesCreated) "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Comparison creates routes."
Require-False ([bool]$comparison.submissionsCreated) "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Comparison creates submissions."

Require-True ([bool]$wakettLimit.baselineCreated -and [bool]$wakettLimit.negativeBaseline -and [bool]$wakettLimit.blockedAsProductionDefault) "EXEC_SIM_R001_FAIL_WAKETT_BASELINES_MISSING" "Wakett limit baseline missing/block not set."
Require-True ([decimal]$wakettLimit.residualAtClose -ge 0.75) "EXEC_SIM_R001_FAIL_WAKETT_BASELINES_MISSING" "Wakett limit residual not high."
Require-True ([decimal]$wakettLimit.estimatedNonFillCostBps -gt 0) "EXEC_SIM_R001_FAIL_WAKETT_BASELINES_MISSING" "Wakett limit non-fill cost missing."
Require-True ([bool]$wakettSlices.baselineCreated -and [bool]$wakettSlices.mechanicalMarketSlices -and [bool]$wakettSlices.repeatedSpreadCrossing) "EXEC_SIM_R001_FAIL_WAKETT_BASELINES_MISSING" "Wakett slices baseline missing."
Require-True ([decimal]$wakettSlices.spreadPaidBps -ge 5) "EXEC_SIM_R001_FAIL_WAKETT_BASELINES_MISSING" "Wakett slices spread cost not high."
Require-False ([bool]$wakettSlices.noRealFill -eq $false) "EXEC_SIM_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Wakett slices creates real fill."

Require-True ([bool]$adaptive.resultCreated) "EXEC_SIM_R001_FAIL_CLOSE_SEEKING_RESULT_MISSING" "Adaptive result missing."
Require-True ([bool]$adaptive.designOnly -and [bool]$adaptive.fixtureOnly -and [bool]$adaptive.paperOnly -and [bool]$adaptive.nonExecutable) "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Adaptive result executable."
Require-True ([bool]$adaptive.noRealFill -and [bool]$adaptive.noExecutionReport) "EXEC_SIM_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Adaptive creates fill/report."
Require-True ([bool]$controlled.resultCreated) "EXEC_SIM_R001_FAIL_CLOSE_SEEKING_RESULT_MISSING" "Controlled residual result missing."
Require-True ([bool]$controlled.opportunityCostExceedsSpreadCost -and [bool]$controlled.costJustifiedResidualCompletionOnly) "EXEC_SIM_R001_FAIL_CLOSE_SEEKING_RESULT_MISSING" "Controlled residual not cost-justified."
Require-False ([bool]$controlled.blindMarketCrossing) "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Blind market crossing allowed."

Require-True ([bool]$slippage.reportCreated) "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING" "Slippage report missing."
Require-True ([bool]$spread.reportCreated) "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING" "Spread report missing."
Require-True ([bool]$opp.reportCreated) "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING" "Opportunity/non-fill report missing."
Require-True ([bool]$residual.reportCreated) "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING" "Residual report missing."
Require-True ([bool]$fillRatio.reportCreated) "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING" "Fill ratio report missing."
Require-False ([bool]$fillRatio.simulationLinesAreFills) "EXEC_SIM_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill ratio report treats simulation lines as fills."

Require-True ([bool]$costBucket.contractCreated) "EXEC_SIM_R001_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "Cost bucket contract missing."
Require-True ([bool]$costBucket.fiveUsdPerMillionIsBestCaseOnly) "EXEC_SIM_R001_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/m not marked best-case only."
Require-False ([bool]$costBucket.fiveUsdPerMillionUniversalized) "EXEC_SIM_R001_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/m universalized."
Require-True ([bool]$major5.scenarioCreated -and [bool]$major5.fiveUsdPerMillionIsBestCaseOnly) "EXEC_SIM_R001_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "Major 5 USD scenario missing."
Require-False ([bool]$major5.universalTarget) "EXEC_SIM_R001_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "Major 5 USD target universal."
Require-True ([bool]$nonMajor.noteCreated -and [bool]$nonMajor.requiresHigherCostBucketOrCalibration) "EXEC_SIM_R001_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "Non-major calibration note missing."
Require-False ([bool]$nonMajor.usesMajorBestCaseFiveUsdPerMillion) "EXEC_SIM_R001_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "Non-major uses major 5 USD target."

Require-True ([bool]$fillAudit.auditCreated) "EXEC_SIM_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "No-fill audit missing."
Require-False ([bool]$fillAudit.simulationResultLinesAreRealFills) "EXEC_SIM_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Simulation lines are fills."
Require-False ([bool]$fillAudit.realFillsCreated) "EXEC_SIM_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Real fills created."
Require-False ([bool]$fillAudit.fillDomainEntityCreated) "EXEC_SIM_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill domain entity created."
Require-True ([bool]$reportAudit.auditCreated) "EXEC_SIM_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "No-report audit missing."
Require-False ([bool]$reportAudit.executionReportsCreated) "EXEC_SIM_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Execution reports created."
Require-False ([bool]$reportAudit.brokerExecutionReportsCreated) "EXEC_SIM_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Broker reports created."
Require-True ([bool]$orderAudit.auditCreated) "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No-order audit missing."
Require-False ([bool]$orderAudit.ordersCreated) "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders created."
Require-False ([bool]$orderAudit.executableOrdersCreated) "EXEC_SIM_R001_FAIL_EXECUTABLE_ORDER_CREATED" "Executable orders created."
Require-False ([bool]$orderAudit.omsOrdersCreated) "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "OMS orders created."
Require-False ([bool]$orderAudit.brokerOrdersCreated) "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Broker orders created."
Require-True ([bool]$routeAudit.auditCreated) "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No-route audit missing."
Require-False ([bool]$routeAudit.routesCreated) "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Routes created."
Require-False ([bool]$routeAudit.submissionsCreated) "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Submissions created."

Require-True ([bool]$lineage.lineagePreserved -and [bool]$lineage.pmsEmsOmsLineagePreserved) "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING" "Lineage missing."
foreach ($field in @("CycleRunId", "QubesRunId", "PaperExecutionPlanId", "PaperExecutionPlanLineId", "PaperCandidateId", "RebalanceIntentId", "RiskReviewId", "LotSizingId")) {
    Require-True (@($lineage.requiredLineage) -contains $field) "EXEC_SIM_R001_FAIL_TCA_CONTRACT_MISSING" "Lineage field missing: $field"
}

Require-True ([bool]$usdjpy.usdjpyCaveatPreserved) "EXEC_SIM_R001_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
Require-True ([string]$usdjpy.securityId -eq "4004") "EXEC_SIM_R001_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID wrong."
Require-True ([string]$usdjpy.securityIdSource -eq "8") "EXEC_SIM_R001_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource wrong."
Require-False ([bool]$usdjpy.weakened) "EXEC_SIM_R001_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat weakened."
Require-False ([bool]$usdjpy.audusdMisclassifiedFailed) "EXEC_SIM_R001_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD misclassified."
Require-True ([bool]$lmax.referenceOnly) "EXEC_SIM_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX not reference-only."
Require-False ([bool]$lmax.brokerCalled) "EXEC_SIM_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker called."
Require-False ([bool]$lmax.lmaxCalled) "EXEC_SIM_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX called."
Require-False ([bool]$lmax.liveMarketDataRequested) "EXEC_SIM_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Live market data requested."

Require-False ([bool]$noExternal.brokerActivation) "EXEC_SIM_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker activation."
Require-False ([bool]$noExternal.socketTlsFixRuntimeAction) "EXEC_SIM_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Socket/TLS/FIX runtime action."
Require-False ([bool]$noExternal.liveMarketRuntimeAction) "EXEC_SIM_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Live market runtime action."
Require-False ([bool]$noExternal.marketDataRequestAttempted) "EXEC_SIM_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "MarketDataRequest attempted."
Require-False ([bool]$noExternal.marketDataResponseRead) "EXEC_SIM_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "MarketDataResponse read."
Require-False ([bool]$noExternal.apiWorkerLiveGatewayEnabled) "EXEC_SIM_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API/Worker live gateway enabled."
Require-False ([bool]$noExternal.schedulerPollingServiceTimerBackgroundJob) "EXEC_SIM_R001_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler/service/polling introduced."
Require-False ([bool]$noExternal.automaticExecution) "EXEC_SIM_R001_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Automatic execution introduced."
Require-False ([bool]$noExternal.ordersCreated) "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders created."
Require-False ([bool]$noExternal.executableOrdersCreated) "EXEC_SIM_R001_FAIL_EXECUTABLE_ORDER_CREATED" "Executable orders created."
Require-False ([bool]$noExternal.fillsCreated) "EXEC_SIM_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fills created."
Require-False ([bool]$noExternal.executionReportsCreated) "EXEC_SIM_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Reports created."
Require-False ([bool]$noExternal.brokerExecutionReportsCreated) "EXEC_SIM_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Broker reports created."
Require-False ([bool]$noExternal.routesCreated) "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Routes created."
Require-False ([bool]$noExternal.submissionsCreated) "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Submissions created."
Require-False ([bool]$noExternal.liveTradingPathIntroduced) "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Live trading path."
Require-False ([bool]$noExternal.paperLedgerCommit) "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Paper ledger commit."
Require-False ([bool]$noExternal.livePositionMutation) "EXEC_SIM_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Live position mutation."
Require-False ([bool]$noExternal.brokerPositionMutation) "EXEC_SIM_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker position mutation."
Require-False ([bool]$noExternal.productionLedgerMutation) "EXEC_SIM_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Production ledger mutation."
Require-False ([bool]$noExternal.tradingStateMutation) "EXEC_SIM_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Trading state mutation."
Require-False ([bool]$noExternal.replayOrShadowReplay) "EXEC_SIM_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Replay/shadow replay."
Require-False ([bool]$noExternal.secretOrRawPayloadSerialization) "EXEC_SIM_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Secret/raw serialization."

Require-False ([bool]$forbidden.brokerActivationDetected) "EXEC_SIM_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden broker activation."
Require-False ([bool]$forbidden.socketTlsFixMarketDataRuntimeDetected) "EXEC_SIM_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden socket/TLS/FIX/MarketData runtime."
Require-False ([bool]$forbidden.liveMarketDataRequestOrResponseDetected) "EXEC_SIM_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden live MarketData."
Require-False ([bool]$forbidden.schedulerPollingServiceTimerBackgroundJobIntroduced) "EXEC_SIM_R001_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Forbidden scheduler/service."
Require-False ([bool]$forbidden.automaticExecutionIntroduced) "EXEC_SIM_R001_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Forbidden automatic execution."
Require-False ([bool]$forbidden.orderSubmissionIntroduced) "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden order submission."
Require-False ([bool]$forbidden.executableOrderCreated) "EXEC_SIM_R001_FAIL_EXECUTABLE_ORDER_CREATED" "Forbidden executable order."
Require-False ([bool]$forbidden.omsOrBrokerOrderCreated) "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden OMS/broker order."
Require-False ([bool]$forbidden.fillOrExecutionReportIntroduced) "EXEC_SIM_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Forbidden fill/report."
Require-False ([bool]$forbidden.brokerExecutionReportIntroduced) "EXEC_SIM_R001_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Forbidden broker report."
Require-False ([bool]$forbidden.routeOrSubmissionIntroduced) "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden route/submission."
Require-False ([bool]$forbidden.liveTradingPathIntroduced) "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden live trading."
Require-False ([bool]$forbidden.stateMutationIntroduced) "EXEC_SIM_R001_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden state mutation."
Require-False ([bool]$forbidden.paperLedgerCommitOccurred) "EXEC_SIM_R001_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden paper ledger commit."

Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "EXEC_SIM_R001_FAIL_BUILD_OR_TESTS" "Build/test evidence missing."
Require-True ([string]$evidence.dotnetBuildNoRestore -eq "PASS") "EXEC_SIM_R001_FAIL_BUILD_OR_TESTS" "Build failed."
Require-True ([string]$evidence.focusedTests -like "PASS*") "EXEC_SIM_R001_FAIL_BUILD_OR_TESTS" "Focused tests failed."
Require-True ([string]$evidence.unitTests -like "PASS*") "EXEC_SIM_R001_FAIL_BUILD_OR_TESTS" "Unit tests failed."
Require-True ([string]$evidence.validator -eq "PASS") "EXEC_SIM_R001_FAIL_BUILD_OR_TESTS" "Validator evidence missing."

Write-Host "EXEC_SIM_R001_PASS_CLOSE_SEEKING_SIMULATION_FOUNDATION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R001_PASS_TCA_BACKTEST_CONTRACT_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R001_PASS_WAKETT_BASELINE_COMPARISON_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R001_PASS_NO_REAL_FILL_NO_ORDER_SIMULATION_READY_NO_EXTERNAL"
