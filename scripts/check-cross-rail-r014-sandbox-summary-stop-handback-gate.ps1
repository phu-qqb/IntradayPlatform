param(
    [string]$ArtifactRoot = "artifacts/readiness/cross-rail-sandbox-handoff"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "CROSS-RAIL-R014 validation failed: $Message"
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

$requiredFiles = @(
    "cross-rail-r014-summary.md",
    "cross-rail-r014-r008-to-r013-evidence-index.json",
    "cross-rail-r014-validated-capabilities.json",
    "cross-rail-r014-unvalidated-capabilities.json",
    "cross-rail-r014-sandbox-lifecycle-final-status.json",
    "cross-rail-r014-reconciliation-final-status.json",
    "cross-rail-r014-price-delta-final-status.json",
    "cross-rail-r014-pnl-gap-final-status.json",
    "cross-rail-r014-production-boundary-final-status.json",
    "cross-rail-r014-stop-and-handback-decision.json",
    "cross-rail-r014-no-new-execution-safety-audit.json",
    "cross-rail-r014-build-test-validator-evidence.json",
    "cross-rail-r014-next-gate-plan.json"
)

$paths = @()
foreach ($file in $requiredFiles) {
    $path = Join-Path $ArtifactRoot $file
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "missing required R014 artifact: $file"
    }
    $paths += $path
}
Assert-NoCredentialValues $paths

$index = Read-Json (Join-Path $ArtifactRoot "cross-rail-r014-r008-to-r013-evidence-index.json")
$validated = Read-Json (Join-Path $ArtifactRoot "cross-rail-r014-validated-capabilities.json")
$unvalidated = Read-Json (Join-Path $ArtifactRoot "cross-rail-r014-unvalidated-capabilities.json")
$lifecycle = Read-Json (Join-Path $ArtifactRoot "cross-rail-r014-sandbox-lifecycle-final-status.json")
$recon = Read-Json (Join-Path $ArtifactRoot "cross-rail-r014-reconciliation-final-status.json")
$price = Read-Json (Join-Path $ArtifactRoot "cross-rail-r014-price-delta-final-status.json")
$gap = Read-Json (Join-Path $ArtifactRoot "cross-rail-r014-pnl-gap-final-status.json")
$boundary = Read-Json (Join-Path $ArtifactRoot "cross-rail-r014-production-boundary-final-status.json")
$decision = Read-Json (Join-Path $ArtifactRoot "cross-rail-r014-stop-and-handback-decision.json")
$audit = Read-Json (Join-Path $ArtifactRoot "cross-rail-r014-no-new-execution-safety-audit.json")
$evidence = Read-Json (Join-Path $ArtifactRoot "cross-rail-r014-build-test-validator-evidence.json")

Assert-True $index.ReadOnlyReview "index ReadOnlyReview"
foreach ($gate in @("R008", "R009", "R010", "R011", "R012R013")) {
    if ($null -eq $index.References.$gate) { Fail "missing evidence reference for $gate" }
    Assert-True $index.References.$gate.ValidatorPassed "$gate validator passed"
}

foreach ($name in @(
    "PmsPaperToR009SandboxHandoff",
    "ApprovalBinding",
    "BoundedSandboxOrderSubmission",
    "SandboxFillsCaptured",
    "SandboxFlattenSucceeded",
    "ResidualZero",
    "SandboxReconciliationClean",
    "SandboxPriceDeltaPreview"
)) {
    Assert-True $validated.$name "validated capability $name"
}
Assert-False $validated.ProductionLiveAllowed "validated ProductionLiveAllowed"

foreach ($name in @(
    "QubesEconomicSignalValidated",
    "ProductionExecutionValidated",
    "LiveLmaxValidated",
    "ProductionBrokerRouteValidated",
    "ProductionLedgerMutationValidated",
    "ProductionPnlValidated",
    "AccountingPnlValidated",
    "FullSandboxTheoreticalPnlValidated"
)) {
    Assert-False $unvalidated.$name "unvalidated capability $name"
}

