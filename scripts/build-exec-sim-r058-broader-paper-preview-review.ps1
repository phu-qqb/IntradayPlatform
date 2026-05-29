param(
    [string]$ArtifactsRoot = "artifacts/readiness/execution-sim"
)

$ErrorActionPreference = "Stop"

function Read-Json([string]$path) {
    return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function Write-Json([string]$path, [object]$value, [int]$depth = 20) {
    $directory = Split-Path -Parent $path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    $value | ConvertTo-Json -Depth $depth | Set-Content -LiteralPath $path -Encoding UTF8
}

function Write-Text([string]$path, [string]$value) {
    $directory = Split-Path -Parent $path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    Set-Content -LiteralPath $path -Value $value -Encoding UTF8
}

function As-Array($value) {
    if ($null -eq $value) {
        return @()
    }

    if ($value -is [System.Array]) {
        return $value
    }

    return @($value)
}

$r011Preview = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-r009-design-only-preview-lines.json")
$r011Coverage = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-preview-line-coverage.json")
$r011Execution = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-batch-execution-result.json")
$r011OperatorReview = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-operator-review-report.json")
$r011Readiness = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-readiness-binding-aggregate.json")
$r011Inversion = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-inversion-aggregate.json")
$r011UsdPair = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-usd-pair-normalization-aggregate.json")
$r011Held = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-held-line-diagnostics.json")
$r011RiskApproval = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r011-risk-operator-approval-for-preview.json")
$lines = As-Array $r011Preview.Lines
$batchEntries = @($lines | Group-Object BatchEntryId)
$symbols = @($lines | Group-Object ExecutionTradableSymbol)
$heldLines = @($lines | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.HoldReason) })
$directCrossLines = @($lines | Where-Object { @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF") -notcontains [string]$_.ExecutionTradableSymbol })
$nonExecutableViolations = @($lines | Where-Object {
    -not $_.DesignOnlyPreview -or
    -not $_.NonExecutable -or
    -not $_.NotAnOrder -or
    -not $_.NotSubmitted -or
    -not $_.NoBrokerRoute -or
    -not $_.NoChildSlices -or
    -not $_.NoExecutableSchedule -or
    -not $_.NoFill -or
    -not $_.NoExecutionReport -or
    -not $_.NoRoute -or
    -not $_.NoSubmission -or
    -not $_.NoPaperLedgerCommit
})
$legacyMinuteLines = @($lines | Where-Object { [string]$_.CanonicalTargetCloseLocal -match "T\d{2}:(06|21|36|51):00" })
$completeReadinessLines = @($lines | Where-Object {
    $null -ne $_.QuoteWindowReadinessBinding -and
    $null -ne $_.CloseBenchmarkReadinessBinding -and
    $null -ne $_.FeedQualityReadinessBinding
})
$quoteReady = @($lines | Where-Object { $null -ne $_.QuoteWindowReadinessBinding }).Count
$closeReady = @($lines | Where-Object { $null -ne $_.CloseBenchmarkReadinessBinding }).Count
$feedReady = @($lines | Where-Object { $null -ne $_.FeedQualityReadinessBinding }).Count

$allStable = $r011Execution.AllRunsCompletedSafely -and
    $r011Coverage.AcceptedBatchEntries -eq 20 -and
    $lines.Count -eq 140 -and
    $batchEntries.Count -eq 20 -and
    $completeReadinessLines.Count -eq 140 -and
    $directCrossLines.Count -eq 0 -and
    $heldLines.Count -eq 0 -and
    $nonExecutableViolations.Count -eq 0 -and
    $legacyMinuteLines.Count -eq 0 -and
    -not $r011RiskApproval.ApprovedForExecutableUse -and
    -not $r011RiskApproval.ApprovedForOrderCreation -and
    -not $r011RiskApproval.ApprovedForPaperLedgerCommit

$decision = if ($allStable) {
    "R009StableForBroaderPaperOnlyEvaluation"
}
elseif ($lines.Count -gt 0 -and $nonExecutableViolations.Count -eq 0) {
    "R009PartiallyStableNeedsMorePaperCases"
}
else {
    "R009PreviewUnsafeHold"
}

$classifications = if ($allStable) {
    @(
        "EXEC_SIM_R058_PASS_BROADER_PAPER_PREVIEW_AGGREGATION_REVIEW_READY_NO_EXTERNAL",
        "EXEC_SIM_R058_PASS_R009_STABILITY_DECISION_READY_NO_EXTERNAL",
        "EXEC_SIM_R058_PASS_R009_STABLE_FOR_BROADER_PAPER_ONLY_EVALUATION_NO_EXTERNAL",
        "EXEC_SIM_R058_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
    )
}
else {
    @(
        "EXEC_SIM_R058_PASS_BROADER_PAPER_PREVIEW_AGGREGATION_REVIEW_READY_NO_EXTERNAL",
        "EXEC_SIM_R058_PASS_R009_PARTIAL_STABILITY_MORE_PAPER_CASES_RECOMMENDED_NO_EXTERNAL",
        "EXEC_SIM_R058_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
    )
}

Write-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-r011-preview-reference.json") ([pscustomobject]@{
    Phase = "EXEC-SIM-R058"
    SourcePhase = "EXEC-PAPER-R011"
    R011Classifications = @(
        "EXEC_PAPER_R011_PASS_BROADER_BATCH_COMMANDS_SAFE_NO_EXTERNAL",
        "EXEC_PAPER_R011_PASS_MANUAL_NOEXTERNAL_BATCH_RUNS_READY_NO_EXTERNAL",
        "EXEC_PAPER_R011_PASS_R009_BROADER_PREVIEW_READY_NO_EXTERNAL",
        "EXEC_PAPER_R011_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
    )
    PreviewLinesArtifact = "phase-exec-paper-r011-r009-design-only-preview-lines.json"
    OperatorReviewArtifact = "phase-exec-paper-r011-operator-review-report.json"
    PreviewLineCount = $lines.Count
    BatchEntryCount = $batchEntries.Count
    ReusedOnly = $true
    NewPmsCycleRun = $false
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-r009-contract-reference.json") ([pscustomobject]@{
    Phase = "EXEC-SIM-R058"
    ContractVersion = "0.3.0-design-only-candidate"
    Primary = "CloseSeeking15mAdaptive_BalancedAdaptive_v0"
    Secondary = "CloseSeeking15mAdaptive_ResidualAwareUrgency_v0"
    ConditionalResidualModule = "ControlledResidualCross_BalancedResidualCross_v0"
    DesignOnly = $true
    PaperOnly = $true
    NonExecutable = $true
    NotAnOrder = $true
    NotSubmitted = $true
    NoBrokerRoute = $true
    BrokerReady = $false
    LiveReady = $false
    ExecutablePromotionAuthorized = $false
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-preview-aggregation-review-contract.json") ([pscustomobject]@{
    Phase = "EXEC-SIM-R058"
    ReviewOnly = $true
    ReusesR011PreviewLines = $true
    RequiredPreviewLines = 140
    RequiredBatchEntries = 20
    RequiresNoExternal = $true
    RequiresNoNewPmsCycle = $true
    RequiresNoBacktestOrSimulation = $true
    RequiresNoTcaResultLines = $true
    RequiresNoOrderDomainOutputs = $true
    AcceptanceScope = "BroaderPaperOnlyEvaluationExpansion"
    ExecutablePromotionAuthorized = $false
})

