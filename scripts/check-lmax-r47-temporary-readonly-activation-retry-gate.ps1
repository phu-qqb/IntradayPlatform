param(
    [string]$Root = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

$artifactRoot = Join-Path $Root "artifacts/readiness/lmax-runtime-enablement"
$gatePath = Join-Path $artifactRoot "phase-lmax-r47-gate-validation.json"
$approvalText = "I, Philippe, explicitly approve Phase LMAX-R47 for one temporary Demo read-only runtime market-data activation retry after the R46 executable boundary operation composition fix for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority."
$r46Decision = "LMAX_R46_PASS_EXECUTABLE_BOUNDARY_OPERATION_COMPOSITION_FIXED_NO_EXTERNAL_ACTIVATION"
$usdJpyCaveat = "prior failed-safe root cause remains unproven"

$allowedClassifications = @(
    "LMAX_R47_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED",
    "LMAX_R47_FAIL_OPERATOR_APPROVAL_MISSING_OR_MISMATCHED",
    "LMAX_R47_FAIL_R46_SUCCESS_EVIDENCE_MISSING",
    "LMAX_R47_FAIL_R45_BLOCKER_STILL_PRESENT",
    "LMAX_R47_FAIL_R44_BOUNDED_RUNTIME_COMPOSITION_REGRESSION",
    "LMAX_R47_FAIL_R42_CONCRETE_ADAPTER_REGRESSION",
    "LMAX_R47_FAIL_CONCRETE_ADAPTER_STILL_DRYRUN_ONLY",
    "LMAX_R47_FAIL_BOUNDED_EXECUTOR_VALIDATION_FAILED",
    "LMAX_R47_FAIL_EXECUTABLE_BOUNDARY_OPERATION_COMPOSITION_MISSING",
    "LMAX_R47_FAIL_RUNTIME_DELEGATE_BINDING_REGRESSION",
    "LMAX_R47_FAIL_CREDENTIAL_CONFIG_MISSING",
    "LMAX_R47_FAIL_CREDENTIAL_CONFIG_NOT_APPROVED",
    "LMAX_R47_FAIL_PRODUCTION_ACCOUNT_RISK",
    "LMAX_R47_FAIL_API_WORKER_GATEWAY_REGRESSION",
    "LMAX_R47_FAIL_LIVE_LAUNCHER_OR_BACKGROUND_SERVICE_RISK",
    "LMAX_R47_FAIL_FORBIDDEN_ACTION_RISK",
    "LMAX_R47_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R47_FAIL_PREFLIGHT_ABORTED",
    "LMAX_R47_FAIL_CREDENTIAL_CONFIG_BOUNDARY",
    "LMAX_R47_FAIL_TCP_SOCKET_BOUNDARY",
    "LMAX_R47_FAIL_TLS_BOUNDARY",
    "LMAX_R47_FAIL_FIX_LOGON_OR_SESSION_BOUNDARY",
    "LMAX_R47_FAIL_MARKETDATA_REQUEST_BOUNDARY",
    "LMAX_R47_FAIL_MARKETDATA_RESPONSE_BOUNDARY",
    "LMAX_R47_FAIL_RUNTIME_BOUNDARY_INCONCLUSIVE",
    "LMAX_R47_FAIL_SHUTDOWN_REVERT_INCOMPLETE",
    "LMAX_R47_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK",
    "LMAX_R47_FAIL_BUILD_OR_TESTS"
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
    "phase-lmax-r47-temporary-readonly-activation-retry-report.md",
    "phase-lmax-r47-temporary-readonly-activation-retry-summary.json",
    "phase-lmax-r47-operator-approval-note.md",
    "phase-lmax-r47-preflight-result.json",
    "phase-lmax-r47-bounded-executor-validation.json",
    "phase-lmax-r47-concrete-bounded-runtime-composition-validation.json",
    "phase-lmax-r47-executable-boundary-operation-composition-validation.json",
    "phase-lmax-r47-runtime-delegate-path-validation.json",
    "phase-lmax-r47-boundary-evidence.json",
    "phase-lmax-r47-marketdata-sanitized-result.json",
    "phase-lmax-r47-forbidden-actions-audit.json",
    "phase-lmax-r47-api-worker-fake-gateway-audit.json",
    "phase-lmax-r47-no-live-launcher-audit.json",
    "phase-lmax-r47-usdjpy-caveat-preservation.json",
    "phase-lmax-r47-shutdown-revert-evidence.json",
    "phase-lmax-r47-next-phase-recommendation.json",
    "phase-lmax-r47-gate-validation.json"
)

$checks = [ordered]@{}
foreach ($file in $required) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactRoot $file)) "Missing R47 artifact: $file"
}
$checks.requiredArtifactsExist = $true

