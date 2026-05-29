param(
    [string]$Root = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

$artifactRoot = Join-Path $Root "artifacts/readiness/lmax-runtime-enablement"
$gatePath = Join-Path $artifactRoot "phase-lmax-r45-gate-validation.json"
$approvalText = "I, Philippe, explicitly approve Phase LMAX-R45 for one temporary Demo read-only runtime market-data activation retry after the R44 concrete bounded runtime composition fix for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority."
$r44Decision = "LMAX_R44_PASS_CONCRETE_BOUNDED_RUNTIME_ACTIVATION_COMPOSITION_FIXED_NO_EXTERNAL_ACTIVATION"
$usdJpyCaveat = "prior failed-safe root cause remains unproven"

$allowedClassifications = @(
    "LMAX_R45_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED",
    "LMAX_R45_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED",
    "LMAX_R45_FAIL_R44_SUCCESS_EVIDENCE_MISSING",
    "LMAX_R45_FAIL_R43_BLOCKER_STILL_PRESENT",
    "LMAX_R45_FAIL_CONCRETE_ADAPTER_STILL_DRYRUN_ONLY",
    "LMAX_R45_FAIL_BOUNDED_EXECUTOR_VALIDATION_FAILED",
    "LMAX_R45_FAIL_CONCRETE_BOUNDED_RUNTIME_COMPOSITION_MISSING",
    "LMAX_R45_FAIL_RUNTIME_DELEGATE_BINDING_REGRESSION",
    "LMAX_R45_FAIL_CREDENTIAL_CONFIG_MISSING",
    "LMAX_R45_FAIL_CREDENTIAL_CONFIG_NOT_APPROVED",
    "LMAX_R45_FAIL_PRODUCTION_ACCOUNT_RISK",
    "LMAX_R45_FAIL_API_WORKER_GATEWAY_REGRESSION",
    "LMAX_R45_FAIL_LIVE_LAUNCHER_OR_BACKGROUND_SERVICE_RISK",
    "LMAX_R45_FAIL_FORBIDDEN_ACTION_RISK",
    "LMAX_R45_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R45_FAIL_PREFLIGHT_ABORTED",
    "LMAX_R45_FAIL_TCP_SOCKET_BOUNDARY",
    "LMAX_R45_FAIL_TLS_BOUNDARY",
    "LMAX_R45_FAIL_FIX_LOGON_OR_SESSION_BOUNDARY",
    "LMAX_R45_FAIL_MARKETDATA_REQUEST_BOUNDARY",
    "LMAX_R45_FAIL_MARKETDATA_RESPONSE_BOUNDARY",
    "LMAX_R45_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE",
    "LMAX_R45_FAIL_SHUTDOWN_REVERT_INCOMPLETE",
    "LMAX_R45_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK",
    "LMAX_R45_FAIL_BUILD_OR_TESTS"
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
    "phase-lmax-r45-temporary-readonly-activation-retry-report.md",
    "phase-lmax-r45-temporary-readonly-activation-retry-summary.json",
    "phase-lmax-r45-operator-approval-note.md",
    "phase-lmax-r45-preflight-result.json",
    "phase-lmax-r45-bounded-executor-validation.json",
    "phase-lmax-r45-concrete-bounded-runtime-composition-validation.json",
    "phase-lmax-r45-runtime-delegate-path-validation.json",
    "phase-lmax-r45-boundary-evidence.json",
    "phase-lmax-r45-marketdata-sanitized-result.json",
    "phase-lmax-r45-forbidden-actions-audit.json",
    "phase-lmax-r45-api-worker-fake-gateway-audit.json",
    "phase-lmax-r45-no-live-launcher-audit.json",
    "phase-lmax-r45-usdjpy-caveat-preservation.json",
    "phase-lmax-r45-shutdown-revert-evidence.json",
    "phase-lmax-r45-next-phase-recommendation.json",
    "phase-lmax-r45-gate-validation.json"
)

$checks = [ordered]@{}
foreach ($file in $required) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactRoot $file)) "Missing R45 artifact: $file"
}
$checks.requiredArtifactsExist = $true

