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
    "phase-pms-ems-oms-r021-summary.md" = "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r021-paper-position-preview-archive-contract.json" = "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r021-paper-position-preview-archive.json" = "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r021-paper-position-preview-report.md" = "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_REPORT_MISSING"
    "phase-pms-ems-oms-r021-paper-position-preview-report.json" = "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_REPORT_MISSING"
    "phase-pms-ems-oms-r021-paper-position-preview-lines.json" = "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r021-no-live-position-mutation-audit.json" = "PMS_EMS_OMS_R021_FAIL_LIVE_POSITION_MUTATION"
    "phase-pms-ems-oms-r021-no-broker-position-mutation-audit.json" = "PMS_EMS_OMS_R021_FAIL_BROKER_POSITION_MUTATION"
    "phase-pms-ems-oms-r021-no-trading-state-mutation-audit.json" = "PMS_EMS_OMS_R021_FAIL_TRADING_STATE_MUTATION"
    "phase-pms-ems-oms-r021-no-fill-no-execution-report-audit.json" = "PMS_EMS_OMS_R021_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
    "phase-pms-ems-oms-r021-no-order-created-audit.json" = "PMS_EMS_OMS_R021_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
    "phase-pms-ems-oms-r021-no-route-no-submission-audit.json" = "PMS_EMS_OMS_R021_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED"
    "phase-pms-ems-oms-r021-idempotency-evidence.json" = "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r021-risk-lineage-preservation.json" = "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r021-qubes-lineage-preservation.json" = "PMS_EMS_OMS_R021_FAIL_QUBES_LINEAGE_WEAKENED"
    "phase-pms-ems-oms-r021-operator-decision-lineage-preservation.json" = "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r021-simulation-result-lineage-preservation.json" = "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r021-plan-lineage-preservation.json" = "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r021-paper-candidate-lineage-preservation.json" = "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r021-rebalance-intent-lineage-preservation.json" = "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r021-lot-sizing-lineage-preservation.json" = "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r021-instrument-universe-handling.json" = "PMS_EMS_OMS_R021_FAIL_LMAX_GAP_BLOCKS_POSITION_PREVIEW_REPORT"
    "phase-pms-ems-oms-r021-usdjpy-caveat-preservation.json" = "PMS_EMS_OMS_R021_FAIL_USDJPY_CAVEAT_WEAKENED"
    "phase-pms-ems-oms-r021-lmax-readonly-baseline-reference.json" = "PMS_EMS_OMS_R021_FAIL_LMAX_GAP_BLOCKS_POSITION_PREVIEW_REPORT"
    "phase-pms-ems-oms-r021-no-external-audit.json" = "PMS_EMS_OMS_R021_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r021-forbidden-actions-audit.json" = "PMS_EMS_OMS_R021_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
    "phase-pms-ems-oms-r021-next-phase-recommendation.json" = "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING"
    "phase-pms-ems-oms-r021-build-test-validator-evidence.json" = "PMS_EMS_OMS_R021_FAIL_BUILD_OR_TESTS"
}

foreach ($entry in $requiredArtifacts.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $entry.Key))) {
        Fail-Gate $entry.Value "Missing required artifact: $($entry.Key)"
    }
}

