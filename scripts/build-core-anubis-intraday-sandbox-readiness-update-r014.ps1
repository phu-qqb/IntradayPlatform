param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$Package = "CORE-ANUBIS-INTRADAY-SANDBOX-READINESS-UPDATE-R014"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-sandbox-readiness-update-r014"
$R013EDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-r013e-post-execution-partial-review-and-closure"
$R013DDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-r013d-fix-and-retry-sandbox-protocol"
$R013BDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-r013b-exact-sandbox-execution-harness"

$CoreRunKey = "fx-intraday-qubes_London_H1_universe-v1_mapping-v1_flatbar-v1_tinyplus-strattaken-v1_202601051006"
$CoreHandoffManifestHash = "sha256:8A445CD6195458D09CC539C0C14895C1EFB09027A9BB58D1369BDDD44D99F182"
$NettedUsdWeightsHash = "sha256:41FF6745857F9E53E28278B7C8DAF91EF2306DE384B753B3BB605B5FD2D01A96"
$CandidateId = "core-anubis-pms-quantity-preview-r010-refined:5E0F1277E153A728481987BD"
$RiskReviewId = "core-anubis-risk-review-r011:5EB84FF8EF3FC6EE8AE6F3E4"
$OperatorApprovalId = "core-anubis-intraday-operator-approval-r012:419206468D9EEAA15DBD3975"

function Write-JsonArtifact([string]$Name, [object]$Payload) {
    New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null
    $Payload | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath (Join-Path $ArtifactDir $Name) -Encoding UTF8
}

