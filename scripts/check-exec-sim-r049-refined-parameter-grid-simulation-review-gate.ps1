param(
    [string]$ArtifactsRoot = "artifacts/readiness/execution-sim"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Error "EXEC-SIM-R049 validation failed: $Message"
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
    "phase-exec-sim-r049-summary.md",
    "phase-exec-sim-r049-r008-contract-reference.json",
    "phase-exec-sim-r049-r047-review-reference.json",
    "phase-exec-sim-r049-r045-decision-reference.json",
    "phase-exec-sim-r049-r044-context-reference.json",
    "phase-exec-sim-r049-refined-grid-simulation-contract.json",
    "phase-exec-sim-r049-refined-grid-simulation-run-result.json",
    "phase-exec-sim-r049-variant-definitions-used.json",
    "phase-exec-sim-r049-tca-result-line-contract.json",
    "phase-exec-sim-r049-candidate-tca-result-lines.json",
    "phase-exec-sim-r049-result-line-count-and-coverage.json",
    "phase-exec-sim-r049-candidate-vs-r044-comparison.json",
    "phase-exec-sim-r049-candidate-vs-current-policy-comparison.json",
    "phase-exec-sim-r049-per-variant-reports.json",
    "phase-exec-sim-r049-per-date-reports.json",
    "phase-exec-sim-r049-per-symbol-reports.json",
    "phase-exec-sim-r049-per-symbol-date-reports.json",
    "phase-exec-sim-r049-aggregate-policy-comparison.json",
    "phase-exec-sim-r049-ranking-median-slippage.json",
    "phase-exec-sim-r049-ranking-p95-slippage.json",
    "phase-exec-sim-r049-ranking-fill-ratio.json",
    "phase-exec-sim-r049-ranking-residual.json",
    "phase-exec-sim-r049-ranking-spread-paid.json",
    "phase-exec-sim-r049-spread-residual-tradeoff-review.json",
    "phase-exec-sim-r049-no-overnight-residual-pressure-review.json",
    "phase-exec-sim-r049-threshold-evidence-calibration-review.json",
    "phase-exec-sim-r049-policy-decision.json",
    "phase-exec-sim-r049-parameter-refinement-decision.json",
    "phase-exec-sim-r049-next-design-only-parameter-contract-recommendation.json",
    "phase-exec-sim-r049-wakett-rejection-preservation.json",
    "phase-exec-sim-r049-benchmark-only-preservation.json",
    "phase-exec-sim-r049-manual-review-do-not-trade-preservation.json",
    "phase-exec-sim-r049-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-sim-r049-legacy-compatibility-preservation.json",
    "phase-exec-sim-r049-usd-pair-normalization-preservation.json",
    "phase-exec-sim-r049-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r049-cost-guidance-preservation.json",
    "phase-exec-sim-r049-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r049-non-executable-result-line-audit.json",
    "phase-exec-sim-r049-no-db-import-audit.json",
    "phase-exec-sim-r049-no-persisted-sanitized-row-audit.json",
    "phase-exec-sim-r049-no-executable-schedule-audit.json",
    "phase-exec-sim-r049-no-child-slices-audit.json",
    "phase-exec-sim-r049-no-child-orders-audit.json",
    "phase-exec-sim-r049-no-real-fill-audit.json",
    "phase-exec-sim-r049-no-execution-report-audit.json",
    "phase-exec-sim-r049-no-order-created-audit.json",
    "phase-exec-sim-r049-no-route-no-submission-audit.json",
    "phase-exec-sim-r049-no-polygon-api-call-audit.json",
    "phase-exec-sim-r049-no-lmax-call-audit.json",
    "phase-exec-sim-r049-no-external-api-call-audit.json",
    "phase-exec-sim-r049-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r049-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r049-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r049-no-external-audit.json",
    "phase-exec-sim-r049-forbidden-actions-audit.json",
    "phase-exec-sim-r049-next-phase-recommendation.json",
    "phase-exec-sim-r049-build-test-validator-evidence.json"
)

foreach ($file in $requiredFiles) {
    $path = Join-Path $ArtifactsRoot $file
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing required artifact $file"
    }
}

$r008 = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r049-r008-contract-reference.json")
if ($r008.ParameterContractVersion -ne "0.2.0-design-only" -or $r008.CandidateVariantCount -ne 8 -or $r008.ExpectedCandidateTcaLines -ne 7560) {
    Fail "R008 contract reference invalid"
}
Assert-FalseField $r008 "RangesAreFinalCalibratedThresholds" "R008 ranges marked final calibrated"
Assert-FalseField $r008 "ExecutablePromotionAuthorized" "R008 executable promotion authorized"

$contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r049-refined-grid-simulation-contract.json")
if ($contract.RefinedGridSimulationRunId -ne "EXEC-SIM-R049-REFINED-PARAMETER-GRID-SIMULATION") {
    Fail "Simulation contract id mismatch"
}
if ($contract.CandidateVariantCount -ne 8 -or $contract.QuoteWindowCount -ne 945 -or $contract.ExpectedCandidateTcaResultLines -ne 7560) {
    Fail "Simulation contract coverage mismatch"
}
Assert-TrueField $contract "FixtureOnly" "Contract is not fixture-only"
Assert-TrueField $contract "PaperOnly" "Contract is not paper-only"
Assert-TrueField $contract "NonExecutable" "Contract is executable"
Assert-TrueField $contract "NoApiCall" "Contract allows API calls"
Assert-TrueField $contract "NoDbImport" "Contract allows DB import"
Assert-TrueField $contract "NoOrderDomainOutput" "Contract allows order-domain output"
Assert-TrueField $contract "NoExecutablePromotion" "Contract allows executable promotion"

$run = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r049-refined-grid-simulation-run-result.json")
if ($run.SimulationStatus -ne "FixtureOnlyPaperNonExecutableCompletedNoExternal" -or $run.CandidateTcaResultLineCount -ne 7560) {
    Fail "Simulation run result invalid"
}
Assert-FalseField $run "ExecutablePromotionAuthorized" "Run result authorizes executable promotion"
foreach ($classification in @(
    "EXEC_SIM_R049_PASS_REFINED_PARAMETER_GRID_SIMULATION_READY_NO_EXTERNAL",
    "EXEC_SIM_R049_PASS_REFINED_CANDIDATE_REVIEW_READY_NO_EXTERNAL",
    "EXEC_SIM_R049_PASS_NEXT_DESIGN_PARAMETER_RECOMMENDATION_READY_NO_EXTERNAL",
    "EXEC_SIM_R049_PASS_NO_DB_IMPORT_NO_REAL_FILL_NO_ORDER_GATE_READY_NO_EXTERNAL"
)) {
    if (-not ($run.Classifications -contains $classification)) {
        Fail "Missing classification $classification"
    }
}

$variants = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r049-variant-definitions-used.json")
if ($variants.VariantCount -ne 8 -or $variants.Variants.Count -ne 8) {
    Fail "Variant definitions missing or count mismatch"
}
foreach ($variant in $variants.Variants) {
    if ($variant.DesignOnly -ne $true -or $variant.NonExecutable -ne $true) {
        Fail "Variant executable risk detected"
    }
}

$lines = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r049-candidate-tca-result-lines.json")
if ($lines.CandidateTcaResultLineCount -ne 7560 -or $lines.PerVariantLineCount -ne 945 -or $lines.PerVariantCounts.Count -ne 8) {
    Fail "Candidate result lines count invalid"
}
Assert-FalseField $lines "ContainsFills" "Result lines represented as fills"
Assert-FalseField $lines "ContainsOrders" "Result lines represented as orders"
Assert-FalseField $lines "ContainsExecutionReports" "Result lines represented as execution reports"
Assert-FalseField $lines "ContainsRoutes" "Result lines represented as routes"
Assert-FalseField $lines "ContainsSubmissions" "Result lines represented as submissions"
Assert-TrueField $lines "AllLinesFixtureOnly" "Not all lines fixture-only"
Assert-TrueField $lines "AllLinesPaperOnly" "Not all lines paper-only"
Assert-TrueField $lines "AllLinesNonExecutable" "Not all lines non-executable"

$coverage = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r049-result-line-count-and-coverage.json")
if ($coverage.CandidateVariantCount -ne 8 -or $coverage.QuoteWindows -ne 945 -or $coverage.ExpectedCandidateTcaResultLines -ne 7560 -or $coverage.ActualCandidateTcaResultLines -ne 7560 -or $coverage.CoverageComplete -ne $true) {
    Fail "Result line coverage invalid"
}

