param(
    [string]$ArtifactRoot = "artifacts/readiness/cross-rail-sandbox-handoff"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "CROSS-RAIL-R012R013 validation failed: $Message"
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

function Assert-PriceDeltaRow($Row, [string]$Symbol, [string]$OpenSide, [string]$FlattenSide, [decimal]$OpenPrice, [decimal]$FlattenPrice, [decimal]$RawDelta, [decimal]$SideDelta) {
    if ([string]$Row.Symbol -ne $Symbol) { Fail "row symbol mismatch for $Symbol" }
    if ([string]$Row.OpenSide -ne $OpenSide) { Fail "$Symbol open side mismatch" }
    if ([string]$Row.FlattenSide -ne $FlattenSide) { Fail "$Symbol flatten side mismatch" }
    if ([decimal]$Row.OpenQuantity -ne [decimal]"0.1") { Fail "$Symbol open quantity mismatch" }
    if ([decimal]$Row.FlattenQuantity -ne [decimal]"0.1") { Fail "$Symbol flatten quantity mismatch" }
    if ([decimal]$Row.OpenFillPrice -ne $OpenPrice) { Fail "$Symbol open price mismatch" }
    if ([decimal]$Row.FlattenFillPrice -ne $FlattenPrice) { Fail "$Symbol flatten price mismatch" }
    if ([decimal]$Row.RawPriceDelta -ne $RawDelta) { Fail "$Symbol raw price delta mismatch: $($Row.RawPriceDelta)" }
    if ([decimal]$Row.SideAdjustedPriceDelta -ne $SideDelta) { Fail "$Symbol side-adjusted price delta mismatch: $($Row.SideAdjustedPriceDelta)" }
    if ($null -ne $Row.TheoreticalRoundTripPnl) { Fail "$Symbol TheoreticalRoundTripPnl must remain null" }
    if ($null -ne $Row.PnlCurrency) { Fail "$Symbol PnlCurrency must remain null" }
    Assert-True $Row.SandboxPriceDeltaOnly "$Symbol SandboxPriceDeltaOnly"
    Assert-True $Row.NotProductionPnl "$Symbol NotProductionPnl"
    Assert-True $Row.NotAccountingPnl "$Symbol NotAccountingPnl"
    Assert-True $Row.NotRiskPnl "$Symbol NotRiskPnl"
    Assert-True $Row.NotTaxPnl "$Symbol NotTaxPnl"
}

$requiredFiles = @(
    "cross-rail-r012r013-summary.md",
    "cross-rail-r012r013-r011-reference.json",
    "cross-rail-r012r013-open-flatten-linkage-review.json",
    "cross-rail-r012r013-partial-sandbox-price-delta-preview.json",
    "cross-rail-r012r013-full-pnl-missing-inputs.json",
    "cross-rail-r012r013-pnl-gap-closure-decision.json",
    "cross-rail-r012r013-production-pnl-boundary.json",
    "cross-rail-r012r013-no-new-execution-safety-audit.json",
    "cross-rail-r012r013-build-test-validator-evidence.json",
    "cross-rail-r012r013-next-gate-plan.json"
)

$paths = @()
foreach ($file in $requiredFiles) {
    $path = Join-Path $ArtifactRoot $file
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "missing required R012R013 artifact: $file"
    }
    $paths += $path
}
Assert-NoCredentialValues $paths

$ref = Read-Json (Join-Path $ArtifactRoot "cross-rail-r012r013-r011-reference.json")
$linkage = Read-Json (Join-Path $ArtifactRoot "cross-rail-r012r013-open-flatten-linkage-review.json")
$preview = Read-Json (Join-Path $ArtifactRoot "cross-rail-r012r013-partial-sandbox-price-delta-preview.json")
$missing = Read-Json (Join-Path $ArtifactRoot "cross-rail-r012r013-full-pnl-missing-inputs.json")
$decision = Read-Json (Join-Path $ArtifactRoot "cross-rail-r012r013-pnl-gap-closure-decision.json")
$boundary = Read-Json (Join-Path $ArtifactRoot "cross-rail-r012r013-production-pnl-boundary.json")
$audit = Read-Json (Join-Path $ArtifactRoot "cross-rail-r012r013-no-new-execution-safety-audit.json")
$evidence = Read-Json (Join-Path $ArtifactRoot "cross-rail-r012r013-build-test-validator-evidence.json")

Assert-True $ref.ReadOnlyReview "R011 ReadOnlyReview"
Assert-True $ref.R011ValidatorPassed "R011 validator reference"
if ([string]$ref.R011ReadinessClassification -ne "PartialSandboxPnlInputsOnly") { Fail "R011 readiness classification mismatch" }
if ([string]$ref.R011FutureR012Readiness -ne "ReadyForPartialPriceDeltaPreviewOnly") { Fail "R011 FutureR012Readiness mismatch" }
Assert-True $ref.R011NoNewExecution "R011NoNewExecution"

