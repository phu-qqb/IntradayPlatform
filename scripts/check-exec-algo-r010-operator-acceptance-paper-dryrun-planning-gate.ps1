param(
    [string]$ArtifactsRoot = "artifacts/readiness/execution-algo"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "EXEC-ALGO-R010 gate failed: $Message"
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
    "phase-exec-algo-r010-summary.md",
    "phase-exec-algo-r010-r009-contract-reference.json",
    "phase-exec-algo-r010-r054-decision-reference.json",
    "phase-exec-algo-r010-operator-acceptance-contract.json",
    "phase-exec-algo-r010-operator-acceptance-result.json",
    "phase-exec-algo-r010-paper-dry-run-planning-contract.json",
    "phase-exec-algo-r010-paper-dry-run-scope.json",
    "phase-exec-algo-r010-paper-dry-run-required-inputs.json",
    "phase-exec-algo-r010-paper-dry-run-success-criteria.json",
    "phase-exec-algo-r010-paper-dry-run-stop-hold-criteria.json",
    "phase-exec-algo-r010-risk-operator-review-prerequisites.json",
    "phase-exec-algo-r010-more-data-recommended-preservation.json",
    "phase-exec-algo-r010-no-executable-promotion-preservation.json",
    "phase-exec-algo-r010-rejected-wakett-preservation.json",
    "phase-exec-algo-r010-benchmark-only-preservation.json",
    "phase-exec-algo-r010-manual-review-do-not-trade-preservation.json",
    "phase-exec-algo-r010-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-algo-r010-legacy-compatibility-preservation.json",
    "phase-exec-algo-r010-usd-pair-normalization-preservation.json",
    "phase-exec-algo-r010-direct-cross-exclusion-preservation.json",
    "phase-exec-algo-r010-cost-guidance-preservation.json",
    "phase-exec-algo-r010-nonmajor-calibration-preservation.json",
    "phase-exec-algo-r010-non-executable-planning-audit.json",
    "phase-exec-algo-r010-no-executable-schedule-audit.json",
    "phase-exec-algo-r010-no-child-slices-audit.json",
    "phase-exec-algo-r010-no-child-orders-audit.json",
    "phase-exec-algo-r010-no-paper-ledger-commit-audit.json",
    "phase-exec-algo-r010-no-new-backtest-audit.json",
    "phase-exec-algo-r010-no-new-simulation-audit.json",
    "phase-exec-algo-r010-no-tca-result-lines-audit.json",
    "phase-exec-algo-r010-no-polygon-api-call-audit.json",
    "phase-exec-algo-r010-no-lmax-call-audit.json",
    "phase-exec-algo-r010-no-external-api-call-audit.json",
    "phase-exec-algo-r010-no-broker-marketdata-runtime-audit.json",
    "phase-exec-algo-r010-no-real-fill-audit.json",
    "phase-exec-algo-r010-no-execution-report-audit.json",
    "phase-exec-algo-r010-no-order-created-audit.json",
    "phase-exec-algo-r010-no-route-no-submission-audit.json",
    "phase-exec-algo-r010-usdjpy-caveat-preservation.json",
    "phase-exec-algo-r010-lmax-readonly-baseline-reference.json",
    "phase-exec-algo-r010-no-external-audit.json",
    "phase-exec-algo-r010-forbidden-actions-audit.json",
    "phase-exec-algo-r010-next-phase-recommendation.json",
    "phase-exec-algo-r010-build-test-validator-evidence.json"
)

foreach ($name in $required) {
    [void](Read-Artifact $name)
}

$r009 = Read-Artifact "phase-exec-algo-r010-r009-contract-reference.json"
$r054 = Read-Artifact "phase-exec-algo-r010-r054-decision-reference.json"
$acceptanceContract = Read-Artifact "phase-exec-algo-r010-operator-acceptance-contract.json"
$acceptanceResult = Read-Artifact "phase-exec-algo-r010-operator-acceptance-result.json"
$planning = Read-Artifact "phase-exec-algo-r010-paper-dry-run-planning-contract.json"
$scope = Read-Artifact "phase-exec-algo-r010-paper-dry-run-scope.json"
$inputs = Read-Artifact "phase-exec-algo-r010-paper-dry-run-required-inputs.json"
$success = Read-Artifact "phase-exec-algo-r010-paper-dry-run-success-criteria.json"
$hold = Read-Artifact "phase-exec-algo-r010-paper-dry-run-stop-hold-criteria.json"
$moreData = Read-Artifact "phase-exec-algo-r010-more-data-recommended-preservation.json"
$noPromotion = Read-Artifact "phase-exec-algo-r010-no-executable-promotion-preservation.json"
$wakett = Read-Artifact "phase-exec-algo-r010-rejected-wakett-preservation.json"
$benchmark = Read-Artifact "phase-exec-algo-r010-benchmark-only-preservation.json"
$manual = Read-Artifact "phase-exec-algo-r010-manual-review-do-not-trade-preservation.json"
$canonical = Read-Artifact "phase-exec-algo-r010-canonical-quarter-hour-policy-preservation.json"
$legacy = Read-Artifact "phase-exec-algo-r010-legacy-compatibility-preservation.json"
$directCross = Read-Artifact "phase-exec-algo-r010-direct-cross-exclusion-preservation.json"
$cost = Read-Artifact "phase-exec-algo-r010-cost-guidance-preservation.json"
$usdPair = Read-Artifact "phase-exec-algo-r010-usd-pair-normalization-preservation.json"
$usdJpy = Read-Artifact "phase-exec-algo-r010-usdjpy-caveat-preservation.json"
$noExternal = Read-Artifact "phase-exec-algo-r010-no-external-audit.json"
$forbidden = Read-Artifact "phase-exec-algo-r010-forbidden-actions-audit.json"
$nonExecutable = Read-Artifact "phase-exec-algo-r010-non-executable-planning-audit.json"
$evidence = Read-Artifact "phase-exec-algo-r010-build-test-validator-evidence.json"

