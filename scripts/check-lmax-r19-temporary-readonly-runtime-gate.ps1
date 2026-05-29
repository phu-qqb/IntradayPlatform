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
    "phase-lmax-r18-gate-validation.json"
)

$requiredR19 = @(
    "phase-lmax-r19-operator-approval-record.json",
    "phase-lmax-r19-preflight-gate.json",
    "phase-lmax-r19-full-stack-validation.json",
    "phase-lmax-r19-temporary-runtime-activation-record.json",
    "phase-lmax-r19-approved-instrument-status-record.json",
    "phase-lmax-r19-sanitized-runtime-boundary-evidence.json",
    "phase-lmax-r19-forbidden-action-validation.json",
    "phase-lmax-r19-shutdown-revert-record.json",
    "phase-lmax-r19-post-attempt-non-mutation-validation.json",
    "phase-lmax-r19-rail-isolation-validation.json",
    "phase-lmax-r19-decision-gate.json",
    "phase-lmax-r19-temporary-readonly-runtime-report.md",
    "phase-lmax-r19-operator-note.md"
)

Write-Host "LMAX-R19 Temporary Read-Only Runtime Gate Validator"
Write-Host "This validator performs no external run, real snapshot, replay, POST endpoint, socket, runtime activation, API/Worker startup, credential loading, or config mutation."

Assert-RequiredArtifacts $readiness $requiredPrior
Assert-RequiredArtifacts $readiness $requiredR19

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

$r18Gate = Read-Json (Join-Path $readiness "phase-lmax-r18-decision-gate.json")
$approval = Read-Json (Join-Path $readiness "phase-lmax-r19-operator-approval-record.json")
$preflight = Read-Json (Join-Path $readiness "phase-lmax-r19-preflight-gate.json")
$fullStack = Read-Json (Join-Path $readiness "phase-lmax-r19-full-stack-validation.json")
$activation = Read-Json (Join-Path $readiness "phase-lmax-r19-temporary-runtime-activation-record.json")
$instrumentStatus = Read-Json (Join-Path $readiness "phase-lmax-r19-approved-instrument-status-record.json")
$boundary = Read-Json (Join-Path $readiness "phase-lmax-r19-sanitized-runtime-boundary-evidence.json")
$forbidden = Read-Json (Join-Path $readiness "phase-lmax-r19-forbidden-action-validation.json")
$shutdown = Read-Json (Join-Path $readiness "phase-lmax-r19-shutdown-revert-record.json")
$nonMutation = Read-Json (Join-Path $readiness "phase-lmax-r19-post-attempt-non-mutation-validation.json")
$isolation = Read-Json (Join-Path $readiness "phase-lmax-r19-rail-isolation-validation.json")
$gate = Read-Json (Join-Path $readiness "phase-lmax-r19-decision-gate.json")
$t7Gate = Read-Json $t7GatePath
$phase7nGate = Read-Json $phase7nGatePath

Assert-Equal $r18Gate.finalDecision "LMAX_R18_EXECUTABLE_LOW_LEVEL_SESSION_STACK_IMPLEMENTED_WITH_TEST_DOUBLES_NO_EXTERNAL_ACTIVATION" "R18 decision"
Assert-Equal $t7Gate.finalDecision "USDJPY_T7_FINAL_READINESS_ARCHIVE_CLOSED_WITH_CAVEAT" "USDJPY T7 decision"
Assert-Equal $phase7nGate.finalDecision "PASS_FINAL_LMAX_READONLY_RUNTIME_EVIDENCE_ARCHIVE_CLOSED" "Phase 7N decision"

$expectedPhrase = "I, Philippe, explicitly approve Phase LMAX-R19 for one temporary Demo read-only runtime market-data activation attempt using the full executable stack for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority."
Assert-Equal $approval.phase "LMAX-R19" "Approval phase"
Assert-Equal $approval.operator "Philippe" "Approval operator"
Assert-Equal $approval.approvalPhrase $expectedPhrase "Approval phrase"
Assert-True $approval.approvalPhraseExact "Approval exact"
Assert-Equal $approval.environment "Demo/read-only" "Approval environment"

