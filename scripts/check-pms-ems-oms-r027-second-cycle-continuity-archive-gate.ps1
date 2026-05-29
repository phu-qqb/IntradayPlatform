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
    "phase-pms-ems-oms-r027-summary.md" = "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r027-second-cycle-archive-contract.json" = "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r027-second-cycle-archive.json" = "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r027-second-cycle-operator-report.md" = "PMS_EMS_OMS_R027_FAIL_OPERATOR_REPORT_MISSING"
    "phase-pms-ems-oms-r027-second-cycle-operator-report.json" = "PMS_EMS_OMS_R027_FAIL_OPERATOR_REPORT_MISSING"
    "phase-pms-ems-oms-r027-paper-baseline-reference.json" = "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r027-target-vs-current-diff-archive.json" = "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r027-theoretical-pnl-archive.json" = "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r027-reconciliation-archive.json" = "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r027-theoretical-vs-real-archive.json" = "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r027-rebalance-intents-archive.json" = "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r027-non-executable-intent-audit.json" = "PMS_EMS_OMS_R027_FAIL_REBALANCE_INTENT_EXECUTABLE"
    "phase-pms-ems-oms-r027-paper-continuity-decision-gate.json" = "PMS_EMS_OMS_R027_FAIL_PAPER_CONTINUITY_GATE_MISSING"
    "phase-pms-ems-oms-r027-no-new-cycle-run-audit.json" = "PMS_EMS_OMS_R027_FAIL_NEW_CYCLE_RAN"
    "phase-pms-ems-oms-r027-no-new-qubes-batch-ingest-audit.json" = "PMS_EMS_OMS_R027_FAIL_NEW_QUBES_BATCH_INGESTED"
    "phase-pms-ems-oms-r027-no-paper-ledger-mutation-audit.json" = "PMS_EMS_OMS_R027_FAIL_PAPER_LEDGER_MUTATED"
    "phase-pms-ems-oms-r027-no-live-position-mutation-audit.json" = "PMS_EMS_OMS_R027_FAIL_LIVE_POSITION_MUTATION"
    "phase-pms-ems-oms-r027-no-broker-position-mutation-audit.json" = "PMS_EMS_OMS_R027_FAIL_BROKER_POSITION_MUTATION"
    "phase-pms-ems-oms-r027-no-production-ledger-mutation-audit.json" = "PMS_EMS_OMS_R027_FAIL_PRODUCTION_LEDGER_MUTATION"
    "phase-pms-ems-oms-r027-no-trading-state-mutation-audit.json" = "PMS_EMS_OMS_R027_FAIL_TRADING_STATE_MUTATION"
    "phase-pms-ems-oms-r027-no-fill-no-execution-report-audit.json" = "PMS_EMS_OMS_R027_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
    "phase-pms-ems-oms-r027-no-order-created-audit.json" = "PMS_EMS_OMS_R027_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r027-no-route-no-submission-audit.json" = "PMS_EMS_OMS_R027_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r027-idempotency-evidence.json" = "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r027-lineage-preservation.json" = "PMS_EMS_OMS_R027_FAIL_QUBES_LINEAGE_WEAKENED"
    "phase-pms-ems-oms-r027-instrument-universe-handling.json" = "PMS_EMS_OMS_R027_FAIL_AUDUSD_MISCLASSIFIED"
    "phase-pms-ems-oms-r027-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R027_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r027-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R027_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r027-no-external-audit.json" = "PMS_EMS_OMS_R027_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r027-forbidden-actions-audit.json" = "PMS_EMS_OMS_R027_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r027-next-phase-recommendation.json" = "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r027-build-test-validator-evidence.json" = "PMS_EMS_OMS_R027_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $entry.Key))) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$contract = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r027-second-cycle-archive-contract.json") "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING"