if ([string]$linkage.Status -ne "OpenFlattenLinkageValidated") { Fail "linkage must be validated" }
Assert-True $linkage.AllLinksValid "AllLinksValid"
Assert-True $linkage.AllResidualsZero "AllResidualsZero"
if (@($linkage.Breaks).Count -ne 0) { Fail "linkage breaks must be empty" }
if (@($linkage.Links).Count -ne 3) { Fail "linkage must contain three links" }
foreach ($link in $linkage.Links) {
    Assert-True $link.QuantitiesMatch "$($link.Symbol) QuantitiesMatch"
    Assert-True $link.SidesOpposite "$($link.Symbol) SidesOpposite"
}

if ([string]$preview.Status -ne "PartialSandboxPriceDeltaPreviewComputed") { Fail "price delta preview status invalid" }
Assert-True $preview.SandboxPriceDeltaOnly "preview SandboxPriceDeltaOnly"
Assert-True $preview.NotProductionPnl "preview NotProductionPnl"
Assert-True $preview.NotAccountingPnl "preview NotAccountingPnl"
Assert-True $preview.NotRiskPnl "preview NotRiskPnl"
Assert-True $preview.NotTaxPnl "preview NotTaxPnl"
Assert-False $preview.QuantityUnitMultiplicationApplied "QuantityUnitMultiplicationApplied"
Assert-True $preview.FullPnlBlocked "FullPnlBlocked"
if (@($preview.Rows).Count -ne 3) { Fail "preview must contain three rows" }

$rows = @{}
foreach ($row in $preview.Rows) { $rows[[string]$row.Symbol] = $row }
Assert-PriceDeltaRow $rows["AUDUSD"] "AUDUSD" "SELL" "BUY" ([decimal]"0.71659") ([decimal]"0.71661") ([decimal]"0.00002") ([decimal]"-0.00002")
Assert-PriceDeltaRow $rows["EURUSD"] "EURUSD" "SELL" "BUY" ([decimal]"1.16223") ([decimal]"1.16228") ([decimal]"0.00005") ([decimal]"-0.00005")
Assert-PriceDeltaRow $rows["GBPUSD"] "GBPUSD" "BUY" "SELL" ([decimal]"1.34457") ([decimal]"1.34447") ([decimal]"-0.00010") ([decimal]"-0.00010")

if ([string]$missing.Status -ne "FullPnlInputsStillMissing") { Fail "full PnL missing input status invalid" }
foreach ($input in @("quantity unit semantics", "account currency", "cost/spread/commission model", "FX conversion", "position/cost basis model", "PnL attribution policy")) {
    if ($input -notin @($missing.MissingInputs)) { Fail "missing full PnL input not preserved: $input" }
}
Assert-True $missing.DoNotInventMissingInputs "DoNotInventMissingInputs"
Assert-True $missing.FutureGateRequired "FutureGateRequired"
Assert-False $missing.FullPnlCanBeComputedNow "FullPnlCanBeComputedNow"
Assert-True $missing.SandboxPriceDeltaPreviewOnlyAvailable "SandboxPriceDeltaPreviewOnlyAvailable"

if ([string]$decision.Status -ne "MissingFullPnlInputsRequireExternalEvidence") { Fail "PnL gap closure decision invalid" }
Assert-False $decision.CurrentPackageCanProduceFullPnl "CurrentPackageCanProduceFullPnl"
Assert-True $decision.CurrentPackageCanProducePriceDeltaPreview "CurrentPackageCanProducePriceDeltaPreview"
if ([string]$decision.RecommendedAction -ne "PauseFullPnlUntilInputsProvided") { Fail "recommended action mismatch" }
Assert-True $decision.CurrentSandboxPriceDeltaPreviewComplete "CurrentSandboxPriceDeltaPreviewComplete"
Assert-True $decision.ReadyForFullSandboxPnlOnlyIfInputsProvided "ReadyForFullSandboxPnlOnlyIfInputsProvided"

if ([string]$boundary.Status -ne "ProductionPnlBlocked") { Fail "production PnL boundary must be blocked" }
Assert-False $boundary.ProductionPnlAllowed "ProductionPnlAllowed"
Assert-False $boundary.FillBasedProductionPnlAvailable "FillBasedProductionPnlAvailable"
Assert-False $boundary.RealizedProductionPnlAvailable "RealizedProductionPnlAvailable"
Assert-False $boundary.AccountingPnlAvailable "AccountingPnlAvailable"
Assert-False $boundary.SandboxPriceDeltaIsProductionPnl "SandboxPriceDeltaIsProductionPnl"
Assert-False $boundary.SandboxPriceDeltaIsAccountingPnl "SandboxPriceDeltaIsAccountingPnl"
Assert-False $boundary.SandboxPriceDeltaIsRiskPnl "SandboxPriceDeltaIsRiskPnl"
Assert-False $boundary.SandboxPriceDeltaIsTaxPnl "SandboxPriceDeltaIsTaxPnl"

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

Write-Host "CROSS-RAIL-R012R013 validator passed."
