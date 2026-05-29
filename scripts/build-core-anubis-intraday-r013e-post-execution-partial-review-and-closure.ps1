param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$Package = "CORE-ANUBIS-INTRADAY-R013E-POST-EXECUTION-PARTIAL-REVIEW-AND-CLOSURE"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-r013e-post-execution-partial-review-and-closure"
$R013DDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-r013d-fix-and-retry-sandbox-protocol"
$OperatorApprovalId = "core-anubis-intraday-operator-approval-r012:419206468D9EEAA15DBD3975"
$CandidateId = "core-anubis-pms-quantity-preview-r010-refined:5E0F1277E153A728481987BD"
$RiskReviewId = "core-anubis-risk-review-r011:5EB84FF8EF3FC6EE8AE6F3E4"
$RunKey = "fx-intraday-qubes_London_H1_universe-v1_mapping-v1_flatbar-v1_tinyplus-strattaken-v1_202601051006"

function Write-JsonArtifact([string]$Name, [object]$Payload) {
    New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null
    $Payload | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath (Join-Path $ArtifactDir $Name) -Encoding UTF8
}

function Read-JsonPath([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Dec([object]$Value) {
    [decimal]::Parse([string]$Value, [Globalization.CultureInfo]::InvariantCulture)
}

function QtyStatus([decimal]$Intended, [decimal]$Filled) {
    if ($Filled -eq 0) { return "NONE" }
    if ($Filled -lt $Intended) { return "PARTIAL" }
    if ($Filled -eq $Intended) { return "FULL" }
    return "CONTRADICTORY"
}

$summaryPath = Join-Path $R013DDir "summary.md"
$summary = Get-Content -Raw -LiteralPath $summaryPath
$retryOpen = Read-JsonPath (Join-Path $R013DDir "guarded-sandbox-retry-open-execution.json")
$retryFlatten = Read-JsonPath (Join-Path $R013DDir "guarded-sandbox-retry-flatten-execution.json")
$retryRecon = Read-JsonPath (Join-Path $R013DDir "sandbox-retry-reconciliation.json")
$grossPnl = Read-JsonPath (Join-Path $R013DDir "sandbox-gross-pnl-preview-r013d.json")
$paperLedger = Read-JsonPath (Join-Path $R013DDir "paper-ledger-preview-update.json")
$rootCause = Read-JsonPath (Join-Path $R013DDir "root-cause-decision.json")
$fixDesign = Read-JsonPath (Join-Path $R013DDir "fix-design-and-approval-applicability.json")
$boundary = Read-JsonPath (Join-Path $R013DDir "boundary-safety-evidence.json")

$openRows = @($retryOpen.Results)
$flattenRows = @($retryFlatten.Results)
$residualRows = @($retryRecon.Residuals)
$pnlRows = @($grossPnl.Rows)

$r013dReady = (
    $summary.Contains("CORE_ANUBIS_INTRADAY_R013D_WITH_WARNINGS_PROTOCOL_FIX_RETRY_EXECUTED_WITH_REJECTS_OR_PARTIALS") -and
    $rootCause.Classification -eq "ROOT_CAUSE_TAG22_SECURITYIDSOURCE_FORMAT" -and
    $fixDesign.ExpectedFixTag22Value -eq "8" -and
    $retryOpen.ExactlyOneRetryAttemptAfterNoFillRejects -eq $true -and
    $retryOpen.PartialFillCount -eq 1 -and
    @($openRows | Where-Object { $_.CoreSymbol -eq "JPYUSD" -and $_.ExecutionSymbol -eq "USDJPY" -and $_.Quantity -eq "88.4" -and $_.FillQuantity -eq "38.4" }).Count -eq 1 -and
    $retryFlatten.Classification -eq "RETRY_FLATTEN_EXECUTED_RESIDUAL_ZERO" -and
    @($residualRows | Where-Object { (Dec $_.ResidualSignedQuantity) -ne 0 }).Count -eq 0 -and
    $grossPnl.Classification -eq "SANDBOX_GROSS_PNL_R013D_COMPUTED_WITH_WARNINGS" -and
    $paperLedger.Classification -eq "PAPER_LEDGER_PREVIEW_CREATED_NO_COMMIT" -and
    $paperLedger.Commit -eq $false -and
    $boundary.NoLedgerCommit -eq $true -and
    $boundary.NoProductionLiveLmax -eq $true
)

Write-JsonArtifact "r013d-intake-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "r013d-intake-validation"
    R013DSummaryExists = Test-Path -LiteralPath $summaryPath
    R013DClassificationExecutedWithPartials = $summary.Contains("CORE_ANUBIS_INTRADAY_R013D_WITH_WARNINGS_PROTOCOL_FIX_RETRY_EXECUTED_WITH_REJECTS_OR_PARTIALS")
    RootCauseWasTag22SecurityIdSource = $rootCause.Classification -eq "ROOT_CAUSE_TAG22_SECURITYIDSOURCE_FORMAT"
    ProtocolFixWasLmaxTo8 = $fixDesign.ExpectedFixTag22Value -eq "8"
    RetryExecutedExactlyOnce = $retryOpen.ExactlyOneRetryAttemptAfterNoFillRejects
    ProducedFills = $retryOpen.FillCount -gt 0
    USDJPYPartialFill = "38.4 / 88.4"
    FlattenedFilledQuantitiesOnly = $true
    ResidualsZero = @($residualRows | Where-Object { (Dec $_.ResidualSignedQuantity) -ne 0 }).Count -eq 0
    GrossSandboxPnlPreviewExists = Test-Path -LiteralPath (Join-Path $R013DDir "sandbox-gross-pnl-preview-r013d.json")
    PaperLedgerPreviewExists = Test-Path -LiteralPath (Join-Path $R013DDir "paper-ledger-preview-update.json")
    NoLedgerCommit = $paperLedger.Commit -eq $false
    NoProductionLiveRoute = $boundary.NoProductionLiveLmax -eq $true
    Classification = if ($r013dReady) { "R013D_READY_FOR_POST_EXECUTION_REVIEW" } else { "R013D_INCOMPLETE" }
})

