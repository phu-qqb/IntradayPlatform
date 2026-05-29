param(
    [string]$ArtifactDirectory = "artifacts/readiness/pms-ems-oms-integration"
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

    try {
        return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    }
    catch {
        Fail-Gate $MissingClassification "Artifact is not valid JSON: $Path"
    }
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
    "phase-pms-ems-oms-r018-summary.md" = "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r018-paper-simulation-result-contract.json" = "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r018-paper-simulation-result.json" = "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r018-paper-simulation-result-lines.json" = "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r018-paper-simulation-summary.json" = "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r018-paper-post-trade-preview.json" = "PMS_EMS_OMS_R018_FAIL_LIVE_STATE_MUTATION"
    "phase-pms-ems-oms-r018-paper-reconciliation-preview.json" = "PMS_EMS_OMS_R018_FAIL_LIVE_STATE_MUTATION"
    "phase-pms-ems-oms-r018-fixture-assumption-preservation.json" = "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r018-no-real-fill-audit.json" = "PMS_EMS_OMS_R018_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
    "phase-pms-ems-oms-r018-no-execution-report-audit.json" = "PMS_EMS_OMS_R018_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
    "phase-pms-ems-oms-r018-no-order-created-audit.json" = "PMS_EMS_OMS_R018_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r018-no-route-no-submission-audit.json" = "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_SUBMITTED_OR_ROUTED"
    "phase-pms-ems-oms-r018-no-live-state-mutation-audit.json" = "PMS_EMS_OMS_R018_FAIL_LIVE_STATE_MUTATION"
    "phase-pms-ems-oms-r018-idempotency-evidence.json" = "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r018-risk-lineage-preservation.json" = "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r018-qubes-lineage-preservation.json" = "PMS_EMS_OMS_R018_FAIL_QUBES_LINEAGE_WEAKENED"
    "phase-pms-ems-oms-r018-operator-decision-lineage-preservation.json" = "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r018-plan-lineage-preservation.json" = "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r018-paper-candidate-lineage-preservation.json" = "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r018-rebalance-intent-lineage-preservation.json" = "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r018-lot-sizing-lineage-preservation.json" = "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r018-missing-stale-mark-preservation.json" = "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r018-drift-acknowledgement-preservation.json" = "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r018-instrument-universe-handling.json" = "PMS_EMS_OMS_R018_FAIL_LMAX_GAP_BLOCKS_SIMULATION"
    "phase-pms-ems-oms-r018-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R018_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r018-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R018_FAIL_LMAX_GAP_BLOCKS_SIMULATION"
    "phase-pms-ems-oms-r018-no-external-audit.json" = "PMS_EMS_OMS_R018_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r018-forbidden-actions-audit.json" = "PMS_EMS_OMS_R018_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r018-next-phase-recommendation.json" = "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r018-build-test-validator-evidence.json" = "PMS_EMS_OMS_R018_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $entry.Key))) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$contract = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r018-paper-simulation-result-contract.json") "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
