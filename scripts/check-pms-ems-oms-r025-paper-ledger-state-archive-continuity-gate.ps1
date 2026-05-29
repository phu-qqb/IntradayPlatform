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
    "phase-pms-ems-oms-r025-summary.md" = "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r025-paper-ledger-state-archive-contract.json" = "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r025-paper-ledger-state-archive.json" = "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r025-paper-ledger-state-report.md" = "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_REPORT_MISSING"
    "phase-pms-ems-oms-r025-paper-ledger-state-report.json" = "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_REPORT_MISSING"
    "phase-pms-ems-oms-r025-paper-ledger-state-lines.json" = "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r025-next-cycle-baseline-reference.json" = "PMS_EMS_OMS_R025_FAIL_NEXT_CYCLE_CONTINUITY_GATE_MISSING"
    "phase-pms-ems-oms-r025-next-cycle-continuity-gate.json" = "PMS_EMS_OMS_R025_FAIL_NEXT_CYCLE_CONTINUITY_GATE_MISSING"
    "phase-pms-ems-oms-r025-no-production-ledger-mutation-audit.json" = "PMS_EMS_OMS_R025_FAIL_PRODUCTION_LEDGER_MUTATION"
    "phase-pms-ems-oms-r025-no-live-position-mutation-audit.json" = "PMS_EMS_OMS_R025_FAIL_LIVE_POSITION_MUTATION"
    "phase-pms-ems-oms-r025-no-broker-position-mutation-audit.json" = "PMS_EMS_OMS_R025_FAIL_BROKER_POSITION_MUTATION"
    "phase-pms-ems-oms-r025-no-trading-state-mutation-audit.json" = "PMS_EMS_OMS_R025_FAIL_TRADING_STATE_MUTATION"
    "phase-pms-ems-oms-r025-no-fill-no-execution-report-audit.json" = "PMS_EMS_OMS_R025_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
    "phase-pms-ems-oms-r025-no-order-created-audit.json" = "PMS_EMS_OMS_R025_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r025-no-route-no-submission-audit.json" = "PMS_EMS_OMS_R025_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED"
    "phase-pms-ems-oms-r025-idempotency-evidence.json" = "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r025-risk-lineage-preservation.json" = "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r025-qubes-lineage-preservation.json" = "PMS_EMS_OMS_R025_FAIL_QUBES_LINEAGE_WEAKENED"
    "phase-pms-ems-oms-r025-operator-decision-lineage-preservation.json" = "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r025-ledger-commit-lineage-preservation.json" = "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r025-ledger-preview-lineage-preservation.json" = "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r025-position-preview-lineage-preservation.json" = "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r025-simulation-result-lineage-preservation.json" = "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r025-plan-lineage-preservation.json" = "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r025-paper-candidate-lineage-preservation.json" = "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r025-rebalance-intent-lineage-preservation.json" = "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r025-lot-sizing-lineage-preservation.json" = "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r025-instrument-universe-handling.json" = "PMS_EMS_OMS_R025_FAIL_AUDUSD_MISCLASSIFIED"
    "phase-pms-ems-oms-r025-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R025_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r025-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R025_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r025-no-external-audit.json" = "PMS_EMS_OMS_R025_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r025-forbidden-actions-audit.json" = "PMS_EMS_OMS_R025_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r025-next-phase-recommendation.json" = "PMS_EMS_OMS_R025_FAIL_NEXT_CYCLE_CONTINUITY_GATE_MISSING"
    "phase-pms-ems-oms-r025-build-test-validator-evidence.json" = "PMS_EMS_OMS_R025_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $entry.Key))) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$contract = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-paper-ledger-state-archive-contract.json") "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING"
