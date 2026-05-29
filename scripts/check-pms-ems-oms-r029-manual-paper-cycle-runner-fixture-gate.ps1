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
    "phase-pms-ems-oms-r029-summary.md" = "PMS_EMS_OMS_R029_FAIL_PREFLIGHT_MISSING_OR_FAILED"
    "phase-pms-ems-oms-r029-manual-cycle-run-request.json" = "PMS_EMS_OMS_R029_FAIL_PREFLIGHT_MISSING_OR_FAILED"
    "phase-pms-ems-oms-r029-manual-cycle-preflight-result.json" = "PMS_EMS_OMS_R029_FAIL_PREFLIGHT_MISSING_OR_FAILED"
    "phase-pms-ems-oms-r029-manual-cycle-run-result.json" = "PMS_EMS_OMS_R029_FAIL_PREFLIGHT_MISSING_OR_FAILED"
    "phase-pms-ems-oms-r029-paper-baseline-input.json" = "PMS_EMS_OMS_R029_FAIL_TARGET_CURRENT_DIFF_MISSING"
    "phase-pms-ems-oms-r029-qubes-lineage.json" = "PMS_EMS_OMS_R029_FAIL_QUBES_LINEAGE_WEAKENED"
    "phase-pms-ems-oms-r029-target-portfolio.json" = "PMS_EMS_OMS_R029_FAIL_TARGET_CURRENT_DIFF_MISSING"
    "phase-pms-ems-oms-r029-target-vs-current-diff.json" = "PMS_EMS_OMS_R029_FAIL_TARGET_CURRENT_DIFF_MISSING"
    "phase-pms-ems-oms-r029-theoretical-pnl.json" = "PMS_EMS_OMS_R029_FAIL_TARGET_CURRENT_DIFF_MISSING"
    "phase-pms-ems-oms-r029-reconciliation.json" = "PMS_EMS_OMS_R029_FAIL_TARGET_CURRENT_DIFF_MISSING"
    "phase-pms-ems-oms-r029-theoretical-vs-real.json" = "PMS_EMS_OMS_R029_FAIL_TARGET_CURRENT_DIFF_MISSING"
    "phase-pms-ems-oms-r029-rebalance-intents.json" = "PMS_EMS_OMS_R029_FAIL_REBALANCE_INTENT_EXECUTABLE"
    "phase-pms-ems-oms-r029-operator-cycle-report.md" = "PMS_EMS_OMS_R029_FAIL_PREFLIGHT_MISSING_OR_FAILED"
    "phase-pms-ems-oms-r029-operator-cycle-report.json" = "PMS_EMS_OMS_R029_FAIL_PREFLIGHT_MISSING_OR_FAILED"
    "phase-pms-ems-oms-r029-non-executable-intent-audit.json" = "PMS_EMS_OMS_R029_FAIL_REBALANCE_INTENT_EXECUTABLE"
    "phase-pms-ems-oms-r029-no-scheduler-service-polling-audit.json" = "PMS_EMS_OMS_R029_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
    "phase-pms-ems-oms-r029-no-paper-ledger-commit-audit.json" = "PMS_EMS_OMS_R029_FAIL_PAPER_LEDGER_COMMIT_OCCURRED"
    "phase-pms-ems-oms-r029-no-live-position-mutation-audit.json" = "PMS_EMS_OMS_R029_FAIL_LIVE_POSITION_MUTATION"
    "phase-pms-ems-oms-r029-no-broker-position-mutation-audit.json" = "PMS_EMS_OMS_R029_FAIL_BROKER_POSITION_MUTATION"
    "phase-pms-ems-oms-r029-no-production-ledger-mutation-audit.json" = "PMS_EMS_OMS_R029_FAIL_PRODUCTION_LEDGER_MUTATION"
    "phase-pms-ems-oms-r029-no-trading-state-mutation-audit.json" = "PMS_EMS_OMS_R029_FAIL_TRADING_STATE_MUTATION"
    "phase-pms-ems-oms-r029-no-fill-no-execution-report-audit.json" = "PMS_EMS_OMS_R029_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
    "phase-pms-ems-oms-r029-no-order-created-audit.json" = "PMS_EMS_OMS_R029_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r029-no-route-no-submission-audit.json" = "PMS_EMS_OMS_R029_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r029-idempotency-evidence.json" = "PMS_EMS_OMS_R029_FAIL_PREFLIGHT_MISSING_OR_FAILED"
    "phase-pms-ems-oms-r029-lineage-preservation.json" = "PMS_EMS_OMS_R029_FAIL_QUBES_LINEAGE_WEAKENED"
    "phase-pms-ems-oms-r029-instrument-universe-handling.json" = "PMS_EMS_OMS_R029_FAIL_AUDUSD_MISCLASSIFIED"
    "phase-pms-ems-oms-r029-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R029_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r029-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R029_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r029-no-external-audit.json" = "PMS_EMS_OMS_R029_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r029-forbidden-actions-audit.json" = "PMS_EMS_OMS_R029_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r029-next-phase-recommendation.json" = "PMS_EMS_OMS_R029_FAIL_PREFLIGHT_MISSING_OR_FAILED"
    "phase-pms-ems-oms-r029-build-test-validator-evidence.json" = "PMS_EMS_OMS_R029_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $entry.Key))) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$request = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r029-manual-cycle-run-request.json") "PMS_EMS_OMS_R029_FAIL_PREFLIGHT_MISSING_OR_FAILED"
