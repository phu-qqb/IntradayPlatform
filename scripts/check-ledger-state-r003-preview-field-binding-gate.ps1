param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "LEDGER_STATE_R003_GATE_FAIL: $Message"
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing required artifact: $Path"
    }

    return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Assert-False($Value, [string]$Name) {
    if ($Value -eq $true) {
        Fail "$Name must be false"
    }
}

function Assert-True($Value, [string]$Name) {
    if ($Value -ne $true) {
        Fail "$Name must be true"
    }
}

$artifactDir = Join-Path $Root "artifacts/readiness/ledger-state"
$required = @(
    "phase-ledger-state-r003-summary.md",
    "phase-ledger-state-r003-r002-reference.json",
    "phase-ledger-state-r003-pms-paper-r015-reference.json",
    "phase-ledger-state-r003-q4e-historical-only-confirmation.json",
    "phase-ledger-state-r003-existing-field-discovery.json",
    "phase-ledger-state-r003-field-binding-matrix.json",
    "phase-ledger-state-r003-hardened-paper-ledger-preview-contract.json",
    "phase-ledger-state-r003-bound-paper-ledger-preview-lines.json",
    "phase-ledger-state-r003-economic-field-gap-diagnostics.json",
    "phase-ledger-state-r003-commit-blockers.json",
    "phase-ledger-state-r003-idempotency-review.json",
    "phase-ledger-state-r003-cash-impact-diagnostics.json",
    "phase-ledger-state-r003-paper-ledger-separation-v1-adoption-wrapper.json",
    "phase-ledger-state-r003-pms-handoff-v1-mapping-status.json",
    "phase-ledger-state-r003-execution-intent-v1-mapping-status.json",
    "phase-ledger-state-r003-r009-sandbox-execution-v1-mapping-status.json",
    "phase-ledger-state-r003-oms-sandbox-state-model-v1-mapping-status.json",
    "phase-ledger-state-r003-no-db-mutation-audit.json",
    "phase-ledger-state-r003-no-ledger-commit-audit.json",
    "phase-ledger-state-r003-no-production-ledger-audit.json",
    "phase-ledger-state-r003-no-trading-state-mutation-audit.json",
    "phase-ledger-state-r003-no-order-fill-route-audit.json",
    "phase-ledger-state-r003-canonical-timing-preservation.json",
    "phase-ledger-state-r003-direct-cross-exclusion-preservation.json",
    "phase-ledger-state-r003-usdjpy-caveat-preservation.json",
    "phase-ledger-state-r003-marketdata-execreport-boundary-preservation.json",
    "phase-ledger-state-r003-forbidden-actions-audit.json",
    "phase-ledger-state-r003-next-phase-recommendation.json",
    "phase-ledger-state-r003-build-test-validator-evidence.json"
)

foreach ($name in $required) {
    $path = Join-Path $artifactDir $name
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing required artifact $name"
    }
}

$r002 = Read-Json (Join-Path $artifactDir "phase-ledger-state-r003-r002-reference.json")
Assert-True $r002.r002PreviewOnly "R002 preview reference"
Assert-False $r002.r002CommitAllowed "R002 commitAllowed"
Assert-False $r002.r002LedgerMutation "R002 ledgerMutation"
Assert-False $r002.r002TradingStateMutation "R002 tradingStateMutation"
if ($r002.r002LineCount -ne 14) { Fail "R002 line count must be 14" }

$pms = Read-Json (Join-Path $artifactDir "phase-ledger-state-r003-pms-paper-r015-reference.json")
Assert-True $pms.pmsPaperR015Passed "PMS-PAPER-R015 passed"
if ($pms.handoffReadiness -ne "HandoffContractDraftOnly") { Fail "PMS-PAPER-R015 handoff readiness must remain draft-only" }
if ($pms.lineBindingToR002SandboxFills -ne "NotBoundNoSharedFillOrClOrdIdLineage") { Fail "PMS-PAPER-R015 must not be falsely line-bound to R002 fills" }

$q4e = Read-Json (Join-Path $artifactDir "phase-ledger-state-r003-q4e-historical-only-confirmation.json")
if ($q4e.oldQubes4EStatus -ne "HistoricalOnlyNotActiveState") { Fail "Old Qubes 4E bootstrap treated as active" }
Assert-False $q4e.q4eUsedForBinding "Q4E used for binding"

$discovery = Read-Json (Join-Path $artifactDir "phase-ledger-state-r003-existing-field-discovery.json")
Assert-False $discovery.fieldsInvented "fieldsInvented"
if (@($discovery.fieldFindings | Where-Object { $_.field -eq "QubesRunId" -and $_.evidenceStatus -eq "PresentButZeroOnlyCurrentBranchNotApprovedForExecutablePmsInput" }).Count -ne 1) {
    Fail "Qubes ZeroOnly evidence must not be treated as approved PMS input"
}

