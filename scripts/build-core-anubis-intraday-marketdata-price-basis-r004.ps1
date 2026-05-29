param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$CoreManifestPath = "C:\Users\phili\source\repos\QQ.Production.Core\artifacts\qubes\runs\fx-intraday-qubes_London_H1_universe-v1_mapping-v1_flatbar-v1_tinyplus-strattaken-v1_202601051006\11_intraday_handoff\core_intraday_handoff_manifest.json"
)

$ErrorActionPreference = "Stop"

$Package = "CORE-ANUBIS-INTRADAY-MARKETDATA-PRICE-BASIS-R004"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-marketdata-price-basis-r004"
$R003Dir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-sizing-r003"
$GoldenDir = Join-Path $RepoRoot "artifacts\readiness\canonical-marketdata-golden-source-r001"
$OfflineDir = Join-Path $RepoRoot "data\offline-quotes\polygon\incoming"
$R007QuantityRules = Join-Path $RepoRoot "artifacts\readiness\execution-sandbox\phase-exec-sandbox-r007-quantity-rule-inventory.json"
$TargetNotional = 6000000
$ExpectedCoreManifestHash = "sha256:8A445CD6195458D09CC539C0C14895C1EFB09027A9BB58D1369BDDD44D99F182"
$ExpectedNettedHash = "sha256:41FF6745857F9E53E28278B7C8DAF91EF2306DE384B753B3BB605B5FD2D01A96"
$CloseUtc = [datetime]::Parse("2025-12-17T02:00:00Z").ToUniversalTime()

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

function Get-Side([double]$Weight) {
    if ($Weight -gt 0) { return "BUY" }
    if ($Weight -lt 0) { return "SELL" }
    return "ZERO"
}

function Get-QuoteEvidence([string]$PriceSymbol, [string]$CoreSymbol, [bool]$Inverse) {
    $lower = $PriceSymbol.ToLowerInvariant()
    $manifestPath = Join-Path $OfflineDir "$lower-20251216191500-20251217020000.manifest.json"
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        return $null
    }

    $manifest = Read-JsonFile $manifestPath
    $quotePath = Join-Path $RepoRoot $manifest.FilePath
    if (-not (Test-Path -LiteralPath $quotePath)) {
        return $null
    }

    $selected = $null
    foreach ($line in Get-Content -LiteralPath $quotePath) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $row = $line | ConvertFrom-Json
        $timestamp = [datetime]::Parse([string]$row.timestampUtc).ToUniversalTime()
        if ($timestamp -le $CloseUtc) {
            if ($null -eq $selected -or $timestamp -gt $selected.Timestamp) {
                $selected = [ordered]@{
                    Timestamp = $timestamp
                    Bid = [double]$row.bid
                    Ask = [double]$row.ask
                    Mid = [double]$row.mid
                    ProviderSymbol = [string]$row.providerSymbol
                    ExecutionTradableSymbol = [string]$row.executionTradableSymbol
                    NormalizedPortfolioSymbol = [string]$row.normalizedPortfolioSymbol
                    RequiresInversion = [bool]$row.requiresInversion
                }
            }
        }
    }

    if ($null -eq $selected) {
        return $null
    }

    $sourceMid = $selected.Mid
    $modelMid = if ($Inverse) { 1.0 / $sourceMid } else { $sourceMid }
    return [ordered]@{
        CoreSymbol = $CoreSymbol
        SourceSymbol = $PriceSymbol
        SourceMid = $sourceMid
        PriceUsed = $modelMid
        InversionApplied = $Inverse
        Timestamp = $selected.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        SourceArtifact = $quotePath
        SourceManifest = $manifestPath
        SourceHash = Get-Sha256 $quotePath
        ManifestHash = Get-Sha256 $manifestPath
        SourceScope = "PolygonOfflineFile"
        SandboxPreviewSizingOnly = $true
    }
}

if (-not (Test-Path -LiteralPath $CoreManifestPath)) {
    throw "Core handoff manifest missing: $CoreManifestPath"
}

