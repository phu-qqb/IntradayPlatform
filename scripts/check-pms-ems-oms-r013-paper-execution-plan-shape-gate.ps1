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
    "phase-pms-ems-oms-r013-summary.md" = "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING"
    "phase-pms-ems-oms-r013-paper-execution-plan-contract.json" = "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING"
    "phase-pms-ems-oms-r013-paper-execution-plan.json" = "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING"
    "phase-pms-ems-oms-r013-paper-execution-plan-lines.json" = "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING"
    "phase-pms-ems-oms-r013-plan-status-summary.json" = "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING"
    "phase-pms-ems-oms-r013-non-executable-plan-audit.json" = "PMS_EMS_OMS_R013_FAIL_PLAN_EXECUTABLE"
    "phase-pms-ems-oms-r013-no-order-created-audit.json" = "PMS_EMS_OMS_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r013-no-route-no-submission-audit.json" = "PMS_EMS_OMS_R013_FAIL_PLAN_SUBMITTED_OR_ROUTED"
    "phase-pms-ems-oms-r013-risk-lineage-preservation.json" = "PMS_EMS_OMS_R013_FAIL_RISK_LINEAGE_MISSING"
    "phase-pms-ems-oms-r013-qubes-lineage-preservation.json" = "PMS_EMS_OMS_R013_FAIL_QUBES_LINEAGE_WEAKENED"
    "phase-pms-ems-oms-r013-operator-decision-lineage-preservation.json" = "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING"
    "phase-pms-ems-oms-r013-paper-candidate-lineage-preservation.json" = "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING"
    "phase-pms-ems-oms-r013-rebalance-intent-lineage-preservation.json" = "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING"
    "phase-pms-ems-oms-r013-lot-sizing-lineage-preservation.json" = "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING"
    "phase-pms-ems-oms-r013-missing-stale-mark-preservation.json" = "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING"
    "phase-pms-ems-oms-r013-drift-acknowledgement-preservation.json" = "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING"
    "phase-pms-ems-oms-r013-instrument-universe-handling.json" = "PMS_EMS_OMS_R013_FAIL_LMAX_GAP_BLOCKS_PLAN_SHAPE"
    "phase-pms-ems-oms-r013-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R013_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r013-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R013_FAIL_LMAX_GAP_BLOCKS_PLAN_SHAPE"
    "phase-pms-ems-oms-r013-no-external-audit.json" = "PMS_EMS_OMS_R013_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r013-forbidden-actions-audit.json" = "PMS_EMS_OMS_R013_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r013-next-phase-recommendation.json" = "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING"
    "phase-pms-ems-oms-r013-build-test-validator-evidence.json" = "PMS_EMS_OMS_R013_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $entry.Key))) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$contract = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r013-paper-execution-plan-contract.json") "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING"
