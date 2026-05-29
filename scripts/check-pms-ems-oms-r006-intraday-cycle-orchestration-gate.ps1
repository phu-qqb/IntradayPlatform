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
    "phase-pms-ems-oms-r006-summary.md" = "PMS_EMS_OMS_R006_FAIL_CYCLE_RUN_MISSING"
    "phase-pms-ems-oms-r006-cycle-run.json" = "PMS_EMS_OMS_R006_FAIL_CYCLE_RUN_MISSING"
    "phase-pms-ems-oms-r006-cycle-status.json" = "PMS_EMS_OMS_R006_FAIL_CYCLE_RUN_MISSING"
    "phase-pms-ems-oms-r006-qubes-lineage.json" = "PMS_EMS_OMS_R006_FAIL_QUBES_LINEAGE_MISSING"
    "phase-pms-ems-oms-r006-target-portfolio-output.json" = "PMS_EMS_OMS_R006_FAIL_TARGET_PORTFOLIO_MISSING"
    "phase-pms-ems-oms-r006-theoretical-pnl-output.json" = "PMS_EMS_OMS_R006_FAIL_THEORETICAL_PNL_MISSING"
    "phase-pms-ems-oms-r006-reconciliation-output.json" = "PMS_EMS_OMS_R006_FAIL_RECONCILIATION_MISSING"
    "phase-pms-ems-oms-r006-theoretical-vs-real-output.json" = "PMS_EMS_OMS_R006_FAIL_THEORETICAL_REAL_REPORT_MISSING"
    "phase-pms-ems-oms-r006-rebalance-intents-output.json" = "PMS_EMS_OMS_R006_FAIL_REBALANCE_INTENT_EXECUTABLE"
    "phase-pms-ems-oms-r006-non-executable-intent-audit.json" = "PMS_EMS_OMS_R006_FAIL_REBALANCE_INTENT_EXECUTABLE"
    "phase-pms-ems-oms-r006-missing-stale-mark-preservation.json" = "PMS_EMS_OMS_R006_FAIL_THEORETICAL_PNL_MISSING"
    "phase-pms-ems-oms-r006-instrument-universe-handling.json" = "PMS_EMS_OMS_R006_FAIL_LMAX_GAP_BLOCKS_CYCLE"
    "phase-pms-ems-oms-r006-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R006_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r006-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R006_FAIL_LMAX_GAP_BLOCKS_CYCLE"
    "phase-pms-ems-oms-r006-no-external-audit.json" = "PMS_EMS_OMS_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r006-forbidden-actions-audit.json" = "PMS_EMS_OMS_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r006-next-phase-recommendation.json" = "PMS_EMS_OMS_R006_FAIL_CYCLE_RUN_MISSING"
    "phase-pms-ems-oms-r006-build-test-validator-evidence.json" = "PMS_EMS_OMS_R006_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    $path = Join-Path $artifactRoot $entry.Key
    if (-not (Test-Path -LiteralPath $path)) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$cycle = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r006-cycle-run.json") "PMS_EMS_OMS_R006_FAIL_CYCLE_RUN_MISSING"
