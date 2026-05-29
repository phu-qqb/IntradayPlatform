param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "LEDGER_STATE_R004_GATE_FAIL: $Message"
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
    "phase-ledger-state-r004-summary.md",
    "phase-ledger-state-r004-cross-rail-r014-reference.json",
    "phase-ledger-state-r004-cross-rail-fill-intake.json",
    "phase-ledger-state-r004-build-caveat-review.json",
    "phase-ledger-state-r004-order-fill-reconciliation-review.json",
    "phase-ledger-state-r004-flatten-reconciliation-review.json",
    "phase-ledger-state-r004-pms-qubes-lineage-binding-matrix.json",
    "phase-ledger-state-r004-paper-ledger-preview-lines.json",
    "phase-ledger-state-r004-sandbox-price-delta-preview.json",
    "phase-ledger-state-r004-sandbox-pnl-preview-inputs.json",
    "phase-ledger-state-r004-gross-sandbox-price-delta-preview.json",
    "phase-ledger-state-r004-pnl-gap-diagnostics.json",
    "phase-ledger-state-r004-preview-reconciliation.json",
    "phase-ledger-state-r004-commit-blockers.json",
    "phase-ledger-state-r004-decision.json",
    "phase-ledger-state-r004-no-db-mutation-audit.json",
    "phase-ledger-state-r004-no-ledger-commit-audit.json",
    "phase-ledger-state-r004-no-production-ledger-audit.json",
    "phase-ledger-state-r004-no-trading-state-mutation-audit.json",
    "phase-ledger-state-r004-no-order-fill-route-audit.json",
    "phase-ledger-state-r004-not-production-pnl-audit.json",
    "phase-ledger-state-r004-not-accounting-pnl-audit.json",
    "phase-ledger-state-r004-canonical-timing-preservation.json",
    "phase-ledger-state-r004-direct-cross-exclusion-preservation.json",
    "phase-ledger-state-r004-usdjpy-caveat-preservation.json",
    "phase-ledger-state-r004-marketdata-execreport-boundary-preservation.json",
    "phase-ledger-state-r004-forbidden-actions-audit.json",
    "phase-ledger-state-r004-next-phase-recommendation.json",
    "phase-ledger-state-r004-build-test-validator-evidence.json"
)

foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $name))) {
        Fail "Missing required artifact $name"
    }
}

$ref = Read-Json (Join-Path $artifactDir "phase-ledger-state-r004-cross-rail-r014-reference.json")
Assert-True $ref.crossRailR014Passed "CROSS-RAIL-R014 passed"
Assert-True $ref.r008ReviewedReadOnly "R008 reviewed read-only"
Assert-True $ref.sandboxOnly "R014 sandboxOnly"
Assert-False $ref.productionLiveAllowed "production live"
Assert-False $ref.productionLedgerStateMutationAllowed "production ledger state mutation"

$intake = Read-Json (Join-Path $artifactDir "phase-ledger-state-r004-cross-rail-fill-intake.json")
if ($intake.openFillCount -ne 3 -or $intake.flattenFillCount -ne 3) { Fail "R014 intake must contain 3 open and 3 flatten fills" }
Assert-True $intake.sandboxOnly "intake sandboxOnly"
Assert-False $intake.productionFill "intake productionFill"
Assert-True $intake.notProductionPnl "intake notProductionPnl"
Assert-False $intake.productionArtifactsCreatedByR004 "production artifacts"
foreach ($fill in @($intake.openFills + $intake.flattenFills)) {
    Assert-True $fill.sandboxOnly "fill sandboxOnly $($fill.clientOrderId)"
    Assert-False $fill.productionFill "fill productionFill $($fill.clientOrderId)"
    Assert-True $fill.notProductionPnl "fill notProductionPnl $($fill.clientOrderId)"
}

$caveat = Read-Json (Join-Path $artifactDir "phase-ledger-state-r004-build-caveat-review.json")
Assert-True $caveat.r014BuildCaveatRecorded "R014 build caveat recorded"
if ($caveat.r014BuildResult -ne "Failed") { Fail "R014 build caveat must record failed R014 build" }
Assert-False $caveat.caveatMisclassifiedAsR014EvidenceFailure "R014 caveat misclassified"
if ($caveat.r004CurrentBuildResult -ne "Passed") { Fail "R004 current build must be passed or explicitly caveated" }