$archive = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r027-second-cycle-archive.json") "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING"
$report = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r027-second-cycle-operator-report.json") "PMS_EMS_OMS_R027_FAIL_OPERATOR_REPORT_MISSING"
$baseline = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r027-paper-baseline-reference.json") "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING"
$diff = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r027-target-vs-current-diff-archive.json") "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING"
$pnl = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r027-theoretical-pnl-archive.json") "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING"
$reconciliation = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r027-reconciliation-archive.json") "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING"
$theoreticalVsReal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r027-theoretical-vs-real-archive.json") "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING"
$intents = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r027-rebalance-intents-archive.json") "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING"
$intentAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r027-non-executable-intent-audit.json") "PMS_EMS_OMS_R027_FAIL_REBALANCE_INTENT_EXECUTABLE"
$gate = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r027-paper-continuity-decision-gate.json") "PMS_EMS_OMS_R027_FAIL_PAPER_CONTINUITY_GATE_MISSING"
$newCycleAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r027-no-new-cycle-run-audit.json") "PMS_EMS_OMS_R027_FAIL_NEW_CYCLE_RAN"
$newQubesAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r027-no-new-qubes-batch-ingest-audit.json") "PMS_EMS_OMS_R027_FAIL_NEW_QUBES_BATCH_INGESTED"
$paperLedgerAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r027-no-paper-ledger-mutation-audit.json") "PMS_EMS_OMS_R027_FAIL_PAPER_LEDGER_MUTATED"
$liveAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r027-no-live-position-mutation-audit.json") "PMS_EMS_OMS_R027_FAIL_LIVE_POSITION_MUTATION"
$brokerAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r027-no-broker-position-mutation-audit.json") "PMS_EMS_OMS_R027_FAIL_BROKER_POSITION_MUTATION"
$productionAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r027-no-production-ledger-mutation-audit.json") "PMS_EMS_OMS_R027_FAIL_PRODUCTION_LEDGER_MUTATION"
$tradingAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r027-no-trading-state-mutation-audit.json") "PMS_EMS_OMS_R027_FAIL_TRADING_STATE_MUTATION"
$fillAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r027-no-fill-no-execution-report-audit.json") "PMS_EMS_OMS_R027_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$orderAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r027-no-order-created-audit.json") "PMS_EMS_OMS_R027_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$routeAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r027-no-route-no-submission-audit.json") "PMS_EMS_OMS_R027_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$idempotency = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r027-idempotency-evidence.json") "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING"
$lineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r027-lineage-preservation.json") "PMS_EMS_OMS_R027_FAIL_QUBES_LINEAGE_WEAKENED"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r027-instrument-universe-handling.json") "PMS_EMS_OMS_R027_FAIL_AUDUSD_MISCLASSIFIED"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r027-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R027_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r027-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R027_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r027-no-external-audit.json") "PMS_EMS_OMS_R027_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r027-forbidden-actions-audit.json") "PMS_EMS_OMS_R027_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r027-build-test-validator-evidence.json") "PMS_EMS_OMS_R027_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$contract.secondCycleArchiveContractCreated) "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING" "Contract missing."
foreach ($property in @("requiresR026SecondCycleOutput", "requiresR025PaperBaselineReference", "requiresNoNewCycleRun", "requiresNoNewQubesBatchIngest", "requiresNoPaperLedgerMutation", "requiresNoLiveBrokerProductionTradingMutation", "requiresNoOrderFillReportRouteSubmission", "requiresNonExecutableRebalanceIntents", "requiresNoSchedulerServicePolling")) {
    Require-True ([bool]$contract.$property) "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING" "Contract flag missing: $property"
}

Require-True ([bool]$archive.secondCycleArchiveCreated) "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING" "Archive missing."
Require-True ([string]$archive.secondCycleRunId -eq "cycle-r026-second-paper-baseline") "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING" "Wrong cycle id."
Require-True ([string]$archive.qubesRunId -eq "qubes-r026-second-cycle") "PMS_EMS_OMS_R027_FAIL_QUBES_LINEAGE_WEAKENED" "Wrong Qubes run id."
Require-True ([int]$archive.cycleCadenceMinutes -eq 15) "PMS_EMS_OMS_R027_FAIL_QUBES_LINEAGE_WEAKENED" "Cadence missing."
Require-True ([string]$archive.archiveStatus -eq "ArchivedNoExternal") "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING" "Archive status wrong."
Require-True ([bool]$archive.paperBaselineFromR025) "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING" "R025 baseline not preserved."
Require-True ([bool]$archive.rebalanceIntentsRemainNonExecutable) "PMS_EMS_OMS_R027_FAIL_REBALANCE_INTENT_EXECUTABLE" "Rebalance intents executable."
foreach ($property in @("r025PaperBaselineMutated", "paperLedgerStateCommittedOrMutated")) {
    Require-False ([bool]$archive.$property) "PMS_EMS_OMS_R027_FAIL_PAPER_LEDGER_MUTATED" "Archive unsafe flag: $property"
}
foreach ($property in @("noExternal", "noBrokerCall", "noLiveMarketData", "noApiWorkerStart", "noSchedulerServicePolling", "noNewCycleRun", "noNewQubesBatchIngest", "noPaperLedgerMutation", "noLivePositionMutation", "noBrokerPositionMutation", "noProductionLedgerMutation", "noTradingStateMutation", "noOrderCreated", "noFillCreated", "noExecutionReportCreated", "noBrokerRoute", "noSubmission")) {
    Require-True ([bool]$archive.$property) "PMS_EMS_OMS_R027_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Archive safety flag missing: $property"
}

