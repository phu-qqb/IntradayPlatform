param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Missing required file: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Assert-Equal($Actual, $Expected, [string]$Message) {
    if ($Actual -ne $Expected) {
        throw "$Message. Expected '$Expected' but got '$Actual'."
    }
}

function Assert-True($Actual, [string]$Message) {
    if ($Actual -ne $true) {
        throw "$Message. Expected true but got '$Actual'."
    }
}

function Assert-False($Actual, [string]$Message) {
    if ($Actual -ne $false) {
        throw "$Message. Expected false but got '$Actual'."
    }
}

function Assert-NoSensitiveContent([string]$Path) {
    $text = Get-Content -LiteralPath $Path -Raw
    $patterns = @(
        '(?i)password\s*[:=]\s*[^,\s\}\]]+',
        '(?i)api[_-]?key\s*[:=]\s*[^,\s\}\]]+',
        '(?i)secret\s*[:=]\s*[^,\s\}\]]+',
        '(?i)sessionpassword\s*[:=]\s*[^,\s\}\]]+',
        '(?i)credential\s*[:=]\s*[^,\s\}\]]+',
        '(?i)raw\s*fix\s*[:=]\s*[^,\s\}\]]+'
    )

    foreach ($pattern in $patterns) {
        if ($text -match $pattern) {
            throw "Sensitive-content marker found in $Path"
        }
    }
}

function Assert-RequiredArtifacts([string]$BasePath, [string[]]$Files) {
    foreach ($file in $Files) {
        $path = Join-Path $BasePath $file
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Missing required artifact: $path"
        }

        Assert-NoSensitiveContent $path
    }
}

function Assert-TextContains([string]$Text, [string]$Needle, [string]$Message) {
    if ($Text -notmatch [regex]::Escape($Needle)) {
        throw "$Message. Missing '$Needle'."
    }
}

function Assert-TextNotContainsPattern([string]$Text, [string]$Pattern, [string]$Message) {
    if ($Text -match $Pattern) {
        throw "$Message. Matched '$Pattern'."
    }
}

$readiness = Join-Path $RepoRoot "artifacts\readiness\lmax-runtime-enablement"
$usdJpy = Join-Path $RepoRoot "artifacts\readiness\usdjpy-troubleshooting"

$requiredPrior = @(
    "phase-lmax-r1-design-only-decision-gate.json",
    "phase-lmax-r2-preflight-decision-gate.json",
    "phase-lmax-r3-decision-gate.json",
    "phase-lmax-r4-remediation-decision-gate.json",
    "phase-lmax-r5-inert-implementation-decision-gate.json",
    "phase-lmax-r6-decision-gate.json",
    "phase-lmax-r7-decision-gate.json",
    "phase-lmax-r8-decision-gate.json",
    "phase-lmax-r9-decision-gate.json",
    "phase-lmax-r10-decision-gate.json",
    "phase-lmax-r11-decision-gate.json",
    "phase-lmax-r12-decision-gate.json",
    "phase-lmax-r13-decision-gate.json",
    "phase-lmax-r14-decision-gate.json",
    "phase-lmax-r15-decision-gate.json",
    "phase-lmax-r16-decision-gate.json",
    "phase-lmax-r17-decision-gate.json",
    "phase-lmax-r18-decision-gate.json",
    "phase-lmax-r19-decision-gate.json",
    "phase-lmax-r20-decision-gate.json",
    "phase-lmax-r21-decision-gate.json",
    "phase-lmax-r22-decision-gate.json",
    "phase-lmax-r23-decision-gate.json",
    "phase-lmax-r24-decision-gate.json",
    "phase-lmax-r25-decision-gate.json",
    "phase-lmax-r26-decision-gate.json",
    "phase-lmax-r27-decision-gate.json",
    "phase-lmax-r28-decision-gate.json",
    "phase-lmax-r29-decision-gate.json",
    "phase-lmax-r30-decision-gate.json",
    "phase-lmax-r31-decision-gate.json",
    "phase-lmax-r31-gate-validation.json"
)

$requiredR32 = @(
    "phase-lmax-r32-r31-blocker-analysis.json",
    "phase-lmax-r32-bounded-executor-implementation-summary.json",
    "phase-lmax-r32-not-live-launcher-proof.json",
    "phase-lmax-r32-executor-options-sanitization-summary.json",
    "phase-lmax-r32-final-pre-execution-readiness-check.json",
    "phase-lmax-r32-test-coverage-summary.json",
    "phase-lmax-r32-no-external-activation-proof.json",
    "phase-lmax-r32-decision-gate.json",
    "phase-lmax-r32-non-run-validation.json",
    "phase-lmax-r32-concrete-final-pre-execution-blocker-fix-report.md",
    "phase-lmax-r32-operator-note.md"
)

