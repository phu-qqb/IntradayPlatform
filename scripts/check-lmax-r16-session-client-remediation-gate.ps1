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
    "phase-lmax-r14-decision-gate.json",
    "phase-lmax-r15-decision-gate.json",
    "phase-lmax-r15-gate-validation.json"
)

$requiredR16 = @(
    "phase-lmax-r16-session-client-implementation-summary.json",
    "phase-lmax-r16-execution-boundary-remediation.json",
    "phase-lmax-r16-readonly-session-client-contract-summary.json",
    "phase-lmax-r16-low-level-dependency-abstractions.json",
    "phase-lmax-r16-test-double-coverage-summary.json",
    "phase-lmax-r16-sanitization-and-safety-summary.json",
    "phase-lmax-r16-no-external-activation-proof.json",
    "phase-lmax-r16-decision-gate.json",
    "phase-lmax-r16-non-run-validation.json",
    "phase-lmax-r16-session-client-remediation-report.md",
    "phase-lmax-r16-operator-note.md"
)

Write-Host "LMAX-R16 Executable Read-Only Session Client Remediation Gate Validator"
Write-Host "This validator performs no external run, real snapshot, replay, POST endpoint, socket, runtime activation, API/Worker startup, credential loading, or config mutation."

Assert-RequiredArtifacts $readiness $requiredPrior
Assert-RequiredArtifacts $readiness $requiredR16

$t7GatePath = Join-Path $usdJpy "phase-usdjpy-t7-final-closure-gate.json"
if (-not (Test-Path -LiteralPath $t7GatePath)) {
    throw "Missing USDJPY T7 closure gate: $t7GatePath"
}
Assert-NoSensitiveContent $t7GatePath

$r15Gate = Read-Json (Join-Path $readiness "phase-lmax-r15-decision-gate.json")
$summary = Read-Json (Join-Path $readiness "phase-lmax-r16-session-client-implementation-summary.json")
$remediation = Read-Json (Join-Path $readiness "phase-lmax-r16-execution-boundary-remediation.json")
$contract = Read-Json (Join-Path $readiness "phase-lmax-r16-readonly-session-client-contract-summary.json")
$abstractions = Read-Json (Join-Path $readiness "phase-lmax-r16-low-level-dependency-abstractions.json")
$tests = Read-Json (Join-Path $readiness "phase-lmax-r16-test-double-coverage-summary.json")
$safety = Read-Json (Join-Path $readiness "phase-lmax-r16-sanitization-and-safety-summary.json")
$proof = Read-Json (Join-Path $readiness "phase-lmax-r16-no-external-activation-proof.json")
$gate = Read-Json (Join-Path $readiness "phase-lmax-r16-decision-gate.json")
$nonRun = Read-Json (Join-Path $readiness "phase-lmax-r16-non-run-validation.json")
$t7Gate = Read-Json $t7GatePath

Assert-Equal $r15Gate.finalDecision "LMAX_R15_FAIL_NO_REAL_TRANSPORT_AVAILABLE" "R15 decision"
Assert-Equal $t7Gate.finalDecision "USDJPY_T7_FINAL_READINESS_ARCHIVE_CLOSED_WITH_CAVEAT" "USDJPY T7 decision"

Assert-Equal $summary.finalDecision "LMAX_R16_EXECUTABLE_READONLY_SESSION_CLIENT_IMPLEMENTED_WITH_TEST_DOUBLES_NO_EXTERNAL_ACTIVATION" "Summary decision"
Assert-Equal $summary.implementation "LmaxExecutableReadOnlyMarketDataSessionClient" "Summary implementation"
Assert-True $summary.executableSessionClientClassExists "Summary executable client"
Assert-True $summary.implementsReadOnlySessionClientInterface "Summary interface"
Assert-Equal $summary.sessionClientInterface "ILmaxReadOnlyMarketDataSessionClient" "Summary interface name"
foreach ($name in @("ILmaxReadOnlySocketSessionBoundary", "ILmaxReadOnlyFixSessionBoundary", "ILmaxReadOnlyMarketDataRequestCodec")) {
    if (-not ($summary.lowLevelAbstractionsAdded -contains $name)) {
        throw "Summary missing low-level abstraction: $name"
    }
}
Assert-True $summary.constructorDependencyInjectionOnly "Summary DI"
foreach ($property in @("opensSocketInConstructor", "loadsRealCredentialsInConstructor", "startsBackgroundWorker", "apiWorkerWiringAdded", "defaultConfigChanged", "externalActivationExecuted")) {
    Assert-False $summary.$property "Summary $property"
}
Assert-True $summary.approvedInstrumentAllowlistEnforced "Summary allowlist"
Assert-True $summary.usdJpyCaveatPreserved "Summary caveat"
Assert-True $summary.sanitizedResultsOnly "Summary sanitized"
Assert-True $summary.testDoubleOnlyExercised "Summary test double"

