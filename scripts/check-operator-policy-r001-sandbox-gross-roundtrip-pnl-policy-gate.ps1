param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "OPERATOR_POLICY_R001_GATE_FAIL: $Message"
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

$artifactDir = Join-Path $Root "artifacts/readiness/operator-policy"
$required = @(
    "phase-operator-policy-r001-summary.md",
    "phase-operator-policy-r001-pnl-policy-reference.json",
    "phase-operator-policy-r001-mark-price-policy-decision.json",
    "phase-operator-policy-r001-cost-spread-commission-policy-decision.json",
    "phase-operator-policy-r001-fx-account-currency-policy-decision.json",
    "phase-operator-policy-r001-position-cost-basis-policy-decision.json",
    "phase-operator-policy-r001-attribution-policy-decision.json",
    "phase-operator-policy-r001-qubes-runid-policy-decision.json",
    "phase-operator-policy-r001-commit-policy-blockers.json",
    "phase-operator-policy-r001-sandbox-gross-roundtrip-pnl-policy-v0.json",
    "phase-operator-policy-r001-pnl-readiness-update.json",
    "phase-operator-policy-r001-unresolved-blockers.json",
    "phase-operator-policy-r001-decision.json",
    "phase-operator-policy-r001-no-external-audit.json",
    "phase-operator-policy-r001-no-execution-audit.json",
    "phase-operator-policy-r001-no-db-mutation-audit.json",
    "phase-operator-policy-r001-no-order-fill-route-audit.json",
    "phase-operator-policy-r001-no-ledger-state-mutation-audit.json",
    "phase-operator-policy-r001-canonical-timing-preservation.json",
    "phase-operator-policy-r001-direct-cross-exclusion-preservation.json",
    "phase-operator-policy-r001-usdjpy-caveat-preservation.json",
    "phase-operator-policy-r001-forbidden-actions-audit.json",
    "phase-operator-policy-r001-next-phase-recommendation.json",
    "phase-operator-policy-r001-build-test-validator-evidence.json"
)

foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $name))) {
        Fail "Missing required artifact $name"
    }
}

$ref = Read-Json (Join-Path $artifactDir "phase-operator-policy-r001-pnl-policy-reference.json")
Assert-True $ref.pnlPolicyR001Completed "PNL-POLICY-R001 completed"
if ($ref.currentCeilingBeforeThisPhase -ne "SandboxPriceDeltaOnlyReady") { Fail "Starting ceiling mismatch" }
Assert-False $ref.policyInventedBeyondPrompt "policy invented beyond prompt"

$mark = Read-Json (Join-Path $artifactDir "phase-operator-policy-r001-mark-price-policy-decision.json")
if ($mark.markPolicy -ne "NotRequiredForClosedRoundTripSandboxGrossPnl") { Fail "Mark policy mismatch" }
Assert-False $mark.markPricesInvented "mark prices invented"
Assert-True $mark.missingMarkPricesPreservedAsBlocker "MissingMarkPrices blocker"
Assert-False $mark.marketDataDbReadinessClaimedComplete "MarketData DB claimed complete"
if ($mark.blockerPreserved -ne "MissingMarkPrices") { Fail "MissingMarkPrices blocker weakened" }
foreach ($blocked in @("OpenPositionPnl", "AccountingPnl", "ProductionPnl", "FairValuePnl", "MarkToMarketPnl")) {
    if ($mark.notAllowedFor -notcontains $blocked) { Fail "mark policy must not allow $blocked" }
}

$cost = Read-Json (Join-Path $artifactDir "phase-operator-policy-r001-cost-spread-commission-policy-decision.json")
if ($cost.costPolicy -ne "GrossOnly_NoCostsApplied") { Fail "Cost policy mismatch" }
Assert-False $cost.netPnlReady "net pnl ready"
Assert-False $cost.costsApplied "costs applied"
Assert-False $cost.feesApplied "fees applied"
Assert-False $cost.commissionsApplied "commissions applied"
Assert-False $cost.costSpreadCommissionModelFinal "cost model final"
Assert-True $cost.fiveUsdPerMillionGuidance.majorOnlyGuidance "5 USD/million major-only"
Assert-True $cost.fiveUsdPerMillionGuidance.optionalSensitivityOnly "5 USD/million optional sensitivity"
Assert-False $cost.fiveUsdPerMillionGuidance.universalized "5 USD/million universalized"
Assert-False $cost.fiveUsdPerMillionGuidance.finalCostModel "5 USD/million final model"
if ($cost.blockerPreserved -ne "MissingCostSpreadCommissionModel") { Fail "cost blocker weakened" }

