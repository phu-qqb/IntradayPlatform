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
    if ($value -ne $true) { throw $message }
}

function Require-False($value, $message) {
    if ($value -ne $false) { throw $message }
}

function Require-Equal($actual, $expected, $message) {
    if ($actual -ne $expected) {
        throw "$message Expected '$expected' but found '$actual'."
    }
}

$summary = Read-Json (Join-Path $artifactRoot 'phase-lmax-r96-tls-runtime-boundary-root-cause-summary.json')
$beforeAfter = Read-Json (Join-Path $artifactRoot 'phase-lmax-r96-r95-boundary-before-after-classification.json')
$handshake = Read-Json (Join-Path $artifactRoot 'phase-lmax-r96-r93-r95-handshake-exception-review.json')
$comparison = Read-Json (Join-Path $artifactRoot 'phase-lmax-r96-r81-r85-vs-r93-r95-tls-comparison.json')
$codeChange = Read-Json (Join-Path $artifactRoot 'phase-lmax-r96-tls-code-change-impact-review.json')
$sni = Read-Json (Join-Path $artifactRoot 'phase-lmax-r96-tls-sni-host-binding-review.json')
$protocol = Read-Json (Join-Path $artifactRoot 'phase-lmax-r96-tls-protocol-and-certificate-review.json')
$exception = Read-Json (Join-Path $artifactRoot 'phase-lmax-r96-tls-exception-sanitized-category-review.json')
$fixBlock = Read-Json (Join-Path $artifactRoot 'phase-lmax-r96-fix-block-after-tls-failure-review.json')
$marketBlock = Read-Json (Join-Path $artifactRoot 'phase-lmax-r96-marketdata-block-after-tls-failure-review.json')
$path = Read-Json (Join-Path $artifactRoot 'phase-lmax-r96-real-bounded-path-validation.json')
$noExternal = Read-Json (Join-Path $artifactRoot 'phase-lmax-r96-no-external-boundary-attempted.json')
$forbidden = Read-Json (Join-Path $artifactRoot 'phase-lmax-r96-forbidden-actions-audit.json')
$apiWorker = Read-Json (Join-Path $artifactRoot 'phase-lmax-r96-api-worker-fake-gateway-audit.json')
$scheduler = Read-Json (Join-Path $artifactRoot 'phase-lmax-r96-no-scheduler-polling-service-audit.json')
$sanitize = Read-Json (Join-Path $artifactRoot 'phase-lmax-r96-credential-endpoint-tls-fix-sanitization-validation.json')
$usdJpy = Read-Json (Join-Path $artifactRoot 'phase-lmax-r96-usdjpy-caveat-preservation.json')
$next = Read-Json (Join-Path $artifactRoot 'phase-lmax-r96-next-phase-recommendation.json')
$gate = Read-Json (Join-Path $artifactRoot 'phase-lmax-r96-gate-validation.json')
$r81 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r81-temporary-readonly-activation-retry-summary.json')
$r85 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r85-temporary-readonly-activation-retry-summary.json')
$r93 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r93-temporary-readonly-activation-retry-summary.json')
$r95 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r95-temporary-readonly-activation-retry-summary.json')