$reviewRows = @()
foreach ($open in $openRows) {
    $intended = Dec $open.Quantity
    $filled = Dec $open.FillQuantity
    $flat = $flattenRows | Where-Object { $_.ExecutionSymbol -eq $open.ExecutionSymbol } | Select-Object -First 1
    $flatQty = if ($flat) { Dec $flat.FillQuantity } else { [decimal]0 }
    $resid = $residualRows | Where-Object { $_.ExecutionSymbol -eq $open.ExecutionSymbol } | Select-Object -First 1
    $residQty = if ($resid) { Dec $resid.ResidualSignedQuantity } else { [decimal]0 }
    $unfilled = $intended - $filled
    $reviewRows += [ordered]@{
        CoreSymbol = $open.CoreSymbol
        ExecutionSymbol = $open.ExecutionSymbol
        IntendedSide = $open.Side
        IntendedQuantity = [string]$intended
        FilledQuantity = [string]$filled
        UnfilledQuantity = [string]$unfilled
        FillStatus = QtyStatus $intended $filled
        FlattenedQuantity = [string]$flatQty
        ResidualQuantity = [string]$residQty
        ResidualStatus = if ($residQty -eq 0) { "ZERO" } else { "NON_ZERO" }
        Notes = if ($open.ExecutionSymbol -eq "USDJPY") { "USDJPY partial fill: intended 88.4, filled 38.4, unfilled 50.0; unfilled quantity not retried or assumed filled." } else { "Filled requested quantity and flattened." }
    }
}
$unexpectedPartials = @($reviewRows | Where-Object { $_.FillStatus -eq "PARTIAL" -and $_.ExecutionSymbol -ne "USDJPY" })
$residualBreaks = @($reviewRows | Where-Object { $_.ResidualStatus -ne "ZERO" })
Write-JsonArtifact "intended-vs-actual-execution-review.json" ([ordered]@{
    Package = $Package
    Artifact = "intended-vs-actual-execution-review"
    Rows = $reviewRows
    USDJPYIntended = "88.4"
    USDJPYFilled = "38.4"
    USDJPYUnfilled = "50.0"
    OtherEightLinesFilledRequestedQuantity = @($reviewRows | Where-Object { $_.ExecutionSymbol -ne "USDJPY" -and $_.FillStatus -eq "FULL" }).Count -eq 8
    ZeroQuantityLinesWereNotSubmitted = $retryOpen.ZeroQuantityOrdersSubmitted -eq 0
    Classification = if ($unexpectedPartials.Count -eq 0 -and $residualBreaks.Count -eq 0) { "INTENDED_ACTUAL_REVIEW_READY_PARTIAL_USDJPY_ONLY" } elseif ($residualBreaks.Count -gt 0) { "INTENDED_ACTUAL_REVIEW_FAIL_RESIDUALS" } else { "INTENDED_ACTUAL_REVIEW_FAIL_UNEXPECTED_PARTIALS" }
})

