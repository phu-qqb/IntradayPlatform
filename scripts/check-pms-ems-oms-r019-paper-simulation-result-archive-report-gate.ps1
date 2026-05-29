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
    "phase-pms-ems-oms-r019-summary.md" = "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r019-paper-simulation-result-archive-contract.json" = "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r019-paper-simulation-result-archive.json" = "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r019-paper-simulation-operator-report.md" = "PMS_EMS_OMS_R019_FAIL_OPERATOR_REPORT_MISSING"
    "phase-pms-ems-oms-r019-paper-simulation-operator-report.json" = "PMS_EMS_OMS_R019_FAIL_OPERATOR_REPORT_MISSING"
    "phase-pms-ems-oms-r019-paper-simulation-result-lines.json" = "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r019-paper-simulation-summary.json" = "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r019-paper-post-trade-preview-archive.json" = "PMS_EMS_OMS_R019_FAIL_LIVE_STATE_MUTATION"
    "phase-pms-ems-oms-r019-paper-reconciliation-preview-archive.json" = "PMS_EMS_OMS_R019_FAIL_LIVE_STATE_MUTATION"
    "phase-pms-ems-oms-r019-blocked-lines-preservation.json" = "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r019-no-real-fill-audit.json" = "PMS_EMS_OMS_R019_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
    "phase-pms-ems-oms-r019-no-execution-report-audit.json" = "PMS_EMS_OMS_R019_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
    "phase-pms-ems-oms-r019-no-order-created-audit.json" = "PMS_EMS_OMS_R019_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r019-no-route-no-submission-audit.json" = "PMS_EMS_OMS_R019_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED"
    "phase-pms-ems-oms-r019-no-live-state-mutation-audit.json" = "PMS_EMS_OMS_R019_FAIL_LIVE_STATE_MUTATION"
    "phase-pms-ems-oms-r019-idempotency-evidence.json" = "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r019-risk-lineage-preservation.json" = "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r019-qubes-lineage-preservation.json" = "PMS_EMS_OMS_R019_FAIL_QUBES_LINEAGE_WEAKENED"
    "phase-pms-ems-oms-r019-operator-decision-lineage-preservation.json" = "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r019-plan-lineage-preservation.json" = "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r019-paper-candidate-lineage-preservation.json" = "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r019-rebalance-intent-lineage-preservation.json" = "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r019-lot-sizing-lineage-preservation.json" = "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r019-missing-stale-mark-preservation.json" = "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r019-drift-acknowledgement-preservation.json" = "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r019-instrument-universe-handling.json" = "PMS_EMS_OMS_R019_FAIL_LMAX_GAP_BLOCKS_SIMULATION_REPORT"
    "phase-pms-ems-oms-r019-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R019_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r019-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R019_FAIL_LMAX_GAP_BLOCKS_SIMULATION_REPORT"
    "phase-pms-ems-oms-r019-no-external-audit.json" = "PMS_EMS_OMS_R019_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r019-forbidden-actions-audit.json" = "PMS_EMS_OMS_R019_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r019-next-phase-recommendation.json" = "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r019-build-test-validator-evidence.json" = "PMS_EMS_OMS_R019_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $entry.Key))) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$contract = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r019-paper-simulation-result-archive-contract.json") "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
