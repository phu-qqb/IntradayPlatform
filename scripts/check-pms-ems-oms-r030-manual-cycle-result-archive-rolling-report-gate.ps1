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
    "phase-pms-ems-oms-r030-summary.md" = "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r030-manual-cycle-result-archive-contract.json" = "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r030-manual-cycle-result-archive.json" = "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r030-operator-rolling-report.md" = "PMS_EMS_OMS_R030_FAIL_OPERATOR_REPORT_MISSING"
    "phase-pms-ems-oms-r030-operator-rolling-report.json" = "PMS_EMS_OMS_R030_FAIL_OPERATOR_REPORT_MISSING"
    "phase-pms-ems-oms-r030-paper-baseline-input-archive.json" = "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r030-qubes-lineage-archive.json" = "PMS_EMS_OMS_R030_FAIL_QUBES_LINEAGE_WEAKENED"
    "phase-pms-ems-oms-r030-target-portfolio-archive.json" = "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r030-target-vs-current-diff-archive.json" = "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r030-theoretical-pnl-archive.json" = "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r030-reconciliation-archive.json" = "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r030-theoretical-vs-real-archive.json" = "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r030-rebalance-intents-archive.json" = "PMS_EMS_OMS_R030_FAIL_REBALANCE_INTENT_EXECUTABLE"
    "phase-pms-ems-oms-r030-non-executable-intent-audit.json" = "PMS_EMS_OMS_R030_FAIL_REBALANCE_INTENT_EXECUTABLE"
    "phase-pms-ems-oms-r030-rolling-readiness-decision-gate.json" = "PMS_EMS_OMS_R030_FAIL_ROLLING_READINESS_GATE_MISSING"
    "phase-pms-ems-oms-r030-no-scheduler-service-polling-audit.json" = "PMS_EMS_OMS_R030_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
    "phase-pms-ems-oms-r030-no-automatic-execution-audit.json" = "PMS_EMS_OMS_R030_FAIL_AUTOMATIC_EXECUTION_INTRODUCED"
    "phase-pms-ems-oms-r030-no-paper-ledger-commit-audit.json" = "PMS_EMS_OMS_R030_FAIL_PAPER_LEDGER_COMMIT_OCCURRED"
    "phase-pms-ems-oms-r030-no-live-position-mutation-audit.json" = "PMS_EMS_OMS_R030_FAIL_LIVE_POSITION_MUTATION"
    "phase-pms-ems-oms-r030-no-broker-position-mutation-audit.json" = "PMS_EMS_OMS_R030_FAIL_BROKER_POSITION_MUTATION"
    "phase-pms-ems-oms-r030-no-production-ledger-mutation-audit.json" = "PMS_EMS_OMS_R030_FAIL_PRODUCTION_LEDGER_MUTATION"
    "phase-pms-ems-oms-r030-no-trading-state-mutation-audit.json" = "PMS_EMS_OMS_R030_FAIL_TRADING_STATE_MUTATION"
    "phase-pms-ems-oms-r030-no-fill-no-execution-report-audit.json" = "PMS_EMS_OMS_R030_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
    "phase-pms-ems-oms-r030-no-order-created-audit.json" = "PMS_EMS_OMS_R030_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r030-no-route-no-submission-audit.json" = "PMS_EMS_OMS_R030_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r030-idempotency-evidence.json" = "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r030-lineage-preservation.json" = "PMS_EMS_OMS_R030_FAIL_QUBES_LINEAGE_WEAKENED"
    "phase-pms-ems-oms-r030-instrument-universe-handling.json" = "PMS_EMS_OMS_R030_FAIL_AUDUSD_MISCLASSIFIED"
    "phase-pms-ems-oms-r030-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R030_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r030-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R030_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r030-no-external-audit.json" = "PMS_EMS_OMS_R030_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r030-forbidden-actions-audit.json" = "PMS_EMS_OMS_R030_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r030-next-phase-recommendation.json" = "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r030-build-test-validator-evidence.json" = "PMS_EMS_OMS_R030_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $entry.Key))) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$contract = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-manual-cycle-result-archive-contract.json") "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING"
