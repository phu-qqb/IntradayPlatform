$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-live"

function Fail {
    param([string]$Message)
    throw "EXEC-LIVE-R009 validator failed: $Message"
}

function Read-Json {
    param([string]$Name)
    $path = Join-Path $artifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { Fail "Missing artifact $Name" }
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$requiredArtifacts = @(
    "phase-exec-live-r009-summary.md",
    "phase-exec-live-r009-r001-r008-readiness-reference.json",
    "phase-exec-live-r009-r009-contract-reference.json",
    "phase-exec-live-r009-internal-trial-readiness-contract.json",
    "phase-exec-live-r009-internal-trial-scope.json",
    "phase-exec-live-r009-internal-trial-prerequisites.json",
    "phase-exec-live-r009-go-no-go-criteria.json",
    "phase-exec-live-r009-readiness-assessment.json",
    "phase-exec-live-r009-blocker-list.json",
    "phase-exec-live-r009-safety-flag-review.json",
    "phase-exec-live-r009-consumer-boundary-review.json",
    "phase-exec-live-r009-audit-path-review.json",
    "phase-exec-live-r009-operator-review-path-review.json",
    "phase-exec-live-r009-rollback-disable-readiness.json",
    "phase-exec-live-r009-no-broker-activation-audit.json",
    "phase-exec-live-r009-no-live-marketdata-audit.json",
    "phase-exec-live-r009-no-scheduler-service-polling-audit.json",
    "phase-exec-live-r009-no-order-created-audit.json",
    "phase-exec-live-r009-no-child-order-audit.json",
    "phase-exec-live-r009-no-executable-schedule-audit.json",
    "phase-exec-live-r009-no-route-no-submission-audit.json",
    "phase-exec-live-r009-no-fill-execution-report-audit.json",
    "phase-exec-live-r009-no-paper-ledger-commit-audit.json",
    "phase-exec-live-r009-no-state-mutation-audit.json",
    "phase-exec-live-r009-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-live-r009-legacy-compatibility-preservation.json",
    "phase-exec-live-r009-direct-cross-exclusion-preservation.json",
    "phase-exec-live-r009-usd-pair-netting-requirement.json",
    "phase-exec-live-r009-usdjpy-caveat-preservation.json",
    "phase-exec-live-r009-cost-guidance-preservation.json",
    "phase-exec-live-r009-nonmajor-calibration-preservation.json",
    "phase-exec-live-r009-no-external-audit.json",
    "phase-exec-live-r009-forbidden-actions-audit.json",
    "phase-exec-live-r009-next-phase-recommendation.json",
    "phase-exec-live-r009-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $artifact))) {
        Fail "Required artifact missing: $artifact"
    }
}

$reference = Read-Json "phase-exec-live-r009-r001-r008-readiness-reference.json"
if ($reference.AllReferencedArtifactsPresent -ne $true) { Fail "R001-R008 references not marked present" }
foreach ($phase in @("EXEC-LIVE-R001", "EXEC-LIVE-R002", "EXEC-LIVE-R003", "EXEC-LIVE-R004", "EXEC-LIVE-R005", "EXEC-LIVE-R006", "EXEC-LIVE-R007", "EXEC-LIVE-R008")) {
    if (-not (@($reference.References) | Where-Object { $_.Phase -eq $phase })) { Fail "Missing reference to $phase" }
}

$contract = Read-Json "phase-exec-live-r009-internal-trial-readiness-contract.json"
if ($contract.TrialType -ne "Internal EMS/OMS disabled preview only") { Fail "Trial type is not disabled-preview only" }
if ($contract.TrialExecutionAuthorized -ne $false -or $contract.ExecutableApproval -ne $false -or $contract.BrokerApproval -ne $false -or $contract.LiveApproval -ne $false) { Fail "Readiness contract implies executable/broker/live approval" }
foreach ($output in @("Orders", "ChildOrders", "Routes", "Submissions", "Fills", "ExecutionReports", "ExecutableSchedules", "LedgerCommits", "TradingStateMutations")) {
    if (@($contract.ForbiddenOutputs) -notcontains $output) { Fail "Forbidden trial output missing $output" }
}

$scope = Read-Json "phase-exec-live-r009-internal-trial-scope.json"
foreach ($output in @("Orders", "ChildOrders", "Routes", "Submissions", "Fills", "ExecutionReports", "ExecutableSchedules", "LedgerCommits", "TradingStateMutations")) {
    if (@($scope.ForbiddenOutputs) -notcontains $output) { Fail "Scope missing forbidden output $output" }
}
if ($scope.NonExecutable -ne $true -or $scope.NotAnOrder -ne $true -or $scope.NoBrokerRoute -ne $true -or $scope.NoPaperLedgerCommit -ne $true -or $scope.NoTradingStateMutation -ne $true) { Fail "Trial scope weakens safety flags" }

