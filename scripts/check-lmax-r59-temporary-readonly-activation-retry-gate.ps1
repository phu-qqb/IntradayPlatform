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
    "phase-lmax-r59-temporary-readonly-activation-retry-report.md",
    "phase-lmax-r59-temporary-readonly-activation-retry-summary.json",
    "phase-lmax-r59-operator-approval-note.md",
    "phase-lmax-r59-preflight-result.json",
    "phase-lmax-r59-preflight-trace.json",
    "phase-lmax-r59-operational-caller-evidence.json",
    "phase-lmax-r59-boundary-evidence.json",
    "phase-lmax-r59-marketdata-sanitized-result.json",
    "phase-lmax-r59-forbidden-actions-audit.json",
    "phase-lmax-r59-api-worker-fake-gateway-audit.json",
    "phase-lmax-r59-usdjpy-caveat-preservation.json",
    "phase-lmax-r59-shutdown-revert-evidence.json",
    "phase-lmax-r59-next-phase-recommendation.json",
    "phase-lmax-r59-gate-validation.json"
)

foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $name))) {
        Fail "Missing required R59 artifact: $name"
    }
}

$allowed = @(
    "LMAX_R59_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED",
    "LMAX_R59_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED",
    "LMAX_R59_FAIL_R58_SUCCESS_EVIDENCE_MISSING",
    "LMAX_R59_FAIL_OPERATIONAL_CALLER_REGRESSION",
    "LMAX_R59_FAIL_INVOCATION_PATH_REGRESSION",
    "LMAX_R59_FAIL_EXECUTE_ONCE_NOT_INVOKED",
    "LMAX_R59_FAIL_PRE_EXTERNAL_APPROVAL_OR_COMPOSITION_REGRESSION",
    "LMAX_R59_FAIL_CREDENTIAL_CONFIG_MISSING",
    "LMAX_R59_FAIL_CREDENTIAL_CONFIG_NOT_APPROVED",
    "LMAX_R59_FAIL_CREDENTIAL_CONFIG_BINDING_REGRESSION",
    "LMAX_R59_FAIL_PRODUCTION_ACCOUNT_RISK",
    "LMAX_R59_FAIL_API_WORKER_GATEWAY_REGRESSION",
    "LMAX_R59_FAIL_LIVE_LAUNCHER_OR_BACKGROUND_SERVICE_RISK",
    "LMAX_R59_FAIL_FORBIDDEN_ACTION_RISK",
    "LMAX_R59_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R59_FAIL_PREFLIGHT_ABORTED",
    "LMAX_R59_FAIL_CREDENTIAL_CONFIG_BOUNDARY",
    "LMAX_R59_FAIL_TCP_SOCKET_BOUNDARY",
    "LMAX_R59_FAIL_TLS_BOUNDARY",
    "LMAX_R59_FAIL_FIX_LOGON_OR_SESSION_BOUNDARY",
    "LMAX_R59_FAIL_MARKETDATA_REQUEST_BOUNDARY",
    "LMAX_R59_FAIL_MARKETDATA_RESPONSE_BOUNDARY",
    "LMAX_R59_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE",
    "LMAX_R59_FAIL_SHUTDOWN_REVERT_INCOMPLETE",
    "LMAX_R59_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK",
    "LMAX_R59_FAIL_BUILD_OR_TESTS"
)

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r59-temporary-readonly-activation-retry-summary.json")
$preflight = Read-Json (Join-Path $artifactRoot "phase-lmax-r59-preflight-result.json")
$trace = Read-Json (Join-Path $artifactRoot "phase-lmax-r59-preflight-trace.json")
$caller = Read-Json (Join-Path $artifactRoot "phase-lmax-r59-operational-caller-evidence.json")
$boundary = Read-Json (Join-Path $artifactRoot "phase-lmax-r59-boundary-evidence.json")
$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r59-forbidden-actions-audit.json")
$apiWorker = Read-Json (Join-Path $artifactRoot "phase-lmax-r59-api-worker-fake-gateway-audit.json")
$usdJpy = Read-Json (Join-Path $artifactRoot "phase-lmax-r59-usdjpy-caveat-preservation.json")
$shutdown = Read-Json (Join-Path $artifactRoot "phase-lmax-r59-shutdown-revert-evidence.json")
$gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r59-gate-validation.json")
$r58Gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r58-gate-validation.json")

if ($allowed -notcontains $summary.classification) {
    Fail "R59 classification is absent or not allowed: $($summary.classification)"
}

if ($summary.classification -ne $gate.classification) {
    Fail "R59 summary/gate classification mismatch."
}

if ($r58Gate.classification -ne "LMAX_R58_PASS_APPROVED_OPERATIONAL_CALLER_READY_NO_EXTERNAL_ACTIVATION" -or -not $r58Gate.passed) {
    Fail "R58 success evidence is missing or not passing."
}

