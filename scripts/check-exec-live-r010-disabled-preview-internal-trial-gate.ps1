$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-live"

function Fail {
    param([string]$Message)
    throw "EXEC-LIVE-R010 validator failed: $Message"
}

function Read-Json {
    param([string]$Name)
    $path = Join-Path $artifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { Fail "Missing artifact $Name" }
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$requiredArtifacts = @(
    "phase-exec-live-r010-summary.md",
    "phase-exec-live-r010-r009-readiness-reference.json",
    "phase-exec-live-r010-r009-contract-reference.json",
    "phase-exec-live-r010-trial-input-selection.json",
    "phase-exec-live-r010-internal-trial-contract.json",
    "phase-exec-live-r010-consumer-boundary-trial-requests.json",
    "phase-exec-live-r010-forbidden-consumer-rejection-results.json",
    "phase-exec-live-r010-disabled-preview-trial-results.json",
    "phase-exec-live-r010-batch-preview-trial-results.json",
    "phase-exec-live-r010-held-readiness-trial-results.json",
    "phase-exec-live-r010-rejected-input-trial-results.json",
    "phase-exec-live-r010-preview-audit-records-created.json",
    "phase-exec-live-r010-operator-review-reports-created.json",
    "phase-exec-live-r010-trial-coverage-summary.json",
    "phase-exec-live-r010-per-symbol-trial-review.json",
    "phase-exec-live-r010-bar-role-trial-review.json",
    "phase-exec-live-r010-direct-cross-rejection-review.json",
    "phase-exec-live-r010-usdjpy-caveat-review.json",
    "phase-exec-live-r010-legacy-target-close-rejection-review.json",
    "phase-exec-live-r010-kill-switch-feature-flag-review.json",
    "phase-exec-live-r010-disabled-boundary-guard-review.json",
    "phase-exec-live-r010-internal-trial-decision.json",
    "phase-exec-live-r010-executable-promotion-blockers.json",
    "phase-exec-live-r010-no-broker-activation-audit.json",
    "phase-exec-live-r010-no-live-marketdata-audit.json",
    "phase-exec-live-r010-no-scheduler-service-polling-audit.json",
    "phase-exec-live-r010-no-order-created-audit.json",
    "phase-exec-live-r010-no-child-order-audit.json",
    "phase-exec-live-r010-no-executable-schedule-audit.json",
    "phase-exec-live-r010-no-route-no-submission-audit.json",
    "phase-exec-live-r010-no-fill-execution-report-audit.json",
    "phase-exec-live-r010-no-paper-ledger-commit-audit.json",
    "phase-exec-live-r010-no-state-mutation-audit.json",
    "phase-exec-live-r010-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-live-r010-legacy-compatibility-preservation.json",
    "phase-exec-live-r010-direct-cross-exclusion-preservation.json",
    "phase-exec-live-r010-usd-pair-netting-requirement.json",
    "phase-exec-live-r010-usdjpy-caveat-preservation.json",
    "phase-exec-live-r010-cost-guidance-preservation.json",
    "phase-exec-live-r010-nonmajor-calibration-preservation.json",
    "phase-exec-live-r010-no-external-audit.json",
    "phase-exec-live-r010-forbidden-actions-audit.json",
    "phase-exec-live-r010-next-phase-recommendation.json",
    "phase-exec-live-r010-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $artifact))) {
        Fail "Required artifact missing: $artifact"
    }
}

