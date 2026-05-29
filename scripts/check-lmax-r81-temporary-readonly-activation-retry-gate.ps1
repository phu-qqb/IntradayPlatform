$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root "artifacts\readiness\lmax-runtime-enablement"

function Read-Json($path) {
    if (-not (Test-Path $path)) {
        throw "Missing required artifact: $path"
    }

    Get-Content $path -Raw | ConvertFrom-Json
}

function Assert-True($condition, $message) {
    if (-not $condition) {
        throw $message
    }
}

function Assert-False($condition, $message) {
    if ($condition) {
        throw $message
    }
}

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r81-temporary-readonly-activation-retry-summary.json")
$preflight = Read-Json (Join-Path $artifactRoot "phase-lmax-r81-preflight-result.json")
$endpoint = Read-Json (Join-Path $artifactRoot "phase-lmax-r81-demo-endpoint-binding-evidence.json")
$socket = Read-Json (Join-Path $artifactRoot "phase-lmax-r81-socket-connector-evidence.json")
$tls = Read-Json (Join-Path $artifactRoot "phase-lmax-r81-tls-boundary-evidence.json")
$fix = Read-Json (Join-Path $artifactRoot "phase-lmax-r81-fix-session-boundary-evidence.json")
$market = Read-Json (Join-Path $artifactRoot "phase-lmax-r81-marketdata-request-evidence.json")
$trace = Read-Json (Join-Path $artifactRoot "phase-lmax-r81-operational-invocation-trace.json")
$boundary = Read-Json (Join-Path $artifactRoot "phase-lmax-r81-boundary-evidence.json")
$marketResult = Read-Json (Join-Path $artifactRoot "phase-lmax-r81-marketdata-sanitized-result.json")
$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r81-forbidden-actions-audit.json")
$apiWorker = Read-Json (Join-Path $artifactRoot "phase-lmax-r81-api-worker-fake-gateway-audit.json")
$usdJpy = Read-Json (Join-Path $artifactRoot "phase-lmax-r81-usdjpy-caveat-preservation.json")
$shutdown = Read-Json (Join-Path $artifactRoot "phase-lmax-r81-shutdown-revert-evidence.json")
$next = Read-Json (Join-Path $artifactRoot "phase-lmax-r81-next-phase-recommendation.json")
$gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r81-gate-validation.json")

$allowedClassifications = @(
    "LMAX_R81_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED",
    "LMAX_R81_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED",
    "LMAX_R81_FAIL_R80_SUCCESS_EVIDENCE_MISSING",
    "LMAX_R81_FAIL_DEMO_ENDPOINT_BINDING_REGRESSION",
    "LMAX_R81_FAIL_PLACEHOLDER_HOST_USED",
    "LMAX_R81_FAIL_SOCKET_CONNECTOR_BINDING_REGRESSION",
    "LMAX_R81_FAIL_TLS_PROGRESSION_BINDING_REGRESSION",
    "LMAX_R81_FAIL_FIX_PROGRESSION_BINDING_REGRESSION",
    "LMAX_R81_FAIL_FIX_ATTEMPT_ALLOWED_WITHOUT_TLS_SUCCESS",
    "LMAX_R81_FAIL_MARKETDATA_ATTEMPT_ALLOWED_WITHOUT_FIX_SUCCESS",
    "LMAX_R81_FAIL_MANUAL_EXECUTION_SURFACE_REGRESSION",
    "LMAX_R81_FAIL_REAL_BOUNDED_ADAPTER_BINDING_REGRESSION",
    "LMAX_R81_FAIL_NO_EXTERNAL_ADAPTER_USED_INSTEAD_OF_REAL_BOUNDARY_ADAPTER",
    "LMAX_R81_FAIL_EXECUTE_ONCE_NOT_INVOKED",
    "LMAX_R81_FAIL_PRE_EXTERNAL_APPROVAL_OR_COMPOSITION_REGRESSION",
    "LMAX_R81_FAIL_CREDENTIAL_CONFIG_MISSING",
    "LMAX_R81_FAIL_CREDENTIAL_CONFIG_NOT_APPROVED",
    "LMAX_R81_FAIL_CREDENTIAL_CONFIG_BINDING_REGRESSION",
    "LMAX_R81_FAIL_PRODUCTION_ACCOUNT_RISK",
    "LMAX_R81_FAIL_API_WORKER_GATEWAY_REGRESSION",
    "LMAX_R81_FAIL_LIVE_LAUNCHER_OR_BACKGROUND_SERVICE_RISK",
    "LMAX_R81_FAIL_FORBIDDEN_ACTION_RISK",
    "LMAX_R81_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R81_FAIL_PREFLIGHT_ABORTED",
    "LMAX_R81_FAIL_TCP_SOCKET_BOUNDARY",
    "LMAX_R81_FAIL_TLS_BOUNDARY",
    "LMAX_R81_FAIL_FIX_LOGON_OR_SESSION_BOUNDARY",
    "LMAX_R81_FAIL_MARKETDATA_REQUEST_BOUNDARY",
    "LMAX_R81_FAIL_MARKETDATA_RESPONSE_BOUNDARY",
    "LMAX_R81_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE",
    "LMAX_R81_FAIL_SHUTDOWN_REVERT_INCOMPLETE",
    "LMAX_R81_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK",
    "LMAX_R81_FAIL_BUILD_OR_TESTS"
)

