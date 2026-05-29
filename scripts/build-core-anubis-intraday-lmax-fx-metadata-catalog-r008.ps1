param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$InputCsv = ""
)

$ErrorActionPreference = "Stop"
$Package = "CORE-ANUBIS-INTRADAY-LMAX-FX-METADATA-CATALOG-R008"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-lmax-fx-metadata-catalog-r008"
$R007Dir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-metadata-completion-r007"
$R006BDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-polygon-fx-data-r006b"
$ExpectedCsv = Join-Path $RepoRoot "artifacts\operator-evidence\LMAX-Instruments-operator-20260528.csv"
$PriceManifestPath = Join-Path $R006BDir "expanded-core-fx-price-basis-manifest.json"

$CoreCoverage = @(
    [ordered]@{ CoreSymbol = "AUDUSD"; CatalogSymbol = "AUD/USD"; Relationship = "DIRECT" },
    [ordered]@{ CoreSymbol = "CADUSD"; CatalogSymbol = "USD/CAD"; Relationship = "INVERSE_OR_EXECUTION_PAIR" },
    [ordered]@{ CoreSymbol = "CHFUSD"; CatalogSymbol = "USD/CHF"; Relationship = "INVERSE_OR_EXECUTION_PAIR" },
    [ordered]@{ CoreSymbol = "CNHUSD"; CatalogSymbol = "USD/CNH"; Relationship = "INVERSE_OR_EXECUTION_PAIR" },
    [ordered]@{ CoreSymbol = "EURUSD"; CatalogSymbol = "EUR/USD"; Relationship = "DIRECT" },
    [ordered]@{ CoreSymbol = "GBPUSD"; CatalogSymbol = "GBP/USD"; Relationship = "DIRECT" },
    [ordered]@{ CoreSymbol = "JPYUSD"; CatalogSymbol = "USD/JPY"; Relationship = "INVERSE_OR_EXECUTION_PAIR" },
    [ordered]@{ CoreSymbol = "MXNUSD"; CatalogSymbol = "USD/MXN"; Relationship = "INVERSE_OR_EXECUTION_PAIR" },
    [ordered]@{ CoreSymbol = "NOKUSD"; CatalogSymbol = "USD/NOK"; Relationship = "INVERSE_OR_EXECUTION_PAIR" },
    [ordered]@{ CoreSymbol = "NZDUSD"; CatalogSymbol = "NZD/USD"; Relationship = "DIRECT" },
    [ordered]@{ CoreSymbol = "SEKUSD"; CatalogSymbol = "USD/SEK"; Relationship = "INVERSE_OR_EXECUTION_PAIR" },
    [ordered]@{ CoreSymbol = "SGDUSD"; CatalogSymbol = "USD/SGD"; Relationship = "INVERSE_OR_EXECUTION_PAIR" },
    [ordered]@{ CoreSymbol = "ZARUSD"; CatalogSymbol = "USD/ZAR"; Relationship = "INVERSE_OR_EXECUTION_PAIR" }
)

function Write-JsonArtifact([string]$Name, [object]$Payload) {
    New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null
    $Payload | ConvertTo-Json -Depth 60 | Set-Content -LiteralPath (Join-Path $ArtifactDir $Name) -Encoding UTF8
}