$archive = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r019-paper-simulation-result-archive.json") "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
$report = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r019-paper-simulation-operator-report.json") "PMS_EMS_OMS_R019_FAIL_OPERATOR_REPORT_MISSING"
$lines = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r019-paper-simulation-result-lines.json") "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
$summary = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r019-paper-simulation-summary.json") "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
$postTrade = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r019-paper-post-trade-preview-archive.json") "PMS_EMS_OMS_R019_FAIL_LIVE_STATE_MUTATION"
$reconciliation = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r019-paper-reconciliation-preview-archive.json") "PMS_EMS_OMS_R019_FAIL_LIVE_STATE_MUTATION"
$blocked = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r019-blocked-lines-preservation.json") "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
$fillAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r019-no-real-fill-audit.json") "PMS_EMS_OMS_R019_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$reportAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r019-no-execution-report-audit.json") "PMS_EMS_OMS_R019_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$orderAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r019-no-order-created-audit.json") "PMS_EMS_OMS_R019_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$routeAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r019-no-route-no-submission-audit.json") "PMS_EMS_OMS_R019_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED"
$stateAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r019-no-live-state-mutation-audit.json") "PMS_EMS_OMS_R019_FAIL_LIVE_STATE_MUTATION"
$idempotency = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r019-idempotency-evidence.json") "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
$risk = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r019-risk-lineage-preservation.json") "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
$qubes = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r019-qubes-lineage-preservation.json") "PMS_EMS_OMS_R019_FAIL_QUBES_LINEAGE_WEAKENED"
$operatorLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r019-operator-decision-lineage-preservation.json") "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
$planLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r019-plan-lineage-preservation.json") "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
$candidateLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r019-paper-candidate-lineage-preservation.json") "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
$rebalance = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r019-rebalance-intent-lineage-preservation.json") "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
$lotSizing = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r019-lot-sizing-lineage-preservation.json") "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
$marks = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r019-missing-stale-mark-preservation.json") "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
$drift = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r019-drift-acknowledgement-preservation.json") "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r019-instrument-universe-handling.json") "PMS_EMS_OMS_R019_FAIL_LMAX_GAP_BLOCKS_SIMULATION_REPORT"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r019-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R019_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r019-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R019_FAIL_LMAX_GAP_BLOCKS_SIMULATION_REPORT"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r019-no-external-audit.json") "PMS_EMS_OMS_R019_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r019-forbidden-actions-audit.json") "PMS_EMS_OMS_R019_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$nextPhase = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r019-next-phase-recommendation.json") "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r019-build-test-validator-evidence.json") "PMS_EMS_OMS_R019_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$contract.paperSimulationResultArchiveContractCreated) "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING" "Archive contract missing."
foreach ($property in @("paperOnly", "noExternal", "nonExecutable", "notAnOrder", "notSubmitted", "noBrokerRoute", "noRealFillCreated", "noExecutionReportCreated", "noOrderCreated", "noLiveStateMutation")) {
    Require-True ([bool]$contract.$property) "PMS_EMS_OMS_R019_FAIL_EXECUTABLE_ORDER_CREATED" "Contract safety flag missing: $property"
}

Require-True ([bool]$archive.paperSimulationResultArchiveCreated) "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING" "Archive missing."
Require-True ([string]$archive.archiveStatus -eq "ArchivedWithBlockedLines") "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING" "Archive status wrong."
Require-True ([int]$archive.simulatedAppliedLines -eq 3) "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING" "Applied line count wrong."
Require-True ([int]$archive.blockedLines -eq 10) "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING" "Blocked line count wrong."
foreach ($property in @("paperOnly", "noExternal", "nonExecutable", "notAnOrder", "notSubmitted", "noBrokerRoute", "noRealFillCreated", "noExecutionReportCreated", "noOrderCreated", "noLiveStateMutation")) {
    Require-True ([bool]$archive.$property) "PMS_EMS_OMS_R019_FAIL_EXECUTABLE_ORDER_CREATED" "Archive safety flag missing: $property"
}
foreach ($property in @("realFillEntityCreated", "brokerExecutionReportEntityCreated", "omsOrderCreated", "parentOrderCreated", "childOrderCreated", "brokerOrderCreated", "orderStateCreated", "submittedOrders", "calledBrokerGateway", "requestedLiveMarketData", "startedApiOrWorker", "startedSchedulerOrBackgroundJob", "mutatedLiveTradingState", "mutatedLivePositionState", "mutatedBrokerState")) {
    Require-False ([bool]$archive.$property) "PMS_EMS_OMS_R019_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Unsafe archive flag detected: $property"
}

