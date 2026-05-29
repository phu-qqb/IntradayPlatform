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

$summary = Read-Json (Join-Path $artifactRoot 'phase-lmax-r94-tls-runtime-boundary-root-cause-summary.json')
$beforeAfter = Read-Json (Join-Path $artifactRoot 'phase-lmax-r94-r93-boundary-before-after-classification.json')
$handshake = Read-Json (Join-Path $artifactRoot 'phase-lmax-r94-r93-tls-handshake-exception-review.json')
$sni = Read-Json (Join-Path $artifactRoot 'phase-lmax-r94-tls-sni-host-binding-review.json')
$protocol = Read-Json (Join-Path $artifactRoot 'phase-lmax-r94-tls-protocol-and-certificate-review.json')
$exception = Read-Json (Join-Path $artifactRoot 'phase-lmax-r94-tls-exception-sanitized-category-review.json')
$fixBlock = Read-Json (Join-Path $artifactRoot 'phase-lmax-r94-fix-block-after-tls-failure-review.json')
$marketBlock = Read-Json (Join-Path $artifactRoot 'phase-lmax-r94-marketdata-block-after-tls-failure-review.json')
$path = Read-Json (Join-Path $artifactRoot 'phase-lmax-r94-real-bounded-path-validation.json')
$noExternal = Read-Json (Join-Path $artifactRoot 'phase-lmax-r94-no-external-boundary-attempted.json')
$forbidden = Read-Json (Join-Path $artifactRoot 'phase-lmax-r94-forbidden-actions-audit.json')
$apiWorker = Read-Json (Join-Path $artifactRoot 'phase-lmax-r94-api-worker-fake-gateway-audit.json')
$scheduler = Read-Json (Join-Path $artifactRoot 'phase-lmax-r94-no-scheduler-polling-service-audit.json')
$sanitize = Read-Json (Join-Path $artifactRoot 'phase-lmax-r94-credential-endpoint-tls-fix-sanitization-validation.json')
$usdJpy = Read-Json (Join-Path $artifactRoot 'phase-lmax-r94-usdjpy-caveat-preservation.json')
$next = Read-Json (Join-Path $artifactRoot 'phase-lmax-r94-next-phase-recommendation.json')
$gate = Read-Json (Join-Path $artifactRoot 'phase-lmax-r94-gate-validation.json')
$r93 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r93-temporary-readonly-activation-retry-summary.json')
$r93Tls = Read-Json (Join-Path $artifactRoot 'phase-lmax-r93-tls-classification-evidence.json')

$allowedClassifications = @(
    'LMAX_R94_PASS_TLS_HANDSHAKE_EXCEPTION_ROOT_CAUSE_IDENTIFIED_NO_EXTERNAL_ACTIVATION',
    'LMAX_R94_PASS_TLS_HANDSHAKE_EXCEPTION_SNI_OR_HOST_BINDING_SUSPECT_NO_EXTERNAL_ACTIVATION',
    'LMAX_R94_PASS_TLS_HANDSHAKE_EXCEPTION_PROTOCOL_OR_ENDPOINT_MISMATCH_SUSPECT_NO_EXTERNAL_ACTIVATION',
    'LMAX_R94_PASS_TLS_HANDSHAKE_EXCEPTION_CERTIFICATE_VALIDATION_NOT_PROVEN_NO_EXTERNAL_ACTIVATION',
    'LMAX_R94_PASS_TLS_HANDSHAKE_EXCEPTION_REMOTE_CLOSE_OR_EXTERNAL_FAILURE_SUSPECT_NO_EXTERNAL_ACTIVATION',
    'LMAX_R94_PASS_TLS_HANDSHAKE_EXCEPTION_SANITIZED_CAUSE_INCONCLUSIVE_NO_EXTERNAL_ACTIVATION',
    'LMAX_R94_PASS_TLS_HANDSHAKE_CONFIG_FIX_READY_NO_EXTERNAL_ACTIVATION',
    'LMAX_R94_FAIL_TLS_HANDSHAKE_ROOT_CAUSE_NOT_PROVABLE',
    'LMAX_R94_FAIL_TLS_SNI_HOST_REVIEW_MISSING',
    'LMAX_R94_FAIL_TLS_EXCEPTION_SANITIZATION_RISK',
    'LMAX_R94_FAIL_FIX_ALLOWED_AFTER_TLS_FAILURE',
    'LMAX_R94_FAIL_MARKETDATA_ALLOWED_AFTER_TLS_FAILURE',
    'LMAX_R94_FAIL_EXTERNAL_BOUNDARY_ATTEMPTED',
    'LMAX_R94_FAIL_FORBIDDEN_ACTION_INTRODUCED',
    'LMAX_R94_FAIL_CREDENTIAL_VALUES_RETURNED_OR_SECRET_RISK',
    'LMAX_R94_FAIL_API_WORKER_GATEWAY_REGRESSION',
    'LMAX_R94_FAIL_USDJPY_CAVEAT_WEAKENED',
    'LMAX_R94_FAIL_BUILD_OR_TESTS'
)

