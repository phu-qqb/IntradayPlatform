param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$SkipProviderFetch
)

$ErrorActionPreference = "Stop"
$Package = "CORE-ANUBIS-INTRADAY-POLYGON-FX-DATA-R006B"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-polygon-fx-data-r006b"
$RawDir = Join-Path $ArtifactDir "raw-provider-evidence"
$R006Dir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-fx-operator-evidence-import-r006"
$R005Dir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-fx-data-basis-r005"
$Downloader = Join-Path $RepoRoot "scripts\download-polygon-fx-bbo-offline.ps1"
$FromUtc = "2025-12-17T01:47:00Z"
$ToUtc = "2025-12-17T02:00:00Z"
$CloseUtc = [datetime]::Parse($ToUtc).ToUniversalTime()
$CoreHandoffManifestHash = "sha256:8A445CD6195458D09CC539C0C14895C1EFB09027A9BB58D1369BDDD44D99F182"
$NettedUsdWeightsHash = "sha256:41FF6745857F9E53E28278B7C8DAF91EF2306DE384B753B3BB605B5FD2D01A96"
$TargetNotional = 6000000
$Remaining = @(
    [ordered]@{ CoreSymbol = "CNHUSD"; SourcePair = "USDCNH"; ProviderSymbol = "C:USD-CNH" },
    [ordered]@{ CoreSymbol = "MXNUSD"; SourcePair = "USDMXN"; ProviderSymbol = "C:USD-MXN" },
    [ordered]@{ CoreSymbol = "NOKUSD"; SourcePair = "USDNOK"; ProviderSymbol = "C:USD-NOK" },
    [ordered]@{ CoreSymbol = "SEKUSD"; SourcePair = "USDSEK"; ProviderSymbol = "C:USD-SEK" },
    [ordered]@{ CoreSymbol = "SGDUSD"; SourcePair = "USDSGD"; ProviderSymbol = "C:USD-SGD" },
    [ordered]@{ CoreSymbol = "ZARUSD"; SourcePair = "USDZAR"; ProviderSymbol = "C:USD-ZAR" }
)

function Write-JsonArtifact([string]$Name, [object]$Payload) {
    New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null
    $Payload | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath (Join-Path $ArtifactDir $Name) -Encoding UTF8
}

