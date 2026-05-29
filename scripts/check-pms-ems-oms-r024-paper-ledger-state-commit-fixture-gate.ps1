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

function Require-Zero {
    param([int]$Value, [string]$Classification, [string]$Message)
    if ($Value -ne 0) { Fail-Gate $Classification $Message }
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
    "phase-pms-ems-oms-r024-summary.md" = "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
    "phase-pms-ems-oms-r024-paper-ledger-commit-contract.json" = "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
    "phase-pms-ems-oms-r024-paper-ledger-commit.json" = "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
    "phase-pms-ems-oms-r024-paper-ledger-state.json" = "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
    "phase-pms-ems-oms-r024-paper-ledger-state-lines.json" = "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
    "phase-pms-ems-oms-r024-paper-ledger-commit-summary.json" = "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
    "phase-pms-ems-oms-r024-no-live-position-mutation-audit.json" = "PMS_EMS_OMS_R024_FAIL_LIVE_POSITION_MUTATION"
    "phase-pms-ems-oms-r024-no-broker-position-mutation-audit.json" = "PMS_EMS_OMS_R024_FAIL_BROKER_POSITION_MUTATION"
    "phase-pms-ems-oms-r024-no-production-ledger-mutation-audit.json" = "PMS_EMS_OMS_R024_FAIL_PRODUCTION_LEDGER_MUTATION"
    "phase-pms-ems-oms-r024-no-trading-state-mutation-audit.json" = "PMS_EMS_OMS_R024_FAIL_TRADING_STATE_MUTATION"
    "phase-pms-ems-oms-r024-no-fill-no-execution-report-audit.json" = "PMS_EMS_OMS_R024_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
    "phase-pms-ems-oms-r024-no-order-created-audit.json" = "PMS_EMS_OMS_R024_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r024-no-route-no-submission-audit.json" = "PMS_EMS_OMS_R024_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED"
    "phase-pms-ems-oms-r024-idempotency-evidence.json" = "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
    "phase-pms-ems-oms-r024-risk-lineage-preservation.json" = "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
    "phase-pms-ems-oms-r024-qubes-lineage-preservation.json" = "PMS_EMS_OMS_R024_FAIL_QUBES_LINEAGE_WEAKENED"
    "phase-pms-ems-oms-r024-operator-decision-lineage-preservation.json" = "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
    "phase-pms-ems-oms-r024-ledger-preview-lineage-preservation.json" = "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
    "phase-pms-ems-oms-r024-position-preview-lineage-preservation.json" = "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
    "phase-pms-ems-oms-r024-simulation-result-lineage-preservation.json" = "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
    "phase-pms-ems-oms-r024-plan-lineage-preservation.json" = "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
    "phase-pms-ems-oms-r024-paper-candidate-lineage-preservation.json" = "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
    "phase-pms-ems-oms-r024-rebalance-intent-lineage-preservation.json" = "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
    "phase-pms-ems-oms-r024-lot-sizing-lineage-preservation.json" = "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
    "phase-pms-ems-oms-r024-instrument-universe-handling.json" = "PMS_EMS_OMS_R024_FAIL_AUDUSD_MISCLASSIFIED"
    "phase-pms-ems-oms-r024-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R024_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r024-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R024_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r024-no-external-audit.json" = "PMS_EMS_OMS_R024_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r024-forbidden-actions-audit.json" = "PMS_EMS_OMS_R024_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r024-next-phase-recommendation.json" = "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
    "phase-pms-ems-oms-r024-build-test-validator-evidence.json" = "PMS_EMS_OMS_R024_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $entry.Key))) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$contract = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r024-paper-ledger-commit-contract.json") "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
