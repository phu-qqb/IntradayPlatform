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

$summary = Read-Json (Join-Path $artifactRoot 'phase-lmax-r99-tls-environment-connectivity-summary.json')
$r98Review = Read-Json (Join-Path $artifactRoot 'phase-lmax-r99-r98-decision-gate-review.json')
$handshake = Read-Json (Join-Path $artifactRoot 'phase-lmax-r99-repeated-handshake-exception-review.json')
$code = Read-Json (Join-Path $artifactRoot 'phase-lmax-r99-code-regression-exclusion-review.json')
$checklist = Read-Json (Join-Path $artifactRoot 'phase-lmax-r99-environment-checklist.json')
$retry = Read-Json (Join-Path $artifactRoot 'phase-lmax-r99-next-retry-decision.json')
$fixBlock = Read-Json (Join-Path $artifactRoot 'phase-lmax-r99-fix-block-after-tls-failure-review.json')
$marketBlock = Read-Json (Join-Path $artifactRoot 'phase-lmax-r99-marketdata-block-after-tls-failure-review.json')
$noExternal = Read-Json (Join-Path $artifactRoot 'phase-lmax-r99-no-external-boundary-attempted.json')
$forbidden = Read-Json (Join-Path $artifactRoot 'phase-lmax-r99-forbidden-actions-audit.json')
$apiWorker = Read-Json (Join-Path $artifactRoot 'phase-lmax-r99-api-worker-fake-gateway-audit.json')
$sanitize = Read-Json (Join-Path $artifactRoot 'phase-lmax-r99-credential-endpoint-tls-fix-sanitization-validation.json')
$usdJpy = Read-Json (Join-Path $artifactRoot 'phase-lmax-r99-usdjpy-caveat-preservation.json')
$next = Read-Json (Join-Path $artifactRoot 'phase-lmax-r99-next-phase-recommendation.json')
$gate = Read-Json (Join-Path $artifactRoot 'phase-lmax-r99-gate-validation.json')
$r98 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r98-tls-runtime-boundary-root-cause-summary.json')
$r81 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r81-temporary-readonly-activation-retry-summary.json')
$r85 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r85-temporary-readonly-activation-retry-summary.json')
$r93 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r93-temporary-readonly-activation-retry-summary.json')
$r95 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r95-temporary-readonly-activation-retry-summary.json')
$r97 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r97-temporary-readonly-activation-retry-summary.json')

$allowedClassifications = @(
    'LMAX_R99_PASS_TLS_ENVIRONMENT_CONNECTIVITY_DECISION_GATE_NO_EXTERNAL_ACTIVATION',
    'LMAX_R99_DECISION_PAUSE_FOR_MANUAL_ENVIRONMENT_CHECKS_NO_EXTERNAL_ACTIVATION',
    'LMAX_R99_DECISION_NEXT_RETRY_AFTER_MANUAL_CHECKS_NO_EXTERNAL_ACTIVATION',
    'LMAX_R99_DECISION_CREATE_BOUNDED_TLS_DIAGNOSTIC_TOOL_NO_EXTERNAL_ACTIVATION',
    'LMAX_R99_DECISION_CONTACT_OR_VERIFY_LMAX_DEMO_ENDPOINT_NO_EXTERNAL_ACTIVATION',
    'LMAX_R99_DECISION_RETRY_LATER_DUE_TO_EXTERNAL_INTERMITTENT_SUSPECT_NO_EXTERNAL_ACTIVATION'
)

if ($allowedClassifications -notcontains $summary.classification) {
    throw "Unexpected R99 classification: $($summary.classification)"
}

Require-Equal $summary.classification 'LMAX_R99_PASS_TLS_ENVIRONMENT_CONNECTIVITY_DECISION_GATE_NO_EXTERNAL_ACTIVATION' 'R99 classification mismatch.'
Require-False $summary.newActivationPerformed 'R99 performed activation.'
Require-Equal $summary.decisionTaken 'PauseActivationForManualEnvironmentConnectivityChecks' 'R99 decision mismatch.'
Require-True $summary.r93R95R97RepeatedHandshakeExceptionReviewed 'Repeated HandshakeException evidence not reviewed.'
Require-True $summary.r81R85TlsSuccessAcknowledged 'R81/R85 TLS success not acknowledged.'
Require-True $summary.codeRegressionRemainsUnlikely 'Code regression exclusion missing.'
Require-Equal $summary.mostLikelySuspectClass 'RepeatedExternalOrIntermittentOrLocalEnvironmentConnectivity' 'Most likely suspect class mismatch.'
Require-True $summary.manualEnvironmentChecksRecommended 'Manual checks not recommended.'
Require-False $summary.anotherImmediateActivationRetryRecommended 'Immediate activation retry should not be recommended.'
Require-Equal $summary.futureRetryPhaseIfManualChecksClear 'LMAX-R101' 'Future retry phase should be R101.'
Require-True $summary.fixBlockedAfterTlsFailure 'FIX block after TLS failure missing.'
Require-True $summary.marketDataRequestBlockedAfterTlsFailure 'MarketData block after TLS failure missing.'
Require-False $summary.externalActivationAttemptedInR99 'External activation attempted in R99.'
Require-False $summary.credentialValuesReturned 'credentialValuesReturned must be false.'
Require-False $summary.credentialEndpointTlsFixSensitiveValuesPrintedStoredSerialized 'Sensitive values serialized.'

