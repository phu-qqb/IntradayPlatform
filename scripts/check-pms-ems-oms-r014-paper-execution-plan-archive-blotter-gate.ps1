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
    "phase-pms-ems-oms-r014-summary.md" = "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r014-paper-execution-plan-archive-contract.json" = "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r014-paper-execution-plan-archive.json" = "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r014-paper-execution-plan-blotter.md" = "PMS_EMS_OMS_R014_FAIL_OPERATOR_PLAN_BLOTTER_MISSING"
    "phase-pms-ems-oms-r014-paper-execution-plan-blotter.json" = "PMS_EMS_OMS_R014_FAIL_OPERATOR_PLAN_BLOTTER_MISSING"
    "phase-pms-ems-oms-r014-plan-line-archive.json" = "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r014-blocked-lines-preservation.json" = "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r014-non-executable-plan-audit.json" = "PMS_EMS_OMS_R014_FAIL_PLAN_EXECUTABLE"
    "phase-pms-ems-oms-r014-no-order-created-audit.json" = "PMS_EMS_OMS_R014_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r014-no-route-no-submission-audit.json" = "PMS_EMS_OMS_R014_FAIL_PLAN_SUBMITTED_OR_ROUTED"
    "phase-pms-ems-oms-r014-no-fill-no-execution-report-audit.json" = "PMS_EMS_OMS_R014_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
    "phase-pms-ems-oms-r014-idempotency-evidence.json" = "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r014-risk-lineage-preservation.json" = "PMS_EMS_OMS_R014_FAIL_RISK_LINEAGE_MISSING"
    "phase-pms-ems-oms-r014-qubes-lineage-preservation.json" = "PMS_EMS_OMS_R014_FAIL_QUBES_LINEAGE_WEAKENED"
    "phase-pms-ems-oms-r014-operator-decision-lineage-preservation.json" = "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r014-paper-candidate-lineage-preservation.json" = "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r014-rebalance-intent-lineage-preservation.json" = "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r014-lot-sizing-lineage-preservation.json" = "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r014-missing-stale-mark-preservation.json" = "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r014-drift-acknowledgement-preservation.json" = "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r014-instrument-universe-handling.json" = "PMS_EMS_OMS_R014_FAIL_LMAX_GAP_BLOCKS_PLAN_ARCHIVE"
    "phase-pms-ems-oms-r014-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R014_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r014-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R014_FAIL_LMAX_GAP_BLOCKS_PLAN_ARCHIVE"
    "phase-pms-ems-oms-r014-no-external-audit.json" = "PMS_EMS_OMS_R014_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r014-forbidden-actions-audit.json" = "PMS_EMS_OMS_R014_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r014-next-phase-recommendation.json" = "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r014-build-test-validator-evidence.json" = "PMS_EMS_OMS_R014_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $entry.Key))) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$contract = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r014-paper-execution-plan-archive-contract.json") "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING"
