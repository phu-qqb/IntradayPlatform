param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Assert-Equal($Actual, $Expected, [string]$Message) {
    if ($Actual -ne $Expected) { throw "$Message Expected=[$Expected] Actual=[$Actual]" }
}

function Read-JsonFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Required artifact missing: $Path" }
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

$builder = Join-Path $RepoRoot "scripts\build-e2e-flow-coverage-audit-r001.ps1"
$gate = Join-Path $RepoRoot "scripts\check-e2e-flow-coverage-audit-r001.ps1"

& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot | Out-Null
& powershell -ExecutionPolicy Bypass -File $gate -RepoRoot $RepoRoot | Out-Null

$auditPath = Join-Path $RepoRoot "artifacts\readiness\e2e-flow-coverage-audit-r001\e2e-flow-coverage-audit-r001.json"
$summaryPath = Join-Path $RepoRoot "artifacts\readiness\e2e-flow-coverage-audit-r001\e2e-flow-coverage-audit-summary-r001.md"
$audit = Read-JsonFile $auditPath

$requiredStages = @(
    "market_data",
    "qubes_weight_generation",
    "drift_calculation",
    "order_creation",
    "execution_algorithm",
    "execution_and_fills",
    "trade_level_reconciliation",
    "broker_statement_reconciliation",
    "pnl",
    "accounting_close",
    "ledger_db_commit",
    "audit_rollback_idempotency",
    "production_live_trading_readiness"
)
$allowedClassifications = @("REAL_CONFIRMED", "SANDBOX_CONFIRMED", "SYNTHETIC_FIXTURE_ONLY", "PREVIEW_ONLY", "BLOCKED", "NOT_FOUND", "AMBIGUOUS")

Assert-Equal $audit.status "E2E_FLOW_COVERAGE_AUDIT_COMPLETE_R001" "Audit status mismatch."
foreach ($stage in $requiredStages) {
    Assert-True ($null -ne $audit.flow_coverage.$stage) "Coverage matrix missing stage: $stage"
    Assert-True ($allowedClassifications -contains $audit.flow_coverage.$stage.classification) "Invalid classification for stage: $stage"
}

Assert-Equal $audit.flow_coverage.market_data.classification "AMBIGUOUS" "Market data should remain ambiguous."
Assert-Equal $audit.flow_coverage.qubes_weight_generation.classification "SYNTHETIC_FIXTURE_ONLY" "Qubes weight generation should be fixture-only."
Assert-Equal $audit.flow_coverage.drift_calculation.classification "PREVIEW_ONLY" "Drift should be preview-only."
Assert-Equal $audit.flow_coverage.order_creation.classification "PREVIEW_ONLY" "Order creation should be preview-only."
Assert-Equal $audit.flow_coverage.execution_algorithm.classification "PREVIEW_ONLY" "Execution algorithm should be preview-only."
Assert-Equal $audit.flow_coverage.execution_and_fills.classification "SANDBOX_CONFIRMED" "Execution/fills should be sandbox-confirmed."
Assert-Equal $audit.flow_coverage.trade_level_reconciliation.classification "SANDBOX_CONFIRMED" "Trade-level reconciliation should be sandbox-confirmed."
Assert-Equal $audit.flow_coverage.broker_statement_reconciliation.classification "REAL_CONFIRMED" "Broker statement reconciliation should be real-confirmed."
Assert-Equal $audit.flow_coverage.pnl.classification "REAL_CONFIRMED" "PnL should be real-confirmed."
Assert-Equal $audit.flow_coverage.accounting_close.classification "REAL_CONFIRMED" "Accounting close should be real-confirmed."
Assert-Equal $audit.flow_coverage.ledger_db_commit.classification "SANDBOX_CONFIRMED" "Ledger/DB commit should be sandbox-confirmed."
Assert-Equal $audit.flow_coverage.audit_rollback_idempotency.classification "SANDBOX_CONFIRMED" "Audit/rollback/idempotency should be sandbox-confirmed."
Assert-Equal $audit.flow_coverage.production_live_trading_readiness.classification "BLOCKED" "Production/live/trading must remain blocked."

Assert-Equal $audit.summary.front_half_trading_flow_complete $false "Front half must not be complete."
Assert-Equal $audit.summary.back_half_broker_accounting_ledger_flow_complete $true "Back half must be complete."
Assert-Equal $audit.summary.full_front_to_back_flow_complete $false "Full flow must not be complete."
Assert-Equal $audit.summary.production_live_ready $false "Production/live must remain false."
Assert-Equal $audit.summary.trading_ready $false "Trading readiness must remain false."
Assert-True (-not [string]::IsNullOrWhiteSpace($audit.summary.recommended_next_macro_package)) "Recommended next macro package missing."

Assert-Equal $audit.global_guards.no_trading $true "Audit no-trading guard mismatch."
Assert-Equal $audit.global_guards.no_lmax_fix_api_call $true "Audit no-LMAX guard mismatch."
Assert-Equal $audit.global_guards.no_broker_api_call $true "Audit no-broker-API guard mismatch."
Assert-Equal $audit.global_guards.no_market_data_fetch $true "Audit no-market-data-fetch guard mismatch."
Assert-Equal $audit.global_guards.no_production_live $true "Audit no-production-live guard mismatch."
Assert-Equal $audit.global_guards.no_db_mutation_by_audit $true "Audit must not mutate DB."
Assert-Equal $audit.global_guards.no_ledger_commit_by_audit $true "Audit must not commit ledger."
Assert-True (Test-Path -LiteralPath $summaryPath) "Audit markdown summary missing."

Write-Host "E2E_FLOW_COVERAGE_AUDIT_R001_TEST_PASS"