foreach ($symbol in @("GBPUSD", "EURGBP", "AUDUSD", "USDJPY")) {
    if (-not (@($approval.approvedInstruments | Where-Object { $_.symbol -eq $symbol }).Count -eq 1)) {
        throw "Approval missing instrument: $symbol"
    }
    if (-not (@($instrumentStatus.instruments | Where-Object { $_.symbol -eq $symbol }).Count -eq 1)) {
        throw "Instrument status missing instrument: $symbol"
    }
}
$usdJpyInstrument = @($approval.approvedInstruments | Where-Object { $_.symbol -eq "USDJPY" })[0]
Assert-Equal $usdJpyInstrument.caveat "prior failed-safe root cause remains unproven" "Approval USDJPY caveat"

Assert-True $preflight.r18DecisionConfirmed "Preflight R18"
Assert-True $preflight.operatorApprovalExact "Preflight approval"
Assert-True $preflight.approvedInstrumentListExact "Preflight instruments"
Assert-True $preflight.usdJpyCaveatPresent "Preflight USDJPY caveat"
Assert-True $preflight.environmentDemoReadOnly "Preflight demo"
Assert-True $preflight.outputSanitizationRequired "Preflight sanitization"
Assert-True $preflight.shutdownRevertPlanPresent "Preflight shutdown"
Assert-True $preflight.approvedPathConfirmed "Preflight approved path"
Assert-True $preflight.outputPathUnderReadinessRuntimeEnablement "Preflight output path"
Assert-True $preflight.preflightPassed "Preflight passed"
Assert-False $preflight.preflightAborted "Preflight aborted"
foreach ($property in @("productionAccount", "allowOrderSubmission", "allowLiveTrading", "isTradingEnabled", "scheduler", "polling", "replay", "shadowReplay", "tradingMutation", "persistentRuntimeEnablement", "defaultGatewayRegistrationChange", "nonApprovedInstrumentConfigured", "permanentRuntimeEnablementPlanned", "defaultGatewayConfigChangePlanned", "liveLauncherCreated", "hostedServiceAdded", "backgroundWorkerAdded", "connectionAttempted", "runtimeActivationAttempted")) {
    Assert-False $preflight.$property "Preflight $property"
}

Assert-True $fullStack.approvedFullStackStructurallyAvailable "Full stack structural"
Assert-True $fullStack.r7HarnessAvailable "Full stack R7"
Assert-True $fullStack.r9AdapterPathAvailable "Full stack R9"
Assert-True $fullStack.r12ConcreteAdapterAvailable "Full stack R12"
Assert-True $fullStack.r14RealTransportAvailable "Full stack R14"
Assert-True $fullStack.r16ExecutableSessionClientAvailable "Full stack R16"
Assert-True $fullStack.r18LowLevelStackAvailable "Full stack R18"
Assert-True $fullStack.publicInterfacesReadOnlyOnly "Full stack read-only"
Assert-False $fullStack.orderTradingReplayMethodsAvailable "Full stack forbidden methods"
foreach ($property in @("approvedExecutableRealSocketBoundaryTransportAvailable", "approvedExecutableRealFixFrameBoundaryAvailable", "approvedExecutableRealMarketDataFrameCodecAvailable", "approvedExecutableCredentialProviderAvailable", "credentialAccessSafe", "credentialValuesPrinted", "credentialValuesPersisted", "apiWorkerStartupRequired", "defaultConfigMutationRequired", "hostedServiceRequired", "backgroundWorkerRequired", "liveLauncherCreated", "oldManualWrappersUsedDirectly", "approvedPathBypassed", "validationPassedForExecution")) {
    Assert-False $fullStack.$property "Full stack $property"
}
Assert-Equal $fullStack.validationResult "FailedNoApprovedExecutableRealLowLevelDependencies" "Full stack validation"
Assert-True $fullStack.abortBeforeExternalAction "Full stack abort"
Assert-Equal $fullStack.resultClassification "LMAX_R19_FAIL_NO_EXECUTABLE_LOW_LEVEL_STACK_AVAILABLE" "Full stack classification"

