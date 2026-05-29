param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\account-specific-commission-confirmation-r001"
$Required = @(
    "account-specific-commission-confirmation-input.json",
    "compatibility-validation.json",
    "sandbox-full-net-pnl-preview-readiness.json",
    "account-specific-commission-confirmation-output.json",
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

Assert-True (Test-Path -LiteralPath $ArtifactDir) "Account-specific commission confirmation artifact directory missing."
foreach ($name in $Required) {
    Assert-True (Test-Path -LiteralPath (Join-Path $ArtifactDir $name)) "Missing required R001 artifact: $name"
}

$input = Read-Json "account-specific-commission-confirmation-input.json"
$compat = Read-Json "compatibility-validation.json"
$readiness = Read-Json "sandbox-full-net-pnl-preview-readiness.json"
$output = Read-Json "account-specific-commission-confirmation-output.json"
$contract = Read-Json "contract-status-update.json"
$boundary = Read-Json "boundary-safety-evidence.json"
$summary = Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir "summary.md")

Assert-True ($input.artifact_type -eq "qq.account_specific_commission_confirmation.r001") "Input artifact type wrong."
Assert-True ($input.environment -eq "sandbox") "Input environment must be sandbox."
Assert-True ($input.account_scope.broker -eq "LMAX" -and $input.account_scope.venue -eq "LMAX_GLOBAL") "Account scope broker/venue wrong."
Assert-True (-not [string]::IsNullOrWhiteSpace($input.account_scope.account_id_hash)) "Account id hash missing."
Assert-True ($input.account_scope.account_id_hash.StartsWith("sha256:")) "Account id hash must be sha256-bound."
Assert-True ($input.account_scope.account_currency -eq "USD" -and $input.account_scope.product_class -eq "FX") "Account currency/product class wrong."
Assert-True (@($input.account_scope.symbols).Count -eq 9) "Account symbol scope must cover 9 R013D symbols."
foreach ($symbol in @("USDCAD", "USDCNH", "USDJPY", "USDMXN", "USDNOK", "NZDUSD", "USDSEK", "USDSGD", "USDZAR")) {
    Assert-True (@($input.account_scope.symbols | Where-Object { $_ -eq $symbol }).Count -eq 1) "$symbol missing from account scope."
}

Assert-True ($input.commission_policy.rate_percent -eq 0.0025) "Commission rate mismatch."
Assert-True ($input.commission_policy.basis -eq "notional_traded") "Commission basis mismatch."
Assert-True ($input.commission_policy.charge_currency -eq "second_named_currency") "Charge currency policy mismatch."
Assert-True ($input.commission_policy.minimum_commission -eq $null) "Minimum commission should be null."
Assert-True ($input.commission_policy.tiering -eq "none") "Tiering should be none."
Assert-True ($input.commission_policy.rounding_policy -eq "preserve_existing_r003_rounding") "Rounding policy mismatch."
Assert-True ($input.commission_policy.source -eq "account_specific_confirmation") "Input must be account-specific, not public-only."

Assert-True ($input.compatibility.public_lmax_charges_policy_used_by_r003 -eq $true -and $input.compatibility.matches_r003_public_policy -eq $true -and $input.compatibility.matches_r001_account_currency_preview_inputs -eq $true) "Compatibility flags not all true."
Assert-True ($input.approval.approved_by -eq "sandbox-operator") "Approval missing."
Assert-True ($input.approval.approved_at_utc -eq "fixture") "Approval timestamp should be fixture."
Assert-True ($input.approval.approval_scope -eq "sandbox_preview_only") "Approval scope invalid."
Assert-True ($input.approval.approval_sha256.StartsWith("sha256:")) "Approval hash missing."

Assert-True ($compat.status -eq "ACCOUNT_SPECIFIC_COMMISSION_CONFIRMED_R001") "Compatibility status not confirmed."
Assert-True ($compat.r003_public_commission_rate_percent -eq 0.0025 -and $compat.r003_basis -eq "notional_traded" -and $compat.r003_charge_currency -eq "second_named_currency") "R003 policy compatibility wrong."
Assert-True ($compat.r001_account_currency_explicit_usd -eq $true -and $compat.r001_checked_in_fixture_rates -eq $true) "R001 account currency/fixture compatibility wrong."
Assert-True ($compat.r001_ledger_commit_false -eq $true -and $compat.r001_db_mutation_false -eq $true -and $compat.r001_external_calls_false -eq $true) "R001 boundary compatibility wrong."
Assert-True ($compat.r001_full_net_pnl_blocked_before_package -eq $true) "R001 should have blocked full net before this package."
Assert-True ($compat.symbols_cover_every_included_r013d_symbol -eq $true) "Symbol scope does not cover all included R013D symbols."
Assert-True ($compat.unfilled_usdjpy_50_excluded -eq $true -and $compat.zero_quantity_lines_excluded -eq $true -and $compat.no_excluded_line_reintroduced -eq $true) "Excluded line safety failed."
Assert-True ($compat.source_artifact_hashes_match -eq $true) "Source artifact hashes do not match."

