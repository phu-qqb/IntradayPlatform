$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root "artifacts\readiness\lmax-runtime-enablement"
$factoryPath = Join-Path $root "tools\QQ.Production.Intraday.Tools.LmaxReadOnlyActivation\LmaxReadOnlyActivationManualExecutionSurfaceFactory.cs"
$connectorPath = Join-Path $root "tools\QQ.Production.Intraday.Tools.LmaxReadOnlyActivation\LmaxReadOnlyActivationManualTcpSocketConnector.cs"
$lowLevelPath = Join-Path $root "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxExecutableReadOnlyRealLowLevelDependencies.cs"
$credentialProviderPath = Join-Path $root "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxRealReadOnlyCredentialConfigBoundaryProvider.cs"
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

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r82-fix-session-boundary-root-cause-summary.json")
$beforeAfter = Read-Json (Join-Path $artifactRoot "phase-lmax-r82-r81-boundary-before-after-classification.json")
$fixReview = Read-Json (Join-Path $artifactRoot "phase-lmax-r82-r81-fix-boundary-review.json")
$rootCause = Read-Json (Join-Path $artifactRoot "phase-lmax-r82-fix-credential-material-root-cause.json")
$requirements = Read-Json (Join-Path $artifactRoot "phase-lmax-r82-fix-session-material-requirements-review.json")
$credentialReview = Read-Json (Join-Path $artifactRoot "phase-lmax-r82-credential-source-sanitization-review.json")
$inMemoryDecision = Read-Json (Join-Path $artifactRoot "phase-lmax-r82-in-memory-secret-use-decision.json")
$marketDataReview = Read-Json (Join-Path $artifactRoot "phase-lmax-r82-marketdata-block-after-fix-failure-review.json")
$realPath = Read-Json (Join-Path $artifactRoot "phase-lmax-r82-real-bounded-path-validation.json")
$noExternal = Read-Json (Join-Path $artifactRoot "phase-lmax-r82-no-external-boundary-attempted.json")
$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r82-forbidden-actions-audit.json")
$apiWorker = Read-Json (Join-Path $artifactRoot "phase-lmax-r82-api-worker-fake-gateway-audit.json")
$scheduler = Read-Json (Join-Path $artifactRoot "phase-lmax-r82-no-scheduler-polling-service-audit.json")
$sanitize = Read-Json (Join-Path $artifactRoot "phase-lmax-r82-credential-endpoint-tls-fix-sanitization-validation.json")
$usdJpy = Read-Json (Join-Path $artifactRoot "phase-lmax-r82-usdjpy-caveat-preservation.json")
$next = Read-Json (Join-Path $artifactRoot "phase-lmax-r82-next-phase-recommendation.json")
$gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r82-gate-validation.json")
$r81Summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r81-temporary-readonly-activation-retry-summary.json")
$r81Fix = Read-Json (Join-Path $artifactRoot "phase-lmax-r81-fix-session-boundary-evidence.json")

$allowedClassifications = @(
    "LMAX_R82_PASS_FIX_SESSION_BOUNDARY_ROOT_CAUSE_IDENTIFIED_NO_EXTERNAL_ACTIVATION",
    "LMAX_R82_PASS_FIX_CREDENTIAL_MATERIAL_MISSING_NO_EXTERNAL_ACTIVATION",
    "LMAX_R82_PASS_FIX_CREDENTIAL_SOURCE_VALIDATION_ONLY_BLOCKS_LOGON_NO_EXTERNAL_ACTIVATION",
    "LMAX_R82_PASS_FIX_SECRET_MATERIAL_BINDING_REQUIRED_NO_EXTERNAL_ACTIVATION",
    "LMAX_R82_PASS_FIX_SESSION_CONFIG_MISSING_NO_EXTERNAL_ACTIVATION",
    "LMAX_R82_PASS_FIX_LOGON_FIELD_MISSING_NO_EXTERNAL_ACTIVATION",
    "LMAX_R82_PASS_FIX_CREDENTIAL_MATERIAL_BINDING_READY_NO_EXTERNAL_ACTIVATION",
    "LMAX_R82_FAIL_FIX_ROOT_CAUSE_NOT_PROVABLE",
    "LMAX_R82_FAIL_FIX_MATERIAL_CATEGORY_NOT_IDENTIFIED",
    "LMAX_R82_FAIL_CREDENTIAL_SOURCE_REVIEW_MISSING",
    "LMAX_R82_FAIL_SECRET_SANITIZATION_RISK",
    "LMAX_R82_FAIL_MARKETDATA_ALLOWED_AFTER_FIX_FAILURE",
    "LMAX_R82_FAIL_EXTERNAL_BOUNDARY_ATTEMPTED",
    "LMAX_R82_FAIL_FIX_OR_MARKETDATA_ATTEMPTED",
    "LMAX_R82_FAIL_FORBIDDEN_ACTION_INTRODUCED",
    "LMAX_R82_FAIL_CREDENTIAL_VALUES_RETURNED_OR_SECRET_RISK",
    "LMAX_R82_FAIL_API_WORKER_GATEWAY_REGRESSION",
    "LMAX_R82_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R82_FAIL_BUILD_OR_TESTS"
)

