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

function Require-ArrayContains {
    param($Array, [string]$Value, [string]$Classification, [string]$Message)
    if (@($Array | Where-Object { [string]$_ -eq $Value }).Count -eq 0) {
        Fail-Gate $Classification $Message
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repoRoot $ArtifactDirectory

$requiredArtifacts = @{
    "phase-pms-ems-oms-r023-summary.md" = "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r023-paper-ledger-preview-archive-contract.json" = "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r023-paper-ledger-preview-archive.json" = "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r023-paper-ledger-preview-report.md" = "PMS_EMS_OMS_R023_FAIL_OPERATOR_LEDGER_REPORT_MISSING"
    "phase-pms-ems-oms-r023-paper-ledger-preview-report.json" = "PMS_EMS_OMS_R023_FAIL_OPERATOR_LEDGER_REPORT_MISSING"
    "phase-pms-ems-oms-r023-paper-ledger-preview-lines.json" = "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r023-paper-ledger-commit-readiness-gate.json" = "PMS_EMS_OMS_R023_FAIL_COMMIT_READINESS_GATE_MISSING"
    "phase-pms-ems-oms-r023-operator-decisions.json" = "PMS_EMS_OMS_R023_FAIL_COMMIT_READINESS_GATE_MISSING"
    "phase-pms-ems-oms-r023-hold-decision-example.json" = "PMS_EMS_OMS_R023_FAIL_COMMIT_READINESS_GATE_MISSING"
    "phase-pms-ems-oms-r023-approve-paper-ledger-commit-readiness-example.json" = "PMS_EMS_OMS_R023_FAIL_COMMIT_READINESS_GATE_MISSING"
    "phase-pms-ems-oms-r023-no-paper-ledger-commit-audit.json" = "PMS_EMS_OMS_R023_FAIL_PAPER_LEDGER_COMMIT_OCCURRED"
    "phase-pms-ems-oms-r023-no-live-position-mutation-audit.json" = "PMS_EMS_OMS_R023_FAIL_LIVE_POSITION_MUTATION"
    "phase-pms-ems-oms-r023-no-broker-position-mutation-audit.json" = "PMS_EMS_OMS_R023_FAIL_BROKER_POSITION_MUTATION"
    "phase-pms-ems-oms-r023-no-production-ledger-mutation-audit.json" = "PMS_EMS_OMS_R023_FAIL_PRODUCTION_LEDGER_MUTATION"
    "phase-pms-ems-oms-r023-no-trading-state-mutation-audit.json" = "PMS_EMS_OMS_R023_FAIL_TRADING_STATE_MUTATION"
    "phase-pms-ems-oms-r023-no-fill-no-execution-report-audit.json" = "PMS_EMS_OMS_R023_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
    "phase-pms-ems-oms-r023-no-order-created-audit.json" = "PMS_EMS_OMS_R023_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r023-no-route-no-submission-audit.json" = "PMS_EMS_OMS_R023_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED"
    "phase-pms-ems-oms-r023-idempotency-evidence.json" = "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r023-risk-lineage-preservation.json" = "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r023-qubes-lineage-preservation.json" = "PMS_EMS_OMS_R023_FAIL_QUBES_LINEAGE_WEAKENED"
    "phase-pms-ems-oms-r023-operator-decision-lineage-preservation.json" = "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r023-position-preview-lineage-preservation.json" = "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r023-ledger-preview-lineage-preservation.json" = "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r023-simulation-result-lineage-preservation.json" = "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r023-plan-lineage-preservation.json" = "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r023-paper-candidate-lineage-preservation.json" = "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r023-rebalance-intent-lineage-preservation.json" = "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r023-lot-sizing-lineage-preservation.json" = "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r023-instrument-universe-handling.json" = "PMS_EMS_OMS_R023_FAIL_LMAX_GAP_BLOCKS_LEDGER_REPORT"
    "phase-pms-ems-oms-r023-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R023_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r023-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R023_FAIL_LMAX_GAP_BLOCKS_LEDGER_REPORT"
    "phase-pms-ems-oms-r023-no-external-audit.json" = "PMS_EMS_OMS_R023_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r023-forbidden-actions-audit.json" = "PMS_EMS_OMS_R023_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r023-next-phase-recommendation.json" = "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r023-build-test-validator-evidence.json" = "PMS_EMS_OMS_R023_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $entry.Key))) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$contract = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-paper-ledger-preview-archive-contract.json") "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING"
$archive = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-paper-ledger-preview-archive.json") "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING"
$report = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-paper-ledger-preview-report.json") "PMS_EMS_OMS_R023_FAIL_OPERATOR_LEDGER_REPORT_MISSING"
$lines = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-paper-ledger-preview-lines.json") "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING"
$gate = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-paper-ledger-commit-readiness-gate.json") "PMS_EMS_OMS_R023_FAIL_COMMIT_READINESS_GATE_MISSING"
$decisions = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-operator-decisions.json") "PMS_EMS_OMS_R023_FAIL_COMMIT_READINESS_GATE_MISSING"
$hold = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-hold-decision-example.json") "PMS_EMS_OMS_R023_FAIL_COMMIT_READINESS_GATE_MISSING"
$approve = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-approve-paper-ledger-commit-readiness-example.json") "PMS_EMS_OMS_R023_FAIL_COMMIT_READINESS_GATE_MISSING"
$noCommitAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-no-paper-ledger-commit-audit.json") "PMS_EMS_OMS_R023_FAIL_PAPER_LEDGER_COMMIT_OCCURRED"
$livePositionAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-no-live-position-mutation-audit.json") "PMS_EMS_OMS_R023_FAIL_LIVE_POSITION_MUTATION"
$brokerPositionAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-no-broker-position-mutation-audit.json") "PMS_EMS_OMS_R023_FAIL_BROKER_POSITION_MUTATION"
$productionLedgerAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-no-production-ledger-mutation-audit.json") "PMS_EMS_OMS_R023_FAIL_PRODUCTION_LEDGER_MUTATION"
$tradingAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-no-trading-state-mutation-audit.json") "PMS_EMS_OMS_R023_FAIL_TRADING_STATE_MUTATION"
$fillAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-no-fill-no-execution-report-audit.json") "PMS_EMS_OMS_R023_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$orderAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-no-order-created-audit.json") "PMS_EMS_OMS_R023_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$routeAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-no-route-no-submission-audit.json") "PMS_EMS_OMS_R023_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED"
$idempotency = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-idempotency-evidence.json") "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING"
$risk = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-risk-lineage-preservation.json") "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING"
$qubes = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-qubes-lineage-preservation.json") "PMS_EMS_OMS_R023_FAIL_QUBES_LINEAGE_WEAKENED"
$operatorLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-operator-decision-lineage-preservation.json") "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING"
$positionPreviewLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-position-preview-lineage-preservation.json") "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING"
$ledgerPreviewLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-ledger-preview-lineage-preservation.json") "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING"
$simulationLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-simulation-result-lineage-preservation.json") "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING"
$planLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-plan-lineage-preservation.json") "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING"
$candidateLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-paper-candidate-lineage-preservation.json") "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING"
$rebalanceLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-rebalance-intent-lineage-preservation.json") "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING"
$lotSizingLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-lot-sizing-lineage-preservation.json") "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-instrument-universe-handling.json") "PMS_EMS_OMS_R023_FAIL_LMAX_GAP_BLOCKS_LEDGER_REPORT"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R023_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R023_FAIL_LMAX_GAP_BLOCKS_LEDGER_REPORT"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-no-external-audit.json") "PMS_EMS_OMS_R023_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-forbidden-actions-audit.json") "PMS_EMS_OMS_R023_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$nextPhase = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-next-phase-recommendation.json") "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r023-build-test-validator-evidence.json") "PMS_EMS_OMS_R023_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$contract.paperLedgerPreviewArchiveContractCreated) "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING" "Ledger preview archive contract missing."
foreach ($value in @("ArchivedNoExternal", "DuplicateReturned", "RejectedInvalidPreview", "InconclusiveSafe")) {
    Require-ArrayContains $contract.archiveStatuses $value "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING" "Archive status missing: $value"
}
foreach ($value in @("ApprovePaperLedgerCommitReadiness", "Hold", "Reject", "RequestLedgerFix", "RequestRiskReview")) {
    Require-ArrayContains $contract.decisionTypes $value "PMS_EMS_OMS_R023_FAIL_COMMIT_READINESS_GATE_MISSING" "Decision type missing: $value"
}
foreach ($value in @("Recorded", "RejectedByGate", "DuplicateReturned", "InconclusiveSafe")) {
    Require-ArrayContains $contract.decisionStatuses $value "PMS_EMS_OMS_R023_FAIL_COMMIT_READINESS_GATE_MISSING" "Decision status missing: $value"
}
foreach ($value in @("PaperLedgerCommitReadyNoExternal", "HeldForRiskReview", "HeldForMissingMarks", "HeldForDrift", "HeldForBlockedLines", "Rejected", "InconclusiveSafe")) {
    Require-ArrayContains $contract.readinessStatuses $value "PMS_EMS_OMS_R023_FAIL_COMMIT_READINESS_GATE_MISSING" "Readiness status missing: $value"
}
foreach ($property in @("requiresNoPaperLedgerCommit", "requiresNoLivePositionMutation", "requiresNoBrokerPositionMutation", "requiresNoProductionLedgerMutation", "requiresNoTradingStateMutation", "requiresNoFillCreated", "requiresNoExecutionReportCreated", "requiresNoOrderCreated", "requiresNoBrokerRoute", "requiresNotSubmitted")) {
    Require-True ([bool]$contract.$property) "PMS_EMS_OMS_R023_FAIL_COMMIT_READINESS_GATE_MISSING" "Contract safety flag missing: $property"
}

Require-True ([bool]$archive.paperLedgerPreviewArchiveCreated) "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING" "Ledger preview archive missing."
Require-True ([string]$archive.archiveStatus -eq "ArchivedNoExternal") "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING" "Archive status wrong."
Require-True ([string]$archive.safetyStatus -eq "PaperLedgerPreviewOnly") "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING" "Archive safety status wrong."
Require-True ([int]$archive.previewLineCount -eq 3) "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING" "Archive preview line count wrong."
Require-True ([int]$archive.blockedLineCount -eq 10) "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING" "Archive blocked line count wrong."
foreach ($property in @("paperOnly", "simulatedOnly", "previewOnly", "noExternal", "noPaperLedgerCommit", "noLivePositionMutation", "noBrokerPositionMutation", "noProductionLedgerMutation", "noTradingStateMutation", "noFillCreated", "noExecutionReportCreated", "noOrderCreated", "noBrokerRoute", "notSubmitted")) {
    Require-True ([bool]$archive.$property) "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING" "Archive safety flag missing: $property"
}
foreach ($property in @("paperLedgerStateCommitted", "livePositionStateMutated", "brokerPositionStateMutated", "productionLedgerStateMutated", "tradingStateMutated", "fillCreated", "executionReportCreated", "omsOrderCreated", "parentOrderCreated", "childOrderCreated", "brokerOrderCreated", "orderStateCreated", "submittedOrders", "brokerRouteCreated")) {
    Require-False ([bool]$archive.$property) "PMS_EMS_OMS_R023_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Archive unsafe flag detected: $property"
}
foreach ($property in @("livePositionMutationCount", "brokerPositionMutationCount", "productionLedgerMutationCount", "tradingStateMutationCount")) {
    Require-True ([int]$archive.$property -eq 0) "PMS_EMS_OMS_R023_FAIL_TRADING_STATE_MUTATION" "Archive mutation count nonzero: $property"
}

Require-True ([bool]$lines.paperLedgerPreviewLinesArchived) "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING" "Ledger preview lines missing."
Require-True ([int]$lines.lineCount -eq 3) "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING" "Ledger preview line count wrong."
Require-True (@($lines.lines | Where-Object { $_.currencyOrSymbol -eq "AUDUSD" -and [decimal]$_.simulatedDeltaQuantity -eq 131000 -and [decimal]$_.previewEndingPaperQuantity -eq 131000 -and $_.quantityCurrency -eq "AUD" }).Count -eq 1) "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING" "AUDUSD ledger archive line missing."
Require-True (@($lines.lines | Where-Object { $_.currencyOrSymbol -eq "EURUSD" -and [decimal]$_.simulatedDeltaQuantity -eq 124000 -and [decimal]$_.previewEndingPaperQuantity -eq 124000 -and $_.quantityCurrency -eq "EUR" }).Count -eq 1) "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING" "EURUSD ledger archive line missing."
Require-True (@($lines.lines | Where-Object { $_.currencyOrSymbol -eq "GBPUSD" -and [decimal]$_.simulatedDeltaQuantity -eq -368000 -and [decimal]$_.previewEndingPaperQuantity -eq -368000 -and $_.quantityCurrency -eq "GBP" }).Count -eq 1) "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING" "GBPUSD ledger archive line missing."
foreach ($line in $lines.lines) {
    foreach ($property in @("paperOnly", "previewOnly", "noPaperLedgerCommit", "noLivePositionMutation", "noBrokerPositionMutation", "noProductionLedgerMutation", "noTradingStateMutation", "noFillCreated", "noExecutionReportCreated", "noOrderCreated", "noBrokerRoute", "notSubmitted")) {
        Require-True ([bool]$line.$property) "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING" "Line safety flag missing: $property"
    }
}

Require-True ([bool]$report.paperLedgerPreviewReportCreated) "PMS_EMS_OMS_R023_FAIL_OPERATOR_LEDGER_REPORT_MISSING" "Operator ledger report missing."
foreach ($property in @("includesPaperLedgerPreviewOnlyDisclaimer", "includesSimulatedOnlyDisclaimer", "includesNoPaperLedgerCommitDisclaimer", "includesNoLivePositionMutationDisclaimer", "includesNoBrokerPositionMutationDisclaimer", "includesNoProductionLedgerMutationDisclaimer", "includesNoTradingStateMutationDisclaimer", "includesNoFillDisclaimer", "includesNoExecutionReportDisclaimer", "includesNoOrderDisclaimer", "includesNoBrokerRouteDisclaimer", "includesNoSubmissionDisclaimer")) {
    Require-True ([bool]$report.$property) "PMS_EMS_OMS_R023_FAIL_OPERATOR_LEDGER_REPORT_MISSING" "Report disclaimer missing: $property"
}
$reportMarkdown = Get-Content -LiteralPath (Join-Path $artifactRoot "phase-pms-ems-oms-r023-paper-ledger-preview-report.md") -Raw
foreach ($phrase in @("paper ledger preview only", "simulated-only", "No paper ledger commit yet", "No live position mutation", "No broker position mutation", "No production ledger mutation", "No trading-state mutation", "No fills", "No execution reports", "No orders", "No broker routes", "No submissions")) {
    if ($reportMarkdown -notmatch [regex]::Escape($phrase)) {
        Fail-Gate "PMS_EMS_OMS_R023_FAIL_OPERATOR_LEDGER_REPORT_MISSING" "Markdown report disclaimer missing: $phrase"
    }
}

Require-True ([bool]$gate.paperLedgerCommitReadinessGateCreated) "PMS_EMS_OMS_R023_FAIL_COMMIT_READINESS_GATE_MISSING" "Commit readiness gate missing."
Require-True ([bool]$gate.previewArchived) "PMS_EMS_OMS_R023_FAIL_COMMIT_READINESS_GATE_MISSING" "Gate does not require archived preview."
Require-True ([string]$gate.safetyStatus -eq "PaperLedgerPreviewOnly") "PMS_EMS_OMS_R023_FAIL_COMMIT_READINESS_GATE_MISSING" "Gate safety status wrong."
Require-True ([string]$gate.resultingReadinessStatus -eq "PaperLedgerCommitReadyNoExternal") "PMS_EMS_OMS_R023_FAIL_COMMIT_READINESS_GATE_MISSING" "Readiness status wrong."
foreach ($property in @("livePositionMutationCount", "brokerPositionMutationCount", "productionLedgerMutationCount", "tradingStateMutationCount", "fillCount", "executionReportCount", "orderCount", "brokerRouteCount", "submissionCount")) {
    Require-True ([int]$gate.$property -eq 0) "PMS_EMS_OMS_R023_FAIL_COMMIT_READINESS_GATE_MISSING" "Gate count is nonzero: $property"
}
foreach ($property in @("blockedLinesAcknowledged", "missingStaleMarksAcknowledged", "driftAcknowledged")) {
    Require-True ([bool]$gate.$property) "PMS_EMS_OMS_R023_FAIL_COMMIT_READINESS_GATE_MISSING" "Acknowledgement missing at gate: $property"
}
foreach ($property in @("approvalCommitsPaperLedgerState", "approvalMutatesLivePositions", "approvalMutatesBrokerPositions", "approvalMutatesProductionLedgerState", "approvalMutatesTradingState")) {
    Require-False ([bool]$gate.$property) "PMS_EMS_OMS_R023_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Approval unsafe flag detected: $property"
}

Require-True ([bool]$decisions.operatorDecisionsCreated) "PMS_EMS_OMS_R023_FAIL_COMMIT_READINESS_GATE_MISSING" "Operator decisions missing."
$holdDecision = @($decisions.decisions | Where-Object { $_.decisionType -eq "Hold" -and $_.resultingReadinessStatus -eq "HeldForBlockedLines" })[0]
$approveDecision = @($decisions.decisions | Where-Object { $_.decisionType -eq "ApprovePaperLedgerCommitReadiness" -and $_.resultingReadinessStatus -eq "PaperLedgerCommitReadyNoExternal" })[0]
Require-True ($null -ne $holdDecision) "PMS_EMS_OMS_R023_FAIL_COMMIT_READINESS_GATE_MISSING" "Hold decision missing."
Require-True ($null -ne $approveDecision) "PMS_EMS_OMS_R023_FAIL_COMMIT_READINESS_GATE_MISSING" "ApprovePaperLedgerCommitReadiness decision missing."
foreach ($decision in @($holdDecision, $approveDecision)) {
    Require-True ([string]$decision.decisionStatus -eq "Recorded") "PMS_EMS_OMS_R023_FAIL_COMMIT_READINESS_GATE_MISSING" "Decision not recorded."
    Require-True ([bool]$decision.noPaperLedgerCommit) "PMS_EMS_OMS_R023_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Decision may commit paper ledger state."
    foreach ($property in @("createsFill", "createsExecutionReport", "createsOrder", "submitsOrders", "mutatesLivePositionState", "mutatesBrokerPositionState", "mutatesProductionLedgerState", "mutatesTradingState")) {
        Require-False ([bool]$decision.$property) "PMS_EMS_OMS_R023_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Decision unsafe flag detected: $property"
    }
}
Require-True ([bool]$approveDecision.blockedLinesAcknowledged) "PMS_EMS_OMS_R023_FAIL_COMMIT_READINESS_GATE_MISSING" "Approve missing blocked-line acknowledgement."
Require-True ([bool]$approveDecision.missingStaleMarksAcknowledged) "PMS_EMS_OMS_R023_FAIL_COMMIT_READINESS_GATE_MISSING" "Approve missing missing/stale acknowledgement."
Require-True ([bool]$approveDecision.driftAcknowledged) "PMS_EMS_OMS_R023_FAIL_COMMIT_READINESS_GATE_MISSING" "Approve missing drift acknowledgement."
Require-True ([string]$hold.decisionType -eq "Hold") "PMS_EMS_OMS_R023_FAIL_COMMIT_READINESS_GATE_MISSING" "Hold example missing."
Require-True ([string]$approve.decisionType -eq "ApprovePaperLedgerCommitReadiness") "PMS_EMS_OMS_R023_FAIL_COMMIT_READINESS_GATE_MISSING" "Approve example missing."
Require-False ([bool]$approve.paperLedgerStateCommitted) "PMS_EMS_OMS_R023_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Approve example commits paper ledger state."

Require-True ([bool]$noCommitAudit.noPaperLedgerCommitAuditCreated) "PMS_EMS_OMS_R023_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "No-paper-ledger-commit audit missing."
foreach ($property in @("paperLedgerStateCommitted", "approvalCommitsPaperLedgerState", "archiveCommitsPaperLedgerState", "reportCommitsPaperLedgerState")) {
    Require-False ([bool]$noCommitAudit.$property) "PMS_EMS_OMS_R023_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Paper ledger commit audit detected: $property"
}
Require-True ([bool]$livePositionAudit.noLivePositionMutationAuditCreated) "PMS_EMS_OMS_R023_FAIL_LIVE_POSITION_MUTATION" "No-live-position audit missing."
Require-False ([bool]$livePositionAudit.livePositionStateMutated) "PMS_EMS_OMS_R023_FAIL_LIVE_POSITION_MUTATION" "Live position mutated."
Require-False ([bool]$livePositionAudit.approvalMutatesLivePositions) "PMS_EMS_OMS_R023_FAIL_LIVE_POSITION_MUTATION" "Approval mutates live positions."
Require-True ([bool]$brokerPositionAudit.noBrokerPositionMutationAuditCreated) "PMS_EMS_OMS_R023_FAIL_BROKER_POSITION_MUTATION" "No-broker-position audit missing."
Require-False ([bool]$brokerPositionAudit.brokerPositionStateMutated) "PMS_EMS_OMS_R023_FAIL_BROKER_POSITION_MUTATION" "Broker position mutated."
Require-False ([bool]$brokerPositionAudit.approvalMutatesBrokerPositions) "PMS_EMS_OMS_R023_FAIL_BROKER_POSITION_MUTATION" "Approval mutates broker positions."
Require-True ([bool]$productionLedgerAudit.noProductionLedgerMutationAuditCreated) "PMS_EMS_OMS_R023_FAIL_PRODUCTION_LEDGER_MUTATION" "No-production-ledger audit missing."
Require-False ([bool]$productionLedgerAudit.productionLedgerStateMutated) "PMS_EMS_OMS_R023_FAIL_PRODUCTION_LEDGER_MUTATION" "Production ledger mutated."
Require-False ([bool]$productionLedgerAudit.persistedProductionLedgerStateMutated) "PMS_EMS_OMS_R023_FAIL_PRODUCTION_LEDGER_MUTATION" "Persisted production ledger mutated."
Require-False ([bool]$productionLedgerAudit.approvalMutatesProductionLedgerState) "PMS_EMS_OMS_R023_FAIL_PRODUCTION_LEDGER_MUTATION" "Approval mutates production ledger."
Require-True ([bool]$tradingAudit.noTradingStateMutationAuditCreated) "PMS_EMS_OMS_R023_FAIL_TRADING_STATE_MUTATION" "No-trading-state audit missing."
Require-False ([bool]$tradingAudit.tradingStateMutated) "PMS_EMS_OMS_R023_FAIL_TRADING_STATE_MUTATION" "Trading state mutated."
Require-False ([bool]$tradingAudit.approvalMutatesTradingState) "PMS_EMS_OMS_R023_FAIL_TRADING_STATE_MUTATION" "Approval mutates trading state."
Require-True ([bool]$fillAudit.noFillNoExecutionReportAuditCreated) "PMS_EMS_OMS_R023_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill/report audit missing."
foreach ($property in @("fillCreated", "executionReportCreated", "simulationFillCreatedAsRealFill", "brokerExecutionReportCreated", "ledgerPreviewArchiveCreatesFillOrExecutionReport", "approvalCreatesFillOrExecutionReport")) {
    Require-False ([bool]$fillAudit.$property) "PMS_EMS_OMS_R023_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill/report audit detected: $property"
}
Require-True ([bool]$orderAudit.noOrderCreatedAuditCreated) "PMS_EMS_OMS_R023_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No-order audit missing."
foreach ($property in @("omsOrderCreated", "parentOrderCreated", "childOrderCreated", "brokerOrderCreated", "executableOrderCreated", "orderStateCreated", "orderSubmissionPathIntroduced", "ordersSubmitted", "ledgerPreviewArchiveCreatesOrder", "approvalCreatesOrder")) {
    Require-False ([bool]$orderAudit.$property) "PMS_EMS_OMS_R023_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order audit detected: $property"
}
Require-True ([bool]$routeAudit.noRouteNoSubmissionAuditCreated) "PMS_EMS_OMS_R023_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "No-route/no-submission audit missing."
foreach ($property in @("brokerRouteCreated", "brokerRouteAssigned", "submissionInstructionCreated", "orderSubmissionPathInvoked", "ledgerPreviewArchiveSubmitted", "ledgerPreviewArchiveRouteable", "approvalSubmitted", "approvalRouteable")) {
    Require-False ([bool]$routeAudit.$property) "PMS_EMS_OMS_R023_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "Route/submission audit detected: $property"
}

Require-True ([bool]$idempotency.idempotencyEvidenceCreated) "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING" "Idempotency missing."
Require-True ([string]$idempotency.archiveIdempotencyKey -eq "PaperPositionLedgerPreviewId") "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING" "Wrong archive idempotency key."
Require-True ([string]$idempotency.decisionIdempotencyKey -eq "OperatorDecisionId") "PMS_EMS_OMS_R023_FAIL_COMMIT_READINESS_GATE_MISSING" "Wrong decision idempotency key."
Require-True ([string]$idempotency.duplicateArchiveBehavior -eq "DuplicateReturned") "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING" "Duplicate archive behavior missing."
Require-True ([string]$idempotency.duplicateDecisionBehavior -eq "DuplicateReturned") "PMS_EMS_OMS_R023_FAIL_COMMIT_READINESS_GATE_MISSING" "Duplicate decision behavior missing."
Require-False ([bool]$idempotency.duplicatesCreateAdditionalArchivesOrDecisions) "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING" "Duplicate archives/decisions created."

Require-True ([bool]$risk.riskLineagePreservationCreated) "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING" "Risk lineage missing."
Require-False ([bool]$risk.riskLineageMissing) "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING" "Risk lineage marked missing."
Require-True ([bool]$qubes.qubesLineagePreservationCreated) "PMS_EMS_OMS_R023_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage missing."
Require-True ([string]$qubes.sourceSystem -eq "ModelWeightSourceSystem.Qubes") "PMS_EMS_OMS_R023_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes source missing."
Require-True ([int]$qubes.cadenceMinutes -eq 15) "PMS_EMS_OMS_R023_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes cadence missing."
Require-False ([bool]$qubes.qubesLineageWeakened) "PMS_EMS_OMS_R023_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage weakened."
foreach ($lineage in @($operatorLineage, $positionPreviewLineage, $ledgerPreviewLineage, $simulationLineage, $planLineage, $candidateLineage, $rebalanceLineage, $lotSizingLineage)) {
    $created = $lineage.PSObject.Properties.Name | Where-Object { $_ -match "Created$" } | Select-Object -First 1
    if ($created) { Require-True ([bool]$lineage.$created) "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING" "Lineage artifact missing: $created" }
    $missing = $lineage.PSObject.Properties.Name | Where-Object { $_ -match "Missing$" } | Select-Object -First 1
    if ($missing) { Require-False ([bool]$lineage.$missing) "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING" "Lineage marked missing: $missing" }
}
Require-True ([bool]$rebalanceLineage.rebalanceIntentsRemainNonExecutable) "PMS_EMS_OMS_R023_FAIL_EXECUTABLE_ORDER_CREATED" "Rebalance intents executable."
Require-False ([bool]$rebalanceLineage.rebalanceIntentCreatesOrder) "PMS_EMS_OMS_R023_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Rebalance intent creates order."

Require-True ([bool]$universe.instrumentUniverseHandlingCreated) "PMS_EMS_OMS_R023_FAIL_LMAX_GAP_BLOCKS_LEDGER_REPORT" "Instrument universe handling missing."
Require-False ([bool]$universe.lmaxLiveValidationGapsBlockPaperLedgerPreviewArchiveReport) "PMS_EMS_OMS_R023_FAIL_LMAX_GAP_BLOCKS_LEDGER_REPORT" "LMAX gaps block ledger report."
Require-False ([bool]$universe.audusdClassifiedAsFailed) "PMS_EMS_OMS_R023_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD classified as failed."
Require-False ([bool]$universe.audusdLiveValidationGapBlocksPaperLedgerPreviewArchiveReport) "PMS_EMS_OMS_R023_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD gap blocks ledger report."
Require-False ([bool]$universe.usdjpyLiveValidationGapBlocksPaperLedgerPreviewArchiveReport) "PMS_EMS_OMS_R023_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY gap blocks ledger report."
Require-True ([bool]$usdjpy.usdJpyCaveatPreservationCreated) "PMS_EMS_OMS_R023_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
Require-True ([string]$usdjpy.usdJpySecurityId -eq "4004") "PMS_EMS_OMS_R023_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID missing."
Require-True ([string]$usdjpy.usdJpySecurityIdSource -eq "8") "PMS_EMS_OMS_R023_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource missing."
Require-False ([bool]$usdjpy.usdJpyClassifiedAsFailed) "PMS_EMS_OMS_R023_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY classified as failed."
Require-False ([bool]$usdjpy.audusdClassifiedAsFailed) "PMS_EMS_OMS_R023_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD classified as failed."
Require-True ([bool]$lmax.lmaxReadonlyBaselineReferenceCreated) "PMS_EMS_OMS_R023_FAIL_LMAX_GAP_BLOCKS_LEDGER_REPORT" "LMAX baseline missing."
Require-False ([bool]$lmax.lmaxUsedInThisPhase) "PMS_EMS_OMS_R023_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX used."
Require-False ([bool]$lmax.lmaxCalledInThisPhase) "PMS_EMS_OMS_R023_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX called."
Require-False ([bool]$lmax.lmaxLiveValidationGapsBlockPaperLedgerPreviewArchiveReport) "PMS_EMS_OMS_R023_FAIL_LMAX_GAP_BLOCKS_LEDGER_REPORT" "LMAX gaps block ledger report."

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
    "productionLedgerStateMutated",
    "paperLedgerStateCommitted",
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
    "lmaxLiveValidationGapsBlockPaperLedgerPreviewArchiveReport"
)) {
    Require-False ([bool]$noExternal.$property) "PMS_EMS_OMS_R023_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected: $property"
}

