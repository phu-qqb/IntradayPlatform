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
    "phase-lmax-r13-gate-validation.json"
)

$requiredR14 = @(
    "phase-lmax-r14-real-transport-implementation-summary.json",
    "phase-lmax-r14-readonly-session-client-contract.json",
    "phase-lmax-r14-test-double-coverage-summary.json",
    "phase-lmax-r14-sanitization-and-boundary-evidence-summary.json",
    "phase-lmax-r14-safety-enforcement-summary.json",
    "phase-lmax-r14-no-external-activation-proof.json",
    "phase-lmax-r14-decision-gate.json",
    "phase-lmax-r14-non-run-validation.json",
    "phase-lmax-r14-real-transport-test-double-report.md",
    "phase-lmax-r14-operator-note.md"
)

Write-Host "LMAX-R14 Real Read-Only Transport Test-Double Gate Validator"
Write-Host "This validator performs no external run, real snapshot, replay, POST endpoint, socket, runtime activation, API/Worker startup, credential loading, or config mutation."

Assert-RequiredArtifacts $readiness $requiredPrior
Assert-RequiredArtifacts $readiness $requiredR14

$t7GatePath = Join-Path $usdJpy "phase-usdjpy-t7-final-closure-gate.json"
if (-not (Test-Path -LiteralPath $t7GatePath)) {
    throw "Missing USDJPY T7 closure gate: $t7GatePath"
}
Assert-NoSensitiveContent $t7GatePath

$r13Gate = Read-Json (Join-Path $readiness "phase-lmax-r13-decision-gate.json")
$summary = Read-Json (Join-Path $readiness "phase-lmax-r14-real-transport-implementation-summary.json")
$contract = Read-Json (Join-Path $readiness "phase-lmax-r14-readonly-session-client-contract.json")
$tests = Read-Json (Join-Path $readiness "phase-lmax-r14-test-double-coverage-summary.json")
$sanitization = Read-Json (Join-Path $readiness "phase-lmax-r14-sanitization-and-boundary-evidence-summary.json")
$safety = Read-Json (Join-Path $readiness "phase-lmax-r14-safety-enforcement-summary.json")
$proof = Read-Json (Join-Path $readiness "phase-lmax-r14-no-external-activation-proof.json")
$gate = Read-Json (Join-Path $readiness "phase-lmax-r14-decision-gate.json")
$nonRun = Read-Json (Join-Path $readiness "phase-lmax-r14-non-run-validation.json")
$t7Gate = Read-Json $t7GatePath

Assert-Equal $r13Gate.finalDecision "LMAX_R13_FAIL_NO_REAL_TRANSPORT_IMPLEMENTATION" "R13 decision"
Assert-Equal $t7Gate.finalDecision "USDJPY_T7_FINAL_READINESS_ARCHIVE_CLOSED_WITH_CAVEAT" "USDJPY T7 decision"

Assert-Equal $summary.finalDecision "LMAX_R14_REAL_READONLY_TRANSPORT_IMPLEMENTED_WITH_TEST_DOUBLES_NO_EXTERNAL_ACTIVATION" "Summary decision"
Assert-Equal $summary.implementation "LmaxRealReadOnlyMarketDataTransport" "Summary implementation"
Assert-True $summary.realTransportClassExists "Summary real transport"
Assert-True $summary.readOnlySessionClientAbstractionExists "Summary session client"
Assert-True $summary.implementsR12TransportInterface "Summary R12 interface"
Assert-Equal $summary.transportInterface "ILmaxTemporaryReadOnlyMarketDataTransport" "Summary transport interface"
Assert-Equal $summary.sessionClientInterface "ILmaxReadOnlyMarketDataSessionClient" "Summary session interface"
Assert-True $summary.constructorDependencyInjectionOnly "Summary DI"
foreach ($property in @("opensSocketInConstructor", "loadsCredentialsInConstructor", "startsBackgroundWorker", "apiWorkerWiringAdded", "defaultConfigChanged", "externalActivationExecuted")) {
    Assert-False $summary.$property "Summary $property"
}
Assert-True $summary.approvedInstrumentAllowlistEnforced "Summary allowlist"
Assert-True $summary.usdJpyCaveatPreserved "Summary caveat"
Assert-True $summary.sanitizedResultsOnly "Summary sanitized"
Assert-True $summary.shutdownRevertAfterStartedSession "Summary shutdown"
Assert-True $summary.testDoubleOnlyExercised "Summary test double"

