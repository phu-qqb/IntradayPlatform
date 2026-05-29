param(
    [string]$Root = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

$artifactRoot = Join-Path $Root "artifacts/readiness/lmax-runtime-enablement"
$gatePath = Join-Path $artifactRoot "phase-lmax-r42-gate-validation.json"
$expectedDecision = "LMAX_R42_PASS_CONCRETE_FINAL_PRE_EXECUTION_BLOCKER_FIXED_NO_EXTERNAL_ACTIVATION"
$usdJpyCaveat = "prior failed-safe root cause remains unproven"

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

$requiredArtifacts = @(
    "phase-lmax-r42-concrete-adapter-blocker-fix-report.md",
    "phase-lmax-r42-concrete-adapter-blocker-fix-summary.json",
    "phase-lmax-r42-adapter-before-after-classification.json",
    "phase-lmax-r42-bounded-executor-validation-result.json",
    "phase-lmax-r42-runtime-delegate-path-validation.json",
    "phase-lmax-r42-forbidden-actions-audit.json",
    "phase-lmax-r42-api-worker-fake-gateway-audit.json",
    "phase-lmax-r42-no-live-launcher-audit.json",
    "phase-lmax-r42-usdjpy-caveat-preservation.json",
    "phase-lmax-r42-next-phase-recommendation.json"
)

$checks = [ordered]@{}
foreach ($file in $requiredArtifacts) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactRoot $file)) "Missing R42 artifact: $file"
}
$checks.requiredArtifactsExist = $true

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r42-concrete-adapter-blocker-fix-summary.json")
Assert-Equal $summary.classification $expectedDecision "R42 classification"
Assert-True $summary.concreteAdapterStillDryRunOnlyClearedForApprovedBoundedPath "ConcreteAdapterStillDryRunOnly remains true for approved bounded path."
Assert-False $summary.externalBoundaryAttempted "External boundary was attempted."
Assert-False $summary.tcpAttempted "TCP boundary was attempted."
Assert-False $summary.tlsAttempted "TLS boundary was attempted."
Assert-False $summary.fixLogonAttempted "FIX logon was attempted."
Assert-False $summary.marketDataRequestAttempted "MarketDataRequest was attempted."
Assert-True $summary.buildRepresented "Build result is not represented in R42 evidence."
Assert-True $summary.testsRepresented "Test result is not represented in R42 evidence."
$checks.summaryPassed = $true

$classification = Read-Json (Join-Path $artifactRoot "phase-lmax-r42-adapter-before-after-classification.json")
Assert-True $classification.before.concreteAdapterStillDryRunOnly "Before classification did not capture the R41 blocker."
Assert-False $classification.after.approvedBoundedExecutableReadOnlyPath.concreteAdapterStillDryRunOnly "After classification still dry-run-only."
Assert-True $classification.after.dryRunOnlyPath.stillAvailable "Dry-run-only path was not preserved."
Assert-True $classification.after.approvedBoundedExecutableReadOnlyPath.requiresBoundedExecutorApproval "Executable path does not require bounded executor approval."
Assert-True $classification.after.approvedBoundedExecutableReadOnlyPath.requiresRuntimeDelegateBindingApproval "Executable path does not require delegate binding approval."
$checks.classificationPassed = $true

$bounded = Read-Json (Join-Path $artifactRoot "phase-lmax-r42-bounded-executor-validation-result.json")
Assert-True $bounded.passed "Bounded executor validation did not pass."
Assert-True $bounded.maxAttemptCountIsOne "Bounded executor maxAttemptCount is not one."
Assert-True $bounded.retryCountIsZero "Bounded executor retryCount is not zero."
Assert-False $bounded.batchMode "Bounded executor batch mode enabled."
Assert-False $bounded.loopMode "Bounded executor loop mode enabled."
Assert-True $bounded.approvedBoundedExecutablePathNoLongerBlockedByDryRunOnly "Bounded path still blocked by dry-run-only."
Assert-False $bounded.externalActionExecuted "Bounded validation executed external action."
$checks.boundedExecutorValidationPassed = $true

$delegatePath = Read-Json (Join-Path $artifactRoot "phase-lmax-r42-runtime-delegate-path-validation.json")
foreach ($field in @(
    "providerCompletenessPass",
    "clientCompletenessPass",
    "operationCompletenessPass",
    "coreCompositionPass",
    "runtimeDelegateBindingCompletenessPass",
    "delegateBindingSetExists",
    "delegateBindingFactoryExists",
    "delegateBindingValidatorExists",
    "approvedBoundedPathRequiresRuntimeDelegateBindingApproval"
)) {
    Assert-True $delegatePath.$field "Runtime delegate path check failed: $field"
}
Assert-False $delegatePath.externalActionExecuted "Runtime delegate path validation executed external action."
$checks.runtimeDelegatePathPassed = $true

$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r42-forbidden-actions-audit.json")
foreach ($field in @(
    "externalRunExecuted",
    "realSocketOpened",
    "tcpConnectionAttempted",
    "tlsHandshakeAttempted",
    "fixLogonAttempted",
    "marketDataRequestSent",
    "snapshotExecuted",
    "orderSubmissionExecuted",
    "orderPathEnabled",
    "tradingEnabled",
    "tradingStateMutated",
    "schedulerStarted",
    "pollingStarted",
    "replayExecuted",
    "shadowReplaySubmitted",
    "productionAccountUsed",
    "credentialsPrinted",
    "credentialsStored",
    "rawFixLogsStored"
)) {
    Assert-False $forbidden.$field "Forbidden action flag was true: $field"
}
Assert-True $forbidden.passed "Forbidden action audit failed."
$checks.forbiddenActionsPassed = $true

