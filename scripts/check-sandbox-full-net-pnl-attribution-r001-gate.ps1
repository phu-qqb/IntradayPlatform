param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Equal($Actual, $Expected, [string]$Message) {
    if ($Actual -ne $Expected) {
        throw "$Message Expected=[$Expected] Actual=[$Actual]"
    }
}

function Assert-DecimalEqual([decimal]$Actual, [decimal]$Expected, [string]$Message) {
    if ([Math]::Abs($Actual - $Expected) -gt [decimal]0.000001) {
        throw "$Message Expected=[$Expected] Actual=[$Actual]"
    }
}

function Read-JsonFile([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

$artifactDir = Join-Path $RepoRoot "artifacts\readiness\sandbox-full-net-pnl-attribution-r001"
$required = @(
    "source-validation.json",
    "sandbox-full-net-pnl-attribution-output.json",
    "label-guard.json",
    "contract-status-update.json",
    "boundary-safety-evidence.json",
    "summary.md"
)

foreach ($name in $required) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactDir $name)) "Required artifact missing: $name"
}

$source = Read-JsonFile (Join-Path $artifactDir "source-validation.json")
$output = Read-JsonFile (Join-Path $artifactDir "sandbox-full-net-pnl-attribution-output.json")
$labelGuard = Read-JsonFile (Join-Path $artifactDir "label-guard.json")
$contract = Read-JsonFile (Join-Path $artifactDir "contract-status-update.json")
$boundary = Read-JsonFile (Join-Path $artifactDir "boundary-safety-evidence.json")

Assert-Equal $source.status "SANDBOX_FULL_NET_PNL_ATTRIBUTION_READY_R001" "Source validation must be ready."
Assert-Equal $source.r003_public_commission_evidence_imported $true "R003 public commission evidence must be validated."
Assert-Equal $source.r003_commission_policy_valid $true "R003 commission policy must validate."
Assert-Equal $source.r003_unfilled_usdjpy_50_excluded $true "R003 USDJPY 50.0 exclusion must validate."
Assert-Equal $source.r003_zero_quantity_lines_excluded $true "R003 zero-quantity exclusion must validate."
Assert-Equal $source.r001_account_currency_explicit_usd $true "Account currency USD must be explicit."
Assert-Equal $source.r001_rate_source_checked_in_fixture_or_prior_artifact $true "Rate source must be fixture or prior artifact."
Assert-Equal $source.r001_external_calls_false $true "Source external calls must be false."
Assert-Equal $source.r001_db_mutation_false $true "Source DB mutation must be false."
Assert-Equal $source.r001_ledger_commit_false $true "Source ledger commit must be false."
Assert-Equal $source.commission_confirmation_status "ACCOUNT_SPECIFIC_COMMISSION_CONFIRMED_R001" "Commission confirmation must be account-specific and confirmed."
Assert-Equal $source.sandbox_full_net_pnl_preview_ready $true "Sandbox full net PnL preview must be ready before attribution."

Assert-Equal $output.package "NEXT_SANDBOX_FULL_NET_PNL_ATTRIBUTION_R001" "Output package mismatch."
Assert-Equal $output.environment "sandbox" "Environment must be sandbox."
Assert-Equal $output.mode "preview_only" "Mode must be preview_only."
Assert-Equal $output.status "SANDBOX_FULL_NET_PNL_ATTRIBUTION_READY_R001" "Output status must be ready."
Assert-Equal $output.account_currency "USD" "Account currency must be USD."
Assert-Equal @($output.symbol_attribution).Count 9 "Expected 9 included symbol attribution rows."
Assert-True (@($output.currency_attribution).Count -ge 1) "Currency attribution must exist."
Assert-Equal @($output.exclusion_attribution.excluded_unfilled | Where-Object { $_.symbol -eq "USDJPY" -and [decimal]$_.quantity -eq [decimal]50.0 }).Count 1 "Unfilled USDJPY 50.0 must remain excluded."
Assert-Equal $output.exclusion_attribution.excluded_zero_quantity_count 4 "Four zero-quantity lines must remain excluded."
Assert-Equal $output.exclusion_attribution.excluded_reintroduced $false "Excluded lines must not be reintroduced."

Assert-DecimalEqual ([decimal]$output.attribution_bridge.gross_pnl_usd) ([decimal]-50.308800) "Gross USD attribution mismatch."
Assert-DecimalEqual ([decimal]$output.attribution_bridge.commission_usd) ([decimal]26.268029) "Commission USD attribution mismatch."
Assert-DecimalEqual ([decimal]$output.attribution_bridge.net_pnl_usd) ([decimal]-76.576829) "Net USD attribution mismatch."
Assert-Equal $output.attribution_bridge.formula "net_pnl_usd = gross_pnl_usd - commission_usd" "Attribution formula mismatch."
Assert-Equal $output.attribution_bridge.reconciled $true "Attribution bridge must be reconciled."
Assert-True ([decimal]$output.attribution_bridge.tolerance -le [decimal]0.000001) "Tolerance must not exceed 0.000001."
Assert-Equal $output.ready_outputs.sandbox_full_net_pnl_attribution_preview $true "Sandbox full net PnL attribution preview must be the ready output."

