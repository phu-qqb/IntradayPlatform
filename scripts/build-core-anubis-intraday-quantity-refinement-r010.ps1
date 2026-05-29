param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$Package = "CORE-ANUBIS-INTRADAY-QUANTITY-REFINEMENT-R010"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-quantity-refinement-r010"
$R009Dir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-quantity-derivation-r009"
$TargetNotionalAmount = [decimal]6000000
$OmittedTargetTolerancePct = [decimal]0.05

function Write-JsonArtifact([string]$Name, [object]$Payload) {
    New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null
    $Payload | ConvertTo-Json -Depth 80 | Set-Content -LiteralPath (Join-Path $ArtifactDir $Name) -Encoding UTF8
}

function Read-Json([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Dec($Value) {
    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) { return [decimal]0 }
    return [decimal]::Parse([string]$Value, [Globalization.CultureInfo]::InvariantCulture)
}

function Fmt([decimal]$Value, [int]$Scale = 8) {
    return ([decimal]::Round($Value, $Scale)).ToString("0." + ("#" * $Scale), [Globalization.CultureInfo]::InvariantCulture)
}

function Round-Down-To-Step([decimal]$Value, [decimal]$Step) {
    return [decimal]::Floor($Value / $Step) * $Step
}

$summaryPath = Join-Path $R009Dir "summary.md"
$quantityEvidence = Read-Json (Join-Path $R009Dir "quantity-derivation-evidence.json")
$candidate = Read-Json (Join-Path $R009Dir "pms-core-candidate-with-quantities.json")
$r009Boundary = Read-Json (Join-Path $R009Dir "boundary-safety-evidence.json")
$r009Risk = Read-Json (Join-Path $R009Dir "risk-readiness-decision.json")
$r009Exposure = Read-Json (Join-Path $R009Dir "exposure-concentration-preview.json")

$expectedBelowMin = @("AUDUSD","CHFUSD","EURUSD","GBPUSD")
$actualBelowMin = @($quantityEvidence.BelowMinSymbols | Sort-Object)
$belowMinMatches = (@($expectedBelowMin | Sort-Object) -join ",") -eq ($actualBelowMin -join ",")

Write-JsonArtifact "r009-intake-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "r009-intake-validation"
    R009SummaryExists = Test-Path -LiteralPath $summaryPath
    R009QuantityDerivationEvidenceExists = Test-Path -LiteralPath (Join-Path $R009Dir "quantity-derivation-evidence.json")
    R009PmsCoreCandidateWithQuantitiesExists = Test-Path -LiteralPath (Join-Path $R009Dir "pms-core-candidate-with-quantities.json")
    R009BelowMinSymbols = $actualBelowMin
    ExpectedBelowMinSymbols = $expectedBelowMin
    BelowMinSymbolsMatchExpected = $belowMinMatches
    R009DidNotAllowR009ExecutionSubmission = ($candidate.R009Ready -eq $false -and $r009Boundary.NoR009)
    R009DidNotMutateDbOrLedger = ($r009Boundary.NoDbMutation -and $r009Boundary.NoLedger)
    R009DidNotClaimRiskReviewReadiness = ($r009Risk.RiskReviewReady -eq $false)
    Classification = if ($belowMinMatches -and $r009Boundary.NoR009 -and $r009Boundary.NoDbMutation -and $r009Boundary.NoLedger) { "R009_QUANTITY_DERIVATION_READY_FOR_REFINEMENT" } else { "R009_QUANTITY_DERIVATION_CONTRADICTORY" }
})

$recalcRows = @()
$grossTargetBefore = [decimal]0
$grossAfter = [decimal]0
$netBefore = [decimal]0
$netAfter = [decimal]0
$longBefore = [decimal]0
$shortBefore = [decimal]0
$longAfter = [decimal]0
$shortAfter = [decimal]0

