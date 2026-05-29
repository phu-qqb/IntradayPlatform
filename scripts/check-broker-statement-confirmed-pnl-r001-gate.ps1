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

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\broker-statement-confirmed-pnl-r001"
$SourceDir = Join-Path $RepoRoot "artifacts\readiness\real-manual-evidence-acceptance-r001"
$AcceptancePath = Join-Path $SourceDir "real-manual-evidence-acceptance-r001.json"
$NormalizedPath = Join-Path $SourceDir "real-manual-broker-statement-normalized-from-lmax-raw-r001.json"
$ValidationPath = Join-Path $SourceDir "real-manual-evidence-validation-report-r001.json"
$DiscoveryPath = Join-Path $SourceDir "real-manual-evidence-discovery-report-r001.json"

$PolicyPath = Join-Path $ArtifactDir "broker-statement-confirmed-pnl-policy-r001.json"
$ReconciliationPath = Join-Path $ArtifactDir "broker-statement-balance-reconciliation-r001.json"
$MainPath = Join-Path $ArtifactDir "broker-statement-confirmed-pnl-r001.json"
$SummaryPath = Join-Path $ArtifactDir "broker-statement-confirmed-pnl-summary-r001.md"

foreach ($path in @($AcceptancePath, $NormalizedPath, $ValidationPath, $DiscoveryPath, $PolicyPath, $ReconciliationPath, $MainPath, $SummaryPath)) {
    Assert-True (Test-Path -LiteralPath $path) "Required artifact missing: $path"
}

$acceptance = Read-JsonFile $AcceptancePath
$normalized = Read-JsonFile $NormalizedPath
$policy = Read-JsonFile $PolicyPath
$reconciliation = Read-JsonFile $ReconciliationPath
$main = Read-JsonFile $MainPath

Assert-Equal $acceptance.status "REAL_MANUAL_EVIDENCE_ACCEPTANCE_PARTIAL_READY_R001" "Source status mismatch."
Assert-Equal $acceptance.readiness.real_manual_broker_statement_acceptance $true "Source real broker statement acceptance must be true."
Assert-Equal $acceptance.readiness.real_manual_accounting_evidence_acceptance $false "Source accounting evidence acceptance must remain false."

Assert-Equal $normalized.sample_only $false "Normalized source must be non-sample."
Assert-Equal $normalized.real_broker_statement $true "Normalized source must be real broker statement."
Assert-Equal $normalized.external_fetch $false "Normalized source must not use external fetch."
Assert-Equal $normalized.broker_api_call $false "Normalized source must not use broker API."
Assert-Equal $normalized.market_data_fetch $false "Normalized source must not fetch market data."
Assert-Equal $normalized.account_data_fetch $false "Normalized source must not fetch account data."
Assert-Equal $normalized.db_mutation $false "Normalized source must not mutate DB."
Assert-Equal $normalized.ledger_commit $false "Normalized source must not commit ledger."
Assert-Equal $normalized.account_currency "USD" "Account currency must be USD."
Assert-Equal $normalized.statement_period.from "03/11/2025" "Statement period from mismatch."
Assert-Equal $normalized.statement_period.to "03/11/2025" "Statement period to mismatch."

Assert-Equal $policy.policy_type "broker_statement_confirmed_pnl_policy" "Policy type mismatch."
Assert-Equal $policy.source_of_truth "accepted_offline_manual_lmax_broker_statement" "Policy source of truth mismatch."
Assert-Equal $policy.scope "broker_statement_totals_only" "Policy scope mismatch."
Assert-Equal $policy.external_fetch_allowed $false "Policy must not allow external fetch."
Assert-Equal $policy.broker_api_allowed $false "Policy must not allow broker API."
Assert-Equal $policy.internal_trade_reconciliation_required_for_this_label $false "Internal trade reconciliation must not be required for this label."
Assert-Equal $policy.internal_trade_reconciliation_still_required_for_trade_level_attribution $true "Trade-level attribution must remain separately required."
Assert-Equal $policy.accounting_evidence_required_for_broker_statement_confirmed_pnl $false "Accounting evidence must not be required for statement-confirmed PnL."
Assert-Equal $policy.accounting_evidence_still_required_for_accounting_close $true "Accounting evidence must remain required for accounting close."
Assert-Equal $policy.ledger_commit_allowed $false "Ledger commit must not be allowed."
Assert-Equal $policy.db_mutation_allowed $false "DB mutation must not be allowed."
Assert-Equal $policy.production_live_allowed $false "Production/live must not be allowed."
Assert-Equal $policy.trading_allowed $false "Trading must not be allowed."

