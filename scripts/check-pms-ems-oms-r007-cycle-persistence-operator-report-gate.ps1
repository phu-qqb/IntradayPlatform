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
    "phase-pms-ems-oms-r007-summary.md" = "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r007-cycle-archive.json" = "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r007-cycle-report.md" = "PMS_EMS_OMS_R007_FAIL_OPERATOR_REPORT_MISSING"
    "phase-pms-ems-oms-r007-cycle-report.json" = "PMS_EMS_OMS_R007_FAIL_OPERATOR_REPORT_MISSING"
    "phase-pms-ems-oms-r007-qubes-lineage-preservation.json" = "PMS_EMS_OMS_R007_FAIL_QUBES_LINEAGE_WEAKENED"
    "phase-pms-ems-oms-r007-target-portfolio-archive.json" = "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r007-theoretical-pnl-archive.json" = "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r007-reconciliation-archive.json" = "PMS_EMS_OMS_R007_FAIL_RECONCILIATION_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r007-theoretical-vs-real-archive.json" = "PMS_EMS_OMS_R007_FAIL_THEORETICAL_REAL_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r007-rebalance-intent-archive.json" = "PMS_EMS_OMS_R007_FAIL_REBALANCE_INTENT_EXECUTABLE"
    "phase-pms-ems-oms-r007-non-executable-intent-audit.json" = "PMS_EMS_OMS_R007_FAIL_REBALANCE_INTENT_EXECUTABLE"
    "phase-pms-ems-oms-r007-missing-stale-mark-warning-report.json" = "PMS_EMS_OMS_R007_FAIL_MISSING_STALE_WARNING_MISSING"
    "phase-pms-ems-oms-r007-cycle-idempotency-evidence.json" = "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r007-instrument-universe-handling.json" = "PMS_EMS_OMS_R007_FAIL_LMAX_GAP_BLOCKS_REPORTING"
    "phase-pms-ems-oms-r007-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R007_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r007-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R007_FAIL_LMAX_GAP_BLOCKS_REPORTING"
    "phase-pms-ems-oms-r007-no-external-audit.json" = "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r007-forbidden-actions-audit.json" = "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r007-next-phase-recommendation.json" = "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r007-build-test-validator-evidence.json" = "PMS_EMS_OMS_R007_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    $path = Join-Path $artifactRoot $entry.Key
    if (-not (Test-Path -LiteralPath $path)) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$archive = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r007-cycle-archive.json") "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING"
