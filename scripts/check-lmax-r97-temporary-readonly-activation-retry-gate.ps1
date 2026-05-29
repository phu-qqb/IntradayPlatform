$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repoRoot 'artifacts/readiness/lmax-runtime-enablement'

function Read-Json($path) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing required artifact: $path"
    }
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

function Require-True($value, $message) { if ($value -ne $true) { throw $message } }
function Require-False($value, $message) { if ($value -ne $false) { throw $message } }
function Require-Equal($actual, $expected, $message) {
    if ($actual -ne $expected) { throw "$message Expected '$expected' but found '$actual'." }
}

$summary = Read-Json (Join-Path $artifactRoot 'phase-lmax-r97-temporary-readonly-activation-retry-summary.json')
$preflight = Read-Json (Join-Path $artifactRoot 'phase-lmax-r97-preflight-result.json')
$endpoint = Read-Json (Join-Path $artifactRoot 'phase-lmax-r97-demo-endpoint-binding-evidence.json')
$socket = Read-Json (Join-Path $artifactRoot 'phase-lmax-r97-socket-connector-evidence.json')
$tls = Read-Json (Join-Path $artifactRoot 'phase-lmax-r97-tls-boundary-evidence.json')
$tlsClassification = Read-Json (Join-Path $artifactRoot 'phase-lmax-r97-tls-classification-evidence.json')
$credential = Read-Json (Join-Path $artifactRoot 'phase-lmax-r97-fix-credential-material-evidence.json')
$fixWrite = Read-Json (Join-Path $artifactRoot 'phase-lmax-r97-fix-logon-frame-write-evidence.json')
$fix = Read-Json (Join-Path $artifactRoot 'phase-lmax-r97-fix-session-boundary-evidence.json')
$market = Read-Json (Join-Path $artifactRoot 'phase-lmax-r97-marketdata-request-evidence.json')
$trace = Read-Json (Join-Path $artifactRoot 'phase-lmax-r97-operational-invocation-trace.json')
$boundary = Read-Json (Join-Path $artifactRoot 'phase-lmax-r97-boundary-evidence.json')
$marketResult = Read-Json (Join-Path $artifactRoot 'phase-lmax-r97-marketdata-sanitized-result.json')
$forbidden = Read-Json (Join-Path $artifactRoot 'phase-lmax-r97-forbidden-actions-audit.json')
$apiWorker = Read-Json (Join-Path $artifactRoot 'phase-lmax-r97-api-worker-fake-gateway-audit.json')
$usdJpy = Read-Json (Join-Path $artifactRoot 'phase-lmax-r97-usdjpy-caveat-preservation.json')
$shutdown = Read-Json (Join-Path $artifactRoot 'phase-lmax-r97-shutdown-revert-evidence.json')
$next = Read-Json (Join-Path $artifactRoot 'phase-lmax-r97-next-phase-recommendation.json')
$gate = Read-Json (Join-Path $artifactRoot 'phase-lmax-r97-gate-validation.json')
$r96 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r96-tls-runtime-boundary-root-cause-summary.json')

