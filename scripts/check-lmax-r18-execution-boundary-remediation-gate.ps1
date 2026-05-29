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
    "phase-lmax-r16-decision-gate.json",
    "phase-lmax-r17-decision-gate.json",
    "phase-lmax-r17-gate-validation.json"
)

$requiredR18 = @(
    "phase-lmax-r18-low-level-session-stack-implementation-summary.json",
    "phase-lmax-r18-credential-boundary-policy.json",
    "phase-lmax-r18-socket-tls-boundary-summary.json",
    "phase-lmax-r18-fix-session-boundary-summary.json",
    "phase-lmax-r18-marketdata-codec-boundary-summary.json",
    "phase-lmax-r18-readonly-interface-safety-proof.json",
    "phase-lmax-r18-test-double-coverage-summary.json",
    "phase-lmax-r18-no-external-activation-proof.json",
    "phase-lmax-r18-decision-gate.json",
    "phase-lmax-r18-non-run-validation.json",
    "phase-lmax-r18-execution-boundary-remediation-report.md",
    "phase-lmax-r18-operator-note.md"
)

Write-Host "LMAX-R18 Execution Boundary Remediation Gate Validator"
Write-Host "This validator performs no external run, real snapshot, replay, POST endpoint, socket, runtime activation, API/Worker startup, real credential loading, or config mutation."

Assert-RequiredArtifacts $readiness $requiredPrior
Assert-RequiredArtifacts $readiness $requiredR18

$t7GatePath = Join-Path $usdJpy "phase-usdjpy-t7-final-closure-gate.json"
if (-not (Test-Path -LiteralPath $t7GatePath)) {
    throw "Missing USDJPY T7 closure gate: $t7GatePath"
}
Assert-NoSensitiveContent $t7GatePath

$r17Gate = Read-Json (Join-Path $readiness "phase-lmax-r17-decision-gate.json")
$summary = Read-Json (Join-Path $readiness "phase-lmax-r18-low-level-session-stack-implementation-summary.json")
$credentialPolicy = Read-Json (Join-Path $readiness "phase-lmax-r18-credential-boundary-policy.json")
$socketTls = Read-Json (Join-Path $readiness "phase-lmax-r18-socket-tls-boundary-summary.json")
$fix = Read-Json (Join-Path $readiness "phase-lmax-r18-fix-session-boundary-summary.json")
$marketData = Read-Json (Join-Path $readiness "phase-lmax-r18-marketdata-codec-boundary-summary.json")
$interfaceProof = Read-Json (Join-Path $readiness "phase-lmax-r18-readonly-interface-safety-proof.json")
$tests = Read-Json (Join-Path $readiness "phase-lmax-r18-test-double-coverage-summary.json")
$proof = Read-Json (Join-Path $readiness "phase-lmax-r18-no-external-activation-proof.json")
$gate = Read-Json (Join-Path $readiness "phase-lmax-r18-decision-gate.json")
$nonRun = Read-Json (Join-Path $readiness "phase-lmax-r18-non-run-validation.json")
$t7Gate = Read-Json $t7GatePath

Assert-Equal $r17Gate.finalDecision "LMAX_R17_FAIL_NO_EXECUTABLE_SESSION_CLIENT_AVAILABLE" "R17 decision"
Assert-Equal $t7Gate.finalDecision "USDJPY_T7_FINAL_READINESS_ARCHIVE_CLOSED_WITH_CAVEAT" "USDJPY T7 decision"