$archive = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r014-paper-execution-plan-archive.json") "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING"
$blotter = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r014-paper-execution-plan-blotter.json") "PMS_EMS_OMS_R014_FAIL_OPERATOR_PLAN_BLOTTER_MISSING"
$lineArchive = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r014-plan-line-archive.json") "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING"
$blocked = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r014-blocked-lines-preservation.json") "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING"
$nonExec = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r014-non-executable-plan-audit.json") "PMS_EMS_OMS_R014_FAIL_PLAN_EXECUTABLE"
$orderAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r014-no-order-created-audit.json") "PMS_EMS_OMS_R014_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$routeAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r014-no-route-no-submission-audit.json") "PMS_EMS_OMS_R014_FAIL_PLAN_SUBMITTED_OR_ROUTED"
$fillAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r014-no-fill-no-execution-report-audit.json") "PMS_EMS_OMS_R014_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$idempotency = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r014-idempotency-evidence.json") "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING"
$risk = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r014-risk-lineage-preservation.json") "PMS_EMS_OMS_R014_FAIL_RISK_LINEAGE_MISSING"
$qubes = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r014-qubes-lineage-preservation.json") "PMS_EMS_OMS_R014_FAIL_QUBES_LINEAGE_WEAKENED"
$operatorDecision = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r014-operator-decision-lineage-preservation.json") "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING"
$candidateLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r014-paper-candidate-lineage-preservation.json") "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING"
$rebalance = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r014-rebalance-intent-lineage-preservation.json") "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING"
$lotSizing = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r014-lot-sizing-lineage-preservation.json") "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING"
$marks = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r014-missing-stale-mark-preservation.json") "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING"
$drift = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r014-drift-acknowledgement-preservation.json") "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r014-instrument-universe-handling.json") "PMS_EMS_OMS_R014_FAIL_LMAX_GAP_BLOCKS_PLAN_ARCHIVE"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r014-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R014_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r014-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R014_FAIL_LMAX_GAP_BLOCKS_PLAN_ARCHIVE"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r014-no-external-audit.json") "PMS_EMS_OMS_R014_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r014-forbidden-actions-audit.json") "PMS_EMS_OMS_R014_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$nextPhase = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r014-next-phase-recommendation.json") "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r014-build-test-validator-evidence.json") "PMS_EMS_OMS_R014_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$contract.paperExecutionPlanArchiveContractCreated) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Archive contract missing."
foreach ($field in @("PaperExecutionPlanId", "CycleRunId", "QubesRunId", "OperatorDecisionId", "PaperOrderCandidateBatchId", "PlanStatus", "PlanMode", "CreatedAtUtc", "ReadyLineCount", "BlockedLineCount", "SafetyStatus")) {
    Require-True (($contract.batchFields -contains $field)) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Batch field missing: $field"
}
foreach ($field in @("PaperExecutionPlanLineId", "PaperOrderCandidateId", "SourceRebalanceIntentId", "RiskReviewReference", "NormalizedSymbol", "PaperTradableSymbol", "Side", "PaperBaseQuantity", "QuantityCurrency", "NotionalCurrency", "LotSize", "QuantityRoundingMode", "QuantityShapeCategory", "QuantityStatus", "ExecutionStyleShape", "TimeInForceShape", "SequencingGroup", "Priority", "PlanLineStatus", "BlockReason", "NonExecutableReason")) {
    Require-True (($contract.lineFields -contains $field)) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Line field missing: $field"
}
foreach ($property in @("paperOnly", "nonExecutable", "notAnOrder", "notSubmitted", "noBrokerRoute", "noFill", "noExecutionReport")) {
    Require-True ([bool]$contract.$property) "PMS_EMS_OMS_R014_FAIL_PLAN_EXECUTABLE" "Contract missing safety flag: $property"
}

Require-True ([bool]$archive.paperExecutionPlanArchiveCreated) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Plan archive missing."
Require-True ([string]$archive.paperExecutionPlanId -eq "cycle-r014-sample:paper-execution-plan") "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Unexpected plan id."
Require-True ([string]$archive.cycleRunId -eq "cycle-r014-sample") "PMS_EMS_OMS_R014_FAIL_QUBES_LINEAGE_WEAKENED" "CycleRunId missing."
Require-True ([string]$archive.qubesRunId -eq "qubes-r014-sample") "PMS_EMS_OMS_R014_FAIL_QUBES_LINEAGE_WEAKENED" "QubesRunId missing."
Require-True ([string]$archive.planStatus -eq "PaperPlanPartiallyReady") "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Plan status wrong."
Require-True ([string]$archive.archiveStatus -eq "ArchivedWithBlockedLines") "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Archive status wrong."
Require-True ([string]$archive.safetyStatus -eq "NoExternalPlanShapeOnly") "PMS_EMS_OMS_R014_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Safety status wrong."
Require-True ([int]$archive.readyLineCount -eq 3) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Ready line count wrong."
Require-True ([int]$archive.blockedLineCount -eq 10) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Blocked line count wrong."
foreach ($property in @("paperOnly", "nonExecutable", "notAnOrder", "notSubmitted", "noBrokerRoute")) {
    Require-True ([bool]$archive.$property) "PMS_EMS_OMS_R014_FAIL_PLAN_EXECUTABLE" "Archive safety flag missing: $property"
}
foreach ($property in @("createdOmsOrder", "createdParentOrder", "createdChildOrder", "createdBrokerOrder", "createdFill", "createdExecutionReport", "submittedOrders", "calledBrokerGateway")) {
    Require-False ([bool]$archive.$property) "PMS_EMS_OMS_R014_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Archive detected forbidden output: $property"
}

