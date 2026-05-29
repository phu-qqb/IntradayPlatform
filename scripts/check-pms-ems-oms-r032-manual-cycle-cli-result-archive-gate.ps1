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
    "phase-pms-ems-oms-r032-summary.md" = "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r032-cli-invocation-result-archive-contract.json" = "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r032-cli-invocation-result-archive.json" = "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r032-operator-cli-report.md" = "PMS_EMS_OMS_R032_FAIL_OPERATOR_CLI_REPORT_MISSING"
    "phase-pms-ems-oms-r032-operator-cli-report.json" = "PMS_EMS_OMS_R032_FAIL_OPERATOR_CLI_REPORT_MISSING"
    "phase-pms-ems-oms-r032-paper-baseline-input-archive.json" = "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r032-qubes-lineage-archive.json" = "PMS_EMS_OMS_R032_FAIL_QUBES_LINEAGE_WEAKENED"
    "phase-pms-ems-oms-r032-target-portfolio-archive.json" = "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r032-target-vs-current-diff-archive.json" = "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r032-theoretical-pnl-archive.json" = "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r032-reconciliation-archive.json" = "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r032-theoretical-vs-real-archive.json" = "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r032-rebalance-intents-archive.json" = "PMS_EMS_OMS_R032_FAIL_REBALANCE_INTENT_EXECUTABLE"
    "phase-pms-ems-oms-r032-non-executable-intent-audit.json" = "PMS_EMS_OMS_R032_FAIL_REBALANCE_INTENT_EXECUTABLE"
    "phase-pms-ems-oms-r032-cli-repeated-use-readiness-gate.json" = "PMS_EMS_OMS_R032_FAIL_REPEATED_USE_GATE_MISSING"
    "phase-pms-ems-oms-r032-no-scheduler-service-polling-audit.json" = "PMS_EMS_OMS_R032_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
    "phase-pms-ems-oms-r032-no-automatic-execution-audit.json" = "PMS_EMS_OMS_R032_FAIL_AUTOMATIC_EXECUTION_INTRODUCED"
    "phase-pms-ems-oms-r032-no-paper-ledger-commit-audit.json" = "PMS_EMS_OMS_R032_FAIL_PAPER_LEDGER_COMMIT_OCCURRED"
    "phase-pms-ems-oms-r032-no-live-position-mutation-audit.json" = "PMS_EMS_OMS_R032_FAIL_LIVE_POSITION_MUTATION"
    "phase-pms-ems-oms-r032-no-broker-position-mutation-audit.json" = "PMS_EMS_OMS_R032_FAIL_BROKER_POSITION_MUTATION"
    "phase-pms-ems-oms-r032-no-production-ledger-mutation-audit.json" = "PMS_EMS_OMS_R032_FAIL_PRODUCTION_LEDGER_MUTATION"
    "phase-pms-ems-oms-r032-no-trading-state-mutation-audit.json" = "PMS_EMS_OMS_R032_FAIL_TRADING_STATE_MUTATION"
    "phase-pms-ems-oms-r032-no-fill-no-execution-report-audit.json" = "PMS_EMS_OMS_R032_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
    "phase-pms-ems-oms-r032-no-order-created-audit.json" = "PMS_EMS_OMS_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r032-no-route-no-submission-audit.json" = "PMS_EMS_OMS_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r032-idempotency-evidence.json" = "PMS_EMS_OMS_R032_FAIL_CLI_GUARD_WEAKENED"
    "phase-pms-ems-oms-r032-lineage-preservation.json" = "PMS_EMS_OMS_R032_FAIL_QUBES_LINEAGE_WEAKENED"
    "phase-pms-ems-oms-r032-instrument-universe-handling.json" = "PMS_EMS_OMS_R032_FAIL_AUDUSD_MISCLASSIFIED"
    "phase-pms-ems-oms-r032-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R032_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r032-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R032_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r032-no-external-audit.json" = "PMS_EMS_OMS_R032_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r032-forbidden-actions-audit.json" = "PMS_EMS_OMS_R032_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r032-next-phase-recommendation.json" = "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r032-build-test-validator-evidence.json" = "PMS_EMS_OMS_R032_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $entry.Key))) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$contract = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-cli-invocation-result-archive-contract.json") "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING"
