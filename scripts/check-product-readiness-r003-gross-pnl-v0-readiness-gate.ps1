param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "PRODUCT_READINESS_R003_GATE_FAIL: $Message"
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

$artifactDir = Join-Path $Root "artifacts/readiness/product-readiness"
$required = @(
    "phase-product-readiness-r003-summary.md",
    "phase-product-readiness-r003-r002-reference.json",
    "phase-product-readiness-r003-pnl-preview-r004-reference.json",
    "phase-product-readiness-r003-readiness-by-layer.json",
    "phase-product-readiness-r003-gross-pnl-v0-summary.json",
    "phase-product-readiness-r003-updated-economic-warnings.json",
    "phase-product-readiness-r003-production-live-blockers.json",
    "phase-product-readiness-r003-next-large-work-packages.json",
    "phase-product-readiness-r003-stop-micro-step-drift-policy.json",
    "phase-product-readiness-r003-roadmap.md",
    "phase-product-readiness-r003-roadmap.json",
    "phase-product-readiness-r003-decision.json",
    "phase-product-readiness-r003-no-external-audit.json",
    "phase-product-readiness-r003-no-execution-audit.json",
    "phase-product-readiness-r003-no-db-mutation-audit.json",
    "phase-product-readiness-r003-no-order-fill-route-audit.json",
    "phase-product-readiness-r003-no-ledger-state-mutation-audit.json",
    "phase-product-readiness-r003-canonical-timing-preservation.json",
    "phase-product-readiness-r003-direct-cross-exclusion-preservation.json",
    "phase-product-readiness-r003-usdjpy-caveat-preservation.json",
    "phase-product-readiness-r003-forbidden-actions-audit.json",
    "phase-product-readiness-r003-next-phase-recommendation.json",
    "phase-product-readiness-r003-build-test-validator-evidence.json"
)

foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $name))) {
        Fail "Missing required artifact $name"
    }
}

$r002 = Read-Json (Join-Path $artifactDir "phase-product-readiness-r003-r002-reference.json")
Assert-False $r002.localR002ArtifactFound "local R002 artifact found"
Assert-True $r002.r003DoesNotInventR002State "R003 does not invent R002 state"

$r004 = Read-Json (Join-Path $artifactDir "phase-product-readiness-r003-pnl-preview-r004-reference.json")
if ($r004.r004Decision -ne "SandboxGrossRoundTripPnlPreviewV0FullThreeSymbolAmountsComputed") { Fail "R004 decision mismatch" }
Assert-DecimalEqual $r004.grossQuoteCurrencyPnl.AUDUSD -0.02 "R004 AUDUSD"
Assert-DecimalEqual $r004.grossQuoteCurrencyPnl.EURUSD -0.05 "R004 EURUSD"
Assert-DecimalEqual $r004.grossQuoteCurrencyPnl.GBPUSD -0.10 "R004 GBPUSD"
Assert-DecimalEqual $r004.grossQuoteCurrencyPnl.aggregate -0.17 "R004 aggregate"
if ($r004.grossQuoteCurrencyPnl.currency -ne "USD") { Fail "R004 currency mismatch" }
Assert-False $r004.netPnlReady "R004 net pnl"
Assert-False $r004.accountingPnlReady "R004 accounting pnl"
Assert-False $r004.productionPnlReady "R004 production pnl"
Assert-False $r004.ledgerCommitReady "R004 ledger commit"

$readiness = Read-Json (Join-Path $artifactDir "phase-product-readiness-r003-readiness-by-layer.json")
$expectedStatuses = @{
    "Sandbox order lifecycle" = "Ready"
    "Cross-rail PMS->R009 sandbox" = "Ready"
    "Sandbox reconciliation / flatten" = "Ready"
    "Ledger preview" = "ReadyWithWarnings"
    "Sandbox price-delta preview" = "Ready"
    "Sandbox gross quote-currency PnL preview V0" = "Ready"
    "Full sandbox theoretical PnL" = "Blocked"
    "Net PnL" = "Blocked"
    "Paper accounting PnL" = "Blocked"
    "Paper ledger commit" = "Blocked"
    "Production/live" = "Blocked"
}
foreach ($layerName in $expectedStatuses.Keys) {
    $row = @($readiness.layers | Where-Object { $_.layer -eq $layerName })[0]
    if ($null -eq $row) { Fail "Missing readiness layer $layerName" }
    if ($row.status -ne $expectedStatuses[$layerName]) { Fail "$layerName status expected $($expectedStatuses[$layerName]) but found $($row.status)" }
}

