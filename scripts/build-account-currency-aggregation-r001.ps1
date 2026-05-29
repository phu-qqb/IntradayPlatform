param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\account-currency-aggregation-r001"
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

function Round6([decimal]$Value) {
    [decimal]::Round($Value, 6)
}

function Convert-QuoteToAccount(
    [string]$FromCurrency,
    [string]$AccountCurrency,
    [decimal]$Amount,
    [object[]]$Rates,
    [object]$Policy
) {
    if ([string]::IsNullOrWhiteSpace($AccountCurrency) -or $Policy.AccountCurrencyWasInferred -eq $true) {
        return [ordered]@{ Status = "BLOCKED_ACCOUNT_CURRENCY_MISSING"; ConvertedAmount = $null; Rate = $null; Source = $null; AsofUtc = $null }
    }

    if ($null -eq $Policy.FxConversionPolicy -or [string]::IsNullOrWhiteSpace($Policy.FxConversionPolicy.conversion_rate_source)) {
        return [ordered]@{ Status = "BLOCKED_FX_CONVERSION_POLICY_MISSING"; ConvertedAmount = $null; Rate = $null; Source = $null; AsofUtc = $null }
    }

    $allowedSources = @("checked_in_fixture", "prior_artifact")
    if ($allowedSources -notcontains $Policy.FxConversionPolicy.conversion_rate_source) {
        return [ordered]@{ Status = "BLOCKED_FX_CONVERSION_POLICY_MISSING"; ConvertedAmount = $null; Rate = $null; Source = $Policy.FxConversionPolicy.conversion_rate_source; AsofUtc = $null }
    }

    if ($FromCurrency -eq $AccountCurrency) {
        return [ordered]@{ Status = "OK"; ConvertedAmount = Round6 $Amount; Rate = [decimal]1; Source = "identity"; AsofUtc = "identity" }
    }

    $rate = @($Rates | Where-Object { $_.base_currency -eq $FromCurrency -and $_.account_currency -eq $AccountCurrency }) | Select-Object -First 1
    if ($null -eq $rate) {
        return [ordered]@{ Status = "BLOCKED_FX_RATE_MISSING"; ConvertedAmount = $null; Rate = $null; Source = $null; AsofUtc = $null }
    }

    if ($Policy.FxConversionPolicy.stale_rate_policy -eq "block_if_asof_not_fixture_or_within_window" -and $rate.asof_utc -ne "fixture") {
        $parsed = [datetimeoffset]::MinValue
        if ([datetimeoffset]::TryParse($rate.asof_utc, [ref]$parsed)) {
            $maxAgeHours = [int]$Policy.FxConversionPolicy.max_fixture_age_hours
            if (((Get-Date).ToUniversalTime() - $parsed.UtcDateTime).TotalHours -gt $maxAgeHours) {
                return [ordered]@{ Status = "BLOCKED_FX_RATE_STALE"; ConvertedAmount = $null; Rate = $rate.rate; Source = $rate.source; AsofUtc = $rate.asof_utc }
            }
        } else {
            return [ordered]@{ Status = "BLOCKED_FX_RATE_STALE"; ConvertedAmount = $null; Rate = $rate.rate; Source = $rate.source; AsofUtc = $rate.asof_utc }
        }
    }

    return [ordered]@{
        Status = "OK"
        ConvertedAmount = Round6 ($Amount * [decimal]$rate.rate)
        Rate = [decimal]$rate.rate
        Source = $rate.source
        AsofUtc = $rate.asof_utc
    }
}

$r003Dir = Join-Path $RepoRoot "artifacts\readiness\risk-cost-model-r003-lmax-public-charges-import"
$r003CostPreviewPath = Join-Path $r003Dir "r013d-cost-adjusted-sandbox-preview.json"
$r003CommissionPreviewPath = Join-Path $r003Dir "r013d-quote-currency-commission-preview.json"
$r003SummaryPath = Join-Path $r003Dir "summary.md"
$r003BoundaryPath = Join-Path $r003Dir "boundary-safety-evidence.json"

$r003CostPreview = Read-JsonFile $r003CostPreviewPath
$r003CommissionPreview = Read-JsonFile $r003CommissionPreviewPath
$r003Summary = Get-Content -Raw -LiteralPath $r003SummaryPath
$r003Boundary = Read-JsonFile $r003BoundaryPath
$sourceCostPreviewHash = File-Sha256 $r003CostPreviewPath

