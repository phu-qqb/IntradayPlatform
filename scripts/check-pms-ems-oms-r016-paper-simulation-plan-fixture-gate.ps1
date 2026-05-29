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
    "phase-pms-ems-oms-r016-summary.md" = "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
    "phase-pms-ems-oms-r016-paper-simulation-plan-contract.json" = "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
    "phase-pms-ems-oms-r016-paper-simulation-plan.json" = "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
    "phase-pms-ems-oms-r016-paper-simulation-plan-lines.json" = "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
    "phase-pms-ems-oms-r016-simulation-assumption-set.json" = "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
    "phase-pms-ems-oms-r016-simulation-readiness-status.json" = "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
    "phase-pms-ems-oms-r016-simulation-not-run-audit.json" = "PMS_EMS_OMS_R016_FAIL_SIMULATION_RAN"
    "phase-pms-ems-oms-r016-no-fill-no-execution-report-audit.json" = "PMS_EMS_OMS_R016_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
    "phase-pms-ems-oms-r016-no-order-created-audit.json" = "PMS_EMS_OMS_R016_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r016-no-route-no-submission-audit.json" = "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_SUBMITTED_OR_ROUTED"
    "phase-pms-ems-oms-r016-idempotency-evidence.json" = "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
    "phase-pms-ems-oms-r016-risk-lineage-preservation.json" = "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
    "phase-pms-ems-oms-r016-qubes-lineage-preservation.json" = "PMS_EMS_OMS_R016_FAIL_QUBES_LINEAGE_WEAKENED"
    "phase-pms-ems-oms-r016-operator-decision-lineage-preservation.json" = "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
    "phase-pms-ems-oms-r016-plan-lineage-preservation.json" = "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
    "phase-pms-ems-oms-r016-paper-candidate-lineage-preservation.json" = "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
    "phase-pms-ems-oms-r016-rebalance-intent-lineage-preservation.json" = "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
    "phase-pms-ems-oms-r016-lot-sizing-lineage-preservation.json" = "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
    "phase-pms-ems-oms-r016-missing-stale-mark-preservation.json" = "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
    "phase-pms-ems-oms-r016-drift-acknowledgement-preservation.json" = "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
    "phase-pms-ems-oms-r016-instrument-universe-handling.json" = "PMS_EMS_OMS_R016_FAIL_LMAX_GAP_BLOCKS_SIMULATION_PLAN"
    "phase-pms-ems-oms-r016-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R016_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r016-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R016_FAIL_LMAX_GAP_BLOCKS_SIMULATION_PLAN"
    "phase-pms-ems-oms-r016-no-external-audit.json" = "PMS_EMS_OMS_R016_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r016-forbidden-actions-audit.json" = "PMS_EMS_OMS_R016_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r016-next-phase-recommendation.json" = "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
    "phase-pms-ems-oms-r016-build-test-validator-evidence.json" = "PMS_EMS_OMS_R016_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $entry.Key))) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$contract = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r016-paper-simulation-plan-contract.json") "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
