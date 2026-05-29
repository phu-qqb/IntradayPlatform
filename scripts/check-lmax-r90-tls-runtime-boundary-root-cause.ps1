param(
    [string]$Root = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'

function Fail([string]$Message) {
    Write-Error "LMAX_R90_VALIDATION_FAIL: $Message"
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
$summary = Read-Json (Join-Path $artifactRoot 'phase-lmax-r90-tls-runtime-boundary-root-cause-summary.json')
$r89BeforeAfter = Read-Json (Join-Path $artifactRoot 'phase-lmax-r90-r89-boundary-before-after-classification.json')
$r89Tls = Read-Json (Join-Path $artifactRoot 'phase-lmax-r90-r89-tls-boundary-review.json')
$comparison = Read-Json (Join-Path $artifactRoot 'phase-lmax-r90-r81-r85-r89-tls-comparison.json')
$success = Read-Json (Join-Path $artifactRoot 'phase-lmax-r90-tls-success-classification-review.json')
$rootCause = Read-Json (Join-Path $artifactRoot 'phase-lmax-r90-tls-attempted-only-root-cause.json')
$fixBlock = Read-Json (Join-Path $artifactRoot 'phase-lmax-r90-fix-block-after-tls-nonsuccess-review.json')
$marketBlock = Read-Json (Join-Path $artifactRoot 'phase-lmax-r90-marketdata-block-after-tls-nonsuccess-review.json')
$path = Read-Json (Join-Path $artifactRoot 'phase-lmax-r90-real-bounded-path-validation.json')
$noExternal = Read-Json (Join-Path $artifactRoot 'phase-lmax-r90-no-external-boundary-attempted.json')
$forbidden = Read-Json (Join-Path $artifactRoot 'phase-lmax-r90-forbidden-actions-audit.json')
$apiWorker = Read-Json (Join-Path $artifactRoot 'phase-lmax-r90-api-worker-fake-gateway-audit.json')
$scheduler = Read-Json (Join-Path $artifactRoot 'phase-lmax-r90-no-scheduler-polling-service-audit.json')
$sanitize = Read-Json (Join-Path $artifactRoot 'phase-lmax-r90-credential-endpoint-tls-fix-sanitization-validation.json')
$usdJpy = Read-Json (Join-Path $artifactRoot 'phase-lmax-r90-usdjpy-caveat-preservation.json')
$next = Read-Json (Join-Path $artifactRoot 'phase-lmax-r90-next-phase-recommendation.json')
$gate = Read-Json (Join-Path $artifactRoot 'phase-lmax-r90-gate-validation.json')
$r89 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r89-temporary-readonly-activation-retry-summary.json')
$r81 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r81-temporary-readonly-activation-retry-summary.json')
$r85 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r85-temporary-readonly-activation-retry-summary.json')

$allowed = @(
    'LMAX_R90_PASS_TLS_RUNTIME_BOUNDARY_ROOT_CAUSE_IDENTIFIED_NO_EXTERNAL_ACTIVATION',
    'LMAX_R90_PASS_TLS_ATTEMPTED_ONLY_DUE_TO_EXTERNAL_HANDSHAKE_RESULT_NO_EXTERNAL_ACTIVATION',
    'LMAX_R90_PASS_TLS_ATTEMPTED_ONLY_DUE_TO_TIMEOUT_NO_EXTERNAL_ACTIVATION',
    'LMAX_R90_PASS_TLS_ATTEMPTED_ONLY_DUE_TO_SANITIZED_CLASSIFICATION_LIMIT_NO_EXTERNAL_ACTIVATION',
    'LMAX_R90_PASS_TLS_ATTEMPTED_ONLY_DUE_TO_CLASSIFICATION_BUG_NO_EXTERNAL_ACTIVATION',
    'LMAX_R90_PASS_TLS_ATTEMPTED_ONLY_DUE_TO_INTERMITTENT_EXTERNAL_BEHAVIOR_NO_EXTERNAL_ACTIVATION',
    'LMAX_R90_PASS_TLS_SUCCESS_CLASSIFICATION_INSTRUMENTATION_NEEDED_NO_EXTERNAL_ACTIVATION',
    'LMAX_R90_PASS_TLS_SUCCESS_CLASSIFICATION_FIX_READY_NO_EXTERNAL_ACTIVATION',
    'LMAX_R90_FAIL_TLS_ROOT_CAUSE_NOT_PROVABLE',
    'LMAX_R90_FAIL_TLS_COMPARISON_MISSING',
    'LMAX_R90_FAIL_TLS_SUCCESS_CLASSIFICATION_REVIEW_MISSING',
    'LMAX_R90_FAIL_FIX_ALLOWED_AFTER_TLS_NONSUCCESS',
    'LMAX_R90_FAIL_MARKETDATA_ALLOWED_AFTER_TLS_NONSUCCESS',
    'LMAX_R90_FAIL_EXTERNAL_BOUNDARY_ATTEMPTED',
    'LMAX_R90_FAIL_FORBIDDEN_ACTION_INTRODUCED',
    'LMAX_R90_FAIL_CREDENTIAL_VALUES_RETURNED_OR_SECRET_RISK',
    'LMAX_R90_FAIL_API_WORKER_GATEWAY_REGRESSION',
    'LMAX_R90_FAIL_USDJPY_CAVEAT_WEAKENED',
    'LMAX_R90_FAIL_BUILD_OR_TESTS'
)

if ($allowed -notcontains $summary.classification) {
    Fail 'R90 classification is absent or not allowed.'
}

Require-Equal $summary.classification 'LMAX_R90_PASS_TLS_ATTEMPTED_ONLY_DUE_TO_SANITIZED_CLASSIFICATION_LIMIT_NO_EXTERNAL_ACTIVATION' 'R90 classification mismatch.'
Require-False $summary.newActivationPerformed 'R90 performed a new activation.'
Require-False $summary.externalActivationAttempted 'R90 attempted an external boundary.'
Require-True $summary.r89TlsAttemptedEvidenceProven 'R89 TLS Attempted evidence missing.'
Require-True $summary.r81R85TlsSucceededEvidenceProven 'R81/R85 TLS Succeeded evidence missing.'
Require-Equal $summary.r89TlsAttemptedOnlyReasonCategory 'SanitizedClassificationLimit' 'Root cause category missing or incorrect.'
Require-True $summary.sanitizedEvidenceLimitation 'Sanitized evidence limitation not recorded.'
Require-False $summary.timeoutProven 'Timeout must not be claimed as proven.'
Require-False $summary.actualTlsFailureProven 'Actual TLS failure must not be claimed as proven.'
Require-True $summary.fixRemainsBlockedAfterTlsNonSuccess 'FIX block after TLS non-success not preserved.'
Require-True $summary.marketDataRequestRemainsBlockedAfterTlsNonSuccess 'MarketData block after TLS non-success not preserved.'
Require-True $summary.safeNextFixNeeded 'Safe next fix not recorded.'

Require-Equal $r89.classification 'LMAX_R89_FAIL_TLS_BOUNDARY' 'R89 classification missing or mismatched.'
Require-True $r89.tlsAttempted 'R89 TLS attempt missing.'
Require-Equal $r89.tlsBoundaryResult 'Attempted' 'R89 TLS result mismatch.'
Require-False $r89.tlsSucceeded 'R89 must not mark TLS succeeded.'
Require-False $r89.fixLogonSessionAttempted 'R89 FIX should not be attempted.'
Require-False $r89.marketDataRequestAttempted 'R89 MarketDataRequest should not be attempted.'

Require-Equal $r81.tlsBoundaryResult 'Succeeded' 'R81 TLS Succeeded evidence missing.'
Require-Equal $r85.tlsBoundaryResult 'Succeeded' 'R85 TLS Succeeded evidence missing.'
Require-True $r81.fixLogonSessionAttempted 'R81 FIX attempt should prove TLS progression.'
Require-True $r85.fixLogonSessionAttempted 'R85 FIX attempt should prove TLS progression.'

Require-Equal $r89BeforeAfter.r90BoundaryStatuses.tcpSocket 'NotAttempted' 'R90 TCP boundary should be NotAttempted.'
Require-Equal $r89BeforeAfter.r90BoundaryStatuses.tls 'NotAttempted' 'R90 TLS boundary should be NotAttempted.'
Require-Equal $r89Tls.observedSanitizedCondition 'TLS was attempted and did not satisfy the TLS Succeeded gate; exact low-level TLS result category is not present in archived CLI output.' 'R89 observed condition mismatch.'
Require-True $r89Tls.sanitizedEvidenceLimitation 'R89 TLS sanitized limitation missing.'

if ($comparison.comparison.Count -lt 3) {
    Fail 'R81/R85/R89 TLS comparison is incomplete.'
}
Require-False $comparison.codePathChangedBetweenR85AndR89ForTls 'Unexpected TLS code path change recorded.'
Require-False $comparison.r87FixWriterAffectedTlsClassification 'R87 FIX writer should not affect TLS classification.'

Require-True $success.tlsSuccessClassificationConditionIdentified 'TLS success classification condition missing.'
if ($success.boundaryStepSuccessCondition -notmatch 'Succeeded') {
    Fail 'TLS Succeeded classification condition does not mention Succeeded.'
}
Require-True $success.instrumentationNeeded 'TLS instrumentation need missing.'

Require-Equal $rootCause.rootCauseCategory 'SanitizedClassificationLimit' 'Root cause file category mismatch.'
Require-True $rootCause.sanitizedEvidenceLimitation 'Root cause does not record sanitized limitation.'
Require-False $rootCause.actualTlsFailureProven 'Root cause should not claim actual TLS failure.'
Require-True $rootCause.noExternalFixPossible 'No-external fix possibility missing.'
if ($rootCause.responsibleClassFactoryBindingOperation.Count -lt 3) {
    Fail 'Responsible class/factory/binding/operation list is too small.'
}

Require-True $fixBlock.fixBlockedAfterTlsNonSuccess 'FIX block after TLS non-success missing.'
Require-False $fixBlock.r89FixLogonSessionAttempted 'R89 FIX attempted unexpectedly.'
Require-False $fixBlock.fixAttemptAllowedWithoutTlsSucceeded 'FIX allowed without TLS success.'
Require-True $marketBlock.marketDataRequestBlockedAfterTlsNonSuccess 'MarketData block after TLS non-success missing.'
Require-False $marketBlock.r89MarketDataRequestAttempted 'R89 MarketDataRequest attempted unexpectedly.'
Require-False $marketBlock.marketDataAttemptAllowedWithoutFixSuccess 'MarketData allowed without FIX success.'

Require-True $path.r89ManualCliUsed 'Manual CLI evidence missing.'
Require-True $path.r89AdapterModeUsed 'Adapter mode evidence missing.'
Require-True $path.r89AuthenticateTlsReached 'AuthenticateTls evidence missing.'
Require-False $path.apiWorkerReachable 'Real bounded path reachable from API/Worker.'
Require-False $path.productionAccountConfigAllowed 'Production account/config allowed.'

Require-False $noExternal.externalActivationAttempted 'External activation attempted during R90.'
Require-False $noExternal.socketOpened 'Socket opened during R90.'
Require-False $noExternal.tcpSocketAttempted 'TCP attempted during R90.'
Require-False $noExternal.tlsAttempted 'TLS attempted during R90.'
Require-False $noExternal.fixLogonAttempted 'FIX attempted during R90.'
Require-False $noExternal.marketDataRequestAttempted 'MarketDataRequest attempted during R90.'

Require-Equal $forbidden.result 'PASS' 'Forbidden action audit failed.'
Require-False $forbidden.ordersSubmitted 'Orders were submitted.'
Require-False $forbidden.orderPathTouched 'Order/trading path touched.'
Require-False $forbidden.tradingStateMutated 'Trading state mutated.'
Require-False $forbidden.productionAccountUsedOrAllowed 'Production account/config used or allowed.'

Require-Equal $apiWorker.result 'PASS' 'API/Worker audit failed.'
Require-Equal $apiWorker.apiWorkerGatewayMode 'FakeLmaxGatewayOnly' 'API/Worker gateway changed.'
Require-False $apiWorker.apiWorkerGatewayChanged 'API/Worker gateway changed.'
Require-False $apiWorker.tlsBoundaryReachableFromApiWorkerDefaultStartup 'TLS boundary reachable from API/Worker.'

Require-Equal $scheduler.result 'PASS' 'Scheduler/polling audit failed.'
Require-False $scheduler.hostedBackgroundServiceIntroduced 'Hosted/background service introduced.'
Require-False $scheduler.schedulerIntroduced 'Scheduler introduced.'
Require-False $scheduler.pollingIntroduced 'Polling introduced.'

Require-Equal $sanitize.result 'PASS' 'Sanitization validation failed.'
Require-False $sanitize.credentialValuesReturned 'credentialValuesReturned must remain false.'
Require-False $sanitize.rawCredentialsPrintedStoredSerialized 'Raw credentials exposed.'
Require-False $sanitize.rawEndpointValuesPrintedStoredSerialized 'Raw endpoint values exposed.'
Require-False $sanitize.rawTlsMaterialPrintedStoredSerialized 'Raw TLS material exposed.'
Require-False $sanitize.rawFixMessagesPrintedStoredSerialized 'Raw FIX messages exposed.'

Require-Equal $usdJpy.result 'PASS' 'USDJPY caveat validation failed.'
Require-True $usdJpy.caveatPreserved 'USDJPY caveat missing or weakened.'
Require-False $usdJpy.weakened 'USDJPY caveat weakened.'

Require-Equal $gate.validatorResult 'PASS' 'Gate validator result missing.'
Require-Equal $gate.buildResult.status 'PASS' 'Build evidence missing.'
Require-Equal $gate.testResult.status 'PASS' 'Test evidence missing.'
Require-Equal $next.nextRecommendedPhase 'LMAX-R91' 'Next phase recommendation missing or incorrect.'
Require-Equal $next.nextRecommendedTitle 'Targeted TLS Success Classification/Instrumentation Fix' 'Next phase title mismatch.'

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r90-*' -File |
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
        Fail "Forbidden sensitive artifact pattern found in R90 artifacts: $pattern"
    }
}

Write-Output 'LMAX_R90_VALIDATION_PASS'