Assert-True ($output.still_blocked -contains "accounting_pnl") "Accounting PnL must remain blocked."
Assert-True ($output.still_blocked -contains "broker_statement_reconciliation") "Broker statement reconciliation must remain blocked."
Assert-True ($output.still_blocked -contains "ledger_commit") "Ledger commit must remain blocked."
Assert-True ($output.still_blocked -contains "production_live") "Production/live must remain blocked."
Assert-True ($output.still_blocked -contains "trading_readiness") "Trading readiness must remain blocked."
Assert-Equal $output.ledger_commit $false "Ledger commit must be false."
Assert-Equal $output.db_mutation $false "DB mutation must be false."
Assert-Equal $output.external_calls $false "External calls must be false."
Assert-Equal $output.trading_activity $false "Trading activity must be false."
Assert-Equal $output.accounting_pnl_ready $false "Accounting PnL ready must be false."
Assert-Equal $output.broker_statement_reconciliation_ready $false "Broker reconciliation ready must be false."
Assert-Equal $output.production_live_ready $false "Production/live ready must be false."

Assert-Equal $labelGuard.status "LABEL_GUARD_PASS" "Label guard must pass."
Assert-Equal $labelGuard.only_ready_label_allowed "sandbox_full_net_pnl_attribution_preview" "Only allowed ready label mismatch."
Assert-Equal $labelGuard.accounting_pnl_ready $false "Accounting PnL ready label must be false."
Assert-Equal $labelGuard.realized_pnl_ready $false "Realized PnL ready label must be false."
Assert-Equal $labelGuard.broker_confirmed_pnl_ready $false "Broker-confirmed PnL ready label must be false."
Assert-Equal $labelGuard.ledger_pnl_ready $false "Ledger PnL ready label must be false."
Assert-Equal $labelGuard.production_pnl_ready $false "Production PnL ready label must be false."
Assert-Equal $labelGuard.live_pnl_ready $false "Live PnL ready label must be false."
Assert-Equal $labelGuard.trading_ready $false "Trading ready label must be false."

Assert-Equal $contract.statuses."sandbox-full-net-pnl-attribution-preview.v1" "YES_PREVIEW_ONLY" "Contract should mark only sandbox attribution preview as ready."
Assert-Equal $contract.statuses."accounting-pnl.v1" "BLOCKED" "Accounting PnL contract must be blocked."
Assert-Equal $contract.statuses."ledger-commit.v1" "BLOCKED" "Ledger commit contract must be blocked."
Assert-Equal $contract.statuses."broker-statement-reconciliation.v1" "BLOCKED" "Broker reconciliation contract must be blocked."
Assert-Equal $contract.statuses."production-readiness.v1" "BLOCKED" "Production readiness contract must be blocked."
Assert-Equal $contract.statuses."trading-readiness.v1" "BLOCKED" "Trading readiness contract must be blocked."

Assert-Equal $boundary.no_trades $true "No trades must be recorded."
Assert-Equal $boundary.no_r009_submission $true "No R009 submission must be recorded."
Assert-Equal $boundary.no_lmax_fix_api_call $true "No LMAX FIX/API call must be recorded."
Assert-Equal $boundary.no_polygon_massive_call $true "No Polygon/Massive call must be recorded."
Assert-Equal $boundary.no_broker_api_call $true "No broker API call must be recorded."
Assert-Equal $boundary.no_market_data_fetch $true "No market-data fetch must be recorded."
Assert-Equal $boundary.no_account_data_fetch $true "No account-data fetch must be recorded."
Assert-Equal $boundary.no_live_order_fill_reports $true "No live order/fill reports must be consumed."
Assert-Equal $boundary.no_db_mutation $true "No DB mutation must be recorded."
Assert-Equal $boundary.no_ledger_commit $true "No ledger commit must be recorded."
Assert-Equal $boundary.accounting_pnl_ready $false "Accounting PnL must remain not ready."
Assert-Equal $boundary.broker_statement_reconciliation_ready $false "Broker reconciliation must remain not ready."
Assert-Equal $boundary.production_live_ready $false "Production/live must remain not ready."
Assert-Equal $boundary.trading_readiness_ready $false "Trading readiness must remain not ready."
Assert-Equal $boundary.prior_artifacts_only $true "Gate must use prior artifacts only."

Write-Host "SANDBOX_FULL_NET_PNL_ATTRIBUTION_R001_GATE_PASS"
