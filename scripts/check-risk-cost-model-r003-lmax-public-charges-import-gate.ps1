param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\risk-cost-model-r003-lmax-public-charges-import"
$Required = @(
    "r002-intake-validation.json",
    "lmax-public-source-acquisition.json",
    "lmax-charges-pdf-validation.json",
    "lmax-fx-instrument-charge-coverage.json",
    "commission-model-scope-decision.json",
    "commission-computation-policy.json",
    "r013d-fill-cost-input-validation.json",
    "r013d-quote-currency-commission-preview.json",
    "r013d-cost-adjusted-sandbox-preview.json",
    "net-pnl-readiness-update.json",
    "account-currency-aggregation-gap.json",
    "accounting-production-boundary-decision.json",
    "contract-status-update.json",
    "blocker-map-update.json",
    "roadmap-decision.json",
    "readiness-impact.json",
    "boundary-safety-evidence.json",
    "summary.md"
)

function Read-Json([string]$Name) {
    Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir $Name) | ConvertFrom-Json
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

Assert-True (Test-Path -LiteralPath $ArtifactDir) "Risk/cost R003 artifact directory missing."
foreach ($name in $Required) {
    Assert-True (Test-Path -LiteralPath (Join-Path $ArtifactDir $name)) "Missing required R003 artifact: $name"
}

$intake = Read-Json "r002-intake-validation.json"
$source = Read-Json "lmax-public-source-acquisition.json"
$pdf = Read-Json "lmax-charges-pdf-validation.json"
$coverage = Read-Json "lmax-fx-instrument-charge-coverage.json"
$scope = Read-Json "commission-model-scope-decision.json"
$policy = Read-Json "commission-computation-policy.json"
$inputs = Read-Json "r013d-fill-cost-input-validation.json"
$commission = Read-Json "r013d-quote-currency-commission-preview.json"
$costAdjusted = Read-Json "r013d-cost-adjusted-sandbox-preview.json"
$net = Read-Json "net-pnl-readiness-update.json"
$account = Read-Json "account-currency-aggregation-gap.json"
$boundaryDecision = Read-Json "accounting-production-boundary-decision.json"
$contract = Read-Json "contract-status-update.json"
$blockers = Read-Json "blocker-map-update.json"
$roadmap = Read-Json "roadmap-decision.json"
$readiness = Read-Json "readiness-impact.json"
$boundary = Read-Json "boundary-safety-evidence.json"
$summary = Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir "summary.md")

$symbols = @("USDCAD", "USDCNH", "USDJPY", "USDMXN", "USDNOK", "NZDUSD", "USDSEK", "USDSGD", "USDZAR")

Assert-True ($intake.Classification -eq "R002_READY_FOR_LMAX_PUBLIC_CHARGES_IMPORT") "R002 intake not ready."
Assert-True ($intake.R002ClassificationConfirmed -eq $true -and $intake.R002FoundNoUsableExoticCostEvidence -eq $true) "R002 classification/evidence status not preserved."
Assert-True ($intake.R002DidNotComputeR013DCostPreview -eq $true -and $intake.R002DidNotMarkNetPnlReady -eq $true) "R002 cost/net boundary not preserved."
Assert-True ($intake.R002DidNotExecuteAnything -eq $true -and $intake.R002DidNotMutateDbOrLedger -eq $true) "R002 claimed execution or mutation."

Assert-True ($source.Classification -eq "LMAX_PUBLIC_SOURCE_ACQUIRED_OFFICIAL") "Official LMAX public source not acquired."
Assert-True ($source.SourceDomain -eq "www.lmax.com" -and $source.ContentType -eq "application/pdf") "Source domain/content type wrong."
Assert-True ($source.SourceHash -eq $source.SourceHashExpected) "Source hash mismatch."
Assert-True ($source.PageCount -eq 13) "PDF page count not preserved."
Assert-True ($source.NoCredentialsUsed -eq $true -and $source.NoTradingApiUsed -eq $true -and $source.NoLmaxFixApiCall -eq $true) "Source acquisition crossed credentials/trading API boundary."
Assert-True (Test-Path -LiteralPath (Join-Path $RepoRoot $source.LocalSourcePath)) "Saved source PDF missing."

Assert-True ($pdf.Classification -eq "LMAX_CHARGES_PDF_VALID_FX_COMMISSION_FOUND") "LMAX PDF validation failed."
Assert-True ($pdf.PdfParses -eq $true -and $pdf.ProductFxSectionExists -eq $true -and $pdf.FxCommissionStatementExists -eq $true) "PDF FX commission evidence missing."
Assert-True ($pdf.CommissionRateDecimal -eq 0.000025) "Commission rate must be 0.0025 percent / 0.000025 decimal."
Assert-True ($pdf.EvidencePublicGeneral -eq $true -and $pdf.EvidenceAccountSpecific -eq $false) "Public/account-specific scope wrong."

