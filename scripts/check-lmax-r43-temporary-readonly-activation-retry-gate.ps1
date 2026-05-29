param(
    [string]$Root = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

$artifactRoot = Join-Path $Root "artifacts/readiness/lmax-runtime-enablement"
$gatePath = Join-Path $artifactRoot "phase-lmax-r43-gate-validation.json"
$approvalText = "I, Philippe, explicitly approve Phase LMAX-R43 for one temporary Demo read-only runtime market-data activation retry after the R42 concrete adapter fix for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority."
$r42Decision = "LMAX_R42_PASS_CONCRETE_FINAL_PRE_EXECUTION_BLOCKER_FIXED_NO_EXTERNAL_ACTIVATION"
$usdJpyCaveat = "prior failed-safe root cause remains unproven"

$allowedClassifications = @(
    "LMAX_R43_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED",
    "LMAX_R43_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED",
    "LMAX_R43_FAIL_R42_SUCCESS_EVIDENCE_MISSING",
    "LMAX_R43_FAIL_CONCRETE_ADAPTER_STILL_DRYRUN_ONLY",
    "LMAX_R43_FAIL_BOUNDED_EXECUTOR_VALIDATION_FAILED",
    "LMAX_R43_FAIL_RUNTIME_DELEGATE_BINDING_REGRESSION",
    "LMAX_R43_FAIL_CREDENTIAL_CONFIG_MISSING",
    "LMAX_R43_FAIL_CREDENTIAL_CONFIG_NOT_APPROVED",
    "LMAX_R43_FAIL_PRODUCTION_ACCOUNT_RISK",
    "LMAX_R43_FAIL_API_WORKER_GATEWAY_REGRESSION",
    "LMAX_R43_FAIL_LIVE_LAUNCHER_OR_BACKGROUND_SERVICE_RISK",
    "LMAX_R43_FAIL_FORBIDDEN_ACTION_RISK",
    "LMAX_R43_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R43_FAIL_PREFLIGHT_ABORTED",
    "LMAX_R43_FAIL_TCP_SOCKET_BOUNDARY",
    "LMAX_R43_FAIL_TLS_BOUNDARY",
    "LMAX_R43_FAIL_FIX_LOGON_OR_SESSION_BOUNDARY",
    "LMAX_R43_FAIL_MARKETDATA_REQUEST_BOUNDARY",
    "LMAX_R43_FAIL_MARKETDATA_RESPONSE_BOUNDARY",
    "LMAX_R43_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE",
    "LMAX_R43_FAIL_SHUTDOWN_REVERT_INCOMPLETE",
    "LMAX_R43_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK",
    "LMAX_R43_FAIL_BUILD_OR_TESTS"
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
    "phase-lmax-r43-temporary-readonly-activation-retry-report.md",
    "phase-lmax-r43-temporary-readonly-activation-retry-summary.json",
    "phase-lmax-r43-operator-approval-note.md",
    "phase-lmax-r43-preflight-result.json",
    "phase-lmax-r43-bounded-executor-validation.json",
    "phase-lmax-r43-runtime-delegate-path-validation.json",
    "phase-lmax-r43-boundary-evidence.json",
    "phase-lmax-r43-marketdata-sanitized-result.json",
    "phase-lmax-r43-forbidden-actions-audit.json",
    "phase-lmax-r43-api-worker-fake-gateway-audit.json",
    "phase-lmax-r43-no-live-launcher-audit.json",
    "phase-lmax-r43-usdjpy-caveat-preservation.json",
    "phase-lmax-r43-shutdown-revert-evidence.json",
    "phase-lmax-r43-next-phase-recommendation.json",
    "phase-lmax-r43-gate-validation.json"
)

$checks = [ordered]@{}
foreach ($file in $required) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactRoot $file)) "Missing R43 artifact: $file"
}
$checks.requiredArtifactsExist = $true

$r42 = Read-Json (Join-Path $artifactRoot "phase-lmax-r42-concrete-adapter-blocker-fix-summary.json")
Assert-Equal $r42.classification $r42Decision "R42 classification"
Assert-True $r42.concreteAdapterStillDryRunOnlyClearedForApprovedBoundedPath "R42 did not clear ConcreteAdapterStillDryRunOnly."
$checks.r42SuccessEvidencePresent = $true

$approvalNote = Get-Content -LiteralPath (Join-Path $artifactRoot "phase-lmax-r43-operator-approval-note.md") -Raw
Assert-True ($approvalNote.IndexOf($approvalText, [StringComparison]::Ordinal) -ge 0) "R43 exact operator approval text missing or mismatched."
$checks.operatorApprovalExact = $true

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r43-temporary-readonly-activation-retry-summary.json")
Assert-True ($allowedClassifications -contains [string]$summary.classification) "R43 classification not allowed: $($summary.classification)"
Assert-Equal $summary.adapterMode "ApprovedBoundedExecutableReadOnly" "AdapterMode"
Assert-True $summary.boundedExecutorApproved "BoundedExecutorApproved"
Assert-True $summary.runtimeDelegateBindingApproved "RuntimeDelegateBindingApproved"
Assert-False $summary.concreteAdapterStillDryRunOnly "ConcreteAdapterStillDryRunOnly remains active."
Assert-True ([int]$summary.attemptCount -le 1) "More than one activation attempt occurred."
Assert-False $summary.productionAccountUsed "Production account used."
Assert-False $summary.credentialValuesReturned "credentialValuesReturned must be false."
Assert-True $summary.shutdownRevertEvidencePresent "Shutdown/revert evidence missing from summary."
Assert-True $summary.buildRepresented "Build result is not represented."
Assert-True $summary.testsRepresented "Test result is not represented."
$checks.summaryPassed = $true

