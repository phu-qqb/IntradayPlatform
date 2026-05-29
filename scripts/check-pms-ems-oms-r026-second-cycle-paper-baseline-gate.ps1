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
    "phase-pms-ems-oms-r026-summary.md" = "PMS_EMS_OMS_R026_FAIL_SECOND_CYCLE_MISSING"
    "phase-pms-ems-oms-r026-second-cycle-run.json" = "PMS_EMS_OMS_R026_FAIL_SECOND_CYCLE_MISSING"
    "phase-pms-ems-oms-r026-paper-baseline-input.json" = "PMS_EMS_OMS_R026_FAIL_PAPER_BASELINE_MISSING"
    "phase-pms-ems-oms-r026-second-cycle-qubes-lineage.json" = "PMS_EMS_OMS_R026_FAIL_QUBES_LINEAGE_WEAKENED"
    "phase-pms-ems-oms-r026-second-cycle-target-portfolio.json" = "PMS_EMS_OMS_R026_FAIL_SECOND_CYCLE_MISSING"
    "phase-pms-ems-oms-r026-second-cycle-current-paper-baseline.json" = "PMS_EMS_OMS_R026_FAIL_PAPER_BASELINE_MISSING"
    "phase-pms-ems-oms-r026-second-cycle-target-vs-current-diff.json" = "PMS_EMS_OMS_R026_FAIL_TARGET_CURRENT_DIFF_MISSING"
    "phase-pms-ems-oms-r026-second-cycle-theoretical-pnl.json" = "PMS_EMS_OMS_R026_FAIL_SECOND_CYCLE_MISSING"
    "phase-pms-ems-oms-r026-second-cycle-reconciliation.json" = "PMS_EMS_OMS_R026_FAIL_SECOND_CYCLE_MISSING"
    "phase-pms-ems-oms-r026-second-cycle-theoretical-vs-real.json" = "PMS_EMS_OMS_R026_FAIL_SECOND_CYCLE_MISSING"
    "phase-pms-ems-oms-r026-second-cycle-rebalance-intents.json" = "PMS_EMS_OMS_R026_FAIL_SECOND_CYCLE_MISSING"
    "phase-pms-ems-oms-r026-non-executable-intent-audit.json" = "PMS_EMS_OMS_R026_FAIL_REBALANCE_INTENT_EXECUTABLE"
    "phase-pms-ems-oms-r026-no-paper-ledger-commit-audit.json" = "PMS_EMS_OMS_R026_FAIL_PAPER_LEDGER_MUTATED"
    "phase-pms-ems-oms-r026-no-live-position-mutation-audit.json" = "PMS_EMS_OMS_R026_FAIL_LIVE_POSITION_MUTATION"
    "phase-pms-ems-oms-r026-no-broker-position-mutation-audit.json" = "PMS_EMS_OMS_R026_FAIL_BROKER_POSITION_MUTATION"
    "phase-pms-ems-oms-r026-no-production-ledger-mutation-audit.json" = "PMS_EMS_OMS_R026_FAIL_PRODUCTION_LEDGER_MUTATION"
    "phase-pms-ems-oms-r026-no-trading-state-mutation-audit.json" = "PMS_EMS_OMS_R026_FAIL_TRADING_STATE_MUTATION"
    "phase-pms-ems-oms-r026-no-fill-no-execution-report-audit.json" = "PMS_EMS_OMS_R026_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
    "phase-pms-ems-oms-r026-no-order-created-audit.json" = "PMS_EMS_OMS_R026_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r026-no-route-no-submission-audit.json" = "PMS_EMS_OMS_R026_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r026-idempotency-evidence.json" = "PMS_EMS_OMS_R026_FAIL_SECOND_CYCLE_MISSING"
    "phase-pms-ems-oms-r026-lineage-preservation.json" = "PMS_EMS_OMS_R026_FAIL_QUBES_LINEAGE_WEAKENED"
    "phase-pms-ems-oms-r026-instrument-universe-handling.json" = "PMS_EMS_OMS_R026_FAIL_AUDUSD_MISCLASSIFIED"
    "phase-pms-ems-oms-r026-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R026_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r026-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R026_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r026-no-external-audit.json" = "PMS_EMS_OMS_R026_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r026-forbidden-actions-audit.json" = "PMS_EMS_OMS_R026_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r026-next-phase-recommendation.json" = "PMS_EMS_OMS_R026_FAIL_SECOND_CYCLE_MISSING"
    "phase-pms-ems-oms-r026-build-test-validator-evidence.json" = "PMS_EMS_OMS_R026_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $entry.Key))) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$run = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r026-second-cycle-run.json") "PMS_EMS_OMS_R026_FAIL_SECOND_CYCLE_MISSING"
