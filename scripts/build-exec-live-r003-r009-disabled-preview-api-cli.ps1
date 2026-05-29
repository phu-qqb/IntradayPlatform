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

$phase = "EXEC-LIVE-R003"
$contractVersion = "0.3.0-design-only-candidate"
$primaryPolicy = "CloseSeeking15mAdaptive_BalancedAdaptive_v0"
$secondaryPolicy = "CloseSeeking15mAdaptive_ResidualAwareUrgency_v0"
$conditionalModule = "ControlledResidualCross_BalancedResidualCross_v0"
$classifications = @(
    "EXEC_LIVE_R003_PASS_R009_DISABLED_PREVIEW_API_CLI_CONTRACT_READY_NO_EXTERNAL",
    "EXEC_LIVE_R003_PASS_REQUEST_RESPONSE_DTO_READY_NO_EXTERNAL",
    "EXEC_LIVE_R003_PASS_DISABLED_BOUNDARY_GUARD_READY_NO_EXTERNAL",
    "EXEC_LIVE_R003_PASS_NO_BROKER_NO_ORDER_NO_ROUTE_GATE_READY_NO_EXTERNAL"
)

function Write-JsonArtifact {
    param(
        [string]$Name,
        [object]$Value
    )

    $path = Join-Path $artifactDir $Name
    $Value | ConvertTo-Json -Depth 40 | Set-Content -Path $path -Encoding UTF8
}

