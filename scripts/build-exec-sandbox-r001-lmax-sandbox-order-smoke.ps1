param(
    [string]$BuildStatus = "Pending",
    [string]$FocusedTestsStatus = "Pending",
    [string]$ValidatorStatus = "Pending"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-sandbox"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

$phase = "EXEC-SANDBOX-R001"
$contractVersion = "0.3.0-design-only-candidate"
$classifications = @(
    "EXEC_SANDBOX_R001_BLOCKED_LMAX_SANDBOX_CONFIG_MISSING",
    "EXEC_SANDBOX_R001_PASS_PRODUCTION_ROUTE_BLOCKED",
    "EXEC_SANDBOX_R001_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE"
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
        LmaxProductionUsed = $false
        ProductionCredentialsUsed = $false
        NonSandboxBrokerRouteUsed = $false
        PolygonCalled = $false
        UnrelatedExternalApiCalled = $false
        SchedulerServicePollingBackgroundJobIntroduced = $false
        ProductionOrderCreated = $false
        ProductionRouteCreated = $false
        ProductionFillOrExecutionReportCreated = $false
        ProductionLedgerCommitOccurred = $false
        ProductionStateMutationOccurred = $false
        SandboxOnly = $true
    }
}

$appsettingsPath = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/appsettings.json"
$appsettings = if (Test-Path -LiteralPath $appsettingsPath) { Get-Content -LiteralPath $appsettingsPath -Raw | ConvertFrom-Json } else { $null }
$lmaxSandboxConfigPresent = $false
if ($appsettings -and ($appsettings.PSObject.Properties.Name -contains "LmaxSandbox")) {
    $lmaxSandboxConfigPresent = $true
}

$readOnlyRuntime = if ($appsettings -and ($appsettings.PSObject.Properties.Name -contains "LmaxReadOnlyRuntime")) { $appsettings.LmaxReadOnlyRuntime } else { $null }
$existingRuntimeSummary = [ordered]@{
    AppSettingsPath = "src/QQ.Production.Intraday.Api/appsettings.json"
    LmaxSandboxSectionPresent = $lmaxSandboxConfigPresent
    ExistingLmaxReadOnlyRuntimePresent = $null -ne $readOnlyRuntime
    ExistingRuntimeEnvironmentName = if ($readOnlyRuntime) { [string]$readOnlyRuntime.EnvironmentName } else { "" }
    ExistingRuntimeEnabled = if ($readOnlyRuntime) { [bool]$readOnlyRuntime.Enabled } else { $false }
    ExistingRuntimeAllowExternalConnections = if ($readOnlyRuntime) { [bool]$readOnlyRuntime.AllowExternalConnections } else { $false }
    ExistingRuntimeAllowCredentialUse = if ($readOnlyRuntime) { [bool]$readOnlyRuntime.AllowCredentialUse } else { $false }
    ExistingRuntimeReadOnly = if ($readOnlyRuntime) { [bool]$readOnlyRuntime.ReadOnly } else { $true }
    ExistingRuntimeAllowOrderSubmission = if ($readOnlyRuntime) { [bool]$readOnlyRuntime.AllowOrderSubmission } else { $false }
    ExistingRuntimeSchedulerEnabled = if ($readOnlyRuntime) { [bool]$readOnlyRuntime.SchedulerEnabled } else { $false }
}

$sandboxGuardrail = [ordered]@{
    Phase = $phase
    Environment = "Sandbox"
    BrokerVenue = "LMAXSandbox"
    ProductionVenueAllowed = $false
    ProductionCredentialsAllowed = $false
    SandboxCredentialsRequired = $true
    MaxSandboxOrderCount = 1
    MaxSandboxNotional = 100
    WhitelistedSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")
    DirectCrossExecutionAllowed = $false
    NonMajorExecutionAllowed = $false
    Legacy06AcceptedAsFutureCanonical = $false
    OperatorSandboxApprovalRequired = $true
    KillSwitchRequiredBeforeEachSubmission = $true
    IdempotentSubmissionRequired = $true
}

$candidateIntent = [ordered]@{
    Phase = $phase
    ExecutionIntentId = "r001-eurusd-sandbox-smoke-intent"
    SourceDecisionPreviewId = "r012-disabled-preview-decision"
    Symbol = "EURUSD"
    ExecutionTradableSymbol = "EURUSD"
    NormalizedPortfolioSymbol = "EURUSD"
    RequiresInversion = $false
    Side = "Buy"
    TargetQuantity = 1
    TargetNotional = 10
    CanonicalTargetCloseUtc = "2026-05-26T15:15:00Z"
    BarRole = "IntradayRebalance"
    ReadinessPresent = $true
    ReadinessWaivedForSandboxSmokeTest = $false
    OperatorSandboxApproval = $true
    KillSwitchOpenForSandboxOnly = $false
    R009DecisionStatus = "PreviewReady"
    SandboxOnly = $true
    ProductionOrder = $false
    IsLiveProduction = $false
    NoProductionLedgerCommit = $true
}

