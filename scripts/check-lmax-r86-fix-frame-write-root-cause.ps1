param(
    [string]$Root = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'

function Fail([string]$Message) {
    Write-Error "LMAX_R86_VALIDATION_FAIL: $Message"
    exit 1
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing required artifact: $Path"
    }

    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Require-True($Value, [string]$Message) {
    if ($Value -ne $true) {
        Fail $Message
    }
}

function Require-False($Value, [string]$Message) {
    if ($Value -ne $false) {
        Fail $Message
    }
}

function Require-Equal($Actual, $Expected, [string]$Message) {
    if ($Actual -ne $Expected) {
        Fail "$Message Expected=[$Expected] Actual=[$Actual]"
    }
}

$artifactRoot = Join-Path $Root 'artifacts/readiness/lmax-runtime-enablement'
$summary = Read-Json (Join-Path $artifactRoot 'phase-lmax-r86-fix-session-boundary-root-cause-summary.json')
$gate = Read-Json (Join-Path $artifactRoot 'phase-lmax-r86-gate-validation.json')
$r85Summary = Read-Json (Join-Path $artifactRoot 'phase-lmax-r85-temporary-readonly-activation-retry-summary.json')
$r85Fix = Read-Json (Join-Path $artifactRoot 'phase-lmax-r85-fix-session-boundary-evidence.json')
$writer = Read-Json (Join-Path $artifactRoot 'phase-lmax-r86-fix-frame-writer-requirements-review.json')
$builder = Read-Json (Join-Path $artifactRoot 'phase-lmax-r86-fix-logon-frame-builder-review.json')
$stream = Read-Json (Join-Path $artifactRoot 'phase-lmax-r86-tls-stream-to-fix-writer-review.json')
$noExternal = Read-Json (Join-Path $artifactRoot 'phase-lmax-r86-no-external-boundary-attempted.json')
$sanitization = Read-Json (Join-Path $artifactRoot 'phase-lmax-r86-credential-endpoint-tls-fix-sanitization-validation.json')
$marketData = Read-Json (Join-Path $artifactRoot 'phase-lmax-r86-marketdata-block-after-fix-failure-review.json')
$forbidden = Read-Json (Join-Path $artifactRoot 'phase-lmax-r86-forbidden-actions-audit.json')
$apiWorker = Read-Json (Join-Path $artifactRoot 'phase-lmax-r86-api-worker-fake-gateway-audit.json')
$usdjpy = Read-Json (Join-Path $artifactRoot 'phase-lmax-r86-usdjpy-caveat-preservation.json')
$next = Read-Json (Join-Path $artifactRoot 'phase-lmax-r86-next-phase-recommendation.json')

Require-Equal $summary.classification 'LMAX_R86_PASS_FIX_FRAME_WRITE_ROOT_CAUSE_IDENTIFIED_NO_EXTERNAL_ACTIVATION' 'R86 classification mismatch.'
Require-Equal $gate.classification $summary.classification 'Gate classification mismatch.'

Require-Equal $r85Summary.classification 'LMAX_R85_FAIL_FIX_LOGON_OR_SESSION_BOUNDARY' 'R85 classification missing or mismatched.'
Require-True $r85Summary.tcpSocketSucceeded 'R85 TCP success evidence is missing.'
Require-Equal $r85Summary.tlsBoundaryResult 'Succeeded' 'R85 TLS success evidence is missing.'
Require-True $r85Summary.fixLogonSessionAttempted 'R85 FIX boundary was not reached.'
Require-True $r85Summary.socketConnectorOpenFixSessionReached 'R85 OpenFixSession evidence is missing.'
Require-Equal $r85Summary.fixBoundaryResultCategory 'FixFrameWriteNotImplemented' 'R85 FixFrameWriteNotImplemented evidence is missing.'
Require-Equal $r85Fix.fixBoundaryResultCategory 'FixFrameWriteNotImplemented' 'R85 FIX session evidence did not acknowledge FixFrameWriteNotImplemented.'

Require-Equal $summary.responsibleClass 'LmaxReadOnlyActivationManualTcpSocketConnector' 'Responsible class is not named.'
Require-Equal $summary.responsibleFactory 'LmaxReadOnlyActivationManualExecutionSurfaceFactory.CreateRealBoundedExecutableReadOnlyAdapter' 'Responsible factory is not named.'
Require-Equal $summary.responsibleClient 'LmaxRealReadOnlyFixFrameClient' 'Responsible client is not named.'
Require-Equal $summary.responsibleOperation 'LmaxReadOnlyActivationManualTcpSocketConnector.OpenFixSession' 'Responsible operation is not named.'

Require-False $writer.approvedRuntimeFixFrameWriterExists 'Approved runtime FIX frame writer should not be reported as present in R86.'
Require-False $builder.approvedRuntimeFixLogonFrameBuilderExists 'Approved runtime FIX logon frame builder should not be reported as present in R86.'
Require-True $stream.tlsStreamAvailableToFutureWriter 'TLS stream availability review is missing.'
Require-False $stream.tlsStreamPassedToApprovedFixWriterToday 'TLS stream should not be passed to a writer in R86.'
Require-True $summary.safeNextFixNeeded 'Safe next fix decision is missing.'

Require-False $noExternal.externalActivationAttempted 'External activation attempted during R86.'
Require-False $noExternal.socketOpened 'Socket opened during R86.'
Require-False $noExternal.tcpSocketAttempted 'TCP attempted during R86.'
Require-False $noExternal.tlsAttempted 'TLS attempted during R86.'
Require-False $noExternal.fixLogonAttempted 'FIX logon attempted during R86.'
Require-False $noExternal.fixFrameWriteAttempted 'FIX frame write attempted during R86.'
Require-False $noExternal.marketDataRequestAttempted 'MarketDataRequest attempted during R86.'

Require-False $sanitization.credentialValuesReturned 'credentialValuesReturned must remain false.'
Require-False $sanitization.rawCredentialsSerialized 'Raw credentials were serialized.'
Require-False $sanitization.rawFixMessagesSerialized 'Raw FIX messages were serialized.'
Require-False $sanitization.rawSensitiveFixLogsStored 'Sensitive FIX logs were stored.'
Require-True $sanitization.sanitizedEvidenceOnly 'Sanitized evidence validation is missing.'

Require-True $marketData.marketDataBlockedAfterFixFailure 'MarketDataRequest must remain blocked after FIX failure.'
Require-False $marketData.marketDataAttemptAllowedWithoutFixSuccess 'MarketDataRequest was allowed without FIX success.'

Require-Equal $forbidden.audit 'PASS' 'Forbidden action audit did not pass.'
Require-False $forbidden.ordersTouched 'Order path was touched.'
Require-False $forbidden.newOrderSingleTouched 'NewOrderSingle path was touched.'
Require-False $forbidden.tradingStateMutationTouched 'Trading state mutation path was touched.'
Require-False $forbidden.schedulerIntroduced 'Scheduler was introduced.'
Require-False $forbidden.pollingIntroduced 'Polling was introduced.'

Require-Equal $apiWorker.audit 'PASS' 'API/Worker audit did not pass.'
Require-Equal $apiWorker.apiWorkerGateway 'FakeLmaxGatewayOnly' 'API/Worker gateway changed.'
Require-False $apiWorker.manualCliReachableFromApiWorker 'Manual CLI became reachable from API/Worker.'
Require-False $apiWorker.fixFrameWriterReachableFromApiWorker 'FIX frame writer became reachable from API/Worker.'

Require-True $usdjpy.caveatPreserved 'USDJPY caveat was not preserved.'
Require-Equal $next.nextRecommendedPhase 'LMAX-R87' 'Next phase recommendation is missing or incorrect.'

$connector = Get-Content -LiteralPath (Join-Path $Root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualTcpSocketConnector.cs') -Raw
$factory = Get-Content -LiteralPath (Join-Path $Root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualExecutionSurfaceFactory.cs') -Raw
$clients = Get-Content -LiteralPath (Join-Path $Root 'src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxRealReadOnlyProviderClients.cs') -Raw
$transport = Get-Content -LiteralPath (Join-Path $Root 'src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxRealReadOnlyMarketDataTransport.cs') -Raw

if ($connector -notmatch 'FixFrameWriteNotImplemented') { Fail 'Connector source no longer contains FixFrameWriteNotImplemented.' }
if ($connector -notmatch 'tlsStream is null \|\| !tlsStream\.IsAuthenticated') { Fail 'Connector source no longer gates FIX on authenticated TLS stream.' }
if ($connector -notmatch 'RealSecretMaterialLoaded') { Fail 'Connector source no longer gates FIX on in-memory credential material.' }
if ($factory -notmatch 'socketConnector\.OpenFixSession') { Fail 'Manual factory no longer binds OpenFixSession.' }
if ($clients -notmatch 'DefaultNotConfigured') { Fail 'FIX client not-configured fallback is missing.' }
if ($transport -notmatch 'if \(!fix\.Succeeded\)') { Fail 'Transport no longer blocks MarketDataRequest until FIX succeeds.' }

$artifactFiles = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r86-*' -File
$sensitivePatterns = @(
    'BEGIN PRIVATE KEY',
    'END PRIVATE KEY',
    'password\s*[:=]',
    'username\s*[:=]',
    'secret\s*[:=]',
    'token\s*[:=]',
    'session[_ -]?token\s*[:=]',
    'sendercompid\s*[:=]',
    'targetcompid\s*[:=]',
    'raw\s*fix\s*log\s*:',
    'fix\s*message\s*:',
    '35=D',
    '35=F',
    '35=H',
    '35=AE'
)

foreach ($file in $artifactFiles) {
    $text = Get-Content -LiteralPath $file.FullName -Raw
    foreach ($pattern in $sensitivePatterns) {
        if ($text -match $pattern) {
            Fail "Sensitive or forbidden artifact content matched pattern [$pattern] in $($file.Name)."
        }
    }
}

Write-Output 'LMAX_R86_VALIDATION_PASS'
