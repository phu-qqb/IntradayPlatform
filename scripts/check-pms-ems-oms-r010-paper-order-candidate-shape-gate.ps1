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
    "phase-pms-ems-oms-r010-summary.md" = "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING"
    "phase-pms-ems-oms-r010-paper-order-candidate-contract.json" = "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING"
    "phase-pms-ems-oms-r010-paper-order-candidates.json" = "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING"
    "phase-pms-ems-oms-r010-blocked-lines-handling.json" = "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING"
    "phase-pms-ems-oms-r010-non-executable-candidate-audit.json" = "PMS_EMS_OMS_R010_FAIL_CANDIDATE_EXECUTABLE"
    "phase-pms-ems-oms-r010-no-order-created-audit.json" = "PMS_EMS_OMS_R010_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r010-risk-lineage-preservation.json" = "PMS_EMS_OMS_R010_FAIL_RISK_LINEAGE_MISSING"
    "phase-pms-ems-oms-r010-qubes-lineage-preservation.json" = "PMS_EMS_OMS_R010_FAIL_QUBES_LINEAGE_WEAKENED"
    "phase-pms-ems-oms-r010-operator-decision-lineage-preservation.json" = "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING"
    "phase-pms-ems-oms-r010-rebalance-intent-lineage-preservation.json" = "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING"
    "phase-pms-ems-oms-r010-missing-stale-mark-preservation.json" = "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING"
    "phase-pms-ems-oms-r010-drift-acknowledgement-preservation.json" = "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING"
    "phase-pms-ems-oms-r010-instrument-universe-handling.json" = "PMS_EMS_OMS_R010_FAIL_LMAX_GAP_BLOCKS_CANDIDATE_SHAPE"
    "phase-pms-ems-oms-r010-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R010_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r010-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R010_FAIL_LMAX_GAP_BLOCKS_CANDIDATE_SHAPE"
    "phase-pms-ems-oms-r010-no-external-audit.json" = "PMS_EMS_OMS_R010_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r010-forbidden-actions-audit.json" = "PMS_EMS_OMS_R010_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r010-next-phase-recommendation.json" = "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING"
    "phase-pms-ems-oms-r010-build-test-validator-evidence.json" = "PMS_EMS_OMS_R010_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    $path = Join-Path $artifactRoot $entry.Key
    if (-not (Test-Path -LiteralPath $path)) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$contract = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r010-paper-order-candidate-contract.json") "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING"