Assert-True ($allowedClassifications -contains $summary.classification) "R82 classification is absent or not allowed."
Assert-True ($summary.classification -eq "LMAX_R82_PASS_FIX_SESSION_BOUNDARY_ROOT_CAUSE_IDENTIFIED_NO_EXTERNAL_ACTIVATION") "R82 final classification mismatch."

Assert-True ($r81Summary.classification -eq "LMAX_R81_FAIL_FIX_LOGON_OR_SESSION_BOUNDARY") "R81 FIX boundary evidence is missing or mismatched."
Assert-True ($r81Summary.tcpSocketSucceeded -eq $true) "R81 TCP success is not proven."
Assert-True ($r81Summary.tlsBoundaryResult -eq "Succeeded") "R81 TLS success is not proven."
Assert-True ($r81Summary.fixLogonSessionAttempted -eq $true) "R81 FIX boundary was not reached."
Assert-True ($r81Summary.socketConnectorOpenFixSessionReached -eq $true) "R81 OpenFixSession evidence missing."
Assert-True ($r81Summary.fixBoundaryResultCategory -eq "FixCredentialMaterialUnavailable") "FixCredentialMaterialUnavailable is not acknowledged in R81 summary."
Assert-True ($r81Fix.fixBoundaryResultCategory -eq "FixCredentialMaterialUnavailable") "FixCredentialMaterialUnavailable is not acknowledged in FIX evidence."
Assert-False $r81Summary.marketDataRequestAttempted "R81 unexpectedly attempted MarketDataRequest after FIX failure."

Assert-True ($beforeAfter.before.classification -eq "LMAX_R81_FAIL_FIX_LOGON_OR_SESSION_BOUNDARY") "R81 before classification missing."
Assert-True ($beforeAfter.after.rootCauseIdentified -eq $true) "R82 after-state does not identify root cause."
Assert-True ($beforeAfter.after.missingMaterialCategoryIdentified -eq $true) "R82 after-state does not identify missing material category."

Assert-True ($summary.r81TcpSuccessProven -eq $true) "Summary does not prove TCP success."
Assert-True ($summary.r81TlsSuccessProven -eq $true) "Summary does not prove TLS success."
Assert-True ($summary.r81FixBoundaryReachedProven -eq $true) "Summary does not prove FIX boundary reached."
Assert-True ($summary.fixBoundaryResultCategory -eq "FixCredentialMaterialUnavailable") "Summary does not acknowledge FIX material blocker."
Assert-True ($summary.exactMissingFixCredentialMaterialCategory -eq "ApprovedDemoReadOnlyInMemoryFixLogonCredentialMaterial") "Missing FIX material category not identified."
Assert-True ($summary.exactMissingGate -eq "LmaxReadOnlyCredentialSanitizationRecord.RealSecretMaterialLoaded") "Exact missing gate not identified."
Assert-True ($summary.inMemoryOnlySecretMaterialBindingRequired -eq $true) "In-memory-only secret material binding requirement missing."
Assert-True ($summary.credentialSourceExists -eq $true) "Credential source existence review missing."
Assert-True ($summary.credentialSourceValidationOnly -eq $true) "Credential source ValidationOnly state missing."
Assert-True ($summary.marketDataRequestBlockedAfterFixFailure -eq $true) "MarketDataRequest block after FIX failure missing."
Assert-False $summary.externalActivationAttemptedInR82 "External activation was attempted during R82."
Assert-False $summary.credentialValuesReturned "credentialValuesReturned is not false."