$report = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r007-cycle-report.json") "PMS_EMS_OMS_R007_FAIL_OPERATOR_REPORT_MISSING"
$lineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r007-qubes-lineage-preservation.json") "PMS_EMS_OMS_R007_FAIL_QUBES_LINEAGE_WEAKENED"
$target = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r007-target-portfolio-archive.json") "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING"
$pnl = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r007-theoretical-pnl-archive.json") "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING"
$recon = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r007-reconciliation-archive.json") "PMS_EMS_OMS_R007_FAIL_RECONCILIATION_ARCHIVE_MISSING"
$comparison = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r007-theoretical-vs-real-archive.json") "PMS_EMS_OMS_R007_FAIL_THEORETICAL_REAL_ARCHIVE_MISSING"
$intents = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r007-rebalance-intent-archive.json") "PMS_EMS_OMS_R007_FAIL_REBALANCE_INTENT_EXECUTABLE"
$intentAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r007-non-executable-intent-audit.json") "PMS_EMS_OMS_R007_FAIL_REBALANCE_INTENT_EXECUTABLE"
$warnings = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r007-missing-stale-mark-warning-report.json") "PMS_EMS_OMS_R007_FAIL_MISSING_STALE_WARNING_MISSING"
$idempotency = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r007-cycle-idempotency-evidence.json") "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r007-instrument-universe-handling.json") "PMS_EMS_OMS_R007_FAIL_LMAX_GAP_BLOCKS_REPORTING"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r007-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R007_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r007-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R007_FAIL_LMAX_GAP_BLOCKS_REPORTING"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r007-no-external-audit.json") "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r007-forbidden-actions-audit.json") "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$nextPhase = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r007-next-phase-recommendation.json") "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r007-build-test-validator-evidence.json") "PMS_EMS_OMS_R007_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$archive.cycleArchiveCreated) "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING" "Cycle archive artifact is missing."
Require-True ([string]$archive.cycleRunId -ne "") "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING" "CycleRunId is missing."
Require-True ([string]$archive.qubesRunId -ne "") "PMS_EMS_OMS_R007_FAIL_QUBES_LINEAGE_WEAKENED" "QubesRunId is missing."
Require-True ([int]$archive.cycleCadenceMinutes -eq 15) "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING" "Cycle cadence is not 15 minutes."
Require-True ([string]$archive.cycleStatus -eq "CompletedWithMissingMarks") "PMS_EMS_OMS_R007_FAIL_MISSING_STALE_WARNING_MISSING" "CompletedWithMissingMarks was not preserved."
Require-True ([string]$archive.safetyStatus -eq "NoExternalFixtureOnly") "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Safety status is not no-external."
Require-True ([bool]$archive.persisted) "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING" "Archive is not marked persisted."
Require-True ([bool]$archive.idempotencyByCycleRunId) "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING" "CycleRunId idempotency missing."
Require-True ([bool]$archive.noExternal) "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Archive is not no-external."
Require-True ([bool]$archive.references.qubesAuditBatchPresent) "PMS_EMS_OMS_R007_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes audit batch reference missing."
Require-True ([bool]$archive.references.rawQubesRowAuditPresent) "PMS_EMS_OMS_R007_FAIL_QUBES_LINEAGE_WEAKENED" "Raw row audit reference missing."
Require-True ([bool]$archive.references.normalizedRowAuditPresent) "PMS_EMS_OMS_R007_FAIL_QUBES_LINEAGE_WEAKENED" "Normalized row audit reference missing."
Require-True ([bool]$archive.references.modelWeightBatchPresent) "PMS_EMS_OMS_R007_FAIL_QUBES_LINEAGE_WEAKENED" "ModelWeightBatch reference missing."
Require-True ([bool]$archive.references.modelRunPresent) "PMS_EMS_OMS_R007_FAIL_QUBES_LINEAGE_WEAKENED" "ModelRun reference missing."
Require-True ([bool]$archive.references.targetWeightLinkagePresent) "PMS_EMS_OMS_R007_FAIL_QUBES_LINEAGE_WEAKENED" "TargetWeight linkage missing."
Require-True ([bool]$archive.safeSummariesArchived.targetPortfolio) "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING" "Target portfolio summary missing."
Require-True ([bool]$archive.safeSummariesArchived.theoreticalPnl) "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING" "Theoretical PnL summary missing."
Require-True ([bool]$archive.safeSummariesArchived.reconciliation) "PMS_EMS_OMS_R007_FAIL_RECONCILIATION_ARCHIVE_MISSING" "Reconciliation summary missing."
Require-True ([bool]$archive.safeSummariesArchived.theoreticalVsReal) "PMS_EMS_OMS_R007_FAIL_THEORETICAL_REAL_ARCHIVE_MISSING" "Theoretical-vs-real summary missing."
Require-True ([bool]$archive.safeSummariesArchived.nonExecutableRebalanceIntents) "PMS_EMS_OMS_R007_FAIL_REBALANCE_INTENT_EXECUTABLE" "Rebalance intent archive missing."
Require-True ([bool]$archive.safeSummariesArchived.missingStaleMarkStatus) "PMS_EMS_OMS_R007_FAIL_MISSING_STALE_WARNING_MISSING" "Missing/stale mark archive missing."
Require-False ([bool]$archive.forbiddenRuntime.brokerCalled) "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker call detected."
Require-False ([bool]$archive.forbiddenRuntime.liveMarketDataRequested) "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Live market data requested."
Require-False ([bool]$archive.forbiddenRuntime.ordersCreated) "PMS_EMS_OMS_R007_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders created."
Require-False ([bool]$archive.forbiddenRuntime.ordersSubmitted) "PMS_EMS_OMS_R007_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders submitted."
Require-False ([bool]$archive.forbiddenRuntime.schedulerOrBackgroundJobIntroduced) "PMS_EMS_OMS_R007_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Scheduler/background job introduced."
Require-False ([bool]$archive.forbiddenRuntime.apiOrWorkerStarted) "PMS_EMS_OMS_R007_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "API/Worker started."
Require-False ([bool]$archive.forbiddenRuntime.liveTradingStateMutated) "PMS_EMS_OMS_R007_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Live trading state mutated."

Require-True ([bool]$report.operatorCycleReportCreated) "PMS_EMS_OMS_R007_FAIL_OPERATOR_REPORT_MISSING" "Operator report artifact missing."
Require-True ([string]$report.cycleRunId -eq [string]$archive.cycleRunId) "PMS_EMS_OMS_R007_FAIL_OPERATOR_REPORT_MISSING" "Report CycleRunId does not match archive."
Require-True ([string]$report.qubesRunId -eq [string]$archive.qubesRunId) "PMS_EMS_OMS_R007_FAIL_OPERATOR_REPORT_MISSING" "Report QubesRunId does not match archive."
Require-True ([string]$report.cycleStatus -eq "CompletedWithMissingMarks") "PMS_EMS_OMS_R007_FAIL_MISSING_STALE_WARNING_MISSING" "Report does not preserve missing mark status."
Require-True ([string]$report.rebalanceIntentSummary -match "non-executable") "PMS_EMS_OMS_R007_FAIL_REBALANCE_INTENT_EXECUTABLE" "Report does not mark intents non-executable."
Require-True (($report.disclaimers -contains "No external broker call occurred.")) "PMS_EMS_OMS_R007_FAIL_OPERATOR_REPORT_MISSING" "Operator report omits no external broker disclaimer."
Require-True (($report.disclaimers -contains "No live market data was requested.")) "PMS_EMS_OMS_R007_FAIL_OPERATOR_REPORT_MISSING" "Operator report omits no live market data disclaimer."
Require-True (($report.disclaimers -contains "No orders were created.")) "PMS_EMS_OMS_R007_FAIL_OPERATOR_REPORT_MISSING" "Operator report omits no orders disclaimer."
Require-True (($report.disclaimers -contains "No trading occurred.")) "PMS_EMS_OMS_R007_FAIL_OPERATOR_REPORT_MISSING" "Operator report omits no trading disclaimer."
Require-True (([string]::Join(" ", $report.disclaimers)) -match "fixture-based") "PMS_EMS_OMS_R007_FAIL_OPERATOR_REPORT_MISSING" "Operator report omits fixture-based state disclaimer."
Require-True (($report.missingStaleMarkWarnings.Count) -gt 0) "PMS_EMS_OMS_R007_FAIL_MISSING_STALE_WARNING_MISSING" "Operator report missing stale/missing mark warnings."

