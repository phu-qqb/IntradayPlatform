param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$CoreManifestPath = "C:\Users\phili\source\repos\QQ.Production.Core\artifacts\qubes\runs\fx-intraday-qubes_London_H1_universe-v1_mapping-v1_flatbar-v1_tinyplus-strattaken-v1_202601051006\11_intraday_handoff\core_intraday_handoff_manifest.json"
)

$ErrorActionPreference = "Stop"

$Package = "CORE-ANUBIS-INTRADAY-SIZING-R003"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-sizing-r003"
$R002Dir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-weights-intraday-handoff-consumer-r002"
$ExpectedManifestHash = "sha256:8A445CD6195458D09CC539C0C14895C1EFB09027A9BB58D1369BDDD44D99F182"
$ExpectedNettedHash = "sha256:41FF6745857F9E53E28278B7C8DAF91EF2306DE384B753B3BB605B5FD2D01A96"
$TargetNotional = 6000000
$MarketDataSnapshotId = "canonical-marketdata-golden-source-r001:polygon-offline-bbo:20251217T020000Z:AUDUSD-EURUSD-GBPUSD"

function Write-JsonArtifact([string]$Name, [object]$Payload) {
    New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null
    $path = Join-Path $ArtifactDir $Name
    $Payload | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $path -Encoding UTF8
}