Assert-Equal $summary.finalDecision "LMAX_R18_EXECUTABLE_LOW_LEVEL_SESSION_STACK_IMPLEMENTED_WITH_TEST_DOUBLES_NO_EXTERNAL_ACTIVATION" "Summary decision"
Assert-True $summary.implementsR16Abstractions "Summary R16 abstractions"
Assert-True $summary.constructorDependencyInjectionOnly "Summary DI"
Assert-True $summary.approvedInstrumentAllowlistEnforced "Summary allowlist"
Assert-True $summary.usdJpyCaveatPreserved "Summary caveat"
Assert-True $summary.sanitizedResultsOnly "Summary sanitized"
Assert-True $summary.testDoubleOnlyExercised "Summary test double"
foreach ($property in @("opensSocketInConstructor", "loadsRealSecretsInConstructor", "startsBackgroundWorker", "apiWorkerWiringAdded", "defaultConfigChanged", "externalActivationExecuted")) {
    Assert-False $summary.$property "Summary $property"
}

Assert-True $credentialPolicy.futureApprovedRuntimeAttemptRequired "Credential future approval"
Assert-False $credentialPolicy.realSecretMaterialAllowedNow "Credential material now"
Assert-True $credentialPolicy.redactSensitiveFields "Credential redaction"
Assert-False $credentialPolicy.loadsRealSecretsInConstructors "Credential constructor"
Assert-False $credentialPolicy.loadsRealSecretsInTests "Credential tests"
Assert-False $credentialPolicy.printsSensitiveMaterial "Credential prints"
Assert-False $credentialPolicy.storesSensitiveMaterial "Credential stores"
Assert-False $credentialPolicy.returnsSensitiveMaterial "Credential returns"
Assert-True $credentialPolicy.futureR19Only "Credential R19 only"

Assert-True $socketTls.representsTcpBoundary "Socket TCP"
Assert-True $socketTls.representsTlsBoundary "Socket TLS"
Assert-True $socketTls.usesFakeTransportInR18Tests "Socket fake"
Assert-False $socketTls.realSocketOpened "Socket real"
Assert-False $socketTls.realTcpConnectionAttempted "Socket TCP real"
Assert-False $socketTls.realTlsHandshakeAttempted "Socket TLS real"

Assert-True $fix.representsFixLogonBoundary "FIX boundary"
Assert-True $fix.messageTypeLevelEvidenceOnly "FIX message type only"
Assert-False $fix.rawSensitiveFixTagsExposed "FIX raw sensitive"
Assert-True $fix.usesFakeFixFramesInR18Tests "FIX fake"
Assert-False $fix.realFixLogonAttempted "FIX real"

Assert-True $marketData.rejectsNonApprovedInstruments "MarketData allowlist"
Assert-True $marketData.preservesUsdJpyCaveat "MarketData caveat"
Assert-True $marketData.representsReadOnlyMarketDataIntent "MarketData read-only intent"
Assert-True $marketData.sanitizedStatusEvidenceOnly "MarketData sanitized"
foreach ($property in @("orderMessagesExposed", "tradeCaptureExposed", "orderStatusExposed", "replayExposed", "shadowReplayExposed", "realMarketDataRequestSent")) {
    Assert-False $marketData.$property "MarketData $property"
}

Assert-True $interfaceProof.reflectionTestCoverageExists "Interface reflection coverage"
foreach ($property in @("orderSubmissionInterfacePresent", "orderCancelInterfacePresent", "orderStatusInterfacePresent", "tradeCaptureInterfacePresent", "replayInterfacePresent", "shadowReplayInterfacePresent", "tradingMutationInterfacePresent")) {
    Assert-False $interfaceProof.$property "Interface proof $property"
}

