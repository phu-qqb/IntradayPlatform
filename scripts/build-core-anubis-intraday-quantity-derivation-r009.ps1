param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$Package = "CORE-ANUBIS-INTRADAY-QUANTITY-DERIVATION-R009"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-quantity-derivation-r009"
$R002Dir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-weights-intraday-handoff-consumer-r002"
$R003Dir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-sizing-r003"
$R006BDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-polygon-fx-data-r006b"
$R008Dir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-lmax-fx-metadata-catalog-r008"
$CoreManifestPath = "C:\Users\phili\source\repos\QQ.Production.Core\artifacts\qubes\runs\fx-intraday-qubes_London_H1_universe-v1_mapping-v1_flatbar-v1_tinyplus-strattaken-v1_202601051006\11_intraday_handoff\core_intraday_handoff_manifest.json"
$ExpectedCoreManifestHash = "sha256:8A445CD6195458D09CC539C0C14895C1EFB09027A9BB58D1369BDDD44D99F182"
$ExpectedNettedHash = "sha256:41FF6745857F9E53E28278B7C8DAF91EF2306DE384B753B3BB605B5FD2D01A96"
$TargetNotionalAmount = [decimal]6000000
$TargetCurrency = "USD"
$TargetScope = "SandboxPreviewSizingOnly"

function Write-JsonArtifact([string]$Name, [object]$Payload) {
    New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null
    $Payload | ConvertTo-Json -Depth 80 | Set-Content -LiteralPath (Join-Path $ArtifactDir $Name) -Encoding UTF8
}

