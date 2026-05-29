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
        Fail 'LMAX_R130_FAIL_BUILD_OR_TESTS' "Missing artifact: $name"
    }

    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$required = @(
    'phase-lmax-r130-weekday-market-hours-readiness-summary.md',
    'phase-lmax-r130-weekday-market-hours-readiness.json',
    'phase-lmax-r130-r131-operator-approval-template.md',
    'phase-lmax-r130-r131-activation-prompt-compact.md',
    'phase-lmax-r130-r131-preflight-checklist.json',
    'phase-lmax-r130-r129-enrichment-readiness-check.json',
    'phase-lmax-r130-sanitization-audit.json',
    'phase-lmax-r130-forbidden-actions-audit.json',
    'phase-lmax-r130-api-worker-fake-gateway-audit.json',
    'phase-lmax-r130-next-phase-recommendation.json'
)

foreach ($artifact in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $artifact))) {
        Fail 'LMAX_R130_FAIL_BUILD_OR_TESTS' "Missing artifact: $artifact"
    }
}

$r127 = Read-Json 'phase-lmax-r127-temporary-readonly-activation-retry-summary.json'
$r128 = Read-Json 'phase-lmax-r128-temporary-runtime-evidence-review-summary.json'
$r129 = Read-Json 'phase-lmax-r129-sessionreject-reason-enrichment.json'
$summary = Read-Json 'phase-lmax-r130-weekday-market-hours-readiness.json'
$preflight = Read-Json 'phase-lmax-r130-r131-preflight-checklist.json'
$enrichment = Read-Json 'phase-lmax-r130-r129-enrichment-readiness-check.json'
$sanitize = Read-Json 'phase-lmax-r130-sanitization-audit.json'
$forbidden = Read-Json 'phase-lmax-r130-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r130-api-worker-fake-gateway-audit.json'
$next = Read-Json 'phase-lmax-r130-next-phase-recommendation.json'

if ($r127.classification -ne 'LMAX_R127_FAIL_MARKETDATA_RESPONSE_BOUNDARY' -or
    $r127.attemptCount -ne 1 -or
    $r127.marketDataResponseCategory -ne 'SessionRejectObserved') {
    Fail 'LMAX_R130_FAIL_R127_R128_R129_EVIDENCE_MISSING' 'R127 evidence missing or mismatched.'
}

if ($r128.classification -ne 'LMAX_R128_PASS_RUNTIME_EVIDENCE_REVIEW_INCONCLUSIVE_SAFE_NO_EXTERNAL_ACTIVATION' -or
    -not $r128.marketClosedOrSessionNotAvailablePlausible -or
    $r128.overClassifiedAsRequestShapeBug) {
    Fail 'LMAX_R130_FAIL_R127_R128_R129_EVIDENCE_MISSING' 'R128 evidence missing or mismatched.'
}

if ($r129.classification -ne 'LMAX_R129_PASS_SESSIONREJECT_REASON_ENRICHMENT_READY_NO_EXTERNAL' -or
    -not $r129.sessionRejectReasonEnrichmentReady -or
    -not $r129.noExternal) {
    Fail 'LMAX_R130_FAIL_R129_ENRICHMENT_NOT_READY' 'R129 enrichment evidence missing or mismatched.'
}

if ($summary.classification -ne 'LMAX_R130_PASS_WEEKDAY_MARKET_HOURS_RETRY_READINESS_PACKAGE_NO_EXTERNAL') {
    Fail 'LMAX_R130_FAIL_BUILD_OR_TESTS' 'Unexpected R130 classification.'
}

if (-not $summary.noExternal -or $summary.externalActivationAttempted -or
    $summary.socketTlsFixMarketDataRuntimeActionAttempted) {
    Fail 'LMAX_R130_FAIL_NEW_EXTERNAL_ACTION_DETECTED' 'R130 attempted or allowed a runtime boundary.'
}

if (-not $summary.r127EvidencePreserved -or -not $summary.r128EvidencePreserved -or
    -not $summary.r129EvidencePreserved -or $summary.r127AttemptCount -ne 1) {
    Fail 'LMAX_R130_FAIL_R127_R128_R129_EVIDENCE_MISSING' 'R127/R128/R129 carryforward evidence missing.'
}

