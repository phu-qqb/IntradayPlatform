param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "PMS_QUBES_IDENTITY_R001_GATE_FAIL: $Message"
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

$artifactDir = Join-Path $Root "artifacts/readiness/pms-qubes-identity"
$required = @(
    "phase-pms-qubes-identity-r001-summary.md",
    "phase-pms-qubes-identity-r001-product-readiness-r003-reference.json",
    "phase-pms-qubes-identity-r001-pms-paper-r015-reference.json",
    "phase-pms-qubes-identity-r001-cross-rail-r014-reference.json",
    "phase-pms-qubes-identity-r001-q4e-historical-only-confirmation.json",
    "phase-pms-qubes-identity-r001-identity-field-discovery.json",
    "phase-pms-qubes-identity-r001-field-binding-matrix.json",
    "phase-pms-qubes-identity-r001-qubes-runid-economic-approval-review.json",
    "phase-pms-qubes-identity-r001-source-execution-intentid-review.json",
    "phase-pms-qubes-identity-r001-account-portfolio-strategy-review.json",
    "phase-pms-qubes-identity-r001-account-currency-review.json",
    "phase-pms-qubes-identity-r001-attribution-policy-review.json",
    "phase-pms-qubes-identity-r001-contract-adoption-impact.json",
    "phase-pms-qubes-identity-r001-ledger-pnl-impact.json",
    "phase-pms-qubes-identity-r001-missing-field-diagnostics.json",
    "phase-pms-qubes-identity-r001-decision.json",
    "phase-pms-qubes-identity-r001-no-execution-audit.json",
    "phase-pms-qubes-identity-r001-no-db-mutation-audit.json",
    "phase-pms-qubes-identity-r001-no-order-fill-route-audit.json",
    "phase-pms-qubes-identity-r001-no-ledger-state-mutation-audit.json",
    "phase-pms-qubes-identity-r001-canonical-timing-preservation.json",
    "phase-pms-qubes-identity-r001-direct-cross-exclusion-preservation.json",
    "phase-pms-qubes-identity-r001-usdjpy-caveat-preservation.json",
    "phase-pms-qubes-identity-r001-forbidden-actions-audit.json",
    "phase-pms-qubes-identity-r001-next-phase-recommendation.json",
    "phase-pms-qubes-identity-r001-build-test-validator-evidence.json"
)

foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $name))) {
        Fail "Missing required artifact $name"
    }
}

$product = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r001-product-readiness-r003-reference.json")
Assert-True $product.sandboxGrossRoundTripPnlPreviewV0Ready "gross pnl V0"
Assert-False $product.netPnlReady "net pnl"
Assert-False $product.accountingPnlReady "accounting pnl"
Assert-False $product.productionPnlReady "production pnl"
Assert-False $product.ledgerCommitReady "ledger commit"
Assert-False $product.productionLiveReady "production live"

$pms = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r001-pms-paper-r015-reference.json")
Assert-True $pms.pmsPaperR015Passed "PMS-PAPER-R015 passed"
Assert-True $pms.sourceContainsNoOrders "R015 source no orders"
Assert-True $pms.sourceContainsNoRoutes "R015 source no routes"
Assert-True $pms.sourceContainsNoFills "R015 source no fills"
Assert-False $pms.accountOrPaperAccountIdAvailableInR015 "R015 account id"

$cross = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r001-cross-rail-r014-reference.json")
Assert-True $cross.crossRailR014Passed "CROSS-RAIL-R014 passed"
Assert-True $cross.sandboxOnly "R014 sandbox only"
Assert-False $cross.productionFill "R014 production fill"
Assert-True $cross.notProductionPnl "R014 not production pnl"
Assert-False $cross.sandboxAccountProfileIsAccountId "sandbox profile as AccountId"

$q4e = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r001-q4e-historical-only-confirmation.json")
Assert-False $q4e.q4eBootstrapActiveState "Q4E active"
Assert-False $q4e.stratTakenBootstrapActiveState "StratTaken active"
Assert-True $q4e.q4eHistoricalOnly "Q4E historical"
Assert-True $q4e.stratTakenHistoricalOnly "StratTaken historical"
Assert-False $q4e.zeroOnlyTreatedAsPmsApprovedEconomicOutput "ZeroOnly PMS approved"
Assert-False $q4e.qubesRunPerformedByR001 "Qubes run"
Assert-False $q4e.executionPerformedByR001 "execution"

