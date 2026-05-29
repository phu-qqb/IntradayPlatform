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
        '(?i)credential\s*[:=]\s*[^,\s\}\]]+'
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
    "phase-lmax-r27-gate-validation.json"
)

$requiredR28 = @(
    "phase-lmax-r28-fix-provider-fix-summary.json",
    "phase-lmax-r28-fix-provider-implementation-summary.json",
    "phase-lmax-r28-fix-options-sanitization-summary.json",
    "phase-lmax-r28-test-coverage-summary.json",
    "phase-lmax-r28-no-external-activation-proof.json",
    "phase-lmax-r28-decision-gate.json",
    "phase-lmax-r28-non-run-validation.json",
    "phase-lmax-r28-concrete-final-execution-boundary-fix-report.md",
    "phase-lmax-r28-operator-note.md"
)

Write-Host "LMAX-R28 Concrete Final Execution Boundary Fix Gate Validator"
Write-Host "This validator performs no external run, socket open, TCP attempt, TLS handshake, FIX logon, FIX send, MarketDataRequest, API/Worker startup, or config mutation."

Assert-RequiredArtifacts $readiness $requiredPrior
Assert-RequiredArtifacts $readiness $requiredR28

$t7GatePath = Join-Path $usdJpy "phase-usdjpy-t7-final-closure-gate.json"
$phase7nGatePath = Join-Path $RepoRoot "artifacts\readiness\phase7n-final-lmax-readonly-runtime-evidence-archive-closure-gate.json"
if (-not (Test-Path -LiteralPath $t7GatePath)) { throw "Missing USDJPY T7 closure gate: $t7GatePath" }
if (-not (Test-Path -LiteralPath $phase7nGatePath)) { throw "Missing Phase 7N closure gate: $phase7nGatePath" }
Assert-NoSensitiveContent $t7GatePath
Assert-NoSensitiveContent $phase7nGatePath

$r27Gate = Read-Json (Join-Path $readiness "phase-lmax-r27-decision-gate.json")
$summary = Read-Json (Join-Path $readiness "phase-lmax-r28-fix-provider-fix-summary.json")
$implementation = Read-Json (Join-Path $readiness "phase-lmax-r28-fix-provider-implementation-summary.json")
$options = Read-Json (Join-Path $readiness "phase-lmax-r28-fix-options-sanitization-summary.json")
$coverage = Read-Json (Join-Path $readiness "phase-lmax-r28-test-coverage-summary.json")
$nonRun = Read-Json (Join-Path $readiness "phase-lmax-r28-non-run-validation.json")
$gate = Read-Json (Join-Path $readiness "phase-lmax-r28-decision-gate.json")
$t7Gate = Read-Json $t7GatePath
$phase7nGate = Read-Json $phase7nGatePath

Assert-Equal $r27Gate.finalDecision "LMAX_R27_FAIL_FIX_PROVIDER_NOT_EXECUTABLE" "R27 decision"
Assert-Equal $t7Gate.finalDecision "USDJPY_T7_FINAL_READINESS_ARCHIVE_CLOSED_WITH_CAVEAT" "USDJPY T7 decision"
Assert-Equal $phase7nGate.finalDecision "PASS_FINAL_LMAX_READONLY_RUNTIME_EVIDENCE_ARCHIVE_CLOSED" "Phase 7N decision"
Assert-Equal $gate.finalDecision "LMAX_R28_FIX_PROVIDER_IMPLEMENTED_NO_EXTERNAL_ACTIVATION" "R28 decision"
Assert-False $gate.r29Authorized "R29 authorization"

Assert-Equal $summary.contextDecision "LMAX_R27_FAIL_FIX_PROVIDER_NOT_EXECUTABLE" "R28 context"
Assert-Equal $summary.r27ConcreteAbortCause "FixProviderNotExecutable" "R27 abort cause"
Assert-True $summary.fixProviderImplemented "FIX provider implemented"
foreach ($property in @("registeredByDefault", "apiWorkerWiringAdded", "defaultConfigChanged", "liveLauncherCreated", "hostedServiceAdded", "backgroundWorkerAdded", "externalRunExecuted", "realSocketOpened", "realTcpConnectionAttempted", "realTlsHandshakeAttempted", "realFixLogonAttempted", "realFixMessageSent", "r29Authorized")) {
    Assert-False $summary.$property "Summary $property"
}

