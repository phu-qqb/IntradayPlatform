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
    "phase-lmax-r56-invocation-path-decision-report.md",
    "phase-lmax-r56-invocation-path-summary.json",
    "phase-lmax-r56-r55-blocker-before-after-classification.json",
    "phase-lmax-r56-preflight-trace-review.json",
    "phase-lmax-r56-approved-invocation-path-validation.json",
    "phase-lmax-r56-single-attempt-validation.json",
    "phase-lmax-r56-composition-chain-validation.json",
    "phase-lmax-r56-approval-gate-validation.json",
    "phase-lmax-r56-forbidden-actions-audit.json",
    "phase-lmax-r56-api-worker-fake-gateway-audit.json",
    "phase-lmax-r56-no-live-launcher-audit.json",
    "phase-lmax-r56-no-scheduler-polling-service-audit.json",
    "phase-lmax-r56-no-external-boundary-attempted.json",
    "phase-lmax-r56-usdjpy-caveat-preservation.json",
    "phase-lmax-r56-next-phase-recommendation.json",
    "phase-lmax-r56-gate-validation.json"
)

foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $name))) {
        Fail "Missing required R56 artifact: $name"
    }
}

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r56-invocation-path-summary.json")
$beforeAfter = Read-Json (Join-Path $artifactRoot "phase-lmax-r56-r55-blocker-before-after-classification.json")
$pathValidation = Read-Json (Join-Path $artifactRoot "phase-lmax-r56-approved-invocation-path-validation.json")
$singleAttempt = Read-Json (Join-Path $artifactRoot "phase-lmax-r56-single-attempt-validation.json")
$composition = Read-Json (Join-Path $artifactRoot "phase-lmax-r56-composition-chain-validation.json")
$approval = Read-Json (Join-Path $artifactRoot "phase-lmax-r56-approval-gate-validation.json")
$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r56-forbidden-actions-audit.json")
$apiWorker = Read-Json (Join-Path $artifactRoot "phase-lmax-r56-api-worker-fake-gateway-audit.json")
$noLauncher = Read-Json (Join-Path $artifactRoot "phase-lmax-r56-no-live-launcher-audit.json")
$noService = Read-Json (Join-Path $artifactRoot "phase-lmax-r56-no-scheduler-polling-service-audit.json")
$noBoundary = Read-Json (Join-Path $artifactRoot "phase-lmax-r56-no-external-boundary-attempted.json")
$usdJpy = Read-Json (Join-Path $artifactRoot "phase-lmax-r56-usdjpy-caveat-preservation.json")
$gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r56-gate-validation.json")
$r55Trace = Read-Json (Join-Path $artifactRoot "phase-lmax-r55-preflight-trace.json")

if ($summary.classification -ne "LMAX_R56_PASS_APPROVED_BOUNDED_RUNTIME_ACTIVATION_INVOCATION_PATH_READY_NO_EXTERNAL_ACTIVATION") {
    Fail "Unexpected R56 classification: $($summary.classification)"
}

if ($beforeAfter.after.blockerPresent) {
    Fail "R55 invocation path blocker remains present."
}

if (-not $pathValidation.approvedInvocationPathProvable -or -not $pathValidation.callsExistingBoundedExecutorExecuteOnce -or $pathValidation.noApprovedR55BoundedRuntimeActivationInvocationPath) {
    Fail "Approved bounded invocation path is not provable or does not call ExecuteOnce."
}

$sourcePath = Join-Path $Root "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxBoundedReadOnlyActivationInvocationPath.cs"
if (-not (Test-Path -LiteralPath $sourcePath)) {
    Fail "Missing invocation path source file."
}

$source = Get-Content -LiteralPath $sourcePath -Raw
foreach ($needle in @("LmaxBoundedReadOnlyActivationInvocationPath", "InvokeOnce", "Validate", "LmaxTemporaryReadOnlyActivationExecutor", "ExecuteOnce", "ExactPerPhaseOperatorApprovalMissing", "ApprovedBoundedExecutableReadOnly", "BoundedExecutorApprovalMissing", "RuntimeDelegateBindingApprovalMissing")) {
    if (-not $source.Contains($needle)) {
        Fail "Invocation path source missing expected proof: $needle"
    }
}

foreach ($forbiddenSource in @("static void Main", "static async Task Main", "AddHostedService", ": BackgroundService", "IHostedService", "while (true)", "TcpClient", "SslStream", "NewOrderSingle")) {
    if ($source.Contains($forbiddenSource)) {
        Fail "Invocation path source contains forbidden launcher/service/network/order marker: $forbiddenSource"
    }
}