foreach ($method in @("OpenReadOnlyTcpBoundary", "OpenReadOnlyTlsBoundary", "OpenReadOnlyFixLogonBoundary", "RequestReadOnlyMarketData", "ShutdownRevert")) {
    if (-not ($contract.allowedMethods -contains $method)) {
        throw "Read-only session client contract missing allowed method: $method"
    }
}
foreach ($property in @("readOnlyOnly", "doesNotExposeOrderSubmission", "doesNotExposeTradingMutation", "doesNotExposeReplayOrShadowReplay", "doesNotLoadCredentials", "doesNotStartApiWorker", "doesNotRegisterGateways")) {
    Assert-True $contract.$property "Contract $property"
}

foreach ($coverage in @(
    "successful test-double read-only flow",
    "adapter can use real transport class with fake/session client test double",
    "approved instruments only are passed to session client",
    "USDJPY caveat preserved",
    "TCP failure returns sanitized TCP boundary failure",
    "TLS failure returns sanitized TLS boundary failure",
    "FIX logon failure returns sanitized FIX boundary failure",
    "MarketData failure returns sanitized MarketData boundary failure",
    "instrument reject returns sanitized instrument-level status",
    "shutdown/revert called after partial start",
    "unsafe request fails before session client is invoked",
    "no credential loading in constructor",
    "no API/Worker/default config wiring added",
    "no order/trading/replay/mutation method exists on read-only session client abstraction"
)) {
    if (-not ($tests.coverage -contains $coverage)) {
        throw "R14 test coverage missing: $coverage"
    }
}
foreach ($property in @("testDoubleSessionClientUsed", "realTransportClassExercisedWithTestDoubles", "fakeTcpBoundarySimulated", "fakeTlsBoundarySimulated", "fakeFixBoundarySimulated", "fakeMarketDataBoundarySimulated")) {
    Assert-True $tests.$property "Tests $property"
}

foreach ($status in @("NotAttempted", "Succeeded", "Failed", "FakeSucceeded", "FakeFailed")) {
    if (-not ($sanitization.boundaryStatusesSupported -contains $status)) {
        throw "Sanitization summary missing boundary status: $status"
    }
}
foreach ($property in @("passwordsReturned", "sessionPasswordsReturned", "rawCredentialsReturned", "rawFixLogsStored", "tlsSecretsStored", "fullEndpointSecretsReturned", "fullRawConfigReturned")) {
    Assert-False $sanitization.$property "Sanitization $property"
}
Assert-True $sanitization.outputSanitized "Sanitization output"

Assert-True $safety.safetyBeforeSessionClientUseEnforced "Safety before client"
foreach ($condition in @("non-approved instrument present", "USDJPY caveat missing", "production account requested", "orders enabled", "live trading enabled", "IsTradingEnabled=true", "scheduler enabled", "polling enabled", "replay enabled", "shadow replay enabled", "trading mutation enabled", "persistent runtime enablement requested", "default gateway registration change requested", "output sanitization disabled", "shutdown/revert plan missing")) {
    if (-not ($safety.failsBeforeSessionClientUseIf -contains $condition)) {
        throw "Safety summary missing condition: $condition"
    }
}
foreach ($property in @("orderGatewayRegistered", "tradingGatewayRegistered", "schedulerStarted", "pollingStarted", "replayExecuted", "shadowReplaySubmitted", "tradingStateMutated")) {
    Assert-False $safety.$property "Safety $property"
}
Assert-Equal $safety.apiWorkerDefaultGatewayMode "FakeLmaxGatewayOnly" "Safety gateway"

foreach ($property in @("networkTypesReferencedByRealTransport", "socketConstructedByRealTransport", "credentialResolversReferencedByRealTransport", "apiWorkerRegistrationAdded", "hostedServiceAdded", "backgroundWorkerAdded", "liveConnectionScriptCreated", "defaultGatewayRegistrationChanged", "defaultConfigChangedToEnableConnectivity", "externalRunExecuted", "realSnapshotExecuted", "runtimeActivationExecuted", "realSocketOpened", "realTcpConnectionAttempted", "realTlsHandshakeAttempted", "realFixLogonAttempted", "realMarketDataRequestSent", "realCredentialLoadingExecuted")) {
    Assert-False $proof.$property "Proof $property"
}
Assert-Equal $proof.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Proof gateway"