$orderRecon = Read-Json (Join-Path $artifactDir "phase-ledger-state-r004-order-fill-reconciliation-review.json")
if ($orderRecon.expectedOrders -ne 3 -or $orderRecon.actualOrders -ne 3 -or $orderRecon.expectedFills -ne 3 -or $orderRecon.actualFills -ne 3) {
    Fail "Order/fill reconciliation counts mismatch"
}
if (@($orderRecon.breaks).Count -ne 0) { Fail "Order/fill breaks must be empty" }

$flattenRecon = Read-Json (Join-Path $artifactDir "phase-ledger-state-r004-flatten-reconciliation-review.json")
if ($flattenRecon.expectedFlattenOrders -ne 3 -or $flattenRecon.actualFlattenOrders -ne 3 -or $flattenRecon.expectedFlattenFills -ne 3 -or $flattenRecon.actualFlattenFills -ne 3) {
    Fail "Flatten reconciliation counts mismatch"
}
foreach ($symbol in @("AUDUSD", "EURUSD", "GBPUSD")) {
    if ([decimal]$flattenRecon.residualBySymbol.$symbol -ne 0) { Fail "$symbol residual must be zero" }
}

$binding = Read-Json (Join-Path $artifactDir "phase-ledger-state-r004-pms-qubes-lineage-binding-matrix.json")
if ($binding.q4eBootstrapStatus -ne "HistoricalOnlyNotActiveState") { Fail "Qubes 4E bootstrap treated as active" }
foreach ($field in @("PmsCycleId", "RiskReviewId", "OperatorApprovalId", "SourceRebalanceIntentId", "CanonicalTargetCloseUtc")) {
    $entry = @($binding.fields | Where-Object { $_.field -eq $field })[0]
    if ($null -eq $entry -or ($entry.evidenceStatus -notmatch "Bound")) { Fail "$field must be bound" }
}
foreach ($field in @("AccountId", "PortfolioId", "StrategyId", "QubesRunId", "SourceExecutionIntentId")) {
    $entry = @($binding.fields | Where-Object { $_.field -eq $field })[0]
    if ($null -eq $entry) { Fail "$field binding entry missing" }
    if ($null -ne $entry.value) { Fail "$field must not be invented" }
}

$lines = Read-Json (Join-Path $artifactDir "phase-ledger-state-r004-paper-ledger-preview-lines.json")
Assert-True $lines.previewOnly "preview lines previewOnly"
Assert-False $lines.commitAllowed "preview lines commitAllowed"
Assert-False $lines.ledgerMutation "preview lines ledgerMutation"
Assert-False $lines.tradingStateMutation "preview lines tradingStateMutation"
if ($lines.lineCount -ne 6 -or @($lines.lines).Count -ne 6) { Fail "Paper-ledger preview must contain 6 lines" }
foreach ($line in $lines.lines) {
    Assert-True $line.sandboxOnly "line sandboxOnly $($line.lineId)"
    Assert-False $line.productionFill "line productionFill $($line.lineId)"
    Assert-True $line.notProductionPnl "line notProductionPnl $($line.lineId)"
    Assert-True $line.noLedgerCommit "line noLedgerCommit $($line.lineId)"
    Assert-True $line.noTradingStateMutation "line noTradingStateMutation $($line.lineId)"
    if ($null -ne $line.accountId -or $null -ne $line.portfolioId -or $null -ne $line.strategyId -or $null -ne $line.qubesRunId -or $null -ne $line.sourceExecutionIntentId) {
        Fail "Missing PMS/Qubes/account fields invented on preview line $($line.lineId)"
    }
}

$price = Read-Json (Join-Path $artifactDir "phase-ledger-state-r004-sandbox-price-delta-preview.json")
Assert-True $price.sandboxPriceDeltaOnly "price delta only"
Assert-True $price.notProductionPnl "price delta not production pnl"
Assert-True $price.notAccountingPnl "price delta not accounting pnl"
Assert-False $price.fullTheoreticalPnlProduced "full theoretical pnl"
if (@($price.rows).Count -ne 3) { Fail "Price delta rows must be 3" }
foreach ($row in $price.rows) {
    Assert-True $row.sandboxPriceDeltaOnly "row price delta only $($row.symbol)"
    Assert-True $row.notProductionPnl "row not production pnl $($row.symbol)"
    Assert-True $row.notAccountingPnl "row not accounting pnl $($row.symbol)"
    Assert-False $row.fullTheoreticalPnlProduced "row full theoretical pnl $($row.symbol)"
}

$pnlInputs = Read-Json (Join-Path $artifactDir "phase-ledger-state-r004-sandbox-pnl-preview-inputs.json")
Assert-True $pnlInputs.sandboxOnly "pnl inputs sandboxOnly"
Assert-False $pnlInputs.productionFill "pnl inputs productionFill"
Assert-True $pnlInputs.notProductionPnl "pnl inputs not production"
Assert-True $pnlInputs.notAccountingPnl "pnl inputs not accounting"
Assert-False $pnlInputs.fullTheoreticalPnlProduced "pnl inputs full theoretical"

