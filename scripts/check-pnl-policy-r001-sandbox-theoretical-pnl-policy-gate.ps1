param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "PNL_POLICY_R001_GATE_FAIL: $Message"
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

$artifactDir = Join-Path $Root "artifacts/readiness/pnl-policy"
$required = @(
    "phase-pnl-policy-r001-summary.md",
    "phase-pnl-policy-r001-economic-readiness-reference.json",
    "phase-pnl-policy-r001-pnl-level-separation.json",
    "phase-pnl-policy-r001-mark-price-policy-options.json",
    "phase-pnl-policy-r001-cost-spread-commission-policy-options.json",
    "phase-pnl-policy-r001-fx-account-currency-policy-requirements.json",
    "phase-pnl-policy-r001-position-cost-basis-policy-options.json",
    "phase-pnl-policy-r001-attribution-policy-requirements.json",
    "phase-pnl-policy-r001-commit-safe-idempotency-policy-requirements.json",
    "phase-pnl-policy-r001-present-vs-missing-inputs.json",
    "phase-pnl-policy-r001-operator-policy-decisions-required.json",
    "phase-pnl-policy-r001-decision.json",
    "phase-pnl-policy-r001-no-external-audit.json",
    "phase-pnl-policy-r001-no-execution-audit.json",
    "phase-pnl-policy-r001-no-db-mutation-audit.json",
    "phase-pnl-policy-r001-no-order-fill-route-audit.json",
    "phase-pnl-policy-r001-no-ledger-state-mutation-audit.json",
    "phase-pnl-policy-r001-canonical-timing-preservation.json",
    "phase-pnl-policy-r001-direct-cross-exclusion-preservation.json",
    "phase-pnl-policy-r001-usdjpy-caveat-preservation.json",
    "phase-pnl-policy-r001-forbidden-actions-audit.json",
    "phase-pnl-policy-r001-next-phase-recommendation.json",
    "phase-pnl-policy-r001-build-test-validator-evidence.json"
)

foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $name))) {
        Fail "Missing required artifact $name"
    }
}

$reference = Read-Json (Join-Path $artifactDir "phase-pnl-policy-r001-economic-readiness-reference.json")
Assert-True $reference.economicReadinessPassed "economic readiness passed"
if ($reference.currentCeiling -ne "SandboxPriceDeltaOnlyReady") { Fail "current ceiling must remain SandboxPriceDeltaOnlyReady" }
foreach ($blocker in @("MissingMarkPrices", "MissingCostSpreadCommissionModel", "MissingFxConversion", "MissingPositionCostBasisModel", "MissingAccountCurrency", "MissingAttributionPolicy", "MissingAccountId", "MissingPortfolioId", "MissingStrategyId", "MissingSourceExecutionIntentId", "QubesRunIdNotPmsApprovedEconomicOutput", "CommitSafeIdempotencyPolicyIncomplete", "MarketDataWarnM30Missing", "MarketDataWarnRowCountsMissing", "MarketDataWarnTickSchemaPartial")) {
    if ($reference.hardBlockers -notcontains $blocker) { Fail "Missing economic-readiness blocker $blocker" }
}
Assert-False $reference.fullPnlReadinessClaimed "full pnl readiness"
Assert-False $reference.productionPnlReadinessClaimed "production pnl readiness"
Assert-False $reference.accountingPnlReadinessClaimed "accounting pnl readiness"

$levels = Read-Json (Join-Path $artifactDir "phase-pnl-policy-r001-pnl-level-separation.json")
if ($levels.currentReadyLevel -ne "SandboxPriceDeltaOnly") { Fail "Only SandboxPriceDeltaOnly may be ready" }
foreach ($level in @("SandboxTheoreticalPnl", "PaperAccountingPnl", "ProductionPnl", "LedgerCommit")) {
    $entry = @($levels.levels | Where-Object { $_.level -eq $level })[0]
    if ($null -eq $entry) { Fail "Missing PnL level $level" }
    Assert-False $entry.allowedNow "$level allowedNow"
    Assert-False $entry.claimReadiness "$level claimReadiness"
}
Assert-False $levels.paperAccountingPnlReadinessClaimed "paper accounting pnl readiness"
Assert-False $levels.productionPnlReadinessClaimed "production pnl readiness"
Assert-False $levels.ledgerCommitEnabled "ledger commit enabled"