Require-True ([bool]$blotter.paperExecutionPlanBlotterCreated) "PMS_EMS_OMS_R014_FAIL_OPERATOR_PLAN_BLOTTER_MISSING" "Blotter missing."
Require-True ([int]$blotter.lineCount -eq 3) "PMS_EMS_OMS_R014_FAIL_OPERATOR_PLAN_BLOTTER_MISSING" "Blotter line count wrong."
Require-True ([int]$blotter.blockedLineSummaryCount -eq 10) "PMS_EMS_OMS_R014_FAIL_OPERATOR_PLAN_BLOTTER_MISSING" "Blotter blocked summary missing."
foreach ($disclaimer in @("No order created.", "No broker route exists.", "Plan lines are not executable.", "Plan lines were not submitted.", "No fills were created.", "No execution reports were created.")) {
    Require-True (($blotter.disclaimers -contains $disclaimer)) "PMS_EMS_OMS_R014_FAIL_OPERATOR_PLAN_BLOTTER_MISSING" "Blotter disclaimer missing: $disclaimer"
}
Require-True (@($blotter.lines | Where-Object { $_.instrument -eq "AUDUSD" -and $_.side -eq "Buy" -and [decimal]$_.paperBaseQuantity -eq 131000 }).Count -eq 1) "PMS_EMS_OMS_R014_FAIL_OPERATOR_PLAN_BLOTTER_MISSING" "AUDUSD blotter line missing."
Require-True (@($blotter.lines | Where-Object { $_.instrument -eq "EURUSD" -and $_.side -eq "Buy" -and [decimal]$_.paperBaseQuantity -eq 124000 }).Count -eq 1) "PMS_EMS_OMS_R014_FAIL_OPERATOR_PLAN_BLOTTER_MISSING" "EURUSD blotter line missing."
Require-True (@($blotter.lines | Where-Object { $_.instrument -eq "GBPUSD" -and $_.side -eq "Sell" -and [decimal]$_.paperBaseQuantity -eq 368000 }).Count -eq 1) "PMS_EMS_OMS_R014_FAIL_OPERATOR_PLAN_BLOTTER_MISSING" "GBPUSD blotter line missing."
foreach ($line in $blotter.lines) {
    foreach ($property in @("paperOnly", "noOrderCreated", "noBrokerRoute", "nonExecutable", "notSubmitted", "noFill", "noExecutionReport")) {
        Require-True ([bool]$line.$property) "PMS_EMS_OMS_R014_FAIL_PLAN_EXECUTABLE" "Blotter line missing safety flag: $property"
    }
}
$blotterMarkdown = Get-Content -LiteralPath (Join-Path $artifactRoot "phase-pms-ems-oms-r014-paper-execution-plan-blotter.md") -Raw
foreach ($text in @("No order created.", "No broker route exists.", "Plan lines are not executable.", "No fills were created.", "No execution reports were created.")) {
    if ($blotterMarkdown -notmatch [regex]::Escape($text)) {
        Fail-Gate "PMS_EMS_OMS_R014_FAIL_OPERATOR_PLAN_BLOTTER_MISSING" "Markdown blotter missing disclaimer: $text"
    }
}

Require-True ([bool]$lineArchive.planLineArchiveCreated) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Line archive missing."
Require-True ([int]$lineArchive.lineCount -eq 3) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Line archive count wrong."
foreach ($line in $lineArchive.lines) {
    Require-True ([string]$line.planLineStatus -eq "PaperLineReady") "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Line status not preserved."
    Require-True ([string]$line.quantityStatus -eq "PaperSized") "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Quantity status not preserved."
    Require-True ([decimal]$line.lotSize -eq 1000) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Lot size not preserved."
    Require-True ([string]$line.quantityRoundingMode -eq "RoundToNearestLot") "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Rounding mode not preserved."
    Require-True ([string]$line.executionStyleShape -eq "MarketShapeOnly") "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Execution shape not preserved."
    Require-True ([string]$line.timeInForceShape -eq "DayShapeOnly") "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "TIF shape not preserved."
    Require-True (-not [string]::IsNullOrWhiteSpace([string]$line.paperOrderCandidateId)) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Candidate id missing."
    Require-True (-not [string]::IsNullOrWhiteSpace([string]$line.sourceRebalanceIntentId)) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Rebalance id missing."
    Require-True (-not [string]::IsNullOrWhiteSpace([string]$line.riskReviewReference)) "PMS_EMS_OMS_R014_FAIL_RISK_LINEAGE_MISSING" "Risk reference missing."
}