Assert-True ($readiness.status -eq "SANDBOX_FULL_NET_PNL_PREVIEW_READY") "Sandbox full net preview readiness not ready."
Assert-True ($readiness.sandbox_full_net_pnl_preview.ready -eq $true) "Sandbox full net preview not marked ready."
Assert-True ($readiness.sandbox_full_net_pnl_preview.gross_pnl_account_currency -eq -50.308800) "Gross USD mismatch."
Assert-True ($readiness.sandbox_full_net_pnl_preview.commission_account_currency -eq 26.268029) "Commission USD mismatch."
Assert-True ($readiness.sandbox_full_net_pnl_preview.net_pnl_account_currency -eq -76.576829) "Net USD mismatch."
Assert-True ($readiness.sandbox_full_net_pnl_preview.commission_confirmation -eq "ACCOUNT_SPECIFIC_COMMISSION_CONFIRMED_R001") "Commission confirmation not bound to readiness."
foreach ($forbidden in @("accounting PnL", "realized PnL", "ledger PnL", "production PnL", "live PnL", "broker-confirmed PnL")) {
    Assert-True (@($readiness.forbidden_names | Where-Object { $_ -eq $forbidden }).Count -eq 1) "$forbidden must remain forbidden naming."
}

Assert-True ($output.status -eq "ACCOUNT_SPECIFIC_COMMISSION_CONFIRMED_R001") "Output status not confirmed."
Assert-True ($output.sandbox_full_net_pnl_preview.ready -eq $true) "Output sandbox full net preview not ready."
Assert-True ($output.sandbox_full_net_pnl_preview.net_pnl_account_currency -eq -76.576829) "Output net USD mismatch."
Assert-True ($output.ledger_commit -eq $false -and $output.db_mutation -eq $false -and $output.external_calls -eq $false -and $output.trading_activity -eq $false) "Output claimed ledger/DB/external/trading."
Assert-True ($output.accounting_pnl_ready -eq $false -and $output.production_live_ready -eq $false) "Output claimed accounting/production readiness."
foreach ($blocked in @("accounting_pnl_attribution", "ledger_commit", "production_live", "broker_statement_reconciliation", "realized_accounting_pnl")) {
    Assert-True (@($output.still_blocked | Where-Object { $_ -eq $blocked }).Count -eq 1) "$blocked must remain blocked."
}

Assert-True ($contract.statuses."account-specific-commission-confirmation.v1" -eq "YES_SANDBOX_PREVIEW_ONLY") "Contract should confirm account-specific commission for sandbox preview only."
Assert-True ($contract.statuses."sandbox-full-net-pnl-preview.v1" -eq "YES_PREVIEW_ONLY") "Contract should mark sandbox full net preview ready only as preview."
Assert-True ($contract.statuses."accounting-pnl.v1" -eq "BLOCKED" -and $contract.statuses."accounting-attribution.v1" -eq "BLOCKED") "Accounting must remain blocked."
Assert-True ($contract.statuses."ledger-commit.v1" -eq "BLOCKED" -and $contract.statuses."production-readiness.v1" -eq "BLOCKED" -and $contract.statuses."trading-readiness.v1" -eq "BLOCKED") "Ledger/production/trading must remain blocked."

Assert-True ($boundary.no_trades -eq $true -and $boundary.no_r009_submission -eq $true -and $boundary.no_lmax_fix_api_call -eq $true) "Trading/R009/LMAX boundary crossed."
Assert-True ($boundary.no_polygon_massive_call -eq $true -and $boundary.no_broker_api_call -eq $true -and $boundary.no_market_data_fetch -eq $true -and $boundary.no_account_data_fetch -eq $true) "Provider/broker/market/account data boundary crossed."
Assert-True ($boundary.no_order_fill_report -eq $true -and $boundary.no_db_mutation -eq $true -and $boundary.no_ledger_commit -eq $true) "Order/fill/DB/ledger boundary crossed."
Assert-True ($boundary.no_accounting_pnl_ready -eq $true -and $boundary.no_production_pnl_ready -eq $true -and $boundary.no_production_live_ready -eq $true) "Accounting/production readiness boundary crossed."

Assert-True ($summary.Contains("Status: ACCOUNT_SPECIFIC_COMMISSION_CONFIRMED_R001")) "Summary missing status."
Assert-True ($summary.Contains("Sandbox full net PnL preview ready: True")) "Summary missing sandbox full net readiness."
Assert-True ($summary.Contains("Net USD: -76.576829")) "Summary missing net USD value."
Assert-True ($summary.Contains("Still blocked: accounting PnL attribution, ledger commit, production/live")) "Summary missing blocked items."
Assert-True ($summary.Contains("No trades")) "Summary missing no-activity confirmation."

Write-Host "ACCOUNT_SPECIFIC_COMMISSION_CONFIRMATION_R001_GATE_PASS"
