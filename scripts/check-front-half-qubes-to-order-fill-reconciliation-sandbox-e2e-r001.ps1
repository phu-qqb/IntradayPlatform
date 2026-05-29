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

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\front-half-qubes-to-order-fill-reconciliation-sandbox-e2e-r001"
$SourceAuditPath = Join-Path $RepoRoot "artifacts\readiness\e2e-flow-coverage-audit-r001\e2e-flow-coverage-audit-r001.json"
$BackHalfPath = Join-Path $RepoRoot "artifacts\readiness\sandbox-ledger-db-post-commit-closeout-and-production-go-no-go-r001\sandbox-ledger-db-post-commit-closeout-r001.json"

$RequiredFiles = @(
    "front-half-market-data-basis-r001.json",
    "qubes-weight-handoff-r001.json",
    "drift-calculation-r001.json",
    "order-targets-r001.json",
    "execution-algo-plan-r001.json",
    "sandbox-orders-fills-r001.json",
    "residual-flatten-report-r001.json",
    "trade-level-reconciliation-r001.json",
    "front-half-strategy-pnl-r001.json",
    "front-to-back-scope-bridge-r001.json",
    "front-half-qubes-to-order-fill-reconciliation-sandbox-e2e-r001.json",
    "e2e-flow-coverage-after-front-half-r001.json",
    "front-half-qubes-to-order-fill-reconciliation-sandbox-e2e-summary-r001.md"
)

foreach ($file in $RequiredFiles) {
    Assert-True (Test-Path -LiteralPath (Join-Path $ArtifactDir $file)) "Missing required artifact: $file"
}
Assert-True (Test-Path -LiteralPath $SourceAuditPath) "Source audit missing."
Assert-True (Test-Path -LiteralPath $BackHalfPath) "Back-half closeout missing."

$sourceAudit = Read-JsonFile $SourceAuditPath
$backHalf = Read-JsonFile $BackHalfPath
$market = Read-JsonFile (Join-Path $ArtifactDir "front-half-market-data-basis-r001.json")
$qubes = Read-JsonFile (Join-Path $ArtifactDir "qubes-weight-handoff-r001.json")
$drift = Read-JsonFile (Join-Path $ArtifactDir "drift-calculation-r001.json")
$orders = Read-JsonFile (Join-Path $ArtifactDir "order-targets-r001.json")
$plan = Read-JsonFile (Join-Path $ArtifactDir "execution-algo-plan-r001.json")
$fills = Read-JsonFile (Join-Path $ArtifactDir "sandbox-orders-fills-r001.json")
$residual = Read-JsonFile (Join-Path $ArtifactDir "residual-flatten-report-r001.json")
$tradeRecon = Read-JsonFile (Join-Path $ArtifactDir "trade-level-reconciliation-r001.json")
$strategyPnl = Read-JsonFile (Join-Path $ArtifactDir "front-half-strategy-pnl-r001.json")
$bridge = Read-JsonFile (Join-Path $ArtifactDir "front-to-back-scope-bridge-r001.json")
$main = Read-JsonFile (Join-Path $ArtifactDir "front-half-qubes-to-order-fill-reconciliation-sandbox-e2e-r001.json")
$coverageAfter = Read-JsonFile (Join-Path $ArtifactDir "e2e-flow-coverage-after-front-half-r001.json")