$status = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r006-cycle-status.json") "PMS_EMS_OMS_R006_FAIL_CYCLE_RUN_MISSING"
$lineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r006-qubes-lineage.json") "PMS_EMS_OMS_R006_FAIL_QUBES_LINEAGE_MISSING"
$target = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r006-target-portfolio-output.json") "PMS_EMS_OMS_R006_FAIL_TARGET_PORTFOLIO_MISSING"
$pnl = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r006-theoretical-pnl-output.json") "PMS_EMS_OMS_R006_FAIL_THEORETICAL_PNL_MISSING"
$recon = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r006-reconciliation-output.json") "PMS_EMS_OMS_R006_FAIL_RECONCILIATION_MISSING"
$report = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r006-theoretical-vs-real-output.json") "PMS_EMS_OMS_R006_FAIL_THEORETICAL_REAL_REPORT_MISSING"
$intents = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r006-rebalance-intents-output.json") "PMS_EMS_OMS_R006_FAIL_REBALANCE_INTENT_EXECUTABLE"
$intentAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r006-non-executable-intent-audit.json") "PMS_EMS_OMS_R006_FAIL_REBALANCE_INTENT_EXECUTABLE"
$marks = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r006-missing-stale-mark-preservation.json") "PMS_EMS_OMS_R006_FAIL_THEORETICAL_PNL_MISSING"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r006-instrument-universe-handling.json") "PMS_EMS_OMS_R006_FAIL_LMAX_GAP_BLOCKS_CYCLE"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r006-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R006_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r006-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R006_FAIL_LMAX_GAP_BLOCKS_CYCLE"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r006-no-external-audit.json") "PMS_EMS_OMS_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r006-forbidden-actions-audit.json") "PMS_EMS_OMS_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$nextPhase = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r006-next-phase-recommendation.json") "PMS_EMS_OMS_R006_FAIL_CYCLE_RUN_MISSING"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r006-build-test-validator-evidence.json") "PMS_EMS_OMS_R006_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$cycle.cycleRunCreated) "PMS_EMS_OMS_R006_FAIL_CYCLE_RUN_MISSING" "Cycle run artifact is missing."
Require-True ([string]$cycle.cycleRunId -ne "") "PMS_EMS_OMS_R006_FAIL_CYCLE_RUN_MISSING" "CycleRunId is missing."
Require-True ([string]$cycle.qubesRunId -ne "") "PMS_EMS_OMS_R006_FAIL_QUBES_LINEAGE_MISSING" "QubesRunId is missing."
Require-True ([int]$cycle.cycleCadenceMinutes -eq 15) "PMS_EMS_OMS_R006_FAIL_CYCLE_RUN_MISSING" "Cycle cadence is not 15 minutes."
Require-True ([string]$cycle.cycleStatus -in @("CompletedNoExternal", "CompletedWithMissingMarks")) "PMS_EMS_OMS_R006_FAIL_CYCLE_RUN_MISSING" "Cycle status is not an allowed completed no-external status."
Require-True ([bool]$cycle.isOneDeterministicFixtureRun) "PMS_EMS_OMS_R006_FAIL_CYCLE_RUN_MISSING" "Cycle is not marked as one deterministic fixture run."
Require-False ([bool]$cycle.isScheduler) "PMS_EMS_OMS_R006_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Cycle is a scheduler."
Require-False ([bool]$cycle.isService) "PMS_EMS_OMS_R006_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Cycle is a service."
Require-False ([bool]$cycle.isPolling) "PMS_EMS_OMS_R006_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Cycle is polling."
Require-False ([bool]$cycle.startsApiOrWorker) "PMS_EMS_OMS_R006_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Cycle starts API/Worker."
Require-False ([bool]$cycle.usesLiveMarketData) "PMS_EMS_OMS_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Cycle uses live market data."
Require-False ([bool]$cycle.callsBrokerGateway) "PMS_EMS_OMS_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Cycle calls broker gateway."
Require-False ([bool]$cycle.submitsOrders) "PMS_EMS_OMS_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Cycle submits orders."
Require-False ([bool]$cycle.createsExecutableOrder) "PMS_EMS_OMS_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Cycle creates executable order."
Require-False ([bool]$cycle.mutatesLiveTradingState) "PMS_EMS_OMS_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Cycle mutates live trading state."

Require-True ([bool]$status.cycleStatusCreated) "PMS_EMS_OMS_R006_FAIL_CYCLE_RUN_MISSING" "Cycle status artifact missing."
Require-True ([bool]$status.allowedStatus) "PMS_EMS_OMS_R006_FAIL_CYCLE_RUN_MISSING" "Cycle status is not allowed."
Require-True ([string]$status.targetWeightsStatus -eq "Accepted") "PMS_EMS_OMS_R006_FAIL_QUBES_LINEAGE_MISSING" "Target weights status is not accepted."
Require-True ([string]$status.persistenceStatus -in @("Persisted", "AlreadyPersisted")) "PMS_EMS_OMS_R006_FAIL_QUBES_LINEAGE_MISSING" "Persistence status is missing."
Require-True ([string]$status.theoreticalPortfolioStatus -eq "Produced") "PMS_EMS_OMS_R006_FAIL_TARGET_PORTFOLIO_MISSING" "Target portfolio status missing."
Require-True ([string]$status.pnlStatus -eq "MissingMark") "PMS_EMS_OMS_R006_FAIL_THEORETICAL_PNL_MISSING" "PnL status does not preserve MissingMark."
Require-True ([string]$status.rebalanceIntentStatus -eq "NonExecutable") "PMS_EMS_OMS_R006_FAIL_REBALANCE_INTENT_EXECUTABLE" "Rebalance intent status is not non-executable."
Require-True ([string]$status.safetyStatus -eq "NoExternalFixtureOnly") "PMS_EMS_OMS_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Safety status is not no-external."

