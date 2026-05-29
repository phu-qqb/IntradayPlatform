param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "PMS_QUBES_LINEAGE_R002_GATE_FAIL: $Message"
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

$artifactDir = Join-Path $Root "artifacts/readiness/pms-qubes-lineage"
$required = @(
    "phase-pms-qubes-lineage-r002-summary.md",
    "phase-pms-qubes-lineage-r002-product-readiness-reference.json",
    "phase-pms-qubes-lineage-r002-active-artifact-scope.json",
    "phase-pms-qubes-lineage-r002-q4e-historical-only-confirmation.json",
    "phase-pms-qubes-lineage-r002-lineage-closure-package.json",
    "phase-pms-qubes-lineage-r002-field-binding-matrix.json",
    "phase-pms-qubes-lineage-r002-missing-field-diagnostics.json",
    "phase-pms-qubes-lineage-r002-attribution-status.json",
    "phase-pms-qubes-lineage-r002-pms-approved-qubes-runid-status.json",
    "phase-pms-qubes-lineage-r002-ledger-pnl-readiness-impact.json",
    "phase-pms-qubes-lineage-r002-production-blockers-preserved.json",
    "phase-pms-qubes-lineage-r002-decision.json",
    "phase-pms-qubes-lineage-r002-no-external-audit.json",
    "phase-pms-qubes-lineage-r002-no-execution-audit.json",
    "phase-pms-qubes-lineage-r002-no-db-mutation-audit.json",
    "phase-pms-qubes-lineage-r002-no-order-fill-route-audit.json",
    "phase-pms-qubes-lineage-r002-no-ledger-commit-audit.json",
    "phase-pms-qubes-lineage-r002-canonical-timing-preservation.json",
    "phase-pms-qubes-lineage-r002-direct-cross-exclusion-preservation.json",
    "phase-pms-qubes-lineage-r002-forbidden-actions-audit.json",
    "phase-pms-qubes-lineage-r002-next-phase-recommendation.json",
    "phase-pms-qubes-lineage-r002-build-test-validator-evidence.json"
)

foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $name))) {
        Fail "Missing required artifact $name"
    }
}

$product = Read-Json (Join-Path $artifactDir "phase-pms-qubes-lineage-r002-product-readiness-reference.json")
if ($product.productDecision -ne "SandboxStrategyReadinessAcceptedWithEconomicWarnings") { Fail "product decision mismatch" }
Assert-True $product.StrategySandboxLifecycleReady "strategy sandbox ready"
Assert-True $product.CrossRailSandboxReady "cross rail sandbox ready"
Assert-False $product.ProductionLiveReady "production live ready"
Assert-False $product.LedgerCommitReady "ledger commit ready"
Assert-False $product.AccountingPnlReady "accounting pnl ready"
Assert-False $product.oldQubes4EActive "old Qubes active"

$scope = Read-Json (Join-Path $artifactDir "phase-pms-qubes-lineage-r002-active-artifact-scope.json")
if ($scope.scope -ne "Active PMS-PAPER-R015 and CROSS-RAIL-R014 artifacts only") { Fail "scope mismatch" }
Assert-True $scope.noQubesRun "no Qubes run"
Assert-True $scope.noExecution "no execution"
Assert-True $scope.noDbMutation "no DB mutation"
foreach ($excluded in @("Qubes 4E bootstrap", "StratTaken bootstrap")) {
    if ($scope.excludedHistoricalArtifacts -notcontains $excluded) { Fail "Missing excluded historical artifact $excluded" }
}

$q4e = Read-Json (Join-Path $artifactDir "phase-pms-qubes-lineage-r002-q4e-historical-only-confirmation.json")
Assert-False $q4e.Qubes4EActiveState "Qubes 4E active"
Assert-False $q4e.StratTakenBootstrapActiveState "StratTaken active"
Assert-True $q4e.DoNotUseQubes4EAsActiveEconomicState "do not use Qubes 4E"
Assert-False $q4e.CurrentQubesBranchHandoffEligible "current Qubes handoff eligible"
if ($q4e.ManagerWeightsProfile -ne "ZeroOnly") { Fail "ManagerWeightsProfile must remain ZeroOnly" }
Assert-False $q4e.PmsApprovedQubesEconomicOutputPresent "PMS-approved Qubes output"