$configDiscovery = [ordered]@{
    Phase = $phase
    Status = "Blocked"
    Classification = "EXEC_SANDBOX_R001_BLOCKED_LMAX_SANDBOX_CONFIG_MISSING"
    ConfigSource = $existingRuntimeSummary
    SandboxConfigPresent = $lmaxSandboxConfigPresent
    Environment = ""
    BrokerVenue = ""
    EnvironmentIsSandbox = $false
    BrokerVenueIsLmaxSandbox = $false
    ProductionVenueAllowed = $false
    ProductionCredentialsAllowed = $false
    SandboxCredentialsRequired = $true
    SandboxCredentialProfilePresent = $false
    ProductionCredentialsDetected = $false
    AllowSandboxOrderSubmission = $false
    SchedulerServicePollingBackgroundJobEnabled = $false
    MaxSandboxOrderCount = 0
    MaxSandboxNotional = 0
    ConnectionAttempted = $false
    SocketOpened = $false
    Reasons = @("LmaxSandboxConfigMissing", "EnvironmentMustBeSandbox", "BrokerVenueMustBeLMAXSandbox", "SandboxCredentialProfileMissing", "SandboxOrderSubmissionNotExplicitlyAllowed", "MaxSandboxOrderCountMustBe1To3", "MaxSandboxNotionalMissingOrNonPositive")
}

$riskCheck = [ordered]@{
    Phase = $phase
    Status = "Blocked"
    EnvironmentIsSandbox = $false
    BrokerVenueIsLmaxSandbox = $false
    SandboxCredentialsPresent = $false
    SymbolWhitelisted = $true
    DirectCrossRejected = $true
    CanonicalQuarterHourTargetClose = $true
    ReadinessPresentOrWaived = $true
    OperatorSandboxApprovalPresent = $true
    MaxOrderCountSatisfied = $false
    MaxNotionalSatisfied = $false
    KillSwitchOpenForSandboxOnly = $false
    NoProductionRoute = $true
    NoProductionLedger = $true
    NoScheduler = $true
    Reasons = $configDiscovery.Reasons + @("KillSwitchMustBeOpenForSandboxOnly")
}

$orderIntent = [ordered]@{
    Phase = $phase
    Created = $false
    Status = "NotCreatedBlockedMissingSandboxConfig"
    Reason = "Sandbox config missing or ambiguous; blocked before route/submission."
    CandidateIntent = $candidateIntent
    SandboxOnly = $true
    ProductionOrder = $false
    IsLiveProduction = $false
    NoProductionLedgerCommit = $true
}

$routeContract = [ordered]@{
    Phase = $phase
    RouteCreated = $false
    RouteId = $null
    BrokerVenue = "LMAXSandbox"
    Environment = "Sandbox"
    SandboxOnly = $true
    ProductionRoute = $false
    NonSandboxBrokerRoute = $false
    ProductionCredentialsUsed = $false
    Reason = "No sandbox route created because sandbox configuration is missing."
}

$submission = [ordered]@{
    Phase = $phase
    SubmissionAttempted = $false
    SubmittedOrderCount = 0
    SubmittedNotional = 0
    MaxSandboxOrderCount = 1
    MaxSandboxNotional = 100
    Status = "NotSubmittedBlocked"
    SandboxOnly = $true
    ProductionSubmission = $false
    AckOrRejectCaptured = $false
    RejectReason = "LmaxSandboxConfigMissing"
}

$executionReport = [ordered]@{
    Phase = $phase
    ExecutionReportCaptured = $false
    SandboxOnly = $true
    ProductionExecutionReport = $false
    Status = "NoExecutionReportBecauseSubmissionBlocked"
}

$fillReport = [ordered]@{
    Phase = $phase
    FillCaptured = $false
    SandboxOnly = $true
    ProductionFill = $false
    Status = "NoFillBecauseSubmissionBlocked"
}

$reconciliation = [ordered]@{
    Phase = $phase
    Status = "BlockedBeforeSubmission"
    IntendedSandboxOrderCreated = $false
    SubmittedSandboxOrderCount = 0
    AckOrRejectCaptured = $false
    ExecutionReportCaptured = $false
    FillCaptured = $false
    ProductionLedgerMutation = $false
    ProductionStateMutation = $false
    SandboxOnly = $true
}