if ([string]$lifecycle.Status -ne "SandboxLifecycleValidated") { Fail "sandbox lifecycle final status invalid" }
Assert-True $lifecycle.OrdersFilled "OrdersFilled"
Assert-True $lifecycle.FlattenSucceeded "FlattenSucceeded"
Assert-True $lifecycle.AllResidualsZero "AllResidualsZero"
Assert-False $lifecycle.ProductionLiveAllowed "lifecycle ProductionLiveAllowed"
foreach ($prop in $lifecycle.ResidualBySymbol.PSObject.Properties) {
    if ([decimal]$prop.Value -ne [decimal]0) { Fail "non-zero residual for $($prop.Name)" }
}

if ([string]$recon.Status -ne "SandboxReconciliationClean") { Fail "reconciliation status invalid" }
if (@($recon.Breaks).Count -ne 0) { Fail "reconciliation breaks must be empty" }
Assert-True $recon.ExpectedActualMatched "ExpectedActualMatched"
Assert-False $recon.ProductionReconciliation "ProductionReconciliation"
foreach ($name in @("ExpectedOrders", "ActualOrders", "ExpectedFills", "ActualFills", "ExpectedFlattenOrders", "ActualFlattenOrders", "ExpectedFlattenFills", "ActualFlattenFills")) {
    if ([int]$recon.$name -ne 3) { Fail "$name must be 3" }
}

if ([string]$price.Status -ne "PriceDeltaPreviewComplete") { Fail "price delta status invalid" }
Assert-True $price.SandboxPriceDeltaOnly "SandboxPriceDeltaOnly"
Assert-True $price.NotProductionPnl "NotProductionPnl"
Assert-True $price.NotAccountingPnl "NotAccountingPnl"
if ([decimal]$price.SideAdjustedPriceDeltas.AUDUSD -ne [decimal]"-0.00002") { Fail "AUDUSD delta mismatch" }
if ([decimal]$price.SideAdjustedPriceDeltas.EURUSD -ne [decimal]"-0.00005") { Fail "EURUSD delta mismatch" }
if ([decimal]$price.SideAdjustedPriceDeltas.GBPUSD -ne [decimal]"-0.00010") { Fail "GBPUSD delta mismatch" }
Assert-False $price.TheoreticalRoundTripPnlProduced "TheoreticalRoundTripPnlProduced"
Assert-False $price.PnlCurrencyAssigned "PnlCurrencyAssigned"

if ([string]$gap.Status -ne "MissingFullPnlInputsRequireExternalEvidence") { Fail "PnL gap status invalid" }
foreach ($input in @("quantity unit semantics", "account currency", "cost/spread/commission model", "FX conversion", "position/cost basis model", "PnL attribution policy")) {
    if ($input -notin @($gap.MissingInputs)) { Fail "missing PnL input not preserved: $input" }
}
if ([string]$gap.RecommendedAction -ne "PauseFullPnlUntilInputsProvided") { Fail "PnL recommended action invalid" }
Assert-False $gap.CurrentPackageCanProduceFullPnl "CurrentPackageCanProduceFullPnl"
Assert-True $gap.CurrentPackageCanProducePriceDeltaPreview "CurrentPackageCanProducePriceDeltaPreview"

if ([string]$boundary.Status -ne "ProductionBoundaryPreserved") { Fail "production boundary status invalid" }
foreach ($name in @(
    "ProductionLiveAllowed",
    "ProductionOrdersAllowed",
    "ProductionRoutesAllowed",
    "ProductionFillsAllowed",
    "ProductionPnlAllowed",
    "AccountingPnlAllowed",
    "LiveLmaxAllowed",
    "ProductionBrokerRouteAllowed",
    "ProductionLedgerMutationAllowed",
    "QubesZeroOnlyPmsApproved"
)) {
    Assert-False $boundary.$name "production boundary $name"
}

if ([string]$decision.Status -ne "CrossRailSandboxLifecycleValidated_StopBeforeProduction") { Fail "stop-and-handback status invalid" }
Assert-True $decision.CurrentWorkstreamComplete "CurrentWorkstreamComplete"
if ([string]$decision.RecommendedNextAction -ne "PRODUCT-READINESS-R001 = Cross-Rail Sandbox Readiness Product Decision Gate") { Fail "recommended next action invalid" }

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

Write-Host "CROSS-RAIL-R014 validator passed."
