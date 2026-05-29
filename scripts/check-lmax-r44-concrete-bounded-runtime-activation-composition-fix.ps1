param(
    [string]$Root = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

$artifactRoot = Join-Path $Root "artifacts/readiness/lmax-runtime-enablement"
$gatePath = Join-Path $artifactRoot "phase-lmax-r44-gate-validation.json"
$successClassification = "LMAX_R44_PASS_CONCRETE_BOUNDED_RUNTIME_ACTIVATION_COMPOSITION_FIXED_NO_EXTERNAL_ACTIVATION"
$r43Blocker = "NoApprovedR43BoundedExecutableRuntimeActivationComposition"
$usdJpyCaveat = "prior failed-safe root cause remains unproven"

$allowedClassifications = @(
    "LMAX_R44_PASS_CONCRETE_BOUNDED_RUNTIME_ACTIVATION_COMPOSITION_FIXED_NO_EXTERNAL_ACTIVATION",
    "LMAX_R44_FAIL_R43_BLOCKER_STILL_PRESENT",
    "LMAX_R44_FAIL_BOUNDED_RUNTIME_COMPOSITION_NOT_PROVABLE",
    "LMAX_R44_FAIL_RUNTIME_DELEGATE_BINDING_REGRESSION",
    "LMAX_R44_FAIL_APPROVAL_GATES_WEAKENED",
    "LMAX_R44_FAIL_API_WORKER_GATEWAY_REGRESSION",
    "LMAX_R44_FAIL_LIVE_LAUNCHER_OR_BACKGROUND_SERVICE_INTRODUCED",
    "LMAX_R44_FAIL_FORBIDDEN_ACTION_INTRODUCED",
    "LMAX_R44_FAIL_EXTERNAL_BOUNDARY_ATTEMPTED",
    "LMAX_R44_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R44_FAIL_BUILD_OR_TESTS"
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
    "phase-lmax-r44-concrete-bounded-runtime-activation-composition-fix-report.md",
    "phase-lmax-r44-concrete-bounded-runtime-activation-composition-summary.json",
    "phase-lmax-r44-r43-blocker-before-after-classification.json",
    "phase-lmax-r44-bounded-executor-composition-validation.json",
    "phase-lmax-r44-runtime-delegate-path-validation.json",
    "phase-lmax-r44-approved-instrument-scope-validation.json",
    "phase-lmax-r44-forbidden-actions-audit.json",
    "phase-lmax-r44-api-worker-fake-gateway-audit.json",
    "phase-lmax-r44-no-live-launcher-audit.json",
    "phase-lmax-r44-no-external-boundary-attempted.json",
    "phase-lmax-r44-usdjpy-caveat-preservation.json",
    "phase-lmax-r44-next-phase-recommendation.json",
    "phase-lmax-r44-gate-validation.json"
)

$checks = [ordered]@{}
foreach ($file in $required) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactRoot $file)) "Missing R44 artifact: $file"
}
$checks.requiredArtifactsExist = $true

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r44-concrete-bounded-runtime-activation-composition-summary.json")
Assert-True ($allowedClassifications -contains [string]$summary.classification) "R44 classification not allowed: $($summary.classification)"
Assert-Equal $summary.classification $successClassification "R44 classification"
Assert-False $summary.noApprovedR43BoundedExecutableRuntimeActivationComposition "R43 blocker remains present."
Assert-True $summary.boundedExecutableRuntimeActivationCompositionExplicit "Composition is not explicit/provable."
Assert-True $summary.adapterModeApprovedBoundedExecutableReadOnly "ApprovedBoundedExecutableReadOnly mode missing."
Assert-True $summary.boundedExecutorApproved "Bounded executor approval missing."
Assert-True $summary.runtimeDelegateBindingApproved "Runtime delegate binding approval missing."
Assert-True $summary.approvedInstrumentsExact "Approved instruments are not exact."
Assert-True $summary.usdJpyCaveatPreserved "USDJPY caveat missing."
Assert-False $summary.externalBoundaryAttempted "External boundary attempted in R44."
Assert-False $summary.productionAccountUsed "Production account used."
Assert-False $summary.credentialValuesReturned "credentialValuesReturned must be false."
Assert-True $summary.buildRepresented "Build evidence missing."
Assert-True $summary.testsRepresented "Test evidence missing."
$checks.summaryPassed = $true

