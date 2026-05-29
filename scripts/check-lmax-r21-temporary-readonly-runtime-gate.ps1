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
    "phase-lmax-r20-gate-validation.json"
)

$requiredR21 = @(
    "phase-lmax-r21-operator-approval-record.json",
    "phase-lmax-r21-preflight-gate.json",
    "phase-lmax-r21-full-real-stack-validation.json",
    "phase-lmax-r21-temporary-runtime-activation-record.json",
    "phase-lmax-r21-approved-instrument-status-record.json",
    "phase-lmax-r21-sanitized-runtime-boundary-evidence.json",
    "phase-lmax-r21-forbidden-action-validation.json",
    "phase-lmax-r21-shutdown-revert-record.json",
    "phase-lmax-r21-post-attempt-non-mutation-validation.json",
    "phase-lmax-r21-rail-isolation-validation.json",
    "phase-lmax-r21-decision-gate.json",
    "phase-lmax-r21-temporary-readonly-runtime-report.md",
    "phase-lmax-r21-operator-note.md"
)

Write-Host "LMAX-R21 Temporary Read-Only Runtime Gate Validator"
Write-Host "This validator verifies R21 artifacts and confirms no external action occurred unless explicitly recorded by an allowed classification."

Assert-RequiredArtifacts $readiness $requiredPrior
Assert-RequiredArtifacts $readiness $requiredR21

$t7GatePath = Join-Path $usdJpy "phase-usdjpy-t7-final-closure-gate.json"
$phase7nGatePath = Join-Path $RepoRoot "artifacts\readiness\phase7n-final-lmax-readonly-runtime-evidence-archive-closure-gate.json"
if (-not (Test-Path -LiteralPath $t7GatePath)) {
    throw "Missing USDJPY T7 closure gate: $t7GatePath"
}
if (-not (Test-Path -LiteralPath $phase7nGatePath)) {
    throw "Missing Phase 7N closure gate: $phase7nGatePath"
}
Assert-NoSensitiveContent $t7GatePath
Assert-NoSensitiveContent $phase7nGatePath

$r20Gate = Read-Json (Join-Path $readiness "phase-lmax-r20-decision-gate.json")
$approval = Read-Json (Join-Path $readiness "phase-lmax-r21-operator-approval-record.json")
$preflight = Read-Json (Join-Path $readiness "phase-lmax-r21-preflight-gate.json")
$stack = Read-Json (Join-Path $readiness "phase-lmax-r21-full-real-stack-validation.json")
$activation = Read-Json (Join-Path $readiness "phase-lmax-r21-temporary-runtime-activation-record.json")
$instrumentStatus = Read-Json (Join-Path $readiness "phase-lmax-r21-approved-instrument-status-record.json")
$boundary = Read-Json (Join-Path $readiness "phase-lmax-r21-sanitized-runtime-boundary-evidence.json")
$forbidden = Read-Json (Join-Path $readiness "phase-lmax-r21-forbidden-action-validation.json")
$shutdown = Read-Json (Join-Path $readiness "phase-lmax-r21-shutdown-revert-record.json")
$nonMutation = Read-Json (Join-Path $readiness "phase-lmax-r21-post-attempt-non-mutation-validation.json")
$isolation = Read-Json (Join-Path $readiness "phase-lmax-r21-rail-isolation-validation.json")
$gate = Read-Json (Join-Path $readiness "phase-lmax-r21-decision-gate.json")
$t7Gate = Read-Json $t7GatePath
$phase7nGate = Read-Json $phase7nGatePath

Assert-Equal $r20Gate.finalDecision "LMAX_R20_REAL_LOW_LEVEL_DEPENDENCIES_IMPLEMENTED_WITH_TEST_DOUBLES_NO_EXTERNAL_ACTIVATION" "R20 decision"
Assert-Equal $t7Gate.finalDecision "USDJPY_T7_FINAL_READINESS_ARCHIVE_CLOSED_WITH_CAVEAT" "USDJPY T7 decision"
Assert-Equal $phase7nGate.finalDecision "PASS_FINAL_LMAX_READONLY_RUNTIME_EVIDENCE_ARCHIVE_CLOSED" "Phase 7N decision"