Assert-Equal $implementation.providerClass "LmaxRealReadOnlyFixFrameBoundaryProvider" "Implementation class"
Assert-Equal $implementation.implements "ILmaxRealReadOnlyFixFrameBoundaryProvider" "Implementation interface"
Assert-False $implementation.constructorOpensSocket "Constructor socket"
Assert-False $implementation.constructorAttemptsTcp "Constructor TCP"
Assert-False $implementation.constructorCreatesTlsStream "Constructor TLS stream"
Assert-False $implementation.constructorPerformsTlsHandshake "Constructor TLS handshake"
Assert-False $implementation.constructorPerformsFixLogon "Constructor FIX logon"
Assert-False $implementation.constructorSendsFixMessage "Constructor FIX send"
Assert-False $implementation.constructorLoadsCredentials "Constructor credentials"
Assert-False $implementation.constructorCreatesBackgroundTask "Constructor background"
Assert-True $implementation.fixOpenRequiresExplicitFutureExecutionApproval "Future execution approval"
Assert-True $implementation.supportsCancellation "Cancellation"
Assert-True $implementation.sanitizedEvidenceReturned "Sanitized evidence"
Assert-True $implementation.credentialFieldsRedacted "Credential redaction"
Assert-False $implementation.rawFixStored "Raw FIX stored"
foreach ($property in @("socketOpened", "tcpAttempted", "tlsHandshakeAttempted", "marketDataRequestSent", "globalStateMutated")) {
    Assert-False $implementation.$property "Implementation $property"
}

Assert-False $options.containsCredentials "Options credentials"
Assert-False $options.containsSessionPassword "Options session password"
Assert-False $options.containsSecrets "Options secrets"
Assert-False $options.containsRawFix "Options raw FIX"
Assert-True $options.rejectsProductionOrLiveEnvironment "Options non-demo"
Assert-True $options.rejectsMissingReadOnlyFlag "Options read-only"
Assert-True $options.rejectsUnsafeFixSessionLabel "Options unsafe label"
Assert-True $options.rejectsUnsupportedFixMessageType "Options unsupported FIX"
Assert-True $options.rejectsOrderTradingReplayTradeCaptureOrderStatusMessageTypes "Options forbidden FIX"
Assert-True $options.rejectsNonApprovedInstrument "Options non-approved instrument"
Assert-True $options.rejectsUsdJpyWithoutCaveat "Options USDJPY caveat"

Assert-True $coverage.fixProviderTestsExist "FIX tests"
Assert-True $coverage.fakeFixFrameClientUsed "Fake client"
Assert-False $coverage.realSocketOpened "Coverage real socket"
Assert-False $coverage.realTcpConnectionAttempted "Coverage real TCP"
Assert-False $coverage.realTlsHandshakeAttempted "Coverage real TLS"
Assert-False $coverage.realFixLogonAttempted "Coverage real FIX logon"
Assert-False $coverage.realFixMessageSent "Coverage real FIX send"

foreach ($property in @("externalRunExecuted", "realSnapshotExecuted", "replayExecuted", "postEndpointInvoked", "realSocketOpened", "realTcpConnectionAttempted", "realTlsHandshakeAttempted", "realFixLogonAttempted", "realFixMessageSent", "realMarketDataRequestSent", "orderSubmissionExecuted", "tradingStateMutated", "schedulerStarted", "pollingStarted", "shadowReplaySubmitted", "apiWorkerStarted", "runtimePoweredUp", "retryExecuted", "batchExecuted", "loopExecuted", "runtimeEnablementExecuted", "tradingEnablementExecuted", "schedulerEnablementExecuted", "orderPathEnablementExecuted", "defaultGatewayRegistrationChanged", "liveConnectionScriptCreated", "realCredentialLoadingExecuted", "hostedServiceAdded", "backgroundWorkerAdded", "apiWorkerWiringAdded", "r29Authorized")) {
    Assert-False $nonRun.$property "Non-run $property"
}
Assert-Equal $nonRun.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Gateway mode"

