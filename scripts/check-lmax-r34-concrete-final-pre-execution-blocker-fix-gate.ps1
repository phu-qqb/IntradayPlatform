param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

function Fail($Message) {
    throw "LMAX R34 gate validation failed: $Message"
}

function Read-JsonFile($Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing required file: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Assert-False($Value, $Name) {
    if ($Value -ne $false) {
        Fail "$Name must be false"
    }
}

function Assert-True($Value, $Name) {
    if ($Value -ne $true) {
        Fail "$Name must be true"
    }
}

$artifactRoot = Join-Path $RepoRoot "artifacts/readiness/lmax-runtime-enablement"
$requiredArtifacts = @(
    "phase-lmax-r34-r33-blocker-analysis.json",
    "phase-lmax-r34-real-provider-clients-summary.json",
    "phase-lmax-r34-socket-client-summary.json",
    "phase-lmax-r34-tls-client-summary.json",
    "phase-lmax-r34-fix-client-summary.json",
    "phase-lmax-r34-marketdata-client-summary.json",
    "phase-lmax-r34-credential-config-client-summary.json",
    "phase-lmax-r34-final-client-completeness-check.json",
    "phase-lmax-r34-readonly-client-interface-safety-proof.json",
    "phase-lmax-r34-test-coverage-summary.json",
    "phase-lmax-r34-no-external-activation-proof.json",
    "phase-lmax-r34-decision-gate.json",
    "phase-lmax-r34-non-run-validation.json",
    "phase-lmax-r34-concrete-final-pre-execution-blocker-fix-report.md",
    "phase-lmax-r34-operator-note.md"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $artifact))) {
        Fail "Missing R34 artifact: $artifact"
    }
}

