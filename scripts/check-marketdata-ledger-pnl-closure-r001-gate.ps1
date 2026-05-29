param(
    [string]$Root = "artifacts/readiness/marketdata-ledger-pnl-closure"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "MARKETDATA-LEDGER-PNL-CLOSURE-R001 validation failed: $Message"
}

function Read-Json([string]$Name) {
    $path = Join-Path $Root $Name
    if (!(Test-Path $path)) { Fail "missing artifact $Name" }
    return Get-Content $path -Raw | ConvertFrom-Json
}

$required = @(
    "phase-marketdata-ledger-pnl-closure-r001-summary.md",
    "phase-marketdata-ledger-pnl-closure-r001-economic-readiness-reference.json",
    "phase-marketdata-ledger-pnl-closure-r001-mark-price-evidence.json",
    "phase-marketdata-ledger-pnl-closure-r001-fx-conversion-evidence.json",
    "phase-marketdata-ledger-pnl-closure-r001-cost-spread-commission-evidence.json",
    "phase-marketdata-ledger-pnl-closure-r001-position-cost-basis-evidence.json",
    "phase-marketdata-ledger-pnl-closure-r001-account-currency-evidence.json",
    "phase-marketdata-ledger-pnl-closure-r001-attribution-policy-evidence.json",
    "phase-marketdata-ledger-pnl-closure-r001-pms-qubes-lineage-recheck.json",
    "phase-marketdata-ledger-pnl-closure-r001-updated-pnl-readiness.json",
    "phase-marketdata-ledger-pnl-closure-r001-remaining-blockers.json",
    "phase-marketdata-ledger-pnl-closure-r001-decision.json",
    "phase-marketdata-ledger-pnl-closure-r001-no-external-audit.json",
    "phase-marketdata-ledger-pnl-closure-r001-no-execution-audit.json",
    "phase-marketdata-ledger-pnl-closure-r001-no-db-mutation-audit.json",
    "phase-marketdata-ledger-pnl-closure-r001-no-order-fill-route-audit.json",
    "phase-marketdata-ledger-pnl-closure-r001-no-ledger-state-mutation-audit.json",
    "phase-marketdata-ledger-pnl-closure-r001-canonical-timing-preservation.json",
    "phase-marketdata-ledger-pnl-closure-r001-direct-cross-exclusion-preservation.json",
    "phase-marketdata-ledger-pnl-closure-r001-usdjpy-caveat-preservation.json",
    "phase-marketdata-ledger-pnl-closure-r001-forbidden-actions-audit.json",
    "phase-marketdata-ledger-pnl-closure-r001-next-phase-recommendation.json",
    "phase-marketdata-ledger-pnl-closure-r001-build-test-validator-evidence.json"
)

foreach ($file in $required) {
    if (!(Test-Path (Join-Path $Root $file))) { Fail "required file missing: $file" }
}

$decision = Read-Json "phase-marketdata-ledger-pnl-closure-r001-decision.json"
if ([string]$decision.Status -ne "PASS") { Fail "decision status must be PASS" }
if ([string]$decision.CurrentCeiling -ne "SandboxPriceDeltaOnlyReady") { Fail "current ceiling must remain SandboxPriceDeltaOnlyReady" }
if ([string]$decision.SandboxTheoreticalPnl -notmatch "Blocked") { Fail "sandbox theoretical PnL must remain blocked" }
if ([string]$decision.PaperAccountingPnl -ne "Blocked") { Fail "paper accounting PnL must remain blocked" }
if ([string]$decision.ProductionPnl -ne "Blocked") { Fail "production PnL must remain blocked" }
if ([string]$decision.LedgerCommit -ne "Blocked") { Fail "ledger commit must remain blocked" }
if ([string]$decision.MarketDataDbStatus -ne "AdoptedWithWarnings") { Fail "MarketData DB status must remain AdoptedWithWarnings" }
if ($decision.AccountingPnlClaimed -or $decision.ProductionPnlClaimed) { Fail "accounting/production PnL claimed" }