$archive = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-cli-invocation-result-archive.json") "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING"
$report = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-operator-cli-report.json") "PMS_EMS_OMS_R032_FAIL_OPERATOR_CLI_REPORT_MISSING"
$baseline = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-paper-baseline-input-archive.json") "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING"
$qubes = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-qubes-lineage-archive.json") "PMS_EMS_OMS_R032_FAIL_QUBES_LINEAGE_WEAKENED"
$target = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-target-portfolio-archive.json") "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING"
$diff = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-target-vs-current-diff-archive.json") "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING"
$pnl = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-theoretical-pnl-archive.json") "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING"
$reconciliation = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-reconciliation-archive.json") "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING"
$theoreticalVsReal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-theoretical-vs-real-archive.json") "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING"
$intents = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-rebalance-intents-archive.json") "PMS_EMS_OMS_R032_FAIL_REBALANCE_INTENT_EXECUTABLE"
$intentAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-non-executable-intent-audit.json") "PMS_EMS_OMS_R032_FAIL_REBALANCE_INTENT_EXECUTABLE"
$gate = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-cli-repeated-use-readiness-gate.json") "PMS_EMS_OMS_R032_FAIL_REPEATED_USE_GATE_MISSING"
$schedulerAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-no-scheduler-service-polling-audit.json") "PMS_EMS_OMS_R032_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED"
$automaticAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-no-automatic-execution-audit.json") "PMS_EMS_OMS_R032_FAIL_AUTOMATIC_EXECUTION_INTRODUCED"
$paperLedgerAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-no-paper-ledger-commit-audit.json") "PMS_EMS_OMS_R032_FAIL_PAPER_LEDGER_COMMIT_OCCURRED"
$liveAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-no-live-position-mutation-audit.json") "PMS_EMS_OMS_R032_FAIL_LIVE_POSITION_MUTATION"
$brokerAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-no-broker-position-mutation-audit.json") "PMS_EMS_OMS_R032_FAIL_BROKER_POSITION_MUTATION"
$productionAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-no-production-ledger-mutation-audit.json") "PMS_EMS_OMS_R032_FAIL_PRODUCTION_LEDGER_MUTATION"
$tradingAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-no-trading-state-mutation-audit.json") "PMS_EMS_OMS_R032_FAIL_TRADING_STATE_MUTATION"
$fillAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-no-fill-no-execution-report-audit.json") "PMS_EMS_OMS_R032_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$orderAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-no-order-created-audit.json") "PMS_EMS_OMS_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$routeAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-no-route-no-submission-audit.json") "PMS_EMS_OMS_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$idempotency = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-idempotency-evidence.json") "PMS_EMS_OMS_R032_FAIL_CLI_GUARD_WEAKENED"
$lineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-lineage-preservation.json") "PMS_EMS_OMS_R032_FAIL_QUBES_LINEAGE_WEAKENED"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-instrument-universe-handling.json") "PMS_EMS_OMS_R032_FAIL_AUDUSD_MISCLASSIFIED"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R032_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R032_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-no-external-audit.json") "PMS_EMS_OMS_R032_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-forbidden-actions-audit.json") "PMS_EMS_OMS_R032_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r032-build-test-validator-evidence.json") "PMS_EMS_OMS_R032_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$contract.cliInvocationResultArchiveContractCreated) "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING" "Contract missing."
Require-True ([bool]$contract.requiresManualNoExternal) "PMS_EMS_OMS_R032_FAIL_CLI_GUARD_WEAKENED" "ManualNoExternal not required."
Require-True ([bool]$contract.requiresPreflightReadyNoExternal) "PMS_EMS_OMS_R032_FAIL_PREFLIGHT_WEAKENED" "Preflight not required."
Require-True ([bool]$contract.requiresExactlyOneCliInvocation) "PMS_EMS_OMS_R032_FAIL_MULTIPLE_CLI_INVOCATIONS" "Multiple CLI invocations allowed."
Require-True ([bool]$contract.requiresExactlyOneCycleExecution) "PMS_EMS_OMS_R032_FAIL_MULTIPLE_CYCLES_RUN" "Multiple cycles allowed."
Require-True ([bool]$contract.doesNotCommitPaperLedger) "PMS_EMS_OMS_R032_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Paper commit allowed."
Require-True ([bool]$contract.doesNotAuthorizeSchedulerServicePollingAutomaticExecution) "PMS_EMS_OMS_R032_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler authorized."
Require-True ([bool]$contract.doesNotAuthorizeBrokerLiveTradingOrdersFillsReportsRoutesSubmissions) "PMS_EMS_OMS_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Trading path authorized."

