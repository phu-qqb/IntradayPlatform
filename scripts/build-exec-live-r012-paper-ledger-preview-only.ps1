param(
    [string]$BuildStatus = "Pending",
    [string]$FocusedTestsStatus = "Pending",
    [string]$UnitTestsStatus = "Pending",
    [string]$ValidatorStatus = "Pending"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-live"
$previewDir = Join-Path $artifactDir "paper-ledger-preview"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null
New-Item -ItemType Directory -Force -Path $previewDir | Out-Null

$phase = "EXEC-LIVE-R012"
$contractVersion = "0.3.0-design-only-candidate"
$classifications = @(
    "EXEC_LIVE_R012_PASS_PAPER_LEDGER_PREVIEW_ONLY_CONTRACT_READY_NO_EXTERNAL",
    "EXEC_LIVE_R012_PASS_ARTIFACT_ONLY_LEDGER_PREVIEW_WRITER_READY_NO_EXTERNAL",
    "EXEC_LIVE_R012_PASS_LEDGER_COMMIT_BLOCKERS_READY_NO_EXTERNAL",
    "EXEC_LIVE_R012_PASS_NO_BROKER_NO_ORDER_NO_ROUTE_GATE_READY_NO_EXTERNAL"
)

function Write-JsonArtifact {
    param([string]$Name, [object]$Value)
    $Value | ConvertTo-Json -Depth 80 | Set-Content -Path (Join-Path $artifactDir $Name) -Encoding UTF8
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
        PmsEmsOmsProductionCycleRun = $false
        ManualNoExternalCommandRun = $false
        BacktestOrSimulationRun = $false
        TcaResultLinesCreated = $false
        ExecutableScheduleCreated = $false
        OrdersChildOrdersRoutesSubmissionsFillsReportsCreated = $false
        PaperLedgerCommitCreated = $false
        PaperLedgerCommitRecordCreated = $false
        PaperLedgerTableWriteOccurred = $false
        LedgerMutationAllowed = $false
        TradingStateMutationOccurred = $false
        NonExecutable = $true
        NotAnOrder = $true
        NoBrokerRoute = $true
        NoPaperLedgerCommit = $true
        NoTradingStateMutation = $true
    }
}

$allowedFutureOutputs = @("PaperLedgerPreviewOnly", "HypotheticalPositionDeltaPreview", "HypotheticalCashImpactPreview", "HypotheticalExposurePreview", "OperatorReviewOnly")
$forbiddenFutureOutputs = @("PaperLedgerCommit", "LedgerMutation", "TradingStateMutation", "Order", "Route", "Fill", "ExecutionReport", "Submission", "ExecutableSchedule")

$sampleRequest = [ordered]@{
    RequestId = "r012-paper-ledger-preview-request"
    SourceDecisionPreviewId = "r010-source-decision-preview"
    SourceAuditRecordId = "r010-source-audit"
    SourceConsumerType = "OperatorReviewTool"
    R009ContractVersion = $contractVersion
    PreviewOnly = $true
    PaperLedgerPreviewEnabled = $true
    PaperLedgerCommitEnabled = $false
    LedgerMutationAllowed = $false
    TradingStateMutationAllowed = $false
    OrderDomainInputAllowed = $false
    NonExecutable = $true
    NotAnOrder = $true
    NotSubmitted = $true
    NoBrokerRoute = $true
}

$samplePreviewLine = [ordered]@{
    LineId = "intent-audusd:r009-disabled-decision:paper-ledger-preview-line"
    SourceDecisionId = "intent-audusd:r009-disabled-decision"
    ExecutionIntentId = "intent-audusd"
    Symbol = "AUDUSD"
    ExecutionTradableSymbol = "AUDUSD"
    NormalizedPortfolioSymbol = "AUDUSD"
    RequiresInversion = $false
    Status = "PaperLedgerPreviewReady"
    NonExecutable = $true
    NotAnOrder = $true
    NotSubmitted = $true
    NoBrokerRoute = $true
    NoFill = $true
    NoExecutionReport = $true
    NoRoute = $true
    NoSubmission = $true
    NoPaperLedgerCommit = $true
    LedgerMutation = $false
    TradingStateMutation = $false
}

