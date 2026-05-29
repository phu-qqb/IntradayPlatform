$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-live"

function Fail {
    param([string]$Message)
    throw "EXEC-LIVE-R011 validator failed: $Message"
}

function Read-Json {
    param([string]$Name)
    $path = Join-Path $artifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { Fail "Missing artifact $Name" }
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$requiredArtifacts = @(
    "phase-exec-live-r011-summary.md",
    "phase-exec-live-r011-r010-trial-reference.json",
    "phase-exec-live-r011-r009-contract-reference.json",
    "phase-exec-live-r011-disabled-preview-trial-review-contract.json",
    "phase-exec-live-r011-disabled-preview-trial-review-result.json",
    "phase-exec-live-r011-pre-paper-ledger-preview-readiness-contract.json",
    "phase-exec-live-r011-pre-paper-ledger-preview-readiness-result.json",
    "phase-exec-live-r011-paper-ledger-preview-only-contract.json",
    "phase-exec-live-r011-paper-ledger-preview-boundary-guard.json",
    "phase-exec-live-r011-ledger-commit-blockers.json",
    "phase-exec-live-r011-allowed-preview-ledger-consumers.json",
    "phase-exec-live-r011-forbidden-ledger-consumers.json",
    "phase-exec-live-r011-trial-coverage-summary.json",
    "phase-exec-live-r011-held-readiness-review.json",
    "phase-exec-live-r011-rejected-input-review.json",
    "phase-exec-live-r011-direct-cross-rejection-review.json",
    "phase-exec-live-r011-legacy-target-close-rejection-review.json",
    "phase-exec-live-r011-usdjpy-caveat-review.json",
    "phase-exec-live-r011-audit-record-review.json",
    "phase-exec-live-r011-operator-review-artifact-review.json",
    "phase-exec-live-r011-decision.json",
    "phase-exec-live-r011-executable-promotion-blockers.json",
    "phase-exec-live-r011-no-broker-activation-audit.json",
    "phase-exec-live-r011-no-live-marketdata-audit.json",
    "phase-exec-live-r011-no-scheduler-service-polling-audit.json",
    "phase-exec-live-r011-no-order-created-audit.json",
    "phase-exec-live-r011-no-child-order-audit.json",
    "phase-exec-live-r011-no-executable-schedule-audit.json",
    "phase-exec-live-r011-no-route-no-submission-audit.json",
    "phase-exec-live-r011-no-fill-execution-report-audit.json",
    "phase-exec-live-r011-no-paper-ledger-commit-audit.json",
    "phase-exec-live-r011-no-state-mutation-audit.json",
    "phase-exec-live-r011-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-live-r011-legacy-compatibility-preservation.json",
    "phase-exec-live-r011-direct-cross-exclusion-preservation.json",
    "phase-exec-live-r011-usd-pair-netting-requirement.json",
    "phase-exec-live-r011-usdjpy-caveat-preservation.json",
    "phase-exec-live-r011-cost-guidance-preservation.json",
    "phase-exec-live-r011-nonmajor-calibration-preservation.json",
    "phase-exec-live-r011-no-external-audit.json",
    "phase-exec-live-r011-forbidden-actions-audit.json",
    "phase-exec-live-r011-next-phase-recommendation.json",
    "phase-exec-live-r011-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $artifact))) {
        Fail "Required artifact missing: $artifact"
    }
}

$testPath = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/R009PrePaperLedgerPreviewReadinessTests.cs"
if (-not (Test-Path -LiteralPath $testPath)) { Fail "Focused R011 tests missing" }
$testSource = Get-Content -LiteralPath $testPath -Raw
foreach ($token in @(
    "R009PrePaperLedgerPreviewReadinessTests",
    "PaperLedgerPreviewOnly",
    "PaperLedgerCommit",
    "PaperLedgerCommitter",
    "ProductionTradingRuntime",
    "R009LiveFeatureFlags.DisabledDefaults",
    "R009DisabledBoundaryGuard.Disabled"
)) {
    if ($testSource -notmatch [regex]::Escape($token)) { Fail "Focused R011 tests missing token: $token" }
}