$archive = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-manual-cycle-result-archive.json") "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING"
$report = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-operator-rolling-report.json") "PMS_EMS_OMS_R030_FAIL_OPERATOR_REPORT_MISSING"
$baseline = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-paper-baseline-input-archive.json") "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING"
$qubes = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-qubes-lineage-archive.json") "PMS_EMS_OMS_R030_FAIL_QUBES_LINEAGE_WEAKENED"
$target = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-target-portfolio-archive.json") "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING"
$diff = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-target-vs-current-diff-archive.json") "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING"
$pnl = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-theoretical-pnl-archive.json") "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING"
$reconciliation = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-reconciliation-archive.json") "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING"
$theoreticalVsReal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-theoretical-vs-real-archive.json") "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING"
$intents = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-rebalance-intents-archive.json") "PMS_EMS_OMS_R030_FAIL_REBALANCE_INTENT_EXECUTABLE"
$intentAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-non-executable-intent-audit.json") "PMS_EMS_OMS_R030_FAIL_REBALANCE_INTENT_EXECUTABLE"
$gate = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-rolling-readiness-decision-gate.json") "PMS_EMS_OMS_R030_FAIL_ROLLING_READINESS_GATE_MISSING"
$schedulerAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-no-scheduler-service-polling-audit.json") "PMS_EMS_OMS_R030_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
$automaticAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-no-automatic-execution-audit.json") "PMS_EMS_OMS_R030_FAIL_AUTOMATIC_EXECUTION_INTRODUCED"
$paperLedgerAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-no-paper-ledger-commit-audit.json") "PMS_EMS_OMS_R030_FAIL_PAPER_LEDGER_COMMIT_OCCURRED"
$liveAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-no-live-position-mutation-audit.json") "PMS_EMS_OMS_R030_FAIL_LIVE_POSITION_MUTATION"
$brokerAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-no-broker-position-mutation-audit.json") "PMS_EMS_OMS_R030_FAIL_BROKER_POSITION_MUTATION"
$productionAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-no-production-ledger-mutation-audit.json") "PMS_EMS_OMS_R030_FAIL_PRODUCTION_LEDGER_MUTATION"
$tradingAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-no-trading-state-mutation-audit.json") "PMS_EMS_OMS_R030_FAIL_TRADING_STATE_MUTATION"
$fillAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-no-fill-no-execution-report-audit.json") "PMS_EMS_OMS_R030_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$orderAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-no-order-created-audit.json") "PMS_EMS_OMS_R030_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$routeAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-no-route-no-submission-audit.json") "PMS_EMS_OMS_R030_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$idempotency = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-idempotency-evidence.json") "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING"
$lineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-lineage-preservation.json") "PMS_EMS_OMS_R030_FAIL_QUBES_LINEAGE_WEAKENED"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-instrument-universe-handling.json") "PMS_EMS_OMS_R030_FAIL_AUDUSD_MISCLASSIFIED"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R030_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R030_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-no-external-audit.json") "PMS_EMS_OMS_R030_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-forbidden-actions-audit.json") "PMS_EMS_OMS_R030_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r030-build-test-validator-evidence.json") "PMS_EMS_OMS_R030_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$contract.manualCycleResultArchiveContractCreated) "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING" "Contract missing."
foreach ($property in @("requiresR029ManualCycleResult", "requiresNoNewCycleRun", "requiresNoNewQubesBatchIngest", "requiresNoSchedulerServicePolling", "requiresNoAutomaticExecution", "requiresNoPaperLedgerCommit", "requiresNoOrdersFillsReportsRoutesSubmissions")) {
    Require-True ([bool]$contract.$property) "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING" "Contract flag missing: $property"
}

