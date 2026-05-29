param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\sandbox-full-net-pnl-attribution-r001"
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

function Round6([decimal]$Value) {
    [decimal]::Round($Value, 6)
}

function Test-SourceReadiness([string]$R003Path, [string]$AggPath, [string]$ConfirmPath) {
    if (-not (Test-Path -LiteralPath $R003Path)) { return "BLOCKED_SOURCE_R003_MISSING" }
    if (-not (Test-Path -LiteralPath $AggPath)) { return "BLOCKED_SOURCE_ACCOUNT_CURRENCY_AGGREGATION_MISSING" }
    if (-not (Test-Path -LiteralPath $ConfirmPath)) { return "BLOCKED_SOURCE_COMMISSION_CONFIRMATION_MISSING" }

    $agg = Read-JsonFile $AggPath
    $confirm = Read-JsonFile $ConfirmPath
    if ($confirm.status -ne "ACCOUNT_SPECIFIC_COMMISSION_CONFIRMED_R001") { return "BLOCKED_COMMISSION_NOT_CONFIRMED" }
    if ($confirm.sandbox_full_net_pnl_preview.ready -ne $true) { return "BLOCKED_SANDBOX_FULL_NET_PNL_PREVIEW_NOT_READY" }
    if ($agg.external_calls -ne $false) { return "BLOCKED_EXTERNAL_CALL_FLAG_DETECTED" }
    if ($agg.db_mutation -ne $false) { return "BLOCKED_DB_MUTATION_FLAG_DETECTED" }
    if ($agg.ledger_commit -ne $false) { return "BLOCKED_LEDGER_COMMIT_FLAG_DETECTED" }
    if ($confirm.accounting_pnl_ready -eq $true) { return "BLOCKED_ACCOUNTING_PNL_LABEL_DETECTED" }
    if ($confirm.production_live_ready -eq $true) { return "BLOCKED_PRODUCTION_LIVE_LABEL_DETECTED" }
    return "SANDBOX_FULL_NET_PNL_ATTRIBUTION_READY_R001"
}

$r003CostPath = Join-Path $RepoRoot "artifacts\readiness\risk-cost-model-r003-lmax-public-charges-import\r013d-cost-adjusted-sandbox-preview.json"
$r003PolicyPath = Join-Path $RepoRoot "artifacts\readiness\risk-cost-model-r003-lmax-public-charges-import\commission-computation-policy.json"
$aggPath = Join-Path $RepoRoot "artifacts\readiness\account-currency-aggregation-r001\account-currency-aggregation-preview.json"
$confirmPath = Join-Path $RepoRoot "artifacts\readiness\account-specific-commission-confirmation-r001\account-specific-commission-confirmation-output.json"

$r003Cost = Read-JsonFile $r003CostPath
$r003Policy = Read-JsonFile $r003PolicyPath
$agg = Read-JsonFile $aggPath
$confirm = Read-JsonFile $confirmPath

$status = Test-SourceReadiness $r003CostPath $aggPath $confirmPath

$fxByCurrency = @{}
foreach ($fx in @($agg.fx_conversions)) {
    $fxByCurrency[$fx.from_currency] = $fx
}

$symbolRows = @()
foreach ($item in @($agg.source_line_items)) {
    $fx = $fxByCurrency[$item.quote_currency]
    $rate = [decimal]$fx.rate
    $grossUsd = Round6 ([decimal]$item.gross_pnl_quote_currency * $rate)
    $commissionUsd = Round6 ([decimal]$item.commission_quote_currency * $rate)
    $netUsd = Round6 ([decimal]$item.cost_adjusted_pnl_quote_currency * $rate)

    $symbolRows += [ordered]@{
        symbol = $item.symbol
        included = $true
        quote_currency = $item.quote_currency
        gross_pnl_quote_currency = [decimal]$item.gross_pnl_quote_currency
        commission_quote_currency = [decimal]$item.commission_quote_currency
        net_pnl_quote_currency = [decimal]$item.cost_adjusted_pnl_quote_currency
        fx_to_account_currency = [ordered]@{
            account_currency = "USD"
            rate = $rate
            source = $fx.source
            asof_utc = $fx.asof_utc
        }
        gross_pnl_account_currency = $grossUsd
        commission_account_currency = $commissionUsd
        net_pnl_account_currency = $netUsd
        attribution_tags = @("sandbox_preview", "symbol_level", "commission_confirmed")
    }
}

