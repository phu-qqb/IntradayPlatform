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

$summary = Read-Json (Join-Path $artifactRoot 'phase-lmax-r100-manual-tls-environment-checks-summary.json')
$r99Review = Read-Json (Join-Path $artifactRoot 'phase-lmax-r100-r99-decision-review.json')
$handshake = Read-Json (Join-Path $artifactRoot 'phase-lmax-r100-repeated-handshake-evidence-summary.json')
$checklist = Read-Json (Join-Path $artifactRoot 'phase-lmax-r100-manual-checklist.json')
$criteria = Read-Json (Join-Path $artifactRoot 'phase-lmax-r100-r101-readiness-decision-criteria.json')
$fixBlock = Read-Json (Join-Path $artifactRoot 'phase-lmax-r100-fix-block-after-tls-failure-review.json')
$marketBlock = Read-Json (Join-Path $artifactRoot 'phase-lmax-r100-marketdata-block-after-tls-failure-review.json')
$noExternal = Read-Json (Join-Path $artifactRoot 'phase-lmax-r100-no-external-boundary-attempted.json')
$forbidden = Read-Json (Join-Path $artifactRoot 'phase-lmax-r100-forbidden-actions-audit.json')
$apiWorker = Read-Json (Join-Path $artifactRoot 'phase-lmax-r100-api-worker-fake-gateway-audit.json')
$sanitize = Read-Json (Join-Path $artifactRoot 'phase-lmax-r100-credential-endpoint-tls-fix-sanitization-validation.json')
$usdJpy = Read-Json (Join-Path $artifactRoot 'phase-lmax-r100-usdjpy-caveat-preservation.json')
$next = Read-Json (Join-Path $artifactRoot 'phase-lmax-r100-next-phase-recommendation.json')
$gate = Read-Json (Join-Path $artifactRoot 'phase-lmax-r100-gate-validation.json')
$r99 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r99-tls-environment-connectivity-summary.json')
$r81 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r81-temporary-readonly-activation-retry-summary.json')
$r85 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r85-temporary-readonly-activation-retry-summary.json')
$r93 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r93-temporary-readonly-activation-retry-summary.json')
$r95 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r95-temporary-readonly-activation-retry-summary.json')
$r97 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r97-temporary-readonly-activation-retry-summary.json')

$allowedClassifications = @(
    'LMAX_R100_PASS_MANUAL_TLS_ENVIRONMENT_CHECKS_EVIDENCE_PACK_READY_NO_EXTERNAL_ACTIVATION',
    'LMAX_R100_DECISION_READY_FOR_MANUAL_ENVIRONMENT_CHECKS_NO_EXTERNAL_ACTIVATION',
    'LMAX_R100_DECISION_R101_RETRY_AFTER_MANUAL_CHECKS_NO_EXTERNAL_ACTIVATION',
    'LMAX_R100_DECISION_NEED_LMAX_ENDPOINT_SUPPORT_VERIFICATION_NO_EXTERNAL_ACTIVATION',
    'LMAX_R100_DECISION_NEED_LOCAL_TLS_INSPECTION_REVIEW_NO_EXTERNAL_ACTIVATION'
)

if ($allowedClassifications -notcontains $summary.classification) {
    throw "Unexpected R100 classification: $($summary.classification)"
}

Require-Equal $summary.classification 'LMAX_R100_PASS_MANUAL_TLS_ENVIRONMENT_CHECKS_EVIDENCE_PACK_READY_NO_EXTERNAL_ACTIVATION' 'R100 classification mismatch.'
Require-False $summary.activationPerformed 'R100 performed an activation.'
Require-False $summary.tcpTlsFixMarketDataBoundaryAttempted 'R100 attempted an external boundary.'
Require-True $summary.manualChecklistCreated 'Manual checklist missing.'
Require-True $summary.safeCommandTemplatesCreated 'Safe command templates missing.'
Require-False $summary.safeCommandTemplatesExecuteAutomatically 'Safe command templates execute automatically.'
Require-True $summary.sanitizedEvidenceFormCreated 'Sanitized evidence form missing.'
Require-True $summary.r101ReadinessDecisionCriteriaCreated 'R101 decision criteria missing.'
Require-True $summary.r93R95R97RepeatedHandshakeExceptionEvidenceSummarized 'Repeated HandshakeException summary missing.'
Require-True $summary.r81R85TlsSuccessContextIncluded 'R81/R85 context missing.'
Require-True $summary.fixBlockedAfterTlsFailure 'FIX block missing.'
Require-True $summary.marketDataRequestBlockedAfterTlsFailure 'MarketData block missing.'
Require-False $summary.credentialValuesReturned 'credentialValuesReturned must be false.'
Require-False $summary.credentialEndpointTlsFixSensitiveValuesPrintedStoredSerialized 'Sensitive values serialized.'