$contract = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r021-paper-position-preview-archive-contract.json") "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING"
$archive = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r021-paper-position-preview-archive.json") "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING"
$report = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r021-paper-position-preview-report.json") "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_REPORT_MISSING"
$lines = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r021-paper-position-preview-lines.json") "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING"
$livePositionAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r021-no-live-position-mutation-audit.json") "PMS_EMS_OMS_R021_FAIL_LIVE_POSITION_MUTATION"
$brokerPositionAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r021-no-broker-position-mutation-audit.json") "PMS_EMS_OMS_R021_FAIL_BROKER_POSITION_MUTATION"
$tradingAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r021-no-trading-state-mutation-audit.json") "PMS_EMS_OMS_R021_FAIL_TRADING_STATE_MUTATION"
$fillAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r021-no-fill-no-execution-report-audit.json") "PMS_EMS_OMS_R021_FAIL_FILL_OR_EXECUTION_REPORT_CREATED"
$orderAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r021-no-order-created-audit.json") "PMS_EMS_OMS_R021_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED"
$routeAudit = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r021-no-route-no-submission-audit.json") "PMS_EMS_OMS_R021_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED"
$idempotency = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r021-idempotency-evidence.json") "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING"
$risk = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r021-risk-lineage-preservation.json") "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING"
$qubes = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r021-qubes-lineage-preservation.json") "PMS_EMS_OMS_R021_FAIL_QUBES_LINEAGE_WEAKENED"
$operatorLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r021-operator-decision-lineage-preservation.json") "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING"
$simulationLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r021-simulation-result-lineage-preservation.json") "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING"
$planLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r021-plan-lineage-preservation.json") "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING"
$candidateLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r021-paper-candidate-lineage-preservation.json") "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING"
$rebalanceLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r021-rebalance-intent-lineage-preservation.json") "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING"
$lotSizingLineage = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r021-lot-sizing-lineage-preservation.json") "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING"
$universe = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r021-instrument-universe-handling.json") "PMS_EMS_OMS_R021_FAIL_LMAX_GAP_BLOCKS_POSITION_PREVIEW_REPORT"
$usdjpy = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r021-usdjpy-caveat-preservation.json") "PMS_EMS_OMS_R021_FAIL_USDJPY_CAVEAT_WEAKENED"
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r021-lmax-readonly-baseline-reference.json") "PMS_EMS_OMS_R021_FAIL_LMAX_GAP_BLOCKS_POSITION_PREVIEW_REPORT"
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r021-no-external-audit.json") "PMS_EMS_OMS_R021_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r021-forbidden-actions-audit.json") "PMS_EMS_OMS_R021_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$nextPhase = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r021-next-phase-recommendation.json") "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING"
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-r021-build-test-validator-evidence.json") "PMS_EMS_OMS_R021_FAIL_BUILD_OR_TESTS"

Require-True ([bool]$contract.paperPositionPreviewArchiveContractCreated) "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING" "Archive contract missing."
foreach ($property in @("requiresPaperOnly", "requiresSimulatedOnly", "requiresNoLivePositionMutation", "requiresNoBrokerPositionMutation", "requiresNoTradingStateMutation", "requiresNoFillCreated", "requiresNoExecutionReportCreated", "requiresNoOrderCreated", "requiresNoBrokerRoute", "requiresNotSubmitted", "noExternal", "nonExecutable", "notAnOrder")) {
    Require-True ([bool]$contract.$property) "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING" "Contract safety flag missing: $property"
}

Require-True ([bool]$archive.paperPositionPreviewArchiveCreated) "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING" "Archive missing."
Require-True ([string]$archive.archiveStatus -eq "ArchivedNoExternal") "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING" "Archive status wrong."
Require-True ([string]$archive.safetyStatus -eq "PaperPositionPreviewOnly") "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING" "Archive safety status wrong."
Require-True ([int]$archive.previewLineCount -eq 3) "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING" "Archive line count wrong."
foreach ($property in @("paperOnly", "simulatedOnly", "noLivePositionMutation", "noBrokerPositionMutation", "noTradingStateMutation", "noFillCreated", "noExecutionReportCreated", "noOrderCreated", "noBrokerRoute", "notSubmitted")) {
    Require-True ([bool]$archive.$property) "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING" "Archive safety flag missing: $property"
}
foreach ($property in @("livePositionStateMutated", "brokerPositionStateMutated", "tradingStateMutated", "fillCreated", "executionReportCreated", "omsOrderCreated", "parentOrderCreated", "childOrderCreated", "brokerOrderCreated", "orderStateCreated", "submittedOrders", "brokerRouteCreated")) {
    Require-False ([bool]$archive.$property) "PMS_EMS_OMS_R021_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Archive unsafe flag detected: $property"
}