function Read-JsonFile([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Get-Sha256([string]$Path) {
    "sha256:" + (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToUpperInvariant()
}

function To-Decimal($Value) {
    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) { return $null }
    return [decimal]::Parse([string]$Value, [Globalization.CultureInfo]::InvariantCulture)
}

function Round-Down-To-Step([decimal]$Value, [decimal]$Step) {
    if ($Step -le 0) { throw "Min order size must be positive." }
    return [decimal]::Floor($Value / $Step) * $Step
}

function Format-Decimal([decimal]$Value, [int]$Scale = 12) {
    $rounded = [decimal]::Round($Value, $Scale)
    return $rounded.ToString("0." + ("#" * $Scale), [Globalization.CultureInfo]::InvariantCulture)
}

function Find-Price([string]$Symbol, [object]$PriceManifest) {
    $all = @()
    if ($PriceManifest.DirectPrices) { $all += @($PriceManifest.DirectPrices) }
    if ($PriceManifest.InverseSourcePrices) { $all += @($PriceManifest.InverseSourcePrices) }
    if ($PriceManifest.DerivedCorePrices) { $all += @($PriceManifest.DerivedCorePrices) }
    return $all | Where-Object { $_.CoreSymbol -eq $Symbol -and $null -ne $_.DerivedCorePrice } | Select-Object -First 1
}

function Find-Metadata([string]$Symbol, [object]$MetadataManifest) {
    $all = @()
    if ($MetadataManifest.DirectMetadata) { $all += @($MetadataManifest.DirectMetadata) }
    if ($MetadataManifest.InverseOrExecutionPairMetadata) { $all += @($MetadataManifest.InverseOrExecutionPairMetadata) }
    return $all | Where-Object { $_.CoreSymbol -eq $Symbol } | Select-Object -First 1
}

function Source-Symbol($price) {
    if ($price.PriceSourceSymbol) { return [string]$price.PriceSourceSymbol }
    if ($price.SourcePair) { return [string]$price.SourcePair }
    return $null
}

function Source-Type($price) {
    if ($price.DirectInverse) { return [string]$price.DirectInverse }
    if ($price.InversionApplied -eq $true) { return "inverse" }
    return "direct"
}

function Price-Timestamp($price) {
    if ($price.TimestampUtc) { return [string]$price.TimestampUtc }
    if ($price.SourceTimestampUtc) { return [string]$price.SourceTimestampUtc }
    return $null
}

function Price-Hash($price) {
    if ($price.SourceHash) { return [string]$price.SourceHash }
    return $null
}

$r002Summary = Join-Path $R002Dir "summary.md"
$r003PolicyPath = Join-Path $R003Dir "core-sandbox-target-notional-policy.json"
$r003CandidatePath = Join-Path $R003Dir "pms-core-candidate-preview-sizing-status.json"
$priceManifestPath = Join-Path $R006BDir "expanded-core-fx-price-basis-manifest.json"
$metadataManifestPath = Join-Path $R008Dir "completed-core-fx-metadata-manifest.json"
$coreManifest = Read-JsonFile $CoreManifestPath
$priceManifest = Read-JsonFile $priceManifestPath
$metadataManifest = Read-JsonFile $metadataManifestPath
$coreManifestHash = Get-Sha256 $CoreManifestPath
$nettedHash = Get-Sha256 ([string]$coreManifest.NettedUsdWeightsPath)
$r003Policy = Read-JsonFile $r003PolicyPath
$r003Candidate = Read-JsonFile $r003CandidatePath

$intakeReady = (
    (Test-Path -LiteralPath $r002Summary) -and
    ([decimal]$r003Policy.TargetNotionalAmount -eq $TargetNotionalAmount) -and
    ([string]$r003Policy.TargetNotionalCurrency -eq $TargetCurrency) -and
    ([string]$r003Policy.TargetNotionalScope -eq $TargetScope) -and
    ([string]$priceManifest.Classification -eq "EXPANDED_CORE_FX_PRICE_BASIS_READY_ALL_SYMBOLS") -and
    ([string]$metadataManifest.Classification -eq "COMPLETED_CORE_FX_METADATA_MANIFEST_READY_ALL_SYMBOLS") -and
    ($coreManifestHash -eq $ExpectedCoreManifestHash) -and
    ($nettedHash -eq $ExpectedNettedHash)
)

Write-JsonArtifact "intake-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "intake-validation"
    R002CoreHandoffConsumed = Test-Path -LiteralPath $r002Summary
    R003TargetNotionalApplied = ([decimal]$r003Policy.TargetNotionalAmount -eq $TargetNotionalAmount -and [string]$r003Policy.TargetNotionalScope -eq $TargetScope)
    R006BPriceBasisComplete = ([string]$priceManifest.Classification -eq "EXPANDED_CORE_FX_PRICE_BASIS_READY_ALL_SYMBOLS")
    R008MetadataComplete = ([string]$metadataManifest.Classification -eq "COMPLETED_CORE_FX_METADATA_MANIFEST_READY_ALL_SYMBOLS")
    CoreHandoffManifestPath = $CoreManifestPath
    CoreHandoffManifestHash = $coreManifestHash
    CoreHandoffManifestHashMatchesExpected = ($coreManifestHash -eq $ExpectedCoreManifestHash)
    NettedUsdWeightsPath = [string]$coreManifest.NettedUsdWeightsPath
    NettedUsdWeightsHash = $nettedHash
    NettedUsdWeightsHashMatchesExpected = ($nettedHash -eq $ExpectedNettedHash)
    NoPriorCoreCandidateQuantitiesExist = ([string]$r003Candidate.QuantityStatus -eq "MissingSizingAndMarketDataBinding")
    R009ExecutionAllowed = $false
    Classification = if ($intakeReady) { "INTAKE_READY_FOR_QUANTITY_DERIVATION" } else { "INTAKE_INCOMPLETE" }
})

$joinRows = @()
$quantityRows = @()
$candidateRows = @()
$belowMinSymbols = @()
$missingQuantities = @()
$grossTarget = [decimal]0
$grossRounded = [decimal]0
$netSigned = [decimal]0
$longNotional = [decimal]0
$shortNotional = [decimal]0