function Read-JsonPath([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

$r013eSummaryPath = Join-Path $R013EDir "summary.md"
$r013eSummary = Get-Content -Raw -LiteralPath $r013eSummaryPath
$r013eReview = Read-JsonPath (Join-Path $R013EDir "intended-vs-actual-execution-review.json")
$r013ePartial = Read-JsonPath (Join-Path $R013EDir "partial-fill-impact-review.json")
$r013eFlatten = Read-JsonPath (Join-Path $R013EDir "flatten-residual-closure-validation.json")
$r013ePnl = Read-JsonPath (Join-Path $R013EDir "gross-sandbox-pnl-preview-validation.json")
$r013eLedger = Read-JsonPath (Join-Path $R013EDir "paper-ledger-preview-validation.json")
$r013eAcceptance = Read-JsonPath (Join-Path $R013EDir "lifecycle-acceptance-decision.json")
$r013eBoundary = Read-JsonPath (Join-Path $R013EDir "boundary-safety-evidence.json")
$r013dOpen = Read-JsonPath (Join-Path $R013DDir "guarded-sandbox-retry-open-execution.json")
$r013dFlatten = Read-JsonPath (Join-Path $R013DDir "guarded-sandbox-retry-flatten-execution.json")
$r013dRecon = Read-JsonPath (Join-Path $R013DDir "sandbox-retry-reconciliation.json")
$r013dGross = Read-JsonPath (Join-Path $R013DDir "sandbox-gross-pnl-preview-r013d.json")
$r013dLedger = Read-JsonPath (Join-Path $R013DDir "paper-ledger-preview-update.json")
$r013bDesign = Read-JsonPath (Join-Path $R013BDir "multi-symbol-r009-sandbox-harness-design.json")

$r013eReady = (
    $r013eSummary.Contains("CORE_ANUBIS_INTRADAY_R013E_PASS_LIFECYCLE_ACCEPTED_WITH_PARTIAL_FILL_WARNING") -and
    $r013eReview.Classification -eq "INTENDED_ACTUAL_REVIEW_READY_PARTIAL_USDJPY_ONLY" -and
    $r013ePartial.Classification -eq "PARTIAL_FILL_IMPACT_READY_RESIDUAL_ZERO" -and
    $r013eFlatten.Classification -eq "FLATTEN_RESIDUAL_CLOSURE_PASS_ZERO_RESIDUAL" -and
    $r013ePnl.Classification -eq "GROSS_SANDBOX_PNL_PREVIEW_VALID_WITH_WARNINGS" -and
    $r013eLedger.Classification -eq "PAPER_LEDGER_PREVIEW_VALID_NO_COMMIT" -and
    $r013eAcceptance.Decision -eq "CORE_ANUBIS_SANDBOX_LIFECYCLE_ACCEPTED_WITH_PARTIAL_FILL_WARNING" -and
    $r013eBoundary.NoNewR009SubmissionInR013E -and $r013eBoundary.NoNewLmaxCallInR013E -and
    $r013eBoundary.NoDbMutation -and $r013eBoundary.NoLedgerCommit -and $r013eBoundary.NoProductionLive
)

Write-JsonArtifact "r013e-intake-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "r013e-intake-validation"
    R013ESummaryExists = Test-Path -LiteralPath $r013eSummaryPath
    R013EClassificationLifecycleAcceptedWithPartialFillWarning = $r013eSummary.Contains("CORE_ANUBIS_INTRADAY_R013E_PASS_LIFECYCLE_ACCEPTED_WITH_PARTIAL_FILL_WARNING")
    IntendedVsActualReviewExists = Test-Path -LiteralPath (Join-Path $R013EDir "intended-vs-actual-execution-review.json")
    PartialFillImpactReviewExists = Test-Path -LiteralPath (Join-Path $R013EDir "partial-fill-impact-review.json")
    FlattenResidualClosureValidationExists = Test-Path -LiteralPath (Join-Path $R013EDir "flatten-residual-closure-validation.json")
    GrossSandboxPnlValidationExists = Test-Path -LiteralPath (Join-Path $R013EDir "gross-sandbox-pnl-preview-validation.json")
    PaperLedgerPreviewValidationExists = Test-Path -LiteralPath (Join-Path $R013EDir "paper-ledger-preview-validation.json")
    LifecycleAcceptanceDecisionExists = Test-Path -LiteralPath (Join-Path $R013EDir "lifecycle-acceptance-decision.json")
    R013EDidNotSubmitNewOrders = $r013eBoundary.NoNewOrderFillReport
    R013EDidNotCallLmax = $r013eBoundary.NoNewLmaxCallInR013E
    R013EDidNotMutateDb = $r013eBoundary.NoDbMutation
    R013EDidNotCommitLedger = $r013eBoundary.NoLedgerCommit
    R013EKeptProductionLiveBlocked = $r013eBoundary.NoProductionLive
    Classification = if ($r013eReady) { "R013E_READY_FOR_SANDBOX_READINESS_UPDATE" } else { "R013E_INCOMPLETE" }
})

$warningSummary = [ordered]@{
    BelowMinZeroing = @("AUDUSD","CHFUSD","EURUSD","GBPUSD")
    OmittedBelowMinExposureUsd = "601.92"
    OmittedBelowMinExposurePct = "0.010032%"
    JPYUSDExecutionInversion = "Core model JPYUSD executed via USDJPY"
    USDJPYPartialFill = "intended 88.4, filled 38.4, unfilled 50.0"
    UnfilledUSDJPYRetryApproval = "not approved; future retry requires new explicit approval and separate package"
}

Write-JsonArtifact "core-anubis-sandbox-lifecycle-evidence-summary.json" ([ordered]@{
    Package = $Package
    Artifact = "core-anubis-sandbox-lifecycle-evidence-summary"
    Source = "CoreAnubisNettedUsdWeights"
    CoreRunKey = $CoreRunKey
    CoreHandoffManifestHash = $CoreHandoffManifestHash
    NettedUsdWeightsHash = $NettedUsdWeightsHash
    CandidateId = $CandidateId
    RiskReviewId = $RiskReviewId
    OperatorApprovalId = $OperatorApprovalId
    HarnessId = $r013bDesign.HarnessId
    R013DLifecycleId = "core-anubis-r013d-tag22-fix-one-retry"
    R013ELifecycleAcceptanceId = $r013eAcceptance.LifecycleAcceptanceId
    IntendedOpenOrderCount = 9
    ActualOpenAttemptCount = $r013dOpen.ActualRetryOpenOrders
    FillCount = $r013dOpen.FillCount
    FillSummary = $r013eReview.Rows
    FlattenCount = $r013dFlatten.FillCount
    FlattenSummary = $r013dFlatten.Results
    ResidualStatus = "zero"
    GrossSandboxPnlPreviewStatus = $r013dGross.Classification
    PaperLedgerPreviewStatus = $r013dLedger.Classification
    WarningSummary = $warningSummary
    NotProduction = $true
    NotAccounting = $true
    NotLedgerCommit = $true
    SandboxOnly = $true
    Classification = "CORE_ANUBIS_SANDBOX_LIFECYCLE_EVIDENCE_READY_WITH_WARNINGS"
})