$reference = Read-Json "phase-exec-live-r011-r010-trial-reference.json"
if ($reference.SourceArtifactsPresent -ne $true -or $reference.R010PassedWithHeldReadiness -ne $true -or $reference.ExecutableApprovalInherited -ne $false) {
    Fail "R010 trial reference missing or implies executable approval"
}

$contract = Read-Json "phase-exec-live-r011-r009-contract-reference.json"
if ($contract.NonExecutable -ne $true -or $contract.NotAnOrder -ne $true -or $contract.NoBrokerRoute -ne $true) { Fail "R009 contract weakens non-executable status" }
if ($contract.BrokerReady -ne $false -or $contract.LiveReady -ne $false -or $contract.ExecutablePromotionAuthorized -ne $false) { Fail "R009 promoted or live/broker ready" }

$reviewContract = Read-Json "phase-exec-live-r011-disabled-preview-trial-review-contract.json"
if ($reviewContract.ReviewOnly -ne $true -or $reviewContract.ExecutesPreviewFlow -ne $false -or $reviewContract.ExecutesLedgerCommit -ne $false) { Fail "Trial review contract runs forbidden work" }
foreach ($flag in @("NonExecutable=true", "NotAnOrder=true", "NotSubmitted=true", "NoBrokerRoute=true", "NoFill=true", "NoExecutionReport=true", "NoRoute=true", "NoSubmission=true", "NoPaperLedgerCommit=true")) {
    if (@($reviewContract.RequiredOutputFlags) -notcontains $flag) { Fail "Trial review contract missing output flag $flag" }
}

$reviewResult = Read-Json "phase-exec-live-r011-disabled-preview-trial-review-result.json"
if ($reviewResult.AcceptedRequests -ne 5 -or
    $reviewResult.ForbiddenConsumerRejections -ne 6 -or
    $reviewResult.PreviewReadyDecisions -ne 5 -or
    $reviewResult.HeldMissingReadinessDecisions -ne 1 -or
    $reviewResult.RejectedDecisions -ne 2 -or
    $reviewResult.AuditRecords -ne 5 -or
    $reviewResult.OperatorReviewReports -ne 1) { Fail "Trial review counts do not match R010" }
foreach ($flag in @("DirectCrossRejected", "Legacy06Rejected", "UsdjpyCaveatPreserved", "NonExecutable", "NotAnOrder", "NotSubmitted", "NoBrokerRoute", "NoFill", "NoExecutionReport", "NoRoute", "NoSubmission", "NoPaperLedgerCommit")) {
    if ($reviewResult.$flag -ne $true) { Fail "Trial review missing flag $flag" }
}
if ($reviewResult.AudusdStatus -ne "SupportedAndNotFailed") { Fail "AUDUSD misclassified" }

$readinessContract = Read-Json "phase-exec-live-r011-pre-paper-ledger-preview-readiness-contract.json"
if ($readinessContract.NotPaperLedgerCommit -ne $true -or $readinessContract.NotExecution -ne $true -or $readinessContract.NotBrokerLiveReadiness -ne $true -or $readinessContract.FutureGateRequired -ne $true) {
    Fail "Pre-paper-ledger readiness contract implies commit/execution/broker/live readiness"
}
foreach ($requirement in @("disabled preview decisions available", "audit records available", "operator review available", "preview outputs are non-executable", "no order-domain output", "no route/fill/report output", "no state mutation", "explicit operator approval for preview-only", "explicit no-commit flag")) {
    if (@($readinessContract.ReadinessRequirements) -notcontains $requirement) { Fail "Missing readiness requirement: $requirement" }
}