$markdownReportPath = Join-Path $artifactRoot "phase-pms-ems-oms-r007-cycle-report.md"
$markdown = Get-Content -LiteralPath $markdownReportPath -Raw
foreach ($requiredText in @(
    "No external broker call occurred.",
    "No live market data was requested.",
    "No orders were created.",
    "No trading occurred.",
    "fixture-based",
    "Missing/Stale Mark Warnings"
)) {
    if ($markdown -notmatch [regex]::Escape($requiredText)) {
        Fail-Gate "PMS_EMS_OMS_R007_FAIL_OPERATOR_REPORT_MISSING" "Operator markdown report missing: $requiredText"
    }
}

Require-True ([bool]$lineage.qubesLineagePreservationCreated) "PMS_EMS_OMS_R007_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage preservation missing."
Require-True ([string]$lineage.sourceSystem -eq "ModelWeightSourceSystem.Qubes") "PMS_EMS_OMS_R007_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes source missing."
Require-True ([int]$lineage.cadenceMinutes -eq 15) "PMS_EMS_OMS_R007_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes cadence missing."
Require-True ([int]$lineage.rawInputRowCount -gt 0) "PMS_EMS_OMS_R007_FAIL_QUBES_LINEAGE_WEAKENED" "Raw row count missing."
Require-True ([int]$lineage.normalizedOutputRowCount -gt 0) "PMS_EMS_OMS_R007_FAIL_QUBES_LINEAGE_WEAKENED" "Normalized row count missing."
Require-True ([bool]$lineage.qubesAuditBatchArchived) "PMS_EMS_OMS_R007_FAIL_QUBES_LINEAGE_WEAKENED" "Audit batch not archived."
Require-True ([bool]$lineage.rawQubesRowAuditArchived) "PMS_EMS_OMS_R007_FAIL_QUBES_LINEAGE_WEAKENED" "Raw audit rows not archived."
Require-True ([bool]$lineage.normalizedRowAuditArchived) "PMS_EMS_OMS_R007_FAIL_QUBES_LINEAGE_WEAKENED" "Normalized audit rows not archived."
Require-True ([bool]$lineage.modelWeightBatchLinkageArchived) "PMS_EMS_OMS_R007_FAIL_QUBES_LINEAGE_WEAKENED" "ModelWeightBatch linkage not archived."
Require-True ([bool]$lineage.modelRunLinkageArchived) "PMS_EMS_OMS_R007_FAIL_QUBES_LINEAGE_WEAKENED" "ModelRun linkage not archived."
Require-True ([bool]$lineage.targetWeightLinkageArchived) "PMS_EMS_OMS_R007_FAIL_QUBES_LINEAGE_WEAKENED" "TargetWeight linkage not archived."
Require-False ([bool]$lineage.rawRowsSerializedAsBrokerPayloads) "PMS_EMS_OMS_R007_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw rows serialized as broker payloads."
Require-False ([bool]$lineage.normalizedRowsSerializedAsMarketDataPayloads) "PMS_EMS_OMS_R007_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Normalized rows serialized as market data payloads."

Require-True ([bool]$target.targetPortfolioArchiveCreated) "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING" "Target portfolio archive missing."
Require-True ([string]$target.stateSource -eq "Theoretical") "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING" "Target portfolio is not theoretical."
Require-True ([int]$target.positionCount -eq 13) "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING" "Target portfolio position count unexpected."
Require-True ([bool]$target.usesR002NormalizedUsdQuoteWeights) "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING" "R002 normalized weights not used."
Require-True ([bool]$target.safeSummaryOnly) "PMS_EMS_OMS_R007_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Target archive is not safe summary only."
Require-False ([bool]$target.rawMarketDataFixturePayloadsSerialized) "PMS_EMS_OMS_R007_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw market-data fixture payload serialized."
Require-False ([bool]$target.brokerReportedActualState) "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Target archive uses broker actual state."