Require-True ([bool]$archive.manualCycleResultArchiveCreated) "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING" "Archive missing."
Require-True ([string]$archive.requestedCycleRunId -eq "cycle-r029-manual-paper-fixture") "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING" "Wrong cycle id."
Require-True ([string]$archive.qubesRunId -eq "qubes-r029-manual-fixture") "PMS_EMS_OMS_R030_FAIL_QUBES_LINEAGE_WEAKENED" "Wrong Qubes id."
Require-True ([string]$archive.runMode -eq "ManualNoExternal") "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING" "Wrong run mode."
Require-True ([string]$archive.preflightStatus -eq "ReadyNoExternal") "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING" "Wrong preflight."
Require-True ([string]$archive.archiveStatus -eq "ArchivedNoExternal") "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING" "Wrong archive status."
Require-True ([int]$archive.rawRowCount -eq 3) "PMS_EMS_OMS_R030_FAIL_QUBES_LINEAGE_WEAKENED" "Raw count wrong."
Require-True ([int]$archive.normalizedRowCount -eq 3) "PMS_EMS_OMS_R030_FAIL_QUBES_LINEAGE_WEAKENED" "Normalized count wrong."
foreach ($property in @("noExternal", "noSchedulerServicePolling", "noAutomaticExecution", "noNewCycleRun", "noNewQubesBatchIngest", "noPaperLedgerCommit", "noPaperLedgerMutation", "noLivePositionMutation", "noBrokerPositionMutation", "noProductionLedgerMutation", "noTradingStateMutation", "noOrderCreated", "noFillCreated", "noExecutionReportCreated", "noBrokerRoute", "noSubmission")) {
    Require-True ([bool]$archive.$property) "PMS_EMS_OMS_R030_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Archive safety missing: $property"
}

Require-True ([bool]$report.operatorRollingReportCreated) "PMS_EMS_OMS_R030_FAIL_OPERATOR_REPORT_MISSING" "Report missing."
foreach ($property in @("includesManualOperatorTriggeredDisclaimer", "includesNoSchedulerDisclaimer", "includesNoServiceDisclaimer", "includesNoPollingDisclaimer", "includesNoBrokerCallDisclaimer", "includesNoLiveMarketDataDisclaimer", "includesNoOrderDisclaimer", "includesNoFillDisclaimer", "includesNoExecutionReportDisclaimer", "includesNoRouteDisclaimer", "includesNoSubmissionDisclaimer", "includesNoPaperLedgerCommitDisclaimer")) {
    Require-True ([bool]$report.$property) "PMS_EMS_OMS_R030_FAIL_OPERATOR_REPORT_MISSING" "Report disclaimer missing: $property"
}
$reportMarkdown = Get-Content -LiteralPath (Join-Path $artifactRoot "phase-pms-ems-oms-r030-operator-rolling-report.md") -Raw
foreach ($phrase in @("Manual operator-triggered cycle only", "No scheduler", "No service", "No polling", "No broker call", "No live market data", "No orders", "No fills", "No execution reports", "No routes", "No submissions", "No paper ledger commit")) {
    if ($reportMarkdown -notmatch [regex]::Escape($phrase)) {
        Fail-Gate "PMS_EMS_OMS_R030_FAIL_OPERATOR_REPORT_MISSING" "Markdown report missing phrase: $phrase"
    }
}

