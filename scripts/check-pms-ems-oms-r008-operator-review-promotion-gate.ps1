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
    "phase-pms-ems-oms-r008-summary.md" = "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING"
    "phase-pms-ems-oms-r008-operator-review-contract.json" = "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING"
    "phase-pms-ems-oms-r008-operator-decision-fixtures.json" = "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING"
    "phase-pms-ems-oms-r008-cycle-promotion-decision-gate.json" = "PMS_EMS_OMS_R008_FAIL_PROMOTION_GATE_MISSING"
    "phase-pms-ems-oms-r008-hold-decision-example.json" = "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING"
    "phase-pms-ems-oms-r008-promote-to-paper-ready-example.json" = "PMS_EMS_OMS_R008_FAIL_PROMOTION_GATE_MISSING"
    "phase-pms-ems-oms-r008-non-executable-promotion-audit.json" = "PMS_EMS_OMS_R008_FAIL_PROMOTION_EXECUTABLE"
    "phase-pms-ems-oms-r008-rebalance-intent-preservation.json" = "PMS_EMS_OMS_R008_FAIL_REBALANCE_INTENT_EXECUTABLE"
    "phase-pms-ems-oms-r008-missing-stale-mark-acknowledgement.json" = "PMS_EMS_OMS_R008_FAIL_MISSING_STALE_ACK_MISSING"
    "phase-pms-ems-oms-r008-drift-preservation.json" = "PMS_EMS_OMS_R008_FAIL_DRIFT_PRESERVATION_MISSING"
    "phase-pms-ems-oms-r008-qubes-lineage-preservation.json" = "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING"
    "phase-pms-ems-oms-r008-cycle-idempotency-evidence.json" = "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING"
    "phase-pms-ems-oms-r008-instrument-universe-handling.json" = "PMS_EMS_OMS_R008_FAIL_LMAX_GAP_BLOCKS_REVIEW"
    "phase-pms-ems-oms-r008-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R008_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r008-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R008_FAIL_LMAX_GAP_BLOCKS_REVIEW"
    "phase-pms-ems-oms-r008-no-external-audit.json" = "PMS_EMS_OMS_R008_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r008-forbidden-actions-audit.json" = "PMS_EMS_OMS_R008_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r008-next-phase-recommendation.json" = "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING"
    "phase-pms-ems-oms-r008-build-test-validator-evidence.json" = "PMS_EMS_OMS_R008_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    $path = Join-Path $artifactRoot $entry.Key
    if (-not (Test-Path -LiteralPath $path)) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$contract = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r008-operator-review-contract.json") "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING"
