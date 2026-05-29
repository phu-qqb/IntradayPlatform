param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$CoreManifestPath = "C:\Users\phili\source\repos\QQ.Production.Core\artifacts\qubes\runs\fx-intraday-qubes_London_H1_universe-v1_mapping-v1_flatbar-v1_tinyplus-strattaken-v1_202601051006\11_intraday_handoff\core_intraday_handoff_manifest.json"
)

$ErrorActionPreference = "Stop"

$Package = "CORE-ANUBIS-INTRADAY-FX-DATA-BASIS-R005"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-fx-data-basis-r005"
$R004Dir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-marketdata-price-basis-r004"
$ExpectedCoreManifestHash = "sha256:8A445CD6195458D09CC539C0C14895C1EFB09027A9BB58D1369BDDD44D99F182"
$ExpectedNettedHash = "sha256:41FF6745857F9E53E28278B7C8DAF91EF2306DE384B753B3BB605B5FD2D01A96"
$TargetNotional = 6000000

function Write-JsonArtifact([string]$Name, [object]$Payload) {
    New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null
    $path = Join-Path $ArtifactDir $Name
    $Payload | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $path -Encoding UTF8
}

function Read-JsonFile([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Get-Sha256([string]$Path) {
    "sha256:" + (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToUpperInvariant()
}

if (-not (Test-Path -LiteralPath $CoreManifestPath)) {
    throw "Core handoff manifest missing: $CoreManifestPath"
}

$CoreManifest = Read-JsonFile $CoreManifestPath
$CoreManifestHash = Get-Sha256 $CoreManifestPath
$R004Summary = Join-Path $R004Dir "summary.md"
$R004Universe = Read-JsonFile (Join-Path $R004Dir "core-symbol-price-universe.json")
$R004PriceCoverage = Read-JsonFile (Join-Path $R004Dir "price-basis-coverage-by-core-symbol.json")
$R004MetadataCoverage = Read-JsonFile (Join-Path $R004Dir "instrument-metadata-coverage-by-core-symbol.json")
$R004Candidate = Read-JsonFile (Join-Path $R004Dir "updated-pms-core-candidate-status.json")
$R004Boundary = Read-JsonFile (Join-Path $R004Dir "boundary-safety-evidence.json")

$PriceRows = @($R004PriceCoverage.SymbolCoverage)
$MetadataRows = @($R004MetadataCoverage.SymbolCoverage)
$PriceCovered = @($R004PriceCoverage.SymbolsCovered)
$PriceMissing = @($R004PriceCoverage.SymbolsMissing)
$MetadataCovered = @($R004MetadataCoverage.SymbolsCovered)
$MetadataMissing = @($R004MetadataCoverage.SymbolsMissing)
$AllMissing = @($PriceMissing + $MetadataMissing | Select-Object -Unique)
$SourcePriceSymbols = @("AUDUSD","EURUSD","GBPUSD","NZDUSD","USDCAD","USDCHF","USDJPY","USDCNH","USDMXN","USDNOK","USDSEK","USDSGD","USDZAR")

$UniverseRows = @()
foreach ($symbol in @($R004Universe.Symbols)) {
    $price = $PriceRows | Where-Object { $_.CoreSymbol -eq $symbol.CoreSymbol } | Select-Object -First 1
    $metadata = $MetadataRows | Where-Object { $_.CoreSymbol -eq $symbol.CoreSymbol } | Select-Object -First 1
    $status = if (($price.Classification -match "READY") -and ($metadata.Classification -match "READY")) {
        "covered"
    } elseif (($price.Classification -match "READY") -or ($metadata.Classification -match "READY")) {
        "partial"
    } else {
        "missing"
    }
    $UniverseRows += [ordered]@{
        CoreSymbol = $symbol.CoreSymbol
        Weight = $symbol.Weight
        NonZero = $symbol.NonZero
        Side = $symbol.Side
        CoreModelSymbol = "XXXUSD"
        PreferredDirectPriceSymbol = $symbol.DirectRequiredPriceSymbol
        PreferredInversePriceSymbol = $symbol.PossibleInversePriceSymbol
        ExpectedExecutionTradableSymbolLater = $symbol.ExecutionSymbolLater
        RequiresInversionLater = $symbol.PossibleInversePriceSymbol -ne $null
        NeedsPrice = $symbol.NeedsPrice
        NeedsMetadata = $symbol.NeedsMetadata
        R004Status = $status
        JPYUSDCaveat = $symbol.JPYUSDCaveat
    }
}

$PriceEvidence = @()
foreach ($row in $PriceRows | Where-Object { $_.Classification -match "READY" }) {
    $PriceEvidence += [ordered]@{
        Path = $row.SourceArtifact
        SourceType = "local offline Polygon BBO file"
        Symbol = $row.SourceSymbol
        CoreSymbol = $row.CoreSymbol
        Bid = $null
        Ask = $null
        Mid = $row.PriceUsed
        Price = $row.PriceUsed
        TimestampUtc = $row.Timestamp
        Window = "2025-12-16T19:15:00Z/2025-12-17T02:00:00Z"
        Hash = $row.SourceHash
        Scope = "SandboxPreviewSizingOnly"
        Classification = "sandbox/offline/operator-provided local file"
        AllowedForSandboxPreviewSizingOnly = $true
        ForbiddenForAccountingProduction = $true
    }
}

$MetadataEvidence = @()
foreach ($row in $MetadataRows | Where-Object { $_.Classification -match "READY" }) {
    $MetadataEvidence += [ordered]@{
        MetadataSource = "R007 local sandbox quantity rule inventory"
        SourceArtifact = "artifacts/readiness/execution-sandbox/phase-exec-sandbox-r007-quantity-rule-inventory.json"
        CoreSymbol = $row.CoreSymbol
        Symbol = $row.MetadataSymbolUsed
        ContractMultiplier = $row.ContractMultiplier
        MinOrderSize = $row.MinOrderSize
        QuotedCurrency = $row.QuotedCurrency
        SecurityId = $row.InstrumentId
        InstrumentId = $row.InstrumentId
        SecurityIdSource = $row.SecurityIdSource
        DirectInverseRelationToCoreSymbol = $row.DirectInverseRelationship
        SandboxPreviewAllowed = $true
        ProductionAccountingForbidden = $true
    }
}

$PriceValidationRows = @()
foreach ($row in $PriceRows) {
    $class = switch ($row.Classification) {
        "PRICE_BASIS_READY_DIRECT" { "PRICE_READY_DIRECT" }
        "PRICE_BASIS_READY_INVERSE" { "PRICE_READY_INVERSE" }
        "PRICE_BASIS_MISSING" { "PRICE_MISSING" }
        default { "PRICE_CONTRADICTORY" }
    }
    $PriceValidationRows += [ordered]@{
        CoreSymbol = $row.CoreSymbol
        Weight = (@($R004Universe.Symbols) | Where-Object { $_.CoreSymbol -eq $row.CoreSymbol } | Select-Object -First 1).Weight
        PriceSourceSymbol = $row.SourceSymbol
        DirectInverse = if ($row.InversionApplied) { "inverse" } else { "direct" }
        SourcePrice = if ($row.InversionApplied -and $row.PriceUsed) { [math]::Round(1 / [double]$row.PriceUsed, 12) } else { $row.PriceUsed }
        DerivedCorePrice = $row.PriceUsed
        InversionFormula = if ($row.InversionApplied) { "CorePrice = 1 / SourcePrice" } else { $null }
        TimestampUtc = $row.Timestamp
        SourceArtifact = $row.SourceArtifact
        SourceHash = $row.SourceHash
        Scope = "SandboxPreviewSizingOnly"
        PriceStatus = $class
        MissingReason = $row.MissingReason
    }
}

$MetadataValidationRows = @()
foreach ($row in $MetadataRows) {
    $class = switch ($row.Classification) {
        "METADATA_READY_DIRECT" { "METADATA_READY_DIRECT" }
        "METADATA_READY_INVERSE_OR_EXECUTION_PAIR" { "METADATA_READY_INVERSE_OR_EXECUTION_PAIR" }
        "METADATA_MISSING" { "METADATA_MISSING" }
        default { "METADATA_CONTRADICTORY" }
    }
    $MetadataValidationRows += [ordered]@{
        CoreSymbol = $row.CoreSymbol
        MetadataSourceSymbol = $row.MetadataSymbolUsed
        DirectInverse = $row.DirectInverseRelationship
        ContractMultiplier = $row.ContractMultiplier
        MinOrderSize = $row.MinOrderSize
        QuotedCurrency = $row.QuotedCurrency
        SecurityId = $row.InstrumentId
        InstrumentId = $row.InstrumentId
        SecurityIdSource = $row.SecurityIdSource
        MetadataStatus = $class
        MissingReason = $row.MissingReason
        LaterExecutionInversion = $row.ExecutionInversionNeededLater
    }
}

$PriceSourceArtifacts = @($PriceRows | Where-Object { $_.SourceArtifact } | ForEach-Object { $_.SourceArtifact } | Select-Object -Unique)
$PriceSourceHashes = @($PriceRows | Where-Object { $_.SourceHash } | ForEach-Object { $_.SourceHash } | Select-Object -Unique)
$MetadataSourceArtifacts = @($MetadataEvidence | ForEach-Object { $_.SourceArtifact } | Select-Object -Unique)
$MetadataSourceHashes = @()
foreach ($source in $MetadataSourceArtifacts) {
    $full = Join-Path $RepoRoot $source
    if (Test-Path -LiteralPath $full) { $MetadataSourceHashes += Get-Sha256 $full }
}

$OperatorTemplate = @()
foreach ($symbol in $AllMissing) {
    $u = $UniverseRows | Where-Object { $_.CoreSymbol -eq $symbol } | Select-Object -First 1
    $OperatorTemplate += [ordered]@{
        CoreSymbol = $symbol
        AcceptableDirectPriceSymbol = $u.PreferredDirectPriceSymbol
        AcceptableInversePriceSymbol = $u.PreferredInversePriceSymbol
        Price = $null
        Bid = $null
        Ask = $null
        Mid = $null
        TimestampUtc = $null
        SourceType = "operator-supplied-local-file-or-approved-local-snapshot"
        SourceArtifactPath = $null
        SourceArtifactHash = $null
        ContractMultiplier = $null
        MinOrderSize = $null
        QuotedCurrency = $null
        InstrumentId = $null
        SecurityId = $null
        SecurityIdSource = $null
        AllowedScope = "SandboxPreviewSizingOnly"
        NotAccounting = $true
        NotProduction = $true
    }
}

Write-JsonArtifact "r004-intake-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "r004-intake-validation"
    Classification = "R004_READY_FOR_FX_DATA_BASIS"
    R004SummaryExists = Test-Path -LiteralPath $R004Summary
    R004PriceCoverageExists = Test-Path -LiteralPath (Join-Path $R004Dir "price-basis-coverage-by-core-symbol.json")
    R004MetadataCoverageExists = Test-Path -LiteralPath (Join-Path $R004Dir "instrument-metadata-coverage-by-core-symbol.json")
    CoveredSymbolsExplicit = $PriceCovered
    MissingSymbolsExplicit = $AllMissing
    R004DidNotDeriveQuantities = $R004Candidate.QuantityFeasibilityStatus -eq "QUANTITY_FEASIBILITY_BLOCKED_PRICE_BASIS"
    R004DidNotAllowR009 = $R004Candidate.R009Ready -eq $false
    R004DidNotMutateDbOrLedger = ($R004Boundary.NoDbMutation -eq $true) -and ($R004Boundary.NoLedger -eq $true)
})

Write-JsonArtifact "core-fx-universe-basis.json" ([ordered]@{
    Package = $Package
    Artifact = "core-fx-universe-basis"
    Classification = "CORE_FX_UNIVERSE_BASIS_READY"
    Symbols = $UniverseRows
})

Write-JsonArtifact "local-fx-price-evidence-inventory.json" ([ordered]@{
    Package = $Package
    Artifact = "local-fx-price-evidence-inventory"
    Classification = "LOCAL_FX_PRICE_EVIDENCE_FOUND_PARTIAL"
    RequiredSourceSymbols = $SourcePriceSymbols
    SearchRoots = @("artifacts/readiness", "Core handoff package/readiness", "local MarketData artifacts", "data/offline-quotes/polygon/incoming", "local CSV/JSON/TXT price evidence")
    Evidence = $PriceEvidence
    SymbolsCovered = $PriceCovered
    SymbolsMissing = $PriceMissing
    InternetCalled = $false
    ExternalApiCalled = $false
    DbQueried = $false
})

Write-JsonArtifact "local-fx-metadata-inventory.json" ([ordered]@{
    Package = $Package
    Artifact = "local-fx-metadata-inventory"
    Classification = "LOCAL_FX_METADATA_FOUND_PARTIAL"
    RequiredSourceSymbols = $SourcePriceSymbols
    SearchRoots = @("LMAX-Instruments.csv if present", "local instrument metadata artifacts", "static mappings", "Core/Intraday mapping artifacts", "readiness evidence")
    Evidence = $MetadataEvidence
    SymbolsCovered = $MetadataCovered
    SymbolsMissing = $MetadataMissing
    ExternalSystemsCalled = $false
})

Write-JsonArtifact "core-symbol-price-basis-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "core-symbol-price-basis-validation"
    OverallClassification = "PRICE_BASIS_READY_PARTIAL"
    SymbolValidation = $PriceValidationRows
    SymbolsCovered = $PriceCovered
    SymbolsMissing = $PriceMissing
})