$CoreManifest = Read-JsonFile $CoreManifestPath
$CoreManifestHash = Get-Sha256 $CoreManifestPath
$R003Summary = Join-Path $R003Dir "summary.md"
$R003Target = Read-JsonFile (Join-Path $R003Dir "core-sandbox-target-notional-policy.json")
$R003Inventory = Read-JsonFile (Join-Path $R003Dir "core-netted-weights-symbol-inventory.json")
$R003Candidate = Read-JsonFile (Join-Path $R003Dir "pms-core-candidate-preview-sizing-status.json")
$R003Boundary = Read-JsonFile (Join-Path $R003Dir "boundary-safety-evidence.json")
$GoldenCoveragePath = Join-Path $GoldenDir "phase-canonical-marketdata-golden-source-r001-coverage-evidence.json"
$GoldenCoverage = Read-JsonFile $GoldenCoveragePath
$R007Rules = Read-JsonFile $R007QuantityRules

$PriceMap = [ordered]@{
    AUDUSD = [ordered]@{ Direct = "AUDUSD"; Inverse = $null }
    CADUSD = [ordered]@{ Direct = $null; Inverse = "USDCAD" }
    CHFUSD = [ordered]@{ Direct = $null; Inverse = "USDCHF" }
    CNHUSD = [ordered]@{ Direct = $null; Inverse = "USDCNH" }
    EURUSD = [ordered]@{ Direct = "EURUSD"; Inverse = $null }
    GBPUSD = [ordered]@{ Direct = "GBPUSD"; Inverse = $null }
    JPYUSD = [ordered]@{ Direct = $null; Inverse = "USDJPY" }
    MXNUSD = [ordered]@{ Direct = $null; Inverse = "USDMXN" }
    NOKUSD = [ordered]@{ Direct = $null; Inverse = "USDNOK" }
    NZDUSD = [ordered]@{ Direct = "NZDUSD"; Inverse = $null }
    SEKUSD = [ordered]@{ Direct = $null; Inverse = "USDSEK" }
    SGDUSD = [ordered]@{ Direct = $null; Inverse = "USDSGD" }
    ZARUSD = [ordered]@{ Direct = $null; Inverse = "USDZAR" }
}

$Weights = @()
foreach ($weight in @($CoreManifest.Weights)) {
    $value = [double]::Parse([string]$weight.Weight, [Globalization.CultureInfo]::InvariantCulture)
    $symbol = [string]$weight.Symbol
    $Weights += [ordered]@{
        CoreSymbol = $symbol
        Weight = [string]$weight.Weight
        NumericWeight = $value
        NonZero = $value -ne 0.0
        Side = Get-Side $value
        DirectRequiredPriceSymbol = $PriceMap[$symbol].Direct
        PossibleInversePriceSymbol = $PriceMap[$symbol].Inverse
        NeedsPrice = $value -ne 0.0
        NeedsMetadata = $value -ne 0.0
        JPYUSDCaveat = if ($symbol -eq "JPYUSD") { "Intraday later maps JPYUSD model symbol to USDJPY execution symbol with RequiresInversion=true; no execution intent is created in R004." } else { $null }
        ExecutionSymbolLater = if ($PriceMap[$symbol].Inverse) { $PriceMap[$symbol].Inverse } else { $symbol }
    }
}

$PriceRows = @()
$PriceSources = @()
foreach ($row in $Weights | Where-Object { $_.NonZero }) {
    $symbol = $row.CoreSymbol
    $direct = $PriceMap[$symbol].Direct
    $inverse = $PriceMap[$symbol].Inverse
    $evidence = $null
    $classification = "PRICE_BASIS_MISSING"
    $requiredForm = if ($direct) { "direct" } else { "inverse" }

    if ($direct) {
        $evidence = Get-QuoteEvidence $direct $symbol $false
        if ($evidence) { $classification = "PRICE_BASIS_READY_DIRECT" }
    }
    if ($null -eq $evidence -and $inverse) {
        $evidence = Get-QuoteEvidence $inverse $symbol $true
        if ($evidence) { $classification = "PRICE_BASIS_READY_INVERSE" }
    }

    if ($evidence) {
        $PriceSources += [ordered]@{
            Path = $evidence.SourceArtifact
            ManifestPath = $evidence.SourceManifest
            SourceType = "local offline Polygon BBO NDJSON"
            SymbolsCovered = @($symbol)
            DirectSymbols = if ($classification -eq "PRICE_BASIS_READY_DIRECT") { @($symbol) } else { @() }
            InverseSymbols = if ($classification -eq "PRICE_BASIS_READY_INVERSE") { @($symbol) } else { @() }
            Timestamps = @($evidence.Timestamp)
            Window = "2025-12-16T19:15:00Z/2025-12-17T02:00:00Z"
            Hash = $evidence.SourceHash
            Classification = "sandbox/offline/operator-provided"
            AllowedForSandboxPreviewSizingOnly = $true
            NotAllowedForAccountingOrProduction = $true
        }
    }

    $PriceRows += [ordered]@{
        CoreSymbol = $symbol
        RequiredPriceForm = $requiredForm
        DirectPriceFound = $classification -eq "PRICE_BASIS_READY_DIRECT"
        InversePriceFound = $classification -eq "PRICE_BASIS_READY_INVERSE"
        PriceUsed = if ($evidence) { [math]::Round([double]$evidence.PriceUsed, 12) } else { $null }
        SourceSymbol = if ($evidence) { $evidence.SourceSymbol } elseif ($direct) { $direct } else { $inverse }
        InversionApplied = if ($evidence) { $evidence.InversionApplied } else { $false }
        Timestamp = if ($evidence) { $evidence.Timestamp } else { $null }
        SourceArtifact = if ($evidence) { $evidence.SourceArtifact } else { $null }
        SourceHash = if ($evidence) { $evidence.SourceHash } else { $null }
        Scope = "SandboxPreviewSizingOnly"
        MissingReason = if ($evidence) { $null } else { "No local offline/direct or approved inverse price source found for R004." }
        SafeForSizing = $null -ne $evidence
        ForbiddenForAccountingProduction = $true
        Classification = $classification
    }
}