Require-True ([bool]$report.operatorReportCreated) "PMS_EMS_OMS_R027_FAIL_OPERATOR_REPORT_MISSING" "Report missing."
foreach ($property in @("includesPaperLedgerFixtureBaselineDisclaimer", "includesNoLiveBrokerProductionTradingMutationDisclaimer", "includesNoPaperLedgerCommitDisclaimer", "includesNoBrokerCallDisclaimer", "includesNoLiveMarketDataDisclaimer", "includesNoOrderDisclaimer", "includesNoFillDisclaimer", "includesNoExecutionReportDisclaimer", "includesNoBrokerRouteDisclaimer", "includesNoSubmissionDisclaimer")) {
    Require-True ([bool]$report.$property) "PMS_EMS_OMS_R027_FAIL_OPERATOR_REPORT_MISSING" "Report disclaimer missing: $property"
}
$reportMarkdown = Get-Content -LiteralPath (Join-Path $artifactRoot "phase-pms-ems-oms-r027-second-cycle-operator-report.md") -Raw
foreach ($phrase in @("Second cycle used paper ledger fixture baseline", "No live/broker/production/trading state mutation", "No paper ledger commit in R026/R027", "No broker calls", "No live market data", "No orders", "No fills", "No execution reports", "No broker routes", "No submissions")) {
    if ($reportMarkdown -notmatch [regex]::Escape($phrase)) {
        Fail-Gate "PMS_EMS_OMS_R027_FAIL_OPERATOR_REPORT_MISSING" "Markdown report disclaimer missing: $phrase"
    }
}

Require-True ([bool]$baseline.paperBaselineReferenceCreated) "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING" "Paper baseline reference missing."
Require-False ([bool]$baseline.r025PaperBaselineMutated) "PMS_EMS_OMS_R027_FAIL_R025_BASELINE_MUTATED" "R025 baseline mutated."
Require-True (@($baseline.lines | Where-Object { $_.symbol -eq "AUDUSD" -and [decimal]$_.quantity -eq 131000 }).Count -eq 1) "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING" "AUDUSD baseline missing."
Require-True (@($baseline.lines | Where-Object { $_.symbol -eq "EURUSD" -and [decimal]$_.quantity -eq 124000 }).Count -eq 1) "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING" "EURUSD baseline missing."
Require-True (@($baseline.lines | Where-Object { $_.symbol -eq "GBPUSD" -and [decimal]$_.quantity -eq -368000 }).Count -eq 1) "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING" "GBPUSD baseline missing."

Require-True ([bool]$diff.targetVsCurrentDiffArchived) "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING" "Diff archive missing."
Require-True ([bool]$diff.deltasComputedRelativeToPaperBaseline) "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING" "Diff not relative to baseline."
Require-True (@($diff.lines | Where-Object { $_.symbol -eq "AUDUSD" -and [decimal]$_.deltaNotional -eq 7690 }).Count -eq 1) "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING" "AUDUSD delta missing."
Require-True (@($diff.lines | Where-Object { $_.symbol -eq "EURUSD" -and [decimal]$_.deltaNotional -eq 22236 }).Count -eq 1) "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING" "EURUSD delta missing."
Require-True (@($diff.lines | Where-Object { $_.symbol -eq "GBPUSD" -and [decimal]$_.deltaNotional -eq 233616 }).Count -eq 1) "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING" "GBPUSD delta missing."

Require-True ([bool]$pnl.theoreticalPnlArchived) "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING" "PnL archive missing."
Require-False ([bool]$pnl.usedLiveMarketData) "PMS_EMS_OMS_R027_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "PnL used live market data."
Require-True ([bool]$reconciliation.reconciliationArchived) "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING" "Reconciliation archive missing."
Require-False ([bool]$reconciliation.actualFixtureIsBrokerReportedLiveState) "PMS_EMS_OMS_R027_FAIL_BROKER_POSITION_MUTATION" "Reconciliation used broker state."
Require-True ([bool]$theoreticalVsReal.theoreticalVsRealArchived) "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING" "Theoretical-vs-real archive missing."
Require-False ([bool]$theoreticalVsReal.liveReconciliationClaim) "PMS_EMS_OMS_R027_FAIL_TRADING_STATE_MUTATION" "Live reconciliation claim made."