Write-JsonArtifact "core-symbol-metadata-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "core-symbol-metadata-validation"
    OverallClassification = "METADATA_READY_PARTIAL"
    SymbolValidation = $MetadataValidationRows
    SymbolsCovered = $MetadataCovered
    SymbolsMissing = $MetadataMissing
})

Write-JsonArtifact "core-fx-price-basis-manifest.json" ([ordered]@{
    Package = $Package
    Artifact = "core-fx-price-basis-manifest"
    Classification = "CORE_FX_PRICE_BASIS_MANIFEST_READY_PARTIAL"
    PriceBasisManifestId = "core-anubis-fx-price-basis-r005:" + $CoreManifestHash.Substring(7, 24)
    Scope = "SandboxPreviewSizingOnly"
    AppliesTo = "CoreAnubisIntradaySizing"
    CoreHandoffManifestHash = $CoreManifestHash
    NettedUsdWeightsHash = $CoreManifest.NettedUsdWeightsHash
    TargetNotionalAmount = $TargetNotional
    SourceArtifacts = $PriceSourceArtifacts
    SourceHashes = $PriceSourceHashes
    DirectPrices = @($PriceValidationRows | Where-Object { $_.PriceStatus -eq "PRICE_READY_DIRECT" })
    InversePrices = @($PriceValidationRows | Where-Object { $_.PriceStatus -eq "PRICE_READY_INVERSE" })
    DerivedCorePrices = @($PriceValidationRows | Where-Object { $_.PriceStatus -match "READY" } | ForEach-Object { [ordered]@{ CoreSymbol = $_.CoreSymbol; DerivedCorePrice = $_.DerivedCorePrice; TimestampUtc = $_.TimestampUtc } })
    InversionPolicy = "Inverse pricing requires explicit local source price; CorePrice = 1 / SourcePrice; no synthetic price source is allowed."
    SymbolsCovered = $PriceCovered
    SymbolsStillMissing = $PriceMissing
    Timestamps = @($PriceValidationRows | Where-Object { $_.TimestampUtc } | ForEach-Object { $_.TimestampUtc } | Select-Object -Unique)
    AllowedUses = @("SandboxPreviewSizingOnly")
    ForbiddenUses = @("AccountingPnl", "ProductionPnl", "LedgerCommit", "ProductionLive", "PnLMarkPolicy")
    NotProduction = $true
    NotAccounting = $true
    NotLedgerCommit = $true
})