Require-True ([bool]$report.paperPositionPreviewReportCreated) "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_REPORT_MISSING" "Operator report missing."
foreach ($property in @("includesPaperPositionPreviewOnlyDisclaimer", "includesSimulatedOnlyDisclaimer", "includesNoLivePositionMutationDisclaimer", "includesNoBrokerPositionMutationDisclaimer", "includesNoTradingStateMutationDisclaimer", "includesNoFillDisclaimer", "includesNoExecutionReportDisclaimer", "includesNoOrderDisclaimer", "includesNoBrokerRouteDisclaimer", "includesNoSubmissionDisclaimer")) {
    Require-True ([bool]$report.$property) "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_REPORT_MISSING" "Report disclaimer missing: $property"
}
$reportMarkdown = Get-Content -LiteralPath (Join-Path $artifactRoot "phase-pms-ems-oms-r021-paper-position-preview-report.md") -Raw
foreach ($text in @("Paper position preview only", "Simulated-only", "no live position mutation", "no broker position mutation", "no trading-state mutation", "no fills", "no execution reports", "no orders", "no broker routes", "no submissions")) {
    if ($reportMarkdown -notmatch [regex]::Escape($text)) {
        Fail-Gate "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_REPORT_MISSING" "Report disclaimer missing: $text"
    }
}

Require-True ([bool]$lines.paperPositionPreviewLinesArchived) "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING" "Preview lines missing."
Require-True ([int]$lines.lineCount -eq 3) "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING" "Preview line count wrong."
Require-True (@($lines.lines | Where-Object { $_.normalizedSymbol -eq "AUDUSD" -and $_.side -eq "Buy" -and [decimal]$_.simulatedPositionDelta -eq 131000 -and $_.quantityCurrency -eq "AUD" }).Count -eq 1) "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING" "AUDUSD preview line missing."
Require-True (@($lines.lines | Where-Object { $_.normalizedSymbol -eq "EURUSD" -and $_.side -eq "Buy" -and [decimal]$_.simulatedPositionDelta -eq 124000 -and $_.quantityCurrency -eq "EUR" }).Count -eq 1) "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING" "EURUSD preview line missing."
Require-True (@($lines.lines | Where-Object { $_.normalizedSymbol -eq "GBPUSD" -and $_.side -eq "Sell" -and [decimal]$_.simulatedPositionDelta -eq -368000 -and $_.quantityCurrency -eq "GBP" }).Count -eq 1) "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING" "GBPUSD preview line missing."
foreach ($line in $lines.lines) {
    foreach ($property in @("paperOnly", "simulatedOnly", "noLivePositionMutation", "noBrokerPositionMutation", "noTradingStateMutation", "noFillCreated", "noExecutionReportCreated", "noOrderCreated")) {
        Require-True ([bool]$line.$property) "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING" "Line safety flag missing: $property"
    }
}

Require-True ([bool]$livePositionAudit.noLivePositionMutationAuditCreated) "PMS_EMS_OMS_R021_FAIL_LIVE_POSITION_MUTATION" "No-live-position audit missing."
Require-False ([bool]$livePositionAudit.livePositionStateMutated) "PMS_EMS_OMS_R021_FAIL_LIVE_POSITION_MUTATION" "Live position mutated."
Require-False ([bool]$livePositionAudit.paperPreviewMutatesLivePositions) "PMS_EMS_OMS_R021_FAIL_LIVE_POSITION_MUTATION" "Paper preview mutates live positions."
Require-False ([bool]$livePositionAudit.archiveMutatesLivePositions) "PMS_EMS_OMS_R021_FAIL_LIVE_POSITION_MUTATION" "Archive mutates live positions."
Require-True ([bool]$brokerPositionAudit.noBrokerPositionMutationAuditCreated) "PMS_EMS_OMS_R021_FAIL_BROKER_POSITION_MUTATION" "No-broker-position audit missing."
Require-False ([bool]$brokerPositionAudit.brokerPositionStateMutated) "PMS_EMS_OMS_R021_FAIL_BROKER_POSITION_MUTATION" "Broker position mutated."
Require-False ([bool]$brokerPositionAudit.paperPreviewMutatesBrokerPositions) "PMS_EMS_OMS_R021_FAIL_BROKER_POSITION_MUTATION" "Paper preview mutates broker positions."
Require-False ([bool]$brokerPositionAudit.archiveMutatesBrokerPositions) "PMS_EMS_OMS_R021_FAIL_BROKER_POSITION_MUTATION" "Archive mutates broker positions."
Require-True ([bool]$tradingAudit.noTradingStateMutationAuditCreated) "PMS_EMS_OMS_R021_FAIL_TRADING_STATE_MUTATION" "No-trading-state audit missing."
Require-False ([bool]$tradingAudit.tradingStateMutated) "PMS_EMS_OMS_R021_FAIL_TRADING_STATE_MUTATION" "Trading state mutated."
Require-False ([bool]$tradingAudit.liveTradingStateMutated) "PMS_EMS_OMS_R021_FAIL_TRADING_STATE_MUTATION" "Live trading state mutated."
Require-False ([bool]$tradingAudit.archiveMutatesTradingState) "PMS_EMS_OMS_R021_FAIL_TRADING_STATE_MUTATION" "Archive mutates trading state."

