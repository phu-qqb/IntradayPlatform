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
    "phase-lmax-r25-gate-validation.json"
)

$requiredR26 = @(
    "phase-lmax-r26-tls-provider-fix-summary.json",
    "phase-lmax-r26-tls-provider-implementation-summary.json",
    "phase-lmax-r26-tls-options-sanitization-summary.json",
    "phase-lmax-r26-test-coverage-summary.json",
    "phase-lmax-r26-no-external-activation-proof.json",
    "phase-lmax-r26-decision-gate.json",
    "phase-lmax-r26-non-run-validation.json",
    "phase-lmax-r26-concrete-final-execution-boundary-fix-report.md",
    "phase-lmax-r26-operator-note.md"
)

Write-Host "LMAX-R26 Concrete Final Execution Boundary Fix Gate Validator"
Write-Host "This validator performs no external run, socket open, TCP attempt, TLS handshake, FIX, MarketDataRequest, API/Worker startup, or config mutation."

Assert-RequiredArtifacts $readiness $requiredPrior
Assert-RequiredArtifacts $readiness $requiredR26

$t7GatePath = Join-Path $usdJpy "phase-usdjpy-t7-final-closure-gate.json"
$phase7nGatePath = Join-Path $RepoRoot "artifacts\readiness\phase7n-final-lmax-readonly-runtime-evidence-archive-closure-gate.json"
if (-not (Test-Path -LiteralPath $t7GatePath)) {
    throw "Missing USDJPY T7 closure gate: $t7GatePath"
}
if (-not (Test-Path -LiteralPath $phase7nGatePath)) {
    throw "Missing Phase 7N closure gate: $phase7nGatePath"
}
Assert-NoSensitiveContent $t7GatePath
Assert-NoSensitiveContent $phase7nGatePath

$r25Gate = Read-Json (Join-Path $readiness "phase-lmax-r25-decision-gate.json")
$summary = Read-Json (Join-Path $readiness "phase-lmax-r26-tls-provider-fix-summary.json")
$implementation = Read-Json (Join-Path $readiness "phase-lmax-r26-tls-provider-implementation-summary.json")
$options = Read-Json (Join-Path $readiness "phase-lmax-r26-tls-options-sanitization-summary.json")
$coverage = Read-Json (Join-Path $readiness "phase-lmax-r26-test-coverage-summary.json")
$nonRun = Read-Json (Join-Path $readiness "phase-lmax-r26-non-run-validation.json")
$gate = Read-Json (Join-Path $readiness "phase-lmax-r26-decision-gate.json")
$t7Gate = Read-Json $t7GatePath
$phase7nGate = Read-Json $phase7nGatePath

Assert-Equal $r25Gate.finalDecision "LMAX_R25_FAIL_TLS_PROVIDER_NOT_EXECUTABLE" "R25 decision"
Assert-Equal $t7Gate.finalDecision "USDJPY_T7_FINAL_READINESS_ARCHIVE_CLOSED_WITH_CAVEAT" "USDJPY T7 decision"
Assert-Equal $phase7nGate.finalDecision "PASS_FINAL_LMAX_READONLY_RUNTIME_EVIDENCE_ARCHIVE_CLOSED" "Phase 7N decision"
Assert-Equal $gate.finalDecision "LMAX_R26_TLS_PROVIDER_IMPLEMENTED_NO_EXTERNAL_ACTIVATION" "R26 decision"
Assert-False $gate.r27Authorized "R27 authorization"

Assert-Equal $summary.contextDecision "LMAX_R25_FAIL_TLS_PROVIDER_NOT_EXECUTABLE" "R26 context"
Assert-Equal $summary.r25ConcreteAbortCause "TlsProviderNotExecutable" "R25 abort cause"
Assert-True $summary.tlsProviderImplemented "TLS provider implemented"
foreach ($property in @("registeredByDefault", "apiWorkerWiringAdded", "defaultConfigChanged", "liveLauncherCreated", "hostedServiceAdded", "backgroundWorkerAdded", "externalRunExecuted", "realSocketOpened", "realTcpConnectionAttempted", "realTlsHandshakeAttempted", "r27Authorized")) {
    Assert-False $summary.$property "Summary $property"
}

