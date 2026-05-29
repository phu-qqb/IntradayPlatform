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

$builder = Join-Path $RepoRoot "scripts\build-broker-statement-accounting-dry-run-and-close-gate-r001.ps1"
$gate = Join-Path $RepoRoot "scripts\check-broker-statement-accounting-dry-run-and-close-gate-r001.ps1"

& powershell -ExecutionPolicy Bypass -File $builder -RepoRoot $RepoRoot | Out-Null
& powershell -ExecutionPolicy Bypass -File $gate -RepoRoot $RepoRoot | Out-Null

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\broker-statement-accounting-dry-run-and-close-gate-r001"
$main = Read-JsonFile (Join-Path $ArtifactDir "broker-statement-accounting-dry-run-and-close-gate-r001.json")
$dryRun = Read-JsonFile (Join-Path $ArtifactDir "broker-statement-accounting-dry-run-r001.json")
$classification = Read-JsonFile (Join-Path $ArtifactDir "broker-statement-realized-unrealized-classification-r001.json")
$journal = Read-JsonFile (Join-Path $ArtifactDir "broker-statement-journal-dry-run-r001.json")
$gap = Read-JsonFile (Join-Path $ArtifactDir "accounting-close-gap-report-r001.json")

Assert-Equal $main.status "BROKER_STATEMENT_ACCOUNTING_DRY_RUN_READY_CLOSE_BLOCKED_R001" "Main status mismatch."
Assert-Equal $main.ready_outputs.broker_statement_accounting_dry_run_ready $true "Accounting dry-run ready output missing."
Assert-Equal $main.ready_outputs.broker_statement_realized_unrealized_classification_ready $true "Classification ready output missing."
Assert-Equal $main.ready_outputs.broker_statement_journal_dry_run_ready $true "Journal dry-run ready output missing."
Assert-Equal $main.ready_outputs.accounting_close_gap_report_ready $true "Close gap report ready output missing."

Assert-DecimalEqual ([decimal]$dryRun.realised_pnl_before_costs_usd) ([decimal]6015.14) "Realised PnL before costs mismatch."
Assert-DecimalEqual ([decimal]$dryRun.commission_expense_usd) ([decimal]225.63) "Commission expense mismatch."
Assert-DecimalEqual ([decimal]$dryRun.financing_expense_usd) ([decimal]40.60) "Financing expense mismatch."
Assert-DecimalEqual ([decimal]$dryRun.realised_net_after_costs_usd) ([decimal]5748.91) "Realised net after costs mismatch."
Assert-DecimalEqual ([decimal]$dryRun.unrealized_open_pnl_usd) ([decimal]463.61) "Unrealized open PnL mismatch."
Assert-DecimalEqual ([decimal]$dryRun.total_equity_pnl_including_open_pnl_usd) ([decimal]6212.52) "Equity PnL including open PnL mismatch."
Assert-DecimalEqual (([decimal]6015.14) - ([decimal]225.63) - ([decimal]40.60)) ([decimal]5748.91) "Realised net formula mismatch."
Assert-DecimalEqual (([decimal]5748.91) + ([decimal]463.61)) ([decimal]6212.52) "Equity PnL formula mismatch."

Assert-Equal @($classification.realized_components).Count 3 "Classification realized component count mismatch."
Assert-Equal @($classification.unrealized_components).Count 1 "Classification unrealized component count mismatch."
Assert-Equal $classification.realized_accounting_close_ready $false "Classification must not mark realized accounting close ready."

Assert-Equal $journal.ready $true "Journal dry-run must be ready."
Assert-Equal $journal.commit_allowed $false "Journal commit must be false."
Assert-Equal $journal.commit_eligible_entries 0 "Journal commit eligible entries must be zero."
foreach ($entry in @($journal.entries)) {
    Assert-Equal $entry.commit_eligible $false "Journal entry must not be commit eligible."
    Assert-Equal $entry.commit_status "NO_COMMIT_DRY_RUN_ONLY" "Journal entry commit status mismatch."
    Assert-Equal $entry.ledger_commit $false "Journal entry ledger commit must be false."
    Assert-Equal $entry.db_mutation $false "Journal entry DB mutation must be false."
}

Assert-Equal $gap.accounting_dry_run_ready $true "Gap report accounting dry-run ready mismatch."
Assert-Equal $gap.realized_accounting_close_ready $false "Gap report must keep realized close false."
Assert-Equal $gap.blocked_reason "ACCOUNTING_CLOSE_REQUIREMENTS_MISSING" "Gap report blocked reason mismatch."

Assert-Equal $main.readiness.real_accounting_evidence_acceptance $false "Real accounting evidence acceptance must remain false."
Assert-Equal $main.readiness.realized_accounting_close $false "Realized accounting close must remain false."
Assert-Equal $main.readiness.internal_trade_reconciliation_ready $false "Internal trade reconciliation must remain false."
Assert-Equal $main.readiness.ledger_commit $false "Ledger commit must remain false."
Assert-Equal $main.readiness.db_mutation $false "DB mutation must remain false."
Assert-Equal $main.readiness.production_live $false "Production/live must remain false."
Assert-Equal $main.readiness.trading_readiness $false "Trading readiness must remain false."
Assert-Equal $main.global_guards.external_calls $false "External calls must remain false."
Assert-Equal $main.global_guards.broker_api_calls $false "Broker API calls must remain false."
Assert-Equal $main.global_guards.market_data_fetch $false "Market-data fetch must remain false."
Assert-Equal $main.global_guards.account_data_fetch $false "Account-data fetch must remain false."
Assert-Equal $main.global_guards.ledger_commit $false "Ledger commit must remain false."
Assert-Equal $main.global_guards.db_mutation $false "DB mutation must remain false."
Assert-Equal $main.global_guards.production_live_ready $false "Production/live ready must remain false."
Assert-Equal $main.global_guards.trading_readiness_ready $false "Trading readiness ready must remain false."

Write-Host "BROKER_STATEMENT_ACCOUNTING_DRY_RUN_AND_CLOSE_GATE_R001_TEST_PASS"