$baseline = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r026-paper-baseline-input.json") "PMS_EMS_OMS_R026_FAIL_PAPER_BASELINE_MISSING"
$qubes = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r026-second-cycle-qubes-lineage.json") "PMS_EMS_OMS_R026_FAIL_QUBES_LINEAGE_WEAKENED"
$target = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r026-second-cycle-target-portfolio.json") "PMS_EMS_OMS_R026_FAIL_SECOND_CYCLE_MISSING"
$current = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r026-second-cycle-current-paper-baseline.json") "PMS_EMS_OMS_R026_FAIL_PAPER_BASELINE_MISSING"
$diff = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r026-second-cycle-target-vs-current-diff.json") "PMS_EMS_OMS_R026_FAIL_TARGET_CURRENT_DIFF_MISSING"
$pnl = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r026-second-cycle-theoretical-pnl.json") "PMS_EMS_OMS_R026_FAIL_SECOND_CYCLE_MISSING"
$reconciliation = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r026-second-cycle-reconciliation.json") "PMS_EMS_OMS_R026_FAIL_SECOND_CYCLE_MISSING"
$theoreticalVsReal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r026-second-cycle-theoretical-vs-real.json") "PMS_EMS_OMS_R026_FAIL_SECOND_CYCLE_MISSING"
$intents = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r026-second-cycle-rebalance-intents.json") "PMS_EMS_OMS_R026_FAIL_SECOND_CYCLE_MISSING"
$intentAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r026-non-executable-intent-audit.json") "PMS_EMS_OMS_R026_FAIL_REBALANCE_INTENT_EXECUTABLE"
$paperLedgerAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r026-no-paper-ledger-commit-audit.json") "PMS_EMS_OMS_R026_FAIL_PAPER_LEDGER_MUTATED"
$livePositionAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r026-no-live-position-mutation-audit.json") "PMS_EMS_OMS_R026_FAIL_LIVE_POSITION_MUTATION"
$brokerPositionAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r026-no-broker-position-mutation-audit.json") "PMS_EMS_OMS_R026_FAIL_BROKER_POSITION_MUTATION"
$productionLedgerAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r026-no-production-ledger-mutation-audit.json") "PMS_EMS_OMS_R026_FAIL_PRODUCTION_LEDGER_MUTATION"
$tradingAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r026-no-trading-state-mutation-audit.json") "PMS_EMS_OMS_R026_FAIL_TRADING_STATE_MUTATION"
$fillAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r026-no-fill-no-execution-report-audit.json") "PMS_EMS_OMS_R026_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$orderAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r026-no-order-created-audit.json") "PMS_EMS_OMS_R026_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$routeAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r026-no-route-no-submission-audit.json") "PMS_EMS_OMS_R026_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$idempotency = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r026-idempotency-evidence.json") "PMS_EMS_OMS_R026_FAIL_SECOND_CYCLE_MISSING"
$lineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r026-lineage-preservation.json") "PMS_EMS_OMS_R026_FAIL_QUBES_LINEAGE_WEAKENED"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r026-instrument-universe-handling.json") "PMS_EMS_OMS_R026_FAIL_AUDUSD_MISCLASSIFIED"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r026-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R026_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r026-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R026_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r026-no-external-audit.json") "PMS_EMS_OMS_R026_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r026-forbidden-actions-audit.json") "PMS_EMS_OMS_R026_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r026-build-test-validator-evidence.json") "PMS_EMS_OMS_R026_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$run.secondCycleRunCreated) "PMS_EMS_OMS_R026_FAIL_SECOND_CYCLE_MISSING" "Second cycle run missing."
Require-True ([string]$run.qubesRunId -eq "qubes-r026-second-cycle") "PMS_EMS_OMS_R026_FAIL_QUBES_LINEAGE_WEAKENED" "Wrong second QubesRunId."
Require-True ([int]$run.cycleCadenceMinutes -eq 15) "PMS_EMS_OMS_R026_FAIL_QUBES_LINEAGE_WEAKENED" "Second cycle cadence is not 15 minutes."
Require-True ([bool]$run.usedR025PaperLedgerBaseline) "PMS_EMS_OMS_R026_FAIL_PAPER_BASELINE_MISSING" "R025 paper ledger baseline was not used."
Require-True ([bool]$run.rebalanceIntentsRemainNonExecutable) "PMS_EMS_OMS_R026_FAIL_REBALANCE_INTENT_EXECUTABLE" "Rebalance intents are executable."
Require-False ([bool]$run.paperLedgerStateCommittedOrMutated) "PMS_EMS_OMS_R026_FAIL_PAPER_LEDGER_MUTATED" "Paper ledger state was committed or mutated."
Require-False ([bool]$run.r025PaperBaselineMutated) "PMS_EMS_OMS_R026_FAIL_R025_BASELINE_MUTATED" "R025 paper baseline mutated."

