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
    "phase-lmax-r21-gate-validation.json"
)

$requiredR22 = @(
    "phase-lmax-r22-real-dependency-providers-summary.json",
    "phase-lmax-r22-socket-tls-provider-summary.json",
    "phase-lmax-r22-fix-provider-summary.json",
    "phase-lmax-r22-marketdata-provider-summary.json",
    "phase-lmax-r22-credential-provider-policy.json",
    "phase-lmax-r22-readonly-provider-interface-safety-proof.json",
    "phase-lmax-r22-test-double-coverage-summary.json",
    "phase-lmax-r22-no-external-activation-proof.json",
    "phase-lmax-r22-decision-gate.json",
    "phase-lmax-r22-non-run-validation.json",
    "phase-lmax-r22-full-real-stack-execution-boundary-remediation-report.md",
    "phase-lmax-r22-operator-note.md"
)

Write-Host "LMAX-R22 Full Real Stack Execution Boundary Remediation Gate Validator"
Write-Host "This validator performs no external run, real snapshot, replay, POST endpoint, socket, runtime activation, API/Worker startup, secret loading, or config mutation."

Assert-RequiredArtifacts $readiness $requiredPrior
Assert-RequiredArtifacts $readiness $requiredR22

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

$r21Gate = Read-Json (Join-Path $readiness "phase-lmax-r21-decision-gate.json")
$summary = Read-Json (Join-Path $readiness "phase-lmax-r22-real-dependency-providers-summary.json")
$socketTls = Read-Json (Join-Path $readiness "phase-lmax-r22-socket-tls-provider-summary.json")
$fix = Read-Json (Join-Path $readiness "phase-lmax-r22-fix-provider-summary.json")
$marketData = Read-Json (Join-Path $readiness "phase-lmax-r22-marketdata-provider-summary.json")
$accessPolicy = Read-Json (Join-Path $readiness "phase-lmax-r22-credential-provider-policy.json")
$interfaceProof = Read-Json (Join-Path $readiness "phase-lmax-r22-readonly-provider-interface-safety-proof.json")
$coverage = Read-Json (Join-Path $readiness "phase-lmax-r22-test-double-coverage-summary.json")
$nonRun = Read-Json (Join-Path $readiness "phase-lmax-r22-non-run-validation.json")
$gate = Read-Json (Join-Path $readiness "phase-lmax-r22-decision-gate.json")
$t7Gate = Read-Json $t7GatePath
$phase7nGate = Read-Json $phase7nGatePath

Assert-Equal $r21Gate.finalDecision "LMAX_R21_FAIL_NO_EXECUTABLE_REAL_DEPENDENCY_STACK_AVAILABLE" "R21 decision"
Assert-Equal $t7Gate.finalDecision "USDJPY_T7_FINAL_READINESS_ARCHIVE_CLOSED_WITH_CAVEAT" "USDJPY T7 decision"
Assert-Equal $phase7nGate.finalDecision "PASS_FINAL_LMAX_READONLY_RUNTIME_EVIDENCE_ARCHIVE_CLOSED" "Phase 7N decision"
Assert-Equal $gate.finalDecision "LMAX_R22_REAL_DEPENDENCY_PROVIDERS_IMPLEMENTED_WITH_TEST_DOUBLES_NO_EXTERNAL_ACTIVATION" "R22 decision"
Assert-False $gate.r23Authorized "R23 authorization"

Assert-Equal $summary.contextDecision "LMAX_R21_FAIL_NO_EXECUTABLE_REAL_DEPENDENCY_STACK_AVAILABLE" "R22 context"
Assert-Equal $summary.r21FullRealStackValidationResult "FailedNoApprovedExecutableRealDependencyProviders" "R21 blocker"
foreach ($provider in @("LmaxRealSocketProvider", "LmaxRealTlsStreamProvider", "LmaxRealFixFrameProvider", "LmaxRealMarketDataFrameProvider", "LmaxRealCredentialConfigProvider", "LmaxRealReadOnlyDependencyProviderSet", "LmaxRealReadOnlyDependencyProviderFactory")) {
    if (@($summary.providerClasses | Where-Object { $_ -eq $provider }).Count -ne 1) {
        throw "Missing R22 provider summary: $provider"
    }
}
foreach ($property in @("apiWorkerWiringAdded", "defaultConfigChanged", "liveLauncherCreated", "hostedServiceAdded", "backgroundWorkerAdded", "externalRunExecuted", "realCredentialLoadingExecuted", "r23Authorized")) {
    Assert-False $summary.$property "Summary $property"
}

