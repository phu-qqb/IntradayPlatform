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
    "phase-lmax-r60-execution-surface-decision-report.md",
    "phase-lmax-r60-execution-surface-summary.json",
    "phase-lmax-r60-r59-blocker-before-after-classification.json",
    "phase-lmax-r60-operational-invocation-trace-review.json",
    "phase-lmax-r60-manual-cli-execution-surface-validation.json",
    "phase-lmax-r60-call-chain-validation.json",
    "phase-lmax-r60-single-attempt-validation.json",
    "phase-lmax-r60-approval-gate-validation.json",
    "phase-lmax-r60-composition-chain-validation.json",
    "phase-lmax-r60-forbidden-actions-audit.json",
    "phase-lmax-r60-api-worker-fake-gateway-audit.json",
    "phase-lmax-r60-no-live-launcher-audit.json",
    "phase-lmax-r60-no-scheduler-polling-service-audit.json",
    "phase-lmax-r60-no-external-boundary-attempted.json",
    "phase-lmax-r60-usdjpy-caveat-preservation.json",
    "phase-lmax-r60-next-phase-recommendation.json",
    "phase-lmax-r60-gate-validation.json"
)

foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $name))) {
        Fail "Missing required R60 artifact: $name"
    }
}

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r60-execution-surface-summary.json")
$beforeAfter = Read-Json (Join-Path $artifactRoot "phase-lmax-r60-r59-blocker-before-after-classification.json")
$surfaceValidation = Read-Json (Join-Path $artifactRoot "phase-lmax-r60-manual-cli-execution-surface-validation.json")
$callChain = Read-Json (Join-Path $artifactRoot "phase-lmax-r60-call-chain-validation.json")
$singleAttempt = Read-Json (Join-Path $artifactRoot "phase-lmax-r60-single-attempt-validation.json")
$approval = Read-Json (Join-Path $artifactRoot "phase-lmax-r60-approval-gate-validation.json")
$composition = Read-Json (Join-Path $artifactRoot "phase-lmax-r60-composition-chain-validation.json")
$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r60-forbidden-actions-audit.json")
$apiWorker = Read-Json (Join-Path $artifactRoot "phase-lmax-r60-api-worker-fake-gateway-audit.json")
$noLauncher = Read-Json (Join-Path $artifactRoot "phase-lmax-r60-no-live-launcher-audit.json")
$noService = Read-Json (Join-Path $artifactRoot "phase-lmax-r60-no-scheduler-polling-service-audit.json")
$noBoundary = Read-Json (Join-Path $artifactRoot "phase-lmax-r60-no-external-boundary-attempted.json")
$usdJpy = Read-Json (Join-Path $artifactRoot "phase-lmax-r60-usdjpy-caveat-preservation.json")
$gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r60-gate-validation.json")
$r58Gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r58-gate-validation.json")
$r59Summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r59-temporary-readonly-activation-retry-summary.json")

if ($summary.classification -ne "LMAX_R60_PASS_APPROVED_MANUAL_ACTIVATION_EXECUTION_SURFACE_READY_NO_EXTERNAL_ACTIVATION") {
    Fail "Unexpected R60 classification: $($summary.classification)"
}

if ($summary.decision -ne "Option A manual execution surface implemented") {
    Fail "R60 did not select Option A manual execution surface."
}

if ($r58Gate.classification -ne "LMAX_R58_PASS_APPROVED_OPERATIONAL_CALLER_READY_NO_EXTERNAL_ACTIVATION" -or -not $r58Gate.passed) {
    Fail "R58 success evidence is missing or not passing."
}

if ($r59Summary.classification -ne "LMAX_R59_FAIL_EXECUTE_ONCE_NOT_INVOKED" -or $r59Summary.concreteCause -ne "ExecuteOnceNotInvokedByApprovedOperationalCallerInR59") {
    Fail "R59 concrete ExecuteOnce-not-invoked blocker evidence is missing."
}

if ($beforeAfter.after.blockerPresentForNextRetry -or -not $summary.r59ExecuteOnceNotInvokedBlockerResolvedForNextRetry -or -not $gate.r59ExecuteOnceNotInvokedBlockerResolvedForNextRetry) {
    Fail "ExecuteOnceNotInvokedByApprovedOperationalCallerInR59 remains unresolved for the next retry."
}

if (-not $surfaceValidation.approvedManualExecutionSurfaceProvable -or -not $summary.executionSurfaceProject) {
    Fail "Approved manual execution surface is not provable."
}