$preflight = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r029-manual-cycle-preflight-result.json") "PMS_EMS_OMS_R029_FAIL_PREFLIGHT_MISSING_OR_FAILED"
$run = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r029-manual-cycle-run-result.json") "PMS_EMS_OMS_R029_FAIL_PREFLIGHT_MISSING_OR_FAILED"
$baseline = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r029-paper-baseline-input.json") "PMS_EMS_OMS_R029_FAIL_TARGET_CURRENT_DIFF_MISSING"
$qubes = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r029-qubes-lineage.json") "PMS_EMS_OMS_R029_FAIL_QUBES_LINEAGE_WEAKENED"
$target = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r029-target-portfolio.json") "PMS_EMS_OMS_R029_FAIL_TARGET_CURRENT_DIFF_MISSING"
$diff = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r029-target-vs-current-diff.json") "PMS_EMS_OMS_R029_FAIL_TARGET_CURRENT_DIFF_MISSING"
$pnl = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r029-theoretical-pnl.json") "PMS_EMS_OMS_R029_FAIL_TARGET_CURRENT_DIFF_MISSING"
$reconciliation = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r029-reconciliation.json") "PMS_EMS_OMS_R029_FAIL_TARGET_CURRENT_DIFF_MISSING"
$theoreticalVsReal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r029-theoretical-vs-real.json") "PMS_EMS_OMS_R029_FAIL_TARGET_CURRENT_DIFF_MISSING"
$intents = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r029-rebalance-intents.json") "PMS_EMS_OMS_R029_FAIL_REBALANCE_INTENT_EXECUTABLE"
$report = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r029-operator-cycle-report.json") "PMS_EMS_OMS_R029_FAIL_PREFLIGHT_MISSING_OR_FAILED"
$intentAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r029-non-executable-intent-audit.json") "PMS_EMS_OMS_R029_FAIL_REBALANCE_INTENT_EXECUTABLE"
$schedulerAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r029-no-scheduler-service-polling-audit.json") "PMS_EMS_OMS_R029_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
$paperLedgerAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r029-no-paper-ledger-commit-audit.json") "PMS_EMS_OMS_R029_FAIL_PAPER_LEDGER_COMMIT_OCCURRED"
$liveAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r029-no-live-position-mutation-audit.json") "PMS_EMS_OMS_R029_FAIL_LIVE_POSITION_MUTATION"
$brokerAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r029-no-broker-position-mutation-audit.json") "PMS_EMS_OMS_R029_FAIL_BROKER_POSITION_MUTATION"
$productionAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r029-no-production-ledger-mutation-audit.json") "PMS_EMS_OMS_R029_FAIL_PRODUCTION_LEDGER_MUTATION"
$tradingAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r029-no-trading-state-mutation-audit.json") "PMS_EMS_OMS_R029_FAIL_TRADING_STATE_MUTATION"
$fillAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r029-no-fill-no-execution-report-audit.json") "PMS_EMS_OMS_R029_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$orderAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r029-no-order-created-audit.json") "PMS_EMS_OMS_R029_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$routeAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r029-no-route-no-submission-audit.json") "PMS_EMS_OMS_R029_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$idempotency = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r029-idempotency-evidence.json") "PMS_EMS_OMS_R029_FAIL_PREFLIGHT_MISSING_OR_FAILED"
$lineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r029-lineage-preservation.json") "PMS_EMS_OMS_R029_FAIL_QUBES_LINEAGE_WEAKENED"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r029-instrument-universe-handling.json") "PMS_EMS_OMS_R029_FAIL_AUDUSD_MISCLASSIFIED"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r029-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R029_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r029-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R029_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r029-no-external-audit.json") "PMS_EMS_OMS_R029_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r029-forbidden-actions-audit.json") "PMS_EMS_OMS_R029_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r029-build-test-validator-evidence.json") "PMS_EMS_OMS_R029_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$request.manualCycleRunRequestCreated) "PMS_EMS_OMS_R029_FAIL_PREFLIGHT_MISSING_OR_FAILED" "Run request missing."
Require-True ([string]$request.requestedCycleRunId -eq "cycle-r029-manual-paper-fixture") "PMS_EMS_OMS_R029_FAIL_PREFLIGHT_MISSING_OR_FAILED" "Wrong cycle run id."
Require-True ([string]$request.qubesRunId -eq "qubes-r029-manual-fixture") "PMS_EMS_OMS_R029_FAIL_QUBES_LINEAGE_WEAKENED" "Wrong QubesRunId."
Require-True ([int]$request.expectedCadenceMinutes -eq 15) "PMS_EMS_OMS_R029_FAIL_PREFLIGHT_MISSING_OR_FAILED" "Wrong cadence."
Require-True ([string]$request.runMode -eq "ManualNoExternal") "PMS_EMS_OMS_R029_FAIL_PREFLIGHT_MISSING_OR_FAILED" "Wrong run mode."

