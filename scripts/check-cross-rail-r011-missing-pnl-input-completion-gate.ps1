param(
    [string]$ArtifactRoot = "artifacts/readiness/cross-rail-sandbox-handoff"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "CROSS-RAIL-R011 validation failed: $Message"
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "missing required file: $Path"
    }
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
            if ($content -match $pattern) {
                Fail "credential value-like content persisted in $path"
            }
        }
    }
}

function Assert-SandboxPnlFlags($Object, [string]$Name) {
    Assert-True $Object.SandboxOnly "$Name SandboxOnly"
    Assert-True $Object.NotProductionPnl "$Name NotProductionPnl"
    Assert-True $Object.NotAccountingPnl "$Name NotAccountingPnl"
    Assert-True $Object.NotRiskPnl "$Name NotRiskPnl"
}

$requiredFiles = @(
    "cross-rail-r011-summary.md",
    "cross-rail-r011-r010-reference.json",
    "cross-rail-r011-quantity-unit-semantics.json",
    "cross-rail-r011-account-currency-readiness.json",
    "cross-rail-r011-cost-model-readiness.json",
    "cross-rail-r011-fx-conversion-readiness.json",
    "cross-rail-r011-position-cost-basis-readiness.json",
    "cross-rail-r011-pnl-attribution-policy.json",
    "cross-rail-r011-sandbox-pnl-input-completion-assessment.json",
    "cross-rail-r011-future-r012-pnl-compute-plan.json",
    "cross-rail-r011-production-pnl-boundary.json",
    "cross-rail-r011-no-new-execution-safety-audit.json",
    "cross-rail-r011-build-test-validator-evidence.json",
    "cross-rail-r011-next-gate-plan.json"
)

$paths = @()
foreach ($file in $requiredFiles) {
    $path = Join-Path $ArtifactRoot $file
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "missing required R011 artifact: $file"
    }
    $paths += $path
}
Assert-NoCredentialValues $paths

$ref = Read-Json (Join-Path $ArtifactRoot "cross-rail-r011-r010-reference.json")
$qty = Read-Json (Join-Path $ArtifactRoot "cross-rail-r011-quantity-unit-semantics.json")
$account = Read-Json (Join-Path $ArtifactRoot "cross-rail-r011-account-currency-readiness.json")
$cost = Read-Json (Join-Path $ArtifactRoot "cross-rail-r011-cost-model-readiness.json")
$fx = Read-Json (Join-Path $ArtifactRoot "cross-rail-r011-fx-conversion-readiness.json")
$basis = Read-Json (Join-Path $ArtifactRoot "cross-rail-r011-position-cost-basis-readiness.json")
$attrib = Read-Json (Join-Path $ArtifactRoot "cross-rail-r011-pnl-attribution-policy.json")
$assessment = Read-Json (Join-Path $ArtifactRoot "cross-rail-r011-sandbox-pnl-input-completion-assessment.json")
$plan = Read-Json (Join-Path $ArtifactRoot "cross-rail-r011-future-r012-pnl-compute-plan.json")
$boundary = Read-Json (Join-Path $ArtifactRoot "cross-rail-r011-production-pnl-boundary.json")
$audit = Read-Json (Join-Path $ArtifactRoot "cross-rail-r011-no-new-execution-safety-audit.json")
$evidence = Read-Json (Join-Path $ArtifactRoot "cross-rail-r011-build-test-validator-evidence.json")

Assert-True $ref.ReadOnlyReview "R010 ReadOnlyReview"
Assert-True $ref.R010ValidatorPassed "R010 validator reference"
Assert-True $ref.R010SandboxFillInputsReviewed "R010 sandbox fill input review"
if ([string]$ref.R010ProductionPnlBoundaryStatus -ne "ProductionPnlBlocked") { Fail "R010 production boundary not blocked" }
if ([string]$ref.R010RoundTripPreviewStatus -ne "PartialSandboxPnlPreview") { Fail "R010 roundtrip preview status not partial" }
foreach ($missing in @("quantity unit semantics", "account currency", "cost/spread/commission model", "FX conversion", "position/cost basis model", "PnL attribution policy")) {
    if ($missing -notin @($ref.R010MissingInputs)) { Fail "R010 missing input not carried forward: $missing" }
}

if ([string]$qty.Status -ne "MissingQuantityUnitSemantics") { Fail "quantity unit semantics must remain missing without evidence" }
if ([decimal]$qty.Quantity -ne [decimal]"0.1") { Fail "quantity must remain 0.1" }
if ($null -ne $qty.UnitSemantics) { Fail "unit semantics must not be invented" }
Assert-SandboxPnlFlags $qty "quantity"
Assert-True $qty.NotProductionSizing "quantity NotProductionSizing"
Assert-True $qty.DoNotInvent "quantity DoNotInvent"

if ([string]$account.Status -ne "MissingAccountCurrency") { Fail "account currency must remain missing" }
if ($null -ne $account.AccountCurrency) { Fail "account currency must not be invented" }
Assert-SandboxPnlFlags $account "account"
Assert-True $account.DoNotInventAccountCurrency "DoNotInventAccountCurrency"