Require-True ([bool]$baseline.paperBaselineInputCreated) "PMS_EMS_OMS_R026_FAIL_PAPER_BASELINE_MISSING" "Paper baseline input missing."
Require-True ([string]$baseline.nextCycleBaselineType -eq "PaperLedgerFixture") "PMS_EMS_OMS_R026_FAIL_PAPER_BASELINE_MISSING" "Baseline type wrong."
Require-False ([bool]$baseline.baselineIsProduction) "PMS_EMS_OMS_R026_FAIL_PRODUCTION_LEDGER_MUTATION" "Baseline is production."
Require-False ([bool]$baseline.baselineIsBroker) "PMS_EMS_OMS_R026_FAIL_BROKER_POSITION_MUTATION" "Baseline is broker."
Require-False ([bool]$baseline.baselineIsLiveTrading) "PMS_EMS_OMS_R026_FAIL_TRADING_STATE_MUTATION" "Baseline is live trading."
Require-False ([bool]$baseline.r025PaperBaselineMutated) "PMS_EMS_OMS_R026_FAIL_R025_BASELINE_MUTATED" "Baseline input mutated R025 state."
Require-False ([bool]$baseline.currentPaperBaselineIsFlatZero) "PMS_EMS_OMS_R026_FAIL_PAPER_BASELINE_MISSING" "Current baseline is flat zero."
Require-True (@($baseline.lines | Where-Object { $_.currencyOrSymbol -eq "AUDUSD" -and [decimal]$_.currentPaperQuantity -eq 131000 }).Count -eq 1) "PMS_EMS_OMS_R026_FAIL_PAPER_BASELINE_MISSING" "AUDUSD baseline missing."
Require-True (@($baseline.lines | Where-Object { $_.currencyOrSymbol -eq "EURUSD" -and [decimal]$_.currentPaperQuantity -eq 124000 }).Count -eq 1) "PMS_EMS_OMS_R026_FAIL_PAPER_BASELINE_MISSING" "EURUSD baseline missing."
Require-True (@($baseline.lines | Where-Object { $_.currencyOrSymbol -eq "GBPUSD" -and [decimal]$_.currentPaperQuantity -eq -368000 }).Count -eq 1) "PMS_EMS_OMS_R026_FAIL_PAPER_BASELINE_MISSING" "GBPUSD baseline missing."