$expectedPhrase = "I, Philippe, explicitly approve Phase LMAX-R21 for one temporary Demo read-only runtime market-data activation attempt using the full real dependency stack for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority."
Assert-Equal $approval.phase "LMAX-R21" "Approval phase"
Assert-Equal $approval.operator "Philippe" "Approval operator"
Assert-Equal $approval.approvalPhrase $expectedPhrase "Approval phrase"
Assert-True $approval.approvalPhraseExact "Approval exact"
Assert-Equal $approval.environment "Demo/read-only" "Approval environment"
foreach ($symbol in @("GBPUSD", "EURGBP", "AUDUSD", "USDJPY")) {
    if (@($approval.approvedInstruments | Where-Object { $_.symbol -eq $symbol }).Count -ne 1) {
        throw "Approval missing instrument: $symbol"
    }
    if (@($instrumentStatus.instruments | Where-Object { $_.symbol -eq $symbol }).Count -ne 1) {
        throw "Instrument status missing instrument: $symbol"
    }
}
$usdJpyInstrument = @($approval.approvedInstruments | Where-Object { $_.symbol -eq "USDJPY" })[0]
Assert-Equal $usdJpyInstrument.caveat "prior failed-safe root cause remains unproven" "USDJPY caveat"

Assert-True $preflight.r20DecisionConfirmed "Preflight R20"
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

foreach ($property in @("lmaxExecutableReadOnlyRealLowLevelDependenciesAvailable", "lmaxRealReadOnlyLowLevelDependencySetAvailable", "lmaxExecutableReadOnlyLowLevelSessionStackAvailable", "lmaxExecutableReadOnlySocketSessionBoundaryAvailable", "lmaxExecutableReadOnlyFixSessionBoundaryAvailable", "lmaxExecutableReadOnlyMarketDataRequestCodecAvailable", "lmaxExecutableReadOnlyCredentialBoundaryAvailable", "publicInterfacesReadOnlyOnly", "concreteAdapterStillDryRunOnly", "abortBeforeExternalAction")) {
    Assert-True $stack.$property "Stack $property"
}
foreach ($property in @("orderTradingReplayMethodsAvailable", "credentialAccessSafe", "credentialValuesPrinted", "credentialValuesPersisted", "apiWorkerStartupRequired", "defaultConfigMutationRequired", "hostedServiceRequired", "backgroundWorkerRequired", "liveLauncherCreated", "approvedPathBypassed", "oldManualWrappersUsedDirectly", "approvedExecutableRealTcpConnectorAvailable", "approvedExecutableRealTlsAuthenticatorAvailable", "approvedExecutableRealFixSessionDriverAvailable", "approvedExecutableRealMarketDataDriverAvailable", "approvedExecutableRealSecretProviderAvailable", "validationPassedForExecution")) {
    Assert-False $stack.$property "Stack $property"
}
Assert-Equal $stack.validationResult "FailedNoApprovedExecutableRealDependencyProviders" "Stack validation"
Assert-Equal $stack.resultClassification "LMAX_R21_FAIL_NO_EXECUTABLE_REAL_DEPENDENCY_STACK_AVAILABLE" "Stack classification"

Assert-True $activation.preflightPassed "Activation preflight"
Assert-False $activation.preflightAborted "Activation preflight aborted"
Assert-True $activation.fullRealStackValidationCompleted "Activation stack validation"
Assert-False $activation.fullRealStackValidationPassedForExecution "Activation stack passed"
Assert-False $activation.activationAttempted "Activation attempted"
Assert-False $activation.activationExecuted "Activation executed"
Assert-Equal $activation.attemptCount 0 "Activation attempt count"
Assert-Equal $activation.retryCount 0 "Activation retry count"
Assert-True $activation.concreteAdapterUsed "Activation concrete adapter"
Assert-False $activation.realTransportUsed "Activation real transport"
Assert-False $activation.executableSessionClientUsed "Activation session client"
Assert-False $activation.lowLevelStackUsed "Activation low-level stack"
Assert-False $activation.realLowLevelDependenciesUsed "Activation real dependencies"
Assert-Equal $activation.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Activation gateway mode"
foreach ($property in @("batchMode", "loopMode", "externalRunExecuted", "realSocketOpened", "realTcpConnectionAttempted", "realTlsHandshakeAttempted", "realFixLogonAttempted", "realMarketDataRequestSent", "temporaryReadOnlyMarketDataAdapterUsed", "runtimePoweredUp", "runtimeEnablementExecuted", "runtimeEnablementPersisted", "apiWorkerStarted", "defaultGatewayRegistrationChanged", "defaultConfigChanged", "liveLauncherCreated", "hostedServiceAdded", "backgroundWorkerAdded")) {
    Assert-False $activation.$property "Activation $property"
}

