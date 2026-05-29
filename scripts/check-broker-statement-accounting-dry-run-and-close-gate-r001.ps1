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

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\broker-statement-accounting-dry-run-and-close-gate-r001"
$BrokerConfirmedDir = Join-Path $RepoRoot "artifacts\readiness\broker-statement-confirmed-pnl-r001"
$RealManualDir = Join-Path $RepoRoot "artifacts\readiness\real-manual-evidence-acceptance-r001"

$BrokerConfirmedPath = Join-Path $BrokerConfirmedDir "broker-statement-confirmed-pnl-r001.json"
$BrokerConfirmedPolicyPath = Join-Path $BrokerConfirmedDir "broker-statement-confirmed-pnl-policy-r001.json"
$BrokerBalancePath = Join-Path $BrokerConfirmedDir "broker-statement-balance-reconciliation-r001.json"
$AcceptancePath = Join-Path $RealManualDir "real-manual-evidence-acceptance-r001.json"
$NormalizedPath = Join-Path $RealManualDir "real-manual-broker-statement-normalized-from-lmax-raw-r001.json"

$PolicyPath = Join-Path $ArtifactDir "broker-statement-accounting-dry-run-policy-r001.json"
$DryRunPath = Join-Path $ArtifactDir "broker-statement-accounting-dry-run-r001.json"
$ClassificationPath = Join-Path $ArtifactDir "broker-statement-realized-unrealized-classification-r001.json"
$JournalPath = Join-Path $ArtifactDir "broker-statement-journal-dry-run-r001.json"
$GapPath = Join-Path $ArtifactDir "accounting-close-gap-report-r001.json"
$MainPath = Join-Path $ArtifactDir "broker-statement-accounting-dry-run-and-close-gate-r001.json"
$SummaryPath = Join-Path $ArtifactDir "broker-statement-accounting-dry-run-and-close-gate-summary-r001.md"

foreach ($path in @($BrokerConfirmedPath, $BrokerConfirmedPolicyPath, $BrokerBalancePath, $AcceptancePath, $NormalizedPath, $PolicyPath, $DryRunPath, $ClassificationPath, $JournalPath, $GapPath, $MainPath, $SummaryPath)) {
    Assert-True (Test-Path -LiteralPath $path) "Required artifact missing: $path"
}

$brokerConfirmed = Read-JsonFile $BrokerConfirmedPath
$brokerBalance = Read-JsonFile $BrokerBalancePath
$acceptance = Read-JsonFile $AcceptancePath
$normalized = Read-JsonFile $NormalizedPath
$policy = Read-JsonFile $PolicyPath
$dryRun = Read-JsonFile $DryRunPath
$classification = Read-JsonFile $ClassificationPath
$journal = Read-JsonFile $JournalPath
$gap = Read-JsonFile $GapPath
$main = Read-JsonFile $MainPath

Assert-Equal $brokerConfirmed.status "BROKER_STATEMENT_CONFIRMED_PNL_READY_R001" "Source broker statement confirmed PnL status mismatch."
Assert-Equal $acceptance.status "REAL_MANUAL_EVIDENCE_ACCEPTANCE_PARTIAL_READY_R001" "Source real manual evidence acceptance status mismatch."
Assert-Equal $acceptance.readiness.real_manual_broker_statement_acceptance $true "Real broker statement acceptance must be true."
Assert-Equal $acceptance.readiness.real_manual_accounting_evidence_acceptance $false "Accounting evidence acceptance must remain false."
Assert-Equal $brokerConfirmed.readiness.broker_statement_confirmed_pnl_ready $true "Broker statement confirmed PnL must be ready."
Assert-Equal $brokerConfirmed.readiness.internal_trade_reconciliation_ready $false "Internal trade reconciliation must remain false."
Assert-Equal $brokerBalance.balance_reconciled $true "Balance reconciliation must be true."
Assert-Equal $brokerBalance.equity_reconciled $true "Equity reconciliation must be true."
Assert-Equal $normalized.external_fetch $false "External fetch must remain false."
Assert-Equal $normalized.broker_api_call $false "Broker API call must remain false."
Assert-Equal $normalized.market_data_fetch $false "Market data fetch must remain false."
Assert-Equal $normalized.account_data_fetch $false "Account data fetch must remain false."
Assert-Equal $normalized.db_mutation $false "DB mutation must remain false."
Assert-Equal $normalized.ledger_commit $false "Ledger commit must remain false."
Assert-Equal $normalized.production_live_ready $false "Production/live ready must remain false."
Assert-Equal $normalized.trading_readiness_ready $false "Trading readiness ready must remain false."