Require-True ([bool]$archive.archiveCreated) "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING" "Archive missing."
Require-True ([string]$archive.archiveStatus -eq "ArchivedNoExternal") "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING" "Archive status wrong."
Require-True ([string]$archive.requestedCycleRunId -eq "cycle-r032-cli-manual-paper-fixture") "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING" "Cycle id wrong."
Require-True ([string]$archive.qubesRunId -eq "qubes-r032-cli-fixture") "PMS_EMS_OMS_R032_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes id wrong."
Require-True ([string]$archive.runMode -eq "ManualNoExternal") "PMS_EMS_OMS_R032_FAIL_CLI_GUARD_WEAKENED" "Mode wrong."
Require-True ([string]$archive.preflightStatus -eq "ReadyNoExternal") "PMS_EMS_OMS_R032_FAIL_PREFLIGHT_WEAKENED" "Preflight wrong."
Require-True ([int]$archive.executionCount -eq 1) "PMS_EMS_OMS_R032_FAIL_MULTIPLE_CYCLES_RUN" "Wrong execution count."
Require-True ([int]$archive.cliInvocationCount -eq 1) "PMS_EMS_OMS_R032_FAIL_MULTIPLE_CLI_INVOCATIONS" "Wrong CLI invocation count."
Require-False ([bool]$archive.moreThanOneCliInvocationRun) "PMS_EMS_OMS_R032_FAIL_MULTIPLE_CLI_INVOCATIONS" "More than one CLI invocation."
Require-False ([bool]$archive.moreThanOneCycleRun) "PMS_EMS_OMS_R032_FAIL_MULTIPLE_CYCLES_RUN" "More than one cycle."
Require-False ([bool]$archive.newQubesBatchOutsideCliIngested) "PMS_EMS_OMS_R032_FAIL_NEW_QUBES_BATCH_OUTSIDE_CLI" "Extra Qubes batch."
Require-False ([bool]$archive.paperLedgerCommitted) "PMS_EMS_OMS_R032_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Paper ledger committed."
Require-False ([bool]$archive.ordersCreated) "PMS_EMS_OMS_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders created."
Require-False ([bool]$archive.fillsCreated) "PMS_EMS_OMS_R032_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fills created."
Require-False ([bool]$archive.executionReportsCreated) "PMS_EMS_OMS_R032_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Reports created."
Require-False ([bool]$archive.routesCreated) "PMS_EMS_OMS_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Routes created."
Require-False ([bool]$archive.ordersSubmitted) "PMS_EMS_OMS_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders submitted."