$preflight = Read-Json (Join-Path $artifactRoot "phase-lmax-r43-preflight-result.json")
Assert-Equal $preflight.r42Classification $r42Decision "Preflight R42 classification"
Assert-True $preflight.operatorApprovalExact "Preflight operator approval exact"
Assert-Equal $preflight.adapterMode "ApprovedBoundedExecutableReadOnly" "Preflight adapter mode"
Assert-True $preflight.boundedExecutorApproved "Preflight bounded executor approval"
Assert-True $preflight.runtimeDelegateBindingApproved "Preflight delegate approval"
Assert-True $preflight.approvedInstrumentsExact "Preflight instrument scope"
Assert-True $preflight.usdJpyCaveatPreserved "Preflight USDJPY caveat"
Assert-False $preflight.productionAccountUsed "Preflight production account"
Assert-False $preflight.externalBoundaryAttempted "Preflight attempted external boundary"
$checks.preflightPassedOrSafelyAborted = ($preflight.passed -eq $true -or ($preflight.abortedBeforeExternalAction -eq $true -and $preflight.externalBoundaryAttempted -eq $false))
Assert-True $checks.preflightPassedOrSafelyAborted "Preflight neither passed nor aborted safely."

$bounded = Read-Json (Join-Path $artifactRoot "phase-lmax-r43-bounded-executor-validation.json")
Assert-True ([int]$bounded.maxAttemptCount -eq 1) "maxAttemptCount must be one."
Assert-True ([int]$bounded.retryCount -eq 0) "retryCount must be zero."
Assert-False $bounded.batchMode "batchMode was enabled."
Assert-False $bounded.loopMode "loopMode was enabled."
Assert-False $bounded.externalActionExecuted "Bounded executor validation executed external action."
if ($summary.classification -eq "LMAX_R43_FAIL_BOUNDED_EXECUTOR_VALIDATION_FAILED") {
    Assert-False $bounded.passed "Bounded validation should record failure for bounded-executor classification."
}
$checks.boundedExecutorValidationChecked = $true

$delegatePath = Read-Json (Join-Path $artifactRoot "phase-lmax-r43-runtime-delegate-path-validation.json")
foreach ($field in @("providerCompletenessPass","clientCompletenessPass","operationCompletenessPass","coreCompositionPass","runtimeDelegateBindingCompletenessPass")) {
    Assert-True $delegatePath.$field "Delegate path regression: $field"
}
Assert-False $delegatePath.externalActionExecuted "Delegate path validation executed external action."
$checks.runtimeDelegatePathChecked = $true

$boundary = Read-Json (Join-Path $artifactRoot "phase-lmax-r43-boundary-evidence.json")
foreach ($field in @("tcpSocket","tls","fixLogonSession","marketDataRequest","marketDataResponseEntries","shutdownRevert")) {
    Assert-True (@("NotAttempted","Attempted","Succeeded","FailedSafe","FailedExternal","FailedValidation","Aborted") -contains [string]$boundary.$field.status) "Invalid boundary status for $field."
}
if ($summary.externalActivationAttempted -eq $false) {
    Assert-Equal $boundary.tcpSocket.status "NotAttempted" "TCP status"
    Assert-Equal $boundary.tls.status "NotAttempted" "TLS status"
    Assert-Equal $boundary.fixLogonSession.status "NotAttempted" "FIX status"
    Assert-Equal $boundary.marketDataRequest.status "NotAttempted" "MarketDataRequest status"
}
$checks.boundaryEvidenceChecked = $true

$marketData = Read-Json (Join-Path $artifactRoot "phase-lmax-r43-marketdata-sanitized-result.json")
Assert-False $marketData.credentialValuesReturned "MarketData credentialValuesReturned must be false."
Assert-True $marketData.outputSanitized "MarketData output was not sanitized."
Assert-True $marketData.approvedInstrumentsOnly "MarketData result contains unapproved instruments."
$checks.marketDataResultChecked = $true

$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r43-forbidden-actions-audit.json")
foreach ($field in @(
    "orderSubmissionExecuted",
    "orderPathTouched",
    "tradingEnabled",
    "tradingStateMutated",
    "productionAccountUsed",
    "apiStarted",
    "workerStarted",
    "hostedServiceAdded",
    "backgroundServiceAdded",
    "schedulerStarted",
    "pollingStarted",
    "replayExecuted",
    "shadowReplaySubmitted",
    "credentialsPrinted",
    "credentialsStored",
    "rawSensitiveFixLogsStored"
)) {
    Assert-False $forbidden.$field "Forbidden action flag was true: $field"
}
Assert-True $forbidden.passed "Forbidden action audit failed."
$checks.forbiddenActionsPassed = $true

