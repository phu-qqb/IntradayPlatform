$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'

function Fail($code, $message) {
    Write-Error "$code $message"
    exit 1
}

function Read-Json($name) {
    $path = Join-Path $artifactRoot $name
    if (-not (Test-Path -LiteralPath $path)) {
        Fail 'LMAX_R128_FAIL_BUILD_OR_TESTS' "Missing artifact: $name"
    }

    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$required = @(
    'phase-lmax-r128-temporary-runtime-evidence-review-report.md',
    'phase-lmax-r128-temporary-runtime-evidence-review-summary.json',
    'phase-lmax-r128-r127-evidence-review.json',
    'phase-lmax-r128-sessionreject-cause-classification-review.json',
    'phase-lmax-r128-market-hours-caveat-review.json',
    'phase-lmax-r128-no-external-boundary-attempted.json',
    'phase-lmax-r128-usdjpy-caveat-preservation.json',
    'phase-lmax-r128-forbidden-actions-audit.json',
    'phase-lmax-r128-api-worker-fake-gateway-audit.json',
    'phase-lmax-r128-credential-fix-sanitization-validation.json',
    'phase-lmax-r128-next-phase-recommendation.json',
    'phase-lmax-r128-gate-validation.json'
)

foreach ($artifact in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $artifact))) {
        Fail 'LMAX_R128_FAIL_BUILD_OR_TESTS' "Missing artifact: $artifact"
    }
}

$r127 = Read-Json 'phase-lmax-r127-temporary-readonly-activation-retry-summary.json'
$summary = Read-Json 'phase-lmax-r128-temporary-runtime-evidence-review-summary.json'
$review = Read-Json 'phase-lmax-r128-r127-evidence-review.json'
$causes = Read-Json 'phase-lmax-r128-sessionreject-cause-classification-review.json'
$marketHours = Read-Json 'phase-lmax-r128-market-hours-caveat-review.json'
$external = Read-Json 'phase-lmax-r128-no-external-boundary-attempted.json'
$usdjpy = Read-Json 'phase-lmax-r128-usdjpy-caveat-preservation.json'
$forbidden = Read-Json 'phase-lmax-r128-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r128-api-worker-fake-gateway-audit.json'
$sanitize = Read-Json 'phase-lmax-r128-credential-fix-sanitization-validation.json'
$next = Read-Json 'phase-lmax-r128-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r128-gate-validation.json'

if ($r127.classification -ne 'LMAX_R127_FAIL_MARKETDATA_RESPONSE_BOUNDARY' -or
    $r127.marketDataResponseCategory -ne 'SessionRejectObserved') {
    Fail 'LMAX_R128_FAIL_R127_EVIDENCE_MISSING' 'R127 response evidence missing or mismatched.'
}

if ($summary.classification -ne 'LMAX_R128_PASS_RUNTIME_EVIDENCE_REVIEW_INCONCLUSIVE_SAFE_NO_EXTERNAL_ACTIVATION') {
    Fail 'LMAX_R128_FAIL_INCONCLUSIVE_SAFE_CLASSIFICATION_MISSING' 'Unexpected R128 classification.'
}

if (-not $summary.reviewOnly -or $summary.externalActivationAttempted -or
    $external.tcpSocket -ne 'NotAttempted' -or $external.tls -ne 'NotAttempted' -or
    $external.fixLogonSession -ne 'NotAttempted' -or $external.marketDataRequest -ne 'NotAttempted' -or
    $external.marketDataResponseRead -ne 'NotAttempted') {
    Fail 'LMAX_R128_FAIL_EXTERNAL_BOUNDARY_ATTEMPTED' 'R128 attempted an external boundary.'
}

if (-not $review.r127EvidenceReviewed -or $review.marketDataResponseCategory -ne 'SessionRejectObserved' -or
    $review.entriesObserved -or $review.sanitizedEntryCount -ne 0) {
    Fail 'LMAX_R128_FAIL_R127_EVIDENCE_MISSING' 'R127 evidence review is incomplete.'
}

if ($summary.sessionRejectReasonAvailable -or $summary.rejectReasonDistinguishesCause) {
    Fail 'LMAX_R128_FAIL_OVERCLASSIFIED_SESSIONREJECT' 'R128 claims a reject reason that R127 did not archive.'
}