Assert-True $activation.preflightPassed "Activation preflight"
Assert-False $activation.preflightAborted "Activation preflight aborted"
Assert-True $activation.fullStackValidationCompleted "Activation full stack validation"
Assert-False $activation.fullStackValidationPassedForExecution "Activation full stack passed"
Assert-False $activation.activationAttempted "Activation attempted"
Assert-False $activation.activationExecuted "Activation executed"
Assert-Equal $activation.attemptCount 0 "Activation attempt count"
Assert-Equal $activation.retryCount 0 "Activation retry count"
Assert-True $activation.concreteAdapterUsed "Activation concrete adapter"
Assert-False $activation.realTransportUsed "Activation real transport"
Assert-False $activation.executableSessionClientUsed "Activation executable session"
Assert-False $activation.lowLevelStackUsed "Activation low-level stack"
Assert-Equal $activation.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Activation gateway"
foreach ($property in @("batchMode", "loopMode", "externalRunExecuted", "realSocketOpened", "realTcpConnectionAttempted", "realTlsHandshakeAttempted", "realFixLogonAttempted", "realMarketDataRequestSent", "temporaryReadOnlyMarketDataAdapterUsed", "runtimePoweredUp", "runtimeEnablementExecuted", "runtimeEnablementPersisted", "apiWorkerStarted", "defaultGatewayRegistrationChanged", "defaultConfigChanged", "liveLauncherCreated", "hostedServiceAdded", "backgroundWorkerAdded")) {
    Assert-False $activation.$property "Activation $property"
}

Assert-True $instrumentStatus.approvedInstrumentsOnly "Instrument approved only"
Assert-True $instrumentStatus.usdJpyCaveatPreserved "Instrument USDJPY caveat"
foreach ($instrument in $instrumentStatus.instruments) {
    Assert-True $instrument.approved "Instrument approved $($instrument.symbol)"
    Assert-False $instrument.attempted "Instrument attempted $($instrument.symbol)"
}

foreach ($boundaryName in @("tcpBoundaryStatus", "tlsBoundaryStatus", "fixLogonBoundaryStatus", "marketDataRequestBoundaryStatus", "instrumentMarketDataStatus")) {
    Assert-Equal $boundary.$boundaryName "NotAttemptedNoExecutableLowLevelStack" "Boundary $boundaryName"
}
Assert-Equal $boundary.fullStackValidation "FailedNoApprovedExecutableRealLowLevelDependencies" "Boundary full stack validation"
Assert-False $boundary.activationAttempted "Boundary activation"
Assert-Equal $boundary.marketDataRejectCount 0 "Boundary market-data rejects"
Assert-Equal $boundary.businessMessageRejectCount 0 "Boundary business rejects"
Assert-Equal $boundary.sessionRejectCount 0 "Boundary session rejects"
Assert-False $boundary.sensitiveInformationDetected "Boundary sensitive"
Assert-True $boundary.outputSanitized "Boundary sanitized"

Assert-True $forbidden.passed "Forbidden validation"
foreach ($property in @("orderSubmissionExecuted", "orderStatusRequestSent", "orderCancelRequestSent", "tradeCaptureRequestSent", "orderPathEnabled", "orderGatewayRegistered", "tradingGatewayRegistered", "allowOrderSubmission", "allowLiveTrading", "isTradingEnabled", "schedulerStarted", "pollingStarted", "replayExecuted", "shadowReplaySubmitted", "tradingStateMutated", "productionAccountUsed", "nonApprovedInstrumentTouched", "defaultGatewayRegistrationChanged", "runtimeEnablementPersisted", "liveLauncherCreated", "hostedServiceAdded", "backgroundWorkerAdded", "retryExecuted", "batchExecuted", "loopExecuted", "approvedPathBypassed")) {
    Assert-False $forbidden.$property "Forbidden $property"
}

Assert-False $shutdown.activationAttempted "Shutdown activation"
Assert-True $shutdown.shutdownOrRevertCompleted "Shutdown completed"
Assert-True $shutdown.containmentCompleted "Containment"
Assert-True $shutdown.defaultConfigUnchanged "Shutdown config"
Assert-True $shutdown.defaultGatewayRegistrationUnchanged "Shutdown gateway"
Assert-False $shutdown.runtimeEnablementPersisted "Shutdown persisted"
Assert-Equal $shutdown.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Shutdown gateway mode"
Assert-False $shutdown.credentialsPrinted "Shutdown printed"
Assert-False $shutdown.credentialsStored "Shutdown stored"
Assert-False $shutdown.rawFixLogsStored "Shutdown raw FIX"