foreach ($weightRow in @($coreManifest.Weights | Sort-Object Symbol)) {
    $symbol = [string]$weightRow.Symbol
    $weight = To-Decimal $weightRow.Weight
    $zero = ($weight -eq 0)
    $side = if ($weight -gt 0) { "BUY" } elseif ($weight -lt 0) { "SELL" } else { "ZERO" }
    $price = Find-Price $symbol $priceManifest
    $metadata = Find-Metadata $symbol $metadataManifest
    $priceValue = if ($price) { To-Decimal $price.DerivedCorePrice } else { $null }
    $contractMultiplier = if ($metadata) { To-Decimal $metadata.ContractMultiplier } else { $null }
    $minOrderSize = if ($metadata) { To-Decimal $metadata.MinOrderSize } else { $null }
    $metadataHash = if ($metadataManifest.FullCatalogHash) { [string]$metadataManifest.FullCatalogHash } else { ($metadataManifest.SourceHashes | Select-Object -First 1) }

    $joinStatus = if ($zero) {
        "JOIN_READY_ZERO_WEIGHT"
    } elseif ($null -eq $priceValue) {
        "JOIN_BLOCKED_MISSING_PRICE"
    } elseif ($null -eq $metadata -or $null -eq $contractMultiplier -or $null -eq $minOrderSize) {
        "JOIN_BLOCKED_MISSING_METADATA"
    } else {
        "JOIN_READY"
    }

    $symbolTargetNotional = [Math]::Abs($weight) * $TargetNotionalAmount
    $rawQty = $null
    $roundedQty = $null
    $roundingDelta = $null
    $belowMin = $false
    $quantityStatus = "QUANTITY_BLOCKED"
    if ($zero) {
        $symbolTargetNotional = [decimal]0
        $rawQty = [decimal]0
        $roundedQty = [decimal]0
        $roundingDelta = [decimal]0
        $quantityStatus = "QUANTITY_ZERO_WEIGHT"
    } elseif ($joinStatus -eq "JOIN_READY") {
        $rawQty = $symbolTargetNotional / ($priceValue * $contractMultiplier)
        $roundedQty = Round-Down-To-Step $rawQty $minOrderSize
        if ($roundedQty -lt $minOrderSize) {
            $belowMin = $true
            $roundedQty = [decimal]0
            $belowMinSymbols += $symbol
            $quantityStatus = "QUANTITY_BELOW_MIN"
        } else {
            $quantityStatus = "QUANTITY_DERIVED"
        }
        $roundingDelta = $rawQty - $roundedQty
    } else {
        $missingQuantities += $symbol
    }

    $roundedNotional = if ($roundedQty -ne $null -and $priceValue -ne $null -and $contractMultiplier -ne $null) { $roundedQty * $priceValue * $contractMultiplier } else { [decimal]0 }
    $signedRounded = if ($side -eq "SELL") { -1 * $roundedNotional } elseif ($side -eq "BUY") { $roundedNotional } else { [decimal]0 }
    $grossTarget += $symbolTargetNotional
    $grossRounded += [Math]::Abs($roundedNotional)
    $netSigned += $signedRounded
    if ($signedRounded -gt 0) { $longNotional += $signedRounded }
    if ($signedRounded -lt 0) { $shortNotional += [Math]::Abs($signedRounded) }

    $joinRows += [ordered]@{
        CoreSymbol = $symbol
        Weight = $weightRow.Weight
        Zero = $zero
        NonZero = (-not $zero)
        Side = $side
        Price = if ($priceValue -ne $null) { Format-Decimal $priceValue 12 } else { $null }
        PriceSourceSymbol = if ($price) { Source-Symbol $price } else { $null }
        PriceSourceType = if ($price) { Source-Type $price } else { $null }
        PriceTimestampUtc = if ($price) { Price-Timestamp $price } else { $null }
        PriceSourceHash = if ($price) { Price-Hash $price } else { $null }
        ContractMultiplier = $contractMultiplier
        MinOrderSize = $minOrderSize
        QuotedCurrency = if ($metadata) { [string]$metadata.QuotedCurrency } else { $null }
        LmaxId = if ($metadata) { [string]$metadata.SecurityId } else { $null }
        SecurityId = if ($metadata) { [string]$metadata.SecurityId } else { $null }
        MetadataSourceHash = $metadataHash
        JoinStatus = $joinStatus
    }

    $quantityRows += [ordered]@{
        CoreSymbol = $symbol
        Weight = $weightRow.Weight
        Side = $side
        Price = if ($priceValue -ne $null) { Format-Decimal $priceValue 12 } else { $null }
        TargetSymbolNotionalUsd = Format-Decimal $symbolTargetNotional 8
        ContractMultiplier = $contractMultiplier
        MinOrderSize = $minOrderSize
        RawQuantity = if ($rawQty -ne $null) { Format-Decimal $rawQty 12 } else { $null }
        RoundedQuantity = if ($roundedQty -ne $null) { Format-Decimal $roundedQty 8 } else { $null }
        RoundingDelta = if ($roundingDelta -ne $null) { Format-Decimal $roundingDelta 12 } else { $null }
        RoundedNotionalUsd = Format-Decimal $roundedNotional 8
        BelowMin = $belowMin
        QuantityStatus = $quantityStatus
        SourcePriceHash = if ($price) { Price-Hash $price } else { $null }
        MetadataHash = $metadataHash
    }

    $candidateRows += [ordered]@{
        Symbol = $symbol
        Side = $side
        Weight = $weightRow.Weight
        Price = if ($priceValue -ne $null) { Format-Decimal $priceValue 12 } else { $null }
        Quantity = if ($roundedQty -ne $null) { Format-Decimal $roundedQty 8 } else { $null }
        QuantityStatus = $quantityStatus
        TargetSymbolNotionalUsd = Format-Decimal $symbolTargetNotional 8
        RoundedNotionalUsd = Format-Decimal $roundedNotional 8
    }
}