$fixtures = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r008-operator-decision-fixtures.json") "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING"
$gate = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r008-cycle-promotion-decision-gate.json") "PMS_EMS_OMS_R008_FAIL_PROMOTION_GATE_MISSING"
$hold = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r008-hold-decision-example.json") "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING"
$promote = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r008-promote-to-paper-ready-example.json") "PMS_EMS_OMS_R008_FAIL_PROMOTION_GATE_MISSING"
$promotionAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r008-non-executable-promotion-audit.json") "PMS_EMS_OMS_R008_FAIL_PROMOTION_EXECUTABLE"
$intents = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r008-rebalance-intent-preservation.json") "PMS_EMS_OMS_R008_FAIL_REBALANCE_INTENT_EXECUTABLE"
$marks = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r008-missing-stale-mark-acknowledgement.json") "PMS_EMS_OMS_R008_FAIL_MISSING_STALE_ACK_MISSING"
$drift = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r008-drift-preservation.json") "PMS_EMS_OMS_R008_FAIL_DRIFT_PRESERVATION_MISSING"
$lineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r008-qubes-lineage-preservation.json") "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING"
$idempotency = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r008-cycle-idempotency-evidence.json") "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r008-instrument-universe-handling.json") "PMS_EMS_OMS_R008_FAIL_LMAX_GAP_BLOCKS_REVIEW"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r008-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R008_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r008-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R008_FAIL_LMAX_GAP_BLOCKS_REVIEW"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r008-no-external-audit.json") "PMS_EMS_OMS_R008_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r008-forbidden-actions-audit.json") "PMS_EMS_OMS_R008_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$nextPhase = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r008-next-phase-recommendation.json") "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r008-build-test-validator-evidence.json") "PMS_EMS_OMS_R008_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$contract.operatorReviewContractCreated) "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Operator review contract missing."
foreach ($decisionType in @("Approve", "Hold", "Reject", "RequestDataFix", "PromoteToPaperReady")) {
    Require-True (($contract.supportedDecisionTypes -contains $decisionType)) "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Decision type missing: $decisionType"
}
Require-True (($contract.supportedDecisionStatuses -contains "Recorded")) "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Recorded status missing."
Require-True (($contract.supportedDecisionStatuses -contains "RejectedByGate")) "PMS_EMS_OMS_R008_FAIL_PROMOTION_GATE_MISSING" "RejectedByGate status missing."
Require-True (($contract.supportedDecisionStatuses -contains "DuplicateReturned")) "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Duplicate status missing."
foreach ($field in @("CycleRunId", "QubesRunId", "OperatorDecisionId", "DecisionType", "DecisionStatus", "ReviewedAtUtc", "ReviewedBy", "ReasonCategory", "CommentSanitized", "ResultingCycleReviewStatus")) {
    Require-True (($contract.decisionRecordFields -contains $field)) "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Decision field missing: $field"
}
Require-True ([bool]$contract.reviewedByIsPlaceholderOrSanitized) "PMS_EMS_OMS_R008_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "ReviewedBy sanitization missing."
Require-True ([bool]$contract.commentIsSanitized) "PMS_EMS_OMS_R008_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Comment sanitization missing."
Require-True ([bool]$contract.operatorActionsNonExecuting) "PMS_EMS_OMS_R008_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Operator actions are executable."
Require-True ([bool]$contract.usesR007CycleArchive) "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "R007 archive not reused."
Require-True ([bool]$contract.usesR007OperatorReport) "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "R007 report not reused."

Require-True ([bool]$fixtures.operatorDecisionFixturesCreated) "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Decision fixtures missing."
Require-True (@($fixtures.fixtures | Where-Object { $_.decisionType -eq "Hold" -and $_.reasonCategory -eq "HeldDueToMissingMarks" }).Count -ge 1) "PMS_EMS_OMS_R008_FAIL_MISSING_STALE_ACK_MISSING" "Hold missing-marks decision fixture missing."
Require-True (@($fixtures.fixtures | Where-Object { $_.decisionType -eq "PromoteToPaperReady" -and $_.noExternalPaperReadyOnly -eq $true }).Count -ge 1) "PMS_EMS_OMS_R008_FAIL_PROMOTION_GATE_MISSING" "PromoteToPaperReady fixture missing."

Require-True ([bool]$gate.cyclePromotionDecisionGateCreated) "PMS_EMS_OMS_R008_FAIL_PROMOTION_GATE_MISSING" "Promotion gate missing."
Require-True ([string]$gate.promotionScope -eq "NoExternalPaperReadyOnly") "PMS_EMS_OMS_R008_FAIL_PROMOTION_EXECUTABLE" "Promotion scope is not paper-only."
Require-False ([bool]$gate.promoteToPaperReadyEnablesLiveTrading) "PMS_EMS_OMS_R008_FAIL_PROMOTION_EXECUTABLE" "Promotion enables live trading."
Require-False ([bool]$gate.promoteToPaperReadyEnablesOrderSubmission) "PMS_EMS_OMS_R008_FAIL_PROMOTION_EXECUTABLE" "Promotion enables order submission."
Require-False ([bool]$gate.promoteToPaperReadyCreatesExecutableOrders) "PMS_EMS_OMS_R008_FAIL_PROMOTION_EXECUTABLE" "Promotion creates executable orders."
Require-True ([bool]$gate.completedWithMissingMarksRequiresAcknowledgement) "PMS_EMS_OMS_R008_FAIL_MISSING_STALE_ACK_MISSING" "Missing-mark acknowledgement gate missing."
Require-True ([bool]$gate.driftRequiresAcknowledgementForPaperPromotion) "PMS_EMS_OMS_R008_FAIL_DRIFT_PRESERVATION_MISSING" "Drift acknowledgement gate missing."
Require-False ([bool]$gate.validationFailureCanBePromoted) "PMS_EMS_OMS_R008_FAIL_PROMOTION_GATE_MISSING" "Validation failure can be promoted."
Require-True ([bool]$gate.missingAcknowledgementGateRejectsWhenMissing) "PMS_EMS_OMS_R008_FAIL_MISSING_STALE_ACK_MISSING" "Missing acknowledgement rejection missing."
Require-True ([bool]$gate.paperPromotionAcceptedAfterAcknowledgements) "PMS_EMS_OMS_R008_FAIL_PROMOTION_GATE_MISSING" "Acknowledged paper promotion not accepted."
Require-False ([bool]$gate.liveTradingPromotionAllowed) "PMS_EMS_OMS_R008_FAIL_PROMOTION_EXECUTABLE" "Live trading promotion allowed."