Require-True ([bool]$baseline.paperBaselineInputArchiveCreated) "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING" "Baseline archive missing."
Require-True (@($baseline.lines | Where-Object { $_.symbol -eq "AUDUSD" -and [decimal]$_.currentPaperQuantity -eq 131000 }).Count -eq 1) "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING" "AUDUSD baseline missing."
Require-True (@($baseline.lines | Where-Object { $_.symbol -eq "EURUSD" -and [decimal]$_.currentPaperQuantity -eq 124000 }).Count -eq 1) "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING" "EURUSD baseline missing."
Require-True (@($baseline.lines | Where-Object { $_.symbol -eq "GBPUSD" -and [decimal]$_.currentPaperQuantity -eq -368000 }).Count -eq 1) "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING" "GBPUSD baseline missing."
Require-True ([bool]$qubes.qubesLineageArchived) "PMS_EMS_OMS_R030_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes archive missing."
Require-True ([bool]$target.targetPortfolioArchived) "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING" "Target archive missing."
Require-True ([bool]$diff.targetVsCurrentDiffArchived) "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING" "Diff archive missing."
Require-True (@($diff.lines | Where-Object { $_.symbol -eq "AUDUSD" -and [decimal]$_.deltaNotional -eq 17690 }).Count -eq 1) "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING" "AUDUSD diff missing."
Require-True (@($diff.lines | Where-Object { $_.symbol -eq "EURUSD" -and [decimal]$_.deltaNotional -eq 12236 }).Count -eq 1) "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING" "EURUSD diff missing."
Require-True (@($diff.lines | Where-Object { $_.symbol -eq "GBPUSD" -and [decimal]$_.deltaNotional -eq 213616 }).Count -eq 1) "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING" "GBPUSD diff missing."
Require-True ([bool]$pnl.theoreticalPnlArchived) "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING" "PnL archive missing."
Require-True ([bool]$reconciliation.reconciliationArchived) "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING" "Reconciliation missing."
Require-True ([bool]$theoreticalVsReal.theoreticalVsRealArchived) "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING" "Theoretical-vs-real missing."
Require-True ([bool]$intents.rebalanceIntentsArchived) "PMS_EMS_OMS_R030_FAIL_REBALANCE_INTENT_EXECUTABLE" "Intents archive missing."
Require-True ([bool]$intents.rebalanceIntentsRemainNonExecutable) "PMS_EMS_OMS_R030_FAIL_REBALANCE_INTENT_EXECUTABLE" "Intents executable."
foreach ($intent in @($intents.intents)) {
    Require-False ([bool]$intent.isExecutable) "PMS_EMS_OMS_R030_FAIL_REBALANCE_INTENT_EXECUTABLE" "Executable intent."
}
Require-True ([bool]$intentAudit.nonExecutableIntentAuditCreated) "PMS_EMS_OMS_R030_FAIL_REBALANCE_INTENT_EXECUTABLE" "Intent audit missing."
Require-False ([bool]$intentAudit.executableRebalanceIntentDetected) "PMS_EMS_OMS_R030_FAIL_REBALANCE_INTENT_EXECUTABLE" "Executable intent detected."

Require-True ([bool]$gate.rollingReadinessDecisionGateCreated) "PMS_EMS_OMS_R030_FAIL_ROLLING_READINESS_GATE_MISSING" "Gate missing."
Require-True ([string]$gate.rollingReadinessStatus -eq "ManualRollingReadyNoExternal") "PMS_EMS_OMS_R030_FAIL_ROLLING_READINESS_GATE_MISSING" "Gate status wrong."
Require-True ([bool]$gate.repeatedOperatorTriggeredManualRunsAllowed) "PMS_EMS_OMS_R030_FAIL_ROLLING_READINESS_GATE_MISSING" "Repeated manual runs not allowed."
foreach ($property in @("authorizesScheduler", "authorizesService", "authorizesPolling", "authorizesAutomaticExecution", "authorizesBrokerCall", "authorizesLiveTrading", "authorizesOrderCreationOrSubmission", "authorizesFills", "authorizesExecutionReports", "authorizesPaperLedgerCommit", "runsAnotherCycle", "ingestsAnotherQubesBatch", "mutatesPaperLedgerState")) {
    Require-False ([bool]$gate.$property) "PMS_EMS_OMS_R030_FAIL_ROLLING_READINESS_GATE_MISSING" "Gate unsafe flag: $property"
}

