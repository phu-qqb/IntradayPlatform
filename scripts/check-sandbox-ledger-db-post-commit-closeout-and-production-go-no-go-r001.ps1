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

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\sandbox-ledger-db-post-commit-closeout-and-production-go-no-go-r001"
$SourceDir = Join-Path $RepoRoot "artifacts\readiness\sandbox-ledger-db-commit-execution-r001"
$CloseoutPath = Join-Path $ArtifactDir "sandbox-ledger-db-post-commit-closeout-r001.json"
$ManifestPath = Join-Path $ArtifactDir "sandbox-post-commit-evidence-manifest-r001.json"
$GoNoGoPath = Join-Path $ArtifactDir "production-live-go-no-go-r001.json"
$SummaryPath = Join-Path $ArtifactDir "sandbox-ledger-db-post-commit-closeout-summary-r001.md"
$SourceMainPath = Join-Path $SourceDir "sandbox-ledger-db-commit-execution-r001.json"
$SourceAuditPath = Join-Path $SourceDir "sandbox-ledger-db-commit-audit-r001.json"
$SourceIdempotencyPath = Join-Path $SourceDir "sandbox-ledger-db-idempotency-report-r001.json"
$SourceRollbackPath = Join-Path $SourceDir "sandbox-ledger-db-rollback-preview-r001.json"
$SourceStatePath = Join-Path $SourceDir "sandbox-db\sandbox-ledger-db-state-r001.json"

foreach ($path in @($CloseoutPath, $ManifestPath, $GoNoGoPath, $SummaryPath, $SourceMainPath, $SourceAuditPath, $SourceIdempotencyPath, $SourceRollbackPath, $SourceStatePath)) {
    Assert-True (Test-Path -LiteralPath $path) "Required artifact missing: $path"
}

$closeout = Read-JsonFile $CloseoutPath
$manifest = Read-JsonFile $ManifestPath
$goNoGo = Read-JsonFile $GoNoGoPath
$sourceMain = Read-JsonFile $SourceMainPath
$sourceAudit = Read-JsonFile $SourceAuditPath
$sourceIdempotency = Read-JsonFile $SourceIdempotencyPath
$sourceRollback = Read-JsonFile $SourceRollbackPath
$state = Read-JsonFile $SourceStatePath

Assert-Equal $closeout.status "SANDBOX_LEDGER_DB_POST_COMMIT_CLOSEOUT_READY_R001" "Post-commit closeout status mismatch."
Assert-True (@("SANDBOX_LEDGER_DB_COMMIT_EXECUTED_R001", "SANDBOX_LEDGER_DB_COMMIT_IDEMPOTENT_ALREADY_APPLIED_R001") -contains $closeout.source_commit_status) "Source commit status mismatch."
Assert-True (@("SANDBOX_LEDGER_DB_COMMIT_EXECUTED_R001", "SANDBOX_LEDGER_DB_COMMIT_IDEMPOTENT_ALREADY_APPLIED_R001") -contains $sourceMain.status) "Source main status mismatch."
Assert-Equal $closeout.idempotency_key $ExpectedKey "Closeout idempotency key mismatch."
Assert-Equal $sourceIdempotency.idempotency_key $ExpectedKey "Source idempotency key mismatch."
Assert-Equal $sourceIdempotency.same_key_same_hashes_and_values $true "Idempotency report hash/value match mismatch."

Assert-Equal $closeout.committed_row_counts.ledger_rows 6 "Closeout ledger row count mismatch."
Assert-Equal $closeout.committed_row_counts.db_rows 8 "Closeout DB row count mismatch."
Assert-Equal $sourceMain.ledger_rows_for_key 6 "Source ledger row count mismatch."
Assert-Equal $sourceMain.db_rows_for_key 8 "Source DB row count mismatch."

Assert-Equal $sourceAudit.audit_status "SANDBOX_AUDIT_WRITTEN" "Source audit status mismatch."
Assert-Equal $closeout.audit_verification.audit_status "SANDBOX_AUDIT_WRITTEN" "Closeout audit verification mismatch."
Assert-Equal $sourceRollback.rollback_preview_created $true "Rollback preview created mismatch."
Assert-Equal $sourceMain.rollback_preview_status "SANDBOX_ROLLBACK_PREVIEW_CREATED" "Source rollback preview status mismatch."
Assert-Equal $closeout.rollback_preview_verification.rollback_preview_status "SANDBOX_ROLLBACK_PREVIEW_CREATED" "Closeout rollback preview status mismatch."

Assert-DecimalEqual ([decimal]$closeout.committed_values.realized_pnl_before_costs_usd) ([decimal]6015.14) "Closeout realized PnL before costs mismatch."
Assert-DecimalEqual ([decimal]$closeout.committed_values.commission_expense_usd) ([decimal]225.63) "Closeout commission mismatch."
Assert-DecimalEqual ([decimal]$closeout.committed_values.financing_expense_usd) ([decimal]40.60) "Closeout financing mismatch."
Assert-DecimalEqual ([decimal]$closeout.committed_values.realized_net_after_costs_usd) ([decimal]5748.91) "Closeout realized net mismatch."
Assert-DecimalEqual ([decimal]$closeout.committed_values.unrealized_open_pnl_usd) ([decimal]463.61) "Closeout unrealized mismatch."
Assert-DecimalEqual ([decimal]$closeout.committed_values.equity_pnl_including_open_pnl_usd) ([decimal]6212.52) "Closeout equity mismatch."