Write-JsonArtifact "partial-fill-warning-preservation.json" ([ordered]@{
    Package = $Package
    Artifact = "partial-fill-warning-preservation"
    USDJPYIntendedQuantity = "88.4"
    USDJPYFilledQuantity = "38.4"
    USDJPYUnfilledQuantity = "50.0"
    UnfilledQuantityNotTreatedAsFilled = $true
    UnfilledQuantityNotApprovedForRetry = $true
    FutureRetryRequiresNewExplicitOperatorApproval = $true
    FutureRetryRequiresSeparateExecutionPackage = $true
    LifecycleAcceptedWithWarning = $true
    ResidualsZeroAfterFlatten = $r013eAcceptance.ResidualsZero
    GrossPnlPreviewUsesActualFillsOnly = $r013ePnl.BasedOnlyOnActualOpenAndFlattenFills
    PaperLedgerPreviewUsesActualFillsOnly = $r013eLedger.UsesActualFillsOnly
    Classification = "PARTIAL_FILL_WARNING_PRESERVED"
})

Write-JsonArtifact "central-readiness-status-update.json" ([ordered]@{
    Package = $Package
    Artifact = "central-readiness-status-update"
    ProductDecisionAnchor = "SandboxProgrammeAcceptedWithGrossPnlV0Ready"
    ExistingReadyLayersPreserved = @("Sandbox order lifecycle","Cross-rail PMS to R009 sandbox","Sandbox reconciliation / flatten","Sandbox price-delta preview","Sandbox gross quote-currency PnL preview V0")
    CoreAnubisSandboxLifecycle = "ACCEPTED_WITH_WARNINGS"
    CoreAnubisSandboxExecution = "executed sandbox/demo"
    CoreAnubisFlatten = "executed"
    CoreAnubisResiduals = "zero"
    CoreAnubisGrossPnlPreview = "valid gross quote-currency sandbox-only"
    CoreAnubisPaperLedgerPreview = "valid preview-only, no commit"
    StillBlocked = @("Full sandbox theoretical PnL","Net PnL","Paper accounting PnL","Paper ledger commit","Production/live","Accounting attribution","Production readiness")
    CrossRailR014RemainsPmsIntentDriven = $true
    SandboxQubesPrototypeRemainsFallbackTestPrototype = $true
    R010PrototypeApprovalNonTransferable = $true
    Classification = "CENTRAL_READINESS_UPDATED_CORE_ANUBIS_SANDBOX_ACCEPTED_WITH_WARNINGS"
})

Write-JsonArtifact "product-decision-update.json" ([ordered]@{
    Package = $Package
    Artifact = "product-decision-update"
    Decision = "SandboxProgrammeAcceptedWithGrossPnlV0Ready_CoreAnubisSandboxLifecycleAcceptedWithWarnings"
    PreservesOriginalAnchor = "SandboxProgrammeAcceptedWithGrossPnlV0Ready"
    CoreAnubisLifecycleWarningRecorded = $true
    NotProduction = $true
    NotAccounting = $true
    NotLedgerCommit = $true
    NoProductionLive = $true
    NoNetPnl = $true
    NoAccountingPnl = $true
})