Require-True ([bool]$hold.holdDecisionExampleCreated) "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Hold decision example missing."
Require-True ([string]$hold.decisionType -eq "Hold") "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Hold decision type incorrect."
Require-True ([string]$hold.reasonCategory -eq "HeldDueToMissingMarks") "PMS_EMS_OMS_R008_FAIL_MISSING_STALE_ACK_MISSING" "Hold reason missing."
Require-True ([bool]$hold.missingStaleMarkWarningsPreserved) "PMS_EMS_OMS_R008_FAIL_MISSING_STALE_ACK_MISSING" "Hold does not preserve missing marks."
Require-True ([bool]$hold.theoreticalVsRealDriftPreserved) "PMS_EMS_OMS_R008_FAIL_DRIFT_PRESERVATION_MISSING" "Hold does not preserve drift."
Require-True ([bool]$hold.nonExecuting) "PMS_EMS_OMS_R008_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Hold is executable."

Require-True ([bool]$promote.promoteToPaperReadyExampleCreated) "PMS_EMS_OMS_R008_FAIL_PROMOTION_GATE_MISSING" "Promote example missing."
Require-True ([string]$promote.decisionType -eq "PromoteToPaperReady") "PMS_EMS_OMS_R008_FAIL_PROMOTION_GATE_MISSING" "Promote decision type incorrect."
Require-True ([string]$promote.resultingCycleReviewStatus -eq "PaperReadyNoExternal") "PMS_EMS_OMS_R008_FAIL_PROMOTION_EXECUTABLE" "Promote result is not paper no-external."
Require-True ([bool]$promote.missingStaleMarksAcknowledged) "PMS_EMS_OMS_R008_FAIL_MISSING_STALE_ACK_MISSING" "Promote missing-mark acknowledgement missing."
Require-True ([bool]$promote.driftAcknowledged) "PMS_EMS_OMS_R008_FAIL_DRIFT_PRESERVATION_MISSING" "Promote drift acknowledgement missing."
Require-True ([string]$promote.noExternalDisclaimer -match "Paper-ready means no-external") "PMS_EMS_OMS_R008_FAIL_PROMOTION_GATE_MISSING" "No-external promotion disclaimer missing."
Require-False ([bool]$promote.promotionIsExecutable) "PMS_EMS_OMS_R008_FAIL_PROMOTION_EXECUTABLE" "Promotion is executable."
Require-False ([bool]$promote.enablesLiveTrading) "PMS_EMS_OMS_R008_FAIL_PROMOTION_EXECUTABLE" "Promotion enables live trading."
Require-False ([bool]$promote.createsOrders) "PMS_EMS_OMS_R008_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Promotion creates orders."
Require-False ([bool]$promote.submitsOrders) "PMS_EMS_OMS_R008_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Promotion submits orders."