$toolProject = Join-Path $Root "tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation.csproj"
$programPath = Join-Path $Root "tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/Program.cs"
$surfacePath = Join-Path $Root "tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualExecutionSurface.cs"
$factoryPath = Join-Path $Root "tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualExecutionSurfaceFactory.cs"
foreach ($path in @($toolProject, $programPath, $surfacePath, $factoryPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing execution surface source path: $path"
    }
}

$program = Get-Content -LiteralPath $programPath -Raw
$surface = Get-Content -LiteralPath $surfacePath -Raw
$factory = Get-Content -LiteralPath $factoryPath -Raw
$combinedTool = "$program`n$surface`n$factory"

foreach ($needle in @(
    "LmaxReadOnlyActivationManualExecutionSurface",
    "LmaxManualBoundedReadOnlyActivationCaller",
    "CallOnce",
    "LmaxBoundedReadOnlyActivationInvocationPath",
    "InvokeOnce",
    "LmaxTemporaryReadOnlyActivationExecutor",
    "ExecuteOnce"
)) {
    if (-not $combinedTool.Contains($needle)) {
        Fail "Manual execution surface source missing call-chain proof: $needle"
    }
}

foreach ($needle in @("--execute-once", "--manual-confirm", "--approval-file", "--expected-approval-file", "--single-attempt-only", "--no-api-worker-startup", "--no-service-scheduler-polling", "--no-order-trading-path", "--no-credential-output")) {
    if (-not $program.Contains($needle)) {
        Fail "Program missing required explicit command/operator input: $needle"
    }
}

if (-not $surface.Contains("LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved") -or -not $surface.Contains("ExpectedOperatorApprovalPhrase") -or -not $surface.Contains("OperatorApprovalPhrase")) {
    Fail "Manual execution surface does not prove retry phase rule and exact per-phase approval checks."
}

if (-not $callChain.callChainProvable -or -not $callChain.executionSurfaceCallsOperationalCaller -or -not $callChain.callOnceCallsInvokeOnce -or -not $callChain.invokeOnceCallsExecuteOnce) {
    Fail "Call chain validation failed."
}

if ($callChain.executeOnceInvokedDuringR60 -or $summary.executeOnceInvokedDuringR60 -or $noBoundary.executeOnceInvoked) {
    Fail "R60 must prove ExecuteOnce reachability but must not invoke it during R60."
}

if (-not $singleAttempt.singleAttemptOnly -or $singleAttempt.maxAttemptCount -ne 1 -or $singleAttempt.retryCount -ne 0 -or $singleAttempt.batchMode -or $singleAttempt.loopMode -or -not $singleAttempt.manualExecutionSurfacePermitsOneCallPerInstance) {
    Fail "Single-attempt validation failed."
}

foreach ($flag in @("exactPerPhaseOperatorApprovalRequired", "approvalFileRequired", "expectedApprovalFileRequired", "approvalPhraseEqualityRequired", "retryPhaseRuleRequired", "arbitraryPhasesRejected", "unapprovedInstrumentsRejected", "approvedBoundedExecutableReadOnlyModeRequired", "boundedExecutorApprovalRequired", "runtimeDelegateBindingApprovalRequired", "r42ThroughR58GateChainRequired")) {
    if (-not $approval.$flag) {
        Fail "Approval gate validation failed: $flag"
    }
}

if ($approval.approvalGatesWeakened) {
    Fail "Approval gates were weakened."
}

foreach ($gateName in @("r42ConcreteAdapterExecutablePathValid", "r44BoundedRuntimeActivationCompositionValid", "r46ExecutableBoundaryOperationCompositionValid", "r48ExternalBoundaryProviderExecutionCompositionValid", "r50FinalPreExternalConsolidationValid", "r52CredentialConfigSourceBindingValid", "r54RetryPhaseReservationRuleValid", "r56BoundedInvocationPathValid", "r58ManualOperationalCallerValid")) {
    if (-not $composition.compositionGates.$gateName) {
        Fail "Composition chain gate missing or false: $gateName"
    }
}

if ($composition.compositionChainBypassed -or $composition.approvalGatesWeakened) {
    Fail "Composition chain was bypassed or approval gates were weakened."
}