Assert-Equal $implementation.providerClass "LmaxRealReadOnlyTlsBoundaryProvider" "Implementation class"
Assert-Equal $implementation.implements "ILmaxRealReadOnlyTlsBoundaryProvider" "Implementation interface"
Assert-False $implementation.constructorOpensSocket "Constructor socket"
Assert-False $implementation.constructorAttemptsTcp "Constructor TCP"
Assert-False $implementation.constructorCreatesTlsStream "Constructor TLS stream"
Assert-False $implementation.constructorPerformsTlsHandshake "Constructor TLS handshake"
Assert-False $implementation.constructorLoadsCredentials "Constructor credentials"
Assert-False $implementation.constructorCreatesBackgroundTask "Constructor background"
Assert-True $implementation.tlsOpenRequiresExplicitFutureExecutionApproval "Future execution approval"
Assert-True $implementation.supportsCancellation "Cancellation"
Assert-True $implementation.supportsTimeoutConfiguration "Timeout"
Assert-True $implementation.sanitizedEvidenceReturned "Sanitized evidence"
foreach ($property in @("socketOpened", "tcpAttempted", "fixLogonPerformed", "marketDataRequestSent", "globalStateMutated")) {
    Assert-False $implementation.$property "Implementation $property"
}

Assert-True $options.rejectsProductionOrLiveEnvironment "Options non-demo"
Assert-True $options.rejectsMissingReadOnlyFlag "Options read-only"
Assert-True $options.rejectsUnsafeEndpointOrServerName "Options endpoint"
Assert-True $options.rejectsInvalidTimeout "Options timeout"
Assert-True $options.rejectsUnsafeCertificateValidationPolicy "Options certificate policy"
Assert-False $options.containsCredentials "Options credentials"
Assert-False $options.containsSecrets "Options secrets"
Assert-False $options.containsCertificatePrivateMaterial "Options private material"

Assert-True $coverage.tlsProviderTestsExist "TLS tests"
Assert-True $coverage.fakeTlsHandshakeClientUsed "Fake client"
Assert-False $coverage.realSocketOpened "Coverage real socket"
Assert-False $coverage.realTcpConnectionAttempted "Coverage real TCP"
Assert-False $coverage.realTlsHandshakeAttempted "Coverage real TLS"

foreach ($property in @("externalRunExecuted", "realSnapshotExecuted", "replayExecuted", "postEndpointInvoked", "realSocketOpened", "realTcpConnectionAttempted", "realTlsHandshakeAttempted", "realFixLogonAttempted", "realMarketDataRequestSent", "orderSubmissionExecuted", "tradingStateMutated", "schedulerStarted", "pollingStarted", "shadowReplaySubmitted", "apiWorkerStarted", "runtimePoweredUp", "retryExecuted", "batchExecuted", "loopExecuted", "runtimeEnablementExecuted", "tradingEnablementExecuted", "schedulerEnablementExecuted", "orderPathEnablementExecuted", "defaultGatewayRegistrationChanged", "liveConnectionScriptCreated", "realCredentialLoadingExecuted", "hostedServiceAdded", "backgroundWorkerAdded", "apiWorkerWiringAdded", "r27Authorized")) {
    Assert-False $nonRun.$property "Non-run $property"
}
Assert-Equal $nonRun.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Gateway mode"

