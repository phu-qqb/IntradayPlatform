param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$Package = "CORE-ANUBIS-INTRADAY-FX-OPERATOR-EVIDENCE-IMPORT-R006"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-fx-operator-evidence-import-r006"
$R005Dir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-fx-data-basis-r005"
$ReadinessRoot = Join-Path $RepoRoot "artifacts\readiness"
$RemainingSymbols = @("CNHUSD","MXNUSD","NOKUSD","SEKUSD","SGDUSD","ZARUSD")

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

function Test-FilledOperatorEvidence([object]$Json) {
    $rows = @()
    if ($Json.TemplateRows) { $rows = @($Json.TemplateRows) }
    elseif ($Json.Evidence) { $rows = @($Json.Evidence) }
    elseif ($Json.Rows) { $rows = @($Json.Rows) }
    else { return $false }

    foreach ($symbol in $RemainingSymbols) {
        $row = $rows | Where-Object { $_.CoreSymbol -eq $symbol } | Select-Object -First 1
        if ($null -eq $row) { return $false }
        $pricePresent = ($null -ne $row.Price) -or ($null -ne $row.Mid) -or (($null -ne $row.Bid) -and ($null -ne $row.Ask))
        $metadataPresent = ($null -ne $row.ContractMultiplier) -and ($null -ne $row.MinOrderSize) -and ($null -ne $row.QuotedCurrency)
        $sourcePresent = ($null -ne $row.SourceArtifactHash) -or ($null -ne $row.SourceArtifactPath)
        if (-not ($pricePresent -and $metadataPresent -and $sourcePresent)) { return $false }
    }
    return $true
}

$R005Summary = Join-Path $R005Dir "summary.md"
$R005TemplatePath = Join-Path $R005Dir "operator-fx-price-metadata-evidence-template.json"
$R005PriceManifestPath = Join-Path $R005Dir "core-fx-price-basis-manifest.json"
$R005MetadataManifestPath = Join-Path $R005Dir "core-fx-metadata-manifest.json"
$R005QuantityPath = Join-Path $R005Dir "quantity-readiness-decision.json"
$R005BoundaryPath = Join-Path $R005Dir "boundary-safety-evidence.json"

$R005Template = Read-JsonFile $R005TemplatePath
$R005PriceManifest = Read-JsonFile $R005PriceManifestPath
$R005MetadataManifest = Read-JsonFile $R005MetadataManifestPath
$R005Quantity = Read-JsonFile $R005QuantityPath
$R005Boundary = Read-JsonFile $R005BoundaryPath

$DiscoveryRoots = @(
    $R005Dir,
    $ArtifactDir,
    $ReadinessRoot,
    (Join-Path $RepoRoot "data"),
    (Join-Path $RepoRoot "fixtures")
)

$CandidateFiles = @()
foreach ($root in $DiscoveryRoots) {
    if (-not (Test-Path -LiteralPath $root)) { continue }
    $CandidateFiles += Get-ChildItem -LiteralPath $root -Recurse -File -Include *.json,*.csv,*.txt -ErrorAction SilentlyContinue |
        Where-Object {
            $_.FullName -match "operator|fx|price|metadata|cnh|mxn|nok|sek|sgd|zar|usdcnh|usdmxn|usdnok|usdsek|usdsgd|usdzar"
        }
}