$gross = Read-Json (Join-Path $artifactDir "phase-product-readiness-r003-gross-pnl-v0-summary.json")
Assert-True $gross.sandboxGrossRoundTripPnlPreviewV0Ready "gross V0 ready"
Assert-True $gross.fullThreeSymbolGrossPnlComputed "full three-symbol computed"
Assert-True $gross.grossOnly "gross only"
Assert-True $gross.quoteCurrencyOnly "quote only"
Assert-True $gross.sandboxOnly "sandbox only"
Assert-True $gross.previewOnly "preview only"
Assert-False $gross.costsApplied "costs applied"
Assert-False $gross.feesApplied "fees applied"
Assert-False $gross.commissionsApplied "commissions applied"
Assert-False $gross.fxConversionApplied "FX applied"
Assert-False $gross.accountCurrencyApplied "account currency applied"
Assert-False $gross.netPnlReady "net pnl ready"
Assert-False $gross.accountingPnlReady "accounting pnl ready"
Assert-False $gross.productionPnlReady "production pnl ready"
Assert-False $gross.ledgerCommitAllowed "ledger commit allowed"
Assert-DecimalEqual $gross.aggregateGrossRoundTripPnlQuoteCurrency -0.17 "gross aggregate"
if ($gross.aggregateCurrency -ne "USD") { Fail "gross aggregate currency mismatch" }
foreach ($expected in @(
    @{ Symbol = "AUDUSD"; Amount = [decimal]"-0.02" },
    @{ Symbol = "EURUSD"; Amount = [decimal]"-0.05" },
    @{ Symbol = "GBPUSD"; Amount = [decimal]"-0.10" }
)) {
    $row = @($gross.symbols | Where-Object { $_.symbol -eq $expected.Symbol })[0]
    if ($null -eq $row) { Fail "Missing gross symbol $($expected.Symbol)" }
    Assert-DecimalEqual $row.grossRoundTripPnlQuoteCurrency $expected.Amount "$($expected.Symbol) gross amount"
    if ($row.currency -ne "USD") { Fail "$($expected.Symbol) gross currency mismatch" }
}

$warnings = Read-Json (Join-Path $artifactDir "phase-product-readiness-r003-updated-economic-warnings.json")
foreach ($removed in @("AUDUSDUnitScaleMissing", "GBPUSDUnitScaleMissing", "Full3SymbolGrossPnlUnavailable")) {
    if ($warnings.warningsRemoved -notcontains $removed) { Fail "Removed warning missing $removed" }
}
foreach ($blocker in @("MissingMarkPrices", "MissingCostSpreadCommissionModel", "MissingFxConversion", "MissingAccountCurrency", "MissingPositionCostBasisModel", "MissingAttributionPolicy", "MissingAccountId", "MissingPortfolioId", "MissingStrategyId", "MissingSourceExecutionIntentId", "QubesRunIdNotPmsApprovedEconomicOutput", "CommitSafeIdempotencyPolicyIncomplete")) {
    if ($warnings.blockersPreserved -notcontains $blocker) { Fail "Missing preserved blocker $blocker" }
}
foreach ($marketDataWarning in @("M30Missing", "RowCountsMissing", "DbTickSchemaPartial")) {
    if ($warnings.marketDataWarningsPreserved -notcontains $marketDataWarning) { Fail "Missing market data warning $marketDataWarning" }
}
if ($warnings.marketDataStatus.lmaxMarketdataDbV1 -ne "WITH_WARNINGS" -or $warnings.marketDataStatus.marketdataReadinessV1 -ne "WITH_WARNINGS") { Fail "MarketData WARN misclassified" }
Assert-False $warnings.marketDataStatus.misclassifiedAsPass "MarketData WARN as PASS"