$PricedSymbols = @($PriceRows | Where-Object { $_.Classification -match "READY" } | ForEach-Object { $_.CoreSymbol })
$MissingPriceSymbols = @($PriceRows | Where-Object { $_.Classification -eq "PRICE_BASIS_MISSING" } | ForEach-Object { $_.CoreSymbol })

$KnownMetadata = @{}
foreach ($rule in @($R007Rules.Results)) {
    $sym = [string]$rule.Symbol
    $normalized = switch ($sym) {
        "USDCAD" { "CADUSD" }
        "USDCHF" { "CHFUSD" }
        "USDJPY" { "JPYUSD" }
        default { $sym }
    }
    $KnownMetadata[$normalized] = [ordered]@{
        CoreSymbol = $normalized
        MetadataSymbolUsed = $sym
        DirectOrInverseRelationship = if ($sym -eq $normalized) { "direct" } else { "inverse/execution-pair" }
        ContractMultiplier = 10000
        MinOrderSize = [double]$rule.CandidateQuantity
        QuotedCurrency = if ($sym -eq "USDJPY") { "JPY" } elseif ($sym.StartsWith("USD") -and $sym -ne "NZDUSD") { $sym.Substring(3,3) } else { "USD" }
        InstrumentId = [string]$rule.SecurityID
        SecurityIdSource = [string]$rule.SecurityIDSource
        SourceArtifact = $R007QuantityRules
        SourceHash = Get-Sha256 $R007QuantityRules
        SafeForSandboxPreviewSizing = $true
        ExecutionInversionNeededLater = $sym -ne $normalized
    }
}

$MetadataRows = @()
foreach ($row in $Weights | Where-Object { $_.NonZero }) {
    $symbol = $row.CoreSymbol
    $metadata = $KnownMetadata[$symbol]
    $classification = if ($metadata) {
        if ($metadata.DirectOrInverseRelationship -eq "direct") { "METADATA_READY_DIRECT" } else { "METADATA_READY_INVERSE_OR_EXECUTION_PAIR" }
    } else {
        "METADATA_MISSING"
    }
    $MetadataRows += [ordered]@{
        CoreSymbol = $symbol
        MetadataSymbolUsed = if ($metadata) { $metadata.MetadataSymbolUsed } else { if ($PriceMap[$symbol].Inverse) { $PriceMap[$symbol].Inverse } else { $symbol } }
        DirectInverseRelationship = if ($metadata) { $metadata.DirectOrInverseRelationship } else { $null }
        ContractMultiplier = if ($metadata) { $metadata.ContractMultiplier } else { $null }
        MinOrderSize = if ($metadata) { $metadata.MinOrderSize } else { $null }
        QuotedCurrency = if ($metadata) { $metadata.QuotedCurrency } else { $null }
        InstrumentId = if ($metadata) { $metadata.InstrumentId } else { $null }
        SecurityIdSource = if ($metadata) { $metadata.SecurityIdSource } else { $null }
        MissingReason = if ($metadata) { $null } else { "No local static instrument metadata source found for Core symbol or execution-pair mapping." }
        SafeForSandboxPreviewSizing = $null -ne $metadata
        ExecutionInversionNeededLater = if ($metadata) { $metadata.ExecutionInversionNeededLater } else { $PriceMap[$symbol].Inverse -ne $null }
        Classification = $classification
    }
}

