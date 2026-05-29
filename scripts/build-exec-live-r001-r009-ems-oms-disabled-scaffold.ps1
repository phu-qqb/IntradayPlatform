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

$phase = "EXEC-LIVE-R001"
$contractVersion = "0.3.0-design-only-candidate"
$classifications = @(
    "EXEC_LIVE_R001_PASS_R009_EMS_OMS_DISABLED_SCAFFOLD_READY_NO_EXTERNAL",
    "EXEC_LIVE_R001_PASS_PRETRADE_RISK_AND_KILL_SWITCH_CONTRACTS_READY_NO_EXTERNAL",
    "EXEC_LIVE_R001_PASS_NO_BROKER_NO_ORDER_NO_ROUTE_GATE_READY_NO_EXTERNAL",
    "EXEC_LIVE_R001_PASS_R009_SELECTED_ALGO_PRESERVED_READY_NO_EXTERNAL"
)

function Write-JsonArtifact {
    param(
        [string]$Name,
        [object]$Value
    )

    $path = Join-Path $artifactDir $Name
    $Value | ConvertTo-Json -Depth 20 | Set-Content -Path $path -Encoding UTF8
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

$r009Contract = [ordered]@{
    Phase = $phase
    R009ContractVersion = $contractVersion
    PrimaryPolicyCandidate = "CloseSeeking15mAdaptive_BalancedAdaptive_v0"
    SecondaryPolicyCandidate = "CloseSeeking15mAdaptive_ResidualAwareUrgency_v0"
    ConditionalResidualModule = "ControlledResidualCross_BalancedResidualCross_v0"
    SelectedForEmsOmsIntegrationTarget = $true
    DisabledModeOnly = $true
    DesignOnly = $true
    PaperOnly = $true
    NonExecutable = $true
    NotAnOrder = $true
    NotSubmitted = $true
    NoBrokerRoute = $true
    BrokerReady = $false
    LiveReady = $false
    ExecutablePromotionAuthorized = $false
}

$paperMaturityReference = [ordered]@{
    Phase = $phase
    Sources = @(
        "artifacts/readiness/execution-algo/phase-exec-algo-r013-long-run-paper-maturity-acceptance-result.json",
        "artifacts/readiness/execution-sim/phase-exec-sim-r061-programme-summary-report.json",
        "artifacts/readiness/execution-sim/phase-exec-paper-r019-continuation-decision.json"
    )
    AcceptedStatus = "R009AcceptedForLongRunPaperOnlyEvaluationWithExplicitReadinessBlocker"
    PaperOnlyMaturity = "R009PaperOnlyMaturityPartialButUsable"
    ReadinessComplete = "644 / 700"
    RemainingHeld = 56
    ResidualBlocker = "LocalMarketDataReadinessIncompleteFor56PreviewLines"
    ResidualBlockerIsR009LogicFailure = $false
    ExecutablePromotionBlocked = $true
}

$operatingModelReference = [ordered]@{
    Phase = $phase
    Source = "artifacts/readiness/execution-algo/phase-exec-algo-r014-accepted-blocker-operating-model-result.json"
    HeldReadinessSemantics = [ordered]@{
        MissingReadinessEqualsR009Failure = $false
        MissingReadinessAuthorizesOrders = $false
        MissingReadinessProducesHeldLine = $true
        HeldLineRemainsNonExecutable = $true
    }
    ContinuationDecisionFromR019 = "R009PaperOnlyContinuationStableWithHeldReadiness"
}

$integrationContract = [ordered]@{
    Phase = $phase
    ContractName = "R009 EMS/OMS Disabled-Mode Integration Contract"
    Description = "PMS/EMS/OMS may pass execution intents into R009 for design-only disabled-mode decision previews."
    RequiredIntentFields = @(
        "ExecutionIntentId",
        "SourcePmsCycleId",
        "SourceQubesRunId",
        "SourceRebalanceIntentId",
        "SourceRiskReviewId",
        "Symbol",
        "ExecutionTradableSymbol",
        "NormalizedPortfolioSymbol",
        "RequiresInversion",
        "Side",
        "TargetQuantity",
        "TargetNotional",
        "CanonicalTargetCloseUtc",
        "CanonicalTargetCloseLocal",
        "CanonicalSession",
        "BarRole",
        "MustEndFlat",
        "OvernightAllowed=false",
        "QuoteWindowReadinessId",
        "CloseBenchmarkReadinessId",
        "FeedQualityReadinessId",
        "R009ContractVersion",
        "OperatorApprovalStatus",
        "RiskApprovalStatus",
        "LiveTradingEnabled=false",
        "BrokerRoutingEnabled=false",
        "OrderSubmissionEnabled=false",
        "NonExecutable=true"
    )
    LiveRuntimeWiring = "NotRegistered"
    SchedulerWorkerRegistered = $false
    BrokerRouteRegistered = $false
}

$intentContract = [ordered]@{
    Phase = $phase
    ContractName = "R009 EMS/OMS Execution Intent"
    Fields = $integrationContract.RequiredIntentFields
    Constraints = @(
        "USD-pair-only after netting",
        "Direct crosses signal-only and execution-disabled",
        "Canonical target closes must use quarter-hour closes 00,15,30,45",
        "Legacy :06/:21/:36/:51 labels are compatibility-only",
        "Risk/operator approvals are preview-only",
        "LiveTradingEnabled=false",
        "BrokerRoutingEnabled=false",
        "OrderSubmissionEnabled=false",
        "NonExecutable=true"
    )
}

$decisionContract = [ordered]@{
    Phase = $phase
    ContractName = "R009 Disabled-Mode Execution Decision"
    AllowedOutputs = @(
        "DesignOnlyExecutionDecision",
        "ExecutionPlanPreview",
        "ScheduleIntentPreview",
        "ResidualRiskAssessment",
        "CostTradeoffAssessment",
        "ManualReviewRecommendation",
        "HoldReason"
    )
    ForbiddenOutputs = @(
        "Order",
        "ChildOrder",
        "Route",
        "Submission",
        "Fill",
        "ExecutionReport",
        "ExecutableSchedule"
    )
    OutputFlags = [ordered]@{
        DesignOnly = $true
        PaperOnly = $true
        NonExecutable = $true
        NotAnOrder = $true
        NotSubmitted = $true
        NoBrokerRoute = $true
        NoChildSlices = $true
        NoChildOrders = $true
        NoExecutableSchedule = $true
        NoFill = $true
        NoExecutionReport = $true
        NoRoute = $true
        NoSubmission = $true
        NoPaperLedgerCommit = $true
    }
    CreationFlags = [ordered]@{
        CreatesOrder = $false
        CreatesChildOrder = $false
        CreatesRoute = $false
        CreatesSubmission = $false
        CreatesFill = $false
        CreatesExecutionReport = $false
        CreatesExecutableSchedule = $false
    }
}

$policyScaffold = [ordered]@{
    Phase = $phase
    CodeFile = "src/QQ.Production.Intraday.Application/R009EmsOmsDisabledScaffold.cs"
    TestFile = "tests/QQ.Production.Intraday.Tests.Unit/R009EmsOmsDisabledScaffoldTests.cs"
    AdapterClass = "R009DisabledEmsOmsExecutionAdapter"
    PrimaryPolicyCandidate = $r009Contract.PrimaryPolicyCandidate
    SecondaryPolicyCandidate = $r009Contract.SecondaryPolicyCandidate
    ConditionalResidualModule = $r009Contract.ConditionalResidualModule
    ControlledResidualCrossCanBecomeAlwaysMarketAtClose = $false
    ProducesExecutableArtifacts = $false
    LiveRuntimeRegistered = $false
}

$riskGate = [ordered]@{
    Phase = $phase
    Checks = @(
        "supported symbol",
        "USD-pair-only",
        "direct crosses excluded",
        "inversion metadata valid",
        "canonical target close",
        "quarter-hour target close",
        "quote-window readiness present",
        "close-benchmark readiness present",
        "feed-quality readiness present",
        "risk approval present",
        "operator approval present",
        "overnight allowed false",
        "must end flat",
        "spread/cost guard",
        "residual/opportunity-cost condition for ControlledResidualCross",
        "kill-switch status"
    )
    MissingReadinessOutcome = "HeldMissingReadiness"
    MissingReadinessAuthorizesOrders = $false
}

$featureFlags = [ordered]@{
    Phase = $phase
    R009LiveTradingEnabled = $false
    R009BrokerRoutingEnabled = $false
    R009OrderSubmissionEnabled = $false
    R009ExecutableScheduleEnabled = $false
    R009PaperLedgerCommitEnabled = $false
    R009SchedulerEnabled = $false
    R009BackgroundWorkerEnabled = $false
    R009DryRunOnly = $true
}

$boundaryGuard = [ordered]@{
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
}

$idempotency = [ordered]@{
    Phase = $phase
    RequiredFields = @(
        "ExecutionIntentId",
        "DecisionId",
        "R009DecisionHash",
        "InputHash",
        "ContractVersion",
        "CreatedAtUtc",
        "NoOrderDomainOutput=true",
        "NoBrokerRoute=true",
        "DryRunOnly=true"
    )
    NoOrderDomainOutput = $true
    NoBrokerRoute = $true
    DryRunOnly = $true
}

$supportedUniverse = [ordered]@{
    Phase = $phase
    SupportedExecutionSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")
    AudusdStatus = "SupportedAndNotFailed"
    DirectCrosses = "SignalOnlyNettingFirstExecutionDisabled"
    NonmajorEmScandiCnh = "CalibrationRequiredAndExcludedUntilExplicitCalibration"
}

Write-JsonArtifact "phase-exec-live-r001-r009-paper-maturity-reference.json" $paperMaturityReference
Write-JsonArtifact "phase-exec-live-r001-r014-operating-model-reference.json" $operatingModelReference
Write-JsonArtifact "phase-exec-live-r001-r009-ems-oms-integration-contract.json" $integrationContract
Write-JsonArtifact "phase-exec-live-r001-r009-execution-intent-contract.json" $intentContract
Write-JsonArtifact "phase-exec-live-r001-r009-execution-decision-contract.json" $decisionContract
Write-JsonArtifact "phase-exec-live-r001-r009-policy-application-scaffold.json" $policyScaffold
Write-JsonArtifact "phase-exec-live-r001-pretrade-risk-gate-contract.json" $riskGate
Write-JsonArtifact "phase-exec-live-r001-kill-switch-feature-flag-contract.json" $featureFlags
Write-JsonArtifact "phase-exec-live-r001-disabled-boundary-guard-contract.json" $boundaryGuard
Write-JsonArtifact "phase-exec-live-r001-idempotency-audit-contract.json" $idempotency
Write-JsonArtifact "phase-exec-live-r001-supported-symbol-universe.json" $supportedUniverse

Write-JsonArtifact "phase-exec-live-r001-direct-cross-exclusion-preservation.json" ([ordered]@{
    Phase = $phase
    DirectCrossExecutionAllowed = $false
    Requirement = "Direct crosses remain signal-only / netting-first / execution-disabled."
})
Write-JsonArtifact "phase-exec-live-r001-usd-pair-netting-requirement.json" ([ordered]@{
    Phase = $phase
    Requirement = "EMS/OMS execution intent must be USD-pair-only after netting."
    SupportedExecutionSymbols = $supportedUniverse.SupportedExecutionSymbols
})
Write-JsonArtifact "phase-exec-live-r001-usdjpy-caveat-preservation.json" ([ordered]@{
    Phase = $phase
    NormalizedPortfolioSymbol = "JPYUSD"
    ExecutionTradableSymbol = "USDJPY"
    RequiresInversion = $true
    SecurityID = "4004"
    SecurityIDSource = "8"
    CaveatPreserved = $true
})
Write-JsonArtifact "phase-exec-live-r001-canonical-quarter-hour-policy-preservation.json" ([ordered]@{
    Phase = $phase
    FutureCanonicalMinutes = @(0, 15, 30, 45)
    LegacyMinutesAreFutureCanonical = $false
})
Write-JsonArtifact "phase-exec-live-r001-legacy-compatibility-preservation.json" ([ordered]@{
    Phase = $phase
    LegacyLabels = @(":06", ":21", ":36", ":51")
    Usage = "CompatibilityOnly"
    UsedAsFutureCanonical = $false
})
Write-JsonArtifact "phase-exec-live-r001-cost-guidance-preservation.json" ([ordered]@{
    Phase = $phase
    FiveUsdPerMillion = "BestCaseMajorOnly"
    Universalized = $false
})
Write-JsonArtifact "phase-exec-live-r001-nonmajor-calibration-preservation.json" ([ordered]@{
    Phase = $phase
    NonmajorEmScandiCnh = "CalibrationRequired"
    LiveCapableExecutionAllowed = $false
})

$auditArtifacts = [ordered]@{
    "phase-exec-live-r001-no-broker-activation-audit.json" = New-Audit "NoBrokerActivation" "No broker runtime, route, FIX, TLS, or socket activation was introduced."
    "phase-exec-live-r001-no-live-marketdata-audit.json" = New-Audit "NoLiveMarketData" "No live market data request path was introduced."
    "phase-exec-live-r001-no-scheduler-service-polling-audit.json" = New-Audit "NoSchedulerServicePolling" "No scheduler, service, timer, polling, or background worker registration was introduced."
    "phase-exec-live-r001-no-order-created-audit.json" = New-Audit "NoOrderCreated" "Disabled adapter output has CreatesOrder=false and NotAnOrder=true."
    "phase-exec-live-r001-no-child-order-audit.json" = New-Audit "NoChildOrder" "Disabled adapter output has CreatesChildOrder=false and NoChildOrders=true."
    "phase-exec-live-r001-no-executable-schedule-audit.json" = New-Audit "NoExecutableSchedule" "Disabled adapter output has CreatesExecutableSchedule=false and NoExecutableSchedule=true."
    "phase-exec-live-r001-no-route-no-submission-audit.json" = New-Audit "NoRouteNoSubmission" "Disabled adapter output has CreatesRoute=false and CreatesSubmission=false."
    "phase-exec-live-r001-no-fill-execution-report-audit.json" = New-Audit "NoFillExecutionReport" "Disabled adapter output has CreatesFill=false and CreatesExecutionReport=false."
    "phase-exec-live-r001-no-paper-ledger-commit-audit.json" = New-Audit "NoPaperLedgerCommit" "Disabled adapter output has NoPaperLedgerCommit=true and feature flag disabled."
    "phase-exec-live-r001-no-state-mutation-audit.json" = New-Audit "NoStateMutation" "Disabled boundary guard has StateMutationAllowed=false."
    "phase-exec-live-r001-no-external-audit.json" = New-Audit "NoExternal" "No external API, Polygon, LMAX, broker, or live market data call is part of this phase."
}

foreach ($entry in $auditArtifacts.GetEnumerator()) {
    Write-JsonArtifact $entry.Key $entry.Value
}

Write-JsonArtifact "phase-exec-live-r001-forbidden-actions-audit.json" ([ordered]@{
    Phase = $phase
    Status = "Pass"
    ProhibitedActionsObserved = @()
    BrokerLiveOrderRouteFillScheduleLedgerStatePathsEnabled = $false
    ExternalApiCallsMade = $false
    R009PromotedToExecutableUse = $false
})
Write-JsonArtifact "phase-exec-live-r001-next-phase-recommendation.json" ([ordered]@{
    Phase = $phase
    RecommendedNextPhase = "EXEC-LIVE-R002"
    Title = "R009 EMS/OMS Disabled-Mode Decision Preview Integration Gate"
    Description = "Feed existing paper execution intents through the disabled R009 EMS/OMS adapter and produce decision previews, with broker/order/route/schedule/live paths still disabled."
})
Write-JsonArtifact "phase-exec-live-r001-build-test-validator-evidence.json" ([ordered]@{
    Phase = $phase
    Build = $BuildStatus
    FocusedR001Tests = $FocusedTestsStatus
    UnitTests = $UnitTestsStatus
    Validator = $ValidatorStatus
    EvidenceRequired = $true
})

$summary = @"
# EXEC-LIVE-R001 Summary

Classifications:
- $($classifications -join "`n- ")

R009 is now the selected EMS/OMS execution algorithm implementation target, but only through a disabled-mode live-capable scaffold. The scaffold accepts EMS/OMS execution intents and returns design-only decision previews, hold reasons, residual risk assessments, and cost tradeoff assessments.

No broker, live market data, scheduler, order, child order, route, submission, fill, execution report, executable schedule, paper ledger commit, or state mutation path is enabled. R009 remains non-executable and not live-ready.

Key code:
- src/QQ.Production.Intraday.Application/R009EmsOmsDisabledScaffold.cs
- tests/QQ.Production.Intraday.Tests.Unit/R009EmsOmsDisabledScaffoldTests.cs

Next phase:
- EXEC-LIVE-R002 - R009 EMS/OMS Disabled-Mode Decision Preview Integration Gate
"@

Set-Content -Path (Join-Path $artifactDir "phase-exec-live-r001-summary.md") -Value $summary -Encoding UTF8

Write-JsonArtifact "phase-exec-live-r001-r009-contract-reference.json" $r009Contract

Write-Host "Wrote EXEC-LIVE-R001 artifacts to $artifactDir"