$marks = Read-Json (Join-Path $artifactDir "phase-pnl-policy-r001-mark-price-policy-options.json")
Assert-False $marks.markPricesInvented "mark prices invented"
if ($marks.selectedForCurrentGate -ne "UseFillToFillPriceDeltaOnly") { Fail "current mark policy must remain fill-to-fill price delta only" }
if ($null -ne $marks.selectedForFutureSandboxTheoreticalPnl) { Fail "future mark policy must not be selected in R001" }
foreach ($option in @("UseCloseBenchmark", "UseMarkAtCanonicalTargetClose", "UseLastValidQuoteBeforeClose", "UseDbBarClose")) {
    $entry = @($marks.options | Where-Object { $_.option -eq $option })[0]
    if ($null -eq $entry) { Fail "Missing mark option $option" }
    if ([string]$entry.classification -eq "Available") { Fail "$option must not be available without evidence" }
}
if ($marks.blockers -notcontains "MissingMarkPrices") { Fail "MissingMarkPrices blocker required" }

$cost = Read-Json (Join-Path $artifactDir "phase-pnl-policy-r001-cost-spread-commission-policy-options.json")
Assert-False $cost.costSpreadCommissionModelFalselyClaimedFinal "cost model final"
Assert-False $cost.costsInvented "costs invented"
$five = @($cost.options | Where-Object { $_.option -eq "FiveUsdPerMillionMajorOnlyGuidance" })[0]
Assert-True $five.majorOnly "5 USD/million major only"
Assert-False $five.universal "5 USD/million universal"
Assert-False $five.finalModel "5 USD/million final model"
if ($cost.blocker -ne "MissingCostSpreadCommissionModel") { Fail "cost blocker mismatch" }

$fx = Read-Json (Join-Path $artifactDir "phase-pnl-policy-r001-fx-account-currency-policy-requirements.json")
Assert-False $fx.accountCurrencyInvented "account currency invented"
Assert-False $fx.fxConversionInvented "FX invented"
foreach ($field in @("AccountCurrency", "ConversionPair", "ConversionTimestamp", "ConversionSource")) {
    $entry = @($fx.requirements | Where-Object { $_.field -eq $field })[0]
    if ($null -eq $entry) { Fail "Missing FX requirement $field" }
    if ([string]$entry.status -eq "Present") { Fail "$field must not be present" }
}

$basis = Read-Json (Join-Path $artifactDir "phase-pnl-policy-r001-position-cost-basis-policy-options.json")
Assert-False $basis.costBasisInvented "cost basis invented"
Assert-True $basis.r014ResidualsZero "R014 residuals zero"
Assert-True $basis.flatOnlyPreviewSufficientForPriceDelta "flat preview price delta"
Assert-False $basis.flatOnlyPreviewSufficientForAccountingPnl "flat preview accounting pnl"
if ($basis.blocker -ne "MissingPositionCostBasisModel") { Fail "basis blocker mismatch" }

$attrib = Read-Json (Join-Path $artifactDir "phase-pnl-policy-r001-attribution-policy-requirements.json")
Assert-False $attrib.attributionPolicyInvented "attribution invented"
foreach ($field in @("AccountId", "PortfolioId", "StrategyId", "SourceExecutionIntentId")) {
    $entry = @($attrib.dimensions | Where-Object { $_.field -eq $field })[0]
    if ($null -eq $entry) { Fail "Missing attribution dimension $field" }
    if ([string]$entry.status -ne "Missing") { Fail "$field must remain missing" }
}
$qubes = @($attrib.dimensions | Where-Object { $_.field -eq "QubesRunId" })[0]
if ([string]$qubes.status -ne "PresentWithWarningNotPmsApprovedEconomicOutput") { Fail "QubesRunId warning weakened" }
if ($attrib.blocker -ne "MissingAttributionPolicy") { Fail "attribution blocker mismatch" }

