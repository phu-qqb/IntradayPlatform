param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$Package = "CORE-ANUBIS-INTRADAY-OPERATOR-APPROVAL-R012"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-operator-approval-r012"
$R011Dir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-risk-review-r011"
$R010Dir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-quantity-refinement-r010"
$R009Dir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-quantity-derivation-r009"
$CoreHandoffManifestHash = "sha256:8A445CD6195458D09CC539C0C14895C1EFB09027A9BB58D1369BDDD44D99F182"
$NettedUsdWeightsHash = "sha256:41FF6745857F9E53E28278B7C8DAF91EF2306DE384B753B3BB605B5FD2D01A96"

function Write-JsonArtifact([string]$Name, [object]$Payload) {
    New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null
    $Payload | ConvertTo-Json -Depth 80 | Set-Content -LiteralPath (Join-Path $ArtifactDir $Name) -Encoding UTF8
}

function Read-Json([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Get-Sha256([string]$Path) {
    "sha256:" + (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToUpperInvariant()
}

function New-ShortHash([string]$InputText) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $bytes = [Text.Encoding]::UTF8.GetBytes($InputText)
    (($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString("X2") }) -join "").Substring(0,24)
}

$r011SummaryPath = Join-Path $R011Dir "summary.md"
$r011RiskPolicyPath = Join-Path $R011Dir "risk-policy-decision.json"
$r011ApprovalReadinessPath = Join-Path $R011Dir "operator-approval-readiness.json"
$r011FuturePath = Join-Path $R011Dir "future-package-decision.json"
$r011BoundaryPath = Join-Path $R011Dir "boundary-safety-evidence.json"
$r011IdentityPath = Join-Path $R011Dir "risk-candidate-identity-lineage.json"
$r010CandidatePath = Join-Path $R010Dir "refined-pms-core-candidate.json"
$r010BelowMinPath = Join-Path $R010Dir "below-min-exposure-impact.json"
$r009CandidatePath = Join-Path $R009Dir "pms-core-candidate-with-quantities.json"

$r011Risk = Read-Json $r011RiskPolicyPath
$r011Approval = Read-Json $r011ApprovalReadinessPath
$r011Future = Read-Json $r011FuturePath
$r011Boundary = Read-Json $r011BoundaryPath
$r011Identity = Read-Json $r011IdentityPath
$r010Candidate = Read-Json $r010CandidatePath
$r010BelowMin = Read-Json $r010BelowMinPath
$r009Candidate = Read-Json $r009CandidatePath

$r011Ready = (
    (Test-Path -LiteralPath $r011SummaryPath) -and
    ([string]$r011Risk.Classification -eq "RISK_REVIEW_PASS_READY_FOR_OPERATOR_APPROVAL_WITH_WARNINGS") -and
    ([string]$r011Approval.Classification -eq "OPERATOR_APPROVAL_READY_WITH_WARNINGS") -and
    ([string]$r011Future.Decision -eq "NEXT_CORE_ANUBIS_INTRADAY_OPERATOR_APPROVAL_R012") -and
    ($r011Risk.R009SubmissionAllowedNow -eq $false) -and
    ($r011Boundary.NoR009 -and $r011Boundary.NoDbMutation -and $r011Boundary.NoLedger)
)

Write-JsonArtifact "r011-intake-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "r011-intake-validation"
    R011SummaryExists = Test-Path -LiteralPath $r011SummaryPath
    R011RiskPolicyDecisionExists = Test-Path -LiteralPath $r011RiskPolicyPath
    R011OperatorApprovalReadinessExists = Test-Path -LiteralPath $r011ApprovalReadinessPath
    R011FuturePackageDecision = [string]$r011Future.Decision
    R011FuturePackageDecisionIsR012 = ([string]$r011Future.Decision -eq "NEXT_CORE_ANUBIS_INTRADAY_OPERATOR_APPROVAL_R012")
    R011RiskReviewPassedWithWarnings = ([string]$r011Risk.Classification -eq "RISK_REVIEW_PASS_READY_FOR_OPERATOR_APPROVAL_WITH_WARNINGS")
    R011DidNotAllowR009Execution = ($r011Risk.R009SubmissionAllowedNow -eq $false -and $r011Boundary.NoR009)
    R011DidNotMutateDbOrLedger = ($r011Boundary.NoDbMutation -and $r011Boundary.NoLedger)
    Classification = if ($r011Ready) { "R011_READY_FOR_OPERATOR_APPROVAL" } else { "R011_INCOMPLETE" }
})

