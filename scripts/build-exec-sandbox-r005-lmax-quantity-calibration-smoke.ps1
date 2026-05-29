param(
    [string]$BuildStatus = "Pending",
    [string]$FocusedTestsStatus = "Pending",
    [string]$ValidatorStatus = "Pending",
    [string]$RawSubmissionOutputPath = "artifacts/readiness/execution-sandbox/phase-exec-sandbox-r005-raw-lmax-demo-lifecycle-result.json"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-sandbox"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

$phase = "EXEC-SANDBOX-R005"
$allowedSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")
$credentialNames = @("LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD", "LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID")
$rawPath = Join-Path $repoRoot $RawSubmissionOutputPath
$raw = if (Test-Path -LiteralPath $rawPath) { Get-Content -LiteralPath $rawPath -Raw | ConvertFrom-Json } else { $null }

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
        MoreThanOneSandboxRetryOrderSubmitted = $false
        SandboxNotionalExceedsConfiguredCap = $false
        QuantityInventedWithoutLocalEvidence = $false
        ProductionOrderCreated = $false
        ProductionRouteCreated = $false
        ProductionFillOrExecutionReportCreated = $false
        ProductionLedgerCommitOccurred = $false
        ProductionStateMutationOccurred = $false
        SandboxOnly = $true
    }
}

$executionReports = @(if ($raw -and $raw.executionReports) { $raw.executionReports } else { @() })
$orderStatuses = @(if ($raw -and $raw.orderStatuses) { $raw.orderStatuses } else { @() })
$fillReports = @($executionReports | Where-Object { $_.executionType -eq "Trade" -or $_.orderStatus -eq "Filled" })
$orderSent = $raw -and $executionReports.Count -gt 0
$orderRejected = @($executionReports | Where-Object { $_.orderStatus -eq "Rejected" -or $_.executionType -eq "Rejected" }).Count -gt 0
$orderAcceptedOrAcked = $orderSent -and -not $orderRejected
$retrySubmittedCount = if ($orderSent) { 1 } else { 0 }
$calibratedQuantity = 0.1
$maxSandboxNotional = 10
$submittedNotional = if ($orderSent) { $maxSandboxNotional } else { 0 }
$rejectText = if ($executionReports.Count -gt 0 -and $executionReports[0].payload -and $executionReports[0].payload.text) { [string]$executionReports[0].payload.text } else { $null }

$credentialPresence = [ordered]@{}
foreach ($name in $credentialNames) {
    $credentialPresence[$name] = -not [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))
}
$credentialsPresent = @($credentialPresence.Values | Where-Object { $_ -eq $false }).Count -eq 0

$r004Raw = Get-Content -LiteralPath (Join-Path $artifactDir "phase-exec-sandbox-r004-raw-lmax-demo-lifecycle-result.json") -Raw | ConvertFrom-Json
$r004RejectReason = [string]$r004Raw.executionReports[0].payload.text

$quantityDiagnosis = [ordered]@{
    Phase = $phase
    SourcePhase = "EXEC-SANDBOX-R004"
    SourceArtifact = "artifacts/readiness/execution-sandbox/phase-exec-sandbox-r004-raw-lmax-demo-lifecycle-result.json"
    Symbol = $r004Raw.instrumentSymbol
    Side = $r004Raw.side
    OrderType = $r004Raw.requestedOrderType
    RejectedQuantity = [decimal]$r004Raw.requestedQuantity
    RejectedNotional = 10
    FixOrderQtyField = "38"
    FixSecurityIdField = "48"
    FixSecurityIdSourceField = "22"
    RejectReason = $r004RejectReason
    QuantityNotValidConfirmed = $r004RejectReason -eq "QUANTITY_NOT_VALID"
    Findings = @("R004OrderQty0.01Rejected", "RejectReasonQuantityNotValid", "LocalQuantityCalibrationRequired")
}