$idempotency = Read-Json (Join-Path $artifactDir "phase-pnl-policy-r001-commit-safe-idempotency-policy-requirements.json")
Assert-False $idempotency.commitEnabled "commit enabled"
Assert-True $idempotency.ledgerCommitBlocked "ledger commit blocked"
if ($idempotency.blocker -ne "CommitSafeIdempotencyPolicyIncomplete") { Fail "idempotency blocker mismatch" }
$sourceIntent = @($idempotency.requirements | Where-Object { $_.field -eq "SourceExecutionIntentId" })[0]
if ([string]$sourceIntent.status -ne "Missing") { Fail "SourceExecutionIntentId must remain missing" }
$noCommit = @($idempotency.requirements | Where-Object { $_.field -eq "NoDoubleCommit" })[0]
if ([string]$noCommit.status -ne "BlockedUntilFutureCommitGate") { Fail "NoDoubleCommit must remain future-gated" }

$inputs = Read-Json (Join-Path $artifactDir "phase-pnl-policy-r001-present-vs-missing-inputs.json")
Assert-True $inputs.doNotInventMissingInputs "do not invent inputs"
foreach ($missing in @("MissingMarkPrices", "MissingCostSpreadCommissionModel", "MissingFxConversion", "MissingPositionCostBasisModel", "MissingAccountCurrency", "MissingAttributionPolicy", "MissingAccountId", "MissingPortfolioId", "MissingStrategyId", "MissingSourceExecutionIntentId", "QubesRunIdNotPmsApprovedEconomicOutput", "CommitSafeIdempotencyPolicyIncomplete")) {
    if ($inputs.missingInputs -notcontains $missing) { Fail "Missing input blocker $missing" }
}

$operator = Read-Json (Join-Path $artifactDir "phase-pnl-policy-r001-operator-policy-decisions-required.json")
Assert-False $operator.operatorPolicyInvented "operator policy invented"
foreach ($decision in @("SelectSandboxMarkPricePolicy", "ApproveSandboxCostModel", "ApproveAccountCurrencyAndFxPolicy", "ApprovePositionCostBasisPolicy", "ApproveAttributionPolicy", "ApproveCommitSafeIdempotencyPolicy")) {
    if (-not (@($operator.requiredOperatorDecisions | Where-Object { $_.decision -eq $decision })[0])) { Fail "Missing operator decision $decision" }
}

$decisionArtifact = Read-Json (Join-Path $artifactDir "phase-pnl-policy-r001-decision.json")
foreach ($d in @("SandboxTheoreticalPnlPolicyDraftReady", "SandboxTheoreticalPnlStillBlockedByMissingEvidence", "PaperAccountingPnlBlocked", "ProductionPnlBlocked", "LedgerCommitBlocked")) {
    if ($decisionArtifact.decisions -notcontains $d) { Fail "Missing decision $d" }
}
Assert-False $decisionArtifact.sandboxTheoreticalPnlReady "sandbox theoretical pnl ready"
Assert-False $decisionArtifact.paperAccountingPnlReady "paper accounting pnl ready"
Assert-False $decisionArtifact.productionPnlReady "production pnl ready"
Assert-False $decisionArtifact.ledgerCommitReady "ledger commit ready"
Assert-False $decisionArtifact.productionPnlReadinessClaimed "production pnl claim"
Assert-False $decisionArtifact.accountingPnlReadinessClaimed "accounting pnl claim"

