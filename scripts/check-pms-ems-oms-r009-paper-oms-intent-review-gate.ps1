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
    "phase-pms-ems-oms-r009-summary.md" = "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING"
    "phase-pms-ems-oms-r009-paper-oms-intent-review-report.json" = "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING"
    "phase-pms-ems-oms-r009-paper-pretrade-risk-contract.json" = "PMS_EMS_OMS_R009_FAIL_PRETRADE_RISK_CONTRACT_MISSING"
    "phase-pms-ems-oms-r009-paper-pretrade-risk-results.json" = "PMS_EMS_OMS_R009_FAIL_PRETRADE_RISK_CONTRACT_MISSING"
    "phase-pms-ems-oms-r009-intent-review-lines.json" = "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING"
    "phase-pms-ems-oms-r009-blocked-intents-summary.json" = "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING"
    "phase-pms-ems-oms-r009-accepted-paper-review-summary.json" = "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING"
    "phase-pms-ems-oms-r009-non-executable-intent-audit.json" = "PMS_EMS_OMS_R009_FAIL_REBALANCE_INTENT_EXECUTABLE"
    "phase-pms-ems-oms-r009-no-order-created-audit.json" = "PMS_EMS_OMS_R009_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r009-qubes-lineage-preservation.json" = "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING"
    "phase-pms-ems-oms-r009-cycle-and-operator-decision-preservation.json" = "PMS_EMS_OMS_R009_FAIL_PROMOTION_GATE_WEAKENED"
    "phase-pms-ems-oms-r009-missing-stale-mark-risk-handling.json" = "PMS_EMS_OMS_R009_FAIL_MISSING_STALE_RISK_HANDLING_MISSING"
    "phase-pms-ems-oms-r009-drift-acknowledgement-preservation.json" = "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING"
    "phase-pms-ems-oms-r009-instrument-universe-handling.json" = "PMS_EMS_OMS_R009_FAIL_LMAX_GAP_BLOCKS_REVIEW"
    "phase-pms-ems-oms-r009-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R009_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r009-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R009_FAIL_LMAX_GAP_BLOCKS_REVIEW"
    "phase-pms-ems-oms-r009-no-external-audit.json" = "PMS_EMS_OMS_R009_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r009-forbidden-actions-audit.json" = "PMS_EMS_OMS_R009_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r009-next-phase-recommendation.json" = "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING"
    "phase-pms-ems-oms-r009-build-test-validator-evidence.json" = "PMS_EMS_OMS_R009_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    $path = Join-Path $artifactRoot $entry.Key
    if (-not (Test-Path -LiteralPath $path)) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$report = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r009-paper-oms-intent-review-report.json") "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING"