$allowedClassifications = @(
    'LMAX_R96_PASS_TLS_HANDSHAKE_EXCEPTION_ROOT_CAUSE_IDENTIFIED_NO_EXTERNAL_ACTIVATION',
    'LMAX_R96_PASS_TLS_HANDSHAKE_EXCEPTION_REPEATED_EXTERNAL_OR_INTERMITTENT_SUSPECT_NO_EXTERNAL_ACTIVATION',
    'LMAX_R96_PASS_TLS_HANDSHAKE_EXCEPTION_CODE_CHANGE_REGRESSION_SUSPECT_NO_EXTERNAL_ACTIVATION',
    'LMAX_R96_PASS_TLS_HANDSHAKE_EXCEPTION_SNI_OR_HOST_BINDING_SUSPECT_NO_EXTERNAL_ACTIVATION',
    'LMAX_R96_PASS_TLS_HANDSHAKE_EXCEPTION_PROTOCOL_OR_ENDPOINT_MISMATCH_SUSPECT_NO_EXTERNAL_ACTIVATION',
    'LMAX_R96_PASS_TLS_HANDSHAKE_EXCEPTION_CERTIFICATE_VALIDATION_NOT_PROVEN_NO_EXTERNAL_ACTIVATION',
    'LMAX_R96_PASS_TLS_HANDSHAKE_EXCEPTION_SANITIZED_CAUSE_INCONCLUSIVE_NO_EXTERNAL_ACTIVATION',
    'LMAX_R96_PASS_TLS_HANDSHAKE_CONFIG_FIX_READY_NO_EXTERNAL_ACTIVATION',
    'LMAX_R96_FAIL_TLS_HANDSHAKE_ROOT_CAUSE_NOT_PROVABLE',
    'LMAX_R96_FAIL_TLS_COMPARISON_MISSING',
    'LMAX_R96_FAIL_TLS_CODE_CHANGE_REVIEW_MISSING',
    'LMAX_R96_FAIL_TLS_SNI_HOST_REVIEW_MISSING',
    'LMAX_R96_FAIL_TLS_EXCEPTION_SANITIZATION_RISK',
    'LMAX_R96_FAIL_FIX_ALLOWED_AFTER_TLS_FAILURE',
    'LMAX_R96_FAIL_MARKETDATA_ALLOWED_AFTER_TLS_FAILURE',
    'LMAX_R96_FAIL_EXTERNAL_BOUNDARY_ATTEMPTED',
    'LMAX_R96_FAIL_FORBIDDEN_ACTION_INTRODUCED',
    'LMAX_R96_FAIL_CREDENTIAL_VALUES_RETURNED_OR_SECRET_RISK',
    'LMAX_R96_FAIL_API_WORKER_GATEWAY_REGRESSION',
    'LMAX_R96_FAIL_USDJPY_CAVEAT_WEAKENED',
    'LMAX_R96_FAIL_BUILD_OR_TESTS'
)

if ($allowedClassifications -notcontains $summary.classification) {
    throw "Unexpected R96 classification: $($summary.classification)"
}

Require-Equal $summary.classification 'LMAX_R96_PASS_TLS_HANDSHAKE_EXCEPTION_REPEATED_EXTERNAL_OR_INTERMITTENT_SUSPECT_NO_EXTERNAL_ACTIVATION' 'R96 classification mismatch.'
Require-False $summary.newActivationPerformed 'R96 performed an activation.'
Require-True $summary.r95TlsHandshakeExceptionProven 'R95 TLS HandshakeException evidence missing.'
Require-True $summary.r93TlsHandshakeExceptionProven 'R93 TLS HandshakeException evidence missing.'
Require-True $summary.r81R85TlsSucceededEvidenceProven 'R81/R85 TLS success comparison missing.'
Require-True $summary.timeoutExcluded 'Timeout exclusion missing.'
Require-Equal $summary.sanitizedTlsFailureCategory 'HandshakeException' 'Sanitized TLS category mismatch.'
Require-True $summary.endpointSniHostBindingConsistentAcrossAttempts 'Endpoint/SNI consistency missing.'
Require-False $summary.codeChangesAfterR85PlausiblyAffectedTlsBehavior 'Code change regression should not be suspected.'
Require-False $summary.r91InstrumentationChangedBehavior 'R91 instrumentation should not change behavior.'
Require-True $summary.r91InstrumentationChangedClassificationOnly 'R91 classification-only evidence missing.'
Require-False $summary.r87FixWriterChangesPlausiblyAffectedTlsBehavior 'R87 FIX writer should not affect TLS behavior.'
Require-False $summary.sniHostMismatchSuspected 'SNI/host mismatch suspected unexpectedly.'
Require-False $summary.certificateValidationFailureProven 'Certificate validation failure should not be proven.'
Require-False $summary.protocolEndpointMismatchSuspected 'Protocol/endpoint mismatch suspected unexpectedly.'
Require-True $summary.remoteCloseExternalIntermittentBehaviorSuspected 'Repeated external/intermittent suspect class missing.'
Require-True $summary.noExternalFixIdentified 'No-external fix decision missing.'
Require-True $summary.anotherBoundedRetryWarranted 'Next retry decision missing.'
Require-True $summary.fixBlockedAfterTlsFailure 'FIX block after TLS failure missing.'
Require-True $summary.marketDataRequestBlockedAfterTlsFailure 'MarketData block after TLS failure missing.'
Require-False $summary.externalActivationAttemptedInR96 'External activation attempted during R96.'
Require-False $summary.credentialValuesReturned 'credentialValuesReturned must be false.'
Require-False $summary.credentialEndpointTlsFixSensitiveValuesPrintedStoredSerialized 'Sensitive values serialized.'