foreach ($coverage in @(
    "low-level stack can be constructed without real credentials",
    "low-level stack construction opens no sockets",
    "fake TCP success boundary",
    "fake TCP failure boundary",
    "fake TLS success boundary",
    "fake TLS failure boundary",
    "fake FIX logon success boundary",
    "fake FIX logon failure boundary",
    "fake MarketData success boundary",
    "fake MarketData failure boundary",
    "approved instruments only",
    "USDJPY caveat preserved",
    "non-approved instrument rejected before request construction",
    "sensitive fields redacted",
    "raw credentials never appear in results",
    "public interfaces expose no order/trading/replay methods",
    "shutdown/revert called in fake low-level session",
    "no API/Worker/default config wiring added",
    "no live launcher added",
    "no hosted/background service added"
)) {
    if (-not ($tests.coverage -contains $coverage)) {
        throw "R18 test coverage missing: $coverage"
    }
}
foreach ($property in @("testDoubleLowLevelStackUsed", "executableLowLevelStackExercisedWithFakes", "fakeTcpBoundarySimulated", "fakeTlsBoundarySimulated", "fakeFixBoundarySimulated", "fakeMarketDataBoundarySimulated")) {
    Assert-True $tests.$property "Tests $property"
}

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
    "apiWorkerWiringAdded"
)) {
    Assert-False $proof.$property "Proof $property"
    Assert-False $nonRun.$property "Non-run $property"
    Assert-False $gate.$property "Gate $property"
}
Assert-False $nonRun.r19Authorized "Non-run R19"
Assert-False $gate.r19Authorized "Gate R19"
foreach ($property in @("testDoubleLowLevelStackUsed", "executableLowLevelStackExercisedWithFakes", "fakeTcpBoundarySimulated", "fakeTlsBoundarySimulated", "fakeFixBoundarySimulated", "fakeMarketDataBoundarySimulated")) {
    Assert-True $nonRun.$property "Non-run $property"
    Assert-True $gate.$property "Gate $property"
}
Assert-Equal $nonRun.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Non-run gateway"
Assert-Equal $gate.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Gate gateway"
Assert-Equal $gate.finalDecision "LMAX_R18_EXECUTABLE_LOW_LEVEL_SESSION_STACK_IMPLEMENTED_WITH_TEST_DOUBLES_NO_EXTERNAL_ACTIVATION" "R18 final decision"
Assert-True $gate.executableLowLevelSessionStackCodeExists "Gate code exists"
Assert-True $gate.socketTlsFixMarketDataBoundaryAbstractionsExist "Gate abstractions"
Assert-True $gate.readOnlyPublicInterfaceSafetyProofExists "Gate proof"
Assert-True $gate.testDoubleCoverageExists "Gate coverage"
Assert-False $gate.evidenceArchivesModified "Gate archives"
Assert-True $gate.usdJpyT7ClosureIntact "Gate USDJPY T7"
Assert-True $gate.validatedRailsArchivesIntact "Gate rails"

$stackPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxExecutableReadOnlyLowLevelSessionStack.cs"
$testPath = Join-Path $RepoRoot "tests\QQ.Production.Intraday.Tests.Unit\LmaxExecutableReadOnlyLowLevelSessionStackTests.cs"
if (-not (Test-Path -LiteralPath $stackPath)) {
    throw "Missing R18 low-level stack file: $stackPath"
}
if (-not (Test-Path -LiteralPath $testPath)) {
    throw "Missing R18 test file: $testPath"
}
$stackSource = Get-Content -LiteralPath $stackPath -Raw
foreach ($requiredToken in @("LmaxExecutableReadOnlySocketSessionBoundary", "LmaxExecutableReadOnlyFixSessionBoundary", "LmaxExecutableReadOnlyMarketDataRequestCodec", "LmaxExecutableReadOnlyCredentialBoundary", "LmaxExecutableReadOnlySessionStackFactory", "ILmaxReadOnlySocketBoundaryTransport", "ILmaxReadOnlyFixFrameBoundary", "ILmaxReadOnlyMarketDataFrameCodec")) {
    if ($stackSource -notmatch [regex]::Escape($requiredToken)) {
        throw "Low-level stack source missing required token: $requiredToken"
    }
}
foreach ($forbiddenToken in @("TcpClient", "System.Net.Sockets", "new Socket", "SslStream", "NetworkStream", "NewOrderSingle", "OrderCancelRequest", "OrderStatusRequest", "TradeCaptureReportRequest", "SubmitOrder", "AddHostedService")) {
    if ($stackSource -match [regex]::Escape($forbiddenToken)) {
        throw "Low-level stack source references forbidden token: $forbiddenToken"
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
if ($programText -match 'LmaxExecutableReadOnlySessionStackFactory|LmaxExecutableReadOnlySocketSessionBoundary|ILmaxReadOnlySocketBoundaryTransport|ILmaxReadOnlyFixFrameBoundary|ILmaxReadOnlyMarketDataFrameCodec') {
    throw "API Program.cs contains R18 low-level stack wiring."
}
if ($workerText -match 'LmaxExecutableReadOnlySessionStackFactory|LmaxExecutableReadOnlySocketSessionBoundary|ILmaxReadOnlySocketBoundaryTransport|ILmaxReadOnlyFixFrameBoundary|ILmaxReadOnlyMarketDataFrameCodec') {
    throw "Worker Program.cs contains R18 low-level stack wiring."
}
Assert-False $appsettings.Safety.AllowExternalConnections "Appsettings Safety.AllowExternalConnections"
Assert-False $appsettings.Safety.AllowLiveTrading "Appsettings Safety.AllowLiveTrading"
Assert-True $appsettings.Safety.RequireFakeExecutionGateway "Appsettings Safety.RequireFakeExecutionGateway"
Assert-False $appsettings.LmaxReadOnlyRuntime.Enabled "Appsettings runtime Enabled"
Assert-Equal $appsettings.LmaxReadOnlyRuntime.ImplementationMode "DesignOnly" "Appsettings runtime mode"
Assert-False $appsettings.LmaxReadOnlyRuntime.AllowExternalConnections "Appsettings runtime external"
Assert-False $appsettings.LmaxReadOnlyRuntime.AllowCredentialUse "Appsettings runtime use"
Assert-False $appsettings.LmaxReadOnlyRuntime.AllowOrderSubmission "Appsettings runtime order"
Assert-False $appsettings.LmaxReadOnlyRuntime.SubmitToShadowReplay "Appsettings runtime shadow replay"
Assert-False $appsettings.LmaxReadOnlyRuntime.SchedulerEnabled "Appsettings runtime scheduler"

$reportPath = Join-Path $readiness "phase-lmax-r18-execution-boundary-remediation-report.md"
$reportText = Get-Content -LiteralPath $reportPath -Raw
foreach ($heading in @(
    "Executive summary",
    "R17 context",
    "Scope and non-run guarantees",
    "Executable low-level session stack implementation",
    "Credential/config boundary policy",
    "Socket/TLS boundary",
    "FIX session boundary",
    "MarketData codec boundary",
    "Read-only interface safety proof",
    "Test-double coverage",
    "Safety enforcement",
    "Sanitization model",
    "What R18 now provides",
    "What remains forbidden",
    "Decision",
    "Recommended next phase"
)) {
    if ($reportText -notmatch [regex]::Escape($heading)) {
        throw "R18 report missing required section: $heading"
    }
}

$validation = [ordered]@{
    phase = "LMAX-R18"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    validator = "scripts/check-lmax-r18-execution-boundary-remediation-gate.ps1"
    requiredArtifactsPresent = $true
    priorArtifactsPresent = $true
    r17DecisionConfirmed = $true
    executableLowLevelSessionStackCodeExists = $true
    socketTlsFixMarketDataBoundaryAbstractionsExist = $true
    readOnlyPublicInterfaceSafetyProofExists = $true
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
    r19Authorized = $false
    testDoubleLowLevelStackUsed = $true
    executableLowLevelStackExercisedWithFakes = $true
    fakeTcpBoundarySimulated = $true
    fakeTlsBoundarySimulated = $true
    fakeFixBoundarySimulated = $true
    fakeMarketDataBoundarySimulated = $true
    apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
    noSensitiveContent = $true
    recommendedNextPhase = $gate.recommendedNextPhase
    finalDecision = $gate.finalDecision
}

$validationPath = Join-Path $readiness "phase-lmax-r18-gate-validation.json"
$validation | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $validationPath -Encoding UTF8

Write-Host "PASS: $($gate.finalDecision)"