$discovery = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r001-identity-field-discovery.json")
Assert-True $discovery.discoveryOnly "discovery only"
Assert-False $discovery.fieldsInvented "fields invented"
foreach ($field in @("AccountId", "PortfolioId", "StrategyId", "SourceExecutionIntentId", "AccountCurrency", "AttributionPolicy", "PMSApprovedQubesRunId")) {
    $row = @($discovery.fields | Where-Object { $_.fieldName -eq $field })[0]
    if ($null -eq $row) { Fail "Missing discovery field $field" }
    if ($null -ne $row.value) { Fail "$field should remain null" }
    if ($row.evidenceStatus -ne "Missing") { Fail "$field status should be Missing" }
}

$binding = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r001-field-binding-matrix.json")
Assert-False $binding.fieldsInvented "binding fields invented"
Assert-False $binding.sandboxAccountProfilePromotedToAccountId "sandbox profile promoted"
foreach ($field in @("AccountId", "PortfolioId", "StrategyId", "SourceExecutionIntentId", "AccountCurrency", "AttributionPolicy", "PMS-approved QubesRunId")) {
    if ($binding.stillMissing -notcontains $field) { Fail "Missing still-missing field $field" }
}
$sandboxProfile = @($binding.boundFields | Where-Object { $_.fieldName -eq "SandboxAccountProfile" })[0]
if ($null -eq $sandboxProfile -or $sandboxProfile.value -ne "ExistingLmaxDemoProfile") { Fail "SandboxAccountProfile missing" }

$qubes = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r001-qubes-runid-economic-approval-review.json")
if ($qubes.currentQubesRunIdStatus -ne "PresentWithWarnings_NotPmsApprovedEconomicOutput") { Fail "QubesRunId warning status hidden" }
if ($null -ne $qubes.pmsApprovedQubesRunId) { Fail "PMS-approved QubesRunId should be null" }
Assert-False $qubes.pmsApprovedQubesEconomicOutputReady "PMS-approved Qubes economic output"
Assert-False $qubes.zeroOnlyTreatedAsPmsApprovedEconomicOutput "ZeroOnly as PMS approved"
Assert-False $qubes.canDriveAccountingPnl "Qubes can drive accounting PnL"
Assert-False $qubes.canDriveLedgerCommit "Qubes can drive ledger"
Assert-False $qubes.canDriveProduction "Qubes can drive production"

$execIntent = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r001-source-execution-intentid-review.json")
if ($execIntent.status -ne "MissingSourceExecutionIntentId") { Fail "SourceExecutionIntentId status mismatch" }
Assert-False $execIntent.executionIntentV1CompatibleIdFound "execution-intent.v1 id"
Assert-False $execIntent.sandboxOrderIdsUsedAsSourceExecutionIntentId "sandbox order IDs as source execution intent"
Assert-False $execIntent.idempotencyKeysUsedAsSourceExecutionIntentId "idempotency as source execution intent"
Assert-False $execIntent.synthesizedFromFillOrOrderIds "synthesized source execution intent"

$identity = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r001-account-portfolio-strategy-review.json")
if ($null -ne $identity.accountId -or $null -ne $identity.portfolioId -or $null -ne $identity.strategyId) { Fail "account/portfolio/strategy invented" }
if ($identity.sandboxAccountProfile -ne "ExistingLmaxDemoProfile") { Fail "sandbox account profile missing" }
Assert-False $identity.sandboxAccountProfilePromotedToAccountId "sandbox profile promoted to AccountId"

$currency = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r001-account-currency-review.json")
if ($null -ne $currency.accountCurrency) { Fail "account currency invented" }
Assert-False $currency.accountCurrencyInferredFromQuoteCurrency "account currency inferred"
Assert-False $currency.fxConversionReady "FX ready"
Assert-False $currency.accountCurrencyPnlReady "account-currency PnL"

