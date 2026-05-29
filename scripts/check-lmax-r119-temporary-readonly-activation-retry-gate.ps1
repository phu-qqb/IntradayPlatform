$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts/readiness/lmax-runtime-enablement'

function Read-Json($name) {
    $path = Join-Path $artifactRoot $name
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing artifact: $name"
    }

    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

function Assert-True($condition, $message) {
    if (-not $condition) {
        throw $message
    }
}

$requiredArtifacts = @(
    'phase-lmax-r119-temporary-readonly-activation-retry-report.md',
    'phase-lmax-r119-temporary-readonly-activation-retry-summary.json',
    'phase-lmax-r119-operator-approval-note.md',
    'phase-lmax-r119-preflight-result.json',
    'phase-lmax-r119-demo-endpoint-binding-evidence.json',
    'phase-lmax-r119-socket-connector-evidence.json',
    'phase-lmax-r119-tls-boundary-evidence.json',
    'phase-lmax-r119-fix-credential-material-evidence.json',
    'phase-lmax-r119-fix-session-identifier-binding-evidence.json',
    'phase-lmax-r119-fix-username-password-tag-binding-evidence.json',
    'phase-lmax-r119-fix-logon-frame-write-evidence.json',
    'phase-lmax-r119-fix-session-acknowledgement-evidence.json',
    'phase-lmax-r119-fix-session-boundary-evidence.json',
    'phase-lmax-r119-marketdata-request-evidence.json',
    'phase-lmax-r119-operational-invocation-trace.json',
    'phase-lmax-r119-boundary-evidence.json',
    'phase-lmax-r119-marketdata-sanitized-result.json',
    'phase-lmax-r119-forbidden-actions-audit.json',
    'phase-lmax-r119-api-worker-fake-gateway-audit.json',
    'phase-lmax-r119-usdjpy-caveat-preservation.json',
    'phase-lmax-r119-shutdown-revert-evidence.json',
    'phase-lmax-r119-next-phase-recommendation.json',
    'phase-lmax-r119-gate-validation.json'
)

foreach ($artifact in $requiredArtifacts) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactRoot $artifact)) "Missing artifact: $artifact"
}

$r117 = Read-Json 'phase-lmax-r117-fix-logon-session-identifier-summary.json'
$summary = Read-Json 'phase-lmax-r119-temporary-readonly-activation-retry-summary.json'
$preflight = Read-Json 'phase-lmax-r119-preflight-result.json'
$endpoint = Read-Json 'phase-lmax-r119-demo-endpoint-binding-evidence.json'
$socket = Read-Json 'phase-lmax-r119-socket-connector-evidence.json'
$tls = Read-Json 'phase-lmax-r119-tls-boundary-evidence.json'
$fixCredential = Read-Json 'phase-lmax-r119-fix-credential-material-evidence.json'
$sessionIds = Read-Json 'phase-lmax-r119-fix-session-identifier-binding-evidence.json'
$tagBinding = Read-Json 'phase-lmax-r119-fix-username-password-tag-binding-evidence.json'
$frameWrite = Read-Json 'phase-lmax-r119-fix-logon-frame-write-evidence.json'
$fixAck = Read-Json 'phase-lmax-r119-fix-session-acknowledgement-evidence.json'
$fixBoundary = Read-Json 'phase-lmax-r119-fix-session-boundary-evidence.json'
$marketData = Read-Json 'phase-lmax-r119-marketdata-request-evidence.json'
$trace = Read-Json 'phase-lmax-r119-operational-invocation-trace.json'
$boundary = Read-Json 'phase-lmax-r119-boundary-evidence.json'
$marketDataResult = Read-Json 'phase-lmax-r119-marketdata-sanitized-result.json'
$forbidden = Read-Json 'phase-lmax-r119-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r119-api-worker-fake-gateway-audit.json'
$usdjpy = Read-Json 'phase-lmax-r119-usdjpy-caveat-preservation.json'
$shutdown = Read-Json 'phase-lmax-r119-shutdown-revert-evidence.json'
$next = Read-Json 'phase-lmax-r119-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r119-gate-validation.json'