if ($singleAttempt.executorOptions.maxAttemptCount -ne 1 -or $singleAttempt.executorOptions.retryCount -ne 0 -or $singleAttempt.executorOptions.batchMode -or $singleAttempt.executorOptions.loopMode) {
    Fail "Single-attempt validation failed."
}

foreach ($gateName in @("r42ConcreteAdapterExecutablePathValid", "r44BoundedRuntimeActivationCompositionValid", "r46ExecutableBoundaryOperationCompositionValid", "r48ExternalBoundaryProviderExecutionCompositionValid", "r50FinalPreExternalConsolidationValid", "r52CredentialConfigSourceBindingValid", "r54RetryPhaseReservationRuleValid")) {
    if (-not $composition.compositionGates.$gateName) {
        Fail "Composition chain gate missing or false: $gateName"
    }
}

if (-not $approval.exactPerPhaseOperatorApprovalRequired -or -not $approval.arbitraryApprovalRejected -or -not $approval.arbitraryPhasesRejected) {
    Fail "Approval gates are weakened."
}

if ($apiWorker.result -ne "PASS" -or $apiWorker.apiWorkerGatewayMode -ne "FakeLmaxGatewayOnly" -or $apiWorker.invocationPathRegisteredInApi -or $apiWorker.invocationPathRegisteredInWorker -or $apiWorker.appsettingsLiveEnablementIntroduced -or $apiWorker.defaultRuntimeGatewayChanged) {
    Fail "API/Worker FakeLmaxGatewayOnly audit failed."
}

if ($noLauncher.liveLauncherIntroduced -or $noLauncher.consoleMainIntroduced -or $noLauncher.genericLiveRuntimeIntroduced) {
    Fail "Live launcher audit failed."
}

foreach ($flag in @("hostedServiceIntroduced", "backgroundServiceIntroduced", "schedulerIntroduced", "pollingLoopIntroduced", "timerIntroduced", "apiEndpointIntroduced", "workerRegistrationIntroduced")) {
    if ($noService.$flag) {
        Fail "Service/scheduler/polling audit failed: $flag"
    }
}

foreach ($flag in @("orderSubmissionExecuted", "newOrderSingleSent", "cancelReplaceSent", "tradingEnablementExecuted", "tradingStateMutated", "productionAccountUsed", "apiStarted", "workerStarted", "hostedServiceStarted", "backgroundServiceStarted", "schedulerStarted", "pollingLoopStarted", "replayExecuted", "shadowReplayExecuted", "shadowReplaySubmitted", "nonApprovedInstrumentTouched", "credentialValuesRead", "credentialValuesReturned", "credentialValuesPrintedStoredSerialized", "rawSensitiveFixLogsStored", "runtimeEnablementPersisted", "defaultGatewayRegistrationChanged")) {
    if ($forbidden.$flag) {
        Fail "Forbidden action flag is true: $flag"
    }
}

if ($noBoundary.externalActivationAttempted -or $noBoundary.attemptCount -ne 0) {
    Fail "R56 must not attempt external activation."
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

if (-not $r55Trace.gates.Where({ $_.blocker -eq "NoApprovedR55BoundedRuntimeActivationInvocationPath" })) {
    Fail "R55 full trace blocker proof is missing."
}

$apiProgram = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/Program.cs") -Raw
$workerProgram = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Worker/Program.cs") -Raw
$appsettings = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/appsettings.json") -Raw
foreach ($text in @($apiProgram, $workerProgram, $appsettings)) {
    if ($text.Contains("LmaxBoundedReadOnlyActivationInvocationPath")) {
        Fail "Invocation path is wired into API/Worker/default startup."
    }
}

if (-not $apiProgram.Contains("FakeLmaxGateway")) {
    Fail "API FakeLmaxGateway default evidence missing."
}

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r56-*" |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$secretPatterns = @(
    "password\s*[:=]",
    "passwd\s*[:=]",
    "token\s*[:=]",
    "secret\s*[:=]",
    "554=",
    "-----BEGIN",
    "SenderCompID\s*[:=]",
    "TargetCompID\s*[:=]"
)
foreach ($pattern in $secretPatterns) {
    if ($artifactText -match $pattern) {
        Fail "Potential raw credential or sensitive FIX material found in R56 artifacts: $pattern"
    }
}

if ($gate.buildResult.status -ne "PASS" -or $gate.testResult.status -ne "PASS") {
    Fail "Build/test evidence is missing or not PASS."
}

Write-Host "LMAX R56 approved bounded runtime activation invocation path validation PASS"