$expectedClassifications = @(
    "MARKETDATA_LEDGER_PNL_CLOSURE_R001_PASS_PNL_EVIDENCE_INVENTORY_READY_NO_EXECUTION",
    "MARKETDATA_LEDGER_PNL_CLOSURE_R001_PASS_MARK_FX_COST_POLICY_GAPS_READY_NO_MUTATION",
    "MARKETDATA_LEDGER_PNL_CLOSURE_R001_PASS_PRICE_DELTA_BOUNDARY_PRESERVED_NO_PRODUCTION_PNL",
    "MARKETDATA_LEDGER_PNL_CLOSURE_R001_PASS_NO_LEDGER_COMMIT_NO_STATE_MUTATION_GATE"
)
foreach ($classification in $expectedClassifications) {
    if ($decision.Classifications -notcontains $classification) { Fail "missing classification $classification" }
}

$mark = Read-Json "phase-marketdata-ledger-pnl-closure-r001-mark-price-evidence.json"
if ([string]$mark.Status -ne "MissingMarkPrices") { Fail "mark price status must remain MissingMarkPrices" }
if ($mark.MarksInvented) { Fail "mark prices invented" }
if ($mark.NoLiveMarketDataCalled -ne $true) { Fail "live market data call not blocked" }

$fx = Read-Json "phase-marketdata-ledger-pnl-closure-r001-fx-conversion-evidence.json"
if ([string]$fx.Status -ne "MissingFxConversion") { Fail "FX conversion must remain missing" }
if ($fx.ConversionRatesInvented) { Fail "FX conversion invented" }

$account = Read-Json "phase-marketdata-ledger-pnl-closure-r001-account-currency-evidence.json"
if ([string]$account.Status -ne "MissingAccountCurrency") { Fail "account currency must remain missing" }
if ($account.AccountCurrencyInvented) { Fail "account currency invented" }

$cost = Read-Json "phase-marketdata-ledger-pnl-closure-r001-cost-spread-commission-evidence.json"
if ([string]$cost.Status -ne "MissingCostSpreadCommissionModel") { Fail "cost/spread/commission model must remain missing" }
if ($cost.CostModelInvented) { Fail "cost model invented" }

$basis = Read-Json "phase-marketdata-ledger-pnl-closure-r001-position-cost-basis-evidence.json"
if ([string]$basis.Status -ne "MissingPositionCostBasisModel") { Fail "position/cost basis model must remain missing" }
if ($basis.AccountingCostBasisReady) { Fail "accounting cost basis incorrectly ready" }

$attrib = Read-Json "phase-marketdata-ledger-pnl-closure-r001-attribution-policy-evidence.json"
if ([string]$attrib.Status -ne "PARTIAL_LINEAGE_POLICY_MISSING") { Fail "attribution policy should be partial/missing" }
if ($attrib.AttributionPolicyInvented) { Fail "attribution policy invented" }

$lineage = Read-Json "phase-marketdata-ledger-pnl-closure-r001-pms-qubes-lineage-recheck.json"
if ([string]$lineage.AccountId -ne "MISSING") { Fail "AccountId must remain missing" }
if ([string]$lineage.PortfolioId -ne "MISSING") { Fail "PortfolioId must remain missing" }
if ([string]$lineage.StrategyId -ne "MISSING") { Fail "StrategyId must remain missing" }
if ([string]$lineage.SourceExecutionIntentId -ne "MISSING") { Fail "SourceExecutionIntentId must remain missing" }
if ([string]$lineage.QubesRunIdStatus -ne "QubesRunIdNotPmsApprovedEconomicOutput") { Fail "QubesRunId warning weakened" }
if ($lineage.DoNotUseOldQubes4EAsActiveState -ne $true) { Fail "old Qubes 4E active-state guard missing" }

$readiness = Read-Json "phase-marketdata-ledger-pnl-closure-r001-updated-pnl-readiness.json"
if ([string]$readiness.CurrentCeiling -ne "SandboxPriceDeltaOnlyReady") { Fail "updated readiness ceiling mismatch" }
if ($readiness.LedgerCommitBlocked -ne $true) { Fail "ledger commit must be blocked" }
if ($readiness.AccountingPnlClaimed -or $readiness.ProductionPnlClaimed) { Fail "accounting/production PnL readiness claimed" }

