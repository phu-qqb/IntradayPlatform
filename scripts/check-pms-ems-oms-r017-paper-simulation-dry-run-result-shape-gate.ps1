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
    "phase-pms-ems-oms-r017-summary.md" = "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r017-dry-run-result-contract.json" = "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r017-dry-run-result-shape.json" = "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r017-dry-run-result-lines.json" = "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r017-dry-run-summary.json" = "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r017-assumption-preservation.json" = "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r017-simulation-not-run-audit.json" = "PMS_EMS_OMS_R017_FAIL_SIMULATION_RAN"
    "phase-pms-ems-oms-r017-no-fill-no-execution-report-audit.json" = "PMS_EMS_OMS_R017_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
    "phase-pms-ems-oms-r017-no-order-created-audit.json" = "PMS_EMS_OMS_R017_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r017-no-route-no-submission-audit.json" = "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_SUBMITTED_OR_ROUTED"
    "phase-pms-ems-oms-r017-idempotency-evidence.json" = "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r017-risk-lineage-preservation.json" = "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r017-qubes-lineage-preservation.json" = "PMS_EMS_OMS_R017_FAIL_QUBES_LINEAGE_WEAKENED"
    "phase-pms-ems-oms-r017-operator-decision-lineage-preservation.json" = "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r017-plan-lineage-preservation.json" = "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r017-paper-candidate-lineage-preservation.json" = "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r017-rebalance-intent-lineage-preservation.json" = "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r017-lot-sizing-lineage-preservation.json" = "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r017-missing-stale-mark-preservation.json" = "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r017-drift-acknowledgement-preservation.json" = "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r017-instrument-universe-handling.json" = "PMS_EMS_OMS_R017_FAIL_LMAX_GAP_BLOCKS_DRY_RUN_RESULT"
    "phase-pms-ems-oms-r017-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R017_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r017-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R017_FAIL_LMAX_GAP_BLOCKS_DRY_RUN_RESULT"
    "phase-pms-ems-oms-r017-no-external-audit.json" = "PMS_EMS_OMS_R017_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r017-forbidden-actions-audit.json" = "PMS_EMS_OMS_R017_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r017-next-phase-recommendation.json" = "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
    "phase-pms-ems-oms-r017-build-test-validator-evidence.json" = "PMS_EMS_OMS_R017_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $entry.Key))) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$contract = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r017-dry-run-result-contract.json") "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
$shape = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r017-dry-run-result-shape.json") "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
$lines = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r017-dry-run-result-lines.json") "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
$summary = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r017-dry-run-summary.json") "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
$assumptions = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r017-assumption-preservation.json") "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
$noSimulation = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r017-simulation-not-run-audit.json") "PMS_EMS_OMS_R017_FAIL_SIMULATION_RAN"
$fillAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r017-no-fill-no-execution-report-audit.json") "PMS_EMS_OMS_R017_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$orderAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r017-no-order-created-audit.json") "PMS_EMS_OMS_R017_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$routeAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r017-no-route-no-submission-audit.json") "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_SUBMITTED_OR_ROUTED"
$idempotency = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r017-idempotency-evidence.json") "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
$risk = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r017-risk-lineage-preservation.json") "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
$qubes = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r017-qubes-lineage-preservation.json") "PMS_EMS_OMS_R017_FAIL_QUBES_LINEAGE_WEAKENED"
$operatorLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r017-operator-decision-lineage-preservation.json") "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
$planLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r017-plan-lineage-preservation.json") "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
$candidateLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r017-paper-candidate-lineage-preservation.json") "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
$rebalance = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r017-rebalance-intent-lineage-preservation.json") "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
$lotSizing = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r017-lot-sizing-lineage-preservation.json") "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
$marks = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r017-missing-stale-mark-preservation.json") "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
$drift = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r017-drift-acknowledgement-preservation.json") "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r017-instrument-universe-handling.json") "PMS_EMS_OMS_R017_FAIL_LMAX_GAP_BLOCKS_DRY_RUN_RESULT"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r017-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R017_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r017-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R017_FAIL_LMAX_GAP_BLOCKS_DRY_RUN_RESULT"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r017-no-external-audit.json") "PMS_EMS_OMS_R017_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r017-forbidden-actions-audit.json") "PMS_EMS_OMS_R017_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$nextPhase = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r017-next-phase-recommendation.json") "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r017-build-test-validator-evidence.json") "PMS_EMS_OMS_R017_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$contract.dryRunResultContractCreated) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "Dry-run result contract missing."
foreach ($model in @("PaperSimulationDryRunResult", "PaperSimulationDryRunLineResult", "PaperSimulationDryRunSummary", "PaperSimulationDryRunStatus", "PaperSimulationDryRunAssumptionReport")) {
    Require-True (($contract.models -contains $model)) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "Model missing: $model"
}
foreach ($field in @("PaperSimulationPlanId", "PaperExecutionPlanId", "CycleRunId", "QubesRunId", "OperatorDecisionId", "PaperOrderCandidateBatchId", "PaperExecutionPlanLineIds", "RiskReviewReference", "LotSizingReference")) {
    Require-True (($contract.resultFields -contains $field)) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "Result field missing: $field"
}
foreach ($property in @("paperOnly", "noExternal", "nonExecutable", "notAnOrder", "notSubmitted", "noBrokerRoute", "simulationNotRun", "noFillCreated", "noExecutionReportCreated", "noOrderCreated")) {
    Require-True ([bool]$contract.$property) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_EXECUTABLE" "Contract safety flag missing: $property"
}

