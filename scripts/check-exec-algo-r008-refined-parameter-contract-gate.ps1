param(
    [string]$ArtifactsRoot = "artifacts/readiness/execution-algo"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Error "EXEC-ALGO-R008 validation failed: $Message"
    exit 1
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing artifact: $Path"
    }
    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Assert-FalseField($Object, [string]$Field, [string]$Message) {
    if ($null -eq $Object.$Field) {
        Fail "Missing field $Field in $Message"
    }
    if ([bool]$Object.$Field) {
        Fail $Message
    }
}

function Assert-TrueField($Object, [string]$Field, [string]$Message) {
    if ($null -eq $Object.$Field) {
        Fail "Missing field $Field in $Message"
    }
    if (-not [bool]$Object.$Field) {
        Fail $Message
    }
}

$requiredFiles = @(
    "phase-exec-algo-r008-summary.md",
    "phase-exec-algo-r008-r047-review-reference.json",
    "phase-exec-algo-r008-refined-parameter-contract.json",
    "phase-exec-algo-r008-parameter-contract-versioning.json",
    "phase-exec-algo-r008-candidate-range-contract.json",
    "phase-exec-algo-r008-close-seeking-adaptive-ranges.json",
    "phase-exec-algo-r008-controlled-residual-cross-ranges.json",
    "phase-exec-algo-r008-passive-until-urgency-ranges.json",
    "phase-exec-algo-r008-compact-design-grid.json",
    "phase-exec-algo-r008-r049-simulation-plan.json",
    "phase-exec-algo-r008-expected-r049-line-count.json",
    "phase-exec-algo-r008-threshold-evidence-status.json",
    "phase-exec-algo-r008-rejected-wakett-preservation.json",
    "phase-exec-algo-r008-benchmark-only-preservation.json",
    "phase-exec-algo-r008-manual-review-do-not-trade-preservation.json",
    "phase-exec-algo-r008-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-algo-r008-legacy-compatibility-preservation.json",
    "phase-exec-algo-r008-usd-pair-normalization-preservation.json",
    "phase-exec-algo-r008-direct-cross-exclusion-preservation.json",
    "phase-exec-algo-r008-cost-guidance-preservation.json",
    "phase-exec-algo-r008-nonmajor-calibration-preservation.json",
    "phase-exec-algo-r008-non-executable-contract-audit.json",
    "phase-exec-algo-r008-no-executable-schedule-audit.json",
    "phase-exec-algo-r008-no-child-slices-audit.json",
    "phase-exec-algo-r008-no-child-orders-audit.json",
    "phase-exec-algo-r008-no-new-backtest-audit.json",
    "phase-exec-algo-r008-no-new-simulation-audit.json",
    "phase-exec-algo-r008-no-tca-result-lines-audit.json",
    "phase-exec-algo-r008-no-polygon-api-call-audit.json",
    "phase-exec-algo-r008-no-lmax-call-audit.json",
    "phase-exec-algo-r008-no-external-api-call-audit.json",
    "phase-exec-algo-r008-no-broker-marketdata-runtime-audit.json",
    "phase-exec-algo-r008-no-real-fill-audit.json",
    "phase-exec-algo-r008-no-execution-report-audit.json",
    "phase-exec-algo-r008-no-order-created-audit.json",
    "phase-exec-algo-r008-no-route-no-submission-audit.json",
    "phase-exec-algo-r008-usdjpy-caveat-preservation.json",
    "phase-exec-algo-r008-lmax-readonly-baseline-reference.json",
    "phase-exec-algo-r008-no-external-audit.json",
    "phase-exec-algo-r008-forbidden-actions-audit.json",
    "phase-exec-algo-r008-next-phase-recommendation.json",
    "phase-exec-algo-r008-build-test-validator-evidence.json"
)

foreach ($file in $requiredFiles) {
    $path = Join-Path $ArtifactsRoot $file
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing required artifact $file"
    }
}

$contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r008-refined-parameter-contract.json")
if ($contract.ParameterContractId -ne "EXEC-ALGO-R008-CLOSE-SEEKING-REFINED-PARAMETER-CONTRACT") {
    Fail "Refined parameter contract id mismatch"
}
if ($contract.ParameterContractVersion -ne "0.2.0-design-only") {
    Fail "Parameter contract version mismatch"
}
if ($contract.SourceReviewPhase -ne "EXEC-SIM-R047" -or $contract.SourceCandidatePhase -ne "EXEC-ALGO-R007" -or $contract.SourceEvidencePhase -ne "EXEC-SIM-R045") {
    Fail "Parameter contract source phases invalid"
}
Assert-TrueField $contract "AppliesToCanonicalQuarterHourTimestamps" "Canonical quarter-hour not preserved"
Assert-TrueField $contract "LegacyCompatibilityOnly" "Legacy compatibility not marked only"
Assert-TrueField $contract "DesignOnly" "Contract is not design-only"
Assert-TrueField $contract "PaperOnly" "Contract is not paper-only"
Assert-TrueField $contract "NonExecutable" "Contract is executable"
Assert-TrueField $contract "NotAnOrder" "Contract is order-like"
Assert-TrueField $contract "NotSubmitted" "Contract is submitted"
Assert-TrueField $contract "NoBrokerRoute" "Contract has broker route"
Assert-FalseField $contract "ExecutablePromotionAuthorized" "Executable promotion authorized"
Assert-TrueField $contract "RequiresFutureSimulation" "Future simulation not required"
Assert-TrueField $contract "RequiresFutureOperatorApproval" "Future operator approval not required"
Assert-TrueField $contract "RangesAreCandidateDesignRanges" "Ranges not marked candidate/design-only"
Assert-FalseField $contract "RangesAreFinalCalibratedThresholds" "Ranges claimed final calibrated"

$rangeContract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r008-candidate-range-contract.json")
if ($rangeContract.CandidateFamilies.Count -ne 3 -or $rangeContract.HugeUnboundedCombinatorialGridCreated -ne $false) {
    Fail "Candidate range contract invalid or huge grid detected"
}
foreach ($status in @("CandidateRange", "DesignOnly", "NeedsSimulation", "NotFinalCalibrated")) {
    if (-not ($rangeContract.RangeEvidenceStatus -contains $status)) {
        Fail "Range evidence status missing $status"
    }
}

$adaptive = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r008-close-seeking-adaptive-ranges.json")
foreach ($field in @(
    "PassiveWindowStartOffsetFromCloseMinutes",
    "PassiveWindowEndOffsetFromCloseMinutes",
    "AdaptiveUrgencyStartOffsetFromCloseMinutes",
    "AdaptiveUrgencyEndOffsetFromCloseMinutes",
    "ControlledResidualStartOffsetFromCloseMinutes",
    "MaxSpreadBpsRange",
    "ResidualAtCloseThresholdRange",
    "OpportunityCostCrossThresholdRange",
    "CloseBenchmarkMaxAgeSecondsRange",
    "QuoteGapLimitSecondsRange"
)) {
    if ($null -eq $adaptive.Ranges.$field) {
        Fail "CloseSeeking adaptive range missing $field"
    }
}
Assert-FalseField $adaptive "FinalCalibratedThresholds" "Adaptive ranges claimed calibrated"
Assert-FalseField $adaptive "ExecutablePromotionAuthorized" "Adaptive executable promotion authorized"

$crc = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r008-controlled-residual-cross-ranges.json")
foreach ($field in @(
    "ControlledCrossStartOffsetFromCloseMinutes",
    "ResidualCrossThresholdRange",
    "OpportunityCostCrossThresholdRange",
    "MaxSpreadBeforeCrossBpsRange",
    "NoOvernightResidualPenaltyWeightRange",
    "MinimumFeedQualityBucket",
    "MaximumCloseBenchmarkAgeSecondsRange"
)) {
    if ($null -eq $crc.Ranges.$field) {
        Fail "ControlledResidualCross range missing $field"
    }
}
Assert-TrueField $crc.Ranges "ManualReviewFallbackEnabled" "CRC manual fallback missing"
Assert-TrueField $crc.Ranges "ConditionalOnly" "CRC not conditional-only"
Assert-TrueField $crc.Ranges "NotDefault" "CRC marked default"
Assert-TrueField $crc.Ranges "NeverAlwaysMarketAtClose" "CRC permits AlwaysMarketAtClose"
Assert-TrueField $crc.Ranges "NeverBlindCrossing" "CRC permits blind crossing"
Assert-FalseField $crc "FinalCalibratedThresholds" "CRC ranges claimed calibrated"

