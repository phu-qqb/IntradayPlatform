param(
    [string]$ArtifactsRoot = "artifacts/readiness/execution-sim"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Error "EXEC-SIM-R047 validation failed: $Message"
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
    "phase-exec-sim-r047-summary.md",
    "phase-exec-sim-r047-r046-authorization-reference.json",
    "phase-exec-sim-r047-r007-candidate-reference.json",
    "phase-exec-sim-r047-r045-decision-reference.json",
    "phase-exec-sim-r047-r044-context-reference.json",
    "phase-exec-sim-r047-parameter-refinement-candidate-simulation-contract.json",
    "phase-exec-sim-r047-parameter-refinement-candidate-simulation-run-result.json",
    "phase-exec-sim-r047-candidate-variant-definitions.json",
    "phase-exec-sim-r047-candidate-tca-result-lines.json",
    "phase-exec-sim-r047-result-line-count-and-coverage.json",
    "phase-exec-sim-r047-candidate-vs-current-policy-comparison.json",
    "phase-exec-sim-r047-candidate-vs-r044-comparison.json",
    "phase-exec-sim-r047-per-date-candidate-reports.json",
    "phase-exec-sim-r047-per-symbol-candidate-reports.json",
    "phase-exec-sim-r047-per-symbol-date-candidate-reports.json",
    "phase-exec-sim-r047-close-seeking-adaptive-refined-report.json",
    "phase-exec-sim-r047-controlled-residual-cross-conditional-report.json",
    "phase-exec-sim-r047-passive-until-urgency-refined-report.json",
    "phase-exec-sim-r047-residual-risk-comparison.json",
    "phase-exec-sim-r047-spread-paid-comparison.json",
    "phase-exec-sim-r047-no-overnight-residual-pressure-review.json",
    "phase-exec-sim-r047-threshold-evidence-calibration-review.json",
    "phase-exec-sim-r047-policy-decision.json",
    "phase-exec-sim-r047-parameter-refinement-decision.json",
    "phase-exec-sim-r047-next-design-only-parameter-contract-recommendation.json",
    "phase-exec-sim-r047-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-sim-r047-legacy-compatibility-preservation.json",
    "phase-exec-sim-r047-usd-pair-normalization-preservation.json",
    "phase-exec-sim-r047-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r047-cost-guidance-preservation.json",
    "phase-exec-sim-r047-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r047-no-db-import-audit.json",
    "phase-exec-sim-r047-no-persisted-sanitized-row-audit.json",
    "phase-exec-sim-r047-no-executable-schedule-audit.json",
    "phase-exec-sim-r047-no-child-slices-audit.json",
    "phase-exec-sim-r047-no-child-orders-audit.json",
    "phase-exec-sim-r047-no-real-fill-audit.json",
    "phase-exec-sim-r047-no-execution-report-audit.json",
    "phase-exec-sim-r047-no-order-created-audit.json",
    "phase-exec-sim-r047-no-route-no-submission-audit.json",
    "phase-exec-sim-r047-no-polygon-api-call-audit.json",
    "phase-exec-sim-r047-no-lmax-call-audit.json",
    "phase-exec-sim-r047-no-external-api-call-audit.json",
    "phase-exec-sim-r047-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r047-no-candidate-executable-promotion-audit.json",
    "phase-exec-sim-r047-no-unsupported-threshold-calibration-audit.json",
    "phase-exec-sim-r047-no-row-revalidation-audit.json",
    "phase-exec-sim-r047-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r047-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r047-no-external-audit.json",
    "phase-exec-sim-r047-forbidden-actions-audit.json",
    "phase-exec-sim-r047-next-phase-recommendation.json",
    "phase-exec-sim-r047-build-test-validator-evidence.json"
)

foreach ($file in $requiredFiles) {
    $path = Join-Path $ArtifactsRoot $file
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing required artifact $file"
    }
}

$contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r047-parameter-refinement-candidate-simulation-contract.json")
if ($contract.ParameterRefinementCandidateSimulationRunId -ne "EXEC-SIM-R047-PARAMETER-REFINEMENT-CANDIDATE-SIMULATION-REVIEW") {
    Fail "Simulation contract id mismatch"
}
if ($contract.SourceAuthorizationPhase -ne "EXEC-SIM-R046" -or $contract.SourceCandidatePhase -ne "EXEC-ALGO-R007" -or $contract.SourceBacktestPhase -ne "EXEC-SIM-R044") {
    Fail "Simulation contract source phases invalid"
}
if ($contract.QuoteWindows -ne 945 -or $contract.CandidateFamilies.Count -ne 3) {
    Fail "Simulation contract coverage invalid"
}
Assert-TrueField $contract "NoApiCall" "Contract allows API calls"
Assert-TrueField $contract "NoDbImport" "Contract allows DB import"
Assert-TrueField $contract "NoPersistedSanitizedRows" "Contract allows persisted sanitized rows"
Assert-TrueField $contract "NoExecutableSchedule" "Contract allows executable schedule"
Assert-TrueField $contract "NoOrderDomainOutput" "Contract allows order-domain output"
Assert-TrueField $contract "NoExecutablePromotion" "Contract allows executable promotion"

$run = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r047-parameter-refinement-candidate-simulation-run-result.json")
if ($run.SimulationStatus -ne "PartialReviewNeedsCalibrationNoExternal" -or $run.CandidateVariantCount -ne 3 -or $run.CandidateTcaResultLineCount -ne 0) {
    Fail "Run result should be partial needs-calibration with zero candidate lines"
}
Assert-FalseField $run "SimulationExecuted" "Simulation executed despite unsupported thresholds"
Assert-TrueField $run "NoFakeResults" "Run result did not confirm no fake results"
foreach ($classification in @(
    "EXEC_SIM_R047_PARTIAL_REVIEW_NEEDS_CALIBRATION_NO_EXTERNAL",
    "EXEC_SIM_R047_PASS_CANDIDATE_REVIEW_AND_POLICY_DECISION_READY_NO_EXTERNAL",
    "EXEC_SIM_R047_PASS_NEXT_DESIGN_PARAMETER_RECOMMENDATION_READY_NO_EXTERNAL",
    "EXEC_SIM_R047_PASS_NO_DB_IMPORT_NO_REAL_FILL_NO_ORDER_GATE_READY_NO_EXTERNAL"
)) {
    if (-not ($run.Classifications -contains $classification)) {
        Fail "Missing classification $classification"
    }
}

$variants = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r047-candidate-variant-definitions.json")
if ($variants.CandidateVariants.Count -ne 3 -or $variants.UnsupportedFinalThresholdsClaimed -ne $false -or $variants.ExecutablePromotionAuthorized -ne $false) {
    Fail "Candidate variant definitions invalid"
}
foreach ($family in @("CloseSeeking15mAdaptive", "ControlledResidualCross", "PassiveUntilUrgency")) {
    $entry = $variants.CandidateVariants | Where-Object { $_.CandidatePolicyFamily -eq $family }
    if (-not $entry) {
        Fail "$family candidate variant missing"
    }
    if ($entry.DesignOnly -ne $true -or $entry.PaperOnly -ne $true -or $entry.NonExecutable -ne $true -or $entry.NotAnOrder -ne $true -or $entry.NotSubmitted -ne $true -or $entry.NoBrokerRoute -ne $true) {
        Fail "$family has executable/order-domain risk"
    }
    if ($entry.FinalNumericThresholdsCalibrated -ne $false -or $entry.SimulatableWithoutUnsupportedThresholds -ne $false) {
        Fail "$family claims unsupported thresholds are calibrated"
    }
}
$crc = $variants.CandidateVariants | Where-Object { $_.CandidatePolicyFamily -eq "ControlledResidualCross" }
if ($crc.ConditionalOnly -ne $true -or $crc.NotDefault -ne $true -or $crc.NotAlwaysMarketAtClose -ne $true -or $crc.RequiresCostJustification -ne $true) {
    Fail "ControlledResidualCross conditional controls weakened"
}

