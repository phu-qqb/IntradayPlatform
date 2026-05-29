param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "PMS_QUBES_IDENTITY_R002_GATE_FAIL: $Message"
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
    "phase-pms-qubes-identity-r002-summary.md",
    "phase-pms-qubes-identity-r002-r001-reference.json",
    "phase-pms-qubes-identity-r002-operator-identity-decisions.json",
    "phase-pms-qubes-identity-r002-field-binding-matrix-updated.json",
    "phase-pms-qubes-identity-r002-sandbox-preview-attribution-policy.json",
    "phase-pms-qubes-identity-r002-qubes-runid-policy.json",
    "phase-pms-qubes-identity-r002-ledger-pnl-impact.json",
    "phase-pms-qubes-identity-r002-contract-adoption-impact.json",
    "phase-pms-qubes-identity-r002-preserved-blockers.json",
    "phase-pms-qubes-identity-r002-decision.json",
    "phase-pms-qubes-identity-r002-no-execution-audit.json",
    "phase-pms-qubes-identity-r002-no-db-mutation-audit.json",
    "phase-pms-qubes-identity-r002-no-order-fill-route-audit.json",
    "phase-pms-qubes-identity-r002-no-ledger-state-mutation-audit.json",
    "phase-pms-qubes-identity-r002-canonical-timing-preservation.json",
    "phase-pms-qubes-identity-r002-direct-cross-exclusion-preservation.json",
    "phase-pms-qubes-identity-r002-usdjpy-caveat-preservation.json",
    "phase-pms-qubes-identity-r002-forbidden-actions-audit.json",
    "phase-pms-qubes-identity-r002-next-phase-recommendation.json",
    "phase-pms-qubes-identity-r002-build-test-validator-evidence.json"
)

foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $name))) {
        Fail "Missing required artifact $name"
    }
}

$r001 = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r002-r001-reference.json")
if ($r001.r001Decision -ne "PmsQubesIdentityStillBlockedMissingCoreFields") { Fail "R001 decision mismatch" }
Assert-False $r001.r001SandboxAccountProfilePromotedToAccountId "R001 sandbox account profile promoted"
Assert-False $r001.r001AccountingPnlReady "R001 accounting PnL"
Assert-False $r001.r001ProductionPnlReady "R001 production PnL"
Assert-False $r001.r001LedgerCommitReady "R001 ledger commit"
Assert-False $r001.r001ProductionLiveReady "R001 production live"

$operator = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r002-operator-identity-decisions.json")
Assert-True $operator.decisionsRecordedOnly "decisions recorded only"
Assert-False $operator.identityFieldsInventedBeyondExplicitDecisions "identity fields invented"
foreach ($field in @("AccountId", "PortfolioId", "StrategyId", "SourceExecutionIntentId", "AccountCurrency", "PMSApprovedQubesRunId")) {
    $row = @($operator.decisions | Where-Object { $_.fieldName -eq $field })[0]
    if ($null -eq $row) { Fail "Missing operator decision $field" }
    if ($null -ne $row.value) { Fail "$field should remain null" }
    if ($row.evidenceStatus -ne "ExplicitlyKeptMissing") { Fail "$field must be explicitly kept missing" }
}
$sandboxProfile = @($operator.decisions | Where-Object { $_.fieldName -eq "SandboxAccountProfile" })[0]
if ($null -eq $sandboxProfile -or $sandboxProfile.value -ne "ExistingLmaxDemoProfile" -or $sandboxProfile.decision -ne "PreserveSandboxOnly_NotAccountId") {
    Fail "SandboxAccountProfile decision mismatch"
}
$attributionDecision = @($operator.decisions | Where-Object { $_.fieldName -eq "AttributionPolicy" })[0]
if ($null -eq $attributionDecision -or $attributionDecision.value -ne "SandboxPreviewAttributionByPmsCycleAndRebalanceIntent") {
    Fail "AttributionPolicy sandbox decision missing"
}
if ($attributionDecision.evidenceStatus -ne "AdoptedForSandboxPreviewOnly") { Fail "AttributionPolicy status mismatch" }

$matrix = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r002-field-binding-matrix-updated.json")
Assert-False $matrix.fieldsInventedBeyondExplicitDecisions "matrix fields invented"
Assert-False $matrix.sandboxAccountProfilePromotedToAccountId "matrix sandbox profile promoted"
foreach ($field in @("AccountId", "PortfolioId", "StrategyId", "SourceExecutionIntentId", "AccountCurrency", "PMSApprovedQubesRunId")) {
    $row = @($matrix.fields | Where-Object { $_.fieldName -eq $field })[0]
    if ($null -eq $row) { Fail "Missing matrix field $field" }
    if ($null -ne $row.value) { Fail "$field matrix value should be null" }
    if ($row.evidenceStatus -ne "ExplicitlyKeptMissing") { Fail "$field matrix status mismatch" }
}
$matrixAttribution = @($matrix.fields | Where-Object { $_.fieldName -eq "AttributionPolicy" })[0]
if ($matrixAttribution.value -ne "SandboxPreviewAttributionByPmsCycleAndRebalanceIntent") { Fail "matrix attribution mismatch" }
if ($matrixAttribution.evidenceStatus -ne "AdoptedForSandboxPreviewOnly") { Fail "matrix attribution status mismatch" }

