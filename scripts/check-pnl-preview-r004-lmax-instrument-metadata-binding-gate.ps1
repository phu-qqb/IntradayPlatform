param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "PNL_PREVIEW_R004_GATE_FAIL: $Message"
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

function Assert-DecimalEqual($Actual, [decimal]$Expected, [string]$Name) {
    if ([decimal]$Actual -ne $Expected) {
        Fail "$Name expected $Expected but found $Actual"
    }
}

$artifactDir = Join-Path $Root "artifacts/readiness/pnl-preview"
$required = @(
    "phase-pnl-preview-r004-summary.md",
    "phase-pnl-preview-r004-r003-reference.json",
    "phase-pnl-preview-r004-lmax-instrument-metadata-reference.json",
    "phase-pnl-preview-r004-instrument-metadata-intake.json",
    "phase-pnl-preview-r004-unit-scale-binding-results.json",
    "phase-pnl-preview-r004-per-symbol-gross-roundtrip-pnl-amounts.json",
    "phase-pnl-preview-r004-aggregate-gross-roundtrip-pnl-amount.json",
    "phase-pnl-preview-r004-boundary-and-blockers.json",
    "phase-pnl-preview-r004-decision.json",
    "phase-pnl-preview-r004-no-external-audit.json",
    "phase-pnl-preview-r004-no-execution-audit.json",
    "phase-pnl-preview-r004-no-db-mutation-audit.json",
    "phase-pnl-preview-r004-no-order-fill-route-audit.json",
    "phase-pnl-preview-r004-no-ledger-state-mutation-audit.json",
    "phase-pnl-preview-r004-not-net-pnl-audit.json",
    "phase-pnl-preview-r004-not-accounting-pnl-audit.json",
    "phase-pnl-preview-r004-not-production-pnl-audit.json",
    "phase-pnl-preview-r004-canonical-timing-preservation.json",
    "phase-pnl-preview-r004-direct-cross-exclusion-preservation.json",
    "phase-pnl-preview-r004-usdjpy-caveat-preservation.json",
    "phase-pnl-preview-r004-forbidden-actions-audit.json",
    "phase-pnl-preview-r004-next-phase-recommendation.json",
    "phase-pnl-preview-r004-build-test-validator-evidence.json"
)

foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $name))) {
        Fail "Missing required artifact $name"
    }
}

$r003 = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r004-r003-reference.json")
if ($r003.r003Decision -ne "SandboxGrossRoundTripPnlPreviewV0StillPartialUnitScaleMissing") { Fail "R003 decision mismatch" }
if ($r003.r003BoundSymbols -notcontains "EURUSD") { Fail "R003 EURUSD bound symbol missing" }
foreach ($symbol in @("AUDUSD", "GBPUSD")) {
    if ($r003.r003MissingSymbols -notcontains $symbol) { Fail "R003 missing symbol $symbol absent" }
}
Assert-False $r003.newOrdersRoutesSubmissionsFillsReportsCreatedByR004 "R004 new order/report creation"

$metadataRef = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r004-lmax-instrument-metadata-reference.json")
if ($metadataRef.metadataSourceType -ne "OperatorProvidedStaticLocalCsv") { Fail "metadata source type mismatch" }
Assert-False $metadataRef.externalApiCalled "metadata external API"
Assert-False $metadataRef.lmaxCalled "metadata LMAX call"
Assert-False $metadataRef.brokerActivated "metadata broker activation"
Assert-True $metadataRef.csvReadOnly "metadata read-only"
foreach ($row in @("AUD/USD", "EUR/USD", "GBP/USD")) {
    if ($metadataRef.requiredRows -notcontains $row) { Fail "metadata required row missing $row" }
}