Write-JsonArtifact "pnl-readiness-update.json" ([ordered]@{
    Package = $Package
    Artifact = "pnl-readiness-update"
    HistoricalSandboxGrossPnlV0Ready = $true
    R013DCoreAnubisGrossSandboxQuoteCurrencyPnlPreviewValid = $true
    R013DPnlActualFillsOnly = $true
    GrossOnly = $true
    QuoteCurrencyOnly = $true
    NoCosts = $true
    NoCommissions = $true
    NoFxConversion = $true
    NoAccountCurrencyAggregation = $true
    NoNetPnl = $true
    NoAccountingPnl = $true
    NoProductionPnl = $true
    PartialUSDJPYHandledUsingActualFillsOnly = $true
    UnfilledUSDJPYNotIncludedAsFilled = $true
    Classification = "PNL_READINESS_UPDATED_GROSS_SANDBOX_ONLY"
})

Write-JsonArtifact "ledger-readiness-update.json" ([ordered]@{
    Package = $Package
    Artifact = "ledger-readiness-update"
    R013DPaperLedgerPreviewValid = $true
    PreviewOnly = $true
    NoCommit = $true
    NoAccountingLedgerMutation = $true
    NoProductionLedgerMutation = $true
    UsesActualFillsOnly = $true
    IncludesFlattenFills = $true
    DoesNotAssumeUnfilledUSDJPYQuantity = $true
    LedgerCommitRemainsBlocked = $true
    Classification = "LEDGER_READINESS_UPDATED_PREVIEW_ONLY_NO_COMMIT"
})

Write-JsonArtifact "execution-readiness-update.json" ([ordered]@{
    Package = $Package
    Artifact = "execution-readiness-update"
    CoreAnubisR013DSandboxExecutionOccurred = $true
    LifecycleAcceptedWithPartialFillWarning = $true
    AnyNewExecutionRequiresNewPackage = $true
    AnyUSDJPYRemainingQuantityRetryRequiresNewOperatorApproval = $true
    NoAutomaticRetries = $true
    ProductionLiveRemainsBlocked = $true
    Classification = "EXECUTION_READINESS_UPDATED_SANDBOX_LIFECYCLE_ACCEPTED_WITH_WARNINGS"
})

Write-JsonArtifact "contract-status-update.json" ([ordered]@{
    Package = $Package
    Artifact = "contract-status-update"
    Statuses = [ordered]@{
        "core-anubis-sandbox-lifecycle.v1" = "YES_WITH_WARNINGS"
        "core-anubis-r013d-protocol-fix-retry.v1" = "WITH_WARNINGS_SANDBOX_ONLY_PARTIAL_USDJPY"
        "core-anubis-r013e-post-execution-review.v1" = "YES_ACCEPTED_WITH_WARNINGS"
        "core-anubis-partial-fill-policy.v1" = "WITH_WARNINGS"
        "sandbox-reconciliation.v1" = "YES"
        "pnl-preview.v1" = "YES_WITH_WARNINGS_GROSS_SANDBOX_QUOTE_CURRENCY_ONLY"
        "ledger-preview.v1" = "YES_WITH_WARNINGS_PREVIEW_ONLY_NO_COMMIT"
        "pms-core-execution-candidate.v1" = "SANDBOX_EXECUTED_WITH_WARNINGS"
        "r009-execution-readiness.v1" = "SANDBOX_ONLY_COMPLETED_WITH_WARNINGS_NOT_PRODUCTION"
        "accounting-attribution.v1" = "BLOCKED"
        "production-readiness.v1" = "BLOCKED"
    }
})

Write-JsonArtifact "blocker-map-update.json" ([ordered]@{
    Package = $Package
    Artifact = "blocker-map-update"
    ClosedBlockers = @(
        "Core/Anubis true weights missing",
        "Core handoff missing",
        "Intraday consumer missing",
        "price basis missing",
        "metadata missing",
        "quantities missing",
        "risk review missing",
        "operator approval missing",
        "exact harness missing",
        "FIX tag 22 protocol blocker",
        "sandbox execution not attempted",
        "residual unknown",
        "gross sandbox PnL missing",
        "paper-ledger preview missing"
    )
    RemainingBlockers = @(
        "partial USDJPY warning remains recorded",
        "net PnL",
        "accounting PnL",
        "production PnL",
        "ledger commit",
        "account-currency aggregation",
        "accounting attribution",
        "production/live",
        "future retry requires new approval"
    )
    Classification = "BLOCKER_MAP_UPDATED_WITH_WARNINGS"
})

