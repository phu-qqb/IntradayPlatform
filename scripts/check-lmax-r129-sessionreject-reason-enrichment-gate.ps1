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
        Fail 'LMAX_R129_FAIL_BUILD_OR_TESTS' "Missing artifact: $name"
    }

    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$required = @(
    'phase-lmax-r129-sessionreject-reason-enrichment-summary.md',
    'phase-lmax-r129-sessionreject-reason-enrichment.json',
    'phase-lmax-r129-market-hours-aware-evidence-review.json',
    'phase-lmax-r129-marketdata-request-shape-review-no-external.json',
    'phase-lmax-r129-sanitization-audit.json',
    'phase-lmax-r129-forbidden-actions-audit.json',
    'phase-lmax-r129-api-worker-fake-gateway-audit.json',
    'phase-lmax-r129-next-phase-recommendation.json'
)

foreach ($artifact in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $artifact))) {
        Fail 'LMAX_R129_FAIL_BUILD_OR_TESTS' "Missing artifact: $artifact"
    }
}

$r127 = Read-Json 'phase-lmax-r127-temporary-readonly-activation-retry-summary.json'
$r128 = Read-Json 'phase-lmax-r128-temporary-runtime-evidence-review-summary.json'
$summary = Read-Json 'phase-lmax-r129-sessionreject-reason-enrichment.json'
$marketHours = Read-Json 'phase-lmax-r129-market-hours-aware-evidence-review.json'
$requestShape = Read-Json 'phase-lmax-r129-marketdata-request-shape-review-no-external.json'
$sanitize = Read-Json 'phase-lmax-r129-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r129-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r129-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r129-next-phase-recommendation.json'

if ($r127.classification -ne 'LMAX_R127_FAIL_MARKETDATA_RESPONSE_BOUNDARY' -or $r127.attemptCount -ne 1 -or
    $r127.marketDataResponseCategory -ne 'SessionRejectObserved') {
    Fail 'LMAX_R129_FAIL_SESSIONREJECT_REASON_ENRICHMENT_MISSING' 'R127 evidence missing or mismatched.'
}

if ($r128.classification -ne 'LMAX_R128_PASS_RUNTIME_EVIDENCE_REVIEW_INCONCLUSIVE_SAFE_NO_EXTERNAL_ACTIVATION' -or
    -not $r128.marketClosedOrSessionNotAvailablePlausible -or $r128.overClassifiedAsRequestShapeBug) {
    Fail 'LMAX_R129_FAIL_MARKET_HOURS_AMBIGUITY_NOT_REPRESENTED' 'R128 inconclusive market-hours evidence missing.'
}

if ($summary.classification -notin @(
        'LMAX_R129_PASS_SESSIONREJECT_REASON_ENRICHMENT_READY_NO_EXTERNAL',
        'LMAX_R129_PASS_MARKET_HOURS_AWARE_INCONCLUSIVE_REVIEW_READY_NO_EXTERNAL')) {
    Fail 'LMAX_R129_FAIL_SESSIONREJECT_REASON_ENRICHMENT_MISSING' 'Unexpected R129 classification.'
}

if (-not $summary.noExternal -or -not $summary.sessionRejectReasonEnrichmentReady -or
    -not $summary.r127EvidencePreserved -or -not $summary.r128ClassificationPreserved) {
    Fail 'LMAX_R129_FAIL_SESSIONREJECT_REASON_ENRICHMENT_MISSING' 'SessionReject reason enrichment evidence missing.'
}

$requiredCategories = @(
    'SessionRejectObservedWithoutReason',
    'SessionRejectObservedWithSanitizedReason',
    'MarketClosedOrSessionUnavailablePlausible',
    'NoEntriesOutOfHoursPlausible',
    'ParserClassifierFalsePositiveNotExcluded',
    'InconclusiveSafe'
)
foreach ($category in $requiredCategories) {
    if ($summary.enrichedCategories -notcontains $category) {
        Fail 'LMAX_R129_FAIL_SESSIONREJECT_REASON_ENRICHMENT_MISSING' "Missing enriched category: $category"
    }
}

if (-not $summary.parserClassifierFalsePositiveNotExcluded -or -not $summary.inconclusiveSafePreserved) {
    Fail 'LMAX_R129_FAIL_MARKET_HOURS_AMBIGUITY_NOT_REPRESENTED' 'Parser false-positive/inconclusive-safe possibility was erased.'
}

if (-not $marketHours.marketHoursAwareEvidenceReviewReady -or
    -not $marketHours.marketClosedOrSessionNotAvailablePlausible -or
    -not $marketHours.noEntriesOutOfHoursPlausible -or
    -not $marketHours.sessionRejectObservedDoesNotProveRequestShapeBug) {
    Fail 'LMAX_R129_FAIL_MARKET_HOURS_AMBIGUITY_NOT_REPRESENTED' 'Market-hours/session availability ambiguity missing.'
}