Assert-True $instrumentStatus.approvedInstrumentsOnly "Instrument approved only"
Assert-True $instrumentStatus.usdJpyCaveatPreserved "Instrument USDJPY"
Assert-False $instrumentStatus.nonApprovedInstrumentsTouched "Non-approved touched"
foreach ($instrument in $instrumentStatus.instruments) {
    Assert-True $instrument.approved "Instrument approved $($instrument.symbol)"
    Assert-False $instrument.attempted "Instrument attempted $($instrument.symbol)"
}

Assert-True $boundary.outputSanitized "Boundary sanitized"
Assert-False $boundary.activationAttempted "Boundary activation"
Assert-Equal $boundary.fullRealStackValidation "FailedNoApprovedExecutableRealDependencyProviders" "Boundary stack validation"
foreach ($boundaryName in @("tcpBoundaryStatus", "tlsBoundaryStatus", "fixLogonBoundaryStatus", "marketDataRequestBoundaryStatus", "instrumentMarketDataStatus")) {
    Assert-Equal $boundary.$boundaryName "NotAttemptedNoExecutableRealDependencyStack" "Boundary $boundaryName"
}
Assert-False $boundary.approvedFullRealStackUsed "Boundary full real stack used"
Assert-True $boundary.noExecutableRealDependencySetAvailable "Boundary no executable stack"
Assert-True $boundary.readOnlyInterfaceConfirmed "Boundary read-only"
Assert-True $boundary.concreteAdapterUsed "Boundary concrete adapter"
foreach ($property in @("realTransportUsed", "executableSessionClientUsed", "lowLevelStackUsed", "realLowLevelDependenciesUsed", "credentialsPrinted", "credentialsStored", "rawFixLogsStored")) {
    Assert-False $boundary.$property "Boundary $property"
}