$binding = Read-Json (Join-Path $artifactDir "phase-ledger-state-r003-field-binding-matrix.json")
if ($binding.boundFieldCount -ne 0) { Fail "R003 must not bind unlinked PMS/Qubes fields to R002 lines" }
Assert-False $binding.fieldsInvented "binding fieldsInvented"
Assert-True $binding.lineageBoundaryPreserved "lineage boundary"
foreach ($field in @("AccountId", "PortfolioId", "StrategyId", "PmsCycleId", "QubesRunId", "RiskReviewId", "OperatorApprovalId")) {
    $entry = @($binding.fieldMatrix | Where-Object { $_.field -eq $field })[0]
    if ($null -eq $entry) { Fail "Missing binding matrix field $field" }
    if ($entry.boundToPreviewLines -eq $true) { Fail "$field was bound without direct R002 line evidence" }
}

$contract = Read-Json (Join-Path $artifactDir "phase-ledger-state-r003-hardened-paper-ledger-preview-contract.json")
Assert-True $contract.previewOnly "contract previewOnly"
Assert-False $contract.commitAllowed "contract commitAllowed"
Assert-False $contract.ledgerMutation "contract ledgerMutation"
Assert-False $contract.tradingStateMutation "contract tradingStateMutation"
Assert-True $contract.missingEconomicFieldsAllowedForPreviewOnly "missing economic fields preview-only allowance"
Assert-True $contract.missingEconomicFieldsBlockCommit "missing economic fields block commit"
if ($contract.boundLineCount -ne 14) { Fail "hardened contract boundLineCount must be 14" }
foreach ($blocker in @("MissingAccountId", "MissingQubesRunId", "MissingRiskReviewId", "MissingOperatorApprovalId", "MissingCommissionFeeModel", "MissingFxConversionModel")) {
    if ($contract.commitBlockers -notcontains $blocker) { Fail "Missing contract blocker $blocker" }
}

$lines = Read-Json (Join-Path $artifactDir "phase-ledger-state-r003-bound-paper-ledger-preview-lines.json")
Assert-True $lines.previewOnly "lines previewOnly"
Assert-False $lines.commitAllowed "lines commitAllowed"
Assert-False $lines.ledgerMutation "lines ledgerMutation"
Assert-False $lines.tradingStateMutation "lines tradingStateMutation"
if ($lines.lineCount -ne 14 -or @($lines.lines).Count -ne 14) { Fail "Bound preview lines must contain 14 lines" }
if ($null -ne $lines.commonBoundFields.accountId -or
    $null -ne $lines.commonBoundFields.portfolioId -or
    $null -ne $lines.commonBoundFields.strategyId -or
    $null -ne $lines.commonBoundFields.pmsCycleId -or
    $null -ne $lines.commonBoundFields.qubesRunId -or
    $null -ne $lines.commonBoundFields.riskReviewId -or
    $null -ne $lines.commonBoundFields.operatorApprovalId) {
    Fail "Missing account/portfolio/strategy/PMS/Qubes/risk/operator fields were invented"
}
foreach ($line in $lines.lines) {
    Assert-True $line.previewOnly "line previewOnly $($line.lineId)"
    Assert-False $line.commitAllowed "line commitAllowed $($line.lineId)"
    Assert-False $line.ledgerMutation "line ledgerMutation $($line.lineId)"
    Assert-False $line.tradingStateMutation "line tradingStateMutation $($line.lineId)"
}

$gaps = Read-Json (Join-Path $artifactDir "phase-ledger-state-r003-economic-field-gap-diagnostics.json")
Assert-True $gaps.commitBlocked "economic gap commitBlocked"
if ($gaps.cashImpactStatus -ne "Incomplete") { Fail "cash impact must remain incomplete" }
Assert-False $gaps.missingFieldsInvented "economic missing fields invented"

$cash = Read-Json (Join-Path $artifactDir "phase-ledger-state-r003-cash-impact-diagnostics.json")
if ($cash.cashImpactStatus -ne "Incomplete") { Fail "cash impact is invented or misclassified" }
Assert-False $cash.cashImpactInvented "cashImpactInvented"
Assert-False $cash.cashMutation "cashMutation"

$sep = Read-Json (Join-Path $artifactDir "phase-ledger-state-r003-paper-ledger-separation-v1-adoption-wrapper.json")
Assert-True $sep.sandboxFillsMayBeReferencedForReview "sandbox reference"
Assert-False $sep.sandboxFillsCanMutatePaperLedger "sandbox mutates paper ledger"
Assert-False $sep.sandboxFillsCanMutateProductionLedger "sandbox mutates production ledger"
Assert-False $sep.sandboxFillsCanMutateProductionState "sandbox mutates production state"
Assert-True $sep.paperLedgerCommitRequiresFutureExplicitGate "future explicit gate"

