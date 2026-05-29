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
    'phase-lmax-r121-marketdata-request-operation-binding-fix-report.md',
    'phase-lmax-r121-marketdata-request-operation-binding-summary.json',
    'phase-lmax-r121-r120-root-cause-before-after-classification.json',
    'phase-lmax-r121-marketdata-request-builder-validation.json',
    'phase-lmax-r121-marketdata-request-writer-binding-validation.json',
    'phase-lmax-r121-marketdata-fix-session-success-gate-validation.json',
    'phase-lmax-r121-approved-instrument-marketdata-scope-validation.json',
    'phase-lmax-r121-usdjpy-caveat-preservation.json',
    'phase-lmax-r121-non-approved-instrument-rejection-validation.json',
    'phase-lmax-r121-marketdata-readonly-safety-validation.json',
    'phase-lmax-r121-order-message-exclusion-validation.json',
    'phase-lmax-r121-raw-fix-sanitization-validation.json',
    'phase-lmax-r121-marketdata-response-block-until-request-success-validation.json',
    'phase-lmax-r121-real-bounded-path-validation.json',
    'phase-lmax-r121-no-external-boundary-attempted.json',
    'phase-lmax-r121-forbidden-actions-audit.json',
    'phase-lmax-r121-api-worker-fake-gateway-audit.json',
    'phase-lmax-r121-no-scheduler-polling-service-audit.json',
    'phase-lmax-r121-credential-endpoint-tls-fix-sanitization-validation.json',
    'phase-lmax-r121-next-phase-recommendation.json',
    'phase-lmax-r121-gate-validation.json'
)

foreach ($artifact in $requiredArtifacts) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactRoot $artifact)) "Missing artifact: $artifact"
}

$r120 = Read-Json 'phase-lmax-r120-marketdata-boundary-root-cause-summary.json'
$summary = Read-Json 'phase-lmax-r121-marketdata-request-operation-binding-summary.json'
$beforeAfter = Read-Json 'phase-lmax-r121-r120-root-cause-before-after-classification.json'
$builder = Read-Json 'phase-lmax-r121-marketdata-request-builder-validation.json'
$writer = Read-Json 'phase-lmax-r121-marketdata-request-writer-binding-validation.json'
$fixGate = Read-Json 'phase-lmax-r121-marketdata-fix-session-success-gate-validation.json'
$scope = Read-Json 'phase-lmax-r121-approved-instrument-marketdata-scope-validation.json'
$usdjpy = Read-Json 'phase-lmax-r121-usdjpy-caveat-preservation.json'
$nonApproved = Read-Json 'phase-lmax-r121-non-approved-instrument-rejection-validation.json'
$readonly = Read-Json 'phase-lmax-r121-marketdata-readonly-safety-validation.json'
$orders = Read-Json 'phase-lmax-r121-order-message-exclusion-validation.json'
$rawFix = Read-Json 'phase-lmax-r121-raw-fix-sanitization-validation.json'
$responseBlock = Read-Json 'phase-lmax-r121-marketdata-response-block-until-request-success-validation.json'
$realBounded = Read-Json 'phase-lmax-r121-real-bounded-path-validation.json'
$noExternal = Read-Json 'phase-lmax-r121-no-external-boundary-attempted.json'
$forbidden = Read-Json 'phase-lmax-r121-forbidden-actions-audit.json'
$apiWorker = Read-Json 'phase-lmax-r121-api-worker-fake-gateway-audit.json'
$noScheduler = Read-Json 'phase-lmax-r121-no-scheduler-polling-service-audit.json'
$sanitization = Read-Json 'phase-lmax-r121-credential-endpoint-tls-fix-sanitization-validation.json'
$next = Read-Json 'phase-lmax-r121-next-phase-recommendation.json'
$gate = Read-Json 'phase-lmax-r121-gate-validation.json'

Assert-True ($r120.classification -eq 'LMAX_R120_PASS_MARKETDATA_OPERATION_BINDING_MISSING_NO_EXTERNAL_ACTIVATION') 'R120 success evidence is missing or mismatched.'
Assert-True ($r120.marketDataOperationNotConfiguredAcknowledged -eq $true) 'R120 MarketDataOperationNotConfigured root cause is missing.'
Assert-True ($r120.approvedInstrumentMappingExists -eq $true -and $r120.usdJpyCaveatPreserved -eq $true) 'R120 instrument/caveat evidence is missing.'