Require-True ([bool]$blocked.blockedLinesPreservationCreated) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Blocked-line preservation missing."
Require-True ([int]$blocked.blockedR011LineCount -eq 10) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Blocked R011 count wrong."
Require-True ([bool]$blocked.blockedR011LinesPreservedSeparately) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Blocked R011 lines not preserved separately."
Require-False ([bool]$blocked.blockedR011LinesBecomeReadyExecutionPlanLines) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Blocked R011 lines became ready plan lines."

Require-True ([bool]$nonExec.nonExecutablePlanAuditCreated) "PMS_EMS_OMS_R014_FAIL_PLAN_EXECUTABLE" "Non-executable audit missing."
foreach ($property in @("planPaperOnly", "planNonExecutable", "planNotAnOrder", "planNotSubmitted", "planNoBrokerRoute", "allLinesPaperOnly", "allLinesNonExecutable", "allLinesNotAnOrder", "allLinesNotSubmitted", "allLinesNoBrokerRoute")) {
    Require-True ([bool]$nonExec.$property) "PMS_EMS_OMS_R014_FAIL_PLAN_EXECUTABLE" "Non-executable audit missing: $property"
}
foreach ($property in @("planExecutable", "planSubmitted", "planHasBrokerRoute", "planRepresentedAsOmsOrder", "planRepresentedAsBrokerOrder")) {
    Require-False ([bool]$nonExec.$property) "PMS_EMS_OMS_R014_FAIL_PLAN_EXECUTABLE" "Non-executable audit detected: $property"
}

Require-True ([bool]$orderAudit.noOrderCreatedAuditCreated) "PMS_EMS_OMS_R014_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No-order audit missing."
foreach ($property in @("omsOrderCreated", "parentOrderCreated", "childOrderCreated", "brokerOrderCreated", "executableOrderCreated", "orderStateCreated", "orderSubmissionPathIntroduced", "ordersSubmitted", "liveTradingStateMutated", "brokerGatewayCalled")) {
    Require-False ([bool]$orderAudit.$property) "PMS_EMS_OMS_R014_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No-order audit detected: $property"
}

Require-True ([bool]$routeAudit.noRouteNoSubmissionAuditCreated) "PMS_EMS_OMS_R014_FAIL_PLAN_SUBMITTED_OR_ROUTED" "No-route/no-submission audit missing."
foreach ($property in @("brokerRouteCreated", "brokerRouteAssigned", "submissionInstructionCreated", "orderSubmissionPathInvoked", "planSubmitted", "lineSubmitted", "planRouteable", "lineRouteable")) {
    Require-False ([bool]$routeAudit.$property) "PMS_EMS_OMS_R014_FAIL_PLAN_SUBMITTED_OR_ROUTED" "Route/submission audit detected: $property"
}

Require-True ([bool]$fillAudit.noFillNoExecutionReportAuditCreated) "PMS_EMS_OMS_R014_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "No-fill/no-exec-report audit missing."
foreach ($property in @("fillCreated", "executionReportCreated", "planCreatesFillsOrExecutionReports", "lineCreatesFill", "lineCreatesExecutionReport")) {
    Require-False ([bool]$fillAudit.$property) "PMS_EMS_OMS_R014_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill/execution-report audit detected: $property"
}

Require-True ([bool]$idempotency.idempotencyEvidenceCreated) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Idempotency evidence missing."
Require-True ([string]$idempotency.idempotencyKey -eq "PaperExecutionPlanId") "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Wrong idempotency key."
Require-True ([string]$idempotency.duplicateArchiveBehavior -eq "DuplicateReturned") "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Duplicate behavior missing."
Require-False ([bool]$idempotency.duplicatesCreateAdditionalPlanRecords) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Duplicate records created."

Require-True ([bool]$risk.riskLineagePreservationCreated) "PMS_EMS_OMS_R014_FAIL_RISK_LINEAGE_MISSING" "Risk lineage missing."
Require-True ([bool]$risk.riskReviewReferencePresentOnEveryArchivedLine) "PMS_EMS_OMS_R014_FAIL_RISK_LINEAGE_MISSING" "Risk missing from archive lines."
Require-True ([bool]$risk.riskReviewReferencePresentOnEveryBlotterLine) "PMS_EMS_OMS_R014_FAIL_RISK_LINEAGE_MISSING" "Risk missing from blotter lines."
Require-True ([bool]$risk.blockedRiskResultsCarriedSeparately) "PMS_EMS_OMS_R014_FAIL_RISK_LINEAGE_MISSING" "Blocked risk results missing."
Require-False ([bool]$risk.riskLineageMissing) "PMS_EMS_OMS_R014_FAIL_RISK_LINEAGE_MISSING" "Risk lineage marked missing."

