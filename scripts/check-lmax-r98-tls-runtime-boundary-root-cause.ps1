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

$summary = Read-Json (Join-Path $artifactRoot 'phase-lmax-r98-tls-runtime-boundary-root-cause-summary.json')
$beforeAfter = Read-Json (Join-Path $artifactRoot 'phase-lmax-r98-r97-boundary-before-after-classification.json')
$handshake = Read-Json (Join-Path $artifactRoot 'phase-lmax-r98-r93-r95-r97-handshake-exception-review.json')
$comparison = Read-Json (Join-Path $artifactRoot 'phase-lmax-r98-r81-r85-vs-r93-r95-r97-tls-comparison.json')
$codeChange = Read-Json (Join-Path $artifactRoot 'phase-lmax-r98-tls-code-change-impact-review.json')
$sni = Read-Json (Join-Path $artifactRoot 'phase-lmax-r98-tls-sni-host-binding-review.json')
$protocol = Read-Json (Join-Path $artifactRoot 'phase-lmax-r98-tls-protocol-and-certificate-review.json')
$exception = Read-Json (Join-Path $artifactRoot 'phase-lmax-r98-tls-exception-sanitized-category-review.json')
$fixBlock = Read-Json (Join-Path $artifactRoot 'phase-lmax-r98-fix-block-after-tls-failure-review.json')
$marketBlock = Read-Json (Join-Path $artifactRoot 'phase-lmax-r98-marketdata-block-after-tls-failure-review.json')
$path = Read-Json (Join-Path $artifactRoot 'phase-lmax-r98-real-bounded-path-validation.json')
$noExternal = Read-Json (Join-Path $artifactRoot 'phase-lmax-r98-no-external-boundary-attempted.json')
$forbidden = Read-Json (Join-Path $artifactRoot 'phase-lmax-r98-forbidden-actions-audit.json')
$apiWorker = Read-Json (Join-Path $artifactRoot 'phase-lmax-r98-api-worker-fake-gateway-audit.json')
$scheduler = Read-Json (Join-Path $artifactRoot 'phase-lmax-r98-no-scheduler-polling-service-audit.json')
$sanitize = Read-Json (Join-Path $artifactRoot 'phase-lmax-r98-credential-endpoint-tls-fix-sanitization-validation.json')
$usdJpy = Read-Json (Join-Path $artifactRoot 'phase-lmax-r98-usdjpy-caveat-preservation.json')
$next = Read-Json (Join-Path $artifactRoot 'phase-lmax-r98-next-phase-recommendation.json')
$gate = Read-Json (Join-Path $artifactRoot 'phase-lmax-r98-gate-validation.json')
$r81 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r81-temporary-readonly-activation-retry-summary.json')
$r85 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r85-temporary-readonly-activation-retry-summary.json')
$r93 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r93-temporary-readonly-activation-retry-summary.json')
$r95 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r95-temporary-readonly-activation-retry-summary.json')
$r97 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r97-temporary-readonly-activation-retry-summary.json')

$allowedClassifications = @(
    'LMAX_R98_PASS_TLS_HANDSHAKE_EXCEPTION_ROOT_CAUSE_IDENTIFIED_NO_EXTERNAL_ACTIVATION',
    'LMAX_R98_PASS_TLS_HANDSHAKE_EXCEPTION_REPEATED_EXTERNAL_OR_INTERMITTENT_SUSPECT_NO_EXTERNAL_ACTIVATION',
    'LMAX_R98_PASS_TLS_HANDSHAKE_EXCEPTION_CODE_CHANGE_REGRESSION_SUSPECT_NO_EXTERNAL_ACTIVATION',
    'LMAX_R98_PASS_TLS_HANDSHAKE_EXCEPTION_SNI_OR_HOST_BINDING_SUSPECT_NO_EXTERNAL_ACTIVATION',
    'LMAX_R98_PASS_TLS_HANDSHAKE_EXCEPTION_PROTOCOL_OR_ENDPOINT_MISMATCH_SUSPECT_NO_EXTERNAL_ACTIVATION',
    'LMAX_R98_PASS_TLS_HANDSHAKE_EXCEPTION_CERTIFICATE_VALIDATION_NOT_PROVEN_NO_EXTERNAL_ACTIVATION',
    'LMAX_R98_PASS_TLS_HANDSHAKE_EXCEPTION_SANITIZED_CAUSE_INCONCLUSIVE_NO_EXTERNAL_ACTIVATION',
    'LMAX_R98_PASS_TLS_HANDSHAKE_EXCEPTION_MANUAL_ENVIRONMENT_DECISION_NEEDED_NO_EXTERNAL_ACTIVATION',
    'LMAX_R98_PASS_TLS_HANDSHAKE_CONFIG_FIX_READY_NO_EXTERNAL_ACTIVATION'
)

