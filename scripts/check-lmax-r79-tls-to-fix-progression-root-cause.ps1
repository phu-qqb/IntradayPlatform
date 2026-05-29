$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root "artifacts\readiness\lmax-runtime-enablement"

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

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r79-tls-to-fix-progression-root-cause-summary.json")
$r78Gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r79-r78-gate-review.json")
$beforeAfter = Read-Json (Join-Path $artifactRoot "phase-lmax-r79-r77-boundary-before-after-classification.json")
$tlsReview = Read-Json (Join-Path $artifactRoot "phase-lmax-r79-tls-attempt-review.json")
$rootCause = Read-Json (Join-Path $artifactRoot "phase-lmax-r79-fix-not-attempted-root-cause.json")
$trace = Read-Json (Join-Path $artifactRoot "phase-lmax-r79-tls-to-fix-progression-trace.json")
$fixReview = Read-Json (Join-Path $artifactRoot "phase-lmax-r79-fix-provider-client-binding-review.json")
$sequence = Read-Json (Join-Path $artifactRoot "phase-lmax-r79-runtime-sequence-review.json")
$realPath = Read-Json (Join-Path $artifactRoot "phase-lmax-r79-real-bounded-path-validation.json")
$noExternal = Read-Json (Join-Path $artifactRoot "phase-lmax-r79-no-external-boundary-attempted.json")
$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r79-forbidden-actions-audit.json")
$apiWorker = Read-Json (Join-Path $artifactRoot "phase-lmax-r79-api-worker-fake-gateway-audit.json")
$scheduler = Read-Json (Join-Path $artifactRoot "phase-lmax-r79-no-scheduler-polling-service-audit.json")
$sanitize = Read-Json (Join-Path $artifactRoot "phase-lmax-r79-credential-endpoint-tls-fix-sanitization-validation.json")
$usdJpy = Read-Json (Join-Path $artifactRoot "phase-lmax-r79-usdjpy-caveat-preservation.json")
$next = Read-Json (Join-Path $artifactRoot "phase-lmax-r79-next-phase-recommendation.json")
$gate = Read-Json (Join-Path $artifactRoot "phase-lmax-r79-gate-validation.json")

$allowedClassifications = @(
    "LMAX_R79_PASS_TLS_TO_FIX_PROGRESSION_ROOT_CAUSE_IDENTIFIED_NO_EXTERNAL_ACTIVATION",
    "LMAX_R79_PASS_FIX_PROGRESS_STOP_AFTER_TLS_BY_DESIGN_NO_EXTERNAL_ACTIVATION",
    "LMAX_R79_PASS_FIX_CLIENT_BINDING_MISSING_NO_EXTERNAL_ACTIVATION",
    "LMAX_R79_PASS_FIX_SESSION_OPERATION_BINDING_MISSING_NO_EXTERNAL_ACTIVATION",
    "LMAX_R79_PASS_TLS_TO_FIX_CONTINUATION_BINDING_MISSING_NO_EXTERNAL_ACTIVATION",
    "LMAX_R79_PASS_FIX_PROVIDER_PRESENT_NOT_SEQUENCED_NO_EXTERNAL_ACTIVATION",
    "LMAX_R79_PASS_TLS_RESULT_NOT_CLASSIFIED_AS_SUCCESS_NO_EXTERNAL_ACTIVATION",
    "LMAX_R79_PASS_FIX_PROGRESSION_BINDING_READY_NO_EXTERNAL_ACTIVATION",
    "LMAX_R79_FAIL_TLS_TO_FIX_PROGRESSION_ROOT_CAUSE_NOT_PROVABLE",
    "LMAX_R79_FAIL_FIX_PROVIDER_CLIENT_REVIEW_MISSING",
    "LMAX_R79_FAIL_TLS_TO_FIX_TRACE_MISSING",
    "LMAX_R79_FAIL_EXTERNAL_BOUNDARY_ATTEMPTED",
    "LMAX_R79_FAIL_TLS_FIX_OR_MARKETDATA_ATTEMPTED",
    "LMAX_R79_FAIL_FORBIDDEN_ACTION_INTRODUCED",
    "LMAX_R79_FAIL_CREDENTIAL_VALUES_RETURNED_OR_SECRET_RISK",
    "LMAX_R79_FAIL_API_WORKER_GATEWAY_REGRESSION",
    "LMAX_R79_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R79_FAIL_BUILD_OR_TESTS"
)

