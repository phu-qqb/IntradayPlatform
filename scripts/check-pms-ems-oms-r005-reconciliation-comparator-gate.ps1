param(
    [string]$ArtifactDirectory = "artifacts/readiness/pms-ems-oms-integration"
)

$ErrorActionPreference = "Stop"

function Fail-Gate {
    param(
        [string]$Classification,
        [string]$Message
    )

    Write-Error "$Classification`: $Message"
    exit 1
}

function Read-JsonArtifact {
    param(
        [string]$Path,
        [string]$MissingClassification
    )

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
    "phase-pms-ems-oms-r005-summary.md" = "PMS_EMS_OMS_R005_FAIL_RECONCILIATION_REPORT_MISSING"
    "phase-pms-ems-oms-r005-target-vs-actual-reconciliation.json" = "PMS_EMS_OMS_R005_FAIL_RECONCILIATION_REPORT_MISSING"
    "phase-pms-ems-oms-r005-theoretical-vs-real-report.json" = "PMS_EMS_OMS_R005_FAIL_THEORETICAL_REAL_REPORT_MISSING"
    "phase-pms-ems-oms-r005-actual-portfolio-fixture.json" = "PMS_EMS_OMS_R005_FAIL_ACTUAL_PORTFOLIO_FIXTURE_MISSING"
    "phase-pms-ems-oms-r005-actual-pnl-fixture.json" = "PMS_EMS_OMS_R005_FAIL_ACTUAL_PNL_FIXTURE_MISSING"
    "phase-pms-ems-oms-r005-drift-report.json" = "PMS_EMS_OMS_R005_FAIL_RECONCILIATION_REPORT_MISSING"
    "phase-pms-ems-oms-r005-missing-and-stale-data-handling.json" = "PMS_EMS_OMS_R005_FAIL_MISSING_STALE_HANDLING_MISSING"
    "phase-pms-ems-oms-r005-qubes-metadata-preservation.json" = "PMS_EMS_OMS_R005_FAIL_QUBES_METADATA_WEAKENED"
    "phase-pms-ems-oms-r005-qubes-db-lineage-preservation.json" = "PMS_EMS_OMS_R005_FAIL_QUBES_DB_LINEAGE_WEAKENED"
    "phase-pms-ems-oms-r005-rebalance-intent-preservation.json" = "PMS_EMS_OMS_R005_FAIL_REBALANCE_INTENT_EXECUTABLE"
    "phase-pms-ems-oms-r005-instrument-universe-handling.json" = "PMS_EMS_OMS_R005_FAIL_LMAX_GAP_BLOCKS_RECONCILIATION"
    "phase-pms-ems-oms-r005-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R005_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r005-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R005_FAIL_LMAX_GAP_BLOCKS_RECONCILIATION"
    "phase-pms-ems-oms-r005-no-external-audit.json" = "PMS_EMS_OMS_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r005-forbidden-actions-audit.json" = "PMS_EMS_OMS_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r005-next-phase-recommendation.json" = "PMS_EMS_OMS_R005_FAIL_RECONCILIATION_REPORT_MISSING"
    "phase-pms-ems-oms-r005-build-test-validator-evidence.json" = "PMS_EMS_OMS_R005_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    $path = Join-Path $artifactRoot $entry.Key
    if (-not (Test-Path -LiteralPath $path)) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$recon = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r005-target-vs-actual-reconciliation.json") "PMS_EMS_OMS_R005_FAIL_RECONCILIATION_REPORT_MISSING"
$report = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r005-theoretical-vs-real-report.json") "PMS_EMS_OMS_R005_FAIL_THEORETICAL_REAL_REPORT_MISSING"
$actualPortfolio = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r005-actual-portfolio-fixture.json") "PMS_EMS_OMS_R005_FAIL_ACTUAL_PORTFOLIO_FIXTURE_MISSING"
$actualPnl = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r005-actual-pnl-fixture.json") "PMS_EMS_OMS_R005_FAIL_ACTUAL_PNL_FIXTURE_MISSING"
$drift = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r005-drift-report.json") "PMS_EMS_OMS_R005_FAIL_RECONCILIATION_REPORT_MISSING"
$missing = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r005-missing-and-stale-data-handling.json") "PMS_EMS_OMS_R005_FAIL_MISSING_STALE_HANDLING_MISSING"
$metadata = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r005-qubes-metadata-preservation.json") "PMS_EMS_OMS_R005_FAIL_QUBES_METADATA_WEAKENED"
$lineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r005-qubes-db-lineage-preservation.json") "PMS_EMS_OMS_R005_FAIL_QUBES_DB_LINEAGE_WEAKENED"
$intents = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r005-rebalance-intent-preservation.json") "PMS_EMS_OMS_R005_FAIL_REBALANCE_INTENT_EXECUTABLE"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r005-instrument-universe-handling.json") "PMS_EMS_OMS_R005_FAIL_LMAX_GAP_BLOCKS_RECONCILIATION"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r005-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R005_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r005-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R005_FAIL_LMAX_GAP_BLOCKS_RECONCILIATION"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r005-no-external-audit.json") "PMS_EMS_OMS_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r005-forbidden-actions-audit.json") "PMS_EMS_OMS_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$nextPhase = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r005-next-phase-recommendation.json") "PMS_EMS_OMS_R005_FAIL_RECONCILIATION_REPORT_MISSING"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r005-build-test-validator-evidence.json") "PMS_EMS_OMS_R005_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$recon.targetVsActualReconciliationCreated) "PMS_EMS_OMS_R005_FAIL_RECONCILIATION_REPORT_MISSING" "Reconciliation report is missing."
Require-True ([bool]$recon.usesExistingPortfolioReconciler) "PMS_EMS_OMS_R005_FAIL_RECONCILIATION_REPORT_MISSING" "Existing reconciler was not used."
Require-True ([int]$recon.lineCount -eq 13) "PMS_EMS_OMS_R005_FAIL_RECONCILIATION_REPORT_MISSING" "Reconciliation line count is unexpected."
Require-True ([int]$recon.statusCounts.Drift -ge 2) "PMS_EMS_OMS_R005_FAIL_RECONCILIATION_REPORT_MISSING" "Drift rows are missing."
Require-True ([int]$recon.statusCounts.MissingActual -ge 1) "PMS_EMS_OMS_R005_FAIL_RECONCILIATION_REPORT_MISSING" "Missing actual row is missing."
Require-True ([int]$recon.statusCounts.MissingMark -ge 1) "PMS_EMS_OMS_R005_FAIL_MISSING_STALE_HANDLING_MISSING" "Missing/stale mark rows are missing."
Require-True ([bool]$recon.missingTargetHandlingSupported) "PMS_EMS_OMS_R005_FAIL_RECONCILIATION_REPORT_MISSING" "Missing target handling is not supported."
Require-True ([bool]$recon.missingActualHandlingPresent) "PMS_EMS_OMS_R005_FAIL_RECONCILIATION_REPORT_MISSING" "Missing actual handling is missing."
Require-True ([bool]$recon.missingOrStaleMarkHandlingPresent) "PMS_EMS_OMS_R005_FAIL_MISSING_STALE_HANDLING_MISSING" "Missing/stale handling is missing."
Require-False ([bool]$recon.brokerStateUsed) "PMS_EMS_OMS_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker state was used."
Require-False ([bool]$recon.liveMarketDataUsed) "PMS_EMS_OMS_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Live market data was used."
Require-False ([bool]$recon.ordersCreated) "PMS_EMS_OMS_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders were created."

