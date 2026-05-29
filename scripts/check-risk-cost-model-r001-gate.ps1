param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\risk-cost-model-r001"
$Required = @(
    "checkpoint-intake-validation.json",
    "sandbox-fill-universe-inventory.json",
    "cost-guidance-inventory.json",
    "actual-fill-gross-vs-cost-policy.json",
    "instrument-cost-coverage-classification.json",
    "commission-fee-model-decision.json",
    "spread-slippage-policy-decision.json",
    "financing-rollover-swap-policy-decision.json",
    "sandbox-cost-preview-feasibility.json",
    "estimated-cost-preview-v0.json",
    "net-pnl-readiness-decision.json",
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

Assert-True (Test-Path -LiteralPath $ArtifactDir) "Risk/cost artifact directory missing."
foreach ($name in $Required) {
    Assert-True (Test-Path -LiteralPath (Join-Path $ArtifactDir $name)) "Missing required risk/cost artifact: $name"
}

$checkpoint = Read-Json "checkpoint-intake-validation.json"
$fillUniverse = Read-Json "sandbox-fill-universe-inventory.json"
$guidance = Read-Json "cost-guidance-inventory.json"
$doubleCount = Read-Json "actual-fill-gross-vs-cost-policy.json"
$coverage = Read-Json "instrument-cost-coverage-classification.json"
$commission = Read-Json "commission-fee-model-decision.json"
$spread = Read-Json "spread-slippage-policy-decision.json"
$financing = Read-Json "financing-rollover-swap-policy-decision.json"
$feasibility = Read-Json "sandbox-cost-preview-feasibility.json"
$preview = Read-Json "estimated-cost-preview-v0.json"
$net = Read-Json "net-pnl-readiness-decision.json"
$boundaryDecision = Read-Json "accounting-production-boundary-decision.json"
$contract = Read-Json "contract-status-update.json"
$blockers = Read-Json "blocker-map-update.json"
$roadmap = Read-Json "roadmap-decision.json"
$readiness = Read-Json "readiness-impact.json"
$boundary = Read-Json "boundary-safety-evidence.json"
$summary = Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir "summary.md")

Assert-True ($checkpoint.Classification -eq "CHECKPOINT_READY_FOR_RISK_COST_MODEL") "Checkpoint intake is not ready."
Assert-True ($checkpoint.ProductState -eq "SandboxProgrammeAcceptedWithGrossPnlV0Ready") "Product state anchor not preserved."
Assert-True ($checkpoint.NetPnlRemainsBlocked -eq $true -and $checkpoint.AccountingPnlRemainsBlocked -eq $true -and $checkpoint.ProductionLiveRemainsBlocked -eq $true) "Checkpoint incorrectly unblocked PnL/production."
Assert-True ($checkpoint.MarketDataPlatform -eq "WITH_WARNINGS") "MarketData status was promoted."

Assert-True ($fillUniverse.Classification -eq "SANDBOX_FILL_UNIVERSE_READY_WITH_WARNINGS") "Sandbox fill universe inventory not ready with warnings."
Assert-True (@($fillUniverse.Lifecycles).Count -eq 2) "Expected historical and Core/Anubis lifecycles."
Assert-True (@($fillUniverse.Lifecycles | Where-Object { $_.Source -eq "HistoricalPmsIntent" -and $_.OpenFillCount -eq 3 -and $_.FlattenFillCount -eq 3 }).Count -eq 1) "Historical lifecycle inventory incomplete."
Assert-True (@($fillUniverse.Lifecycles | Where-Object { $_.Source -eq "CoreAnubis" -and $_.OpenFillCount -eq 9 -and $_.FlattenFillCount -eq 9 }).Count -eq 1) "Core/Anubis lifecycle inventory incomplete."

Assert-True ($guidance.Classification -eq "COST_GUIDANCE_INVENTORY_READY_WITH_WARNINGS") "Cost guidance inventory not ready with warnings."
Assert-True ($guidance.Guidance[0].ValueUsdPerMillion -eq 5) "5 USD/million guidance missing."
Assert-True ($guidance.Guidance[0].Scope -eq "best-case major-only guidance") "5 USD/million scope is wrong."
Assert-True ($guidance.Guidance[0].UsableForCoreAnubisExotics -eq $false) "5 USD/million was applied to Core/Anubis exotics."
Assert-True ($guidance.ExplicitLmaxCommissionEvidence -eq $false -and $guidance.ExplicitSpreadEvidence -eq $false -and $guidance.ExplicitFeeEvidence -eq $false) "Unevidenced cost components claimed."

