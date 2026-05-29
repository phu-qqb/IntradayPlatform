$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$ArtifactsRoot = Join-Path $RepoRoot "artifacts\readiness\execution-algo"

function Fail([string]$Message) {
    Write-Error "EXEC-ALGO-R009 validation failed: $Message"
    exit 1
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing artifact: $Path"
    }
    return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Assert-FalseField($Object, [string]$FieldName, [string]$Message) {
    if (-not ($Object.PSObject.Properties.Name -contains $FieldName)) {
        Fail "Missing field $FieldName"
    }
    if ($Object.$FieldName -ne $false) {
        Fail $Message
    }
}

function Assert-TrueField($Object, [string]$FieldName, [string]$Message) {
    if (-not ($Object.PSObject.Properties.Name -contains $FieldName)) {
        Fail "Missing field $FieldName"
    }
    if ($Object.$FieldName -ne $true) {
        Fail $Message
    }
}

$requiredArtifacts = @(
    "phase-exec-algo-r009-summary.md",
    "phase-exec-algo-r009-r049-recommendation-reference.json",
    "phase-exec-algo-r009-r008-range-contract-reference.json",
    "phase-exec-algo-r009-final-design-only-parameter-contract.json",
    "phase-exec-algo-r009-contract-versioning.json",
    "phase-exec-algo-r009-primary-close-seeking-balanced-adaptive.json",
    "phase-exec-algo-r009-secondary-close-seeking-residual-aware.json",
    "phase-exec-algo-r009-controlled-residual-conditional-module.json",
    "phase-exec-algo-r009-passive-until-urgency-hold.json",
    "phase-exec-algo-r009-rejected-wakett-preservation.json",
    "phase-exec-algo-r009-benchmark-only-preservation.json",
    "phase-exec-algo-r009-manual-review-do-not-trade-preservation.json",
    "phase-exec-algo-r009-threshold-status.json",
    "phase-exec-algo-r009-future-requirements-before-executable-use.json",
    "phase-exec-algo-r009-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-algo-r009-legacy-compatibility-preservation.json",
    "phase-exec-algo-r009-usd-pair-normalization-preservation.json",
    "phase-exec-algo-r009-direct-cross-exclusion-preservation.json",
    "phase-exec-algo-r009-cost-guidance-preservation.json",
    "phase-exec-algo-r009-nonmajor-calibration-preservation.json",
    "phase-exec-algo-r009-non-executable-contract-audit.json",
    "phase-exec-algo-r009-no-executable-schedule-audit.json",
    "phase-exec-algo-r009-no-child-slices-audit.json",
    "phase-exec-algo-r009-no-child-orders-audit.json",
    "phase-exec-algo-r009-no-new-backtest-audit.json",
    "phase-exec-algo-r009-no-new-simulation-audit.json",
    "phase-exec-algo-r009-no-tca-result-lines-audit.json",
    "phase-exec-algo-r009-no-polygon-api-call-audit.json",
    "phase-exec-algo-r009-no-lmax-call-audit.json",
    "phase-exec-algo-r009-no-external-api-call-audit.json",
    "phase-exec-algo-r009-no-broker-marketdata-runtime-audit.json",
    "phase-exec-algo-r009-no-real-fill-audit.json",
    "phase-exec-algo-r009-no-execution-report-audit.json",
    "phase-exec-algo-r009-no-order-created-audit.json",
    "phase-exec-algo-r009-no-route-no-submission-audit.json",
    "phase-exec-algo-r009-usdjpy-caveat-preservation.json",
    "phase-exec-algo-r009-lmax-readonly-baseline-reference.json",
    "phase-exec-algo-r009-no-external-audit.json",
    "phase-exec-algo-r009-forbidden-actions-audit.json",
    "phase-exec-algo-r009-next-phase-recommendation.json",
    "phase-exec-algo-r009-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    $path = Join-Path $ArtifactsRoot $artifact
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Required artifact missing: $artifact"
    }
}