Require-True ([bool]$qubes.secondCycleQubesLineageCreated) "PMS_EMS_OMS_R026_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage missing."
Require-True ([string]$qubes.sourceSystem -eq "ModelWeightSourceSystem.Qubes") "PMS_EMS_OMS_R026_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes source wrong."
Require-True ([int]$qubes.cadenceMinutes -eq 15) "PMS_EMS_OMS_R026_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes cadence wrong."
Require-True ([bool]$qubes.modelWeightBatchLinked) "PMS_EMS_OMS_R026_FAIL_QUBES_LINEAGE_WEAKENED" "ModelWeightBatch linkage missing."
Require-True ([bool]$qubes.modelRunLinked) "PMS_EMS_OMS_R026_FAIL_QUBES_LINEAGE_WEAKENED" "ModelRun linkage missing."
Require-True ([bool]$qubes.targetWeightsLinked) "PMS_EMS_OMS_R026_FAIL_QUBES_LINEAGE_WEAKENED" "TargetWeight linkage missing."
Require-False ([bool]$qubes.qubesLineageWeakened) "PMS_EMS_OMS_R026_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage weakened."

Require-True ([bool]$target.secondCycleTargetPortfolioCreated) "PMS_EMS_OMS_R026_FAIL_SECOND_CYCLE_MISSING" "Target portfolio missing."
Require-True ([bool]$current.currentPaperBaselineCreated) "PMS_EMS_OMS_R026_FAIL_PAPER_BASELINE_MISSING" "Current paper baseline missing."
Require-False ([bool]$current.currentPaperBaselineIsFlatZero) "PMS_EMS_OMS_R026_FAIL_PAPER_BASELINE_MISSING" "Current paper baseline is zero."
Require-True ([bool]$diff.secondCycleTargetVsCurrentDiffCreated) "PMS_EMS_OMS_R026_FAIL_TARGET_CURRENT_DIFF_MISSING" "Target-vs-current diff missing."
Require-True ([bool]$diff.deltasComputedRelativeToPaperBaseline) "PMS_EMS_OMS_R026_FAIL_TARGET_CURRENT_DIFF_MISSING" "Deltas not computed from paper baseline."
Require-False ([bool]$diff.currentBaselineWasFlatZero) "PMS_EMS_OMS_R026_FAIL_TARGET_CURRENT_DIFF_MISSING" "Diff used flat zero baseline."
Require-True (@($diff.diffLines | Where-Object { $_.symbol -eq "AUDUSD" -and [decimal]$_.currentNotional -eq 132310 -and [decimal]$_.deltaNotional -eq 7690 }).Count -eq 1) "PMS_EMS_OMS_R026_FAIL_TARGET_CURRENT_DIFF_MISSING" "AUDUSD diff wrong."
Require-True (@($diff.diffLines | Where-Object { $_.symbol -eq "EURUSD" -and [decimal]$_.currentNotional -eq 137764 -and [decimal]$_.deltaNotional -eq 22236 }).Count -eq 1) "PMS_EMS_OMS_R026_FAIL_TARGET_CURRENT_DIFF_MISSING" "EURUSD diff wrong."
Require-True (@($diff.diffLines | Where-Object { $_.symbol -eq "GBPUSD" -and [decimal]$_.currentNotional -eq -473616 -and [decimal]$_.deltaNotional -eq 233616 }).Count -eq 1) "PMS_EMS_OMS_R026_FAIL_TARGET_CURRENT_DIFF_MISSING" "GBPUSD diff wrong."

Require-True ([bool]$pnl.secondCycleTheoreticalPnlCreated) "PMS_EMS_OMS_R026_FAIL_SECOND_CYCLE_MISSING" "Theoretical PnL missing."
Require-True ([bool]$pnl.usedNoExternalMarkFixture) "PMS_EMS_OMS_R026_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "PnL did not use no-external fixture."
Require-False ([bool]$pnl.usedLiveBrokerMarketData) "PMS_EMS_OMS_R026_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "PnL used live broker market data."
Require-True ([bool]$reconciliation.secondCycleReconciliationCreated) "PMS_EMS_OMS_R026_FAIL_SECOND_CYCLE_MISSING" "Reconciliation missing."
Require-True ([bool]$theoreticalVsReal.secondCycleTheoreticalVsRealCreated) "PMS_EMS_OMS_R026_FAIL_SECOND_CYCLE_MISSING" "Theoretical-vs-real report missing."
Require-False ([bool]$theoreticalVsReal.liveReconciliationClaim) "PMS_EMS_OMS_R026_FAIL_TRADING_STATE_MUTATION" "Live reconciliation claim made."