Assert-True ($doubleCount.Classification -eq "DOUBLE_COUNTING_POLICY_READY") "Double-counting policy not ready."
Assert-True ($doubleCount.DoubleCountingPolicy.Contains("Do not add a separate spread/slippage estimate")) "Double-counting policy missing no-spread-add rule."

Assert-True ($coverage.Classification -eq "COST_COVERAGE_PARTIAL_CORE_ANUBIS_EXOTICS_BLOCKED") "Coverage should be partial with Core/Anubis exotics blocked."
foreach ($symbol in @("USDCNH", "USDMXN", "USDNOK", "USDSEK", "USDSGD", "USDZAR")) {
    Assert-True (@($coverage.Rows | Where-Object { $_.ExecutionSymbol -eq $symbol -and $_.CostCoverageStatus -eq "COST_COVERAGE_BLOCKED_EXOTIC_NO_COST_EVIDENCE" }).Count -eq 1) "$symbol should be blocked for missing exotic cost evidence."
}
foreach ($symbol in @("AUDUSD", "EURUSD", "GBPUSD")) {
    Assert-True (@($coverage.Rows | Where-Object { $_.ExecutionSymbol -eq $symbol -and $_.CostCoverageStatus -eq "COST_COVERAGE_READY_MAJOR_GUIDANCE_ONLY" }).Count -eq 1) "$symbol should be covered by historical major-only guidance."
}

Assert-True ($commission.Classification -eq "COMMISSION_FEE_MODEL_READY_MAJOR_ONLY") "Commission/fee model should be major-only."
Assert-True ($commission.CoversCoreAnubisExotics -eq $false -and $commission.MissingSymbolsBlockNetPnl -eq $true) "Commission/fee model incorrectly covers exotics or unblocks net PnL."
Assert-True ($spread.Classification -eq "SPREAD_SLIPPAGE_POLICY_READY_ACTUAL_FILL_GROSS_NO_DOUBLE_COUNT") "Spread/slippage policy wrong."
Assert-True ($spread.FiveUsdPerMillionAsSpread -eq $false) "5 USD/million was treated as spread."
Assert-True ($financing.Classification -eq "FINANCING_SWAP_NOT_APPLICABLE_FLAT_INTRADAY_SANDBOX") "Financing/swap policy wrong."

Assert-True ($feasibility.Classification -eq "SANDBOX_COST_PREVIEW_READY_HISTORICAL_MAJOR_ONLY") "Cost preview feasibility should be historical major-only."
Assert-True (@($feasibility.Lifecycles | Where-Object { $_.Lifecycle -eq "Core/Anubis R013D" -and $_.CostAdjustedPreviewCanBeComputed -eq $false }).Count -eq 1) "Core/Anubis cost-adjusted preview should be blocked."

Assert-True ($preview.Classification -eq "ESTIMATED_COST_PREVIEW_COMPUTED_PARTIAL_MAJOR_ONLY") "Expected partial major-only estimated cost preview."
Assert-True (@($preview.LifecyclesIncluded | Where-Object { $_ -eq "CROSS-RAIL-R014 / PNL-PREVIEW-R004" }).Count -eq 1) "Historical lifecycle missing from preview."
Assert-True (@($preview.LifecyclesExcluded | Where-Object { $_ -eq "CORE-ANUBIS-R013D/R013E" }).Count -eq 1) "Core/Anubis should be excluded from partial preview."
Assert-True ($preview.CostAdjustedPreview.FullNetPnlReady -eq $false) "Partial preview incorrectly marked full net PnL ready."
Assert-True ($preview.NotAccounting -eq $true -and $preview.NotProduction -eq $true -and $preview.NotLedgerCommit -eq $true) "Preview crossed accounting/production/ledger boundary."

Assert-True ($net.Classification -eq "NET_PNL_BLOCKED_CORE_ANUBIS_EXOTICS_COSTS_MISSING") "Net PnL should remain blocked by Core/Anubis exotics."
Assert-True ($net.FullNetPnlReady -eq $false -and $net.AccountingProductionReady -eq $false) "Net/accounting/production readiness incorrectly marked ready."

