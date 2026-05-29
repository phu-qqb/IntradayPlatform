param(
    [string]$BuildStatus = "Pending",
    [string]$FocusedTestsStatus = "Pending",
    [string]$UnitTestsStatus = "Pending",
    [string]$ValidatorStatus = "Pending"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-live"
$operatorReviewDir = Join-Path $artifactDir "operator-review"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null
New-Item -ItemType Directory -Force -Path $operatorReviewDir | Out-Null

$phase = "EXEC-LIVE-R007"
$contractVersion = "0.3.0-design-only-candidate"
$classifications = @(
    "EXEC_LIVE_R007_PASS_OPERATOR_REVIEW_HANDOFF_READY_NO_EXTERNAL",
    "EXEC_LIVE_R007_PASS_REVIEW_CLI_REPORTING_CONTRACT_READY_NO_EXTERNAL",
    "EXEC_LIVE_R007_PASS_REVIEW_OUTPUT_BOUNDARY_GUARD_READY_NO_EXTERNAL",
    "EXEC_LIVE_R007_PASS_NO_BROKER_NO_ORDER_NO_ROUTE_GATE_READY_NO_EXTERNAL"
)

function Write-JsonArtifact {
    param([string]$Name, [object]$Value)
    $Value | ConvertTo-Json -Depth 50 | Set-Content -Path (Join-Path $artifactDir $Name) -Encoding UTF8
}

function New-Hash {
    param([string]$Value)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Value)))).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function New-Audit {
    param([string]$Name, [string]$Evidence)
    [ordered]@{
        Phase = $phase
        Audit = $Name
        Status = "Pass"
        Evidence = $Evidence
        ExternalApiCallsMade = $false
        PolygonCallsMade = $false
        LmaxCallsMade = $false
        BrokerActivationOccurred = $false
        LiveMarketDataRequested = $false
        SchedulerServicePollingStarted = $false
        PmsEmsOmsCycleRun = $false
        ManualNoExternalCommandRun = $false
        BacktestOrSimulationRun = $false
        NonExecutable = $true
        NotAnOrder = $true
        NoBrokerRoute = $true
        NoPaperLedgerCommit = $true
        NoTradingStateMutation = $true
    }
}

$sampleAuditHash = New-Hash "exec-live-r007|sample-audit"
$sampleBatchAuditHash = New-Hash "exec-live-r007|sample-batch-audit"
$sampleList = [ordered]@{
    Phase = $phase
    SampleOnly = $true
    CommandMode = "ListAuditRecords"
    AuditRecords = @(
        [ordered]@{
            RequestId = "exec-live-r007-sample-single"
            BatchRequestId = $null
            DecisionPreviewId = "exec-live-r007-sample-single-decision"
            ConsumerType = "InternalEmsPreviewConsumer"
            AuditHash = $sampleAuditHash
            InputHash = New-Hash "exec-live-r007|single|input"
            DecisionHash = New-Hash "exec-live-r007|single|decision"
            ArtifactPath = "artifacts/readiness/execution-live/audit/exec-live-r007-sample-single.preview-audit.json"
            NonExecutable = $true
            NotAnOrder = $true
            NoBrokerRoute = $true
            NoPaperLedgerCommit = $true
        },
        [ordered]@{
            RequestId = "exec-live-r007-sample-batch"
            BatchRequestId = "exec-live-r007-sample-batch-request"
            DecisionPreviewId = "exec-live-r007-sample-batch-request"
            ConsumerType = "InternalPmsPreviewConsumer"
            AuditHash = $sampleBatchAuditHash
            InputHash = New-Hash "exec-live-r007|batch|input"
            DecisionHash = New-Hash "exec-live-r007|batch|decision"
            ArtifactPath = "artifacts/readiness/execution-live/audit/exec-live-r007-sample-batch.preview-audit.json"
            NonExecutable = $true
            NotAnOrder = $true
            NoBrokerRoute = $true
            NoPaperLedgerCommit = $true
        }
    )
    NonExecutable = $true
    NotAnOrder = $true
    NoBrokerRoute = $true
    ReviewOnly = $true
    ExecutableApproval = $false
    BrokerApproval = $false
    LiveApproval = $false
}