$fx = Read-Json (Join-Path $artifactDir "phase-operator-policy-r001-fx-account-currency-policy-decision.json")
if ($fx.fxPolicy -ne "QuoteCurrencyOnlySandboxPreview") { Fail "FX policy mismatch" }
Assert-False $fx.accountCurrencyReady "account currency ready"
Assert-False $fx.fxConversionReady "FX conversion ready"
Assert-False $fx.fxConversionInvented "FX conversion invented"
Assert-False $fx.accountCurrencyInvented "account currency invented"
Assert-False $fx.globalAccountCurrencyPnlAuthorized "global account currency PnL"
Assert-False $fx.crossCurrencyAggregationAuthorized "cross currency aggregation"
foreach ($symbol in @("AUDUSD", "EURUSD", "GBPUSD")) {
    $entry = @($fx.symbolCurrencyPolicy | Where-Object { $_.symbol -eq $symbol })[0]
    if ($null -eq $entry) { Fail "Missing FX symbol policy $symbol" }
    if ($entry.quoteCurrency -ne "USD") { Fail "$symbol quote currency must be USD" }
    if ($null -ne $entry.accountCurrency) { Fail "$symbol account currency must remain null" }
    Assert-False $entry.accountCurrencyPnlReady "$symbol account currency pnl"
}
foreach ($blocker in @("MissingFxConversion", "MissingAccountCurrency")) {
    if ($fx.blockersPreserved -notcontains $blocker) { Fail "FX blocker $blocker missing" }
}

$basis = Read-Json (Join-Path $artifactDir "phase-operator-policy-r001-position-cost-basis-policy-decision.json")
if ($basis.positionBasisPolicy -ne "MatchedRoundTripFlatOnlySandboxBasis") { Fail "Position basis policy mismatch" }
Assert-True $basis.grossRoundTripPriceDeltaAllowed "gross price delta"
Assert-False $basis.accountingCostBasisReady "accounting cost basis"
Assert-False $basis.productionCostBasisReady "production cost basis"
Assert-False $basis.costBasisInvented "cost basis invented"
if ($basis.blockerPreserved -ne "MissingPositionCostBasisModel") { Fail "position basis blocker weakened" }

$attrib = Read-Json (Join-Path $artifactDir "phase-operator-policy-r001-attribution-policy-decision.json")
if ($attrib.attributionPolicy -ne "SandboxPreviewAttributionByPmsCycleAndRebalanceIntent") { Fail "Attribution policy mismatch" }
Assert-False $attrib.fullStrategyAttributionClaimed "strategy attribution claimed"
Assert-False $attrib.fullAccountAttributionClaimed "account attribution claimed"
Assert-False $attrib.fullPortfolioAttributionClaimed "portfolio attribution claimed"
Assert-False $attrib.attributionPolicyInvented "attribution invented"
foreach ($dimension in @("AccountId", "PortfolioId", "StrategyId", "SourceExecutionIntentId", "PMS-approved QubesRunId")) {
    if ($attrib.knownMissingAttributionDimensions -notcontains $dimension) { Fail "Missing attribution dimension $dimension not preserved" }
}
foreach ($blocker in @("MissingAttributionPolicy", "MissingAccountId", "MissingPortfolioId", "MissingStrategyId", "MissingSourceExecutionIntentId", "QubesRunIdNotPmsApprovedEconomicOutput")) {
    if ($attrib.blockersPreserved -notcontains $blocker) { Fail "Attribution blocker $blocker missing" }
}

$qubes = Read-Json (Join-Path $artifactDir "phase-operator-policy-r001-qubes-runid-policy-decision.json")
if ($qubes.qubesRunIdStatus -ne "PresentWithWarnings_NotPmsApprovedEconomicOutput") { Fail "QubesRunId warning weakened" }
Assert-True $qubes.mayRecordAsWarningLineageIfPresent "QubesRunId warning lineage"
Assert-False $qubes.treatAsPmsApprovedEconomicOutput "QubesRunId PMS-approved"
Assert-False $qubes.oldQubes4EStratTakenActiveState "old Qubes 4E active"
Assert-False $qubes.qubesEconomicApprovalInvented "Qubes approval invented"