$candidates = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r010-paper-order-candidates.json") "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING"
$blocked = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r010-blocked-lines-handling.json") "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING"
$nonExec = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r010-non-executable-candidate-audit.json") "PMS_EMS_OMS_R010_FAIL_CANDIDATE_EXECUTABLE"
$orderAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r010-no-order-created-audit.json") "PMS_EMS_OMS_R010_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$risk = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r010-risk-lineage-preservation.json") "PMS_EMS_OMS_R010_FAIL_RISK_LINEAGE_MISSING"
$lineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r010-qubes-lineage-preservation.json") "PMS_EMS_OMS_R010_FAIL_QUBES_LINEAGE_WEAKENED"
$decision = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r010-operator-decision-lineage-preservation.json") "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING"
$rebalance = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r010-rebalance-intent-lineage-preservation.json") "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING"
$marks = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r010-missing-stale-mark-preservation.json") "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING"
$drift = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r010-drift-acknowledgement-preservation.json") "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r010-instrument-universe-handling.json") "PMS_EMS_OMS_R010_FAIL_LMAX_GAP_BLOCKS_CANDIDATE_SHAPE"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r010-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R010_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r010-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R010_FAIL_LMAX_GAP_BLOCKS_CANDIDATE_SHAPE"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r010-no-external-audit.json") "PMS_EMS_OMS_R010_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r010-forbidden-actions-audit.json") "PMS_EMS_OMS_R010_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$nextPhase = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r010-next-phase-recommendation.json") "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r010-build-test-validator-evidence.json") "PMS_EMS_OMS_R010_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$contract.paperOrderCandidateContractCreated) "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING" "Candidate contract missing."
foreach ($field in @("PaperOrderCandidateId", "CycleRunId", "QubesRunId", "OperatorDecisionId", "SourceRebalanceIntentId", "InstrumentId", "NormalizedSymbol", "Side", "TargetWeight", "CurrentWeight", "DeltaWeight", "TargetNotional", "CurrentNotional", "DeltaNotional", "QuantityShapeCategory", "OrderTypeShapeCategory", "TimeInForceShapeCategory", "CandidateStatus", "NonExecutableReason", "RiskReviewReference")) {
    Require-True (($contract.candidateFields -contains $field)) "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING" "Candidate field missing: $field"
}
Require-True ([bool]$contract.paperOnly) "PMS_EMS_OMS_R010_FAIL_CANDIDATE_EXECUTABLE" "Candidates not paper-only."
Require-True ([bool]$contract.nonExecutable) "PMS_EMS_OMS_R010_FAIL_CANDIDATE_EXECUTABLE" "Candidates not non-executable."
Require-True ([bool]$contract.notAnOrder) "PMS_EMS_OMS_R010_FAIL_CANDIDATE_IS_OMS_OR_BROKER_ORDER" "Candidates are orders."
Require-True ([bool]$contract.notSubmitted) "PMS_EMS_OMS_R010_FAIL_CANDIDATE_SUBMITTED_OR_ROUTED" "Candidates can be submitted."
Require-True ([bool]$contract.noBrokerRoute) "PMS_EMS_OMS_R010_FAIL_CANDIDATE_SUBMITTED_OR_ROUTED" "Candidates have broker route."
Require-False ([bool]$contract.omsOrderEntityCreated) "PMS_EMS_OMS_R010_FAIL_CANDIDATE_IS_OMS_OR_BROKER_ORDER" "OMS order entity created."
Require-False ([bool]$contract.brokerOrderEntityCreated) "PMS_EMS_OMS_R010_FAIL_CANDIDATE_IS_OMS_OR_BROKER_ORDER" "Broker order entity created."

Require-True ([bool]$candidates.paperOrderCandidatesCreated) "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING" "Candidate artifact missing."
Require-True ([int]$candidates.candidateCount -eq 3) "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING" "Unexpected candidate count."
Require-True ([int]$candidates.acceptedPaperReviewLineCount -eq 3) "PMS_EMS_OMS_R010_FAIL_RISK_LINEAGE_MISSING" "Accepted line count missing."
Require-True ([int]$candidates.blockedPaperReviewLineCount -eq 10) "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING" "Blocked line count missing."
Require-False ([bool]$candidates.blockedLinesBecomeReadyCandidates) "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING" "Blocked lines became candidates."
foreach ($candidate in $candidates.candidates) {
    Require-True ([bool]$candidate.paperOnly) "PMS_EMS_OMS_R010_FAIL_CANDIDATE_EXECUTABLE" "Candidate not paper-only."
    Require-True ([bool]$candidate.nonExecutable) "PMS_EMS_OMS_R010_FAIL_CANDIDATE_EXECUTABLE" "Candidate executable."
    Require-True ([bool]$candidate.notAnOrder) "PMS_EMS_OMS_R010_FAIL_CANDIDATE_IS_OMS_OR_BROKER_ORDER" "Candidate is an order."
    Require-True ([bool]$candidate.notSubmitted) "PMS_EMS_OMS_R010_FAIL_CANDIDATE_SUBMITTED_OR_ROUTED" "Candidate submitted."
    Require-True ([bool]$candidate.noBrokerRoute) "PMS_EMS_OMS_R010_FAIL_CANDIDATE_SUBMITTED_OR_ROUTED" "Candidate has broker route."
    Require-True ([string]$candidate.quantityShapeCategory -eq "QuantityRequiresMarkOrLotSizing") "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING" "Quantity shape is unsafe."
    Require-True ([string]$candidate.orderTypeShapeCategory -eq "NotExecutable") "PMS_EMS_OMS_R010_FAIL_CANDIDATE_EXECUTABLE" "Order type shape executable."
    Require-True ([string]$candidate.timeInForceShapeCategory -eq "NotExecutable") "PMS_EMS_OMS_R010_FAIL_CANDIDATE_EXECUTABLE" "Time-in-force shape executable."
}

