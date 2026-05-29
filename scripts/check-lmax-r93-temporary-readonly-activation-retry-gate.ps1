$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repoRoot 'artifacts/readiness/lmax-runtime-enablement'

function Read-Json($path) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing required artifact: $path"
    }

    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

function Require-True($value, $message) {
    if ($value -ne $true) {
        throw $message
    }
}

function Require-False($value, $message) {
    if ($value -ne $false) {
        throw $message
    }
}

function Require-Equal($actual, $expected, $message) {
    if ($actual -ne $expected) {
        throw "$message Expected '$expected' but found '$actual'."
    }
}

$summary = Read-Json (Join-Path $artifactRoot 'phase-lmax-r93-temporary-readonly-activation-retry-summary.json')
$preflight = Read-Json (Join-Path $artifactRoot 'phase-lmax-r93-preflight-result.json')
$endpoint = Read-Json (Join-Path $artifactRoot 'phase-lmax-r93-demo-endpoint-binding-evidence.json')
$socket = Read-Json (Join-Path $artifactRoot 'phase-lmax-r93-socket-connector-evidence.json')
$tls = Read-Json (Join-Path $artifactRoot 'phase-lmax-r93-tls-boundary-evidence.json')
$tlsClassification = Read-Json (Join-Path $artifactRoot 'phase-lmax-r93-tls-classification-evidence.json')
$credential = Read-Json (Join-Path $artifactRoot 'phase-lmax-r93-fix-credential-material-evidence.json')
$fixWrite = Read-Json (Join-Path $artifactRoot 'phase-lmax-r93-fix-logon-frame-write-evidence.json')
$fix = Read-Json (Join-Path $artifactRoot 'phase-lmax-r93-fix-session-boundary-evidence.json')
$market = Read-Json (Join-Path $artifactRoot 'phase-lmax-r93-marketdata-request-evidence.json')
$trace = Read-Json (Join-Path $artifactRoot 'phase-lmax-r93-operational-invocation-trace.json')
$boundary = Read-Json (Join-Path $artifactRoot 'phase-lmax-r93-boundary-evidence.json')
$marketResult = Read-Json (Join-Path $artifactRoot 'phase-lmax-r93-marketdata-sanitized-result.json')
$forbidden = Read-Json (Join-Path $artifactRoot 'phase-lmax-r93-forbidden-actions-audit.json')
$apiWorker = Read-Json (Join-Path $artifactRoot 'phase-lmax-r93-api-worker-fake-gateway-audit.json')
$usdJpy = Read-Json (Join-Path $artifactRoot 'phase-lmax-r93-usdjpy-caveat-preservation.json')
$shutdown = Read-Json (Join-Path $artifactRoot 'phase-lmax-r93-shutdown-revert-evidence.json')
$next = Read-Json (Join-Path $artifactRoot 'phase-lmax-r93-next-phase-recommendation.json')
$gate = Read-Json (Join-Path $artifactRoot 'phase-lmax-r93-gate-validation.json')
$r91 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r91-tls-success-classification-summary.json')

