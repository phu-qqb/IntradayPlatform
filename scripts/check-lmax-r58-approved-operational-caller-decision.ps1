param(
    [string]$Root = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Error $Message
    exit 1
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing required artifact: $Path"
    }

    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

$artifactRoot = Join-Path $Root "artifacts/readiness/lmax-runtime-enablement"
$required = @(
    "phase-lmax-r58-operational-caller-decision-report.md",
    "phase-lmax-r58-operational-caller-summary.json",
    "phase-lmax-r58-r57-blocker-before-after-classification.json",
    "phase-lmax-r58-preflight-trace-review.json",
    "phase-lmax-r58-approved-operational-caller-validation.json",
    "phase-lmax-r58-manual-only-validation.json",
    "phase-lmax-r58-single-attempt-validation.json",
    "phase-lmax-r58-composition-chain-validation.json",
    "phase-lmax-r58-approval-gate-validation.json",
    "phase-lmax-r58-forbidden-actions-audit.json",
    "phase-lmax-r58-api-worker-fake-gateway-audit.json",
    "phase-lmax-r58-no-live-launcher-audit.json",
    "phase-lmax-r58-no-scheduler-polling-service-audit.json",
    "phase-lmax-r58-no-external-boundary-attempted.json",
    "phase-lmax-r58-usdjpy-caveat-preservation.json",
    "phase-lmax-r58-next-phase-recommendation.json",
    "phase-lmax-r58-gate-validation.json"
)

foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $name))) {
        Fail "Missing required R58 artifact: $name"
    }
}

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r58-operational-caller-summary.json")
$beforeAfter = Read-Json (Join-Path $artifactRoot "phase-lmax-r58-r57-blocker-before-after-classification.json")
$callerValidation = Read-Json (Join-Path $artifactRoot "phase-lmax-r58-approved-operational-caller-validation.json")
$manualOnly = Read-Json (Join-Path $artifactRoot "phase-lmax-r58-manual-only-validation.json")
$singleAttempt = Read-Json (Join-Path $artifactRoot "phase-lmax-r58-single-attempt-validation.json")
$composition = Read-Json (Join-Path $artifactRoot "phase-lmax-r58-composition-chain-validation.json")
$approval = Read-Json (Join-Path $artifactRoot "phase-lmax-r58-approval-gate-validation.json")
$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r58-forbidden-actions-audit.json")
$apiWorker = Read-Json (Join-Path $artifactRoot "phase-lmax-r58-api-worker-fake-gateway-audit.json")
$noLauncher = Read-Json (Join-Path $artifactRoot "phase-lmax-r58-no-live-launcher-audit.json")
$noService = Read-Json (Join-Path $artifactRoot "phase-lmax-r58-no-scheduler-polling-service-audit.json")
$noBoundary = Read-Json (Join-Path $artifactRoot "phase-lmax-r58-no-external-boundary-attempted.json")
$usdJpy = Read-Json (Join-Path $artifactRoot "phase-lmax-r58-usdjpy-caveat-preservation.json")
$gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r58-gate-validation.json")
$r56Gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r56-gate-validation.json")
$r57Trace = Read-Json (Join-Path $artifactRoot "phase-lmax-r57-preflight-trace.json")

if ($summary.classification -ne "LMAX_R58_PASS_APPROVED_OPERATIONAL_CALLER_READY_NO_EXTERNAL_ACTIVATION") {
    Fail "Unexpected R58 classification: $($summary.classification)"
}

if ($summary.decision -ne "Option A caller implemented") {
    Fail "R58 did not select Option A caller implementation."
}

if ($r56Gate.classification -ne "LMAX_R56_PASS_APPROVED_BOUNDED_RUNTIME_ACTIVATION_INVOCATION_PATH_READY_NO_EXTERNAL_ACTIVATION" -or -not $r56Gate.passed) {
    Fail "R56 success evidence is missing or not passing."
}

if (-not $r57Trace.gates.Where({ $_.result -eq "FAIL" -and $_.blocker -eq "NoApprovedR57OperationalCallerForBoundedInvocationPath" })) {
    Fail "R57 full trace concrete operational caller blocker is missing."
}

if ($beforeAfter.after.blockerPresent -or $summary.noApprovedR57OperationalCallerForBoundedInvocationPath -or $callerValidation.noApprovedR57OperationalCallerForBoundedInvocationPath) {
    Fail "NoApprovedR57OperationalCallerForBoundedInvocationPath remains true."
}