$archive = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-paper-ledger-state-archive.json") "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING"
$report = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-paper-ledger-state-report.json") "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_REPORT_MISSING"
$lines = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-paper-ledger-state-lines.json") "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING"
$baseline = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-next-cycle-baseline-reference.json") "PMS_EMS_OMS_R025_FAIL_NEXT_CYCLE_CONTINUITY_GATE_MISSING"
$gate = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-next-cycle-continuity-gate.json") "PMS_EMS_OMS_R025_FAIL_NEXT_CYCLE_CONTINUITY_GATE_MISSING"
$productionLedgerAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-no-production-ledger-mutation-audit.json") "PMS_EMS_OMS_R025_FAIL_PRODUCTION_LEDGER_MUTATION"
$livePositionAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-no-live-position-mutation-audit.json") "PMS_EMS_OMS_R025_FAIL_LIVE_POSITION_MUTATION"
$brokerPositionAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-no-broker-position-mutation-audit.json") "PMS_EMS_OMS_R025_FAIL_BROKER_POSITION_MUTATION"
$tradingAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-no-trading-state-mutation-audit.json") "PMS_EMS_OMS_R025_FAIL_TRADING_STATE_MUTATION"
$fillAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-no-fill-no-execution-report-audit.json") "PMS_EMS_OMS_R025_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$orderAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-no-order-created-audit.json") "PMS_EMS_OMS_R025_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$routeAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-no-route-no-submission-audit.json") "PMS_EMS_OMS_R025_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED"
$idempotency = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-idempotency-evidence.json") "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING"
$risk = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-risk-lineage-preservation.json") "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING"
$qubes = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-qubes-lineage-preservation.json") "PMS_EMS_OMS_R025_FAIL_QUBES_LINEAGE_WEAKENED"
$operatorLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-operator-decision-lineage-preservation.json") "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING"
$ledgerCommitLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-ledger-commit-lineage-preservation.json") "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING"
$ledgerPreviewLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-ledger-preview-lineage-preservation.json") "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING"
$positionPreviewLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-position-preview-lineage-preservation.json") "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING"
$simulationLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-simulation-result-lineage-preservation.json") "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING"
$planLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-plan-lineage-preservation.json") "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING"
$candidateLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-paper-candidate-lineage-preservation.json") "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING"
$rebalanceLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-rebalance-intent-lineage-preservation.json") "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING"
$lotSizingLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-lot-sizing-lineage-preservation.json") "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-instrument-universe-handling.json") "PMS_EMS_OMS_R025_FAIL_AUDUSD_MISCLASSIFIED"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R025_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R025_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-no-external-audit.json") "PMS_EMS_OMS_R025_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-forbidden-actions-audit.json") "PMS_EMS_OMS_R025_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$nextPhase = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-next-phase-recommendation.json") "PMS_EMS_OMS_R025_FAIL_NEXT_CYCLE_CONTINUITY_GATE_MISSING"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r025-build-test-validator-evidence.json") "PMS_EMS_OMS_R025_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$contract.paperLedgerStateArchiveContractCreated) "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING" "Archive contract missing."
foreach ($value in @("ArchivedNoExternal", "ArchivedPaperFixtureState", "DuplicateReturned", "RejectedInvalidState", "InconclusiveSafe")) {
    Require-ArrayContains $contract.archiveStatuses $value "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING" "Archive status missing: $value"
}
foreach ($property in @("requiresPaperLedgerFixtureState", "requiresNoProductionLedgerMutation", "requiresNoLivePositionMutation", "requiresNoBrokerPositionMutation", "requiresNoTradingStateMutation", "requiresNoOrderFillReportRouteSubmission", "requiresNoNewCycle", "requiresNoNewQubesBatch", "requiresNoPaperLedgerMutationAgain")) {
    Require-True ([bool]$contract.$property) "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING" "Contract flag missing: $property"
}

