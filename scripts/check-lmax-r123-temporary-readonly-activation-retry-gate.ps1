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
    'phase-lmax-r123-temporary-readonly-activation-retry-report.md',
    'phase-lmax-r123-temporary-readonly-activation-retry-summary.json',
    'phase-lmax-r123-operator-approval-note.md',
    'phase-lmax-r123-preflight-result.json',
    'phase-lmax-r123-demo-endpoint-binding-evidence.json',
    'phase-lmax-r123-socket-connector-evidence.json',
    'phase-lmax-r123-tls-boundary-evidence.json',
    'phase-lmax-r123-fix-session-boundary-evidence.json',
    'phase-lmax-r123-marketdata-request-evidence.json',
    'phase-lmax-r123-marketdata-response-evidence.json',
    'phase-lmax-r123-approved-instrument-marketdata-evidence.json',
    'phase-lmax-r123-usdjpy-caveat-preservation.json',
    'phase-lmax-r123-operational-invocation-trace.json',
    'phase-lmax-r123-boundary-evidence.json',
    'phase-lmax-r123-marketdata-sanitized-result.json',
    'phase-lmax-r123-forbidden-actions-audit.json',
    'phase-lmax-r123-api-worker-fake-gateway-audit.json',
    'phase-lmax-r123-shutdown-revert-evidence.json',
    'phase-lmax-r123-next-phase-recommendation.json',
    'phase-lmax-r123-gate-validation.json'
)

foreach ($artifact in $requiredArtifacts) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactRoot $artifact)) "Missing artifact: $artifact"
}

$r121 = Read-Json 'phase-lmax-r121-marketdata-request-operation-binding-summary.json'
$summary = Read-Json 'phase-lmax-r123-temporary-readonly-activation-retry-summary.json'
$preflight = Read-Json 'phase-lmax-r123-preflight-result.json'
$endpoint = Read-Json 'phase-lmax-r123-demo-endpoint-binding-evidence.json'
$socket = Read-Json 'phase-lmax-r123-socket-connector-evidence.json'
$tls = Read-Json 'phase-lmax-r123-tls-boundary-evidence.json'
$fix = Read-Json 'phase-lmax-r123-fix-session-boundary-evidence.json'
$marketDataRequest = Read-Json 'phase-lmax-r123-marketdata-request-evidence.json'
$marketDataResponse = Read-Json 'phase-lmax-r123-marketdata-response-evidence.json'
$instruments = Read-Json 'phase-lmax-r123-approved-instrument-marketdata-evidence.json'
$usdjpy = Read-Json 'phase-lmax-r123-usdjpy-caveat-preservation.json'
$trace = Read-Json 'phase-lmax-r123-operational-invocation-trace.json'
$boundary = Read-Json 'phase-lmax-r123-boundary-evidence.json'
$marketDataResult = Read-Json 'phase-lmax-r123-marketdata-sanitized-result.json'
$forbidden = Read-Json 'phase-lmax-r123-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r123-api-worker-fake-gateway-audit.json'
$shutdown = Read-Json 'phase-lmax-r123-shutdown-revert-evidence.json'
$next = Read-Json 'phase-lmax-r123-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r123-gate-validation.json'

Assert-True ($r121.classification -eq 'LMAX_R121_PASS_MARKETDATA_REQUEST_OPERATION_BINDING_READY_NO_EXTERNAL_ACTIVATION') 'R121 success evidence is missing or mismatched.'
Assert-True ($r121.marketDataOperationNotConfiguredClearedForFutureApprovedManualRealBoundedPath -eq $true) 'R121 market-data operation binding evidence missing.'
Assert-True ($summary.classification -eq 'LMAX_R123_FAIL_MARKETDATA_RESPONSE_BOUNDARY') 'Unexpected R123 classification.'
Assert-True ($summary.externalActivationAttempted -eq $true -and $summary.attemptCount -eq 1) 'R123 must record exactly one approved external activation attempt.'
Assert-True ($summary.retryPhaseReservationPassed -eq $true) 'R123 retry phase reservation did not pass.'
Assert-True ($summary.toolUsed -eq 'QQ.Production.Intraday.Tools.LmaxReadOnlyActivation') 'Unexpected R123 tool.'
Assert-True ($summary.adapterMode -eq 'real-bounded-executable-readonly') 'Unexpected R123 adapter mode.'