Require-True ([bool]$lineage.qubesLineageCreated) "PMS_EMS_OMS_R006_FAIL_QUBES_LINEAGE_MISSING" "Qubes lineage artifact missing."
Require-True ([string]$lineage.sourceSystem -eq "ModelWeightSourceSystem.Qubes") "PMS_EMS_OMS_R006_FAIL_QUBES_LINEAGE_MISSING" "Qubes source missing."
Require-True ([int]$lineage.cadenceMinutes -eq 15) "PMS_EMS_OMS_R006_FAIL_QUBES_LINEAGE_MISSING" "Qubes cadence missing."
Require-True ([int]$lineage.rawInputRowCount -gt 0) "PMS_EMS_OMS_R006_FAIL_QUBES_LINEAGE_MISSING" "Raw row count missing."
Require-True ([int]$lineage.normalizedOutputRowCount -gt 0) "PMS_EMS_OMS_R006_FAIL_QUBES_LINEAGE_MISSING" "Normalized row count missing."
Require-True ([bool]$lineage.rawRowsPersisted) "PMS_EMS_OMS_R006_FAIL_QUBES_LINEAGE_MISSING" "Raw rows not persisted."
Require-True ([bool]$lineage.normalizedRowsPersisted) "PMS_EMS_OMS_R006_FAIL_QUBES_LINEAGE_MISSING" "Normalized rows not persisted."
Require-True ([bool]$lineage.modelWeightBatchLinkagePresent) "PMS_EMS_OMS_R006_FAIL_QUBES_LINEAGE_MISSING" "ModelWeightBatch linkage missing."
Require-True ([bool]$lineage.modelRunLinkagePresent) "PMS_EMS_OMS_R006_FAIL_QUBES_LINEAGE_MISSING" "ModelRun linkage missing."
Require-True ([bool]$lineage.targetWeightLinkagePresent) "PMS_EMS_OMS_R006_FAIL_QUBES_LINEAGE_MISSING" "TargetWeight linkage missing."
Require-True ([bool]$lineage.idempotencyByQubesRunIdPreserved) "PMS_EMS_OMS_R006_FAIL_QUBES_LINEAGE_MISSING" "Idempotency missing."

Require-True ([bool]$target.targetPortfolioOutputCreated) "PMS_EMS_OMS_R006_FAIL_TARGET_PORTFOLIO_MISSING" "Target portfolio output missing."
Require-True ([string]$target.stateSource -eq "Theoretical") "PMS_EMS_OMS_R006_FAIL_TARGET_PORTFOLIO_MISSING" "Target portfolio is not theoretical."
Require-True ([int]$target.positionCount -eq 13) "PMS_EMS_OMS_R006_FAIL_TARGET_PORTFOLIO_MISSING" "Target portfolio position count unexpected."
Require-True ([bool]$target.usesR002NormalizedUsdQuoteWeights) "PMS_EMS_OMS_R006_FAIL_TARGET_PORTFOLIO_MISSING" "R002 normalized weights not used."

