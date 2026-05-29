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

function Assert-In($Actual, [string[]]$Expected, [string]$Message) {
    if ($Expected -notcontains $Actual) {
        throw "$Message. Value '$Actual' was not one of: $($Expected -join ', ')."
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
    "phase-lmax-r28-decision-gate.json",
    "phase-lmax-r29-decision-gate.json",
    "phase-lmax-r29-gate-validation.json"
)

$requiredR30 = @(
    "phase-lmax-r30-final-provider-sweep-summary.json",
    "phase-lmax-r30-marketdata-provider-fix-summary.json",
    "phase-lmax-r30-marketdata-provider-implementation-summary.json",
    "phase-lmax-r30-credential-config-provider-status.json",
    "phase-lmax-r30-final-provider-completeness-check.json",
    "phase-lmax-r30-marketdata-options-sanitization-summary.json",
    "phase-lmax-r30-test-coverage-summary.json",
    "phase-lmax-r30-no-external-activation-proof.json",
    "phase-lmax-r30-decision-gate.json",
    "phase-lmax-r30-non-run-validation.json",
    "phase-lmax-r30-final-provider-completion-sweep-report.md",
    "phase-lmax-r30-operator-note.md"
)

Write-Host "LMAX-R30 Final Provider Completion Sweep Gate Validator"
Write-Host "This validator performs no external run, socket open, TCP attempt, TLS handshake, FIX logon, FIX send, MarketDataRequest, API/Worker startup, or config mutation."

Assert-RequiredArtifacts $readiness $requiredPrior
Assert-RequiredArtifacts $readiness $requiredR30

$t7GatePath = Join-Path $usdJpy "phase-usdjpy-t7-final-closure-gate.json"
$phase7nGatePath = Join-Path $RepoRoot "artifacts\readiness\phase7n-final-lmax-readonly-runtime-evidence-archive-closure-gate.json"
if (-not (Test-Path -LiteralPath $t7GatePath)) { throw "Missing USDJPY T7 closure gate: $t7GatePath" }
if (-not (Test-Path -LiteralPath $phase7nGatePath)) { throw "Missing Phase 7N closure gate: $phase7nGatePath" }
Assert-NoSensitiveContent $t7GatePath
Assert-NoSensitiveContent $phase7nGatePath

$r29Gate = Read-Json (Join-Path $readiness "phase-lmax-r29-decision-gate.json")
$summary = Read-Json (Join-Path $readiness "phase-lmax-r30-final-provider-sweep-summary.json")
$marketDataFix = Read-Json (Join-Path $readiness "phase-lmax-r30-marketdata-provider-fix-summary.json")
$marketDataImplementation = Read-Json (Join-Path $readiness "phase-lmax-r30-marketdata-provider-implementation-summary.json")
$credentialStatus = Read-Json (Join-Path $readiness "phase-lmax-r30-credential-config-provider-status.json")
$completeness = Read-Json (Join-Path $readiness "phase-lmax-r30-final-provider-completeness-check.json")
$options = Read-Json (Join-Path $readiness "phase-lmax-r30-marketdata-options-sanitization-summary.json")
$coverage = Read-Json (Join-Path $readiness "phase-lmax-r30-test-coverage-summary.json")
$nonRun = Read-Json (Join-Path $readiness "phase-lmax-r30-non-run-validation.json")
$gate = Read-Json (Join-Path $readiness "phase-lmax-r30-decision-gate.json")
$t7Gate = Read-Json $t7GatePath
$phase7nGate = Read-Json $phase7nGatePath

Assert-Equal $r29Gate.finalDecision "LMAX_R29_FAIL_MARKETDATA_PROVIDER_NOT_EXECUTABLE" "R29 decision"
Assert-Equal $t7Gate.finalDecision "USDJPY_T7_FINAL_READINESS_ARCHIVE_CLOSED_WITH_CAVEAT" "USDJPY T7 decision"
Assert-Equal $phase7nGate.finalDecision "PASS_FINAL_LMAX_READONLY_RUNTIME_EVIDENCE_ARCHIVE_CLOSED" "Phase 7N decision"
Assert-In $gate.finalDecision @("LMAX_R30_FINAL_PROVIDER_SWEEP_COMPLETE_NO_EXTERNAL_ACTIVATION", "LMAX_R30_FINAL_PROVIDER_SWEEP_INCOMPLETE_SPECIFIC_BLOCKER") "R30 decision"
Assert-Equal $gate.finalDecision "LMAX_R30_FINAL_PROVIDER_SWEEP_COMPLETE_NO_EXTERNAL_ACTIVATION" "R30 final decision"
Assert-False $gate.r31Authorized "R31 authorization"

Assert-Equal $summary.contextDecision "LMAX_R29_FAIL_MARKETDATA_PROVIDER_NOT_EXECUTABLE" "R30 context"
Assert-Equal $summary.r29ConcreteAbortCause "MarketDataProviderNotExecutable" "R29 abort cause"
Assert-True $summary.marketDataProviderImplemented "MarketData provider implemented"
Assert-True $summary.credentialConfigProviderImplemented "Credential/config provider implemented"
Assert-True $summary.finalProviderCompletenessPassed "Final provider completeness"
foreach ($property in @("registeredByDefault", "apiWorkerWiringAdded", "defaultConfigChanged", "liveLauncherCreated", "hostedServiceAdded", "backgroundWorkerAdded", "externalRunExecuted", "realSocketOpened", "realTcpConnectionAttempted", "realTlsHandshakeAttempted", "realFixLogonAttempted", "realFixMessageSent", "realMarketDataRequestSent", "realCredentialLoadingExecuted", "r31Authorized")) {
    Assert-False $summary.$property "Summary $property"
}

Assert-Equal $marketDataFix.providerClass "LmaxRealReadOnlyMarketDataFrameBoundaryProvider" "MarketData implementation class"
Assert-Equal $marketDataFix.implements "ILmaxRealReadOnlyMarketDataFrameBoundaryProvider" "MarketData interface"
Assert-True $marketDataFix.nonApprovedInstrumentsRejected "MarketData non-approved instrument"
Assert-True $marketDataFix.usdJpyCaveatPreserved "MarketData USDJPY caveat"
Assert-True $marketDataFix.futureExecutionRequiresExplicitApproval "MarketData future approval"
Assert-False $marketDataFix.constructorSendsMarketDataRequest "MarketData constructor request"
Assert-False $marketDataFix.externalActivationExecuted "MarketData external activation"

Assert-Equal $marketDataImplementation.providerClass "LmaxRealReadOnlyMarketDataFrameBoundaryProvider" "Implementation class"
Assert-False $marketDataImplementation.constructorOpensSocket "Constructor socket"
Assert-False $marketDataImplementation.constructorAttemptsTcp "Constructor TCP"
Assert-False $marketDataImplementation.constructorCreatesTlsStream "Constructor TLS stream"
Assert-False $marketDataImplementation.constructorPerformsTlsHandshake "Constructor TLS handshake"
Assert-False $marketDataImplementation.constructorPerformsFixLogon "Constructor FIX logon"
Assert-False $marketDataImplementation.constructorSendsFixMessage "Constructor FIX send"
Assert-False $marketDataImplementation.constructorSendsMarketDataRequest "Constructor MarketDataRequest"
Assert-False $marketDataImplementation.constructorLoadsCredentials "Constructor credential load"
Assert-True $marketDataImplementation.marketDataRequestRequiresExplicitFutureExecutionApproval "Future MarketData approval"
Assert-True $marketDataImplementation.sanitizedEvidenceReturned "Sanitized evidence"
Assert-False $marketDataImplementation.rawFixStored "Raw FIX stored"

Assert-Equal $credentialStatus.status "implemented" "Credential/config provider status"
Assert-Equal $credentialStatus.providerClass "LmaxRealReadOnlyCredentialConfigBoundaryProvider" "Credential/config provider class"
Assert-Equal $credentialStatus.implements "ILmaxRealReadOnlyCredentialConfigBoundaryProvider" "Credential/config interface"
Assert-True $credentialStatus.requiredBeforeFirstRealExternalActivation "Credential/config required"
Assert-False $credentialStatus.constructorLoadsRealSecrets "Constructor secret load"
Assert-False $credentialStatus.r30LoadsRealSecrets "R30 secret load"
Assert-True $credentialStatus.futureRealSecretAccessRequiresExplicitApproval "Future secret approval"
Assert-True $credentialStatus.rejectsProductionAccount "Production rejection"

Assert-True $completeness.passed "Provider completeness"
Assert-True $completeness.providers.socket.nonTestImplementationExists "Socket provider"
Assert-True $completeness.providers.tls.nonTestImplementationExists "TLS provider"
Assert-True $completeness.providers.fix.nonTestImplementationExists "FIX provider"
Assert-True $completeness.providers.marketData.nonTestImplementationExists "MarketData provider"
Assert-True $completeness.providers.credentialConfig.nonTestImplementationExists "Credential/config provider"
Assert-True $completeness.noProviderTestFakeOnly "No provider fake-only"
Assert-False $completeness.apiWorkerWiringAdded "Completeness API/Worker wiring"
Assert-False $completeness.liveLauncherAdded "Completeness live launcher"

Assert-False $options.containsCredentials "Options credentials"
Assert-False $options.containsSessionPassword "Options session password"
Assert-False $options.containsSecrets "Options secrets"
Assert-False $options.containsRawFix "Options raw FIX"
Assert-True $options.rejectsProductionOrLiveEnvironment "Options non-demo"
Assert-True $options.rejectsMissingReadOnlyFlag "Options read-only"
Assert-True $options.rejectsUnsafeMarketDataLabel "Options unsafe label"
Assert-True $options.rejectsUnsupportedMarketDataMessageType "Options unsupported MarketData"
Assert-True $options.rejectsOrderTradingReplayTradeCaptureOrderStatusMessageTypes "Options forbidden message"
Assert-True $options.rejectsNonApprovedInstrument "Options non-approved instrument"
Assert-True $options.rejectsUsdJpyWithoutCaveat "Options USDJPY caveat"

Assert-True $coverage.marketDataProviderTestsExist "MarketData tests"
Assert-True $coverage.credentialConfigProviderTestsExist "Credential/config tests"
Assert-True $coverage.finalProviderCompletenessTestsExist "Completeness tests"
Assert-True $coverage.fakeMarketDataFrameClientUsed "Fake MarketData client"
Assert-True $coverage.fakeCredentialConfigClientUsed "Fake config client"
Assert-True $coverage.finalProviderCompletenessPassedWithFakes "Completeness with fakes"
foreach ($property in @("realSocketOpened", "realTcpConnectionAttempted", "realTlsHandshakeAttempted", "realFixLogonAttempted", "realFixMessageSent", "realMarketDataRequestSent", "realCredentialLoadingExecuted")) {
    Assert-False $coverage.$property "Coverage $property"
}

foreach ($property in @("externalRunExecuted", "realSnapshotExecuted", "replayExecuted", "postEndpointInvoked", "realSocketOpened", "realTcpConnectionAttempted", "realTlsHandshakeAttempted", "realFixLogonAttempted", "realFixMessageSent", "realMarketDataRequestSent", "orderSubmissionExecuted", "tradingStateMutated", "schedulerStarted", "pollingStarted", "shadowReplaySubmitted", "apiWorkerStarted", "runtimePoweredUp", "retryExecuted", "batchExecuted", "loopExecuted", "runtimeEnablementExecuted", "tradingEnablementExecuted", "schedulerEnablementExecuted", "orderPathEnablementExecuted", "defaultGatewayRegistrationChanged", "liveConnectionScriptCreated", "realCredentialLoadingExecuted", "hostedServiceAdded", "backgroundWorkerAdded", "apiWorkerWiringAdded", "r31Authorized")) {
    Assert-False $nonRun.$property "Non-run $property"
}
Assert-Equal $nonRun.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Gateway mode"

$marketDataCodePath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxRealReadOnlyMarketDataFrameBoundaryProvider.cs"
$credentialCodePath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxRealReadOnlyCredentialConfigBoundaryProvider.cs"
$marketDataTestPath = Join-Path $RepoRoot "tests\QQ.Production.Intraday.Tests.Unit\LmaxRealReadOnlyMarketDataFrameBoundaryProviderTests.cs"
$credentialTestPath = Join-Path $RepoRoot "tests\QQ.Production.Intraday.Tests.Unit\LmaxRealReadOnlyCredentialConfigBoundaryProviderTests.cs"
$completenessTestPath = Join-Path $RepoRoot "tests\QQ.Production.Intraday.Tests.Unit\LmaxReadOnlyFinalProviderCompletenessTests.cs"
foreach ($path in @($marketDataCodePath, $credentialCodePath, $marketDataTestPath, $credentialTestPath, $completenessTestPath)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing R30 file: $path" }
}

$marketDataCode = Get-Content -LiteralPath $marketDataCodePath -Raw
$credentialCode = Get-Content -LiteralPath $credentialCodePath -Raw
foreach ($needle in @("LmaxRealReadOnlyMarketDataFrameBoundaryProvider", "ILmaxRealReadOnlyMarketDataFrameBoundaryProvider", "LmaxReadOnlyMarketDataRequestOptions", "ILmaxReadOnlyMarketDataFrameClient", "ExternalMarketDataRequestExecutionApproved")) {
    Assert-TextContains $marketDataCode $needle "R30 MarketData code token"
}
foreach ($needle in @("LmaxRealReadOnlyCredentialConfigBoundaryProvider", "ILmaxRealReadOnlyCredentialConfigBoundaryProvider", "LmaxReadOnlyCredentialConfigOptions", "ILmaxReadOnlyCredentialConfigClient", "ExternalCredentialAccessApproved")) {
    Assert-TextContains $credentialCode $needle "R30 credential/config code token"
}
foreach ($pattern in @("TcpClient", "System\.Net\.Sockets", "new\s+Socket", "SslStream", "NetworkStream", "AuthenticateAsClient", "SendAsync", "AddHostedService")) {
    Assert-TextNotContainsPattern $marketDataCode $pattern "R30 MarketData code must not include live execution token"
    Assert-TextNotContainsPattern $credentialCode $pattern "R30 credential/config code must not include live execution token"
}

$marketDataImplementations = Select-String -Path (Join-Path $RepoRoot "src\QQ.Production.Intraday.Infrastructure.Lmax\*.cs") -Pattern "class\s+LmaxRealReadOnlyMarketDataFrameBoundaryProvider\s*:\s*ILmaxRealReadOnlyMarketDataFrameBoundaryProvider"
if (@($marketDataImplementations).Count -ne 1) {
    throw "Expected exactly one non-test LmaxRealReadOnlyMarketDataFrameBoundaryProvider implementation."
}

$credentialImplementations = Select-String -Path (Join-Path $RepoRoot "src\QQ.Production.Intraday.Infrastructure.Lmax\*.cs") -Pattern "class\s+LmaxRealReadOnlyCredentialConfigBoundaryProvider\s*:\s*ILmaxRealReadOnlyCredentialConfigBoundaryProvider"
if (@($credentialImplementations).Count -ne 1) {
    throw "Expected exactly one non-test LmaxRealReadOnlyCredentialConfigBoundaryProvider implementation."
}

$programPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\Program.cs"
$workerPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Worker\Program.cs"
$settingsPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\appsettings.json"
$program = Get-Content -LiteralPath $programPath -Raw
$worker = Get-Content -LiteralPath $workerPath -Raw
$settings = Get-Content -LiteralPath $settingsPath -Raw
Assert-TextContains $program "FakeLmaxGateway" "API default gateway"
foreach ($pattern in @("LmaxRealReadOnlyMarketDataFrameBoundaryProvider", "LmaxRealReadOnlyCredentialConfigBoundaryProvider", "LmaxReadOnlyMarketDataRequestOptions", "LmaxReadOnlyCredentialConfigOptions", "phase-lmax-r30")) {
    Assert-TextNotContainsPattern $program $pattern "API wiring must not include R30 provider"
    Assert-TextNotContainsPattern $worker $pattern "Worker wiring must not include R30 provider"
    Assert-TextNotContainsPattern $settings $pattern "Default config must not include R30 provider"
}

$validation = [ordered]@{
    phase = "LMAX-R30"
    validator = "scripts/check-lmax-r30-final-provider-completion-sweep-gate.ps1"
    validationPassed = $true
    finalDecision = "LMAX_R30_FINAL_PROVIDER_SWEEP_COMPLETE_NO_EXTERNAL_ACTIVATION"
    r29DecisionConfirmed = $true
    allRequiredArtifactsExist = $true
    marketDataProviderCodeExists = $true
    nonTestMarketDataProviderImplementationExists = $true
    credentialConfigProviderStatus = "implemented"
    nonTestCredentialConfigProviderImplementationExists = $true
    finalProviderCompletenessPassed = $true
    specificRemainingBlocker = $null
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
    r31Authorized = $false
    apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
    recommendedNextPhase = "Phase LMAX-R31 - Operator-Approved Single Temporary Demo Read-Only Activation After Final Provider Sweep"
}

$validationPath = Join-Path $readiness "phase-lmax-r30-gate-validation.json"
($validation | ConvertTo-Json -Depth 8) + [Environment]::NewLine | Set-Content -LiteralPath $validationPath -Encoding UTF8
Assert-NoSensitiveContent $validationPath

Write-Host "LMAX-R30 gate validation passed."
Write-Host "Wrote $validationPath"
