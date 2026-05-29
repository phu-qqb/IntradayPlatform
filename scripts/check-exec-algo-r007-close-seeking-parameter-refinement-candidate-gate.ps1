param(
    [string]$ArtifactsRoot = "artifacts/readiness/execution-algo"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Error "EXEC-ALGO-R007 validation failed: $Message"
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
    "phase-exec-algo-r007-summary.md",
    "phase-exec-algo-r007-r045-decision-reference.json",
    "phase-exec-algo-r007-parameter-refinement-candidate-contract.json",
    "phase-exec-algo-r007-candidate-versioning.json",
    "phase-exec-algo-r007-close-seeking-adaptive-candidate.json",
    "phase-exec-algo-r007-controlled-residual-cross-candidate.json",
    "phase-exec-algo-r007-passive-until-urgency-candidate.json",
    "phase-exec-algo-r007-rejected-wakett-patterns.json",
    "phase-exec-algo-r007-benchmark-only-preservation.json",
    "phase-exec-algo-r007-manual-review-do-not-trade-preservation.json",
    "phase-exec-algo-r007-refinement-dimensions.json",
    "phase-exec-algo-r007-threshold-evidence-status.json",
    "phase-exec-algo-r007-future-simulation-requirements.json",
    "phase-exec-algo-r007-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-algo-r007-legacy-compatibility-preservation.json",
    "phase-exec-algo-r007-usd-pair-normalization-preservation.json",
    "phase-exec-algo-r007-direct-cross-exclusion-preservation.json",
    "phase-exec-algo-r007-cost-guidance-preservation.json",
    "phase-exec-algo-r007-nonmajor-calibration-preservation.json",
    "phase-exec-algo-r007-non-executable-candidate-audit.json",
    "phase-exec-algo-r007-no-executable-schedule-audit.json",
    "phase-exec-algo-r007-no-child-slices-audit.json",
    "phase-exec-algo-r007-no-child-orders-audit.json",
    "phase-exec-algo-r007-no-new-backtest-audit.json",
    "phase-exec-algo-r007-no-new-simulation-audit.json",
    "phase-exec-algo-r007-no-tca-result-lines-audit.json",
    "phase-exec-algo-r007-no-polygon-api-call-audit.json",
    "phase-exec-algo-r007-no-lmax-call-audit.json",
    "phase-exec-algo-r007-no-external-api-call-audit.json",
    "phase-exec-algo-r007-no-broker-marketdata-runtime-audit.json",
    "phase-exec-algo-r007-no-real-fill-audit.json",
    "phase-exec-algo-r007-no-execution-report-audit.json",
    "phase-exec-algo-r007-no-order-created-audit.json",
    "phase-exec-algo-r007-no-route-no-submission-audit.json",
    "phase-exec-algo-r007-usdjpy-caveat-preservation.json",
    "phase-exec-algo-r007-lmax-readonly-baseline-reference.json",
    "phase-exec-algo-r007-no-external-audit.json",
    "phase-exec-algo-r007-forbidden-actions-audit.json",
    "phase-exec-algo-r007-next-phase-recommendation.json",
    "phase-exec-algo-r007-build-test-validator-evidence.json"
)

foreach ($file in $requiredFiles) {
    $path = Join-Path $ArtifactsRoot $file
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing required artifact $file"
    }
}

$reference = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r007-r045-decision-reference.json")
if ($reference.SourcePhase -ne "EXEC-SIM-R045" -or $reference.TcaLinesReviewed -ne 10395) {
    Fail "R045 decision reference is missing or incorrect"
}

$contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r007-parameter-refinement-candidate-contract.json")
if ($contract.ContractId -ne "EXEC-ALGO-R007-PARAMETER-REFINEMENT-CANDIDATE-CONTRACT") {
    Fail "Parameter refinement candidate contract id mismatch"
}
Assert-TrueField $contract "DesignOnly" "Contract does not require design-only candidates"
Assert-TrueField $contract "PaperOnly" "Contract does not require paper-only candidates"
Assert-TrueField $contract "NonExecutable" "Contract does not require non-executable candidates"
Assert-TrueField $contract "NotAnOrder" "Contract does not require NotAnOrder"
Assert-TrueField $contract "NotSubmitted" "Contract does not require NotSubmitted"
Assert-TrueField $contract "NoBrokerRoute" "Contract does not require NoBrokerRoute"
Assert-FalseField $contract "ExecutablePromotionAuthorized" "Contract authorizes executable promotion"
if ($contract.ThresholdPolicy -notlike "*Do not invent final numeric thresholds*") {
    Fail "Threshold evidence policy is missing"
}