$allowedClassifications = @(
    'LMAX_R93_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED',
    'LMAX_R93_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED',
    'LMAX_R93_FAIL_R91_SUCCESS_EVIDENCE_MISSING',
    'LMAX_R93_FAIL_DEMO_ENDPOINT_BINDING_REGRESSION',
    'LMAX_R93_FAIL_PLACEHOLDER_HOST_USED',
    'LMAX_R93_FAIL_SOCKET_CONNECTOR_BINDING_REGRESSION',
    'LMAX_R93_FAIL_TLS_PROGRESSION_BINDING_REGRESSION',
    'LMAX_R93_FAIL_TLS_CLASSIFICATION_INSTRUMENTATION_REGRESSION',
    'LMAX_R93_FAIL_FIX_PROGRESSION_BINDING_REGRESSION',
    'LMAX_R93_FAIL_FIX_CREDENTIAL_MATERIAL_BINDING_REGRESSION',
    'LMAX_R93_FAIL_FIX_LOGON_FRAME_WRITE_BINDING_REGRESSION',
    'LMAX_R93_FAIL_FIX_WRITER_SUPPORTS_ORDER_FRAMES',
    'LMAX_R93_FAIL_REAL_SECRET_MATERIAL_NOT_ALLOWED_FOR_APPROVED_RETRY',
    'LMAX_R93_FAIL_REAL_SECRET_MATERIAL_NOT_LOADED_FOR_APPROVED_RETRY',
    'LMAX_R93_FAIL_FIX_ATTEMPT_ALLOWED_WITHOUT_TLS_SUCCESS',
    'LMAX_R93_FAIL_MARKETDATA_ATTEMPT_ALLOWED_WITHOUT_FIX_SUCCESS',
    'LMAX_R93_FAIL_MANUAL_EXECUTION_SURFACE_REGRESSION',
    'LMAX_R93_FAIL_REAL_BOUNDED_ADAPTER_BINDING_REGRESSION',
    'LMAX_R93_FAIL_NO_EXTERNAL_ADAPTER_USED_INSTEAD_OF_REAL_BOUNDARY_ADAPTER',
    'LMAX_R93_FAIL_EXECUTE_ONCE_NOT_INVOKED',
    'LMAX_R93_FAIL_PRE_EXTERNAL_APPROVAL_OR_COMPOSITION_REGRESSION',
    'LMAX_R93_FAIL_CREDENTIAL_CONFIG_MISSING',
    'LMAX_R93_FAIL_CREDENTIAL_CONFIG_NOT_APPROVED',
    'LMAX_R93_FAIL_CREDENTIAL_CONFIG_BINDING_REGRESSION',
    'LMAX_R93_FAIL_PRODUCTION_ACCOUNT_RISK',
    'LMAX_R93_FAIL_API_WORKER_GATEWAY_REGRESSION',
    'LMAX_R93_FAIL_LIVE_LAUNCHER_OR_BACKGROUND_SERVICE_RISK',
    'LMAX_R93_FAIL_FORBIDDEN_ACTION_RISK',
    'LMAX_R93_FAIL_USDJPY_CAVEAT_WEAKENED',
    'LMAX_R93_FAIL_PREFLIGHT_ABORTED',
    'LMAX_R93_FAIL_TCP_SOCKET_BOUNDARY',
    'LMAX_R93_FAIL_TLS_BOUNDARY',
    'LMAX_R93_FAIL_FIX_LOGON_OR_SESSION_BOUNDARY',
    'LMAX_R93_FAIL_MARKETDATA_REQUEST_BOUNDARY',
    'LMAX_R93_FAIL_MARKETDATA_RESPONSE_BOUNDARY',
    'LMAX_R93_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE',
    'LMAX_R93_FAIL_SHUTDOWN_REVERT_INCOMPLETE',
    'LMAX_R93_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK',
    'LMAX_R93_FAIL_BUILD_OR_TESTS'
)

if ($allowedClassifications -notcontains $summary.classification) {
    throw "Unexpected R93 classification: $($summary.classification)"
}