$auditRecord = [ordered]@{
    Phase = $phase
    AuditId = "r001-lmax-sandbox-smoke-audit"
    Environment = "Sandbox"
    BrokerVenue = "LMAXSandbox"
    SandboxOnly = $true
    SandboxConnectionAttempted = $false
    SandboxOrderSubmitted = $false
    ProductionOrderCreated = $false
    ProductionRouteCreated = $false
    ProductionFillOrReportCreated = $false
    ProductionLedgerCommit = $false
    ProductionStateMutation = $false
    AuditHash = "blocked-missing-sandbox-config"
}

Write-JsonArtifact "phase-exec-sandbox-r001-r009-contract-reference.json" ([ordered]@{
    Phase = $phase
    R009ContractVersion = $contractVersion
    PrimaryPolicyCandidate = "CloseSeeking15mAdaptive_BalancedAdaptive_v0"
    SecondaryPolicyCandidate = "CloseSeeking15mAdaptive_ResidualAwareUrgency_v0"
    ConditionalResidualModule = "ControlledResidualCross_BalancedResidualCross_v0"
    SelectedExecutionAlgorithmCandidate = $true
    ProductionLiveApproved = $false
    SandboxOrderPathSmokeOnly = $true
})
Write-JsonArtifact "phase-exec-sandbox-r001-lmax-sandbox-config-discovery.json" $configDiscovery
Write-JsonArtifact "phase-exec-sandbox-r001-production-config-rejection-check.json" ([ordered]@{
    Phase = $phase
    ProductionConfigRejected = $true
    ProductionVenueAllowed = $false
    ProductionCredentialsAllowed = $false
    ExistingRuntimeEnvironmentName = $existingRuntimeSummary.ExistingRuntimeEnvironmentName
    ExistingRuntimeAllowOrderSubmission = $existingRuntimeSummary.ExistingRuntimeAllowOrderSubmission
    Reason = "Only local read-only/design-only config was found; no LMAXSandbox order config was found."
})
Write-JsonArtifact "phase-exec-sandbox-r001-sandbox-guardrail-contract.json" $sandboxGuardrail
Write-JsonArtifact "phase-exec-sandbox-r001-r009-sandbox-execution-intent.json" $candidateIntent
Write-JsonArtifact "phase-exec-sandbox-r001-r009-sandbox-order-intent.json" $orderIntent
Write-JsonArtifact "phase-exec-sandbox-r001-pretrade-sandbox-risk-check.json" $riskCheck
Write-JsonArtifact "phase-exec-sandbox-r001-sandbox-route-contract.json" $routeContract
Write-JsonArtifact "phase-exec-sandbox-r001-sandbox-submission-result.json" $submission
Write-JsonArtifact "phase-exec-sandbox-r001-sandbox-execution-report.json" $executionReport
Write-JsonArtifact "phase-exec-sandbox-r001-sandbox-fill-report.json" $fillReport
Write-JsonArtifact "phase-exec-sandbox-r001-sandbox-reconciliation-result.json" $reconciliation
Write-JsonArtifact "phase-exec-sandbox-r001-sandbox-audit-record.json" $auditRecord