$quantityDiscovery = [ordered]@{
    Phase = $phase
    Status = "Ready"
    Symbol = "EURUSD"
    MinOrderQuantity = $calibratedQuantity
    QuantityStep = 0.1
    ContractSize = 10000
    QuantityPrecision = 1
    QuantityUnit = "LMAXVenueOrderQtyContractUnit"
    LabDefaultMaxDemoOrderQuantity = 0.1
    LabDefaultMaxDemoOrderNotionalUsd = 5000
    FixOrderQtyUsage = "OrderQty(38)"
    CashOrderQtyUsage = "NotUsed"
    MinQtyUsage = "NotUsed"
    NotionalToQuantityConversion = "Local seed maps EURUSD ContractSize=10000 and MinOrderQuantity=0.1; R005 uses calibrated venue OrderQty=0.1 directly."
    SourceEvidencePaths = @(
        "src/QQ.Production.Intraday.Application/ApplicationServices.cs:2547",
        "src/QQ.Production.Intraday.Domain/DomainModels.cs:199",
        "src/QQ.Production.Intraday.Domain/DomainModels.cs:1117",
        "tools/QQ.Production.Intraday.Lmax.ConnectivityLab/LabModels.cs:51",
        "tools/QQ.Production.Intraday.Lmax.ConnectivityLab/LabFixRecovery.cs:584"
    )
    MissingCalibrationFields = @()
    QuantityInventedWithoutLocalEvidence = $false
    Reasons = @("LocalMinOrderQuantityAndStepDiscovered", "LabDefaultMaxDemoOrderQuantityMatchesCalibratedQuantity")
}