Require-True ([bool]$blocked.blockedLinesHandlingCreated) "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING" "Blocked handling missing."
Require-True ([int]$blocked.blockedPaperReviewLineCount -eq 10) "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING" "Blocked count unexpected."
Require-True ([bool]$blocked.blockedLinesCarriedSeparately) "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING" "Blocked lines not carried separately."
Require-True ([bool]$blocked.blockedLinesIgnoredForReadyCandidates) "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING" "Blocked lines not ignored."
Require-False ([bool]$blocked.blockedR009LinesBecomeReadyCandidates) "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING" "Blocked R009 lines became ready candidates."
Require-False ([bool]$blocked.blockedLinesCreateOrders) "PMS_EMS_OMS_R010_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Blocked lines create orders."
Require-False ([bool]$blocked.blockedLinesSubmitOrders) "PMS_EMS_OMS_R010_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "Blocked lines submit orders."

Require-True ([bool]$nonExec.nonExecutableCandidateAuditCreated) "PMS_EMS_OMS_R010_FAIL_CANDIDATE_EXECUTABLE" "Non-executable audit missing."
foreach ($property in @("allCandidatesPaperOnly", "allCandidatesNonExecutable", "allCandidatesNotAnOrder", "allCandidatesNotSubmitted", "allCandidatesNoBrokerRoute", "allCandidatesOrderTypeNotExecutable", "allCandidatesTimeInForceNotExecutable")) {
    Require-True ([bool]$nonExec.$property) "PMS_EMS_OMS_R010_FAIL_CANDIDATE_EXECUTABLE" "Candidate audit missing: $property"
}
foreach ($property in @("candidateExecutable", "candidateSubmitted", "candidateHasBrokerRoute", "candidateRepresentedAsOmsOrder", "candidateRepresentedAsBrokerOrder")) {
    Require-False ([bool]$nonExec.$property) "PMS_EMS_OMS_R010_FAIL_CANDIDATE_EXECUTABLE" "Candidate audit detected: $property"
}

Require-True ([bool]$orderAudit.noOrderCreatedAuditCreated) "PMS_EMS_OMS_R010_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No-order audit missing."
foreach ($property in @("omsOrderCreated", "parentOrderCreated", "childOrderCreated", "brokerOrderCreated", "executableOrderCreated", "fillCreated", "executionReportCreated", "orderSubmissionPathIntroduced", "ordersSubmitted", "liveTradingStateMutated", "brokerGatewayCalled")) {
    Require-False ([bool]$orderAudit.$property) "PMS_EMS_OMS_R010_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No-order audit detected: $property"
}

Require-True ([bool]$risk.riskLineagePreservationCreated) "PMS_EMS_OMS_R010_FAIL_RISK_LINEAGE_MISSING" "Risk lineage missing."
Require-True ([bool]$risk.paperRiskReviewReportReferenced) "PMS_EMS_OMS_R010_FAIL_RISK_LINEAGE_MISSING" "Risk report not referenced."
Require-True ([bool]$risk.acceptedRiskResultReferenced) "PMS_EMS_OMS_R010_FAIL_RISK_LINEAGE_MISSING" "Accepted risk result not referenced."
Require-True ([bool]$risk.blockedRiskResultsCarriedSeparately) "PMS_EMS_OMS_R010_FAIL_RISK_LINEAGE_MISSING" "Blocked risk results not preserved."
Require-True ([bool]$risk.riskReviewReferencePresentOnEveryCandidate) "PMS_EMS_OMS_R010_FAIL_RISK_LINEAGE_MISSING" "Risk reference missing on candidates."