$ledgerRows = @($state.ledger_journal_entries | Where-Object { $_.idempotency_key -eq $ExpectedKey })
$batchRows = @($state.ledger_commit_batches | Where-Object { $_.idempotency_key -eq $ExpectedKey })
$auditRows = @($state.accounting_close_audit | Where-Object { $_.idempotency_key -eq $ExpectedKey })
Assert-Equal $ledgerRows.Count 6 "Sandbox DB state ledger rows mismatch."
Assert-Equal $batchRows.Count 1 "Sandbox DB state batch rows mismatch."
Assert-Equal $batchRows[0].db_row_count 8 "Sandbox DB state DB rows mismatch."
Assert-Equal $auditRows.Count 1 "Sandbox DB state audit rows mismatch."

$requiredManifestNames = @(
    "raw_lmax_normalized_broker_statement",
    "broker_statement_confirmed_pnl",
    "accounting_dry_run",
    "accounting_close_acceptance",
    "ledger_db_commit_authorization",
    "sandbox_commit_execution",
    "sandbox_commit_audit",
    "sandbox_commit_idempotency_report",
    "sandbox_commit_rollback_preview",
    "sandbox_db_state"
)
foreach ($name in $requiredManifestNames) {
    $entry = @($manifest.source_artifacts | Where-Object { $_.name -eq $name })
    Assert-Equal $entry.Count 1 "Evidence manifest entry missing or duplicated: $name"
    Assert-True ($entry[0].sha256 -like "sha256:*") "Evidence manifest hash missing: $name"
}
Assert-Equal $manifest.sandbox_db_state_hashed $true "Evidence manifest must hash sandbox DB state."

Assert-Equal $closeout.readiness.sandbox_post_commit_closeout_ready $true "Closeout readiness mismatch."
Assert-Equal $closeout.readiness.sandbox_ledger_commit $true "Sandbox ledger commit mismatch."
Assert-Equal $closeout.readiness.sandbox_db_mutation $true "Sandbox DB mutation mismatch."
Assert-Equal $closeout.readiness.production_live_ready $false "Production/live ready must remain false."
Assert-Equal $closeout.readiness.trading_readiness $false "Trading readiness must remain false."

Assert-Equal $goNoGo.status "SANDBOX_POST_COMMIT_CLOSEOUT_READY_PRODUCTION_BLOCKED_R001" "Go/no-go status mismatch."
Assert-Equal $goNoGo.sandbox_closeout_ready $true "Go/no-go sandbox closeout readiness mismatch."
Assert-Equal $goNoGo.production_live_ready $false "Go/no-go production/live must remain false."
Assert-Equal $goNoGo.trading_readiness $false "Go/no-go trading readiness must remain false."
Assert-Equal $goNoGo.blocked_reason "PRODUCTION_LIVE_APPROVAL_NOT_REQUESTED_OR_GRANTED" "Go/no-go blocked reason mismatch."
Assert-True (@($goNoGo.required_before_production_live).Count -ge 12) "Go/no-go production requirements incomplete."

foreach ($flag in @("trading_activity", "r009_submission", "lmax_fix_api_call", "broker_api_call", "polygon_massive_call", "market_data_fetch", "broker_fetch", "account_data_fetch", "production_live_write", "production_live_ready", "trading_readiness_ready")) {
    Assert-Equal $sourceMain.global_guards.$flag $false "Source global guard must remain false: $flag"
    Assert-Equal $closeout.global_guards.$flag $false "Closeout global guard must remain false: $flag"
}

$scanFiles = @(
    (Join-Path $RepoRoot "scripts\build-sandbox-ledger-db-post-commit-closeout-and-production-go-no-go-r001.ps1"),
    (Join-Path $RepoRoot "scripts\test-sandbox-ledger-db-post-commit-closeout-and-production-go-no-go-r001.ps1")
)
$forbiddenPatterns = @("SubmitOrder", "SubmitOrderUnmanaged", "AtmStrategyCreate", "Invoke-WebRequest", "Invoke-RestMethod", "curl ", "wget ", "api_key", "apikey", "password")
foreach ($pattern in $forbiddenPatterns) {
    $matches = Select-String -Path $scanFiles -Pattern $pattern -SimpleMatch -ErrorAction SilentlyContinue
    if ($matches) {
        throw "Forbidden static pattern detected [$pattern]:`n$($matches | Out-String)"
    }
}

Write-Host "SANDBOX_LEDGER_DB_POST_COMMIT_CLOSEOUT_AND_PRODUCTION_GO_NO_GO_R001_GATE_PASS"