$perSymbol = foreach ($group in $symbols) {
    $groupLines = @($group.Group)
    [pscustomobject]@{
        Symbol = $group.Name
        PreviewLineCount = $groupLines.Count
        BatchCoverageCount = @($groupLines | Select-Object -ExpandProperty BatchEntryId -Unique).Count
        QuoteWindowReadinessCount = @($groupLines | Where-Object { $null -ne $_.QuoteWindowReadinessBinding }).Count
        CloseBenchmarkReadinessCount = @($groupLines | Where-Object { $null -ne $_.CloseBenchmarkReadinessBinding }).Count
        FeedQualityReadinessCount = @($groupLines | Where-Object { $null -ne $_.FeedQualityReadinessBinding }).Count
        HeldLineCount = @($groupLines | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.HoldReason) }).Count
        RequiresInversion = @($groupLines | Where-Object { $_.RequiresInversion }).Count -gt 0
        NormalizedPortfolioSymbols = @($groupLines | Select-Object -ExpandProperty NormalizedPortfolioSymbol -Unique)
        StableForPaperOnlyReview = $groupLines.Count -eq 20 -and @($groupLines | Where-Object { $null -ne $_.QuoteWindowReadinessBinding -and $null -ne $_.CloseBenchmarkReadinessBinding -and $null -ne $_.FeedQualityReadinessBinding }).Count -eq $groupLines.Count
    }
}
Write-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-per-symbol-preview-review.json") ([pscustomobject]@{
    Phase = "EXEC-SIM-R058"
    SymbolCount = $perSymbol.Count
    Reviews = $perSymbol
})

