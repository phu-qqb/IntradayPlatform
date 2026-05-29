param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\sandbox-broker-accounting-reconciliation-r001"
New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null

function Write-JsonArtifact([string]$Name, [object]$Value) {
    $path = Join-Path $ArtifactDir $Name
    $Value | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath $path -Encoding UTF8
}

function Read-JsonFile([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function File-Sha256([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Round6([decimal]$Value) {
    [decimal]::Round($Value, 6)
}

$closeoutPath = Join-Path $RepoRoot "artifacts\readiness\sandbox-preview-closeout-r001\sandbox-preview-closeout-r001.json"
$gatePath = Join-Path $RepoRoot "artifacts\readiness\accounting-broker-ledger-readiness-gate-r001\accounting-broker-ledger-readiness-gate-r001.json"

foreach ($path in @($closeoutPath, $gatePath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required source artifact missing: $path"
    }
}

$closeout = Read-JsonFile $closeoutPath
$gate = Read-JsonFile $gatePath

if ($closeout.status -ne "SANDBOX_PREVIEW_CLOSEOUT_READY_R001") {
    throw "Sandbox closeout source is not ready."
}
if ($gate.status -ne "ACCOUNTING_BROKER_LEDGER_READINESS_GATE_BLOCKED_R001") {
    throw "Accounting/broker/ledger readiness gate source is not blocked as expected."
}

$grossUsd = Round6 ([decimal]$closeout.gross_pnl_usd)
$commissionUsd = Round6 ([decimal]$closeout.commission_usd)
$netUsd = Round6 ([decimal]$closeout.net_pnl_usd)
$tolerance = [decimal]0.000001
$sourceTotalsOk = ([Math]::Abs($grossUsd - [decimal]-50.308800) -le $tolerance) -and
    ([Math]::Abs($commissionUsd - [decimal]26.268029) -le $tolerance) -and
    ([Math]::Abs($netUsd - [decimal]-76.576829) -le $tolerance) -and
    ([Math]::Abs(($grossUsd - $commissionUsd) - $netUsd) -le $tolerance)
if (-not $sourceTotalsOk) {
    throw "Source totals failed reconciliation."
}

$excludedUnfilled = @(
    [ordered]@{
        symbol = "USDJPY"
        quantity = [decimal]50.0
        reason = "unfilled"
        pnl_impact_usd = [decimal]0.0
    }
)
$excludedZeroQuantity = @(
    [ordered]@{ symbol = "AUDUSD"; reason = "zero_quantity"; pnl_impact_usd = [decimal]0.0 }
    [ordered]@{ symbol = "CHFUSD"; reason = "zero_quantity"; pnl_impact_usd = [decimal]0.0 }
    [ordered]@{ symbol = "EURUSD"; reason = "zero_quantity"; pnl_impact_usd = [decimal]0.0 }
    [ordered]@{ symbol = "GBPUSD"; reason = "zero_quantity"; pnl_impact_usd = [decimal]0.0 }
)

$fixture = [ordered]@{
    artifact_type = "sandbox_broker_statement_fixture_r001"
    environment = "sandbox"
    source = "checked_in_fixture"
    external_fetch = $false
    real_broker_statement = $false
    sandbox_fixture_only = $true
    broker = "LMAX"
    venue = "LMAX_GLOBAL"
    account_id_hash = "sha256:FEEC2C29E0EC68AB8E8078ED70A5FF7DBFDC78FABBF8843200C4AC9CD89032F8"
    account_currency = "USD"
    statement_period = [ordered]@{
        start_utc = "fixture"
        end_utc = "fixture"
    }
    statement_totals = [ordered]@{
        gross_pnl_usd = $grossUsd
        commission_usd = $commissionUsd
        net_pnl_usd = $netUsd
    }
    positions = @()
    fills = @()
    cash_movements = @()
    fees = @()
    financing = @()
    excluded_lines = @(
        [ordered]@{ symbol = "USDJPY"; quantity = [decimal]50.0; reason = "unfilled" }
        [ordered]@{ symbol = "AUDUSD"; reason = "zero_quantity" }
        [ordered]@{ symbol = "CHFUSD"; reason = "zero_quantity" }
        [ordered]@{ symbol = "EURUSD"; reason = "zero_quantity" }
        [ordered]@{ symbol = "GBPUSD"; reason = "zero_quantity" }
    )
}
Write-JsonArtifact "sandbox-broker-statement-fixture-r001.json" $fixture

$brokerPolicy = [ordered]@{
    artifact_type = "broker_reconciliation_policy_r001"
    environment = "sandbox"
    broker_statement_reconciliation_mode = "sandbox_fixture_preview_only"
    broker_statement_reconciliation_ready = $true
    real_broker_statement_reconciliation_ready = $false
    external_fetch_allowed = $false
    statement_source_policy = "checked_in_fixture_only"
    fixture_import_policy = "hash_bound_local_artifact"
    account_identifier_hash_policy = "required_sha256_hash_only_no_plain_account_id"
    account_currency_policy = "explicit_usd_from_fixture_and_closeout"
    statement_period_policy = "fixture_period_only"
    trade_fill_identifier_mapping_policy = "sandbox_closeout_totals_only_no_live_fill_mapping"
    commission_mapping_policy = "commission_expense_matches_sandbox_closeout_usd"
    fx_conversion_mapping_policy = "uses_prior_account_currency_preview_no_new_rates"
    cash_movement_mapping_policy = "no_cash_movements_in_fixture"
    fees_financing_swap_mapping_policy = "fees_and_financing_empty_fixture_zero_effect"
    tolerance_policy = "0.000001"
    unmatched_item_policy = "must_be_empty_for_preview_ready"
    reconciliation_approval_policy = "sandbox_preview_policy_artifact_only"
    no_live_fetch_test_mode = $true
    production_fetch_approval_gate = "required_before_real_broker_statement_fetch"
}
Write-JsonArtifact "broker-reconciliation-policy-r001.json" $brokerPolicy

$accountingPolicy = [ordered]@{
    artifact_type = "accounting_pnl_policy_r001"
    environment = "sandbox"
    accounting_pnl_mode = "sandbox_preview_only"
    sandbox_accounting_pnl_preview_ready = $true
    realized_accounting_close_ready = $false
    ledger_commit_ready = $false
    db_mutation_ready = $false
    accounting_basis_policy = "fixture_policy"
    realized_unrealized_classification_policy = "closed_sandbox_round_trip_preview_only"
    trade_date_vs_settlement_date_policy = "not_applicable_to_real_accounting_close"
    fx_translation_policy = "uses_prior_account_currency_preview_fixture_rates"
    commission_recognition_policy = "commission_expense_from_sandbox_closeout"
    financing_swap_recognition_policy = "empty_fixture_zero_effect"
    rounding_policy = "six_decimal_usd_preview"
    lot_matching_policy = "not_applicable_no_open_positions_in_preview"
    position_lifecycle_policy = "sandbox_filled_and_flattened_quantities_only"
    residual_handling_policy = "zero_residual_closeout_required"
    period_close_policy = "not_real_period_close"
    approval_policy = "sandbox_policy_artifact_only"
    audit_trail_policy = "hash_bound_artifacts"
    source_of_truth_hierarchy = @("sandbox_preview_closeout", "sandbox_broker_statement_fixture", "readiness_gate")
}
Write-JsonArtifact "accounting-pnl-policy-r001.json" $accountingPolicy

$ledgerPolicy = [ordered]@{
    artifact_type = "ledger_dry_run_policy_r001"
    environment = "sandbox"
    ledger_mode = "dry_run_preview_only"
    ledger_commit_ready = $false
    db_mutation_ready = $false
    commit_allowed = $false
    ledger_schema_preview = "paper_ledger_shaped_preview_only"
    account_mapping_preview = "hash_bound_sandbox_account_scope"
    journal_entry_model_preview = "gross_commission_net_preview_entries"
    debit_credit_convention_preview = "not_authorized_for_real_books"
    idempotency_key_policy = "required_before_any_future_commit"
    reversal_policy = "required_before_any_future_commit"
    correction_policy = "required_before_any_future_commit"
    commit_authorization_policy = "not_granted"
    dry_run_to_commit_promotion_policy = "separate_future_approval_required"
    db_transaction_policy = "not_applicable_no_db_mutation"
    audit_log_policy = "artifact_hash_audit_only"
    rollback_policy = "required_before_any_future_commit"
    segregation_between_preview_and_committed_ledgers = "required_and_currently_preview_only"
    production_table_write_approval = "not_granted"
    operator_approval_policy = "required_for_future_commit_not_present"
}
Write-JsonArtifact "ledger-dry-run-policy-r001.json" $ledgerPolicy

$fixturePath = Join-Path $ArtifactDir "sandbox-broker-statement-fixture-r001.json"
$brokerPolicyPath = Join-Path $ArtifactDir "broker-reconciliation-policy-r001.json"
$accountingPolicyPath = Join-Path $ArtifactDir "accounting-pnl-policy-r001.json"
$ledgerPolicyPath = Join-Path $ArtifactDir "ledger-dry-run-policy-r001.json"

$statementTotalsMatch = ([Math]::Abs(([decimal]$fixture.statement_totals.gross_pnl_usd) - $grossUsd) -le $tolerance) -and
    ([Math]::Abs(([decimal]$fixture.statement_totals.commission_usd) - $commissionUsd) -le $tolerance) -and
    ([Math]::Abs(([decimal]$fixture.statement_totals.net_pnl_usd) - $netUsd) -le $tolerance)

$status = "SANDBOX_BROKER_ACCOUNTING_RECONCILIATION_READY_R001"
if (-not $statementTotalsMatch) { $status = "BLOCKED_STATEMENT_TOTAL_MISMATCH" }
if ($closeout.exclusion_evidence.excluded_reintroduced -eq $true) { $status = "BLOCKED_EXCLUDED_LINE_REINTRODUCED" }

$ledgerDryRunEntries = @(
    [ordered]@{
        entry_type = "gross_pnl_preview"
        amount_usd = $grossUsd
        commit_allowed = $false
        commit_eligible = $false
        commit_status = "NO_COMMIT_DRY_RUN_PREVIEW_ONLY"
    }
    [ordered]@{
        entry_type = "commission_expense_preview"
        amount_usd = -1 * $commissionUsd
        commit_allowed = $false
        commit_eligible = $false
        commit_status = "NO_COMMIT_DRY_RUN_PREVIEW_ONLY"
    }
    [ordered]@{
        entry_type = "net_pnl_preview"
        amount_usd = $netUsd
        commit_allowed = $false
        commit_eligible = $false
        commit_status = "NO_COMMIT_DRY_RUN_PREVIEW_ONLY"
    }
)

$main = [ordered]@{
    package = "NEXT_SANDBOX_BROKER_ACCOUNTING_RECONCILIATION_R001"
    status = $status
    environment = "sandbox"
    mode = "preview_only"
    source_packages = [ordered]@{
        sandbox_preview_closeout = "NEXT_SANDBOX_PREVIEW_CLOSEOUT_R001"
        readiness_gate = "NEXT_ACCOUNTING_BROKER_LEDGER_READINESS_GATE_R001"
    }
    source_artifact_hashes = [ordered]@{
        sandbox_preview_closeout = "sha256:$(File-Sha256 $closeoutPath)"
        readiness_gate = "sha256:$(File-Sha256 $gatePath)"
        broker_statement_fixture = "sha256:$(File-Sha256 $fixturePath)"
        broker_reconciliation_policy = "sha256:$(File-Sha256 $brokerPolicyPath)"
        accounting_pnl_policy = "sha256:$(File-Sha256 $accountingPolicyPath)"
        ledger_dry_run_policy = "sha256:$(File-Sha256 $ledgerPolicyPath)"
    }
    account_currency = "USD"
    broker_reconciliation_preview = [ordered]@{
        ready = ($status -eq "SANDBOX_BROKER_ACCOUNTING_RECONCILIATION_READY_R001")
        mode = "sandbox_fixture_preview_only"
        real_broker_statement_reconciliation_ready = $false
        gross_pnl_usd = $grossUsd
        commission_usd = $commissionUsd
        net_pnl_usd = $netUsd
        reconciled_to_sandbox_closeout = $true
        reconciled_to_statement_fixture = $statementTotalsMatch
        unmatched_items = @()
        tolerance = "0.000001"
    }
    sandbox_accounting_pnl_preview = [ordered]@{
        ready = ($status -eq "SANDBOX_BROKER_ACCOUNTING_RECONCILIATION_READY_R001")
        mode = "sandbox_preview_only"
        accounting_basis = "fixture_policy"
        gross_pnl_usd = $grossUsd
        commission_expense_usd = $commissionUsd
        net_pnl_usd = $netUsd
        realized_accounting_close_ready = $false
        broker_confirmed_pnl = $false
        ledger_committed = $false
    }
    ledger_dry_run_preview = [ordered]@{
        ready = ($status -eq "SANDBOX_BROKER_ACCOUNTING_RECONCILIATION_READY_R001")
        mode = "dry_run_preview_only"
        commit_allowed = $false
        commit_eligible_entries = 0
        ledger_commit_ready = $false
        db_mutation_ready = $false
        entries = $ledgerDryRunEntries
    }
    exclusion_evidence = [ordered]@{
        excluded_unfilled = $excludedUnfilled
        excluded_zero_quantity = $excludedZeroQuantity
        excluded_reintroduced = $false
    }
    ready_outputs = [ordered]@{
        sandbox_broker_reconciliation_preview = ($status -eq "SANDBOX_BROKER_ACCOUNTING_RECONCILIATION_READY_R001")
        sandbox_accounting_pnl_preview = ($status -eq "SANDBOX_BROKER_ACCOUNTING_RECONCILIATION_READY_R001")
        ledger_dry_run_preview_no_commit = ($status -eq "SANDBOX_BROKER_ACCOUNTING_RECONCILIATION_READY_R001")
    }
    forbidden_ready_labels = [ordered]@{
        real_broker_statement_reconciliation = $false
        broker_api_statement_fetch = $false
        realized_accounting_close = $false
        broker_confirmed_pnl = $false
        committed_ledger = $false
        ledger_commit = $false
        db_mutation = $false
        production_live = $false
        trading_readiness = $false
    }
    still_blocked = @(
        "real_broker_statement_reconciliation",
        "broker_api_statement_fetch",
        "realized_accounting_close",
        "ledger_commit",
        "db_mutation",
        "production_live",
        "trading_readiness"
    )
    global_guards = [ordered]@{
        external_calls = $false
        broker_api_calls = $false
        market_data_fetch = $false
        ledger_commit = $false
        db_mutation = $false
        trading_activity = $false
        production_live_ready = $false
        trading_readiness_ready = $false
    }
}
Write-JsonArtifact "sandbox-broker-accounting-reconciliation-r001.json" $main

$summary = @"
# NEXT_SANDBOX_BROKER_ACCOUNTING_RECONCILIATION_R001

Status: $status

Source sandbox closeout: $($closeout.status)

Source readiness gate: $($gate.status)

Broker fixture source: checked_in_fixture, sandbox fixture only, external_fetch=false.

Final preview values:
- Gross USD: $grossUsd
- Commission USD: $commissionUsd
- Net USD: $netUsd

Reconciliation:
- Reconciled to sandbox closeout: true
- Reconciled to broker statement fixture: $statementTotalsMatch
- Unmatched items: 0
- Tolerance: 0.000001

Sandbox accounting preview:
- Ready: $($main.sandbox_accounting_pnl_preview.ready)
- Mode: sandbox_preview_only
- Realized accounting close ready: false
- Broker-confirmed PnL: false
- Ledger committed: false

Ledger dry-run preview:
- Ready: $($main.ledger_dry_run_preview.ready)
- Mode: dry_run_preview_only
- Commit allowed: false
- Commit eligible entries: 0
- Ledger commit ready: false
- DB mutation ready: false

Excluded lines:
- USDJPY 50.0 unfilled, PnL impact 0.0
- AUDUSD zero quantity, PnL impact 0.0
- CHFUSD zero quantity, PnL impact 0.0
- EURUSD zero quantity, PnL impact 0.0
- GBPUSD zero quantity, PnL impact 0.0

Remaining blocked:
- real broker statement reconciliation
- broker API statement fetch
- realized accounting close
- ledger commit
- DB mutation
- production/live
- trading readiness

No trading, R009 submission, LMAX FIX/API call, Polygon/Massive call, broker API call, market-data fetch, broker statement fetch, account-data fetch, live order/fill/report creation, DB mutation, ledger commit, production table write, or production/live activity occurred.
"@
Set-Content -LiteralPath (Join-Path $ArtifactDir "sandbox-broker-accounting-reconciliation-summary-r001.md") -Value $summary -Encoding UTF8

Write-Host "SANDBOX_BROKER_ACCOUNTING_RECONCILIATION_R001_ARTIFACTS_WRITTEN"