$passive = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r008-passive-until-urgency-ranges.json")
foreach ($field in @(
    "PassiveFillProbabilityEstimateRange",
    "NonFillCostEstimateRange",
    "UrgencyEscalationTriggerRange",
    "ResidualRiskTriggerRange",
    "SpreadCaptureTargetRange"
)) {
    if ($null -eq $passive.Ranges.$field) {
        Fail "PassiveUntilUrgency range missing $field"
    }
}
Assert-TrueField $passive.Ranges "ManualReviewFallbackEnabled" "Passive manual fallback missing"
Assert-TrueField $passive.Ranges "NotPureLimitUntilClose" "Passive defaults to pure limit"
Assert-FalseField $passive "FinalCalibratedThresholds" "Passive ranges claimed calibrated"

$grid = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r008-compact-design-grid.json")
if ($grid.VariantCount -ne 8 -or $grid.Variants.Count -ne 8 -or $grid.HugeUnboundedCombinatorialGridCreated -ne $false) {
    Fail "Compact design grid invalid"
}
foreach ($variant in $grid.Variants) {
    if ($variant.DesignOnly -ne $true -or $variant.NonExecutable -ne $true) {
        Fail "Grid variant executable risk detected"
    }
}

$plan = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r008-r049-simulation-plan.json")
if ($plan.IntendedNextPhase -ne "EXEC-SIM-R049" -or $plan.QuoteWindows -ne 945 -or $plan.CandidateVariantCount -ne 8) {
    Fail "R049 simulation plan invalid"
}
if ($plan.NegativeBaselineFamilies -contains "WakettPureLimitUntilClose") {
    # expected
} else {
    Fail "Wakett negative baseline missing"
}
Assert-TrueField $plan "NoExecutionInR008" "R008 executed future plan"
Assert-TrueField $plan "NoOrderDomainObjects" "R049 plan allows order-domain objects"

$lineCount = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r008-expected-r049-line-count.json")
if ($lineCount.ExpectedR049CandidateVariantCount -ne 8 -or $lineCount.QuoteWindows -ne 945 -or $lineCount.ExpectedFutureCandidateTcaLines -ne 7560) {
    Fail "Expected R049 line count invalid"
}

$thresholds = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r008-threshold-evidence-status.json")
if ($thresholds.FinalCalibratedThresholdsClaimed -ne $false -or $thresholds.UnsupportedFinalNumericThresholdsInvented -ne $false) {
    Fail "Thresholds claimed final/calibrated"
}
foreach ($status in @("CandidateRange", "DesignOnly", "NeedsSimulation", "NotFinalCalibrated")) {
    if (-not ($thresholds.ThresholdEvidenceStatus -contains $status)) {
        Fail "Threshold evidence missing $status"
    }
}

$wakett = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r008-rejected-wakett-preservation.json")
Assert-FalseField $wakett "WakettPromotedAsNormalCandidate" "Wakett promoted as normal candidate"
$bench = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r008-benchmark-only-preservation.json")
Assert-FalseField $bench "PromotedToExecutable" "Benchmark promoted to executable"
Assert-FalseField $bench "PromotedToCandidatePolicy" "Benchmark promoted to candidate policy"
$safety = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r008-manual-review-do-not-trade-preservation.json")
Assert-FalseField $safety "PromotedToExecutable" "ManualReview/DoNotTrade promoted to executable"
Assert-FalseField $safety "PromotedToCandidatePolicy" "ManualReview/DoNotTrade promoted to candidate policy"

$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r008-canonical-quarter-hour-policy-preservation.json")
if ($canonical.Legacy06UsedAsFutureCanonical -ne $false -or $canonical.Preserved -ne $true) {
    Fail "Canonical quarter-hour policy weakened"
}
$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r008-legacy-compatibility-preservation.json")
if ($legacy.LegacyCompatibilityOnly -ne $true -or $legacy.Legacy06UsedAsFutureCanonical -ne $false) {
    Fail "Legacy :06 used as future canonical"
}
$normalization = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r008-usd-pair-normalization-preservation.json")
if ($normalization.AppliesToExecutionUniverse -ne "USDPairOnly" -or $normalization.Symbols.Count -ne 7) {
    Fail "USD-pair normalization invalid"
}
$audusd = $normalization.Symbols | Where-Object { $_.Symbol -eq "AUDUSD" }
if (-not $audusd -or $audusd.AudUsdNotFailed -ne $true) {
    Fail "AUDUSD misclassified"
}
$direct = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r008-direct-cross-exclusion-preservation.json")
if ($direct.DirectCrossExecutionDisabled -ne $true -or $direct.DirectCrossesIncluded -ne $false) {
    Fail "Direct-cross exclusion weakened"
}
$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r008-cost-guidance-preservation.json")
if ($cost.FiveUsdPerMillionGuidance -ne "BestCaseMajorOnly" -or $cost.Universalized -ne $false) {
    Fail "5 USD/million universalized"
}
$usdjpy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r008-usdjpy-caveat-preservation.json")
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or $usdjpy.RequiresInversion -ne $true -or "$($usdjpy.SecurityID)" -ne "4004" -or "$($usdjpy.SecurityIDSource)" -ne "8" -or $usdjpy.Failed -ne $false) {
    Fail "USDJPY caveat weakened"
}

