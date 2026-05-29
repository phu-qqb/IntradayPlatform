param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Missing required file: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Assert-Equal($Actual, $Expected, [string]$Message) {
    if ($Actual -ne $Expected) {
        throw "$Message. Expected '$Expected' but got '$Actual'."
    }
}

function Assert-In($Actual, [string[]]$Expected, [string]$Message) {
    if ($Expected -notcontains $Actual) {
        throw "$Message. Value '$Actual' was not one of: $($Expected -join ', ')."
    }
}

function Assert-True($Actual, [string]$Message) {
    if ($Actual -ne $true) {
        throw "$Message. Expected true but got '$Actual'."
    }
}

function Assert-False($Actual, [string]$Message) {
    if ($Actual -ne $false) {
        throw "$Message. Expected false but got '$Actual'."
    }
}

function Assert-NoSensitiveContent([string]$Path) {
    $text = Get-Content -LiteralPath $Path -Raw
    $patterns = @(
        '(?i)password\s*[:=]\s*[^,\s\}\]]+',
        '(?i)api[_-]?key\s*[:=]\s*[^,\s\}\]]+',
        '(?i)secret\s*[:=]\s*[^,\s\}\]]+',
        '(?i)sessionpassword\s*[:=]\s*[^,\s\}\]]+',
        '(?i)credential\s*[:=]\s*[^,\s\}\]]+',
        '(?i)raw\s*fix\s*[:=]\s*[^,\s\}\]]+'
    )

    foreach ($pattern in $patterns) {
        if ($text -match $pattern) {
            throw "Sensitive-content marker found in $Path"
        }
    }
}

function Assert-RequiredArtifacts([string]$BasePath, [string[]]$Files) {
    foreach ($file in $Files) {
        $path = Join-Path $BasePath $file
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Missing required artifact: $path"
        }

        Assert-NoSensitiveContent $path
    }
}

function Assert-TextContains([string]$Text, [string]$Needle, [string]$Message) {
    if ($Text -notmatch [regex]::Escape($Needle)) {
        throw "$Message. Missing '$Needle'."
    }
}

function Assert-TextNotContainsPattern([string]$Text, [string]$Pattern, [string]$Message) {
    if ($Text -match $Pattern) {
        throw "$Message. Matched '$Pattern'."
    }
}

$readiness = Join-Path $RepoRoot "artifacts\readiness\lmax-runtime-enablement"
$usdJpy = Join-Path $RepoRoot "artifacts\readiness\usdjpy-troubleshooting"

$requiredPrior = @(
    "phase-lmax-r1-design-only-decision-gate.json",
    "phase-lmax-r2-preflight-decision-gate.json",
    "phase-lmax-r3-decision-gate.json",
    "phase-lmax-r4-remediation-decision-gate.json",
    "phase-lmax-r5-inert-implementation-decision-gate.json",
    "phase-lmax-r6-decision-gate.json",
    "phase-lmax-r7-decision-gate.json",
    "phase-lmax-r8-decision-gate.json",
    "phase-lmax-r9-decision-gate.json",
    "phase-lmax-r10-decision-gate.json",
    "phase-lmax-r11-decision-gate.json",
    "phase-lmax-r12-decision-gate.json",
    "phase-lmax-r13-decision-gate.json",
    "phase-lmax-r14-decision-gate.json",
    "phase-lmax-r15-decision-gate.json",
    "phase-lmax-r16-decision-gate.json",
    "phase-lmax-r17-decision-gate.json",
    "phase-lmax-r18-decision-gate.json",
    "phase-lmax-r19-decision-gate.json",
    "phase-lmax-r20-decision-gate.json",
    "phase-lmax-r21-decision-gate.json",
    "phase-lmax-r22-decision-gate.json",
    "phase-lmax-r23-decision-gate.json",
    "phase-lmax-r24-decision-gate.json",
    "phase-lmax-r25-decision-gate.json",
    "phase-lmax-r26-decision-gate.json",
    "phase-lmax-r27-decision-gate.json",
    "phase-lmax-r28-decision-gate.json",
    "phase-lmax-r29-decision-gate.json",
    "phase-lmax-r30-decision-gate.json",
    "phase-lmax-r30-gate-validation.json"
)