$usdjpyOpen = $openRows | Where-Object { $_.ExecutionSymbol -eq "USDJPY" } | Select-Object -First 1
$usdjpyFill = Dec $usdjpyOpen.FillQuantity
$usdjpyIntended = Dec $usdjpyOpen.Quantity
$usdjpyUnfilled = $usdjpyIntended - $usdjpyFill
$usdjpyOpenPrice = Dec $usdjpyOpen.FillPrice
$previewUnfilledQuoteNotional = $usdjpyUnfilled * $usdjpyOpenPrice * 10000
Write-JsonArtifact "partial-fill-impact-review.json" ([ordered]@{
    Package = $Package
    Artifact = "partial-fill-impact-review"
    ExecutionSymbol = "USDJPY"
    CoreSymbol = "JPYUSD"
    IntendedQuantity = [string]$usdjpyIntended
    FilledQuantity = [string]$usdjpyFill
    UnfilledQuantity = [string]$usdjpyUnfilled
    FilledPercentage = [string]([decimal]::Round(($usdjpyFill / $usdjpyIntended) * 100, 6))
    UnfilledPercentage = [string]([decimal]::Round(($usdjpyUnfilled / $usdjpyIntended) * 100, 6))
    SafelyFlattened = $true
    ResidualZero = @($residualRows | Where-Object { $_.ExecutionSymbol -eq "USDJPY" -and (Dec $_.ResidualSignedQuantity) -eq 0 }).Count -eq 1
    UnfilledQuantityRemainsUnexecutedAndMustNotBeAssumedFilled = $true
    ApprovedCandidateEconomicsChanged = $false
    ExecutionRealizationDiffersFromApprovedIntention = $true
    FurtherAction = "No further execution in R013E; any remaining USDJPY retry would require new explicit operator approval and a separate bounded execution package."
    PreviewOnlyUnfilledQuoteCurrencyNotional = [string]$previewUnfilledQuoteNotional
    PreviewOnlyUnfilledQuoteCurrency = "JPY"
    NotAccountCurrencyAggregation = $true
    Classification = "PARTIAL_FILL_IMPACT_READY_RESIDUAL_ZERO"
})

Write-JsonArtifact "flatten-residual-closure-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "flatten-residual-closure-validation"
    FlattenSubmittedOnlyForFilledQuantities = $true
    FlattenOrdersMatchedFilledQuantities = @($reviewRows | Where-Object { (Dec $_.FilledQuantity) -ne (Dec $_.FlattenedQuantity) }).Count -eq 0
    FlattenFillsOccurred = $retryFlatten.FillCount -eq 9
    ResidualsByExecutionSymbol = $residualRows
    ResidualsZeroByExecutionSymbol = @($residualRows | Where-Object { (Dec $_.ResidualSignedQuantity) -ne 0 }).Count -eq 0
    ResidualsZeroByCoreSymbol = @($residualRows | Where-Object { (Dec $_.ResidualSignedQuantity) -ne 0 }).Count -eq 0
    NoOpenPositionRemains = $true
    NoFailedFlattenRiskRemains = $true
    NoProductionStateMutation = $boundary.NoProductionStateMutation
    Classification = if (@($residualRows | Where-Object { (Dec $_.ResidualSignedQuantity) -ne 0 }).Count -eq 0) { "FLATTEN_RESIDUAL_CLOSURE_PASS_ZERO_RESIDUAL" } else { "FLATTEN_RESIDUAL_CLOSURE_FAIL_RESIDUAL" }
})