$plan = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r013-paper-execution-plan.json") "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING"
$lines = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r013-paper-execution-plan-lines.json") "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING"
$status = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r013-plan-status-summary.json") "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING"
$nonExec = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r013-non-executable-plan-audit.json") "PMS_EMS_OMS_R013_FAIL_PLAN_EXECUTABLE"
$orderAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r013-no-order-created-audit.json") "PMS_EMS_OMS_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$routeAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r013-no-route-no-submission-audit.json") "PMS_EMS_OMS_R013_FAIL_PLAN_SUBMITTED_OR_ROUTED"
$risk = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r013-risk-lineage-preservation.json") "PMS_EMS_OMS_R013_FAIL_RISK_LINEAGE_MISSING"
$qubes = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r013-qubes-lineage-preservation.json") "PMS_EMS_OMS_R013_FAIL_QUBES_LINEAGE_WEAKENED"
$operatorDecision = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r013-operator-decision-lineage-preservation.json") "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING"
$candidateLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r013-paper-candidate-lineage-preservation.json") "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING"
$rebalance = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r013-rebalance-intent-lineage-preservation.json") "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING"
$lotSizing = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r013-lot-sizing-lineage-preservation.json") "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING"
$marks = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r013-missing-stale-mark-preservation.json") "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING"
$drift = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r013-drift-acknowledgement-preservation.json") "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r013-instrument-universe-handling.json") "PMS_EMS_OMS_R013_FAIL_LMAX_GAP_BLOCKS_PLAN_SHAPE"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r013-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R013_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r013-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R013_FAIL_LMAX_GAP_BLOCKS_PLAN_SHAPE"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r013-no-external-audit.json") "PMS_EMS_OMS_R013_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r013-forbidden-actions-audit.json") "PMS_EMS_OMS_R013_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$nextPhase = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r013-next-phase-recommendation.json") "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r013-build-test-validator-evidence.json") "PMS_EMS_OMS_R013_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$contract.paperExecutionPlanContractCreated) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Paper execution plan contract missing."
foreach ($field in @("PaperExecutionPlanId", "CycleRunId", "QubesRunId", "OperatorDecisionId", "PaperOrderCandidateBatchId", "PlanStatus", "PlanMode", "ReadyLineCount", "BlockedLineCount")) {
    Require-True (($contract.planFields -contains $field)) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Plan field missing: $field"
}
foreach ($field in @("PaperExecutionPlanLineId", "PaperOrderCandidateId", "SourceRebalanceIntentId", "RiskReviewReference", "NormalizedSymbol", "PaperTradableSymbol", "Side", "PaperBaseQuantity", "QuantityCurrency", "NotionalCurrency", "LotSize", "QuantityRoundingMode", "QuantityShapeCategory", "QuantityStatus", "PlanLineStatus", "ExecutionStyleShape", "TimeInForceShape", "SequencingGroup", "Priority", "BlockReason", "NonExecutableReason")) {
    Require-True (($contract.lineFields -contains $field)) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Line field missing: $field"
}
foreach ($mode in @("PaperOnly", "NoExternal", "NonExecutable")) {
    Require-True (($contract.planModes -contains $mode)) "PMS_EMS_OMS_R013_FAIL_PLAN_EXECUTABLE" "Plan mode missing: $mode"
}
foreach ($shape in @("MarketShapeOnly", "LimitShapeRequiresPrice", "VWAPShapeOnly", "NotSpecified", "NotExecutable")) {
    Require-True (($contract.executionStyleShapes -contains $shape)) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Execution style shape missing: $shape"
}
foreach ($shape in @("DayShapeOnly", "IOCShapeOnly", "NotSpecified", "NotExecutable")) {
    Require-True (($contract.timeInForceShapes -contains $shape)) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Time-in-force shape missing: $shape"
}
foreach ($property in @("paperOnly", "nonExecutable", "notAnOrder", "notSubmitted", "noBrokerRoute")) {
    Require-True ([bool]$contract.$property) "PMS_EMS_OMS_R013_FAIL_PLAN_EXECUTABLE" "Contract missing flag: $property"
}

Require-True ([bool]$plan.paperExecutionPlanCreated) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Plan artifact missing."
Require-True ([string]$plan.paperExecutionPlanId -eq "cycle-r013-sample:paper-execution-plan") "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Unexpected plan id."
Require-True ([string]$plan.cycleRunId -eq "cycle-r013-sample") "PMS_EMS_OMS_R013_FAIL_QUBES_LINEAGE_WEAKENED" "CycleRunId missing."
Require-True ([string]$plan.qubesRunId -eq "qubes-r013-sample") "PMS_EMS_OMS_R013_FAIL_QUBES_LINEAGE_WEAKENED" "QubesRunId missing."
Require-True ([string]$plan.operatorDecisionId -eq "decision-r013-promote-paper-ready") "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "OperatorDecisionId missing."
Require-True ([string]$plan.planStatus -eq "PaperPlanPartiallyReady") "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Unexpected plan status."
Require-True ([int]$plan.readyLineCount -eq 3) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Ready line count wrong."
Require-True ([int]$plan.blockedLineCount -eq 10) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Blocked line count wrong."
foreach ($property in @("paperOnly", "nonExecutable", "notAnOrder", "notSubmitted", "noBrokerRoute")) {
    Require-True ([bool]$plan.$property) "PMS_EMS_OMS_R013_FAIL_PLAN_EXECUTABLE" "Plan flag missing: $property"
}
foreach ($property in @("createdOmsOrder", "createdParentOrder", "createdChildOrder", "createdBrokerOrder", "createdFill", "createdExecutionReport", "submittedOrders", "calledBrokerGateway")) {
    Require-False ([bool]$plan.$property) "PMS_EMS_OMS_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Plan detected forbidden output: $property"
}