$readinessResult = Read-Json "phase-exec-live-r011-pre-paper-ledger-preview-readiness-result.json"
foreach ($flag in @("DisabledPreviewDecisionsAvailable", "AuditRecordsAvailable", "OperatorReviewAvailable", "PreviewOutputsNonExecutable", "NoOrderDomainOutput", "NoRouteFillReportOutput", "NoStateMutation", "ExplicitOperatorApprovalForPreviewOnlyRequired", "ExplicitNoCommitFlagRequired")) {
    if ($readinessResult.$flag -ne $true) { Fail "Readiness result missing flag $flag" }
}
if ($readinessResult.LedgerCommitApproval -ne $false -or $readinessResult.PaperLedgerCommitRecordsCreated -ne $false) { Fail "Readiness result implies ledger commit approval or record creation" }

$previewContract = Read-Json "phase-exec-live-r011-paper-ledger-preview-only-contract.json"
foreach ($allowed in @("PaperLedgerPreviewOnly", "HypotheticalPositionDeltaPreview", "HypotheticalCashImpactPreview", "HypotheticalExposurePreview", "OperatorReviewOnly")) {
    if (@($previewContract.AllowedFutureOutputs) -notcontains $allowed) { Fail "Preview-only contract missing allowed output $allowed" }
}
foreach ($forbiddenOutput in @("PaperLedgerCommit", "LedgerMutation", "TradingStateMutation", "Order", "Route", "Fill", "ExecutionReport", "Submission", "ExecutableSchedule")) {
    if (@($previewContract.AllowedFutureOutputs) -contains $forbiddenOutput) { Fail "Preview-only contract allows forbidden output $forbiddenOutput" }
    if (@($previewContract.ForbiddenFutureOutputs) -notcontains $forbiddenOutput) { Fail "Preview-only contract missing forbidden output $forbiddenOutput" }
}
if ($previewContract.PaperLedgerCommitAllowed -ne $false -or $previewContract.LedgerMutationAllowed -ne $false -or $previewContract.TradingStateMutationAllowed -ne $false -or $previewContract.OrderDomainInputAllowed -ne $false -or $previewContract.PreviewOnly -ne $true) {
    Fail "Paper-ledger-preview contract allows commit, mutation, order input, or non-preview output"
}

$guard = Read-Json "phase-exec-live-r011-paper-ledger-preview-boundary-guard.json"
if ($guard.PaperLedgerPreviewEnabledNow -ne $false -or $guard.PaperLedgerCommitEnabled -ne $false -or $guard.LedgerMutationAllowed -ne $false -or $guard.TradingStateMutationAllowed -ne $false -or $guard.OrderDomainInputAllowed -ne $false -or $guard.BrokerRouteAllowed -ne $false -or $guard.ExecutableScheduleAllowed -ne $false -or $guard.PreviewOnly -ne $true) {
    Fail "Paper-ledger-preview boundary guard allows forbidden path"
}

$ledgerBlockers = Read-Json "phase-exec-live-r011-ledger-commit-blockers.json"
if ($ledgerBlockers.PaperLedgerCommitBlocked -ne $true -or $ledgerBlockers.PaperLedgerCommitRecordsCreated -ne $false) { Fail "Ledger commit blockers missing" }
foreach ($blocker in @("PaperLedgerCommitEnabled=false", "LedgerMutationAllowed=false", "TradingStateMutationAllowed=false", "OrderDomainInputAllowed=false", "Separate future explicit paper-ledger-preview gate required", "Separate future explicit paper-ledger-commit gate required before any commit discussion")) {
    if (@($ledgerBlockers.Blockers) -notcontains $blocker) { Fail "Ledger commit blocker missing: $blocker" }
}