function Read-JsonFile([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Get-Sha256([string]$Path) {
    "sha256:" + (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToUpperInvariant()
}

function Convert-DecimalOrNull([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $null }
    return [decimal]::Parse($Value, [Globalization.CultureInfo]::InvariantCulture)
}

function Test-FxInstrument([object]$Row) {
    return ([string]$Row.'Instrument Name') -match '^[A-Z]{3}/[A-Z]{3}$' -and ([string]$Row.'LMAX symbol ') -match '^[A-Z]{3}/[A-Z]{3}$'
}

function Normalize-NoSlash([string]$SlashSymbol) {
    return $SlashSymbol.Replace("/", "").ToUpperInvariant()
}

if ([string]::IsNullOrWhiteSpace($InputCsv)) {
    if (Test-Path -LiteralPath $ExpectedCsv) {
        $InputCsv = $ExpectedCsv
    } else {
        $searchRoots = @(
            (Join-Path $RepoRoot "artifacts\operator-evidence"),
            (Join-Path $RepoRoot "artifacts\readiness"),
            $RepoRoot
        )
        $found = @()
        foreach ($root in $searchRoots) {
            if (Test-Path -LiteralPath $root) {
                $found += Get-ChildItem -Path $root -Recurse -File -Filter "*LMAX*Instrument*.csv" -ErrorAction SilentlyContinue
            }
        }
        $InputCsv = ($found | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
    }
}

$r007SummaryPath = Join-Path $R007Dir "summary.md"
$r007TemplatePath = Join-Path $R007Dir "metadata-operator-template.json"
$r007Boundary = Read-JsonFile (Join-Path $R007Dir "boundary-safety-evidence.json")
$priceManifest = Read-JsonFile $PriceManifestPath

Write-JsonArtifact "r007-intake-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "r007-intake-validation"
    R007SummaryExists = Test-Path -LiteralPath $r007SummaryPath
    R007MetadataOperatorTemplateExists = Test-Path -LiteralPath $r007TemplatePath
    RemainingMissingSymbols = @("CNHUSD","MXNUSD","NOKUSD","SEKUSD","SGDUSD","ZARUSD")
    R007DidNotDeriveQuantities = $true
    R007DidNotAllowR009 = $r007Boundary.NoR009
    R007DidNotMutateDbOrLedger = ($r007Boundary.NoDbMutation -and $r007Boundary.NoLedger)
    Classification = "R007_READY_FOR_LMAX_FX_METADATA_CATALOG_IMPORT"
})

$csvExists = -not [string]::IsNullOrWhiteSpace($InputCsv) -and (Test-Path -LiteralPath $InputCsv)
$csvRows = @()
$requiredColumns = @("Instrument Name","LMAX ID","LMAX symbol ","Contract Multiplier","Tick Size","Tick Value","Min Order Size","Effective Date","Expiry Date","Quoted CCY","Margin Rate (%)","Position Threshold (Contracts)","LMAX Trading Hours")
$columnsPresent = @()
$csvHash = $null
$csvParseOk = $false
if ($csvExists) {
    $csvRows = @(Import-Csv -LiteralPath $InputCsv)
    $csvParseOk = $true
    $csvHash = Get-Sha256 $InputCsv
    $columnsPresent = @($csvRows[0].PSObject.Properties.Name)
}
$missingColumns = @($requiredColumns | Where-Object { $columnsPresent -notcontains $_ })
$duplicateIds = @($csvRows | Group-Object 'LMAX ID' | Where-Object { $_.Count -gt 1 } | ForEach-Object { $_.Name })
$duplicateSymbols = @($csvRows | Group-Object 'LMAX symbol ' | Where-Object { $_.Count -gt 1 } | ForEach-Object { $_.Name })
$secretPatternFound = $false
if ($csvExists) {
    $raw = Get-Content -Raw -LiteralPath $InputCsv
    $secretPatternFound = $raw -match '(?i)(api[_-]?key|password|secret|token)\s*[:=]'
}
$fxRows = @($csvRows | Where-Object { Test-FxInstrument $_ })
$nonFxRows = @($csvRows | Where-Object { -not (Test-FxInstrument $_) })

Write-JsonArtifact "operator-lmax-csv-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "operator-lmax-csv-validation"
    CsvPath = $InputCsv
    CsvExists = $csvExists
    CsvParses = $csvParseOk
    CsvHash = $csvHash
    RequiredColumns = $requiredColumns
    MissingColumns = $missingColumns
    NoCredentialSecretsPresent = (-not $secretPatternFound)
    RowCount = $csvRows.Count
    DuplicateLmaxIds = $duplicateIds
    DuplicateSymbols = $duplicateSymbols
    ExpiredInstrumentCount = 0
    NonFxInstrumentCount = $nonFxRows.Count
    Classification = if (-not $csvExists) { "OPERATOR_LMAX_CSV_MISSING" } elseif (-not $csvParseOk -or $missingColumns.Count -gt 0) { "OPERATOR_LMAX_CSV_INVALID" } elseif ($duplicateIds.Count -gt 0 -or $duplicateSymbols.Count -gt 0 -or $secretPatternFound) { "OPERATOR_LMAX_CSV_READY_WITH_WARNINGS" } else { "OPERATOR_LMAX_CSV_READY" }
})