Require-True ([bool]$lines.paperExecutionPlanLinesCreated) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Plan lines missing."
Require-True ([int]$lines.lineCount -eq 3) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Unexpected line count."
Require-True ([int]$lines.blockedR011LineCount -eq 10) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Blocked R011 line count missing."
Require-False ([bool]$lines.blockedR011LinesBecomeReadyExecutionPlanLines) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Blocked R011 lines became ready plan lines."
Require-True (@($lines.lines | Where-Object { $_.normalizedSymbol -eq "AUDUSD" -and $_.side -eq "Buy" -and [decimal]$_.paperBaseQuantity -eq 131000 }).Count -eq 1) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "AUDUSD Buy plan line missing."
Require-True (@($lines.lines | Where-Object { $_.normalizedSymbol -eq "EURUSD" -and $_.side -eq "Buy" -and [decimal]$_.paperBaseQuantity -eq 124000 }).Count -eq 1) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "EURUSD Buy plan line missing."
Require-True (@($lines.lines | Where-Object { $_.normalizedSymbol -eq "GBPUSD" -and $_.side -eq "Sell" -and [decimal]$_.paperBaseQuantity -eq 368000 }).Count -eq 1) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "GBPUSD Sell plan line missing."
foreach ($line in $lines.lines) {
    Require-True ([string]$line.planLineStatus -eq "PaperLineReady") "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Plan line not ready shape."
    Require-True ([string]$line.executionStyleShape -eq "MarketShapeOnly") "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Execution style is not shape-only."
    Require-True ([string]$line.timeInForceShape -eq "DayShapeOnly") "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "TIF is not shape-only."
    Require-True ([string]$line.quantityStatus -eq "PaperSized") "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Lot-sized quantity not preserved."
    Require-True ([decimal]$line.lotSize -eq 1000) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Lot size not preserved."
    Require-True ([string]$line.quantityRoundingMode -eq "RoundToNearestLot") "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Rounding mode not preserved."
    Require-True ([bool]$line.paperOnly) "PMS_EMS_OMS_R013_FAIL_PLAN_EXECUTABLE" "Line is not paper-only."
    Require-True ([bool]$line.nonExecutable) "PMS_EMS_OMS_R013_FAIL_PLAN_EXECUTABLE" "Line is executable."
    Require-True ([bool]$line.notAnOrder) "PMS_EMS_OMS_R013_FAIL_PLAN_IS_OMS_OR_BROKER_ORDER" "Line represented as an order."
    Require-True ([bool]$line.notSubmitted) "PMS_EMS_OMS_R013_FAIL_PLAN_SUBMITTED_OR_ROUTED" "Line submitted."
    Require-True ([bool]$line.noBrokerRoute) "PMS_EMS_OMS_R013_FAIL_PLAN_SUBMITTED_OR_ROUTED" "Line has broker route."
    Require-True (-not [string]::IsNullOrWhiteSpace([string]$line.paperOrderCandidateId)) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Paper candidate lineage missing on line."
    Require-True (-not [string]::IsNullOrWhiteSpace([string]$line.sourceRebalanceIntentId)) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Rebalance lineage missing on line."
    Require-True (-not [string]::IsNullOrWhiteSpace([string]$line.riskReviewReference)) "PMS_EMS_OMS_R013_FAIL_RISK_LINEAGE_MISSING" "Risk lineage missing on line."
}

Require-True ([bool]$status.planStatusSummaryCreated) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Status summary missing."
Require-True ([string]$status.planStatus -eq "PaperPlanPartiallyReady") "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Status summary wrong."
Require-True ([int]$status.readyLineCount -eq 3) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Ready status count wrong."
Require-True ([int]$status.blockedLineCount -eq 10) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Blocked status count wrong."
Require-True ([int]$status.paperLineReadyCount -eq 3) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Ready line status count wrong."

Require-True ([bool]$nonExec.nonExecutablePlanAuditCreated) "PMS_EMS_OMS_R013_FAIL_PLAN_EXECUTABLE" "Non-executable audit missing."
foreach ($property in @("planPaperOnly", "planNonExecutable", "planNotAnOrder", "planNotSubmitted", "planNoBrokerRoute", "allLinesPaperOnly", "allLinesNonExecutable", "allLinesNotAnOrder", "allLinesNotSubmitted", "allLinesNoBrokerRoute")) {
    Require-True ([bool]$nonExec.$property) "PMS_EMS_OMS_R013_FAIL_PLAN_EXECUTABLE" "Non-executable audit missing: $property"
}
foreach ($property in @("planExecutable", "planSubmitted", "planHasBrokerRoute", "planRepresentedAsOmsOrder", "planRepresentedAsBrokerOrder")) {
    Require-False ([bool]$nonExec.$property) "PMS_EMS_OMS_R013_FAIL_PLAN_EXECUTABLE" "Non-executable audit detected: $property"
}