Require-Equal $summary.classification 'LMAX_R93_FAIL_TLS_BOUNDARY' 'R93 classification mismatch.'
Require-Equal $r91.classification 'LMAX_R91_PASS_TLS_SUCCESS_CLASSIFICATION_INSTRUMENTATION_READY_NO_EXTERNAL_ACTIVATION' 'R91 success evidence missing or mismatched.'
Require-True $summary.externalActivationAttempted 'External activation attempt not proven.'
Require-Equal $summary.attemptCount 1 'attemptCount must be exactly 1.'
Require-True $summary.retryPhaseReservationPassed 'LMAX-R93 retry phase reservation did not pass.'
Require-True $summary.manualCliSurfaceUsed 'Manual CLI use not proven.'
Require-Equal $summary.manualCliSurface 'QQ.Production.Intraday.Tools.LmaxReadOnlyActivation' 'Manual CLI surface mismatch.'
Require-True $summary.adapterModeRealBoundedExecutableReadOnlyUsed 'Real bounded adapter mode not used.'
Require-Equal $summary.adapterMode 'real-bounded-executable-readonly' 'Adapter mode mismatch.'
Require-True $summary.concreteDemoEndpointBindingUsed 'Concrete Demo endpoint binding not used.'
Require-True $summary.configuredSocketConnectorUsed 'Configured socket connector not used.'
Require-True $summary.lmaxReadOnlyActivationManualTcpSocketConnectorConnectReached 'Manual TCP connector was not reached.'
Require-True $summary.tcpSocketSucceeded 'TCP/socket did not succeed.'
Require-True $summary.tlsAttempted 'TLS attempt not proven.'
Require-True $summary.socketConnectorAuthenticateTlsReached 'AuthenticateTls not reached.'
Require-False $summary.tlsSucceeded 'R93 should not claim TLS success for this evidence pack.'
Require-Equal $summary.tlsResultCategory 'HandshakeException' 'TLS result category mismatch.'
Require-Equal $summary.tlsFailureCategory 'HandshakeException' 'TLS failure category mismatch.'
Require-False $summary.tlsTimedOut 'R93 TLS evidence should not be classified as timeout.'
Require-Equal $summary.tlsExceptionCategory 'HandshakeException' 'TLS exception category mismatch.'
Require-False $summary.tlsStreamAvailableForFix 'TLS stream should not be available for FIX after TLS failure.'
Require-False $summary.tlsRawMaterialSerialized 'TLS raw material was serialized.'
Require-False $summary.fixLogonSessionAttempted 'FIX logon/session should not be attempted after TLS did not succeed.'
Require-False $summary.socketConnectorOpenFixSessionReached 'OpenFixSession should not be reached after TLS did not succeed.'
Require-False $summary.sessionLogonOnlyFixFrameWriterUsed 'FIX writer should not be used after TLS did not succeed.'
Require-False $summary.rawFixFrameSerializedInArtifacts 'Raw FIX frame serialized.'
Require-True $summary.orderCapableFixFramePathAbsent 'Order-capable FIX path not proven absent.'
Require-False $summary.marketDataRequestAttempted 'MarketDataRequest was attempted without FIX success.'
Require-False $summary.marketDataResponseEntriesObserved 'MarketDataResponse entries should not be observed.'
Require-False $summary.credentialValuesReturned 'credentialValuesReturned must remain false.'
Require-False $summary.rawCredentialsFixFieldsMessagesTlsMaterialPrintedStoredSerialized 'Sensitive values were serialized.'
Require-Equal $summary.boundaryStatuses.credentialConfig 'ValidationOnly' 'Credential/config boundary status mismatch.'
Require-Equal $summary.boundaryStatuses.tcpSocket 'Succeeded' 'TCP/socket boundary status mismatch.'
Require-Equal $summary.boundaryStatuses.tls 'FailedExternal' 'TLS boundary status mismatch.'
Require-Equal $summary.boundaryStatuses.fixLogonSession 'NotAttempted' 'FIX boundary status mismatch.'
Require-Equal $summary.boundaryStatuses.marketDataRequest 'NotAttempted' 'MarketDataRequest status mismatch.'
Require-Equal $summary.boundaryStatuses.marketDataResponseEntries 'NotAttempted' 'MarketDataResponse status mismatch.'
Require-Equal $summary.boundaryStatuses.shutdownRevert 'Succeeded' 'Shutdown/revert status mismatch.'

Require-True $preflight.operatorApprovalExact 'Exact operator approval missing.'
Require-True $preflight.r91SuccessEvidencePresent 'R91 evidence missing.'
Require-True $preflight.retryPhaseReservationPassed 'Retry phase reservation missing.'
Require-True $preflight.realBoundedExecutableReadOnlyAdapterSelected 'Real bounded adapter not selected.'
Require-False $preflight.noExternalAdapterUsedInstead 'NoExternal adapter was used instead.'
Require-Equal $preflight.preflightResult 'PASS' 'Preflight did not pass.'

Require-Equal $endpoint.endpointMode 'Demo' 'Endpoint mode must be Demo.'
Require-True $endpoint.endpointPresent 'Endpoint not present.'
Require-True $endpoint.hostPresent 'Host not present.'
Require-True $endpoint.hostConcreteBinding 'Concrete host binding missing.'
Require-False $endpoint.hostWasPlaceholder 'Placeholder host used.'
Require-True $endpoint.portPresent 'Port not present.'
Require-True $endpoint.portConcreteBinding 'Concrete port binding missing.'
Require-True $endpoint.productionExcluded 'Production endpoint not excluded.'
Require-True $endpoint.endpointApproved 'Endpoint not approved.'
Require-False $endpoint.rawEndpointSerialized 'Raw endpoint serialized.'