$plan = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r016-paper-simulation-plan.json") "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
$lines = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r016-paper-simulation-plan-lines.json") "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
$assumptions = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r016-simulation-assumption-set.json") "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
$readiness = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r016-simulation-readiness-status.json") "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
$noSimulation = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r016-simulation-not-run-audit.json") "PMS_EMS_OMS_R016_FAIL_SIMULATION_RAN"
$fillAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r016-no-fill-no-execution-report-audit.json") "PMS_EMS_OMS_R016_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$orderAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r016-no-order-created-audit.json") "PMS_EMS_OMS_R016_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$routeAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r016-no-route-no-submission-audit.json") "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_SUBMITTED_OR_ROUTED"
$idempotency = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r016-idempotency-evidence.json") "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
$risk = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r016-risk-lineage-preservation.json") "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
$qubes = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r016-qubes-lineage-preservation.json") "PMS_EMS_OMS_R016_FAIL_QUBES_LINEAGE_WEAKENED"
$operatorLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r016-operator-decision-lineage-preservation.json") "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
$planLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r016-plan-lineage-preservation.json") "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
$candidateLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r016-paper-candidate-lineage-preservation.json") "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
$rebalance = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r016-rebalance-intent-lineage-preservation.json") "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
$lotSizing = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r016-lot-sizing-lineage-preservation.json") "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
$marks = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r016-missing-stale-mark-preservation.json") "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
$drift = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r016-drift-acknowledgement-preservation.json") "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r016-instrument-universe-handling.json") "PMS_EMS_OMS_R016_FAIL_LMAX_GAP_BLOCKS_SIMULATION_PLAN"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r016-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R016_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r016-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R016_FAIL_LMAX_GAP_BLOCKS_SIMULATION_PLAN"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r016-no-external-audit.json") "PMS_EMS_OMS_R016_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r016-forbidden-actions-audit.json") "PMS_EMS_OMS_R016_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$nextPhase = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r016-next-phase-recommendation.json") "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r016-build-test-validator-evidence.json") "PMS_EMS_OMS_R016_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$contract.paperSimulationPlanContractCreated) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Simulation plan contract missing."
foreach ($field in @("PaperSimulationPlanId", "PaperExecutionPlanId", "CycleRunId", "QubesRunId", "OperatorDecisionId", "PaperOrderCandidateBatchId", "PaperExecutionPlanLineIds", "RiskReviewReference", "LotSizingReference")) {
    Require-True (($contract.planFields -contains $field)) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Plan field missing: $field"
}
foreach ($status in @("PaperSimulationPlanReady", "PaperSimulationPlanBlocked", "PaperSimulationPlanRequiresAcknowledgement", "PaperSimulationPlanInconclusiveSafe")) {
    Require-True (($contract.planReadinessStatuses -contains $status)) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Readiness status missing: $status"
}
foreach ($property in @("paperOnly", "noExternal", "nonExecutable", "notAnOrder", "notSubmitted", "noBrokerRoute", "noFillCreated", "noExecutionReportCreated", "simulationNotRun")) {
    Require-True ([bool]$contract.$property) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_EXECUTABLE" "Contract safety flag missing: $property"
}

Require-True ([bool]$plan.paperSimulationPlanCreated) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Simulation plan artifact missing."
Require-True ([string]$plan.readinessStatus -eq "PaperSimulationPlanReady") "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Readiness status wrong."
Require-True ([int]$plan.lineCount -eq 3) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Line count wrong."
Require-True ([int]$plan.blockedLineCount -eq 10) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Blocked count wrong."
foreach ($property in @("paperOnly", "noExternal", "nonExecutable", "notAnOrder", "notSubmitted", "noBrokerRoute", "noFillCreated", "noExecutionReportCreated", "simulationNotRun")) {
    Require-True ([bool]$plan.$property) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_EXECUTABLE" "Plan safety flag missing: $property"
}
foreach ($property in @("createdOmsOrder", "createdParentOrder", "createdChildOrder", "createdBrokerOrder", "createdFill", "createdExecutionReport", "submittedOrders", "calledBrokerGateway", "requestedLiveMarketData", "startedApiOrWorker", "startedSchedulerOrBackgroundJob", "ranPaperSimulation", "mutatedLiveTradingState")) {
    Require-False ([bool]$plan.$property) "PMS_EMS_OMS_R016_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Plan unsafe flag detected: $property"
}