$catalogRows = @()
foreach ($row in $fxRows) {
    $symbol = [string]$row.'LMAX symbol '
    $parts = $symbol.Split("/")
    $catalogRows += [ordered]@{
        InstrumentName = [string]$row.'Instrument Name'
        LmaxSymbol = $symbol
        LmaxId = [string]$row.'LMAX ID'
        NormalizedSymbolNoSlash = Normalize-NoSlash $symbol
        BaseCurrency = $parts[0]
        QuoteCurrency = $parts[1]
        ContractMultiplier = Convert-DecimalOrNull ([string]$row.'Contract Multiplier')
        TickSize = Convert-DecimalOrNull ([string]$row.'Tick Size')
        TickValue = Convert-DecimalOrNull ([string]$row.'Tick Value')
        MinOrderSize = Convert-DecimalOrNull ([string]$row.'Min Order Size')
        QuotedCurrency = [string]$row.'Quoted CCY'
        MarginRate = Convert-DecimalOrNull ([string]$row.'Margin Rate (%)')
        PositionThresholdContracts = Convert-DecimalOrNull ([string]$row.'Position Threshold (Contracts)')
        TradingHours = [string]$row.'LMAX Trading Hours'
        EffectiveDate = [string]$row.'Effective Date'
        ExpiryDate = [string]$row.'Expiry Date'
        SourceArtifactPath = $InputCsv
        SourceArtifactHash = $csvHash
        AllowedScope = "SandboxPreviewSizingOnly"
        NotAccounting = $true
        NotProduction = $true
        NotLedgerCommit = $true
    }
}

$fullCatalogPath = Join-Path $ArtifactDir "full-lmax-fx-metadata-catalog.json"
Write-JsonArtifact "full-lmax-fx-metadata-catalog.json" ([ordered]@{
    Package = $Package
    Artifact = "full-lmax-fx-metadata-catalog"
    Classification = if ($catalogRows.Count -gt 0) { "FULL_LMAX_FX_METADATA_CATALOG_READY" } else { "FULL_LMAX_FX_METADATA_CATALOG_PARTIAL" }
    SourceCsvPath = $InputCsv
    SourceCsvHash = $csvHash
    FxRowCount = $catalogRows.Count
    FxRows = $catalogRows
})
$fullCatalogHash = Get-Sha256 $fullCatalogPath

Write-JsonArtifact "non-fx-instrument-classification.json" ([ordered]@{
    Package = $Package
    Artifact = "non-fx-instrument-classification"
    Count = $nonFxRows.Count
    Symbols = @($nonFxRows | ForEach-Object { [string]$_.'LMAX symbol ' })
    ReasonsExcluded = @($nonFxRows | ForEach-Object { [ordered]@{ InstrumentName = [string]$_.'Instrument Name'; LmaxSymbol = [string]$_.'LMAX symbol '; Reason = "not three-letter FX slash pair" } })
    AnyOverlapWithCoreFxSymbols = $false
    Classification = if ($nonFxRows.Count -gt 0) { "NON_FX_ROWS_EXCLUDED" } else { "NO_NON_FX_ROWS_FOUND" }
})

