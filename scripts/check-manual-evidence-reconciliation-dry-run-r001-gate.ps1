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

$controlledPath = Join-Path $RepoRoot "artifacts\readiness\controlled-real-evidence-import-r001\controlled-real-evidence-import-r001.json"
$reconciliationPath = Join-Path $RepoRoot "artifacts\readiness\sandbox-broker-accounting-reconciliation-r001\sandbox-broker-accounting-reconciliation-r001.json"
$closeoutPath = Join-Path $RepoRoot "artifacts\readiness\sandbox-preview-closeout-r001\sandbox-preview-closeout-r001.json"
$artifactDir = Join-Path $RepoRoot "artifacts\readiness\manual-evidence-reconciliation-dry-run-r001"

$requiredArtifacts = @(
    "manual-evidence-reconciliation-dry-run-r001.json",
    "manual-evidence-diff-report-r001.json",
    "manual-evidence-quarantine-preview-r001.json",
    "manual-evidence-reconciliation-dry-run-summary-r001.md"
)
foreach ($path in @($controlledPath, $reconciliationPath, $closeoutPath)) {
    Assert-True (Test-Path -LiteralPath $path) "Required source artifact missing: $path"
}
foreach ($name in $requiredArtifacts) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactDir $name)) "Required artifact missing: $name"
}
foreach ($dir in @("inbox\broker-statements", "inbox\accounting-evidence", "quarantine", "accepted")) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactDir $dir)) "Required folder missing: $dir"
}

$controlled = Read-JsonFile $controlledPath
$reconciliation = Read-JsonFile $reconciliationPath
$closeout = Read-JsonFile $closeoutPath
$main = Read-JsonFile (Join-Path $artifactDir "manual-evidence-reconciliation-dry-run-r001.json")
$diff = Read-JsonFile (Join-Path $artifactDir "manual-evidence-diff-report-r001.json")
$quarantine = Read-JsonFile (Join-Path $artifactDir "manual-evidence-quarantine-preview-r001.json")

Assert-Equal $controlled.status "CONTROLLED_REAL_EVIDENCE_IMPORT_READY_R001" "Controlled import framework source status mismatch."
Assert-Equal $reconciliation.status "SANDBOX_BROKER_ACCOUNTING_RECONCILIATION_READY_R001" "Source reconciliation status mismatch."
Assert-Equal $closeout.status "SANDBOX_PREVIEW_CLOSEOUT_READY_R001" "Source closeout status mismatch."
Assert-DecimalEqual ([decimal]$closeout.gross_pnl_usd) ([decimal]-50.308800) "Source gross USD mismatch."
Assert-DecimalEqual ([decimal]$closeout.commission_usd) ([decimal]26.268029) "Source commission USD mismatch."
Assert-DecimalEqual ([decimal]$closeout.net_pnl_usd) ([decimal]-76.576829) "Source net USD mismatch."
Assert-Equal $closeout.reconciled $true "Source closeout must be reconciled."

Assert-Equal $main.package "NEXT_MANUAL_EVIDENCE_RECONCILIATION_DRY_RUN_R001" "Main package mismatch."
Assert-Equal $main.status "MANUAL_EVIDENCE_RECONCILIATION_DRY_RUN_READY_R001" "Main status mismatch."
Assert-Equal $main.environment "sandbox" "Main environment mismatch."
Assert-Equal $main.mode "offline_manual_dry_run_only" "Main mode mismatch."
foreach ($hashName in @("controlled_real_evidence_import_r001", "sandbox_broker_accounting_reconciliation_r001", "sandbox_preview_closeout_r001", "broker_statement_manual_import_schema_r001", "accounting_evidence_manual_import_schema_r001", "controlled_import_validation_policy_r001")) {
    Assert-True ($main.source_artifact_hashes.$hashName -match "^sha256:[A-F0-9]{64}$") "Missing source hash: $hashName"
}