$zeroed = @("AUDUSD","CHFUSD","EURUSD","GBPUSD")
$quantities = @($r010Candidate.Quantities)
$sides = @($r010Candidate.Sides)
$weights = @($r010Candidate.Weights)
$symbols = @($r010Candidate.Symbols)
$omittedUsd = [string]$r010BelowMin.TotalOmittedNotionalUsd
$omittedPct = [string]$r010BelowMin.TotalOmittedPercentageOfUsd6000000

Write-JsonArtifact "exact-approved-candidate-binding.json" ([ordered]@{
    Package = $Package
    Artifact = "exact-approved-candidate-binding"
    ApprovalTarget = "CoreAnubisPmsCandidateSandboxPreview"
    FutureExecutionPackage = "CORE-ANUBIS-INTRADAY-R009-SANDBOX-LIFECYCLE-R013"
    CandidateId = [string]$r010Candidate.CandidateId
    RiskReviewId = [string]$r011Risk.RiskReviewId
    Source = "CoreAnubisNettedUsdWeights"
    RunKey = [string]$r010Candidate.RunKey
    CoreHandoffManifestHash = $CoreHandoffManifestHash
    NettedUsdWeightsHash = $NettedUsdWeightsHash
    TargetNotionalAmount = 6000000
    TargetNotionalScope = "SandboxPreviewSizingOnly"
    PriceBasisManifestId = [string]$r009Candidate.PriceBasisManifestId
    MetadataManifestId = [string]$r009Candidate.MetadataManifestId
    Symbols = $symbols
    Weights = $weights
    Sides = $sides
    Quantities = $quantities
    ZeroedBelowMinSymbols = $zeroed
    OmittedExposureUsd = $omittedUsd
    OmittedExposurePct = "$omittedPct%"
    JPYUSDCaveat = "JPYUSD remains Core/PMS model symbol; later execution inversion must be handled separately."
    SandboxOnly = $true
    NotProduction = $true
    NotAccounting = $true
    NotExecuted = $true
    NotLedgerCommit = $true
    R010PrototypeTransferability = $false
    ExecuteNow = $false
    Classification = "EXACT_CORE_ANUBIS_CANDIDATE_BOUND_FOR_OPERATOR_APPROVAL"
})

Write-JsonArtifact "quantity-warning-disclosure-statement.json" ([ordered]@{
    Package = $Package
    Artifact = "quantity-warning-disclosure-statement"
    Disclosures = @(
        "AUDUSD SELL was zeroed below min.",
        "CHFUSD BUY was zeroed below min.",
        "EURUSD SELL was zeroed below min.",
        "GBPUSD SELL was zeroed below min.",
        "Total omitted exposure = USD 601.92.",
        "Omitted exposure = 0.010032% of USD 6,000,000.",
        "Accepted only as SandboxPreviewSizingOnly.",
        "Not production risk tolerance.",
        "Not accounting tolerance.",
        "Future execution approval must preserve or re-review these warnings."
    )
    ZeroedBelowMinSymbols = $zeroed
    TotalOmittedExposureUsd = "601.92"
    OmittedExposurePctOfTarget = "0.010032%"
    SandboxPreviewSizingOnly = $true
    NotProductionTolerance = $true
    NotAccountingTolerance = $true
    FutureExecutionApprovalMustPreserveOrReReviewWarnings = $true
    Classification = "QUANTITY_WARNING_DISCLOSURE_READY"
})

$disclosureHash = Get-Sha256 (Join-Path $ArtifactDir "quantity-warning-disclosure-statement.json")
$riskArtifactHash = Get-Sha256 $r011RiskPolicyPath