if (($summary.responsibleClassFactoryBindingOperation -join '|') -notmatch 'LmaxReadOnlyActivationManualTcpSocketConnector\.AuthenticateTls') {
    throw 'Responsible TLS operation not named.'
}

Require-Equal $r81.tlsBoundaryResult 'Succeeded' 'R81 TLS success evidence missing.'
Require-Equal $r85.tlsBoundaryResult 'Succeeded' 'R85 TLS success evidence missing.'
Require-Equal $r93.tlsResultCategory 'HandshakeException' 'R93 HandshakeException missing.'
Require-Equal $r95.tlsResultCategory 'HandshakeException' 'R95 HandshakeException missing.'
Require-True $r81.tcpSocketSucceeded 'R81 TCP success missing.'
Require-True $r85.tcpSocketSucceeded 'R85 TCP success missing.'
Require-True $r93.tcpSocketSucceeded 'R93 TCP success missing.'
Require-True $r95.tcpSocketSucceeded 'R95 TCP success missing.'

Require-Equal $beforeAfter.r95TlsResultCategory 'HandshakeException' 'Before/after R95 category mismatch.'
Require-False $beforeAfter.r96ExternalBoundaryAttempted 'R96 external boundary attempted.'

Require-True $handshake.repeatedHandshakeExceptionProven 'Repeated handshake exception not proven.'
Require-True $handshake.timeoutExcludedAcrossFailures 'Timeout exclusion across failures missing.'
Require-True $handshake.attemptedOnlyExcludedAcrossFailures 'Attempted-only exclusion missing.'
Require-False $handshake.rawTlsExceptionDetailsSerialized 'Raw TLS exception details serialized.'
Require-False $handshake.r93.tlsTimedOut 'R93 timeout should be false.'
Require-False $handshake.r95.tlsTimedOut 'R95 timeout should be false.'
Require-Equal $handshake.r93.tlsResultCategory 'HandshakeException' 'R93 review category mismatch.'
Require-Equal $handshake.r95.tlsResultCategory 'HandshakeException' 'R95 review category mismatch.'

Require-True $comparison.r81R85TlsSuccessProven 'R81/R85 TLS success not proven.'
Require-True $comparison.r93R95TlsHandshakeExceptionProven 'R93/R95 TLS HandshakeException not proven.'
Require-True $comparison.manualCliAdapterSocketTlsPathConsistent 'Manual CLI/adapter/socket/TLS path not consistent.'
Require-True $comparison.endpointHostPortSniClassificationsConsistent 'Endpoint/SNI classification consistency missing.'
Require-Equal $comparison.r81.tlsBoundaryResult 'Succeeded' 'Comparison R81 TLS mismatch.'
Require-Equal $comparison.r85.tlsBoundaryResult 'Succeeded' 'Comparison R85 TLS mismatch.'
Require-Equal $comparison.r93.tlsResultCategory 'HandshakeException' 'Comparison R93 TLS mismatch.'
Require-Equal $comparison.r95.tlsResultCategory 'HandshakeException' 'Comparison R95 TLS mismatch.'

