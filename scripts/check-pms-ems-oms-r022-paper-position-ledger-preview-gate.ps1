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
    "phase-pms-ems-oms-r022-summary.md" = "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
    "phase-pms-ems-oms-r022-paper-position-ledger-preview-contract.json" = "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
    "phase-pms-ems-oms-r022-paper-position-ledger-preview.json" = "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
    "phase-pms-ems-oms-r022-paper-position-ledger-preview-lines.json" = "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
    "phase-pms-ems-oms-r022-paper-ledger-baseline-fixture.json" = "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
    "phase-pms-ems-oms-r022-paper-ledger-preview-summary.json" = "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
    "phase-pms-ems-oms-r022-no-live-position-mutation-audit.json" = "PMS_EMS_OMS_R022_FAIL_LIVE_POSITION_MUTATION"
    "phase-pms-ems-oms-r022-no-broker-position-mutation-audit.json" = "PMS_EMS_OMS_R022_FAIL_BROKER_POSITION_MUTATION"
    "phase-pms-ems-oms-r022-no-production-ledger-mutation-audit.json" = "PMS_EMS_OMS_R022_FAIL_PRODUCTION_LEDGER_MUTATION"
    "phase-pms-ems-oms-r022-no-trading-state-mutation-audit.json" = "PMS_EMS_OMS_R022_FAIL_TRADING_STATE_MUTATION"
    "phase-pms-ems-oms-r022-no-fill-no-execution-report-audit.json" = "PMS_EMS_OMS_R022_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
    "phase-pms-ems-oms-r022-no-order-created-audit.json" = "PMS_EMS_OMS_R022_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r022-no-route-no-submission-audit.json" = "PMS_EMS_OMS_R022_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED"
    "phase-pms-ems-oms-r022-idempotency-evidence.json" = "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
    "phase-pms-ems-oms-r022-risk-lineage-preservation.json" = "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
    "phase-pms-ems-oms-r022-qubes-lineage-preservation.json" = "PMS_EMS_OMS_R022_FAIL_QUBES_LINEAGE_WEAKENED"
    "phase-pms-ems-oms-r022-operator-decision-lineage-preservation.json" = "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
    "phase-pms-ems-oms-r022-position-preview-lineage-preservation.json" = "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
    "phase-pms-ems-oms-r022-simulation-result-lineage-preservation.json" = "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
    "phase-pms-ems-oms-r022-plan-lineage-preservation.json" = "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
    "phase-pms-ems-oms-r022-paper-candidate-lineage-preservation.json" = "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
    "phase-pms-ems-oms-r022-rebalance-intent-lineage-preservation.json" = "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
    "phase-pms-ems-oms-r022-lot-sizing-lineage-preservation.json" = "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
    "phase-pms-ems-oms-r022-instrument-universe-handling.json" = "PMS_EMS_OMS_R022_FAIL_LMAX_GAP_BLOCKS_LEDGER_PREVIEW"
    "phase-pms-ems-oms-r022-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R022_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r022-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R022_FAIL_LMAX_GAP_BLOCKS_LEDGER_PREVIEW"
    "phase-pms-ems-oms-r022-no-external-audit.json" = "PMS_EMS_OMS_R022_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r022-forbidden-actions-audit.json" = "PMS_EMS_OMS_R022_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r022-next-phase-recommendation.json" = "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
    "phase-pms-ems-oms-r022-build-test-validator-evidence.json" = "PMS_EMS_OMS_R022_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $entry.Key))) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$contract = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r022-paper-position-ledger-preview-contract.json") "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