$eur = $recon.sampleLines | Where-Object { [string]$_.symbol -eq "EURUSD" } | Select-Object -First 1
$gbp = $recon.sampleLines | Where-Object { [string]$_.symbol -eq "GBPUSD" } | Select-Object -First 1
$jpy = $recon.sampleLines | Where-Object { [string]$_.symbol -eq "JPYUSD" } | Select-Object -First 1
Require-True ($null -ne $eur -and [string]$eur.status -eq "Drift" -and [decimal]$eur.weightDifference -gt 0) "PMS_EMS_OMS_R005_FAIL_RECONCILIATION_REPORT_MISSING" "Overweight drift evidence is missing."
Require-True ($null -ne $gbp -and [string]$gbp.status -eq "Drift" -and [decimal]$gbp.weightDifference -lt 0) "PMS_EMS_OMS_R005_FAIL_RECONCILIATION_REPORT_MISSING" "Underweight drift evidence is missing."
Require-True ($null -ne $jpy -and [string]$jpy.status -eq "MissingActual") "PMS_EMS_OMS_R005_FAIL_RECONCILIATION_REPORT_MISSING" "Missing actual evidence is missing."

Require-True ([bool]$report.theoreticalVsRealReportCreated) "PMS_EMS_OMS_R005_FAIL_THEORETICAL_REAL_REPORT_MISSING" "Theoretical-vs-real report is missing."
Require-True ([bool]$report.usesExistingTheoreticalVsRealComparator) "PMS_EMS_OMS_R005_FAIL_THEORETICAL_REAL_REPORT_MISSING" "Existing comparator was not used."
Require-True ([string]$report.foundationStatus -eq "Drift") "PMS_EMS_OMS_R005_FAIL_THEORETICAL_REAL_REPORT_MISSING" "Comparator status is not Drift."
Require-True ([int]$report.statusCounts.InSync -ge 1) "PMS_EMS_OMS_R005_FAIL_THEORETICAL_REAL_REPORT_MISSING" "InSync comparator row missing."
Require-True ([int]$report.statusCounts.Drift -ge 2) "PMS_EMS_OMS_R005_FAIL_THEORETICAL_REAL_REPORT_MISSING" "Comparator drift rows missing."
Require-True ([int]$report.statusCounts.MissingActual -ge 1) "PMS_EMS_OMS_R005_FAIL_THEORETICAL_REAL_REPORT_MISSING" "Comparator missing actual row missing."
Require-True ([decimal]$report.portfolioPnL.theoreticalUnrealizedPnL -eq 6804.66) "PMS_EMS_OMS_R005_FAIL_THEORETICAL_REAL_REPORT_MISSING" "Theoretical PnL value is unexpected."
Require-True ([decimal]$report.portfolioPnL.actualFixtureUnrealizedPnL -eq 6704.66) "PMS_EMS_OMS_R005_FAIL_ACTUAL_PNL_FIXTURE_MISSING" "Actual fixture PnL value is unexpected."
Require-True ([decimal]$report.portfolioPnL.pnlDifference -eq -100.0) "PMS_EMS_OMS_R005_FAIL_THEORETICAL_REAL_REPORT_MISSING" "PnL difference is unexpected."
Require-False ([bool]$report.livePnlClaimed) "PMS_EMS_OMS_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Live PnL is claimed."
Require-False ([bool]$report.brokerReportedPnlClaimed) "PMS_EMS_OMS_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker-reported PnL is claimed."
Require-False ([bool]$report.brokerGatewayCalled) "PMS_EMS_OMS_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker gateway was called."
Require-False ([bool]$report.liveMarketDataUsed) "PMS_EMS_OMS_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Live market data was used."

