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

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\sandbox-ledger-db-commit-execution-r001"
$AuthGatePath = Join-Path $RepoRoot "artifacts\readiness\ledger-db-commit-authorization-gate-r001\ledger-db-commit-authorization-gate-r001.json"
$MainPath = Join-Path $ArtifactDir "sandbox-ledger-db-commit-execution-r001.json"
$AuditPath = Join-Path $ArtifactDir "sandbox-ledger-db-commit-audit-r001.json"
$IdempotencyPath = Join-Path $ArtifactDir "sandbox-ledger-db-idempotency-report-r001.json"
$RollbackPath = Join-Path $ArtifactDir "sandbox-ledger-db-rollback-preview-r001.json"
$SummaryPath = Join-Path $ArtifactDir "sandbox-ledger-db-commit-summary-r001.md"

foreach ($path in @($AuthGatePath, $MainPath, $AuditPath, $IdempotencyPath, $RollbackPath, $SummaryPath)) {
    Assert-True (Test-Path -LiteralPath $path) "Required artifact missing: $path"
}

$authGate = Read-JsonFile $AuthGatePath
$main = Read-JsonFile $MainPath
$audit = Read-JsonFile $AuditPath
$idempotency = Read-JsonFile $IdempotencyPath
$rollback = Read-JsonFile $RollbackPath

Assert-Equal $authGate.status "LEDGER_DB_COMMIT_AUTHORIZATION_READY_R001" "Authorization gate must be ready."
Assert-Equal $authGate.readiness.ledger_db_commit_ready_for_future_commit_package $true "Authorization must mark future commit package ready."
Assert-True (@("SANDBOX_LEDGER_DB_COMMIT_EXECUTED_R001", "SANDBOX_LEDGER_DB_COMMIT_IDEMPOTENT_ALREADY_APPLIED_R001") -contains $main.status) "Main status mismatch."
Assert-True (-not [string]::IsNullOrWhiteSpace($main.idempotency_key)) "Idempotency key missing."
Assert-True (-not [string]::IsNullOrWhiteSpace($main.commit_fingerprint)) "Commit fingerprint missing."

Assert-DecimalEqual ([decimal]$main.values.realized_pnl_before_costs_usd) ([decimal]6015.14) "Realized PnL mismatch."
Assert-DecimalEqual ([decimal]$main.values.commission_expense_usd) ([decimal]225.63) "Commission mismatch."
Assert-DecimalEqual ([decimal]$main.values.financing_expense_usd) ([decimal]40.60) "Financing mismatch."
Assert-DecimalEqual ([decimal]$main.values.realized_net_after_costs_usd) ([decimal]5748.91) "Realized net mismatch."
Assert-DecimalEqual ([decimal]$main.values.unrealized_open_pnl_usd) ([decimal]463.61) "Unrealized PnL mismatch."
Assert-DecimalEqual ([decimal]$main.values.equity_pnl_including_open_pnl_usd) ([decimal]6212.52) "Equity PnL mismatch."
Assert-DecimalEqual (([decimal]$main.values.realized_pnl_before_costs_usd) - ([decimal]$main.values.commission_expense_usd) - ([decimal]$main.values.financing_expense_usd)) ([decimal]$main.values.realized_net_after_costs_usd) "Realized net formula mismatch."
Assert-DecimalEqual (([decimal]$main.values.realized_net_after_costs_usd) + ([decimal]$main.values.unrealized_open_pnl_usd)) ([decimal]$main.values.equity_pnl_including_open_pnl_usd) "Equity formula mismatch."

Assert-Equal $main.readiness.sandbox_ledger_commit $true "Sandbox ledger commit readiness must be true."
Assert-Equal $main.readiness.sandbox_db_mutation $true "Sandbox DB mutation readiness must be true."
Assert-Equal $main.readiness.production_live $false "Production/live must remain false."
Assert-Equal $main.readiness.trading_readiness $false "Trading readiness must remain false."
Assert-Equal $main.ledger_rows_for_key 6 "Ledger rows for key mismatch."
Assert-Equal $main.db_rows_for_key 8 "DB rows for key mismatch."
Assert-True (($main.rows_inserted -eq 8 -and $main.status -eq "SANDBOX_LEDGER_DB_COMMIT_EXECUTED_R001") -or ($main.rows_already_present -eq 8 -and $main.status -eq "SANDBOX_LEDGER_DB_COMMIT_IDEMPOTENT_ALREADY_APPLIED_R001")) "Rows inserted/already-present mismatch."