if (-not $callerValidation.approvedOperationalCallerProvable -or -not $callerValidation.callsBoundedInvocationPath) {
    Fail "Approved operational caller is not provable or does not call the invocation path."
}

if (-not $callerValidation.invocationPathCallsExecuteOnce -or $callerValidation.executeOnceTarget -ne "LmaxTemporaryReadOnlyActivationExecutor.ExecuteOnce") {
    Fail "Invocation path does not prove ExecuteOnce usage."
}

$callerSourcePath = Join-Path $Root "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxManualBoundedReadOnlyActivationCaller.cs"
$invocationSourcePath = Join-Path $Root "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxBoundedReadOnlyActivationInvocationPath.cs"
foreach ($path in @($callerSourcePath, $invocationSourcePath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing source path: $path"
    }
}

$callerSource = Get-Content -LiteralPath $callerSourcePath -Raw
$invocationSource = Get-Content -LiteralPath $invocationSourcePath -Raw
foreach ($needle in @("LmaxManualBoundedReadOnlyActivationCaller", "CallOnce", "LmaxBoundedReadOnlyActivationInvocationPath", "InvokeOnce", "ManualOperatorInvocationMissing", "OperationalCallerAlreadyConsumed", "ExactPerPhaseOperatorApprovalRequired", "ApprovedBoundedExecutableReadOnly")) {
    if (-not $callerSource.Contains($needle)) {
        Fail "Operational caller source missing expected proof: $needle"
    }
}

foreach ($needle in @("LmaxBoundedReadOnlyActivationInvocationPath", "InvokeOnce", "LmaxTemporaryReadOnlyActivationExecutor", "ExecuteOnce", "ExactPerPhaseOperatorApprovalMissing")) {
    if (-not $invocationSource.Contains($needle)) {
        Fail "Invocation path source missing expected proof: $needle"
    }
}

foreach ($forbiddenSource in @("static void Main", "static async Task Main", "AddHostedService", ": BackgroundService", "IHostedService", "while (true)", "TcpClient", "SslStream", "NewOrderSingle")) {
    if ($callerSource.Contains($forbiddenSource)) {
        Fail "Operational caller source contains forbidden launcher/service/network/order marker: $forbiddenSource"
    }
}

if (-not $manualOnly.manualOnly -or -not $manualOnly.manualOperatorInvocationRequired -or -not $manualOnly.manualRunbookReviewedRequired) {
    Fail "Manual-only validation failed."
}

foreach ($flag in @("apiEndpointIntroduced", "workerRegistrationIntroduced", "appsettingsDefaultEnablementIntroduced", "hostedServiceIntroduced", "backgroundServiceIntroduced", "schedulerIntroduced", "pollingLoopIntroduced", "genericLiveLauncherIntroduced")) {
    if ($manualOnly.$flag) {
        Fail "Manual-only validation introduced forbidden reachability: $flag"
    }
}

if (-not $singleAttempt.singleAttemptOnly -or -not $singleAttempt.callerPermitsOneCallPerInstance -or $singleAttempt.executorOptions.maxAttemptCount -ne 1 -or $singleAttempt.executorOptions.retryCount -ne 0 -or $singleAttempt.executorOptions.batchMode -or $singleAttempt.executorOptions.loopMode) {
    Fail "Single-attempt validation failed."
}

foreach ($gateName in @("r42ConcreteAdapterExecutablePathValid", "r44BoundedRuntimeActivationCompositionValid", "r46ExecutableBoundaryOperationCompositionValid", "r48ExternalBoundaryProviderExecutionCompositionValid", "r50FinalPreExternalConsolidationValid", "r52CredentialConfigSourceBindingValid", "r54RetryPhaseReservationRuleValid", "r56BoundedInvocationPathValid")) {
    if (-not $composition.compositionGates.$gateName) {
        Fail "Composition chain gate missing or false: $gateName"
    }
}

if ($composition.compositionChainBypassed -or $composition.approvalGatesWeakened) {
    Fail "Composition chain was bypassed or approval gates were weakened."
}

foreach ($flag in @("exactPerPhaseOperatorApprovalRequired", "retryPhaseRuleRequired", "arbitraryPhasesRejected", "unapprovedInstrumentsRejected", "boundedExecutorApprovalRequired", "runtimeDelegateBindingApprovalRequired", "approvedBoundedExecutableReadOnlyModeRequired", "r42ThroughR56GateChainRequired")) {
    if (-not $approval.$flag) {
        Fail "Approval gate validation failed: $flag"
    }
}