$prod = Read-Json (Join-Path $artifactDir "phase-product-readiness-r003-production-live-blockers.json")
Assert-False $prod.productionLiveReady "production live ready"
Assert-False $prod.productionLiveDiscussionAllowed "production live discussion"
Assert-True $prod.noProductionPromotion "no production promotion"

$packages = Read-Json (Join-Path $artifactDir "phase-product-readiness-r003-next-large-work-packages.json")
Assert-True $packages.largePackagesOnly "large packages only"
foreach ($pkg in @("PMS/Qubes economic identity package", "MarketData pricing/readiness package", "Risk/Cost model package", "Ledger accounting policy package", "Production readiness package")) {
    if (@($packages.packages | Where-Object { $_.package -eq $pkg }).Count -ne 1) { Fail "Missing large package $pkg" }
}

$drift = Read-Json (Join-Path $artifactDir "phase-product-readiness-r003-stop-micro-step-drift-policy.json")
Assert-True $drift.doNotRunFurtherSandboxOrderLifecycleProofsUnlessNewExecutionFunctionalityChanges "stop lifecycle micro proof"
Assert-True $drift.doNotRunFurtherGrossSandboxPnlMicroGatesUnlessNewEvidenceChanges "stop gross PnL micro gates"
Assert-False $drift.microStepRoadmapProduced "micro roadmap produced"

$roadmap = Read-Json (Join-Path $artifactDir "phase-product-readiness-r003-roadmap.json")
if ($roadmap.roadmapType -ne "LargePackagesOnly") { Fail "roadmap type mismatch" }
Assert-False $roadmap.microStepRoadmapProduced "roadmap micro steps"
foreach ($blocked in @("NetPnl", "AccountingPnl", "ProductionPnl", "LedgerCommit", "ProductionLive")) {
    if ($roadmap.doNotProceedTo -notcontains $blocked) { Fail "Roadmap missing blocked target $blocked" }
}

$decision = Read-Json (Join-Path $artifactDir "phase-product-readiness-r003-decision.json")
foreach ($d in @("SandboxProgrammeAcceptedWithGrossPnlV0Ready", "EconomicWarningsPreserved", "ProductionLiveStillBlocked", "NextLargePackagesDefined")) {
    if ($decision.decision -notcontains $d) { Fail "Missing decision $d" }
}
foreach ($classification in @("PRODUCT_READINESS_R003_PASS_SANDBOX_GROSS_PNL_V0_READY", "PRODUCT_READINESS_R003_PASS_ECONOMIC_WARNINGS_UPDATED", "PRODUCT_READINESS_R003_PASS_PRODUCTION_LIVE_BLOCKERS_PRESERVED", "PRODUCT_READINESS_R003_PASS_NO_EXECUTION_NO_MUTATION_GATE")) {
    if ($decision.classifications -notcontains $classification) { Fail "Missing classification $classification" }
}
Assert-True $decision.sandboxStrategyReadinessAccepted "sandbox strategy readiness"
Assert-True $decision.sandboxGrossRoundTripPnlPreviewV0Ready "sandbox gross V0"
Assert-True $decision.fullThreeSymbolGrossPnlComputed "full three symbol"
Assert-DecimalEqual $decision.aggregateGrossRoundTripPnlQuoteCurrency -0.17 "decision aggregate"
Assert-False $decision.netPnlReady "decision net pnl"
Assert-False $decision.accountingPnlReady "decision accounting pnl"
Assert-False $decision.productionPnlReady "decision production pnl"
Assert-False $decision.ledgerCommitReady "decision ledger commit"
Assert-False $decision.productionLiveReady "decision production live"
Assert-False $decision.tradingStateMutation "decision trading state"