Require-True $codeChange.r87FixWriterChangeReviewed 'R87 code-change review missing.'
Require-False $codeChange.r87PlausiblyAffectedTlsBehavior 'R87 should not plausibly affect TLS behavior.'
Require-True $codeChange.r91TlsClassificationChangeReviewed 'R91 code-change review missing.'
Require-False $codeChange.r91ChangedTlsHandshakeOperation 'R91 should not change TLS handshake operation.'
Require-False $codeChange.r91ChangedSslStreamConstruction 'R91 should not change SslStream construction.'
Require-False $codeChange.r91ChangedSniTargetHost 'R91 should not change SNI target host.'
Require-False $codeChange.r91ChangedTlsProtocolOptions 'R91 should not change TLS protocol options.'
Require-False $codeChange.r91ChangedCertificateValidationPolicy 'R91 should not change certificate validation policy.'
Require-False $codeChange.r91ChangedTimeoutOrCancellationHandling 'R91 should not change timeout/cancellation handling.'
Require-Equal $codeChange.r91ChangedBehaviorOrOnlyClassification 'ClassificationOnly' 'R91 should be classification-only.'
Require-True $codeChange.manualTlsOperationStillUsesSslStreamAuthenticateAsClient 'TLS operation path not reviewed.'
Require-False $codeChange.safeNoExternalFixIdentified 'A safe no-external fix should not be claimed.'

Require-Equal $sni.endpointMode 'Demo' 'SNI endpoint mode mismatch.'
Require-True $sni.endpointPresent 'Endpoint not present.'
Require-True $sni.hostPresent 'Host not present.'
Require-True $sni.hostConcreteBinding 'Concrete host binding missing.'
Require-False $sni.hostWasPlaceholder 'Placeholder host used.'
Require-True $sni.portPresent 'Port not present.'
Require-True $sni.portConcreteBinding 'Concrete port binding missing.'
Require-True $sni.productionExcluded 'Production endpoint not excluded.'
Require-True $sni.endpointApproved 'Endpoint not approved.'
Require-True $sni.tlsSniUsesRuntimeHost 'TLS SNI runtime host binding missing.'
Require-True $sni.tlsSniHostSameAsTcpHost 'TLS SNI/TCP host consistency missing.'
Require-True $sni.endpointSniHostBindingConsistentAcrossR81R85R93R95 'Endpoint/SNI consistency missing.'
Require-False $sni.sniHostMismatchSuspected 'SNI host mismatch suspected unexpectedly.'
Require-False $sni.rawHostSerialized 'Raw host serialized.'
Require-False $sni.rawPortSerialized 'Raw port serialized.'

Require-Equal $protocol.tlsProtocolSelection 'SystemDefault' 'TLS protocol selection mismatch.'
Require-True $protocol.timeoutExcluded 'Protocol review timeout exclusion missing.'
Require-Equal $protocol.certificateValidationPolicy 'SystemDefaultValidation' 'Certificate validation policy mismatch.'
Require-False $protocol.certificateValidationFailureProven 'Certificate validation failure should not be proven.'
Require-False $protocol.certificateValidationFailureCategoryObserved 'Certificate validation category observed unexpectedly.'
Require-False $protocol.protocolEndpointMismatchSuspected 'Protocol/endpoint mismatch suspected unexpectedly.'
Require-False $protocol.protocolEndpointMismatchProven 'Protocol/endpoint mismatch should not be proven.'
Require-False $protocol.rawCertificateDetailsSerialized 'Raw certificate details serialized.'
Require-False $protocol.rawTlsMaterialSerialized 'Raw TLS material serialized.'

Require-Equal $exception.sanitizedTlsFailureCategory 'HandshakeException' 'Exception category mismatch.'
Require-True $exception.repeatedAcrossR93R95 'Repeated exception evidence missing.'
Require-True $exception.timeoutExcluded 'Exception review timeout exclusion missing.'
Require-True $exception.attemptedOnlyExcluded 'Attempted-only exclusion missing.'
Require-False $exception.certificateValidationFailureProven 'Certificate validation failure proven unexpectedly.'
Require-True $exception.remoteCloseExternalIntermittentBehaviorSuspected 'External/intermittent suspect missing.'
Require-False $exception.specificUnderlyingCauseProven 'Specific underlying cause should not be proven.'
Require-False $exception.rawExceptionMessageSerialized 'Raw exception message serialized.'
Require-False $exception.rawExceptionStackSerialized 'Raw exception stack serialized.'

