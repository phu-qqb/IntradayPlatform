param(
    [string]$ArtifactsRoot = "artifacts/readiness/execution-sim"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "EXEC-PAPER-R001 gate failed: $Message"
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
    "phase-exec-paper-r001-summary.md",
    "phase-exec-paper-r001-r055-diagnostics-reference.json",
    "phase-exec-paper-r001-r009-contract-reference.json",
    "phase-exec-paper-r001-r010-planning-reference.json",
    "phase-exec-paper-r001-pms-artifact-inventory.json",
    "phase-exec-paper-r001-current-paper-plan-line-search-results.json",
    "phase-exec-paper-r001-historical-paper-plan-line-reference.json",
    "phase-exec-paper-r001-current-input-readiness-contract.json",
    "phase-exec-paper-r001-present-inputs-report.json",
    "phase-exec-paper-r001-missing-inputs-diagnostics.json",
    "phase-exec-paper-r001-canonical-target-close-readiness.json",
    "phase-exec-paper-r001-readiness-binding-search-results.json",
    "phase-exec-paper-r001-risk-operator-approval-readiness.json",
    "phase-exec-paper-r001-r009-dryrun-handoff-contract.json",
    "phase-exec-paper-r001-r009-dryrun-handoff-package.json",
    "phase-exec-paper-r001-operator-action-package.json",
    "phase-exec-paper-r001-readiness-result.json",
    "phase-exec-paper-r001-next-phase-recommendation.json",
    "phase-exec-paper-r001-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-paper-r001-legacy-compatibility-preservation.json",
    "phase-exec-paper-r001-usd-pair-normalization-preservation.json",
    "phase-exec-paper-r001-direct-cross-exclusion-preservation.json",
    "phase-exec-paper-r001-cost-guidance-preservation.json",
    "phase-exec-paper-r001-nonmajor-calibration-preservation.json",
    "phase-exec-paper-r001-no-pms-cycle-run-audit.json",
    "phase-exec-paper-r001-no-executable-schedule-audit.json",
    "phase-exec-paper-r001-no-child-slices-audit.json",
    "phase-exec-paper-r001-no-child-orders-audit.json",
    "phase-exec-paper-r001-no-order-created-audit.json",
    "phase-exec-paper-r001-no-real-fill-audit.json",
    "phase-exec-paper-r001-no-execution-report-audit.json",
    "phase-exec-paper-r001-no-route-no-submission-audit.json",
    "phase-exec-paper-r001-no-paper-ledger-commit-audit.json",
    "phase-exec-paper-r001-no-polygon-api-call-audit.json",
    "phase-exec-paper-r001-no-lmax-call-audit.json",
    "phase-exec-paper-r001-no-external-api-call-audit.json",
    "phase-exec-paper-r001-no-broker-marketdata-runtime-audit.json",
    "phase-exec-paper-r001-usdjpy-caveat-preservation.json",
    "phase-exec-paper-r001-lmax-readonly-baseline-reference.json",
    "phase-exec-paper-r001-no-external-audit.json",
    "phase-exec-paper-r001-forbidden-actions-audit.json",
    "phase-exec-paper-r001-build-test-validator-evidence.json"
)

foreach ($name in $required) {
    [void](Read-Artifact $name)
}

$r055 = Read-Artifact "phase-exec-paper-r001-r055-diagnostics-reference.json"
$r009 = Read-Artifact "phase-exec-paper-r001-r009-contract-reference.json"
$search = Read-Artifact "phase-exec-paper-r001-current-paper-plan-line-search-results.json"
$historical = Read-Artifact "phase-exec-paper-r001-historical-paper-plan-line-reference.json"
$contract = Read-Artifact "phase-exec-paper-r001-current-input-readiness-contract.json"
$missing = Read-Artifact "phase-exec-paper-r001-missing-inputs-diagnostics.json"
$targetClose = Read-Artifact "phase-exec-paper-r001-canonical-target-close-readiness.json"
$bindings = Read-Artifact "phase-exec-paper-r001-readiness-binding-search-results.json"
$risk = Read-Artifact "phase-exec-paper-r001-risk-operator-approval-readiness.json"
$handoffContract = Read-Artifact "phase-exec-paper-r001-r009-dryrun-handoff-contract.json"
$handoff = Read-Artifact "phase-exec-paper-r001-r009-dryrun-handoff-package.json"
$operatorAction = Read-Artifact "phase-exec-paper-r001-operator-action-package.json"
$result = Read-Artifact "phase-exec-paper-r001-readiness-result.json"
$canonical = Read-Artifact "phase-exec-paper-r001-canonical-quarter-hour-policy-preservation.json"
$legacy = Read-Artifact "phase-exec-paper-r001-legacy-compatibility-preservation.json"
$directCross = Read-Artifact "phase-exec-paper-r001-direct-cross-exclusion-preservation.json"
$cost = Read-Artifact "phase-exec-paper-r001-cost-guidance-preservation.json"
$usdPair = Read-Artifact "phase-exec-paper-r001-usd-pair-normalization-preservation.json"
$usdJpy = Read-Artifact "phase-exec-paper-r001-usdjpy-caveat-preservation.json"
$noExternal = Read-Artifact "phase-exec-paper-r001-no-external-audit.json"
$forbidden = Read-Artifact "phase-exec-paper-r001-forbidden-actions-audit.json"
$evidence = Read-Artifact "phase-exec-paper-r001-build-test-validator-evidence.json"

