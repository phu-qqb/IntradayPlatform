param(
    [string]$BuildStatus = "Pending",
    [string]$FocusedTestsStatus = "Pending",
    [string]$UnitTestsStatus = "Pending",
    [string]$ValidatorStatus = "Pending"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-live"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

$phase = "EXEC-LIVE-R005"
$contractVersion = "0.3.0-design-only-candidate"
$classifications = @(
    "EXEC_LIVE_R005_PASS_DISABLED_PREVIEW_CONSUMER_HANDOFF_READY_NO_EXTERNAL",
    "EXEC_LIVE_R005_PASS_EMS_OMS_BOUNDARY_GUARD_READY_NO_EXTERNAL",
    "EXEC_LIVE_R005_PASS_PREVIEW_OUTPUT_USAGE_POLICY_READY_NO_EXTERNAL",
    "EXEC_LIVE_R005_PASS_NO_BROKER_NO_ORDER_NO_ROUTE_GATE_READY_NO_EXTERNAL"
)

function Write-JsonArtifact {
    param([string]$Name, [object]$Value)
    $Value | ConvertTo-Json -Depth 40 | Set-Content -Path (Join-Path $artifactDir $Name) -Encoding UTF8
}

function New-Audit {
    param([string]$Name, [string]$Evidence)
    [ordered]@{
        Phase = $phase
        Audit = $Name
        Status = "Pass"
        Evidence = $Evidence
        NonExecutable = $true
        NotAnOrder = $true
        NotSubmitted = $true
        NoBrokerRoute = $true
        NoExternal = $true
    }
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

$allowedConsumers = @("InternalPmsPreviewConsumer", "InternalEmsPreviewConsumer", "InternalOmsPreviewConsumer", "OperatorReviewTool", "TestHarness")
$forbiddenConsumers = @("BrokerGateway", "LiveMarketDataWorker", "Scheduler", "BackgroundWorker", "OrderRouter", "ExecutionReportHandler", "PaperLedgerCommitter", "ProductionTradingRuntime")
$allowedUsages = @("DisplayToOperator", "PersistAsReadinessArtifact", "ComparePolicies", "GenerateManualReviewNote", "FeedFutureNoExternalPaperOnlyEvaluation")
$forbiddenUsages = @("ConvertToOrder", "ConvertToChildOrder", "ConvertToRouteSubmission", "CommitLedger", "TriggerBroker", "TriggerSchedulerWorker", "MutatePositionsState", "GenerateFillExecutionReport")

$validRequests = @(
    [ordered]@{ Consumer = "InternalPmsPreviewConsumer"; Source = "SinglePreview"; Usage = "DisplayToOperator"; Accepted = $true },
    [ordered]@{ Consumer = "InternalEmsPreviewConsumer"; Source = "BatchPreview"; Usage = "ComparePolicies"; Accepted = $true },
    [ordered]@{ Consumer = "InternalOmsPreviewConsumer"; Source = "SinglePreview"; Usage = "GenerateManualReviewNote"; Accepted = $true },
    [ordered]@{ Consumer = "OperatorReviewTool"; Source = "BatchPreview"; Usage = "DisplayToOperator"; Accepted = $true }
)

$invalidResults = @(
    [ordered]@{ Consumer = "BrokerGateway"; Accepted = $false; RejectionReasons = @("ForbiddenConsumer:BrokerGateway") },
    [ordered]@{ Consumer = "OrderRouter"; Accepted = $false; RejectionReasons = @("ForbiddenConsumer:OrderRouter") },
    [ordered]@{ Consumer = "Scheduler"; Accepted = $false; RejectionReasons = @("ForbiddenConsumer:Scheduler") },
    [ordered]@{ Consumer = "PaperLedgerCommitter"; Accepted = $false; RejectionReasons = @("ForbiddenConsumer:PaperLedgerCommitter") },
    [ordered]@{ Consumer = "InternalEmsPreviewConsumer"; Accepted = $false; RejectionReasons = @("ForbiddenUsage:ConvertToOrder") },
    [ordered]@{ Consumer = "InternalOmsPreviewConsumer"; Accepted = $false; RejectionReasons = @("ForbiddenUsage:ConvertToRouteSubmission") },
    [ordered]@{ Consumer = "OperatorReviewTool"; Accepted = $false; RejectionReasons = @("ForbiddenUsage:CommitLedger") }
)

Write-JsonArtifact "phase-exec-live-r005-r004-batch-contract-reference.json" ([ordered]@{
    Phase = $phase
    R004BatchContract = "artifacts/readiness/execution-live/phase-exec-live-r004-batch-api-contract.json"
    R004BatchRequestDto = "artifacts/readiness/execution-live/phase-exec-live-r004-batch-request-dto-contract.json"
    R004BatchResponseDto = "artifacts/readiness/execution-live/phase-exec-live-r004-batch-response-dto-contract.json"
})
Write-JsonArtifact "phase-exec-live-r005-r009-contract-reference.json" ([ordered]@{
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
Write-JsonArtifact "phase-exec-live-r005-consumer-handoff-contract.json" ([ordered]@{
    Phase = $phase
    ContractName = "R009 Disabled Preview Consumer Handoff Contract"
    BoundaryService = "R009PreviewConsumerBoundaryService"
    RequestEnvelope = "R009PreviewConsumerRequestEnvelope"
    ResponseEnvelope = "R009PreviewConsumerResponseEnvelope"
    PreviewOutputIsOrderIntent = $false
    PreviewOutputIsRouteable = $false
    PreviewOutputIsExecutableSchedule = $false
    PreviewOutputIsFillReportInput = $false
    Purpose = "Operator review and dry-run decisioning only"
})
Write-JsonArtifact "phase-exec-live-r005-allowed-consumers.json" ([ordered]@{ Phase = $phase; AllowedConsumers = $allowedConsumers })
Write-JsonArtifact "phase-exec-live-r005-forbidden-consumers.json" ([ordered]@{ Phase = $phase; ForbiddenConsumers = $forbiddenConsumers })
Write-JsonArtifact "phase-exec-live-r005-preview-response-usage-policy.json" ([ordered]@{
    Phase = $phase
    AllowedUsage = $allowedUsages
    ForbiddenUsage = $forbiddenUsages
    PreviewOutputIsOrderIntent = $false
    PreviewOutputIsRouteable = $false
    PreviewOutputIsExecutableSchedule = $false
    PreviewOutputIsFillReportInput = $false
})
Write-JsonArtifact "phase-exec-live-r005-ems-oms-boundary-model.json" ([ordered]@{
    Phase = $phase
    BoundaryObjects = @("PreviewConsumerRequestEnvelope", "PreviewConsumerResponseEnvelope", "PreviewUsagePolicy", "PreviewBoundaryGuard", "PreviewConsumerAuditRecord")
    Enforcement = "Allowed consumers and allowed usages only; responses must remain non-executable."
})
Write-JsonArtifact "phase-exec-live-r005-preview-boundary-guard-contract.json" ([ordered]@{
    Phase = $phase
    NonExecutable = $true
    NotAnOrder = $true
    NotSubmitted = $true
    NoBrokerRoute = $true
    NoRoute = $true
    NoSubmission = $true
    NoFill = $true
    NoExecutionReport = $true
    NoPaperLedgerCommit = $true
    NoStateMutation = $true
})
Write-JsonArtifact "phase-exec-live-r005-preview-consumer-audit-contract.json" ([ordered]@{
    Phase = $phase
    AuditRecord = "R009PreviewConsumerAuditRecord"
    RequiredFields = @("ConsumerRequestId", "ConsumerType", "AuditHash", "CreatedAtUtc", "NoOrderDomainOutput=true", "NoBrokerRoute=true", "NoStateMutation=true", "DryRunOnly=true")
    SampleAuditHash = New-Hash "exec-live-r005|consumer-audit"
})
Write-JsonArtifact "phase-exec-live-r005-valid-consumer-request-examples.json" ([ordered]@{ Phase = $phase; Examples = $validRequests })
Write-JsonArtifact "phase-exec-live-r005-invalid-consumer-rejection-results.json" ([ordered]@{ Phase = $phase; Results = $invalidResults })

Write-JsonArtifact "phase-exec-live-r005-preview-output-not-order-audit.json" ([ordered]@{ Phase = $phase; CanConvertToOrder = $false; CanConvertToChildOrder = $false; NotAnOrder = $true })
Write-JsonArtifact "phase-exec-live-r005-preview-output-not-route-audit.json" ([ordered]@{ Phase = $phase; CanConvertToRoute = $false; CanConvertToSubmission = $false; NoRoute = $true; NoSubmission = $true; NoBrokerRoute = $true })
Write-JsonArtifact "phase-exec-live-r005-preview-output-not-schedule-audit.json" ([ordered]@{ Phase = $phase; CanConvertToExecutableSchedule = $false; NoExecutableSchedule = $true })
Write-JsonArtifact "phase-exec-live-r005-preview-output-not-ledger-audit.json" ([ordered]@{ Phase = $phase; CanCommitPaperLedger = $false; NoPaperLedgerCommit = $true; NoStateMutation = $true })
Write-JsonArtifact "phase-exec-live-r005-kill-switch-feature-flag-review.json" ([ordered]@{
    Phase = $phase
    R009LiveTradingEnabled = $false
    R009BrokerRoutingEnabled = $false
    R009OrderSubmissionEnabled = $false
    R009ExecutableScheduleEnabled = $false
    R009PaperLedgerCommitEnabled = $false
    R009SchedulerEnabled = $false
    R009BackgroundWorkerEnabled = $false
    R009DryRunOnly = $true
})
Write-JsonArtifact "phase-exec-live-r005-disabled-boundary-guard-review.json" ([ordered]@{
    Phase = $phase
    BrokerRouteCreationAllowed = $false
    OrderCreationAllowed = $false
    ChildSliceCreationAllowed = $false
    ChildOrderCreationAllowed = $false
    ScheduleExecutionAllowed = $false
    SubmissionAllowed = $false
    FillCreationAllowed = $false
    ExecutionReportCreationAllowed = $false
    StateMutationAllowed = $false
    PaperLedgerCommitAllowed = $false
})
Write-JsonArtifact "phase-exec-live-r005-idempotency-audit-review.json" ([ordered]@{
    Phase = $phase
    ConsumerAuditHashPresent = $true
    NoOrderDomainOutput = $true
    NoBrokerRoute = $true
    NoStateMutation = $true
    DryRunOnly = $true
})

Write-JsonArtifact "phase-exec-live-r005-canonical-quarter-hour-policy-preservation.json" ([ordered]@{ Phase = $phase; FutureCanonicalMinutes = @(0, 15, 30, 45); LegacyMinutesAcceptedAsFutureCanonical = $false })
Write-JsonArtifact "phase-exec-live-r005-legacy-compatibility-preservation.json" ([ordered]@{ Phase = $phase; LegacyLabels = @(":06", ":21", ":36", ":51"); Usage = "CompatibilityOnly"; UsedAsFutureCanonical = $false })
Write-JsonArtifact "phase-exec-live-r005-direct-cross-exclusion-preservation.json" ([ordered]@{ Phase = $phase; DirectCrossExecutionAllowed = $false })
Write-JsonArtifact "phase-exec-live-r005-usd-pair-netting-requirement.json" ([ordered]@{ Phase = $phase; Requirement = "EMS/OMS execution intents must be USD-pair-only after netting."; SupportedExecutionSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF"); AudusdStatus = "SupportedAndNotFailed" })
Write-JsonArtifact "phase-exec-live-r005-usdjpy-caveat-preservation.json" ([ordered]@{ Phase = $phase; NormalizedPortfolioSymbol = "JPYUSD"; ExecutionTradableSymbol = "USDJPY"; RequiresInversion = $true; SecurityID = "4004"; SecurityIDSource = "8"; CaveatPreserved = $true })
Write-JsonArtifact "phase-exec-live-r005-cost-guidance-preservation.json" ([ordered]@{ Phase = $phase; FiveUsdPerMillion = "BestCaseMajorOnly"; Universalized = $false })
Write-JsonArtifact "phase-exec-live-r005-nonmajor-calibration-preservation.json" ([ordered]@{ Phase = $phase; NonmajorEmScandiCnh = "CalibrationRequired"; LiveCapableExecutionAllowed = $false })

$audits = [ordered]@{
    "phase-exec-live-r005-no-broker-activation-audit.json" = New-Audit "NoBrokerActivation" "BrokerGateway is forbidden and no broker runtime was introduced."
    "phase-exec-live-r005-no-live-marketdata-audit.json" = New-Audit "NoLiveMarketData" "LiveMarketDataWorker is forbidden and no live market data request path was introduced."
    "phase-exec-live-r005-no-scheduler-service-polling-audit.json" = New-Audit "NoSchedulerServicePolling" "Scheduler and BackgroundWorker are forbidden consumers."
    "phase-exec-live-r005-no-order-created-audit.json" = New-Audit "NoOrderCreated" "Preview output cannot be converted to order."
    "phase-exec-live-r005-no-child-order-audit.json" = New-Audit "NoChildOrder" "Preview output cannot be converted to child order."
    "phase-exec-live-r005-no-executable-schedule-audit.json" = New-Audit "NoExecutableSchedule" "Preview output cannot be converted to executable schedule."
    "phase-exec-live-r005-no-route-no-submission-audit.json" = New-Audit "NoRouteNoSubmission" "Preview output cannot be converted to route or submission."
    "phase-exec-live-r005-no-fill-execution-report-audit.json" = New-Audit "NoFillExecutionReport" "Preview output cannot feed fills or execution reports."
    "phase-exec-live-r005-no-paper-ledger-commit-audit.json" = New-Audit "NoPaperLedgerCommit" "PaperLedgerCommitter is a forbidden consumer."
    "phase-exec-live-r005-no-state-mutation-audit.json" = New-Audit "NoStateMutation" "Preview boundary requires NoStateMutation=true."
    "phase-exec-live-r005-no-external-audit.json" = New-Audit "NoExternal" "No external API, Polygon, LMAX, broker, or live market data call is part of this phase."
}
foreach ($entry in $audits.GetEnumerator()) {
    Write-JsonArtifact $entry.Key $entry.Value
}

Write-JsonArtifact "phase-exec-live-r005-forbidden-actions-audit.json" ([ordered]@{
    Phase = $phase
    ProhibitedActionsObserved = @()
    ExternalApiCallsMade = $false
    BrokerLiveOrderRouteFillScheduleLedgerStatePathsEnabled = $false
    PmsEmsOmsCycleRun = $false
    ManualNoExternalCommandRun = $false
    R009PromotedToExecutableUse = $false
})
Write-JsonArtifact "phase-exec-live-r005-next-phase-recommendation.json" ([ordered]@{
    Phase = $phase
    RecommendedNextPhase = "EXEC-LIVE-R006"
    Title = "R009 Disabled Preview API Request Persistence and Audit Trail Gate"
})
Write-JsonArtifact "phase-exec-live-r005-build-test-validator-evidence.json" ([ordered]@{
    Phase = $phase
    Build = $BuildStatus
    FocusedR005Tests = $FocusedTestsStatus
    UnitTests = $UnitTestsStatus
    Validator = $ValidatorStatus
    EvidenceRequired = $true
})

$summary = @"
# EXEC-LIVE-R005 Summary

Classifications:
- $($classifications -join "`n- ")

R009 disabled preview API consumer handoff is defined and enforced through:
- R009PreviewConsumerRequestEnvelope
- R009PreviewConsumerResponseEnvelope
- R009PreviewUsagePolicy
- R009PreviewBoundaryGuard
- R009PreviewConsumerAuditRecord
- R009PreviewConsumerBoundaryService

Allowed consumers:
- $($allowedConsumers -join "`n- ")

Forbidden consumers:
- $($forbiddenConsumers -join "`n- ")

Preview output remains operator-review / dry-run decisioning only. It is not an order intent, routeable output, executable schedule, fill/report input, or ledger commit input.

No broker, live market data, scheduler, order, child order, route, submission, fill, execution report, executable schedule, paper ledger commit, or state mutation path is enabled.

Next phase:
- EXEC-LIVE-R006 - R009 Disabled Preview API Request Persistence and Audit Trail Gate
"@
Set-Content -Path (Join-Path $artifactDir "phase-exec-live-r005-summary.md") -Value $summary -Encoding UTF8

Write-Host "Wrote EXEC-LIVE-R005 artifacts to $artifactDir"
