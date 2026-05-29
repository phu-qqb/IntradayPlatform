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
    "phase-lmax-r57-temporary-readonly-activation-retry-report.md",
    "phase-lmax-r57-temporary-readonly-activation-retry-summary.json",
    "phase-lmax-r57-operator-approval-note.md",
    "phase-lmax-r57-preflight-result.json",
    "phase-lmax-r57-preflight-trace.json",
    "phase-lmax-r57-boundary-evidence.json",
    "phase-lmax-r57-marketdata-sanitized-result.json",
    "phase-lmax-r57-forbidden-actions-audit.json",
    "phase-lmax-r57-api-worker-fake-gateway-audit.json",
    "phase-lmax-r57-usdjpy-caveat-preservation.json",
    "phase-lmax-r57-shutdown-revert-evidence.json",
    "phase-lmax-r57-next-phase-recommendation.json",
    "phase-lmax-r57-gate-validation.json"
)

foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $name))) {
        Fail "Missing required R57 artifact: $name"
    }
}

$allowed = @(
    "LMAX_R57_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED",
    "LMAX_R57_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED",
    "LMAX_R57_FAIL_R56_SUCCESS_EVIDENCE_MISSING",
    "LMAX_R57_FAIL_INVOCATION_PATH_REGRESSION",
    "LMAX_R57_FAIL_PRE_EXTERNAL_APPROVAL_OR_COMPOSITION_REGRESSION",
    "LMAX_R57_FAIL_CREDENTIAL_CONFIG_MISSING",
    "LMAX_R57_FAIL_CREDENTIAL_CONFIG_NOT_APPROVED",
    "LMAX_R57_FAIL_CREDENTIAL_CONFIG_BINDING_REGRESSION",
    "LMAX_R57_FAIL_PRODUCTION_ACCOUNT_RISK",
    "LMAX_R57_FAIL_API_WORKER_GATEWAY_REGRESSION",
    "LMAX_R57_FAIL_LIVE_LAUNCHER_OR_BACKGROUND_SERVICE_RISK",
    "LMAX_R57_FAIL_FORBIDDEN_ACTION_RISK",
    "LMAX_R57_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R57_FAIL_PREFLIGHT_ABORTED",
    "LMAX_R57_FAIL_CREDENTIAL_CONFIG_BOUNDARY",
    "LMAX_R57_FAIL_TCP_SOCKET_BOUNDARY",
    "LMAX_R57_FAIL_TLS_BOUNDARY",
    "LMAX_R57_FAIL_FIX_LOGON_OR_SESSION_BOUNDARY",
    "LMAX_R57_FAIL_MARKETDATA_REQUEST_BOUNDARY",
    "LMAX_R57_FAIL_MARKETDATA_RESPONSE_BOUNDARY",
    "LMAX_R57_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE",
    "LMAX_R57_FAIL_SHUTDOWN_REVERT_INCOMPLETE",
    "LMAX_R57_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK",
    "LMAX_R57_FAIL_BUILD_OR_TESTS"
)

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r57-temporary-readonly-activation-retry-summary.json")
$trace = Read-Json (Join-Path $artifactRoot "phase-lmax-r57-preflight-trace.json")
$boundary = Read-Json (Join-Path $artifactRoot "phase-lmax-r57-boundary-evidence.json")
$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r57-forbidden-actions-audit.json")
$apiWorker = Read-Json (Join-Path $artifactRoot "phase-lmax-r57-api-worker-fake-gateway-audit.json")
$usdJpy = Read-Json (Join-Path $artifactRoot "phase-lmax-r57-usdjpy-caveat-preservation.json")
$gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r57-gate-validation.json")
$r56Gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r56-gate-validation.json")

if ($allowed -notcontains $summary.classification) {
    Fail "R57 classification is absent or not allowed: $($summary.classification)"
}

if ($r56Gate.classification -ne "LMAX_R56_PASS_APPROVED_BOUNDED_RUNTIME_ACTIVATION_INVOCATION_PATH_READY_NO_EXTERNAL_ACTIVATION" -or -not $r56Gate.passed) {
    Fail "R56 success evidence is missing or not passing."
}