$perVariant = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r049-per-variant-reports.json")
if ($perVariant.Reports.Count -ne 8) {
    Fail "Per-variant reports missing"
}
$candidateComparison = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r049-candidate-vs-r044-comparison.json")
if ($candidateComparison.ComparisonStatus -ne "CompletedNoExternal") {
    Fail "Candidate vs R044 comparison missing"
}
$currentComparison = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r049-candidate-vs-current-policy-comparison.json")
if ($currentComparison.Rows.Count -lt 3) {
    Fail "Candidate vs current policy comparison missing"
}
$aggregate = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r049-aggregate-policy-comparison.json")
if ($aggregate.Reports.Count -lt 4) {
    Fail "Aggregate policy comparison missing"
}
foreach ($ranking in @(
    "phase-exec-sim-r049-ranking-median-slippage.json",
    "phase-exec-sim-r049-ranking-p95-slippage.json",
    "phase-exec-sim-r049-ranking-fill-ratio.json",
    "phase-exec-sim-r049-ranking-residual.json",
    "phase-exec-sim-r049-ranking-spread-paid.json"
)) {
    $doc = Read-Json (Join-Path $ArtifactsRoot $ranking)
    if ($doc.Rows.Count -ne 8) {
        Fail "$ranking does not rank all 8 variants"
    }
}

$decision = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r049-policy-decision.json")
if ($decision.PrimaryDesignRecommendation -ne "CloseSeeking15mAdaptive_BalancedAdaptive_v0") {
    Fail "Unexpected primary design recommendation"
}
if ($decision.WakettPureLimitUntilClose -ne "RejectedNegativeBaselineOnly" -or $decision.WakettFiveMarketSlicesAroundClose -ne "RejectedNegativeBaselineOnly") {
    Fail "Wakett patterns promoted"
}
if ($decision.VWAPBenchmarkOnly -ne "BenchmarkOnlyNotExecutable" -or $decision.TWAPBenchmarkOnly -ne "BenchmarkOnlyNotExecutable") {
    Fail "VWAP/TWAP promoted from benchmark-only"
}
if ($decision.ManualReview -ne "SafetyOutcomeOnly" -or $decision.DoNotTrade -ne "SafetyOutcomeOnly") {
    Fail "ManualReview/DoNotTrade promoted"
}
Assert-FalseField $decision "ExecutablePromotionAuthorized" "Policy decision authorizes executable promotion"

$parameterDecision = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r049-parameter-refinement-decision.json")
if ($parameterDecision.RecommendedNextDesignOnlyParameterSet -ne "CloseSeeking15mAdaptive_BalancedAdaptive_v0") {
    Fail "Parameter refinement decision missing recommendation"
}
Assert-FalseField $parameterDecision "ExecutablePromotionAuthorized" "Parameter decision authorizes executable promotion"
$nextContract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r049-next-design-only-parameter-contract-recommendation.json")
if ($nextContract.RecommendedNextPhase -ne "EXEC-ALGO-R009") {
    Fail "Next design-only parameter recommendation missing"
}
Assert-FalseField $nextContract.ContractMustRemain "ExecutablePromotionAuthorized" "Next contract allows executable promotion"

$thresholds = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r049-threshold-evidence-calibration-review.json")
Assert-FalseField $thresholds "FinalCalibratedThresholdsClaimed" "Unsupported final thresholds claimed"
Assert-FalseField $thresholds "UnsupportedFinalThresholdsClaimedCalibrated" "Unsupported thresholds claimed calibrated"

$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r049-canonical-quarter-hour-policy-preservation.json")
if ($canonical.Legacy06UsedAsFutureCanonical -ne $false -or $canonical.Preserved -ne $true) {
    Fail "Canonical quarter-hour policy weakened"
}
$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r049-legacy-compatibility-preservation.json")
if ($legacy.LegacyCompatibilityOnly -ne $true -or $legacy.Legacy06UsedAsFutureCanonical -ne $false) {
    Fail "Legacy :06 used as future canonical"
}
$direct = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r049-direct-cross-exclusion-preservation.json")
if ($direct.DirectCrossExecutionDisabled -ne $true -or $direct.DirectCrossesIncluded -ne $false) {
    Fail "Direct-cross exclusion weakened"
}
$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r049-cost-guidance-preservation.json")
if ($cost.FiveUsdPerMillionGuidance -ne "BestCaseMajorOnly" -or $cost.Universalized -ne $false) {
    Fail "5 USD/million universalized"
}
$normalization = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r049-usd-pair-normalization-preservation.json")
if ($normalization.Symbols.Count -ne 7) {
    Fail "USD-pair symbol coverage invalid"
}
$audusd = $normalization.Symbols | Where-Object { $_.Symbol -eq "AUDUSD" }
if (-not $audusd -or $audusd.AudUsdNotFailed -ne $true) {
    Fail "AUDUSD misclassified"
}
$usdjpy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r049-usdjpy-caveat-preservation.json")
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or $usdjpy.RequiresInversion -ne $true -or "$($usdjpy.SecurityID)" -ne "4004" -or "$($usdjpy.SecurityIDSource)" -ne "8" -or $usdjpy.Failed -ne $false) {
    Fail "USDJPY caveat weakened"
}