$result = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r018-paper-simulation-result.json") "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
$lines = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r018-paper-simulation-result-lines.json") "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
$summary = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r018-paper-simulation-summary.json") "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
$postTrade = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r018-paper-post-trade-preview.json") "PMS_EMS_OMS_R018_FAIL_LIVE_STATE_MUTATION"
$reconciliation = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r018-paper-reconciliation-preview.json") "PMS_EMS_OMS_R018_FAIL_LIVE_STATE_MUTATION"
$assumptions = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r018-fixture-assumption-preservation.json") "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
$fillAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r018-no-real-fill-audit.json") "PMS_EMS_OMS_R018_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$reportAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r018-no-execution-report-audit.json") "PMS_EMS_OMS_R018_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$orderAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r018-no-order-created-audit.json") "PMS_EMS_OMS_R018_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$routeAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r018-no-route-no-submission-audit.json") "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_SUBMITTED_OR_ROUTED"
$stateAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r018-no-live-state-mutation-audit.json") "PMS_EMS_OMS_R018_FAIL_LIVE_STATE_MUTATION"
$idempotency = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r018-idempotency-evidence.json") "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
$risk = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r018-risk-lineage-preservation.json") "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
$qubes = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r018-qubes-lineage-preservation.json") "PMS_EMS_OMS_R018_FAIL_QUBES_LINEAGE_WEAKENED"
$operatorLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r018-operator-decision-lineage-preservation.json") "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
$planLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r018-plan-lineage-preservation.json") "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
$candidateLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r018-paper-candidate-lineage-preservation.json") "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
$rebalance = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r018-rebalance-intent-lineage-preservation.json") "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
$lotSizing = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r018-lot-sizing-lineage-preservation.json") "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
$marks = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r018-missing-stale-mark-preservation.json") "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
$drift = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r018-drift-acknowledgement-preservation.json") "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r018-instrument-universe-handling.json") "PMS_EMS_OMS_R018_FAIL_LMAX_GAP_BLOCKS_SIMULATION"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r018-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R018_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r018-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R018_FAIL_LMAX_GAP_BLOCKS_SIMULATION"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r018-no-external-audit.json") "PMS_EMS_OMS_R018_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r018-forbidden-actions-audit.json") "PMS_EMS_OMS_R018_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$nextPhase = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r018-next-phase-recommendation.json") "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r018-build-test-validator-evidence.json") "PMS_EMS_OMS_R018_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$contract.paperSimulationResultContractCreated) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Result contract missing."
foreach ($model in @("PaperSimulationFixtureResult", "PaperSimulationFixtureResultLine", "PaperSimulationFixtureExecutor", "PaperSimulationFixtureSummary", "PaperSimulationPostTradePreview", "PaperSimulationReconciliationPreview")) {
    Require-True (($contract.models -contains $model)) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Model missing: $model"
}
foreach ($property in @("paperOnly", "noExternal", "nonExecutable", "notAnOrder", "notSubmitted", "noBrokerRoute", "noRealFillCreated", "noExecutionReportCreated", "noOrderCreated", "noLiveStateMutation")) {
    Require-True ([bool]$contract.$property) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_EXECUTABLE" "Contract safety flag missing: $property"
}

Require-True ([bool]$result.paperSimulationResultCreated) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Result artifact missing."
Require-True ([string]$result.resultStatus -eq "CompletedNoExternalFixture") "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Result status wrong."
Require-True ([int]$result.lineCount -eq 3) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Result line count wrong."
Require-True ([int]$result.blockedLineCount -eq 10) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Blocked line count wrong."
foreach ($property in @("paperOnly", "noExternal", "nonExecutable", "notAnOrder", "notSubmitted", "noBrokerRoute", "resultIsPaperOnly", "noRealFillCreated", "noExecutionReportCreated", "noOrderCreated", "noLiveStateMutation")) {
    Require-True ([bool]$result.$property) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_EXECUTABLE" "Result safety flag missing: $property"
}
foreach ($property in @("realFillEntityCreated", "brokerExecutionReportEntityCreated", "omsOrderCreated", "parentOrderCreated", "childOrderCreated", "brokerOrderCreated", "orderStateCreated", "submittedOrders", "calledBrokerGateway", "requestedLiveMarketData", "startedApiOrWorker", "startedSchedulerOrBackgroundJob", "mutatedLiveTradingState", "mutatedLivePositionState", "mutatedBrokerState", "replayOrShadowReplayIntroduced")) {
    Require-False ([bool]$result.$property) "PMS_EMS_OMS_R018_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Unsafe result flag detected: $property"
}