$r049 = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r009-r049-recommendation-reference.json")
if ($r049.PrimaryDesignRecommendation -ne "CloseSeeking15mAdaptive_BalancedAdaptive_v0") {
    Fail "R049 primary recommendation missing or changed"
}
if ($r049.SecondaryDesignRecommendation -ne "CloseSeeking15mAdaptive_ResidualAwareUrgency_v0") {
    Fail "R049 secondary recommendation missing or changed"
}
Assert-FalseField $r049 "ExecutablePromotionAuthorized" "R049 reference authorizes executable promotion"

$r008 = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r009-r008-range-contract-reference.json")
if ($r008.ReferencedContractVersion -ne "0.2.0-design-only") {
    Fail "R008 range contract version missing"
}
Assert-TrueField $r008 "RangesInherited" "R008 ranges were not inherited"
Assert-FalseField $r008 "RangesAreFinalCalibratedThresholds" "R008 ranges claimed final thresholds"
Assert-FalseField $r008 "ExecutablePromotionAuthorized" "R008 reference authorizes executable promotion"

$contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r009-final-design-only-parameter-contract.json")
if ($contract.ContractVersion -ne "0.3.0-design-only-candidate") {
    Fail "Unexpected R009 contract version"
}
if ($contract.ContractStatus -ne "FinalDesignOnlyCandidate") {
    Fail "Unexpected R009 contract status"
}
if ($contract.PrimaryCandidate -ne "CloseSeeking15mAdaptive_BalancedAdaptive_v0") {
    Fail "Primary BalancedAdaptive candidate missing"
}
if ($contract.SecondaryCandidate -ne "CloseSeeking15mAdaptive_ResidualAwareUrgency_v0") {
    Fail "Secondary ResidualAware candidate missing"
}
if ($contract.ConditionalResidualModule -ne "ControlledResidualCross_BalancedResidualCross_v0") {
    Fail "Conditional ControlledResidual module missing"
}
foreach ($flag in @("AppliesToCanonicalQuarterHourTimestamps", "LegacyCompatibilityOnly", "DesignOnly", "PaperOnly", "NonExecutable", "NotAnOrder", "NotSubmitted", "NoBrokerRoute", "RequiresFutureSimulation", "RequiresFutureOperatorApproval", "RequiresFuturePaperDryRun", "RequiresSeparateExecutableGate")) {
    Assert-TrueField $contract $flag "Contract flag $flag is not true"
}
Assert-FalseField $contract "ExecutablePromotionAuthorized" "Executable promotion is authorized"
Assert-FalseField $contract "LiveTradingAuthorized" "Live trading is authorized"
Assert-FalseField $contract "OrderRoutingAuthorized" "Order routing is authorized"
Assert-FalseField $contract "LegacyOutputTimestampsAreFutureCanonical" "Legacy timestamps used as future canonical"
if ($contract.AppliesToExecutionUniverse -ne "USDPairOnly") {
    Fail "Execution universe is not USDPairOnly"
}

$primary = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r009-primary-close-seeking-balanced-adaptive.json")
if ($primary.CandidateId -ne "CloseSeeking15mAdaptive_BalancedAdaptive_v0") {
    Fail "Primary candidate artifact is not BalancedAdaptive"
}
Assert-TrueField $primary "SpreadGuardPreserved" "Primary spread guard not preserved"
Assert-TrueField $primary "FeedQualityGuardPreserved" "Primary feed guard not preserved"
Assert-TrueField $primary "AdaptiveUrgencyPreserved" "Primary adaptive urgency not preserved"
Assert-TrueField $primary "ResidualAwareEscalationPreserved" "Primary residual escalation not preserved"
Assert-TrueField $primary "NoBlindCrossing" "Primary allows blind crossing"
Assert-TrueField $primary "NoDefaultMarketAtClose" "Primary defaults market at close"
Assert-FalseField $primary "ExecutablePromotionAuthorized" "Primary candidate executable promotion authorized"

$secondary = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r009-secondary-close-seeking-residual-aware.json")
if ($secondary.CandidateId -ne "CloseSeeking15mAdaptive_ResidualAwareUrgency_v0") {
    Fail "Secondary candidate artifact is not ResidualAwareUrgency"
}
Assert-FalseField $secondary "ExecutablePromotionAuthorized" "Secondary candidate executable promotion authorized"