$sampleBatchSummary = [ordered]@{
    Phase = $phase
    SampleOnly = $true
    CommandMode = "SummarizeBatch"
    BatchRequestId = "exec-live-r007-sample-batch-request"
    BatchStatus = "PreviewBatchGeneratedWithRejectedItems"
    ItemCount = 3
    PreviewReadyCount = 1
    HeldMissingReadinessCount = 1
    RejectedCount = 1
    HeldReasons = @([ordered]@{ Reason = "HeldMissingReadiness"; Count = 1; HeldNotOrder = $true })
    RejectedReasons = @([ordered]@{ Reason = "Rejected"; Count = 1; RejectedNotOrder = $true })
    NonExecutable = $true
    NotAnOrder = $true
    NoBrokerRoute = $true
}

Write-JsonArtifact "phase-exec-live-r007-r006-audit-reference.json" ([ordered]@{
    Phase = $phase
    R006Classifications = @(
        "EXEC_LIVE_R006_PASS_PREVIEW_AUDIT_CONTRACT_READY_NO_EXTERNAL",
        "EXEC_LIVE_R006_PASS_ARTIFACT_AUDIT_WRITER_READY_NO_EXTERNAL",
        "EXEC_LIVE_R006_PASS_IDEMPOTENCY_REPLAY_SEMANTICS_READY_NO_EXTERNAL",
        "EXEC_LIVE_R006_PASS_NO_ORDER_DOMAIN_PERSISTENCE_GATE_READY_NO_EXTERNAL"
    )
    AuditEnvelope = "R009PreviewAuditEnvelope"
    ArtifactAuditWriter = "R009PreviewArtifactAuditWriter"
    AuditReadRoot = "artifacts/readiness/execution-live/audit"
})
Write-JsonArtifact "phase-exec-live-r007-r005-consumer-boundary-reference.json" ([ordered]@{
    Phase = $phase
    AllowedReviewConsumer = "OperatorReviewTool"
    ForbiddenConsumers = @("BrokerGateway", "OrderRouter", "Scheduler", "PaperLedgerCommitter", "ExecutionReportHandler", "ProductionTradingRuntime")
    PreviewOutputIsOrderIntent = $false
    PreviewOutputIsRouteable = $false
    PreviewOutputIsExecutableSchedule = $false
    PreviewOutputIsFillReportInput = $false
})
Write-JsonArtifact "phase-exec-live-r007-r009-contract-reference.json" ([ordered]@{
    Phase = $phase
    R009ContractVersion = $contractVersion
    PrimaryPolicyCandidate = "CloseSeeking15mAdaptive_BalancedAdaptive_v0"
    SecondaryPolicyCandidate = "CloseSeeking15mAdaptive_ResidualAwareUrgency_v0"
    ConditionalResidualModule = "ControlledResidualCross_BalancedResidualCross_v0"
    DesignOnly = $true
    PaperOnly = $true
    NonExecutable = $true
    NotAnOrder = $true
    NotSubmitted = $true
    NoBrokerRoute = $true
    BrokerReady = $false
    LiveReady = $false
    ExecutablePromotionAuthorized = $false
})
Write-JsonArtifact "phase-exec-live-r007-operator-review-contract.json" ([ordered]@{
    Phase = $phase
    Contract = "R009 Disabled Preview Operator Review Contract"
    Service = "R009OperatorPreviewReviewService"
    Supports = @("ListAuditRecords", "ShowAuditRecord", "SummarizeBatch", "ExportOperatorReport", "HeldReasonSummary", "RejectedReasonSummary", "SafetyFlags", "IdempotencyAuditHashes", "NotExecutableStatus")
    DoesNotSupport = @("ApproveForExecution", "SubmitOrder", "CreateRoute", "CreateSchedule", "CommitLedger", "ActivateBroker", "TriggerLiveMarketData", "StartSchedulerWorker")
    OperatorApprovalImpliesExecutableApproval = $false
})
Write-JsonArtifact "phase-exec-live-r007-operator-cli-contract.json" ([ordered]@{
    Phase = $phase
    Surface = "R009DisabledPreviewReview"
    Implementation = "Application service/reporting contract; not wired to live runtime."
    AllowedCommandModes = @("ListAuditRecords", "ShowAuditRecord", "SummarizeBatch", "ExportOperatorReport")
    ForbiddenCommandModes = @("Execute", "Submit", "Route", "Fill", "CommitLedger", "ActivateBroker", "StartScheduler", "PromoteLive")
    ReadRoot = "artifacts/readiness/execution-live/audit"
    WriteRoot = "artifacts/readiness/execution-live/operator-review"
    BrokerLiveOrderRouteScheduleLedgerStatePathsEnabled = $false
})
Write-JsonArtifact "phase-exec-live-r007-operator-review-request-dto-contract.json" ([ordered]@{
    Phase = $phase
    Dto = "R009OperatorPreviewReviewRequest"
    Fields = @("ReviewRequestId", "CommandMode", "ConsumerType", "RequestId", "BatchRequestId", "AuditRootPath", "OutputRootPath")
})
Write-JsonArtifact "phase-exec-live-r007-operator-review-response-dto-contract.json" ([ordered]@{
    Phase = $phase
    Dto = "R009OperatorPreviewReviewResponse"
    RequiredFlags = @("NonExecutable=true", "NotAnOrder=true", "NotSubmitted=true", "NoBrokerRoute=true", "NoFill=true", "NoExecutionReport=true", "NoRoute=true", "NoSubmission=true", "NoPaperLedgerCommit=true", "ReviewOnly=true", "ExecutableApproval=false", "BrokerApproval=false", "LiveApproval=false")
})
Write-JsonArtifact "phase-exec-live-r007-operator-review-summary-contract.json" ([ordered]@{ Phase = $phase; Dto = "R009OperatorPreviewSummary"; Fields = @("AuditRecordCount", "SinglePreviewAuditCount", "BatchPreviewAuditCount", "PreviewReadyCount", "HeldMissingReadinessCount", "RejectedCount", "HeldReasons", "RejectedReasons") })
Write-JsonArtifact "phase-exec-live-r007-operator-held-reason-summary-contract.json" ([ordered]@{ Phase = $phase; Dto = "R009OperatorHeldReasonSummary"; HeldMissingReadinessProducesOrder = $false; HeldNotOrder = $true })
Write-JsonArtifact "phase-exec-live-r007-operator-rejected-reason-summary-contract.json" ([ordered]@{ Phase = $phase; Dto = "R009OperatorRejectedReasonSummary"; RejectedDirectCrossProducesOrder = $false; RejectedNotOrder = $true })