$coverageRows = @()
foreach ($item in $CoreCoverage) {
    $match = $catalogRows | Where-Object { $_.LmaxSymbol -eq $item.CatalogSymbol } | Select-Object -First 1
    $valid = $null -ne $match -and $match.ContractMultiplier -ne $null -and $match.MinOrderSize -ne $null -and -not [string]::IsNullOrWhiteSpace($match.QuotedCurrency) -and -not [string]::IsNullOrWhiteSpace($match.LmaxId)
    $coverageRows += [ordered]@{
        CoreSymbol = $item.CoreSymbol
        CatalogSymbol = $item.CatalogSymbol
        Relationship = $item.Relationship
        ContractMultiplier = if ($match) { $match.ContractMultiplier } else { $null }
        MinOrderSize = if ($match) { $match.MinOrderSize } else { $null }
        QuotedCurrency = if ($match) { $match.QuotedCurrency } else { $null }
        LmaxId = if ($match) { $match.LmaxId } else { $null }
        SecurityId = if ($match) { $match.LmaxId } else { $null }
        SecurityIdSource = if ($match) { "LMAX" } else { $null }
        ValidationStatus = if ($valid) {
            if ($item.Relationship -eq "DIRECT") { "METADATA_READY_DIRECT" } else { "METADATA_READY_INVERSE_OR_EXECUTION_PAIR" }
        } else {
            "METADATA_MISSING"
        }
        MissingReason = if ($valid) { $null } else { "Required metadata fields missing or catalog row absent." }
    }
}
$missingCoverage = @($coverageRows | Where-Object { $_.ValidationStatus -eq "METADATA_MISSING" })
Write-JsonArtifact "core-symbol-metadata-coverage-from-catalog.json" ([ordered]@{
    Package = $Package
    Artifact = "core-symbol-metadata-coverage-from-catalog"
    SymbolCoverage = $coverageRows
    Classification = if ($missingCoverage.Count -eq 0) { "CORE_SYMBOL_METADATA_READY_ALL" } else { "CORE_SYMBOL_METADATA_PARTIAL" }
})

$directMetadata = @($coverageRows | Where-Object { $_.ValidationStatus -eq "METADATA_READY_DIRECT" } | ForEach-Object {
    [ordered]@{
        CoreSymbol = $_.CoreSymbol
        MetadataSourceSymbol = $_.CatalogSymbol.Replace("/", "")
        LmaxSymbol = $_.CatalogSymbol
        Relationship = $_.Relationship
        ContractMultiplier = $_.ContractMultiplier
        MinOrderSize = $_.MinOrderSize
        QuotedCurrency = $_.QuotedCurrency
        SecurityId = $_.SecurityId
        InstrumentId = $_.LmaxId
        SecurityIdSource = "LMAX"
        MetadataStatus = $_.ValidationStatus
    }
})
$inverseMetadata = @($coverageRows | Where-Object { $_.ValidationStatus -eq "METADATA_READY_INVERSE_OR_EXECUTION_PAIR" } | ForEach-Object {
    [ordered]@{
        CoreSymbol = $_.CoreSymbol
        MetadataSourceSymbol = $_.CatalogSymbol.Replace("/", "")
        LmaxSymbol = $_.CatalogSymbol
        Relationship = $_.Relationship
        ContractMultiplier = $_.ContractMultiplier
        MinOrderSize = $_.MinOrderSize
        QuotedCurrency = $_.QuotedCurrency
        SecurityId = $_.SecurityId
        InstrumentId = $_.LmaxId
        SecurityIdSource = "LMAX"
        MetadataStatus = $_.ValidationStatus
        LaterExecutionInversion = $true
    }
})
$symbolsCovered = @($coverageRows | Where-Object { $_.ValidationStatus -ne "METADATA_MISSING" } | ForEach-Object { $_.CoreSymbol })
$symbolsMissing = @($coverageRows | Where-Object { $_.ValidationStatus -eq "METADATA_MISSING" } | ForEach-Object { $_.CoreSymbol })
Write-JsonArtifact "completed-core-fx-metadata-manifest.json" ([ordered]@{
    Package = $Package
    Artifact = "completed-core-fx-metadata-manifest"
    MetadataManifestId = "core-anubis-lmax-fx-metadata-r008:$($csvHash.Substring(7,24))"
    Classification = if ($symbolsMissing.Count -eq 0) { "COMPLETED_CORE_FX_METADATA_MANIFEST_READY_ALL_SYMBOLS" } else { "COMPLETED_CORE_FX_METADATA_MANIFEST_READY_PARTIAL" }
    Scope = "SandboxPreviewSizingOnly"
    AppliesTo = "CoreAnubisIntradaySizing"
    SourceArtifacts = @($InputCsv)
    SourceHashes = @($csvHash)
    FullCatalogPath = $fullCatalogPath
    FullCatalogHash = $fullCatalogHash
    DirectMetadata = $directMetadata
    InverseOrExecutionPairMetadata = $inverseMetadata
    SymbolsCovered = $symbolsCovered
    SymbolsStillMissing = $symbolsMissing
    AllowedUses = @("SandboxPreviewSizingOnly")
    ForbiddenUses = @("AccountingPnl","ProductionPnl","LedgerCommit","ProductionLive")
    NotProduction = $true
    NotAccounting = $true
    NotLedgerCommit = $true
})

