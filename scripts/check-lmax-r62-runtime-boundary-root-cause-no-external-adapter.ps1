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
    "phase-lmax-r62-runtime-boundary-root-cause-report.md",
    "phase-lmax-r62-runtime-boundary-root-cause-summary.json",
    "phase-lmax-r62-r61-blocker-before-after-classification.json",
    "phase-lmax-r62-manual-surface-adapter-resolution-trace.json",
    "phase-lmax-r62-no-external-adapter-root-cause.json",
    "phase-lmax-r62-real-bounded-adapter-binding-decision.json",
    "phase-lmax-r62-real-bounded-adapter-binding-validation.json",
    "phase-lmax-r62-composition-chain-validation.json",
    "phase-lmax-r62-approval-gate-validation.json",
    "phase-lmax-r62-forbidden-actions-audit.json",
    "phase-lmax-r62-api-worker-fake-gateway-audit.json",
    "phase-lmax-r62-no-live-launcher-audit.json",
    "phase-lmax-r62-no-scheduler-polling-service-audit.json",
    "phase-lmax-r62-no-external-boundary-attempted.json",
    "phase-lmax-r62-usdjpy-caveat-preservation.json",
    "phase-lmax-r62-next-phase-recommendation.json",
    "phase-lmax-r62-gate-validation.json"
)

foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $name))) {
        Fail "Missing required R62 artifact: $name"
    }
}

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r62-runtime-boundary-root-cause-summary.json")
$beforeAfter = Read-Json (Join-Path $artifactRoot "phase-lmax-r62-r61-blocker-before-after-classification.json")
$trace = Read-Json (Join-Path $artifactRoot "phase-lmax-r62-manual-surface-adapter-resolution-trace.json")
$rootCause = Read-Json (Join-Path $artifactRoot "phase-lmax-r62-no-external-adapter-root-cause.json")
$decision = Read-Json (Join-Path $artifactRoot "phase-lmax-r62-real-bounded-adapter-binding-decision.json")
$binding = Read-Json (Join-Path $artifactRoot "phase-lmax-r62-real-bounded-adapter-binding-validation.json")
$composition = Read-Json (Join-Path $artifactRoot "phase-lmax-r62-composition-chain-validation.json")
$approval = Read-Json (Join-Path $artifactRoot "phase-lmax-r62-approval-gate-validation.json")
$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r62-forbidden-actions-audit.json")
$apiWorker = Read-Json (Join-Path $artifactRoot "phase-lmax-r62-api-worker-fake-gateway-audit.json")
$noLauncher = Read-Json (Join-Path $artifactRoot "phase-lmax-r62-no-live-launcher-audit.json")
$noService = Read-Json (Join-Path $artifactRoot "phase-lmax-r62-no-scheduler-polling-service-audit.json")
$noBoundary = Read-Json (Join-Path $artifactRoot "phase-lmax-r62-no-external-boundary-attempted.json")
$usdJpy = Read-Json (Join-Path $artifactRoot "phase-lmax-r62-usdjpy-caveat-preservation.json")
$gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r62-gate-validation.json")
$r61Gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r61-gate-validation.json")

if ($summary.classification -ne "LMAX_R62_PASS_REAL_BOUNDED_ADAPTER_BINDING_READY_NO_EXTERNAL_ACTIVATION" -or $gate.classification -ne $summary.classification) {
    Fail "Unexpected R62 classification: $($summary.classification)"
}

if ($r61Gate.classification -ne "LMAX_R61_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE" -or -not $r61Gate.executeOnceInvoked) {
    Fail "R61 inconclusive boundary evidence is missing or does not prove ExecuteOnce invocation."
}

if (-not $rootCause.rootCauseProvable -or $rootCause.class -ne "LmaxReadOnlyActivationManualExecutionSurfaceFactory" -or $rootCause.method -ne "CreateCaller") {
    Fail "NoExternalAdapter root cause does not identify the responsible class/factory/method."
}

