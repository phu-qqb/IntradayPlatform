param(
    [string]$Root = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'

function Fail([string]$Message) {
    Write-Error "LMAX_R89_VALIDATION_FAIL: $Message"
    exit 1
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing required artifact: $Path"
    }

    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Require-True($Value, [string]$Message) {
    if ($Value -ne $true) { Fail $Message }
}

function Require-False($Value, [string]$Message) {
    if ($Value -ne $false) { Fail $Message }
}

function Require-Equal($Actual, $Expected, [string]$Message) {
    if ($Actual -ne $Expected) { Fail "$Message Expected=[$Expected] Actual=[$Actual]" }
}

$artifactRoot = Join-Path $Root 'artifacts/readiness/lmax-runtime-enablement'
$summary = Read-Json (Join-Path $artifactRoot 'phase-lmax-r89-temporary-readonly-activation-retry-summary.json')
$preflight = Read-Json (Join-Path $artifactRoot 'phase-lmax-r89-preflight-result.json')
$endpoint = Read-Json (Join-Path $artifactRoot 'phase-lmax-r89-demo-endpoint-binding-evidence.json')
$socket = Read-Json (Join-Path $artifactRoot 'phase-lmax-r89-socket-connector-evidence.json')
$tls = Read-Json (Join-Path $artifactRoot 'phase-lmax-r89-tls-boundary-evidence.json')
$credential = Read-Json (Join-Path $artifactRoot 'phase-lmax-r89-fix-credential-material-evidence.json')
$fixWrite = Read-Json (Join-Path $artifactRoot 'phase-lmax-r89-fix-logon-frame-write-evidence.json')
$fix = Read-Json (Join-Path $artifactRoot 'phase-lmax-r89-fix-session-boundary-evidence.json')
$market = Read-Json (Join-Path $artifactRoot 'phase-lmax-r89-marketdata-request-evidence.json')
$trace = Read-Json (Join-Path $artifactRoot 'phase-lmax-r89-operational-invocation-trace.json')
$boundary = Read-Json (Join-Path $artifactRoot 'phase-lmax-r89-boundary-evidence.json')
$marketResult = Read-Json (Join-Path $artifactRoot 'phase-lmax-r89-marketdata-sanitized-result.json')
$forbidden = Read-Json (Join-Path $artifactRoot 'phase-lmax-r89-forbidden-actions-audit.json')
$apiWorker = Read-Json (Join-Path $artifactRoot 'phase-lmax-r89-api-worker-fake-gateway-audit.json')
$usdJpy = Read-Json (Join-Path $artifactRoot 'phase-lmax-r89-usdjpy-caveat-preservation.json')
$shutdown = Read-Json (Join-Path $artifactRoot 'phase-lmax-r89-shutdown-revert-evidence.json')
$next = Read-Json (Join-Path $artifactRoot 'phase-lmax-r89-next-phase-recommendation.json')
$gate = Read-Json (Join-Path $artifactRoot 'phase-lmax-r89-gate-validation.json')
$r87 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r87-fix-logon-frame-write-binding-summary.json')

$allowedClassifications = @(
    'LMAX_R89_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED',
    'LMAX_R89_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED',
    'LMAX_R89_FAIL_R87_SUCCESS_EVIDENCE_MISSING',
    'LMAX_R89_FAIL_DEMO_ENDPOINT_BINDING_REGRESSION',
    'LMAX_R89_FAIL_PLACEHOLDER_HOST_USED',
    'LMAX_R89_FAIL_SOCKET_CONNECTOR_BINDING_REGRESSION',
    'LMAX_R89_FAIL_TLS_PROGRESSION_BINDING_REGRESSION',
    'LMAX_R89_FAIL_FIX_PROGRESSION_BINDING_REGRESSION',
    'LMAX_R89_FAIL_FIX_CREDENTIAL_MATERIAL_BINDING_REGRESSION',
    'LMAX_R89_FAIL_FIX_LOGON_FRAME_WRITE_BINDING_REGRESSION',
    'LMAX_R89_FAIL_FIX_WRITER_SUPPORTS_ORDER_FRAMES',
    'LMAX_R89_FAIL_REAL_SECRET_MATERIAL_NOT_ALLOWED_FOR_APPROVED_RETRY',
    'LMAX_R89_FAIL_REAL_SECRET_MATERIAL_NOT_LOADED_FOR_APPROVED_RETRY',
    'LMAX_R89_FAIL_FIX_ATTEMPT_ALLOWED_WITHOUT_TLS_SUCCESS',
    'LMAX_R89_FAIL_MARKETDATA_ATTEMPT_ALLOWED_WITHOUT_FIX_SUCCESS',
    'LMAX_R89_FAIL_MANUAL_EXECUTION_SURFACE_REGRESSION',
    'LMAX_R89_FAIL_REAL_BOUNDED_ADAPTER_BINDING_REGRESSION',
    'LMAX_R89_FAIL_NO_EXTERNAL_ADAPTER_USED_INSTEAD_OF_REAL_BOUNDARY_ADAPTER',
    'LMAX_R89_FAIL_EXECUTE_ONCE_NOT_INVOKED',
    'LMAX_R89_FAIL_PRE_EXTERNAL_APPROVAL_OR_COMPOSITION_REGRESSION',
    'LMAX_R89_FAIL_CREDENTIAL_CONFIG_MISSING',
    'LMAX_R89_FAIL_CREDENTIAL_CONFIG_NOT_APPROVED',
    'LMAX_R89_FAIL_CREDENTIAL_CONFIG_BINDING_REGRESSION',
    'LMAX_R89_FAIL_PRODUCTION_ACCOUNT_RISK',
    'LMAX_R89_FAIL_API_WORKER_GATEWAY_REGRESSION',
    'LMAX_R89_FAIL_LIVE_LAUNCHER_OR_BACKGROUND_SERVICE_RISK',
    'LMAX_R89_FAIL_FORBIDDEN_ACTION_RISK',
    'LMAX_R89_FAIL_USDJPY_CAVEAT_WEAKENED',
    'LMAX_R89_FAIL_PREFLIGHT_ABORTED',
    'LMAX_R89_FAIL_TCP_SOCKET_BOUNDARY',
    'LMAX_R89_FAIL_TLS_BOUNDARY',
    'LMAX_R89_FAIL_FIX_LOGON_OR_SESSION_BOUNDARY',
    'LMAX_R89_FAIL_MARKETDATA_REQUEST_BOUNDARY',
    'LMAX_R89_FAIL_MARKETDATA_RESPONSE_BOUNDARY',
    'LMAX_R89_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE',
    'LMAX_R89_FAIL_SHUTDOWN_REVERT_INCOMPLETE',
    'LMAX_R89_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK',
    'LMAX_R89_FAIL_BUILD_OR_TESTS'
)

if ($allowedClassifications -notcontains $summary.classification) {
    Fail 'R89 classification is absent or not allowed.'
}

Require-Equal $summary.classification 'LMAX_R89_FAIL_TLS_BOUNDARY' 'R89 classification mismatch.'
Require-Equal $r87.classification 'LMAX_R87_PASS_FIX_LOGON_FRAME_WRITE_BINDING_READY_NO_EXTERNAL_ACTIVATION' 'R87 success evidence missing.'

Require-True $summary.externalActivationAttempted 'External activation was not recorded.'
Require-Equal $summary.attemptCount 1 'attemptCount must be exactly 1.'
Require-True $summary.retryPhaseReservationPassed 'LMAX-R89 retry phase reservation did not pass.'
Require-True $summary.manualCliSurfaceUsed 'Manual CLI surface evidence missing.'
Require-Equal $summary.manualCliSurface 'QQ.Production.Intraday.Tools.LmaxReadOnlyActivation' 'Manual CLI surface mismatch.'
Require-True $summary.adapterModeRealBoundedExecutableReadOnlyUsed 'real-bounded adapter mode evidence missing.'
Require-Equal $summary.adapterMode 'real-bounded-executable-readonly' 'Adapter mode mismatch.'
Require-True $summary.concreteDemoEndpointBindingUsed 'Concrete Demo endpoint binding evidence missing.'
Require-True $summary.configuredSocketConnectorUsed 'Configured socket connector evidence missing.'
Require-True $summary.lmaxReadOnlyActivationManualTcpSocketConnectorConnectReached 'Connect was not reached.'
Require-True $summary.tcpSocketSucceeded 'TCP/socket success not proven.'
Require-True $summary.tlsAttempted 'TLS attempt not proven.'
Require-True $summary.socketConnectorAuthenticateTlsReached 'AuthenticateTls not reached.'
Require-Equal $summary.tlsBoundaryResult 'Attempted' 'TLS boundary result mismatch.'
Require-Equal $summary.tlsBoundaryResultCategory 'TlsBoundaryAttemptedSanitized' 'TLS boundary category mismatch.'
Require-False $summary.tlsSucceeded 'TLS should not be marked succeeded for this R89 result.'
Require-False $summary.fixLogonSessionAttempted 'FIX logon/session should not be attempted after TLS did not succeed.'
Require-False $summary.socketConnectorOpenFixSessionReached 'OpenFixSession should not be reached after TLS did not succeed.'
Require-False $summary.sessionLogonOnlyFixFrameWriterUsed 'FIX writer should not be used when FIX was not reached.'
Require-True $summary.sessionLogonOnlyFixFrameWriterReady 'FIX writer readiness evidence missing.'
Require-False $summary.rawFixFrameSerializedInArtifacts 'Raw FIX frame serialized in artifacts.'
Require-True $summary.orderCapableFixFramePathAbsent 'Order-capable FIX frame path was not absent.'
Require-False $summary.marketDataRequestAttempted 'MarketDataRequest was attempted without FIX success.'
Require-False $summary.marketDataResponseEntriesObserved 'MarketDataResponse entries were unexpectedly observed.'
Require-False $summary.credentialValuesReturned 'credentialValuesReturned must remain false.'
Require-False $summary.rawCredentialsFixFieldsMessagesPrintedStoredSerialized 'Raw credential/FIX values were printed/stored/serialized.'

Require-Equal $preflight.preflightResult 'PASS' 'Preflight did not pass.'
Require-True $preflight.operatorApprovalExact 'Exact operator approval missing.'
Require-True $preflight.r87SuccessEvidencePresent 'R87 success evidence missing.'

Require-Equal $endpoint.endpointMode 'Demo' 'Endpoint mode is not Demo.'
Require-True $endpoint.endpointPresent 'Endpoint not present.'
Require-True $endpoint.hostPresent 'Host not present.'
Require-True $endpoint.hostConcreteBinding 'Concrete host binding missing.'
Require-False $endpoint.hostWasPlaceholder 'Placeholder host used.'
Require-True $endpoint.portPresent 'Port not present.'
Require-True $endpoint.portConcreteBinding 'Concrete port binding missing.'
Require-True $endpoint.productionExcluded 'Production endpoint/account not excluded.'
Require-True $endpoint.endpointApproved 'Endpoint not approved.'
Require-False $endpoint.rawHostSerialized 'Raw host serialized.'
Require-False $endpoint.rawPortSerialized 'Raw port serialized.'
Require-False $endpoint.rawEndpointSerialized 'Raw endpoint serialized.'

Require-True $socket.configuredSocketConnectorUsed 'Configured socket connector not used.'
Require-True $socket.connectReached 'Connect not reached.'
Require-True $socket.tcpSocketSucceeded 'TCP did not succeed.'
Require-False $socket.retryLoopUsed 'Retry loop used.'
Require-False $socket.pollingLoopUsed 'Polling loop used.'

Require-True $tls.tlsAttempted 'TLS not attempted.'
Require-True $tls.socketConnectorAuthenticateTlsReached 'AuthenticateTls not reached.'
Require-Equal $tls.tlsBoundary 'Attempted' 'TLS boundary status mismatch.'
Require-False $tls.tlsSucceeded 'TLS should not be marked succeeded.'
Require-Equal $tls.fixNotAttemptedReason 'TlsBoundaryDidNotReturnSucceeded' 'FIX not-attempted reason mismatch.'
Require-False $tls.rawTlsMaterialStored 'Raw TLS material stored.'
Require-False $tls.rawTlsMaterialPrinted 'Raw TLS material printed.'
Require-False $tls.rawTlsMaterialSerialized 'Raw TLS material serialized.'

Require-True $credential.approvedDemoReadOnlyInMemoryFixLogonCredentialMaterialReady 'FIX credential material binding readiness missing.'
Require-True $credential.realSecretMaterialAllowedNow 'RealSecretMaterialAllowedNow not true for approved path.'
Require-False $credential.realSecretMaterialLoadedInMemory 'Credential material should not be loaded when FIX was not reached.'
Require-True $credential.realSecretMaterialLoadedSanitizedBooleanOnly 'Material loaded evidence is not boolean-only.'
Require-False $credential.credentialValuesReturned 'Credential values returned.'
Require-False $credential.sensitiveMaterialReturned 'Sensitive material returned.'
Require-False $credential.sensitiveMaterialPrinted 'Sensitive material printed.'
Require-False $credential.sensitiveMaterialStored 'Sensitive material stored.'
Require-False $credential.sensitiveMaterialSerialized 'Sensitive material serialized.'
Require-False $credential.rawSecretSerialized 'Raw secret serialized.'
Require-False $credential.rawFixSerialized 'Raw FIX serialized.'
Require-False $credential.apiWorkerReachable 'Credential material API/Worker reachable.'

Require-True $fixWrite.fixLogonFrameWriteBindingReady 'FIX logon frame write binding readiness missing.'
Require-True $fixWrite.fixLogonBuilderReady 'FIX logon builder readiness missing.'
Require-True $fixWrite.fixFrameWriterReady 'FIX frame writer readiness missing.'
Require-True $fixWrite.sessionOnly 'FIX writer is not session-only.'
Require-True $fixWrite.logonOnly 'FIX writer is not logon-only.'
Require-False $fixWrite.orderFramesSupported 'Order frames supported.'
Require-False $fixWrite.newOrderSingleSupported 'NewOrderSingle supported.'
Require-False $fixWrite.cancelReplaceSupported 'Cancel/replace supported.'
Require-False $fixWrite.sessionLogonOnlyFixFrameWriterUsed 'FIX writer used unexpectedly.'
Require-False $fixWrite.rawFixMessagesStored 'Raw FIX messages stored.'
Require-False $fixWrite.rawFixMessagesPrinted 'Raw FIX messages printed.'
Require-False $fixWrite.rawFixMessagesSerialized 'Raw FIX messages serialized.'

Require-False $fix.fixLogonSessionAttempted 'FIX attempted unexpectedly.'
Require-False $fix.socketConnectorOpenFixSessionReached 'OpenFixSession reached unexpectedly.'
Require-Equal $fix.fixBoundary 'NotAttempted' 'FIX boundary mismatch.'
Require-Equal $fix.fixBoundaryResultCategory 'NotAttemptedAfterTlsBoundaryDidNotSucceed' 'FIX category mismatch.'
Require-True $fix.fixAttemptRequiresTlsSucceeded 'FIX no longer requires TLS success.'
Require-False $fix.fixAttemptedWithoutTlsSucceeded 'FIX attempted without TLS success.'
Require-False $fix.marketDataRequestAttemptedAfterFix 'MarketDataRequest attempted after failed/not-attempted FIX.'

Require-False $market.marketDataRequestAttempted 'MarketDataRequest attempted.'
Require-Equal $market.marketDataRequestResultCategory 'NotAttemptedAfterTlsBoundaryDidNotSucceed' 'MarketDataRequest category mismatch.'
Require-False $market.fixSessionSucceeded 'FIX session should not be marked succeeded.'
Require-False $market.marketDataAttemptAllowedWithoutFixSuccess 'MarketDataRequest allowed without FIX success.'

Require-True $trace.callOnceInvoked 'CallOnce not invoked.'
Require-True $trace.invokeOnceInvoked 'InvokeOnce not invoked.'
Require-True $trace.executeOnceInvoked 'ExecuteOnce not invoked.'
Require-Equal $trace.attemptCount 1 'Trace attemptCount invalid.'
Require-True $trace.externalActivationAttempted 'Trace does not show external activation.'
Require-True $trace.tcpConnectionAttempted 'Trace does not show TCP.'
Require-True $trace.tlsHandshakeAttempted 'Trace does not show TLS.'
Require-False $trace.fixLogonAttempted 'Trace shows FIX attempted.'
Require-False $trace.marketDataRequestSent 'Trace shows MarketDataRequest sent.'

Require-Equal $boundary.boundaryStatuses.credentialConfig 'ValidationOnly' 'Credential/config boundary mismatch.'
Require-Equal $boundary.boundaryStatuses.tcpSocket 'Succeeded' 'TCP boundary mismatch.'
Require-Equal $boundary.boundaryStatuses.tls 'Attempted' 'TLS boundary mismatch.'
Require-Equal $boundary.boundaryStatuses.fixLogonSession 'NotAttempted' 'FIX boundary mismatch.'
Require-Equal $boundary.boundaryStatuses.marketDataRequest 'NotAttempted' 'MarketDataRequest boundary mismatch.'
Require-Equal $boundary.boundaryStatuses.marketDataResponseEntries 'NotAttempted' 'MarketDataResponse boundary mismatch.'
Require-Equal $boundary.boundaryStatuses.shutdownRevert 'Succeeded' 'Shutdown/revert boundary mismatch.'

Require-False $marketResult.marketDataRequestAttempted 'Market data result says request attempted.'
Require-False $marketResult.marketDataResponseEntriesObserved 'Market data result says entries observed.'
Require-Equal $marketResult.instrumentResultCount 0 'Market data instrument result count should be zero.'

Require-Equal $forbidden.result 'PASS' 'Forbidden actions audit failed.'
Require-False $forbidden.ordersSubmitted 'Orders were submitted.'
Require-False $forbidden.orderPathTouched 'Order/trading path touched.'
Require-False $forbidden.tradingStateMutated 'Trading state mutated.'
Require-False $forbidden.productionAccountUsedOrAllowed 'Production account/config used or allowed.'
Require-False $forbidden.fixAttemptedWithoutTlsSucceeded 'FIX attempted without TLS success.'
Require-False $forbidden.marketDataAttemptedWithoutFixSucceeded 'MarketData attempted without FIX success.'
Require-False $forbidden.orderCapableFixWriterUsed 'Order-capable FIX writer used.'

Require-Equal $apiWorker.result 'PASS' 'API/Worker audit failed.'
Require-Equal $apiWorker.apiWorkerGatewayMode 'FakeLmaxGatewayOnly' 'API/Worker gateway changed.'
Require-False $apiWorker.manualCliReachableFromApiWorkerDefaultStartup 'Manual CLI reachable from API/Worker/default startup.'
Require-False $apiWorker.fixFrameWriterReachableFromApiWorkerDefaultStartup 'FIX writer reachable from API/Worker/default startup.'

Require-Equal $usdJpy.result 'PASS' 'USDJPY caveat validation failed.'
Require-True $usdJpy.caveatPreserved 'USDJPY caveat missing or weakened.'
Require-False $usdJpy.weakened 'USDJPY caveat weakened.'

Require-True $shutdown.shutdownRevertCompleted 'Shutdown/revert incomplete.'
Require-True $shutdown.socketDisposedByConnector 'Socket disposal evidence missing.'
Require-True $shutdown.tlsStreamDisposedByConnector 'TLS stream disposal evidence missing.'
Require-False $shutdown.marketDataRequestSent 'MarketDataRequest sent.'

Require-Equal $gate.validatorResult 'PASS' 'Gate validator result missing.'
Require-Equal $gate.buildResult.status 'PASS' 'Build evidence missing.'
Require-Equal $gate.focusedTestResult.status 'PASS' 'Focused test evidence missing.'
Require-Equal $gate.testResult.status 'PASS' 'Full test evidence missing.'
Require-Equal $next.nextRecommendedPhase 'LMAX-R90' 'Next phase recommendation missing or incorrect.'
Require-Equal $next.nextRecommendedTitle 'TLS Runtime Boundary Root-Cause Pack' 'Next phase title mismatch.'

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r89-*' -File |
    Where-Object { $_.Name -ne 'phase-lmax-r89-operator-approval-note.md' } |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$joined = $artifactText -join "`n"
$forbiddenPatterns = @(
    '35=D',
    '35=F',
    '35=H',
    '35=AE',
    '554=',
    'BEGIN PRIVATE KEY',
    'END PRIVATE KEY',
    'password\s*[:=]',
    'username\s*[:=]',
    'secret\s*[:=]',
    'token\s*[:=]',
    'session[_ -]?token\s*[:=]',
    'sendercompid\s*[:=]',
    'targetcompid\s*[:=]',
    'fix\s*message\s*:',
    'raw\s*fix\s*log\s*:'
)

foreach ($pattern in $forbiddenPatterns) {
    if ($joined -match $pattern) {
        Fail "Forbidden sensitive artifact pattern found in R89 artifacts: $pattern"
    }
}

Write-Output 'LMAX_R89_VALIDATION_PASS'
