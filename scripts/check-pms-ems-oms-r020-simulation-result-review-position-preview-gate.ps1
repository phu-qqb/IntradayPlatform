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
    "phase-pms-ems-oms-r020-summary.md" = "PMS_EMS_OMS_R020_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING"
    "phase-pms-ems-oms-r020-simulation-result-operator-review-contract.json" = "PMS_EMS_OMS_R020_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING"
    "phase-pms-ems-oms-r020-simulation-result-operator-decisions.json" = "PMS_EMS_OMS_R020_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING"
    "phase-pms-ems-oms-r020-paper-position-preview-gate.json" = "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
    "phase-pms-ems-oms-r020-paper-position-preview.json" = "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
    "phase-pms-ems-oms-r020-paper-position-preview-lines.json" = "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
    "phase-pms-ems-oms-r020-hold-decision-example.json" = "PMS_EMS_OMS_R020_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING"
    "phase-pms-ems-oms-r020-promote-to-paper-position-preview-example.json" = "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
    "phase-pms-ems-oms-r020-blocked-lines-acknowledgement.json" = "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
    "phase-pms-ems-oms-r020-missing-stale-mark-acknowledgement.json" = "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
    "phase-pms-ems-oms-r020-drift-acknowledgement.json" = "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
    "phase-pms-ems-oms-r020-no-live-position-mutation-audit.json" = "PMS_EMS_OMS_R020_FAIL_LIVE_POSITION_MUTATION"
    "phase-pms-ems-oms-r020-no-broker-position-mutation-audit.json" = "PMS_EMS_OMS_R020_FAIL_BROKER_POSITION_MUTATION"
    "phase-pms-ems-oms-r020-no-trading-state-mutation-audit.json" = "PMS_EMS_OMS_R020_FAIL_TRADING_STATE_MUTATION"
    "phase-pms-ems-oms-r020-no-fill-no-execution-report-audit.json" = "PMS_EMS_OMS_R020_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
    "phase-pms-ems-oms-r020-no-order-created-audit.json" = "PMS_EMS_OMS_R020_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r020-no-route-no-submission-audit.json" = "PMS_EMS_OMS_R020_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED"
    "phase-pms-ems-oms-r020-idempotency-evidence.json" = "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
    "phase-pms-ems-oms-r020-risk-lineage-preservation.json" = "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
    "phase-pms-ems-oms-r020-qubes-lineage-preservation.json" = "PMS_EMS_OMS_R020_FAIL_QUBES_LINEAGE_WEAKENED"
    "phase-pms-ems-oms-r020-operator-decision-lineage-preservation.json" = "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
    "phase-pms-ems-oms-r020-simulation-result-lineage-preservation.json" = "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
    "phase-pms-ems-oms-r020-plan-lineage-preservation.json" = "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
    "phase-pms-ems-oms-r020-paper-candidate-lineage-preservation.json" = "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
    "phase-pms-ems-oms-r020-rebalance-intent-lineage-preservation.json" = "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
    "phase-pms-ems-oms-r020-lot-sizing-lineage-preservation.json" = "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
    "phase-pms-ems-oms-r020-instrument-universe-handling.json" = "PMS_EMS_OMS_R020_FAIL_LMAX_GAP_BLOCKS_POSITION_PREVIEW"
    "phase-pms-ems-oms-r020-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R020_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r020-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R020_FAIL_LMAX_GAP_BLOCKS_POSITION_PREVIEW"
    "phase-pms-ems-oms-r020-no-external-audit.json" = "PMS_EMS_OMS_R020_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r020-forbidden-actions-audit.json" = "PMS_EMS_OMS_R020_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r020-next-phase-recommendation.json" = "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
    "phase-pms-ems-oms-r020-build-test-validator-evidence.json" = "PMS_EMS_OMS_R020_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $entry.Key))) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$contract = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-simulation-result-operator-review-contract.json") "PMS_EMS_OMS_R020_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING"