if ($apiWorker.result -ne "PASS" -or $apiWorker.apiWorkerGatewayMode -ne "FakeLmaxGatewayOnly" -or $apiWorker.operationalCallerRegisteredInApi -or $apiWorker.operationalCallerRegisteredInWorker -or $apiWorker.invocationPathRegisteredInApi -or $apiWorker.invocationPathRegisteredInWorker -or $apiWorker.appsettingsLiveEnablementIntroduced -or $apiWorker.defaultRuntimeGatewayChanged) {
    Fail "API/Worker FakeLmaxGatewayOnly audit failed."
}

if ($noLauncher.liveLauncherIntroduced -or $noLauncher.consoleMainIntroduced -or $noLauncher.genericLiveRuntimeIntroduced -or $noLauncher.oldPrototypeWrapperUsed -or $noLauncher.oldLabWrapperUsed) {
    Fail "Live launcher audit failed."
}

foreach ($flag in @("hostedServiceIntroduced", "backgroundServiceIntroduced", "schedulerIntroduced", "pollingLoopIntroduced", "timerIntroduced", "apiEndpointIntroduced", "workerRegistrationIntroduced")) {
    if ($noService.$flag) {
        Fail "Service/scheduler/polling audit failed: $flag"
    }
}

foreach ($flag in @("orderSubmissionExecuted", "newOrderSingleSent", "cancelReplaceSent", "tradingEnablementExecuted", "tradingStateMutated", "productionAccountUsed", "productionAccountAllowed", "apiStarted", "workerStarted", "hostedServiceStarted", "backgroundServiceStarted", "schedulerStarted", "pollingLoopStarted", "replayExecuted", "shadowReplayExecuted", "shadowReplaySubmitted", "nonApprovedInstrumentTouched", "credentialValuesRead", "credentialValuesReturned", "credentialValuesPrintedStoredSerialized", "rawSensitiveFixLogsStored", "defaultGatewayRegistrationChanged", "runtimeEnablementPersisted")) {
    if ($forbidden.$flag) {
        Fail "Forbidden action flag is true: $flag"
    }
}

if ($noBoundary.externalActivationAttempted -or $summary.externalActivationAttempted -or $noBoundary.attemptCount -ne 0 -or $summary.attemptCount -ne 0 -or $noBoundary.executeOnceInvoked) {
    Fail "R58 must not attempt external activation or invoke ExecuteOnce."
}

foreach ($key in @("credentialConfig", "tcpSocket", "tls", "fixLogonSession", "marketDataRequest", "marketDataResponseEntries")) {
    if ($noBoundary.boundaryStatuses.$key -ne "NotAttempted") {
        Fail "Boundary $key must be NotAttempted; actual: $($noBoundary.boundaryStatuses.$key)"
    }
}

if ($summary.credentialValuesReturned -or $noBoundary.credentialValuesReturned) {
    Fail "credentialValuesReturned must remain false."
}

if (-not $usdJpy.caveatPreserved -or $usdJpy.securityId -ne "4004" -or $usdJpy.securityIdSource -ne "8" -or $usdJpy.caveat -ne "prior failed-safe root cause remains unproven") {
    Fail "USDJPY caveat evidence is missing or weakened."
}

$apiProgram = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/Program.cs") -Raw
$workerProgram = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Worker/Program.cs") -Raw
$appsettings = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/appsettings.json") -Raw
foreach ($text in @($apiProgram, $workerProgram, $appsettings)) {
    if ($text.Contains("LmaxManualBoundedReadOnlyActivationCaller")) {
        Fail "Operational caller is wired into API/Worker/default startup."
    }
}

if (-not $apiProgram.Contains("FakeLmaxGateway")) {
    Fail "API FakeLmaxGateway default evidence missing."
}

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r58-*" |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$sensitivePatterns = @(
    "password\s*[:=]",
    "passwd\s*[:=]",
    "token\s*[:=]",
    "secret\s*[:=]",
    "554=",
    "-----BEGIN",
    "SenderCompID\s*[:=]",
    "TargetCompID\s*[:=]"
)
foreach ($pattern in $sensitivePatterns) {
    if ($artifactText -match $pattern) {
        Fail "Potential raw credential or sensitive FIX material found in R58 artifacts: $pattern"
    }
}

if ($gate.buildResult.status -ne "PASS" -or $gate.testResult.status -ne "PASS") {
    Fail "Build/test evidence is missing or not PASS."
}

Write-Host "LMAX R58 approved operational caller decision validation PASS"