for ($phase = 1; $phase -le 33; $phase++) {
    if (-not (Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r$phase-*" -File -ErrorAction SilentlyContinue)) {
        Fail "Missing prior phase artifacts for R$phase"
    }
}

$r33Decision = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r33-decision-gate.json")
if ($r33Decision.finalDecision -ne "LMAX_R33_FAIL_BOUNDED_EXECUTOR_VALIDATION_FAILED") {
    Fail "R33 decision is not intact"
}

$decision = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r34-decision-gate.json")
if ($decision.finalDecision -ne "LMAX_R34_EXECUTABLE_PROVIDER_CLIENTS_IMPLEMENTED_NO_EXTERNAL_ACTIVATION") {
    Fail "R34 decision is not exact"
}

$clients = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r34-real-provider-clients-summary.json")
$completeness = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r34-final-client-completeness-check.json")
$safety = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r34-readonly-client-interface-safety-proof.json")
$nonRun = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r34-non-run-validation.json")
$coverage = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r34-test-coverage-summary.json")

foreach ($field in @(
    "socketClientImplemented",
    "tlsClientImplemented",
    "fixClientImplemented",
    "marketDataClientImplemented",
    "credentialConfigClientImplemented",
    "eachClientConstructibleWithoutExternalSideEffects",
    "noClientRegisteredByDefault",
    "noClientRequiresApiWorkerStartup",
    "noClientIsLiveLauncher",
    "publicSurfaceReadOnly",
    "allClientsSupportSanitization"
)) {
    Assert-True $completeness.$field $field
}

if ($completeness.result -ne "Passed") {
    Fail "Final client completeness check did not pass"
}

Assert-True $safety.forbiddenOperationalMethodsAbsent "forbiddenOperationalMethodsAbsent"
Assert-True $safety.ordersTradingReplayImpossibleThroughClientSurface "ordersTradingReplayImpossibleThroughClientSurface"
Assert-False $safety.rawCredentialOutputExposed "rawCredentialOutputExposed"
Assert-False $safety.rawSensitiveFixOutputStored "rawSensitiveFixOutputStored"

$clientSourcePath = Join-Path $RepoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxRealReadOnlyProviderClients.cs"
if (-not (Test-Path -LiteralPath $clientSourcePath)) {
    Fail "Provider client source file missing"
}

$clientSource = Get-Content -LiteralPath $clientSourcePath -Raw
foreach ($token in @(
    "class LmaxRealReadOnlySocketConnectionClient",
    "ILmaxReadOnlySocketConnectionClient",
    "class LmaxRealReadOnlyTlsHandshakeClient",
    "ILmaxReadOnlyTlsHandshakeClient",
    "class LmaxRealReadOnlyFixFrameClient",
    "ILmaxReadOnlyFixFrameClient",
    "class LmaxRealReadOnlyMarketDataFrameClient",
    "ILmaxReadOnlyMarketDataFrameClient",
    "class LmaxRealReadOnlyCredentialConfigClient",
    "ILmaxReadOnlyCredentialConfigClient"
)) {
    if ($clientSource -notmatch [regex]::Escape($token)) {
        Fail "Missing provider client implementation token: $token"
    }
}

foreach ($field in @(
    "allClientsNonTest",
    "constructorsOpenSockets",
    "constructorsPerformTcp",
    "constructorsPerformTls",
    "constructorsSendFix",
    "constructorsSendMarketDataRequest",
    "constructorsLoadCredentials",
    "registeredByDefault",
    "apiWorkerUsed",
    "externalActivationExecuted"
)) {
    if ($field -eq "allClientsNonTest") {
        Assert-True $clients.$field $field
    }
    else {
        Assert-False $clients.$field $field
    }
}

foreach ($field in @(
    "realSocketOpenedInTests",
    "realTlsHandshakeInTests",
    "realFixMessageSentInTests",
    "realMarketDataRequestSentInTests",
    "realCredentialLoadingInTests"
)) {
    Assert-False $coverage.$field $field
}

foreach ($field in @(
    "externalRunExecuted",
    "realSnapshotExecuted",
    "replayExecuted",
    "postEndpointInvoked",
    "realSocketOpened",
    "realTcpConnectionAttempted",
    "realTlsHandshakeAttempted",
    "realFixLogonAttempted",
    "realFixMessageSent",
    "realMarketDataRequestSent",
    "realCredentialLoadingExecuted",
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
    "hostedServiceAdded",
    "backgroundWorkerAdded",
    "apiWorkerWiringAdded",
    "r35Authorized"
)) {
    Assert-False $nonRun.$field $field
}

if ($nonRun.apiWorkerGatewayMode -ne "FakeLmaxGatewayOnly") {
    Fail "API/Worker gateway mode is not FakeLmaxGatewayOnly"
}

$apiProgram = Join-Path $RepoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$apiSettings = Join-Path $RepoRoot "src/QQ.Production.Intraday.Api/appsettings.json"
$workerProgram = Join-Path $RepoRoot "src/QQ.Production.Intraday.Worker/Program.cs"

$apiProgramText = Get-Content -LiteralPath $apiProgram -Raw
if ($apiProgramText -notmatch "FakeLmaxGateway") {
    Fail "FakeLmaxGateway default was not confirmed"
}

foreach ($path in @($apiProgram, $apiSettings, $workerProgram)) {
    if (Test-Path -LiteralPath $path) {
        $text = Get-Content -LiteralPath $path -Raw
        if ($text -match "LmaxRealReadOnlySocketConnectionClient|LmaxRealReadOnlyTlsHandshakeClient|LmaxRealReadOnlyFixFrameClient|LmaxRealReadOnlyMarketDataFrameClient|LmaxRealReadOnlyCredentialConfigClient|phase-lmax-r34") {
            Fail "Forbidden R34 API/Worker/default config wiring found in $path"
        }
    }
}

$gateValidation = [ordered]@{
    phase = "LMAX-R34"
    validatedAt = (Get-Date).ToUniversalTime().ToString("o")
    result = "Passed"
    decision = $decision.finalDecision
    r33DecisionIntact = $true
    requiredArtifactsPresent = $true
    priorArtifactsPresent = $true
    allExecutableProviderClientsImplemented = $true
    finalClientCompletenessPassed = $true
    readOnlyClientInterfaceSafetyProofPresent = $true
    noExternalActionOccurred = $true
    noRealSocketOpened = $true
    noRealTcpConnectionAttempted = $true
    noRealTlsHandshakeAttempted = $true
    noRealFixLogonAttempted = $true
    noRealFixMessageSent = $true
    noRealMarketDataRequestSent = $true
    noRealCredentialLoadingExecuted = $true
    apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
    evidenceArchivesModified = $false
    liveConnectionScriptCreated = $false
    defaultConfigChangedForLmaxConnectivity = $false
    hostedServiceAdded = $false
    backgroundWorkerAdded = $false
    apiWorkerWiringAdded = $false
    r35Authorized = $false
}

$gateValidationPath = Join-Path $artifactRoot "phase-lmax-r34-gate-validation.json"
$gateValidation | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $gateValidationPath -Encoding UTF8
Write-Output "LMAX R34 gate validation passed: $gateValidationPath"