$gross = Read-Json (Join-Path $artifactDir "phase-ledger-state-r004-gross-sandbox-price-delta-preview.json")
Assert-True $gross.grossSandboxPriceDeltaOnly "gross price delta only"
Assert-False $gross.fullPnlComputed "full pnl"
Assert-False $gross.productionPnlComputed "production pnl"
Assert-False $gross.accountingPnlComputed "accounting pnl"
Assert-False $gross.currencyAssigned "currency assigned"

$gaps = Read-Json (Join-Path $artifactDir "phase-ledger-state-r004-pnl-gap-diagnostics.json")
Assert-True $gaps.sandboxPriceDeltaOnlyReady "sandbox price delta ready"
Assert-False $gaps.fullSandboxTheoreticalPnlReady "full sandbox pnl"
Assert-False $gaps.productionPnlReady "production pnl"
Assert-False $gaps.accountingPnlReady "accounting pnl"
foreach ($field in @("MissingMarkPrices", "MissingCostSpreadCommissionModel", "MissingFxConversion", "MissingPositionCostBasisModel", "MissingAccountCurrency", "MissingAttributionPolicy")) {
    if ($gaps.missingInputs -notcontains $field) { Fail "Missing PnL gap $field" }
}
Assert-False $gaps.feesInvented "fees invented"
Assert-False $gaps.fxConversionInvented "fx invented"
Assert-False $gaps.accountCurrencyInvented "account currency invented"
Assert-False $gaps.markPricesInvented "mark prices invented"
Assert-False $gaps.attributionInvented "attribution invented"

$previewRecon = Read-Json (Join-Path $artifactDir "phase-ledger-state-r004-preview-reconciliation.json")
Assert-True $previewRecon.allResidualsZero "preview residuals zero"
if (@($previewRecon.breaks).Count -ne 0) { Fail "Preview breaks must be empty" }
Assert-False $previewRecon.ledgerMutation "preview reconciliation ledger mutation"
Assert-False $previewRecon.tradingStateMutation "preview reconciliation trading mutation"

$blockers = Read-Json (Join-Path $artifactDir "phase-ledger-state-r004-commit-blockers.json")
Assert-True $blockers.previewOnly "commit blockers previewOnly"
Assert-False $blockers.commitAllowed "commitAllowed"
Assert-False $blockers.paperLedgerCommitAllowed "paper ledger commit"
Assert-False $blockers.productionLedgerCommitAllowed "production ledger commit"
foreach ($blocker in @("MissingAccountId", "MissingPortfolioId", "MissingStrategyId", "MissingQubesRunId", "MissingSourceExecutionIntentId", "MissingMarkPrices", "MissingCostSpreadCommissionModel", "MissingFxConversion", "MissingPositionCostBasisModel", "MissingAccountCurrency", "MissingAttributionPolicy", "CommitSafeIdempotencyPolicyIncomplete")) {
    if ($blockers.blockers -notcontains $blocker) { Fail "Missing commit blocker $blocker" }
}

$decision = Read-Json (Join-Path $artifactDir "phase-ledger-state-r004-decision.json")
if ($decision.decision -ne "CrossRailPmsLinkedSandboxLedgerPreviewReadyWithCompletePriceDeltaOnly") { Fail "Unexpected R004 decision" }
Assert-True $decision.previewOnly "decision previewOnly"
Assert-False $decision.commitAllowed "decision commitAllowed"
Assert-False $decision.ledgerMutation "decision ledgerMutation"
Assert-False $decision.tradingStateMutation "decision tradingStateMutation"
Assert-False $decision.productionPnl "decision productionPnl"
Assert-False $decision.accountingPnl "decision accountingPnl"
Assert-False $decision.fullTheoreticalPnl "decision fullTheoreticalPnl"

foreach ($auditFile in @(
    "phase-ledger-state-r004-no-db-mutation-audit.json",
    "phase-ledger-state-r004-no-ledger-commit-audit.json",
    "phase-ledger-state-r004-no-production-ledger-audit.json",
    "phase-ledger-state-r004-no-trading-state-mutation-audit.json"
)) {
    $audit = Read-Json (Join-Path $artifactDir $auditFile)
    foreach ($prop in $audit.PSObject.Properties.Name) {
        if ($prop -ne "phase" -and $prop -ne "auditResult" -and $audit.$prop -eq $true) {
            Fail "$auditFile has true forbidden property $prop"
        }
    }
}