function Check-Candidate($Path, [string]$Family, [string]$Status) {
    $candidate = Read-Json (Join-Path $ArtifactsRoot $Path)
    if ($candidate.CandidatePolicyFamily -ne $Family -or $candidate.CandidateStatus -ne $Status) {
        Fail "$Family candidate missing or wrong status"
    }
    Assert-TrueField $candidate "DesignOnly" "$Family is not design-only"
    Assert-TrueField $candidate "PaperOnly" "$Family is not paper-only"
    Assert-TrueField $candidate "NonExecutable" "$Family is executable"
    Assert-TrueField $candidate "NotAnOrder" "$Family is represented as an order"
    Assert-TrueField $candidate "NotSubmitted" "$Family is submitted"
    Assert-TrueField $candidate "NoBrokerRoute" "$Family has broker route"
    Assert-FalseField $candidate "ExecutablePromotionAuthorized" "$Family authorizes executable promotion"
    if (($candidate.RefinementDimensions | Where-Object { $_.FinalThresholdAuthorized -eq $true }).Count -ne 0) {
        Fail "$Family contains an authorized final numeric threshold"
    }
    if (($candidate.RefinementDimensions | Where-Object { $_.CandidateValueStatus -notin @("NeedsFurtherCalibration", "MissingEvidence") }).Count -ne 0) {
        Fail "$Family contains unsupported threshold evidence status"
    }
}

Check-Candidate "phase-exec-algo-r007-close-seeking-adaptive-candidate.json" "CloseSeeking15mAdaptive" "KeepForParameterRefinement"
Check-Candidate "phase-exec-algo-r007-controlled-residual-cross-candidate.json" "ControlledResidualCross" "ConditionalKeepForParameterRefinement"
Check-Candidate "phase-exec-algo-r007-passive-until-urgency-candidate.json" "PassiveUntilUrgency" "NeedsRefinement"

$controlled = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r007-controlled-residual-cross-candidate.json")
if ($controlled.NotAlwaysMarketAtClose -ne $true -or $controlled.BlindCrossingAuthorized -ne $false -or $controlled.ConditionalOnly -ne $true) {
    Fail "ControlledResidualCross was promoted to blind/default crossing"
}

$rejected = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r007-rejected-wakett-patterns.json")
foreach ($pattern in @("WakettPureLimitUntilClose", "WakettFiveMarketSlicesAroundClose", "AlwaysMarketAtClose", "BlindMarketCrossingWithoutCostJustification", "MechanicalMarketSlicesAroundClose")) {
    $entry = $rejected.RejectedPatterns | Where-Object { $_.Pattern -eq $pattern }
    if (-not $entry -or $entry.CandidateStatus -ne "RejectUnsafePattern" -or $entry.ExecutablePromotionAuthorized -ne $false) {
        Fail "Rejected pattern $pattern is missing or weakened"
    }
}

$benchmark = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r007-benchmark-only-preservation.json")
if ($benchmark.VwapTwapPromotedToExecutable -ne $false -or $benchmark.BenchmarkPolicies.Count -ne 3) {
    Fail "Benchmark-only preservation missing or promoted"
}
foreach ($policy in $benchmark.BenchmarkPolicies) {
    if ($policy.CandidateStatus -ne "BenchmarkOnlyKeepForComparison" -or $policy.NonExecutable -ne $true -or $policy.NotAnOrder -ne $true -or $policy.NoBrokerRoute -ne $true -or $policy.ExecutablePromotionAuthorized -ne $false) {
        Fail "Benchmark policy $($policy.PolicyFamily) was promoted or made executable"
    }
}

$manual = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r007-manual-review-do-not-trade-preservation.json")
if ($manual.ManualReviewPromotedToExecutable -ne $false -or $manual.DoNotTradePromotedToExecutable -ne $false) {
    Fail "ManualReview or DoNotTrade was promoted to executable"
}
foreach ($policy in $manual.SafetyOutcomes) {
    if ($policy.CandidateStatus -ne "SafetyOutcomeOnly" -or $policy.NonExecutable -ne $true -or $policy.ExecutablePromotionAuthorized -ne $false) {
        Fail "Safety outcome $($policy.PolicyFamily) was weakened"
    }
}

$thresholds = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r007-threshold-evidence-status.json")
if ($thresholds.UnsupportedFinalNumericThresholdsInvented -ne $false) {
    Fail "Unsupported final numeric thresholds were invented"
}
if (($thresholds.ThresholdEvidenceStatus | Where-Object { $_.SupportedFinalNumericThreshold -eq $true -or $_.EvidenceStatus -notin @("NeedsFurtherCalibration", "MissingEvidence") }).Count -ne 0) {
    Fail "Threshold evidence status contains unsupported final numeric thresholds"
}

$future = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r007-future-simulation-requirements.json")
if ($future.NoExecutionInR007 -ne $true -or -not ($future.Requirements -contains "Use canonical quarter-hour timestamps") -or -not ($future.Requirements -contains "Require no-order-domain outputs")) {
    Fail "Future simulation requirements are incomplete"
}

