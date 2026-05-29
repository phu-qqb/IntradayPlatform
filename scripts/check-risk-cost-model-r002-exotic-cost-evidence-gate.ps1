param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\risk-cost-model-r002-exotic-cost-evidence"
$Required = @(
    "r001-intake-validation.json",
    "core-anubis-exotic-execution-universe.json",
    "local-cost-evidence-discovery.json",
    "exotic-cost-evidence-validation-by-symbol.json",
    "major-minor-cost-evidence-validation.json",
    "cost-model-scope-decision.json",
    "commission-fee-computation-policy.json",
    "spread-slippage-evidence-policy.json",
    "r013d-cost-preview-feasibility.json",
    "r013d-estimated-cost-preview.json",
    "net-pnl-readiness-update.json",
    "operator-exotic-cost-evidence-template.json",
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

Assert-True (Test-Path -LiteralPath $ArtifactDir) "Risk/cost R002 artifact directory missing."
foreach ($name in $Required) {
    Assert-True (Test-Path -LiteralPath (Join-Path $ArtifactDir $name)) "Missing required R002 artifact: $name"
}

$intake = Read-Json "r001-intake-validation.json"
$universe = Read-Json "core-anubis-exotic-execution-universe.json"
$discovery = Read-Json "local-cost-evidence-discovery.json"
$exotic = Read-Json "exotic-cost-evidence-validation-by-symbol.json"
$major = Read-Json "major-minor-cost-evidence-validation.json"
$scope = Read-Json "cost-model-scope-decision.json"
$commission = Read-Json "commission-fee-computation-policy.json"
$spread = Read-Json "spread-slippage-evidence-policy.json"
$feasibility = Read-Json "r013d-cost-preview-feasibility.json"
$preview = Read-Json "r013d-estimated-cost-preview.json"
$net = Read-Json "net-pnl-readiness-update.json"
$template = Read-Json "operator-exotic-cost-evidence-template.json"
$boundaryDecision = Read-Json "accounting-production-boundary-decision.json"
$contract = Read-Json "contract-status-update.json"
$blockers = Read-Json "blocker-map-update.json"
$roadmap = Read-Json "roadmap-decision.json"
$readiness = Read-Json "readiness-impact.json"
$boundary = Read-Json "boundary-safety-evidence.json"
$summary = Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir "summary.md")

$requiredExotics = @("USDCNH", "USDMXN", "USDNOK", "USDSEK", "USDSGD", "USDZAR")

Assert-True ($intake.Classification -eq "R001_READY_FOR_EXOTIC_COST_EVIDENCE_SEARCH") "R001 intake not ready."
Assert-True ($intake.R001ClassificationConfirmed -eq $true) "R001 classification mismatch."
Assert-True ($intake.R001HistoricalMajorOnlyCostPreviewExists -eq $true) "R001 historical major-only preview missing."
Assert-True ($intake.R001FiveUsdPerMillionMajorOnly -eq $true) "R001 major-only guidance not preserved."
Assert-True ($intake.R001CoreAnubisExoticsUncovered -eq $true -and $intake.R001DidNotClaimFullNetPnlReadiness -eq $true) "R001 exotic blocker/net-PnL boundary not preserved."
Assert-True ($intake.R001DidNotMutateDbOrLedger -eq $true -and $intake.R001DidNotExecuteAnything -eq $true) "R001 claimed mutation or execution."

Assert-True ($universe.Classification -eq "CORE_ANUBIS_EXOTIC_UNIVERSE_READY_WITH_WARNINGS") "Exotic universe not ready."
foreach ($symbol in $requiredExotics) {
    Assert-True (@($universe.Rows | Where-Object { $_.ExecutionSymbol -eq $symbol -and $_.ExplicitExoticCostEvidenceRequired -eq $true -and $_.FiveUsdPerMillionMajorOnlyGuidanceApplies -eq $false }).Count -eq 1) "$symbol must require explicit exotic evidence and reject major-only guidance."
}
Assert-True (@($universe.Rows | Where-Object { $_.ExecutionSymbol -eq "USDJPY" -and $_.FilledQuantity -eq "38.4" -and $_.FillStatus -eq "PARTIAL" }).Count -eq 1) "USDJPY partial fill not preserved."

Assert-True ($discovery.Classification -eq "LOCAL_COST_EVIDENCE_PARTIAL") "Local cost evidence discovery should be partial."
Assert-True ($discovery.SearchLocalOnly -eq $true -and $discovery.NoInternet -eq $true -and $discovery.NoGithub -eq $true -and $discovery.NoExternalApi -eq $true) "Discovery crossed local-only boundary."
Assert-True ($discovery.GeneratedFixtureCommissionCsvsUsableForOperatorCostEvidence -eq $false) "Generated fixture commissions were incorrectly treated as operator evidence."
Assert-True (@($discovery.CandidateEvidenceFiles | Where-Object { $_.UsableForRequiredExotics -eq $true }).Count -eq 0) "Discovery found usable exotic evidence but R002 is classed missing."

