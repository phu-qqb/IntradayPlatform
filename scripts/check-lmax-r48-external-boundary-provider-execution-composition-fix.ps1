param(
    [string]$Root = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

$artifactRoot = Join-Path $Root "artifacts/readiness/lmax-runtime-enablement"
$gatePath = Join-Path $artifactRoot "phase-lmax-r48-gate-validation.json"
$classification = "LMAX_R48_PASS_EXTERNAL_BOUNDARY_PROVIDER_EXECUTION_COMPOSITION_FIXED_NO_EXTERNAL_ACTIVATION"
$r46Decision = "LMAX_R46_PASS_EXECUTABLE_BOUNDARY_OPERATION_COMPOSITION_FIXED_NO_EXTERNAL_ACTIVATION"
$r47Decision = "LMAX_R47_FAIL_BOUNDED_EXECUTOR_VALIDATION_FAILED"
$r47Blocker = "NoApprovedR47ExternalBoundaryProviderExecutionComposition"
$usdJpyCaveat = "prior failed-safe root cause remains unproven"

$allowedClassifications = @(
    "LMAX_R48_PASS_EXTERNAL_BOUNDARY_PROVIDER_EXECUTION_COMPOSITION_FIXED_NO_EXTERNAL_ACTIVATION",
    "LMAX_R48_FAIL_R47_BLOCKER_STILL_PRESENT",
    "LMAX_R48_FAIL_PROVIDER_EXECUTION_COMPOSITION_NOT_PROVABLE",
    "LMAX_R48_FAIL_BOUNDARY_OPERATION_COMPOSITION_REGRESSION",
    "LMAX_R48_FAIL_BOUNDED_RUNTIME_COMPOSITION_REGRESSION",
    "LMAX_R48_FAIL_RUNTIME_DELEGATE_BINDING_REGRESSION",
    "LMAX_R48_FAIL_APPROVAL_GATES_WEAKENED",
    "LMAX_R48_FAIL_API_WORKER_GATEWAY_REGRESSION",
    "LMAX_R48_FAIL_LIVE_LAUNCHER_OR_BACKGROUND_SERVICE_INTRODUCED",
    "LMAX_R48_FAIL_FORBIDDEN_ACTION_INTRODUCED",
    "LMAX_R48_FAIL_EXTERNAL_BOUNDARY_ATTEMPTED",
    "LMAX_R48_FAIL_CREDENTIAL_VALUES_RETURNED_OR_SECRET_RISK",
    "LMAX_R48_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R48_FAIL_BUILD_OR_TESTS"
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
    "phase-lmax-r48-external-boundary-provider-execution-composition-fix-report.md",
    "phase-lmax-r48-external-boundary-provider-execution-composition-summary.json",
    "phase-lmax-r48-r47-blocker-before-after-classification.json",
    "phase-lmax-r48-provider-execution-composition-validation.json",
    "phase-lmax-r48-executable-boundary-operation-composition-validation.json",
    "phase-lmax-r48-bounded-executor-validation.json",
    "phase-lmax-r48-runtime-delegate-path-validation.json",
    "phase-lmax-r48-approved-instrument-scope-validation.json",
    "phase-lmax-r48-forbidden-actions-audit.json",
    "phase-lmax-r48-api-worker-fake-gateway-audit.json",
    "phase-lmax-r48-no-live-launcher-audit.json",
    "phase-lmax-r48-no-external-boundary-attempted.json",
    "phase-lmax-r48-usdjpy-caveat-preservation.json",
    "phase-lmax-r48-next-phase-recommendation.json",
    "phase-lmax-r48-gate-validation.json"
)

$checks = [ordered]@{}
foreach ($file in $required) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactRoot $file)) "Missing R48 artifact: $file"
}
$checks.requiredArtifactsExist = $true

$r46 = Read-Json (Join-Path $artifactRoot "phase-lmax-r46-executable-boundary-operation-composition-summary.json")
Assert-Equal $r46.classification $r46Decision "R46 classification"
Assert-False $r46.noApprovedR45ExecutableBoundaryOperationComposition "R46 operation composition blocker remains."
$checks.r46SuccessEvidencePresent = $true