$controlled = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r009-controlled-residual-conditional-module.json")
if ($controlled.ModuleId -ne "ControlledResidualCross_BalancedResidualCross_v0") {
    Fail "Controlled residual conditional module missing"
}
foreach ($flag in @("NotDefault", "NeverAlwaysMarketAtClose", "NeverBlindCrossing", "RequiresResidualOpportunityCostJustification", "RequiresSpreadCrossingCostGuard", "RequiresFeedBenchmarkSafety", "ManualReviewFallback", "DesignOnly", "PaperOnly", "NonExecutable", "NotAnOrder", "NotSubmitted", "NoBrokerRoute")) {
    Assert-TrueField $controlled $flag "Controlled residual flag $flag is not true"
}
Assert-FalseField $controlled "ExecutablePromotionAuthorized" "Controlled residual module executable promotion authorized"

$thresholds = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r009-threshold-status.json")
Assert-FalseField $thresholds "FinalCalibratedThresholdsClaimed" "Thresholds claimed final/calibrated"
Assert-FalseField $thresholds "ExecutableThresholdsClaimed" "Thresholds claimed executable"
Assert-FalseField $thresholds "LiveThresholdsClaimed" "Thresholds claimed live"

$wakett = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r009-rejected-wakett-preservation.json")
Assert-FalseField $wakett "WakettRejectionWeakened" "Wakett rejection weakened"
Assert-FalseField $wakett "ExecutablePromotionAuthorized" "Wakett executable promotion authorized"
if ($wakett.WakettPureLimitUntilClose.NormalCandidate -ne $false -or $wakett.WakettFiveMarketSlicesAroundClose.NormalCandidate -ne $false) {
    Fail "Wakett promoted as normal candidate"
}

$benchmarks = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r009-benchmark-only-preservation.json")
Assert-FalseField $benchmarks "BenchmarkOnlyPoliciesPromoted" "Benchmark-only policies promoted"
Assert-FalseField $benchmarks "ExecutablePromotionAuthorized" "Benchmark executable promotion authorized"

$safety = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r009-manual-review-do-not-trade-preservation.json")
Assert-FalseField $safety "SafetyOutcomesPromoted" "ManualReview/DoNotTrade promoted"
Assert-FalseField $safety "ExecutablePromotionAuthorized" "Safety executable promotion authorized"

$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r009-canonical-quarter-hour-policy-preservation.json")
Assert-TrueField $canonical "AppliesToCanonicalQuarterHourTimestamps" "Canonical quarter-hour policy missing"
Assert-FalseField $canonical "CanonicalQuarterHourPolicyWeakened" "Canonical quarter-hour policy weakened"

$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r009-legacy-compatibility-preservation.json")
Assert-FalseField $legacy "LegacyOutputTimestampsAreFutureCanonical" "Legacy timestamps used as future canonical"
Assert-FalseField $legacy "Legacy06UsedAsFutureCanonical" "Legacy :06 used as future canonical"

$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r009-cost-guidance-preservation.json")
Assert-FalseField $cost "FiveUsdPerMillionUniversalized" "5 USD/million universalized"

$usd = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r009-usd-pair-normalization-preservation.json")
Assert-FalseField $usd "AudUsdMisclassified" "AUDUSD misclassified"
Assert-FalseField $usd "UsdJpyCaveatWeakened" "USDJPY caveat weakened"
if ($usd.Mappings.USDJPY.SecurityID -ne "4004" -or $usd.Mappings.USDJPY.SecurityIDSource -ne "8") {
    Fail "USDJPY SecurityID caveat missing"
}

