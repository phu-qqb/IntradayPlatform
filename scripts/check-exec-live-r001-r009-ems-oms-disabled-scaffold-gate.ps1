$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-live"

function Fail {
    param([string]$Message)
    throw "EXEC-LIVE-R001 validator failed: $Message"
}

function Read-Json {
    param([string]$Name)
    $path = Join-Path $artifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing artifact $Name"
    }

    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$requiredArtifacts = @(
    "phase-exec-live-r001-summary.md",
    "phase-exec-live-r001-r009-paper-maturity-reference.json",
    "phase-exec-live-r001-r014-operating-model-reference.json",
    "phase-exec-live-r001-r009-ems-oms-integration-contract.json",
    "phase-exec-live-r001-r009-execution-intent-contract.json",
    "phase-exec-live-r001-r009-execution-decision-contract.json",
    "phase-exec-live-r001-r009-policy-application-scaffold.json",
    "phase-exec-live-r001-pretrade-risk-gate-contract.json",
    "phase-exec-live-r001-kill-switch-feature-flag-contract.json",
    "phase-exec-live-r001-disabled-boundary-guard-contract.json",
    "phase-exec-live-r001-idempotency-audit-contract.json",
    "phase-exec-live-r001-supported-symbol-universe.json",
    "phase-exec-live-r001-direct-cross-exclusion-preservation.json",
    "phase-exec-live-r001-usd-pair-netting-requirement.json",
    "phase-exec-live-r001-usdjpy-caveat-preservation.json",
    "phase-exec-live-r001-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-live-r001-legacy-compatibility-preservation.json",
    "phase-exec-live-r001-cost-guidance-preservation.json",
    "phase-exec-live-r001-nonmajor-calibration-preservation.json",
    "phase-exec-live-r001-no-broker-activation-audit.json",
    "phase-exec-live-r001-no-live-marketdata-audit.json",
    "phase-exec-live-r001-no-scheduler-service-polling-audit.json",
    "phase-exec-live-r001-no-order-created-audit.json",
    "phase-exec-live-r001-no-child-order-audit.json",
    "phase-exec-live-r001-no-executable-schedule-audit.json",
    "phase-exec-live-r001-no-route-no-submission-audit.json",
    "phase-exec-live-r001-no-fill-execution-report-audit.json",
    "phase-exec-live-r001-no-paper-ledger-commit-audit.json",
    "phase-exec-live-r001-no-state-mutation-audit.json",
    "phase-exec-live-r001-no-external-audit.json",
    "phase-exec-live-r001-forbidden-actions-audit.json",
    "phase-exec-live-r001-next-phase-recommendation.json",
    "phase-exec-live-r001-r009-contract-reference.json",
    "phase-exec-live-r001-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $artifact))) {
        Fail "Required artifact missing: $artifact"
    }
}

$sourcePath = Join-Path $repoRoot "src/QQ.Production.Intraday.Application/R009EmsOmsDisabledScaffold.cs"
$testPath = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/R009EmsOmsDisabledScaffoldTests.cs"
if (-not (Test-Path -LiteralPath $sourcePath)) { Fail "Missing disabled scaffold source file" }
if (-not (Test-Path -LiteralPath $testPath)) { Fail "Missing focused R001 tests" }

$source = Get-Content -LiteralPath $sourcePath -Raw
if ($source -notmatch "R009DisabledEmsOmsExecutionAdapter") { Fail "Disabled adapter class missing" }
if ($source -notmatch "R009LiveTradingEnabled: false") { Fail "Live trading flag default is not visibly disabled" }
if ($source -notmatch "R009BrokerRoutingEnabled: false") { Fail "Broker routing flag default is not visibly disabled" }
if ($source -notmatch "R009OrderSubmissionEnabled: false") { Fail "Order submission flag default is not visibly disabled" }
if ($source -notmatch "ControlledResidualCrossAlwaysMarketAtClose: false") { Fail "Controlled residual cross AlwaysMarketAtClose guard missing" }
if ($source -notmatch "CreatesOrder: false") { Fail "Order creation guard missing" }
if ($source -notmatch "CreatesRoute: false") { Fail "Route creation guard missing" }
if ($source -notmatch "CreatesFill: false") { Fail "Fill creation guard missing" }
if ($source -notmatch "CreatesExecutableSchedule: false") { Fail "Executable schedule creation guard missing" }

$r009 = Read-Json "phase-exec-live-r001-r009-contract-reference.json"
if ($r009.ExecutablePromotionAuthorized -ne $false) { Fail "R009 executable promotion is not blocked" }
if ($r009.BrokerReady -ne $false) { Fail "R009 broker ready was set true" }
if ($r009.LiveReady -ne $false) { Fail "R009 live ready was set true" }
if ($r009.NonExecutable -ne $true -or $r009.NotAnOrder -ne $true -or $r009.NoBrokerRoute -ne $true) {
    Fail "R009 non-executable contract flags weakened"
}

