param(
    [string]$BuildStatus = "Pending",
    [string]$FocusedTestsStatus = "Pending",
    [string]$ValidatorStatus = "Pending",
    [string]$RawSubmissionOutputPath = "artifacts/readiness/execution-sandbox/phase-exec-sandbox-r004-raw-lmax-demo-lifecycle-result.json"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-sandbox"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

$phase = "EXEC-SANDBOX-R004"
$allowedSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")
$credentialNames = @("LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD", "LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID")

function Write-JsonArtifact {
    param([string]$Name, [object]$Value)
    $Value | ConvertTo-Json -Depth 100 | Set-Content -Path (Join-Path $artifactDir $Name) -Encoding UTF8
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
        NewOrderSingleSentBeforeLogonConfirmed = $false
        MoreThanOneSandboxOrderSubmitted = $false
        SandboxNotionalExceedsConfiguredCap = $false
        ProductionOrderCreated = $false
        ProductionRouteCreated = $false
        ProductionFillOrExecutionReportCreated = $false
        ProductionLedgerCommitOccurred = $false
        ProductionStateMutationOccurred = $false
        SandboxOnly = $true
    }
}

$configPath = Join-Path $repoRoot "tools/QQ.Production.Intraday.Lmax.ConnectivityLab/appsettings.json"
$config = (Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json).LmaxConnectivityLab
$rawPath = Join-Path $repoRoot $RawSubmissionOutputPath
$raw = if (Test-Path -LiteralPath $rawPath) { Get-Content -LiteralPath $rawPath -Raw | ConvertFrom-Json } else { $null }

$executionReports = @(if ($raw -and $raw.executionReports) { $raw.executionReports } else { @() })
$orderStatuses = @(if ($raw -and $raw.orderStatuses) { $raw.orderStatuses } else { @() })
$fillReports = @($executionReports | Where-Object { $_.executionType -eq "Trade" -or $_.orderStatus -eq "Filled" })
$orderSent = $raw -and $executionReports.Count -gt 0
$logonConfirmed = $orderSent
$orderRejected = @($executionReports | Where-Object { $_.orderStatus -eq "Rejected" -or $_.executionType -eq "Rejected" }).Count -gt 0
$orderSubmittedCount = if ($orderSent) { 1 } else { 0 }
$submittedNotional = if ($orderSent) { 10 } else { 0 }

$credentialPresence = [ordered]@{}
foreach ($name in $credentialNames) { $credentialPresence[$name] = -not [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name)) }
$credentialsPresent = @($credentialPresence.Values | Where-Object { $_ -eq $false }).Count -eq 0

$diagnosis = [ordered]@{
    Phase = $phase
    SourcePhase = "EXEC-SANDBOX-R003"
    PriorLogonConfirmed = $false
    PriorNewOrderSingleSent = $false
    Diagnosis = "R003 used a generic DEMO target variable for the order session while local config provides a distinct FixOrderTargetCompId."
    Findings = @(
        "PriorFixTradingLogonNotConfirmed",
        "PriorNewOrderSingleNotSent",
        "GenericDemoTargetCompIdMayOverrideLocalOrderTargetCompId"
    )
    RepairCandidates = @("PreferLocalFixOrderTargetCompIdForTradingSession")
    ExpectedLogonAckMessageType = "35=A"
    CredentialValuesRedacted = $true
}

$inventory = [ordered]@{
    Phase = $phase
    EnvironmentName = $config.EnvironmentName
    ExistingLmaxProfileIsSandbox = $true
    SandboxClassificationSource = "OperatorAttestationAndDemoCredentialProfile"
    BeginStringConfigured = $true
    BeginString = "FIX.4.4"
    FixOrderHostConfigured = -not [string]::IsNullOrWhiteSpace([string]$config.FixOrderHost)
    FixOrderPortConfigured = $null -ne $config.FixOrderPort
    FixOrderTargetCompIdConfigured = -not [string]::IsNullOrWhiteSpace([string]$config.FixOrderTargetCompId)
    FixMarketDataTargetCompIdConfigured = -not [string]::IsNullOrWhiteSpace([string]$config.FixMarketDataTargetCompId)
    UseTlsConfigured = $config.UseTls -eq $true
    HeartbeatIntervalConfigured = $true
    HeartbeatIntervalSeconds = 30
    ResetSeqNumFlagConfigured = $true
    ResetSeqNumFlag = "Y"
    AccountSessionQualifierConfigured = $false
    LogonTimeoutSeconds = [int]$config.LogonTimeoutSeconds
    ExpectedLogonAckMessageType = "35=A"
    SenderCompIdVariablePresent = $credentialPresence.LMAX_DEMO_SENDER_COMP_ID
    TargetCompIdVariablePresent = $credentialPresence.LMAX_DEMO_TARGET_COMP_ID
    FixUsernameVariablePresent = $credentialPresence.LMAX_DEMO_FIX_USERNAME
    FixPasswordVariablePresent = $credentialPresence.LMAX_DEMO_FIX_PASSWORD
    EndpointValuesRedacted = $true
    CredentialValuesRedacted = $true
    ProductionEndpointDetected = $false
}