$blockedJoins = @($joinRows | Where-Object { $_.JoinStatus -like "JOIN_BLOCKED*" })
$derived = @($quantityRows | Where-Object { $_.QuantityStatus -eq "QUANTITY_DERIVED" })
$belowMinRows = @($quantityRows | Where-Object { $_.QuantityStatus -eq "QUANTITY_BELOW_MIN" })
$blockedQuantities = @($quantityRows | Where-Object { $_.QuantityStatus -eq "QUANTITY_BLOCKED" })
$nonZeroRows = @($quantityRows | Where-Object { $_.QuantityStatus -ne "QUANTITY_ZERO_WEIGHT" })
$largest = $candidateRows | Sort-Object { [decimal]$_.RoundedNotionalUsd } -Descending | Select-Object -First 1

Write-JsonArtifact "core-weight-price-metadata-join.json" ([ordered]@{
    Package = $Package
    Artifact = "core-weight-price-metadata-join"
    Rows = $joinRows
    Classification = if ($blockedJoins.Count -eq 0) { if ($belowMinRows.Count -gt 0) { "JOIN_READY_WITH_WARNINGS" } else { "JOIN_READY_ALL_NONZERO_SYMBOLS" } } else { "JOIN_BLOCKED" }
})

Write-JsonArtifact "quantity-transformation-policy.json" ([ordered]@{
    Package = $Package
    Artifact = "quantity-transformation-policy"
    TargetNotionalAmount = 6000000
    TargetNotionalCurrency = $TargetCurrency
    TargetNotionalScope = $TargetScope
    Formula = "target symbol notional USD = abs(weight) * 6000000; raw quantity = target symbol notional USD / (price * contract multiplier)"
    RoundingPolicy = "Round down to nearest min order size; do not round up exposure."
    BelowMinPolicy = "Set rounded preview quantity to 0.0 and retain the symbol as a below-min warning; do not create execution quantity."
    ZeroWeightPolicy = "Retain zero-weight symbols as zero if present; no execution quantity for zeros."
    JPYUSDCaveat = "JPYUSD remains the Core/PMS model symbol; later execution inversion to USDJPY is outside this package."
    NoUSDJPYCoreEmission = $true
    NotProduction = $true
    NotAccounting = $true
    NotLedgerCommit = $true
    NotExecuted = $true
    R009Submission = $false
    Classification = "QUANTITY_TRANSFORMATION_POLICY_READY"
})