Require-True ([bool]$lines.paperSimulationResultLinesCreated) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Result lines missing."
Require-True ([int]$lines.lineCount -eq 3) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Line count wrong."
Require-True ([bool]$lines.blockedLinesPreservedSeparately) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Blocked lines not preserved."
Require-True (@($lines.lines | Where-Object { $_.normalizedSymbol -eq "AUDUSD" -and $_.side -eq "Buy" -and [decimal]$_.simulatedAppliedQuantity -eq 131000 }).Count -eq 1) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "AUDUSD applied line missing."
Require-True (@($lines.lines | Where-Object { $_.normalizedSymbol -eq "EURUSD" -and $_.side -eq "Buy" -and [decimal]$_.simulatedAppliedQuantity -eq 124000 }).Count -eq 1) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "EURUSD applied line missing."
Require-True (@($lines.lines | Where-Object { $_.normalizedSymbol -eq "GBPUSD" -and $_.side -eq "Sell" -and [decimal]$_.simulatedAppliedQuantity -eq 368000 }).Count -eq 1) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "GBPUSD applied line missing."
foreach ($line in $lines.lines) {
    Require-True ([string]$line.simulatedOutcomeCategory -eq "PaperApplied") "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Line outcome not paper-applied."
    Require-True ([string]$line.simulatedSlippageCategory -eq "FixtureSlippageApplied") "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Line slippage category wrong."
    Require-True ([string]$line.simulatedFeeCategory -eq "FixtureFeeApplied") "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Line fee category wrong."
    foreach ($property in @("resultIsPaperOnly", "noFillCreated", "noExecutionReportCreated", "noOrderCreated", "noBrokerRoute", "nonExecutable")) {
        Require-True ([bool]$line.$property) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_EXECUTABLE" "Line safety flag missing: $property"
    }
    foreach ($property in @("realFillEntityCreated", "brokerExecutionReportEntityCreated", "omsOrderCreated", "parentOrderCreated", "childOrderCreated", "brokerOrderCreated", "orderStateCreated", "submitted")) {
        Require-False ([bool]$line.$property) "PMS_EMS_OMS_R018_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Line unsafe flag detected: $property"
    }
}

Require-True ([bool]$summary.paperSimulationSummaryCreated) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Summary missing."
Require-True ([int]$summary.totalLines -eq 3) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Summary total lines wrong."
Require-True ([int]$summary.simulatedAppliedLines -eq 3) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Applied lines wrong."
Require-True ([int]$summary.blockedLines -eq 10) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Blocked lines wrong."
Require-True ([int]$summary.realFillCount -eq 0) "PMS_EMS_OMS_R018_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Real fill count nonzero."
Require-True ([int]$summary.executionReportCount -eq 0) "PMS_EMS_OMS_R018_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Execution report count nonzero."
Require-True ([int]$summary.orderCount -eq 0) "PMS_EMS_OMS_R018_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order count nonzero."
Require-True ([int]$summary.brokerRouteCount -eq 0) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_SUBMITTED_OR_ROUTED" "Broker route count nonzero."
Require-True ([string]$summary.simulationState -eq "CompletedNoExternalFixture") "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Simulation state wrong."
Require-True ([string]$summary.safetyStatus -eq "PaperSimulationOnly") "PMS_EMS_OMS_R018_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Safety status wrong."

Require-True ([bool]$postTrade.paperPostTradePreviewCreated) "PMS_EMS_OMS_R018_FAIL_LIVE_STATE_MUTATION" "Post-trade preview missing."
Require-True ([bool]$postTrade.simulatedOnly) "PMS_EMS_OMS_R018_FAIL_LIVE_STATE_MUTATION" "Post-trade preview not simulated-only."
Require-False ([bool]$postTrade.livePositionStateMutated) "PMS_EMS_OMS_R018_FAIL_LIVE_STATE_MUTATION" "Live position mutated."
Require-False ([bool]$postTrade.brokerStateMutated) "PMS_EMS_OMS_R018_FAIL_LIVE_STATE_MUTATION" "Broker state mutated."
Require-False ([bool]$postTrade.tradingStateMutated) "PMS_EMS_OMS_R018_FAIL_LIVE_STATE_MUTATION" "Trading state mutated."
Require-True (@($postTrade.positionDeltas | Where-Object { $_.normalizedSymbol -eq "GBPUSD" -and [decimal]$_.paperQuantityDelta -eq -368000 }).Count -eq 1) "PMS_EMS_OMS_R018_FAIL_LIVE_STATE_MUTATION" "GBPUSD simulated sell delta missing."

