$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-live"

function Fail {
    param([string]$Message)
    throw "EXEC-LIVE-R003 validator failed: $Message"
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
    "phase-exec-live-r003-summary.md",
    "phase-exec-live-r003-r002-preview-reference.json",
    "phase-exec-live-r003-r009-contract-reference.json",
    "phase-exec-live-r003-api-cli-contract.json",
    "phase-exec-live-r003-request-dto-contract.json",
    "phase-exec-live-r003-response-dto-contract.json",
    "phase-exec-live-r003-disabled-preview-service-contract.json",
    "phase-exec-live-r003-sample-inline-request.json",
    "phase-exec-live-r003-sample-artifact-request.json",
    "phase-exec-live-r003-sample-preview-response.json",
    "phase-exec-live-r003-invalid-request-rejection-results.json",
    "phase-exec-live-r003-decision-preview-output-audit.json",
    "phase-exec-live-r003-kill-switch-feature-flag-review.json",
    "phase-exec-live-r003-disabled-boundary-guard-review.json",
    "phase-exec-live-r003-idempotency-audit-review.json",
    "phase-exec-live-r003-no-broker-activation-audit.json",
    "phase-exec-live-r003-no-live-marketdata-audit.json",
    "phase-exec-live-r003-no-scheduler-service-polling-audit.json",
    "phase-exec-live-r003-no-order-created-audit.json",
    "phase-exec-live-r003-no-child-order-audit.json",
    "phase-exec-live-r003-no-executable-schedule-audit.json",
    "phase-exec-live-r003-no-route-no-submission-audit.json",
    "phase-exec-live-r003-no-fill-execution-report-audit.json",
    "phase-exec-live-r003-no-paper-ledger-commit-audit.json",
    "phase-exec-live-r003-no-state-mutation-audit.json",
    "phase-exec-live-r003-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-live-r003-legacy-compatibility-preservation.json",
    "phase-exec-live-r003-direct-cross-exclusion-preservation.json",
    "phase-exec-live-r003-usd-pair-netting-requirement.json",
    "phase-exec-live-r003-usdjpy-caveat-preservation.json",
    "phase-exec-live-r003-cost-guidance-preservation.json",
    "phase-exec-live-r003-nonmajor-calibration-preservation.json",
    "phase-exec-live-r003-no-external-audit.json",
    "phase-exec-live-r003-forbidden-actions-audit.json",
    "phase-exec-live-r003-next-phase-recommendation.json",
    "phase-exec-live-r003-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $artifact))) {
        Fail "Required artifact missing: $artifact"
    }
}

$sourcePath = Join-Path $repoRoot "src/QQ.Production.Intraday.Application/R009EmsOmsDisabledScaffold.cs"
$testPath = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/R009DisabledPreviewApiCliContractTests.cs"
if (-not (Test-Path -LiteralPath $sourcePath)) { Fail "Missing R009 scaffold source file" }
if (-not (Test-Path -LiteralPath $testPath)) { Fail "Missing focused R003 tests" }

$source = Get-Content -LiteralPath $sourcePath -Raw
if ($source -notmatch "R009DisabledPreviewRequest") { Fail "Disabled preview request DTO missing" }
if ($source -notmatch "R009DisabledPreviewResponse") { Fail "Disabled preview response DTO missing" }
if ($source -notmatch "R009DisabledPreviewContractService") { Fail "Disabled preview contract service missing" }
if ($source -notmatch "LiveTradingMustRemainDisabled") { Fail "Live trading rejection missing" }
if ($source -notmatch "BrokerRoutingMustRemainDisabled") { Fail "Broker routing rejection missing" }
if ($source -notmatch "OrderSubmissionMustRemainDisabled") { Fail "Order submission rejection missing" }
if ($source -notmatch "ForbiddenOutputRequested") { Fail "Forbidden output rejection missing" }

$contract = Read-Json "phase-exec-live-r003-api-cli-contract.json"
if ($contract.LiveTradingAllowed -ne $false) { Fail "API/CLI contract allows live trading" }
if ($contract.BrokerRoutingAllowed -ne $false) { Fail "API/CLI contract allows broker routing" }
if ($contract.OrderSubmissionAllowed -ne $false) { Fail "API/CLI contract allows order submission" }
if ($contract.ExecutableScheduleAllowed -ne $false) { Fail "API/CLI contract allows executable schedule" }
if ($contract.PaperLedgerCommitAllowed -ne $false) { Fail "API/CLI contract allows paper ledger commit" }
if ($contract.BrokerRouteRegistered -ne $false -or $contract.SchedulerWorkerRegistered -ne $false -or $contract.LiveRuntimeRegistered -ne $false) {
    Fail "API/CLI contract registered live runtime, broker route, or scheduler"
}
foreach ($forbidden in @("Order", "ChildOrder", "Route", "Submission", "Fill", "ExecutionReport", "ExecutableSchedule")) {
    if (@($contract.ForbiddenRequestedOutputs) -notcontains $forbidden) {
        Fail "Contract omits forbidden requested output $forbidden"
    }
}

