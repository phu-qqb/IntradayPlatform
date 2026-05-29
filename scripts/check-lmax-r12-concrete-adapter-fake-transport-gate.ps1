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
    "phase-lmax-r11-gate-validation.json"
)

$requiredR12 = @(
    "phase-lmax-r12-concrete-adapter-implementation-summary.json",
    "phase-lmax-r12-fake-transport-contract-summary.json",
    "phase-lmax-r12-sanitized-boundary-model-summary.json",
    "phase-lmax-r12-safety-enforcement-summary.json",
    "phase-lmax-r12-test-coverage-summary.json",
    "phase-lmax-r12-no-external-activation-proof.json",
    "phase-lmax-r12-decision-gate.json",
    "phase-lmax-r12-non-run-validation.json",
    "phase-lmax-r12-concrete-adapter-fake-transport-report.md",
    "phase-lmax-r12-operator-note.md"
)

Write-Host "LMAX-R12 Concrete Adapter Fake Transport Gate Validator"
Write-Host "This validator performs no external run, real snapshot, replay, POST endpoint, socket, runtime activation, API/Worker startup, credential loading, or config mutation."

Assert-RequiredArtifacts $readiness $requiredPrior
Assert-RequiredArtifacts $readiness $requiredR12

$t7GatePath = Join-Path $usdJpy "phase-usdjpy-t7-final-closure-gate.json"
if (-not (Test-Path -LiteralPath $t7GatePath)) {
    throw "Missing USDJPY T7 closure gate: $t7GatePath"
}
Assert-NoSensitiveContent $t7GatePath

$r11Gate = Read-Json (Join-Path $readiness "phase-lmax-r11-decision-gate.json")
$summary = Read-Json (Join-Path $readiness "phase-lmax-r12-concrete-adapter-implementation-summary.json")
$transport = Read-Json (Join-Path $readiness "phase-lmax-r12-fake-transport-contract-summary.json")
$boundary = Read-Json (Join-Path $readiness "phase-lmax-r12-sanitized-boundary-model-summary.json")
$safety = Read-Json (Join-Path $readiness "phase-lmax-r12-safety-enforcement-summary.json")
$tests = Read-Json (Join-Path $readiness "phase-lmax-r12-test-coverage-summary.json")
$proof = Read-Json (Join-Path $readiness "phase-lmax-r12-no-external-activation-proof.json")
$gate = Read-Json (Join-Path $readiness "phase-lmax-r12-decision-gate.json")
$nonRun = Read-Json (Join-Path $readiness "phase-lmax-r12-non-run-validation.json")
$t7Gate = Read-Json $t7GatePath

Assert-Equal $r11Gate.finalDecision "LMAX_R11_CONCRETE_ADAPTER_IMPLEMENTATION_PLAN_READY_NO_ACTIVATION" "R11 decision"
Assert-Equal $t7Gate.finalDecision "USDJPY_T7_FINAL_READINESS_ARCHIVE_CLOSED_WITH_CAVEAT" "USDJPY T7 decision"

Assert-Equal $summary.finalDecision "LMAX_R12_CONCRETE_ADAPTER_IMPLEMENTED_FAKE_TRANSPORT_ONLY_NO_EXTERNAL_ACTIVATION" "Summary decision"
Assert-True $summary.concreteAdapterImplemented "Summary adapter implemented"
Assert-Equal $summary.concreteAdapter "LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter" "Summary adapter"
Assert-Equal $summary.transportInterface "ILmaxTemporaryReadOnlyMarketDataTransport" "Summary transport interface"
Assert-True $summary.consumesR7R9HarnessBackedRequest "Summary consumes harness"
Assert-True $summary.enforcesSafetyBeforeTransportUse "Summary safety before transport"
Assert-True $summary.approvedInstrumentAllowlistEnforced "Summary allowlist"
Assert-True $summary.usdJpyCaveatPreserved "Summary USDJPY caveat"
Assert-True $summary.fakeTransportOnly "Summary fake transport"
Assert-False $summary.realTransportImplemented "Summary real transport"
foreach ($property in @("apiWorkerWiringAdded", "defaultConfigChanged", "credentialLoadingAdded", "liveConnectionScriptCreated", "hostedServiceAdded", "backgroundWorkerAdded")) {
    Assert-False $summary.$property "Summary $property"
}