Require-True ([bool]$pnl.theoreticalPnlOutputCreated) "PMS_EMS_OMS_R006_FAIL_THEORETICAL_PNL_MISSING" "Theoretical PnL output missing."
Require-True ([string]$pnl.pnlSource -eq "Theoretical") "PMS_EMS_OMS_R006_FAIL_THEORETICAL_PNL_MISSING" "PnL source not theoretical."
Require-True ([bool]$pnl.fixtureBasedOnly) "PMS_EMS_OMS_R006_FAIL_THEORETICAL_PNL_MISSING" "PnL is not fixture-based only."
Require-False ([bool]$pnl.usesLiveMarketData) "PMS_EMS_OMS_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "PnL uses live market data."
Require-False ([bool]$pnl.brokerReportedPnlClaimed) "PMS_EMS_OMS_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker PnL claimed."
Require-True ([decimal]$pnl.portfolioPnL.unrealizedPnL -eq 6804.66) "PMS_EMS_OMS_R006_FAIL_THEORETICAL_PNL_MISSING" "Theoretical PnL unexpected."
Require-True ([string]$pnl.portfolioPnL.status -eq "MissingMark") "PMS_EMS_OMS_R006_FAIL_THEORETICAL_PNL_MISSING" "MissingMark status not preserved."
Require-False ([bool]$pnl.rawMarketDataFixturePayloadSerialized) "PMS_EMS_OMS_R006_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw market-data fixture payload serialized."

Require-True ([bool]$recon.reconciliationOutputCreated) "PMS_EMS_OMS_R006_FAIL_RECONCILIATION_MISSING" "Reconciliation output missing."
Require-True ([bool]$recon.usesExistingPortfolioReconciler) "PMS_EMS_OMS_R006_FAIL_RECONCILIATION_MISSING" "Existing reconciler not used."
Require-True ([string]$recon.actualPortfolioStateSource -eq "Simulated") "PMS_EMS_OMS_R006_FAIL_RECONCILIATION_MISSING" "Actual portfolio is not simulated."
Require-False ([bool]$recon.brokerReportedActualState) "PMS_EMS_OMS_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Actual portfolio is broker-reported."
Require-True ([int]$recon.lineCount -eq 13) "PMS_EMS_OMS_R006_FAIL_RECONCILIATION_MISSING" "Reconciliation line count unexpected."
Require-True ([int]$recon.statusCounts.Drift -ge 2) "PMS_EMS_OMS_R006_FAIL_RECONCILIATION_MISSING" "Drift rows missing."
Require-True ([int]$recon.statusCounts.MissingActual -ge 1) "PMS_EMS_OMS_R006_FAIL_RECONCILIATION_MISSING" "Missing actual row missing."
Require-False ([bool]$recon.lmaxLiveGapsBlockReconciliation) "PMS_EMS_OMS_R006_FAIL_LMAX_GAP_BLOCKS_CYCLE" "LMAX gaps block reconciliation."

Require-True ([bool]$report.theoreticalVsRealOutputCreated) "PMS_EMS_OMS_R006_FAIL_THEORETICAL_REAL_REPORT_MISSING" "Theoretical-vs-real output missing."
Require-True ([bool]$report.usesExistingTheoreticalVsRealComparator) "PMS_EMS_OMS_R006_FAIL_THEORETICAL_REAL_REPORT_MISSING" "Existing comparator not used."
Require-True ([decimal]$report.theoreticalPnl -eq 6804.66) "PMS_EMS_OMS_R006_FAIL_THEORETICAL_REAL_REPORT_MISSING" "Theoretical PnL unexpected."
Require-True ([decimal]$report.actualFixturePnl -eq 6704.66) "PMS_EMS_OMS_R006_FAIL_THEORETICAL_REAL_REPORT_MISSING" "Actual fixture PnL unexpected."
Require-True ([decimal]$report.pnlDifference -eq -100.0) "PMS_EMS_OMS_R006_FAIL_THEORETICAL_REAL_REPORT_MISSING" "PnL difference unexpected."
Require-True ([string]$report.status -eq "Drift") "PMS_EMS_OMS_R006_FAIL_THEORETICAL_REAL_REPORT_MISSING" "Comparator status missing."
Require-False ([bool]$report.livePnlClaimed) "PMS_EMS_OMS_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Live PnL claimed."
Require-False ([bool]$report.brokerReportedPnlClaimed) "PMS_EMS_OMS_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker PnL claimed."

