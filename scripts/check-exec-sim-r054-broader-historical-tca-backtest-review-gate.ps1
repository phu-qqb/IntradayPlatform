param(
    [string]$ArtifactsRoot = "artifacts/readiness/execution-sim"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "EXEC-SIM-R054 gate failed: $Message"
}

function Read-Artifact([string]$Name) {
    $path = Join-Path $ArtifactsRoot $Name
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "missing artifact $Name"
    }
    if ($Name.EndsWith(".json")) {
        return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    }
    return Get-Content -LiteralPath $path -Raw
}

$required = @(
    "phase-exec-sim-r054-summary.md",
    "phase-exec-sim-r054-r053-authorization-reference.json",
    "phase-exec-sim-r054-r009-contract-reference.json",
    "phase-exec-sim-r054-r049-result-reference.json",
    "phase-exec-sim-r054-broader-backtest-execution-contract.json",
    "phase-exec-sim-r054-broader-backtest-run-result.json",
    "phase-exec-sim-r054-tca-result-line-contract.json",
    "phase-exec-sim-r054-tca-result-lines.json",
    "phase-exec-sim-r054-result-line-count-and-coverage.json",
    "phase-exec-sim-r054-per-date-reports.json",
    "phase-exec-sim-r054-per-symbol-reports.json",
    "phase-exec-sim-r054-per-symbol-date-reports.json",
    "phase-exec-sim-r054-aggregate-policy-comparison.json",
    "phase-exec-sim-r054-ranking-median-slippage.json",
    "phase-exec-sim-r054-ranking-p95-slippage.json",
    "phase-exec-sim-r054-ranking-fill-ratio.json",
    "phase-exec-sim-r054-ranking-residual.json",
    "phase-exec-sim-r054-ranking-spread-paid.json",
    "phase-exec-sim-r054-no-overnight-residual-pressure-review.json",
    "phase-exec-sim-r054-r009-contract-validation-review.json",
    "phase-exec-sim-r054-candidate-vs-r049-comparison.json",
    "phase-exec-sim-r054-candidate-vs-r044-comparison.json",
    "phase-exec-sim-r054-date-regime-stability-review.json",
    "phase-exec-sim-r054-symbol-stability-review.json",
    "phase-exec-sim-r054-duplicate-aware-review.json",
    "phase-exec-sim-r054-spread-residual-tradeoff-review.json",
    "phase-exec-sim-r054-5usd-per-million-review.json",
    "phase-exec-sim-r054-policy-decision.json",
    "phase-exec-sim-r054-parameter-contract-decision.json",
    "phase-exec-sim-r054-wakett-rejection-preservation.json",
    "phase-exec-sim-r054-benchmark-only-preservation.json",
    "phase-exec-sim-r054-manual-review-do-not-trade-preservation.json",
    "phase-exec-sim-r054-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-sim-r054-legacy-compatibility-preservation.json",
    "phase-exec-sim-r054-usd-pair-normalization-preservation.json",
    "phase-exec-sim-r054-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r054-cost-guidance-preservation.json",
    "phase-exec-sim-r054-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r054-non-executable-result-line-audit.json",
    "phase-exec-sim-r054-no-db-import-audit.json",
    "phase-exec-sim-r054-no-persisted-sanitized-row-audit.json",
    "phase-exec-sim-r054-no-executable-schedule-audit.json",
    "phase-exec-sim-r054-no-child-slices-audit.json",
    "phase-exec-sim-r054-no-child-orders-audit.json",
    "phase-exec-sim-r054-no-real-fill-audit.json",
    "phase-exec-sim-r054-no-execution-report-audit.json",
    "phase-exec-sim-r054-no-order-created-audit.json",
    "phase-exec-sim-r054-no-route-no-submission-audit.json",
    "phase-exec-sim-r054-no-polygon-api-call-audit.json",
    "phase-exec-sim-r054-no-lmax-call-audit.json",
    "phase-exec-sim-r054-no-external-api-call-audit.json",
    "phase-exec-sim-r054-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r054-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r054-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r054-no-external-audit.json",
    "phase-exec-sim-r054-forbidden-actions-audit.json",
    "phase-exec-sim-r054-next-phase-recommendation.json",
    "phase-exec-sim-r054-build-test-validator-evidence.json"
)