Assert-Equal $sourceAudit.status "E2E_FLOW_COVERAGE_AUDIT_COMPLETE_R001" "Source audit status mismatch."
Assert-Equal $main.status "FRONT_HALF_QUBES_TO_ORDER_FILL_RECONCILIATION_SANDBOX_CONFIRMED_R001" "Main status mismatch."
Assert-Equal $market.status "FRONT_HALF_MARKET_DATA_BASIS_READY_R001" "Market data basis missing or blocked."
Assert-Equal $qubes.status "QUBES_WEIGHT_HANDOFF_SANDBOX_CONNECTED_R001" "Qubes handoff missing or blocked."
Assert-Equal $qubes.real_qubes_core_generation_confirmed $false "Real Qubes/Core generation must not be claimed."
Assert-Equal $drift.status "DRIFT_CALCULATION_SANDBOX_CONFIRMED_R001" "Drift status mismatch."
Assert-Equal $orders.status "ORDER_TARGETS_SANDBOX_CONFIRMED_R001" "Order target status mismatch."
Assert-Equal $plan.status "EXECUTION_ALGO_PLAN_SANDBOX_CONFIRMED_R001" "Execution plan status mismatch."
Assert-Equal $fills.status "SANDBOX_ORDERS_FILLS_CONFIRMED_R001" "Sandbox orders/fills status mismatch."
Assert-Equal $residual.status "RESIDUAL_FLATTEN_REPORT_SANDBOX_CONFIRMED_R001" "Residual report status mismatch."
Assert-Equal $tradeRecon.status "TRADE_LEVEL_RECONCILIATION_SANDBOX_CONFIRMED_R001" "Trade-level reconciliation status mismatch."
Assert-Equal $strategyPnl.status "FRONT_HALF_STRATEGY_PNL_SANDBOX_CONFIRMED_R001" "Strategy PnL status mismatch."
Assert-Equal $bridge.status "FRONT_TO_BACK_SCOPE_BRIDGE_READY_REAL_FULL_FLOW_BLOCKED_R001" "Scope bridge status mismatch."

Assert-True (@($market.instruments).Count -gt 0) "Market data basis must contain instruments."
Assert-Equal $market.external_calls $false "Market basis must not have external calls."
Assert-Equal $market.market_data_fetch $false "Market basis must not fetch market data."
Assert-True (@($qubes.netted_usd_weights).Count -gt 0) "Qubes handoff must contain netted USD weights."
Assert-True (@($drift.rows).Count -gt 0) "Drift rows missing."
Assert-True (@($orders.orders).Count -gt 0) "Order targets missing."
Assert-True (@($orders.skipped_orders).Count -gt 0) "Skipped zero/below-min order lines missing."
Assert-Equal $orders.zero_quantity_orders_excluded $true "Zero quantity orders must be excluded."
Assert-Equal $orders.tag22_policy.enforced $true "Tag 22 policy must be enforced."
Assert-True (@($orders.orders | Where-Object { $_.security_id -and $_.security_id_source_tag22 -ne "8" }).Count -eq 0) "Every order with SecurityID must use tag 22 value 8."

Assert-Equal $plan.no_live_routing $true "Execution plan must be no-live-routing."
Assert-Equal $plan.no_fix_api_call $true "Execution plan must not call FIX/API."
Assert-Equal $plan.no_broker_api_call $true "Execution plan must not call broker API."
Assert-True (@($fills.fills).Count -gt 0) "Sandbox fills missing."
Assert-Equal $fills.no_live_trading $true "Sandbox fills must not be live trading."
Assert-Equal $fills.no_broker_api $true "Sandbox fills must not use broker API."
Assert-True (@($fills.fills | Where-Object { $_.sandbox -ne $true -or $_.simulated -ne $true -or $_.live_trading -ne $false }).Count -eq 0) "Fills must be labelled sandbox/simulated/non-live."
Assert-Equal $residual.residual_zero $true "Residuals must be zero."
Assert-True (@($residual.unfilled_lines | Where-Object { $_.symbol -eq "USDJPY" -and [decimal]$_.quantity -eq [decimal]50.0 }).Count -eq 1) "USDJPY unfilled 50.0 must be preserved."
Assert-Equal $tradeRecon.order_targets_reconciled $true "Order targets must reconcile."
Assert-Equal $tradeRecon.sandbox_fills_reconciled $true "Sandbox fills must reconcile."
Assert-Equal $tradeRecon.residual_report_reconciled $true "Residual report must reconcile."
Assert-Equal $strategyPnl.broker_statement_pnl_overwritten $false "Front-half PnL must not overwrite broker statement PnL."
Assert-Equal $strategyPnl.equals_lmax_statement $false "Front-half PnL must not be falsely equated with LMAX statement."
Assert-Equal $bridge.run_ids_match $false "Run IDs must not be claimed to match."
Assert-Equal $bridge.statement_periods_match $false "Statement periods must not be claimed to match."
Assert-Equal $bridge.fills_or_order_ids_match $false "Fill/order IDs must not be claimed to match."
Assert-Equal $bridge.pnl_equality_expected $false "PnL equality must not be expected."
Assert-Equal $bridge.broker_accounting_ledger_artifacts_downstream_of_this_front_half_run $false "Back-half must not be claimed downstream of this front-half run."
Assert-Equal $bridge.full_real_front_to_back_chain_complete $false "Full real front-to-back chain must remain incomplete."