$quantityClassification = if ($blockedQuantities.Count -gt 0) { "QUANTITIES_PARTIAL" } elseif ($belowMinRows.Count -gt 0) { "QUANTITIES_DERIVED_WITH_BELOW_MIN_WARNINGS" } else { "QUANTITIES_DERIVED_FOR_ALL_NONZERO_SYMBOLS" }
Write-JsonArtifact "quantity-derivation-evidence.json" ([ordered]@{
    Package = $Package
    Artifact = "quantity-derivation-evidence"
    Rows = $quantityRows
    DerivedQuantityCount = $derived.Count
    BelowMinSymbols = @($belowMinSymbols)
    MissingQuantities = @($missingQuantities)
    Classification = $quantityClassification
})

$candidateClassification = if ($blockedQuantities.Count -gt 0) { "PMS_CORE_CANDIDATE_PARTIAL_QUANTITIES" } elseif ($belowMinRows.Count -gt 0) { "PMS_CORE_CANDIDATE_WITH_QUANTITIES_READY_WITH_WARNINGS" } else { "PMS_CORE_CANDIDATE_WITH_QUANTITIES_READY_FOR_RISK_REVIEW" }
$candidateIdSeed = ($coreManifest.NettedUsdWeightsHash + ":" + $priceManifest.PriceBasisManifestId + ":" + $metadataManifest.MetadataManifestId + ":6000000")
$sha = [System.Security.Cryptography.SHA256]::Create()
$idBytes = [Text.Encoding]::UTF8.GetBytes($candidateIdSeed)
$idHash = (($sha.ComputeHash($idBytes) | ForEach-Object { $_.ToString("X2") }) -join "").Substring(0,24)
$candidateId = "core-anubis-pms-quantity-preview-r009:$idHash"

Write-JsonArtifact "pms-core-candidate-with-quantities.json" ([ordered]@{
    Package = $Package
    Artifact = "pms-core-candidate-with-quantities"
    CandidateId = $candidateId
    Source = "CoreAnubisNettedUsdWeights"
    RunKey = [string]$coreManifest.RunKey
    CoreHandoffManifestHash = $ExpectedCoreManifestHash
    NettedUsdWeightsHash = $ExpectedNettedHash
    PriceBasisManifestId = [string]$priceManifest.PriceBasisManifestId
    MetadataManifestId = [string]$metadataManifest.MetadataManifestId
    TargetNotionalAmount = 6000000
    TargetNotionalCurrency = $TargetCurrency
    TargetNotionalScope = $TargetScope
    Symbols = @($candidateRows | ForEach-Object { $_.Symbol })
    Weights = @($candidateRows | ForEach-Object { [ordered]@{ Symbol = $_.Symbol; Weight = $_.Weight } })
    Sides = @($candidateRows | ForEach-Object { [ordered]@{ Symbol = $_.Symbol; Side = $_.Side } })
    Prices = @($candidateRows | ForEach-Object { [ordered]@{ Symbol = $_.Symbol; Price = $_.Price } })
    Quantities = @($candidateRows | ForEach-Object { [ordered]@{ Symbol = $_.Symbol; Quantity = $_.Quantity; QuantityStatus = $_.QuantityStatus } })
    ZeroWeights = @($quantityRows | Where-Object { $_.QuantityStatus -eq "QUANTITY_ZERO_WEIGHT" } | ForEach-Object { $_.CoreSymbol })
    BelowMinSymbols = @($belowMinSymbols)
    MissingQuantities = @($missingQuantities)
    QuantityStatus = $quantityClassification
    ExecutionReadyPreview = $false
    ExecutionReadyPreviewReason = if ($belowMinRows.Count -gt 0) { "Below-min symbols require quantity refinement before risk review." } else { "Risk review and operator approval are still required before any execution readiness." }
    SandboxOnly = $true
    NotProduction = $true
    NotAccounting = $true
    NotExecuted = $true
    NotLedgerCommit = $true
    R009Ready = $false
    RequiresRiskReview = $true
    RequiresOperatorApproval = $true
    R010Transferability = $false
    Rows = $candidateRows
    Classification = $candidateClassification
})