Require-True ([bool]$actualPortfolio.actualPortfolioFixtureCreated) "PMS_EMS_OMS_R005_FAIL_ACTUAL_PORTFOLIO_FIXTURE_MISSING" "Actual portfolio fixture is missing."
Require-True ([string]$actualPortfolio.stateSource -eq "Simulated") "PMS_EMS_OMS_R005_FAIL_ACTUAL_PORTFOLIO_FIXTURE_MISSING" "Actual portfolio fixture is not simulated."
Require-False ([bool]$actualPortfolio.brokerReportedLiveState) "PMS_EMS_OMS_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Actual portfolio fixture is broker-reported."
Require-False ([bool]$actualPortfolio.liveBrokerPositionsInferred) "PMS_EMS_OMS_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Live broker positions were inferred."
Require-False ([bool]$actualPortfolio.ordersCreated) "PMS_EMS_OMS_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Orders were created."
Require-False ([bool]$actualPortfolio.liveTradingStateMutated) "PMS_EMS_OMS_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Live trading state was mutated."

Require-True ([bool]$actualPnl.actualPnlFixtureCreated) "PMS_EMS_OMS_R005_FAIL_ACTUAL_PNL_FIXTURE_MISSING" "Actual PnL fixture is missing."
Require-True ([string]$actualPnl.pnlSource -eq "Simulated") "PMS_EMS_OMS_R005_FAIL_ACTUAL_PNL_FIXTURE_MISSING" "Actual PnL fixture is not simulated."
Require-False ([bool]$actualPnl.brokerReportedLivePnl) "PMS_EMS_OMS_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Actual PnL fixture is broker-reported live PnL."
Require-False ([bool]$actualPnl.realizedPnlComputed) "PMS_EMS_OMS_R005_FAIL_ACTUAL_PNL_FIXTURE_MISSING" "Realized PnL was computed."
Require-True ([decimal]$actualPnl.portfolioPnL.unrealizedPnL -eq 6704.66) "PMS_EMS_OMS_R005_FAIL_ACTUAL_PNL_FIXTURE_MISSING" "Actual PnL fixture total is unexpected."
Require-False ([bool]$actualPnl.liveMarketDataUsed) "PMS_EMS_OMS_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Actual PnL used live market data."
Require-False ([bool]$actualPnl.rawMarketDataFixturePayloadSerialized) "PMS_EMS_OMS_R005_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw fixture payload was serialized."
Require-False ([bool]$actualPnl.rawBrokerPayloadSerialized) "PMS_EMS_OMS_R005_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw broker payload was serialized."