Write-Host "LMAX-R32 Concrete Final Pre-Execution Blocker Fix Gate Validator"
Write-Host "This validator performs no external run, socket open, TCP attempt, TLS handshake, FIX logon, FIX send, MarketDataRequest, API/Worker startup, or config mutation."

Assert-RequiredArtifacts $readiness $requiredPrior
Assert-RequiredArtifacts $readiness $requiredR32

$t7GatePath = Join-Path $usdJpy "phase-usdjpy-t7-final-closure-gate.json"
$phase7nGatePath = Join-Path $RepoRoot "artifacts\readiness\phase7n-final-lmax-readonly-runtime-evidence-archive-closure-gate.json"
if (-not (Test-Path -LiteralPath $t7GatePath)) { throw "Missing USDJPY T7 closure gate: $t7GatePath" }
if (-not (Test-Path -LiteralPath $phase7nGatePath)) { throw "Missing Phase 7N closure gate: $phase7nGatePath" }
Assert-NoSensitiveContent $t7GatePath
Assert-NoSensitiveContent $phase7nGatePath

$r31Gate = Read-Json (Join-Path $readiness "phase-lmax-r31-decision-gate.json")
$analysis = Read-Json (Join-Path $readiness "phase-lmax-r32-r31-blocker-analysis.json")
$implementation = Read-Json (Join-Path $readiness "phase-lmax-r32-bounded-executor-implementation-summary.json")
$notLauncher = Read-Json (Join-Path $readiness "phase-lmax-r32-not-live-launcher-proof.json")
$options = Read-Json (Join-Path $readiness "phase-lmax-r32-executor-options-sanitization-summary.json")
$readinessCheck = Read-Json (Join-Path $readiness "phase-lmax-r32-final-pre-execution-readiness-check.json")
$coverage = Read-Json (Join-Path $readiness "phase-lmax-r32-test-coverage-summary.json")
$nonRun = Read-Json (Join-Path $readiness "phase-lmax-r32-non-run-validation.json")
$gate = Read-Json (Join-Path $readiness "phase-lmax-r32-decision-gate.json")
$t7Gate = Read-Json $t7GatePath
$phase7nGate = Read-Json $phase7nGatePath

Assert-Equal $r31Gate.finalDecision "LMAX_R31_FAIL_REQUIRES_LIVE_LAUNCHER" "R31 decision"
Assert-Equal $t7Gate.finalDecision "USDJPY_T7_FINAL_READINESS_ARCHIVE_CLOSED_WITH_CAVEAT" "USDJPY T7 decision"
Assert-Equal $phase7nGate.finalDecision "PASS_FINAL_LMAX_READONLY_RUNTIME_EVIDENCE_ARCHIVE_CLOSED" "Phase 7N decision"
Assert-Equal $gate.finalDecision "LMAX_R32_BOUNDED_TEMPORARY_EXECUTOR_IMPLEMENTED_NO_EXTERNAL_ACTIVATION" "R32 final decision"
Assert-False $gate.r33Authorized "R33 authorization"

Assert-Equal $analysis.contextDecision "LMAX_R31_FAIL_REQUIRES_LIVE_LAUNCHER" "R32 context"
Assert-Equal $analysis.r31ConcreteAbortCause "RequiresLiveLauncherCreation" "R31 abort cause"
Assert-Equal $analysis.blockerFixedBy "LmaxTemporaryReadOnlyActivationExecutor" "Blocker fix"
foreach ($property in @("externalActivationExecuted", "realSocketOpened", "realTcpConnectionAttempted", "realTlsHandshakeAttempted", "realFixLogonAttempted", "realFixMessageSent", "realMarketDataRequestSent", "realCredentialLoadingExecuted")) {
    Assert-False $analysis.$property "Analysis $property"
}

Assert-Equal $implementation.executorClass "LmaxTemporaryReadOnlyActivationExecutor" "Executor class"
Assert-Equal $implementation.explicitExecutionMethod "ExecuteOnce" "Execution method"
Assert-True $implementation.requiresOperatorApprovalMetadata "Operator approval metadata"
Assert-Equal $implementation.maxAttemptCount 1 "Max attempts"
Assert-Equal $implementation.retryCount 0 "Retry count"
Assert-False $implementation.batchMode "Batch mode"
Assert-False $implementation.loopMode "Loop mode"
Assert-True $implementation.demoReadOnlyOnly "Demo/read-only"
Assert-True $implementation.approvedInstrumentsOnly "Approved instruments"
Assert-True $implementation.usdJpyCaveatPreserved "USDJPY caveat"
Assert-False $implementation.constructorExecutes "Constructor executes"
Assert-False $implementation.constructorLoadsCredentials "Constructor credential load"
Assert-False $implementation.constructorOpensSocket "Constructor socket"
Assert-False $implementation.registeredByDefault "Default registration"
Assert-False $implementation.apiWorkerWiringAdded "API/Worker wiring"
Assert-False $implementation.hostedServiceAdded "Hosted service"
Assert-False $implementation.backgroundWorkerAdded "Background worker"
Assert-False $implementation.externalActivationExecuted "External activation"