Require-Equal $r98.classification 'LMAX_R98_PASS_TLS_HANDSHAKE_EXCEPTION_MANUAL_ENVIRONMENT_DECISION_NEEDED_NO_EXTERNAL_ACTIVATION' 'R98 classification missing or mismatched.'
Require-Equal $r98Review.r98Classification 'LMAX_R98_PASS_TLS_HANDSHAKE_EXCEPTION_MANUAL_ENVIRONMENT_DECISION_NEEDED_NO_EXTERNAL_ACTIVATION' 'R98 review classification mismatch.'
Require-True $r98Review.r98Reviewed 'R98 review missing.'
Require-True $r98Review.r98ManualEnvironmentDecisionNeeded 'R98 manual environment decision missing.'
Require-False $r98Review.r98NewActivationPerformed 'R98 performed activation unexpectedly.'

Require-Equal $r81.tlsBoundaryResult 'Succeeded' 'R81 TLS success evidence missing.'
Require-Equal $r85.tlsBoundaryResult 'Succeeded' 'R85 TLS success evidence missing.'
Require-Equal $r93.tlsResultCategory 'HandshakeException' 'R93 HandshakeException evidence missing.'
Require-Equal $r95.tlsResultCategory 'HandshakeException' 'R95 HandshakeException evidence missing.'
Require-Equal $r97.tlsResultCategory 'HandshakeException' 'R97 HandshakeException evidence missing.'

Require-True $handshake.r81R85TlsSucceededEvidenceAcknowledged 'R81/R85 TLS success acknowledgement missing.'
Require-True $handshake.r93R95R97RepeatedHandshakeExceptionEvidenceReviewed 'Repeated HandshakeException review missing.'
Require-True $handshake.r93R95R97FailuresMateriallyIdentical 'Failure material identity missing.'
Require-True $handshake.timeoutExcludedAcrossR93R95R97 'Timeout exclusion missing.'
Require-Equal $handshake.sanitizedTlsFailureCategory 'HandshakeException' 'TLS failure category mismatch.'
Require-False $handshake.tlsStreamAvailableForFixAcrossFailures 'TLS stream should not be available for FIX across failures.'
Require-True $handshake.fixBlockedAcrossFailures 'FIX block missing.'
Require-True $handshake.marketDataBlockedAcrossFailures 'MarketData block missing.'
Require-False $handshake.rawTlsExceptionDetailsSerialized 'Raw TLS exception details serialized.'

Require-True $code.codeRegressionReviewPresent 'Code regression review missing.'
Require-False $code.postR85CodeChangesPlausiblyAffectedTlsBehavior 'Post-R85 code change should not be suspected.'
Require-False $code.r87FixWriterPlausiblyAffectedTlsBehavior 'R87 should not affect TLS behavior.'
Require-False $code.r91InstrumentationChangedBehavior 'R91 should not change TLS behavior.'
Require-True $code.r91InstrumentationChangedClassificationOnly 'R91 classification-only review missing.'
Require-False $code.endpointSniHostMismatchSuspected 'SNI/host mismatch should not be suspected.'
Require-False $code.certificateValidationFailureProven 'Certificate validation failure should not be proven.'
Require-False $code.protocolEndpointMismatchSuspected 'Protocol/endpoint mismatch should not be suspected.'
Require-False $code.sourceLevelTlsConfigFixIdentified 'Source-level TLS config fix should not be claimed.'

Require-True $checklist.manualEnvironmentChecklistPresent 'Manual environment checklist missing.'
if ($checklist.checks.Count -lt 8) {
    throw 'Manual environment checklist is too thin.'
}
Require-False $checklist.rawEndpointValuesAllowed 'Raw endpoint values allowed.'
Require-False $checklist.rawCredentialsAllowed 'Raw credentials allowed.'
Require-False $checklist.rawTlsMaterialAllowed 'Raw TLS material allowed.'
Require-False $checklist.rawTlsExceptionDetailsAllowed 'Raw TLS exception details allowed.'
Require-False $checklist.rawFixMessagesAllowed 'Raw FIX messages allowed.'

Require-False $retry.anotherImmediateActivationRetryRecommended 'Immediate activation retry should not be recommended.'
Require-True $retry.manualEnvironmentChecksRecommendedFirst 'Manual checks should be recommended first.'
Require-True $retry.futureRetryAllowedOnlyAfterManualChecks 'Future retry should require manual checks.'
Require-True $retry.futureRetryRequiresExplicitOperatorApproval 'Future retry should require explicit approval.'
Require-Equal $retry.futureRetryPhaseNumber 'LMAX-R101' 'Future retry phase number mismatch.'

