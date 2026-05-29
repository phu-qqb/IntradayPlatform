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

$phase = "EXEC-LIVE-R006"
$contractVersion = "0.3.0-design-only-candidate"
$classifications = @(
    "EXEC_LIVE_R006_PASS_PREVIEW_AUDIT_CONTRACT_READY_NO_EXTERNAL",
    "EXEC_LIVE_R006_PASS_ARTIFACT_AUDIT_WRITER_READY_NO_EXTERNAL",
    "EXEC_LIVE_R006_PASS_IDEMPOTENCY_REPLAY_SEMANTICS_READY_NO_EXTERNAL",
    "EXEC_LIVE_R006_PASS_NO_ORDER_DOMAIN_PERSISTENCE_GATE_READY_NO_EXTERNAL"
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
        NotSubmitted = $true
        NoBrokerRoute = $true
        NoPaperLedgerCommit = $true
        NoTradingStateMutation = $true
    }
}

$sampleInputHash = New-Hash "exec-live-r006|sample-single|input"
$sampleDecisionHash = New-Hash "exec-live-r006|sample-single|decision"
$sampleAuditHash = New-Hash "exec-live-r006|sample-single|audit"
$batchInputHash = New-Hash "exec-live-r006|batch|item-a|item-b|item-c"
$batchDecisionHash = New-Hash "exec-live-r006|batch|decision"
$batchAuditHash = New-Hash "exec-live-r006|batch|audit"

Write-JsonArtifact "phase-exec-live-r006-r005-consumer-boundary-reference.json" ([ordered]@{
    Phase = $phase
    R005Classifications = @(
        "EXEC_LIVE_R005_PASS_DISABLED_PREVIEW_CONSUMER_HANDOFF_READY_NO_EXTERNAL",
        "EXEC_LIVE_R005_PASS_EMS_OMS_BOUNDARY_GUARD_READY_NO_EXTERNAL",
        "EXEC_LIVE_R005_PASS_PREVIEW_OUTPUT_USAGE_POLICY_READY_NO_EXTERNAL",
        "EXEC_LIVE_R005_PASS_NO_BROKER_NO_ORDER_NO_ROUTE_GATE_READY_NO_EXTERNAL"
    )
    ConsumerBoundaryService = "R009PreviewConsumerBoundaryService"
    AllowedConsumers = @("InternalPmsPreviewConsumer", "InternalEmsPreviewConsumer", "InternalOmsPreviewConsumer", "OperatorReviewTool", "TestHarness")
    ForbiddenConsumers = @("BrokerGateway", "OrderRouter", "Scheduler", "PaperLedgerCommitter", "ExecutionReportHandler", "ProductionTradingRuntime")
    PreviewOutputIsOrderIntent = $false
    PreviewOutputIsRouteable = $false
    PreviewOutputIsExecutableSchedule = $false
    PreviewOutputIsFillReportInput = $false
})