if ($allowedClassifications -notcontains $summary.classification) {
    throw "Unexpected R94 classification: $($summary.classification)"
}

Require-Equal $summary.classification 'LMAX_R94_PASS_TLS_HANDSHAKE_EXCEPTION_SANITIZED_CAUSE_INCONCLUSIVE_NO_EXTERNAL_ACTIVATION' 'R94 classification mismatch.'
Require-Equal $r93.classification 'LMAX_R93_FAIL_TLS_BOUNDARY' 'R93 classification missing or mismatched.'
Require-True $r93.tcpSocketSucceeded 'R93 TCP success evidence missing.'
Require-True $r93.tlsAttempted 'R93 TLS attempt evidence missing.'
Require-False $r93.tlsSucceeded 'R93 TLS should not be succeeded.'
Require-Equal $r93.tlsResultCategory 'HandshakeException' 'R93 TLS HandshakeException evidence missing.'
Require-Equal $r93Tls.tlsResultCategory 'HandshakeException' 'R93 TLS classification evidence missing.'
Require-True $summary.r93TcpSuccessProven 'R93 TCP success not proven in R94 summary.'
Require-True $summary.r93TlsHandshakeExceptionProven 'R93 TLS HandshakeException not proven.'
Require-True $summary.timeoutExcluded 'Timeout exclusion missing.'
Require-Equal $summary.sanitizedTlsFailureCategory 'HandshakeException' 'Sanitized TLS failure category mismatch.'
Require-True $summary.sniHostBindingConcreteNonPlaceholderSanitizedOnly 'SNI/host binding review missing.'
Require-False $summary.sniHostMismatchSuspected 'SNI/host mismatch should not be suspected from R94 evidence.'
Require-False $summary.certificateValidationFailureProven 'Certificate validation failure should not be proven.'
Require-False $summary.protocolEndpointMismatchSuspected 'Protocol/endpoint mismatch should not be specifically suspected.'
Require-True $summary.remoteCloseExternalFailureSuspected 'Remote/external failure suspect class missing.'
Require-True $summary.sanitizedCauseInconclusive 'Sanitized inconclusive conclusion missing.'
Require-True $summary.fixBlockedAfterTlsFailure 'FIX block after TLS failure missing.'
Require-True $summary.marketDataRequestBlockedAfterTlsFailure 'MarketData block after TLS failure missing.'
Require-False $summary.externalActivationAttemptedInR94 'External activation attempted during R94.'
Require-False $summary.credentialEndpointTlsFixSensitiveValuesPrintedStoredSerialized 'Sensitive values serialized.'
Require-False $summary.credentialValuesReturned 'credentialValuesReturned must be false.'

if (($summary.responsibleClassFactoryBindingOperation -join '|') -notmatch 'LmaxReadOnlyActivationManualTcpSocketConnector\.AuthenticateTls') {
    throw 'Responsible TLS operation not named.'
}

Require-Equal $beforeAfter.r93TlsResultCategory 'HandshakeException' 'Before/after TLS category mismatch.'
Require-False $beforeAfter.r94ExternalBoundaryAttempted 'R94 external boundary attempted.'

Require-True $handshake.r93TlsAttempted 'Handshake review missing TLS attempt.'
Require-True $handshake.r93AuthenticateTlsReached 'Handshake review missing AuthenticateTls reachability.'
Require-True $handshake.r93TcpSucceededBeforeTls 'Handshake review missing TCP success.'
Require-True $handshake.sslStreamAuthenticateAsClientPathInvokedOrPrepared 'SslStream AuthenticateAsClient path not reviewed.'
Require-Equal $handshake.tlsResultCategory 'HandshakeException' 'Handshake review category mismatch.'
Require-False $handshake.tlsTimedOut 'Handshake review timeout exclusion missing.'
Require-True $handshake.timeoutExcluded 'Timeout exclusion missing.'
Require-False $handshake.rawTlsExceptionDetailsSerialized 'Raw TLS exception details serialized.'
Require-False $handshake.rawTlsMaterialSerialized 'Raw TLS material serialized.'

