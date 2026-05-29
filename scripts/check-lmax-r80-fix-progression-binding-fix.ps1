$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root "artifacts\readiness\lmax-runtime-enablement"
$factoryPath = Join-Path $root "tools\QQ.Production.Intraday.Tools.LmaxReadOnlyActivation\LmaxReadOnlyActivationManualExecutionSurfaceFactory.cs"
$connectorPath = Join-Path $root "tools\QQ.Production.Intraday.Tools.LmaxReadOnlyActivation\LmaxReadOnlyActivationManualTcpSocketConnector.cs"
$transportPath = Join-Path $root "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxRealReadOnlyMarketDataTransport.cs"
$apiProgramPath = Join-Path $root "src\QQ.Production.Intraday.Api\Program.cs"
$workerProgramPath = Join-Path $root "src\QQ.Production.Intraday.Worker\Program.cs"

function Read-Json($path) {
    if (-not (Test-Path $path)) {
        throw "Missing required artifact: $path"
    }

    Get-Content $path -Raw | ConvertFrom-Json
}

function Assert-True($condition, $message) {
    if (-not $condition) {
        throw $message
    }
}

function Assert-False($condition, $message) {
    if ($condition) {
        throw $message
    }
}

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r80-fix-progression-binding-summary.json")
$beforeAfter = Read-Json (Join-Path $artifactRoot "phase-lmax-r80-r79-root-cause-before-after-classification.json")
$fixBinding = Read-Json (Join-Path $artifactRoot "phase-lmax-r80-fix-session-operation-binding-validation.json")
$continuation = Read-Json (Join-Path $artifactRoot "phase-lmax-r80-tls-to-fix-continuation-binding-validation.json")
$tlsGate = Read-Json (Join-Path $artifactRoot "phase-lmax-r80-tls-success-gate-preservation-validation.json")
$fixSafety = Read-Json (Join-Path $artifactRoot "phase-lmax-r80-fix-readonly-session-safety-validation.json")
$realPath = Read-Json (Join-Path $artifactRoot "phase-lmax-r80-real-bounded-path-validation.json")
$noExternal = Read-Json (Join-Path $artifactRoot "phase-lmax-r80-no-external-boundary-attempted.json")
$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r80-forbidden-actions-audit.json")
$apiWorker = Read-Json (Join-Path $artifactRoot "phase-lmax-r80-api-worker-fake-gateway-audit.json")
$scheduler = Read-Json (Join-Path $artifactRoot "phase-lmax-r80-no-scheduler-polling-service-audit.json")
$sanitize = Read-Json (Join-Path $artifactRoot "phase-lmax-r80-credential-endpoint-tls-fix-sanitization-validation.json")
$usdJpy = Read-Json (Join-Path $artifactRoot "phase-lmax-r80-usdjpy-caveat-preservation.json")
$next = Read-Json (Join-Path $artifactRoot "phase-lmax-r80-next-phase-recommendation.json")
$gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r80-gate-validation.json")

$allowedClassifications = @(
    "LMAX_R80_PASS_FIX_PROGRESSION_BINDING_READY_NO_EXTERNAL_ACTIVATION",
    "LMAX_R80_PASS_FIX_SESSION_OPERATION_BOUND_TLS_SUCCESS_CLASSIFICATION_STILL_REQUIRED_NO_EXTERNAL_ACTIVATION",
    "LMAX_R80_FAIL_FIX_SESSION_OPERATION_BINDING_NOT_PROVABLE",
    "LMAX_R80_FAIL_TLS_TO_FIX_CONTINUATION_STILL_MISSING",
    "LMAX_R80_FAIL_FIX_ATTEMPT_ALLOWED_WITHOUT_TLS_SUCCESS",
    "LMAX_R80_FAIL_TLS_SUCCESS_GATE_WEAKENED",
    "LMAX_R80_FAIL_FIX_DEFAULT_GLOBAL_RISK",
    "LMAX_R80_FAIL_EXTERNAL_BOUNDARY_ATTEMPTED",
    "LMAX_R80_FAIL_FORBIDDEN_ACTION_INTRODUCED",
    "LMAX_R80_FAIL_CREDENTIAL_VALUES_RETURNED_OR_SECRET_RISK",
    "LMAX_R80_FAIL_USDJYP_CAVEAT_WEAKENED",
    "LMAX_R80_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R80_FAIL_BUILD_OR_TESTS"
)

