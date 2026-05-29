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
    "phase-pms-ems-oms-r011-summary.md" = "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r011-paper-candidate-archive-contract.json" = "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r011-paper-candidate-archive.json" = "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r011-paper-candidate-blotter.md" = "PMS_EMS_OMS_R011_FAIL_OPERATOR_BLOTTER_MISSING"
    "phase-pms-ems-oms-r011-paper-candidate-blotter.json" = "PMS_EMS_OMS_R011_FAIL_OPERATOR_BLOTTER_MISSING"
    "phase-pms-ems-oms-r011-blocked-lines-preservation.json" = "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r011-non-executable-candidate-audit.json" = "PMS_EMS_OMS_R011_FAIL_CANDIDATE_EXECUTABLE"
    "phase-pms-ems-oms-r011-no-order-created-audit.json" = "PMS_EMS_OMS_R011_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r011-idempotency-evidence.json" = "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r011-risk-lineage-preservation.json" = "PMS_EMS_OMS_R011_FAIL_RISK_LINEAGE_MISSING"
    "phase-pms-ems-oms-r011-qubes-lineage-preservation.json" = "PMS_EMS_OMS_R011_FAIL_QUBES_LINEAGE_WEAKENED"
    "phase-pms-ems-oms-r011-operator-decision-lineage-preservation.json" = "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r011-rebalance-intent-lineage-preservation.json" = "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r011-missing-stale-mark-preservation.json" = "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r011-drift-acknowledgement-preservation.json" = "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r011-instrument-universe-handling.json" = "PMS_EMS_OMS_R011_FAIL_LMAX_GAP_BLOCKS_CANDIDATE_ARCHIVE"
    "phase-pms-ems-oms-r011-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R011_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r011-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R011_FAIL_LMAX_GAP_BLOCKS_CANDIDATE_ARCHIVE"
    "phase-pms-ems-oms-r011-no-external-audit.json" = "PMS_EMS_OMS_R011_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r011-forbidden-actions-audit.json" = "PMS_EMS_OMS_R011_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r011-next-phase-recommendation.json" = "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r011-build-test-validator-evidence.json" = "PMS_EMS_OMS_R011_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $entry.Key))) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$contract = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r011-paper-candidate-archive-contract.json") "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING"