$package = Read-Json (Join-Path $artifactDir "phase-pms-qubes-lineage-r002-lineage-closure-package.json")
if ($package.packageStatus -ne "LineageClosurePackageReadyWithRemainingMissingFields") { Fail "package status mismatch" }
Assert-False $package.lineageFieldsInvented "lineage invented"
Assert-True $package.previewUseAllowed "preview allowed"
Assert-False $package.ledgerCommitAllowed "ledger commit"
Assert-False $package.accountingPnlReady "accounting pnl"
Assert-False $package.productionLiveReady "production live"
foreach ($field in @("AccountId", "PortfolioId", "StrategyId", "SourceExecutionIntentId", "AccountCurrency", "AttributionPolicy", "PMS-approved QubesRunId")) {
    if ($package.stillMissing -notcontains $field) { Fail "Missing stillMissing $field" }
}
if ($package.boundFields.PmsCycleId -ne "pms-paper-r010-delta-fields-20260525-001") { Fail "PmsCycleId mismatch" }
if ($package.warningLineage.QubesRunId.status -ne "PresentWithWarnings_NotPmsApprovedEconomicOutput") { Fail "QubesRunId warning status mismatch" }
Assert-False $package.warningLineage.QubesRunId.canDriveLedgerOrPnl "QubesRunId can drive ledger/PnL"

$matrix = Read-Json (Join-Path $artifactDir "phase-pms-qubes-lineage-r002-field-binding-matrix.json")
if (@($matrix.inventedFields).Count -ne 0) { Fail "invented fields present" }
foreach ($field in @("AccountId", "PortfolioId", "StrategyId", "SourceExecutionIntentId", "AccountCurrency", "AttributionPolicy", "PMSApprovedQubesRunId")) {
    $row = @($matrix.fields | Where-Object { $_.fieldName -eq $field })[0]
    if ($null -eq $row) { Fail "Missing matrix row $field" }
    if ($null -ne $row.value) { Fail "$field must remain null" }
    if ($row.evidenceStatus -ne "Missing") { Fail "$field evidence status must be Missing" }
}
foreach ($field in @("PmsCycleId", "RiskReviewId", "OperatorApprovalId", "CanonicalTargetCloseUtc")) {
    $row = @($matrix.fields | Where-Object { $_.fieldName -eq $field })[0]
    if ($null -eq $row -or $null -eq $row.value) { Fail "$field must be bound" }
}
$qubes = @($matrix.fields | Where-Object { $_.fieldName -eq "QubesRunId" })[0]
if ($qubes.evidenceStatus -ne "PresentWithWarnings_NotPmsApprovedEconomicOutput") { Fail "QubesRunId matrix status mismatch" }

$missing = Read-Json (Join-Path $artifactDir "phase-pms-qubes-lineage-r002-missing-field-diagnostics.json")
if ($missing.status -ne "MissingFieldsStillOpen") { Fail "missing diagnostics status mismatch" }
if (@($missing.inventedFields).Count -ne 0) { Fail "invented fields in diagnostics" }
foreach ($field in @("AccountId", "PortfolioId", "StrategyId", "SourceExecutionIntentId", "AccountCurrency", "AttributionPolicy", "PMS-approved QubesRunId")) {
    if (-not (@($missing.missingFields | Where-Object { $_.field -eq $field })[0])) { Fail "Missing diagnostic $field" }
}

$attrib = Read-Json (Join-Path $artifactDir "phase-pms-qubes-lineage-r002-attribution-status.json")
if ($attrib.attributionStatus -ne "PartialSandboxPreviewAttributionOnly") { Fail "attribution status mismatch" }
Assert-False $attrib.accountingAttributionReady "accounting attribution"
Assert-False $attrib.productionAttributionReady "production attribution"
Assert-False $attrib.ledgerCommitAttributionReady "ledger commit attribution"
foreach ($dim in @("AccountId", "PortfolioId", "StrategyId", "SourceExecutionIntentId", "PMS-approved QubesRunId", "AccountCurrency", "FullAttributionPolicy")) {
    if ($attrib.missingAttributionDimensions -notcontains $dim) { Fail "Missing attribution dimension $dim" }
}