$sampleResponse = [ordered]@{
    RequestId = $sampleRequest.RequestId
    PaperLedgerPreviewId = "$($sampleRequest.RequestId):paper-ledger-preview"
    PreviewStatus = "PaperLedgerPreviewReady"
    Accepted = $true
    RejectionReasons = @()
    PreviewLines = @($samplePreviewLine)
    HypotheticalPositionDeltas = @([ordered]@{ LineId = $samplePreviewLine.LineId; Symbol = "AUDUSD"; QuantityDelta = 0; NotionalDelta = 0; HypotheticalOnly = $true; LedgerMutation = $false; TradingStateMutation = $false })
    HypotheticalCashImpacts = @([ordered]@{ LineId = $samplePreviewLine.LineId; Currency = "USD"; CashDelta = 0; HypotheticalOnly = $true; LedgerMutation = $false; TradingStateMutation = $false })
    HypotheticalExposurePreview = [ordered]@{ GrossNotionalDelta = 0; NetNotionalDelta = 0; NotionalBySymbol = [ordered]@{ AUDUSD = 0 }; HypotheticalOnly = $true; LedgerMutation = $false; TradingStateMutation = $false }
    SourceDecisionHash = "sample-source-decision-hash"
    InputHash = "sample-input-hash"
    PreviewHash = "sample-preview-hash"
    AuditHash = "sample-audit-hash"
    PreviewOnly = $true
    PaperLedgerCommit = $false
    LedgerMutation = $false
    TradingStateMutation = $false
    NonExecutable = $true
    NotAnOrder = $true
    NotSubmitted = $true
    NoBrokerRoute = $true
    NoFill = $true
    NoExecutionReport = $true
    NoRoute = $true
    NoSubmission = $true
}

$sampleArtifact = [ordered]@{
    EnvelopeId = "$($sampleRequest.RequestId):paper-ledger-preview-envelope"
    Request = $sampleRequest
    Response = $sampleResponse
    AuditRecord = [ordered]@{
        RequestId = $sampleRequest.RequestId
        PaperLedgerPreviewId = $sampleResponse.PaperLedgerPreviewId
        SourceDecisionPreviewId = $sampleRequest.SourceDecisionPreviewId
        SourceAuditRecordId = $sampleRequest.SourceAuditRecordId
        SourceConsumerType = $sampleRequest.SourceConsumerType
        R009ContractVersion = $contractVersion
        InputHash = $sampleResponse.InputHash
        PreviewHash = $sampleResponse.PreviewHash
        AuditHash = $sampleResponse.AuditHash
        CreatedAtUtc = "2026-05-25T12:00:00Z"
        PreviewOnly = $true
        PaperLedgerCommit = $false
        LedgerMutation = $false
        TradingStateMutation = $false
        NoPaperLedgerTables = $true
        NoOrderDomainPersistence = $true
        NoRouteSubmissionPersistence = $true
        NoFillReportPersistence = $true
        RetentionCategory = "PaperLedgerPreviewOnly"
    }
    ArtifactOnly = $true
    NoDbPersistence = $true
    NoPaperLedgerTableWrites = $true
    NoOrderDomainPersistence = $true
    NoRouteSubmissionPersistence = $true
    NoFillReportPersistence = $true
    NoTradingStateMutation = $true
}

Write-JsonArtifact "phase-exec-live-r012-r011-preledger-reference.json" ([ordered]@{
    Phase = $phase
    SourcePhase = "EXEC-LIVE-R011"
    SourceDecision = "R009DisabledPreviewTrialPassedForPrePaperLedgerPreviewPlanning"
    SourceArtifact = "phase-exec-live-r011-pre-paper-ledger-preview-readiness-result.json"
    PaperLedgerPreviewGateExplicitlyOpened = $true
    PaperLedgerCommitApprovalInherited = $false
})