Require-True ([bool]$report.operatorCliReportCreated) "PMS_EMS_OMS_R032_FAIL_OPERATOR_CLI_REPORT_MISSING" "Operator report missing."
Require-True ([bool]$report.includesManualCliInvocationOnlyDisclaimer) "PMS_EMS_OMS_R032_FAIL_OPERATOR_CLI_REPORT_MISSING" "Manual disclaimer missing."
Require-True ([bool]$report.includesExactlyOneCycleDisclaimer) "PMS_EMS_OMS_R032_FAIL_MULTIPLE_CYCLES_RUN" "One-cycle disclaimer missing."
Require-True ([bool]$report.includesNoSchedulerDisclaimer) "PMS_EMS_OMS_R032_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "No scheduler disclaimer missing."
Require-True ([bool]$report.includesNoServiceDisclaimer) "PMS_EMS_OMS_R032_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "No service disclaimer missing."
Require-True ([bool]$report.includesNoPollingDisclaimer) "PMS_EMS_OMS_R032_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "No polling disclaimer missing."
Require-True ([bool]$report.includesNoAutomaticExecutionDisclaimer) "PMS_EMS_OMS_R032_FAIL_AUTOMATIC_EXECUTION_INTRODUCED" "No automatic disclaimer missing."
Require-True ([bool]$report.includesNoBrokerCallDisclaimer) "PMS_EMS_OMS_R032_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No broker disclaimer missing."
Require-True ([bool]$report.includesNoLiveMarketDataDisclaimer) "PMS_EMS_OMS_R032_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No live market disclaimer missing."
Require-True ([bool]$report.includesNoOrderDisclaimer) "PMS_EMS_OMS_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No order disclaimer missing."
Require-True ([bool]$report.includesNoFillDisclaimer) "PMS_EMS_OMS_R032_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "No fill disclaimer missing."
Require-True ([bool]$report.includesNoExecutionReportDisclaimer) "PMS_EMS_OMS_R032_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "No report disclaimer missing."
Require-True ([bool]$report.includesNoRouteDisclaimer) "PMS_EMS_OMS_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No route disclaimer missing."
Require-True ([bool]$report.includesNoSubmissionDisclaimer) "PMS_EMS_OMS_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No submission disclaimer missing."
Require-True ([bool]$report.includesNoPaperLedgerCommitDisclaimer) "PMS_EMS_OMS_R032_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "No paper commit disclaimer missing."

Require-True ([bool]$baseline.paperBaselineInputArchiveCreated) "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING" "Baseline missing."
Require-True ([bool]$baseline.baselineIsProduction -eq $false) "PMS_EMS_OMS_R032_FAIL_PRODUCTION_LEDGER_MUTATION" "Baseline production."
Require-True (@($baseline.lines | Where-Object { $_.symbol -eq "AUDUSD" -and [decimal]$_.quantity -eq 131000 }).Count -eq 1) "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING" "AUDUSD baseline missing."
Require-True (@($baseline.lines | Where-Object { $_.symbol -eq "EURUSD" -and [decimal]$_.quantity -eq 124000 }).Count -eq 1) "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING" "EURUSD baseline missing."
Require-True (@($baseline.lines | Where-Object { $_.symbol -eq "GBPUSD" -and [decimal]$_.quantity -eq -368000 }).Count -eq 1) "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING" "GBPUSD baseline missing."

Require-True ([bool]$qubes.qubesLineageArchiveCreated) "PMS_EMS_OMS_R032_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage missing."
Require-True ([int]$qubes.rawRowCount -eq 3) "PMS_EMS_OMS_R032_FAIL_QUBES_LINEAGE_WEAKENED" "Raw row count wrong."
Require-True ([int]$qubes.normalizedRowCount -eq 3) "PMS_EMS_OMS_R032_FAIL_QUBES_LINEAGE_WEAKENED" "Normalized row count wrong."
Require-True ([bool]$qubes.inputIsFixtureNoExternal) "PMS_EMS_OMS_R032_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Qubes input not fixture."
Require-False ([bool]$qubes.rawPayloadSerialized) "PMS_EMS_OMS_R032_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Raw payload serialized."
Require-True ([bool]$target.targetPortfolioArchiveCreated) "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING" "Target missing."
Require-True ([bool]$diff.targetVsCurrentDiffArchiveCreated) "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING" "Diff missing."
Require-True ([bool]$diff.diffRelativeToPaperBaseline) "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING" "Diff not relative to paper baseline."
Require-True (@($diff.lines | Where-Object { $_.symbol -eq "AUDUSD" -and [decimal]$_.deltaNotional -eq 17690 }).Count -eq 1) "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING" "AUDUSD diff missing."
Require-True (@($diff.lines | Where-Object { $_.symbol -eq "EURUSD" -and [decimal]$_.deltaNotional -eq 12236 }).Count -eq 1) "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING" "EURUSD diff missing."
Require-True (@($diff.lines | Where-Object { $_.symbol -eq "GBPUSD" -and [decimal]$_.deltaNotional -eq 213616 }).Count -eq 1) "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING" "GBPUSD diff missing."
Require-True ([bool]$pnl.theoreticalPnlArchiveCreated) "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING" "PnL missing."
Require-True ([bool]$reconciliation.reconciliationArchiveCreated) "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING" "Reconciliation missing."
Require-True ([bool]$theoreticalVsReal.theoreticalVsRealArchiveCreated) "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING" "Theoretical-vs-real missing."
Require-False ([bool]$theoreticalVsReal.liveBrokerDataUsed) "PMS_EMS_OMS_R032_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Live broker data used."
Require-True ([bool]$intents.rebalanceIntentsArchiveCreated) "PMS_EMS_OMS_R032_FAIL_REBALANCE_INTENT_EXECUTABLE" "Intents missing."
Require-True ([bool]$intents.intentsRemainNonExecutable) "PMS_EMS_OMS_R032_FAIL_REBALANCE_INTENT_EXECUTABLE" "Intent executable."
Require-False ([bool]$intents.orderCandidatesCreated) "PMS_EMS_OMS_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order candidates created."
Require-True ([bool]$intentAudit.auditCreated) "PMS_EMS_OMS_R032_FAIL_REBALANCE_INTENT_EXECUTABLE" "Intent audit missing."
Require-False ([bool]$intentAudit.executableIntentCreated) "PMS_EMS_OMS_R032_FAIL_REBALANCE_INTENT_EXECUTABLE" "Executable intent created."
Require-False ([bool]$intentAudit.orderPathIntroduced) "PMS_EMS_OMS_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order path introduced."