Require-Equal $sni.endpointMode 'Demo' 'SNI endpoint mode mismatch.'
Require-True $sni.endpointPresent 'Endpoint not present.'
Require-True $sni.hostPresent 'Host not present.'
Require-True $sni.hostConcreteBinding 'Concrete host binding missing.'
Require-False $sni.hostWasPlaceholder 'Placeholder host used.'
Require-True $sni.portPresent 'Port not present.'
Require-True $sni.portConcreteBinding 'Concrete port binding missing.'
Require-True $sni.productionExcluded 'Production endpoint not excluded.'
Require-True $sni.endpointApproved 'Endpoint not approved.'
Require-True $sni.tcpRuntimeHostConcrete 'TCP runtime host concrete evidence missing.'
Require-True $sni.tlsSniUsesRuntimeHost 'TLS SNI runtime host binding missing.'
Require-True $sni.tlsSniHostSameAsTcpHost 'TLS SNI/TCP host consistency missing.'
Require-False $sni.sniHostMismatchSuspected 'SNI host mismatch suspected unexpectedly.'
Require-False $sni.rawHostSerialized 'Raw host serialized.'
Require-False $sni.rawPortSerialized 'Raw port serialized.'

Require-Equal $protocol.tlsProtocolSelection 'SystemDefault' 'TLS protocol selection review mismatch.'
Require-Equal $protocol.certificateValidationPolicy 'SystemDefaultValidation' 'Certificate validation policy mismatch.'
Require-False $protocol.certificateValidationFailureProven 'Certificate validation failure should not be proven.'
Require-False $protocol.certificateValidationFailureCategoryObserved 'Certificate validation category observed unexpectedly.'
Require-False $protocol.protocolEndpointMismatchProven 'Protocol/endpoint mismatch should not be proven.'
Require-False $protocol.rawCertificateDetailsSerialized 'Raw certificate details serialized.'
Require-False $protocol.rawTlsMaterialSerialized 'Raw TLS material serialized.'

Require-Equal $exception.sanitizedTlsFailureCategory 'HandshakeException' 'Exception review category mismatch.'
Require-True $exception.timeoutExcluded 'Exception review timeout exclusion missing.'
Require-True $exception.attemptedOnlyExcluded 'Attempted-only exclusion missing.'
Require-False $exception.certificateValidationFailureProven 'Certificate validation failure proven unexpectedly.'
Require-True $exception.remoteCloseExternalFailureSuspected 'Remote/external suspect class missing.'
Require-False $exception.specificUnderlyingCauseProven 'Specific underlying cause should not be proven.'
Require-True $exception.sanitizedCauseInconclusive 'Sanitized inconclusive result missing.'
Require-False $exception.rawExceptionMessageSerialized 'Raw exception message serialized.'
Require-False $exception.rawExceptionStackSerialized 'Raw exception stack serialized.'

Require-False $fixBlock.tlsSucceeded 'TLS should be non-success in fix block review.'
Require-False $fixBlock.fixLogonSessionAttempted 'FIX attempted after TLS failure.'
Require-False $fixBlock.socketConnectorOpenFixSessionReached 'OpenFixSession reached after TLS failure.'
Require-True $fixBlock.fixBlockedAfterTlsFailure 'FIX not blocked after TLS failure.'
Require-False $fixBlock.tlsSucceededGateWeakened 'TLS success gate weakened.'

Require-False $marketBlock.marketDataRequestAttempted 'MarketDataRequest attempted after TLS failure.'
Require-False $marketBlock.marketDataResponseEntriesObserved 'MarketDataResponse entries observed.'
Require-True $marketBlock.marketDataRequestBlockedAfterTlsFailure 'MarketDataRequest not blocked after TLS failure.'
Require-True $marketBlock.marketDataRequestBlockedWithoutFixSuccess 'MarketDataRequest not blocked without FIX success.'

Require-True $path.realBoundedPathValidated 'Real bounded path validation missing.'
Require-True $path.tlsContinuationBindingPresent 'TLS continuation binding missing.'
Require-True $path.noExternalDefaultPreserved 'No-external default not preserved.'
Require-False $path.realAdapterGlobalDefault 'Real adapter became global/default.'
Require-False $path.apiWorkerReachable 'Manual path reachable from API/Worker.'
Require-False $path.externalBoundaryAttemptedDuringR94 'External boundary attempted during R94.'

Require-False $noExternal.externalActivationAttempted 'External activation attempted during R94.'
Require-False $noExternal.tcpSocketAttempted 'TCP attempted during R94.'
Require-False $noExternal.tlsAttempted 'TLS attempted during R94.'
Require-False $noExternal.fixLogonAttempted 'FIX attempted during R94.'
Require-False $noExternal.marketDataRequestAttempted 'MarketDataRequest attempted during R94.'