Assert-Equal $transport.transportInterface "ILmaxTemporaryReadOnlyMarketDataTransport" "Transport interface"
Assert-True $transport.fakeTransportUsedInTests "Transport fake tests"
Assert-False $transport.opensNetworkSockets "Transport network sockets"
Assert-False $transport.loadsCredentials "Transport credentials"
Assert-False $transport.callsExternalSystems "Transport external systems"
Assert-False $transport.usesFilesystemSecrets "Transport filesystem secrets"
Assert-False $transport.startsBackgroundTasks "Transport background"
Assert-True $transport.simulatesTcpBoundary "Transport TCP"
Assert-True $transport.simulatesTlsBoundary "Transport TLS"
Assert-True $transport.simulatesFixBoundary "Transport FIX"
Assert-True $transport.simulatesMarketDataBoundary "Transport MD"
Assert-True $transport.shutdownRevertCalledExactlyOnceInSuccessTest "Transport shutdown once"

foreach ($status in @("NotAttempted", "Succeeded", "Failed", "FakeSucceeded", "FakeFailed")) {
    if (-not ($boundary.supportedStatuses -contains $status)) {
        throw "Boundary model missing status $status"
    }
}
foreach ($property in @("rawFixLogsStored", "credentialsStored", "tlsSecretsStored", "fullRawConfigStored")) {
    Assert-False $boundary.$property "Boundary $property"
}

Assert-True $safety.transportNotInvokedForUnsafeScopes "Safety transport not invoked"
Assert-True $safety.shutdownRevertGuaranteedAfterTransportUse "Safety shutdown/revert"
Assert-Equal $safety.apiWorkerDefaultGatewayMode "FakeLmaxGatewayOnly" "Safety gateway"
foreach ($condition in @("harness validation missing or failed", "operator approval missing", "environment is not Demo/read-only", "production account requested", "non-approved instrument present", "USDJPY included without caveat", "AllowOrderSubmission=true", "AllowLiveTrading=true", "IsTradingEnabled=true", "scheduler enabled", "polling enabled", "replay enabled", "shadow replay enabled", "trading mutation enabled", "persistent runtime enablement requested", "default gateway registration change requested", "output sanitization disabled", "shutdown/revert plan missing")) {
    if (-not ($safety.failsBeforeTransportUseIf -contains $condition)) {
        throw "Safety summary missing condition: $condition"
    }
}

foreach ($coverage in @(
    "valid fake transport activation succeeds locally",
    "adapter consumes R7/R9 harness-backed request",
    "approved instruments only are passed to fake transport",
    "USDJPY caveat is preserved",
    "sanitized result contains no credentials/secrets",
    "TCP/TLS/FIX/MarketData fake-success boundaries are represented",
    "fake transport shutdown/revert called exactly once",
    "unsafe flags fail before fake transport is invoked",
    "non-approved instrument fails before fake transport is invoked",
    "USDJPY without caveat fails before fake transport is invoked",
    "production account fails before fake transport is invoked",
    "orders enabled fails before fake transport is invoked",
    "live trading enabled fails before fake transport is invoked",
    "scheduler/polling/replay/shadow replay fail before fake transport is invoked",
    "mutation enabled fails before fake transport is invoked",
    "persistent runtime enablement fails before fake transport is invoked",
    "default gateway registration change fails before fake transport is invoked",
    "missing shutdown/revert plan fails before fake transport is invoked",
    "transport failure returns sanitized failure result",
    "no credential loading is required",
    "no API/Worker/default config wiring is added"
)) {
    if (-not ($tests.coverage -contains $coverage)) {
        throw "R12 test coverage missing: $coverage"
    }
}

foreach ($property in @("networkTypesReferenced", "credentialResolversReferenced", "apiWorkerRegistrationAdded", "hostedServiceAdded", "backgroundWorkerAdded", "liveConnectionScriptCreated", "defaultGatewayRegistrationChanged", "defaultConfigChangedToEnableConnectivity", "externalRunExecuted", "realSnapshotExecuted", "runtimeActivationExecuted", "realSocketOpened", "realTcpConnectionAttempted", "realTlsHandshakeAttempted", "realFixLogonAttempted", "realMarketDataRequestSent")) {
    Assert-False $proof.$property "Proof $property"
}
Assert-Equal $proof.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Proof gateway"