Assert-True ($preflight.operatorApprovalMatched -eq $true -and $preflight.r121SuccessEvidencePresent -eq $true) 'Pre-external approval or R121 evidence missing.'
Assert-True ($preflight.executeOnceInvoked -eq $true -and $preflight.singleAttemptOnly -eq $true) 'Execute-once/single-attempt proof missing.'
Assert-True ($preflight.noApiWorkerStartup -eq $true -and $preflight.noServiceSchedulerPolling -eq $true -and $preflight.noOrderTradingPath -eq $true -and $preflight.noCredentialOutput -eq $true) 'Required safety flags missing.'

Assert-True ($endpoint.endpointMode -eq 'Demo' -and $endpoint.endpointPresent -eq $true -and $endpoint.hostPresent -eq $true -and $endpoint.portPresent -eq $true) 'Demo endpoint evidence missing.'
Assert-True ($endpoint.hostConcreteBinding -eq $true -and $endpoint.hostWasPlaceholder -eq $false -and $endpoint.portConcreteBinding -eq $true) 'Concrete Demo endpoint evidence missing.'
Assert-True ($endpoint.productionExcluded -eq $true -and $endpoint.endpointApproved -eq $true -and $endpoint.rawEndpointSerialized -eq $false) 'Endpoint approval/sanitization failed.'

Assert-True ($socket.configuredSocketConnectorUsed -eq $true -and $socket.connectReached -eq $true) 'Socket connector evidence missing.'
Assert-True ($socket.tcpConnectionAttempted -eq $true -and $socket.tcpSocketSucceeded -eq $true -and $socket.boundaryStatus -eq 'Succeeded') 'TCP/socket success not proven.'
Assert-True ($tls.tlsAttempted -eq $true -and $tls.authenticateTlsReached -eq $true -and $tls.tlsSucceeded -eq $true -and $tls.tlsResultCategory -eq 'Succeeded') 'TLS success not proven.'
Assert-True ($tls.tlsRawMaterialSerialized -eq $false) 'Raw TLS material serialized.'

Assert-True ($fix.fixLogonSessionAttempted -eq $true -and $fix.socketConnectorOpenFixSessionReached -eq $true) 'FIX logon/session attempt not proven.'
Assert-True ($fix.fixAcknowledgementReaderParserClassifierUsed -eq $true -and $fix.fixAcknowledgementCategory -eq 'FixLogonAcknowledged') 'FIX acknowledgement success not proven.'
Assert-True ($fix.fixLogonSessionSucceeded -eq $true -and $fix.rawFixFrameOrMessageSerialized -eq $false -and $fix.orderCapableFixFrameOrParserPathAbsent -eq $true) 'FIX session safety evidence failed.'

Assert-True ($marketDataRequest.fixSessionSucceededBeforeMarketData -eq $true) 'MarketDataRequest was allowed without FIX success.'
Assert-True ($marketDataRequest.marketDataRequestOperationBindingReady -eq $true -and $marketDataRequest.marketDataRequestBuilderReady -eq $true -and $marketDataRequest.marketDataRequestWriterReady -eq $true) 'MarketDataRequest binding regression.'
Assert-True ($marketDataRequest.marketDataRequestAttempted -eq $true -and $marketDataRequest.marketDataRequestResult -eq 'Succeeded') 'MarketDataRequest boundary was not reached successfully.'
Assert-True ($marketDataRequest.approvedInstrumentsOnly -eq $true -and $marketDataRequest.nonApprovedInstrumentsAbsent -eq $true) 'Non-approved market-data instrument was allowed.'
Assert-True ($marketDataRequest.rawFixSerialized -eq $false -and $marketDataRequest.rawSessionValuesSerialized -eq $false) 'Raw market-data FIX/session material serialized.'

Assert-True ($marketDataResponse.marketDataResponseEntriesObserved -eq $false) 'R123 unexpectedly claims market-data entries were observed.'
Assert-True ($marketDataResponse.marketDataResponseResult -eq 'FailedValidation' -and $marketDataResponse.marketDataResponseCategory -eq 'MarketDataResponseEntriesNotObserved') 'MarketDataResponse boundary classification missing.'
Assert-True ($marketDataResponse.rawMarketDataPayloadSerialized -eq $false) 'Raw market-data payload serialized.'