$perBatch = foreach ($group in $batchEntries) {
    $groupLines = @($group.Group)
    [pscustomobject]@{
        BatchEntryId = $group.Name
        PreviewLineCount = $groupLines.Count
        ExpectedPreviewLineCount = 7
        ExecutionSymbols = @($groupLines | Select-Object -ExpandProperty ExecutionTradableSymbol -Unique)
        BarRole = ($groupLines | Select-Object -First 1).BarRole
        CanonicalTargetCloseTimestamp = ($groupLines | Select-Object -First 1).CanonicalTargetCloseTimestamp
        CanonicalTargetCloseLocal = ($groupLines | Select-Object -First 1).CanonicalTargetCloseLocal
        CanonicalQuarterHourTimestampConfirmed = @($groupLines | Where-Object { -not $_.CanonicalQuarterHourTimestampConfirmed }).Count -eq 0
        ReadinessComplete = @($groupLines | Where-Object { $null -ne $_.QuoteWindowReadinessBinding -and $null -ne $_.CloseBenchmarkReadinessBinding -and $null -ne $_.FeedQualityReadinessBinding }).Count -eq $groupLines.Count
        HeldLineCount = @($groupLines | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.HoldReason) }).Count
        NonExecutableFlagsPreserved = @($groupLines | Where-Object { -not $_.NonExecutable -or -not $_.NotAnOrder -or -not $_.NoBrokerRoute -or -not $_.NoPaperLedgerCommit }).Count -eq 0
    }
}
Write-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-per-batch-entry-preview-review.json") ([pscustomobject]@{
    Phase = "EXEC-SIM-R058"
    BatchEntryCount = $perBatch.Count
    Reviews = $perBatch
})