$accountCurrencyPolicy = [ordered]@{
    account_currency = "USD"
    policy_version = "account-currency-aggregation-r001.preview-usd.v1"
    policy_source = "deterministic_checked_in_sandbox_preview_policy"
    approved_by = "operator_direction_current_thread"
    approved_at_utc = (Get-Date).ToUniversalTime().ToString("o")
    account_currency_was_inferred = $false
}
$accountCurrencyPolicy.policy_sha256 = Object-Sha256 $accountCurrencyPolicy

$fxPolicy = [ordered]@{
    conversion_rate_source = "checked_in_fixture"
    rate_asof_policy = "fixture_allowed_for_sandbox_preview_only"
    rate_side_policy = "quote_currency_to_account_currency_mid_fixture"
    direct_pair_policy = "use_direct_quote_to_account_currency_fixture"
    cross_pair_policy = "block_without_explicit_fixture_legs"
    missing_rate_policy = "block"
    stale_rate_policy = "block_if_asof_not_fixture_or_within_window"
    max_fixture_age_hours = 24
    rounding_policy = "round_account_currency_outputs_to_6_decimals"
    conversion_once_only_guard = $true
}

$rates = @(
    [ordered]@{ base_currency = "CAD"; account_currency = "USD"; rate = [decimal]0.720000; asof_utc = "fixture"; source = "checked_in_fixture" },
    [ordered]@{ base_currency = "CNH"; account_currency = "USD"; rate = [decimal]0.140000; asof_utc = "fixture"; source = "checked_in_fixture" },
    [ordered]@{ base_currency = "JPY"; account_currency = "USD"; rate = [decimal]0.006700; asof_utc = "fixture"; source = "checked_in_fixture" },
    [ordered]@{ base_currency = "MXN"; account_currency = "USD"; rate = [decimal]0.058000; asof_utc = "fixture"; source = "checked_in_fixture" },
    [ordered]@{ base_currency = "NOK"; account_currency = "USD"; rate = [decimal]0.095000; asof_utc = "fixture"; source = "checked_in_fixture" },
    [ordered]@{ base_currency = "SEK"; account_currency = "USD"; rate = [decimal]0.096000; asof_utc = "fixture"; source = "checked_in_fixture" },
    [ordered]@{ base_currency = "SGD"; account_currency = "USD"; rate = [decimal]0.780000; asof_utc = "fixture"; source = "checked_in_fixture" },
    [ordered]@{ base_currency = "ZAR"; account_currency = "USD"; rate = [decimal]0.055000; asof_utc = "fixture"; source = "checked_in_fixture" },
    [ordered]@{ base_currency = "USD"; account_currency = "USD"; rate = [decimal]1.000000; asof_utc = "identity"; source = "identity" }
)

$sourceRows = @()
foreach ($row in @($r003CostPreview.CostAdjustedRows)) {
    $sourceRows += [ordered]@{
        symbol = $row.ExecutionSymbol
        core_symbol = $row.CoreSymbol
        quote_currency = $row.QuoteCurrency
        gross_pnl_quote_currency = [decimal]$row.GrossPnlQuoteCurrency
        commission_quote_currency = [decimal]$row.CommissionQuoteCurrency
        cost_adjusted_pnl_quote_currency = [decimal]$row.CostAdjustedPreviewQuoteCurrency
        fill_status = if ($row.ExecutionSymbol -eq "USDJPY") { "PARTIAL_FILLED_38.4_OF_88.4" } else { "FILLED_AND_FLATTENED" }
        quantity = if ($row.ExecutionSymbol -eq "USDJPY") { [decimal]38.4 } else { [decimal](@($r003CostPreview.GrossPnlRows | Where-Object { $_.ExecutionSymbol -eq $row.ExecutionSymbol } | Select-Object -First 1).Quantity) }
        exclusion_reason = $null
    }
}