foreach ($property in @(
    "promotionIsExecutable",
    "promoteToPaperReadyEnablesLiveTrading",
    "promoteToPaperReadyEnablesOrderSubmission",
    "brokerGatewayCalled",
    "liveMarketDataRequested",
    "apiOrWorkerStarted",
    "schedulerOrBackgroundJobStarted",
    "omsOrderCreated",
    "parentOrderCreated",
    "childOrderCreated",
    "brokerOrderCreated",
    "executableOrderCreated",
    "ordersSubmitted",
    "liveTradingStateMutated"
)) {
    Require-False ([bool]$promotionAudit.$property) "PMS_EMS_OMS_R008_FAIL_PROMOTION_EXECUTABLE" "Promotion audit detected: $property"
}
Require-True ([bool]$promotionAudit.nonExecutablePromotionAuditCreated) "PMS_EMS_OMS_R008_FAIL_PROMOTION_EXECUTABLE" "Non-executable promotion audit missing."
Require-True ([bool]$promotionAudit.promotionIsNoExternalPaperReadyOnly) "PMS_EMS_OMS_R008_FAIL_PROMOTION_EXECUTABLE" "Promotion not paper-ready only."

Require-True ([bool]$intents.rebalanceIntentPreservationCreated) "PMS_EMS_OMS_R008_FAIL_REBALANCE_INTENT_EXECUTABLE" "Rebalance intent preservation missing."
Require-True ([bool]$intents.rebalanceIntentsRemainNonExecutable) "PMS_EMS_OMS_R008_FAIL_REBALANCE_INTENT_EXECUTABLE" "Rebalance intents became executable."
Require-True ([bool]$intents.allIntentLinesHaveTheoreticalOnlyStatus) "PMS_EMS_OMS_R008_FAIL_REBALANCE_INTENT_EXECUTABLE" "TheoreticalOnly status missing."
Require-True ([bool]$intents.allIntentLinesHaveNotExecutableStatus) "PMS_EMS_OMS_R008_FAIL_REBALANCE_INTENT_EXECUTABLE" "NotExecutable status missing."
Require-True ([bool]$intents.allIntentLinesHaveBlockedNoOmsStatus) "PMS_EMS_OMS_R008_FAIL_REBALANCE_INTENT_EXECUTABLE" "BlockedNoOMS status missing."
Require-False ([bool]$intents.operatorDecisionChangedIntentExecutability) "PMS_EMS_OMS_R008_FAIL_REBALANCE_INTENT_EXECUTABLE" "Operator decision changed intent executability."
Require-False ([bool]$intents.executableOrderCreatedFromPromotion) "PMS_EMS_OMS_R008_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Executable order created from promotion."
Require-False ([bool]$intents.orderSubmissionIntroduced) "PMS_EMS_OMS_R008_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order submission introduced."

Require-True ([bool]$marks.missingStaleMarkAcknowledgementCreated) "PMS_EMS_OMS_R008_FAIL_MISSING_STALE_ACK_MISSING" "Missing/stale acknowledgement missing."
Require-True ([string]$marks.cycleStatus -eq "CompletedWithMissingMarks") "PMS_EMS_OMS_R008_FAIL_MISSING_STALE_ACK_MISSING" "Cycle status did not preserve missing marks."
Require-True ([bool]$marks.missingMarkStatusPreserved) "PMS_EMS_OMS_R008_FAIL_MISSING_STALE_ACK_MISSING" "MissingMark not preserved."
Require-True ([bool]$marks.staleMarkStatusPreserved) "PMS_EMS_OMS_R008_FAIL_MISSING_STALE_ACK_MISSING" "StaleMark not preserved."
Require-True ([bool]$marks.promotionRequiresExplicitAcknowledgement) "PMS_EMS_OMS_R008_FAIL_MISSING_STALE_ACK_MISSING" "Promotion acknowledgement missing."
Require-True ([bool]$marks.promotionWithoutAcknowledgementRejected) "PMS_EMS_OMS_R008_FAIL_MISSING_STALE_ACK_MISSING" "Promotion without acknowledgement not rejected."
Require-True ([bool]$marks.promotionWithAcknowledgementAccepted) "PMS_EMS_OMS_R008_FAIL_MISSING_STALE_ACK_MISSING" "Promotion with acknowledgement not accepted."
Require-False ([bool]$marks.missingOrStaleMarksHidden) "PMS_EMS_OMS_R008_FAIL_MISSING_STALE_ACK_MISSING" "Missing/stale marks hidden."