Require-True ([bool]$pnl.theoreticalPnlArchiveCreated) "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING" "Theoretical PnL archive missing."
Require-True ([string]$pnl.pnlSource -eq "Theoretical") "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING" "PnL source not theoretical."
Require-True ([bool]$pnl.fixtureBasedOnly) "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "PnL is not fixture-based only."
Require-True ([decimal]$pnl.theoreticalTotalPnl -eq 6804.66) "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING" "Theoretical PnL total unexpected."
Require-True ([string]$pnl.pnlStatus -eq "MissingMark") "PMS_EMS_OMS_R007_FAIL_MISSING_STALE_WARNING_MISSING" "MissingMark PnL status not preserved."
Require-True ([bool]$pnl.missingMarkStatusPreserved) "PMS_EMS_OMS_R007_FAIL_MISSING_STALE_WARNING_MISSING" "MissingMark status missing."
Require-True ([bool]$pnl.staleMarkStatusPreserved) "PMS_EMS_OMS_R007_FAIL_MISSING_STALE_WARNING_MISSING" "StaleMark status missing."
Require-False ([bool]$pnl.usesLiveMarketData) "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "PnL uses live market data."
Require-False ([bool]$pnl.brokerReportedPnlClaimed) "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker PnL claimed."
Require-False ([bool]$pnl.rawBrokerMarketDataPayloadsSerialized) "PMS_EMS_OMS_R007_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw broker market-data payload serialized."
Require-False ([bool]$pnl.rawMarketDataFixturePayloadsSerialized) "PMS_EMS_OMS_R007_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw market-data fixture payload serialized."

Require-True ([bool]$recon.reconciliationArchiveCreated) "PMS_EMS_OMS_R007_FAIL_RECONCILIATION_ARCHIVE_MISSING" "Reconciliation archive missing."
Require-True ([bool]$recon.usesExistingPortfolioReconciler) "PMS_EMS_OMS_R007_FAIL_RECONCILIATION_ARCHIVE_MISSING" "Existing reconciler not used."
Require-True ([string]$recon.actualPortfolioStateSource -eq "Simulated") "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Actual portfolio is not simulated fixture."
Require-False ([bool]$recon.brokerReportedActualState) "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker reported actual state used."
Require-True ([int]$recon.lineCount -eq 13) "PMS_EMS_OMS_R007_FAIL_RECONCILIATION_ARCHIVE_MISSING" "Reconciliation line count unexpected."
Require-True ([int]$recon.statusCounts.Drift -ge 2) "PMS_EMS_OMS_R007_FAIL_RECONCILIATION_ARCHIVE_MISSING" "Drift rows missing."
Require-True ([int]$recon.statusCounts.MissingActual -ge 1) "PMS_EMS_OMS_R007_FAIL_RECONCILIATION_ARCHIVE_MISSING" "Missing actual row missing."
Require-True ([int]$recon.statusCounts.MissingMark -ge 1) "PMS_EMS_OMS_R007_FAIL_MISSING_STALE_WARNING_MISSING" "Missing mark row missing."
Require-True ([bool]$recon.missingStaleMarkEffectsArchived) "PMS_EMS_OMS_R007_FAIL_MISSING_STALE_WARNING_MISSING" "Missing/stale mark effects not archived."
Require-False ([bool]$recon.lmaxLiveGapsBlockReconciliation) "PMS_EMS_OMS_R007_FAIL_LMAX_GAP_BLOCKS_REPORTING" "LMAX gaps block reconciliation."

Require-True ([bool]$comparison.theoreticalVsRealArchiveCreated) "PMS_EMS_OMS_R007_FAIL_THEORETICAL_REAL_ARCHIVE_MISSING" "Theoretical-vs-real archive missing."
Require-True ([bool]$comparison.usesExistingTheoreticalVsRealComparator) "PMS_EMS_OMS_R007_FAIL_THEORETICAL_REAL_ARCHIVE_MISSING" "Existing comparator not used."
Require-True ([decimal]$comparison.theoreticalPnl -eq 6804.66) "PMS_EMS_OMS_R007_FAIL_THEORETICAL_REAL_ARCHIVE_MISSING" "Theoretical PnL unexpected."
Require-True ([decimal]$comparison.actualFixturePnl -eq 6704.66) "PMS_EMS_OMS_R007_FAIL_THEORETICAL_REAL_ARCHIVE_MISSING" "Actual fixture PnL unexpected."
Require-True ([decimal]$comparison.pnlDifference -eq -100.00) "PMS_EMS_OMS_R007_FAIL_THEORETICAL_REAL_ARCHIVE_MISSING" "PnL difference unexpected."
Require-True ([string]$comparison.status -eq "Drift") "PMS_EMS_OMS_R007_FAIL_THEORETICAL_REAL_ARCHIVE_MISSING" "Comparator status missing."
Require-True ([bool]$comparison.actualStateIsFixture) "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Actual state is not fixture."
Require-False ([bool]$comparison.livePnlClaimed) "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Live PnL claimed."
Require-False ([bool]$comparison.brokerReportedPnlClaimed) "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker PnL claimed."
Require-False ([bool]$comparison.rawMarketDataFixturePayloadsSerialized) "PMS_EMS_OMS_R007_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw market-data fixture payload serialized."