$archive = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r011-paper-candidate-archive.json") "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING"
$blotter = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r011-paper-candidate-blotter.json") "PMS_EMS_OMS_R011_FAIL_OPERATOR_BLOTTER_MISSING"
$blocked = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r011-blocked-lines-preservation.json") "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING"
$nonExec = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r011-non-executable-candidate-audit.json") "PMS_EMS_OMS_R011_FAIL_CANDIDATE_EXECUTABLE"
$orderAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r011-no-order-created-audit.json") "PMS_EMS_OMS_R011_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$idempotency = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r011-idempotency-evidence.json") "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING"
$risk = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r011-risk-lineage-preservation.json") "PMS_EMS_OMS_R011_FAIL_RISK_LINEAGE_MISSING"
$lineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r011-qubes-lineage-preservation.json") "PMS_EMS_OMS_R011_FAIL_QUBES_LINEAGE_WEAKENED"
$decision = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r011-operator-decision-lineage-preservation.json") "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING"
$rebalance = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r011-rebalance-intent-lineage-preservation.json") "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING"
$marks = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r011-missing-stale-mark-preservation.json") "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING"
$drift = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r011-drift-acknowledgement-preservation.json") "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r011-instrument-universe-handling.json") "PMS_EMS_OMS_R011_FAIL_LMAX_GAP_BLOCKS_CANDIDATE_ARCHIVE"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r011-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R011_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r011-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R011_FAIL_LMAX_GAP_BLOCKS_CANDIDATE_ARCHIVE"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r011-no-external-audit.json") "PMS_EMS_OMS_R011_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r011-forbidden-actions-audit.json") "PMS_EMS_OMS_R011_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$nextPhase = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r011-next-phase-recommendation.json") "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r011-build-test-validator-evidence.json") "PMS_EMS_OMS_R011_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$contract.paperCandidateArchiveContractCreated) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Archive contract missing."
foreach ($field in @("PaperOrderCandidateBatchId", "CycleRunId", "QubesRunId", "OperatorDecisionId", "PaperRiskReviewId", "CreatedAtUtc", "CandidateCount", "ReadyCandidateCount", "BlockedCandidateCount", "BatchStatus")) {
    Require-True (($contract.batchFields -contains $field)) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Batch field missing: $field"
}
foreach ($field in @("PaperOrderCandidateId", "SourceRebalanceIntentId", "InstrumentId", "NormalizedSymbol", "Side", "DeltaWeight", "DeltaNotional", "QuantityShapeCategory", "CandidateStatus", "RiskReviewReference", "PaperOnly", "NonExecutable", "NotAnOrder", "NotSubmitted", "NoBrokerRoute")) {
    Require-True (($contract.lineFields -contains $field)) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Line field missing: $field"
}
Require-True ([bool]$contract.paperOnly) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_EXECUTABLE" "Contract not paper-only."
Require-True ([bool]$contract.nonExecutable) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_EXECUTABLE" "Contract not non-executable."
Require-True ([bool]$contract.notAnOrder) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_IS_OMS_OR_BROKER_ORDER" "Contract permits order representation."
Require-True ([bool]$contract.notSubmitted) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_SUBMITTED_OR_ROUTED" "Contract permits submission."
Require-True ([bool]$contract.noBrokerRoute) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_SUBMITTED_OR_ROUTED" "Contract permits broker route."
foreach ($property in @("omsOrderEntityCreated", "parentOrderEntityCreated", "childOrderEntityCreated", "brokerOrderEntityCreated", "fillEntityCreated", "executionReportEntityCreated")) {
    Require-False ([bool]$contract.$property) "PMS_EMS_OMS_R011_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Contract created forbidden entity: $property"
}

Require-True ([bool]$archive.paperCandidateArchiveCreated) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Archive artifact missing."
Require-True ([int]$archive.candidateCount -eq 3) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Unexpected candidate count."
Require-True ([int]$archive.readyCandidateCount -eq 3) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Unexpected ready count."
Require-True ([int]$archive.blockedCandidateCount -eq 10) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Unexpected blocked count."
Require-True ([string]$archive.batchStatus -eq "ArchivedWithBlockedLines") "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Archive status wrong."
Require-True ([bool]$archive.noExternal) "PMS_EMS_OMS_R011_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Archive not no-external."
Require-False ([bool]$archive.blockedLinesBecomePaperReadyCandidates) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Blocked lines became candidates."
foreach ($candidate in $archive.candidateLines) {
    Require-True ([bool]$candidate.paperOnly) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_EXECUTABLE" "Candidate not paper-only."
    Require-True ([bool]$candidate.nonExecutable) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_EXECUTABLE" "Candidate executable."
    Require-True ([bool]$candidate.notAnOrder) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_IS_OMS_OR_BROKER_ORDER" "Candidate is an order."
    Require-True ([bool]$candidate.notSubmitted) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_SUBMITTED_OR_ROUTED" "Candidate submitted."
    Require-True ([bool]$candidate.noBrokerRoute) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_SUBMITTED_OR_ROUTED" "Candidate has route."
    Require-True ([string]$candidate.quantityShapeCategory -eq "QuantityRequiresMarkOrLotSizing") "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Quantity is not shape-only."
    Require-True ([string]$candidate.orderTypeShapeCategory -eq "NotExecutable") "PMS_EMS_OMS_R011_FAIL_CANDIDATE_EXECUTABLE" "Order type executable."
    Require-True ([string]$candidate.timeInForceShapeCategory -eq "NotExecutable") "PMS_EMS_OMS_R011_FAIL_CANDIDATE_EXECUTABLE" "TIF executable."
}