Require-True ([bool]$intents.rebalanceIntentsOutputCreated) "PMS_EMS_OMS_R006_FAIL_REBALANCE_INTENT_EXECUTABLE" "Rebalance intents output missing."
Require-True ([bool]$intents.rebalanceIntentsRemainNonExecutable) "PMS_EMS_OMS_R006_FAIL_REBALANCE_INTENT_EXECUTABLE" "Rebalance intents became executable."
Require-False ([bool]$intents.isExecutable) "PMS_EMS_OMS_R006_FAIL_REBALANCE_INTENT_EXECUTABLE" "Intent is executable."
Require-False ([bool]$intents.ordersCreated) "PMS_EMS_OMS_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders created."
Require-False ([bool]$intents.ordersSubmitted) "PMS_EMS_OMS_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders submitted."
Require-False ([bool]$intents.brokerGatewayCalled) "PMS_EMS_OMS_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker gateway called."

Require-True ([bool]$intentAudit.nonExecutableIntentAuditCreated) "PMS_EMS_OMS_R006_FAIL_REBALANCE_INTENT_EXECUTABLE" "Non-executable audit missing."
Require-True ([bool]$intentAudit.allRebalanceIntentsNonExecutable) "PMS_EMS_OMS_R006_FAIL_REBALANCE_INTENT_EXECUTABLE" "Executable rebalance intent present."
Require-True ([bool]$intentAudit.allIntentLinesHaveTheoreticalOnlyStatus) "PMS_EMS_OMS_R006_FAIL_REBALANCE_INTENT_EXECUTABLE" "TheoreticalOnly status missing."
Require-True ([bool]$intentAudit.allIntentLinesHaveNotExecutableStatus) "PMS_EMS_OMS_R006_FAIL_REBALANCE_INTENT_EXECUTABLE" "NotExecutable status missing."
Require-True ([bool]$intentAudit.allIntentLinesHaveBlockedNoOmsStatus) "PMS_EMS_OMS_R006_FAIL_REBALANCE_INTENT_EXECUTABLE" "BlockedNoOMS status missing."
Require-False ([bool]$intentAudit.executableOrderCreated) "PMS_EMS_OMS_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Executable order created."
Require-False ([bool]$intentAudit.orderSubmissionIntroduced) "PMS_EMS_OMS_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order submission introduced."

Require-True ([bool]$marks.missingStaleMarkPreservationCreated) "PMS_EMS_OMS_R006_FAIL_THEORETICAL_PNL_MISSING" "Missing/stale mark artifact missing."
Require-True ([bool]$marks.missingMarkStatusPreserved) "PMS_EMS_OMS_R006_FAIL_THEORETICAL_PNL_MISSING" "MissingMark not preserved."
Require-True ([bool]$marks.staleMarkStatusPreserved) "PMS_EMS_OMS_R006_FAIL_THEORETICAL_PNL_MISSING" "StaleMark not preserved."
Require-False ([bool]$marks.missingOrStaleMarksHidden) "PMS_EMS_OMS_R006_FAIL_THEORETICAL_PNL_MISSING" "Missing/stale marks hidden."
Require-True ([bool]$marks.cycleStatusReflectsMissingMarks) "PMS_EMS_OMS_R006_FAIL_THEORETICAL_PNL_MISSING" "Cycle status does not reflect missing marks."
Require-False ([bool]$marks.rawMarketDataFixturePayloadSerialized) "PMS_EMS_OMS_R006_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw market-data fixture payload serialized."
Require-False ([bool]$marks.fabricatedPnlForMissingMarks) "PMS_EMS_OMS_R006_FAIL_THEORETICAL_PNL_MISSING" "PnL fabricated for missing marks."