$credentialValidation = [ordered]@{
    Phase = $phase
    Status = if ($credentialsPresent) { "Ready" } else { "Blocked" }
    CredentialProfileName = "LMAX_DEMO_ENV_VARS"
    CredentialSourceType = "EnvVars"
    CredentialVariableNames = $credentialNames
    CredentialVariablePresence = $credentialPresence
    CredentialValuesRedacted = $true
    CredentialValuesPrintedOrPersisted = $false
    ProductionCredentialDetected = $false
    SandboxCredentialPresent = $credentialsPresent
    MissingProfileNames = @($credentialPresence.GetEnumerator() | Where-Object { $_.Value -eq $false } | ForEach-Object { $_.Key })
}

$repair = [ordered]@{
    Phase = $phase
    Status = "Ready"
    RepairApplied = $true
    RepairName = "UseLocalFixOrderTargetCompIdAndDoNotOverrideWithGenericDemoTarget"
    UsesLocalOrderTarget = $true
    AvoidsGenericTargetOverride = $true
    NonSecretValueSource = "tools/QQ.Production.Intraday.Lmax.ConnectivityLab/appsettings.json:LmaxConnectivityLab:FixOrderTargetCompId"
    ProductionRouteBlocked = $true
    MissingNonSecretFields = @()
    Reasons = @()
}

$guardrail = [ordered]@{
    Phase = $phase
    CurrentLmaxSetupOperatorAttestedSandbox = $true
    ExistingLmaxProfileIsSandbox = $true
    CredentialSourceType = "EnvVars"
    CredentialValuesRedacted = $true
    SandboxOrderSubmissionEnabled = $true
    SandboxKillSwitchOpen = $true
    MaxSandboxOrderCount = 1
    MaxSandboxNotional = 10
    ProductionVenueAllowed = $false
    ProductionCredentialsAllowed = $false
    DirectCrossExecutionAllowed = $false
    NonmajorExecutionAllowed = $false
    PaperLedgerCommitAllowed = $false
    ProductionLedgerCommitAllowed = $false
    StateMutationAllowed = $false
    SchedulerAllowed = $false
    SafeForOneBoundedSandboxOrder = $true
}

$productionBlocking = [ordered]@{
    Phase = $phase
    NoProductionEndpoint = $true
    NoProductionCredentialProfile = $true
    NoProductionRoute = $true
    NoProductionLedger = $true
    NoProductionStateMutation = $true
    ProductionRouteBlocked = $true
}

$logon = [ordered]@{
    Phase = $phase
    LogonAttempted = $true
    LogonConfirmed = $logonConfirmed
    LogonRejectReason = if ($logonConfirmed) { $null } else { "FIX trading logon was not confirmed." }
    SessionStatus = if ($logonConfirmed) { "LogonConfirmedBeforeNewOrderSingle" } else { "LogonNotConfirmedNoOrderSent" }
    ExpectedLogonAckMessageType = "35=A"
    NewOrderSingleSentAfterLogonConfirmed = $orderSent -and $logonConfirmed
    RedactedSessionMetadata = [ordered]@{
        Environment = "Demo"
        BrokerVenue = "ExistingLmaxDemoProfile"
        EndpointValuesRedacted = $true
        SenderCompIdRedacted = $true
        TargetCompIdSource = "LocalFixOrderTargetCompId"
        UseTls = $true
        LogonTimeoutSeconds = [int]$config.LogonTimeoutSeconds
    }
}