$intake = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r004-instrument-metadata-intake.json")
Assert-True $intake.metadataFileFound "metadata file found"
Assert-True $intake.intakeReadOnly "intake read-only"
Assert-True $intake.allRequiredRowsFound "all required rows"
Assert-True $intake.allRequiredFieldsPresent "all required fields"
Assert-False $intake.contractMultiplierInvented "contract multiplier invented"
Assert-False $intake.unitScaleInvented "unit scale invented"
foreach ($expected in @(
    @{ Symbol = "AUDUSD"; Instrument = "AUD/USD"; Id = "4007"; Threshold = 3790 },
    @{ Symbol = "EURUSD"; Instrument = "EUR/USD"; Id = "4001"; Threshold = 2500 },
    @{ Symbol = "GBPUSD"; Instrument = "GBP/USD"; Id = "4002"; Threshold = 2000 }
)) {
    $row = @($intake.rows | Where-Object { $_.symbol -eq $expected.Symbol })[0]
    if ($null -eq $row) { Fail "missing intake row $($expected.Symbol)" }
    if ($row.instrumentName -ne $expected.Instrument) { Fail "$($expected.Symbol) instrument mismatch" }
    if ($row.lmaxId -ne $expected.Id) { Fail "$($expected.Symbol) LMAX ID mismatch" }
    if ($row.lmaxSymbol -ne $expected.Instrument) { Fail "$($expected.Symbol) LMAX symbol mismatch" }
    Assert-DecimalEqual $row.contractMultiplier 10000 "$($expected.Symbol) multiplier"
    Assert-DecimalEqual $row.tickSize 0.00001 "$($expected.Symbol) tick size"
    Assert-DecimalEqual $row.tickValue 0.1 "$($expected.Symbol) tick value"
    Assert-DecimalEqual $row.minOrderSize 0.1 "$($expected.Symbol) min order size"
    if ($row.quotedCurrency -ne "USD") { Fail "$($expected.Symbol) quoted currency must be USD" }
    Assert-True $row.rowFound "$($expected.Symbol) row found"
    Assert-True $row.requiredFieldsPresent "$($expected.Symbol) fields present"
    if ($row.evidenceConfidence -ne "High") { Fail "$($expected.Symbol) confidence mismatch" }
}
if (@($intake.conflicts).Count -ne 0) { Fail "metadata conflicts present" }

$binding = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r004-unit-scale-binding-results.json")
if ($binding.quantityUnit -ne "LMAXVenueOrderQtyContractUnit") { Fail "quantity unit mismatch" }
Assert-True $binding.allSymbolsBound "all symbols bound"
Assert-False $binding.unitScaleInvented "binding unit scale invented"
Assert-False $binding.contractMultiplierInvented "binding contract multiplier invented"
foreach ($symbol in @("AUDUSD", "EURUSD", "GBPUSD")) {
    if ($binding.boundSymbols -notcontains $symbol) { Fail "$symbol missing from bound symbols" }
    $row = @($binding.bindingResults | Where-Object { $_.symbol -eq $symbol })[0]
    if ($row.bindingStatus -ne "UnitScaleBound") { Fail "$symbol binding status mismatch" }
    Assert-DecimalEqual $row.contractSizeOrUnitScale 10000 "$symbol bound unit scale"
    Assert-DecimalEqual $row.minOrderSize 0.1 "$symbol min order size"
    if ($row.quotedCurrency -ne "USD") { Fail "$symbol quoted currency mismatch" }
    if ($row.evidenceConfidence -ne "High") { Fail "$symbol binding confidence mismatch" }
    if (@($row.warnings).Count -ne 0) { Fail "$symbol binding warnings present" }
}
if (@($binding.missingSymbols).Count -ne 0) { Fail "missing symbols present after R004 binding" }
if (@($binding.conflicts).Count -ne 0) { Fail "unit scale conflicts present" }

$amounts = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r004-per-symbol-gross-roundtrip-pnl-amounts.json")
Assert-True $amounts.previewOnly "amount preview only"
Assert-True $amounts.sandboxOnly "amount sandbox only"
Assert-True $amounts.grossOnly "amount gross"
Assert-True $amounts.quoteCurrencyOnly "amount quote"
Assert-True $amounts.closedRoundTripOnly "amount closed"
Assert-True $amounts.absoluteGrossPnlAmountsComputedForAllSymbols "amounts for all symbols"
Assert-False $amounts.costsApplied "amount costs"
Assert-False $amounts.feesApplied "amount fees"
Assert-False $amounts.commissionsApplied "amount commissions"
Assert-False $amounts.fxConversionApplied "amount FX"
Assert-False $amounts.accountCurrencyAggregationApplied "amount account currency"
Assert-False $amounts.netPnlReady "amount net pnl"
Assert-False $amounts.accountingPnlReady "amount accounting pnl"
Assert-False $amounts.productionPnlReady "amount production pnl"
Assert-False $amounts.ledgerCommitAllowed "amount ledger commit"
Assert-False $amounts.unitScaleInvented "amount unit scale invented"

