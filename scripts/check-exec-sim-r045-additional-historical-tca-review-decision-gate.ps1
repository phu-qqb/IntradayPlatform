param(
    [string]$ArtifactsRoot = "artifacts/readiness/execution-sim"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Error "EXEC-SIM-R045 validation failed: $Message"
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
    "phase-exec-sim-r045-summary.md",
    "phase-exec-sim-r045-r044-tca-review-contract.json",
    "phase-exec-sim-r045-operator-review-report.md",
    "phase-exec-sim-r045-operator-review-report.json",
    "phase-exec-sim-r045-numeric-tca-summary.json",
    "phase-exec-sim-r045-policy-ranking-review.json",
    "phase-exec-sim-r045-per-date-review.json",
    "phase-exec-sim-r045-per-symbol-review.json",
    "phase-exec-sim-r045-per-symbol-date-review.json",
    "phase-exec-sim-r045-canonical-session-aggregate-review.json",
    "phase-exec-sim-r045-wakett-vs-close-seeking-review.json",
    "phase-exec-sim-r045-close-seeking-adaptive-review.json",
    "phase-exec-sim-r045-controlled-residual-cross-review.json",
    "phase-exec-sim-r045-passive-until-urgency-review.json",
    "phase-exec-sim-r045-wakett-limit-residual-risk-review.json",
    "phase-exec-sim-r045-wakett-five-slices-spread-risk-review.json",
    "phase-exec-sim-r045-benchmark-only-review.json",
    "phase-exec-sim-r045-manual-review-do-not-trade-review.json",
    "phase-exec-sim-r045-inverted-symbol-review.json",
    "phase-exec-sim-r045-duplicate-aware-review.json",
    "phase-exec-sim-r045-5usd-per-million-review.json",
    "phase-exec-sim-r045-no-overnight-residual-penalty-review.json",
    "phase-exec-sim-r045-comparison-vs-r025-r031-review.json",
    "phase-exec-sim-r045-execution-policy-decision.json",
    "phase-exec-sim-r045-parameter-refinement-decision.json",
    "phase-exec-sim-r045-data-expansion-decision.json",
    "phase-exec-sim-r045-design-only-shape-decision.json",
    "phase-exec-sim-r045-next-phase-recommendation.json",
    "phase-exec-sim-r045-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-sim-r045-legacy-compatibility-preservation.json",
    "phase-exec-sim-r045-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r045-cost-guidance-preservation.json",
    "phase-exec-sim-r045-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r045-no-new-simulation-audit.json",
    "phase-exec-sim-r045-no-new-backtest-audit.json",
    "phase-exec-sim-r045-no-new-tca-lines-audit.json",
    "phase-exec-sim-r045-no-db-import-audit.json",
    "phase-exec-sim-r045-no-persisted-sanitized-row-audit.json",
    "phase-exec-sim-r045-no-executable-schedule-audit.json",
    "phase-exec-sim-r045-no-child-slices-audit.json",
    "phase-exec-sim-r045-no-child-orders-audit.json",
    "phase-exec-sim-r045-no-real-fill-audit.json",
    "phase-exec-sim-r045-no-execution-report-audit.json",
    "phase-exec-sim-r045-no-order-created-audit.json",
    "phase-exec-sim-r045-no-route-no-submission-audit.json",
    "phase-exec-sim-r045-no-polygon-api-call-audit.json",
    "phase-exec-sim-r045-no-lmax-call-audit.json",
    "phase-exec-sim-r045-no-external-api-call-audit.json",
    "phase-exec-sim-r045-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r045-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r045-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r045-no-external-audit.json",
    "phase-exec-sim-r045-forbidden-actions-audit.json",
    "phase-exec-sim-r045-build-test-validator-evidence.json"
)

foreach ($file in $requiredFiles) {
    $path = Join-Path $ArtifactsRoot $file
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing required artifact $file"
    }
}

$contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r045-r044-tca-review-contract.json")
if ($contract.ReviewContractId -ne "EXEC-SIM-R045-ADDITIONAL-HISTORICAL-TCA-REVIEW-CONTRACT") {
    Fail "Review contract id mismatch"
}
Assert-TrueField $contract "ReviewOnly" "Review contract is not review-only"
Assert-TrueField $contract "NoNewSimulation" "Review contract does not prohibit new simulation"
Assert-TrueField $contract "NoNewBacktest" "Review contract does not prohibit new backtest"
Assert-TrueField $contract "NoNewTcaResultLines" "Review contract does not prohibit new TCA lines"
Assert-TrueField $contract "NoDbImport" "Review contract does not prohibit DB import"
Assert-TrueField $contract "NoOrderDomainOutput" "Review contract does not prohibit order-domain output"
if ($contract.MissingEvidencePolicy -notlike "*MissingEvidence*") {
    Fail "Missing-evidence policy is absent"
}

$operator = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r045-operator-review-report.json")
foreach ($classification in @(
    "EXEC_SIM_R045_PASS_ADDITIONAL_HISTORICAL_TCA_REVIEW_READY_NO_EXTERNAL",
    "EXEC_SIM_R045_PASS_EXECUTION_POLICY_DECISION_READY_NO_EXTERNAL",
    "EXEC_SIM_R045_PASS_PARAMETER_REFINEMENT_DECISION_READY_NO_EXTERNAL",
    "EXEC_SIM_R045_PASS_NO_NEW_SIMULATION_NO_ORDER_GATE_READY_NO_EXTERNAL",
    "EXEC_SIM_R045_PASS_CLOSE_SEEKING_ADAPTIVE_KEEP_READY_NO_EXTERNAL",
    "EXEC_SIM_R045_PASS_CONTROLLED_RESIDUAL_CROSS_CONDITIONAL_READY_NO_EXTERNAL",
    "EXEC_SIM_R045_PASS_WAKETT_PATTERNS_REJECTED_READY_NO_EXTERNAL"
)) {
    if (-not ($operator.Classifications -contains $classification)) {
        Fail "Missing classification $classification"
    }
}

$numeric = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r045-numeric-tca-summary.json")
if ($numeric.TcaResultLines -ne 10395 -or $numeric.QuoteWindows -ne 945 -or $numeric.PoliciesPerWindow -ne 11) {
    Fail "Numeric TCA summary count mismatch"
}
if ($numeric.RejectedRowsExcluded -ne 0) {
    Fail "Numeric summary did not preserve rejected row count 0"
}
if ($numeric.PerPolicyUsdPerMillionSummary.Count -ne 11) {
    Fail "USD/million summary is incomplete"
}

$ranking = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r045-policy-ranking-review.json")
foreach ($name in @("MedianSlippage","P95Slippage","FillRatio","Residual","SpreadPaid")) {
    if ($ranking.Rankings.$name.Ranking.Count -ne 11) {
        Fail "Ranking $name is incomplete"
    }
}

$perDate = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r045-per-date-review.json")
if ($perDate.Reports.Count -ne 5 -or $perDate.BestWorstByDate.Count -ne 5) {
    Fail "Per-date review missing entries"
}
$perSymbol = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r045-per-symbol-review.json")
if ($perSymbol.Reports.Count -ne 7 -or $perSymbol.BestWorstBySymbol.Count -ne 7) {
    Fail "Per-symbol review missing entries"
}
$audusd = $perSymbol.Reports | Where-Object { $_.Symbol -eq "AUDUSD" }
if (-not $audusd -or $audusd.AudUsdNotFailed -ne $true) {
    Fail "AUDUSD is missing or misclassified as failed"
}
$perSymbolDate = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r045-per-symbol-date-review.json")
if ($perSymbolDate.ReportCount -ne 35 -or $perSymbolDate.Reports.Count -ne 35) {
    Fail "Per-symbol/date review missing entries"
}

$aggregate = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r045-canonical-session-aggregate-review.json")
if ($aggregate.Review.ResultLineCount -ne 10395) {
    Fail "Canonical-session aggregate review missing result count"
}

$wakettVsClose = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r045-wakett-vs-close-seeking-review.json")
if ($wakettVsClose.Decision -notlike "*CloseSeekingAdaptiveKeepForParameterRefinement*" -or $wakettVsClose.Decision -notlike "*Wakett defaults rejected*") {
    Fail "Wakett vs CloseSeeking review decision is incorrect"
}