$allowed = @(
    'LMAX_R97_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED',
    'LMAX_R97_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED',
    'LMAX_R97_FAIL_R96_SUCCESS_EVIDENCE_MISSING',
    'LMAX_R97_FAIL_DEMO_ENDPOINT_BINDING_REGRESSION',
    'LMAX_R97_FAIL_PLACEHOLDER_HOST_USED',
    'LMAX_R97_FAIL_SOCKET_CONNECTOR_BINDING_REGRESSION',
    'LMAX_R97_FAIL_TLS_PROGRESSION_BINDING_REGRESSION',
    'LMAX_R97_FAIL_TLS_CLASSIFICATION_INSTRUMENTATION_REGRESSION',
    'LMAX_R97_FAIL_FIX_PROGRESSION_BINDING_REGRESSION',
    'LMAX_R97_FAIL_FIX_CREDENTIAL_MATERIAL_BINDING_REGRESSION',
    'LMAX_R97_FAIL_FIX_LOGON_FRAME_WRITE_BINDING_REGRESSION',
    'LMAX_R97_FAIL_FIX_WRITER_SUPPORTS_ORDER_FRAMES',
    'LMAX_R97_FAIL_REAL_SECRET_MATERIAL_NOT_ALLOWED_FOR_APPROVED_RETRY',
    'LMAX_R97_FAIL_REAL_SECRET_MATERIAL_NOT_LOADED_FOR_APPROVED_RETRY',
    'LMAX_R97_FAIL_FIX_ATTEMPT_ALLOWED_WITHOUT_TLS_SUCCESS',
    'LMAX_R97_FAIL_MARKETDATA_ATTEMPT_ALLOWED_WITHOUT_FIX_SUCCESS',
    'LMAX_R97_FAIL_MANUAL_EXECUTION_SURFACE_REGRESSION',
    'LMAX_R97_FAIL_REAL_BOUNDED_ADAPTER_BINDING_REGRESSION',
    'LMAX_R97_FAIL_NO_EXTERNAL_ADAPTER_USED_INSTEAD_OF_REAL_BOUNDARY_ADAPTER',
    'LMAX_R97_FAIL_EXECUTE_ONCE_NOT_INVOKED',
    'LMAX_R97_FAIL_PRE_EXTERNAL_APPROVAL_OR_COMPOSITION_REGRESSION',
    'LMAX_R97_FAIL_CREDENTIAL_CONFIG_MISSING',
    'LMAX_R97_FAIL_CREDENTIAL_CONFIG_NOT_APPROVED',
    'LMAX_R97_FAIL_CREDENTIAL_CONFIG_BINDING_REGRESSION',
    'LMAX_R97_FAIL_PRODUCTION_ACCOUNT_RISK',
    'LMAX_R97_FAIL_API_WORKER_GATEWAY_REGRESSION',
    'LMAX_R97_FAIL_LIVE_LAUNCHER_OR_BACKGROUND_SERVICE_RISK',
    'LMAX_R97_FAIL_FORBIDDEN_ACTION_RISK',
    'LMAX_R97_FAIL_USDJPY_CAVEAT_WEAKENED',
    'LMAX_R97_FAIL_PREFLIGHT_ABORTED',
    'LMAX_R97_FAIL_TCP_SOCKET_BOUNDARY',
    'LMAX_R97_FAIL_TLS_BOUNDARY',
    'LMAX_R97_FAIL_FIX_LOGON_OR_SESSION_BOUNDARY',
    'LMAX_R97_FAIL_MARKETDATA_REQUEST_BOUNDARY',
    'LMAX_R97_FAIL_MARKETDATA_RESPONSE_BOUNDARY',
    'LMAX_R97_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE',
    'LMAX_R97_FAIL_SHUTDOWN_REVERT_INCOMPLETE',
    'LMAX_R97_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK',
    'LMAX_R97_FAIL_BUILD_OR_TESTS'
)

if ($allowed -notcontains $summary.classification) {
    throw "Unexpected R97 classification: $($summary.classification)"
}

Require-Equal $summary.classification 'LMAX_R97_FAIL_TLS_BOUNDARY' 'R97 classification mismatch.'
Require-Equal $r96.classification 'LMAX_R96_PASS_TLS_HANDSHAKE_EXCEPTION_REPEATED_EXTERNAL_OR_INTERMITTENT_SUSPECT_NO_EXTERNAL_ACTIVATION' 'R96 evidence missing.'
Require-True $summary.externalActivationAttempted 'External activation attempt not proven.'
Require-Equal $summary.attemptCount 1 'attemptCount must be exactly 1.'
Require-True $summary.retryPhaseReservationPassed 'LMAX-R97 retry phase reservation did not pass.'
Require-True $summary.manualCliSurfaceUsed 'Manual CLI not used.'
Require-Equal $summary.manualCliSurface 'QQ.Production.Intraday.Tools.LmaxReadOnlyActivation' 'Manual CLI mismatch.'
Require-True $summary.adapterModeRealBoundedExecutableReadOnlyUsed 'Real bounded adapter mode not used.'
Require-Equal $summary.adapterMode 'real-bounded-executable-readonly' 'Adapter mode mismatch.'
Require-True $summary.concreteDemoEndpointBindingUsed 'Concrete Demo endpoint not used.'
Require-True $summary.configuredSocketConnectorUsed 'Configured socket connector not used.'
Require-True $summary.lmaxReadOnlyActivationManualTcpSocketConnectorConnectReached 'Manual TCP connector not reached.'
Require-True $summary.tcpSocketSucceeded 'TCP/socket did not succeed.'
Require-True $summary.tlsAttempted 'TLS not attempted.'
Require-True $summary.socketConnectorAuthenticateTlsReached 'AuthenticateTls not reached.'
Require-False $summary.tlsSucceeded 'TLS should not be marked succeeded.'
Require-Equal $summary.tlsBoundaryStatus 'Failed' 'TLS boundary status mismatch.'
Require-Equal $summary.tlsResultCategory 'HandshakeException' 'TLS result category mismatch.'
Require-Equal $summary.tlsFailureCategory 'HandshakeException' 'TLS failure category mismatch.'
Require-False $summary.tlsTimedOut 'TLS timeout should be false.'
Require-Equal $summary.tlsExceptionCategory 'HandshakeException' 'TLS exception category mismatch.'
Require-False $summary.tlsStreamAvailableForFix 'TLS stream should not be available for FIX.'
Require-False $summary.tlsRawMaterialSerialized 'Raw TLS material serialized.'
Require-False $summary.fixLogonSessionAttempted 'FIX attempted after TLS failure.'
Require-False $summary.socketConnectorOpenFixSessionReached 'OpenFixSession reached after TLS failure.'
Require-False $summary.sessionLogonOnlyFixFrameWriterUsed 'FIX writer used after TLS failure.'
Require-False $summary.rawFixFrameSerializedInArtifacts 'Raw FIX frame serialized.'
Require-True $summary.orderCapableFixFramePathAbsent 'Order-capable FIX frame path not absent.'
Require-False $summary.marketDataRequestAttempted 'MarketDataRequest attempted after TLS failure.'
Require-False $summary.marketDataResponseEntriesObserved 'MarketDataResponse entries observed.'
Require-False $summary.credentialValuesReturned 'credentialValuesReturned must be false.'
Require-False $summary.rawCredentialsFixFieldsMessagesTlsMaterialPrintedStoredSerialized 'Sensitive values serialized.'

