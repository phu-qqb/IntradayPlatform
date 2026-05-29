$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-live"

function Fail {
    param([string]$Message)
    throw "EXEC-LIVE-R007 validator failed: $Message"
}

function Read-Json {
    param([string]$Name)
    $path = Join-Path $artifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { Fail "Missing artifact $Name" }
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$requiredArtifacts = @(
    "phase-exec-live-r007-summary.md",
    "phase-exec-live-r007-r006-audit-reference.json",
    "phase-exec-live-r007-r005-consumer-boundary-reference.json",
    "phase-exec-live-r007-r009-contract-reference.json",
    "phase-exec-live-r007-operator-review-contract.json",
    "phase-exec-live-r007-operator-cli-contract.json",
    "phase-exec-live-r007-operator-review-request-dto-contract.json",
    "phase-exec-live-r007-operator-review-response-dto-contract.json",
    "phase-exec-live-r007-operator-review-summary-contract.json",
    "phase-exec-live-r007-operator-held-reason-summary-contract.json",
    "phase-exec-live-r007-operator-rejected-reason-summary-contract.json",
    "phase-exec-live-r007-sample-list-audit-response.json",
    "phase-exec-live-r007-sample-single-record-review.json",
    "phase-exec-live-r007-sample-batch-summary.json",
    "phase-exec-live-r007-sample-held-line-summary.json",
    "phase-exec-live-r007-sample-operator-report.md",
    "phase-exec-live-r007-invalid-review-command-rejection-results.json",
    "phase-exec-live-r007-review-output-not-order-audit.json",
    "phase-exec-live-r007-review-output-not-route-audit.json",
    "phase-exec-live-r007-review-output-not-schedule-audit.json",
    "phase-exec-live-r007-review-output-not-ledger-audit.json",
    "phase-exec-live-r007-artifact-path-safety-review.json",
    "phase-exec-live-r007-kill-switch-feature-flag-review.json",
    "phase-exec-live-r007-disabled-boundary-guard-review.json",
    "phase-exec-live-r007-no-broker-activation-audit.json",
    "phase-exec-live-r007-no-live-marketdata-audit.json",
    "phase-exec-live-r007-no-scheduler-service-polling-audit.json",
    "phase-exec-live-r007-no-order-created-audit.json",
    "phase-exec-live-r007-no-child-order-audit.json",
    "phase-exec-live-r007-no-executable-schedule-audit.json",
    "phase-exec-live-r007-no-route-no-submission-audit.json",
    "phase-exec-live-r007-no-fill-execution-report-audit.json",
    "phase-exec-live-r007-no-paper-ledger-commit-audit.json",
    "phase-exec-live-r007-no-state-mutation-audit.json",
    "phase-exec-live-r007-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-live-r007-legacy-compatibility-preservation.json",
    "phase-exec-live-r007-direct-cross-exclusion-preservation.json",
    "phase-exec-live-r007-usd-pair-netting-requirement.json",
    "phase-exec-live-r007-usdjpy-caveat-preservation.json",
    "phase-exec-live-r007-cost-guidance-preservation.json",
    "phase-exec-live-r007-nonmajor-calibration-preservation.json",
    "phase-exec-live-r007-no-external-audit.json",
    "phase-exec-live-r007-forbidden-actions-audit.json",
    "phase-exec-live-r007-next-phase-recommendation.json",
    "phase-exec-live-r007-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $artifact))) {
        Fail "Required artifact missing: $artifact"
    }
}

$sourcePath = Join-Path $repoRoot "src/QQ.Production.Intraday.Application/R009EmsOmsDisabledScaffold.cs"
$testPath = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/R009OperatorPreviewReviewTests.cs"
if (-not (Test-Path -LiteralPath $sourcePath)) { Fail "Missing R009 scaffold source file" }
if (-not (Test-Path -LiteralPath $testPath)) { Fail "Missing focused R007 tests" }

$source = Get-Content -LiteralPath $sourcePath -Raw
foreach ($needle in @(
    "R009OperatorPreviewReviewRequest",
    "R009OperatorPreviewReviewResponse",
    "R009OperatorPreviewSummary",
    "R009OperatorHeldReasonSummary",
    "R009OperatorRejectedReasonSummary",
    "R009OperatorAuditRecordReference",
    "R009OperatorReviewExport",
    "R009OperatorPreviewReviewService",
    "ForbiddenCommandMode",
    "ReviewWritePathMustBeArtifactsReadinessExecutionLiveOperatorReview"
)) {
    if ($source -notmatch [regex]::Escape($needle)) { Fail "Source missing $needle" }
}

$tests = Get-Content -LiteralPath $testPath -Raw
foreach ($needle in @(
    "Operator_review_can_list_audit_records_from_artifact_path",
    "Operator_review_can_summarize_batch",
    "Held_readiness_and_rejected_direct_cross_are_review_only_not_orders",
    "Forbidden_command_modes_are_rejected",
    "Forbidden_consumer_broker_gateway_is_rejected",
    "Operator_review_output_cannot_be_order_route_schedule_or_ledger",
    "Export_operator_report_writes_only_allowed_artifact_path",
    "Review_rejects_paths_outside_allowed_artifacts",
    "Legacy_06_is_not_accepted_as_future_canonical_in_reviewed_preview",
    "Usdjpy_caveat_remains_preserved_in_reviewed_preview"
)) {
    if ($tests -notmatch [regex]::Escape($needle)) { Fail "Focused test missing $needle" }
}