Require-True ([bool]$lineage.qubesLineagePreservationCreated) "PMS_EMS_OMS_R010_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage missing."
Require-True ([string]$lineage.sourceSystem -eq "ModelWeightSourceSystem.Qubes") "PMS_EMS_OMS_R010_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes source missing."
Require-True ([int]$lineage.cadenceMinutes -eq 15) "PMS_EMS_OMS_R010_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes cadence missing."
Require-True ([bool]$lineage.qubesAuditBatchPreserved) "PMS_EMS_OMS_R010_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes audit batch not preserved."
Require-True ([bool]$lineage.modelWeightBatchLinkagePreserved) "PMS_EMS_OMS_R010_FAIL_QUBES_LINEAGE_WEAKENED" "ModelWeightBatch not preserved."
Require-True ([bool]$lineage.modelRunLinkagePreserved) "PMS_EMS_OMS_R010_FAIL_QUBES_LINEAGE_WEAKENED" "ModelRun not preserved."
Require-True ([bool]$lineage.targetWeightLinkagePreserved) "PMS_EMS_OMS_R010_FAIL_QUBES_LINEAGE_WEAKENED" "TargetWeight not preserved."

Require-True ([bool]$decision.operatorDecisionLineagePreservationCreated) "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING" "Operator decision lineage missing."
Require-True ([string]$decision.operatorDecisionType -eq "PromoteToPaperReady") "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING" "Operator decision is not promotion."
Require-True ([string]$decision.resultingCycleReviewStatus -eq "PaperReadyNoExternal") "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING" "Operator decision not paper no-external."
Require-True ([bool]$decision.operatorDecisionReferencedOnEveryCandidate) "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING" "Operator decision missing on candidates."

Require-True ([bool]$rebalance.rebalanceIntentLineagePreservationCreated) "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING" "Rebalance lineage missing."
Require-True ([bool]$rebalance.sourceRebalanceIntentReferencedOnEveryCandidate) "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING" "Source rebalance intent missing."
Require-True ([bool]$rebalance.rebalanceIntentsRemainNonExecutable) "PMS_EMS_OMS_R010_FAIL_CANDIDATE_EXECUTABLE" "Source rebalance intents executable."
Require-True ([bool]$rebalance.allSourceIntentsTheoreticalOnly) "PMS_EMS_OMS_R010_FAIL_CANDIDATE_EXECUTABLE" "TheoreticalOnly missing."
Require-True ([bool]$rebalance.allSourceIntentsNotExecutable) "PMS_EMS_OMS_R010_FAIL_CANDIDATE_EXECUTABLE" "NotExecutable missing."
Require-True ([bool]$rebalance.allSourceIntentsBlockedNoOms) "PMS_EMS_OMS_R010_FAIL_CANDIDATE_EXECUTABLE" "BlockedNoOMS missing."

Require-True ([bool]$marks.missingStaleMarkPreservationCreated) "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING" "Missing/stale preservation missing."
Require-True ([bool]$marks.missingMarkStatusPreserved) "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING" "MissingMark not preserved."
Require-True ([bool]$marks.staleMarkStatusPreserved) "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING" "StaleMark not preserved."
Require-False ([bool]$marks.blockedMissingStaleLinesBecomeCandidates) "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING" "Blocked missing/stale lines became candidates."
Require-False ([bool]$marks.missingOrStaleMarksHidden) "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING" "Missing/stale marks hidden."
Require-False ([bool]$marks.fabricatedMarksForCandidateShape) "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING" "Marks fabricated."
Require-False ([bool]$marks.rawMarketDataFixturePayloadsSerialized) "PMS_EMS_OMS_R010_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw fixture payload serialized."

Require-True ([bool]$drift.driftAcknowledgementPreservationCreated) "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING" "Drift preservation missing."
Require-True ([string]$drift.theoreticalVsRealStatus -eq "Drift") "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING" "Drift status missing."
Require-True ([bool]$drift.driftAcknowledgedByOperatorDecision) "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING" "Drift acknowledgement missing."
Require-True ([bool]$drift.driftAcknowledgementPreservedInCandidateBatch) "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING" "Drift not preserved in candidate batch."
Require-True ([bool]$drift.driftAllowsPaperCandidateShapesOnly) "PMS_EMS_OMS_R010_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Drift allows non-paper candidate."
Require-False ([bool]$drift.liveTradingApprovalCreated) "PMS_EMS_OMS_R010_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Live trading approval created."