foreach ($name in $required) {
    [void](Read-Artifact $name)
}

$r053 = Read-Artifact "phase-exec-sim-r054-r053-authorization-reference.json"
$r009 = Read-Artifact "phase-exec-sim-r054-r009-contract-reference.json"
$run = Read-Artifact "phase-exec-sim-r054-broader-backtest-run-result.json"
$lines = Read-Artifact "phase-exec-sim-r054-tca-result-lines.json"
$coverage = Read-Artifact "phase-exec-sim-r054-result-line-count-and-coverage.json"
$aggregate = Read-Artifact "phase-exec-sim-r054-aggregate-policy-comparison.json"
$policyDecision = Read-Artifact "phase-exec-sim-r054-policy-decision.json"
$parameterDecision = Read-Artifact "phase-exec-sim-r054-parameter-contract-decision.json"
$wakett = Read-Artifact "phase-exec-sim-r054-wakett-rejection-preservation.json"
$benchmark = Read-Artifact "phase-exec-sim-r054-benchmark-only-preservation.json"
$manual = Read-Artifact "phase-exec-sim-r054-manual-review-do-not-trade-preservation.json"
$canonical = Read-Artifact "phase-exec-sim-r054-canonical-quarter-hour-policy-preservation.json"
$legacy = Read-Artifact "phase-exec-sim-r054-legacy-compatibility-preservation.json"
$directCross = Read-Artifact "phase-exec-sim-r054-direct-cross-exclusion-preservation.json"
$cost = Read-Artifact "phase-exec-sim-r054-cost-guidance-preservation.json"
$usdJpy = Read-Artifact "phase-exec-sim-r054-usdjpy-caveat-preservation.json"
$usdPair = Read-Artifact "phase-exec-sim-r054-usd-pair-normalization-preservation.json"
$noExternal = Read-Artifact "phase-exec-sim-r054-no-external-audit.json"
$forbidden = Read-Artifact "phase-exec-sim-r054-forbidden-actions-audit.json"
$nonExecutable = Read-Artifact "phase-exec-sim-r054-non-executable-result-line-audit.json"
$evidence = Read-Artifact "phase-exec-sim-r054-build-test-validator-evidence.json"

if ([int]$r053.QuoteWindows -ne 3780 -or [int]$r053.FeedQuality -ne 140) { Fail "R053 readiness reference is incomplete" }
if ($r009.Primary -ne "CloseSeeking15mAdaptive_BalancedAdaptive_v0") { Fail "R009 primary candidate reference missing" }
if ($r009.ExecutablePromotionAuthorized -ne $false) { Fail "R009 executable promotion authorized" }

if ($run.RunStatus -ne "CompletedFixtureOnlyPaperBacktest") { Fail "broader backtest run result missing or incomplete" }
if ([int]$run.Dates -ne 20 -or [int]$run.Symbols -ne 7 -or [int]$run.QuoteWindows -ne 3780) { Fail "run scope mismatch" }
if ([int]$run.PolicyFamilies -ne 11 -or [int]$run.TcaResultLineCount -ne 41580) { Fail "run result-line count mismatch" }
if ($run.FixtureOnly -ne $true -or $run.PaperOnly -ne $true -or $run.NonExecutable -ne $true) { Fail "run result is not fixture-only/paper-only/non-executable" }
if ($run.DbImportOccurred -ne $false -or $run.OrdersCreated -ne $false -or $run.FillsCreated -ne $false -or $run.ExecutionReportsCreated -ne $false -or $run.RoutesCreated -ne $false -or $run.SubmissionsCreated -ne $false) {
    Fail "run result includes forbidden order-domain output"
}

if ([int]$lines.TcaResultLineCount -ne 41580) { Fail "TCA result lines missing" }
if ($lines.ContainsFills -ne $false -or $lines.ContainsOrders -ne $false -or $lines.ContainsExecutionReports -ne $false -or $lines.ContainsRoutes -ne $false -or $lines.ContainsSubmissions -ne $false) {
    Fail "TCA lines represented as fills/orders/reports/routes/submissions"
}
if ($lines.AllLinesFixtureOnly -ne $true -or $lines.AllLinesPaperOnly -ne $true -or $lines.AllLinesNonExecutable -ne $true) {
    Fail "TCA lines are not all non-executable fixture-only paper lines"
}
if ([int]$coverage.ExpectedResultLines -ne 41580 -or [int]$coverage.ActualResultLines -ne 41580 -or $coverage.CoverageComplete -ne $true) {
    Fail "result-line coverage missing or incorrect"
}