Assert-Equal $main.ready_outputs.front_half_sandbox_e2e_ready $true "Front-half sandbox E2E ready output mismatch."
Assert-Equal $main.ready_outputs.qubes_to_orders_sandbox_ready $true "Qubes-to-orders ready output mismatch."
Assert-Equal $main.ready_outputs.orders_to_fills_sandbox_ready $true "Orders-to-fills ready output mismatch."
Assert-Equal $main.ready_outputs.trade_level_reconciliation_sandbox_ready $true "Trade-level reconciliation ready output mismatch."
Assert-Equal $main.full_flow.sandbox_front_half_complete $true "Sandbox front half must be complete."
Assert-Equal $main.full_flow.back_half_complete $true "Back half must be complete."
Assert-Equal $main.full_flow.full_real_front_to_back_complete $false "Full real front-to-back must remain incomplete."
Assert-Equal $backHalf.status "SANDBOX_LEDGER_DB_POST_COMMIT_CLOSEOUT_READY_R001" "Existing back-half closeout not detected."

Assert-Equal $coverageAfter.flow_coverage.market_data "SANDBOX_CONFIRMED" "Updated market-data coverage mismatch."
Assert-Equal $coverageAfter.flow_coverage.qubes_weight_generation "SANDBOX_CONFIRMED" "Updated Qubes coverage mismatch."
Assert-Equal $coverageAfter.flow_coverage.trade_level_reconciliation "SANDBOX_CONFIRMED" "Updated trade recon coverage mismatch."
Assert-Equal $coverageAfter.flow_coverage.production_live_trading "BLOCKED" "Production/trading coverage must remain blocked."
Assert-Equal $coverageAfter.full_real_front_to_back_complete $false "Updated coverage must not claim full real flow."

foreach ($flag in @("trading_activity", "r009_submission", "lmax_fix_api_call", "broker_api_call", "polygon_massive_call", "market_data_fetch", "broker_fetch", "account_data_fetch", "production_live_write", "production_live_ready", "trading_readiness_ready")) {
    Assert-Equal $main.global_guards.$flag $false "Main global guard must remain false: $flag"
}

$scanFiles = @(
    (Join-Path $RepoRoot "scripts\build-front-half-qubes-to-order-fill-reconciliation-sandbox-e2e-r001.ps1"),
    (Join-Path $RepoRoot "scripts\test-front-half-qubes-to-order-fill-reconciliation-sandbox-e2e-r001.ps1")
)
$forbiddenPatterns = @("SubmitOrder", "SubmitOrderUnmanaged", "AtmStrategyCreate", "Invoke-WebRequest", "Invoke-RestMethod", "curl ", "wget ", "api_key", "apikey", "password")
foreach ($pattern in $forbiddenPatterns) {
    $matches = Select-String -Path $scanFiles -Pattern $pattern -SimpleMatch -ErrorAction SilentlyContinue
    if ($matches) {
        throw "Forbidden static pattern detected [$pattern]:`n$($matches | Out-String)"
    }
}

Write-Host "FRONT_HALF_QUBES_TO_ORDER_FILL_RECONCILIATION_SANDBOX_E2E_R001_GATE_PASS"