$decisions = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-simulation-result-operator-decisions.json") "PMS_EMS_OMS_R020_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING"
$gate = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-paper-position-preview-gate.json") "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
$preview = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-paper-position-preview.json") "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
$previewLines = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-paper-position-preview-lines.json") "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
$holdExample = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-hold-decision-example.json") "PMS_EMS_OMS_R020_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING"
$promoteExample = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-promote-to-paper-position-preview-example.json") "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
$blockedAck = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-blocked-lines-acknowledgement.json") "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
$marksAck = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-missing-stale-mark-acknowledgement.json") "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
$driftAck = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-drift-acknowledgement.json") "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
$livePositionAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-no-live-position-mutation-audit.json") "PMS_EMS_OMS_R020_FAIL_LIVE_POSITION_MUTATION"
$brokerPositionAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-no-broker-position-mutation-audit.json") "PMS_EMS_OMS_R020_FAIL_BROKER_POSITION_MUTATION"
$tradingAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-no-trading-state-mutation-audit.json") "PMS_EMS_OMS_R020_FAIL_TRADING_STATE_MUTATION"
$fillAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-no-fill-no-execution-report-audit.json") "PMS_EMS_OMS_R020_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$orderAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-no-order-created-audit.json") "PMS_EMS_OMS_R020_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$routeAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-no-route-no-submission-audit.json") "PMS_EMS_OMS_R020_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED"
$idempotency = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-idempotency-evidence.json") "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
$risk = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-risk-lineage-preservation.json") "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
$qubes = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-qubes-lineage-preservation.json") "PMS_EMS_OMS_R020_FAIL_QUBES_LINEAGE_WEAKENED"
$operatorLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-operator-decision-lineage-preservation.json") "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
$simulationLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-simulation-result-lineage-preservation.json") "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
$planLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-plan-lineage-preservation.json") "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
$candidateLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-paper-candidate-lineage-preservation.json") "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
$rebalanceLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-rebalance-intent-lineage-preservation.json") "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
$lotSizingLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-lot-sizing-lineage-preservation.json") "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-instrument-universe-handling.json") "PMS_EMS_OMS_R020_FAIL_LMAX_GAP_BLOCKS_POSITION_PREVIEW"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R020_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R020_FAIL_LMAX_GAP_BLOCKS_POSITION_PREVIEW"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-no-external-audit.json") "PMS_EMS_OMS_R020_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-forbidden-actions-audit.json") "PMS_EMS_OMS_R020_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$nextPhase = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-next-phase-recommendation.json") "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r020-build-test-validator-evidence.json") "PMS_EMS_OMS_R020_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$contract.simulationResultOperatorReviewContractCreated) "PMS_EMS_OMS_R020_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Operator review contract missing."
foreach ($action in @("ApprovePaperSimulationResult", "Hold", "Reject", "RequestSimulationFix", "RequestRiskReview", "PromoteToPaperPositionPreview")) {
    Require-True ($contract.operatorActions -contains $action) "PMS_EMS_OMS_R020_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Missing operator action: $action"
}
foreach ($status in @("Recorded", "RejectedByGate", "DuplicateReturned", "InconclusiveSafe")) {
    Require-True ($contract.decisionStatuses -contains $status) "PMS_EMS_OMS_R020_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Missing decision status: $status"
}
foreach ($status in @("PaperPositionPreviewReady", "HeldForBlockedLines", "HeldForMissingMarks", "HeldForRiskReview", "Rejected", "InconclusiveSafe")) {
    Require-True ($contract.paperPositionPreviewStatuses -contains $status) "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Missing preview status: $status"
}
foreach ($property in @("requiresArchivedSimulationResult", "requiresPaperSimulationOnly", "requiresRealFillCountZero", "requiresExecutionReportCountZero", "requiresOrderCountZero", "requiresBrokerRouteCountZero", "requiresBlockedLineAcknowledgement", "requiresMissingStaleMarkAcknowledgement", "requiresDriftAcknowledgement", "noExternal", "nonExecutable", "notAnOrder", "notSubmitted", "noBrokerRoute")) {
    Require-True ([bool]$contract.$property) "PMS_EMS_OMS_R020_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Contract flag missing: $property"
}

