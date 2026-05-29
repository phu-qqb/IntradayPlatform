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
    'phase-lmax-r120-marketdata-boundary-root-cause-report.md',
    'phase-lmax-r120-marketdata-boundary-root-cause-summary.json',
    'phase-lmax-r120-r119-boundary-before-after-classification.json',
    'phase-lmax-r120-r119-marketdata-boundary-review.json',
    'phase-lmax-r120-marketdata-operation-not-configured-root-cause.json',
    'phase-lmax-r120-marketdata-request-builder-review.json',
    'phase-lmax-r120-marketdata-request-writer-review.json',
    'phase-lmax-r120-approved-instrument-marketdata-mapping-review.json',
    'phase-lmax-r120-usdjpy-caveat-preservation.json',
    'phase-lmax-r120-marketdata-readonly-safety-review.json',
    'phase-lmax-r120-marketdata-response-block-after-request-failure-review.json',
    'phase-lmax-r120-real-bounded-path-validation.json',
    'phase-lmax-r120-no-external-boundary-attempted.json',
    'phase-lmax-r120-forbidden-actions-audit.json',
    'phase-lmax-r120-api-worker-fake-gateway-audit.json',
    'phase-lmax-r120-no-scheduler-polling-service-audit.json',
    'phase-lmax-r120-credential-endpoint-tls-fix-sanitization-validation.json',
    'phase-lmax-r120-next-phase-recommendation.json',
    'phase-lmax-r120-gate-validation.json'
)

foreach ($artifact in $requiredArtifacts) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactRoot $artifact)) "Missing artifact: $artifact"
}

$r119 = Read-Json 'phase-lmax-r119-temporary-readonly-activation-retry-summary.json'
$r119MarketData = Read-Json 'phase-lmax-r119-marketdata-request-evidence.json'
$summary = Read-Json 'phase-lmax-r120-marketdata-boundary-root-cause-summary.json'
$beforeAfter = Read-Json 'phase-lmax-r120-r119-boundary-before-after-classification.json'
$review = Read-Json 'phase-lmax-r120-r119-marketdata-boundary-review.json'
$rootCause = Read-Json 'phase-lmax-r120-marketdata-operation-not-configured-root-cause.json'
$builder = Read-Json 'phase-lmax-r120-marketdata-request-builder-review.json'
$writer = Read-Json 'phase-lmax-r120-marketdata-request-writer-review.json'
$mapping = Read-Json 'phase-lmax-r120-approved-instrument-marketdata-mapping-review.json'
$usdjpy = Read-Json 'phase-lmax-r120-usdjpy-caveat-preservation.json'
$readonly = Read-Json 'phase-lmax-r120-marketdata-readonly-safety-review.json'
$responseBlock = Read-Json 'phase-lmax-r120-marketdata-response-block-after-request-failure-review.json'
$realBounded = Read-Json 'phase-lmax-r120-real-bounded-path-validation.json'
$noExternal = Read-Json 'phase-lmax-r120-no-external-boundary-attempted.json'
$forbidden = Read-Json 'phase-lmax-r120-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r120-api-worker-fake-gateway-audit.json'
$noScheduler = Read-Json 'phase-lmax-r120-no-scheduler-polling-service-audit.json'
$sanitization = Read-Json 'phase-lmax-r120-credential-endpoint-tls-fix-sanitization-validation.json'
$next = Read-Json 'phase-lmax-r120-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r120-gate-validation.json'

Assert-True ($r119.classification -eq 'LMAX_R119_FAIL_MARKETDATA_REQUEST_BOUNDARY') 'R119 MarketDataRequest boundary evidence is missing or mismatched.'
Assert-True ($r119.attemptCount -eq 1 -and $r119.externalActivationAttempted -eq $true) 'R119 exact single external attempt evidence is missing.'
Assert-True ($r119.fixAcknowledgementCategory -eq 'FixLogonAcknowledged' -and $r119.fixBoundaryResult -eq 'Succeeded') 'R119 FIX Logon acknowledgement success is not proven.'
Assert-True ($r119.marketDataRequestResult -eq 'FailedValidation' -and $r119.marketDataRequestCategory -eq 'MarketDataOperationNotConfigured') 'R119 MarketDataOperationNotConfigured evidence is missing.'
Assert-True ($r119MarketData.marketDataBoundaryReached -eq $true -and $r119MarketData.fixSessionSucceededBeforeMarketData -eq $true) 'R119 market-data boundary was not proven reached after FIX success.'
Assert-True ($r119MarketData.marketDataRequestSent -eq $false) 'R119 must not have sent a MarketDataRequest after the not-configured validation failure.'

