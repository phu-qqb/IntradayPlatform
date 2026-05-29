param(
    [string]$Root = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

$artifactRoot = Join-Path $Root "artifacts/readiness/lmax-runtime-enablement"
$gatePath = Join-Path $artifactRoot "phase-lmax-r46-gate-validation.json"
$successClassification = "LMAX_R46_PASS_EXECUTABLE_BOUNDARY_OPERATION_COMPOSITION_FIXED_NO_EXTERNAL_ACTIVATION"
$r45Blocker = "NoApprovedR45ExecutableBoundaryOperationComposition"
$usdJpyCaveat = "prior failed-safe root cause remains unproven"

$allowedClassifications = @(
    "LMAX_R46_PASS_EXECUTABLE_BOUNDARY_OPERATION_COMPOSITION_FIXED_NO_EXTERNAL_ACTIVATION",
    "LMAX_R46_FAIL_R45_BLOCKER_STILL_PRESENT",
    "LMAX_R46_FAIL_BOUNDARY_OPERATION_COMPOSITION_NOT_PROVABLE",
    "LMAX_R46_FAIL_BOUNDED_RUNTIME_COMPOSITION_REGRESSION",
    "LMAX_R46_FAIL_RUNTIME_DELEGATE_BINDING_REGRESSION",
    "LMAX_R46_FAIL_APPROVAL_GATES_WEAKENED",
    "LMAX_R46_FAIL_API_WORKER_GATEWAY_REGRESSION",
    "LMAX_R46_FAIL_LIVE_LAUNCHER_OR_BACKGROUND_SERVICE_INTRODUCED",
    "LMAX_R46_FAIL_FORBIDDEN_ACTION_INTRODUCED",
    "LMAX_R46_FAIL_EXTERNAL_BOUNDARY_ATTEMPTED",
    "LMAX_R46_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R46_FAIL_BUILD_OR_TESTS"
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
    "phase-lmax-r46-executable-boundary-operation-composition-fix-report.md",
    "phase-lmax-r46-executable-boundary-operation-composition-summary.json",
    "phase-lmax-r46-r45-blocker-before-after-classification.json",
    "phase-lmax-r46-boundary-operation-composition-validation.json",
    "phase-lmax-r46-bounded-executor-validation.json",
    "phase-lmax-r46-runtime-delegate-path-validation.json",
    "phase-lmax-r46-approved-instrument-scope-validation.json",
    "phase-lmax-r46-forbidden-actions-audit.json",
    "phase-lmax-r46-api-worker-fake-gateway-audit.json",
    "phase-lmax-r46-no-live-launcher-audit.json",
    "phase-lmax-r46-no-external-boundary-attempted.json",
    "phase-lmax-r46-usdjpy-caveat-preservation.json",
    "phase-lmax-r46-next-phase-recommendation.json",
    "phase-lmax-r46-gate-validation.json"
)

$checks = [ordered]@{}
foreach ($file in $required) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactRoot $file)) "Missing R46 artifact: $file"
}
$checks.requiredArtifactsExist = $true

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r46-executable-boundary-operation-composition-summary.json")
Assert-True ($allowedClassifications -contains [string]$summary.classification) "R46 classification not allowed: $($summary.classification)"
Assert-Equal $summary.classification $successClassification "R46 classification"
Assert-False $summary.noApprovedR45ExecutableBoundaryOperationComposition "R45 blocker remains present."
Assert-True $summary.executableBoundaryOperationCompositionExplicit "Boundary operation composition is not explicit/provable."
Assert-True $summary.concreteBoundedRuntimeCompositionUsed "R44 bounded runtime composition was bypassed."
Assert-True $summary.adapterModeApprovedBoundedExecutableReadOnly "ApprovedBoundedExecutableReadOnly mode missing."
Assert-True $summary.boundedExecutorApproved "Bounded executor approval missing."
Assert-True $summary.runtimeDelegateBindingApproved "Runtime delegate binding approval missing."
Assert-True $summary.approvedInstrumentsExact "Approved instruments are not exact."
Assert-True $summary.usdJpyCaveatPreserved "USDJPY caveat missing."
Assert-False $summary.externalBoundaryAttempted "External boundary attempted in R46."
Assert-False $summary.productionAccountUsed "Production account used."
Assert-False $summary.credentialValuesReturned "credentialValuesReturned must be false."
Assert-True $summary.buildRepresented "Build evidence missing."
Assert-True $summary.testsRepresented "Test evidence missing."
$checks.summaryPassed = $true

$beforeAfter = Read-Json (Join-Path $artifactRoot "phase-lmax-r46-r45-blocker-before-after-classification.json")
Assert-Equal $beforeAfter.before.blocker $r45Blocker "R45 blocker before"
Assert-True $beforeAfter.before.present "R45 blocker before should be present."
Assert-False $beforeAfter.after.present "R45 blocker after should be cleared."
Assert-Equal $beforeAfter.after.classification $successClassification "R46 after classification"
$checks.r45BlockerCleared = $true