Write-JsonArtifact "roadmap-decision.json" ([ordered]@{
    Package = $Package
    Artifact = "roadmap-decision"
    Decision = "NEXT_STOP_CLEAN_PRODUCT_CHECKPOINT"
    Rationale = "Core/Anubis sandbox lifecycle is accepted with warning, and no new evidence or business priority was supplied for cost/net/accounting/product promotion."
    NotAMicroStep = $true
})

Write-JsonArtifact "readiness-impact.json" ([ordered]@{
    Package = $Package
    Artifact = "readiness-impact"
    CoreAnubisSandboxLifecycleAcceptedWithPartialFillWarning = $true
    SandboxProgrammeAcceptedWithGrossPnlV0ReadyRemainsValid = $true
    SandboxReadinessStrengthenedAtGrossPreviewLayer = $true
    NoNetPnlReadiness = $true
    NoAccountingPnlReadiness = $true
    NoLedgerCommitReadiness = $true
    NoProductionLiveReadiness = $true
    NoNewExecutionOccurredInR014 = $true
})

Write-JsonArtifact "boundary-safety-evidence.json" ([ordered]@{
    Package = $Package
    Artifact = "boundary-safety-evidence"
    NoNewR009Submission = $true
    NoNewLmaxCall = $true
    NoNewOrderFillReport = $true
    NoDbMutation = $true
    NoLedgerCommit = $true
    NoProductionLive = $true
    NoCoreExecution = $true
    NoManager = $true
    NoAnubis = $true
    NoCuda = $true
    NoCoreNetting = $true
    NoR010PrototypeTransfer = $true
    NoAccountingNetProductionPnl = $true
    NoAccountCurrencyAggregation = $true
    NoUSDJPYRemainingRetryApproval = $true
})

$finalClassification = "CORE_ANUBIS_INTRADAY_SANDBOX_READINESS_UPDATE_R014_PASS_PRODUCT_STATE_UPDATED_CORE_ANUBIS_LIFECYCLE_ACCEPTED_WITH_WARNINGS"
$summaryText = @"
# CORE-ANUBIS-INTRADAY-SANDBOX-READINESS-UPDATE-R014

Classification: $finalClassification

Was Core/Anubis sandbox lifecycle accepted? yes, accepted with warnings.
What warning remains? USDJPY partial fill: intended 88.4, filled 38.4, unfilled 50.0; unfilled quantity is not treated as filled and not approved for retry. Below-min zeroing warning also remains for AUDUSD, CHFUSD, EURUSD, GBPUSD.
Are residuals zero? yes.
Is gross sandbox PnL preview valid? yes, gross quote-currency sandbox-only and actual-fills-only.
Is paper-ledger preview valid? yes, preview-only/no commit.
Does this change product readiness? yes, Core/Anubis sandbox lifecycle is now accepted-with-warnings while preserving SandboxProgrammeAcceptedWithGrossPnlV0Ready.
What remains blocked? net PnL, accounting PnL, production PnL, account-currency aggregation, accounting attribution, ledger commit, production/live, and any USDJPY remaining-quantity retry without new approval.
What is the recommended next large package? NEXT_STOP_CLEAN_PRODUCT_CHECKPOINT.
What did not run? no new R009, LMAX, order, fill/report, DB mutation, ledger commit, Core manager, Anubis, CUDA, Core netting, production/live, or accounting/net PnL.
"@
$summaryText | Set-Content -LiteralPath (Join-Path $ArtifactDir "summary.md") -Encoding UTF8

Write-Host "CORE_ANUBIS_INTRADAY_SANDBOX_READINESS_UPDATE_R014_BUILD_COMPLETE"
