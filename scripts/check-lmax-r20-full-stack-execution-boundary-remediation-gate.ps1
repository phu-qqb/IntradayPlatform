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
    "phase-lmax-r19-gate-validation.json"
)

$requiredR20 = @(
    "phase-lmax-r20-real-low-level-dependencies-summary.json",
    "phase-lmax-r20-socket-tls-dependency-summary.json",
    "phase-lmax-r20-fix-session-dependency-summary.json",
    "phase-lmax-r20-marketdata-dependency-summary.json",
    "phase-lmax-r20-credential-dependency-policy.json",
    "phase-lmax-r20-readonly-interface-safety-proof.json",
    "phase-lmax-r20-test-double-coverage-summary.json",
    "phase-lmax-r20-no-external-activation-proof.json",
    "phase-lmax-r20-decision-gate.json",
    "phase-lmax-r20-non-run-validation.json",
    "phase-lmax-r20-full-stack-execution-boundary-remediation-report.md",
    "phase-lmax-r20-operator-note.md"
)

Write-Host "LMAX-R20 Full Stack Execution Boundary Remediation Gate Validator"
Write-Host "This validator performs no external run, real snapshot, replay, POST endpoint, socket, runtime activation, API/Worker startup, secret loading, or config mutation."

Assert-RequiredArtifacts $readiness $requiredPrior
Assert-RequiredArtifacts $readiness $requiredR20

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

$r19Gate = Read-Json (Join-Path $readiness "phase-lmax-r19-decision-gate.json")
$r20Summary = Read-Json (Join-Path $readiness "phase-lmax-r20-real-low-level-dependencies-summary.json")
$socketTls = Read-Json (Join-Path $readiness "phase-lmax-r20-socket-tls-dependency-summary.json")
$fix = Read-Json (Join-Path $readiness "phase-lmax-r20-fix-session-dependency-summary.json")
$marketData = Read-Json (Join-Path $readiness "phase-lmax-r20-marketdata-dependency-summary.json")
$secretPolicy = Read-Json (Join-Path $readiness "phase-lmax-r20-credential-dependency-policy.json")
$interfaceProof = Read-Json (Join-Path $readiness "phase-lmax-r20-readonly-interface-safety-proof.json")
$coverage = Read-Json (Join-Path $readiness "phase-lmax-r20-test-double-coverage-summary.json")
$nonRun = Read-Json (Join-Path $readiness "phase-lmax-r20-non-run-validation.json")
$gate = Read-Json (Join-Path $readiness "phase-lmax-r20-decision-gate.json")
$t7Gate = Read-Json $t7GatePath
$phase7nGate = Read-Json $phase7nGatePath

Assert-Equal $r19Gate.finalDecision "LMAX_R19_FAIL_NO_EXECUTABLE_LOW_LEVEL_STACK_AVAILABLE" "R19 decision"
Assert-Equal $t7Gate.finalDecision "USDJPY_T7_FINAL_READINESS_ARCHIVE_CLOSED_WITH_CAVEAT" "USDJPY T7 decision"
Assert-Equal $phase7nGate.finalDecision "PASS_FINAL_LMAX_READONLY_RUNTIME_EVIDENCE_ARCHIVE_CLOSED" "Phase 7N decision"
Assert-Equal $gate.finalDecision "LMAX_R20_REAL_LOW_LEVEL_DEPENDENCIES_IMPLEMENTED_WITH_TEST_DOUBLES_NO_EXTERNAL_ACTIVATION" "R20 decision"
Assert-False $gate.r21Authorized "R21 authorization"

Assert-Equal $r20Summary.contextDecision "LMAX_R19_FAIL_NO_EXECUTABLE_LOW_LEVEL_STACK_AVAILABLE" "R20 context"
Assert-Equal $r20Summary.r19FullStackValidationResult "FailedNoApprovedExecutableRealLowLevelDependencies" "R19 blocker"
foreach ($component in @("LmaxRealReadOnlySocketDependency", "LmaxRealReadOnlyFixSessionDependency", "LmaxRealReadOnlyMarketDataDependency", "LmaxRealReadOnlyCredentialDependency", "LmaxRealReadOnlyLowLevelDependencySet", "LmaxRealReadOnlyLowLevelDependencyFactory")) {
    if (@($r20Summary.components | Where-Object { $_ -eq $component }).Count -ne 1) {
        throw "Missing R20 component summary: $component"
    }
}
Assert-False $r20Summary.apiWorkerWiringAdded "Summary API/Worker wiring"
Assert-False $r20Summary.defaultConfigChanged "Summary default config"
Assert-False $r20Summary.liveLauncherCreated "Summary live launcher"
Assert-False $r20Summary.externalRunExecuted "Summary external run"
Assert-False $r20Summary.realCredentialLoadingExecuted "Summary real secret loading"

