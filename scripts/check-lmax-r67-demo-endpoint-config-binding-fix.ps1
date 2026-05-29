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
    "phase-lmax-r67-demo-endpoint-config-binding-fix-report.md",
    "phase-lmax-r67-demo-endpoint-config-binding-summary.json",
    "phase-lmax-r67-r66-root-cause-before-after-classification.json",
    "phase-lmax-r67-endpoint-binding-decision.json",
    "phase-lmax-r67-endpoint-binding-validation.json",
    "phase-lmax-r67-placeholder-host-elimination-validation.json",
    "phase-lmax-r67-production-endpoint-exclusion-validation.json",
    "phase-lmax-r67-real-bounded-adapter-path-validation.json",
    "phase-lmax-r67-no-external-boundary-attempted.json",
    "phase-lmax-r67-forbidden-actions-audit.json",
    "phase-lmax-r67-api-worker-fake-gateway-audit.json",
    "phase-lmax-r67-no-live-launcher-audit.json",
    "phase-lmax-r67-no-scheduler-polling-service-audit.json",
    "phase-lmax-r67-credential-sanitization-validation.json",
    "phase-lmax-r67-usdjpy-caveat-preservation.json",
    "phase-lmax-r67-next-phase-recommendation.json",
    "phase-lmax-r67-gate-validation.json"
)

foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $name))) {
        Fail "Missing required R67 artifact: $name"
    }
}

$allowed = @(
    "LMAX_R67_PASS_DEMO_ENDPOINT_CONFIG_BINDING_READY_NO_EXTERNAL_ACTIVATION",
    "LMAX_R67_PASS_ENDPOINT_BINDING_ROOT_CAUSE_CONFIRMED_BINDING_NOT_IMPLEMENTED_NO_EXTERNAL_ACTIVATION",
    "LMAX_R67_FAIL_DEMO_ENDPOINT_BINDING_NOT_PROVABLE",
    "LMAX_R67_FAIL_PLACEHOLDER_HOST_STILL_USED",
    "LMAX_R67_FAIL_PRODUCTION_ENDPOINT_RISK",
    "LMAX_R67_FAIL_ENDPOINT_SANITIZATION_RISK",
    "LMAX_R67_FAIL_REAL_ENDPOINT_DEFAULT_GLOBAL_RISK",
    "LMAX_R67_FAIL_NO_EXTERNAL_DEFAULT_REMOVED_UNSAFELY",
    "LMAX_R67_FAIL_APPROVAL_GATES_WEAKENED",
    "LMAX_R67_FAIL_API_WORKER_GATEWAY_REGRESSION",
    "LMAX_R67_FAIL_LIVE_LAUNCHER_OR_BACKGROUND_SERVICE_INTRODUCED",
    "LMAX_R67_FAIL_SCHEDULER_OR_POLLING_INTRODUCED",
    "LMAX_R67_FAIL_FORBIDDEN_ACTION_INTRODUCED",
    "LMAX_R67_FAIL_EXTERNAL_BOUNDARY_ATTEMPTED",
    "LMAX_R67_FAIL_CREDENTIAL_VALUES_RETURNED_OR_SECRET_RISK",
    "LMAX_R67_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R67_FAIL_BUILD_OR_TESTS"
)

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r67-demo-endpoint-config-binding-summary.json")
$beforeAfter = Read-Json (Join-Path $artifactRoot "phase-lmax-r67-r66-root-cause-before-after-classification.json")
$decision = Read-Json (Join-Path $artifactRoot "phase-lmax-r67-endpoint-binding-decision.json")
$binding = Read-Json (Join-Path $artifactRoot "phase-lmax-r67-endpoint-binding-validation.json")
$placeholder = Read-Json (Join-Path $artifactRoot "phase-lmax-r67-placeholder-host-elimination-validation.json")
$production = Read-Json (Join-Path $artifactRoot "phase-lmax-r67-production-endpoint-exclusion-validation.json")
$path = Read-Json (Join-Path $artifactRoot "phase-lmax-r67-real-bounded-adapter-path-validation.json")
$noExternal = Read-Json (Join-Path $artifactRoot "phase-lmax-r67-no-external-boundary-attempted.json")
$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r67-forbidden-actions-audit.json")
$apiWorker = Read-Json (Join-Path $artifactRoot "phase-lmax-r67-api-worker-fake-gateway-audit.json")
$live = Read-Json (Join-Path $artifactRoot "phase-lmax-r67-no-live-launcher-audit.json")
$scheduler = Read-Json (Join-Path $artifactRoot "phase-lmax-r67-no-scheduler-polling-service-audit.json")
$cred = Read-Json (Join-Path $artifactRoot "phase-lmax-r67-credential-sanitization-validation.json")
$usdJpy = Read-Json (Join-Path $artifactRoot "phase-lmax-r67-usdjpy-caveat-preservation.json")
$gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r67-gate-validation.json")
$r66Gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r66-gate-validation.json")