$preview = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r022-paper-position-ledger-preview.json") "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
$lines = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r022-paper-position-ledger-preview-lines.json") "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
$baseline = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r022-paper-ledger-baseline-fixture.json") "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
$summary = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r022-paper-ledger-preview-summary.json") "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
$livePositionAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r022-no-live-position-mutation-audit.json") "PMS_EMS_OMS_R022_FAIL_LIVE_POSITION_MUTATION"
$brokerPositionAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r022-no-broker-position-mutation-audit.json") "PMS_EMS_OMS_R022_FAIL_BROKER_POSITION_MUTATION"
$productionLedgerAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r022-no-production-ledger-mutation-audit.json") "PMS_EMS_OMS_R022_FAIL_PRODUCTION_LEDGER_MUTATION"
$tradingAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r022-no-trading-state-mutation-audit.json") "PMS_EMS_OMS_R022_FAIL_TRADING_STATE_MUTATION"
$fillAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r022-no-fill-no-execution-report-audit.json") "PMS_EMS_OMS_R022_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$orderAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r022-no-order-created-audit.json") "PMS_EMS_OMS_R022_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$routeAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r022-no-route-no-submission-audit.json") "PMS_EMS_OMS_R022_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED"
$idempotency = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r022-idempotency-evidence.json") "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
$risk = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r022-risk-lineage-preservation.json") "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
$qubes = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r022-qubes-lineage-preservation.json") "PMS_EMS_OMS_R022_FAIL_QUBES_LINEAGE_WEAKENED"
$operatorLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r022-operator-decision-lineage-preservation.json") "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
$positionPreviewLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r022-position-preview-lineage-preservation.json") "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
$simulationLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r022-simulation-result-lineage-preservation.json") "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
$planLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r022-plan-lineage-preservation.json") "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
$candidateLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r022-paper-candidate-lineage-preservation.json") "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
$rebalanceLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r022-rebalance-intent-lineage-preservation.json") "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
$lotSizingLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r022-lot-sizing-lineage-preservation.json") "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r022-instrument-universe-handling.json") "PMS_EMS_OMS_R022_FAIL_LMAX_GAP_BLOCKS_LEDGER_PREVIEW"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r022-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R022_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r022-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R022_FAIL_LMAX_GAP_BLOCKS_LEDGER_PREVIEW"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r022-no-external-audit.json") "PMS_EMS_OMS_R022_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r022-forbidden-actions-audit.json") "PMS_EMS_OMS_R022_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$nextPhase = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r022-next-phase-recommendation.json") "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r022-build-test-validator-evidence.json") "PMS_EMS_OMS_R022_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$contract.paperPositionLedgerPreviewContractCreated) "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "Ledger preview contract missing."
foreach ($property in @("requiresPaperOnly", "requiresPreviewOnly", "requiresNoExternal", "requiresNoLivePositionMutation", "requiresNoBrokerPositionMutation", "requiresNoProductionLedgerMutation", "requiresNoTradingStateMutation", "requiresNoFillCreated", "requiresNoExecutionReportCreated", "requiresNoOrderCreated", "requiresNoBrokerRoute", "requiresNotSubmitted")) {
    Require-True ([bool]$contract.$property) "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "Contract safety flag missing: $property"
}

Require-True ([bool]$preview.paperPositionLedgerPreviewCreated) "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "Ledger preview missing."
Require-True ([string]$preview.previewStatus -eq "PaperLedgerPreviewReady") "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "Ledger preview status wrong."
Require-True ([string]$preview.safetyStatus -eq "PaperLedgerPreviewOnly") "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "Ledger preview safety status wrong."
Require-True ([int]$preview.previewLineCount -eq 3) "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "Ledger preview line count wrong."
foreach ($property in @("paperOnly", "previewOnly", "noExternal", "noLivePositionMutation", "noBrokerPositionMutation", "noProductionLedgerMutation", "noTradingStateMutation", "noFillCreated", "noExecutionReportCreated", "noOrderCreated", "noBrokerRoute", "notSubmitted")) {
    Require-True ([bool]$preview.$property) "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "Ledger preview safety flag missing: $property"
}
foreach ($property in @("livePositionStateMutated", "brokerPositionStateMutated", "productionLedgerStateMutated", "tradingStateMutated", "fillCreated", "executionReportCreated", "omsOrderCreated", "parentOrderCreated", "childOrderCreated", "brokerOrderCreated", "orderStateCreated", "submittedOrders", "brokerRouteCreated")) {
    Require-False ([bool]$preview.$property) "PMS_EMS_OMS_R022_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Ledger preview unsafe flag detected: $property"
}

