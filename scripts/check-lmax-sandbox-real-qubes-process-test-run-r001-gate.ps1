param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$OutputSubdir = "lmax-sandbox-real-qubes-process-test-run-r001"
)

$ErrorActionPreference = "Stop"

$OutputDir = Join-Path $RepoRoot "artifacts\readiness\$OutputSubdir"
$MainPath = Join-Path $OutputDir "lmax-sandbox-real-qubes-process-test-run-r001.json"
$BindingPath = Join-Path $OutputDir "real-qubes-source-handoff-binding-r001.json"
$OrderManifestPath = Join-Path $OutputDir "real-qubes-lmax-order-manifest-r001.json"
$RunManifestPath = Join-Path $OutputDir "real-qubes-run-manifest-r001.json"
$ExecutionResultPath = Join-Path $OutputDir "sandbox-execution-result-r001.json"
$TradeReconPath = Join-Path $OutputDir "sandbox-trade-level-reconciliation-r001.json"
$PnlPath = Join-Path $OutputDir "sandbox-pnl-r001.json"

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Assert-In($Value, [object[]]$Allowed, [string]$Message) {
    if ($Allowed -notcontains $Value) { throw "$Message Value=[$Value]" }
}

function Read-JsonFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Missing expected artifact: $Path" }
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

$main = Read-JsonFile $MainPath
$binding = Read-JsonFile $BindingPath
$orders = Read-JsonFile $OrderManifestPath
$runManifest = Read-JsonFile $RunManifestPath
$execution = Read-JsonFile $ExecutionResultPath
$tradeRecon = Read-JsonFile $TradeReconPath
$pnl = Read-JsonFile $PnlPath

$allowedStatuses = @(
    "LMAX_SANDBOX_REAL_QUBES_PROCESS_TEST_RUN_BLOCKED_OPERATOR_APPROVAL_REQUIRED_R001",
    "LMAX_SANDBOX_REAL_QUBES_PROCESS_TEST_RUN_BLOCKED_EXECUTION_SWITCH_REQUIRED_R001",
    "LMAX_SANDBOX_REAL_QUBES_PROCESS_TEST_RUN_BLOCKED_LMAX_SANDBOX_CONFIG_REQUIRED_R001",
    "LMAX_SANDBOX_REAL_QUBES_PROCESS_TEST_RUN_BLOCKED_ACTUAL_ADAPTER_BINDING_REQUIRED_R001",
    "LMAX_SANDBOX_REAL_QUBES_PROCESS_TEST_RUN_EXECUTION_APPROVED_READY_R001",
    "LMAX_SANDBOX_REAL_QUBES_PROCESS_TEST_RUN_RECONCILED_R001",
    "LMAX_SANDBOX_EXECUTION_BLOCKED_DUPLICATE_CLORDID_R001",
    "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_RESIDUAL_NONZERO_R001"
)

Assert-In $main.status $allowedStatuses "Unexpected real-Qubes LMAX sandbox run status."
Assert-True ($main.run_id -match "^LMAX_SANDBOX_REAL_QUBES_TEST_R001_\d{8}T\d{6}Z$") "Run ID must use the real-Qubes LMAX sandbox prefix."
Assert-True ($main.old_run_id -eq "LMAX_SANDBOX_GLOBAL_TEST_R001_20260529T125324Z") "Old run ID reference is missing."
Assert-True ($main.old_run_overwritten -eq $false) "Old run must not be marked overwritten."
Assert-True ($main.generated_by_qubes_core -eq $true) "Real Qubes generated flag must be true."
Assert-True ($main.synthetic_fixture -eq $false) "Synthetic fixture flag must be false."
Assert-True ($main.real_qubes_handoff_accepted -eq $true) "Real Qubes handoff must be accepted."
Assert-True ([int]$main.order_count -eq 7) "Real-Qubes LMAX manifest must contain seven orders."
Assert-True ($main.same_run_broker_evidence_status -eq "BLOCKED_SAME_RUN_BROKER_EXPORT_MISSING") "Same-run broker evidence must remain blocked."
Assert-True ($main.production_live -eq $false) "Production/live must remain false."
Assert-True ($main.trading_readiness -eq $false) "Trading readiness must remain false."
Assert-True ($main.global_guards.lmax_fix_api_call -eq $false) "Build/check artifacts must not indicate an LMAX FIX/API call."
Assert-True ($main.global_guards.broker_api_call -eq $false) "Broker API call guard must remain false."
Assert-True ($main.global_guards.market_data_fetch -eq $false) "Market-data fetch guard must remain false."
Assert-True ($main.global_guards.production_live_ready -eq $false) "Production/live readiness must remain false."
Assert-True ($main.global_guards.trading_readiness_ready -eq $false) "Trading readiness must remain false."

Assert-True ($binding.real_qubes_core_output_accepted -eq $true) "Source binding must accept the staged real Qubes/Core handoff."
Assert-True ($binding.generated_by_qubes_core -eq $true) "Source binding must preserve generated_by_qubes_core true."
Assert-True ($binding.synthetic_fixture -eq $false) "Source binding must preserve synthetic_fixture false."
Assert-True ([int]$binding.order_count -eq 7) "Source binding must see seven preview orders."
Assert-True (-not [string]::IsNullOrWhiteSpace([string]$binding.source_handoff_hash)) "Source binding must include handoff hash."

Assert-True ($orders.status -eq "REAL_QUBES_LMAX_ORDER_MANIFEST_READY_PREVIEW_ONLY_R001") "Order manifest must be preview-only ready."
Assert-True ([int]$orders.order_count -eq 7) "Order manifest order count must be seven."
foreach ($order in @($orders.orders)) {
    Assert-True ($order.run_id -eq $main.run_id) "Each order must be rebound to the new run_id."
    Assert-True ($order.production_live -eq $false) "Each order must remain sandbox/non-production."
    if (-not [string]::IsNullOrWhiteSpace([string]$order.security_id)) {
        Assert-True ([string]$order.security_id_source_tag22 -eq "8") "Tag 22 must equal 8 when tag 48/security_id exists."
    }
}

Assert-True ($runManifest.production_live -eq $false) "Run manifest must keep production_live false."
Assert-True ($runManifest.trading_readiness -eq $false) "Run manifest must keep trading_readiness false."
Assert-True ($execution.production_lmax_call -eq $false) "Execution result must never indicate production LMAX call."
Assert-True ($execution.production_live -eq $false) "Execution result must keep production_live false."
Assert-True ($execution.trading_readiness -eq $false) "Execution result must keep trading_readiness false."
Assert-True ($tradeRecon.uses_historical_lmax_statement -ne $true) "Trade reconciliation must not claim historical broker statement evidence."
Assert-True ($pnl.broker_statement_pnl_comparison.applicable -ne $true) "Strategy PnL must not use historical LMAX statement as same-run evidence."

Write-Host "PASS: real-Qubes LMAX sandbox process gate passed for status $($main.status)."
