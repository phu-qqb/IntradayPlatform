param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$ExpectedKey = "lmax-921640160-2025-11-03-ledger-db-commit-r001"
$Tolerance = [decimal]0.000001

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Assert-Equal($Actual, $Expected, [string]$Message) {
    if ($Actual -ne $Expected) { throw "$Message Expected=[$Expected] Actual=[$Actual]" }
}

function Assert-DecimalEqual([decimal]$Actual, [decimal]$Expected, [string]$Message) {
    if ([Math]::Abs($Actual - $Expected) -gt $Tolerance) {
        throw "$Message Expected=[$Expected] Actual=[$Actual]"
    }
}

function Read-JsonFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Required artifact missing: $Path" }
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

$builder = Join-Path $RepoRoot "scripts\build-sandbox-ledger-db-post-commit-closeout-and-production-go-no-go-r001.ps1"
$gate = Join-Path $RepoRoot "scripts\check-sandbox-ledger-db-post-commit-closeout-and-production-go-no-go-r001.ps1"

& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot | Out-Null
& powershell -ExecutionPolicy Bypass -File $gate -RepoRoot $RepoRoot | Out-Null

$artifactDir = Join-Path $RepoRoot "artifacts\readiness\sandbox-ledger-db-post-commit-closeout-and-production-go-no-go-r001"
$sourceDir = Join-Path $RepoRoot "artifacts\readiness\sandbox-ledger-db-commit-execution-r001"
$closeout = Read-JsonFile (Join-Path $artifactDir "sandbox-ledger-db-post-commit-closeout-r001.json")
$manifest = Read-JsonFile (Join-Path $artifactDir "sandbox-post-commit-evidence-manifest-r001.json")
$goNoGo = Read-JsonFile (Join-Path $artifactDir "production-live-go-no-go-r001.json")
$sourceMain = Read-JsonFile (Join-Path $sourceDir "sandbox-ledger-db-commit-execution-r001.json")
$sourceIdempotency = Read-JsonFile (Join-Path $sourceDir "sandbox-ledger-db-idempotency-report-r001.json")
$sourceAudit = Read-JsonFile (Join-Path $sourceDir "sandbox-ledger-db-commit-audit-r001.json")
$sourceRollback = Read-JsonFile (Join-Path $sourceDir "sandbox-ledger-db-rollback-preview-r001.json")
$state = Read-JsonFile (Join-Path $sourceDir "sandbox-db\sandbox-ledger-db-state-r001.json")

Assert-True (@("SANDBOX_LEDGER_DB_COMMIT_EXECUTED_R001", "SANDBOX_LEDGER_DB_COMMIT_IDEMPOTENT_ALREADY_APPLIED_R001") -contains $sourceMain.status) "Source status must be executed or idempotent."
Assert-Equal $sourceIdempotency.idempotency_key $ExpectedKey "Idempotency key mismatch."
Assert-Equal $closeout.idempotency_key $ExpectedKey "Closeout idempotency key mismatch."
Assert-Equal $sourceMain.ledger_rows_for_key 6 "Source ledger row count mismatch."
Assert-Equal $sourceMain.db_rows_for_key 8 "Source DB row count mismatch."
Assert-Equal $closeout.committed_row_counts.ledger_rows 6 "Closeout ledger row count mismatch."
Assert-Equal $closeout.committed_row_counts.db_rows 8 "Closeout DB row count mismatch."
Assert-Equal $sourceAudit.audit_status "SANDBOX_AUDIT_WRITTEN" "Audit status mismatch."
Assert-Equal $sourceRollback.rollback_preview_created $true "Rollback preview must be created."
Assert-Equal $sourceMain.rollback_preview_status "SANDBOX_ROLLBACK_PREVIEW_CREATED" "Rollback preview status mismatch."