Require-True ([bool]$shape.dryRunResultShapeCreated) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "Dry-run result shape missing."
Require-True ([string]$shape.resultStatus -eq "DryRunResultShapeReady") "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "Result status is not ready."
Require-True ([int]$shape.lineCount -eq 3) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "Result line count wrong."
Require-True ([int]$shape.blockedLineCount -eq 10) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "Blocked line count wrong."
foreach ($property in @("paperOnly", "noExternal", "nonExecutable", "notAnOrder", "notSubmitted", "noBrokerRoute", "simulationNotRun", "noFillCreated", "noExecutionReportCreated", "noOrderCreated")) {
    Require-True ([bool]$shape.$property) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_EXECUTABLE" "Result safety flag missing: $property"
}
foreach ($property in @("createdOmsOrder", "createdParentOrder", "createdChildOrder", "createdBrokerOrder", "createdOrderState", "createdFill", "createdExecutionReport", "submittedOrders", "calledBrokerGateway", "requestedLiveMarketData", "startedApiOrWorker", "startedSchedulerOrBackgroundJob", "ranPaperSimulation", "mutatedLiveTradingState")) {
    Require-False ([bool]$shape.$property) "PMS_EMS_OMS_R017_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Unsafe result flag detected: $property"
}

Require-True ([bool]$lines.dryRunResultLinesCreated) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "Dry-run result lines missing."
Require-True ([int]$lines.lineCount -eq 3) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "Line count wrong."
Require-True ([bool]$lines.blockedLinesPreservedSeparately) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "Blocked lines not preserved."
Require-True (@($lines.lines | Where-Object { $_.normalizedSymbol -eq "AUDUSD" -and $_.side -eq "Buy" -and [decimal]$_.paperBaseQuantity -eq 131000 }).Count -eq 1) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "AUDUSD line missing."
Require-True (@($lines.lines | Where-Object { $_.normalizedSymbol -eq "EURUSD" -and $_.side -eq "Buy" -and [decimal]$_.paperBaseQuantity -eq 124000 }).Count -eq 1) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "EURUSD line missing."
Require-True (@($lines.lines | Where-Object { $_.normalizedSymbol -eq "GBPUSD" -and $_.side -eq "Sell" -and [decimal]$_.paperBaseQuantity -eq 368000 }).Count -eq 1) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "GBPUSD line missing."
foreach ($line in $lines.lines) {
    Require-True ([string]$line.simulatedOutcomeCategory -eq "SimulationNotRun") "PMS_EMS_OMS_R017_FAIL_SIMULATION_RAN" "Line outcome is simulated."
    Require-True ([string]$line.simulatedFillStatus -eq "NoFillCreated") "PMS_EMS_OMS_R017_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Line fill status is unsafe."
    Require-True ([string]$line.resultLineStatus -eq "ResultLineNotSimulated") "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "Line status wrong."
    foreach ($property in @("simulationNotRun", "noFillCreated", "noExecutionReportCreated", "noOrderCreated", "noBrokerRoute", "nonExecutable")) {
        Require-True ([bool]$line.$property) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_EXECUTABLE" "Line safety flag missing: $property"
    }
}