$policy = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r002-sandbox-preview-attribution-policy.json")
if ($policy.attributionPolicy -ne "SandboxPreviewAttributionByPmsCycleAndRebalanceIntent") { Fail "attribution policy name mismatch" }
if ($policy.status -ne "AdoptedForSandboxPreviewOnly") { Fail "attribution policy status mismatch" }
foreach ($dimension in @("PmsCycleId", "SourceRebalanceIntentId", "RiskReviewId", "OperatorApprovalId", "SandboxAccountProfile", "CanonicalTargetCloseUtc")) {
    if ($policy.dimensions -notcontains $dimension) { Fail "missing attribution dimension $dimension" }
}
Assert-False $policy.accountingAttributionReady "accounting attribution ready"
Assert-False $policy.productionAttributionReady "production attribution ready"
Assert-False $policy.ledgerCommitAttributionReady "ledger attribution ready"
Assert-True $policy.notAccountingAttribution "not accounting attribution"
Assert-True $policy.notProductionAttribution "not production attribution"

$qubes = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r002-qubes-runid-policy.json")
if ($qubes.currentQubesRunIdStatus -ne "PresentWithWarnings_NotPmsApprovedEconomicOutput") { Fail "QubesRunId warning hidden" }
if ($null -ne $qubes.pmsApprovedQubesRunId) { Fail "PMS-approved QubesRunId should remain null" }
Assert-False $qubes.pmsApprovedQubesEconomicOutputReady "PMS-approved Qubes output"
Assert-False $qubes.zeroOnlyPromotedToPmsApprovedEconomicOutput "ZeroOnly promoted"
Assert-False $qubes.draftOrHistoricalQubesOutputPromoted "draft/historical Qubes promoted"
Assert-True $qubes.futureExplicitQubesEconomicHandoffGateRequired "future Qubes gate"

$impact = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r002-ledger-pnl-impact.json")
if ($impact.sandboxGrossPnlPreviewV0 -ne "Ready") { Fail "sandbox gross PnL V0 not ready" }
if ($impact.ledgerPreview -ne "ReadyWithWarnings") { Fail "ledger preview status mismatch" }
Assert-True $impact.sandboxPreviewAttributionReady "sandbox preview attribution"
Assert-False $impact.accountingAttributionReady "accounting attribution"
Assert-False $impact.productionAttributionReady "production attribution"
Assert-False $impact.accountCurrencyAggregationReady "account currency aggregation"
foreach ($blocked in @("fullSandboxTheoreticalPnl", "netPnl", "paperAccountingPnl", "ledgerCommit", "productionPnl", "productionLive")) {
    if ($impact.$blocked -ne "Blocked") { Fail "$blocked must be Blocked" }
}

$contracts = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r002-contract-adoption-impact.json")
$expected = @{
    "pms-handoff.v1" = "AdoptedWithWarnings"
    "qubes-output.v1" = "AdoptedWithWarnings"
    "execution-intent.v1" = "Partial"
    "paper-ledger-separation.v1" = "Adopted"
    "risk-control.v1" = "Adopted"
}
foreach ($contractId in $expected.Keys) {
    $row = @($contracts.contracts | Where-Object { $_.contractId -eq $contractId })[0]
    if ($null -eq $row) { Fail "Missing contract $contractId" }
    if ($row.status -ne $expected[$contractId]) { Fail "$contractId expected $($expected[$contractId]) but found $($row.status)" }
}

$blockers = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r002-preserved-blockers.json")
foreach ($blocker in @("MissingAccountId", "MissingPortfolioId", "MissingStrategyId", "MissingSourceExecutionIntentId", "MissingAccountCurrency", "AccountCurrencyAggregationBlocked", "AccountingAttributionBlocked", "ProductionAttributionBlocked", "QubesRunIdNotPmsApprovedEconomicOutput", "LedgerCommitBlocked", "ProductionLiveBlocked")) {
    if ($blockers.blockersPreserved -notcontains $blocker) { Fail "Missing preserved blocker $blocker" }
}
if ($blockers.accountingProductionBlockersRemoved.Count -ne 0) { Fail "Accounting/production blockers removed" }