$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r007-canonical-quarter-hour-policy-preservation.json")
if ($canonical.Legacy06UsedAsFutureCanonical -ne $false -or $canonical.Preserved -ne $true) {
    Fail "Canonical quarter-hour policy weakened"
}
$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r007-legacy-compatibility-preservation.json")
if ($legacy.LegacyCompatibilityOnly -ne $true -or $legacy.Legacy06UsedAsFutureCanonical -ne $false) {
    Fail "Legacy :06 used as future canonical"
}
$normalization = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r007-usd-pair-normalization-preservation.json")
if ($normalization.AppliesToExecutionUniverse -ne "USDPairOnly" -or $normalization.Symbols.Count -ne 7) {
    Fail "USD-pair normalization preservation missing"
}
$audusd = $normalization.Symbols | Where-Object { $_.Symbol -eq "AUDUSD" }
if (-not $audusd -or $audusd.AudUsdNotFailed -ne $true) {
    Fail "AUDUSD is misclassified"
}
$direct = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r007-direct-cross-exclusion-preservation.json")
if ($direct.DirectCrossExecutionDisabled -ne $true -or $direct.DirectCrossesIncluded -ne $false -or $direct.ExecutionUniverse -ne "USD-pair-only") {
    Fail "Direct-cross exclusion weakened"
}
$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r007-cost-guidance-preservation.json")
if ($cost.FiveUsdPerMillionGuidance -ne "BestCaseMajorOnly" -or $cost.Universalized -ne $false) {
    Fail "5 USD/million guidance universalized"
}
$usdjpy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r007-usdjpy-caveat-preservation.json")
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or $usdjpy.RequiresInversion -ne $true -or "$($usdjpy.SecurityID)" -ne "4004" -or "$($usdjpy.SecurityIDSource)" -ne "8" -or $usdjpy.Failed -ne $false) {
    Fail "USDJPY caveat weakened"
}

$nonExec = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r007-non-executable-candidate-audit.json")
Assert-TrueField $nonExec "AllCandidatesDesignOnly" "Not all candidates are design-only"
Assert-TrueField $nonExec "AllCandidatesPaperOnly" "Not all candidates are paper-only"
Assert-TrueField $nonExec "AllCandidatesNonExecutable" "Not all candidates are non-executable"
Assert-FalseField $nonExec "ExecutablePromotionAuthorized" "Executable promotion authorized"

$auditFalseChecks = @(
    @("phase-exec-algo-r007-no-executable-schedule-audit.json", "ExecutableSchedulesCreated", "Executable schedules created"),
    @("phase-exec-algo-r007-no-child-slices-audit.json", "ChildSlicesCreated", "Child slices created"),
    @("phase-exec-algo-r007-no-child-orders-audit.json", "ChildOrdersCreated", "Child orders created"),
    @("phase-exec-algo-r007-no-new-backtest-audit.json", "NewBacktestExecuted", "New backtest executed"),
    @("phase-exec-algo-r007-no-new-simulation-audit.json", "NewSimulationExecuted", "New simulation executed"),
    @("phase-exec-algo-r007-no-tca-result-lines-audit.json", "TcaResultLinesCreated", "TCA result lines created"),
    @("phase-exec-algo-r007-no-polygon-api-call-audit.json", "PolygonApiCalled", "Polygon API called"),
    @("phase-exec-algo-r007-no-lmax-call-audit.json", "LmaxCalled", "LMAX called"),
    @("phase-exec-algo-r007-no-external-api-call-audit.json", "ExternalApiCalled", "External API called"),
    @("phase-exec-algo-r007-no-broker-marketdata-runtime-audit.json", "BrokerMarketDataRuntimeStarted", "Broker market-data runtime started"),
    @("phase-exec-algo-r007-no-real-fill-audit.json", "RealFillsCreated", "Real fills created"),
    @("phase-exec-algo-r007-no-execution-report-audit.json", "ExecutionReportsCreated", "Execution reports created"),
    @("phase-exec-algo-r007-no-order-created-audit.json", "OrdersCreated", "Orders created")
)
foreach ($check in $auditFalseChecks) {
    $doc = Read-Json (Join-Path $ArtifactsRoot $check[0])
    Assert-FalseField $doc $check[1] $check[2]
}
$route = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r007-no-route-no-submission-audit.json")
Assert-FalseField $route "RoutesCreated" "Routes created"
Assert-FalseField $route "SubmissionsCreated" "Submissions created"

$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r007-build-test-validator-evidence.json")
if ($evidence.DotnetBuild.Status -notin @("PASS", "PASS_WITH_WARNINGS")) {
    Fail "Build evidence missing or failing"
}
if ($evidence.FocusedR007StaticChecks.Status -ne "PASS") {
    Fail "Focused R007 static checks missing or failing"
}
if ($evidence.UnitTests.Status -notin @("PASS", "PASS_WITH_WARNINGS", "NOT_FEASIBLE")) {
    Fail "Unit test evidence missing"
}

Write-Host "EXEC-ALGO-R007 validator passed."
Write-Host "Classifications:"
Write-Host "EXEC_ALGO_R007_PASS_CLOSE_SEEKING_REFINEMENT_CANDIDATES_READY_NO_EXTERNAL"
Write-Host "EXEC_ALGO_R007_PASS_CONTROLLED_RESIDUAL_CROSS_CONDITIONAL_CANDIDATE_READY_NO_EXTERNAL"
Write-Host "EXEC_ALGO_R007_PASS_WAKETT_REJECTION_AND_BENCHMARK_PRESERVATION_READY_NO_EXTERNAL"
Write-Host "EXEC_ALGO_R007_PASS_NONEXECUTABLE_REFINEMENT_GATE_READY_NO_EXTERNAL"