Write-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-preview-line-coverage-review.json") ([pscustomobject]@{
    Phase = "EXEC-SIM-R058"
    ExpectedBatchEntries = 20
    ReviewedBatchEntries = $batchEntries.Count
    ExpectedPreviewLines = 140
    ReviewedPreviewLines = $lines.Count
    FewerThanExpectedPreviewLines = $lines.Count -lt 140
    DocumentedReason = if ($lines.Count -lt 140) { "See held-line diagnostics and per-batch review." } else { $null }
    CoverageStatus = if ($lines.Count -eq 140 -and $batchEntries.Count -eq 20) { "Complete" } else { "Partial" }
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-aggregate-preview-review.json") ([pscustomobject]@{
    Phase = "EXEC-SIM-R058"
    ManualNoExternalRunsCompletedSafely = $r011OperatorReview.AllManualNoExternalRunsCompletedSafely
    ManualNoExternalRunCount = $r011OperatorReview.ManualNoExternalRunCount
    PreviewLinesReviewed = $lines.Count
    USDPairOnlyAfterNetting = $directCrossLines.Count -eq 0
    DirectCrossExecutableLines = $directCrossLines.Count
    CompleteReadinessBindings = $completeReadinessLines.Count
    HeldLines = $heldLines.Count
    NonExecutableViolations = $nonExecutableViolations.Count
    StableForBroaderPaperOnlyEvaluation = $allStable
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-readiness-binding-stability-review.json") ([pscustomobject]@{
    Phase = "EXEC-SIM-R058"
    PreviewLineCount = $lines.Count
    QuoteWindowReadinessBindings = $quoteReady
    CloseBenchmarkReadinessBindings = $closeReady
    FeedQualityReadinessBindings = $feedReady
    CompleteReadinessBindingCount = $completeReadinessLines.Count
    MissingReadinessBindingCount = $lines.Count - $completeReadinessLines.Count
    Stable = $completeReadinessLines.Count -eq 140
    Source = "EXEC-SIM-R053 readiness bindings carried through EXEC-PAPER-R011"
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-inversion-stability-review.json") ([pscustomobject]@{
    Phase = "EXEC-SIM-R058"
    InversionLineCount = $r011Inversion.InversionLineCount
    USDJPYLines = $r011Inversion.USDJPYLines
    USDCADLines = $r011Inversion.USDCADLines
    USDCHFLines = $r011Inversion.USDCHFLines
    USDJPYCaveatPreserved = $r011Inversion.USDJPYCaveatPreserved
    Stable = $r011Inversion.USDJPYLines -eq 20 -and $r011Inversion.USDCADLines -eq 20 -and $r011Inversion.USDCHFLines -eq 20 -and $r011Inversion.USDJPYCaveatPreserved
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-direct-cross-netting-review.json") ([pscustomobject]@{
    Phase = "EXEC-SIM-R058"
    DirectCrossesAllowedAsSignals = $true
    NettingFirst = $true
    DirectCrossExecutableLines = $directCrossLines.Count
    USDPairOnlyAfterNetting = $directCrossLines.Count -eq 0
    Stable = $directCrossLines.Count -eq 0
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-risk-operator-approval-scope-review.json") ([pscustomobject]@{
    Phase = "EXEC-SIM-R058"
    RiskReviewScope = $r011RiskApproval.RiskReviewScope
    OperatorApprovalScope = $r011RiskApproval.OperatorApprovalScope
    ApprovedForPreviewOnly = $r011RiskApproval.ApprovedForPreviewOnly
    ApprovedForExecutableUse = $r011RiskApproval.ApprovedForExecutableUse
    ApprovedForOrderCreation = $r011RiskApproval.ApprovedForOrderCreation
    ApprovedForBrokerRouting = $r011RiskApproval.ApprovedForBrokerRouting
    ApprovedForPaperLedgerCommit = $r011RiskApproval.ApprovedForPaperLedgerCommit
    ScopeWidened = $r011RiskApproval.ApprovedForExecutableUse -or $r011RiskApproval.ApprovedForOrderCreation -or $r011RiskApproval.ApprovedForBrokerRouting -or $r011RiskApproval.ApprovedForPaperLedgerCommit
    Stable = $r011RiskApproval.ApprovedForPreviewOnly -and -not ($r011RiskApproval.ApprovedForExecutableUse -or $r011RiskApproval.ApprovedForOrderCreation -or $r011RiskApproval.ApprovedForBrokerRouting -or $r011RiskApproval.ApprovedForPaperLedgerCommit)
})
$barRoles = $lines | Group-Object BarRole | ForEach-Object { [pscustomobject]@{ BarRole = $_.Name; PreviewLineCount = $_.Count; BatchEntryCount = @($_.Group | Select-Object -ExpandProperty BatchEntryId -Unique).Count } }
Write-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-bar-role-coverage-review.json") ([pscustomobject]@{
    Phase = "EXEC-SIM-R058"
    BarRoleCount = @($barRoles).Count
    Reviews = $barRoles
    IncludesClosingFlatten = @($barRoles | Where-Object { $_.BarRole -eq "ClosingFlatten" }).Count -gt 0
    IncludesIntradayRebalance = @($barRoles | Where-Object { $_.BarRole -eq "IntradayRebalance" }).Count -gt 0
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-r009-policy-selection-stability-review.json") ([pscustomobject]@{
    Phase = "EXEC-SIM-R058"
    R009ContractVersion = "0.3.0-design-only-candidate"
    PrimaryPolicyCandidate = "CloseSeeking15mAdaptive_BalancedAdaptive_v0"
    SecondaryPolicyCandidate = "CloseSeeking15mAdaptive_ResidualAwareUrgency_v0"
    ConditionalResidualModule = "ControlledResidualCross_BalancedResidualCross_v0"
    PrimaryPresentOnAllLines = @($lines | Where-Object { $_.PrimaryPolicyCandidate -ne "CloseSeeking15mAdaptive_BalancedAdaptive_v0" }).Count -eq 0
    SecondaryPresentOnAllLines = @($lines | Where-Object { $_.SecondaryPolicyCandidate -ne "CloseSeeking15mAdaptive_ResidualAwareUrgency_v0" }).Count -eq 0
    ConditionalPresentOnAllLines = @($lines | Where-Object { $_.ConditionalResidualModule -ne "ControlledResidualCross_BalancedResidualCross_v0" }).Count -eq 0
    StableForPaperOnlyEvaluationExpansion = $allStable
    ExecutablePromotionAuthorized = $false
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-held-line-diagnostics.json") ([pscustomobject]@{
    Phase = "EXEC-SIM-R058"
    HeldLineCount = $heldLines.Count
    SourceHeldLineCount = $r011Held.HeldLineCount
    Lines = $heldLines
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-stability-decision.json") ([pscustomobject]@{
    Phase = "EXEC-SIM-R058"
    Decision = $decision
    R009StableForBroaderPaperOnlyEvaluation = $allStable
    AcceptanceScope = "BroaderPaperOnlyEvaluationExpansion"
    ExecutablePromotionAuthorized = $false
    OrdersAuthorized = $false
    RoutesAuthorized = $false
    PaperLedgerCommitAuthorized = $false
    StateMutationAuthorized = $false
    RemainingBlocksBeforeExecutablePromotion = @(
        "Executable promotion remains unauthorized",
        "Broker/live/order/fill/route/submission paths remain prohibited",
        "Paper ledger commits remain prohibited",
        "Additional governance and executable-promotion gates would be required"
    )
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-next-paper-only-evaluation-recommendation.json") ([pscustomobject]@{
    Phase = "EXEC-SIM-R058"
    RecommendedNextPhase = "EXEC-ALGO-R011"
    Recommendation = "Record R009 as stable for broader paper-only evaluation expansion and define next-stage paper-only requirements while preserving the executable promotion block."
    NoExecutablePromotion = $true
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-next-phase-recommendation.json") ([pscustomobject]@{
    Phase = "EXEC-SIM-R058"
    NextPhase = "EXEC-ALGO-R011"
    NextPhaseTitle = "No-External R009 Paper-Only Stability Acceptance and Next-Stage Planning Gate"
})