Require-True ([bool]$summary.dryRunSummaryCreated) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "Dry-run summary missing."
Require-True ([int]$summary.totalLines -eq 3) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "Summary total lines wrong."
Require-True ([int]$summary.readyLines -eq 3) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "Summary ready lines wrong."
Require-True ([int]$summary.blockedLines -eq 10) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "Summary blocked lines wrong."
Require-True ([string]$summary.simulationState -eq "NotRun") "PMS_EMS_OMS_R017_FAIL_SIMULATION_RAN" "Summary says simulation ran."
Require-True ([int]$summary.fillCount -eq 0) "PMS_EMS_OMS_R017_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill count nonzero."
Require-True ([int]$summary.executionReportCount -eq 0) "PMS_EMS_OMS_R017_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Execution report count nonzero."
Require-True ([int]$summary.orderCount -eq 0) "PMS_EMS_OMS_R017_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order count nonzero."
Require-True ([int]$summary.brokerRouteCount -eq 0) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_SUBMITTED_OR_ROUTED" "Broker route count nonzero."
Require-True ([string]$summary.safetyStatus -eq "NoExternalResultShapeOnly") "PMS_EMS_OMS_R017_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Safety status wrong."

Require-True ([bool]$assumptions.assumptionPreservationCreated) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "Assumption preservation missing."
Require-True ([string]$assumptions.simulationMode -eq "PaperNoExternal") "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "Simulation mode wrong."
Require-True ([string]$assumptions.fillModel -eq "NotRunYet") "PMS_EMS_OMS_R017_FAIL_SIMULATION_RAN" "Fill model is not NotRunYet."
Require-True ([string]$assumptions.marketDataSource -eq "FixtureOnly") "PMS_EMS_OMS_R017_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Market data source is not fixture-only."
Require-True ([string]$assumptions.executionVenue -eq "None") "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_SUBMITTED_OR_ROUTED" "Execution venue present."
Require-True ([string]$assumptions.brokerRoute -eq "None") "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_SUBMITTED_OR_ROUTED" "Broker route present."
Require-False ([bool]$assumptions.rawMarketDataFixturePayloadsSerialized) "PMS_EMS_OMS_R017_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw fixture payload serialized."
Require-False ([bool]$assumptions.rawBrokerPricesSerialized) "PMS_EMS_OMS_R017_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw broker prices serialized."

Require-True ([bool]$noSimulation.simulationNotRunAuditCreated) "PMS_EMS_OMS_R017_FAIL_SIMULATION_RAN" "Simulation-not-run audit missing."
foreach ($property in @("paperSimulationRan", "simulationEngineInvoked", "simulationClockAdvanced", "simulationStateCreated", "simulationFillCreated", "simulationExecutionReportCreated")) {
    Require-False ([bool]$noSimulation.$property) "PMS_EMS_OMS_R017_FAIL_SIMULATION_RAN" "Simulation audit detected: $property"
}
Require-True ([bool]$noSimulation.dryRunResultShapePreparedOnly) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "Prepared-only flag missing."

Require-True ([bool]$fillAudit.noFillNoExecutionReportAuditCreated) "PMS_EMS_OMS_R017_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill/report audit missing."
foreach ($property in @("fillCreated", "executionReportCreated", "simulationFillCreated", "simulationExecutionReportCreated", "dryRunResultCreatesFillsOrExecutionReports", "lineCreatesFill", "lineCreatesExecutionReport")) {
    Require-False ([bool]$fillAudit.$property) "PMS_EMS_OMS_R017_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill/report audit detected: $property"
}

Require-True ([bool]$orderAudit.noOrderCreatedAuditCreated) "PMS_EMS_OMS_R017_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No-order audit missing."
foreach ($property in @("omsOrderCreated", "parentOrderCreated", "childOrderCreated", "brokerOrderCreated", "executableOrderCreated", "orderStateCreated", "orderSubmissionPathIntroduced", "ordersSubmitted", "liveTradingStateMutated", "brokerGatewayCalled")) {
    Require-False ([bool]$orderAudit.$property) "PMS_EMS_OMS_R017_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order audit detected: $property"
}

Require-True ([bool]$routeAudit.noRouteNoSubmissionAuditCreated) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_SUBMITTED_OR_ROUTED" "Route/submission audit missing."
foreach ($property in @("brokerRouteCreated", "brokerRouteAssigned", "submissionInstructionCreated", "orderSubmissionPathInvoked", "dryRunResultSubmitted", "dryRunResultRouteable")) {
    Require-False ([bool]$routeAudit.$property) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_SUBMITTED_OR_ROUTED" "Route/submission audit detected: $property"
}