Require-True ([bool]$intents.rebalanceIntentArchiveCreated) "PMS_EMS_OMS_R007_FAIL_REBALANCE_INTENT_EXECUTABLE" "Rebalance intent archive missing."
Require-True ([int]$intents.intentCount -gt 0) "PMS_EMS_OMS_R007_FAIL_REBALANCE_INTENT_EXECUTABLE" "Rebalance intents missing."
Require-True ([bool]$intents.rebalanceIntentsRemainNonExecutable) "PMS_EMS_OMS_R007_FAIL_REBALANCE_INTENT_EXECUTABLE" "Rebalance intents became executable."
Require-False ([bool]$intents.isExecutable) "PMS_EMS_OMS_R007_FAIL_REBALANCE_INTENT_EXECUTABLE" "Intent archive marks intents executable."
Require-False ([bool]$intents.ordersCreated) "PMS_EMS_OMS_R007_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders created."
Require-False ([bool]$intents.ordersSubmitted) "PMS_EMS_OMS_R007_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders submitted."
Require-False ([bool]$intents.brokerGatewayCalled) "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker gateway called."
Require-True (($intents.archivedStatuses -contains "TheoreticalOnly")) "PMS_EMS_OMS_R007_FAIL_REBALANCE_INTENT_EXECUTABLE" "TheoreticalOnly status missing."
Require-True (($intents.archivedStatuses -contains "NotExecutable")) "PMS_EMS_OMS_R007_FAIL_REBALANCE_INTENT_EXECUTABLE" "NotExecutable status missing."
Require-True (($intents.archivedStatuses -contains "BlockedNoOMS")) "PMS_EMS_OMS_R007_FAIL_REBALANCE_INTENT_EXECUTABLE" "BlockedNoOMS status missing."

Require-True ([bool]$intentAudit.nonExecutableIntentAuditCreated) "PMS_EMS_OMS_R007_FAIL_REBALANCE_INTENT_EXECUTABLE" "Non-executable intent audit missing."
Require-True ([bool]$intentAudit.allRebalanceIntentsNonExecutable) "PMS_EMS_OMS_R007_FAIL_REBALANCE_INTENT_EXECUTABLE" "Executable rebalance intent present."
Require-True ([bool]$intentAudit.allIntentLinesHaveTheoreticalOnlyStatus) "PMS_EMS_OMS_R007_FAIL_REBALANCE_INTENT_EXECUTABLE" "TheoreticalOnly status missing."
Require-True ([bool]$intentAudit.allIntentLinesHaveNotExecutableStatus) "PMS_EMS_OMS_R007_FAIL_REBALANCE_INTENT_EXECUTABLE" "NotExecutable status missing."
Require-True ([bool]$intentAudit.allIntentLinesHaveBlockedNoOmsStatus) "PMS_EMS_OMS_R007_FAIL_REBALANCE_INTENT_EXECUTABLE" "BlockedNoOMS status missing."
Require-False ([bool]$intentAudit.canBeSubmittedAsOrder) "PMS_EMS_OMS_R007_FAIL_REBALANCE_INTENT_EXECUTABLE" "Intent can be submitted as order."
Require-False ([bool]$intentAudit.omsOrderCreated) "PMS_EMS_OMS_R007_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "OMS order created."
Require-False ([bool]$intentAudit.parentOrderCreated) "PMS_EMS_OMS_R007_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Parent order created."
Require-False ([bool]$intentAudit.childOrderCreated) "PMS_EMS_OMS_R007_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Child order created."
Require-False ([bool]$intentAudit.brokerOrderCreated) "PMS_EMS_OMS_R007_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Broker order created."
Require-False ([bool]$intentAudit.orderSubmissionIntroduced) "PMS_EMS_OMS_R007_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order submission introduced."
Require-False ([bool]$intentAudit.executableOrderCreated) "PMS_EMS_OMS_R007_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Executable order created."

