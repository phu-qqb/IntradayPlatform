param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\sandbox-preview-closeout-r001"
New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null

function Write-JsonArtifact([string]$Name, [object]$Value) {
    $path = Join-Path $ArtifactDir $Name
    $Value | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $path -Encoding UTF8
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

$r003Path = Join-Path $RepoRoot "artifacts\readiness\risk-cost-model-r003-lmax-public-charges-import\r013d-cost-adjusted-sandbox-preview.json"
$r003SummaryPath = Join-Path $RepoRoot "artifacts\readiness\risk-cost-model-r003-lmax-public-charges-import\summary.md"
$aggregationPath = Join-Path $RepoRoot "artifacts\readiness\account-currency-aggregation-r001\account-currency-aggregation-preview.json"
$commissionConfirmationPath = Join-Path $RepoRoot "artifacts\readiness\account-specific-commission-confirmation-r001\account-specific-commission-confirmation-output.json"
$attributionPath = Join-Path $RepoRoot "artifacts\readiness\sandbox-full-net-pnl-attribution-r001\sandbox-full-net-pnl-attribution-output.json"

$requiredPaths = @($r003Path, $r003SummaryPath, $aggregationPath, $commissionConfirmationPath, $attributionPath)
foreach ($path in $requiredPaths) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required source artifact missing: $path"
    }
}

$r003 = Read-JsonFile $r003Path
$r003Summary = Get-Content -Raw -LiteralPath $r003SummaryPath
$aggregation = Read-JsonFile $aggregationPath
$commissionConfirmation = Read-JsonFile $commissionConfirmationPath
$attribution = Read-JsonFile $attributionPath

$grossUsd = Round6 ([decimal]$attribution.attribution_bridge.gross_pnl_usd)
$commissionUsd = Round6 ([decimal]$attribution.attribution_bridge.commission_usd)
$netUsd = Round6 ([decimal]$attribution.attribution_bridge.net_pnl_usd)
$tolerance = [decimal]0.000001
$formulaReconciled = ([Math]::Abs(($grossUsd - $commissionUsd) - $netUsd) -le $tolerance)
$expectedValuesReconciled = ([Math]::Abs($grossUsd - [decimal]-50.308800) -le $tolerance) -and
    ([Math]::Abs($commissionUsd - [decimal]26.268029) -le $tolerance) -and
    ([Math]::Abs($netUsd - [decimal]-76.576829) -le $tolerance)
$reconciled = ($attribution.attribution_bridge.reconciled -eq $true) -and $formulaReconciled -and $expectedValuesReconciled
if (-not $reconciled) {
    throw "Closeout reconciliation failed."
}

$sourceArtifacts = @(
    [ordered]@{
        package = "RISK_COST_MODEL_R003"
        artifact_path = $r003Path
        sha256 = "sha256:$(File-Sha256 $r003Path)"
        package_status = "RISK_COST_MODEL_R003_WITH_WARNINGS_PUBLIC_COST_EVIDENCE_IMPORTED_ACCOUNT_CURRENCY_BLOCKED"
        accepted_result = "public_lmax_charges_imported_quote_currency_cost_preview_computed_account_currency_blocked"
        key_values_consumed = [ordered]@{
            public_lmax_commission_policy = "0.0025_percent_notional_second_named_currency"
            covered_symbols = @($r003.GrossPnlRows | ForEach-Object { $_.ExecutionSymbol })
            unfilled_usdjpy_50_excluded = $true
            zero_quantity_lines_excluded = $true
        }
    }
    [ordered]@{
        package = "NEXT_ACCOUNT_CURRENCY_AGGREGATION_R001"
        artifact_path = $aggregationPath
        sha256 = "sha256:$(File-Sha256 $aggregationPath)"
        package_status = $aggregation.status
        accepted_result = "account_currency_preview_computed_usd_fixture_rates"
        key_values_consumed = [ordered]@{
            account_currency = $aggregation.account_currency
            gross_pnl_usd = [decimal]$aggregation.account_currency_preview.gross_pnl_account_currency
            commission_usd = [decimal]$aggregation.account_currency_preview.commission_account_currency
            net_pnl_usd = [decimal]$aggregation.account_currency_preview.net_pnl_account_currency
            external_calls = $aggregation.external_calls
            db_mutation = $aggregation.db_mutation
            ledger_commit = $aggregation.ledger_commit
        }
    }
    [ordered]@{
        package = "NEXT_ACCOUNT_SPECIFIC_COMMISSION_CONFIRMATION_R001"
        artifact_path = $commissionConfirmationPath
        sha256 = "sha256:$(File-Sha256 $commissionConfirmationPath)"
        package_status = $commissionConfirmation.status
        accepted_result = "account_specific_commission_confirmed_sandbox_full_net_pnl_preview_ready"
        key_values_consumed = [ordered]@{
            commission_confirmation_status = $commissionConfirmation.status
            sandbox_full_net_pnl_preview_ready = $commissionConfirmation.sandbox_full_net_pnl_preview.ready
            gross_pnl_usd = [decimal]$commissionConfirmation.sandbox_full_net_pnl_preview.gross_pnl_account_currency
            commission_usd = [decimal]$commissionConfirmation.sandbox_full_net_pnl_preview.commission_account_currency
            net_pnl_usd = [decimal]$commissionConfirmation.sandbox_full_net_pnl_preview.net_pnl_account_currency
        }
    }
    [ordered]@{
        package = "NEXT_SANDBOX_FULL_NET_PNL_ATTRIBUTION_R001"
        artifact_path = $attributionPath
        sha256 = "sha256:$(File-Sha256 $attributionPath)"
        package_status = $attribution.status
        accepted_result = "sandbox_full_net_pnl_attribution_preview_ready"
        key_values_consumed = [ordered]@{
            gross_pnl_usd = $grossUsd
            commission_usd = $commissionUsd
            net_pnl_usd = $netUsd
            reconciled = $attribution.attribution_bridge.reconciled
            tolerance = $attribution.attribution_bridge.tolerance
            excluded_unfilled = $attribution.exclusion_attribution.excluded_unfilled
            excluded_zero_quantity_symbols = $attribution.exclusion_attribution.excluded_zero_quantity_symbols
        }
    }
)