Assert-True ($allowedClassifications -contains $summary.classification) "R80 classification is absent or not allowed."
Assert-True ($summary.classification -eq "LMAX_R80_PASS_FIX_PROGRESSION_BINDING_READY_NO_EXTERNAL_ACTIVATION") "R80 final classification mismatch."

Assert-True ($beforeAfter.before.classification -eq "LMAX_R79_PASS_TLS_TO_FIX_PROGRESSION_ROOT_CAUSE_IDENTIFIED_NO_EXTERNAL_ACTIVATION") "R79 root-cause classification missing."
Assert-False $beforeAfter.before.concreteFixSessionOperationBound "R79 before-state should show missing FIX operation binding."
Assert-True ($beforeAfter.after.concreteFixSessionOperationBound -eq $true) "R80 after-state does not prove FIX operation binding."
Assert-True ($beforeAfter.after.tlsToFixContinuationBound -eq $true) "R80 after-state does not prove TLS-to-FIX continuation."

Assert-True ($summary.concreteFixSessionOperationBindingReady -eq $true) "Concrete FIX session operation binding is not ready."
Assert-True ($summary.lmaxRealReadOnlyFixFrameClientConstructedWithConcreteFixSessionOperation -eq $true) "FIX client construction with concrete operation is not proven."
Assert-True ($summary.tlsToFixContinuationReady -eq $true) "TLS-to-FIX continuation remains missing."
Assert-True ($summary.fixRequiresTlsSucceeded -eq $true) "FIX no longer requires TLS Succeeded."
Assert-True ($summary.tlsAttemptedOnlyInsufficientForFix -eq $true) "TLS attempted-only is not represented as insufficient for FIX."
Assert-False $summary.tlsSuccessFaked "TLS success was faked."
Assert-False $summary.transportSafetyWeakened "Transport safety was weakened."
Assert-False $summary.externalActivationAttempted "External activation was attempted during R80."
Assert-False $summary.credentialValuesReturned "credentialValuesReturned is not false."

Assert-True ($fixBinding.concreteFixSessionOperationBindingProvable -eq $true) "FIX session operation binding validation failed."
Assert-True ($fixBinding.fixClientExecutionDependencyMissingClearedForNextRetry -eq $true) "FixClientExecutionDependencyMissing remains true."
Assert-True ($fixBinding.fixSessionOperationNotConfiguredClearedForNextRetry -eq $true) "FixSessionOperationNotConfigured remains true."
Assert-True ($fixBinding.operation -eq "LmaxReadOnlyActivationManualTcpSocketConnector.OpenFixSession") "Unexpected FIX operation binding."
Assert-True ($fixBinding.operationRequiresApprovedManualRetryScope -eq $true) "FIX operation does not require approved manual retry scope."
Assert-True ($fixBinding.operationRequiresDemoReadOnlyFixApproval -eq $true) "FIX operation does not require Demo/read-only approval."
Assert-True ($fixBinding.operationDoesNotSubmitOrders -eq $true) "FIX operation can submit orders."

Assert-True ($continuation.tlsToFixContinuationReady -eq $true) "TLS-to-FIX continuation validation failed."
Assert-True ($continuation.fixContinuationRequiresTlsAuthenticated -eq $true) "FIX continuation does not require authenticated TLS."
Assert-True ($continuation.tlsBoundaryNotSucceededBlocksFix -eq $true) "TLS not-succeeded does not block FIX."