Require-True ([bool]$universe.instrumentUniverseHandlingCreated) "PMS_EMS_OMS_R006_FAIL_LMAX_GAP_BLOCKS_CYCLE" "Instrument universe artifact missing."
Require-False ([bool]$universe.lmaxReadOnlyScopeUsedAsCycleGate) "PMS_EMS_OMS_R006_FAIL_LMAX_GAP_BLOCKS_CYCLE" "LMAX scope gates cycle."
Require-False ([bool]$universe.lmaxLiveValidationGapsBlockCycle) "PMS_EMS_OMS_R006_FAIL_LMAX_GAP_BLOCKS_CYCLE" "LMAX gaps block cycle."
Require-False ([bool]$universe.audusdLiveValidationGapBlocksCycle) "PMS_EMS_OMS_R006_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD gap blocks cycle."
Require-False ([bool]$universe.usdjpyLiveValidationGapBlocksCycle) "PMS_EMS_OMS_R006_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY gap blocks cycle."
Require-True ([bool]$universe.instrumentsWithoutBrokerValidationHandledSafely) "PMS_EMS_OMS_R006_FAIL_LMAX_GAP_BLOCKS_CYCLE" "Unvalidated instruments not safe."
Require-True ([bool]$universe.instrumentsWithoutFixtureMarksHandledSafely) "PMS_EMS_OMS_R006_FAIL_THEORETICAL_PNL_MISSING" "Missing fixture marks not safe."

Require-True ([bool]$usdjpy.usdJpyCaveatPreservationCreated) "PMS_EMS_OMS_R006_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat artifact missing."
Require-True ([string]$usdjpy.usdJpySecurityId -eq "4004") "PMS_EMS_OMS_R006_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID caveat missing."
Require-True ([string]$usdjpy.usdJpySecurityIdSource -eq "8") "PMS_EMS_OMS_R006_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource caveat missing."
Require-True ([bool]$usdjpy.usdJpyNotProven) "PMS_EMS_OMS_R006_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY not-proven missing."
Require-False ([bool]$usdjpy.usdJpyClassifiedAsFailed) "PMS_EMS_OMS_R006_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY classified as failed."
Require-True ([bool]$usdjpy.audusdTlsBoundaryInconclusive) "PMS_EMS_OMS_R006_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD TLS-boundary status missing."
Require-False ([bool]$usdjpy.audusdClassifiedAsFailed) "PMS_EMS_OMS_R006_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD classified as failed."
Require-False ([bool]$usdjpy.lmaxLiveValidationGapsBlockCycle) "PMS_EMS_OMS_R006_FAIL_LMAX_GAP_BLOCKS_CYCLE" "LMAX gaps block cycle."

Require-True ([bool]$lmax.lmaxReadonlyBaselineReferenceCreated) "PMS_EMS_OMS_R006_FAIL_LMAX_GAP_BLOCKS_CYCLE" "LMAX baseline missing."
Require-False ([bool]$lmax.lmaxUsedInThisPhase) "PMS_EMS_OMS_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX used in this phase."
Require-False ([bool]$lmax.lmaxLiveValidationGapsBlockCycle) "PMS_EMS_OMS_R006_FAIL_LMAX_GAP_BLOCKS_CYCLE" "LMAX gaps block cycle."
Require-True ([bool]$lmax.baseline.GBPUSD.readOnlyMarketDataSucceeded) "PMS_EMS_OMS_R006_FAIL_LMAX_GAP_BLOCKS_CYCLE" "GBPUSD baseline missing."
Require-True ([int]$lmax.baseline.GBPUSD.sanitizedEntryCount -eq 2) "PMS_EMS_OMS_R006_FAIL_LMAX_GAP_BLOCKS_CYCLE" "GBPUSD sanitized count missing."
Require-True ([bool]$lmax.baseline.EURGBP.readOnlyMarketDataSucceeded) "PMS_EMS_OMS_R006_FAIL_LMAX_GAP_BLOCKS_CYCLE" "EURGBP baseline missing."
Require-True ([int]$lmax.baseline.EURGBP.sanitizedEntryCount -eq 2) "PMS_EMS_OMS_R006_FAIL_LMAX_GAP_BLOCKS_CYCLE" "EURGBP sanitized count missing."
Require-False ([bool]$lmax.baseline.AUDUSD.classifiedAsFailed) "PMS_EMS_OMS_R006_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD classified as failed."
Require-False ([bool]$lmax.baseline.USDJPY.classifiedAsFailed) "PMS_EMS_OMS_R006_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY classified as failed."
Require-True ([string]$lmax.baseline.USDJPY.securityId -eq "4004") "PMS_EMS_OMS_R006_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY baseline SecurityID missing."
Require-True ([string]$lmax.baseline.USDJPY.securityIdSource -eq "8") "PMS_EMS_OMS_R006_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY baseline SecurityIDSource missing."

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
    "lmaxOrBrokerCalled",
    "lmaxLiveValidationGapsBlockCycle",
    "rebalanceIntentExecutable",
    "actualPortfolioBrokerReportedLiveState"
)) {
    Require-False ([bool]$noExternal.$property) "PMS_EMS_OMS_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected: $property"
}