$requiredR31 = @(
    "phase-lmax-r31-operator-approval-record.json",
    "phase-lmax-r31-preflight-gate.json",
    "phase-lmax-r31-final-full-stack-validation.json",
    "phase-lmax-r31-temporary-runtime-activation-record.json",
    "phase-lmax-r31-approved-instrument-status-record.json",
    "phase-lmax-r31-sanitized-runtime-boundary-evidence.json",
    "phase-lmax-r31-forbidden-action-validation.json",
    "phase-lmax-r31-shutdown-revert-record.json",
    "phase-lmax-r31-post-attempt-non-mutation-validation.json",
    "phase-lmax-r31-rail-isolation-validation.json",
    "phase-lmax-r31-decision-gate.json",
    "phase-lmax-r31-temporary-readonly-runtime-report.md",
    "phase-lmax-r31-operator-note.md"
)

Write-Host "LMAX-R31 Temporary Read-Only Runtime Gate Validator"
Write-Host "This validator verifies R31 artifacts and confirms no forbidden action occurred."

Assert-RequiredArtifacts $readiness $requiredPrior
Assert-RequiredArtifacts $readiness $requiredR31

$t7GatePath = Join-Path $usdJpy "phase-usdjpy-t7-final-closure-gate.json"
$phase7nGatePath = Join-Path $RepoRoot "artifacts\readiness\phase7n-final-lmax-readonly-runtime-evidence-archive-closure-gate.json"
if (-not (Test-Path -LiteralPath $t7GatePath)) { throw "Missing USDJPY T7 closure gate: $t7GatePath" }
if (-not (Test-Path -LiteralPath $phase7nGatePath)) { throw "Missing Phase 7N closure gate: $phase7nGatePath" }
Assert-NoSensitiveContent $t7GatePath
Assert-NoSensitiveContent $phase7nGatePath

$r30Gate = Read-Json (Join-Path $readiness "phase-lmax-r30-decision-gate.json")
$approval = Read-Json (Join-Path $readiness "phase-lmax-r31-operator-approval-record.json")
$preflight = Read-Json (Join-Path $readiness "phase-lmax-r31-preflight-gate.json")
$stack = Read-Json (Join-Path $readiness "phase-lmax-r31-final-full-stack-validation.json")
$activation = Read-Json (Join-Path $readiness "phase-lmax-r31-temporary-runtime-activation-record.json")
$instrumentStatus = Read-Json (Join-Path $readiness "phase-lmax-r31-approved-instrument-status-record.json")
$boundary = Read-Json (Join-Path $readiness "phase-lmax-r31-sanitized-runtime-boundary-evidence.json")
$forbidden = Read-Json (Join-Path $readiness "phase-lmax-r31-forbidden-action-validation.json")
$shutdown = Read-Json (Join-Path $readiness "phase-lmax-r31-shutdown-revert-record.json")
$nonMutation = Read-Json (Join-Path $readiness "phase-lmax-r31-post-attempt-non-mutation-validation.json")
$isolation = Read-Json (Join-Path $readiness "phase-lmax-r31-rail-isolation-validation.json")
$gate = Read-Json (Join-Path $readiness "phase-lmax-r31-decision-gate.json")
$t7Gate = Read-Json $t7GatePath
$phase7nGate = Read-Json $phase7nGatePath