Require-True ([bool]$blotter.paperCandidateBlotterCreated) "PMS_EMS_OMS_R011_FAIL_OPERATOR_BLOTTER_MISSING" "Blotter missing."
Require-True ([int]$blotter.lineCount -eq 3) "PMS_EMS_OMS_R011_FAIL_OPERATOR_BLOTTER_MISSING" "Blotter line count wrong."
Require-True ([int]$blotter.blockedLineSummaryCount -eq 10) "PMS_EMS_OMS_R011_FAIL_OPERATOR_BLOTTER_MISSING" "Blocked summary count wrong."
Require-True (($blotter.disclaimers -contains "No order created.")) "PMS_EMS_OMS_R011_FAIL_OPERATOR_BLOTTER_MISSING" "No-order disclaimer missing."
Require-True (($blotter.disclaimers -contains "No broker route exists.")) "PMS_EMS_OMS_R011_FAIL_OPERATOR_BLOTTER_MISSING" "No-route disclaimer missing."
Require-True (($blotter.disclaimers -contains "Candidates are not executable.")) "PMS_EMS_OMS_R011_FAIL_OPERATOR_BLOTTER_MISSING" "Non-executable disclaimer missing."
Require-True (($blotter.disclaimers -contains "Candidates were not submitted.")) "PMS_EMS_OMS_R011_FAIL_OPERATOR_BLOTTER_MISSING" "Not-submitted disclaimer missing."
Require-True (@($blotter.lines | Where-Object { $_.instrument -eq "AUDUSD" -and $_.side -eq "Buy" }).Count -eq 1) "PMS_EMS_OMS_R011_FAIL_OPERATOR_BLOTTER_MISSING" "AUDUSD Buy missing."
Require-True (@($blotter.lines | Where-Object { $_.instrument -eq "EURUSD" -and $_.side -eq "Buy" }).Count -eq 1) "PMS_EMS_OMS_R011_FAIL_OPERATOR_BLOTTER_MISSING" "EURUSD Buy missing."
Require-True (@($blotter.lines | Where-Object { $_.instrument -eq "GBPUSD" -and $_.side -eq "Sell" }).Count -eq 1) "PMS_EMS_OMS_R011_FAIL_OPERATOR_BLOTTER_MISSING" "GBPUSD Sell missing."
foreach ($line in $blotter.lines) {
    Require-True ([bool]$line.paperOnly) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_EXECUTABLE" "Blotter line not paper-only."
    Require-True ([bool]$line.noOrderCreated) "PMS_EMS_OMS_R011_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Blotter line order created."
    Require-True ([bool]$line.noBrokerRoute) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_SUBMITTED_OR_ROUTED" "Blotter line has route."
    Require-True ([bool]$line.nonExecutable) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_EXECUTABLE" "Blotter line executable."
    Require-True ([bool]$line.notSubmitted) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_SUBMITTED_OR_ROUTED" "Blotter line submitted."
}

$blotterMarkdown = Get-Content -LiteralPath (Join-Path $artifactRoot "phase-pms-ems-oms-r011-paper-candidate-blotter.md") -Raw
foreach ($text in @("Paper Order Candidate Blotter", "No order created.", "No broker route exists.", "Candidates are not executable.", "Candidates were not submitted.", "AUDUSD", "EURUSD", "GBPUSD")) {
    if ($blotterMarkdown -notmatch [regex]::Escape($text)) {
        Fail-Gate "PMS_EMS_OMS_R011_FAIL_OPERATOR_BLOTTER_MISSING" "Blotter markdown missing: $text"
    }
}

Require-True ([bool]$blocked.blockedLinesPreservationCreated) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Blocked preservation missing."
Require-True ([int]$blocked.blockedR009LineCount -eq 10) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Blocked count wrong."
Require-True ([bool]$blocked.blockedLinesCarriedSeparately) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Blocked lines not separate."
Require-True ([bool]$blocked.blockedLinesIgnoredForPaperReadyCandidates) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Blocked lines not ignored."
Require-False ([bool]$blocked.blockedR009LinesBecomePaperReadyCandidates) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Blocked lines became paper-ready."
Require-False ([bool]$blocked.blockedLinesCreateOrders) "PMS_EMS_OMS_R011_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Blocked lines create orders."
Require-False ([bool]$blocked.blockedLinesSubmitOrders) "PMS_EMS_OMS_R011_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "Blocked lines submit orders."