foreach ($auditFile in @(
    "phase-product-readiness-r003-no-external-audit.json",
    "phase-product-readiness-r003-no-execution-audit.json",
    "phase-product-readiness-r003-no-db-mutation-audit.json",
    "phase-product-readiness-r003-no-ledger-state-mutation-audit.json"
)) {
    $audit = Read-Json (Join-Path $artifactDir $auditFile)
    foreach ($prop in $audit.PSObject.Properties.Name) {
        if ($prop -ne "phase" -and $prop -ne "auditResult" -and $audit.$prop -eq $true) {
            Fail "$auditFile has true forbidden property $prop"
        }
    }
}

$noOrder = Read-Json (Join-Path $artifactDir "phase-product-readiness-r003-no-order-fill-route-audit.json")
if ($noOrder.ordersCreated -ne 0 -or $noOrder.routesCreated -ne 0 -or $noOrder.submissionsCreated -ne 0 -or $noOrder.newFillsCreated -ne 0 -or $noOrder.newExecutionReportsCreated -ne 0 -or $noOrder.executableSchedulesCreated -ne 0) {
    Fail "order/fill/route artifacts created"
}

$timing = Read-Json (Join-Path $artifactDir "phase-product-readiness-r003-canonical-timing-preservation.json")
Assert-True $timing.futureTimestampsUseCanonicalQuarterHour "canonical quarter-hour"
Assert-False $timing.legacy06UsedAsFutureCanonical "legacy :06 future canonical"

$direct = Read-Json (Join-Path $artifactDir "phase-product-readiness-r003-direct-cross-exclusion-preservation.json")
Assert-False $direct.directCrossExecutionAllowed "direct-cross execution"
Assert-True $direct.directCrossesSignalOnly "direct crosses signal only"
Assert-True $direct.nettingFirstRequired "netting first"

$usdjpy = Read-Json (Join-Path $artifactDir "phase-product-readiness-r003-usdjpy-caveat-preservation.json")
Assert-True $usdjpy.usdjpyCaveatPreserved "USDJPY caveat"
Assert-False $usdjpy.usdJpyCaveatWeakened "USDJPY caveat weakened"
if ($usdjpy.normalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.executionTradableSymbol -ne "USDJPY" -or $usdjpy.securityID -ne "4004" -or $usdjpy.securityIDSource -ne "8") {
    Fail "USDJPY caveat values changed"
}

$forbidden = Read-Json (Join-Path $artifactDir "phase-product-readiness-r003-forbidden-actions-audit.json")
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
    "productionLiveReadinessClaimed",
    "netAccountingPnlReadinessClaimed",
    "ledgerCommitReadinessClaimed",
    "costsFeesFxAccountCurrencyAggregationApplied",
    "marketDataWarnMisclassifiedAsPass",
    "oldQubes4eTreatedAsActive",
    "directCrossExecutionAllowed",
    "legacy06UsedAsFutureCanonical",
    "usdJpyCaveatWeakened",
    "microStepRoadmapProduced"
)) {
    Assert-False $forbidden.$name $name
}

$next = Read-Json (Join-Path $artifactDir "phase-product-readiness-r003-next-phase-recommendation.json")
if ($next.recommendedMode -ne "LargePackage") { Fail "next phase mode must be LargePackage" }
foreach ($blocked in @("NetPnl", "AccountingPnl", "ProductionPnl", "LedgerCommit", "ProductionLive")) {
    if ($next.doNotProceedTo -notcontains $blocked) { Fail "Next phase missing blocked target $blocked" }
}

$evidence = Read-Json (Join-Path $artifactDir "phase-product-readiness-r003-build-test-validator-evidence.json")
if ($evidence.build.result -ne "Passed") { Fail "build evidence missing/pending" }
if ($evidence.focusedStaticChecks.result -ne "Passed") { Fail "static check evidence missing/pending" }
if ($evidence.validator.result -ne "Passed") { Fail "validator evidence missing/pending" }
if ($evidence.focusedTests.result -ne "NotApplicable") { Fail "focused tests should be NotApplicable for static R003" }

Write-Output "PRODUCT_READINESS_R003_GATE_PASS_GROSS_PNL_V0_READY_NO_MUTATION"