Require-True $preflight.operatorApprovalExact 'Operator approval mismatch.'
Require-True $preflight.r96SuccessEvidencePresent 'R96 success evidence missing.'
Require-True $preflight.realBoundedExecutableReadOnlyAdapterSelected 'Real bounded adapter not selected.'
Require-False $preflight.noExternalAdapterUsedInstead 'NoExternal adapter used instead.'
Require-Equal $preflight.preflightResult 'PASS' 'Preflight did not pass.'

Require-Equal $endpoint.endpointMode 'Demo' 'Endpoint mode mismatch.'
Require-True $endpoint.endpointPresent 'Endpoint missing.'
Require-True $endpoint.hostPresent 'Host missing.'
Require-True $endpoint.hostConcreteBinding 'Concrete host binding missing.'
Require-False $endpoint.hostWasPlaceholder 'Placeholder host used.'
Require-True $endpoint.portPresent 'Port missing.'
Require-True $endpoint.portConcreteBinding 'Concrete port binding missing.'
Require-True $endpoint.productionExcluded 'Production endpoint not excluded.'
Require-True $endpoint.endpointApproved 'Endpoint not approved.'
Require-False $endpoint.rawEndpointSerialized 'Raw endpoint serialized.'

Require-True $socket.configuredSocketConnectorUsed 'Configured socket connector missing.'
Require-True $socket.lmaxReadOnlyActivationManualTcpSocketConnectorConnectReached 'Connect not reached.'
Require-True $socket.tcpConnectionAttempted 'TCP not attempted.'
Require-True $socket.tcpSocketSucceeded 'TCP not succeeded.'
Require-False $socket.socketConnectorGlobalDefault 'Socket connector became global/default.'

Require-True $tls.tlsAttempted 'TLS evidence missing.'
Require-True $tls.socketConnectorAuthenticateTlsReached 'AuthenticateTls evidence missing.'
Require-Equal $tls.tlsBoundaryResult 'FailedExternal' 'TLS boundary result mismatch.'
Require-Equal $tls.tlsBoundaryResultCategory 'HandshakeException' 'TLS boundary category mismatch.'
Require-False $tls.tlsSucceeded 'TLS evidence should not show success.'
Require-Equal $tls.fixNotAttemptedReason 'TlsBoundaryDidNotReturnSucceeded' 'FIX not-attempted reason mismatch.'
Require-False $tls.tlsRawMaterialSerialized 'Raw TLS material serialized.'
Require-False $tls.rawTlsExceptionDetailsSerialized 'Raw TLS exception details serialized.'

Require-True $tlsClassification.tlsAttempted 'TLS classification attempted missing.'
Require-False $tlsClassification.tlsSucceeded 'TLS classification should be non-success.'
Require-Equal $tlsClassification.tlsBoundaryStatus 'Failed' 'TLS classification status mismatch.'
Require-Equal $tlsClassification.tlsResultCategory 'HandshakeException' 'TLS classification category mismatch.'
Require-False $tlsClassification.tlsTimedOut 'TLS classification timeout mismatch.'
Require-False $tlsClassification.tlsStreamAvailableForFix 'TLS classification stream mismatch.'
Require-False $tlsClassification.tlsRawMaterialSerialized 'TLS classification raw material serialized.'
foreach ($category in @('Succeeded','AttemptedOnly','Timeout','HandshakeException','CertificateValidationFailure','StreamUnavailable','CancelledOrAborted','UnknownFailure','NotAttempted')) {
    if ($tlsClassification.supportedSanitizedTlsCategories -notcontains $category) { throw "Missing TLS category: $category" }
}