Require-True ([bool]$idempotency.idempotencyEvidenceCreated) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "Idempotency missing."
Require-True ([string]$idempotency.idempotencyKey -eq "PaperSimulationDryRunResultId") "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "Wrong idempotency key."
Require-True ([string]$idempotency.duplicateResultBehavior -eq "DuplicateReturned") "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "Duplicate behavior missing."
Require-False ([bool]$idempotency.duplicatesCreateAdditionalDryRunResults) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "Duplicate dry-run results created."

Require-True ([bool]$risk.riskLineagePreservationCreated) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "Risk lineage missing."
Require-False ([bool]$risk.riskLineageMissing) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "Risk lineage marked missing."
Require-True ([bool]$qubes.qubesLineagePreservationCreated) "PMS_EMS_OMS_R017_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage missing."
Require-True ([string]$qubes.sourceSystem -eq "ModelWeightSourceSystem.Qubes") "PMS_EMS_OMS_R017_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes source missing."
Require-True ([int]$qubes.cadenceMinutes -eq 15) "PMS_EMS_OMS_R017_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes cadence missing."
Require-False ([bool]$qubes.qubesLineageWeakened) "PMS_EMS_OMS_R017_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage weakened."
Require-True ([bool]$operatorLineage.operatorDecisionLineagePreservationCreated) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "Operator lineage missing."
foreach ($property in @("blockedLinesAcknowledged", "missingStaleMarksAcknowledged", "driftAcknowledged", "operatorDecisionLineagePreserved")) {
    Require-True ([bool]$operatorLineage.$property) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "Operator lineage flag missing: $property"
}
foreach ($lineage in @($planLineage, $candidateLineage, $rebalance, $lotSizing, $marks, $drift)) {
    $createdProperty = $lineage.PSObject.Properties.Name | Where-Object { $_ -match "Created$" } | Select-Object -First 1
    if ($createdProperty) {
        Require-True ([bool]$lineage.$createdProperty) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "Lineage artifact missing: $createdProperty"
    }
}
Require-True ([bool]$rebalance.rebalanceIntentsRemainNonExecutable) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_EXECUTABLE" "Rebalance intents executable."
Require-False ([bool]$rebalance.rebalanceIntentCreatesOrder) "PMS_EMS_OMS_R017_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Rebalance creates order."
Require-False ([bool]$marks.rawMarketDataFixturePayloadsSerialized) "PMS_EMS_OMS_R017_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw fixture payloads serialized."
Require-False ([bool]$marks.fabricatedMarksForDryRunResult) "PMS_EMS_OMS_R017_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Marks fabricated."
Require-False ([bool]$drift.liveTradingApprovalCreated) "PMS_EMS_OMS_R017_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Live trading approval created."

Require-True ([bool]$universe.instrumentUniverseHandlingCreated) "PMS_EMS_OMS_R017_FAIL_LMAX_GAP_BLOCKS_DRY_RUN_RESULT" "Universe handling missing."
Require-False ([bool]$universe.lmaxLiveValidationGapsBlockDryRunResult) "PMS_EMS_OMS_R017_FAIL_LMAX_GAP_BLOCKS_DRY_RUN_RESULT" "LMAX gaps block dry-run result."
Require-False ([bool]$universe.audusdClassifiedAsFailed) "PMS_EMS_OMS_R017_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD failed."
Require-False ([bool]$universe.audusdLiveValidationGapBlocksDryRunResult) "PMS_EMS_OMS_R017_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD gap blocks dry-run result."
Require-False ([bool]$universe.usdjpyLiveValidationGapBlocksDryRunResult) "PMS_EMS_OMS_R017_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY gap blocks dry-run result."
Require-True ([bool]$usdjpy.usdJpyCaveatPreservationCreated) "PMS_EMS_OMS_R017_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
Require-True ([string]$usdjpy.usdJpySecurityId -eq "4004") "PMS_EMS_OMS_R017_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID missing."
Require-True ([string]$usdjpy.usdJpySecurityIdSource -eq "8") "PMS_EMS_OMS_R017_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource missing."
Require-False ([bool]$usdjpy.usdJpyClassifiedAsFailed) "PMS_EMS_OMS_R017_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY failed."
Require-False ([bool]$usdjpy.audusdClassifiedAsFailed) "PMS_EMS_OMS_R017_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD failed."
Require-True ([bool]$lmax.lmaxReadonlyBaselineReferenceCreated) "PMS_EMS_OMS_R017_FAIL_LMAX_GAP_BLOCKS_DRY_RUN_RESULT" "LMAX baseline missing."
Require-False ([bool]$lmax.lmaxUsedInThisPhase) "PMS_EMS_OMS_R017_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX used."
Require-False ([bool]$lmax.lmaxCalledInThisPhase) "PMS_EMS_OMS_R017_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX called."
Require-False ([bool]$lmax.lmaxLiveValidationGapsBlockDryRunResult) "PMS_EMS_OMS_R017_FAIL_LMAX_GAP_BLOCKS_DRY_RUN_RESULT" "LMAX gaps block dry-run result."

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
    "fillCreated",
    "executionReportCreated",
    "simulationFillCreated",
    "simulationExecutionReportCreated",
    "simulationRan",
    "liveTradingPathIntroduced",
    "liveTradingStateMutated",
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
    "dryRunResultExecutable",
    "dryRunResultSubmitted",
    "dryRunResultHasBrokerRoute",
    "dryRunResultCreatesFillsOrExecutionReports",
    "lmaxLiveValidationGapsBlockDryRunResult"
)) {
    Require-False ([bool]$noExternal.$property) "PMS_EMS_OMS_R017_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected: $property"
}