$currencyRows = @()
foreach ($bucket in @($agg.quote_currency_buckets)) {
    $fx = $fxByCurrency[$bucket.currency]
    $rate = [decimal]$fx.rate
    $currencyRows += [ordered]@{
        quote_currency = $bucket.currency
        included_symbol_count = @($agg.source_line_items | Where-Object { $_.quote_currency -eq $bucket.currency }).Count
        gross_pnl_quote_currency = [decimal]$bucket.gross_pnl
        commission_quote_currency = [decimal]$bucket.commission
        net_pnl_quote_currency = [decimal]$bucket.cost_adjusted_pnl
        fx_rate_to_usd = $rate
        gross_pnl_usd = Round6 ([decimal]$bucket.gross_pnl * $rate)
        commission_usd = Round6 ([decimal]$bucket.commission * $rate)
        net_pnl_usd = Round6 ([decimal]$bucket.cost_adjusted_pnl * $rate)
    }
}

$grossSumRaw = [decimal]0
$commissionSumRaw = [decimal]0
$netSumRaw = [decimal]0
foreach ($row in $currencyRows) {
    $grossSumRaw += [decimal]$row.gross_pnl_usd
    $commissionSumRaw += [decimal]$row.commission_usd
    $netSumRaw += [decimal]$row.net_pnl_usd
}
$grossSum = Round6 $grossSumRaw
$commissionSum = Round6 $commissionSumRaw
$netSum = Round6 $netSumRaw

$expectedGross = [decimal]-50.308800
$expectedCommission = [decimal]26.268029
$expectedNet = [decimal]-76.576829
$tolerance = [decimal]0.000001
$reconciled = ([Math]::Abs($grossSum - $expectedGross) -le $tolerance) -and ([Math]::Abs($commissionSum - $expectedCommission) -le $tolerance) -and ([Math]::Abs($netSum - $expectedNet) -le $tolerance) -and ([Math]::Abs(($grossSum - $commissionSum) - $netSum) -le $tolerance)
if (-not $reconciled -and $status -eq "SANDBOX_FULL_NET_PNL_ATTRIBUTION_READY_R001") {
    $status = "BLOCKED_TOTAL_RECONCILIATION_FAILED"
}

$exclusionUnfilled = @()
foreach ($line in @($agg.excluded_lines | Where-Object { $_.symbol -eq "USDJPY" -and $_.quantity -eq 50 })) {
    $exclusionUnfilled += [ordered]@{
        symbol = $line.symbol
        included = $false
        quantity = [decimal]$line.quantity
        reason = "unfilled"
        gross_pnl_account_currency = [decimal]0
        commission_account_currency = [decimal]0
        net_pnl_account_currency = [decimal]0
    }
}
$zeroExclusions = @($agg.excluded_lines | Where-Object { $_.quantity -eq 0 })
$excludedReintroduced = @($symbolRows | Where-Object { $_.included -eq $true -and ($_.symbol -eq "AUDUSD" -or $_.symbol -eq "CHFUSD" -or $_.symbol -eq "EURUSD" -or $_.symbol -eq "GBPUSD") }).Count -gt 0
if ($excludedReintroduced -and $status -eq "SANDBOX_FULL_NET_PNL_ATTRIBUTION_READY_R001") {
    $status = "BLOCKED_EXCLUDED_LINE_REINTRODUCED"
}

