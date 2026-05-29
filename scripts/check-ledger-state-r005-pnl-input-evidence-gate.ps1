param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "LEDGER_STATE_R005_GATE_FAIL: $Message"
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

$artifactDir = Join-Path $Root "artifacts/readiness/ledger-state"
$required = @(
    "phase-ledger-state-r005-summary.md",
    "phase-ledger-state-r005-r004-reference.json",
    "phase-ledger-state-r005-pnl-input-evidence-inventory.json",
    "phase-ledger-state-r005-mark-price-evidence-status.json",
    "phase-ledger-state-r005-cost-spread-commission-evidence-status.json",
    "phase-ledger-state-r005-fx-conversion-evidence-status.json",
    "phase-ledger-state-r005-position-cost-basis-evidence-status.json",
    "phase-ledger-state-r005-account-currency-evidence-status.json",
    "phase-ledger-state-r005-attribution-policy-evidence-status.json",
    "phase-ledger-state-r005-pms-qubes-lineage-recheck.json",
    "phase-ledger-state-r005-theoretical-pnl-readiness-assessment.json",
    "phase-ledger-state-r005-price-delta-boundary-preservation.json",
    "phase-ledger-state-r005-remaining-pnl-blockers.json",
    "phase-ledger-state-r005-decision.json",
    "phase-ledger-state-r005-no-db-mutation-audit.json",
    "phase-ledger-state-r005-no-ledger-commit-audit.json",
    "phase-ledger-state-r005-no-production-ledger-audit.json",
    "phase-ledger-state-r005-no-trading-state-mutation-audit.json",
    "phase-ledger-state-r005-no-order-fill-route-audit.json",
    "phase-ledger-state-r005-not-production-pnl-audit.json",
    "phase-ledger-state-r005-not-accounting-pnl-audit.json",
    "phase-ledger-state-r005-canonical-timing-preservation.json",
    "phase-ledger-state-r005-direct-cross-exclusion-preservation.json",
    "phase-ledger-state-r005-usdjpy-caveat-preservation.json",
    "phase-ledger-state-r005-marketdata-execreport-boundary-preservation.json",
    "phase-ledger-state-r005-forbidden-actions-audit.json",
    "phase-ledger-state-r005-next-phase-recommendation.json",
    "phase-ledger-state-r005-build-test-validator-evidence.json"
)

foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $name))) {
        Fail "Missing required artifact $name"
    }
}

$r004 = Read-Json (Join-Path $artifactDir "phase-ledger-state-r005-r004-reference.json")
Assert-True $r004.r004Completed "R004 completed"
Assert-True $r004.r004PriceDeltaOnly "R004 price delta only"
Assert-False $r004.r004FullTheoreticalPnl "R004 full theoretical pnl"
Assert-False $r004.r004ProductionPnl "R004 production pnl"
Assert-False $r004.r004AccountingPnl "R004 accounting pnl"
if ($r004.r004Decision -ne "CrossRailPmsLinkedSandboxLedgerPreviewReadyWithCompletePriceDeltaOnly") { Fail "Unexpected R004 reference decision" }

$inventory = Read-Json (Join-Path $artifactDir "phase-ledger-state-r005-pnl-input-evidence-inventory.json")
Assert-True $inventory.evidenceOnly "inventory evidenceOnly"
Assert-True $inventory.noFieldsInvented "inventory noFieldsInvented"
foreach ($name in @("MarkPrices", "CostSpreadCommissionModel", "FxConversion", "PositionCostBasis", "AccountCurrency", "AttributionPolicy", "AccountId", "PortfolioId", "StrategyId", "QubesRunId", "SourceExecutionIntentId")) {
    $entry = @($inventory.items | Where-Object { $_.name -eq $name })[0]
    if ($null -eq $entry) { Fail "Missing inventory item $name" }
    if ($null -eq $entry.blocker) { Fail "$name must carry a blocker" }
}

$marks = Read-Json (Join-Path $artifactDir "phase-ledger-state-r005-mark-price-evidence-status.json")
if ($marks.status -ne "Missing") { Fail "Mark prices must remain missing" }
Assert-False $marks.markPricesPresent "mark prices present"
Assert-False $marks.markPricesInvented "mark prices invented"
Assert-False $marks.liveMarketDataRequested "live market data requested"
Assert-False $marks.dbQueryRun "DB query run"
Assert-False $marks.marketDataDbReadinessClaimedComplete "MarketData DB claimed complete"

