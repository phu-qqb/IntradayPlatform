param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\risk-cost-model-r002-exotic-cost-evidence"

function Read-Json([string]$Name) {
    Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir $Name) | ConvertFrom-Json
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$exotic = Read-Json "exotic-cost-evidence-validation-by-symbol.json"
$scope = Read-Json "cost-model-scope-decision.json"
$commission = Read-Json "commission-fee-computation-policy.json"
$spread = Read-Json "spread-slippage-evidence-policy.json"
$preview = Read-Json "r013d-estimated-cost-preview.json"
$net = Read-Json "net-pnl-readiness-update.json"
$boundary = Read-Json "accounting-production-boundary-decision.json"

Assert-True ($exotic.Classification -eq "EXOTIC_COST_EVIDENCE_MISSING_ALL_REQUIRED") "Exotics must block when explicit evidence is absent."
Assert-True (@($exotic.Rows | Where-Object { $_.SufficientlyClearForComputation -eq $true }).Count -eq 0) "No exotic symbol should be computationally covered."

Assert-True ($scope.CanComputeFullSandboxNetPnlPreview -eq $false) "Full sandbox net PnL cannot be ready with missing exotic cost evidence."
Assert-True ($preview.Classification -eq "R013D_ESTIMATED_COST_PREVIEW_NOT_COMPUTED_EXOTIC_COST_GAPS") "R013D preview must not be computed without exotic evidence."
Assert-True (@($preview.IncludedFills).Count -eq 0) "Partial evidence must not be labelled full R013D cost preview."

Assert-True ($commission.ExcludeUnfilledUSDJPY50 -eq $true) "Unfilled USDJPY 50.0 must be excluded from any future cost preview."
Assert-True ($commission.ExcludeZeroQuantityLines -eq $true) "Zero-quantity lines must be excluded."
Assert-True ($spread.DoNotAddSpreadSlippageEstimateByDefault -eq $true) "Actual-fill gross PnL must not double-count spread."

Assert-True ($net.FullSandboxNetPnlReady -eq $false) "Net PnL must remain blocked."
Assert-True ($boundary.NoAccountingPnl -eq $true -and $boundary.NoProductionPnl -eq $true -and $boundary.NoLedgerCommit -eq $true) "Accounting/production/ledger must remain blocked."

Write-Host "RISK_COST_MODEL_R002_EXOTIC_COST_EVIDENCE_FOCUSED_TESTS_PASS"