Assert-True ($summary.classification -eq 'LMAX_R120_PASS_MARKETDATA_OPERATION_BINDING_MISSING_NO_EXTERNAL_ACTIVATION') 'Unexpected R120 classification.'
Assert-True ($summary.reviewOnly -eq $true -and $summary.externalActivationAttempted -eq $false) 'R120 must be review-only with no external activation.'
Assert-True ($summary.marketDataOperationNotConfiguredAcknowledged -eq $true) 'MarketDataOperationNotConfigured is not acknowledged.'
Assert-True ($summary.approvedManualRealBoundedMarketDataRequestBuilderExists -eq $false) 'Approved manual real-bounded MarketDataRequest builder should not be reported as present.'
Assert-True ($summary.approvedManualRealBoundedMarketDataRequestWriterExists -eq $false) 'Approved manual real-bounded MarketDataRequest writer should not be reported as present.'
Assert-True ($summary.marketDataRequestBlockedUnlessFixSucceeds -eq $true -and $summary.nonApprovedInstrumentsRejected -eq $true) 'Market-data safety gating is incomplete.'
Assert-True ($summary.credentialValuesReturned -eq $false -and $summary.sensitiveValuesPrintedStoredSerialized -eq $false) 'Secret sanitization summary failed.'

Assert-True ($beforeAfter.marketDataBoundary.category -eq 'MarketDataOperationNotConfigured') 'Before/after classification does not record MarketDataOperationNotConfigured.'
Assert-True ($beforeAfter.r120ExternalBoundaryAttempted -eq $false) 'R120 external boundary attempt recorded unexpectedly.'
Assert-True ($review.marketDataBoundaryReached -eq $true -and $review.marketDataRequestCategory -eq 'MarketDataOperationNotConfigured') 'MarketData boundary review is incomplete.'

Assert-True ($rootCause.classReturnedCategory -eq 'LmaxRealReadOnlyMarketDataFrameClient') 'Root cause does not identify the responsible class.'
Assert-True ($rootCause.methodReturnedCategory -eq 'DefaultNotConfigured') 'Root cause does not identify the responsible operation.'
Assert-True ($rootCause.clientOperationBindingPresent -eq $false -and $rootCause.defaultNotConfiguredOperationUsed -eq $true) 'Market-data operation binding root cause is not represented.'
Assert-True ($rootCause.providerReachedClient -eq $true -and $rootCause.fixSucceededBeforeProviderRequest -eq $true) 'Provider/client reachability after FIX success is not represented.'

Assert-True ($builder.approvedManualRealBoundedMarketDataRequestBuilderExists -eq $false -and $builder.approvedManualRealBoundedMarketDataRequestBuilderBound -eq $false) 'MarketDataRequest builder review is missing or incorrect.'
Assert-True ($builder.legacyOrSkeletonLogicApprovedForManualRealBoundedPath -eq $false) 'Legacy/skeleton logic must not be approved for the manual real-bounded path.'
Assert-True ($builder.orderMessagesSupported -eq $false -and $builder.newOrderSingleSupported -eq $false -and $builder.cancelReplaceSupported -eq $false) 'Builder review introduced order capability.'
Assert-True ($writer.approvedManualRealBoundedMarketDataRequestWriterExists -eq $false -and $writer.approvedManualRealBoundedMarketDataRequestWriterBound -eq $false) 'MarketDataRequest writer review is missing or incorrect.'
Assert-True ($writer.writerMustRemainReadOnly -eq $true -and $writer.orderCapableWriterPresentInApprovedPath -eq $false) 'Writer review is not read-only safe.'