$sourceHashes = [ordered]@{}
foreach ($source in $sourceArtifacts) {
    $sourceHashes[$source.package] = $source.sha256
}

$blockedState = [ordered]@{
    package = "NEXT_SANDBOX_PREVIEW_CLOSEOUT_R001"
    status = "SANDBOX_PREVIEW_BLOCKED_STATE_CERTIFIED_R001"
    accounting_pnl_ready = $false
    realized_accounting_pnl_ready = $false
    broker_statement_reconciliation_ready = $false
    ledger_commit_ready = $false
    db_mutation_allowed = $false
    production_live_ready = $false
    trading_readiness_ready = $false
    ledger_commit = $false
    db_mutation = $false
    external_calls = $false
    trading_activity = $false
    blocked_items = @(
        "accounting_pnl",
        "realized_accounting_pnl",
        "broker_statement_reconciliation",
        "ledger_commit",
        "db_mutation",
        "production_live",
        "trading_readiness"
    )
}
Write-JsonArtifact "sandbox-preview-blocked-state-certificate-r001.json" $blockedState

$paperLedgerEntries = @(
    [ordered]@{
        entry_id = "sandbox-preview-closeout-r001:gross-pnl-preview"
        entry_type = "gross_pnl_preview"
        environment = "sandbox"
        mode = "preview_only"
        currency = "USD"
        amount = $grossUsd
        signed_impact = $grossUsd
        source = "NEXT_SANDBOX_FULL_NET_PNL_ATTRIBUTION_R001"
        commit_eligible = $false
        commit_status = "NO_COMMIT_PREVIEW_ONLY"
        committed_at_utc = $null
    }
    [ordered]@{
        entry_id = "sandbox-preview-closeout-r001:commission-preview"
        entry_type = "commission_preview"
        environment = "sandbox"
        mode = "preview_only"
        currency = "USD"
        amount = $commissionUsd
        signed_impact = -1 * $commissionUsd
        source = "NEXT_SANDBOX_FULL_NET_PNL_ATTRIBUTION_R001"
        commit_eligible = $false
        commit_status = "NO_COMMIT_PREVIEW_ONLY"
        committed_at_utc = $null
    }
    [ordered]@{
        entry_id = "sandbox-preview-closeout-r001:net-pnl-preview"
        entry_type = "net_pnl_preview"
        environment = "sandbox"
        mode = "preview_only"
        currency = "USD"
        amount = $netUsd
        signed_impact = $netUsd
        source = "NEXT_SANDBOX_FULL_NET_PNL_ATTRIBUTION_R001"
        commit_eligible = $false
        commit_status = "NO_COMMIT_PREVIEW_ONLY"
        committed_at_utc = $null
    }
)

$nonEventConfirmation = [ordered]@{
    no_trades = $true
    no_r009_submission = $true
    no_lmax_fix_api_call = $true
    no_polygon_massive_call = $true
    no_broker_api_call = $true
    no_market_data_fetch = $true
    no_account_data_fetch = $true
    no_order_fill_report_creation_from_live_sources = $true
    no_db_mutation = $true
    no_ledger_commit = $true
    no_production_live_activity = $true
}