Require-True ([bool]$lines.paperSimulationPlanLinesCreated) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Simulation lines missing."
Require-True ([int]$lines.lineCount -eq 3) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Simulation line count wrong."
Require-True ([int]$lines.blockedR011R014LineCount -eq 10) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Blocked line count wrong."
Require-True ([bool]$lines.blockedLinesPreservedSeparately) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Blocked lines not preserved."
Require-True (@($lines.lines | Where-Object { $_.normalizedSymbol -eq "AUDUSD" -and $_.side -eq "Buy" -and [decimal]$_.paperBaseQuantity -eq 131000 }).Count -eq 1) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "AUDUSD line missing."
Require-True (@($lines.lines | Where-Object { $_.normalizedSymbol -eq "EURUSD" -and $_.side -eq "Buy" -and [decimal]$_.paperBaseQuantity -eq 124000 }).Count -eq 1) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "EURUSD line missing."
Require-True (@($lines.lines | Where-Object { $_.normalizedSymbol -eq "GBPUSD" -and $_.side -eq "Sell" -and [decimal]$_.paperBaseQuantity -eq 368000 }).Count -eq 1) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "GBPUSD line missing."
foreach ($line in $lines.lines) {
    Require-True ([string]$line.lineReadinessStatus -eq "SimulationLineReady") "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Line not ready."
    Require-True ([bool]$line.simulationNotRun) "PMS_EMS_OMS_R016_FAIL_SIMULATION_RAN" "Line simulation ran."
    Require-True (-not [string]::IsNullOrWhiteSpace([string]$line.riskReviewReference)) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Risk reference missing."
    Require-True (-not [string]::IsNullOrWhiteSpace([string]$line.lotSizingReference)) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Lot-sizing reference missing."
}

Require-True ([bool]$assumptions.simulationAssumptionSetCreated) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Assumption set missing."
Require-True ([string]$assumptions.simulationMode -eq "PaperNoExternal") "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Simulation mode wrong."
Require-True ([string]$assumptions.fillModel -eq "NotRunYet") "PMS_EMS_OMS_R016_FAIL_SIMULATION_RAN" "Fill model not NotRunYet."
Require-True ([string]$assumptions.marketDataSource -eq "FixtureOnly") "PMS_EMS_OMS_R016_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Market data source not fixture-only."
Require-True ([string]$assumptions.executionVenue -eq "None") "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_SUBMITTED_OR_ROUTED" "Execution venue present."
Require-True ([string]$assumptions.brokerRoute -eq "None") "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_SUBMITTED_OR_ROUTED" "Broker route present."
Require-True ([bool]$assumptions.fixtureOnly) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Fixture-only flag missing."
Require-True ([bool]$assumptions.noExternal) "PMS_EMS_OMS_R016_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external flag missing."
Require-True ([bool]$assumptions.simulationNotRun) "PMS_EMS_OMS_R016_FAIL_SIMULATION_RAN" "Simulation-not-run missing."
Require-False ([bool]$assumptions.rawMarketDataFixturePayloadsSerialized) "PMS_EMS_OMS_R016_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw fixture payload serialized."
Require-False ([bool]$assumptions.rawBrokerPricesSerialized) "PMS_EMS_OMS_R016_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw broker prices serialized."

Require-True ([bool]$readiness.simulationReadinessStatusCreated) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Readiness artifact missing."
Require-True ([string]$readiness.readinessStatus -eq "PaperSimulationPlanReady") "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Readiness artifact status wrong."
Require-True ([bool]$readiness.simulationNotRun) "PMS_EMS_OMS_R016_FAIL_SIMULATION_RAN" "Readiness says simulation ran."

Require-True ([bool]$noSimulation.simulationNotRunAuditCreated) "PMS_EMS_OMS_R016_FAIL_SIMULATION_RAN" "Simulation-not-run audit missing."
foreach ($property in @("paperSimulationRan", "simulationEngineInvoked", "simulationClockAdvanced", "simulationStateCreated", "simulationFillCreated", "simulationExecutionReportCreated")) {
    Require-False ([bool]$noSimulation.$property) "PMS_EMS_OMS_R016_FAIL_SIMULATION_RAN" "Simulation audit detected: $property"
}
Require-True ([bool]$noSimulation.simulationPlanPreparedOnly) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Prepared-only flag missing."