foreach ($item in $forbidden.forbiddenActions) {
    if ([bool]$item.detected) {
        if ([string]$item.action -match "scheduler|service|timer|background") {
            Fail-Gate "PMS_EMS_OMS_R023_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "paper ledger state commit") {
            Fail-Gate "PMS_EMS_OMS_R023_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "production ledger") {
            Fail-Gate "PMS_EMS_OMS_R023_FAIL_PRODUCTION_LEDGER_MUTATION" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "position state") {
            if ([string]$item.action -match "broker") { Fail-Gate "PMS_EMS_OMS_R023_FAIL_BROKER_POSITION_MUTATION" "Forbidden action detected: $($item.action)" }
            Fail-Gate "PMS_EMS_OMS_R023_FAIL_LIVE_POSITION_MUTATION" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "trading state") {
            Fail-Gate "PMS_EMS_OMS_R023_FAIL_TRADING_STATE_MUTATION" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "fill|execution report") {
            Fail-Gate "PMS_EMS_OMS_R023_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "order|trading|executable|OMS|parent|child|broker|submission|submitted|routed|route") {
            Fail-Gate "PMS_EMS_OMS_R023_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden action detected: $($item.action)"
        }

        Fail-Gate "PMS_EMS_OMS_R023_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $($item.action)"
    }
}