$auditFalseChecks = @(
    @("phase-exec-algo-r009-no-executable-schedule-audit.json", "ExecutableSchedulesCreated", "Executable schedules created"),
    @("phase-exec-algo-r009-no-child-slices-audit.json", "ChildSlicesCreated", "Child slices created"),
    @("phase-exec-algo-r009-no-child-orders-audit.json", "ChildOrdersCreated", "Child orders created"),
    @("phase-exec-algo-r009-no-new-backtest-audit.json", "NewBacktestExecuted", "New backtest executed"),
    @("phase-exec-algo-r009-no-new-simulation-audit.json", "NewSimulationExecuted", "New simulation executed"),
    @("phase-exec-algo-r009-no-tca-result-lines-audit.json", "TcaResultLinesProduced", "TCA result lines produced"),
    @("phase-exec-algo-r009-no-polygon-api-call-audit.json", "PolygonApiCalled", "Polygon API called"),
    @("phase-exec-algo-r009-no-lmax-call-audit.json", "LmaxCalled", "LMAX called"),
    @("phase-exec-algo-r009-no-external-api-call-audit.json", "ExternalApiCalled", "External API called"),
    @("phase-exec-algo-r009-no-broker-marketdata-runtime-audit.json", "BrokerMarketDataRuntimeStarted", "Broker runtime started"),
    @("phase-exec-algo-r009-no-real-fill-audit.json", "RealFillsCreated", "Real fills created"),
    @("phase-exec-algo-r009-no-execution-report-audit.json", "ExecutionReportsCreated", "Execution reports created"),
    @("phase-exec-algo-r009-no-order-created-audit.json", "OrdersCreated", "Orders created")
)
foreach ($check in $auditFalseChecks) {
    $doc = Read-Json (Join-Path $ArtifactsRoot $check[0])
    Assert-FalseField $doc $check[1] $check[2]
}

$route = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r009-no-route-no-submission-audit.json")
Assert-FalseField $route "RoutesCreated" "Routes created"
Assert-FalseField $route "SubmissionsCreated" "Submissions created"

$noExternal = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r009-no-external-audit.json")
Assert-TrueField $noExternal "NoExternal" "NoExternal audit missing"
Assert-FalseField $noExternal "PolygonApiCalled" "Polygon API called"
Assert-FalseField $noExternal "LmaxCalled" "LMAX called"
Assert-FalseField $noExternal "ExternalApiCalled" "External API called"
Assert-FalseField $noExternal "FilesDownloaded" "Files downloaded"

$forbidden = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r009-forbidden-actions-audit.json")
foreach ($flag in @("ForbiddenActionsDetected", "QuoteRowsValidated", "DbImportOccurred", "PersistedSanitizedRowsCreated", "NewBacktestExecuted", "NewSimulationExecuted", "TcaResultLinesProduced", "ExecutableSchedulesCreated", "ChildSlicesCreated", "ChildOrdersCreated", "OrdersCreated", "FillsCreated", "ExecutionReportsCreated", "RoutesCreated", "SubmissionsCreated", "StateMutated", "ExecutablePromotionAuthorized")) {
    Assert-FalseField $forbidden $flag "Forbidden action detected: $flag"
}

$future = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r009-future-requirements-before-executable-use.json")
foreach ($flag in @("RequiresFutureSimulation", "RequiresFutureOperatorApproval", "RequiresFuturePaperDryRun", "RequiresSeparateExecutableGate")) {
    Assert-TrueField $future $flag "Future requirement missing: $flag"
}
Assert-FalseField $future "ExecutablePromotionAuthorized" "Future requirements authorize executable promotion"

$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-algo-r009-build-test-validator-evidence.json")
if ($evidence.DotnetBuild.Status -notin @("PASS", "PASS_WITH_WARNINGS")) {
    Fail "Build evidence missing or failing"
}
if ($evidence.FocusedR009StaticChecks.Status -ne "PASS") {
    Fail "Focused R009 static checks missing or failing"
}
if ($evidence.UnitTests.Status -notin @("PASS", "PASS_WITH_WARNINGS", "NOT_FEASIBLE")) {
    Fail "Unit test evidence missing"
}

Write-Host "EXEC-ALGO-R009 validator passed."
Write-Host "Classifications:"
Write-Host "EXEC_ALGO_R009_PASS_FINAL_DESIGN_ONLY_PARAMETER_CONTRACT_READY_NO_EXTERNAL"
Write-Host "EXEC_ALGO_R009_PASS_BALANCED_ADAPTIVE_PRIMARY_CANDIDATE_READY_NO_EXTERNAL"
Write-Host "EXEC_ALGO_R009_PASS_CONDITIONAL_RESIDUAL_MODULE_READY_NO_EXTERNAL"
Write-Host "EXEC_ALGO_R009_PASS_NONEXECUTABLE_CONTRACT_CANDIDATE_GATE_READY_NO_EXTERNAL"