Require-True ([bool]$preflight.preflightPassed) "PMS_EMS_OMS_R029_FAIL_PREFLIGHT_MISSING_OR_FAILED" "Preflight did not pass."
Require-True ([string]$preflight.preflightStatus -eq "ReadyNoExternal") "PMS_EMS_OMS_R029_FAIL_PREFLIGHT_MISSING_OR_FAILED" "Preflight status wrong."
Require-True ([bool]$preflight.priorPaperContinuityReadyNoExternal) "PMS_EMS_OMS_R029_FAIL_PREFLIGHT_MISSING_OR_FAILED" "Prior continuity missing."
Require-True ([bool]$preflight.paperLedgerBaselineExists) "PMS_EMS_OMS_R029_FAIL_PREFLIGHT_MISSING_OR_FAILED" "Baseline missing."
Require-True ([bool]$preflight.qubesRunIdPresent) "PMS_EMS_OMS_R029_FAIL_QUBES_LINEAGE_WEAKENED" "QubesRunId missing."
Require-True ([bool]$preflight.qubesInputNoExternalFixture) "PMS_EMS_OMS_R029_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes input unsafe."
foreach ($property in @("schedulerServicePollingRequested", "brokerOrLiveMarketDataRequested")) {
    Require-False ([bool]$preflight.$property) "PMS_EMS_OMS_R029_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Unsafe preflight flag: $property"
}

Require-True ([bool]$run.manualCycleRunResultCreated) "PMS_EMS_OMS_R029_FAIL_PREFLIGHT_MISSING_OR_FAILED" "Run result missing."
Require-True ([string]$run.runStatus -eq "CompletedNoExternalFixture") "PMS_EMS_OMS_R029_FAIL_PREFLIGHT_MISSING_OR_FAILED" "Run status wrong."
Require-True ([int]$run.manualCycleExecutionCount -eq 1) "PMS_EMS_OMS_R029_FAIL_MULTIPLE_CYCLES_RUN" "Manual cycle execution count not one."
Require-False ([bool]$run.multipleCyclesRun) "PMS_EMS_OMS_R029_FAIL_MULTIPLE_CYCLES_RUN" "Multiple cycles ran."
foreach ($property in @("startedSchedulerServicePolling", "usedBrokerOrLiveMarketData", "paperLedgerStateCommittedOrMutated", "createdOrder", "createdFill", "createdExecutionReport", "createdBrokerRoute", "submittedOrder", "mutatedLivePositionState", "mutatedBrokerPositionState", "mutatedProductionLedgerState", "mutatedTradingState")) {
    Require-False ([bool]$run.$property) "PMS_EMS_OMS_R029_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Run unsafe flag: $property"
}