Assert-True ($r117.classification -eq 'LMAX_R117_PASS_FIX_LOGON_SESSION_IDENTIFIER_BINDING_READY_NO_EXTERNAL_ACTIVATION') 'R117 success evidence is missing or mismatched.'
Assert-True ($r117.senderCompIdBindingReady -eq $true -and $r117.targetCompIdBindingReady -eq $true -and $r117.sessionIdentifierMaterialBindingReady -eq $true) 'R117 session identifier binding success evidence is missing.'
Assert-True ($r117.placeholderLabelsEliminatedFromActualInMemoryLogonFramePath -eq $true) 'R117 placeholder elimination evidence is missing.'
Assert-True ($r117.credentialValuesReturned -eq $false) 'R117 credentialValuesReturned must be false.'

Assert-True ($summary.classification -eq 'LMAX_R119_FAIL_MARKETDATA_REQUEST_BOUNDARY') 'Unexpected R119 classification.'
Assert-True ($summary.externalActivationAttempted -eq $true) 'R119 must record the approved external activation attempt.'
Assert-True ($summary.attemptCount -eq 1) 'R119 must record exactly one external attempt.'
Assert-True ($summary.retryPhaseReservationPassed -eq $true) 'R119 retry phase reservation must pass.'
Assert-True ($summary.toolUsed -eq 'QQ.Production.Intraday.Tools.LmaxReadOnlyActivation') 'Unexpected activation tool.'
Assert-True ($summary.adapterMode -eq 'real-bounded-executable-readonly') 'Unexpected adapter mode.'

Assert-True ($preflight.operatorApprovalMatched -eq $true -and $preflight.r117SuccessEvidencePresent -eq $true) 'Pre-external approval or R117 evidence is missing.'
Assert-True ($preflight.retryPhaseReservationPassed -eq $true -and $preflight.executeOnceInvoked -eq $true -and $preflight.singleAttemptOnly -eq $true) 'Preflight execution reservation/single-attempt proof missing.'
Assert-True ($preflight.noApiWorkerStartup -eq $true -and $preflight.noServiceSchedulerPolling -eq $true -and $preflight.noOrderTradingPath -eq $true -and $preflight.noCredentialOutput -eq $true) 'Required safety flags missing.'

Assert-True ($endpoint.endpointMode -eq 'Demo') 'Endpoint mode must be Demo.'
Assert-True ($endpoint.endpointPresent -eq $true -and $endpoint.hostPresent -eq $true -and $endpoint.portPresent -eq $true) 'Endpoint presence evidence is incomplete.'
Assert-True ($endpoint.hostConcreteBinding -eq $true -and $endpoint.hostWasPlaceholder -eq $false -and $endpoint.portConcreteBinding -eq $true) 'Concrete endpoint binding evidence is incomplete.'
Assert-True ($endpoint.productionExcluded -eq $true -and $endpoint.endpointApproved -eq $true) 'Demo endpoint approval evidence is incomplete.'
Assert-True ($endpoint.rawEndpointSerialized -eq $false) 'Raw endpoint values must not be serialized.'

Assert-True ($socket.configuredSocketConnectorUsed -eq $true -and $socket.connectReached -eq $true) 'Socket connector was not proven.'
Assert-True ($socket.tcpConnectionAttempted -eq $true -and $socket.tcpSocketSucceeded -eq $true -and $socket.boundaryStatus -eq 'Succeeded') 'TCP/socket success is not proven.'
Assert-True ($tls.tlsAttempted -eq $true -and $tls.authenticateTlsReached -eq $true) 'TLS was not proven attempted through the socket connector.'
Assert-True ($tls.tlsSucceeded -eq $true -and $tls.tlsResultCategory -eq 'Succeeded') 'TLS must be recorded as succeeded.'
Assert-True ($tls.tlsRawMaterialSerialized -eq $false) 'TLS material must not be serialized.'