Require-True ([bool]$qubes.qubesLineagePreservationCreated) "PMS_EMS_OMS_R014_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage missing."
Require-True ([string]$qubes.sourceSystem -eq "ModelWeightSourceSystem.Qubes") "PMS_EMS_OMS_R014_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes source missing."
Require-True ([int]$qubes.cadenceMinutes -eq 15) "PMS_EMS_OMS_R014_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes cadence missing."
foreach ($property in @("qubesAuditBatchPreserved", "rawQubesRowAuditPreserved", "normalizedWeightAuditPreserved", "modelWeightBatchLinkagePreserved", "modelRunLinkagePreserved", "targetWeightLinkagePreserved")) {
    Require-True ([bool]$qubes.$property) "PMS_EMS_OMS_R014_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage missing: $property"
}
Require-False ([bool]$qubes.qubesLineageWeakened) "PMS_EMS_OMS_R014_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage weakened."

Require-True ([bool]$operatorDecision.operatorDecisionLineagePreservationCreated) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Operator lineage missing."
Require-True ([string]$operatorDecision.operatorDecisionType -eq "PromoteToPaperReady") "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Operator decision not promotion."
Require-True ([string]$operatorDecision.resultingCycleReviewStatus -eq "PaperReadyNoExternal") "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Operator decision not PaperReadyNoExternal."
Require-True ([bool]$operatorDecision.operatorDecisionReferencedOnArchive) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Operator decision missing on archive."
Require-True ([bool]$operatorDecision.operatorDecisionReferencedOnEveryBlotterLine) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Operator decision missing on blotter."
Require-False ([bool]$operatorDecision.promotionMeansLiveTrading) "PMS_EMS_OMS_R014_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Promotion means live trading."
Require-False ([bool]$operatorDecision.promotionCreatesOrders) "PMS_EMS_OMS_R014_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Promotion creates orders."

Require-True ([bool]$candidateLineage.paperCandidateLineagePreservationCreated) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Paper candidate lineage missing."
Require-True ([bool]$candidateLineage.paperOrderCandidateIdReferencedOnEveryArchivedLine) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Candidate id missing on archive."
Require-True ([bool]$candidateLineage.paperOrderCandidateIdReferencedOnEveryBlotterLine) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Candidate id missing on blotter."
Require-True ([bool]$candidateLineage.paperCandidateArchiveReferenced) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Candidate archive missing."
Require-False ([bool]$candidateLineage.blockedR011LinesBecomeReadyExecutionPlanLines) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Blocked R011 lines became ready lines."

Require-True ([bool]$rebalance.rebalanceIntentLineagePreservationCreated) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Rebalance lineage missing."
Require-True ([bool]$rebalance.sourceRebalanceIntentReferencedOnEveryArchivedLine) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Rebalance id missing on archive."
Require-True ([bool]$rebalance.sourceRebalanceIntentReferencedOnEveryBlotterLine) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Rebalance id missing on blotter."
Require-True ([bool]$rebalance.rebalanceIntentsRemainNonExecutable) "PMS_EMS_OMS_R014_FAIL_PLAN_EXECUTABLE" "Rebalance intents executable."
Require-True ([bool]$rebalance.allSourceIntentsBlockedNoOms) "PMS_EMS_OMS_R014_FAIL_PLAN_IS_OMS_OR_BROKER_ORDER" "Source intents not BlockedNoOMS."
Require-False ([bool]$rebalance.rebalanceIntentCreatesOrder) "PMS_EMS_OMS_R014_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Rebalance creates order."
Require-False ([bool]$rebalance.rebalanceIntentSubmitted) "PMS_EMS_OMS_R014_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "Rebalance submitted."

Require-True ([bool]$lotSizing.lotSizingLineagePreservationCreated) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Lot-sizing lineage missing."
foreach ($property in @("lotSizedCandidateBatchReferenced", "paperQuantityShapePreservedOnEveryArchivedLine", "paperBaseQuantityPreservedOnEveryArchivedLine", "lotSizePreservedOnEveryArchivedLine", "roundingModePreservedOnEveryArchivedLine", "quantityStatusPreservedOnEveryArchivedLine", "instrumentConventionPreservedOnEveryArchivedLine")) {
    Require-True ([bool]$lotSizing.$property) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Lot-sizing lineage missing: $property"
}