Require-True ([bool]$drift.driftReportCreated) "PMS_EMS_OMS_R005_FAIL_RECONCILIATION_REPORT_MISSING" "Drift report is missing."
Require-True ([bool]$drift.overweightDetected) "PMS_EMS_OMS_R005_FAIL_RECONCILIATION_REPORT_MISSING" "Overweight drift is missing."
Require-True ([bool]$drift.underweightDetected) "PMS_EMS_OMS_R005_FAIL_RECONCILIATION_REPORT_MISSING" "Underweight drift is missing."
Require-True ([bool]$drift.missingActualDetected) "PMS_EMS_OMS_R005_FAIL_RECONCILIATION_REPORT_MISSING" "Missing actual drift is missing."

Require-True ([bool]$missing.missingAndStaleHandlingCreated) "PMS_EMS_OMS_R005_FAIL_MISSING_STALE_HANDLING_MISSING" "Missing/stale handling artifact is missing."
Require-True ([bool]$missing.missingActualHandlingPresent) "PMS_EMS_OMS_R005_FAIL_MISSING_STALE_HANDLING_MISSING" "Missing actual handling missing."
Require-True ([bool]$missing.missingTargetHandlingSupported) "PMS_EMS_OMS_R005_FAIL_MISSING_STALE_HANDLING_MISSING" "Missing target handling missing."
Require-True ([bool]$missing.missingMarkHandlingPresent) "PMS_EMS_OMS_R005_FAIL_MISSING_STALE_HANDLING_MISSING" "Missing mark handling missing."
Require-True ([bool]$missing.staleMarkHandlingPresent) "PMS_EMS_OMS_R005_FAIL_MISSING_STALE_HANDLING_MISSING" "Stale mark handling missing."
Require-True ([bool]$missing.inconclusiveSafeHandlingPresent) "PMS_EMS_OMS_R005_FAIL_MISSING_STALE_HANDLING_MISSING" "Inconclusive safe handling missing."
Require-True ([bool]$missing.missingOrStaleRowsContributeZeroPnl) "PMS_EMS_OMS_R005_FAIL_MISSING_STALE_HANDLING_MISSING" "Missing/stale rows do not contribute zero PnL."
Require-True ([bool]$missing.missingOrStaleRowsDoNotFabricatePnl) "PMS_EMS_OMS_R005_FAIL_MISSING_STALE_HANDLING_MISSING" "Missing/stale rows fabricate PnL."
Require-False ([bool]$missing.rawMarketDataFixturePayloadSerialized) "PMS_EMS_OMS_R005_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw fixture payload serialized."
Require-False ([bool]$missing.rawBrokerPayloadSerialized) "PMS_EMS_OMS_R005_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Raw broker payload serialized."

