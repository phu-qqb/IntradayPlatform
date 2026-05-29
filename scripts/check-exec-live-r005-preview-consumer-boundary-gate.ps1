$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-live"

function Fail {
    param([string]$Message)
    throw "EXEC-LIVE-R005 validator failed: $Message"
}

function Read-Json {
    param([string]$Name)
    $path = Join-Path $artifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { Fail "Missing artifact $Name" }
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$requiredArtifacts = @(
    "phase-exec-live-r005-summary.md",
    "phase-exec-live-r005-r004-batch-contract-reference.json",
    "phase-exec-live-r005-r009-contract-reference.json",
    "phase-exec-live-r005-consumer-handoff-contract.json",
    "phase-exec-live-r005-allowed-consumers.json",
    "phase-exec-live-r005-forbidden-consumers.json",
    "phase-exec-live-r005-preview-response-usage-policy.json",
    "phase-exec-live-r005-ems-oms-boundary-model.json",
    "phase-exec-live-r005-preview-boundary-guard-contract.json",
    "phase-exec-live-r005-preview-consumer-audit-contract.json",
    "phase-exec-live-r005-valid-consumer-request-examples.json",
    "phase-exec-live-r005-invalid-consumer-rejection-results.json",
    "phase-exec-live-r005-preview-output-not-order-audit.json",
    "phase-exec-live-r005-preview-output-not-route-audit.json",
    "phase-exec-live-r005-preview-output-not-schedule-audit.json",
    "phase-exec-live-r005-preview-output-not-ledger-audit.json",
    "phase-exec-live-r005-kill-switch-feature-flag-review.json",
    "phase-exec-live-r005-disabled-boundary-guard-review.json",
    "phase-exec-live-r005-idempotency-audit-review.json",
    "phase-exec-live-r005-no-broker-activation-audit.json",
    "phase-exec-live-r005-no-live-marketdata-audit.json",
    "phase-exec-live-r005-no-scheduler-service-polling-audit.json",
    "phase-exec-live-r005-no-order-created-audit.json",
    "phase-exec-live-r005-no-child-order-audit.json",
    "phase-exec-live-r005-no-executable-schedule-audit.json",
    "phase-exec-live-r005-no-route-no-submission-audit.json",
    "phase-exec-live-r005-no-fill-execution-report-audit.json",
    "phase-exec-live-r005-no-paper-ledger-commit-audit.json",
    "phase-exec-live-r005-no-state-mutation-audit.json",
    "phase-exec-live-r005-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-live-r005-legacy-compatibility-preservation.json",
    "phase-exec-live-r005-direct-cross-exclusion-preservation.json",
    "phase-exec-live-r005-usd-pair-netting-requirement.json",
    "phase-exec-live-r005-usdjpy-caveat-preservation.json",
    "phase-exec-live-r005-cost-guidance-preservation.json",
    "phase-exec-live-r005-nonmajor-calibration-preservation.json",
    "phase-exec-live-r005-no-external-audit.json",
    "phase-exec-live-r005-forbidden-actions-audit.json",
    "phase-exec-live-r005-next-phase-recommendation.json",
    "phase-exec-live-r005-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $artifact))) {
        Fail "Required artifact missing: $artifact"
    }
}

$sourcePath = Join-Path $repoRoot "src/QQ.Production.Intraday.Application/R009EmsOmsDisabledScaffold.cs"
$testPath = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/R009PreviewConsumerBoundaryTests.cs"
if (-not (Test-Path -LiteralPath $sourcePath)) { Fail "Missing R009 scaffold source file" }
if (-not (Test-Path -LiteralPath $testPath)) { Fail "Missing focused R005 tests" }

$source = Get-Content -LiteralPath $sourcePath -Raw
foreach ($needle in @(
    "R009PreviewConsumerRequestEnvelope",
    "R009PreviewConsumerResponseEnvelope",
    "R009PreviewUsagePolicy",
    "R009PreviewBoundaryGuard",
    "R009PreviewConsumerAuditRecord",
    "R009PreviewConsumerBoundaryService",
    "ForbiddenConsumer",
    "ForbiddenUsage"
)) {
    if ($source -notmatch [regex]::Escape($needle)) { Fail "Source missing $needle" }
}