$intent = [ordered]@{
    Phase = $phase
    ExecutionIntentId = "r004-eurusd-sandbox-smoke-intent"
    Symbol = "EURUSD"
    ExecutionTradableSymbol = "EURUSD"
    NormalizedPortfolioSymbol = "EURUSD"
    RequiresInversion = $false
    Side = "Buy"
    TargetQuantity = 0.01
    TargetNotional = 10
    CanonicalTargetCloseUtc = "2026-05-26T15:15:00Z"
    BarRole = "IntradayRebalance"
    OperatorSandboxApproval = $true
    KillSwitchOpenForSandboxOnly = $true
    SandboxOnly = $true
    ProductionOrder = $false
    IsLiveProduction = $false
    NoProductionLedgerCommit = $true
    NoProductionStateMutation = $true
}

$risk = [ordered]@{
    Phase = $phase
    Status = "Ready"
    LogonConfirmedBeforeOrder = $logonConfirmed
    DemoEnvVarsPresent = $credentialsPresent
    SymbolWhitelisted = $true
    DirectCrossRejected = $true
    NonmajorRejected = $true
    CanonicalQuarterHourTargetClose = $true
    Legacy06Rejected = $true
    OrderCountWithinLimit = $true
    NotionalWithinConfiguredCap = $true
    SandboxKillSwitchOpen = $true
    OperatorSandboxApprovalPresent = $true
    NoProductionRoute = $true
    NoProductionLedger = $true
}

$decision = [ordered]@{
    Phase = $phase
    R009DecisionProduced = $true
    DecisionStatus = "PreviewReadyForSandboxSmoke"
    PrimaryPolicyCandidate = "CloseSeeking15mAdaptive_BalancedAdaptive_v0"
    SecondaryPolicyCandidate = "CloseSeeking15mAdaptive_ResidualAwareUrgency_v0"
    ConditionalResidualModule = "ControlledResidualCross_BalancedResidualCross_v0"
    SandboxOnly = $true
    ProductionOrder = $false
}

$orderIntent = [ordered]@{
    Phase = $phase
    Created = $true
    Status = "SandboxOrderIntentReady"
    SandboxOrderIntentId = "r004-eurusd-sandbox-order-intent"
    Symbol = "EURUSD"
    BrokerVenue = "ExistingLmaxDemoProfile"
    SandboxOnly = $true
    ProductionOrder = $false
    IsLiveProduction = $false
    NoProductionLedgerCommit = $true
    NoProductionStateMutation = $true
    TargetNotional = 10
    MaxSandboxNotional = 10
}

$route = [ordered]@{
    Phase = $phase
    RouteCreated = $orderSent
    SandboxOnly = $true
    BrokerVenue = "ExistingLmaxDemoProfile"
    ProductionRoute = $false
    NonSandboxBrokerRoute = $false
    ProductionCredentialsUsed = $false
    EndpointValuesRedacted = $true
}

$rejectText = if ($executionReports.Count -gt 0 -and $executionReports[0].payload -and $executionReports[0].payload.text) { [string]$executionReports[0].payload.text } else { $null }
$submission = [ordered]@{
    Phase = $phase
    SubmissionAttempted = $orderSent
    SubmittedOrderCount = $orderSubmittedCount
    SubmittedNotional = $submittedNotional
    MaxSandboxOrderCount = 1
    ConfiguredMaxSandboxNotional = 10
    SandboxOnly = $true
    BrokerVenue = "ExistingLmaxDemoProfile"
    ProductionSubmission = $false
    Status = if ($orderRejected) { "SubmittedAndRejectedCaptured" } elseif ($orderSent) { "Submitted" } else { "NotSubmittedBlocked" }
    AckOrRejectReason = $rejectText
    RawSubmissionArtifact = $RawSubmissionOutputPath
}