$contract = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r009-paper-pretrade-risk-contract.json") "PMS_EMS_OMS_R009_FAIL_PRETRADE_RISK_CONTRACT_MISSING"
$risk = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r009-paper-pretrade-risk-results.json") "PMS_EMS_OMS_R009_FAIL_PRETRADE_RISK_CONTRACT_MISSING"
$lines = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r009-intent-review-lines.json") "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING"
$blocked = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r009-blocked-intents-summary.json") "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING"
$accepted = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r009-accepted-paper-review-summary.json") "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING"
$intentAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r009-non-executable-intent-audit.json") "PMS_EMS_OMS_R009_FAIL_REBALANCE_INTENT_EXECUTABLE"
$orderAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r009-no-order-created-audit.json") "PMS_EMS_OMS_R009_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$lineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r009-qubes-lineage-preservation.json") "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING"
$cycleDecision = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r009-cycle-and-operator-decision-preservation.json") "PMS_EMS_OMS_R009_FAIL_PROMOTION_GATE_WEAKENED"
$marks = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r009-missing-stale-mark-risk-handling.json") "PMS_EMS_OMS_R009_FAIL_MISSING_STALE_RISK_HANDLING_MISSING"
$drift = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r009-drift-acknowledgement-preservation.json") "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r009-instrument-universe-handling.json") "PMS_EMS_OMS_R009_FAIL_LMAX_GAP_BLOCKS_REVIEW"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r009-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R009_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r009-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R009_FAIL_LMAX_GAP_BLOCKS_REVIEW"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r009-no-external-audit.json") "PMS_EMS_OMS_R009_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r009-forbidden-actions-audit.json") "PMS_EMS_OMS_R009_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$nextPhase = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r009-next-phase-recommendation.json") "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r009-build-test-validator-evidence.json") "PMS_EMS_OMS_R009_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$report.paperOmsIntentReviewReportCreated) "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING" "Paper OMS review report missing."
Require-True ([string]$report.cycleRunId -ne "") "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING" "CycleRunId missing."
Require-True ([string]$report.qubesRunId -ne "") "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING" "QubesRunId missing."
Require-True ([string]$report.operatorDecisionId -ne "") "PMS_EMS_OMS_R009_FAIL_PROMOTION_GATE_WEAKENED" "OperatorDecisionId missing."
Require-True ([string]$report.operatorDecisionType -eq "PromoteToPaperReady") "PMS_EMS_OMS_R009_FAIL_PROMOTION_GATE_WEAKENED" "PromoteToPaperReady gate missing."
Require-True ([string]$report.cycleReviewStatus -eq "PaperReadyNoExternal") "PMS_EMS_OMS_R009_FAIL_PROMOTION_GATE_WEAKENED" "Cycle is not paper-ready no-external."
Require-True ([bool]$report.promoteToPaperReadyGatePresent) "PMS_EMS_OMS_R009_FAIL_PROMOTION_GATE_WEAKENED" "Promotion gate not present."
Require-True ([bool]$report.cycleWithoutPromotionBlocked) "PMS_EMS_OMS_R009_FAIL_PROMOTION_GATE_WEAKENED" "Cycle without promotion can enter paper review."
Require-True ([bool]$report.missingStaleMarkAcknowledgementPreserved) "PMS_EMS_OMS_R009_FAIL_MISSING_STALE_RISK_HANDLING_MISSING" "Missing/stale acknowledgement missing."
Require-True ([bool]$report.driftAcknowledgementPreserved) "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING" "Drift acknowledgement missing."
Require-True ([bool]$report.rebalanceIntentsRemainNonExecutable) "PMS_EMS_OMS_R009_FAIL_REBALANCE_INTENT_EXECUTABLE" "Rebalance intents executable."
Require-True ([int]$report.intentCount -eq 13) "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING" "Intent count unexpected."
Require-True ([int]$report.acceptedIntentCount -ge 1) "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING" "No accepted paper review lines."
Require-True ([int]$report.blockedIntentCount -ge 1) "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING" "No blocked paper review lines."

Require-True ([bool]$contract.paperPreTradeRiskContractCreated) "PMS_EMS_OMS_R009_FAIL_PRETRADE_RISK_CONTRACT_MISSING" "Risk contract missing."
foreach ($check in @("max absolute delta weight per instrument", "max absolute target weight per instrument", "max gross notional change", "max per-instrument notional change", "missing current mark", "stale current mark", "unsupported instrument", "non-approved instrument", "missing operator promotion", "rebalance intent not explicitly non-executable")) {
    Require-True (($contract.riskChecks -contains $check)) "PMS_EMS_OMS_R009_FAIL_PRETRADE_RISK_CONTRACT_MISSING" "Risk check missing: $check"
}
foreach ($category in @("AcceptedForPaperReview", "BlockedMissingPromotion", "BlockedMissingMark", "BlockedStaleMark", "BlockedUnsupportedInstrument", "BlockedNonApprovedInstrument", "BlockedLimitExceeded", "BlockedIntentExecutable", "BlockedNoOMS", "InconclusiveSafe")) {
    Require-True (($contract.resultCategories -contains $category)) "PMS_EMS_OMS_R009_FAIL_PRETRADE_RISK_CONTRACT_MISSING" "Risk category missing: $category"
}
Require-True ([bool]$contract.promoteToPaperReadyRequired) "PMS_EMS_OMS_R009_FAIL_PROMOTION_GATE_WEAKENED" "Promotion requirement missing."
Require-True ([bool]$contract.paperOnlyNoExternal) "PMS_EMS_OMS_R009_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Contract is not no-external paper only."
Require-False ([bool]$contract.ordersCanBeCreated) "PMS_EMS_OMS_R009_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Contract permits orders."