if (-not ($rootCause.constructorDecision -like "*LmaxReadOnlyActivationManualExecutionSurfaceNoExternalAdapter*")) {
    Fail "Root cause does not name the NoExternalAdapter constructor decision."
}

if ($trace.nextRetryWouldStillResolveToNoExternalAdapter -or $beforeAfter.after.blockerPresentForNextRetry) {
    Fail "The next retry would still obviously resolve to NoExternalAdapter."
}

if (-not $binding.realBoundedAdapterBindingProvable -or $binding.realAdapterType -ne "LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter" -or $binding.realTransportType -ne "LmaxRealReadOnlyMarketDataTransport") {
    Fail "Real bounded executable read-only adapter binding is not provable."
}

if (-not $binding.realAdapterSelectedByExplicitModeOnly -or $decision.defaultAdapterMode -ne "no-external-boundary" -or $decision.nextRetryAdapterMode -ne "real-bounded-executable-readonly") {
    Fail "Adapter selection mode is not explicit or next retry mode is not provable."
}

if (-not $binding.noExternalAdapterPreserved -or -not $rootCause.noExternalAdapterPreservedForInertContexts -or $rootCause.noExternalAdapterRemoved) {
    Fail "NoExternalAdapter was removed or not preserved for inert contexts."
}

if ($binding.realAdapterDefaultGlobally -or $decision.realAdapterDefaultGlobally -or $summary.realAdapterDefaultGlobally -or $gate.realAdapterDefaultGlobally) {
    Fail "Real adapter became the global default."
}

$factoryPath = Join-Path $Root "tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualExecutionSurfaceFactory.cs"
$programPath = Join-Path $Root "tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/Program.cs"
$testPath = Join-Path $Root "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlyActivationManualExecutionSurfaceTests.cs"
foreach ($path in @($factoryPath, $programPath, $testPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing R62 source/test path: $path"
    }
}

$factory = Get-Content -LiteralPath $factoryPath -Raw
$program = Get-Content -LiteralPath $programPath -Raw
$tests = Get-Content -LiteralPath $testPath -Raw
foreach ($needle in @("NoExternalBoundaryMode", "RealBoundedExecutableReadOnlyMode", "CreateAdapter", "LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter", "LmaxRealReadOnlyMarketDataTransport", "LmaxReadOnlyActivationManualExecutionSurfaceNoExternalAdapter")) {
    if (-not $factory.Contains($needle)) {
        Fail "Factory source missing adapter binding proof: $needle"
    }
}

foreach ($needle in @("--adapter-mode", "real-bounded-executable-readonly", "no-external-boundary")) {
    if (-not $program.Contains($needle)) {
        Fail "Program source missing explicit adapter mode proof: $needle"
    }
}

foreach ($needle in @("R61_root_cause_is_reproducible", "Next_retry_can_select_real_bounded_executable_readonly_adapter_without_executing_it", "Real_bounded_adapter_is_not_the_global_default", "Unapproved_adapter_modes_are_rejected")) {
    if (-not $tests.Contains($needle)) {
        Fail "Focused tests missing required R62 proof: $needle"
    }
}

foreach ($gateName in @("r42ConcreteAdapterExecutablePathValid", "r44BoundedRuntimeActivationCompositionValid", "r46ExecutableBoundaryOperationCompositionValid", "r48ExternalBoundaryProviderExecutionCompositionValid", "r50FinalPreExternalConsolidationValid", "r52CredentialConfigSourceBindingValid", "r54RetryPhaseReservationRuleValid", "r56BoundedInvocationPathValid", "r58ManualOperationalCallerValid", "r60ManualCliExecutionSurfaceValid")) {
    if (-not $composition.compositionGates.$gateName) {
        Fail "Composition chain gate missing or false: $gateName"
    }
}

if ($composition.compositionChainBypassed -or $composition.approvalGatesWeakened) {
    Fail "Composition chain was bypassed or approval gates were weakened."
}