$r46 = Read-Json (Join-Path $artifactRoot "phase-lmax-r46-executable-boundary-operation-composition-summary.json")
Assert-Equal $r46.classification $r46Decision "R46 classification"
Assert-False $r46.noApprovedR45ExecutableBoundaryOperationComposition "R45 blocker remains in R46 evidence."
Assert-True $r46.executableBoundaryOperationCompositionExplicit "R46 boundary operation composition not explicit."
$checks.r46SuccessEvidencePresent = $true

$approvalNote = Get-Content -LiteralPath (Join-Path $artifactRoot "phase-lmax-r47-operator-approval-note.md") -Raw
Assert-True ($approvalNote.IndexOf($approvalText, [StringComparison]::Ordinal) -ge 0) "R47 exact operator approval text missing or mismatched."
$checks.operatorApprovalExact = $true

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r47-temporary-readonly-activation-retry-summary.json")
Assert-True ($allowedClassifications -contains [string]$summary.classification) "R47 classification not allowed: $($summary.classification)"
Assert-False $summary.noApprovedR45ExecutableBoundaryOperationComposition "NoApprovedR45... remains true."
Assert-True $summary.r44BoundedRuntimeCompositionProvable "R44 bounded runtime composition not provable."
Assert-True $summary.r42ConcreteAdapterExecutablePathValid "R42 concrete adapter executable path regressed."
Assert-False $summary.concreteAdapterStillDryRunOnly "ConcreteAdapterStillDryRunOnly remains true."
Assert-Equal $summary.adapterMode "ApprovedBoundedExecutableReadOnly" "AdapterMode"
Assert-True $summary.boundedExecutorApproved "BoundedExecutorApproved"
Assert-True $summary.runtimeDelegateBindingApproved "RuntimeDelegateBindingApproved"
Assert-True $summary.executableBoundaryOperationCompositionProvable "Executable boundary operation composition not provable."
Assert-True ([int]$summary.attemptCount -le 1) "More than one activation attempt occurred."
Assert-False $summary.productionAccountUsed "Production account used."
Assert-False $summary.credentialValuesReturned "credentialValuesReturned must be false."
Assert-True $summary.outputSanitized "Output not sanitized."
Assert-True $summary.buildRepresented "Build evidence missing."
Assert-True $summary.testsRepresented "Test evidence missing."
$checks.summaryPassed = $true

$preflight = Read-Json (Join-Path $artifactRoot "phase-lmax-r47-preflight-result.json")
Assert-Equal $preflight.r46Classification $r46Decision "Preflight R46 classification"
Assert-True $preflight.operatorApprovalExact "Preflight operator approval exact"
Assert-False $preflight.noApprovedR45ExecutableBoundaryOperationComposition "Preflight R45 blocker remains."
Assert-True $preflight.r44BoundedRuntimeCompositionProvable "Preflight R44 composition proof missing."
Assert-True $preflight.r42ConcreteAdapterExecutablePathValid "Preflight R42 adapter proof missing."
Assert-False $preflight.concreteAdapterStillDryRunOnly "Preflight concrete adapter still dry-run-only."
Assert-Equal $preflight.adapterMode "ApprovedBoundedExecutableReadOnly" "Preflight adapter mode"
Assert-True $preflight.boundedExecutorApproved "Preflight bounded executor approval"
Assert-True $preflight.runtimeDelegateBindingApproved "Preflight delegate approval"
Assert-True $preflight.executableBoundaryOperationCompositionProvable "Preflight boundary operation composition proof missing"
Assert-True $preflight.approvedInstrumentsExact "Preflight instrument scope"
Assert-True $preflight.usdJpyCaveatPreserved "Preflight USDJPY caveat"
Assert-False $preflight.productionAccountUsed "Preflight production account"
Assert-False $preflight.externalBoundaryAttempted "Preflight attempted external boundary"
Assert-True ($preflight.passed -eq $true -or ($preflight.abortedBeforeExternalAction -eq $true -and $preflight.externalBoundaryAttempted -eq $false)) "Preflight neither passed nor aborted safely."
$checks.preflightPassedOrSafelyAborted = $true

