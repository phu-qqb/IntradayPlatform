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
    "phase-lmax-r9-gate-validation.json"
)

$requiredR10 = @(
    "phase-lmax-r10-operator-approval-record.json",
    "phase-lmax-r10-preflight-gate.json",
    "phase-lmax-r10-adapter-path-resolution.json",
    "phase-lmax-r10-temporary-runtime-activation-record.json",
    "phase-lmax-r10-approved-instrument-status-record.json",
    "phase-lmax-r10-sanitized-runtime-boundary-evidence.json",
    "phase-lmax-r10-forbidden-action-validation.json",
    "phase-lmax-r10-shutdown-revert-record.json",
    "phase-lmax-r10-post-attempt-non-mutation-validation.json",
    "phase-lmax-r10-rail-isolation-validation.json",
    "phase-lmax-r10-decision-gate.json",
    "phase-lmax-r10-temporary-readonly-runtime-report.md",
    "phase-lmax-r10-operator-note.md"
)

Write-Host "LMAX-R10 Temporary Read-Only Runtime Gate Validator"
Write-Host "This validator performs no external run, snapshot, replay, POST endpoint, socket, runtime activation, API/Worker startup, credential loading, or config mutation."

Assert-RequiredArtifacts $readiness $requiredPrior
Assert-RequiredArtifacts $readiness $requiredR10

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

$r7Gate = Read-Json (Join-Path $readiness "phase-lmax-r7-decision-gate.json")
$r9GatePrior = Read-Json (Join-Path $readiness "phase-lmax-r9-decision-gate.json")
$r10Approval = Read-Json (Join-Path $readiness "phase-lmax-r10-operator-approval-record.json")
$r10Preflight = Read-Json (Join-Path $readiness "phase-lmax-r10-preflight-gate.json")
$r10Resolution = Read-Json (Join-Path $readiness "phase-lmax-r10-adapter-path-resolution.json")
$r10Activation = Read-Json (Join-Path $readiness "phase-lmax-r10-temporary-runtime-activation-record.json")
$r10InstrumentStatus = Read-Json (Join-Path $readiness "phase-lmax-r10-approved-instrument-status-record.json")
$r10Boundary = Read-Json (Join-Path $readiness "phase-lmax-r10-sanitized-runtime-boundary-evidence.json")
$r10Forbidden = Read-Json (Join-Path $readiness "phase-lmax-r10-forbidden-action-validation.json")
$r10Shutdown = Read-Json (Join-Path $readiness "phase-lmax-r10-shutdown-revert-record.json")
$r10NonMutation = Read-Json (Join-Path $readiness "phase-lmax-r10-post-attempt-non-mutation-validation.json")
$r10Isolation = Read-Json (Join-Path $readiness "phase-lmax-r10-rail-isolation-validation.json")
$r10Gate = Read-Json (Join-Path $readiness "phase-lmax-r10-decision-gate.json")
$t7Gate = Read-Json $t7GatePath
$phase7nGate = Read-Json $phase7nGatePath

Assert-Equal $r7Gate.finalDecision "LMAX_R7_LOCAL_RUNTIME_GATE_HARNESS_READY_NO_ACTIVATION" "R7 decision"
Assert-Equal $r9GatePrior.finalDecision "LMAX_R9_TEMPORARY_ADAPTER_PATH_IMPLEMENTED_DRY_RUN_ONLY_NO_ACTIVATION" "R9 decision"
Assert-Equal $t7Gate.finalDecision "USDJPY_T7_FINAL_READINESS_ARCHIVE_CLOSED_WITH_CAVEAT" "USDJPY T7 decision"
Assert-Equal $phase7nGate.finalDecision "PASS_FINAL_LMAX_READONLY_RUNTIME_EVIDENCE_ARCHIVE_CLOSED" "Phase 7N decision"

$expectedPhrase = "I, Philippe, explicitly approve Phase LMAX-R10 for one temporary Demo read-only runtime market-data activation attempt using the harness-backed adapter path for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority."

Assert-Equal $r10Approval.phase "LMAX-R10" "R10 approval phase"
Assert-Equal $r10Approval.operator "Philippe" "R10 operator"
Assert-Equal $r10Approval.approvalPhrase $expectedPhrase "R10 approval phrase"
Assert-True $r10Approval.approvalPhraseExact "R10 approval exact"
Assert-Equal $r10Approval.environment "Demo/read-only" "R10 approval environment"