if (-not $summary.r129EnrichmentReady -or -not $summary.r129EnrichedCategoriesIncludedForFutureRetry -or
    -not $enrichment.sessionRejectReasonEnrichmentReady) {
    Fail 'LMAX_R130_FAIL_R129_ENRICHMENT_NOT_READY' 'R129 enrichment readiness missing.'
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
    if ($enrichment.enrichedCategories -notcontains $category) {
        Fail 'LMAX_R130_FAIL_R129_ENRICHMENT_NOT_READY' "Missing R129 enriched category: $category"
    }
}

if (-not $summary.weekdayMarketHoursAwarenessReady -or
    -not $summary.marketHoursAssumptions.weekdayRequired -or
    -not $summary.marketHoursAssumptions.activeFxMarketDataAvailabilityRequired -or
    -not $summary.marketHoursAssumptions.avoidWeekend -or
    -not $summary.marketHoursAssumptions.avoidKnownMaintenanceWindow -or
    -not $summary.marketHoursAssumptions.operatorMustConfirmTimingBeforeR131 -or
    -not $summary.marketHoursAssumptions.marketClosedOrSessionUnavailableCauseClassPreserved -or
    -not $summary.marketHoursAssumptions.noEntriesOutOfHoursCauseClassPreserved) {
    Fail 'LMAX_R130_FAIL_MARKET_HOURS_AWARENESS_MISSING' 'Market-hours/session availability awareness missing.'
}

$approvalTemplate = Get-Content -LiteralPath (Join-Path $artifactRoot 'phase-lmax-r130-r131-operator-approval-template.md') -Raw
if ($approvalTemplate -notmatch 'I, Philippe, explicitly approve Phase LMAX-R131' -or
    $approvalTemplate -notmatch 'weekday market-hours' -or
    $approvalTemplate -notmatch 'one temporary QQ Workspace Demo' -or
    $approvalTemplate -notmatch 'no orders') {
    Fail 'LMAX_R130_FAIL_R131_APPROVAL_TEMPLATE_MISSING' 'R131 approval template missing required exact approval content.'
}

$prompt = Get-Content -LiteralPath (Join-Path $artifactRoot 'phase-lmax-r130-r131-activation-prompt-compact.md') -Raw
if ($prompt -notmatch 'LMAX-R131' -or
    $prompt -notmatch 'Fresh exact R131 operator approval is required' -or
    $prompt -notmatch '--adapter-mode real-bounded-executable-readonly' -or
    $prompt -notmatch 'exactly one bounded attempt') {
    Fail 'LMAX_R130_FAIL_R131_APPROVAL_TEMPLATE_MISSING' 'R131 compact prompt missing required constraints.'
}

if (-not $preflight.freshOperatorApprovalRequired -or -not $preflight.approvalTemplatePresent -or
    -not $preflight.doNotReusePriorApproval -or -not $preflight.weekdayMarketHoursConfirmedByOperatorRequired -or
    -not $preflight.singleBoundedAttemptOnly -or
    $preflight.adapterModeRequired -ne 'real-bounded-executable-readonly' -or
    -not $preflight.r129EnrichmentRequired) {
    Fail 'LMAX_R130_FAIL_R131_APPROVAL_TEMPLATE_MISSING' 'R131 preflight checklist missing required controls.'
}

$expectedInstruments = @('GBPUSD', 'EURGBP', 'AUDUSD', 'USDJPY')
if (($summary.approvedInstruments -join ',') -ne ($expectedInstruments -join ',') -or
    ($preflight.approvedInstrumentsExact -join ',') -ne ($expectedInstruments -join ',') -or
    -not $preflight.nonApprovedInstrumentsForbidden) {
    Fail 'LMAX_R130_FAIL_BUILD_OR_TESTS' 'Approved instrument scope weakened.'
}