$commit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r024-paper-ledger-commit.json") "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
$state = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r024-paper-ledger-state.json") "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
$lines = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r024-paper-ledger-state-lines.json") "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
$summary = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r024-paper-ledger-commit-summary.json") "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
$livePositionAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r024-no-live-position-mutation-audit.json") "PMS_EMS_OMS_R024_FAIL_LIVE_POSITION_MUTATION"
$brokerPositionAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r024-no-broker-position-mutation-audit.json") "PMS_EMS_OMS_R024_FAIL_BROKER_POSITION_MUTATION"
$productionLedgerAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r024-no-production-ledger-mutation-audit.json") "PMS_EMS_OMS_R024_FAIL_PRODUCTION_LEDGER_MUTATION"
$tradingAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r024-no-trading-state-mutation-audit.json") "PMS_EMS_OMS_R024_FAIL_TRADING_STATE_MUTATION"
$fillAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r024-no-fill-no-execution-report-audit.json") "PMS_EMS_OMS_R024_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$orderAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r024-no-order-created-audit.json") "PMS_EMS_OMS_R024_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$routeAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r024-no-route-no-submission-audit.json") "PMS_EMS_OMS_R024_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED"
$idempotency = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r024-idempotency-evidence.json") "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
$risk = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r024-risk-lineage-preservation.json") "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
$qubes = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r024-qubes-lineage-preservation.json") "PMS_EMS_OMS_R024_FAIL_QUBES_LINEAGE_WEAKENED"
$operatorLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r024-operator-decision-lineage-preservation.json") "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
$ledgerPreviewLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r024-ledger-preview-lineage-preservation.json") "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
$positionPreviewLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r024-position-preview-lineage-preservation.json") "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
$simulationLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r024-simulation-result-lineage-preservation.json") "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
$planLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r024-plan-lineage-preservation.json") "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
$candidateLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r024-paper-candidate-lineage-preservation.json") "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
$rebalanceLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r024-rebalance-intent-lineage-preservation.json") "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
$lotSizingLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r024-lot-sizing-lineage-preservation.json") "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r024-instrument-universe-handling.json") "PMS_EMS_OMS_R024_FAIL_AUDUSD_MISCLASSIFIED"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r024-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R024_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r024-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R024_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r024-no-external-audit.json") "PMS_EMS_OMS_R024_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r024-forbidden-actions-audit.json") "PMS_EMS_OMS_R024_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$nextPhase = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r024-next-phase-recommendation.json") "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r024-build-test-validator-evidence.json") "PMS_EMS_OMS_R024_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$contract.paperLedgerCommitContractCreated) "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "Commit contract missing."
foreach ($value in @("PaperLedgerCommittedNoExternal", "DuplicateReturned", "RejectedInvalidPreview", "RejectedMissingApproval", "InconclusiveSafe")) {
    Require-ArrayContains $contract.commitStatuses $value "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "Commit status missing: $value"
}
foreach ($property in @("requiresPaperLedgerCommitReadyNoExternal", "requiresPaperOnly", "requiresNoExternal", "requiresFixtureState", "requiresNotProductionLedger", "requiresNotBrokerPosition", "requiresNotTradingState", "requiresNoLivePositionMutation", "requiresNoBrokerPositionMutation", "requiresNoProductionLedgerMutation", "requiresNoTradingStateMutation", "requiresNoOrderFillReportRouteSubmission")) {
    Require-True ([bool]$contract.$property) "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "Contract safety flag missing: $property"
}

Require-True ([bool]$commit.paperLedgerCommitCreated) "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "Commit artifact missing."
Require-True ([string]$commit.requiredApprovalStatus -eq "PaperLedgerCommitReadyNoExternal") "PMS_EMS_OMS_R024_FAIL_COMMIT_WITHOUT_APPROVAL" "Commit approval status missing."
Require-True ([string]$commit.commitStatus -eq "PaperLedgerCommittedNoExternal") "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "Commit status wrong."
Require-True ([int]$commit.appliedLineCount -eq 3) "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "Applied line count wrong."
Require-True ([int]$commit.blockedLineCount -eq 10) "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "Blocked line count wrong."
foreach ($property in @("paperOnly", "noExternal", "fixtureState", "notProductionLedger", "notBrokerPosition", "notTradingState", "noLivePositionMutation", "noBrokerPositionMutation", "noProductionLedgerMutation", "noTradingStateMutation", "noFillCreated", "noExecutionReportCreated", "noOrderCreated", "noBrokerRoute", "notSubmitted")) {
    Require-True ([bool]$commit.$property) "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "Commit safety flag missing: $property"
}
foreach ($property in @("livePositionMutationCount", "brokerPositionMutationCount", "productionLedgerMutationCount", "tradingStateMutationCount", "orderCount", "fillCount", "executionReportCount", "brokerRouteCount")) {
    Require-Zero ([int]$commit.$property) "PMS_EMS_OMS_R024_FAIL_TRADING_STATE_MUTATION" "Commit count nonzero: $property"
}

