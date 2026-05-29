param(
    [string]$Root = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

$artifactRoot = Join-Path $Root "artifacts/readiness/lmax-runtime-enablement"
$gatePath = Join-Path $artifactRoot "phase-lmax-r53-gate-validation.json"
$approvalText = "I, Philippe, explicitly approve Phase LMAX-R53 for one temporary Demo read-only runtime market-data activation retry after the R52 credential/config source binding fix for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority."
$classification = "LMAX_R53_FAIL_PRE_EXTERNAL_APPROVAL_OR_COMPOSITION_REGRESSION"
$cause = "ApprovedBoundedExecutableRetryPhaseReservationMissingR53"
$usdJpyCaveat = "prior failed-safe root cause remains unproven"

$allowedClassifications = @(
    "LMAX_R53_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED",
    "LMAX_R53_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED",
    "LMAX_R53_FAIL_R52_SUCCESS_EVIDENCE_MISSING",
    "LMAX_R53_FAIL_PRE_EXTERNAL_APPROVAL_OR_COMPOSITION_REGRESSION",
    "LMAX_R53_FAIL_CREDENTIAL_CONFIG_MISSING",
    "LMAX_R53_FAIL_CREDENTIAL_CONFIG_NOT_APPROVED",
    "LMAX_R53_FAIL_CREDENTIAL_CONFIG_BINDING_REGRESSION",
    "LMAX_R53_FAIL_PRODUCTION_ACCOUNT_RISK",
    "LMAX_R53_FAIL_API_WORKER_GATEWAY_REGRESSION",
    "LMAX_R53_FAIL_LIVE_LAUNCHER_OR_BACKGROUND_SERVICE_RISK",
    "LMAX_R53_FAIL_FORBIDDEN_ACTION_RISK",
    "LMAX_R53_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R53_FAIL_PREFLIGHT_ABORTED",
    "LMAX_R53_FAIL_CREDENTIAL_CONFIG_BOUNDARY",
    "LMAX_R53_FAIL_TCP_SOCKET_BOUNDARY",
    "LMAX_R53_FAIL_TLS_BOUNDARY",
    "LMAX_R53_FAIL_FIX_LOGON_OR_SESSION_BOUNDARY",
    "LMAX_R53_FAIL_MARKETDATA_REQUEST_BOUNDARY",
    "LMAX_R53_FAIL_MARKETDATA_RESPONSE_BOUNDARY",
    "LMAX_R53_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE",
    "LMAX_R53_FAIL_SHUTDOWN_REVERT_INCOMPLETE",
    "LMAX_R53_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK",
    "LMAX_R53_FAIL_BUILD_OR_TESTS"
)

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Missing required file: $Path"
    }

    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Assert-True($Condition, [string]$Message) {
    if ($Condition -ne $true) {
        throw $Message
    }
}

function Assert-False($Value, [string]$Message) {
    if ($Value -ne $false) {
        throw $Message
    }
}

function Assert-Equal($Actual, $Expected, [string]$Message) {
    if ($Actual -ne $Expected) {
        throw "$Message Expected '$Expected' but got '$Actual'."
    }
}

$required = @(
    "phase-lmax-r53-temporary-readonly-activation-retry-report.md",
    "phase-lmax-r53-temporary-readonly-activation-retry-summary.json",
    "phase-lmax-r53-operator-approval-note.md",
    "phase-lmax-r53-preflight-result.json",
    "phase-lmax-r53-preflight-trace.json",
    "phase-lmax-r53-boundary-evidence.json",
    "phase-lmax-r53-marketdata-sanitized-result.json",
    "phase-lmax-r53-forbidden-actions-audit.json",
    "phase-lmax-r53-api-worker-fake-gateway-audit.json",
    "phase-lmax-r53-usdjpy-caveat-preservation.json",
    "phase-lmax-r53-shutdown-revert-evidence.json",
    "phase-lmax-r53-next-phase-recommendation.json",
    "phase-lmax-r53-gate-validation.json"
)

$checks = [ordered]@{}
foreach ($file in $required) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactRoot $file)) "Missing R53 artifact: $file"
}
$checks.requiredArtifactsExist = $true

$r52 = Read-Json (Join-Path $artifactRoot "phase-lmax-r52-gate-validation.json")
Assert-True $r52.passed "R52 gate validation did not pass."
Assert-Equal $r52.classification "LMAX_R52_PASS_CREDENTIAL_CONFIG_SOURCE_BINDING_FIXED_NO_EXTERNAL_ACTIVATION" "R52 classification"
Assert-False $r52.noApprovedR51CredentialConfigOperationBindingForSecretValueLoad "R52 credential binding blocker still true."
Assert-True $r52.approvedDemoReadOnlyCredentialConfigSourceBindingProvable "R52 credential/config binding not provable."
Assert-False $r52.credentialValuesReturned "R52 credentialValuesReturned must be false."
$checks.r52SuccessEvidencePresent = $true