foreach ($item in $forbidden.forbiddenActions) {
    if ([bool]$item.detected) {
        if ([string]$item.action -match "scheduler|service|timer|background") {
            Fail-Gate "PMS_EMS_OMS_R006_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "order|trading|rebalance|executable") {
            Fail-Gate "PMS_EMS_OMS_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden action detected: $($item.action)"
        }

        Fail-Gate "PMS_EMS_OMS_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $($item.action)"
    }
}

Require-True ([bool]$nextPhase.nextPhaseRecommendationCreated) "PMS_EMS_OMS_R006_FAIL_CYCLE_RUN_MISSING" "Next phase recommendation missing."
Require-True ([string]$nextPhase.recommendedNextPhase -eq "PMS-EMS-OMS-R007") "PMS_EMS_OMS_R006_FAIL_CYCLE_RUN_MISSING" "Next phase is not R007."
Require-True ([bool]$nextPhase.mustRemainNoExternal) "PMS_EMS_OMS_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase not no-external."
Require-True ([bool]$nextPhase.mustNotStartScheduler) "PMS_EMS_OMS_R006_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Next phase permits scheduler."
Require-True ([bool]$nextPhase.mustNotGenerateExecutableOrders) "PMS_EMS_OMS_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Next phase permits executable orders."

$apiSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Api/appsettings.json") "PMS_EMS_OMS_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$workerSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/appsettings.json") "PMS_EMS_OMS_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED"