foreach ($auditFile in @(
    "phase-pnl-policy-r001-no-external-audit.json",
    "phase-pnl-policy-r001-no-execution-audit.json",
    "phase-pnl-policy-r001-no-db-mutation-audit.json",
    "phase-pnl-policy-r001-no-ledger-state-mutation-audit.json"
)) {
    $audit = Read-Json (Join-Path $artifactDir $auditFile)
    foreach ($prop in $audit.PSObject.Properties.Name) {
        if ($prop -ne "phase" -and $prop -ne "auditResult" -and $audit.$prop -eq $true) {
            Fail "$auditFile has true forbidden property $prop"
        }
    }
}

$noOrder = Read-Json (Join-Path $artifactDir "phase-pnl-policy-r001-no-order-fill-route-audit.json")
if ($noOrder.ordersCreated -ne 0 -or $noOrder.routesCreated -ne 0 -or $noOrder.submissionsCreated -ne 0 -or $noOrder.newFillsCreated -ne 0 -or $noOrder.newExecutionReportsCreated -ne 0 -or $noOrder.executableSchedulesCreated -ne 0) {
    Fail "order/fill/route artifacts created"
}

$timing = Read-Json (Join-Path $artifactDir "phase-pnl-policy-r001-canonical-timing-preservation.json")
Assert-True $timing.futureTimestampsUseCanonicalQuarterHour "canonical quarter-hour"
Assert-False $timing.legacy06UsedAsFutureCanonical "legacy :06 future canonical"

$direct = Read-Json (Join-Path $artifactDir "phase-pnl-policy-r001-direct-cross-exclusion-preservation.json")
Assert-False $direct.directCrossExecutionAllowed "direct-cross execution"
Assert-False $direct.pnlLedgerCreatesDirectCrossExecutionExposureFromRawQubesSignals "direct-cross PnL exposure"

$usdjpy = Read-Json (Join-Path $artifactDir "phase-pnl-policy-r001-usdjpy-caveat-preservation.json")
Assert-True $usdjpy.usdjpyCaveatPreserved "USDJPY caveat"
Assert-False $usdjpy.usdJpyCaveatWeakened "USDJPY caveat weakened"
if ($usdjpy.normalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.executionTradableSymbol -ne "USDJPY" -or $usdjpy.securityID -ne "4004" -or $usdjpy.securityIDSource -ne "8") {
    Fail "USDJPY caveat values changed"
}

$forbidden = Read-Json (Join-Path $artifactDir "phase-pnl-policy-r001-forbidden-actions-audit.json")
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
    "costSpreadCommissionModelFalselyClaimedFinal",
    "fxConversionInvented",
    "accountCurrencyInvented",
    "attributionPolicyInvented",
    "realAccountingProductionPnlClaimed",
    "legacy06UsedAsFutureCanonical",
    "directCrossExecutionAllowed",
    "usdJpyCaveatWeakened",
    "marketDataDbReadinessClaimedComplete"
)) {
    Assert-False $forbidden.$name $name
}

$next = Read-Json (Join-Path $artifactDir "phase-pnl-policy-r001-next-phase-recommendation.json")
if ($next.decision -ne "SandboxTheoreticalPnlPolicyDraftReadyButStillBlockedByMissingEvidence") { Fail "next phase decision mismatch" }
foreach ($blocked in @("PaperAccountingPnl", "ProductionPnl", "LedgerCommit")) {
    if ($next.doNotProceedTo -notcontains $blocked) { Fail "Missing do-not-proceed target $blocked" }
}

$evidence = Read-Json (Join-Path $artifactDir "phase-pnl-policy-r001-build-test-validator-evidence.json")
if ($evidence.build.result -ne "Passed") { Fail "build evidence missing/pending" }
if ($evidence.focusedStaticChecks.result -ne "Passed") { Fail "static check evidence missing/pending" }

Write-Output "PNL_POLICY_R001_GATE_PASS_SANDBOX_THEORETICAL_PNL_POLICY_READY_NO_MUTATION"