$expectedPhrase = "I, Philippe, explicitly approve Phase LMAX-R31 for one temporary Demo read-only runtime market-data activation attempt after the final provider sweep for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority."
Assert-Equal $r30Gate.finalDecision "LMAX_R30_FINAL_PROVIDER_SWEEP_COMPLETE_NO_EXTERNAL_ACTIVATION" "R30 decision"
Assert-Equal $t7Gate.finalDecision "USDJPY_T7_FINAL_READINESS_ARCHIVE_CLOSED_WITH_CAVEAT" "USDJPY T7 decision"
Assert-Equal $phase7nGate.finalDecision "PASS_FINAL_LMAX_READONLY_RUNTIME_EVIDENCE_ARCHIVE_CLOSED" "Phase 7N decision"
Assert-Equal $approval.phase "LMAX-R31" "Approval phase"
Assert-Equal $approval.operator "Philippe" "Approval operator"
Assert-Equal $approval.approvalPhrase $expectedPhrase "Approval phrase"
Assert-True $approval.approvalPhraseMatchedExactly "Approval phrase exact"

Assert-True $preflight.passed "Preflight passed"
Assert-False $preflight.productionAccount "Production account"
Assert-False $preflight.allowOrderSubmission "AllowOrderSubmission"
Assert-False $preflight.allowLiveTrading "AllowLiveTrading"
Assert-False $preflight.isTradingEnabled "IsTradingEnabled"
Assert-False $preflight.scheduler "Scheduler"
Assert-False $preflight.polling "Polling"
Assert-False $preflight.replay "Replay"
Assert-False $preflight.shadowReplay "Shadow replay"
Assert-False $preflight.tradingMutation "Trading mutation"
Assert-False $preflight.liveLauncherCreated "Live launcher"
Assert-False $preflight.hostedBackgroundServiceAdded "Hosted/background service"

Assert-True $stack.providerCompletenessConfirmed "Provider completeness"
Assert-True $stack.credentialConfigSourcePresent "Credential source presence"
Assert-False $stack.credentialValuesReturned "Credential values returned"
Assert-False $stack.credentialValuesPrinted "Credential values printed"
Assert-False $stack.credentialValuesStored "Credential values stored"
Assert-True $stack.readOnlyInterfaceConfirmed "Read-only interface"
Assert-False $stack.orderTradingReplayMethodsAvailable "Order/trading/replay methods"
Assert-False $stack.apiWorkerStartupRequired "API/Worker startup"
Assert-True $stack.liveLauncherRequired "Live launcher required"
Assert-False $stack.liveLauncherCreated "Live launcher created"
Assert-False $stack.validationPassed "Final stack validation"
Assert-Equal $stack.resultClassification "LMAX_R31_FAIL_REQUIRES_LIVE_LAUNCHER" "Stack classification"
Assert-Equal $stack.concreteFinalAbortCause "RequiresLiveLauncherCreation" "Concrete abort cause"

Assert-False $activation.temporaryRuntimeActivationAttempted "Activation attempted"
Assert-False $activation.activationAttemptExecuted "Activation executed"
Assert-Equal $activation.attemptCount 0 "Attempt count"
Assert-Equal $activation.retryCount 0 "Retry count"
Assert-False $activation.batchMode "Batch mode"
Assert-False $activation.loopMode "Loop mode"
Assert-False $activation.externalRunExecuted "External run"
Assert-False $activation.runtimePoweredUp "Runtime powered"
Assert-True $activation.abortBeforeConnection "Abort before connection"

Assert-True $instrumentStatus.approvedInstrumentsOnly "Approved instruments only"
Assert-False $instrumentStatus.nonApprovedInstrumentTouched "Non-approved touched"
Assert-True $instrumentStatus.usdJpyCaveatPreserved "USDJPY caveat"

Assert-True $boundary.preflightPassed "Boundary preflight"
Assert-False $boundary.fullStackValidationPassed "Boundary stack validation"
Assert-True $boundary.providerCompletenessConfirmed "Boundary provider completeness"
Assert-True $boundary.credentialConfigAccessSafe "Credential/config safe"
Assert-True $boundary.credentialLabelsPresent "Credential labels present"
Assert-False $boundary.runtimeActivationAttempted "Boundary activation"
Assert-Equal $boundary.tcpBoundaryStatus "NotAttempted" "TCP boundary"
Assert-Equal $boundary.tlsBoundaryStatus "NotAttempted" "TLS boundary"
Assert-Equal $boundary.fixLogonSessionBoundaryStatus "NotAttempted" "FIX boundary"
Assert-Equal $boundary.marketDataRequestBoundaryStatus "NotAttempted" "MarketData boundary"
Assert-True $boundary.outputSanitized "Boundary output sanitized"