Require-True ([bool]$nonExec.nonExecutableCandidateAuditCreated) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_EXECUTABLE" "Non-executable audit missing."
foreach ($property in @("allCandidatesPaperOnly", "allCandidatesNonExecutable", "allCandidatesNotAnOrder", "allCandidatesNotSubmitted", "allCandidatesNoBrokerRoute", "allCandidatesOrderTypeNotExecutable", "allCandidatesTimeInForceNotExecutable")) {
    Require-True ([bool]$nonExec.$property) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_EXECUTABLE" "Candidate audit missing: $property"
}
foreach ($property in @("candidateExecutable", "candidateSubmitted", "candidateHasBrokerRoute", "candidateRepresentedAsOmsOrder", "candidateRepresentedAsBrokerOrder", "candidateCreatesFill", "candidateCreatesExecutionReport")) {
    Require-False ([bool]$nonExec.$property) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_EXECUTABLE" "Candidate audit detected: $property"
}

Require-True ([bool]$orderAudit.noOrderCreatedAuditCreated) "PMS_EMS_OMS_R011_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No-order audit missing."
foreach ($property in @("omsOrderCreated", "parentOrderCreated", "childOrderCreated", "brokerOrderCreated", "executableOrderCreated", "fillCreated", "executionReportCreated", "orderStateCreated", "orderSubmissionPathIntroduced", "ordersSubmitted", "liveTradingStateMutated", "brokerGatewayCalled")) {
    Require-False ([bool]$orderAudit.$property) "PMS_EMS_OMS_R011_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No-order audit detected: $property"
}

Require-True ([bool]$idempotency.idempotencyEvidenceCreated) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Idempotency missing."
Require-True ([string]$idempotency.idempotencyKey -eq "PaperOrderCandidateBatchId") "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Wrong idempotency key."
Require-True ([string]$idempotency.secondArchiveStatus -eq "DuplicateReturned") "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Duplicate not returned safely."
Require-False ([bool]$idempotency.duplicateCreatesAdditionalRows) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Duplicate creates rows."
Require-False ([bool]$idempotency.duplicateCreatesOrders) "PMS_EMS_OMS_R011_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Duplicate creates orders."
Require-False ([bool]$idempotency.duplicateSubmitsOrders) "PMS_EMS_OMS_R011_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "Duplicate submits orders."

Require-True ([bool]$risk.riskLineagePreservationCreated) "PMS_EMS_OMS_R011_FAIL_RISK_LINEAGE_MISSING" "Risk lineage missing."
Require-True ([bool]$risk.paperRiskReviewReportReferenced) "PMS_EMS_OMS_R011_FAIL_RISK_LINEAGE_MISSING" "Risk report missing."
Require-True ([bool]$risk.riskReviewReferencePresentOnEveryCandidate) "PMS_EMS_OMS_R011_FAIL_RISK_LINEAGE_MISSING" "Risk reference missing."
Require-True ([bool]$risk.riskReasonsPreservedInBlotter) "PMS_EMS_OMS_R011_FAIL_RISK_LINEAGE_MISSING" "Risk reasons missing in blotter."

Require-True ([bool]$lineage.qubesLineagePreservationCreated) "PMS_EMS_OMS_R011_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage missing."
Require-True ([string]$lineage.sourceSystem -eq "ModelWeightSourceSystem.Qubes") "PMS_EMS_OMS_R011_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes source missing."
Require-True ([int]$lineage.cadenceMinutes -eq 15) "PMS_EMS_OMS_R011_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes cadence missing."
foreach ($property in @("qubesAuditBatchPreserved", "rawQubesRowAuditPreserved", "normalizedWeightAuditPreserved", "modelWeightBatchLinkagePreserved", "modelRunLinkagePreserved", "targetWeightLinkagePreserved")) {
    Require-True ([bool]$lineage.$property) "PMS_EMS_OMS_R011_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage missing: $property"
}
Require-False ([bool]$lineage.qubesLineageWeakened) "PMS_EMS_OMS_R011_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage weakened."