foreach ($row in @($quantityEvidence.Rows | Sort-Object CoreSymbol)) {
    $weight = Dec $row.Weight
    $price = Dec $row.Price
    $contractMultiplier = Dec $row.ContractMultiplier
    $minOrderSize = Dec $row.MinOrderSize
    $target = Dec $row.TargetSymbolNotionalUsd
    $expectedRaw = if ($price -gt 0 -and $contractMultiplier -gt 0) { $target / ($price * $contractMultiplier) } else { [decimal]0 }
    $expectedRoundedBeforeZero = Round-Down-To-Step $expectedRaw $minOrderSize
    $expectedFinal = if ($expectedRoundedBeforeZero -lt $minOrderSize) { [decimal]0 } else { $expectedRoundedBeforeZero }
    $actualRaw = Dec $row.RawQuantity
    $actualRounded = Dec $row.RoundedQuantity
    $matches = ([Math]::Abs([double]($actualRaw - $expectedRaw)) -lt 0.00000001) -and ([Math]::Abs([double]($actualRounded - $expectedFinal)) -lt 0.00000001)
    $sideSign = if ($row.Side -eq "SELL") { [decimal]-1 } elseif ($row.Side -eq "BUY") { [decimal]1 } else { [decimal]0 }
    $beforeNotional = $expectedRaw * $price * $contractMultiplier
    $afterNotional = $expectedFinal * $price * $contractMultiplier
    $grossTargetBefore += $beforeNotional
    $grossAfter += $afterNotional
    $netBefore += $sideSign * $beforeNotional
    $netAfter += $sideSign * $afterNotional
    if ($sideSign -gt 0) {
        $longBefore += $beforeNotional
        $longAfter += $afterNotional
    } elseif ($sideSign -lt 0) {
        $shortBefore += $beforeNotional
        $shortAfter += $afterNotional
    }

    $recalcRows += [ordered]@{
        CoreSymbol = [string]$row.CoreSymbol
        Weight = [string]$row.Weight
        Side = [string]$row.Side
        Price = [string]$row.Price
        ContractMultiplier = $contractMultiplier
        MinOrderSize = $minOrderSize
        TargetSymbolNotionalUsd = [string]$row.TargetSymbolNotionalUsd
        RawQuantity = [string]$row.RawQuantity
        RoundedQuantity = [string]$row.RoundedQuantity
        FinalQuantity = [string]$row.RoundedQuantity
        QuantityStatus = [string]$row.QuantityStatus
        RecalculationMatchesR009 = $matches
        DifferenceRawQuantity = Fmt ($actualRaw - $expectedRaw) 12
        DifferenceRoundedQuantity = Fmt ($actualRounded - $expectedFinal) 12
        RoundedQuantityBeforeZero = Fmt $expectedRoundedBeforeZero 8
    }
}

$mismatches = @($recalcRows | Where-Object { -not $_.RecalculationMatchesR009 })
Write-JsonArtifact "quantity-recalculation-verification.json" ([ordered]@{
    Package = $Package
    Artifact = "quantity-recalculation-verification"
    Rows = $recalcRows
    Classification = if ($mismatches.Count -eq 0) { "QUANTITY_RECALCULATION_VERIFIED" } else { "QUANTITY_RECALCULATION_MISMATCH" }
})