Require-True ([bool]$baseline.paperBaselineInputCreated) "PMS_EMS_OMS_R029_FAIL_TARGET_CURRENT_DIFF_MISSING" "Baseline input missing."
Require-True (@($baseline.lines | Where-Object { $_.symbol -eq "AUDUSD" -and [decimal]$_.currentPaperQuantity -eq 131000 }).Count -eq 1) "PMS_EMS_OMS_R029_FAIL_TARGET_CURRENT_DIFF_MISSING" "AUDUSD baseline missing."
Require-True (@($baseline.lines | Where-Object { $_.symbol -eq "EURUSD" -and [decimal]$_.currentPaperQuantity -eq 124000 }).Count -eq 1) "PMS_EMS_OMS_R029_FAIL_TARGET_CURRENT_DIFF_MISSING" "EURUSD baseline missing."
Require-True (@($baseline.lines | Where-Object { $_.symbol -eq "GBPUSD" -and [decimal]$_.currentPaperQuantity -eq -368000 }).Count -eq 1) "PMS_EMS_OMS_R029_FAIL_TARGET_CURRENT_DIFF_MISSING" "GBPUSD baseline missing."
Require-False ([bool]$baseline.baselineIsProduction) "PMS_EMS_OMS_R029_FAIL_PRODUCTION_LEDGER_MUTATION" "Baseline production."
Require-False ([bool]$baseline.baselineIsBroker) "PMS_EMS_OMS_R029_FAIL_BROKER_POSITION_MUTATION" "Baseline broker."

Require-True ([bool]$qubes.qubesLineageCreated) "PMS_EMS_OMS_R029_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage missing."
Require-True ([int]$qubes.rawRowCount -eq 3) "PMS_EMS_OMS_R029_FAIL_QUBES_LINEAGE_WEAKENED" "Wrong raw count."
Require-True ([int]$qubes.normalizedRowCount -eq 3) "PMS_EMS_OMS_R029_FAIL_QUBES_LINEAGE_WEAKENED" "Wrong normalized count."
Require-True ([bool]$qubes.modelWeightBatchLinked) "PMS_EMS_OMS_R029_FAIL_QUBES_LINEAGE_WEAKENED" "ModelWeightBatch not linked."
Require-True ([bool]$qubes.modelRunLinked) "PMS_EMS_OMS_R029_FAIL_QUBES_LINEAGE_WEAKENED" "ModelRun not linked."
Require-True ([bool]$qubes.targetWeightsLinked) "PMS_EMS_OMS_R029_FAIL_QUBES_LINEAGE_WEAKENED" "TargetWeights not linked."

Require-True ([bool]$target.targetPortfolioCreated) "PMS_EMS_OMS_R029_FAIL_TARGET_CURRENT_DIFF_MISSING" "Target portfolio missing."
Require-True (@($target.lines | Where-Object { $_.symbol -eq "AUDUSD" -and [decimal]$_.targetNotional -eq 150000 }).Count -eq 1) "PMS_EMS_OMS_R029_FAIL_TARGET_CURRENT_DIFF_MISSING" "AUDUSD target missing."
Require-True (@($target.lines | Where-Object { $_.symbol -eq "EURUSD" -and [decimal]$_.targetNotional -eq 150000 }).Count -eq 1) "PMS_EMS_OMS_R029_FAIL_TARGET_CURRENT_DIFF_MISSING" "EURUSD target missing."
Require-True (@($target.lines | Where-Object { $_.symbol -eq "GBPUSD" -and [decimal]$_.targetNotional -eq -260000 }).Count -eq 1) "PMS_EMS_OMS_R029_FAIL_TARGET_CURRENT_DIFF_MISSING" "GBPUSD target missing."