Require-False $fixBlock.tlsSucceeded 'TLS should be non-success in fix block review.'
Require-False $fixBlock.fixLogonSessionAttemptedInR93 'FIX attempted in R93 after TLS failure.'
Require-False $fixBlock.fixLogonSessionAttemptedInR95 'FIX attempted in R95 after TLS failure.'
Require-False $fixBlock.socketConnectorOpenFixSessionReachedInR93 'OpenFixSession reached in R93.'
Require-False $fixBlock.socketConnectorOpenFixSessionReachedInR95 'OpenFixSession reached in R95.'
Require-True $fixBlock.fixBlockedAfterTlsFailure 'FIX not blocked after TLS failure.'
Require-False $fixBlock.tlsSucceededGateWeakened 'TLS success gate weakened.'

Require-False $marketBlock.marketDataRequestAttemptedInR93 'MarketDataRequest attempted in R93.'
Require-False $marketBlock.marketDataRequestAttemptedInR95 'MarketDataRequest attempted in R95.'
Require-False $marketBlock.marketDataResponseEntriesObserved 'MarketDataResponse entries observed.'
Require-True $marketBlock.marketDataRequestBlockedAfterTlsFailure 'MarketDataRequest not blocked after TLS failure.'
Require-True $marketBlock.marketDataRequestBlockedWithoutFixSuccess 'MarketDataRequest not blocked without FIX success.'

Require-True $path.realBoundedPathValidated 'Real bounded path validation missing.'
Require-True $path.tlsContinuationBindingPresent 'TLS continuation binding missing.'
Require-True $path.noExternalDefaultPreserved 'No-external default not preserved.'
Require-False $path.realAdapterGlobalDefault 'Real adapter became global/default.'
Require-False $path.apiWorkerReachable 'Manual path reachable from API/Worker.'
Require-False $path.externalBoundaryAttemptedDuringR96 'External boundary attempted during R96.'

Require-False $noExternal.externalActivationAttempted 'External activation attempted during R96.'
Require-False $noExternal.tcpSocketAttempted 'TCP attempted during R96.'
Require-False $noExternal.tlsAttempted 'TLS attempted during R96.'
Require-False $noExternal.fixLogonAttempted 'FIX attempted during R96.'
Require-False $noExternal.marketDataRequestAttempted 'MarketDataRequest attempted during R96.'

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

Require-Equal $next.nextRecommendedPhase 'LMAX-R97' 'Next phase recommendation missing.'
Require-Equal $next.nextRecommendedTitle 'Operator-Approved Single Temporary Demo Read-Only Activation Retry After Repeated TLS Handshake Review' 'Next phase title mismatch.'
Require-True $next.requiresExplicitOperatorApproval 'Next activation approval requirement missing.'

Require-Equal $gate.gateValidation 'PASS' 'Gate validation mismatch.'
Require-True $gate.r95TlsHandshakeExceptionEvidencePresent 'R95 TLS evidence missing from gate.'
Require-True $gate.r93TlsHandshakeExceptionEvidencePresent 'R93 TLS evidence missing from gate.'
Require-True $gate.r81R85TlsSucceededEvidenceCompared 'R81/R85 comparison missing from gate.'
Require-True $gate.timeoutExclusionPresent 'Timeout exclusion missing from gate.'
Require-True $gate.endpointSniHostBindingReviewPresent 'SNI/host review missing from gate.'
Require-True $gate.codeChangeImpactReviewPresent 'Code-change review missing from gate.'
Require-True $gate.tlsExceptionCategoryReviewPresent 'TLS exception review missing from gate.'
Require-True $gate.sanitizationEvidencePresent 'Sanitization evidence missing from gate.'
Require-True $gate.buildEvidencePresent 'Build evidence missing from gate.'
Require-True $gate.testEvidencePresent 'Test evidence missing from gate.'
Require-True $gate.nextPhaseRecommendationPresent 'Next phase recommendation missing from gate.'

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r96-*' -File |
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
        throw "Forbidden sensitive endpoint, TLS, credential, or FIX pattern found in R96 artifacts: $pattern"
    }
}

Write-Output 'LMAX_R96_VALIDATION_PASS'
