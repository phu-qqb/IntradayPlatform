$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root "artifacts\readiness\lmax-runtime-enablement"
$factoryPath = Join-Path $root "tools\QQ.Production.Intraday.Tools.LmaxReadOnlyActivation\LmaxReadOnlyActivationManualExecutionSurfaceFactory.cs"
$connectorPath = Join-Path $root "tools\QQ.Production.Intraday.Tools.LmaxReadOnlyActivation\LmaxReadOnlyActivationManualTcpSocketConnector.cs"
$transportPath = Join-Path $root "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxRealReadOnlyMarketDataTransport.cs"
$apiProgramPath = Join-Path $root "src\QQ.Production.Intraday.Api\Program.cs"
$workerProgramPath = Join-Path $root "src\QQ.Production.Intraday.Worker\Program.cs"
$focusedTestsPath = Join-Path $root "tests\QQ.Production.Intraday.Tests.Unit\LmaxReadOnlyActivationManualExecutionSurfaceTests.cs"

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

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r83-fix-credential-material-binding-summary.json")
$beforeAfter = Read-Json (Join-Path $artifactRoot "phase-lmax-r83-r82-root-cause-before-after-classification.json")
$binding = Read-Json (Join-Path $artifactRoot "phase-lmax-r83-in-memory-fix-secret-material-binding-validation.json")
$sanitize = Read-Json (Join-Path $artifactRoot "phase-lmax-r83-credential-sanitization-validation.json")
$production = Read-Json (Join-Path $artifactRoot "phase-lmax-r83-production-config-exclusion-validation.json")
$manualPath = Read-Json (Join-Path $artifactRoot "phase-lmax-r83-manual-real-bounded-path-validation.json")
$fixReadiness = Read-Json (Join-Path $artifactRoot "phase-lmax-r83-fix-logon-readiness-validation.json")
$marketData = Read-Json (Join-Path $artifactRoot "phase-lmax-r83-marketdata-block-until-fix-success-validation.json")
$noExternal = Read-Json (Join-Path $artifactRoot "phase-lmax-r83-no-external-boundary-attempted.json")
$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r83-forbidden-actions-audit.json")
$apiWorker = Read-Json (Join-Path $artifactRoot "phase-lmax-r83-api-worker-fake-gateway-audit.json")
$scheduler = Read-Json (Join-Path $artifactRoot "phase-lmax-r83-no-scheduler-polling-service-audit.json")
$usdJpy = Read-Json (Join-Path $artifactRoot "phase-lmax-r83-usdjpy-caveat-preservation.json")
$next = Read-Json (Join-Path $artifactRoot "phase-lmax-r83-next-phase-recommendation.json")
$gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r83-gate-validation.json")
$r82 = Read-Json (Join-Path $artifactRoot "phase-lmax-r82-fix-session-boundary-root-cause-summary.json")

$allowedClassifications = @(
    "LMAX_R83_PASS_FIX_CREDENTIAL_MATERIAL_BINDING_READY_NO_EXTERNAL_ACTIVATION",
    "LMAX_R83_PASS_FIX_CREDENTIAL_BINDING_DESIGN_READY_IMPLEMENTATION_DEFERRED_NO_EXTERNAL_ACTIVATION",
    "LMAX_R83_FAIL_FIX_CREDENTIAL_MATERIAL_BINDING_NOT_PROVABLE",
    "LMAX_R83_FAIL_REAL_SECRET_MATERIAL_NOT_ALLOWED_FOR_APPROVED_RETRY",
    "LMAX_R83_FAIL_REAL_SECRET_MATERIAL_NOT_LOADED_FOR_APPROVED_RETRY",
    "LMAX_R83_FAIL_CREDENTIAL_VALUES_RETURNED_OR_SECRET_RISK",
    "LMAX_R83_FAIL_PRODUCTION_ACCOUNT_RISK",
    "LMAX_R83_FAIL_API_WORKER_GATEWAY_REGRESSION",
    "LMAX_R83_FAIL_MARKETDATA_ALLOWED_WITHOUT_FIX_SUCCESS",
    "LMAX_R83_FAIL_FORBIDDEN_ACTION_INTRODUCED",
    "LMAX_R83_FAIL_EXTERNAL_BOUNDARY_ATTEMPTED",
    "LMAX_R83_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R83_FAIL_BUILD_OR_TESTS"
)

Assert-True ($allowedClassifications -contains $summary.classification) "R83 classification is absent or not allowed."
Assert-True ($summary.classification -eq "LMAX_R83_PASS_FIX_CREDENTIAL_MATERIAL_BINDING_READY_NO_EXTERNAL_ACTIVATION") "R83 final classification mismatch."
Assert-True ($r82.classification -eq "LMAX_R82_PASS_FIX_SESSION_BOUNDARY_ROOT_CAUSE_IDENTIFIED_NO_EXTERNAL_ACTIVATION") "R82 success evidence missing."