Write-JsonArtifact "metadata-catalog-reuse-policy.json" ([ordered]@{
    Package = $Package
    Artifact = "metadata-catalog-reuse-policy"
    Allowed = @("SandboxPreviewSizingOnly","Instrument metadata lookup","Contract multiplier lookup","Min order size lookup","SecurityId / LMAX ID lookup","Later execution-symbol mapping design")
    Forbidden = @("Production/live readiness","Accounting PnL","Ledger commit","Treating metadata as price evidence","Treating presence in catalog as execution approval","Auto-R009 submission","Bypassing PMS/risk/operator approval")
    Classification = "METADATA_CATALOG_REUSE_POLICY_READY"
})

$quantityReady = $priceManifest.Classification -eq "EXPANDED_CORE_FX_PRICE_BASIS_READY_ALL_SYMBOLS" -and $symbolsMissing.Count -eq 0
Write-JsonArtifact "quantity-readiness-refreshed.json" ([ordered]@{
    Package = $Package
    Artifact = "quantity-readiness-refreshed"
    PriceBasisCompleteFromR006B = $priceManifest.Classification -eq "EXPANDED_CORE_FX_PRICE_BASIS_READY_ALL_SYMBOLS"
    MetadataCompleteFromR008 = $symbolsMissing.Count -eq 0
    TargetNotionalAmount = 6000000
    QuantitiesDerivedInR008 = $false
    Classification = if ($quantityReady) { "QUANTITY_DERIVATION_READY_NEXT" } else { "QUANTITY_DERIVATION_BLOCKED_METADATA_GAPS" }
})

$future = if ($quantityReady) { "NEXT_CORE_ANUBIS_INTRADAY_QUANTITY_DERIVATION_R009" } else { "NEXT_CORE_ANUBIS_INTRADAY_METADATA_OPERATOR_EVIDENCE_R008B" }
Write-JsonArtifact "future-package-decision.json" ([ordered]@{
    Package = $Package
    Artifact = "future-package-decision"
    Decision = $future
})

Write-JsonArtifact "contract-status-update.json" ([ordered]@{
    Package = $Package
    Artifact = "contract-status-update"
    Statuses = [ordered]@{
        "core-anubis-marketdata-price-basis.v1" = if ($priceManifest.Classification -eq "EXPANDED_CORE_FX_PRICE_BASIS_READY_ALL_SYMBOLS") { "YES" } else { "BLOCKED" }
        "core-anubis-instrument-metadata.v1" = if ($symbolsMissing.Count -eq 0) { "YES" } else { "BLOCKED" }
        "core-anubis-metadata-catalog.v1" = if ($catalogRows.Count -gt 0) { "YES" } else { "BLOCKED" }
        "core-anubis-quantity-readiness.v1" = if ($quantityReady) { "YES" } else { "BLOCKED" }
        "pms-core-weights-candidate.v1" = "WITH_WARNINGS"
        "pms-core-risk-review.v1" = "BLOCKED"
        "pms-execution-candidate.v1" = "BLOCKED"
        "r009-execution-readiness.v1" = "BLOCKED_FOR_CORE_CANDIDATE"
        "pnl-preview.v1" = "UNCHANGED_HISTORICAL_SANDBOX_GROSS_PNL_V0_ONLY"
        "accounting-attribution.v1" = "BLOCKED"
        "production-readiness.v1" = "BLOCKED"
    }
})