Require-True ([bool]$decisions.simulationResultOperatorDecisionsCreated) "PMS_EMS_OMS_R020_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Operator decisions missing."
Require-True (@($decisions.decisions | Where-Object { $_.decisionType -eq "Hold" -and $_.decisionStatus -eq "Recorded" -and $_.resultingPaperPositionPreviewStatus -eq "HeldForBlockedLines" }).Count -eq 1) "PMS_EMS_OMS_R020_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Hold decision example missing."
Require-True (@($decisions.decisions | Where-Object { $_.decisionType -eq "PromoteToPaperPositionPreview" -and $_.decisionStatus -eq "Recorded" -and $_.resultingPaperPositionPreviewStatus -eq "PaperPositionPreviewReady" -and $_.blockedLinesAcknowledged -and $_.missingStaleMarksAcknowledged -and $_.driftAcknowledged }).Count -eq 1) "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Promote decision with acknowledgements missing."
foreach ($decision in $decisions.decisions) {
    foreach ($property in @("createsFill", "createsExecutionReport", "createsOrder", "submitsOrders", "mutatesLivePositionState", "mutatesBrokerPositionState", "mutatesLiveTradingState")) {
        Require-False ([bool]$decision.$property) "PMS_EMS_OMS_R020_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Decision unsafe flag detected: $property"
    }
}

Require-True ([bool]$gate.paperPositionPreviewGateCreated) "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Preview gate missing."
Require-True ([string]$gate.gateStatus -eq "PaperPositionPreviewReady") "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Preview gate status wrong."
Require-True ([bool]$gate.paperSimulationResultArchived) "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Archived simulation result requirement missing."
Require-True ([string]$gate.safetyStatus -eq "PaperSimulationOnly") "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Simulation result safety status wrong."
Require-True ([int]$gate.realFillCount -eq 0) "PMS_EMS_OMS_R020_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "RealFillCount is nonzero."
Require-True ([int]$gate.executionReportCount -eq 0) "PMS_EMS_OMS_R020_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "ExecutionReportCount is nonzero."
Require-True ([int]$gate.orderCount -eq 0) "PMS_EMS_OMS_R020_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "OrderCount is nonzero."
Require-True ([int]$gate.brokerRouteCount -eq 0) "PMS_EMS_OMS_R020_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "BrokerRouteCount is nonzero."
foreach ($property in @("blockedLinesAcknowledged", "missingStaleMarksAcknowledged", "driftAcknowledged", "paperOnly", "simulatedOnly", "noLivePositionMutation", "noBrokerPositionMutation", "noTradingStateMutation")) {
    Require-True ([bool]$gate.$property) "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Preview gate flag missing: $property"
}
Require-False ([bool]$gate.approvalPermitsNonzeroRealFillsExecutionReportsOrdersOrBrokerRoutes) "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_MUTABLE" "Approval permits unsafe nonzero counts."
Require-False ([bool]$gate.acknowledgementGateWeakened) "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Acknowledgement gate weakened."

Require-True ([bool]$preview.paperPositionPreviewCreated) "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Paper position preview missing."
Require-True ([string]$preview.previewStatus -eq "PaperPositionPreviewReady") "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Preview status wrong."
Require-True ([int]$preview.lineCount -eq 3) "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Preview line count wrong."
Require-True ([int]$preview.blockedLineCount -eq 10) "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Blocked line count wrong."
foreach ($property in @("paperOnly", "simulatedOnly", "noLivePositionMutation", "noBrokerPositionMutation", "noTradingStateMutation", "noFillCreated", "noExecutionReportCreated", "noOrderCreated", "noBrokerRoute")) {
    Require-True ([bool]$preview.$property) "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_MUTABLE" "Preview safety flag missing: $property"
}
foreach ($property in @("livePositionStateMutated", "brokerPositionStateMutated", "tradingStateMutated", "fillCreated", "executionReportCreated", "omsOrderCreated", "brokerOrderCreated", "orderStateCreated", "submittedOrders")) {
    Require-False ([bool]$preview.$property) "PMS_EMS_OMS_R020_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Preview unsafe flag detected: $property"
}