Assert-True ($exotic.Classification -eq "EXOTIC_COST_EVIDENCE_MISSING_ALL_REQUIRED") "Exotic evidence should be missing for all required symbols."
foreach ($symbol in $requiredExotics) {
    Assert-True (@($exotic.Rows | Where-Object { $_.ExecutionSymbol -eq $symbol -and $_.Classification -eq "EXOTIC_COST_EVIDENCE_MISSING" -and $_.SufficientlyClearForComputation -eq $false }).Count -eq 1) "$symbol exotic evidence must be missing."
}

Assert-True ($major.Classification -eq "MAJOR_MINOR_COST_EVIDENCE_READY_WITH_WARNINGS") "Major/minor evidence should be ready with warnings."
Assert-True ($major.GuidanceCoversHistoricalThreePairs -eq $true -and $major.BroaderMajorCoverageRequiresExplicitClassification -eq $true) "Major guidance scope not preserved."
Assert-True ($major.GuidanceIsFeeCommissionOrGeneralCost -eq "general cost guidance, not explicit commission schedule") "Major guidance was misclassified as commission schedule."

Assert-True ($scope.Classification -eq "COST_MODEL_SCOPE_MAJOR_ONLY") "Cost model scope must remain major-only."
Assert-True ($scope.CoversCoreAnubisLifecycle -eq $false -and $scope.CanComputeFullSandboxNetPnlPreview -eq $false) "Scope incorrectly covers Core/Anubis or full net PnL."
Assert-True ($scope.AccountingReady -eq $false -and $scope.ProductionReady -eq $false) "Scope incorrectly marks accounting/production ready."

Assert-True ($commission.Classification -eq "COMMISSION_FEE_COMPUTATION_POLICY_READY_WITH_WARNINGS") "Commission/fee computation policy missing."
Assert-True ($commission.ExcludeUnfilledUSDJPY50 -eq $true -and $commission.ExcludeZeroQuantityLines -eq $true -and $commission.DoNotDoubleCountSpread -eq $true) "Commission policy fails USDJPY/zero/spread guard."
Assert-True ($spread.Classification -eq "SPREAD_SLIPPAGE_POLICY_READY_NO_DOUBLE_COUNT") "Spread/slippage policy wrong."
Assert-True ($spread.DoNotAddSpreadSlippageEstimateByDefault -eq $true -and $spread.SpreadSlippageGapsBlockFullAllInNetModel -eq $true) "Spread policy would double-count or unblock all-in net model."

Assert-True ($feasibility.Classification -eq "R013D_COST_PREVIEW_BLOCKED_EXOTIC_COST_GAPS") "R013D feasibility should be blocked by exotic gaps."
Assert-True ($feasibility.ActualFillsExist -eq $true -and $feasibility.GrossPnlPreviewExists -eq $true -and $feasibility.FullNetPnlReady -eq $false) "R013D gross/fill evidence or net boundary wrong."
Assert-True (@($feasibility.ExoticsMissingCostEvidence).Count -eq 6) "R013D missing exotic list incomplete."

Assert-True ($preview.Classification -eq "R013D_ESTIMATED_COST_PREVIEW_NOT_COMPUTED_EXOTIC_COST_GAPS") "R013D estimated cost preview should not be computed."
Assert-True (@($preview.IncludedFills).Count -eq 0) "No R013D fills should be included without complete exotic evidence."
Assert-True ($preview.NotAccounting -eq $true -and $preview.NotProduction -eq $true -and $preview.NotLedgerCommit -eq $true) "R013D preview crossed accounting/production/ledger boundary."
Assert-True (@($preview.Warnings | Where-Object { $_ -eq "Do not include unfilled USDJPY 50.0" }).Count -eq 1) "USDJPY unfilled exclusion warning missing."

Assert-True ($net.Classification -eq "NET_PNL_BLOCKED_EXOTIC_COST_EVIDENCE_MISSING") "Net PnL should remain blocked by missing exotic evidence."
Assert-True ($net.FullSandboxNetPnlReady -eq $false -and $net.AccountingPnlRemainsBlocked -eq $true -and $net.ProductionPnlRemainsBlocked -eq $true -and $net.LedgerCommitRemainsBlocked -eq $true) "Net/accounting/production/ledger boundary wrong."