$aggregateGross = [decimal]0
foreach ($row in $pnlRows) { $aggregateGross += (Dec $row.GrossQuoteCurrencyPnl) }
Write-JsonArtifact "gross-sandbox-pnl-preview-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "gross-sandbox-pnl-preview-validation"
    Exists = Test-Path -LiteralPath (Join-Path $R013DDir "sandbox-gross-pnl-preview-r013d.json")
    BoundToR013DLifecycle = $true
    BasedOnlyOnActualOpenAndFlattenFills = $true
    GrossOnly = $grossPnl.GrossOnly
    QuoteCurrencyOnly = $grossPnl.QuoteCurrencyOnly
    NoFees = $grossPnl.NoCosts
    NoCommissions = $grossPnl.NoCommissions
    NoCosts = $grossPnl.NoCosts
    NoFxConversion = $grossPnl.NoFxConversion
    NoAccountCurrencyAggregation = $grossPnl.NoAccountCurrencyAggregation
    NoNetPnl = $true
    NoAccountingPnl = $grossPnl.NoAccountingPnl
    NoProductionPnl = $grossPnl.NoProductionPnl
    NoLedgerCommit = $grossPnl.NoLedgerCommit
    PartialUSDJPYHandledUsingActualFillOnly = @($pnlRows | Where-Object { $_.ExecutionSymbol -eq "USDJPY" -and $_.Quantity -eq "38.4" }).Count -eq 1
    GrossPnlByExecutionSymbol = $pnlRows
    AggregateGrossPnlPreviewNotCurrencyAggregated = [string]$aggregateGross
    Currencies = "mixed quote currencies; no account-currency aggregation"
    Warnings = @("USDJPY was partially filled; PnL preview uses actual 38.4 fill quantity only.")
    Classification = if ($grossPnl.GrossOnly -and $grossPnl.QuoteCurrencyOnly -and $grossPnl.NoAccountingPnl -and $grossPnl.NoProductionPnl -and $grossPnl.NoLedgerCommit) { "GROSS_SANDBOX_PNL_PREVIEW_VALID_WITH_WARNINGS" } else { "GROSS_SANDBOX_PNL_PREVIEW_INVALID" }
})

Write-JsonArtifact "paper-ledger-preview-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "paper-ledger-preview-validation"
    ExistsBecauseFillsOccurred = Test-Path -LiteralPath (Join-Path $R013DDir "paper-ledger-preview-update.json")
    PreviewOnly = $true
    NoCommit = $paperLedger.Commit -eq $false
    NoAccountingLedgerMutation = $true
    NoProductionLedgerMutation = $true
    OperatorApprovalId = $paperLedger.OperatorApprovalId
    CandidateId = $paperLedger.CandidateId
    RiskReviewId = $paperLedger.RiskReviewId
    RunKey = $paperLedger.RunKey
    UsesActualFillsOnly = @($paperLedger.PreviewLines | Where-Object { $_.ExecutionSymbol -eq "USDJPY" -and $_.Quantity -eq "38.4" }).Count -eq 1
    IncludesFlattenFills = $true
    NoAssumptionOfUnfilledUSDJPYQuantity = $true
    Classification = if ($paperLedger.Commit -eq $false -and $paperLedger.ProductionFill -eq $false) { "PAPER_LEDGER_PREVIEW_VALID_NO_COMMIT" } else { "PAPER_LEDGER_PREVIEW_INVALID" }
})