foreach ($audit in @($schedulerAudit, $automaticAudit, $paperLedgerAudit, $liveAudit, $brokerAudit, $productionAudit, $tradingAudit, $fillAudit, $orderAudit, $routeAudit)) {
    Require-True ([bool]$audit.auditCreated) "PMS_EMS_OMS_R030_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Audit missing."
}
foreach ($property in @("schedulerStarted", "serviceStarted", "pollingStarted", "timerStarted", "backgroundJobStarted")) {
    Require-False ([bool]$schedulerAudit.$property) "PMS_EMS_OMS_R030_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler/service detected: $property"
}
Require-False ([bool]$automaticAudit.automaticExecutionIntroduced) "PMS_EMS_OMS_R030_FAIL_AUTOMATIC_EXECUTION_INTRODUCED" "Automatic execution introduced."
Require-False ([bool]$paperLedgerAudit.paperLedgerStateCommitted) "PMS_EMS_OMS_R030_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Paper ledger committed."
Require-False ([bool]$paperLedgerAudit.paperLedgerStateMutated) "PMS_EMS_OMS_R030_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Paper ledger mutated."
Require-False ([bool]$liveAudit.livePositionStateMutated) "PMS_EMS_OMS_R030_FAIL_LIVE_POSITION_MUTATION" "Live position mutated."
Require-False ([bool]$brokerAudit.brokerPositionStateMutated) "PMS_EMS_OMS_R030_FAIL_BROKER_POSITION_MUTATION" "Broker position mutated."
Require-False ([bool]$productionAudit.productionLedgerStateMutated) "PMS_EMS_OMS_R030_FAIL_PRODUCTION_LEDGER_MUTATION" "Production ledger mutated."
Require-False ([bool]$tradingAudit.tradingStateMutated) "PMS_EMS_OMS_R030_FAIL_TRADING_STATE_MUTATION" "Trading state mutated."
Require-False ([bool]$fillAudit.fillCreated) "PMS_EMS_OMS_R030_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill created."
Require-False ([bool]$fillAudit.executionReportCreated) "PMS_EMS_OMS_R030_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Execution report created."
Require-False ([bool]$orderAudit.executableOrderCreated) "PMS_EMS_OMS_R030_FAIL_EXECUTABLE_ORDER_CREATED" "Executable order created."
Require-False ([bool]$orderAudit.omsOrderCreated) "PMS_EMS_OMS_R030_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "OMS order created."
Require-False ([bool]$routeAudit.brokerRouteCreated) "PMS_EMS_OMS_R030_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Broker route created."
Require-False ([bool]$routeAudit.ordersSubmitted) "PMS_EMS_OMS_R030_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders submitted."

Require-True ([bool]$idempotency.idempotencyEvidenceCreated) "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING" "Idempotency missing."
Require-True ([string]$idempotency.duplicateArchiveBehavior -eq "DuplicateReturned") "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING" "Duplicate archive wrong."
Require-True ([string]$idempotency.duplicateRequestedCycleRunIdBehavior -eq "DuplicateReturned") "PMS_EMS_OMS_R030_FAIL_MANUAL_CYCLE_ARCHIVE_MISSING" "Duplicate cycle wrong."
foreach ($property in @("duplicatesRunAnotherCycle", "duplicatesIngestAnotherQubesBatch", "duplicatesMutatePaperLedger")) {
    Require-False ([bool]$idempotency.$property) "PMS_EMS_OMS_R030_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Duplicate unsafe behavior: $property"
}

Require-True ([bool]$lineage.lineagePreservationCreated) "PMS_EMS_OMS_R030_FAIL_QUBES_LINEAGE_WEAKENED" "Lineage missing."
foreach ($property in @("qubesLineagePreserved", "cycleLineagePreserved", "r028ContractLineagePreserved", "r027ContinuityGateLineagePreserved", "r025PaperBaselineLineagePreserved", "ledgerArchiveCommitPreviewLineagePreserved", "simulationResultPlanLineagePreserved", "executionPlanLineagePreserved", "paperCandidateLineagePreserved", "riskLineagePreserved", "rebalanceIntentLineagePreserved", "lotSizingLineagePreserved")) {
    Require-True ([bool]$lineage.$property) "PMS_EMS_OMS_R030_FAIL_QUBES_LINEAGE_WEAKENED" "Lineage flag missing: $property"
}
Require-False ([bool]$lineage.lineageWeakened) "PMS_EMS_OMS_R030_FAIL_QUBES_LINEAGE_WEAKENED" "Lineage weakened."