Require-True ([bool]$previewLines.paperPositionPreviewLinesCreated) "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Preview lines missing."
Require-True ([int]$previewLines.lineCount -eq 3) "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Preview line artifact count wrong."
Require-True (@($previewLines.lines | Where-Object { $_.normalizedSymbol -eq "AUDUSD" -and $_.side -eq "Buy" -and [decimal]$_.simulatedPositionDelta -eq 131000 -and $_.quantityCurrency -eq "AUD" }).Count -eq 1) "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "AUDUSD preview delta missing."
Require-True (@($previewLines.lines | Where-Object { $_.normalizedSymbol -eq "EURUSD" -and $_.side -eq "Buy" -and [decimal]$_.simulatedPositionDelta -eq 124000 -and $_.quantityCurrency -eq "EUR" }).Count -eq 1) "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "EURUSD preview delta missing."
Require-True (@($previewLines.lines | Where-Object { $_.normalizedSymbol -eq "GBPUSD" -and $_.side -eq "Sell" -and [decimal]$_.simulatedPositionDelta -eq -368000 -and $_.quantityCurrency -eq "GBP" }).Count -eq 1) "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "GBPUSD preview delta missing."
foreach ($line in $previewLines.lines) {
    foreach ($property in @("paperOnly", "simulatedOnly", "noLivePositionMutation", "noBrokerPositionMutation", "noTradingStateMutation", "noFillCreated", "noExecutionReportCreated", "noOrderCreated")) {
        Require-True ([bool]$line.$property) "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_MUTABLE" "Preview line safety flag missing: $property"
    }
}

Require-True ([bool]$holdExample.holdDecisionExampleCreated) "PMS_EMS_OMS_R020_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Hold example missing."
Require-True ([string]$holdExample.decisionType -eq "Hold") "PMS_EMS_OMS_R020_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Hold example wrong."
Require-True ([bool]$promoteExample.promoteToPaperPositionPreviewExampleCreated) "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Promote example missing."
Require-True ([string]$promoteExample.decisionType -eq "PromoteToPaperPositionPreview") "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Promote example wrong."
foreach ($artifact in @($blockedAck, $marksAck, $driftAck)) {
    $created = $artifact.PSObject.Properties.Name | Where-Object { $_ -match "Created$" } | Select-Object -First 1
    if ($created) { Require-True ([bool]$artifact.$created) "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Acknowledgement artifact missing: $created" }
    $required = $artifact.PSObject.Properties.Name | Where-Object { $_ -match "RequireAcknowledgement$|RequiresAcknowledgement$" } | Select-Object -First 1
    if ($required) { Require-True ([bool]$artifact.$required) "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Acknowledgement requirement missing: $required" }
    $provided = $artifact.PSObject.Properties.Name | Where-Object { $_ -match "AcknowledgedForPromotion$" } | Select-Object -First 1
    if ($provided) { Require-True ([bool]$artifact.$provided) "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Acknowledgement not provided: $provided" }
    Require-False ([bool]$artifact.acknowledgementGateWeakened) "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Acknowledgement gate weakened."
}