$approvalNote = Get-Content -LiteralPath (Join-Path $artifactRoot "phase-lmax-r53-operator-approval-note.md") -Raw
Assert-True ($approvalNote.IndexOf($approvalText, [StringComparison]::Ordinal) -ge 0) "R53 exact operator approval text missing or mismatched."
$checks.operatorApprovalExact = $true

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r53-temporary-readonly-activation-retry-summary.json")
Assert-True ($allowedClassifications -contains [string]$summary.classification) "R53 classification not allowed: $($summary.classification)"
Assert-Equal $summary.classification $classification "R53 classification"
Assert-Equal $summary.concreteCause $cause "R53 concrete cause"
Assert-True $summary.r52SuccessEvidencePresent "R52 success evidence missing."
Assert-True $summary.r42AdapterExecutablePathValid "R42 gate regressed."
Assert-True $summary.r44BoundedRuntimeCompositionValid "R44 gate regressed."
Assert-True $summary.r46ExecutableBoundaryOperationCompositionValid "R46 gate regressed."
Assert-True $summary.r48ExternalBoundaryProviderExecutionCompositionValid "R48 gate regressed."
Assert-True $summary.r50PreExternalConsolidationValid "R50 gate regressed."
Assert-True $summary.r52CredentialConfigSourceBindingValid "R52 credential/config binding regressed."
Assert-True $summary.operatorApprovalExact "Operator approval not exact."
Assert-False $summary.r53ExplicitlyReserved "R53 should document missing reservation for this safe abort."
Assert-False $summary.preflightPassed "Preflight should fail for reservation blocker."
Assert-True $summary.abortedBeforeExternalAction "R53 did not abort before external action."
Assert-False $summary.externalActivationAttempted "External activation attempted."
Assert-False $summary.externalBoundaryAttempted "External boundary attempted."
Assert-Equal ([int]$summary.attemptCount) 0 "Attempt count"
Assert-False $summary.credentialValuesReturned "credentialValuesReturned must be false."
Assert-False $summary.credentialValuesPrinted "credentialValuesPrinted must be false."
Assert-False $summary.credentialValuesStored "credentialValuesStored must be false."
Assert-False $summary.credentialValuesSerialized "credentialValuesSerialized must be false."
Assert-False $summary.realCredentialValuesRead "realCredentialValuesRead must be false."
Assert-False $summary.productionAccountUsed "Production account used."
Assert-True $summary.outputSanitized "Output not sanitized."
Assert-True $summary.buildRepresented "Build evidence missing."
Assert-True $summary.testsRepresented "Test evidence missing."
Assert-True ([string]$summary.buildResult -like "PASS*") "Build result is not PASS."
Assert-True ([string]$summary.focusedTestResult -like "PASS*") "Focused test result is not PASS."
Assert-True ([string]$summary.fullTestResult -like "PASS*") "Full test result is not PASS."
$checks.summaryPassed = $true

$preflight = Read-Json (Join-Path $artifactRoot "phase-lmax-r53-preflight-result.json")
Assert-False $preflight.passed "Preflight unexpectedly passed."
Assert-True $preflight.abortedBeforeExternalAction "Preflight did not abort before external action."
Assert-Equal $preflight.failureCause $cause "Preflight failure cause"
Assert-True $preflight.operatorApprovalExact "Preflight approval not exact."
Assert-True $preflight.r52SuccessEvidencePresent "Preflight R52 evidence missing."
Assert-True $preflight.expectedR53Reservation "Preflight expected R53 reservation missing."
Assert-False $preflight.actualR53Reservation "Preflight actual R53 reservation should be false."
Assert-Equal $preflight.reservationClass "LmaxApprovedBoundedExecutableRetryPhaseReservations" "Reservation class"
Assert-Equal $preflight.reservationMethod "IsApproved" "Reservation method"
Assert-False $preflight.externalActivationAttempted "Preflight external activation attempted."
Assert-False $preflight.externalBoundaryAttempted "Preflight external boundary attempted."
Assert-Equal ([int]$preflight.attemptCount) 0 "Preflight attempt count"
Assert-False $preflight.credentialValuesReturned "Preflight credentialValuesReturned true."
Assert-False $preflight.realCredentialValuesRead "Preflight realCredentialValuesRead true."
Assert-True $preflight.approvedInstrumentsExact "Preflight approved instrument mismatch."
Assert-True $preflight.usdJpyCaveatPreserved "Preflight USDJPY caveat missing."
$checks.preflightPassed = $true