Require-True ([bool]$warnings.missingStaleMarkWarningReportCreated) "PMS_EMS_OMS_R007_FAIL_MISSING_STALE_WARNING_MISSING" "Missing/stale warning report missing."
Require-True ([int]$warnings.warningCount -gt 0) "PMS_EMS_OMS_R007_FAIL_MISSING_STALE_WARNING_MISSING" "Missing/stale warnings missing."
Require-True ([bool]$warnings.missingMarkStatusPreserved) "PMS_EMS_OMS_R007_FAIL_MISSING_STALE_WARNING_MISSING" "MissingMark not preserved."
Require-True ([bool]$warnings.staleMarkStatusPreserved) "PMS_EMS_OMS_R007_FAIL_MISSING_STALE_WARNING_MISSING" "StaleMark not preserved."
Require-False ([bool]$warnings.missingOrStaleMarksHidden) "PMS_EMS_OMS_R007_FAIL_MISSING_STALE_WARNING_MISSING" "Missing/stale marks hidden."
Require-True ([bool]$warnings.operatorReportIncludesWarnings) "PMS_EMS_OMS_R007_FAIL_MISSING_STALE_WARNING_MISSING" "Operator warning missing."
Require-False ([bool]$warnings.fabricatedPnlForMissingMarks) "PMS_EMS_OMS_R007_FAIL_MISSING_STALE_WARNING_MISSING" "PnL fabricated for missing marks."
Require-False ([bool]$warnings.rawMarketDataFixturePayloadsSerialized) "PMS_EMS_OMS_R007_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw market-data fixture payload serialized."

Require-True ([bool]$idempotency.cycleIdempotencyEvidenceCreated) "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING" "Cycle idempotency evidence missing."
Require-True ([string]$idempotency.idempotencyKey -eq "CycleRunId") "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING" "Idempotency key is not CycleRunId."
Require-True ([bool]$idempotency.firstArchivePersisted) "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING" "First archive not persisted."
Require-True ([bool]$idempotency.secondArchiveAlreadyArchived) "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING" "Duplicate archive not identified."
Require-False ([bool]$idempotency.secondArchivePersisted) "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING" "Duplicate archive persisted again."
Require-False ([bool]$idempotency.duplicateArchiveRecordsCreated) "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING" "Duplicate archive records created."
Require-True ([bool]$idempotency.returnsExistingArchiveRecord) "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING" "Existing archive record not returned."
Require-True ([bool]$idempotency.safeDuplicateHandling) "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING" "Duplicate handling not safe."

Require-True ([bool]$universe.instrumentUniverseHandlingCreated) "PMS_EMS_OMS_R007_FAIL_LMAX_GAP_BLOCKS_REPORTING" "Instrument universe artifact missing."
Require-True ([bool]$universe.qubesTargetUniverseMayBeBroaderThanLmaxValidatedScope) "PMS_EMS_OMS_R007_FAIL_LMAX_GAP_BLOCKS_REPORTING" "Qubes-vs-LMAX universe distinction missing."
Require-False ([bool]$universe.lmaxReadOnlyScopeUsedAsArchiveGate) "PMS_EMS_OMS_R007_FAIL_LMAX_GAP_BLOCKS_REPORTING" "LMAX scope gates archive."
Require-False ([bool]$universe.lmaxLiveValidationGapsBlockArchive) "PMS_EMS_OMS_R007_FAIL_LMAX_GAP_BLOCKS_REPORTING" "LMAX gaps block archive."
Require-False ([bool]$universe.audusdLiveValidationGapBlocksArchive) "PMS_EMS_OMS_R007_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD gap blocks archive."
Require-False ([bool]$universe.usdjpyLiveValidationGapBlocksArchive) "PMS_EMS_OMS_R007_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY gap blocks archive."
Require-True ([bool]$universe.instrumentsWithoutBrokerValidationHandledSafely) "PMS_EMS_OMS_R007_FAIL_LMAX_GAP_BLOCKS_REPORTING" "Unvalidated instruments not safe."
Require-True ([bool]$universe.instrumentsWithoutFixtureMarksHandledSafely) "PMS_EMS_OMS_R007_FAIL_MISSING_STALE_WARNING_MISSING" "Missing fixture marks not safe."

Require-True ([bool]$usdjpy.usdJpyCaveatPreservationCreated) "PMS_EMS_OMS_R007_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat artifact missing."
Require-True ([string]$usdjpy.usdJpySecurityId -eq "4004") "PMS_EMS_OMS_R007_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID caveat missing."
Require-True ([string]$usdjpy.usdJpySecurityIdSource -eq "8") "PMS_EMS_OMS_R007_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource caveat missing."
Require-True ([bool]$usdjpy.usdJpyNotProven) "PMS_EMS_OMS_R007_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY not-proven missing."
Require-False ([bool]$usdjpy.usdJpyClassifiedAsFailed) "PMS_EMS_OMS_R007_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY classified as failed."
Require-True ([bool]$usdjpy.audusdTlsBoundaryInconclusive) "PMS_EMS_OMS_R007_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD TLS-boundary status missing."
Require-False ([bool]$usdjpy.audusdClassifiedAsFailed) "PMS_EMS_OMS_R007_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD classified as failed."
Require-False ([bool]$usdjpy.lmaxLiveValidationGapsBlockArchive) "PMS_EMS_OMS_R007_FAIL_LMAX_GAP_BLOCKS_REPORTING" "LMAX gaps block archive."
Require-False ([bool]$usdjpy.lmaxLiveValidationGapsBlockReporting) "PMS_EMS_OMS_R007_FAIL_LMAX_GAP_BLOCKS_REPORTING" "LMAX gaps block reporting."