$lines = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r047-candidate-tca-result-lines.json")
if ($lines.CandidateTcaResultLineCount -ne 0 -or $lines.Lines.Count -ne 0 -or $lines.NoFakeResultLines -ne $true) {
    Fail "Candidate TCA result lines are invalid"
}
Assert-FalseField $lines "ContainsFills" "TCA lines contain fills"
Assert-FalseField $lines "ContainsOrders" "TCA lines contain orders"
Assert-FalseField $lines "ContainsExecutionReports" "TCA lines contain execution reports"
Assert-FalseField $lines "ContainsRoutes" "TCA lines contain routes"
Assert-FalseField $lines "ContainsSubmissions" "TCA lines contain submissions"

$coverage = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r047-result-line-count-and-coverage.json")
if ($coverage.R044QuoteWindowsAvailable -ne 945 -or $coverage.CandidateVariantsDefined -ne 3 -or $coverage.CandidateTcaResultLinesProduced -ne 0 -or $coverage.ExpectedCandidateLinesIfCalibrated -ne 2835) {
    Fail "Result line count/coverage invalid"
}

$policy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r047-policy-decision.json")
if ($policy.WakettPureLimitUntilClose -ne "RejectedNegativeBaselineOnly" -or $policy.WakettFiveMarketSlicesAroundClose -ne "RejectedNegativeBaselineOnly") {
    Fail "Wakett patterns promoted or rejection weakened"
}
if ($policy.VWAPBenchmarkOnly -ne "BenchmarkOnlyNotExecutable" -or $policy.TWAPBenchmarkOnly -ne "BenchmarkOnlyNotExecutable") {
    Fail "VWAP/TWAP promoted from benchmark-only"
}
if ($policy.ManualReview -ne "SafetyOutcomeOnly" -or $policy.DoNotTrade -ne "SafetyOutcomeOnly") {
    Fail "ManualReview/DoNotTrade promoted from safety outcomes"
}
Assert-FalseField $policy "ExecutablePromotionAuthorized" "Policy decision authorizes executable promotion"

$thresholds = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r047-threshold-evidence-calibration-review.json")
if ($thresholds.ThresholdEvidenceStatus -ne "NeedsFurtherCalibration" -or $thresholds.UnsupportedFinalThresholdsClaimedCalibrated -ne $false -or $thresholds.FinalNumericThresholdsAuthorized -ne $false -or $thresholds.BlockedSimulation -ne $true) {
    Fail "Threshold evidence/calibration review invalid"
}
$thresholdAudit = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r047-no-unsupported-threshold-calibration-audit.json")
Assert-FalseField $thresholdAudit "UnsupportedFinalThresholdsClaimedCalibrated" "Unsupported thresholds claimed calibrated"
Assert-FalseField $thresholdAudit "FinalNumericThresholdsAuthorized" "Final numeric thresholds authorized"

$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r047-canonical-quarter-hour-policy-preservation.json")
if ($canonical.Legacy06UsedAsFutureCanonical -ne $false -or $canonical.Preserved -ne $true) {
    Fail "Canonical quarter-hour policy weakened"
}
$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r047-legacy-compatibility-preservation.json")
if ($legacy.LegacyCompatibilityOnly -ne $true -or $legacy.Legacy06UsedAsFutureCanonical -ne $false) {
    Fail "Legacy :06 used as future canonical"
}
$normalization = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r047-usd-pair-normalization-preservation.json")
if ($normalization.AppliesToExecutionUniverse -ne "USDPairOnly" -or $normalization.Symbols.Count -ne 7) {
    Fail "USD-pair normalization missing"
}
$audusd = $normalization.Symbols | Where-Object { $_.Symbol -eq "AUDUSD" }
if (-not $audusd -or $audusd.AudUsdNotFailed -ne $true) {
    Fail "AUDUSD misclassified"
}
$direct = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r047-direct-cross-exclusion-preservation.json")
if ($direct.DirectCrossExecutionDisabled -ne $true -or $direct.DirectCrossesIncluded -ne $false -or $direct.ExecutionUniverse -ne "USD-pair-only") {
    Fail "Direct-cross exclusion weakened"
}
$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r047-cost-guidance-preservation.json")
if ($cost.FiveUsdPerMillionGuidance -ne "BestCaseMajorOnly" -or $cost.Universalized -ne $false) {
    Fail "5 USD/million universalized"
}
$usdjpy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r047-usdjpy-caveat-preservation.json")
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or $usdjpy.RequiresInversion -ne $true -or "$($usdjpy.SecurityID)" -ne "4004" -or "$($usdjpy.SecurityIDSource)" -ne "8" -or $usdjpy.Failed -ne $false) {
    Fail "USDJPY caveat weakened"
}