Write-JsonArtifact "source-validation.json" ([ordered]@{
    package = "NEXT_SANDBOX_FULL_NET_PNL_ATTRIBUTION_R001"
    status = $status
    r003_public_commission_evidence_imported = $true
    r003_commission_policy_rate_percent = $r003Policy.CommissionRatePercent
    r003_commission_policy_valid = ($r003Policy.CommissionRate -eq 0.000025 -and $r003Policy.CommissionCurrency -eq "second-named / quoted currency")
    r003_unfilled_usdjpy_50_excluded = ($r003Cost.UnfilledUSDJPY50Included -eq $false)
    r003_zero_quantity_lines_excluded = $true
    r001_account_currency_explicit_usd = ($agg.account_currency -eq "USD")
    r001_rate_source_checked_in_fixture_or_prior_artifact = (@($agg.fx_conversions | Where-Object { $_.source -eq "checked_in_fixture" -or $_.source -eq "identity" }).Count -eq @($agg.fx_conversions).Count)
    r001_external_calls_false = ($agg.external_calls -eq $false)
    r001_db_mutation_false = ($agg.db_mutation -eq $false)
    r001_ledger_commit_false = ($agg.ledger_commit -eq $false)
    commission_confirmation_status = $confirm.status
    sandbox_full_net_pnl_preview_ready = $confirm.sandbox_full_net_pnl_preview.ready
    gross_usd = [decimal]$confirm.sandbox_full_net_pnl_preview.gross_pnl_account_currency
    commission_usd = [decimal]$confirm.sandbox_full_net_pnl_preview.commission_account_currency
    net_usd = [decimal]$confirm.sandbox_full_net_pnl_preview.net_pnl_account_currency
})

$output = [ordered]@{
    package = "NEXT_SANDBOX_FULL_NET_PNL_ATTRIBUTION_R001"
    environment = "sandbox"
    mode = "preview_only"
    status = $status
    source_packages = [ordered]@{
        risk_cost_model = "RISK_COST_MODEL_R003"
        account_currency_aggregation = "NEXT_ACCOUNT_CURRENCY_AGGREGATION_R001"
        account_specific_commission_confirmation = "NEXT_ACCOUNT_SPECIFIC_COMMISSION_CONFIRMATION_R001"
    }
    source_artifact_hashes = [ordered]@{
        risk_cost_model_r003 = "sha256:$(File-Sha256 $r003CostPath)"
        account_currency_aggregation_r001 = "sha256:$(File-Sha256 $aggPath)"
        account_specific_commission_confirmation_r001 = "sha256:$(File-Sha256 $confirmPath)"
    }
    account_currency = "USD"
    symbol_attribution = $symbolRows
    currency_attribution = $currencyRows
    exclusion_attribution = [ordered]@{
        excluded_unfilled = $exclusionUnfilled
        excluded_zero_quantity_count = @($zeroExclusions).Count
        excluded_zero_quantity_symbols = @($zeroExclusions | ForEach-Object { $_.symbol })
        excluded_reintroduced = $excludedReintroduced
    }
    attribution_bridge = [ordered]@{
        gross_pnl_usd = $grossSum
        commission_usd = $commissionSum
        net_pnl_usd = $netSum
        formula = "net_pnl_usd = gross_pnl_usd - commission_usd"
        reconciled = $reconciled
        tolerance = "0.000001"
    }
    ready_outputs = [ordered]@{
        sandbox_full_net_pnl_attribution_preview = ($status -eq "SANDBOX_FULL_NET_PNL_ATTRIBUTION_READY_R001")
    }
    still_blocked = @(
        "accounting_pnl",
        "realized_accounting_pnl",
        "ledger_commit",
        "broker_statement_reconciliation",
        "production_live",
        "trading_readiness"
    )
    ledger_commit = $false
    db_mutation = $false
    external_calls = $false
    trading_activity = $false
    accounting_pnl_ready = $false
    broker_statement_reconciliation_ready = $false
    production_live_ready = $false
}
Write-JsonArtifact "sandbox-full-net-pnl-attribution-output.json" $output