$prereq = Read-Json "phase-exec-live-r009-internal-trial-prerequisites.json"
foreach ($property in @("RunbookAvailable", "RollbackDisablePlanAvailable", "OperatorChecklistAvailable", "AuditPathConfigured", "OperatorReviewPathConfigured", "PrerequisitesSatisfied")) {
    if ($prereq.$property -ne $true) { Fail "Prerequisite missing $property" }
}

$criteria = Read-Json "phase-exec-live-r009-go-no-go-criteria.json"
foreach ($go in @("All disabled preview contracts present", "Batch API present", "Consumer boundary present", "Artifact-only audit trail present", "Operator review surface present", "Runbook and rollback plan present", "Tests and validators pass", "No executable path detected")) {
    if (@($criteria.GoCriteria) -notcontains $go) { Fail "Go criterion missing $go" }
}
foreach ($noGo in @("Any broker/live/scheduler/order/route/fill/ledger path", "Any executable schedule path", "Any forbidden consumer allowed", "Any preview output convertible to order/route/fill/schedule", "Legacy :06 accepted as canonical", "Direct-cross execution accepted", "USDJPY caveat weakened", "Kill-switch defaults enabled")) {
    if (@($criteria.NoGoCriteria) -notcontains $noGo) { Fail "No-go criterion missing $noGo" }
}
if ($criteria.GoForInternalDisabledPreviewTrialReadiness -ne $true -or $criteria.GoForExecutableUse -ne $false -or $criteria.SeparateFutureGateRequiredForAnyExecution -ne $true) { Fail "Go/no-go criteria imply executable use" }

$assessment = Read-Json "phase-exec-live-r009-readiness-assessment.json"
if ($assessment.Readiness -ne "ReadyForInternalDisabledPreviewTrial") { Fail "Assessment not ready for internal disabled preview trial" }
if ($assessment.ExecutableReadiness -ne $false -or $assessment.BrokerReadiness -ne $false -or $assessment.LiveReadiness -ne $false) { Fail "Assessment implies executable/broker/live readiness" }
foreach ($property in @("AllSafetyFlagsDisabled", "AllowedForbiddenConsumersReviewed", "AuditAndOperatorReviewPathsReady", "RollbackDisablePlanReady", "NoExecutablePathEnabled")) {
    if ($assessment.$property -ne $true) { Fail "Assessment missing $property" }
}

$blockers = Read-Json "phase-exec-live-r009-blocker-list.json"
if (@($blockers.TrialReadinessBlockers).Count -ne 0) { Fail "Trial readiness blockers remain" }
foreach ($blocker in @("No broker integration authorized", "No live market data authorized", "No scheduler/service/polling authorized", "No order-domain creation authorized", "No route/submission/fill/execution-report path authorized", "No executable schedule authorized", "No paper ledger commit authorized", "No trading state mutation authorized", "Separate explicit executable gate required")) {
    if (@($blockers.ExecutableUseBlockers) -notcontains $blocker) { Fail "Executable blocker missing $blocker" }
}

$flags = Read-Json "phase-exec-live-r009-safety-flag-review.json"
if ($flags.LiveTradingEnabled -ne $false -or
    $flags.BrokerRoutingEnabled -ne $false -or
    $flags.OrderSubmissionEnabled -ne $false -or
    $flags.ExecutableScheduleEnabled -ne $false -or
    $flags.PaperLedgerCommitEnabled -ne $false -or
    $flags.SchedulerEnabled -ne $false -or
    $flags.BackgroundWorkerEnabled -ne $false -or
    $flags.DryRunOnly -ne $true) { Fail "Safety flags weakened" }

$consumer = Read-Json "phase-exec-live-r009-consumer-boundary-review.json"
foreach ($forbiddenConsumer in @("BrokerGateway", "OrderRouter", "Scheduler", "PaperLedgerCommitter", "ExecutionReportHandler", "ProductionTradingRuntime")) {
    if (@($consumer.ForbiddenConsumers) -notcontains $forbiddenConsumer) { Fail "Forbidden consumer missing $forbiddenConsumer" }
    if (@($consumer.AllowedConsumers) -contains $forbiddenConsumer) { Fail "Forbidden consumer allowed $forbiddenConsumer" }
}
if ($consumer.BrokerGatewayAllowed -ne $false -or $consumer.OrderRouterAllowed -ne $false -or $consumer.SchedulerAllowed -ne $false -or $consumer.PaperLedgerCommitterAllowed -ne $false -or $consumer.ProductionTradingRuntimeAllowed -ne $false) { Fail "Forbidden consumer review weakened" }