Require-True ([bool]$intents.rebalanceIntentsArchived) "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING" "Intent archive missing."
Require-True ([bool]$intents.rebalanceIntentsRemainNonExecutable) "PMS_EMS_OMS_R027_FAIL_REBALANCE_INTENT_EXECUTABLE" "Intent archive executable."
foreach ($intent in @($intents.intents)) {
    Require-False ([bool]$intent.isExecutable) "PMS_EMS_OMS_R027_FAIL_REBALANCE_INTENT_EXECUTABLE" "Executable intent detected."
}
Require-True ([bool]$intentAudit.nonExecutableIntentAuditCreated) "PMS_EMS_OMS_R027_FAIL_REBALANCE_INTENT_EXECUTABLE" "Intent audit missing."
Require-False ([bool]$intentAudit.executableRebalanceIntentDetected) "PMS_EMS_OMS_R027_FAIL_REBALANCE_INTENT_EXECUTABLE" "Executable intent detected."
Require-False ([bool]$intentAudit.orderCandidateCreated) "PMS_EMS_OMS_R027_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order candidate created."

Require-True ([bool]$gate.paperContinuityDecisionGateCreated) "PMS_EMS_OMS_R027_FAIL_PAPER_CONTINUITY_GATE_MISSING" "Continuity gate missing."
Require-True ([string]$gate.continuityStatus -eq "PaperContinuityReadyNoExternal") "PMS_EMS_OMS_R027_FAIL_PAPER_CONTINUITY_GATE_MISSING" "Continuity status wrong."
foreach ($property in @("startsSchedulerOrService", "runsAnotherCycle", "ingestsNewQubesBatch", "mutatesPaperLedgerState", "mutatesLivePositionState", "mutatesBrokerPositionState", "mutatesProductionLedgerState", "mutatesTradingState", "createsOrderCandidates", "createsExecutionPlans", "createsOrders", "createsFills", "createsExecutionReports", "createsRoutes", "submitsOrders")) {
    Require-False ([bool]$gate.$property) "PMS_EMS_OMS_R027_FAIL_PAPER_CONTINUITY_GATE_MISSING" "Continuity gate unsafe flag: $property"
}
Require-True ([bool]$gate.noExternal) "PMS_EMS_OMS_R027_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Gate not no-external."

Require-True ([bool]$newCycleAudit.noNewCycleRunAuditCreated) "PMS_EMS_OMS_R027_FAIL_NEW_CYCLE_RAN" "No-new-cycle audit missing."
Require-False ([bool]$newCycleAudit.newCycleRan) "PMS_EMS_OMS_R027_FAIL_NEW_CYCLE_RAN" "New cycle ran."
Require-False ([bool]$newCycleAudit.thirdCycleRan) "PMS_EMS_OMS_R027_FAIL_NEW_CYCLE_RAN" "Third cycle ran."
Require-True ([bool]$newQubesAudit.noNewQubesBatchIngestAuditCreated) "PMS_EMS_OMS_R027_FAIL_NEW_QUBES_BATCH_INGESTED" "No-new-Qubes audit missing."
Require-False ([bool]$newQubesAudit.newQubesBatchIngested) "PMS_EMS_OMS_R027_FAIL_NEW_QUBES_BATCH_INGESTED" "New Qubes batch ingested."
Require-True ([bool]$paperLedgerAudit.noPaperLedgerMutationAuditCreated) "PMS_EMS_OMS_R027_FAIL_PAPER_LEDGER_MUTATED" "No-paper-ledger audit missing."
Require-False ([bool]$paperLedgerAudit.paperLedgerStateCommitted) "PMS_EMS_OMS_R027_FAIL_PAPER_LEDGER_MUTATED" "Paper ledger committed."
Require-False ([bool]$paperLedgerAudit.paperLedgerStateMutated) "PMS_EMS_OMS_R027_FAIL_PAPER_LEDGER_MUTATED" "Paper ledger mutated."
Require-False ([bool]$paperLedgerAudit.r025PaperBaselineMutated) "PMS_EMS_OMS_R027_FAIL_R025_BASELINE_MUTATED" "R025 baseline mutated."