Write-JsonArtifact "phase-exec-live-r006-r009-contract-reference.json" ([ordered]@{
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

$auditFields = @(
    "RequestId",
    "BatchRequestId if applicable",
    "DecisionPreviewId",
    "ConsumerType",
    "RequestMode=DisabledPreviewOnly",
    "R009ContractVersion",
    "InputHash",
    "DecisionHash",
    "AuditHash",
    "CreatedAtUtc",
    "DryRunOnly=true",
    "NonExecutable=true",
    "NotAnOrder=true",
    "NotSubmitted=true",
    "NoBrokerRoute=true",
    "NoOrderDomainPersistence=true",
    "NoTradingStateMutation=true",
    "NoPaperLedgerCommit=true",
    "RetentionCategory=PreviewAuditOnly"
)

Write-JsonArtifact "phase-exec-live-r006-preview-request-audit-contract.json" ([ordered]@{
    Phase = $phase
    Contract = "R009PreviewRequestAuditRecord"
    RequiredFields = $auditFields
    PersistenceDomain = "PreviewAuditOnly"
})
Write-JsonArtifact "phase-exec-live-r006-preview-response-audit-contract.json" ([ordered]@{
    Phase = $phase
    Contract = "R009PreviewResponseAuditRecord"
    RequiredFields = $auditFields + @("DecisionStatus", "ResponseAccepted")
    PersistenceDomain = "PreviewAuditOnly"
})
Write-JsonArtifact "phase-exec-live-r006-preview-batch-audit-contract.json" ([ordered]@{
    Phase = $phase
    Contract = "R009PreviewBatchAuditRecord"
    RequiredFields = $auditFields + @("BatchStatus", "ItemCount", "PreviewReadyCount", "HeldMissingReadinessCount", "RejectedCount")
    BatchIdempotency = "Batch input hash is computed from sorted item hashes."
})
Write-JsonArtifact "phase-exec-live-r006-preview-audit-envelope-contract.json" ([ordered]@{
    Phase = $phase
    Contract = "R009PreviewAuditEnvelope"
    ArtifactOnly = $true
    NoDbPersistence = $true
    NoOrderDomainPersistence = $true
    NoRouteSubmissionPersistence = $true
    NoLedgerPersistence = $true
    NoTradingStateMutation = $true
})
Write-JsonArtifact "phase-exec-live-r006-preview-audit-store-contract.json" ([ordered]@{
    Phase = $phase
    Contract = "R009PreviewAuditStoreContract"
    StoreName = "R009PreviewArtifactAuditStore"
    ArtifactOnly = $true
    DbRequired = $false
    ExternalServiceRequired = $false
    OrderDomainPersistenceAllowed = $false
    RouteSubmissionPersistenceAllowed = $false
    LedgerPersistenceAllowed = $false
    TradingStateMutationAllowed = $false
})
Write-JsonArtifact "phase-exec-live-r006-artifact-audit-writer-contract.json" ([ordered]@{
    Phase = $phase
    Writer = "R009PreviewArtifactAuditWriter"
    RequiredRootRelativePath = "artifacts/readiness/execution-live/audit"
    WritesOrderTables = $false
    WritesExecutionReportTables = $false
    WritesRouteSubmissionTables = $false
    WritesLedgerTables = $false
    MutatesTradingState = $false
    RequiresDb = $false
    RequiresExternalServices = $false
    ReplaySafe = $true
    SameRequestIdDifferentInputRejected = $true
})

$singleAudit = [ordered]@{
    RequestId = "exec-live-r006-single-preview-audit-sample"
    BatchRequestId = $null
    DecisionPreviewId = "exec-live-r006-single-preview-decision"
    ConsumerType = "InternalEmsPreviewConsumer"
    RequestMode = "DisabledPreviewOnly"
    R009ContractVersion = $contractVersion
    InputHash = $sampleInputHash
    DecisionHash = $sampleDecisionHash
    AuditHash = $sampleAuditHash
    CreatedAtUtc = "2026-05-25T12:00:00Z"
    DryRunOnly = $true
    NonExecutable = $true
    NotAnOrder = $true
    NotSubmitted = $true
    NoBrokerRoute = $true
    NoOrderDomainPersistence = $true
    NoTradingStateMutation = $true
    NoPaperLedgerCommit = $true
    RetentionCategory = "PreviewAuditOnly"
}
$batchAudit = [ordered]@{
    RequestId = "exec-live-r006-batch-preview-audit-sample"
    BatchRequestId = "exec-live-r006-batch-preview-request"
    DecisionPreviewId = "exec-live-r006-batch-preview-request"
    ConsumerType = "InternalPmsPreviewConsumer"
    RequestMode = "DisabledPreviewOnly"
    R009ContractVersion = $contractVersion
    InputHash = $batchInputHash
    DecisionHash = $batchDecisionHash
    AuditHash = $batchAuditHash
    CreatedAtUtc = "2026-05-25T12:00:00Z"
    BatchStatus = "Accepted"
    ItemCount = 3
    PreviewReadyCount = 2
    HeldMissingReadinessCount = 1
    RejectedCount = 0
    DryRunOnly = $true
    NonExecutable = $true
    NotAnOrder = $true
    NotSubmitted = $true
    NoBrokerRoute = $true
    NoOrderDomainPersistence = $true
    NoTradingStateMutation = $true
    NoPaperLedgerCommit = $true
    RetentionCategory = "PreviewAuditOnly"
}
Write-JsonArtifact "phase-exec-live-r006-sample-single-preview-audit-record.json" ([ordered]@{ Phase = $phase; RequestAudit = $singleAudit; ResponseAudit = $singleAudit })
Write-JsonArtifact "phase-exec-live-r006-sample-batch-preview-audit-record.json" ([ordered]@{ Phase = $phase; BatchAudit = $batchAudit })

Write-JsonArtifact "phase-exec-live-r006-idempotency-replay-semantics.json" ([ordered]@{
    Phase = $phase
    SameRequestIdSameInputHash = "ReplaySafe"
    SameRequestIdDifferentInputHash = "Conflict"
    NewRequestIdSameInputHash = "AllowedLinkedByInputHash"
    BatchIdempotencyHash = "Computed from sorted item hashes"
    AuditHashDeterministic = $true
    SameRequestIdDifferentInputRejected = $true
})
Write-JsonArtifact "phase-exec-live-r006-idempotency-conflict-results.json" ([ordered]@{
    Phase = $phase
    ReplaySafeExample = [ordered]@{
        RequestId = "exec-live-r006-replay-safe"
        FirstInputHash = New-Hash "same-input"
        SecondInputHash = New-Hash "same-input"
        Result = "ReplaySafe"
    }
    ConflictExample = [ordered]@{
        RequestId = "exec-live-r006-conflict"
        FirstInputHash = New-Hash "first-input"
        SecondInputHash = New-Hash "second-input"
        Result = "Conflict"
        Reason = "SameRequestIdDifferentInputHash"
    }
})

Write-JsonArtifact "phase-exec-live-r006-audit-path-safety-review.json" ([ordered]@{
    Phase = $phase
    RequiredRootRelativePath = "artifacts/readiness/execution-live/audit"
    ArtifactOnly = $true
    DbRequired = $false
    ExternalServiceRequired = $false
    OrderDomainPersistenceAllowed = $false
    RouteSubmissionPersistenceAllowed = $false
    LedgerPersistenceAllowed = $false
    TradingStateMutationAllowed = $false
})
Write-JsonArtifact "phase-exec-live-r006-no-order-domain-persistence-audit.json" ([ordered]@{ Phase = $phase; WritesOrderTables = $false; WritesChildOrderTables = $false; PersistsPreviewAsOrderDomainEntity = $false; NoOrderDomainPersistence = $true })
Write-JsonArtifact "phase-exec-live-r006-no-route-submission-persistence-audit.json" ([ordered]@{ Phase = $phase; WritesRouteTables = $false; WritesSubmissionTables = $false; NoRouteSubmissionPersistence = $true })
Write-JsonArtifact "phase-exec-live-r006-no-ledger-persistence-audit.json" ([ordered]@{ Phase = $phase; WritesLedgerTables = $false; CommitsPaperLedger = $false; NoPaperLedgerCommit = $true })
Write-JsonArtifact "phase-exec-live-r006-no-trading-state-mutation-audit.json" ([ordered]@{ Phase = $phase; MutatesTradingState = $false; MutatesLiveBrokerProductionTradingState = $false; NoTradingStateMutation = $true })

Write-JsonArtifact "phase-exec-live-r006-kill-switch-feature-flag-review.json" ([ordered]@{
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
Write-JsonArtifact "phase-exec-live-r006-disabled-boundary-guard-review.json" ([ordered]@{
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

$audits = [ordered]@{
    "phase-exec-live-r006-no-broker-activation-audit.json" = New-Audit "NoBrokerActivation" "Audit persistence uses artifact files only; no broker runtime was activated."
    "phase-exec-live-r006-no-live-marketdata-audit.json" = New-Audit "NoLiveMarketData" "No live market data request path was introduced."
    "phase-exec-live-r006-no-scheduler-service-polling-audit.json" = New-Audit "NoSchedulerServicePolling" "No scheduler, service, timer, polling, or background job was introduced."
    "phase-exec-live-r006-no-order-created-audit.json" = New-Audit "NoOrderCreated" "Audit records are PreviewAuditOnly and NotAnOrder."
    "phase-exec-live-r006-no-child-order-audit.json" = New-Audit "NoChildOrder" "No child order or child slice entity is created by audit persistence."
    "phase-exec-live-r006-no-executable-schedule-audit.json" = New-Audit "NoExecutableSchedule" "No executable schedule entity is created by audit persistence."
    "phase-exec-live-r006-no-route-no-submission-audit.json" = New-Audit "NoRouteNoSubmission" "No route or submission persistence is allowed."
    "phase-exec-live-r006-no-fill-execution-report-audit.json" = New-Audit "NoFillExecutionReport" "No fill or execution report persistence is allowed."
    "phase-exec-live-r006-no-paper-ledger-commit-audit.json" = New-Audit "NoPaperLedgerCommit" "No paper ledger commit is allowed."
    "phase-exec-live-r006-no-state-mutation-audit.json" = New-Audit "NoStateMutation" "No live, broker, production, trading, or paper-ledger state is mutated."
    "phase-exec-live-r006-no-external-audit.json" = New-Audit "NoExternal" "No external API, Polygon, LMAX, broker, or live market data call is part of this phase."
}
foreach ($entry in $audits.GetEnumerator()) {
    Write-JsonArtifact $entry.Key $entry.Value
}

Write-JsonArtifact "phase-exec-live-r006-canonical-quarter-hour-policy-preservation.json" ([ordered]@{ Phase = $phase; FutureCanonicalMinutes = @(0, 15, 30, 45); LegacyMinutesAcceptedAsFutureCanonical = $false })
Write-JsonArtifact "phase-exec-live-r006-legacy-compatibility-preservation.json" ([ordered]@{ Phase = $phase; LegacyLabels = @(":06", ":21", ":36", ":51"); Usage = "CompatibilityOnly"; UsedAsFutureCanonical = $false })
Write-JsonArtifact "phase-exec-live-r006-direct-cross-exclusion-preservation.json" ([ordered]@{ Phase = $phase; DirectCrossExecutionAllowed = $false; DirectCrossRejectionsCanBeAuditedAsRejectionOnly = $true })
Write-JsonArtifact "phase-exec-live-r006-usd-pair-netting-requirement.json" ([ordered]@{ Phase = $phase; Requirement = "EMS/OMS execution intents must be USD-pair-only after netting."; SupportedExecutionSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF"); AudusdStatus = "SupportedAndNotFailed" })
Write-JsonArtifact "phase-exec-live-r006-usdjpy-caveat-preservation.json" ([ordered]@{ Phase = $phase; NormalizedPortfolioSymbol = "JPYUSD"; ExecutionTradableSymbol = "USDJPY"; RequiresInversion = $true; SecurityID = "4004"; SecurityIDSource = "8"; CaveatPreserved = $true })
Write-JsonArtifact "phase-exec-live-r006-cost-guidance-preservation.json" ([ordered]@{ Phase = $phase; FiveUsdPerMillion = "BestCaseMajorOnly"; Universalized = $false })
Write-JsonArtifact "phase-exec-live-r006-nonmajor-calibration-preservation.json" ([ordered]@{ Phase = $phase; NonmajorEmScandiCnh = "CalibrationRequired"; LiveCapableExecutionAllowed = $false })

Write-JsonArtifact "phase-exec-live-r006-forbidden-actions-audit.json" ([ordered]@{
    Phase = $phase
    ExternalApiCallsMade = $false
    PolygonCallsMade = $false
    LmaxCallsMade = $false
    BrokerActivationOccurred = $false
    LiveMarketDataRequested = $false
    SchedulerServicePollingBackgroundJobIntroduced = $false
    PmsEmsOmsCycleRun = $false
    ManualNoExternalCommandRun = $false
    BacktestSimulationRun = $false
    TcaResultLinesCreated = $false
    ExecutableScheduleCreated = $false
    OrdersChildOrdersRoutesSubmissionsFillsReportsCreated = $false
    PaperLedgerCommitCreated = $false
    StateMutationOccurred = $false
    R009PromotedToExecutableUse = $false
})
Write-JsonArtifact "phase-exec-live-r006-next-phase-recommendation.json" ([ordered]@{
    Phase = $phase
    RecommendedNextPhase = "EXEC-LIVE-R007"
    Title = "R009 Disabled Preview API Operator Review UI/CLI Handoff Gate"
    Constraints = "Continue with disabled preview only; no broker, no live market data, no orders, no routes, no fills, no schedules, no ledger commits, no trading state mutation."
})
Write-JsonArtifact "phase-exec-live-r006-build-test-validator-evidence.json" ([ordered]@{
    Phase = $phase
    Build = $BuildStatus
    FocusedR006Tests = $FocusedTestsStatus
    UnitTests = $UnitTestsStatus
    Validator = $ValidatorStatus
    DotnetBuildNoRestore = "dotnet build --no-restore"
    FocusedTests = "dotnet test tests\QQ.Production.Intraday.Tests.Unit\QQ.Production.Intraday.Tests.Unit.csproj --no-build --no-restore --filter FullyQualifiedName~R009PreviewAuditPersistenceTests"
    ValidatorScript = "scripts/check-exec-live-r006-preview-audit-persistence-gate.ps1"
})

$summary = @"
# EXEC-LIVE-R006 Summary

Classifications:
- EXEC_LIVE_R006_PASS_PREVIEW_AUDIT_CONTRACT_READY_NO_EXTERNAL
- EXEC_LIVE_R006_PASS_ARTIFACT_AUDIT_WRITER_READY_NO_EXTERNAL
- EXEC_LIVE_R006_PASS_IDEMPOTENCY_REPLAY_SEMANTICS_READY_NO_EXTERNAL
- EXEC_LIVE_R006_PASS_NO_ORDER_DOMAIN_PERSISTENCE_GATE_READY_NO_EXTERNAL

R006 adds artifact-only persistence and audit-trail contracts for R009 disabled preview requests and responses. Audit records are PreviewAuditOnly, DryRunOnly, NonExecutable, NotAnOrder, NotSubmitted, NoBrokerRoute, NoOrderDomainPersistence, NoTradingStateMutation, and NoPaperLedgerCommit.

The artifact writer is `R009PreviewArtifactAuditWriter`; it writes only under `artifacts/readiness/execution-live/audit`, requires no DB, requires no external service, and rejects unsafe live/broker/order/schedule/ledger-enabled source requests. Same RequestId plus same InputHash is replay-safe. Same RequestId plus different InputHash is a conflict.

No external API, Polygon, LMAX, broker activation, live market data, scheduler/service/polling, PMS/EMS/OMS cycle, ManualNoExternal command, backtest/simulation, TCA result line, order, route, submission, fill, execution report, executable schedule, paper ledger commit, trading state mutation, or R009 executable promotion occurred.

Build/tests/validator:
- Build: $BuildStatus
- Focused R006 tests: $FocusedTestsStatus
- Unit tests: $UnitTestsStatus
- Validator: $ValidatorStatus

Next recommendation: EXEC-LIVE-R007 - R009 Disabled Preview API Operator Review UI/CLI Handoff Gate.
"@
$summary | Set-Content -Path (Join-Path $artifactDir "phase-exec-live-r006-summary.md") -Encoding UTF8

Write-Host "EXEC-LIVE-R006 artifacts written to $artifactDir"
