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
    "phase-pms-ems-oms-r015-summary.md" = "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING"
    "phase-pms-ems-oms-r015-plan-operator-approval-contract.json" = "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING"
    "phase-pms-ems-oms-r015-plan-operator-decisions.json" = "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING"
    "phase-pms-ems-oms-r015-simulation-readiness-gate.json" = "PMS_EMS_OMS_R015_FAIL_SIMULATION_READINESS_GATE_MISSING"
    "phase-pms-ems-oms-r015-hold-decision-example.json" = "PMS_EMS_OMS_R015_FAIL_ACKNOWLEDGEMENT_GATE_WEAKENED"
    "phase-pms-ems-oms-r015-approve-for-paper-simulation-example.json" = "PMS_EMS_OMS_R015_FAIL_SIMULATION_READINESS_GATE_MISSING"
    "phase-pms-ems-oms-r015-blocked-lines-acknowledgement.json" = "PMS_EMS_OMS_R015_FAIL_ACKNOWLEDGEMENT_GATE_WEAKENED"
    "phase-pms-ems-oms-r015-missing-stale-mark-acknowledgement.json" = "PMS_EMS_OMS_R015_FAIL_ACKNOWLEDGEMENT_GATE_WEAKENED"
    "phase-pms-ems-oms-r015-drift-acknowledgement.json" = "PMS_EMS_OMS_R015_FAIL_ACKNOWLEDGEMENT_GATE_WEAKENED"
    "phase-pms-ems-oms-r015-no-simulation-run-audit.json" = "PMS_EMS_OMS_R015_FAIL_SIMULATION_RAN"
    "phase-pms-ems-oms-r015-no-fill-no-execution-report-audit.json" = "PMS_EMS_OMS_R015_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
    "phase-pms-ems-oms-r015-no-order-created-audit.json" = "PMS_EMS_OMS_R015_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r015-no-route-no-submission-audit.json" = "PMS_EMS_OMS_R015_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED"
    "phase-pms-ems-oms-r015-idempotency-evidence.json" = "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING"
    "phase-pms-ems-oms-r015-risk-lineage-preservation.json" = "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING"
    "phase-pms-ems-oms-r015-qubes-lineage-preservation.json" = "PMS_EMS_OMS_R015_FAIL_QUBES_LINEAGE_WEAKENED"
    "phase-pms-ems-oms-r015-plan-lineage-preservation.json" = "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING"
    "phase-pms-ems-oms-r015-paper-candidate-lineage-preservation.json" = "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING"
    "phase-pms-ems-oms-r015-rebalance-intent-lineage-preservation.json" = "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING"
    "phase-pms-ems-oms-r015-lot-sizing-lineage-preservation.json" = "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING"
    "phase-pms-ems-oms-r015-instrument-universe-handling.json" = "PMS_EMS_OMS_R015_FAIL_LMAX_GAP_BLOCKS_PLAN_APPROVAL"
    "phase-pms-ems-oms-r015-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R015_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r015-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R015_FAIL_LMAX_GAP_BLOCKS_PLAN_APPROVAL"
    "phase-pms-ems-oms-r015-no-external-audit.json" = "PMS_EMS_OMS_R015_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r015-forbidden-actions-audit.json" = "PMS_EMS_OMS_R015_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r015-next-phase-recommendation.json" = "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING"
    "phase-pms-ems-oms-r015-build-test-validator-evidence.json" = "PMS_EMS_OMS_R015_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $entry.Key))) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$contract = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r015-plan-operator-approval-contract.json") "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING"
