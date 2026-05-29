param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\risk-cost-model-r001"

function Read-Json([string]$Name) {
    Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir $Name) | ConvertFrom-Json
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$guidance = Read-Json "cost-guidance-inventory.json"
$coverage = Read-Json "instrument-cost-coverage-classification.json"
$doubleCount = Read-Json "actual-fill-gross-vs-cost-policy.json"
$preview = Read-Json "estimated-cost-preview-v0.json"
$net = Read-Json "net-pnl-readiness-decision.json"
$contract = Read-Json "contract-status-update.json"

Assert-True ($guidance.Guidance[0].Scope -eq "best-case major-only guidance") "5 USD/million guidance must remain major-only."
Assert-True ($guidance.Guidance[0].UsableForCoreAnubisExotics -eq $false) "5 USD/million guidance must not cover Core/Anubis exotics."

$blockedExotics = @($coverage.Rows | Where-Object { $_.CostCoverageStatus -eq "COST_COVERAGE_BLOCKED_EXOTIC_NO_COST_EVIDENCE" })
Assert-True ($blockedExotics.Count -ge 6) "Core/Anubis exotics must block cost coverage without explicit evidence."

Assert-True ($doubleCount.DoubleCountingPolicy.Contains("Do not add a separate spread/slippage estimate")) "Actual-fill gross PnL policy must prevent spread double-counting."

Assert-True ($preview.Classification -eq "ESTIMATED_COST_PREVIEW_COMPUTED_PARTIAL_MAJOR_ONLY") "Estimated preview must be partial major-only."
Assert-True ($preview.CostAdjustedPreview.FullNetPnlReady -eq $false) "Partial estimated preview must not be labelled full net PnL."

Assert-True ($net.FullNetPnlReady -eq $false) "Net PnL must remain blocked."
Assert-True ($contract.Statuses."accounting-attribution.v1" -eq "BLOCKED") "Accounting attribution must remain blocked."
Assert-True ($contract.Statuses."production-readiness.v1" -eq "BLOCKED") "Production readiness must remain blocked."
Assert-True ($contract.Statuses."marketdata-readiness.v1" -eq "WITH_WARNINGS") "MarketData must remain WITH_WARNINGS."

Write-Host "RISK_COST_MODEL_R001_FOCUSED_TESTS_PASS"