$bounded = Read-Json (Join-Path $artifactRoot "phase-lmax-r47-bounded-executor-validation.json")
Assert-True ([int]$bounded.maxAttemptCount -eq 1) "maxAttemptCount must be one."
Assert-True ([int]$bounded.retryCount -eq 0) "retryCount must be zero."
Assert-False $bounded.batchMode "batchMode was enabled."
Assert-False $bounded.loopMode "loopMode was enabled."
Assert-False $bounded.externalActionExecuted "Bounded executor validation executed external action."
if ($summary.classification -eq "LMAX_R47_FAIL_BOUNDED_EXECUTOR_VALIDATION_FAILED") {
    Assert-False $bounded.passed "Bounded validation should record failure for bounded-executor classification."
}
$checks.boundedExecutorValidationChecked = $true

$boundedComposition = Read-Json (Join-Path $artifactRoot "phase-lmax-r47-concrete-bounded-runtime-composition-validation.json")
Assert-True $boundedComposition.passed "Concrete bounded runtime composition validation failed."
Assert-True $boundedComposition.explicitAndProvable "Concrete bounded runtime composition not explicit/provable."
Assert-False $boundedComposition.noApprovedR43BoundedExecutableRuntimeActivationComposition "R43 blocker remains in bounded composition validation."
$checks.concreteBoundedCompositionChecked = $true

$operationComposition = Read-Json (Join-Path $artifactRoot "phase-lmax-r47-executable-boundary-operation-composition-validation.json")
Assert-True $operationComposition.passed "Executable boundary operation composition validation failed."
Assert-True $operationComposition.explicitAndProvable "Executable boundary operation composition not explicit/provable."
Assert-False $operationComposition.noApprovedR45ExecutableBoundaryOperationComposition "R45 blocker remains in operation composition validation."
Assert-False $operationComposition.externalBoundaryAttempted "Operation composition validation attempted external boundary."
$checks.boundaryOperationCompositionChecked = $true

$delegatePath = Read-Json (Join-Path $artifactRoot "phase-lmax-r47-runtime-delegate-path-validation.json")
foreach ($field in @("providerCompletenessPass","clientCompletenessPass","operationCompletenessPass","coreCompositionPass","runtimeDelegateBindingCompletenessPass","runtimeDelegatePathProvable")) {
    Assert-True $delegatePath.$field "Delegate path regression: $field"
}
Assert-False $delegatePath.externalActionExecuted "Delegate path validation executed external action."
$checks.runtimeDelegatePathChecked = $true

$boundary = Read-Json (Join-Path $artifactRoot "phase-lmax-r47-boundary-evidence.json")
foreach ($field in @("credentialConfig","tcpSocket","tls","fixLogonSession","marketDataRequest","marketDataResponseEntries","shutdownRevert")) {
    Assert-True (@("NotAttempted","Attempted","Succeeded","FailedSafe","FailedExternal","FailedValidation","Aborted") -contains [string]$boundary.$field.status) "Invalid boundary status for $field."
}
if ($summary.externalActivationAttempted -eq $false) {
    foreach ($field in @("credentialConfig","tcpSocket","tls","fixLogonSession","marketDataRequest","marketDataResponseEntries")) {
        Assert-Equal $boundary.$field.status "NotAttempted" "Boundary status for $field"
    }
}
$checks.boundaryEvidenceChecked = $true

$marketData = Read-Json (Join-Path $artifactRoot "phase-lmax-r47-marketdata-sanitized-result.json")
Assert-False $marketData.credentialValuesReturned "MarketData credentialValuesReturned must be false."
Assert-True $marketData.outputSanitized "MarketData output was not sanitized."
Assert-True $marketData.approvedInstrumentsOnly "MarketData result contains unapproved instruments."
$checks.marketDataResultChecked = $true

$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r47-forbidden-actions-audit.json")
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

$api = Read-Json (Join-Path $artifactRoot "phase-lmax-r47-api-worker-fake-gateway-audit.json")
Assert-True $api.passed "API/Worker fake gateway audit failed."
Assert-Equal $api.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "API/Worker gateway mode"
Assert-False $api.defaultGatewayRegistrationChanged "Default gateway registration changed."
$checks.apiWorkerFakeGatewayPassed = $true