Assert-True ($tlsGate.fixStillRequiresTlsSucceeded -eq $true) "FIX still-requires-TLS-Succeeded evidence missing."
Assert-True ($tlsGate.tlsAttemptedOnlyDoesNotTriggerFix -eq $true) "TLS attempted-only can trigger FIX."
Assert-False $tlsGate.tlsSuccessFaked "TLS success was faked in gate validation."
Assert-False $tlsGate.runAsyncSafetyWeakened "RunAsync safety was weakened."

Assert-True ($fixSafety.fixOperationReadOnlySessionOnly -eq $true) "FIX operation is not read-only session-only."
Assert-False $fixSafety.newOrderSinglePossible "Order submission path introduced."
Assert-False $fixSafety.tradingStateMutationPossible "Trading mutation path introduced."
Assert-False $fixSafety.marketDataRequestSentDuringR80 "MarketDataRequest attempted during R80."
Assert-False $fixSafety.rawFixMessagesPrintedStoredSerialized "Raw FIX messages were printed/stored/serialized."

Assert-True ($realPath.manualRealBoundedPathOnly -eq $true) "FIX binding is not limited to manual real-bounded path."
Assert-True ($realPath.noExternalDefaultPreserved -eq $true) "No-external default was removed."
Assert-True ($realPath.fixOperationNotGlobalDefault -eq $true) "FIX operation became global/default."
Assert-False $realPath.apiWorkerReachable "Manual CLI became reachable from API/Worker/default startup."

Assert-False $noExternal.externalActivationAttempted "External activation attempted during R80."
Assert-False $noExternal.tcpSocketAttempted "TCP attempted during R80."
Assert-False $noExternal.tlsAttempted "TLS attempted during R80."
Assert-False $noExternal.fixLogonSessionAttempted "FIX attempted during R80."
Assert-False $noExternal.marketDataRequestAttempted "MarketDataRequest attempted during R80."

Assert-True ($forbidden.result -eq "PASS") "Forbidden actions audit failed."
Assert-False $forbidden.ordersSubmitted "Orders were submitted."
Assert-False $forbidden.orderPathTouched "Order/trading path was touched."
Assert-False $forbidden.productionAccountUsedOrAllowed "Production account/config was used or allowed."

Assert-True ($apiWorker.result -eq "PASS") "API/Worker audit failed."
Assert-True ($apiWorker.apiWorkerGatewayMode -eq "FakeLmaxGatewayOnly") "API/Worker gateway changed away from FakeLmaxGatewayOnly."
Assert-False $apiWorker.manualCliReachableFromApiWorkerDefaultStartup "Manual CLI became API/Worker reachable."
Assert-False $apiWorker.fixProgressionWiredIntoApiWorker "FIX progression was wired into API/Worker."

Assert-True ($scheduler.result -eq "PASS") "Scheduler/polling/service audit failed."
Assert-False $scheduler.schedulerIntroduced "Scheduler introduced."
Assert-False $scheduler.pollingIntroduced "Polling introduced."
Assert-False $scheduler.hostedServiceIntroduced "Hosted service introduced."
Assert-False $scheduler.backgroundServiceIntroduced "Background service introduced."

Assert-True ($sanitize.result -eq "PASS") "Sanitization validation failed."
Assert-False $sanitize.credentialValuesReturned "credentialValuesReturned is not false."
Assert-False $sanitize.credentialValuesPrintedStoredSerialized "Credential values were printed/stored/serialized."
Assert-False $sanitize.rawEndpointValuesPrintedStoredSerialized "Raw endpoint values were printed/stored/serialized."
Assert-False $sanitize.rawTlsMaterialPrintedStoredSerialized "Raw TLS material was printed/stored/serialized."
Assert-False $sanitize.rawFixMessagesPrintedStoredSerialized "Raw FIX messages were printed/stored/serialized."
Assert-False $sanitize.rawSensitiveFixLogsPrintedStoredSerialized "Raw sensitive FIX logs were printed/stored/serialized."