if ([string]$cost.Status -ne "MissingCostModel") { Fail "cost model must remain missing" }
Assert-False $cost.CommissionModelAvailable "CommissionModelAvailable"
Assert-False $cost.SpreadModelAvailable "SpreadModelAvailable"
Assert-False $cost.SlippageModelAvailable "SlippageModelAvailable"
Assert-False $cost.SandboxZeroCostAssumption "SandboxZeroCostAssumption"
Assert-True $cost.NotProductionCostModel "NotProductionCostModel"
Assert-True $cost.NeedsFutureCostModel "NeedsFutureCostModel"
Assert-SandboxPnlFlags $cost "cost"
Assert-True $cost.DoNotInventCosts "DoNotInventCosts"

if ([string]$fx.Status -ne "MissingFxConversion") { Fail "FX conversion must remain missing for account-currency PnL" }
Assert-True $fx.QuoteCurrencyPreviewPossible "QuoteCurrencyPreviewPossible"
if (@($fx.AvailableConversions).Count -ne 0) { Fail "FX conversions must not be invented" }
Assert-SandboxPnlFlags $fx "fx"
Assert-True $fx.DoNotInventFxRates "DoNotInventFxRates"

if ([string]$basis.Status -ne "SandboxRoundTripCostBasisAvailable") { Fail "sandbox round-trip basis should be available" }
Assert-True $basis.OpenFillAndFlattenFillLinked "OpenFillAndFlattenFillLinked"
Assert-True $basis.CostBasisAvailableForSandboxRoundTrip "CostBasisAvailableForSandboxRoundTrip"
Assert-False $basis.ProductionCostBasisAvailable "ProductionCostBasisAvailable"
Assert-SandboxPnlFlags $basis "basis"

if ([string]$attrib.Status -ne "AttributionBlockedMissingInputs") { Fail "attribution must remain blocked" }
Assert-False $attrib.ProductionAttributionAllowed "ProductionAttributionAllowed"
Assert-SandboxPnlFlags $attrib "attribution"

if ([string]$assessment.Status -ne "PartialSandboxPnlInputsOnly") { Fail "assessment must be partial" }
if ([string]$assessment.FutureR012Readiness -ne "ReadyForPartialPriceDeltaPreviewOnly") { Fail "R012 readiness must be partial price-delta only" }
Assert-True $assessment.SandboxOnly "assessment SandboxOnly"
Assert-True $assessment.NotProductionPnl "assessment NotProductionPnl"
Assert-True $assessment.NotAccountingPnl "assessment NotAccountingPnl"
Assert-True $assessment.NotRiskPnl "assessment NotRiskPnl"
Assert-False $assessment.FullSandboxTheoreticalPnlReady "FullSandboxTheoreticalPnlReady"
Assert-True $assessment.PartialPriceDeltaPreviewReady "PartialPriceDeltaPreviewReady"
Assert-False $assessment.ProductionPnlReady "ProductionPnlReady"

if ([string]$plan.Status -ne "DraftInactiveReadyForR012") { Fail "future R012 plan must be draft inactive" }
if ("PriceDeltaPreviewOnly" -notin @($plan.AllowedComputationScope)) { Fail "future R012 plan must be price-delta only" }
Assert-False $plan.ExecutionAllowedNow "plan ExecutionAllowedNow"
Assert-True $plan.NoNewOrders "plan NoNewOrders"
Assert-True $plan.NoNewFills "plan NoNewFills"
Assert-True $plan.NoLmaxCall "plan NoLmaxCall"
Assert-True $plan.NoFixSession "plan NoFixSession"

if ([string]$boundary.Status -ne "ProductionPnlBlocked") { Fail "production PnL boundary must be blocked" }
Assert-False $boundary.ProductionPnlAllowed "ProductionPnlAllowed"
Assert-False $boundary.FillBasedProductionPnlAvailable "FillBasedProductionPnlAvailable"
Assert-False $boundary.RealizedProductionPnlAvailable "RealizedProductionPnlAvailable"
Assert-False $boundary.AccountingPnlAvailable "AccountingPnlAvailable"
Assert-False $boundary.SandboxTheoreticalPreviewIsProductionPnl "SandboxTheoreticalPreviewIsProductionPnl"
Assert-False $boundary.SandboxAssumptionsAreProductionReady "SandboxAssumptionsAreProductionReady"

if ([string]$audit.Status -ne "Passed") { Fail "no-new-execution audit must pass" }
foreach ($name in @(
    "NoNewLmaxCall",
    "NoNewFixSession",
    "NoNewOrdersSubmitted",
    "NoNewRoutesCreated",
    "NoNewFillsCreated",
    "NoNewSchedulesCreated",
    "NoNewBrokerSubmission",
    "NoProductionLiveTradingStateMutation",
    "NoProductionCredentialUse",
    "CredentialValuesRedacted",
    "NoQubesExecutableRun",
    "NoNettingRun",
    "NoNettedUsdWeightsProduced"
)) {
    Assert-True $audit.$name "no-new-execution audit $name"
}

Assert-True $evidence.ReadOnlyReview "evidence ReadOnlyReview"
Assert-True $evidence.NoNewExecutionOccurred "evidence NoNewExecutionOccurred"
if ($evidence.Validator.Result -ne "Passed") { Fail "validator evidence must be updated to Passed after validation" }

Write-Host "CROSS-RAIL-R011 validator passed."