Assert-Equal $main.manual_imports.broker_statement_imports_seen 2 "Broker imports seen mismatch."
Assert-Equal $main.manual_imports.broker_statement_imports_accepted 1 "Broker imports accepted mismatch."
Assert-Equal $main.manual_imports.broker_statement_imports_quarantined 1 "Broker imports quarantined mismatch."
Assert-Equal $main.manual_imports.accounting_evidence_imports_seen 2 "Accounting imports seen mismatch."
Assert-Equal $main.manual_imports.accounting_evidence_imports_accepted 1 "Accounting imports accepted mismatch."
Assert-Equal $main.manual_imports.accounting_evidence_imports_quarantined 1 "Accounting imports quarantined mismatch."

Assert-Equal @($main.accepted_imports.broker_statements).Count 1 "Exactly one broker import should be accepted."
Assert-Equal @($main.accepted_imports.accounting_evidence).Count 1 "Exactly one accounting import should be accepted."
Assert-Equal $main.accepted_imports.broker_statements[0].sample_only $true "Accepted broker sample should be sample_only."
Assert-Equal $main.accepted_imports.broker_statements[0].real_broker_statement $false "Accepted broker sample must not be real broker statement."
Assert-Equal $main.accepted_imports.broker_statements[0].external_fetch $false "Accepted broker sample external_fetch must be false."
Assert-Equal $main.accepted_imports.broker_statements[0].broker_api_call $false "Accepted broker sample broker_api_call must be false."
Assert-Equal $main.accepted_imports.accounting_evidence[0].sample_only $true "Accepted accounting sample should be sample_only."
Assert-Equal $main.accepted_imports.accounting_evidence[0].real_accounting_close $false "Accepted accounting sample must not be real accounting close."
Assert-Equal $main.accepted_imports.accounting_evidence[0].db_mutation $false "Accepted accounting sample db_mutation must be false."
Assert-Equal $main.accepted_imports.accounting_evidence[0].ledger_commit $false "Accepted accounting sample ledger_commit must be false."

Assert-Equal $main.quarantine_preview.quarantined_count 2 "Main quarantine count mismatch."
Assert-Equal $quarantine.quarantined_count 2 "Quarantine artifact count mismatch."
Assert-Equal $quarantine.no_destructive_file_movement $true "Quarantine must be report-only, no destructive movement."
Assert-Equal $quarantine.no_db_mutation $true "Quarantine must not mutate DB."
Assert-Equal $quarantine.no_external_calls $true "Quarantine must not use external calls."
Assert-True (@($quarantine.items | Where-Object { $_.evidence_type -eq "broker_statement" }).Count -eq 1) "Invalid broker import must be quarantined."
Assert-True (@($quarantine.items | Where-Object { $_.evidence_type -eq "accounting_evidence" }).Count -eq 1) "Invalid accounting import must be quarantined."
$allReasons = @($quarantine.items | ForEach-Object { $_.reasons }) -join "|"
foreach ($reason in @("source_file_sha256 missing", "approval_id missing", "account_id_hash missing", "statement_period missing", "period missing", "external_fetch true", "broker_api_call true", "market_data_fetch true", "db_mutation true", "ledger_commit true", "production_live flag true")) {
    Assert-True ($allReasons.Contains($reason)) "Expected quarantine reason missing: $reason"
}

Assert-DecimalEqual ([decimal]$main.source_closeout_values.gross_usd) ([decimal]-50.308800) "Main source gross mismatch."
Assert-DecimalEqual ([decimal]$main.source_closeout_values.commission_usd) ([decimal]26.268029) "Main source commission mismatch."
Assert-DecimalEqual ([decimal]$main.source_closeout_values.net_usd) ([decimal]-76.576829) "Main source net mismatch."
Assert-Equal $main.source_closeout_values.reconciled $true "Main source closeout reconciliation mismatch."
Assert-True ([decimal]$main.source_closeout_values.tolerance -le [decimal]0.000001) "Tolerance must be no wider than 0.000001."