$symbols = @($mapping.approvedInstruments | ForEach-Object { $_.symbol })
Assert-True (($symbols -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved instrument mapping differs from GBPUSD, EURGBP, AUDUSD, USDJPY.'
Assert-True ($mapping.approvedInstrumentMappingExists -eq $true -and $mapping.approvedInstrumentsExact -eq $true) 'Approved instrument mapping review is missing.'
Assert-True ($mapping.nonApprovedInstrumentsRejected -eq $true) 'Non-approved instrument rejection is not represented.'
Assert-True ($usdjpy.caveatPreserved -eq $true -and $usdjpy.securityId -eq '4004' -and $usdjpy.securityIdSource -eq '8') 'USDJPY caveat is missing or weakened.'

Assert-True ($readonly.marketDataRequestReadOnlyOnly -eq $true -and $readonly.marketDataRequestBlockedUnlessFixSucceeds -eq $true) 'MarketDataRequest read-only/FIX success gate failed.'
Assert-True ($readonly.ordersSupported -eq $false -and $readonly.tradingStateMutationSupported -eq $false -and $readonly.executionReportFillOrderLifecycleParsingSupported -eq $false) 'Order/trading capability was introduced.'
Assert-True ($responseBlock.marketDataResponseCaptureBlockedUntilRequestSucceeds -eq $true -and $responseBlock.marketDataResponseBoundary -eq 'NotAttempted') 'MarketDataResponse block after request failure review is missing.'

Assert-True ($realBounded.realBoundedExecutableReadOnlyAdapterReviewed -eq $true -and $realBounded.marketDataOperationBindingPresent -eq $false) 'Real-bounded path validation does not prove missing market-data operation binding.'
Assert-True ($realBounded.defaultNoExternalModePreserved -eq $true -and $realBounded.orderTradingPathReachable -eq $false) 'Default/no-order safety regressed.'
Assert-True ($noExternal.externalActivationAttempted -eq $false -and $noExternal.tcpSocketAttempted -eq $false -and $noExternal.tlsAttempted -eq $false -and $noExternal.fixLogonAttempted -eq $false -and $noExternal.marketDataRequestAttempted -eq $false) 'An external boundary was attempted during R120.'
Assert-True ($noExternal.boundaryStatuses.tcpSocket -eq 'NotAttempted' -and $noExternal.boundaryStatuses.tls -eq 'NotAttempted') 'R120 TCP/TLS boundaries must be NotAttempted.'

Assert-True ($forbidden.result -eq 'PASS') 'Forbidden action audit failed.'
Assert-True ($forbidden.orders -eq $false -and $forbidden.newOrderSingle -eq $false -and $forbidden.cancelReplace -eq $false) 'Order path introduced or touched.'
Assert-True ($forbidden.scheduler -eq $false -and $forbidden.pollingLoop -eq $false -and $forbidden.replay -eq $false -and $forbidden.shadowReplay -eq $false) 'Scheduler/polling/replay introduced.'
Assert-True ($apiWorker.result -eq 'PASS' -and $apiWorker.apiWorkerFakeLmaxGatewayOnly -eq $true) 'API/Worker FakeLmaxGatewayOnly audit failed.'
Assert-True ($apiWorker.defaultGlobalRealAdapterEnabled -eq $false -and $apiWorker.noExternalDefaultModePreserved -eq $true) 'API/Worker/default startup safety regressed.'
Assert-True ($noScheduler.result -eq 'PASS' -and $noScheduler.schedulerIntroduced -eq $false -and $noScheduler.pollingIntroduced -eq $false) 'Scheduler/polling/service audit failed.'
Assert-True ($sanitization.result -eq 'PASS' -and $sanitization.credentialValuesReturned -eq $false) 'Credential/FIX sanitization failed.'
Assert-True ($sanitization.rawCredentialsSerialized -eq $false -and $sanitization.rawFixMessagesSerialized -eq $false -and $sanitization.rawTlsMaterialSerialized -eq $false) 'Raw sensitive material serialized.'

Assert-True ($next.nextRecommendedPhase -eq 'Phase LMAX-R121 - Targeted MarketDataRequest Operation Binding Fix') 'Next-phase recommendation is absent or wrong.'
Assert-True ($next.r121ShouldRemainNoExternalUnlessExplicitlyApproved -eq $true) 'R121 no-external recommendation missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R120_VALIDATION_PASS') 'Gate validation result is missing.'
Assert-True ($gate.buildResult -like 'PASS*') 'Build evidence is missing.'
Assert-True ($gate.unitTests -like 'PASS*' -and $gate.integrationTests -like 'PASS*') 'Test evidence is missing.'

$clientSource = Get-Content -LiteralPath (Join-Path $root 'src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxRealReadOnlyProviderClients.cs') -Raw
Assert-True ($clientSource -match 'class LmaxRealReadOnlyMarketDataFrameClient') 'Market-data frame client source is missing.'
Assert-True ($clientSource -match 'DefaultNotConfigured' -and $clientSource -match 'MarketDataOperationNotConfigured') 'Default not-configured market-data operation is missing.'

$factorySource = Get-Content -LiteralPath (Join-Path $root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualExecutionSurfaceFactory.cs') -Raw
Assert-True ($factorySource -match 'new LmaxRealReadOnlyMarketDataFrameClient\(\)') 'Manual real-bounded factory no longer shows the missing market-data operation binding reviewed by R120.'
Assert-True ($factorySource -match 'ExternalMarketDataRequestExecutionApproved:\s*true') 'R119/R120 market-data provider approval path is not represented.'

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r120-*' -File |
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
    Assert-True ($joined.IndexOf($pattern, [StringComparison]::OrdinalIgnoreCase) -lt 0) "Sensitive or protocol material found in R120 artifacts: $pattern"
}

Write-Host 'LMAX_R120_VALIDATION_PASS'