Assert-Equal $remediation.r15Blocker "FailedNoApprovedExecutableSessionClient" "Remediation blocker"
Assert-True $remediation.externalExecutionStillNotAuthorized "Remediation no external"
Assert-True $remediation.r17RequiredForExternalAttempt "Remediation R17"
foreach ($property in @("noApiWorkerStartupRequired", "noLiveLauncherCreated", "noDefaultConfigChange", "noOrderTradingReplaySchedulerPollingPath")) {
    Assert-True $remediation.$property "Remediation $property"
}

foreach ($operation in @("OpenReadOnlyTcpBoundary", "OpenReadOnlyTlsBoundary", "OpenReadOnlyFixLogonBoundary", "RequestReadOnlyMarketData", "ShutdownRevert")) {
    if (-not ($contract.allowedOperations -contains $operation)) {
        throw "Contract missing allowed operation: $operation"
    }
}
foreach ($property in @("readOnlyOnly", "doesNotExposeOrderSubmission", "doesNotExposeTradingMutation", "doesNotExposeReplayOrShadowReplay", "doesNotStartApiWorker", "doesNotRegisterGateways")) {
    Assert-True $contract.$property "Contract $property"
}

foreach ($property in @("realSocketImplementationAdded", "realCredentialProviderAdded", "realCredentialLoadingExecuted", "liveLauncherCreated", "hostedServiceAdded", "apiWorkerWiringAdded")) {
    Assert-False $abstractions.$property "Abstractions $property"
}

foreach ($coverage in @(
    "executable session client can be constructed with fake low-level dependencies",
    "no real credential loading in constructor",
    "no network calls in constructor",
    "successful fake TCP/TLS/FIX/MarketData flow",
    "TCP failure returns sanitized TCP boundary failure",
    "TLS failure returns sanitized TLS boundary failure",
    "FIX logon failure returns sanitized FIX boundary failure",
    "MarketData failure returns sanitized MarketData boundary failure",
    "instrument reject returns sanitized instrument-level status",
    "shutdown called exactly once after partial start",
    "approved instruments only are passed to fake session/codec",
    "USDJPY caveat preserved",
    "no order/trading methods exist on public read-only interfaces",
    "unsafe request fails before low-level session client is invoked",
    "no API/Worker/default config wiring added",
    "no live launcher added",
    "no hosted/background service added"
)) {
    if (-not ($tests.coverage -contains $coverage)) {
        throw "R16 test coverage missing: $coverage"
    }
}
foreach ($property in @("testDoubleSessionClientUsed", "executableSessionClientExercisedWithTestDoubles", "fakeTcpBoundarySimulated", "fakeTlsBoundarySimulated", "fakeFixBoundarySimulated", "fakeMarketDataBoundarySimulated")) {
    Assert-True $tests.$property "Tests $property"
}

Assert-True $safety.safetyBeforeLowLevelUseEnforced "Safety before low-level"
foreach ($condition in @("non-approved instrument present", "USDJPY caveat missing", "production account requested", "orders enabled", "live trading enabled", "IsTradingEnabled=true", "scheduler enabled", "polling enabled", "replay enabled", "shadow replay enabled", "trading mutation enabled", "persistent runtime enablement requested", "default gateway registration change requested", "output sanitization disabled", "shutdown/revert plan missing")) {
    if (-not ($safety.failsBeforeLowLevelUseIf -contains $condition)) {
        throw "Safety summary missing condition: $condition"
    }
}
foreach ($property in @("orderGatewayRegistered", "tradingGatewayRegistered", "schedulerStarted", "pollingStarted", "replayExecuted", "shadowReplaySubmitted", "tradingStateMutated")) {
    Assert-False $safety.$property "Safety $property"
}
Assert-Equal $safety.apiWorkerDefaultGatewayMode "FakeLmaxGatewayOnly" "Safety gateway"

foreach ($property in @("networkTypesReferencedByExecutableClient", "socketConstructedByExecutableClient", "realCredentialProviderAdded", "realCredentialLoadingExecuted", "apiWorkerRegistrationAdded", "hostedServiceAdded", "backgroundWorkerAdded", "liveConnectionScriptCreated", "defaultGatewayRegistrationChanged", "defaultConfigChangedToEnableConnectivity", "externalRunExecuted", "realSnapshotExecuted", "runtimeActivationExecuted", "realSocketOpened", "realTcpConnectionAttempted", "realTlsHandshakeAttempted", "realFixLogonAttempted", "realMarketDataRequestSent")) {
    Assert-False $proof.$property "Proof $property"
}
Assert-Equal $proof.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Proof gateway"