Require-True ([bool]$diff.targetVsCurrentDiffCreated) "PMS_EMS_OMS_R029_FAIL_TARGET_CURRENT_DIFF_MISSING" "Diff missing."
Require-True ([bool]$diff.deltasComputedRelativeToPaperBaseline) "PMS_EMS_OMS_R029_FAIL_TARGET_CURRENT_DIFF_MISSING" "Diff not relative to baseline."
Require-True (@($diff.lines | Where-Object { $_.symbol -eq "AUDUSD" -and [decimal]$_.deltaNotional -eq 17690 }).Count -eq 1) "PMS_EMS_OMS_R029_FAIL_TARGET_CURRENT_DIFF_MISSING" "AUDUSD diff wrong."
Require-True (@($diff.lines | Where-Object { $_.symbol -eq "EURUSD" -and [decimal]$_.deltaNotional -eq 12236 }).Count -eq 1) "PMS_EMS_OMS_R029_FAIL_TARGET_CURRENT_DIFF_MISSING" "EURUSD diff wrong."
Require-True (@($diff.lines | Where-Object { $_.symbol -eq "GBPUSD" -and [decimal]$_.deltaNotional -eq 213616 }).Count -eq 1) "PMS_EMS_OMS_R029_FAIL_TARGET_CURRENT_DIFF_MISSING" "GBPUSD diff wrong."

Require-True ([bool]$pnl.theoreticalPnlCreated) "PMS_EMS_OMS_R029_FAIL_TARGET_CURRENT_DIFF_MISSING" "PnL missing."
Require-False ([bool]$pnl.usedLiveMarketData) "PMS_EMS_OMS_R029_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "PnL used live market data."
Require-True ([bool]$reconciliation.reconciliationCreated) "PMS_EMS_OMS_R029_FAIL_TARGET_CURRENT_DIFF_MISSING" "Reconciliation missing."
Require-True ([bool]$theoreticalVsReal.theoreticalVsRealCreated) "PMS_EMS_OMS_R029_FAIL_TARGET_CURRENT_DIFF_MISSING" "Theoretical-vs-real missing."
Require-False ([bool]$theoreticalVsReal.liveReconciliationClaim) "PMS_EMS_OMS_R029_FAIL_TRADING_STATE_MUTATION" "Live reconciliation claim."

Require-True ([bool]$intents.rebalanceIntentsCreated) "PMS_EMS_OMS_R029_FAIL_REBALANCE_INTENT_EXECUTABLE" "Intents missing."
Require-True ([bool]$intents.rebalanceIntentsRemainNonExecutable) "PMS_EMS_OMS_R029_FAIL_REBALANCE_INTENT_EXECUTABLE" "Intents executable."
foreach ($intent in @($intents.intents)) {
    Require-False ([bool]$intent.isExecutable) "PMS_EMS_OMS_R029_FAIL_REBALANCE_INTENT_EXECUTABLE" "Executable intent."
}
Require-True ([bool]$intentAudit.nonExecutableIntentAuditCreated) "PMS_EMS_OMS_R029_FAIL_REBALANCE_INTENT_EXECUTABLE" "Intent audit missing."
Require-False ([bool]$intentAudit.executableRebalanceIntentDetected) "PMS_EMS_OMS_R029_FAIL_REBALANCE_INTENT_EXECUTABLE" "Executable intent detected."

Require-True ([bool]$report.operatorCycleReportCreated) "PMS_EMS_OMS_R029_FAIL_PREFLIGHT_MISSING_OR_FAILED" "Report missing."
foreach ($property in @("includesManualNoExternalDisclaimer", "includesNoSchedulerServicePollingDisclaimer", "includesNoBrokerCallDisclaimer", "includesNoLiveMarketDataDisclaimer", "includesNoPaperLedgerCommitDisclaimer", "includesNoOrderDisclaimer", "includesNoFillDisclaimer", "includesNoExecutionReportDisclaimer", "includesNoRouteDisclaimer", "includesNoSubmissionDisclaimer")) {
    Require-True ([bool]$report.$property) "PMS_EMS_OMS_R029_FAIL_PREFLIGHT_MISSING_OR_FAILED" "Report disclaimer missing: $property"
}
$reportMarkdown = Get-Content -LiteralPath (Join-Path $artifactRoot "phase-pms-ems-oms-r029-operator-cycle-report.md") -Raw
foreach ($phrase in @("executed exactly once", "No scheduler", "No broker calls", "No live market data", "No paper ledger commit in R029", "No orders", "No fills", "No execution reports", "No broker routes", "No submissions")) {
    if ($reportMarkdown -notmatch [regex]::Escape($phrase)) {
        Fail-Gate "PMS_EMS_OMS_R029_FAIL_PREFLIGHT_MISSING_OR_FAILED" "Markdown report missing phrase: $phrase"
    }
}