$decisions = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r015-plan-operator-decisions.json") "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING"
$readiness = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r015-simulation-readiness-gate.json") "PMS_EMS_OMS_R015_FAIL_SIMULATION_READINESS_GATE_MISSING"
$hold = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r015-hold-decision-example.json") "PMS_EMS_OMS_R015_FAIL_ACKNOWLEDGEMENT_GATE_WEAKENED"
$approve = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r015-approve-for-paper-simulation-example.json") "PMS_EMS_OMS_R015_FAIL_SIMULATION_READINESS_GATE_MISSING"
$blockedAck = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r015-blocked-lines-acknowledgement.json") "PMS_EMS_OMS_R015_FAIL_ACKNOWLEDGEMENT_GATE_WEAKENED"
$missingAck = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r015-missing-stale-mark-acknowledgement.json") "PMS_EMS_OMS_R015_FAIL_ACKNOWLEDGEMENT_GATE_WEAKENED"
$driftAck = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r015-drift-acknowledgement.json") "PMS_EMS_OMS_R015_FAIL_ACKNOWLEDGEMENT_GATE_WEAKENED"
$noSimulation = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r015-no-simulation-run-audit.json") "PMS_EMS_OMS_R015_FAIL_SIMULATION_RAN"
$fillAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r015-no-fill-no-execution-report-audit.json") "PMS_EMS_OMS_R015_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$orderAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r015-no-order-created-audit.json") "PMS_EMS_OMS_R015_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$routeAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r015-no-route-no-submission-audit.json") "PMS_EMS_OMS_R015_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED"
$idempotency = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r015-idempotency-evidence.json") "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING"
$risk = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r015-risk-lineage-preservation.json") "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING"
$qubes = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r015-qubes-lineage-preservation.json") "PMS_EMS_OMS_R015_FAIL_QUBES_LINEAGE_WEAKENED"
$planLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r015-plan-lineage-preservation.json") "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING"
$candidateLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r015-paper-candidate-lineage-preservation.json") "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING"
$rebalance = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r015-rebalance-intent-lineage-preservation.json") "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING"
$lotSizing = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r015-lot-sizing-lineage-preservation.json") "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r015-instrument-universe-handling.json") "PMS_EMS_OMS_R015_FAIL_LMAX_GAP_BLOCKS_PLAN_APPROVAL"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r015-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R015_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r015-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R015_FAIL_LMAX_GAP_BLOCKS_PLAN_APPROVAL"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r015-no-external-audit.json") "PMS_EMS_OMS_R015_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r015-forbidden-actions-audit.json") "PMS_EMS_OMS_R015_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$nextPhase = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r015-next-phase-recommendation.json") "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r015-build-test-validator-evidence.json") "PMS_EMS_OMS_R015_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$contract.planOperatorApprovalContractCreated) "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING" "Approval contract missing."
foreach ($value in @("ApproveForPaperSimulation", "Hold", "Reject", "RequestPlanFix", "RequestRiskReview")) {
    Require-True (($contract.decisionTypes -contains $value)) "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING" "Decision type missing: $value"
}
foreach ($value in @("SimulationReadyNoExternal", "HeldForMissingMarks", "HeldForDrift", "HeldForBlockedLines", "HeldForRiskReview", "Rejected", "InconclusiveSafe")) {
    Require-True (($contract.simulationReadinessStatuses -contains $value)) "PMS_EMS_OMS_R015_FAIL_SIMULATION_READINESS_GATE_MISSING" "Readiness status missing: $value"
}
foreach ($property in @("planMustBeArchived", "planMustBePaperOnlyNoExternalNonExecutable", "planMustHaveNoOmsOrBrokerOrders", "planMustHaveNoSubmittedLines", "planMustHaveNoFills", "planMustHaveNoExecutionReports", "blockedLinesRequireAcknowledgement", "missingStaleMarksRequireAcknowledgement", "driftRequiresAcknowledgement", "executableRoutedSubmittedPlanRejected")) {
    Require-True ([bool]$contract.gateRules.$property) "PMS_EMS_OMS_R015_FAIL_ACKNOWLEDGEMENT_GATE_WEAKENED" "Gate rule missing: $property"
}

