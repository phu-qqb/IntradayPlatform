param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "PRODUCT_READINESS_R001_GATE_FAIL: $Message"
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

$artifactDir = Join-Path $Root "artifacts/readiness/product-readiness"
$required = @(
    "phase-product-readiness-r001-summary.md",
    "phase-product-readiness-r001-evidence-chain-reference.json",
    "phase-product-readiness-r001-readiness-by-layer.json",
    "phase-product-readiness-r001-blockers-by-category.json",
    "phase-product-readiness-r001-sandbox-strategy-readiness-decision.json",
    "phase-product-readiness-r001-economic-warnings.json",
    "phase-product-readiness-r001-production-live-blockers.json",
    "phase-product-readiness-r001-next-large-packages.json",
    "phase-product-readiness-r001-roadmap.md",
    "phase-product-readiness-r001-roadmap.json",
    "phase-product-readiness-r001-no-external-audit.json",
    "phase-product-readiness-r001-no-execution-audit.json",
    "phase-product-readiness-r001-no-db-mutation-audit.json",
    "phase-product-readiness-r001-no-order-fill-route-audit.json",
    "phase-product-readiness-r001-no-ledger-state-mutation-audit.json",
    "phase-product-readiness-r001-canonical-timing-preservation.json",
    "phase-product-readiness-r001-direct-cross-exclusion-preservation.json",
    "phase-product-readiness-r001-usdjpy-caveat-preservation.json",
    "phase-product-readiness-r001-forbidden-actions-audit.json",
    "phase-product-readiness-r001-next-phase-recommendation.json",
    "phase-product-readiness-r001-build-test-validator-evidence.json"
)

foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $name))) {
        Fail "Missing required artifact $name"
    }
}

$evidence = Read-Json (Join-Path $artifactDir "phase-product-readiness-r001-evidence-chain-reference.json")
Assert-True $evidence.noLiveExecutionEvidenceCreatedByR001 "no live execution evidence by R001"
foreach ($domain in @("Exec/R009/Sandbox", "Cross-rail PMS/R009/Sandbox", "PMS/Qubes lineage", "MarketData", "Ledger", "PnL")) {
    if (-not (@($evidence.evidenceChain | Where-Object { $_.domain -eq $domain })[0])) {
        Fail "Missing evidence chain domain $domain"
    }
}

$layers = Read-Json (Join-Path $artifactDir "phase-product-readiness-r001-readiness-by-layer.json")
$expectedStatuses = @{
    "Sandbox order lifecycle" = "Ready"
    "Cross-rail PMS->R009 sandbox" = "Ready"
    "Sandbox reconciliation / flatten" = "Ready"
    "Ledger preview" = "ReadyWithWarnings"
    "Sandbox gross PnL preview" = "Partial"
    "Full sandbox theoretical PnL" = "Blocked"
    "Paper accounting PnL" = "Blocked"
    "Paper ledger commit" = "Blocked"
    "Production/live" = "Blocked"
}
foreach ($key in $expectedStatuses.Keys) {
    $layer = @($layers.readinessByLayer | Where-Object { $_.layer -eq $key })[0]
    if ($null -eq $layer) { Fail "Missing readiness layer $key" }
    if ($layer.status -ne $expectedStatuses[$key]) { Fail "$key status expected $($expectedStatuses[$key]) got $($layer.status)" }
}
Assert-True $layers.acceptedCurrentReadiness.StrategySandboxLifecycleReady "sandbox lifecycle ready"
Assert-True $layers.acceptedCurrentReadiness.CrossRailSandboxReady "cross-rail ready"
Assert-True $layers.acceptedCurrentReadiness.SandboxPriceDeltaPreviewReady "price delta ready"
Assert-True $layers.acceptedCurrentReadiness.SandboxGrossPnlPreviewPartialReady "partial gross pnl ready"
Assert-False $layers.acceptedCurrentReadiness.ProductionLiveReady "production live ready"
Assert-False $layers.acceptedCurrentReadiness.LedgerCommitReady "ledger commit ready"
Assert-False $layers.acceptedCurrentReadiness.AccountingPnlReady "accounting pnl ready"