Require-True $socket.configuredSocketConnectorUsed 'Configured socket connector not used.'
Require-True $socket.lmaxReadOnlyActivationManualTcpSocketConnectorConnectReached 'Connect not reached.'
Require-True $socket.tcpConnectionAttempted 'TCP not attempted.'
Require-True $socket.tcpSocketSucceeded 'TCP did not succeed.'
Require-False $socket.socketConnectorGlobalDefault 'Socket connector became global/default.'

Require-True $tls.tlsAttempted 'TLS not attempted.'
Require-True $tls.socketConnectorAuthenticateTlsReached 'AuthenticateTls not reached.'
Require-Equal $tls.tlsBoundaryResult 'FailedExternal' 'TLS boundary result mismatch.'
Require-Equal $tls.tlsBoundaryResultCategory 'HandshakeException' 'TLS boundary category mismatch.'
Require-False $tls.tlsSucceeded 'TLS should not be marked succeeded.'
Require-Equal $tls.fixNotAttemptedReason 'TlsBoundaryDidNotReturnSucceeded' 'FIX not-attempted reason mismatch.'
Require-False $tls.tlsRawMaterialSerialized 'Raw TLS material serialized.'
Require-False $tls.rawTlsExceptionDetailsSerialized 'Raw TLS exception details serialized.'

Require-True $tlsClassification.tlsAttempted 'TLS classification should show attempted.'
Require-False $tlsClassification.tlsSucceeded 'TLS classification should show non-success.'
Require-Equal $tlsClassification.tlsBoundaryStatus 'Failed' 'Sanitized TLS boundary status mismatch.'
Require-Equal $tlsClassification.tlsResultCategory 'HandshakeException' 'Sanitized TLS result category mismatch.'
Require-Equal $tlsClassification.tlsFailureCategory 'HandshakeException' 'Sanitized TLS failure category mismatch.'
Require-False $tlsClassification.tlsTimedOut 'TLS timeout classification mismatch.'
Require-Equal $tlsClassification.tlsExceptionCategory 'HandshakeException' 'Sanitized TLS exception category mismatch.'
Require-False $tlsClassification.tlsStreamAvailableForFix 'TLS stream should not be available for FIX.'
Require-False $tlsClassification.tlsRawMaterialSerialized 'Raw TLS material serialized.'
Require-True $tlsClassification.classificationInstrumentationWorked 'TLS classification instrumentation did not work.'
Require-True $tlsClassification.tlsSucceededVsAttemptedOnlyDistinct 'TLS success vs attempted-only not distinct.'
$requiredTlsCategories = @('Succeeded','AttemptedOnly','Timeout','HandshakeException','CertificateValidationFailure','StreamUnavailable','CancelledOrAborted','UnknownFailure','NotAttempted')
foreach ($category in $requiredTlsCategories) {
    if ($tlsClassification.supportedSanitizedTlsCategories -notcontains $category) {
        throw "Missing sanitized TLS category: $category"
    }
}

Require-True $credential.approvedDemoReadOnlyInMemoryFixLogonCredentialMaterialReady 'FIX credential material readiness missing.'
Require-True $credential.inMemoryOnlyFixCredentialMaterialAllowed 'FIX credential material should be allowed for approved manual path.'
Require-False $credential.inMemoryOnlyFixCredentialMaterialLoadedSanitizedBoolean 'FIX credential material should not be loaded when TLS fails before FIX.'
Require-Equal $credential.fixCredentialMaterialLoadSkippedReason 'TlsBoundaryDidNotReturnSucceeded' 'Credential load skipped reason mismatch.'
Require-False $credential.credentialValuesReturned 'Credential values returned.'
Require-False $credential.rawCredentialSerialized 'Raw credential serialized.'
Require-False $credential.credentialDerivedValuesSerialized 'Credential-derived values serialized.'
Require-True $credential.productionExcluded 'Production not excluded.'

