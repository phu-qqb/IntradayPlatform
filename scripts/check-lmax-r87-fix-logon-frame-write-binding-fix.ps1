param(
    [string]$Root = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'

function Fail([string]$Message) {
    Write-Error "LMAX_R87_VALIDATION_FAIL: $Message"
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
$summary = Read-Json (Join-Path $artifactRoot 'phase-lmax-r87-fix-logon-frame-write-binding-summary.json')
$gate = Read-Json (Join-Path $artifactRoot 'phase-lmax-r87-gate-validation.json')
$builder = Read-Json (Join-Path $artifactRoot 'phase-lmax-r87-fix-logon-frame-builder-validation.json')
$writer = Read-Json (Join-Path $artifactRoot 'phase-lmax-r87-fix-frame-writer-binding-validation.json')
$sessionOnly = Read-Json (Join-Path $artifactRoot 'phase-lmax-r87-session-only-fix-safety-validation.json')
$orderExclusion = Read-Json (Join-Path $artifactRoot 'phase-lmax-r87-order-frame-exclusion-validation.json')
$credential = Read-Json (Join-Path $artifactRoot 'phase-lmax-r87-in-memory-credential-use-validation.json')
$rawFix = Read-Json (Join-Path $artifactRoot 'phase-lmax-r87-raw-fix-sanitization-validation.json')
$marketData = Read-Json (Join-Path $artifactRoot 'phase-lmax-r87-marketdata-block-until-fix-success-validation.json')
$path = Read-Json (Join-Path $artifactRoot 'phase-lmax-r87-real-bounded-path-validation.json')
$noExternal = Read-Json (Join-Path $artifactRoot 'phase-lmax-r87-no-external-boundary-attempted.json')
$forbidden = Read-Json (Join-Path $artifactRoot 'phase-lmax-r87-forbidden-actions-audit.json')
$apiWorker = Read-Json (Join-Path $artifactRoot 'phase-lmax-r87-api-worker-fake-gateway-audit.json')
$usdjpy = Read-Json (Join-Path $artifactRoot 'phase-lmax-r87-usdjpy-caveat-preservation.json')
$next = Read-Json (Join-Path $artifactRoot 'phase-lmax-r87-next-phase-recommendation.json')
$r86 = Read-Json (Join-Path $artifactRoot 'phase-lmax-r86-fix-session-boundary-root-cause-summary.json')

Require-Equal $summary.classification 'LMAX_R87_PASS_FIX_LOGON_FRAME_WRITE_BINDING_READY_NO_EXTERNAL_ACTIVATION' 'R87 classification mismatch.'
Require-Equal $gate.classification $summary.classification 'R87 gate classification mismatch.'
Require-Equal $r86.classification 'LMAX_R86_PASS_FIX_FRAME_WRITE_ROOT_CAUSE_IDENTIFIED_NO_EXTERNAL_ACTIVATION' 'R86 root-cause evidence missing.'

Require-True $summary.fixFrameWriteBlockerClearedForFutureApprovedManualRealBoundedPath 'FIX frame write blocker was not cleared.'
Require-True $builder.fixLogonBuilderReady 'Approved FIX Logon frame builder is not provable.'
Require-True $writer.fixFrameWriterReady 'Approved FIX frame writer binding is not provable.'
Require-True $builder.sessionOnly 'Builder is not session-only.'
Require-True $builder.logonOnly 'Builder is not logon-only.'
Require-False $builder.orderFramesSupported 'Builder supports order frames.'
Require-False $builder.newOrderSingleSupported 'Builder supports NewOrderSingle.'
Require-False $builder.cancelReplaceSupported 'Builder supports cancel/replace.'
Require-False $orderExclusion.newOrderSingleSupported 'NewOrderSingle support introduced.'
Require-False $orderExclusion.cancelReplaceSupported 'Cancel/replace support introduced.'
Require-False $orderExclusion.tradingMutationSupported 'Trading mutation support introduced.'

Require-True $sessionOnly.marketDataRequestRequiresFixSessionSuccess 'MarketDataRequest no longer requires FIX success.'
Require-True $marketData.marketDataRequestBlockedUntilFixSuccess 'MarketDataRequest is not blocked until FIX success.'
Require-False $marketData.marketDataAttemptAllowedWithoutFixSuccess 'MarketDataRequest can be attempted without FIX success.'
Require-True $writer.sessionAcknowledgementNotFaked 'FIX session success was faked after frame write.'
Require-True $writer.fixSessionSuccessNotClaimedAfterFrameWriteOnly 'Frame write incorrectly claims full FIX success.'

Require-False $credential.credentialMaterialReturnedToCaller 'Credential material returned to caller.'
Require-False $credential.credentialValuesReturned 'credentialValuesReturned must remain false.'
Require-False $credential.rawCredentialsSerialized 'Raw credentials serialized.'
Require-False $rawFix.rawFixMessagesSerialized 'Raw FIX messages serialized.'
Require-False $rawFix.rawFixMessagesPrinted 'Raw FIX messages printed.'
Require-False $rawFix.rawSensitiveFixLogsStored 'Sensitive FIX logs stored.'

Require-True $path.fixWriterBoundOnlyInManualRealBoundedPath 'FIX writer is not scoped to manual real-bounded path.'
Require-False $path.fixWriterGlobalDefault 'FIX write became global/default.'
Require-False $path.apiWorkerReachable 'FIX writer became reachable from API/Worker.'
Require-True $path.noExternalDefaultPreserved 'No-external default was not preserved.'
Require-False $path.productionAccountConfigAllowed 'Production account/config allowed.'

Require-False $noExternal.externalActivationAttempted 'External activation attempted during R87.'
Require-False $noExternal.socketOpened 'Socket opened during R87.'
Require-False $noExternal.tcpSocketAttempted 'TCP attempted during R87.'
Require-False $noExternal.tlsAttempted 'TLS attempted during R87.'
Require-False $noExternal.liveFixFrameWriteAttempted 'Live FIX frame write attempted during R87.'
Require-False $noExternal.fixLogonAttempted 'FIX logon attempted during R87.'
Require-False $noExternal.marketDataRequestAttempted 'MarketDataRequest attempted during R87.'

Require-Equal $forbidden.audit 'PASS' 'Forbidden action audit failed.'
Require-False $forbidden.ordersTouched 'Orders were touched.'
Require-False $forbidden.newOrderSingleIntroduced 'NewOrderSingle introduced.'
Require-False $forbidden.cancelReplaceIntroduced 'Cancel/replace introduced.'
Require-False $forbidden.schedulerIntroduced 'Scheduler introduced.'
Require-False $forbidden.pollingIntroduced 'Polling introduced.'
Require-Equal $apiWorker.audit 'PASS' 'API/Worker audit failed.'
Require-Equal $apiWorker.apiWorkerGateway 'FakeLmaxGatewayOnly' 'API/Worker gateway changed.'
Require-False $apiWorker.fixWriterReachableFromApiWorker 'FIX writer reachable from API/Worker.'
Require-True $usdjpy.caveatPreserved 'USDJPY caveat weakened.'
Require-Equal $next.nextRecommendedPhase 'LMAX-R89' 'Next phase recommendation must be R89.'

$connector = Get-Content -LiteralPath (Join-Path $Root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualTcpSocketConnector.cs') -Raw
$writerSource = Get-Content -LiteralPath (Join-Path $Root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualFixLogonFrameWriter.cs') -Raw
$transport = Get-Content -LiteralPath (Join-Path $Root 'src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxRealReadOnlyMarketDataTransport.cs') -Raw
$api = Get-Content -LiteralPath (Join-Path $Root 'src/QQ.Production.Intraday.Api/Program.cs') -Raw
$worker = Get-Content -LiteralPath (Join-Path $Root 'src/QQ.Production.Intraday.Worker/Program.cs') -Raw

if ($connector -match 'FixFrameWriteNotImplemented') { Fail 'Old FixFrameWriteNotImplemented blocker remains in manual connector source.' }
if ($connector -notmatch 'fixLogonFrameWriter\.WriteLogonFrame') { Fail 'Manual connector does not call the approved FIX logon writer.' }
if ($writerSource -notmatch 'LmaxReadOnlyActivationManualFixLogonFrameBuilder') { Fail 'Approved FIX Logon frame builder source is missing.' }
if ($writerSource -notmatch 'ManualFixLogonFrameWriteSucceededSanitized') { Fail 'Writer does not expose sanitized frame-write evidence.' }
if ($writerSource -notmatch 'FixSessionAcknowledgementNotImplemented') { Fail 'Writer does not preserve session acknowledgement as future boundary.' }
if ($transport -notmatch 'if \(!fix\.Succeeded\)') { Fail 'Transport no longer blocks MarketDataRequest until FIX succeeds.' }
if ($api -match 'LmaxReadOnlyActivationManualFixLogonFrameWriter') { Fail 'FIX writer is reachable from API startup.' }
if ($worker -match 'LmaxReadOnlyActivationManualFixLogonFrameWriter') { Fail 'FIX writer is reachable from Worker startup.' }

$artifactFiles = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r87-*' -File
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
    '35=AE',
    '554='
)

foreach ($file in $artifactFiles) {
    $text = Get-Content -LiteralPath $file.FullName -Raw
    foreach ($pattern in $sensitivePatterns) {
        if ($text -match $pattern) {
            Fail "Sensitive artifact content matched pattern [$pattern] in $($file.Name)."
        }
    }
}

Write-Output 'LMAX_R87_VALIDATION_PASS'