Require-True ([bool]$lmax.lmaxReadonlyBaselineReferenceCreated) "PMS_EMS_OMS_R007_FAIL_LMAX_GAP_BLOCKS_REPORTING" "LMAX baseline missing."
Require-False ([bool]$lmax.lmaxUsedInThisPhase) "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX used in this phase."
Require-False ([bool]$lmax.lmaxCalledInThisPhase) "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX called in this phase."
Require-False ([bool]$lmax.lmaxLiveValidationGapsBlockArchive) "PMS_EMS_OMS_R007_FAIL_LMAX_GAP_BLOCKS_REPORTING" "LMAX gaps block archive."
Require-False ([bool]$lmax.lmaxLiveValidationGapsBlockReporting) "PMS_EMS_OMS_R007_FAIL_LMAX_GAP_BLOCKS_REPORTING" "LMAX gaps block reporting."
Require-True ([bool]$lmax.baseline.GBPUSD.readOnlyMarketDataSucceeded) "PMS_EMS_OMS_R007_FAIL_LMAX_GAP_BLOCKS_REPORTING" "GBPUSD baseline missing."
Require-True ([int]$lmax.baseline.GBPUSD.sanitizedEntryCount -eq 2) "PMS_EMS_OMS_R007_FAIL_LMAX_GAP_BLOCKS_REPORTING" "GBPUSD sanitized count missing."
Require-True ([bool]$lmax.baseline.EURGBP.readOnlyMarketDataSucceeded) "PMS_EMS_OMS_R007_FAIL_LMAX_GAP_BLOCKS_REPORTING" "EURGBP baseline missing."
Require-True ([int]$lmax.baseline.EURGBP.sanitizedEntryCount -eq 2) "PMS_EMS_OMS_R007_FAIL_LMAX_GAP_BLOCKS_REPORTING" "EURGBP sanitized count missing."
Require-True ([bool]$lmax.baseline.AUDUSD.tlsBoundaryInconclusive) "PMS_EMS_OMS_R007_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD TLS-boundary status missing."
Require-False ([bool]$lmax.baseline.AUDUSD.classifiedAsFailed) "PMS_EMS_OMS_R007_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD classified as failed."
Require-True ([bool]$lmax.baseline.USDJPY.notProven) "PMS_EMS_OMS_R007_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY not-proven missing."
Require-False ([bool]$lmax.baseline.USDJPY.classifiedAsFailed) "PMS_EMS_OMS_R007_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY classified as failed."
Require-True ([string]$lmax.baseline.USDJPY.securityId -eq "4004") "PMS_EMS_OMS_R007_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY baseline SecurityID missing."
Require-True ([string]$lmax.baseline.USDJPY.securityIdSource -eq "8") "PMS_EMS_OMS_R007_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY baseline SecurityIDSource missing."

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
    "lmaxLiveValidationGapsBlockArchive",
    "lmaxLiveValidationGapsBlockReporting",
    "rebalanceIntentExecutable",
    "actualPortfolioBrokerReportedLiveState"
)) {
    Require-False ([bool]$noExternal.$property) "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected: $property"
}

foreach ($item in $forbidden.forbiddenActions) {
    if ([bool]$item.detected) {
        if ([string]$item.action -match "scheduler|service|timer|background") {
            Fail-Gate "PMS_EMS_OMS_R007_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "order|trading|rebalance|executable") {
            Fail-Gate "PMS_EMS_OMS_R007_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden action detected: $($item.action)"
        }

        Fail-Gate "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $($item.action)"
    }
}

Require-True ([bool]$nextPhase.nextPhaseRecommendationCreated) "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING" "Next phase recommendation missing."
Require-True ([string]$nextPhase.recommendedNextPhase -eq "PMS-EMS-OMS-R008") "PMS_EMS_OMS_R007_FAIL_CYCLE_ARCHIVE_MISSING" "Next phase is not R008."
Require-True ([bool]$nextPhase.mustRemainNoExternal) "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase not no-external."
Require-True ([bool]$nextPhase.mustNotStartScheduler) "PMS_EMS_OMS_R007_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Next phase permits scheduler."
Require-True ([bool]$nextPhase.mustNotCallBroker) "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase permits broker calls."
Require-True ([bool]$nextPhase.mustNotRequestLiveMarketData) "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase permits live market data."
Require-True ([bool]$nextPhase.mustNotGenerateExecutableOrders) "PMS_EMS_OMS_R007_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Next phase permits executable orders."

$apiSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Api/appsettings.json") "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$workerSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/appsettings.json") "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED"