Require-True ([bool]$drift.driftPreservationCreated) "PMS_EMS_OMS_R008_FAIL_DRIFT_PRESERVATION_MISSING" "Drift preservation missing."
Require-True ([string]$drift.theoreticalVsRealStatus -eq "Drift") "PMS_EMS_OMS_R008_FAIL_DRIFT_PRESERVATION_MISSING" "Drift status missing."
Require-True ([decimal]$drift.theoreticalPnl -eq 6804.66) "PMS_EMS_OMS_R008_FAIL_DRIFT_PRESERVATION_MISSING" "Theoretical PnL changed."
Require-True ([decimal]$drift.actualFixturePnl -eq 6704.66) "PMS_EMS_OMS_R008_FAIL_DRIFT_PRESERVATION_MISSING" "Actual fixture PnL changed."
Require-True ([decimal]$drift.pnlDifference -eq -100.00) "PMS_EMS_OMS_R008_FAIL_DRIFT_PRESERVATION_MISSING" "PnL difference changed."
Require-True ([bool]$drift.driftPreservedInOperatorReview) "PMS_EMS_OMS_R008_FAIL_DRIFT_PRESERVATION_MISSING" "Drift not preserved in review."
Require-True ([bool]$drift.driftAcknowledgementRequiredForPaperPromotion) "PMS_EMS_OMS_R008_FAIL_DRIFT_PRESERVATION_MISSING" "Drift acknowledgement missing."
Require-True ([bool]$drift.driftPromotesOnlyToPaperReview) "PMS_EMS_OMS_R008_FAIL_PROMOTION_EXECUTABLE" "Drift promotes outside paper review."
Require-False ([bool]$drift.liveTradingApprovalCreated) "PMS_EMS_OMS_R008_FAIL_PROMOTION_EXECUTABLE" "Live trading approval created."

Require-True ([bool]$lineage.qubesLineagePreservationCreated) "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Qubes lineage missing."
Require-True ([string]$lineage.sourceSystem -eq "ModelWeightSourceSystem.Qubes") "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Qubes source missing."
Require-True ([int]$lineage.cadenceMinutes -eq 15) "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Qubes cadence missing."
Require-True ([bool]$lineage.qubesAuditBatchPreserved) "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Qubes audit batch not preserved."
Require-True ([bool]$lineage.rawQubesRowAuditPreserved) "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Raw row audit not preserved."
Require-True ([bool]$lineage.normalizedRowAuditPreserved) "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Normalized row audit not preserved."
Require-True ([bool]$lineage.modelWeightBatchLinkagePreserved) "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "ModelWeightBatch not preserved."
Require-True ([bool]$lineage.modelRunLinkagePreserved) "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "ModelRun not preserved."
Require-True ([bool]$lineage.targetWeightLinkagePreserved) "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "TargetWeight not preserved."

Require-True ([bool]$idempotency.cycleIdempotencyEvidenceCreated) "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Idempotency evidence missing."
Require-True ([string]$idempotency.idempotencyKey -eq "OperatorDecisionId") "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Idempotency key wrong."
Require-True ([bool]$idempotency.firstDecisionPersisted) "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "First decision not persisted."
Require-True ([bool]$idempotency.secondDecisionAlreadyRecorded) "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Duplicate decision not recognized."
Require-False ([bool]$idempotency.secondDecisionPersisted) "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Duplicate decision persisted."
Require-False ([bool]$idempotency.duplicateDecisionRecordsCreated) "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Duplicate records created."
Require-True ([bool]$idempotency.returnsExistingDecisionRecord) "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Existing decision not returned."
Require-True ([bool]$idempotency.safeDuplicateHandling) "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Duplicate handling unsafe."

