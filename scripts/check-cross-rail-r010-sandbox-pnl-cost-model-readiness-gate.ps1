param(
    [string]$ArtifactRoot = "artifacts/readiness/cross-rail-sandbox-handoff"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "CROSS-RAIL-R010 validation failed: $Message"
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

function Assert-FillRows($Rows, [hashtable]$ExpectedSides, [string]$Name) {
    if ($null -eq $Rows -or @($Rows).Count -ne 3) {
        Fail "$Name must contain exactly 3 rows"
    }
    foreach ($row in $Rows) {
        $symbol = [string]$row.Symbol
        if (-not $ExpectedSides.ContainsKey($symbol)) {
            Fail "$Name contains unexpected symbol $symbol"
        }
        if ([string]$row.Side -ne $ExpectedSides[$symbol]) {
            Fail "$Name side mismatch for ${symbol}: $($row.Side)"
        }
        if ([decimal]$row.Quantity -ne [decimal]"0.1") {
            Fail "$Name quantity mismatch for ${symbol}: $($row.Quantity)"
        }
        if ($null -eq $row.FillPrice) { Fail "$Name missing fill price for $symbol" }
        if ([string]::IsNullOrWhiteSpace([string]$row.Timestamp)) { Fail "$Name missing timestamp for $symbol" }
        if ([string]::IsNullOrWhiteSpace([string]$row.SandboxOrderId)) { Fail "$Name missing sandbox order id for $symbol" }
        if ([string]::IsNullOrWhiteSpace([string]$row.ExecutionReportId)) { Fail "$Name missing execution report id for $symbol" }
        if ([string]::IsNullOrWhiteSpace([string]$row.FillId)) { Fail "$Name missing fill id for $symbol" }
        Assert-True $row.SandboxOnly "$Name $symbol SandboxOnly"
        Assert-False $row.ProductionFill "$Name $symbol ProductionFill"
        Assert-True $row.NotProductionPnl "$Name $symbol NotProductionPnl"
    }
}

$requiredFiles = @(
    "cross-rail-r010-summary.md",
    "cross-rail-r010-r009-reference.json",
    "cross-rail-r010-sandbox-fill-input-review.json",
    "cross-rail-r010-sandbox-roundtrip-pnl-preview.json",
    "cross-rail-r010-cost-model-readiness.json",
    "cross-rail-r010-fx-conversion-readiness.json",
    "cross-rail-r010-account-currency-readiness.json",
    "cross-rail-r010-position-cost-basis-readiness.json",
    "cross-rail-r010-pnl-attribution-readiness.json",
    "cross-rail-r010-production-pnl-boundary.json",
    "cross-rail-r010-no-new-execution-safety-audit.json",
    "cross-rail-r010-build-test-validator-evidence.json",
    "cross-rail-r010-next-gate-plan.json"
)

$paths = @()
foreach ($file in $requiredFiles) {
    $path = Join-Path $ArtifactRoot $file
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "missing required R010 artifact: $file"
    }
    $paths += $path
}
Assert-NoCredentialValues $paths

$ref = Read-Json (Join-Path $ArtifactRoot "cross-rail-r010-r009-reference.json")
$fills = Read-Json (Join-Path $ArtifactRoot "cross-rail-r010-sandbox-fill-input-review.json")
$preview = Read-Json (Join-Path $ArtifactRoot "cross-rail-r010-sandbox-roundtrip-pnl-preview.json")
$cost = Read-Json (Join-Path $ArtifactRoot "cross-rail-r010-cost-model-readiness.json")
$fx = Read-Json (Join-Path $ArtifactRoot "cross-rail-r010-fx-conversion-readiness.json")
$account = Read-Json (Join-Path $ArtifactRoot "cross-rail-r010-account-currency-readiness.json")
$basis = Read-Json (Join-Path $ArtifactRoot "cross-rail-r010-position-cost-basis-readiness.json")
$attrib = Read-Json (Join-Path $ArtifactRoot "cross-rail-r010-pnl-attribution-readiness.json")
$boundary = Read-Json (Join-Path $ArtifactRoot "cross-rail-r010-production-pnl-boundary.json")
$audit = Read-Json (Join-Path $ArtifactRoot "cross-rail-r010-no-new-execution-safety-audit.json")
$evidence = Read-Json (Join-Path $ArtifactRoot "cross-rail-r010-build-test-validator-evidence.json")

Assert-True $ref.ReadOnlyReview "R009 ReadOnlyReview"
Assert-True $ref.R009ValidatorPassed "R009 validator reference"
Assert-True $ref.R009ReadOnlyReviewOfR008 "R009 reviewed R008 read-only"
if ([string]$ref.R009OrderFillReconciliationStatus -ne "OrdersAndFillsReconciled") { Fail "R009 order/fill reconciliation not accepted" }
if ([string]$ref.R009FlattenReconciliationStatus -ne "FlattenReconciled") { Fail "R009 flatten reconciliation not accepted" }
if ([string]$ref.R009ResidualStatus -ne "ResidualZero") { Fail "R009 residual status not accepted" }
if ([string]$ref.R009BreakDetectionStatus -ne "NoBreaksDetected") { Fail "R009 breaks status not accepted" }
if ([string]$ref.R009SandboxPnlInputsStatus -ne "SandboxFillPnlInputsCaptured") { Fail "R009 PnL inputs not accepted" }

if ([string]$fills.Status -ne "SandboxFillInputsReviewed") { Fail "fill input review status invalid" }
Assert-True $fills.SandboxOnly "fill review SandboxOnly"
Assert-False $fills.ProductionFill "fill review ProductionFill"
Assert-True $fills.NotProductionPnl "fill review NotProductionPnl"
if (@($fills.MissingFillFields).Count -ne 0) { Fail "fill input review must not have missing fill fields" }