Require-True ([bool]$intents.secondCycleRebalanceIntentsCreated) "PMS_EMS_OMS_R026_FAIL_SECOND_CYCLE_MISSING" "Rebalance intents missing."
Require-True ([bool]$intents.rebalanceIntentsRemainNonExecutable) "PMS_EMS_OMS_R026_FAIL_REBALANCE_INTENT_EXECUTABLE" "Intents executable."
Require-False ([bool]$intents.createdPaperOrderCandidates) "PMS_EMS_OMS_R026_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Paper order candidates created unexpectedly."
foreach ($intent in @($intents.intents)) {
    Require-False ([bool]$intent.isExecutable) "PMS_EMS_OMS_R026_FAIL_REBALANCE_INTENT_EXECUTABLE" "Executable intent detected."
}
Require-True ([bool]$intentAudit.nonExecutableIntentAuditCreated) "PMS_EMS_OMS_R026_FAIL_REBALANCE_INTENT_EXECUTABLE" "Non-executable audit missing."
Require-False ([bool]$intentAudit.executableRebalanceIntentDetected) "PMS_EMS_OMS_R026_FAIL_REBALANCE_INTENT_EXECUTABLE" "Executable intent audit detected."

Require-True ([bool]$paperLedgerAudit.noPaperLedgerCommitAuditCreated) "PMS_EMS_OMS_R026_FAIL_PAPER_LEDGER_MUTATED" "No-paper-ledger-commit audit missing."
Require-False ([bool]$paperLedgerAudit.paperLedgerStateCommitted) "PMS_EMS_OMS_R026_FAIL_PAPER_LEDGER_MUTATED" "Paper ledger committed."
Require-False ([bool]$paperLedgerAudit.paperLedgerStateMutated) "PMS_EMS_OMS_R026_FAIL_PAPER_LEDGER_MUTATED" "Paper ledger mutated."
Require-False ([bool]$paperLedgerAudit.r025PaperBaselineMutated) "PMS_EMS_OMS_R026_FAIL_R025_BASELINE_MUTATED" "R025 baseline mutated."

Require-True ([bool]$livePositionAudit.noLivePositionMutationAuditCreated) "PMS_EMS_OMS_R026_FAIL_LIVE_POSITION_MUTATION" "Live-position audit missing."
Require-False ([bool]$livePositionAudit.livePositionStateMutated) "PMS_EMS_OMS_R026_FAIL_LIVE_POSITION_MUTATION" "Live position mutated."
Require-True ([bool]$brokerPositionAudit.noBrokerPositionMutationAuditCreated) "PMS_EMS_OMS_R026_FAIL_BROKER_POSITION_MUTATION" "Broker-position audit missing."
Require-False ([bool]$brokerPositionAudit.brokerPositionStateMutated) "PMS_EMS_OMS_R026_FAIL_BROKER_POSITION_MUTATION" "Broker position mutated."
Require-True ([bool]$productionLedgerAudit.noProductionLedgerMutationAuditCreated) "PMS_EMS_OMS_R026_FAIL_PRODUCTION_LEDGER_MUTATION" "Production-ledger audit missing."
Require-False ([bool]$productionLedgerAudit.productionLedgerStateMutated) "PMS_EMS_OMS_R026_FAIL_PRODUCTION_LEDGER_MUTATION" "Production ledger mutated."
Require-True ([bool]$tradingAudit.noTradingStateMutationAuditCreated) "PMS_EMS_OMS_R026_FAIL_TRADING_STATE_MUTATION" "Trading-state audit missing."
Require-False ([bool]$tradingAudit.tradingStateMutated) "PMS_EMS_OMS_R026_FAIL_TRADING_STATE_MUTATION" "Trading state mutated."
Require-False ([bool]$tradingAudit.liveTradingPathIntroduced) "PMS_EMS_OMS_R026_FAIL_TRADING_STATE_MUTATION" "Live trading path introduced."