Require-True ([bool]$baseline.paperLedgerBaselineFixtureCreated) "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "Baseline fixture missing."
Require-True ([string]$baseline.baselineSource -eq "NoExternalZeroPaperLedgerBaselineFixture") "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "Baseline source wrong."
Require-True ([bool]$baseline.noExternal) "PMS_EMS_OMS_R022_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Baseline is not no-external."
Require-True ([bool]$baseline.notBrokerState) "PMS_EMS_OMS_R022_FAIL_BROKER_POSITION_MUTATION" "Baseline reads broker state."
Require-True ([bool]$baseline.notLiveProductionPositionState) "PMS_EMS_OMS_R022_FAIL_LIVE_POSITION_MUTATION" "Baseline reads live position state."
Require-True ([bool]$baseline.notPersistedProductionLedgerState) "PMS_EMS_OMS_R022_FAIL_PRODUCTION_LEDGER_MUTATION" "Baseline reads production ledger state."
Require-True (@($baseline.lines | Where-Object { $_.currencyOrSymbol -eq "AUDUSD" -and [decimal]$_.startingPaperQuantity -eq 0 }).Count -eq 1) "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "AUDUSD baseline missing."
Require-True (@($baseline.lines | Where-Object { $_.currencyOrSymbol -eq "EURUSD" -and [decimal]$_.startingPaperQuantity -eq 0 }).Count -eq 1) "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "EURUSD baseline missing."
Require-True (@($baseline.lines | Where-Object { $_.currencyOrSymbol -eq "GBPUSD" -and [decimal]$_.startingPaperQuantity -eq 0 }).Count -eq 1) "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "GBPUSD baseline missing."

Require-True ([bool]$lines.paperPositionLedgerPreviewLinesCreated) "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "Ledger preview lines missing."
Require-True ([int]$lines.lineCount -eq 3) "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "Ledger preview line count wrong."
Require-True (@($lines.lines | Where-Object { $_.currencyOrSymbol -eq "AUDUSD" -and [decimal]$_.startingPaperQuantity -eq 0 -and [decimal]$_.simulatedDeltaQuantity -eq 131000 -and [decimal]$_.previewEndingPaperQuantity -eq 131000 -and $_.quantityCurrency -eq "AUD" }).Count -eq 1) "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "AUDUSD ledger line missing."
Require-True (@($lines.lines | Where-Object { $_.currencyOrSymbol -eq "EURUSD" -and [decimal]$_.startingPaperQuantity -eq 0 -and [decimal]$_.simulatedDeltaQuantity -eq 124000 -and [decimal]$_.previewEndingPaperQuantity -eq 124000 -and $_.quantityCurrency -eq "EUR" }).Count -eq 1) "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "EURUSD ledger line missing."
Require-True (@($lines.lines | Where-Object { $_.currencyOrSymbol -eq "GBPUSD" -and [decimal]$_.startingPaperQuantity -eq 0 -and [decimal]$_.simulatedDeltaQuantity -eq -368000 -and [decimal]$_.previewEndingPaperQuantity -eq -368000 -and $_.quantityCurrency -eq "GBP" }).Count -eq 1) "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "GBPUSD ledger line missing."
foreach ($line in $lines.lines) {
    foreach ($property in @("paperOnly", "previewOnly", "noExternal", "noLivePositionMutation", "noBrokerPositionMutation", "noProductionLedgerMutation", "noTradingStateMutation", "noFillCreated", "noExecutionReportCreated", "noOrderCreated", "noBrokerRoute", "notSubmitted")) {
        Require-True ([bool]$line.$property) "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "Line safety flag missing: $property"
    }
}

Require-True ([bool]$summary.paperLedgerPreviewSummaryCreated) "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "Ledger summary missing."
Require-True ([int]$summary.previewLineCount -eq 3) "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "Summary line count wrong."
Require-True ([int]$summary.appliedPreviewLineCount -eq 3) "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "Summary applied count wrong."
Require-True ([int]$summary.livePositionMutationCount -eq 0) "PMS_EMS_OMS_R022_FAIL_LIVE_POSITION_MUTATION" "Live position mutation count nonzero."
Require-True ([int]$summary.brokerPositionMutationCount -eq 0) "PMS_EMS_OMS_R022_FAIL_BROKER_POSITION_MUTATION" "Broker position mutation count nonzero."
Require-True ([int]$summary.productionLedgerMutationCount -eq 0) "PMS_EMS_OMS_R022_FAIL_PRODUCTION_LEDGER_MUTATION" "Production ledger mutation count nonzero."
Require-True ([int]$summary.tradingStateMutationCount -eq 0) "PMS_EMS_OMS_R022_FAIL_TRADING_STATE_MUTATION" "Trading state mutation count nonzero."
Require-True ([string]$summary.safetyStatus -eq "PaperLedgerPreviewOnly") "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "Summary safety status wrong."

