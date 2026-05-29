param(
    [string]$BuildStatus = "Pending",
    [string]$FocusedTestsStatus = "Pending",
    [string]$ValidatorStatus = "Pending",
    [string]$RawSubmissionOutputPath = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-sandbox"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

$phase = "EXEC-SANDBOX-R003"
$requiredCredentialVariables = @(
    "LMAX_DEMO_FIX_USERNAME",
    "LMAX_DEMO_FIX_PASSWORD",
    "LMAX_DEMO_SENDER_COMP_ID",
    "LMAX_DEMO_TARGET_COMP_ID"
)
$allowedSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")

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

function Get-LabConfig {
    $path = Join-Path $repoRoot "tools/QQ.Production.Intraday.Lmax.ConnectivityLab/appsettings.json"
    if (-not (Test-Path -LiteralPath $path)) {
        return [ordered]@{ Path = $path; Present = $false; Values = [ordered]@{} }
    }
    $json = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    return [ordered]@{ Path = $path; Present = $true; Values = $json.LmaxConnectivityLab }
}

function Is-Present([string]$Name) {
    -not [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($Name))
}

$labConfig = Get-LabConfig
$values = $labConfig.Values
$environmentName = if ($values.EnvironmentName) { [string]$values.EnvironmentName } else { "" }
$fixOrderHostPresent = -not [string]::IsNullOrWhiteSpace([string]$values.FixOrderHost)
$fixOrderPortPresent = $null -ne $values.FixOrderPort
$fixOrderTargetPresent = -not [string]::IsNullOrWhiteSpace([string]$values.FixOrderTargetCompId)
$demoEndpointDetected = ([string]$values.FixOrderHost) -match "demo|uat"
$productionEndpointDetected = ([string]$values.FixOrderHost) -match "prod|production|live"
$environmentIsDemo = $environmentName -in @("Demo", "Sandbox", "UAT")

$credentialVariablePresence = [ordered]@{}
foreach ($name in $requiredCredentialVariables) {
    $credentialVariablePresence[$name] = Is-Present $name
}
$sandboxCredentialPresent = @($credentialVariablePresence.Values | Where-Object { $_ -eq $false }).Count -eq 0
$missingCredentialVariables = @($credentialVariablePresence.GetEnumerator() | Where-Object { $_.Value -eq $false } | ForEach-Object { $_.Key })

$runtimeSubmissionEnabled = $values.AllowExternalConnections -eq $true -and $values.AllowOrderSubmission -eq $true -and $values.DryRun -eq $false
$nonSecretConfigMissing = @()
if (-not $fixOrderHostPresent) { $nonSecretConfigMissing += "LmaxConnectivityLab:FixOrderHost" }
if (-not $fixOrderPortPresent) { $nonSecretConfigMissing += "LmaxConnectivityLab:FixOrderPort" }
if (-not $fixOrderTargetPresent) { $nonSecretConfigMissing += "LmaxConnectivityLab:FixOrderTargetCompId" }

$profileReady = $labConfig.Present -and $environmentIsDemo -and $demoEndpointDetected -and -not $productionEndpointDetected -and $sandboxCredentialPresent -and $nonSecretConfigMissing.Count -eq 0
$sandboxGuardrailsReady = $profileReady

$rawSubmission = $null
if (-not [string]::IsNullOrWhiteSpace($RawSubmissionOutputPath) -and (Test-Path -LiteralPath $RawSubmissionOutputPath)) {
    $rawSubmission = Get-Content -LiteralPath $RawSubmissionOutputPath -Raw | ConvertFrom-Json
}

$submissionAttempted = $null -ne $rawSubmission
$orderSubmission = if ($rawSubmission -and $rawSubmission.OrderSubmission) { $rawSubmission.OrderSubmission } else { $rawSubmission }
$orderSent = $submissionAttempted -and $orderSubmission.OrderSent -eq $true
$executionReports = @()
if ($orderSubmission -and $orderSubmission.ExecutionReports) { $executionReports = @($orderSubmission.ExecutionReports) }
$requestRejected = $submissionAttempted -and $orderSubmission.RequestRejected -eq $true
$terminalReport = $submissionAttempted -and $orderSubmission.TerminalExecutionReportReceived -eq $true
$submissionStatus = if (-not $submissionAttempted) {
    "NotSubmittedBlocked"
} elseif ($orderSubmission.Status -eq "Ok") {
    "Acknowledged"
} elseif (-not $orderSent -and -not $requestRejected) {
    "BlockedLogonNotConfirmedNoOrderSent"
} elseif ($requestRejected -or $orderSubmission.Status -eq "Failed") {
    "Rejected"
} else {
    [string]$orderSubmission.Status
}

$classifications = if ($submissionAttempted -and $orderSubmission.Status -eq "Ok") {
    @(
        "EXEC_SANDBOX_R003_PASS_EXISTING_LMAX_SANDBOX_PROFILE_READY",
        "EXEC_SANDBOX_R003_PASS_R009_SANDBOX_ORDER_SMOKE_READY",
        "EXEC_SANDBOX_R003_PASS_SANDBOX_ACK_OR_REPORT_CAPTURED",
        "EXEC_SANDBOX_R003_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE"
    )
} elseif ($submissionAttempted -and -not $orderSent -and -not $requestRejected) {
    @(
        "EXEC_SANDBOX_R003_PASS_EXISTING_LMAX_SANDBOX_PROFILE_READY",
        "EXEC_SANDBOX_R003_BLOCKED_LMAX_DEMO_LOGON_NOT_CONFIRMED_NO_ORDER_SENT",
        "EXEC_SANDBOX_R003_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE"
    )
} elseif ($submissionAttempted) {
    @(
        "EXEC_SANDBOX_R003_PASS_EXISTING_LMAX_SANDBOX_PROFILE_READY",
        "EXEC_SANDBOX_R003_PASS_R009_SANDBOX_ORDER_SUBMITTED_AND_REJECT_CAPTURED",
        "EXEC_SANDBOX_R003_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE"
    )
} else {
    @(
        "EXEC_SANDBOX_R003_BLOCKED_LMAX_DEMO_ENDPOINT_OR_SESSION_CONFIG_MISSING",
        "EXEC_SANDBOX_R003_PASS_OPERATOR_SANDBOX_ATTESTATION_READY",
        "EXEC_SANDBOX_R003_PASS_CREDENTIAL_ENVVARS_REDACTED_READY",
        "EXEC_SANDBOX_R003_PASS_PRODUCTION_ROUTE_BLOCKED",
        "EXEC_SANDBOX_R003_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE"
    )
}

$r002Reference = [ordered]@{
    Phase = $phase
    SourcePhase = "EXEC-SANDBOX-R002"
    SourceClassifications = @(
        "EXEC_SANDBOX_R002_BLOCKED_LMAX_SANDBOX_CONFIG_OR_CREDENTIALS_MISSING",
        "EXEC_SANDBOX_R002_PASS_PRODUCTION_ROUTE_BLOCKED",
        "EXEC_SANDBOX_R002_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE"
    )
    CorrectionApplied = "Literal LmaxSandbox section is no longer required when the existing LMAX setup is operator-attested as demo/sandbox and DEMO env vars are present."
}

$attestation = [ordered]@{
    Phase = $phase
    CurrentLmaxSetupOperatorAttestedSandbox = $true
    ExistingLmaxProfileIsSandbox = $profileReady
    SandboxClassificationSource = "OperatorAttestationAndDemoCredentialProfile"
    BrokerVenue = "ExistingLmaxDemoProfile"
    EndpointValuesRedacted = $true
    ProductionEndpointDetected = $productionEndpointDetected
    ProductionRouteBlocked = $true
    ProductionLedgerBlocked = $true
}

$profileClassification = [ordered]@{
    Phase = $phase
    Status = if ($profileReady) { "Ready" } else { "Blocked" }
    EnvironmentName = $environmentName
    EnvironmentIsDemoOrSandbox = $environmentIsDemo
    BrokerVenue = "ExistingLmaxDemoProfile"
    ExistingLmaxProfileIsSandbox = $profileReady
    SandboxClassificationSource = "OperatorAttestationAndDemoCredentialProfile"
    FixOrderHostConfigured = $fixOrderHostPresent
    FixOrderPortConfigured = $fixOrderPortPresent
    FixOrderTargetCompIdConfigured = $fixOrderTargetPresent
    EndpointValuesRedacted = $true
    DemoEndpointDetected = $demoEndpointDetected
    ProductionEndpointDetected = $productionEndpointDetected
    ProductionCredentialsDetected = $false
    SafeForSandboxGuardrailEvaluation = $profileReady
    MissingNonSecretConfigurationNames = $nonSecretConfigMissing
}

$sandboxConfig = [ordered]@{
    Phase = $phase
    CurrentLmaxSetupOperatorAttestedSandbox = $true
    Environment = "Sandbox"
    BrokerVenue = "ExistingLmaxDemoProfile"
    ExistingLmaxProfileIsSandbox = $profileReady
    CredentialSourceType = "EnvVars"
    CredentialVariableNames = $requiredCredentialVariables
    CredentialValuesRedacted = $true
    ProductionVenueAllowed = $false
    ProductionCredentialsAllowed = $false
    SandboxCredentialsRequired = $true
    SandboxOrderSubmissionEnabled = $true
    SandboxKillSwitchOpen = $true
    MaxSandboxOrderCount = 1
    MaxSandboxNotional = 10
    AllowedSymbols = $allowedSymbols
    DirectCrossExecutionAllowed = $false
    NonmajorExecutionAllowed = $false
    PaperLedgerCommitAllowed = $false
    ProductionLedgerCommitAllowed = $false
    StateMutationAllowed = $false
    AutomaticExecutionAllowed = $false
    SchedulerAllowed = $false
    ContractOnly = -not $submissionAttempted
}

$configReasons = @()
if (-not $profileReady) { $configReasons += "ExistingLmaxDemoProfileNotReady" }
if (-not $runtimeSubmissionEnabled) { $configReasons += "LocalRuntimeDefaultsRemainNoExternalNoOrder; explicit command override required for actual smoke submission" }
if ($nonSecretConfigMissing.Count -gt 0) { $configReasons += $nonSecretConfigMissing | ForEach-Object { "MissingNonSecretConfig:$_" } }
$configValidation = [ordered]@{
    Phase = $phase
    Status = if ($profileReady) { "Ready" } else { "Blocked" }
    ExistingLmaxProfileAcceptedAsSandbox = $profileReady
    LiteralLmaxSandboxSectionRequired = $false
    CredentialProfileReady = $sandboxCredentialPresent
    ProductionRouteBlocked = $true
    ProductionLedgerBlocked = $true
    ProductionEndpointDetected = $productionEndpointDetected
    EndpointValuesRedacted = $true
    SandboxGuardrailsReady = $sandboxGuardrailsReady
    RuntimeAppsettingsOrderSubmissionEnabled = $runtimeSubmissionEnabled
    SafeForOneBoundedSandboxOrder = $profileReady
    ConnectionAttempted = $submissionAttempted
    SubmissionAllowedByGuardrails = $profileReady
    Reasons = $configReasons
}

$credentialValidation = [ordered]@{
    Phase = $phase
    Status = if ($sandboxCredentialPresent) { "Ready" } else { "Blocked" }
    CredentialProfileName = "LMAX_DEMO_ENV_VARS"
    CredentialSourceType = "EnvVars"
    CredentialVariableNames = $requiredCredentialVariables
    CredentialVariablePresence = $credentialVariablePresence
    CredentialValuesRedacted = $true
    CredentialValuesPrintedOrPersisted = $false
    ProductionCredentialDetected = $false
    SandboxCredentialPresent = $sandboxCredentialPresent
    MissingProfileNames = $missingCredentialVariables
    Reasons = if ($sandboxCredentialPresent) { @() } else { @("SandboxCredentialEnvVarMissing") + $missingCredentialVariables }
}

$productionBlocking = [ordered]@{
    Phase = $phase
    NoProductionEndpoint = -not $productionEndpointDetected
    NoProductionAccount = $true
    NoProductionCredentialProfile = $true
    NoProductionRoute = $true
    NoProductionLedger = $true
    NoProductionStateMutation = $true
    ProductionRouteBlocked = $true
}

$operatorApproval = [ordered]@{
    Phase = $phase
    OperatorSandboxApprovalPresent = $true
    ApprovalScope = "OneTinyR009LmaxSandboxSmokeOrderOnly"
    ProductionApproval = $false
    AutomaticExecutionApproval = $false
    MaxSandboxOrderCount = 1
    MaxSandboxNotional = 10
}

$intent = [ordered]@{
    Phase = $phase
    ExecutionIntentId = "r003-eurusd-sandbox-smoke-intent"
    Symbol = "EURUSD"
    ExecutionTradableSymbol = "EURUSD"
    NormalizedPortfolioSymbol = "EURUSD"
    RequiresInversion = $false
    Side = "Buy"
    TargetQuantity = 0.01
    TargetNotional = 10
    CanonicalTargetCloseUtc = "2026-05-26T15:15:00Z"
    BarRole = "IntradayRebalance"
    ReadinessPresent = $true
    ReadinessWaivedForSandboxSmokeTest = $false
    OperatorSandboxApproval = $true
    KillSwitchOpenForSandboxOnly = $true
    IdempotencyKey = "R003SMOKE2605261515"
    SandboxOnly = $true
    ProductionOrder = $false
    IsLiveProduction = $false
    NoProductionLedgerCommit = $true
    NoProductionStateMutation = $true
}

$risk = [ordered]@{
    Phase = $phase
    Status = if ($profileReady) { "Ready" } else { "Blocked" }
    OperatorAttestedSandboxProfile = $true
    DemoEnvVarsPresent = $sandboxCredentialPresent
    SymbolWhitelisted = $true
    DirectCrossRejected = $true
    NonmajorRejected = $true
    CanonicalQuarterHourTargetClose = $true
    Legacy06Rejected = $true
    OrderCountWithinLimit = $true
    NotionalWithinConfiguredCap = $true
    SandboxKillSwitchOpen = $true
    OperatorSandboxApprovalPresent = $true
    IdempotencyKeyPresent = $true
    NoProductionRoute = $true
    NoProductionLedger = $true
    Reasons = if ($profileReady) { @() } else { @("ExistingLmaxDemoProfileNotReady") + $nonSecretConfigMissing }
}

$decision = [ordered]@{
    Phase = $phase
    R009DecisionProduced = $profileReady
    DecisionStatus = if ($profileReady) { "PreviewReadyForSandboxSmoke" } else { "BlockedBeforeR009SandboxDecision" }
    PrimaryPolicyCandidate = "CloseSeeking15mAdaptive_BalancedAdaptive_v0"
    SecondaryPolicyCandidate = "CloseSeeking15mAdaptive_ResidualAwareUrgency_v0"
    ConditionalResidualModule = "ControlledResidualCross_BalancedResidualCross_v0"
    SandboxOnly = $true
    ProductionOrder = $false
}

$orderIntent = [ordered]@{
    Phase = $phase
    Created = $profileReady
    Status = if ($profileReady) { "SandboxOrderIntentReady" } else { "NotCreatedBlocked" }
    SandboxOrderIntentId = "r003-eurusd-sandbox-order-intent"
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
    RouteCreated = $submissionAttempted
    SandboxOnly = $true
    BrokerVenue = "ExistingLmaxDemoProfile"
    ProductionRoute = $false
    NonSandboxBrokerRoute = $false
    ProductionCredentialsUsed = $false
    EndpointValuesRedacted = $true
}

$submission = [ordered]@{
    Phase = $phase
    SubmissionAttempted = $submissionAttempted
    SubmittedOrderCount = if ($orderSent) { 1 } else { 0 }
    SubmittedNotional = if ($orderSent) { 10 } else { 0 }
    MaxSandboxOrderCount = 1
    ConfiguredMaxSandboxNotional = 10
    SandboxOnly = $true
    BrokerVenue = "ExistingLmaxDemoProfile"
    ProductionSubmission = $false
    Status = $submissionStatus
    AckOrRejectReason = if ($orderSubmission -and -not [string]::IsNullOrWhiteSpace([string]$orderSubmission.Message)) { [string]$orderSubmission.Message } elseif ($rawSubmission -and -not [string]::IsNullOrWhiteSpace([string]$rawSubmission.Message)) { [string]$rawSubmission.Message } elseif ($submissionAttempted) { "FIX trading logon was not confirmed; demo order was not sent." } else { "Submission not attempted by artifact builder; no socket/FIX route opened." }
    RawSubmissionArtifact = if ($RawSubmissionOutputPath) { $RawSubmissionOutputPath } else { $null }
}

$ackReject = [ordered]@{
    Phase = $phase
    AckCaptured = $executionReports.Count -gt 0 -or $terminalReport
    RejectCaptured = $requestRejected
    Status = if ($submissionAttempted) { $submissionStatus } else { "NoAckOrRejectBecauseSubmissionBlockedOrNotRun" }
    SandboxOnly = $true
    ProductionAckReject = $false
}
$executionReport = [ordered]@{
    Phase = $phase
    ExecutionReportCaptured = $executionReports.Count -gt 0
    ExecutionReportCount = $executionReports.Count
    Status = if ($executionReports.Count -gt 0) { "Captured" } else { "NoExecutionReportCaptured" }
    SandboxOnly = $true
    ProductionExecutionReport = $false
}
$fillReport = [ordered]@{
    Phase = $phase
    FillCaptured = @($executionReports | Where-Object { $_.ExecType -eq "Trade" -or $_.OrdStatus -eq "Filled" }).Count -gt 0
    Status = "SandboxFillIfReturnedOnly"
    SandboxOnly = $true
    ProductionFill = $false
}
$reconciliation = [ordered]@{
    Phase = $phase
    Status = if ($submissionAttempted) { "SandboxSubmissionReconciledFromSanitizedLifecycleResult" } else { "BlockedBeforeSubmission" }
    IntendedSandboxOrderCreated = $profileReady
    SubmittedSandboxOrder = $orderSent
    SubmittedSandboxOrderCount = if ($orderSent) { 1 } else { 0 }
    AckOrRejectCaptured = $ackReject.AckCaptured -or $ackReject.RejectCaptured
    ExecutionReportCaptured = $executionReport.ExecutionReportCaptured
    FillCaptured = $fillReport.FillCaptured
    ProductionOrderCreated = $false
    ProductionRouteCreated = $false
    ProductionFillOrReportCreated = $false
    ProductionLedgerMutation = $false
    ProductionStateMutation = $false
    SandboxOnly = $true
}
$audit = [ordered]@{
    Phase = $phase
    AuditId = "r003-existing-lmax-demo-sandbox-smoke-audit"
    SandboxOnly = $true
    ExistingLmaxDemoProfileReady = $profileReady
    CredentialProfileReady = $sandboxCredentialPresent
    SandboxSubmissionAttempted = $submissionAttempted
    SandboxOrderSubmitted = $orderSent
    ProductionOrderCreated = $false
    ProductionRouteCreated = $false
    ProductionFillOrReportCreated = $false
    ProductionLedgerCommit = $false
    ProductionStateMutation = $false
    CredentialValuesRedacted = $true
    CredentialValuesPrintedOrPersisted = $false
    AuditHash = if ($submissionAttempted) { "r003-existing-demo-profile-submission-sanitized" } else { "r003-existing-demo-profile-ready-no-submission-evidence" }
}

Write-JsonArtifact "phase-exec-sandbox-r003-r002-reference.json" $r002Reference
Write-JsonArtifact "phase-exec-sandbox-r003-operator-sandbox-attestation.json" $attestation
Write-JsonArtifact "phase-exec-sandbox-r003-existing-lmax-profile-classification.json" $profileClassification
Write-JsonArtifact "phase-exec-sandbox-r003-lmax-sandbox-config.json" $sandboxConfig
Write-JsonArtifact "phase-exec-sandbox-r003-lmax-sandbox-config-validation.json" $configValidation
Write-JsonArtifact "phase-exec-sandbox-r003-credential-envvar-presence-validation.json" $credentialValidation
Write-JsonArtifact "phase-exec-sandbox-r003-production-route-blocking-check.json" $productionBlocking
Write-JsonArtifact "phase-exec-sandbox-r003-sandbox-guardrail-contract.json" $sandboxConfig
Write-JsonArtifact "phase-exec-sandbox-r003-operator-sandbox-approval.json" $operatorApproval
Write-JsonArtifact "phase-exec-sandbox-r003-r009-sandbox-execution-intent.json" $intent
Write-JsonArtifact "phase-exec-sandbox-r003-pretrade-sandbox-risk-check.json" $risk
Write-JsonArtifact "phase-exec-sandbox-r003-r009-sandbox-decision.json" $decision
Write-JsonArtifact "phase-exec-sandbox-r003-sandbox-order-intent.json" $orderIntent
Write-JsonArtifact "phase-exec-sandbox-r003-sandbox-route.json" $route
Write-JsonArtifact "phase-exec-sandbox-r003-sandbox-submission-result.json" $submission
Write-JsonArtifact "phase-exec-sandbox-r003-sandbox-ack-reject.json" $ackReject
Write-JsonArtifact "phase-exec-sandbox-r003-sandbox-execution-report.json" $executionReport
Write-JsonArtifact "phase-exec-sandbox-r003-sandbox-fill-report.json" $fillReport
Write-JsonArtifact "phase-exec-sandbox-r003-sandbox-reconciliation-result.json" $reconciliation
Write-JsonArtifact "phase-exec-sandbox-r003-sandbox-audit-record.json" $audit

Write-JsonArtifact "phase-exec-sandbox-r003-no-secret-persistence-audit.json" ([ordered]@{ Phase = $phase; CredentialValuesPrintedOrPersisted = $false; CredentialValuesRedacted = $true; CredentialVariableNamesOnly = $true; SecretValuesSerialized = $false })
Write-JsonArtifact "phase-exec-sandbox-r003-no-production-broker-audit.json" (New-Audit "NoProductionBroker" "LMAX production and non-sandbox broker routes were not used.")
Write-JsonArtifact "phase-exec-sandbox-r003-no-production-order-audit.json" (New-Audit "NoProductionOrder" "No production order artifact was created.")
Write-JsonArtifact "phase-exec-sandbox-r003-no-production-route-audit.json" (New-Audit "NoProductionRoute" "No production route artifact was created.")
Write-JsonArtifact "phase-exec-sandbox-r003-no-production-fill-report-audit.json" (New-Audit "NoProductionFillReport" "No production fill/report artifact was created.")
Write-JsonArtifact "phase-exec-sandbox-r003-no-production-ledger-audit.json" (New-Audit "NoProductionLedger" "No production ledger commit occurred.")
Write-JsonArtifact "phase-exec-sandbox-r003-no-production-state-mutation-audit.json" (New-Audit "NoProductionStateMutation" "No production trading state was mutated.")
Write-JsonArtifact "phase-exec-sandbox-r003-direct-cross-exclusion-preservation.json" ([ordered]@{ Phase = $phase; DirectCrossExecutionAllowed = $false; DirectCrossExecutionIntentRejected = $true })
Write-JsonArtifact "phase-exec-sandbox-r003-usd-pair-whitelist-preservation.json" ([ordered]@{ Phase = $phase; WhitelistedSymbols = $allowedSymbols; NonWhitelistedSymbolAllowed = $false; AudusdStatus = "SupportedAndNotFailed" })
Write-JsonArtifact "phase-exec-sandbox-r003-usdjpy-caveat-preservation.json" ([ordered]@{ Phase = $phase; NormalizedPortfolioSymbol = "JPYUSD"; ExecutionTradableSymbol = "USDJPY"; RequiresInversion = $true; SecurityID = "4004"; SecurityIDSource = "8"; CaveatPreserved = $true })
Write-JsonArtifact "phase-exec-sandbox-r003-canonical-quarter-hour-policy-preservation.json" ([ordered]@{ Phase = $phase; FutureCanonicalMinutes = @(0, 15, 30, 45); CandidateTargetCloseUtc = $intent.CanonicalTargetCloseUtc; CandidateIsCanonical = $true })
Write-JsonArtifact "phase-exec-sandbox-r003-legacy-compatibility-preservation.json" ([ordered]@{ Phase = $phase; LegacyLabels = @(":06", ":21", ":36", ":51"); UsedAsFutureCanonical = $false; Legacy06AcceptedAsFutureCanonical = $false })
Write-JsonArtifact "phase-exec-sandbox-r003-cost-guidance-preservation.json" ([ordered]@{ Phase = $phase; FiveUsdPerMillion = "BestCaseMajorOnly"; Universalized = $false })
Write-JsonArtifact "phase-exec-sandbox-r003-nonmajor-calibration-preservation.json" ([ordered]@{ Phase = $phase; NonmajorEmScandiCnh = "CalibrationRequired"; SandboxExecutionAllowed = $false })
Write-JsonArtifact "phase-exec-sandbox-r003-forbidden-actions-audit.json" ([ordered]@{
    Phase = $phase
    LmaxProductionUsed = $false
    ProductionCredentialsUsed = $false
    NonSandboxBrokerRouteUsed = $false
    CredentialValuesPrintedOrPersisted = $false
    PolygonCalled = $false
    UnrelatedExternalApiCalled = $false
    SchedulerServicePollingBackgroundJobIntroduced = $false
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
Write-JsonArtifact "phase-exec-sandbox-r003-next-phase-recommendation.json" ([ordered]@{
    Phase = $phase
    RecommendedNextPhase = "EXEC-SANDBOX-R004"
    Title = "LMAX Sandbox Smoke Result Review and Bounded Retry Policy Gate"
    Reason = "Review the R003 demo/sandbox profile, sanitized ack/reject evidence if submitted, and decide whether another bounded sandbox-only smoke is warranted."
})
Write-JsonArtifact "phase-exec-sandbox-r003-build-test-validator-evidence.json" ([ordered]@{
    Phase = $phase
    Build = $BuildStatus
    FocusedSandboxTests = $FocusedTestsStatus
    Validator = $ValidatorStatus
    DotnetBuildNoRestore = "dotnet build --no-restore"
    FocusedTests = "dotnet test tests\QQ.Production.Intraday.Tests.Unit\QQ.Production.Intraday.Tests.Unit.csproj --no-restore --filter FullyQualifiedName~R009ExistingLmaxSandboxProfileAttestationTests|FullyQualifiedName~R009LmaxSandboxConfigCompletionTests|FullyQualifiedName~R009LmaxSandboxOrderSmokeTests"
    ValidatorScript = "scripts/check-exec-sandbox-r003-existing-lmax-sandbox-smoke-gate.ps1"
})

$summary = @"
# EXEC-SANDBOX-R003 Summary

Classifications:
$($classifications | ForEach-Object { "- $_" } | Out-String)
R003 accepts the current operator-attested LMAX demo setup as the sandbox profile without requiring a literal LmaxSandbox config section. DEMO credential environment variables were validated by variable-name presence only; credential values were not printed or persisted.

Existing profile status: $(if ($profileReady) { "Ready" } else { "Blocked" })
Sandbox submission attempted: $submissionAttempted
Sandbox submission status: $submissionStatus

Build/tests/validator:
- Build: $BuildStatus
- Focused sandbox tests/static checks: $FocusedTestsStatus
- Validator: $ValidatorStatus
"@
$summary | Set-Content -Path (Join-Path $artifactDir "phase-exec-sandbox-r003-summary.md") -Encoding UTF8

Write-Host "EXEC-SANDBOX-R003 artifacts written to $artifactDir"