Assert-True ($fixCredential.realSecretMaterialAllowedNow -eq $true -and $fixCredential.realSecretMaterialLoaded -eq $true) 'Approved in-memory credential material must be loaded for R119.'
Assert-True ($fixCredential.credentialValuesReturned -eq $false -and $fixCredential.rawSecretSerialized -eq $false) 'Credential values must not be returned or serialized.'
Assert-True ($sessionIds.senderCompIdBound -eq $true -and $sessionIds.targetCompIdBound -eq $true -and $sessionIds.sessionIdentifierMaterialBound -eq $true) 'Session identifier binding evidence missing.'
Assert-True ($sessionIds.senderCompIdWasPlaceholder -eq $false -and $sessionIds.targetCompIdWasPlaceholder -eq $false) 'Session identifier placeholder regression.'
Assert-True ($sessionIds.rawCompIdSerialized -eq $false -and $sessionIds.rawSessionIdSerialized -eq $false) 'Raw CompID/session values must not be serialized.'
Assert-True ($tagBinding.usernameTag553Bound -eq $true -and $tagBinding.passwordTag554Bound -eq $true) 'Username/password tag binding must be proven.'
Assert-True ($tagBinding.usernameValueSerialized -eq $false -and $tagBinding.passwordValueSerialized -eq $false) 'Username/password values must not be serialized.'
Assert-True ($tagBinding.unsupportedTagsInvented -eq $false) 'Unsupported LMAX tags must not be invented.'

Assert-True ($trace.fixLogonAttempted -eq $true) 'FIX logon/session must be recorded as attempted.'
Assert-True ($frameWrite.openFixSessionReached -eq $true -and $frameWrite.sessionLogonOnlyFixFrameWriterUsed -eq $true) 'FIX frame writer path must be proven.'
Assert-True ($frameWrite.resetSeqNumFlagYPresent -eq $true) 'ResetSeqNumFlag=Y policy regressed.'
Assert-True ($frameWrite.rawFixFrameSerialized -eq $false -and $frameWrite.rawSessionValuesSerialized -eq $false -and $frameWrite.orderFramesSupported -eq $false) 'FIX writer safety evidence failed.'
Assert-True ($fixAck.fixAcknowledgementReaderParserClassifierUsed -eq $true) 'FIX acknowledgement reader/parser/classifier use must be recorded.'
Assert-True ($fixAck.fixAcknowledgementCategory -eq 'FixLogonAcknowledged' -and $fixAck.fixSessionAcknowledgementSucceeded -eq $true) 'FIX Logon acknowledgement success evidence is missing.'
Assert-True ($fixAck.orderMessageParsingSupported -eq $false -and $fixAck.executionReportParsingSupported -eq $false -and $fixAck.orderLifecycleSupported -eq $false) 'FIX parser must remain session-level only.'
Assert-True ($fixAck.rawFixMessageSerialized -eq $false) 'FIX messages must not be serialized.'
Assert-True ($fixBoundary.fixBoundaryStatus -eq 'Succeeded' -and $fixBoundary.fixBoundaryCategory -eq 'FixLogonAcknowledged') 'FIX boundary result/category mismatch.'
Assert-True ($fixBoundary.rawFixFrameOrMessageSerialized -eq $false -and $fixBoundary.orderCapableFixFrameOrParserPathAbsent -eq $true) 'FIX boundary safety evidence failed.'

Assert-True ($marketData.fixSessionSucceededBeforeMarketData -eq $true) 'Market data boundary must occur only after FIX success.'
Assert-True ($marketData.marketDataBoundaryReached -eq $true) 'Market data boundary review missing.'
Assert-True ($marketData.marketDataRequestAttempted -eq $false -and $marketData.marketDataRequestSent -eq $false) 'No live MarketDataRequest should have been sent after MarketDataOperationNotConfigured.'
Assert-True ($marketData.marketDataRequestResult -eq 'FailedValidation' -and $marketData.marketDataRequestCategory -eq 'MarketDataOperationNotConfigured') 'MarketDataRequest boundary category mismatch.'
Assert-True ($marketDataResult.marketDataResponseEntriesObserved -eq $false -and $marketDataResult.marketDataResultCategory -eq 'MarketDataOperationNotConfigured') 'Market data sanitized result mismatch.'

