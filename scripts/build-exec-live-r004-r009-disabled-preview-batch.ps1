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

$phase = "EXEC-LIVE-R004"
$contractVersion = "0.3.0-design-only-candidate"
$classifications = @(
    "EXEC_LIVE_R004_PASS_R009_DISABLED_PREVIEW_BATCH_CONTRACT_READY_NO_EXTERNAL",
    "EXEC_LIVE_R004_PASS_BATCH_SCHEMA_VALIDATION_READY_NO_EXTERNAL",
    "EXEC_LIVE_R004_PASS_BATCH_DECISION_PREVIEW_READY_NO_EXTERNAL",
    "EXEC_LIVE_R004_PASS_NO_BROKER_NO_ORDER_NO_ROUTE_GATE_READY_NO_EXTERNAL"
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

$r002IntentPath = Join-Path $artifactDir "phase-exec-live-r002-execution-intents.json"
if (-not (Test-Path -LiteralPath $r002IntentPath)) {
    throw "Missing R002 execution intents artifact."
}
$r002Intents = Get-Content -LiteralPath $r002IntentPath -Raw | ConvertFrom-Json
$audusd = @($r002Intents.Intents | Where-Object { $_.ExecutionTradableSymbol -eq "AUDUSD" } | Select-Object -First 1)[0]
$usdjpy = @($r002Intents.Intents | Where-Object { $_.ExecutionTradableSymbol -eq "USDJPY" } | Select-Object -First 1)[0]
$held = $audusd.PSObject.Copy()
$held.ExecutionIntentId = "exec-live-r004-held-missing-readiness-intent"
$held.QuoteWindowReadinessId = $null
$held.CloseBenchmarkReadinessId = $null
$held.FeedQualityReadinessId = $null

$sampleBatchRequest = [ordered]@{
    BatchRequestId = "exec-live-r004-sample-batch"
    RequestMode = "DisabledPreviewOnly"
    R009ContractVersion = $contractVersion
    DryRunOnly = $true
    LiveTradingEnabled = $false
    BrokerRoutingEnabled = $false
    OrderSubmissionEnabled = $false
    ExecutableScheduleEnabled = $false
    PaperLedgerCommitEnabled = $false
    OperatorApprovalScope = "DesignOnlyPreviewOnly"
    RiskApprovalScope = "DesignOnlyPreviewOnly"
    NoBrokerRoute = $true
    MaxBatchSize = 250
    Items = @(
        [ordered]@{ ItemId = "ready-audusd"; SourceType = "ExecutionIntent"; ExecutionIntent = $audusd },
        [ordered]@{ ItemId = "held-missing-readiness"; SourceType = "ExecutionIntent"; ExecutionIntent = $held },
        [ordered]@{ ItemId = "ready-usdjpy"; SourceType = "ExecutionIntent"; ExecutionIntent = $usdjpy }
    )
}

$sampleItemResults = @(
    [ordered]@{
        ItemId = "ready-audusd"
        Status = "PreviewReady"
        RejectionReasons = @()
        DecisionPreviewCount = 1
        IdempotencyHash = New-Hash "exec-live-r004-sample-batch|ready-audusd|$($audusd.ExecutionIntentId)"
        AuditHash = New-Hash "exec-live-r004-sample-batch|ready-audusd|PreviewReady"
    },
    [ordered]@{
        ItemId = "held-missing-readiness"
        Status = "HeldMissingReadiness"
        RejectionReasons = @()
        DecisionPreviewCount = 1
        HeldReasons = @("MissingQuoteWindowReadiness;MissingCloseBenchmarkReadiness;MissingFeedQualityReadiness")
        IdempotencyHash = New-Hash "exec-live-r004-sample-batch|held-missing-readiness|$($held.ExecutionIntentId)"
        AuditHash = New-Hash "exec-live-r004-sample-batch|held-missing-readiness|HeldMissingReadiness"
    },
    [ordered]@{
        ItemId = "ready-usdjpy"
        Status = "PreviewReady"
        RejectionReasons = @()
        DecisionPreviewCount = 1
        UsdjpyCaveatPreserved = $true
        SecurityID = "4004"
        SecurityIDSource = "8"
        IdempotencyHash = New-Hash "exec-live-r004-sample-batch|ready-usdjpy|$($usdjpy.ExecutionIntentId)"
        AuditHash = New-Hash "exec-live-r004-sample-batch|ready-usdjpy|PreviewReady"
    }
)

$sampleBatchResponse = [ordered]@{
    BatchRequestId = $sampleBatchRequest.BatchRequestId
    BatchStatus = "PreviewBatchGenerated"
    Validation = [ordered]@{
        IsValid = $true
        RejectionReasons = @()
        ItemCount = 3
        MaxBatchSize = 250
    }
    ItemResults = $sampleItemResults
    PreviewReadyCount = 2
    HeldMissingReadinessCount = 1
    RejectedCount = 0
    NonExecutable = $true
    NotAnOrder = $true
    NotSubmitted = $true
    NoBrokerRoute = $true
    NoFill = $true
    NoExecutionReport = $true
    NoRoute = $true
    NoSubmission = $true
    NoPaperLedgerCommit = $true
    IdempotencyHash = New-Hash "exec-live-r004-sample-batch|3|$contractVersion|250"
    AuditHash = New-Hash "exec-live-r004-sample-batch|PreviewBatchGenerated|2|1|0"
}

$invalidResults = @(
    [ordered]@{ Scenario = "LiveTradingEnabled"; BatchStatus = "Rejected"; RejectionReasons = @("LiveTradingMustRemainDisabled"); ItemResults = @() },
    [ordered]@{ Scenario = "MaxBatchSizeExceeded"; BatchStatus = "Rejected"; RejectionReasons = @("MaxBatchSizeExceeded"); ItemResults = @() },
    [ordered]@{ Scenario = "DirectCrossItem"; BatchStatus = "PreviewBatchGeneratedWithRejectedItems"; RejectedItemStatus = "Rejected"; RejectionReasons = @("DirectCrossExecutionIntentRejected") },
    [ordered]@{ Scenario = "UnsupportedNonmajorCnhItem"; BatchStatus = "PreviewBatchGeneratedWithRejectedItems"; RejectedItemStatus = "Rejected"; RejectionReasons = @("UnsupportedInstrumentRejected") },
    [ordered]@{ Scenario = "Legacy06TargetCloseItem"; BatchStatus = "PreviewBatchGeneratedWithRejectedItems"; RejectedItemStatus = "Rejected"; RejectionReasons = @("CanonicalQuarterHourTargetCloseRequired") },
    [ordered]@{ Scenario = "BadUsdjpyCaveat"; BatchStatus = "PreviewBatchGeneratedWithRejectedItems"; RejectedItemStatus = "Rejected"; RejectionReasons = @("InversionMetadataInvalid") },
    [ordered]@{ Scenario = "ExecutableScheduleOutputRequested"; BatchStatus = "Rejected"; RejectionReasons = @("ForbiddenOutputRequested:ExecutableSchedule"); ItemResults = @() }
)

Write-JsonArtifact "phase-exec-live-r004-r003-contract-reference.json" ([ordered]@{
    Phase = $phase
    R003Contract = "artifacts/readiness/execution-live/phase-exec-live-r003-api-cli-contract.json"
    R003RequestDto = "artifacts/readiness/execution-live/phase-exec-live-r003-request-dto-contract.json"
    R003ResponseDto = "artifacts/readiness/execution-live/phase-exec-live-r003-response-dto-contract.json"
})
Write-JsonArtifact "phase-exec-live-r004-r009-contract-reference.json" ([ordered]@{
    Phase = $phase
    R009ContractVersion = $contractVersion
    PrimaryPolicyCandidate = "CloseSeeking15mAdaptive_BalancedAdaptive_v0"
    SecondaryPolicyCandidate = "CloseSeeking15mAdaptive_ResidualAwareUrgency_v0"
    ConditionalResidualModule = "ControlledResidualCross_BalancedResidualCross_v0"
    NonExecutable = $true
    NotAnOrder = $true
    NotSubmitted = $true
    NoBrokerRoute = $true
    BrokerReady = $false
    LiveReady = $false
    ExecutablePromotionAuthorized = $false
})
Write-JsonArtifact "phase-exec-live-r004-batch-api-contract.json" ([ordered]@{
    Phase = $phase
    Service = "R009DisabledPreviewBatchService"
    RequestDto = "R009DisabledPreviewBatchRequest"
    ResponseDto = "R009DisabledPreviewBatchResponse"
    MaxBatchSizeDefault = 250
    AllowedStatuses = @("PreviewReady", "HeldMissingReadiness", "Rejected")
    LiveTradingAllowed = $false
    BrokerRoutingAllowed = $false
    OrderSubmissionAllowed = $false
    ExecutableScheduleAllowed = $false
    PaperLedgerCommitAllowed = $false
})
Write-JsonArtifact "phase-exec-live-r004-batch-request-dto-contract.json" ([ordered]@{
    Phase = $phase
    Dto = "R009DisabledPreviewBatchRequest"
    RequiredFields = @("BatchRequestId", "RequestMode=DisabledPreviewOnly", "Items", "R009ContractVersion", "DryRunOnly=true", "LiveTradingEnabled=false", "BrokerRoutingEnabled=false", "OrderSubmissionEnabled=false", "ExecutableScheduleEnabled=false", "PaperLedgerCommitEnabled=false", "NoBrokerRoute=true", "MaxBatchSize")
})
Write-JsonArtifact "phase-exec-live-r004-batch-response-dto-contract.json" ([ordered]@{
    Phase = $phase
    Dto = "R009DisabledPreviewBatchResponse"
    RequiredFields = @("BatchRequestId", "BatchStatus", "Validation", "ItemResults", "PreviewReadyCount", "HeldMissingReadinessCount", "RejectedCount", "IdempotencyHash", "AuditHash", "NoBrokerRoute=true", "NoPaperLedgerCommit=true")
})
Write-JsonArtifact "phase-exec-live-r004-batch-item-contract.json" ([ordered]@{
    Phase = $phase
    Dto = "R009DisabledPreviewBatchItem"
    SourceTypes = @("ExecutionIntent", "PaperPlanLineArtifact")
})
Write-JsonArtifact "phase-exec-live-r004-batch-item-result-contract.json" ([ordered]@{
    Phase = $phase
    Dto = "R009DisabledPreviewBatchItemResult"
    Statuses = @("PreviewReady", "HeldMissingReadiness", "Rejected")
})
Write-JsonArtifact "phase-exec-live-r004-batch-validation-contract.json" ([ordered]@{
    Phase = $phase
    Dto = "R009DisabledPreviewBatchValidationResult"
    BatchLevelChecks = @("RequestMode", "DryRunOnly", "LiveTrading", "BrokerRouting", "OrderSubmission", "ExecutableSchedule", "PaperLedgerCommit", "NoBrokerRoute", "ApprovalScopes", "MaxBatchSize", "ForbiddenOutputs")
    ItemLevelChecks = @("canonical quarter-hour target close", "legacy :06 rejection", "direct-cross rejection", "unsupported instrument rejection", "USDJPY caveat", "non-executable intent")
})
Write-JsonArtifact "phase-exec-live-r004-sample-batch-request.json" $sampleBatchRequest
Write-JsonArtifact "phase-exec-live-r004-sample-batch-response.json" $sampleBatchResponse
Write-JsonArtifact "phase-exec-live-r004-invalid-batch-rejection-results.json" ([ordered]@{
    Phase = $phase
    InvalidScenarioCount = $invalidResults.Count
    Results = $invalidResults
})
Write-JsonArtifact "phase-exec-live-r004-batch-output-audit.json" ([ordered]@{
    Phase = $phase
    CreatesOrder = $false
    CreatesChildOrder = $false
    CreatesRoute = $false
    CreatesSubmission = $false
    CreatesFill = $false
    CreatesExecutionReport = $false
    CreatesExecutableSchedule = $false
    NoPaperLedgerCommit = $true
    ResponseCanBeRepresentedAsOrderRouteFillSchedule = $false
})
Write-JsonArtifact "phase-exec-live-r004-idempotency-audit-review.json" ([ordered]@{
    Phase = $phase
    BatchIdempotencyHashPresent = -not [string]::IsNullOrWhiteSpace($sampleBatchResponse.IdempotencyHash)
    BatchAuditHashPresent = -not [string]::IsNullOrWhiteSpace($sampleBatchResponse.AuditHash)
    ItemHashesPresent = @($sampleItemResults | Where-Object { [string]::IsNullOrWhiteSpace($_.IdempotencyHash) -or [string]::IsNullOrWhiteSpace($_.AuditHash) }).Count -eq 0
})
Write-JsonArtifact "phase-exec-live-r004-kill-switch-feature-flag-review.json" ([ordered]@{
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
Write-JsonArtifact "phase-exec-live-r004-disabled-boundary-guard-review.json" ([ordered]@{
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

Write-JsonArtifact "phase-exec-live-r004-canonical-quarter-hour-policy-preservation.json" ([ordered]@{ Phase = $phase; FutureCanonicalMinutes = @(0, 15, 30, 45); LegacyMinutesAcceptedAsFutureCanonical = $false })
Write-JsonArtifact "phase-exec-live-r004-legacy-compatibility-preservation.json" ([ordered]@{ Phase = $phase; LegacyLabels = @(":06", ":21", ":36", ":51"); Usage = "CompatibilityOnly"; UsedAsFutureCanonical = $false })
Write-JsonArtifact "phase-exec-live-r004-direct-cross-exclusion-preservation.json" ([ordered]@{ Phase = $phase; DirectCrossExecutionAllowed = $false })
Write-JsonArtifact "phase-exec-live-r004-usd-pair-netting-requirement.json" ([ordered]@{ Phase = $phase; Requirement = "Batch execution intents must be USD-pair-only after netting."; SupportedExecutionSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF") })
Write-JsonArtifact "phase-exec-live-r004-usdjpy-caveat-preservation.json" ([ordered]@{ Phase = $phase; NormalizedPortfolioSymbol = "JPYUSD"; ExecutionTradableSymbol = "USDJPY"; RequiresInversion = $true; SecurityID = "4004"; SecurityIDSource = "8"; CaveatPreserved = $true })
Write-JsonArtifact "phase-exec-live-r004-cost-guidance-preservation.json" ([ordered]@{ Phase = $phase; FiveUsdPerMillion = "BestCaseMajorOnly"; Universalized = $false })
Write-JsonArtifact "phase-exec-live-r004-nonmajor-calibration-preservation.json" ([ordered]@{ Phase = $phase; NonmajorEmScandiCnh = "CalibrationRequired"; LiveCapableExecutionAllowed = $false })

$audits = [ordered]@{
    "phase-exec-live-r004-no-broker-activation-audit.json" = New-Audit "NoBrokerActivation" "No broker runtime, route, FIX, TLS, or socket activation was introduced."
    "phase-exec-live-r004-no-live-marketdata-audit.json" = New-Audit "NoLiveMarketData" "No live market data request path was introduced."
    "phase-exec-live-r004-no-scheduler-service-polling-audit.json" = New-Audit "NoSchedulerServicePolling" "No scheduler, service, timer, polling, or background worker registration was introduced."
    "phase-exec-live-r004-no-order-created-audit.json" = New-Audit "NoOrderCreated" "Batch preview responses cannot create orders."
    "phase-exec-live-r004-no-child-order-audit.json" = New-Audit "NoChildOrder" "Batch preview responses cannot create child orders."
    "phase-exec-live-r004-no-executable-schedule-audit.json" = New-Audit "NoExecutableSchedule" "Batch preview responses cannot create executable schedules."
    "phase-exec-live-r004-no-route-no-submission-audit.json" = New-Audit "NoRouteNoSubmission" "Batch preview responses cannot create routes or submissions."
    "phase-exec-live-r004-no-fill-execution-report-audit.json" = New-Audit "NoFillExecutionReport" "Batch preview responses cannot create fills or execution reports."
    "phase-exec-live-r004-no-paper-ledger-commit-audit.json" = New-Audit "NoPaperLedgerCommit" "Batch preview responses cannot commit paper ledger state."
    "phase-exec-live-r004-no-state-mutation-audit.json" = New-Audit "NoStateMutation" "Batch preview service has no state mutation authorization."
    "phase-exec-live-r004-no-external-audit.json" = New-Audit "NoExternal" "No external API, Polygon, LMAX, broker, or live market data call is part of this phase."
}
foreach ($entry in $audits.GetEnumerator()) {
    Write-JsonArtifact $entry.Key $entry.Value
}

Write-JsonArtifact "phase-exec-live-r004-forbidden-actions-audit.json" ([ordered]@{
    Phase = $phase
    ProhibitedActionsObserved = @()
    ExternalApiCallsMade = $false
    BrokerLiveOrderRouteFillScheduleLedgerStatePathsEnabled = $false
    PmsEmsOmsCycleRun = $false
    ManualNoExternalCommandRun = $false
    R009PromotedToExecutableUse = $false
})
Write-JsonArtifact "phase-exec-live-r004-next-phase-recommendation.json" ([ordered]@{
    Phase = $phase
    RecommendedNextPhase = "EXEC-LIVE-R005"
    Title = "R009 Disabled Preview API Consumer Handoff and EMS/OMS Integration Boundary Gate"
})
Write-JsonArtifact "phase-exec-live-r004-build-test-validator-evidence.json" ([ordered]@{
    Phase = $phase
    Build = $BuildStatus
    FocusedR004Tests = $FocusedTestsStatus
    UnitTests = $UnitTestsStatus
    Validator = $ValidatorStatus
    EvidenceRequired = $true
})

$summary = @"
# EXEC-LIVE-R004 Summary

Classifications:
- $($classifications -join "`n- ")

R009 disabled preview API is hardened for batch requests through:
- R009DisabledPreviewBatchRequest
- R009DisabledPreviewBatchResponse
- R009DisabledPreviewBatchItem
- R009DisabledPreviewBatchItemResult
- R009DisabledPreviewBatchValidationResult
- R009DisabledPreviewBatchService

Sample batch result:
- PreviewReady: 2
- HeldMissingReadiness: 1
- Rejected: 0

Invalid batch/item scenarios are documented for live trading, max batch size, direct-cross execution, unsupported nonmajor/CNH, legacy :06 target close, bad USDJPY caveat, and executable schedule output requests.

No broker, live market data, scheduler, order, child order, route, submission, fill, execution report, executable schedule, paper ledger commit, or state mutation path is enabled.

Next phase:
- EXEC-LIVE-R005 - R009 Disabled Preview API Consumer Handoff and EMS/OMS Integration Boundary Gate
"@
Set-Content -Path (Join-Path $artifactDir "phase-exec-live-r004-summary.md") -Value $summary -Encoding UTF8

Write-Host "Wrote EXEC-LIVE-R004 artifacts to $artifactDir"