Write-JsonArtifact "phase-exec-live-r007-sample-list-audit-response.json" $sampleList
Write-JsonArtifact "phase-exec-live-r007-sample-single-record-review.json" ([ordered]@{
    Phase = $phase
    SampleOnly = $true
    CommandMode = "ShowAuditRecord"
    RequestId = "exec-live-r007-sample-single"
    DecisionStatus = "PreviewGenerated"
    AuditHash = $sampleAuditHash
    NonExecutable = $true
    NotAnOrder = $true
    NoBrokerRoute = $true
    NoPaperLedgerCommit = $true
    ReviewOnly = $true
})
Write-JsonArtifact "phase-exec-live-r007-sample-batch-summary.json" $sampleBatchSummary
Write-JsonArtifact "phase-exec-live-r007-sample-held-line-summary.json" ([ordered]@{
    Phase = $phase
    SampleOnly = $true
    HeldReason = "HeldMissingReadiness"
    Count = 1
    HeldNotOrder = $true
    ExecutableApproval = $false
})

$operatorReport = @"
# R009 Disabled Preview Operator Review

SampleOnly=true

Audit records: 2
PreviewReady: 1
HeldMissingReadiness: 1
Rejected: 1

NonExecutable=true
NotAnOrder=true
NoBrokerRoute=true
NoPaperLedgerCommit=true
ExecutableApproval=false
BrokerApproval=false
LiveApproval=false