Assert-Equal $nonMutation.attemptCount 0 "Non-mutation attempt"
Assert-Equal $nonMutation.retryCount 0 "Non-mutation retry"
Assert-True $nonMutation.approvedInstrumentsOnly "Non-mutation instruments"
Assert-True $nonMutation.usdJpyCaveatPreserved "Non-mutation caveat"
Assert-True $nonMutation.outputSanitized "Non-mutation sanitized"
Assert-True $nonMutation.shutdownOrRevertCompleted "Non-mutation shutdown"
Assert-Equal $nonMutation.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Non-mutation gateway"
foreach ($property in @("batchMode", "loopMode", "productionAccountUsed", "orderSubmissionExecuted", "orderPathEnabled", "orderGatewayRegistered", "tradingGatewayRegistered", "allowOrderSubmission", "allowLiveTrading", "isTradingEnabled", "schedulerStarted", "pollingStarted", "replayExecuted", "shadowReplaySubmitted", "tradingStateMutated", "defaultGatewayRegistrationChanged", "runtimeEnablementPersisted", "credentialsPrinted", "credentialsStored", "archivesModified", "liveLauncherCreated", "hostedServiceAdded", "backgroundWorkerAdded")) {
    Assert-False $nonMutation.$property "Non-mutation $property"
}

Assert-True $isolation.passed "Isolation passed"
Assert-True $isolation.usdJpyCaveatPreserved "Isolation USDJPY caveat"
foreach ($property in @("evidenceArchivesModified", "validatedRailsModified", "phase7ArchiveModified", "usdJpyT1T7ArtifactsModified", "r1ArtifactsModified", "r2ArtifactsModified", "r3ArtifactsModified", "r4ArtifactsModified", "r5ArtifactsModified", "r6ArtifactsModified", "r7ArtifactsModified", "r8ArtifactsModified", "r9ArtifactsModified", "r10ArtifactsModified", "r11ArtifactsModified", "r12ArtifactsModified", "r13ArtifactsModified", "r14ArtifactsModified", "r15ArtifactsModified", "r16ArtifactsModified", "r17ArtifactsModified", "r18ArtifactsModified", "gbpusdArchiveModified", "eurgbpArchiveModified", "audusdArchiveModified", "nonApprovedInstrumentTouched")) {
    Assert-False $isolation.$property "Isolation $property"
}