$r47 = Read-Json (Join-Path $artifactRoot "phase-lmax-r47-temporary-readonly-activation-retry-summary.json")
Assert-Equal $r47.classification $r47Decision "R47 classification"
Assert-Equal $r47.concreteBlocker $r47Blocker "R47 concrete blocker"
Assert-False $r47.externalActivationAttempted "R47 unexpectedly attempted activation."
$checks.r47BlockerEvidencePresent = $true

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r48-external-boundary-provider-execution-composition-summary.json")
Assert-True ($allowedClassifications -contains [string]$summary.classification) "R48 classification not allowed: $($summary.classification)"
Assert-Equal $summary.classification $classification "R48 classification"
Assert-False $summary.noApprovedR47ExternalBoundaryProviderExecutionComposition "R47 blocker remains true."
Assert-True $summary.externalBoundaryProviderExecutionCompositionExplicit "Provider execution composition not explicit/provable."
Assert-True $summary.providerExecutionCompositionApproved "Provider execution composition approval missing."
Assert-True $summary.r44BoundedRuntimeCompositionProvable "R44 bounded runtime composition not provable."
Assert-True $summary.r46ExecutableBoundaryOperationCompositionProvable "R46 boundary operation composition not provable."
Assert-True $summary.r42ConcreteAdapterExecutablePathValid "R42 concrete adapter executable path regressed."
Assert-False $summary.concreteAdapterStillDryRunOnly "Concrete adapter still dry-run-only."
Assert-Equal $summary.adapterMode "ApprovedBoundedExecutableReadOnly" "AdapterMode"
Assert-True $summary.boundedExecutorApproved "BoundedExecutorApproved"
Assert-True $summary.runtimeDelegateBindingApproved "RuntimeDelegateBindingApproved"
Assert-False $summary.externalActivationAttempted "R48 attempted activation."
Assert-False $summary.externalBoundaryAttempted "R48 attempted external boundary."
Assert-False $summary.realCredentialValuesRead "R48 read credential values."
Assert-False $summary.credentialValuesReturned "credentialValuesReturned must be false."
Assert-True $summary.credentialConfigSanitizedValidationOnly "Credential/config proof must be sanitized validation only."
Assert-True $summary.outputSanitized "Output not sanitized."
Assert-True $summary.buildRepresented "Build evidence missing."
Assert-True $summary.testsRepresented "Test evidence missing."
$checks.summaryPassed = $true

$beforeAfter = Read-Json (Join-Path $artifactRoot "phase-lmax-r48-r47-blocker-before-after-classification.json")
Assert-Equal $beforeAfter.before.blocker $r47Blocker "Before blocker"
Assert-True $beforeAfter.before.present "Before blocker should be present."
Assert-Equal $beforeAfter.after.blocker $r47Blocker "After blocker"
Assert-False $beforeAfter.after.present "After blocker should be cleared."
$checks.beforeAfterPassed = $true

$provider = Read-Json (Join-Path $artifactRoot "phase-lmax-r48-provider-execution-composition-validation.json")
Assert-True $provider.passed "Provider execution composition validation failed."
Assert-False $provider.noApprovedR47ExternalBoundaryProviderExecutionComposition "Provider validation still has R47 blocker."
foreach ($field in @(
    "externalBoundaryProviderExecutionCompositionExplicit",
    "providerExecutionCompositionApproved",
    "credentialConfigProviderClientPresent",
    "tcpSocketProviderClientPresent",
    "tlsProviderClientPresent",
    "fixFrameSessionProviderClientPresent",
    "marketDataFrameRequestProviderClientPresent",
    "marketDataResponseEntryCaptureProviderClientPresent",
    "shutdownRevertProviderClientPresent",
    "credentialConfigSanitizedValidationOnly"
)) {
    Assert-True $provider.$field "Provider validation flag missing: $field"
}
Assert-False $provider.realCredentialValuesRead "Provider validation read credentials."
Assert-False $provider.credentialValuesReturned "Provider validation returned credentials."
Assert-False $provider.externalBoundaryAttempted "Provider validation attempted external boundary."
$checks.providerCompositionPassed = $true