$api = Read-Json (Join-Path $artifactRoot "phase-lmax-r42-api-worker-fake-gateway-audit.json")
Assert-True $api.passed "API/Worker fake gateway audit failed."
Assert-Equal $api.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "API/Worker gateway mode"
Assert-False $api.apiWiringAdded "API wiring was added."
Assert-False $api.workerWiringAdded "Worker wiring was added."
Assert-False $api.defaultConfigChangedToEnableLmax "Default config changed to enable LMAX."
$checks.apiWorkerFakeGatewayPassed = $true

$launcher = Read-Json (Join-Path $artifactRoot "phase-lmax-r42-no-live-launcher-audit.json")
Assert-True $launcher.passed "No-live-launcher audit failed."
Assert-False $launcher.liveLauncherCreated "Live launcher created."
Assert-False $launcher.hostedServiceAdded "Hosted service added."
Assert-False $launcher.backgroundWorkerAdded "Background worker added."
Assert-False $launcher.schedulerOrPollingAdded "Scheduler or polling added."
$checks.noLiveLauncherPassed = $true

$usd = Read-Json (Join-Path $artifactRoot "phase-lmax-r42-usdjpy-caveat-preservation.json")
Assert-True $usd.passed "USDJPY caveat audit failed."
Assert-Equal $usd.securityId "4004" "USDJPY SecurityID"
Assert-Equal $usd.securityIdSource "8" "USDJPY SecurityIDSource"
Assert-Equal $usd.caveat $usdJpyCaveat "USDJPY caveat"
Assert-False $usd.caveatWeakened "USDJPY caveat weakened."
$checks.usdJpyCaveatPassed = $true

$next = Read-Json (Join-Path $artifactRoot "phase-lmax-r42-next-phase-recommendation.json")
Assert-Equal $next.recommendedNextPhase "Phase LMAX-R43 - Operator-Approved Single Temporary Demo Read-Only Activation Retry After Concrete Adapter Fix" "R42 next phase"
Assert-False $next.r43Executed "R43 was executed."
$checks.nextPhasePassed = $true

$adapterPath = Join-Path $Root "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter.cs"
$adapterSource = Get-Content -LiteralPath $adapterPath -Raw
foreach ($token in @(
    "ApprovedBoundedExecutableReadOnly",
    "BoundedExecutorApprovalMissing",
    "RuntimeDelegateBindingApprovalMissing",
    "BoundedExecutableReadOnlyAccepted"
)) {
    Assert-True ($adapterSource -match [regex]::Escape($token)) "Adapter source missing R42 token: $token"
}
Assert-True ($adapterSource -notmatch "TcpClient|SslStream|NetworkStream|ConnectAsync|AddHostedService|BackgroundService") "Adapter source introduced network or hosted-service implementation."
$checks.adapterSourcePassed = $true

$requestPath = Join-Path $Root "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxTemporaryReadOnlyRuntimeAdapterPath.cs"
$requestSource = Get-Content -LiteralPath $requestPath -Raw
Assert-True ($requestSource -match "BoundedExecutorApproved") "Request contract missing bounded executor approval."
Assert-True ($requestSource -match "RuntimeDelegateBindingApproved") "Request contract missing runtime delegate binding approval."
$checks.requestContractPassed = $true

$apiProgram = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/Program.cs") -Raw
$workerPath = Join-Path $Root "src/QQ.Production.Intraday.Worker/Program.cs"
$workerProgram = if (Test-Path -LiteralPath $workerPath) { Get-Content -LiteralPath $workerPath -Raw } else { "" }
$appsettings = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/appsettings.json") -Raw
Assert-True ($apiProgram -match "AddSingleton<IVenueExecutionGateway,\s*FakeLmaxGateway>") "API default gateway is not FakeLmaxGateway."
Assert-True ($appsettings -match '"RequireFakeExecutionGateway"\s*:\s*true') "RequireFakeExecutionGateway changed."
Assert-True ($appsettings -match '"AllowExternalConnections"\s*:\s*false') "AllowExternalConnections default changed."
Assert-True ($appsettings -match '"AllowOrderSubmission"\s*:\s*false') "AllowOrderSubmission default changed."
foreach ($token in @("LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter", "ApprovedBoundedExecutableReadOnly", "phase-lmax-r42")) {
    Assert-True ($apiProgram -notmatch [regex]::Escape($token)) "R42 API wiring detected: $token"
    Assert-True ($workerProgram -notmatch [regex]::Escape($token)) "R42 Worker wiring detected: $token"
    Assert-True ($appsettings -notmatch [regex]::Escape($token)) "R42 appsettings wiring detected: $token"
}
$checks.localStartupSafetyPassed = $true

$artifactText = (Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r42-*" -File | ForEach-Object {
    Get-Content -LiteralPath $_.FullName -Raw
}) -join "`n"
foreach ($envName in @("LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD", "LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID")) {
    $value = [Environment]::GetEnvironmentVariable($envName)
    if (-not [string]::IsNullOrWhiteSpace($value)) {
        Assert-True ($artifactText -notlike "*$value*") "R42 artifacts contain raw sensitive value for $envName."
    }
}
Assert-True ($artifactText -notmatch "554=") "R42 artifacts contain raw FIX password tag."
Assert-True ($artifactText -notmatch "35=D|35=F|35=H|35=AE|35=8") "R42 artifacts contain forbidden raw sensitive FIX message type."
$checks.sanitizationPassed = $true

$gate = [ordered]@{
    phase = "LMAX-R42"
    validatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    validator = "scripts/check-lmax-r42-concrete-final-pre-execution-blocker-fix.ps1"
    passed = $true
    classification = $expectedDecision
    checks = $checks
}

$gate | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $gatePath -Encoding UTF8
Write-Host "LMAX R42 gate validation passed: $gatePath"