$allowed = Read-Json "phase-exec-live-r005-allowed-consumers.json"
foreach ($consumer in @("InternalPmsPreviewConsumer", "InternalEmsPreviewConsumer", "InternalOmsPreviewConsumer", "OperatorReviewTool", "TestHarness")) {
    if (@($allowed.AllowedConsumers) -notcontains $consumer) { Fail "Allowed consumer missing $consumer" }
}
foreach ($forbidden in @("BrokerGateway", "OrderRouter", "Scheduler", "PaperLedgerCommitter")) {
    if (@($allowed.AllowedConsumers) -contains $forbidden) { Fail "$forbidden is allowed as consumer" }
}

$forbiddenConsumers = Read-Json "phase-exec-live-r005-forbidden-consumers.json"
foreach ($consumer in @("BrokerGateway", "OrderRouter", "Scheduler", "PaperLedgerCommitter", "ProductionTradingRuntime")) {
    if (@($forbiddenConsumers.ForbiddenConsumers) -notcontains $consumer) { Fail "Forbidden consumer missing $consumer" }
}

$usage = Read-Json "phase-exec-live-r005-preview-response-usage-policy.json"
if ($usage.PreviewOutputIsOrderIntent -ne $false) { Fail "Preview output is order intent" }
if ($usage.PreviewOutputIsRouteable -ne $false) { Fail "Preview output is routeable" }
if ($usage.PreviewOutputIsExecutableSchedule -ne $false) { Fail "Preview output is executable schedule" }
if ($usage.PreviewOutputIsFillReportInput -ne $false) { Fail "Preview output is fill/report input" }
foreach ($forbiddenUsage in @("ConvertToOrder", "ConvertToRouteSubmission", "CommitLedger", "TriggerBroker", "TriggerSchedulerWorker", "GenerateFillExecutionReport")) {
    if (@($usage.ForbiddenUsage) -notcontains $forbiddenUsage) { Fail "Forbidden usage missing $forbiddenUsage" }
}

$guard = Read-Json "phase-exec-live-r005-preview-boundary-guard-contract.json"
foreach ($property in @("NonExecutable", "NotAnOrder", "NotSubmitted", "NoBrokerRoute", "NoRoute", "NoSubmission", "NoFill", "NoExecutionReport", "NoPaperLedgerCommit", "NoStateMutation")) {
    if ($guard.$property -ne $true) { Fail "Preview boundary guard missing $property=true" }
}

$invalid = Read-Json "phase-exec-live-r005-invalid-consumer-rejection-results.json"
$allReasons = @($invalid.Results | ForEach-Object { $_.RejectionReasons } | ForEach-Object { $_ })
foreach ($expected in @("ForbiddenConsumer:BrokerGateway", "ForbiddenConsumer:OrderRouter", "ForbiddenConsumer:Scheduler", "ForbiddenConsumer:PaperLedgerCommitter", "ForbiddenUsage:ConvertToOrder", "ForbiddenUsage:ConvertToRouteSubmission", "ForbiddenUsage:CommitLedger")) {
    if ($allReasons -notcontains $expected) { Fail "Invalid consumer rejection missing $expected" }
}
foreach ($result in @($invalid.Results)) {
    if ($result.Accepted -ne $false) { Fail "Invalid consumer/usage accepted" }
}

$orderAudit = Read-Json "phase-exec-live-r005-preview-output-not-order-audit.json"
if ($orderAudit.CanConvertToOrder -ne $false -or $orderAudit.CanConvertToChildOrder -ne $false -or $orderAudit.NotAnOrder -ne $true) { Fail "Preview output can become order" }
$routeAudit = Read-Json "phase-exec-live-r005-preview-output-not-route-audit.json"
if ($routeAudit.CanConvertToRoute -ne $false -or $routeAudit.CanConvertToSubmission -ne $false -or $routeAudit.NoBrokerRoute -ne $true) { Fail "Preview output can become route/submission" }
$scheduleAudit = Read-Json "phase-exec-live-r005-preview-output-not-schedule-audit.json"
if ($scheduleAudit.CanConvertToExecutableSchedule -ne $false) { Fail "Preview output can become schedule" }
$ledgerAudit = Read-Json "phase-exec-live-r005-preview-output-not-ledger-audit.json"
if ($ledgerAudit.CanCommitPaperLedger -ne $false -or $ledgerAudit.NoStateMutation -ne $true) { Fail "Preview output can commit ledger or mutate state" }