$commit = Read-Json (Join-Path $artifactDir "phase-operator-policy-r001-commit-policy-blockers.json")
Assert-False $commit.paperLedgerCommitAllowed "paper ledger commit"
Assert-False $commit.productionLedgerCommitAllowed "production ledger commit"
Assert-False $commit.tradingStateMutationAllowed "trading state mutation"
Assert-False $commit.productionPnlAllowed "production pnl"
Assert-False $commit.accountingPnlAllowed "accounting pnl"
Assert-False $commit.paperPositionMutationAllowed "paper position mutation"
Assert-False $commit.productionPositionMutationAllowed "production position mutation"
Assert-False $commit.cashMutationAllowed "cash mutation"
Assert-False $commit.productionLivePromotionAllowed "production live promotion"
if ($commit.blockersPreserved -notcontains "CommitSafeIdempotencyPolicyIncomplete") { Fail "commit blocker missing" }

$contract = Read-Json (Join-Path $artifactDir "phase-operator-policy-r001-sandbox-gross-roundtrip-pnl-policy-v0.json")
if ($contract.contractId -ne "SandboxGrossRoundTripPnlPreviewV0") { Fail "contract id mismatch" }
if ($contract.readiness -ne "Ready") { Fail "gross round-trip policy must be ready" }
foreach ($symbol in @("AUDUSD", "EURUSD", "GBPUSD")) {
    if ($contract.allowedSymbols -notcontains $symbol) { Fail "contract missing symbol $symbol" }
}
if ($contract.pnlCurrencyPolicy -ne "QuoteCurrencyOnlySandboxPreview") { Fail "contract FX policy mismatch" }
if ($contract.quoteCurrency -ne "USD") { Fail "contract quote currency must be USD" }
if ($null -ne $contract.accountCurrency) { Fail "contract account currency must remain null" }
Assert-False $contract.costsApplied "contract costs"
Assert-False $contract.feesApplied "contract fees"
Assert-False $contract.commissionsApplied "contract commissions"
Assert-False $contract.fxConversionApplied "contract FX conversion"
Assert-False $contract.accountCurrencyAggregationApplied "contract account currency aggregation"
Assert-False $contract.accountingAttributionApplied "contract accounting attribution"
Assert-False $contract.ledgerCommitAllowed "contract ledger commit"
Assert-False $contract.productionPnlAllowed "contract production pnl"
Assert-False $contract.accountingPnlAllowed "contract accounting pnl"
Assert-True $contract.sandboxOnly "contract sandbox only"

$readiness = Read-Json (Join-Path $artifactDir "phase-operator-policy-r001-pnl-readiness-update.json")
Assert-True $readiness.SandboxPriceDeltaOnlyReady "SandboxPriceDeltaOnlyReady"
Assert-True $readiness.SandboxGrossRoundTripPnlPreviewV0Ready "SandboxGrossRoundTripPnlPreviewV0Ready"
Assert-False $readiness.SandboxTheoreticalPnlReady "SandboxTheoreticalPnlReady"
Assert-False $readiness.PaperAccountingPnlReady "PaperAccountingPnlReady"
Assert-False $readiness.ProductionPnlReady "ProductionPnlReady"
Assert-False $readiness.LedgerCommitReady "LedgerCommitReady"
if ($readiness.newAllowedCeiling -ne "SandboxGrossRoundTripPnlPreviewV0Ready") { Fail "new ceiling mismatch" }

$blockers = Read-Json (Join-Path $artifactDir "phase-operator-policy-r001-unresolved-blockers.json")
Assert-True $blockers.allRequiredBlockersPreserved "all blockers preserved"
foreach ($blocker in @("MissingMarkPrices", "MissingCostSpreadCommissionModel", "MissingFxConversion", "MissingAccountCurrency", "MissingPositionCostBasisModel", "MissingAttributionPolicy", "MissingAccountId", "MissingPortfolioId", "MissingStrategyId", "MissingSourceExecutionIntentId", "QubesRunIdNotPmsApprovedEconomicOutput", "CommitSafeIdempotencyPolicyIncomplete", "MarketDataWarnM30Missing", "MarketDataWarnRowCountsMissing", "MarketDataWarnTickSchemaPartial")) {
    $entry = @($blockers.blockers | Where-Object { $_.blocker -eq $blocker })[0]
    if ($null -eq $entry) { Fail "Missing unresolved blocker $blocker" }
    Assert-True $entry.preserved "$blocker preserved"
}