$operation = Read-Json (Join-Path $artifactRoot "phase-lmax-r48-executable-boundary-operation-composition-validation.json")
Assert-True $operation.passed "R46 executable boundary operation composition regressed."
Assert-False $operation.noApprovedR45ExecutableBoundaryOperationComposition "R45 operation blocker returned."
$checks.operationCompositionPassed = $true

$bounded = Read-Json (Join-Path $artifactRoot "phase-lmax-r48-bounded-executor-validation.json")
Assert-True $bounded.passed "Bounded executor validation failed."
Assert-False $bounded.noApprovedR47ExternalBoundaryProviderExecutionComposition "Bounded executor still sees R47 blocker."
Assert-True ([int]$bounded.maxAttemptCount -eq 1) "maxAttemptCount must be one."
Assert-True ([int]$bounded.retryCount -eq 0) "retryCount must be zero."
Assert-False $bounded.batchMode "batchMode enabled."
Assert-False $bounded.loopMode "loopMode enabled."
Assert-False $bounded.externalActionExecuted "Bounded validation executed external action."
$checks.boundedExecutorPassed = $true

$delegatePath = Read-Json (Join-Path $artifactRoot "phase-lmax-r48-runtime-delegate-path-validation.json")
foreach ($field in @("providerCompletenessPass","clientCompletenessPass","operationCompletenessPass","coreCompositionPass","runtimeDelegateBindingCompletenessPass","runtimeDelegatePathProvable")) {
    Assert-True $delegatePath.$field "Delegate path regression: $field"
}
Assert-False $delegatePath.externalActionExecuted "Delegate path validation executed external action."
$checks.runtimeDelegatePathPassed = $true

$scope = Read-Json (Join-Path $artifactRoot "phase-lmax-r48-approved-instrument-scope-validation.json")
Assert-True $scope.passed "Approved instrument scope validation failed."
Assert-True $scope.approvedInstrumentsExact "Approved instruments mismatch."
Assert-True $scope.usdJpyCaveatPreserved "USDJPY caveat missing."
$symbols = @($scope.approvedInstruments | ForEach-Object { $_.symbol } | Sort-Object)
Assert-True (($symbols -join ",") -eq "AUDUSD,EURGBP,GBPUSD,USDJPY") "Approved instrument list mismatch."
$checks.instrumentScopePassed = $true

$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r48-forbidden-actions-audit.json")
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
    "credentialValuesRead",
    "credentialValuesReturned",
    "credentialPrinted",
    "credentialStored",
    "rawSensitiveFixLogsStored",
    "runtimeEnablementPersisted"
)) {
    Assert-False $forbidden.$field "Forbidden action flag was true: $field"
}
Assert-True $forbidden.passed "Forbidden action audit failed."
$checks.forbiddenActionsPassed = $true

$api = Read-Json (Join-Path $artifactRoot "phase-lmax-r48-api-worker-fake-gateway-audit.json")
Assert-True $api.passed "API/Worker fake gateway audit failed."
Assert-Equal $api.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "API/Worker gateway mode"
Assert-False $api.defaultGatewayRegistrationChanged "Default gateway registration changed."
$checks.apiWorkerFakeGatewayPassed = $true

$launcher = Read-Json (Join-Path $artifactRoot "phase-lmax-r48-no-live-launcher-audit.json")
Assert-True $launcher.passed "No-live-launcher audit failed."
foreach ($field in @("liveLauncherCreated","consoleAppCreated","scriptCreated","hostedServiceAdded","backgroundServiceAdded","schedulerAdded","pollingAdded","apiEndpointAdded")) {
    Assert-False $launcher.$field "Launcher/service flag was true: $field"
}
$checks.noLiveLauncherPassed = $true

$boundary = Read-Json (Join-Path $artifactRoot "phase-lmax-r48-no-external-boundary-attempted.json")
Assert-True $boundary.passed "External boundary audit failed."
foreach ($field in @("credentialConfig","tcpSocket","tls","fixLogonSession","marketDataRequest","marketDataResponseEntries")) {
    Assert-Equal $boundary.boundaryStatuses.$field "NotAttempted" "Boundary status for $field"
}
Assert-True (@("Succeeded", "NotRequired") -contains [string]$boundary.boundaryStatuses.shutdownRevert) "Shutdown/revert status invalid."
$checks.noExternalBoundaryPassed = $true

