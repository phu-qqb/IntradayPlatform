param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\account-currency-aggregation-r001"
$Required = @(
    "source-r003-intake-validation.json",
    "account-currency-policy-input.json",
    "fx-conversion-policy-input.json",
    "fx-conversion-rates-fixture.json",
    "quote-currency-source-buckets.json",
    "account-currency-aggregation-preview.json",
    "net-pnl-readiness-update.json",
    "contract-status-update.json",
    "boundary-safety-evidence.json",
    "summary.md"
)

function Read-Json([string]$Name) {
    Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir $Name) | ConvertFrom-Json
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

Assert-True (Test-Path -LiteralPath $ArtifactDir) "Account-currency aggregation artifact directory missing."
foreach ($name in $Required) {
    Assert-True (Test-Path -LiteralPath (Join-Path $ArtifactDir $name)) "Missing required R001 artifact: $name"
}

$intake = Read-Json "source-r003-intake-validation.json"
$accountPolicy = Read-Json "account-currency-policy-input.json"
$fxPolicy = Read-Json "fx-conversion-policy-input.json"
$rates = Read-Json "fx-conversion-rates-fixture.json"
$source = Read-Json "quote-currency-source-buckets.json"
$preview = Read-Json "account-currency-aggregation-preview.json"
$net = Read-Json "net-pnl-readiness-update.json"
$contract = Read-Json "contract-status-update.json"
$boundary = Read-Json "boundary-safety-evidence.json"
$summary = Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir "summary.md")

Assert-True ($intake.status -eq "R003_READY_FOR_ACCOUNT_CURRENCY_AGGREGATION_R001") "R003 intake not ready."
Assert-True ($intake.source_classification_confirmed -eq $true) "R003 classification not confirmed."
Assert-True ($intake.source_quote_currency_preview_computed -eq $true) "R003 quote-currency preview not ready."
Assert-True ($intake.source_no_trading_api_db_ledger -eq $true) "R003 source boundary not preserved."

Assert-True ($accountPolicy.account_currency -eq "USD") "Account currency must be explicit USD for this preview."
Assert-True ($accountPolicy.account_currency_was_inferred -eq $false) "Account currency must not be inferred."
Assert-True (-not [string]::IsNullOrWhiteSpace($accountPolicy.policy_version)) "Account currency policy version missing."
Assert-True (-not [string]::IsNullOrWhiteSpace($accountPolicy.policy_source)) "Account currency policy source missing."
Assert-True (-not [string]::IsNullOrWhiteSpace($accountPolicy.policy_sha256)) "Account currency policy hash missing."

Assert-True ($fxPolicy.conversion_rate_source -eq "checked_in_fixture") "FX conversion source must be checked_in_fixture."
Assert-True ($fxPolicy.conversion_once_only_guard -eq $true) "Conversion once-only guard missing."
Assert-True ($fxPolicy.external_fetch_required -eq $false) "FX conversion policy must not require external fetch."
foreach ($bad in @("live_market_data", "polygon", "massive", "lmax_market_data", "broker_api", "web_fetch")) {
    Assert-True (@($fxPolicy.disallowed_sources_rejected | Where-Object { $_ -eq $bad }).Count -eq 1) "$bad must be explicitly rejected."
}

Assert-True ($rates.status -eq "FX_CONVERSION_FIXTURE_READY") "FX fixture not ready."
foreach ($currency in @("CAD", "CNH", "JPY", "MXN", "NOK", "SEK", "SGD", "USD", "ZAR")) {
    Assert-True (@($rates.rates | Where-Object { $_.base_currency -eq $currency -and $_.account_currency -eq "USD" }).Count -eq 1) "$currency to USD fixture missing."
}

Assert-True ($source.status -eq "QUOTE_CURRENCY_BUCKETS_PRESERVED") "Quote-currency source buckets not preserved."
Assert-True ($source.source_buckets_preserved_before_conversion -eq $true) "Source buckets must be preserved before conversion."
Assert-True (@($source.line_items).Count -eq 9) "Expected 9 included source lines."
Assert-True (@($source.excluded_lines | Where-Object { $_.symbol -eq "USDJPY" -and $_.quantity -eq 50.0 -and $_.exclusion_reason -eq "unfilled_usdjpy_remaining_quantity_not_approved_for_retry" }).Count -eq 1) "Unfilled USDJPY 50.0 exclusion missing."
Assert-True (@($source.excluded_lines | Where-Object { $_.quantity -eq 0 }).Count -eq 4) "Zero-quantity exclusions missing."