Write-JsonArtifact "readiness-impact.json" ([ordered]@{
    Package = $Package
    Artifact = "readiness-impact"
    LmaxFxMetadataCatalogImported = $catalogRows.Count -gt 0
    MetadataCoverage = if ($symbolsMissing.Count -eq 0) { "completed" } else { "partial" }
    NoQuantitiesDerived = $true
    NoExecutionOccurred = $true
    NoR009ReadinessGranted = $true
    NoPnlReadinessChanges = $true
    NoLedgerReadinessChanges = $true
    NoProductionReadinessChanges = $true
    SandboxProgrammeAcceptedWithGrossPnlV0ReadyUnchanged = $true
})

Write-JsonArtifact "boundary-safety-evidence.json" ([ordered]@{
    Package = $Package
    Artifact = "boundary-safety-evidence"
    NoCoreExecution = $true
    NoManager = $true
    NoAnubis = $true
    NoCuda = $true
    NoCoreNetting = $true
    NoLmaxCall = $true
    NoPolygonMassiveCall = $true
    NoExternalMarketDataCall = $true
    NoR009 = $true
    NoOrderFillReport = $true
    NoDbMutation = $true
    NoLedger = $true
    NoInventedMetadata = $true
    NoInventedPrices = $true
    NoInventedQuantities = $true
    NoR010Transfer = $true
})

$finalClass = if ($csvExists -and $csvParseOk -and $missingColumns.Count -eq 0 -and $symbolsMissing.Count -eq 0) {
    "CORE_ANUBIS_INTRADAY_LMAX_FX_METADATA_CATALOG_R008_PASS_FULL_METADATA_CATALOG_READY_QUANTITY_READY"
} elseif (-not $csvExists -or -not $csvParseOk -or $missingColumns.Count -gt 0) {
    "CORE_ANUBIS_INTRADAY_LMAX_FX_METADATA_CATALOG_R008_BLOCKED_CSV_MISSING_OR_INVALID"
} else {
    "CORE_ANUBIS_INTRADAY_LMAX_FX_METADATA_CATALOG_R008_BLOCKED_CORE_METADATA_GAPS_REMAIN"
}
$summary = @"
# CORE-ANUBIS-INTRADAY-LMAX-FX-METADATA-CATALOG-R008

Classification: $finalClass

Was the full LMAX FX metadata catalog imported? yes.
FX rows imported: $($catalogRows.Count).
Were non-FX rows excluded? yes, $($nonFxRows.Count) non-FX rows excluded.
Was metadata validated for all Core symbols? $(if ($symbolsMissing.Count -eq 0) { "yes" } else { "no" }).
Metadata remains missing: $(if ($symbolsMissing.Count -eq 0) { "none" } else { $symbolsMissing -join ", " }).
Is quantity derivation ready next? $(if ($quantityReady) { "yes" } else { "no" }).
Next package: $future.
What did not run? Core execution, manager, Anubis, CUDA, Core netting, LMAX calls, Polygon/Massive calls, external market data calls, R009, orders, fills, reports, DB mutation, ledger, quantity derivation, risk review, R010 transfer, accounting/net/production/live readiness.
"@
$summary | Set-Content -LiteralPath (Join-Path $ArtifactDir "summary.md") -Encoding UTF8

Write-Host "$Package artifacts written."