$request = Read-Json "phase-exec-live-r003-sample-inline-request.json"
if ($request.RequestMode -ne "DisabledPreviewOnly") { Fail "Sample request not disabled preview only" }
if ($request.DryRunOnly -ne $true) { Fail "Sample request missing DryRunOnly=true" }
if ($request.LiveTradingEnabled -ne $false) { Fail "Sample request enables live trading" }
if ($request.BrokerRoutingEnabled -ne $false) { Fail "Sample request enables broker routing" }
if ($request.OrderSubmissionEnabled -ne $false) { Fail "Sample request enables order submission" }
if ($request.ExecutableScheduleEnabled -ne $false) { Fail "Sample request enables executable schedule" }
if ($request.PaperLedgerCommitEnabled -ne $false) { Fail "Sample request enables paper ledger commit" }
if ($request.NoBrokerRoute -ne $true) { Fail "Sample request does not require NoBrokerRoute" }

$response = Read-Json "phase-exec-live-r003-sample-preview-response.json"
if ($response.NonExecutable -ne $true -or
    $response.NotAnOrder -ne $true -or
    $response.NotSubmitted -ne $true -or
    $response.NoBrokerRoute -ne $true -or
    $response.NoFill -ne $true -or
    $response.NoExecutionReport -ne $true -or
    $response.NoRoute -ne $true -or
    $response.NoSubmission -ne $true -or
    $response.NoPaperLedgerCommit -ne $true) {
    Fail "Sample response safety flags weakened"
}
if ($response.SafetyFlags.LiveTradingEnabled -ne $false -or
    $response.SafetyFlags.BrokerRoutingEnabled -ne $false -or
    $response.SafetyFlags.OrderSubmissionEnabled -ne $false -or
    $response.SafetyFlags.ExecutableScheduleEnabled -ne $false -or
    $response.SafetyFlags.PaperLedgerCommitEnabled -ne $false -or
    $response.SafetyFlags.DryRunOnly -ne $true) {
    Fail "Sample response safety flags allow forbidden path"
}
foreach ($decision in @($response.DecisionPreviews)) {
    if ($decision.CreatesOrder -ne $false -or
        $decision.CreatesChildOrder -ne $false -or
        $decision.CreatesRoute -ne $false -or
        $decision.CreatesSubmission -ne $false -or
        $decision.CreatesFill -ne $false -or
        $decision.CreatesExecutionReport -ne $false -or
        $decision.CreatesExecutableSchedule -ne $false) {
        Fail "Sample response decision creates executable/order-like output"
    }
    foreach ($output in @($decision.Outputs)) {
        if ($output -in @("Order", "ChildOrder", "Route", "Submission", "Fill", "ExecutionReport", "ExecutableSchedule")) {
            Fail "Sample response represented as forbidden output $output"
        }
    }
}

$invalid = Read-Json "phase-exec-live-r003-invalid-request-rejection-results.json"
$allReasons = @($invalid.Results | ForEach-Object { $_.RejectionReasons } | ForEach-Object { $_ })
foreach ($expected in @(
    "LiveTradingMustRemainDisabled",
    "BrokerRoutingMustRemainDisabled",
    "OrderSubmissionMustRemainDisabled",
    "ForbiddenOutputRequested:ExecutableSchedule",
    "PaperLedgerCommitMustRemainDisabled"
)) {
    if ($allReasons -notcontains $expected) {
        Fail "Invalid request rejection missing $expected"
    }
}
foreach ($result in @($invalid.Results)) {
    if ($result.Accepted -ne $false) { Fail "Invalid request was accepted: $($result.RequestId)" }
    if ($result.DecisionPreviewCount -ne 0) { Fail "Invalid request generated decision previews: $($result.RequestId)" }
}