$pmsMap = Read-Json (Join-Path $artifactDir "phase-ledger-state-r003-pms-handoff-v1-mapping-status.json")
Assert-False $pmsMap.fullAdoptionClaimed "PMS full adoption"
$execMap = Read-Json (Join-Path $artifactDir "phase-ledger-state-r003-execution-intent-v1-mapping-status.json")
Assert-False $execMap.boundToR002PreviewLines "execution intent bound to R002 lines"
Assert-False $execMap.fullAdoptionClaimed "execution full adoption"

$noDb = Read-Json (Join-Path $artifactDir "phase-ledger-state-r003-no-db-mutation-audit.json")
Assert-False $noDb.dbMutation "DB mutation"
Assert-False $noDb.migrationCreatedOrApplied "migration"
$noLedger = Read-Json (Join-Path $artifactDir "phase-ledger-state-r003-no-ledger-commit-audit.json")
Assert-False $noLedger.paperLedgerCommit "paper ledger commit"
Assert-False $noLedger.productionLedgerCommit "production ledger commit"
$noOrder = Read-Json (Join-Path $artifactDir "phase-ledger-state-r003-no-order-fill-route-audit.json")
if ($noOrder.ordersCreated -ne 0 -or $noOrder.routesCreated -ne 0 -or $noOrder.submissionsCreated -ne 0 -or $noOrder.fillsCreated -ne 0 -or $noOrder.executionReportsCreated -ne 0) {
    Fail "Order/route/submission/fill/execution report artifact created"
}

$timing = Read-Json (Join-Path $artifactDir "phase-ledger-state-r003-canonical-timing-preservation.json")
Assert-False $timing.legacy06UsedAsFutureCanonical "legacy :06 canonical"
$direct = Read-Json (Join-Path $artifactDir "phase-ledger-state-r003-direct-cross-exclusion-preservation.json")
Assert-False $direct.directCrossExecutionAllowed "direct-cross execution"
Assert-False $direct.ledgerCreatesDirectCrossExposureFromRawQubesSignals "direct-cross ledger exposure"
$usdjpy = Read-Json (Join-Path $artifactDir "phase-ledger-state-r003-usdjpy-caveat-preservation.json")
Assert-True $usdjpy.usdjpyCaveatPreserved "USDJPY caveat"
if ($usdjpy.normalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.executionTradableSymbol -ne "USDJPY" -or $usdjpy.securityID -ne "4004" -or $usdjpy.securityIDSource -ne "8") {
    Fail "USDJPY caveat weakened"
}

$market = Read-Json (Join-Path $artifactDir "phase-ledger-state-r003-marketdata-execreport-boundary-preservation.json")
Assert-False $market.marketDataDbReadinessClaimedComplete "MarketData DB complete claim"
if ($market.marketDataStatus.'lmax-marketdata-db.v1' -ne "WITH_WARNINGS" -or $market.marketDataStatus.'marketdata-readiness.v1' -ne "WITH_WARNINGS") {
    Fail "MarketData WARN status not preserved"
}

$forbidden = Read-Json (Join-Path $artifactDir "phase-ledger-state-r003-forbidden-actions-audit.json")
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
    "orderRouteSubmissionFillExecutionReportCreated",
    "ledgerCommit",
    "stateMutation",
    "productionLivePromotion",
    "legacy06UsedAsFutureCanonical",
    "fieldsInvented"
)) {
    Assert-False $forbidden.$name $name
}

$next = Read-Json (Join-Path $artifactDir "phase-ledger-state-r003-next-phase-recommendation.json")
if ($next.decision -ne "PaperLedgerPreviewHardenedWithMissingEconomicFields") { Fail "Unexpected R003 decision" }

$evidence = Read-Json (Join-Path $artifactDir "phase-ledger-state-r003-build-test-validator-evidence.json")
if ($evidence.build.result -ne "Passed") { Fail "Build evidence missing or not passed" }
if ($evidence.focusedTests.result -ne "Passed" -or $evidence.focusedTests.passed -ne 5 -or $evidence.focusedTests.failed -ne 0) { Fail "Focused test evidence missing or not passed" }
if ($evidence.focusedStaticChecks.result -ne "Passed") { Fail "Static check evidence missing or not passed" }

Write-Output "LEDGER_STATE_R003_GATE_PASS_PREVIEW_FIELD_BINDING_READY_NO_MUTATION"
