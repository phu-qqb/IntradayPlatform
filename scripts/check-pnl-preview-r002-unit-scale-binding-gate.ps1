param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "PNL_PREVIEW_R002_GATE_FAIL: $Message"
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing required artifact: $Path"
    }

    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Assert-True($Value, [string]$Name) {
    if ($Value -ne $true) { Fail "$Name must be true" }
}

function Assert-False($Value, [string]$Name) {
    if ($Value -eq $true) { Fail "$Name must be false" }
}

$artifactDir = Join-Path $Root "artifacts/readiness/pnl-preview"
$required = @(
    "phase-pnl-preview-r002-summary.md",
    "phase-pnl-preview-r002-r001-reference.json",
    "phase-pnl-preview-r002-operator-policy-reference.json",
    "phase-pnl-preview-r002-unit-scale-evidence-discovery.json",
    "phase-pnl-preview-r002-unit-scale-binding-results.json",
    "phase-pnl-preview-r002-per-symbol-gross-roundtrip-pnl-amounts.json",
    "phase-pnl-preview-r002-aggregate-gross-roundtrip-pnl-amount.json",
    "phase-pnl-preview-r002-boundary-and-blockers.json",
    "phase-pnl-preview-r002-decision.json",
    "phase-pnl-preview-r002-no-external-audit.json",
    "phase-pnl-preview-r002-no-execution-audit.json",
    "phase-pnl-preview-r002-no-db-mutation-audit.json",
    "phase-pnl-preview-r002-no-order-fill-route-audit.json",
    "phase-pnl-preview-r002-no-ledger-state-mutation-audit.json",
    "phase-pnl-preview-r002-not-net-pnl-audit.json",
    "phase-pnl-preview-r002-not-accounting-pnl-audit.json",
    "phase-pnl-preview-r002-not-production-pnl-audit.json",
    "phase-pnl-preview-r002-canonical-timing-preservation.json",
    "phase-pnl-preview-r002-direct-cross-exclusion-preservation.json",
    "phase-pnl-preview-r002-usdjpy-caveat-preservation.json",
    "phase-pnl-preview-r002-forbidden-actions-audit.json",
    "phase-pnl-preview-r002-next-phase-recommendation.json",
    "phase-pnl-preview-r002-build-test-validator-evidence.json"
)

foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $name))) {
        Fail "Missing required artifact $name"
    }
}

$r001 = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r002-r001-reference.json")
if ($r001.r001Decision -ne "SandboxGrossRoundTripPnlPreviewV0Partial") { Fail "R001 decision mismatch" }
Assert-True $r001.fillPairsComplete "R001 fill pairs complete"
Assert-False $r001.newOrdersRoutesSubmissionsFillsReportsCreatedByR002 "R002 new order/report creation"

$policy = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r002-operator-policy-reference.json")
Assert-True $policy.sandboxGrossRoundTripPnlPreviewV0Ready "operator policy ready"
Assert-True $policy.grossOnly "gross only"
Assert-True $policy.quoteCurrencyOnly "quote currency only"
Assert-True $policy.closedRoundTripOnly "closed round-trip only"
Assert-False $policy.costsApplied "policy costs"
Assert-False $policy.feesApplied "policy fees"
Assert-False $policy.commissionsApplied "policy commissions"
Assert-False $policy.fxConversionApplied "policy FX"
Assert-False $policy.accountCurrencyAggregationApplied "policy account currency"
Assert-False $policy.netPnlReady "policy net pnl"
Assert-False $policy.accountingPnlReady "policy accounting pnl"
Assert-False $policy.productionPnlReady "policy production pnl"
Assert-False $policy.ledgerCommitAllowed "policy ledger commit"
Assert-False $policy.tradingStateMutationAllowed "policy trading state"

$discovery = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r002-unit-scale-evidence-discovery.json")
Assert-True $discovery.evidenceDiscoveryOnly "evidence discovery only"
Assert-False $discovery.unitScaleInvented "unit scale invented"
Assert-False $discovery.commonValidatedUsdPairUnitScaleFound "common USD-pair unit scale"
$eurDiscovery = @($discovery.discovery | Where-Object { $_.symbol -eq "EURUSD" })[0]
if ($null -eq $eurDiscovery -or [decimal]$eurDiscovery.contractSizeOrUnitScale -ne 10000) { Fail "EURUSD ContractSize=10000 evidence missing" }
if ($eurDiscovery.quantityUnit -ne "LMAXVenueOrderQtyContractUnit") { Fail "EURUSD quantity unit mismatch" }
foreach ($symbol in @("AUDUSD", "GBPUSD")) {
    $row = @($discovery.discovery | Where-Object { $_.symbol -eq $symbol })[0]
    if ($null -eq $row) { Fail "Missing discovery row $symbol" }
    if ($null -ne $row.contractSizeOrUnitScale) { Fail "$symbol unit scale must not be invented" }
    if ($row.status -ne "Missing") { Fail "$symbol status must be Missing" }
}