$composition = Read-Json (Join-Path $artifactRoot "phase-lmax-r46-boundary-operation-composition-validation.json")
foreach ($field in @(
    "passed",
    "explicitAndProvable",
    "concreteBoundedRuntimeCompositionUsed",
    "credentialConfigOperationPresent",
    "tcpSocketOperationPresent",
    "tlsOperationPresent",
    "fixLogonSessionOperationPresent",
    "marketDataRequestOperationPresent",
    "marketDataResponseEntryCapturePresent",
    "shutdownRevertOperationPresent",
    "adapterModeApprovedBoundedExecutableReadOnly",
    "boundedExecutorApproved",
    "runtimeDelegateBindingApproved",
    "approvedInstrumentsExact",
    "usdJpyCaveatPreserved"
)) {
    Assert-True $composition.$field "Boundary operation composition field failed: $field"
}
Assert-False $composition.noApprovedR45ExecutableBoundaryOperationComposition "Boundary operation composition still has R45 blocker."
Assert-False $composition.apiWorkerStartupRequired "Composition requires API/Worker startup."
Assert-False $composition.liveLauncherRequired "Composition requires live launcher."
Assert-False $composition.hostedBackgroundServiceRequired "Composition requires hosted/background service."
Assert-False $composition.schedulerPollingRequired "Composition requires scheduler/polling."
Assert-False $composition.orderTradingPathReachable "Composition exposes order/trading path."
Assert-False $composition.externalBoundaryAttempted "Composition attempted external boundary."
$checks.boundaryOperationCompositionPassed = $true

$bounded = Read-Json (Join-Path $artifactRoot "phase-lmax-r46-bounded-executor-validation.json")
Assert-True $bounded.passed "Bounded executor validation failed."
Assert-False $bounded.noApprovedR45ExecutableBoundaryOperationComposition "Bounded executor still sees R45 blocker."
Assert-True ([int]$bounded.maxAttemptCount -eq 1) "maxAttemptCount must be one."
Assert-True ([int]$bounded.retryCount -eq 0) "retryCount must be zero."
Assert-False $bounded.batchMode "batchMode was enabled."
Assert-False $bounded.loopMode "loopMode was enabled."
Assert-False $bounded.externalActionExecuted "Bounded executor validation executed external action."
$checks.boundedExecutorValidationPassed = $true

$delegate = Read-Json (Join-Path $artifactRoot "phase-lmax-r46-runtime-delegate-path-validation.json")
foreach ($field in @("providerCompletenessPass","clientCompletenessPass","operationCompletenessPass","coreCompositionPass","runtimeDelegateBindingCompletenessPass","runtimeDelegatePathProvable")) {
    Assert-True $delegate.$field "Runtime delegate path validation failed: $field"
}
Assert-False $delegate.externalActionExecuted "Runtime delegate path executed external action."
$checks.runtimeDelegatePathPassed = $true

$instrument = Read-Json (Join-Path $artifactRoot "phase-lmax-r46-approved-instrument-scope-validation.json")
Assert-True $instrument.passed "Instrument scope validation failed."
Assert-True $instrument.approvedInstrumentsExact "Approved instrument scope differs."
Assert-Equal (($instrument.instruments | ForEach-Object { $_.symbol }) -join ",") "GBPUSD,EURGBP,AUDUSD,USDJPY" "Approved instrument order"
Assert-True $instrument.usdJpyCaveatPreserved "USDJPY caveat missing in instrument scope."
$checks.instrumentScopePassed = $true

$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r46-forbidden-actions-audit.json")
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

$api = Read-Json (Join-Path $artifactRoot "phase-lmax-r46-api-worker-fake-gateway-audit.json")
Assert-True $api.passed "API/Worker fake gateway audit failed."
Assert-Equal $api.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "API/Worker gateway mode"
Assert-False $api.defaultGatewayRegistrationChanged "Default gateway registration changed."
$checks.apiWorkerFakeGatewayPassed = $true

$launcher = Read-Json (Join-Path $artifactRoot "phase-lmax-r46-no-live-launcher-audit.json")
Assert-True $launcher.passed "No-live-launcher audit failed."
foreach ($field in @("liveLauncherCreated","consoleAppCreated","scriptCreated","hostedServiceAdded","backgroundServiceAdded","schedulerAdded","pollingAdded","apiEndpointAdded")) {
    Assert-False $launcher.$field "Launcher/service flag was true: $field"
}
$checks.noLiveLauncherPassed = $true

$boundary = Read-Json (Join-Path $artifactRoot "phase-lmax-r46-no-external-boundary-attempted.json")
Assert-True $boundary.passed "No-external-boundary validation failed."
foreach ($field in @("tcpSocket","tls","fixLogonSession","marketDataRequest","marketDataResponseEntries")) {
    Assert-Equal $boundary.$field "NotAttempted" "Boundary status for $field"
}
Assert-False $boundary.externalActivationAttempted "External activation attempted."
Assert-Equal ([int]$boundary.attemptCount) 0 "Attempt count"
$checks.noExternalBoundaryPassed = $true

