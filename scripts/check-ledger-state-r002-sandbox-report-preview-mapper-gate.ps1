param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Error "LEDGER_STATE_R002_GATE_FAIL: $Message"
    exit 1
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing required artifact: $Path"
    }

    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

$auditRoot = Join-Path $Root "artifacts/readiness/ledger-state"
$required = @(
    "phase-ledger-state-r002-summary.md",
    "phase-ledger-state-r002-ledger-audit-r001-reference.json",
    "phase-ledger-state-r002-selected-sandbox-report-evidence.json",
    "phase-ledger-state-r002-mapper-contract.json",
    "phase-ledger-state-r002-field-mapping-results.json",
    "phase-ledger-state-r002-paper-ledger-preview-lines.json",
    "phase-ledger-state-r002-hypothetical-position-deltas.json",
    "phase-ledger-state-r002-hypothetical-exposure-preview.json",
    "phase-ledger-state-r002-hypothetical-cash-impact.json",
    "phase-ledger-state-r002-cash-impact-diagnostics.json",
    "phase-ledger-state-r002-commit-blockers.json",
    "phase-ledger-state-r002-idempotency-results.json",
    "phase-ledger-state-r002-preview-reconciliation.json",
    "phase-ledger-state-r002-decision.json",
    "phase-ledger-state-r002-no-db-mutation-audit.json",
    "phase-ledger-state-r002-no-ledger-commit-audit.json",
    "phase-ledger-state-r002-no-production-ledger-audit.json",
    "phase-ledger-state-r002-no-trading-state-mutation-audit.json",
    "phase-ledger-state-r002-no-order-fill-route-audit.json",
    "phase-ledger-state-r002-canonical-timing-preservation.json",
    "phase-ledger-state-r002-direct-cross-exclusion-preservation.json",
    "phase-ledger-state-r002-usdjpy-caveat-preservation.json",
    "phase-ledger-state-r002-marketdata-execreport-boundary-preservation.json",
    "phase-ledger-state-r002-forbidden-actions-audit.json",
    "phase-ledger-state-r002-next-phase-recommendation.json",
    "phase-ledger-state-r002-build-test-validator-evidence.json"
)

foreach ($file in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $auditRoot $file))) {
        Fail "Missing required LEDGER-STATE-R002 artifact: $file"
    }
}