foreach ($property in @("libraryLevelOnly", "manuallyInvokedOnlyByFutureApprovedPhase")) {
    Assert-True $notLauncher.$property "Not-launcher $property"
}
foreach ($property in @("mainMethodAdded", "consoleLauncherAdded", "connectionOpeningScriptAdded", "hostedServiceAdded", "schedulerAdded", "pollingLoopAdded", "backgroundWorkerAdded", "apiEndpointAdded", "defaultServiceRegistrationAdded", "persistentConfigEnablementAdded", "broadLiveLauncherCreated", "liveConnectionScriptCreated", "apiWorkerStartupRequired")) {
    Assert-False $notLauncher.$property "Not-launcher $property"
}

Assert-False $options.containsCredentials "Options credentials"
Assert-False $options.containsSessionPassword "Options session password"
Assert-False $options.containsSecrets "Options secrets"
Assert-False $options.containsRawFix "Options raw FIX"
Assert-Equal $options.maxAttemptCount 1 "Options max attempts"
Assert-Equal $options.retryCount 0 "Options retry"
Assert-True $options.rejectsMaxAttemptCountGreaterThanOne "Reject max attempts"
Assert-True $options.rejectsRetryCountGreaterThanZero "Reject retry"
Assert-True $options.rejectsBatchMode "Reject batch"
Assert-True $options.rejectsLoopMode "Reject loop"
Assert-True $options.rejectsProductionAccount "Reject production"
Assert-True $options.rejectsNonApprovedInstrument "Reject non-approved"
Assert-True $options.rejectsUsdJpyWithoutCaveat "Reject USDJPY caveat"

Assert-True $readinessCheck.passed "Final pre-execution readiness"
Assert-True $readinessCheck.r31BlockerFixed "R31 blocker fixed"
Assert-Equal $readinessCheck.concreteBlockerFixed "RequiresLiveLauncherCreationFixedByBoundedExecutor" "Concrete blocker fixed"
Assert-True $readinessCheck.boundedExecutorImplemented "Executor implemented"
Assert-True $readinessCheck.providerCompletenessConfirmed "Provider completeness"
Assert-True $readinessCheck.approvedPathAvailableThroughInjectedAbstractions "Injected abstraction path"
Assert-True $readinessCheck.requiresFutureExplicitOperatorApproval "Future approval"
Assert-False $readinessCheck.r33Authorized "R33 authorized"
Assert-False $readinessCheck.externalActivationExecuted "External activation"

Assert-True $coverage.executorTestsExist "Executor tests"
Assert-True $coverage.fakeExecutionOnly "Fake execution only"
Assert-True $coverage.constructWithoutSocketTcpTlsFixMarketDataTested "Constructor inert test"
Assert-True $coverage.validFutureExecutionRequestWithFakeStackTested "Fake future execution test"
Assert-True $coverage.maxAttemptCountRejectedTested "Max attempt rejected"
Assert-True $coverage.retryRejectedTested "Retry rejected"
Assert-True $coverage.batchLoopRejectedTested "Batch loop rejected"
Assert-True $coverage.productionAccountRejectedTested "Production rejected"
Assert-True $coverage.nonApprovedInstrumentRejectedTested "Non-approved rejected"
Assert-True $coverage.usdJpyWithoutCaveatRejectedTested "USDJPY caveat rejected"
foreach ($property in @("realSocketOpened", "realTcpConnectionAttempted", "realTlsHandshakeAttempted", "realFixLogonAttempted", "realFixMessageSent", "realMarketDataRequestSent", "realCredentialLoadingExecuted")) {
    Assert-False $coverage.$property "Coverage $property"
}

foreach ($property in @("externalRunExecuted", "realSnapshotExecuted", "replayExecuted", "postEndpointInvoked", "realSocketOpened", "realTcpConnectionAttempted", "realTlsHandshakeAttempted", "realFixLogonAttempted", "realFixMessageSent", "realMarketDataRequestSent", "orderSubmissionExecuted", "tradingStateMutated", "schedulerStarted", "pollingStarted", "shadowReplaySubmitted", "apiWorkerStarted", "runtimePoweredUp", "retryExecuted", "batchExecuted", "loopExecuted", "runtimeEnablementExecuted", "tradingEnablementExecuted", "schedulerEnablementExecuted", "orderPathEnablementExecuted", "defaultGatewayRegistrationChanged", "liveConnectionScriptCreated", "realCredentialLoadingExecuted", "hostedServiceAdded", "backgroundWorkerAdded", "apiWorkerWiringAdded", "r33Authorized")) {
    Assert-False $nonRun.$property "Non-run $property"
}
Assert-Equal $nonRun.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Gateway mode"

