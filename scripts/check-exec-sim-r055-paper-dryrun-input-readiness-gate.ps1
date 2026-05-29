param(
    [string]$ArtifactsRoot = "artifacts/readiness/execution-sim"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "EXEC-SIM-R055 gate failed: $Message"
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
    "phase-exec-sim-r055-summary.md",
    "phase-exec-sim-r055-r010-paper-dryrun-plan-reference.json",
    "phase-exec-sim-r055-r009-contract-reference.json",
    "phase-exec-sim-r055-paper-dryrun-input-readiness-contract.json",
    "phase-exec-sim-r055-existing-artifact-inventory.json",
    "phase-exec-sim-r055-present-inputs-report.json",
    "phase-exec-sim-r055-missing-inputs-diagnostics.json",
    "phase-exec-sim-r055-paper-execution-plan-line-readiness.json",
    "phase-exec-sim-r055-symbol-inversion-readiness.json",
    "phase-exec-sim-r055-canonical-target-close-readiness.json",
    "phase-exec-sim-r055-quote-window-readiness-reference.json",
    "phase-exec-sim-r055-close-benchmark-readiness-reference.json",
    "phase-exec-sim-r055-feed-quality-readiness-reference.json",
    "phase-exec-sim-r055-risk-operator-prerequisite-readiness.json",
    "phase-exec-sim-r055-r009-application-preview-contract.json",
    "phase-exec-sim-r055-r009-application-preview-lines.json",
    "phase-exec-sim-r055-readiness-result.json",
    "phase-exec-sim-r055-next-paper-dryrun-recommendation.json",
    "phase-exec-sim-r055-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-sim-r055-legacy-compatibility-preservation.json",
    "phase-exec-sim-r055-usd-pair-normalization-preservation.json",
    "phase-exec-sim-r055-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r055-cost-guidance-preservation.json",
    "phase-exec-sim-r055-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r055-no-pms-cycle-run-audit.json",
    "phase-exec-sim-r055-no-new-simulation-audit.json",
    "phase-exec-sim-r055-no-new-backtest-audit.json",
    "phase-exec-sim-r055-no-tca-result-lines-audit.json",
    "phase-exec-sim-r055-no-executable-schedule-audit.json",
    "phase-exec-sim-r055-no-child-slices-audit.json",
    "phase-exec-sim-r055-no-child-orders-audit.json",
    "phase-exec-sim-r055-no-order-created-audit.json",
    "phase-exec-sim-r055-no-real-fill-audit.json",
    "phase-exec-sim-r055-no-execution-report-audit.json",
    "phase-exec-sim-r055-no-route-no-submission-audit.json",
    "phase-exec-sim-r055-no-paper-ledger-commit-audit.json",
    "phase-exec-sim-r055-no-polygon-api-call-audit.json",
    "phase-exec-sim-r055-no-lmax-call-audit.json",
    "phase-exec-sim-r055-no-external-api-call-audit.json",
    "phase-exec-sim-r055-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r055-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r055-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r055-no-external-audit.json",
    "phase-exec-sim-r055-forbidden-actions-audit.json",
    "phase-exec-sim-r055-next-phase-recommendation.json",
    "phase-exec-sim-r055-build-test-validator-evidence.json"
)

foreach ($name in $required) {
    [void](Read-Artifact $name)
}

$r010 = Read-Artifact "phase-exec-sim-r055-r010-paper-dryrun-plan-reference.json"
$r009 = Read-Artifact "phase-exec-sim-r055-r009-contract-reference.json"
$contract = Read-Artifact "phase-exec-sim-r055-paper-dryrun-input-readiness-contract.json"
$inventory = Read-Artifact "phase-exec-sim-r055-existing-artifact-inventory.json"
$present = Read-Artifact "phase-exec-sim-r055-present-inputs-report.json"
$missing = Read-Artifact "phase-exec-sim-r055-missing-inputs-diagnostics.json"
$lineReadiness = Read-Artifact "phase-exec-sim-r055-paper-execution-plan-line-readiness.json"
$targetClose = Read-Artifact "phase-exec-sim-r055-canonical-target-close-readiness.json"
$quoteWindow = Read-Artifact "phase-exec-sim-r055-quote-window-readiness-reference.json"
$closeBenchmark = Read-Artifact "phase-exec-sim-r055-close-benchmark-readiness-reference.json"
$feedQuality = Read-Artifact "phase-exec-sim-r055-feed-quality-readiness-reference.json"
$risk = Read-Artifact "phase-exec-sim-r055-risk-operator-prerequisite-readiness.json"
$previewContract = Read-Artifact "phase-exec-sim-r055-r009-application-preview-contract.json"
$previewLines = Read-Artifact "phase-exec-sim-r055-r009-application-preview-lines.json"
$result = Read-Artifact "phase-exec-sim-r055-readiness-result.json"
$canonical = Read-Artifact "phase-exec-sim-r055-canonical-quarter-hour-policy-preservation.json"
$legacy = Read-Artifact "phase-exec-sim-r055-legacy-compatibility-preservation.json"
$directCross = Read-Artifact "phase-exec-sim-r055-direct-cross-exclusion-preservation.json"
$cost = Read-Artifact "phase-exec-sim-r055-cost-guidance-preservation.json"
$usdPair = Read-Artifact "phase-exec-sim-r055-usd-pair-normalization-preservation.json"
$usdJpy = Read-Artifact "phase-exec-sim-r055-usdjpy-caveat-preservation.json"
$noExternal = Read-Artifact "phase-exec-sim-r055-no-external-audit.json"
$forbidden = Read-Artifact "phase-exec-sim-r055-forbidden-actions-audit.json"
$evidence = Read-Artifact "phase-exec-sim-r055-build-test-validator-evidence.json"