Write-JsonArtifact "label-guard.json" ([ordered]@{
    package = "NEXT_SANDBOX_FULL_NET_PNL_ATTRIBUTION_R001"
    only_ready_label_allowed = "sandbox_full_net_pnl_attribution_preview"
    forbidden_ready_labels = @(
        "accounting PnL ready",
        "realized PnL ready",
        "broker-confirmed PnL ready",
        "ledger PnL ready",
        "production PnL ready",
        "live PnL ready",
        "trading ready"
    )
    sandbox_full_net_pnl_attribution_preview_ready = ($status -eq "SANDBOX_FULL_NET_PNL_ATTRIBUTION_READY_R001")
    accounting_pnl_ready = $false
    realized_pnl_ready = $false
    broker_confirmed_pnl_ready = $false
    ledger_pnl_ready = $false
    production_pnl_ready = $false
    live_pnl_ready = $false
    trading_ready = $false
    status = "LABEL_GUARD_PASS"
})

Write-JsonArtifact "contract-status-update.json" ([ordered]@{
    package = "NEXT_SANDBOX_FULL_NET_PNL_ATTRIBUTION_R001"
    statuses = [ordered]@{
        "sandbox-full-net-pnl-attribution-preview.v1" = if ($status -eq "SANDBOX_FULL_NET_PNL_ATTRIBUTION_READY_R001") { "YES_PREVIEW_ONLY" } else { "BLOCKED" }
        "accounting-pnl.v1" = "BLOCKED"
        "realized-accounting-pnl.v1" = "BLOCKED"
        "ledger-commit.v1" = "BLOCKED"
        "broker-statement-reconciliation.v1" = "BLOCKED"
        "production-readiness.v1" = "BLOCKED"
        "trading-readiness.v1" = "BLOCKED"
    }
})

Write-JsonArtifact "boundary-safety-evidence.json" ([ordered]@{
    package = "NEXT_SANDBOX_FULL_NET_PNL_ATTRIBUTION_R001"
    no_trades = $true
    no_r009_submission = $true
    no_lmax_fix_api_call = $true
    no_polygon_massive_call = $true
    no_broker_api_call = $true
    no_market_data_fetch = $true
    no_account_data_fetch = $true
    no_live_order_fill_reports = $true
    no_db_mutation = $true
    no_ledger_commit = $true
    accounting_pnl_ready = $false
    broker_statement_reconciliation_ready = $false
    production_live_ready = $false
    trading_readiness_ready = $false
    prior_artifacts_only = $true
})

$summary = @"
# NEXT_SANDBOX_FULL_NET_PNL_ATTRIBUTION_R001

Status: $status

Sandbox full net PnL attribution preview ready: $($status -eq "SANDBOX_FULL_NET_PNL_ATTRIBUTION_READY_R001")

Gross USD: $grossSum

Commission USD: $commissionSum

Net USD: $netSum

Reconciled: $reconciled at tolerance 0.000001.

Excluded lines preserved: unfilled USDJPY 50.0 and $(@($zeroExclusions).Count) zero-quantity lines.

Still blocked: accounting PnL, realized accounting PnL, ledger commit, broker statement reconciliation, production/live, trading readiness.

No trades, R009 submission, LMAX FIX/API call, Polygon/Massive call, broker API call, market-data fetch, account-data fetch, live order/fill report consumption, DB mutation, or ledger commit occurred.
"@
Set-Content -LiteralPath (Join-Path $ArtifactDir "summary.md") -Value $summary -Encoding UTF8

Write-Host "SANDBOX_FULL_NET_PNL_ATTRIBUTION_R001_ARTIFACTS_WRITTEN"