$nonExec = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r008-non-executable-contract-audit.json")
Assert-TrueField $nonExec "AllRangesDesignOnly" "Not all ranges design-only"
Assert-TrueField $nonExec "AllRangesPaperOnly" "Not all ranges paper-only"
Assert-TrueField $nonExec "AllRangesNonExecutable" "Not all ranges non-executable"
Assert-FalseField $nonExec "ExecutablePromotionAuthorized" "Executable promotion authorized"

$auditFalseChecks = @(
    @("phase-exec-algo-r008-no-executable-schedule-audit.json", "ExecutableSchedulesCreated", "Executable schedules created"),
    @("phase-exec-algo-r008-no-child-slices-audit.json", "ChildSlicesCreated", "Child slices created"),
    @("phase-exec-algo-r008-no-child-orders-audit.json", "ChildOrdersCreated", "Child orders created"),
    @("phase-exec-algo-r008-no-new-backtest-audit.json", "NewBacktestExecuted", "New backtest executed"),
    @("phase-exec-algo-r008-no-new-simulation-audit.json", "NewSimulationExecuted", "New simulation executed"),
    @("phase-exec-algo-r008-no-tca-result-lines-audit.json", "TcaResultLinesCreated", "TCA result lines created"),
    @("phase-exec-algo-r008-no-polygon-api-call-audit.json", "PolygonApiCalled", "Polygon API called"),
    @("phase-exec-algo-r008-no-lmax-call-audit.json", "LmaxCalled", "LMAX called"),
    @("phase-exec-algo-r008-no-external-api-call-audit.json", "ExternalApiCalled", "External API called"),
    @("phase-exec-algo-r008-no-broker-marketdata-runtime-audit.json", "BrokerMarketDataRuntimeStarted", "Broker runtime started"),
    @("phase-exec-algo-r008-no-real-fill-audit.json", "RealFillsCreated", "Real fills created"),
    @("phase-exec-algo-r008-no-execution-report-audit.json", "ExecutionReportsCreated", "Execution reports created"),
    @("phase-exec-algo-r008-no-order-created-audit.json", "OrdersCreated", "Orders created")
)
foreach ($check in $auditFalseChecks) {
    $doc = Read-Json (Join-Path $ArtifactsRoot $check[0])
    Assert-FalseField $doc $check[1] $check[2]
}
$route = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r008-no-route-no-submission-audit.json")
Assert-FalseField $route "RoutesCreated" "Routes created"
Assert-FalseField $route "SubmissionsCreated" "Submissions created"
$forbidden = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r008-forbidden-actions-audit.json")
Assert-FalseField $forbidden "ForbiddenActionsDetected" "Forbidden actions detected"
Assert-FalseField $forbidden "StateMutated" "State mutated"
Assert-FalseField $forbidden "ExecutablePromotionAuthorized" "Executable promotion authorized"

$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r008-build-test-validator-evidence.json")
if ($evidence.DotnetBuild.Status -notin @("PASS", "PASS_WITH_WARNINGS")) {
    Fail "Build evidence missing or failing"
}
if ($evidence.FocusedR008StaticChecks.Status -ne "PASS") {
    Fail "Focused R008 static checks missing or failing"
}
if ($evidence.UnitTests.Status -notin @("PASS", "PASS_WITH_WARNINGS", "NOT_FEASIBLE")) {
    Fail "Unit test evidence missing"
}

Write-Host "EXEC-ALGO-R008 validator passed."
Write-Host "Classifications:"
Write-Host "EXEC_ALGO_R008_PASS_REFINED_PARAMETER_CONTRACT_READY_NO_EXTERNAL"
Write-Host "EXEC_ALGO_R008_PASS_CANDIDATE_RANGE_GRID_READY_NO_EXTERNAL"
Write-Host "EXEC_ALGO_R008_PASS_R049_SIMULATION_PLAN_READY_NO_EXTERNAL"
Write-Host "EXEC_ALGO_R008_PASS_NONEXECUTABLE_PARAMETER_UPDATE_GATE_READY_NO_EXTERNAL"