$cost = Read-Json (Join-Path $artifactDir "phase-ledger-state-r005-cost-spread-commission-evidence-status.json")
Assert-False $cost.explicitCostModelPresent "explicit cost model"
Assert-False $cost.explicitSpreadModelPresent "explicit spread model"
Assert-False $cost.explicitCommissionModelPresent "explicit commission model"
Assert-False $cost.explicitSlippageModelPresent "explicit slippage model"
Assert-True $cost.fiveUsdPerMillionGuidance.present "5 USD/million guidance present"
Assert-False $cost.fiveUsdPerMillionGuidance.universalized "5 USD/million universalized"
Assert-False $cost.fiveUsdPerMillionGuidance.usableAsFullModel "5 USD/million usable full model"
Assert-False $cost.costModelInvented "cost model invented"

$fx = Read-Json (Join-Path $artifactDir "phase-ledger-state-r005-fx-conversion-evidence-status.json")
Assert-True $fx.quoteCurrencyPriceDeltaPreviewPossible "quote currency price delta possible"
Assert-False $fx.accountCurrencyKnown "account currency known"
Assert-False $fx.conversionRatesPresent "conversion rates"
Assert-False $fx.fxConversionInvented "FX conversion invented"

$basis = Read-Json (Join-Path $artifactDir "phase-ledger-state-r005-position-cost-basis-evidence-status.json")
Assert-True $basis.r004ResidualsZero "R004 residuals zero"
Assert-True $basis.positionDeltaPreviewComplete "position delta preview"
Assert-False $basis.pnlBasisComplete "PnL basis complete"
Assert-False $basis.costBasisInvented "cost basis invented"

$currency = Read-Json (Join-Path $artifactDir "phase-ledger-state-r005-account-currency-evidence-status.json")
if ($null -ne $currency.accountCurrency) { Fail "Account currency must not be invented" }
Assert-False $currency.accountCurrencyKnown "account currency known"
Assert-False $currency.accountCurrencyInvented "account currency invented"

$attrib = Read-Json (Join-Path $artifactDir "phase-ledger-state-r005-attribution-policy-evidence-status.json")
Assert-False $attrib.strategyLevelPolicyPresent "strategy attribution"
Assert-False $attrib.accountLevelPolicyPresent "account attribution"
Assert-False $attrib.pmsCycleLevelPolicyPresent "PMS attribution"
Assert-False $attrib.qubesRunLevelPolicyPresent "Qubes attribution"
Assert-False $attrib.executionIntentLevelPolicyPresent "execution intent attribution"
Assert-False $attrib.attributionInvented "attribution invented"

$lineage = Read-Json (Join-Path $artifactDir "phase-ledger-state-r005-pms-qubes-lineage-recheck.json")
if ($lineage.oldQubes4EStatus -ne "HistoricalOnlyNotActiveState") { Fail "Old Qubes 4E treated as active" }
Assert-False $lineage.q4eStratTakenActiveState "Q4E active"
Assert-False $lineage.missingPmsQubesFieldsInvented "PMS/Qubes fields invented"
foreach ($field in @("AccountId", "PortfolioId", "StrategyId", "QubesRunId", "SourceExecutionIntentId")) {
    $entry = @($lineage.fields | Where-Object { $_.field -eq $field })[0]
    if ($null -eq $entry) { Fail "Missing lineage field $field" }
    if ($null -ne $entry.value) { Fail "$field must remain null" }
    if ($null -eq $entry.blocker) { Fail "$field must carry blocker" }
}