Require-True ([bool]$livePositionAudit.noLivePositionMutationAuditCreated) "PMS_EMS_OMS_R020_FAIL_LIVE_POSITION_MUTATION" "No-live-position audit missing."
Require-False ([bool]$livePositionAudit.livePositionStateMutated) "PMS_EMS_OMS_R020_FAIL_LIVE_POSITION_MUTATION" "Live position state mutated."
Require-False ([bool]$livePositionAudit.paperPreviewMutatesLivePositions) "PMS_EMS_OMS_R020_FAIL_LIVE_POSITION_MUTATION" "Paper preview mutates live positions."
Require-True ([bool]$brokerPositionAudit.noBrokerPositionMutationAuditCreated) "PMS_EMS_OMS_R020_FAIL_BROKER_POSITION_MUTATION" "No-broker-position audit missing."
Require-False ([bool]$brokerPositionAudit.brokerPositionStateMutated) "PMS_EMS_OMS_R020_FAIL_BROKER_POSITION_MUTATION" "Broker position state mutated."
Require-False ([bool]$brokerPositionAudit.paperPreviewMutatesBrokerPositions) "PMS_EMS_OMS_R020_FAIL_BROKER_POSITION_MUTATION" "Paper preview mutates broker positions."
Require-True ([bool]$tradingAudit.noTradingStateMutationAuditCreated) "PMS_EMS_OMS_R020_FAIL_TRADING_STATE_MUTATION" "No-trading-state audit missing."
Require-False ([bool]$tradingAudit.tradingStateMutated) "PMS_EMS_OMS_R020_FAIL_TRADING_STATE_MUTATION" "Trading state mutated."
Require-False ([bool]$tradingAudit.liveTradingStateMutated) "PMS_EMS_OMS_R020_FAIL_TRADING_STATE_MUTATION" "Live trading state mutated."
Require-False ([bool]$tradingAudit.paperPreviewMutatesTradingState) "PMS_EMS_OMS_R020_FAIL_TRADING_STATE_MUTATION" "Paper preview mutates trading state."

Require-True ([bool]$fillAudit.noFillNoExecutionReportAuditCreated) "PMS_EMS_OMS_R020_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill/execution-report audit missing."
foreach ($property in @("fillCreated", "executionReportCreated", "simulationFillCreatedAsRealFill", "brokerExecutionReportCreated", "paperPreviewCreatesFillOrExecutionReport")) {
    Require-False ([bool]$fillAudit.$property) "PMS_EMS_OMS_R020_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill/report audit detected: $property"
}
Require-True ([bool]$orderAudit.noOrderCreatedAuditCreated) "PMS_EMS_OMS_R020_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No-order audit missing."
foreach ($property in @("omsOrderCreated", "parentOrderCreated", "childOrderCreated", "brokerOrderCreated", "executableOrderCreated", "orderStateCreated", "orderSubmissionPathIntroduced", "ordersSubmitted")) {
    Require-False ([bool]$orderAudit.$property) "PMS_EMS_OMS_R020_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order audit detected: $property"
}
Require-True ([bool]$routeAudit.noRouteNoSubmissionAuditCreated) "PMS_EMS_OMS_R020_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "No-route/no-submission audit missing."
foreach ($property in @("brokerRouteCreated", "brokerRouteAssigned", "submissionInstructionCreated", "orderSubmissionPathInvoked", "paperPreviewSubmitted", "paperPreviewRouteable")) {
    Require-False ([bool]$routeAudit.$property) "PMS_EMS_OMS_R020_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "Route/submission audit detected: $property"
}

Require-True ([bool]$idempotency.idempotencyEvidenceCreated) "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Idempotency evidence missing."
Require-True ([string]$idempotency.decisionIdempotencyKey -eq "OperatorDecisionId") "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Decision idempotency key wrong."
Require-True ([string]$idempotency.previewIdempotencyKey -eq "PaperPositionPreviewId") "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Preview idempotency key wrong."
Require-True ([string]$idempotency.duplicateDecisionBehavior -eq "DuplicateReturned") "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Duplicate decision behavior wrong."
Require-True ([string]$idempotency.duplicatePreviewBehavior -eq "ExistingPreviewReturned") "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Duplicate preview behavior wrong."
Require-False ([bool]$idempotency.duplicatesCreateAdditionalDecisionsOrPreviews) "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Duplicates create additional records."