Require-True ([bool]$decisions.operatorDecisionsCreated) "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING" "Operator decisions missing."
Require-True (@($decisions.decisions | Where-Object { $_.decisionType -eq "Hold" -and $_.resultingSimulationReadinessStatus -eq "HeldForMissingMarks" }).Count -eq 1) "PMS_EMS_OMS_R015_FAIL_ACKNOWLEDGEMENT_GATE_WEAKENED" "Hold decision missing."
Require-True (@($decisions.decisions | Where-Object { $_.decisionType -eq "ApproveForPaperSimulation" -and $_.resultingSimulationReadinessStatus -eq "SimulationReadyNoExternal" -and [bool]$_.blockedLinesAcknowledged -and [bool]$_.missingStaleMarksAcknowledged -and [bool]$_.driftAcknowledged }).Count -eq 1) "PMS_EMS_OMS_R015_FAIL_SIMULATION_READINESS_GATE_MISSING" "Approve decision missing."
foreach ($property in @("enablesLiveTrading", "runsPaperSimulation", "createsOrders", "createsFills", "createsExecutionReports")) {
    Require-False ([bool]$decisions.$property) "PMS_EMS_OMS_R015_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Decision artifact detected: $property"
}

Require-True ([bool]$readiness.simulationReadinessGateCreated) "PMS_EMS_OMS_R015_FAIL_SIMULATION_READINESS_GATE_MISSING" "Readiness gate missing."
Require-True ([string]$readiness.resultingStatus -eq "SimulationReadyNoExternal") "PMS_EMS_OMS_R015_FAIL_SIMULATION_READINESS_GATE_MISSING" "Readiness status wrong."
foreach ($property in @("simulationReadinessOnly", "paperSimulationReadinessOnly", "noLiveTrading", "noOrders", "noBrokerRoute", "noSubmission", "noFills", "noExecutionReports")) {
    Require-True ([bool]$readiness.$property) "PMS_EMS_OMS_R015_FAIL_SIMULATION_READINESS_GATE_MISSING" "Readiness safety flag missing: $property"
}
foreach ($property in @("simulationIsRun", "simulationReadinessExecutable", "simulationReadinessRouted", "simulationReadinessCreatesOrders", "simulationReadinessCreatesFills", "simulationReadinessCreatesExecutionReports")) {
    Require-False ([bool]$readiness.$property) "PMS_EMS_OMS_R015_FAIL_SIMULATION_READINESS_EXECUTABLE" "Readiness unsafe flag detected: $property"
}

Require-True ([bool]$hold.holdDecisionExampleCreated) "PMS_EMS_OMS_R015_FAIL_ACKNOWLEDGEMENT_GATE_WEAKENED" "Hold example missing."
Require-True ([string]$hold.decisionType -eq "Hold") "PMS_EMS_OMS_R015_FAIL_ACKNOWLEDGEMENT_GATE_WEAKENED" "Hold example wrong."
Require-False ([bool]$hold.externalActionTaken) "PMS_EMS_OMS_R015_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Hold external action detected."
Require-True ([bool]$approve.approveForPaperSimulationExampleCreated) "PMS_EMS_OMS_R015_FAIL_SIMULATION_READINESS_GATE_MISSING" "Approve example missing."
Require-True ([string]$approve.resultingSimulationReadinessStatus -eq "SimulationReadyNoExternal") "PMS_EMS_OMS_R015_FAIL_SIMULATION_READINESS_GATE_MISSING" "Approve example status wrong."
foreach ($property in @("blockedLinesAcknowledged", "missingStaleMarksAcknowledged", "driftAcknowledged", "paperSimulationReadinessOnly")) {
    Require-True ([bool]$approve.$property) "PMS_EMS_OMS_R015_FAIL_ACKNOWLEDGEMENT_GATE_WEAKENED" "Approve acknowledgement missing: $property"
}
foreach ($property in @("simulationIsRun", "createsOrders", "createsFills", "createsExecutionReports")) {
    Require-False ([bool]$approve.$property) "PMS_EMS_OMS_R015_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Approve unsafe flag detected: $property"
}