$launcher = Read-Json (Join-Path $artifactRoot "phase-lmax-r47-no-live-launcher-audit.json")
Assert-True $launcher.passed "No-live-launcher audit failed."
foreach ($field in @("liveLauncherCreated","consoleAppCreated","scriptCreated","hostedServiceAdded","backgroundServiceAdded","schedulerAdded","pollingAdded","apiEndpointAdded")) {
    Assert-False $launcher.$field "Launcher/service flag was true: $field"
}
$checks.noLiveLauncherPassed = $true

$usd = Read-Json (Join-Path $artifactRoot "phase-lmax-r47-usdjpy-caveat-preservation.json")
Assert-True $usd.passed "USDJPY caveat preservation failed."
Assert-Equal $usd.securityId "4004" "USDJPY SecurityID"
Assert-Equal $usd.securityIdSource "8" "USDJPY SecurityIDSource"
Assert-Equal $usd.caveat $usdJpyCaveat "USDJPY caveat"
Assert-False $usd.caveatWeakened "USDJPY caveat weakened."
$checks.usdJpyCaveatPassed = $true

$shutdown = Read-Json (Join-Path $artifactRoot "phase-lmax-r47-shutdown-revert-evidence.json")
Assert-True $shutdown.present "Shutdown/revert evidence missing."
Assert-True $shutdown.completed "Shutdown/revert not completed."
Assert-False $shutdown.runtimeEnablementPersisted "Runtime enablement persisted."
$checks.shutdownRevertPassed = $true

$next = Read-Json (Join-Path $artifactRoot "phase-lmax-r47-next-phase-recommendation.json")
Assert-True ([string]$next.recommendedNextPhase -like "Phase LMAX-R48*") "Missing R48 recommendation."
Assert-False $next.r48Executed "R48 was executed."
$checks.nextPhasePassed = $true

$artifactText = (Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r47-*" -File | ForEach-Object {
    Get-Content -LiteralPath $_.FullName -Raw
}) -join "`n"
foreach ($envName in @("LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD", "LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID")) {
    $value = [Environment]::GetEnvironmentVariable($envName)
    if (-not [string]::IsNullOrWhiteSpace($value)) {
        Assert-True ($artifactText -notlike "*$value*") "R47 artifacts contain raw sensitive value for $envName."
    }
}
Assert-True ($artifactText -notmatch "554=") "R47 artifacts contain raw FIX password tag."
Assert-True ($artifactText -notmatch "35=D|35=F|35=H|35=AE|35=8") "R47 artifacts contain forbidden raw sensitive FIX message type."
$checks.sanitizationPassed = $true

$apiProgram = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/Program.cs") -Raw
$workerPath = Join-Path $Root "src/QQ.Production.Intraday.Worker/Program.cs"
$workerProgram = if (Test-Path -LiteralPath $workerPath) { Get-Content -LiteralPath $workerPath -Raw } else { "" }
$appsettings = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/appsettings.json") -Raw
Assert-True ($apiProgram -match "AddSingleton<IVenueExecutionGateway,\s*FakeLmaxGateway>") "API default gateway is not FakeLmaxGateway."
Assert-True ($appsettings -match '"RequireFakeExecutionGateway"\s*:\s*true') "RequireFakeExecutionGateway changed."
Assert-True ($appsettings -match '"AllowExternalConnections"\s*:\s*false') "AllowExternalConnections default changed."
foreach ($token in @("ApprovedBoundedExecutableReadOnly", "phase-lmax-r47", "LmaxTemporaryReadOnlyActivationExecutor")) {
    Assert-True ($apiProgram -notmatch [regex]::Escape($token)) "R47 API wiring detected: $token"
    Assert-True ($workerProgram -notmatch [regex]::Escape($token)) "R47 Worker wiring detected: $token"
    Assert-True ($appsettings -notmatch [regex]::Escape($token)) "R47 appsettings wiring detected: $token"
}
$checks.startupSafetyPassed = $true

$gate = [ordered]@{
    phase = "LMAX-R47"
    validatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    validator = "scripts/check-lmax-r47-temporary-readonly-activation-retry-gate.ps1"
    passed = $true
    classification = $summary.classification
    externalActivationAttempted = $summary.externalActivationAttempted
    attemptCount = $summary.attemptCount
    checks = $checks
}

$gate | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $gatePath -Encoding UTF8
Write-Host "LMAX R47 gate validation passed: $gatePath"