if ($allowed -notcontains $summary.classification -or $allowed -notcontains $gate.classification) {
    Fail "R67 final classification is absent or not allowed."
}

if ($summary.classification -ne "LMAX_R67_PASS_DEMO_ENDPOINT_CONFIG_BINDING_READY_NO_EXTERNAL_ACTIVATION") {
    Fail "Unexpected R67 classification: $($summary.classification)"
}

if ($r66Gate.classification -ne "LMAX_R66_PASS_TCP_SOCKET_ROOT_CAUSE_ENDPOINT_CONFIG_INVALID_NO_ADVANCE_TO_TLS" -or $r66Gate.validatorResult -ne "PASS") {
    Fail "R66 endpoint config invalid root-cause evidence is missing."
}

foreach ($obj in @($summary, $binding)) {
    if ($obj.endpointMode -ne "Demo" -or -not $obj.endpointPresent -or -not $obj.hostPresent -or -not $obj.hostConcreteBinding -or $obj.hostWasPlaceholder -or -not $obj.portPresent -or -not $obj.portConcreteBinding -or -not $obj.productionExcluded -or -not $obj.endpointApproved) {
        Fail "Concrete approved Demo endpoint host/port binding is not provable."
    }
}

if (-not $beforeAfter.after.nextRetryShouldNotFailOnPlaceholderHost -or $beforeAfter.after.placeholderHostUsed) {
    Fail "R66 placeholder host root cause is not closed."
}

if (-not $decision.endpointApproved -or $decision.productionEndpointAllowed -or $decision.realEndpointDefaultGlobal -or -not $decision.noExternalBoundaryDefaultPreserved -or $decision.apiWorkerReachable -or $decision.rawEndpointSerializedInArtifacts) {
    Fail "Endpoint binding decision weakens safety or sanitization."
}

if (-not $placeholder.placeholderHostEliminatedForNextRetry -or $placeholder.connectorUsesSanitizedEndpointLabelAsTcpHost -or $placeholder.hostWasPlaceholder -or $placeholder.tcpStatusDuringR67 -ne "ValidationOnly") {
    Fail "Placeholder/sanitized endpoint label can still be used as TCP host."
}

if ($production.productionEndpointAllowed -or $production.productionAccountAllowed -or $production.productionAccountUsed -or $production.orderEndpointAllowed -or -not $production.productionExcluded) {
    Fail "Production endpoint/config risk detected."
}

if ($path.realBoundedAdapterDefaultGlobal -or -not $path.noExternalAdapterDefaultPreserved -or $path.apiWorkerReachable -or $path.externalActivationAttemptedDuringR67) {
    Fail "Real bounded adapter path became global/default or reachable unsafely."
}

if ($noExternal.externalActivationAttemptedDuringR67 -or $noExternal.attemptCount -ne 0 -or $noExternal.socketOpenedDuringR67 -or $noExternal.tlsAttemptedDuringR67 -or $noExternal.fixLogonAttemptedDuringR67 -or $noExternal.marketDataRequestAttemptedDuringR67) {
    Fail "External boundary was attempted during R67."
}

if ($noExternal.tcpSocket -ne "ValidationOnly" -or $noExternal.credentialConfig -ne "NotAttempted" -or $noExternal.tls -ne "NotAttempted" -or $noExternal.fixLogonSession -ne "NotAttempted" -or $noExternal.marketDataRequest -ne "NotAttempted") {
    Fail "Unexpected R67 boundary status."
}

if ($forbidden.result -ne "PASS") {
    Fail "Forbidden-action audit failed."
}

foreach ($flag in @("ordersSubmitted", "newOrderSingleSent", "cancelReplaceSent", "tradingEnablementExecuted", "tradingStateMutated", "productionAccountUsed", "productionAccountAllowed", "apiStarted", "workerStarted", "hostedServiceStarted", "backgroundServiceStarted", "schedulerStarted", "pollingLoopStarted", "replayExecuted", "shadowReplayExecuted", "shadowReplaySubmitted", "credentialValuesReturned", "credentialValuesPrintedStoredSerialized", "rawSensitiveFixLogsStored", "rawEndpointValuesPrintedStoredSerialized", "externalActivationAttempted", "socketOpened", "tlsAttempted", "fixLogonAttempted", "marketDataRequestAttempted")) {
    if ($forbidden.$flag) {
        Fail "Forbidden action flag is true: $flag"
    }
}

if ($apiWorker.result -ne "PASS" -or $apiWorker.apiWorkerGatewayMode -ne "FakeLmaxGatewayOnly" -or $apiWorker.manualExecutionSurfaceRegisteredInApi -or $apiWorker.manualExecutionSurfaceRegisteredInWorker -or $apiWorker.realBoundedAdapterRegisteredInApi -or $apiWorker.realBoundedAdapterRegisteredInWorker -or $apiWorker.endpointBindingRegisteredInApi -or $apiWorker.endpointBindingRegisteredInWorker -or $apiWorker.defaultRuntimeGatewayChanged) {
    Fail "API/Worker FakeLmaxGatewayOnly audit failed."
}