Assert-True ($allowedClassifications -contains $summary.classification) "R81 classification is absent or not allowed."
Assert-True ($summary.classification -eq "LMAX_R81_FAIL_FIX_LOGON_OR_SESSION_BOUNDARY") "R81 classification mismatch."

Assert-True ($summary.externalActivationAttempted -eq $true) "External activation was not recorded as attempted."
Assert-True ($summary.attemptCount -eq 1) "attemptCount must be exactly 1."
Assert-True ($summary.retryPhaseReservationPassed -eq $true) "LMAX-R81 retry phase reservation did not pass."
Assert-True ($summary.manualCliSurfaceUsed -eq $true) "Manual CLI surface evidence missing."
Assert-True ($summary.adapterModeRealBoundedExecutableReadOnlyUsed -eq $true) "real-bounded adapter mode evidence missing."
Assert-True ($summary.concreteDemoEndpointBindingUsed -eq $true) "Concrete Demo endpoint binding evidence missing."
Assert-True ($summary.configuredSocketConnectorUsed -eq $true) "Configured socket connector evidence missing."
Assert-True ($summary.lmaxReadOnlyActivationManualTcpSocketConnectorConnectReached -eq $true) "Connect was not reached."
Assert-True ($summary.tcpSocketSucceeded -eq $true) "TCP/socket success not proven."
Assert-True ($summary.tlsAttempted -eq $true) "TLS attempt not proven."
Assert-True ($summary.socketConnectorAuthenticateTlsReached -eq $true) "AuthenticateTls not reached."
Assert-True ($summary.tlsBoundaryResult -eq "Succeeded") "TLS success not proven for FIX attempt."
Assert-True ($summary.fixLogonSessionAttempted -eq $true) "FIX logon/session was not attempted."
Assert-True ($summary.socketConnectorOpenFixSessionReached -eq $true) "OpenFixSession was not reached."
Assert-True ($summary.fixBoundaryResult -eq "FailedValidation") "Unexpected FIX boundary result."
Assert-True ($summary.fixBoundaryResultCategory -eq "FixCredentialMaterialUnavailable") "Unexpected FIX boundary category."
Assert-False $summary.marketDataRequestAttempted "MarketDataRequest was attempted without FIX success."
Assert-False $summary.marketDataResponseEntriesObserved "MarketDataResponse entries were unexpectedly observed."
Assert-False $summary.credentialValuesReturned "credentialValuesReturned is not false."
Assert-False $summary.credentialEndpointTlsFixSensitiveValuesPrintedStoredSerialized "Sensitive values were printed/stored/serialized."

Assert-True ($preflight.preflightResult -eq "PASS") "Preflight did not pass."
Assert-True ($preflight.operatorApprovalExact -eq $true) "Exact operator approval missing."
Assert-True ($preflight.r80SuccessEvidencePresent -eq $true) "R80 success evidence missing."