Require-True ([bool]$universe.instrumentUniverseHandlingCreated) "PMS_EMS_OMS_R010_FAIL_LMAX_GAP_BLOCKS_CANDIDATE_SHAPE" "Universe handling missing."
Require-False ([bool]$universe.lmaxReadOnlyScopeUsedAsCandidateGate) "PMS_EMS_OMS_R010_FAIL_LMAX_GAP_BLOCKS_CANDIDATE_SHAPE" "LMAX scope gates candidates."
Require-False ([bool]$universe.lmaxLiveValidationGapsBlockCandidateShape) "PMS_EMS_OMS_R010_FAIL_LMAX_GAP_BLOCKS_CANDIDATE_SHAPE" "LMAX gaps block candidates."
Require-False ([bool]$universe.audusdLiveValidationGapBlocksCandidateShape) "PMS_EMS_OMS_R010_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD gap blocks candidates."
Require-False ([bool]$universe.usdjpyLiveValidationGapBlocksCandidateShape) "PMS_EMS_OMS_R010_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY gap blocks candidates."

Require-True ([bool]$usdjpy.usdJpyCaveatPreservationCreated) "PMS_EMS_OMS_R010_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
Require-True ([string]$usdjpy.usdJpySecurityId -eq "4004") "PMS_EMS_OMS_R010_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID missing."
Require-True ([string]$usdjpy.usdJpySecurityIdSource -eq "8") "PMS_EMS_OMS_R010_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource missing."
Require-True ([bool]$usdjpy.usdJpyNotProven) "PMS_EMS_OMS_R010_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY not-proven missing."
Require-False ([bool]$usdjpy.usdJpyClassifiedAsFailed) "PMS_EMS_OMS_R010_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY classified failed."
Require-True ([bool]$usdjpy.audusdTlsBoundaryInconclusive) "PMS_EMS_OMS_R010_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD TLS status missing."
Require-False ([bool]$usdjpy.audusdClassifiedAsFailed) "PMS_EMS_OMS_R010_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD classified failed."

Require-True ([bool]$lmax.lmaxReadonlyBaselineReferenceCreated) "PMS_EMS_OMS_R010_FAIL_LMAX_GAP_BLOCKS_CANDIDATE_SHAPE" "LMAX baseline missing."
Require-False ([bool]$lmax.lmaxUsedInThisPhase) "PMS_EMS_OMS_R010_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX used in R010."
Require-False ([bool]$lmax.lmaxCalledInThisPhase) "PMS_EMS_OMS_R010_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX called in R010."
Require-False ([bool]$lmax.lmaxLiveValidationGapsBlockCandidateShape) "PMS_EMS_OMS_R010_FAIL_LMAX_GAP_BLOCKS_CANDIDATE_SHAPE" "LMAX gaps block candidates."
Require-True ([bool]$lmax.baseline.GBPUSD.readOnlyMarketDataSucceeded) "PMS_EMS_OMS_R010_FAIL_LMAX_GAP_BLOCKS_CANDIDATE_SHAPE" "GBPUSD baseline missing."
Require-True ([int]$lmax.baseline.GBPUSD.sanitizedEntryCount -eq 2) "PMS_EMS_OMS_R010_FAIL_LMAX_GAP_BLOCKS_CANDIDATE_SHAPE" "GBPUSD sanitized count missing."
Require-True ([bool]$lmax.baseline.EURGBP.readOnlyMarketDataSucceeded) "PMS_EMS_OMS_R010_FAIL_LMAX_GAP_BLOCKS_CANDIDATE_SHAPE" "EURGBP baseline missing."
Require-True ([int]$lmax.baseline.EURGBP.sanitizedEntryCount -eq 2) "PMS_EMS_OMS_R010_FAIL_LMAX_GAP_BLOCKS_CANDIDATE_SHAPE" "EURGBP sanitized count missing."
Require-False ([bool]$lmax.baseline.AUDUSD.classifiedAsFailed) "PMS_EMS_OMS_R010_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD classified failed."
Require-False ([bool]$lmax.baseline.USDJPY.classifiedAsFailed) "PMS_EMS_OMS_R010_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY classified failed."
Require-True ([string]$lmax.baseline.USDJPY.securityId -eq "4004") "PMS_EMS_OMS_R010_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID missing."
Require-True ([string]$lmax.baseline.USDJPY.securityIdSource -eq "8") "PMS_EMS_OMS_R010_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource missing."

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
    "candidateExecutable",
    "candidateSubmitted",
    "candidateHasBrokerRoute",
    "candidateRepresentedAsOmsOrder",
    "candidateRepresentedAsBrokerOrder",
    "blockedR009LinesBecomeReadyCandidates",
    "lmaxLiveValidationGapsBlockCandidateShape"
)) {
    Require-False ([bool]$noExternal.$property) "PMS_EMS_OMS_R010_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected: $property"
}