if ($allowedClassifications -notcontains $summary.classification) {
    throw "Unexpected R98 classification: $($summary.classification)"
}

Require-Equal $summary.classification 'LMAX_R98_PASS_TLS_HANDSHAKE_EXCEPTION_MANUAL_ENVIRONMENT_DECISION_NEEDED_NO_EXTERNAL_ACTIVATION' 'R98 classification mismatch.'
Require-False $summary.newActivationPerformed 'R98 performed an activation.'
Require-True $summary.r97TlsHandshakeExceptionProven 'R97 TLS HandshakeException evidence missing.'
Require-True $summary.r95TlsHandshakeExceptionProven 'R95 TLS HandshakeException evidence missing.'
Require-True $summary.r93TlsHandshakeExceptionProven 'R93 TLS HandshakeException evidence missing.'
Require-True $summary.r81R85TlsSucceededEvidenceProven 'R81/R85 TLS success comparison missing.'
Require-True $summary.timeoutExcluded 'Timeout exclusion missing.'
Require-Equal $summary.sanitizedTlsFailureCategory 'HandshakeException' 'Sanitized TLS category mismatch.'
Require-True $summary.r93R95R97FailuresMateriallyIdentical 'R93/R95/R97 material identity missing.'
Require-True $summary.endpointSniHostBindingConsistentAcrossAttempts 'Endpoint/SNI consistency missing.'
Require-False $summary.codeChangesAfterR85PlausiblyAffectedTlsBehavior 'Code change regression should not be suspected.'
Require-False $summary.r91InstrumentationChangedBehavior 'R91 instrumentation should not change behavior.'
Require-True $summary.r91InstrumentationChangedClassificationOnly 'R91 classification-only evidence missing.'
Require-False $summary.r87FixWriterChangesPlausiblyAffectedTlsBehavior 'R87 FIX writer should not affect TLS behavior.'
Require-False $summary.sniHostMismatchSuspected 'SNI/host mismatch suspected unexpectedly.'
Require-False $summary.certificateValidationFailureProven 'Certificate validation failure should not be proven.'
Require-False $summary.protocolEndpointMismatchSuspected 'Protocol/endpoint mismatch suspected unexpectedly.'
Require-True $summary.remoteCloseExternalIntermittentBehaviorSuspected 'Repeated external/intermittent suspect class missing.'
Require-True $summary.manualEnvironmentConnectivityDecisionNeeded 'Manual environment/connectivity decision missing.'
Require-False $summary.safeNoExternalFixIdentified 'A safe no-external TLS fix should not be claimed.'
Require-False $summary.anotherImmediateBoundedRetryWarranted 'Immediate retry should not be warranted without environment decision.'
Require-True $summary.fixBlockedAfterTlsFailure 'FIX block after TLS failure missing.'
Require-True $summary.marketDataRequestBlockedAfterTlsFailure 'MarketData block after TLS failure missing.'
Require-False $summary.externalActivationAttemptedInR98 'External activation attempted during R98.'
Require-False $summary.credentialValuesReturned 'credentialValuesReturned must be false.'
Require-False $summary.credentialEndpointTlsFixSensitiveValuesPrintedStoredSerialized 'Sensitive values serialized.'

if (($summary.responsibleClassFactoryBindingOperation -join '|') -notmatch 'LmaxReadOnlyActivationManualTcpSocketConnector\.AuthenticateTls') {
    throw 'Responsible TLS operation not named.'
}

Require-Equal $r81.tlsBoundaryResult 'Succeeded' 'R81 TLS success evidence missing.'
Require-Equal $r85.tlsBoundaryResult 'Succeeded' 'R85 TLS success evidence missing.'
Require-Equal $r93.tlsResultCategory 'HandshakeException' 'R93 HandshakeException missing.'
Require-Equal $r95.tlsResultCategory 'HandshakeException' 'R95 HandshakeException missing.'
Require-Equal $r97.tlsResultCategory 'HandshakeException' 'R97 HandshakeException missing.'
Require-True $r81.tcpSocketSucceeded 'R81 TCP success missing.'
Require-True $r85.tcpSocketSucceeded 'R85 TCP success missing.'
Require-True $r93.tcpSocketSucceeded 'R93 TCP success missing.'
Require-True $r95.tcpSocketSucceeded 'R95 TCP success missing.'
Require-True $r97.tcpSocketSucceeded 'R97 TCP success missing.'
Require-False $r93.tlsTimedOut 'R93 timeout should be false.'
Require-False $r95.tlsTimedOut 'R95 timeout should be false.'
Require-False $r97.tlsTimedOut 'R97 timeout should be false.'

