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

$builder = Join-Path $RepoRoot "scripts\build-front-half-qubes-to-order-fill-reconciliation-sandbox-e2e-r001.ps1"
$gate = Join-Path $RepoRoot "scripts\check-front-half-qubes-to-order-fill-reconciliation-sandbox-e2e-r001.ps1"

& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot | Out-Null
& powershell -ExecutionPolicy Bypass -File $gate -RepoRoot $RepoRoot | Out-Null

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\front-half-qubes-to-order-fill-reconciliation-sandbox-e2e-r001"
$main = Read-JsonFile (Join-Path $ArtifactDir "front-half-qubes-to-order-fill-reconciliation-sandbox-e2e-r001.json")
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
$coverageAfter = Read-JsonFile (Join-Path $ArtifactDir "e2e-flow-coverage-after-front-half-r001.json")

Assert-Equal $main.status "FRONT_HALF_QUBES_TO_ORDER_FILL_RECONCILIATION_SANDBOX_CONFIRMED_R001" "Main status mismatch."
Assert-Equal $market.classification "SANDBOX_CONFIRMED" "Market data basis should be sandbox-confirmed."
Assert-Equal $market.market_data_fetch $false "Market data fetch must be false."
Assert-Equal $qubes.classification "SANDBOX_CONFIRMED" "Qubes handoff should be sandbox-confirmed."
Assert-Equal $qubes.real_qubes_core_generation_confirmed $false "Real Qubes/Core generation must not be claimed."
Assert-True (@($qubes.netted_usd_weights).Count -gt 0) "Qubes handoff rows missing."
Assert-Equal $drift.classification "SANDBOX_CONFIRMED" "Drift should be sandbox-confirmed."
Assert-True (@($drift.rows | Where-Object { $_.rebalance_required -eq $true }).Count -gt 0) "Drift must include rebalanced rows."
Assert-Equal $orders.classification "SANDBOX_CONFIRMED" "Orders should be sandbox-confirmed."
Assert-True (@($orders.orders).Count -gt 0) "Order targets missing."
Assert-True (@($orders.orders | Where-Object { $_.security_id -and $_.security_id_source_tag22 -ne "8" }).Count -eq 0) "Tag 22 must be 8 when SecurityID is present."
Assert-Equal $orders.zero_quantity_orders_excluded $true "Zero quantity orders must be excluded."
Assert-Equal $plan.classification "SANDBOX_CONFIRMED" "Execution plan should be sandbox-confirmed."
Assert-Equal $plan.no_live_routing $true "Execution plan must not route live."
Assert-Equal $plan.no_fix_api_call $true "Execution plan must not call FIX/API."
Assert-Equal $plan.no_broker_api_call $true "Execution plan must not call broker API."
Assert-Equal $fills.classification "SANDBOX_CONFIRMED" "Sandbox fills should be confirmed."
Assert-True (@($fills.fills).Count -gt 0) "Sandbox fills missing."
Assert-True (@($fills.fills | Where-Object { $_.sandbox -ne $true -or $_.simulated -ne $true }).Count -eq 0) "Fills must be labelled sandbox/simulated."
Assert-Equal $residual.residual_zero $true "Residuals must be zero."
Assert-True (@($residual.unfilled_lines | Where-Object { $_.symbol -eq "USDJPY" -and [decimal]$_.quantity -eq [decimal]50.0 }).Count -eq 1) "USDJPY 50.0 unfilled must be preserved."
Assert-Equal $tradeRecon.classification "SANDBOX_CONFIRMED" "Trade-level reconciliation should be sandbox-confirmed."
Assert-Equal $tradeRecon.sandbox_fills_reconciled $true "Sandbox fills must reconcile."
Assert-Equal $strategyPnl.classification "SANDBOX_CONFIRMED" "Strategy PnL should be sandbox-confirmed."
Assert-Equal $strategyPnl.broker_statement_pnl_overwritten $false "Broker PnL must not be overwritten."
Assert-Equal $strategyPnl.equals_lmax_statement $false "Front-half PnL must not be equated with LMAX statement."
Assert-Equal $bridge.front_half_sandbox_chain_confirmed $true "Bridge should confirm front-half sandbox chain."
Assert-Equal $bridge.back_half_broker_accounting_ledger_chain_confirmed $true "Bridge should confirm back-half reference."
Assert-Equal $bridge.full_real_front_to_back_chain_complete $false "Bridge must keep full real chain incomplete."
Assert-Equal $bridge.statement_periods_match $false "Bridge must not claim statement periods match."
Assert-Equal $bridge.broker_accounting_ledger_artifacts_downstream_of_this_front_half_run $false "Bridge must not claim back-half downstream of front-half run."
Assert-Equal $main.full_flow.sandbox_front_half_complete $true "Sandbox front-half should be complete."
Assert-Equal $main.full_flow.back_half_complete $true "Back-half should be complete."
Assert-Equal $main.full_flow.full_real_front_to_back_complete $false "Full real front-to-back must remain incomplete."
Assert-Equal $coverageAfter.flow_coverage.production_live_trading "BLOCKED" "Production/live/trading must remain blocked."

foreach ($flag in @("trading_activity", "r009_submission", "lmax_fix_api_call", "broker_api_call", "polygon_massive_call", "market_data_fetch", "broker_fetch", "account_data_fetch", "production_live_write", "production_live_ready", "trading_readiness_ready")) {
    Assert-Equal $main.global_guards.$flag $false "Global guard must remain false: $flag"
}

Write-Host "FRONT_HALF_QUBES_TO_ORDER_FILL_RECONCILIATION_SANDBOX_E2E_R001_TEST_PASS"