$reviewContract = Read-Json "phase-exec-live-r007-operator-review-contract.json"
foreach ($forbiddenSupport in @("ApproveForExecution", "SubmitOrder", "CreateRoute", "CreateSchedule", "CommitLedger", "ActivateBroker", "TriggerLiveMarketData", "StartSchedulerWorker")) {
    if (@($reviewContract.DoesNotSupport) -notcontains $forbiddenSupport) { Fail "Operator review contract missing forbidden support $forbiddenSupport" }
}
if ($reviewContract.OperatorApprovalImpliesExecutableApproval -ne $false) { Fail "Operator approval implies executable approval" }

$cli = Read-Json "phase-exec-live-r007-operator-cli-contract.json"
foreach ($mode in @("ListAuditRecords", "ShowAuditRecord", "SummarizeBatch", "ExportOperatorReport")) {
    if (@($cli.AllowedCommandModes) -notcontains $mode) { Fail "Allowed command mode missing $mode" }
}
foreach ($mode in @("Execute", "Submit", "Route", "CommitLedger", "ActivateBroker", "StartScheduler", "PromoteLive")) {
    if (@($cli.ForbiddenCommandModes) -notcontains $mode) { Fail "Forbidden command mode missing $mode" }
    if (@($cli.AllowedCommandModes) -contains $mode) { Fail "Forbidden command mode allowed $mode" }
}
if ($cli.BrokerLiveOrderRouteScheduleLedgerStatePathsEnabled -ne $false) { Fail "CLI/reporting contract enables forbidden paths" }

$responseContract = Read-Json "phase-exec-live-r007-operator-review-response-dto-contract.json"
foreach ($flag in @("NonExecutable=true", "NotAnOrder=true", "NotSubmitted=true", "NoBrokerRoute=true", "NoFill=true", "NoExecutionReport=true", "NoRoute=true", "NoSubmission=true", "NoPaperLedgerCommit=true", "ReviewOnly=true", "ExecutableApproval=false", "BrokerApproval=false", "LiveApproval=false")) {
    if (@($responseContract.RequiredFlags) -notcontains $flag) { Fail "Response contract missing $flag" }
}

$list = Read-Json "phase-exec-live-r007-sample-list-audit-response.json"
if ($list.SampleOnly -ne $true) { Fail "Sample list response is not marked SampleOnly" }
if ($list.NonExecutable -ne $true -or $list.NotAnOrder -ne $true -or $list.NoBrokerRoute -ne $true -or $list.ReviewOnly -ne $true) { Fail "Sample list response safety flags weakened" }
if ($list.ExecutableApproval -ne $false -or $list.BrokerApproval -ne $false -or $list.LiveApproval -ne $false) { Fail "Sample list response grants approval" }

$batch = Read-Json "phase-exec-live-r007-sample-batch-summary.json"
if ($batch.PreviewReadyCount -ne 1 -or $batch.HeldMissingReadinessCount -ne 1 -or $batch.RejectedCount -ne 1) { Fail "Sample batch summary counts unexpected" }
if ($batch.NonExecutable -ne $true -or $batch.NotAnOrder -ne $true -or $batch.NoBrokerRoute -ne $true) { Fail "Sample batch summary safety flags weakened" }

$held = Read-Json "phase-exec-live-r007-sample-held-line-summary.json"
if ($held.HeldNotOrder -ne $true -or $held.ExecutableApproval -ne $false) { Fail "Held-line summary can become executable/order" }

$invalid = Read-Json "phase-exec-live-r007-invalid-review-command-rejection-results.json"
$rejections = @($invalid.Results | ForEach-Object { $_.RejectionReason })
foreach ($expected in @("ForbiddenCommandMode:Execute", "ForbiddenCommandMode:Submit", "ForbiddenCommandMode:Route", "ForbiddenCommandMode:CommitLedger", "ForbiddenCommandMode:StartScheduler", "ForbiddenConsumer:BrokerGateway")) {
    if ($rejections -notcontains $expected) { Fail "Invalid command rejection missing $expected" }
}
foreach ($result in @($invalid.Results)) {
    if ($result.Accepted -ne $false) { Fail "Invalid review command accepted" }
}