$qRun = Read-Json (Join-Path $artifactDir "phase-pms-qubes-lineage-r002-pms-approved-qubes-runid-status.json")
if ($qRun.status -ne "PresentWithWarnings_NotPmsApprovedEconomicOutput") { Fail "QubesRunId status mismatch" }
if ($null -ne $qRun.PmsApprovedQubesRunId) { Fail "PMS approved QubesRunId must be null" }
Assert-False $qRun.PmsApprovedQubesEconomicOutputReady "PMS-approved Qubes output ready"
Assert-True $qRun.Qubes4EHistoricalOnly "Qubes4E historical only"
if ($qRun.ManagerWeightsProfile -ne "ZeroOnly") { Fail "Qubes ManagerWeightsProfile mismatch" }
Assert-False $qRun.canDriveAccountingPnl "Qubes can drive accounting PnL"
Assert-False $qRun.canDriveLedgerCommit "Qubes can drive ledger commit"
Assert-False $qRun.canDriveProduction "Qubes can drive production"

$impact = Read-Json (Join-Path $artifactDir "phase-pms-qubes-lineage-r002-ledger-pnl-readiness-impact.json")
Assert-True $impact.ledgerPreviewReadyWithWarnings "ledger preview with warnings"
Assert-True $impact.sandboxPriceDeltaPreviewReady "price delta ready"
Assert-True $impact.sandboxGrossPnlPreviewPartialReady "partial gross pnl ready"
Assert-False $impact.fullThreeSymbolGrossPnlReady "full three symbol gross pnl"
Assert-False $impact.fullSandboxTheoreticalPnlReady "full theoretical pnl"
Assert-False $impact.paperAccountingPnlReady "paper accounting pnl"
Assert-False $impact.paperLedgerCommitReady "paper ledger commit"
Assert-False $impact.productionLiveReady "production live"
foreach ($blocker in @("AccountId", "PortfolioId", "StrategyId", "SourceExecutionIntentId", "AccountCurrency", "AttributionPolicy", "PMS-approved QubesRunId")) {
    if ($impact.blockingMissingInputs -notcontains $blocker) { Fail "Missing ledger impact blocker $blocker" }
}

$prod = Read-Json (Join-Path $artifactDir "phase-pms-qubes-lineage-r002-production-blockers-preserved.json")
Assert-False $prod.productionLiveReady "production live"
Assert-False $prod.ledgerCommitReady "ledger commit"
Assert-False $prod.accountingPnlReady "accounting pnl"
Assert-False $prod.productionRouteEnabled "production route"
Assert-False $prod.brokerActivated "broker activated"
Assert-False $prod.directCrossExecutionAllowed "direct cross"
Assert-False $prod.legacy06UsedAsFutureCanonical "legacy :06"

$decision = Read-Json (Join-Path $artifactDir "phase-pms-qubes-lineage-r002-decision.json")
if ($decision.decision -ne "PmsQubesLineageClosureReadyWithRemainingMissingFields") { Fail "decision mismatch" }
if (@($decision.newFieldsBound).Count -ne 0) { Fail "new fields should not be bound" }
Assert-False $decision.lineageFieldsInvented "decision invented fields"
Assert-True $decision.previewOnly "decision preview only"
Assert-False $decision.ledgerCommitReady "decision ledger commit"
Assert-False $decision.accountingPnlReady "decision accounting pnl"
Assert-False $decision.productionLiveReady "decision production live"

