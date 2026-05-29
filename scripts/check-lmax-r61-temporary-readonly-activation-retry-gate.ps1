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
    "phase-lmax-r61-temporary-readonly-activation-retry-report.md",
    "phase-lmax-r61-temporary-readonly-activation-retry-summary.json",
    "phase-lmax-r61-operator-approval-note.md",
    "phase-lmax-r61-preflight-result.json",
    "phase-lmax-r61-manual-execution-surface-evidence.json",
    "phase-lmax-r61-operational-invocation-trace.json",
    "phase-lmax-r61-boundary-evidence.json",
    "phase-lmax-r61-marketdata-sanitized-result.json",
    "phase-lmax-r61-forbidden-actions-audit.json",
    "phase-lmax-r61-api-worker-fake-gateway-audit.json",
    "phase-lmax-r61-usdjpy-caveat-preservation.json",
    "phase-lmax-r61-shutdown-revert-evidence.json",
    "phase-lmax-r61-next-phase-recommendation.json",
    "phase-lmax-r61-gate-validation.json"
)

foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $name))) {
        Fail "Missing required R61 artifact: $name"
    }
}

$allowed = @(
    "LMAX_R61_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED",
    "LMAX_R61_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED",
    "LMAX_R61_FAIL_R60_SUCCESS_EVIDENCE_MISSING",
    "LMAX_R61_FAIL_MANUAL_EXECUTION_SURFACE_REGRESSION",
    "LMAX_R61_FAIL_OPERATIONAL_CALLER_REGRESSION",
    "LMAX_R61_FAIL_INVOCATION_PATH_REGRESSION",
    "LMAX_R61_FAIL_EXECUTE_ONCE_NOT_INVOKED",
    "LMAX_R61_FAIL_PRE_EXTERNAL_APPROVAL_OR_COMPOSITION_REGRESSION",
    "LMAX_R61_FAIL_CREDENTIAL_CONFIG_MISSING",
    "LMAX_R61_FAIL_CREDENTIAL_CONFIG_NOT_APPROVED",
    "LMAX_R61_FAIL_CREDENTIAL_CONFIG_BINDING_REGRESSION",
    "LMAX_R61_FAIL_PRODUCTION_ACCOUNT_RISK",
    "LMAX_R61_FAIL_API_WORKER_GATEWAY_REGRESSION",
    "LMAX_R61_FAIL_LIVE_LAUNCHER_OR_BACKGROUND_SERVICE_RISK",
    "LMAX_R61_FAIL_FORBIDDEN_ACTION_RISK",
    "LMAX_R61_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R61_FAIL_PREFLIGHT_ABORTED",
    "LMAX_R61_FAIL_CREDENTIAL_CONFIG_BOUNDARY",
    "LMAX_R61_FAIL_TCP_SOCKET_BOUNDARY",
    "LMAX_R61_FAIL_TLS_BOUNDARY",
    "LMAX_R61_FAIL_FIX_LOGON_OR_SESSION_BOUNDARY",
    "LMAX_R61_FAIL_MARKETDATA_REQUEST_BOUNDARY",
    "LMAX_R61_FAIL_MARKETDATA_RESPONSE_BOUNDARY",
    "LMAX_R61_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE",
    "LMAX_R61_FAIL_SHUTDOWN_REVERT_INCOMPLETE",
    "LMAX_R61_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK",
    "LMAX_R61_FAIL_BUILD_OR_TESTS"
)

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r61-temporary-readonly-activation-retry-summary.json")
$preflight = Read-Json (Join-Path $artifactRoot "phase-lmax-r61-preflight-result.json")
$surface = Read-Json (Join-Path $artifactRoot "phase-lmax-r61-manual-execution-surface-evidence.json")
$trace = Read-Json (Join-Path $artifactRoot "phase-lmax-r61-operational-invocation-trace.json")
$boundary = Read-Json (Join-Path $artifactRoot "phase-lmax-r61-boundary-evidence.json")
$marketData = Read-Json (Join-Path $artifactRoot "phase-lmax-r61-marketdata-sanitized-result.json")
$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r61-forbidden-actions-audit.json")
$apiWorker = Read-Json (Join-Path $artifactRoot "phase-lmax-r61-api-worker-fake-gateway-audit.json")
$usdJpy = Read-Json (Join-Path $artifactRoot "phase-lmax-r61-usdjpy-caveat-preservation.json")
$shutdown = Read-Json (Join-Path $artifactRoot "phase-lmax-r61-shutdown-revert-evidence.json")
$gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r61-gate-validation.json")
$r60Gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r60-gate-validation.json")
$approvalText = Get-Content -LiteralPath (Join-Path $artifactRoot "phase-lmax-r61-operator-approval-note.md") -Raw

if ($allowed -notcontains $summary.classification -or $allowed -notcontains $gate.classification) {
    Fail "R61 classification is absent or not allowed."
}

if ($summary.classification -ne "LMAX_R61_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE" -or $gate.classification -ne $summary.classification) {
    Fail "Unexpected R61 classification: $($summary.classification)"
}

if ($r60Gate.classification -ne "LMAX_R60_PASS_APPROVED_MANUAL_ACTIVATION_EXECUTION_SURFACE_READY_NO_EXTERNAL_ACTIVATION" -or -not $r60Gate.passed) {
    Fail "R60 success evidence is missing or not passing."
}

$expectedApproval = "I, Philippe, explicitly approve Phase LMAX-R61 for one temporary Demo read-only runtime market-data activation retry through the R60 approved manual execution surface for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority."
if (-not $approvalText.Contains($expectedApproval)) {
    Fail "Exact R61 operator approval text missing or mismatched."
}