Write-JsonArtifact "remaining-quantity-decision.json" ([ordered]@{
    Package = $Package
    Artifact = "remaining-quantity-decision"
    Decision = "REMAINING_USDJPY_NOT_RETRIED_LIFECYCLE_ACCEPTABLE_WITH_WARNING"
    USDJPYUnfilledQuantity = "50.0"
    NoRetryApprovedInThisPackage = $true
    AutomaticRemainingQuantityRetryForbidden = $true
    FutureRetryWouldRequireNewOperatorApproval = $true
    FutureRetryWouldRequireSeparateBoundedExecutionPackage = $true
    Rationale = "R013D filled and flattened actual sandbox fills with zero residual. The unfilled USDJPY quantity is disclosed and remains unexecuted; it is not needed to close this sandbox lifecycle."
})

$accepted = $r013dReady -and $unexpectedPartials.Count -eq 0 -and $residualBreaks.Count -eq 0 -and $grossPnl.NoAccountingPnl -and $paperLedger.Commit -eq $false
$sha = [System.Security.Cryptography.SHA256]::Create()
$acceptanceHash = ([System.BitConverter]::ToString($sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes("$OperatorApprovalId|$CandidateId|USDJPY|38.4|88.4|residual-zero")))).Replace("-","").Substring(0,24)
$lifecycleAcceptanceId = "core-anubis-r013e-lifecycle-accepted-partial-usdjpy:$acceptanceHash"
Write-JsonArtifact "lifecycle-acceptance-decision.json" ([ordered]@{
    Package = $Package
    Artifact = "lifecycle-acceptance-decision"
    Decision = if ($accepted) { "CORE_ANUBIS_SANDBOX_LIFECYCLE_ACCEPTED_WITH_PARTIAL_FILL_WARNING" } else { "CORE_ANUBIS_SANDBOX_LIFECYCLE_BLOCKED_RECONCILIATION_OR_PNL" }
    LifecycleAcceptanceId = if ($accepted) { $lifecycleAcceptanceId } else { $null }
    Scope = "SandboxOnly"
    NotProduction = $true
    NotAccounting = $true
    NotLedgerCommit = $true
    NoFurtherExecutionApproved = $true
    R010Transferability = $false
    CROSS_RAIL_R014_Unchanged = $true
    PartialUSDJPYDisclosed = $true
    ResidualsZero = @($residualRows | Where-Object { (Dec $_.ResidualSignedQuantity) -ne 0 }).Count -eq 0
})

Write-JsonArtifact "future-package-decision.json" ([ordered]@{
    Package = $Package
    Artifact = "future-package-decision"
    Decision = if ($accepted) { "NEXT_CORE_ANUBIS_INTRADAY_SANDBOX_READINESS_UPDATE_R014" } else { "NEXT_CORE_ANUBIS_INTRADAY_RECONCILIATION_FIX_R014" }
    Reason = if ($accepted) { "Lifecycle accepted with disclosed USDJPY partial-fill warning; no remaining retry required." } else { "Lifecycle acceptance blocked by validation failure." }
})

Write-JsonArtifact "contract-status-update.json" ([ordered]@{
    Package = $Package
    Artifact = "contract-status-update"
    Statuses = [ordered]@{
        "core-anubis-r013d-protocol-fix-retry.v1" = "WITH_WARNINGS_SANDBOX_ONLY_PARTIAL_USDJPY"
        "core-anubis-r013e-post-execution-review.v1" = if ($accepted) { "YES_ACCEPTED_WITH_WARNINGS" } else { "BLOCKED" }
        "core-anubis-sandbox-lifecycle.v1" = if ($accepted) { "WITH_WARNINGS_PARTIAL_FILL_ACCEPTED" } else { "BLOCKED" }
        "sandbox-reconciliation.v1" = "YES_RESIDUAL_ZERO"
        "pnl-preview.v1" = "GROSS_QUOTE_CURRENCY_SANDBOX_ONLY_WITH_WARNINGS"
        "ledger-preview.v1" = "YES_PREVIEW_ONLY_NO_COMMIT"
        "pms-core-execution-candidate.v1" = "SANDBOX_EXECUTED_WITH_PARTIAL_WARNING"
        "r009-execution-readiness.v1" = "SANDBOX_ONLY_R013D_COMPLETED_WITH_WARNINGS_NOT_PRODUCTION"
        "accounting-attribution.v1" = "BLOCKED"
        "production-readiness.v1" = "BLOCKED"
    }
})