Assert-False $socketTls.tcpBoundary.constructorOpensSocket "TCP constructor socket"
Assert-False $socketTls.tcpBoundary.constructorAttemptsConnection "TCP constructor connection"
Assert-True $socketTls.tcpBoundary.supportsCancellation "TCP cancellation"
Assert-False $socketTls.tlsBoundary.constructorPerformsHandshake "TLS constructor handshake"
Assert-True $socketTls.tlsBoundary.requiresTcpBoundarySuccessFirst "TLS requires TCP"
Assert-False $socketTls.realSocketOpened "Socket opened"
Assert-False $socketTls.realTcpConnectionAttempted "TCP attempted"
Assert-False $socketTls.realTlsHandshakeAttempted "TLS attempted"

Assert-False $fix.constructorLogsOn "FIX constructor logon"
Assert-False $fix.constructorLoadsSecrets "FIX constructor secrets"
Assert-True $fix.readOnlySessionLevelOnly "FIX read-only"
Assert-False $fix.orderMessagesExposed "FIX order messages"
Assert-False $fix.tradeCaptureMessagesExposed "FIX trade capture"
Assert-False $fix.orderStatusMessagesExposed "FIX order status"
Assert-False $fix.replayMessagesExposed "FIX replay"
Assert-False $fix.realFixLogonAttempted "FIX real logon"

Assert-True $marketData.nonApprovedInstrumentRejectedBeforeRequestConstruction "MarketData non-approved rejection"
Assert-True $marketData.usdJpyCaveatPreserved "MarketData USDJPY caveat"
Assert-True $marketData.readOnlyMarketDataIntentRepresented "MarketData read-only intent"
Assert-False $marketData.realMarketDataRequestSent "MarketData real request"
foreach ($symbol in @("GBPUSD", "EURGBP", "AUDUSD", "USDJPY")) {
    if (@($marketData.approvedInstruments | Where-Object { $_.symbol -eq $symbol }).Count -ne 1) {
        throw "MarketData summary missing approved instrument: $symbol"
    }
}

Assert-False $secretPolicy.constructorLoadsRealSecrets "Secret constructor loading"
Assert-False $secretPolicy.r20TestsLoadRealSecrets "R20 real secret tests"
Assert-True $secretPolicy.futureR21AccessRequiresExplicitApproval "R21 explicit approval"
Assert-True $secretPolicy.demoReadOnlyOnly "Demo/read-only policy"
Assert-True $secretPolicy.productionAccountBlocked "Production account block"
Assert-False $secretPolicy.sensitiveMaterialReturned "Sensitive material returned"
Assert-False $secretPolicy.sensitiveMaterialPrinted "Sensitive material printed"
Assert-False $secretPolicy.sensitiveMaterialStored "Sensitive material stored"
Assert-False $secretPolicy.realCredentialLoadingExecuted "Real secret loading executed"

Assert-True $interfaceProof.reflectionTestsAdded "Reflection tests"
Assert-True $interfaceProof.readOnlyPublicInterfaceConfirmed "Read-only public interface"
Assert-False $interfaceProof.orderPathExposed "Order path exposed"
Assert-False $interfaceProof.tradingPathExposed "Trading path exposed"
Assert-False $interfaceProof.replayPathExposed "Replay path exposed"

foreach ($property in @("testDoubleRealDependencySetUsed", "realLowLevelDependenciesExercisedWithFakes", "fakeTcpBoundarySimulated", "fakeTlsBoundarySimulated", "fakeFixBoundarySimulated", "fakeMarketDataBoundarySimulated", "fakeCredentialProviderUsed")) {
    Assert-True $coverage.$property "Coverage $property"
    Assert-True $nonRun.$property "Non-run $property"
}

foreach ($property in @("externalRunExecuted", "realSnapshotExecuted", "replayExecuted", "postEndpointInvoked", "realSocketOpened", "realTcpConnectionAttempted", "realTlsHandshakeAttempted", "realFixLogonAttempted", "realMarketDataRequestSent", "orderSubmissionExecuted", "tradingStateMutated", "schedulerStarted", "pollingStarted", "shadowReplaySubmitted", "apiWorkerStarted", "runtimePoweredUp", "retryExecuted", "batchExecuted", "loopExecuted", "runtimeEnablementExecuted", "tradingEnablementExecuted", "schedulerEnablementExecuted", "orderPathEnablementExecuted", "defaultGatewayRegistrationChanged", "liveConnectionScriptCreated", "realCredentialLoadingExecuted", "hostedServiceAdded", "backgroundWorkerAdded", "apiWorkerWiringAdded", "r21Authorized")) {
    Assert-False $nonRun.$property "Non-run $property"
}
Assert-Equal $nonRun.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Non-run gateway mode"