$attribution = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r001-attribution-policy-review.json")
if ($null -ne $attribution.attributionPolicy) { Fail "attribution policy invented" }
Assert-False $attribution.attributionPolicyClaimedFinal "attribution final"
Assert-False $attribution.accountingAttributionReady "accounting attribution"
Assert-True $attribution.attributionPolicyCandidate.operatorApprovalRequired "candidate operator approval"
if ($attribution.attributionPolicyCandidate.status -ne "ProposalOnly_OperatorApprovalRequired") { Fail "attribution candidate status mismatch" }

$contracts = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r001-contract-adoption-impact.json")
$expectedContracts = @{
    "pms-handoff.v1" = "Partial"
    "qubes-output.v1" = "Blocked"
    "execution-intent.v1" = "Partial"
    "risk-control.v1" = "AdoptedWithWarnings"
    "paper-ledger-separation.v1" = "AdoptedWithWarnings"
}
foreach ($contractId in $expectedContracts.Keys) {
    $row = @($contracts.contracts | Where-Object { $_.contractId -eq $contractId })[0]
    if ($null -eq $row) { Fail "Missing contract impact $contractId" }
    if ($row.status -ne $expectedContracts[$contractId]) { Fail "$contractId expected $($expectedContracts[$contractId]) but found $($row.status)" }
}

$impact = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r001-ledger-pnl-impact.json")
if (@($impact.identityFieldsResolvedByR001).Count -ne 0) { Fail "identity fields resolved unexpectedly" }
Assert-True $impact.sandboxGrossPnlPreviewV0Ready "sandbox gross PnL V0"
Assert-False $impact.fullSandboxTheoreticalPnlReady "full theoretical"
Assert-False $impact.netPnlReady "net pnl"
Assert-False $impact.paperAccountingPnlReady "paper accounting"
Assert-False $impact.ledgerCommitReady "ledger commit"
Assert-False $impact.productionPnlReady "production pnl"
Assert-False $impact.productionLiveReady "production live"

$missing = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r001-missing-field-diagnostics.json")
Assert-False $missing.missingFieldsInvented "missing fields invented"
foreach ($field in @("AccountId", "PortfolioId", "StrategyId", "SourceExecutionIntentId", "AccountCurrency", "AttributionPolicy", "PMS-approved QubesRunId")) {
    if (@($missing.missingFields | Where-Object { $_.field -eq $field }).Count -ne 1) { Fail "Missing diagnostic for $field" }
}

$decision = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r001-decision.json")
if ($decision.decision -ne "PmsQubesIdentityStillBlockedMissingCoreFields") { Fail "decision mismatch" }
foreach ($classification in @("PMS_QUBES_IDENTITY_R001_PASS_IDENTITY_FIELD_DISCOVERY_READY_NO_EXECUTION", "PMS_QUBES_IDENTITY_R001_PASS_PMS_QUBES_ADOPTION_IMPACT_READY_NO_MUTATION", "PMS_QUBES_IDENTITY_R001_PASS_LEDGER_PNL_IMPACT_READY_NO_MUTATION", "PMS_QUBES_IDENTITY_R001_PASS_PRODUCTION_BLOCKERS_PRESERVED")) {
    if ($decision.classifications -notcontains $classification) { Fail "Missing classification $classification" }
}
Assert-False $decision.identityPackageReady "identity ready"
Assert-True $decision.blockedMissingCoreFields "blocked missing core fields"
Assert-False $decision.lineageFieldsInvented "lineage invented"
Assert-False $decision.sandboxAccountProfilePromotedToAccountId "sandbox profile promoted"
Assert-False $decision.qubesRunIdWarningHidden "Qubes warning hidden"
Assert-False $decision.attributionPolicyClaimedFinal "attribution final"
Assert-False $decision.accountingPnlReady "accounting pnl"
Assert-False $decision.productionPnlReady "production pnl"
Assert-False $decision.ledgerCommitReady "ledger commit"
Assert-False $decision.productionLiveReady "production live"

