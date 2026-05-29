param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$BuildScript = Join-Path $PSScriptRoot "build-lmax-sandbox-global-process-test-run-r001.ps1"
$PackageDir = Join-Path $RepoRoot "artifacts\readiness\lmax-sandbox-global-process-test-run-r001"

function Read-JsonFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Missing JSON artifact: $Path" }
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Assert-Equal($Expected, $Actual, [string]$Message) {
    if ($Expected -ne $Actual) {
        throw "$Message Expected=[$Expected] Actual=[$Actual]"
    }
}

& $BuildScript -RepoRoot $RepoRoot -ExecutionMode LmaxSandbox

$required = @(
    "lmax-sandbox-global-process-run-manifest-r001.json",
    "lmax-sandbox-market-data-basis-r001.json",
    "qubes-core-weight-handoff-r001.json",
    "drift-and-order-targets-r001.json",
    "lmax-order-manifest-r001.json",
    "execution-algo-plan-r001.json",
    "operator-approval-required-r001.json",
    "simulation-approval-status-r001.json",
    "operator-approval-lmax-demo-execution-status-r001.json",
    "lmax-demo-execution-switch-status-r001.json",
    "lmax-demo-execution-config-validation-r001.json",
    "lmax-sandbox-execution-harness-r001.json",
    "sandbox-simulated-fills-r001.json",
    "sandbox-execution-result-r001.json",
    "residual-flatten-report-r001.json",
    "sandbox-trade-level-reconciliation-r001.json",
    "sandbox-pnl-r001.json",
    "same-run-broker-evidence-instructions-r001.md",
    "lmax-sandbox-global-process-test-run-r001.json",
    "e2e-flow-coverage-after-lmax-sandbox-run-r001.json"
)

foreach ($artifact in $required) {
    Assert-True (Test-Path -LiteralPath (Join-Path $PackageDir $artifact)) "Required artifact missing: $artifact"
}

$main = Read-JsonFile (Join-Path $PackageDir "lmax-sandbox-global-process-test-run-r001.json")
$manifest = Read-JsonFile (Join-Path $PackageDir "lmax-sandbox-global-process-run-manifest-r001.json")
$market = Read-JsonFile (Join-Path $PackageDir "lmax-sandbox-market-data-basis-r001.json")
$qubes = Read-JsonFile (Join-Path $PackageDir "qubes-core-weight-handoff-r001.json")
$drift = Read-JsonFile (Join-Path $PackageDir "drift-and-order-targets-r001.json")
$orders = Read-JsonFile (Join-Path $PackageDir "lmax-order-manifest-r001.json")
$plan = Read-JsonFile (Join-Path $PackageDir "execution-algo-plan-r001.json")
$approval = Read-JsonFile (Join-Path $PackageDir "operator-approval-required-r001.json")
$lmaxDemoApproval = Read-JsonFile (Join-Path $PackageDir "operator-approval-lmax-demo-execution-status-r001.json")
$lmaxDemoSwitch = Read-JsonFile (Join-Path $PackageDir "lmax-demo-execution-switch-status-r001.json")
$lmaxDemoConfig = Read-JsonFile (Join-Path $PackageDir "lmax-demo-execution-config-validation-r001.json")
$harness = Read-JsonFile (Join-Path $PackageDir "lmax-sandbox-execution-harness-r001.json")
$simFills = Read-JsonFile (Join-Path $PackageDir "sandbox-simulated-fills-r001.json")
$residual = Read-JsonFile (Join-Path $PackageDir "residual-flatten-report-r001.json")
$execution = Read-JsonFile (Join-Path $PackageDir "sandbox-execution-result-r001.json")
$recon = Read-JsonFile (Join-Path $PackageDir "sandbox-trade-level-reconciliation-r001.json")
$pnl = Read-JsonFile (Join-Path $PackageDir "sandbox-pnl-r001.json")
$coverage = Read-JsonFile (Join-Path $PackageDir "e2e-flow-coverage-after-lmax-sandbox-run-r001.json")

Assert-True ($main.status -in @(
    "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_OPERATOR_APPROVAL_REQUIRED_R001",
    "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_EXECUTION_SWITCH_DISABLED_R001",
    "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_LMAX_SANDBOX_CONFIG_MISSING_R001",
    "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_KILL_SWITCH_ACTIVE_R001",
    "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_RISK_LIMITS_FAILED_R001",
    "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_FORBIDDEN_PRODUCTION_OR_LIVE_ACTIVITY_R001",
    "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_QUBES_HANDOFF_MISSING_R001",
    "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_MARKET_DATA_MISSING_R001",
    "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_ORDER_TARGETS_INVALID_R001",
    "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_EXECUTION_APPROVED_READY_R001",
    "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_EXECUTED_R001",
    "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_RECONCILED_R001",
    "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_SIMULATED_RECONCILED_R001"
)) "Unexpected package status."