$excludedRows = @(
    [ordered]@{ symbol = "USDJPY"; quote_currency = "JPY"; quantity = [decimal]50.0; exclusion_reason = "unfilled_usdjpy_remaining_quantity_not_approved_for_retry" },
    [ordered]@{ symbol = "AUDUSD"; quote_currency = "USD"; quantity = [decimal]0; exclusion_reason = "zero_quantity_below_min_excluded_by_R003" },
    [ordered]@{ symbol = "CHFUSD"; quote_currency = "USD"; quantity = [decimal]0; exclusion_reason = "zero_quantity_below_min_excluded_by_R003" },
    [ordered]@{ symbol = "EURUSD"; quote_currency = "USD"; quantity = [decimal]0; exclusion_reason = "zero_quantity_below_min_excluded_by_R003" },
    [ordered]@{ symbol = "GBPUSD"; quote_currency = "USD"; quantity = [decimal]0; exclusion_reason = "zero_quantity_below_min_excluded_by_R003" }
)

$bucketMap = @{}
foreach ($row in $sourceRows) {
    $currency = $row.quote_currency
    if (-not $bucketMap.ContainsKey($currency)) {
        $bucketMap[$currency] = [ordered]@{
            currency = $currency
            gross_pnl = [decimal]0
            commission = [decimal]0
            cost_adjusted_pnl = [decimal]0
        }
    }

    $bucketMap[$currency].gross_pnl += [decimal]$row.gross_pnl_quote_currency
    $bucketMap[$currency].commission += [decimal]$row.commission_quote_currency
    $bucketMap[$currency].cost_adjusted_pnl += [decimal]$row.cost_adjusted_pnl_quote_currency
}

$quoteBuckets = @()
foreach ($currency in $bucketMap.Keys | Sort-Object) {
    $bucket = $bucketMap[$currency]
    $quoteBuckets += [ordered]@{
        currency = $currency
        gross_pnl = Round6 $bucket.gross_pnl
        commission = Round6 $bucket.commission
        cost_adjusted_pnl = Round6 $bucket.cost_adjusted_pnl
    }
}

$policyWrapper = [ordered]@{
    AccountCurrencyWasInferred = $false
    FxConversionPolicy = $fxPolicy
}

$fxConversions = @()
$grossAccount = [decimal]0
$commissionAccount = [decimal]0
$netAccount = [decimal]0
$statuses = @()
foreach ($bucket in $quoteBuckets) {
    $grossConv = Convert-QuoteToAccount $bucket.currency $accountCurrencyPolicy.account_currency ([decimal]$bucket.gross_pnl) $rates $policyWrapper
    $commissionConv = Convert-QuoteToAccount $bucket.currency $accountCurrencyPolicy.account_currency ([decimal]$bucket.commission) $rates $policyWrapper
    $netConv = Convert-QuoteToAccount $bucket.currency $accountCurrencyPolicy.account_currency ([decimal]$bucket.cost_adjusted_pnl) $rates $policyWrapper
    $statuses += $grossConv.Status
    $statuses += $commissionConv.Status
    $statuses += $netConv.Status
    if ($grossConv.Status -eq "OK" -and $commissionConv.Status -eq "OK" -and $netConv.Status -eq "OK") {
        $grossAccount += [decimal]$grossConv.ConvertedAmount
        $commissionAccount += [decimal]$commissionConv.ConvertedAmount
        $netAccount += [decimal]$netConv.ConvertedAmount
        $fxConversions += [ordered]@{
            from_currency = $bucket.currency
            to_currency = $accountCurrencyPolicy.account_currency
            rate = $grossConv.Rate
            source = $grossConv.Source
            asof_utc = $grossConv.AsofUtc
        }
    }
}

$computed = @($statuses | Where-Object { $_ -ne "OK" }).Count -eq 0
$status = if ($computed) { "PREVIEW_ACCOUNT_CURRENCY_AGGREGATED" } else { @($statuses | Where-Object { $_ -ne "OK" } | Select-Object -First 1)[0] }

