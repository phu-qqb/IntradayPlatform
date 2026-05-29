param(
    [string]$ArtifactsRoot = "artifacts/readiness/execution-sim"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Error "EXEC-SIM-R046 validation failed: $Message"
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
    "phase-exec-sim-r046-summary.md",
    "phase-exec-sim-r046-r007-candidate-reference.json",
    "phase-exec-sim-r046-r045-decision-reference.json",
    "phase-exec-sim-r046-r044-backtest-context-reference.json",
    "phase-exec-sim-r046-parameter-refinement-simulation-authorization-contract.json",
    "phase-exec-sim-r046-parameter-refinement-simulation-authorization-request.json",
    "phase-exec-sim-r046-parameter-refinement-simulation-preflight-contract.json",
    "phase-exec-sim-r046-parameter-refinement-simulation-authorization-result.json",
    "phase-exec-sim-r046-authorized-candidate-families.json",
    "phase-exec-sim-r046-baseline-families.json",
    "phase-exec-sim-r046-benchmark-only-families.json",
    "phase-exec-sim-r046-safety-outcome-families.json",
    "phase-exec-sim-r046-negative-baseline-families.json",
    "phase-exec-sim-r046-r047-expected-simulation-scope.json",
    "phase-exec-sim-r046-r047-expected-report-list.json",
    "phase-exec-sim-r046-threshold-evidence-preflight.json",
    "phase-exec-sim-r046-non-executable-candidate-preflight.json",
    "phase-exec-sim-r046-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-sim-r046-legacy-compatibility-preservation.json",
    "phase-exec-sim-r046-usd-pair-normalization-preservation.json",
    "phase-exec-sim-r046-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r046-cost-guidance-preservation.json",
    "phase-exec-sim-r046-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r046-no-new-simulation-audit.json",
    "phase-exec-sim-r046-no-new-backtest-audit.json",
    "phase-exec-sim-r046-no-tca-result-lines-audit.json",
    "phase-exec-sim-r046-no-db-import-audit.json",
    "phase-exec-sim-r046-no-persisted-sanitized-row-audit.json",
    "phase-exec-sim-r046-no-executable-schedule-audit.json",
    "phase-exec-sim-r046-no-child-slices-audit.json",
    "phase-exec-sim-r046-no-child-orders-audit.json",
    "phase-exec-sim-r046-no-real-fill-audit.json",
    "phase-exec-sim-r046-no-execution-report-audit.json",
    "phase-exec-sim-r046-no-order-created-audit.json",
    "phase-exec-sim-r046-no-route-no-submission-audit.json",
    "phase-exec-sim-r046-no-polygon-api-call-audit.json",
    "phase-exec-sim-r046-no-lmax-call-audit.json",
    "phase-exec-sim-r046-no-external-api-call-audit.json",
    "phase-exec-sim-r046-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r046-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r046-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r046-no-external-audit.json",
    "phase-exec-sim-r046-forbidden-actions-audit.json",
    "phase-exec-sim-r046-next-phase-recommendation.json",
    "phase-exec-sim-r046-build-test-validator-evidence.json"
)

foreach ($file in $requiredFiles) {
    $path = Join-Path $ArtifactsRoot $file
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing required artifact $file"
    }
}

$r007 = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r046-r007-candidate-reference.json")
if ($r007.SourcePhase -ne "EXEC-ALGO-R007" -or $r007.CandidateCount -ne 3 -or $r007.AllCandidatesNonExecutable -ne $true -or $r007.ExecutablePromotionAuthorized -ne $false) {
    Fail "R007 candidate reference is invalid"
}

$r045 = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r046-r045-decision-reference.json")
if ($r045.SourcePhase -ne "EXEC-SIM-R045" -or $r045.ExecutablePromotionBlocked -ne $true -or $r045.WakettPatternsRejected -ne $true) {
    Fail "R045 decision reference is invalid"
}

$r044 = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r046-r044-backtest-context-reference.json")
if ($r044.SourcePhase -ne "EXEC-SIM-R044" -or $r044.QuoteWindows -ne 945 -or $r044.TcaResultLines -ne 10395 -or $r044.NoDbImport -ne $true -or $r044.NoOrderDomainOutput -ne $true) {
    Fail "R044 backtest context reference is invalid"
}

$contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r046-parameter-refinement-simulation-authorization-contract.json")
if ($contract.ParameterRefinementSimulationAuthorizationId -ne "EXEC-SIM-R046-PARAMETER-REFINEMENT-SIMULATION-AUTHORIZATION") {
    Fail "Authorization contract id mismatch"
}
if ($contract.CandidateCount -ne 3 -or $contract.IntendedNextPhase -ne "EXEC-SIM-R047") {
    Fail "Authorization contract candidate count or next phase mismatch"
}
Assert-TrueField $contract "AuthorizationOnly" "Contract is not authorization-only"
Assert-TrueField $contract "NoSimulation" "Contract does not prohibit simulation"
Assert-TrueField $contract "NoBacktest" "Contract does not prohibit backtest"
Assert-TrueField $contract "NoTcaResultLines" "Contract does not prohibit TCA result lines"
Assert-TrueField $contract "NoOrderDomainOutput" "Contract does not prohibit order-domain output"
Assert-TrueField $contract "NoExecutablePromotion" "Contract does not prohibit executable promotion"

$request = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r046-parameter-refinement-simulation-authorization-request.json")
if ($request.AuthorizationRequestOnly -ne $true -or $request.CandidateCount -ne 3) {
    Fail "Authorization request is invalid"
}

$preflight = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r046-parameter-refinement-simulation-preflight-contract.json")
if ($preflight.AllPassed -ne $true) {
    Fail "Preflight did not pass"
}

$result = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r046-parameter-refinement-simulation-authorization-result.json")
if ($result.AuthorizationReady -ne $true -or $result.CandidateCount -ne 3) {
    Fail "Authorization result is not ready"
}
foreach ($classification in @(
    "EXEC_SIM_R046_PASS_PARAMETER_REFINEMENT_SIM_AUTHORIZATION_READY_NO_EXTERNAL",
    "EXEC_SIM_R046_PASS_REFINEMENT_CANDIDATES_ACCEPTED_FOR_SIMULATION_NO_EXTERNAL",
    "EXEC_SIM_R046_PASS_NEGATIVE_BASELINES_AND_BENCHMARKS_PRESERVED_NO_EXTERNAL",
    "EXEC_SIM_R046_PASS_NO_SIMULATION_NO_ORDER_GATE_READY_NO_EXTERNAL"
)) {
    if (-not ($result.Classifications -contains $classification)) {
        Fail "Missing classification $classification"
    }
}
Assert-TrueField $result "NoSimulationExecuted" "Simulation executed"
Assert-TrueField $result "NoBacktestExecuted" "Backtest executed"
Assert-TrueField $result "NoTcaResultLinesCreated" "TCA result lines created"

$authorized = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r046-authorized-candidate-families.json")
if ($authorized.AuthorizedCandidateFamilies.Count -ne 3) {
    Fail "Authorized candidate families count mismatch"
}
foreach ($family in @("CloseSeeking15mAdaptive", "ControlledResidualCross", "PassiveUntilUrgency")) {
    $entry = $authorized.AuthorizedCandidateFamilies | Where-Object { $_.CandidatePolicyFamily -eq $family }
    if (-not $entry -or $entry.AuthorizedForFutureR047Simulation -ne $true -or $entry.NonExecutable -ne $true -or $entry.NotAnOrder -ne $true -or $entry.NoBrokerRoute -ne $true) {
        Fail "$family missing or executable/order-domain risk detected"
    }
}
$crc = $authorized.AuthorizedCandidateFamilies | Where-Object { $_.CandidatePolicyFamily -eq "ControlledResidualCross" }
if ($crc.NotAlwaysMarketAtClose -ne $true -or $crc.NotDefault -ne $true -or $crc.ConditionalOnly -ne $true) {
    Fail "ControlledResidualCross conditional safety weakened"
}