Require-True ([bool]$livePositionAudit.noLivePositionMutationAuditCreated) "PMS_EMS_OMS_R022_FAIL_LIVE_POSITION_MUTATION" "No-live-position audit missing."
Require-False ([bool]$livePositionAudit.livePositionStateMutated) "PMS_EMS_OMS_R022_FAIL_LIVE_POSITION_MUTATION" "Live position mutated."
Require-False ([bool]$livePositionAudit.ledgerPreviewMutatesLivePositions) "PMS_EMS_OMS_R022_FAIL_LIVE_POSITION_MUTATION" "Ledger preview mutates live positions."
Require-True ([bool]$brokerPositionAudit.noBrokerPositionMutationAuditCreated) "PMS_EMS_OMS_R022_FAIL_BROKER_POSITION_MUTATION" "No-broker-position audit missing."
Require-False ([bool]$brokerPositionAudit.brokerPositionStateMutated) "PMS_EMS_OMS_R022_FAIL_BROKER_POSITION_MUTATION" "Broker position mutated."
Require-False ([bool]$brokerPositionAudit.ledgerPreviewMutatesBrokerPositions) "PMS_EMS_OMS_R022_FAIL_BROKER_POSITION_MUTATION" "Ledger preview mutates broker positions."
Require-True ([bool]$productionLedgerAudit.noProductionLedgerMutationAuditCreated) "PMS_EMS_OMS_R022_FAIL_PRODUCTION_LEDGER_MUTATION" "No-production-ledger audit missing."
Require-False ([bool]$productionLedgerAudit.productionLedgerStateMutated) "PMS_EMS_OMS_R022_FAIL_PRODUCTION_LEDGER_MUTATION" "Production ledger mutated."
Require-False ([bool]$productionLedgerAudit.persistedProductionLedgerStateMutated) "PMS_EMS_OMS_R022_FAIL_PRODUCTION_LEDGER_MUTATION" "Persisted production ledger mutated."
Require-False ([bool]$productionLedgerAudit.ledgerPreviewMutatesProductionLedgerState) "PMS_EMS_OMS_R022_FAIL_PRODUCTION_LEDGER_MUTATION" "Ledger preview mutates production ledger."
Require-True ([bool]$tradingAudit.noTradingStateMutationAuditCreated) "PMS_EMS_OMS_R022_FAIL_TRADING_STATE_MUTATION" "No-trading-state audit missing."
Require-False ([bool]$tradingAudit.tradingStateMutated) "PMS_EMS_OMS_R022_FAIL_TRADING_STATE_MUTATION" "Trading state mutated."
Require-False ([bool]$tradingAudit.ledgerPreviewMutatesTradingState) "PMS_EMS_OMS_R022_FAIL_TRADING_STATE_MUTATION" "Ledger preview mutates trading state."

Require-True ([bool]$fillAudit.noFillNoExecutionReportAuditCreated) "PMS_EMS_OMS_R022_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill/report audit missing."
foreach ($property in @("fillCreated", "executionReportCreated", "simulationFillCreatedAsRealFill", "brokerExecutionReportCreated", "ledgerPreviewCreatesFillOrExecutionReport")) {
    Require-False ([bool]$fillAudit.$property) "PMS_EMS_OMS_R022_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill/report audit detected: $property"
}
Require-True ([bool]$orderAudit.noOrderCreatedAuditCreated) "PMS_EMS_OMS_R022_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No-order audit missing."
foreach ($property in @("omsOrderCreated", "parentOrderCreated", "childOrderCreated", "brokerOrderCreated", "executableOrderCreated", "orderStateCreated", "orderSubmissionPathIntroduced", "ordersSubmitted")) {
    Require-False ([bool]$orderAudit.$property) "PMS_EMS_OMS_R022_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order audit detected: $property"
}
Require-True ([bool]$routeAudit.noRouteNoSubmissionAuditCreated) "PMS_EMS_OMS_R022_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "No-route/no-submission audit missing."
foreach ($property in @("brokerRouteCreated", "brokerRouteAssigned", "submissionInstructionCreated", "orderSubmissionPathInvoked", "paperPreviewSubmitted", "paperPreviewRouteable", "ledgerPreviewSubmitted", "ledgerPreviewRouteable")) {
    Require-False ([bool]$routeAudit.$property) "PMS_EMS_OMS_R022_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "Route/submission audit detected: $property"
}