Assert-True ($boundary.'Credential/config' -eq 'Succeeded') 'Credential/config boundary mismatch.'
Assert-True ($boundary.'TCP/socket' -eq 'Succeeded') 'TCP/socket boundary mismatch.'
Assert-True ($boundary.TLS -eq 'Succeeded') 'TLS boundary mismatch.'
Assert-True ($boundary.'FIX logon/session' -eq 'Succeeded') 'FIX boundary mismatch.'
Assert-True ($boundary.MarketDataRequest -eq 'FailedValidation' -and $boundary.'MarketDataResponse/entries' -eq 'NotAttempted') 'Market data boundary mismatch.'
Assert-True ($boundary.'Shutdown/revert' -eq 'Succeeded') 'Shutdown/revert must succeed.'

Assert-True ($summary.credentialValuesReturned -eq $false) 'credentialValuesReturned must remain false.'
Assert-True ($summary.sensitiveValuesPrintedStoredSerialized -eq $false) 'Sensitive values must not be printed/stored/serialized.'
Assert-True ($forbidden.result -eq 'PASS') 'Forbidden action audit failed.'
Assert-True ($forbidden.orders -eq $false -and $forbidden.newOrderSingle -eq $false -and $forbidden.cancelReplace -eq $false) 'Order path was introduced.'
Assert-True ($forbidden.scheduler -eq $false -and $forbidden.pollingLoop -eq $false -and $forbidden.replay -eq $false -and $forbidden.shadowReplay -eq $false) 'Scheduler/polling/replay path was introduced.'
Assert-True ($forbidden.executionReportFillOrderLifecycleParsing -eq $false) 'Execution/fill/order lifecycle parsing was introduced.'
Assert-True ($apiWorker.result -eq 'PASS' -and $apiWorker.apiWorkerFakeLmaxGatewayOnly -eq $true) 'API/Worker FakeLmaxGatewayOnly audit failed.'
Assert-True ($apiWorker.defaultGlobalRealAdapterEnabled -eq $false -and $apiWorker.noExternalDefaultModePreserved -eq $true) 'Live/default adapter safety regressed.'
Assert-True ($usdjpy.caveatPreserved -eq $true -and $usdjpy.securityId -eq '4004' -and $usdjpy.securityIdSource -eq '8') 'USDJPY caveat is missing or weakened.'
Assert-True ($shutdown.shutdownRevertCompleted -eq $true -and $shutdown.shutdownRevertStatus -eq 'Succeeded' -and $shutdown.retryLoopUsed -eq $false) 'Shutdown/revert evidence missing or retry loop used.'
Assert-True ($next.nextRecommendedPhase -eq 'Phase LMAX-R120 - MarketData Runtime Boundary Root-Cause Pack') 'Next phase recommendation missing or wrong.'
Assert-True ($gate.buildResult -like 'PASS*') 'Build evidence is missing.'
Assert-True ($gate.focusedTests -like 'PASS*' -and $gate.unitTests -like 'PASS*' -and $gate.integrationTests -like 'PASS*') 'Test evidence is missing.'

$reservationSource = Get-Content -LiteralPath (Join-Path $root 'src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxApprovedBoundedExecutableRetryPhaseReservations.cs') -Raw
Assert-True ($reservationSource -match 'WorkspaceApprovedRetryPhases' -and $reservationSource -match 'LMAX-R119') 'R119 workspace retry reservation is missing.'
Assert-True ($reservationSource -notmatch 'LMAX-R117"') 'R117 must not be reserved as an activation retry phase.'

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r119-*' -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$joined = $artifactText -join "`n"
$forbiddenPatterns = @(
    'password=',
    'password:',
    'username=',
    'SenderCompID=',
    'TargetCompID=',
    'session token',
    'BEGIN CERTIFICATE',
    'PRIVATE KEY',
    '35=A',
    '35=D',
    '35=F',
    '35=G',
    '35=8',
    '49=',
    '56=',
    '553=',
    '554='
)

foreach ($pattern in $forbiddenPatterns) {
    Assert-True ($joined.IndexOf($pattern, [StringComparison]::OrdinalIgnoreCase) -lt 0) "Sensitive or protocol material found in R119 artifacts: $pattern"
}

Write-Host 'LMAX_R119_VALIDATION_PASS'