Assert-False $socketTls.socketBoundary.constructorOpensSocket "Socket constructor opens"
Assert-False $socketTls.socketBoundary.constructorAttemptsConnection "Socket constructor connects"
Assert-True $socketTls.socketBoundary.supportsCancellation "Socket cancellation"
Assert-False $socketTls.tlsBoundary.constructorPerformsHandshake "TLS constructor handshake"
Assert-True $socketTls.tlsBoundary.supportsCancellation "TLS cancellation"
foreach ($property in @("realSocketOpened", "realTcpConnectionAttempted", "realTlsHandshakeAttempted")) {
    Assert-False $socketTls.$property "Socket/TLS $property"
}

Assert-False $fix.constructorSendsFixMessages "FIX constructor sends"
Assert-False $fix.constructorLogsOn "FIX constructor logon"
Assert-False $fix.constructorLoadsSecrets "FIX constructor secrets"
Assert-True $fix.readOnlySessionLevelAndMarketDataCategoriesOnly "FIX read-only categories"
foreach ($property in @("orderMessagesExposed", "tradeCaptureMessagesExposed", "orderStatusMessagesExposed", "replayMessagesExposed", "realFixLogonAttempted")) {
    Assert-False $fix.$property "FIX $property"
}

Assert-True $marketData.nonApprovedInstrumentRejectedBeforeRequestConstruction "MarketData non-approved rejection"
Assert-True $marketData.usdJpyCaveatPreserved "MarketData USDJPY"
Assert-True $marketData.readOnlyMarketDataIntentRepresented "MarketData intent"
Assert-False $marketData.realMarketDataRequestSent "MarketData real request"
foreach ($symbol in @("GBPUSD", "EURGBP", "AUDUSD", "USDJPY")) {
    if (@($marketData.approvedInstruments | Where-Object { $_.symbol -eq $symbol }).Count -ne 1) {
        throw "MarketData summary missing approved instrument: $symbol"
    }
}

Assert-False $accessPolicy.constructorLoadsRealSecrets "Access constructor secrets"
Assert-False $accessPolicy.r22TestsLoadRealSecrets "R22 real secret tests"
Assert-True $accessPolicy.futureR23AccessRequiresExplicitApproval "R23 explicit approval"
Assert-True $accessPolicy.demoReadOnlyOnly "Demo/read-only"
Assert-True $accessPolicy.productionAccountBlocked "Production account block"
foreach ($property in @("sensitiveMaterialReturned", "sensitiveMaterialPrinted", "sensitiveMaterialStored", "realCredentialLoadingExecuted")) {
    Assert-False $accessPolicy.$property "Access policy $property"
}

Assert-True $interfaceProof.reflectionTestsAdded "Reflection tests"
Assert-True $interfaceProof.readOnlyPublicInterfaceConfirmed "Read-only provider interface"
Assert-False $interfaceProof.orderPathExposed "Order path exposed"
Assert-False $interfaceProof.tradingPathExposed "Trading path exposed"
Assert-False $interfaceProof.replayPathExposed "Replay path exposed"

foreach ($property in @("testDoubleRealProviderSetUsed", "realDependencyProvidersExercisedWithFakes", "fakeTcpBoundarySimulated", "fakeTlsBoundarySimulated", "fakeFixBoundarySimulated", "fakeMarketDataBoundarySimulated", "fakeCredentialProviderUsed")) {
    Assert-True $coverage.$property "Coverage $property"
    Assert-True $nonRun.$property "Non-run $property"
}

foreach ($property in @("externalRunExecuted", "realSnapshotExecuted", "replayExecuted", "postEndpointInvoked", "realSocketOpened", "realTcpConnectionAttempted", "realTlsHandshakeAttempted", "realFixLogonAttempted", "realMarketDataRequestSent", "orderSubmissionExecuted", "tradingStateMutated", "schedulerStarted", "pollingStarted", "shadowReplaySubmitted", "apiWorkerStarted", "runtimePoweredUp", "retryExecuted", "batchExecuted", "loopExecuted", "runtimeEnablementExecuted", "tradingEnablementExecuted", "schedulerEnablementExecuted", "orderPathEnablementExecuted", "defaultGatewayRegistrationChanged", "liveConnectionScriptCreated", "realCredentialLoadingExecuted", "hostedServiceAdded", "backgroundWorkerAdded", "apiWorkerWiringAdded", "r23Authorized")) {
    Assert-False $nonRun.$property "Non-run $property"
}
Assert-Equal $nonRun.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Gateway mode"