Assert-Equal $gate.finalDecision "LMAX_R14_REAL_READONLY_TRANSPORT_IMPLEMENTED_WITH_TEST_DOUBLES_NO_EXTERNAL_ACTIVATION" "R14 final decision"
Assert-True $gate.realTransportClassExists "Gate transport"
Assert-True $gate.readOnlySessionClientAbstractionExists "Gate session client"
Assert-True $gate.testDoubleCoverageExists "Gate coverage"
Assert-True $gate.testDoubleSessionClientUsed "Gate test double"
Assert-True $gate.realTransportClassExercisedWithTestDoubles "Gate exercised"
Assert-True $gate.approvedInstrumentAllowlistEnforced "Gate allowlist"
Assert-True $gate.usdJpyCaveatPreserved "Gate caveat"
Assert-True $gate.safetyBeforeSessionClientUseEnforced "Gate safety"
Assert-True $gate.sanitizedResultsOnly "Gate sanitized"
Assert-False $gate.r15Authorized "Gate R15"
Assert-Equal $gate.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Gate gateway"
Assert-False $gate.evidenceArchivesModified "Gate archives"
Assert-True $gate.usdJpyT7ClosureIntact "Gate USDJPY T7"
Assert-True $gate.validatedRailsArchivesIntact "Gate validated rails"

foreach ($property in @(
    "externalRunExecuted",
    "realSnapshotExecuted",
    "replayExecuted",
    "postEndpointInvoked",
    "realSocketOpened",
    "realTcpConnectionAttempted",
    "realTlsHandshakeAttempted",
    "realFixLogonAttempted",
    "realMarketDataRequestSent",
    "orderSubmissionExecuted",
    "tradingStateMutated",
    "schedulerStarted",
    "pollingStarted",
    "shadowReplaySubmitted",
    "apiWorkerStarted",
    "runtimePoweredUp",
    "retryExecuted",
    "batchExecuted",
    "loopExecuted",
    "runtimeEnablementExecuted",
    "tradingEnablementExecuted",
    "schedulerEnablementExecuted",
    "orderPathEnablementExecuted",
    "defaultGatewayRegistrationChanged",
    "liveConnectionScriptCreated",
    "realCredentialLoadingExecuted",
    "hostedServiceAdded",
    "backgroundWorkerAdded",
    "apiWorkerWiringAdded",
    "r15Authorized"
)) {
    Assert-False $nonRun.$property "Non-run $property"
    Assert-False $gate.$property "Gate $property"
}
foreach ($property in @("testDoubleSessionClientUsed", "realTransportClassExercisedWithTestDoubles", "fakeTcpBoundarySimulated", "fakeTlsBoundarySimulated", "fakeFixBoundarySimulated", "fakeMarketDataBoundarySimulated")) {
    Assert-True $nonRun.$property "Non-run $property"
    Assert-True $gate.$property "Gate $property"
}
Assert-Equal $nonRun.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Non-run gateway"

$transportPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxRealReadOnlyMarketDataTransport.cs"
$testPath = Join-Path $RepoRoot "tests\QQ.Production.Intraday.Tests.Unit\LmaxRealReadOnlyMarketDataTransportTests.cs"
if (-not (Test-Path -LiteralPath $transportPath)) {
    throw "Missing real transport file: $transportPath"
}
if (-not (Test-Path -LiteralPath $testPath)) {
    throw "Missing R14 test file: $testPath"
}
$transportSource = Get-Content -LiteralPath $transportPath -Raw
foreach ($requiredToken in @("LmaxRealReadOnlyMarketDataTransport", "ILmaxReadOnlyMarketDataSessionClient", "ILmaxTemporaryReadOnlyMarketDataTransport", "RequestReadOnlyMarketData", "ShutdownRevert")) {
    if ($transportSource -notmatch [regex]::Escape($requiredToken)) {
        throw "Transport source missing required token: $requiredToken"
    }
}
foreach ($forbiddenToken in @("TcpClient", "System.Net.Sockets", "new Socket", "SslStream", "NetworkStream", "CredentialProfileResolver", "SessionPassword", "NewOrderSingle", "OrderCancelRequest", "TradeCaptureReportRequest", "SubmitOrder")) {
    if ($transportSource -match [regex]::Escape($forbiddenToken)) {
        throw "Transport source references forbidden token: $forbiddenToken"
    }
}

$programPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\Program.cs"
$workerPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Worker\Program.cs"
$appsettingsPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\appsettings.json"
$programText = Get-Content -LiteralPath $programPath -Raw
$workerText = Get-Content -LiteralPath $workerPath -Raw
$appsettings = Read-Json $appsettingsPath
if ($programText -notmatch 'AddSingleton<IVenueExecutionGateway,\s*FakeLmaxGateway>') {
    throw "API Program.cs no longer registers FakeLmaxGateway."
}
if ($workerText -notmatch 'AddSingleton<IVenueExecutionGateway,\s*FakeLmaxGateway>') {
    throw "Worker Program.cs no longer registers FakeLmaxGateway."
}
if ($programText -match 'LmaxRealReadOnlyMarketDataTransport|ILmaxReadOnlyMarketDataSessionClient') {
    throw "API Program.cs contains R14 transport wiring."
}
if ($workerText -match 'LmaxRealReadOnlyMarketDataTransport|ILmaxReadOnlyMarketDataSessionClient') {
    throw "Worker Program.cs contains R14 transport wiring."
}
Assert-False $appsettings.Safety.AllowExternalConnections "Appsettings Safety.AllowExternalConnections"
Assert-False $appsettings.Safety.AllowLiveTrading "Appsettings Safety.AllowLiveTrading"
Assert-True $appsettings.Safety.RequireFakeExecutionGateway "Appsettings Safety.RequireFakeExecutionGateway"
Assert-False $appsettings.LmaxReadOnlyRuntime.Enabled "Appsettings runtime Enabled"
Assert-Equal $appsettings.LmaxReadOnlyRuntime.ImplementationMode "DesignOnly" "Appsettings runtime mode"
Assert-False $appsettings.LmaxReadOnlyRuntime.AllowExternalConnections "Appsettings runtime external"
Assert-False $appsettings.LmaxReadOnlyRuntime.AllowCredentialUse "Appsettings runtime credential"
Assert-False $appsettings.LmaxReadOnlyRuntime.AllowOrderSubmission "Appsettings runtime order"
Assert-False $appsettings.LmaxReadOnlyRuntime.SubmitToShadowReplay "Appsettings runtime shadow replay"
Assert-False $appsettings.LmaxReadOnlyRuntime.SchedulerEnabled "Appsettings runtime scheduler"

$reportPath = Join-Path $readiness "phase-lmax-r14-real-transport-test-double-report.md"
$reportText = Get-Content -LiteralPath $reportPath -Raw
foreach ($heading in @(
    "Executive summary",
    "R13 context",
    "Scope and non-run guarantees",
    "Real read-only transport implementation",
    "Read-only session client contract",
    "Test-double coverage",
    "Safety enforcement",
    "Sanitization and boundary evidence model",
    "What R14 now provides",
    "What remains forbidden",
    "Decision",
    "Recommended next phase"
)) {
    if ($reportText -notmatch [regex]::Escape($heading)) {
        throw "R14 report missing required section: $heading"
    }
}

$validation = [ordered]@{
    phase = "LMAX-R14"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    validator = "scripts/check-lmax-r14-real-transport-test-double-gate.ps1"
    requiredArtifactsPresent = $true
    priorArtifactsPresent = $true
    r13DecisionConfirmed = $true
    realTransportClassExists = $true
    readOnlySessionClientAbstractionExists = $true
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
    retryExecuted = $false
    batchExecuted = $false
    loopExecuted = $false
    runtimeEnablementExecuted = $false
    tradingEnablementExecuted = $false
    schedulerEnablementExecuted = $false
    orderPathEnablementExecuted = $false
    defaultGatewayRegistrationChanged = $false
    liveConnectionScriptCreated = $false
    realCredentialLoadingExecuted = $false
    hostedServiceAdded = $false
    backgroundWorkerAdded = $false
    apiWorkerWiringAdded = $false
    r15Authorized = $false
    testDoubleSessionClientUsed = $true
    realTransportClassExercisedWithTestDoubles = $true
    fakeTcpBoundarySimulated = $true
    fakeTlsBoundarySimulated = $true
    fakeFixBoundarySimulated = $true
    fakeMarketDataBoundarySimulated = $true
    apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
    noSensitiveContent = $true
    recommendedNextPhase = $gate.recommendedNextPhase
    finalDecision = $gate.finalDecision
}

$validationPath = Join-Path $readiness "phase-lmax-r14-gate-validation.json"
$validation | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $validationPath -Encoding UTF8

Write-Host "PASS: $($gate.finalDecision)"