$allowedConsumers = Read-Json "phase-exec-live-r011-allowed-preview-ledger-consumers.json"
foreach ($consumer in @("OperatorReviewTool", "InternalPmsPreviewConsumer", "InternalEmsPreviewConsumer", "InternalOmsPreviewConsumer", "TestHarness")) {
    if (@($allowedConsumers.AllowedConsumers) -notcontains $consumer) { Fail "Allowed preview ledger consumer missing $consumer" }
}
if ($allowedConsumers.PaperLedgerCommitterAllowed -ne $false -or $allowedConsumers.BrokerGatewayAllowed -ne $false -or $allowedConsumers.OrderRouterAllowed -ne $false -or $allowedConsumers.ProductionTradingRuntimeAllowed -ne $false) { Fail "Forbidden preview ledger consumer allowed" }

$forbiddenConsumers = Read-Json "phase-exec-live-r011-forbidden-ledger-consumers.json"
foreach ($consumer in @("PaperLedgerCommitter", "ProductionTradingRuntime", "BrokerGateway", "OrderRouter")) {
    if (@($forbiddenConsumers.ForbiddenConsumers) -notcontains $consumer) { Fail "Forbidden ledger consumer missing $consumer" }
}
if ($forbiddenConsumers.ForbiddenConsumersAllowed -ne $false) { Fail "Forbidden ledger consumers allowed" }

$coverage = Read-Json "phase-exec-live-r011-trial-coverage-summary.json"
if ($coverage.AcceptedRequests -ne 5 -or $coverage.ForbiddenConsumerRejections -ne 6 -or $coverage.PreviewReadyDecisions -ne 5 -or $coverage.HeldMissingReadinessDecisions -ne 1 -or $coverage.RejectedDecisions -ne 2 -or $coverage.AuditRecords -ne 5 -or $coverage.OperatorReviewReports -ne 1 -or $coverage.StableForPrePaperLedgerPreviewPlanning -ne $true) {
    Fail "Coverage summary unexpected"
}

$held = Read-Json "phase-exec-live-r011-held-readiness-review.json"
if ($held.HeldMissingReadinessDecisions -ne 1 -or $held.HeldReadinessIsR009LogicFailure -ne $false -or $held.HeldReadinessAuthorizesLedgerCommit -ne $false -or $held.HeldReadinessAuthorizesOrder -ne $false) { Fail "Held readiness review unsafe" }

$rejected = Read-Json "phase-exec-live-r011-rejected-input-review.json"
if ($rejected.RejectedDecisions -ne 2 -or $rejected.RejectedInputProducesOrder -ne $false -or $rejected.RejectedInputProducesRoute -ne $false -or $rejected.RejectedInputProducesExecutableSchedule -ne $false) { Fail "Rejected input review unsafe" }

$direct = Read-Json "phase-exec-live-r011-direct-cross-rejection-review.json"
if ($direct.DirectCrossExecutionAllowed -ne $false -or $direct.DirectCrossExecutionIntentRejected -ne $true -or $direct.CreatesOrder -ne $false -or $direct.CreatesRoute -ne $false) { Fail "Direct-cross rejection weakened" }

$legacy = Read-Json "phase-exec-live-r011-legacy-target-close-rejection-review.json"
if ($legacy.AcceptedAsFutureCanonical -ne $false) { Fail "Legacy :06 accepted as future canonical" }

$audit = Read-Json "phase-exec-live-r011-audit-record-review.json"
if ($audit.AuditRecords -ne 5 -or $audit.ArtifactOnly -ne $true -or $audit.DbWrites -ne $false -or $audit.OrderDomainPersistence -ne $false -or $audit.RouteSubmissionPersistence -ne $false -or $audit.LedgerPersistence -ne $false -or $audit.TradingStateMutation -ne $false) { Fail "Audit record review unsafe" }

$operator = Read-Json "phase-exec-live-r011-operator-review-artifact-review.json"
if ($operator.OperatorReviewReports -ne 1 -or $operator.ReviewOnly -ne $true -or $operator.WritesOutsideArtifactPath -ne $false -or $operator.ExecutableApproval -ne $false) { Fail "Operator review artifact review unsafe" }

