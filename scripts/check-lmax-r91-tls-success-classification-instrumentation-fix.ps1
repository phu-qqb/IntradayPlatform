param(
    [string]$Root = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'

function Fail([string]$Message) {
    Write-Error "LMAX_R91_VALIDATION_FAIL: $Message"
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
$summary = Read-Json (Join-Path $artifactRoot 'phase-lmax-r91-tls-success-classification-summary.json')
$beforeAfter = Read-Json (Join-Path $artifactRoot 'phase-lmax-r91-r90-root-cause-before-after-classification.json')
$model = Read-Json (Join-Path $artifactRoot 'phase-lmax-r91-tls-boundary-result-model-validation.json')
$categories = Read-Json (Join-Path $artifactRoot 'phase-lmax-r91-tls-sanitized-category-validation.json')
$tlsGate = Read-Json (Join-Path $artifactRoot 'phase-lmax-r91-tls-success-gate-preservation-validation.json')
$fixBlock = Read-Json (Join-Path $artifactRoot 'phase-lmax-r91-fix-block-after-tls-nonsuccess-validation.json')
$marketBlock = Read-Json (Join-Path $artifactRoot 'phase-lmax-r91-marketdata-block-after-tls-nonsuccess-validation.json')
$path = Read-Json (Join-Path $artifactRoot 'phase-lmax-r91-real-bounded-path-validation.json')
$noExternal = Read-Json (Join-Path $artifactRoot 'phase-lmax-r91-no-external-boundary-attempted.json')
$forbidden = Read-Json (Join-Path $artifactRoot 'phase-lmax-r91-forbidden-actions-audit.json')
$apiWorker = Read-Json (Join-Path $artifactRoot 'phase-lmax-r91-api-worker-fake-gateway-audit.json')
$scheduler = Read-Json (Join-Path $artifactRoot 'phase-lmax-r91-no-scheduler-polling-service-audit.json')
$sanitize = Read-Json (Join-Path $artifactRoot 'phase-lmax-r91-credential-endpoint-tls-fix-sanitization-validation.json')
$usdJpy = Read-Json (Join-Path $artifactRoot 'phase-lmax-r91-usdjpy-caveat-preservation.json')
$next = Read-Json (Join-Path $artifactRoot 'phase-lmax-r91-next-phase-recommendation.json')
$gate = Read-Json (Join-Path $artifactRoot 'phase-lmax-r91-gate-validation.json')

$allowed = @(
    'LMAX_R91_PASS_TLS_SUCCESS_CLASSIFICATION_INSTRUMENTATION_READY_NO_EXTERNAL_ACTIVATION',
    'LMAX_R91_PASS_TLS_CLASSIFICATION_DESIGN_READY_IMPLEMENTATION_DEFERRED_NO_EXTERNAL_ACTIVATION',
    'LMAX_R91_FAIL_TLS_CLASSIFICATION_NOT_PROVABLE',
    'LMAX_R91_FAIL_TLS_SUCCESS_GATE_WEAKENED',
    'LMAX_R91_FAIL_FIX_ALLOWED_AFTER_TLS_NONSUCCESS',
    'LMAX_R91_FAIL_MARKETDATA_ALLOWED_WITHOUT_FIX_SUCCESS',
    'LMAX_R91_FAIL_TLS_SANITIZATION_RISK',
    'LMAX_R91_FAIL_CREDENTIAL_VALUES_RETURNED_OR_SECRET_RISK',
    'LMAX_R91_FAIL_API_WORKER_GATEWAY_REGRESSION',
    'LMAX_R91_FAIL_FORBIDDEN_ACTION_INTRODUCED',
    'LMAX_R91_FAIL_EXTERNAL_BOUNDARY_ATTEMPTED',
    'LMAX_R91_FAIL_USDJPY_CAVEAT_WEAKENED',
    'LMAX_R91_FAIL_BUILD_OR_TESTS'
)

if ($allowed -notcontains $summary.classification) {
    Fail 'R91 classification is absent or not allowed.'
}

Require-Equal $summary.classification 'LMAX_R91_PASS_TLS_SUCCESS_CLASSIFICATION_INSTRUMENTATION_READY_NO_EXTERNAL_ACTIVATION' 'R91 classification mismatch.'
Require-True $summary.tlsSuccessClassificationInstrumentationReady 'TLS classification instrumentation is not ready.'
Require-True $summary.tlsSucceededVsAttemptedOnlyDistinct 'TLS Succeeded vs AttemptedOnly is not distinct.'
Require-True $summary.tlsSucceededGateUnchanged 'TLS Succeeded gate changed.'
Require-True $summary.fixBlockedAfterTlsNonSuccess 'FIX block after TLS non-success missing.'
Require-True $summary.marketDataRequestBlockedWithoutFixSuccess 'MarketData block without FIX success missing.'
Require-False $summary.externalActivationAttempted 'External activation attempted during R91.'
Require-False $summary.credentialValuesReturned 'credentialValuesReturned must remain false.'

$requiredCategories = @(
    'Succeeded',
    'AttemptedOnly',
    'Timeout',
    'HandshakeException',
    'CertificateValidationFailure',
    'StreamUnavailable',
    'CancelledOrAborted',
    'UnknownFailure',
    'NotAttempted'
)
foreach ($category in $requiredCategories) {
    if ($summary.supportedSanitizedTlsCategories -notcontains $category) {
        Fail "Missing supported TLS category: $category"
    }
}

Require-Equal $beforeAfter.r90Classification 'LMAX_R90_PASS_TLS_ATTEMPTED_ONLY_DUE_TO_SANITIZED_CLASSIFICATION_LIMIT_NO_EXTERNAL_ACTIVATION' 'R90 evidence missing.'
Require-True $beforeAfter.after.manualCliEmitsTlsSucceeded 'CLI does not emit tlsSucceeded.'
Require-True $beforeAfter.after.manualCliEmitsTlsResultCategory 'CLI does not emit tlsResultCategory.'
Require-True $beforeAfter.after.manualCliEmitsTlsFailureCategory 'CLI does not emit tlsFailureCategory.'
Require-False $beforeAfter.externalBoundaryAttemptedDuringR91 'External boundary attempted during R91.'

Require-Equal $model.result 'PASS' 'TLS result model validation failed.'
Require-Equal $model.modelName 'LmaxSanitizedTlsBoundaryEvidence' 'TLS evidence model missing.'
Require-True $model.transportResultCarriesTlsEvidence 'Transport result does not carry TLS evidence.'
Require-True $model.activationResultCarriesTlsEvidence 'Activation result does not carry TLS evidence.'
Require-True $model.manualCliEmitsTlsEvidence 'Manual CLI does not emit TLS evidence.'
Require-False $model.rawTlsMaterialSerialized 'Raw TLS material serialized.'

Require-Equal $categories.result 'PASS' 'TLS category validation failed.'
Require-True $categories.succeededDistinctFromAttemptedOnly 'Succeeded is not distinct from AttemptedOnly.'
Require-True $categories.timeoutDistinct 'Timeout category missing.'
Require-True $categories.handshakeExceptionDistinct 'HandshakeException category missing.'
Require-True $categories.certificateValidationFailureDistinct 'CertificateValidationFailure category missing.'
Require-True $categories.streamUnavailableDistinct 'StreamUnavailable category missing.'
Require-True $categories.cancelledOrAbortedDistinct 'CancelledOrAborted category missing.'
Require-True $categories.unknownFailureDistinct 'UnknownFailure category missing.'
Require-True $categories.notAttemptedDistinct 'NotAttempted category missing.'
Require-False $categories.rawTlsMaterialSerialized 'Raw TLS material serialized.'
Require-False $categories.rawExceptionDetailsSerialized 'Raw exception details serialized.'

Require-Equal $tlsGate.result 'PASS' 'TLS success gate validation failed.'
Require-True $tlsGate.tlsSucceededGateUnchanged 'TLS success gate weakened.'
Require-True $tlsGate.transportStillUsesTlsSucceeded 'Transport does not use tls.Succeeded.'
Require-True $tlsGate.fixRequiresTlsSucceeded 'FIX no longer requires TLS succeeded.'
Require-True $tlsGate.tlsAttemptedOnlyInsufficientForFix 'AttemptedOnly became sufficient for FIX.'
Require-False $tlsGate.tlsDefaultGlobal 'TLS became global/default.'

Require-Equal $fixBlock.result 'PASS' 'FIX block validation failed.'
Require-True $fixBlock.fixBlockedAfterTlsNonSuccess 'FIX not blocked after TLS non-success.'
Require-False $fixBlock.fixAllowedAfterAttemptedOnly 'FIX allowed after AttemptedOnly.'
Require-False $fixBlock.fixAllowedAfterTimeout 'FIX allowed after Timeout.'
Require-False $fixBlock.fixAllowedAfterHandshakeException 'FIX allowed after HandshakeException.'

Require-Equal $marketBlock.result 'PASS' 'MarketData block validation failed.'
Require-True $marketBlock.marketDataRequestBlockedWithoutFixSuccess 'MarketData not blocked without FIX success.'
Require-False $marketBlock.marketDataRequestAllowedAfterTlsNonSuccess 'MarketData allowed after TLS non-success.'
Require-False $marketBlock.marketDataRequestAllowedWithoutFixSuccess 'MarketData allowed without FIX success.'

Require-Equal $path.result 'PASS' 'Real bounded path validation failed.'
Require-True $path.noExternalDefaultPreserved 'No-external default removed.'
Require-False $path.apiWorkerReachable 'Manual path reachable from API/Worker.'
Require-False $path.productionAccountConfigAllowed 'Production account/config allowed.'
Require-False $path.orderTradingPathReachable 'Order/trading path reachable.'

Require-False $noExternal.externalActivationAttempted 'External activation attempted during R91.'
Require-False $noExternal.socketOpened 'Socket opened during R91.'
Require-False $noExternal.tcpSocketAttempted 'TCP attempted during R91.'
Require-False $noExternal.tlsAttempted 'TLS attempted during R91.'
Require-False $noExternal.fixLogonAttempted 'FIX attempted during R91.'
Require-False $noExternal.marketDataRequestAttempted 'MarketDataRequest attempted during R91.'

Require-Equal $forbidden.result 'PASS' 'Forbidden actions audit failed.'
Require-False $forbidden.ordersSubmitted 'Orders submitted.'
Require-False $forbidden.orderPathTouched 'Order path touched.'
Require-False $forbidden.tradingStateMutated 'Trading state mutated.'
Require-False $forbidden.productionAccountUsedOrAllowed 'Production account/config used or allowed.'

Require-Equal $apiWorker.result 'PASS' 'API/Worker audit failed.'
Require-Equal $apiWorker.apiWorkerGatewayMode 'FakeLmaxGatewayOnly' 'API/Worker gateway changed.'
Require-False $apiWorker.apiWorkerGatewayChanged 'API/Worker gateway changed.'
Require-False $apiWorker.tlsBoundaryReachableFromApiWorkerDefaultStartup 'TLS boundary reachable from API/Worker.'

Require-Equal $scheduler.result 'PASS' 'Scheduler/polling audit failed.'
Require-False $scheduler.hostedBackgroundServiceIntroduced 'Hosted service introduced.'
Require-False $scheduler.schedulerIntroduced 'Scheduler introduced.'
Require-False $scheduler.pollingIntroduced 'Polling introduced.'

Require-Equal $sanitize.result 'PASS' 'Sanitization validation failed.'
Require-False $sanitize.credentialValuesReturned 'credentialValuesReturned must remain false.'
Require-False $sanitize.rawCredentialsPrintedStoredSerialized 'Raw credentials exposed.'
Require-False $sanitize.rawEndpointValuesPrintedStoredSerialized 'Raw endpoint values exposed.'
Require-False $sanitize.rawTlsMaterialPrintedStoredSerialized 'Raw TLS material exposed.'
Require-False $sanitize.rawCertificateDumpSerialized 'Raw certificate dump serialized.'
Require-False $sanitize.rawExceptionDetailsSerialized 'Raw exception details serialized.'
Require-False $sanitize.rawFixMessagesPrintedStoredSerialized 'Raw FIX messages exposed.'

Require-Equal $usdJpy.result 'PASS' 'USDJPY caveat validation failed.'
Require-True $usdJpy.caveatPreserved 'USDJPY caveat missing or weakened.'
Require-False $usdJpy.weakened 'USDJPY caveat weakened.'

Require-Equal $gate.validatorResult 'PASS' 'Gate validator result missing.'
Require-Equal $gate.buildResult.status 'PASS' 'Build evidence missing.'
Require-Equal $gate.focusedTestResult.status 'PASS' 'Focused test evidence missing.'
Require-Equal $gate.testResult.status 'PASS' 'Full test evidence missing.'
Require-Equal $next.nextRecommendedPhase 'LMAX-R93' 'Next phase recommendation missing or incorrect.'
Require-Equal $next.nextRecommendedTitle 'Operator-Approved Single Temporary Demo Read-Only Activation Retry After TLS Classification Instrumentation Fix' 'Next phase title mismatch.'

$classifierSource = Get-Content -LiteralPath (Join-Path $Root 'src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxSanitizedTlsBoundaryEvidence.cs') -Raw
$transportSource = Get-Content -LiteralPath (Join-Path $Root 'src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxRealReadOnlyMarketDataTransport.cs') -Raw
$adapterSource = Get-Content -LiteralPath (Join-Path $Root 'src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter.cs') -Raw
$runtimeResultSource = Get-Content -LiteralPath (Join-Path $Root 'src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxTemporaryReadOnlyRuntimeAdapterPath.cs') -Raw
$programSource = Get-Content -LiteralPath (Join-Path $Root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/Program.cs') -Raw
$apiSource = Get-Content -LiteralPath (Join-Path $Root 'src/QQ.Production.Intraday.Api/Program.cs') -Raw
$workerSource = Get-Content -LiteralPath (Join-Path $Root 'src/QQ.Production.Intraday.Worker/Program.cs') -Raw

foreach ($category in $requiredCategories) {
    if ($classifierSource -notmatch [regex]::Escape($category)) {
        Fail "Classifier source missing category: $category"
    }
}
if ($transportSource -notmatch 'if \(!tls\.Succeeded\)') { Fail 'Transport no longer blocks FIX unless TLS succeeds.' }
if ($transportSource -notmatch 'if \(!fix\.Succeeded\)') { Fail 'Transport no longer blocks MarketData unless FIX succeeds.' }
if ($adapterSource -notmatch 'TlsEvidence') { Fail 'Adapter/transport result does not carry TLS evidence.' }
if ($runtimeResultSource -notmatch 'TlsEvidence') { Fail 'Activation result does not carry TLS evidence.' }
foreach ($field in @('tlsSucceeded=', 'tlsBoundaryStatus=', 'tlsResultCategory=', 'tlsFailureCategory=', 'tlsTimedOut=', 'tlsExceptionCategory=', 'tlsStreamAvailableForFix=', 'tlsRawMaterialSerialized=')) {
    if ($programSource -notmatch [regex]::Escape($field)) {
        Fail "Manual CLI missing TLS field: $field"
    }
}
if ($apiSource -match 'LmaxSanitizedTlsBoundaryEvidence|LmaxReadOnlyActivationManualTcpSocketConnector') { Fail 'TLS instrumentation reachable from API startup.' }
if ($workerSource -match 'LmaxSanitizedTlsBoundaryEvidence|LmaxReadOnlyActivationManualTcpSocketConnector') { Fail 'TLS instrumentation reachable from Worker startup.' }

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r91-*' -File |
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
    'raw\s*fix\s*log\s*:',
    'raw\s*tls\s*material\s*:',
    'certificate\s*dump\s*:'
)

foreach ($pattern in $forbiddenPatterns) {
    if ($joined -match $pattern) {
        Fail "Forbidden sensitive artifact pattern found in R91 artifacts: $pattern"
    }
}

Write-Output 'LMAX_R91_VALIDATION_PASS'