foreach ($auditFile in @(
    "phase-pms-qubes-lineage-r002-no-external-audit.json",
    "phase-pms-qubes-lineage-r002-no-execution-audit.json",
    "phase-pms-qubes-lineage-r002-no-db-mutation-audit.json",
    "phase-pms-qubes-lineage-r002-no-ledger-commit-audit.json"
)) {
    $audit = Read-Json (Join-Path $artifactDir $auditFile)
    foreach ($prop in $audit.PSObject.Properties.Name) {
        if ($prop -ne "phase" -and $prop -ne "auditResult" -and $prop -notmatch "Preserved$" -and $audit.$prop -eq $true) {
            Fail "$auditFile has true forbidden property $prop"
        }
    }
}

$noOrder = Read-Json (Join-Path $artifactDir "phase-pms-qubes-lineage-r002-no-order-fill-route-audit.json")
if ($noOrder.ordersCreated -ne 0 -or $noOrder.routesCreated -ne 0 -or $noOrder.submissionsCreated -ne 0 -or $noOrder.newFillsCreated -ne 0 -or $noOrder.newExecutionReportsCreated -ne 0 -or $noOrder.executableSchedulesCreated -ne 0) {
    Fail "order/fill/route artifacts created"
}

$timing = Read-Json (Join-Path $artifactDir "phase-pms-qubes-lineage-r002-canonical-timing-preservation.json")
Assert-True $timing.futureTimestampsUseCanonicalQuarterHour "canonical quarter-hour"
Assert-False $timing.legacy06UsedAsFutureCanonical "legacy :06 future canonical"

$direct = Read-Json (Join-Path $artifactDir "phase-pms-qubes-lineage-r002-direct-cross-exclusion-preservation.json")
Assert-False $direct.directCrossExecutionAllowed "direct-cross execution"
Assert-True $direct.directCrossesSignalOnly "direct-cross signal only"
Assert-True $direct.nettingFirstRequired "netting first"
Assert-True $direct.executionDisabledForDirectCrosses "direct-cross execution disabled"

$forbidden = Read-Json (Join-Path $artifactDir "phase-pms-qubes-lineage-r002-forbidden-actions-audit.json")
foreach ($name in @(
    "lmaxCalled",
    "polygonCalled",
    "externalApiCalled",
    "brokerActivated",
    "liveMarketDataRequested",
    "qubesRun",
    "pmsEmsOmsCycleRun",
    "manualNoExternalRun",
    "pythonCppCudaWorkloadRun",
    "backtestOrSimulationRun",
    "dbMutation",
    "migrationCreatedOrApplied",
    "orderRouteSubmissionFillExecutionReportCreated",
    "ledgerCommit",
    "productionLedgerCommit",
    "tradingStateMutation",
    "productionLiveReadinessClaimed",
    "accountingPnlReadinessClaimed",
    "ledgerCommitReadinessClaimed",
    "lineageFieldsInvented",
    "oldQubes4ETreatedAsActive",
    "directCrossExecutionAllowed",
    "legacy06UsedAsFutureCanonical"
)) {
    Assert-False $forbidden.$name $name
}

$next = Read-Json (Join-Path $artifactDir "phase-pms-qubes-lineage-r002-next-phase-recommendation.json")
if ($next.decision -ne "PmsQubesLineageClosureReadyWithRemainingMissingFields") { Fail "next decision mismatch" }
foreach ($blocked in @("PaperAccountingPnl", "PaperLedgerCommit", "ProductionLive")) {
    if ($next.doNotProceedTo -notcontains $blocked) { Fail "Missing do-not-proceed target $blocked" }
}

$buildEvidence = Read-Json (Join-Path $artifactDir "phase-pms-qubes-lineage-r002-build-test-validator-evidence.json")
if ($buildEvidence.build.result -ne "Passed") { Fail "build evidence missing/pending" }
if ($buildEvidence.focusedStaticChecks.result -ne "Passed") { Fail "static check evidence missing/pending" }
if ($buildEvidence.validator.result -ne "Passed") { Fail "validator evidence missing/pending" }
if ($buildEvidence.focusedTests.result -ne "NotApplicable") { Fail "focused tests should be NotApplicable for static R002" }

Write-Output "PMS_QUBES_LINEAGE_R002_GATE_PASS_LINEAGE_CLOSURE_WITH_REMAINING_MISSING_FIELDS_NO_MUTATION"