Assert-True ($allowedClassifications -contains $summary.classification) "R79 classification is absent or not allowed."
Assert-True ($summary.classification -eq "LMAX_R79_PASS_TLS_TO_FIX_PROGRESSION_ROOT_CAUSE_IDENTIFIED_NO_EXTERNAL_ACTIVATION") "R79 final classification mismatch."

Assert-True ($r78Gate.r78ControlledReadinessDecision -eq "READY_FOR_TLS_TO_FIX_PROGRESSION_ROOT_CAUSE") "R78 controlled readiness decision missing or mismatched."
Assert-True ($r78Gate.r78Classification -eq "LMAX_R78_PASS_RUNTIME_EVIDENCE_REVIEW_CONTROLLED_READINESS_GATE") "R78 success classification missing or mismatched."
Assert-True ($r78Gate.r78GateAcceptedForRootCauseReview -eq $true) "R78 gate was not accepted for R79 root-cause review."

Assert-True ($beforeAfter.before.classification -eq "LMAX_R77_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED") "R77 success classification missing or mismatched."
Assert-True ($beforeAfter.before.tcpSocket -eq "Succeeded") "R77 TCP success evidence missing."
Assert-True ($beforeAfter.before.tls -eq "Attempted") "R77 TLS attempt evidence missing."
Assert-True ($beforeAfter.before.fixBoundaryResultCategory -eq "NotAttemptedAfterTlsBoundaryAttempt") "R77 FIX NotAttemptedAfterTlsBoundaryAttempt evidence missing."

Assert-True ($tlsReview.tcpSocketSucceededBeforeTls -eq $true) "TCP success before TLS is not proven."
Assert-True ($tlsReview.tlsAttempted -eq $true) "TLS attempt evidence missing."
Assert-True ($tlsReview.socketConnectorAuthenticateTlsReached -eq $true) "socketConnector.AuthenticateTls evidence missing."
Assert-True ($tlsReview.tlsResultClassifiedAsAttemptedOnly -eq $true) "TLS attempted-only classification not represented."
Assert-False $tlsReview.tlsResultClassifiedAsSuccess "TLS is incorrectly represented as succeeded in R79."
Assert-False $tlsReview.fixLogonSessionAttemptedAfterTls "FIX logon/session was unexpectedly represented as attempted."

Assert-True ($rootCause.rootCauseIdentified -eq $true) "Root cause is not identified."
Assert-True ($rootCause.fixNotAttemptedAfterTlsBoundaryAttemptAcknowledged -eq $true) "FIX NotAttemptedAfterTlsBoundaryAttempt was not acknowledged."
Assert-True ($rootCause.responsibleRuntimeGuard -match "LmaxRealReadOnlyMarketDataTransport") "Responsible runtime guard not named."
Assert-True ($rootCause.responsibleFactoryBinding -match "LmaxReadOnlyActivationManualExecutionSurfaceFactory") "Responsible factory/binding not named."
Assert-True ($rootCause.responsibleMissingOperation -eq "LmaxReadOnlyFixSessionOperation") "Responsible missing operation not named."

Assert-True ($trace.trace.Count -ge 5) "TLS-to-FIX progression trace is missing or incomplete."
Assert-False $trace.tlsToFixContinuationExists "TLS-to-FIX continuation should remain missing in R79 root-cause pack."
Assert-False $trace.fixOperationBindingReady "FIX operation binding should not be marked ready in R79 root-cause pack."

Assert-True ($fixReview.fixProviderExists -eq $true) "FIX provider/client binding review missing provider evidence."
Assert-True ($fixReview.fixClientExists -eq $true) "FIX provider/client binding review missing client evidence."
Assert-True ($fixReview.fixSessionOperationTypeExists -eq $true) "FIX session operation type evidence missing."
Assert-False $fixReview.manualRealBoundedFactoryBindsConcreteFixSessionOperation "Manual real-bounded path should not claim concrete FIX operation binding in R79."
Assert-True ($fixReview.defaultUnconfiguredBehavior.category -eq "FixSessionOperationNotConfigured") "FIX default unconfigured behavior not represented."

Assert-True ($sequence.runtimeSequenceReviewed -eq $true) "Runtime sequence review missing."
Assert-True ($sequence.stopCondition -match "TLS") "Runtime sequence stop condition is not named."
Assert-False $sequence.nextRetryAllowedBeforeFix "R79 must not allow another retry before targeted FIX fix."

