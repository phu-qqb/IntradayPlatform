param(
    [string]$ArtifactRoot = "artifacts/readiness/cross-rail-sandbox-handoff"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "CROSS-RAIL-R009 validation failed: $Message"
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

function Assert-OrderRows($Rows, [string]$Name) {
    if ($null -eq $Rows -or $Rows.Count -ne 3) {
        Fail "$Name must contain exactly 3 rows"
    }
    $expected = @{
        AUDUSD = "SELL"
        EURUSD = "SELL"
        GBPUSD = "BUY"
    }
    foreach ($row in $Rows) {
        $symbol = [string]$row.Symbol
        if (-not $expected.ContainsKey($symbol)) {
            Fail "$Name contains unsupported/direct-cross symbol $symbol"
        }
        if ([string]$row.Side -ne $expected[$symbol]) {
            Fail "$Name side mismatch for ${symbol}: $($row.Side)"
        }
        if ([decimal]$row.Quantity -ne [decimal]"0.1") {
            Fail "$Name quantity mismatch for ${symbol}: $($row.Quantity)"
        }
    }
}

function Assert-FlattenRows($Rows, [string]$Name) {
    if ($null -eq $Rows -or $Rows.Count -ne 3) {
        Fail "$Name must contain exactly 3 rows"
    }
    $expected = @{
        AUDUSD = "BUY"
        EURUSD = "BUY"
        GBPUSD = "SELL"
    }
    foreach ($row in $Rows) {
        $symbol = [string]$row.Symbol
        if (-not $expected.ContainsKey($symbol)) {
            Fail "$Name contains unsupported/direct-cross symbol $symbol"
        }
        if ([string]$row.Side -ne $expected[$symbol]) {
            Fail "$Name side mismatch for ${symbol}: $($row.Side)"
        }
        if ([decimal]$row.Quantity -ne [decimal]"0.1") {
            Fail "$Name quantity mismatch for ${symbol}: $($row.Quantity)"
        }
    }
}

$requiredFiles = @(
    "cross-rail-r009-summary.md",
    "cross-rail-r009-r008-reference.json",
    "cross-rail-r009-sandbox-lifecycle-review.json",
    "cross-rail-r009-order-fill-reconciliation-review.json",
    "cross-rail-r009-flatten-reconciliation-review.json",
    "cross-rail-r009-residual-zero-review.json",
    "cross-rail-r009-sandbox-fill-pnl-inputs.json",
    "cross-rail-r009-pnl-readiness-review.json",
    "cross-rail-r009-break-detection-review.json",
    "cross-rail-r009-no-new-execution-safety-audit.json",
    "cross-rail-r009-build-test-validator-evidence.json",
    "cross-rail-r009-next-gate-plan.json"
)

$paths = @()
foreach ($file in $requiredFiles) {
    $path = Join-Path $ArtifactRoot $file
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "missing required R009 artifact: $file"
    }
    $paths += $path
}
Assert-NoCredentialValues $paths

$ref = Read-Json (Join-Path $ArtifactRoot "cross-rail-r009-r008-reference.json")
$lifecycle = Read-Json (Join-Path $ArtifactRoot "cross-rail-r009-sandbox-lifecycle-review.json")
$orderReview = Read-Json (Join-Path $ArtifactRoot "cross-rail-r009-order-fill-reconciliation-review.json")
$flattenReview = Read-Json (Join-Path $ArtifactRoot "cross-rail-r009-flatten-reconciliation-review.json")
$residual = Read-Json (Join-Path $ArtifactRoot "cross-rail-r009-residual-zero-review.json")
$pnlInputs = Read-Json (Join-Path $ArtifactRoot "cross-rail-r009-sandbox-fill-pnl-inputs.json")
$pnlReview = Read-Json (Join-Path $ArtifactRoot "cross-rail-r009-pnl-readiness-review.json")
$breaks = Read-Json (Join-Path $ArtifactRoot "cross-rail-r009-break-detection-review.json")
$audit = Read-Json (Join-Path $ArtifactRoot "cross-rail-r009-no-new-execution-safety-audit.json")
$evidence = Read-Json (Join-Path $ArtifactRoot "cross-rail-r009-build-test-validator-evidence.json")

Assert-True $ref.ReadOnlyReview "R008 ReadOnlyReview"
Assert-True $ref.R008ValidatorPassed "R008 validator reference"
if ([string]$ref.R008Status -ne "Succeeded") { Fail "R008 status must be Succeeded" }
if ([string]$ref.R008ReconciliationStatus -ne "ReconciledSandboxLifecycle") { Fail "R008 reconciliation status must be ReconciledSandboxLifecycle" }

if ([string]$lifecycle.Status -ne "SandboxLifecycleReviewed") { Fail "lifecycle status invalid" }
if ([int]$lifecycle.SubmittedOrderCount -ne 3) { Fail "submitted order count must be 3" }
if ([int]$lifecycle.FilledOrderCount -ne 3) { Fail "filled order count must be 3" }
if ([int]$lifecycle.FlattenOrderCount -ne 3) { Fail "flatten order count must be 3" }
if ([int]$lifecycle.FlattenFillCount -ne 3) { Fail "flatten fill count must be 3" }
Assert-True $lifecycle.SandboxOnly "lifecycle SandboxOnly"
Assert-False $lifecycle.ProductionLifecycle "lifecycle ProductionLifecycle"