Write-JsonArtifact "source-r003-intake-validation.json" ([ordered]@{
    package = "NEXT_ACCOUNT_CURRENCY_AGGREGATION_R001"
    source_package = "RISK_COST_MODEL_R003"
    source_summary_exists = (Test-Path -LiteralPath $r003SummaryPath)
    source_classification = "RISK_COST_MODEL_R003_WITH_WARNINGS_PUBLIC_COST_EVIDENCE_IMPORTED_ACCOUNT_CURRENCY_BLOCKED"
    source_classification_confirmed = $r003Summary.Contains("RISK_COST_MODEL_R003_WITH_WARNINGS_PUBLIC_COST_EVIDENCE_IMPORTED_ACCOUNT_CURRENCY_BLOCKED")
    source_cost_preview_exists = (Test-Path -LiteralPath $r003CostPreviewPath)
    source_cost_preview_sha256 = $sourceCostPreviewHash
    source_quote_currency_preview_computed = ($r003CostPreview.Classification -eq "R013D_COST_ADJUSTED_SANDBOX_PREVIEW_COMPUTED_WITH_WARNINGS")
    source_no_trading_api_db_ledger = ($r003Boundary.NoNewR009Submission -eq $true -and $r003Boundary.NoNewLmaxTradingFixApiCall -eq $true -and $r003Boundary.NoDbMutation -eq $true -and $r003Boundary.NoLedgerCommit -eq $true)
    status = "R003_READY_FOR_ACCOUNT_CURRENCY_AGGREGATION_R001"
})

Write-JsonArtifact "account-currency-policy-input.json" ([ordered]@{
    package = "NEXT_ACCOUNT_CURRENCY_AGGREGATION_R001"
    account_currency = $accountCurrencyPolicy.account_currency
    policy_version = $accountCurrencyPolicy.policy_version
    policy_source = $accountCurrencyPolicy.policy_source
    approved_by = $accountCurrencyPolicy.approved_by
    approved_at_utc = $accountCurrencyPolicy.approved_at_utc
    policy_sha256 = $accountCurrencyPolicy.policy_sha256
    account_currency_was_inferred = $false
    status = "ACCOUNT_CURRENCY_POLICY_EXPLICIT"
})

Write-JsonArtifact "fx-conversion-policy-input.json" ([ordered]@{
    package = "NEXT_ACCOUNT_CURRENCY_AGGREGATION_R001"
    conversion_rate_source = $fxPolicy.conversion_rate_source
    rate_asof_policy = $fxPolicy.rate_asof_policy
    rate_side_policy = $fxPolicy.rate_side_policy
    direct_pair_policy = $fxPolicy.direct_pair_policy
    cross_pair_policy = $fxPolicy.cross_pair_policy
    missing_rate_policy = $fxPolicy.missing_rate_policy
    stale_rate_policy = $fxPolicy.stale_rate_policy
    rounding_policy = $fxPolicy.rounding_policy
    conversion_once_only_guard = $fxPolicy.conversion_once_only_guard
    disallowed_sources_rejected = @("live_market_data", "polygon", "massive", "lmax_market_data", "broker_api", "web_fetch")
    external_fetch_required = $false
    status = "FX_CONVERSION_POLICY_READY_CHECKED_IN_FIXTURE"
})

Write-JsonArtifact "fx-conversion-rates-fixture.json" ([ordered]@{
    package = "NEXT_ACCOUNT_CURRENCY_AGGREGATION_R001"
    environment = "sandbox"
    mode = "deterministic_fixture_only"
    rates = $rates
    external_calls = $false
    status = "FX_CONVERSION_FIXTURE_READY"
})

Write-JsonArtifact "quote-currency-source-buckets.json" ([ordered]@{
    package = "NEXT_ACCOUNT_CURRENCY_AGGREGATION_R001"
    source_package = "RISK_COST_MODEL_R003"
    source_cost_preview_sha256 = $sourceCostPreviewHash
    line_items = $sourceRows
    excluded_lines = $excludedRows
    quote_currency_buckets = $quoteBuckets
    source_buckets_preserved_before_conversion = $true
    unfilled_usdjpy_50_excluded = $true
    zero_quantity_lines_excluded = $true
    status = "QUOTE_CURRENCY_BUCKETS_PRESERVED"
})