Assert-True ($template.Classification -eq "OPERATOR_EXOTIC_COST_TEMPLATE_CREATED") "Operator exotic cost template missing."
foreach ($symbol in $requiredExotics) {
    Assert-True (@($template.TemplateRows | Where-Object { $_.ExecutionSymbol -eq $symbol -and $_.AllowedScope -eq "SandboxPreviewOnly" -and $_.NotAccounting -eq $true -and $_.NotProduction -eq $true -and $_.NotLedgerCommit -eq $true }).Count -eq 1) "$symbol template row missing or unsafe."
}

Assert-True ($boundaryDecision.Classification -eq "ACCOUNTING_PRODUCTION_BOUNDARY_PRESERVED") "Accounting/production boundary decision wrong."
Assert-True ($boundaryDecision.NoAccountingPnl -eq $true -and $boundaryDecision.NoProductionPnl -eq $true -and $boundaryDecision.NoLedgerCommit -eq $true -and $boundaryDecision.NoAccountCurrency -eq $true) "Accounting/production boundary crossed."

Assert-True ($contract.Statuses."r013d-cost-preview.v1" -eq "BLOCKED_EXOTIC_COST_GAPS") "R013D cost preview contract should be blocked."
Assert-True ($contract.Statuses."net-pnl-preview.v1" -eq "BLOCKED_EXOTIC_COST_EVIDENCE_MISSING") "Net PnL contract should be blocked."
Assert-True ($contract.Statuses."accounting-attribution.v1" -eq "BLOCKED" -and $contract.Statuses."production-readiness.v1" -eq "BLOCKED") "Accounting/production contract not blocked."
Assert-True ($contract.Statuses."marketdata-readiness.v1" -eq "WITH_WARNINGS") "MarketData should remain WITH_WARNINGS."

Assert-True ($blockers.Classification -eq "BLOCKER_MAP_UPDATED_WITH_WARNINGS") "Blocker map not updated."
Assert-True (@($blockers.RemainingBlockers | Where-Object { $_ -eq "explicit exotic costs" }).Count -eq 1) "Explicit exotic costs blocker missing."
Assert-True ($roadmap.Decision -eq "NEXT_RISK_COST_MODEL_R003_OPERATOR_EXOTIC_COST_IMPORT") "Roadmap decision wrong."

Assert-True ($readiness.ExoticCostEvidenceFound -eq $false -and $readiness.R013DCostPreviewCanBeComputed -eq $false) "Readiness impact incorrectly found evidence or computed preview."
Assert-True ($readiness.NetPnlReadinessChanged -eq $false -and $readiness.NetPnlRemainsBlocked -eq $true) "Net PnL readiness incorrectly changed."
Assert-True ($readiness.MarketDataRemainsWithWarnings -eq $true -and $readiness.SandboxProgrammeAcceptedWithGrossPnlV0ReadyRemainsValid -eq $true) "MarketData/product anchor readiness wrong."

Assert-True ($boundary.NoNewR009Submission -eq $true -and $boundary.NoNewLmaxCall -eq $true -and $boundary.NoNewPolygonMassiveCall -eq $true -and $boundary.NoMarketDataFetch -eq $true) "External execution/provider call claimed."
Assert-True ($boundary.NoNewOrderFillReport -eq $true -and $boundary.NoDbMutation -eq $true -and $boundary.NoLedgerCommit -eq $true) "Order/fill/DB/ledger claimed."
Assert-True ($boundary.NoProductionLive -eq $true -and $boundary.NoCoreExecution -eq $true -and $boundary.NoManager -eq $true -and $boundary.NoAnubis -eq $true -and $boundary.NoCuda -eq $true -and $boundary.NoCoreNetting -eq $true) "Production/Core boundary crossed."
Assert-True ($boundary.NoAccountCurrencyAggregation -eq $true -and $boundary.NoAccountingPnl -eq $true -and $boundary.NoProductionPnl -eq $true -and $boundary.NoInventedFeesSpreadsCommissions -eq $true) "Account/PnL/invented-cost boundary crossed."

Assert-True ($summary.Contains("RISK_COST_MODEL_R002_WITH_WARNINGS_NO_EXOTIC_COST_EVIDENCE_TEMPLATE_CREATED")) "Summary missing final classification."
Assert-True ($summary.Contains("Found: no usable explicit exotic cost evidence.")) "Summary missing evidence result."
Assert-True ($summary.Contains("Computed: no.")) "Summary must say R013D cost preview was not computed."
Assert-True ($summary.Contains("NEXT_RISK_COST_MODEL_R003_OPERATOR_EXOTIC_COST_IMPORT")) "Summary missing next package."
Assert-True ($summary.Contains("No R009 submission")) "Summary missing no-execution confirmation."

Write-Host "RISK_COST_MODEL_R002_EXOTIC_COST_EVIDENCE_GATE_PASS"