Require-True ([bool]$liveAudit.noLivePositionMutationAuditCreated) "PMS_EMS_OMS_R027_FAIL_LIVE_POSITION_MUTATION" "Live-position audit missing."
Require-False ([bool]$liveAudit.livePositionStateMutated) "PMS_EMS_OMS_R027_FAIL_LIVE_POSITION_MUTATION" "Live position mutated."
Require-True ([bool]$brokerAudit.noBrokerPositionMutationAuditCreated) "PMS_EMS_OMS_R027_FAIL_BROKER_POSITION_MUTATION" "Broker-position audit missing."
Require-False ([bool]$brokerAudit.brokerPositionStateMutated) "PMS_EMS_OMS_R027_FAIL_BROKER_POSITION_MUTATION" "Broker position mutated."
Require-True ([bool]$productionAudit.noProductionLedgerMutationAuditCreated) "PMS_EMS_OMS_R027_FAIL_PRODUCTION_LEDGER_MUTATION" "Production-ledger audit missing."
Require-False ([bool]$productionAudit.productionLedgerStateMutated) "PMS_EMS_OMS_R027_FAIL_PRODUCTION_LEDGER_MUTATION" "Production ledger mutated."
Require-True ([bool]$tradingAudit.noTradingStateMutationAuditCreated) "PMS_EMS_OMS_R027_FAIL_TRADING_STATE_MUTATION" "Trading-state audit missing."
Require-False ([bool]$tradingAudit.tradingStateMutated) "PMS_EMS_OMS_R027_FAIL_TRADING_STATE_MUTATION" "Trading state mutated."
Require-True ([bool]$fillAudit.noFillNoExecutionReportAuditCreated) "PMS_EMS_OMS_R027_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill/report audit missing."
foreach ($property in @("fillCreated", "realFillCreated", "executionReportCreated", "brokerExecutionReportCreated", "continuityGateCreatesFillOrExecutionReport")) {
    Require-False ([bool]$fillAudit.$property) "PMS_EMS_OMS_R027_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill/report detected: $property"
}
Require-True ([bool]$orderAudit.noOrderCreatedAuditCreated) "PMS_EMS_OMS_R027_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order audit missing."
foreach ($property in @("executableOrderCreated", "omsOrderCreated", "parentOrderCreated", "childOrderCreated", "brokerOrderCreated", "orderStateCreated", "continuityGateCreatesOrders")) {
    Require-False ([bool]$orderAudit.$property) "PMS_EMS_OMS_R027_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order detected: $property"
}
Require-True ([bool]$routeAudit.noRouteNoSubmissionAuditCreated) "PMS_EMS_OMS_R027_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Route/submission audit missing."
foreach ($property in @("brokerRouteCreated", "submissionInstructionCreated", "orderSubmissionPathIntroduced", "ordersSubmitted", "continuityGateCreatesRoutesOrSubmissions")) {
    Require-False ([bool]$routeAudit.$property) "PMS_EMS_OMS_R027_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Route/submission detected: $property"
}

Require-True ([bool]$idempotency.idempotencyEvidenceCreated) "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING" "Idempotency missing."
Require-True ([string]$idempotency.duplicateArchiveBehavior -eq "DuplicateReturned") "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING" "Duplicate behavior wrong."
foreach ($property in @("duplicatesCreateAdditionalArchives", "duplicatesMutatePaperLedger", "duplicatesRunAnotherCycle", "duplicatesIngestNewQubesBatch")) {
    Require-False ([bool]$idempotency.$property) "PMS_EMS_OMS_R027_FAIL_SECOND_CYCLE_ARCHIVE_MISSING" "Duplicate unsafe behavior: $property"
}

Require-True ([bool]$lineage.lineagePreservationCreated) "PMS_EMS_OMS_R027_FAIL_QUBES_LINEAGE_WEAKENED" "Lineage missing."
foreach ($property in @("qubesLineagePreserved", "cycleLineagePreserved", "paperBaselineLineagePreserved", "ledgerStateArchiveLineagePreserved", "ledgerCommitLineagePreserved", "ledgerPreviewLineagePreserved", "positionPreviewLineagePreserved", "simulationResultLineagePreserved", "simulationPlanLineagePreserved", "executionPlanLineagePreserved", "paperCandidateLineagePreserved", "riskLineagePreserved", "rebalanceIntentLineagePreserved", "lotSizingLineagePreserved", "missingStaleMarkHandlingPreserved", "driftAcknowledgementPreserved")) {
    Require-True ([bool]$lineage.$property) "PMS_EMS_OMS_R027_FAIL_QUBES_LINEAGE_WEAKENED" "Lineage flag missing: $property"
}
Require-False ([bool]$lineage.lineageWeakened) "PMS_EMS_OMS_R027_FAIL_QUBES_LINEAGE_WEAKENED" "Lineage weakened."