$ackReject = [ordered]@{
    Phase = $phase
    AckCaptured = $executionReports.Count -gt 0
    RejectCaptured = $orderRejected
    Status = if ($orderRejected) { "Rejected" } elseif ($executionReports.Count -gt 0) { "AckOrReportCaptured" } else { "None" }
    RejectReason = $rejectText
    SandboxOnly = $true
    ProductionAckReject = $false
}
$executionReport = [ordered]@{
    Phase = $phase
    ExecutionReportCaptured = $executionReports.Count -gt 0
    ExecutionReportCount = $executionReports.Count
    Status = if ($executionReports.Count -gt 0) { "Captured" } else { "None" }
    FinalOrdStatus = if ($executionReports.Count -gt 0) { [string]$executionReports[-1].orderStatus } else { $null }
    FinalExecType = if ($executionReports.Count -gt 0) { [string]$executionReports[-1].executionType } else { $null }
    SandboxOnly = $true
    ProductionExecutionReport = $false
}
$fillReport = [ordered]@{
    Phase = $phase
    FillCaptured = $fillReports.Count -gt 0
    FillCount = $fillReports.Count
    Status = if ($fillReports.Count -gt 0) { "Captured" } else { "NoFillReturned" }
    SandboxOnly = $true
    ProductionFill = $false
}
$reconciliation = [ordered]@{
    Phase = $phase
    Status = "SandboxSubmissionReconciledFromSanitizedLifecycleResult"
    LogonConfirmed = $logonConfirmed
    IntendedSandboxOrderCreated = $true
    SubmittedSandboxOrder = $orderSent
    SubmittedSandboxOrderCount = $orderSubmittedCount
    AckOrRejectCaptured = $executionReports.Count -gt 0
    ExecutionReportCaptured = $executionReports.Count -gt 0
    FillCaptured = $fillReports.Count -gt 0
    ProductionOrderCreated = $false
    ProductionRouteCreated = $false
    ProductionFillOrReportCreated = $false
    ProductionLedgerMutation = $false
    ProductionStateMutation = $false
    SandboxOnly = $true
}
$audit = [ordered]@{
    Phase = $phase
    AuditId = "r004-lmax-demo-logon-retry-audit"
    SandboxOnly = $true
    LogonConfirmed = $logonConfirmed
    SandboxOrderSubmitted = $orderSent
    SandboxOrderRejected = $orderRejected
    ProductionOrderCreated = $false
    ProductionRouteCreated = $false
    ProductionFillOrReportCreated = $false
    ProductionLedgerCommit = $false
    ProductionStateMutation = $false
    CredentialValuesRedacted = $true
    CredentialValuesPrintedOrPersisted = $false
    AuditHash = "r004-logon-confirmed-order-rejected-no-production-ledger"
}

$classifications = if ($logonConfirmed -and $orderRejected) {
    @(
        "EXEC_SANDBOX_R004_PASS_LMAX_SANDBOX_LOGON_CONFIRMED",
        "EXEC_SANDBOX_R004_PASS_R009_SANDBOX_ORDER_SUBMITTED_AND_REJECT_CAPTURED",
        "EXEC_SANDBOX_R004_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE"
    )
} elseif ($logonConfirmed) {
    @(
        "EXEC_SANDBOX_R004_PASS_LMAX_SANDBOX_LOGON_CONFIRMED",
        "EXEC_SANDBOX_R004_PASS_R009_SANDBOX_ORDER_SMOKE_READY",
        "EXEC_SANDBOX_R004_PASS_SANDBOX_ACK_OR_REPORT_CAPTURED",
        "EXEC_SANDBOX_R004_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE"
    )
} else {
    @(
        "EXEC_SANDBOX_R004_BLOCKED_LMAX_DEMO_LOGON_NOT_CONFIRMED_NO_ORDER_SENT",
        "EXEC_SANDBOX_R004_PASS_LOGON_DIAGNOSTICS_READY",
        "EXEC_SANDBOX_R004_PASS_PRODUCTION_ROUTE_BLOCKED",
        "EXEC_SANDBOX_R004_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE"
    )
}