$binding = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r002-unit-scale-binding-results.json")
Assert-False $binding.unitScaleInvented "binding unit scale invented"
Assert-False $binding.allSymbolsBound "all symbols bound"
if (@($binding.boundSymbols).Count -ne 1 -or $binding.boundSymbols -notcontains "EURUSD") { Fail "Only EURUSD should be bound" }
foreach ($symbol in @("AUDUSD", "GBPUSD")) {
    if ($binding.missingSymbols -notcontains $symbol) { Fail "$symbol must remain unit-scale missing" }
    $row = @($binding.bindingResults | Where-Object { $_.symbol -eq $symbol })[0]
    if ($row.bindingStatus -ne "UnitScaleMissing") { Fail "$symbol binding status mismatch" }
    if ($null -ne $row.contractSizeOrUnitScale) { Fail "$symbol contract scale must be null" }
}
$eurBinding = @($binding.bindingResults | Where-Object { $_.symbol -eq "EURUSD" })[0]
if ($eurBinding.bindingStatus -ne "UnitScaleBound" -or [decimal]$eurBinding.contractSizeOrUnitScale -ne 10000) { Fail "EURUSD binding mismatch" }
if (@($binding.conflicts).Count -ne 0) { Fail "Unit scale conflicts present" }

$amounts = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r002-per-symbol-gross-roundtrip-pnl-amounts.json")
Assert-True $amounts.previewOnly "amount preview only"
Assert-True $amounts.grossOnly "amount gross"
Assert-True $amounts.quoteCurrencyOnly "amount quote"
Assert-True $amounts.closedRoundTripOnly "amount closed"
Assert-False $amounts.absoluteGrossPnlAmountsComputedForAllSymbols "amounts for all symbols"
Assert-True $amounts.absoluteGrossPnlAmountsComputedForBoundSymbols "amounts for bound symbols"
Assert-False $amounts.unitScaleInvented "amount unit scale invented"
$eurAmount = @($amounts.rows | Where-Object { $_.symbol -eq "EURUSD" })[0]
if ([decimal]$eurAmount.contractSizeOrUnitScale -ne 10000) { Fail "EURUSD amount scale mismatch" }
if ([decimal]$eurAmount.priceDelta -ne -0.00005) { Fail "EURUSD price delta mismatch" }
if ([decimal]$eurAmount.grossRoundTripPnlQuoteCurrency -ne -0.05) { Fail "EURUSD gross amount mismatch" }
foreach ($symbol in @("AUDUSD", "GBPUSD")) {
    $row = @($amounts.rows | Where-Object { $_.symbol -eq $symbol })[0]
    if ($null -ne $row.contractSizeOrUnitScale) { Fail "$symbol amount scale must be null" }
    if ($null -ne $row.grossRoundTripPnlQuoteCurrency) { Fail "$symbol amount must be null" }
    if ($row.warnings -notcontains "UnitScaleMissing") { Fail "$symbol must warn UnitScaleMissing" }
}
foreach ($row in $amounts.rows) {
    if ($row.pnlCurrency -ne "USD") { Fail "$($row.symbol) PnL currency must be USD" }
    Assert-False $row.costsApplied "$($row.symbol) costs"
    Assert-False $row.feesApplied "$($row.symbol) fees"
    Assert-False $row.commissionsApplied "$($row.symbol) commissions"
    Assert-False $row.fxConversionApplied "$($row.symbol) FX"
    Assert-False $row.accountCurrencyApplied "$($row.symbol) account currency"
    Assert-False $row.netPnlReady "$($row.symbol) net pnl"
    Assert-False $row.accountingPnlReady "$($row.symbol) accounting pnl"
    Assert-False $row.productionPnlReady "$($row.symbol) production pnl"
    Assert-False $row.ledgerCommitAllowed "$($row.symbol) ledger commit"
    Assert-True $row.sandboxOnly "$($row.symbol) sandbox"
    Assert-True $row.notProductionPnl "$($row.symbol) not production pnl"
}