Write-JsonArtifact "core-fx-metadata-manifest.json" ([ordered]@{
    Package = $Package
    Artifact = "core-fx-metadata-manifest"
    Classification = "CORE_FX_METADATA_MANIFEST_READY_PARTIAL"
    MetadataManifestId = "core-anubis-fx-metadata-r005:" + $CoreManifestHash.Substring(7, 24)
    Scope = "SandboxPreviewSizingOnly"
    AppliesTo = "CoreAnubisIntradaySizing"
    SourceArtifacts = $MetadataSourceArtifacts
    SourceHashes = $MetadataSourceHashes
    DirectMetadata = @($MetadataValidationRows | Where-Object { $_.MetadataStatus -eq "METADATA_READY_DIRECT" })
    InverseOrExecutionPairMetadata = @($MetadataValidationRows | Where-Object { $_.MetadataStatus -eq "METADATA_READY_INVERSE_OR_EXECUTION_PAIR" })
    SymbolsCovered = $MetadataCovered
    SymbolsStillMissing = $MetadataMissing
    AllowedUses = @("SandboxPreviewSizingOnly")
    ForbiddenUses = @("AccountingPnl", "ProductionPnl", "LedgerCommit", "ProductionLive", "R009Submission")
    NotProduction = $true
    NotAccounting = $true
    NotLedgerCommit = $true
})