Assert-True ($summary.approvedDemoReadOnlyInMemoryFixLogonCredentialMaterialReady -eq $true) "ApprovedDemoReadOnlyInMemoryFixLogonCredentialMaterial is not ready."
Assert-True ($summary.realSecretMaterialAllowedNowForFutureApprovedManualRealBoundedRetryPath -eq $true) "RealSecretMaterialAllowedNow remains false for future approved retry."
Assert-True ($summary.realSecretMaterialLoadedInMemoryForFutureAttemptSanitizedBoolean -eq $true) "RealSecretMaterialLoaded remains false for future approved retry."
Assert-False $summary.credentialValuesReturned "credentialValuesReturned is not false."
Assert-False $summary.rawCredentialsFixFieldsMessagesPrintedStoredSerialized "Raw credential/FIX values printed/stored/serialized."
Assert-True ($summary.productionAccountConfigExcluded -eq $true) "Production account/config not excluded."
Assert-True ($summary.credentialMaterialUnreachableFromApiWorkerDefaultStartup -eq $true) "Credential material became API/Worker reachable."
Assert-True ($summary.marketDataRequestBlockedUntilFixSuccess -eq $true) "MarketDataRequest can be attempted without FIX success."
Assert-False $summary.externalActivationAttempted "External activation attempted during R83."

Assert-True ($beforeAfter.before.classification -eq "LMAX_R82_PASS_FIX_SESSION_BOUNDARY_ROOT_CAUSE_IDENTIFIED_NO_EXTERNAL_ACTIVATION") "R82 before classification missing."
Assert-False $beforeAfter.before.realSecretMaterialAllowedNow "Before-state should show RealSecretMaterialAllowedNow=false."
Assert-False $beforeAfter.before.realSecretMaterialLoaded "Before-state should show RealSecretMaterialLoaded=false."
Assert-True ($beforeAfter.after.realSecretMaterialAllowedNowForFutureApprovedManualRealBoundedRetryPath -eq $true) "After-state does not enable RealSecretMaterialAllowedNow."
Assert-True ($beforeAfter.after.realSecretMaterialLoadedInMemoryForFutureAttemptSanitizedBoolean -eq $true) "After-state does not prove RealSecretMaterialLoaded."
Assert-False $beforeAfter.after.credentialValuesReturned "After-state credentialValuesReturned is not false."
Assert-False $beforeAfter.after.rawSecretSerialized "After-state serialized raw material."
Assert-False $beforeAfter.after.externalActivationAttempted "After-state attempted external activation."

Assert-True ($binding.approvedDemoReadOnlyInMemoryFixLogonCredentialMaterialReady -eq $true) "Binding validation does not prove material readiness."
Assert-True ($binding.realSecretMaterialAllowedForApprovedManualRetry -eq $true) "Binding validation does not allow material for approved manual retry."
Assert-True ($binding.realSecretMaterialLoadedInMemoryForFutureAttempt -eq $true) "Binding validation does not load material as sanitized boolean."
Assert-True ($binding.realSecretMaterialLoadedIsSanitizedBooleanOnly -eq $true) "Binding evidence is not boolean-only."
Assert-False $binding.credentialValuesReturned "Binding returned credential values."
Assert-False $binding.sensitiveMaterialReturned "Sensitive material returned."
Assert-False $binding.sensitiveMaterialPrinted "Sensitive material printed."
Assert-False $binding.sensitiveMaterialStored "Sensitive material stored."
Assert-False $binding.sensitiveMaterialSerialized "Sensitive material serialized."
Assert-False $binding.rawSecretSerialized "Raw material serialized."
Assert-False $binding.rawFixSerialized "Raw FIX serialized."
Assert-False $binding.apiWorkerReachable "Binding API/Worker reachable."
Assert-False $binding.externalBoundaryAttemptedDuringValidation "Binding validation attempted external boundary."

Assert-True ($sanitize.result -eq "PASS") "Credential sanitization validation failed."
Assert-False $sanitize.credentialValuesReturned "credentialValuesReturned not false in sanitization validation."
Assert-False $sanitize.rawCredentialsPrintedStoredSerialized "Raw credentials printed/stored/serialized."
Assert-False $sanitize.rawFixFieldsPrintedStoredSerialized "Raw FIX fields printed/stored/serialized."
Assert-False $sanitize.rawFixMessagesPrintedStoredSerialized "Raw FIX messages printed/stored/serialized."
Assert-False $sanitize.rawSensitiveFixLogsPrintedStoredSerialized "Sensitive FIX logs printed/stored/serialized."
Assert-False $sanitize.credentialDerivedValuesPrintedStoredSerialized "Credential-derived values printed/stored/serialized."
Assert-False $sanitize.fullAccountIdentifiersPrintedStoredSerialized "Full account identifiers printed/stored/serialized."