Require-True ([bool]$reconciliation.paperReconciliationPreviewCreated) "PMS_EMS_OMS_R018_FAIL_LIVE_STATE_MUTATION" "Reconciliation preview missing."
Require-False ([bool]$reconciliation.liveReconciliationClaimCreated) "PMS_EMS_OMS_R018_FAIL_LIVE_STATE_MUTATION" "Live reconciliation claim created."
Require-False ([bool]$reconciliation.livePositionStateMutated) "PMS_EMS_OMS_R018_FAIL_LIVE_STATE_MUTATION" "Reconciliation mutated live position."
Require-False ([bool]$reconciliation.brokerStateMutated) "PMS_EMS_OMS_R018_FAIL_LIVE_STATE_MUTATION" "Reconciliation mutated broker state."
Require-False ([bool]$reconciliation.tradingStateMutated) "PMS_EMS_OMS_R018_FAIL_LIVE_STATE_MUTATION" "Reconciliation mutated trading state."

Require-True ([bool]$assumptions.fixtureAssumptionPreservationCreated) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Assumptions missing."
Require-True ([string]$assumptions.slippageModel -eq "FixtureOnly") "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Slippage not fixture-only."
Require-True ([string]$assumptions.feeModel -eq "FixtureOnly") "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Fee not fixture-only."
Require-True ([string]$assumptions.marketDataSource -eq "FixtureOnly") "PMS_EMS_OMS_R018_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Market data source not fixture-only."
Require-True ([string]$assumptions.executionVenue -eq "None") "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_SUBMITTED_OR_ROUTED" "Execution venue present."
Require-True ([string]$assumptions.brokerRoute -eq "None") "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_SUBMITTED_OR_ROUTED" "Broker route present."
Require-False ([bool]$assumptions.rawMarketDataFixturePayloadsSerialized) "PMS_EMS_OMS_R018_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw fixture payload serialized."
Require-False ([bool]$assumptions.rawBrokerPricesSerialized) "PMS_EMS_OMS_R018_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw broker prices serialized."

Require-True ([bool]$fillAudit.noRealFillAuditCreated) "PMS_EMS_OMS_R018_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "No-real-fill audit missing."
foreach ($property in @("realFillEntityCreated", "fillDomainEntityCreated", "simulationResultLineIsRealFill")) {
    Require-False ([bool]$fillAudit.$property) "PMS_EMS_OMS_R018_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Real fill audit detected: $property"
}
Require-True ([int]$fillAudit.realFillCount -eq 0) "PMS_EMS_OMS_R018_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Real fill count nonzero."

Require-True ([bool]$reportAudit.noExecutionReportAuditCreated) "PMS_EMS_OMS_R018_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "No execution-report audit missing."
foreach ($property in @("brokerExecutionReportEntityCreated", "executionReportDomainEntityCreated", "simulationResultLineIsBrokerExecutionReport")) {
    Require-False ([bool]$reportAudit.$property) "PMS_EMS_OMS_R018_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Execution report audit detected: $property"
}
Require-True ([int]$reportAudit.executionReportCount -eq 0) "PMS_EMS_OMS_R018_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Execution report count nonzero."

Require-True ([bool]$orderAudit.noOrderCreatedAuditCreated) "PMS_EMS_OMS_R018_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No-order audit missing."
foreach ($property in @("omsOrderCreated", "parentOrderCreated", "childOrderCreated", "brokerOrderCreated", "executableOrderCreated", "orderStateCreated", "orderSubmissionPathIntroduced", "ordersSubmitted", "liveTradingStateMutated", "brokerGatewayCalled")) {
    Require-False ([bool]$orderAudit.$property) "PMS_EMS_OMS_R018_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order audit detected: $property"
}