if (-not $requestShape.marketDataRequestShapeReviewedNoExternal -or
    $requestShape.marketDataRequestShapeProvenWrong -or
    $requestShape.malformedUnsupportedRequestProven -or
    -not $requestShape.approvedInstrumentsOnly -or
    -not $requestShape.nonApprovedInstrumentsRejected) {
    Fail 'LMAX_R129_FAIL_MARKET_HOURS_AMBIGUITY_NOT_REPRESENTED' 'MarketDataRequest shape was over-classified or instrument scope weakened.'
}

if ($requestShape.usdJpySecurityId -ne '4004' -or $requestShape.usdJpySecurityIdSource -ne '8' -or
    -not $requestShape.usdJpyCaveatPreserved) {
    Fail 'LMAX_R129_FAIL_USDJPY_CAVEAT_WEAKENED' 'USDJPY caveat weakened.'
}

if ($sanitize.result -ne 'PASS' -or $sanitize.rawFixMessagesSerialized -or $sanitize.rawRejectTextSerialized -or
    $sanitize.rawCredentialsSerialized -or $sanitize.rawEndpointSerialized -or
    $sanitize.rawTlsMaterialSerialized -or $sanitize.rawSessionIdentifiersSerialized -or
    $sanitize.rawCompIdsSerialized -or $sanitize.credentialValuesReturned -or
    $summary.rawFixSerialized -or $summary.rawRejectTextSerialized -or $summary.credentialValuesReturned) {
    Fail 'LMAX_R129_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' 'Sanitization evidence failed.'
}

if ($forbidden.result -ne 'PASS' -or $forbidden.externalActivationAttempted -or $forbidden.socketOpened -or
    $forbidden.tlsAttempted -or $forbidden.fixAttempted -or $forbidden.marketDataRequestSent -or
    $forbidden.marketDataResponseReadLive -or $forbidden.orders -or $forbidden.newOrderSingle -or
    $forbidden.cancelReplace -or $forbidden.tradingEnablement -or $forbidden.tradingStateMutation -or
    $forbidden.productionAccount -or $forbidden.apiStartup -or $forbidden.workerStartup -or
    $forbidden.scheduler -or $forbidden.pollingLoop -or $forbidden.service -or
    $forbidden.replay -or $forbidden.shadowReplay) {
    Fail 'LMAX_R129_FAIL_FORBIDDEN_ACTION_RISK' 'Forbidden action introduced.'
}

if ($apiWorker.result -ne 'PASS' -or -not $apiWorker.apiWorkerFakeLmaxGatewayOnly -or
    $apiWorker.apiStartupAttempted -or $apiWorker.workerStartupAttempted) {
    Fail 'LMAX_R129_FAIL_API_WORKER_GATEWAY_REGRESSION' 'API/Worker FakeLmaxGatewayOnly regression.'
}

if ($next.nextRecommendedPhase -ne 'Phase LMAX-R130 - No-External Weekday Market-Hours Runtime Retry Readiness Package' -or
    $next.r130ExternalActivationAllowed) {
    Fail 'LMAX_R129_FAIL_BUILD_OR_TESTS' 'Next phase recommendation missing or unsafe.'
}

if ($summary.buildResult -notlike 'PASS*' -or $summary.focusedTests -notlike 'PASS*' -or
    $summary.unitTests -notlike 'PASS*' -or $summary.integrationTests -notlike 'PASS*' -or
    $summary.validatorResult -ne 'LMAX_R129_VALIDATION_PASS') {
    Fail 'LMAX_R129_FAIL_BUILD_OR_TESTS' 'Build/test/validator evidence missing.'
}

$source = Get-Content -LiteralPath (Join-Path $root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualMarketDataRequestOperation.cs') -Raw
foreach ($category in $requiredCategories) {
    if ($source -notmatch [regex]::Escape($category)) {
        Fail 'LMAX_R129_FAIL_SESSIONREJECT_REASON_ENRICHMENT_MISSING' "Source missing enriched category: $category"
    }
}

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r129-*' -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$joined = $artifactText -join "`n"
$blockedPatterns = @('password=', 'username=', 'SenderCompID=', 'TargetCompID=', 'BEGIN CERTIFICATE', 'PRIVATE KEY', '35=', '58=')
foreach ($pattern in $blockedPatterns) {
    if ($joined.IndexOf($pattern, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        Fail 'LMAX_R129_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' "Sensitive/protocol pattern found in R129 artifacts: $pattern"
    }
}

Write-Host 'LMAX_R129_VALIDATION_PASS'