Require-True ([bool]$risk.paperPreTradeRiskResultsCreated) "PMS_EMS_OMS_R009_FAIL_PRETRADE_RISK_CONTRACT_MISSING" "Risk results missing."
Require-True ([int]$risk.resultCounts.AcceptedForPaperReview -ge 1) "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING" "Accepted result missing."
Require-True ([int]$risk.resultCounts.BlockedMissingMark -ge 1) "PMS_EMS_OMS_R009_FAIL_MISSING_STALE_RISK_HANDLING_MISSING" "Missing mark result missing."
Require-True ([int]$risk.resultCounts.BlockedStaleMark -ge 1) "PMS_EMS_OMS_R009_FAIL_MISSING_STALE_RISK_HANDLING_MISSING" "Stale mark result missing."
Require-True ([bool]$risk.executableIntentArtificialCaseBlocked) "PMS_EMS_OMS_R009_FAIL_REBALANCE_INTENT_EXECUTABLE" "Executable intent artificial case not blocked."
Require-True ([bool]$risk.limitExceededArtificialCasesBlocked) "PMS_EMS_OMS_R009_FAIL_PRETRADE_RISK_CONTRACT_MISSING" "Limit exceeded cases not blocked."
Require-True ([bool]$risk.unsupportedInstrumentArtificialCaseBlocked) "PMS_EMS_OMS_R009_FAIL_PRETRADE_RISK_CONTRACT_MISSING" "Unsupported instrument not blocked."
Require-True ([bool]$risk.nonApprovedInstrumentArtificialCaseBlocked) "PMS_EMS_OMS_R009_FAIL_PRETRADE_RISK_CONTRACT_MISSING" "Non-approved instrument not blocked."

Require-True ([bool]$lines.intentReviewLinesCreated) "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING" "Intent review lines missing."
Require-True ([int]$lines.lineCount -eq 13) "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING" "Line count unexpected."
Require-True ([bool]$lines.safeSummaryOnly) "PMS_EMS_OMS_R009_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Intent lines are not safe summary only."
Require-False ([bool]$lines.rawMarketDataFixturePayloadsSerialized) "PMS_EMS_OMS_R009_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw market-data fixture payload serialized."
foreach ($line in $lines.lines) {
    Require-False ([bool]$line.isExecutable) "PMS_EMS_OMS_R009_FAIL_REBALANCE_INTENT_EXECUTABLE" "Intent line executable."
    Require-False ([bool]$line.createsOrder) "PMS_EMS_OMS_R009_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Intent line creates order."
}

Require-True ([bool]$blocked.blockedIntentsSummaryCreated) "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING" "Blocked summary missing."
Require-True ([int]$blocked.blockedIntentCount -ge 1) "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING" "Blocked intent count missing."
Require-True ([bool]$blocked.cycleWithoutPromotionBlocked) "PMS_EMS_OMS_R009_FAIL_PROMOTION_GATE_WEAKENED" "Cycle without promotion not blocked."
Require-False ([bool]$blocked.executableIntentAccepted) "PMS_EMS_OMS_R009_FAIL_REBALANCE_INTENT_EXECUTABLE" "Executable intent accepted."
Require-False ([bool]$blocked.blockedLinesCreateOrders) "PMS_EMS_OMS_R009_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Blocked lines create orders."
Require-False ([bool]$blocked.blockedLinesSubmitOrders) "PMS_EMS_OMS_R009_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "Blocked lines submit orders."