Require-True ([bool]$orderAudit.noOrderCreatedAuditCreated) "PMS_EMS_OMS_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No-order audit missing."
foreach ($property in @("omsOrderCreated", "parentOrderCreated", "childOrderCreated", "brokerOrderCreated", "executableOrderCreated", "fillCreated", "executionReportCreated", "orderStateCreated", "orderSubmissionPathIntroduced", "ordersSubmitted", "liveTradingStateMutated", "brokerGatewayCalled")) {
    Require-False ([bool]$orderAudit.$property) "PMS_EMS_OMS_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No-order audit detected: $property"
}

Require-True ([bool]$routeAudit.noRouteNoSubmissionAuditCreated) "PMS_EMS_OMS_R013_FAIL_PLAN_SUBMITTED_OR_ROUTED" "No-route/no-submission audit missing."
foreach ($property in @("brokerRouteCreated", "brokerRouteAssigned", "submissionInstructionCreated", "orderSubmissionPathInvoked", "planSubmitted", "lineSubmitted", "planRouteable", "lineRouteable")) {
    Require-False ([bool]$routeAudit.$property) "PMS_EMS_OMS_R013_FAIL_PLAN_SUBMITTED_OR_ROUTED" "Route/submission audit detected: $property"
}

Require-True ([bool]$risk.riskLineagePreservationCreated) "PMS_EMS_OMS_R013_FAIL_RISK_LINEAGE_MISSING" "Risk lineage missing."
Require-True ([bool]$risk.riskReviewReferencePresentOnEveryPlanLine) "PMS_EMS_OMS_R013_FAIL_RISK_LINEAGE_MISSING" "Risk reference missing from plan lines."
Require-True ([bool]$risk.acceptedRiskResultReferenced) "PMS_EMS_OMS_R013_FAIL_RISK_LINEAGE_MISSING" "Accepted risk result missing."
Require-True ([bool]$risk.blockedRiskResultsCarriedSeparately) "PMS_EMS_OMS_R013_FAIL_RISK_LINEAGE_MISSING" "Blocked risk results not preserved."
Require-False ([bool]$risk.riskLineageMissing) "PMS_EMS_OMS_R013_FAIL_RISK_LINEAGE_MISSING" "Risk lineage marked missing."

Require-True ([bool]$qubes.qubesLineagePreservationCreated) "PMS_EMS_OMS_R013_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage missing."
Require-True ([string]$qubes.sourceSystem -eq "ModelWeightSourceSystem.Qubes") "PMS_EMS_OMS_R013_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes source missing."
Require-True ([int]$qubes.cadenceMinutes -eq 15) "PMS_EMS_OMS_R013_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes cadence missing."
foreach ($property in @("qubesAuditBatchPreserved", "rawQubesRowAuditPreserved", "normalizedWeightAuditPreserved", "modelWeightBatchLinkagePreserved", "modelRunLinkagePreserved", "targetWeightLinkagePreserved")) {
    Require-True ([bool]$qubes.$property) "PMS_EMS_OMS_R013_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage missing: $property"
}
Require-False ([bool]$qubes.qubesLineageWeakened) "PMS_EMS_OMS_R013_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage weakened."

Require-True ([bool]$operatorDecision.operatorDecisionLineagePreservationCreated) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Operator decision lineage missing."
Require-True ([string]$operatorDecision.operatorDecisionType -eq "PromoteToPaperReady") "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Operator decision is not PromoteToPaperReady."
Require-True ([string]$operatorDecision.resultingCycleReviewStatus -eq "PaperReadyNoExternal") "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Operator decision is not PaperReadyNoExternal."
Require-True ([bool]$operatorDecision.operatorDecisionReferencedOnEveryPlanLine) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Operator decision missing from lines."
Require-False ([bool]$operatorDecision.promotionMeansLiveTrading) "PMS_EMS_OMS_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Promotion means live trading."
Require-False ([bool]$operatorDecision.promotionCreatesOrders) "PMS_EMS_OMS_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Promotion creates orders."