Assert-Equal $policy.policy_type "broker_statement_accounting_dry_run_policy" "Accounting dry-run policy type mismatch."
Assert-Equal $policy.mode "broker_statement_backed_accounting_dry_run_only" "Accounting dry-run policy mode mismatch."
Assert-Equal $policy.real_accounting_evidence_required_for_close $true "Real accounting evidence must be required for close."
Assert-Equal $policy.realized_accounting_close_allowed $false "Realized accounting close must not be allowed."
Assert-Equal $policy.ledger_commit_allowed $false "Ledger commit must not be allowed."
Assert-Equal $policy.db_mutation_allowed $false "DB mutation must not be allowed."
Assert-Equal $policy.production_live_allowed $false "Production/live must not be allowed."
Assert-Equal $policy.trading_allowed $false "Trading must not be allowed."
Assert-Equal $policy.broker_statement_totals_sufficient_for_dry_run $true "Broker statement totals must be sufficient for dry-run."
Assert-Equal $policy.broker_statement_totals_sufficient_for_realized_accounting_close $false "Broker statement totals must not be sufficient for realized accounting close."

Assert-Equal $main.package "NEXT_BROKER_STATEMENT_ACCOUNTING_DRY_RUN_AND_CLOSE_GATE_R001" "Main package mismatch."
Assert-Equal $main.status "BROKER_STATEMENT_ACCOUNTING_DRY_RUN_READY_CLOSE_BLOCKED_R001" "Main status mismatch."
Assert-Equal $main.environment "sandbox" "Main environment mismatch."
Assert-Equal $main.mode "broker_statement_accounting_dry_run_only" "Main mode mismatch."

Assert-DecimalEqual ([decimal]$dryRun.realised_pnl_before_costs_usd) ([decimal]6015.14) "Dry-run realised PnL mismatch."
Assert-DecimalEqual ([decimal]$dryRun.commission_expense_usd) ([decimal]225.63) "Dry-run commission expense mismatch."
Assert-DecimalEqual ([decimal]$dryRun.financing_expense_usd) ([decimal]40.60) "Dry-run financing expense mismatch."
Assert-DecimalEqual ([decimal]$dryRun.realised_net_after_costs_usd) ([decimal]5748.91) "Dry-run realised net mismatch."
Assert-DecimalEqual ([decimal]$dryRun.unrealized_open_pnl_usd) ([decimal]463.61) "Dry-run unrealized open PnL mismatch."
Assert-DecimalEqual ([decimal]$dryRun.total_equity_pnl_including_open_pnl_usd) ([decimal]6212.52) "Dry-run equity PnL mismatch."
Assert-DecimalEqual (([decimal]$dryRun.realised_pnl_before_costs_usd) - ([decimal]$dryRun.commission_expense_usd) - ([decimal]$dryRun.financing_expense_usd)) ([decimal]$dryRun.realised_net_after_costs_usd) "Dry-run realised net formula failed."
Assert-DecimalEqual (([decimal]$dryRun.realised_net_after_costs_usd) + ([decimal]$dryRun.unrealized_open_pnl_usd)) ([decimal]$dryRun.total_equity_pnl_including_open_pnl_usd) "Dry-run equity formula failed."
Assert-Equal $dryRun.realized_accounting_close_ready $false "Dry-run must not mark realized accounting close ready."
Assert-Equal $dryRun.ledger_commit $false "Dry-run must not commit ledger."
Assert-Equal $dryRun.db_mutation $false "Dry-run must not mutate DB."

