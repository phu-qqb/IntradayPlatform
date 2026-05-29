param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Error "LEDGER_AUDIT_R001_GATE_FAIL: $Message"
    exit 1
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing required artifact: $Path"
    }

    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

$auditRoot = Join-Path $Root "artifacts/readiness/ledger-state-audit"
$required = @(
    "phase-ledger-audit-r001-summary.md",
    "phase-ledger-audit-r001-exec-sandbox-evidence-inventory.json",
    "phase-ledger-audit-r001-existing-lmax-report-model-audit.json",
    "phase-ledger-audit-r001-fill-report-model-audit.json",
    "phase-ledger-audit-r001-oms-lifecycle-evidence-map.json",
    "phase-ledger-audit-r001-reconciliation-evidence-map.json",
    "phase-ledger-audit-r001-ledger-field-mapping-matrix.json",
    "phase-ledger-audit-r001-ledger-preview-readiness-assessment.json",
    "phase-ledger-audit-r001-commit-separation-audit.json",
    "phase-ledger-audit-r001-marketdata-vs-execution-report-boundary.json",
    "phase-ledger-audit-r001-pms-qubes-required-fields-for-ledger.json",
    "phase-ledger-audit-r001-marketdata-required-fields-for-ledger.json",
    "phase-ledger-audit-r001-exec-sandbox-do-not-redo-list.json",
    "phase-ledger-audit-r001-gap-list.json",
    "phase-ledger-audit-r001-next-ledger-phase-recommendation.json",
    "phase-ledger-audit-r001-no-external-audit.json",
    "phase-ledger-audit-r001-no-execution-audit.json",
    "phase-ledger-audit-r001-no-db-mutation-audit.json",
    "phase-ledger-audit-r001-no-ledger-commit-audit.json",
    "phase-ledger-audit-r001-forbidden-actions-audit.json",
    "phase-ledger-audit-r001-build-test-validator-evidence.json"
)

foreach ($file in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $auditRoot $file))) {
        Fail "Missing required LEDGER-AUDIT-R001 artifact: $file"
    }
}