if ($r055.InputsReady -ne $false -or $r055.SafeBlocked -ne $true) { Fail "R055 diagnostics reference unsafe" }
if ($r009.Primary -ne "CloseSeeking15mAdaptive_BalancedAdaptive_v0") { Fail "R009 primary missing" }
if ($r009.ExecutablePromotionAuthorized -ne $false -or $r009.NonExecutable -ne $true -or $r009.NotAnOrder -ne $true -or $r009.NoBrokerRoute -ne $true) {
    Fail "R009 promoted to executable/order/broker behavior"
}
if ($search.CurrentPaperPlanLinesFound -ne $false -or $search.PlanLinesInvented -ne $false -or $search.PmsCycleRunAutomatically -ne $false) {
    Fail "current plan line search invented lines or ran cycle"
}
if ($historical.CurrentUsableForR009 -ne $false) { Fail "historical plan lines incorrectly marked current usable" }
if ($contract.NoAutomaticPmsCycle -ne $true -or $contract.NoPlanLineInvention -ne $true -or $contract.NoOrderDomainOutput -ne $true -or $contract.NoLedgerCommit -ne $true -or $contract.NoExecutablePromotion -ne $true) {
    Fail "current input readiness contract permits forbidden behavior"
}
if ([int]$missing.MissingInputCount -lt 1 -or $missing.SafeBlocked -ne $true -or $missing.OperatorActionRequired -ne $true) {
    Fail "missing-input diagnostics incomplete"
}
if ($targetClose.CurrentCanonicalTargetClosesPresent -ne $false -or $targetClose.Legacy06UsedAsFutureCanonical -ne $false) {
    Fail "canonical target close readiness unsafe"
}
if ($bindings.CurrentPlanLineBindingsFound -ne $false) { Fail "readiness bindings incorrectly claimed" }
if ($risk.CurrentR009RiskReviewFound -ne $false -or $risk.CurrentR009OperatorApprovalFound -ne $false) {
    Fail "current risk/operator approval incorrectly claimed"
}
if ($handoffContract.CurrentInputsReady -ne $false -or $handoffContract.HandoffStatus -ne "BlockedMissingCurrentInputs") {
    Fail "handoff contract should be blocked"
}
if ($handoff.HandoffPackageReady -ne $false -or [int]$handoff.HandoffLineCount -ne 0 -or $handoff.PlanLinesInvented -ne $false) {
    Fail "handoff package claims current lines"
}
if ($operatorAction.OperatorActionRequired -ne $true -or $operatorAction.CommandExecutedInR001 -ne $false -or $operatorAction.ClaimGeneratedFilesExist -ne $false) {
    Fail "operator action package unsafe"
}
if ($result.SafeBlocked -ne $true -or $result.CurrentInputsReady -ne $false -or $result.HandoffPackageReady -ne $false -or $result.PlanLinesInvented -ne $false -or $result.PmsCycleRunAutomatically -ne $false) {
    Fail "readiness result is not safe-blocked"
}

if ($canonical.Legacy06UsedAsFutureCanonical -ne $false) { Fail "legacy :06 used as future canonical" }
if ($legacy.CompatibilityOnly -ne $true -or $legacy.Legacy06UsedAsFutureCanonical -ne $false) { Fail "legacy compatibility weakened" }
if ($directCross.DirectCrossExecutionEnabled -ne $false) { Fail "direct-cross exclusion weakened" }
if ($cost.Universalized -ne $false) { Fail "5 USD/million universalized" }
if ($usdPair.AUDUSDNotFailed -ne $true) { Fail "AUDUSD misclassified" }
if ($usdJpy.CaveatPreserved -ne $true -or $usdJpy.SecurityID -ne "4004" -or $usdJpy.SecurityIDSource -ne "8") { Fail "USDJPY caveat weakened" }

if ($noExternal.PolygonCalled -ne $false -or $noExternal.LmaxCalled -ne $false -or $noExternal.ExternalApiCalled -ne $false -or $noExternal.DownloadsExecuted -ne $false -or $noExternal.BrokerActivated -ne $false -or $noExternal.LiveMarketDataRequested -ne $false) {
    Fail "external/broker action detected"
}
if ($forbidden.ForbiddenActionsOccurred -ne $false -or $forbidden.PmsEmsOmsCycleRunAutomatically -ne $false -or $forbidden.ValidationImportBacktestSimulationExecuted -ne $false -or $forbidden.TcaResultLinesProduced -ne $false -or $forbidden.ExecutableScheduleCreated -ne $false -or $forbidden.PaperLedgerCommitCreated -ne $false -or $forbidden.CurrentPlanLinesInvented -ne $false -or $forbidden.ExecutablePromotionAuthorized -ne $false) {
    Fail "forbidden action audit failed"
}

if ($evidence.DotnetBuildNoRestoreSucceeded -ne $true) { Fail "dotnet build evidence missing or failed" }
if ($evidence.FocusedR001StaticChecksSucceeded -ne $true) { Fail "focused R001 checks evidence missing or failed" }
if ($evidence.UnitTestsFeasible -eq $true -and $evidence.UnitTestsSucceeded -ne $true) { Fail "unit test evidence missing or failed" }

Write-Host "EXEC-PAPER-R001 validation passed"
