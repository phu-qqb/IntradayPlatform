param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "PNL_PREVIEW_R001_GATE_FAIL: $Message"
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
    "phase-pnl-preview-r001-summary.md",
    "phase-pnl-preview-r001-operator-policy-reference.json",
    "phase-pnl-preview-r001-cross-rail-r014-reference.json",
    "phase-pnl-preview-r001-ledger-r004-reference.json",
    "phase-pnl-preview-r001-fill-pairing-results.json",
    "phase-pnl-preview-r001-per-symbol-gross-roundtrip-pnl.json",
    "phase-pnl-preview-r001-aggregate-gross-roundtrip-pnl.json",
    "phase-pnl-preview-r001-price-delta-details.json",
    "phase-pnl-preview-r001-unit-scale-evidence.json",
    "phase-pnl-preview-r001-boundary-and-blockers.json",
    "phase-pnl-preview-r001-decision.json",
    "phase-pnl-preview-r001-no-external-audit.json",
    "phase-pnl-preview-r001-no-execution-audit.json",
    "phase-pnl-preview-r001-no-db-mutation-audit.json",
    "phase-pnl-preview-r001-no-order-fill-route-audit.json",
    "phase-pnl-preview-r001-no-ledger-state-mutation-audit.json",
    "phase-pnl-preview-r001-not-net-pnl-audit.json",
    "phase-pnl-preview-r001-not-accounting-pnl-audit.json",
    "phase-pnl-preview-r001-not-production-pnl-audit.json",
    "phase-pnl-preview-r001-canonical-timing-preservation.json",
    "phase-pnl-preview-r001-direct-cross-exclusion-preservation.json",
    "phase-pnl-preview-r001-usdjpy-caveat-preservation.json",
    "phase-pnl-preview-r001-forbidden-actions-audit.json",
    "phase-pnl-preview-r001-next-phase-recommendation.json",
    "phase-pnl-preview-r001-build-test-validator-evidence.json"
)

foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $name))) {
        Fail "Missing required artifact $name"
    }
}

$policy = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r001-operator-policy-reference.json")
Assert-True $policy.operatorPolicyCompleted "operator policy completed"
if ($policy.policyContract -ne "SandboxGrossRoundTripPnlPreviewV0") { Fail "policy contract mismatch" }
if ($policy.allowedCeiling -ne "SandboxGrossRoundTripPnlPreviewV0Ready") { Fail "operator policy ceiling mismatch" }
Assert-False $policy.policyDecisions.netPnlReady "net pnl policy"
Assert-False $policy.policyDecisions.accountCurrencyReady "account currency policy"
Assert-False $policy.policyDecisions.fxConversionReady "FX policy"
Assert-False $policy.policyDecisions.paperLedgerCommitAllowed "paper ledger commit policy"
Assert-False $policy.policyDecisions.productionLedgerCommitAllowed "production ledger commit policy"
Assert-False $policy.policyDecisions.tradingStateMutationAllowed "trading state mutation policy"
Assert-False $policy.policyDecisions.productionPnlAllowed "production pnl policy"
Assert-False $policy.policyDecisions.accountingPnlAllowed "accounting pnl policy"

$crossRail = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r001-cross-rail-r014-reference.json")
Assert-True $crossRail.crossRailR014Passed "cross rail R014 passed"
Assert-True $crossRail.orderFillReconciliation.passed "order fill reconciliation"
Assert-True $crossRail.flattenReconciliation.passed "flatten reconciliation"
Assert-True $crossRail.sandboxOnly "sandbox only"
Assert-False $crossRail.productionFill "production fill"
Assert-True $crossRail.notProductionPnl "not production pnl"
Assert-False $crossRail.newReportsFillsOrdersCreatedByR001 "new reports/fills/orders"
foreach ($symbol in @("AUDUSD", "EURUSD", "GBPUSD")) {
    if ([decimal]$crossRail.residuals.$symbol -ne 0) { Fail "$symbol residual must be zero" }
}
if (@($crossRail.breaks).Count -ne 0) { Fail "breaks must be empty" }

$ledger = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r001-ledger-r004-reference.json")
if ($ledger.ledgerR004Decision -ne "CrossRailPmsLinkedSandboxLedgerPreviewReadyWithCompletePriceDeltaOnly") { Fail "R004 decision mismatch" }
if ($ledger.previewLineCount -ne 6 -or $ledger.openFillCount -ne 3 -or $ledger.flattenFillCount -ne 3) { Fail "R004 fill counts mismatch" }

$pairs = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r001-fill-pairing-results.json")
Assert-True $pairs.allPairsComplete "all fill pairs complete"
Assert-False $pairs.missingFillPairsInvented "missing fill pairs invented"
foreach ($symbol in @("AUDUSD", "EURUSD", "GBPUSD")) {
    $pair = @($pairs.pairs | Where-Object { $_.symbol -eq $symbol })[0]
    if ($null -eq $pair) { Fail "Missing pair $symbol" }
    Assert-True $pair.oppositeSides "$symbol opposite sides"
    Assert-True $pair.quantitiesMatch "$symbol quantities match"
    Assert-True $pair.bothPricesPresent "$symbol prices present"
    if ($pair.quoteCurrency -ne "USD") { Fail "$symbol quote currency must be USD" }
    if ([decimal]$pair.residual -ne 0) { Fail "$symbol residual must be zero" }
    Assert-True $pair.complete "$symbol complete"
}