Require-True ([bool]$accepted.acceptedPaperReviewSummaryCreated) "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING" "Accepted summary missing."
Require-True ([int]$accepted.acceptedIntentCount -ge 1) "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING" "Accepted intent count missing."
Require-True ([bool]$accepted.acceptedForPaperReviewOnly) "PMS_EMS_OMS_R009_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Accepted lines not paper review only."
Require-False ([bool]$accepted.acceptedLinesExecutable) "PMS_EMS_OMS_R009_FAIL_REBALANCE_INTENT_EXECUTABLE" "Accepted lines executable."
Require-False ([bool]$accepted.acceptedLinesCreateOrders) "PMS_EMS_OMS_R009_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Accepted lines create orders."
Require-False ([bool]$accepted.acceptedLinesSubmitOrders) "PMS_EMS_OMS_R009_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "Accepted lines submit orders."
Require-False ([bool]$accepted.brokerGatewayCalledForAcceptedLines) "PMS_EMS_OMS_R009_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker called for accepted lines."

Require-True ([bool]$intentAudit.nonExecutableIntentAuditCreated) "PMS_EMS_OMS_R009_FAIL_REBALANCE_INTENT_EXECUTABLE" "Non-executable audit missing."
Require-True ([bool]$intentAudit.rebalanceIntentsRemainNonExecutable) "PMS_EMS_OMS_R009_FAIL_REBALANCE_INTENT_EXECUTABLE" "Rebalance intents executable."
Require-True ([bool]$intentAudit.allIntentLinesHaveTheoreticalOnlyStatus) "PMS_EMS_OMS_R009_FAIL_REBALANCE_INTENT_EXECUTABLE" "TheoreticalOnly missing."
Require-True ([bool]$intentAudit.allIntentLinesHaveNotExecutableStatus) "PMS_EMS_OMS_R009_FAIL_REBALANCE_INTENT_EXECUTABLE" "NotExecutable missing."
Require-True ([bool]$intentAudit.allIntentLinesHaveBlockedNoOmsStatus) "PMS_EMS_OMS_R009_FAIL_REBALANCE_INTENT_EXECUTABLE" "BlockedNoOMS missing."
Require-True ([bool]$intentAudit.executableIntentArtificialCaseBlocked) "PMS_EMS_OMS_R009_FAIL_REBALANCE_INTENT_EXECUTABLE" "Executable artificial case not blocked."
Require-False ([bool]$intentAudit.rebalanceIntentExecutable) "PMS_EMS_OMS_R009_FAIL_REBALANCE_INTENT_EXECUTABLE" "Rebalance intent executable."
Require-False ([bool]$intentAudit.paperReviewCreatesExecutableOrder) "PMS_EMS_OMS_R009_FAIL_EXECUTABLE_ORDER_CREATED" "Paper review creates executable order."
Require-False ([bool]$intentAudit.paperReviewSubmitsOrders) "PMS_EMS_OMS_R009_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "Paper review submits orders."

foreach ($property in @("omsOrderCreated", "parentOrderCreated", "childOrderCreated", "brokerOrderCreated", "executableOrderCreated", "orderSubmissionPathIntroduced", "ordersSubmitted", "liveTradingStateMutated", "brokerGatewayCalled")) {
    Require-False ([bool]$orderAudit.$property) "PMS_EMS_OMS_R009_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No-order audit detected: $property"
}
Require-True ([bool]$orderAudit.noOrderCreatedAuditCreated) "PMS_EMS_OMS_R009_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No-order audit missing."

Require-True ([bool]$lineage.qubesLineagePreservationCreated) "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING" "Qubes lineage missing."
Require-True ([string]$lineage.sourceSystem -eq "ModelWeightSourceSystem.Qubes") "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING" "Qubes source missing."
Require-True ([int]$lineage.cadenceMinutes -eq 15) "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING" "Cadence missing."
Require-True ([bool]$lineage.qubesAuditBatchPreserved) "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING" "Qubes audit batch not preserved."
Require-True ([bool]$lineage.modelWeightBatchLinkagePreserved) "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING" "ModelWeightBatch not preserved."
Require-True ([bool]$lineage.modelRunLinkagePreserved) "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING" "ModelRun not preserved."
Require-True ([bool]$lineage.targetWeightLinkagePreserved) "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING" "TargetWeight not preserved."