Require-True ([bool]$schedulerAudit.auditCreated) "PMS_EMS_OMS_R029_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler audit missing."
foreach ($property in @("schedulerStarted", "serviceStarted", "pollingStarted", "timerStarted", "backgroundJobStarted", "automaticExecutionIntroduced")) {
    Require-False ([bool]$schedulerAudit.$property) "PMS_EMS_OMS_R029_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler/service detected: $property"
}
Require-True ([bool]$paperLedgerAudit.auditCreated) "PMS_EMS_OMS_R029_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Paper ledger audit missing."
Require-False ([bool]$paperLedgerAudit.paperLedgerStateCommitted) "PMS_EMS_OMS_R029_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Paper ledger committed."
Require-False ([bool]$paperLedgerAudit.paperLedgerStateMutated) "PMS_EMS_OMS_R029_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Paper ledger mutated."
Require-True ([bool]$liveAudit.auditCreated) "PMS_EMS_OMS_R029_FAIL_LIVE_POSITION_MUTATION" "Live audit missing."
Require-False ([bool]$liveAudit.livePositionStateMutated) "PMS_EMS_OMS_R029_FAIL_LIVE_POSITION_MUTATION" "Live position mutated."
Require-True ([bool]$brokerAudit.auditCreated) "PMS_EMS_OMS_R029_FAIL_BROKER_POSITION_MUTATION" "Broker audit missing."
Require-False ([bool]$brokerAudit.brokerPositionStateMutated) "PMS_EMS_OMS_R029_FAIL_BROKER_POSITION_MUTATION" "Broker position mutated."
Require-True ([bool]$productionAudit.auditCreated) "PMS_EMS_OMS_R029_FAIL_PRODUCTION_LEDGER_MUTATION" "Production audit missing."
Require-False ([bool]$productionAudit.productionLedgerStateMutated) "PMS_EMS_OMS_R029_FAIL_PRODUCTION_LEDGER_MUTATION" "Production ledger mutated."
Require-True ([bool]$tradingAudit.auditCreated) "PMS_EMS_OMS_R029_FAIL_TRADING_STATE_MUTATION" "Trading audit missing."
Require-False ([bool]$tradingAudit.tradingStateMutated) "PMS_EMS_OMS_R029_FAIL_TRADING_STATE_MUTATION" "Trading state mutated."
Require-True ([bool]$fillAudit.auditCreated) "PMS_EMS_OMS_R029_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill audit missing."
Require-False ([bool]$fillAudit.fillCreated) "PMS_EMS_OMS_R029_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill created."
Require-False ([bool]$fillAudit.executionReportCreated) "PMS_EMS_OMS_R029_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Execution report created."
Require-True ([bool]$orderAudit.auditCreated) "PMS_EMS_OMS_R029_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order audit missing."
Require-False ([bool]$orderAudit.executableOrderCreated) "PMS_EMS_OMS_R029_FAIL_EXECUTABLE_ORDER_CREATED" "Executable order created."
Require-False ([bool]$orderAudit.omsOrderCreated) "PMS_EMS_OMS_R029_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "OMS order created."
Require-True ([bool]$routeAudit.auditCreated) "PMS_EMS_OMS_R029_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Route audit missing."
Require-False ([bool]$routeAudit.brokerRouteCreated) "PMS_EMS_OMS_R029_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Broker route created."
Require-False ([bool]$routeAudit.ordersSubmitted) "PMS_EMS_OMS_R029_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders submitted."

Require-True ([bool]$idempotency.idempotencyEvidenceCreated) "PMS_EMS_OMS_R029_FAIL_PREFLIGHT_MISSING_OR_FAILED" "Idempotency missing."
Require-True ([string]$idempotency.duplicateRequestedCycleRunIdBehavior -eq "DuplicateReturned") "PMS_EMS_OMS_R029_FAIL_PREFLIGHT_MISSING_OR_FAILED" "Duplicate behavior wrong."
Require-False ([bool]$idempotency.duplicateRunsSecondCycle) "PMS_EMS_OMS_R029_FAIL_MULTIPLE_CYCLES_RUN" "Duplicate runs second cycle."