Require-True ([bool]$report.operatorSimulationReportCreated) "PMS_EMS_OMS_R019_FAIL_OPERATOR_REPORT_MISSING" "Operator report missing."
foreach ($property in @("includesPaperOnlyDisclaimer", "includesNoRealFillDisclaimer", "includesNoExecutionReportDisclaimer", "includesNoOmsOrderDisclaimer", "includesNoBrokerOrderDisclaimer", "includesNoSubmissionDisclaimer", "includesNoBrokerRouteDisclaimer", "includesNoLiveStateMutationDisclaimer", "includesBlockedLinesSummary", "includesPostTradePreview", "includesReconciliationPreview")) {
    Require-True ([bool]$report.$property) "PMS_EMS_OMS_R019_FAIL_OPERATOR_REPORT_MISSING" "Operator report flag missing: $property"
}
$reportMarkdown = Get-Content -LiteralPath (Join-Path $artifactRoot "phase-pms-ems-oms-r019-paper-simulation-operator-report.md") -Raw
foreach ($text in @("Paper simulation only", "No real fills", "no execution reports", "no OMS orders", "no broker orders", "no submissions", "no broker route", "no live state mutation")) {
    if ($reportMarkdown -notmatch [regex]::Escape($text)) {
        Fail-Gate "PMS_EMS_OMS_R019_FAIL_OPERATOR_REPORT_MISSING" "Operator report disclaimer missing: $text"
    }
}

Require-True ([bool]$lines.paperSimulationResultLinesArchived) "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING" "Result lines missing."
Require-True (@($lines.lines | Where-Object { $_.normalizedSymbol -eq "AUDUSD" -and $_.side -eq "Buy" -and [decimal]$_.simulatedAppliedQuantity -eq 131000 }).Count -eq 1) "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING" "AUDUSD line missing."
Require-True (@($lines.lines | Where-Object { $_.normalizedSymbol -eq "EURUSD" -and $_.side -eq "Buy" -and [decimal]$_.simulatedAppliedQuantity -eq 124000 }).Count -eq 1) "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING" "EURUSD line missing."
Require-True (@($lines.lines | Where-Object { $_.normalizedSymbol -eq "GBPUSD" -and $_.side -eq "Sell" -and [decimal]$_.simulatedAppliedQuantity -eq 368000 }).Count -eq 1) "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING" "GBPUSD line missing."
foreach ($line in $lines.lines) {
    Require-True ([string]$line.simulatedOutcomeCategory -eq "PaperApplied") "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING" "Line not paper-applied."
    foreach ($property in @("noFillCreated", "noExecutionReportCreated", "noOrderCreated", "noBrokerRoute")) {
        Require-True ([bool]$line.$property) "PMS_EMS_OMS_R019_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Line safety flag missing: $property"
    }
}

Require-True ([bool]$summary.paperSimulationSummaryArchived) "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING" "Summary missing."
Require-True ([int]$summary.simulatedAppliedLines -eq 3) "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING" "Summary applied lines wrong."
Require-True ([int]$summary.blockedLines -eq 10) "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING" "Summary blocked lines wrong."
Require-True ([int]$summary.realFillCount -eq 0) "PMS_EMS_OMS_R019_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Real fill count nonzero."
Require-True ([int]$summary.executionReportCount -eq 0) "PMS_EMS_OMS_R019_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Execution report count nonzero."
Require-True ([int]$summary.orderCount -eq 0) "PMS_EMS_OMS_R019_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order count nonzero."
Require-True ([int]$summary.brokerRouteCount -eq 0) "PMS_EMS_OMS_R019_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "Broker route count nonzero."