$noOrder = Read-Json (Join-Path $artifactDir "phase-ledger-state-r004-no-order-fill-route-audit.json")
if ($noOrder.ordersCreated -ne 0 -or $noOrder.routesCreated -ne 0 -or $noOrder.submissionsCreated -ne 0 -or $noOrder.fillsCreated -ne 0 -or $noOrder.executionReportsCreated -ne 0) {
    Fail "R004 created order/route/submission/fill/execution report"
}
Assert-True $noOrder.sourceSandboxFillsReusedOnly "source sandbox fills reused only"

$notProd = Read-Json (Join-Path $artifactDir "phase-ledger-state-r004-not-production-pnl-audit.json")
Assert-False $notProd.productionPnlComputed "production pnl computed"
Assert-False $notProd.productionPnlClaimedReady "production pnl claimed ready"
$notAcct = Read-Json (Join-Path $artifactDir "phase-ledger-state-r004-not-accounting-pnl-audit.json")
Assert-False $notAcct.accountingPnlComputed "accounting pnl computed"
Assert-False $notAcct.accountingPnlClaimedReady "accounting pnl claimed ready"
Assert-False $notAcct.realPnlComputedWithoutEvidence "real pnl without evidence"

$timing = Read-Json (Join-Path $artifactDir "phase-ledger-state-r004-canonical-timing-preservation.json")
Assert-True $timing.canonicalTargetCloseIsQuarterHour "canonical quarter hour"
Assert-False $timing.legacy06UsedAsFutureCanonical "legacy :06 canonical"
$direct = Read-Json (Join-Path $artifactDir "phase-ledger-state-r004-direct-cross-exclusion-preservation.json")
Assert-False $direct.directCrossExecutionAllowed "direct-cross execution"
Assert-False $direct.ledgerCreatesDirectCrossExposureFromRawQubesSignals "direct-cross ledger exposure"
$usdjpy = Read-Json (Join-Path $artifactDir "phase-ledger-state-r004-usdjpy-caveat-preservation.json")
Assert-True $usdjpy.usdjpyCaveatPreservedForFutureUse "USDJPY caveat"
if ($usdjpy.normalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.executionTradableSymbol -ne "USDJPY" -or $usdjpy.securityID -ne "4004" -or $usdjpy.securityIDSource -ne "8") {
    Fail "USDJPY caveat weakened"
}

$market = Read-Json (Join-Path $artifactDir "phase-ledger-state-r004-marketdata-execreport-boundary-preservation.json")
Assert-False $market.marketDataDbReadinessClaimedComplete "MarketData DB complete claim"
if ($market.marketDataStatus.'lmax-marketdata-db.v1' -ne "WITH_WARNINGS" -or $market.marketDataStatus.'marketdata-readiness.v1' -ne "WITH_WARNINGS") {
    Fail "MarketData WARN boundary not preserved"
}

$forbidden = Read-Json (Join-Path $artifactDir "phase-ledger-state-r004-forbidden-actions-audit.json")
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
    "sandboxPnlMisclassifiedAsProductionPnl",
    "priceDeltaMisclassifiedAsFullTheoreticalPnl",
    "accountingPnlComputed",
    "missingFieldsInvented",
    "legacy06UsedAsFutureCanonical",
    "directCrossExecutionAllowed",
    "usdJpyCaveatWeakened",
    "marketDataDbReadinessClaimedComplete",
    "r014BuildCaveatOmitted"
)) {
    Assert-False $forbidden.$name $name
}

$next = Read-Json (Join-Path $artifactDir "phase-ledger-state-r004-next-phase-recommendation.json")
if ($next.decision -ne "CrossRailPmsLinkedSandboxLedgerPreviewReadyWithCompletePriceDeltaOnly") { Fail "Next phase decision mismatch" }

$evidence = Read-Json (Join-Path $artifactDir "phase-ledger-state-r004-build-test-validator-evidence.json")
if ($evidence.build.result -ne "Passed") { Fail "Build evidence missing/pending" }
if ($evidence.focusedTests.result -ne "Passed" -or $evidence.focusedTests.passed -ne 5 -or $evidence.focusedTests.failed -ne 0) { Fail "Focused test evidence missing/pending" }
if ($evidence.focusedStaticChecks.result -ne "Passed") { Fail "Static check evidence missing/pending" }

Write-Output "LEDGER_STATE_R004_GATE_PASS_CROSS_RAIL_R014_PNL_PREVIEW_READY_NO_MUTATION"