Require-True ([bool]$metadata.qubesMetadataPreservationCreated) "PMS_EMS_OMS_R005_FAIL_QUBES_METADATA_WEAKENED" "Qubes metadata artifact is missing."
Require-True ([string]$metadata.qubesRunId -ne "") "PMS_EMS_OMS_R005_FAIL_QUBES_METADATA_WEAKENED" "QubesRunId missing."
Require-True ([string]$metadata.sourceSystem -eq "ModelWeightSourceSystem.Qubes") "PMS_EMS_OMS_R005_FAIL_QUBES_METADATA_WEAKENED" "Qubes source missing."
Require-True ([int]$metadata.cadenceMinutes -eq 15) "PMS_EMS_OMS_R005_FAIL_QUBES_METADATA_WEAKENED" "15-minute cadence missing."
Require-True ([int]$metadata.rawInputRowCount -gt 0) "PMS_EMS_OMS_R005_FAIL_QUBES_METADATA_WEAKENED" "Raw row count missing."
Require-True ([int]$metadata.normalizedOutputRowCount -gt 0) "PMS_EMS_OMS_R005_FAIL_QUBES_METADATA_WEAKENED" "Normalized row count missing."
Require-True ([bool]$metadata.r004bPersistedLineageConsumed) "PMS_EMS_OMS_R005_FAIL_QUBES_DB_LINEAGE_WEAKENED" "R004B persisted lineage was not consumed."

Require-True ([bool]$lineage.qubesDbLineagePreservationCreated) "PMS_EMS_OMS_R005_FAIL_QUBES_DB_LINEAGE_WEAKENED" "Qubes DB lineage artifact missing."
Require-True ([bool]$lineage.usesPersistedQubesLineage) "PMS_EMS_OMS_R005_FAIL_QUBES_DB_LINEAGE_WEAKENED" "Persisted Qubes lineage not used."
Require-True ([bool]$lineage.auditBatchPresent) "PMS_EMS_OMS_R005_FAIL_QUBES_DB_LINEAGE_WEAKENED" "Audit batch missing."
Require-True ([bool]$lineage.rawRowAuditPresent) "PMS_EMS_OMS_R005_FAIL_QUBES_DB_LINEAGE_WEAKENED" "Raw row audit missing."
Require-True ([bool]$lineage.normalizedRowAuditPresent) "PMS_EMS_OMS_R005_FAIL_QUBES_DB_LINEAGE_WEAKENED" "Normalized row audit missing."
Require-True ([bool]$lineage.modelWeightBatchLinkagePresent) "PMS_EMS_OMS_R005_FAIL_QUBES_DB_LINEAGE_WEAKENED" "ModelWeightBatch linkage missing."
Require-True ([bool]$lineage.modelRunLinkagePresent) "PMS_EMS_OMS_R005_FAIL_QUBES_DB_LINEAGE_WEAKENED" "ModelRun linkage missing."
Require-True ([bool]$lineage.targetWeightLinkagePresent) "PMS_EMS_OMS_R005_FAIL_QUBES_DB_LINEAGE_WEAKENED" "TargetWeight linkage missing."

Require-True ([bool]$intents.rebalanceIntentPreservationCreated) "PMS_EMS_OMS_R005_FAIL_REBALANCE_INTENT_EXECUTABLE" "Rebalance intent artifact missing."
Require-True ([bool]$intents.rebalanceIntentsRemainNonExecutable) "PMS_EMS_OMS_R005_FAIL_REBALANCE_INTENT_EXECUTABLE" "Rebalance intents became executable."
Require-True ([bool]$intents.allIntentLinesHaveTheoreticalOnlyStatus) "PMS_EMS_OMS_R005_FAIL_REBALANCE_INTENT_EXECUTABLE" "TheoreticalOnly status missing."
Require-True ([bool]$intents.allIntentLinesHaveNotExecutableStatus) "PMS_EMS_OMS_R005_FAIL_REBALANCE_INTENT_EXECUTABLE" "NotExecutable status missing."
Require-True ([bool]$intents.allIntentLinesHaveBlockedNoOmsStatus) "PMS_EMS_OMS_R005_FAIL_REBALANCE_INTENT_EXECUTABLE" "BlockedNoOMS status missing."
Require-False ([bool]$intents.isExecutable) "PMS_EMS_OMS_R005_FAIL_REBALANCE_INTENT_EXECUTABLE" "Intent is executable."
Require-False ([bool]$intents.executableOrderCreated) "PMS_EMS_OMS_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Executable order created."
Require-False ([bool]$intents.orderSubmissionIntroduced) "PMS_EMS_OMS_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order submission introduced."
Require-False ([bool]$intents.brokerGatewayCalled) "PMS_EMS_OMS_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Broker gateway called."