Require-True $credential.inMemoryOnlyFixCredentialMaterialAllowed 'FIX credential material not allowed for approved path.'
Require-False $credential.inMemoryOnlyFixCredentialMaterialLoadedSanitizedBoolean 'FIX credential material should not load after TLS failure.'
Require-Equal $credential.fixCredentialMaterialLoadSkippedReason 'TlsBoundaryDidNotReturnSucceeded' 'Credential skip reason mismatch.'
Require-False $credential.credentialValuesReturned 'Credential values returned.'
Require-False $credential.rawCredentialSerialized 'Raw credential serialized.'

Require-True $fixWrite.fixLogonBuilderReady 'FIX logon builder not ready.'
Require-True $fixWrite.fixFrameWriterReady 'FIX frame writer not ready.'
Require-True $fixWrite.sessionOnly 'FIX writer not session-only.'
Require-False $fixWrite.orderFramesSupported 'FIX writer supports order frames.'
Require-False $fixWrite.sessionLogonOnlyFixFrameWriterUsed 'FIX writer used after TLS failure.'
Require-False $fixWrite.rawFixSerialized 'Raw FIX serialized.'
Require-True $fixWrite.orderCapableFixFramePathAbsent 'Order-capable path not absent.'

Require-False $fix.fixLogonSessionAttempted 'FIX attempted.'
Require-False $fix.socketConnectorOpenFixSessionReached 'OpenFixSession reached.'
Require-Equal $fix.fixBoundaryResult 'NotAttempted' 'FIX boundary mismatch.'
Require-Equal $fix.fixBoundaryResultCategory 'NotAttemptedAfterTlsBoundaryDidNotSucceed' 'FIX category mismatch.'
Require-False $fix.fixAttemptedWithoutTlsSucceeded 'FIX attempted without TLS success.'
Require-False $fix.rawFixMessagesSerialized 'Raw FIX messages serialized.'
Require-False $fix.rawSensitiveFixLogsSerialized 'Sensitive FIX logs serialized.'

Require-False $market.marketDataRequestAttempted 'MarketDataRequest attempted.'
Require-Equal $market.marketDataRequestResultCategory 'NotAttemptedAfterTlsBoundaryDidNotSucceed' 'MarketData category mismatch.'
Require-False $market.marketDataAttemptedWithoutFixSucceeded 'MarketData attempted without FIX success.'
if (($market.approvedInstruments -join ',') -ne 'GBPUSD,EURGBP,AUDUSD,USDJPY') { throw 'Approved instruments mismatch.' }
Require-False $market.nonApprovedInstrumentsUsed 'Non-approved instruments used.'

Require-Equal $trace.attemptCount 1 'Trace attempt count mismatch.'
Require-True $trace.callOnceInvoked 'CallOnce not invoked.'
Require-True $trace.invokeOnceInvoked 'InvokeOnce not invoked.'
Require-True $trace.executeOnceInvoked 'ExecuteOnce not invoked.'
Require-True $trace.externalActivationAttempted 'Trace external activation missing.'
Require-True $trace.tcpConnectionAttempted 'Trace TCP missing.'
Require-True $trace.tlsHandshakeAttempted 'Trace TLS missing.'
Require-False $trace.fixLogonAttempted 'Trace FIX attempted.'
Require-False $trace.marketDataRequestSent 'Trace MarketData sent.'
Require-True $trace.shutdownRevertCompleted 'Trace shutdown incomplete.'
Require-False $trace.credentialValuesReturned 'Trace credential values returned.'
Require-True $trace.outputSanitized 'Trace output not sanitized.'

Require-Equal $boundary.credentialConfig 'ValidationOnly' 'Boundary credential mismatch.'
Require-Equal $boundary.tcpSocket 'Succeeded' 'Boundary TCP mismatch.'
Require-Equal $boundary.tls 'FailedExternal' 'Boundary TLS mismatch.'
Require-Equal $boundary.fixLogonSession 'NotAttempted' 'Boundary FIX mismatch.'
Require-Equal $boundary.marketDataRequest 'NotAttempted' 'Boundary MarketData mismatch.'
Require-Equal $boundary.marketDataResponseEntries 'NotAttempted' 'Boundary response mismatch.'
Require-Equal $boundary.shutdownRevert 'Succeeded' 'Boundary shutdown mismatch.'
Require-True $boundary.fixBlockedAfterTlsNonSuccess 'FIX not blocked after TLS non-success.'
Require-True $boundary.marketDataBlockedAfterTlsNonSuccess 'MarketData not blocked after TLS non-success.'