foreach ($property in @("ordersSubmitted", "orderPathEnabled", "orderGatewayRegistered", "tradingGatewayRegistered", "allowOrderSubmission", "allowLiveTrading", "isTradingEnabled", "schedulerStarted", "pollingStarted", "replayExecuted", "shadowReplaySubmitted", "tradingStateMutated", "productionAccountUsed", "defaultGatewayRegistrationChanged", "runtimeEnablementPersisted", "liveLauncherCreated", "hostedServiceAdded", "backgroundWorkerAdded", "apiWorkerStarted")) {
    Assert-False $forbidden.$property "Forbidden $property"
}
Assert-True $forbidden.passed "Forbidden validation"

Assert-True $shutdown.shutdownOrRevertCompleted "Shutdown/revert"
Assert-False $shutdown.activationAttempted "Shutdown activation"
Assert-False $shutdown.socketOpened "Shutdown socket"
Assert-False $shutdown.tcpConnectionAttempted "Shutdown TCP"
Assert-False $shutdown.tlsHandshakeAttempted "Shutdown TLS"
Assert-False $shutdown.fixLogonAttempted "Shutdown FIX"
Assert-False $shutdown.marketDataRequestSent "Shutdown MarketData"

Assert-Equal $nonMutation.attemptCount 0 "Non-mutation attempt count"
Assert-False $nonMutation.productionAccountUsed "Non-mutation production"
Assert-True $nonMutation.approvedInstrumentsOnly "Non-mutation approved instruments"
Assert-True $nonMutation.usdJpyCaveatPreserved "Non-mutation USDJPY"
foreach ($property in @("orderSubmissionExecuted", "orderPathEnabled", "orderGatewayRegistered", "tradingGatewayRegistered", "allowOrderSubmission", "allowLiveTrading", "isTradingEnabled", "schedulerStarted", "pollingStarted", "replayExecuted", "shadowReplaySubmitted", "tradingStateMutated", "defaultGatewayRegistrationChanged", "runtimeEnablementPersisted", "credentialsPrinted", "credentialsStored", "archivesModified", "liveLauncherCreated", "hostedServiceAdded", "backgroundWorkerAdded")) {
    Assert-False $nonMutation.$property "Non-mutation $property"
}
Assert-True $nonMutation.outputSanitized "Non-mutation sanitized"
Assert-True $nonMutation.shutdownOrRevertCompleted "Non-mutation shutdown"

Assert-False $isolation.validatedEvidenceArchivesModified "Evidence archives"
Assert-False $isolation.phase7AThrough7NArchivesModified "Phase 7 archives"
Assert-False $isolation.usdJpyT1ThroughT7ArtifactsModified "USDJPY artifacts"
Assert-True $isolation.usdJpyT7ClosureIntact "USDJPY T7 intact"
Assert-True $isolation.phase7NClosureIntact "Phase 7N intact"