$negative = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r046-negative-baseline-families.json")
if ($negative.WakettAuthorizedAsNormalCandidate -ne $false) {
    Fail "Wakett patterns authorized as normal candidates"
}
foreach ($family in @("WakettPureLimitUntilClose", "WakettFiveMarketSlicesAroundClose")) {
    if (-not ($negative.NegativeBaselineFamilies -contains $family)) {
        Fail "$family missing as negative baseline"
    }
}

$bench = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r046-benchmark-only-families.json")
if ($bench.PromotedToExecutable -ne $false) {
    Fail "Benchmark policies promoted to executable"
}
foreach ($family in @("VWAPBenchmarkOnly", "TWAPBenchmarkOnly", "ImmediatePaperBenchmark")) {
    if (-not ($bench.BenchmarkOnlyFamilies -contains $family)) {
        Fail "$family missing from benchmark-only families"
    }
}

$safety = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r046-safety-outcome-families.json")
if ($safety.PromotedToExecutable -ne $false -or -not ($safety.SafetyOutcomeFamilies -contains "ManualReview") -or -not ($safety.SafetyOutcomeFamilies -contains "DoNotTrade")) {
    Fail "Safety outcomes promoted or missing"
}

$thresholds = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r046-threshold-evidence-preflight.json")
if ($thresholds.UnsupportedFinalThresholdsAuthorized -ne $false) {
    Fail "Unsupported final thresholds authorized"
}
if (($thresholds.ThresholdEvidenceStatus | Where-Object { $_.SupportedFinalNumericThreshold -eq $true }).Count -ne 0) {
    Fail "At least one final numeric threshold is authorized"
}

$nonExec = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r046-non-executable-candidate-preflight.json")
Assert-TrueField $nonExec "AllCandidatesDesignOnly" "Not all candidates design-only"
Assert-TrueField $nonExec "AllCandidatesPaperOnly" "Not all candidates paper-only"
Assert-TrueField $nonExec "AllCandidatesNonExecutable" "Not all candidates non-executable"
Assert-TrueField $nonExec "AllCandidatesNotAnOrder" "At least one candidate is an order"
Assert-TrueField $nonExec "AllCandidatesNotSubmitted" "At least one candidate submitted"
Assert-TrueField $nonExec "AllCandidatesNoBrokerRoute" "At least one candidate has broker route"
Assert-FalseField $nonExec "ExecutablePromotionAuthorized" "Executable promotion authorized"

$scope = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r046-r047-expected-simulation-scope.json")
if ($scope.QuoteWindows -ne 945 -or $scope.CandidateFamilies.Count -ne 3 -or $scope.NoExecutableScheduleOrOrderDomainObjects -ne $true) {
    Fail "R047 expected simulation scope invalid"
}
$reports = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r046-r047-expected-report-list.json")
if ($reports.ExpectedReports.Count -lt 15) {
    Fail "R047 expected report list incomplete"
}

$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r046-canonical-quarter-hour-policy-preservation.json")
if ($canonical.Legacy06UsedAsFutureCanonical -ne $false -or $canonical.Preserved -ne $true) {
    Fail "Canonical quarter-hour policy weakened"
}
$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r046-legacy-compatibility-preservation.json")
if ($legacy.LegacyCompatibilityOnly -ne $true -or $legacy.Legacy06UsedAsFutureCanonical -ne $false) {
    Fail "Legacy :06 used as future canonical"
}
$normalization = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r046-usd-pair-normalization-preservation.json")
if ($normalization.AppliesToExecutionUniverse -ne "USDPairOnly" -or $normalization.Symbols.Count -ne 7) {
    Fail "USD-pair normalization missing"
}
$audusd = $normalization.Symbols | Where-Object { $_.Symbol -eq "AUDUSD" }
if (-not $audusd -or $audusd.AudUsdNotFailed -ne $true) {
    Fail "AUDUSD misclassified"
}
$direct = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r046-direct-cross-exclusion-preservation.json")
if ($direct.DirectCrossExecutionDisabled -ne $true -or $direct.DirectCrossesIncluded -ne $false -or $direct.ExecutionUniverse -ne "USD-pair-only") {
    Fail "Direct-cross exclusion weakened"
}
$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r046-cost-guidance-preservation.json")
if ($cost.FiveUsdPerMillionGuidance -ne "BestCaseMajorOnly" -or $cost.Universalized -ne $false) {
    Fail "5 USD/million universalized"
}
$usdjpy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r046-usdjpy-caveat-preservation.json")
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or $usdjpy.RequiresInversion -ne $true -or "$($usdjpy.SecurityID)" -ne "4004" -or "$($usdjpy.SecurityIDSource)" -ne "8" -or $usdjpy.Failed -ne $false) {
    Fail "USDJPY caveat weakened"
}