$flags = Read-Json "phase-exec-live-r003-kill-switch-feature-flag-review.json"
if ($flags.R009LiveTradingEnabled -ne $false) { Fail "R009 live trading flag defaults true" }
if ($flags.R009BrokerRoutingEnabled -ne $false) { Fail "R009 broker routing flag defaults true" }
if ($flags.R009OrderSubmissionEnabled -ne $false) { Fail "R009 order submission flag defaults true" }
if ($flags.R009ExecutableScheduleEnabled -ne $false) { Fail "R009 executable schedule flag defaults true" }
if ($flags.R009PaperLedgerCommitEnabled -ne $false) { Fail "R009 paper ledger commit flag defaults true" }
if ($flags.R009SchedulerEnabled -ne $false -or $flags.R009BackgroundWorkerEnabled -ne $false) { Fail "Scheduler/background flag enabled" }
if ($flags.R009DryRunOnly -ne $true) { Fail "DryRunOnly flag is not true" }

$guard = Read-Json "phase-exec-live-r003-disabled-boundary-guard-review.json"
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
    if ($guard.$property -ne $false) {
        Fail "Disabled boundary guard weakened: $property"
    }
}

$outputAudit = Read-Json "phase-exec-live-r003-decision-preview-output-audit.json"
if ($outputAudit.ForbiddenOutputCount -ne 0) { Fail "Forbidden output count nonzero" }
if ($outputAudit.CreatesOrder -ne $false -or
    $outputAudit.CreatesChildOrder -ne $false -or
    $outputAudit.CreatesRoute -ne $false -or
    $outputAudit.CreatesSubmission -ne $false -or
    $outputAudit.CreatesFill -ne $false -or
    $outputAudit.CreatesExecutionReport -ne $false -or
    $outputAudit.CreatesExecutableSchedule -ne $false) {
    Fail "Output audit creates executable/order-like output"
}

$legacy = Read-Json "phase-exec-live-r003-legacy-compatibility-preservation.json"
if ($legacy.UsedAsFutureCanonical -ne $false) { Fail "Legacy :06 used as future canonical" }

$directCross = Read-Json "phase-exec-live-r003-direct-cross-exclusion-preservation.json"
if ($directCross.DirectCrossExecutionAllowed -ne $false) { Fail "Direct-cross execution is allowed" }

$cost = Read-Json "phase-exec-live-r003-cost-guidance-preservation.json"
if ($cost.Universalized -ne $false) { Fail "5 USD/million was universalized" }

$usdjpy = Read-Json "phase-exec-live-r003-usdjpy-caveat-preservation.json"
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or
    $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or
    $usdjpy.RequiresInversion -ne $true -or
    $usdjpy.SecurityID -ne "4004" -or
    $usdjpy.SecurityIDSource -ne "8") {
    Fail "USDJPY caveat weakened"
}

$r009 = Read-Json "phase-exec-live-r003-r009-contract-reference.json"
if ($r009.BrokerReady -ne $false -or $r009.LiveReady -ne $false -or $r009.ExecutablePromotionAuthorized -ne $false) {
    Fail "R009 readiness/promotion flags weakened"
}
if ($r009.NonExecutable -ne $true -or $r009.NotAnOrder -ne $true -or $r009.NoBrokerRoute -ne $true) {
    Fail "R009 non-executable contract flags weakened"
}

$forbidden = Read-Json "phase-exec-live-r003-forbidden-actions-audit.json"
if ($forbidden.ExternalApiCallsMade -ne $false) { Fail "External API call recorded" }
if ($forbidden.BrokerLiveOrderRouteFillScheduleLedgerStatePathsEnabled -ne $false) { Fail "Broker/live/order path enabled" }
if ($forbidden.PmsEmsOmsCycleRun -ne $false) { Fail "PMS/EMS/OMS cycle was run" }
if ($forbidden.ManualNoExternalCommandRun -ne $false) { Fail "ManualNoExternal command was run" }
if ($forbidden.R009PromotedToExecutableUse -ne $false) { Fail "R009 promoted to executable use" }

$evidence = Read-Json "phase-exec-live-r003-build-test-validator-evidence.json"
if ($evidence.Build -ne "Passed") { Fail "Build evidence missing or not passed" }
if ($evidence.FocusedR003Tests -ne "Passed") { Fail "Focused R003 test evidence missing or not passed" }
if ($evidence.UnitTests -ne "Passed") { Fail "Unit test evidence missing or not passed" }
if ($evidence.Validator -ne "Passed") { Fail "Validator evidence missing or not passed" }

Write-Host "EXEC-LIVE-R003 validator passed."