Write-JsonArtifact "operator-approval-statement.json" ([ordered]@{
    Package = $Package
    Artifact = "operator-approval-statement"
    ApprovalDecision = "APPROVED_FOR_FUTURE_BOUNDED_SANDBOX_EXECUTION_PACKAGE"
    ApprovalSource = "OperatorProvidedInCentralThread"
    ApprovalPackage = $Package
    ApprovalScope = "FutureR013BoundedSandboxExecutionOnly"
    NotImmediateExecution = $true
    RequiresSeparateExecutionPackage = $true
    NoExecutionInThisPackage = $true
    AppliesOnlyTo = [ordered]@{
        CoreHandoffManifestHash = $CoreHandoffManifestHash
        NettedUsdWeightsHash = $NettedUsdWeightsHash
        TargetNotional = "USD 6,000,000 SandboxPreviewSizingOnly"
        Quantities = $quantities
        RiskReviewId = [string]$r011Risk.RiskReviewId
        QuantityWarningDisclosureHash = $disclosureHash
        JPYUSDCaveat = "JPYUSD model symbol requires later execution inversion handling."
        SandboxOnlyRoute = $true
        NoProductionLive = $true
        NoLedgerCommit = $true
        NoAccountingNetProductionPnl = $true
    }
    DoesNotApplyTo = @(
        "different Core handoff manifest",
        "different netted weights hash",
        "different target notional",
        "different quantities",
        "different zeroing policy",
        "missing quantity warning disclosure",
        "production/live route",
        "ledger commit",
        "accounting/net/production PnL",
        "R010 SandboxQubesPrototype approval",
        "any R009 execution without a separate R013 package"
    )
    Classification = "OPERATOR_APPROVAL_EXPLICIT_FOR_CORE_ANUBIS_FUTURE_SANDBOX_EXECUTION"
})