Assert-True ($production.result -eq "PASS") "Production exclusion validation failed."
Assert-True ($production.endpointMode -eq "Demo") "Endpoint mode not Demo."
Assert-True ($production.productionAccountConfigExcluded -eq $true) "Production account/config not excluded."
Assert-False $production.productionAccountConfigAllowed "Production account/config allowed."
Assert-False $production.productionEndpointAllowed "Production endpoint allowed."
Assert-False $production.apiWorkerReachable "Production/credential path API/Worker reachable."

Assert-True ($manualPath.noExternalDefaultPreserved -eq $true) "No-external default removed."
Assert-True ($manualPath.realBoundedAdapterNotGlobalDefault -eq $true) "Real bounded adapter became global/default."
Assert-True ($manualPath.credentialMaterialBindingNotGlobalDefault -eq $true) "Credential material binding became global/default."
Assert-False $manualPath.manualCliBecomesDefaultLivePath "Manual CLI became default live path."
Assert-False $manualPath.apiWorkerDefaultStartupReachable "Manual path became API/Worker default reachable."

Assert-True ($fixReadiness.approvedDemoReadOnlyInMemoryFixLogonCredentialMaterialReady -eq $true) "FIX readiness lacks credential material."
Assert-True ($fixReadiness.fixStillRequiresTlsSucceeded -eq $true) "FIX no longer requires TLS Succeeded."
Assert-True ($fixReadiness.tlsAttemptedOnlyInsufficientForFix -eq $true) "TLS attempted-only can trigger FIX."
Assert-False $fixReadiness.fixBoundaryAttemptedDuringR83 "FIX attempted during R83."
Assert-False $fixReadiness.fixFrameWriteExecutedDuringR83 "FIX frame write executed during R83."
Assert-False $fixReadiness.ordersPossible "Orders possible."
Assert-False $fixReadiness.tradingMutationPossible "Trading mutation possible."

Assert-True ($marketData.result -eq "PASS") "MarketData gate validation failed."
Assert-True ($marketData.marketDataRequestBlockedUntilFixSuccess -eq $true) "MarketDataRequest not blocked until FIX success."
Assert-False $marketData.marketDataRequestAttemptedDuringR83 "MarketDataRequest attempted during R83."
Assert-False $marketData.marketDataAttemptAllowedWithoutFixSuccess "MarketDataRequest allowed without FIX success."

Assert-False $noExternal.externalActivationAttempted "External activation attempted during R83."
Assert-True ($noExternal.credentialConfigBoundary -eq "ValidationOnly") "Credential/config status should be ValidationOnly."
Assert-True ($noExternal.tcpSocketBoundary -eq "NotAttempted") "TCP status should be NotAttempted."
Assert-True ($noExternal.tlsBoundary -eq "NotAttempted") "TLS status should be NotAttempted."
Assert-True ($noExternal.fixLogonSessionBoundary -eq "ValidationOnly") "FIX status should be ValidationOnly."
Assert-True ($noExternal.marketDataRequestBoundary -eq "NotAttempted") "MarketDataRequest status should be NotAttempted."
Assert-False $noExternal.socketOpened "Socket opened during R83."
Assert-False $noExternal.tlsAttempted "TLS attempted during R83."
Assert-False $noExternal.fixLogonAttempted "FIX logon attempted during R83."
Assert-False $noExternal.marketDataRequestSent "MarketDataRequest sent during R83."

Assert-True ($forbidden.result -eq "PASS") "Forbidden actions audit failed."
Assert-False $forbidden.ordersSubmitted "Orders submitted."
Assert-False $forbidden.tradingEnabled "Trading enabled."
Assert-False $forbidden.tradingStateMutated "Trading state mutated."
Assert-False $forbidden.productionAccountUsedOrAllowed "Production account/config used or allowed."
Assert-False $forbidden.schedulerIntroduced "Scheduler introduced."
Assert-False $forbidden.pollingIntroduced "Polling introduced."
Assert-False $forbidden.replayIntroduced "Replay introduced."
Assert-False $forbidden.shadowReplayIntroduced "Shadow replay introduced."

Assert-True ($apiWorker.result -eq "PASS") "API/Worker audit failed."
Assert-True ($apiWorker.apiWorkerGatewayMode -eq "FakeLmaxGatewayOnly") "API/Worker gateway changed away from FakeLmaxGatewayOnly."
Assert-False $apiWorker.credentialMaterialReachableFromApiWorkerDefaultStartup "Credential material reachable from API/Worker default startup."
Assert-False $apiWorker.manualCliReachableFromApiWorkerDefaultStartup "Manual CLI reachable from API/Worker default startup."