Require-True ([bool]$fillAudit.noFillNoExecutionReportAuditCreated) "PMS_EMS_OMS_R016_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill/report audit missing."
foreach ($property in @("fillCreated", "executionReportCreated", "simulationFillCreated", "simulationExecutionReportCreated", "planCreatesFillsOrExecutionReports", "lineCreatesFill", "lineCreatesExecutionReport")) {
    Require-False ([bool]$fillAudit.$property) "PMS_EMS_OMS_R016_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill/report audit detected: $property"
}
Require-True ([bool]$orderAudit.noOrderCreatedAuditCreated) "PMS_EMS_OMS_R016_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No-order audit missing."
foreach ($property in @("omsOrderCreated", "parentOrderCreated", "childOrderCreated", "brokerOrderCreated", "executableOrderCreated", "orderStateCreated", "orderSubmissionPathIntroduced", "ordersSubmitted", "liveTradingStateMutated", "brokerGatewayCalled")) {
    Require-False ([bool]$orderAudit.$property) "PMS_EMS_OMS_R016_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order audit detected: $property"
}
Require-True ([bool]$routeAudit.noRouteNoSubmissionAuditCreated) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_SUBMITTED_OR_ROUTED" "Route/submission audit missing."
foreach ($property in @("brokerRouteCreated", "brokerRouteAssigned", "submissionInstructionCreated", "orderSubmissionPathInvoked", "simulationPlanSubmitted", "simulationPlanRouteable")) {
    Require-False ([bool]$routeAudit.$property) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_SUBMITTED_OR_ROUTED" "Route/submission audit detected: $property"
}

Require-True ([bool]$idempotency.idempotencyEvidenceCreated) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Idempotency missing."
Require-True ([string]$idempotency.idempotencyKey -eq "PaperSimulationPlanId") "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Wrong idempotency key."
Require-True ([string]$idempotency.duplicatePlanBehavior -eq "DuplicateReturned") "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Duplicate behavior missing."
Require-False ([bool]$idempotency.duplicatesCreateAdditionalSimulationPlans) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Duplicate plans created."

Require-True ([bool]$risk.riskLineagePreservationCreated) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Risk lineage missing."
Require-False ([bool]$risk.riskLineageMissing) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Risk lineage marked missing."
Require-True ([bool]$qubes.qubesLineagePreservationCreated) "PMS_EMS_OMS_R016_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage missing."
Require-True ([string]$qubes.sourceSystem -eq "ModelWeightSourceSystem.Qubes") "PMS_EMS_OMS_R016_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes source missing."
Require-True ([int]$qubes.cadenceMinutes -eq 15) "PMS_EMS_OMS_R016_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes cadence missing."
Require-False ([bool]$qubes.qubesLineageWeakened) "PMS_EMS_OMS_R016_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage weakened."
Require-True ([bool]$operatorLineage.operatorDecisionLineagePreservationCreated) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Operator lineage missing."
Require-True ([string]$operatorLineage.resultingSimulationReadinessStatus -eq "SimulationReadyNoExternal") "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Operator readiness missing."
foreach ($property in @("blockedLinesAcknowledged", "missingStaleMarksAcknowledged", "driftAcknowledged", "operatorDecisionLineagePreserved")) {
    Require-True ([bool]$operatorLineage.$property) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Operator lineage flag missing: $property"
}
foreach ($lineage in @($planLineage, $candidateLineage, $rebalance, $lotSizing, $marks, $drift)) {
    $createdProperty = $lineage.PSObject.Properties.Name | Where-Object { $_ -match "Created$" } | Select-Object -First 1
    if ($createdProperty) {
        Require-True ([bool]$lineage.$createdProperty) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Lineage artifact missing: $createdProperty"
    }
}
Require-True ([bool]$rebalance.rebalanceIntentsRemainNonExecutable) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_EXECUTABLE" "Rebalance intents executable."
Require-False ([bool]$rebalance.rebalanceIntentCreatesOrder) "PMS_EMS_OMS_R016_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Rebalance creates order."
Require-False ([bool]$rebalance.rebalanceIntentSubmitted) "PMS_EMS_OMS_R016_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "Rebalance submitted."
Require-False ([bool]$marks.rawMarketDataFixturePayloadsSerialized) "PMS_EMS_OMS_R016_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw fixture payloads serialized."
Require-False ([bool]$marks.fabricatedMarksForSimulationPlan) "PMS_EMS_OMS_R016_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Marks fabricated."
Require-False ([bool]$drift.liveTradingApprovalCreated) "PMS_EMS_OMS_R016_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Live trading approval created."