$codePath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxRealReadOnlyFixFrameBoundaryProvider.cs"
$testPath = Join-Path $RepoRoot "tests\QQ.Production.Intraday.Tests.Unit\LmaxRealReadOnlyFixFrameBoundaryProviderTests.cs"
if (-not (Test-Path -LiteralPath $codePath)) { throw "Missing R28 FIX provider code: $codePath" }
if (-not (Test-Path -LiteralPath $testPath)) { throw "Missing R28 FIX provider tests: $testPath" }
$code = Get-Content -LiteralPath $codePath -Raw
$tests = Get-Content -LiteralPath $testPath -Raw
foreach ($needle in @("LmaxRealReadOnlyFixFrameBoundaryProvider", "ILmaxRealReadOnlyFixFrameBoundaryProvider", "LmaxReadOnlyFixSessionOptions", "ILmaxReadOnlyFixFrameClient", "ExternalFixExecutionApproved")) {
    Assert-TextContains $code $needle "R28 code token"
}
foreach ($pattern in @("TcpClient", "System\.Net\.Sockets", "new\s+Socket", "SslStream", "NetworkStream", "AuthenticateAsClient", "SendAsync", "AddHostedService")) {
    Assert-TextNotContainsPattern $code $pattern "R28 FIX provider code must not include live execution token"
}
foreach ($needle in @("Provider_can_be_constructed_without_opening_socket_tls_fix_or_loading_credentials", "OpenSessionLogon_without_future_execution_approval_does_not_call_client", "Unsupported_order_or_trading_fix_message_type_is_rejected", "UsdJpy_without_caveat_is_rejected_before_client_use")) {
    Assert-TextContains $tests $needle "R28 test token"
}

$implementationSearch = Select-String -Path (Join-Path $RepoRoot "src\QQ.Production.Intraday.Infrastructure.Lmax\*.cs") -Pattern "class\s+LmaxRealReadOnlyFixFrameBoundaryProvider\s*:\s*ILmaxRealReadOnlyFixFrameBoundaryProvider"
if (@($implementationSearch).Count -ne 1) {
    throw "Expected exactly one non-test LmaxRealReadOnlyFixFrameBoundaryProvider implementation."
}

$programPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\Program.cs"
$workerPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Worker\Program.cs"
$settingsPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\appsettings.json"
$program = Get-Content -LiteralPath $programPath -Raw
$worker = Get-Content -LiteralPath $workerPath -Raw
$settings = Get-Content -LiteralPath $settingsPath -Raw
Assert-TextContains $program "FakeLmaxGateway" "API default gateway"
foreach ($pattern in @("LmaxRealReadOnlyFixFrameBoundaryProvider", "LmaxReadOnlyFixSessionOptions", "ILmaxReadOnlyFixFrameClient", "phase-lmax-r28")) {
    Assert-TextNotContainsPattern $program $pattern "API wiring must not include R28 provider"
    Assert-TextNotContainsPattern $worker $pattern "Worker wiring must not include R28 provider"
    Assert-TextNotContainsPattern $settings $pattern "Default config must not include R28 provider"
}

$validation = [ordered]@{
    phase = "LMAX-R28"
    validator = "scripts/check-lmax-r28-concrete-final-execution-boundary-fix-gate.ps1"
    validationPassed = $true
    finalDecision = "LMAX_R28_FIX_PROVIDER_IMPLEMENTED_NO_EXTERNAL_ACTIVATION"
    r27DecisionConfirmed = $true
    allRequiredArtifactsExist = $true
    concreteFixProviderCodeExists = $true
    nonTestFixProviderImplementationExists = $true
    fixProviderTestsExist = $true
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
    orderSubmissionExecuted = $false
    tradingStateMutated = $false
    schedulerStarted = $false
    pollingStarted = $false
    shadowReplaySubmitted = $false
    apiWorkerStarted = $false
    runtimePoweredUp = $false
    defaultGatewayRegistrationChanged = $false
    liveConnectionScriptCreated = $false
    realCredentialLoadingExecuted = $false
    hostedServiceAdded = $false
    backgroundWorkerAdded = $false
    apiWorkerWiringAdded = $false
    r29Authorized = $false
    apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
    recommendedNextPhase = "Phase LMAX-R29 - Operator-Approved Single Temporary Demo Read-Only Activation After FIX Provider Fix"
}

$validationPath = Join-Path $readiness "phase-lmax-r28-gate-validation.json"
($validation | ConvertTo-Json -Depth 8) + [Environment]::NewLine | Set-Content -LiteralPath $validationPath -Encoding UTF8
Assert-NoSensitiveContent $validationPath

Write-Host "LMAX-R28 gate validation passed."
Write-Host "Wrote $validationPath"