Require-True ([bool]$universe.instrumentUniverseHandlingCreated) "PMS_EMS_OMS_R005_FAIL_LMAX_GAP_BLOCKS_RECONCILIATION" "Instrument universe artifact missing."
Require-False ([bool]$universe.lmaxReadOnlyScopeUsedAsReconciliationGate) "PMS_EMS_OMS_R005_FAIL_LMAX_GAP_BLOCKS_RECONCILIATION" "LMAX scope gates reconciliation."
Require-False ([bool]$universe.lmaxLiveValidationGapsBlockReconciliation) "PMS_EMS_OMS_R005_FAIL_LMAX_GAP_BLOCKS_RECONCILIATION" "LMAX gaps block reconciliation."
Require-False ([bool]$universe.audusdLiveValidationGapBlocksReconciliation) "PMS_EMS_OMS_R005_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD gap blocks reconciliation."
Require-False ([bool]$universe.usdjpyLiveValidationGapBlocksReconciliation) "PMS_EMS_OMS_R005_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY gap blocks reconciliation."
Require-True ([bool]$universe.instrumentsWithoutBrokerValidationHandledSafely) "PMS_EMS_OMS_R005_FAIL_LMAX_GAP_BLOCKS_RECONCILIATION" "Unvalidated instruments are unsafe."
Require-True ([bool]$universe.instrumentsWithoutFixtureMarksHandledSafely) "PMS_EMS_OMS_R005_FAIL_MISSING_STALE_HANDLING_MISSING" "Missing fixture marks are unsafe."

Require-True ([bool]$usdjpy.usdJpyCaveatPreservationCreated) "PMS_EMS_OMS_R005_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat artifact missing."
Require-True ([string]$usdjpy.usdJpySecurityId -eq "4004") "PMS_EMS_OMS_R005_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID caveat missing."
Require-True ([string]$usdjpy.usdJpySecurityIdSource -eq "8") "PMS_EMS_OMS_R005_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource caveat missing."
Require-True ([bool]$usdjpy.usdJpyNotProven) "PMS_EMS_OMS_R005_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY not-proven status missing."
Require-False ([bool]$usdjpy.usdJpyClassifiedAsFailed) "PMS_EMS_OMS_R005_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY classified as failed."
Require-True ([bool]$usdjpy.audusdTlsBoundaryInconclusive) "PMS_EMS_OMS_R005_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD TLS-boundary status missing."
Require-False ([bool]$usdjpy.audusdClassifiedAsFailed) "PMS_EMS_OMS_R005_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD classified as failed."
Require-False ([bool]$usdjpy.lmaxLiveValidationGapsBlockReconciliation) "PMS_EMS_OMS_R005_FAIL_LMAX_GAP_BLOCKS_RECONCILIATION" "LMAX gaps block reconciliation."