$auditFalseChecks = @(
    @("phase-exec-sim-r047-no-db-import-audit.json", "DbImportExecuted", "DB import executed"),
    @("phase-exec-sim-r047-no-persisted-sanitized-row-audit.json", "PersistedSanitizedRowsCreated", "Persisted sanitized rows created"),
    @("phase-exec-sim-r047-no-executable-schedule-audit.json", "ExecutableSchedulesCreated", "Executable schedules created"),
    @("phase-exec-sim-r047-no-child-slices-audit.json", "ChildSlicesCreated", "Child slices created"),
    @("phase-exec-sim-r047-no-child-orders-audit.json", "ChildOrdersCreated", "Child orders created"),
    @("phase-exec-sim-r047-no-real-fill-audit.json", "RealFillsCreated", "Real fills created"),
    @("phase-exec-sim-r047-no-execution-report-audit.json", "ExecutionReportsCreated", "Execution reports created"),
    @("phase-exec-sim-r047-no-order-created-audit.json", "OrdersCreated", "Orders created"),
    @("phase-exec-sim-r047-no-polygon-api-call-audit.json", "PolygonApiCalled", "Polygon API called"),
    @("phase-exec-sim-r047-no-lmax-call-audit.json", "LmaxCalled", "LMAX called"),
    @("phase-exec-sim-r047-no-external-api-call-audit.json", "ExternalApiCalled", "External API called"),
    @("phase-exec-sim-r047-no-broker-marketdata-runtime-audit.json", "BrokerMarketDataRuntimeStarted", "Broker runtime started"),
    @("phase-exec-sim-r047-no-candidate-executable-promotion-audit.json", "CandidatesPromotedToExecutableUse", "Candidates promoted to executable use"),
    @("phase-exec-sim-r047-no-row-revalidation-audit.json", "QuoteRowsRevalidated", "Quote rows revalidated")
)
foreach ($check in $auditFalseChecks) {
    $doc = Read-Json (Join-Path $ArtifactsRoot $check[0])
    Assert-FalseField $doc $check[1] $check[2]
}
$route = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r047-no-route-no-submission-audit.json")
Assert-FalseField $route "RoutesCreated" "Routes created"
Assert-FalseField $route "SubmissionsCreated" "Submissions created"
$forbidden = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r047-forbidden-actions-audit.json")
Assert-FalseField $forbidden "ForbiddenActionsDetected" "Forbidden actions detected"
Assert-FalseField $forbidden "StateMutated" "State mutated"

$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r047-build-test-validator-evidence.json")
if ($evidence.DotnetBuild.Status -notin @("PASS", "PASS_WITH_WARNINGS")) {
    Fail "Build evidence missing or failing"
}
if ($evidence.FocusedR047StaticChecks.Status -ne "PASS") {
    Fail "Focused R047 static checks missing or failing"
}
if ($evidence.UnitTests.Status -notin @("PASS", "PASS_WITH_WARNINGS", "NOT_FEASIBLE")) {
    Fail "Unit test evidence missing"
}

Write-Host "EXEC-SIM-R047 validator passed."
Write-Host "Classifications:"
Write-Host "EXEC_SIM_R047_PARTIAL_REVIEW_NEEDS_CALIBRATION_NO_EXTERNAL"
Write-Host "EXEC_SIM_R047_PASS_CANDIDATE_REVIEW_AND_POLICY_DECISION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R047_PASS_NEXT_DESIGN_PARAMETER_RECOMMENDATION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R047_PASS_NO_DB_IMPORT_NO_REAL_FILL_NO_ORDER_GATE_READY_NO_EXTERNAL"