Assert-True ($endpoint.endpointMode -eq "Demo") "Endpoint mode is not Demo."
Assert-True ($endpoint.endpointPresent -eq $true) "Endpoint not present."
Assert-True ($endpoint.hostPresent -eq $true) "Host not present."
Assert-True ($endpoint.hostConcreteBinding -eq $true) "Concrete host binding missing."
Assert-False $endpoint.hostWasPlaceholder "Placeholder host used."
Assert-True ($endpoint.portPresent -eq $true) "Port not present."
Assert-True ($endpoint.portConcreteBinding -eq $true) "Concrete port binding missing."
Assert-True ($endpoint.productionExcluded -eq $true) "Production endpoint/account not excluded."
Assert-True ($endpoint.endpointApproved -eq $true) "Endpoint not approved."
Assert-False $endpoint.rawHostSerialized "Raw host serialized."
Assert-False $endpoint.rawPortSerialized "Raw port serialized."

Assert-True ($socket.configuredSocketConnectorUsed -eq $true) "Configured socket connector not used."
Assert-True ($socket.connectReached -eq $true) "Connect not reached."
Assert-True ($socket.tcpSocketSucceeded -eq $true) "TCP did not succeed."
Assert-False $socket.retryLoopUsed "Retry loop used."
Assert-False $socket.pollingLoopUsed "Polling loop used."

Assert-True ($tls.tlsAttempted -eq $true) "TLS not attempted."
Assert-True ($tls.socketConnectorAuthenticateTlsReached -eq $true) "AuthenticateTls not reached."
Assert-True ($tls.tlsBoundary -eq "Succeeded") "TLS boundary not succeeded."
Assert-True ($tls.tlsSucceededBeforeFix -eq $true) "FIX was not gated by TLS success."
Assert-False $tls.rawTlsMaterialStored "Raw TLS material stored."
Assert-False $tls.rawTlsMaterialPrinted "Raw TLS material printed."
Assert-False $tls.rawTlsMaterialSerialized "Raw TLS material serialized."

Assert-True ($fix.fixLogonSessionAttempted -eq $true) "FIX not attempted."
Assert-True ($fix.socketConnectorOpenFixSessionReached -eq $true) "OpenFixSession not reached."
Assert-True ($fix.fixBoundary -eq "FailedValidation") "FIX boundary result mismatch."
Assert-True ($fix.fixBoundaryResultCategory -eq "FixCredentialMaterialUnavailable") "FIX category mismatch."
Assert-True ($fix.fixAttemptedOnlyAfterTlsSucceeded -eq $true) "FIX was allowed without TLS success."
Assert-False $fix.marketDataRequestAttemptedAfterFix "MarketDataRequest attempted after failed FIX."
Assert-False $fix.rawFixMessagesStored "Raw FIX messages stored."
Assert-False $fix.rawFixMessagesPrinted "Raw FIX messages printed."
Assert-False $fix.rawSensitiveFixLogsStored "Sensitive FIX logs stored."

Assert-False $market.marketDataRequestAttempted "MarketDataRequest attempted."
Assert-True ($market.marketDataRequestResultCategory -eq "NotAttemptedAfterFixBoundaryFailure") "MarketDataRequest category mismatch."
Assert-False $market.fixSessionSucceeded "FIX session should not be marked succeeded."
Assert-False $market.marketDataResponseEntriesObserved "MarketDataResponse entries observed."

Assert-True ($trace.callOnceInvoked -eq $true) "CallOnce not invoked."
Assert-True ($trace.invokeOnceInvoked -eq $true) "InvokeOnce not invoked."
Assert-True ($trace.executeOnceInvoked -eq $true) "ExecuteOnce not invoked."
Assert-True ($trace.attemptCount -eq 1) "Trace attemptCount invalid."
Assert-True ($trace.fixLogonAttempted -eq $true) "Trace does not show FIX attempt."
Assert-False $trace.marketDataRequestSent "Trace shows MarketDataRequest sent."