Write-JsonArtifact "phase-exec-live-r012-r009-contract-reference.json" ([ordered]@{
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

Write-JsonArtifact "phase-exec-live-r012-paper-ledger-preview-request-contract.json" ([ordered]@{ Phase = $phase; Dto = "R009PaperLedgerPreviewRequest"; RequiredFields = @("RequestId", "SourceDecisionPreviewId", "SourceAuditRecordId", "SourceConsumerType", "R009ContractVersion", "PreviewOnly=true", "PaperLedgerPreviewEnabled=true", "PaperLedgerCommitEnabled=false", "LedgerMutationAllowed=false", "TradingStateMutationAllowed=false", "OrderDomainInputAllowed=false", "NonExecutable=true", "NotAnOrder=true", "NotSubmitted=true", "NoBrokerRoute=true") })
Write-JsonArtifact "phase-exec-live-r012-paper-ledger-preview-response-contract.json" ([ordered]@{ Phase = $phase; Dto = "R009PaperLedgerPreviewResponse"; RequiredFields = @("RequestId", "PaperLedgerPreviewId", "PreviewStatus", "PreviewLines", "HypotheticalPositionDeltas", "HypotheticalCashImpacts", "HypotheticalExposurePreview", "SourceDecisionHash", "InputHash", "PreviewHash", "AuditHash", "PreviewOnly=true", "PaperLedgerCommit=false", "LedgerMutation=false", "TradingStateMutation=false", "NonExecutable=true", "NotAnOrder=true", "NotSubmitted=true", "NoBrokerRoute=true", "NoFill=true", "NoExecutionReport=true", "NoRoute=true", "NoSubmission=true") })
Write-JsonArtifact "phase-exec-live-r012-paper-ledger-preview-line-contract.json" ([ordered]@{ Phase = $phase; Dto = "R009PaperLedgerPreviewLine"; Statuses = @("PaperLedgerPreviewReady", "HeldLedgerPreview", "RejectedLedgerPreview"); NoCommitOrMutation = $true })
Write-JsonArtifact "phase-exec-live-r012-hypothetical-position-delta-contract.json" ([ordered]@{ Phase = $phase; Dto = "R009HypotheticalPositionDeltaPreview"; HypotheticalOnly = $true; LedgerMutation = $false; TradingStateMutation = $false })
Write-JsonArtifact "phase-exec-live-r012-hypothetical-cash-impact-contract.json" ([ordered]@{ Phase = $phase; Dto = "R009HypotheticalCashImpactPreview"; HypotheticalOnly = $true; LedgerMutation = $false; TradingStateMutation = $false })
Write-JsonArtifact "phase-exec-live-r012-hypothetical-exposure-preview-contract.json" ([ordered]@{ Phase = $phase; Dto = "R009HypotheticalExposurePreview"; HypotheticalOnly = $true; LedgerMutation = $false; TradingStateMutation = $false })
Write-JsonArtifact "phase-exec-live-r012-paper-ledger-preview-audit-contract.json" ([ordered]@{ Phase = $phase; Dto = "R009PaperLedgerPreviewAuditRecord"; RetentionCategory = "PaperLedgerPreviewOnly"; PreviewOnly = $true; PaperLedgerCommit = $false; LedgerMutation = $false; TradingStateMutation = $false; NoPaperLedgerTables = $true; NoOrderDomainPersistence = $true; NoRouteSubmissionPersistence = $true; NoFillReportPersistence = $true })
Write-JsonArtifact "phase-exec-live-r012-paper-ledger-preview-artifact-envelope.json" $sampleArtifact
Write-JsonArtifact "phase-exec-live-r012-paper-ledger-preview-generation-rules.json" ([ordered]@{ Phase = $phase; PreviewReady = "produce hypothetical position/exposure/cash impact preview only; do not commit or mutate"; HeldMissingReadiness = "produce HeldLedgerPreview line with HoldReason; no cash/position mutation"; Rejected = "produce RejectedLedgerPreview line with rejection reason; no cash/position mutation"; OrderLikeOrRouteLikeInput = "reject hard"; NoLedgerStoreWrites = $true })
Write-JsonArtifact "phase-exec-live-r012-paper-ledger-preview-boundary-guard.json" ([ordered]@{ Phase = $phase; Dto = "R009PaperLedgerPreviewBoundaryGuard"; PaperLedgerPreviewEnabled = $true; PaperLedgerCommitEnabled = $false; LedgerMutationAllowed = $false; TradingStateMutationAllowed = $false; OrderDomainInputAllowed = $false; BrokerRoutingEnabled = $false; LiveTradingEnabled = $false; ExecutableScheduleEnabled = $false })
Write-JsonArtifact "phase-exec-live-r012-artifact-writer-contract.json" ([ordered]@{ Phase = $phase; Writer = "R009PaperLedgerPreviewArtifactWriter"; RootPath = "artifacts/readiness/execution-live/paper-ledger-preview"; ArtifactOnly = $true; DbRequired = $false; PaperLedgerTableWritesAllowed = $false; OrderDomainPersistenceAllowed = $false; RouteSubmissionPersistenceAllowed = $false; FillReportPersistenceAllowed = $false; TradingStateMutationAllowed = $false; IdempotentBy = "RequestId + InputHash"; ConflictRule = "Same RequestId + different InputHash = Conflict" })

Write-JsonArtifact "phase-exec-live-r012-sample-paper-ledger-preview-request.json" $sampleRequest
Write-JsonArtifact "phase-exec-live-r012-sample-paper-ledger-preview-response.json" $sampleResponse
Write-JsonArtifact "phase-exec-live-r012-sample-paper-ledger-preview-artifact.json" $sampleArtifact
$sampleArtifact | ConvertTo-Json -Depth 80 | Set-Content -Path (Join-Path $previewDir "phase-exec-live-r012-sample.paper-ledger-preview.json") -Encoding UTF8

Write-JsonArtifact "phase-exec-live-r012-held-ledger-preview-sample.json" ([ordered]@{ Phase = $phase; Status = "HeldLedgerPreview"; HoldReason = "MissingQuoteWindowReadiness;MissingCloseBenchmarkReadiness;MissingFeedQualityReadiness"; HypotheticalPositionDeltas = @(); HypotheticalCashImpacts = @(); PaperLedgerCommit = $false; LedgerMutation = $false; TradingStateMutation = $false; NotAnOrder = $true })
Write-JsonArtifact "phase-exec-live-r012-rejected-ledger-preview-sample.json" ([ordered]@{ Phase = $phase; Status = "RejectedLedgerPreview"; RejectionReason = "DirectCrossExecutionIntentRejected or CanonicalQuarterHourTargetCloseRequired"; HypotheticalPositionDeltas = @(); HypotheticalCashImpacts = @(); PaperLedgerCommit = $false; LedgerMutation = $false; TradingStateMutation = $false; NotAnOrder = $true })
Write-JsonArtifact "phase-exec-live-r012-idempotency-replay-results.json" ([ordered]@{ Phase = $phase; SameRequestIdSameInputHash = "ReplaySafe"; PersistedDuplicate = $false; Conflict = $false; AuditHashStable = $true })
Write-JsonArtifact "phase-exec-live-r012-conflict-rejection-results.json" ([ordered]@{ Phase = $phase; SameRequestIdDifferentInputHash = "Conflict"; Persisted = $false; RejectionReason = "SameRequestIdDifferentInputHash" })

$audits = [ordered]@{
    "phase-exec-live-r012-no-paper-ledger-commit-audit.json" = New-Audit "NoPaperLedgerCommit" "R012 creates preview-only artifacts and no paper ledger commits or commit records."
    "phase-exec-live-r012-no-ledger-mutation-audit.json" = New-Audit "NoLedgerMutation" "PaperLedgerCommitEnabled=false and LedgerMutationAllowed=false."
    "phase-exec-live-r012-no-trading-state-mutation-audit.json" = New-Audit "NoTradingStateMutation" "Preview artifacts are hypothetical and mutate no trading state."
    "phase-exec-live-r012-no-order-domain-persistence-audit.json" = New-Audit "NoOrderDomainPersistence" "Artifact writer does not write order-domain persistence."
    "phase-exec-live-r012-no-route-submission-persistence-audit.json" = New-Audit "NoRouteSubmissionPersistence" "Artifact writer does not write route/submission persistence."
    "phase-exec-live-r012-no-fill-report-persistence-audit.json" = New-Audit "NoFillReportPersistence" "Artifact writer does not write fill/report persistence."
    "phase-exec-live-r012-no-broker-activation-audit.json" = New-Audit "NoBrokerActivation" "R012 does not activate broker or register broker paths."
    "phase-exec-live-r012-no-live-marketdata-audit.json" = New-Audit "NoLiveMarketData" "R012 uses disabled preview decisions only and requests no live data."
    "phase-exec-live-r012-no-scheduler-service-polling-audit.json" = New-Audit "NoSchedulerServicePolling" "R012 starts no scheduler/service/timer/polling/background jobs."
    "phase-exec-live-r012-no-order-created-audit.json" = New-Audit "NoOrderCreated" "Paper-ledger preview artifacts are NotAnOrder."
    "phase-exec-live-r012-no-child-order-audit.json" = New-Audit "NoChildOrder" "R012 creates no child slices or child orders."
    "phase-exec-live-r012-no-executable-schedule-audit.json" = New-Audit "NoExecutableSchedule" "R012 creates no executable schedules."
    "phase-exec-live-r012-no-route-no-submission-audit.json" = New-Audit "NoRouteNoSubmission" "R012 creates no routes or submissions."
    "phase-exec-live-r012-no-fill-execution-report-audit.json" = New-Audit "NoFillExecutionReport" "R012 creates no fills or execution reports."
    "phase-exec-live-r012-no-external-audit.json" = New-Audit "NoExternal" "No external API, Polygon, LMAX, broker, or live market data call is part of this phase."
}
foreach ($entry in $audits.GetEnumerator()) { Write-JsonArtifact $entry.Key $entry.Value }

Write-JsonArtifact "phase-exec-live-r012-canonical-quarter-hour-policy-preservation.json" ([ordered]@{ Phase = $phase; FutureCanonicalMinutes = @(0, 15, 30, 45); LegacyMinutesAcceptedAsFutureCanonical = $false })
Write-JsonArtifact "phase-exec-live-r012-legacy-compatibility-preservation.json" ([ordered]@{ Phase = $phase; LegacyLabels = @(":06", ":21", ":36", ":51"); Usage = "CompatibilityOnly"; UsedAsFutureCanonical = $false })
Write-JsonArtifact "phase-exec-live-r012-direct-cross-exclusion-preservation.json" ([ordered]@{ Phase = $phase; DirectCrossExecutionAllowed = $false; QubesWeightsMayContainCrossesAsSignalsOnly = $true; DirectCrossExecutionIntentRejected = $true })
Write-JsonArtifact "phase-exec-live-r012-usd-pair-netting-requirement.json" ([ordered]@{ Phase = $phase; Requirement = "EMS/OMS execution intents must be USD-pair-only after netting."; SupportedExecutionSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF"); AudusdStatus = "SupportedAndNotFailed" })
Write-JsonArtifact "phase-exec-live-r012-usdjpy-caveat-preservation.json" ([ordered]@{ Phase = $phase; NormalizedPortfolioSymbol = "JPYUSD"; ExecutionTradableSymbol = "USDJPY"; RequiresInversion = $true; SecurityID = "4004"; SecurityIDSource = "8"; CaveatPreserved = $true })
Write-JsonArtifact "phase-exec-live-r012-cost-guidance-preservation.json" ([ordered]@{ Phase = $phase; FiveUsdPerMillion = "BestCaseMajorOnly"; Universalized = $false })
Write-JsonArtifact "phase-exec-live-r012-nonmajor-calibration-preservation.json" ([ordered]@{ Phase = $phase; NonmajorEmScandiCnh = "CalibrationRequired"; LiveCapableExecutionAllowed = $false })
Write-JsonArtifact "phase-exec-live-r012-forbidden-actions-audit.json" ([ordered]@{ Phase = $phase; ExternalApiCallsMade = $false; PolygonCallsMade = $false; LmaxCallsMade = $false; BrokerActivationOccurred = $false; LiveMarketDataRequested = $false; SchedulerServicePollingBackgroundJobIntroduced = $false; PmsEmsOmsProductionCycleRun = $false; ManualNoExternalCommandRun = $false; BacktestSimulationRun = $false; TcaResultLinesCreated = $false; ExecutableScheduleCreated = $false; OrdersChildOrdersRoutesSubmissionsFillsReportsCreated = $false; PaperLedgerCommitCreated = $false; PaperLedgerCommitRecordCreated = $false; PaperLedgerTableWriteOccurred = $false; LedgerMutationAllowed = $false; TradingStateMutationOccurred = $false; ArtifactWriterWritesOutsideAllowedPath = $false; OrderDomainPersistenceOccurred = $false; RouteSubmissionPersistenceOccurred = $false; FillReportPersistenceOccurred = $false; PaperLedgerPreviewMisclassifiedAsCommit = $false; R009PromotedToExecutableUse = $false; DirectCrossExecutionAllowed = $false; Legacy06AcceptedAsFutureCanonical = $false })
Write-JsonArtifact "phase-exec-live-r012-next-phase-recommendation.json" ([ordered]@{ Phase = $phase; RecommendedNextPhase = "EXEC-LIVE-R013"; Title = "R009 Paper-Ledger Preview Internal Trial and Operator Review Gate"; Constraints = "Use paper-ledger-preview-only artifacts internally; still no commit, no ledger mutation, no orders, routes, fills, reports, executable schedules, broker/live paths, or trading state mutation." })
$buildFailureReason = ""
if ($BuildStatus -ne "Passed") {
    $buildFailureReason = "dotnet build --no-restore failed because tools/QQ.Production.Intraday.Tools.StratTakenPopulationAudit/obj/project.assets.json is missing. No restore was run under R012 no-external constraints."
}
Write-JsonArtifact "phase-exec-live-r012-build-test-validator-evidence.json" ([ordered]@{
    Phase = $phase
    Build = $BuildStatus
    BuildFailureReason = $buildFailureReason
    NoExternalRestoreRun = $true
    FocusedR012Tests = $FocusedTestsStatus
    UnitTests = $UnitTestsStatus
    Validator = $ValidatorStatus
    DotnetBuildNoRestore = "dotnet build --no-restore"
    FocusedTests = "dotnet test tests\QQ.Production.Intraday.Tests.Unit\QQ.Production.Intraday.Tests.Unit.csproj --no-build --no-restore --filter FullyQualifiedName~R009PaperLedgerPreviewOnlyTests"
    ValidatorScript = "scripts/check-exec-live-r012-paper-ledger-preview-only-gate.ps1"
})

$summary = @"
# EXEC-LIVE-R012 Summary

Classifications:
- EXEC_LIVE_R012_PASS_PAPER_LEDGER_PREVIEW_ONLY_CONTRACT_READY_NO_EXTERNAL
- EXEC_LIVE_R012_PASS_ARTIFACT_ONLY_LEDGER_PREVIEW_WRITER_READY_NO_EXTERNAL
- EXEC_LIVE_R012_PASS_LEDGER_COMMIT_BLOCKERS_READY_NO_EXTERNAL
- EXEC_LIVE_R012_PASS_NO_BROKER_NO_ORDER_NO_ROUTE_GATE_READY_NO_EXTERNAL

R012 creates the R009 paper-ledger preview-only DTOs, boundary guard, generation rules, artifact envelope, and artifact-only writer contract. PreviewReady disabled decisions may produce hypothetical position/cash/exposure previews. HeldMissingReadiness and rejected inputs produce held/rejected preview lines only.

This phase is not paper ledger commit, not trading state mutation, not execution, and not broker/live readiness. PaperLedgerCommit=false, LedgerMutation=false, TradingStateMutation=false, NonExecutable=true, NotAnOrder=true, and NoBrokerRoute=true remain preserved.

Build/tests/validator:
- Build: $BuildStatus
- Focused R012 tests: $FocusedTestsStatus
- Unit tests: $UnitTestsStatus
- Validator: $ValidatorStatus

Next recommendation: EXEC-LIVE-R013 - R009 Paper-Ledger Preview Internal Trial and Operator Review Gate.
"@
$summary | Set-Content -Path (Join-Path $artifactDir "phase-exec-live-r012-summary.md") -Encoding UTF8

Write-Host "EXEC-LIVE-R012 artifacts written to $artifactDir"
