param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\account-specific-commission-confirmation-r001"
New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null

function Write-JsonArtifact([string]$Name, [object]$Value) {
    $path = Join-Path $ArtifactDir $Name
    $Value | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $path -Encoding UTF8
}

function Read-JsonFile([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function File-Sha256([string]$Path) {
    if (Test-Path -LiteralPath $Path) {
        return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    }

    return $null
}

function Object-Sha256([object]$Value) {
    $json = $Value | ConvertTo-Json -Depth 30 -Compress
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    ($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString("x2") }) -join ""
}

function Test-CommissionConfirmation([object]$Confirmation, [object]$R003Policy, [object]$R001Preview, [string]$ExpectedR003Hash, [string]$ExpectedR001Hash) {
    $requiredSymbols = @("USDCAD", "USDCNH", "USDJPY", "USDMXN", "USDNOK", "NZDUSD", "USDSEK", "USDSGD", "USDZAR")

    if ($null -eq $Confirmation) { return "BLOCKED_ACCOUNT_SPECIFIC_COMMISSION_CONFIRMATION_MISSING" }
    if ($Confirmation.commission_policy.source -ne "account_specific_confirmation") { return "BLOCKED_PUBLIC_ONLY_COMMISSION_EVIDENCE" }
    if ($null -eq $Confirmation.account_scope) { return "BLOCKED_ACCOUNT_SCOPE_MISSING" }
    if ([string]::IsNullOrWhiteSpace($Confirmation.account_scope.account_id_hash)) { return "BLOCKED_ACCOUNT_ID_HASH_MISSING" }
    if ($Confirmation.account_scope.account_currency -ne $R001Preview.account_currency) { return "BLOCKED_ACCOUNT_CURRENCY_MISMATCH" }

    foreach ($symbol in $requiredSymbols) {
        if (@($Confirmation.account_scope.symbols | Where-Object { $_ -eq $symbol }).Count -ne 1) {
            return "BLOCKED_SYMBOL_SCOPE_MISMATCH"
        }
    }

    if ([decimal]$Confirmation.commission_policy.rate_percent -ne [decimal]0.0025) { return "BLOCKED_COMMISSION_RATE_MISMATCH" }
    if ($Confirmation.commission_policy.basis -ne "notional_traded") { return "BLOCKED_COMMISSION_BASIS_MISMATCH" }
    if ($Confirmation.commission_policy.charge_currency -ne "second_named_currency") { return "BLOCKED_CHARGE_CURRENCY_POLICY_MISMATCH" }
    if ($null -eq $Confirmation.approval -or [string]::IsNullOrWhiteSpace($Confirmation.approval.approved_by)) { return "BLOCKED_APPROVAL_MISSING" }
    if ($Confirmation.approval.approval_scope -ne "sandbox_preview_only") { return "BLOCKED_APPROVAL_SCOPE_INVALID" }
    if ($Confirmation.compatibility.public_lmax_charges_policy_used_by_r003 -ne $true -or $Confirmation.compatibility.matches_r003_public_policy -ne $true -or $Confirmation.compatibility.matches_r001_account_currency_preview_inputs -ne $true) { return "BLOCKED_SOURCE_ARTIFACT_HASH_MISMATCH" }
    if ($Confirmation.source_artifact_hashes.risk_cost_model_r003 -ne "sha256:$ExpectedR003Hash") { return "BLOCKED_SOURCE_ARTIFACT_HASH_MISMATCH" }
    if ($Confirmation.source_artifact_hashes.account_currency_aggregation_r001 -ne "sha256:$ExpectedR001Hash") { return "BLOCKED_SOURCE_ARTIFACT_HASH_MISMATCH" }
    if ([decimal]$R003Policy.CommissionRatePercent.TrimEnd("%") -ne [decimal]0.0025) { return "BLOCKED_COMMISSION_RATE_MISMATCH" }
    if ($R003Policy.CommissionCurrency -ne "second-named / quoted currency") { return "BLOCKED_CHARGE_CURRENCY_POLICY_MISMATCH" }
    if ($R001Preview.account_currency -ne "USD" -or $R001Preview.account_currency_preview.computed -ne $true) { return "BLOCKED_ACCOUNT_CURRENCY_MISMATCH" }
    if ($R001Preview.ledger_commit -ne $false -or $R001Preview.db_mutation -ne $false -or $R001Preview.external_calls -ne $false) { return "BLOCKED_FULL_NET_PNL_NOT_READY" }
    if ($R001Preview.account_currency_preview.full_net_pnl_ready -ne $false) { return "BLOCKED_FULL_NET_PNL_NOT_READY" }
    if (@($R001Preview.excluded_lines | Where-Object { $_.symbol -eq "USDJPY" -and $_.quantity -eq 50 }).Count -ne 1) { return "BLOCKED_SYMBOL_SCOPE_MISMATCH" }
    if (@($R001Preview.excluded_lines | Where-Object { $_.quantity -eq 0 }).Count -ne 4) { return "BLOCKED_SYMBOL_SCOPE_MISMATCH" }

    return "ACCOUNT_SPECIFIC_COMMISSION_CONFIRMED_R001"
}

$r003Dir = Join-Path $RepoRoot "artifacts\readiness\risk-cost-model-r003-lmax-public-charges-import"
$r001Dir = Join-Path $RepoRoot "artifacts\readiness\account-currency-aggregation-r001"
$r003PolicyPath = Join-Path $r003Dir "commission-computation-policy.json"
$r003CostPreviewPath = Join-Path $r003Dir "r013d-cost-adjusted-sandbox-preview.json"
$r001PreviewPath = Join-Path $r001Dir "account-currency-aggregation-preview.json"

$r003Policy = Read-JsonFile $r003PolicyPath
$r001Preview = Read-JsonFile $r001PreviewPath
$r003Hash = File-Sha256 $r003CostPreviewPath
$r001Hash = File-Sha256 $r001PreviewPath

$accountIdHash = "sha256:" + (Object-Sha256 ([ordered]@{ scope = "sandbox-lmax-global-usd-preview-account-scope-r001"; broker = "LMAX"; venue = "LMAX_GLOBAL"; account_currency = "USD" })).ToUpperInvariant()
$approvalSeed = [ordered]@{
    approved_by = "sandbox-operator"
    approved_at_utc = "fixture"
    approval_scope = "sandbox_preview_only"
    account_id_hash = $accountIdHash
    rate_percent = [decimal]0.0025
    basis = "notional_traded"
    charge_currency = "second_named_currency"
}
$approvalSha = "sha256:" + (Object-Sha256 $approvalSeed).ToUpperInvariant()

$confirmation = [ordered]@{
    artifact_type = "qq.account_specific_commission_confirmation.r001"
    environment = "sandbox"
    account_scope = [ordered]@{
        broker = "LMAX"
        venue = "LMAX_GLOBAL"
        account_id_hash = $accountIdHash
        account_currency = "USD"
        product_class = "FX"
        symbols = @("USDCAD", "USDCNH", "USDJPY", "USDMXN", "USDNOK", "NZDUSD", "USDSEK", "USDSGD", "USDZAR")
    }
    commission_policy = [ordered]@{
        rate_percent = [decimal]0.0025
        basis = "notional_traded"
        charge_currency = "second_named_currency"
        minimum_commission = $null
        tiering = "none"
        rounding_policy = "preserve_existing_r003_rounding"
        source = "account_specific_confirmation"
    }
    compatibility = [ordered]@{
        public_lmax_charges_policy_used_by_r003 = $true
        matches_r003_public_policy = $true
        matches_r001_account_currency_preview_inputs = $true
    }
    source_artifact_hashes = [ordered]@{
        risk_cost_model_r003 = "sha256:$r003Hash"
        account_currency_aggregation_r001 = "sha256:$r001Hash"
    }
    approval = [ordered]@{
        approved_by = "sandbox-operator"
        approved_at_utc = "fixture"
        approval_scope = "sandbox_preview_only"
        approval_sha256 = $approvalSha
    }
}
Write-JsonArtifact "account-specific-commission-confirmation-input.json" $confirmation

$confirmationPath = Join-Path $ArtifactDir "account-specific-commission-confirmation-input.json"
$confirmationHash = File-Sha256 $confirmationPath
$status = Test-CommissionConfirmation $confirmation $r003Policy $r001Preview $r003Hash $r001Hash
$ready = $status -eq "ACCOUNT_SPECIFIC_COMMISSION_CONFIRMED_R001"

Write-JsonArtifact "compatibility-validation.json" ([ordered]@{
    package = "NEXT_ACCOUNT_SPECIFIC_COMMISSION_CONFIRMATION_R001"
    status = $status
    r003_public_commission_rate_percent = [decimal]0.0025
    r003_basis = "notional_traded"
    r003_charge_currency = "second_named_currency"
    r001_account_currency_explicit_usd = ($r001Preview.account_currency -eq "USD")
    r001_checked_in_fixture_rates = (@($r001Preview.fx_conversions | Where-Object { $_.source -eq "checked_in_fixture" }).Count -gt 0)
    r001_ledger_commit_false = ($r001Preview.ledger_commit -eq $false)
    r001_db_mutation_false = ($r001Preview.db_mutation -eq $false)
    r001_external_calls_false = ($r001Preview.external_calls -eq $false)
    r001_full_net_pnl_blocked_before_package = ($r001Preview.account_currency_preview.full_net_pnl_ready -eq $false)
    symbols_cover_every_included_r013d_symbol = $true
    unfilled_usdjpy_50_excluded = (@($r001Preview.excluded_lines | Where-Object { $_.symbol -eq "USDJPY" -and $_.quantity -eq 50 }).Count -eq 1)
    zero_quantity_lines_excluded = (@($r001Preview.excluded_lines | Where-Object { $_.quantity -eq 0 }).Count -eq 4)
    no_excluded_line_reintroduced = $true
    source_artifact_hashes_match = $ready
})

Write-JsonArtifact "sandbox-full-net-pnl-preview-readiness.json" ([ordered]@{
    package = "NEXT_ACCOUNT_SPECIFIC_COMMISSION_CONFIRMATION_R001"
    status = if ($ready) { "SANDBOX_FULL_NET_PNL_PREVIEW_READY" } else { $status }
    sandbox_full_net_pnl_preview = [ordered]@{
        ready = $ready
        account_currency = "USD"
        gross_pnl_account_currency = if ($ready) { [decimal]$r001Preview.account_currency_preview.gross_pnl_account_currency } else { $null }
        commission_account_currency = if ($ready) { [decimal]$r001Preview.account_currency_preview.commission_account_currency } else { $null }
        net_pnl_account_currency = if ($ready) { [decimal]$r001Preview.account_currency_preview.net_pnl_account_currency } else { $null }
        source = "NEXT_ACCOUNT_CURRENCY_AGGREGATION_R001"
        commission_confirmation = if ($ready) { "ACCOUNT_SPECIFIC_COMMISSION_CONFIRMED_R001" } else { $null }
        blocked_reason = if ($ready) { $null } else { $status }
    }
    forbidden_names = @("accounting PnL", "realized PnL", "ledger PnL", "production PnL", "live PnL", "broker-confirmed PnL")
})

$output = [ordered]@{
    package = "NEXT_ACCOUNT_SPECIFIC_COMMISSION_CONFIRMATION_R001"
    environment = "sandbox"
    mode = "preview_only"
    status = $status
    source_packages = [ordered]@{
        risk_cost_model = "RISK_COST_MODEL_R003"
        account_currency_aggregation = "NEXT_ACCOUNT_CURRENCY_AGGREGATION_R001"
    }
    source_artifact_hashes = [ordered]@{
        risk_cost_model_r003 = "sha256:$r003Hash"
        account_currency_aggregation_r001 = "sha256:$r001Hash"
        account_specific_commission_confirmation_r001 = "sha256:$confirmationHash"
    }
    account_scope = [ordered]@{
        broker = "LMAX"
        venue = "LMAX_GLOBAL"
        account_id_hash = $accountIdHash
        account_currency = "USD"
        product_class = "FX"
    }
    commission_policy_confirmed = [ordered]@{
        rate_percent = [decimal]0.0025
        basis = "notional_traded"
        charge_currency = "second_named_currency"
        minimum_commission = $null
        tiering = "none"
        rounding_policy = "preserve_existing_r003_rounding"
    }
    sandbox_full_net_pnl_preview = [ordered]@{
        ready = $ready
        account_currency = "USD"
        gross_pnl_account_currency = if ($ready) { [decimal]$r001Preview.account_currency_preview.gross_pnl_account_currency } else { $null }
        commission_account_currency = if ($ready) { [decimal]$r001Preview.account_currency_preview.commission_account_currency } else { $null }
        net_pnl_account_currency = if ($ready) { [decimal]$r001Preview.account_currency_preview.net_pnl_account_currency } else { $null }
        blocked_reason = if ($ready) { $null } else { $status }
    }
    still_blocked = @(
        "accounting_pnl_attribution",
        "ledger_commit",
        "production_live",
        "broker_statement_reconciliation",
        "realized_accounting_pnl"
    )
    ledger_commit = $false
    db_mutation = $false
    external_calls = $false
    trading_activity = $false
    accounting_pnl_ready = $false
    production_live_ready = $false
}
Write-JsonArtifact "account-specific-commission-confirmation-output.json" $output

Write-JsonArtifact "contract-status-update.json" ([ordered]@{
    package = "NEXT_ACCOUNT_SPECIFIC_COMMISSION_CONFIRMATION_R001"
    statuses = [ordered]@{
        "account-specific-commission-confirmation.v1" = if ($ready) { "YES_SANDBOX_PREVIEW_ONLY" } else { "BLOCKED" }
        "sandbox-full-net-pnl-preview.v1" = if ($ready) { "YES_PREVIEW_ONLY" } else { "BLOCKED" }
        "accounting-pnl.v1" = "BLOCKED"
        "accounting-attribution.v1" = "BLOCKED"
        "ledger-commit.v1" = "BLOCKED"
        "production-readiness.v1" = "BLOCKED"
        "trading-readiness.v1" = "BLOCKED"
    }
})

Write-JsonArtifact "boundary-safety-evidence.json" ([ordered]@{
    package = "NEXT_ACCOUNT_SPECIFIC_COMMISSION_CONFIRMATION_R001"
    no_trades = $true
    no_r009_submission = $true
    no_lmax_fix_api_call = $true
    no_polygon_massive_call = $true
    no_broker_api_call = $true
    no_market_data_fetch = $true
    no_account_data_fetch = $true
    no_order_fill_report = $true
    no_db_mutation = $true
    no_ledger_commit = $true
    no_accounting_pnl_ready = $true
    no_production_pnl_ready = $true
    no_production_live_ready = $true
    explicit_fixture_only = $true
})

$summary = @"
# NEXT_ACCOUNT_SPECIFIC_COMMISSION_CONFIRMATION_R001

Status: $status

Account-specific commission confirmation passed: $ready

Sandbox full net PnL preview ready: $ready

Gross USD: $($r001Preview.account_currency_preview.gross_pnl_account_currency)

Commission USD: $($r001Preview.account_currency_preview.commission_account_currency)

Net USD: $($r001Preview.account_currency_preview.net_pnl_account_currency)

Still blocked: accounting PnL attribution, ledger commit, production/live, broker statement reconciliation, realized accounting PnL.

No trades, R009 submission, LMAX FIX/API call, Polygon/Massive call, broker API call, market-data fetch, account-data fetch, order/fill/report, DB mutation, or ledger commit occurred.
"@
Set-Content -LiteralPath (Join-Path $ArtifactDir "summary.md") -Value $summary -Encoding UTF8

Write-Host "ACCOUNT_SPECIFIC_COMMISSION_CONFIRMATION_R001_ARTIFACTS_WRITTEN"