Require-False $marketResult.marketDataRequestAttempted 'Market data result attempted.'
Require-False $marketResult.marketDataResponseEntriesObserved 'Market data entries observed.'
Require-Equal $marketResult.entryCount 0 'Market data entry count mismatch.'
Require-False $marketResult.rawMarketDataSerialized 'Raw market data serialized.'

Require-Equal $forbidden.result 'PASS' 'Forbidden audit failed.'
Require-False $forbidden.ordersSubmitted 'Orders submitted.'
Require-False $forbidden.newOrderSingleUsed 'NewOrderSingle used.'
Require-False $forbidden.cancelReplaceUsed 'Cancel/replace used.'
Require-False $forbidden.tradingEnabled 'Trading enabled.'
Require-False $forbidden.tradingStateMutated 'Trading state mutated.'
Require-False $forbidden.productionAccountUsed 'Production account used.'
Require-False $forbidden.apiStartupUsed 'API startup used.'
Require-False $forbidden.workerStartupUsed 'Worker startup used.'
Require-False $forbidden.schedulerPollingLoopUsed 'Scheduler/polling loop used.'
Require-False $forbidden.fixAttemptedWithoutTlsSucceeded 'FIX attempted without TLS.'
Require-False $forbidden.marketDataAttemptedWithoutFixSucceeded 'MarketData attempted without FIX.'
Require-True $forbidden.orderCapableFixFramePathAbsent 'Order-capable FIX path not absent.'

Require-Equal $apiWorker.result 'PASS' 'API/Worker audit failed.'
Require-True $apiWorker.apiWorkerFakeLmaxGatewayOnly 'API/Worker not fake gateway only.'
Require-False $apiWorker.manualCliReachableFromApiWorkerDefaultStartup 'Manual CLI reachable from API/Worker.'
Require-False $apiWorker.realAdapterGlobalDefault 'Real adapter global/default.'

Require-True $usdJpy.caveatPreserved 'USDJPY caveat not preserved.'
Require-Equal $usdJpy.securityId '4004' 'USDJPY SecurityID mismatch.'
Require-Equal $usdJpy.securityIdSource '8' 'USDJPY SecurityIDSource mismatch.'
Require-Equal $usdJpy.caveat 'prior failed-safe root cause remains unproven' 'USDJPY caveat weakened.'

Require-Equal $shutdown.shutdownRevert 'Succeeded' 'Shutdown mismatch.'
Require-True $shutdown.shutdownRevertCompleted 'Shutdown not completed.'
Require-True $shutdown.socketClosedOrDisposed 'Socket close/dispose missing.'
Require-False $shutdown.tradingStateMutated 'Trading state mutated during shutdown.'

Require-Equal $next.nextRecommendedPhase 'LMAX-R98' 'Next phase mismatch.'
Require-Equal $next.nextRecommendedTitle 'TLS Runtime Boundary Root-Cause Pack' 'Next phase title mismatch.'
Require-True $next.r98MustNotPerformActivation 'R98 no-activation constraint missing.'

Require-Equal $gate.gateValidation 'PASS' 'Gate validation mismatch.'
Require-True $gate.buildEvidencePresent 'Build evidence missing.'
Require-True $gate.testEvidencePresent 'Test evidence missing.'
Require-True $gate.sanitizationEvidencePresent 'Sanitization evidence missing.'
Require-True $gate.nextPhaseRecommendationPresent 'Next phase recommendation missing.'

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r97-*' -File |
    Where-Object { $_.Name -ne 'phase-lmax-r97-operator-approval-note.md' } |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }

$forbiddenPatterns = @(
    'password\s*[:=]',
    'username\s*[:=]',
    'session\s*token',
    'sendercompid\s*[:=]',
    'targetcompid\s*[:=]',
    '35=A',
    '8=FIX',
    '10=\d{3}',
    'BEGIN CERTIFICATE',
    'END CERTIFICATE',
    'subject=',
    'issuer=',
    'private key'
)

foreach ($pattern in $forbiddenPatterns) {
    if ($artifactText -match $pattern) { throw "Forbidden sensitive or trading pattern found in R97 artifacts: $pattern" }
}

Write-Output 'LMAX_R97_VALIDATION_PASS'