Assert-True ($realPath.manualCliSurfaceValidatedFromR77 -eq $true) "Manual CLI evidence missing from real bounded path validation."
Assert-True ($realPath.tcpSuccessValidatedFromR77 -eq $true) "TCP success missing from real bounded path validation."
Assert-True ($realPath.tlsAttemptValidatedFromR77 -eq $true) "TLS attempt missing from real bounded path validation."
Assert-False $realPath.apiWorkerReachable "Manual CLI became reachable from API/Worker/default startup."

Assert-False $noExternal.externalActivationAttempted "External activation attempted during R79."
Assert-False $noExternal.tcpSocketAttempted "TCP attempted during R79."
Assert-False $noExternal.tlsAttempted "TLS attempted during R79."
Assert-False $noExternal.fixLogonSessionAttempted "FIX attempted during R79."
Assert-False $noExternal.marketDataRequestAttempted "MarketDataRequest attempted during R79."

Assert-True ($forbidden.result -eq "PASS") "Forbidden actions audit did not pass."
Assert-False $forbidden.ordersSubmitted "Orders were submitted."
Assert-False $forbidden.orderPathTouched "Order/trading path was touched."
Assert-False $forbidden.tradingStateMutated "Trading state was mutated."
Assert-False $forbidden.productionAccountUsedOrAllowed "Production account/config was used or allowed."

Assert-True ($apiWorker.result -eq "PASS") "API/Worker audit did not pass."
Assert-True ($apiWorker.apiWorkerGatewayMode -eq "FakeLmaxGatewayOnly") "API/Worker gateway changed away from FakeLmaxGatewayOnly."
Assert-False $apiWorker.apiWorkerChanged "API/Worker changed during R79."
Assert-False $apiWorker.manualCliReachableFromApiWorkerDefaultStartup "Manual CLI became reachable from API/Worker/default startup."

Assert-True ($scheduler.result -eq "PASS") "Scheduler/polling/service audit did not pass."
Assert-False $scheduler.schedulerIntroduced "Scheduler introduced."
Assert-False $scheduler.pollingIntroduced "Polling introduced."
Assert-False $scheduler.hostedServiceIntroduced "Hosted service introduced."
Assert-False $scheduler.backgroundServiceIntroduced "Background service introduced."

Assert-True ($sanitize.result -eq "PASS") "Sanitization validation did not pass."
Assert-False $sanitize.credentialValuesReturned "credentialValuesReturned is not false."
Assert-False $sanitize.credentialValuesPrintedStoredSerialized "Credential values were printed/stored/serialized."
Assert-False $sanitize.rawEndpointValuesPrintedStoredSerialized "Raw endpoint values were printed/stored/serialized."
Assert-False $sanitize.rawTlsMaterialPrintedStoredSerialized "Raw TLS material was printed/stored/serialized."
Assert-False $sanitize.rawFixMessagesPrintedStoredSerialized "Raw FIX messages were printed/stored/serialized."

Assert-True ($usdJpy.result -eq "PASS") "USDJPY caveat validation did not pass."
Assert-True ($usdJpy.caveatPreserved -eq $true) "USDJPY caveat is missing or weakened."
Assert-False $usdJpy.weakened "USDJPY caveat was weakened."

Assert-True ($gate.rootCauseIdentified -eq $true) "Gate does not record root-cause identification."
Assert-True ($gate.responsibleClassFactoryBindingOperationNamed -eq $true) "Gate does not record responsible class/factory/binding."
Assert-True ($gate.tlsToFixProgressionTracePresent -eq $true) "Gate does not record TLS-to-FIX trace."
Assert-True ($gate.fixProviderClientBindingReviewPresent -eq $true) "Gate does not record FIX provider/client review."
Assert-True ($gate.buildResult.status -eq "PASS") "Build evidence is missing."
Assert-True ($gate.testResult.status -eq "PASS") "Test evidence is missing."
Assert-True ($next.nextRecommendedPhase -eq "LMAX-R80") "Next phase recommendation is absent or incorrect."
Assert-True ($next.nextPhaseMustBeNoExternal -eq $true) "R80 must be no-external."

$artifactText = Get-ChildItem $artifactRoot -Filter "phase-lmax-r79-*" -File |
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
        throw "Forbidden sensitive artifact pattern found in R79 artifacts: $pattern"
    }
}

Write-Output "LMAX_R79_VALIDATION_PASS"