$r44 = Read-Json (Join-Path $artifactRoot "phase-lmax-r44-concrete-bounded-runtime-activation-composition-summary.json")
Assert-Equal $r44.classification $r44Decision "R44 classification"
Assert-False $r44.noApprovedR43BoundedExecutableRuntimeActivationComposition "R43 blocker remains in R44 evidence."
Assert-True $r44.boundedExecutableRuntimeActivationCompositionExplicit "R44 composition not explicit."
$checks.r44SuccessEvidencePresent = $true

$approvalNote = Get-Content -LiteralPath (Join-Path $artifactRoot "phase-lmax-r45-operator-approval-note.md") -Raw
Assert-True ($approvalNote.IndexOf($approvalText, [StringComparison]::Ordinal) -ge 0) "R45 exact operator approval text missing or mismatched."
$checks.operatorApprovalExact = $true

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r45-temporary-readonly-activation-retry-summary.json")
Assert-True ($allowedClassifications -contains [string]$summary.classification) "R45 classification not allowed: $($summary.classification)"
Assert-False $summary.noApprovedR43BoundedExecutableRuntimeActivationComposition "NoApprovedR43... remains true."
Assert-False $summary.concreteAdapterStillDryRunOnly "ConcreteAdapterStillDryRunOnly remains true."
Assert-Equal $summary.adapterMode "ApprovedBoundedExecutableReadOnly" "AdapterMode"
Assert-True $summary.boundedExecutorApproved "BoundedExecutorApproved"
Assert-True $summary.runtimeDelegateBindingApproved "RuntimeDelegateBindingApproved"
Assert-True $summary.concreteBoundedRuntimeActivationCompositionProvable "Concrete bounded composition not provable."
Assert-True ([int]$summary.attemptCount -le 1) "More than one activation attempt occurred."
Assert-False $summary.productionAccountUsed "Production account used."
Assert-False $summary.credentialValuesReturned "credentialValuesReturned must be false."
Assert-True $summary.outputSanitized "Output not sanitized."
Assert-True $summary.buildRepresented "Build evidence missing."
Assert-True $summary.testsRepresented "Test evidence missing."
$checks.summaryPassed = $true

$preflight = Read-Json (Join-Path $artifactRoot "phase-lmax-r45-preflight-result.json")
Assert-Equal $preflight.r44Classification $r44Decision "Preflight R44 classification"
Assert-True $preflight.operatorApprovalExact "Preflight operator approval exact"
Assert-False $preflight.noApprovedR43BoundedExecutableRuntimeActivationComposition "Preflight R43 blocker remains."
Assert-False $preflight.concreteAdapterStillDryRunOnly "Preflight concrete adapter still dry-run-only."
Assert-Equal $preflight.adapterMode "ApprovedBoundedExecutableReadOnly" "Preflight adapter mode"
Assert-True $preflight.boundedExecutorApproved "Preflight bounded executor approval"
Assert-True $preflight.runtimeDelegateBindingApproved "Preflight delegate approval"
Assert-True $preflight.concreteBoundedRuntimeActivationCompositionProvable "Preflight composition proof missing"
Assert-True $preflight.approvedInstrumentsExact "Preflight instrument scope"
Assert-True $preflight.usdJpyCaveatPreserved "Preflight USDJPY caveat"
Assert-False $preflight.productionAccountUsed "Preflight production account"
Assert-False $preflight.externalBoundaryAttempted "Preflight attempted external boundary"
Assert-True ($preflight.passed -eq $true -or ($preflight.abortedBeforeExternalAction -eq $true -and $preflight.externalBoundaryAttempted -eq $false)) "Preflight neither passed nor aborted safely."
$checks.preflightPassedOrSafelyAborted = $true

$bounded = Read-Json (Join-Path $artifactRoot "phase-lmax-r45-bounded-executor-validation.json")
Assert-True ([int]$bounded.maxAttemptCount -eq 1) "maxAttemptCount must be one."
Assert-True ([int]$bounded.retryCount -eq 0) "retryCount must be zero."
Assert-False $bounded.batchMode "batchMode was enabled."
Assert-False $bounded.loopMode "loopMode was enabled."
Assert-False $bounded.externalActionExecuted "Bounded executor validation executed external action."
if ($summary.classification -eq "LMAX_R45_FAIL_BOUNDED_EXECUTOR_VALIDATION_FAILED") {
    Assert-False $bounded.passed "Bounded validation should record failure for bounded-executor classification."
}
$checks.boundedExecutorValidationChecked = $true