Require-True ([bool]$routeAudit.noRouteNoSubmissionAuditCreated) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_SUBMITTED_OR_ROUTED" "Route/submission audit missing."
foreach ($property in @("brokerRouteCreated", "brokerRouteAssigned", "submissionInstructionCreated", "orderSubmissionPathInvoked", "paperSimulationResultSubmitted", "paperSimulationResultRouteable")) {
    Require-False ([bool]$routeAudit.$property) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_SUBMITTED_OR_ROUTED" "Route/submission audit detected: $property"
}

Require-True ([bool]$stateAudit.noLiveStateMutationAuditCreated) "PMS_EMS_OMS_R018_FAIL_LIVE_STATE_MUTATION" "No-live-state audit missing."
foreach ($property in @("liveTradingStateMutated", "livePositionStateMutated", "brokerStateMutated", "liveReconciliationStateMutated")) {
    Require-False ([bool]$stateAudit.$property) "PMS_EMS_OMS_R018_FAIL_LIVE_STATE_MUTATION" "Live state mutation detected: $property"
}
Require-True ([bool]$stateAudit.postTradePreviewIsSimulatedOnly) "PMS_EMS_OMS_R018_FAIL_LIVE_STATE_MUTATION" "Post-trade preview not simulated-only."

Require-True ([bool]$idempotency.idempotencyEvidenceCreated) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Idempotency missing."
Require-True ([string]$idempotency.idempotencyKey -eq "PaperSimulationResultId") "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Wrong idempotency key."
Require-True ([string]$idempotency.duplicateResultBehavior -eq "DuplicateReturned") "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Duplicate behavior missing."
Require-False ([bool]$idempotency.duplicatesCreateAdditionalSimulationResults) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Duplicate simulation results created."

Require-True ([bool]$risk.riskLineagePreservationCreated) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Risk lineage missing."
Require-False ([bool]$risk.riskLineageMissing) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Risk lineage marked missing."
Require-True ([bool]$qubes.qubesLineagePreservationCreated) "PMS_EMS_OMS_R018_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage missing."
Require-True ([string]$qubes.sourceSystem -eq "ModelWeightSourceSystem.Qubes") "PMS_EMS_OMS_R018_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes source missing."
Require-True ([int]$qubes.cadenceMinutes -eq 15) "PMS_EMS_OMS_R018_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes cadence missing."
Require-False ([bool]$qubes.qubesLineageWeakened) "PMS_EMS_OMS_R018_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage weakened."
foreach ($lineage in @($operatorLineage, $planLineage, $candidateLineage, $rebalance, $lotSizing, $marks, $drift)) {
    $createdProperty = $lineage.PSObject.Properties.Name | Where-Object { $_ -match "Created$" } | Select-Object -First 1
    if ($createdProperty) {
        Require-True ([bool]$lineage.$createdProperty) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Lineage artifact missing: $createdProperty"
    }
}
Require-True ([bool]$operatorLineage.blockedLinesAcknowledged) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Blocked line acknowledgement missing."
Require-True ([bool]$operatorLineage.missingStaleMarksAcknowledged) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Missing/stale acknowledgement missing."
Require-True ([bool]$operatorLineage.driftAcknowledged) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Drift acknowledgement missing."
Require-True ([bool]$rebalance.rebalanceIntentsRemainNonExecutable) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_EXECUTABLE" "Rebalance intents executable."
Require-False ([bool]$rebalance.rebalanceIntentCreatesOrder) "PMS_EMS_OMS_R018_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Rebalance creates order."
Require-False ([bool]$marks.rawMarketDataFixturePayloadsSerialized) "PMS_EMS_OMS_R018_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw fixture payloads serialized."
Require-False ([bool]$marks.fabricatedMarksForPaperSimulation) "PMS_EMS_OMS_R018_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Marks fabricated."
Require-False ([bool]$drift.liveTradingApprovalCreated) "PMS_EMS_OMS_R018_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Live trading approval created."