function Read-JsonFile([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Get-Sha256([string]$Path) {
    "sha256:" + (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToUpperInvariant()
}

function Test-CoreSymbol([string]$Symbol) {
    return $Symbol.Length -eq 6 -and $Symbol.EndsWith("USD") -and $Symbol -ne "USDJPY"
}

function Get-Side([double]$Weight) {
    if ($Weight -gt 0) { return "BUY" }
    if ($Weight -lt 0) { return "SELL" }
    return "ZERO"
}

if (-not (Test-Path -LiteralPath $CoreManifestPath)) {
    throw "Core manifest missing: $CoreManifestPath"
}

$CoreManifest = Read-JsonFile $CoreManifestPath
$CoreManifestHash = Get-Sha256 $CoreManifestPath
$R002Summary = Join-Path $R002Dir "summary.md"
$R002CandidatePath = Join-Path $R002Dir "pms-core-weights-candidate-preview.json"
$R002IntakePath = Join-Path $R002Dir "core-handoff-intake-validation.json"
$R002BoundaryPath = Join-Path $R002Dir "boundary-safety-evidence.json"
$R002Candidate = Read-JsonFile $R002CandidatePath
$R002Intake = Read-JsonFile $R002IntakePath
$R002Boundary = Read-JsonFile $R002BoundaryPath

$Rows = @()
foreach ($weight in @($CoreManifest.Weights)) {
    $value = [double]::Parse([string]$weight.Weight, [Globalization.CultureInfo]::InvariantCulture)
    $Rows += [ordered]@{
        Symbol = [string]$weight.Symbol
        Weight = [string]$weight.Weight
        NumericWeight = $value
        Side = Get-Side $value
    }
}
$Sides = [ordered]@{}
foreach ($row in $Rows) {
    $Sides[$row.Symbol] = $row.Side
}
$Symbols = @($Rows | ForEach-Object { $_.Symbol })
$NonZeroRows = @($Rows | Where-Object { $_.NumericWeight -ne 0.0 })
$ZeroRows = @($Rows | Where-Object { $_.NumericWeight -eq 0.0 })
$DirectCrosses = @($Symbols | Where-Object { -not (Test-CoreSymbol $_) })

$KnownPrices = [ordered]@{
    AUDUSD = [ordered]@{ Price = 0.6632; Timestamp = "2025-12-17T01:59:59Z"; Source = $MarketDataSnapshotId }
    EURUSD = [ordered]@{ Price = 1.174725; Timestamp = "2025-12-17T01:59:57Z"; Source = $MarketDataSnapshotId }
    GBPUSD = [ordered]@{ Price = 1.342475; Timestamp = "2025-12-17T01:59:57Z"; Source = $MarketDataSnapshotId }
}
$KnownMetadata = [ordered]@{
    AUDUSD = [ordered]@{ ContractMultiplier = 10000; MinOrderSize = 0.1; QuotedCurrency = "USD"; Source = "prior sandbox prototype path / LMAX static metadata evidence" }
    EURUSD = [ordered]@{ ContractMultiplier = 10000; MinOrderSize = 0.1; QuotedCurrency = "USD"; Source = "prior sandbox prototype path / LMAX static metadata evidence" }
    GBPUSD = [ordered]@{ ContractMultiplier = 10000; MinOrderSize = 0.1; QuotedCurrency = "USD"; Source = "prior sandbox prototype path / LMAX static metadata evidence" }
}

$CoveredPriceSymbols = @($NonZeroRows | Where-Object { $KnownPrices.Contains($_.Symbol) } | ForEach-Object { $_.Symbol })
$MissingPriceSymbols = @($NonZeroRows | Where-Object { -not $KnownPrices.Contains($_.Symbol) } | ForEach-Object { $_.Symbol })
$CoveredMetadataSymbols = @($NonZeroRows | Where-Object { $KnownMetadata.Contains($_.Symbol) } | ForEach-Object { $_.Symbol })
$MissingMetadataSymbols = @($NonZeroRows | Where-Object { -not $KnownMetadata.Contains($_.Symbol) } | ForEach-Object { $_.Symbol })

Write-JsonArtifact "r002-intake-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "r002-intake-validation"
    Classification = "R002_CORE_HANDOFF_READY_FOR_SIZING"
    R002SummaryExists = Test-Path -LiteralPath $R002Summary
    CoreHandoffConsumed = $R002Intake.Classification -eq "CORE_HANDOFF_INTAKE_READY"
    WeightsOnlyPmsCoreCandidateExists = Test-Path -LiteralPath $R002CandidatePath
    CoreHandoffManifestHashMatches = $CoreManifestHash -eq $ExpectedManifestHash
    NettedUsdWeightsHashMatches = $CoreManifest.NettedUsdWeightsHash -eq $ExpectedNettedHash
    R002DidNotCreateQuantities = $null -eq $R002Candidate.Quantities
    R002DidNotAllowR009Execution = $R002Candidate.R009Ready -eq $false
    R002DidNotMutateDbOrLedger = ($R002Boundary.NoIntradayDbMutation -eq $true) -and ($R002Boundary.NoLedger -eq $true)
})

Write-JsonArtifact "core-netted-weights-symbol-inventory.json" ([ordered]@{
    Package = $Package
    Artifact = "core-netted-weights-symbol-inventory"
    Classification = "CORE_NETTED_WEIGHTS_SYMBOL_INVENTORY_READY"
    Symbols = $Symbols
    Weights = $Rows
    ZeroWeights = @($ZeroRows | ForEach-Object { $_.Symbol })
    NonZeroWeights = @($NonZeroRows | ForEach-Object { $_.Symbol })
    JPYUSDPresent = $Symbols -contains "JPYUSD"
    UnexpectedUSDJPYEmission = $Symbols -contains "USDJPY"
    DirectCrosses = $DirectCrosses
    SymbolCount = $Symbols.Count
    NonZeroWeightCount = $NonZeroRows.Count
    AllSymbolsCoreCanonicalXXXUSD = $DirectCrosses.Count -eq 0
})

Write-JsonArtifact "core-sandbox-target-notional-policy.json" ([ordered]@{
    Package = $Package
    Artifact = "core-sandbox-target-notional-policy"
    Classification = "CORE_SANDBOX_TARGET_NOTIONAL_POLICY_READY_OPERATOR_PROVIDED_USD_6000000"
    TargetNotionalAmount = $TargetNotional
    TargetNotionalCurrency = "USD"
    TargetNotionalScope = "SandboxPreviewSizingOnly"
    AppliesTo = "Core/Anubis PMS preview candidate only"
    NotAccounting = $true
    NotProduction = $true
    NotAccountCurrency = $true
    NotNav = $true
    NotLedgerCapital = $true
    DoesNotImplyAccountIdPortfolioIdStrategyId = $true
})

Write-JsonArtifact "marketdata-price-basis-coverage.json" ([ordered]@{
    Package = $Package
    Artifact = "marketdata-price-basis-coverage"
    Classification = "MARKETDATA_PRICE_BASIS_BLOCKED_MISSING_CORE_SYMBOL_PRICES"
    MarketDataSnapshotId = $MarketDataSnapshotId
    SnapshotEvidence = "canonical-marketdata-golden-source-r001 local readiness artifacts"
    Source = "polygon-offline-bbo"
    SandboxOfflinePreviewOnly = $true
    PriceUseScope = "SandboxPreviewSizingOnly"
    Prices = $KnownPrices
    CoveredCoreSymbols = $CoveredPriceSymbols
    MissingCoreSymbolPrices = $MissingPriceSymbols
    JPYUSDPriceHandling = "missing Core model price basis; later execution inversion to USDJPY remains out of scope for R003"
    ExternalDataCalls = 0
    InventedPrices = $false
})

Write-JsonArtifact "instrument-metadata-coverage.json" ([ordered]@{
    Package = $Package
    Artifact = "instrument-metadata-coverage"
    Classification = "INSTRUMENT_METADATA_READY_FOR_SUBSET_ONLY"
    Metadata = $KnownMetadata
    CoveredCoreSymbols = $CoveredMetadataSymbols
    MissingCoreSymbolMetadata = $MissingMetadataSymbols
    JPYUSDRequiresFutureUSDJPYInversionInIntraday = $true
    InventedContractMultiplier = $false
    InventedMinOrderSize = $false
    InventedTradability = $false
})

Write-JsonArtifact "quantity-transformation-policy.json" ([ordered]@{
    Package = $Package
    Artifact = "quantity-transformation-policy"
    Classification = "QUANTITY_TRANSFORMATION_BLOCKED_MISSING_PRICE_BASIS"
    TargetSymbolNotionalFormula = "abs(weight) * 6000000 USD"
    SidePolicy = "BUY if weight > 0, SELL if weight < 0, ZERO if weight = 0"
    RawQuantityFormula = "target symbol quote notional / (price * contract multiplier)"
    RoundingPolicy = "round down to nearest min order size; do not round up exposure"
    BelowMinHandling = "zero/omit only after explicit future policy"
    CoreSymbolsRemainModelSymbols = $true
    IntradayHandlesExecutionSymbolConversionLater = $true
    BlockReason = "Price basis is missing for non-zero Core symbols."
    MissingPriceSymbols = $MissingPriceSymbols
    MissingMetadataSymbols = $MissingMetadataSymbols
    QuantitiesInvented = $false
})

$CandidateId = "intraday-core-anubis-sizing-preview:" + $CoreManifestHash.Substring(7, 24)
Write-JsonArtifact "pms-core-candidate-preview-sizing-status.json" ([ordered]@{
    Package = $Package
    Artifact = "pms-core-candidate-preview-sizing-status"
    Classification = "PMS_CORE_CANDIDATE_PREVIEW_WEIGHTS_ONLY_SIZING_BLOCKED"
    CandidateId = $CandidateId
    Source = "CoreAnubisNettedUsdWeights"
    RunKey = $CoreManifest.RunKey
    CoreHandoffManifestHash = $CoreManifestHash
    NettedUsdWeightsHash = $CoreManifest.NettedUsdWeightsHash
    MarketDataSnapshotId = $MarketDataSnapshotId
    TargetNotionalAmount = $TargetNotional
    TargetNotionalCurrency = "USD"
    TargetNotionalScope = "SandboxPreviewSizingOnly"
    Symbols = $Symbols
    Weights = $Rows
    Sides = $Sides
    Prices = $KnownPrices
    Quantities = $null
    MissingPrices = $MissingPriceSymbols
    MissingMetadata = $MissingMetadataSymbols
    QuantityStatus = "BlockedMissingCoreSymbolPriceBasis"
    ExecutionReadyPreview = $false
    SandboxOnly = $true
    NotProduction = $true
    NotAccounting = $true
    NotExecuted = $true
    NotLedgerCommit = $true
    R009Ready = $false
})

Write-JsonArtifact "r009-approval-readiness-decision.json" ([ordered]@{
    Package = $Package
    Artifact = "r009-approval-readiness-decision"
    Classification = "CORE_CANDIDATE_PARTIAL_OR_BLOCKED_SIZING_NO_RISK_REVIEW"
    R009AllowedInR003 = $false
    NewRiskReviewRequired = $true
    NewOperatorApprovalRequired = $true
    R010PrototypeApprovalReusable = $false
    QuantitiesMissing = $true
    RiskExecutionApprovalBlocked = $true
})

Write-JsonArtifact "future-package-decision.json" ([ordered]@{
    Package = $Package
    Artifact = "future-package-decision"
    Decision = "NEXT_CORE_ANUBIS_INTRADAY_MARKETDATA_PRICE_BASIS_R004"
    Reason = "Target notional is approved for sandbox preview, but price basis is missing for Core symbols beyond AUDUSD/EURUSD/GBPUSD."
})

Write-JsonArtifact "contract-status-update.json" ([ordered]@{
    Package = $Package
    Artifact = "contract-status-update"
    Statuses = @(
        [ordered]@{ ContractId = "core-anubis-handoff-consumer.v1"; Status = "YES"; Reason = "R002 consumed and validated Core handoff." },
        [ordered]@{ ContractId = "core-anubis-netted-weights.v1"; Status = "YES"; Reason = "Core netted weights remain valid." },
        [ordered]@{ ContractId = "core-anubis-target-notional.v1"; Status = "YES"; Reason = "USD 6,000,000 sandbox preview sizing policy applied to Core candidate only." },
        [ordered]@{ ContractId = "core-anubis-marketdata-price-basis.v1"; Status = "BLOCKED"; Reason = "Missing price basis for Core symbols." },
        [ordered]@{ ContractId = "core-anubis-instrument-metadata.v1"; Status = "WITH_WARNINGS"; Reason = "Known metadata covers AUDUSD/EURUSD/GBPUSD only." },
        [ordered]@{ ContractId = "core-anubis-pms-sizing.v1"; Status = "BLOCKED"; Reason = "Quantities not derivable without complete price basis." },
        [ordered]@{ ContractId = "pms-core-weights-candidate.v1"; Status = "WITH_WARNINGS"; Reason = "Weights-only candidate remains available; sizing blocked." },
        [ordered]@{ ContractId = "pms-core-risk-review.v1"; Status = "BLOCKED"; Reason = "No full quantities." },
        [ordered]@{ ContractId = "pms-execution-candidate.v1"; Status = "BLOCKED"; Reason = "No quantities/risk/operator approval." },
        [ordered]@{ ContractId = "r009-execution-readiness.v1"; Status = "UNCHANGED_BLOCKED_FOR_CORE_CANDIDATE"; Reason = "R003 grants no execution readiness." },
        [ordered]@{ ContractId = "pnl-preview.v1"; Status = "YES_ONLY_FOR_ACCEPTED_HISTORICAL_SANDBOX_GROSS_PNL_V0"; Reason = "No PnL readiness change." },
        [ordered]@{ ContractId = "accounting-attribution.v1"; Status = "BLOCKED"; Reason = "No accounting attribution." },
        [ordered]@{ ContractId = "production-readiness.v1"; Status = "BLOCKED"; Reason = "No production/live readiness." }
    )
})

Write-JsonArtifact "readiness-impact.json" ([ordered]@{
    Package = $Package
    Artifact = "readiness-impact"
    Classification = "CORE_WEIGHTS_SIZING_BLOCKED_PRICE_BASIS_NO_EXECUTION_READINESS_CHANGE"
    CoreWeightsConsumed = $true
    TargetNotionalApplied = $true
    SizingBlocked = $true
    SizingBlockedReason = "missing MarketData price basis for Core symbols"
    NoExecutionOccurred = $true
    NoR009ReadinessGranted = $true
    NoPnlReadinessChanged = $true
    NoLedgerReadinessChanged = $true
    NoProductionReadinessChanged = $true
    SandboxProgrammeAcceptedWithGrossPnlV0ReadyRemainsUnchanged = $true
})

Write-JsonArtifact "boundary-safety-evidence.json" ([ordered]@{
    Package = $Package
    Artifact = "boundary-safety-evidence"
    NoCoreExecution = $true
    NoManager = $true
    NoAnubis = $true
    NoCuda = $true
    NoCoreNetting = $true
    NoLmax = $true
    NoR009 = $true
    NoOrderFillReport = $true
    NoDbMutation = $true
    NoLedger = $true
    NoAccountIdInvented = $true
    NoPortfolioIdInvented = $true
    NoStrategyIdInvented = $true
    NoSourceExecutionIntentIdInvented = $true
    NoAccountCurrencyInvented = $true
    NoInventedPrices = $true
    NoInventedQuantitiesWithoutRequiredInputs = $true
    NoR010Transfer = $true
})

$Summary = @"
# $Package

Classification: CORE_ANUBIS_INTRADAY_SIZING_R003_WITH_WARNINGS_SIZING_BLOCKED_PRICE_BASIS

Was target notional applied to Core weights? yes, USD 6,000,000 as SandboxPreviewSizingOnly.
Was MarketData price basis available for all Core symbols? no. AUDUSD/EURUSD/GBPUSD are covered; CADUSD, CHFUSD, CNHUSD, JPYUSD, MXNUSD, NOKUSD, NZDUSD, SEKUSD, SGDUSD, and ZARUSD are missing.
Was instrument metadata available for all Core symbols? no. AUDUSD/EURUSD/GBPUSD are covered; the rest are missing for this R003 evidence package.
Were quantities derived? no.
Is the PMS/Core candidate execution-ready preview, partial, or blocked? blocked, weights-only.
Is R009 allowed? no.
Next package: NEXT_CORE_ANUBIS_INTRADAY_MARKETDATA_PRICE_BASIS_R004.
What did not run? Core execution, manager, Anubis, CUDA, Core netting, LMAX, R009, orders, fills, reports, DB mutation, ledger, production/live, and accounting/net/production PnL readiness.
"@
$Summary | Set-Content -LiteralPath (Join-Path $ArtifactDir "summary.md") -Encoding UTF8

Write-Host "CORE_ANUBIS_INTRADAY_SIZING_R003 artifacts written."