Require-True ([bool]$universe.instrumentUniverseHandlingCreated) "PMS_EMS_OMS_R008_FAIL_LMAX_GAP_BLOCKS_REVIEW" "Instrument universe handling missing."
Require-False ([bool]$universe.lmaxReadOnlyScopeUsedAsReviewGate) "PMS_EMS_OMS_R008_FAIL_LMAX_GAP_BLOCKS_REVIEW" "LMAX scope gates review."
Require-False ([bool]$universe.lmaxLiveValidationGapsBlockReview) "PMS_EMS_OMS_R008_FAIL_LMAX_GAP_BLOCKS_REVIEW" "LMAX gaps block review."
Require-False ([bool]$universe.audusdLiveValidationGapBlocksReview) "PMS_EMS_OMS_R008_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD gap blocks review."
Require-False ([bool]$universe.usdjpyLiveValidationGapBlocksReview) "PMS_EMS_OMS_R008_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY gap blocks review."
Require-True ([bool]$universe.instrumentsWithoutBrokerValidationHandledSafely) "PMS_EMS_OMS_R008_FAIL_LMAX_GAP_BLOCKS_REVIEW" "Unvalidated instruments not safe."

Require-True ([bool]$usdjpy.usdJpyCaveatPreservationCreated) "PMS_EMS_OMS_R008_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat artifact missing."
Require-True ([string]$usdjpy.usdJpySecurityId -eq "4004") "PMS_EMS_OMS_R008_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID caveat missing."
Require-True ([string]$usdjpy.usdJpySecurityIdSource -eq "8") "PMS_EMS_OMS_R008_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource caveat missing."
Require-True ([bool]$usdjpy.usdJpyNotProven) "PMS_EMS_OMS_R008_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY not-proven missing."
Require-False ([bool]$usdjpy.usdJpyClassifiedAsFailed) "PMS_EMS_OMS_R008_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY classified failed."
Require-True ([bool]$usdjpy.audusdTlsBoundaryInconclusive) "PMS_EMS_OMS_R008_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD TLS status missing."
Require-False ([bool]$usdjpy.audusdClassifiedAsFailed) "PMS_EMS_OMS_R008_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD classified failed."
Require-False ([bool]$usdjpy.lmaxLiveValidationGapsBlockReview) "PMS_EMS_OMS_R008_FAIL_LMAX_GAP_BLOCKS_REVIEW" "LMAX gaps block review."

Require-True ([bool]$lmax.lmaxReadonlyBaselineReferenceCreated) "PMS_EMS_OMS_R008_FAIL_LMAX_GAP_BLOCKS_REVIEW" "LMAX baseline missing."
Require-False ([bool]$lmax.lmaxUsedInThisPhase) "PMS_EMS_OMS_R008_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX used in R008."
Require-False ([bool]$lmax.lmaxCalledInThisPhase) "PMS_EMS_OMS_R008_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX called in R008."
Require-False ([bool]$lmax.lmaxLiveValidationGapsBlockReview) "PMS_EMS_OMS_R008_FAIL_LMAX_GAP_BLOCKS_REVIEW" "LMAX gaps block review."
Require-True ([bool]$lmax.baseline.GBPUSD.readOnlyMarketDataSucceeded) "PMS_EMS_OMS_R008_FAIL_LMAX_GAP_BLOCKS_REVIEW" "GBPUSD baseline missing."
Require-True ([int]$lmax.baseline.GBPUSD.sanitizedEntryCount -eq 2) "PMS_EMS_OMS_R008_FAIL_LMAX_GAP_BLOCKS_REVIEW" "GBPUSD sanitized count missing."
Require-True ([bool]$lmax.baseline.EURGBP.readOnlyMarketDataSucceeded) "PMS_EMS_OMS_R008_FAIL_LMAX_GAP_BLOCKS_REVIEW" "EURGBP baseline missing."
Require-True ([int]$lmax.baseline.EURGBP.sanitizedEntryCount -eq 2) "PMS_EMS_OMS_R008_FAIL_LMAX_GAP_BLOCKS_REVIEW" "EURGBP sanitized count missing."
Require-False ([bool]$lmax.baseline.AUDUSD.classifiedAsFailed) "PMS_EMS_OMS_R008_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD classified failed."
Require-False ([bool]$lmax.baseline.USDJPY.classifiedAsFailed) "PMS_EMS_OMS_R008_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY classified failed."
Require-True ([string]$lmax.baseline.USDJPY.securityId -eq "4004") "PMS_EMS_OMS_R008_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID missing."
Require-True ([string]$lmax.baseline.USDJPY.securityIdSource -eq "8") "PMS_EMS_OMS_R008_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource missing."

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
    "promotionExecutable",
    "promoteToPaperReadyEnablesLiveTrading",
    "promoteToPaperReadyEnablesOrderSubmission",
    "rebalanceIntentExecutable",
    "lmaxLiveValidationGapsBlockReview"
)) {
    Require-False ([bool]$noExternal.$property) "PMS_EMS_OMS_R008_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected: $property"
}