$main = [ordered]@{
    package = "NEXT_SANDBOX_PREVIEW_CLOSEOUT_R001"
    status = "SANDBOX_PREVIEW_CLOSEOUT_READY_R001"
    environment = "sandbox"
    mode = "preview_only"
    account_currency = "USD"
    gross_pnl_usd = $grossUsd
    commission_usd = $commissionUsd
    net_pnl_usd = $netUsd
    reconciled = $reconciled
    tolerance = "0.000001"
    formula = "net_pnl_usd = gross_pnl_usd - commission_usd"
    source_packages = [ordered]@{
        risk_cost_model = "RISK_COST_MODEL_R003"
        account_currency_aggregation = "NEXT_ACCOUNT_CURRENCY_AGGREGATION_R001"
        account_specific_commission_confirmation = "NEXT_ACCOUNT_SPECIFIC_COMMISSION_CONFIRMATION_R001"
        sandbox_full_net_pnl_attribution = "NEXT_SANDBOX_FULL_NET_PNL_ATTRIBUTION_R001"
    }
    source_artifact_hashes = $sourceHashes
    paper_ledger_shaped_preview_entries = $paperLedgerEntries
    exclusion_evidence = [ordered]@{
        excluded_unfilled = $attribution.exclusion_attribution.excluded_unfilled
        excluded_zero_quantity_count = $attribution.exclusion_attribution.excluded_zero_quantity_count
        excluded_zero_quantity_symbols = $attribution.exclusion_attribution.excluded_zero_quantity_symbols
        excluded_reintroduced = $attribution.exclusion_attribution.excluded_reintroduced
        excluded_lines_contribute_to_pnl = $false
    }
    blocked_state_certificate = $blockedState
    non_event_confirmation = $nonEventConfirmation
    no_commit_confirmation = [ordered]@{
        ledger_commit = $false
        all_preview_entries_commit_eligible = $false
        all_preview_entries_commit_status = "NO_COMMIT_PREVIEW_ONLY"
        committed_at_utc_present = $false
    }
}
Write-JsonArtifact "sandbox-preview-closeout-r001.json" $main

$manifest = [ordered]@{
    package = "NEXT_SANDBOX_PREVIEW_CLOSEOUT_R001"
    status = "SANDBOX_PREVIEW_EVIDENCE_MANIFEST_READY_R001"
    source_artifacts = $sourceArtifacts
    source_summary = [ordered]@{
        r003_summary_path = $r003SummaryPath
        r003_summary_sha256 = "sha256:$(File-Sha256 $r003SummaryPath)"
        r003_summary_contains_expected_classification = $r003Summary.Contains("RISK_COST_MODEL_R003_WITH_WARNINGS_PUBLIC_COST_EVIDENCE_IMPORTED_ACCOUNT_CURRENCY_BLOCKED")
        attribution_status = $attribution.status
        account_currency = "USD"
        gross_pnl_usd = $grossUsd
        commission_usd = $commissionUsd
        net_pnl_usd = $netUsd
    }
}
Write-JsonArtifact "sandbox-preview-evidence-manifest-r001.json" $manifest

$summary = @"
# NEXT_SANDBOX_PREVIEW_CLOSEOUT_R001

Status: SANDBOX_PREVIEW_CLOSEOUT_READY_R001

Final sandbox preview values:
- Gross USD: $grossUsd
- Commission USD: $commissionUsd
- Net USD: $netUsd
- Reconciled: $reconciled
- Tolerance: 0.000001

Paper-ledger-shaped preview:
- Gross PnL preview entry: $grossUsd USD
- Commission preview entry signed impact: $(-1 * $commissionUsd) USD
- Net PnL preview entry: $netUsd USD
- Commit eligible: false for every entry
- Commit status: NO_COMMIT_PREVIEW_ONLY

Excluded lines preserved:
- USDJPY 50.0 unfilled
- AUDUSD zero quantity
- CHFUSD zero quantity
- EURUSD zero quantity
- GBPUSD zero quantity

Source packages:
- RISK_COST_MODEL_R003
- NEXT_ACCOUNT_CURRENCY_AGGREGATION_R001
- NEXT_ACCOUNT_SPECIFIC_COMMISSION_CONFIRMATION_R001
- NEXT_SANDBOX_FULL_NET_PNL_ATTRIBUTION_R001

Remaining blocked:
- accounting PnL
- realized accounting PnL
- broker statement reconciliation
- ledger commit
- DB mutation
- production/live
- trading readiness

Validation: closeout test and gate scripts validate the chain end to end.

No trading, R009 submission, LMAX FIX/API call, Polygon/Massive call, broker API call, market-data fetch, account-data fetch, live order/fill/report creation, DB mutation, ledger commit, or production/live activity occurred.
"@
Set-Content -LiteralPath (Join-Path $ArtifactDir "sandbox-preview-closeout-summary-r001.md") -Value $summary -Encoding UTF8

Write-Host "SANDBOX_PREVIEW_CLOSEOUT_R001_ARTIFACTS_WRITTEN"