$decision = Read-Json (Join-Path $artifactDir "phase-operator-policy-r001-decision.json")
foreach ($d in @("SandboxGrossRoundTripPnlPreviewV0PolicyReady", "LedgerCommitStillBlocked", "AccountingPnlStillBlocked", "ProductionPnlStillBlocked")) {
    if ($decision.decisions -notcontains $d) { Fail "Missing decision $d" }
}
if ($decision.newAllowedCeiling -ne "SandboxGrossRoundTripPnlPreviewV0Ready") { Fail "decision ceiling mismatch" }
Assert-False $decision.sandboxTheoreticalPnlReady "sandbox theoretical pnl"
Assert-False $decision.paperAccountingPnlReady "paper accounting pnl"
Assert-False $decision.productionPnlReady "production pnl"
Assert-False $decision.ledgerCommitReady "ledger commit"
Assert-False $decision.netPnlReady "net pnl"
Assert-False $decision.accountingPnlReadinessClaimed "accounting pnl claim"
Assert-False $decision.productionPnlReadinessClaimed "production pnl claim"
Assert-False $decision.fullSandboxTheoreticalPnlClaimed "full sandbox theoretical pnl claim"

foreach ($auditFile in @(
    "phase-operator-policy-r001-no-external-audit.json",
    "phase-operator-policy-r001-no-execution-audit.json",
    "phase-operator-policy-r001-no-db-mutation-audit.json",
    "phase-operator-policy-r001-no-ledger-state-mutation-audit.json"
)) {
    $audit = Read-Json (Join-Path $artifactDir $auditFile)
    foreach ($prop in $audit.PSObject.Properties.Name) {
        if ($prop -ne "phase" -and $prop -ne "auditResult" -and $audit.$prop -eq $true) {
            Fail "$auditFile has true forbidden property $prop"
        }
    }
}

$noOrder = Read-Json (Join-Path $artifactDir "phase-operator-policy-r001-no-order-fill-route-audit.json")
if ($noOrder.ordersCreated -ne 0 -or $noOrder.routesCreated -ne 0 -or $noOrder.submissionsCreated -ne 0 -or $noOrder.newFillsCreated -ne 0 -or $noOrder.newExecutionReportsCreated -ne 0 -or $noOrder.executableSchedulesCreated -ne 0) {
    Fail "order/fill/route artifacts created"
}

$timing = Read-Json (Join-Path $artifactDir "phase-operator-policy-r001-canonical-timing-preservation.json")
Assert-True $timing.futureTimestampsUseCanonicalQuarterHour "canonical quarter-hour"
Assert-False $timing.legacy06UsedAsFutureCanonical "legacy :06 future canonical"

$direct = Read-Json (Join-Path $artifactDir "phase-operator-policy-r001-direct-cross-exclusion-preservation.json")
Assert-False $direct.directCrossExecutionAllowed "direct-cross execution"
Assert-True $direct.noDirectCrossExecution "no direct-cross execution"

$usdjpy = Read-Json (Join-Path $artifactDir "phase-operator-policy-r001-usdjpy-caveat-preservation.json")
Assert-True $usdjpy.usdjpyCaveatPreserved "USDJPY caveat"
Assert-False $usdjpy.usdJpyCaveatWeakened "USDJPY caveat weakened"
if ($usdjpy.normalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.executionTradableSymbol -ne "USDJPY" -or $usdjpy.securityID -ne "4004" -or $usdjpy.securityIDSource -ne "8") {
    Fail "USDJPY caveat values changed"
}

$forbidden = Read-Json (Join-Path $artifactDir "phase-operator-policy-r001-forbidden-actions-audit.json")
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
    "policyInventedBeyondPrompt",
    "fiveUsdPerMillionUniversalized",
    "costsFeesCommissionsTreatedAsFinal",
    "fxConversionInvented",
    "accountCurrencyInvented",
    "accountingProductionPnlClaimed",
    "fullSandboxTheoreticalPnlClaimed",
    "legacy06UsedAsFutureCanonical",
    "directCrossExecutionAllowed",
    "usdJpyCaveatWeakened"
)) {
    Assert-False $forbidden.$name $name
}

$next = Read-Json (Join-Path $artifactDir "phase-operator-policy-r001-next-phase-recommendation.json")
if ($next.decision -ne "SandboxGrossRoundTripPnlPreviewV0PolicyReady") { Fail "next decision mismatch" }
foreach ($blocked in @("SandboxTheoreticalPnlReady", "PaperAccountingPnlReady", "ProductionPnlReady", "LedgerCommitReady")) {
    if ($next.doNotProceedTo -notcontains $blocked) { Fail "Missing do-not-proceed target $blocked" }
}

$evidence = Read-Json (Join-Path $artifactDir "phase-operator-policy-r001-build-test-validator-evidence.json")
if ($evidence.build.result -ne "Passed") { Fail "build evidence missing/pending" }
if ($evidence.focusedStaticChecks.result -ne "Passed") { Fail "static check evidence missing/pending" }

Write-Output "OPERATOR_POLICY_R001_GATE_PASS_SANDBOX_GROSS_ROUNDTRIP_PNL_POLICY_READY_NO_MUTATION"
