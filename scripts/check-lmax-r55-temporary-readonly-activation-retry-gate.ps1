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
    "phase-lmax-r55-temporary-readonly-activation-retry-report.md",
    "phase-lmax-r55-temporary-readonly-activation-retry-summary.json",
    "phase-lmax-r55-operator-approval-note.md",
    "phase-lmax-r55-preflight-result.json",
    "phase-lmax-r55-preflight-trace.json",
    "phase-lmax-r55-boundary-evidence.json",
    "phase-lmax-r55-marketdata-sanitized-result.json",
    "phase-lmax-r55-forbidden-actions-audit.json",
    "phase-lmax-r55-api-worker-fake-gateway-audit.json",
    "phase-lmax-r55-usdjpy-caveat-preservation.json",
    "phase-lmax-r55-shutdown-revert-evidence.json",
    "phase-lmax-r55-next-phase-recommendation.json",
    "phase-lmax-r55-gate-validation.json"
)

foreach ($name in $required) {
    $path = Join-Path $artifactRoot $name
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing required R55 artifact: $name"
    }
}

$allowedClassifications = @(
    "LMAX_R55_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED",
    "LMAX_R55_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED",
    "LMAX_R55_FAIL_R54_SUCCESS_EVIDENCE_MISSING",
    "LMAX_R55_FAIL_PRE_EXTERNAL_APPROVAL_OR_COMPOSITION_REGRESSION",
    "LMAX_R55_FAIL_CREDENTIAL_CONFIG_MISSING",
    "LMAX_R55_FAIL_CREDENTIAL_CONFIG_NOT_APPROVED",
    "LMAX_R55_FAIL_CREDENTIAL_CONFIG_BINDING_REGRESSION",
    "LMAX_R55_FAIL_PRODUCTION_ACCOUNT_RISK",
    "LMAX_R55_FAIL_API_WORKER_GATEWAY_REGRESSION",
    "LMAX_R55_FAIL_LIVE_LAUNCHER_OR_BACKGROUND_SERVICE_RISK",
    "LMAX_R55_FAIL_FORBIDDEN_ACTION_RISK",
    "LMAX_R55_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R55_FAIL_PREFLIGHT_ABORTED",
    "LMAX_R55_FAIL_CREDENTIAL_CONFIG_BOUNDARY",
    "LMAX_R55_FAIL_TCP_SOCKET_BOUNDARY",
    "LMAX_R55_FAIL_TLS_BOUNDARY",
    "LMAX_R55_FAIL_FIX_LOGON_OR_SESSION_BOUNDARY",
    "LMAX_R55_FAIL_MARKETDATA_REQUEST_BOUNDARY",
    "LMAX_R55_FAIL_MARKETDATA_RESPONSE_BOUNDARY",
    "LMAX_R55_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE",
    "LMAX_R55_FAIL_SHUTDOWN_REVERT_INCOMPLETE",
    "LMAX_R55_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK",
    "LMAX_R55_FAIL_BUILD_OR_TESTS"
)

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r55-temporary-readonly-activation-retry-summary.json")
$preflight = Read-Json (Join-Path $artifactRoot "phase-lmax-r55-preflight-result.json")
$trace = Read-Json (Join-Path $artifactRoot "phase-lmax-r55-preflight-trace.json")
$boundary = Read-Json (Join-Path $artifactRoot "phase-lmax-r55-boundary-evidence.json")
$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r55-forbidden-actions-audit.json")
$apiWorker = Read-Json (Join-Path $artifactRoot "phase-lmax-r55-api-worker-fake-gateway-audit.json")
$usdJpy = Read-Json (Join-Path $artifactRoot "phase-lmax-r55-usdjpy-caveat-preservation.json")
$gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r55-gate-validation.json")
$r54Gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r54-gate-validation.json")

if ($allowedClassifications -notcontains $summary.classification) {
    Fail "R55 classification is absent or not allowed: $($summary.classification)"
}

if ($r54Gate.classification -ne "LMAX_R54_PASS_RETRY_PHASE_RESERVATION_RULE_FIXED_NO_EXTERNAL_ACTIVATION" -or -not $r54Gate.passed) {
    Fail "R54 success evidence is missing or not passing."
}

$expectedApproval = "I, Philippe, explicitly approve Phase LMAX-R55 for one temporary Demo read-only runtime market-data activation retry after the R54 retry phase reservation rule fix for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority."
$approvalText = Get-Content -LiteralPath (Join-Path $artifactRoot "phase-lmax-r55-operator-approval-note.md") -Raw
if (-not $approvalText.Contains($expectedApproval)) {
    Fail "Exact R55 operator approval text is missing or mismatched."
}

if (-not $summary.retryPhaseReservationAcceptedR55) {
    Fail "R55 retry phase reservation is not accepted."
}

$reservationSource = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxApprovedBoundedExecutableRetryPhaseReservations.cs") -Raw
foreach ($needle in @("MinimumRetryPhaseNumber = 43", "MaximumRetryPhaseNumber = 99", "IsOdd(number)", "StartsWith(prefix, StringComparison.Ordinal)", "numeric[0] == '0'")) {
    if (-not $reservationSource.Contains($needle)) {
        Fail "Retry phase reservation rule source is missing expected guard: $needle"
    }
}

if ($summary.externalActivationAttempted -or $summary.attemptCount -ne 0) {
    Fail "R55 must not report an external activation attempt for this safe abort."
}

foreach ($key in @("credentialConfig", "tcpSocket", "tls", "fixLogonSession", "marketDataRequest", "marketDataResponseEntries")) {
    if ($boundary.boundaries.$key -ne "NotAttempted") {
        Fail "Boundary $key must be NotAttempted; actual: $($boundary.boundaries.$key)"
    }
}

if ($boundary.boundaries.shutdownRevert -ne "Succeeded") {
    Fail "Shutdown/revert evidence must be Succeeded."
}

if (-not $trace.gates.Where({ $_.result -eq "FAIL" -and $_.blocker -eq "NoApprovedR55BoundedRuntimeActivationInvocationPath" })) {
    Fail "Full R55 preflight trace is missing the concrete invocation-path blocker."
}

if ($forbidden.result -ne "PASS") {
    Fail "Forbidden action audit did not pass."
}

foreach ($flag in @("orderSubmissionExecuted", "newOrderSingleSent", "cancelReplaceSent", "tradingEnablementExecuted", "tradingStateMutated", "productionAccountUsed", "apiStarted", "workerStarted", "hostedServiceStarted", "backgroundServiceStarted", "schedulerStarted", "pollingLoopStarted", "replayExecuted", "shadowReplayExecuted", "shadowReplaySubmitted", "nonApprovedInstrumentTouched", "credentialValuesPrintedStoredSerialized", "rawSensitiveFixLogsStored", "defaultGatewayRegistrationChanged", "runtimeEnablementPersisted")) {
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

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r55-*" |
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
        Fail "Potential raw credential or sensitive FIX material found in R55 artifacts: $pattern"
    }
}

if ($gate.buildResult.status -ne "PASS") {
    Fail "Build evidence is missing or not PASS."
}

if ($gate.testResult.status -ne "PASS") {
    Fail "Test evidence is missing or not PASS."
}

Write-Host "LMAX R55 temporary read-only activation retry gate validation PASS"