$usd = Read-Json (Join-Path $artifactRoot "phase-lmax-r46-usdjpy-caveat-preservation.json")
Assert-True $usd.passed "USDJPY caveat validation failed."
Assert-Equal $usd.securityId "4004" "USDJPY SecurityID"
Assert-Equal $usd.securityIdSource "8" "USDJPY SecurityIDSource"
Assert-Equal $usd.caveat $usdJpyCaveat "USDJPY caveat"
Assert-False $usd.caveatWeakened "USDJPY caveat weakened."
$checks.usdJpyCaveatPassed = $true

$next = Read-Json (Join-Path $artifactRoot "phase-lmax-r46-next-phase-recommendation.json")
Assert-False $next.r47Executed "R47 was executed."
Assert-True ([string]$next.recommendedNextPhase -like "Phase LMAX-R47*") "Missing R47 recommendation."
$checks.nextPhasePassed = $true

$artifactText = (Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r46-*" -File | ForEach-Object {
    Get-Content -LiteralPath $_.FullName -Raw
}) -join "`n"
foreach ($envName in @("LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD", "LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID")) {
    $value = [Environment]::GetEnvironmentVariable($envName)
    if (-not [string]::IsNullOrWhiteSpace($value)) {
        Assert-True ($artifactText -notlike "*$value*") "R46 artifacts contain raw sensitive value for $envName."
    }
}
Assert-True ($artifactText -notmatch "554=") "R46 artifacts contain raw FIX password tag."
Assert-True ($artifactText -notmatch "35=D|35=F|35=H|35=AE|35=8") "R46 artifacts contain forbidden raw sensitive FIX message type."
$checks.sanitizationPassed = $true

$compositionSource = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxExecutableBoundaryOperationComposition.cs") -Raw
foreach ($token in @(
    "LmaxExecutableBoundaryOperationComposition",
    "NoApprovedR45ExecutableBoundaryOperationComposition",
    "CredentialConfigBoundaryOperationMissing",
    "TcpSocketBoundaryOperationMissing",
    "TlsBoundaryOperationMissing",
    "FixLogonBoundaryOperationMissing",
    "MarketDataRequestBoundaryOperationMissing",
    "MarketDataResponseEntryCaptureMissing",
    "ShutdownRevertBoundaryOperationMissing"
)) {
    Assert-True ($compositionSource -match [regex]::Escape($token)) "Boundary operation source missing token: $token"
}
foreach ($token in @("TcpClient", "SslStream", "ConnectAsync", "NewOrderSingle", "ShadowReplaySubmitted = true")) {
    Assert-True ($compositionSource -notmatch [regex]::Escape($token)) "Boundary operation source contains forbidden token: $token"
}
$checks.sourceCompositionPassed = $true

$apiProgram = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/Program.cs") -Raw
$workerPath = Join-Path $Root "src/QQ.Production.Intraday.Worker/Program.cs"
$workerProgram = if (Test-Path -LiteralPath $workerPath) { Get-Content -LiteralPath $workerPath -Raw } else { "" }
$appsettings = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/appsettings.json") -Raw
Assert-True ($apiProgram -match "AddSingleton<IVenueExecutionGateway,\s*FakeLmaxGateway>") "API default gateway is not FakeLmaxGateway."
Assert-True ($appsettings -match '"RequireFakeExecutionGateway"\s*:\s*true') "RequireFakeExecutionGateway changed."
Assert-True ($appsettings -match '"AllowExternalConnections"\s*:\s*false') "AllowExternalConnections default changed."
foreach ($token in @("LmaxExecutableBoundaryOperationComposition", "ApprovedBoundedExecutableReadOnly", "phase-lmax-r46")) {
    Assert-True ($apiProgram -notmatch [regex]::Escape($token)) "R46 API wiring detected: $token"
    Assert-True ($workerProgram -notmatch [regex]::Escape($token)) "R46 Worker wiring detected: $token"
    Assert-True ($appsettings -notmatch [regex]::Escape($token)) "R46 appsettings wiring detected: $token"
}
$checks.startupSafetyPassed = $true

$gate = [ordered]@{
    phase = "LMAX-R46"
    validatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    validator = "scripts/check-lmax-r46-executable-boundary-operation-composition-fix.ps1"
    passed = $true
    classification = $summary.classification
    noApprovedR45ExecutableBoundaryOperationComposition = $summary.noApprovedR45ExecutableBoundaryOperationComposition
    externalActivationAttempted = $summary.externalBoundaryAttempted
    checks = $checks
}

$gate | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $gatePath -Encoding UTF8
Write-Host "LMAX R46 gate validation passed: $gatePath"