foreach ($flag in @("exactPerPhaseOperatorApprovalRequired", "retryPhaseRuleRequired", "arbitraryPhasesRejected", "unapprovedAdapterModesRejected", "unapprovedInstrumentsRejected", "approvedBoundedExecutableReadOnlyModeRequired", "boundedExecutorApprovalRequired", "runtimeDelegateBindingApprovalRequired", "r42ThroughR60GateChainRequired")) {
    if (-not $approval.$flag) {
        Fail "Approval gate validation failed: $flag"
    }
}

if ($approval.approvalGatesWeakened) {
    Fail "Approval gates were weakened."
}

if ($apiWorker.result -ne "PASS" -or $apiWorker.apiWorkerGatewayMode -ne "FakeLmaxGatewayOnly" -or $apiWorker.manualExecutionSurfaceRegisteredInApi -or $apiWorker.manualExecutionSurfaceRegisteredInWorker -or $apiWorker.realBoundedAdapterRegisteredInApi -or $apiWorker.realBoundedAdapterRegisteredInWorker -or $apiWorker.appsettingsLiveEnablementIntroduced -or $apiWorker.defaultRuntimeGatewayChanged) {
    Fail "API/Worker FakeLmaxGatewayOnly audit failed."
}

$apiProgram = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/Program.cs") -Raw
$workerProgram = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Worker/Program.cs") -Raw
$appsettings = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/appsettings.json") -Raw
foreach ($text in @($apiProgram, $workerProgram, $appsettings)) {
    if ($text.Contains("QQ.Production.Intraday.Tools.LmaxReadOnlyActivation") -or $text.Contains("LmaxReadOnlyActivationManualExecutionSurface") -or $text.Contains("real-bounded-executable-readonly")) {
        Fail "Manual CLI or real adapter mode is wired into API/Worker/default startup."
    }
}

if (-not $apiProgram.Contains("FakeLmaxGateway")) {
    Fail "API FakeLmaxGateway evidence missing."
}

if ($noLauncher.liveLauncherIntroduced -or $noLauncher.genericLiveLauncherIntroduced -or $noLauncher.oldPrototypeWrapperUsed -or $noLauncher.oldLabWrapperUsed -or $noLauncher.apiWorkerStartupPathIntroduced -or $noLauncher.appsettingsDefaultLiveEnablementIntroduced) {
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

if ($noBoundary.externalActivationAttempted -or $summary.externalActivationAttempted -or $gate.externalActivationAttempted -or $noBoundary.attemptCount -ne 0 -or $summary.attemptCount -ne 0 -or $noBoundary.executeOnceInvoked) {
    Fail "R62 must not attempt external activation or invoke ExecuteOnce."
}

foreach ($key in @("credentialConfig", "tcpSocket", "tls", "fixLogonSession", "marketDataRequest", "marketDataResponseEntries")) {
    if ($noBoundary.boundaryStatuses.$key -ne "NotAttempted") {
        Fail "Boundary $key must be NotAttempted during R62; actual: $($noBoundary.boundaryStatuses.$key)"
    }
}

if ($summary.credentialValuesReturned -or $binding.realCredentialValuesReadDuringR62 -or $noBoundary.credentialValuesReturned -or $gate.credentialValuesReturned) {
    Fail "credentialValuesReturned/credential reads must remain false."
}

if (-not $usdJpy.caveatPreserved -or $usdJpy.securityId -ne "4004" -or $usdJpy.securityIdSource -ne "8" -or $usdJpy.caveat -ne "prior failed-safe root cause remains unproven") {
    Fail "USDJPY caveat evidence is missing or weakened."
}

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r62-*" |
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
        Fail "Potential raw credential or sensitive FIX material found in R62 artifacts: $pattern"
    }
}

if ($gate.buildResult.status -ne "PASS" -or $gate.testResult.status -ne "PASS") {
    Fail "Build/test evidence is missing or not PASS."
}

Write-Host "LMAX R62 runtime boundary root-cause no-external adapter validation PASS"