$blockers = Read-Json (Join-Path $artifactDir "phase-product-readiness-r001-blockers-by-category.json")
foreach ($category in @("Execution blockers", "PMS/Qubes lineage blockers", "MarketData blockers", "PnL/economic blockers", "Ledger/state blockers", "Production/live blockers", "Operator policy blockers")) {
    $value = $blockers.blockersByCategory.$category
    if ($null -eq $value -or @($value).Count -eq 0) { Fail "Missing blocker category $category" }
}
if ($blockers.blockersByCategory."MarketData blockers" -notcontains "MarketData adoption WARN, not PASS.") { Fail "MarketData WARN blocker missing" }
if ($blockers.blockersByCategory."PnL/economic blockers" -notcontains "AUDUSDUnitScaleMissing") { Fail "AUDUSD unit-scale blocker missing" }
if ($blockers.blockersByCategory."PnL/economic blockers" -notcontains "GBPUSDUnitScaleMissing") { Fail "GBPUSD unit-scale blocker missing" }
if ($blockers.blockersByCategory."PMS/Qubes lineage blockers" -notcontains "OldQubes4EStratTakenHistoricalOnly") { Fail "old Qubes historical blocker missing" }

$decision = Read-Json (Join-Path $artifactDir "phase-product-readiness-r001-sandbox-strategy-readiness-decision.json")
if ($decision.decision -ne "SandboxStrategyReadinessAcceptedWithEconomicWarnings") { Fail "decision mismatch" }
Assert-True $decision.StrategySandboxLifecycleReady "decision sandbox lifecycle"
Assert-True $decision.CrossRailSandboxReady "decision cross-rail"
Assert-True $decision.SandboxPriceDeltaPreviewReady "decision price delta"
Assert-True $decision.SandboxGrossPnlPreviewPartialReady "decision partial pnl"
Assert-False $decision.ProductionLiveReady "decision production live"
Assert-False $decision.LedgerCommitReady "decision ledger commit"
Assert-False $decision.AccountingPnlReady "decision accounting pnl"
Assert-False $decision.fullThreeSymbolGrossPnlReady "full three-symbol pnl"
Assert-False $decision.marketDataWarnMisclassifiedAsPass "MarketData WARN as PASS"
Assert-False $decision.oldQubes4EActive "old Qubes active"

$warnings = Read-Json (Join-Path $artifactDir "phase-product-readiness-r001-economic-warnings.json")
foreach ($id in @("WARN-PNL-UNIT-SCALE-PARTIAL", "WARN-PNL-GROSS-ONLY", "WARN-MD-ADOPTION", "WARN-QUBES-RUNID", "WARN-SANDBOX-APPROVALS")) {
    if (-not (@($warnings.warnings | Where-Object { $_.id -eq $id })[0])) { Fail "Missing economic warning $id" }
}

$production = Read-Json (Join-Path $artifactDir "phase-product-readiness-r001-production-live-blockers.json")
Assert-False $production.ProductionLiveReady "production live ready"
Assert-False $production.productionLiveDiscussionAllowed "production live discussion"
foreach ($blocker in @("Production route remains forbidden.", "Broker activation remains forbidden.", "Production credentials forbidden.", "Accounting/production PnL blocked.")) {
    if ($production.blockers -notcontains $blocker) { Fail "Missing production blocker $blocker" }
}
Assert-False $production.preservedRules.legacy06FutureCanonical "legacy :06 future canonical"
Assert-False $production.preservedRules.directCrossExecutionAllowed "direct cross execution"
Assert-True $production.preservedRules.usdJpyCaveatPreserved "USDJPY caveat"
Assert-False $production.preservedRules.fiveUsdPerMillionUniversalized "5 USD/million universalized"

$packages = Read-Json (Join-Path $artifactDir "phase-product-readiness-r001-next-large-packages.json")
Assert-False $packages.microStepRoadmapProduced "micro-step roadmap"
foreach ($package in @("PMS/Qubes economic lineage completion", "MarketData pricing/readiness completion", "Risk/Cost policy completion", "Ledger accounting policy completion", "Production readiness discussion")) {
    if (-not (@($packages.nextLargePackages | Where-Object { $_.package -eq $package })[0])) { Fail "Missing large package $package" }
}

$roadmap = Read-Json (Join-Path $artifactDir "phase-product-readiness-r001-roadmap.json")
if ($roadmap.roadmapType -ne "LargePackagesOnly") { Fail "roadmap type must be LargePackagesOnly" }
Assert-False $roadmap.microStepRoadmapProduced "roadmap micro steps"
foreach ($blocked in @("PaperAccountingPnl", "PaperLedgerCommit", "ProductionLive")) {
    if ($roadmap.doNotProceedTo -notcontains $blocked) { Fail "Missing roadmap do-not-proceed $blocked" }
}