$aggregate = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r002-aggregate-gross-roundtrip-pnl-amount.json")
Assert-False $aggregate.aggregateAvailable "aggregate available"
Assert-True $aggregate.partialAggregateOnly "partial aggregate"
Assert-False $aggregate.fullThreeSymbolAggregateAvailable "full aggregate"
if ([decimal]$aggregate.grossRoundTripPnlQuoteCurrency -ne -0.05) { Fail "partial aggregate amount mismatch" }
if ($aggregate.computedSymbols -notcontains "EURUSD") { Fail "EURUSD missing from computed symbols" }
foreach ($symbol in @("AUDUSD", "GBPUSD")) {
    if ($aggregate.missingSymbols -notcontains $symbol) { Fail "$symbol missing from aggregate missing symbols" }
}
Assert-False $aggregate.unsupportedSymbolsIncluded "unsupported symbols"
Assert-False $aggregate.directCrossesIncluded "direct crosses"
Assert-False $aggregate.productionFillsIncluded "production fills"
Assert-False $aggregate.costsApplied "aggregate costs"
Assert-False $aggregate.feesApplied "aggregate fees"
Assert-False $aggregate.commissionsApplied "aggregate commissions"
Assert-False $aggregate.fxConversionApplied "aggregate FX"
Assert-False $aggregate.accountCurrencyAggregationApplied "aggregate account currency"
Assert-False $aggregate.netPnlReady "aggregate net pnl"
Assert-False $aggregate.accountingPnlReady "aggregate accounting pnl"
Assert-False $aggregate.productionPnlReady "aggregate production pnl"
Assert-False $aggregate.ledgerCommitAllowed "aggregate ledger commit"

$boundary = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r002-boundary-and-blockers.json")
Assert-True $boundary.previewOnly "boundary preview"
Assert-True $boundary.grossOnly "boundary gross"
Assert-True $boundary.quoteCurrencyOnly "boundary quote"
Assert-True $boundary.closedRoundTripOnly "boundary closed"
Assert-False $boundary.costsApplied "boundary costs"
Assert-False $boundary.feesApplied "boundary fees"
Assert-False $boundary.commissionsApplied "boundary commissions"
Assert-False $boundary.fxConversionApplied "boundary FX"
Assert-False $boundary.accountCurrencyAggregationApplied "boundary account currency"
Assert-False $boundary.netPnlReady "boundary net pnl"
Assert-False $boundary.accountingPnlReady "boundary accounting pnl"
Assert-False $boundary.productionPnlReady "boundary production pnl"
Assert-False $boundary.paperLedgerCommitAllowed "paper ledger commit"
Assert-False $boundary.productionLedgerCommitAllowed "production ledger commit"
Assert-False $boundary.tradingStateMutationAllowed "trading state mutation"
Assert-True $boundary.accountingProductionPnlBlockersPreserved "accounting/production blockers"
foreach ($blocker in @("MissingMarkPrices", "MissingCostSpreadCommissionModel", "MissingFxConversion", "MissingAccountCurrency", "MissingPositionCostBasisModel", "MissingAttributionPolicy", "MissingAccountId", "MissingPortfolioId", "MissingStrategyId", "MissingSourceExecutionIntentId", "QubesRunIdNotPmsApprovedEconomicOutput", "CommitSafeIdempotencyPolicyIncomplete")) {
    if ($boundary.blockersPreserved -notcontains $blocker) { Fail "Missing preserved blocker $blocker" }
}
foreach ($blocker in @("AUDUSDUnitScaleMissing", "GBPUSDUnitScaleMissing")) {
    if ($boundary.unitScaleBlockersRemaining -notcontains $blocker) { Fail "Missing unit-scale blocker $blocker" }
}

$decision = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r002-decision.json")
if ($decision.decision -ne "SandboxGrossRoundTripPnlPreviewV0PartialUnitScaleMissing") { Fail "decision mismatch" }
Assert-True $decision.amountsComputedForBoundSymbols "bound symbol amounts"
Assert-False $decision.amountsComputedForAllSymbols "all symbol amounts"
Assert-False $decision.unitScaleConflicts "unit scale conflicts"
Assert-False $decision.netPnlReady "decision net pnl"
Assert-False $decision.accountingPnlReady "decision accounting pnl"
Assert-False $decision.productionPnlReady "decision production pnl"
Assert-False $decision.ledgerCommitReady "decision ledger commit"
Assert-False $decision.tradingStateMutation "decision trading state"