$decision = Read-Json "phase-exec-live-r011-decision.json"
if ($decision.Decision -ne "R009DisabledPreviewTrialPassedForPrePaperLedgerPreviewPlanning" -or $decision.TrialReviewPassed -ne $true -or $decision.PrePaperLedgerPreviewPlanningReady -ne $true) { Fail "R011 decision not ready for pre-paper-ledger preview planning" }
if ($decision.PaperLedgerCommitApproval -ne $false -or $decision.ExecutableApproval -ne $false -or $decision.BrokerApproval -ne $false -or $decision.LiveApproval -ne $false -or $decision.SeparateFuturePaperLedgerPreviewGateRequired -ne $true -or $decision.SeparateFuturePaperLedgerCommitGateRequired -ne $true) {
    Fail "R011 decision implies commit/executable/broker/live approval or omits future gates"
}

$blockers = Read-Json "phase-exec-live-r011-executable-promotion-blockers.json"
if ($blockers.ExecutablePromotionBlocked -ne $true) { Fail "Executable promotion not blocked" }
foreach ($blocker in @("No broker integration authorized", "No live market data authorized", "No scheduler/service/polling authorized", "No order-domain creation authorized", "No route/submission/fill/execution-report path authorized", "No executable schedule authorized", "No paper ledger commit authorized", "No ledger mutation authorized", "No trading state mutation authorized", "Separate explicit executable gate required")) {
    if (@($blockers.Blockers) -notcontains $blocker) { Fail "Executable blocker missing $blocker" }
}

$usdPair = Read-Json "phase-exec-live-r011-usd-pair-netting-requirement.json"
if ($usdPair.AudusdStatus -ne "SupportedAndNotFailed") { Fail "AUDUSD misclassified" }
$usdjpy = Read-Json "phase-exec-live-r011-usdjpy-caveat-preservation.json"
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or $usdjpy.RequiresInversion -ne $true -or $usdjpy.SecurityID -ne "4004" -or $usdjpy.SecurityIDSource -ne "8") {
    Fail "USDJPY caveat weakened"
}
$cost = Read-Json "phase-exec-live-r011-cost-guidance-preservation.json"
if ($cost.Universalized -ne $false) { Fail "5 USD/million universalized" }
$nonmajor = Read-Json "phase-exec-live-r011-nonmajor-calibration-preservation.json"
if ($nonmajor.LiveCapableExecutionAllowed -ne $false) { Fail "Nonmajor/EM/scandi/CNH execution allowed" }

$forbidden = Read-Json "phase-exec-live-r011-forbidden-actions-audit.json"
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
    "PaperLedgerCommitRecordCreated",
    "LedgerMutationAllowed",
    "StateMutationOccurred",
    "R009PromotedToExecutableUse",
    "PrePaperLedgerReadinessImpliesLedgerCommitApproval",
    "PaperLedgerPreviewContractAllowsCommits",
    "BrokerLiveOrderRouteScheduleLedgerPathEnabled",
    "ForbiddenConsumerAllowed",
    "DirectCrossExecutionAllowed",
    "Legacy06AcceptedAsFutureCanonical",
    "PreviewOutputRepresentedAsOrderRouteFillSchedule"
)) {
    if ($forbidden.$property -ne $false) { Fail "Forbidden action observed or allowed: $property" }
}

$evidence = Read-Json "phase-exec-live-r011-build-test-validator-evidence.json"
if ($evidence.Build -ne "Passed") { Fail "Build evidence missing or not passed" }
if ($evidence.FocusedR011Tests -ne "Passed") { Fail "Focused R011 test evidence missing or not passed" }
if ($evidence.UnitTests -ne "Passed") { Fail "Unit test evidence missing or not passed" }
if ($evidence.Validator -ne "Passed") { Fail "Validator evidence missing or not passed" }

Write-Host "EXEC-LIVE-R011 validator passed."