Require-True ([bool]$archive.paperLedgerStateArchiveCreated) "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING" "Archive missing."
Require-True ([string]$archive.archiveStatus -eq "ArchivedPaperFixtureState") "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING" "Archive status wrong."
Require-True ([string]$archive.stateStatus -eq "PaperLedgerCommittedNoExternal") "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING" "State status wrong."
Require-True ([string]$archive.safetyStatus -eq "PaperLedgerFixtureStateOnly") "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING" "Safety status wrong."
Require-True ([int]$archive.lineCount -eq 3) "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING" "Line count wrong."
foreach ($property in @("paperOnly", "noExternal", "fixtureState", "notProductionLedger", "notBrokerPosition", "notTradingState", "noProductionLedgerMutation", "noLivePositionMutation", "noBrokerPositionMutation", "noTradingStateMutation", "noFillCreated", "noExecutionReportCreated", "noOrderCreated", "noBrokerRoute", "notSubmitted")) {
    Require-True ([bool]$archive.$property) "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING" "Archive safety flag missing: $property"
}
foreach ($property in @("paperLedgerMutatedAgain", "newCycleRan", "newQubesBatchIngested", "livePositionStateMutated", "brokerPositionStateMutated", "productionLedgerStateMutated", "tradingStateMutated", "fillCreated", "executionReportCreated", "omsOrderCreated", "parentOrderCreated", "childOrderCreated", "brokerOrderCreated", "orderStateCreated", "submittedOrders", "brokerRouteCreated")) {
    Require-False ([bool]$archive.$property) "PMS_EMS_OMS_R025_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Archive unsafe flag detected: $property"
}

Require-True ([bool]$lines.paperLedgerStateLinesArchived) "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING" "State lines missing."
Require-True ([int]$lines.lineCount -eq 3) "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING" "State line count wrong."
Require-True (@($lines.lines | Where-Object { $_.currencyOrSymbol -eq "AUDUSD" -and [decimal]$_.endingPaperQuantity -eq 131000 -and $_.quantityCurrency -eq "AUD" }).Count -eq 1) "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING" "AUDUSD archive line missing."
Require-True (@($lines.lines | Where-Object { $_.currencyOrSymbol -eq "EURUSD" -and [decimal]$_.endingPaperQuantity -eq 124000 -and $_.quantityCurrency -eq "EUR" }).Count -eq 1) "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING" "EURUSD archive line missing."
Require-True (@($lines.lines | Where-Object { $_.currencyOrSymbol -eq "GBPUSD" -and [decimal]$_.endingPaperQuantity -eq -368000 -and $_.quantityCurrency -eq "GBP" }).Count -eq 1) "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING" "GBPUSD archive line missing."

Require-True ([bool]$report.paperLedgerStateReportCreated) "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_REPORT_MISSING" "Report missing."
foreach ($property in @("includesPaperFixtureStateDisclaimer", "includesNoProductionLedgerMutationDisclaimer", "includesNoLivePositionMutationDisclaimer", "includesNoBrokerPositionMutationDisclaimer", "includesNoTradingStateMutationDisclaimer", "includesNoOrderDisclaimer", "includesNoFillDisclaimer", "includesNoExecutionReportDisclaimer", "includesNoBrokerRouteDisclaimer", "includesNoSubmissionDisclaimer")) {
    Require-True ([bool]$report.$property) "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_REPORT_MISSING" "Report disclaimer missing: $property"
}
$reportMarkdown = Get-Content -LiteralPath (Join-Path $artifactRoot "phase-pms-ems-oms-r025-paper-ledger-state-report.md") -Raw
foreach ($phrase in @("Paper ledger fixture state only", "no production ledger mutation", "no live position mutation", "no broker position mutation", "no trading-state mutation", "no orders", "no fills", "no execution reports", "no broker routes", "no submissions")) {
    if ($reportMarkdown -notmatch [regex]::Escape($phrase)) {
        Fail-Gate "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_REPORT_MISSING" "Markdown report disclaimer missing: $phrase"
    }
}