$nonExec = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r049-non-executable-result-line-audit.json")
Assert-TrueField $nonExec "AllResultLinesFixtureOnly" "Not all result lines fixture-only"
Assert-TrueField $nonExec "AllResultLinesPaperOnly" "Not all result lines paper-only"
Assert-TrueField $nonExec "AllResultLinesNonExecutable" "Not all result lines non-executable"
Assert-FalseField $nonExec "ContainsFills" "Result lines contain fills"
Assert-FalseField $nonExec "ContainsOrders" "Result lines contain orders"
Assert-FalseField $nonExec "ContainsExecutionReports" "Result lines contain execution reports"
Assert-FalseField $nonExec "ContainsRoutes" "Result lines contain routes"
Assert-FalseField $nonExec "ContainsSubmissions" "Result lines contain submissions"
Assert-FalseField $nonExec "ExecutablePromotionAuthorized" "Result line audit authorizes executable promotion"

$auditFalseChecks = @(
    @("phase-exec-sim-r049-no-db-import-audit.json", "DbImportExecuted", "DB import executed"),
    @("phase-exec-sim-r049-no-persisted-sanitized-row-audit.json", "PersistedSanitizedRowsCreated", "Persisted sanitized rows created"),
    @("phase-exec-sim-r049-no-executable-schedule-audit.json", "ExecutableSchedulesCreated", "Executable schedules created"),
    @("phase-exec-sim-r049-no-child-slices-audit.json", "ChildSlicesCreated", "Child slices created"),
    @("phase-exec-sim-r049-no-child-orders-audit.json", "ChildOrdersCreated", "Child orders created"),
    @("phase-exec-sim-r049-no-real-fill-audit.json", "RealFillsCreated", "Real fills created"),
    @("phase-exec-sim-r049-no-execution-report-audit.json", "ExecutionReportsCreated", "Execution reports created"),
    @("phase-exec-sim-r049-no-order-created-audit.json", "OrdersCreated", "Orders created"),
    @("phase-exec-sim-r049-no-polygon-api-call-audit.json", "PolygonApiCalled", "Polygon API called"),
    @("phase-exec-sim-r049-no-lmax-call-audit.json", "LmaxCalled", "LMAX called"),
    @("phase-exec-sim-r049-no-external-api-call-audit.json", "ExternalApiCalled", "External API called"),
    @("phase-exec-sim-r049-no-broker-marketdata-runtime-audit.json", "BrokerMarketDataRuntimeStarted", "Broker runtime started")
)
foreach ($check in $auditFalseChecks) {
    $doc = Read-Json (Join-Path $ArtifactsRoot $check[0])
    Assert-FalseField $doc $check[1] $check[2]
}
$route = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r049-no-route-no-submission-audit.json")
Assert-FalseField $route "RoutesCreated" "Routes created"
Assert-FalseField $route "SubmissionsCreated" "Submissions created"
$forbidden = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r049-forbidden-actions-audit.json")
Assert-FalseField $forbidden "ForbiddenActionsDetected" "Forbidden actions detected"
Assert-FalseField $forbidden "QuoteRowsRevalidated" "Quote rows revalidated"
Assert-FalseField $forbidden "StateMutated" "State mutated"
Assert-FalseField $forbidden "ExecutablePromotionAuthorized" "Executable promotion authorized"

$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r049-build-test-validator-evidence.json")
if ($evidence.DotnetBuild.Status -notin @("PASS", "PASS_WITH_WARNINGS")) {
    Fail "Build evidence missing or failing"
}
if ($evidence.FocusedR049StaticChecks.Status -ne "PASS") {
    Fail "Focused R049 static checks missing or failing"
}
if ($evidence.UnitTests.Status -notin @("PASS", "PASS_WITH_WARNINGS", "NOT_FEASIBLE")) {
    Fail "Unit test evidence missing"
}

Write-Host "EXEC-SIM-R049 validator passed."
Write-Host "Classifications:"
Write-Host "EXEC_SIM_R049_PASS_REFINED_PARAMETER_GRID_SIMULATION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R049_PASS_REFINED_CANDIDATE_REVIEW_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R049_PASS_NEXT_DESIGN_PARAMETER_RECOMMENDATION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R049_PASS_NO_DB_IMPORT_NO_REAL_FILL_NO_ORDER_GATE_READY_NO_EXTERNAL"
