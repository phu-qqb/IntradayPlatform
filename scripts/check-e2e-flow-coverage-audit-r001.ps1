param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$AllowedClassifications = @("REAL_CONFIRMED", "SANDBOX_CONFIRMED", "SYNTHETIC_FIXTURE_ONLY", "PREVIEW_ONLY", "BLOCKED", "NOT_FOUND", "AMBIGUOUS")
$RequiredStages = @(
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

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\e2e-flow-coverage-audit-r001"
$AuditPath = Join-Path $ArtifactDir "e2e-flow-coverage-audit-r001.json"
$SummaryPath = Join-Path $ArtifactDir "e2e-flow-coverage-audit-summary-r001.md"
$PostCommitPath = Join-Path $RepoRoot "artifacts\readiness\sandbox-ledger-db-post-commit-closeout-and-production-go-no-go-r001\sandbox-ledger-db-post-commit-closeout-r001.json"
$BrokerPnlPath = Join-Path $RepoRoot "artifacts\readiness\broker-statement-confirmed-pnl-r001\broker-statement-confirmed-pnl-r001.json"
$AccountingClosePath = Join-Path $RepoRoot "artifacts\readiness\real-accounting-evidence-and-close-acceptance-r001\real-accounting-evidence-and-close-acceptance-r001.json"

foreach ($path in @($AuditPath, $SummaryPath, $PostCommitPath, $BrokerPnlPath, $AccountingClosePath)) {
    Assert-True (Test-Path -LiteralPath $path) "Required artifact missing: $path"
}

$audit = Read-JsonFile $AuditPath
$postCommit = Read-JsonFile $PostCommitPath
$brokerPnl = Read-JsonFile $BrokerPnlPath
$accountingClose = Read-JsonFile $AccountingClosePath

Assert-Equal $audit.package "E2E_FLOW_COVERAGE_AUDIT_R001" "Audit package mismatch."
Assert-Equal $audit.status "E2E_FLOW_COVERAGE_AUDIT_COMPLETE_R001" "Audit status mismatch."

foreach ($stage in $RequiredStages) {
    $node = $audit.flow_coverage.$stage
    Assert-True ($null -ne $node) "Missing flow stage: $stage"
    Assert-True ($AllowedClassifications -contains $node.classification) "Invalid classification for $stage"
    Assert-True ($null -ne $node.evidence) "Missing evidence list for $stage"
    Assert-True ($null -ne $node.gaps) "Missing gaps list for $stage"
}

Assert-Equal $brokerPnl.status "BROKER_STATEMENT_CONFIRMED_PNL_READY_R001" "Broker PnL source status mismatch."
Assert-Equal $accountingClose.status "REAL_ACCOUNTING_EVIDENCE_AND_CLOSE_ACCEPTANCE_READY_R001" "Accounting close source status mismatch."
Assert-Equal $postCommit.status "SANDBOX_LEDGER_DB_POST_COMMIT_CLOSEOUT_READY_R001" "Post-commit closeout source status mismatch."
Assert-Equal $audit.flow_coverage.broker_statement_reconciliation.classification "REAL_CONFIRMED" "Broker statement classification mismatch."
Assert-Equal $audit.flow_coverage.accounting_close.classification "REAL_CONFIRMED" "Accounting close classification mismatch."
Assert-Equal $audit.flow_coverage.ledger_db_commit.classification "SANDBOX_CONFIRMED" "Ledger/DB classification mismatch."
Assert-Equal $audit.flow_coverage.audit_rollback_idempotency.classification "SANDBOX_CONFIRMED" "Audit/idempotency classification mismatch."
Assert-Equal $audit.flow_coverage.production_live_trading_readiness.classification "BLOCKED" "Production/trading classification mismatch."

Assert-Equal $audit.summary.front_half_trading_flow_complete $false "Front-half summary must remain incomplete."
Assert-Equal $audit.summary.back_half_broker_accounting_ledger_flow_complete $true "Back-half summary must be complete."
Assert-Equal $audit.summary.full_front_to_back_flow_complete $false "Full flow summary must remain incomplete."
Assert-Equal $audit.summary.production_live_ready $false "Production/live must remain false."
Assert-Equal $audit.summary.trading_ready $false "Trading ready must remain false."

Assert-Equal $postCommit.readiness.production_live_ready $false "Post-commit production/live must remain false."
Assert-Equal $postCommit.readiness.trading_readiness $false "Post-commit trading readiness must remain false."
Assert-Equal $postCommit.global_guards.trading_activity $false "Trading activity guard mismatch."
Assert-Equal $postCommit.global_guards.lmax_fix_api_call $false "LMAX FIX/API guard mismatch."
Assert-Equal $postCommit.global_guards.broker_api_call $false "Broker API guard mismatch."
Assert-Equal $postCommit.global_guards.market_data_fetch $false "Market-data fetch guard mismatch."
Assert-Equal $postCommit.global_guards.broker_fetch $false "Broker fetch guard mismatch."
Assert-Equal $postCommit.global_guards.account_data_fetch $false "Account fetch guard mismatch."

Assert-Equal $audit.global_guards.no_trading $true "Audit no-trading guard mismatch."
Assert-Equal $audit.global_guards.no_lmax_fix_api_call $true "Audit no-LMAX guard mismatch."
Assert-Equal $audit.global_guards.no_broker_api_call $true "Audit no-broker-API guard mismatch."
Assert-Equal $audit.global_guards.no_market_data_fetch $true "Audit no-market-data-fetch guard mismatch."
Assert-Equal $audit.global_guards.no_production_live $true "Audit no-production-live guard mismatch."
Assert-Equal $audit.global_guards.no_db_mutation_by_audit $true "Audit must not mutate DB."
Assert-Equal $audit.global_guards.no_ledger_commit_by_audit $true "Audit must not commit ledger."

Write-Host "E2E_FLOW_COVERAGE_AUDIT_R001_GATE_PASS"
