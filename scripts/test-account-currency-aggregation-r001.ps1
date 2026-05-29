param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\account-currency-aggregation-r001"

function Read-Json([string]$Name) {
    Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir $Name) | ConvertFrom-Json
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Test-Convert(
    [string]$FromCurrency,
    [string]$AccountCurrency,
    [decimal]$Amount,
    [object[]]$Rates,
    [object]$Policy,
    [bool]$Inferred = $false
) {
    if ([string]::IsNullOrWhiteSpace($AccountCurrency) -or $Inferred) {
        return [ordered]@{ Status = "BLOCKED_ACCOUNT_CURRENCY_MISSING"; Converted = $null }
    }

    if ($null -eq $Policy -or [string]::IsNullOrWhiteSpace($Policy.conversion_rate_source)) {
        return [ordered]@{ Status = "BLOCKED_FX_CONVERSION_POLICY_MISSING"; Converted = $null }
    }

    if (@("checked_in_fixture", "prior_artifact") -notcontains $Policy.conversion_rate_source) {
        return [ordered]@{ Status = "BLOCKED_FX_CONVERSION_POLICY_MISSING"; Converted = $null }
    }

    if ($FromCurrency -eq $AccountCurrency) {
        return [ordered]@{ Status = "OK"; Converted = $Amount; Rate = [decimal]1 }
    }

    $rate = @($Rates | Where-Object { $_.base_currency -eq $FromCurrency -and $_.account_currency -eq $AccountCurrency }) | Select-Object -First 1
    if ($null -eq $rate) {
        return [ordered]@{ Status = "BLOCKED_FX_RATE_MISSING"; Converted = $null }
    }

    if ($Policy.stale_rate_policy -eq "block_if_asof_not_fixture_or_within_window" -and $rate.asof_utc -ne "fixture") {
        return [ordered]@{ Status = "BLOCKED_FX_RATE_STALE"; Converted = $null }
    }

    return [ordered]@{ Status = "OK"; Converted = [decimal]$rate.rate * $Amount; Rate = [decimal]$rate.rate }
}

$source = Read-Json "quote-currency-source-buckets.json"
$policy = Read-Json "account-currency-policy-input.json"
$fxPolicy = Read-Json "fx-conversion-policy-input.json"
$fixture = Read-Json "fx-conversion-rates-fixture.json"
$preview = Read-Json "account-currency-aggregation-preview.json"
$net = Read-Json "net-pnl-readiness-update.json"
$contract = Read-Json "contract-status-update.json"
$boundary = Read-Json "boundary-safety-evidence.json"

Assert-True ($source.source_buckets_preserved_before_conversion -eq $true) "Quote-currency buckets must be preserved before conversion."
Assert-True (@($source.line_items).Count -eq 9) "Expected 9 included source line items."
Assert-True (@($source.excluded_lines | Where-Object { $_.symbol -eq "USDJPY" -and $_.quantity -eq 50.0 }).Count -eq 1) "Unfilled USDJPY 50.0 must remain excluded."
Assert-True (@($source.excluded_lines | Where-Object { $_.quantity -eq 0 }).Count -eq 4) "Zero-quantity lines must remain excluded."

Assert-True ($policy.account_currency -eq "USD" -and $policy.account_currency_was_inferred -eq $false) "Account currency must be explicit and not inferred."

$basePolicy = [pscustomobject]@{
    conversion_rate_source = "checked_in_fixture"
    stale_rate_policy = "block_if_asof_not_fixture_or_within_window"
}
$rates = @($fixture.rates)

Assert-True ((Test-Convert "JPY" "" 1 $rates $basePolicy).Status -eq "BLOCKED_ACCOUNT_CURRENCY_MISSING") "Missing account currency must block."
Assert-True ((Test-Convert "JPY" "USD" 1 $rates $basePolicy $true).Status -eq "BLOCKED_ACCOUNT_CURRENCY_MISSING") "Inferred account currency must block."
Assert-True ((Test-Convert "JPY" "USD" 1 $rates $null).Status -eq "BLOCKED_FX_CONVERSION_POLICY_MISSING") "Missing FX policy must block."
Assert-True ((Test-Convert "AUD" "USD" 1 $rates $basePolicy).Status -eq "BLOCKED_FX_RATE_MISSING") "Missing FX rate must block."

$staleRates = @([pscustomobject]@{ base_currency = "JPY"; account_currency = "USD"; rate = [decimal]0.0067; asof_utc = "2020-01-01T00:00:00Z"; source = "checked_in_fixture" })
Assert-True ((Test-Convert "JPY" "USD" 1 $staleRates $basePolicy).Status -eq "BLOCKED_FX_RATE_STALE") "Stale FX rate must block when policy requires it."

Assert-True ((Test-Convert "USD" "USD" 2.5 $rates $basePolicy).Converted -eq 2.5) "Identity conversion must work."
Assert-True ((Test-Convert "JPY" "USD" 100 $rates $basePolicy).Converted -eq 0.6700) "Deterministic fixture conversion must work."

foreach ($sourceName in @("live_market_data", "polygon", "massive", "lmax_market_data", "broker_api", "web_fetch")) {
    $badPolicy = [pscustomobject]@{ conversion_rate_source = $sourceName; stale_rate_policy = "block_if_asof_not_fixture_or_within_window" }
    Assert-True ((Test-Convert "JPY" "USD" 1 $rates $badPolicy).Status -eq "BLOCKED_FX_CONVERSION_POLICY_MISSING") "$sourceName must be rejected."
}

Assert-True ($preview.account_currency_preview.computed -eq $true) "Account-currency preview should compute with explicit USD fixture policy."
Assert-True ($preview.account_currency_preview.full_net_pnl_ready -eq $false) "Preview must not be full net PnL ready."
Assert-True (@($preview.blocked_items | Where-Object { $_ -eq "account_specific_commission_confirmation" }).Count -eq 1) "Account-specific commission confirmation must remain blocked."
Assert-True ($net.full_net_pnl_ready -eq $false -and $net.accounting_pnl_ready -eq $false -and $net.ledger_commit_ready -eq $false -and $net.production_live_ready -eq $false) "Net/accounting/ledger/production must remain blocked."
Assert-True ($contract.statuses."production-readiness.v1" -eq "BLOCKED") "Production/live readiness must remain blocked."
Assert-True ($boundary.no_lmax_fix_api_call -eq $true -and $boundary.no_polygon_massive_call -eq $true -and $boundary.no_external_market_data_fetch -eq $true) "No LMAX/Polygon/Massive/API/market-data call path may be introduced."
Assert-True ($boundary.no_db_mutation -eq $true -and $boundary.no_ledger_commit -eq $true) "DB mutation and ledger commit must remain false."

Write-Host "ACCOUNT_CURRENCY_AGGREGATION_R001_TESTS_PASS"