$usd = Read-Json (Join-Path $artifactRoot "phase-lmax-r48-usdjpy-caveat-preservation.json")
Assert-True $usd.passed "USDJPY caveat preservation failed."
Assert-Equal $usd.securityId "4004" "USDJPY SecurityID"
Assert-Equal $usd.securityIdSource "8" "USDJPY SecurityIDSource"
Assert-Equal $usd.caveat $usdJpyCaveat "USDJPY caveat"
Assert-False $usd.caveatWeakened "USDJPY caveat weakened."
$checks.usdJpyCaveatPassed = $true

$next = Read-Json (Join-Path $artifactRoot "phase-lmax-r48-next-phase-recommendation.json")
Assert-True ([string]$next.recommendedNextPhase -like "Phase LMAX-R49*") "Missing R49 recommendation."
Assert-False $next.r49Executed "R49 was executed."
$checks.nextPhasePassed = $true

$source = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxExternalBoundaryProviderExecutionComposition.cs") -Raw
foreach ($token in @(
    "LmaxExternalBoundaryProviderExecutionComposition",
    "NoApprovedR47ExternalBoundaryProviderExecutionComposition",
    "ExternalBoundaryProviderExecutionCompositionReadyNoExternalActivation",
    "CredentialConfigValidationOnly"
)) {
    Assert-True ($source -match [regex]::Escape($token)) "R48 source missing token: $token"
}
$checks.sourceProofPassed = $true

$artifactText = (Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r48-*" -File | ForEach-Object {
    Get-Content -LiteralPath $_.FullName -Raw
}) -join "`n"
foreach ($envName in @("LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD", "LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID")) {
    $value = [Environment]::GetEnvironmentVariable($envName)
    if (-not [string]::IsNullOrWhiteSpace($value)) {
        Assert-True ($artifactText -notlike "*$value*") "R48 artifacts contain raw sensitive value for $envName."
    }
}
Assert-True ($artifactText -notmatch "554=") "R48 artifacts contain raw FIX password tag."
Assert-True ($artifactText -notmatch "35=D|35=F|35=H|35=AE|35=8") "R48 artifacts contain forbidden raw sensitive FIX message type."
$checks.sanitizationPassed = $true

$apiProgram = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/Program.cs") -Raw
$workerPath = Join-Path $Root "src/QQ.Production.Intraday.Worker/Program.cs"
$workerProgram = if (Test-Path -LiteralPath $workerPath) { Get-Content -LiteralPath $workerPath -Raw } else { "" }
$appsettings = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/appsettings.json") -Raw
Assert-True ($apiProgram -match "AddSingleton<IVenueExecutionGateway,\s*FakeLmaxGateway>") "API default gateway is not FakeLmaxGateway."
Assert-True ($appsettings -match '"RequireFakeExecutionGateway"\s*:\s*true') "RequireFakeExecutionGateway changed."
Assert-True ($appsettings -match '"AllowExternalConnections"\s*:\s*false') "AllowExternalConnections default changed."
foreach ($token in @("LmaxExternalBoundaryProviderExecutionComposition", "ApprovedBoundedExecutableReadOnly", "phase-lmax-r48")) {
    Assert-True ($apiProgram -notmatch [regex]::Escape($token)) "R48 API wiring detected: $token"
    Assert-True ($workerProgram -notmatch [regex]::Escape($token)) "R48 Worker wiring detected: $token"
    Assert-True ($appsettings -notmatch [regex]::Escape($token)) "R48 appsettings wiring detected: $token"
}
$checks.startupSafetyPassed = $true

$gate = [ordered]@{
    phase = "LMAX-R48"
    validatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    validator = "scripts/check-lmax-r48-external-boundary-provider-execution-composition-fix.ps1"
    passed = $true
    classification = $summary.classification
    noApprovedR47ExternalBoundaryProviderExecutionComposition = $summary.noApprovedR47ExternalBoundaryProviderExecutionComposition
    externalActivationAttempted = $summary.externalActivationAttempted
    credentialValuesReturned = $summary.credentialValuesReturned
    checks = $checks
}

$gate | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $gatePath -Encoding UTF8
Write-Host "LMAX R48 gate validation passed: $gatePath"