Require-True ([bool]$universe.instrumentUniverseHandlingCreated) "PMS_EMS_OMS_R016_FAIL_LMAX_GAP_BLOCKS_SIMULATION_PLAN" "Universe handling missing."
Require-False ([bool]$universe.lmaxReadOnlyScopeUsedAsSimulationPlanGate) "PMS_EMS_OMS_R016_FAIL_LMAX_GAP_BLOCKS_SIMULATION_PLAN" "LMAX scope gates simulation plan."
Require-False ([bool]$universe.lmaxLiveValidationGapsBlockSimulationPlan) "PMS_EMS_OMS_R016_FAIL_LMAX_GAP_BLOCKS_SIMULATION_PLAN" "LMAX gaps block simulation plan."
Require-False ([bool]$universe.audusdLiveValidationGapBlocksSimulationPlan) "PMS_EMS_OMS_R016_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD gap blocks simulation plan."
Require-False ([bool]$universe.usdjpyLiveValidationGapBlocksSimulationPlan) "PMS_EMS_OMS_R016_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY gap blocks simulation plan."
Require-True ([bool]$usdjpy.usdJpyCaveatPreservationCreated) "PMS_EMS_OMS_R016_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
Require-True ([string]$usdjpy.usdJpySecurityId -eq "4004") "PMS_EMS_OMS_R016_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID missing."
Require-True ([string]$usdjpy.usdJpySecurityIdSource -eq "8") "PMS_EMS_OMS_R016_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource missing."
Require-False ([bool]$usdjpy.usdJpyClassifiedAsFailed) "PMS_EMS_OMS_R016_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY failed."
Require-False ([bool]$usdjpy.audusdClassifiedAsFailed) "PMS_EMS_OMS_R016_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD failed."
Require-True ([bool]$lmax.lmaxReadonlyBaselineReferenceCreated) "PMS_EMS_OMS_R016_FAIL_LMAX_GAP_BLOCKS_SIMULATION_PLAN" "LMAX baseline missing."
Require-False ([bool]$lmax.lmaxUsedInThisPhase) "PMS_EMS_OMS_R016_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX used."
Require-False ([bool]$lmax.lmaxCalledInThisPhase) "PMS_EMS_OMS_R016_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX called."
Require-False ([bool]$lmax.lmaxLiveValidationGapsBlockSimulationPlan) "PMS_EMS_OMS_R016_FAIL_LMAX_GAP_BLOCKS_SIMULATION_PLAN" "LMAX gaps block simulation plan."

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
    "simulationPlanExecutable",
    "simulationPlanSubmitted",
    "simulationPlanHasBrokerRoute",
    "simulationPlanRepresentedAsOmsOrder",
    "simulationPlanRepresentedAsBrokerOrder",
    "simulationCreatesFillsOrExecutionReports",
    "lmaxLiveValidationGapsBlockSimulationPlan"
)) {
    Require-False ([bool]$noExternal.$property) "PMS_EMS_OMS_R016_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected: $property"
}