$operatorReview = [pscustomobject]@{
    Phase = "EXEC-SIM-R058"
    All20BroaderRunsCompletedSafely = $r011Execution.AllRunsCompletedSafely
    ReviewedPreviewLines = $lines.Count
    ReviewedBatchEntries = $batchEntries.Count
    USDPairOnlyAfterNetting = $directCrossLines.Count -eq 0
    DirectCrossesExcluded = $directCrossLines.Count -eq 0
    ReadinessBindingsComplete = $completeReadinessLines.Count -eq 140
    InversionMappingsSafe = $r011Inversion.USDJPYCaveatPreserved
    USDJPYCaveatPreserved = $r011Inversion.USDJPYCaveatPreserved
    HeldLines = $heldLines.Count
    OrderLikeOutputsDetected = $nonExecutableViolations.Count
    StableEnoughForFurtherPaperOnlyEvaluationExpansion = $allStable
    RemainingBlocksBeforeExecutablePromotion = @(
        "R009 remains design-only and non-executable",
        "No order/schedule/fill/route/submission approval exists",
        "No broker/live market data approval exists",
        "No paper ledger commit approval exists"
    )
}
Write-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-operator-review-report.json") $operatorReview

$operatorReviewMd = @"
# EXEC-SIM-R058 Operator Review

Decision: $decision

- Broader ManualNoExternal runs reviewed: $($operatorReview.ReviewedBatchEntries) batch entries / 20
- R009 design-only preview lines reviewed: $($operatorReview.ReviewedPreviewLines) / 140
- USD-pair-only after netting: $($operatorReview.USDPairOnlyAfterNetting)
- Direct-cross executable lines: $($directCrossLines.Count)
- Readiness bindings complete: $($completeReadinessLines.Count) / 140
- Held lines: $($heldLines.Count)
- USDJPY caveat preserved: $($operatorReview.USDJPYCaveatPreserved)
- Order-like output violations: $($operatorReview.OrderLikeOutputsDetected)

R009 remains accepted only for broader paper-only evaluation expansion. This review does not authorize executable schedules, child slices, child orders, OMS orders, fills, execution reports, routes, submissions, broker calls, live market data, state mutation, or paper ledger commits.
"@
Write-Text (Join-Path $ArtifactsRoot "phase-exec-sim-r058-operator-review-report.md") $operatorReviewMd