Require-Equal $forbidden.result 'PASS' 'Forbidden actions audit failed.'
Require-False $forbidden.ordersSubmitted 'Orders submitted.'
Require-False $forbidden.newOrderSingleUsed 'NewOrderSingle used.'
Require-False $forbidden.cancelReplaceUsed 'Cancel/replace used.'
Require-False $forbidden.tradingEnabled 'Trading enabled.'
Require-False $forbidden.tradingStateMutated 'Trading state mutated.'
Require-False $forbidden.productionAccountUsed 'Production account used.'
Require-False $forbidden.schedulerPollingLoopUsed 'Scheduler/polling loop used.'
Require-False $forbidden.fixAllowedAfterTlsFailure 'FIX allowed after TLS failure.'
Require-False $forbidden.marketDataAllowedAfterTlsFailure 'MarketData allowed after TLS failure.'

Require-Equal $apiWorker.result 'PASS' 'API/Worker audit failed.'
Require-True $apiWorker.apiWorkerFakeLmaxGatewayOnly 'API/Worker gateway changed away from FakeLmaxGatewayOnly.'
Require-False $apiWorker.manualCliReachableFromApiWorkerDefaultStartup 'Manual CLI reachable from API/Worker default startup.'
Require-False $apiWorker.realAdapterGlobalDefault 'Real adapter became global/default.'

Require-Equal $scheduler.result 'PASS' 'Scheduler/polling/service audit failed.'
Require-False $scheduler.hostedBackgroundServiceIntroduced 'Hosted/background service introduced.'
Require-False $scheduler.schedulerIntroduced 'Scheduler introduced.'
Require-False $scheduler.pollingLoopIntroduced 'Polling loop introduced.'
Require-False $scheduler.replayIntroduced 'Replay introduced.'
Require-False $scheduler.shadowReplayIntroduced 'Shadow replay introduced.'

Require-Equal $sanitize.result 'PASS' 'Sanitization validation failed.'
Require-False $sanitize.credentialValuesReturned 'Credential values returned.'
Require-False $sanitize.rawCredentialsSerialized 'Raw credentials serialized.'
Require-False $sanitize.rawEndpointValuesSerialized 'Raw endpoint values serialized.'
Require-False $sanitize.rawTlsMaterialSerialized 'Raw TLS material serialized.'
Require-False $sanitize.rawCertificateDetailsSerialized 'Raw certificate details serialized.'
Require-False $sanitize.rawTlsExceptionDetailsSerialized 'Raw TLS exception details serialized.'
Require-False $sanitize.rawFixMessagesSerialized 'Raw FIX messages serialized.'
Require-False $sanitize.rawSensitiveFixLogsSerialized 'Raw sensitive FIX logs serialized.'

Require-True $usdJpy.caveatPreserved 'USDJPY caveat not preserved.'
Require-Equal $usdJpy.securityId '4004' 'USDJPY SecurityID mismatch.'
Require-Equal $usdJpy.securityIdSource '8' 'USDJPY SecurityIDSource mismatch.'
Require-Equal $usdJpy.caveat 'prior failed-safe root cause remains unproven' 'USDJPY caveat weakened.'

Require-Equal $next.nextRecommendedPhase 'LMAX-R95' 'Next phase recommendation missing.'
Require-Equal $next.nextRecommendedTitle 'Operator-Approved Single Temporary Demo Read-Only Activation Retry After TLS Handshake Root-Cause Review' 'Next phase title mismatch.'
Require-True $next.requiresExplicitOperatorApproval 'Next activation approval requirement missing.'

Require-Equal $gate.gateValidation 'PASS' 'Gate validation mismatch.'
Require-True $gate.r93TlsHandshakeExceptionEvidencePresent 'R93 TLS evidence missing from gate.'
Require-True $gate.r93TcpSuccessEvidencePresent 'R93 TCP evidence missing from gate.'
Require-True $gate.timeoutExclusionPresent 'Timeout exclusion missing from gate.'
Require-True $gate.tlsSniHostBindingReviewPresent 'SNI/host review missing from gate.'
Require-True $gate.tlsProtocolCertificateReviewPresent 'TLS protocol/certificate review missing from gate.'
Require-True $gate.sanitizationEvidencePresent 'Sanitization evidence missing from gate.'
Require-True $gate.buildEvidencePresent 'Build evidence missing from gate.'
Require-True $gate.testEvidencePresent 'Test evidence missing from gate.'
Require-True $gate.nextPhaseRecommendationPresent 'Next phase recommendation missing from gate.'

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r94-*' -File |
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
    'private key',
    'fix-marketdata',
    'london-demo',
    'lmax\.com'
)

foreach ($pattern in $forbiddenPatterns) {
    if ($artifactText -match $pattern) {
        throw "Forbidden sensitive endpoint, TLS, credential, or FIX pattern found in R94 artifacts: $pattern"
    }
}

Write-Output 'LMAX_R94_VALIDATION_PASS'