if (-not $summary.operatorApprovalExactTextPresent -or -not $preflight.operatorApprovalExactTextPresent -or -not $gate.operatorApprovalExactTextPresent) {
    Fail "Operator approval evidence is not marked present."
}

foreach ($flag in @("manualExecutionSurfaceUsed", "lmaxManualBoundedReadOnlyActivationCallerCallOnceInvoked", "lmaxBoundedReadOnlyActivationInvocationPathInvokeOnceInvoked", "lmaxTemporaryReadOnlyActivationExecutorExecuteOnceInvoked")) {
    if (-not $summary.$flag) {
        Fail "Summary invocation flag is false: $flag"
    }
}

foreach ($flag in @("toolUsed", "callOnceInvoked", "invokeOnceInvoked", "executeOnceInvoked")) {
    if (-not $surface.$flag) {
        Fail "Manual execution surface evidence flag is false: $flag"
    }
}

if ($summary.attemptCount -ne 1 -or $surface.attemptCount -ne 1 -or $gate.attemptCount -ne 1 -or $trace.attemptCount -ne 1) {
    Fail "R61 must record exactly one bounded ExecuteOnce attempt."
}

if ($trace.executeOnceNotInvoked -or -not $trace.callOnceInvoked -or -not $trace.invokeOnceInvoked -or -not $trace.executeOnceInvoked) {
    Fail "Operational invocation trace does not prove CallOnce -> InvokeOnce -> ExecuteOnce."
}

if ($preflight.executeOnceInvoked -ne $true -or $preflight.runtimeBoundaryInconclusive -ne $true) {
    Fail "Preflight/result evidence must show ExecuteOnce invoked and runtime boundary inconclusive."
}

$toolProject = Join-Path $Root "tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation.csproj"
$programPath = Join-Path $Root "tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/Program.cs"
$surfacePath = Join-Path $Root "tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualExecutionSurface.cs"
foreach ($path in @($toolProject, $programPath, $surfacePath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Manual execution surface source missing: $path"
    }
}

$program = Get-Content -LiteralPath $programPath -Raw
$surfaceSource = Get-Content -LiteralPath $surfacePath -Raw
foreach ($needle in @("LmaxManualBoundedReadOnlyActivationCaller", "CallOnce", "InvokeOnce", "ExecuteOnce", "--execute-once", "--manual-confirm", "--approval-file", "--expected-approval-file")) {
    if (-not ("$program`n$surfaceSource").Contains($needle)) {
        Fail "Manual execution surface source missing expected proof: $needle"
    }
}

if ($summary.credentialValuesReturned -or $boundary.credentialValuesReturned -or $marketData.credentialValuesReturned -or $gate.credentialValuesReturned) {
    Fail "credentialValuesReturned must remain false."
}

foreach ($key in @("credentialConfig", "tcpSocket", "tls", "fixLogonSession", "marketDataRequest", "marketDataResponseEntries")) {
    if ($boundary.boundaryStatuses.$key -ne "NotAttempted" -or $summary.boundaryStatuses.$key -ne "NotAttempted" -or $gate.boundaryStatuses.$key -ne "NotAttempted") {
        Fail "Boundary $key must be NotAttempted for this inconclusive R61 result."
    }
}

if ($boundary.boundaryStatuses.shutdownRevert -ne "Succeeded" -or -not $shutdown.shutdownRevertCompleted) {
    Fail "Shutdown/revert evidence is missing or incomplete."
}

foreach ($flag in @("orderSubmissionExecuted", "newOrderSingleSent", "cancelReplaceSent", "tradingEnablementExecuted", "tradingStateMutated", "productionAccountUsed", "productionAccountAllowed", "apiStarted", "workerStarted", "hostedServiceStarted", "backgroundServiceStarted", "schedulerStarted", "pollingLoopStarted", "replayExecuted", "shadowReplayExecuted", "shadowReplaySubmitted", "nonApprovedInstrumentTouched", "credentialValuesRead", "credentialValuesReturned", "credentialValuesPrintedStoredSerialized", "rawSensitiveFixLogsStored", "defaultGatewayRegistrationChanged", "runtimeEnablementPersisted")) {
    if ($forbidden.$flag) {
        Fail "Forbidden action flag is true: $flag"
    }
}

if ($apiWorker.result -ne "PASS" -or $apiWorker.apiWorkerGatewayMode -ne "FakeLmaxGatewayOnly" -or $apiWorker.manualExecutionSurfaceRegisteredInApi -or $apiWorker.manualExecutionSurfaceRegisteredInWorker -or $apiWorker.appsettingsLiveEnablementIntroduced -or $apiWorker.defaultRuntimeGatewayChanged) {
    Fail "API/Worker FakeLmaxGatewayOnly audit failed."
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
    Fail "API FakeLmaxGateway evidence missing."
}

if (-not $usdJpy.caveatPreserved -or $usdJpy.securityId -ne "4004" -or $usdJpy.securityIdSource -ne "8" -or $usdJpy.caveat -ne "prior failed-safe root cause remains unproven") {
    Fail "USDJPY caveat evidence is missing or weakened."
}

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r61-*" |
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
        Fail "Potential raw credential or sensitive FIX material found in R61 artifacts: $pattern"
    }
}

if ($gate.buildResult.status -ne "PASS" -or $gate.testResult.status -ne "PASS") {
    Fail "Build/test evidence is missing or not PASS."
}

Write-Host "LMAX R61 temporary read-only activation retry gate validation PASS"