foreach ($symbol in @("GBPUSD", "EURGBP", "AUDUSD", "USDJPY")) {
    if (-not (@($r10Approval.approvedInstruments | Where-Object { $_.symbol -eq $symbol }).Count -eq 1)) {
        throw "R10 approval missing instrument: $symbol"
    }
    if (-not (@($r10InstrumentStatus.instruments | Where-Object { $_.symbol -eq $symbol }).Count -eq 1)) {
        throw "R10 instrument status missing instrument: $symbol"
    }
}
$usdJpyApproval = @($r10Approval.approvedInstruments | Where-Object { $_.symbol -eq "USDJPY" })[0]
Assert-Equal $usdJpyApproval.caveat "prior failed-safe root cause remains unproven" "R10 approval USDJPY caveat"
$usdJpyStatus = @($r10InstrumentStatus.instruments | Where-Object { $_.symbol -eq "USDJPY" })[0]
Assert-Equal $usdJpyStatus.securityId "4004" "R10 USDJPY SecurityID"
Assert-Equal $usdJpyStatus.securityIdSource "8" "R10 USDJPY SecurityIDSource"
Assert-Equal $usdJpyStatus.caveat "prior failed-safe root cause remains unproven" "R10 USDJPY caveat"
Assert-False $usdJpyStatus.attempted "R10 USDJPY attempted"

Assert-True $r10Preflight.r7DecisionConfirmed "Preflight R7 decision"
Assert-True $r10Preflight.r9DecisionConfirmed "Preflight R9 decision"
Assert-True $r10Preflight.r7HarnessOutputUsed "Preflight R7 harness used"
Assert-True $r10Preflight.r9AdapterPathUsed "Preflight R9 path used"
Assert-True $r10Preflight.operatorApprovalExact "Preflight approval"
Assert-True $r10Preflight.approvedInstrumentListExact "Preflight instruments"
Assert-True $r10Preflight.usdJpyCaveatPresent "Preflight USDJPY caveat"
Assert-True $r10Preflight.environmentDemoReadOnly "Preflight demo read-only"
Assert-True $r10Preflight.outputSanitizationRequired "Preflight sanitization"
Assert-True $r10Preflight.shutdownRevertPlanPresent "Preflight shutdown/revert"
Assert-True $r10Preflight.outputPathUnderReadinessRuntimeEnablement "Preflight output path"
Assert-True $r10Preflight.preflightPassed "Preflight passed"
Assert-False $r10Preflight.preflightAborted "Preflight aborted"
foreach ($property in @("productionAccount", "allowOrderSubmission", "allowLiveTrading", "isTradingEnabled", "scheduler", "polling", "replay", "shadowReplay", "tradingMutation", "persistentRuntimeEnablement", "defaultGatewayRegistrationChange", "nonApprovedInstrumentConfigured", "permanentRuntimeEnablementPlanned", "defaultGatewayConfigChangePlanned", "liveLauncherCreated", "connectionAttempted", "runtimeActivationAttempted")) {
    Assert-False $r10Preflight.$property "Preflight $property"
}

Assert-True $r10Resolution.r9AdapterPathPresent "Resolution R9 path"
Assert-True $r10Resolution.r7HarnessOutputConsumable "Resolution R7 harness"
Assert-True $r10Resolution.dryRunAdapterAvailable "Resolution dry-run adapter"
Assert-True $r10Resolution.realAdapterSkeletonAvailable "Resolution skeleton"
Assert-False $r10Resolution.realAdapterSkeletonCanExecute "Resolution skeleton executable"
Assert-False $r10Resolution.concreteTemporaryRealAdapterAvailable "Resolution concrete adapter"
Assert-False $r10Resolution.concreteAdapterConsumesHarnessBackedRequest "Resolution concrete consumes request"
Assert-Equal $r10Resolution.resolution "DryRunAndSkeletonOnly" "Resolution summary"
Assert-True $r10Resolution.abortBeforeActivation "Resolution abort before activation"
Assert-Equal $r10Resolution.resultClassification "LMAX_R10_FAIL_NO_CONCRETE_REAL_ADAPTER_PATH" "Resolution classification"

