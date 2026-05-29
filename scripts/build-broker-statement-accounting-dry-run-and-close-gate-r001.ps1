param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\broker-statement-accounting-dry-run-and-close-gate-r001"
New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null

function Write-JsonArtifact([string]$Name, [object]$Value) {
    $path = Join-Path $ArtifactDir $Name
    $Value | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $path -Encoding UTF8
}

function Write-TextArtifact([string]$Name, [string]$Value) {
    $path = Join-Path $ArtifactDir $Name
    $Value | Set-Content -LiteralPath $path -Encoding UTF8
}

function Read-JsonFile([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Sha([string]$Path) {
    "sha256:$((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash)"
}

function Assert-Equal($Actual, $Expected, [string]$Message) {
    if ($Actual -ne $Expected) { throw "$Message Expected=[$Expected] Actual=[$Actual]" }
}

function Assert-True($Actual, [string]$Message) {
    if ($Actual -ne $true) { throw "$Message Expected=[True] Actual=[$Actual]" }
}

function Assert-False($Actual, [string]$Message) {
    if ($Actual -ne $false) { throw "$Message Expected=[False] Actual=[$Actual]" }
}

function As-Decimal($Value, [string]$Name) {
    if ($null -eq $Value -or ($Value -is [string] -and [string]::IsNullOrWhiteSpace($Value))) {
        throw "Required decimal value missing: $Name"
    }
    return [decimal]$Value
}

function Assert-DecimalEqual([decimal]$Actual, [decimal]$Expected, [string]$Message) {
    if ([Math]::Abs($Actual - $Expected) -gt [decimal]0.000001) {
        throw "$Message Expected=[$Expected] Actual=[$Actual]"
    }
}

$BrokerConfirmedDir = Join-Path $RepoRoot "artifacts\readiness\broker-statement-confirmed-pnl-r001"
$RealManualDir = Join-Path $RepoRoot "artifacts\readiness\real-manual-evidence-acceptance-r001"

$BrokerConfirmedPath = Join-Path $BrokerConfirmedDir "broker-statement-confirmed-pnl-r001.json"
$BrokerConfirmedPolicyPath = Join-Path $BrokerConfirmedDir "broker-statement-confirmed-pnl-policy-r001.json"
$BrokerBalancePath = Join-Path $BrokerConfirmedDir "broker-statement-balance-reconciliation-r001.json"
$AcceptancePath = Join-Path $RealManualDir "real-manual-evidence-acceptance-r001.json"
$NormalizedPath = Join-Path $RealManualDir "real-manual-broker-statement-normalized-from-lmax-raw-r001.json"

foreach ($path in @($BrokerConfirmedPath, $BrokerConfirmedPolicyPath, $BrokerBalancePath, $AcceptancePath, $NormalizedPath)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Required source artifact missing: $path" }
}

$brokerConfirmed = Read-JsonFile $BrokerConfirmedPath
$brokerBalance = Read-JsonFile $BrokerBalancePath
$acceptance = Read-JsonFile $AcceptancePath
$normalized = Read-JsonFile $NormalizedPath

Assert-Equal $brokerConfirmed.status "BROKER_STATEMENT_CONFIRMED_PNL_READY_R001" "Broker statement confirmed PnL source status mismatch."
Assert-Equal $acceptance.status "REAL_MANUAL_EVIDENCE_ACCEPTANCE_PARTIAL_READY_R001" "Real manual evidence acceptance source status mismatch."
Assert-True $acceptance.readiness.real_manual_broker_statement_acceptance "Real manual broker statement acceptance must be true."
Assert-False $acceptance.readiness.real_manual_accounting_evidence_acceptance "Accounting evidence acceptance must remain false."
Assert-True $brokerConfirmed.readiness.broker_statement_confirmed_pnl_ready "Broker statement confirmed PnL must be ready."
Assert-False $brokerConfirmed.readiness.internal_trade_reconciliation_ready "Internal trade reconciliation must remain false."
Assert-True $brokerBalance.balance_reconciled "Balance reconciliation must be true."
Assert-True $brokerBalance.equity_reconciled "Equity reconciliation must be true."
Assert-False $normalized.external_fetch "Normalized broker statement must not use external fetch."
Assert-False $normalized.broker_api_call "Normalized broker statement must not use broker API."
Assert-False $normalized.market_data_fetch "Normalized broker statement must not fetch market data."
Assert-False $normalized.account_data_fetch "Normalized broker statement must not fetch account data."
Assert-False $normalized.db_mutation "Normalized broker statement must not mutate DB."
Assert-False $normalized.ledger_commit "Normalized broker statement must not commit ledger."
Assert-False $normalized.production_live_ready "Normalized broker statement must not mark production/live ready."
Assert-False $normalized.trading_readiness_ready "Normalized broker statement must not mark trading ready."

$pnl = $brokerConfirmed.broker_statement_confirmed_pnl
$realisedBeforeCosts = As-Decimal $pnl.realised_pnl_usd "realised_pnl_usd"
$commissionExpense = As-Decimal $pnl.commission_cost_usd "commission_cost_usd"
$commissionSigned = As-Decimal $pnl.commission_usd_signed "commission_usd_signed"
$financingExpense = As-Decimal $pnl.financing_cost_usd "financing_cost_usd"
$financingSigned = As-Decimal $pnl.financing_usd_signed "financing_usd_signed"
$realisedNetAfterCosts = As-Decimal $pnl.realised_net_after_commission_financing_usd "realised_net_after_commission_financing_usd"
$unrealizedOpenPnl = As-Decimal $pnl.closing_pnl_usd "closing_pnl_usd"
$equityPnlIncludingOpen = As-Decimal $pnl.equity_pnl_including_open_pnl_usd "equity_pnl_including_open_pnl_usd"
$closingBalance = As-Decimal $pnl.closing_balance_usd "closing_balance_usd"
$closingEquity = As-Decimal $pnl.closing_equity_usd "closing_equity_usd"

Assert-DecimalEqual ($realisedBeforeCosts - $commissionExpense - $financingExpense) $realisedNetAfterCosts "Accounting dry-run realised net formula failed."
Assert-DecimalEqual ($realisedNetAfterCosts + $unrealizedOpenPnl) $equityPnlIncludingOpen "Accounting dry-run equity PnL formula failed."

$policy = [ordered]@{
    policy_type = "broker_statement_accounting_dry_run_policy"
    policy_version = "R001"
    environment = "sandbox"
    mode = "broker_statement_backed_accounting_dry_run_only"
    source_of_truth = "accepted_offline_manual_lmax_broker_statement"
    source_scope = "broker_statement_totals_and_wallets_only"
    real_accounting_evidence_required_for_close = $true
    realized_accounting_close_allowed = $false
    ledger_commit_allowed = $false
    db_mutation_allowed = $false
    production_live_allowed = $false
    trading_allowed = $false
    internal_trade_reconciliation_required_for_trade_level_attribution = $true
    broker_statement_totals_sufficient_for_dry_run = $true
    broker_statement_totals_sufficient_for_realized_accounting_close = $false
}
Write-JsonArtifact "broker-statement-accounting-dry-run-policy-r001.json" $policy
$AccountingPolicyPath = Join-Path $ArtifactDir "broker-statement-accounting-dry-run-policy-r001.json"

$accountingDryRun = [ordered]@{
    artifact_type = "broker_statement_accounting_dry_run_r001"
    account_currency = "USD"
    statement_period = [ordered]@{
        from = $brokerConfirmed.broker_statement_scope.statement_period.from
        to = $brokerConfirmed.broker_statement_scope.statement_period.to
    }
    realised_pnl_before_costs_usd = $realisedBeforeCosts
    commission_expense_usd = $commissionExpense
    financing_expense_usd = $financingExpense
    realised_net_after_costs_usd = $realisedNetAfterCosts
    unrealized_open_pnl_usd = $unrealizedOpenPnl
    total_equity_pnl_including_open_pnl_usd = $equityPnlIncludingOpen
    closing_balance_usd = $closingBalance
    closing_equity_usd = $closingEquity
    classifications = [ordered]@{
        realised_pnl_before_costs_usd = "realised broker P&L before costs"
        commission_expense_usd = "expense"
        financing_expense_usd = "expense"
        realised_net_after_costs_usd = "realised broker P&L after commission and financing"
        unrealized_open_pnl_usd = "open position P&L, not realized accounting close"
        total_equity_pnl_including_open_pnl_usd = "realized net after costs plus open P&L"
    }
    formulas = [ordered]@{
        realised_net_after_costs = "6015.14 - 225.63 - 40.60 = 5748.91"
        total_equity_pnl_including_open_pnl = "5748.91 + 463.61 = 6212.52"
    }
    realized_accounting_close_ready = $false
    ledger_commit = $false
    db_mutation = $false
}
Write-JsonArtifact "broker-statement-accounting-dry-run-r001.json" $accountingDryRun
$AccountingDryRunPath = Join-Path $ArtifactDir "broker-statement-accounting-dry-run-r001.json"

$classification = [ordered]@{
    artifact_type = "broker_statement_realized_unrealized_classification_r001"
    realized_components = @(
        [ordered]@{
            component = "realised_pnl_before_costs"
            amount_usd = $realisedBeforeCosts
            classification = "realized_broker_statement_pnl_before_costs"
        },
        [ordered]@{
            component = "commission"
            amount_usd = $commissionSigned
            classification = "realized_cost_expense"
        },
        [ordered]@{
            component = "financing"
            amount_usd = $financingSigned
            classification = "realized_cost_expense"
        }
    )
    unrealized_components = @(
        [ordered]@{
            component = "closing_open_pnl"
            amount_usd = $unrealizedOpenPnl
            classification = "unrealized_open_position_pnl"
        }
    )
    realized_net_after_costs_usd = $realisedNetAfterCosts
    unrealized_open_pnl_usd = $unrealizedOpenPnl
    equity_pnl_including_open_pnl_usd = $equityPnlIncludingOpen
    realized_accounting_close_ready = $false
}
Write-JsonArtifact "broker-statement-realized-unrealized-classification-r001.json" $classification
$ClassificationPath = Join-Path $ArtifactDir "broker-statement-realized-unrealized-classification-r001.json"

$sourceHash = Sha $BrokerConfirmedPath
function New-JournalEntry([string]$Id, [string]$Subtype, [decimal]$Amount, [decimal]$SignedAmount, [string]$Hint, [string]$Component, [string]$Memo) {
    [ordered]@{
        dry_run_entry_id = $Id
        environment = "sandbox"
        mode = "dry_run_preview_only"
        account_currency = "USD"
        entry_type = "broker_statement_accounting_dry_run"
        entry_subtype = $Subtype
        amount_usd = $Amount
        signed_amount_usd = $SignedAmount
        debit_credit_hint = $Hint
        source_component = $Component
        source_artifact_hash = $sourceHash
        commit_eligible = $false
        commit_status = "NO_COMMIT_DRY_RUN_ONLY"
        ledger_commit = $false
        db_mutation = $false
        memo = $Memo
    }
}

$journalEntries = @(
    (New-JournalEntry "BSDR-R001-001" "realised_pnl_before_costs" $realisedBeforeCosts $realisedBeforeCosts "credit_pnl_preview" "broker_statement_confirmed_pnl.realised_pnl_usd" "Realised broker statement PnL before costs."),
    (New-JournalEntry "BSDR-R001-002" "commission_expense" $commissionExpense $commissionSigned "debit_expense_preview" "broker_statement_confirmed_pnl.commission_usd_signed" "Broker statement commission expense preview."),
    (New-JournalEntry "BSDR-R001-003" "financing_expense" $financingExpense $financingSigned "debit_expense_preview" "broker_statement_confirmed_pnl.financing_usd_signed" "Broker statement financing expense preview."),
    (New-JournalEntry "BSDR-R001-004" "realised_net_after_costs" $realisedNetAfterCosts $realisedNetAfterCosts "credit_net_pnl_preview" "derived.realised_pnl_minus_commission_minus_financing" "Realised net after commission and financing."),
    (New-JournalEntry "BSDR-R001-005" "unrealized_open_pnl" $unrealizedOpenPnl $unrealizedOpenPnl "credit_unrealized_preview" "broker_statement_confirmed_pnl.closing_pnl_usd" "Open position PnL classified as unrealized."),
    (New-JournalEntry "BSDR-R001-006" "equity_pnl_including_open_pnl" $equityPnlIncludingOpen $equityPnlIncludingOpen "credit_equity_pnl_preview" "derived.realised_net_after_costs_plus_open_pnl" "Equity PnL including open position PnL.")
)

$journalDryRun = [ordered]@{
    artifact_type = "broker_statement_journal_dry_run_r001"
    ready = $true
    mode = "dry_run_preview_only"
    commit_allowed = $false
    commit_eligible_entries = 0
    ledger_commit = $false
    db_mutation = $false
    entries = $journalEntries
    formulas = [ordered]@{
        realised_net_after_costs = "6015.14 - 225.63 - 40.60 = 5748.91"
        equity_pnl_including_open_pnl = "5748.91 + 463.61 = 6212.52"
    }
}
Write-JsonArtifact "broker-statement-journal-dry-run-r001.json" $journalDryRun
$JournalPath = Join-Path $ArtifactDir "broker-statement-journal-dry-run-r001.json"

$missingRequirements = @(
    "real accounting evidence acceptance missing",
    "accounting policy approval for realized close missing",
    "period close approval missing",
    "source-of-truth hierarchy approval missing",
    "internal trade-level reconciliation missing",
    "settlement/trade-date policy approval missing",
    "FX translation policy approval missing",
    "cost recognition policy approval missing",
    "financing recognition policy approval missing",
    "audit trail approval missing",
    "ledger handoff approval missing",
    "operator accounting close approval missing"
)

$gapReport = [ordered]@{
    artifact_type = "accounting_close_gap_report_r001"
    accounting_dry_run_ready = $true
    realized_accounting_close_ready = $false
    blocked_reason = "ACCOUNTING_CLOSE_REQUIREMENTS_MISSING"
    missing_requirements = $missingRequirements
}
Write-JsonArtifact "accounting-close-gap-report-r001.json" $gapReport
$GapPath = Join-Path $ArtifactDir "accounting-close-gap-report-r001.json"

$main = [ordered]@{
    package = "NEXT_BROKER_STATEMENT_ACCOUNTING_DRY_RUN_AND_CLOSE_GATE_R001"
    status = "BROKER_STATEMENT_ACCOUNTING_DRY_RUN_READY_CLOSE_BLOCKED_R001"
    environment = "sandbox"
    mode = "broker_statement_accounting_dry_run_only"
    source_packages = [ordered]@{
        broker_statement_confirmed_pnl = "NEXT_BROKER_STATEMENT_CONFIRMED_PNL_R001"
        real_manual_evidence_acceptance = "NEXT_REAL_MANUAL_EVIDENCE_ACCEPTANCE_R001"
    }
    source_artifacts = [ordered]@{
        broker_statement_confirmed_pnl_r001 = $BrokerConfirmedPath
        broker_statement_confirmed_pnl_policy_r001 = $BrokerConfirmedPolicyPath
        broker_statement_balance_reconciliation_r001 = $BrokerBalancePath
        normalized_lmax_broker_statement_r001 = $NormalizedPath
        real_manual_evidence_acceptance_r001 = $AcceptancePath
        broker_statement_accounting_dry_run_policy_r001 = $AccountingPolicyPath
        broker_statement_accounting_dry_run_r001 = $AccountingDryRunPath
        broker_statement_realized_unrealized_classification_r001 = $ClassificationPath
        broker_statement_journal_dry_run_r001 = $JournalPath
        accounting_close_gap_report_r001 = $GapPath
    }
    source_artifact_hashes = [ordered]@{
        broker_statement_confirmed_pnl_r001 = Sha $BrokerConfirmedPath
        broker_statement_confirmed_pnl_policy_r001 = Sha $BrokerConfirmedPolicyPath
        broker_statement_balance_reconciliation_r001 = Sha $BrokerBalancePath
        normalized_lmax_broker_statement_r001 = Sha $NormalizedPath
        real_manual_evidence_acceptance_r001 = Sha $AcceptancePath
        broker_statement_accounting_dry_run_policy_r001 = Sha $AccountingPolicyPath
        broker_statement_accounting_dry_run_r001 = Sha $AccountingDryRunPath
        broker_statement_realized_unrealized_classification_r001 = Sha $ClassificationPath
        broker_statement_journal_dry_run_r001 = Sha $JournalPath
        accounting_close_gap_report_r001 = Sha $GapPath
    }
    broker_statement_accounting_dry_run = [ordered]@{
        ready = $true
        realised_pnl_before_costs_usd = $realisedBeforeCosts
        commission_expense_usd = $commissionExpense
        financing_expense_usd = $financingExpense
        realised_net_after_costs_usd = $realisedNetAfterCosts
        unrealized_open_pnl_usd = $unrealizedOpenPnl
        total_equity_pnl_including_open_pnl_usd = $equityPnlIncludingOpen
    }
    classification = [ordered]@{
        realized_components_ready = $true
        unrealized_components_ready = $true
        realized_accounting_close_ready = $false
    }
    journal_dry_run = [ordered]@{
        ready = $true
        commit_allowed = $false
        commit_eligible_entries = 0
        ledger_commit = $false
        db_mutation = $false
    }
    accounting_close_gap_report = [ordered]@{
        ready = $true
        realized_accounting_close_ready = $false
        blocked_reason = "ACCOUNTING_CLOSE_REQUIREMENTS_MISSING"
    }
    synthetic_sandbox_closeout_comparison = [ordered]@{
        comparison_performed = $true
        comparison_purpose = "diagnostic_only_not_accounting_dry_run_or_close_gate"
        synthetic_gross_usd = [decimal]-50.308800
        synthetic_commission_usd = [decimal]26.268029
        synthetic_net_usd = [decimal]-76.576829
        used_as_broker_statement_acceptance_gate = $false
        used_as_accounting_dry_run_gate = $false
        used_as_accounting_close_gate = $false
    }
    ready_outputs = [ordered]@{
        broker_statement_accounting_dry_run_ready = $true
        broker_statement_realized_unrealized_classification_ready = $true
        broker_statement_journal_dry_run_ready = $true
        accounting_close_gap_report_ready = $true
    }
    still_blocked = @(
        "real_accounting_evidence_acceptance",
        "realized_accounting_close",
        "internal_trade_reconciliation",
        "ledger_commit",
        "db_mutation",
        "production_live",
        "trading_readiness"
    )
    readiness = [ordered]@{
        broker_statement_accounting_dry_run_ready = $true
        broker_statement_realized_unrealized_classification_ready = $true
        broker_statement_journal_dry_run_ready = $true
        accounting_close_gap_report_ready = $true
        real_accounting_evidence_acceptance = $false
        realized_accounting_close = $false
        internal_trade_reconciliation_ready = $false
        ledger_commit = $false
        db_mutation = $false
        production_live = $false
        trading_readiness = $false
    }
    forbidden_ready_labels = [ordered]@{
        real_accounting_evidence_acceptance = $false
        realized_accounting_close = $false
        internal_trade_reconciliation_ready = $false
        committed_ledger = $false
        ledger_commit = $false
        db_mutation = $false
        production_live = $false
        trading_readiness = $false
    }
    global_guards = [ordered]@{
        external_calls = $false
        broker_api_calls = $false
        market_data_fetch = $false
        account_data_fetch = $false
        ledger_commit = $false
        db_mutation = $false
        trading_activity = $false
        production_live_ready = $false
        trading_readiness_ready = $false
    }
}
Write-JsonArtifact "broker-statement-accounting-dry-run-and-close-gate-r001.json" $main

$summary = @"
# Broker Statement Accounting Dry-Run And Close Gate R001

Source broker statement confirmed PnL status: $($brokerConfirmed.status)
Source real manual evidence acceptance status: $($acceptance.status)
Statement period: $($brokerConfirmed.broker_statement_scope.statement_period.from) to $($brokerConfirmed.broker_statement_scope.statement_period.to)

Broker-statement-backed accounting dry-run:
- Realised PnL before costs USD: $realisedBeforeCosts
- Commission expense USD: $commissionExpense
- Financing expense USD: $financingExpense
- Realised net after costs USD: $realisedNetAfterCosts
- Unrealized open PnL USD: $unrealizedOpenPnl
- Equity PnL including open PnL USD: $equityPnlIncludingOpen

Dry-run journal status: ready, dry-run only, zero commit-eligible entries.
Close gap report status: ready; realized accounting close remains blocked.

Missing close requirements:
- real accounting evidence acceptance
- accounting policy approval for realized close
- period close approval
- source-of-truth hierarchy approval
- internal trade-level reconciliation
- settlement/trade-date policy approval
- FX translation policy approval
- cost recognition policy approval
- financing recognition policy approval
- audit trail approval
- ledger handoff approval
- operator accounting close approval

The synthetic sandbox closeout remains diagnostic only and is not used as an accounting dry-run or accounting close gate.

Still blocked:
- real accounting evidence acceptance
- realized accounting close
- internal trade reconciliation
- ledger commit
- DB mutation
- production/live
- trading readiness

No trading, R009 submission, LMAX FIX/API call, broker API call, Polygon/Massive call, market-data fetch, broker fetch, account-data fetch, DB mutation, ledger commit, production/live action, or trading activity occurred.
"@
Write-TextArtifact "broker-statement-accounting-dry-run-and-close-gate-summary-r001.md" $summary

Write-Host "BROKER_STATEMENT_ACCOUNTING_DRY_RUN_AND_CLOSE_GATE_R001_BUILD_READY"