Require-True ([bool]$state.paperLedgerStateCreated) "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "Paper ledger state missing."
Require-True ([string]$state.commitStatus -eq "PaperLedgerCommittedNoExternal") "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "State commit status wrong."
Require-True ([string]$state.safetyStatus -eq "PaperLedgerFixtureStateOnly") "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "State safety status wrong."
Require-True ([int]$state.lineCount -eq 3) "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "State line count wrong."
foreach ($property in @("paperOnly", "noExternal", "fixtureState", "notProductionLedger", "notBrokerPosition", "notTradingState", "noLivePositionMutation", "noBrokerPositionMutation", "noProductionLedgerMutation", "noTradingStateMutation", "noFillCreated", "noExecutionReportCreated", "noOrderCreated", "noBrokerRoute", "notSubmitted")) {
    Require-True ([bool]$state.$property) "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "State safety flag missing: $property"
}
foreach ($property in @("livePositionStateMutated", "brokerPositionStateMutated", "productionLedgerStateMutated", "tradingStateMutated", "fillCreated", "executionReportCreated", "omsOrderCreated", "parentOrderCreated", "childOrderCreated", "brokerOrderCreated", "orderStateCreated", "submittedOrders", "brokerRouteCreated")) {
    Require-False ([bool]$state.$property) "PMS_EMS_OMS_R024_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "State unsafe flag detected: $property"
}

Require-True ([bool]$lines.paperLedgerStateLinesCreated) "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "State lines missing."
Require-True ([int]$lines.lineCount -eq 3) "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "State line count wrong."
Require-True (@($lines.lines | Where-Object { $_.currencyOrSymbol -eq "AUDUSD" -and [decimal]$_.startingPaperQuantity -eq 0 -and [decimal]$_.appliedPaperDeltaQuantity -eq 131000 -and [decimal]$_.endingPaperQuantity -eq 131000 -and $_.quantityCurrency -eq "AUD" }).Count -eq 1) "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "AUDUSD state line missing."
Require-True (@($lines.lines | Where-Object { $_.currencyOrSymbol -eq "EURUSD" -and [decimal]$_.startingPaperQuantity -eq 0 -and [decimal]$_.appliedPaperDeltaQuantity -eq 124000 -and [decimal]$_.endingPaperQuantity -eq 124000 -and $_.quantityCurrency -eq "EUR" }).Count -eq 1) "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "EURUSD state line missing."
Require-True (@($lines.lines | Where-Object { $_.currencyOrSymbol -eq "GBPUSD" -and [decimal]$_.startingPaperQuantity -eq 0 -and [decimal]$_.appliedPaperDeltaQuantity -eq -368000 -and [decimal]$_.endingPaperQuantity -eq -368000 -and $_.quantityCurrency -eq "GBP" }).Count -eq 1) "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "GBPUSD state line missing."
foreach ($line in $lines.lines) {
    foreach ($property in @("paperOnly", "noExternal", "fixtureState", "notProductionLedger", "notBrokerPosition", "notTradingState", "noLivePositionMutation", "noBrokerPositionMutation", "noProductionLedgerMutation", "noTradingStateMutation", "noFillCreated", "noExecutionReportCreated", "noOrderCreated", "noBrokerRoute", "notSubmitted")) {
        Require-True ([bool]$line.$property) "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "Line safety flag missing: $property"
    }
}

Require-True ([bool]$summary.paperLedgerCommitSummaryCreated) "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "Commit summary missing."
Require-True ([int]$summary.appliedLineCount -eq 3) "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "Summary applied line count wrong."
Require-True ([int]$summary.blockedLineCount -eq 10) "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "Summary blocked line count wrong."
foreach ($property in @("livePositionMutationCount", "brokerPositionMutationCount", "productionLedgerMutationCount", "tradingStateMutationCount", "orderCount", "fillCount", "executionReportCount", "brokerRouteCount")) {
    Require-Zero ([int]$summary.$property) "PMS_EMS_OMS_R024_FAIL_TRADING_STATE_MUTATION" "Summary count nonzero: $property"
}