Assert-True ($boundary.boundaryStatuses.credentialConfig -eq "ValidationOnly") "Credential/config boundary mismatch."
Assert-True ($boundary.boundaryStatuses.tcpSocket -eq "Succeeded") "TCP boundary mismatch."
Assert-True ($boundary.boundaryStatuses.tls -eq "Succeeded") "TLS boundary mismatch."
Assert-True ($boundary.boundaryStatuses.fixLogonSession -eq "FailedValidation") "FIX boundary mismatch."
Assert-True ($boundary.boundaryStatuses.marketDataRequest -eq "NotAttempted") "MarketDataRequest boundary mismatch."
Assert-True ($boundary.boundaryStatuses.marketDataResponseEntries -eq "NotAttempted") "MarketDataResponse boundary mismatch."
Assert-True ($boundary.boundaryStatuses.shutdownRevert -eq "Succeeded") "Shutdown/revert boundary mismatch."

Assert-False $marketResult.marketDataRequestAttempted "Market data result says request attempted."
Assert-False $marketResult.marketDataResponseEntriesObserved "Market data result says entries observed."
Assert-True ($marketResult.instrumentResultCount -eq 0) "Market data instrument result count should be zero."

Assert-True ($forbidden.result -eq "PASS") "Forbidden actions audit failed."
Assert-False $forbidden.ordersSubmitted "Orders were submitted."
Assert-False $forbidden.orderPathTouched "Order/trading path touched."
Assert-False $forbidden.tradingStateMutated "Trading state mutated."
Assert-False $forbidden.productionAccountUsedOrAllowed "Production account/config used or allowed."
Assert-False $forbidden.fixAttemptedWithoutTlsSucceeded "FIX attempted without TLS success."
Assert-False $forbidden.marketDataAttemptedWithoutFixSucceeded "MarketData attempted without FIX success."

Assert-True ($apiWorker.result -eq "PASS") "API/Worker audit failed."
Assert-True ($apiWorker.apiWorkerGatewayMode -eq "FakeLmaxGatewayOnly") "API/Worker gateway changed."
Assert-False $apiWorker.manualCliReachableFromApiWorkerDefaultStartup "Manual CLI reachable from API/Worker/default startup."
Assert-False $apiWorker.fixProgressionWiredIntoApiWorker "FIX progression wired into API/Worker."

Assert-True ($usdJpy.result -eq "PASS") "USDJPY caveat validation failed."
Assert-True ($usdJpy.caveatPreserved -eq $true) "USDJPY caveat missing or weakened."
Assert-False $usdJpy.weakened "USDJPY caveat weakened."

Assert-True ($shutdown.shutdownRevertCompleted -eq $true) "Shutdown/revert incomplete."
Assert-True ($shutdown.socketDisposedByConnector -eq $true) "Socket disposal evidence missing."
Assert-True ($shutdown.tlsStreamDisposedByConnector -eq $true) "TLS stream disposal evidence missing."

Assert-True ($gate.validatorResult -eq "PASS") "Gate validator result missing."
Assert-True ($gate.buildResult.status -eq "PASS") "Build evidence missing."
Assert-True ($gate.testResult.status -eq "PASS") "Test evidence missing."
Assert-True ($next.nextRecommendedPhase -eq "LMAX-R82") "Next phase recommendation missing or incorrect."
Assert-True ($next.nextRecommendedTitle -eq "FIX Session Boundary Root-Cause Pack") "Next phase title mismatch."

$artifactText = Get-ChildItem $artifactRoot -Filter "phase-lmax-r81-*" -File |
    Where-Object { $_.Name -ne "phase-lmax-r81-operator-approval-note.md" } |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
$forbiddenPatterns = @(
    "35=D",
    "35=F",
    "35=H",
    "35=AE",
    "554=",
    "BEGIN PRIVATE KEY",
    "password=",
    "secret=",
    "token=",
    "fix-marketdata",
    "london-demo.lmax.com"
)

foreach ($pattern in $forbiddenPatterns) {
    if ($joined -match [regex]::Escape($pattern)) {
        throw "Forbidden sensitive artifact pattern found in R81 artifacts: $pattern"
    }
}

Write-Output "LMAX_R81_VALIDATION_PASS"