$codePath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxRealReadOnlyTlsBoundaryProvider.cs"
$testPath = Join-Path $RepoRoot "tests\QQ.Production.Intraday.Tests.Unit\LmaxRealReadOnlyTlsBoundaryProviderTests.cs"
if (-not (Test-Path -LiteralPath $codePath)) {
    throw "Missing R26 TLS provider code: $codePath"
}
if (-not (Test-Path -LiteralPath $testPath)) {
    throw "Missing R26 TLS provider tests: $testPath"
}
$code = Get-Content -LiteralPath $codePath -Raw
$tests = Get-Content -LiteralPath $testPath -Raw
foreach ($needle in @("LmaxRealReadOnlyTlsBoundaryProvider", "ILmaxRealReadOnlyTlsBoundaryProvider", "LmaxReadOnlyTlsConnectionOptions", "ILmaxReadOnlyTlsHandshakeClient", "ExternalTlsHandshakeExecutionApproved")) {
    Assert-TextContains $code $needle "R26 code token"
}
foreach ($pattern in @("TcpClient", "System\.Net\.Sockets", "new\s+Socket", "SslStream", "NetworkStream", "AuthenticateAsClient", "AddHostedService", "NewOrderSingle", "OrderCancelRequest", "OrderStatusRequest", "TradeCaptureReportRequest", "SubmitOrder")) {
    Assert-TextNotContainsPattern $code $pattern "R26 TLS provider code must not include live or forbidden token"
}
foreach ($needle in @("Provider_can_be_constructed_without_opening_socket_creating_tls_stream_or_loading_credentials", "OpenTls_without_future_execution_approval_does_not_call_client", "Unsafe_scope_is_rejected_before_client_use", "Provider_public_surface_exposes_no_fix_marketdata_order_or_runtime_wiring_methods")) {
    Assert-TextContains $tests $needle "R26 test token"
}

$implementationSearch = Select-String -Path (Join-Path $RepoRoot "src\QQ.Production.Intraday.Infrastructure.Lmax\*.cs") -Pattern "class\s+LmaxRealReadOnlyTlsBoundaryProvider\s*:\s*ILmaxRealReadOnlyTlsBoundaryProvider"
if (@($implementationSearch).Count -ne 1) {
    throw "Expected exactly one non-test LmaxRealReadOnlyTlsBoundaryProvider implementation."
}

$programPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\Program.cs"
$workerPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Worker\Program.cs"
$settingsPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\appsettings.json"
$program = Get-Content -LiteralPath $programPath -Raw
$worker = Get-Content -LiteralPath $workerPath -Raw
$settings = Get-Content -LiteralPath $settingsPath -Raw
Assert-TextContains $program "FakeLmaxGateway" "API default gateway"
foreach ($pattern in @("LmaxRealReadOnlyTlsBoundaryProvider", "LmaxReadOnlyTlsConnectionOptions", "ILmaxReadOnlyTlsHandshakeClient", "phase-lmax-r26")) {
    Assert-TextNotContainsPattern $program $pattern "API wiring must not include R26 provider"
    Assert-TextNotContainsPattern $worker $pattern "Worker wiring must not include R26 provider"
    Assert-TextNotContainsPattern $settings $pattern "Default config must not include R26 provider"
}

$validation = [ordered]@{
    phase = "LMAX-R26"
    validator = "scripts/check-lmax-r26-concrete-final-execution-boundary-fix-gate.ps1"
    validationPassed = $true
    finalDecision = "LMAX_R26_TLS_PROVIDER_IMPLEMENTED_NO_EXTERNAL_ACTIVATION"
    r25DecisionConfirmed = $true
    allRequiredArtifactsExist = $true
    concreteTlsProviderCodeExists = $true
    nonTestTlsProviderImplementationExists = $true
    tlsProviderTestsExist = $true
    externalRunExecuted = $false
    realSnapshotExecuted = $false
    replayExecuted = $false
    postEndpointInvoked = $false
    realSocketOpened = $false
    realTcpConnectionAttempted = $false
    realTlsHandshakeAttempted = $false
    realFixLogonAttempted = $false
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
    r27Authorized = $false
    apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
    recommendedNextPhase = "Phase LMAX-R27 - Operator-Approved Single Temporary Demo Read-Only Activation After TLS Provider Fix"
}

$validationPath = Join-Path $readiness "phase-lmax-r26-gate-validation.json"
($validation | ConvertTo-Json -Depth 8) + [Environment]::NewLine | Set-Content -LiteralPath $validationPath -Encoding UTF8
Assert-NoSensitiveContent $validationPath

Write-Host "LMAX-R26 gate validation passed."
Write-Host "Wrote $validationPath"