Require-True ([bool]$fillAudit.noFillNoExecutionReportAuditCreated) "PMS_EMS_OMS_R021_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill/report audit missing."
foreach ($property in @("fillCreated", "executionReportCreated", "simulationFillCreatedAsRealFill", "brokerExecutionReportCreated", "archiveCreatesFillOrExecutionReport", "reportCreatesFillOrExecutionReport")) {
    Require-False ([bool]$fillAudit.$property) "PMS_EMS_OMS_R021_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Fill/report audit detected: $property"
}
Require-True ([bool]$orderAudit.noOrderCreatedAuditCreated) "PMS_EMS_OMS_R021_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "No-order audit missing."
foreach ($property in @("omsOrderCreated", "parentOrderCreated", "childOrderCreated", "brokerOrderCreated", "executableOrderCreated", "orderStateCreated", "orderSubmissionPathIntroduced", "ordersSubmitted")) {
    Require-False ([bool]$orderAudit.$property) "PMS_EMS_OMS_R021_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order audit detected: $property"
}
Require-True ([bool]$routeAudit.noRouteNoSubmissionAuditCreated) "PMS_EMS_OMS_R021_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "No-route/no-submission audit missing."
foreach ($property in @("brokerRouteCreated", "brokerRouteAssigned", "submissionInstructionCreated", "orderSubmissionPathInvoked", "paperPreviewSubmitted", "paperPreviewRouteable", "archiveSubmitted", "archiveRouteable")) {
    Require-False ([bool]$routeAudit.$property) "PMS_EMS_OMS_R021_FAIL_ORDER_SUBMISSION_PATH_INTRODUCED" "Route/submission audit detected: $property"
}

Require-True ([bool]$idempotency.idempotencyEvidenceCreated) "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING" "Idempotency missing."
Require-True ([string]$idempotency.idempotencyKey -eq "PaperPositionPreviewId") "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING" "Wrong idempotency key."
Require-True ([string]$idempotency.duplicateArchiveBehavior -eq "DuplicateReturned") "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING" "Duplicate behavior missing."
Require-False ([bool]$idempotency.duplicatesCreateAdditionalArchives) "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING" "Duplicate archives created."

Require-True ([bool]$risk.riskLineagePreservationCreated) "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING" "Risk lineage missing."
Require-False ([bool]$risk.riskLineageMissing) "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING" "Risk lineage marked missing."
Require-True ([bool]$qubes.qubesLineagePreservationCreated) "PMS_EMS_OMS_R021_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage missing."
Require-True ([string]$qubes.sourceSystem -eq "ModelWeightSourceSystem.Qubes") "PMS_EMS_OMS_R021_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes source missing."
Require-True ([int]$qubes.cadenceMinutes -eq 15) "PMS_EMS_OMS_R021_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes cadence missing."
Require-False ([bool]$qubes.qubesLineageWeakened) "PMS_EMS_OMS_R021_FAIL_QUBES_LINEAGE_WEAKENED" "Qubes lineage weakened."
foreach ($lineage in @($operatorLineage, $simulationLineage, $planLineage, $candidateLineage, $rebalanceLineage, $lotSizingLineage)) {
    $created = $lineage.PSObject.Properties.Name | Where-Object { $_ -match "Created$" } | Select-Object -First 1
    if ($created) { Require-True ([bool]$lineage.$created) "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING" "Lineage artifact missing: $created" }
    $missing = $lineage.PSObject.Properties.Name | Where-Object { $_ -match "Missing$" } | Select-Object -First 1
    if ($missing) { Require-False ([bool]$lineage.$missing) "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING" "Lineage marked missing: $missing" }
}
Require-True ([bool]$rebalanceLineage.rebalanceIntentsRemainNonExecutable) "PMS_EMS_OMS_R021_FAIL_EXECUTABLE_ORDER_CREATED" "Rebalance intents executable."
Require-False ([bool]$rebalanceLineage.rebalanceIntentCreatesOrder) "PMS_EMS_OMS_R021_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Rebalance intent creates order."