Require-True ([bool]$baseline.nextCycleBaselineReferenceCreated) "PMS_EMS_OMS_R025_FAIL_NEXT_CYCLE_CONTINUITY_GATE_MISSING" "Baseline reference missing."
Require-True ([string]$baseline.nextCycleBaselineType -eq "PaperLedgerFixture") "PMS_EMS_OMS_R025_FAIL_NEXT_CYCLE_CONTINUITY_GATE_MISSING" "Baseline type wrong."
Require-True ([string]$baseline.baselineSource -eq "R024 committed paper ledger state") "PMS_EMS_OMS_R025_FAIL_NEXT_CYCLE_CONTINUITY_GATE_MISSING" "Baseline source wrong."
Require-False ([bool]$baseline.baselineIsProduction) "PMS_EMS_OMS_R025_FAIL_PRODUCTION_LEDGER_MUTATION" "Baseline is production."
Require-False ([bool]$baseline.baselineIsBroker) "PMS_EMS_OMS_R025_FAIL_BROKER_POSITION_MUTATION" "Baseline is broker."
Require-False ([bool]$baseline.baselineIsLiveTrading) "PMS_EMS_OMS_R025_FAIL_TRADING_STATE_MUTATION" "Baseline is live trading."
Require-False ([bool]$baseline.newCycleRan) "PMS_EMS_OMS_R025_FAIL_NEW_CYCLE_RAN" "Baseline ran new cycle."
Require-False ([bool]$baseline.newQubesBatchIngested) "PMS_EMS_OMS_R025_FAIL_NEW_QUBES_BATCH_INGESTED" "Baseline ingested new Qubes batch."
Require-False ([bool]$baseline.paperLedgerMutatedAgain) "PMS_EMS_OMS_R025_FAIL_PAPER_LEDGER_MUTATED_AGAIN" "Baseline mutated paper ledger again."
Require-True ([bool]$gate.nextCycleContinuityGateCreated) "PMS_EMS_OMS_R025_FAIL_NEXT_CYCLE_CONTINUITY_GATE_MISSING" "Continuity gate missing."
Require-True ([bool]$gate.nextNoExternalCycleMayUsePaperLedgerFixtureAsCurrentState) "PMS_EMS_OMS_R025_FAIL_NEXT_CYCLE_CONTINUITY_GATE_MISSING" "Continuity gate does not permit paper fixture baseline."
Require-False ([bool]$gate.newCycleRan) "PMS_EMS_OMS_R025_FAIL_NEW_CYCLE_RAN" "Continuity gate ran new cycle."
Require-False ([bool]$gate.newQubesBatchIngested) "PMS_EMS_OMS_R025_FAIL_NEW_QUBES_BATCH_INGESTED" "Continuity gate ingested new Qubes batch."
Require-False ([bool]$gate.paperLedgerMutatedAgain) "PMS_EMS_OMS_R025_FAIL_PAPER_LEDGER_MUTATED_AGAIN" "Continuity gate mutated paper ledger again."
Require-True ([bool]$gate.noExternal) "PMS_EMS_OMS_R025_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Continuity gate not no-external."
Require-True ([bool]$gate.noOrderFillReportRouteOrSubmission) "PMS_EMS_OMS_R025_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Continuity gate allows order/fill/report/route/submission."

Require-True ([bool]$productionLedgerAudit.noProductionLedgerMutationAuditCreated) "PMS_EMS_OMS_R025_FAIL_PRODUCTION_LEDGER_MUTATION" "Production-ledger audit missing."
Require-False ([bool]$productionLedgerAudit.productionLedgerStateMutated) "PMS_EMS_OMS_R025_FAIL_PRODUCTION_LEDGER_MUTATION" "Production ledger mutated."
Require-False ([bool]$productionLedgerAudit.persistedProductionLedgerStateMutated) "PMS_EMS_OMS_R025_FAIL_PRODUCTION_LEDGER_MUTATION" "Persisted production ledger mutated."
Require-False ([bool]$productionLedgerAudit.paperLedgerStateArchiveMutatesProductionLedgerState) "PMS_EMS_OMS_R025_FAIL_PRODUCTION_LEDGER_MUTATION" "Archive mutates production ledger."
Require-True ([bool]$livePositionAudit.noLivePositionMutationAuditCreated) "PMS_EMS_OMS_R025_FAIL_LIVE_POSITION_MUTATION" "Live-position audit missing."
Require-False ([bool]$livePositionAudit.livePositionStateMutated) "PMS_EMS_OMS_R025_FAIL_LIVE_POSITION_MUTATION" "Live position mutated."
Require-False ([bool]$livePositionAudit.paperLedgerStateArchiveMutatesLivePositions) "PMS_EMS_OMS_R025_FAIL_LIVE_POSITION_MUTATION" "Archive mutates live positions."
Require-True ([bool]$brokerPositionAudit.noBrokerPositionMutationAuditCreated) "PMS_EMS_OMS_R025_FAIL_BROKER_POSITION_MUTATION" "Broker-position audit missing."
Require-False ([bool]$brokerPositionAudit.brokerPositionStateMutated) "PMS_EMS_OMS_R025_FAIL_BROKER_POSITION_MUTATION" "Broker position mutated."
Require-False ([bool]$brokerPositionAudit.paperLedgerStateArchiveMutatesBrokerPositions) "PMS_EMS_OMS_R025_FAIL_BROKER_POSITION_MUTATION" "Archive mutates broker positions."
Require-True ([bool]$tradingAudit.noTradingStateMutationAuditCreated) "PMS_EMS_OMS_R025_FAIL_TRADING_STATE_MUTATION" "Trading-state audit missing."
Require-False ([bool]$tradingAudit.tradingStateMutated) "PMS_EMS_OMS_R025_FAIL_TRADING_STATE_MUTATION" "Trading state mutated."
Require-False ([bool]$tradingAudit.paperLedgerStateArchiveMutatesTradingState) "PMS_EMS_OMS_R025_FAIL_TRADING_STATE_MUTATION" "Archive mutates trading state."