Assert-True ($main.run_id -like "LMAX_SANDBOX_GLOBAL_TEST_R001_*") "Run ID must use required prefix."
Assert-Equal "sandbox" $manifest.environment "Manifest environment must be sandbox."
Assert-Equal "LMAX_DEMO_OR_SANDBOX" $manifest.venue "Manifest venue must be demo/sandbox scoped."
Assert-Equal $false $manifest.production_live "Manifest production_live must remain false."
Assert-Equal $false $market.external_calls "Market basis must not use external calls."
Assert-Equal $false $market.market_data_fetch "Market basis must not fetch live market data."
Assert-True (@($market.instruments).Count -gt 0) "Market data basis must include instruments."
Assert-True (@($qubes.netted_usd_weights).Count -gt 0) "Qubes/Core handoff must include target weights."
Assert-True (@($drift.order_targets).Count -gt 0) "Order targets must be created."
Assert-True (@($orders.orders).Count -gt 0) "Order manifest must contain orders."

foreach ($order in @($orders.orders)) {
    if (-not [string]::IsNullOrWhiteSpace([string]$order.security_id)) {
        Assert-Equal "8" $order.security_id_source_tag22 "FIX tag 22 must equal 8 when tag 48 SecurityID is present."
    }
    Assert-Equal $false $order.submit_allowed_without_approval "Orders must not be sendable without approval."
}

Assert-Equal $true $plan.no_production_routing "Execution plan must block production routing."
Assert-Equal $false $harness.raw_secrets_present "Execution harness must not expose raw secrets."
Assert-Equal $true $approval.approval_required "Operator approval must be required."
Assert-Equal $true $lmaxDemoConfig.no_raw_secrets_in_artifacts "LMAX demo config validation must reject raw secrets."
Assert-Equal $true $lmaxDemoConfig.production_endpoint_strings_rejected "Production endpoint strings must be rejected by policy."
Assert-Equal $true $lmaxDemoConfig.production_credentials_rejected "Production credential policies must be rejected by policy."
Assert-Equal $false $execution.lmax_fix_api_call "Execution result must not call LMAX."
Assert-Equal $false $execution.production_lmax_call "Execution result must not call production LMAX."
Assert-Equal $false $execution.broker_api_call "Execution result must not call broker API."
if ($main.status -eq "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_EXECUTION_APPROVED_READY_R001") {
    Assert-Equal "LMAX_DEMO_EXECUTION_APPROVAL_ACCEPTED_R001" $lmaxDemoApproval.status "Approved-ready mode requires accepted operator approval."
    Assert-Equal "LMAX_DEMO_EXECUTION_SWITCH_ACCEPTED_R001" $lmaxDemoSwitch.status "Approved-ready mode requires accepted execution switch."
    Assert-Equal "LMAX_DEMO_EXECUTION_CONFIG_VALID_R001" $lmaxDemoConfig.status "Approved-ready mode requires valid demo config."
    Assert-Equal "APPROVED_READY_NOT_EXECUTED_BY_BUILD_SCRIPT" $execution.status "Gate must not execute LMAX in test mode."
    Assert-Equal 0 $execution.orders_submitted_count "Approved-ready gate must not submit orders in test mode."
} elseif ($main.status -eq "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_SIMULATED_RECONCILED_R001") {
    Assert-Equal "simulated_only" $execution.status "Simulated package must remain simulation-only."
    Assert-True (@($simFills.fills).Count -gt 0) "Simulated fills must be generated."
    Assert-Equal $true $residual.residual_zero "Residual-zero must be achieved in simulation or block explicitly."
    Assert-Equal "SANDBOX_SIMULATED_TRADE_LEVEL_RECONCILIATION_READY_R001" $recon.status "Simulated trade-level reconciliation must be ready."
    Assert-Equal "SANDBOX_SIMULATED_STRATEGY_PNL_READY_R001" $pnl.status "Simulated strategy PnL must be ready."
    Assert-Equal "BLOCKED" $coverage.flow_coverage.same_run_broker_reconciliation "Same-run broker evidence must remain blocked until export exists."
} else {
    Assert-Equal 0 @($execution.orders_submitted).Count "Blocked gate must not have submitted orders."
    Assert-True ($recon.status -in @("BLOCKED_AWAITING_SANDBOX_EXECUTION_OR_SIMULATION", "SANDBOX_TRADE_LEVEL_RECONCILIATION_RECONCILED_R001")) "Trade-level reconciliation must exist or block explicitly."
    Assert-True ($pnl.status -in @("BLOCKED_AWAITING_SANDBOX_FILLS", "SANDBOX_STRATEGY_PNL_COMPUTED_R001")) "Strategy PnL must exist or block explicitly."
    Assert-Equal "BLOCKED_EXPORT_MISSING" $coverage.flow_coverage.same_run_broker_statement_reconciliation "Same-run broker evidence must remain blocked until export exists."
}

foreach ($name in @(
    "trading_activity",
    "r009_submission",
    "lmax_fix_api_call",
    "broker_api_call",
    "polygon_massive_call",
    "market_data_fetch",
    "broker_fetch",
    "account_data_fetch",
    "production_live_write",
    "production_live_ready",
    "trading_readiness_ready"
)) {
    Assert-Equal $false $main.global_guards.$name "Guard $name must remain false."
}

Assert-Equal $false $main.production_live "Production/live must remain false."
Assert-Equal $false $main.trading_readiness "Trading readiness must remain false."

Write-Host "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_R001_GATE_PASS"