Require-True ([bool]$candidateLineage.paperCandidateLineagePreservationCreated) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Paper candidate lineage missing."
Require-True ([bool]$candidateLineage.paperOrderCandidateIdReferencedOnEveryPlanLine) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Paper candidate id missing on lines."
Require-True ([bool]$candidateLineage.paperCandidateArchiveReferenced) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Paper candidate archive missing."
Require-True ([bool]$candidateLineage.blockedR011LinesPreservedSeparately) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Blocked R011 lines not preserved."
Require-False ([bool]$candidateLineage.blockedR011LinesBecomeReadyExecutionPlanLines) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Blocked R011 lines became ready plan lines."

Require-True ([bool]$rebalance.rebalanceIntentLineagePreservationCreated) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Rebalance lineage missing."
Require-True ([bool]$rebalance.sourceRebalanceIntentReferencedOnEveryPlanLine) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Source rebalance id missing from plan lines."
Require-True ([bool]$rebalance.rebalanceIntentsRemainNonExecutable) "PMS_EMS_OMS_R013_FAIL_PLAN_EXECUTABLE" "Rebalance intents became executable."
Require-True ([bool]$rebalance.allSourceIntentsTheoreticalOnly) "PMS_EMS_OMS_R013_FAIL_PLAN_EXECUTABLE" "Source intents not theoretical-only."
Require-True ([bool]$rebalance.allSourceIntentsNotExecutable) "PMS_EMS_OMS_R013_FAIL_PLAN_EXECUTABLE" "Source intents executable."
Require-True ([bool]$rebalance.allSourceIntentsBlockedNoOms) "PMS_EMS_OMS_R013_FAIL_PLAN_IS_OMS_OR_BROKER_ORDER" "Source intents not BlockedNoOMS."
Require-False ([bool]$rebalance.rebalanceIntentCreatesOrder) "PMS_EMS_OMS_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Rebalance intent creates order."
Require-False ([bool]$rebalance.rebalanceIntentSubmitted) "PMS_EMS_OMS_R013_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "Rebalance intent submitted."

Require-True ([bool]$lotSizing.lotSizingLineagePreservationCreated) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Lot-sizing lineage missing."
foreach ($property in @("lotSizedCandidateBatchReferenced", "paperQuantityShapePreservedOnEveryPlanLine", "paperBaseQuantityPreservedOnEveryPlanLine", "lotSizePreservedOnEveryPlanLine", "roundingModePreservedOnEveryPlanLine", "quantityStatusPreservedOnEveryPlanLine", "instrumentConventionPreservedOnEveryPlanLine")) {
    Require-True ([bool]$lotSizing.$property) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Lot-sizing lineage missing: $property"
}

Require-True ([bool]$marks.missingStaleMarkPreservationCreated) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Missing/stale preservation missing."
Require-True ([bool]$marks.missingMarkStatusPreserved) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Missing mark status not preserved."
Require-True ([bool]$marks.staleMarkStatusPreserved) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Stale mark status not preserved."
Require-True ([bool]$marks.blockedMissingStaleLinesPreservedSeparately) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Blocked missing/stale lines not preserved."
Require-False ([bool]$marks.blockedMissingStaleLinesBecomePlanLines) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Blocked missing/stale lines became plan lines."
Require-False ([bool]$marks.missingOrStaleMarksHidden) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Missing/stale marks hidden."
Require-False ([bool]$marks.fabricatedMarksForPlanShape) "PMS_EMS_OMS_R013_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Marks fabricated."
Require-False ([bool]$marks.rawMarketDataFixturePayloadsSerialized) "PMS_EMS_OMS_R013_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw fixture payload serialized."

Require-True ([bool]$drift.driftAcknowledgementPreservationCreated) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Drift preservation missing."
Require-True ([string]$drift.theoreticalVsRealStatus -eq "Drift") "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Drift status missing."
Require-True ([bool]$drift.driftAcknowledgedByOperatorDecision) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Drift not acknowledged."
Require-True ([bool]$drift.driftAcknowledgementPreservedInPlan) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Drift not preserved in plan."
Require-True ([bool]$drift.driftAllowsPaperPlanOnly) "PMS_EMS_OMS_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Drift permits more than paper plan."
Require-False ([bool]$drift.liveTradingApprovalCreated) "PMS_EMS_OMS_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Live approval created."