Require-True ([bool]$fillAudit.noFillNoExecutionReportAuditCreated) "PMS_EMS_OMS_R025_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill/report audit missing."
foreach ($property in @("fillCreated", "executionReportCreated", "simulationFillCreatedAsRealFill", "brokerExecutionReportCreated", "paperLedgerStateArchiveCreatesFillOrExecutionReport")) {
    Require-False ([bool]$fillAudit.$property) "PMS_EMS_OMS_R025_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill/report audit detected: $property"
}
Require-True ([bool]$orderAudit.noOrderCreatedAuditCreated) "PMS_EMS_OMS_R025_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No-order audit missing."
foreach ($property in @("omsOrderCreated", "parentOrderCreated", "childOrderCreated", "brokerOrderCreated", "executableOrderCreated", "orderStateCreated", "orderSubmissionPathIntroduced", "ordersSubmitted", "paperLedgerStateArchiveCreatesOrder")) {
    Require-False ([bool]$orderAudit.$property) "PMS_EMS_OMS_R025_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order audit detected: $property"
}
Require-True ([bool]$routeAudit.noRouteNoSubmissionAuditCreated) "PMS_EMS_OMS_R025_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "Route/submission audit missing."
foreach ($property in @("brokerRouteCreated", "brokerRouteAssigned", "submissionInstructionCreated", "orderSubmissionPathInvoked", "paperLedgerStateArchiveSubmitted", "paperLedgerStateArchiveRouteable")) {
    Require-False ([bool]$routeAudit.$property) "PMS_EMS_OMS_R025_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "Route/submission audit detected: $property"
}

Require-True ([bool]$idempotency.idempotencyEvidenceCreated) "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING" "Idempotency missing."
Require-True ([string]$idempotency.idempotencyKey -eq "PaperLedgerStateArchiveId") "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING" "Wrong idempotency key."
Require-True ([string]$idempotency.duplicateArchiveBehavior -eq "DuplicateReturned") "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING" "Duplicate behavior missing."
Require-False ([bool]$idempotency.duplicatesCreateAdditionalArchives) "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING" "Duplicate archives created."

Require-True ([bool]$risk.lineagePreserved) "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING" "Risk lineage missing."
Require-True ([bool]$qubes.qubesLineagePreservationCreated) "PMS_EMS_OMS_R025_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage missing."
Require-True ([string]$qubes.sourceSystem -eq "ModelWeightSourceSystem.Qubes") "PMS_EMS_OMS_R025_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes source missing."
Require-True ([int]$qubes.cadenceMinutes -eq 15) "PMS_EMS_OMS_R025_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes cadence missing."
Require-False ([bool]$qubes.qubesLineageWeakened) "PMS_EMS_OMS_R025_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage weakened."
foreach ($lineage in @($operatorLineage, $ledgerCommitLineage, $ledgerPreviewLineage, $positionPreviewLineage, $simulationLineage, $planLineage, $candidateLineage, $rebalanceLineage, $lotSizingLineage)) {
    Require-True ([bool]$lineage.lineagePreserved) "PMS_EMS_OMS_R025_FAIL_LEDGER_STATE_ARCHIVE_MISSING" "Lineage artifact not preserved."
}