Require-True ([bool]$universe.instrumentUniverseHandlingCreated) "PMS_EMS_OMS_R021_FAIL_LMAX_GAP_BLOCKS_POSITION_PREVIEW_REPORT" "Instrument universe handling missing."
Require-False ([bool]$universe.lmaxLiveValidationGapsBlockPaperPositionPreviewArchiveReport) "PMS_EMS_OMS_R021_FAIL_LMAX_GAP_BLOCKS_POSITION_PREVIEW_REPORT" "LMAX gaps block archive/report."
Require-False ([bool]$universe.audusdClassifiedAsFailed) "PMS_EMS_OMS_R021_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD classified as failed."
Require-False ([bool]$universe.audusdLiveValidationGapBlocksPaperPositionPreviewArchiveReport) "PMS_EMS_OMS_R021_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD gap blocks archive/report."
Require-False ([bool]$universe.usdjpyLiveValidationGapBlocksPaperPositionPreviewArchiveReport) "PMS_EMS_OMS_R021_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY gap blocks archive/report."
Require-True ([bool]$usdjpy.usdJpyCaveatPreservationCreated) "PMS_EMS_OMS_R021_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat missing."
Require-True ([string]$usdjpy.usdJpySecurityId -eq "4004") "PMS_EMS_OMS_R021_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityID missing."
Require-True ([string]$usdjpy.usdJpySecurityIdSource -eq "8") "PMS_EMS_OMS_R021_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY SecurityIDSource missing."
Require-False ([bool]$usdjpy.usdJpyClassifiedAsFailed) "PMS_EMS_OMS_R021_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY classified as failed."
Require-False ([bool]$usdjpy.audusdClassifiedAsFailed) "PMS_EMS_OMS_R021_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD classified as failed."
Require-True ([bool]$lmax.lmaxReadonlyBaselineReferenceCreated) "PMS_EMS_OMS_R021_FAIL_LMAX_GAP_BLOCKS_POSITION_PREVIEW_REPORT" "LMAX baseline missing."
Require-False ([bool]$lmax.lmaxUsedInThisPhase) "PMS_EMS_OMS_R021_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX used."
Require-False ([bool]$lmax.lmaxCalledInThisPhase) "PMS_EMS_OMS_R021_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX called."
Require-False ([bool]$lmax.lmaxLiveValidationGapsBlockPaperPositionPreviewArchiveReport) "PMS_EMS_OMS_R021_FAIL_LMAX_GAP_BLOCKS_POSITION_PREVIEW_REPORT" "LMAX gaps block archive/report."

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
    "paperPreviewMutatesTradingState",
    "lmaxLiveValidationGapsBlockPaperPositionPreviewArchiveReport"
)) {
    Require-False ([bool]$noExternal.$property) "PMS_EMS_OMS_R021_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected: $property"
}