Require-True ([bool]$gate.repeatedUseReadinessGateCreated) "PMS_EMS_OMS_R032_FAIL_REPEATED_USE_GATE_MISSING" "Repeated-use gate missing."
Require-True ([string]$gate.readinessStatus -eq "ManualCliReadyForRepeatedOperatorUseNoExternal") "PMS_EMS_OMS_R032_FAIL_REPEATED_USE_GATE_MISSING" "Gate status wrong."
Require-False ([bool]$gate.authorizesScheduler) "PMS_EMS_OMS_R032_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Gate authorizes scheduler."
Require-False ([bool]$gate.authorizesService) "PMS_EMS_OMS_R032_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Gate authorizes service."
Require-False ([bool]$gate.authorizesPolling) "PMS_EMS_OMS_R032_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Gate authorizes polling."
Require-False ([bool]$gate.authorizesAutomaticExecution) "PMS_EMS_OMS_R032_FAIL_AUTOMATIC_EXECUTION_INTRODUCED" "Gate authorizes automatic execution."
Require-False ([bool]$gate.authorizesBrokerCall) "PMS_EMS_OMS_R032_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Gate authorizes broker."
Require-False ([bool]$gate.authorizesLiveTrading) "PMS_EMS_OMS_R032_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Gate authorizes live trading."
Require-False ([bool]$gate.authorizesOrderCreationOrSubmission) "PMS_EMS_OMS_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Gate authorizes orders."
Require-False ([bool]$gate.authorizesFills) "PMS_EMS_OMS_R032_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Gate authorizes fills."
Require-False ([bool]$gate.authorizesExecutionReports) "PMS_EMS_OMS_R032_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Gate authorizes reports."
Require-False ([bool]$gate.authorizesPaperLedgerCommit) "PMS_EMS_OMS_R032_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Gate authorizes paper commit."
Require-False ([bool]$gate.runsAnotherCycle) "PMS_EMS_OMS_R032_FAIL_MULTIPLE_CYCLES_RUN" "Gate runs another cycle."