$composition = Read-Json (Join-Path $artifactRoot "phase-lmax-r45-concrete-bounded-runtime-composition-validation.json")
Assert-True $composition.passed "Concrete bounded runtime composition validation failed."
Assert-True $composition.explicitAndProvable "Concrete bounded runtime composition not explicit/provable."
Assert-False $composition.noApprovedR43BoundedExecutableRuntimeActivationComposition "R43 blocker remains in composition validation."
Assert-False $composition.externalBoundaryAttempted "Composition validation attempted external boundary."
$checks.concreteCompositionChecked = $true

$delegatePath = Read-Json (Join-Path $artifactRoot "phase-lmax-r45-runtime-delegate-path-validation.json")
foreach ($field in @("providerCompletenessPass","clientCompletenessPass","operationCompletenessPass","coreCompositionPass","runtimeDelegateBindingCompletenessPass","runtimeDelegatePathProvable")) {
    Assert-True $delegatePath.$field "Delegate path regression: $field"
}
Assert-False $delegatePath.externalActionExecuted "Delegate path validation executed external action."
$checks.runtimeDelegatePathChecked = $true

$boundary = Read-Json (Join-Path $artifactRoot "phase-lmax-r45-boundary-evidence.json")
foreach ($field in @("tcpSocket","tls","fixLogonSession","marketDataRequest","marketDataResponseEntries","shutdownRevert")) {
    Assert-True (@("NotAttempted","Attempted","Succeeded","FailedSafe","FailedExternal","FailedValidation","Aborted") -contains [string]$boundary.$field.status) "Invalid boundary status for $field."
}
if ($summary.externalActivationAttempted -eq $false) {
    Assert-Equal $boundary.tcpSocket.status "NotAttempted" "TCP status"
    Assert-Equal $boundary.tls.status "NotAttempted" "TLS status"
    Assert-Equal $boundary.fixLogonSession.status "NotAttempted" "FIX status"
    Assert-Equal $boundary.marketDataRequest.status "NotAttempted" "MarketDataRequest status"
    Assert-Equal $boundary.marketDataResponseEntries.status "NotAttempted" "MarketDataResponse status"
}
$checks.boundaryEvidenceChecked = $true

$marketData = Read-Json (Join-Path $artifactRoot "phase-lmax-r45-marketdata-sanitized-result.json")
Assert-False $marketData.credentialValuesReturned "MarketData credentialValuesReturned must be false."
Assert-True $marketData.outputSanitized "MarketData output was not sanitized."
Assert-True $marketData.approvedInstrumentsOnly "MarketData result contains unapproved instruments."
$checks.marketDataResultChecked = $true

$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r45-forbidden-actions-audit.json")
foreach ($field in @(
    "orderSubmissionExecuted",
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
    "credentialPrinted",
    "credentialStored",
    "rawSensitiveFixLogsStored",
    "runtimeEnablementPersisted"
)) {
    Assert-False $forbidden.$field "Forbidden action flag was true: $field"
}
Assert-True $forbidden.passed "Forbidden action audit failed."
$checks.forbiddenActionsPassed = $true

$api = Read-Json (Join-Path $artifactRoot "phase-lmax-r45-api-worker-fake-gateway-audit.json")
Assert-True $api.passed "API/Worker fake gateway audit failed."
Assert-Equal $api.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "API/Worker gateway mode"
Assert-False $api.defaultGatewayRegistrationChanged "Default gateway registration changed."
$checks.apiWorkerFakeGatewayPassed = $true

$launcher = Read-Json (Join-Path $artifactRoot "phase-lmax-r45-no-live-launcher-audit.json")
Assert-True $launcher.passed "No-live-launcher audit failed."
foreach ($field in @("liveLauncherCreated","consoleAppCreated","scriptCreated","hostedServiceAdded","backgroundServiceAdded","schedulerAdded","pollingAdded","apiEndpointAdded")) {
    Assert-False $launcher.$field "Launcher/service flag was true: $field"
}
$checks.noLiveLauncherPassed = $true