Require-True ([bool]$marks.missingStaleMarkPreservationCreated) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Missing/stale preservation missing."
Require-True ([bool]$marks.blockedMissingStaleLinesPreservedSeparately) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Blocked missing/stale rows missing."
Require-False ([bool]$marks.blockedMissingStaleLinesBecomePlanLines) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Blocked missing/stale lines became plan lines."
Require-False ([bool]$marks.missingOrStaleMarksHidden) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Missing/stale marks hidden."
Require-False ([bool]$marks.fabricatedMarksForPlanArchive) "PMS_EMS_OMS_R014_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Marks fabricated."
Require-False ([bool]$marks.rawMarketDataFixturePayloadsSerialized) "PMS_EMS_OMS_R014_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw fixture payloads serialized."

Require-True ([bool]$drift.driftAcknowledgementPreservationCreated) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Drift preservation missing."
Require-True ([string]$drift.theoreticalVsRealStatus -eq "Drift") "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Drift status missing."
Require-True ([bool]$drift.driftAcknowledgedByOperatorDecision) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Drift not acknowledged."
Require-True ([bool]$drift.driftAcknowledgementPreservedInArchive) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Drift not preserved."
Require-False ([bool]$drift.liveTradingApprovalCreated) "PMS_EMS_OMS_R014_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Live approval created."

Require-True ([bool]$universe.instrumentUniverseHandlingCreated) "PMS_EMS_OMS_R014_FAIL_LMAX_GAP_BLOCKS_PLAN_ARCHIVE" "Universe handling missing."
Require-False ([bool]$universe.lmaxReadOnlyScopeUsedAsArchiveGate) "PMS_EMS_OMS_R014_FAIL_LMAX_GAP_BLOCKS_PLAN_ARCHIVE" "LMAX scope used as archive gate."
Require-False ([bool]$universe.lmaxLiveValidationGapsBlockPlanArchive) "PMS_EMS_OMS_R014_FAIL_LMAX_GAP_BLOCKS_PLAN_ARCHIVE" "LMAX gaps block archive."
Require-False ([bool]$universe.audusdLiveValidationGapBlocksPlanArchive) "PMS_EMS_OMS_R014_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD gap blocks archive."
Require-False ([bool]$universe.usdjpyLiveValidationGapBlocksPlanArchive) "PMS_EMS_OMS_R014_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY gap blocks archive."
Require-True ([string]$universe.audusdStatus -match "not failed") "PMS_EMS_OMS_R014_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD status weakened."
Require-True ([string]$universe.usdjpyStatus -match "not failed") "PMS_EMS_OMS_R014_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY status weakened."

Require-True ([bool]$usdjpy.usdJpyCaveatPreservationCreated) "PMS_EMS_OMS_R014_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
Require-True ([string]$usdjpy.usdJpySecurityId -eq "4004") "PMS_EMS_OMS_R014_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID missing."
Require-True ([string]$usdjpy.usdJpySecurityIdSource -eq "8") "PMS_EMS_OMS_R014_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource missing."
Require-True ([bool]$usdjpy.usdJpyNotProven) "PMS_EMS_OMS_R014_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY not-proven caveat missing."
Require-False ([bool]$usdjpy.usdJpyClassifiedAsFailed) "PMS_EMS_OMS_R014_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY classified failed."
Require-True ([bool]$usdjpy.audusdTlsBoundaryInconclusive) "PMS_EMS_OMS_R014_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD TLS-boundary status missing."
Require-False ([bool]$usdjpy.audusdClassifiedAsFailed) "PMS_EMS_OMS_R014_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD classified failed."