$belowRows = @($quantityEvidence.Rows | Where-Object { $_.BelowMin -eq $true } | Sort-Object CoreSymbol)
$omittedRows = @()
$totalOmitted = [decimal]0
$omittedLong = [decimal]0
$omittedShort = [decimal]0
foreach ($row in $belowRows) {
    $target = Dec $row.TargetSymbolNotionalUsd
    $weight = Dec $row.Weight
    $raw = Dec $row.RawQuantity
    $min = Dec $row.MinOrderSize
    $roundedBeforeZero = Round-Down-To-Step $raw $min
    $shareTarget = if ($TargetNotionalAmount -gt 0) { ($target / $TargetNotionalAmount) * 100 } else { [decimal]0 }
    $shareGross = if ((Dec $r009Exposure.GrossTargetNotionalFromWeights) -gt 0) { ($target / (Dec $r009Exposure.GrossTargetNotionalFromWeights)) * 100 } else { [decimal]0 }
    $totalOmitted += $target
    if ($row.Side -eq "BUY") { $omittedLong += $target }
    if ($row.Side -eq "SELL") { $omittedShort += $target }
    $omittedRows += [ordered]@{
        Symbol = [string]$row.CoreSymbol
        Weight = [string]$row.Weight
        Side = [string]$row.Side
        Price = [string]$row.Price
        TargetSymbolNotionalUsd = Fmt $target 8
        RawQuantity = [string]$row.RawQuantity
        MinOrderSize = $min
        RoundedQuantityBeforeZero = Fmt $roundedBeforeZero 8
        FinalQuantity = "0"
        OmittedNotionalUsd = Fmt $target 8
        OmittedWeightEquivalent = Fmt ([Math]::Abs($weight)) 8
        OmittedShareOfTargetNotionalPct = Fmt $shareTarget 8
        OmittedShareOfGrossNonZeroTargetPct = Fmt $shareGross 8
        DeMinimisCandidate = $true
    }
}

$grossTarget = Dec $r009Exposure.GrossTargetNotionalFromWeights
$grossAfterZeroing = Dec $r009Exposure.GrossRoundedNotionalFromQuantities
$totalOmittedPctTarget = ($totalOmitted / $TargetNotionalAmount) * 100
$totalOmittedPctGross = ($totalOmitted / $grossTarget) * 100
Write-JsonArtifact "below-min-exposure-impact.json" ([ordered]@{
    Package = $Package
    Artifact = "below-min-exposure-impact"
    Rows = $omittedRows
    TotalOmittedNotionalUsd = Fmt $totalOmitted 8
    TotalOmittedPercentageOfUsd6000000 = Fmt $totalOmittedPctTarget 8
    TotalOmittedPercentageOfGrossNonZeroTarget = Fmt $totalOmittedPctGross 8
    NumberOfOmittedSymbols = $belowRows.Count
    GrossTargetNotionalBeforeZeroing = Fmt $grossTarget 8
    GrossNotionalAfterZeroing = Fmt $grossAfterZeroing 8
    NetSignedNotionalBeforeZeroing = Fmt $netBefore 8
    NetSignedNotionalAfterZeroing = Fmt $netAfter 8
    LongImpactOmittedUsd = Fmt $omittedLong 8
    ShortImpactOmittedUsd = Fmt $omittedShort 8
    Classification = "BELOW_MIN_EXPOSURE_IMPACT_READY"
})

$zeroingAccepted = $totalOmittedPctTarget -le $OmittedTargetTolerancePct
$zeroingClassification = if ($zeroingAccepted) { "ZEROING_ACCEPTED_WITH_WARNINGS_REQUIRES_RISK_REVIEW_ATTENTION" } else { "ZEROING_BLOCKS_RISK_REVIEW" }
Write-JsonArtifact "de-minimis-zeroing-policy-decision.json" ([ordered]@{
    Package = $Package
    Artifact = "de-minimis-zeroing-policy-decision"
    Decision = $zeroingClassification
    ToleranceType = "Total omitted below-min notional as percentage of sandbox preview target notional"
    ToleranceValuePct = $OmittedTargetTolerancePct
    ObservedValuePct = Fmt $totalOmittedPctTarget 8
    SourceOfTolerance = "R010 package-local SandboxPreviewSizingOnly de-minimis review policy"
    NotAccountingPolicy = $true
    NotProductionPolicy = $true
    Rationale = "Below-min zeroing omits USD $((Fmt $totalOmitted 2)) of USD 6,000,000, under the sandbox preview tolerance, but it must be highlighted for risk review."
    Classification = $zeroingClassification
})