Require-True ([bool]$universe.instrumentUniverseHandlingCreated) "PMS_EMS_OMS_R025_FAIL_AUDUSD_MISCLASSIFIED" "Instrument universe handling missing."
Require-False ([bool]$universe.audusdClassifiedAsFailed) "PMS_EMS_OMS_R025_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD classified as failed."
Require-False ([bool]$universe.audusdLiveValidationGapBlocksPaperLedgerStateArchive) "PMS_EMS_OMS_R025_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD gap blocks archive."
Require-False ([bool]$universe.usdjpyLiveValidationGapBlocksPaperLedgerStateArchive) "PMS_EMS_OMS_R025_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY gap blocks archive."
Require-True ([bool]$usdjpy.usdJpyCaveatPreservationCreated) "PMS_EMS_OMS_R025_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
Require-True ([string]$usdjpy.usdJpySecurityId -eq "4004") "PMS_EMS_OMS_R025_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID missing."
Require-True ([string]$usdjpy.usdJpySecurityIdSource -eq "8") "PMS_EMS_OMS_R025_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource missing."
Require-False ([bool]$usdjpy.usdJpyClassifiedAsFailed) "PMS_EMS_OMS_R025_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY classified as failed."
Require-False ([bool]$usdjpy.audusdClassifiedAsFailed) "PMS_EMS_OMS_R025_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD classified as failed."
Require-True ([bool]$lmax.lmaxReadonlyBaselineReferenceCreated) "PMS_EMS_OMS_R025_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX baseline missing."
Require-False ([bool]$lmax.lmaxUsedInThisPhase) "PMS_EMS_OMS_R025_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX used."
Require-False ([bool]$lmax.lmaxCalledInThisPhase) "PMS_EMS_OMS_R025_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX called."
Require-False ([bool]$lmax.lmaxLiveValidationGapsBlockPaperLedgerStateArchive) "PMS_EMS_OMS_R025_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX gaps block archive."

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
    "livePositionStateMutated",
    "brokerPositionStateMutated",
    "productionLedgerStateMutated",
    "tradingStateMutated",
    "paperLedgerStateMutatedAgain",
    "newCycleRan",
    "newQubesBatchIngested",
    "replayOrShadowReplayIntroduced",
    "secretsOrCredentialsSerialized",
    "rawFixSerialized",
    "rawEndpointTlsValuesSerialized",
    "sessionIdsSerialized",
    "compIdsSerialized",
    "rawMdReqIdSerialized",
    "rawBrokerMarketDataPayloadsSerialized",
    "rawBrokerMarketDataPricesSerialized",
    "rawMarketDataFixturePayloadsSerialized"
)) {
    Require-False ([bool]$noExternal.$property) "PMS_EMS_OMS_R025_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected: $property"
}

foreach ($item in $forbidden.forbiddenActions) {
    if ([bool]$item.detected) {
        if ([string]$item.action -match "scheduler|service|timer|background") { Fail-Gate "PMS_EMS_OMS_R025_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Forbidden action detected: $($item.action)" }
        if ([string]$item.action -match "new cycle") { Fail-Gate "PMS_EMS_OMS_R025_FAIL_NEW_CYCLE_RAN" "Forbidden action detected: $($item.action)" }
        if ([string]$item.action -match "Qubes") { Fail-Gate "PMS_EMS_OMS_R025_FAIL_NEW_QUBES_BATCH_INGESTED" "Forbidden action detected: $($item.action)" }
        if ([string]$item.action -match "paper ledger state mutated again") { Fail-Gate "PMS_EMS_OMS_R025_FAIL_PAPER_LEDGER_MUTATED_AGAIN" "Forbidden action detected: $($item.action)" }
        if ([string]$item.action -match "production ledger") { Fail-Gate "PMS_EMS_OMS_R025_FAIL_PRODUCTION_LEDGER_MUTATION" "Forbidden action detected: $($item.action)" }
        if ([string]$item.action -match "position state") {
            if ([string]$item.action -match "broker") { Fail-Gate "PMS_EMS_OMS_R025_FAIL_BROKER_POSITION_MUTATION" "Forbidden action detected: $($item.action)" }
            Fail-Gate "PMS_EMS_OMS_R025_FAIL_LIVE_POSITION_MUTATION" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "trading state") { Fail-Gate "PMS_EMS_OMS_R025_FAIL_TRADING_STATE_MUTATION" "Forbidden action detected: $($item.action)" }
        if ([string]$item.action -match "fill|execution report") { Fail-Gate "PMS_EMS_OMS_R025_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Forbidden action detected: $($item.action)" }
        if ([string]$item.action -match "order|trading|executable|OMS|parent|child|broker|submission|submitted|routed|route") { Fail-Gate "PMS_EMS_OMS_R025_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden action detected: $($item.action)" }
        Fail-Gate "PMS_EMS_OMS_R025_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $($item.action)"
    }
}