foreach ($item in $forbidden.forbiddenActions) {
    if ([bool]$item.detected) {
        if ([string]$item.action -match "scheduler|service|timer|background") {
            Fail-Gate "PMS_EMS_OMS_R016_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "simulation") {
            Fail-Gate "PMS_EMS_OMS_R016_FAIL_SIMULATION_RAN" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "fill|execution report") {
            Fail-Gate "PMS_EMS_OMS_R016_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "order|trading|executable|OMS|parent|child|broker|submitted|routed") {
            Fail-Gate "PMS_EMS_OMS_R016_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden action detected: $($item.action)"
        }

        Fail-Gate "PMS_EMS_OMS_R016_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $($item.action)"
    }
}

Require-True ([bool]$nextPhase.nextPhaseRecommendationCreated) "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Next phase missing."
Require-True ([string]$nextPhase.recommendedNextPhase -eq "PMS-EMS-OMS-R017") "PMS_EMS_OMS_R016_FAIL_SIMULATION_PLAN_CONTRACT_MISSING" "Next phase is not R017."
Require-True ([bool]$nextPhase.mustRemainNoExternal) "PMS_EMS_OMS_R016_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase not no-external."
Require-True ([bool]$nextPhase.mustNotRunSimulationYet) "PMS_EMS_OMS_R016_FAIL_SIMULATION_RAN" "Next phase permits simulation too early."

$apiSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Api/appsettings.json") "PMS_EMS_OMS_R016_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$workerSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/appsettings.json") "PMS_EMS_OMS_R016_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-False ([bool]$apiSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R016_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API live trading enabled."
Require-False ([bool]$workerSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R016_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker live trading enabled."
Require-False ([bool]$apiSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R016_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API external enabled."
Require-False ([bool]$workerSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R016_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker external enabled."
Require-True ([bool]$apiSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R016_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API fake gateway not required."
Require-True ([bool]$workerSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R016_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker fake gateway not required."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.Enabled) "PMS_EMS_OMS_R016_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX runtime enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowExternalConnections) "PMS_EMS_OMS_R016_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX external enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowOrderSubmission) "PMS_EMS_OMS_R016_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "LMAX order submission enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SchedulerEnabled) "PMS_EMS_OMS_R016_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "LMAX scheduler enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SubmitToShadowReplay) "PMS_EMS_OMS_R016_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Shadow replay enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.PersistRawFixMessages) "PMS_EMS_OMS_R016_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw FIX persistence enabled."
Require-False ([bool]$workerSettings.MarketDataBars.Enabled) "PMS_EMS_OMS_R016_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker market data bars enabled."

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-pms-ems-oms-r016-*" -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combined = [string]::Join("`n", $artifactText)
foreach ($pattern in @("\u0001", "35=", "MDReqID\s*[:=]", "SenderCompID\s*[:=]", "TargetCompID\s*[:=]", "BeginString\s*[:=]", "SocketHost\s*[:=]", "TlsHost\s*[:=]", "Password\s*[:=]", "ApiKey\s*[:=]", "Bearer\s+[A-Za-z0-9_\.-]+", "rawBid", "rawAsk", "rawMid")) {
    if ($combined -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R016_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Unsafe serialized content pattern found: $pattern"
    }
}

$requiredFiles = @(
    "src/QQ.Production.Intraday.Application/QubesPaperSimulationPlanFixture.cs",
    "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperSimulationPlanFixtureTests.cs"
)
foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $file))) {
        Fail-Gate "PMS_EMS_OMS_R016_FAIL_BUILD_OR_TESTS" "Required file missing: $file"
    }
}

$source = Get-Content -LiteralPath (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/QubesPaperSimulationPlanFixture.cs") -Raw
foreach ($pattern in @("TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "SendOrderAsync", "SubmitOrder", "FixSession")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R016_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "R016 source contains forbidden runtime pattern: $pattern"
    }
}
foreach ($pattern in @("AddHostedService", "IHostedService", "BackgroundService", "PeriodicTimer", "Task.Delay", "System.Threading.Timer")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R016_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "R016 source contains scheduler/service pattern: $pattern"
    }
}

$tests = Get-Content -LiteralPath (Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperSimulationPlanFixtureTests.cs") -Raw
foreach ($requiredTestName in @(
    "R015_simulation_ready_no_external_plan_can_create_paper_simulation_plan_fixture",
    "Plan_preserves_paper_execution_plan_id",
    "Plan_preserves_cycle_run_id_and_qubes_run_id",
    "Plan_preserves_operator_decision_id",
    "Plan_preserves_candidate_risk_rebalance_and_lot_sizing_lineage",
    "Audusd_buy_line_is_carried_into_simulation_plan",
    "Eurusd_buy_line_is_carried_into_simulation_plan",
    "Gbpusd_sell_line_is_carried_into_simulation_plan",
    "Simulation_assumptions_are_fixture_only_and_no_external",
    "Simulation_plan_is_paper_only_no_external_and_non_executable",
    "Simulation_plan_is_not_an_order_not_submitted_and_no_broker_route",
    "Simulation_plan_explicitly_says_simulation_not_run",
    "No_fills_are_created",
    "No_execution_reports_are_created",
    "No_oms_parent_child_or_broker_order_is_created",
    "No_order_submission_path_is_introduced",
    "Blocked_lines_are_preserved_separately",
    "Missing_stale_mark_warnings_are_preserved",
    "Drift_acknowledgement_is_preserved",
    "Duplicate_simulation_plan_handling_is_idempotent",
    "Simulation_plan_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action",
    "Api_and_worker_live_gateway_remain_disabled",
    "Simulation_plan_source_introduces_no_scheduler_timer_polling_or_background_job",
    "Audusd_is_not_misclassified_as_failed",
    "Usdjpy_caveat_remains_preserved"
)) {
    if ($tests -notmatch [regex]::Escape($requiredTestName)) {
        Fail-Gate "PMS_EMS_OMS_R016_FAIL_BUILD_OR_TESTS" "Focused test missing: $requiredTestName"
    }
}

Require-True ([string]$evidence.build.status -eq "PASS") "PMS_EMS_OMS_R016_FAIL_BUILD_OR_TESTS" "Build evidence missing or not PASS."
Require-True ([int]$evidence.build.failed -eq 0) "PMS_EMS_OMS_R016_FAIL_BUILD_OR_TESTS" "Build evidence has failures."
Require-True ([string]$evidence.focusedTests.status -eq "PASS") "PMS_EMS_OMS_R016_FAIL_BUILD_OR_TESTS" "Focused test evidence missing or not PASS."
Require-True ([int]$evidence.focusedTests.failed -eq 0) "PMS_EMS_OMS_R016_FAIL_BUILD_OR_TESTS" "Focused tests have failures."
Require-True ([string]$evidence.unitTests.status -eq "PASS") "PMS_EMS_OMS_R016_FAIL_BUILD_OR_TESTS" "Unit test evidence missing or not PASS."
Require-True ([int]$evidence.unitTests.failed -eq 0) "PMS_EMS_OMS_R016_FAIL_BUILD_OR_TESTS" "Unit tests have failures."
Require-True ([string]$evidence.validator.status -eq "PASS") "PMS_EMS_OMS_R016_FAIL_BUILD_OR_TESTS" "Validator evidence missing or not PASS."
Require-True ([int]$evidence.validator.failed -eq 0) "PMS_EMS_OMS_R016_FAIL_BUILD_OR_TESTS" "Validator evidence has failures."
Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R016_FAIL_BUILD_OR_TESTS" "Evidence marker missing."

Write-Host "PMS_EMS_OMS_R016_PASS_PAPER_SIMULATION_PLAN_FIXTURE_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R016_PASS_SIMULATION_NOT_RUN_GATE_READY_NO_EXTERNAL"