Require-True ([bool]$lmax.lmaxReadonlyBaselineReferenceCreated) "PMS_EMS_OMS_R014_FAIL_LMAX_GAP_BLOCKS_PLAN_ARCHIVE" "LMAX baseline missing."
Require-False ([bool]$lmax.lmaxUsedInThisPhase) "PMS_EMS_OMS_R014_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX used."
Require-False ([bool]$lmax.lmaxCalledInThisPhase) "PMS_EMS_OMS_R014_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX called."
Require-False ([bool]$lmax.lmaxLiveValidationGapsBlockPlanArchive) "PMS_EMS_OMS_R014_FAIL_LMAX_GAP_BLOCKS_PLAN_ARCHIVE" "LMAX gaps block archive."
Require-False ([bool]$lmax.baseline.AUDUSD.classifiedAsFailed) "PMS_EMS_OMS_R014_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD failed."
Require-False ([bool]$lmax.baseline.USDJPY.classifiedAsFailed) "PMS_EMS_OMS_R014_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY failed."
Require-True ([string]$lmax.baseline.USDJPY.securityId -eq "4004") "PMS_EMS_OMS_R014_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID missing."
Require-True ([string]$lmax.baseline.USDJPY.securityIdSource -eq "8") "PMS_EMS_OMS_R014_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource missing."

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
    "lmaxLiveValidationGapsBlockPlanArchive"
)) {
    Require-False ([bool]$noExternal.$property) "PMS_EMS_OMS_R014_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected: $property"
}

foreach ($item in $forbidden.forbiddenActions) {
    if ([bool]$item.detected) {
        if ([string]$item.action -match "scheduler|service|timer|background") {
            Fail-Gate "PMS_EMS_OMS_R014_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "fill|execution report") {
            Fail-Gate "PMS_EMS_OMS_R014_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "order|trading|executable|OMS|parent|child|broker|submitted|routed") {
            Fail-Gate "PMS_EMS_OMS_R014_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden action detected: $($item.action)"
        }

        Fail-Gate "PMS_EMS_OMS_R014_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $($item.action)"
    }
}

Require-True ([bool]$nextPhase.nextPhaseRecommendationCreated) "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Next phase missing."
Require-True ([string]$nextPhase.recommendedNextPhase -eq "PMS-EMS-OMS-R015") "PMS_EMS_OMS_R014_FAIL_PAPER_EXECUTION_PLAN_ARCHIVE_MISSING" "Next phase is not R015."
Require-True ([bool]$nextPhase.mustRemainNoExternal) "PMS_EMS_OMS_R014_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase not no-external."
Require-True ([bool]$nextPhase.mustNotCreateExecutableOrders) "PMS_EMS_OMS_R014_FAIL_EXECUTABLE_ORDER_CREATED" "Next phase permits executable orders."
Require-True ([bool]$nextPhase.mustNotCreateOmsOrders) "PMS_EMS_OMS_R014_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Next phase permits OMS orders."
Require-True ([bool]$nextPhase.mustNotCreateBrokerOrders) "PMS_EMS_OMS_R014_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Next phase permits broker orders."
Require-True ([bool]$nextPhase.mustNotSubmitOrders) "PMS_EMS_OMS_R014_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "Next phase permits submission."
Require-True ([bool]$nextPhase.mustNotCreateFillsOrExecutionReports) "PMS_EMS_OMS_R014_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Next phase permits fills/execution reports."

$apiSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Api/appsettings.json") "PMS_EMS_OMS_R014_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$workerSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/appsettings.json") "PMS_EMS_OMS_R014_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-False ([bool]$apiSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R014_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API live trading enabled."
Require-False ([bool]$workerSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R014_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker live trading enabled."
Require-False ([bool]$apiSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R014_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API external enabled."
Require-False ([bool]$workerSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R014_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker external enabled."
Require-True ([bool]$apiSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R014_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API fake gateway not required."
Require-True ([bool]$workerSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R014_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker fake gateway not required."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.Enabled) "PMS_EMS_OMS_R014_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX runtime enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowExternalConnections) "PMS_EMS_OMS_R014_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX external enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowOrderSubmission) "PMS_EMS_OMS_R014_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "LMAX order submission enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SchedulerEnabled) "PMS_EMS_OMS_R014_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "LMAX scheduler enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SubmitToShadowReplay) "PMS_EMS_OMS_R014_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Shadow replay enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.PersistRawFixMessages) "PMS_EMS_OMS_R014_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw FIX persistence enabled."
Require-False ([bool]$workerSettings.MarketDataBars.Enabled) "PMS_EMS_OMS_R014_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker market data bars enabled."

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-pms-ems-oms-r014-*" -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combined = [string]::Join("`n", $artifactText)
foreach ($pattern in @("\u0001", "35=", "MDReqID\s*[:=]", "SenderCompID\s*[:=]", "TargetCompID\s*[:=]", "BeginString\s*[:=]", "SocketHost\s*[:=]", "TlsHost\s*[:=]", "Password\s*[:=]", "ApiKey\s*[:=]", "Bearer\s+[A-Za-z0-9_\.-]+", "rawBid", "rawAsk", "rawMid")) {
    if ($combined -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R014_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Unsafe serialized content pattern found: $pattern"
    }
}

$requiredFiles = @(
    "src/QQ.Production.Intraday.Application/QubesPaperExecutionPlanArchive.cs",
    "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperExecutionPlanArchiveTests.cs"
)
foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $file))) {
        Fail-Gate "PMS_EMS_OMS_R014_FAIL_BUILD_OR_TESTS" "Required file missing: $file"
    }
}

$source = Get-Content -LiteralPath (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/QubesPaperExecutionPlanArchive.cs") -Raw
foreach ($pattern in @("TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "SendOrderAsync", "SubmitOrder", "FixSession")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R014_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "R014 source contains forbidden runtime pattern: $pattern"
    }
}
foreach ($pattern in @("AddHostedService", "IHostedService", "BackgroundService", "PeriodicTimer", "Task.Delay", "System.Threading.Timer")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R014_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "R014 source contains scheduler/service pattern: $pattern"
    }
}

$tests = Get-Content -LiteralPath (Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperExecutionPlanArchiveTests.cs") -Raw
foreach ($requiredTestName in @(
    "R013_paper_execution_plan_can_be_archived_no_externally",
    "Plan_archive_preserves_cycle_run_id_and_qubes_run_id",
    "Plan_archive_preserves_operator_decision_id",
    "Plan_archive_preserves_paper_candidate_batch_id",
    "Plan_lines_preserve_source_paper_candidate_ids",
    "Plan_lines_preserve_source_rebalance_intent_ids",
    "Plan_lines_preserve_risk_review_references",
    "Plan_lines_preserve_lot_sizing_metadata",
    "Audusd_buy_line_appears_in_blotter",
    "Eurusd_buy_line_appears_in_blotter",
    "Gbpusd_sell_line_appears_in_blotter",
    "Blocked_r011_lines_are_preserved_separately",
    "Blotter_includes_no_order_no_route_no_submission_no_fill_no_execution_report_disclaimer",
    "Plan_remains_paper_only",
    "Plan_remains_non_executable",
    "Plan_remains_not_an_order_not_submitted_and_no_broker_route",
    "Duplicate_plan_archive_is_idempotent",
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
        Fail-Gate "PMS_EMS_OMS_R014_FAIL_BUILD_OR_TESTS" "Focused test missing: $requiredTestName"
    }
}

Require-True ([string]$evidence.build.status -eq "PASS") "PMS_EMS_OMS_R014_FAIL_BUILD_OR_TESTS" "Build evidence missing or not PASS."
Require-True ([int]$evidence.build.failed -eq 0) "PMS_EMS_OMS_R014_FAIL_BUILD_OR_TESTS" "Build evidence has failures."
Require-True ([string]$evidence.focusedTests.status -eq "PASS") "PMS_EMS_OMS_R014_FAIL_BUILD_OR_TESTS" "Focused test evidence missing or not PASS."
Require-True ([int]$evidence.focusedTests.failed -eq 0) "PMS_EMS_OMS_R014_FAIL_BUILD_OR_TESTS" "Focused tests have failures."
Require-True ([string]$evidence.unitTests.status -eq "PASS") "PMS_EMS_OMS_R014_FAIL_BUILD_OR_TESTS" "Unit test evidence missing or not PASS."
Require-True ([int]$evidence.unitTests.failed -eq 0) "PMS_EMS_OMS_R014_FAIL_BUILD_OR_TESTS" "Unit tests have failures."
Require-True ([string]$evidence.validator.status -eq "PASS") "PMS_EMS_OMS_R014_FAIL_BUILD_OR_TESTS" "Validator evidence missing or not PASS."
Require-True ([int]$evidence.validator.failed -eq 0) "PMS_EMS_OMS_R014_FAIL_BUILD_OR_TESTS" "Validator evidence has failures."
Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R014_FAIL_BUILD_OR_TESTS" "Evidence marker missing."

Write-Host "PMS_EMS_OMS_R014_PASS_PAPER_EXECUTION_PLAN_ARCHIVE_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R014_PASS_OPERATOR_PLAN_BLOTTER_READY_NO_EXTERNAL"