Require-False ([bool]$schedulerAudit.schedulerIntroduced) "PMS_EMS_OMS_R032_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler introduced."
Require-False ([bool]$schedulerAudit.serviceIntroduced) "PMS_EMS_OMS_R032_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Service introduced."
Require-False ([bool]$schedulerAudit.pollingIntroduced) "PMS_EMS_OMS_R032_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Polling introduced."
Require-False ([bool]$schedulerAudit.timerIntroduced) "PMS_EMS_OMS_R032_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Timer introduced."
Require-False ([bool]$schedulerAudit.backgroundJobIntroduced) "PMS_EMS_OMS_R032_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Background job introduced."
Require-False ([bool]$automaticAudit.automaticExecutionIntroduced) "PMS_EMS_OMS_R032_FAIL_AUTOMATIC_EXECUTION_INTRODUCED" "Automatic execution introduced."
Require-True ([int]$automaticAudit.cliInvocationCount -eq 1) "PMS_EMS_OMS_R032_FAIL_MULTIPLE_CLI_INVOCATIONS" "Wrong CLI invocation count."
Require-True ([int]$automaticAudit.manualCycleExecutionCount -eq 1) "PMS_EMS_OMS_R032_FAIL_MULTIPLE_CYCLES_RUN" "Wrong manual cycle count."
Require-False ([bool]$paperLedgerAudit.paperLedgerCommitOccurred) "PMS_EMS_OMS_R032_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Paper ledger commit."
Require-False ([bool]$paperLedgerAudit.paperLedgerStateMutated) "PMS_EMS_OMS_R032_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Paper ledger mutated."
Require-False ([bool]$liveAudit.livePositionStateMutated) "PMS_EMS_OMS_R032_FAIL_LIVE_POSITION_MUTATION" "Live position mutated."
Require-False ([bool]$brokerAudit.brokerPositionStateMutated) "PMS_EMS_OMS_R032_FAIL_BROKER_POSITION_MUTATION" "Broker position mutated."
Require-False ([bool]$productionAudit.productionLedgerStateMutated) "PMS_EMS_OMS_R032_FAIL_PRODUCTION_LEDGER_MUTATION" "Production ledger mutated."
Require-False ([bool]$tradingAudit.tradingStateMutated) "PMS_EMS_OMS_R032_FAIL_TRADING_STATE_MUTATION" "Trading state mutated."
Require-False ([bool]$fillAudit.fillsCreated) "PMS_EMS_OMS_R032_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fills created."
Require-False ([bool]$fillAudit.executionReportsCreated) "PMS_EMS_OMS_R032_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Reports created."
Require-False ([bool]$orderAudit.ordersCreated) "PMS_EMS_OMS_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders created."
Require-False ([bool]$orderAudit.executableOrdersCreated) "PMS_EMS_OMS_R032_FAIL_EXECUTABLE_ORDER_CREATED" "Executable orders created."
Require-False ([bool]$routeAudit.brokerRoutesCreated) "PMS_EMS_OMS_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Routes created."
Require-False ([bool]$routeAudit.ordersSubmitted) "PMS_EMS_OMS_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders submitted."

Require-True ([bool]$idempotency.idempotencyEvidenceCreated) "PMS_EMS_OMS_R032_FAIL_CLI_GUARD_WEAKENED" "Idempotency missing."
Require-True ([string]$idempotency.duplicateRequestedCycleRunIdBehavior -eq "DuplicateReturned") "PMS_EMS_OMS_R032_FAIL_CLI_GUARD_WEAKENED" "Duplicate cycle wrong."
Require-True ([string]$idempotency.duplicateArchiveBehavior -eq "DuplicateReturned") "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING" "Duplicate archive wrong."
Require-False ([bool]$idempotency.duplicateRunsMoreThanOneCycle) "PMS_EMS_OMS_R032_FAIL_MULTIPLE_CYCLES_RUN" "Duplicate runs more than one."
Require-False ([bool]$idempotency.duplicateCommitsPaperLedger) "PMS_EMS_OMS_R032_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Duplicate commits paper ledger."