$codePath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxTemporaryReadOnlyActivationExecutor.cs"
$testPath = Join-Path $RepoRoot "tests\QQ.Production.Intraday.Tests.Unit\LmaxTemporaryReadOnlyActivationExecutorTests.cs"
if (-not (Test-Path -LiteralPath $codePath)) { throw "Missing R32 executor code: $codePath" }
if (-not (Test-Path -LiteralPath $testPath)) { throw "Missing R32 executor tests: $testPath" }
$code = Get-Content -LiteralPath $codePath -Raw
$tests = Get-Content -LiteralPath $testPath -Raw
foreach ($needle in @("LmaxTemporaryReadOnlyActivationExecutor", "LmaxTemporaryReadOnlyActivationExecutorOptions", "LmaxTemporaryReadOnlyActivationExecutorResult", "ExecuteOnce", "RequiresLiveLauncherCreationFixedByBoundedExecutor")) {
    Assert-TextContains $code $needle "R32 code token"
}
foreach ($needle in @("Executor_can_be_constructed_without_socket_tcp_tls_fix_marketdata_credentials_or_worker_startup", "Valid_future_execution_request_passes_local_validation_with_fake_stack", "Executor_is_not_a_live_launcher_and_has_no_main_or_hosted_service_behavior")) {
    Assert-TextContains $tests $needle "R32 test token"
}
foreach ($pattern in @("static\s+(async\s+)?Task\s+Main", "static\s+void\s+Main", "AddHostedService", "BackgroundService", "while\s*\(\s*true\s*\)", "TcpClient", "System\.Net\.Sockets", "SslStream", "NetworkStream", "AuthenticateAsClient", "SendAsync")) {
    Assert-TextNotContainsPattern $code $pattern "R32 executor code must not include launcher/live execution token"
}

$programPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\Program.cs"
$workerPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Worker\Program.cs"
$settingsPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\appsettings.json"
$program = Get-Content -LiteralPath $programPath -Raw
$worker = Get-Content -LiteralPath $workerPath -Raw
$settings = Get-Content -LiteralPath $settingsPath -Raw
Assert-TextContains $program "FakeLmaxGateway" "API default gateway"
foreach ($pattern in @("LmaxTemporaryReadOnlyActivationExecutor", "phase-lmax-r32")) {
    Assert-TextNotContainsPattern $program $pattern "API wiring must not include R32 executor"
    Assert-TextNotContainsPattern $worker $pattern "Worker wiring must not include R32 executor"
    Assert-TextNotContainsPattern $settings $pattern "Default config must not include R32 executor"
}

$validation = [ordered]@{
    phase = "LMAX-R32"
    validator = "scripts/check-lmax-r32-concrete-final-pre-execution-blocker-fix-gate.ps1"
    validationPassed = $true
    finalDecision = "LMAX_R32_BOUNDED_TEMPORARY_EXECUTOR_IMPLEMENTED_NO_EXTERNAL_ACTIVATION"
    r31DecisionConfirmed = $true
    boundedExecutorCodeExists = $true
    broadLiveLauncherExists = $false
    mainOrConsoleLauncherAdded = $false
    connectionOpeningScriptAdded = $false
    apiEndpointAdded = $false
    hostedServiceAdded = $false
    backgroundWorkerAdded = $false
    apiWorkerWiringAdded = $false
    defaultConfigChanged = $false
    finalPreExecutionReadinessPassed = $true
    externalRunExecuted = $false
    realSnapshotExecuted = $false
    replayExecuted = $false
    postEndpointInvoked = $false
    realSocketOpened = $false
    realTcpConnectionAttempted = $false
    realTlsHandshakeAttempted = $false
    realFixLogonAttempted = $false
    realFixMessageSent = $false
    realMarketDataRequestSent = $false
    realCredentialLoadingExecuted = $false
    runtimePoweredUp = $false
    r33Authorized = $false
    apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
    recommendedNextPhase = "Phase LMAX-R33 - Operator-Approved Single Temporary Demo Read-Only Activation Using Bounded Executor"
}

$validationPath = Join-Path $readiness "phase-lmax-r32-gate-validation.json"
($validation | ConvertTo-Json -Depth 8) + [Environment]::NewLine | Set-Content -LiteralPath $validationPath -Encoding UTF8
Assert-NoSensitiveContent $validationPath

Write-Host "LMAX-R32 gate validation passed."
Write-Host "Wrote $validationPath"