Require-True ([bool]$livePositionAudit.noLivePositionMutationAuditCreated) "PMS_EMS_OMS_R024_FAIL_LIVE_POSITION_MUTATION" "No-live-position audit missing."
Require-Zero ([int]$livePositionAudit.livePositionMutationCount) "PMS_EMS_OMS_R024_FAIL_LIVE_POSITION_MUTATION" "Live position mutation count nonzero."
Require-False ([bool]$livePositionAudit.livePositionStateMutated) "PMS_EMS_OMS_R024_FAIL_LIVE_POSITION_MUTATION" "Live position mutated."
Require-False ([bool]$livePositionAudit.paperLedgerCommitMutatesLivePositions) "PMS_EMS_OMS_R024_FAIL_LIVE_POSITION_MUTATION" "Paper commit mutates live positions."
Require-True ([bool]$brokerPositionAudit.noBrokerPositionMutationAuditCreated) "PMS_EMS_OMS_R024_FAIL_BROKER_POSITION_MUTATION" "No-broker-position audit missing."
Require-Zero ([int]$brokerPositionAudit.brokerPositionMutationCount) "PMS_EMS_OMS_R024_FAIL_BROKER_POSITION_MUTATION" "Broker position mutation count nonzero."
Require-False ([bool]$brokerPositionAudit.brokerPositionStateMutated) "PMS_EMS_OMS_R024_FAIL_BROKER_POSITION_MUTATION" "Broker position mutated."
Require-False ([bool]$brokerPositionAudit.paperLedgerCommitMutatesBrokerPositions) "PMS_EMS_OMS_R024_FAIL_BROKER_POSITION_MUTATION" "Paper commit mutates broker positions."
Require-True ([bool]$productionLedgerAudit.noProductionLedgerMutationAuditCreated) "PMS_EMS_OMS_R024_FAIL_PRODUCTION_LEDGER_MUTATION" "No-production-ledger audit missing."
Require-Zero ([int]$productionLedgerAudit.productionLedgerMutationCount) "PMS_EMS_OMS_R024_FAIL_PRODUCTION_LEDGER_MUTATION" "Production ledger mutation count nonzero."
Require-False ([bool]$productionLedgerAudit.productionLedgerStateMutated) "PMS_EMS_OMS_R024_FAIL_PRODUCTION_LEDGER_MUTATION" "Production ledger mutated."
Require-False ([bool]$productionLedgerAudit.persistedProductionLedgerStateMutated) "PMS_EMS_OMS_R024_FAIL_PRODUCTION_LEDGER_MUTATION" "Persisted production ledger mutated."
Require-False ([bool]$productionLedgerAudit.paperLedgerCommitMutatesProductionLedgerState) "PMS_EMS_OMS_R024_FAIL_PRODUCTION_LEDGER_MUTATION" "Paper commit mutates production ledger."
Require-False ([bool]$productionLedgerAudit.paperLedgerCommitIsProductionLedgerCommit) "PMS_EMS_OMS_R024_FAIL_PRODUCTION_LEDGER_MUTATION" "Paper commit is production ledger commit."
Require-True ([bool]$tradingAudit.noTradingStateMutationAuditCreated) "PMS_EMS_OMS_R024_FAIL_TRADING_STATE_MUTATION" "No-trading-state audit missing."
Require-Zero ([int]$tradingAudit.tradingStateMutationCount) "PMS_EMS_OMS_R024_FAIL_TRADING_STATE_MUTATION" "Trading mutation count nonzero."
Require-False ([bool]$tradingAudit.tradingStateMutated) "PMS_EMS_OMS_R024_FAIL_TRADING_STATE_MUTATION" "Trading state mutated."
Require-False ([bool]$tradingAudit.paperLedgerCommitMutatesTradingState) "PMS_EMS_OMS_R024_FAIL_TRADING_STATE_MUTATION" "Paper commit mutates trading state."