$adaptive = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r045-close-seeking-adaptive-review.json")
if ($adaptive.RecommendationStatus -ne "CloseSeekingAdaptiveKeepForParameterRefinement" -or $adaptive.MainCandidate -ne $true) {
    Fail "CloseSeekingAdaptive review is not keeping the main candidate"
}
$controlled = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r045-controlled-residual-cross-review.json")
if ($controlled.RecommendationStatus -ne "ControlledResidualCrossConditionalKeep" -or $controlled.EspeciallyUsefulNearNoOvernightResidualPressure -ne $true) {
    Fail "ControlledResidualCross conditional review is invalid"
}
$passive = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r045-passive-until-urgency-review.json")
if ($passive.RecommendationStatus -ne "PassiveUntilUrgencyNeedsRefinement") {
    Fail "PassiveUntilUrgency decision is invalid"
}
$wakettLimit = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r045-wakett-limit-residual-risk-review.json")
if ($wakettLimit.RecommendationStatus -ne "WakettPureLimitReject") {
    Fail "Wakett pure limit rejection is missing"
}
$wakettFive = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r045-wakett-five-slices-spread-risk-review.json")
if ($wakettFive.RecommendationStatus -ne "WakettFiveSlicesReject") {
    Fail "Wakett five slices rejection is missing"
}
$benchmark = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r045-benchmark-only-review.json")
if ($benchmark.TwapVwapRemainBenchmarkOnly -ne $true) {
    Fail "TWAP/VWAP benchmark-only status weakened"
}

$fiveUsd = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r045-5usd-per-million-review.json")
if ($fiveUsd.FiveUsdPerMillionGuidance -ne "BestCaseMajorOnly" -or $fiveUsd.Universalized -ne $false) {
    Fail "5 USD/million guidance was universalized"
}
$inverted = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r045-inverted-symbol-review.json")
if ($inverted.UsdJpyCaveatPreserved -ne $true -or $inverted.InvertedSymbols.Count -lt 3) {
    Fail "Inverted symbol review or USDJPY caveat is weakened"
}
$duplicate = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r045-duplicate-aware-review.json")
if ($duplicate.DuplicateAwareHandlingPreserved -ne $true -or $duplicate.OutOfOrderRows -ne 0) {
    Fail "Duplicate-aware review is invalid"
}

$policyDecision = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r045-execution-policy-decision.json")
if ($policyDecision.ExecutablePromotionBlocked -ne $true -or $policyDecision.DecisionCategories.Count -lt 5) {
    Fail "Execution policy decision is missing or executable promotion is not blocked"
}
$paramDecision = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r045-parameter-refinement-decision.json")
if ($paramDecision.RecommendationStatus -ne "ParameterRefinementRecommended" -or $paramDecision.PrimaryCandidate -ne "CloseSeeking15mAdaptive") {
    Fail "Parameter refinement decision is invalid"
}
$dataDecision = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r045-data-expansion-decision.json")
if (-not ($dataDecision.DecisionCategories -contains "ExpandMoreDates")) {
    Fail "Data expansion decision does not recommend more dates"
}

$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r045-canonical-quarter-hour-policy-preservation.json")
if ($canonical.Legacy06UsedAsFutureCanonical -ne $false -or $canonical.Preserved -ne $true) {
    Fail "Canonical quarter-hour policy weakened"
}
$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r045-legacy-compatibility-preservation.json")
if ($legacy.LegacyCompatibilityOnly -ne $true -or $legacy.Legacy06UsedAsFutureCanonical -ne $false) {
    Fail "Legacy :06 used as future canonical"
}
$direct = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r045-direct-cross-exclusion-preservation.json")
if ($direct.DirectCrossExecutionDisabled -ne $true -or $direct.DirectCrossesIncluded -ne $false -or $direct.ExecutionUniverse -ne "USD-pair-only") {
    Fail "Direct-cross exclusion weakened"
}
$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r045-cost-guidance-preservation.json")
if ($cost.FiveUsdPerMillionGuidance -ne "BestCaseMajorOnly" -or $cost.Universalized -ne $false) {
    Fail "Cost guidance weakened"
}
$nonmajor = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r045-nonmajor-calibration-preservation.json")
if ($nonmajor.NonmajorEmScandiCnhCalibrationRequired -ne $true) {
    Fail "Nonmajor calibration requirement weakened"
}
$usdjpy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r045-usdjpy-caveat-preservation.json")
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or $usdjpy.RequiresInversion -ne $true -or "$($usdjpy.SecurityID)" -ne "4004" -or "$($usdjpy.SecurityIDSource)" -ne "8" -or $usdjpy.Failed -ne $false) {
    Fail "USDJPY caveat weakened"
}