$beforeAfter = Read-Json (Join-Path $artifactRoot "phase-lmax-r44-r43-blocker-before-after-classification.json")
Assert-Equal $beforeAfter.before.blocker $r43Blocker "R43 blocker before"
Assert-True $beforeAfter.before.present "R43 blocker before should be present."
Assert-False $beforeAfter.after.present "R43 blocker after should be cleared."
Assert-Equal $beforeAfter.after.classification $successClassification "R44 after classification"
$checks.r43BlockerCleared = $true

$composition = Read-Json (Join-Path $artifactRoot "phase-lmax-r44-bounded-executor-composition-validation.json")
foreach ($field in @(
    "passed",
    "concreteAdapterPresent",
    "boundedExecutorPresent",
    "runtimeDelegateBindingPresent",
    "operationBindingSetPresent",
    "providerClientSetPresent",
    "adapterModeApprovedBoundedExecutableReadOnly",
    "boundedExecutorApproved",
    "runtimeDelegateBindingApproved",
    "phaseReservedForApprovedRetry",
    "approvedInstrumentsExact",
    "usdJpyCaveatPreserved"
)) {
    Assert-True $composition.$field "Composition validation field failed: $field"
}
Assert-False $composition.noApprovedR43BoundedExecutableRuntimeActivationComposition "Composition validation still has R43 blocker."
Assert-False $composition.apiWorkerStartupRequired "Composition requires API/Worker startup."
Assert-False $composition.liveLauncherRequired "Composition requires live launcher."
Assert-False $composition.hostedBackgroundServiceRequired "Composition requires hosted/background service."
Assert-False $composition.schedulerPollingRequired "Composition requires scheduler/polling."
Assert-False $composition.orderTradingPathReachable "Composition exposes order/trading path."
Assert-False $composition.externalBoundaryAttempted "Composition attempted external boundary."
$checks.compositionValidationPassed = $true

$delegate = Read-Json (Join-Path $artifactRoot "phase-lmax-r44-runtime-delegate-path-validation.json")
foreach ($field in @("providerCompletenessPass","clientCompletenessPass","operationCompletenessPass","coreCompositionPass","runtimeDelegateBindingCompletenessPass","runtimeDelegatePathProvable")) {
    Assert-True $delegate.$field "Runtime delegate path validation failed: $field"
}
Assert-False $delegate.externalActionExecuted "Runtime delegate path executed external action."
$checks.runtimeDelegatePathPassed = $true

$instrument = Read-Json (Join-Path $artifactRoot "phase-lmax-r44-approved-instrument-scope-validation.json")
Assert-True $instrument.passed "Instrument scope validation failed."
Assert-True $instrument.approvedInstrumentsExact "Approved instrument scope differs."
Assert-Equal (($instrument.instruments | ForEach-Object { $_.symbol }) -join ",") "GBPUSD,EURGBP,AUDUSD,USDJPY" "Approved instrument order"
Assert-True $instrument.usdJpyCaveatPreserved "USDJPY caveat missing in instrument scope."
$checks.instrumentScopePassed = $true

$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r44-forbidden-actions-audit.json")
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

$api = Read-Json (Join-Path $artifactRoot "phase-lmax-r44-api-worker-fake-gateway-audit.json")
Assert-True $api.passed "API/Worker fake gateway audit failed."
Assert-Equal $api.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "API/Worker gateway mode"
Assert-False $api.defaultGatewayRegistrationChanged "Default gateway registration changed."
$checks.apiWorkerFakeGatewayPassed = $true

$launcher = Read-Json (Join-Path $artifactRoot "phase-lmax-r44-no-live-launcher-audit.json")
Assert-True $launcher.passed "No-live-launcher audit failed."
foreach ($field in @("liveLauncherCreated","consoleAppCreated","scriptCreated","hostedServiceAdded","backgroundServiceAdded","schedulerAdded","pollingAdded","apiEndpointAdded")) {
    Assert-False $launcher.$field "Launcher/service flag was true: $field"
}
$checks.noLiveLauncherPassed = $true

$boundary = Read-Json (Join-Path $artifactRoot "phase-lmax-r44-no-external-boundary-attempted.json")
Assert-True $boundary.passed "No-external-boundary validation failed."
foreach ($field in @("tcpSocket","tls","fixLogonSession","marketDataRequest","marketDataResponseEntries")) {
    Assert-Equal $boundary.$field "NotAttempted" "Boundary status for $field"
}
Assert-False $boundary.externalActivationAttempted "External activation attempted."
Assert-Equal ([int]$boundary.attemptCount) 0 "Attempt count"
$checks.noExternalBoundaryPassed = $true