Require-True ([bool]$decision.operatorDecisionLineagePreservationCreated) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Operator decision lineage missing."
Require-True ([string]$decision.operatorDecisionType -eq "PromoteToPaperReady") "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Operator decision is not promotion."
Require-True ([string]$decision.resultingCycleReviewStatus -eq "PaperReadyNoExternal") "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Operator decision is not no-external paper."
Require-True ([bool]$decision.operatorDecisionReferencedOnEveryCandidate) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Operator decision missing on candidates."
Require-False ([bool]$decision.promotionMeansLiveTrading) "PMS_EMS_OMS_R011_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Promotion enables live trading."
Require-False ([bool]$decision.promotionCreatesOrders) "PMS_EMS_OMS_R011_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Promotion creates orders."

Require-True ([bool]$rebalance.rebalanceIntentLineagePreservationCreated) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Rebalance lineage missing."
Require-True ([bool]$rebalance.sourceRebalanceIntentReferencedOnEveryCandidate) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Source rebalance intent missing."
Require-True ([bool]$rebalance.rebalanceIntentsRemainNonExecutable) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_EXECUTABLE" "Rebalance intents executable."
foreach ($property in @("allSourceIntentsTheoreticalOnly", "allSourceIntentsNotExecutable", "allSourceIntentsBlockedNoOms")) {
    Require-True ([bool]$rebalance.$property) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_EXECUTABLE" "Rebalance status missing: $property"
}
Require-False ([bool]$rebalance.rebalanceIntentCreatesOrder) "PMS_EMS_OMS_R011_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Rebalance intent creates order."
Require-False ([bool]$rebalance.rebalanceIntentSubmitted) "PMS_EMS_OMS_R011_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "Rebalance intent submitted."

Require-True ([bool]$marks.missingStaleMarkPreservationCreated) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Missing/stale preservation missing."
Require-True ([bool]$marks.missingMarkStatusPreserved) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "MissingMark missing."
Require-True ([bool]$marks.staleMarkStatusPreserved) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "StaleMark missing."
Require-True ([bool]$marks.blockedMissingStaleLinesPreservedSeparately) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Blocked missing/stale not separate."
Require-False ([bool]$marks.blockedMissingStaleLinesBecomeCandidates) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Blocked missing/stale became candidates."
Require-False ([bool]$marks.missingOrStaleMarksHidden) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Missing/stale hidden."
Require-False ([bool]$marks.fabricatedMarksForCandidateArchive) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Marks fabricated."
Require-False ([bool]$marks.rawMarketDataFixturePayloadsSerialized) "PMS_EMS_OMS_R011_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw fixture payload serialized."

Require-True ([bool]$drift.driftAcknowledgementPreservationCreated) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Drift preservation missing."
Require-True ([string]$drift.theoreticalVsRealStatus -eq "Drift") "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Drift status missing."
Require-True ([bool]$drift.driftAcknowledgedByOperatorDecision) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Drift not acknowledged."
Require-True ([bool]$drift.driftAcknowledgementPreservedInCandidateArchive) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Drift not preserved."
Require-True ([bool]$drift.driftAllowsPaperBlotterOnly) "PMS_EMS_OMS_R011_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Drift allows non-paper path."
Require-False ([bool]$drift.liveTradingApprovalCreated) "PMS_EMS_OMS_R011_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Live trading approval created."