$flags = Read-Json "phase-exec-live-r005-kill-switch-feature-flag-review.json"
if ($flags.R009LiveTradingEnabled -ne $false -or
    $flags.R009BrokerRoutingEnabled -ne $false -or
    $flags.R009OrderSubmissionEnabled -ne $false -or
    $flags.R009ExecutableScheduleEnabled -ne $false -or
    $flags.R009PaperLedgerCommitEnabled -ne $false -or
    $flags.R009SchedulerEnabled -ne $false -or
    $flags.R009BackgroundWorkerEnabled -ne $false -or
    $flags.R009DryRunOnly -ne $true) { Fail "Kill-switch flags weakened" }

$disabled = Read-Json "phase-exec-live-r005-disabled-boundary-guard-review.json"
foreach ($property in @("BrokerRouteCreationAllowed", "OrderCreationAllowed", "ChildSliceCreationAllowed", "ChildOrderCreationAllowed", "ScheduleExecutionAllowed", "SubmissionAllowed", "FillCreationAllowed", "ExecutionReportCreationAllowed", "StateMutationAllowed", "PaperLedgerCommitAllowed")) {
    if ($disabled.$property -ne $false) { Fail "Disabled boundary guard weakened: $property" }
}

$legacy = Read-Json "phase-exec-live-r005-legacy-compatibility-preservation.json"
if ($legacy.UsedAsFutureCanonical -ne $false) { Fail "Legacy :06 used as future canonical" }
$direct = Read-Json "phase-exec-live-r005-direct-cross-exclusion-preservation.json"
if ($direct.DirectCrossExecutionAllowed -ne $false) { Fail "Direct-cross execution allowed" }
$cost = Read-Json "phase-exec-live-r005-cost-guidance-preservation.json"
if ($cost.Universalized -ne $false) { Fail "5 USD/million universalized" }
$usdPair = Read-Json "phase-exec-live-r005-usd-pair-netting-requirement.json"
if ($usdPair.AudusdStatus -ne "SupportedAndNotFailed") { Fail "AUDUSD misclassified" }
$usdjpy = Read-Json "phase-exec-live-r005-usdjpy-caveat-preservation.json"
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or $usdjpy.RequiresInversion -ne $true -or $usdjpy.SecurityID -ne "4004" -or $usdjpy.SecurityIDSource -ne "8") {
    Fail "USDJPY caveat weakened"
}

$forbidden = Read-Json "phase-exec-live-r005-forbidden-actions-audit.json"
if ($forbidden.ExternalApiCallsMade -ne $false) { Fail "External API call recorded" }
if ($forbidden.BrokerLiveOrderRouteFillScheduleLedgerStatePathsEnabled -ne $false) { Fail "Broker/live/order path enabled" }
if ($forbidden.PmsEmsOmsCycleRun -ne $false) { Fail "PMS/EMS/OMS cycle was run" }
if ($forbidden.ManualNoExternalCommandRun -ne $false) { Fail "ManualNoExternal command was run" }
if ($forbidden.R009PromotedToExecutableUse -ne $false) { Fail "R009 promoted to executable use" }

$evidence = Read-Json "phase-exec-live-r005-build-test-validator-evidence.json"
if ($evidence.Build -ne "Passed") { Fail "Build evidence missing or not passed" }
if ($evidence.FocusedR005Tests -ne "Passed") { Fail "Focused R005 test evidence missing or not passed" }
if ($evidence.UnitTests -ne "Passed") { Fail "Unit test evidence missing or not passed" }
if ($evidence.Validator -ne "Passed") { Fail "Validator evidence missing or not passed" }

Write-Host "EXEC-LIVE-R005 validator passed."