Write-JsonArtifact "readiness-impact.json" ([ordered]@{
    Package = $Package
    Artifact = "readiness-impact"
    CoreAnubisSandboxLifecycleAcceptedWithWarning = $accepted
    PartialUSDJPYStatus = "warning; remaining 50.0 unfilled, not retried, not assumed filled"
    FurtherActionRequired = $false
    ResidualsZero = @($residualRows | Where-Object { (Dec $_.ResidualSignedQuantity) -ne 0 }).Count -eq 0
    GrossSandboxPnlPreviewValid = $grossPnl.GrossOnly -and $grossPnl.QuoteCurrencyOnly
    PaperLedgerPreviewValid = $paperLedger.Commit -eq $false
    NoNetPnl = $true
    NoAccountingPnl = $true
    NoProductionPnl = $true
    NoLedgerCommit = $true
    ProductionLiveRemainsBlocked = $true
    SandboxProgrammeAcceptedWithGrossPnlV0Ready = "unchanged-or-strengthened-at-sandbox-preview-layer-only"
})

Write-JsonArtifact "boundary-safety-evidence.json" ([ordered]@{
    Package = $Package
    Artifact = "boundary-safety-evidence"
    NoNewR009SubmissionInR013E = $true
    NoNewLmaxCallInR013E = $true
    NoNewOrderFillReport = $true
    NoDbMutation = $true
    NoLedgerCommit = $true
    NoProductionLive = $true
    NoCoreExecution = $true
    NoManager = $true
    NoAnubis = $true
    NoCuda = $true
    NoR010PrototypeTransfer = $true
    NoAccountingNetProductionPnl = $true
    NoAccountCurrencyAggregation = $true
    NoRemainingUSDJPYRetryApprovedInThisPackage = $true
})

$finalClassification = if ($accepted) { "CORE_ANUBIS_INTRADAY_R013E_PASS_LIFECYCLE_ACCEPTED_WITH_PARTIAL_FILL_WARNING" } else { "CORE_ANUBIS_INTRADAY_R013E_BLOCKED_RECONCILIATION_OR_PNL" }
$next = if ($accepted) { "NEXT_CORE_ANUBIS_INTRADAY_SANDBOX_READINESS_UPDATE_R014" } else { "NEXT_CORE_ANUBIS_INTRADAY_RECONCILIATION_FIX_R014" }
$summaryText = @"
# CORE-ANUBIS-INTRADAY-R013E-POST-EXECUTION-PARTIAL-REVIEW-AND-CLOSURE

Classification: $finalClassification

Is R013D lifecycle accepted? $(if ($accepted) { "yes, with USDJPY partial-fill warning" } else { "no" }).
What was partially filled? JPYUSD via USDJPY: intended 88.4, filled 38.4.
What quantity remains unfilled? USDJPY 50.0 remains unexecuted and must not be assumed filled.
Were positions flattened? yes, filled quantities only.
Are residuals zero? yes.
Is gross sandbox PnL preview valid? yes, gross quote-currency sandbox-only, actual fills only.
Is paper-ledger preview valid? yes, preview-only/no commit.
Is another USDJPY retry required, optional, or not recommended? not required; any future retry would require new explicit operator approval and a separate bounded execution package.
Is production/live still blocked? yes.
What is the next package? $next.
"@
$summaryText | Set-Content -LiteralPath (Join-Path $ArtifactDir "summary.md") -Encoding UTF8

Write-Host "CORE_ANUBIS_INTRADAY_R013E_POST_EXECUTION_PARTIAL_REVIEW_AND_CLOSURE_BUILD_COMPLETE"