Require-True ([bool]$universe.instrumentUniverseHandlingCreated) "PMS_EMS_OMS_R027_FAIL_AUDUSD_MISCLASSIFIED" "Universe handling missing."
Require-False ([bool]$universe.audusdClassifiedAsFailed) "PMS_EMS_OMS_R027_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD classified failed."
Require-False ([bool]$universe.audusdLiveValidationGapBlocksSecondCycleArchive) "PMS_EMS_OMS_R027_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD gap blocks archive."
Require-True ([bool]$usdjpy.usdJpyCaveatPreservationCreated) "PMS_EMS_OMS_R027_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
Require-True ([string]$usdjpy.usdJpySecurityId -eq "4004") "PMS_EMS_OMS_R027_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID wrong."
Require-True ([string]$usdjpy.usdJpySecurityIdSource -eq "8") "PMS_EMS_OMS_R027_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource wrong."
Require-False ([bool]$usdjpy.usdJpyClassifiedAsFailed) "PMS_EMS_OMS_R027_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY classified failed."
Require-False ([bool]$usdjpy.usdJpyCaveatWeakened) "PMS_EMS_OMS_R027_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat weakened."
Require-True ([bool]$lmax.lmaxReadonlyBaselineReferenceCreated) "PMS_EMS_OMS_R027_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX reference missing."
Require-False ([bool]$lmax.lmaxUsedInThisPhase) "PMS_EMS_OMS_R027_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX used."
Require-False ([bool]$lmax.lmaxCalledInThisPhase) "PMS_EMS_OMS_R027_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX called."

foreach ($property in @("externalBrokerActivationDetected", "socketTlsFixMarketDataRuntimeActionDetected", "marketDataRequestAttempted", "liveMarketDataResponseRead", "apiStarted", "workerStarted", "schedulerPollingServiceTimerBackgroundJobStartedOrIntroduced", "liveGatewayEnabled", "newCycleRan", "newQubesBatchIngested", "replayOrShadowReplayIntroduced", "secretsOrCredentialsSerialized", "rawFixSerialized", "rawEndpointTlsValuesSerialized", "sessionIdsSerialized", "compIdsSerialized", "rawMdReqIdSerialized", "rawBrokerMarketDataPayloadsOrPricesSerialized", "rawMarketDataFixturePayloadsSerializedBeyondApprovedSafeSummaries")) {
    Require-False ([bool]$noExternal.$property) "PMS_EMS_OMS_R027_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected: $property"
}
foreach ($property in @("brokerActivation", "socketTlsFix", "liveMarketDataRequestOrResponse", "apiWorkerSchedulerService", "timersPollingBackgroundJobs", "ordersRoutesSubmissions", "fillsExecutionReports", "liveTradingPath", "livePositionMutation", "brokerPositionMutation", "productionLedgerMutation", "tradingStateMutation", "paperLedgerMutation", "r025BaselineMutation", "newCycleRun", "newQubesBatchIngest", "replayShadowReplay", "secretOrRawPayloadSerialization")) {
    Require-False ([bool]$forbidden.$property) "PMS_EMS_OMS_R027_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $property"
}

Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R027_FAIL_BUILD_OR_TESTS" "Build/test evidence missing."
Require-True ([string]$evidence.dotnetBuildNoRestore -eq "PASS") "PMS_EMS_OMS_R027_FAIL_BUILD_OR_TESTS" "Build did not pass."
Require-True ([string]$evidence.focusedTests -like "PASS*") "PMS_EMS_OMS_R027_FAIL_BUILD_OR_TESTS" "Focused tests did not pass."
Require-True ([string]$evidence.unitTests -like "PASS*") "PMS_EMS_OMS_R027_FAIL_BUILD_OR_TESTS" "Unit tests did not pass."

Write-Host "PMS_EMS_OMS_R027_PASS_SECOND_CYCLE_ARCHIVE_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R027_PASS_SECOND_CYCLE_OPERATOR_REPORT_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R027_PASS_PAPER_CONTINUITY_DECISION_GATE_READY_NO_EXTERNAL"