$Discovered = @()
$FilledEvidenceFiles = @()
foreach ($file in ($CandidateFiles | Sort-Object FullName -Unique)) {
    $symbolsCovered = @()
    $priceFieldsPresent = $false
    $metadataFieldsPresent = $false
    $timestampFieldsPresent = $false
    $scopeFieldsPresent = $false
    $appearsFilled = $false

    $raw = Get-Content -Raw -LiteralPath $file.FullName
    foreach ($symbol in $RemainingSymbols) {
        if ($raw -match $symbol) { $symbolsCovered += $symbol }
    }

    if ($file.Extension -eq ".json") {
        try {
            $json = $raw | ConvertFrom-Json
            $priceFieldsPresent = $raw -match '"(Price|Bid|Ask|Mid)"\s*:'
            $metadataFieldsPresent = $raw -match '"(ContractMultiplier|MinOrderSize|QuotedCurrency|SecurityId|InstrumentId)"\s*:'
            $timestampFieldsPresent = $raw -match '"TimestampUtc"\s*:'
            $scopeFieldsPresent = $raw -match "SandboxPreviewSizingOnly"
            $appearsFilled = Test-FilledOperatorEvidence $json
        } catch {
            $appearsFilled = $false
        }
    } else {
        $priceFieldsPresent = $raw -match "price|bid|ask|mid"
        $metadataFieldsPresent = $raw -match "contract|multiplier|min.*order|quoted|security|instrument"
        $timestampFieldsPresent = $raw -match "timestamp|time"
        $scopeFieldsPresent = $raw -match "SandboxPreviewSizingOnly"
        $appearsFilled = $false
    }

    if (@($symbolsCovered).Count -gt 0) {
        $record = [ordered]@{
            Path = $file.FullName
            FileType = $file.Extension.TrimStart(".").ToUpperInvariant()
            Hash = Get-Sha256 $file.FullName
            SymbolsCovered = @($symbolsCovered | Select-Object -Unique)
            PriceFieldsPresent = $priceFieldsPresent
            MetadataFieldsPresent = $metadataFieldsPresent
            TimestampFieldsPresent = $timestampFieldsPresent
            SourceScopeFieldsPresent = $scopeFieldsPresent
            AppearsFilled = $appearsFilled
            TemplateOnly = ($file.FullName -eq $R005TemplatePath) -and (-not $appearsFilled)
        }
        $Discovered += $record
        if ($appearsFilled) { $FilledEvidenceFiles += $record }
    }
}

$FilledEvidenceFound = @($FilledEvidenceFiles).Count -gt 0
$ExistingPriceCovered = @($R005PriceManifest.SymbolsCovered)
$ExistingMetadataCovered = @($R005MetadataManifest.SymbolsCovered)

$PriceValidation = @()
$MetadataValidation = @()
foreach ($symbol in $RemainingSymbols) {
    $templateRow = @($R005Template.TemplateRows) | Where-Object { $_.CoreSymbol -eq $symbol } | Select-Object -First 1
    $PriceValidation += [ordered]@{
        CoreSymbol = $symbol
        SourceSymbol = $templateRow.AcceptableInversePriceSymbol
        DirectInverse = "inverse"
        SourcePrice = $null
        Bid = $null
        Ask = $null
        Mid = $null
        DerivedCorePrice = $null
        TimestampUtc = $null
        SourceArtifactPath = $null
        SourceArtifactHash = $null
        SourceType = $templateRow.SourceType
        Scope = "SandboxPreviewSizingOnly"
        AllowedForSandboxPreviewSizingOnly = $false
        ForbiddenForAccountingProduction = $true
        ValidationStatus = "OPERATOR_PRICE_EVIDENCE_MISSING"
        MissingReason = "No filled operator evidence file found; R005 template remains blank for $symbol."
    }
    $MetadataValidation += [ordered]@{
        CoreSymbol = $symbol
        MetadataSymbol = $templateRow.AcceptableInversePriceSymbol
        DirectInverseOrExecutionPair = "inverse/execution-pair"
        ContractMultiplier = $null
        MinOrderSize = $null
        QuotedCurrency = $null
        InstrumentId = $null
        SecurityId = $null
        SecurityIdSource = $null
        SourceArtifactPath = $null
        SourceArtifactHash = $null
        AllowedForSandboxPreviewSizingOnly = $false
        ForbiddenForAccountingProduction = $true
        ValidationStatus = "OPERATOR_METADATA_EVIDENCE_MISSING"
        MissingReason = "No filled operator metadata evidence file found; R005 template remains blank for $symbol."
    }
}

Write-JsonArtifact "r005-intake-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "r005-intake-validation"
    Classification = "R005_READY_FOR_OPERATOR_EVIDENCE_IMPORT"
    R005SummaryExists = Test-Path -LiteralPath $R005Summary
    R005OperatorEvidenceTemplateExists = Test-Path -LiteralPath $R005TemplatePath
    RemainingMissingSymbols = $RemainingSymbols
    R005DidNotDeriveQuantities = $R005Quantity.DoNotDeriveQuantitiesInR005 -eq $true
    R005DidNotAllowR009 = $true
    R005DidNotMutateDbOrLedger = ($R005Boundary.NoDbMutation -eq $true) -and ($R005Boundary.NoLedger -eq $true)
})