Require-True ([bool]$fillAudit.noFillNoExecutionReportAuditCreated) "PMS_EMS_OMS_R024_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill/report audit missing."
foreach ($property in @("fillCreated", "executionReportCreated", "simulationFillCreatedAsRealFill", "brokerExecutionReportCreated", "paperLedgerCommitCreatesFillOrExecutionReport")) {
    Require-False ([bool]$fillAudit.$property) "PMS_EMS_OMS_R024_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill/report audit detected: $property"
}
Require-True ([bool]$orderAudit.noOrderCreatedAuditCreated) "PMS_EMS_OMS_R024_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No-order audit missing."
foreach ($property in @("omsOrderCreated", "parentOrderCreated", "childOrderCreated", "brokerOrderCreated", "executableOrderCreated", "orderStateCreated", "orderSubmissionPathIntroduced", "ordersSubmitted", "paperLedgerCommitCreatesOrder")) {
    Require-False ([bool]$orderAudit.$property) "PMS_EMS_OMS_R024_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order audit detected: $property"
}
Require-True ([bool]$routeAudit.noRouteNoSubmissionAuditCreated) "PMS_EMS_OMS_R024_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "No-route/no-submission audit missing."
foreach ($property in @("brokerRouteCreated", "brokerRouteAssigned", "submissionInstructionCreated", "orderSubmissionPathInvoked", "paperLedgerCommitSubmitted", "paperLedgerCommitRouteable")) {
    Require-False ([bool]$routeAudit.$property) "PMS_EMS_OMS_R024_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "Route/submission audit detected: $property"
}

Require-True ([bool]$idempotency.idempotencyEvidenceCreated) "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "Idempotency missing."
Require-True ([string]$idempotency.idempotencyKey -eq "PaperLedgerCommitId") "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "Wrong idempotency key."
Require-True ([string]$idempotency.duplicateCommitBehavior -eq "DuplicateReturned") "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "Duplicate behavior missing."
Require-False ([bool]$idempotency.duplicatesCreateAdditionalPaperLedgerStates) "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "Duplicate states created."

Require-True ([bool]$risk.riskLineagePreservationCreated) "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "Risk lineage missing."
Require-False ([bool]$risk.riskLineageMissing) "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "Risk lineage marked missing."
Require-True ([bool]$qubes.qubesLineagePreservationCreated) "PMS_EMS_OMS_R024_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage missing."
Require-True ([string]$qubes.sourceSystem -eq "ModelWeightSourceSystem.Qubes") "PMS_EMS_OMS_R024_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes source missing."
Require-True ([int]$qubes.cadenceMinutes -eq 15) "PMS_EMS_OMS_R024_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes cadence missing."
Require-False ([bool]$qubes.qubesLineageWeakened) "PMS_EMS_OMS_R024_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage weakened."
foreach ($lineage in @($operatorLineage, $ledgerPreviewLineage, $positionPreviewLineage, $simulationLineage, $planLineage, $candidateLineage, $rebalanceLineage, $lotSizingLineage)) {
    Require-True ([bool]$lineage.lineagePreserved) "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "Lineage not preserved."
}
Require-False ([bool]$rebalanceLineage.rebalanceintentLineageMissing) "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "Rebalance lineage missing."

Require-True ([bool]$universe.instrumentUniverseHandlingCreated) "PMS_EMS_OMS_R024_FAIL_AUDUSD_MISCLASSIFIED" "Instrument universe handling missing."
Require-False ([bool]$universe.audusdClassifiedAsFailed) "PMS_EMS_OMS_R024_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD classified as failed."
Require-False ([bool]$universe.audusdLiveValidationGapBlocksPaperLedgerCommit) "PMS_EMS_OMS_R024_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD gap blocks commit."
Require-False ([bool]$universe.usdjpyLiveValidationGapBlocksPaperLedgerCommit) "PMS_EMS_OMS_R024_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY gap blocks commit."
Require-True ([bool]$usdjpy.usdJpyCaveatPreservationCreated) "PMS_EMS_OMS_R024_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
Require-True ([string]$usdjpy.usdJpySecurityId -eq "4004") "PMS_EMS_OMS_R024_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID missing."
Require-True ([string]$usdjpy.usdJpySecurityIdSource -eq "8") "PMS_EMS_OMS_R024_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource missing."
Require-False ([bool]$usdjpy.usdJpyClassifiedAsFailed) "PMS_EMS_OMS_R024_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY classified as failed."
Require-False ([bool]$usdjpy.audusdClassifiedAsFailed) "PMS_EMS_OMS_R024_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD classified as failed."
Require-True ([bool]$lmax.lmaxReadonlyBaselineReferenceCreated) "PMS_EMS_OMS_R024_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX baseline missing."
Require-False ([bool]$lmax.lmaxUsedInThisPhase) "PMS_EMS_OMS_R024_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX used."
Require-False ([bool]$lmax.lmaxCalledInThisPhase) "PMS_EMS_OMS_R024_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX called."
Require-False ([bool]$lmax.lmaxLiveValidationGapsBlockPaperLedgerCommit) "PMS_EMS_OMS_R024_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX gaps block commit."

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
    Require-False ([bool]$noExternal.$property) "PMS_EMS_OMS_R024_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected: $property"
}