foreach ($item in $forbidden.forbiddenActions) {
    if ([bool]$item.detected) {
        if ([string]$item.action -match "scheduler|service|timer|background") {
            Fail-Gate "PMS_EMS_OMS_R021_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "position state") {
            if ([string]$item.action -match "broker") { Fail-Gate "PMS_EMS_OMS_R021_FAIL_BROKER_POSITION_MUTATION" "Forbidden action detected: $($item.action)" }
            Fail-Gate "PMS_EMS_OMS_R021_FAIL_LIVE_POSITION_MUTATION" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "trading state") {
            Fail-Gate "PMS_EMS_OMS_R021_FAIL_TRADING_STATE_MUTATION" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "fill|execution report") {
            Fail-Gate "PMS_EMS_OMS_R021_FAIL_FILL_OR_EXECUTION_REPORT_CREATED" "Forbidden action detected: $($item.action)"
        }
        if ([string]$item.action -match "order|trading|executable|OMS|parent|child|broker|submitted|routed") {
            Fail-Gate "PMS_EMS_OMS_R021_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden action detected: $($item.action)"
        }

        Fail-Gate "PMS_EMS_OMS_R021_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $($item.action)"
    }
}

Require-True ([bool]$nextPhase.nextPhaseRecommendationCreated) "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING" "Next phase missing."
Require-True ([string]$nextPhase.recommendedNextPhase -eq "PMS-EMS-OMS-R022") "PMS_EMS_OMS_R021_FAIL_POSITION_PREVIEW_ARCHIVE_MISSING" "Next phase is not R022."
Require-True ([bool]$nextPhase.mustRemainNoExternal) "PMS_EMS_OMS_R021_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Next phase not no-external."

$apiSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Api/appsettings.json") "PMS_EMS_OMS_R021_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
$workerSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/appsettings.json") "PMS_EMS_OMS_R021_FAIL_NEW_EXTERNAL_ACTION_DETECTED"
Require-False ([bool]$apiSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R021_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API live trading enabled."
Require-False ([bool]$workerSettings.Safety.AllowLiveTrading) "PMS_EMS_OMS_R021_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker live trading enabled."
Require-False ([bool]$apiSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R021_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "API external enabled."
Require-False ([bool]$workerSettings.Safety.AllowExternalConnections) "PMS_EMS_OMS_R021_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker external enabled."
Require-True ([bool]$apiSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R021_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "API fake gateway not required."
Require-True ([bool]$workerSettings.Safety.RequireFakeExecutionGateway) "PMS_EMS_OMS_R021_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Worker fake gateway not required."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.Enabled) "PMS_EMS_OMS_R021_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX runtime enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowExternalConnections) "PMS_EMS_OMS_R021_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX external enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.AllowOrderSubmission) "PMS_EMS_OMS_R021_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "LMAX order submission enabled."
Require-False ([bool]$apiSettings.LmaxReadOnlyRuntime.SchedulerEnabled) "PMS_EMS_OMS_R021_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "LMAX scheduler enabled."
Require-False ([bool]$workerSettings.MarketDataBars.Enabled) "PMS_EMS_OMS_R021_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Worker market data bars enabled."

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-pms-ems-oms-r021-*" -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combined = [string]::Join("`n", $artifactText)
foreach ($pattern in @("\u0001", "35=", "MDReqID\s*[:=]", "SenderCompID\s*[:=]", "TargetCompID\s*[:=]", "BeginString\s*[:=]", "SocketHost\s*[:=]", "TlsHost\s*[:=]", "Password\s*[:=]", "ApiKey\s*[:=]", "Bearer\s+[A-Za-z0-9_\.-]+", "rawBid", "rawAsk", "rawMid")) {
    if ($combined -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R021_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Unsafe serialized content pattern found: $pattern"
    }
}

$requiredFiles = @(
    "src/QQ.Production.Intraday.Application/QubesPaperPositionPreviewArchive.cs",
    "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperPositionPreviewArchiveTests.cs"
)
foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $file))) {
        Fail-Gate "PMS_EMS_OMS_R021_FAIL_BUILD_OR_TESTS" "Required file missing: $file"
    }
}

$source = Get-Content -LiteralPath (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/QubesPaperPositionPreviewArchive.cs") -Raw
foreach ($pattern in @("TcpClient", "SslStream", "MarketDataRequest", "MarketDataResponse", "SendOrderAsync", "SubmitOrder", "FixSession", "CreateOrderAsync")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R021_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "R021 source contains forbidden runtime pattern: $pattern"
    }
}
foreach ($pattern in @("AddHostedService", "IHostedService", "BackgroundService", "PeriodicTimer", "Task.Delay", "System.Threading.Timer")) {
    if ($source -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_R021_FAIL_SCHEDULER_OR_SERVICE_INTRODUCED" "R021 source contains scheduler/service pattern: $pattern"
    }
}

$tests = Get-Content -LiteralPath (Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/QubesPaperPositionPreviewArchiveTests.cs") -Raw
foreach ($requiredTestName in @(
    "R020_paper_position_preview_can_be_archived_no_externally",
    "Archive_preserves_paper_position_preview_id",
    "Archive_preserves_paper_simulation_result_id",
    "Archive_preserves_paper_simulation_plan_id",
    "Archive_preserves_paper_execution_plan_id",
    "Archive_preserves_cycle_run_id_and_qubes_run_id",
    "Archive_preserves_operator_decision_id",
    "Preview_lines_include_audusd_delta",
    "Preview_lines_include_eurusd_delta",
    "Preview_lines_include_gbpusd_sell_delta",
    "Operator_report_includes_paper_only_and_simulated_only_disclaimer",
    "Operator_report_includes_no_live_position_mutation_disclaimer",
    "Operator_report_includes_no_broker_position_mutation_disclaimer",
    "Operator_report_includes_no_trading_state_mutation_disclaimer",
    "Operator_report_includes_no_order_fill_report_route_or_submission_disclaimer",
    "Duplicate_preview_archive_handling_is_idempotent",
    "No_live_position_state_is_mutated",
    "No_broker_position_state_is_mutated",
    "No_trading_state_is_mutated",
    "No_fills_are_created",
    "No_execution_reports_are_created",
    "No_oms_or_broker_orders_are_created",
    "No_order_submission_path_is_introduced",
    "Archive_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action",
    "Api_and_worker_live_gateway_remain_disabled",
    "Archive_source_introduces_no_scheduler_timer_polling_or_background_job",
    "Audusd_is_not_misclassified_as_failed",
    "Usdjpy_caveat_remains_preserved"
)) {
    if ($tests -notmatch [regex]::Escape($requiredTestName)) {
        Fail-Gate "PMS_EMS_OMS_R021_FAIL_BUILD_OR_TESTS" "Focused test missing: $requiredTestName"
    }
}

Require-True ([bool]$evidence.buildTestValidatorEvidenceCreated) "PMS_EMS_OMS_R021_FAIL_BUILD_OR_TESTS" "Evidence marker missing."
Require-True ([string]$evidence.build.status -eq "PASS") "PMS_EMS_OMS_R021_FAIL_BUILD_OR_TESTS" "Build evidence missing or not PASS."
Require-True ([int]$evidence.build.failed -eq 0) "PMS_EMS_OMS_R021_FAIL_BUILD_OR_TESTS" "Build evidence has failures."
Require-True ([string]$evidence.focusedTests.status -eq "PASS") "PMS_EMS_OMS_R021_FAIL_BUILD_OR_TESTS" "Focused test evidence missing or not PASS."
Require-True ([int]$evidence.focusedTests.failed -eq 0) "PMS_EMS_OMS_R021_FAIL_BUILD_OR_TESTS" "Focused tests have failures."
Require-True ([string]$evidence.unitTests.status -eq "PASS") "PMS_EMS_OMS_R021_FAIL_BUILD_OR_TESTS" "Unit test evidence missing or not PASS."
Require-True ([int]$evidence.unitTests.failed -eq 0) "PMS_EMS_OMS_R021_FAIL_BUILD_OR_TESTS" "Unit tests have failures."
Require-True ([string]$evidence.validator.status -eq "PASS") "PMS_EMS_OMS_R021_FAIL_BUILD_OR_TESTS" "Validator evidence missing or not PASS."
Require-True ([int]$evidence.validator.failed -eq 0) "PMS_EMS_OMS_R021_FAIL_BUILD_OR_TESTS" "Validator evidence has failures."

Write-Host "PMS_EMS_OMS_R021_PASS_PAPER_POSITION_PREVIEW_ARCHIVE_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_R021_PASS_OPERATOR_POSITION_PREVIEW_REPORT_READY_NO_EXTERNAL"