if (-not $summary.marketClosedOrSessionNotAvailablePlausible -or
    -not $marketHours.marketClosedOrSessionNotAvailablePlausible -or
    -not $gate.marketClosedOrSessionNotAvailableCauseClassAdded) {
    Fail 'LMAX_R128_FAIL_MARKET_HOURS_CAUSE_CLASS_MISSING' 'market_closed_or_session_not_available cause class missing.'
}

if ($summary.overClassifiedAsRequestShapeBug -or
    -not $causes.doNotOverClassifyAsMarketDataRequestShapeBug -or
    $causes.safeCauseDecision -ne 'inconclusive_safe') {
    Fail 'LMAX_R128_FAIL_OVERCLASSIFIED_SESSIONREJECT' 'SessionReject was over-classified as request shape bug.'
}

$selected = @($causes.causeClasses | Where-Object { $_.name -eq 'inconclusive' -and $_.classification -eq 'selected' }).Count
if ($selected -ne 1) {
    Fail 'LMAX_R128_FAIL_INCONCLUSIVE_SAFE_CLASSIFICATION_MISSING' 'Inconclusive-safe cause decision missing.'
}

if ($usdjpy.securityId -ne '4004' -or $usdjpy.securityIdSource -ne '8' -or -not $usdjpy.caveatPreserved -or $usdjpy.mappingWeakened) {
    Fail 'LMAX_R128_FAIL_USDJPY_CAVEAT_WEAKENED' 'USDJPY caveat weakened.'
}

if ($sanitize.credentialValuesReturned -or $sanitize.rawCredentialsSerialized -or
    $sanitize.rawEndpointSerialized -or $sanitize.rawTlsMaterialSerialized -or
    $sanitize.rawFixSerialized -or $sanitize.rawFixMessagesSerialized -or
    $sanitize.rawSessionIdentifiersSerialized) {
    Fail 'LMAX_R128_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' 'Sensitive values serialized.'
}

if ($forbidden.result -ne 'PASS' -or $forbidden.orders -or $forbidden.newOrderSingle -or
    $forbidden.cancelReplace -or $forbidden.tradingEnablement -or $forbidden.tradingStateMutation -or
    $forbidden.scheduler -or $forbidden.pollingLoop -or $forbidden.replay -or $forbidden.shadowReplay -or
    $forbidden.externalBoundaryAttempted) {
    Fail 'LMAX_R128_FAIL_FORBIDDEN_ACTION_RISK' 'Forbidden action audit failed.'
}

if ($apiWorker.result -ne 'PASS' -or -not $apiWorker.apiWorkerFakeLmaxGatewayOnly -or
    $apiWorker.apiStartupAttempted -or $apiWorker.workerStartupAttempted) {
    Fail 'LMAX_R128_FAIL_API_WORKER_GATEWAY_REGRESSION' 'API/Worker FakeLmaxGatewayOnly audit failed.'
}

if ($next.nextRecommendedPhase -ne 'Phase LMAX-R129 - No-External SessionReject Reason Enrichment and Market-Hours-Aware Evidence Review' -or
    $next.r129ExternalActivationAllowed) {
    Fail 'LMAX_R128_FAIL_NEXT_PHASE_RECOMMENDATION_MISSING' 'Next phase recommendation missing or unsafe.'
}

if ($gate.validatorResult -ne 'LMAX_R128_VALIDATION_PASS' -or
    -not $gate.buildEvidencePresent -or -not $gate.testEvidencePresent) {
    Fail 'LMAX_R128_FAIL_BUILD_OR_TESTS' 'Build/test/validator evidence missing.'
}

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r128-*' -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$joined = $artifactText -join "`n"
$blockedPatterns = @('password=', 'username=', 'SenderCompID=', 'TargetCompID=', 'BEGIN CERTIFICATE', 'PRIVATE KEY')
foreach ($pattern in $blockedPatterns) {
    if ($joined.IndexOf($pattern, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        Fail 'LMAX_R128_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' "Sensitive pattern found in R128 artifacts: $pattern"
    }
}

Write-Host 'LMAX_R128_VALIDATION_PASS'
