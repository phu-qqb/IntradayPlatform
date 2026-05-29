param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

function Assert-Equal($Actual, $Expected, [string]$Message) {
    if ($Actual -ne $Expected) { throw "$Message Expected=[$Expected] Actual=[$Actual]" }
}

function Assert-DecimalEqual([decimal]$Actual, [decimal]$Expected, [string]$Message) {
    if ([Math]::Abs($Actual - $Expected) -gt [decimal]0.000001) {
        throw "$Message Expected=[$Expected] Actual=[$Actual]"
    }
}

function Read-JsonFile([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

$builder = Join-Path $RepoRoot "scripts\build-real-evidence-promotion-and-commit-readiness-gate-r001.ps1"
$gate = Join-Path $RepoRoot "scripts\check-real-evidence-promotion-and-commit-readiness-gate-r001.ps1"

& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot | Out-Null
& powershell -ExecutionPolicy Bypass -File $gate -RepoRoot $RepoRoot | Out-Null

$artifactDir = Join-Path $RepoRoot "artifacts\readiness\real-evidence-promotion-and-commit-readiness-gate-r001"
$main = Read-JsonFile (Join-Path $artifactDir "real-evidence-promotion-and-commit-readiness-gate-r001.json")
$brokerPnl = Read-JsonFile (Join-Path $artifactDir "broker-confirmed-pnl-readiness-requirements-r001.json")
$realizedClose = Read-JsonFile (Join-Path $artifactDir "realized-accounting-close-readiness-requirements-r001.json")
$commit = Read-JsonFile (Join-Path $artifactDir "ledger-db-commit-readiness-requirements-r001.json")
$production = Read-JsonFile (Join-Path $artifactDir "production-live-trading-readiness-requirements-r001.json")

Assert-Equal $main.status "REAL_EVIDENCE_PROMOTION_AND_COMMIT_READINESS_BLOCKED_R001" "Promotion gate must be blocked."
Assert-Equal $main.blocked_reason "NO_REAL_MANUAL_EVIDENCE_IMPORTED" "Blocked reason mismatch."
Assert-DecimalEqual ([decimal]$main.source_values.gross_usd) ([decimal]-50.308800) "Gross USD mismatch."
Assert-DecimalEqual ([decimal]$main.source_values.commission_usd) ([decimal]26.268029) "Commission USD mismatch."
Assert-DecimalEqual ([decimal]$main.source_values.net_usd) ([decimal]-76.576829) "Net USD mismatch."
Assert-Equal $main.manual_dry_run_state.broker_imports_seen 2 "Broker seen mismatch."
Assert-Equal $main.manual_dry_run_state.broker_imports_accepted 1 "Broker accepted mismatch."
Assert-Equal $main.manual_dry_run_state.broker_imports_quarantined 1 "Broker quarantined mismatch."
Assert-Equal $main.manual_dry_run_state.accounting_imports_seen 2 "Accounting seen mismatch."
Assert-Equal $main.manual_dry_run_state.accounting_imports_accepted 1 "Accounting accepted mismatch."
Assert-Equal $main.manual_dry_run_state.accounting_imports_quarantined 1 "Accounting quarantined mismatch."
Assert-Equal $main.manual_dry_run_state.accepted_imports_are_sample_only $true "Accepted imports must be sample only."
Assert-Equal $main.manual_dry_run_state.real_manual_broker_statement_present $false "Real broker statement should be absent."
Assert-Equal $main.manual_dry_run_state.real_manual_accounting_evidence_present $false "Real accounting evidence should be absent."
Assert-Equal $main.promotion_gates.broker_confirmed_pnl.ready $false "Broker-confirmed PnL should remain false."
Assert-Equal $brokerPnl.broker_confirmed_pnl_ready $false "Broker-confirmed requirements should remain false."
Assert-Equal $realizedClose.realized_accounting_close_ready $false "Realized accounting close should remain false."
Assert-Equal $commit.ledger_commit_ready $false "Ledger commit should remain false."
Assert-Equal $commit.db_mutation_ready $false "DB mutation should remain false."
Assert-Equal $production.production_live_ready $false "Production/live should remain false."
Assert-Equal $production.trading_readiness_ready $false "Trading readiness should remain false."
Assert-Equal $main.global_guards.external_calls $false "External calls should remain false."
Assert-Equal $main.global_guards.broker_api_calls $false "Broker API calls should remain false."
Assert-Equal $main.global_guards.market_data_fetch $false "Market-data fetch should remain false."
Assert-Equal $main.global_guards.account_data_fetch $false "Account-data fetch should remain false."
Assert-Equal $main.global_guards.db_mutation $false "DB mutation should remain false."
Assert-Equal $main.global_guards.ledger_commit $false "Ledger commit should remain false."
Assert-Equal $main.global_guards.production_live_ready $false "Production/live should remain false."
Assert-Equal $main.global_guards.trading_readiness_ready $false "Trading readiness should remain false."
foreach ($label in $main.forbidden_ready_labels.PSObject.Properties) {
    Assert-Equal $label.Value $false "Forbidden label must remain false: $($label.Name)"
}

Write-Host "REAL_EVIDENCE_PROMOTION_AND_COMMIT_READINESS_GATE_R001_TESTS_PASS"