Require-True ([bool]$universe.instrumentUniverseHandlingCreated) "PMS_EMS_OMS_R011_FAIL_LMAX_GAP_BLOCKS_CANDIDATE_ARCHIVE" "Universe handling missing."
Require-False ([bool]$universe.lmaxReadOnlyScopeUsedAsCandidateArchiveGate) "PMS_EMS_OMS_R011_FAIL_LMAX_GAP_BLOCKS_CANDIDATE_ARCHIVE" "LMAX gates archive."
Require-False ([bool]$universe.lmaxLiveValidationGapsBlockCandidateArchive) "PMS_EMS_OMS_R011_FAIL_LMAX_GAP_BLOCKS_CANDIDATE_ARCHIVE" "LMAX gaps block archive."
Require-False ([bool]$universe.audusdLiveValidationGapBlocksCandidateArchive) "PMS_EMS_OMS_R011_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD gap blocks archive."
Require-False ([bool]$universe.usdjpyLiveValidationGapBlocksCandidateArchive) "PMS_EMS_OMS_R011_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY gap blocks archive."

Require-True ([bool]$usdjpy.usdJpyCaveatPreservationCreated) "PMS_EMS_OMS_R011_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
Require-True ([string]$usdjpy.usdJpySecurityId -eq "4004") "PMS_EMS_OMS_R011_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID missing."
Require-True ([string]$usdjpy.usdJpySecurityIdSource -eq "8") "PMS_EMS_OMS_R011_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource missing."
Require-True ([bool]$usdjpy.usdJpyNotProven) "PMS_EMS_OMS_R011_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY not-proven missing."
Require-False ([bool]$usdjpy.usdJpyClassifiedAsFailed) "PMS_EMS_OMS_R011_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY classified failed."
Require-True ([bool]$usdjpy.audusdTlsBoundaryInconclusive) "PMS_EMS_OMS_R011_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD TLS status missing."
Require-False ([bool]$usdjpy.audusdClassifiedAsFailed) "PMS_EMS_OMS_R011_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD classified failed."

Require-True ([bool]$lmax.lmaxReadonlyBaselineReferenceCreated) "PMS_EMS_OMS_R011_FAIL_LMAX_GAP_BLOCKS_CANDIDATE_ARCHIVE" "LMAX baseline missing."
Require-False ([bool]$lmax.lmaxUsedInThisPhase) "PMS_EMS_OMS_R011_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX used."
Require-False ([bool]$lmax.lmaxCalledInThisPhase) "PMS_EMS_OMS_R011_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX called."
Require-False ([bool]$lmax.lmaxLiveValidationGapsBlockCandidateArchive) "PMS_EMS_OMS_R011_FAIL_LMAX_GAP_BLOCKS_CANDIDATE_ARCHIVE" "LMAX gaps block archive."
Require-True ([int]$lmax.baseline.GBPUSD.sanitizedEntryCount -eq 2) "PMS_EMS_OMS_R011_FAIL_LMAX_GAP_BLOCKS_CANDIDATE_ARCHIVE" "GBPUSD baseline count missing."
Require-True ([int]$lmax.baseline.EURGBP.sanitizedEntryCount -eq 2) "PMS_EMS_OMS_R011_FAIL_LMAX_GAP_BLOCKS_CANDIDATE_ARCHIVE" "EURGBP baseline count missing."
Require-False ([bool]$lmax.baseline.AUDUSD.classifiedAsFailed) "PMS_EMS_OMS_R011_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD classified failed."
Require-False ([bool]$lmax.baseline.USDJPY.classifiedAsFailed) "PMS_EMS_OMS_R011_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY classified failed."
Require-True ([string]$lmax.baseline.USDJPY.securityId -eq "4004") "PMS_EMS_OMS_R011_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID missing."
Require-True ([string]$lmax.baseline.USDJPY.securityIdSource -eq "8") "PMS_EMS_OMS_R011_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource missing."

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
    "blockedR009LinesBecomePaperReadyCandidates",
    "lmaxLiveValidationGapsBlockCandidateArchive"
)) {
    Require-False ([bool]$noExternal.$property) "PMS_EMS_OMS_R011_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected: $property"
}