$usd = Read-Json (Join-Path $artifactRoot "phase-lmax-r44-usdjpy-caveat-preservation.json")
Assert-True $usd.passed "USDJPY caveat validation failed."
Assert-Equal $usd.securityId "4004" "USDJPY SecurityID"
Assert-Equal $usd.securityIdSource "8" "USDJPY SecurityIDSource"
Assert-Equal $usd.caveat $usdJpyCaveat "USDJPY caveat"
Assert-False $usd.caveatWeakened "USDJPY caveat weakened."
$checks.usdJpyCaveatPassed = $true

$next = Read-Json (Join-Path $artifactRoot "phase-lmax-r44-next-phase-recommendation.json")
Assert-False $next.r45Executed "R45 was executed."
Assert-True ([string]$next.recommendedNextPhase -like "Phase LMAX-R45*") "Missing R45 recommendation."
$checks.nextPhasePassed = $true

$artifactText = (Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r44-*" -File | ForEach-Object {
    Get-Content -LiteralPath $_.FullName -Raw
}) -join "`n"
foreach ($envName in @("LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD", "LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID")) {
    $value = [Environment]::GetEnvironmentVariable($envName)
    if (-not [string]::IsNullOrWhiteSpace($value)) {
        Assert-True ($artifactText -notlike "*$value*") "R44 artifacts contain raw sensitive value for $envName."
    }
}
Assert-True ($artifactText -notmatch "554=") "R44 artifacts contain raw FIX password tag."
Assert-True ($artifactText -notmatch "35=D|35=F|35=H|35=AE|35=8") "R44 artifacts contain forbidden raw sensitive FIX message type."
$checks.sanitizationPassed = $true

$compositionSource = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxConcreteBoundedRuntimeActivationComposition.cs") -Raw
foreach ($token in @(
    "LmaxConcreteBoundedRuntimeActivationComposition",
    "ApprovedBoundedExecutableReadOnly",
    "BoundedExecutorApprovalMissing",
    "RuntimeDelegateBindingApprovalMissing",
    "NoApprovedR43BoundedExecutableRuntimeActivationComposition"
)) {
    Assert-True ($compositionSource -match [regex]::Escape($token)) "Composition source missing token: $token"
}
foreach ($token in @("TcpClient", "SslStream", "ConnectAsync", "NewOrderSingle", "ShadowReplaySubmitted = true")) {
    Assert-True ($compositionSource -notmatch [regex]::Escape($token)) "Composition source contains forbidden token: $token"
}
$checks.sourceCompositionPassed = $true

$apiProgram = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/Program.cs") -Raw
$workerPath = Join-Path $Root "src/QQ.Production.Intraday.Worker/Program.cs"
$workerProgram = if (Test-Path -LiteralPath $workerPath) { Get-Content -LiteralPath $workerPath -Raw } else { "" }
$appsettings = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/appsettings.json") -Raw
Assert-True ($apiProgram -match "AddSingleton<IVenueExecutionGateway,\s*FakeLmaxGateway>") "API default gateway is not FakeLmaxGateway."
Assert-True ($appsettings -match '"RequireFakeExecutionGateway"\s*:\s*true') "RequireFakeExecutionGateway changed."
Assert-True ($appsettings -match '"AllowExternalConnections"\s*:\s*false') "AllowExternalConnections default changed."
foreach ($token in @("LmaxConcreteBoundedRuntimeActivationComposition", "ApprovedBoundedExecutableReadOnly", "phase-lmax-r44")) {
    Assert-True ($apiProgram -notmatch [regex]::Escape($token)) "R44 API wiring detected: $token"
    Assert-True ($workerProgram -notmatch [regex]::Escape($token)) "R44 Worker wiring detected: $token"
    Assert-True ($appsettings -notmatch [regex]::Escape($token)) "R44 appsettings wiring detected: $token"
}
$checks.startupSafetyPassed = $true

$gate = [ordered]@{
    phase = "LMAX-R44"
    validatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    validator = "scripts/check-lmax-r44-concrete-bounded-runtime-activation-composition-fix.ps1"
    passed = $true
    classification = $summary.classification
    noApprovedR43BoundedExecutableRuntimeActivationComposition = $summary.noApprovedR43BoundedExecutableRuntimeActivationComposition
    externalActivationAttempted = $summary.externalBoundaryAttempted
    checks = $checks
}

$gate | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $gatePath -Encoding UTF8
Write-Host "LMAX R44 gate validation passed: $gatePath"