foreach ($auditFile in @(
    "phase-product-readiness-r001-no-external-audit.json",
    "phase-product-readiness-r001-no-execution-audit.json",
    "phase-product-readiness-r001-no-db-mutation-audit.json",
    "phase-product-readiness-r001-no-ledger-state-mutation-audit.json"
)) {
    $audit = Read-Json (Join-Path $artifactDir $auditFile)
    foreach ($prop in $audit.PSObject.Properties.Name) {
        if ($prop -ne "phase" -and $prop -ne "auditResult" -and $prop -notmatch "Preserved$" -and $audit.$prop -eq $true) {
            Fail "$auditFile has true forbidden property $prop"
        }
    }
}

$noOrder = Read-Json (Join-Path $artifactDir "phase-product-readiness-r001-no-order-fill-route-audit.json")
if ($noOrder.ordersCreated -ne 0 -or $noOrder.routesCreated -ne 0 -or $noOrder.submissionsCreated -ne 0 -or $noOrder.newFillsCreated -ne 0 -or $noOrder.newExecutionReportsCreated -ne 0 -or $noOrder.executableSchedulesCreated -ne 0) {
    Fail "order/fill/route artifacts created"
}
Assert-True $noOrder.sourceEvidenceReusedOnly "source evidence reused only"

$timing = Read-Json (Join-Path $artifactDir "phase-product-readiness-r001-canonical-timing-preservation.json")
Assert-True $timing.futureTimestampsUseCanonicalQuarterHour "canonical quarter-hour"
Assert-False $timing.legacy06UsedAsFutureCanonical "legacy :06 future canonical"

$direct = Read-Json (Join-Path $artifactDir "phase-product-readiness-r001-direct-cross-exclusion-preservation.json")
Assert-False $direct.directCrossExecutionAllowed "direct-cross execution"
Assert-True $direct.directCrossesSignalOnly "direct-cross signal only"
Assert-True $direct.nettingFirstRequired "netting first"
Assert-True $direct.executionDisabledForDirectCrosses "direct-cross execution disabled"

$usdjpy = Read-Json (Join-Path $artifactDir "phase-product-readiness-r001-usdjpy-caveat-preservation.json")
Assert-True $usdjpy.usdjpyCaveatPreserved "USDJPY caveat"
Assert-False $usdjpy.usdJpyCaveatWeakened "USDJPY caveat weakened"
if ($usdjpy.normalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.executionTradableSymbol -ne "USDJPY" -or $usdjpy.securityID -ne "4004" -or $usdjpy.securityIDSource -ne "8") {
    Fail "USDJPY caveat values changed"
}

$forbidden = Read-Json (Join-Path $artifactDir "phase-product-readiness-r001-forbidden-actions-audit.json")
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
    "accountingPnlReadinessClaimed",
    "ledgerCommitReadinessClaimed",
    "fullThreeSymbolGrossPnlClaimedDespiteMissingUnitScale",
    "marketDataWarnMisclassifiedAsPass",
    "oldQubes4ETreatedAsActive",
    "directCrossExecutionAllowed",
    "legacy06UsedAsFutureCanonical",
    "usdJpyCaveatWeakened",
    "microStepRoadmapProduced"
)) {
    Assert-False $forbidden.$name $name
}

$next = Read-Json (Join-Path $artifactDir "phase-product-readiness-r001-next-phase-recommendation.json")
if ($next.decision -ne "SandboxStrategyReadinessAcceptedWithEconomicWarnings") { Fail "next phase decision mismatch" }
foreach ($blocked in @("PaperAccountingPnl", "PaperLedgerCommit", "ProductionLive")) {
    if ($next.doNotProceedTo -notcontains $blocked) { Fail "Missing next-phase do-not-proceed target $blocked" }
}

$buildEvidence = Read-Json (Join-Path $artifactDir "phase-product-readiness-r001-build-test-validator-evidence.json")
if ($buildEvidence.build.result -ne "Passed") { Fail "build evidence missing/pending" }
if ($buildEvidence.focusedStaticChecks.result -ne "Passed") { Fail "static check evidence missing/pending" }
if ($buildEvidence.validator.result -ne "Passed") { Fail "validator evidence missing/pending" }
if ($buildEvidence.focusedTests.result -ne "NotApplicable") { Fail "focused tests should be NotApplicable for static R001" }

Write-Output "PRODUCT_READINESS_R001_GATE_PASS_SANDBOX_STRATEGY_READINESS_WITH_ECONOMIC_WARNINGS_NO_MUTATION"