$selected = Read-Json (Join-Path $auditRoot "phase-ledger-state-r002-selected-sandbox-report-evidence.json")
$contract = Read-Json (Join-Path $auditRoot "phase-ledger-state-r002-mapper-contract.json")
$lines = Read-Json (Join-Path $auditRoot "phase-ledger-state-r002-paper-ledger-preview-lines.json")
$cash = Read-Json (Join-Path $auditRoot "phase-ledger-state-r002-hypothetical-cash-impact.json")
$cashDiagnostics = Read-Json (Join-Path $auditRoot "phase-ledger-state-r002-cash-impact-diagnostics.json")
$blockers = Read-Json (Join-Path $auditRoot "phase-ledger-state-r002-commit-blockers.json")
$idempotency = Read-Json (Join-Path $auditRoot "phase-ledger-state-r002-idempotency-results.json")
$recon = Read-Json (Join-Path $auditRoot "phase-ledger-state-r002-preview-reconciliation.json")
$decision = Read-Json (Join-Path $auditRoot "phase-ledger-state-r002-decision.json")
$noDb = Read-Json (Join-Path $auditRoot "phase-ledger-state-r002-no-db-mutation-audit.json")
$noLedger = Read-Json (Join-Path $auditRoot "phase-ledger-state-r002-no-ledger-commit-audit.json")
$noProdLedger = Read-Json (Join-Path $auditRoot "phase-ledger-state-r002-no-production-ledger-audit.json")
$noState = Read-Json (Join-Path $auditRoot "phase-ledger-state-r002-no-trading-state-mutation-audit.json")
$noOrder = Read-Json (Join-Path $auditRoot "phase-ledger-state-r002-no-order-fill-route-audit.json")
$timing = Read-Json (Join-Path $auditRoot "phase-ledger-state-r002-canonical-timing-preservation.json")
$direct = Read-Json (Join-Path $auditRoot "phase-ledger-state-r002-direct-cross-exclusion-preservation.json")
$usdjpy = Read-Json (Join-Path $auditRoot "phase-ledger-state-r002-usdjpy-caveat-preservation.json")
$boundary = Read-Json (Join-Path $auditRoot "phase-ledger-state-r002-marketdata-execreport-boundary-preservation.json")
$forbidden = Read-Json (Join-Path $auditRoot "phase-ledger-state-r002-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $auditRoot "phase-ledger-state-r002-build-test-validator-evidence.json")

if (-not $selected.newOrderRouteSubmissionFillExecutionReportCreated -eq $false) {
    Fail "Selected evidence does not prove no new order/route/fill/report creation."
}

if ($selected.selectedEvidence.openFills.fillCount -ne 7 -or $selected.selectedEvidence.flattenFills.fillCountUsedForPreview -ne 7) {
    Fail "R007/R008 selected evidence counts are invalid."
}

if (-not $contract.rules.reuseExistingEvidenceOnly -or -not $contract.rules.sideMustComeFromExistingEvidence -or $contract.rules.commitAllowed -or $contract.rules.ledgerMutation -or $contract.rules.tradingStateMutation) {
    Fail "Mapper contract weakened preview-only/no-mutation rules."
}

if ($lines.lineCount -ne 14 -or -not $lines.previewOnly -or $lines.commitAllowed -or $lines.ledgerMutation -or $lines.tradingStateMutation) {
    Fail "Preview lines artifact has invalid preview-only flags."
}

foreach ($line in $lines.lines) {
    if (-not $line.sandboxOnly -or $line.productionOrder -or -not $line.noLedgerCommit) {
        Fail "Preview line can mutate or references production order: $($line.lineId)"
    }
    foreach ($missing in @("AccountId", "StrategyId", "PmsCycleId", "QubesRunId")) {
        if ($line.missingFields -notcontains $missing) {
            Fail "Missing field $missing was not preserved on $($line.lineId)"
        }
    }
}

if ($cash.cashImpactStatus -ne "Incomplete" -or $cash.cashImpactInventedWithoutEvidence) {
    Fail "Cash impact must remain incomplete and not invented."
}

if ($cashDiagnostics.safeToComputeLedgerCashImpact -or $cashDiagnostics.ledgerCashImpactInvented) {
    Fail "Cash diagnostics incorrectly allow ledger cash impact."
}

foreach ($blocker in @("MissingAccountId", "MissingStrategyId", "MissingPmsCycleId", "MissingQubesRunId", "MissingCommissionFeeModel", "MissingFxConversionModel", "CommitSafeIdempotencyPolicyIncomplete")) {
    if ($blockers.blockers -notcontains $blocker) {
        Fail "Required commit blocker missing: $blocker"
    }
}

if ($blockers.commitAllowed -or -not $blockers.blockersPreventCommitNotPreview) {
    Fail "Commit blockers do not block commit correctly."
}

if (-not $idempotency.sameSourceFillReportSameInputHashSamePreviewHash -or -not $idempotency.sameSourceFillReportDifferentInputHashConflict -or -not $idempotency.sameFillCannotCreateDuplicateCommitCandidate -or $idempotency.duplicateCommitCandidateCreated) {
    Fail "Idempotency results invalid."
}

if ($recon.openFillCount -ne 7 -or $recon.flattenFillCount -ne 7 -or $recon.previewResidualQuantity -ne 0 -or -not $recon.residualMatchesEvidence -or $recon.ledgerMutation -or $recon.tradingStateMutation) {
    Fail "Preview reconciliation invalid."
}

if ($decision.decision -ne "PaperLedgerPreviewMapperReadyWithEconomicFieldGaps" -or -not $decision.previewOnly -or $decision.commitAllowed -or $decision.ledgerMutation -or $decision.tradingStateMutation) {
    Fail "Decision artifact invalid."
}

if ($noDb.dbMutationOccurred -or $noDb.dbWriteOccurred -or $noDb.migrationCreated -or $noDb.migrationApplied -or $noDb.sqlMutationOccurred) {
    Fail "DB mutation or migration detected."
}

if ($noLedger.paperLedgerCommitOccurred -or $noLedger.productionLedgerCommitOccurred -or $noLedger.existingSandboxFillAllowedToMutateLedger -or $noLedger.paperLedgerPreviewMisclassifiedAsCommit -or $noLedger.ledgerMutation -or $noLedger.paperPositionMutation -or $noLedger.productionPositionMutation -or $noLedger.cashStateMutation) {
    Fail "Ledger commit/mutation detected."
}

if ($noProdLedger.productionLedgerCommitOccurred -or $noProdLedger.productionLedgerWriteOccurred -or $noProdLedger.productionPositionMutationOccurred -or $noProdLedger.productionCashMutationOccurred -or $noProdLedger.productionTradingStateMutationOccurred) {
    Fail "Production ledger/state mutation detected."
}

if ($noState.tradingStateMutationOccurred -or $noState.paperPositionMutationOccurred -or $noState.productionPositionMutationOccurred -or $noState.cashStateMutationOccurred -or $noState.ledgerMutationOccurred) {
    Fail "Trading/position/cash state mutation detected."
}

if ($noOrder.ordersCreated -or $noOrder.routesCreated -or $noOrder.submissionsCreated -or $noOrder.fillsCreated -or $noOrder.executionReportsCreated -or $noOrder.executableSchedulesCreated -or $noOrder.newOrderFillRouteArtifactsCreated -or -not $noOrder.usedExistingSandboxEvidenceOnly) {
    Fail "Order/fill/route/report creation detected."
}

if ($timing.legacy06UsedAsFutureCanonical -or $timing.legacy06213651CompatibilityOnly -ne $true) {
    Fail "Canonical timing policy weakened."
}

if ($direct.directCrossExecutionAllowed -or $direct.directCrossExposureCreatedFromRawQubesSignals) {
    Fail "Direct-cross exclusion weakened."
}

if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or -not $usdjpy.RequiresInversion -or $usdjpy.SecurityID -ne "4004" -or $usdjpy.SecurityIDSource -ne "8" -or $usdjpy.usdjpyCaveatWeakened) {
    Fail "USDJPY caveat weakened."
}