Write-JsonArtifact "exposure-concentration-preview.json" ([ordered]@{
    Package = $Package
    Artifact = "exposure-concentration-preview"
    GrossTargetNotionalFromWeights = Format-Decimal $grossTarget 8
    GrossRoundedNotionalFromQuantities = Format-Decimal $grossRounded 8
    NetSignedNotional = Format-Decimal $netSigned 8
    LongNotional = Format-Decimal $longNotional 8
    ShortNotional = Format-Decimal $shortNotional 8
    LargestSymbolConcentration = if ($largest) { [ordered]@{ Symbol = $largest.Symbol; RoundedNotionalUsd = $largest.RoundedNotionalUsd } } else { $null }
    NonZeroSymbolCount = $nonZeroRows.Count
    SymbolsBelowMin = @($belowMinSymbols)
    RoundingImpactUsd = Format-Decimal ($grossTarget - $grossRounded) 8
    PreviewOnly = $true
    NotAccounting = $true
    NotRiskApproval = $true
    Classification = if ($belowMinRows.Count -gt 0) { "EXPOSURE_PREVIEW_READY_WITH_WARNINGS" } else { "EXPOSURE_PREVIEW_READY" }
})

$riskClass = if ($blockedQuantities.Count -gt 0) { "CORE_CANDIDATE_NOT_READY_FOR_RISK_REVIEW" } elseif ($belowMinRows.Count -gt 0) { "CORE_CANDIDATE_QUANTITY_WARNINGS_REQUIRE_REVIEW_BEFORE_RISK" } else { "CORE_CANDIDATE_READY_FOR_RISK_REVIEW_NOT_EXECUTION" }
Write-JsonArtifact "risk-readiness-decision.json" ([ordered]@{
    Package = $Package
    Artifact = "risk-readiness-decision"
    R009SubmissionAllowedInThisPackage = $false
    NewRiskReviewRequired = $true
    NewOperatorApprovalRequiredAfterRiskReview = $true
    R010PrototypeApprovalTransferable = $false
    RiskReviewReady = ($riskClass -eq "CORE_CANDIDATE_READY_FOR_RISK_REVIEW_NOT_EXECUTION")
    RiskReviewBlockedReason = if ($belowMinRows.Count -gt 0) { "Below-min quantity warnings require refinement before risk review." } elseif ($blockedQuantities.Count -gt 0) { "Quantity derivation incomplete." } else { $null }
    Classification = $riskClass
})

$futureDecision = if ($blockedQuantities.Count -gt 0) { "NEXT_CORE_ANUBIS_INTRADAY_PRICE_METADATA_FIX_R010" } elseif ($belowMinRows.Count -gt 0) { "NEXT_CORE_ANUBIS_INTRADAY_QUANTITY_REFINEMENT_R010" } else { "NEXT_CORE_ANUBIS_INTRADAY_RISK_REVIEW_R010" }
Write-JsonArtifact "future-package-decision.json" ([ordered]@{
    Package = $Package
    Artifact = "future-package-decision"
    Decision = $futureDecision
    Rationale = if ($belowMinRows.Count -gt 0) { "Quantities were derived where above minimum, but below-min symbols require an explicit refinement decision before risk review." } else { "Quantities complete and candidate can proceed to risk review, not execution." }
    R009ExecutionAllowed = $false
    NoCoreExecution = $true
    NoLmaxCall = $true
    NoDbMutation = $true
    NoLedger = $true
})

Write-JsonArtifact "contract-status-update.json" ([ordered]@{
    Package = $Package
    Artifact = "contract-status-update"
    Statuses = [ordered]@{
        "core-anubis-marketdata-price-basis.v1" = "YES"
        "core-anubis-instrument-metadata.v1" = "YES"
        "core-anubis-quantity-readiness.v1" = "YES"
        "core-anubis-quantity-derivation.v1" = if ($belowMinRows.Count -gt 0) { "WITH_WARNINGS" } else { "YES" }
        "pms-core-weights-candidate.v1" = if ($belowMinRows.Count -gt 0) { "WITH_WARNINGS" } else { "YES" }
        "pms-core-risk-review.v1" = "BLOCKED"
        "pms-execution-candidate.v1" = "BLOCKED"
        "r009-execution-readiness.v1" = "BLOCKED_FOR_CORE_CANDIDATE"
        "pnl-preview.v1" = "YES_HISTORICAL_SANDBOX_GROSS_PNL_V0_ONLY"
        "accounting-attribution.v1" = "BLOCKED"
        "production-readiness.v1" = "BLOCKED"
    }
    R009Ready = $false
})

