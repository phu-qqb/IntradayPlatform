param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$Package = "NEXT_SANDBOX_LEDGER_DB_POST_COMMIT_CLOSEOUT_AND_PRODUCTION_GO_NO_GO_R001"
$OutputDir = Join-Path $RepoRoot "artifacts\readiness\sandbox-ledger-db-post-commit-closeout-and-production-go-no-go-r001"
$CommitDir = Join-Path $RepoRoot "artifacts\readiness\sandbox-ledger-db-commit-execution-r001"
$ExpectedKey = "lmax-921640160-2025-11-03-ledger-db-commit-r001"
$Tolerance = [decimal]0.000001

function Read-JsonFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Required artifact missing: $Path" }
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Write-JsonFile([string]$Path, [object]$Value) {
    $parent = Split-Path -Parent $Path
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
    $Value | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Write-TextFile([string]$Path, [string]$Value) {
    $parent = Split-Path -Parent $Path
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
    Set-Content -LiteralPath $Path -Value $Value -Encoding UTF8
}

function Get-Sha256([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Cannot hash missing artifact: $Path" }
    "sha256:$((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash)"
}

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

function Add-ManifestEntry([System.Collections.ArrayList]$Entries, [string]$Name, [string]$Path, [hashtable]$Extra = @{}) {
    $entry = [ordered]@{
        name = $Name
        path = $Path
        sha256 = Get-Sha256 $Path
    }
    foreach ($key in $Extra.Keys) { $entry[$key] = $Extra[$key] }
    [void]$Entries.Add($entry)
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$mainPath = Join-Path $CommitDir "sandbox-ledger-db-commit-execution-r001.json"
$auditPath = Join-Path $CommitDir "sandbox-ledger-db-commit-audit-r001.json"
$idempotencyPath = Join-Path $CommitDir "sandbox-ledger-db-idempotency-report-r001.json"
$rollbackPath = Join-Path $CommitDir "sandbox-ledger-db-rollback-preview-r001.json"
$summaryPath = Join-Path $CommitDir "sandbox-ledger-db-commit-summary-r001.md"
$stateDir = Join-Path $CommitDir "sandbox-db"
$statePath = Join-Path $stateDir "sandbox-ledger-db-state-r001.json"

$main = Read-JsonFile $mainPath
$audit = Read-JsonFile $auditPath
$idempotency = Read-JsonFile $idempotencyPath
$rollback = Read-JsonFile $rollbackPath
$state = Read-JsonFile $statePath
if (-not (Test-Path -LiteralPath $summaryPath)) { throw "Required summary missing: $summaryPath" }

$allowedStatuses = @(
    "SANDBOX_LEDGER_DB_COMMIT_EXECUTED_R001",
    "SANDBOX_LEDGER_DB_COMMIT_IDEMPOTENT_ALREADY_APPLIED_R001"
)
Assert-True ($allowedStatuses -contains $main.status) "Commit execution status must be executed or idempotent already applied."
Assert-Equal $main.idempotency_key $ExpectedKey "Commit execution idempotency key mismatch."
Assert-Equal $idempotency.idempotency_key $ExpectedKey "Idempotency report key mismatch."
Assert-Equal $idempotency.same_key_same_hashes_and_values $true "Idempotency report must confirm same hashes and values."

Assert-DecimalEqual ([decimal]$main.values.realized_pnl_before_costs_usd) ([decimal]6015.14) "Realized PnL before costs mismatch."
Assert-DecimalEqual ([decimal]$main.values.commission_expense_usd) ([decimal]225.63) "Commission expense mismatch."
Assert-DecimalEqual ([decimal]$main.values.financing_expense_usd) ([decimal]40.60) "Financing expense mismatch."
Assert-DecimalEqual ([decimal]$main.values.realized_net_after_costs_usd) ([decimal]5748.91) "Realized net after costs mismatch."
Assert-DecimalEqual ([decimal]$main.values.unrealized_open_pnl_usd) ([decimal]463.61) "Unrealized open PnL mismatch."
Assert-DecimalEqual ([decimal]$main.values.equity_pnl_including_open_pnl_usd) ([decimal]6212.52) "Equity PnL including open PnL mismatch."

Assert-Equal $main.ledger_rows_for_key 6 "Main artifact ledger row count mismatch."
Assert-Equal $main.db_rows_for_key 8 "Main artifact DB row count mismatch."
Assert-Equal $audit.audit_status "SANDBOX_AUDIT_WRITTEN" "Audit status mismatch."
Assert-Equal $main.audit_status "SANDBOX_AUDIT_WRITTEN" "Main artifact audit status mismatch."
Assert-Equal $main.rollback_preview_status "SANDBOX_ROLLBACK_PREVIEW_CREATED" "Main artifact rollback status mismatch."
Assert-Equal $rollback.rollback_preview_created $true "Rollback preview must be created."
Assert-Equal $rollback.rollback_executes_now $false "Rollback preview must not execute."

Assert-Equal $main.readiness.sandbox_ledger_commit $true "Sandbox ledger commit must be true after commit execution."
Assert-Equal $main.readiness.sandbox_db_mutation $true "Sandbox DB mutation must be true after commit execution."
Assert-Equal $main.readiness.production_live $false "Production/live must remain false."
Assert-Equal $main.readiness.trading_readiness $false "Trading readiness must remain false."

foreach ($flag in @("trading_activity", "r009_submission", "lmax_fix_api_call", "broker_api_call", "polygon_massive_call", "market_data_fetch", "broker_fetch", "account_data_fetch", "production_live_write", "production_live_ready", "trading_readiness_ready")) {
    Assert-Equal $main.global_guards.$flag $false "Global guard must remain false: $flag"
}

$ledgerRows = @($state.ledger_journal_entries | Where-Object { $_.idempotency_key -eq $ExpectedKey })
$idempotencyRows = @($state.idempotency | Where-Object { $_.idempotency_key -eq $ExpectedKey })
$batchRows = @($state.ledger_commit_batches | Where-Object { $_.idempotency_key -eq $ExpectedKey })
$auditRows = @($state.accounting_close_audit | Where-Object { $_.idempotency_key -eq $ExpectedKey })

Assert-Equal $ledgerRows.Count 6 "Sandbox DB state ledger row count mismatch."
Assert-Equal $idempotencyRows.Count 1 "Sandbox DB state idempotency row count mismatch."
Assert-Equal $batchRows.Count 1 "Sandbox DB state batch row count mismatch."
Assert-Equal $auditRows.Count 1 "Sandbox DB state audit row count mismatch."
Assert-Equal $batchRows[0].db_row_count 8 "Sandbox DB state DB row count mismatch."
Assert-Equal $batchRows[0].ledger_row_count 6 "Sandbox DB state batch ledger row count mismatch."
Assert-Equal $batchRows[0].production_live $false "Sandbox DB state production/live must remain false."
Assert-Equal $batchRows[0].trading_readiness $false "Sandbox DB state trading readiness must remain false."

$sourceHashes = [ordered]@{
    sandbox_ledger_db_commit_execution = Get-Sha256 $mainPath
    sandbox_ledger_db_commit_audit = Get-Sha256 $auditPath
    sandbox_ledger_db_idempotency_report = Get-Sha256 $idempotencyPath
    sandbox_ledger_db_rollback_preview = Get-Sha256 $rollbackPath
    sandbox_ledger_db_commit_summary = Get-Sha256 $summaryPath
    sandbox_db_state = Get-Sha256 $statePath
}

$committedValues = [ordered]@{
    realized_pnl_before_costs_usd = [decimal]6015.14
    commission_expense_usd = [decimal]225.63
    financing_expense_usd = [decimal]40.60
    realized_net_after_costs_usd = [decimal]5748.91
    unrealized_open_pnl_usd = [decimal]463.61
    equity_pnl_including_open_pnl_usd = [decimal]6212.52
}

$closeout = [ordered]@{
    package = $Package
    status = "SANDBOX_LEDGER_DB_POST_COMMIT_CLOSEOUT_READY_R001"
    environment = "sandbox"
    mode = "post_commit_closeout_read_only"
    source_package = "NEXT_SANDBOX_LEDGER_DB_COMMIT_EXECUTION_R001"
    source_commit_status = $main.status
    idempotency_key = $ExpectedKey
    commit_fingerprint = $main.commit_fingerprint
    committed_row_counts = [ordered]@{
        rows_inserted = $main.rows_inserted
        rows_already_present = $main.rows_already_present
        ledger_rows = 6
        db_rows = 8
        idempotency_rows = $idempotencyRows.Count
        commit_batch_rows = $batchRows.Count
        audit_rows = $auditRows.Count
    }
    audit_verification = [ordered]@{
        audit_status = "SANDBOX_AUDIT_WRITTEN"
        audit_artifact_exists = $true
        db_audit_rows_for_key = $auditRows.Count
    }
    rollback_preview_verification = [ordered]@{
        rollback_preview_status = "SANDBOX_ROLLBACK_PREVIEW_CREATED"
        rollback_preview_created = $true
        rollback_executes_now = $false
        reversal_entry_count = @($rollback.reversal_entries).Count
    }
    source_artifact_hashes = $sourceHashes
    committed_values = $committedValues
    sandbox_db_state = [ordered]@{
        path = $statePath
        sha256 = Get-Sha256 $statePath
        exists = $true
        ledger_rows_verified = 6
        db_rows_verified = 8
    }
    readiness = [ordered]@{
        sandbox_post_commit_closeout_ready = $true
        sandbox_ledger_commit = $true
        sandbox_db_mutation = $true
        production_live_ready = $false
        trading_readiness = $false
    }
    global_guards = [ordered]@{
        trading_activity = $false
        r009_submission = $false
        lmax_fix_api_call = $false
        broker_api_call = $false
        polygon_massive_call = $false
        market_data_fetch = $false
        broker_fetch = $false
        account_data_fetch = $false
        production_live_write = $false
        production_live_ready = $false
        trading_readiness_ready = $false
    }
}

$manifestEntries = [System.Collections.ArrayList]::new()
Add-ManifestEntry $manifestEntries "raw_lmax_normalized_broker_statement" (Join-Path $RepoRoot "artifacts\readiness\real-manual-evidence-acceptance-r001\real-manual-broker-statement-normalized-from-lmax-raw-r001.json")
Add-ManifestEntry $manifestEntries "broker_statement_confirmed_pnl" (Join-Path $RepoRoot "artifacts\readiness\broker-statement-confirmed-pnl-r001\broker-statement-confirmed-pnl-r001.json")
Add-ManifestEntry $manifestEntries "accounting_dry_run" (Join-Path $RepoRoot "artifacts\readiness\broker-statement-accounting-dry-run-and-close-gate-r001\broker-statement-accounting-dry-run-and-close-gate-r001.json")
Add-ManifestEntry $manifestEntries "accounting_close_acceptance" (Join-Path $RepoRoot "artifacts\readiness\real-accounting-evidence-and-close-acceptance-r001\real-accounting-evidence-and-close-acceptance-r001.json")
Add-ManifestEntry $manifestEntries "ledger_db_commit_authorization" (Join-Path $RepoRoot "artifacts\readiness\ledger-db-commit-authorization-gate-r001\ledger-db-commit-authorization-gate-r001.json")
Add-ManifestEntry $manifestEntries "sandbox_commit_execution" $mainPath
Add-ManifestEntry $manifestEntries "sandbox_commit_audit" $auditPath
Add-ManifestEntry $manifestEntries "sandbox_commit_idempotency_report" $idempotencyPath
Add-ManifestEntry $manifestEntries "sandbox_commit_rollback_preview" $rollbackPath
Add-ManifestEntry $manifestEntries "sandbox_db_state" $statePath

Get-ChildItem -LiteralPath $stateDir -File | ForEach-Object {
    if ($_.FullName -ne $statePath) {
        Add-ManifestEntry $manifestEntries "sandbox_db_state_file" $_.FullName
    }
}

$manifest = [ordered]@{
    package = $Package
    artifact_type = "sandbox_post_commit_evidence_manifest_r001"
    environment = "sandbox"
    manifest_status = "SANDBOX_POST_COMMIT_EVIDENCE_MANIFEST_FROZEN_R001"
    source_artifacts = $manifestEntries
    source_artifact_count = $manifestEntries.Count
    sandbox_db_state_hashed = $true
    external_calls = $false
    production_live_ready = $false
    trading_readiness = $false
}

$goNoGo = [ordered]@{
    package = $Package
    status = "SANDBOX_POST_COMMIT_CLOSEOUT_READY_PRODUCTION_BLOCKED_R001"
    sandbox_closeout_ready = $true
    production_live_ready = $false
    trading_readiness = $false
    blocked_reason = "PRODUCTION_LIVE_APPROVAL_NOT_REQUESTED_OR_GRANTED"
    required_before_production_live = @(
        "production risk limits approval",
        "live credentials approval",
        "live venue approval",
        "live order routing approval",
        "live market-data policy approval",
        "kill switch validation",
        "monitoring approval",
        "incident response approval",
        "operator approval workflow",
        "production ledger/DB policy",
        "compliance review",
        "final change approval"
    )
    global_guards = [ordered]@{
        trading_activity = $false
        r009_submission = $false
        lmax_fix_api_call = $false
        broker_api_call = $false
        polygon_massive_call = $false
        market_data_fetch = $false
        broker_fetch = $false
        account_data_fetch = $false
        production_live_write = $false
    }
}

$summary = @"
# Sandbox Ledger/DB Post-Commit Closeout R001

Package: $Package

## Sandbox Closeout

- Status: SANDBOX_LEDGER_DB_POST_COMMIT_CLOSEOUT_READY_R001
- Source commit status: $($main.status)
- Idempotency key: $ExpectedKey
- Ledger rows: 6
- DB rows: 8
- Audit status: SANDBOX_AUDIT_WRITTEN
- Rollback preview status: SANDBOX_ROLLBACK_PREVIEW_CREATED

## Committed Sandbox Values

- Realized PnL before costs USD: 6015.14
- Commission expense USD: 225.63
- Financing expense USD: 40.60
- Realized net after costs USD: 5748.91
- Unrealized open PnL USD: 463.61
- Equity PnL including open PnL USD: 6212.52

## Production Go/No-Go

Production/live remains blocked. Trading readiness remains blocked.

Blocked reason: PRODUCTION_LIVE_APPROVAL_NOT_REQUESTED_OR_GRANTED

## Non-Event Confirmation

No trading, R009 submission, LMAX FIX/API call, broker API call, Polygon/Massive call, market-data fetch, broker/account fetch, production/live write, or trading readiness was introduced by this closeout package.
"@

Write-JsonFile (Join-Path $OutputDir "sandbox-ledger-db-post-commit-closeout-r001.json") $closeout
Write-JsonFile (Join-Path $OutputDir "sandbox-post-commit-evidence-manifest-r001.json") $manifest
Write-JsonFile (Join-Path $OutputDir "production-live-go-no-go-r001.json") $goNoGo
Write-TextFile (Join-Path $OutputDir "sandbox-ledger-db-post-commit-closeout-summary-r001.md") $summary

Write-Host "SANDBOX_LEDGER_DB_POST_COMMIT_CLOSEOUT_AND_PRODUCTION_GO_NO_GO_R001_BUILD_PASS"