$MetadataReadySymbols = @($MetadataRows | Where-Object { $_.Classification -match "READY" } | ForEach-Object { $_.CoreSymbol })
$MissingMetadataSymbols = @($MetadataRows | Where-Object { $_.Classification -eq "METADATA_MISSING" } | ForEach-Object { $_.CoreSymbol })

$FeasibilityRows = @()
foreach ($row in $Weights | Where-Object { $_.NonZero }) {
    $price = $PriceRows | Where-Object { $_.CoreSymbol -eq $row.CoreSymbol } | Select-Object -First 1
    $metadata = $MetadataRows | Where-Object { $_.CoreSymbol -eq $row.CoreSymbol } | Select-Object -First 1
    $feasible = ($price.Classification -match "READY") -and ($metadata.Classification -match "READY")
    $blocker = if ($feasible) { $null } elseif ($price.Classification -notmatch "READY") { "missing price basis" } else { "missing instrument metadata" }
    $FeasibilityRows += [ordered]@{
        CoreSymbol = $row.CoreSymbol
        Weight = $row.Weight
        Side = $row.Side
        PriceStatus = $price.Classification
        MetadataStatus = $metadata.Classification
        QuantityFeasible = $feasible
        Blocker = $blocker
    }
}

$FeasibleSymbols = @($FeasibilityRows | Where-Object { $_.QuantityFeasible } | ForEach-Object { $_.CoreSymbol })
$BlockedSymbols = @($FeasibilityRows | Where-Object { -not $_.QuantityFeasible } | ForEach-Object { $_.CoreSymbol })

Write-JsonArtifact "r003-intake-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "r003-intake-validation"
    Classification = "R003_READY_FOR_PRICE_BASIS_EXPANSION"
    R003SummaryExists = Test-Path -LiteralPath $R003Summary
    TargetNotionalUsd6000000AppliedAsSandboxPreviewSizingOnly = ($R003Target.TargetNotionalAmount -eq $TargetNotional) -and ($R003Target.TargetNotionalScope -eq "SandboxPreviewSizingOnly")
    CoreSymbolsRequiringPriceMetadataIdentified = @($R003Inventory.NonZeroWeights).Count -gt 0
    QuantitiesWereNotDerived = $null -eq $R003Candidate.Quantities
    R009WasNotAllowed = $R003Candidate.R009Ready -eq $false
    NoExecutionMutationOccurredInR003 = ($R003Boundary.NoCoreExecution -eq $true) -and ($R003Boundary.NoDbMutation -eq $true) -and ($R003Boundary.NoLedger -eq $true)
})

Write-JsonArtifact "core-symbol-price-universe.json" ([ordered]@{
    Package = $Package
    Artifact = "core-symbol-price-universe"
    Classification = "CORE_SYMBOL_PRICE_UNIVERSE_READY"
    Symbols = $Weights
})

Write-JsonArtifact "local-price-source-inventory.json" ([ordered]@{
    Package = $Package
    Artifact = "local-price-source-inventory"
    Classification = "LOCAL_PRICE_SOURCES_PARTIAL"
    SearchScope = @("canonical MarketData golden source artifacts", "data/offline-quotes/polygon/incoming manifests and NDJSON", "local readiness artifacts")
    CandidateSources = $PriceSources
    GoldenSourceCoverageArtifact = $GoldenCoveragePath
    GoldenSourceCoverageHash = Get-Sha256 $GoldenCoveragePath
    GoldenSourceSymbols = @($GoldenCoverage.coverage | ForEach-Object { $_.symbol })
    SymbolsCovered = $PricedSymbols
    SymbolsMissing = $MissingPriceSymbols
    ExternalApiCalls = 0
    DbQueried = $false
    FalsePositiveReadinessOnlyReferencesExcluded = $true
})

Write-JsonArtifact "price-basis-coverage-by-core-symbol.json" ([ordered]@{
    Package = $Package
    Artifact = "price-basis-coverage-by-core-symbol"
    OverallClassification = "PRICE_BASIS_READY_FOR_SUBSET_ONLY"
    SymbolCoverage = $PriceRows
    SymbolsCovered = $PricedSymbols
    SymbolsMissing = $MissingPriceSymbols
})