if ($boundary.marketDataDbReadinessFalselyClaimedComplete -or $boundary.latestMarketDataStatus.'lmax-marketdata-db.v1' -ne "WITH_WARNINGS" -or $boundary.latestMarketDataStatus.'marketdata-readiness.v1' -ne "WITH_WARNINGS") {
    Fail "MarketData boundary or WARN status invalid."
}

$fa = $forbidden.forbiddenActions
if ($fa.externalApiLmaxPolygonCalled -or $fa.brokerActivation -or $fa.liveMarketDataRequested -or $fa.pmsEmsOmsCycleRun -or $fa.manualNoExternalRun -or $fa.qubesPythonCppCudaWorkloadRun -or $fa.backtestOrSimulationRun -or $fa.dbMutation -or $fa.migrationCreatedOrApplied -or $fa.orderRouteSubmissionFillExecutionReportCreated -or $fa.ledgerCommit -or $fa.productionLedgerCommit -or $fa.tradingStateMutation -or $fa.existingSandboxFillAllowedToMutateLedger -or $fa.paperLedgerPreviewMisclassifiedAsCommit -or $fa.cashImpactInventedWithoutEvidence -or $fa.missingAccountStrategyPmsQubesInvented -or $fa.legacy06UsedAsFutureCanonical -or $fa.directCrossExecutionAllowed -or $fa.usdjpyCaveatWeakened -or $fa.marketDataDbReadinessFalselyClaimedComplete) {
    Fail "Forbidden action or weakened blocker detected."
}

if ($evidence.build.result -notin @("Passed", "PassedWithWarnings")) {
    Fail "Build evidence missing or not passing."
}

if ($evidence.focusedTests.result -ne "Passed") {
    Fail "Focused test evidence missing or failing."
}

if ($evidence.validator.result -notin @("Passed", "Pending")) {
    Fail "Validator evidence invalid."
}

Write-Output "LEDGER_STATE_R002_GATE_PASS_SANDBOX_REPORT_PREVIEW_MAPPER_READY_NO_MUTATION"