Assert-Equal $audit.audit_artifact_created $true "Audit artifact must be created."
Assert-Equal $audit.db_audit_row_written $true "DB audit row must be written."
Assert-Equal $audit.sandbox_ledger_commit $true "Audit must record sandbox ledger commit."
Assert-Equal $audit.sandbox_db_mutation $true "Audit must record sandbox DB mutation."
Assert-Equal $audit.production_live $false "Audit production/live must remain false."
Assert-Equal $audit.trading_readiness $false "Audit trading readiness must remain false."

Assert-Equal $idempotency.idempotency_key $main.idempotency_key "Idempotency key mismatch."
Assert-Equal $idempotency.commit_fingerprint $main.commit_fingerprint "Idempotency fingerprint mismatch."
Assert-Equal $idempotency.ledger_rows_for_key 6 "Idempotency ledger row count mismatch."
Assert-Equal $idempotency.commit_batch_rows_for_key 1 "Idempotency batch row count mismatch."
Assert-Equal $idempotency.audit_rows_for_key 1 "Idempotency audit row count mismatch."
Assert-Equal $idempotency.same_key_same_hashes_and_values $true "Idempotency hash/value match must be true."

Assert-Equal $rollback.rollback_preview_created $true "Rollback preview must be created."
Assert-Equal $rollback.rollback_executes_now $false "Rollback must not execute now."
Assert-Equal $rollback.ledger_commit $false "Rollback preview must not commit ledger."
Assert-Equal $rollback.db_mutation $false "Rollback preview must not mutate DB."
Assert-Equal @($rollback.reversal_entries).Count 6 "Rollback preview reversal entry count mismatch."

Assert-Equal $main.global_guards.trading_activity $false "Trading activity must remain false."
Assert-Equal $main.global_guards.r009_submission $false "R009 submission must remain false."
Assert-Equal $main.global_guards.lmax_fix_api_call $false "LMAX FIX/API call must remain false."
Assert-Equal $main.global_guards.broker_api_call $false "Broker API call must remain false."
Assert-Equal $main.global_guards.polygon_massive_call $false "Polygon/Massive call must remain false."
Assert-Equal $main.global_guards.market_data_fetch $false "Market-data fetch must remain false."
Assert-Equal $main.global_guards.broker_fetch $false "Broker fetch must remain false."
Assert-Equal $main.global_guards.account_data_fetch $false "Account-data fetch must remain false."
Assert-Equal $main.global_guards.production_live_write $false "Production/live write must remain false."
Assert-Equal $main.global_guards.production_live_ready $false "Production/live readiness must remain false."
Assert-Equal $main.global_guards.trading_readiness_ready $false "Trading readiness must remain false."

$scanFiles = @(
    (Join-Path $RepoRoot "scripts\build-sandbox-ledger-db-commit-execution-r001.ps1"),
    (Join-Path $RepoRoot "scripts\test-sandbox-ledger-db-commit-execution-r001.ps1")
)
$forbiddenPatterns = @("SubmitOrder", "SubmitOrderUnmanaged", "AtmStrategyCreate", "Invoke-WebRequest", "Invoke-RestMethod", "curl ", "wget ", "api_key", "apikey", "password")
foreach ($pattern in $forbiddenPatterns) {
    $matches = Select-String -Path $scanFiles -Pattern $pattern -SimpleMatch -ErrorAction SilentlyContinue
    if ($matches) {
        throw "Forbidden call/secret/static pattern detected [$pattern]:`n$($matches | Out-String)"
    }
}

Write-Host "SANDBOX_LEDGER_DB_COMMIT_EXECUTION_R001_GATE_PASS"