$allowedDecisions = @(
    "LMAX_R19_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED",
    "LMAX_R19_FAIL_PREFLIGHT_ABORTED",
    "LMAX_R19_FAIL_NO_EXECUTABLE_LOW_LEVEL_STACK_AVAILABLE",
    "LMAX_R19_FAIL_UNSAFE_FULL_STACK_REJECTED",
    "LMAX_R19_FAIL_RUNTIME_ACTIVATION_BOUNDARY",
    "LMAX_R19_FAIL_SESSION_OR_MARKETDATA_BOUNDARY",
    "LMAX_R19_FAIL_SAFETY_CONSTRAINT",
    "LMAX_R19_INCONCLUSIVE_SANITIZED_EVIDENCE"
)
Assert-In $gate.finalDecision $allowedDecisions "R19 final decision"
Assert-Equal $gate.finalDecision "LMAX_R19_FAIL_NO_EXECUTABLE_LOW_LEVEL_STACK_AVAILABLE" "R19 expected decision"
Assert-Equal $gate.resultClassification "LMAX_R19_FAIL_NO_EXECUTABLE_LOW_LEVEL_STACK_AVAILABLE" "R19 classification"
Assert-True $gate.operatorApprovalExact "Gate approval"
Assert-True $gate.preflightPassed "Gate preflight"
Assert-False $gate.preflightAborted "Gate preflight aborted"
Assert-True $gate.fullStackValidationCompleted "Gate full stack validation"
Assert-False $gate.fullStackValidationPassedForExecution "Gate full stack execution"
Assert-Equal $gate.fullStackValidation "FailedNoApprovedExecutableRealLowLevelDependencies" "Gate full stack validation value"
Assert-False $gate.activationAttemptExecuted "Gate activation"
Assert-Equal $gate.attemptCount 0 "Gate attempt"
Assert-Equal $gate.retryCount 0 "Gate retry"
Assert-True $gate.approvedInstrumentsOnly "Gate approved instruments"
Assert-True $gate.usdJpyCaveatPreserved "Gate caveat"
Assert-True $gate.concreteAdapterUsed "Gate concrete adapter"
Assert-False $gate.realTransportUsed "Gate real transport used"
Assert-False $gate.executableSessionClientUsed "Gate executable session used"
Assert-False $gate.lowLevelStackUsed "Gate low-level stack used"
Assert-True $gate.outputSanitized "Gate sanitized"
Assert-True $gate.shutdownOrRevertCompleted "Gate shutdown"
Assert-Equal $gate.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Gate gateway"
foreach ($property in @("batchMode", "loopMode", "productionAccountUsed", "externalRunExecuted", "realSnapshotExecuted", "replayExecuted", "postEndpointInvoked", "realSocketOpened", "realTcpConnectionAttempted", "realTlsHandshakeAttempted", "realFixLogonAttempted", "realMarketDataRequestSent", "orderSubmissionExecuted", "tradingStateMutated", "schedulerStarted", "pollingStarted", "shadowReplaySubmitted", "apiWorkerStarted", "runtimePoweredUp", "retryExecuted", "batchExecuted", "loopExecuted", "runtimeEnablementExecuted", "runtimeEnablementPersisted", "tradingEnablementExecuted", "schedulerEnablementExecuted", "orderPathEnablementExecuted", "defaultGatewayRegistrationChanged", "liveConnectionScriptCreated", "liveLauncherCreated", "hostedServiceAdded", "backgroundWorkerAdded", "temporaryReadOnlyMarketDataAdapterUsed", "orderGatewayRegistered", "tradingGatewayRegistered", "credentialsPrinted", "credentialsStored", "archivesModified")) {
    Assert-False $gate.$property "Gate $property"
}

$stackPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxExecutableReadOnlyLowLevelSessionStack.cs"
if (-not (Test-Path -LiteralPath $stackPath)) {
    throw "Missing R18 low-level stack: $stackPath"
}
$stackSource = Get-Content -LiteralPath $stackPath -Raw
foreach ($requiredToken in @("LmaxExecutableReadOnlySocketSessionBoundary", "LmaxExecutableReadOnlyFixSessionBoundary", "LmaxExecutableReadOnlyMarketDataRequestCodec", "LmaxExecutableReadOnlyCredentialBoundary", "LmaxExecutableReadOnlySessionStackFactory")) {
    if ($stackSource -notmatch [regex]::Escape($requiredToken)) {
        throw "Full stack source missing token: $requiredToken"
    }
}
foreach ($forbiddenToken in @("TcpClient", "System.Net.Sockets", "new Socket", "SslStream", "NetworkStream", "NewOrderSingle", "OrderCancelRequest", "OrderStatusRequest", "TradeCaptureReportRequest", "SubmitOrder")) {
    if ($stackSource -match [regex]::Escape($forbiddenToken)) {
        throw "Low-level stack source references forbidden token: $forbiddenToken"
    }
}

$programPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\Program.cs"
$workerPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Worker\Program.cs"
$appsettingsPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\appsettings.json"
$programText = Get-Content -LiteralPath $programPath -Raw
$workerText = Get-Content -LiteralPath $workerPath -Raw
$appsettings = Read-Json $appsettingsPath
if ($programText -notmatch 'AddSingleton<IVenueExecutionGateway,\s*FakeLmaxGateway>') {
    throw "API Program.cs no longer registers FakeLmaxGateway."
}
if ($workerText -notmatch 'AddSingleton<IVenueExecutionGateway,\s*FakeLmaxGateway>') {
    throw "Worker Program.cs no longer registers FakeLmaxGateway."
}
if ($programText -match 'LmaxExecutableReadOnlySessionStackFactory|LmaxExecutableReadOnlySocketSessionBoundary|ILmaxReadOnlySocketBoundaryTransport|ILmaxReadOnlyFixFrameBoundary|ILmaxReadOnlyMarketDataFrameCodec') {
    throw "API Program.cs contains R19/R18 low-level stack wiring."
}
if ($workerText -match 'LmaxExecutableReadOnlySessionStackFactory|LmaxExecutableReadOnlySocketSessionBoundary|ILmaxReadOnlySocketBoundaryTransport|ILmaxReadOnlyFixFrameBoundary|ILmaxReadOnlyMarketDataFrameCodec') {
    throw "Worker Program.cs contains R19/R18 low-level stack wiring."
}
Assert-False $appsettings.Safety.AllowExternalConnections "Appsettings Safety.AllowExternalConnections"
Assert-False $appsettings.Safety.AllowLiveTrading "Appsettings Safety.AllowLiveTrading"
Assert-True $appsettings.Safety.RequireFakeExecutionGateway "Appsettings Safety.RequireFakeExecutionGateway"
Assert-False $appsettings.LmaxReadOnlyRuntime.Enabled "Appsettings runtime Enabled"
Assert-Equal $appsettings.LmaxReadOnlyRuntime.ImplementationMode "DesignOnly" "Appsettings runtime mode"
Assert-False $appsettings.LmaxReadOnlyRuntime.AllowExternalConnections "Appsettings runtime external"
Assert-False $appsettings.LmaxReadOnlyRuntime.AllowCredentialUse "Appsettings runtime use"
Assert-False $appsettings.LmaxReadOnlyRuntime.AllowOrderSubmission "Appsettings runtime order"
Assert-False $appsettings.LmaxReadOnlyRuntime.SubmitToShadowReplay "Appsettings runtime shadow replay"
Assert-False $appsettings.LmaxReadOnlyRuntime.SchedulerEnabled "Appsettings runtime scheduler"

$reportPath = Join-Path $readiness "phase-lmax-r19-temporary-readonly-runtime-report.md"
$reportText = Get-Content -LiteralPath $reportPath -Raw
foreach ($heading in @(
    "Executive summary",
    "Operator approval",
    "Scope and constraints",
    "R18 full-stack basis",
    "Preflight result",
    "Full-stack validation",
    "Temporary runtime activation summary, if attempted",
    "Approved instrument status",
    "USDJPY caveat preservation",
    "Runtime boundary evidence",
    "Forbidden-action validation",
    "Shutdown/revert evidence",
    "Post-attempt non-mutation validation",
    "Rail/archive isolation validation",
    "Decision",
    "Recommended next phase"
)) {
    if ($reportText -notmatch [regex]::Escape($heading)) {
        throw "R19 report missing required section: $heading"
    }
}

$validation = [ordered]@{
    phase = "LMAX-R19"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    validator = "scripts/check-lmax-r19-temporary-readonly-runtime-gate.ps1"
    requiredArtifactsPresent = $true
    priorArtifactsPresent = $true
    r18DecisionConfirmed = $true
    operatorApprovalExact = $true
    preflightPassed = $true
    preflightAborted = $false
    fullStackValidation = "FailedNoApprovedExecutableRealLowLevelDependencies"
    fullStackValidationPassedForExecution = $false
    activationAttemptExecuted = $false
    attemptCount = 0
    retryCount = 0
    batchMode = $false
    loopMode = $false
    approvedInstrumentsOnly = $true
    usdJpyCaveatPreserved = $true
    outputSanitized = $true
    shutdownOrRevertCompleted = $true
    apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
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
    liveLauncherCreated = $false
    hostedServiceAdded = $false
    backgroundWorkerAdded = $false
    concreteAdapterUsed = $true
    realTransportUsed = $false
    executableSessionClientUsed = $false
    lowLevelStackUsed = $false
    noSensitiveContent = $true
    resultClassification = $gate.resultClassification
    recommendedNextPhase = $gate.recommendedNextPhase
    finalDecision = $gate.finalDecision
}

$validationPath = Join-Path $readiness "phase-lmax-r19-gate-validation.json"
$validation | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $validationPath -Encoding UTF8

Write-Host "PASS: $($gate.finalDecision)"