Require-True $fixWrite.fixLogonBuilderReady 'FIX logon builder not ready.'
Require-True $fixWrite.fixFrameWriterReady 'FIX frame writer not ready.'
Require-True $fixWrite.sessionOnly 'FIX writer must be session-only.'
Require-False $fixWrite.orderFramesSupported 'FIX writer supports order frames.'
Require-False $fixWrite.sessionLogonOnlyFixFrameWriterUsed 'FIX writer should not be used after TLS failure.'
Require-Equal $fixWrite.fixWriterNotUsedReason 'TlsBoundaryDidNotReturnSucceeded' 'FIX writer not-used reason mismatch.'
Require-False $fixWrite.rawFixSerialized 'Raw FIX serialized.'
Require-True $fixWrite.orderCapableFixFramePathAbsent 'Order-capable FIX path not absent.'

Require-False $fix.fixLogonSessionAttempted 'FIX attempted unexpectedly.'
Require-False $fix.socketConnectorOpenFixSessionReached 'OpenFixSession reached unexpectedly.'
Require-Equal $fix.fixBoundaryResult 'NotAttempted' 'FIX boundary result mismatch.'
Require-Equal $fix.fixBoundaryResultCategory 'NotAttemptedAfterTlsBoundaryDidNotSucceed' 'FIX boundary category mismatch.'
Require-False $fix.fixAttemptedWithoutTlsSucceeded 'FIX attempted without TLS success.'
Require-False $fix.marketDataRequestAttemptedAfterFix 'MarketDataRequest attempted after failed/not-attempted FIX.'
Require-False $fix.rawFixMessagesSerialized 'Raw FIX messages serialized.'
Require-False $fix.rawSensitiveFixLogsSerialized 'Raw sensitive FIX logs serialized.'

Require-False $market.marketDataRequestAttempted 'MarketDataRequest attempted.'
Require-Equal $market.marketDataRequestResultCategory 'NotAttemptedAfterTlsBoundaryDidNotSucceed' 'MarketDataRequest category mismatch.'
Require-False $market.marketDataAttemptedWithoutFixSucceeded 'MarketData attempted without FIX success.'
if (($market.approvedInstruments -join ',') -ne 'GBPUSD,EURGBP,AUDUSD,USDJPY') {
    throw 'Approved instruments differ from GBPUSD, EURGBP, AUDUSD, USDJPY.'
}
Require-False $market.nonApprovedInstrumentsUsed 'Non-approved instruments used.'

Require-Equal $trace.attemptCount 1 'Trace attemptCount invalid.'
Require-True $trace.callOnceInvoked 'CallOnce not invoked.'
Require-True $trace.invokeOnceInvoked 'InvokeOnce not invoked.'
Require-True $trace.executeOnceInvoked 'ExecuteOnce not invoked.'
Require-True $trace.externalActivationAttempted 'Trace does not show external activation.'
Require-True $trace.tcpConnectionAttempted 'Trace does not show TCP attempt.'
Require-True $trace.tlsHandshakeAttempted 'Trace does not show TLS attempt.'
Require-False $trace.fixLogonAttempted 'Trace shows FIX attempted.'
Require-False $trace.marketDataRequestSent 'Trace shows market data sent.'
Require-True $trace.shutdownRevertCompleted 'Trace shutdown/revert incomplete.'
Require-False $trace.credentialValuesReturned 'Trace shows credential values returned.'
Require-True $trace.outputSanitized 'Trace output not sanitized.'

Require-Equal $boundary.credentialConfig 'ValidationOnly' 'Boundary credential/config mismatch.'
Require-Equal $boundary.tcpSocket 'Succeeded' 'Boundary TCP mismatch.'
Require-Equal $boundary.tls 'FailedExternal' 'Boundary TLS mismatch.'
Require-Equal $boundary.fixLogonSession 'NotAttempted' 'Boundary FIX mismatch.'
Require-Equal $boundary.marketDataRequest 'NotAttempted' 'Boundary MarketDataRequest mismatch.'
Require-Equal $boundary.marketDataResponseEntries 'NotAttempted' 'Boundary entries mismatch.'
Require-Equal $boundary.shutdownRevert 'Succeeded' 'Boundary shutdown mismatch.'
Require-True $boundary.fixBlockedAfterTlsNonSuccess 'FIX not blocked after TLS non-success.'
Require-True $boundary.marketDataBlockedAfterTlsNonSuccess 'MarketData not blocked after TLS non-success.'