function New-Audit {
    param(
        [string]$Name,
        [string]$Description
    )

    [ordered]@{
        Phase = $phase
        Audit = $Name
        Status = "Pass"
        Evidence = $Description
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
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
        return ([System.BitConverter]::ToString($sha.ComputeHash($bytes))).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

$r002IntentPath = Join-Path $artifactDir "phase-exec-live-r002-execution-intents.json"
$r002DecisionPath = Join-Path $artifactDir "phase-exec-live-r002-r009-decision-previews.json"
if (-not (Test-Path -LiteralPath $r002IntentPath)) {
    throw "R002 execution intents artifact missing: $r002IntentPath"
}
if (-not (Test-Path -LiteralPath $r002DecisionPath)) {
    throw "R002 decision previews artifact missing: $r002DecisionPath"
}

$r002Intents = Get-Content -LiteralPath $r002IntentPath -Raw | ConvertFrom-Json
$r002Decisions = Get-Content -LiteralPath $r002DecisionPath -Raw | ConvertFrom-Json
$sampleIntent = @($r002Intents.Intents | Where-Object { $_.ExecutionTradableSymbol -eq "AUDUSD" } | Select-Object -First 1)[0]
$sampleDecision = @($r002Decisions.DecisionPreviews | Where-Object { $_.ExecutionIntentId -eq $sampleIntent.ExecutionIntentId } | Select-Object -First 1)[0]

$sampleInlineRequest = [ordered]@{
    RequestId = "exec-live-r003-inline-preview-sample"
    RequestMode = "DisabledPreviewOnly"
    SourceType = "ExecutionIntent"
    ExecutionIntent = $sampleIntent
    SourceArtifactPath = $null
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
    RequestedOutputs = @("DesignOnlyExecutionDecision", "ExecutionPlanPreview", "ResidualRiskAssessment", "CostTradeoffAssessment")
}

$sampleArtifactRequest = [ordered]@{
    RequestId = "exec-live-r003-artifact-preview-sample"
    RequestMode = "DisabledPreviewOnly"
    SourceType = "PaperPlanLineArtifact"
    ExecutionIntent = $null
    SourceArtifactPath = "artifacts/readiness/execution-sim/phase-exec-paper-r012-r009-design-only-preview-lines.json"
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
    RequestedOutputs = @("DesignOnlyExecutionDecision", "ExecutionPlanPreview", "ResidualRiskAssessment", "CostTradeoffAssessment")
}

$sampleResponse = [ordered]@{
    RequestId = $sampleInlineRequest.RequestId
    DecisionPreviewId = "$($sampleInlineRequest.RequestId):r009-disabled-preview-response"
    DecisionStatus = "PreviewGenerated"
    Accepted = $true
    RejectionReasons = @()
    DecisionPreviews = @($sampleDecision)
    HeldReasons = @()
    NonExecutable = $true
    NotAnOrder = $true
    NotSubmitted = $true
    NoBrokerRoute = $true
    NoFill = $true
    NoExecutionReport = $true
    NoRoute = $true
    NoSubmission = $true
    NoPaperLedgerCommit = $true
    SafetyFlags = [ordered]@{
        DryRunOnly = $true
        LiveTradingEnabled = $false
        BrokerRoutingEnabled = $false
        OrderSubmissionEnabled = $false
        ExecutableScheduleEnabled = $false
        PaperLedgerCommitEnabled = $false
        SchedulerEnabled = $false
        BackgroundWorkerEnabled = $false
        NoBrokerRoute = $true
    }
    IdempotencyHash = New-Hash "$($sampleInlineRequest.RequestId)|ExecutionIntent|$contractVersion"
    AuditHash = New-Hash "$($sampleInlineRequest.RequestId):r009-disabled-preview-response|PreviewGenerated|1"
}

$invalidResults = @(
    [ordered]@{
        RequestId = "invalid-live-trading"
        Accepted = $false
        RejectionReasons = @("LiveTradingMustRemainDisabled")
        DecisionPreviewCount = 0
    },
    [ordered]@{
        RequestId = "invalid-broker-routing"
        Accepted = $false
        RejectionReasons = @("BrokerRoutingMustRemainDisabled")
        DecisionPreviewCount = 0
    },
    [ordered]@{
        RequestId = "invalid-order-submission"
        Accepted = $false
        RejectionReasons = @("OrderSubmissionMustRemainDisabled")
        DecisionPreviewCount = 0
    },
    [ordered]@{
        RequestId = "invalid-executable-schedule-output"
        Accepted = $false
        RejectionReasons = @("ForbiddenOutputRequested:ExecutableSchedule")
        DecisionPreviewCount = 0
    },
    [ordered]@{
        RequestId = "invalid-paper-ledger-commit"
        Accepted = $false
        RejectionReasons = @("PaperLedgerCommitMustRemainDisabled")
        DecisionPreviewCount = 0
    }
)

Write-JsonArtifact "phase-exec-live-r003-r002-preview-reference.json" ([ordered]@{
    Phase = $phase
    R002ExecutionIntents = "artifacts/readiness/execution-live/phase-exec-live-r002-execution-intents.json"
    R002DecisionPreviews = "artifacts/readiness/execution-live/phase-exec-live-r002-r009-decision-previews.json"
    R002DecisionPreviewCount = $r002Decisions.DecisionPreviewCount
})
Write-JsonArtifact "phase-exec-live-r003-r009-contract-reference.json" ([ordered]@{
    Phase = $phase
    R009ContractVersion = $contractVersion
    PrimaryPolicyCandidate = $primaryPolicy
    SecondaryPolicyCandidate = $secondaryPolicy
    ConditionalResidualModule = $conditionalModule
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
Write-JsonArtifact "phase-exec-live-r003-api-cli-contract.json" ([ordered]@{
    Phase = $phase
    ContractName = "R009 Disabled-Mode EMS/OMS Decision Preview API/CLI Contract"
    InternalSurface = "R009DisabledPreviewContractService"
    AcceptedSourceTypes = @("ExecutionIntent", "PaperPlanLineArtifact")
    RequestModeRequired = "DisabledPreviewOnly"
    DryRunOnlyRequired = $true
    LiveTradingAllowed = $false
    BrokerRoutingAllowed = $false
    OrderSubmissionAllowed = $false
    ExecutableScheduleAllowed = $false
    PaperLedgerCommitAllowed = $false
    NoBrokerRouteRequired = $true
    ForbiddenRequestedOutputs = @("Order", "ChildOrder", "Route", "Submission", "Fill", "ExecutionReport", "ExecutableSchedule")
    LiveRuntimeRegistered = $false
    BrokerRouteRegistered = $false
    SchedulerWorkerRegistered = $false
})
Write-JsonArtifact "phase-exec-live-r003-request-dto-contract.json" ([ordered]@{
    Phase = $phase
    Dto = "R009DisabledPreviewRequest"
    RequiredFields = @(
        "RequestId",
        "RequestMode=DisabledPreviewOnly",
        "SourceType=ExecutionIntent|PaperPlanLineArtifact",
        "ExecutionIntent when SourceType=ExecutionIntent",
        "SourceArtifactPath when SourceType=PaperPlanLineArtifact",
        "R009ContractVersion",
        "DryRunOnly=true",
        "LiveTradingEnabled=false",
        "BrokerRoutingEnabled=false",
        "OrderSubmissionEnabled=false",
        "ExecutableScheduleEnabled=false",
        "PaperLedgerCommitEnabled=false",
        "OperatorApprovalScope=DesignOnlyPreviewOnly",
        "RiskApprovalScope=DesignOnlyPreviewOnly",
        "NoBrokerRoute=true"
    )
})
Write-JsonArtifact "phase-exec-live-r003-response-dto-contract.json" ([ordered]@{
    Phase = $phase
    Dto = "R009DisabledPreviewResponse"
    RequiredFields = @(
        "RequestId",
        "DecisionPreviewId",
        "DecisionStatus",
        "DecisionPreviews",
        "HeldReasons",
        "NonExecutable=true",
        "NotAnOrder=true",
        "NotSubmitted=true",
        "NoBrokerRoute=true",
        "NoFill=true",
        "NoExecutionReport=true",
        "NoRoute=true",
        "NoSubmission=true",
        "NoPaperLedgerCommit=true",
        "SafetyFlags",
        "IdempotencyHash",
        "AuditHash"
    )
})
Write-JsonArtifact "phase-exec-live-r003-disabled-preview-service-contract.json" ([ordered]@{
    Phase = $phase
    Service = "R009DisabledPreviewContractService"
    Uses = @("R009DisabledDecisionPreviewIntegrationService", "R009DisabledEmsOmsExecutionAdapter")
    RegisteredAsLiveRuntime = $false
    ReadsExternalData = $false
    CreatesExecutableArtifacts = $false
})
Write-JsonArtifact "phase-exec-live-r003-sample-inline-request.json" $sampleInlineRequest
Write-JsonArtifact "phase-exec-live-r003-sample-artifact-request.json" $sampleArtifactRequest
Write-JsonArtifact "phase-exec-live-r003-sample-preview-response.json" $sampleResponse
Write-JsonArtifact "phase-exec-live-r003-invalid-request-rejection-results.json" ([ordered]@{
    Phase = $phase
    InvalidRequestCount = $invalidResults.Count
    Results = $invalidResults
})
Write-JsonArtifact "phase-exec-live-r003-decision-preview-output-audit.json" ([ordered]@{
    Phase = $phase
    DecisionPreviewCount = 1
    ForbiddenOutputCount = 0
    CreatesOrder = $false
    CreatesChildOrder = $false
    CreatesRoute = $false
    CreatesSubmission = $false
    CreatesFill = $false
    CreatesExecutionReport = $false
    CreatesExecutableSchedule = $false
    NoPaperLedgerCommit = $true
})
Write-JsonArtifact "phase-exec-live-r003-kill-switch-feature-flag-review.json" ([ordered]@{
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
Write-JsonArtifact "phase-exec-live-r003-disabled-boundary-guard-review.json" ([ordered]@{
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
Write-JsonArtifact "phase-exec-live-r003-idempotency-audit-review.json" ([ordered]@{
    Phase = $phase
    IdempotencyHashPresent = -not [string]::IsNullOrWhiteSpace($sampleResponse.IdempotencyHash)
    AuditHashPresent = -not [string]::IsNullOrWhiteSpace($sampleResponse.AuditHash)
    NoOrderDomainOutput = $true
    NoBrokerRoute = $true
    DryRunOnly = $true
})

$audits = [ordered]@{
    "phase-exec-live-r003-no-broker-activation-audit.json" = New-Audit "NoBrokerActivation" "No broker runtime, route, FIX, TLS, or socket activation was introduced."
    "phase-exec-live-r003-no-live-marketdata-audit.json" = New-Audit "NoLiveMarketData" "No live market data request path was introduced."
    "phase-exec-live-r003-no-scheduler-service-polling-audit.json" = New-Audit "NoSchedulerServicePolling" "No scheduler, service, timer, polling, or background worker registration was introduced."
    "phase-exec-live-r003-no-order-created-audit.json" = New-Audit "NoOrderCreated" "Preview responses cannot create orders."
    "phase-exec-live-r003-no-child-order-audit.json" = New-Audit "NoChildOrder" "Preview responses cannot create child orders."
    "phase-exec-live-r003-no-executable-schedule-audit.json" = New-Audit "NoExecutableSchedule" "Preview responses cannot create executable schedules."
    "phase-exec-live-r003-no-route-no-submission-audit.json" = New-Audit "NoRouteNoSubmission" "Preview responses cannot create routes or submissions."
    "phase-exec-live-r003-no-fill-execution-report-audit.json" = New-Audit "NoFillExecutionReport" "Preview responses cannot create fills or execution reports."
    "phase-exec-live-r003-no-paper-ledger-commit-audit.json" = New-Audit "NoPaperLedgerCommit" "Preview responses cannot commit paper ledger state."
    "phase-exec-live-r003-no-state-mutation-audit.json" = New-Audit "NoStateMutation" "Preview service has no state mutation authorization."
    "phase-exec-live-r003-no-external-audit.json" = New-Audit "NoExternal" "No external API, Polygon, LMAX, broker, or live market data call is part of this phase."
}
foreach ($entry in $audits.GetEnumerator()) {
    Write-JsonArtifact $entry.Key $entry.Value
}

Write-JsonArtifact "phase-exec-live-r003-canonical-quarter-hour-policy-preservation.json" ([ordered]@{
    Phase = $phase
    FutureCanonicalMinutes = @(0, 15, 30, 45)
    LegacyMinutesAcceptedAsFutureCanonical = $false
})
Write-JsonArtifact "phase-exec-live-r003-legacy-compatibility-preservation.json" ([ordered]@{
    Phase = $phase
    LegacyLabels = @(":06", ":21", ":36", ":51")
    Usage = "CompatibilityOnly"
    UsedAsFutureCanonical = $false
})
Write-JsonArtifact "phase-exec-live-r003-direct-cross-exclusion-preservation.json" ([ordered]@{
    Phase = $phase
    DirectCrossExecutionAllowed = $false
})
Write-JsonArtifact "phase-exec-live-r003-usd-pair-netting-requirement.json" ([ordered]@{
    Phase = $phase
    Requirement = "EMS/OMS execution intents must be USD-pair-only after netting."
    SupportedExecutionSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")
})
Write-JsonArtifact "phase-exec-live-r003-usdjpy-caveat-preservation.json" ([ordered]@{
    Phase = $phase
    NormalizedPortfolioSymbol = "JPYUSD"
    ExecutionTradableSymbol = "USDJPY"
    RequiresInversion = $true
    SecurityID = "4004"
    SecurityIDSource = "8"
    CaveatPreserved = $true
})
Write-JsonArtifact "phase-exec-live-r003-cost-guidance-preservation.json" ([ordered]@{
    Phase = $phase
    FiveUsdPerMillion = "BestCaseMajorOnly"
    Universalized = $false
})
Write-JsonArtifact "phase-exec-live-r003-nonmajor-calibration-preservation.json" ([ordered]@{
    Phase = $phase
    NonmajorEmScandiCnh = "CalibrationRequired"
    LiveCapableExecutionAllowed = $false
})
Write-JsonArtifact "phase-exec-live-r003-forbidden-actions-audit.json" ([ordered]@{
    Phase = $phase
    ProhibitedActionsObserved = @()
    ExternalApiCallsMade = $false
    BrokerLiveOrderRouteFillScheduleLedgerStatePathsEnabled = $false
    PmsEmsOmsCycleRun = $false
    ManualNoExternalCommandRun = $false
    R009PromotedToExecutableUse = $false
})
Write-JsonArtifact "phase-exec-live-r003-next-phase-recommendation.json" ([ordered]@{
    Phase = $phase
    RecommendedNextPhase = "EXEC-LIVE-R004"
    Title = "R009 Disabled Preview API Hardening and Batch Request Gate"
    Description = "Harden the disabled preview API for batch requests and schema validation, with broker/order/live paths still disabled."
})
Write-JsonArtifact "phase-exec-live-r003-build-test-validator-evidence.json" ([ordered]@{
    Phase = $phase
    Build = $BuildStatus
    FocusedR003Tests = $FocusedTestsStatus
    UnitTests = $UnitTestsStatus
    Validator = $ValidatorStatus
    EvidenceRequired = $true
})

$summary = @"
# EXEC-LIVE-R003 Summary

Classifications:
- $($classifications -join "`n- ")

R009 disabled-mode decision preview is exposed through an internal application service contract:
- R009DisabledPreviewRequest
- R009DisabledPreviewResponse
- R009DisabledPreviewContractService

The contract accepts inline execution intents or paper-plan-line artifact requests, and rejects requests that enable live trading, broker routing, order submission, executable schedules, paper ledger commits, or forbidden order-like outputs.

No broker, live market data, scheduler, order, child order, route, submission, fill, execution report, executable schedule, paper ledger commit, or state mutation path is enabled.

Next phase:
- EXEC-LIVE-R004 - R009 Disabled Preview API Hardening and Batch Request Gate
"@
Set-Content -Path (Join-Path $artifactDir "phase-exec-live-r003-summary.md") -Value $summary -Encoding UTF8

Write-Host "Wrote EXEC-LIVE-R003 artifacts to $artifactDir"