if (-not $aggregate.Reports -or @($aggregate.Reports).Count -lt 11) { Fail "aggregate comparison missing policy reports" }
foreach ($ranking in @(
    "phase-exec-sim-r054-ranking-median-slippage.json",
    "phase-exec-sim-r054-ranking-p95-slippage.json",
    "phase-exec-sim-r054-ranking-fill-ratio.json",
    "phase-exec-sim-r054-ranking-residual.json",
    "phase-exec-sim-r054-ranking-spread-paid.json"
)) {
    $r = Read-Artifact $ranking
    if (-not $r.Ranking -or @($r.Ranking).Count -lt 11) { Fail "ranking incomplete: $ranking" }
}

if ($policyDecision.ExecutablePromotionAuthorized -ne $false) { Fail "policy decision authorizes executable promotion" }
if ($policyDecision.Wakett -ne "RejectedNegativeBaselineOnly") { Fail "Wakett promoted as candidate" }
if ($policyDecision.Benchmarks -ne "BenchmarkOnly") { Fail "benchmark-only policy promoted" }
if ($policyDecision.SafetyOutcomes -ne "SafetyOnly") { Fail "ManualReview/DoNotTrade promoted" }
if ($parameterDecision.ExecutablePromotionAuthorized -ne $false) { Fail "parameter decision authorizes executable promotion" }

if ($wakett.PromotedAsCandidate -ne $false) { Fail "Wakett rejection weakened" }
if ($benchmark.PromotedToExecutable -ne $false) { Fail "benchmark-only preservation weakened" }
if ($manual.PromotedToExecutable -ne $false) { Fail "manual/do-not-trade preservation weakened" }
if ($canonical.Legacy06UsedAsFutureCanonical -ne $false) { Fail "legacy :06 used as future canonical" }
if ($legacy.CompatibilityOnly -ne $true -or $legacy.Legacy06UsedAsFutureCanonical -ne $false) { Fail "legacy compatibility policy weakened" }
if ($directCross.DirectCrossExecutionEnabled -ne $false) { Fail "direct-cross exclusion weakened" }
if ($cost.Universalized -ne $false) { Fail "5 USD/million universalized" }
if ($usdJpy.CaveatPreserved -ne $true -or $usdJpy.SecurityID -ne "4004" -or $usdJpy.SecurityIDSource -ne "8") { Fail "USDJPY caveat weakened" }
if ($usdPair.AUDUSDNotFailed -ne $true) { Fail "AUDUSD misclassified" }

if ($noExternal.PolygonCalled -ne $false -or $noExternal.LmaxCalled -ne $false -or $noExternal.ExternalApiCalled -ne $false -or $noExternal.DownloadsExecuted -ne $false) {
    Fail "external action detected"
}
if ($forbidden.ForbiddenActionsOccurred -ne $false -or $forbidden.DbImportOccurred -ne $false -or $forbidden.ExecutableScheduleCreated -ne $false -or $forbidden.StateMutated -ne $false) {
    Fail "forbidden action audit failed"
}
if ($nonExecutable.FixtureOnly -ne $true -or $nonExecutable.PaperOnly -ne $true -or $nonExecutable.NonExecutable -ne $true -or $nonExecutable.IsFill -ne $false -or $nonExecutable.IsOrder -ne $false -or $nonExecutable.IsExecutionReport -ne $false) {
    Fail "non-executable result-line audit failed"
}

if ($evidence.DotnetBuildNoRestoreSucceeded -ne $true) { Fail "dotnet build evidence missing or failed" }
if ($evidence.FocusedR054StaticChecksSucceeded -ne $true) { Fail "focused R054 checks evidence missing or failed" }
if ($evidence.UnitTestsFeasible -eq $true -and $evidence.UnitTestsSucceeded -ne $true) { Fail "unit test evidence missing or failed" }

Write-Host "EXEC-SIM-R054 validation passed"