$auditFalseChecks = @(
    @("phase-exec-sim-r046-no-new-simulation-audit.json", "NewSimulationExecuted", "New simulation executed"),
    @("phase-exec-sim-r046-no-new-backtest-audit.json", "NewBacktestExecuted", "New backtest executed"),
    @("phase-exec-sim-r046-no-tca-result-lines-audit.json", "TcaResultLinesCreated", "TCA result lines created"),
    @("phase-exec-sim-r046-no-db-import-audit.json", "DbImportExecuted", "DB import executed"),
    @("phase-exec-sim-r046-no-persisted-sanitized-row-audit.json", "PersistedSanitizedRowsCreated", "Persisted sanitized rows created"),
    @("phase-exec-sim-r046-no-executable-schedule-audit.json", "ExecutableSchedulesCreated", "Executable schedules created"),
    @("phase-exec-sim-r046-no-child-slices-audit.json", "ChildSlicesCreated", "Child slices created"),
    @("phase-exec-sim-r046-no-child-orders-audit.json", "ChildOrdersCreated", "Child orders created"),
    @("phase-exec-sim-r046-no-real-fill-audit.json", "RealFillsCreated", "Real fills created"),
    @("phase-exec-sim-r046-no-execution-report-audit.json", "ExecutionReportsCreated", "Execution reports created"),
    @("phase-exec-sim-r046-no-order-created-audit.json", "OrdersCreated", "Orders created"),
    @("phase-exec-sim-r046-no-polygon-api-call-audit.json", "PolygonApiCalled", "Polygon API called"),
    @("phase-exec-sim-r046-no-lmax-call-audit.json", "LmaxCalled", "LMAX called"),
    @("phase-exec-sim-r046-no-external-api-call-audit.json", "ExternalApiCalled", "External API called"),
    @("phase-exec-sim-r046-no-broker-marketdata-runtime-audit.json", "BrokerMarketDataRuntimeStarted", "Broker runtime started")
)
foreach ($check in $auditFalseChecks) {
    $doc = Read-Json (Join-Path $ArtifactsRoot $check[0])
    Assert-FalseField $doc $check[1] $check[2]
}
$route = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r046-no-route-no-submission-audit.json")
Assert-FalseField $route "RoutesCreated" "Routes created"
Assert-FalseField $route "SubmissionsCreated" "Submissions created"

$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r046-build-test-validator-evidence.json")
if ($evidence.DotnetBuild.Status -notin @("PASS", "PASS_WITH_WARNINGS")) {
    Fail "Build evidence missing or failing"
}
if ($evidence.FocusedR046StaticChecks.Status -ne "PASS") {
    Fail "Focused R046 static checks missing or failing"
}
if ($evidence.UnitTests.Status -notin @("PASS", "PASS_WITH_WARNINGS", "NOT_FEASIBLE")) {
    Fail "Unit test evidence missing"
}

Write-Host "EXEC-SIM-R046 validator passed."
Write-Host "Classifications:"
Write-Host "EXEC_SIM_R046_PASS_PARAMETER_REFINEMENT_SIM_AUTHORIZATION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R046_PASS_REFINEMENT_CANDIDATES_ACCEPTED_FOR_SIMULATION_NO_EXTERNAL"
Write-Host "EXEC_SIM_R046_PASS_NEGATIVE_BASELINES_AND_BENCHMARKS_PRESERVED_NO_EXTERNAL"
Write-Host "EXEC_SIM_R046_PASS_NO_SIMULATION_NO_ORDER_GATE_READY_NO_EXTERNAL"