Assert-True ($preview.status -eq "PREVIEW_ACCOUNT_CURRENCY_AGGREGATED") "Account-currency preview should be aggregated."
Assert-True ($preview.environment -eq "sandbox" -and $preview.mode -eq "preview_only") "Preview must be sandbox preview-only."
Assert-True ($preview.account_currency -eq "USD") "Preview account currency should be USD."
Assert-True ($preview.account_currency_preview.computed -eq $true) "Account-currency preview not computed."
Assert-True ($preview.account_currency_preview.full_net_pnl_ready -eq $false) "Preview must not mark full net PnL ready."
Assert-True (@($preview.fx_conversions).Count -eq 9) "Expected conversion evidence for each quote-currency bucket."
Assert-True ($preview.ledger_commit -eq $false -and $preview.db_mutation -eq $false -and $preview.external_calls -eq $false) "Preview claimed ledger/DB/external calls."
Assert-True ($preview.accounting_pnl_ready -eq $false -and $preview.production_live_ready -eq $false) "Preview claimed accounting/production readiness."
Assert-True (@($preview.blocked_items | Where-Object { $_ -eq "account_specific_commission_confirmation" }).Count -eq 1) "Account-specific commission confirmation blocker missing."

Assert-True ($net.status -eq "PREVIEW_ACCOUNT_CURRENCY_AGGREGATED") "Net readiness status should reflect preview aggregation only."
Assert-True ($net.full_net_pnl_ready -eq $false -and $net.full_net_pnl_blocked_reason -eq "BLOCKED_FULL_NET_PNL_NOT_READY") "Full net PnL must remain blocked."
Assert-True ($net.account_specific_commission_confirmation -eq "BLOCKED_ACCOUNT_SPECIFIC_COMMISSION_CONFIRMATION") "Account-specific commission confirmation must remain blocked."
Assert-True ($net.accounting_pnl_ready -eq $false -and $net.attribution_ready -eq $false -and $net.ledger_commit_ready -eq $false -and $net.production_live_ready -eq $false) "Accounting/attribution/ledger/production readiness must remain blocked."

Assert-True ($contract.statuses."account-currency-aggregation-preview.v1" -eq "YES_PREVIEW_ONLY") "Contract should mark preview only."
Assert-True ($contract.statuses."net-pnl-preview.v1" -eq "BLOCKED_FULL_NET_PNL_NOT_READY") "Net PnL contract must remain blocked."
Assert-True ($contract.statuses."accounting-attribution.v1" -eq "BLOCKED" -and $contract.statuses."ledger-commit.v1" -eq "BLOCKED" -and $contract.statuses."production-readiness.v1" -eq "BLOCKED") "Accounting/ledger/production contracts must remain blocked."

Assert-True ($boundary.no_trades -eq $true -and $boundary.no_r009_submission -eq $true -and $boundary.no_lmax_fix_api_call -eq $true) "Trading/LMAX boundary crossed."
Assert-True ($boundary.no_polygon_massive_call -eq $true -and $boundary.no_external_market_data_fetch -eq $true) "Market data/provider boundary crossed."
Assert-True ($boundary.no_order_fill_report -eq $true -and $boundary.no_db_mutation -eq $true -and $boundary.no_ledger_commit -eq $true) "Order/fill/DB/ledger boundary crossed."
Assert-True ($boundary.no_core_manager_anubis_cuda_netting -eq $true) "Core/manager/Anubis/CUDA/netting boundary crossed."
Assert-True ($boundary.no_accounting_pnl_ready -eq $true -and $boundary.no_full_net_pnl_ready -eq $true -and $boundary.no_production_live_ready -eq $true) "Readiness boundary crossed."
Assert-True ($boundary.deterministic_fixture_only -eq $true) "Expected deterministic fixture-only conversion."

Assert-True ($summary.Contains("Status: PREVIEW_ACCOUNT_CURRENCY_AGGREGATED")) "Summary missing preview status."
Assert-True ($summary.Contains("Account-currency preview computed: True")) "Summary missing computed preview."
Assert-True ($summary.Contains("Full net PnL ready: no")) "Summary must keep full net PnL blocked."
Assert-True ($summary.Contains("No trading")) "Summary missing no-activity boundary."

Write-Host "ACCOUNT_CURRENCY_AGGREGATION_R001_GATE_PASS"