Assert-Equal @($classification.realized_components).Count 3 "Classification must include three realized components."
Assert-Equal @($classification.unrealized_components).Count 1 "Classification must include one unrealized component."
Assert-DecimalEqual ([decimal]$classification.realized_net_after_costs_usd) ([decimal]5748.91) "Classification realised net mismatch."
Assert-DecimalEqual ([decimal]$classification.unrealized_open_pnl_usd) ([decimal]463.61) "Classification unrealized PnL mismatch."
Assert-DecimalEqual ([decimal]$classification.equity_pnl_including_open_pnl_usd) ([decimal]6212.52) "Classification equity PnL mismatch."
Assert-Equal $classification.realized_accounting_close_ready $false "Classification must not mark realized accounting close ready."

Assert-Equal $journal.ready $true "Journal dry-run must be ready."
Assert-Equal $journal.commit_allowed $false "Journal commit must not be allowed."
Assert-Equal $journal.commit_eligible_entries 0 "Journal commit eligible entries must be zero."
Assert-Equal $journal.ledger_commit $false "Journal ledger commit must be false."
Assert-Equal $journal.db_mutation $false "Journal DB mutation must be false."
Assert-Equal @($journal.entries).Count 6 "Journal must include six dry-run entries."
foreach ($entry in @($journal.entries)) {
    Assert-Equal $entry.environment "sandbox" "Journal entry environment mismatch."
    Assert-Equal $entry.mode "dry_run_preview_only" "Journal entry mode mismatch."
    Assert-Equal $entry.commit_eligible $false "Journal entry must not be commit eligible: $($entry.dry_run_entry_id)"
    Assert-Equal $entry.commit_status "NO_COMMIT_DRY_RUN_ONLY" "Journal entry commit status mismatch: $($entry.dry_run_entry_id)"
    Assert-Equal $entry.ledger_commit $false "Journal entry ledger commit must be false: $($entry.dry_run_entry_id)"
    Assert-Equal $entry.db_mutation $false "Journal entry DB mutation must be false: $($entry.dry_run_entry_id)"
}

Assert-Equal $gap.accounting_dry_run_ready $true "Gap report must mark accounting dry-run ready."
Assert-Equal $gap.realized_accounting_close_ready $false "Gap report must block realized accounting close."
Assert-Equal $gap.blocked_reason "ACCOUNTING_CLOSE_REQUIREMENTS_MISSING" "Gap blocked reason mismatch."
Assert-True (@($gap.missing_requirements).Count -ge 12) "Gap report missing requirements list too short."

Assert-Equal $main.broker_statement_accounting_dry_run.ready $true "Main accounting dry-run ready mismatch."
Assert-DecimalEqual ([decimal]$main.broker_statement_accounting_dry_run.realised_pnl_before_costs_usd) ([decimal]6015.14) "Main realised PnL mismatch."
Assert-DecimalEqual ([decimal]$main.broker_statement_accounting_dry_run.commission_expense_usd) ([decimal]225.63) "Main commission expense mismatch."
Assert-DecimalEqual ([decimal]$main.broker_statement_accounting_dry_run.financing_expense_usd) ([decimal]40.60) "Main financing expense mismatch."
Assert-DecimalEqual ([decimal]$main.broker_statement_accounting_dry_run.realised_net_after_costs_usd) ([decimal]5748.91) "Main realised net mismatch."
Assert-DecimalEqual ([decimal]$main.broker_statement_accounting_dry_run.unrealized_open_pnl_usd) ([decimal]463.61) "Main unrealized open PnL mismatch."
Assert-DecimalEqual ([decimal]$main.broker_statement_accounting_dry_run.total_equity_pnl_including_open_pnl_usd) ([decimal]6212.52) "Main equity PnL mismatch."
Assert-Equal $main.classification.realized_components_ready $true "Main realized classification ready mismatch."
Assert-Equal $main.classification.unrealized_components_ready $true "Main unrealized classification ready mismatch."
Assert-Equal $main.classification.realized_accounting_close_ready $false "Main classification must not mark realized accounting close ready."
Assert-Equal $main.journal_dry_run.ready $true "Main journal dry-run ready mismatch."
Assert-Equal $main.journal_dry_run.commit_allowed $false "Main journal commit allowed must be false."
Assert-Equal $main.journal_dry_run.commit_eligible_entries 0 "Main journal commit eligible entries must be zero."
Assert-Equal $main.journal_dry_run.ledger_commit $false "Main journal ledger commit must be false."
Assert-Equal $main.journal_dry_run.db_mutation $false "Main journal DB mutation must be false."
Assert-Equal $main.accounting_close_gap_report.ready $true "Main close gap report ready mismatch."
Assert-Equal $main.accounting_close_gap_report.realized_accounting_close_ready $false "Main close gap must block realized close."
Assert-Equal $main.accounting_close_gap_report.blocked_reason "ACCOUNTING_CLOSE_REQUIREMENTS_MISSING" "Main close gap blocked reason mismatch."