foreach ($auditFile in @(
    "phase-pms-qubes-identity-r001-no-execution-audit.json",
    "phase-pms-qubes-identity-r001-no-db-mutation-audit.json",
    "phase-pms-qubes-identity-r001-no-ledger-state-mutation-audit.json"
)) {
    $audit = Read-Json (Join-Path $artifactDir $auditFile)
    foreach ($prop in $audit.PSObject.Properties.Name) {
        if ($prop -ne "phase" -and $prop -ne "auditResult" -and $audit.$prop -eq $true) {
            Fail "$auditFile has true forbidden property $prop"
        }
    }
}

$noOrder = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r001-no-order-fill-route-audit.json")
if ($noOrder.ordersCreated -ne 0 -or $noOrder.routesCreated -ne 0 -or $noOrder.submissionsCreated -ne 0 -or $noOrder.fillsCreated -ne 0 -or $noOrder.executionReportsCreated -ne 0 -or $noOrder.executableSchedulesCreated -ne 0) {
    Fail "order/fill/route/report artifacts created"
}

$timing = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r001-canonical-timing-preservation.json")
Assert-True $timing.futureTimestampsUseCanonicalQuarterHour "canonical quarter-hour"
Assert-False $timing.legacy06UsedAsFutureCanonical "legacy :06 future canonical"

$direct = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r001-direct-cross-exclusion-preservation.json")
Assert-False $direct.directCrossExecutionAllowed "direct-cross execution"
Assert-True $direct.directCrossesSignalOnly "direct crosses signal only"
Assert-True $direct.nettingFirstRequired "netting first"

$usdjpy = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r001-usdjpy-caveat-preservation.json")
Assert-True $usdjpy.usdjpyCaveatPreserved "USDJPY caveat"
Assert-False $usdjpy.usdJpyCaveatWeakened "USDJPY caveat weakened"
if ($usdjpy.normalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.executionTradableSymbol -ne "USDJPY" -or $usdjpy.securityID -ne "4004" -or $usdjpy.securityIDSource -ne "8") {
    Fail "USDJPY caveat values changed"
}

$forbidden = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r001-forbidden-actions-audit.json")
foreach ($name in @(
    "q4eStratTakenTreatedAsActiveState",
    "lmaxCalled",
    "polygonCalled",
    "externalApiCalled",
    "brokerActivated",
    "liveMarketDataRequested",
    "qubesExecutableRun",
    "pythonCppCudaWorkloadRun",
    "pmsEmsOmsExecutionCycleRun",
    "manualNoExternalRun",
    "dbMutation",
    "orderRouteSubmissionFillExecutionReportCreated",
    "ledgerCommit",
    "tradingStateMutation",
    "missingIdentityFieldsInvented",
    "sandboxAccountProfileMisclassifiedAsAccountId",
    "qubesRunIdWarningHidden",
    "attributionPolicyClaimedFinalWithoutEvidence",
    "directCrossExecutionAllowed",
    "legacy06UsedAsFutureCanonical",
    "usdJpyCaveatWeakened",
    "accountingProductionPnlReadinessClaimed",
    "productionLivePromoted"
)) {
    Assert-False $forbidden.$name $name
}

$next = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r001-next-phase-recommendation.json")
if ($next.recommendedNextPhase -ne "PMS-QUBES-IDENTITY-R002") { Fail "next phase mismatch" }
foreach ($blocked in @("AccountingPnl", "ProductionPnl", "LedgerCommit", "ProductionLive")) {
    if ($next.doNotProceedTo -notcontains $blocked) { Fail "Missing do-not-proceed $blocked" }
}

$evidence = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r001-build-test-validator-evidence.json")
if ($evidence.build.result -ne "Passed") { Fail "build evidence missing/pending" }
if ($evidence.focusedStaticChecks.result -ne "Passed") { Fail "static check evidence missing/pending" }
if ($evidence.validator.result -ne "Passed") { Fail "validator evidence missing/pending" }
if ($evidence.focusedTests.result -ne "NotApplicable") { Fail "focused tests should be NotApplicable for static R001" }

Write-Output "PMS_QUBES_IDENTITY_R001_GATE_PASS_DISCOVERY_IMPACT_BLOCKERS_PRESERVED_NO_MUTATION"