if ($r009.Primary -ne "CloseSeeking15mAdaptive_BalancedAdaptive_v0") { Fail "R009 primary reference missing" }
if ($r009.DesignOnly -ne $true -or $r009.NonExecutable -ne $true -or $r009.NotAnOrder -ne $true -or $r009.NoBrokerRoute -ne $true) {
    Fail "R009 reference is not non-executable design-only"
}
if ($r009.ExecutablePromotionAuthorized -ne $false) { Fail "R009 executable promotion authorized" }
if ($r054.PolicyDecision -ne "KeepR009PrimaryDesignOnlyCandidate" -or $r054.ExecutablePromotionAuthorized -ne $false) {
    Fail "R054 decision reference missing or unsafe"
}

if ($acceptanceContract.DesignOnly -ne $true -or $acceptanceContract.NonExecutable -ne $true -or $acceptanceContract.ExecutablePromotionAuthorized -ne $false) {
    Fail "operator acceptance contract authorizes executable use"
}
if ($acceptanceResult.OperatorAcceptanceStatus -ne "AcceptedForFurtherPaperDryRunPlanning") {
    Fail "operator acceptance result missing expected status"
}
if ($acceptanceResult.NotExecutable -ne $true -or $acceptanceResult.BrokerReady -ne $false -or $acceptanceResult.LiveReady -ne $false -or $acceptanceResult.ExecutablePromotionAuthorized -ne $false) {
    Fail "R009 marked executable/live/broker-ready"
}

if ($planning.PlanningOnly -ne $true -or $planning.NoExecution -ne $true -or $planning.NoExecutableSchedules -ne $true -or $planning.NoOrders -ne $true -or $planning.NoPaperLedgerCommit -ne $true) {
    Fail "paper dry-run planning contract is unsafe"
}
if (-not $scope.Scope -or @($scope.Scope).Count -lt 5) { Fail "paper dry-run scope missing" }
if (-not $inputs.RequiredInputs -or @($inputs.RequiredInputs).Count -lt 5) { Fail "required inputs missing" }
if (-not $success.SuccessCriteria -or @($success.SuccessCriteria).Count -lt 5) { Fail "success criteria missing" }
if (-not $hold.StopHoldCriteria -or @($hold.StopHoldCriteria).Count -lt 5) { Fail "stop/hold criteria missing" }

if ($moreData.Preserved -ne $true -or $moreData.ExecutablePromotionDiscussionBlocked -ne $true) { Fail "more-data recommendation not preserved" }
if ($noPromotion.ExecutablePromotionAuthorized -ne $false -or $noPromotion.R009MarkedExecutable -ne $false -or $noPromotion.LiveReady -ne $false -or $noPromotion.BrokerReady -ne $false) {
    Fail "executable promotion preservation failed"
}
if ($wakett.PromotedAsCandidate -ne $false) { Fail "Wakett rejection weakened" }
if ($benchmark.PromotedToExecutable -ne $false) { Fail "benchmark-only policy promoted" }
if ($manual.PromotedToExecutable -ne $false) { Fail "ManualReview/DoNotTrade promoted" }
if ($canonical.Legacy06UsedAsFutureCanonical -ne $false) { Fail "legacy :06 used as future canonical" }
if ($legacy.CompatibilityOnly -ne $true -or $legacy.Legacy06UsedAsFutureCanonical -ne $false) { Fail "legacy policy weakened" }
if ($directCross.DirectCrossExecutionEnabled -ne $false) { Fail "direct-cross exclusion weakened" }
if ($cost.Universalized -ne $false) { Fail "5 USD/million universalized" }
if ($usdPair.AUDUSDNotFailed -ne $true) { Fail "AUDUSD misclassified" }
if ($usdJpy.CaveatPreserved -ne $true -or $usdJpy.SecurityID -ne "4004" -or $usdJpy.SecurityIDSource -ne "8") { Fail "USDJPY caveat weakened" }

if ($noExternal.PolygonCalled -ne $false -or $noExternal.LmaxCalled -ne $false -or $noExternal.ExternalApiCalled -ne $false -or $noExternal.DownloadsExecuted -ne $false) {
    Fail "external action detected"
}
if ($forbidden.ForbiddenActionsOccurred -ne $false -or $forbidden.ValidationImportBacktestSimulationExecuted -ne $false -or $forbidden.TcaResultLinesProduced -ne $false -or $forbidden.PaperLedgerCommitCreated -ne $false -or $forbidden.ExecutablePromotionAuthorized -ne $false) {
    Fail "forbidden action audit failed"
}
if ($nonExecutable.PlanningOnly -ne $true -or $nonExecutable.NonExecutable -ne $true -or $nonExecutable.NotAnOrder -ne $true -or $nonExecutable.NoBrokerRoute -ne $true -or $nonExecutable.ExecutablePromotionAuthorized -ne $false) {
    Fail "non-executable planning audit failed"
}

if ($evidence.DotnetBuildNoRestoreSucceeded -ne $true) { Fail "dotnet build evidence missing or failed" }
if ($evidence.FocusedR010StaticChecksSucceeded -ne $true) { Fail "focused R010 checks evidence missing or failed" }
if ($evidence.UnitTestsFeasible -eq $true -and $evidence.UnitTestsSucceeded -ne $true) { Fail "unit test evidence missing or failed" }

Write-Host "EXEC-ALGO-R010 validation passed"