Require-Equal $beforeAfter.r97TlsResultCategory 'HandshakeException' 'Before/after R97 category mismatch.'
Require-False $beforeAfter.r98ExternalBoundaryAttempted 'R98 external boundary attempted.'

Require-True $handshake.repeatedHandshakeExceptionProven 'Repeated handshake exception not proven.'
Require-True $handshake.failuresMateriallyIdentical 'Repeated failures not materially identical.'
Require-True $handshake.timeoutExcludedAcrossFailures 'Timeout exclusion across failures missing.'
Require-True $handshake.attemptedOnlyExcludedAcrossFailures 'Attempted-only exclusion missing.'
Require-False $handshake.rawTlsExceptionDetailsSerialized 'Raw TLS exception details serialized.'
Require-Equal $handshake.r93.tlsResultCategory 'HandshakeException' 'R93 review category mismatch.'
Require-Equal $handshake.r95.tlsResultCategory 'HandshakeException' 'R95 review category mismatch.'
Require-Equal $handshake.r97.tlsResultCategory 'HandshakeException' 'R97 review category mismatch.'

Require-True $comparison.r81R85TlsSuccessProven 'R81/R85 TLS success not proven.'
Require-True $comparison.r93R95R97TlsHandshakeExceptionProven 'R93/R95/R97 TLS HandshakeException not proven.'
Require-True $comparison.manualCliAdapterSocketTlsPathConsistent 'Manual CLI/adapter/socket/TLS path not consistent.'
Require-True $comparison.endpointHostPortSniClassificationsConsistent 'Endpoint/SNI classification consistency missing.'
Require-True $comparison.adapterModeConsistent 'Adapter mode consistency missing.'
Require-True $comparison.socketConnectorConsistent 'Socket connector consistency missing.'

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
Require-True $codeChange.manualTlsOperationStillUsesRuntimeHostForTarget 'TLS target host path not reviewed.'
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
Require-True $sni.endpointSniHostBindingConsistentAcrossR81R85R93R95R97 'Endpoint/SNI consistency missing.'
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
Require-True $exception.repeatedAcrossR93R95R97 'Repeated exception evidence missing.'
Require-True $exception.timeoutExcluded 'Exception review timeout exclusion missing.'
Require-True $exception.attemptedOnlyExcluded 'Attempted-only exclusion missing.'
Require-False $exception.certificateValidationFailureProven 'Certificate validation failure proven unexpectedly.'
Require-True $exception.remoteCloseExternalIntermittentBehaviorSuspected 'External/intermittent suspect missing.'
Require-False $exception.specificUnderlyingCauseProven 'Specific underlying cause should not be proven.'
Require-False $exception.rawExceptionMessageSerialized 'Raw exception message serialized.'
Require-False $exception.rawExceptionStackSerialized 'Raw exception stack serialized.'
Require-False $exception.rawTlsMaterialSerialized 'Raw TLS material serialized.'

Require-False $fixBlock.tlsSucceeded 'TLS should be non-success in fix block review.'
Require-False $fixBlock.fixLogonSessionAttemptedInR93 'FIX attempted in R93 after TLS failure.'
Require-False $fixBlock.fixLogonSessionAttemptedInR95 'FIX attempted in R95 after TLS failure.'
Require-False $fixBlock.fixLogonSessionAttemptedInR97 'FIX attempted in R97 after TLS failure.'
Require-False $fixBlock.socketConnectorOpenFixSessionReachedInR93 'OpenFixSession reached in R93.'
Require-False $fixBlock.socketConnectorOpenFixSessionReachedInR95 'OpenFixSession reached in R95.'
Require-False $fixBlock.socketConnectorOpenFixSessionReachedInR97 'OpenFixSession reached in R97.'
Require-True $fixBlock.fixBlockedAfterTlsFailure 'FIX not blocked after TLS failure.'
Require-False $fixBlock.tlsSucceededGateWeakened 'TLS success gate weakened.'