$testPath = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/R009InternalDisabledPreviewTrialTests.cs"
if (-not (Test-Path -LiteralPath $testPath)) { Fail "Focused R010 tests missing" }
$testSource = Get-Content -LiteralPath $testPath -Raw
foreach ($required in @(
    "R009InternalDisabledPreviewTrialTests",
    "R009PreviewConsumerBoundaryService",
    "R009PreviewArtifactAuditWriter",
    "R009OperatorPreviewReviewService",
    "Forbidden_consumers_are_rejected",
    "DirectCrossExecutionIntentRejected",
    "CanonicalQuarterHourTargetCloseRequired",
    "securityIdSource: `"8`""
)) {
    if ($testSource -notmatch [regex]::Escape($required)) { Fail "Focused R010 tests missing assertion/source token: $required" }
}

$readiness = Read-Json "phase-exec-live-r010-r009-readiness-reference.json"
if ($readiness.TrialOnly -ne $true -or $readiness.ExecutableApproval -ne $false -or $readiness.BrokerReady -ne $false -or $readiness.LiveReady -ne $false) {
    Fail "R009 readiness reference implies executable/broker/live approval"
}

$contract = Read-Json "phase-exec-live-r010-r009-contract-reference.json"
if ($contract.NonExecutable -ne $true -or $contract.NotAnOrder -ne $true -or $contract.NoBrokerRoute -ne $true) { Fail "R009 contract weakens non-executable status" }
if ($contract.BrokerReady -ne $false -or $contract.LiveReady -ne $false -or $contract.ExecutablePromotionAuthorized -ne $false) { Fail "R009 promoted or live/broker ready" }

$input = Read-Json "phase-exec-live-r010-trial-input-selection.json"
if ($input.SourceArtifactExists -ne $true) { Fail "Selected source paper-plan input artifact missing" }
if ($input.SourceIntentCount -lt 210) { Fail "Selected balanced source does not expose expected R002 intent count" }
if ($input.PmsCyclesRun -ne $false -or $input.ManualNoExternalCommandsRun -ne $false -or $input.InputLinesInvented -ne $false) { Fail "Input selection ran forbidden cycles or invented lines" }

$trialContract = Read-Json "phase-exec-live-r010-internal-trial-contract.json"
if ($trialContract.MarketExecution -ne $false -or $trialContract.ExecutableApproval -ne $false) { Fail "Trial contract implies market execution" }
foreach ($output in @("Orders", "ChildOrders", "Routes", "Submissions", "Fills", "ExecutionReports", "ExecutableSchedules", "LedgerCommits", "TradingStateMutations")) {
    if (@($trialContract.ForbiddenOutputs) -notcontains $output) { Fail "Trial contract missing forbidden output $output" }
}
foreach ($consumer in @("BrokerGateway", "OrderRouter", "Scheduler", "PaperLedgerCommitter", "ExecutionReportHandler", "ProductionTradingRuntime")) {
    if (@($trialContract.ForbiddenConsumers) -notcontains $consumer) { Fail "Trial contract missing forbidden consumer $consumer" }
    if (@($trialContract.AllowedConsumers) -contains $consumer) { Fail "Trial contract allows forbidden consumer $consumer" }
}

$requests = Read-Json "phase-exec-live-r010-consumer-boundary-trial-requests.json"
if ($requests.TotalRequests -ne 11 -or $requests.BatchRequests -ne 1 -or $requests.AcceptedRequests -ne 5 -or $requests.RejectedRequests -ne 6) { Fail "Consumer boundary request counts do not match R010 trial" }
if ($requests.NonExecutable -ne $true -or $requests.NotAnOrder -ne $true -or $requests.NoBrokerRoute -ne $true) { Fail "Consumer boundary requests weaken preview-only flags" }

$forbiddenConsumers = Read-Json "phase-exec-live-r010-forbidden-consumer-rejection-results.json"
if ($forbiddenConsumers.ForbiddenConsumersAllowed -ne $false) { Fail "Forbidden consumer allowed" }
foreach ($consumer in @("BrokerGateway", "OrderRouter", "Scheduler", "PaperLedgerCommitter", "ExecutionReportHandler", "ProductionTradingRuntime")) {
    $result = @($forbiddenConsumers.Results) | Where-Object { $_.ConsumerType -eq $consumer }
    if (-not $result -or $result.Accepted -ne $false -or $result.PersistedAsValidAudit -ne $false) { Fail "Forbidden consumer not rejected safely: $consumer" }
}

$single = Read-Json "phase-exec-live-r010-disabled-preview-trial-results.json"
if ($single.SinglePreviewResponses -ne 4 -or $single.SinglePreviewReady -ne 4) { Fail "Single preview trial counts unexpected" }
foreach ($flag in @("NonExecutable", "NotAnOrder", "NotSubmitted", "NoBrokerRoute", "NoFill", "NoExecutionReport", "NoRoute", "NoSubmission", "NoPaperLedgerCommit")) {
    if ($single.$flag -ne $true) { Fail "Single preview result missing safety flag $flag" }
}

$batch = Read-Json "phase-exec-live-r010-batch-preview-trial-results.json"
if ($batch.ItemCount -ne 4 -or $batch.PreviewReadyCount -ne 1 -or $batch.HeldMissingReadinessCount -ne 1 -or $batch.RejectedCount -ne 2) { Fail "Batch preview trial counts unexpected" }
if ($batch.NonExecutable -ne $true -or $batch.NotAnOrder -ne $true -or $batch.NoBrokerRoute -ne $true) { Fail "Batch preview result weakens safety flags" }
if (-not (@($batch.ItemResults) | Where-Object { $_.ItemId -eq "rejected-direct-cross" -and $_.Status -eq "Rejected" -and $_.RejectionReason -eq "DirectCrossExecutionIntentRejected" })) { Fail "Direct-cross item not rejected" }
if (-not (@($batch.ItemResults) | Where-Object { $_.ItemId -eq "rejected-legacy-06" -and $_.Status -eq "Rejected" -and $_.RejectionReason -eq "CanonicalQuarterHourTargetCloseRequired" })) { Fail "Legacy :06 item not rejected" }

$held = Read-Json "phase-exec-live-r010-held-readiness-trial-results.json"
if ($held.HeldDecisionCount -ne 1 -or $held.HeldProducesOrder -ne $false -or $held.HeldProducesRoute -ne $false -or $held.HeldProducesExecutableSchedule -ne $false -or $held.HeldProducesLedgerCommit -ne $false) { Fail "Held readiness did not remain preview-only" }

$rejected = Read-Json "phase-exec-live-r010-rejected-input-trial-results.json"
if ($rejected.RejectedDecisionCount -ne 2 -or $rejected.RejectedProducesOrder -ne $false -or $rejected.RejectedProducesRoute -ne $false -or $rejected.RejectedProducesExecutableSchedule -ne $false) { Fail "Rejected input outputs unsafe" }

$audit = Read-Json "phase-exec-live-r010-preview-audit-records-created.json"
if ($audit.AuditRecordsCreated -ne 5 -or $audit.ArtifactOnly -ne $true -or $audit.DbWrites -ne $false -or $audit.OrderDomainPersistence -ne $false -or $audit.RouteSubmissionPersistence -ne $false -or $audit.LedgerPersistence -ne $false -or $audit.TradingStateMutation -ne $false) { Fail "Preview audit records are missing or unsafe" }

$operator = Read-Json "phase-exec-live-r010-operator-review-reports-created.json"
if ($operator.OperatorReportsCreated -ne 1 -or $operator.WritesOutsideArtifactPath -ne $false -or $operator.ExecutableApproval -ne $false) { Fail "Operator report creation unsafe" }
$operatorReportPath = Join-Path $repoRoot "artifacts/readiness/execution-live/operator-review/phase-exec-live-r010-operator-trial-review.md"
if (-not (Test-Path -LiteralPath $operatorReportPath)) { Fail "Operator trial review markdown missing" }

$coverage = Read-Json "phase-exec-live-r010-trial-coverage-summary.json"
if ($coverage.TotalRequests -ne 11 -or $coverage.PreviewReadyDecisions -ne 5 -or $coverage.HeldDecisions -ne 1 -or $coverage.RejectedDecisions -ne 2) { Fail "Coverage summary counts unexpected" }
if ($coverage.DisabledFlagsRemainFalse -ne $true -or $coverage.NoBrokerOrderRouteFillScheduleLedgerPath -ne $true) { Fail "Coverage summary weakens disabled path review" }

$direct = Read-Json "phase-exec-live-r010-direct-cross-rejection-review.json"
if ($direct.DirectCrossExecutionAllowed -ne $false -or $direct.DirectCrossExecutionIntentRejected -ne $true -or $direct.CreatesOrder -ne $false -or $direct.CreatesRoute -ne $false) { Fail "Direct-cross rejection review weakened" }

$legacy = Read-Json "phase-exec-live-r010-legacy-target-close-rejection-review.json"
if ($legacy.AcceptedAsFutureCanonical -ne $false) { Fail "Legacy :06 accepted as future canonical" }

$flags = Read-Json "phase-exec-live-r010-kill-switch-feature-flag-review.json"
if ($flags.LiveTradingEnabled -ne $false -or
    $flags.BrokerRoutingEnabled -ne $false -or
    $flags.OrderSubmissionEnabled -ne $false -or
    $flags.ExecutableScheduleEnabled -ne $false -or
    $flags.PaperLedgerCommitEnabled -ne $false -or
    $flags.SchedulerEnabled -ne $false -or
    $flags.BackgroundWorkerEnabled -ne $false -or
    $flags.DryRunOnly -ne $true) { Fail "Kill-switch flags weakened" }

$guard = Read-Json "phase-exec-live-r010-disabled-boundary-guard-review.json"
foreach ($property in @("BrokerRouteCreationAllowed", "OrderCreationAllowed", "ChildSliceCreationAllowed", "ChildOrderCreationAllowed", "ScheduleExecutionAllowed", "SubmissionAllowed", "FillCreationAllowed", "ExecutionReportCreationAllowed", "StateMutationAllowed", "PaperLedgerCommitAllowed")) {
    if ($guard.$property -ne $false) { Fail "Disabled boundary guard allows $property" }
}

$decision = Read-Json "phase-exec-live-r010-internal-trial-decision.json"
if ($decision.Decision -ne "DisabledPreviewTrialPassedWithHeldReadiness" -or $decision.TrialPassed -ne $true) { Fail "Trial decision not passed with held readiness" }
if ($decision.ExecutableApproval -ne $false -or $decision.BrokerApproval -ne $false -or $decision.LiveApproval -ne $false -or $decision.PaperLedgerCommitApproval -ne $false -or $decision.SeparateExplicitExecutableGateRequired -ne $true) { Fail "Trial decision implies executable/broker/live/ledger approval" }

$blockers = Read-Json "phase-exec-live-r010-executable-promotion-blockers.json"
if ($blockers.ExecutablePromotionBlocked -ne $true) { Fail "Executable promotion not blocked" }
foreach ($blocker in @("No broker integration authorized", "No live market data authorized", "No scheduler/service/polling authorized", "No order-domain creation authorized", "No route/submission/fill/execution-report path authorized", "No executable schedule authorized", "No paper ledger commit authorized", "No trading state mutation authorized", "Separate explicit executable gate required")) {
    if (@($blockers.Blockers) -notcontains $blocker) { Fail "Executable blocker missing $blocker" }
}

$symbolReview = Read-Json "phase-exec-live-r010-per-symbol-trial-review.json"
if ($symbolReview.DirectCrossExecutableLines -ne 0 -or $symbolReview.NonmajorEmScandiCnhAllowed -ne $false) { Fail "Symbol review allows direct-cross/nonmajor execution" }
if (-not (@($symbolReview.SymbolCoverage) | Where-Object { $_.Symbol -eq "AUDUSD" -and $_.AudusdStatus -eq "SupportedAndNotFailed" })) { Fail "AUDUSD misclassified" }

$usdjpy = Read-Json "phase-exec-live-r010-usdjpy-caveat-preservation.json"
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or $usdjpy.RequiresInversion -ne $true -or $usdjpy.SecurityID -ne "4004" -or $usdjpy.SecurityIDSource -ne "8") {
    Fail "USDJPY caveat weakened"
}
$cost = Read-Json "phase-exec-live-r010-cost-guidance-preservation.json"
if ($cost.Universalized -ne $false) { Fail "5 USD/million universalized" }
$nonmajor = Read-Json "phase-exec-live-r010-nonmajor-calibration-preservation.json"
if ($nonmajor.LiveCapableExecutionAllowed -ne $false) { Fail "Nonmajor/EM/scandi/CNH execution allowed" }

$forbidden = Read-Json "phase-exec-live-r010-forbidden-actions-audit.json"
foreach ($property in @(
    "ExternalApiCallsMade",
    "PolygonCallsMade",
    "LmaxCallsMade",
    "BrokerActivationOccurred",
    "LiveMarketDataRequested",
    "SchedulerServicePollingBackgroundJobIntroduced",
    "PmsEmsOmsProductionCycleRun",
    "ManualNoExternalCommandRun",
    "BacktestSimulationRun",
    "TcaResultLinesCreated",
    "ExecutableScheduleCreated",
    "OrdersChildOrdersRoutesSubmissionsFillsReportsCreated",
    "PaperLedgerCommitCreated",
    "StateMutationOccurred",
    "R009PromotedToExecutableUse",
    "TrialDecisionImpliesExecutableApproval",
    "BrokerLiveOrderRouteScheduleLedgerPathEnabled",
    "ForbiddenConsumerAllowed",
    "DirectCrossExecutionAllowed",
    "Legacy06AcceptedAsFutureCanonical",
    "PreviewOutputRepresentedAsOrderRouteFillSchedule"
)) {
    if ($forbidden.$property -ne $false) { Fail "Forbidden action observed or allowed: $property" }
}

$evidence = Read-Json "phase-exec-live-r010-build-test-validator-evidence.json"
if ($evidence.Build -ne "Passed") { Fail "Build evidence missing or not passed" }
if ($evidence.FocusedR010Tests -ne "Passed") { Fail "Focused R010 test evidence missing or not passed" }
if ($evidence.UnitTests -ne "Passed") { Fail "Unit test evidence missing or not passed" }
if ($evidence.Validator -ne "Passed") { Fail "Validator evidence missing or not passed" }

Write-Host "EXEC-LIVE-R010 validator passed."