Require-True ([bool]$lineage.lineagePreservationCreated) "PMS_EMS_OMS_R032_FAIL_QUBES_LINEAGE_WEAKENED" "Lineage missing."
Require-True ([bool]$lineage.qubesLineagePreserved) "PMS_EMS_OMS_R032_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage weakened."
Require-True ([bool]$lineage.r028ContractLineagePreserved) "PMS_EMS_OMS_R032_FAIL_QUBES_LINEAGE_WEAKENED" "R028 lineage missing."
Require-True ([bool]$lineage.r030ManualRollingReadinessLineagePreserved) "PMS_EMS_OMS_R032_FAIL_QUBES_LINEAGE_WEAKENED" "R030 lineage missing."
Require-True ([bool]$lineage.r027ContinuityGateLineagePreserved) "PMS_EMS_OMS_R032_FAIL_QUBES_LINEAGE_WEAKENED" "R027 lineage missing."
Require-True ([bool]$lineage.r025PaperBaselineLineagePreserved) "PMS_EMS_OMS_R032_FAIL_QUBES_LINEAGE_WEAKENED" "R025 lineage missing."
Require-True ([bool]$lineage.riskLineagePreserved) "PMS_EMS_OMS_R032_FAIL_QUBES_LINEAGE_WEAKENED" "Risk lineage missing."
Require-True ([bool]$lineage.lotSizingLineagePreserved) "PMS_EMS_OMS_R032_FAIL_QUBES_LINEAGE_WEAKENED" "Lot-sizing lineage missing."

Require-False ([bool]$universe.audusdMisclassifiedFailed) "PMS_EMS_OMS_R032_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD misclassified."
Require-True ([string]$universe.audusdStatus -eq "PausedTlsBoundaryInconclusiveNotFailed") "PMS_EMS_OMS_R032_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD status wrong."
Require-True ([bool]$usdjpy.usdjpyCaveatPreserved) "PMS_EMS_OMS_R032_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY missing."
Require-True ([string]$usdjpy.securityId -eq "4004") "PMS_EMS_OMS_R032_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID wrong."
Require-True ([string]$usdjpy.securityIdSource -eq "8") "PMS_EMS_OMS_R032_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource wrong."
Require-False ([bool]$usdjpy.weakened) "PMS_EMS_OMS_R032_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY weakened."
Require-True ([bool]$lmax.referenceOnly) "PMS_EMS_OMS_R032_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX not reference-only."
Require-False ([bool]$lmax.brokerCalled) "PMS_EMS_OMS_R032_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker called."

Require-False ([bool]$noExternal.brokerActivation) "PMS_EMS_OMS_R032_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker activation."
Require-False ([bool]$noExternal.socketTlsFixRuntimeAction) "PMS_EMS_OMS_R032_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Socket/TLS/FIX runtime."
Require-False ([bool]$noExternal.liveMarketRuntimeAction) "PMS_EMS_OMS_R032_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Live market runtime."
Require-False ([bool]$noExternal.apiWorkerLiveGatewayEnabled) "PMS_EMS_OMS_R032_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API/Worker live gateway."
Require-False ([bool]$noExternal.schedulerPollingServiceTimerBackgroundJob) "PMS_EMS_OMS_R032_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler/service/polling."
Require-False ([bool]$noExternal.automaticExecution) "PMS_EMS_OMS_R032_FAIL_AUTOMATIC_EXECUTION_INTRODUCED" "Automatic execution."
Require-False ([bool]$noExternal.moreThanOneCliInvocationRun) "PMS_EMS_OMS_R032_FAIL_MULTIPLE_CLI_INVOCATIONS" "Multiple CLI invocations."
Require-False ([bool]$noExternal.moreThanOneManualCycleRun) "PMS_EMS_OMS_R032_FAIL_MULTIPLE_CYCLES_RUN" "Multiple cycles."
Require-False ([bool]$noExternal.newQubesBatchOutsideCliIngested) "PMS_EMS_OMS_R032_FAIL_NEW_QUBES_BATCH_OUTSIDE_CLI" "Extra Qubes batch."
Require-False ([bool]$noExternal.paperLedgerCommit) "PMS_EMS_OMS_R032_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Paper ledger commit."
Require-False ([bool]$noExternal.liveBrokerProductionTradingMutation) "PMS_EMS_OMS_R032_FAIL_TRADING_STATE_MUTATION" "State mutation."
Require-False ([bool]$noExternal.replayOrShadowReplay) "PMS_EMS_OMS_R032_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Replay introduced."