Require-True ([bool]$fillAudit.noFillNoExecutionReportAuditCreated) "PMS_EMS_OMS_R026_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill/report audit missing."
foreach ($property in @("fillCreated", "realFillCreated", "executionReportCreated", "brokerExecutionReportCreated", "secondCycleCreatesFillOrExecutionReport")) {
    Require-False ([bool]$fillAudit.$property) "PMS_EMS_OMS_R026_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill/report audit detected: $property"
}
Require-True ([bool]$orderAudit.noOrderCreatedAuditCreated) "PMS_EMS_OMS_R026_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No-order audit missing."
foreach ($property in @("executableOrderCreated", "omsOrderCreated", "parentOrderCreated", "childOrderCreated", "brokerOrderCreated", "orderStateCreated", "secondCycleCreatesOrders")) {
    Require-False ([bool]$orderAudit.$property) "PMS_EMS_OMS_R026_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order audit detected: $property"
}
Require-True ([bool]$routeAudit.noRouteNoSubmissionAuditCreated) "PMS_EMS_OMS_R026_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Route/submission audit missing."
foreach ($property in @("brokerRouteCreated", "brokerRouteAssigned", "submissionInstructionCreated", "orderSubmissionPathIntroduced", "ordersSubmitted")) {
    Require-False ([bool]$routeAudit.$property) "PMS_EMS_OMS_R026_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Route/submission audit detected: $property"
}

Require-True ([bool]$idempotency.idempotencyEvidenceCreated) "PMS_EMS_OMS_R026_FAIL_SECOND_CYCLE_MISSING" "Idempotency missing."
Require-False ([bool]$idempotency.duplicatesCreateAdditionalCycleResults) "PMS_EMS_OMS_R026_FAIL_SECOND_CYCLE_MISSING" "Duplicate cycle result created."
Require-False ([bool]$idempotency.duplicatesMutatePaperLedger) "PMS_EMS_OMS_R026_FAIL_PAPER_LEDGER_MUTATED" "Duplicate mutated paper ledger."

Require-True ([bool]$lineage.lineagePreservationCreated) "PMS_EMS_OMS_R026_FAIL_QUBES_LINEAGE_WEAKENED" "Lineage preservation missing."
foreach ($property in @("qubesLineagePreserved", "cycleLineagePreserved", "operatorDecisionLineagePreserved", "ledgerStateArchiveLineagePreserved", "ledgerCommitLineagePreserved", "ledgerPreviewLineagePreserved", "positionPreviewLineagePreserved", "simulationResultLineagePreserved", "simulationPlanLineagePreserved", "executionPlanLineagePreserved", "paperCandidateLineagePreserved", "riskLineagePreserved", "rebalanceIntentLineagePreserved", "lotSizingLineagePreserved")) {
    Require-True ([bool]$lineage.$property) "PMS_EMS_OMS_R026_FAIL_QUBES_LINEAGE_WEAKENED" "Lineage flag missing: $property"
}
Require-False ([bool]$lineage.lineageWeakened) "PMS_EMS_OMS_R026_FAIL_QUBES_LINEAGE_WEAKENED" "Lineage weakened."