$ledgerRows = @($state.ledger_journal_entries | Where-Object { $_.idempotency_key -eq $ExpectedKey })
$batchRows = @($state.ledger_commit_batches | Where-Object { $_.idempotency_key -eq $ExpectedKey })
$auditRows = @($state.accounting_close_audit | Where-Object { $_.idempotency_key -eq $ExpectedKey })
Assert-Equal $ledgerRows.Count 6 "State ledger row count mismatch."
Assert-Equal $batchRows.Count 1 "State batch row count mismatch."
Assert-Equal $batchRows[0].db_row_count 8 "State DB row count mismatch."
Assert-Equal $auditRows.Count 1 "State audit row count mismatch."

Assert-DecimalEqual ([decimal]$closeout.committed_values.realized_pnl_before_costs_usd) ([decimal]6015.14) "Realized PnL mismatch."
Assert-DecimalEqual ([decimal]$closeout.committed_values.commission_expense_usd) ([decimal]225.63) "Commission mismatch."
Assert-DecimalEqual ([decimal]$closeout.committed_values.financing_expense_usd) ([decimal]40.60) "Financing mismatch."
Assert-DecimalEqual ([decimal]$closeout.committed_values.realized_net_after_costs_usd) ([decimal]5748.91) "Realized net mismatch."
Assert-DecimalEqual ([decimal]$closeout.committed_values.unrealized_open_pnl_usd) ([decimal]463.61) "Unrealized PnL mismatch."
Assert-DecimalEqual ([decimal]$closeout.committed_values.equity_pnl_including_open_pnl_usd) ([decimal]6212.52) "Equity PnL mismatch."

Assert-Equal $closeout.status "SANDBOX_LEDGER_DB_POST_COMMIT_CLOSEOUT_READY_R001" "Closeout status mismatch."
Assert-Equal $closeout.readiness.sandbox_post_commit_closeout_ready $true "Sandbox closeout ready mismatch."
Assert-Equal $manifest.manifest_status "SANDBOX_POST_COMMIT_EVIDENCE_MANIFEST_FROZEN_R001" "Manifest status mismatch."
Assert-True (@($manifest.source_artifacts).Count -ge 10) "Manifest must hash all required source artifacts."
foreach ($entry in @($manifest.source_artifacts)) {
    Assert-True ($entry.sha256 -like "sha256:*") "Manifest entry missing sha256: $($entry.name)"
}

Assert-Equal $goNoGo.status "SANDBOX_POST_COMMIT_CLOSEOUT_READY_PRODUCTION_BLOCKED_R001" "Production go/no-go status mismatch."
Assert-Equal $goNoGo.sandbox_closeout_ready $true "Go/no-go sandbox closeout ready mismatch."
Assert-Equal $goNoGo.production_live_ready $false "Production/live must remain false."
Assert-Equal $goNoGo.trading_readiness $false "Trading readiness must remain false."

Assert-Equal $closeout.global_guards.trading_activity $false "Trading activity must remain false."
Assert-Equal $closeout.global_guards.r009_submission $false "R009 submission must remain false."
Assert-Equal $closeout.global_guards.lmax_fix_api_call $false "LMAX FIX/API must remain false."
Assert-Equal $closeout.global_guards.broker_api_call $false "Broker API must remain false."
Assert-Equal $closeout.global_guards.polygon_massive_call $false "Polygon/Massive must remain false."
Assert-Equal $closeout.global_guards.market_data_fetch $false "Market data fetch must remain false."
Assert-Equal $closeout.global_guards.broker_fetch $false "Broker fetch must remain false."
Assert-Equal $closeout.global_guards.account_data_fetch $false "Account fetch must remain false."
Assert-Equal $closeout.global_guards.production_live_write $false "Production/live write must remain false."
Assert-Equal $closeout.global_guards.production_live_ready $false "Production/live readiness must remain false."
Assert-Equal $closeout.global_guards.trading_readiness_ready $false "Trading readiness must remain false."

Write-Host "SANDBOX_LEDGER_DB_POST_COMMIT_CLOSEOUT_AND_PRODUCTION_GO_NO_GO_R001_TEST_PASS"