Require-False ([bool]$apiSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API live trading enabled."
Require-False ([bool]$workerSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker live trading enabled."
Require-False ([bool]$apiSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API external connections enabled."
Require-False ([bool]$workerSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker external connections enabled."
Require-True ([bool]$apiSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API fake gateway not required."
Require-True ([bool]$workerSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker fake gateway not required."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.Enabled) "PMS_EMS_OMS_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX read-only runtime enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowExternalConnections) "PMS_EMS_OMS_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX read-only runtime allows external connections."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowOrderSubmission) "PMS_EMS_OMS_R006_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "LMAX read-only runtime allows order submission."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SchedulerEnabled) "PMS_EMS_OMS_R006_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "LMAX scheduler enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SubmitToShadowReplay) "PMS_EMS_OMS_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX submits to shadow replay."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.PersistRawFixMessages) "PMS_EMS_OMS_R006_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "LMAX persists raw FIX."
Require-False ([bool]$workerSettings.MarketDataBars.Enabled) "PMS_EMS_OMS_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker market-data bars enabled."

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-pms-ems-oms-r006-*" -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combined = [string]::Join("`n", $artifactText)
$unsafePatterns = @(
    "\u0001",
    "35=",
    "MDReqID\s*[:=]",
    "SenderCompID\s*[:=]",
    "TargetCompID\s*[:=]",
    "BeginString\s*[:=]",
    "SocketHost\s*[:=]",
    "TlsHost\s*[:=]",
    "Password\s*[:=]",
    "ApiKey\s*[:=]",
    "Secret\s*[:=]",
    "Bearer\s+[A-Za-z0-9_\.-]+",
    "rawBid",
    "rawAsk",
    "rawMid"
)

foreach ($pattern in $unsafePatterns) {
    if ($combined -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R006_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Unsafe serialized content pattern found: $pattern"
    }
}

$requiredFiles = @(
    "src/QQ.Production.Intraday.Application/QubesIntradayCycleFixture.cs",
    "tests/QQ.Production.Intraday.Tests.Unit/QubesIntradayCycleFixtureTests.cs"
)

foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $file))) {
        Fail-Gate "PMS_EMS_OMS_R006_FAIL_BUILD_OR_TESTS" "Required implementation/test file missing: $file"
    }
}

$source = Get-Content -LiteralPath (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/QubesIntradayCycleFixture.cs") -Raw
foreach ($pattern in @("TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "SendOrderAsync", "SubmitOrder", "ParentOrder", "ChildOrder", "FixSession", "Lmax")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R006_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "R006 source contains forbidden runtime pattern: $pattern"
    }
}

foreach ($pattern in @("AddHostedService", "IHostedService", "BackgroundService", "PeriodicTimer", "Task.Delay", "System.Threading.Timer")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R006_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "R006 source contains scheduler/service pattern: $pattern"
    }
}

$tests = Get-Content -LiteralPath (Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/QubesIntradayCycleFixtureTests.cs") -Raw
foreach ($requiredTestName in @(
    "Valid_qubes_fifteen_minute_fixture_runs_full_no_external_cycle",
    "Qubes_run_id_is_preserved_end_to_end",
    "Raw_and_normalized_rows_persist_through_r004b_lineage",
    "Modelweightbatch_modelrun_targetweight_linkage_is_present",
    "Theoretical_target_portfolio_is_produced",
    "Theoretical_pnl_fixture_is_produced",
    "Target_vs_actual_reconciliation_is_produced",
    "Theoretical_vs_real_report_is_produced",
    "Non_executable_rebalance_intents_are_produced",
    "Cycle_status_preserves_missing_marks",
    "Missing_and_stale_mark_status_is_preserved_not_hidden",
    "Actual_portfolio_remains_fixture_not_broker_reported_live_state",
    "No_executable_order_is_created",
    "No_order_submission_or_broker_runtime_path_is_introduced",
    "No_service_or_background_execution_is_introduced",
    "Api_and_worker_live_gateway_remain_disabled",
    "Audusd_is_not_misclassified_as_failed",
    "Usdjpy_caveat_remains_preserved",
    "Duplicate_qubes_run_id_behavior_remains_idempotent"
)) {
    if ($tests -notmatch [regex]::Escape($requiredTestName)) {
        Fail-Gate "PMS_EMS_OMS_R006_FAIL_BUILD_OR_TESTS" "Focused test missing: $requiredTestName"
    }
}

Require-True ([string]$evidence.build.status -eq "PASS") "PMS_EMS_OMS_R006_FAIL_BUILD_OR_TESTS" "Build evidence missing or not PASS."
Require-True ([string]$evidence.focusedTests.status -eq "PASS") "PMS_EMS_OMS_R006_FAIL_BUILD_OR_TESTS" "Focused test evidence missing or not PASS."
Require-True ([int]$evidence.focusedTests.failed -eq 0) "PMS_EMS_OMS_R006_FAIL_BUILD_OR_TESTS" "Focused tests have failures."
Require-True ([string]$evidence.unitTests.status -eq "PASS") "PMS_EMS_OMS_R006_FAIL_BUILD_OR_TESTS" "Unit test evidence missing or not PASS."
Require-True ([int]$evidence.unitTests.failed -eq 0) "PMS_EMS_OMS_R006_FAIL_BUILD_OR_TESTS" "Unit tests have failures."
Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R006_FAIL_BUILD_OR_TESTS" "Build/test/validator evidence marker missing."
Require-True (Test-Path -LiteralPath (Join-Path $repoRoot "scripts/check-pms-ems-oms-r006-intraday-cycle-orchestration-gate.ps1")) "PMS_EMS_OMS_R006_FAIL_BUILD_OR_TESTS" "Validator script missing."

Write-Host "PMS_EMS_OMS_R006_PASS_INTRADAY_CYCLE_ORCHESTRATION_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R006_PASS_15MIN_CYCLE_FIXTURE_READY_NO_EXTERNAL"