$expectedApproval = "I, Philippe, explicitly approve Phase LMAX-R59 for one temporary Demo read-only runtime market-data activation retry after the R58 approved operational caller for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority."
$approvalText = Get-Content -LiteralPath (Join-Path $artifactRoot "phase-lmax-r59-operator-approval-note.md") -Raw
if (-not $approvalText.Contains($expectedApproval)) {
    Fail "Exact R59 operator approval text is missing or mismatched."
}

$callerSource = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxManualBoundedReadOnlyActivationCaller.cs") -Raw
$invocationSource = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxBoundedReadOnlyActivationInvocationPath.cs") -Raw
foreach ($needle in @("LmaxManualBoundedReadOnlyActivationCaller", "CallOnce", "LmaxBoundedReadOnlyActivationInvocationPath", "InvokeOnce", "LmaxTemporaryReadOnlyActivationExecutor.ExecuteOnce")) {
    if (-not ($callerSource.Contains($needle) -or $invocationSource.Contains($needle))) {
        Fail "Expected caller/invocation source proof missing: $needle"
    }
}

if (-not $caller.callerExists -or -not $caller.callerValidationPassed) {
    Fail "Operational caller evidence is missing or not valid."
}

if (-not $caller.invocationPathCallsExecuteOnce) {
    Fail "Invocation path does not prove ExecuteOnce target."
}

if ($summary.classification -eq "LMAX_R59_FAIL_EXECUTE_ONCE_NOT_INVOKED") {
    if ($summary.attemptCount -ne 0 -or $summary.externalActivationAttempted -or $summary.executeOnceInvoked -or $caller.callOnceInvoked -or $caller.invokeOnceUsed) {
        Fail "ExecuteOnce-not-invoked safe abort has inconsistent execution flags."
    }

    if (-not $trace.gates.Where({ $_.result -eq "FAIL" -and $_.blocker -eq "ExecuteOnceNotInvokedByApprovedOperationalCallerInR59" })) {
        Fail "R59 full trace is missing the ExecuteOnce-not-invoked blocker."
    }
}

foreach ($key in @("credentialConfig", "tcpSocket", "tls", "fixLogonSession", "marketDataRequest", "marketDataResponseEntries")) {
    if ($boundary.boundaries.$key -ne "NotAttempted") {
        Fail "Boundary $key must be NotAttempted for this R59 safe abort; actual: $($boundary.boundaries.$key)"
    }
}

if ($boundary.boundaries.shutdownRevert -ne "Succeeded") {
    Fail "Shutdown/revert evidence must be Succeeded."
}

if ($boundary.credentialValuesReturned -or $summary.credentialValuesReturned) {
    Fail "credentialValuesReturned must remain false."
}

foreach ($flag in @("orderSubmissionExecuted", "newOrderSingleSent", "cancelReplaceSent", "tradingEnablementExecuted", "tradingStateMutated", "productionAccountUsed", "productionAccountAllowed", "apiStarted", "workerStarted", "hostedServiceStarted", "backgroundServiceStarted", "schedulerStarted", "pollingLoopStarted", "replayExecuted", "shadowReplayExecuted", "shadowReplaySubmitted", "nonApprovedInstrumentTouched", "credentialValuesRead", "credentialValuesReturned", "credentialValuesPrintedStoredSerialized", "rawSensitiveFixLogsStored", "defaultGatewayRegistrationChanged", "runtimeEnablementPersisted")) {
    if ($forbidden.$flag) {
        Fail "Forbidden action flag is true: $flag"
    }
}

if ($apiWorker.result -ne "PASS" -or $apiWorker.apiWorkerGatewayMode -ne "FakeLmaxGatewayOnly" -or $apiWorker.apiStarted -or $apiWorker.workerStarted -or $apiWorker.operationalCallerRegisteredInApi -or $apiWorker.operationalCallerRegisteredInWorker -or $apiWorker.appsettingsLiveEnablementIntroduced -or $apiWorker.defaultRuntimeGatewayChanged) {
    Fail "API/Worker FakeLmaxGatewayOnly audit failed."
}

if (-not $usdJpy.caveatPreserved -or $usdJpy.securityId -ne "4004" -or $usdJpy.securityIdSource -ne "8" -or $usdJpy.caveat -ne "prior failed-safe root cause remains unproven") {
    Fail "USDJPY caveat evidence is missing or weakened."
}

if ($shutdown.result -ne "PASS" -or $shutdown.runtimeEnablementPersisted -or $shutdown.defaultGatewayRegistrationChanged -or $shutdown.apiWorkerStarted) {
    Fail "Shutdown/revert or non-mutation evidence failed."
}

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r59-*" |
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
        Fail "Potential raw credential or sensitive FIX material found in R59 artifacts: $pattern"
    }
}

if ($gate.buildResult.status -ne "PASS" -or $gate.testResult.status -ne "PASS") {
    Fail "Build/test evidence is missing or not PASS."
}

Write-Host "LMAX R59 temporary read-only activation retry gate validation PASS"