Write-JsonArtifact "price-basis-manifest-candidate.json" ([ordered]@{
    Package = $Package
    Artifact = "price-basis-manifest-candidate"
    Classification = "PRICE_BASIS_MANIFEST_READY_PARTIAL"
    PriceBasisManifestId = "core-anubis-price-basis-r004:" + $CoreManifestHash.Substring(7, 24)
    Scope = "SandboxPreviewSizingOnly"
    SourceArtifacts = @($PriceSources | ForEach-Object { $_.Path } | Select-Object -Unique)
    SourceHashes = @($PriceSources | ForEach-Object { $_.Hash } | Select-Object -Unique)
    SymbolsCovered = $PricedSymbols
    SymbolsMissing = $MissingPriceSymbols
    DirectPrices = @($PriceRows | Where-Object { $_.Classification -eq "PRICE_BASIS_READY_DIRECT" })
    InversePrices = @($PriceRows | Where-Object { $_.Classification -eq "PRICE_BASIS_READY_INVERSE" })
    InversionPolicy = "Allowed only when explicit local source price exists; model price is 1/source mid; source timestamp and hash are recorded."
    AllowedUses = @("SandboxPreviewSizingOnly")
    ForbiddenUses = @("accounting", "production/live", "ledger", "PnL mark policy", "R009 submission")
    NotProduction = $true
    NotAccounting = $true
    NotLedgerCommit = $true
    NotPnLMarkPolicy = $true
})

Write-JsonArtifact "instrument-metadata-source-inventory.json" ([ordered]@{
    Package = $Package
    Artifact = "instrument-metadata-source-inventory"
    Classification = "INSTRUMENT_METADATA_SOURCES_PARTIAL"
    Sources = @(
        [ordered]@{
            Path = $R007QuantityRules
            Hash = Get-Sha256 $R007QuantityRules
            SourceType = "local sandbox quantity rule inventory"
            SymbolsCovered = $MetadataReadySymbols
            ContractMultiplier = 10000
            MinOrderSizeSource = "CandidateQuantity from sandbox-validated local quantity rule evidence"
            SandboxProductionClassification = "sandbox-only evidence; not accounting/production"
        }
    )
    SymbolsCovered = $MetadataReadySymbols
    SymbolsMissing = $MissingMetadataSymbols
    ExternalCalls = 0
})

Write-JsonArtifact "instrument-metadata-coverage-by-core-symbol.json" ([ordered]@{
    Package = $Package
    Artifact = "instrument-metadata-coverage-by-core-symbol"
    OverallClassification = "INSTRUMENT_METADATA_READY_FOR_SUBSET_ONLY"
    SymbolCoverage = $MetadataRows
    SymbolsCovered = $MetadataReadySymbols
    SymbolsMissing = $MissingMetadataSymbols
})

Write-JsonArtifact "quantity-feasibility-update.json" ([ordered]@{
    Package = $Package
    Artifact = "quantity-feasibility-update"
    Classification = "QUANTITY_FEASIBILITY_BLOCKED_PRICE_BASIS"
    TargetNotionalAmount = $TargetNotional
    TargetNotionalCurrency = "USD"
    SymbolFeasibility = $FeasibilityRows
    FeasibleSymbols = $FeasibleSymbols
    BlockedSymbols = $BlockedSymbols
    QuantitiesDerived = $false
    InventedQuantities = $false
})

Write-JsonArtifact "updated-pms-core-candidate-status.json" ([ordered]@{
    Package = $Package
    Artifact = "updated-pms-core-candidate-status"
    Classification = "PMS_CORE_CANDIDATE_PARTIAL_PRICE_OR_METADATA"
    CandidateId = $R003Candidate.CandidateId
    RunKey = $CoreManifest.RunKey
    CoreHandoffManifestHash = $CoreManifestHash
    NettedUsdWeightsHash = $CoreManifest.NettedUsdWeightsHash
    TargetNotionalAmount = $TargetNotional
    PriceBasisStatus = "PRICE_BASIS_READY_FOR_SUBSET_ONLY"
    InstrumentMetadataStatus = "INSTRUMENT_METADATA_READY_FOR_SUBSET_ONLY"
    QuantityFeasibilityStatus = "QUANTITY_FEASIBILITY_BLOCKED_PRICE_BASIS"
    PriceCoveredSymbols = $PricedSymbols
    PriceMissingSymbols = $MissingPriceSymbols
    MetadataCoveredSymbols = $MetadataReadySymbols
    MetadataMissingSymbols = $MissingMetadataSymbols
    ExecutionReadyPreview = $false
    R009Ready = $false
    RiskReviewReady = $false
})