Require-False ([bool]$forbidden.newExternalActionDetected) "PMS_EMS_OMS_R032_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden external action."
Require-False ([bool]$forbidden.schedulerOrServiceIntroduced) "PMS_EMS_OMS_R032_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Forbidden scheduler/service."
Require-False ([bool]$forbidden.automaticExecutionIntroduced) "PMS_EMS_OMS_R032_FAIL_AUTOMATIC_EXECUTION_INTRODUCED" "Forbidden automatic."
Require-False ([bool]$forbidden.multipleCliInvocations) "PMS_EMS_OMS_R032_FAIL_MULTIPLE_CLI_INVOCATIONS" "Multiple CLI invocations."
Require-False ([bool]$forbidden.multipleCyclesRun) "PMS_EMS_OMS_R032_FAIL_MULTIPLE_CYCLES_RUN" "Multiple cycles."
Require-False ([bool]$forbidden.newQubesBatchOutsideCli) "PMS_EMS_OMS_R032_FAIL_NEW_QUBES_BATCH_OUTSIDE_CLI" "Extra Qubes batch."
Require-False ([bool]$forbidden.cliGuardWeakened) "PMS_EMS_OMS_R032_FAIL_CLI_GUARD_WEAKENED" "CLI guard weakened."
Require-False ([bool]$forbidden.preflightWeakened) "PMS_EMS_OMS_R032_FAIL_PREFLIGHT_WEAKENED" "Preflight weakened."
Require-False ([bool]$forbidden.paperLedgerCommitOccurred) "PMS_EMS_OMS_R032_FAIL_PAPER_LEDGER_COMMIT_OCCURRED" "Paper commit."
Require-False ([bool]$forbidden.orderOrTradingPathIntroduced) "PMS_EMS_OMS_R032_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order path."
Require-False ([bool]$forbidden.secretOrRawPayloadSerializationRisk) "PMS_EMS_OMS_R032_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Secret/raw payload risk."

$sourceFiles = @(
    (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/ManualPaperCycleCliResultArchive.cs"),
    (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/ManualPaperCycleCliSurface.cs"),
    (Join-Path $repoRoot "tools/QQ.Production.Intraday.Tools.ManualPaperCycle/Program.cs")
)
$forbiddenPatterns = @(
    "TcpClient",
    "SslStream",
    "MarketDataRequest",
    "MarketDataResponse",
    "FixSession",
    "IHostedService",
    "BackgroundService",
    "PeriodicTimer",
    "Task.Delay",
    "System.Threading.Timer",
    "SubmitOrder",
    "SendOrderAsync"
)
foreach ($file in $sourceFiles) {
    if (-not (Test-Path -LiteralPath $file)) {
        Fail-Gate "PMS_EMS_OMS_R032_FAIL_CLI_RESULT_ARCHIVE_MISSING" "Missing source file: $file"
    }
    $text = Get-Content -LiteralPath $file -Raw
    foreach ($pattern in $forbiddenPatterns) {
        if ($text.Contains($pattern)) {
            Fail-Gate "PMS_EMS_OMS_R032_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden source token '$pattern' in $file"
        }
    }
}

Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R032_FAIL_BUILD_OR_TESTS" "Build/test evidence missing."
Require-True ([string]$evidence.dotnetBuildNoRestore -eq "PASS") "PMS_EMS_OMS_R032_FAIL_BUILD_OR_TESTS" "Build failed."
Require-True ([string]$evidence.focusedTests -like "PASS*") "PMS_EMS_OMS_R032_FAIL_BUILD_OR_TESTS" "Focused tests failed."
Require-True ([string]$evidence.unitTests -like "PASS*") "PMS_EMS_OMS_R032_FAIL_BUILD_OR_TESTS" "Unit tests failed."
Require-True ([string]$evidence.validator -eq "PASS") "PMS_EMS_OMS_R032_FAIL_BUILD_OR_TESTS" "Validator evidence missing."

Write-Host "PMS_EMS_OMS_R032_PASS_MANUAL_CLI_RESULT_ARCHIVE_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R032_PASS_OPERATOR_CLI_REPORT_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R032_PASS_REPEATED_MANUAL_USE_GATE_READY_NO_EXTERNAL"