Assert-True ($scheduler.result -eq "PASS") "Scheduler/polling/service audit failed."
Assert-False $scheduler.hostedServiceIntroduced "Hosted service introduced."
Assert-False $scheduler.backgroundServiceIntroduced "Background service introduced."
Assert-False $scheduler.schedulerIntroduced "Scheduler introduced."
Assert-False $scheduler.pollingIntroduced "Polling introduced."

Assert-True ($usdJpy.result -eq "PASS") "USDJPY caveat validation failed."
Assert-True ($usdJpy.caveatPreserved -eq $true) "USDJPY caveat missing or weakened."
Assert-False $usdJpy.weakened "USDJPY caveat weakened."

Assert-True ($gate.approvedDemoReadOnlyInMemoryFixLogonCredentialMaterialReady -eq $true) "Gate does not prove material readiness."
Assert-True ($gate.realSecretMaterialAllowedForApprovedManualRetry -eq $true) "Gate does not prove material allowed for approved retry."
Assert-True ($gate.realSecretMaterialLoadedInMemoryForFutureAttempt -eq $true) "Gate does not prove material loaded as boolean evidence."
Assert-False $gate.credentialValuesReturned "Gate says credentialValuesReturned is true."
Assert-False $gate.rawCredentialsFixFieldsMessagesPrintedStoredSerialized "Gate says raw values were exposed."
Assert-False $gate.externalBoundaryAttemptedDuringR83 "Gate says external boundary was attempted."
Assert-True ($gate.buildResult.status -eq "PASS") "Build evidence missing."
Assert-True ($gate.testResult.status -eq "PASS") "Test evidence missing."

Assert-True ($next.nextRecommendedPhase -eq "LMAX-R85") "Next phase recommendation missing or incorrect."
Assert-True ($next.useR85NotR84BecauseActivationRetryPhasesMustBeOdd -eq $true) "Odd activation phase guidance missing."

$factory = Get-Content $factoryPath -Raw
$connector = Get-Content $connectorPath -Raw
$transport = Get-Content $transportPath -Raw
$apiProgram = Get-Content $apiProgramPath -Raw
$workerProgram = Get-Content $workerProgramPath -Raw
$focusedTests = Get-Content $focusedTestsPath -Raw

Assert-True ($factory.Contains("ValidateFixCredentialMaterialBinding")) "Factory does not expose no-external credential material validation."
Assert-True ($factory.Contains("LmaxCredentialConfigSourceBinding.CreateApprovedOperation(CredentialBindingResult())")) "Factory does not bind approved credential operation."
Assert-True ($factory.Contains("RealSecretMaterialAllowedNow: true")) "Factory does not allow in-memory material for future approved manual retry path."
Assert-True ($factory.Contains("new LmaxRealReadOnlyCredentialConfigClient(")) "Factory does not construct configured credential client."
Assert-True ($factory.Contains("NoExternalBoundaryMode")) "No-external default mode missing."
Assert-True ($connector.Contains("FixCredentialMaterialUnavailable")) "FIX credential gate missing."
Assert-True ($connector.Contains("RealSecretMaterialLoaded")) "FIX operation does not require loaded material."
Assert-True ($transport.Contains("if (!fix.Succeeded)")) "MarketDataRequest no longer gated by FIX success."
Assert-False ($apiProgram.Contains("LmaxReadOnlyActivationManualFixCredentialMaterialBinding")) "API references manual credential binding."
Assert-False ($workerProgram.Contains("LmaxReadOnlyActivationManualFixCredentialMaterialBinding")) "Worker references manual credential binding."
Assert-True ($focusedTests.Contains("R82_fix_credential_material_root_cause_is_reproduced_by_validation_only_policy")) "Focused root-cause reproduction test missing."
Assert-True ($focusedTests.Contains("Approved_manual_real_bounded_path_enables_in_memory_fix_material_without_returning_values")) "Focused in-memory binding test missing."
Assert-True ($focusedTests.Contains("MarketDataRequest")) "Focused tests do not cover market-data gating."

$artifactText = Get-ChildItem $artifactRoot -Filter "phase-lmax-r83-*" -File |
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
    "username=",
    "secret=",
    "token=",
    "fix-marketdata",
    "london-demo.lmax.com"
)

foreach ($pattern in $forbiddenPatterns) {
    if ($joined -match [regex]::Escape($pattern)) {
        throw "Forbidden sensitive artifact pattern found in R83 artifacts: $pattern"
    }
}

Write-Output "LMAX_R83_VALIDATION_PASS"