$perSymbol = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r001-per-symbol-gross-roundtrip-pnl.json")
Assert-True $perSymbol.previewOnly "preview only"
Assert-True $perSymbol.grossOnly "gross only"
Assert-True $perSymbol.quoteCurrencyOnly "quote currency only"
Assert-True $perSymbol.closedRoundTripOnly "closed round-trip only"
Assert-True $perSymbol.allRowsQuoteCurrencyUsd "all rows USD"
Assert-False $perSymbol.absoluteGrossPnlAmountsComputed "absolute gross pnl computed"
foreach ($row in $perSymbol.rows) {
    if ($row.pnlCurrency -ne "USD") { Fail "$($row.symbol) PnL currency must be USD" }
    if ($null -ne $row.contractSizeOrUnitScale) { Fail "$($row.symbol) unit scale must not be invented" }
    if ($null -ne $row.grossRoundTripPnlQuoteCurrency) { Fail "$($row.symbol) absolute gross pnl must not be computed without scale" }
    Assert-False $row.costsApplied "$($row.symbol) costs"
    Assert-False $row.feesApplied "$($row.symbol) fees"
    Assert-False $row.commissionsApplied "$($row.symbol) commissions"
    Assert-False $row.fxConversionApplied "$($row.symbol) FX conversion"
    Assert-False $row.accountCurrencyApplied "$($row.symbol) account currency"
    Assert-False $row.netPnlReady "$($row.symbol) net pnl"
    Assert-False $row.accountingPnlReady "$($row.symbol) accounting pnl"
    Assert-False $row.productionPnlReady "$($row.symbol) production pnl"
    Assert-False $row.ledgerCommitAllowed "$($row.symbol) ledger commit"
    Assert-True $row.sandboxOnly "$($row.symbol) sandbox"
    Assert-True $row.notProductionPnl "$($row.symbol) not production pnl"
    if ($row.warnings -notcontains "UnitScaleMissing") { Fail "$($row.symbol) must warn UnitScaleMissing" }
}
if ([decimal](@($perSymbol.rows | Where-Object { $_.symbol -eq "AUDUSD" })[0].priceDelta) -ne -0.00002) { Fail "AUDUSD price delta mismatch" }
if ([decimal](@($perSymbol.rows | Where-Object { $_.symbol -eq "EURUSD" })[0].priceDelta) -ne -0.00005) { Fail "EURUSD price delta mismatch" }
if ([decimal](@($perSymbol.rows | Where-Object { $_.symbol -eq "GBPUSD" })[0].priceDelta) -ne -0.00010) { Fail "GBPUSD price delta mismatch" }

$aggregate = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r001-aggregate-gross-roundtrip-pnl.json")
Assert-False $aggregate.aggregateAvailable "aggregate available"
Assert-True $aggregate.partialAggregateOnly "partial aggregate only"
Assert-False $aggregate.unsupportedSymbolsIncluded "unsupported symbols"
Assert-False $aggregate.directCrossesIncluded "direct crosses"
Assert-False $aggregate.productionFillsIncluded "production fills"
if ($null -ne $aggregate.grossRoundTripPnlQuoteCurrency) { Fail "aggregate absolute gross pnl must be null" }
if ([decimal]$aggregate.aggregatePriceDeltaSum -ne -0.00017) { Fail "aggregate price delta sum mismatch" }
if ($aggregate.diagnostics -notcontains "UnitScaleMissing") { Fail "aggregate must warn UnitScaleMissing" }
Assert-False $aggregate.costsApplied "aggregate costs"
Assert-False $aggregate.fxConversionApplied "aggregate FX"
Assert-False $aggregate.accountCurrencyAggregationApplied "aggregate account currency"
Assert-False $aggregate.netPnlReady "aggregate net pnl"
Assert-False $aggregate.accountingPnlReady "aggregate accounting pnl"
Assert-False $aggregate.productionPnlReady "aggregate production pnl"

$details = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r001-price-delta-details.json")
Assert-True $details.priceDeltaOnlyBecauseUnitScaleMissing "price delta only due unit scale"

$scale = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r001-unit-scale-evidence.json")
Assert-True $scale.quantityPresent "quantity present"
Assert-False $scale.contractSizeOrUnitScalePresent "contract scale present"
if ($null -ne $scale.contractSizeOrUnitScale) { Fail "contract scale must remain null" }
Assert-False $scale.unitScaleInvented "unit scale invented"
Assert-False $scale.absoluteGrossRoundTripPnlQuoteCurrencyComputable "absolute gross pnl computable"
if ($scale.warning -ne "UnitScaleMissing") { Fail "unit scale warning missing" }