Require-True ([bool]$idempotency.idempotencyEvidenceCreated) "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "Idempotency missing."
Require-True ([string]$idempotency.idempotencyKey -eq "PaperPositionLedgerPreviewId") "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "Wrong idempotency key."
Require-True ([string]$idempotency.duplicatePreviewBehavior -eq "ExistingPreviewReturned") "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "Duplicate behavior missing."
Require-False ([bool]$idempotency.duplicatesCreateAdditionalLedgerPreviews) "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "Duplicate previews created."

Require-True ([bool]$risk.riskLineagePreservationCreated) "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "Risk lineage missing."
Require-False ([bool]$risk.riskLineageMissing) "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "Risk lineage marked missing."
Require-True ([bool]$qubes.qubesLineagePreservationCreated) "PMS_EMS_OMS_R022_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage missing."
Require-True ([string]$qubes.sourceSystem -eq "ModelWeightSourceSystem.Qubes") "PMS_EMS_OMS_R022_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes source missing."
Require-True ([int]$qubes.cadenceMinutes -eq 15) "PMS_EMS_OMS_R022_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes cadence missing."
Require-False ([bool]$qubes.qubesLineageWeakened) "PMS_EMS_OMS_R022_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage weakened."
foreach ($lineage in @($operatorLineage, $positionPreviewLineage, $simulationLineage, $planLineage, $candidateLineage, $rebalanceLineage, $lotSizingLineage)) {
    $created = $lineage.PSObject.Properties.Name | Where-Object { $_ -match "Created$" } | Select-Object -First 1
    if ($created) { Require-True ([bool]$lineage.$created) "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "Lineage artifact missing: $created" }
    $missing = $lineage.PSObject.Properties.Name | Where-Object { $_ -match "Missing$" } | Select-Object -First 1
    if ($missing) { Require-False ([bool]$lineage.$missing) "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "Lineage marked missing: $missing" }
}
Require-True ([bool]$rebalanceLineage.rebalanceIntentsRemainNonExecutable) "PMS_EMS_OMS_R022_FAIL_EXECUTABLE_ORDER_CREATED" "Rebalance intents executable."
Require-False ([bool]$rebalanceLineage.rebalanceIntentCreatesOrder) "PMS_EMS_OMS_R022_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Rebalance intent creates order."

Require-True ([bool]$universe.instrumentUniverseHandlingCreated) "PMS_EMS_OMS_R022_FAIL_LMAX_GAP_BLOCKS_LEDGER_PREVIEW" "Instrument universe handling missing."
Require-False ([bool]$universe.lmaxLiveValidationGapsBlockPaperLedgerPreview) "PMS_EMS_OMS_R022_FAIL_LMAX_GAP_BLOCKS_LEDGER_PREVIEW" "LMAX gaps block ledger preview."
Require-False ([bool]$universe.audusdClassifiedAsFailed) "PMS_EMS_OMS_R022_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD classified as failed."
Require-False ([bool]$universe.audusdLiveValidationGapBlocksPaperLedgerPreview) "PMS_EMS_OMS_R022_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD gap blocks ledger preview."
Require-False ([bool]$universe.usdjpyLiveValidationGapBlocksPaperLedgerPreview) "PMS_EMS_OMS_R022_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY gap blocks ledger preview."
Require-True ([bool]$usdjpy.usdJpyCaveatPreservationCreated) "PMS_EMS_OMS_R022_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
Require-True ([string]$usdjpy.usdJpySecurityId -eq "4004") "PMS_EMS_OMS_R022_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID missing."
Require-True ([string]$usdjpy.usdJpySecurityIdSource -eq "8") "PMS_EMS_OMS_R022_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource missing."
Require-False ([bool]$usdjpy.usdJpyClassifiedAsFailed) "PMS_EMS_OMS_R022_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY classified as failed."
Require-False ([bool]$usdjpy.audusdClassifiedAsFailed) "PMS_EMS_OMS_R022_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD classified as failed."
Require-True ([bool]$lmax.lmaxReadonlyBaselineReferenceCreated) "PMS_EMS_OMS_R022_FAIL_LMAX_GAP_BLOCKS_LEDGER_PREVIEW" "LMAX baseline missing."
Require-False ([bool]$lmax.lmaxUsedInThisPhase) "PMS_EMS_OMS_R022_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX used."
Require-False ([bool]$lmax.lmaxCalledInThisPhase) "PMS_EMS_OMS_R022_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX called."
Require-False ([bool]$lmax.lmaxLiveValidationGapsBlockPaperLedgerPreview) "PMS_EMS_OMS_R022_FAIL_LMAX_GAP_BLOCKS_LEDGER_PREVIEW" "LMAX gaps block ledger preview."

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
    "tradingStateMutated",
    "livePositionStateMutated",
    "brokerPositionStateMutated",
    "productionLedgerStateMutated",
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
    "paperPreviewMutatesLivePositions",
    "paperPreviewMutatesBrokerPositions",
    "paperPreviewMutatesProductionLedgerState",
    "paperPreviewMutatesTradingState",
    "lmaxLiveValidationGapsBlockPaperLedgerPreview"
)) {
    Require-False ([bool]$noExternal.$property) "PMS_EMS_OMS_R022_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected: $property"
}