Require-True ([bool]$universe.instrumentUniverseHandlingCreated) "PMS_EMS_OMS_R018_FAIL_LMAX_GAP_BLOCKS_SIMULATION" "Universe handling missing."
Require-False ([bool]$universe.lmaxLiveValidationGapsBlockPaperSimulationFixture) "PMS_EMS_OMS_R018_FAIL_LMAX_GAP_BLOCKS_SIMULATION" "LMAX gaps block paper simulation."
Require-False ([bool]$universe.audusdClassifiedAsFailed) "PMS_EMS_OMS_R018_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD failed."
Require-False ([bool]$universe.audusdLiveValidationGapBlocksPaperSimulationFixture) "PMS_EMS_OMS_R018_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD gap blocks paper simulation."
Require-False ([bool]$universe.usdjpyLiveValidationGapBlocksPaperSimulationFixture) "PMS_EMS_OMS_R018_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY gap blocks paper simulation."
Require-True ([bool]$usdjpy.usdJpyCaveatPreservationCreated) "PMS_EMS_OMS_R018_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
Require-True ([string]$usdjpy.usdJpySecurityId -eq "4004") "PMS_EMS_OMS_R018_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID missing."
Require-True ([string]$usdjpy.usdJpySecurityIdSource -eq "8") "PMS_EMS_OMS_R018_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource missing."
Require-False ([bool]$usdjpy.usdJpyClassifiedAsFailed) "PMS_EMS_OMS_R018_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY failed."
Require-False ([bool]$usdjpy.audusdClassifiedAsFailed) "PMS_EMS_OMS_R018_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD failed."
Require-True ([bool]$lmax.lmaxReadonlyBaselineReferenceCreated) "PMS_EMS_OMS_R018_FAIL_LMAX_GAP_BLOCKS_SIMULATION" "LMAX baseline missing."
Require-False ([bool]$lmax.lmaxUsedInThisPhase) "PMS_EMS_OMS_R018_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX used."
Require-False ([bool]$lmax.lmaxCalledInThisPhase) "PMS_EMS_OMS_R018_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX called."
Require-False ([bool]$lmax.lmaxLiveValidationGapsBlockPaperSimulationFixture) "PMS_EMS_OMS_R018_FAIL_LMAX_GAP_BLOCKS_SIMULATION" "LMAX gaps block paper simulation."

foreach ($property in @(
    "externalBrokerActivationDetected",
    "socketTlsFixMarketDataRuntimeActionDetected",
    "marketDataRequestAttempted",
    "liveMarketDataResponseRead",
    "apiStarted",
    "workerStarted",
    "schedulerPollingServiceTimerBackgroundJobStartedOrIntroduced",
    "liveGatewayEnabled",
    "orderSubmissionIntroduced",
    "executableOrderCreated",
    "omsOrderCreated",
    "parentOrderCreated",
    "childOrderCreated",
    "brokerOrderCreated",
    "orderStateCreated",
    "realFillEntityCreated",
    "brokerExecutionReportEntityCreated",
    "fillCreatedAsRealOrderDomainEntity",
    "executionReportCreatedAsBrokerDomainEntity",
    "liveTradingPathIntroduced",
    "liveTradingStateMutated",
    "livePositionStateMutated",
    "brokerStateMutated",
    "replayOrShadowReplayIntroduced",
    "secretsOrCredentialsSerialized",
    "rawFixSerialized",
    "rawEndpointTlsValuesSerialized",
    "sessionIdsSerialized",
    "compIdsSerialized",
    "rawMdReqIdSerialized",
    "rawBrokerMarketDataPayloadsSerialized",
    "rawBrokerMarketDataPricesSerialized",
    "rawMarketDataFixturePayloadsSerialized",
    "paperSimulationResultExecutable",
    "paperSimulationResultSubmitted",
    "paperSimulationResultHasBrokerRoute",
    "lmaxLiveValidationGapsBlockPaperSimulationFixture"
)) {
    Require-False ([bool]$noExternal.$property) "PMS_EMS_OMS_R018_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected: $property"
}