$expectedAmounts = @{
    "AUDUSD" = @{ OpenSide = "SELL"; FlattenSide = "BUY"; OpenPrice = [decimal]"0.71659"; FlattenPrice = [decimal]"0.71661"; PriceDelta = [decimal]"-0.00002"; Pnl = [decimal]"-0.02" }
    "EURUSD" = @{ OpenSide = "SELL"; FlattenSide = "BUY"; OpenPrice = [decimal]"1.16223"; FlattenPrice = [decimal]"1.16228"; PriceDelta = [decimal]"-0.00005"; Pnl = [decimal]"-0.05" }
    "GBPUSD" = @{ OpenSide = "BUY"; FlattenSide = "SELL"; OpenPrice = [decimal]"1.34457"; FlattenPrice = [decimal]"1.34447"; PriceDelta = [decimal]"-0.00010"; Pnl = [decimal]"-0.10" }
}
foreach ($symbol in $expectedAmounts.Keys) {
    $row = @($amounts.rows | Where-Object { $_.symbol -eq $symbol })[0]
    if ($null -eq $row) { Fail "missing amount row $symbol" }
    if ($row.openSide -ne $expectedAmounts[$symbol].OpenSide) { Fail "$symbol open side mismatch" }
    if ($row.flattenSide -ne $expectedAmounts[$symbol].FlattenSide) { Fail "$symbol flatten side mismatch" }
    Assert-DecimalEqual $row.openPrice $expectedAmounts[$symbol].OpenPrice "$symbol open price"
    Assert-DecimalEqual $row.flattenPrice $expectedAmounts[$symbol].FlattenPrice "$symbol flatten price"
    Assert-DecimalEqual $row.quantity 0.1 "$symbol quantity"
    Assert-DecimalEqual $row.contractSizeOrUnitScale 10000 "$symbol amount scale"
    Assert-DecimalEqual $row.priceDelta $expectedAmounts[$symbol].PriceDelta "$symbol price delta"
    Assert-DecimalEqual $row.grossRoundTripPnlQuoteCurrency $expectedAmounts[$symbol].Pnl "$symbol gross pnl"
    if ($row.pnlCurrency -ne "USD" -or $row.quoteCurrency -ne "USD") { Fail "$symbol currency mismatch" }
    Assert-False $row.costsApplied "$symbol costs"
    Assert-False $row.feesApplied "$symbol fees"
    Assert-False $row.commissionsApplied "$symbol commissions"
    Assert-False $row.fxConversionApplied "$symbol FX"
    Assert-False $row.accountCurrencyApplied "$symbol account currency"
    Assert-False $row.netPnlReady "$symbol net pnl"
    Assert-False $row.accountingPnlReady "$symbol accounting pnl"
    Assert-False $row.productionPnlReady "$symbol production pnl"
    Assert-False $row.ledgerCommitAllowed "$symbol ledger commit"
    Assert-True $row.sandboxOnly "$symbol sandbox"
    Assert-True $row.notProductionPnl "$symbol not production pnl"
    if (@($row.warnings).Count -ne 0) { Fail "$symbol warnings present" }
}

$aggregate = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r004-aggregate-gross-roundtrip-pnl-amount.json")
Assert-True $aggregate.aggregateAvailable "aggregate available"
Assert-True $aggregate.fullThreeSymbolAggregateAvailable "full aggregate"
Assert-False $aggregate.partialAggregateOnly "partial aggregate"
Assert-DecimalEqual $aggregate.grossRoundTripPnlQuoteCurrency -0.17 "aggregate amount"
if ($aggregate.pnlCurrency -ne "USD") { Fail "aggregate currency mismatch" }
foreach ($symbol in @("AUDUSD", "EURUSD", "GBPUSD")) {
    if ($aggregate.symbolsIncluded -notcontains $symbol) { Fail "$symbol missing from aggregate included symbols" }
    if ($aggregate.computedSymbols -notcontains $symbol) { Fail "$symbol missing from aggregate computed symbols" }
}
if (@($aggregate.missingSymbols).Count -ne 0) { Fail "aggregate missing symbols present" }
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
Assert-False $aggregate.tradingStateMutationAllowed "aggregate trading state"

$boundary = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r004-boundary-and-blockers.json")
Assert-True $boundary.previewOnly "boundary preview"
Assert-True $boundary.sandboxOnly "boundary sandbox"
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
foreach ($closed in @("AUDUSDUnitScaleMissing", "GBPUSDUnitScaleMissing")) {
    if ($boundary.unitScaleBlockersClosed -notcontains $closed) { Fail "Missing closed unit-scale blocker $closed" }
}
if (@($boundary.unitScaleBlockersRemaining).Count -ne 0) { Fail "unit-scale blockers should be closed for R004 symbols" }