Assert-Equal $main.package "NEXT_BROKER_STATEMENT_CONFIRMED_PNL_R001" "Main package mismatch."
Assert-Equal $main.status "BROKER_STATEMENT_CONFIRMED_PNL_READY_R001" "Main status mismatch."
Assert-Equal $main.environment "sandbox" "Main environment mismatch."
Assert-Equal $main.mode "offline_manual_broker_statement_confirmation_only" "Main mode mismatch."
Assert-Equal $main.source_status "REAL_MANUAL_EVIDENCE_ACCEPTANCE_PARTIAL_READY_R001" "Main source status mismatch."
Assert-Equal $main.broker_statement_scope.broker "LMAX" "Broker mismatch."
Assert-Equal $main.broker_statement_scope.venue "LMAX_GLOBAL" "Venue mismatch."
Assert-Equal $main.broker_statement_scope.account_currency "USD" "Broker statement scope currency mismatch."
Assert-Equal $main.broker_statement_scope.statement_period.from "03/11/2025" "Broker statement scope from mismatch."
Assert-Equal $main.broker_statement_scope.statement_period.to "03/11/2025" "Broker statement scope to mismatch."
Assert-Equal $main.broker_statement_scope.trading_statement_date "30/04/2026 15:08:47" "Trading statement date mismatch."

Assert-DecimalEqual ([decimal]$main.broker_statement_confirmed_pnl.realised_pnl_usd) ([decimal]6015.14) "Realised PnL mismatch."
Assert-DecimalEqual ([decimal]$main.broker_statement_confirmed_pnl.commission_usd_signed) ([decimal]-225.63) "Commission signed mismatch."
Assert-DecimalEqual ([decimal]$main.broker_statement_confirmed_pnl.commission_cost_usd) ([decimal]225.63) "Commission cost mismatch."
Assert-DecimalEqual ([decimal]$main.broker_statement_confirmed_pnl.financing_usd_signed) ([decimal]-40.60) "Financing signed mismatch."
Assert-DecimalEqual ([decimal]$main.broker_statement_confirmed_pnl.financing_cost_usd) ([decimal]40.60) "Financing cost mismatch."
Assert-DecimalEqual ([decimal]$main.broker_statement_confirmed_pnl.closing_pnl_usd) ([decimal]463.61) "Closing PnL mismatch."
Assert-DecimalEqual ([decimal]$main.broker_statement_confirmed_pnl.closing_balance_usd) ([decimal]496446.04) "Closing balance mismatch."
Assert-DecimalEqual ([decimal]$main.broker_statement_confirmed_pnl.closing_equity_usd) ([decimal]496909.65) "Closing equity mismatch."
Assert-DecimalEqual ([decimal]$main.broker_statement_confirmed_pnl.realised_net_after_commission_financing_usd) ([decimal]5748.91) "Realised net after costs mismatch."
Assert-DecimalEqual ([decimal]$main.broker_statement_confirmed_pnl.equity_pnl_including_open_pnl_usd) ([decimal]6212.52) "Equity PnL including open PnL mismatch."

Assert-DecimalEqual ([decimal]$reconciliation.opening_balance_usd) ([decimal]490697.13) "Opening balance mismatch."
Assert-DecimalEqual ([decimal]$reconciliation.realised_pnl_usd) ([decimal]6015.14) "Reconciliation realised PnL mismatch."
Assert-DecimalEqual ([decimal]$reconciliation.commission_usd_signed) ([decimal]-225.63) "Reconciliation commission signed mismatch."
Assert-DecimalEqual ([decimal]$reconciliation.commission_cost_usd) ([decimal]225.63) "Reconciliation commission cost mismatch."
Assert-DecimalEqual ([decimal]$reconciliation.financing_usd_signed) ([decimal]-40.60) "Reconciliation financing signed mismatch."
Assert-DecimalEqual ([decimal]$reconciliation.financing_cost_usd) ([decimal]40.60) "Reconciliation financing cost mismatch."
Assert-DecimalEqual ([decimal]$reconciliation.closing_pnl_usd) ([decimal]463.61) "Reconciliation closing PnL mismatch."
Assert-DecimalEqual ([decimal]$reconciliation.closing_balance_usd) ([decimal]496446.04) "Reconciliation closing balance mismatch."
Assert-DecimalEqual ([decimal]$reconciliation.closing_equity_usd) ([decimal]496909.65) "Reconciliation closing equity mismatch."
Assert-DecimalEqual ([decimal]$reconciliation.realised_net_after_commission_financing_usd) ([decimal]5748.91) "Reconciliation realised net after costs mismatch."
Assert-DecimalEqual ([decimal]$reconciliation.equity_pnl_including_open_pnl_usd) ([decimal]6212.52) "Reconciliation equity PnL mismatch."