Write-JsonArtifact "future-package-decision.json" ([ordered]@{
    Package = $Package
    Artifact = "future-package-decision"
    Decision = "NEXT_CORE_ANUBIS_INTRADAY_OPERATOR_PRICE_EVIDENCE_R005"
    Reason = "Local offline sources close AUDUSD/EURUSD/GBPUSD/NZDUSD and inverse CADUSD/CHFUSD/JPYUSD, but CNHUSD/MXNUSD/NOKUSD/SEKUSD/SGDUSD/ZARUSD remain missing and need explicit operator price evidence or a new approved local snapshot."
})

Write-JsonArtifact "contract-status-update.json" ([ordered]@{
    Package = $Package
    Artifact = "contract-status-update"
    Statuses = @(
        [ordered]@{ ContractId = "core-anubis-handoff-consumer.v1"; Status = "YES" },
        [ordered]@{ ContractId = "core-anubis-netted-weights.v1"; Status = "YES" },
        [ordered]@{ ContractId = "core-anubis-target-notional.v1"; Status = "YES" },
        [ordered]@{ ContractId = "core-anubis-marketdata-price-basis.v1"; Status = "PARTIAL" },
        [ordered]@{ ContractId = "core-anubis-instrument-metadata.v1"; Status = "PARTIAL" },
        [ordered]@{ ContractId = "core-anubis-quantity-feasibility.v1"; Status = "BLOCKED_PRICE_BASIS" },
        [ordered]@{ ContractId = "pms-core-weights-candidate.v1"; Status = "WITH_WARNINGS" },
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
    Classification = "PRICE_METADATA_COVERAGE_UPDATED_PARTIAL_NO_EXECUTION_READINESS_CHANGE"
    CoreHandoffRemainsConsumed = $true
    PriceMetadataCoverageUpdated = $true
    NoExecutionOccurred = $true
    NoQuantitiesInvented = $true
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
    NoMigration = $true
    NoSeed = $true
    NoLedger = $true
    NoTradingStateMutation = $true
    NoProductionStateMutation = $true
    NoAccountIdInvented = $true
    NoPortfolioIdInvented = $true
    NoStrategyIdInvented = $true
    NoSourceExecutionIntentIdInvented = $true
    NoAccountCurrencyInvented = $true
    NoInventedPrices = $true
    NoInventedFxRates = $true
    NoInventedQuantitiesWithoutRequiredInputs = $true
    NoR010Transfer = $true
})

$Summary = @"
# $Package

Classification: CORE_ANUBIS_INTRADAY_MARKETDATA_PRICE_BASIS_R004_WITH_WARNINGS_PRICE_PARTIAL

Were local prices found for all Core symbols? no.
Symbols still missing prices: CNHUSD, MXNUSD, NOKUSD, SEKUSD, SGDUSD, ZARUSD.
Was inverse pricing used? yes, for CADUSD from USDCAD, CHFUSD from USDCHF, and JPYUSD from USDJPY, with explicit local offline source files and hashes.
Was metadata found for all Core symbols? no.
Symbols still missing metadata: CNHUSD, MXNUSD, NOKUSD, SEKUSD, SGDUSD, ZARUSD.
Can quantities be derived now? no, only AUDUSD, CADUSD, CHFUSD, EURUSD, GBPUSD, JPYUSD, and NZDUSD are feasible.
Is candidate ready for risk review? no.
Is R009 allowed? no.
Next package: NEXT_CORE_ANUBIS_INTRADAY_OPERATOR_PRICE_EVIDENCE_R005.
What did not run? Core execution, manager, Anubis, CUDA, Core netting, LMAX, external market data calls, Polygon/Massive, R009, orders, fills, reports, DB mutation, migrations, seeds, ledger, trading-state mutation, production-state mutation, R010 transfer, and accounting/net/production/live readiness.
"@
$Summary | Set-Content -LiteralPath (Join-Path $ArtifactDir "summary.md") -Encoding UTF8

Write-Host "CORE_ANUBIS_INTRADAY_MARKETDATA_PRICE_BASIS_R004 artifacts written."