Require-True ([bool]$cycleDecision.cycleAndOperatorDecisionPreservationCreated) "PMS_EMS_OMS_R009_FAIL_PROMOTION_GATE_WEAKENED" "Cycle/operator decision preservation missing."
Require-True ([string]$cycleDecision.operatorDecisionType -eq "PromoteToPaperReady") "PMS_EMS_OMS_R009_FAIL_PROMOTION_GATE_WEAKENED" "Operator promotion missing."
Require-True ([string]$cycleDecision.resultingCycleReviewStatus -eq "PaperReadyNoExternal") "PMS_EMS_OMS_R009_FAIL_PROMOTION_GATE_WEAKENED" "Operator decision not paper no-external."
Require-True ([bool]$cycleDecision.missingStaleMarksAcknowledged) "PMS_EMS_OMS_R009_FAIL_MISSING_STALE_RISK_HANDLING_MISSING" "Missing/stale acknowledgement missing."
Require-True ([bool]$cycleDecision.driftAcknowledged) "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING" "Drift acknowledgement missing."
Require-True ([bool]$cycleDecision.promotionGatePresent) "PMS_EMS_OMS_R009_FAIL_PROMOTION_GATE_WEAKENED" "Promotion gate missing."
Require-False ([bool]$cycleDecision.cycleWithoutPromotionCanEnterPaperReview) "PMS_EMS_OMS_R009_FAIL_PROMOTION_GATE_WEAKENED" "Cycle without promotion can enter paper review."

Require-True ([bool]$marks.missingStaleMarkRiskHandlingCreated) "PMS_EMS_OMS_R009_FAIL_MISSING_STALE_RISK_HANDLING_MISSING" "Missing/stale risk handling missing."
Require-True ([bool]$marks.missingMarkStatusPreserved) "PMS_EMS_OMS_R009_FAIL_MISSING_STALE_RISK_HANDLING_MISSING" "MissingMark not preserved."
Require-True ([bool]$marks.staleMarkStatusPreserved) "PMS_EMS_OMS_R009_FAIL_MISSING_STALE_RISK_HANDLING_MISSING" "StaleMark not preserved."
Require-True ([bool]$marks.missingStaleMarkAcknowledgementPreserved) "PMS_EMS_OMS_R009_FAIL_MISSING_STALE_RISK_HANDLING_MISSING" "Acknowledgement not preserved."
Require-False ([bool]$marks.missingOrStaleMarksHidden) "PMS_EMS_OMS_R009_FAIL_MISSING_STALE_RISK_HANDLING_MISSING" "Missing/stale marks hidden."
Require-False ([bool]$marks.fabricatedMarksForPaperReview) "PMS_EMS_OMS_R009_FAIL_MISSING_STALE_RISK_HANDLING_MISSING" "Marks fabricated."
Require-False ([bool]$marks.rawMarketDataFixturePayloadsSerialized) "PMS_EMS_OMS_R009_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw fixture payload serialized."

Require-True ([bool]$drift.driftAcknowledgementPreservationCreated) "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING" "Drift preservation missing."
Require-True ([string]$drift.theoreticalVsRealStatus -eq "Drift") "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING" "Drift status missing."
Require-True ([bool]$drift.driftAcknowledgedByOperatorDecision) "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING" "Drift acknowledgement missing."
Require-True ([bool]$drift.driftAcknowledgementPreservedInPaperReview) "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING" "Drift not preserved in paper review."
Require-True ([bool]$drift.driftAllowsPaperReviewOnly) "PMS_EMS_OMS_R009_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Drift allows non-paper review."
Require-False ([bool]$drift.liveTradingApprovalCreated) "PMS_EMS_OMS_R009_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Live trading approval created."

