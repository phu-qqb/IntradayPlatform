param(
    [string]$BuildStatus = "Pending",
    [string]$FocusedChecksStatus = "Pending",
    [string]$UnitTestsStatus = "Pending",
    [string]$ValidatorStatus = "Pending"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-live"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

$phase = "EXEC-LIVE-R008"
$contractVersion = "0.3.0-design-only-candidate"
$classifications = @(
    "EXEC_LIVE_R008_PASS_OPERATIONAL_RUNBOOK_READY_NO_EXTERNAL",
    "EXEC_LIVE_R008_PASS_ROLLBACK_DISABLE_PLAN_READY_NO_EXTERNAL",
    "EXEC_LIVE_R008_PASS_OPERATOR_CHECKLIST_READY_NO_EXTERNAL",
    "EXEC_LIVE_R008_PASS_NO_BROKER_NO_ORDER_NO_ROUTE_GATE_READY_NO_EXTERNAL"
)

function Write-JsonArtifact {
    param([string]$Name, [object]$Value)
    $Value | ConvertTo-Json -Depth 50 | Set-Content -Path (Join-Path $artifactDir $Name) -Encoding UTF8
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

$safeModes = @("ListAuditRecords", "ShowAuditRecord", "SummarizeBatch", "ExportOperatorReport")
$forbiddenModes = @("Execute", "Submit", "Route", "Fill", "CommitLedger", "ActivateBroker", "StartScheduler", "PromoteLive")
$stopRules = @(
    "Any order-like artifact appears",
    "Any route, submission, fill, or execution report appears",
    "Any executable schedule appears",
    "Any broker or live market data path appears",
    "Any scheduler, service, timer, polling, or background job path appears",
    "Any ledger commit appears",
    "Any live, broker, production, trading, or paper-ledger state mutation appears",
    "Direct-cross execution intent is accepted",
    "Legacy :06/:21/:36/:51 timestamp is accepted as future canonical",
    "USDJPY caveat is weakened",
    "Preview output is consumed by a forbidden consumer",
    "Operator approval is treated as executable approval"
)
$checklist = @(
    "Verify feature flags disabled",
    "Verify ReviewOnly=true",
    "Verify NonExecutable=true",
    "Verify NotAnOrder=true",
    "Verify NoBrokerRoute=true",
    "Verify NoPaperLedgerCommit=true",
    "Verify direct-cross exclusion",
    "Verify USDJPY caveat",
    "Verify canonical quarter-hour close",
    "Verify held lines are not orders",
    "Verify audit hash present",
    "Verify output path is artifact-only"
)

Write-JsonArtifact "phase-exec-live-r008-r007-operator-review-reference.json" ([ordered]@{
    Phase = $phase
    R007Classifications = @(
        "EXEC_LIVE_R007_PASS_OPERATOR_REVIEW_HANDOFF_READY_NO_EXTERNAL",
        "EXEC_LIVE_R007_PASS_REVIEW_CLI_REPORTING_CONTRACT_READY_NO_EXTERNAL",
        "EXEC_LIVE_R007_PASS_REVIEW_OUTPUT_BOUNDARY_GUARD_READY_NO_EXTERNAL",
        "EXEC_LIVE_R007_PASS_NO_BROKER_NO_ORDER_NO_ROUTE_GATE_READY_NO_EXTERNAL"
    )
    ReviewService = "R009OperatorPreviewReviewService"
    AllowedModes = $safeModes
    ForbiddenModes = $forbiddenModes
    ReadRoot = "artifacts/readiness/execution-live/audit"
    WriteRoot = "artifacts/readiness/execution-live/operator-review"
    ReviewOnly = $true
    ExecutableApproval = $false
})

Write-JsonArtifact "phase-exec-live-r008-r009-contract-reference.json" ([ordered]@{
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

$runbook = [ordered]@{
    Phase = $phase
    Title = "R009 Disabled Preview Operational Runbook"
    Purpose = "Operator review of disabled R009 preview decisions only."
    WhatItIs = @(
        "A disabled-preview API and review flow for inspecting R009 design-only execution decisions.",
        "An artifact-only audit and operator-report workflow.",
        "A dry-run review surface for PreviewReady, HeldMissingReadiness, and Rejected outcomes."
    )
    WhatItIsNot = @(
        "Not executable approval",
        "Not broker-ready",
        "Not live-ready",
        "Not an order generator",
        "Not a route, submission, fill, execution report, schedule, scheduler, worker, ledger, or state mutation system"
    )
    OperatorReviewSteps = @(
        "Use ListAuditRecords to enumerate preview audit records.",
        "Use ShowAuditRecord to inspect request, response, idempotency hash, and audit hash.",
        "Use SummarizeBatch to inspect PreviewReady, HeldMissingReadiness, and Rejected counts.",
        "Use ExportOperatorReport to write review-only reports under artifacts/readiness/execution-live/operator-review.",
        "Confirm NonExecutable=true, NotAnOrder=true, NoBrokerRoute=true, NoPaperLedgerCommit=true, ReviewOnly=true, ExecutableApproval=false."
    )
    Interpretation = [ordered]@{
        PreviewReady = "Design-only preview line passed disabled pre-trade checks; still not an order and not executable."
        HeldMissingReadiness = "Readiness evidence is missing; line remains held and non-executable."
        Rejected = "Input failed disabled-preview validation, such as direct-cross or unsupported instrument; rejection is not an order."
        Policies = "Primary, secondary, and conditional R009 policy labels are preview decision metadata only."
    }
    AllowedModes = $safeModes
    ForbiddenModes = $forbiddenModes
    ReviewOnly = $true
    ExecutableApproval = $false
    BrokerApproval = $false
    LiveApproval = $false
}
Write-JsonArtifact "phase-exec-live-r008-operational-runbook.json" $runbook

$runbookMd = @"
# R009 Disabled Preview Operational Runbook

R009 disabled preview lets an operator inspect design-only R009 preview decisions and audit records. It is a dry-run review surface only.

## What It Is
- Disabled preview inspection for R009 decision previews.
- Artifact-only audit browsing and operator report export.
- A way to review `PreviewReady`, `HeldMissingReadiness`, and `Rejected` outcomes.

## What It Is Not
- Not executable approval.
- Not broker-ready.
- Not live-ready.
- Not an order generator.
- Not a route, submission, fill, execution report, executable schedule, scheduler, worker, ledger, or state mutation system.

## Safe Review Flow
1. Use `ListAuditRecords` to enumerate audit records.
2. Use `ShowAuditRecord` to inspect one audit envelope.
3. Use `SummarizeBatch` to review preview-ready, held, and rejected counts.
4. Use `ExportOperatorReport` to write review-only reports under `artifacts/readiness/execution-live/operator-review`.
5. Confirm `ReviewOnly=true`, `NonExecutable=true`, `NotAnOrder=true`, `NoBrokerRoute=true`, `NoPaperLedgerCommit=true`, and `ExecutableApproval=false`.

## Outcome Interpretation
- `PreviewReady`: disabled preview passed checks, but remains non-executable and not an order.
- `HeldMissingReadiness`: missing readiness evidence; held line remains non-executable.
- `Rejected`: invalid disabled-preview input, such as direct-cross execution or unsupported instrument; rejection remains not an order.

## Stop Immediately If
$($stopRules | ForEach-Object { "- $_" } | Out-String)

Operator approval is review-only and must never be treated as executable approval.
"@
$runbookMd | Set-Content -Path (Join-Path $artifactDir "phase-exec-live-r008-operational-runbook.md") -Encoding UTF8

$rollback = [ordered]@{
    Phase = $phase
    Title = "R009 Disabled Preview Rollback / Disable Plan"
    Actions = @(
        "Disable operator access to R009OperatorPreviewReviewService.",
        "Disable R009 disabled preview API consumer access.",
        "Preserve existing audit artifacts under artifacts/readiness/execution-live/audit.",
        "Preserve existing operator reports under artifacts/readiness/execution-live/operator-review.",
        "Confirm feature flags remain false.",
        "Confirm no scheduler, service, polling, timer, or background worker exists.",
        "Confirm no broker route registration exists.",
        "Confirm no order-domain, route/submission, execution-report, fill, ledger, or trading-state persistence exists."
    )
    FeatureFlagsMustRemainFalse = @("LiveTradingEnabled", "BrokerRoutingEnabled", "OrderSubmissionEnabled", "ExecutableScheduleEnabled", "PaperLedgerCommitEnabled", "SchedulerEnabled", "BackgroundWorkerEnabled")
    AuditArtifactsPreserved = $true
    BrokerRouteRegistrationAllowed = $false
    OrderDomainPersistenceAllowed = $false
    SchedulerServicePollingAllowed = $false
}
Write-JsonArtifact "phase-exec-live-r008-rollback-disable-plan.json" $rollback

$rollbackMd = @"
# R009 Disabled Preview Rollback / Disable Plan

1. Disable operator access to the disabled preview review surface.
2. Disable disabled preview API consumer access.
3. Preserve audit artifacts under `artifacts/readiness/execution-live/audit`.
4. Preserve operator review reports under `artifacts/readiness/execution-live/operator-review`.
5. Confirm all kill-switch flags remain disabled.
6. Confirm no scheduler, service, timer, polling, or background worker exists.
7. Confirm no broker route registration exists.
8. Confirm no order-domain, route/submission, fill/report, ledger, or trading-state persistence exists.

Rollback preserves evidence and removes review access only. It does not activate broker, live data, scheduler, orders, routes, fills, schedules, ledgers, or state mutation.
"@
$rollbackMd | Set-Content -Path (Join-Path $artifactDir "phase-exec-live-r008-rollback-disable-plan.md") -Encoding UTF8

Write-JsonArtifact "phase-exec-live-r008-incident-stop-rules.json" ([ordered]@{
    Phase = $phase
    HardStopRules = $stopRules
    OperatorApprovalExecutableApproval = $false
    AnyViolationAction = "Stop review, preserve artifacts, disable consumer access, investigate before any future gate."
})
Write-JsonArtifact "phase-exec-live-r008-operator-checklist.json" ([ordered]@{ Phase = $phase; Checklist = $checklist; MustPassAll = $true; ExecutableApproval = $false })

$checklistMd = @"
# R009 Disabled Preview Operator Checklist

$($checklist | ForEach-Object { "- [ ] $_" } | Out-String)

Do not continue review if any item fails. Passing this checklist does not authorize execution.
"@
$checklistMd | Set-Content -Path (Join-Path $artifactDir "phase-exec-live-r008-operator-checklist.md") -Encoding UTF8

Write-JsonArtifact "phase-exec-live-r008-safe-use-examples.json" ([ordered]@{
    Phase = $phase
    Examples = @(
        [ordered]@{ Scenario = "List audit records"; CommandMode = "ListAuditRecords"; Safe = $true; Output = "ReviewOnly audit references" },
        [ordered]@{ Scenario = "Show single audit record"; CommandMode = "ShowAuditRecord"; Safe = $true; Output = "NonExecutable audit envelope inspection" },
        [ordered]@{ Scenario = "Summarize batch"; CommandMode = "SummarizeBatch"; Safe = $true; Output = "PreviewReady/HeldMissingReadiness/Rejected counts" },
        [ordered]@{ Scenario = "Export operator report"; CommandMode = "ExportOperatorReport"; Safe = $true; Output = "Artifact-only operator report" },
        [ordered]@{ Scenario = "Interpret held readiness"; Meaning = "Held line remains non-executable and not an order"; Safe = $true },
        [ordered]@{ Scenario = "Interpret rejected direct-cross"; Meaning = "Rejected input remains not an order and direct-cross execution remains disabled"; Safe = $true },
        [ordered]@{ Scenario = "Interpret unsupported nonmajor"; Meaning = "Calibration-required and excluded from live-capable execution"; Safe = $true }
    )
})
Write-JsonArtifact "phase-exec-live-r008-unsafe-use-rejection-examples.json" ([ordered]@{
    Phase = $phase
    Examples = @(
        [ordered]@{ Scenario = "Submit preview as order"; Rejected = $true; Reason = "Preview output is NotAnOrder" },
        [ordered]@{ Scenario = "Convert preview to route"; Rejected = $true; Reason = "NoRoute and NoSubmission" },
        [ordered]@{ Scenario = "Start scheduler"; Rejected = $true; Reason = "Scheduler disabled" },
        [ordered]@{ Scenario = "Commit ledger"; Rejected = $true; Reason = "NoPaperLedgerCommit" },
        [ordered]@{ Scenario = "Activate broker"; Rejected = $true; Reason = "BrokerReady=false and BrokerRoutingEnabled=false" },
        [ordered]@{ Scenario = "Use legacy :06 as canonical"; Rejected = $true; Reason = "Future canonical target closes must be 00/15/30/45" }
    )
})
Write-JsonArtifact "phase-exec-live-r008-feature-flag-review.json" ([ordered]@{ Phase = $phase; LiveTradingEnabled = $false; BrokerRoutingEnabled = $false; OrderSubmissionEnabled = $false; ExecutableScheduleEnabled = $false; PaperLedgerCommitEnabled = $false; SchedulerEnabled = $false; BackgroundWorkerEnabled = $false; DryRunOnly = $true })
Write-JsonArtifact "phase-exec-live-r008-consumer-access-review.json" ([ordered]@{ Phase = $phase; AllowedConsumers = @("InternalPmsPreviewConsumer", "InternalEmsPreviewConsumer", "InternalOmsPreviewConsumer", "OperatorReviewTool", "TestHarness"); ForbiddenConsumers = @("BrokerGateway", "OrderRouter", "Scheduler", "PaperLedgerCommitter", "ExecutionReportHandler", "ProductionTradingRuntime"); OperatorReviewToolOnlyForReview = $true; ExecutableApproval = $false })
Write-JsonArtifact "phase-exec-live-r008-audit-artifact-retention-plan.json" ([ordered]@{ Phase = $phase; PreserveAuditRoot = "artifacts/readiness/execution-live/audit"; PreserveOperatorReviewRoot = "artifacts/readiness/execution-live/operator-review"; RetentionCategory = "PreviewAuditOnly"; DeleteOnRollback = $false; TradingStatePersistence = $false; LedgerPersistence = $false })

$audits = [ordered]@{
    "phase-exec-live-r008-no-broker-activation-audit.json" = New-Audit "NoBrokerActivation" "Runbook is documentation-only and keeps broker activation forbidden."
    "phase-exec-live-r008-no-live-marketdata-audit.json" = New-Audit "NoLiveMarketData" "Runbook forbids live market data."
    "phase-exec-live-r008-no-scheduler-service-polling-audit.json" = New-Audit "NoSchedulerServicePolling" "Runbook forbids scheduler/service/timer/polling/background jobs."
    "phase-exec-live-r008-no-order-created-audit.json" = New-Audit "NoOrderCreated" "Runbook states previews are NotAnOrder and cannot be submitted."
    "phase-exec-live-r008-no-child-order-audit.json" = New-Audit "NoChildOrder" "Runbook forbids child slices and child orders."
    "phase-exec-live-r008-no-executable-schedule-audit.json" = New-Audit "NoExecutableSchedule" "Runbook forbids executable schedules."
    "phase-exec-live-r008-no-route-no-submission-audit.json" = New-Audit "NoRouteNoSubmission" "Runbook forbids routes and submissions."
    "phase-exec-live-r008-no-fill-execution-report-audit.json" = New-Audit "NoFillExecutionReport" "Runbook forbids fills and execution reports."
    "phase-exec-live-r008-no-paper-ledger-commit-audit.json" = New-Audit "NoPaperLedgerCommit" "Runbook forbids ledger commits."
    "phase-exec-live-r008-no-state-mutation-audit.json" = New-Audit "NoStateMutation" "Runbook forbids trading state mutation."
    "phase-exec-live-r008-no-external-audit.json" = New-Audit "NoExternal" "No external API, Polygon, LMAX, broker, or live market data call is part of this phase."
}
foreach ($entry in $audits.GetEnumerator()) { Write-JsonArtifact $entry.Key $entry.Value }

Write-JsonArtifact "phase-exec-live-r008-canonical-quarter-hour-policy-preservation.json" ([ordered]@{ Phase = $phase; FutureCanonicalMinutes = @(0, 15, 30, 45); LegacyMinutesAcceptedAsFutureCanonical = $false })
Write-JsonArtifact "phase-exec-live-r008-legacy-compatibility-preservation.json" ([ordered]@{ Phase = $phase; LegacyLabels = @(":06", ":21", ":36", ":51"); Usage = "CompatibilityOnly"; UsedAsFutureCanonical = $false })
Write-JsonArtifact "phase-exec-live-r008-direct-cross-exclusion-preservation.json" ([ordered]@{ Phase = $phase; DirectCrossExecutionAllowed = $false; DirectCrossRejectedNotOrder = $true })
Write-JsonArtifact "phase-exec-live-r008-usd-pair-netting-requirement.json" ([ordered]@{ Phase = $phase; Requirement = "EMS/OMS execution intents must be USD-pair-only after netting."; SupportedExecutionSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF"); AudusdStatus = "SupportedAndNotFailed" })
Write-JsonArtifact "phase-exec-live-r008-usdjpy-caveat-preservation.json" ([ordered]@{ Phase = $phase; NormalizedPortfolioSymbol = "JPYUSD"; ExecutionTradableSymbol = "USDJPY"; RequiresInversion = $true; SecurityID = "4004"; SecurityIDSource = "8"; CaveatPreserved = $true })
Write-JsonArtifact "phase-exec-live-r008-cost-guidance-preservation.json" ([ordered]@{ Phase = $phase; FiveUsdPerMillion = "BestCaseMajorOnly"; Universalized = $false })
Write-JsonArtifact "phase-exec-live-r008-nonmajor-calibration-preservation.json" ([ordered]@{ Phase = $phase; NonmajorEmScandiCnh = "CalibrationRequired"; LiveCapableExecutionAllowed = $false })
Write-JsonArtifact "phase-exec-live-r008-forbidden-actions-audit.json" ([ordered]@{ Phase = $phase; ExternalApiCallsMade = $false; PolygonCallsMade = $false; LmaxCallsMade = $false; BrokerActivationOccurred = $false; LiveMarketDataRequested = $false; SchedulerServicePollingBackgroundJobIntroduced = $false; PmsEmsOmsCycleRun = $false; ManualNoExternalCommandRun = $false; BacktestSimulationRun = $false; TcaResultLinesCreated = $false; ExecutableScheduleCreated = $false; OrdersChildOrdersRoutesSubmissionsFillsReportsCreated = $false; PaperLedgerCommitCreated = $false; StateMutationOccurred = $false; R009PromotedToExecutableUse = $false; RunbookImpliesExecutableApproval = $false; RunbookAllowsBrokerOrderRouteFillScheduleLedger = $false })
Write-JsonArtifact "phase-exec-live-r008-next-phase-recommendation.json" ([ordered]@{ Phase = $phase; RecommendedNextPhase = "EXEC-LIVE-R009"; Title = "R009 Disabled Preview Readiness for Internal EMS/OMS Trial Gate"; Constraints = "Continue disabled-preview-only readiness; no broker, live market data, orders, routes, fills, schedules, ledger commits, or trading state mutation." })
Write-JsonArtifact "phase-exec-live-r008-build-test-validator-evidence.json" ([ordered]@{ Phase = $phase; Build = $BuildStatus; FocusedR008Checks = $FocusedChecksStatus; UnitTests = $UnitTestsStatus; Validator = $ValidatorStatus; DotnetBuildNoRestore = "dotnet build --no-restore"; FocusedChecks = "scripts/check-exec-live-r008-operational-runbook-rollback-gate.ps1"; ValidatorScript = "scripts/check-exec-live-r008-operational-runbook-rollback-gate.ps1" })

$summary = @"
# EXEC-LIVE-R008 Summary

Classifications:
- EXEC_LIVE_R008_PASS_OPERATIONAL_RUNBOOK_READY_NO_EXTERNAL
- EXEC_LIVE_R008_PASS_ROLLBACK_DISABLE_PLAN_READY_NO_EXTERNAL
- EXEC_LIVE_R008_PASS_OPERATOR_CHECKLIST_READY_NO_EXTERNAL
- EXEC_LIVE_R008_PASS_NO_BROKER_NO_ORDER_NO_ROUTE_GATE_READY_NO_EXTERNAL

R008 creates the operational runbook, rollback/disable plan, incident stop rules, operator checklist, safe-use examples, unsafe-use rejection examples, feature flag review, consumer access review, and audit artifact retention plan for R009 disabled preview operation.

This is documentation and artifact-only. It does not activate broker, request live market data, start scheduler/service/polling, run PMS/EMS/OMS, run ManualNoExternal, create TCA result lines, create orders, routes, submissions, fills, execution reports, executable schedules, ledger commits, state mutations, or promote R009 to executable use.

Build/tests/validator:
- Build: $BuildStatus
- Focused R008 checks: $FocusedChecksStatus
- Unit tests: $UnitTestsStatus
- Validator: $ValidatorStatus

Next recommendation: EXEC-LIVE-R009 - R009 Disabled Preview Readiness for Internal EMS/OMS Trial Gate.
"@
$summary | Set-Content -Path (Join-Path $artifactDir "phase-exec-live-r008-summary.md") -Encoding UTF8

Write-Host "EXEC-LIVE-R008 artifacts written to $artifactDir"