Assert-Equal $main.ready_outputs.broker_statement_accounting_dry_run_ready $true "Accounting dry-run ready output missing."
Assert-Equal $main.ready_outputs.broker_statement_realized_unrealized_classification_ready $true "Classification ready output missing."
Assert-Equal $main.ready_outputs.broker_statement_journal_dry_run_ready $true "Journal dry-run ready output missing."
Assert-Equal $main.ready_outputs.accounting_close_gap_report_ready $true "Close gap report ready output missing."

Assert-Equal $main.readiness.real_accounting_evidence_acceptance $false "Real accounting evidence acceptance must remain false."
Assert-Equal $main.readiness.realized_accounting_close $false "Realized accounting close must remain false."
Assert-Equal $main.readiness.internal_trade_reconciliation_ready $false "Internal trade reconciliation must remain false."
Assert-Equal $main.readiness.ledger_commit $false "Ledger commit must remain false."
Assert-Equal $main.readiness.db_mutation $false "DB mutation must remain false."
Assert-Equal $main.readiness.production_live $false "Production/live must remain false."
Assert-Equal $main.readiness.trading_readiness $false "Trading readiness must remain false."
foreach ($label in $main.forbidden_ready_labels.PSObject.Properties) {
    Assert-Equal $label.Value $false "Forbidden ready label must remain false: $($label.Name)"
}

Assert-Equal $main.synthetic_sandbox_closeout_comparison.comparison_purpose "diagnostic_only_not_accounting_dry_run_or_close_gate" "Synthetic sandbox closeout must be diagnostic only."
Assert-Equal $main.synthetic_sandbox_closeout_comparison.used_as_broker_statement_acceptance_gate $false "Synthetic closeout must not gate broker acceptance."
Assert-Equal $main.synthetic_sandbox_closeout_comparison.used_as_accounting_dry_run_gate $false "Synthetic closeout must not gate accounting dry-run."
Assert-Equal $main.synthetic_sandbox_closeout_comparison.used_as_accounting_close_gate $false "Synthetic closeout must not gate accounting close."

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
    (Join-Path $RepoRoot "scripts\build-broker-statement-accounting-dry-run-and-close-gate-r001.ps1"),
    (Join-Path $RepoRoot "scripts\test-broker-statement-accounting-dry-run-and-close-gate-r001.ps1")
)
$forbiddenPatterns = @("SubmitOrder", "SubmitOrderUnmanaged", "AtmStrategyCreate", "Invoke-WebRequest", "Invoke-RestMethod", "curl ", "wget ", "api_key", "apikey", "password")
foreach ($pattern in $forbiddenPatterns) {
    $matches = Select-String -Path $scanFiles -Pattern $pattern -SimpleMatch -ErrorAction SilentlyContinue
    if ($matches) {
        throw "Forbidden call/secret/static pattern detected [$pattern]:`n$($matches | Out-String)"
    }
}

Write-Host "BROKER_STATEMENT_ACCOUNTING_DRY_RUN_AND_CLOSE_GATE_R001_GATE_PASS"
