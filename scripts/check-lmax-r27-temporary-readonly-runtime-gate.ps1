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

function Assert-In($Actual, [object[]]$Allowed, [string]$Message) {
    if ($Allowed -notcontains $Actual) {
        throw "$Message. Expected one of '$($Allowed -join "', '")' but got '$Actual'."
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
        '(?i)credential\s*[:=]\s*[^,\s\}\]]+'
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
    "phase-lmax-r26-gate-validation.json"
)

$requiredR27 = @(
    "phase-lmax-r27-operator-approval-record.json",
    "phase-lmax-r27-preflight-gate.json",
    "phase-lmax-r27-final-provider-stack-validation.json",
    "phase-lmax-r27-temporary-runtime-activation-record.json",
    "phase-lmax-r27-approved-instrument-status-record.json",
    "phase-lmax-r27-sanitized-runtime-boundary-evidence.json",
    "phase-lmax-r27-forbidden-action-validation.json",
    "phase-lmax-r27-shutdown-revert-record.json",
    "phase-lmax-r27-post-attempt-non-mutation-validation.json",
    "phase-lmax-r27-rail-isolation-validation.json",
    "phase-lmax-r27-decision-gate.json",
    "phase-lmax-r27-temporary-readonly-runtime-report.md",
    "phase-lmax-r27-operator-note.md"
)

Write-Host "LMAX-R27 Temporary Read-Only Runtime Gate Validator"
Write-Host "This validator verifies R27 artifacts and confirms no forbidden action occurred."

Assert-RequiredArtifacts $readiness $requiredPrior
Assert-RequiredArtifacts $readiness $requiredR27

$t7GatePath = Join-Path $usdJpy "phase-usdjpy-t7-final-closure-gate.json"
$phase7nGatePath = Join-Path $RepoRoot "artifacts\readiness\phase7n-final-lmax-readonly-runtime-evidence-archive-closure-gate.json"
if (-not (Test-Path -LiteralPath $t7GatePath)) { throw "Missing USDJPY T7 closure gate: $t7GatePath" }
if (-not (Test-Path -LiteralPath $phase7nGatePath)) { throw "Missing Phase 7N closure gate: $phase7nGatePath" }
Assert-NoSensitiveContent $t7GatePath
Assert-NoSensitiveContent $phase7nGatePath

$r26Gate = Read-Json (Join-Path $readiness "phase-lmax-r26-decision-gate.json")
$approval = Read-Json (Join-Path $readiness "phase-lmax-r27-operator-approval-record.json")
$preflight = Read-Json (Join-Path $readiness "phase-lmax-r27-preflight-gate.json")
$stack = Read-Json (Join-Path $readiness "phase-lmax-r27-final-provider-stack-validation.json")
$activation = Read-Json (Join-Path $readiness "phase-lmax-r27-temporary-runtime-activation-record.json")
$instrumentStatus = Read-Json (Join-Path $readiness "phase-lmax-r27-approved-instrument-status-record.json")
$boundary = Read-Json (Join-Path $readiness "phase-lmax-r27-sanitized-runtime-boundary-evidence.json")
$forbidden = Read-Json (Join-Path $readiness "phase-lmax-r27-forbidden-action-validation.json")
$shutdown = Read-Json (Join-Path $readiness "phase-lmax-r27-shutdown-revert-record.json")
$nonMutation = Read-Json (Join-Path $readiness "phase-lmax-r27-post-attempt-non-mutation-validation.json")
$isolation = Read-Json (Join-Path $readiness "phase-lmax-r27-rail-isolation-validation.json")
$gate = Read-Json (Join-Path $readiness "phase-lmax-r27-decision-gate.json")
$t7Gate = Read-Json $t7GatePath
$phase7nGate = Read-Json $phase7nGatePath

Assert-Equal $r26Gate.finalDecision "LMAX_R26_TLS_PROVIDER_IMPLEMENTED_NO_EXTERNAL_ACTIVATION" "R26 decision"
Assert-Equal $t7Gate.finalDecision "USDJPY_T7_FINAL_READINESS_ARCHIVE_CLOSED_WITH_CAVEAT" "USDJPY T7 decision"
Assert-Equal $phase7nGate.finalDecision "PASS_FINAL_LMAX_READONLY_RUNTIME_EVIDENCE_ARCHIVE_CLOSED" "Phase 7N decision"

$expectedPhrase = "I, Philippe, explicitly approve Phase LMAX-R27 for one temporary Demo read-only runtime market-data activation attempt after the TLS provider fix for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority."
Assert-Equal $approval.phase "LMAX-R27" "Approval phase"
Assert-Equal $approval.operator "Philippe" "Approval operator"
Assert-Equal $approval.approvalPhrase $expectedPhrase "Approval phrase"
Assert-True $approval.approvalPhraseExact "Approval exact"
Assert-Equal $approval.environment "Demo/read-only" "Approval environment"
foreach ($symbol in @("GBPUSD", "EURGBP", "AUDUSD", "USDJPY")) {
    if (@($approval.approvedInstruments | Where-Object { $_.symbol -eq $symbol }).Count -ne 1) { throw "Approval missing instrument: $symbol" }
    if (@($instrumentStatus.instruments | Where-Object { $_.symbol -eq $symbol }).Count -ne 1) { throw "Instrument status missing instrument: $symbol" }
}
$usdJpyInstrument = @($approval.approvedInstruments | Where-Object { $_.symbol -eq "USDJPY" })[0]
Assert-Equal $usdJpyInstrument.caveat "prior failed-safe root cause remains unproven" "USDJPY caveat"

Assert-True $preflight.r26DecisionConfirmed "Preflight R26"
Assert-True $preflight.operatorApprovalExact "Preflight approval"
Assert-True $preflight.approvedInstrumentListExact "Preflight instruments"
Assert-True $preflight.usdJpyCaveatPresent "Preflight USDJPY"
Assert-True $preflight.environmentDemoReadOnly "Preflight demo"
Assert-True $preflight.outputSanitizationRequired "Preflight sanitization"
Assert-True $preflight.shutdownRevertPlanPresent "Preflight shutdown"
Assert-True $preflight.approvedPathConfirmed "Preflight path"
Assert-True $preflight.outputPathUnderReadinessRuntimeEnablement "Preflight output path"
Assert-True $preflight.preflightPassed "Preflight passed"
Assert-False $preflight.preflightAborted "Preflight aborted"
foreach ($property in @("productionAccount", "allowOrderSubmission", "allowLiveTrading", "isTradingEnabled", "scheduler", "polling", "replay", "shadowReplay", "tradingMutation", "persistentRuntimeEnablement", "defaultGatewayRegistrationChange", "nonApprovedInstrumentConfigured", "permanentRuntimeEnablementPlanned", "defaultGatewayConfigChangePlanned", "liveLauncherCreated", "hostedServiceAdded", "backgroundWorkerAdded", "connectionAttempted", "runtimeActivationAttempted")) {
    Assert-False $preflight.$property "Preflight $property"
}

foreach ($property in @("lmaxRealReadOnlySocketBoundaryProviderAvailable", "lmaxRealReadOnlyTlsBoundaryProviderAvailable", "ilmaxRealReadOnlySocketBoundaryProviderHasNonTestImplementation", "ilmaxRealReadOnlyTlsBoundaryProviderHasNonTestImplementation", "socketProviderFixRemainsValid", "tlsProviderFixResolvedR25Boundary", "lmaxExecutableReadOnlyRealDependencyProvidersAvailable", "lmaxExecutableReadOnlyRealLowLevelDependenciesAvailable", "lmaxExecutableReadOnlyLowLevelSessionStackAvailable", "lmaxExecutableReadOnlySocketSessionBoundaryAvailable", "lmaxExecutableReadOnlyFixSessionBoundaryAvailable", "lmaxExecutableReadOnlyMarketDataRequestCodecAvailable", "lmaxExecutableReadOnlyCredentialBoundaryAvailable", "publicInterfacesReadOnlyOnly", "approvedExecutableSocketBoundaryProviderAvailable", "approvedExecutableTlsBoundaryProviderAvailable", "testDoubleFixProviderOnly", "abortBeforeExternalAction")) {
    Assert-True $stack.$property "Stack $property"
}
foreach ($property in @("orderTradingReplayMethodsAvailable", "credentialAccessSafe", "credentialValuesPrinted", "credentialValuesPersisted", "apiWorkerStartupRequired", "defaultConfigMutationRequired", "hostedServiceRequired", "backgroundWorkerRequired", "liveLauncherCreated", "approvedPathBypassed", "oldManualWrappersUsedDirectly", "approvedExecutableFixFrameBoundaryProviderAvailable", "approvedExecutableMarketDataFrameBoundaryProviderAvailable", "approvedExecutableCredentialConfigBoundaryProviderAvailable", "validationPassedForExecution")) {
    Assert-False $stack.$property "Stack $property"
}
Assert-Equal $stack.concreteFinalAbortCause "FixProviderNotExecutable" "Concrete abort cause"
Assert-Equal $stack.validationResult "FailedFixProviderNotExecutable" "Stack validation"
Assert-Equal $stack.resultClassification "LMAX_R27_FAIL_FIX_PROVIDER_NOT_EXECUTABLE" "Stack classification"

Assert-True $activation.preflightPassed "Activation preflight"
Assert-False $activation.preflightAborted "Activation preflight aborted"
Assert-True $activation.finalProviderStackValidationCompleted "Activation stack validation"
Assert-False $activation.finalProviderStackValidationPassedForExecution "Activation stack passed"
Assert-False $activation.activationAttempted "Activation attempted"
Assert-False $activation.activationExecuted "Activation executed"
Assert-Equal $activation.attemptCount 0 "Activation attempt count"
Assert-Equal $activation.retryCount 0 "Activation retry count"
Assert-True $activation.concreteAdapterUsed "Activation concrete adapter"
foreach ($property in @("batchMode", "loopMode", "externalRunExecuted", "realSocketOpened", "realTcpConnectionAttempted", "realTlsHandshakeAttempted", "realFixLogonAttempted", "realMarketDataRequestSent", "temporaryReadOnlyMarketDataAdapterUsed", "realTransportUsed", "executableSessionClientUsed", "lowLevelStackUsed", "realLowLevelDependenciesUsed", "realDependencyProvidersUsed", "socketProviderUsed", "tlsProviderUsed", "runtimePoweredUp", "runtimeEnablementExecuted", "runtimeEnablementPersisted", "apiWorkerStarted", "defaultGatewayRegistrationChanged", "defaultConfigChanged", "liveLauncherCreated", "hostedServiceAdded", "backgroundWorkerAdded")) {
    Assert-False $activation.$property "Activation $property"
}
Assert-Equal $activation.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Activation gateway mode"

Assert-True $instrumentStatus.approvedInstrumentsOnly "Instrument approved only"
Assert-True $instrumentStatus.usdJpyCaveatPreserved "Instrument USDJPY"
Assert-False $instrumentStatus.nonApprovedInstrumentsTouched "Non-approved touched"
foreach ($instrument in $instrumentStatus.instruments) {
    Assert-True $instrument.approved "Instrument approved $($instrument.symbol)"
    Assert-False $instrument.attempted "Instrument attempted $($instrument.symbol)"
    Assert-Equal $instrument.status "NotAttemptedFixProviderNotExecutable" "Instrument status $($instrument.symbol)"
}

Assert-True $boundary.outputSanitized "Boundary sanitized"
Assert-False $boundary.activationAttempted "Boundary activation"
Assert-Equal $boundary.finalProviderStackValidation "FailedFixProviderNotExecutable" "Boundary stack validation"
Assert-Equal $boundary.concreteFinalAbortCause "FixProviderNotExecutable" "Boundary abort cause"
foreach ($boundaryName in @("tcpBoundaryStatus", "tlsBoundaryStatus", "fixLogonBoundaryStatus", "marketDataRequestBoundaryStatus", "instrumentMarketDataStatus")) {
    Assert-Equal $boundary.$boundaryName "NotAttemptedFixProviderNotExecutable" "Boundary $boundaryName"
}
Assert-False $boundary.approvedFinalProviderStackUsed "Boundary final stack used"
Assert-True $boundary.socketProviderFixedAvailable "Boundary socket fix"
Assert-True $boundary.tlsProviderFixedAvailable "Boundary TLS fix"
Assert-True $boundary.readOnlyInterfaceConfirmed "Boundary read-only"
Assert-True $boundary.concreteAdapterUsed "Boundary concrete adapter"
foreach ($property in @("realTransportUsed", "executableSessionClientUsed", "lowLevelStackUsed", "realLowLevelDependenciesUsed", "realDependencyProvidersUsed", "socketProviderUsed", "tlsProviderUsed", "credentialsPrinted", "credentialsStored", "rawFixLogsStored")) {
    Assert-False $boundary.$property "Boundary $property"
}

foreach ($property in @("orderSubmissionExecuted", "orderStatusRequestExecuted", "orderCancelRequestExecuted", "tradeCaptureRequestExecuted", "orderPathEnabled", "orderGatewayRegistered", "tradingGatewayRegistered", "allowOrderSubmission", "allowLiveTrading", "isTradingEnabled", "schedulerStarted", "pollingStarted", "replayExecuted", "shadowReplaySubmitted", "tradingStateMutated", "productionAccountUsed", "liveTradingAccountUsed", "nonApprovedInstrumentSubscription", "defaultRuntimeEnablement", "persistentGatewayRegistrationChange", "apiWorkerDefaultGatewayChange", "liveLauncherCreated", "hostedServiceAdded", "backgroundWorkerAdded", "credentialsPrinted", "credentialsPersisted", "approvedStackBypassed", "forbiddenActionOccurred")) {
    Assert-False $forbidden.$property "Forbidden $property"
}

Assert-False $shutdown.activationAttempted "Shutdown activation"
Assert-False $shutdown.shutdownOrRevertRequired "Shutdown required"
Assert-True $shutdown.shutdownOrRevertCompleted "Shutdown completed"
Assert-False $shutdown.runtimePoweredUp "Shutdown runtime powered"
Assert-False $shutdown.runtimeEnablementPersisted "Shutdown runtime persisted"
Assert-Equal $shutdown.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Shutdown gateway"

Assert-Equal $nonMutation.attemptCount 0 "Non-mutation attempt count"
Assert-Equal $nonMutation.retryCount 0 "Non-mutation retry count"
foreach ($property in @("batchMode", "loopMode", "productionAccountUsed", "orderSubmissionExecuted", "orderPathEnabled", "orderGatewayRegistered", "tradingGatewayRegistered", "allowOrderSubmission", "allowLiveTrading", "isTradingEnabled", "schedulerStarted", "pollingStarted", "replayExecuted", "shadowReplaySubmitted", "tradingStateMutated", "defaultGatewayRegistrationChanged", "runtimeEnablementPersisted", "credentialsPrinted", "credentialsStored", "archivesModified", "liveLauncherCreated", "hostedServiceAdded", "backgroundWorkerAdded")) {
    Assert-False $nonMutation.$property "Non-mutation $property"
}
Assert-True $nonMutation.approvedInstrumentsOnly "Non-mutation approved instruments"
Assert-True $nonMutation.usdJpyCaveatPreserved "Non-mutation USDJPY"
Assert-True $nonMutation.outputSanitized "Non-mutation sanitized"
Assert-True $nonMutation.shutdownOrRevertCompleted "Non-mutation shutdown"

foreach ($property in @("phase7ArchivesModified", "usdJpyT1T7ArtifactsModified", "gbpusdArchiveModified", "eurgbpArchiveModified", "audusdArchiveModified", "r1ToR26ArtifactsModifiedExceptReadOnlyReference", "defaultConfigChanged", "runtimeGatewayRegisteredByDefault", "persistentRuntimeEnablement")) {
    Assert-False $isolation.$property "Isolation $property"
}
Assert-True $isolation.railIsolationPreserved "Rail isolation"
Assert-Equal $isolation.apiWorkerDefaultMode "FakeLmaxGatewayOnly" "Isolation gateway"

$allowedDecisions = @(
    "LMAX_R27_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED",
    "LMAX_R27_FAIL_PREFLIGHT_ABORTED",
    "LMAX_R27_FAIL_UNSAFE_PROVIDER_STACK_REJECTED",
    "LMAX_R27_FAIL_SOCKET_PROVIDER_NOT_EXECUTABLE",
    "LMAX_R27_FAIL_TLS_PROVIDER_NOT_EXECUTABLE",
    "LMAX_R27_FAIL_FIX_PROVIDER_NOT_EXECUTABLE",
    "LMAX_R27_FAIL_MARKETDATA_PROVIDER_NOT_EXECUTABLE",
    "LMAX_R27_FAIL_CREDENTIAL_PROVIDER_NOT_APPROVED",
    "LMAX_R27_FAIL_DEMO_CREDENTIAL_CONFIG_MISSING",
    "LMAX_R27_FAIL_REQUIRES_API_WORKER_STARTUP",
    "LMAX_R27_FAIL_REQUIRES_LIVE_LAUNCHER",
    "LMAX_R27_FAIL_RUNTIME_ACTIVATION_BOUNDARY",
    "LMAX_R27_FAIL_SESSION_OR_MARKETDATA_BOUNDARY",
    "LMAX_R27_FAIL_SAFETY_CONSTRAINT",
    "LMAX_R27_INCONCLUSIVE_SANITIZED_EVIDENCE"
)
Assert-In $gate.finalDecision $allowedDecisions "R27 decision classification"
Assert-Equal $gate.finalDecision "LMAX_R27_FAIL_FIX_PROVIDER_NOT_EXECUTABLE" "R27 final decision"
Assert-Equal $gate.concreteFinalAbortCause "FixProviderNotExecutable" "Gate abort cause"
Assert-True $gate.preflightPassed "Gate preflight"
Assert-False $gate.activationAttempted "Gate activation attempted"
Assert-False $gate.activationExecuted "Gate activation executed"
Assert-False $gate.r28Authorized "R28 authorization"

$providerPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxExecutableReadOnlyRealDependencyProviders.cs"
$socketProviderPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxRealReadOnlySocketBoundaryProvider.cs"
$tlsProviderPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxRealReadOnlyTlsBoundaryProvider.cs"
foreach ($path in @($providerPath, $socketProviderPath, $tlsProviderPath)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing provider code: $path" }
}
$providerCode = Get-Content -LiteralPath $providerPath -Raw
$socketProviderCode = Get-Content -LiteralPath $socketProviderPath -Raw
$tlsProviderCode = Get-Content -LiteralPath $tlsProviderPath -Raw
foreach ($needle in @("ILmaxRealReadOnlyFixFrameBoundaryProvider", "LmaxRealFixFrameProvider", "LmaxRealReadOnlyDependencyProviderFactory")) {
    Assert-TextContains $providerCode $needle "R22 provider code token"
}
Assert-TextContains $socketProviderCode "class LmaxRealReadOnlySocketBoundaryProvider : ILmaxRealReadOnlySocketBoundaryProvider" "R24 socket implementation"
Assert-TextContains $tlsProviderCode "class LmaxRealReadOnlyTlsBoundaryProvider : ILmaxRealReadOnlyTlsBoundaryProvider" "R26 TLS implementation"

$socketImplementations = Select-String -Path (Join-Path $RepoRoot "src\QQ.Production.Intraday.Infrastructure.Lmax\*.cs") -Pattern "class\s+LmaxRealReadOnlySocketBoundaryProvider\s*:\s*ILmaxRealReadOnlySocketBoundaryProvider"
if (@($socketImplementations).Count -ne 1) { throw "Expected exactly one non-test socket provider implementation." }
$tlsImplementations = Select-String -Path (Join-Path $RepoRoot "src\QQ.Production.Intraday.Infrastructure.Lmax\*.cs") -Pattern "class\s+LmaxRealReadOnlyTlsBoundaryProvider\s*:\s*ILmaxRealReadOnlyTlsBoundaryProvider"
if (@($tlsImplementations).Count -ne 1) { throw "Expected exactly one non-test TLS provider implementation." }
$fixImplementations = Select-String -Path (Join-Path $RepoRoot "src\QQ.Production.Intraday.Infrastructure.Lmax\*.cs") -Pattern "class\s+\w+\s*:\s*ILmaxRealReadOnlyFixFrameBoundaryProvider"
if (@($fixImplementations).Count -ne 0) { throw "R27 expected no approved production ILmaxRealReadOnlyFixFrameBoundaryProvider implementation, but found one." }

$programPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\Program.cs"
$workerPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Worker\Program.cs"
$settingsPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\appsettings.json"
$program = Get-Content -LiteralPath $programPath -Raw
$worker = Get-Content -LiteralPath $workerPath -Raw
$settings = Get-Content -LiteralPath $settingsPath -Raw
Assert-TextContains $program "FakeLmaxGateway" "API default gateway"
foreach ($pattern in @("LmaxRealReadOnlyDependencyProviderFactory", "LmaxRealReadOnlySocketBoundaryProvider", "LmaxRealReadOnlyTlsBoundaryProvider", "LmaxRealFixFrameProvider", "phase-lmax-r27")) {
    Assert-TextNotContainsPattern $program $pattern "API wiring must not include R27 path"
    Assert-TextNotContainsPattern $worker $pattern "Worker wiring must not include R27 path"
    Assert-TextNotContainsPattern $settings $pattern "Default config must not include R27 path"
}

$validation = [ordered]@{
    phase = "LMAX-R27"
    validator = "scripts/check-lmax-r27-temporary-readonly-runtime-gate.ps1"
    validationPassed = $true
    finalDecision = "LMAX_R27_FAIL_FIX_PROVIDER_NOT_EXECUTABLE"
    r26DecisionConfirmed = $true
    operatorApprovalExact = $true
    preflightPassed = $true
    preflightAborted = $false
    finalProviderStackValidation = "FailedFixProviderNotExecutable"
    concreteFinalAbortCause = "FixProviderNotExecutable"
    socketProviderFixRemainsValid = $true
    tlsProviderFixResolvedR25Boundary = $true
    activationAttempted = $false
    activationExecuted = $false
    attemptCount = 0
    retryCount = 0
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
    runtimeEnablementExecuted = $false
    runtimeEnablementPersisted = $false
    tradingEnablementExecuted = $false
    schedulerEnablementExecuted = $false
    orderPathEnablementExecuted = $false
    defaultGatewayRegistrationChanged = $false
    liveConnectionScriptCreated = $false
    hostedServiceAdded = $false
    backgroundWorkerAdded = $false
    productionAccountUsed = $false
    approvedInstrumentsOnly = $true
    usdJpyCaveatPreserved = $true
    outputSanitized = $true
    archivesModified = $false
    shutdownOrRevertCompleted = $true
    apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
    temporaryReadOnlyMarketDataAdapterUsed = $false
    concreteAdapterUsed = $true
    realTransportUsed = $false
    executableSessionClientUsed = $false
    lowLevelStackUsed = $false
    realLowLevelDependenciesUsed = $false
    realDependencyProvidersUsed = $false
    socketProviderUsed = $false
    tlsProviderUsed = $false
    recommendedNextPhase = "Phase LMAX-R28 - Concrete Final Execution Boundary Fix"
}

$validationPath = Join-Path $readiness "phase-lmax-r27-gate-validation.json"
($validation | ConvertTo-Json -Depth 8) + [Environment]::NewLine | Set-Content -LiteralPath $validationPath -Encoding UTF8
Assert-NoSensitiveContent $validationPath

Write-Host "LMAX-R27 gate validation passed."
Write-Host "Wrote $validationPath"