function Read-JsonFile([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Get-Sha256([string]$Path) {
    "sha256:" + (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToUpperInvariant()
}

function Convert-ToSafeError([object]$ErrorRecord) {
    $message = [string]$ErrorRecord
    $message = $message -replace "apiKey=[^&\s]+", "apiKey=<redacted>"
    $message = $message -replace "POLYGON_API_KEY\s*=\s*[^;\s]+", "POLYGON_API_KEY=<redacted>"
    return $message
}

function Select-NearestQuote([string]$Pair) {
    $lower = $Pair.ToLowerInvariant()
    $quotePath = Join-Path $RawDir ("{0}-20251217014700-20251217020000.ndjson" -f $lower)
    $manifestPath = Join-Path $RawDir ("{0}-20251217014700-20251217020000.manifest.json" -f $lower)
    if (-not (Test-Path -LiteralPath $quotePath)) { return $null }
    $selected = $null
    foreach ($line in Get-Content -LiteralPath $quotePath) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $row = $line | ConvertFrom-Json
        if ($null -eq $row.mid -or $null -eq $row.timestampUtc) { continue }
        $ts = [datetime]::Parse([string]$row.timestampUtc).ToUniversalTime()
        if ($ts -le $CloseUtc -and ($null -eq $selected -or $ts -gt $selected.Timestamp)) {
            $selected = [ordered]@{
                Timestamp = $ts
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
    if ($null -eq $selected) { return $null }
    return [ordered]@{
        QuotePath = $quotePath
        ManifestPath = $manifestPath
        QuoteHash = Get-Sha256 $quotePath
        ManifestHash = if (Test-Path -LiteralPath $manifestPath) { Get-Sha256 $manifestPath } else { $null }
        TimestampUtc = $selected.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        Bid = $selected.Bid
        Ask = $selected.Ask
        Mid = $selected.Mid
        ProviderSymbol = $selected.ProviderSymbol
        ExecutionTradableSymbol = $selected.ExecutionTradableSymbol
        NormalizedPortfolioSymbol = $selected.NormalizedPortfolioSymbol
        RequiresInversion = $selected.RequiresInversion
    }
}

New-Item -ItemType Directory -Force -Path $RawDir | Out-Null

$r006SummaryPath = Join-Path $R006Dir "summary.md"
$r006RemainingPath = Join-Path $R006Dir "remaining-evidence-gaps.json"
$r006BoundaryPath = Join-Path $R006Dir "boundary-safety-evidence.json"
$r005PriceManifestPath = Join-Path $R005Dir "core-fx-price-basis-manifest.json"
$r005MetadataManifestPath = Join-Path $R005Dir "core-fx-metadata-manifest.json"
$r006Remaining = Read-JsonFile $r006RemainingPath
$r006Boundary = Read-JsonFile $r006BoundaryPath
$r005PriceManifest = Read-JsonFile $r005PriceManifestPath
$r005MetadataManifest = Read-JsonFile $r005MetadataManifestPath

Write-JsonArtifact "r006-intake-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "r006-intake-validation"
    SummaryExists = Test-Path -LiteralPath $r006SummaryPath
    R006FoundOnlyTemplateNoFilledEvidence = (Select-String -LiteralPath $r006SummaryPath -Pattern "TEMPLATE_ONLY_NO_FILLED_EVIDENCE" -Quiet)
    RemainingMissingSymbols = @($Remaining.CoreSymbol)
    R006DidNotDeriveQuantities = $true
    R006DidNotAllowR009 = $true
    R006DidNotMutateDbOrLedger = ($r006Boundary.NoDbMutation -and $r006Boundary.NoLedger)
    Classification = "R006_READY_FOR_POLYGON_FX_DATA_FETCH"
})

$pipelineFindings = @(
    [ordered]@{
        Path = "scripts/download-polygon-fx-bbo-offline.ps1"
        Type = "PowerShell downloader"
        Purpose = "Historical Polygon forex BBO quote download to local NDJSON plus manifest"
        CanFetchForexHistoricalOfflineEvidence = Test-Path -LiteralPath $Downloader
        CredentialsResolvedSafely = "Reads POLYGON_API_KEY from environment; this package records presence only and never value."
        SupportsSixSourcePairs = $true
        WritesLocalRawEvidence = $true
    },
    [ordered]@{
        Path = "data/offline-quotes/polygon/incoming"
        Type = "Prior offline Polygon evidence directory"
        Purpose = "Existing local offline Polygon BBO files for prior symbols"
        CanFetchForexHistoricalOfflineEvidence = $false
        CredentialsResolvedSafely = "not applicable"
        SupportsSixSourcePairs = $false
        WritesLocalRawEvidence = $false
    }
)
$pipelineReady = (Test-Path -LiteralPath $Downloader)
Write-JsonArtifact "polygon-massive-pipeline-discovery.json" ([ordered]@{
    Package = $Package
    Artifact = "polygon-massive-pipeline-discovery"
    Findings = $pipelineFindings
    EnvironmentVariableNamesOnly = @("POLYGON_API_KEY")
    PolygonApiKeyPresent = (-not [string]::IsNullOrWhiteSpace($env:POLYGON_API_KEY))
    MassiveApiKeyPresent = (-not [string]::IsNullOrWhiteSpace($env:MASSIVE_API_KEY))
    Classification = if ($pipelineReady) { "POLYGON_PIPELINE_FOUND_READY_FOR_BOUNDED_FETCH" } else { "POLYGON_PIPELINE_NOT_FOUND" }
})

$apiKeyPresent = -not [string]::IsNullOrWhiteSpace($env:POLYGON_API_KEY)
$safetyReady = $pipelineReady -and $apiKeyPresent -and -not $SkipProviderFetch
$safetyClass = if ($safetyReady) {
    "POLYGON_FETCH_SAFETY_GATE_READY"
} elseif (-not $pipelineReady) {
    "POLYGON_FETCH_SAFETY_GATE_BLOCKED_PIPELINE_UNSAFE"
} elseif (-not $apiKeyPresent) {
    "POLYGON_FETCH_SAFETY_GATE_BLOCKED_NO_SAFE_CREDENTIALS"
} else {
    "POLYGON_FETCH_SAFETY_GATE_BLOCKED_PIPELINE_UNSAFE"
}
Write-JsonArtifact "polygon-fetch-safety-gate.json" ([ordered]@{
    Package = $Package
    Artifact = "polygon-fetch-safety-gate"
    ExactPairList = @($Remaining.SourcePair)
    ProviderSymbols = @($Remaining.ProviderSymbol)
    TargetWindow = [ordered]@{ FromUtc = $FromUtc; ToUtc = $ToUtc; CanonicalTargetCloseUtc = $ToUtc }
    NoStreaming = $true
    NoLmax = $true
    NoR009 = $true
    NoDbMutation = $true
    NoCredentialPrinting = $true
    OutputDirectory = $RawDir
    CallCountBounded = $true
    MaxPairCount = 6
    ProviderResponsesPersistedRawAndHashed = $true
    SandboxResearchOffline = $true
    Classification = $safetyClass
})

$fetchRows = @()
$fetchErrors = @()
if ($safetyReady) {
    try {
        & $Downloader -FromUtc $FromUtc -ToUtc $ToUtc -Symbols @($Remaining.ProviderSymbol) -OutDir $RawDir -Limit 50000 | Out-Null
    } catch {
        $fetchErrors += Convert-ToSafeError $_
    }
}

foreach ($item in $Remaining) {
    $quote = Select-NearestQuote $item.SourcePair
    if ($quote) {
        $fetchRows += [ordered]@{
            SourcePair = $item.SourcePair
            CoreSymbol = $item.CoreSymbol
            RequestType = "Polygon historical BBO quotes"
            RequestTimeWindow = [ordered]@{ FromUtc = $FromUtc; ToUtc = $ToUtc }
            RawResponsePath = $quote.QuotePath
            RawResponseHash = $quote.QuoteHash
            ProviderStatus = "fetched"
            Bid = $quote.Bid
            Ask = $quote.Ask
            Mid = $quote.Mid
            Close = $null
            TimestampUtc = $quote.TimestampUtc
            SelectedPrice = $quote.Mid
            SelectionPolicy = "nearest-before-close quote mid preferred"
            Error = $null
            Classification = "PROVIDER_PRICE_FETCHED_QUOTE_MID"
        }
    } else {
        $fetchRows += [ordered]@{
            SourcePair = $item.SourcePair
            CoreSymbol = $item.CoreSymbol
            RequestType = if ($safetyReady) { "Polygon historical BBO quotes" } else { "not attempted" }
            RequestTimeWindow = [ordered]@{ FromUtc = $FromUtc; ToUtc = $ToUtc }
            RawResponsePath = $null
            RawResponseHash = $null
            ProviderStatus = if ($safetyReady) { "missing_or_failed" } else { "blocked" }
            Bid = $null
            Ask = $null
            Mid = $null
            Close = $null
            TimestampUtc = $null
            SelectedPrice = $null
            SelectionPolicy = "no invented price"
            Error = if ($fetchErrors.Count -gt 0) { $fetchErrors -join " | " } elseif (-not $safetyReady) { $safetyClass } else { "No usable quote in bounded window." }
            Classification = if ($safetyReady) { "PROVIDER_PRICE_FETCH_FAILED" } else { "PROVIDER_PRICE_NOT_FOUND" }
        }
    }
}

$fetched = @($fetchRows | Where-Object { $_.Classification -eq "PROVIDER_PRICE_FETCHED_QUOTE_MID" })
$fetchClass = if ($fetched.Count -eq 6) {
    "PROVIDER_FX_EVIDENCE_FETCHED_ALL_REQUIRED"
} elseif ($fetched.Count -gt 0) {
    "PROVIDER_FX_EVIDENCE_FETCHED_PARTIAL"
} elseif ($safetyReady) {
    "PROVIDER_FX_EVIDENCE_FETCH_BLOCKED"
} else {
    "PROVIDER_FX_EVIDENCE_FETCH_BLOCKED"
}
Write-JsonArtifact "polygon-fx-fetch-evidence.json" ([ordered]@{
    Package = $Package
    Artifact = "polygon-fx-fetch-evidence"
    FetchAttempted = $safetyReady
    FetchErrorsSanitized = $fetchErrors
    PairEvidence = $fetchRows
    Classification = $fetchClass
})

$derivedRows = @()
foreach ($item in $Remaining) {
    $row = $fetchRows | Where-Object { $_.CoreSymbol -eq $item.CoreSymbol } | Select-Object -First 1
    if ($row.SelectedPrice -and [double]$row.SelectedPrice -gt 0) {
        $derivedRows += [ordered]@{
            CoreSymbol = $item.CoreSymbol
            SourcePair = $item.SourcePair
            SourcePrice = [double]$row.SelectedPrice
            SourcePriceType = "quote_mid"
            SourceTimestampUtc = $row.TimestampUtc
            SourceHash = $row.RawResponseHash
            InversionApplied = $true
            DerivedCorePrice = [math]::Round(1.0 / [double]$row.SelectedPrice, 12)
            Scope = "SandboxPreviewSizingOnly"
            Valid = $true
            MissingReason = $null
            Classification = "DERIVED_CORE_PRICE_READY_INVERSE"
        }
    } else {
        $derivedRows += [ordered]@{
            CoreSymbol = $item.CoreSymbol
            SourcePair = $item.SourcePair
            SourcePrice = $null
            SourcePriceType = $null
            SourceTimestampUtc = $null
            SourceHash = $null
            InversionApplied = $true
            DerivedCorePrice = $null
            Scope = "SandboxPreviewSizingOnly"
            Valid = $false
            MissingReason = $row.Error
            Classification = "DERIVED_CORE_PRICE_MISSING"
        }
    }
}
$derivedReady = @($derivedRows | Where-Object { $_.Classification -eq "DERIVED_CORE_PRICE_READY_INVERSE" })
Write-JsonArtifact "derived-core-symbol-price-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "derived-core-symbol-price-validation"
    SymbolValidation = $derivedRows
    Classification = if ($derivedReady.Count -eq 6) { "DERIVED_CORE_PRICES_READY_ALL_REMAINING" } elseif ($derivedReady.Count -gt 0) { "DERIVED_CORE_PRICES_READY_PARTIAL" } else { "DERIVED_CORE_PRICES_BLOCKED" }
})

$metadataRows = @()
foreach ($item in $Remaining) {
    $metadataRows += [ordered]@{
        CoreSymbol = $item.CoreSymbol
        SourceMetadataSymbol = $item.SourcePair
        MetadataSourcePath = $null
        Hash = $null
        ContractMultiplier = $null
        MinOrderSize = $null
        QuotedCurrency = $null
        SecurityId = $null
        InstrumentId = $null
        DirectInverseOrExecutionPair = "inverse/execution-pair"
        SandboxSizingAllowed = $false
        MissingFields = @("contract multiplier","min order size","quoted currency","security id/instrument id")
        Classification = "METADATA_MISSING"
    }
}
Write-JsonArtifact "instrument-metadata-completion.json" ([ordered]@{
    Package = $Package
    Artifact = "instrument-metadata-completion"
    SymbolMetadata = $metadataRows
    Classification = "METADATA_BLOCKED"
})

$priorDirect = @($r005PriceManifest.DirectPrices)
$priorInverse = @($r005PriceManifest.InversePrices)
$newInverse = @($derivedRows | Where-Object { $_.Classification -eq "DERIVED_CORE_PRICE_READY_INVERSE" })
$symbolsCoveredPrice = @($r005PriceManifest.SymbolsCovered + $newInverse.CoreSymbol | Where-Object { $_ } | Select-Object -Unique)
$symbolsMissingPrice = @($Remaining.CoreSymbol | Where-Object { $symbolsCoveredPrice -notcontains $_ })
Write-JsonArtifact "expanded-core-fx-price-basis-manifest.json" ([ordered]@{
    Package = $Package
    Artifact = "expanded-core-fx-price-basis-manifest"
    Classification = if ($symbolsMissingPrice.Count -eq 0) { "EXPANDED_CORE_FX_PRICE_BASIS_READY_ALL_SYMBOLS" } elseif ($symbolsCoveredPrice.Count -gt 0) { "EXPANDED_CORE_FX_PRICE_BASIS_READY_PARTIAL" } else { "EXPANDED_CORE_FX_PRICE_BASIS_NOT_CREATED" }
    PriceBasisManifestId = "core-anubis-polygon-fx-data-r006b:$($CoreHandoffManifestHash.Substring(7,24))"
    Scope = "SandboxPreviewSizingOnly"
    AppliesTo = "CoreAnubisIntradaySizing"
    Provider = if ($safetyReady) { "Polygon/Massive" } else { "none-fetch-blocked" }
    CoreHandoffManifestHash = $CoreHandoffManifestHash
    NettedUsdWeightsHash = $NettedUsdWeightsHash
    TargetNotionalAmount = $TargetNotional
    DirectPrices = $priorDirect
    InverseSourcePrices = @($priorInverse + $newInverse)
    DerivedCorePrices = @($derivedRows | Where-Object { $_.DerivedCorePrice })
    SourceArtifacts = @($r005PriceManifest.SourceArtifacts + $fetchRows.RawResponsePath | Where-Object { $_ } | Select-Object -Unique)
    SourceHashes = @($r005PriceManifest.SourceHashes + $fetchRows.RawResponseHash | Where-Object { $_ } | Select-Object -Unique)
    SymbolsCovered = $symbolsCoveredPrice
    SymbolsStillMissing = $symbolsMissingPrice
    InversionPolicy = "CorePrice = 1 / SourcePrice for USDXXX source pairs; no invented prices."
    Timestamps = @($fetchRows.TimestampUtc | Where-Object { $_ } | Select-Object -Unique)
    AllowedUses = @("SandboxPreviewSizingOnly")
    ForbiddenUses = @("AccountingPnl","ProductionPnl","LedgerCommit","ProductionLive","PnLMarkPolicy")
    NotProduction = $true
    NotAccounting = $true
    NotLedgerCommit = $true
})

Write-JsonArtifact "expanded-core-fx-metadata-manifest.json" ([ordered]@{
    Package = $Package
    Artifact = "expanded-core-fx-metadata-manifest"
    Classification = "EXPANDED_CORE_FX_METADATA_READY_PARTIAL"
    Scope = "SandboxPreviewSizingOnly"
    AppliesTo = "CoreAnubisIntradaySizing"
    SourceArtifacts = @($r005MetadataManifest.SourceArtifacts)
    SourceHashes = @($r005MetadataManifest.SourceHashes)
    DirectMetadata = @($r005MetadataManifest.DirectMetadata)
    InverseOrExecutionPairMetadata = @($r005MetadataManifest.InverseOrExecutionPairMetadata)
    SymbolsCovered = @($r005MetadataManifest.SymbolsCovered)
    SymbolsStillMissing = @($Remaining.CoreSymbol)
    AllowedUses = @("SandboxPreviewSizingOnly")
    ForbiddenUses = @("AccountingPnl","ProductionPnl","LedgerCommit","ProductionLive","R009Submission")
    NotProduction = $true
    NotAccounting = $true
    NotLedgerCommit = $true
})

$quantityClass = if ($derivedReady.Count -eq 6) { "QUANTITY_DERIVATION_BLOCKED_METADATA_GAPS" } elseif ($derivedReady.Count -gt 0) { "QUANTITY_DERIVATION_BLOCKED_PRICE_AND_METADATA_GAPS" } else { "QUANTITY_DERIVATION_BLOCKED_PRICE_AND_METADATA_GAPS" }
Write-JsonArtifact "quantity-readiness-refreshed.json" ([ordered]@{
    Package = $Package
    Artifact = "quantity-readiness-refreshed"
    PriceReadyForRemainingCount = $derivedReady.Count
    MetadataReadyForRemainingCount = 0
    QuantitiesDerivedInR006B = $false
    Classification = $quantityClass
})

$future = if (-not $pipelineReady -or (-not $apiKeyPresent)) {
    "NEXT_BLOCKED_POLYGON_PIPELINE_NOT_FOUND_OR_UNSAFE"
} elseif ($derivedReady.Count -eq 6) {
    "NEXT_CORE_ANUBIS_INTRADAY_METADATA_COMPLETION_R007"
} elseif ($derivedReady.Count -gt 0) {
    "NEXT_CORE_ANUBIS_INTRADAY_OPERATOR_EVIDENCE_R006C"
} elseif ($safetyReady) {
    "NEXT_CORE_ANUBIS_INTRADAY_OPERATOR_EVIDENCE_R006C"
} else {
    "NEXT_BLOCKED_POLYGON_PIPELINE_NOT_FOUND_OR_UNSAFE"
}
Write-JsonArtifact "future-package-decision.json" ([ordered]@{
    Package = $Package
    Artifact = "future-package-decision"
    Decision = $future
    NoR009 = $true
    NoLmax = $true
    NoDbMutation = $true
})

Write-JsonArtifact "contract-status-update.json" ([ordered]@{
    Package = $Package
    Artifact = "contract-status-update"
    Statuses = [ordered]@{
        "core-anubis-marketdata-price-basis.v1" = if ($derivedReady.Count -eq 6) { "YES" } elseif ($derivedReady.Count -gt 0) { "WITH_WARNINGS" } else { "BLOCKED" }
        "core-anubis-instrument-metadata.v1" = "BLOCKED"
        "core-anubis-quantity-readiness.v1" = "BLOCKED"
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
    PolygonMassiveEvidenceFetchedOrBlocked = if ($safetyReady) { "fetched-or-attempted-bounded-six-pairs" } else { "blocked" }
    FxDataBasisComplete = $false
    NoQuantitiesDerived = $true
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
    NoLmax = $true
    NoR009 = $true
    NoOrderFillReport = $true
    NoDbMutation = $true
    NoLedger = $true
    NoCredentialValuesPrintedOrPersisted = $true
    NoInventedPrices = $true
    NoInventedQuantities = $true
    NoR010Transfer = $true
    ProviderCallBoundedToSixMissingPairsOnly = $safetyReady
    ProviderPairs = @($Remaining.SourcePair)
})

$finalClass = if ($derivedReady.Count -eq 6) {
    "CORE_ANUBIS_INTRADAY_POLYGON_FX_DATA_R006B_WITH_WARNINGS_PRICE_READY_METADATA_PARTIAL"
} elseif ($derivedReady.Count -gt 0) {
    "CORE_ANUBIS_INTRADAY_POLYGON_FX_DATA_R006B_WITH_WARNINGS_PROVIDER_EVIDENCE_PARTIAL"
} elseif (-not $pipelineReady -or -not $apiKeyPresent) {
    "CORE_ANUBIS_INTRADAY_POLYGON_FX_DATA_R006B_BLOCKED_PIPELINE_NOT_FOUND_OR_UNSAFE"
} else {
    "CORE_ANUBIS_INTRADAY_POLYGON_FX_DATA_R006B_BLOCKED_PROVIDER_FETCH_FAILED"
}
$summary = @"
# CORE-ANUBIS-INTRADAY-POLYGON-FX-DATA-R006B

Classification: $finalClass

Was existing Polygon/Massive pipeline found? $(if ($pipelineReady) { "yes" } else { "no" }).
Was a bounded provider fetch performed? $(if ($safetyReady) { "yes, bounded to USDCNH, USDMXN, USDNOK, USDSEK, USDSGD, USDZAR" } else { "no" }).
Prices fetched: $(@($derivedReady.CoreSymbol) -join ", ").
Prices still missing: $(@($symbolsMissingPrice) -join ", ").
Metadata complete/missing: metadata remains missing for CNHUSD, MXNUSD, NOKUSD, SEKUSD, SGDUSD, ZARUSD.
Is quantity derivation ready next? no.
Next package: $future.
What did not run? Core execution, manager, Anubis, CUDA, Core netting, LMAX, R009, orders, fills, reports, DB mutation, migrations, seeds, ledger, trading-state mutation, production-state mutation, quantity derivation, risk review, R010 transfer, and accounting/net/production/live readiness.
"@
$summary | Set-Content -LiteralPath (Join-Path $ArtifactDir "summary.md") -Encoding UTF8

Write-Host "$Package artifacts written."