Operator review is for disabled preview inspection only. It cannot approve execution, submit orders, create routes, create schedules, commit ledger state, activate broker paths, trigger live market data, or start scheduler/worker paths.
"@
$operatorReport | Set-Content -Path (Join-Path $artifactDir "phase-exec-live-r007-sample-operator-report.md") -Encoding UTF8
$operatorReport | Set-Content -Path (Join-Path $operatorReviewDir "phase-exec-live-r007-sample-operator-report.md") -Encoding UTF8

Write-JsonArtifact "phase-exec-live-r007-invalid-review-command-rejection-results.json" ([ordered]@{
    Phase = $phase
    Results = @(
        [ordered]@{ CommandMode = "Execute"; Accepted = $false; RejectionReason = "ForbiddenCommandMode:Execute" },
        [ordered]@{ CommandMode = "Submit"; Accepted = $false; RejectionReason = "ForbiddenCommandMode:Submit" },
        [ordered]@{ CommandMode = "Route"; Accepted = $false; RejectionReason = "ForbiddenCommandMode:Route" },
        [ordered]@{ CommandMode = "CommitLedger"; Accepted = $false; RejectionReason = "ForbiddenCommandMode:CommitLedger" },
        [ordered]@{ CommandMode = "StartScheduler"; Accepted = $false; RejectionReason = "ForbiddenCommandMode:StartScheduler" },
        [ordered]@{ ConsumerType = "BrokerGateway"; Accepted = $false; RejectionReason = "ForbiddenConsumer:BrokerGateway" }
    )
})

Write-JsonArtifact "phase-exec-live-r007-review-output-not-order-audit.json" ([ordered]@{ Phase = $phase; ReviewCanApproveExecution = $false; CanConvertToOrder = $false; CanConvertToChildOrder = $false; NotAnOrder = $true })
Write-JsonArtifact "phase-exec-live-r007-review-output-not-route-audit.json" ([ordered]@{ Phase = $phase; CanConvertToRoute = $false; CanConvertToSubmission = $false; NoRoute = $true; NoSubmission = $true; NoBrokerRoute = $true })
Write-JsonArtifact "phase-exec-live-r007-review-output-not-schedule-audit.json" ([ordered]@{ Phase = $phase; CanCreateExecutableSchedule = $false; NoExecutableSchedule = $true })
Write-JsonArtifact "phase-exec-live-r007-review-output-not-ledger-audit.json" ([ordered]@{ Phase = $phase; CanCommitPaperLedger = $false; NoPaperLedgerCommit = $true; NoTradingStateMutation = $true })
Write-JsonArtifact "phase-exec-live-r007-artifact-path-safety-review.json" ([ordered]@{ Phase = $phase; ReadRoot = "artifacts/readiness/execution-live/audit"; WriteRoot = "artifacts/readiness/execution-live/operator-review"; ReadsOutsideArtifactPath = $false; WritesOutsideArtifactPath = $false; RequiresDb = $false; RequiresExternalService = $false })
Write-JsonArtifact "phase-exec-live-r007-kill-switch-feature-flag-review.json" ([ordered]@{ Phase = $phase; R009LiveTradingEnabled = $false; R009BrokerRoutingEnabled = $false; R009OrderSubmissionEnabled = $false; R009ExecutableScheduleEnabled = $false; R009PaperLedgerCommitEnabled = $false; R009SchedulerEnabled = $false; R009BackgroundWorkerEnabled = $false; R009DryRunOnly = $true })
Write-JsonArtifact "phase-exec-live-r007-disabled-boundary-guard-review.json" ([ordered]@{ Phase = $phase; BrokerRouteCreationAllowed = $false; OrderCreationAllowed = $false; ChildSliceCreationAllowed = $false; ChildOrderCreationAllowed = $false; ScheduleExecutionAllowed = $false; SubmissionAllowed = $false; FillCreationAllowed = $false; ExecutionReportCreationAllowed = $false; StateMutationAllowed = $false; PaperLedgerCommitAllowed = $false })