$decision = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r004-decision.json")
if ($decision.decision -ne "SandboxGrossRoundTripPnlPreviewV0FullThreeSymbolAmountsComputed") { Fail "decision mismatch" }
Assert-True $decision.fullThreeSymbolAmountsComputed "decision full three-symbol amounts"
Assert-DecimalEqual $decision.aggregateGrossRoundTripPnlQuoteCurrency -0.17 "decision aggregate"
if ($decision.pnlCurrency -ne "USD") { Fail "decision PnL currency mismatch" }
Assert-True $decision.instrumentMetadataBound "instrument metadata bound"
Assert-False $decision.instrumentMetadataConflicts "instrument metadata conflicts"
Assert-False $decision.contractMultiplierInvented "decision contract multiplier invented"
Assert-False $decision.unitScaleInvented "decision unit scale invented"
Assert-False $decision.netPnlReady "decision net pnl"
Assert-False $decision.accountingPnlReady "decision accounting pnl"
Assert-False $decision.productionPnlReady "decision production pnl"
Assert-False $decision.ledgerCommitReady "decision ledger commit"
Assert-False $decision.tradingStateMutation "decision trading state"

foreach ($auditFile in @(
    "phase-pnl-preview-r004-no-external-audit.json",
    "phase-pnl-preview-r004-no-execution-audit.json",
    "phase-pnl-preview-r004-no-db-mutation-audit.json",
    "phase-pnl-preview-r004-no-ledger-state-mutation-audit.json",
    "phase-pnl-preview-r004-not-net-pnl-audit.json",
    "phase-pnl-preview-r004-not-accounting-pnl-audit.json",
    "phase-pnl-preview-r004-not-production-pnl-audit.json"
)) {
    $audit = Read-Json (Join-Path $artifactDir $auditFile)
    foreach ($prop in $audit.PSObject.Properties.Name) {
        if ($prop -ne "phase" -and $prop -ne "auditResult" -and $prop -notmatch "Preserved$" -and $audit.$prop -eq $true) {
            Fail "$auditFile has true forbidden property $prop"
        }
    }
}

$noOrder = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r004-no-order-fill-route-audit.json")
if ($noOrder.ordersCreated -ne 0 -or $noOrder.routesCreated -ne 0 -or $noOrder.submissionsCreated -ne 0 -or $noOrder.newFillsCreated -ne 0 -or $noOrder.newExecutionReportsCreated -ne 0 -or $noOrder.executableSchedulesCreated -ne 0) {
    Fail "order/fill/route artifacts created"
}
Assert-True $noOrder.sourceFillsReusedOnly "source fills reused only"

$timing = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r004-canonical-timing-preservation.json")
Assert-True $timing.futureTimestampsUseCanonicalQuarterHour "canonical quarter-hour"
Assert-False $timing.legacy06UsedAsFutureCanonical "legacy :06 future canonical"

$direct = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r004-direct-cross-exclusion-preservation.json")
Assert-False $direct.directCrossExecutionAllowed "direct-cross execution"
Assert-False $direct.directCrossesIncluded "direct-cross included"
Assert-False $direct.pnlLedgerCreatesDirectCrossExposureFromRawQubesSignals "direct-cross exposure"

$usdjpy = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r004-usdjpy-caveat-preservation.json")
Assert-True $usdjpy.usdjpyCaveatPreserved "USDJPY caveat"
Assert-False $usdjpy.usdJpyCaveatWeakened "USDJPY caveat weakened"
if ($usdjpy.normalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.executionTradableSymbol -ne "USDJPY" -or $usdjpy.securityID -ne "4004" -or $usdjpy.securityIDSource -ne "8") {
    Fail "USDJPY caveat values changed"
}

$forbidden = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r004-forbidden-actions-audit.json")
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
    "contractMultiplierUnitScaleInvented",
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

$next = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r004-next-phase-recommendation.json")
if ($next.decision -ne "SandboxGrossRoundTripPnlPreviewV0FullThreeSymbolAmountsComputed") { Fail "next phase decision mismatch" }
foreach ($blocked in @("NetPnl", "AccountingPnl", "ProductionPnl", "LedgerCommit", "ProductionLive")) {
    if ($next.doNotProceedTo -notcontains $blocked) { Fail "Missing do-not-proceed target $blocked" }
}

$evidence = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r004-build-test-validator-evidence.json")
if ($evidence.build.result -ne "Passed") { Fail "build evidence missing/pending" }
if ($evidence.focusedStaticChecks.result -ne "Passed") { Fail "static check evidence missing/pending" }
if ($evidence.validator.result -ne "Passed") { Fail "validator evidence missing/pending" }
if ($evidence.focusedTests.result -ne "NotApplicable") { Fail "focused tests should be NotApplicable for static R004" }

Write-Output "PNL_PREVIEW_R004_GATE_PASS_LMAX_INSTRUMENT_METADATA_BINDING_FULL_GROSS_PREVIEW_NO_MUTATION"

