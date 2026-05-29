param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$Package = "CORE-ANUBIS-INTRADAY-RISK-REVIEW-R011"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-risk-review-r011"
$R010Dir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-quantity-refinement-r010"
$R009Dir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-quantity-derivation-r009"
$CoreManifestPath = "C:\Users\phili\source\repos\QQ.Production.Core\artifacts\qubes\runs\fx-intraday-qubes_London_H1_universe-v1_mapping-v1_flatbar-v1_tinyplus-strattaken-v1_202601051006\11_intraday_handoff\core_intraday_handoff_manifest.json"
$ExpectedCoreManifestHash = "sha256:8A445CD6195458D09CC539C0C14895C1EFB09027A9BB58D1369BDDD44D99F182"
$ExpectedNettedHash = "sha256:41FF6745857F9E53E28278B7C8DAF91EF2306DE384B753B3BB605B5FD2D01A96"
$TargetNotional = [decimal]6000000

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
    ([decimal]::Round($Value, $Scale)).ToString("0." + ("#" * $Scale), [Globalization.CultureInfo]::InvariantCulture)
}

function New-ShortHash([string]$InputText) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $bytes = [Text.Encoding]::UTF8.GetBytes($InputText)
    (($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString("X2") }) -join "").Substring(0,24)
}

$r010SummaryPath = Join-Path $R010Dir "summary.md"
$r010RecalcPath = Join-Path $R010Dir "quantity-recalculation-verification.json"
$r010BelowMinPath = Join-Path $R010Dir "below-min-exposure-impact.json"
$r010CandidatePath = Join-Path $R010Dir "refined-pms-core-candidate.json"
$r010RiskPath = Join-Path $R010Dir "risk-review-readiness-decision.json"
$r010BoundaryPath = Join-Path $R010Dir "boundary-safety-evidence.json"
$r010ExposurePath = Join-Path $R010Dir "exposure-concentration-refreshed.json"

$r010Recalc = Read-Json $r010RecalcPath
$r010BelowMin = Read-Json $r010BelowMinPath
$r010Candidate = Read-Json $r010CandidatePath
$r010Risk = Read-Json $r010RiskPath
$r010Boundary = Read-Json $r010BoundaryPath
$r010Exposure = Read-Json $r010ExposurePath
$r009Candidate = Read-Json (Join-Path $R009Dir "pms-core-candidate-with-quantities.json")
$coreManifest = Read-Json $CoreManifestPath

$r010Ready = (
    (Test-Path -LiteralPath $r010SummaryPath) -and
    ([string]$r010Recalc.Classification -eq "QUANTITY_RECALCULATION_VERIFIED") -and
    ([string]$r010Candidate.Classification -eq "REFINED_PMS_CORE_CANDIDATE_READY_FOR_RISK_REVIEW_WITH_WARNINGS") -and
    ([string]$r010Risk.Classification -eq "CORE_CANDIDATE_READY_FOR_RISK_REVIEW_WITH_QUANTITY_WARNINGS") -and
    ($r010Risk.RiskReviewReady -eq $true) -and
    ($r010Boundary.NoR009 -and $r010Boundary.NoDbMutation -and $r010Boundary.NoLedger)
)

Write-JsonArtifact "r010-intake-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "r010-intake-validation"
    R010SummaryExists = Test-Path -LiteralPath $r010SummaryPath
    R010QuantityRecalculationVerificationExists = Test-Path -LiteralPath $r010RecalcPath
    R010BelowMinExposureImpactExists = Test-Path -LiteralPath $r010BelowMinPath
    R010RefinedPmsCoreCandidateExists = Test-Path -LiteralPath $r010CandidatePath
    R010RiskReviewReadinessDecisionExists = Test-Path -LiteralPath $r010RiskPath
    R010CandidateReadyForRiskReviewWithWarnings = ($r010Risk.RiskReviewReady -eq $true)
    R010DidNotAllowR009Execution = $r010Boundary.NoR009
    R010DidNotMutateDbOrLedger = ($r010Boundary.NoDbMutation -and $r010Boundary.NoLedger)
    Classification = if ($r010Ready) { "R010_READY_FOR_RISK_REVIEW" } else { "R010_INCOMPLETE" }
})