Assert-True $r10Activation.preflightPassed "Activation record preflight"
Assert-True $r10Activation.adapterResolutionCompleted "Activation record resolution"
Assert-False $r10Activation.activationAttempted "Activation attempted"
Assert-False $r10Activation.activationExecuted "Activation executed"
Assert-Equal $r10Activation.attemptCount 0 "Activation attempt count"
Assert-Equal $r10Activation.retryCount 0 "Activation retry count"
foreach ($property in @("batchMode", "loopMode", "externalRunExecuted", "temporaryReadOnlyMarketDataAdapterUsed", "concreteTemporaryRealAdapterUsed", "runtimePoweredUp", "runtimeEnablementExecuted", "runtimeEnablementPersisted", "apiWorkerStarted", "defaultGatewayRegistrationChanged", "defaultConfigChanged", "liveLauncherCreated", "hostedServiceAdded", "backgroundWorkerAdded")) {
    Assert-False $r10Activation.$property "Activation record $property"
}
Assert-Equal $r10Activation.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Activation gateway mode"

foreach ($boundary in @("tcpBoundaryStatus", "tlsBoundaryStatus", "fixLogonBoundaryStatus", "marketDataRequestBoundaryStatus", "instrumentMarketDataStatus")) {
    Assert-Equal $r10Boundary.$boundary "NotAttemptedNoConcreteRealAdapterPath" "Boundary $boundary"
}
Assert-Equal $r10Boundary.adapterPathResolution "DryRunAndSkeletonOnly" "Boundary adapter resolution"
Assert-False $r10Boundary.activationAttempted "Boundary activation"
Assert-False $r10Boundary.rejectsObserved "Boundary rejects"
Assert-False $r10Boundary.sensitiveInformationDetected "Boundary sensitive"
Assert-True $r10Boundary.outputSanitized "Boundary sanitized"

Assert-True $r10Forbidden.passed "Forbidden validation"
foreach ($property in @("orderSubmissionExecuted", "orderStatusRequestSent", "orderCancelRequestSent", "tradeCaptureRequestSent", "orderPathEnabled", "orderGatewayRegistered", "tradingGatewayRegistered", "allowOrderSubmission", "allowLiveTrading", "isTradingEnabled", "schedulerStarted", "pollingStarted", "replayExecuted", "shadowReplaySubmitted", "tradingStateMutated", "productionAccountUsed", "nonApprovedInstrumentTouched", "defaultGatewayRegistrationChanged", "runtimeEnablementPersisted", "liveLauncherCreated", "hostedServiceAdded", "backgroundWorkerAdded", "retryExecuted", "batchExecuted", "loopExecuted")) {
    Assert-False $r10Forbidden.$property "Forbidden $property"
}

Assert-True $r10Shutdown.shutdownOrRevertCompleted "Shutdown/revert completed"
Assert-True $r10Shutdown.containmentCompleted "Containment completed"
Assert-False $r10Shutdown.activationAttempted "Shutdown activation"
Assert-True $r10Shutdown.defaultConfigUnchanged "Shutdown config"
Assert-True $r10Shutdown.defaultGatewayRegistrationUnchanged "Shutdown gateway"
Assert-False $r10Shutdown.runtimeEnablementPersisted "Shutdown persisted"
Assert-Equal $r10Shutdown.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Shutdown gateway mode"

Assert-Equal $r10NonMutation.attemptCount 0 "Non-mutation attempt count"
Assert-Equal $r10NonMutation.retryCount 0 "Non-mutation retry count"
Assert-True $r10NonMutation.approvedInstrumentsOnly "Non-mutation instruments"
Assert-True $r10NonMutation.usdJpyCaveatPreserved "Non-mutation USDJPY caveat"
Assert-True $r10NonMutation.outputSanitized "Non-mutation sanitized"
Assert-True $r10NonMutation.shutdownOrRevertCompleted "Non-mutation shutdown/revert"
Assert-Equal $r10NonMutation.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Non-mutation gateway mode"
foreach ($property in @("batchMode", "loopMode", "productionAccountUsed", "orderSubmissionExecuted", "orderPathEnabled", "orderGatewayRegistered", "tradingGatewayRegistered", "allowOrderSubmission", "allowLiveTrading", "isTradingEnabled", "schedulerStarted", "pollingStarted", "replayExecuted", "shadowReplaySubmitted", "tradingStateMutated", "defaultGatewayRegistrationChanged", "runtimeEnablementPersisted", "credentialsPrinted", "credentialsStored", "credentialsLoaded", "archivesModified", "liveLauncherCreated", "hostedServiceAdded", "backgroundWorkerAdded")) {
    Assert-False $r10NonMutation.$property "Non-mutation $property"
}