Require-False $marketBlock.marketDataRequestAttemptedInR93 'MarketDataRequest attempted in R93.'
Require-False $marketBlock.marketDataRequestAttemptedInR95 'MarketDataRequest attempted in R95.'
Require-False $marketBlock.marketDataRequestAttemptedInR97 'MarketDataRequest attempted in R97.'
Require-False $marketBlock.marketDataResponseEntriesObserved 'MarketDataResponse entries observed.'
Require-True $marketBlock.marketDataRequestBlockedAfterTlsFailure 'MarketDataRequest not blocked after TLS failure.'
Require-True $marketBlock.marketDataRequestBlockedWithoutFixSuccess 'MarketDataRequest not blocked without FIX success.'

Require-True $path.realBoundedPathValidated 'Real bounded path validation missing.'
Require-True $path.tlsContinuationBindingPresent 'TLS continuation binding missing.'
Require-True $path.fixContinuationStillBlockedUntilTlsSucceeded 'FIX TLS gate missing.'
Require-True $path.noExternalDefaultPreserved 'No-external default not preserved.'
Require-False $path.realAdapterGlobalDefault 'Real adapter became global/default.'
Require-False $path.apiWorkerReachable 'Manual path reachable from API/Worker.'
Require-False $path.externalBoundaryAttemptedDuringR98 'External boundary attempted during R98.'

Require-False $noExternal.externalActivationAttempted 'External activation attempted during R98.'
Require-False $noExternal.tcpSocketAttempted 'TCP attempted during R98.'
Require-False $noExternal.tlsAttempted 'TLS attempted during R98.'
Require-False $noExternal.fixLogonAttempted 'FIX attempted during R98.'
Require-False $noExternal.marketDataRequestAttempted 'MarketDataRequest attempted during R98.'

Require-Equal $forbidden.result 'PASS' 'Forbidden actions audit failed.'
Require-False $forbidden.ordersSubmitted 'Orders submitted.'
Require-False $forbidden.newOrderSingleUsed 'NewOrderSingle used.'
Require-False $forbidden.cancelReplaceUsed 'Cancel/replace used.'
Require-False $forbidden.tradingEnabled 'Trading enabled.'
Require-False $forbidden.tradingStateMutated 'Trading state mutated.'
Require-False $forbidden.productionAccountUsed 'Production account used.'
Require-False $forbidden.schedulerPollingLoopUsed 'Scheduler/polling loop used.'
Require-False $forbidden.replayUsed 'Replay used.'
Require-False $forbidden.shadowReplayUsed 'Shadow replay used.'
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

Require-Equal $next.nextRecommendedPhase 'LMAX-R99' 'Next phase recommendation missing.'
Require-Equal $next.nextRecommendedTitle 'TLS Environment/Connectivity Decision Gate After Repeated Handshake Exceptions' 'Next phase title mismatch.'
Require-False $next.activationRetryRecommendedImmediately 'Immediate retry should not be recommended.'
Require-True $next.manualEnvironmentConnectivityDecisionRecommended 'Manual environment decision recommendation missing.'
Require-True $next.requiresExplicitOperatorApprovalForAnyFutureActivation 'Future activation approval requirement missing.'

Require-Equal $gate.gateValidation 'PASS' 'Gate validation mismatch.'
Require-True $gate.r97TlsHandshakeExceptionEvidencePresent 'R97 TLS evidence missing from gate.'
Require-True $gate.r95TlsHandshakeExceptionEvidencePresent 'R95 TLS evidence missing from gate.'
Require-True $gate.r93TlsHandshakeExceptionEvidencePresent 'R93 TLS evidence missing from gate.'
Require-True $gate.r81R85TlsSucceededEvidenceCompared 'R81/R85 comparison missing from gate.'
Require-True $gate.timeoutExclusionPresent 'Timeout exclusion missing from gate.'
Require-True $gate.endpointSniHostBindingReviewPresent 'SNI/host review missing from gate.'
Require-True $gate.codeChangeImpactReviewPresent 'Code-change review missing from gate.'
Require-True $gate.tlsExceptionCategoryReviewPresent 'TLS exception review missing from gate.'
Require-True $gate.fixBlockAfterTlsFailurePresent 'FIX block review missing from gate.'
Require-True $gate.marketDataBlockAfterTlsFailurePresent 'MarketData block review missing from gate.'
Require-True $gate.sanitizationEvidencePresent 'Sanitization evidence missing from gate.'
Require-True $gate.buildEvidencePresent 'Build evidence missing from gate.'
Require-True $gate.testEvidencePresent 'Test evidence missing from gate.'
Require-True $gate.nextPhaseRecommendationPresent 'Next phase recommendation missing from gate.'

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r98-*' -File |
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
        throw "Forbidden sensitive endpoint, TLS, credential, or FIX pattern found in R98 artifacts: $pattern"
    }
}

Write-Output 'LMAX_R98_VALIDATION_PASS'