Assert-True ($summary.classification -eq 'LMAX_R121_PASS_MARKETDATA_REQUEST_OPERATION_BINDING_READY_NO_EXTERNAL_ACTIVATION') 'Unexpected R121 classification.'
Assert-True ($summary.marketDataOperationNotConfiguredClearedForFutureApprovedManualRealBoundedPath -eq $true) 'MarketDataOperationNotConfigured remains true for future approved manual real-bounded path.'
Assert-True ($summary.approvedMarketDataRequestBuilderReady -eq $true) 'Approved MarketDataRequest builder is not provable.'
Assert-True ($summary.approvedMarketDataRequestWriterOperationReady -eq $true) 'Approved MarketDataRequest writer/operation is not provable.'
Assert-True ($summary.marketDataRequestGatedOnFixSessionSuccess -eq $true) 'MarketDataRequest can be attempted without FIX session success.'
Assert-True ($summary.nonApprovedInstrumentsRejected -eq $true) 'Non-approved instruments are allowed.'
Assert-True (($summary.approvedInstruments -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved instrument scope differs from GBPUSD, EURGBP, AUDUSD, USDJPY.'
Assert-True ($summary.usdJpySecurityIdPreserved -eq $true -and $summary.usdJpySecurityIdSourcePreserved -eq $true -and $summary.usdJpyCaveatPreserved -eq $true) 'USDJPY caveat or mapping weakened.'
Assert-True ($summary.orderTradingPathIntroduced -eq $false -and $summary.marketDataRequestReadOnly -eq $true) 'MarketDataRequest supports trading or orders.'
Assert-True ($summary.rawFixMessagesPrintedStoredSerialized -eq $false -and $summary.credentialValuesReturned -eq $false) 'Raw FIX or credential risk detected.'
Assert-True ($summary.externalActivationAttempted -eq $false) 'R121 attempted an external activation.'

Assert-True ($beforeAfter.before.marketDataOperationNotConfigured -eq $true -and $beforeAfter.after.marketDataOperationNotConfiguredClearedForFutureApprovedManualRealBoundedPath -eq $true) 'Before/after root cause classification is incomplete.'
Assert-True ($builder.approvedMarketDataRequestBuilderReady -eq $true -and $builder.approvedInstrumentScopeExact -eq $true) 'MarketDataRequest builder validation is incomplete.'
Assert-True ($builder.requestMessageCategoryPresent -eq $true -and $builder.mdReqIdPresent -eq $true -and $builder.relatedSymbolsPresent -eq $true) 'Required MarketDataRequest categories are not represented.'
Assert-True ($builder.securityIdPresentForAllApprovedInstruments -eq $true -and $builder.securityIdSourcePresentForAllApprovedInstruments -eq $true) 'Approved instrument market-data mapping is incomplete.'
Assert-True ($builder.rawFixSerialized -eq $false -and $builder.rawSessionIdentifiersSerialized -eq $false -and $builder.credentialValuesReturned -eq $false) 'Builder serialized raw sensitive material.'
Assert-True ($builder.unsupportedLmaxTagsInvented -eq $false) 'Unsupported LMAX tags were invented.'

Assert-True ($writer.approvedMarketDataRequestWriterReady -eq $true -and $writer.approvedMarketDataRequestOperationReady -eq $true) 'MarketDataRequest writer/operation binding is missing.'
Assert-True ($writer.factoryBindsSocketConnectorRequestMarketData -eq $true -and $writer.defaultNotConfiguredClientCleared -eq $true) 'Manual real-bounded factory binding is missing.'
Assert-True ($writer.liveMarketDataRequestSentDuringR121 -eq $false) 'A live MarketDataRequest was sent during R121.'
Assert-True ($fixGate.marketDataRequestGatedOnFixSessionSuccess -eq $true -and $fixGate.requestBlockedWithoutFixSessionSuccess -eq $true) 'FIX success gate validation missing.'
Assert-True ($fixGate.blockedCategoryWithoutFixSuccess -eq 'FixSessionAcknowledgementRequired') 'Wrong blocked category for missing FIX success.'

Assert-True ($scope.approvedInstrumentScopeExact -eq $true -and ($scope.approvedInstruments -join ',') -eq 'GBPUSD,EURGBP,AUDUSD,USDJPY') 'Approved instrument scope validation failed.'
Assert-True ($scope.nonApprovedInstrumentAllowed -eq $false) 'Non-approved instrument was allowed.'
Assert-True ($usdjpy.caveatPreserved -eq $true -and $usdjpy.securityId -eq '4004' -and $usdjpy.securityIdSource -eq '8') 'USDJPY caveat is missing or weakened.'
Assert-True ($nonApproved.nonApprovedInstrumentsRejected -eq $true -and $nonApproved.writerNotCalledForNonApprovedInstrument -eq $true) 'Non-approved instrument rejection validation failed.'

Assert-True ($readonly.marketDataRequestReadOnly -eq $true -and $readonly.ordersSupported -eq $false -and $readonly.tradingMutationSupported -eq $false) 'MarketData readonly safety failed.'
Assert-True ($orders.result -eq 'PASS' -and $orders.newOrderSingleSupported -eq $false -and $orders.cancelReplaceSupported -eq $false) 'Order message exclusion failed.'
Assert-True ($orders.executionReportFillOrderLifecycleParsingSupported -eq $false) 'Execution report/fill/order lifecycle parsing was introduced.'
Assert-True ($rawFix.result -eq 'PASS' -and $rawFix.rawFixMessagesSerialized -eq $false -and $rawFix.rawCredentialsSerialized -eq $false) 'Raw FIX/credential serialization risk.'
Assert-True ($responseBlock.marketDataResponseBlockedUntilRequestSuccess -eq $true -and $responseBlock.marketDataResponseObservationAttemptedInR121 -eq $false) 'MarketData response block validation failed.'

Assert-True ($realBounded.marketDataClientConstructedWithExplicitOperation -eq $true -and $realBounded.marketDataOperationBindingPresent -eq $true) 'Real-bounded path validation failed.'
Assert-True ($realBounded.operationBinding -eq 'socketConnector.RequestMarketData') 'Unexpected market-data operation binding.'
Assert-True ($realBounded.defaultNoExternalModePreserved -eq $true -and $realBounded.orderTradingPathReachable -eq $false) 'Default/no-order safety regressed.'
Assert-True ($noExternal.externalActivationAttempted -eq $false -and $noExternal.tcpSocketAttempted -eq $false -and $noExternal.tlsAttempted -eq $false -and $noExternal.fixLogonAttempted -eq $false -and $noExternal.liveMarketDataRequestAttempted -eq $false) 'External boundary attempted during R121.'
Assert-True ($noExternal.boundaryStatuses.tcpSocket -eq 'NotAttempted' -and $noExternal.boundaryStatuses.marketDataRequest -eq 'ValidationOnly') 'R121 boundary status mismatch.'

Assert-True ($forbidden.result -eq 'PASS' -and $forbidden.externalBoundaryAttempted -eq $false) 'Forbidden action audit failed.'
Assert-True ($apiWorker.result -eq 'PASS' -and $apiWorker.apiWorkerFakeLmaxGatewayOnly -eq $true) 'API/Worker FakeLmaxGatewayOnly audit failed.'
Assert-True ($apiWorker.manualCliReachableFromApiWorkerDefaultStartup -eq $false -and $apiWorker.noExternalDefaultModePreserved -eq $true) 'API/Worker/default startup safety regressed.'
Assert-True ($noScheduler.result -eq 'PASS' -and $noScheduler.schedulerIntroduced -eq $false -and $noScheduler.pollingIntroduced -eq $false) 'Scheduler/polling/service introduced.'
Assert-True ($sanitization.result -eq 'PASS' -and $sanitization.credentialValuesReturned -eq $false) 'Credential/FIX sanitization failed.'
Assert-True ($next.nextRecommendedPhase -eq 'Phase LMAX-R123 - Operator-Approved Single Temporary QQ Workspace Demo Read-Only Activation Retry After MarketDataRequest Operation Binding Fix') 'Next phase recommendation missing.'
Assert-True ($next.useR123NotR122BecauseActivationRetriesMustBeOdd -eq $true -and $next.r123RequiresFreshExplicitOperatorApproval -eq $true) 'R123 odd-phase/fresh approval recommendation missing.'
Assert-True ($gate.validatorResult -eq 'LMAX_R121_VALIDATION_PASS') 'Gate validation result is missing.'
Assert-True ($gate.buildResult -like 'PASS*' -and $gate.focusedTests -like 'PASS*' -and $gate.unitTests -like 'PASS*' -and $gate.integrationTests -like 'PASS*') 'Build/test evidence is missing.'

$operationSource = Get-Content -LiteralPath (Join-Path $root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualMarketDataRequestOperation.cs') -Raw
Assert-True ($operationSource -match 'class LmaxReadOnlyActivationManualMarketDataRequestOperation') 'Manual market-data request operation source is missing.'
Assert-True ($operationSource -match 'class LmaxReadOnlyActivationManualMarketDataRequestBuilder') 'Manual MarketDataRequest builder source is missing.'
Assert-True ($operationSource -match 'class LmaxReadOnlyActivationManualMarketDataRequestWriter') 'Manual MarketDataRequest writer source is missing.'
Assert-True ($operationSource -match 'FixSessionAcknowledgementRequired') 'FIX session success gate is missing.'
Assert-True ($operationSource -notmatch 'NewOrderSingle' -or $operationSource -match 'NewOrderSingleSupported:\s*false') 'NewOrderSingle support introduced.'

$factorySource = Get-Content -LiteralPath (Join-Path $root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualExecutionSurfaceFactory.cs') -Raw
Assert-True ($factorySource -match 'socketConnector\.RequestMarketData') 'Manual real-bounded factory is not bound to socketConnector.RequestMarketData.'
Assert-True ($factorySource -notmatch 'new LmaxRealReadOnlyMarketDataFrameClient\(\)') 'MarketDataOperationNotConfigured default client construction remains in the manual real-bounded path.'

$connectorSource = Get-Content -LiteralPath (Join-Path $root 'tools/QQ.Production.Intraday.Tools.LmaxReadOnlyActivation/LmaxReadOnlyActivationManualTcpSocketConnector.cs') -Raw
Assert-True ($connectorSource -match 'fixSessionOpened') 'Connector does not track FIX session success.'
Assert-True ($connectorSource -match 'RequestMarketData') 'Connector market-data request operation is missing.'

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -Filter 'phase-lmax-r121-*' -File |
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
    Assert-True ($joined.IndexOf($pattern, [StringComparison]::OrdinalIgnoreCase) -lt 0) "Sensitive or protocol material found in R121 artifacts: $pattern"
}

Write-Host 'LMAX_R121_VALIDATION_PASS'