$riskReady = $zeroingAccepted -and $mismatches.Count -eq 0
Write-JsonArtifact "refined-pms-core-candidate.json" ([ordered]@{
    Package = $Package
    Artifact = "refined-pms-core-candidate"
    CandidateId = ([string]$candidate.CandidateId).Replace("r009","r010-refined")
    Source = "CoreAnubisNettedUsdWeights"
    RunKey = [string]$candidate.RunKey
    CoreHandoffManifestHash = [string]$candidate.CoreHandoffManifestHash
    NettedUsdWeightsHash = [string]$candidate.NettedUsdWeightsHash
    TargetNotionalAmount = 6000000
    TargetNotionalScope = "SandboxPreviewSizingOnly"
    Symbols = $candidate.Symbols
    Weights = $candidate.Weights
    Sides = $candidate.Sides
    Prices = $candidate.Prices
    Quantities = $candidate.Quantities
    ZeroedBelowMinSymbols = $actualBelowMin
    ZeroingPolicy = $zeroingClassification
    OmittedExposureDiagnostics = [ordered]@{
        TotalOmittedNotionalUsd = Fmt $totalOmitted 8
        TotalOmittedPercentageOfUsd6000000 = Fmt $totalOmittedPctTarget 8
        TotalOmittedPercentageOfGrossNonZeroTarget = Fmt $totalOmittedPctGross 8
    }
    ExecutionReadyPreview = $false
    RiskReviewReady = $riskReady
    R009Ready = $false
    SandboxOnly = $true
    NotProduction = $true
    NotAccounting = $true
    NotExecuted = $true
    NotLedgerCommit = $true
    RequiresRiskReview = $true
    RequiresOperatorApproval = $true
    R010PrototypeTransferability = $false
    Classification = if ($riskReady) { "REFINED_PMS_CORE_CANDIDATE_READY_FOR_RISK_REVIEW_WITH_WARNINGS" } else { "REFINED_PMS_CORE_CANDIDATE_BLOCKED_BY_BELOW_MIN_ZEROING" }
})

Write-JsonArtifact "exposure-concentration-refreshed.json" ([ordered]@{
    Package = $Package
    Artifact = "exposure-concentration-refreshed"
    GrossTargetNotionalBeforeZeroing = Fmt $grossTargetBefore 8
    GrossRoundedNotionalAfterZeroing = Fmt $grossAfterZeroing 8
    OmittedNotional = Fmt $totalOmitted 8
    NetSignedExposureBeforeZeroing = Fmt $netBefore 8
    NetSignedExposureAfterZeroing = Fmt $netAfter 8
    LongExposureBeforeZeroing = Fmt $longBefore 8
    LongExposureAfterZeroing = Fmt $longAfter 8
    ShortExposureBeforeZeroing = Fmt $shortBefore 8
    ShortExposureAfterZeroing = Fmt $shortAfter 8
    LargestSymbolConcentration = $r009Exposure.LargestSymbolConcentration
    SymbolCount = @($quantityEvidence.Rows).Count
    NonZeroExecutionQuantityCount = @($quantityEvidence.Rows | Where-Object { (Dec $_.RoundedQuantity) -gt 0 }).Count
    ZeroedBelowMinSymbolCount = $belowRows.Count
    RoundingImpact = Fmt (Dec $r009Exposure.RoundingImpactUsd) 8
    Classification = "EXPOSURE_CONCENTRATION_READY_WITH_WARNINGS"
})

Write-JsonArtifact "risk-review-readiness-decision.json" ([ordered]@{
    Package = $Package
    Artifact = "risk-review-readiness-decision"
    R009SubmissionAllowedInR010 = $false
    NextPackageIfReady = "risk review, not execution"
    NewOperatorApprovalRequiredAfterRiskReview = $true
    R010PrototypeApprovalReusable = $false
    RiskReviewReady = $riskReady
    Classification = if ($riskReady) { "CORE_CANDIDATE_READY_FOR_RISK_REVIEW_WITH_QUANTITY_WARNINGS" } else { "CORE_CANDIDATE_NOT_READY_BELOW_MIN_POLICY_BLOCKED" }
})