Assert-True ($boundaryDecision.Classification -eq "ACCOUNTING_PRODUCTION_BOUNDARY_PRESERVED") "Accounting/production boundary not preserved."
Assert-True ($boundaryDecision.NoAccountingPnl -eq $true -and $boundaryDecision.NoProductionPnl -eq $true -and $boundaryDecision.NoLedgerCommit -eq $true) "Accounting/production/ledger boundary crossed."
Assert-True ($boundaryDecision.NoAccountCurrency -eq $true -and $boundaryDecision.NoAccountId -eq $true -and $boundaryDecision.NoPortfolioId -eq $true -and $boundaryDecision.NoStrategyId -eq $true -and $boundaryDecision.NoSourceExecutionIntentId -eq $true) "Invented account identifiers claimed."

Assert-True ($contract.Statuses."sandbox-cost-preview.v1" -eq "WITH_WARNINGS_HISTORICAL_MAJOR_ONLY") "Sandbox cost preview contract wrong."
Assert-True ($contract.Statuses."net-pnl-preview.v1" -eq "BLOCKED_CORE_ANUBIS_EXOTICS_COSTS_MISSING") "Net PnL contract should be blocked."
Assert-True ($contract.Statuses."accounting-attribution.v1" -eq "BLOCKED" -and $contract.Statuses."production-readiness.v1" -eq "BLOCKED") "Accounting/production contract not blocked."
Assert-True ($contract.Statuses."marketdata-readiness.v1" -eq "WITH_WARNINGS") "MarketData contract should remain WITH_WARNINGS."

Assert-True ($blockers.Classification -eq "BLOCKER_MAP_UPDATED_WITH_WARNINGS") "Blocker map not updated with warnings."
Assert-True (@($blockers.RemainingBlockers | Where-Object { $_ -eq "explicit cost evidence for Core/Anubis exotics" }).Count -eq 1) "Exotic cost evidence blocker missing."
Assert-True ($roadmap.Decision -eq "NEXT_RISK_COST_MODEL_R002_EXOTIC_COST_EVIDENCE") "Roadmap should point to exotic cost evidence."

Assert-True ($readiness.CostModelReadiness -eq "partial-major-only") "Readiness impact should be partial-major-only."
Assert-True ($readiness.EstimatedCostPreviewComputed -eq $true) "Estimated cost preview should be computed."
Assert-True ($readiness.NetPnlReadinessChanged -eq $false -and $readiness.NetPnlRemainsBlocked -eq $true) "Net PnL readiness should remain blocked."
Assert-True ($readiness.AccountingPnlChanged -eq $false -and $readiness.ProductionReadinessChanged -eq $false -and $readiness.MarketDataRemainsWithWarnings -eq $true) "Accounting/production/MarketData readiness changed incorrectly."

Assert-True ($boundary.NoNewR009Submission -eq $true -and $boundary.NoNewLmaxCall -eq $true -and $boundary.NoNewPolygonMassiveCall -eq $true) "External/execution call claimed."
Assert-True ($boundary.NoNewOrderFillReport -eq $true -and $boundary.NoDbMutation -eq $true -and $boundary.NoLedgerCommit -eq $true) "Order/fill/DB/ledger claimed."
Assert-True ($boundary.NoProductionLive -eq $true -and $boundary.NoCoreExecution -eq $true -and $boundary.NoManager -eq $true -and $boundary.NoAnubis -eq $true -and $boundary.NoCuda -eq $true -and $boundary.NoCoreNetting -eq $true) "Forbidden production/Core boundary crossed."
Assert-True ($boundary.NoAccountingPnl -eq $true -and $boundary.NoProductionPnl -eq $true -and $boundary.NoInventedFeesSpreadsCommissions -eq $true) "Invented costs or forbidden PnL claimed."

Assert-True ($summary.Contains("RISK_COST_MODEL_R001_WITH_WARNINGS_COST_MODEL_PARTIAL_MAJOR_ONLY")) "Summary missing final classification."
Assert-True ($summary.Contains("Net PnL preview ready: no")) "Summary must keep net PnL blocked."
Assert-True ($summary.Contains("NEXT_RISK_COST_MODEL_R002_EXOTIC_COST_EVIDENCE")) "Summary missing next package."
Assert-True ($summary.Contains("No R009 submission")) "Summary missing no-execution confirmation."

Write-Host "RISK_COST_MODEL_R001_GATE_PASS"