Assert-True ($fixReview.fixBoundaryResultCategory -eq "FixCredentialMaterialUnavailable") "FIX review does not include material unavailable category."
Assert-True ($fixReview.marketDataRequestBlockedAfterFixFailure -eq $true) "FIX review does not prove MarketDataRequest blocked."

Assert-True ($rootCause.rootCauseCategory -eq "FixCredentialSourceValidationOnlyBlocksLogon") "Root cause category is not precise."
Assert-True ($rootCause.missingMaterialCategory -eq "ApprovedDemoReadOnlyInMemoryFixLogonCredentialMaterial") "Root cause does not identify missing material."
Assert-True ($rootCause.missingMaterialGate -eq "LmaxReadOnlyCredentialSanitizationRecord.RealSecretMaterialLoaded") "Root cause does not identify RealSecretMaterialLoaded gate."
Assert-False $rootCause.observedGateValueInR81 "R81 gate value should be false."
Assert-True ($rootCause.policyDecision.realSecretMaterialAllowedNow -eq $false) "Policy decision did not preserve RealSecretMaterialAllowedNow=false."
Assert-True ($rootCause.credentialDependencyDecision.realSecretMaterialLoaded -eq $false) "Credential dependency decision did not record material as unloaded."
Assert-True ($rootCause.fixBoundaryDecision.requiresRealSecretMaterialLoaded -eq $true) "FIX boundary decision does not require RealSecretMaterialLoaded."
Assert-True ($rootCause.individualFixFieldValidationReached -eq $false) "Per-field FIX material validation should not be marked reached."
Assert-False $rootCause.rawMaterialInspected "R82 inspected raw material."
Assert-False $rootCause.rawMaterialSerialized "R82 serialized raw material."

Assert-True ($requirements.missingMaterialCategory -eq "ApprovedDemoReadOnlyInMemoryFixLogonCredentialMaterial") "Requirements review missing material category."
Assert-True ($requirements.exactPerFieldMaterialUnavailableInR81 -eq "NotReachedBecauseAggregateRealSecretMaterialLoadedGateWasFalse") "Requirements review should show aggregate gate stopped per-field validation."
Assert-False $requirements.senderCounterpartyRawValuesSerialized "Sensitive session identity values serialized."
Assert-False $requirements.authenticationRawValuesSerialized "Sensitive authentication values serialized."
Assert-False $requirements.fullAccountIdentifiersSerialized "Full account identifiers serialized."
Assert-False $requirements.rawFixMessagesSerialized "Raw FIX messages serialized."

Assert-True ($credentialReview.credentialSourceExists -eq $true) "Credential source review missing."
Assert-True ($credentialReview.credentialSourceValidationOnly -eq $true) "Credential source is not recorded as ValidationOnly."
Assert-False $credentialReview.currentPolicyAllowsRealSecretMaterialNow "R82 should not allow real secret material now."
Assert-False $credentialReview.credentialValuesReturned "credentialValuesReturned not false in credential review."
Assert-False $credentialReview.sensitiveMaterialReturned "Sensitive material returned in credential review."
Assert-False $credentialReview.sensitiveMaterialPrinted "Sensitive material printed in credential review."
Assert-False $credentialReview.sensitiveMaterialStored "Sensitive material stored in credential review."
Assert-False $credentialReview.sensitiveMaterialSerialized "Sensitive material serialized in credential review."
Assert-False $credentialReview.productionAccountConfigAllowed "Production account/config allowed."