Require-True ([bool]$risk.riskLineagePreservationCreated) "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Risk lineage missing."
Require-True ([bool]$risk.riskLineagePreserved) "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Risk lineage not preserved."
Require-False ([bool]$risk.riskLineageMissing) "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Risk lineage marked missing."
Require-True ([bool]$qubes.qubesLineagePreservationCreated) "PMS_EMS_OMS_R020_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage missing."
Require-True ([string]$qubes.sourceSystem -eq "ModelWeightSourceSystem.Qubes") "PMS_EMS_OMS_R020_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes source missing."
Require-True ([int]$qubes.cadenceMinutes -eq 15) "PMS_EMS_OMS_R020_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes cadence missing."
Require-False ([bool]$qubes.qubesLineageWeakened) "PMS_EMS_OMS_R020_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage weakened."
foreach ($lineage in @($operatorLineage, $simulationLineage, $planLineage, $candidateLineage, $rebalanceLineage, $lotSizingLineage)) {
    $created = $lineage.PSObject.Properties.Name | Where-Object { $_ -match "Created$" } | Select-Object -First 1
    if ($created) { Require-True ([bool]$lineage.$created) "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Lineage artifact missing: $created" }
    $missing = $lineage.PSObject.Properties.Name | Where-Object { $_ -match "Missing$" } | Select-Object -First 1
    if ($missing) { Require-False ([bool]$lineage.$missing) "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Lineage marked missing: $missing" }
}

Require-True ([bool]$universe.instrumentUniverseHandlingCreated) "PMS_EMS_OMS_R020_FAIL_LMAX_GAP_BLOCKS_POSITION_PREVIEW" "Instrument universe handling missing."
Require-False ([bool]$universe.lmaxLiveValidationGapsBlockPaperPositionPreview) "PMS_EMS_OMS_R020_FAIL_LMAX_GAP_BLOCKS_POSITION_PREVIEW" "LMAX gaps block preview."
Require-False ([bool]$universe.audusdClassifiedAsFailed) "PMS_EMS_OMS_R020_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD classified as failed."
Require-False ([bool]$universe.audusdLiveValidationGapBlocksPaperPositionPreview) "PMS_EMS_OMS_R020_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD gap blocks preview."
Require-False ([bool]$universe.usdjpyLiveValidationGapBlocksPaperPositionPreview) "PMS_EMS_OMS_R020_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY gap blocks preview."
Require-True ([bool]$usdjpy.usdJpyCaveatPreservationCreated) "PMS_EMS_OMS_R020_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
Require-True ([string]$usdjpy.usdJpySecurityId -eq "4004") "PMS_EMS_OMS_R020_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID missing."
Require-True ([string]$usdjpy.usdJpySecurityIdSource -eq "8") "PMS_EMS_OMS_R020_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource missing."
Require-False ([bool]$usdjpy.usdJpyClassifiedAsFailed) "PMS_EMS_OMS_R020_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY classified as failed."
Require-False ([bool]$usdjpy.audusdClassifiedAsFailed) "PMS_EMS_OMS_R020_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD classified as failed."
Require-True ([bool]$lmax.lmaxReadonlyBaselineReferenceCreated) "PMS_EMS_OMS_R020_FAIL_LMAX_GAP_BLOCKS_POSITION_PREVIEW" "LMAX baseline missing."
Require-False ([bool]$lmax.lmaxUsedInThisPhase) "PMS_EMS_OMS_R020_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX used in R020."
Require-False ([bool]$lmax.lmaxCalledInThisPhase) "PMS_EMS_OMS_R020_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX called in R020."
Require-False ([bool]$lmax.lmaxLiveValidationGapsBlockPaperPositionPreview) "PMS_EMS_OMS_R020_FAIL_LMAX_GAP_BLOCKS_POSITION_PREVIEW" "LMAX gaps block preview."

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
    "simulationFillCreatedAsRealFill",
    "brokerExecutionReportCreated",
    "liveTradingPathIntroduced",
    "tradingStateMutated",
    "livePositionStateMutated",
    "brokerPositionStateMutated",
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
    "paperPreviewMutatesLivePositions",
    "paperPreviewMutatesBrokerPositions",
    "paperPreviewMutatesTradingState",
    "lmaxLiveValidationGapsBlockPaperPositionPreview"
)) {
    Require-False ([bool]$noExternal.$property) "PMS_EMS_OMS_R020_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected: $property"
}