Require-False ([bool]$apiSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R007_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API live trading enabled."
Require-False ([bool]$workerSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R007_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker live trading enabled."
Require-False ([bool]$apiSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API external connections enabled."
Require-False ([bool]$workerSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker external connections enabled."
Require-True ([bool]$apiSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R007_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API fake gateway not required."
Require-True ([bool]$workerSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R007_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker fake gateway not required."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.Enabled) "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX read-only runtime enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowExternalConnections) "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX read-only runtime allows external connections."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowOrderSubmission) "PMS_EMS_OMS_R007_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "LMAX read-only runtime allows order submission."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SchedulerEnabled) "PMS_EMS_OMS_R007_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "LMAX scheduler enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SubmitToShadowReplay) "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX submits to shadow replay."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.PersistRawFixMessages) "PMS_EMS_OMS_R007_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "LMAX persists raw FIX."
Require-False ([bool]$workerSettings.MarketDataBars.Enabled) "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker market-data bars enabled."

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-pms-ems-oms-r007-*" -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
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
        Fail-Gate "PMS_EMS_OMS_R007_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Unsafe serialized content pattern found: $pattern"
    }
}

$requiredFiles = @(
    "src/QQ.Production.Intraday.Application/QubesIntradayCycleArchive.cs",
    "tests/QQ.Production.Intraday.Tests.Unit/QubesIntradayCycleArchiveTests.cs"
)

foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $file))) {
        Fail-Gate "PMS_EMS_OMS_R007_FAIL_BUILD_OR_TESTS" "Required implementation/test file missing: $file"
    }
}

$source = Get-Content -LiteralPath (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/QubesIntradayCycleArchive.cs") -Raw
foreach ($pattern in @("TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "SendOrderAsync", "SubmitOrder", "ParentOrder", "ChildOrder", "FixSession")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R007_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "R007 source contains forbidden runtime pattern: $pattern"
    }
}

foreach ($pattern in @("AddHostedService", "IHostedService", "BackgroundService", "PeriodicTimer", "Task.Delay", "System.Threading.Timer")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R007_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "R007 source contains scheduler/service pattern: $pattern"
    }
}

$tests = Get-Content -LiteralPath (Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/QubesIntradayCycleArchiveTests.cs") -Raw
foreach ($requiredTestName in @(
    "R006_cycle_output_can_be_archived_no_externally",
    "Cycle_run_id_and_qubes_run_id_are_preserved",
    "Qubes_lineage_references_are_preserved",
    "Target_portfolio_summary_is_archived",
    "Theoretical_pnl_summary_is_archived",
    "Reconciliation_summary_is_archived",
    "Theoretical_vs_real_summary_is_archived",
    "Rebalance_intents_are_archived_as_non_executable",
    "Missing_and_stale_mark_status_is_preserved_in_warning_report",
    "Operator_report_includes_no_external_and_no_trading_disclaimer",
    "Duplicate_cycle_run_id_is_idempotent",
    "No_order_submission_or_executable_order_is_archived",
    "Archive_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action",
    "Archive_source_introduces_no_scheduler_timer_polling_or_background_job",
    "Api_and_worker_live_gateway_remain_disabled",
    "Audusd_is_not_misclassified_as_failed",
    "Usdjpy_caveat_remains_preserved"
)) {
    if ($tests -notmatch [regex]::Escape($requiredTestName)) {
        Fail-Gate "PMS_EMS_OMS_R007_FAIL_BUILD_OR_TESTS" "Focused test missing: $requiredTestName"
    }
}

Require-True ([string]$evidence.build.status -eq "PASS") "PMS_EMS_OMS_R007_FAIL_BUILD_OR_TESTS" "Build evidence missing or not PASS."
Require-True ([int]$evidence.build.failed -eq 0) "PMS_EMS_OMS_R007_FAIL_BUILD_OR_TESTS" "Build evidence has failures."
Require-True ([string]$evidence.focusedTests.status -eq "PASS") "PMS_EMS_OMS_R007_FAIL_BUILD_OR_TESTS" "Focused test evidence missing or not PASS."
Require-True ([int]$evidence.focusedTests.failed -eq 0) "PMS_EMS_OMS_R007_FAIL_BUILD_OR_TESTS" "Focused tests have failures."
Require-True ([string]$evidence.unitTests.status -eq "PASS") "PMS_EMS_OMS_R007_FAIL_BUILD_OR_TESTS" "Unit test evidence missing or not PASS."
Require-True ([int]$evidence.unitTests.failed -eq 0) "PMS_EMS_OMS_R007_FAIL_BUILD_OR_TESTS" "Unit tests have failures."
Require-True ([string]$evidence.validator.status -eq "PASS") "PMS_EMS_OMS_R007_FAIL_BUILD_OR_TESTS" "Validator evidence missing or not PASS."
Require-True ([int]$evidence.validator.failed -eq 0) "PMS_EMS_OMS_R007_FAIL_BUILD_OR_TESTS" "Validator evidence has failures."
Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R007_FAIL_BUILD_OR_TESTS" "Build/test/validator evidence marker missing."

Write-Host "PMS_EMS_OMS_R007_PASS_INTRADAY_CYCLE_ARCHIVE_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R007_PASS_OPERATOR_REPORT_READY_NO_EXTERNAL"
