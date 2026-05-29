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

function Assert-DecimalEqual([decimal]$Actual, [decimal]$Expected, [string]$Message) {
    if ([Math]::Abs($Actual - $Expected) -gt [decimal]0.000001) {
        throw "$Message Expected=[$Expected] Actual=[$Actual]"
    }
}

function Read-JsonFile([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

$builder = Join-Path $RepoRoot "scripts\build-broker-statement-confirmed-pnl-r001.ps1"
$gate = Join-Path $RepoRoot "scripts\check-broker-statement-confirmed-pnl-r001-gate.ps1"

& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot | Out-Null
& powershell -ExecutionPolicy Bypass -File $gate -RepoRoot $RepoRoot | Out-Null

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\broker-statement-confirmed-pnl-r001"
$SourceDir = Join-Path $RepoRoot "artifacts\readiness\real-manual-evidence-acceptance-r001"
$main = Read-JsonFile (Join-Path $ArtifactDir "broker-statement-confirmed-pnl-r001.json")
$reconciliation = Read-JsonFile (Join-Path $ArtifactDir "broker-statement-balance-reconciliation-r001.json")
$policy = Read-JsonFile (Join-Path $ArtifactDir "broker-statement-confirmed-pnl-policy-r001.json")
$normalized = Read-JsonFile (Join-Path $SourceDir "real-manual-broker-statement-normalized-from-lmax-raw-r001.json")

Assert-Equal $main.status "BROKER_STATEMENT_CONFIRMED_PNL_READY_R001" "Broker-statement-confirmed PnL status mismatch."
Assert-Equal $main.ready_outputs.broker_statement_confirmed_pnl_ready $true "Allowed ready output should be true."
Assert-Equal $main.readiness.broker_statement_confirmed_pnl_ready $true "Readiness should mark broker-statement-confirmed PnL ready."
Assert-Equal $policy.internal_trade_reconciliation_required_for_this_label $false "Internal trade reconciliation should not be required for this label."
Assert-Equal $policy.accounting_evidence_required_for_broker_statement_confirmed_pnl $false "Accounting evidence should not be required for this label."

Assert-Equal $normalized.sample_only $false "Source must be non-sample."
Assert-Equal $normalized.real_broker_statement $true "Source must be real broker statement evidence."
Assert-Equal $normalized.external_fetch $false "Source must not use external fetch."
Assert-Equal $normalized.broker_api_call $false "Source must not use broker API."
Assert-Equal $normalized.market_data_fetch $false "Source must not fetch market data."
Assert-Equal $normalized.account_data_fetch $false "Source must not fetch account data."
Assert-Equal $normalized.db_mutation $false "Source must not mutate DB."
Assert-Equal $normalized.ledger_commit $false "Source must not commit ledger."

Assert-Equal $main.broker_statement_scope.account_currency "USD" "Account currency should be USD."
Assert-Equal $main.broker_statement_scope.statement_period.from "03/11/2025" "Statement period from mismatch."
Assert-Equal $main.broker_statement_scope.statement_period.to "03/11/2025" "Statement period to mismatch."
Assert-Equal $main.broker_statement_scope.trading_statement_date "30/04/2026 15:08:47" "Trading statement date mismatch."

Assert-DecimalEqual ([decimal]$main.broker_statement_confirmed_pnl.realised_pnl_usd) ([decimal]6015.14) "Realised PnL mismatch."
Assert-DecimalEqual ([decimal]$main.broker_statement_confirmed_pnl.commission_usd_signed) ([decimal]-225.63) "Commission signed mismatch."
Assert-DecimalEqual ([decimal]$main.broker_statement_confirmed_pnl.commission_cost_usd) ([decimal]225.63) "Commission cost mismatch."
Assert-DecimalEqual ([decimal]$main.broker_statement_confirmed_pnl.financing_usd_signed) ([decimal]-40.60) "Financing signed mismatch."
Assert-DecimalEqual ([decimal]$main.broker_statement_confirmed_pnl.financing_cost_usd) ([decimal]40.60) "Financing cost mismatch."
Assert-DecimalEqual ([decimal]$main.broker_statement_confirmed_pnl.realised_net_after_commission_financing_usd) ([decimal]5748.91) "Realised net after costs mismatch."
Assert-DecimalEqual ([decimal]$main.broker_statement_confirmed_pnl.closing_pnl_usd) ([decimal]463.61) "Closing PnL mismatch."
Assert-DecimalEqual ([decimal]$main.broker_statement_confirmed_pnl.equity_pnl_including_open_pnl_usd) ([decimal]6212.52) "Equity PnL including open PnL mismatch."
Assert-DecimalEqual ([decimal]$main.broker_statement_confirmed_pnl.closing_balance_usd) ([decimal]496446.04) "Closing balance mismatch."
Assert-DecimalEqual ([decimal]$main.broker_statement_confirmed_pnl.closing_equity_usd) ([decimal]496909.65) "Closing equity mismatch."

$balanceFormula = [decimal]$reconciliation.opening_balance_usd + [decimal]$reconciliation.realised_pnl_usd + [decimal]$reconciliation.commission_usd_signed + [decimal]$reconciliation.financing_usd_signed
$equityFormula = [decimal]$reconciliation.closing_balance_usd + [decimal]$reconciliation.closing_pnl_usd
Assert-DecimalEqual $balanceFormula ([decimal]496446.04) "Balance formula should reconcile."
Assert-DecimalEqual $equityFormula ([decimal]496909.65) "Equity formula should reconcile."
Assert-Equal $reconciliation.balance_reconciled $true "Balance reconciliation should be true."
Assert-Equal $reconciliation.equity_reconciled $true "Equity reconciliation should be true."
Assert-True ([decimal]$reconciliation.tolerance -le [decimal]0.000001) "Tolerance must be no wider than 0.000001."

Assert-Equal $main.synthetic_sandbox_closeout_comparison.comparison_purpose "diagnostic_only_not_acceptance_gate" "Synthetic sandbox closeout should be diagnostic only."
Assert-Equal $main.synthetic_sandbox_closeout_comparison.used_as_broker_acceptance_gate $false "Synthetic sandbox closeout must not gate broker acceptance."
Assert-Equal $main.synthetic_sandbox_closeout_comparison.used_as_broker_confirmed_pnl_gate $false "Synthetic sandbox closeout must not gate broker statement confirmed PnL."

Assert-Equal $main.readiness.internal_trade_reconciliation_ready $false "Internal trade reconciliation must remain blocked."
Assert-Equal $main.readiness.accounting_evidence_acceptance $false "Accounting evidence acceptance must remain blocked."
Assert-Equal $main.readiness.realized_accounting_close $false "Realized accounting close must remain blocked."
Assert-Equal $main.readiness.ledger_commit $false "Ledger commit must remain blocked."
Assert-Equal $main.readiness.db_mutation $false "DB mutation must remain blocked."
Assert-Equal $main.readiness.production_live $false "Production/live must remain blocked."
Assert-Equal $main.readiness.trading_readiness $false "Trading readiness must remain blocked."
Assert-Equal $main.global_guards.external_calls $false "External calls must remain false."
Assert-Equal $main.global_guards.broker_api_calls $false "Broker API calls must remain false."
Assert-Equal $main.global_guards.market_data_fetch $false "Market-data fetch must remain false."
Assert-Equal $main.global_guards.account_data_fetch $false "Account-data fetch must remain false."
Assert-Equal $main.global_guards.ledger_commit $false "Ledger commit must remain false."
Assert-Equal $main.global_guards.db_mutation $false "DB mutation must remain false."
Assert-Equal $main.global_guards.production_live_ready $false "Production/live ready must remain false."
Assert-Equal $main.global_guards.trading_readiness_ready $false "Trading readiness ready must remain false."

Write-Host "BROKER_STATEMENT_CONFIRMED_PNL_R001_TEST_PASS"