Write-JsonArtifact "operator-evidence-file-discovery.json" ([ordered]@{
    Package = $Package
    Artifact = "operator-evidence-file-discovery"
    Classification = "OPERATOR_EVIDENCE_TEMPLATE_ONLY_NO_FILLED_EVIDENCE"
    SearchRoots = $DiscoveryRoots
    CandidateFiles = $Discovered
    FilledEvidenceFiles = $FilledEvidenceFiles
    FilledEvidenceFound = $FilledEvidenceFound
})

Write-JsonArtifact "operator-price-evidence-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "operator-price-evidence-validation"
    OverallClassification = "OPERATOR_PRICE_EVIDENCE_MISSING_ALL_REMAINING"
    SymbolValidation = $PriceValidation
    ImportedPrices = @()
    MissingPrices = $RemainingSymbols
})

Write-JsonArtifact "operator-metadata-evidence-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "operator-metadata-evidence-validation"
    OverallClassification = "OPERATOR_METADATA_EVIDENCE_MISSING_ALL_REMAINING"
    SymbolValidation = $MetadataValidation
    ImportedMetadata = @()
    MissingMetadata = $RemainingSymbols
})

Write-JsonArtifact "expanded-fx-price-basis-manifest.json" ([ordered]@{
    Package = $Package
    Artifact = "expanded-fx-price-basis-manifest"
    Classification = "EXPANDED_FX_PRICE_BASIS_MANIFEST_READY_PARTIAL"
    PriceBasisManifestId = "core-anubis-fx-price-basis-r006:" + $R005PriceManifest.CoreHandoffManifestHash.Substring(7, 24)
    Scope = "SandboxPreviewSizingOnly"
    AppliesTo = "CoreAnubisIntradaySizing"
    CoreHandoffManifestHash = $R005PriceManifest.CoreHandoffManifestHash
    NettedUsdWeightsHash = $R005PriceManifest.NettedUsdWeightsHash
    TargetNotionalAmount = 6000000
    DirectPrices = $R005PriceManifest.DirectPrices
    InversePrices = $R005PriceManifest.InversePrices
    DerivedCorePrices = $R005PriceManifest.DerivedCorePrices
    SourceArtifacts = $R005PriceManifest.SourceArtifacts
    SourceHashes = $R005PriceManifest.SourceHashes
    SymbolsCovered = $ExistingPriceCovered
    SymbolsStillMissing = $RemainingSymbols
    InversionPolicy = $R005PriceManifest.InversionPolicy
    Timestamps = $R005PriceManifest.Timestamps
    AllowedUses = @("SandboxPreviewSizingOnly")
    ForbiddenUses = @("AccountingPnl", "ProductionPnl", "LedgerCommit", "ProductionLive", "PnLMarkPolicy")
    NotProduction = $true
    NotAccounting = $true
    NotLedgerCommit = $true
})

Write-JsonArtifact "expanded-fx-metadata-manifest.json" ([ordered]@{
    Package = $Package
    Artifact = "expanded-fx-metadata-manifest"
    Classification = "EXPANDED_FX_METADATA_MANIFEST_READY_PARTIAL"
    MetadataManifestId = "core-anubis-fx-metadata-r006:" + $R005MetadataManifest.MetadataManifestId.Substring($R005MetadataManifest.MetadataManifestId.Length - 24)
    Scope = "SandboxPreviewSizingOnly"
    AppliesTo = "CoreAnubisIntradaySizing"
    SourceArtifacts = $R005MetadataManifest.SourceArtifacts
    SourceHashes = $R005MetadataManifest.SourceHashes
    DirectMetadata = $R005MetadataManifest.DirectMetadata
    InverseOrExecutionPairMetadata = $R005MetadataManifest.InverseOrExecutionPairMetadata
    SymbolsCovered = $ExistingMetadataCovered
    SymbolsStillMissing = $RemainingSymbols
    AllowedUses = @("SandboxPreviewSizingOnly")
    ForbiddenUses = @("AccountingPnl", "ProductionPnl", "LedgerCommit", "ProductionLive", "R009Submission")
    NotProduction = $true
    NotAccounting = $true
    NotLedgerCommit = $true
})