if ([string]$orderReview.Status -ne "OrdersAndFillsReconciled") { Fail "order/fill reconciliation must pass" }
if ([int]$orderReview.ExpectedOrders -ne 3 -or [int]$orderReview.ActualOrders -ne 3) { Fail "order reconciliation counts mismatch" }
if ([int]$orderReview.ExpectedFills -ne 3 -or [int]$orderReview.ActualFills -ne 3) { Fail "fill reconciliation counts mismatch" }
Assert-OrderRows $orderReview.Orders "orders"
if (@($orderReview.Breaks).Count -ne 0) { Fail "order/fill breaks must be empty" }

$expectedPrices = @{
    AUDUSD = [decimal]"0.71659"
    EURUSD = [decimal]"1.16223"
    GBPUSD = [decimal]"1.34457"
}
foreach ($fill in $orderReview.Fills) {
    $symbol = [string]$fill.Symbol
    if (-not $expectedPrices.ContainsKey($symbol)) { Fail "unexpected fill symbol $symbol" }
    if ([decimal]$fill.FillPrice -ne $expectedPrices[$symbol]) { Fail "fill price mismatch for $symbol" }
    Assert-True $fill.SandboxOnly "fill $symbol SandboxOnly"
    Assert-False $fill.ProductionFill "fill $symbol ProductionFill"
    Assert-True $fill.NotProductionPnl "fill $symbol NotProductionPnl"
}

if ([string]$flattenReview.Status -ne "FlattenReconciled") { Fail "flatten reconciliation must pass" }
if ([int]$flattenReview.ExpectedFlattenOrders -ne 3 -or [int]$flattenReview.ActualFlattenOrders -ne 3) { Fail "flatten order counts mismatch" }
if ([int]$flattenReview.ExpectedFlattenFills -ne 3 -or [int]$flattenReview.ActualFlattenFills -ne 3) { Fail "flatten fill counts mismatch" }
Assert-FlattenRows $flattenReview.FlattenOrders "flatten orders"
if (@($flattenReview.Breaks).Count -ne 0) { Fail "flatten breaks must be empty" }
foreach ($fill in $flattenReview.FlattenFills) {
    Assert-True $fill.SandboxOnly "flatten fill $($fill.Symbol) SandboxOnly"
    Assert-False $fill.ProductionFill "flatten fill $($fill.Symbol) ProductionFill"
    Assert-True $fill.NotProductionPnl "flatten fill $($fill.Symbol) NotProductionPnl"
}

if ([string]$residual.Status -ne "ResidualZero") { Fail "residual status must be ResidualZero" }
Assert-True $residual.AllResidualsZero "AllResidualsZero"
foreach ($prop in $residual.ResidualBySymbol.PSObject.Properties) {
    if ([decimal]$prop.Value -ne [decimal]0) { Fail "non-zero residual for $($prop.Name)" }
}

if ([string]$pnlInputs.Status -ne "SandboxFillPnlInputsCaptured") { Fail "PnL input status invalid" }
Assert-True $pnlInputs.NotProductionPnl "pnl inputs NotProductionPnl"
Assert-True $pnlInputs.SandboxOnly "pnl inputs SandboxOnly"
Assert-False $pnlInputs.ProductionFill "pnl inputs ProductionFill"
Assert-False $pnlInputs.PnlComputedNow "pnl inputs PnlComputedNow"
Assert-True $pnlInputs.PnlPreviewNotComputed "pnl inputs PnlPreviewNotComputed"
if (@($pnlInputs.OpenFills).Count -ne 3 -or @($pnlInputs.FlattenFills).Count -ne 3) { Fail "PnL fill inputs must include open and flatten fills" }

if ([string]$pnlReview.Status -ne "PnlInputReadinessReviewed") { Fail "PnL review status invalid" }
Assert-False $pnlReview.ProductionPnlAllowed "ProductionPnlAllowed"
Assert-False $pnlReview.FillBasedProductionPnlAvailable "FillBasedProductionPnlAvailable"
Assert-False $pnlReview.SandboxTheoreticalPnlAvailable "SandboxTheoreticalPnlAvailable"
Assert-True $pnlReview.SandboxFillInputsAvailable "SandboxFillInputsAvailable"

if ([string]$breaks.Status -ne "NoBreaksDetected") { Fail "break detection must be NoBreaksDetected" }
foreach ($name in @("Breaks", "MissingOrders", "MissingFills", "MissingFlattenOrders", "ResidualBreaks", "DuplicateIdempotencyKeys", "UnsupportedSymbolBreaks", "DirectCrossSubmissions")) {
    if (@($breaks.$name).Count -ne 0) { Fail "$name must be empty" }
}

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
Assert-False $audit.SandboxFillsMarkedProduction "SandboxFillsMarkedProduction"

Assert-True $evidence.ReadOnlyReview "evidence ReadOnlyReview"
Assert-True $evidence.NoNewExecutionOccurred "evidence NoNewExecutionOccurred"

Write-Host "CROSS-RAIL-R009 validator passed."