foreach ($item in $forbidden.forbiddenActions) {
    if ([bool]$item.detected) {
        if ([string]$item.action -match "scheduler|service|timer|background") { Fail-Gate "PMS_EMS_OMS_R024_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Forbidden action detected: $($item.action)" }
        if ([string]$item.action -match "production ledger") { Fail-Gate "PMS_EMS_OMS_R024_FAIL_PRODUCTION_LEDGER_MUTATION" "Forbidden action detected: $($item.action)" }
        if ([string]$item.action -match "position state") {
            if ([string]$item.action -match "broker") { Fail-Gate "PMS_EMS_OMS_R024_FAIL_BROKER_POSITION_MUTATION" "Forbidden action detected: $($item.action)" }
            Fail-Gate "PMS_EMS_OMS_R024_FAIL_LIVE_POSITION_MUTATION" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "trading state") { Fail-Gate "PMS_EMS_OMS_R024_FAIL_TRADING_STATE_MUTATION" "Forbidden action detected: $($item.action)" }
        if ([string]$item.action -match "fill|execution report") { Fail-Gate "PMS_EMS_OMS_R024_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Forbidden action detected: $($item.action)" }
        if ([string]$item.action -match "order|trading|executable|OMS|parent|child|broker|submission|submitted|routed|route") { Fail-Gate "PMS_EMS_OMS_R024_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden action detected: $($item.action)" }
        Fail-Gate "PMS_EMS_OMS_R024_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $($item.action)"
    }
}

Require-True ([bool]$nextPhase.nextPhaseRecommendationCreated) "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "Next phase missing."
Require-True ([string]$nextPhase.recommendedNextPhase -eq "PMS-EMS-OMS-R025") "PMS_EMS_OMS_R024_FAIL_PAPER_LEDGER_COMMIT_MISSING" "Next phase is not R025."
Require-True ([bool]$nextPhase.mustRemainNoExternal) "PMS_EMS_OMS_R024_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase not no-external."

$apiSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Api/appsettings.json") "PMS_EMS_OMS_R024_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$workerSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/appsettings.json") "PMS_EMS_OMS_R024_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-False ([bool]$apiSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R024_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API live trading enabled."
Require-False ([bool]$workerSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R024_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker live trading enabled."
Require-False ([bool]$apiSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R024_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API external enabled."
Require-False ([bool]$workerSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R024_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker external enabled."
Require-True ([bool]$apiSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R024_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API fake gateway not required."
Require-True ([bool]$workerSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R024_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker fake gateway not required."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.Enabled) "PMS_EMS_OMS_R024_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX runtime enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowExternalConnections) "PMS_EMS_OMS_R024_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX external enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowOrderSubmission) "PMS_EMS_OMS_R024_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "LMAX order submission enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SchedulerEnabled) "PMS_EMS_OMS_R024_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "LMAX scheduler enabled."
Require-False ([bool]$workerSettings.MarketDataBars.Enabled) "PMS_EMS_OMS_R024_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker market data bars enabled."

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-pms-ems-oms-r024-*" -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combined = [string]::Join("`n", $artifactText)
foreach ($pattern in @("\u0001", "35=", "MDReqID\s*[:=]", "SenderCompID\s*[:=]", "TargetCompID\s*[:=]", "BeginString\s*[:=]", "SocketHost\s*[:=]", "TlsHost\s*[:=]", "Password\s*[:=]", "ApiKey\s*[:=]", "Bearer\s+[A-Za-z0-9_\.-]+", "rawBid", "rawAsk", "rawMid")) {
    if ($combined -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R024_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Unsafe serialized content pattern found: $pattern"
    }
}

$requiredFiles = @(
    "src/QQ.Production.Intraday.Application/QubesPaperLedgerStateCommit.cs",
    "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperLedgerStateCommitTests.cs"
)
foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $file))) {
        Fail-Gate "PMS_EMS_OMS_R024_FAIL_BUILD_OR_TESTS" "Required file missing: $file"
    }
}

$source = Get-Content -LiteralPath (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/QubesPaperLedgerStateCommit.cs") -Raw
foreach ($pattern in @("TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "SendOrderAsync", "SubmitOrder", "FixSession", "CreateOrderAsync")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R024_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "R024 source contains forbidden runtime pattern: $pattern"
    }
}
foreach ($pattern in @("AddHostedService", "IHostedService", "BackgroundService", "PeriodicTimer", "Task.Delay", "System.Threading.Timer")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R024_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "R024 source contains scheduler/service pattern: $pattern"
    }
}

$tests = Get-Content -LiteralPath (Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperLedgerStateCommitTests.cs") -Raw
foreach ($requiredTestName in @(
    "R023_approved_paper_ledger_preview_can_commit_to_paper_ledger_fixture_state",
    "Commit_requires_paper_ledger_commit_ready_no_external",
    "Audusd_ending_paper_quantity_is_applied",
    "Eurusd_ending_paper_quantity_is_applied",
    "Gbpusd_ending_paper_quantity_is_applied",
    "Commit_mutates_only_paper_ledger_fixture_state",
    "Live_position_mutation_count_remains_zero",
    "Broker_position_mutation_count_remains_zero",
    "Production_ledger_mutation_count_remains_zero",
    "Trading_state_mutation_count_remains_zero",
    "Order_fill_and_execution_report_counts_remain_zero",
    "Duplicate_commit_is_idempotent",
    "Qubes_cycle_operator_preview_simulation_plan_candidate_risk_rebalance_and_lot_sizing_lineage_is_preserved",
    "Commit_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action",
    "Api_and_worker_live_gateway_remain_disabled",
    "Commit_source_introduces_no_scheduler_timer_polling_or_background_job",
    "No_oms_or_broker_orders_are_created",
    "No_fills_or_execution_reports_are_created",
    "Audusd_is_not_misclassified_as_failed",
    "Usdjpy_caveat_remains_preserved"
)) {
    if ($tests -notmatch [regex]::Escape($requiredTestName)) {
        Fail-Gate "PMS_EMS_OMS_R024_FAIL_BUILD_OR_TESTS" "Focused test missing: $requiredTestName"
    }
}

Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R024_FAIL_BUILD_OR_TESTS" "Evidence marker missing."
Require-True ([string]$evidence.build.status -eq "PASS") "PMS_EMS_OMS_R024_FAIL_BUILD_OR_TESTS" "Build evidence missing or not PASS."
Require-Zero ([int]$evidence.build.failed) "PMS_EMS_OMS_R024_FAIL_BUILD_OR_TESTS" "Build evidence has failures."
Require-True ([string]$evidence.focusedTests.status -eq "PASS") "PMS_EMS_OMS_R024_FAIL_BUILD_OR_TESTS" "Focused test evidence missing or not PASS."
Require-Zero ([int]$evidence.focusedTests.failed) "PMS_EMS_OMS_R024_FAIL_BUILD_OR_TESTS" "Focused tests have failures."
Require-True ([string]$evidence.unitTests.status -eq "PASS") "PMS_EMS_OMS_R024_FAIL_BUILD_OR_TESTS" "Unit test evidence missing or not PASS."
Require-Zero ([int]$evidence.unitTests.failed) "PMS_EMS_OMS_R024_FAIL_BUILD_OR_TESTS" "Unit tests have failures."
Require-True ([string]$evidence.validator.status -eq "PASS") "PMS_EMS_OMS_R024_FAIL_BUILD_OR_TESTS" "Validator evidence missing or not PASS."
Require-Zero ([int]$evidence.validator.failed) "PMS_EMS_OMS_R024_FAIL_BUILD_OR_TESTS" "Validator evidence has failures."

Write-Host "PMS_EMS_OMS_R024_PASS_PAPER_LEDGER_STATE_COMMIT_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R024_PASS_PAPER_ONLY_LEDGER_MUTATION_GATE_READY_NO_EXTERNAL"