Require-Equal $r99.classification 'LMAX_R99_PASS_TLS_ENVIRONMENT_CONNECTIVITY_DECISION_GATE_NO_EXTERNAL_ACTIVATION' 'R99 decision missing or mismatched.'
Require-Equal $r99Review.r99Classification 'LMAX_R99_PASS_TLS_ENVIRONMENT_CONNECTIVITY_DECISION_GATE_NO_EXTERNAL_ACTIVATION' 'R99 review classification mismatch.'
Require-True $r99Review.r99DecisionReviewed 'R99 decision review missing.'
Require-Equal $r99Review.r99Decision 'PauseActivationForManualEnvironmentConnectivityChecks' 'R99 decision mismatch.'
Require-False $r99Review.r99RecommendedImmediateRetry 'R99 immediate retry mismatch.'
Require-Equal $r99Review.r99FutureRetryPhaseIfChecksClear 'LMAX-R101' 'R99 future retry phase mismatch.'

Require-Equal $r81.tlsBoundaryResult 'Succeeded' 'R81 TLS success context missing.'
Require-Equal $r85.tlsBoundaryResult 'Succeeded' 'R85 TLS success context missing.'
Require-Equal $r93.tlsResultCategory 'HandshakeException' 'R93 HandshakeException missing.'
Require-Equal $r95.tlsResultCategory 'HandshakeException' 'R95 HandshakeException missing.'
Require-Equal $r97.tlsResultCategory 'HandshakeException' 'R97 HandshakeException missing.'

Require-True $handshake.r81R85TlsSuccessContextIncluded 'Handshake summary missing R81/R85 context.'
Require-True $handshake.r93R95R97RepeatedHandshakeExceptionEvidenceSummarized 'Handshake summary missing R93/R95/R97 evidence.'
Require-True $handshake.codeRegressionRemainsUnlikely 'Code regression conclusion missing.'
Require-Equal $handshake.mostLikelySuspectClass 'RepeatedExternalOrIntermittentOrLocalEnvironmentConnectivity' 'Suspect class mismatch.'

Require-True $checklist.manualChecklistCreated 'Manual checklist missing.'
if ($checklist.checks.Count -lt 13) {
    throw 'Manual checklist does not cover all required categories.'
}
Require-True $checklist.requiresSanitizedEvidenceOnly 'Checklist does not require sanitized evidence.'
Require-False $checklist.rawEndpointValuesAllowed 'Raw endpoint values allowed.'
Require-False $checklist.rawCredentialsAllowed 'Raw credentials allowed.'
Require-False $checklist.rawTlsMaterialAllowed 'Raw TLS material allowed.'
Require-False $checklist.rawTlsExceptionMessagesAllowed 'Raw TLS exception messages allowed.'
Require-False $checklist.rawFixMessagesAllowed 'Raw FIX messages allowed.'

Require-True $criteria.r101ReadinessDecisionCriteriaCreated 'R101 readiness criteria missing.'
if ($criteria.r101RetryAllowedOnlyIf.Count -lt 8) {
    throw 'R101 allow criteria are incomplete.'
}
if ($criteria.r101RetryNotAllowedIf.Count -lt 5) {
    throw 'R101 block criteria are incomplete.'
}
Require-Equal $criteria.futureRetryPhaseNumber 'LMAX-R101' 'R101 phase number mismatch.'
Require-True $criteria.activationRetryPhaseMustBeOdd 'Odd retry phase rule missing.'