foreach ($auditFile in @(
    "phase-pnl-preview-r002-no-external-audit.json",
    "phase-pnl-preview-r002-no-execution-audit.json",
    "phase-pnl-preview-r002-no-db-mutation-audit.json",
    "phase-pnl-preview-r002-no-ledger-state-mutation-audit.json",
    "phase-pnl-preview-r002-not-net-pnl-audit.json",
    "phase-pnl-preview-r002-not-accounting-pnl-audit.json",
    "phase-pnl-preview-r002-not-production-pnl-audit.json"
)) {
    $audit = Read-Json (Join-Path $artifactDir $auditFile)
    foreach ($prop in $audit.PSObject.Properties.Name) {
        if ($prop -ne "phase" -and $prop -ne "auditResult" -and $prop -notmatch "Preserved$" -and $audit.$prop -eq $true) {
            Fail "$auditFile has true forbidden property $prop"
        }
    }
}

$noOrder = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r002-no-order-fill-route-audit.json")
if ($noOrder.ordersCreated -ne 0 -or $noOrder.routesCreated -ne 0 -or $noOrder.submissionsCreated -ne 0 -or $noOrder.newFillsCreated -ne 0 -or $noOrder.newExecutionReportsCreated -ne 0 -or $noOrder.executableSchedulesCreated -ne 0) {
    Fail "order/fill/route artifacts created"
}
Assert-True $noOrder.sourceFillsReusedOnly "source fills reused only"

$timing = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r002-canonical-timing-preservation.json")
Assert-True $timing.futureTimestampsUseCanonicalQuarterHour "canonical quarter-hour"
Assert-False $timing.legacy06UsedAsFutureCanonical "legacy :06 future canonical"

$direct = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r002-direct-cross-exclusion-preservation.json")
Assert-False $direct.directCrossExecutionAllowed "direct-cross execution"
Assert-False $direct.directCrossesIncluded "direct-cross included"
Assert-False $direct.pnlLedgerCreatesDirectCrossExposureFromRawQubesSignals "direct-cross exposure"

$usdjpy = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r002-usdjpy-caveat-preservation.json")
Assert-True $usdjpy.usdjpyCaveatPreserved "USDJPY caveat"
Assert-False $usdjpy.usdJpyCaveatWeakened "USDJPY caveat weakened"
if ($usdjpy.normalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.executionTradableSymbol -ne "USDJPY" -or $usdjpy.securityID -ne "4004" -or $usdjpy.securityIDSource -ne "8") {
    Fail "USDJPY caveat values changed"
}

$forbidden = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r002-forbidden-actions-audit.json")
foreach ($name in @(
    "lmaxCalled",
    "polygonCalled",
    "externalApiCalled",
    "brokerActivated",
    "liveMarketDataRequested",
    "pmsEmsOmsCycleRun",
    "manualNoExternalRun",
    "qubesExecutableRun",
    "pythonCppCudaWorkloadRun",
    "backtestOrSimulationRun",
    "dbMutation",
    "migrationCreatedOrApplied",
    "orderRouteSubmissionFillExecutionReportCreated",
    "ledgerCommit",
    "productionLedgerCommit",
    "tradingStateMutation",
    "contractSizeUnitScaleInvented",
    "costsFeesCommissionsApplied",
    "fxConversionApplied",
    "accountCurrencyAggregationApplied",
    "netPnlClaimed",
    "accountingProductionPnlClaimed",
    "ledgerCommitReadinessClaimed",
    "directCrossExecutionAllowed",
    "unsupportedSymbolIncluded",
    "missingFillPairsInvented",
    "legacy06UsedAsFutureCanonical",
    "usdJpyCaveatWeakened"
)) {
    Assert-False $forbidden.$name $name
}

$next = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r002-next-phase-recommendation.json")
if ($next.decision -ne "SandboxGrossRoundTripPnlPreviewV0PartialUnitScaleMissing") { Fail "next phase decision mismatch" }
foreach ($blocked in @("NetPnl", "AccountingPnl", "ProductionPnl", "LedgerCommit")) {
    if ($next.doNotProceedTo -notcontains $blocked) { Fail "Missing do-not-proceed target $blocked" }
}

$evidence = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r002-build-test-validator-evidence.json")
if ($evidence.build.result -ne "Passed") { Fail "build evidence missing/pending" }
if ($evidence.focusedTests.result -ne "Passed" -or $evidence.focusedTests.passed -ne 3 -or $evidence.focusedTests.failed -ne 0) { Fail "focused tests missing/pending" }
if ($evidence.focusedStaticChecks.result -ne "Passed") { Fail "static check evidence missing/pending" }
if ($evidence.validator.result -ne "Passed") { Fail "validator evidence missing/pending" }

Write-Output "PNL_PREVIEW_R002_GATE_PASS_UNIT_SCALE_BINDING_PARTIAL_NO_MUTATION"