Assert-True ($coverage.Classification -eq "LMAX_CHARGE_COVERAGE_READY_ALL_R013D_SYMBOLS") "R013D symbol coverage not complete."
foreach ($symbol in $symbols) {
    Assert-True (@($coverage.Rows | Where-Object { $_.ExecutionSymbol -eq $symbol -and $_.Covered -eq $true -and $_.Classification -eq "LMAX_CHARGE_COVERAGE_READY" }).Count -eq 1) "$symbol coverage missing."
}

Assert-True ($scope.Classification -eq "COMMISSION_MODEL_SCOPE_READY_WITH_WARNINGS_NOT_ACCOUNT_SPECIFIC") "Commission model scope should be public sandbox with warnings."
Assert-True ($scope.PublicLmaxGlobalSandboxPreviewEvidence -eq $true -and $scope.AccountSpecific -eq $false) "Commission scope incorrectly account-specific."
Assert-True ($scope.AccountingPnl -eq $false -and $scope.ProductionPnl -eq $false -and $scope.LedgerCommit -eq $false) "Commission scope crossed accounting/production/ledger."

Assert-True ($policy.Classification -eq "COMMISSION_COMPUTATION_POLICY_READY") "Commission computation policy not ready."
Assert-True ($policy.ExcludeUnfilledUSDJPY50 -eq $true -and $policy.ExcludeZeroQuantityLines -eq $true) "Policy must exclude unfilled USDJPY and zero lines."
Assert-True ($policy.CommissionRate -eq 0.000025 -and $policy.OpenAndFlattenBothIncurCommission -eq $true) "Policy commission rate or open/flatten treatment wrong."
Assert-True ($policy.NoAccountCurrencyConversionWithoutPolicy -eq $true -and $policy.NoAccountingPnl -eq $true -and $policy.NoProductionPnl -eq $true) "Policy crossed account-currency/accounting/production boundary."

Assert-True ($inputs.Classification -eq "R013D_FILL_COST_INPUTS_READY") "R013D fill cost inputs not ready."
Assert-True ($inputs.ContractMultiplier -eq 10000 -and $inputs.PartialUSDJPYActualFillOnly -eq $true -and $inputs.USDJPYFilledQuantityUsed -eq "38.4" -and $inputs.USDJPYUnfilledQuantityExcluded -eq "50.0") "R013D fill inputs mishandled multiplier or USDJPY partial."
Assert-True ($inputs.ZeroQuantityLinesExcluded -eq $true) "R013D fill inputs included zero-quantity lines."

Assert-True ($commission.Classification -eq "R013D_QUOTE_CURRENCY_COMMISSION_PREVIEW_COMPUTED_WITH_WARNINGS") "R013D commission preview not computed with warnings."
Assert-True (@($commission.Rows).Count -eq 18) "Expected open and flatten commission rows for 9 symbols."
Assert-True ($commission.AccountCurrencyTotal -eq $null) "Commission preview must not aggregate to account currency."
foreach ($currency in @("CAD", "CNH", "JPY", "MXN", "NOK", "USD", "SEK", "SGD", "ZAR")) {
    Assert-True (@($commission.CommissionByQuoteCurrency | Where-Object { $_.QuoteCurrency -eq $currency }).Count -eq 1) "$currency commission bucket missing."
}

Assert-True ($costAdjusted.Classification -eq "R013D_COST_ADJUSTED_SANDBOX_PREVIEW_COMPUTED_WITH_WARNINGS") "Cost-adjusted preview not computed with warnings."
Assert-True ($costAdjusted.AccountCurrencyAggregation -eq $false -and $costAdjusted.FullNetPnl -eq $false) "Cost-adjusted preview incorrectly claims account-currency/full net PnL."
Assert-True ($costAdjusted.UnfilledUSDJPY50Included -eq $false -and $costAdjusted.SpreadEstimateIncluded -eq $false -and $costAdjusted.SwapFinancingIncluded -eq $false) "Cost-adjusted preview included forbidden components."
Assert-True (@($costAdjusted.CostAdjustedRows).Count -eq 9) "Expected 9 cost-adjusted rows."

Assert-True ($net.Classification -eq "NET_PNL_BLOCKED_ACCOUNT_CURRENCY_AGGREGATION") "Net PnL should be blocked by account-currency aggregation."
Assert-True ($net.QuoteCurrencyCostAdjustedPreviewExists -eq $true -and $net.FullNetPnlReady -eq $false) "Net PnL readiness wrong."
Assert-True ($net.AccountingPnlRemainsBlocked -eq $true -and $net.ProductionPnlRemainsBlocked -eq $true -and $net.LedgerCommitRemainsBlocked -eq $true) "Net update crossed accounting/production/ledger."