Assert-Equal $gate.finalDecision "LMAX_R16_EXECUTABLE_READONLY_SESSION_CLIENT_IMPLEMENTED_WITH_TEST_DOUBLES_NO_EXTERNAL_ACTIVATION" "R16 final decision"
Assert-True $gate.executableSessionClientClassExists "Gate executable"
Assert-True $gate.readOnlySessionClientAbstractionStillSafe "Gate interface safe"
Assert-True $gate.testDoubleCoverageExists "Gate coverage"
Assert-True $gate.testDoubleSessionClientUsed "Gate test double"
Assert-True $gate.executableSessionClientExercisedWithTestDoubles "Gate exercised"
Assert-True $gate.approvedInstrumentAllowlistEnforced "Gate allowlist"
Assert-True $gate.usdJpyCaveatPreserved "Gate caveat"
Assert-True $gate.safetyBeforeLowLevelUseEnforced "Gate safety"
Assert-True $gate.sanitizedResultsOnly "Gate sanitized"
Assert-False $gate.r17Authorized "Gate R17"
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
    "r17Authorized"
)) {
    Assert-False $nonRun.$property "Non-run $property"
    Assert-False $gate.$property "Gate $property"
}
foreach ($property in @("testDoubleSessionClientUsed", "executableSessionClientExercisedWithTestDoubles", "fakeTcpBoundarySimulated", "fakeTlsBoundarySimulated", "fakeFixBoundarySimulated", "fakeMarketDataBoundarySimulated")) {
    Assert-True $nonRun.$property "Non-run $property"
    Assert-True $gate.$property "Gate $property"
}
Assert-Equal $nonRun.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Non-run gateway"

$clientPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxExecutableReadOnlyMarketDataSessionClient.cs"
$testPath = Join-Path $RepoRoot "tests\QQ.Production.Intraday.Tests.Unit\LmaxExecutableReadOnlyMarketDataSessionClientTests.cs"
if (-not (Test-Path -LiteralPath $clientPath)) {
    throw "Missing executable session client file: $clientPath"
}
if (-not (Test-Path -LiteralPath $testPath)) {
    throw "Missing R16 test file: $testPath"
}
$clientSource = Get-Content -LiteralPath $clientPath -Raw
foreach ($requiredToken in @("LmaxExecutableReadOnlyMarketDataSessionClient", "ILmaxReadOnlySocketSessionBoundary", "ILmaxReadOnlyFixSessionBoundary", "ILmaxReadOnlyMarketDataRequestCodec", "ILmaxReadOnlyMarketDataSessionClient")) {
    if ($clientSource -notmatch [regex]::Escape($requiredToken)) {
        throw "Executable client source missing required token: $requiredToken"
    }
}
foreach ($forbiddenToken in @("TcpClient", "System.Net.Sockets", "new Socket", "SslStream", "NetworkStream", "CredentialProfileResolver", "SessionPassword", "NewOrderSingle", "OrderCancelRequest", "TradeCaptureReportRequest", "SubmitOrder")) {
    if ($clientSource -match [regex]::Escape($forbiddenToken)) {
        throw "Executable client source references forbidden token: $forbiddenToken"
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
if ($programText -match 'LmaxExecutableReadOnlyMarketDataSessionClient|ILmaxReadOnlySocketSessionBoundary|ILmaxReadOnlyFixSessionBoundary|ILmaxReadOnlyMarketDataRequestCodec') {
    throw "API Program.cs contains R16 session client wiring."
}
if ($workerText -match 'LmaxExecutableReadOnlyMarketDataSessionClient|ILmaxReadOnlySocketSessionBoundary|ILmaxReadOnlyFixSessionBoundary|ILmaxReadOnlyMarketDataRequestCodec') {
    throw "Worker Program.cs contains R16 session client wiring."
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

$reportPath = Join-Path $readiness "phase-lmax-r16-session-client-remediation-report.md"
$reportText = Get-Content -LiteralPath $reportPath -Raw
foreach ($heading in @(
    "Executive summary",
    "R15 context",
    "Scope and non-run guarantees",
    "Executable read-only session client implementation",
    "Low-level dependency abstractions",
    "Read-only-only interface proof",
    "Test-double coverage",
    "Safety enforcement",
    "Sanitization model",
    "What R16 now provides",
    "What remains forbidden",
    "Decision",
    "Recommended next phase"
)) {
    if ($reportText -notmatch [regex]::Escape($heading)) {
        throw "R16 report missing required section: $heading"
    }
}

$validation = [ordered]@{
    phase = "LMAX-R16"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    validator = "scripts/check-lmax-r16-session-client-remediation-gate.ps1"
    requiredArtifactsPresent = $true
    priorArtifactsPresent = $true
    r15DecisionConfirmed = $true
    executableSessionClientClassExists = $true
    readOnlySessionClientAbstractionStillSafe = $true
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
    r17Authorized = $false
    testDoubleSessionClientUsed = $true
    executableSessionClientExercisedWithTestDoubles = $true
    fakeTcpBoundarySimulated = $true
    fakeTlsBoundarySimulated = $true
    fakeFixBoundarySimulated = $true
    fakeMarketDataBoundarySimulated = $true
    apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
    noSensitiveContent = $true
    recommendedNextPhase = $gate.recommendedNextPhase
    finalDecision = $gate.finalDecision
}

$validationPath = Join-Path $readiness "phase-lmax-r16-gate-validation.json"
$validation | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $validationPath -Encoding UTF8

Write-Host "PASS: $($gate.finalDecision)"