Require-True ([bool]$universe.instrumentUniverseHandlingCreated) "PMS_EMS_OMS_R030_FAIL_AUDUSD_MISCLASSIFIED" "Universe missing."
Require-False ([bool]$universe.audusdClassifiedAsFailed) "PMS_EMS_OMS_R030_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD failed."
Require-True ([bool]$usdjpy.usdJpyCaveatPreservationCreated) "PMS_EMS_OMS_R030_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY missing."
Require-True ([string]$usdjpy.usdJpySecurityId -eq "4004") "PMS_EMS_OMS_R030_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID wrong."
Require-True ([string]$usdjpy.usdJpySecurityIdSource -eq "8") "PMS_EMS_OMS_R030_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource wrong."
Require-False ([bool]$usdjpy.usdJpyCaveatWeakened) "PMS_EMS_OMS_R030_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY weakened."
Require-True ([bool]$lmax.lmaxReadonlyBaselineReferenceCreated) "PMS_EMS_OMS_R030_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX missing."
Require-False ([bool]$lmax.lmaxUsedInThisPhase) "PMS_EMS_OMS_R030_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX used."
Require-False ([bool]$lmax.lmaxCalledInThisPhase) "PMS_EMS_OMS_R030_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX called."

foreach ($property in @("externalBrokerActivationDetected", "boundaryRuntimeActionDetected", "liveMarketDataAttempted", "apiStarted", "workerStarted", "schedulerPollingServiceTimerBackgroundJobStartedOrIntroduced", "automaticExecutionIntroduced", "liveGatewayEnabled", "moreThanOneManualCycleRun", "anotherCycleRanInR030", "anotherQubesBatchIngestedInR030", "replayOrShadowReplayIntroduced", "secretsOrCredentialsSerialized", "rawFixSerialized", "rawEndpointTlsValuesSerialized", "sessionIdsSerialized", "compIdsSerialized", "rawMdReqIdSerialized", "rawBrokerMarketDataPayloadsOrPricesSerialized", "rawMarketDataFixturePayloadsSerializedBeyondApprovedSafeSummaries")) {
    Require-False ([bool]$noExternal.$property) "PMS_EMS_OMS_R030_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected: $property"
}
foreach ($property in @("brokerActivation", "socketTlsFix", "liveMarketData", "apiWorkerSchedulerService", "timersPollingBackgroundJobs", "automaticExecution", "ordersRoutesSubmissions", "fillsExecutionReports", "liveTradingPath", "livePositionMutation", "brokerPositionMutation", "productionLedgerMutation", "tradingStateMutation", "paperLedgerCommitOrMutation", "newCycleRun", "newQubesBatchIngest", "replayShadowReplay", "secretOrRawPayloadSerialization")) {
    Require-False ([bool]$forbidden.$property) "PMS_EMS_OMS_R030_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $property"
}

Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R030_FAIL_BUILD_OR_TESTS" "Build/test evidence missing."
Require-True ([string]$evidence.dotnetBuildNoRestore -eq "PASS") "PMS_EMS_OMS_R030_FAIL_BUILD_OR_TESTS" "Build failed."
Require-True ([string]$evidence.focusedTests -like "PASS*") "PMS_EMS_OMS_R030_FAIL_BUILD_OR_TESTS" "Focused tests failed."
Require-True ([string]$evidence.unitTests -like "PASS*") "PMS_EMS_OMS_R030_FAIL_BUILD_OR_TESTS" "Unit tests failed."
Require-True ([string]$evidence.validator -eq "PASS") "PMS_EMS_OMS_R030_FAIL_BUILD_OR_TESTS" "Validator evidence missing."

Write-Host "PMS_EMS_OMS_R030_PASS_MANUAL_CYCLE_RESULT_ARCHIVE_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R030_PASS_OPERATOR_ROLLING_REPORT_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R030_PASS_MANUAL_ROLLING_READINESS_GATE_READY_NO_EXTERNAL"