Require-True ([bool]$nextPhase.nextPhaseRecommendationCreated) "PMS_EMS_OMS_R025_FAIL_NEXT_CYCLE_CONTINUITY_GATE_MISSING" "Next phase missing."
Require-True ([string]$nextPhase.recommendedNextPhase -eq "PMS-EMS-OMS-R026") "PMS_EMS_OMS_R025_FAIL_NEXT_CYCLE_CONTINUITY_GATE_MISSING" "Next phase is not R026."
Require-True ([bool]$nextPhase.mustRemainNoExternal) "PMS_EMS_OMS_R025_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase not no-external."

$apiSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Api/appsettings.json") "PMS_EMS_OMS_R025_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$workerSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/appsettings.json") "PMS_EMS_OMS_R025_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-False ([bool]$apiSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R025_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API live trading enabled."
Require-False ([bool]$workerSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R025_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker live trading enabled."
Require-False ([bool]$apiSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R025_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API external enabled."
Require-False ([bool]$workerSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R025_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker external enabled."
Require-True ([bool]$apiSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R025_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API fake gateway not required."
Require-True ([bool]$workerSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R025_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker fake gateway not required."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.Enabled) "PMS_EMS_OMS_R025_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX runtime enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowExternalConnections) "PMS_EMS_OMS_R025_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX external enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowOrderSubmission) "PMS_EMS_OMS_R025_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "LMAX order submission enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SchedulerEnabled) "PMS_EMS_OMS_R025_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "LMAX scheduler enabled."
Require-False ([bool]$workerSettings.MarketDataBars.Enabled) "PMS_EMS_OMS_R025_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker market data bars enabled."

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-pms-ems-oms-r025-*" -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combined = [string]::Join("`n", $artifactText)
foreach ($pattern in @("\u0001", "35=", "MDReqID\s*[:=]", "SenderCompID\s*[:=]", "TargetCompID\s*[:=]", "BeginString\s*[:=]", "SocketHost\s*[:=]", "TlsHost\s*[:=]", "Password\s*[:=]", "ApiKey\s*[:=]", "Bearer\s+[A-Za-z0-9_\.-]+", "rawBid", "rawAsk", "rawMid")) {
    if ($combined -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R025_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Unsafe serialized content pattern found: $pattern"
    }
}

$requiredFiles = @(
    "src/QQ.Production.Intraday.Application/QubesPaperLedgerStateArchive.cs",
    "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperLedgerStateArchiveTests.cs"
)
foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $file))) {
        Fail-Gate "PMS_EMS_OMS_R025_FAIL_BUILD_OR_TESTS" "Required file missing: $file"
    }
}

$source = Get-Content -LiteralPath (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/QubesPaperLedgerStateArchive.cs") -Raw
foreach ($pattern in @("TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "SendOrderAsync", "SubmitOrder", "FixSession", "CreateOrderAsync", "RunOneCycleAsync", "ParseNormalizeAndMap")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R025_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "R025 source contains forbidden runtime or cycle/ingest pattern: $pattern"
    }
}
foreach ($pattern in @("AddHostedService", "IHostedService", "BackgroundService", "PeriodicTimer", "Task.Delay", "System.Threading.Timer")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R025_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "R025 source contains scheduler/service pattern: $pattern"
    }
}

$tests = Get-Content -LiteralPath (Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperLedgerStateArchiveTests.cs") -Raw
foreach ($requiredTestName in @(
    "R024_paper_ledger_state_can_be_archived_no_externally",
    "Archive_preserves_paper_ledger_state_id",
    "Archive_preserves_paper_ledger_commit_id",
    "Archive_preserves_cycle_run_id_and_qubes_run_id",
    "Archive_preserves_operator_decision_id",
    "Archive_lines_include_audusd",
    "Archive_lines_include_eurusd",
    "Archive_lines_include_gbpusd",
    "Operator_report_includes_paper_fixture_state_disclaimer",
    "Operator_report_includes_no_production_ledger_mutation_disclaimer",
    "Operator_report_includes_no_live_position_mutation_disclaimer",
    "Operator_report_includes_no_broker_position_mutation_disclaimer",
    "Operator_report_includes_no_trading_state_mutation_disclaimer",
    "Operator_report_includes_no_order_fill_execution_report_route_or_submission_disclaimer",
    "Next_cycle_continuity_baseline_references_paper_ledger_fixture_state",
    "Next_cycle_continuity_does_not_run_a_new_cycle",
    "Next_cycle_continuity_does_not_ingest_a_new_qubes_batch",
    "Next_cycle_continuity_does_not_mutate_paper_ledger_again",
    "Duplicate_archive_handling_is_idempotent",
    "No_live_broker_production_or_trading_state_is_mutated",
    "No_fills_execution_reports_or_orders_are_created",
    "Archive_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action",
    "Api_and_worker_live_gateway_remain_disabled",
    "Archive_source_introduces_no_scheduler_timer_polling_or_background_job",
    "Audusd_is_not_misclassified_as_failed",
    "Usdjpy_caveat_remains_preserved"
)) {
    if ($tests -notmatch [regex]::Escape($requiredTestName)) {
        Fail-Gate "PMS_EMS_OMS_R025_FAIL_BUILD_OR_TESTS" "Focused test missing: $requiredTestName"
    }
}

Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R025_FAIL_BUILD_OR_TESTS" "Evidence marker missing."
Require-True ([string]$evidence.build.status -eq "PASS") "PMS_EMS_OMS_R025_FAIL_BUILD_OR_TESTS" "Build evidence missing or not PASS."
Require-True ([int]$evidence.build.failed -eq 0) "PMS_EMS_OMS_R025_FAIL_BUILD_OR_TESTS" "Build evidence has failures."
Require-True ([string]$evidence.focusedTests.status -eq "PASS") "PMS_EMS_OMS_R025_FAIL_BUILD_OR_TESTS" "Focused test evidence missing or not PASS."
Require-True ([int]$evidence.focusedTests.failed -eq 0) "PMS_EMS_OMS_R025_FAIL_BUILD_OR_TESTS" "Focused tests have failures."
Require-True ([string]$evidence.unitTests.status -eq "PASS") "PMS_EMS_OMS_R025_FAIL_BUILD_OR_TESTS" "Unit test evidence missing or not PASS."
Require-True ([int]$evidence.unitTests.failed -eq 0) "PMS_EMS_OMS_R025_FAIL_BUILD_OR_TESTS" "Unit tests have failures."
Require-True ([string]$evidence.validator.status -eq "PASS") "PMS_EMS_OMS_R025_FAIL_BUILD_OR_TESTS" "Validator evidence missing or not PASS."
Require-True ([int]$evidence.validator.failed -eq 0) "PMS_EMS_OMS_R025_FAIL_BUILD_OR_TESTS" "Validator evidence has failures."

Write-Host "PMS_EMS_OMS_R025_PASS_PAPER_LEDGER_STATE_ARCHIVE_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R025_PASS_OPERATOR_LEDGER_STATE_REPORT_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R025_PASS_NEXT_CYCLE_CONTINUITY_GATE_READY_NO_EXTERNAL"