Assert-True ($account.Classification -eq "ACCOUNT_CURRENCY_AGGREGATION_GAP_CONFIRMED") "Account-currency gap not confirmed."
Assert-True ($account.AccountCurrencyBound -eq $false -and $account.FxConversionPolicyApproved -eq $false) "Account currency or FX conversion was incorrectly approved."

Assert-True ($boundaryDecision.Classification -eq "ACCOUNTING_PRODUCTION_BOUNDARY_PRESERVED_WITH_WARNINGS") "Accounting/production boundary decision wrong."
Assert-True ($boundaryDecision.NoAccountingPnl -eq $true -and $boundaryDecision.NoProductionPnl -eq $true -and $boundaryDecision.NoLedgerCommit -eq $true -and $boundaryDecision.NoAccountCurrencyDefined -eq $true) "Accounting/production/ledger/account boundary crossed."

Assert-True ($contract.Statuses."lmax-public-charges-evidence.v1" -eq "YES") "LMAX public evidence contract should be YES."
Assert-True ($contract.Statuses."r013d-commission-preview.v1" -eq "YES_WITH_WARNINGS_QUOTE_CURRENCY") "R013D commission preview contract wrong."
Assert-True ($contract.Statuses."r013d-cost-adjusted-preview.v1" -eq "YES_WITH_WARNINGS_QUOTE_CURRENCY") "R013D cost-adjusted preview contract wrong."
Assert-True ($contract.Statuses."net-pnl-preview.v1" -eq "BLOCKED_ACCOUNT_CURRENCY_AGGREGATION" -and $contract.Statuses."account-currency-aggregation.v1" -eq "BLOCKED") "Net/account-currency contracts wrong."
Assert-True ($contract.Statuses."accounting-attribution.v1" -eq "BLOCKED" -and $contract.Statuses."production-readiness.v1" -eq "BLOCKED") "Accounting/production contracts not blocked."

Assert-True ($blockers.Classification -eq "BLOCKER_MAP_UPDATED_WITH_WARNINGS") "Blocker map not updated."
Assert-True (@($blockers.RemainingBlockers | Where-Object { $_ -eq "account-currency aggregation" }).Count -eq 1) "Account-currency blocker missing."
Assert-True ($roadmap.Decision -eq "NEXT_ACCOUNT_CURRENCY_AGGREGATION_R001") "Roadmap should point to account-currency aggregation."

Assert-True ($readiness.PublicLmaxCommissionEvidenceImported -eq $true -and $readiness.R013DCommissionPreviewComputed -eq $true -and $readiness.R013DCostAdjustedPreviewComputed -eq $true) "Readiness impact missing imported/computed evidence."
Assert-True ($readiness.FullNetPnlRemainsBlocked -eq $true -and $readiness.AccountCurrencyAggregationRemainsBlocked -eq $true) "Readiness impact incorrectly unblocked net/account-currency."
Assert-True ($readiness.AccountingPnlChanged -eq $false -and $readiness.ProductionReadinessChanged -eq $false) "Accounting/production readiness changed."

Assert-True ($boundary.NoNewR009Submission -eq $true -and $boundary.NoNewLmaxTradingFixApiCall -eq $true -and $boundary.NoNewPolygonMassiveCall -eq $true -and $boundary.NoMarketDataFetch -eq $true) "Forbidden execution/provider/market-data call claimed."
Assert-True ($boundary.NoNewOrderFillReport -eq $true -and $boundary.NoDbMutation -eq $true -and $boundary.NoLedgerCommit -eq $true) "Order/fill/DB/ledger claimed."
Assert-True ($boundary.NoProductionLive -eq $true -and $boundary.NoCoreExecution -eq $true -and $boundary.NoManager -eq $true -and $boundary.NoAnubis -eq $true -and $boundary.NoCuda -eq $true -and $boundary.NoCoreNetting -eq $true) "Production/Core boundary crossed."
Assert-True ($boundary.NoAccountCurrencyAggregation -eq $true -and $boundary.NoAccountingPnl -eq $true -and $boundary.NoProductionPnl -eq $true -and $boundary.NoInventedFeesSpreadsCommissions -eq $true) "Account/PnL/invented-cost boundary crossed."

Assert-True ($summary.Contains("RISK_COST_MODEL_R003_WITH_WARNINGS_PUBLIC_COST_EVIDENCE_IMPORTED_ACCOUNT_CURRENCY_BLOCKED")) "Summary missing final classification."
Assert-True ($summary.Contains("Commission found: 0.0025%")) "Summary missing commission policy."
Assert-True ($summary.Contains("Full net PnL ready: no")) "Summary must keep full net PnL blocked."
Assert-True ($summary.Contains("NEXT_ACCOUNT_CURRENCY_AGGREGATION_R001")) "Summary missing next package."
Assert-True ($summary.Contains("No R009 submission")) "Summary missing no-execution confirmation."

Write-Host "RISK_COST_MODEL_R003_LMAX_PUBLIC_CHARGES_IMPORT_GATE_PASS"