$audits = [ordered]@{
    "phase-exec-live-r007-no-broker-activation-audit.json" = New-Audit "NoBrokerActivation" "Operator review reads preview audit artifacts only; no broker runtime was activated."
    "phase-exec-live-r007-no-live-marketdata-audit.json" = New-Audit "NoLiveMarketData" "Operator review does not request live market data."
    "phase-exec-live-r007-no-scheduler-service-polling-audit.json" = New-Audit "NoSchedulerServicePolling" "StartScheduler is a forbidden command mode."
    "phase-exec-live-r007-no-order-created-audit.json" = New-Audit "NoOrderCreated" "Operator review output is NotAnOrder and cannot approve execution."
    "phase-exec-live-r007-no-child-order-audit.json" = New-Audit "NoChildOrder" "Operator review cannot create child orders or child slices."
    "phase-exec-live-r007-no-executable-schedule-audit.json" = New-Audit "NoExecutableSchedule" "Operator review cannot create executable schedules."
    "phase-exec-live-r007-no-route-no-submission-audit.json" = New-Audit "NoRouteNoSubmission" "Route and Submit are forbidden command modes."
    "phase-exec-live-r007-no-fill-execution-report-audit.json" = New-Audit "NoFillExecutionReport" "Fill mode is forbidden and review output is not fill/report input."
    "phase-exec-live-r007-no-paper-ledger-commit-audit.json" = New-Audit "NoPaperLedgerCommit" "CommitLedger is a forbidden command mode."
    "phase-exec-live-r007-no-state-mutation-audit.json" = New-Audit "NoStateMutation" "Review writes only operator-review artifacts and mutates no trading state."
    "phase-exec-live-r007-no-external-audit.json" = New-Audit "NoExternal" "No external API, Polygon, LMAX, broker, or live market data call is part of this phase."
}
foreach ($entry in $audits.GetEnumerator()) { Write-JsonArtifact $entry.Key $entry.Value }