Require-True ([bool]$universe.instrumentUniverseHandlingCreated) "PMS_EMS_OMS_R013_FAIL_LMAX_GAP_BLOCKS_PLAN_SHAPE" "Instrument universe handling missing."
Require-True ([bool]$universe.portfolioNormalizedSymbolDistinguishedFromPaperTradableSymbol) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Normalized/tradable distinction missing."
Require-False ([bool]$universe.lmaxReadOnlyScopeUsedAsPlanGate) "PMS_EMS_OMS_R013_FAIL_LMAX_GAP_BLOCKS_PLAN_SHAPE" "LMAX scope used as plan gate."
Require-False ([bool]$universe.lmaxLiveValidationGapsBlockPlanShape) "PMS_EMS_OMS_R013_FAIL_LMAX_GAP_BLOCKS_PLAN_SHAPE" "LMAX gaps block plan shape."
Require-False ([bool]$universe.audusdLiveValidationGapBlocksPlanShape) "PMS_EMS_OMS_R013_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD gap blocks plan shape."
Require-False ([bool]$universe.usdjpyLiveValidationGapBlocksPlanShape) "PMS_EMS_OMS_R013_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY gap blocks plan shape."
Require-True ([string]$universe.audusdStatus -match "not failed") "PMS_EMS_OMS_R013_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD status weakened."
Require-True ([string]$universe.usdjpyStatus -match "not failed") "PMS_EMS_OMS_R013_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY status weakened."

Require-True ([bool]$usdjpy.usdJpyCaveatPreservationCreated) "PMS_EMS_OMS_R013_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
Require-True ([string]$usdjpy.usdJpySecurityId -eq "4004") "PMS_EMS_OMS_R013_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID missing."
Require-True ([string]$usdjpy.usdJpySecurityIdSource -eq "8") "PMS_EMS_OMS_R013_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource missing."
Require-True ([bool]$usdjpy.usdJpyNotProven) "PMS_EMS_OMS_R013_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY not-proven caveat missing."
Require-False ([bool]$usdjpy.usdJpyClassifiedAsFailed) "PMS_EMS_OMS_R013_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY classified failed."
Require-True ([bool]$usdjpy.audusdTlsBoundaryInconclusive) "PMS_EMS_OMS_R013_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD TLS-boundary status missing."
Require-False ([bool]$usdjpy.audusdClassifiedAsFailed) "PMS_EMS_OMS_R013_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD classified failed."

Require-True ([bool]$lmax.lmaxReadonlyBaselineReferenceCreated) "PMS_EMS_OMS_R013_FAIL_LMAX_GAP_BLOCKS_PLAN_SHAPE" "LMAX baseline missing."
Require-False ([bool]$lmax.lmaxUsedInThisPhase) "PMS_EMS_OMS_R013_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX used in this phase."
Require-False ([bool]$lmax.lmaxCalledInThisPhase) "PMS_EMS_OMS_R013_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX called in this phase."
Require-False ([bool]$lmax.lmaxLiveValidationGapsBlockPlanShape) "PMS_EMS_OMS_R013_FAIL_LMAX_GAP_BLOCKS_PLAN_SHAPE" "LMAX gaps block plan shape."
Require-True ([int]$lmax.baseline.GBPUSD.sanitizedEntryCount -eq 2) "PMS_EMS_OMS_R013_FAIL_LMAX_GAP_BLOCKS_PLAN_SHAPE" "GBPUSD baseline missing."
Require-True ([int]$lmax.baseline.EURGBP.sanitizedEntryCount -eq 2) "PMS_EMS_OMS_R013_FAIL_LMAX_GAP_BLOCKS_PLAN_SHAPE" "EURGBP baseline missing."
Require-False ([bool]$lmax.baseline.AUDUSD.classifiedAsFailed) "PMS_EMS_OMS_R013_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD baseline failed."
Require-False ([bool]$lmax.baseline.USDJPY.classifiedAsFailed) "PMS_EMS_OMS_R013_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY baseline failed."
Require-True ([string]$lmax.baseline.USDJPY.securityId -eq "4004") "PMS_EMS_OMS_R013_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID missing in baseline."
Require-True ([string]$lmax.baseline.USDJPY.securityIdSource -eq "8") "PMS_EMS_OMS_R013_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource missing in baseline."

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
    "planExecutable",
    "planSubmitted",
    "planHasBrokerRoute",
    "planRepresentedAsOmsOrder",
    "planRepresentedAsBrokerOrder",
    "planCreatesParentChildOrders",
    "planCreatesFillsOrExecutionReports",
    "blockedR011LinesBecomeReadyExecutionPlanLines",
    "lmaxLiveValidationGapsBlockPlanShape"
)) {
    Require-False ([bool]$noExternal.$property) "PMS_EMS_OMS_R013_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected: $property"
}