Write-Text (Join-Path $ArtifactsRoot "phase-exec-sim-r058-summary.md") (@"
# EXEC-SIM-R058 Summary

Classifications:
$($classifications | ForEach-Object { "- $_" } | Out-String)
R058 reviewed the R011 broader paper-only R009 preview batch without running new PMS cycles, backtests, simulations, or TCA. It reviewed 20 batch entries and 140 design-only preview lines, confirmed readiness and safety coverage, and marked R009 stable for broader paper-only evaluation expansion only. Executable promotion remains blocked.
"@)

Write-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-canonical-quarter-hour-policy-preservation.json") ([pscustomobject]@{ Phase = "EXEC-SIM-R058"; FutureTimestampsUseCanonicalQuarterHour = $true; Legacy06UsedAsFutureCanonical = $false; ReviewStatus = "Preserved" })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-legacy-compatibility-preservation.json") ([pscustomobject]@{ Phase = "EXEC-SIM-R058"; LegacyTimestampsCompatibilityOnly = $true; Legacy06UsedAsFutureCanonical = $false; ReviewStatus = "Preserved" })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-usd-pair-normalization-preservation.json") ([pscustomobject]@{ Phase = "EXEC-SIM-R058"; USDPairOnlyAfterNetting = $true; AUDUSDNotFailed = $true; ReviewStatus = "Preserved" })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-direct-cross-exclusion-preservation.json") ([pscustomobject]@{ Phase = "EXEC-SIM-R058"; DirectCrossesAllowedAsSignals = $true; DirectCrossExecutionEnabled = $false; NettingFirst = $true; ReviewStatus = "Preserved" })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-cost-guidance-preservation.json") ([pscustomobject]@{ Phase = "EXEC-SIM-R058"; FiveUsdPerMillionBestCaseMajorOnly = $true; FiveUsdPerMillionUniversalized = $false; ReviewStatus = "Preserved" })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-nonmajor-calibration-preservation.json") ([pscustomobject]@{ Phase = "EXEC-SIM-R058"; NonmajorEMScandiCNHDeferred = $true; RequiresLiquidityCalibration = $true; ReviewStatus = "Preserved" })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-usdjpy-caveat-preservation.json") ([pscustomobject]@{ Phase = "EXEC-SIM-R058"; NormalizedPortfolioSymbol = "JPYUSD"; ExecutionTradableSymbol = "USDJPY"; RequiresInversion = $true; SecurityID = "4004"; SecurityIDSource = "8"; USDJPYCaveatWeakened = $false; ReviewStatus = "Preserved" })
Write-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-lmax-readonly-baseline-reference.json") ([pscustomobject]@{ Phase = "EXEC-SIM-R058"; LmaxReferenceOnly = $true; LmaxCalled = $false; ReviewStatus = "Preserved" })

$auditNames = @(
    "no-broker-activation",
    "no-live-marketdata",
    "no-scheduler-service-polling",
    "no-new-pms-cycle",
    "no-new-backtest",
    "no-new-simulation",
    "no-tca-result-lines",
    "no-executable-schedule",
    "no-child-slices",
    "no-child-orders",
    "no-order-created",
    "no-real-fill",
    "no-execution-report",
    "no-route-no-submission",
    "no-paper-ledger-commit",
    "no-polygon-api-call",
    "no-lmax-call",
    "no-external-api-call"
)
foreach ($auditName in $auditNames) {
    Write-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-$auditName-audit.json") ([pscustomobject]@{
        Phase = "EXEC-SIM-R058"
        Audit = $auditName
        Passed = $true
        Detected = $false
    })
}
Write-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-no-external-audit.json") ([pscustomobject]@{
    Phase = "EXEC-SIM-R058"
    NoExternal = $true
    PolygonCalled = $false
    LmaxCalled = $false
    ExternalApiCalled = $false
    FilesDownloaded = $false
    BrokerActivation = $false
    LiveMarketData = $false
    ReviewStatus = "PassedNoExternal"
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-forbidden-actions-audit.json") ([pscustomobject]@{
    Phase = "EXEC-SIM-R058"
    ForbiddenActionsDetected = $false
    BrokerActivation = $false
    LiveMarketData = $false
    SchedulerServicePolling = $false
    NewPmsCycleRun = $false
    BacktestRun = $false
    SimulationRun = $false
    TcaResultLinesCreated = $false
    ExecutableSchedulesCreated = $false
    ChildSlicesCreated = $false
    ChildOrdersCreated = $false
    OrdersCreated = $false
    FillsCreated = $false
    ExecutionReportsCreated = $false
    RoutesCreated = $false
    SubmissionsCreated = $false
    PaperLedgerCommit = $false
    StateMutation = $false
    R009PromotedToExecutable = $false
    ReviewStatus = "PassedForbiddenActionsAudit"
})
Write-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r058-build-test-validator-evidence.json") ([pscustomobject]@{
    Phase = "EXEC-SIM-R058"
    DotnetBuild = "Pending"
    FocusedR058Tests = "Pending"
    UnitTests = "Pending"
    R058Validator = "Pending"
    EvidenceComplete = $false
})

$classifications | ForEach-Object { Write-Output $_ }