Require-True ([bool]$universe.instrumentUniverseHandlingCreated) "PMS_EMS_OMS_R009_FAIL_LMAX_GAP_BLOCKS_REVIEW" "Universe handling missing."
Require-True ([bool]$universe.unsupportedInstrumentBlocked) "PMS_EMS_OMS_R009_FAIL_PRETRADE_RISK_CONTRACT_MISSING" "Unsupported instrument not blocked."
Require-True ([bool]$universe.nonApprovedInstrumentBlocked) "PMS_EMS_OMS_R009_FAIL_PRETRADE_RISK_CONTRACT_MISSING" "Non-approved instrument not blocked."
Require-False ([bool]$universe.lmaxReadOnlyScopeUsedAsPaperReviewGate) "PMS_EMS_OMS_R009_FAIL_LMAX_GAP_BLOCKS_REVIEW" "LMAX scope gates paper review."
Require-False ([bool]$universe.lmaxLiveValidationGapsBlockPaperReview) "PMS_EMS_OMS_R009_FAIL_LMAX_GAP_BLOCKS_REVIEW" "LMAX gaps block paper review."
Require-False ([bool]$universe.audusdLiveValidationGapBlocksPaperReview) "PMS_EMS_OMS_R009_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD gap blocks paper review."
Require-False ([bool]$universe.usdjpyLiveValidationGapBlocksPaperReview) "PMS_EMS_OMS_R009_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY gap blocks paper review."

Require-True ([bool]$usdjpy.usdJpyCaveatPreservationCreated) "PMS_EMS_OMS_R009_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
Require-True ([string]$usdjpy.usdJpySecurityId -eq "4004") "PMS_EMS_OMS_R009_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID missing."
Require-True ([string]$usdjpy.usdJpySecurityIdSource -eq "8") "PMS_EMS_OMS_R009_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource missing."
Require-True ([bool]$usdjpy.usdJpyNotProven) "PMS_EMS_OMS_R009_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY not-proven missing."
Require-False ([bool]$usdjpy.usdJpyClassifiedAsFailed) "PMS_EMS_OMS_R009_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY classified failed."
Require-True ([bool]$usdjpy.audusdTlsBoundaryInconclusive) "PMS_EMS_OMS_R009_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD TLS status missing."
Require-False ([bool]$usdjpy.audusdClassifiedAsFailed) "PMS_EMS_OMS_R009_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD classified failed."

Require-True ([bool]$lmax.lmaxReadonlyBaselineReferenceCreated) "PMS_EMS_OMS_R009_FAIL_LMAX_GAP_BLOCKS_REVIEW" "LMAX baseline missing."
Require-False ([bool]$lmax.lmaxUsedInThisPhase) "PMS_EMS_OMS_R009_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX used in R009."
Require-False ([bool]$lmax.lmaxCalledInThisPhase) "PMS_EMS_OMS_R009_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX called in R009."
Require-False ([bool]$lmax.lmaxLiveValidationGapsBlockPaperReview) "PMS_EMS_OMS_R009_FAIL_LMAX_GAP_BLOCKS_REVIEW" "LMAX gaps block paper review."
Require-True ([bool]$lmax.baseline.GBPUSD.readOnlyMarketDataSucceeded) "PMS_EMS_OMS_R009_FAIL_LMAX_GAP_BLOCKS_REVIEW" "GBPUSD baseline missing."
Require-True ([int]$lmax.baseline.GBPUSD.sanitizedEntryCount -eq 2) "PMS_EMS_OMS_R009_FAIL_LMAX_GAP_BLOCKS_REVIEW" "GBPUSD sanitized count missing."
Require-True ([bool]$lmax.baseline.EURGBP.readOnlyMarketDataSucceeded) "PMS_EMS_OMS_R009_FAIL_LMAX_GAP_BLOCKS_REVIEW" "EURGBP baseline missing."
Require-True ([int]$lmax.baseline.EURGBP.sanitizedEntryCount -eq 2) "PMS_EMS_OMS_R009_FAIL_LMAX_GAP_BLOCKS_REVIEW" "EURGBP sanitized count missing."
Require-False ([bool]$lmax.baseline.AUDUSD.classifiedAsFailed) "PMS_EMS_OMS_R009_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD classified failed."
Require-False ([bool]$lmax.baseline.USDJPY.classifiedAsFailed) "PMS_EMS_OMS_R009_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY classified failed."
Require-True ([string]$lmax.baseline.USDJPY.securityId -eq "4004") "PMS_EMS_OMS_R009_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID missing."
Require-True ([string]$lmax.baseline.USDJPY.securityIdSource -eq "8") "PMS_EMS_OMS_R009_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource missing."

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
    "cycleWithoutPromotionCanEnterPaperReview",
    "rebalanceIntentExecutable",
    "executableIntentAccepted",
    "lmaxLiveValidationGapsBlockPaperReview"
)) {
    Require-False ([bool]$noExternal.$property) "PMS_EMS_OMS_R009_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected: $property"
}
Require-True ([bool]$noExternal.promotionGatePresent) "PMS_EMS_OMS_R009_FAIL_PROMOTION_GATE_WEAKENED" "Promotion gate missing in no-external audit."