foreach ($ack in @($blockedAck, $missingAck, $driftAck)) {
    Require-True ([bool]$ack.approvalWithoutAcknowledgementRejected) "PMS_EMS_OMS_R015_FAIL_ACKNOWLEDGEMENT_GATE_WEAKENED" "Missing rejected-without-ack evidence."
    Require-True ([bool]$ack.approvalWithAcknowledgementAccepted) "PMS_EMS_OMS_R015_FAIL_ACKNOWLEDGEMENT_GATE_WEAKENED" "Missing accepted-with-ack evidence."
}
Require-True ([bool]$blockedAck.blockedLinesRequireAcknowledgement) "PMS_EMS_OMS_R015_FAIL_ACKNOWLEDGEMENT_GATE_WEAKENED" "Blocked-line acknowledgement missing."
Require-True ([bool]$missingAck.missingStaleMarksRequireAcknowledgement) "PMS_EMS_OMS_R015_FAIL_ACKNOWLEDGEMENT_GATE_WEAKENED" "Missing/stale acknowledgement missing."
Require-True ([bool]$driftAck.driftRequiresAcknowledgement) "PMS_EMS_OMS_R015_FAIL_ACKNOWLEDGEMENT_GATE_WEAKENED" "Drift acknowledgement missing."

Require-True ([bool]$noSimulation.noSimulationRunAuditCreated) "PMS_EMS_OMS_R015_FAIL_SIMULATION_RAN" "No-simulation audit missing."
foreach ($property in @("paperSimulationRan", "simulationEngineInvoked", "simulationClockAdvanced", "simulationStateCreated", "simulationFillCreated", "simulationExecutionReportCreated")) {
    Require-False ([bool]$noSimulation.$property) "PMS_EMS_OMS_R015_FAIL_SIMULATION_RAN" "Simulation audit detected: $property"
}
Require-True ([bool]$noSimulation.simulationReadinessOnly) "PMS_EMS_OMS_R015_FAIL_SIMULATION_READINESS_GATE_MISSING" "Readiness-only flag missing."

Require-True ([bool]$fillAudit.noFillNoExecutionReportAuditCreated) "PMS_EMS_OMS_R015_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill/report audit missing."
foreach ($property in @("fillCreated", "executionReportCreated", "simulationFillCreated", "simulationExecutionReportCreated", "approvalCreatesFillsOrExecutionReports")) {
    Require-False ([bool]$fillAudit.$property) "PMS_EMS_OMS_R015_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill/report detected: $property"
}
Require-True ([bool]$orderAudit.noOrderCreatedAuditCreated) "PMS_EMS_OMS_R015_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No-order audit missing."
foreach ($property in @("omsOrderCreated", "parentOrderCreated", "childOrderCreated", "brokerOrderCreated", "executableOrderCreated", "orderStateCreated", "orderSubmissionPathIntroduced", "ordersSubmitted", "liveTradingStateMutated", "brokerGatewayCalled")) {
    Require-False ([bool]$orderAudit.$property) "PMS_EMS_OMS_R015_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order audit detected: $property"
}
Require-True ([bool]$routeAudit.noRouteNoSubmissionAuditCreated) "PMS_EMS_OMS_R015_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "Route/submission audit missing."
foreach ($property in @("brokerRouteCreated", "brokerRouteAssigned", "submissionInstructionCreated", "orderSubmissionPathInvoked", "readinessSubmitted", "readinessRouteable")) {
    Require-False ([bool]$routeAudit.$property) "PMS_EMS_OMS_R015_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "Route/submission detected: $property"
}

Require-True ([bool]$idempotency.idempotencyEvidenceCreated) "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING" "Idempotency missing."
Require-True ([string]$idempotency.idempotencyKey -eq "OperatorDecisionId") "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING" "Wrong idempotency key."
Require-True ([string]$idempotency.duplicateDecisionBehavior -eq "DuplicateReturned") "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING" "Duplicate behavior missing."
Require-False ([bool]$idempotency.duplicatesCreateAdditionalDecisionRecords) "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING" "Duplicate records created."