$riskReviewId = "core-anubis-risk-review-r011:" + (New-ShortHash (([string]$r010Candidate.CandidateId) + ":" + $ExpectedCoreManifestHash + ":" + $ExpectedNettedHash))
Write-JsonArtifact "risk-candidate-identity-lineage.json" ([ordered]@{
    Package = $Package
    Artifact = "risk-candidate-identity-lineage"
    RiskReviewPackage = $Package
    RiskReviewId = $riskReviewId
    CandidateId = [string]$r010Candidate.CandidateId
    Source = "CoreAnubisNettedUsdWeights"
    RunKey = [string]$r010Candidate.RunKey
    CoreHandoffManifestHash = [string]$r010Candidate.CoreHandoffManifestHash
    NettedUsdWeightsHash = [string]$r010Candidate.NettedUsdWeightsHash
    TargetNotionalAmount = 6000000
    TargetNotionalScope = "SandboxPreviewSizingOnly"
    PriceBasisManifestId = [string]$r009Candidate.PriceBasisManifestId
    MetadataManifestId = [string]$r009Candidate.MetadataManifestId
    QuantityDerivationPackage = "R009"
    QuantityRefinementPackage = "R010"
    R010PrototypeApprovalTransferability = $false
    SandboxOnly = $true
    NotProduction = $true
    NotAccounting = $true
    NotExecuted = $true
    NotLedgerCommit = $true
    Classification = "RISK_CANDIDATE_IDENTITY_READY"
})

$grossTarget = Dec $r010Exposure.GrossTargetNotionalBeforeZeroing
$grossRounded = Dec $r010Exposure.GrossRoundedNotionalAfterZeroing
$omitted = Dec $r010BelowMin.TotalOmittedNotionalUsd
$omittedPct = Dec $r010BelowMin.TotalOmittedPercentageOfUsd6000000
$netBefore = Dec $r010Exposure.NetSignedExposureBeforeZeroing
$netAfter = Dec $r010Exposure.NetSignedExposureAfterZeroing
$longAfter = Dec $r010Exposure.LongExposureAfterZeroing
$shortAfter = Dec $r010Exposure.ShortExposureAfterZeroing
$largestExposure = Dec $r010Exposure.LargestSymbolConcentration.RoundedNotionalUsd
$largestConcentrationPct = if ($grossRounded -gt 0) { ($largestExposure / $grossRounded) * 100 } else { [decimal]0 }
$grossLeveragePct = ($grossRounded / $TargetNotional) * 100
$activeCount = [int]$r010Exposure.NonZeroExecutionQuantityCount
$zeroedCount = [int]$r010Exposure.ZeroedBelowMinSymbolCount
$exposurePass = ($grossLeveragePct -lt 1 -and $omittedPct -lt 0.05 -and $largestConcentrationPct -lt 50 -and $activeCount -gt 0)

Write-JsonArtifact "risk-exposure-review.json" ([ordered]@{
    Package = $Package
    Artifact = "risk-exposure-review"
    GrossTargetNotionalBeforeRoundingZeroing = Fmt $grossTarget 8
    GrossRoundedNotionalAfterQuantities = Fmt $grossRounded 8
    TotalOmittedNotional = Fmt $omitted 8
    OmittedPercentageOfSandboxTarget = Fmt $omittedPct 8
    NetSignedExposureBeforeZeroing = Fmt $netBefore 8
    NetSignedExposureAfterZeroing = Fmt $netAfter 8
    LongExposure = Fmt $longAfter 8
    ShortExposure = Fmt $shortAfter 8
    LargestSymbolExposure = $r010Exposure.LargestSymbolConcentration
    LargestSymbolConcentrationPctOfGrossRounded = Fmt $largestConcentrationPct 8
    ActiveQuantityCount = $activeCount
    ZeroedBelowMinCount = $zeroedCount
    BelowMinSymbols = $r010Candidate.ZeroedBelowMinSymbols
    GrossLeveragePctOfUsd6000000SandboxTarget = Fmt $grossLeveragePct 8
    ExposureBounded = $exposurePass
    Classification = if ($exposurePass) { "RISK_EXPOSURE_REVIEW_PASS_WITH_QUANTITY_WARNINGS" } else { "RISK_EXPOSURE_REVIEW_BLOCKED_EXPOSURE_OR_CONCENTRATION" }
})