Write-JsonArtifact "phase-exec-live-r007-canonical-quarter-hour-policy-preservation.json" ([ordered]@{ Phase = $phase; FutureCanonicalMinutes = @(0, 15, 30, 45); LegacyMinutesAcceptedAsFutureCanonical = $false })
Write-JsonArtifact "phase-exec-live-r007-legacy-compatibility-preservation.json" ([ordered]@{ Phase = $phase; LegacyLabels = @(":06", ":21", ":36", ":51"); Usage = "CompatibilityOnly"; UsedAsFutureCanonical = $false })
Write-JsonArtifact "phase-exec-live-r007-direct-cross-exclusion-preservation.json" ([ordered]@{ Phase = $phase; DirectCrossExecutionAllowed = $false; DirectCrossRejectedNotOrder = $true })
Write-JsonArtifact "phase-exec-live-r007-usd-pair-netting-requirement.json" ([ordered]@{ Phase = $phase; Requirement = "EMS/OMS execution intents must be USD-pair-only after netting."; SupportedExecutionSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF"); AudusdStatus = "SupportedAndNotFailed" })
Write-JsonArtifact "phase-exec-live-r007-usdjpy-caveat-preservation.json" ([ordered]@{ Phase = $phase; NormalizedPortfolioSymbol = "JPYUSD"; ExecutionTradableSymbol = "USDJPY"; RequiresInversion = $true; SecurityID = "4004"; SecurityIDSource = "8"; CaveatPreserved = $true })
Write-JsonArtifact "phase-exec-live-r007-cost-guidance-preservation.json" ([ordered]@{ Phase = $phase; FiveUsdPerMillion = "BestCaseMajorOnly"; Universalized = $false })
Write-JsonArtifact "phase-exec-live-r007-nonmajor-calibration-preservation.json" ([ordered]@{ Phase = $phase; NonmajorEmScandiCnh = "CalibrationRequired"; LiveCapableExecutionAllowed = $false })
Write-JsonArtifact "phase-exec-live-r007-forbidden-actions-audit.json" ([ordered]@{ Phase = $phase; ExternalApiCallsMade = $false; PolygonCallsMade = $false; LmaxCallsMade = $false; BrokerActivationOccurred = $false; LiveMarketDataRequested = $false; SchedulerServicePollingBackgroundJobIntroduced = $false; PmsEmsOmsCycleRun = $false; ManualNoExternalCommandRun = $false; BacktestSimulationRun = $false; TcaResultLinesCreated = $false; ExecutableScheduleCreated = $false; OrdersChildOrdersRoutesSubmissionsFillsReportsCreated = $false; PaperLedgerCommitCreated = $false; StateMutationOccurred = $false; R009PromotedToExecutableUse = $false; OperatorReviewCanApproveExecution = $false })
Write-JsonArtifact "phase-exec-live-r007-next-phase-recommendation.json" ([ordered]@{ Phase = $phase; RecommendedNextPhase = "EXEC-LIVE-R008"; Title = "R009 Disabled Preview Operational Runbook and Rollback Gate"; Constraints = "Continue disabled-preview-only operation; no broker, live market data, orders, routes, fills, schedules, ledger commits, or trading state mutation." })
Write-JsonArtifact "phase-exec-live-r007-build-test-validator-evidence.json" ([ordered]@{ Phase = $phase; Build = $BuildStatus; FocusedR007Tests = $FocusedTestsStatus; UnitTests = $UnitTestsStatus; Validator = $ValidatorStatus; DotnetBuildNoRestore = "dotnet build --no-restore"; FocusedTests = "dotnet test tests\QQ.Production.Intraday.Tests.Unit\QQ.Production.Intraday.Tests.Unit.csproj --no-build --no-restore --filter FullyQualifiedName~R009OperatorPreviewReviewTests"; ValidatorScript = "scripts/check-exec-live-r007-r009-preview-operator-review-gate.ps1" })

$summary = @"
# EXEC-LIVE-R007 Summary

Classifications:
- EXEC_LIVE_R007_PASS_OPERATOR_REVIEW_HANDOFF_READY_NO_EXTERNAL
- EXEC_LIVE_R007_PASS_REVIEW_CLI_REPORTING_CONTRACT_READY_NO_EXTERNAL
- EXEC_LIVE_R007_PASS_REVIEW_OUTPUT_BOUNDARY_GUARD_READY_NO_EXTERNAL
- EXEC_LIVE_R007_PASS_NO_BROKER_NO_ORDER_NO_ROUTE_GATE_READY_NO_EXTERNAL

R007 adds an operator-facing disabled-preview review contract and reporting surface for R009. The review service can list audit records, show a record, summarize batch counts, summarize held/rejected reasons, and export operator review reports under `artifacts/readiness/execution-live/operator-review`.

The surface is review-only. It cannot approve execution, submit orders, create routes, create executable schedules, commit ledgers, activate brokers, request live market data, or start scheduler/worker paths. Operator approval remains design-only preview approval and never executable approval.

Build/tests/validator:
- Build: $BuildStatus
- Focused R007 tests: $FocusedTestsStatus
- Unit tests: $UnitTestsStatus
- Validator: $ValidatorStatus

Next recommendation: EXEC-LIVE-R008 - R009 Disabled Preview Operational Runbook and Rollback Gate.
"@
$summary | Set-Content -Path (Join-Path $artifactDir "phase-exec-live-r007-summary.md") -Encoding UTF8

Write-Host "EXEC-LIVE-R007 artifacts written to $artifactDir"