foreach ($item in $forbidden.forbiddenActions) {
    if ([bool]$item.detected) {
        if ([string]$item.action -match "scheduler|service|timer|background") {
            Fail-Gate "PMS_EMS_OMS_R009_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "order|trading|rebalance|executable|OMS|parent|child|broker order") {
            Fail-Gate "PMS_EMS_OMS_R009_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden action detected: $($item.action)"
        }

        Fail-Gate "PMS_EMS_OMS_R009_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $($item.action)"
    }
}

Require-True ([bool]$nextPhase.nextPhaseRecommendationCreated) "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING" "Next phase recommendation missing."
Require-True ([string]$nextPhase.recommendedNextPhase -eq "PMS-EMS-OMS-R010") "PMS_EMS_OMS_R009_FAIL_PAPER_REVIEW_REPORT_MISSING" "Next phase is not R010."
Require-True ([bool]$nextPhase.mustRemainNoExternal) "PMS_EMS_OMS_R009_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase not no-external."
Require-True ([bool]$nextPhase.mustNotCreateExecutableOrders) "PMS_EMS_OMS_R009_FAIL_EXECUTABLE_ORDER_CREATED" "Next phase permits executable orders."
Require-True ([bool]$nextPhase.mustNotCreateOmsOrders) "PMS_EMS_OMS_R009_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Next phase permits OMS orders."
Require-True ([bool]$nextPhase.mustNotCreateBrokerOrders) "PMS_EMS_OMS_R009_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Next phase permits broker orders."
Require-True ([bool]$nextPhase.mustNotSubmitOrders) "PMS_EMS_OMS_R009_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "Next phase permits order submission."

$apiSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Api/appsettings.json") "PMS_EMS_OMS_R009_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$workerSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/appsettings.json") "PMS_EMS_OMS_R009_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-False ([bool]$apiSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R009_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API live trading enabled."
Require-False ([bool]$workerSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R009_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker live trading enabled."
Require-False ([bool]$apiSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R009_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API external connections enabled."
Require-False ([bool]$workerSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R009_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker external connections enabled."
Require-True ([bool]$apiSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R009_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API fake gateway not required."
Require-True ([bool]$workerSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R009_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker fake gateway not required."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.Enabled) "PMS_EMS_OMS_R009_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX read-only runtime enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowExternalConnections) "PMS_EMS_OMS_R009_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX external connections enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowOrderSubmission) "PMS_EMS_OMS_R009_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "LMAX order submission enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SchedulerEnabled) "PMS_EMS_OMS_R009_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "LMAX scheduler enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SubmitToShadowReplay) "PMS_EMS_OMS_R009_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Shadow replay enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.PersistRawFixMessages) "PMS_EMS_OMS_R009_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw FIX persistence enabled."
Require-False ([bool]$workerSettings.MarketDataBars.Enabled) "PMS_EMS_OMS_R009_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker market data bars enabled."

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-pms-ems-oms-r009-*" -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combined = [string]::Join("`n", $artifactText)
foreach ($pattern in @("\u0001", "35=", "MDReqID\s*[:=]", "SenderCompID\s*[:=]", "TargetCompID\s*[:=]", "BeginString\s*[:=]", "SocketHost\s*[:=]", "TlsHost\s*[:=]", "Password\s*[:=]", "ApiKey\s*[:=]", "Bearer\s+[A-Za-z0-9_\.-]+", "rawBid", "rawAsk", "rawMid")) {
    if ($combined -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R009_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Unsafe serialized content pattern found: $pattern"
    }
}

$requiredFiles = @(
    "src/QQ.Production.Intraday.Application/QubesPaperOmsIntentReview.cs",
    "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperOmsIntentReviewTests.cs"
)
foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $file))) {
        Fail-Gate "PMS_EMS_OMS_R009_FAIL_BUILD_OR_TESTS" "Required implementation/test file missing: $file"
    }
}

$source = Get-Content -LiteralPath (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/QubesPaperOmsIntentReview.cs") -Raw
foreach ($pattern in @("TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "SendOrderAsync", "SubmitOrder", "FixSession")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R009_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "R009 source contains forbidden runtime pattern: $pattern"
    }
}
foreach ($pattern in @("AddHostedService", "IHostedService", "BackgroundService", "PeriodicTimer", "Task.Delay", "System.Threading.Timer")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R009_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "R009 source contains scheduler/service pattern: $pattern"
    }
}

$tests = Get-Content -LiteralPath (Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperOmsIntentReviewTests.cs") -Raw
foreach ($requiredTestName in @(
    "Promote_to_paper_ready_cycle_can_enter_paper_oms_review",
    "Cycle_without_promote_to_paper_ready_is_blocked",
    "Rebalance_intents_remain_non_executable",
    "Executable_intent_is_blocked",
    "Missing_mark_blocks_affected_intent_line",
    "Stale_mark_blocks_affected_intent_line",
    "Max_delta_weight_limit_is_enforced",
    "Max_target_weight_limit_is_enforced",
    "Max_notional_change_limit_is_enforced",
    "Unsupported_instrument_is_blocked",
    "Non_approved_instrument_is_blocked",
    "Drift_acknowledgement_is_preserved",
    "Missing_stale_mark_acknowledgement_is_preserved",
    "Paper_review_report_preserves_cycle_run_id_and_qubes_run_id",
    "No_oms_broker_parent_or_child_order_is_created",
    "No_order_submission_path_is_introduced",
    "Paper_review_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action",
    "Api_and_worker_live_gateway_remain_disabled",
    "Paper_review_source_introduces_no_scheduler_timer_polling_or_background_job",
    "Audusd_is_not_misclassified_as_failed",
    "Usdjpy_caveat_remains_preserved"
)) {
    if ($tests -notmatch [regex]::Escape($requiredTestName)) {
        Fail-Gate "PMS_EMS_OMS_R009_FAIL_BUILD_OR_TESTS" "Focused test missing: $requiredTestName"
    }
}

Require-True ([string]$evidence.build.status -eq "PASS") "PMS_EMS_OMS_R009_FAIL_BUILD_OR_TESTS" "Build evidence missing or not PASS."
Require-True ([int]$evidence.build.failed -eq 0) "PMS_EMS_OMS_R009_FAIL_BUILD_OR_TESTS" "Build evidence has failures."
Require-True ([string]$evidence.focusedTests.status -eq "PASS") "PMS_EMS_OMS_R009_FAIL_BUILD_OR_TESTS" "Focused test evidence missing or not PASS."
Require-True ([int]$evidence.focusedTests.failed -eq 0) "PMS_EMS_OMS_R009_FAIL_BUILD_OR_TESTS" "Focused tests have failures."
Require-True ([string]$evidence.unitTests.status -eq "PASS") "PMS_EMS_OMS_R009_FAIL_BUILD_OR_TESTS" "Unit test evidence missing or not PASS."
Require-True ([int]$evidence.unitTests.failed -eq 0) "PMS_EMS_OMS_R009_FAIL_BUILD_OR_TESTS" "Unit tests have failures."
Require-True ([string]$evidence.validator.status -eq "PASS") "PMS_EMS_OMS_R009_FAIL_BUILD_OR_TESTS" "Validator evidence missing or not PASS."
Require-True ([int]$evidence.validator.failed -eq 0) "PMS_EMS_OMS_R009_FAIL_BUILD_OR_TESTS" "Validator evidence has failures."
Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R009_FAIL_BUILD_OR_TESTS" "Build/test/validator evidence marker missing."

Write-Host "PMS_EMS_OMS_R009_PASS_PAPER_OMS_INTENT_REVIEW_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R009_PASS_PRETRADE_RISK_GATE_READY_NO_EXTERNAL"