$order = Read-Json "phase-exec-live-r007-review-output-not-order-audit.json"
if ($order.ReviewCanApproveExecution -ne $false -or $order.CanConvertToOrder -ne $false -or $order.CanConvertToChildOrder -ne $false -or $order.NotAnOrder -ne $true) { Fail "Review output can become order or approve execution" }
$route = Read-Json "phase-exec-live-r007-review-output-not-route-audit.json"
if ($route.CanConvertToRoute -ne $false -or $route.CanConvertToSubmission -ne $false -or $route.NoBrokerRoute -ne $true) { Fail "Review output can become route/submission" }
$schedule = Read-Json "phase-exec-live-r007-review-output-not-schedule-audit.json"
if ($schedule.CanCreateExecutableSchedule -ne $false) { Fail "Review output can create schedule" }
$ledger = Read-Json "phase-exec-live-r007-review-output-not-ledger-audit.json"
if ($ledger.CanCommitPaperLedger -ne $false -or $ledger.NoTradingStateMutation -ne $true) { Fail "Review output can commit ledger or mutate state" }

$pathSafety = Read-Json "phase-exec-live-r007-artifact-path-safety-review.json"
if ($pathSafety.ReadRoot -ne "artifacts/readiness/execution-live/audit" -or $pathSafety.WriteRoot -ne "artifacts/readiness/execution-live/operator-review") { Fail "Artifact path safety roots missing" }
if ($pathSafety.ReadsOutsideArtifactPath -ne $false -or $pathSafety.WritesOutsideArtifactPath -ne $false -or $pathSafety.RequiresDb -ne $false -or $pathSafety.RequiresExternalService -ne $false) { Fail "Review path safety weakened" }

$flags = Read-Json "phase-exec-live-r007-kill-switch-feature-flag-review.json"
if ($flags.R009LiveTradingEnabled -ne $false -or
    $flags.R009BrokerRoutingEnabled -ne $false -or
    $flags.R009OrderSubmissionEnabled -ne $false -or
    $flags.R009ExecutableScheduleEnabled -ne $false -or
    $flags.R009PaperLedgerCommitEnabled -ne $false -or
    $flags.R009SchedulerEnabled -ne $false -or
    $flags.R009BackgroundWorkerEnabled -ne $false -or
    $flags.R009DryRunOnly -ne $true) { Fail "Kill-switch flags weakened" }

$disabled = Read-Json "phase-exec-live-r007-disabled-boundary-guard-review.json"
foreach ($property in @("BrokerRouteCreationAllowed", "OrderCreationAllowed", "ChildSliceCreationAllowed", "ChildOrderCreationAllowed", "ScheduleExecutionAllowed", "SubmissionAllowed", "FillCreationAllowed", "ExecutionReportCreationAllowed", "StateMutationAllowed", "PaperLedgerCommitAllowed")) {
    if ($disabled.$property -ne $false) { Fail "Disabled boundary guard weakened: $property" }
}

$legacy = Read-Json "phase-exec-live-r007-legacy-compatibility-preservation.json"
if ($legacy.UsedAsFutureCanonical -ne $false) { Fail "Legacy :06 used as future canonical" }
$direct = Read-Json "phase-exec-live-r007-direct-cross-exclusion-preservation.json"
if ($direct.DirectCrossExecutionAllowed -ne $false -or $direct.DirectCrossRejectedNotOrder -ne $true) { Fail "Direct-cross exclusion weakened" }
$cost = Read-Json "phase-exec-live-r007-cost-guidance-preservation.json"
if ($cost.Universalized -ne $false) { Fail "5 USD/million universalized" }
$usdPair = Read-Json "phase-exec-live-r007-usd-pair-netting-requirement.json"
if ($usdPair.AudusdStatus -ne "SupportedAndNotFailed") { Fail "AUDUSD misclassified" }
$usdjpy = Read-Json "phase-exec-live-r007-usdjpy-caveat-preservation.json"
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or $usdjpy.RequiresInversion -ne $true -or $usdjpy.SecurityID -ne "4004" -or $usdjpy.SecurityIDSource -ne "8") {
    Fail "USDJPY caveat weakened"
}

$forbidden = Read-Json "phase-exec-live-r007-forbidden-actions-audit.json"
foreach ($property in @(
    "ExternalApiCallsMade",
    "PolygonCallsMade",
    "LmaxCallsMade",
    "BrokerActivationOccurred",
    "LiveMarketDataRequested",
    "SchedulerServicePollingBackgroundJobIntroduced",
    "PmsEmsOmsCycleRun",
    "ManualNoExternalCommandRun",
    "BacktestSimulationRun",
    "TcaResultLinesCreated",
    "ExecutableScheduleCreated",
    "OrdersChildOrdersRoutesSubmissionsFillsReportsCreated",
    "PaperLedgerCommitCreated",
    "StateMutationOccurred",
    "R009PromotedToExecutableUse",
    "OperatorReviewCanApproveExecution"
)) {
    if ($forbidden.$property -ne $false) { Fail "Forbidden action observed: $property" }
}

$evidence = Read-Json "phase-exec-live-r007-build-test-validator-evidence.json"
if ($evidence.Build -ne "Passed") { Fail "Build evidence missing or not passed" }
if ($evidence.FocusedR007Tests -ne "Passed") { Fail "Focused R007 test evidence missing or not passed" }
if ($evidence.UnitTests -ne "Passed") { Fail "Unit test evidence missing or not passed" }
if ($evidence.Validator -ne "Passed") { Fail "Validator evidence missing or not passed" }

Write-Host "EXEC-LIVE-R007 validator passed."
