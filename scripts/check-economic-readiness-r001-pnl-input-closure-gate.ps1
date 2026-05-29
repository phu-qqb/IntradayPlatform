param(
    [string]$ArtifactRoot = "artifacts/readiness/economic-readiness"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "ECONOMIC-READINESS-R001 validation failed: $Message"
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { Fail "missing required file: $Path" }
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Assert-True($Value, [string]$Name) {
    if ($Value -ne $true) { Fail "$Name must be true" }
}

function Assert-False($Value, [string]$Name) {
    if ($Value -ne $false) { Fail "$Name must be false" }
}

function Assert-NoCredentialValues([string[]]$Paths) {
    $patterns = @(
        '"CredentialValue"\s*:',
        '"CredentialValues"\s*:',
        '"PasswordValue"\s*:',
        '"SecretValue"\s*:',
        '554=[^|\r\n]+',
        'LMAX_DEMO_FIX_PASSWORD"\s*:\s*"[^"]+"'
    )
    foreach ($path in $Paths) {
        $content = Get-Content -LiteralPath $path -Raw
        foreach ($pattern in $patterns) {
            if ($content -match $pattern) { Fail "credential value-like content persisted in $path" }
        }
    }
}

function MatrixItem($Matrix, [string]$Name) {
    $item = @($Matrix.Inputs | Where-Object { $_.input -eq $Name })[0]
    if ($null -eq $item) { Fail "matrix missing input: $Name" }
    return $item
}

$requiredFiles = @(
    "phase-economic-readiness-r001-summary.md",
    "phase-economic-readiness-r001-cross-rail-r014-reference.json",
    "phase-economic-readiness-r001-pms-qubes-lineage-reference.json",
    "phase-economic-readiness-r001-ledger-r004-r005-reference.json",
    "phase-economic-readiness-r001-marketdata-warn-reference.json",
    "phase-economic-readiness-r001-consolidated-pnl-input-matrix.json",
    "phase-economic-readiness-r001-pnl-level-readiness-status.json",
    "phase-economic-readiness-r001-closed-inputs-summary.json",
    "phase-economic-readiness-r001-hard-blockers.json",
    "phase-economic-readiness-r001-pms-qubes-closure-package.json",
    "phase-economic-readiness-r001-marketdata-pricing-closure-package.json",
    "phase-economic-readiness-r001-risk-cost-policy-closure-package.json",
    "phase-economic-readiness-r001-ledger-policy-closure-package.json",
    "phase-economic-readiness-r001-production-live-blockers.json",
    "phase-economic-readiness-r001-decision.json",
    "phase-economic-readiness-r001-no-external-audit.json",
    "phase-economic-readiness-r001-no-execution-audit.json",
    "phase-economic-readiness-r001-no-db-mutation-audit.json",
    "phase-economic-readiness-r001-no-order-fill-route-audit.json",
    "phase-economic-readiness-r001-no-ledger-state-mutation-audit.json",
    "phase-economic-readiness-r001-canonical-timing-preservation.json",
    "phase-economic-readiness-r001-direct-cross-exclusion-preservation.json",
    "phase-economic-readiness-r001-usdjpy-caveat-preservation.json",
    "phase-economic-readiness-r001-forbidden-actions-audit.json",
    "phase-economic-readiness-r001-next-phase-recommendation.json"
)

$paths = @()
foreach ($file in $requiredFiles) {
    $path = Join-Path $ArtifactRoot $file
    if (-not (Test-Path -LiteralPath $path)) { Fail "missing required artifact: $file" }
    $paths += $path
}
Assert-NoCredentialValues $paths

$cross = Read-Json (Join-Path $ArtifactRoot "phase-economic-readiness-r001-cross-rail-r014-reference.json")
$lineage = Read-Json (Join-Path $ArtifactRoot "phase-economic-readiness-r001-pms-qubes-lineage-reference.json")
$ledger = Read-Json (Join-Path $ArtifactRoot "phase-economic-readiness-r001-ledger-r004-r005-reference.json")
$market = Read-Json (Join-Path $ArtifactRoot "phase-economic-readiness-r001-marketdata-warn-reference.json")
$matrix = Read-Json (Join-Path $ArtifactRoot "phase-economic-readiness-r001-consolidated-pnl-input-matrix.json")
$levels = Read-Json (Join-Path $ArtifactRoot "phase-economic-readiness-r001-pnl-level-readiness-status.json")
$closed = Read-Json (Join-Path $ArtifactRoot "phase-economic-readiness-r001-closed-inputs-summary.json")
$hard = Read-Json (Join-Path $ArtifactRoot "phase-economic-readiness-r001-hard-blockers.json")
$pmsPkg = Read-Json (Join-Path $ArtifactRoot "phase-economic-readiness-r001-pms-qubes-closure-package.json")
$mdPkg = Read-Json (Join-Path $ArtifactRoot "phase-economic-readiness-r001-marketdata-pricing-closure-package.json")
$riskPkg = Read-Json (Join-Path $ArtifactRoot "phase-economic-readiness-r001-risk-cost-policy-closure-package.json")
$ledgerPkg = Read-Json (Join-Path $ArtifactRoot "phase-economic-readiness-r001-ledger-policy-closure-package.json")
$prod = Read-Json (Join-Path $ArtifactRoot "phase-economic-readiness-r001-production-live-blockers.json")
$decision = Read-Json (Join-Path $ArtifactRoot "phase-economic-readiness-r001-decision.json")
$noExternal = Read-Json (Join-Path $ArtifactRoot "phase-economic-readiness-r001-no-external-audit.json")
$noExec = Read-Json (Join-Path $ArtifactRoot "phase-economic-readiness-r001-no-execution-audit.json")
$noDb = Read-Json (Join-Path $ArtifactRoot "phase-economic-readiness-r001-no-db-mutation-audit.json")
$noOrder = Read-Json (Join-Path $ArtifactRoot "phase-economic-readiness-r001-no-order-fill-route-audit.json")
$noLedger = Read-Json (Join-Path $ArtifactRoot "phase-economic-readiness-r001-no-ledger-state-mutation-audit.json")
$timing = Read-Json (Join-Path $ArtifactRoot "phase-economic-readiness-r001-canonical-timing-preservation.json")
$direct = Read-Json (Join-Path $ArtifactRoot "phase-economic-readiness-r001-direct-cross-exclusion-preservation.json")
$usdjpy = Read-Json (Join-Path $ArtifactRoot "phase-economic-readiness-r001-usdjpy-caveat-preservation.json")
$forbidden = Read-Json (Join-Path $ArtifactRoot "phase-economic-readiness-r001-forbidden-actions-audit.json")

Assert-True $cross.ValidatorPassed "CROSS-RAIL-R014 validator"
Assert-True $lineage.ValidatorPassed "PMS-QUBES-LINEAGE-R001 validator"
Assert-True $lineage.QubesRunIdNotPmsApprovedEconomicOutput "QubesRunId warning"
if ([string]$ledger.R005CurrentCeiling -ne "SandboxPriceDeltaOnlyReady") { Fail "ledger ceiling must be SandboxPriceDeltaOnlyReady" }
if ([string]$market.MarketDataAdoption -ne "WARN") { Fail "MarketData adoption must remain WARN" }
if ([string]$market.M30Evidence -ne "MISSING_CONFIRMED") { Fail "M30 missing warning must be preserved" }
if ($market.MarketDataDbReadinessComplete -ne $false) { Fail "MarketData DB readiness must not be falsely complete" }

if ([string](MatrixItem $matrix "mark prices").classification -ne "BlockedByMarketData") { Fail "mark prices classification invalid" }
if ([string](MatrixItem $matrix "open fill prices").classification -ne "Present") { Fail "open fill prices must be Present" }
if ([string](MatrixItem $matrix "flatten fill prices").classification -ne "Present") { Fail "flatten fill prices must be Present" }
if ([string](MatrixItem $matrix "price delta").classification -ne "Present") { Fail "price delta must be Present" }
if ([string](MatrixItem $matrix "cost/spread model").classification -ne "RequiresRiskCostPolicy") { Fail "cost/spread model classification invalid" }
if ([string](MatrixItem $matrix "commission model").classification -ne "RequiresRiskCostPolicy") { Fail "commission model classification invalid" }
if ([string](MatrixItem $matrix "FX conversion").classification -ne "Missing") { Fail "FX conversion must be Missing" }
if ([string](MatrixItem $matrix "position/cost basis model").classification -ne "Missing") { Fail "position/cost basis model must be Missing" }
if ([string](MatrixItem $matrix "account currency").classification -ne "Missing") { Fail "account currency must be Missing" }
if ([string](MatrixItem $matrix "attribution policy").classification -ne "RequiresOperatorPolicy") { Fail "attribution policy classification invalid" }
foreach ($name in @("AccountId", "PortfolioId", "StrategyId")) {
    $item = MatrixItem $matrix $name
    if ([string]$item.classification -ne "BlockedByPmsQubes") { Fail "$name must be BlockedByPmsQubes" }
    if ($null -ne $item.currentValue) { Fail "$name must not be invented" }
}
if ($null -ne (MatrixItem $matrix "SourceExecutionIntentId").currentValue) { Fail "SourceExecutionIntentId must not be invented" }
if ([string](MatrixItem $matrix "QubesRunId").classification -ne "PresentWithWarnings") { Fail "QubesRunId warning hidden" }

$sandboxPrice = @($levels.Levels | Where-Object { $_.Level -eq "SandboxPriceDeltaOnly" })[0]
if ([string]$sandboxPrice.ReadinessStatus -ne "Ready") { Fail "SandboxPriceDeltaOnly must be Ready" }
foreach ($levelName in @("SandboxTheoreticalPnl", "PaperAccountingPnl", "ProductionPnl", "PaperLedgerCommit", "ProductionLedgerCommit")) {
    $level = @($levels.Levels | Where-Object { $_.Level -eq $levelName })[0]
    if ($null -eq $level) { Fail "missing level $levelName" }
    if ([string]$level.ReadinessStatus -ne "Blocked") { Fail "$levelName must be Blocked" }
}

foreach ($input in @("open fill prices", "flatten fill prices", "price-delta-only preview", "PmsCycleId", "CanonicalTargetCloseUtc")) {
    if ($input -notin @($closed.ClosedInputs)) { Fail "closed input missing: $input" }
}

foreach ($blocker in @(
    "MissingMarkPrices",
    "MissingCostSpreadCommissionModel",
    "MissingFxConversion",
    "MissingPositionCostBasisModel",
    "MissingAccountCurrency",
    "MissingAttributionPolicy",
    "MissingAccountId",
    "MissingPortfolioId",
    "MissingStrategyId",
    "MissingSourceExecutionIntentId",
    "QubesRunIdNotPmsApprovedEconomicOutput",
    "CommitSafeIdempotencyPolicyIncomplete",
    "MarketDataWarnM30Missing",
    "MarketDataWarnRowCountsMissing",
    "MarketDataWarnTickSchemaPartial"
)) {
    if ($blocker -notin @($hard.HardBlockers)) { Fail "hard blocker missing: $blocker" }
}
Assert-True $hard.DoNotInventMissingInputs "DoNotInventMissingInputs"

if ([string]$pmsPkg.Status -ne "PmsQubesClosurePackageReady") { Fail "PMS/Qubes closure package invalid" }
if ([string]$mdPkg.Status -ne "MarketDataPricingClosurePackageReady") { Fail "MarketData closure package invalid" }
if ([string]$riskPkg.Status -ne "RiskCostPolicyClosurePackageReady") { Fail "Risk/Cost closure package invalid" }
if ([string]$ledgerPkg.Status -ne "LedgerPolicyClosurePackageReady") { Fail "Ledger closure package invalid" }
Assert-False $ledgerPkg.CurrentStatus.LedgerCommitAllowed "LedgerCommitAllowed"
Assert-True $prod.ProductionLiveStillBlocked "ProductionLiveStillBlocked"
Assert-False $prod.R009ProductionLivePromoted "R009ProductionLivePromoted"

foreach ($d in @("EconomicReadinessPriceDeltaOnlyAccepted", "SandboxTheoreticalPnlBlockedByMissingEconomicInputs", "PmsQubesLineagePartiallyReady", "MarketDataPricingEvidenceStillWarn", "ProductionLiveStillBlocked")) {
    if ($d -notin @($decision.Decisions)) { Fail "decision missing: $d" }
}
Assert-False $decision.FullPnlReadinessClaimed "FullPnlReadinessClaimed"
Assert-False $decision.ProductionPnlReadinessClaimed "ProductionPnlReadinessClaimed"
Assert-False $decision.AccountingPnlReadinessClaimed "AccountingPnlReadinessClaimed"

foreach ($name in @("NoExternalApiCall", "NoLmaxCall", "NoPolygonCall", "NoBrokerActivation", "NoLiveMarketDataRequested")) {
    Assert-True $noExternal.$name "no-external $name"
}
foreach ($name in @("NoPmsEmsOmsCycleRun", "NoManualNoExternalRun", "NoQubesRun", "NoPythonCppCudaWorkloadRun", "NoBacktestOrSimulationRun", "NoExecutableSchedulesCreated", "NoProductionLivePromotion")) {
    Assert-True $noExec.$name "no-exec $name"
}
foreach ($name in @("NoDbMutation", "NoDbMigrationCreated", "NoDbMigrationApplied", "NoSqlMutationArtifact", "NoPaperPositionMutation", "NoProductionPositionMutation", "NoCashStateMutation", "NoTradingStateMutation")) {
    Assert-True $noDb.$name "no-db $name"
}
foreach ($name in @("NoOrdersCreated", "NoRoutesCreated", "NoSubmissionsCreated", "NoNewFillsCreated", "NoExecutionReportsCreated", "NoBrokerSubmission")) {
    Assert-True $noOrder.$name "no-order $name"
}
foreach ($name in @("NoPaperLedgerCommit", "NoProductionLedgerCommit", "NoLedgerCommit", "NoPaperLedgerMutation", "NoProductionLedgerMutation", "NoPositionMutation", "NoCashMutation", "NoTradingStateMutation")) {
    Assert-True $noLedger.$name "no-ledger $name"
}

Assert-False $timing.Legacy06UsedAsFutureCanonical "Legacy06UsedAsFutureCanonical"
Assert-False $direct.DirectCrossExecutionAllowed "DirectCrossExecutionAllowed"
Assert-True $direct.ExecutionHandoffUsdPairOnly "ExecutionHandoffUsdPairOnly"
Assert-True $usdjpy.RequiresInversion "USDJPY RequiresInversion"
if ([int]$usdjpy.SecurityID -ne 4004) { Fail "USDJPY SecurityID must be 4004" }
Assert-False $usdjpy.CaveatWeakened "USDJPY CaveatWeakened"

foreach ($prop in $forbidden.PSObject.Properties) {
    if ($prop.Name -eq "Gate" -or $prop.Name -eq "Status") { continue }
    Assert-False $prop.Value "forbidden $($prop.Name)"
}

$evidencePath = Join-Path $ArtifactRoot "phase-economic-readiness-r001-build-test-validator-evidence.json"
if (-not (Test-Path -LiteralPath $evidencePath)) { Fail "build/tests/validator evidence missing" }
$evidence = Read-Json $evidencePath
if ($evidence.Validator.Result -ne "Passed") { Fail "validator evidence must be Passed" }

Write-Host "ECONOMIC-READINESS-R001 validator passed."