$api = Read-Json (Join-Path $artifactRoot "phase-lmax-r43-api-worker-fake-gateway-audit.json")
Assert-True $api.passed "API/Worker fake gateway audit failed."
Assert-Equal $api.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "API/Worker gateway mode"
Assert-False $api.defaultGatewayRegistrationChanged "Default gateway registration changed."
$checks.apiWorkerFakeGatewayPassed = $true

$launcher = Read-Json (Join-Path $artifactRoot "phase-lmax-r43-no-live-launcher-audit.json")
Assert-True $launcher.passed "No-live-launcher audit failed."
Assert-False $launcher.liveLauncherCreated "Live launcher created."
Assert-False $launcher.hostedServiceAdded "Hosted service added."
Assert-False $launcher.backgroundServiceAdded "Background service added."
Assert-False $launcher.schedulerOrPollingIntroduced "Scheduler/polling introduced."
$checks.noLiveLauncherPassed = $true

$usd = Read-Json (Join-Path $artifactRoot "phase-lmax-r43-usdjpy-caveat-preservation.json")
Assert-True $usd.passed "USDJPY caveat preservation failed."
Assert-Equal $usd.securityId "4004" "USDJPY SecurityID"
Assert-Equal $usd.securityIdSource "8" "USDJPY SecurityIDSource"
Assert-Equal $usd.caveat $usdJpyCaveat "USDJPY caveat"
Assert-False $usd.caveatWeakened "USDJPY caveat weakened."
$checks.usdJpyCaveatPassed = $true

$shutdown = Read-Json (Join-Path $artifactRoot "phase-lmax-r43-shutdown-revert-evidence.json")
Assert-True $shutdown.present "Shutdown/revert evidence missing."
Assert-True $shutdown.completed "Shutdown/revert not completed."
Assert-False $shutdown.runtimeEnablementPersisted "Runtime enablement persisted."
$checks.shutdownRevertPassed = $true

$next = Read-Json (Join-Path $artifactRoot "phase-lmax-r43-next-phase-recommendation.json")
Assert-False $next.r44Executed "R44 was executed."
Assert-True ([string]$next.recommendedNextPhase -like "Phase LMAX-R44*") "Missing R44 recommendation."
$checks.nextPhasePassed = $true

$artifactText = (Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r43-*" -File | ForEach-Object {
    Get-Content -LiteralPath $_.FullName -Raw
}) -join "`n"
foreach ($envName in @("LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD", "LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID")) {
    $value = [Environment]::GetEnvironmentVariable($envName)
    if (-not [string]::IsNullOrWhiteSpace($value)) {
        Assert-True ($artifactText -notlike "*$value*") "R43 artifacts contain raw sensitive value for $envName."
    }
}
Assert-True ($artifactText -notmatch "554=") "R43 artifacts contain raw FIX password tag."
Assert-True ($artifactText -notmatch "35=D|35=F|35=H|35=AE|35=8") "R43 artifacts contain forbidden raw sensitive FIX message type."
$checks.sanitizationPassed = $true

$apiProgram = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/Program.cs") -Raw
$workerPath = Join-Path $Root "src/QQ.Production.Intraday.Worker/Program.cs"
$workerProgram = if (Test-Path -LiteralPath $workerPath) { Get-Content -LiteralPath $workerPath -Raw } else { "" }
$appsettings = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/appsettings.json") -Raw
Assert-True ($apiProgram -match "AddSingleton<IVenueExecutionGateway,\s*FakeLmaxGateway>") "API default gateway is not FakeLmaxGateway."
Assert-True ($appsettings -match '"RequireFakeExecutionGateway"\s*:\s*true') "RequireFakeExecutionGateway changed."
Assert-True ($appsettings -match '"AllowExternalConnections"\s*:\s*false') "AllowExternalConnections default changed."
foreach ($token in @("LmaxTemporaryReadOnlyActivationExecutor", "ApprovedBoundedExecutableReadOnly", "phase-lmax-r43")) {
    Assert-True ($apiProgram -notmatch [regex]::Escape($token)) "R43 API wiring detected: $token"
    Assert-True ($workerProgram -notmatch [regex]::Escape($token)) "R43 Worker wiring detected: $token"
    Assert-True ($appsettings -notmatch [regex]::Escape($token)) "R43 appsettings wiring detected: $token"
}
$checks.startupSafetyPassed = $true

$gate = [ordered]@{
    phase = "LMAX-R43"
    validatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    validator = "scripts/check-lmax-r43-temporary-readonly-activation-retry-gate.ps1"
    passed = $true
    classification = $summary.classification
    externalActivationAttempted = $summary.externalActivationAttempted
    attemptCount = $summary.attemptCount
    checks = $checks
}

$gate | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $gatePath -Encoding UTF8
Write-Host "LMAX R43 gate validation passed: $gatePath"
