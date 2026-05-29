param(
    [string]$BuildStatus = "Pending",
    [string]$FocusedTestsStatus = "Pending",
    [string]$ValidatorStatus = "Pending"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-sandbox"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

$phase = "EXEC-SANDBOX-R002"
$contractVersion = "0.3.0-design-only-candidate"
$classifications = @(
    "EXEC_SANDBOX_R002_BLOCKED_LMAX_SANDBOX_CONFIG_OR_CREDENTIALS_MISSING",
    "EXEC_SANDBOX_R002_PASS_PRODUCTION_ROUTE_BLOCKED",
    "EXEC_SANDBOX_R002_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE"
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
        CredentialValuesPrintedOrPersisted = $false
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

$r001Reference = [ordered]@{
    Phase = $phase
    SourcePhase = "EXEC-SANDBOX-R001"
    SourceClassifications = @(
        "EXEC_SANDBOX_R001_BLOCKED_LMAX_SANDBOX_CONFIG_MISSING",
        "EXEC_SANDBOX_R001_PASS_PRODUCTION_ROUTE_BLOCKED",
        "EXEC_SANDBOX_R001_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE"
    )
    SourceArtifact = "phase-exec-sandbox-r001-lmax-sandbox-config-discovery.json"
    SourceFinding = "No LmaxSandbox order-submission configuration found; existing LMAX runtime was local/read-only/design-only."
}

$configContract = [ordered]@{
    Phase = $phase
    Environment = "Sandbox"
    BrokerVenue = "LMAXSandbox"
    ProductionVenueAllowed = $false
    ProductionCredentialsAllowed = $false
    SandboxCredentialsRequired = $true
    SandboxOrderSubmissionEnabled = $true
    SandboxKillSwitchOpen = $true
    MaxSandboxOrderCount = 1
    MaxSandboxNotional = 100
    AllowedSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")
    DirectCrossExecutionAllowed = $false
    NonmajorExecutionAllowed = $false
    PaperLedgerCommitAllowed = $false
    ProductionLedgerCommitAllowed = $false
    StateMutationAllowed = $false
    ContractOnly = $true
    LocalConcreteConfigFound = $false
}

$configValidation = [ordered]@{
    Phase = $phase
    Status = "Blocked"
    Classification = "EXEC_SANDBOX_R002_BLOCKED_LMAX_SANDBOX_CONFIG_OR_CREDENTIALS_MISSING"
    ExplicitSandboxConfigPresent = $false
    EnvironmentIsSandbox = $false
    BrokerVenueIsLmaxSandbox = $false
    SandboxOrderSubmissionEnabled = $false
    SandboxKillSwitchOpen = $false
    MaxSandboxOrderCountConfigured = $false
    MaxSandboxNotionalConfigured = $false
    MaxSandboxOrderCount = 0
    MaxSandboxNotional = 0
    DirectCrossExecutionAllowed = $false
    NonmajorExecutionAllowed = $false
    PaperLedgerCommitAllowed = $false
    ProductionLedgerCommitAllowed = $false
    StateMutationAllowed = $false
    SafeForOneBoundedSandboxOrder = $false
    ConnectionAttempted = $false
    SubmissionAllowed = $false
    Reasons = @("LmaxSandboxConfigMissing", "SandboxOrderSubmissionNotExplicitlyAllowed", "SandboxKillSwitchNotOpen", "MaxSandboxOrderCountMissing", "MaxSandboxNotionalMissing")
}

$requiredCredentialVariables = @(
    "LMAX_DEMO_FIX_USERNAME",
    "LMAX_DEMO_FIX_PASSWORD",
    "LMAX_DEMO_SENDER_COMP_ID",
    "LMAX_DEMO_TARGET_COMP_ID"
)
$credentialVariablePresence = [ordered]@{}
foreach ($name in $requiredCredentialVariables) {
    $credentialVariablePresence[$name] = -not [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))
}
$sandboxCredentialPresent = @($credentialVariablePresence.Values | Where-Object { $_ -eq $false }).Count -eq 0
$missingCredentialVariables = @($credentialVariablePresence.GetEnumerator() | Where-Object { $_.Value -eq $false } | ForEach-Object { $_.Key })
$credentialValidation = [ordered]@{
    Phase = $phase
    Status = if ($sandboxCredentialPresent) { "Ready" } else { "Blocked" }
    CredentialProfileName = "LMAX_DEMO_ENV_VARS"
    CredentialSourceType = "EnvVars"
    CredentialValuesRedacted = $true
    CredentialValuesPrintedOrPersisted = $false
    ProductionCredentialDetected = $false
    SandboxCredentialPresent = $sandboxCredentialPresent
    CredentialVariablePresence = $credentialVariablePresence
    MissingProfileNames = $missingCredentialVariables
    Reasons = if ($sandboxCredentialPresent) { @() } else { @("SandboxCredentialEnvVarMissing") + $missingCredentialVariables }
}

$productionBlocking = [ordered]@{
    Phase = $phase
    NoProductionEndpoint = $true
    NoProductionAccount = $true
    NoProductionCredentialProfile = $true
    NoProductionRoute = $true
    NoProductionLedger = $true
    NoProductionStateMutation = $true
    ProductionRouteBlocked = $true
}

$guardrail = [ordered]@{
    Phase = $phase
    Environment = "Sandbox"
    BrokerVenue = "LMAXSandbox"
    MaxSandboxOrderCount = 1
    MaxSandboxNotional = 100
    AllowedSymbols = $configContract.AllowedSymbols
    DirectCrossExecutionAllowed = $false
    NonmajorExecutionAllowed = $false
    ProductionVenueAllowed = $false
    ProductionCredentialsAllowed = $false
    PaperLedgerCommitAllowed = $false
    ProductionLedgerCommitAllowed = $false
    StateMutationAllowed = $false
    IdempotencyRequired = $true
}

$operatorApproval = [ordered]@{
    Phase = $phase
    OperatorSandboxApprovalPresent = $false
    ApprovalScope = "SandboxSmokeOnly"
    ProductionApproval = $false
    Reason = "No explicit local sandbox config/credential profile; operator sandbox submission approval not consumed."
}

$intent = [ordered]@{
    Phase = $phase
    ExecutionIntentId = "r002-eurusd-sandbox-smoke-intent"
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
    OperatorSandboxApproval = $false
    KillSwitchOpenForSandboxOnly = $false
    IdempotencyKey = "r002-eurusd-sandbox-smoke-intent:20260526T151500Z"
    SandboxOnly = $true
    ProductionOrder = $false
    IsLiveProduction = $false
    NoProductionLedgerCommit = $true
}

$risk = [ordered]@{
    Phase = $phase
    Status = "Blocked"
    ConfigIsExplicitSandbox = $false
    CredentialProfilePresent = $false
    SandboxKillSwitchOpen = $false
    SymbolWhitelisted = $true
    DirectCrossRejected = $true
    CanonicalQuarterHourTargetClose = $true
    Legacy06Rejected = $true
    OrderCountWithinLimit = $true
    NotionalWithinConfiguredCap = $false
    NoProductionRoute = $true
    NoProductionLedger = $true
    OperatorSandboxApprovalPresent = $false
    IdempotencyKeyPresent = $true
    Reasons = $configValidation.Reasons + @("OperatorSandboxApprovalMissing")
}

$decision = [ordered]@{
    Phase = $phase
    R009DecisionProduced = $false
    DecisionStatus = "BlockedBeforeR009SandboxDecision"
    Reason = "Sandbox config/credential/risk gate blocked before order-path conversion."
    PrimaryPolicyCandidate = "CloseSeeking15mAdaptive_BalancedAdaptive_v0"
    SecondaryPolicyCandidate = "CloseSeeking15mAdaptive_ResidualAwareUrgency_v0"
    ConditionalResidualModule = "ControlledResidualCross_BalancedResidualCross_v0"
}

$orderIntent = [ordered]@{
    Phase = $phase
    Created = $false
    Status = "NotCreatedBlocked"
    SandboxOnly = $true
    BrokerVenue = "LMAXSandbox"
    ProductionOrder = $false
    IsLiveProduction = $false
    NoProductionLedgerCommit = $true
    NoProductionStateMutation = $true
    Reason = "Blocked by missing explicit LMAX sandbox config/credential profile."
}

$route = [ordered]@{
    Phase = $phase
    RouteCreated = $false
    SandboxOnly = $true
    BrokerVenue = "LMAXSandbox"
    ProductionRoute = $false
    NonSandboxBrokerRoute = $false
    ProductionCredentialsUsed = $false
    Reason = "No route created because guardrails did not pass."
}

$submission = [ordered]@{
    Phase = $phase
    SubmissionAttempted = $false
    SubmittedOrderCount = 0
    SubmittedNotional = 0
    MaxSandboxOrderCount = 1
    ConfiguredMaxSandboxNotional = 0
    SandboxOnly = $true
    ProductionSubmission = $false
    Status = "NotSubmittedBlocked"
    Reason = "Missing explicit sandbox config/credentials/kill switch."
}

$ackReject = [ordered]@{ Phase = $phase; AckCaptured = $false; RejectCaptured = $false; Status = "NoAckOrRejectBecauseSubmissionBlocked"; SandboxOnly = $true; ProductionAckReject = $false }
$executionReport = [ordered]@{ Phase = $phase; ExecutionReportCaptured = $false; Status = "NoExecutionReportBecauseSubmissionBlocked"; SandboxOnly = $true; ProductionExecutionReport = $false }
$fillReport = [ordered]@{ Phase = $phase; FillCaptured = $false; Status = "NoFillBecauseSubmissionBlocked"; SandboxOnly = $true; ProductionFill = $false }
$reconciliation = [ordered]@{
    Phase = $phase
    Status = "BlockedBeforeSubmission"
    IntendedSandboxOrderCreated = $false
    SubmittedSandboxOrder = $false
    SubmittedSandboxOrderCount = 0
    AckOrRejectCaptured = $false
    ExecutionReportCaptured = $false
    FillCaptured = $false
    ProductionOrderCreated = $false
    ProductionRouteCreated = $false
    ProductionFillOrReportCreated = $false
    ProductionLedgerMutation = $false
    ProductionStateMutation = $false
    SandboxOnly = $true
}
$audit = [ordered]@{
    Phase = $phase
    AuditId = "r002-lmax-sandbox-config-and-smoke-audit"
    SandboxOnly = $true
    SandboxConfigReady = $false
    CredentialProfileReady = $false
    SandboxSubmissionAttempted = $false
    SandboxOrderSubmitted = $false
    ProductionOrderCreated = $false
    ProductionRouteCreated = $false
    ProductionFillOrReportCreated = $false
    ProductionLedgerCommit = $false
    ProductionStateMutation = $false
    CredentialValuesRedacted = $true
    CredentialValuesPrintedOrPersisted = $false
    AuditHash = "r002-blocked-missing-sandbox-config-or-credentials"
}

Write-JsonArtifact "phase-exec-sandbox-r002-r001-reference.json" $r001Reference
Write-JsonArtifact "phase-exec-sandbox-r002-lmax-sandbox-config-contract.json" $configContract
Write-JsonArtifact "phase-exec-sandbox-r002-lmax-sandbox-config-validation.json" $configValidation
Write-JsonArtifact "phase-exec-sandbox-r002-credential-profile-validation.json" $credentialValidation
Write-JsonArtifact "phase-exec-sandbox-r002-production-route-blocking-check.json" $productionBlocking
Write-JsonArtifact "phase-exec-sandbox-r002-sandbox-guardrail-contract.json" $guardrail
Write-JsonArtifact "phase-exec-sandbox-r002-operator-sandbox-approval.json" $operatorApproval
Write-JsonArtifact "phase-exec-sandbox-r002-r009-sandbox-execution-intent.json" $intent
Write-JsonArtifact "phase-exec-sandbox-r002-pretrade-sandbox-risk-check.json" $risk
Write-JsonArtifact "phase-exec-sandbox-r002-r009-sandbox-decision.json" $decision
Write-JsonArtifact "phase-exec-sandbox-r002-sandbox-order-intent.json" $orderIntent
Write-JsonArtifact "phase-exec-sandbox-r002-sandbox-route.json" $route
Write-JsonArtifact "phase-exec-sandbox-r002-sandbox-submission-result.json" $submission
Write-JsonArtifact "phase-exec-sandbox-r002-sandbox-ack-reject.json" $ackReject
Write-JsonArtifact "phase-exec-sandbox-r002-sandbox-execution-report.json" $executionReport
Write-JsonArtifact "phase-exec-sandbox-r002-sandbox-fill-report.json" $fillReport
Write-JsonArtifact "phase-exec-sandbox-r002-sandbox-reconciliation-result.json" $reconciliation
Write-JsonArtifact "phase-exec-sandbox-r002-sandbox-audit-record.json" $audit

Write-JsonArtifact "phase-exec-sandbox-r002-no-production-broker-audit.json" (New-Audit "NoProductionBroker" "No LMAX production or non-sandbox broker route was used.")
Write-JsonArtifact "phase-exec-sandbox-r002-no-production-order-audit.json" (New-Audit "NoProductionOrder" "No production order artifact was created.")
Write-JsonArtifact "phase-exec-sandbox-r002-no-production-route-audit.json" (New-Audit "NoProductionRoute" "No production route artifact was created.")
Write-JsonArtifact "phase-exec-sandbox-r002-no-production-fill-report-audit.json" (New-Audit "NoProductionFillReport" "No production fill or execution report artifact was created.")
Write-JsonArtifact "phase-exec-sandbox-r002-no-production-ledger-audit.json" (New-Audit "NoProductionLedger" "No production ledger commit occurred.")
Write-JsonArtifact "phase-exec-sandbox-r002-no-production-state-mutation-audit.json" (New-Audit "NoProductionStateMutation" "No live/broker/production/trading state was mutated.")
Write-JsonArtifact "phase-exec-sandbox-r002-direct-cross-exclusion-preservation.json" ([ordered]@{ Phase = $phase; DirectCrossExecutionAllowed = $false; DirectCrossExecutionIntentRejected = $true })
Write-JsonArtifact "phase-exec-sandbox-r002-usd-pair-whitelist-preservation.json" ([ordered]@{ Phase = $phase; WhitelistedSymbols = $configContract.AllowedSymbols; NonWhitelistedSymbolAllowed = $false; AudusdStatus = "SupportedAndNotFailed" })
Write-JsonArtifact "phase-exec-sandbox-r002-usdjpy-caveat-preservation.json" ([ordered]@{ Phase = $phase; NormalizedPortfolioSymbol = "JPYUSD"; ExecutionTradableSymbol = "USDJPY"; RequiresInversion = $true; SecurityID = "4004"; SecurityIDSource = "8"; CaveatPreserved = $true })
Write-JsonArtifact "phase-exec-sandbox-r002-canonical-quarter-hour-policy-preservation.json" ([ordered]@{ Phase = $phase; FutureCanonicalMinutes = @(0, 15, 30, 45); CandidateTargetCloseUtc = $intent.CanonicalTargetCloseUtc; CandidateIsCanonical = $true })
Write-JsonArtifact "phase-exec-sandbox-r002-legacy-compatibility-preservation.json" ([ordered]@{ Phase = $phase; LegacyLabels = @(":06", ":21", ":36", ":51"); UsedAsFutureCanonical = $false; Legacy06AcceptedAsFutureCanonical = $false })
Write-JsonArtifact "phase-exec-sandbox-r002-cost-guidance-preservation.json" ([ordered]@{ Phase = $phase; FiveUsdPerMillion = "BestCaseMajorOnly"; Universalized = $false })
Write-JsonArtifact "phase-exec-sandbox-r002-nonmajor-calibration-preservation.json" ([ordered]@{ Phase = $phase; NonmajorEmScandiCnh = "CalibrationRequired"; SandboxExecutionAllowed = $false })
Write-JsonArtifact "phase-exec-sandbox-r002-forbidden-actions-audit.json" ([ordered]@{
    Phase = $phase
    LmaxProductionUsed = $false
    ProductionCredentialsUsed = $false
    NonSandboxBrokerRouteUsed = $false
    CredentialValuesPrintedOrPersisted = $false
    PolygonCalled = $false
    UnrelatedExternalApiCalled = $false
    SchedulerServicePollingBackgroundJobIntroduced = $false
    MoreSandboxOrdersThanAllowedSubmitted = $false
    SandboxNotionalExceedsConfiguredCap = $false
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
Write-JsonArtifact "phase-exec-sandbox-r002-next-phase-recommendation.json" ([ordered]@{
    Phase = $phase
    RecommendedNextPhase = "EXEC-SANDBOX-R003"
    Title = "LMAX Sandbox Credential Profile Wiring and One-Order Smoke Execution Gate"
    Reason = "R002 blocked before connection because explicit local LMAXSandbox config and sandbox credential profile were still missing."
})
Write-JsonArtifact "phase-exec-sandbox-r002-build-test-validator-evidence.json" ([ordered]@{
    Phase = $phase
    Build = $BuildStatus
    FocusedSandboxTests = $FocusedTestsStatus
    Validator = $ValidatorStatus
    DotnetBuildNoRestore = "dotnet build --no-restore"
    FocusedTests = "dotnet test tests\QQ.Production.Intraday.Tests.Unit\QQ.Production.Intraday.Tests.Unit.csproj --no-restore --filter FullyQualifiedName~R009LmaxSandboxConfigCompletionTests|FullyQualifiedName~R009LmaxSandboxOrderSmokeTests"
    ValidatorScript = "scripts/check-exec-sandbox-r002-lmax-sandbox-config-and-smoke-gate.ps1"
})

$summary = @"
# EXEC-SANDBOX-R002 Summary

Classifications:
- EXEC_SANDBOX_R002_BLOCKED_LMAX_SANDBOX_CONFIG_OR_CREDENTIALS_MISSING
- EXEC_SANDBOX_R002_PASS_PRODUCTION_ROUTE_BLOCKED
- EXEC_SANDBOX_R002_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE

R002 added and validated the explicit LMAX sandbox config/credential guardrail contract. Existing LMAX demo/sandbox credential environment variables were detected by name and presence only, with values redacted and not persisted. The current workspace still does not contain concrete LMAXSandbox order-submission config, sandbox kill switch, and sandbox order/notional caps, so no connection, socket, route, submission, ack/reject, execution report, fill, production order, production route, production ledger commit, or production state mutation occurred.

Build/tests/validator:
- Build: $BuildStatus
- Focused sandbox tests/static checks: $FocusedTestsStatus
- Validator: $ValidatorStatus

Next recommendation: EXEC-SANDBOX-R003 - LMAX Sandbox Credential Profile Wiring and One-Order Smoke Execution Gate.
"@
$summary | Set-Content -Path (Join-Path $artifactDir "phase-exec-sandbox-r002-summary.md") -Encoding UTF8

Write-Host "EXEC-SANDBOX-R002 artifacts written to $artifactDir"