Require-True ([bool]$postTrade.paperPostTradePreviewArchived) "PMS_EMS_OMS_R019_FAIL_LIVE_STATE_MUTATION" "Post-trade archive missing."
Require-True ([bool]$postTrade.simulatedOnly) "PMS_EMS_OMS_R019_FAIL_LIVE_STATE_MUTATION" "Post-trade preview not simulated-only."
Require-False ([bool]$postTrade.livePositionStateMutated) "PMS_EMS_OMS_R019_FAIL_LIVE_STATE_MUTATION" "Live position mutated."
Require-False ([bool]$postTrade.brokerStateMutated) "PMS_EMS_OMS_R019_FAIL_LIVE_STATE_MUTATION" "Broker state mutated."
Require-False ([bool]$postTrade.tradingStateMutated) "PMS_EMS_OMS_R019_FAIL_LIVE_STATE_MUTATION" "Trading state mutated."
Require-True ([bool]$reconciliation.paperReconciliationPreviewArchived) "PMS_EMS_OMS_R019_FAIL_LIVE_STATE_MUTATION" "Reconciliation archive missing."
Require-False ([bool]$reconciliation.liveReconciliationClaimCreated) "PMS_EMS_OMS_R019_FAIL_LIVE_STATE_MUTATION" "Live reconciliation claim created."
Require-False ([bool]$reconciliation.livePositionStateMutated) "PMS_EMS_OMS_R019_FAIL_LIVE_STATE_MUTATION" "Reconciliation mutated live position."

Require-True ([bool]$blocked.blockedLinesPreservationCreated) "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING" "Blocked-lines preservation missing."
Require-True ([bool]$blocked.blockedLinesPreservedSeparately) "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING" "Blocked lines not separate."
Require-False ([bool]$blocked.blockedLinesBecomeArchivedAppliedLines) "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING" "Blocked lines became applied lines."

foreach ($property in @("realFillEntityCreated", "fillDomainEntityCreated", "archiveCreatesRealFill", "operatorReportCreatesRealFill")) {
    Require-False ([bool]$fillAudit.$property) "PMS_EMS_OMS_R019_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Real fill audit detected: $property"
}
foreach ($property in @("brokerExecutionReportEntityCreated", "executionReportDomainEntityCreated", "archiveCreatesExecutionReport", "operatorReportCreatesExecutionReport")) {
    Require-False ([bool]$reportAudit.$property) "PMS_EMS_OMS_R019_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Execution report audit detected: $property"
}
foreach ($property in @("omsOrderCreated", "parentOrderCreated", "childOrderCreated", "brokerOrderCreated", "executableOrderCreated", "orderStateCreated", "orderSubmissionPathIntroduced", "ordersSubmitted", "liveTradingStateMutated", "brokerGatewayCalled")) {
    Require-False ([bool]$orderAudit.$property) "PMS_EMS_OMS_R019_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order audit detected: $property"
}
foreach ($property in @("brokerRouteCreated", "brokerRouteAssigned", "submissionInstructionCreated", "orderSubmissionPathInvoked", "archiveSubmitted", "archiveRouteable")) {
    Require-False ([bool]$routeAudit.$property) "PMS_EMS_OMS_R019_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "Route/submission audit detected: $property"
}
foreach ($property in @("liveTradingStateMutated", "livePositionStateMutated", "brokerStateMutated", "liveReconciliationStateMutated")) {
    Require-False ([bool]$stateAudit.$property) "PMS_EMS_OMS_R019_FAIL_LIVE_STATE_MUTATION" "Live state mutation detected: $property"
}

Require-True ([bool]$idempotency.idempotencyEvidenceCreated) "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING" "Idempotency missing."
Require-True ([string]$idempotency.idempotencyKey -eq "PaperSimulationResultId") "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING" "Wrong idempotency key."
Require-True ([string]$idempotency.duplicateArchiveBehavior -eq "DuplicateReturned") "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING" "Duplicate behavior missing."
Require-False ([bool]$idempotency.duplicatesCreateAdditionalArchives) "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING" "Duplicate archives created."