$codePath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxExecutableReadOnlyRealLowLevelDependencies.cs"
$testPath = Join-Path $RepoRoot "tests\QQ.Production.Intraday.Tests.Unit\LmaxExecutableReadOnlyRealLowLevelDependenciesTests.cs"
if (-not (Test-Path -LiteralPath $codePath)) {
    throw "Missing R20 code file: $codePath"
}
if (-not (Test-Path -LiteralPath $testPath)) {
    throw "Missing R20 test file: $testPath"
}
$code = Get-Content -LiteralPath $codePath -Raw
$tests = Get-Content -LiteralPath $testPath -Raw
foreach ($needle in @("LmaxRealReadOnlySocketDependency", "LmaxRealReadOnlyFixSessionDependency", "LmaxRealReadOnlyMarketDataDependency", "LmaxRealReadOnlyCredentialDependency", "LmaxRealReadOnlyLowLevelDependencySet", "LmaxRealReadOnlyLowLevelDependencyFactory", "ILmaxRealReadOnlyTcpConnector", "ILmaxRealReadOnlyTlsAuthenticator", "ILmaxRealReadOnlyFixSessionDriver", "ILmaxRealReadOnlyMarketDataDriver", "ILmaxRealReadOnlySecretProvider")) {
    Assert-TextContains $code $needle "R20 code token"
}
foreach ($pattern in @("TcpClient", "System\.Net\.Sockets", "new\s+Socket", "SslStream", "NetworkStream", "AddHostedService", "NewOrderSingle", "OrderCancelRequest", "OrderStatusRequest", "TradeCaptureReportRequest", "SubmitOrder")) {
    Assert-TextNotContainsPattern $code $pattern "R20 code must not include live or forbidden token"
}
foreach ($needle in @("Real_dependency_set_can_be_constructed_without_opening_sockets_or_loading_real_secrets", "Fake_success_path_exercises_real_dependency_set_without_real_external_action", "Unsafe_scope_fails_before_real_low_level_execution", "Public_real_dependency_interfaces_expose_no_order_trading_replay_or_mutation_methods")) {
    Assert-TextContains $tests $needle "R20 test token"
}

$programPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\Program.cs"
$settingsPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\appsettings.json"
$program = Get-Content -LiteralPath $programPath -Raw
$settings = Get-Content -LiteralPath $settingsPath -Raw
Assert-TextContains $program "FakeLmaxGateway" "API default gateway"
foreach ($pattern in @("LmaxRealReadOnlyLowLevelDependencyFactory", "LmaxRealReadOnlySocketDependency", "LmaxRealReadOnlyFixSessionDependency", "LmaxRealReadOnlyMarketDataDependency", "LmaxRealReadOnlyCredentialDependency", "LmaxExecutableReadOnlyRealLowLevelDependencies")) {
    Assert-TextNotContainsPattern $program $pattern "API/Worker wiring must not include R20 dependency"
    Assert-TextNotContainsPattern $settings $pattern "Default config must not include R20 dependency"
}

$validation = [ordered]@{
    phase = "LMAX-R20"
    validator = "scripts/check-lmax-r20-full-stack-execution-boundary-remediation-gate.ps1"
    validationPassed = $true
    finalDecision = "LMAX_R20_REAL_LOW_LEVEL_DEPENDENCIES_IMPLEMENTED_WITH_TEST_DOUBLES_NO_EXTERNAL_ACTIVATION"
    r19DecisionConfirmed = $true
    allRequiredArtifactsExist = $true
    realLowLevelDependencyCodeExists = $true
    socketTlsFixMarketDataCredentialBoundariesExist = $true
    readonlyPublicInterfaceSafetyProofExists = $true
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
    r21Authorized = $false
    testDoubleRealDependencySetUsed = $true
    realLowLevelDependenciesExercisedWithFakes = $true
    fakeTcpBoundarySimulated = $true
    fakeTlsBoundarySimulated = $true
    fakeFixBoundarySimulated = $true
    fakeMarketDataBoundarySimulated = $true
    fakeCredentialProviderUsed = $true
    apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
    recommendedNextPhase = "Phase LMAX-R21 - Operator-Approved Single Temporary Demo Read-Only Activation With Full Real Dependency Stack"
}

$validationPath = Join-Path $readiness "phase-lmax-r20-gate-validation.json"
($validation | ConvertTo-Json -Depth 8) + [Environment]::NewLine | Set-Content -LiteralPath $validationPath -Encoding UTF8
Assert-NoSensitiveContent $validationPath

Write-Host "LMAX-R20 gate validation passed."
Write-Host "Wrote $validationPath"