$trace = Read-Json (Join-Path $artifactRoot "phase-lmax-r53-preflight-trace.json")
Assert-Equal $trace.classification $classification "Trace classification"
Assert-Equal $trace.concreteCause $cause "Trace concrete cause"
Assert-False $trace.externalActivationAttempted "Trace external activation attempted."
Assert-False $trace.externalBoundaryAttempted "Trace external boundary attempted."
Assert-Equal ([int]$trace.attemptCount) 0 "Trace attempt count"
$failed = @($trace.trace | Where-Object { $_.result -eq "FAIL" })
Assert-True ($failed.Count -eq 1) "Expected exactly one failed trace gate."
Assert-Equal $failed[0].blocker $cause "Trace blocker"
Assert-Equal $failed[0].class "LmaxApprovedBoundedExecutableRetryPhaseReservations" "Trace failure class"
Assert-Equal $failed[0].method "IsApproved" "Trace failure method"
foreach ($gate in @("R52 success evidence","R53 operator approval","R42 concrete adapter executable path","R44 bounded runtime activation composition","R46 executable boundary operation composition","R48 external boundary provider execution composition","R50 final pre-external consolidation","R52 credential/config source binding")) {
    $entry = @($trace.trace | Where-Object { $_.gate -eq $gate })
    Assert-True ($entry.Count -eq 1) "Missing trace gate: $gate"
    Assert-Equal $entry[0].result "PASS" "Trace result for $gate"
}
$checks.preflightTracePassed = $true

$boundary = Read-Json (Join-Path $artifactRoot "phase-lmax-r53-boundary-evidence.json")
Assert-False $boundary.externalActivationAttempted "Boundary evidence external activation attempted."
Assert-False $boundary.externalBoundaryAttempted "Boundary evidence external boundary attempted."
Assert-Equal ([int]$boundary.attemptCount) 0 "Boundary attempt count"
foreach ($field in @("credentialConfig","tcpSocket","tls","fixLogonSession","marketDataRequest","marketDataResponseEntries")) {
    Assert-Equal $boundary.$field.status "NotAttempted" "Boundary status for $field"
}
Assert-Equal $boundary.shutdownRevert.status "Succeeded" "Shutdown/revert status"
$checks.boundaryEvidencePassed = $true

$marketData = Read-Json (Join-Path $artifactRoot "phase-lmax-r53-marketdata-sanitized-result.json")
Assert-True $marketData.approvedInstrumentsOnly "Market data contains unapproved instruments."
Assert-False $marketData.credentialValuesReturned "Market data credentialValuesReturned true."
Assert-True $marketData.outputSanitized "Market data output not sanitized."
Assert-False $marketData.marketDataRequestSent "MarketDataRequest sent."
Assert-False $marketData.entriesObserved "Market data entries observed despite no attempt."
$checks.marketDataPassed = $true

$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r53-forbidden-actions-audit.json")
Assert-True $forbidden.passed "Forbidden action audit failed."
foreach ($field in @(
    "orderSubmissionExecuted",
    "newOrderSingleTouched",
    "cancelReplaceTouched",
    "orderPathIntroduced",
    "orderPathTouched",
    "tradingEnablementExecuted",
    "tradingStateMutated",
    "productionAccountUsed",
    "apiStarted",
    "workerStarted",
    "hostedServiceAdded",
    "backgroundServiceAdded",
    "schedulerStarted",
    "pollingStarted",
    "replayExecuted",
    "shadowReplayExecuted",
    "shadowReplaySubmitted",
    "credentialValuesReturned",
    "credentialPrinted",
    "credentialStored",
    "credentialSerialized",
    "rawSensitiveFixLogsStored",
    "nonApprovedInstrumentTouched",
    "apiWorkerGatewayChanged",
    "externalActivationAttempted",
    "externalBoundaryAttempted"
)) {
    Assert-False $forbidden.$field "Forbidden action flag was true: $field"
}
$checks.forbiddenActionsPassed = $true

$api = Read-Json (Join-Path $artifactRoot "phase-lmax-r53-api-worker-fake-gateway-audit.json")
Assert-True $api.passed "API/Worker fake gateway audit failed."
Assert-Equal $api.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "API/Worker gateway mode"
Assert-False $api.defaultGatewayRegistrationChanged "Default gateway registration changed."
Assert-False $api.apiStarted "API started."
Assert-False $api.workerStarted "Worker started."
Assert-False $api.appsettingsLiveEnablementIntroduced "Appsettings live enablement introduced."
Assert-True $api.requireFakeExecutionGateway "RequireFakeExecutionGateway changed."
Assert-False $api.allowExternalConnections "AllowExternalConnections changed."
$checks.apiWorkerFakeGatewayPassed = $true