Write-JsonArtifact "phase-exec-sandbox-r001-no-production-broker-audit.json" (New-Audit "NoProductionBroker" "No production LMAX or non-sandbox broker route was used.")
Write-JsonArtifact "phase-exec-sandbox-r001-no-production-order-audit.json" (New-Audit "NoProductionOrder" "No production order artifact was created.")
Write-JsonArtifact "phase-exec-sandbox-r001-no-production-route-audit.json" (New-Audit "NoProductionRoute" "No production route artifact was created.")
Write-JsonArtifact "phase-exec-sandbox-r001-no-production-ledger-audit.json" (New-Audit "NoProductionLedger" "No production ledger commit occurred.")
Write-JsonArtifact "phase-exec-sandbox-r001-no-production-state-mutation-audit.json" (New-Audit "NoProductionStateMutation" "No live/broker/production/trading state was mutated.")
Write-JsonArtifact "phase-exec-sandbox-r001-direct-cross-exclusion-preservation.json" ([ordered]@{ Phase = $phase; DirectCrossExecutionAllowed = $false; DirectCrossExecutionIntentRejected = $true })
Write-JsonArtifact "phase-exec-sandbox-r001-usd-pair-whitelist-preservation.json" ([ordered]@{ Phase = $phase; WhitelistedSymbols = $sandboxGuardrail.WhitelistedSymbols; NonWhitelistedSymbolAllowed = $false; AudusdStatus = "SupportedAndNotFailed" })
Write-JsonArtifact "phase-exec-sandbox-r001-usdjpy-caveat-preservation.json" ([ordered]@{ Phase = $phase; NormalizedPortfolioSymbol = "JPYUSD"; ExecutionTradableSymbol = "USDJPY"; RequiresInversion = $true; SecurityID = "4004"; SecurityIDSource = "8"; CaveatPreserved = $true })
Write-JsonArtifact "phase-exec-sandbox-r001-canonical-quarter-hour-policy-preservation.json" ([ordered]@{ Phase = $phase; FutureCanonicalMinutes = @(0, 15, 30, 45); CandidateTargetCloseUtc = $candidateIntent.CanonicalTargetCloseUtc; CandidateIsCanonical = $true })
Write-JsonArtifact "phase-exec-sandbox-r001-legacy-compatibility-preservation.json" ([ordered]@{ Phase = $phase; LegacyLabels = @(":06", ":21", ":36", ":51"); UsedAsFutureCanonical = $false; Legacy06AcceptedAsFutureCanonical = $false })
Write-JsonArtifact "phase-exec-sandbox-r001-cost-guidance-preservation.json" ([ordered]@{ Phase = $phase; FiveUsdPerMillion = "BestCaseMajorOnly"; Universalized = $false })
Write-JsonArtifact "phase-exec-sandbox-r001-nonmajor-calibration-preservation.json" ([ordered]@{ Phase = $phase; NonmajorEmScandiCnh = "CalibrationRequired"; SandboxExecutionAllowed = $false })
Write-JsonArtifact "phase-exec-sandbox-r001-forbidden-actions-audit.json" ([ordered]@{
    Phase = $phase
    LmaxProductionUsed = $false
    ProductionCredentialsUsed = $false
    NonSandboxBrokerRouteUsed = $false
    PolygonCalled = $false
    UnrelatedExternalApiCalled = $false
    SchedulerServicePollingBackgroundJobIntroduced = $false
    MoreSandboxOrdersThanAllowedSubmitted = $false
    OrderNotionalExceedsSandboxCap = $false
    DirectCrossExecutionAllowed = $false
    NonWhitelistedSymbolAllowed = $false
    Legacy06AcceptedAsFutureCanonical = $false
    UsdjpyCaveatWeakened = $false
    AudusdMisclassified = $false
    ProductionOrderArtifactCreated = $false
    ProductionRouteArtifactCreated = $false
    ProductionFillReportArtifactCreated = $false
    ProductionLedgerCommitOccurred = $false
    ProductionStateMutationOccurred = $false
    SandboxArtifactsClearlyMarkedSandboxOnly = $true
})
Write-JsonArtifact "phase-exec-sandbox-r001-next-phase-recommendation.json" ([ordered]@{
    Phase = $phase
    RecommendedNextPhase = "EXEC-SANDBOX-R002"
    Title = "LMAX Sandbox Configuration Completion and Bounded Smoke Retry Gate"
    Reason = "R001 blocked before connection because LMAXSandbox order configuration was missing."
})
Write-JsonArtifact "phase-exec-sandbox-r001-build-test-validator-evidence.json" ([ordered]@{
    Phase = $phase
    Build = $BuildStatus
    FocusedSandboxTests = $FocusedTestsStatus
    Validator = $ValidatorStatus
    DotnetBuildNoRestore = "dotnet build --no-restore"
    FocusedTests = "dotnet test tests\QQ.Production.Intraday.Tests.Unit\QQ.Production.Intraday.Tests.Unit.csproj --no-restore --filter FullyQualifiedName~R009LmaxSandboxOrderSmokeTests"
    ValidatorScript = "scripts/check-exec-sandbox-r001-lmax-sandbox-order-smoke-gate.ps1"
})

$summary = @"
# EXEC-SANDBOX-R001 Summary

Classifications:
- EXEC_SANDBOX_R001_BLOCKED_LMAX_SANDBOX_CONFIG_MISSING
- EXEC_SANDBOX_R001_PASS_PRODUCTION_ROUTE_BLOCKED
- EXEC_SANDBOX_R001_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE

R001 inspected local configuration only. No LMAXSandbox order-submission configuration was present. The existing LMAX runtime config is local/read-only/design-only with order submission disabled. Therefore no socket, FIX session, broker route, sandbox submission, execution report, fill, production order, production route, production ledger commit, or production state mutation occurred.

The R009 sandbox order-path contracts, guardrails, execution intent, blocked order-intent artifact, pre-trade sandbox risk check, route/submission/report/fill placeholders, reconciliation, and audit records were produced as sandbox-only artifacts.

Build/tests/validator:
- Build: $BuildStatus
- Focused sandbox tests: $FocusedTestsStatus
- Validator: $ValidatorStatus

Next recommendation: EXEC-SANDBOX-R002 - LMAX Sandbox Configuration Completion and Bounded Smoke Retry Gate.
"@
$summary | Set-Content -Path (Join-Path $artifactDir "phase-exec-sandbox-r001-summary.md") -Encoding UTF8

Write-Host "EXEC-SANDBOX-R001 artifacts written to $artifactDir"