$balanceFormula = [decimal]$reconciliation.opening_balance_usd + [decimal]$reconciliation.realised_pnl_usd + [decimal]$reconciliation.commission_usd_signed + [decimal]$reconciliation.financing_usd_signed
$equityFormula = [decimal]$reconciliation.closing_balance_usd + [decimal]$reconciliation.closing_pnl_usd
Assert-DecimalEqual $balanceFormula ([decimal]$reconciliation.closing_balance_usd) "Balance formula failed."
Assert-DecimalEqual $equityFormula ([decimal]$reconciliation.closing_equity_usd) "Equity formula failed."
Assert-Equal $reconciliation.balance_reconciled $true "Balance reconciliation must be true."
Assert-Equal $reconciliation.equity_reconciled $true "Equity reconciliation must be true."
Assert-True ([decimal]$reconciliation.tolerance -le [decimal]0.000001) "Reconciliation tolerance must be no wider than 0.000001."
Assert-Equal $main.broker_statement_reconciliation.balance_reconciled $true "Main balance reconciliation must be true."
Assert-Equal $main.broker_statement_reconciliation.equity_reconciled $true "Main equity reconciliation must be true."
Assert-True ([decimal]$main.broker_statement_reconciliation.tolerance -le [decimal]0.000001) "Main tolerance must be no wider than 0.000001."

Assert-Equal $main.ready_outputs.broker_statement_confirmed_pnl_ready $true "Broker statement confirmed PnL ready output missing."
Assert-Equal $main.readiness.broker_statement_confirmed_pnl_ready $true "Broker statement confirmed PnL readiness missing."
Assert-Equal $main.synthetic_sandbox_closeout_comparison.comparison_purpose "diagnostic_only_not_acceptance_gate" "Synthetic comparison must be diagnostic only."
Assert-DecimalEqual ([decimal]$main.synthetic_sandbox_closeout_comparison.synthetic_gross_usd) ([decimal]-50.308800) "Synthetic gross mismatch."
Assert-DecimalEqual ([decimal]$main.synthetic_sandbox_closeout_comparison.synthetic_commission_usd) ([decimal]26.268029) "Synthetic commission mismatch."
Assert-DecimalEqual ([decimal]$main.synthetic_sandbox_closeout_comparison.synthetic_net_usd) ([decimal]-76.576829) "Synthetic net mismatch."
Assert-Equal $main.synthetic_sandbox_closeout_comparison.used_as_broker_acceptance_gate $false "Synthetic closeout must not gate broker acceptance."
Assert-Equal $main.synthetic_sandbox_closeout_comparison.used_as_broker_confirmed_pnl_gate $false "Synthetic closeout must not gate broker statement confirmed PnL."

Assert-Equal $main.readiness.internal_trade_reconciliation_ready $false "Internal trade reconciliation must remain false."
Assert-Equal $main.readiness.accounting_evidence_acceptance $false "Accounting evidence acceptance must remain false."
Assert-Equal $main.readiness.realized_accounting_close $false "Realized accounting close must remain false."
Assert-Equal $main.readiness.ledger_commit $false "Ledger commit must remain false."
Assert-Equal $main.readiness.db_mutation $false "DB mutation must remain false."
Assert-Equal $main.readiness.production_live $false "Production/live must remain false."
Assert-Equal $main.readiness.trading_readiness $false "Trading readiness must remain false."
foreach ($label in $main.forbidden_ready_labels.PSObject.Properties) {
    Assert-Equal $label.Value $false "Forbidden ready label must remain false: $($label.Name)"
}

Assert-Equal $main.global_guards.external_calls $false "External calls must remain false."
Assert-Equal $main.global_guards.broker_api_calls $false "Broker API calls must remain false."
Assert-Equal $main.global_guards.market_data_fetch $false "Market-data fetch must remain false."
Assert-Equal $main.global_guards.account_data_fetch $false "Account-data fetch must remain false."
Assert-Equal $main.global_guards.ledger_commit $false "Ledger commit must remain false."
Assert-Equal $main.global_guards.db_mutation $false "DB mutation must remain false."
Assert-Equal $main.global_guards.trading_activity $false "Trading activity must remain false."
Assert-Equal $main.global_guards.production_live_ready $false "Production/live ready must remain false."
Assert-Equal $main.global_guards.trading_readiness_ready $false "Trading readiness ready must remain false."

$scanFiles = @(
    (Join-Path $RepoRoot "scripts\build-broker-statement-confirmed-pnl-r001.ps1"),
    (Join-Path $RepoRoot "scripts\test-broker-statement-confirmed-pnl-r001.ps1")
)
$forbiddenPatterns = @("SubmitOrder", "SubmitOrderUnmanaged", "AtmStrategyCreate", "Invoke-WebRequest", "Invoke-RestMethod", "curl ", "wget ", "api_key", "apikey", "password")
foreach ($pattern in $forbiddenPatterns) {
    $matches = Select-String -Path $scanFiles -Pattern $pattern -SimpleMatch -ErrorAction SilentlyContinue
    if ($matches) {
        throw "Forbidden call/secret/static pattern detected [$pattern]:`n$($matches | Out-String)"
    }
}

Write-Host "BROKER_STATEMENT_CONFIRMED_PNL_R001_GATE_PASS"