Assert-True ($inMemoryDecision.bindingRequired -eq $true) "In-memory secret material binding decision missing."
Assert-True ($inMemoryDecision.manualRealBoundedPathOnly -eq $true) "Binding decision not limited to manual real-bounded path."
Assert-False $inMemoryDecision.apiWorkerReachable "Binding decision made API/Worker reachable."
Assert-True ($inMemoryDecision.artifactCredentialValuesReturnedMustRemainFalse -eq $true) "Decision does not keep credentialValuesReturned=false."
Assert-False $inMemoryDecision.sensitiveMaterialReturnedToArtifacts "Decision allows sensitive material in artifacts."
Assert-False $inMemoryDecision.sensitiveMaterialPrintedStoredSerialized "Decision allows sensitive material printed/stored/serialized."
Assert-False $inMemoryDecision.rawFixMessagesAllowedInArtifacts "Decision allows raw FIX messages in artifacts."
Assert-False $inMemoryDecision.r82ImplementsBinding "R82 should not implement binding."

Assert-True ($marketDataReview.marketDataRequestBlockedAfterFixFailure -eq $true) "MarketDataRequest allowed after FIX failure."
Assert-False $marketDataReview.marketDataAttemptAllowedWithoutFixSuccess "MarketDataRequest allowed without FIX success."

Assert-True ($realPath.r81FixBoundaryReached -eq $true) "Real bounded path validation missing FIX boundary evidence."
Assert-True ($realPath.noExternalDefaultPreserved -eq $true) "No-external default removed."
Assert-True ($realPath.realBoundedPathNotGlobalDefault -eq $true) "Real bounded path became global/default."
Assert-False $realPath.apiWorkerReachable "Manual bounded path became API/Worker reachable."

Assert-False $noExternal.externalActivationAttempted "External activation attempted during R82."
Assert-False $noExternal.tcpSocketAttempted "TCP attempted during R82."
Assert-False $noExternal.tlsAttempted "TLS attempted during R82."
Assert-False $noExternal.fixLogonSessionAttempted "FIX attempted during R82."
Assert-False $noExternal.marketDataRequestAttempted "MarketDataRequest attempted during R82."

Assert-True ($forbidden.result -eq "PASS") "Forbidden actions audit failed."
Assert-False $forbidden.ordersSubmitted "Orders were submitted."
Assert-False $forbidden.tradingEnabled "Trading was enabled."
Assert-False $forbidden.tradingStateMutated "Trading state was mutated."
Assert-False $forbidden.productionAccountUsedOrAllowed "Production account/config was used or allowed."
Assert-False $forbidden.rawFixMessagesStored "Raw FIX messages stored."
Assert-False $forbidden.rawSensitiveFixLogsStored "Raw sensitive FIX logs stored."

Assert-True ($apiWorker.result -eq "PASS") "API/Worker audit failed."
Assert-True ($apiWorker.apiWorkerGatewayMode -eq "FakeLmaxGatewayOnly") "API/Worker gateway changed away from FakeLmaxGatewayOnly."
Assert-False $apiWorker.manualCliReachableFromApiWorkerDefaultStartup "Manual CLI became reachable from API/Worker/default startup."
Assert-False $apiWorker.boundedActivationPathWiredIntoApiWorker "Bounded activation path wired into API/Worker."

Assert-True ($scheduler.result -eq "PASS") "Scheduler/polling/service audit failed."
Assert-False $scheduler.hostedServiceIntroduced "Hosted service introduced."
Assert-False $scheduler.backgroundServiceIntroduced "Background service introduced."
Assert-False $scheduler.schedulerIntroduced "Scheduler introduced."
Assert-False $scheduler.pollingIntroduced "Polling introduced."
Assert-False $scheduler.replayIntroduced "Replay introduced."
Assert-False $scheduler.shadowReplayIntroduced "Shadow replay introduced."

Assert-True ($sanitize.result -eq "PASS") "Sanitization review failed."
Assert-False $sanitize.credentialValuesReturned "credentialValuesReturned not false."
Assert-False $sanitize.credentialValuesPrintedStoredSerialized "Credential values printed/stored/serialized."
Assert-False $sanitize.rawEndpointValuesPrintedStoredSerialized "Raw endpoint values printed/stored/serialized."
Assert-False $sanitize.rawTlsMaterialPrintedStoredSerialized "Raw TLS material printed/stored/serialized."
Assert-False $sanitize.rawFixMessagesPrintedStoredSerialized "Raw FIX messages printed/stored/serialized."
Assert-False $sanitize.rawSensitiveFixLogsPrintedStoredSerialized "Raw sensitive FIX logs printed/stored/serialized."
Assert-False $sanitize.fullAccountIdentifiersPrintedStoredSerialized "Full account identifiers printed/stored/serialized."
Assert-False $sanitize.credentialDerivedValuesPrintedStoredSerialized "Credential-derived values printed/stored/serialized."