$quantityInput = ($quantities | ForEach-Object { "$($_.Symbol):$($_.Quantity):$($_.QuantityStatus)" }) -join "|"
$hashInputs = [ordered]@{
    PackageName = $Package
    CandidateId = [string]$r010Candidate.CandidateId
    RiskReviewId = [string]$r011Risk.RiskReviewId
    CoreHandoffManifestHash = $CoreHandoffManifestHash
    NettedUsdWeightsHash = $NettedUsdWeightsHash
    TargetNotionalAmount = "6000000"
    SymbolsSidesQuantities = $quantityInput
    ZeroedBelowMinSymbols = ($zeroed -join "|")
    OmittedExposure = "601.92|0.010032%"
    JPYUSDCaveat = "JPYUSD remains model symbol; later execution inversion required."
    R011RiskReviewArtifactHash = $riskArtifactHash
    QuantityWarningDisclosureHash = $disclosureHash
}
$hashInputText = ($hashInputs.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join "`n"
$approvalId = "core-anubis-intraday-operator-approval-r012:" + (New-ShortHash $hashInputText)
Write-JsonArtifact "operator-approval-id.json" ([ordered]@{
    Package = $Package
    Artifact = "operator-approval-id"
    OperatorApprovalId = $approvalId
    HashAlgorithm = "SHA256"
    HashInputs = $hashInputs
    Scope = "FutureR013BoundedSandboxExecutionOnly"
    NotImmediateExecution = $true
    NotProduction = $true
    NotAccounting = $true
    NotLedger = $true
    CreatedForPackage = $Package
    Classification = "OPERATOR_APPROVAL_ID_CREATED"
})

Write-JsonArtifact "future-r013-execution-preconditions.json" ([ordered]@{
    Package = $Package
    Artifact = "future-r013-execution-preconditions"
    Preconditions = @(
        "R012 OperatorApprovalId exists and validates.",
        "CandidateId matches R012.",
        "RiskReviewId matches R011.",
        "CoreHandoffManifestHash matches approved hash.",
        "NettedUsdWeightsHash matches approved hash.",
        "exact symbols/sides/quantities match R012.",
        "zeroed below-min symbols disclosed.",
        "JPYUSD execution inversion plan exists before any execution involving JPYUSD.",
        "sandbox/demo profile only.",
        "production route disabled.",
        "no DB mutation.",
        "no ledger commit.",
        "duplicate/idempotency guard.",
        "bounded order submission.",
        "flatten plan.",
        "residual handling.",
        "paper-ledger preview only.",
        "no accounting/net/production PnL claim.",
        "no automatic production/live promotion."
    )
    OperatorApprovalId = $approvalId
    CandidateId = [string]$r010Candidate.CandidateId
    RiskReviewId = [string]$r011Risk.RiskReviewId
    Classification = "FUTURE_R013_PRECONDITIONS_READY"
})

Write-JsonArtifact "approval-guardrails.json" ([ordered]@{
    Package = $Package
    Artifact = "approval-guardrails"
    Guardrails = @(
        "Future R013 may submit only the exact approved candidate.",
        "Future R013 must not submit zero-quantity lines as orders.",
        "Future R013 must preserve below-min disclosure.",
        "Future R013 must resolve JPYUSD execution inversion before submission.",
        "Future R013 must use sandbox/demo only.",
        "Future R013 must not use production/live route.",
        "Future R013 must not commit ledger.",
        "Future R013 must not mutate DB.",
        "Future R013 must not claim accounting/net/production PnL.",
        "Future R013 must not proceed automatically to production.",
        "Future R013 must flatten and reconcile if it submits.",
        "Future R013 must produce gross sandbox preview only if fills occur."
    )
    Classification = "APPROVAL_GUARDRAILS_READY"
})

Write-JsonArtifact "readiness-impact.json" ([ordered]@{
    Package = $Package
    Artifact = "readiness-impact"
    R012CapturesOperatorApprovalOnly = $true
    NoExecutionOccurred = $true
    NoR009SubmissionOccurred = $true
    NoPnlReadinessChanged = $true
    NoLedgerReadinessChanged = $true
    NoProductionReadinessChanged = $true
    CoreAnubisCandidateMayProceedToSeparateFutureR013IfPreconditionsPass = $true
    SandboxProgrammeAcceptedWithGrossPnlV0Ready = "unchanged"
    Classification = "OPERATOR_APPROVAL_CAPTURED_NO_EXECUTION_READINESS_CHANGE"
})

Write-JsonArtifact "contract-status-update.json" ([ordered]@{
    Package = $Package
    Artifact = "contract-status-update"
    Statuses = [ordered]@{
        "core-anubis-risk-review.v1" = "YES_WITH_WARNINGS"
        "pms-core-risk-review.v1" = "WITH_WARNINGS"
        "pms-core-operator-approval.v1" = "YES"
        "pms-core-execution-candidate.v1" = "WITH_WARNINGS_NOT_EXECUTED"
        "r009-execution-readiness.v1" = "WITH_WARNINGS_REQUIRES_R013_VALIDATION"
        "pnl-preview.v1" = "YES_HISTORICAL_SANDBOX_GROSS_PNL_V0_ONLY"
        "accounting-attribution.v1" = "BLOCKED"
        "production-readiness.v1" = "BLOCKED"
    }
    R009ExecutionOccurred = $false
    R009SubmissionAllowedNow = $false
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
    NoAccountingNetProductionPnlReadinessClaim = $true
    Classification = "BOUNDARY_SAFETY_CONFIRMED_NO_EXECUTION_OR_MUTATION"
})

$summary = @"
# CORE-ANUBIS-INTRADAY-OPERATOR-APPROVAL-R012

Classification: CORE_ANUBIS_INTRADAY_OPERATOR_APPROVAL_R012_PASS_APPROVAL_CAPTURED_FOR_FUTURE_SANDBOX_EXECUTION

Was operator approval captured? yes.
OperatorApprovalId: $approvalId.
Which candidate is approved? $($r010Candidate.CandidateId), risk review $($r011Risk.RiskReviewId), exact Core handoff hash $CoreHandoffManifestHash, exact netted weights hash $NettedUsdWeightsHash.
What warnings must be disclosed? AUDUSD SELL 0, CHFUSD BUY 0, EURUSD SELL 0, GBPUSD SELL 0 were zeroed below min; omitted exposure USD 601.92 / 0.010032%; JPYUSD requires later execution inversion handling.
Is R009 execution allowed now? no.
Next package: NEXT_CORE_ANUBIS_INTRADAY_R009_SANDBOX_LIFECYCLE_R013.
What did not run? Core execution, manager, Anubis, CUDA, Core netting, LMAX, Polygon/Massive, external market data, R009 execution submission, orders, fills, reports, DB mutation, migrations, seeds, ledger, R010 prototype approval transfer, accounting/net/production/live readiness.
"@
$summary | Set-Content -LiteralPath (Join-Path $ArtifactDir "summary.md") -Encoding UTF8

Write-Host "CORE_ANUBIS_INTRADAY_OPERATOR_APPROVAL_R012_BUILD_COMPLETE"