Require-True ([bool]$lmax.lmaxReadonlyBaselineReferenceCreated) "PMS_EMS_OMS_R005_FAIL_LMAX_GAP_BLOCKS_RECONCILIATION" "LMAX baseline artifact missing."
Require-False ([bool]$lmax.lmaxUsedInThisPhase) "PMS_EMS_OMS_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX used in this phase."
Require-False ([bool]$lmax.lmaxLiveValidationGapsBlockReconciliation) "PMS_EMS_OMS_R005_FAIL_LMAX_GAP_BLOCKS_RECONCILIATION" "LMAX gaps block reconciliation."
Require-True ([bool]$lmax.baseline.GBPUSD.readOnlyMarketDataSucceeded) "PMS_EMS_OMS_R005_FAIL_LMAX_GAP_BLOCKS_RECONCILIATION" "GBPUSD baseline missing."
Require-True ([int]$lmax.baseline.GBPUSD.sanitizedEntryCount -eq 2) "PMS_EMS_OMS_R005_FAIL_LMAX_GAP_BLOCKS_RECONCILIATION" "GBPUSD sanitized count missing."
Require-True ([bool]$lmax.baseline.EURGBP.readOnlyMarketDataSucceeded) "PMS_EMS_OMS_R005_FAIL_LMAX_GAP_BLOCKS_RECONCILIATION" "EURGBP baseline missing."
Require-True ([int]$lmax.baseline.EURGBP.sanitizedEntryCount -eq 2) "PMS_EMS_OMS_R005_FAIL_LMAX_GAP_BLOCKS_RECONCILIATION" "EURGBP sanitized count missing."
Require-False ([bool]$lmax.baseline.AUDUSD.classifiedAsFailed) "PMS_EMS_OMS_R005_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD classified as failed."
Require-False ([bool]$lmax.baseline.USDJPY.classifiedAsFailed) "PMS_EMS_OMS_R005_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY classified as failed."
Require-True ([string]$lmax.baseline.USDJPY.securityId -eq "4004") "PMS_EMS_OMS_R005_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY baseline SecurityID missing."
Require-True ([string]$lmax.baseline.USDJPY.securityIdSource -eq "8") "PMS_EMS_OMS_R005_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY baseline SecurityIDSource missing."

foreach ($property in @(
    "externalBrokerActivationDetected",
    "socketTlsFixMarketDataRuntimeActionDetected",
    "marketDataRequestAttempted",
    "liveMarketDataResponseRead",
    "apiStarted",
    "workerStarted",
    "schedulerPollingServiceStarted",
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
    "lmaxLiveValidationGapsBlockReconciliation",
    "rebalanceIntentExecutable"
)) {
    Require-False ([bool]$noExternal.$property) "PMS_EMS_OMS_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected: $property"
}

foreach ($item in $forbidden.forbiddenActions) {
    if ([bool]$item.detected) {
        if ([string]$item.action -match "order|trading|rebalance|executable") {
            Fail-Gate "PMS_EMS_OMS_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden action detected: $($item.action)"
        }

        Fail-Gate "PMS_EMS_OMS_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $($item.action)"
    }
}

Require-True ([bool]$nextPhase.nextPhaseRecommendationCreated) "PMS_EMS_OMS_R005_FAIL_RECONCILIATION_REPORT_MISSING" "Next phase recommendation missing."
Require-True ([string]$nextPhase.recommendedNextPhase -eq "PMS-EMS-OMS-R006") "PMS_EMS_OMS_R005_FAIL_RECONCILIATION_REPORT_MISSING" "Next phase is not R006."
Require-True ([bool]$nextPhase.mustRemainNoExternal) "PMS_EMS_OMS_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase is not no-external."
Require-True ([bool]$nextPhase.mustNotUseLiveMarketData) "PMS_EMS_OMS_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase permits live market data."
Require-True ([bool]$nextPhase.mustNotGenerateExecutableOrders) "PMS_EMS_OMS_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Next phase permits executable orders."

$apiSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Api/appsettings.json") "PMS_EMS_OMS_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$workerSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/appsettings.json") "PMS_EMS_OMS_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED"

Require-False ([bool]$apiSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API live trading enabled."
Require-False ([bool]$workerSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker live trading enabled."
Require-False ([bool]$apiSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API external connections enabled."
Require-False ([bool]$workerSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker external connections enabled."
Require-True ([bool]$apiSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API fake gateway not required."
Require-True ([bool]$workerSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker fake gateway not required."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.Enabled) "PMS_EMS_OMS_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX read-only runtime enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowExternalConnections) "PMS_EMS_OMS_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX read-only runtime allows external connections."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowOrderSubmission) "PMS_EMS_OMS_R005_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "LMAX read-only runtime allows order submission."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SchedulerEnabled) "PMS_EMS_OMS_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX read-only scheduler enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SubmitToShadowReplay) "PMS_EMS_OMS_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX submits to shadow replay."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.PersistRawFixMessages) "PMS_EMS_OMS_R005_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "LMAX persists raw FIX."
Require-False ([bool]$workerSettings.MarketDataBars.Enabled) "PMS_EMS_OMS_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker market-data bars enabled."

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-pms-ems-oms-r005-*" -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
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
        Fail-Gate "PMS_EMS_OMS_R005_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Unsafe serialized content pattern found: $pattern"
    }
}