Require-True ([bool]$risk.riskLineagePreservationCreated) "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING" "Risk lineage missing."
Require-False ([bool]$risk.riskLineageMissing) "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING" "Risk lineage marked missing."
Require-True ([bool]$qubes.qubesLineagePreservationCreated) "PMS_EMS_OMS_R019_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage missing."
Require-True ([string]$qubes.sourceSystem -eq "ModelWeightSourceSystem.Qubes") "PMS_EMS_OMS_R019_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes source missing."
Require-True ([int]$qubes.cadenceMinutes -eq 15) "PMS_EMS_OMS_R019_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes cadence missing."
Require-False ([bool]$qubes.qubesLineageWeakened) "PMS_EMS_OMS_R019_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage weakened."
foreach ($lineage in @($operatorLineage, $planLineage, $candidateLineage, $rebalance, $lotSizing, $marks, $drift)) {
    $createdProperty = $lineage.PSObject.Properties.Name | Where-Object { $_ -match "Created$" } | Select-Object -First 1
    if ($createdProperty) {
        Require-True ([bool]$lineage.$createdProperty) "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING" "Lineage artifact missing: $createdProperty"
    }
}
Require-True ([bool]$operatorLineage.blockedLinesAcknowledged) "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING" "Blocked line acknowledgement missing."
Require-True ([bool]$operatorLineage.missingStaleMarksAcknowledged) "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING" "Missing/stale acknowledgement missing."
Require-True ([bool]$operatorLineage.driftAcknowledged) "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING" "Drift acknowledgement missing."
Require-True ([bool]$rebalance.rebalanceIntentsRemainNonExecutable) "PMS_EMS_OMS_R019_FAIL_EXECUTABLE_ORDER_CREATED" "Rebalance intents executable."
Require-False ([bool]$rebalance.rebalanceIntentCreatesOrder) "PMS_EMS_OMS_R019_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Rebalance creates order."
Require-False ([bool]$marks.rawMarketDataFixturePayloadsSerialized) "PMS_EMS_OMS_R019_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw fixture payloads serialized."
Require-False ([bool]$marks.fabricatedMarksForArchive) "PMS_EMS_OMS_R019_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Marks fabricated."
Require-False ([bool]$drift.liveTradingApprovalCreated) "PMS_EMS_OMS_R019_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Live trading approval created."

Require-True ([bool]$universe.instrumentUniverseHandlingCreated) "PMS_EMS_OMS_R019_FAIL_LMAX_GAP_BLOCKS_SIMULATION_REPORT" "Universe handling missing."
Require-False ([bool]$universe.lmaxLiveValidationGapsBlockPaperSimulationArchive) "PMS_EMS_OMS_R019_FAIL_LMAX_GAP_BLOCKS_SIMULATION_REPORT" "LMAX gaps block archive."
Require-False ([bool]$universe.audusdClassifiedAsFailed) "PMS_EMS_OMS_R019_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD failed."
Require-False ([bool]$universe.audusdLiveValidationGapBlocksPaperSimulationArchive) "PMS_EMS_OMS_R019_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD gap blocks archive."
Require-False ([bool]$universe.usdjpyLiveValidationGapBlocksPaperSimulationArchive) "PMS_EMS_OMS_R019_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY gap blocks archive."
Require-True ([bool]$usdjpy.usdJpyCaveatPreservationCreated) "PMS_EMS_OMS_R019_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
Require-True ([string]$usdjpy.usdJpySecurityId -eq "4004") "PMS_EMS_OMS_R019_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID missing."
Require-True ([string]$usdjpy.usdJpySecurityIdSource -eq "8") "PMS_EMS_OMS_R019_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource missing."
Require-False ([bool]$usdjpy.usdJpyClassifiedAsFailed) "PMS_EMS_OMS_R019_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY failed."
Require-False ([bool]$usdjpy.audusdClassifiedAsFailed) "PMS_EMS_OMS_R019_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD failed."
Require-True ([bool]$lmax.lmaxReadonlyBaselineReferenceCreated) "PMS_EMS_OMS_R019_FAIL_LMAX_GAP_BLOCKS_SIMULATION_REPORT" "LMAX baseline missing."
Require-False ([bool]$lmax.lmaxUsedInThisPhase) "PMS_EMS_OMS_R019_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX used."
Require-False ([bool]$lmax.lmaxCalledInThisPhase) "PMS_EMS_OMS_R019_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX called."
Require-False ([bool]$lmax.lmaxLiveValidationGapsBlockPaperSimulationArchive) "PMS_EMS_OMS_R019_FAIL_LMAX_GAP_BLOCKS_SIMULATION_REPORT" "LMAX gaps block archive."

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
    "realFillEntityCreated",
    "brokerExecutionReportEntityCreated",
    "fillCreatedAsRealOrderDomainEntity",
    "executionReportCreatedAsBrokerDomainEntity",
    "liveTradingPathIntroduced",
    "liveTradingStateMutated",
    "livePositionStateMutated",
    "brokerStateMutated",
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
    "archiveExecutable",
    "archiveSubmitted",
    "archiveHasBrokerRoute",
    "lmaxLiveValidationGapsBlockPaperSimulationArchive"
)) {
    Require-False ([bool]$noExternal.$property) "PMS_EMS_OMS_R019_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected: $property"
}