foreach ($item in $forbidden.forbiddenActions) {
    if ([bool]$item.detected) {
        if ([string]$item.action -match "scheduler|service|timer|background") {
            Fail-Gate "PMS_EMS_OMS_R020_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "position state") {
            if ([string]$item.action -match "broker") { Fail-Gate "PMS_EMS_OMS_R020_FAIL_BROKER_POSITION_MUTATION" "Forbidden action detected: $($item.action)" }
            Fail-Gate "PMS_EMS_OMS_R020_FAIL_LIVE_POSITION_MUTATION" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "trading state") {
            Fail-Gate "PMS_EMS_OMS_R020_FAIL_TRADING_STATE_MUTATION" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "fill|execution report") {
            Fail-Gate "PMS_EMS_OMS_R020_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "order|trading|executable|OMS|parent|child|broker|submitted|routed") {
            Fail-Gate "PMS_EMS_OMS_R020_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden action detected: $($item.action)"
        }

        Fail-Gate "PMS_EMS_OMS_R020_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $($item.action)"
    }
}

Require-True ([bool]$nextPhase.nextPhaseRecommendationCreated) "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Next phase recommendation missing."
Require-True ([string]$nextPhase.recommendedNextPhase -eq "PMS-EMS-OMS-R021") "PMS_EMS_OMS_R020_FAIL_POSITION_PREVIEW_GATE_MISSING" "Next phase is not R021."
Require-True ([bool]$nextPhase.mustRemainNoExternal) "PMS_EMS_OMS_R020_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase not no-external."

$apiSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Api/appsettings.json") "PMS_EMS_OMS_R020_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$workerSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/appsettings.json") "PMS_EMS_OMS_R020_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-False ([bool]$apiSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R020_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API live trading enabled."
Require-False ([bool]$workerSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R020_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker live trading enabled."
Require-False ([bool]$apiSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R020_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API external enabled."
Require-False ([bool]$workerSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R020_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker external enabled."
Require-True ([bool]$apiSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R020_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API fake gateway not required."
Require-True ([bool]$workerSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R020_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker fake gateway not required."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.Enabled) "PMS_EMS_OMS_R020_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX runtime enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowExternalConnections) "PMS_EMS_OMS_R020_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX external enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowOrderSubmission) "PMS_EMS_OMS_R020_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "LMAX order submission enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SchedulerEnabled) "PMS_EMS_OMS_R020_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "LMAX scheduler enabled."
Require-False ([bool]$workerSettings.MarketDataBars.Enabled) "PMS_EMS_OMS_R020_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker market data bars enabled."

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-pms-ems-oms-r020-*" -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combined = [string]::Join("`n", $artifactText)
foreach ($pattern in @("\u0001", "35=", "MDReqID\s*[:=]", "SenderCompID\s*[:=]", "TargetCompID\s*[:=]", "BeginString\s*[:=]", "SocketHost\s*[:=]", "TlsHost\s*[:=]", "Password\s*[:=]", "ApiKey\s*[:=]", "Bearer\s+[A-Za-z0-9_\.-]+", "rawBid", "rawAsk", "rawMid")) {
    if ($combined -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R020_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Unsafe serialized content pattern found: $pattern"
    }
}

$requiredFiles = @(
    "src/QQ.Production.Intraday.Application/QubesPaperSimulationResultOperatorReview.cs",
    "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperSimulationResultOperatorReviewTests.cs"
)
foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $file))) {
        Fail-Gate "PMS_EMS_OMS_R020_FAIL_BUILD_OR_TESTS" "Required file missing: $file"
    }
}

$source = Get-Content -LiteralPath (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/QubesPaperSimulationResultOperatorReview.cs") -Raw
foreach ($pattern in @("TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "SendOrderAsync", "SubmitOrder", "FixSession", "CreateOrderAsync")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R020_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "R020 source contains forbidden runtime pattern: $pattern"
    }
}
foreach ($pattern in @("AddHostedService", "IHostedService", "BackgroundService", "PeriodicTimer", "Task.Delay", "System.Threading.Timer")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R020_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "R020 source contains scheduler/service pattern: $pattern"
    }
}

$tests = Get-Content -LiteralPath (Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperSimulationResultOperatorReviewTests.cs") -Raw
foreach ($requiredTestName in @(
    "R019_archived_paper_simulation_result_can_enter_operator_review",
    "Approve_paper_simulation_result_records_decision_without_external_action",
    "Hold_records_decision_without_external_action",
    "Reject_records_decision_without_external_action",
    "Request_simulation_fix_records_decision_safely",
    "Promote_to_paper_position_preview_creates_paper_preview_only",
    "Paper_preview_includes_audusd_simulated_delta",
    "Paper_preview_includes_eurusd_simulated_delta",
    "Paper_preview_includes_gbpusd_sell_delta",
    "Paper_preview_does_not_mutate_live_positions",
    "Paper_preview_does_not_mutate_broker_positions",
    "Paper_preview_does_not_mutate_trading_state",
    "Real_fill_count_zero_is_required",
    "Execution_report_count_zero_is_required",
    "Order_count_zero_is_required",
    "Broker_route_count_zero_is_required",
    "Blocked_lines_require_acknowledgement",
    "Missing_stale_marks_require_acknowledgement",
    "Drift_requires_acknowledgement",
    "Duplicate_decision_or_preview_handling_is_idempotent",
    "Qubes_run_id_and_cycle_run_id_are_preserved",
    "Simulation_result_lineage_is_preserved",
    "Plan_candidate_risk_rebalance_and_lot_sizing_lineage_is_preserved",
    "Review_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action",
    "Api_and_worker_live_gateway_remain_disabled",
    "Review_source_introduces_no_scheduler_timer_polling_or_background_job",
    "No_fills_are_created",
    "No_execution_reports_are_created",
    "No_oms_or_broker_orders_are_created",
    "Audusd_is_not_misclassified_as_failed",
    "Usdjpy_caveat_remains_preserved"
)) {
    if ($tests -notmatch [regex]::Escape($requiredTestName)) {
        Fail-Gate "PMS_EMS_OMS_R020_FAIL_BUILD_OR_TESTS" "Focused test missing: $requiredTestName"
    }
}

Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R020_FAIL_BUILD_OR_TESTS" "Evidence marker missing."
Require-True ([string]$evidence.build.status -eq "PASS") "PMS_EMS_OMS_R020_FAIL_BUILD_OR_TESTS" "Build evidence missing or not PASS."
Require-True ([int]$evidence.build.failed -eq 0) "PMS_EMS_OMS_R020_FAIL_BUILD_OR_TESTS" "Build evidence has failures."
Require-True ([string]$evidence.focusedTests.status -eq "PASS") "PMS_EMS_OMS_R020_FAIL_BUILD_OR_TESTS" "Focused test evidence missing or not PASS."
Require-True ([int]$evidence.focusedTests.failed -eq 0) "PMS_EMS_OMS_R020_FAIL_BUILD_OR_TESTS" "Focused tests have failures."
Require-True ([string]$evidence.unitTests.status -eq "PASS") "PMS_EMS_OMS_R020_FAIL_BUILD_OR_TESTS" "Unit test evidence missing or not PASS."
Require-True ([int]$evidence.unitTests.failed -eq 0) "PMS_EMS_OMS_R020_FAIL_BUILD_OR_TESTS" "Unit tests have failures."
Require-True ([string]$evidence.validator.status -eq "PASS") "PMS_EMS_OMS_R020_FAIL_BUILD_OR_TESTS" "Validator evidence missing or not PASS."
Require-True ([int]$evidence.validator.failed -eq 0) "PMS_EMS_OMS_R020_FAIL_BUILD_OR_TESTS" "Validator evidence has failures."

Write-Host "PMS_EMS_OMS_R020_PASS_SIMULATION_RESULT_OPERATOR_REVIEW_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R020_PASS_PAPER_POSITION_PREVIEW_GATE_READY_NO_EXTERNAL"