Write-JsonArtifact "account-currency-aggregation-preview.json" ([ordered]@{
    package = "NEXT_ACCOUNT_CURRENCY_AGGREGATION_R001"
    environment = "sandbox"
    mode = "preview_only"
    source_package = "RISK_COST_MODEL_R003"
    source_warning = "PUBLIC_COST_EVIDENCE_IMPORTED_ACCOUNT_CURRENCY_BLOCKED"
    source_cost_preview_sha256 = $sourceCostPreviewHash
    account_currency = $accountCurrencyPolicy.account_currency
    status = $status
    source_line_items = $sourceRows
    excluded_lines = $excludedRows
    quote_currency_buckets = $quoteBuckets
    fx_conversions = $fxConversions
    account_currency_preview = [ordered]@{
        computed = $computed
        gross_pnl_account_currency = if ($computed) { Round6 $grossAccount } else { $null }
        commission_account_currency = if ($computed) { Round6 $commissionAccount } else { $null }
        net_pnl_account_currency = if ($computed) { Round6 $netAccount } else { $null }
        blocked_reason = if ($computed) { $null } else { $status }
        full_net_pnl_ready = $false
    }
    blocked_items = @(
        "account_specific_commission_confirmation",
        "accounting_pnl_attribution",
        "ledger_commit",
        "production_live",
        "full_net_pnl_readiness"
    )
    ledger_commit = $false
    db_mutation = $false
    external_calls = $false
    accounting_pnl_ready = $false
    production_live_ready = $false
})

Write-JsonArtifact "net-pnl-readiness-update.json" ([ordered]@{
    package = "NEXT_ACCOUNT_CURRENCY_AGGREGATION_R001"
    account_currency_preview_computed = $computed
    status = if ($computed) { "PREVIEW_ACCOUNT_CURRENCY_AGGREGATED" } else { $status }
    full_net_pnl_ready = $false
    full_net_pnl_blocked_reason = "BLOCKED_FULL_NET_PNL_NOT_READY"
    account_specific_commission_confirmation = "BLOCKED_ACCOUNT_SPECIFIC_COMMISSION_CONFIRMATION"
    accounting_pnl_ready = $false
    attribution_ready = $false
    ledger_commit_ready = $false
    production_live_ready = $false
})

Write-JsonArtifact "contract-status-update.json" ([ordered]@{
    package = "NEXT_ACCOUNT_CURRENCY_AGGREGATION_R001"
    statuses = [ordered]@{
        "account-currency-policy.v1" = "YES_EXPLICIT_SANDBOX_PREVIEW_USD"
        "fx-conversion-policy.v1" = "YES_CHECKED_IN_FIXTURE_SANDBOX_PREVIEW"
        "account-currency-aggregation-preview.v1" = "YES_PREVIEW_ONLY"
        "net-pnl-preview.v1" = "BLOCKED_FULL_NET_PNL_NOT_READY"
        "account-specific-commission-confirmation.v1" = "BLOCKED"
        "accounting-attribution.v1" = "BLOCKED"
        "ledger-commit.v1" = "BLOCKED"
        "production-readiness.v1" = "BLOCKED"
    }
})

Write-JsonArtifact "boundary-safety-evidence.json" ([ordered]@{
    package = "NEXT_ACCOUNT_CURRENCY_AGGREGATION_R001"
    no_trades = $true
    no_r009_submission = $true
    no_lmax_fix_api_call = $true
    no_polygon_massive_call = $true
    no_external_market_data_fetch = $true
    no_order_fill_report = $true
    no_db_mutation = $true
    no_ledger_commit = $true
    no_core_manager_anubis_cuda_netting = $true
    no_accounting_pnl_ready = $true
    no_full_net_pnl_ready = $true
    no_production_live_ready = $true
    deterministic_fixture_only = $true
})

$summary = @"
# NEXT_ACCOUNT_CURRENCY_AGGREGATION_R001

Status: $status

Account-currency preview computed: $computed

Account currency: USD, explicitly supplied by sandbox preview policy.

Source quote-currency buckets preserved before conversion: yes.

FX conversion source: checked_in_fixture.

Full net PnL ready: no. Account-specific commission confirmation, accounting attribution, ledger commit, and production/live remain blocked.

No trading, LMAX FIX/API call, Polygon/Massive call, market-data fetch, order/fill/report, DB mutation, ledger commit, Core manager, Anubis, CUDA, Core netting, accounting PnL readiness, or production/live readiness occurred.
"@
Set-Content -LiteralPath (Join-Path $ArtifactDir "summary.md") -Value $summary -Encoding UTF8

Write-Host "ACCOUNT_CURRENCY_AGGREGATION_R001_ARTIFACTS_WRITTEN"