$allowedDecisions = @(
    "LMAX_R31_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED",
    "LMAX_R31_FAIL_PREFLIGHT_ABORTED",
    "LMAX_R31_FAIL_UNSAFE_PROVIDER_STACK_REJECTED",
    "LMAX_R31_FAIL_CREDENTIAL_CONFIG_MISSING",
    "LMAX_R31_FAIL_CREDENTIAL_CONFIG_NOT_APPROVED",
    "LMAX_R31_FAIL_REQUIRES_API_WORKER_STARTUP",
    "LMAX_R31_FAIL_REQUIRES_LIVE_LAUNCHER",
    "LMAX_R31_FAIL_PROVIDER_COMPLETENESS_REGRESSION",
    "LMAX_R31_FAIL_TCP_RUNTIME_BOUNDARY",
    "LMAX_R31_FAIL_TLS_RUNTIME_BOUNDARY",
    "LMAX_R31_FAIL_FIX_LOGON_RUNTIME_BOUNDARY",
    "LMAX_R31_FAIL_MARKETDATA_RUNTIME_BOUNDARY",
    "LMAX_R31_FAIL_SESSION_OR_MARKETDATA_BOUNDARY",
    "LMAX_R31_FAIL_SAFETY_CONSTRAINT",
    "LMAX_R31_INCONCLUSIVE_SANITIZED_EVIDENCE"
)
Assert-In $gate.finalDecision $allowedDecisions "R31 decision classification"
Assert-Equal $gate.finalDecision "LMAX_R31_FAIL_REQUIRES_LIVE_LAUNCHER" "R31 final decision"
Assert-False $gate.forbiddenActionOccurred "Forbidden action occurred"
Assert-True $gate.shutdownOrRevertCompleted "Gate shutdown"

$programPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\Program.cs"
$workerPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Worker\Program.cs"
$settingsPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\appsettings.json"
$program = Get-Content -LiteralPath $programPath -Raw
$worker = Get-Content -LiteralPath $workerPath -Raw
$settings = Get-Content -LiteralPath $settingsPath -Raw
Assert-TextContains $program "FakeLmaxGateway" "API default gateway"
foreach ($pattern in @("phase-lmax-r31", "LmaxRealReadOnlyDependencyProviderFactory", "LmaxRealReadOnlyMarketDataFrameBoundaryProvider", "LmaxRealReadOnlyCredentialConfigBoundaryProvider")) {
    Assert-TextNotContainsPattern $program $pattern "API wiring must not include R31 path"
    Assert-TextNotContainsPattern $worker $pattern "Worker wiring must not include R31 path"
    Assert-TextNotContainsPattern $settings $pattern "Default config must not include R31 path"
}

$validation = [ordered]@{
    phase = "LMAX-R31"
    validator = "scripts/check-lmax-r31-temporary-readonly-runtime-gate.ps1"
    validationPassed = $true
    finalDecision = "LMAX_R31_FAIL_REQUIRES_LIVE_LAUNCHER"
    concreteFinalAbortCause = "RequiresLiveLauncherCreation"
    r30DecisionConfirmed = $true
    allRequiredArtifactsExist = $true
    preflightPassed = $true
    finalFullStackValidationPassed = $false
    temporaryActivationAttemptExecuted = $false
    attemptCount = 0
    providerCompletenessConfirmed = $true
    credentialConfigSourcePresent = $true
    credentialValuesReturned = $false
    credentialValuesPrinted = $false
    credentialValuesStored = $false
    externalRunExecuted = $false
    realSnapshotExecuted = $false
    replayExecuted = $false
    postEndpointInvoked = $false
    realSocketOpened = $false
    realTcpConnectionAttempted = $false
    realTlsHandshakeAttempted = $false
    realFixLogonAttempted = $false
    realMarketDataRequestSent = $false
    orderSubmissionExecuted = $false
    tradingStateMutated = $false
    schedulerStarted = $false
    pollingStarted = $false
    shadowReplaySubmitted = $false
    apiWorkerStarted = $false
    runtimePoweredUp = $false
    runtimeEnablementPersisted = $false
    defaultGatewayRegistrationChanged = $false
    liveConnectionScriptCreated = $false
    hostedServiceAdded = $false
    backgroundWorkerAdded = $false
    apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
    recommendedNextPhase = "Phase LMAX-R32 - Concrete Final Pre-Execution Blocker Fix"
}

$validationPath = Join-Path $readiness "phase-lmax-r31-gate-validation.json"
($validation | ConvertTo-Json -Depth 8) + [Environment]::NewLine | Set-Content -LiteralPath $validationPath -Encoding UTF8
Assert-NoSensitiveContent $validationPath

Write-Host "LMAX-R31 gate validation passed."
Write-Host "Wrote $validationPath"