$inventory = Read-Json (Join-Path $auditRoot "phase-ledger-audit-r001-exec-sandbox-evidence-inventory.json")
$reportAudit = Read-Json (Join-Path $auditRoot "phase-ledger-audit-r001-existing-lmax-report-model-audit.json")
$fillAudit = Read-Json (Join-Path $auditRoot "phase-ledger-audit-r001-fill-report-model-audit.json")
$lifecycle = Read-Json (Join-Path $auditRoot "phase-ledger-audit-r001-oms-lifecycle-evidence-map.json")
$mapping = Read-Json (Join-Path $auditRoot "phase-ledger-audit-r001-ledger-field-mapping-matrix.json")
$readiness = Read-Json (Join-Path $auditRoot "phase-ledger-audit-r001-ledger-preview-readiness-assessment.json")
$commit = Read-Json (Join-Path $auditRoot "phase-ledger-audit-r001-commit-separation-audit.json")
$boundary = Read-Json (Join-Path $auditRoot "phase-ledger-audit-r001-marketdata-vs-execution-report-boundary.json")
$noExternal = Read-Json (Join-Path $auditRoot "phase-ledger-audit-r001-no-external-audit.json")
$noExecution = Read-Json (Join-Path $auditRoot "phase-ledger-audit-r001-no-execution-audit.json")
$noDb = Read-Json (Join-Path $auditRoot "phase-ledger-audit-r001-no-db-mutation-audit.json")
$noLedger = Read-Json (Join-Path $auditRoot "phase-ledger-audit-r001-no-ledger-commit-audit.json")
$forbidden = Read-Json (Join-Path $auditRoot "phase-ledger-audit-r001-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $auditRoot "phase-ledger-audit-r001-build-test-validator-evidence.json")

if (-not ($inventory.inventory | Where-Object { $_.sourcePhase -eq "EXEC-SANDBOX-R007" -and $_.evidenceType -match "fill" })) {
    Fail "Existing R007 sandbox fill evidence was ignored."
}

if (-not ($inventory.inventory | Where-Object { $_.sourcePhase -eq "EXEC-SANDBOX-R008" -and $_.evidenceType -match "Flatten" })) {
    Fail "Existing R008 flatten evidence was ignored."
}

if (-not ($inventory.inventory | Where-Object { $_.sourcePhase -eq "EXEC-SANDBOX-R010" -and $_.evidenceType -match "OMS" })) {
    Fail "Existing R010 OMS handoff evidence was ignored."
}

if ($reportAudit.auditedFields.ExecType -ne "Present" -or $reportAudit.auditedFields.OrdStatus -ne "Present" -or $reportAudit.auditedFields.ClOrdID -ne "Present") {
    Fail "LMAX execution report core fields not inventoried as present."
}

if ($fillAudit.auditedFields.Quantity -ne "Present" -or $fillAudit.auditedFields.Price -ne "Present") {
    Fail "Fill quantity/price evidence not inventoried as present."
}

if ($lifecycle.verified.r007OpenLifecycleEvidenceExists -ne $true -or $lifecycle.verified.r008FlattenLifecycleEvidenceExists -ne $true -or $lifecycle.verified.r009RepeatabilityEvidenceExists -ne $true -or $lifecycle.verified.residualZeroEvidenceExists -ne $true) {
    Fail "Lifecycle evidence verification incomplete."
}

if ($readiness.assessment -ne "ReadyWithMissingEconomicFields") {
    Fail "Ledger preview readiness must be ReadyWithMissingEconomicFields."
}

if ($readiness.decision -ne "LedgerR001CanProceedWithExistingExecutionReportEvidence") {
    Fail "Incorrect R001 recommendation."
}

if ($commit.confirmed.sandboxFillCanMutatePaperLedger -or $commit.confirmed.sandboxFillCanMutateProductionLedger -or $commit.confirmed.sandboxFillCanMutateProductionState -or -not $commit.confirmed.paperLedgerCommitRequiresSeparateFutureGate) {
    Fail "Commit separation was weakened."
}

if ($boundary.latestMarketDataStatus.'lmax-marketdata-db.v1' -ne "WITH_WARNINGS" -or $boundary.latestMarketDataStatus.'marketdata-readiness.v1' -ne "WITH_WARNINGS") {
    Fail "MarketData WARN status was not preserved."
}

if ($boundary.latestMarketDataStatus.m30Evidence -ne "MISSING_CONFIRMED" -or $boundary.latestMarketDataStatus.rowCounts -ne "MISSING") {
    Fail "MarketData missing evidence was not preserved."
}

if ($noExternal.externalApiCalled -or $noExternal.lmaxCalled -or $noExternal.polygonCalled -or $noExternal.brokerActivated -or $noExternal.liveMarketDataRequested -or $noExternal.credentialValuesPersisted) {
    Fail "No-external audit indicates forbidden action."
}

if ($noExecution.pmsEmsOmsCycleRun -or $noExecution.manualNoExternalRun -or $noExecution.qubesRun -or $noExecution.pythonCppCudaWorkloadRun -or $noExecution.backtestOrSimulationRun -or $noExecution.ordersCreated -or $noExecution.routesCreated -or $noExecution.submissionsCreated -or $noExecution.fillsCreated -or $noExecution.executionReportsCreated -or $noExecution.executableSchedulesCreated) {
    Fail "No-execution audit indicates forbidden creation or workload."
}

if ($noDb.dbMutationOccurred -or $noDb.migrationCreated -or $noDb.migrationApplied -or $noDb.sqlMutationOccurred) {
    Fail "No-DB-mutation audit indicates forbidden DB action."
}

if ($noLedger.paperLedgerCommitOccurred -or $noLedger.productionLedgerCommitOccurred -or $noLedger.paperPositionMutationOccurred -or $noLedger.productionPositionMutationOccurred -or $noLedger.cashStateMutationOccurred -or $noLedger.tradingStateMutationOccurred -or $noLedger.paperLedgerPreviewMisclassifiedAsCommit -or $noLedger.sandboxFillAllowedToMutateLedger) {
    Fail "No-ledger-commit audit indicates forbidden mutation."
}

$fa = $forbidden.forbiddenActions
if ($fa.externalApiLmaxPolygonCalled -or $fa.brokerActivation -or $fa.liveMarketDataRequested -or $fa.pmsEmsOmsCycleRun -or $fa.manualNoExternalRun -or $fa.qubesPythonCppCudaWorkloadRun -or $fa.dbMutation -or $fa.migrationCreatedOrApplied -or $fa.orderRouteSubmissionFillExecutionReportCreated -or $fa.ledgerCommit -or $fa.productionLedgerCommit -or $fa.tradingStateMutation -or $fa.sandboxFillAllowedToMutateLedger -or $fa.paperLedgerPreviewMisclassifiedAsCommit -or $fa.legacy06UsedAsFutureCanonical -or $fa.directCrossExecutionAllowed -or $fa.usdjpyCaveatWeakened -or $fa.existingLmaxExecutionReportEvidenceIgnored -or $fa.marketDataDbReadinessFalselyClaimedComplete) {
    Fail "Forbidden-actions audit indicates a forbidden action or weakened blocker."
}

if ($evidence.build.result -notin @("Passed", "PassedWithWarnings")) {
    Fail "Build evidence missing or not passing."
}

if ($evidence.validator.result -notin @("Passed", "Pending")) {
    Fail "Validator evidence invalid."
}

$text = Get-ChildItem -LiteralPath $auditRoot -File -Filter "phase-ledger-audit-r001-*" |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }

if (($text -join "`n") -match "LMAX_DEMO_(FIX|MD)_(USERNAME|PASSWORD|SENDER_COMP_ID|TARGET_COMP_ID)\s*[:=]\s*['""][^'""]+['""]") {
    Fail "Possible credential value persisted in ledger audit artifacts."
}

Write-Output "LEDGER_AUDIT_R001_GATE_PASS_EXISTING_LMAX_REPORT_INVENTORY_READY"