Require-True ([bool]$risk.riskLineagePreservationCreated) "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING" "Risk lineage missing."
Require-False ([bool]$risk.riskLineageMissing) "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING" "Risk lineage marked missing."
Require-True ([bool]$qubes.qubesLineagePreservationCreated) "PMS_EMS_OMS_R015_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage missing."
Require-True ([string]$qubes.sourceSystem -eq "ModelWeightSourceSystem.Qubes") "PMS_EMS_OMS_R015_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes source missing."
Require-True ([int]$qubes.cadenceMinutes -eq 15) "PMS_EMS_OMS_R015_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes cadence missing."
Require-False ([bool]$qubes.qubesLineageWeakened) "PMS_EMS_OMS_R015_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage weakened."
foreach ($property in @("planLineagePreservationCreated", "paperOnly", "noExternal", "nonExecutable", "notAnOrder", "notSubmitted", "noBrokerRoute")) {
    Require-True ([bool]$planLineage.$property) "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING" "Plan lineage missing: $property"
}
Require-True ([bool]$candidateLineage.paperCandidateLineagePreservationCreated) "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING" "Candidate lineage missing."
Require-True ([bool]$rebalance.rebalanceIntentLineagePreservationCreated) "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING" "Rebalance lineage missing."
Require-True ([bool]$rebalance.rebalanceIntentsRemainNonExecutable) "PMS_EMS_OMS_R015_FAIL_SIMULATION_READINESS_EXECUTABLE" "Rebalance intents executable."
Require-False ([bool]$rebalance.rebalanceIntentCreatesOrder) "PMS_EMS_OMS_R015_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Rebalance creates order."
Require-False ([bool]$rebalance.rebalanceIntentSubmitted) "PMS_EMS_OMS_R015_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "Rebalance submitted."
Require-True ([bool]$lotSizing.lotSizingLineagePreservationCreated) "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING" "Lot-sizing lineage missing."

Require-True ([bool]$universe.instrumentUniverseHandlingCreated) "PMS_EMS_OMS_R015_FAIL_LMAX_GAP_BLOCKS_PLAN_APPROVAL" "Universe handling missing."
Require-False ([bool]$universe.lmaxReadOnlyScopeUsedAsApprovalGate) "PMS_EMS_OMS_R015_FAIL_LMAX_GAP_BLOCKS_PLAN_APPROVAL" "LMAX scope used as approval gate."
Require-False ([bool]$universe.lmaxLiveValidationGapsBlockPlanApproval) "PMS_EMS_OMS_R015_FAIL_LMAX_GAP_BLOCKS_PLAN_APPROVAL" "LMAX gaps block approval."
Require-False ([bool]$universe.audusdLiveValidationGapBlocksPlanApproval) "PMS_EMS_OMS_R015_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD gap blocks approval."
Require-False ([bool]$universe.usdjpyLiveValidationGapBlocksPlanApproval) "PMS_EMS_OMS_R015_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY gap blocks approval."
Require-True ([bool]$usdjpy.usdJpyCaveatPreservationCreated) "PMS_EMS_OMS_R015_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
Require-True ([string]$usdjpy.usdJpySecurityId -eq "4004") "PMS_EMS_OMS_R015_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID missing."
Require-True ([string]$usdjpy.usdJpySecurityIdSource -eq "8") "PMS_EMS_OMS_R015_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource missing."
Require-False ([bool]$usdjpy.usdJpyClassifiedAsFailed) "PMS_EMS_OMS_R015_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY failed."
Require-False ([bool]$usdjpy.audusdClassifiedAsFailed) "PMS_EMS_OMS_R015_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD failed."
Require-True ([bool]$lmax.lmaxReadonlyBaselineReferenceCreated) "PMS_EMS_OMS_R015_FAIL_LMAX_GAP_BLOCKS_PLAN_APPROVAL" "LMAX baseline missing."
Require-False ([bool]$lmax.lmaxUsedInThisPhase) "PMS_EMS_OMS_R015_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX used."
Require-False ([bool]$lmax.lmaxCalledInThisPhase) "PMS_EMS_OMS_R015_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX called."
Require-False ([bool]$lmax.lmaxLiveValidationGapsBlockPlanApproval) "PMS_EMS_OMS_R015_FAIL_LMAX_GAP_BLOCKS_PLAN_APPROVAL" "LMAX gaps block approval."

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
    "simulationReadinessExecutable",
    "simulationReadinessRouted",
    "simulationReadinessCreatesOrders",
    "simulationReadinessCreatesFills",
    "simulationReadinessCreatesExecutionReports",
    "lmaxLiveValidationGapsBlockPlanApproval"
)) {
    Require-False ([bool]$noExternal.$property) "PMS_EMS_OMS_R015_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected: $property"
}