if ($apiWorker.result -ne "PASS" -or $apiWorker.apiWorkerGatewayMode -ne "FakeLmaxGatewayOnly" -or $apiWorker.executionSurfaceRegisteredInApi -or $apiWorker.executionSurfaceRegisteredInWorker -or $apiWorker.operationalCallerRegisteredInApi -or $apiWorker.operationalCallerRegisteredInWorker -or $apiWorker.invocationPathRegisteredInApi -or $apiWorker.invocationPathRegisteredInWorker -or $apiWorker.appsettingsLiveEnablementIntroduced -or $apiWorker.defaultRuntimeGatewayChanged) {
    Fail "API/Worker FakeLmaxGatewayOnly audit failed."
}

if (-not $noLauncher.manualCliExecutionSurfaceIntroduced -or $noLauncher.genericLiveLauncherIntroduced -or $noLauncher.liveLauncherIntroduced -or $noLauncher.oldPrototypeWrapperUsed -or $noLauncher.oldLabWrapperUsed -or $noLauncher.apiWorkerStartupPathIntroduced -or $noLauncher.appsettingsDefaultLiveEnablementIntroduced) {
    Fail "Live launcher audit failed."
}

foreach ($flag in @("hostedServiceIntroduced", "backgroundServiceIntroduced", "schedulerIntroduced", "pollingLoopIntroduced", "timerIntroduced", "apiEndpointIntroduced", "workerRegistrationIntroduced")) {
    if ($noService.$flag) {
        Fail "Service/scheduler/polling audit failed: $flag"
    }
}

foreach ($forbiddenSource in @("AddHostedService", ": BackgroundService", "IHostedService", "while (true)", "TcpClient", "SslStream", "NewOrderSingle", "OrderCancelRequest", "TradeCapture", "ShadowReplaySubmit")) {
    if ($combinedTool.Contains($forbiddenSource)) {
        Fail "Execution surface source contains forbidden marker: $forbiddenSource"
    }
}

$apiProgram = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/Program.cs") -Raw
$workerProgram = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Worker/Program.cs") -Raw
$appsettings = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/appsettings.json") -Raw
foreach ($text in @($apiProgram, $workerProgram, $appsettings)) {
    if ($text.Contains("QQ.Production.Intraday.Tools.LmaxReadOnlyActivation") -or $text.Contains("LmaxReadOnlyActivationManualExecutionSurface")) {
        Fail "Manual execution surface is wired into API/Worker/default startup."
    }
}

if (-not $apiProgram.Contains("FakeLmaxGateway")) {
    Fail "API FakeLmaxGateway default evidence missing."
}

foreach ($flag in @("orderSubmissionExecuted", "newOrderSingleSent", "cancelReplaceSent", "tradingEnablementExecuted", "tradingStateMutated", "productionAccountUsed", "productionAccountAllowed", "apiStarted", "workerStarted", "hostedServiceStarted", "backgroundServiceStarted", "schedulerStarted", "pollingLoopStarted", "replayExecuted", "shadowReplayExecuted", "shadowReplaySubmitted", "nonApprovedInstrumentTouched", "credentialValuesRead", "credentialValuesReturned", "credentialValuesPrintedStoredSerialized", "rawSensitiveFixLogsStored", "defaultGatewayRegistrationChanged", "runtimeEnablementPersisted")) {
    if ($forbidden.$flag) {
        Fail "Forbidden action flag is true: $flag"
    }
}

if ($noBoundary.externalActivationAttempted -or $summary.externalActivationAttempted -or $noBoundary.attemptCount -ne 0 -or $summary.attemptCount -ne 0) {
    Fail "R60 must not attempt external activation."
}

foreach ($key in @("credentialConfig", "tcpSocket", "tls", "fixLogonSession", "marketDataRequest", "marketDataResponseEntries")) {
    if ($noBoundary.boundaryStatuses.$key -ne "NotAttempted") {
        Fail "Boundary $key must be NotAttempted; actual: $($noBoundary.boundaryStatuses.$key)"
    }
}

if ($summary.credentialValuesReturned -or $noBoundary.credentialValuesReturned -or $gate.credentialValuesReturned) {
    Fail "credentialValuesReturned must remain false."
}

if (-not $usdJpy.caveatPreserved -or $usdJpy.securityId -ne "4004" -or $usdJpy.securityIdSource -ne "8" -or $usdJpy.caveat -ne "prior failed-safe root cause remains unproven") {
    Fail "USDJPY caveat evidence is missing or weakened."
}

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r60-*" |
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
        Fail "Potential raw credential or sensitive FIX material found in R60 artifacts: $pattern"
    }
}

if ($gate.buildResult.status -ne "PASS" -or $gate.testResult.status -ne "PASS") {
    Fail "Build/test evidence is missing or not PASS."
}

Write-Host "LMAX R60 activation execution surface decision validation PASS"