Write-JsonArtifact "operator-fx-price-metadata-evidence-template.json" ([ordered]@{
    Package = $Package
    Artifact = "operator-fx-price-metadata-evidence-template"
    Classification = "OPERATOR_FX_EVIDENCE_TEMPLATE_CREATED_FOR_REMAINING_GAPS"
    MissingSymbols = $AllMissing
    TemplateRows = $OperatorTemplate
})

Write-JsonArtifact "quantity-readiness-decision.json" ([ordered]@{
    Package = $Package
    Artifact = "quantity-readiness-decision"
    Classification = "QUANTITY_DERIVATION_BLOCKED_PRICE_AND_METADATA_GAPS"
    DoNotDeriveQuantitiesInR005 = $true
    PriceBasisComplete = $PriceMissing.Count -eq 0
    MetadataComplete = $MetadataMissing.Count -eq 0
    MissingPriceSymbols = $PriceMissing
    MissingMetadataSymbols = $MetadataMissing
})

Write-JsonArtifact "future-package-decision.json" ([ordered]@{
    Package = $Package
    Artifact = "future-package-decision"
    Decision = "NEXT_CORE_ANUBIS_INTRADAY_FX_OPERATOR_EVIDENCE_IMPORT_R006"
    Reason = "R005 normalized the available local FX price and metadata evidence, but CNHUSD/MXNUSD/NOKUSD/SEKUSD/SGDUSD/ZARUSD still require explicit operator-supplied local price and metadata evidence."
})