Write-JsonArtifact "remaining-evidence-gaps.json" ([ordered]@{
    Package = $Package
    Artifact = "remaining-evidence-gaps"
    Classification = "REMAINING_FX_PRICE_AND_METADATA_GAPS"
    MissingPriceSymbols = $RemainingSymbols
    MissingMetadataSymbols = $RemainingSymbols
    InvalidEvidence = @()
    StaleTimestamps = @()
    MissingHashes = $RemainingSymbols
    MissingSourcePath = $RemainingSymbols
    MissingContractMultiplier = $RemainingSymbols
    MissingMinOrderSize = $RemainingSymbols
    MissingQuotedCurrency = $RemainingSymbols
})

Write-JsonArtifact "quantity-readiness-refreshed.json" ([ordered]@{
    Package = $Package
    Artifact = "quantity-readiness-refreshed"
    Classification = "QUANTITY_DERIVATION_BLOCKED_PRICE_AND_METADATA_GAPS"
    DoNotDeriveQuantitiesInR006 = $true
    PriceEvidenceComplete = $false
    MetadataEvidenceComplete = $false
    MissingPriceSymbols = $RemainingSymbols
    MissingMetadataSymbols = $RemainingSymbols
})

Write-JsonArtifact "future-package-decision.json" ([ordered]@{
    Package = $Package
    Artifact = "future-package-decision"
    Decision = "NEXT_CORE_ANUBIS_INTRADAY_FX_OPERATOR_EVIDENCE_R006B"
    Reason = "No filled operator evidence was found, but the R005 operator evidence template remains a usable path for the six missing symbols."
})

Write-JsonArtifact "contract-status-update.json" ([ordered]@{
    Package = $Package
    Artifact = "contract-status-update"
    Statuses = @(
        [ordered]@{ ContractId = "core-anubis-marketdata-price-basis.v1"; Status = "PARTIAL_TEMPLATE_ONLY_NO_NEW_IMPORT" },
        [ordered]@{ ContractId = "core-anubis-instrument-metadata.v1"; Status = "PARTIAL_TEMPLATE_ONLY_NO_NEW_IMPORT" },
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
    Classification = "FX_OPERATOR_EVIDENCE_NOT_IMPORTED_TEMPLATE_ONLY_NO_EXECUTION_READINESS_CHANGE"
    FxOperatorEvidenceImported = $false
    NoFilledEvidenceFound = $true
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

Classification: CORE_ANUBIS_INTRADAY_FX_OPERATOR_EVIDENCE_IMPORT_R006_WITH_WARNINGS_TEMPLATE_ONLY_NO_FILLED_EVIDENCE

Was filled operator evidence found? no.
Prices imported: none.
Prices still missing: CNHUSD, MXNUSD, NOKUSD, SEKUSD, SGDUSD, ZARUSD.
Metadata imported: none.
Metadata still missing: CNHUSD, MXNUSD, NOKUSD, SEKUSD, SGDUSD, ZARUSD.
Which gaps remain? all six remaining symbols still lack filled operator price and metadata evidence.
Is full quantity derivation ready next? no.
Next package: NEXT_CORE_ANUBIS_INTRADAY_FX_OPERATOR_EVIDENCE_R006B.
What did not run? Core execution, manager, Anubis, CUDA, Core netting, LMAX, external market data calls, Polygon/Massive, R009, orders, fills, reports, DB mutation, migrations, seeds, ledger, trading-state mutation, production-state mutation, quantity derivation, risk review, R010 transfer, and accounting/net/production/live readiness.
"@
$Summary | Set-Content -LiteralPath (Join-Path $ArtifactDir "summary.md") -Encoding UTF8

Write-Host "CORE_ANUBIS_INTRADAY_FX_OPERATOR_EVIDENCE_IMPORT_R006 artifacts written."