$decision = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r002-decision.json")
foreach ($d in @("PmsQubesIdentityPolicyRecordedWithWarnings", "SandboxPreviewAttributionReady", "AccountingAttributionBlocked", "ProductionLiveStillBlocked")) {
    if ($decision.decision -notcontains $d) { Fail "Missing decision $d" }
}
foreach ($classification in @("PMS_QUBES_IDENTITY_R002_PASS_OPERATOR_IDENTITY_DECISIONS_RECORDED_NO_EXECUTION", "PMS_QUBES_IDENTITY_R002_PASS_SANDBOX_PREVIEW_ATTRIBUTION_READY_NO_MUTATION", "PMS_QUBES_IDENTITY_R002_PASS_ACCOUNTING_PRODUCTION_ATTRIBUTION_BLOCKERS_PRESERVED", "PMS_QUBES_IDENTITY_R002_PASS_PRODUCTION_BLOCKERS_PRESERVED")) {
    if ($decision.classifications -notcontains $classification) { Fail "Missing classification $classification" }
}
Assert-True $decision.sandboxPreviewAttributionReady "decision sandbox attribution"
Assert-False $decision.accountingAttributionReady "decision accounting attribution"
Assert-False $decision.productionAttributionReady "decision production attribution"
Assert-False $decision.identityFieldsInventedBeyondExplicitDecisions "decision invented fields"
Assert-False $decision.sandboxAccountProfilePromotedToAccountId "decision sandbox profile promoted"
Assert-False $decision.qubesRunIdWarningHidden "decision Qubes warning hidden"
Assert-False $decision.accountingPnlReady "decision accounting pnl"
Assert-False $decision.productionPnlReady "decision production pnl"
Assert-False $decision.ledgerCommitReady "decision ledger commit"
Assert-False $decision.productionLiveReady "decision production live"

foreach ($auditFile in @(
    "phase-pms-qubes-identity-r002-no-execution-audit.json",
    "phase-pms-qubes-identity-r002-no-db-mutation-audit.json",
    "phase-pms-qubes-identity-r002-no-ledger-state-mutation-audit.json"
)) {
    $audit = Read-Json (Join-Path $artifactDir $auditFile)
    foreach ($prop in $audit.PSObject.Properties.Name) {
        if ($prop -ne "phase" -and $prop -ne "auditResult" -and $audit.$prop -eq $true) {
            Fail "$auditFile has true forbidden property $prop"
        }
    }
}

$noOrder = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r002-no-order-fill-route-audit.json")
if ($noOrder.ordersCreated -ne 0 -or $noOrder.routesCreated -ne 0 -or $noOrder.submissionsCreated -ne 0 -or $noOrder.fillsCreated -ne 0 -or $noOrder.executionReportsCreated -ne 0 -or $noOrder.executableSchedulesCreated -ne 0) {
    Fail "order/fill/route/report artifacts created"
}

$timing = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r002-canonical-timing-preservation.json")
Assert-True $timing.futureTimestampsUseCanonicalQuarterHour "canonical quarter-hour"
Assert-False $timing.legacy06UsedAsFutureCanonical "legacy :06 future canonical"

$direct = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r002-direct-cross-exclusion-preservation.json")
Assert-False $direct.directCrossExecutionAllowed "direct-cross execution"
Assert-True $direct.directCrossesSignalOnly "direct crosses signal only"
Assert-True $direct.nettingFirstRequired "netting first"

$usdjpy = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r002-usdjpy-caveat-preservation.json")
Assert-True $usdjpy.usdjpyCaveatPreserved "USDJPY caveat"
Assert-False $usdjpy.usdJpyCaveatWeakened "USDJPY caveat weakened"
if ($usdjpy.normalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.executionTradableSymbol -ne "USDJPY" -or $usdjpy.securityID -ne "4004" -or $usdjpy.securityIDSource -ne "8") {
    Fail "USDJPY caveat values changed"
}

$forbidden = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r002-forbidden-actions-audit.json")
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
    "missingIdentityFieldsInventedBeyondExplicitDecisions",
    "sandboxAccountProfilePromotedToAccountId",
    "qubesRunIdWarningHidden",
    "attributionPolicyMisclassifiedAsAccountingProductionAttribution",
    "directCrossExecutionAllowed",
    "legacy06UsedAsFutureCanonical",
    "usdJpyCaveatWeakened",
    "accountingProductionPnlReadinessClaimed",
    "productionLivePromoted"
)) {
    Assert-False $forbidden.$name $name
}

$next = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r002-next-phase-recommendation.json")
foreach ($blocked in @("AccountingPnl", "ProductionPnl", "LedgerCommit", "ProductionLive")) {
    if ($next.doNotProceedTo -notcontains $blocked) { Fail "Missing do-not-proceed $blocked" }
}

$evidence = Read-Json (Join-Path $artifactDir "phase-pms-qubes-identity-r002-build-test-validator-evidence.json")
if ($evidence.build.result -ne "Passed") { Fail "build evidence missing/pending" }
if ($evidence.focusedStaticChecks.result -ne "Passed") { Fail "static check evidence missing/pending" }
if ($evidence.validator.result -ne "Passed") { Fail "validator evidence missing/pending" }
if ($evidence.focusedTests.result -ne "NotApplicable") { Fail "focused tests should be NotApplicable for static R002" }

Write-Output "PMS_QUBES_IDENTITY_R002_GATE_PASS_OPERATOR_DECISIONS_SANDBOX_ATTRIBUTION_NO_MUTATION"