$auditFalseChecks = @(
    @("phase-exec-sim-r045-no-new-simulation-audit.json", "NewSimulationExecuted", "New simulation executed"),
    @("phase-exec-sim-r045-no-new-backtest-audit.json", "NewBacktestExecuted", "New backtest executed"),
    @("phase-exec-sim-r045-no-new-tca-lines-audit.json", "NewTcaResultLinesCreated", "New TCA lines created"),
    @("phase-exec-sim-r045-no-db-import-audit.json", "DbImportExecuted", "DB import executed"),
    @("phase-exec-sim-r045-no-persisted-sanitized-row-audit.json", "PersistedSanitizedRowsCreated", "Persisted sanitized rows created"),
    @("phase-exec-sim-r045-no-executable-schedule-audit.json", "ExecutableSchedulesCreated", "Executable schedules created"),
    @("phase-exec-sim-r045-no-child-slices-audit.json", "ChildSlicesCreated", "Child slices created"),
    @("phase-exec-sim-r045-no-child-orders-audit.json", "ChildOrdersCreated", "Child orders created"),
    @("phase-exec-sim-r045-no-real-fill-audit.json", "RealFillsCreated", "Real fills created"),
    @("phase-exec-sim-r045-no-execution-report-audit.json", "ExecutionReportsCreated", "Execution reports created"),
    @("phase-exec-sim-r045-no-order-created-audit.json", "OrdersCreated", "Orders created"),
    @("phase-exec-sim-r045-no-polygon-api-call-audit.json", "PolygonApiCalled", "Polygon API called"),
    @("phase-exec-sim-r045-no-lmax-call-audit.json", "LmaxCalled", "LMAX called"),
    @("phase-exec-sim-r045-no-external-api-call-audit.json", "ExternalApiCalled", "External API called"),
    @("phase-exec-sim-r045-no-broker-marketdata-runtime-audit.json", "BrokerMarketDataRuntimeStarted", "Broker/MarketData runtime started")
)
foreach ($check in $auditFalseChecks) {
    $doc = Read-Json (Join-Path $ArtifactsRoot $check[0])
    Assert-FalseField $doc $check[1] $check[2]
}
$routeAudit = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r045-no-route-no-submission-audit.json")
Assert-FalseField $routeAudit "RoutesCreated" "Routes created"
Assert-FalseField $routeAudit "SubmissionsCreated" "Submissions created"

$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r045-build-test-validator-evidence.json")
if ($evidence.DotnetBuild.Status -notin @("PASS", "PASS_WITH_WARNINGS")) {
    Fail "Build evidence missing or failing"
}
if ($evidence.FocusedR045StaticChecks.Status -ne "PASS") {
    Fail "Focused R045 static checks missing or failing"
}
if ($evidence.UnitTests.Status -notin @("PASS", "PASS_WITH_WARNINGS", "NOT_FEASIBLE")) {
    Fail "Unit test evidence missing"
}

Write-Host "EXEC-SIM-R045 validator passed."
Write-Host "Classifications:"
Write-Host "EXEC_SIM_R045_PASS_ADDITIONAL_HISTORICAL_TCA_REVIEW_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R045_PASS_EXECUTION_POLICY_DECISION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R045_PASS_PARAMETER_REFINEMENT_DECISION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R045_PASS_NO_NEW_SIMULATION_NO_ORDER_GATE_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R045_PASS_CLOSE_SEEKING_ADAPTIVE_KEEP_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R045_PASS_CONTROLLED_RESIDUAL_CROSS_CONDITIONAL_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R045_PASS_WAKETT_PATTERNS_REJECTED_READY_NO_EXTERNAL"