foreach ($item in $forbidden.forbiddenActions) {
    if ([bool]$item.detected) {
        if ([string]$item.action -match "scheduler|service|timer|background") {
            Fail-Gate "PMS_EMS_OMS_R010_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "order|trading|executable|OMS|parent|child|broker|fill|execution report|submitted|routed") {
            Fail-Gate "PMS_EMS_OMS_R010_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden action detected: $($item.action)"
        }

        Fail-Gate "PMS_EMS_OMS_R010_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $($item.action)"
    }
}

Require-True ([bool]$nextPhase.nextPhaseRecommendationCreated) "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING" "Next phase recommendation missing."
Require-True ([string]$nextPhase.recommendedNextPhase -eq "PMS-EMS-OMS-R011") "PMS_EMS_OMS_R010_FAIL_PAPER_ORDER_CANDIDATE_CONTRACT_MISSING" "Next phase is not R011."
Require-True ([bool]$nextPhase.mustRemainNoExternal) "PMS_EMS_OMS_R010_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase not no-external."
Require-True ([bool]$nextPhase.mustNotCreateExecutableOrders) "PMS_EMS_OMS_R010_FAIL_EXECUTABLE_ORDER_CREATED" "Next phase permits executable orders."
Require-True ([bool]$nextPhase.mustNotCreateOmsOrders) "PMS_EMS_OMS_R010_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Next phase permits OMS orders."
Require-True ([bool]$nextPhase.mustNotCreateBrokerOrders) "PMS_EMS_OMS_R010_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Next phase permits broker orders."
Require-True ([bool]$nextPhase.mustNotSubmitOrders) "PMS_EMS_OMS_R010_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "Next phase permits order submission."

$apiSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Api/appsettings.json") "PMS_EMS_OMS_R010_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$workerSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/appsettings.json") "PMS_EMS_OMS_R010_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-False ([bool]$apiSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R010_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API live trading enabled."
Require-False ([bool]$workerSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R010_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker live trading enabled."
Require-False ([bool]$apiSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R010_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API external connections enabled."
Require-False ([bool]$workerSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R010_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker external connections enabled."
Require-True ([bool]$apiSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R010_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API fake gateway not required."
Require-True ([bool]$workerSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R010_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker fake gateway not required."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.Enabled) "PMS_EMS_OMS_R010_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX read-only runtime enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowExternalConnections) "PMS_EMS_OMS_R010_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX external connections enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowOrderSubmission) "PMS_EMS_OMS_R010_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "LMAX order submission enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SchedulerEnabled) "PMS_EMS_OMS_R010_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "LMAX scheduler enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SubmitToShadowReplay) "PMS_EMS_OMS_R010_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Shadow replay enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.PersistRawFixMessages) "PMS_EMS_OMS_R010_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw FIX persistence enabled."
Require-False ([bool]$workerSettings.MarketDataBars.Enabled) "PMS_EMS_OMS_R010_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker market data bars enabled."

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-pms-ems-oms-r010-*" -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combined = [string]::Join("`n", $artifactText)
foreach ($pattern in @("\u0001", "35=", "MDReqID\s*[:=]", "SenderCompID\s*[:=]", "TargetCompID\s*[:=]", "BeginString\s*[:=]", "SocketHost\s*[:=]", "TlsHost\s*[:=]", "Password\s*[:=]", "ApiKey\s*[:=]", "Bearer\s+[A-Za-z0-9_\.-]+", "rawBid", "rawAsk", "rawMid")) {
    if ($combined -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R010_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Unsafe serialized content pattern found: $pattern"
    }
}

$requiredFiles = @(
    "src/QQ.Production.Intraday.Application/QubesPaperOrderCandidateShape.cs",
    "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperOrderCandidateShapeTests.cs"
)
foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $file))) {
        Fail-Gate "PMS_EMS_OMS_R010_FAIL_BUILD_OR_TESTS" "Required implementation/test file missing: $file"
    }
}

$source = Get-Content -LiteralPath (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/QubesPaperOrderCandidateShape.cs") -Raw
foreach ($pattern in @("TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "SendOrderAsync", "SubmitOrder", "FixSession")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R010_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "R010 source contains forbidden runtime pattern: $pattern"
    }
}
foreach ($pattern in @("AddHostedService", "IHostedService", "BackgroundService", "PeriodicTimer", "Task.Delay", "System.Threading.Timer")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R010_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "R010 source contains scheduler/service pattern: $pattern"
    }
}

$tests = Get-Content -LiteralPath (Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperOrderCandidateShapeTests.cs") -Raw
foreach ($requiredTestName in @(
    "Accepted_r009_review_lines_create_paper_order_candidates",
    "Blocked_r009_review_lines_do_not_create_ready_candidates",
    "Candidate_references_cycle_run_id_and_qubes_run_id",
    "Candidate_references_operator_decision_id",
    "Candidate_references_source_rebalance_intent",
    "Candidate_references_paper_risk_review_result",
    "Positive_delta_creates_buy_candidate",
    "Negative_delta_creates_sell_candidate",
    "Zero_delta_creates_no_candidate_by_convention",
    "Candidate_is_explicitly_non_executable",
    "Candidate_is_not_an_order_not_submitted_and_has_no_broker_route",
    "Quantity_remains_safe_shape_when_live_mark_or_lot_sizing_is_unavailable",
    "No_oms_parent_child_or_broker_order_is_created",
    "No_order_submission_path_is_introduced",
    "No_fill_or_execution_report_is_introduced",
    "Candidate_shape_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action",
    "Api_and_worker_live_gateway_remain_disabled",
    "Candidate_shape_source_introduces_no_scheduler_timer_polling_or_background_job",
    "Audusd_is_not_misclassified_as_failed",
    "Usdjpy_caveat_remains_preserved"
)) {
    if ($tests -notmatch [regex]::Escape($requiredTestName)) {
        Fail-Gate "PMS_EMS_OMS_R010_FAIL_BUILD_OR_TESTS" "Focused test missing: $requiredTestName"
    }
}

Require-True ([string]$evidence.build.status -eq "PASS") "PMS_EMS_OMS_R010_FAIL_BUILD_OR_TESTS" "Build evidence missing or not PASS."
Require-True ([int]$evidence.build.failed -eq 0) "PMS_EMS_OMS_R010_FAIL_BUILD_OR_TESTS" "Build evidence has failures."
Require-True ([string]$evidence.focusedTests.status -eq "PASS") "PMS_EMS_OMS_R010_FAIL_BUILD_OR_TESTS" "Focused test evidence missing or not PASS."
Require-True ([int]$evidence.focusedTests.failed -eq 0) "PMS_EMS_OMS_R010_FAIL_BUILD_OR_TESTS" "Focused tests have failures."
Require-True ([string]$evidence.unitTests.status -eq "PASS") "PMS_EMS_OMS_R010_FAIL_BUILD_OR_TESTS" "Unit test evidence missing or not PASS."
Require-True ([int]$evidence.unitTests.failed -eq 0) "PMS_EMS_OMS_R010_FAIL_BUILD_OR_TESTS" "Unit tests have failures."
Require-True ([string]$evidence.validator.status -eq "PASS") "PMS_EMS_OMS_R010_FAIL_BUILD_OR_TESTS" "Validator evidence missing or not PASS."
Require-True ([int]$evidence.validator.failed -eq 0) "PMS_EMS_OMS_R010_FAIL_BUILD_OR_TESTS" "Validator evidence has failures."
Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R010_FAIL_BUILD_OR_TESTS" "Build/test/validator evidence marker missing."

Write-Host "PMS_EMS_OMS_R010_PASS_PAPER_ORDER_CANDIDATE_SHAPES_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R010_PASS_NONEXECUTABLE_ORDER_CANDIDATE_GATE_READY_NO_EXTERNAL"