$usd = Read-Json (Join-Path $artifactRoot "phase-lmax-r53-usdjpy-caveat-preservation.json")
Assert-True $usd.passed "USDJPY caveat validation failed."
Assert-Equal $usd.securityId "4004" "USDJPY SecurityID"
Assert-Equal $usd.securityIdSource "8" "USDJPY SecurityIDSource"
Assert-Equal $usd.lineage "USDJPY_T7_FINAL_READINESS_ARCHIVE_CLOSED_WITH_CAVEAT" "USDJPY lineage"
Assert-Equal $usd.caveat $usdJpyCaveat "USDJPY caveat"
Assert-True $usd.caveatPreserved "USDJPY caveat not preserved."
Assert-False $usd.caveatWeakened "USDJPY caveat weakened."
$checks.usdJpyCaveatPassed = $true

$shutdown = Read-Json (Join-Path $artifactRoot "phase-lmax-r53-shutdown-revert-evidence.json")
Assert-True $shutdown.present "Shutdown/revert evidence missing."
Assert-True $shutdown.completed "Shutdown/revert not completed."
Assert-False $shutdown.runtimeStarted "Runtime started."
Assert-False $shutdown.runtimeEnablementPersisted "Runtime enablement persisted."
Assert-False $shutdown.defaultGatewayRegistrationChanged "Default gateway registration changed."
$checks.shutdownRevertPassed = $true

$next = Read-Json (Join-Path $artifactRoot "phase-lmax-r53-next-phase-recommendation.json")
Assert-True ([string]$next.recommendedNextPhase -like "Phase LMAX-R54*") "Missing R54 recommendation."
Assert-True $next.r54MustNotPerformActivation "R54 must be non-activation."
$checks.nextPhasePassed = $true

$reservationSource = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxApprovedBoundedExecutableRetryPhaseReservations.cs") -Raw
Assert-True ($reservationSource -match '"LMAX-R51"') "Reservation source missing R51 baseline."
Assert-True ($reservationSource -notmatch '"LMAX-R53"') "R53 reservation unexpectedly present; R53 safe-abort evidence would be stale."
$checks.reservationSourceMatchesBlocker = $true

$apiProgram = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/Program.cs") -Raw
$workerPath = Join-Path $Root "src/QQ.Production.Intraday.Worker/Program.cs"
$workerProgram = if (Test-Path -LiteralPath $workerPath) { Get-Content -LiteralPath $workerPath -Raw } else { "" }
$appsettings = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/appsettings.json") -Raw
Assert-True ($apiProgram -match "AddSingleton<IVenueExecutionGateway,\s*FakeLmaxGateway>") "API default gateway is not FakeLmaxGateway."
Assert-True ($appsettings -match '"RequireFakeExecutionGateway"\s*:\s*true') "RequireFakeExecutionGateway changed."
Assert-True ($appsettings -match '"AllowExternalConnections"\s*:\s*false') "AllowExternalConnections default changed."
foreach ($token in @("phase-lmax-r53", "ApprovedBoundedExecutableReadOnly", "LmaxTemporaryReadOnlyActivationExecutor")) {
    Assert-True ($apiProgram -notmatch [regex]::Escape($token)) "R53 API wiring detected: $token"
    Assert-True ($workerProgram -notmatch [regex]::Escape($token)) "R53 Worker wiring detected: $token"
    Assert-True ($appsettings -notmatch [regex]::Escape($token)) "R53 appsettings wiring detected: $token"
}
$checks.startupSafetyPassed = $true

$artifactText = (Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r53-*" -File | ForEach-Object {
    Get-Content -LiteralPath $_.FullName -Raw
}) -join "`n"
foreach ($envName in @("LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD", "LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID")) {
    $value = [Environment]::GetEnvironmentVariable($envName)
    if (-not [string]::IsNullOrWhiteSpace($value)) {
        Assert-True ($artifactText -notlike "*$value*") "R53 artifacts contain raw sensitive value for $envName."
    }
}
Assert-True ($artifactText -notmatch "554=") "R53 artifacts contain raw FIX password tag."
Assert-True ($artifactText -notmatch "35=D|35=F|35=H|35=AE|35=8") "R53 artifacts contain forbidden raw sensitive FIX message type."
$checks.artifactSanitizationPassed = $true

$gate = [ordered]@{
    phase = "LMAX-R53"
    validatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    validator = "scripts/check-lmax-r53-temporary-readonly-activation-retry-gate.ps1"
    passed = $true
    classification = $classification
    concreteCause = $cause
    externalActivationAttempted = $false
    attemptCount = 0
    credentialValuesReturned = $false
    checks = $checks
}

$gate | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $gatePath -Encoding UTF8
Write-Host "LMAX R53 gate validation passed: $gatePath"
