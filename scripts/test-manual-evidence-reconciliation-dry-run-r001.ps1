param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

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

$builder = Join-Path $RepoRoot "scripts\build-manual-evidence-reconciliation-dry-run-r001.ps1"
$gate = Join-Path $RepoRoot "scripts\check-manual-evidence-reconciliation-dry-run-r001-gate.ps1"

& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot | Out-Null
& powershell -ExecutionPolicy Bypass -File $gate -RepoRoot $RepoRoot | Out-Null

$artifactDir = Join-Path $RepoRoot "artifacts\readiness\manual-evidence-reconciliation-dry-run-r001"
$main = Read-JsonFile (Join-Path $artifactDir "manual-evidence-reconciliation-dry-run-r001.json")
$diff = Read-JsonFile (Join-Path $artifactDir "manual-evidence-diff-report-r001.json")
$quarantine = Read-JsonFile (Join-Path $artifactDir "manual-evidence-quarantine-preview-r001.json")

Assert-Equal $main.status "MANUAL_EVIDENCE_RECONCILIATION_DRY_RUN_READY_R001" "Dry-run package should be ready."
Assert-Equal $main.manual_imports.broker_statement_imports_seen 2 "Broker imports seen mismatch."
Assert-Equal $main.manual_imports.broker_statement_imports_accepted 1 "Broker imports accepted mismatch."
Assert-Equal $main.manual_imports.broker_statement_imports_quarantined 1 "Broker imports quarantined mismatch."
Assert-Equal $main.manual_imports.accounting_evidence_imports_seen 2 "Accounting imports seen mismatch."
Assert-Equal $main.manual_imports.accounting_evidence_imports_accepted 1 "Accounting imports accepted mismatch."
Assert-Equal $main.manual_imports.accounting_evidence_imports_quarantined 1 "Accounting imports quarantined mismatch."
Assert-Equal $quarantine.quarantined_count 2 "Quarantine should contain two invalid deterministic imports."
Assert-DecimalEqual ([decimal]$main.source_closeout_values.gross_usd) ([decimal]-50.308800) "Gross USD mismatch."
Assert-DecimalEqual ([decimal]$main.source_closeout_values.commission_usd) ([decimal]26.268029) "Commission USD mismatch."
Assert-DecimalEqual ([decimal]$main.source_closeout_values.net_usd) ([decimal]-76.576829) "Net USD mismatch."
Assert-Equal $main.broker_statement_reconciliation_dry_run.reconciled $true "Broker dry-run should reconcile."
Assert-Equal $main.accounting_evidence_reconciliation_dry_run.reconciled $true "Accounting dry-run should reconcile."
Assert-Equal @($diff.broker_statement_diffs | Where-Object { $_.reconciled -eq $true }).Count 3 "Broker diff rows should reconcile."
Assert-Equal @($diff.accounting_evidence_diffs | Where-Object { $_.reconciled -eq $true }).Count 3 "Accounting diff rows should reconcile."
Assert-Equal $main.global_guards.external_calls $false "External calls should remain false."
Assert-Equal $main.global_guards.broker_api_calls $false "Broker API calls should remain false."
Assert-Equal $main.global_guards.market_data_fetch $false "Market-data fetch should remain false."
Assert-Equal $main.global_guards.account_data_fetch $false "Account-data fetch should remain false."
Assert-Equal $main.global_guards.db_mutation $false "DB mutation should remain false."
Assert-Equal $main.global_guards.ledger_commit $false "Ledger commit should remain false."
Assert-Equal $main.global_guards.production_live_ready $false "Production/live should remain false."
Assert-Equal $main.global_guards.trading_readiness_ready $false "Trading readiness should remain false."
foreach ($label in $main.forbidden_ready_labels.PSObject.Properties) {
    Assert-Equal $label.Value $false "Forbidden label must remain false: $($label.Name)"
}

Write-Host "MANUAL_EVIDENCE_RECONCILIATION_DRY_RUN_R001_TESTS_PASS"