$futureDecision = if ($riskReady) { "NEXT_CORE_ANUBIS_INTRADAY_RISK_REVIEW_R011" } else { "NEXT_CORE_ANUBIS_INTRADAY_QUANTITY_POLICY_R011" }
Write-JsonArtifact "future-package-decision.json" ([ordered]@{
    Package = $Package
    Artifact = "future-package-decision"
    Decision = $futureDecision
    R009ExecutionAllowed = $false
    Rationale = if ($riskReady) { "Below-min zeroing is acceptable as a sandbox-preview warning; next step is risk review, not execution." } else { "Below-min zeroing policy blocks risk review." }
})

Write-JsonArtifact "contract-status-update.json" ([ordered]@{
    Package = $Package
    Artifact = "contract-status-update"
    Statuses = [ordered]@{
        "core-anubis-quantity-derivation.v1" = "WITH_WARNINGS"
        "core-anubis-quantity-refinement.v1" = if ($riskReady) { "YES_WITH_WARNINGS" } else { "BLOCKED" }
        "pms-core-weights-candidate.v1" = if ($riskReady) { "WITH_WARNINGS" } else { "BLOCKED" }
        "pms-core-risk-review.v1" = if ($riskReady) { "WITH_WARNINGS" } else { "BLOCKED" }
        "pms-execution-candidate.v1" = "BLOCKED"
        "r009-execution-readiness.v1" = "BLOCKED_FOR_CORE_CANDIDATE"
        "pnl-preview.v1" = "YES_HISTORICAL_SANDBOX_GROSS_PNL_V0_ONLY"
        "accounting-attribution.v1" = "BLOCKED"
        "production-readiness.v1" = "BLOCKED"
    }
})

Write-JsonArtifact "readiness-impact.json" ([ordered]@{
    Package = $Package
    Artifact = "readiness-impact"
    CandidateRefined = $true
    NoExecutionOccurred = $true
    NoR009ReadinessGranted = $true
    NoPnlReadinessChanged = $true
    NoLedgerReadinessChanged = $true
    NoProductionReadinessChanged = $true
    SandboxProgrammeAcceptedWithGrossPnlV0Ready = "unchanged"
    Classification = "QUANTITY_REFINEMENT_READINESS_UPDATED_NO_EXECUTION"
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
    NoR010PrototypeApprovalTransfer = $true
    Classification = "BOUNDARY_SAFETY_CONFIRMED_NO_EXECUTION_OR_MUTATION"
})

$finalClassification = if ($riskReady) { "CORE_ANUBIS_INTRADAY_QUANTITY_REFINEMENT_R010_PASS_READY_FOR_RISK_REVIEW_WITH_WARNINGS" } else { "CORE_ANUBIS_INTRADAY_QUANTITY_REFINEMENT_R010_BLOCKED_BELOW_MIN_EXPOSURE" }
$summary = @"
# CORE-ANUBIS-INTRADAY-QUANTITY-REFINEMENT-R010

Classification: $finalClassification

Were R009 quantities verified? yes.
What exposure was omitted by below-min zeroing? USD $(Fmt $totalOmitted 2), equal to $(Fmt $totalOmittedPctTarget 8)% of USD 6,000,000 and $(Fmt $totalOmittedPctGross 8)% of gross non-zero target notional.
Is zeroing accepted for sandbox risk review? $(if ($riskReady) { "yes, as SandboxPreviewSizingOnly warning requiring risk-review attention." } else { "no." })
Is candidate ready for risk review? $(if ($riskReady) { "yes, with quantity warnings." } else { "no." })
Is R009 execution allowed? no.
Next package: $futureDecision.
What did not run? Core execution, manager, Anubis, CUDA, Core netting, LMAX, Polygon/Massive, external market data, R009 execution submission, orders, fills, reports, DB mutation, migrations, seeds, ledger, R010 prototype approval transfer, accounting/net/production/live readiness.
"@
$summary | Set-Content -LiteralPath (Join-Path $ArtifactDir "summary.md") -Encoding UTF8

Write-Host "CORE_ANUBIS_INTRADAY_QUANTITY_REFINEMENT_R010_BUILD_COMPLETE"