$remaining = Read-Json "phase-marketdata-ledger-pnl-closure-r001-remaining-blockers.json"
foreach ($blocker in @("MissingMarkPrices", "MissingCostSpreadCommissionModel", "MissingFxConversion", "MissingPositionCostBasisModel", "MissingAccountCurrency", "MissingAttributionPolicy", "MissingAccountId", "MissingPortfolioId", "MissingStrategyId", "MissingSourceExecutionIntentId", "QubesRunIdNotPmsApprovedEconomicOutput", "CommitSafeIdempotencyPolicyIncomplete", "MarketDataWarnM30Missing", "MarketDataWarnRowCountsMissing", "MarketDataWarnTickSchemaPartial")) {
    if ($remaining.RemainingBlockers -notcontains $blocker) { Fail "remaining blocker missing: $blocker" }
}

$noExternal = Read-Json "phase-marketdata-ledger-pnl-closure-r001-no-external-audit.json"
if ($noExternal.LmaxCalled -or $noExternal.PolygonCalled -or $noExternal.ExternalApiCalled -or $noExternal.BrokerActivated -or $noExternal.LiveMarketDataRequested) { Fail "external/broker/live market data boundary violation" }

$noExecution = Read-Json "phase-marketdata-ledger-pnl-closure-r001-no-execution-audit.json"
foreach ($flag in @("PmsEmsOmsCycleRun", "ManualNoExternalRun", "QubesRun", "PythonCppCudaWorkloadRun", "BacktestOrSimulationRun", "OrdersCreated", "RoutesCreated", "SubmissionsCreated", "NewFillsCreated", "ExecutionReportsCreated", "ExecutableSchedulesCreated")) {
    if ($noExecution.$flag) { Fail "execution boundary violation: $flag" }
}

$noDb = Read-Json "phase-marketdata-ledger-pnl-closure-r001-no-db-mutation-audit.json"
foreach ($flag in @("DbMutation", "MigrationCreatedOrApplied", "Insert", "Update", "Delete", "Merge", "Truncate", "Drop", "Alter", "MigrateCalled", "EnsureCreatedCalled")) {
    if ($noDb.$flag) { Fail "DB mutation boundary violation: $flag" }
}

$noLedger = Read-Json "phase-marketdata-ledger-pnl-closure-r001-no-ledger-state-mutation-audit.json"
foreach ($flag in @("PaperLedgerCommit", "ProductionLedgerCommit", "PaperPositionsMutated", "ProductionPositionsMutated", "CashStateMutated", "TradingStateMutated")) {
    if ($noLedger.$flag) { Fail "ledger/state mutation boundary violation: $flag" }
}

$canonical = Read-Json "phase-marketdata-ledger-pnl-closure-r001-canonical-timing-preservation.json"
if ($canonical.Legacy06213651FutureCanonicalUsed) { Fail "legacy :06/:21/:36/:51 used as future canonical" }
if ($canonical.CanonicalQuarterHourClosesOnly -ne $true) { Fail "canonical quarter-hour policy not preserved" }

$direct = Read-Json "phase-marketdata-ledger-pnl-closure-r001-direct-cross-exclusion-preservation.json"
if ($direct.DirectCrossExecutionAllowed) { Fail "direct-cross execution allowed" }

$usdjpy = Read-Json "phase-marketdata-ledger-pnl-closure-r001-usdjpy-caveat-preservation.json"
if ([string]$usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or [string]$usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or $usdjpy.RequiresInversion -ne $true -or [string]$usdjpy.SecurityID -ne "4004" -or [string]$usdjpy.SecurityIDSource -ne "8" -or $usdjpy.CaveatWeakened) {
    Fail "USDJPY caveat weakened"
}

$forbidden = Read-Json "phase-marketdata-ledger-pnl-closure-r001-forbidden-actions-audit.json"
foreach ($flag in @("ForbiddenActionsTouched", "ProductionPnlClaimed", "AccountingPnlClaimed", "MarketDataDbReadinessClaimedComplete", "MarkPricesInvented", "FxConversionInvented", "CostModelInvented", "AccountCurrencyInvented", "AttributionPolicyInvented")) {
    if ($forbidden.$flag) { Fail "forbidden action or invented evidence: $flag" }
}

$build = Read-Json "phase-marketdata-ledger-pnl-closure-r001-build-test-validator-evidence.json"
if ([string]$build.Build -eq "Pending" -or [string]$build.StaticChecks -eq "Pending" -or [string]$build.Validator -eq "Pending") {
    Fail "build/tests/validator evidence missing"
}

Write-Host "MARKETDATA-LEDGER-PNL-CLOSURE-R001 validator passed."