$codePath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxExecutableReadOnlyRealDependencyProviders.cs"
$testPath = Join-Path $RepoRoot "tests\QQ.Production.Intraday.Tests.Unit\LmaxExecutableReadOnlyRealDependencyProvidersTests.cs"
if (-not (Test-Path -LiteralPath $codePath)) {
    throw "Missing R22 code file: $codePath"
}
if (-not (Test-Path -LiteralPath $testPath)) {
    throw "Missing R22 test file: $testPath"
}
$code = Get-Content -LiteralPath $codePath -Raw
$tests = Get-Content -LiteralPath $testPath -Raw
foreach ($needle in @("LmaxRealSocketProvider", "LmaxRealTlsStreamProvider", "LmaxRealFixFrameProvider", "LmaxRealMarketDataFrameProvider", "LmaxRealCredentialConfigProvider", "LmaxRealReadOnlyDependencyProviderSet", "LmaxRealReadOnlyDependencyProviderFactory", "ILmaxRealReadOnlySocketBoundaryProvider", "ILmaxRealReadOnlyTlsBoundaryProvider", "ILmaxRealReadOnlyFixFrameBoundaryProvider", "ILmaxRealReadOnlyMarketDataFrameBoundaryProvider", "ILmaxRealReadOnlyCredentialConfigBoundaryProvider")) {
    Assert-TextContains $code $needle "R22 code token"
}
foreach ($pattern in @("TcpClient", "System\.Net\.Sockets", "new\s+Socket", "SslStream", "NetworkStream", "AddHostedService", "NewOrderSingle", "OrderCancelRequest", "OrderStatusRequest", "TradeCaptureReportRequest", "SubmitOrder")) {
    Assert-TextNotContainsPattern $code $pattern "R22 provider code must not include live or forbidden token"
}
foreach ($needle in @("Provider_set_can_be_constructed_without_opening_sockets_or_loading_real_credentials", "Fake_success_path_exercises_real_providers_without_real_external_action", "Unsafe_scope_fails_before_real_provider_execution", "Public_provider_surface_exposes_no_order_trading_replay_or_mutation_methods")) {
    Assert-TextContains $tests $needle "R22 test token"
}

$programPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\Program.cs"
$workerPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Worker\Program.cs"
$settingsPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\appsettings.json"
$program = Get-Content -LiteralPath $programPath -Raw
$worker = Get-Content -LiteralPath $workerPath -Raw
$settings = Get-Content -LiteralPath $settingsPath -Raw
Assert-TextContains $program "FakeLmaxGateway" "API default gateway"
foreach ($pattern in @("LmaxRealReadOnlyDependencyProviderFactory", "LmaxRealSocketProvider", "LmaxRealTlsStreamProvider", "LmaxRealFixFrameProvider", "LmaxRealMarketDataFrameProvider", "LmaxRealCredentialConfigProvider")) {
    Assert-TextNotContainsPattern $program $pattern "API wiring must not include R22 provider"
    Assert-TextNotContainsPattern $worker $pattern "Worker wiring must not include R22 provider"
    Assert-TextNotContainsPattern $settings $pattern "Default config must not include R22 provider"
}

$validation = [ordered]@{
    phase = "LMAX-R22"
    validator = "scripts/check-lmax-r22-full-real-stack-execution-boundary-remediation-gate.ps1"
    validationPassed = $true
    finalDecision = "LMAX_R22_REAL_DEPENDENCY_PROVIDERS_IMPLEMENTED_WITH_TEST_DOUBLES_NO_EXTERNAL_ACTIVATION"
    r21DecisionConfirmed = $true
    allRequiredArtifactsExist = $true
    realDependencyProviderCodeExists = $true
    socketTlsFixMarketDataCredentialProvidersExist = $true
    readonlyProviderInterfaceSafetyProofExists = $true
    testDoubleCoverageExists = $true
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
    r23Authorized = $false
    testDoubleRealProviderSetUsed = $true
    realDependencyProvidersExercisedWithFakes = $true
    fakeTcpBoundarySimulated = $true
    fakeTlsBoundarySimulated = $true
    fakeFixBoundarySimulated = $true
    fakeMarketDataBoundarySimulated = $true
    fakeCredentialProviderUsed = $true
    apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
    recommendedNextPhase = "Phase LMAX-R23 - Operator-Approved Single Temporary Demo Read-Only Activation With Real Dependency Providers"
}

$validationPath = Join-Path $readiness "phase-lmax-r22-gate-validation.json"
($validation | ConvertTo-Json -Depth 8) + [Environment]::NewLine | Set-Content -LiteralPath $validationPath -Encoding UTF8
Assert-NoSensitiveContent $validationPath

Write-Host "LMAX-R22 gate validation passed."
Write-Host "Wrote $validationPath"