foreach ($item in $forbidden.forbiddenActions) {
    if ([bool]$item.detected) {
        if ([string]$item.action -match "scheduler|service|timer|background") {
            Fail-Gate "PMS_EMS_OMS_R015_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "simulation") {
            Fail-Gate "PMS_EMS_OMS_R015_FAIL_SIMULATION_RAN" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "fill|execution report") {
            Fail-Gate "PMS_EMS_OMS_R015_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "order|trading|executable|OMS|parent|child|broker|submitted|routed") {
            Fail-Gate "PMS_EMS_OMS_R015_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden action detected: $($item.action)"
        }

        Fail-Gate "PMS_EMS_OMS_R015_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $($item.action)"
    }
}

Require-True ([bool]$nextPhase.nextPhaseRecommendationCreated) "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING" "Next phase missing."
Require-True ([string]$nextPhase.recommendedNextPhase -eq "PMS-EMS-OMS-R016") "PMS_EMS_OMS_R015_FAIL_PLAN_APPROVAL_CONTRACT_MISSING" "Next phase is not R016."
Require-True ([bool]$nextPhase.mustRemainNoExternal) "PMS_EMS_OMS_R015_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase not no-external."
Require-True ([bool]$nextPhase.mustNotRunSimulationYet) "PMS_EMS_OMS_R015_FAIL_SIMULATION_RAN" "Next phase permits simulation too early."

$apiSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Api/appsettings.json") "PMS_EMS_OMS_R015_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$workerSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/appsettings.json") "PMS_EMS_OMS_R015_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-False ([bool]$apiSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R015_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API live trading enabled."
Require-False ([bool]$workerSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R015_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker live trading enabled."
Require-False ([bool]$apiSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R015_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API external enabled."
Require-False ([bool]$workerSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R015_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker external enabled."
Require-True ([bool]$apiSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R015_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API fake gateway not required."
Require-True ([bool]$workerSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R015_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker fake gateway not required."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.Enabled) "PMS_EMS_OMS_R015_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX runtime enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowExternalConnections) "PMS_EMS_OMS_R015_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX external enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowOrderSubmission) "PMS_EMS_OMS_R015_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "LMAX order submission enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SchedulerEnabled) "PMS_EMS_OMS_R015_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "LMAX scheduler enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SubmitToShadowReplay) "PMS_EMS_OMS_R015_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Shadow replay enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.PersistRawFixMessages) "PMS_EMS_OMS_R015_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw FIX persistence enabled."
Require-False ([bool]$workerSettings.MarketDataBars.Enabled) "PMS_EMS_OMS_R015_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker market data bars enabled."

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-pms-ems-oms-r015-*" -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combined = [string]::Join("`n", $artifactText)
foreach ($pattern in @("\u0001", "35=", "MDReqID\s*[:=]", "SenderCompID\s*[:=]", "TargetCompID\s*[:=]", "BeginString\s*[:=]", "SocketHost\s*[:=]", "TlsHost\s*[:=]", "Password\s*[:=]", "ApiKey\s*[:=]", "Bearer\s+[A-Za-z0-9_\.-]+", "rawBid", "rawAsk", "rawMid")) {
    if ($combined -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R015_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Unsafe serialized content pattern found: $pattern"
    }
}

$requiredFiles = @(
    "src/QQ.Production.Intraday.Application/QubesPaperExecutionPlanApproval.cs",
    "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperExecutionPlanApprovalTests.cs"
)
foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $file))) {
        Fail-Gate "PMS_EMS_OMS_R015_FAIL_BUILD_OR_TESTS" "Required file missing: $file"
    }
}

$source = Get-Content -LiteralPath (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/QubesPaperExecutionPlanApproval.cs") -Raw
foreach ($pattern in @("TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "SendOrderAsync", "SubmitOrder", "FixSession")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R015_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "R015 source contains forbidden runtime pattern: $pattern"
    }
}
foreach ($pattern in @("AddHostedService", "IHostedService", "BackgroundService", "PeriodicTimer", "Task.Delay", "System.Threading.Timer")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R015_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "R015 source contains scheduler/service pattern: $pattern"
    }
}

$tests = Get-Content -LiteralPath (Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperExecutionPlanApprovalTests.cs") -Raw
foreach ($requiredTestName in @(
    "R014_archived_paper_plan_can_enter_operator_approval_review",
    "Approve_for_paper_simulation_records_decision_without_external_action",
    "Hold_records_decision_without_external_action",
    "Reject_records_decision_without_external_action",
    "Request_plan_fix_records_decision_safely",
    "Plan_with_blocked_lines_requires_acknowledgement_before_simulation_readiness",
    "Plan_with_missing_stale_marks_requires_acknowledgement_before_simulation_readiness",
    "Plan_with_drift_requires_acknowledgement_before_simulation_readiness",
    "Approval_produces_simulation_ready_no_external_only",
    "Simulation_ready_no_external_does_not_create_fills",
    "Simulation_ready_no_external_does_not_create_execution_reports",
    "Simulation_ready_no_external_does_not_create_oms_orders",
    "Simulation_ready_no_external_does_not_create_broker_orders",
    "Simulation_ready_no_external_does_not_submit_anything",
    "Executable_routed_or_submitted_plan_is_rejected",
    "Duplicate_operator_decision_id_handling_is_idempotent",
    "Qubes_run_id_and_cycle_run_id_are_preserved",
    "Paper_execution_plan_id_is_preserved",
    "Candidate_risk_rebalance_and_lot_sizing_lineage_is_preserved",
    "Approval_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action",
    "Api_and_worker_live_gateway_remain_disabled",
    "Approval_source_introduces_no_scheduler_timer_polling_or_background_job",
    "Audusd_is_not_misclassified_as_failed",
    "Usdjpy_caveat_remains_preserved"
)) {
    if ($tests -notmatch [regex]::Escape($requiredTestName)) {
        Fail-Gate "PMS_EMS_OMS_R015_FAIL_BUILD_OR_TESTS" "Focused test missing: $requiredTestName"
    }
}

Require-True ([string]$evidence.build.status -eq "PASS") "PMS_EMS_OMS_R015_FAIL_BUILD_OR_TESTS" "Build evidence missing or not PASS."
Require-True ([int]$evidence.build.failed -eq 0) "PMS_EMS_OMS_R015_FAIL_BUILD_OR_TESTS" "Build evidence has failures."
Require-True ([string]$evidence.focusedTests.status -eq "PASS") "PMS_EMS_OMS_R015_FAIL_BUILD_OR_TESTS" "Focused test evidence missing or not PASS."
Require-True ([int]$evidence.focusedTests.failed -eq 0) "PMS_EMS_OMS_R015_FAIL_BUILD_OR_TESTS" "Focused tests have failures."
Require-True ([string]$evidence.unitTests.status -eq "PASS") "PMS_EMS_OMS_R015_FAIL_BUILD_OR_TESTS" "Unit test evidence missing or not PASS."
Require-True ([int]$evidence.unitTests.failed -eq 0) "PMS_EMS_OMS_R015_FAIL_BUILD_OR_TESTS" "Unit tests have failures."
Require-True ([string]$evidence.validator.status -eq "PASS") "PMS_EMS_OMS_R015_FAIL_BUILD_OR_TESTS" "Validator evidence missing or not PASS."
Require-True ([int]$evidence.validator.failed -eq 0) "PMS_EMS_OMS_R015_FAIL_BUILD_OR_TESTS" "Validator evidence has failures."
Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R015_FAIL_BUILD_OR_TESTS" "Evidence marker missing."

Write-Host "PMS_EMS_OMS_R015_PASS_PLAN_OPERATOR_APPROVAL_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R015_PASS_SIMULATION_READINESS_GATE_READY_NO_EXTERNAL"