if ($r010.NotExecutable -ne $true) { Fail "R010 plan reference is not non-executable" }
if ($r009.Primary -ne "CloseSeeking15mAdaptive_BalancedAdaptive_v0") { Fail "R009 contract reference missing primary" }
if ($r009.ExecutablePromotionAuthorized -ne $false -or $r009.NonExecutable -ne $true -or $r009.NotAnOrder -ne $true -or $r009.NoBrokerRoute -ne $true) {
    Fail "R009 reference promotes executable/order/broker behavior"
}
if ($contract.NoPmsCycleRun -ne $true -or $contract.NoSimulation -ne $true -or $contract.NoBacktest -ne $true -or $contract.NoTcaResultLines -ne $true -or $contract.NoOrderDomainOutput -ne $true) {
    Fail "input-readiness contract permits forbidden action"
}
if ([int]$inventory.InventoryCount -lt 1) { Fail "artifact inventory missing" }
if ([int]$present.PresentInputCount -lt 1) { Fail "present-input report missing" }
if ([int]$missing.MissingInputCount -lt 1 -and $result.InputsReady -ne $true) { Fail "missing-input diagnostics absent while inputs are blocked" }
if ($missing.PlanLinesInvented -ne $false -or $missing.PmsCycleRun -ne $false) { Fail "plan lines invented or PMS cycle run" }

if ($lineReadiness.CurrentR009PlanLinesReady -ne $false) { Fail "current R009 plan lines incorrectly marked ready" }
if ($targetClose.TargetCloseTimestampPerPlanLinePresent -ne $false -or $targetClose.Legacy06UsedAsFutureCanonical -ne $false) {
    Fail "canonical target close readiness unsafe"
}
if ($quoteWindow.BindingToCurrentPlanLinesPresent -ne $false) { Fail "quote-window binding unexpectedly claimed" }
if ($closeBenchmark.BindingToCurrentPlanLinesPresent -ne $false) { Fail "close-benchmark binding unexpectedly claimed" }
if ($feedQuality.BindingToCurrentPlanLinesPresent -ne $false) { Fail "feed-quality binding unexpectedly claimed" }
if ($risk.CurrentR009RiskReviewPresent -ne $false -or $risk.CurrentR009OperatorApprovalPresent -ne $false) {
    Fail "current R009 risk/operator readiness incorrectly claimed"
}

if ($previewContract.InputsReady -ne $false -or $previewContract.PreviewStatus -ne "BlockedMissingInputs") { Fail "preview contract should be blocked" }
if ($previewContract.NotAnOrder -ne $true -or $previewContract.NoBrokerRoute -ne $true -or $previewContract.NoChildSlices -ne $true -or $previewContract.NoExecutableSchedule -ne $true -or $previewContract.NoFill -ne $true -or $previewContract.NoRoute -ne $true -or $previewContract.NoSubmission -ne $true) {
    Fail "preview contract is represented as executable/order/fill/route"
}
foreach ($line in @($previewLines.Lines)) {
    if ($line.NonExecutable -ne $true -or $line.NotAnOrder -ne $true -or $line.NoBrokerRoute -ne $true -or $line.NoExecutableSchedule -ne $true -or $line.NoFill -ne $true -or $line.NoRoute -ne $true -or $line.NoSubmission -ne $true) {
        Fail "preview line represented as executable/order/fill/route/submission"
    }
}
if ($result.SafeBlocked -ne $true -or $result.InputsReady -ne $false -or $result.PreviewReady -ne $false -or $result.NewPlanLinesCreated -ne $false -or $result.PmsCycleRun -ne $false) {
    Fail "readiness result is not a safe blocked result"
}

if ($canonical.Legacy06UsedAsFutureCanonical -ne $false) { Fail "legacy :06 used as future canonical" }
if ($legacy.CompatibilityOnly -ne $true -or $legacy.Legacy06UsedAsFutureCanonical -ne $false) { Fail "legacy compatibility weakened" }
if ($directCross.DirectCrossExecutionEnabled -ne $false) { Fail "direct-cross exclusion weakened" }
if ($cost.Universalized -ne $false) { Fail "5 USD/million universalized" }
if ($usdPair.AUDUSDNotFailed -ne $true) { Fail "AUDUSD misclassified" }
if ($usdJpy.CaveatPreserved -ne $true -or $usdJpy.SecurityID -ne "4004" -or $usdJpy.SecurityIDSource -ne "8") { Fail "USDJPY caveat weakened" }

if ($noExternal.PolygonCalled -ne $false -or $noExternal.LmaxCalled -ne $false -or $noExternal.ExternalApiCalled -ne $false -or $noExternal.DownloadsExecuted -ne $false) {
    Fail "external action detected"
}
if ($forbidden.ForbiddenActionsOccurred -ne $false -or $forbidden.FilesDownloaded -ne $false -or $forbidden.PmsEmsOmsCycleRun -ne $false -or $forbidden.ValidationImportBacktestSimulationExecuted -ne $false -or $forbidden.TcaResultLinesProduced -ne $false -or $forbidden.ExecutableScheduleCreated -ne $false -or $forbidden.PaperLedgerCommitCreated -ne $false -or $forbidden.ExecutablePromotionAuthorized -ne $false) {
    Fail "forbidden action audit failed"
}

if ($evidence.DotnetBuildNoRestoreSucceeded -ne $true) { Fail "dotnet build evidence missing or failed" }
if ($evidence.FocusedR055StaticChecksSucceeded -ne $true) { Fail "focused R055 checks evidence missing or failed" }
if ($evidence.UnitTestsFeasible -eq $true -and $evidence.UnitTestsSucceeded -ne $true) { Fail "unit test evidence missing or failed" }

Write-Host "EXEC-SIM-R055 validation passed"