Write-JsonArtifact "contract-status-update.json" ([ordered]@{
    Package = $Package
    Artifact = "contract-status-update"
    Statuses = @(
        [ordered]@{ ContractId = "core-anubis-marketdata-price-basis.v1"; Status = "PARTIAL_MANIFEST_READY" },
        [ordered]@{ ContractId = "core-anubis-instrument-metadata.v1"; Status = "PARTIAL_MANIFEST_READY" },
        [ordered]@{ ContractId = "core-anubis-quantity-readiness.v1"; Status = "BLOCKED_PRICE_AND_METADATA_GAPS" },
        [ordered]@{ ContractId = "pms-core-weights-candidate.v1"; Status = "WITH_WARNINGS_DATA_BASIS_PARTIAL" },
        [ordered]@{ ContractId = "pms-core-risk-review.v1"; Status = "BLOCKED" },
        [ordered]@{ ContractId = "pms-execution-candidate.v1"; Status = "BLOCKED" },
        [ordered]@{ ContractId = "r009-execution-readiness.v1"; Status = "BLOCKED_FOR_CORE_CANDIDATE" },
        [ordered]@{ ContractId = "pnl-preview.v1"; Status = "YES_ONLY_FOR_ACCEPTED_HISTORICAL_SANDBOX_GROSS_PNL_V0" },
        [ordered]@{ ContractId = "accounting-attribution.v1"; Status = "BLOCKED" },
        [ordered]@{ ContractId = "production-readiness.v1"; Status = "BLOCKED" }
    )
})

Write-JsonArtifact "readiness-impact.json" ([ordered]@{
    Package = $Package
    Artifact = "readiness-impact"
    Classification = "FX_DATA_BASIS_IMPROVED_GAPS_REMAIN_NO_EXECUTION_READINESS_CHANGE"
    FxDataBasisImproved = $true
    GapsRemain = $true
    NoQuantitiesDerived = $true
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
    NoExternalMarketDataCall = $true
    NoFreshPolygonMassiveCall = $true
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
    NoInventedQuantities = $true
    NoR010Transfer = $true
})

$Summary = @"
# $Package

Classification: CORE_ANUBIS_INTRADAY_FX_DATA_BASIS_R005_WITH_WARNINGS_PARTIAL_DATA_BASIS_TEMPLATE_CREATED

Did we pause sizing and clean up the FX data basis? yes.
Core symbols now with price evidence: AUDUSD, CADUSD, CHFUSD, EURUSD, GBPUSD, JPYUSD, NZDUSD.
Core symbols still missing price evidence: CNHUSD, MXNUSD, NOKUSD, SEKUSD, SGDUSD, ZARUSD.
Core symbols now with metadata: AUDUSD, CADUSD, CHFUSD, EURUSD, GBPUSD, JPYUSD, NZDUSD.
Core symbols still missing metadata: CNHUSD, MXNUSD, NOKUSD, SEKUSD, SGDUSD, ZARUSD.
Is quantity derivation ready next? no.
Was an operator evidence template created? yes.
Next package: NEXT_CORE_ANUBIS_INTRADAY_FX_OPERATOR_EVIDENCE_IMPORT_R006.
What did not run? Core execution, manager, Anubis, CUDA, Core netting, LMAX, external market data calls, Polygon/Massive, DB mutation, R009, orders, fills, reports, ledger, trading-state mutation, production-state mutation, quantity derivation, risk review, R010 transfer, and accounting/net/production/live readiness.
"@
$Summary | Set-Content -LiteralPath (Join-Path $ArtifactDir "summary.md") -Encoding UTF8

Write-Host "CORE_ANUBIS_INTRADAY_FX_DATA_BASIS_R005 artifacts written."