foreach ($item in $forbidden.forbiddenActions) {
    if ([bool]$item.detected) {
        if ([string]$item.action -match "scheduler|service|timer|background") {
            Fail-Gate "PMS_EMS_OMS_R018_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "fill|execution report") {
            Fail-Gate "PMS_EMS_OMS_R018_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "state mutation") {
            Fail-Gate "PMS_EMS_OMS_R018_FAIL_LIVE_STATE_MUTATION" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "order|trading|executable|OMS|parent|child|broker|submitted|routed") {
            Fail-Gate "PMS_EMS_OMS_R018_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden action detected: $($item.action)"
        }

        Fail-Gate "PMS_EMS_OMS_R018_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $($item.action)"
    }
}

Require-True ([bool]$nextPhase.nextPhaseRecommendationCreated) "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Next phase missing."
Require-True ([string]$nextPhase.recommendedNextPhase -eq "PMS-EMS-OMS-R019") "PMS_EMS_OMS_R018_FAIL_SIMULATION_RESULT_CONTRACT_MISSING" "Next phase is not R019."
Require-True ([bool]$nextPhase.mustRemainNoExternal) "PMS_EMS_OMS_R018_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase not no-external."

$apiSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Api/appsettings.json") "PMS_EMS_OMS_R018_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$workerSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/appsettings.json") "PMS_EMS_OMS_R018_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-False ([bool]$apiSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R018_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API live trading enabled."
Require-False ([bool]$workerSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R018_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker live trading enabled."
Require-False ([bool]$apiSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R018_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API external enabled."
Require-False ([bool]$workerSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R018_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker external enabled."
Require-True ([bool]$apiSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R018_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API fake gateway not required."
Require-True ([bool]$workerSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R018_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker fake gateway not required."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.Enabled) "PMS_EMS_OMS_R018_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX runtime enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowExternalConnections) "PMS_EMS_OMS_R018_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX external enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowOrderSubmission) "PMS_EMS_OMS_R018_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "LMAX order submission enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SchedulerEnabled) "PMS_EMS_OMS_R018_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "LMAX scheduler enabled."
Require-False ([bool]$workerSettings.MarketDataBars.Enabled) "PMS_EMS_OMS_R018_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker market data bars enabled."

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-pms-ems-oms-r018-*" -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combined = [string]::Join("`n", $artifactText)
foreach ($pattern in @("\u0001", "35=", "MDReqID\s*[:=]", "SenderCompID\s*[:=]", "TargetCompID\s*[:=]", "BeginString\s*[:=]", "SocketHost\s*[:=]", "TlsHost\s*[:=]", "Password\s*[:=]", "ApiKey\s*[:=]", "Bearer\s+[A-Za-z0-9_\.-]+", "rawBid", "rawAsk", "rawMid")) {
    if ($combined -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R018_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Unsafe serialized content pattern found: $pattern"
    }
}

$requiredFiles = @(
    "src/QQ.Production.Intraday.Application/QubesPaperSimulationFixtureExecution.cs",
    "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperSimulationFixtureExecutionTests.cs"
)
foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $file))) {
        Fail-Gate "PMS_EMS_OMS_R018_FAIL_BUILD_OR_TESTS" "Required file missing: $file"
    }
}

$source = Get-Content -LiteralPath (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/QubesPaperSimulationFixtureExecution.cs") -Raw
foreach ($pattern in @("TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "SendOrderAsync", "SubmitOrder", "FixSession")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R018_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "R018 source contains forbidden runtime pattern: $pattern"
    }
}
foreach ($pattern in @("AddHostedService", "IHostedService", "BackgroundService", "PeriodicTimer", "Task.Delay", "System.Threading.Timer")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R018_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "R018 source contains scheduler/service pattern: $pattern"
    }
}

$tests = Get-Content -LiteralPath (Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperSimulationFixtureExecutionTests.cs") -Raw
foreach ($requiredTestName in @(
    "R016_R017_simulation_ready_plan_can_produce_no_external_paper_simulation_fixture_result",
    "Audusd_buy_line_is_simulated_as_paper_only",
    "Eurusd_buy_line_is_simulated_as_paper_only",
    "Gbpusd_sell_line_is_simulated_as_paper_only",
    "Simulated_applied_quantities_match_paper_quantities",
    "Simulation_summary_reports_real_fill_count_zero",
    "Execution_report_count_is_zero",
    "Order_count_is_zero",
    "Broker_route_count_is_zero",
    "No_real_fill_entities_are_created",
    "No_execution_report_entities_are_created",
    "No_oms_parent_child_or_broker_order_is_created",
    "No_order_state_is_created",
    "No_order_submission_path_is_introduced",
    "Post_trade_preview_is_simulated_only_and_does_not_mutate_live_state",
    "Blocked_lines_are_preserved_separately",
    "Missing_stale_mark_warnings_are_preserved",
    "Drift_acknowledgement_is_preserved",
    "Duplicate_simulation_result_handling_is_idempotent",
    "Qubes_cycle_operator_plan_candidate_risk_rebalance_and_lot_sizing_lineage_is_preserved",
    "Simulation_fixture_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action",
    "Api_and_worker_live_gateway_remain_disabled",
    "Simulation_fixture_source_introduces_no_scheduler_timer_polling_or_background_job",
    "Audusd_is_not_misclassified_as_failed",
    "Usdjpy_caveat_remains_preserved"
)) {
    if ($tests -notmatch [regex]::Escape($requiredTestName)) {
        Fail-Gate "PMS_EMS_OMS_R018_FAIL_BUILD_OR_TESTS" "Focused test missing: $requiredTestName"
    }
}

Require-True ([string]$evidence.build.status -eq "PASS") "PMS_EMS_OMS_R018_FAIL_BUILD_OR_TESTS" "Build evidence missing or not PASS."
Require-True ([int]$evidence.build.failed -eq 0) "PMS_EMS_OMS_R018_FAIL_BUILD_OR_TESTS" "Build evidence has failures."
Require-True ([string]$evidence.focusedTests.status -eq "PASS") "PMS_EMS_OMS_R018_FAIL_BUILD_OR_TESTS" "Focused test evidence missing or not PASS."
Require-True ([int]$evidence.focusedTests.failed -eq 0) "PMS_EMS_OMS_R018_FAIL_BUILD_OR_TESTS" "Focused tests have failures."
Require-True ([string]$evidence.unitTests.status -eq "PASS") "PMS_EMS_OMS_R018_FAIL_BUILD_OR_TESTS" "Unit test evidence missing or not PASS."
Require-True ([int]$evidence.unitTests.failed -eq 0) "PMS_EMS_OMS_R018_FAIL_BUILD_OR_TESTS" "Unit tests have failures."
Require-True ([string]$evidence.validator.status -eq "PASS") "PMS_EMS_OMS_R018_FAIL_BUILD_OR_TESTS" "Validator evidence missing or not PASS."
Require-True ([int]$evidence.validator.failed -eq 0) "PMS_EMS_OMS_R018_FAIL_BUILD_OR_TESTS" "Validator evidence has failures."
Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R018_FAIL_BUILD_OR_TESTS" "Evidence marker missing."

Write-Host "PMS_EMS_OMS_R018_PASS_PAPER_SIMULATION_FIXTURE_RESULT_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R018_PASS_NO_REAL_FILL_NO_ORDER_SIMULATION_GATE_READY_NO_EXTERNAL"