if ($live.genericLiveLauncherIntroduced -or $live.manualCliBecameDefaultStartup -or $live.appsettingsDefaultLiveEnablementIntroduced -or $live.realEndpointDefaultGlobal -or $live.apiWorkerStartupPathIntroduced) {
    Fail "Live launcher/default enablement risk detected."
}

if ($scheduler.hostedServiceIntroduced -or $scheduler.backgroundServiceIntroduced -or $scheduler.schedulerIntroduced -or $scheduler.pollingLoopIntroduced -or $scheduler.retryLoopIntroduced -or $scheduler.timerIntroduced) {
    Fail "Scheduler/polling/service risk detected."
}

if ($cred.credentialValuesRead -or $cred.credentialValuesReturned -or $cred.credentialValuesPrinted -or $cred.credentialValuesStored -or $cred.credentialValuesSerialized -or $cred.rawEndpointValuesPrintedStoredSerialized -or $cred.rawHostSerialized -or $cred.rawPortSerialized -or $cred.rawSensitiveFixLogsStored) {
    Fail "Credential or endpoint sanitization failed."
}

if (-not $usdJpy.caveatPreserved -or $usdJpy.securityId -ne "4004" -or $usdJpy.securityIdSource -ne "8" -or $usdJpy.caveat -ne "prior failed-safe root cause remains unproven") {
    Fail "USDJPY caveat is missing or weakened."
}

$bindingPath = Join-Path $Root "tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualDemoEndpointBinding.cs"
$connectorPath = Join-Path $Root "tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualTcpSocketConnector.cs"
$factoryPath = Join-Path $Root "tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualExecutionSurfaceFactory.cs"
foreach ($sourcePath in @($bindingPath, $connectorPath, $factoryPath)) {
    if (-not (Test-Path -LiteralPath $sourcePath)) {
        Fail "Required R67 source file is missing: $sourcePath"
    }
}

$bindingSource = Get-Content -LiteralPath $bindingPath -Raw
$connectorSource = Get-Content -LiteralPath $connectorPath -Raw
$factorySource = Get-Content -LiteralPath $factoryPath -Raw
if ($bindingSource -notmatch "LmaxReadOnlyActivationManualDemoEndpointBinding" -or $bindingSource -notmatch "london-demo\.lmax\.com" -or $bindingSource -notmatch "ProductionExcluded") {
    Fail "Concrete Demo endpoint binding source proof is missing."
}

if ($connectorSource -match "ConnectAsync\(options\.SanitizedEndpointLabel" -or $connectorSource -notmatch "ConnectAsync\(endpointBinding\.RuntimeHost" -or $connectorSource -notmatch "endpointBinding\.RuntimePort") {
    Fail "Connector still uses a sanitized endpoint label as TCP host or lacks concrete binding use."
}

if ($factorySource -notmatch "LmaxReadOnlyActivationManualDemoEndpointBinding\.CreateApprovedDemoMarketData\(\)" -or $factorySource -notmatch "new LmaxReadOnlyActivationManualTcpSocketConnector\(endpointBinding\)" -or $factorySource -notmatch "RealBoundedExecutableReadOnlyMode") {
    Fail "Factory does not bind the concrete Demo endpoint only for real-bounded manual mode."
}

$apiProgram = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/Program.cs") -Raw
$workerProgram = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Worker/Program.cs") -Raw
$apiSettings = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/appsettings.json") -Raw
foreach ($text in @($apiProgram, $workerProgram, $apiSettings)) {
    if ($text -match "LmaxReadOnlyActivationManualDemoEndpointBinding" -or $text -match "LmaxReadOnlyActivationManualTcpSocketConnector" -or $text -match "real-bounded-executable-readonly") {
        Fail "Manual endpoint binding or real adapter mode leaked into API/Worker/default startup."
    }
}

if ($apiProgram -notmatch "FakeLmaxGateway") {
    Fail "API/Worker FakeLmaxGatewayOnly proof is missing."
}

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r67-*" |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$sensitivePatterns = @(
    "fix-marketdata\.london-demo\.lmax\.com",
    "fix-order\.london-demo\.lmax\.com",
    "password\s*[:=]",
    "passwd\s*[:=]",
    "token\s*[:=]",
    "554=",
    "-----BEGIN"
)
foreach ($pattern in $sensitivePatterns) {
    if ($artifactText -match $pattern) {
        Fail "Potential raw endpoint, credential, or sensitive FIX material found in R67 artifacts: $pattern"
    }
}

if ($gate.buildResult.status -ne "PASS" -or $gate.testResult.status -ne "PASS") {
    Fail "Build/test evidence is missing or not PASS."
}

Write-Host "LMAX R67 Demo endpoint config binding validation PASS"