Assert-True ($usdJpy.result -eq "PASS") "USDJPY caveat validation failed."
Assert-True ($usdJpy.caveatPreserved -eq $true) "USDJPY caveat missing or weakened."
Assert-False $usdJpy.weakened "USDJPY caveat weakened."

Assert-True ($gate.concreteFixSessionOperationBindingReady -eq $true) "Gate does not prove FIX binding readiness."
Assert-True ($gate.fixClientConstructedWithConcreteOperation -eq $true) "Gate does not prove concrete FIX client operation construction."
Assert-True ($gate.tlsToFixContinuationReady -eq $true) "Gate does not prove TLS-to-FIX continuation."
Assert-True ($gate.fixRequiresTlsSucceeded -eq $true) "Gate does not prove FIX requires TLS Succeeded."
Assert-True ($gate.tlsAttemptedOnlyInsufficientForFix -eq $true) "Gate does not prove TLS attempted-only is insufficient."
Assert-False $gate.tlsSuccessFaked "Gate says TLS success was faked."
Assert-False $gate.fixDefaultGlobal "Gate says FIX became global/default."
Assert-True ($gate.buildResult.status -eq "PASS") "Build evidence missing."
Assert-True ($gate.testResult.status -eq "PASS") "Test evidence missing."

Assert-True ($next.nextRecommendedPhase -eq "LMAX-R81") "Next phase recommendation missing or incorrect."
Assert-True ($next.nextPhaseIsOddNumbered -eq $true) "Next activation retry phase is not marked odd-numbered."
Assert-True ($next.fixRequiresTlsSucceeded -eq $true) "Next phase recommendation does not preserve TLS success gate."

$factory = Get-Content $factoryPath -Raw
$connector = Get-Content $connectorPath -Raw
$transport = Get-Content $transportPath -Raw
$apiProgram = Get-Content $apiProgramPath -Raw
$workerProgram = Get-Content $workerProgramPath -Raw

Assert-True ($factory.Contains("new LmaxRealReadOnlyFixFrameClient(")) "Factory does not construct LmaxRealReadOnlyFixFrameClient with arguments."
Assert-True ($factory.Contains("socketConnector.OpenFixSession")) "Factory does not bind socketConnector.OpenFixSession."
Assert-False ($factory.Contains("new LmaxRealReadOnlyFixFrameClient();")) "Factory still constructs unconfigured FIX client."
Assert-True ($connector.Contains("OpenFixSession")) "Manual connector does not expose OpenFixSession."
Assert-True ($connector.Contains("TlsBoundaryNotSucceeded")) "Manual FIX operation does not block when TLS is not succeeded."
Assert-True ($connector.Contains("FixCredentialMaterialUnavailable")) "Manual FIX operation does not keep credential gate explicit."
Assert-True ($transport.Contains("if (!tls.Succeeded)")) "Transport no longer gates FIX behind TLS Succeeded."
Assert-False ($apiProgram.Contains("LmaxReadOnlyActivationManualTcpSocketConnector")) "API can reach manual FIX connector."
Assert-False ($workerProgram.Contains("LmaxReadOnlyActivationManualTcpSocketConnector")) "Worker can reach manual FIX connector."

$artifactText = Get-ChildItem $artifactRoot -Filter "phase-lmax-r80-*" -File |
    ForEach-Object { Get-Content $_.FullName -Raw }
$joined = $artifactText -join "`n"
$forbiddenPatterns = @(
    "35=D",
    "35=F",
    "35=H",
    "35=AE",
    "554=",
    "BEGIN PRIVATE KEY",
    "password=",
    "secret=",
    "token=",
    "fix-marketdata",
    "london-demo.lmax.com"
)

foreach ($pattern in $forbiddenPatterns) {
    if ($joined -match [regex]::Escape($pattern)) {
        throw "Forbidden sensitive artifact pattern found in R80 artifacts: $pattern"
    }
}

Write-Output "LMAX_R80_VALIDATION_PASS"