foreach ($item in $forbidden.forbiddenActions) {
    if ([bool]$item.detected) {
        if ([string]$item.action -match "scheduler|service|timer|background") {
            Fail-Gate "PMS_EMS_OMS_R019_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "fill|execution report") {
            Fail-Gate "PMS_EMS_OMS_R019_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "state mutation") {
            Fail-Gate "PMS_EMS_OMS_R019_FAIL_LIVE_STATE_MUTATION" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "order|trading|executable|OMS|parent|child|broker|submitted|routed") {
            Fail-Gate "PMS_EMS_OMS_R019_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden action detected: $($item.action)"
        }

        Fail-Gate "PMS_EMS_OMS_R019_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $($item.action)"
    }
}

Require-True ([bool]$nextPhase.nextPhaseRecommendationCreated) "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING" "Next phase missing."
Require-True ([string]$nextPhase.recommendedNextPhase -eq "PMS-EMS-OMS-R020") "PMS_EMS_OMS_R019_FAIL_SIMULATION_RESULT_ARCHIVE_MISSING" "Next phase is not R020."
Require-True ([bool]$nextPhase.mustRemainNoExternal) "PMS_EMS_OMS_R019_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase not no-external."

$apiSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Api/appsettings.json") "PMS_EMS_OMS_R019_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$workerSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/appsettings.json") "PMS_EMS_OMS_R019_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-False ([bool]$apiSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R019_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API live trading enabled."
Require-False ([bool]$workerSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R019_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker live trading enabled."
Require-False ([bool]$apiSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R019_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API external enabled."
Require-False ([bool]$workerSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R019_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker external enabled."
Require-True ([bool]$apiSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R019_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API fake gateway not required."
Require-True ([bool]$workerSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R019_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker fake gateway not required."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.Enabled) "PMS_EMS_OMS_R019_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX runtime enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowExternalConnections) "PMS_EMS_OMS_R019_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX external enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowOrderSubmission) "PMS_EMS_OMS_R019_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "LMAX order submission enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SchedulerEnabled) "PMS_EMS_OMS_R019_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "LMAX scheduler enabled."
Require-False ([bool]$workerSettings.MarketDataBars.Enabled) "PMS_EMS_OMS_R019_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker market data bars enabled."

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-pms-ems-oms-r019-*" -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combined = [string]::Join("`n", $artifactText)
foreach ($pattern in @("\u0001", "35=", "MDReqID\s*[:=]", "SenderCompID\s*[:=]", "TargetCompID\s*[:=]", "BeginString\s*[:=]", "SocketHost\s*[:=]", "TlsHost\s*[:=]", "Password\s*[:=]", "ApiKey\s*[:=]", "Bearer\s+[A-Za-z0-9_\.-]+", "rawBid", "rawAsk", "rawMid")) {
    if ($combined -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R019_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Unsafe serialized content pattern found: $pattern"
    }
}

$requiredFiles = @(
    "src/QQ.Production.Intraday.Application/QubesPaperSimulationResultArchive.cs",
    "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperSimulationResultArchiveTests.cs"
)
foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $file))) {
        Fail-Gate "PMS_EMS_OMS_R019_FAIL_BUILD_OR_TESTS" "Required file missing: $file"
    }
}

$source = Get-Content -LiteralPath (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/QubesPaperSimulationResultArchive.cs") -Raw
foreach ($pattern in @("TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "SendOrderAsync", "SubmitOrder", "FixSession")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R019_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "R019 source contains forbidden runtime pattern: $pattern"
    }
}
foreach ($pattern in @("AddHostedService", "IHostedService", "BackgroundService", "PeriodicTimer", "Task.Delay", "System.Threading.Timer")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R019_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "R019 source contains scheduler/service pattern: $pattern"
    }
}

$tests = Get-Content -LiteralPath (Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperSimulationResultArchiveTests.cs") -Raw
foreach ($requiredTestName in @(
    "R018_paper_simulation_result_can_be_archived_no_externally",
    "Archive_preserves_paper_simulation_result_id",
    "Archive_preserves_paper_simulation_plan_id",
    "Archive_preserves_paper_execution_plan_id",
    "Archive_preserves_cycle_run_id_and_qubes_run_id",
    "Archive_preserves_operator_decision_id",
    "Result_lines_preserve_instruments_and_quantities",
    "Summary_preserves_zero_real_domain_counts",
    "Operator_report_includes_paper_only_no_real_fill_no_order_disclaimer",
    "Operator_report_includes_blocked_lines_summary",
    "Operator_report_includes_simulated_post_trade_preview",
    "Operator_report_includes_simulated_reconciliation_preview",
    "Duplicate_archive_handling_is_idempotent",
    "No_real_fills_are_created",
    "No_execution_reports_are_created",
    "No_oms_parent_child_or_broker_order_is_created",
    "No_order_submission_path_is_introduced",
    "No_live_state_mutation_occurs",
    "Archive_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action",
    "Api_and_worker_live_gateway_remain_disabled",
    "Archive_source_introduces_no_scheduler_timer_polling_or_background_job",
    "Audusd_is_not_misclassified_as_failed",
    "Usdjpy_caveat_remains_preserved"
)) {
    if ($tests -notmatch [regex]::Escape($requiredTestName)) {
        Fail-Gate "PMS_EMS_OMS_R019_FAIL_BUILD_OR_TESTS" "Focused test missing: $requiredTestName"
    }
}

Require-True ([string]$evidence.build.status -eq "PASS") "PMS_EMS_OMS_R019_FAIL_BUILD_OR_TESTS" "Build evidence missing or not PASS."
Require-True ([int]$evidence.build.failed -eq 0) "PMS_EMS_OMS_R019_FAIL_BUILD_OR_TESTS" "Build evidence has failures."
Require-True ([string]$evidence.focusedTests.status -eq "PASS") "PMS_EMS_OMS_R019_FAIL_BUILD_OR_TESTS" "Focused test evidence missing or not PASS."
Require-True ([int]$evidence.focusedTests.failed -eq 0) "PMS_EMS_OMS_R019_FAIL_BUILD_OR_TESTS" "Focused tests have failures."
Require-True ([string]$evidence.unitTests.status -eq "PASS") "PMS_EMS_OMS_R019_FAIL_BUILD_OR_TESTS" "Unit test evidence missing or not PASS."
Require-True ([int]$evidence.unitTests.failed -eq 0) "PMS_EMS_OMS_R019_FAIL_BUILD_OR_TESTS" "Unit tests have failures."
Require-True ([string]$evidence.validator.status -eq "PASS") "PMS_EMS_OMS_R019_FAIL_BUILD_OR_TESTS" "Validator evidence missing or not PASS."
Require-True ([int]$evidence.validator.failed -eq 0) "PMS_EMS_OMS_R019_FAIL_BUILD_OR_TESTS" "Validator evidence has failures."
Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R019_FAIL_BUILD_OR_TESTS" "Evidence marker missing."

Write-Host "PMS_EMS_OMS_R019_PASS_PAPER_SIMULATION_RESULT_ARCHIVE_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R019_PASS_OPERATOR_SIMULATION_REPORT_READY_NO_EXTERNAL"