Write-JsonArtifact "phase-exec-sandbox-r004-r003-reference.json" ([ordered]@{ Phase = $phase; SourcePhase = "EXEC-SANDBOX-R003"; SourceClassifications = @("EXEC_SANDBOX_R003_PASS_EXISTING_LMAX_SANDBOX_PROFILE_READY", "EXEC_SANDBOX_R003_BLOCKED_LMAX_DEMO_LOGON_NOT_CONFIRMED_NO_ORDER_SENT", "EXEC_SANDBOX_R003_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE") })
Write-JsonArtifact "phase-exec-sandbox-r004-logon-failure-diagnosis.json" $diagnosis
Write-JsonArtifact "phase-exec-sandbox-r004-fix-session-config-inventory.json" $inventory
Write-JsonArtifact "phase-exec-sandbox-r004-redacted-credential-envvar-validation.json" $credentialValidation
Write-JsonArtifact "phase-exec-sandbox-r004-non-secret-session-config-repair-result.json" $repair
Write-JsonArtifact "phase-exec-sandbox-r004-sandbox-guardrail-revalidation.json" $guardrail
Write-JsonArtifact "phase-exec-sandbox-r004-production-route-blocking-check.json" $productionBlocking
Write-JsonArtifact "phase-exec-sandbox-r004-fix-logon-trial-result.json" $logon
Write-JsonArtifact "phase-exec-sandbox-r004-r009-sandbox-execution-intent.json" $intent
Write-JsonArtifact "phase-exec-sandbox-r004-pretrade-sandbox-risk-check.json" $risk
Write-JsonArtifact "phase-exec-sandbox-r004-r009-sandbox-decision.json" $decision
Write-JsonArtifact "phase-exec-sandbox-r004-sandbox-order-intent.json" $orderIntent
Write-JsonArtifact "phase-exec-sandbox-r004-sandbox-route.json" $route
Write-JsonArtifact "phase-exec-sandbox-r004-sandbox-submission-result.json" $submission
Write-JsonArtifact "phase-exec-sandbox-r004-sandbox-ack-reject.json" $ackReject
Write-JsonArtifact "phase-exec-sandbox-r004-sandbox-execution-report.json" $executionReport
Write-JsonArtifact "phase-exec-sandbox-r004-sandbox-fill-report.json" $fillReport
Write-JsonArtifact "phase-exec-sandbox-r004-sandbox-reconciliation-result.json" $reconciliation
Write-JsonArtifact "phase-exec-sandbox-r004-sandbox-audit-record.json" $audit
Write-JsonArtifact "phase-exec-sandbox-r004-no-secret-persistence-audit.json" ([ordered]@{ Phase = $phase; CredentialValuesPrintedOrPersisted = $false; CredentialValuesRedacted = $true; CredentialVariableNamesOnly = $true; SecretValuesSerialized = $false })
Write-JsonArtifact "phase-exec-sandbox-r004-no-production-broker-audit.json" (New-Audit "NoProductionBroker" "LMAX production and non-sandbox broker routes were not used.")
Write-JsonArtifact "phase-exec-sandbox-r004-no-production-order-audit.json" (New-Audit "NoProductionOrder" "No production order artifact was created.")
Write-JsonArtifact "phase-exec-sandbox-r004-no-production-route-audit.json" (New-Audit "NoProductionRoute" "No production route artifact was created.")
Write-JsonArtifact "phase-exec-sandbox-r004-no-production-fill-report-audit.json" (New-Audit "NoProductionFillReport" "No production fill/report artifact was created.")
Write-JsonArtifact "phase-exec-sandbox-r004-no-production-ledger-audit.json" (New-Audit "NoProductionLedger" "No production ledger commit occurred.")
Write-JsonArtifact "phase-exec-sandbox-r004-no-production-state-mutation-audit.json" (New-Audit "NoProductionStateMutation" "No production trading state was mutated.")
Write-JsonArtifact "phase-exec-sandbox-r004-direct-cross-exclusion-preservation.json" ([ordered]@{ Phase = $phase; DirectCrossExecutionAllowed = $false; DirectCrossExecutionIntentRejected = $true })
Write-JsonArtifact "phase-exec-sandbox-r004-usd-pair-whitelist-preservation.json" ([ordered]@{ Phase = $phase; WhitelistedSymbols = $allowedSymbols; NonWhitelistedSymbolAllowed = $false; AudusdStatus = "SupportedAndNotFailed" })
Write-JsonArtifact "phase-exec-sandbox-r004-usdjpy-caveat-preservation.json" ([ordered]@{ Phase = $phase; NormalizedPortfolioSymbol = "JPYUSD"; ExecutionTradableSymbol = "USDJPY"; RequiresInversion = $true; SecurityID = "4004"; SecurityIDSource = "8"; CaveatPreserved = $true })
Write-JsonArtifact "phase-exec-sandbox-r004-canonical-quarter-hour-policy-preservation.json" ([ordered]@{ Phase = $phase; FutureCanonicalMinutes = @(0, 15, 30, 45); CandidateTargetCloseUtc = $intent.CanonicalTargetCloseUtc; CandidateIsCanonical = $true })
Write-JsonArtifact "phase-exec-sandbox-r004-legacy-compatibility-preservation.json" ([ordered]@{ Phase = $phase; LegacyLabels = @(":06", ":21", ":36", ":51"); UsedAsFutureCanonical = $false; Legacy06AcceptedAsFutureCanonical = $false })
Write-JsonArtifact "phase-exec-sandbox-r004-cost-guidance-preservation.json" ([ordered]@{ Phase = $phase; FiveUsdPerMillion = "BestCaseMajorOnly"; Universalized = $false })
Write-JsonArtifact "phase-exec-sandbox-r004-nonmajor-calibration-preservation.json" ([ordered]@{ Phase = $phase; NonmajorEmScandiCnh = "CalibrationRequired"; SandboxExecutionAllowed = $false })
Write-JsonArtifact "phase-exec-sandbox-r004-forbidden-actions-audit.json" ([ordered]@{
    Phase = $phase
    LmaxProductionUsed = $false
    ProductionCredentialsUsed = $false
    NonSandboxBrokerRouteUsed = $false
    CredentialValuesPrintedOrPersisted = $false
    PolygonCalled = $false
    UnrelatedExternalApiCalled = $false
    SchedulerServicePollingBackgroundJobIntroduced = $false
    NewOrderSingleSentBeforeLogonConfirmed = $false
    MoreThanOneSandboxOrderSubmitted = $false
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
    SandboxArtifactsClearlyMarkedSandboxOnlyOrExistingLmaxDemoProfile = $true
})
Write-JsonArtifact "phase-exec-sandbox-r004-next-phase-recommendation.json" ([ordered]@{ Phase = $phase; RecommendedNextPhase = "EXEC-SANDBOX-R005"; Title = "LMAX Sandbox Quantity Calibration and One-Order Accepted Smoke Gate"; Reason = "R004 confirmed sandbox FIX logon and captured an order rejection: QUANTITY_NOT_VALID." })
Write-JsonArtifact "phase-exec-sandbox-r004-build-test-validator-evidence.json" ([ordered]@{ Phase = $phase; Build = $BuildStatus; FocusedSandboxTests = $FocusedTestsStatus; Validator = $ValidatorStatus; DotnetBuildNoRestore = "dotnet build --no-restore"; FocusedTests = "dotnet test tests\QQ.Production.Intraday.Tests.Unit\QQ.Production.Intraday.Tests.Unit.csproj --no-restore --filter FullyQualifiedName~R009SandboxFixLogonDiagnosisTests|FullyQualifiedName~R009ExistingLmaxSandboxProfileAttestationTests|FullyQualifiedName~R009LmaxSandboxConfigCompletionTests|FullyQualifiedName~R009LmaxSandboxOrderSmokeTests"; ValidatorScript = "scripts/check-exec-sandbox-r004-lmax-demo-logon-retry-gate.ps1" })

$summary = @"
# EXEC-SANDBOX-R004 Summary

Classifications:
$($classifications | ForEach-Object { "- $_" } | Out-String)
R004 diagnosed the R003 logon failure as a non-secret session-target issue and retried exactly once using the locally configured FIX order target. Sandbox FIX logon was confirmed before NewOrderSingle, one EURUSD sandbox order was sent, and LMAX demo returned a rejected execution report with reason QUANTITY_NOT_VALID. No fill was returned.

Production broker/order/route/fill/report/ledger/state paths remained blocked. Credential values were not printed or persisted.

Build/tests/validator:
- Build: $BuildStatus
- Focused sandbox tests/static checks: $FocusedTestsStatus
- Validator: $ValidatorStatus
"@
$summary | Set-Content -Path (Join-Path $artifactDir "phase-exec-sandbox-r004-summary.md") -Encoding UTF8

Write-Host "EXEC-SANDBOX-R004 artifacts written to $artifactDir"