Require-True $fixBlock.fixRemainsBlockedAfterTlsFailure 'FIX not blocked after TLS failure.'
Require-False $fixBlock.fixAttemptAllowedAfterTlsFailure 'FIX allowed after TLS failure.'
Require-False $fixBlock.socketConnectorOpenFixSessionReachableAfterTlsFailure 'OpenFixSession reachable after TLS failure.'
Require-False $fixBlock.tlsSucceededGateWeakened 'TLS succeeded gate weakened.'

Require-True $marketBlock.marketDataRequestRemainsBlockedAfterTlsFailure 'MarketDataRequest not blocked after TLS failure.'
Require-True $marketBlock.marketDataRequestRemainsBlockedWithoutFixSuccess 'MarketDataRequest not blocked without FIX success.'
Require-False $marketBlock.marketDataRequestAllowedAfterTlsFailure 'MarketDataRequest allowed after TLS failure.'
Require-False $marketBlock.marketDataResponseEntriesObservedDuringR100 'MarketDataResponse observed during R100.'

Require-False $noExternal.activationPerformed 'Activation performed.'
Require-False $noExternal.manualCliActivationRun 'Manual CLI activation run.'
Require-False $noExternal.tcpSocketAttempted 'TCP attempted.'
Require-False $noExternal.tlsAttempted 'TLS attempted.'
Require-False $noExternal.fixLogonAttempted 'FIX attempted.'
Require-False $noExternal.marketDataRequestAttempted 'MarketDataRequest attempted.'
Require-True $noExternal.safeCommandTemplatesOnly 'Safe command templates not marked template-only.'
Require-False $noExternal.safeCommandTemplatesExecuted 'Safe command templates executed.'

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
Require-False $forbidden.manualCliActivationRun 'Manual CLI activation run.'
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

Require-Equal $next.nextRecommendedPhase 'LMAX-R101' 'Next phase recommendation missing.'
Require-Equal $next.nextRecommendedTitle 'Manual Environment Check Result Review' 'Next phase title mismatch.'
Require-False $next.nextPhaseShouldPerformActivation 'R101 should review manual results first by default.'

Require-Equal $gate.gateValidation 'PASS' 'Gate validation mismatch.'
Require-True $gate.r99DecisionPresent 'R99 decision missing from gate.'
Require-True $gate.r99DecisionMatches 'R99 decision mismatch in gate.'
Require-True $gate.manualEnvironmentChecklistPresent 'Manual checklist missing from gate.'
Require-True $gate.repeatedHandshakeEvidenceSummaryPresent 'Handshake evidence summary missing from gate.'
Require-True $gate.r81R85TlsSuccessContextPresent 'R81/R85 context missing from gate.'
Require-True $gate.safeCommandTemplatesPresent 'Safe command templates missing from gate.'
Require-True $gate.safeCommandTemplatesOnly 'Safe command templates not template-only.'
Require-True $gate.sanitizedEvidenceFormPresent 'Sanitized evidence form missing from gate.'
Require-True $gate.r101DecisionCriteriaPresent 'R101 criteria missing from gate.'
Require-True $gate.fixBlockAfterTlsFailurePresent 'FIX block missing from gate.'
Require-True $gate.marketDataBlockAfterTlsFailurePresent 'MarketData block missing from gate.'
Require-True $gate.sanitizationEvidencePresent 'Sanitization evidence missing from gate.'
Require-True $gate.buildEvidencePresent 'Build evidence missing from gate.'
Require-True $gate.testEvidencePresent 'Test evidence missing from gate.'
Require-True $gate.nextPhaseRecommendationPresent 'Next phase recommendation missing from gate.'

$templates = Get-Content -LiteralPath (Join-Path $artifactRoot 'phase-lmax-r100-safe-command-templates.md') -Raw
if ($templates -notmatch 'Template only' -or $templates -notmatch 'R100 does not execute') {
    throw 'Safe command templates are not clearly marked template-only.'
}

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r100-*' -File |
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
        throw "Forbidden sensitive endpoint, TLS, credential, or FIX pattern found in R100 artifacts: $pattern"
    }
}

Write-Output 'LMAX_R100_VALIDATION_PASS'