Assert-True ($usdJpy.result -eq "PASS") "USDJPY caveat validation failed."
Assert-True ($usdJpy.caveatPreserved -eq $true) "USDJPY caveat missing or weakened."
Assert-False $usdJpy.weakened "USDJPY caveat weakened."

Assert-True ($gate.r81FixBoundaryEvidenceReviewed -eq $true) "Gate does not prove R81 FIX evidence review."
Assert-True ($gate.fixCredentialMaterialUnavailableAcknowledged -eq $true) "Gate does not acknowledge FixCredentialMaterialUnavailable."
Assert-True ($gate.missingMaterialCategoryIdentified -eq $true) "Gate does not identify missing material category."
Assert-True ($gate.responsiblePathNamed -eq $true) "Gate does not name responsible path."
Assert-True ($gate.credentialSourceSanitizationReviewed -eq $true) "Gate lacks credential source/sanitization review."
Assert-True ($gate.inMemoryOnlySecretUseDecisionPresent -eq $true) "Gate lacks in-memory-only secret use decision."
Assert-True ($gate.marketDataRequestBlockedAfterFixFailure -eq $true) "Gate does not prove MarketDataRequest remains blocked."
Assert-False $gate.externalBoundaryAttemptedDuringR82 "Gate says external boundary was attempted."
Assert-False $gate.credentialValuesReturned "Gate says credentialValuesReturned is true."
Assert-True ($gate.buildResult.status -eq "PASS") "Build evidence missing."
Assert-True ($gate.testResult.status -eq "PASS") "Test evidence missing."

Assert-True ($next.nextRecommendedPhase -eq "LMAX-R83") "Next phase recommendation missing or incorrect."
Assert-True ($next.r83ShouldRemainNoExternalUnlessExplicitlyOperatorApprovedOtherwise -eq $true) "R83 no-external guidance missing."
Assert-True ($next.r83ShouldBindSecretMaterialInMemoryOnly -eq $true) "R83 in-memory-only binding guidance missing."
Assert-True ($next.r83MustKeepCredentialValuesReturnedFalseInArtifacts -eq $true) "R83 credentialValuesReturned guidance missing."

$factory = Get-Content $factoryPath -Raw
$connector = Get-Content $connectorPath -Raw
$lowLevel = Get-Content $lowLevelPath -Raw
$credentialProvider = Get-Content $credentialProviderPath -Raw
$transport = Get-Content $transportPath -Raw
$apiProgram = Get-Content $apiProgramPath -Raw
$workerProgram = Get-Content $workerProgramPath -Raw

Assert-True ($factory.Contains("RealSecretMaterialAllowedNow: false")) "Manual factory no longer shows ValidationOnly secret material policy."
Assert-True ($connector.Contains("FixCredentialMaterialUnavailable")) "Manual connector no longer has explicit FIX material unavailable gate."
Assert-True ($connector.Contains("RealSecretMaterialLoaded")) "Manual connector no longer checks RealSecretMaterialLoaded."
Assert-True ($lowLevel.Contains("RealCredentialDependencyAcceptedNoSecretMaterialLoaded")) "Credential dependency no longer records accepted no-material status."
Assert-True ($credentialProvider.Contains("CredentialConfigBoundaryAcceptedNoSecretMaterialLoaded")) "Credential provider no longer records no-material status."
Assert-True ($transport.Contains("if (!fix.Succeeded)")) "Transport no longer blocks market data after FIX failure."
Assert-False ($apiProgram.Contains("LmaxReadOnlyActivationManualTcpSocketConnector")) "API can reach manual connector."
Assert-False ($workerProgram.Contains("LmaxReadOnlyActivationManualTcpSocketConnector")) "Worker can reach manual connector."

$artifactText = Get-ChildItem $artifactRoot -Filter "phase-lmax-r82-*" -File |
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
        throw "Forbidden sensitive artifact pattern found in R82 artifacts: $pattern"
    }
}

Write-Output "LMAX_R82_VALIDATION_PASS"