Require-True ([bool]$universe.instrumentUniverseHandlingCreated) "PMS_EMS_OMS_R026_FAIL_AUDUSD_MISCLASSIFIED" "Instrument universe handling missing."
Require-False ([bool]$universe.audusdClassifiedAsFailed) "PMS_EMS_OMS_R026_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD classified as failed."
Require-False ([bool]$universe.audusdLiveValidationGapBlocksSecondCycle) "PMS_EMS_OMS_R026_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD gap blocks second cycle."
Require-True ([bool]$usdjpy.usdJpyCaveatPreservationCreated) "PMS_EMS_OMS_R026_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
Require-True ([string]$usdjpy.usdJpySecurityId -eq "4004") "PMS_EMS_OMS_R026_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID missing."
Require-True ([string]$usdjpy.usdJpySecurityIdSource -eq "8") "PMS_EMS_OMS_R026_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource missing."
Require-False ([bool]$usdjpy.usdJpyClassifiedAsFailed) "PMS_EMS_OMS_R026_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY classified as failed."
Require-False ([bool]$usdjpy.usdJpyCaveatWeakened) "PMS_EMS_OMS_R026_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat weakened."
Require-True ([bool]$lmax.lmaxReadonlyBaselineReferenceCreated) "PMS_EMS_OMS_R026_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX baseline missing."
Require-False ([bool]$lmax.lmaxUsedInThisPhase) "PMS_EMS_OMS_R026_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX used."
Require-False ([bool]$lmax.lmaxCalledInThisPhase) "PMS_EMS_OMS_R026_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX called."
Require-False ([bool]$lmax.lmaxLiveValidationGapsBlockSecondCycle) "PMS_EMS_OMS_R026_FAIL_AUDUSD_MISCLASSIFIED" "LMAX gaps block second cycle."

foreach ($property in @(
    "externalBrokerActivationDetected",
    "socketTlsFixMarketDataRuntimeActionDetected",
    "marketDataRequestAttempted",
    "liveMarketDataResponseRead",
    "apiStarted",
    "workerStarted",
    "schedulerPollingServiceTimerBackgroundJobStartedOrIntroduced",
    "liveGatewayEnabled",
    "calledBrokerGateway",
    "usedLiveMarketData",
    "startedBackgroundExecution",
    "replayOrShadowReplayIntroduced",
    "secretsOrCredentialsSerialized",
    "rawFixSerialized",
    "rawEndpointTlsValuesSerialized",
    "sessionIdsSerialized",
    "compIdsSerialized",
    "rawMdReqIdSerialized",
    "rawBrokerMarketDataPayloadsOrPricesSerialized",
    "rawMarketDataFixturePayloadsSerializedBeyondApprovedSafeSummaries")) {
    Require-False ([bool]$noExternal.$property) "PMS_EMS_OMS_R026_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected: $property"
}

foreach ($property in @(
    "brokerActivation",
    "socketTlsFix",
    "liveMarketDataRequestOrResponse",
    "apiWorkerSchedulerService",
    "timersPollingBackgroundJobs",
    "ordersRoutesSubmissions",
    "fillsExecutionReports",
    "liveTradingPath",
    "livePositionMutation",
    "brokerPositionMutation",
    "productionLedgerMutation",
    "tradingStateMutation",
    "paperLedgerMutation",
    "r025BaselineMutation",
    "replayShadowReplay",
    "secretOrRawPayloadSerialization")) {
    Require-False ([bool]$forbidden.$property) "PMS_EMS_OMS_R026_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $property"
}

Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R026_FAIL_BUILD_OR_TESTS" "Build/test evidence missing."
Require-True ([string]$evidence.dotnetBuildNoRestore -eq "PASS") "PMS_EMS_OMS_R026_FAIL_BUILD_OR_TESTS" "Build did not pass."
Require-True ([string]$evidence.focusedTests -like "PASS*") "PMS_EMS_OMS_R026_FAIL_BUILD_OR_TESTS" "Focused tests did not pass."
Require-True ([string]$evidence.unitTests -like "PASS*") "PMS_EMS_OMS_R026_FAIL_BUILD_OR_TESTS" "Unit tests did not pass."

Write-Host "PMS_EMS_OMS_R026_PASS_SECOND_15MIN_CYCLE_WITH_PAPER_BASELINE_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R026_PASS_PAPER_CYCLE_CONTINUITY_READY_NO_EXTERNAL"