$symbols = @($r010Candidate.Symbols)
$unknownSymbols = @($symbols | Where-Object { $_ -notmatch '^[A-Z]{3}USD$' })
$directCrosses = @($symbols | Where-Object { $_ -notmatch 'USD$' })
$hasJpy = $symbols -contains "JPYUSD"
$hasUsdjpy = $symbols -contains "USDJPY"
Write-JsonArtifact "symbol-execution-universe-risk-review.json" ([ordered]@{
    Package = $Package
    Artifact = "symbol-execution-universe-risk-review"
    NoDirectCrosses = ($directCrosses.Count -eq 0 -and $coreManifest.DirectCrossesRemoved -eq $true)
    CoreModelSymbolsAreXXXUSD = ($unknownSymbols.Count -eq 0)
    USDJPYNotEmittedByCore = (-not $hasUsdjpy -and $coreManifest.DoNotEmitUSDJPY -eq $true)
    JPYUSDCaveatPreserved = ($hasJpy -and $coreManifest.PreserveJPYUSD -eq $true)
    LaterIntradayExecutionInversionRequiredForJPYUSD = $true
    NoExecutionIntentCreated = $true
    NoProductionLiveRoute = $true
    NoNonFxSymbols = ($unknownSymbols.Count -eq 0)
    UnknownSymbols = $unknownSymbols
    Classification = if ($hasJpy) { "SYMBOL_EXECUTION_UNIVERSE_REVIEW_PASS_WITH_JPYUSD_CAVEAT" } else { "SYMBOL_EXECUTION_UNIVERSE_REVIEW_PASS" }
})

Write-JsonArtifact "quantity-warning-risk-treatment.json" ([ordered]@{
    Package = $Package
    Artifact = "quantity-warning-risk-treatment"
    ZeroedSymbols = @("AUDUSD","CHFUSD","EURUSD","GBPUSD")
    TotalOmittedExposureUsd = Fmt $omitted 8
    OmittedPercentageOfUsd6000000 = Fmt $omittedPct 8
    OmittedExposureAcceptableForSandboxRiskReview = $true
    RiskReviewMustHighlightOmittedSymbols = $true
    FutureOperatorApprovalMustReferenceZeroedSymbols = $true
    ExecutionCandidateLinePolicy = "Future execution candidate should exclude zero-quantity lines or carry them as explicit non-executable zero lines; R011 creates no execution intent."
    Classification = "QUANTITY_WARNINGS_ACCEPTED_WITH_OPERATOR_DISCLOSURE_REQUIRED"
})

$riskPass = $r010Ready -and $exposurePass -and (-not $hasUsdjpy) -and $hasJpy
Write-JsonArtifact "risk-policy-decision.json" ([ordered]@{
    Package = $Package
    Artifact = "risk-policy-decision"
    RiskReviewId = $riskReviewId
    Decision = if ($riskPass) { "RISK_REVIEW_PASS_READY_FOR_OPERATOR_APPROVAL_WITH_WARNINGS" } else { "RISK_REVIEW_BLOCKED_EXPOSURE_OR_CONCENTRATION" }
    ReviewScope = "SandboxPreviewOnly"
    NotProduction = $true
    NotAccounting = $true
    NotLedgerCommit = $true
    NotExecuted = $true
    RequiresOperatorApprovalBeforeExecution = $true
    R009SubmissionAllowedNow = $false
    Classification = if ($riskPass) { "RISK_REVIEW_PASS_READY_FOR_OPERATOR_APPROVAL_WITH_WARNINGS" } else { "RISK_REVIEW_BLOCKED_EXPOSURE_OR_CONCENTRATION" }
})

Write-JsonArtifact "operator-approval-readiness.json" ([ordered]@{
    Package = $Package
    Artifact = "operator-approval-readiness"
    RiskReviewId = $riskReviewId
    CandidateId = [string]$r010Candidate.CandidateId
    RunKey = [string]$r010Candidate.RunKey
    CoreHandoffManifestHash = [string]$r010Candidate.CoreHandoffManifestHash
    NettedUsdWeightsHash = [string]$r010Candidate.NettedUsdWeightsHash
    TargetNotionalAmount = 6000000
    PriceBasisManifestId = [string]$r009Candidate.PriceBasisManifestId
    MetadataManifestId = [string]$r009Candidate.MetadataManifestId
    SymbolsSidesQuantities = $r010Candidate.Quantities
    ZeroedBelowMinSymbols = $r010Candidate.ZeroedBelowMinSymbols
    OmittedExposureUsd = Fmt $omitted 8
    JPYUSDCaveat = "JPYUSD remains model symbol; later execution inversion is separate and not approved here."
    SandboxOnly = $true
    NoProductionLive = $true
    NoLedgerCommit = $true
    NoAccountingNetPnl = $true
    FuturePackageCannotSubmitR009UnlessSeparateExecutionPackage = $true
    Classification = if ($riskPass) { "OPERATOR_APPROVAL_READY_WITH_WARNINGS" } else { "OPERATOR_APPROVAL_NOT_READY" }
})