Assert-True (($instruments.approvedInstrumentsRequested -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved instruments requested differ from expected scope.'
Assert-True ($instruments.approvedInstrumentScopeExact -eq $true -and $instruments.nonApprovedInstrumentsAbsent -eq $true) 'Approved instrument scope failed.'
Assert-True ($instruments.usdJpySecurityIdPreserved -eq $true -and $instruments.usdJpySecurityIdSourcePreserved -eq $true -and $instruments.usdJpyCaveatPreserved -eq $true) 'USDJPY mapping/caveat weakened.'
Assert-True ($usdjpy.caveatPreserved -eq $true -and $usdjpy.securityId -eq '4004' -and $usdjpy.securityIdSource -eq '8') 'USDJPY caveat artifact missing or weakened.'

Assert-True ($trace.attemptCount -eq 1 -and $trace.retryLoopUsed -eq $false -and $trace.pollingLoopUsed -eq $false) 'Operational invocation trace violates single-attempt/no-polling rule.'
Assert-True ($boundary.'Credential/config' -eq 'Succeeded' -and $boundary.'TCP/socket' -eq 'Succeeded' -and $boundary.TLS -eq 'Succeeded' -and $boundary.'FIX logon/session' -eq 'Succeeded') 'Successful upstream boundary evidence missing.'
Assert-True ($boundary.MarketDataRequest -eq 'Succeeded' -and $boundary.'MarketDataResponse/entries' -eq 'FailedValidation' -and $boundary.'Shutdown/revert' -eq 'Succeeded') 'Market-data or shutdown boundary evidence mismatch.'
Assert-True ($marketDataResult.marketDataResponseEntriesObserved -eq $false -and $marketDataResult.sanitizedEntryCount -eq 0) 'Market-data sanitized result mismatch.'

Assert-True ($summary.credentialValuesReturned -eq $false -and $summary.sensitiveValuesPrintedStoredSerialized -eq $false) 'Credential values returned or sensitive output serialized.'
Assert-True ($forbidden.result -eq 'PASS' -and $forbidden.orders -eq $false -and $forbidden.newOrderSingle -eq $false -and $forbidden.cancelReplace -eq $false) 'Forbidden order action introduced.'
Assert-True ($forbidden.scheduler -eq $false -and $forbidden.pollingLoop -eq $false -and $forbidden.replay -eq $false -and $forbidden.shadowReplay -eq $false) 'Forbidden scheduler/polling/replay action introduced.'
Assert-True ($apiWorker.result -eq 'PASS' -and $apiWorker.apiWorkerFakeLmaxGatewayOnly -eq $true) 'API/Worker FakeLmaxGatewayOnly audit failed.'
Assert-True ($shutdown.shutdownRevertCompleted -eq $true -and $shutdown.shutdownRevertStatus -eq 'Succeeded') 'Shutdown/revert incomplete.'
Assert-True ($next.nextRecommendedPhase -eq 'Phase LMAX-R124 - MarketDataResponse Boundary Root-Cause Pack') 'Next phase recommendation absent or wrong.'
Assert-True ($gate.validatorResult -eq 'LMAX_R123_VALIDATION_PASS') 'Gate validation result missing.'
Assert-True ($gate.buildResult -like 'PASS*' -and $gate.focusedTests -like 'PASS*' -and $gate.unitTests -like 'PASS*' -and $gate.integrationTests -like 'PASS*') 'Build/test evidence missing.'

$reservationSource = Get-Content -LiteralPath (Join-Path $root 'src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxApprovedBoundedExecutableRetryPhaseReservations.cs') -Raw
Assert-True ($reservationSource -match 'LMAX-R123') 'R123 workspace retry reservation missing.'

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r123-*' -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$joined = $artifactText -join "`n"
$forbiddenPatterns = @(
    'password=',
    'username=',
    'SenderCompID=',
    'TargetCompID=',
    'BEGIN CERTIFICATE',
    'PRIVATE KEY',
    '35=',
    '49=',
    '56=',
    '553=',
    '554='
)

foreach ($pattern in $forbiddenPatterns) {
    Assert-True ($joined.IndexOf($pattern, [StringComparison]::OrdinalIgnoreCase) -lt 0) "Sensitive or protocol material found in R123 artifacts: $pattern"
}

Write-Host 'LMAX_R123_VALIDATION_PASS'