Assert-Equal $main.broker_statement_reconciliation_dry_run.ready $true "Broker statement dry-run should be ready."
Assert-Equal $main.broker_statement_reconciliation_dry_run.real_broker_statement_reconciliation_ready $false "Real broker statement reconciliation must remain false."
Assert-Equal $main.broker_statement_reconciliation_dry_run.reconciled $true "Broker dry-run must reconcile."
Assert-Equal @($main.broker_statement_reconciliation_dry_run.unmatched_items).Count 0 "Broker unmatched items must be empty."
Assert-Equal @($main.broker_statement_reconciliation_dry_run.diffs).Count 3 "Broker diff count mismatch."

Assert-Equal $main.accounting_evidence_reconciliation_dry_run.ready $true "Accounting evidence dry-run should be ready."
Assert-Equal $main.accounting_evidence_reconciliation_dry_run.realized_accounting_close_ready $false "Realized accounting close must remain false."
Assert-Equal $main.accounting_evidence_reconciliation_dry_run.reconciled $true "Accounting dry-run must reconcile."
Assert-Equal @($main.accounting_evidence_reconciliation_dry_run.unmatched_items).Count 0 "Accounting unmatched items must be empty."
Assert-Equal @($main.accounting_evidence_reconciliation_dry_run.diffs).Count 3 "Accounting diff count mismatch."

foreach ($row in @($diff.broker_statement_diffs + $diff.accounting_evidence_diffs)) {
    Assert-DecimalEqual ([decimal]$row.delta) ([decimal]0.000000) "Diff delta must be zero for field $($row.field)."
    Assert-Equal $row.reconciled $true "Diff row must reconcile for field $($row.field)."
    Assert-True ([decimal]$row.tolerance -le [decimal]0.000001) "Diff tolerance must be no wider than 0.000001."
}
Assert-True (@($diff.broker_statement_diffs | Where-Object { $_.field -eq "gross_pnl_usd" }).Count -eq 1) "Broker gross diff missing."
Assert-True (@($diff.broker_statement_diffs | Where-Object { $_.field -eq "commission_usd" }).Count -eq 1) "Broker commission diff missing."
Assert-True (@($diff.broker_statement_diffs | Where-Object { $_.field -eq "net_pnl_usd" }).Count -eq 1) "Broker net diff missing."
Assert-True (@($diff.accounting_evidence_diffs | Where-Object { $_.field -eq "gross_pnl" }).Count -eq 1) "Accounting gross diff missing."
Assert-True (@($diff.accounting_evidence_diffs | Where-Object { $_.field -eq "commission_expense" }).Count -eq 1) "Accounting commission diff missing."
Assert-True (@($diff.accounting_evidence_diffs | Where-Object { $_.field -eq "net_pnl" }).Count -eq 1) "Accounting net diff missing."

foreach ($label in @("manual_broker_statement_reconciliation_dry_run", "manual_accounting_evidence_reconciliation_dry_run", "manual_evidence_diff_report", "invalid_evidence_quarantine_preview")) {
    Assert-Equal $main.ready_outputs.$label $true "Allowed ready output missing: $label"
}
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
    (Join-Path $RepoRoot "scripts\build-manual-evidence-reconciliation-dry-run-r001.ps1")
)
$forbiddenPatterns = @(
    "SubmitOrder",
    "SubmitOrderUnmanaged",
    "AtmStrategyCreate",
    "Invoke-WebRequest",
    "Invoke-RestMethod",
    "curl ",
    "wget ",
    "api_key",
    "apikey",
    "password"
)
foreach ($pattern in $forbiddenPatterns) {
    $matches = Select-String -Path $scanFiles -Pattern $pattern -SimpleMatch -ErrorAction SilentlyContinue
    if ($matches) {
        throw "Forbidden call/secret/static pattern detected [$pattern]:`n$($matches | Out-String)"
    }
}

Write-Host "MANUAL_EVIDENCE_RECONCILIATION_DRY_RUN_R001_GATE_PASS"