foreach ($item in $forbidden.forbiddenActions) {
    if ([bool]$item.detected) {
        if ([string]$item.action -match "scheduler|service|timer|background") {
            Fail-Gate "PMS_EMS_OMS_R013_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "order|trading|executable|OMS|parent|child|broker|fill|execution report|submitted|routed") {
            Fail-Gate "PMS_EMS_OMS_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden action detected: $($item.action)"
        }

        Fail-Gate "PMS_EMS_OMS_R013_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $($item.action)"
    }
}

Require-True ([bool]$nextPhase.nextPhaseRecommendationCreated) "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Next phase missing."
Require-True ([string]$nextPhase.recommendedNextPhase -eq "PMS-EMS-OMS-R014") "PMS_EMS_OMS_R013_FAIL_PAPER_EXECUTION_PLAN_CONTRACT_MISSING" "Next phase is not R014."
Require-True ([bool]$nextPhase.mustRemainNoExternal) "PMS_EMS_OMS_R013_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase not no-external."
Require-True ([bool]$nextPhase.mustNotCreateExecutableOrders) "PMS_EMS_OMS_R013_FAIL_EXECUTABLE_ORDER_CREATED" "Next phase permits executable orders."
Require-True ([bool]$nextPhase.mustNotCreateOmsOrders) "PMS_EMS_OMS_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Next phase permits OMS orders."
Require-True ([bool]$nextPhase.mustNotCreateBrokerOrders) "PMS_EMS_OMS_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Next phase permits broker orders."
Require-True ([bool]$nextPhase.mustNotSubmitOrders) "PMS_EMS_OMS_R013_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "Next phase permits submission."
Require-True ([bool]$nextPhase.mustNotCreateFillsOrExecutionReports) "PMS_EMS_OMS_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Next phase permits fills/execution reports."

$apiSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Api/appsettings.json") "PMS_EMS_OMS_R013_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$workerSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/appsettings.json") "PMS_EMS_OMS_R013_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-False ([bool]$apiSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API live trading enabled."
Require-False ([bool]$workerSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker live trading enabled."
Require-False ([bool]$apiSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R013_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API external connections enabled."
Require-False ([bool]$workerSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R013_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker external connections enabled."
Require-True ([bool]$apiSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API fake gateway not required."
Require-True ([bool]$workerSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker fake gateway not required."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.Enabled) "PMS_EMS_OMS_R013_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX runtime enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowExternalConnections) "PMS_EMS_OMS_R013_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX external connections enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowOrderSubmission) "PMS_EMS_OMS_R013_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "LMAX order submission enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SchedulerEnabled) "PMS_EMS_OMS_R013_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "LMAX scheduler enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SubmitToShadowReplay) "PMS_EMS_OMS_R013_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Shadow replay enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.PersistRawFixMessages) "PMS_EMS_OMS_R013_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw FIX persistence enabled."
Require-False ([bool]$workerSettings.MarketDataBars.Enabled) "PMS_EMS_OMS_R013_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker market data bars enabled."

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-pms-ems-oms-r013-*" -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combined = [string]::Join("`n", $artifactText)
foreach ($pattern in @("\u0001", "35=", "MDReqID\s*[:=]", "SenderCompID\s*[:=]", "TargetCompID\s*[:=]", "BeginString\s*[:=]", "SocketHost\s*[:=]", "TlsHost\s*[:=]", "Password\s*[:=]", "ApiKey\s*[:=]", "Bearer\s+[A-Za-z0-9_\.-]+", "rawBid", "rawAsk", "rawMid")) {
    if ($combined -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R013_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Unsafe serialized content pattern found: $pattern"
    }
}

$requiredFiles = @(
    "src/QQ.Production.Intraday.Application/QubesPaperExecutionPlanShape.cs",
    "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperExecutionPlanShapeTests.cs"
)
foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $file))) {
        Fail-Gate "PMS_EMS_OMS_R013_FAIL_BUILD_OR_TESTS" "Required file missing: $file"
    }
}

$source = Get-Content -LiteralPath (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/QubesPaperExecutionPlanShape.cs") -Raw
foreach ($pattern in @("TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "SendOrderAsync", "SubmitOrder", "FixSession")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R013_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "R013 source contains forbidden runtime pattern: $pattern"
    }
}
foreach ($pattern in @("AddHostedService", "IHostedService", "BackgroundService", "PeriodicTimer", "Task.Delay", "System.Threading.Timer")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R013_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "R013 source contains scheduler/service pattern: $pattern"
    }
}

$tests = Get-Content -LiteralPath (Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperExecutionPlanShapeTests.cs") -Raw
foreach ($requiredTestName in @(
    "R012_lot_sized_candidates_can_feed_paper_execution_plan_shape_generation",
    "One_paper_execution_plan_batch_is_created_for_the_cycle",
    "Audusd_buy_line_is_included",
    "Eurusd_buy_line_is_included",
    "Gbpusd_sell_line_is_included",
    "Plan_preserves_cycle_run_id_and_qubes_run_id",
    "Plan_preserves_operator_decision_id",
    "Plan_preserves_source_paper_candidate_ids",
    "Plan_preserves_source_rebalance_intent_lineage",
    "Plan_preserves_risk_review_references",
    "Plan_preserves_quantity_shapes_and_lot_sizing_metadata",
    "Plan_is_paper_only",
    "Plan_is_non_executable",
    "Plan_is_not_an_order_not_submitted_and_no_broker_route",
    "Execution_style_and_time_in_force_are_shape_only",
    "Blocked_r011_lines_do_not_become_ready_plan_lines",
    "Missing_stale_mark_warnings_are_preserved",
    "Drift_acknowledgement_is_preserved",
    "No_oms_parent_child_or_broker_order_is_created",
    "No_fill_or_execution_report_is_introduced",
    "No_order_submission_path_is_introduced",
    "Plan_shape_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action",
    "Api_and_worker_live_gateway_remain_disabled",
    "Plan_shape_source_introduces_no_scheduler_timer_polling_or_background_job",
    "Audusd_is_not_misclassified_as_failed",
    "Usdjpy_caveat_remains_preserved"
)) {
    if ($tests -notmatch [regex]::Escape($requiredTestName)) {
        Fail-Gate "PMS_EMS_OMS_R013_FAIL_BUILD_OR_TESTS" "Focused test missing: $requiredTestName"
    }
}

Require-True ([string]$evidence.build.status -eq "PASS") "PMS_EMS_OMS_R013_FAIL_BUILD_OR_TESTS" "Build evidence missing or not PASS."
Require-True ([int]$evidence.build.failed -eq 0) "PMS_EMS_OMS_R013_FAIL_BUILD_OR_TESTS" "Build evidence has failures."
Require-True ([string]$evidence.focusedTests.status -eq "PASS") "PMS_EMS_OMS_R013_FAIL_BUILD_OR_TESTS" "Focused test evidence missing or not PASS."
Require-True ([int]$evidence.focusedTests.failed -eq 0) "PMS_EMS_OMS_R013_FAIL_BUILD_OR_TESTS" "Focused tests have failures."
Require-True ([string]$evidence.unitTests.status -eq "PASS") "PMS_EMS_OMS_R013_FAIL_BUILD_OR_TESTS" "Unit test evidence missing or not PASS."
Require-True ([int]$evidence.unitTests.failed -eq 0) "PMS_EMS_OMS_R013_FAIL_BUILD_OR_TESTS" "Unit tests have failures."
Require-True ([string]$evidence.validator.status -eq "PASS") "PMS_EMS_OMS_R013_FAIL_BUILD_OR_TESTS" "Validator evidence missing or not PASS."
Require-True ([int]$evidence.validator.failed -eq 0) "PMS_EMS_OMS_R013_FAIL_BUILD_OR_TESTS" "Validator evidence has failures."
Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R013_FAIL_BUILD_OR_TESTS" "Evidence marker missing."

Write-Host "PMS_EMS_OMS_R013_PASS_PAPER_EXECUTION_PLAN_SHAPES_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R013_PASS_NONEXECUTABLE_EXECUTION_PLAN_GATE_READY_NO_EXTERNAL"