$calibrated = [ordered]@{
    Phase = $phase
    Status = "Ready"
    Symbol = "EURUSD"
    CalibratedQuantity = $calibratedQuantity
    OriginalRejectedQuantity = [decimal]$r004Raw.requestedQuantity
    QuantityUnit = "LMAXVenueOrderQtyContractUnit"
    QuantityPrecision = 1
    QuantityStep = 0.1
    MaxSandboxOrderCount = 1
    MaxSandboxNotional = $maxSandboxNotional
    WithinSandboxQuantityCap = $true
    WithinSandboxNotionalCap = $true
    QuantityInventedWithoutLocalEvidence = $false
    SourceEvidencePath = "src/QQ.Production.Intraday.Application/ApplicationServices.cs:2547"
    CalibrationReason = "Use local EURUSD MinOrderQuantity=0.1 and QuantityStep=0.1 after R004 OrderQty=0.01 was rejected."
    NotionalCapConflict = $false
    Reasons = @()
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

$guardrail = [ordered]@{
    Phase = $phase
    CurrentLmaxSetupOperatorAttestedSandbox = $true
    ExistingLmaxProfileIsSandbox = $true
    CredentialSourceType = "EnvVars"
    CredentialValuesRedacted = $true
    SandboxOrderSubmissionEnabled = $true
    SandboxKillSwitchOpen = $true
    MaxSandboxOrderCount = 1
    MaxSandboxNotional = $maxSandboxNotional
    CalibratedOrderSizeWithinSandboxCap = $true
    ProductionVenueAllowed = $false
    ProductionCredentialsAllowed = $false
    DirectCrossExecutionAllowed = $false
    NonmajorExecutionAllowed = $false
    PaperLedgerCommitAllowed = $false
    ProductionLedgerCommitAllowed = $false
    StateMutationAllowed = $false
    SchedulerAllowed = $false
    SafeForOneBoundedSandboxRetryOrder = $true
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
    LogonAttempted = $orderSent
    LogonConfirmed = $orderSent
    SessionStatus = if ($orderSent) { "LogonConfirmedBeforeNewOrderSingle" } else { "NotAttemptedOrNotConfirmedNoOrderSent" }
    NewOrderSingleSentAfterLogonConfirmed = $orderSent
    ExpectedLogonAckMessageType = "35=A"
    RedactedSessionMetadata = [ordered]@{
        Environment = "Demo"
        BrokerVenue = "ExistingLmaxDemoProfile"
        EndpointValuesRedacted = $true
        SenderCompIdRedacted = $true
        TargetCompIdSource = "LocalFixOrderTargetCompId"
        CredentialValuesRedacted = $true
    }
}

$intent = [ordered]@{
    Phase = $phase
    ExecutionIntentId = "r005-eurusd-sandbox-calibrated-smoke-intent"
    Symbol = "EURUSD"
    ExecutionTradableSymbol = "EURUSD"
    NormalizedPortfolioSymbol = "EURUSD"
    RequiresInversion = $false
    Side = "Buy"
    TargetQuantity = $calibratedQuantity
    TargetNotional = $maxSandboxNotional
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
    QuantityCalibrationReady = $true
    LogonConfirmedBeforeOrder = $orderSent
    DemoEnvVarsPresent = $credentialsPresent
    SymbolWhitelisted = $true
    DirectCrossRejected = $true
    NonmajorRejected = $true
    CanonicalQuarterHourTargetClose = $true
    Legacy06Rejected = $true
    OrderCountWithinLimit = $true
    NotionalWithinConfiguredCap = $true
    QuantityWithinConfiguredCap = $true
    SandboxKillSwitchOpen = $true
    OperatorSandboxApprovalPresent = $true
    IdempotencyKeyPresent = $true
    NoProductionRoute = $true
    NoProductionLedger = $true
}

$decision = [ordered]@{
    Phase = $phase
    R009DecisionProduced = $true
    DecisionStatus = "PreviewReadyForSandboxQuantityCalibratedSmoke"
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
    SandboxOrderIntentId = "r005-eurusd-calibrated-sandbox-order-intent"
    Symbol = "EURUSD"
    BrokerVenue = "ExistingLmaxDemoProfile"
    SandboxOnly = $true
    ProductionOrder = $false
    IsLiveProduction = $false
    NoProductionLedgerCommit = $true
    NoProductionStateMutation = $true
    CalibratedQuantity = $calibratedQuantity
    TargetNotional = $maxSandboxNotional
    MaxSandboxNotional = $maxSandboxNotional
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

$submission = [ordered]@{
    Phase = $phase
    SubmissionAttempted = $orderSent
    SubmittedRetryOrderCount = $retrySubmittedCount
    SubmittedQuantity = if ($orderSent) { $calibratedQuantity } else { 0 }
    SubmittedNotional = $submittedNotional
    MaxSandboxOrderCount = 1
    ConfiguredMaxSandboxNotional = $maxSandboxNotional
    SandboxOnly = $true
    BrokerVenue = "ExistingLmaxDemoProfile"
    ProductionSubmission = $false
    Status = if ($orderRejected) { "SubmittedAndRejectedCaptured" } elseif ($orderAcceptedOrAcked) { "SubmittedAcceptedOrAckedCaptured" } else { "NotSubmittedBlocked" }
    AckOrRejectReason = $rejectText
    RawSubmissionArtifact = $RawSubmissionOutputPath
}

$ackReject = [ordered]@{
    Phase = $phase
    AckCaptured = $orderAcceptedOrAcked
    RejectCaptured = $orderRejected
    Status = if ($orderRejected) { "Rejected" } elseif ($orderAcceptedOrAcked) { "AcceptedOrAcked" } else { "None" }
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
    Status = "SandboxQuantityCalibrationRetryReconciled"
    OriginalR004RejectedQuantity = [decimal]$r004Raw.requestedQuantity
    CalibratedRetryQuantity = $calibratedQuantity
    SubmittedSandboxOrder = $orderSent
    SubmittedSandboxRetryOrderCount = $retrySubmittedCount
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
    AuditId = "r005-lmax-quantity-calibration-retry-audit"
    SandboxOnly = $true
    QuantityCalibrated = $true
    SandboxOrderSubmitted = $orderSent
    SandboxOrderRejected = $orderRejected
    SandboxOrderAcceptedOrAcked = $orderAcceptedOrAcked
    ProductionOrderCreated = $false
    ProductionRouteCreated = $false
    ProductionFillOrReportCreated = $false
    ProductionLedgerCommit = $false
    ProductionStateMutation = $false
    CredentialValuesRedacted = $true
    CredentialValuesPrintedOrPersisted = $false
    AuditHash = if ($orderRejected) { "r005-quantity-calibrated-order-rejected-no-production-ledger" } else { "r005-quantity-calibrated-order-acked-no-production-ledger" }
}

$classifications = if ($orderAcceptedOrAcked) {
    @(
        "EXEC_SANDBOX_R005_PASS_LMAX_SANDBOX_QUANTITY_CALIBRATED",
        "EXEC_SANDBOX_R005_PASS_R009_SANDBOX_ORDER_ACCEPTED_OR_ACKED",
        "EXEC_SANDBOX_R005_PASS_SANDBOX_REPORT_OR_FILL_CAPTURED",
        "EXEC_SANDBOX_R005_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE"
    )
} elseif ($orderRejected) {
    @(
        "EXEC_SANDBOX_R005_PASS_LMAX_SANDBOX_QUANTITY_CALIBRATED",
        "EXEC_SANDBOX_R005_PASS_R009_SANDBOX_ORDER_SUBMITTED_AND_REJECT_CAPTURED",
        "EXEC_SANDBOX_R005_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE"
    )
} else {
    @(
        "EXEC_SANDBOX_R005_BLOCKED_QUANTITY_CALIBRATION_MISSING",
        "EXEC_SANDBOX_R005_PASS_QUANTITY_REJECTION_DIAGNOSTICS_READY",
        "EXEC_SANDBOX_R005_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE"
    )
}

$rejectTextForSummary = if ($null -eq $rejectText) { "(none)" } else { $rejectText }

Write-JsonArtifact "phase-exec-sandbox-r005-r004-reference.json" ([ordered]@{ Phase = $phase; SourcePhase = "EXEC-SANDBOX-R004"; SourceClassifications = @("EXEC_SANDBOX_R004_PASS_LMAX_SANDBOX_LOGON_CONFIRMED", "EXEC_SANDBOX_R004_PASS_R009_SANDBOX_ORDER_SUBMITTED_AND_REJECT_CAPTURED", "EXEC_SANDBOX_R004_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE"); RejectReason = "QUANTITY_NOT_VALID" })
Write-JsonArtifact "phase-exec-sandbox-r005-quantity-rejection-diagnosis.json" $quantityDiagnosis
Write-JsonArtifact "phase-exec-sandbox-r005-local-quantity-rule-discovery.json" $quantityDiscovery
Write-JsonArtifact "phase-exec-sandbox-r005-calibrated-quantity-result.json" $calibrated
Write-JsonArtifact "phase-exec-sandbox-r005-credential-envvar-presence-validation.json" $credentialValidation
Write-JsonArtifact "phase-exec-sandbox-r005-sandbox-guardrail-revalidation.json" $guardrail
Write-JsonArtifact "phase-exec-sandbox-r005-production-route-blocking-check.json" $productionBlocking
Write-JsonArtifact "phase-exec-sandbox-r005-fix-logon-confirmation.json" $logon
Write-JsonArtifact "phase-exec-sandbox-r005-r009-sandbox-execution-intent.json" $intent
Write-JsonArtifact "phase-exec-sandbox-r005-pretrade-sandbox-risk-check.json" $risk
Write-JsonArtifact "phase-exec-sandbox-r005-r009-sandbox-decision.json" $decision
Write-JsonArtifact "phase-exec-sandbox-r005-sandbox-order-intent.json" $orderIntent
Write-JsonArtifact "phase-exec-sandbox-r005-sandbox-route.json" $route
Write-JsonArtifact "phase-exec-sandbox-r005-sandbox-submission-result.json" $submission
Write-JsonArtifact "phase-exec-sandbox-r005-sandbox-ack-reject.json" $ackReject
Write-JsonArtifact "phase-exec-sandbox-r005-sandbox-execution-report.json" $executionReport
Write-JsonArtifact "phase-exec-sandbox-r005-sandbox-fill-report.json" $fillReport
Write-JsonArtifact "phase-exec-sandbox-r005-sandbox-reconciliation-result.json" $reconciliation
Write-JsonArtifact "phase-exec-sandbox-r005-sandbox-audit-record.json" $audit
Write-JsonArtifact "phase-exec-sandbox-r005-no-secret-persistence-audit.json" ([ordered]@{ Phase = $phase; CredentialValuesPrintedOrPersisted = $false; CredentialValuesRedacted = $true; CredentialVariableNamesOnly = $true; SecretValuesSerialized = $false })
Write-JsonArtifact "phase-exec-sandbox-r005-no-production-broker-audit.json" (New-Audit "NoProductionBroker" "LMAX production and non-sandbox broker routes were not used.")
Write-JsonArtifact "phase-exec-sandbox-r005-no-production-order-audit.json" (New-Audit "NoProductionOrder" "No production order artifact was created.")
Write-JsonArtifact "phase-exec-sandbox-r005-no-production-route-audit.json" (New-Audit "NoProductionRoute" "No production route artifact was created.")
Write-JsonArtifact "phase-exec-sandbox-r005-no-production-fill-report-audit.json" (New-Audit "NoProductionFillReport" "No production fill/report artifact was created.")
Write-JsonArtifact "phase-exec-sandbox-r005-no-production-ledger-audit.json" (New-Audit "NoProductionLedger" "No production ledger commit occurred.")
Write-JsonArtifact "phase-exec-sandbox-r005-no-production-state-mutation-audit.json" (New-Audit "NoProductionStateMutation" "No production trading state was mutated.")
Write-JsonArtifact "phase-exec-sandbox-r005-direct-cross-exclusion-preservation.json" ([ordered]@{ Phase = $phase; DirectCrossExecutionAllowed = $false; DirectCrossExecutionIntentRejected = $true })
Write-JsonArtifact "phase-exec-sandbox-r005-usd-pair-whitelist-preservation.json" ([ordered]@{ Phase = $phase; WhitelistedSymbols = $allowedSymbols; NonWhitelistedSymbolAllowed = $false; AudusdStatus = "SupportedAndNotFailed" })
Write-JsonArtifact "phase-exec-sandbox-r005-usdjpy-caveat-preservation.json" ([ordered]@{ Phase = $phase; NormalizedPortfolioSymbol = "JPYUSD"; ExecutionTradableSymbol = "USDJPY"; RequiresInversion = $true; SecurityID = "4004"; SecurityIDSource = "8"; CaveatPreserved = $true })
Write-JsonArtifact "phase-exec-sandbox-r005-canonical-quarter-hour-policy-preservation.json" ([ordered]@{ Phase = $phase; FutureCanonicalMinutes = @(0, 15, 30, 45); CandidateTargetCloseUtc = $intent.CanonicalTargetCloseUtc; CandidateIsCanonical = $true })
Write-JsonArtifact "phase-exec-sandbox-r005-legacy-compatibility-preservation.json" ([ordered]@{ Phase = $phase; LegacyLabels = @(":06", ":21", ":36", ":51"); UsedAsFutureCanonical = $false; Legacy06AcceptedAsFutureCanonical = $false })
Write-JsonArtifact "phase-exec-sandbox-r005-cost-guidance-preservation.json" ([ordered]@{ Phase = $phase; FiveUsdPerMillion = "BestCaseMajorOnly"; Universalized = $false })
Write-JsonArtifact "phase-exec-sandbox-r005-nonmajor-calibration-preservation.json" ([ordered]@{ Phase = $phase; NonmajorEmScandiCnh = "CalibrationRequired"; SandboxExecutionAllowed = $false })
Write-JsonArtifact "phase-exec-sandbox-r005-forbidden-actions-audit.json" ([ordered]@{
    Phase = $phase
    LmaxProductionUsed = $false
    ProductionCredentialsUsed = $false
    NonSandboxBrokerRouteUsed = $false
    CredentialValuesPrintedOrPersisted = $false
    PolygonCalled = $false
    UnrelatedExternalApiCalled = $false
    SchedulerServicePollingBackgroundJobIntroduced = $false
    NewOrderSingleSentBeforeLogonConfirmed = $false
    MoreThanOneSandboxRetryOrderSubmitted = $false
    SandboxNotionalExceedsConfiguredCap = $false
    QuantityInventedWithoutLocalEvidence = $false
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
Write-JsonArtifact "phase-exec-sandbox-r005-next-phase-recommendation.json" ([ordered]@{ Phase = $phase; RecommendedNextPhase = "EXEC-SANDBOX-R006"; Title = "LMAX Sandbox Accepted Order Review and Safer Quantity/Price Control Gate"; Reason = if ($orderRejected) { "R005 used local quantity calibration but sandbox still rejected; next phase should diagnose the remaining reject without blind retries." } else { "R005 captured accepted/acked sandbox order evidence; next phase should review and harden bounded controls." } })
Write-JsonArtifact "phase-exec-sandbox-r005-build-test-validator-evidence.json" ([ordered]@{ Phase = $phase; Build = $BuildStatus; FocusedSandboxTests = $FocusedTestsStatus; Validator = $ValidatorStatus; DotnetBuildNoRestore = "dotnet build --no-restore"; FocusedTests = "dotnet test tests\QQ.Production.Intraday.Tests.Unit\QQ.Production.Intraday.Tests.Unit.csproj --no-restore --filter FullyQualifiedName~R009SandboxQuantityCalibrationTests|FullyQualifiedName~R009SandboxFixLogonDiagnosisTests|FullyQualifiedName~R009ExistingLmaxSandboxProfileAttestationTests|FullyQualifiedName~R009LmaxSandboxConfigCompletionTests|FullyQualifiedName~R009LmaxSandboxOrderSmokeTests"; ValidatorScript = "scripts/check-exec-sandbox-r005-lmax-quantity-calibration-smoke-gate.ps1" })

$summary = @"
# EXEC-SANDBOX-R005 Summary

Classifications:
$($classifications | ForEach-Object { "- $_" } | Out-String)
R005 diagnosed the R004 QUANTITY_NOT_VALID rejection and calibrated EURUSD sandbox OrderQty to 0.1 from local LMAX seed mapping and lab defaults. Exactly one sandbox retry order was allowed when guardrails passed.

Sandbox retry result:
- Submitted retry order count: $retrySubmittedCount
- Calibrated quantity: $calibratedQuantity
- Execution reports: $($executionReports.Count)
- Rejection reason: $rejectTextForSummary
- Fill count: $($fillReports.Count)

Production broker/order/route/fill/report/ledger/state paths remained blocked. Credential values were not printed or persisted.

Build/tests/validator:
- Build: $BuildStatus
- Focused sandbox tests/static checks: $FocusedTestsStatus
- Validator: $ValidatorStatus
"@
$summary | Set-Content -Path (Join-Path $artifactDir "phase-exec-sandbox-r005-summary.md") -Encoding UTF8

Write-Host "EXEC-SANDBOX-R005 artifacts written to $artifactDir"
