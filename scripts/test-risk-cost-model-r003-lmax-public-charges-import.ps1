param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\risk-cost-model-r003-lmax-public-charges-import"

function Read-Json([string]$Name) {
    Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir $Name) | ConvertFrom-Json
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$pdf = Read-Json "lmax-charges-pdf-validation.json"
$coverage = Read-Json "lmax-fx-instrument-charge-coverage.json"
$policy = Read-Json "commission-computation-policy.json"
$commission = Read-Json "r013d-quote-currency-commission-preview.json"
$costAdjusted = Read-Json "r013d-cost-adjusted-sandbox-preview.json"
$net = Read-Json "net-pnl-readiness-update.json"
$scope = Read-Json "commission-model-scope-decision.json"
$boundary = Read-Json "accounting-production-boundary-decision.json"

Assert-True ($pdf.CommissionRateDecimal -eq 0.000025) "FX commission rate must be 0.0025 percent."
Assert-True ($pdf.FxCommissionStatement.Contains("second-named currency")) "Commission currency must be second-named currency."

foreach ($pair in @("USDCNH", "USDMXN", "USDNOK", "USDSEK", "USDSGD", "USDZAR")) {
    Assert-True (@($coverage.Rows | Where-Object { $_.ExecutionSymbol -eq $pair -and $_.Covered -eq $true }).Count -eq 1) "$pair must be covered by public LMAX charges evidence."
}

Assert-True ($policy.ExcludeUnfilledUSDJPY50 -eq $true) "Unfilled USDJPY 50.0 must be excluded."
Assert-True ($policy.ExcludeZeroQuantityLines -eq $true) "Zero-quantity lines must be excluded."

Assert-True (@($commission.Rows | Where-Object { $_.ExecutionSymbol -eq "USDJPY" }).Count -eq 2) "USDJPY should have open and flatten commission rows only."
Assert-True (@($commission.Rows | Where-Object { $_.ExecutionSymbol -eq "USDJPY" -and $_.Quantity -eq 38.4 }).Count -eq 2) "USDJPY commission must use actual filled 38.4 only."

Assert-True ($costAdjusted.FullNetPnl -eq $false) "Quote-currency cost preview must not be labelled full net PnL."
Assert-True ($net.Classification -eq "NET_PNL_BLOCKED_ACCOUNT_CURRENCY_AGGREGATION") "Full net PnL must remain blocked without account-currency aggregation."
Assert-True ($scope.AccountSpecific -eq $false) "Public LMAX evidence must not be treated as account-specific."
Assert-True ($boundary.NoAccountingPnl -eq $true -and $boundary.NoProductionPnl -eq $true) "Accounting/production must remain blocked."

Write-Host "RISK_COST_MODEL_R003_LMAX_PUBLIC_CHARGES_IMPORT_FOCUSED_TESTS_PASS"