Require-False $marketResult.marketDataRequestAttempted 'Market data result says request attempted.'
Require-False $marketResult.marketDataResponseEntriesObserved 'Market data entries observed unexpectedly.'
Require-Equal $marketResult.entryCount 0 'Market data entry count should be zero.'
Require-False $marketResult.rawMarketDataSerialized 'Raw market data serialized.'

Require-Equal $forbidden.result 'PASS' 'Forbidden actions audit did not pass.'
Require-False $forbidden.ordersSubmitted 'Orders submitted.'
Require-False $forbidden.newOrderSingleUsed 'NewOrderSingle used.'
Require-False $forbidden.cancelReplaceUsed 'Cancel/replace used.'
Require-False $forbidden.tradingEnabled 'Trading enabled.'
Require-False $forbidden.tradingStateMutated 'Trading state mutated.'
Require-False $forbidden.productionAccountUsed 'Production account used.'
Require-False $forbidden.apiStartupUsed 'API startup used.'
Require-False $forbidden.workerStartupUsed 'Worker startup used.'
Require-False $forbidden.hostedBackgroundServiceIntroduced 'Hosted/background service introduced.'
Require-False $forbidden.schedulerPollingLoopUsed 'Scheduler/polling loop used.'
Require-False $forbidden.replayUsed 'Replay used.'
Require-False $forbidden.shadowReplayUsed 'Shadow replay used.'
Require-False $forbidden.shadowReplaySubmitUsed 'Shadow replay submit used.'
Require-False $forbidden.fixAttemptedWithoutTlsSucceeded 'FIX attempted without TLS success.'
Require-False $forbidden.marketDataAttemptedWithoutFixSucceeded 'MarketData attempted without FIX success.'
Require-True $forbidden.orderCapableFixFramePathAbsent 'Order-capable FIX path not absent.'

Require-Equal $apiWorker.result 'PASS' 'API/Worker audit did not pass.'
Require-True $apiWorker.apiWorkerFakeLmaxGatewayOnly 'API/Worker not FakeLmaxGatewayOnly.'
Require-False $apiWorker.apiGatewayChangedAwayFromFake 'API gateway changed away from fake.'
Require-False $apiWorker.workerGatewayChangedAwayFromFake 'Worker gateway changed away from fake.'
Require-False $apiWorker.manualCliReachableFromApiWorkerDefaultStartup 'Manual CLI reachable from API/Worker default startup.'
Require-False $apiWorker.realAdapterGlobalDefault 'Real adapter became global/default.'

Require-True $usdJpy.caveatPreserved 'USDJPY caveat not preserved.'
Require-Equal $usdJpy.securityId '4004' 'USDJPY SecurityID mismatch.'
Require-Equal $usdJpy.securityIdSource '8' 'USDJPY SecurityIDSource mismatch.'
Require-Equal $usdJpy.caveat 'prior failed-safe root cause remains unproven' 'USDJPY caveat weakened.'

Require-Equal $shutdown.shutdownRevert 'Succeeded' 'Shutdown/revert evidence mismatch.'
Require-True $shutdown.shutdownRevertCompleted 'Shutdown/revert not completed.'
Require-True $shutdown.socketClosedOrDisposed 'Socket close/dispose not evidenced.'
Require-False $shutdown.tradingStateMutated 'Trading state mutated during shutdown.'

Require-Equal $next.nextRecommendedPhase 'LMAX-R94' 'Next phase recommendation missing or incorrect.'
Require-Equal $next.nextRecommendedTitle 'TLS Runtime Boundary Root-Cause Pack' 'Next phase title mismatch.'
Require-True $next.r94MustNotPerformActivation 'R94 no-activation constraint missing.'

Require-Equal $gate.gateValidation 'PASS' 'Gate validation mismatch.'
Require-True $gate.buildEvidencePresent 'Build evidence missing.'
Require-True $gate.testEvidencePresent 'Test evidence missing.'
Require-True $gate.sanitizationEvidencePresent 'Sanitization evidence missing.'
Require-True $gate.nextPhaseRecommendationPresent 'Next phase recommendation missing.'

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r93-*' -File |
    Where-Object { $_.Name -ne 'phase-lmax-r93-operator-approval-note.md' } |
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
    if ($artifactText -match $pattern) {
        throw "Forbidden sensitive or trading pattern found in R93 artifacts: $pattern"
    }
}

Write-Output 'LMAX_R93_VALIDATION_PASS'