foreach ($item in $forbidden.forbiddenActions) {
    if ([bool]$item.detected) {
        if ([string]$item.action -match "scheduler|service|timer|background") {
            Fail-Gate "PMS_EMS_OMS_R011_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "order|trading|executable|OMS|parent|child|broker|fill|execution report|submitted|routed") {
            Fail-Gate "PMS_EMS_OMS_R011_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden action detected: $($item.action)"
        }

        Fail-Gate "PMS_EMS_OMS_R011_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $($item.action)"
    }
}

Require-True ([bool]$nextPhase.nextPhaseRecommendationCreated) "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Next phase missing."
Require-True ([string]$nextPhase.recommendedNextPhase -eq "PMS-EMS-OMS-R012") "PMS_EMS_OMS_R011_FAIL_CANDIDATE_ARCHIVE_MISSING" "Next phase is not R012."
Require-True ([bool]$nextPhase.mustRemainNoExternal) "PMS_EMS_OMS_R011_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase not no-external."
Require-True ([bool]$nextPhase.mustNotCreateExecutableOrders) "PMS_EMS_OMS_R011_FAIL_EXECUTABLE_ORDER_CREATED" "Next phase permits executable orders."
Require-True ([bool]$nextPhase.mustNotCreateOmsOrders) "PMS_EMS_OMS_R011_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Next phase permits OMS orders."
Require-True ([bool]$nextPhase.mustNotCreateBrokerOrders) "PMS_EMS_OMS_R011_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Next phase permits broker orders."
Require-True ([bool]$nextPhase.mustNotSubmitOrders) "PMS_EMS_OMS_R011_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "Next phase permits submission."

$apiSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Api/appsettings.json") "PMS_EMS_OMS_R011_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$workerSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/appsettings.json") "PMS_EMS_OMS_R011_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-False ([bool]$apiSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R011_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API live trading enabled."
Require-False ([bool]$workerSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R011_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker live trading enabled."
Require-False ([bool]$apiSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R011_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API external enabled."
Require-False ([bool]$workerSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R011_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker external enabled."
Require-True ([bool]$apiSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R011_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API fake gateway not required."
Require-True ([bool]$workerSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R011_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker fake gateway not required."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.Enabled) "PMS_EMS_OMS_R011_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX runtime enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowExternalConnections) "PMS_EMS_OMS_R011_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX external enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowOrderSubmission) "PMS_EMS_OMS_R011_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "LMAX order submission enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SchedulerEnabled) "PMS_EMS_OMS_R011_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "LMAX scheduler enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SubmitToShadowReplay) "PMS_EMS_OMS_R011_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Shadow replay enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.PersistRawFixMessages) "PMS_EMS_OMS_R011_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw FIX persistence enabled."
Require-False ([bool]$workerSettings.MarketDataBars.Enabled) "PMS_EMS_OMS_R011_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker market data bars enabled."

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-pms-ems-oms-r011-*" -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combined = [string]::Join("`n", $artifactText)
foreach ($pattern in @("\u0001", "35=", "MDReqID\s*[:=]", "SenderCompID\s*[:=]", "TargetCompID\s*[:=]", "BeginString\s*[:=]", "SocketHost\s*[:=]", "TlsHost\s*[:=]", "Password\s*[:=]", "ApiKey\s*[:=]", "Bearer\s+[A-Za-z0-9_\.-]+", "rawBid", "rawAsk", "rawMid")) {
    if ($combined -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R011_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Unsafe serialized content pattern found: $pattern"
    }
}

$requiredFiles = @(
    "src/QQ.Production.Intraday.Application/QubesPaperOrderCandidateArchive.cs",
    "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperOrderCandidateArchiveTests.cs"
)
foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $file))) {
        Fail-Gate "PMS_EMS_OMS_R011_FAIL_BUILD_OR_TESTS" "Required file missing: $file"
    }
}

$source = Get-Content -LiteralPath (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/QubesPaperOrderCandidateArchive.cs") -Raw
foreach ($pattern in @("TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "SendOrderAsync", "SubmitOrder", "FixSession")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R011_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "R011 source contains forbidden runtime pattern: $pattern"
    }
}
foreach ($pattern in @("AddHostedService", "IHostedService", "BackgroundService", "PeriodicTimer", "Task.Delay", "System.Threading.Timer")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R011_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "R011 source contains scheduler/service pattern: $pattern"
    }
}

$tests = Get-Content -LiteralPath (Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperOrderCandidateArchiveTests.cs") -Raw
foreach ($requiredTestName in @(
    "R010_paper_candidates_can_be_archived_no_externally",
    "Candidate_batch_preserves_cycle_run_id_and_qubes_run_id",
    "Candidate_batch_preserves_operator_decision_id",
    "Candidate_lines_preserve_source_rebalance_intent_id",
    "Candidate_lines_preserve_risk_review_reference",
    "Audusd_buy_eurusd_buy_and_gbpusd_sell_candidates_appear_in_blotter",
    "Blocked_r009_lines_are_preserved_separately_and_do_not_become_paper_ready_candidates",
    "Candidate_status_remains_non_executable",
    "Candidate_remains_not_an_order_not_submitted_and_no_broker_route",
    "Blotter_includes_no_order_no_execution_disclaimer",
    "Missing_stale_mark_warnings_are_preserved",
    "Drift_acknowledgement_is_preserved",
    "Duplicate_candidate_batch_archive_is_idempotent",
    "No_oms_parent_child_or_broker_order_is_created",
    "No_fill_or_execution_report_is_introduced",
    "No_order_submission_path_is_introduced",
    "Archive_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action",
    "Api_and_worker_live_gateway_remain_disabled",
    "Archive_source_introduces_no_scheduler_timer_polling_or_background_job",
    "Audusd_is_not_misclassified_as_failed",
    "Usdjpy_caveat_remains_preserved"
)) {
    if ($tests -notmatch [regex]::Escape($requiredTestName)) {
        Fail-Gate "PMS_EMS_OMS_R011_FAIL_BUILD_OR_TESTS" "Focused test missing: $requiredTestName"
    }
}

Require-True ([string]$evidence.build.status -eq "PASS") "PMS_EMS_OMS_R011_FAIL_BUILD_OR_TESTS" "Build evidence missing or not PASS."
Require-True ([int]$evidence.build.failed -eq 0) "PMS_EMS_OMS_R011_FAIL_BUILD_OR_TESTS" "Build evidence has failures."
Require-True ([string]$evidence.focusedTests.status -eq "PASS") "PMS_EMS_OMS_R011_FAIL_BUILD_OR_TESTS" "Focused test evidence missing or not PASS."
Require-True ([int]$evidence.focusedTests.failed -eq 0) "PMS_EMS_OMS_R011_FAIL_BUILD_OR_TESTS" "Focused tests have failures."
Require-True ([string]$evidence.unitTests.status -eq "PASS") "PMS_EMS_OMS_R011_FAIL_BUILD_OR_TESTS" "Unit test evidence missing or not PASS."
Require-True ([int]$evidence.unitTests.failed -eq 0) "PMS_EMS_OMS_R011_FAIL_BUILD_OR_TESTS" "Unit tests have failures."
Require-True ([string]$evidence.validator.status -eq "PASS") "PMS_EMS_OMS_R011_FAIL_BUILD_OR_TESTS" "Validator evidence missing or not PASS."
Require-True ([int]$evidence.validator.failed -eq 0) "PMS_EMS_OMS_R011_FAIL_BUILD_OR_TESTS" "Validator evidence has failures."
Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R011_FAIL_BUILD_OR_TESTS" "Build/test/validator marker missing."

Write-Host "PMS_EMS_OMS_R011_PASS_PAPER_CANDIDATE_ARCHIVE_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R011_PASS_OPERATOR_BLOTTER_READY_NO_EXTERNAL"