$readiness = Read-Json (Join-Path $artifactDir "phase-ledger-state-r005-theoretical-pnl-readiness-assessment.json")
if ($readiness.readinessStatus -ne "SandboxTheoreticalPnlBlockedMissingEconomicInputs") { Fail "Unexpected readiness status" }
Assert-True $readiness.sandboxPriceDeltaOnlyReady "sandbox price delta only"
Assert-False $readiness.sandboxTheoreticalPnlReadyWithWarnings "sandbox theoretical pnl warnings"
Assert-False $readiness.fullTheoreticalPnlReady "full theoretical pnl ready"
Assert-False $readiness.productionPnlReady "production pnl ready"
Assert-False $readiness.accountingPnlReady "accounting pnl ready"
Assert-False $readiness.realPnlComputed "real pnl computed"
Assert-False $readiness.ledgerCommitAllowed "ledger commit"
Assert-False $readiness.ledgerMutation "ledger mutation"
Assert-False $readiness.tradingStateMutation "trading state mutation"

$boundary = Read-Json (Join-Path $artifactDir "phase-ledger-state-r005-price-delta-boundary-preservation.json")
Assert-True $boundary.sandboxPriceDeltaOnlyReady "price delta only ready"
Assert-True $boundary.r004PriceDeltaPreviewPreserved "R004 price delta preserved"
Assert-False $boundary.fullTheoreticalPnlProduced "full theoretical pnl produced"
Assert-False $boundary.sandboxPriceDeltaMisclassifiedAsFullTheoreticalPnl "price delta misclassified"
Assert-False $boundary.productionPnlComputed "production pnl computed"
Assert-False $boundary.productionPnlClaimedReady "production pnl ready"
Assert-False $boundary.accountingPnlComputed "accounting pnl computed"
Assert-False $boundary.accountingPnlClaimedReady "accounting pnl ready"

$blockers = Read-Json (Join-Path $artifactDir "phase-ledger-state-r005-remaining-pnl-blockers.json")
Assert-True $blockers.previewOnly "blockers previewOnly"
Assert-False $blockers.commitAllowed "blockers commitAllowed"
foreach ($blocker in @("MissingMarkPrices", "MissingCostSpreadCommissionModel", "MissingFxConversion", "MissingPositionCostBasisModel", "MissingAccountCurrency", "MissingAttributionPolicy", "MissingAccountId", "MissingPortfolioId", "MissingStrategyId", "MissingQubesRunId", "MissingSourceExecutionIntentId", "CommitSafeIdempotencyPolicyIncomplete")) {
    if ($blockers.blockers -notcontains $blocker) { Fail "Missing blocker $blocker" }
}

$decision = Read-Json (Join-Path $artifactDir "phase-ledger-state-r005-decision.json")
if ($decision.decision -ne "SandboxTheoreticalPnlBlockedMissingInputs") { Fail "Unexpected decision" }
Assert-True $decision.previewOnly "decision previewOnly"
Assert-False $decision.commitAllowed "decision commitAllowed"
Assert-False $decision.ledgerMutation "decision ledgerMutation"
Assert-False $decision.tradingStateMutation "decision tradingStateMutation"
Assert-False $decision.productionPnl "decision productionPnl"
Assert-False $decision.accountingPnl "decision accountingPnl"
Assert-False $decision.fullTheoreticalPnl "decision fullTheoreticalPnl"

foreach ($auditFile in @(
    "phase-ledger-state-r005-no-db-mutation-audit.json",
    "phase-ledger-state-r005-no-ledger-commit-audit.json",
    "phase-ledger-state-r005-no-production-ledger-audit.json",
    "phase-ledger-state-r005-no-trading-state-mutation-audit.json"
)) {
    $audit = Read-Json (Join-Path $artifactDir $auditFile)
    foreach ($prop in $audit.PSObject.Properties.Name) {
        if ($prop -ne "phase" -and $prop -ne "auditResult" -and $audit.$prop -eq $true) {
            Fail "$auditFile has true forbidden property $prop"
        }
    }
}

$noOrder = Read-Json (Join-Path $artifactDir "phase-ledger-state-r005-no-order-fill-route-audit.json")
if ($noOrder.ordersCreated -ne 0 -or $noOrder.routesCreated -ne 0 -or $noOrder.submissionsCreated -ne 0 -or $noOrder.fillsCreated -ne 0 -or $noOrder.executionReportsCreated -ne 0 -or $noOrder.schedulesCreated -ne 0) {
    Fail "R005 created order/route/submission/fill/execution report/schedule"
}
Assert-True $noOrder.sourceSandboxFillsReusedOnly "source sandbox fills reused only"