foreach ($item in $forbidden.forbiddenActions) {
    if ([bool]$item.detected) {
        if ([string]$item.action -match "scheduler|service|timer|background") {
            Fail-Gate "PMS_EMS_OMS_R017_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "simulation") {
            Fail-Gate "PMS_EMS_OMS_R017_FAIL_SIMULATION_RAN" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "fill|execution report") {
            Fail-Gate "PMS_EMS_OMS_R017_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "order|trading|executable|OMS|parent|child|broker|submitted|routed") {
            Fail-Gate "PMS_EMS_OMS_R017_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden action detected: $($item.action)"
        }

        Fail-Gate "PMS_EMS_OMS_R017_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $($item.action)"
    }
}

Require-True ([bool]$nextPhase.nextPhaseRecommendationCreated) "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "Next phase missing."
Require-True ([string]$nextPhase.recommendedNextPhase -eq "PMS-EMS-OMS-R018") "PMS_EMS_OMS_R017_FAIL_DRY_RUN_RESULT_CONTRACT_MISSING" "Next phase is not R018."
Require-True ([bool]$nextPhase.mustRemainNoExternal) "PMS_EMS_OMS_R017_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase not no-external."

$apiSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Api/appsettings.json") "PMS_EMS_OMS_R017_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$workerSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/appsettings.json") "PMS_EMS_OMS_R017_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-False ([bool]$apiSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R017_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API live trading enabled."
Require-False ([bool]$workerSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R017_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker live trading enabled."
Require-False ([bool]$apiSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R017_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API external enabled."
Require-False ([bool]$workerSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R017_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker external enabled."
Require-True ([bool]$apiSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R017_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API fake gateway not required."
Require-True ([bool]$workerSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R017_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker fake gateway not required."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.Enabled) "PMS_EMS_OMS_R017_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX runtime enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowExternalConnections) "PMS_EMS_OMS_R017_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX external enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowOrderSubmission) "PMS_EMS_OMS_R017_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "LMAX order submission enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SchedulerEnabled) "PMS_EMS_OMS_R017_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "LMAX scheduler enabled."
Require-False ([bool]$workerSettings.MarketDataBars.Enabled) "PMS_EMS_OMS_R017_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker market data bars enabled."

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-pms-ems-oms-r017-*" -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combined = [string]::Join("`n", $artifactText)
foreach ($pattern in @("\u0001", "35=", "MDReqID\s*[:=]", "SenderCompID\s*[:=]", "TargetCompID\s*[:=]", "BeginString\s*[:=]", "SocketHost\s*[:=]", "TlsHost\s*[:=]", "Password\s*[:=]", "ApiKey\s*[:=]", "Bearer\s+[A-Za-z0-9_\.-]+", "rawBid", "rawAsk", "rawMid")) {
    if ($combined -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R017_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Unsafe serialized content pattern found: $pattern"
    }
}

$requiredFiles = @(
    "src/QQ.Production.Intraday.Application/QubesPaperSimulationDryRunResultShape.cs",
    "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperSimulationDryRunResultShapeTests.cs"
)
foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $file))) {
        Fail-Gate "PMS_EMS_OMS_R017_FAIL_BUILD_OR_TESTS" "Required file missing: $file"
    }
}

$source = Get-Content -LiteralPath (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/QubesPaperSimulationDryRunResultShape.cs") -Raw
foreach ($pattern in @("TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "SendOrderAsync", "SubmitOrder", "FixSession")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R017_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "R017 source contains forbidden runtime pattern: $pattern"
    }
}
foreach ($pattern in @("AddHostedService", "IHostedService", "BackgroundService", "PeriodicTimer", "Task.Delay", "System.Threading.Timer")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R017_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "R017 source contains scheduler/service pattern: $pattern"
    }
}

$tests = Get-Content -LiteralPath (Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperSimulationDryRunResultShapeTests.cs") -Raw
foreach ($requiredTestName in @(
    "R016_paper_simulation_plan_can_produce_dry_run_result_shape",
    "Result_shape_preserves_paper_simulation_plan_id",
    "Result_shape_preserves_paper_execution_plan_id",
    "Result_shape_preserves_cycle_run_id_and_qubes_run_id",
    "Result_shape_preserves_operator_decision_id",
    "Result_shape_preserves_candidate_risk_rebalance_and_lot_sizing_lineage",
    "Audusd_buy_line_appears_in_result_shape",
    "Eurusd_buy_line_appears_in_result_shape",
    "Gbpusd_sell_line_appears_in_result_shape",
    "Simulation_state_remains_simulation_not_run",
    "Fill_count_is_zero",
    "Execution_report_count_is_zero",
    "Order_count_is_zero",
    "Broker_route_count_is_zero",
    "No_fills_are_created",
    "No_execution_reports_are_created",
    "No_oms_parent_child_or_broker_order_is_created",
    "No_order_submission_path_is_introduced",
    "Assumption_set_remains_fixture_only_and_no_external",
    "Blocked_lines_are_preserved_separately",
    "Missing_stale_mark_warnings_are_preserved",
    "Drift_acknowledgement_is_preserved",
    "Duplicate_dry_run_result_handling_is_idempotent",
    "Dry_run_result_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action",
    "Api_and_worker_live_gateway_remain_disabled",
    "Dry_run_result_source_introduces_no_scheduler_timer_polling_or_background_job",
    "Audusd_is_not_misclassified_as_failed",
    "Usdjpy_caveat_remains_preserved"
)) {
    if ($tests -notmatch [regex]::Escape($requiredTestName)) {
        Fail-Gate "PMS_EMS_OMS_R017_FAIL_BUILD_OR_TESTS" "Focused test missing: $requiredTestName"
    }
}

Require-True ([string]$evidence.build.status -eq "PASS") "PMS_EMS_OMS_R017_FAIL_BUILD_OR_TESTS" "Build evidence missing or not PASS."
Require-True ([int]$evidence.build.failed -eq 0) "PMS_EMS_OMS_R017_FAIL_BUILD_OR_TESTS" "Build evidence has failures."
Require-True ([string]$evidence.focusedTests.status -eq "PASS") "PMS_EMS_OMS_R017_FAIL_BUILD_OR_TESTS" "Focused test evidence missing or not PASS."
Require-True ([int]$evidence.focusedTests.failed -eq 0) "PMS_EMS_OMS_R017_FAIL_BUILD_OR_TESTS" "Focused tests have failures."
Require-True ([string]$evidence.unitTests.status -eq "PASS") "PMS_EMS_OMS_R017_FAIL_BUILD_OR_TESTS" "Unit test evidence missing or not PASS."
Require-True ([int]$evidence.unitTests.failed -eq 0) "PMS_EMS_OMS_R017_FAIL_BUILD_OR_TESTS" "Unit tests have failures."
Require-True ([string]$evidence.validator.status -eq "PASS") "PMS_EMS_OMS_R017_FAIL_BUILD_OR_TESTS" "Validator evidence missing or not PASS."
Require-True ([int]$evidence.validator.failed -eq 0) "PMS_EMS_OMS_R017_FAIL_BUILD_OR_TESTS" "Validator evidence has failures."
Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R017_FAIL_BUILD_OR_TESTS" "Evidence marker missing."

Write-Host "PMS_EMS_OMS_R017_PASS_PAPER_SIMULATION_DRY_RUN_RESULT_SHAPE_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R017_PASS_SIMULATION_NOT_RUN_RESULT_GATE_READY_NO_EXTERNAL"