$auditPath = Read-Json "phase-exec-live-r009-audit-path-review.json"
if ($auditPath.AuditPath -ne "artifacts/readiness/execution-live/audit" -or $auditPath.ArtifactOnly -ne $true -or $auditPath.DbRequired -ne $false -or $auditPath.ExternalServiceRequired -ne $false) { Fail "Audit path missing or unsafe" }
if ($auditPath.OrderDomainPersistenceAllowed -ne $false -or $auditPath.RouteSubmissionPersistenceAllowed -ne $false -or $auditPath.LedgerPersistenceAllowed -ne $false -or $auditPath.TradingStateMutationAllowed -ne $false) { Fail "Audit path permits forbidden persistence" }

$operatorPath = Read-Json "phase-exec-live-r009-operator-review-path-review.json"
if ($operatorPath.OperatorReviewPath -ne "artifacts/readiness/execution-live/operator-review" -or $operatorPath.ReviewOnly -ne $true -or $operatorPath.ExecutableApproval -ne $false -or $operatorPath.BrokerApproval -ne $false -or $operatorPath.LiveApproval -ne $false) { Fail "Operator review path missing or unsafe" }

$rollback = Read-Json "phase-exec-live-r009-rollback-disable-readiness.json"
foreach ($property in @("RunbookAvailable", "RollbackDisablePlanAvailable", "OperatorChecklistAvailable", "IncidentStopRulesAvailable", "AuditArtifactsPreservedOnRollback", "ConsumerAccessCanBeDisabled", "FeatureFlagsRemainFalse")) {
    if ($rollback.$property -ne $true) { Fail "Rollback/operator readiness missing $property" }
}

$r009 = Read-Json "phase-exec-live-r009-r009-contract-reference.json"
if ($r009.NonExecutable -ne $true -or $r009.NotAnOrder -ne $true -or $r009.NoBrokerRoute -ne $true) { Fail "R009 contract weakens non-executable status" }
if ($r009.BrokerReady -ne $false -or $r009.LiveReady -ne $false -or $r009.ExecutablePromotionAuthorized -ne $false) { Fail "R009 promoted or live/broker ready" }

$legacy = Read-Json "phase-exec-live-r009-legacy-compatibility-preservation.json"
if ($legacy.UsedAsFutureCanonical -ne $false) { Fail "Legacy :06 used as future canonical" }
$direct = Read-Json "phase-exec-live-r009-direct-cross-exclusion-preservation.json"
if ($direct.DirectCrossExecutionAllowed -ne $false -or $direct.DirectCrossExecutionIntentRejected -ne $true) { Fail "Direct-cross exclusion weakened" }
$cost = Read-Json "phase-exec-live-r009-cost-guidance-preservation.json"
if ($cost.Universalized -ne $false) { Fail "5 USD/million universalized" }
$usdPair = Read-Json "phase-exec-live-r009-usd-pair-netting-requirement.json"
if ($usdPair.AudusdStatus -ne "SupportedAndNotFailed") { Fail "AUDUSD misclassified" }
$usdjpy = Read-Json "phase-exec-live-r009-usdjpy-caveat-preservation.json"
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or $usdjpy.RequiresInversion -ne $true -or $usdjpy.SecurityID -ne "4004" -or $usdjpy.SecurityIDSource -ne "8") {
    Fail "USDJPY caveat weakened"
}

$forbidden = Read-Json "phase-exec-live-r009-forbidden-actions-audit.json"
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
    "InternalTrialReadinessImpliesExecutableApproval",
    "BrokerLiveOrderRouteScheduleLedgerPathEnabled"
)) {
    if ($forbidden.$property -ne $false) { Fail "Forbidden action observed or allowed: $property" }
}

$evidence = Read-Json "phase-exec-live-r009-build-test-validator-evidence.json"
if ($evidence.Build -ne "Passed") { Fail "Build evidence missing or not passed" }
if ($evidence.FocusedR009Checks -ne "Passed") { Fail "Focused R009 checks evidence missing or not passed" }
if ($evidence.UnitTests -ne "Passed") { Fail "Unit test evidence missing or not passed" }
if ($evidence.Validator -ne "Passed") { Fail "Validator evidence missing or not passed" }

Write-Host "EXEC-LIVE-R009 validator passed."