Assert-Equal $gate.finalDecision "LMAX_R12_CONCRETE_ADAPTER_IMPLEMENTED_FAKE_TRANSPORT_ONLY_NO_EXTERNAL_ACTIVATION" "R12 final decision"
Assert-True $gate.concreteAdapterImplemented "Gate adapter"
Assert-Equal $gate.concreteAdapter "LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter" "Gate adapter name"
Assert-True $gate.transportInterfaceImplemented "Gate transport interface"
Assert-True $gate.fakeTransportTestCoverageExists "Gate fake coverage"
Assert-True $gate.harnessBackedRequestConsumed "Gate harness consumed"
Assert-True $gate.safetyBeforeTransportUseEnforced "Gate safety"
Assert-True $gate.approvedInstrumentAllowlistEnforced "Gate allowlist"
Assert-True $gate.usdJpyCaveatPreserved "Gate USDJPY caveat"
Assert-True $gate.fakeTransportOnly "Gate fake only"
Assert-True $gate.fakeTransportUsedInTests "Gate fake used"
Assert-True $gate.fakeTcpBoundarySimulated "Gate fake TCP"
Assert-True $gate.fakeTlsBoundarySimulated "Gate fake TLS"
Assert-True $gate.fakeFixBoundarySimulated "Gate fake FIX"
Assert-True $gate.fakeMarketDataBoundarySimulated "Gate fake MD"
Assert-False $gate.r13Authorized "Gate R13"
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
    "credentialLoadingAdded",
    "hostedServiceAdded",
    "backgroundWorkerAdded",
    "apiWorkerWiringAdded",
    "r13Authorized"
)) {
    Assert-False $nonRun.$property "Non-run $property"
    Assert-False $gate.$property "Gate $property"
}

foreach ($property in @("fakeTransportUsedInTests", "fakeTcpBoundarySimulated", "fakeTlsBoundarySimulated", "fakeFixBoundarySimulated", "fakeMarketDataBoundarySimulated")) {
    Assert-True $nonRun.$property "Non-run $property"
}
Assert-Equal $nonRun.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Non-run gateway"

$adapterPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter.cs"
$testPath = Join-Path $RepoRoot "tests\QQ.Production.Intraday.Tests.Unit\LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapterTests.cs"
if (-not (Test-Path -LiteralPath $adapterPath)) {
    throw "Missing concrete adapter code file: $adapterPath"
}
if (-not (Test-Path -LiteralPath $testPath)) {
    throw "Missing concrete adapter test file: $testPath"
}
$adapterSource = Get-Content -LiteralPath $adapterPath -Raw
foreach ($requiredToken in @("LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter", "ILmaxTemporaryReadOnlyMarketDataTransport", "LmaxTemporaryReadOnlyTransportResult", "LmaxTemporaryReadOnlySessionBoundaryStatus")) {
    if ($adapterSource -notmatch [regex]::Escape($requiredToken)) {
        throw "Adapter source missing required token: $requiredToken"
    }
}
foreach ($pattern in @("TcpClient", "System.Net.Sockets", "new Socket", "SslStream", "NetworkStream", "ConnectAsync", "CredentialProfileResolver", "SessionPassword")) {
    if ($adapterSource -match [regex]::Escape($pattern)) {
        throw "Adapter source references forbidden network/credential token: $pattern"
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
if ($programText -match 'LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter|ILmaxTemporaryReadOnlyMarketDataTransport') {
    throw "API Program.cs contains R12 adapter wiring."
}
if ($workerText -match 'LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter|ILmaxTemporaryReadOnlyMarketDataTransport') {
    throw "Worker Program.cs contains R12 adapter wiring."
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

$reportPath = Join-Path $readiness "phase-lmax-r12-concrete-adapter-fake-transport-report.md"
$reportText = Get-Content -LiteralPath $reportPath -Raw
foreach ($heading in @(
    "Executive summary",
    "R11 context",
    "Scope and non-run guarantees",
    "Concrete adapter implementation",
    "Fake transport contract",
    "Harness-backed request consumption",
    "Safety enforcement",
    "Sanitized boundary model",
    "Tests added/updated",
    "What R12 now provides",
    "What remains forbidden",
    "Decision",
    "Recommended next phase"
)) {
    if ($reportText -notmatch [regex]::Escape($heading)) {
        throw "R12 report missing required section: $heading"
    }
}

$validation = [ordered]@{
    phase = "LMAX-R12"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    validator = "scripts/check-lmax-r12-concrete-adapter-fake-transport-gate.ps1"
    requiredArtifactsPresent = $true
    priorArtifactsPresent = $true
    r11DecisionConfirmed = $true
    concreteAdapterImplemented = $true
    fakeTransportTestCoverageExists = $true
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
    credentialLoadingAdded = $false
    hostedServiceAdded = $false
    backgroundWorkerAdded = $false
    apiWorkerWiringAdded = $false
    r13Authorized = $false
    fakeTransportUsedInTests = $true
    fakeTcpBoundarySimulated = $true
    fakeTlsBoundarySimulated = $true
    fakeFixBoundarySimulated = $true
    fakeMarketDataBoundarySimulated = $true
    apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
    noSensitiveContent = $true
    recommendedNextPhase = $gate.recommendedNextPhase
    finalDecision = $gate.finalDecision
}

$validationPath = Join-Path $readiness "phase-lmax-r12-gate-validation.json"
$validation | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $validationPath -Encoding UTF8

Write-Host "PASS: $($gate.finalDecision)"