Write-JsonArtifact "readiness-impact.json" ([ordered]@{
    Package = $Package
    Artifact = "readiness-impact"
    QuantitiesDerived = ($blockedQuantities.Count -eq 0)
    QuantityDerivationStatus = $quantityClassification
    NoExecutionOccurred = $true
    NoR009ReadinessGranted = $true
    NoPnlReadinessChanged = $true
    NoLedgerReadinessChanged = $true
    NoProductionReadinessChanged = $true
    SandboxProgrammeAcceptedWithGrossPnlV0Ready = "unchanged"
    Classification = if ($blockedQuantities.Count -eq 0) { "QUANTITY_DERIVATION_READINESS_UPDATED_NO_EXECUTION" } else { "QUANTITY_DERIVATION_BLOCKED_NO_EXECUTION" }
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
    NoPolygonMassiveCall = $true
    NoExternalMarketDataCall = $true
    NoR009 = $true
    NoOrderFillReport = $true
    NoDbMutation = $true
    NoLedger = $true
    NoInventedAccountId = $true
    NoInventedPortfolioId = $true
    NoInventedStrategyId = $true
    NoInventedSourceExecutionIntentId = $true
    NoInventedAccountCurrency = $true
    NoInventedPrices = $true
    NoInventedMetadata = $true
    NoInventedQuantitiesWithoutRequiredInputs = $true
    NoR010Transfer = $true
    R009PackageNameOnlyNotExecutionAlgorithm = $true
    Classification = "BOUNDARY_SAFETY_CONFIRMED_NO_EXECUTION_OR_MUTATION"
})

$quantityLines = $candidateRows | ForEach-Object { "- $($_.Symbol) $($_.Side) quantity $($_.Quantity) ($($_.QuantityStatus))" }
$finalClassification = if ($blockedQuantities.Count -gt 0) { "CORE_ANUBIS_INTRADAY_QUANTITY_DERIVATION_R009_WITH_WARNINGS_PARTIAL_QUANTITIES" } elseif ($belowMinRows.Count -gt 0) { "CORE_ANUBIS_INTRADAY_QUANTITY_DERIVATION_R009_WITH_WARNINGS_QUANTITIES_DERIVED_WITH_WARNINGS" } else { "CORE_ANUBIS_INTRADAY_QUANTITY_DERIVATION_R009_PASS_CANDIDATE_READY_FOR_RISK_REVIEW" }
$summary = @"
# CORE-ANUBIS-INTRADAY-QUANTITY-DERIVATION-R009

Classification: $finalClassification

Were quantities derived for all non-zero Core symbols? $(if ($blockedQuantities.Count -eq 0) { "yes, with below-min zero handling where required." } else { "no, partial only." })
Were any symbols below min order size? $(if ($belowMinRows.Count -gt 0) { "yes: " + (($belowMinSymbols | Sort-Object) -join ", ") } else { "no" }).
Resulting symbols/sides/quantities:
$($quantityLines -join "`n")
Is candidate ready for risk review? $(if ($riskClass -eq "CORE_CANDIDATE_READY_FOR_RISK_REVIEW_NOT_EXECUTION") { "yes, but not execution." } else { "no, quantity refinement is required before risk review." })
Is R009 allowed? no.
Next package: $futureDecision.
What did not run? Core execution, manager, Anubis, CUDA, Core netting, LMAX, Polygon/Massive, external market data, R009 execution submission, orders, fills, reports, DB mutation, migrations, seeds, ledger, risk review, R010 transfer, accounting/net/production/live readiness.
"@
$summary | Set-Content -LiteralPath (Join-Path $ArtifactDir "summary.md") -Encoding UTF8

Write-Host "CORE_ANUBIS_INTRADAY_QUANTITY_DERIVATION_R009_BUILD_COMPLETE"