foreach ($property in @("orderSubmissionExecuted", "orderPathEnabled", "orderGatewayRegistered", "tradingGatewayRegistered", "allowOrderSubmission", "allowLiveTrading", "isTradingEnabled", "schedulerStarted", "pollingStarted", "replayExecuted", "shadowReplaySubmitted", "tradingStateMutated", "productionAccountUsed", "liveTradingAccountUsed", "nonApprovedInstrumentSubscription", "defaultRuntimeEnablement", "persistentGatewayRegistrationChange", "apiWorkerDefaultGatewayChange", "liveLauncherCreated", "hostedServiceAdded", "backgroundWorkerAdded", "credentialsPrinted", "credentialsPersisted", "approvedStackBypassed", "forbiddenActionOccurred")) {
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

foreach ($property in @("phase7ArchivesModified", "usdJpyT1T7ArtifactsModified", "gbpusdArchiveModified", "eurgbpArchiveModified", "audusdArchiveModified", "r1ToR20ArtifactsModifiedExceptReadOnlyReference", "defaultConfigChanged", "runtimeGatewayRegisteredByDefault", "persistentRuntimeEnablement")) {
    Assert-False $isolation.$property "Isolation $property"
}
Assert-True $isolation.railIsolationPreserved "Rail isolation"
Assert-Equal $isolation.apiWorkerDefaultMode "FakeLmaxGatewayOnly" "Isolation gateway"

$allowedDecisions = @(
    "LMAX_R21_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED",
    "LMAX_R21_FAIL_PREFLIGHT_ABORTED",
    "LMAX_R21_FAIL_NO_EXECUTABLE_REAL_DEPENDENCY_STACK_AVAILABLE",
    "LMAX_R21_FAIL_UNSAFE_FULL_REAL_STACK_REJECTED",
    "LMAX_R21_FAIL_RUNTIME_ACTIVATION_BOUNDARY",
    "LMAX_R21_FAIL_SESSION_OR_MARKETDATA_BOUNDARY",
    "LMAX_R21_FAIL_SAFETY_CONSTRAINT",
    "LMAX_R21_INCONCLUSIVE_SANITIZED_EVIDENCE"
)
Assert-In $gate.finalDecision $allowedDecisions "R21 decision classification"
Assert-Equal $gate.finalDecision "LMAX_R21_FAIL_NO_EXECUTABLE_REAL_DEPENDENCY_STACK_AVAILABLE" "R21 final decision"
Assert-True $gate.preflightPassed "Gate preflight"
Assert-False $gate.activationAttempted "Gate activation attempted"
Assert-False $gate.activationExecuted "Gate activation executed"

$realDepsPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxExecutableReadOnlyRealLowLevelDependencies.cs"
$adapterPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter.cs"
if (-not (Test-Path -LiteralPath $realDepsPath)) {
    throw "Missing R20 real dependency code: $realDepsPath"
}
$realDeps = Get-Content -LiteralPath $realDepsPath -Raw
$adapter = Get-Content -LiteralPath $adapterPath -Raw
foreach ($needle in @("ILmaxRealReadOnlyTcpConnector", "ILmaxRealReadOnlyTlsAuthenticator", "ILmaxRealReadOnlyFixSessionDriver", "ILmaxRealReadOnlyMarketDataDriver", "ILmaxRealReadOnlySecretProvider", "LmaxRealReadOnlyLowLevelDependencyFactory")) {
    Assert-TextContains $realDeps $needle "R20 dependency code token"
}
Assert-TextContains $adapter "DryRunOnly" "Concrete adapter dry-run guard"
foreach ($pattern in @("TcpClient", "System\.Net\.Sockets", "new\s+Socket", "SslStream", "NetworkStream", "AddHostedService", "NewOrderSingle", "OrderCancelRequest", "OrderStatusRequest", "TradeCaptureReportRequest", "SubmitOrder")) {
    Assert-TextNotContainsPattern $realDeps $pattern "R20 real dependency code must not include live or forbidden token"
}

$programPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\Program.cs"
$settingsPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\appsettings.json"
$program = Get-Content -LiteralPath $programPath -Raw
$settings = Get-Content -LiteralPath $settingsPath -Raw
Assert-TextContains $program "FakeLmaxGateway" "API default gateway"
foreach ($pattern in @("LmaxRealReadOnlyLowLevelDependencyFactory", "LmaxRealReadOnlySocketDependency", "LmaxRealReadOnlyFixSessionDependency", "LmaxRealReadOnlyMarketDataDependency", "LmaxRealReadOnlyCredentialDependency", "phase-lmax-r21")) {
    Assert-TextNotContainsPattern $program $pattern "API/Worker wiring must not include R21 path"
    Assert-TextNotContainsPattern $settings $pattern "Default config must not include R21 path"
}

$validation = [ordered]@{
    phase = "LMAX-R21"
    validator = "scripts/check-lmax-r21-temporary-readonly-runtime-gate.ps1"
    validationPassed = $true
    finalDecision = "LMAX_R21_FAIL_NO_EXECUTABLE_REAL_DEPENDENCY_STACK_AVAILABLE"
    r20DecisionConfirmed = $true
    operatorApprovalExact = $true
    preflightPassed = $true
    preflightAborted = $false
    fullRealDependencyStackValidation = "FailedNoApprovedExecutableRealDependencyProviders"
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
    recommendedNextPhase = "Phase LMAX-R22 - Full Real Stack Execution Boundary Remediation"
}

$validationPath = Join-Path $readiness "phase-lmax-r21-gate-validation.json"
($validation | ConvertTo-Json -Depth 8) + [Environment]::NewLine | Set-Content -LiteralPath $validationPath -Encoding UTF8
Assert-NoSensitiveContent $validationPath

Write-Host "LMAX-R21 gate validation passed."
Write-Host "Wrote $validationPath"