foreach ($item in $forbidden.forbiddenActions) {
    if ([bool]$item.detected) {
        if ([string]$item.action -match "scheduler|service|timer|background") {
            Fail-Gate "PMS_EMS_OMS_R008_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "order|trading|rebalance|executable|OMS|parent|child|broker order|promotion") {
            Fail-Gate "PMS_EMS_OMS_R008_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden action detected: $($item.action)"
        }

        Fail-Gate "PMS_EMS_OMS_R008_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $($item.action)"
    }
}

Require-True ([bool]$nextPhase.nextPhaseRecommendationCreated) "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Next phase recommendation missing."
Require-True ([string]$nextPhase.recommendedNextPhase -eq "PMS-EMS-OMS-R009") "PMS_EMS_OMS_R008_FAIL_OPERATOR_REVIEW_CONTRACT_MISSING" "Next phase is not R009."
Require-True ([bool]$nextPhase.mustRemainNoExternal) "PMS_EMS_OMS_R008_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase not no-external."
Require-True ([bool]$nextPhase.mustNotStartScheduler) "PMS_EMS_OMS_R008_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Next phase permits scheduler."
Require-True ([bool]$nextPhase.mustNotCallBroker) "PMS_EMS_OMS_R008_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase permits broker call."
Require-True ([bool]$nextPhase.mustNotRequestLiveMarketData) "PMS_EMS_OMS_R008_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase permits live market data."
Require-True ([bool]$nextPhase.mustNotCreateExecutableOrders) "PMS_EMS_OMS_R008_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Next phase permits executable orders."
Require-True ([bool]$nextPhase.mustNotSubmitOrders) "PMS_EMS_OMS_R008_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Next phase permits order submission."

$apiSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Api/appsettings.json") "PMS_EMS_OMS_R008_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$workerSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/appsettings.json") "PMS_EMS_OMS_R008_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-False ([bool]$apiSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R008_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API live trading enabled."
Require-False ([bool]$workerSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R008_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker live trading enabled."
Require-False ([bool]$apiSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R008_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API external connections enabled."
Require-False ([bool]$workerSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R008_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker external connections enabled."
Require-True ([bool]$apiSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R008_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API fake gateway not required."
Require-True ([bool]$workerSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R008_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker fake gateway not required."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.Enabled) "PMS_EMS_OMS_R008_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX read-only runtime enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowExternalConnections) "PMS_EMS_OMS_R008_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX external connections enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowOrderSubmission) "PMS_EMS_OMS_R008_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "LMAX order submission enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SchedulerEnabled) "PMS_EMS_OMS_R008_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "LMAX scheduler enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SubmitToShadowReplay) "PMS_EMS_OMS_R008_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Shadow replay enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.PersistRawFixMessages) "PMS_EMS_OMS_R008_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw FIX persistence enabled."
Require-False ([bool]$workerSettings.MarketDataBars.Enabled) "PMS_EMS_OMS_R008_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker market data bars enabled."

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-pms-ems-oms-r008-*" -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combined = [string]::Join("`n", $artifactText)
foreach ($pattern in @("\u0001", "35=", "MDReqID\s*[:=]", "SenderCompID\s*[:=]", "TargetCompID\s*[:=]", "BeginString\s*[:=]", "SocketHost\s*[:=]", "TlsHost\s*[:=]", "Password\s*[:=]", "ApiKey\s*[:=]", "Bearer\s+[A-Za-z0-9_\.-]+", "rawBid", "rawAsk", "rawMid")) {
    if ($combined -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R008_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Unsafe serialized content pattern found: $pattern"
    }
}

$requiredFiles = @(
    "src/QQ.Production.Intraday.Application/QubesIntradayCycleOperatorReview.cs",
    "tests/QQ.Production.Intraday.Tests.Unit/QubesIntradayCycleOperatorReviewTests.cs"
)
foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $file))) {
        Fail-Gate "PMS_EMS_OMS_R008_FAIL_BUILD_OR_TESTS" "Required implementation/test file missing: $file"
    }
}

$source = Get-Content -LiteralPath (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/QubesIntradayCycleOperatorReview.cs") -Raw
foreach ($pattern in @("TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "SendOrderAsync", "SubmitOrder", "FixSession")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R008_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "R008 source contains forbidden runtime pattern: $pattern"
    }
}
foreach ($pattern in @("AddHostedService", "IHostedService", "BackgroundService", "PeriodicTimer", "Task.Delay", "System.Threading.Timer")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R008_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "R008 source contains scheduler/service pattern: $pattern"
    }
}

$tests = Get-Content -LiteralPath (Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/QubesIntradayCycleOperatorReviewTests.cs") -Raw
foreach ($requiredTestName in @(
    "R007_archived_cycle_can_be_reviewed_by_operator_decision_fixture",
    "Approve_decision_is_recorded_without_external_action",
    "Hold_decision_is_recorded_for_missing_and_stale_marks",
    "Request_data_fix_decision_is_recorded_safely",
    "Promote_to_paper_ready_is_non_executable",
    "Promote_to_paper_ready_does_not_create_orders",
    "Promote_to_paper_ready_does_not_mutate_live_trading_state",
    "Completed_with_missing_marks_requires_acknowledgement_before_paper_promotion",
    "Drift_status_is_preserved_in_operator_review",
    "Rebalance_intents_remain_non_executable_after_operator_decision",
    "Duplicate_operator_decision_handling_is_idempotent",
    "Qubes_run_id_and_cycle_run_id_are_preserved",
    "Qubes_lineage_references_are_preserved",
    "Operator_review_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action",
    "Api_and_worker_live_gateway_remain_disabled",
    "Operator_review_source_introduces_no_scheduler_timer_polling_or_background_job",
    "Audusd_is_not_misclassified_as_failed",
    "Usdjpy_caveat_remains_preserved"
)) {
    if ($tests -notmatch [regex]::Escape($requiredTestName)) {
        Fail-Gate "PMS_EMS_OMS_R008_FAIL_BUILD_OR_TESTS" "Focused test missing: $requiredTestName"
    }
}

Require-True ([string]$evidence.build.status -eq "PASS") "PMS_EMS_OMS_R008_FAIL_BUILD_OR_TESTS" "Build evidence missing or not PASS."
Require-True ([int]$evidence.build.failed -eq 0) "PMS_EMS_OMS_R008_FAIL_BUILD_OR_TESTS" "Build evidence has failures."
Require-True ([string]$evidence.focusedTests.status -eq "PASS") "PMS_EMS_OMS_R008_FAIL_BUILD_OR_TESTS" "Focused test evidence missing or not PASS."
Require-True ([int]$evidence.focusedTests.failed -eq 0) "PMS_EMS_OMS_R008_FAIL_BUILD_OR_TESTS" "Focused tests have failures."
Require-True ([string]$evidence.unitTests.status -eq "PASS") "PMS_EMS_OMS_R008_FAIL_BUILD_OR_TESTS" "Unit test evidence missing or not PASS."
Require-True ([int]$evidence.unitTests.failed -eq 0) "PMS_EMS_OMS_R008_FAIL_BUILD_OR_TESTS" "Unit tests have failures."
Require-True ([string]$evidence.validator.status -eq "PASS") "PMS_EMS_OMS_R008_FAIL_BUILD_OR_TESTS" "Validator evidence missing or not PASS."
Require-True ([int]$evidence.validator.failed -eq 0) "PMS_EMS_OMS_R008_FAIL_BUILD_OR_TESTS" "Validator evidence has failures."
Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R008_FAIL_BUILD_OR_TESTS" "Build/test/validator evidence marker missing."

Write-Host "PMS_EMS_OMS_R008_PASS_OPERATOR_REVIEW_ACTIONS_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R008_PASS_CYCLE_PROMOTION_DECISION_GATE_READY_NO_EXTERNAL"