Require-True ([bool]$lineage.lineagePreservationCreated) "PMS_EMS_OMS_R029_FAIL_QUBES_LINEAGE_WEAKENED" "Lineage missing."
foreach ($property in @("qubesLineagePreserved", "cycleLineagePreserved", "paperBaselineLineagePreserved", "r028ContractLineagePreserved", "r027ContinuityLineagePreserved", "ledgerStateArchiveLineagePreserved", "ledgerCommitLineagePreserved", "ledgerPreviewLineagePreserved", "positionPreviewLineagePreserved", "simulationResultLineagePreserved", "simulationPlanLineagePreserved", "executionPlanLineagePreserved", "paperCandidateLineagePreserved", "riskLineagePreserved", "rebalanceIntentLineagePreserved", "lotSizingLineagePreserved")) {
    Require-True ([bool]$lineage.$property) "PMS_EMS_OMS_R029_FAIL_QUBES_LINEAGE_WEAKENED" "Lineage flag missing: $property"
}
Require-False ([bool]$lineage.lineageWeakened) "PMS_EMS_OMS_R029_FAIL_QUBES_LINEAGE_WEAKENED" "Lineage weakened."

Require-True ([bool]$universe.instrumentUniverseHandlingCreated) "PMS_EMS_OMS_R029_FAIL_AUDUSD_MISCLASSIFIED" "Universe missing."
Require-False ([bool]$universe.audusdClassifiedAsFailed) "PMS_EMS_OMS_R029_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD failed."
Require-True ([bool]$usdjpy.usdJpyCaveatPreservationCreated) "PMS_EMS_OMS_R029_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY missing."
Require-True ([string]$usdjpy.usdJpySecurityId -eq "4004") "PMS_EMS_OMS_R029_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID wrong."
Require-True ([string]$usdjpy.usdJpySecurityIdSource -eq "8") "PMS_EMS_OMS_R029_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource wrong."
Require-False ([bool]$usdjpy.usdJpyCaveatWeakened) "PMS_EMS_OMS_R029_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat weakened."
Require-True ([bool]$lmax.lmaxReadonlyBaselineReferenceCreated) "PMS_EMS_OMS_R029_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX missing."
Require-False ([bool]$lmax.lmaxUsedInThisPhase) "PMS_EMS_OMS_R029_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX used."
Require-False ([bool]$lmax.lmaxCalledInThisPhase) "PMS_EMS_OMS_R029_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX called."

foreach ($property in @("externalBrokerActivationDetected", "boundaryRuntimeActionDetected", "liveMarketDataAttempted", "apiStarted", "workerStarted", "schedulerPollingServiceTimerBackgroundJobStartedOrIntroduced", "liveGatewayEnabled", "multipleCyclesRun", "replayOrShadowReplayIntroduced", "secretsOrCredentialsSerialized", "rawFixSerialized", "rawEndpointTlsValuesSerialized", "sessionIdsSerialized", "compIdsSerialized", "rawMdReqIdSerialized", "rawBrokerMarketDataPayloadsOrPricesSerialized", "rawMarketDataFixturePayloadsSerializedBeyondApprovedSafeSummaries")) {
    Require-False ([bool]$noExternal.$property) "PMS_EMS_OMS_R029_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected: $property"
}
foreach ($property in @("brokerActivation", "socketTlsFix", "liveMarketData", "apiWorkerSchedulerService", "timersPollingBackgroundJobs", "automaticExecution", "ordersRoutesSubmissions", "fillsExecutionReports", "liveTradingPath", "livePositionMutation", "brokerPositionMutation", "productionLedgerMutation", "tradingStateMutation", "paperLedgerCommitOrMutation", "multipleCyclesRun", "replayShadowReplay", "secretOrRawPayloadSerialization")) {
    Require-False ([bool]$forbidden.$property) "PMS_EMS_OMS_R029_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $property"
}

Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R029_FAIL_BUILD_OR_TESTS" "Build/test evidence missing."
Require-True ([string]$evidence.dotnetBuildNoRestore -eq "PASS") "PMS_EMS_OMS_R029_FAIL_BUILD_OR_TESTS" "Build failed."
Require-True ([string]$evidence.focusedTests -like "PASS*") "PMS_EMS_OMS_R029_FAIL_BUILD_OR_TESTS" "Focused tests failed."
Require-True ([string]$evidence.unitTests -like "PASS*") "PMS_EMS_OMS_R029_FAIL_BUILD_OR_TESTS" "Unit tests failed."

Write-Host "PMS_EMS_OMS_R029_PASS_MANUAL_PAPER_CYCLE_FIXTURE_EXECUTED_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R029_PASS_ROLLING_PAPER_CYCLE_MANUAL_RUN_READY_NO_EXTERNAL"