$notProd = Read-Json (Join-Path $artifactDir "phase-ledger-state-r005-not-production-pnl-audit.json")
Assert-False $notProd.productionPnlComputed "production pnl computed"
Assert-False $notProd.productionPnlClaimedReady "production pnl ready"
Assert-True $notProd.notProductionPnl "not production pnl"

$notAcct = Read-Json (Join-Path $artifactDir "phase-ledger-state-r005-not-accounting-pnl-audit.json")
Assert-False $notAcct.accountingPnlComputed "accounting pnl computed"
Assert-False $notAcct.accountingPnlClaimedReady "accounting pnl ready"
Assert-False $notAcct.realPnlComputedWithoutEvidence "real pnl computed without evidence"
Assert-True $notAcct.notAccountingPnl "not accounting pnl"

$timing = Read-Json (Join-Path $artifactDir "phase-ledger-state-r005-canonical-timing-preservation.json")
Assert-True $timing.canonicalTargetCloseIsQuarterHour "canonical quarter hour"
Assert-False $timing.legacy06UsedAsFutureCanonical "legacy :06 canonical"

$direct = Read-Json (Join-Path $artifactDir "phase-ledger-state-r005-direct-cross-exclusion-preservation.json")
Assert-False $direct.directCrossExecutionAllowed "direct-cross execution"
Assert-False $direct.ledgerCreatesDirectCrossExposureFromRawQubesSignals "direct-cross ledger exposure"

$usdjpy = Read-Json (Join-Path $artifactDir "phase-ledger-state-r005-usdjpy-caveat-preservation.json")
Assert-True $usdjpy.usdjpyCaveatPreservedForFutureUse "USDJPY caveat"
Assert-False $usdjpy.usdJpyCaveatWeakened "USDJPY caveat weakened"
if ($usdjpy.normalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.executionTradableSymbol -ne "USDJPY" -or $usdjpy.securityID -ne "4004" -or $usdjpy.securityIDSource -ne "8") {
    Fail "USDJPY caveat values changed"
}

$market = Read-Json (Join-Path $artifactDir "phase-ledger-state-r005-marketdata-execreport-boundary-preservation.json")
Assert-False $market.marketDataDbReadinessClaimedComplete "MarketData DB complete"
Assert-True $market.marketDataWarnDoesNotInvalidateExecutionReportEvidence "MarketData boundary"
if ($market.marketDataStatus.'lmax-marketdata-db.v1' -ne "WITH_WARNINGS" -or $market.marketDataStatus.'marketdata-readiness.v1' -ne "WITH_WARNINGS") {
    Fail "MarketData WARN status not preserved"
}

$forbidden = Read-Json (Join-Path $artifactDir "phase-ledger-state-r005-forbidden-actions-audit.json")
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
    "costSpreadCommissionModelInvented",
    "fxConversionInvented",
    "accountCurrencyInvented",
    "attributionPolicyInvented",
    "priceDeltaMisclassifiedAsFullTheoreticalPnl",
    "realAccountingProductionPnlClaimed",
    "missingPmsQubesFieldsInvented",
    "oldQubes4ETreatedAsActive",
    "legacy06UsedAsFutureCanonical",
    "directCrossExecutionAllowed",
    "usdJpyCaveatWeakened",
    "marketDataDbReadinessClaimedComplete"
)) {
    Assert-False $forbidden.$name $name
}

$next = Read-Json (Join-Path $artifactDir "phase-ledger-state-r005-next-phase-recommendation.json")
if ($next.decision -ne "SandboxTheoreticalPnlBlockedMissingInputs") { Fail "Next phase decision mismatch" }

$evidence = Read-Json (Join-Path $artifactDir "phase-ledger-state-r005-build-test-validator-evidence.json")
if ($evidence.build.result -ne "Passed") { Fail "Build evidence missing/pending" }
if ($evidence.focusedTests.result -ne "Passed" -or $evidence.focusedTests.passed -ne 4 -or $evidence.focusedTests.failed -ne 0) { Fail "Focused test evidence missing/pending" }
if ($evidence.focusedStaticChecks.result -ne "Passed") { Fail "Static check evidence missing/pending" }

Write-Output "LEDGER_STATE_R005_GATE_PASS_PNL_INPUT_EVIDENCE_READY_NO_MUTATION"