$usd = Read-Json (Join-Path $artifactRoot "phase-lmax-r45-usdjpy-caveat-preservation.json")
Assert-True $usd.passed "USDJPY caveat preservation failed."
Assert-Equal $usd.securityId "4004" "USDJPY SecurityID"
Assert-Equal $usd.securityIdSource "8" "USDJPY SecurityIDSource"
Assert-Equal $usd.caveat $usdJpyCaveat "USDJPY caveat"
Assert-False $usd.caveatWeakened "USDJPY caveat weakened."
$checks.usdJpyCaveatPassed = $true

$shutdown = Read-Json (Join-Path $artifactRoot "phase-lmax-r45-shutdown-revert-evidence.json")
Assert-True $shutdown.present "Shutdown/revert evidence missing."
Assert-True $shutdown.completed "Shutdown/revert not completed."
Assert-False $shutdown.runtimeEnablementPersisted "Runtime enablement persisted."
$checks.shutdownRevertPassed = $true

$next = Read-Json (Join-Path $artifactRoot "phase-lmax-r45-next-phase-recommendation.json")
Assert-True ([string]$next.recommendedNextPhase -like "Phase LMAX-R46*") "Missing R46 recommendation."
Assert-False $next.r46Executed "R46 was executed."
$checks.nextPhasePassed = $true

$artifactText = (Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r45-*" -File | ForEach-Object {
    Get-Content -LiteralPath $_.FullName -Raw
}) -join "`n"
foreach ($envName in @("LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD", "LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID")) {
    $value = [Environment]::GetEnvironmentVariable($envName)
    if (-not [string]::IsNullOrWhiteSpace($value)) {
        Assert-True ($artifactText -notlike "*$value*") "R45 artifacts contain raw sensitive value for $envName."
    }
}
Assert-True ($artifactText -notmatch "554=") "R45 artifacts contain raw FIX password tag."
Assert-True ($artifactText -notmatch "35=D|35=F|35=H|35=AE|35=8") "R45 artifacts contain forbidden raw sensitive FIX message type."
$checks.sanitizationPassed = $true

$apiProgram = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/Program.cs") -Raw
$workerPath = Join-Path $Root "src/QQ.Production.Intraday.Worker/Program.cs"
$workerProgram = if (Test-Path -LiteralPath $workerPath) { Get-Content -LiteralPath $workerPath -Raw } else { "" }
$appsettings = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/appsettings.json") -Raw
Assert-True ($apiProgram -match "AddSingleton<IVenueExecutionGateway,\s*FakeLmaxGateway>") "API default gateway is not FakeLmaxGateway."
Assert-True ($appsettings -match '"RequireFakeExecutionGateway"\s*:\s*true') "RequireFakeExecutionGateway changed."
Assert-True ($appsettings -match '"AllowExternalConnections"\s*:\s*false') "AllowExternalConnections default changed."
foreach ($token in @("ApprovedBoundedExecutableReadOnly", "phase-lmax-r45", "LmaxTemporaryReadOnlyActivationExecutor")) {
    Assert-True ($apiProgram -notmatch [regex]::Escape($token)) "R45 API wiring detected: $token"
    Assert-True ($workerProgram -notmatch [regex]::Escape($token)) "R45 Worker wiring detected: $token"
    Assert-True ($appsettings -notmatch [regex]::Escape($token)) "R45 appsettings wiring detected: $token"
}
$checks.startupSafetyPassed = $true

$gate = [ordered]@{
    phase = "LMAX-R45"
    validatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    validator = "scripts/check-lmax-r45-temporary-readonly-activation-retry-gate.ps1"
    passed = $true
    classification = $summary.classification
    externalActivationAttempted = $summary.externalActivationAttempted
    attemptCount = $summary.attemptCount
    checks = $checks
}

$gate | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $gatePath -Encoding UTF8
Write-Host "LMAX R45 gate validation passed: $gatePath"