$boundary = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r001-boundary-and-blockers.json")
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
foreach ($blocker in @("MissingMarkPrices", "MissingCostSpreadCommissionModel", "MissingFxConversion", "MissingAccountCurrency", "MissingPositionCostBasisModel", "MissingAttributionPolicy", "MissingAccountId", "MissingPortfolioId", "MissingStrategyId", "MissingSourceExecutionIntentId", "QubesRunIdNotPmsApprovedEconomicOutput", "CommitSafeIdempotencyPolicyIncomplete")) {
    if ($boundary.blockersPreserved -notcontains $blocker) { Fail "Missing preserved blocker $blocker" }
}
if ($boundary.unitScaleBlocker -ne "UnitScaleMissing") { Fail "unit scale blocker missing" }

$decision = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r001-decision.json")
if ($decision.decision -ne "SandboxGrossRoundTripPnlPreviewV0Partial") { Fail "decision mismatch" }
Assert-False $decision.netPnlReady "decision net pnl"
Assert-False $decision.accountingPnlReady "decision accounting pnl"
Assert-False $decision.productionPnlReady "decision production pnl"
Assert-False $decision.ledgerCommitReady "decision ledger commit"
Assert-False $decision.tradingStateMutation "decision trading state mutation"

foreach ($auditFile in @(
    "phase-pnl-preview-r001-no-external-audit.json",
    "phase-pnl-preview-r001-no-execution-audit.json",
    "phase-pnl-preview-r001-no-db-mutation-audit.json",
    "phase-pnl-preview-r001-no-ledger-state-mutation-audit.json",
    "phase-pnl-preview-r001-not-net-pnl-audit.json",
    "phase-pnl-preview-r001-not-accounting-pnl-audit.json",
    "phase-pnl-preview-r001-not-production-pnl-audit.json"
)) {
    $audit = Read-Json (Join-Path $artifactDir $auditFile)
    foreach ($prop in $audit.PSObject.Properties.Name) {
        if ($prop -ne "phase" -and $prop -ne "auditResult" -and $prop -notmatch "Preserved$" -and $audit.$prop -eq $true) {
            Fail "$auditFile has true forbidden property $prop"
        }
    }
}

$noOrder = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r001-no-order-fill-route-audit.json")
if ($noOrder.ordersCreated -ne 0 -or $noOrder.routesCreated -ne 0 -or $noOrder.submissionsCreated -ne 0 -or $noOrder.newFillsCreated -ne 0 -or $noOrder.newExecutionReportsCreated -ne 0 -or $noOrder.executableSchedulesCreated -ne 0) {
    Fail "order/fill/route artifacts created"
}
Assert-True $noOrder.sourceFillsReusedOnly "source fills reused only"

$timing = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r001-canonical-timing-preservation.json")
Assert-True $timing.futureTimestampsUseCanonicalQuarterHour "canonical quarter-hour"
Assert-False $timing.legacy06UsedAsFutureCanonical "legacy :06 future canonical"

$direct = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r001-direct-cross-exclusion-preservation.json")
Assert-False $direct.directCrossExecutionAllowed "direct-cross execution"
Assert-False $direct.directCrossesIncluded "direct-cross included"
Assert-False $direct.pnlLedgerCreatesDirectCrossExposureFromRawQubesSignals "direct-cross exposure"

$usdjpy = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r001-usdjpy-caveat-preservation.json")
Assert-True $usdjpy.usdjpyCaveatPreserved "USDJPY caveat"
Assert-False $usdjpy.usdJpyCaveatWeakened "USDJPY caveat weakened"
if ($usdjpy.normalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.executionTradableSymbol -ne "USDJPY" -or $usdjpy.securityID -ne "4004" -or $usdjpy.securityIDSource -ne "8") {
    Fail "USDJPY caveat values changed"
}

$forbidden = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r001-forbidden-actions-audit.json")
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
    "markPricesInvented",
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

$next = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r001-next-phase-recommendation.json")
if ($next.decision -ne "SandboxGrossRoundTripPnlPreviewV0Partial") { Fail "next phase decision mismatch" }
foreach ($blocked in @("NetPnl", "AccountingPnl", "ProductionPnl", "LedgerCommit")) {
    if ($next.doNotProceedTo -notcontains $blocked) { Fail "Missing do-not-proceed target $blocked" }
}

$evidence = Read-Json (Join-Path $artifactDir "phase-pnl-preview-r001-build-test-validator-evidence.json")
if ($evidence.build.result -ne "Passed") { Fail "build evidence missing/pending" }
if ($evidence.focusedTests.result -ne "Passed" -or $evidence.focusedTests.passed -ne 4 -or $evidence.focusedTests.failed -ne 0) { Fail "focused tests missing/pending" }
if ($evidence.focusedStaticChecks.result -ne "Passed") { Fail "static check evidence missing/pending" }

Write-Output "PNL_PREVIEW_R001_GATE_PASS_SANDBOX_GROSS_ROUNDTRIP_PNL_PREVIEW_READY_NO_MUTATION"