$openSides = @{
    AUDUSD = "SELL"
    EURUSD = "SELL"
    GBPUSD = "BUY"
}
$flattenSides = @{
    AUDUSD = "BUY"
    EURUSD = "BUY"
    GBPUSD = "SELL"
}
Assert-FillRows $fills.OpenFills $openSides "open fills"
Assert-FillRows $fills.FlattenFills $flattenSides "flatten fills"

$expectedOpenPrices = @{
    AUDUSD = [decimal]"0.71659"
    EURUSD = [decimal]"1.16223"
    GBPUSD = [decimal]"1.34457"
}
$expectedFlattenPrices = @{
    AUDUSD = [decimal]"0.71661"
    EURUSD = [decimal]"1.16228"
    GBPUSD = [decimal]"1.34447"
}
foreach ($fill in $fills.OpenFills) {
    if ([decimal]$fill.FillPrice -ne $expectedOpenPrices[[string]$fill.Symbol]) {
        Fail "open fill price mismatch for $($fill.Symbol)"
    }
}
foreach ($fill in $fills.FlattenFills) {
    if ([decimal]$fill.FillPrice -ne $expectedFlattenPrices[[string]$fill.Symbol]) {
        Fail "flatten fill price mismatch for $($fill.Symbol)"
    }
}

if ([string]$preview.Status -notin @("PartialSandboxPnlPreview", "PnlPreviewBlockedMissingInputs", "ComputedSandboxTheoreticalPnl")) {
    Fail "roundtrip preview status invalid"
}
Assert-True $preview.SandboxTheoreticalOnly "preview SandboxTheoreticalOnly"
Assert-True $preview.NotProductionPnl "preview NotProductionPnl"
Assert-True $preview.NotAccountingPnl "preview NotAccountingPnl"
if ([string]$preview.Status -eq "ComputedSandboxTheoreticalPnl") {
    foreach ($row in $preview.Rows) {
        if ($null -eq $row.TheoreticalRoundTripPnl) { Fail "computed preview row missing PnL" }
        if ([string]::IsNullOrWhiteSpace([string]$row.PnlCurrency)) { Fail "computed preview row missing PnL currency" }
    }
} else {
    if (@($preview.MissingInputs).Count -eq 0) { Fail "blocked or partial preview must record missing inputs" }
    foreach ($row in $preview.Rows) {
        if ($null -ne $row.TheoreticalRoundTripPnl) { Fail "partial/blocked preview must not invent TheoreticalRoundTripPnl" }
        if ($null -ne $row.PnlCurrency) { Fail "partial/blocked preview must not invent PnlCurrency" }
    }
}

if ([string]$cost.Status -ne "MissingCostModel") { Fail "cost model should be MissingCostModel" }
Assert-False $cost.CommissionModelAvailable "CommissionModelAvailable"
Assert-False $cost.SpreadModelAvailable "SpreadModelAvailable"
Assert-False $cost.SlippageModelAvailable "SlippageModelAvailable"
Assert-True $cost.DoNotInventCosts "DoNotInventCosts"

if ([string]$fx.Status -ne "MissingFxConversion") { Fail "FX conversion should be MissingFxConversion" }
Assert-True $fx.DoNotInventFxRates "DoNotInventFxRates"
if (@($fx.AvailableConversions).Count -ne 0) { Fail "FX conversions must not be invented" }

if ([string]$account.Status -ne "MissingAccountCurrency") { Fail "account currency should be MissingAccountCurrency" }
if ($null -ne $account.AccountCurrency) { Fail "account currency must not be invented" }
Assert-True $account.DoNotInventAccountCurrency "DoNotInventAccountCurrency"

if ([string]$basis.Status -ne "MissingPositionCostBasis") { Fail "position/cost basis should be MissingPositionCostBasis" }
Assert-False $basis.PositionBasisAvailable "PositionBasisAvailable"
Assert-False $basis.CostBasisAvailable "CostBasisAvailable"
Assert-True $basis.DoNotInventPositionOrCostBasis "DoNotInventPositionOrCostBasis"

if ([string]$attrib.Status -ne "AttributionBlockedMissingInputs") { Fail "attribution should be blocked" }
Assert-False $attrib.ProductionAttributionAllowed "ProductionAttributionAllowed"

if ([string]$boundary.Status -ne "ProductionPnlBlocked") { Fail "production PnL boundary must be blocked" }
Assert-False $boundary.ProductionPnlAllowed "ProductionPnlAllowed"
Assert-False $boundary.FillBasedProductionPnlAvailable "FillBasedProductionPnlAvailable"
Assert-False $boundary.RealizedProductionPnlAvailable "RealizedProductionPnlAvailable"
Assert-False $boundary.AccountingPnlAvailable "AccountingPnlAvailable"
Assert-False $boundary.SandboxTheoreticalPreviewIsProductionPnl "SandboxTheoreticalPreviewIsProductionPnl"
Assert-False $boundary.SandboxTheoreticalPreviewIsAccountingPnl "SandboxTheoreticalPreviewIsAccountingPnl"
Assert-False $boundary.SandboxFillsAreProductionFills "SandboxFillsAreProductionFills"

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
    "NoNettedUsdWeightsProduced",
    "NoProductionPnlCalculated"
)) {
    Assert-True $audit.$name "no-new-execution audit $name"
}

Assert-True $evidence.ReadOnlyReview "evidence ReadOnlyReview"
Assert-True $evidence.NoNewExecutionOccurred "evidence NoNewExecutionOccurred"
if ($evidence.Validator.Result -ne "Passed") { Fail "validator evidence must be updated to Passed after validation" }

Write-Host "CROSS-RAIL-R010 validator passed."