$requiredFiles = @(
    "src/QQ.Production.Intraday.Application/QubesReconciliationComparatorFixture.cs",
    "tests/QQ.Production.Intraday.Tests.Unit/QubesReconciliationComparatorFixtureTests.cs"
)

foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $file))) {
        Fail-Gate "PMS_EMS_OMS_R005_FAIL_BUILD_OR_TESTS" "Required implementation/test file missing: $file"
    }
}

$source = Get-Content -LiteralPath (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/QubesReconciliationComparatorFixture.cs") -Raw
foreach ($pattern in @("TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "SendOrderAsync", "SubmitOrder", "ParentOrder", "ChildOrder", "FixSession", "Lmax")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R005_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "R005 source contains forbidden runtime pattern: $pattern"
    }
}

$tests = Get-Content -LiteralPath (Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/QubesReconciliationComparatorFixtureTests.cs") -Raw
foreach ($requiredTestName in @(
    "Persisted_qubes_target_weights_feed_reconciliation",
    "Qubes_run_id_lineage_is_preserved_into_reports",
    "Modelweightbatch_modelrun_targetweight_linkage_is_preserved",
    "R003_target_portfolio_feeds_reconciliation",
    "R004_theoretical_pnl_feeds_theoretical_vs_real_comparator",
    "Actual_portfolio_fixture_is_no_external_and_not_broker_reported",
    "Reconciliation_detects_overweight_drift",
    "Reconciliation_detects_underweight_drift",
    "Reconciliation_detects_missing_actual_position",
    "Reconciliation_handles_missing_and_stale_marks_safely",
    "Theoretical_vs_real_comparator_computes_pnl_difference",
    "Comparator_emits_drift_when_differences_exceed_tolerance",
    "Comparator_emits_insync_when_within_tolerance",
    "Actual_pnl_fixture_is_no_external_and_not_broker_reported_live_pnl",
    "Rebalance_intents_remain_non_executable",
    "No_executable_order_or_broker_runtime_path_is_introduced",
    "Audusd_and_usdjpy_live_validation_gaps_do_not_block_reconciliation",
    "Usdjpy_caveat_remains_preserved",
    "Api_and_worker_remain_fake_gateway_only"
)) {
    if ($tests -notmatch [regex]::Escape($requiredTestName)) {
        Fail-Gate "PMS_EMS_OMS_R005_FAIL_BUILD_OR_TESTS" "Focused test missing: $requiredTestName"
    }
}

Require-True ([string]$evidence.build.status -eq "PASS") "PMS_EMS_OMS_R005_FAIL_BUILD_OR_TESTS" "Build evidence missing or not PASS."
Require-True ([string]$evidence.focusedTests.status -eq "PASS") "PMS_EMS_OMS_R005_FAIL_BUILD_OR_TESTS" "Focused test evidence missing or not PASS."
Require-True ([int]$evidence.focusedTests.failed -eq 0) "PMS_EMS_OMS_R005_FAIL_BUILD_OR_TESTS" "Focused tests have failures."
Require-True ([string]$evidence.unitTests.status -eq "PASS") "PMS_EMS_OMS_R005_FAIL_BUILD_OR_TESTS" "Unit test evidence missing or not PASS."
Require-True ([int]$evidence.unitTests.failed -eq 0) "PMS_EMS_OMS_R005_FAIL_BUILD_OR_TESTS" "Unit tests have failures."
Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R005_FAIL_BUILD_OR_TESTS" "Build/test/validator evidence marker missing."
Require-True (Test-Path -LiteralPath (Join-Path $repoRoot "scripts/check-pms-ems-oms-r005-reconciliation-comparator-gate.ps1")) "PMS_EMS_OMS_R005_FAIL_BUILD_OR_TESTS" "Validator script missing."

Write-Host "PMS_EMS_OMS_R005_PASS_TARGET_ACTUAL_RECONCILIATION_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R005_PASS_THEORETICAL_REAL_COMPARATOR_READY_NO_EXTERNAL"