Require-True $fixBlock.fixRemainsBlockedAfterTlsFailure 'FIX not blocked after TLS failure.'
Require-False $fixBlock.fixAttemptAllowedAfterTlsFailure 'FIX allowed after TLS failure.'
Require-False $fixBlock.socketConnectorOpenFixSessionReachableAfterTlsFailure 'OpenFixSession reachable after TLS failure.'
Require-False $fixBlock.tlsSucceededGateWeakened 'TLS success gate weakened.'

Require-True $marketBlock.marketDataRequestRemainsBlockedAfterTlsFailure 'MarketDataRequest not blocked after TLS failure.'
Require-True $marketBlock.marketDataRequestRemainsBlockedWithoutFixSuccess 'MarketDataRequest not blocked without FIX success.'
Require-False $marketBlock.marketDataRequestAllowedAfterTlsFailure 'MarketDataRequest allowed after TLS failure.'
Require-False $marketBlock.marketDataResponseEntriesObservedDuringR99 'MarketDataResponse observed during R99.'

Require-False $noExternal.externalActivationAttempted 'External activation attempted.'
Require-False $noExternal.tcpSocketAttempted 'TCP attempted.'
Require-False $noExternal.tlsAttempted 'TLS attempted.'
Require-False $noExternal.fixLogonAttempted 'FIX attempted.'
Require-False $noExternal.marketDataRequestAttempted 'MarketDataRequest attempted.'
Require-False $noExternal.boundedDiagnosticPlanApproved 'Bounded diagnostic plan should not be approved in R99.'

Require-Equal $forbidden.result 'PASS' 'Forbidden action audit failed.'
Require-False $forbidden.ordersSubmitted 'Orders submitted.'
Require-False $forbidden.newOrderSingleUsed 'NewOrderSingle used.'
Require-False $forbidden.cancelReplaceUsed 'Cancel/replace used.'
Require-False $forbidden.tradingEnabled 'Trading enabled.'
Require-False $forbidden.tradingStateMutated 'Trading state mutated.'
Require-False $forbidden.productionAccountUsed 'Production account used.'
Require-False $forbidden.schedulerPollingLoopUsed 'Scheduler/polling used.'
Require-False $forbidden.replayUsed 'Replay used.'
Require-False $forbidden.shadowReplayUsed 'Shadow replay used.'
Require-False $forbidden.socketOpened 'Socket opened.'
Require-False $forbidden.tlsAttempted 'TLS attempted.'
Require-False $forbidden.fixAttempted 'FIX attempted.'
Require-False $forbidden.marketDataRequestAttempted 'MarketDataRequest attempted.'

Require-Equal $apiWorker.result 'PASS' 'API/Worker audit failed.'
Require-True $apiWorker.apiWorkerFakeLmaxGatewayOnly 'API/Worker changed away from FakeLmaxGatewayOnly.'
Require-False $apiWorker.manualCliReachableFromApiWorkerDefaultStartup 'Manual CLI reachable from API/Worker default startup.'
Require-False $apiWorker.realAdapterGlobalDefault 'Real adapter became global/default.'

Require-Equal $sanitize.result 'PASS' 'Sanitization failed.'
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

Require-Equal $next.nextRecommendedPhase 'LMAX-R100' 'Next phase recommendation missing.'
Require-Equal $next.nextRecommendedTitle 'Manual TLS Environment Connectivity Checks Evidence Pack' 'Next phase title mismatch.'
Require-False $next.nextPhaseShouldPerformActivation 'R100 should not perform activation.'
Require-Equal $next.futureActivationRetryPhaseIfChecksClear 'LMAX-R101' 'Future activation retry phase mismatch.'
Require-True $next.futureActivationRequiresExplicitOperatorApproval 'Future activation approval requirement missing.'

Require-Equal $gate.gateValidation 'PASS' 'Gate validation mismatch.'
Require-True $gate.r98ClassificationPresent 'R98 classification missing from gate.'
Require-True $gate.r98ClassificationMatches 'R98 classification mismatch in gate.'
Require-True $gate.r93R95R97HandshakeExceptionReviewPresent 'Handshake exception review missing.'
Require-True $gate.r81R85TlsSuccessAcknowledged 'R81/R85 TLS success acknowledgement missing.'
Require-True $gate.codeRegressionExclusionReviewPresent 'Code regression review missing.'
Require-True $gate.manualEnvironmentChecklistPresent 'Manual environment checklist missing.'
Require-True $gate.nextRetryDecisionPresent 'Next retry decision missing.'
Require-True $gate.nextPhaseRecommendationPresent 'Next phase recommendation missing.'
Require-True $gate.fixBlockAfterTlsFailurePresent 'FIX block review missing.'
Require-True $gate.marketDataBlockAfterTlsFailurePresent 'MarketData block review missing.'
Require-True $gate.sanitizationEvidencePresent 'Sanitization evidence missing.'
Require-True $gate.buildEvidencePresent 'Build evidence missing.'
Require-True $gate.testEvidencePresent 'Test evidence missing.'

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r99-*' -File |
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
        throw "Forbidden sensitive endpoint, TLS, credential, or FIX pattern found in R99 artifacts: $pattern"
    }
}

Write-Output 'LMAX_R99_VALIDATION_PASS'