Assert-True $r10Isolation.passed "Rail isolation"
Assert-True $r10Isolation.usdJpyCaveatPreserved "Rail isolation USDJPY caveat"
foreach ($property in @("evidenceArchivesModified", "validatedRailsModified", "phase7ArchiveModified", "usdJpyT1T7ArtifactsModified", "r1ArtifactsModified", "r2ArtifactsModified", "r3ArtifactsModified", "r4ArtifactsModified", "r5ArtifactsModified", "r6ArtifactsModified", "r7ArtifactsModified", "r8ArtifactsModified", "r9ArtifactsModified", "gbpusdArchiveModified", "eurgbpArchiveModified", "audusdArchiveModified", "nonApprovedInstrumentTouched")) {
    Assert-False $r10Isolation.$property "Rail isolation $property"
}

$allowedDecisions = @(
    "LMAX_R10_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED",
    "LMAX_R10_FAIL_PREFLIGHT_ABORTED",
    "LMAX_R10_FAIL_NO_CONCRETE_REAL_ADAPTER_PATH",
    "LMAX_R10_FAIL_RUNTIME_ACTIVATION_BOUNDARY",
    "LMAX_R10_FAIL_SESSION_OR_MARKETDATA_BOUNDARY",
    "LMAX_R10_FAIL_SAFETY_CONSTRAINT",
    "LMAX_R10_INCONCLUSIVE_SANITIZED_EVIDENCE"
)
Assert-In $r10Gate.finalDecision $allowedDecisions "R10 final decision"
Assert-Equal $r10Gate.finalDecision "LMAX_R10_FAIL_NO_CONCRETE_REAL_ADAPTER_PATH" "R10 expected decision"
Assert-Equal $r10Gate.resultClassification "LMAX_R10_FAIL_NO_CONCRETE_REAL_ADAPTER_PATH" "R10 classification"
Assert-True $r10Gate.operatorApprovalExact "Gate approval"
Assert-True $r10Gate.r7DecisionConfirmed "Gate R7"
Assert-True $r10Gate.r9DecisionConfirmed "Gate R9"
Assert-True $r10Gate.preflightPassed "Gate preflight passed"
Assert-False $r10Gate.preflightAborted "Gate preflight aborted"
Assert-True $r10Gate.adapterPathResolutionCompleted "Gate adapter resolution"
Assert-Equal $r10Gate.adapterPathResolution "DryRunAndSkeletonOnly" "Gate adapter resolution"
Assert-False $r10Gate.concreteTemporaryRealAdapterAvailable "Gate concrete adapter"
Assert-False $r10Gate.activationAttemptExecuted "Gate activation"
Assert-Equal $r10Gate.attemptCount 0 "Gate attempt count"
Assert-Equal $r10Gate.retryCount 0 "Gate retry count"
Assert-True $r10Gate.approvedInstrumentsOnly "Gate approved instruments"
Assert-True $r10Gate.usdJpyCaveatPreserved "Gate USDJPY caveat"
Assert-Equal $r10Gate.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Gate gateway mode"
Assert-True $r10Gate.outputSanitized "Gate sanitized"
Assert-True $r10Gate.shutdownOrRevertCompleted "Gate shutdown/revert"
foreach ($property in @("batchMode", "loopMode", "productionAccountUsed", "externalRunExecuted", "snapshotExecuted", "replayExecuted", "postEndpointInvoked", "realSocketOpened", "tcpConnectionAttempted", "tlsHandshakeAttempted", "fixLogonAttempted", "marketDataRequestSent", "orderSubmissionExecuted", "tradingStateMutated", "schedulerStarted", "pollingStarted", "shadowReplaySubmitted", "apiWorkerStarted", "runtimePoweredUp", "retryExecuted", "batchExecuted", "loopExecuted", "runtimeEnablementExecuted", "runtimeEnablementPersisted", "tradingEnablementExecuted", "schedulerEnablementExecuted", "orderPathEnablementExecuted", "defaultGatewayRegistrationChanged", "liveConnectionScriptCreated", "liveLauncherCreated", "hostedServiceAdded", "backgroundWorkerAdded", "temporaryReadOnlyMarketDataAdapterUsed", "orderGatewayRegistered", "tradingGatewayRegistered", "credentialsPrinted", "credentialsStored", "credentialsLoaded", "archivesModified")) {
    Assert-False $r10Gate.$property "Gate $property"
}

$programPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\Program.cs"
$workerPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Worker\Program.cs"
$appsettingsPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\appsettings.json"
$programText = Get-Content -LiteralPath $programPath -Raw
$workerText = Get-Content -LiteralPath $workerPath -Raw
$appsettings = Read-Json $appsettingsPath
if ($programText -notmatch 'AddSingleton<IVenueExecutionGateway,\s*FakeLmaxGateway>') {
    throw "API Program.cs no longer registers FakeLmaxGateway as IVenueExecutionGateway."
}
if ($workerText -notmatch 'AddSingleton<IVenueExecutionGateway,\s*FakeLmaxGateway>') {
    throw "Worker Program.cs no longer registers FakeLmaxGateway as IVenueExecutionGateway."
}
if ($programText -match 'LmaxRealTemporaryReadOnlyRuntimeActivationAdapter|LmaxDryRunTemporaryReadOnlyRuntimeActivationAdapter|ILmaxTemporaryReadOnlyRuntimeActivationAdapter') {
    throw "API Program.cs contains temporary adapter wiring."
}
if ($workerText -match 'LmaxRealTemporaryReadOnlyRuntimeActivationAdapter|LmaxDryRunTemporaryReadOnlyRuntimeActivationAdapter|ILmaxTemporaryReadOnlyRuntimeActivationAdapter') {
    throw "Worker Program.cs contains temporary adapter wiring."
}
Assert-False $appsettings.Safety.AllowExternalConnections "Appsettings Safety.AllowExternalConnections"
Assert-False $appsettings.Safety.AllowLiveTrading "Appsettings Safety.AllowLiveTrading"
Assert-True $appsettings.Safety.RequireFakeExecutionGateway "Appsettings Safety.RequireFakeExecutionGateway"
Assert-False $appsettings.LmaxReadOnlyRuntime.Enabled "Appsettings runtime Enabled"
Assert-Equal $appsettings.LmaxReadOnlyRuntime.ImplementationMode "DesignOnly" "Appsettings runtime ImplementationMode"
Assert-False $appsettings.LmaxReadOnlyRuntime.AllowExternalConnections "Appsettings runtime AllowExternalConnections"
Assert-False $appsettings.LmaxReadOnlyRuntime.AllowCredentialUse "Appsettings runtime AllowCredentialUse"
Assert-False $appsettings.LmaxReadOnlyRuntime.AllowOrderSubmission "Appsettings runtime AllowOrderSubmission"
Assert-False $appsettings.LmaxReadOnlyRuntime.SubmitToShadowReplay "Appsettings runtime SubmitToShadowReplay"
Assert-False $appsettings.LmaxReadOnlyRuntime.SchedulerEnabled "Appsettings runtime SchedulerEnabled"

$reportPath = Join-Path $readiness "phase-lmax-r10-temporary-readonly-runtime-report.md"
$reportText = Get-Content -LiteralPath $reportPath -Raw
foreach ($heading in @(
    "Executive summary",
    "Operator approval",
    "Scope and constraints",
    "R7 harness and R9 adapter path basis",
    "Preflight result",
    "Adapter path resolution",
    "Temporary runtime activation summary",
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
        throw "R10 report missing required section: $heading"
    }
}

$validation = [ordered]@{
    phase = "LMAX-R10"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    validator = "scripts/check-lmax-r10-temporary-readonly-runtime-gate.ps1"
    requiredArtifactsPresent = $true
    priorArtifactsPresent = $true
    r7DecisionConfirmed = $true
    r9DecisionConfirmed = $true
    operatorApprovalExact = $true
    preflightPassed = $true
    preflightAborted = $false
    adapterPathResolution = "DryRunAndSkeletonOnly"
    concreteTemporaryRealAdapterAvailable = $false
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
    snapshotExecuted = $false
    replayExecuted = $false
    postEndpointInvoked = $false
    realSocketOpened = $false
    tcpConnectionAttempted = $false
    tlsHandshakeAttempted = $false
    fixLogonAttempted = $false
    marketDataRequestSent = $false
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
    noSensitiveContent = $true
    resultClassification = $r10Gate.resultClassification
    recommendedNextPhase = $r10Gate.recommendedNextPhase
    finalDecision = $r10Gate.finalDecision
}

$validationPath = Join-Path $readiness "phase-lmax-r10-gate-validation.json"
$validation | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $validationPath -Encoding UTF8

Write-Host "PASS: $($r10Gate.finalDecision)"