$featureFlags = Read-Json "phase-exec-live-r001-kill-switch-feature-flag-contract.json"
if ($featureFlags.R009LiveTradingEnabled -ne $false) { Fail "R009 live trading flag defaults true" }
if ($featureFlags.R009BrokerRoutingEnabled -ne $false) { Fail "R009 broker routing flag defaults true" }
if ($featureFlags.R009OrderSubmissionEnabled -ne $false) { Fail "R009 order submission flag defaults true" }
if ($featureFlags.R009ExecutableScheduleEnabled -ne $false) { Fail "R009 executable schedule flag defaults true" }
if ($featureFlags.R009PaperLedgerCommitEnabled -ne $false) { Fail "R009 paper ledger commit flag defaults true" }
if ($featureFlags.R009SchedulerEnabled -ne $false -or $featureFlags.R009BackgroundWorkerEnabled -ne $false) {
    Fail "Scheduler/background worker flags are enabled"
}
if ($featureFlags.R009DryRunOnly -ne $true) { Fail "R009 dry-run-only flag is not true" }

$boundary = Read-Json "phase-exec-live-r001-disabled-boundary-guard-contract.json"
foreach ($property in @(
    "BrokerRouteCreationAllowed",
    "OrderCreationAllowed",
    "ChildSliceCreationAllowed",
    "ChildOrderCreationAllowed",
    "ScheduleExecutionAllowed",
    "SubmissionAllowed",
    "FillCreationAllowed",
    "ExecutionReportCreationAllowed",
    "StateMutationAllowed",
    "PaperLedgerCommitAllowed"
)) {
    if ($boundary.$property -ne $false) {
        Fail "Disabled boundary guard weakened: $property"
    }
}

$decision = Read-Json "phase-exec-live-r001-r009-execution-decision-contract.json"
$forbidden = @($decision.ForbiddenOutputs)
foreach ($output in @("Order", "ChildOrder", "Route", "Submission", "Fill", "ExecutionReport", "ExecutableSchedule")) {
    if ($forbidden -notcontains $output) {
        Fail "Forbidden output omitted: $output"
    }
}
if ($decision.CreationFlags.CreatesOrder -ne $false -or
    $decision.CreationFlags.CreatesChildOrder -ne $false -or
    $decision.CreationFlags.CreatesRoute -ne $false -or
    $decision.CreationFlags.CreatesSubmission -ne $false -or
    $decision.CreationFlags.CreatesFill -ne $false -or
    $decision.CreationFlags.CreatesExecutionReport -ne $false -or
    $decision.CreationFlags.CreatesExecutableSchedule -ne $false) {
    Fail "Decision contract creates executable/order-like output"
}

$directCross = Read-Json "phase-exec-live-r001-direct-cross-exclusion-preservation.json"
if ($directCross.DirectCrossExecutionAllowed -ne $false) { Fail "Direct-cross execution is allowed" }

$legacy = Read-Json "phase-exec-live-r001-legacy-compatibility-preservation.json"
if ($legacy.UsedAsFutureCanonical -ne $false) { Fail "Legacy :06 timestamps used as future canonical" }

$cost = Read-Json "phase-exec-live-r001-cost-guidance-preservation.json"
if ($cost.Universalized -ne $false) { Fail "5 USD/million guidance was universalized" }

$usdjpy = Read-Json "phase-exec-live-r001-usdjpy-caveat-preservation.json"
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or
    $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or
    $usdjpy.RequiresInversion -ne $true -or
    $usdjpy.SecurityID -ne "4004" -or
    $usdjpy.SecurityIDSource -ne "8") {
    Fail "USDJPY caveat weakened"
}

$universe = Read-Json "phase-exec-live-r001-supported-symbol-universe.json"
if (@($universe.SupportedExecutionSymbols) -notcontains "AUDUSD") { Fail "AUDUSD missing from supported universe" }
if ($universe.AudusdStatus -ne "SupportedAndNotFailed") { Fail "AUDUSD misclassified" }
if ($universe.NonmajorEmScandiCnh -notmatch "CalibrationRequired") { Fail "Nonmajor calibration preservation missing" }

$forbiddenActions = Read-Json "phase-exec-live-r001-forbidden-actions-audit.json"
if ($forbiddenActions.ExternalApiCallsMade -ne $false) { Fail "External API call recorded" }
if ($forbiddenActions.BrokerLiveOrderRouteFillScheduleLedgerStatePathsEnabled -ne $false) {
    Fail "Broker/live/order/route/fill/schedule/ledger/state path enabled"
}
if ($forbiddenActions.R009PromotedToExecutableUse -ne $false) { Fail "R009 promoted to executable use" }

$evidence = Read-Json "phase-exec-live-r001-build-test-validator-evidence.json"
if ($evidence.Build -ne "Passed") { Fail "Build evidence missing or not passed" }
if ($evidence.FocusedR001Tests -ne "Passed") { Fail "Focused R001 test evidence missing or not passed" }
if ($evidence.UnitTests -ne "Passed") { Fail "Unit test evidence missing or not passed" }
if ($evidence.Validator -ne "Passed") { Fail "Validator evidence missing or not passed" }

Write-Host "EXEC-LIVE-R001 validator passed."