$futureDecision = if ($riskPass) { "NEXT_CORE_ANUBIS_INTRADAY_OPERATOR_APPROVAL_R012" } else { "NEXT_CORE_ANUBIS_INTRADAY_RISK_REVIEW_R011B" }
Write-JsonArtifact "future-package-decision.json" ([ordered]@{
    Package = $Package
    Artifact = "future-package-decision"
    Decision = $futureDecision
    R009ExecutionAllowed = $false
    Rationale = if ($riskPass) { "Risk review passes with quantity warnings; operator approval is next, not execution." } else { "Risk review did not pass." }
})

Write-JsonArtifact "contract-status-update.json" ([ordered]@{
    Package = $Package
    Artifact = "contract-status-update"
    Statuses = [ordered]@{
        "core-anubis-risk-review.v1" = if ($riskPass) { "YES_WITH_WARNINGS" } else { "BLOCKED" }
        "pms-core-risk-review.v1" = if ($riskPass) { "WITH_WARNINGS" } else { "BLOCKED" }
        "pms-core-operator-approval.v1" = "BLOCKED"
        "pms-core-execution-candidate.v1" = "BLOCKED"
        "r009-execution-readiness.v1" = "BLOCKED_FOR_CORE_CANDIDATE"
        "pnl-preview.v1" = "YES_HISTORICAL_SANDBOX_GROSS_PNL_V0_ONLY"
        "accounting-attribution.v1" = "BLOCKED"
        "production-readiness.v1" = "BLOCKED"
    }
})

Write-JsonArtifact "readiness-impact.json" ([ordered]@{
    Package = $Package
    Artifact = "readiness-impact"
    RiskReviewCompleted = $riskPass
    NoExecutionOccurred = $true
    NoR009ReadinessGranted = $true
    NoPnlReadinessChanged = $true
    NoLedgerReadinessChanged = $true
    NoProductionReadinessChanged = $true
    SandboxProgrammeAcceptedWithGrossPnlV0Ready = "unchanged"
    Classification = if ($riskPass) { "RISK_REVIEW_COMPLETED_NO_EXECUTION_READINESS" } else { "RISK_REVIEW_BLOCKED_NO_EXECUTION" }
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
    NoInventedQuantities = $true
    NoR010PrototypeApprovalTransfer = $true
    Classification = "BOUNDARY_SAFETY_CONFIRMED_NO_EXECUTION_OR_MUTATION"
})

$finalClassification = if ($riskPass) { "CORE_ANUBIS_INTRADAY_RISK_REVIEW_R011_PASS_READY_FOR_OPERATOR_APPROVAL_WITH_WARNINGS" } else { "CORE_ANUBIS_INTRADAY_RISK_REVIEW_R011_BLOCKED_EXPOSURE_OR_CONCENTRATION" }
$summary = @"
# CORE-ANUBIS-INTRADAY-RISK-REVIEW-R011

Classification: $finalClassification

Did risk review pass? $(if ($riskPass) { "yes, with quantity warnings." } else { "no." })
What warnings remain? Below-min zeroing for AUDUSD, CHFUSD, EURUSD, GBPUSD; JPYUSD requires later Intraday execution inversion handling.
What omitted exposure must be disclosed? USD $(Fmt $omitted 2), $(Fmt $omittedPct 8)% of USD 6,000,000 sandbox preview target notional.
Is candidate ready for operator approval? $(if ($riskPass) { "yes, with warnings and disclosure." } else { "no." })
Is R009 execution allowed? no.
Next package: $futureDecision.
What did not run? Core execution, manager, Anubis, CUDA, Core netting, LMAX, Polygon/Massive, external market data, R009 execution submission, orders, fills, reports, DB mutation, migrations, seeds, ledger, R010 prototype approval transfer, accounting/net/production/live readiness.
"@
$summary | Set-Content -LiteralPath (Join-Path $ArtifactDir "summary.md") -Encoding UTF8

Write-Host "CORE_ANUBIS_INTRADAY_RISK_REVIEW_R011_BUILD_COMPLETE"