foreach ($item in $forbidden.forbiddenActions) {
    if ([bool]$item.detected) {
        if ([string]$item.action -match "scheduler|service|timer|background") {
            Fail-Gate "PMS_EMS_OMS_R022_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "production ledger") {
            Fail-Gate "PMS_EMS_OMS_R022_FAIL_PRODUCTION_LEDGER_MUTATION" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "position state") {
            if ([string]$item.action -match "broker") { Fail-Gate "PMS_EMS_OMS_R022_FAIL_BROKER_POSITION_MUTATION" "Forbidden action detected: $($item.action)" }
            Fail-Gate "PMS_EMS_OMS_R022_FAIL_LIVE_POSITION_MUTATION" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "trading state") {
            Fail-Gate "PMS_EMS_OMS_R022_FAIL_TRADING_STATE_MUTATION" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "fill|execution report") {
            Fail-Gate "PMS_EMS_OMS_R022_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "order|trading|executable|OMS|parent|child|broker|submitted|routed") {
            Fail-Gate "PMS_EMS_OMS_R022_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden action detected: $($item.action)"
        }

        Fail-Gate "PMS_EMS_OMS_R022_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $($item.action)"
    }
}

Require-True ([bool]$nextPhase.nextPhaseRecommendationCreated) "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "Next phase missing."
Require-True ([string]$nextPhase.recommendedNextPhase -eq "PMS-EMS-OMS-R023") "PMS_EMS_OMS_R022_FAIL_POSITION_LEDGER_PREVIEW_MISSING" "Next phase is not R023."
Require-True ([bool]$nextPhase.mustRemainNoExternal) "PMS_EMS_OMS_R022_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase not no-external."

$apiSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Api/appsettings.json") "PMS_EMS_OMS_R022_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$workerSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/appsettings.json") "PMS_EMS_OMS_R022_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-False ([bool]$apiSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R022_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API live trading enabled."
Require-False ([bool]$workerSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R022_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker live trading enabled."
Require-False ([bool]$apiSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R022_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API external enabled."
Require-False ([bool]$workerSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R022_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker external enabled."
Require-True ([bool]$apiSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R022_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API fake gateway not required."
Require-True ([bool]$workerSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R022_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker fake gateway not required."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.Enabled) "PMS_EMS_OMS_R022_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX runtime enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowExternalConnections) "PMS_EMS_OMS_R022_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX external enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowOrderSubmission) "PMS_EMS_OMS_R022_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "LMAX order submission enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SchedulerEnabled) "PMS_EMS_OMS_R022_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "LMAX scheduler enabled."
Require-False ([bool]$workerSettings.MarketDataBars.Enabled) "PMS_EMS_OMS_R022_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker market data bars enabled."

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-pms-ems-oms-r022-*" -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combined = [string]::Join("`n", $artifactText)
foreach ($pattern in @("\u0001", "35=", "MDReqID\s*[:=]", "SenderCompID\s*[:=]", "TargetCompID\s*[:=]", "BeginString\s*[:=]", "SocketHost\s*[:=]", "TlsHost\s*[:=]", "Password\s*[:=]", "ApiKey\s*[:=]", "Bearer\s+[A-Za-z0-9_\.-]+", "rawBid", "rawAsk", "rawMid")) {
    if ($combined -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R022_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Unsafe serialized content pattern found: $pattern"
    }
}

$requiredFiles = @(
    "src/QQ.Production.Intraday.Application/QubesPaperPositionLedgerPreview.cs",
    "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperPositionLedgerPreviewTests.cs"
)
foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $file))) {
        Fail-Gate "PMS_EMS_OMS_R022_FAIL_BUILD_OR_TESTS" "Required file missing: $file"
    }
}

$source = Get-Content -LiteralPath (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/QubesPaperPositionLedgerPreview.cs") -Raw
foreach ($pattern in @("TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "SendOrderAsync", "SubmitOrder", "FixSession", "CreateOrderAsync")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R022_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "R022 source contains forbidden runtime pattern: $pattern"
    }
}
foreach ($pattern in @("AddHostedService", "IHostedService", "BackgroundService", "PeriodicTimer", "Task.Delay", "System.Threading.Timer")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R022_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "R022 source contains scheduler/service pattern: $pattern"
    }
}

$tests = Get-Content -LiteralPath (Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperPositionLedgerPreviewTests.cs") -Raw
foreach ($requiredTestName in @(
    "R021_archived_paper_position_preview_can_produce_paper_ledger_preview",
    "Ledger_preview_preserves_paper_position_preview_id",
    "Ledger_preview_preserves_paper_simulation_result_id",
    "Ledger_preview_preserves_cycle_run_id_and_qubes_run_id",
    "Ledger_preview_preserves_operator_decision_id",
    "Audusd_preview_line_applies_delta",
    "Eurusd_preview_line_applies_delta",
    "Gbpusd_preview_line_applies_sell_delta",
    "Starting_paper_quantities_come_from_no_external_fixture_not_broker_state",
    "Ending_paper_quantities_are_computed_deterministically",
    "Live_position_mutation_count_is_zero",
    "Broker_position_mutation_count_is_zero",
    "Trading_state_mutation_count_is_zero",
    "No_live_position_state_is_mutated",
    "No_broker_position_state_is_mutated",
    "No_production_ledger_state_is_mutated",
    "No_trading_state_is_mutated",
    "No_fills_are_created",
    "No_execution_reports_are_created",
    "No_oms_or_broker_orders_are_created",
    "No_order_submission_path_is_introduced",
    "Duplicate_ledger_preview_handling_is_idempotent",
    "Qubes_cycle_operator_preview_simulation_plan_candidate_risk_rebalance_and_lot_sizing_lineage_is_preserved",
    "Preview_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action",
    "Api_and_worker_live_gateway_remain_disabled",
    "Preview_source_introduces_no_scheduler_timer_polling_or_background_job",
    "Audusd_is_not_misclassified_as_failed",
    "Usdjpy_caveat_remains_preserved"
)) {
    if ($tests -notmatch [regex]::Escape($requiredTestName)) {
        Fail-Gate "PMS_EMS_OMS_R022_FAIL_BUILD_OR_TESTS" "Focused test missing: $requiredTestName"
    }
}

Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R022_FAIL_BUILD_OR_TESTS" "Evidence marker missing."
Require-True ([string]$evidence.build.status -eq "PASS") "PMS_EMS_OMS_R022_FAIL_BUILD_OR_TESTS" "Build evidence missing or not PASS."
Require-True ([int]$evidence.build.failed -eq 0) "PMS_EMS_OMS_R022_FAIL_BUILD_OR_TESTS" "Build evidence has failures."
Require-True ([string]$evidence.focusedTests.status -eq "PASS") "PMS_EMS_OMS_R022_FAIL_BUILD_OR_TESTS" "Focused test evidence missing or not PASS."
Require-True ([int]$evidence.focusedTests.failed -eq 0) "PMS_EMS_OMS_R022_FAIL_BUILD_OR_TESTS" "Focused tests have failures."
Require-True ([string]$evidence.unitTests.status -eq "PASS") "PMS_EMS_OMS_R022_FAIL_BUILD_OR_TESTS" "Unit test evidence missing or not PASS."
Require-True ([int]$evidence.unitTests.failed -eq 0) "PMS_EMS_OMS_R022_FAIL_BUILD_OR_TESTS" "Unit tests have failures."
Require-True ([string]$evidence.validator.status -eq "PASS") "PMS_EMS_OMS_R022_FAIL_BUILD_OR_TESTS" "Validator evidence missing or not PASS."
Require-True ([int]$evidence.validator.failed -eq 0) "PMS_EMS_OMS_R022_FAIL_BUILD_OR_TESTS" "Validator evidence has failures."

Write-Host "PMS_EMS_OMS_R022_PASS_PAPER_POSITION_LEDGER_PREVIEW_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R022_PASS_NO_LIVE_POSITION_MUTATION_GATE_READY_NO_EXTERNAL"