$expectedApproval = "I, Philippe, explicitly approve Phase LMAX-R57 for one temporary Demo read-only runtime market-data activation retry after the R56 approved bounded invocation path for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority."
$approvalText = Get-Content -LiteralPath (Join-Path $artifactRoot "phase-lmax-r57-operator-approval-note.md") -Raw
if (-not $approvalText.Contains($expectedApproval)) {
    Fail "Exact R57 operator approval text is missing or mismatched."
}

$reservationSource = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxApprovedBoundedExecutableRetryPhaseReservations.cs") -Raw
foreach ($needle in @("MinimumRetryPhaseNumber = 43", "MaximumRetryPhaseNumber = 99", "IsOdd(number)", "StartsWith(prefix, StringComparison.Ordinal)")) {
    if (-not $reservationSource.Contains($needle)) {
        Fail "Retry phase reservation rule source is missing expected guard: $needle"
    }
}

$invocationSource = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxBoundedReadOnlyActivationInvocationPath.cs") -Raw
foreach ($needle in @("LmaxBoundedReadOnlyActivationInvocationPath", "InvokeOnce", "LmaxTemporaryReadOnlyActivationExecutor", "ExecuteOnce", "ExactPerPhaseOperatorApprovalMissing")) {
    if (-not $invocationSource.Contains($needle)) {
        Fail "R56 invocation path source missing expected proof: $needle"
    }
}

if ($summary.externalActivationAttempted -or $summary.attemptCount -ne 0) {
    Fail "R57 must not report external activation when safe-aborted before caller availability."
}

if ($summary.executeOnceInvoked -or $boundary.executeOnceInvoked) {
    Fail "ExecuteOnce must not be invoked in this safe pre-external abort."
}

foreach ($key in @("credentialConfig", "tcpSocket", "tls", "fixLogonSession", "marketDataRequest", "marketDataResponseEntries")) {
    if ($boundary.boundaries.$key -ne "NotAttempted") {
        Fail "Boundary $key must be NotAttempted; actual: $($boundary.boundaries.$key)"
    }
}

if ($boundary.boundaries.shutdownRevert -ne "Succeeded") {
    Fail "Shutdown/revert evidence must be Succeeded."
}

if (-not $trace.gates.Where({ $_.result -eq "FAIL" -and $_.blocker -eq "NoApprovedR57OperationalCallerForBoundedInvocationPath" })) {
    Fail "Full R57 preflight trace is missing the concrete operational-caller blocker."
}

if ($forbidden.result -ne "PASS") {
    Fail "Forbidden action audit did not pass."
}

foreach ($flag in @("orderSubmissionExecuted", "newOrderSingleSent", "cancelReplaceSent", "tradingEnablementExecuted", "tradingStateMutated", "productionAccountUsed", "apiStarted", "workerStarted", "hostedServiceStarted", "backgroundServiceStarted", "schedulerStarted", "pollingLoopStarted", "replayExecuted", "shadowReplayExecuted", "shadowReplaySubmitted", "nonApprovedInstrumentTouched", "credentialValuesRead", "credentialValuesReturned", "credentialValuesPrintedStoredSerialized", "rawSensitiveFixLogsStored", "defaultGatewayRegistrationChanged", "runtimeEnablementPersisted")) {
    if ($forbidden.$flag) {
        Fail "Forbidden action flag is true: $flag"
    }
}

if ($apiWorker.result -ne "PASS" -or $apiWorker.apiWorkerGatewayMode -ne "FakeLmaxGatewayOnly" -or $apiWorker.defaultRuntimeGatewayChanged) {
    Fail "API/Worker FakeLmaxGatewayOnly audit failed."
}

if (-not $usdJpy.caveatPreserved -or $usdJpy.securityId -ne "4004" -or $usdJpy.securityIdSource -ne "8" -or $usdJpy.caveat -ne "prior failed-safe root cause remains unproven") {
    Fail "USDJPY caveat evidence is missing or weakened."
}

if ($summary.credentialValuesReturned -or $boundary.credentialValuesReturned) {
    Fail "credentialValuesReturned must remain false."
}

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r57-*" |
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
        Fail "Potential raw credential or sensitive FIX material found in R57 artifacts: $pattern"
    }
}

if ($gate.buildResult.status -ne "PASS" -or $gate.testResult.status -ne "PASS") {
    Fail "Build/test evidence is missing or not PASS."
}

Write-Host "LMAX R57 temporary read-only activation retry gate validation PASS"