if ($summary.usdJpySecurityId -ne '4004' -or $summary.usdJpySecurityIdSource -ne '8' -or
    -not $summary.usdJpyCaveatPreserved -or
    $preflight.usdJpySecurityId -ne '4004' -or $preflight.usdJpySecurityIdSource -ne '8' -or
    -not $preflight.usdJpyCaveatPreserved) {
    Fail 'LMAX_R130_FAIL_USDJPY_CAVEAT_WEAKENED' 'USDJPY caveat weakened.'
}

if ($sanitize.result -ne 'PASS' -or $sanitize.credentialValuesReturned -or
    $sanitize.rawCredentialsSerialized -or $sanitize.rawFixMessagesSerialized -or
    $sanitize.rawRejectTextSerialized -or $sanitize.rawSessionIdentifiersSerialized -or
    $sanitize.rawCompIdsSerialized -or $sanitize.rawTlsMaterialSerialized -or
    $sanitize.rawEndpointSerialized -or $summary.credentialValuesReturned -or
    $summary.rawSensitiveValuesSerialized) {
    Fail 'LMAX_R130_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' 'Sanitization audit failed.'
}

if ($forbidden.result -ne 'PASS' -or $forbidden.externalActivationAttempted -or
    $forbidden.socketOpened -or $forbidden.tlsAttempted -or $forbidden.fixAttempted -or
    $forbidden.marketDataRequestSent -or $forbidden.liveMarketDataResponseRead -or
    $forbidden.orders -or $forbidden.newOrderSingle -or $forbidden.cancelReplace -or
    $forbidden.tradingEnablement -or $forbidden.tradingStateMutation -or
    $forbidden.productionAccount -or $forbidden.apiStartup -or $forbidden.workerStartup -or
    $forbidden.scheduler -or $forbidden.pollingLoop -or $forbidden.service -or
    $forbidden.replay -or $forbidden.shadowReplay) {
    Fail 'LMAX_R130_FAIL_FORBIDDEN_ACTION_RISK' 'Forbidden action audit failed.'
}

if ($apiWorker.result -ne 'PASS' -or -not $apiWorker.apiWorkerFakeLmaxGatewayOnly -or
    $apiWorker.apiStartupAttempted -or $apiWorker.workerStartupAttempted -or
    $apiWorker.runtimeActivationPerformed) {
    Fail 'LMAX_R130_FAIL_API_WORKER_GATEWAY_REGRESSION' 'API/Worker FakeLmaxGatewayOnly audit failed.'
}

if ($next.nextRecommendedPhase -ne 'Phase LMAX-R131 - Operator-Approved Single Temporary QQ Workspace Demo Weekday Market-Hours Read-Only Runtime Activation Retry After SessionReject Enrichment' -or
    -not $next.r131FreshOperatorApprovalRequired -or
    -not $next.r131SingleBoundedAttemptOnly -or
    -not $next.r131ExternalActivationAllowedOnlyAfterApproval -or
    $next.r130ExternalActivationAllowed) {
    Fail 'LMAX_R130_FAIL_BUILD_OR_TESTS' 'Next phase recommendation missing or unsafe.'
}

if ($summary.validatorResult -ne 'LMAX_R130_VALIDATION_PASS' -or
    $summary.buildResult -notlike 'PASS*' -or
    $summary.focusedTests -ne 'NOT_REQUIRED_NO_RUNTIME_CODE_CHANGED' -or
    $summary.unitTests -notlike 'PASS*' -or
    $summary.integrationTests -notlike 'PASS*') {
    Fail 'LMAX_R130_FAIL_BUILD_OR_TESTS' 'Build/test/validator evidence missing.'
}

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r130-*' -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$joined = $artifactText -join "`n"
$blockedPatterns = @('password=', 'username=', 'SenderCompID=', 'TargetCompID=', 'BEGIN CERTIFICATE', 'PRIVATE KEY', '35=', '58=')
foreach ($pattern in $blockedPatterns) {
    if ($joined.IndexOf($pattern, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        Fail 'LMAX_R130_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK' "Sensitive/protocol pattern found in R130 artifacts: $pattern"
    }
}

Write-Host 'LMAX_R130_VALIDATION_PASS'