Require-True ([bool]$nextPhase.nextPhaseRecommendationCreated) "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING" "Next phase missing."
Require-True ([string]$nextPhase.recommendedNextPhase -eq "PMS-EMS-OMS-R024") "PMS_EMS_OMS_R023_FAIL_LEDGER_PREVIEW_ARCHIVE_MISSING" "Next phase is not R024."
Require-True ([bool]$nextPhase.mustRemainNoExternal) "PMS_EMS_OMS_R023_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase not no-external."

$apiSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Api/appsettings.json") "PMS_EMS_OMS_R023_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$workerSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/appsettings.json") "PMS_EMS_OMS_R023_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-False ([bool]$apiSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R023_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API live trading enabled."
Require-False ([bool]$workerSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R023_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker live trading enabled."
Require-False ([bool]$apiSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R023_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API external enabled."
Require-False ([bool]$workerSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R023_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker external enabled."
Require-True ([bool]$apiSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R023_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API fake gateway not required."
Require-True ([bool]$workerSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R023_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker fake gateway not required."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.Enabled) "PMS_EMS_OMS_R023_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX runtime enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowExternalConnections) "PMS_EMS_OMS_R023_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX external enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowOrderSubmission) "PMS_EMS_OMS_R023_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "LMAX order submission enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SchedulerEnabled) "PMS_EMS_OMS_R023_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "LMAX scheduler enabled."
Require-False ([bool]$workerSettings.MarketDataBars.Enabled) "PMS_EMS_OMS_R023_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker market data bars enabled."

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-pms-ems-oms-r023-*" -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combined = [string]::Join("`n", $artifactText)
foreach ($pattern in @("\u0001", "35=", "MDReqID\s*[:=]", "SenderCompID\s*[:=]", "TargetCompID\s*[:=]", "BeginString\s*[:=]", "SocketHost\s*[:=]", "TlsHost\s*[:=]", "Password\s*[:=]", "ApiKey\s*[:=]", "Bearer\s+[A-Za-z0-9_\.-]+", "rawBid", "rawAsk", "rawMid")) {
    if ($combined -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R023_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Unsafe serialized content pattern found: $pattern"
    }
}

$requiredFiles = @(
    "src/QQ.Production.Intraday.Application/QubesPaperLedgerPreviewArchive.cs",
    "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperLedgerPreviewArchiveTests.cs"
)
foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $file))) {
        Fail-Gate "PMS_EMS_OMS_R023_FAIL_BUILD_OR_TESTS" "Required file missing: $file"
    }
}

$source = Get-Content -LiteralPath (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/QubesPaperLedgerPreviewArchive.cs") -Raw
foreach ($pattern in @("TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "SendOrderAsync", "SubmitOrder", "FixSession", "CreateOrderAsync")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R023_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "R023 source contains forbidden runtime pattern: $pattern"
    }
}
foreach ($pattern in @("AddHostedService", "IHostedService", "BackgroundService", "PeriodicTimer", "Task.Delay", "System.Threading.Timer")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R023_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "R023 source contains scheduler/service pattern: $pattern"
    }
}

$tests = Get-Content -LiteralPath (Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperLedgerPreviewArchiveTests.cs") -Raw
foreach ($requiredTestName in @(
    "R022_paper_position_ledger_preview_can_be_archived_no_externally",
    "Archive_preserves_paper_position_ledger_preview_id",
    "Archive_preserves_paper_position_preview_id",
    "Archive_preserves_paper_simulation_result_id",
    "Archive_preserves_paper_simulation_plan_id",
    "Archive_preserves_paper_execution_plan_id",
    "Archive_preserves_cycle_run_id_and_qubes_run_id",
    "Archive_preserves_operator_decision_id",
    "Preview_lines_include_audusd_delta",
    "Preview_lines_include_eurusd_delta",
    "Preview_lines_include_gbpusd_delta",
    "Operator_ledger_report_includes_no_paper_ledger_commit_disclaimer",
    "Operator_ledger_report_includes_no_live_position_mutation_disclaimer",
    "Operator_ledger_report_includes_no_broker_position_mutation_disclaimer",
    "Operator_ledger_report_includes_no_production_ledger_mutation_disclaimer",
    "Operator_ledger_report_includes_no_trading_state_mutation_disclaimer",
    "Operator_ledger_report_includes_no_order_fill_report_route_or_submission_disclaimer",
    "Hold_decision_is_recorded_safely",
    "Approve_paper_ledger_commit_readiness_is_recorded_safely",
    "Approve_paper_ledger_commit_readiness_does_not_commit_paper_ledger_state",
    "Approve_paper_ledger_commit_readiness_does_not_mutate_live_positions",
    "Approve_paper_ledger_commit_readiness_does_not_mutate_broker_positions",
    "Approve_paper_ledger_commit_readiness_does_not_mutate_production_ledger_state",
    "Approve_paper_ledger_commit_readiness_does_not_mutate_trading_state",
    "Duplicate_preview_archive_and_duplicate_decision_handling_are_idempotent",
    "Qubes_cycle_operator_preview_simulation_plan_candidate_risk_rebalance_and_lot_sizing_lineage_is_preserved",
    "Archive_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action",
    "Api_and_worker_live_gateway_remain_disabled",
    "Archive_source_introduces_no_scheduler_timer_polling_or_background_job",
    "No_fills_are_created",
    "No_execution_reports_are_created",
    "No_oms_or_broker_orders_are_created",
    "Audusd_is_not_misclassified_as_failed",
    "Usdjpy_caveat_remains_preserved"
)) {
    if ($tests -notmatch [regex]::Escape($requiredTestName)) {
        Fail-Gate "PMS_EMS_OMS_R023_FAIL_BUILD_OR_TESTS" "Focused test missing: $requiredTestName"
    }
}

Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R023_FAIL_BUILD_OR_TESTS" "Evidence marker missing."
Require-True ([string]$evidence.build.status -eq "PASS") "PMS_EMS_OMS_R023_FAIL_BUILD_OR_TESTS" "Build evidence missing or not PASS."
Require-True ([int]$evidence.build.failed -eq 0) "PMS_EMS_OMS_R023_FAIL_BUILD_OR_TESTS" "Build evidence has failures."
Require-True ([string]$evidence.focusedTests.status -eq "PASS") "PMS_EMS_OMS_R023_FAIL_BUILD_OR_TESTS" "Focused test evidence missing or not PASS."
Require-True ([int]$evidence.focusedTests.failed -eq 0) "PMS_EMS_OMS_R023_FAIL_BUILD_OR_TESTS" "Focused tests have failures."
Require-True ([string]$evidence.unitTests.status -eq "PASS") "PMS_EMS_OMS_R023_FAIL_BUILD_OR_TESTS" "Unit test evidence missing or not PASS."
Require-True ([int]$evidence.unitTests.failed -eq 0) "PMS_EMS_OMS_R023_FAIL_BUILD_OR_TESTS" "Unit tests have failures."
Require-True ([string]$evidence.validator.status -eq "PASS") "PMS_EMS_OMS_R023_FAIL_BUILD_OR_TESTS" "Validator evidence missing or not PASS."
Require-True ([int]$evidence.validator.failed -eq 0) "PMS_EMS_OMS_R023_FAIL_BUILD_OR_TESTS" "Validator evidence has failures."

Write-Host "PMS_EMS_OMS_R023_PASS_PAPER_LEDGER_PREVIEW_ARCHIVE_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R023_PASS_OPERATOR_LEDGER_REPORT_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R023_PASS_PAPER_LEDGER_COMMIT_READINESS_GATE_READY_NO_EXTERNAL"
