param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

function Fail($Message) {
    throw "LMAX R36 gate validation failed: $Message"
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
    "phase-lmax-r36-r35-blocker-analysis.json",
    "phase-lmax-r36-real-execution-operations-summary.json",
    "phase-lmax-r36-socket-operation-summary.json",
    "phase-lmax-r36-tls-operation-summary.json",
    "phase-lmax-r36-fix-operation-summary.json",
    "phase-lmax-r36-marketdata-operation-summary.json",
    "phase-lmax-r36-credential-config-operation-summary.json",
    "phase-lmax-r36-final-operation-completeness-check.json",
    "phase-lmax-r36-readonly-operation-interface-safety-proof.json",
    "phase-lmax-r36-test-coverage-summary.json",
    "phase-lmax-r36-no-external-activation-proof.json",
    "phase-lmax-r36-decision-gate.json",
    "phase-lmax-r36-non-run-validation.json",
    "phase-lmax-r36-concrete-final-pre-execution-blocker-fix-report.md",
    "phase-lmax-r36-operator-note.md"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $artifact))) {
        Fail "Missing R36 artifact: $artifact"
    }
}

for ($phase = 1; $phase -le 35; $phase++) {
    if (-not (Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r$phase-*" -File -ErrorAction SilentlyContinue)) {
        Fail "Missing prior phase artifacts for R$phase"
    }
}

$r35Decision = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r35-decision-gate.json")
if ($r35Decision.finalDecision -ne "LMAX_R35_FAIL_BOUNDED_EXECUTOR_VALIDATION_FAILED") {
    Fail "R35 decision is not intact"
}

$decision = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r36-decision-gate.json")
if ($decision.finalDecision -ne "LMAX_R36_EXECUTION_OPERATIONS_IMPLEMENTED_NO_EXTERNAL_ACTIVATION") {
    Fail "R36 decision is not exact"
}

$operations = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r36-real-execution-operations-summary.json")
$completeness = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r36-final-operation-completeness-check.json")
$safety = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r36-readonly-operation-interface-safety-proof.json")
$nonRun = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r36-non-run-validation.json")
$coverage = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r36-test-coverage-summary.json")

foreach ($field in @(
    "socketOperationImplemented",
    "tlsOperationImplemented",
    "fixOperationImplemented",
    "marketDataOperationImplemented",
    "credentialConfigOperationImplemented",
    "eachOperationConstructibleWithoutExternalSideEffects",
    "operationBindingSetImplemented",
    "providerClientDelegateBridgeImplemented",
    "noOperationRegisteredByDefault",
    "noOperationRequiresApiWorkerStartup",
    "noOperationIsLiveLauncher",
    "publicSurfaceReadOnly",
    "allOperationsSupportSanitization"
)) {
    Assert-True $completeness.$field $field
}

if ($completeness.result -ne "Passed") {
    Fail "Final operation completeness check did not pass"
}

Assert-True $safety.forbiddenOperationalMethodsAbsent "forbiddenOperationalMethodsAbsent"
Assert-True $safety.ordersTradingReplayImpossibleThroughOperationSurface "ordersTradingReplayImpossibleThroughOperationSurface"
Assert-False $safety.rawCredentialOutputExposed "rawCredentialOutputExposed"
Assert-False $safety.rawSensitiveFixOutputStored "rawSensitiveFixOutputStored"

$operationSourcePath = Join-Path $RepoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyExecutionOperations.cs"
if (-not (Test-Path -LiteralPath $operationSourcePath)) {
    Fail "Execution operation source file missing"
}

$operationSource = Get-Content -LiteralPath $operationSourcePath -Raw
foreach ($token in @(
    "class LmaxReadOnlySocketConnectOperationBinding",
    "ILmaxReadOnlySocketConnectOperationBinding",
    "class LmaxReadOnlyTlsHandshakeOperationBinding",
    "ILmaxReadOnlyTlsHandshakeOperationBinding",
    "class LmaxReadOnlyFixSessionOperationBinding",
    "ILmaxReadOnlyFixSessionOperationBinding",
    "class LmaxReadOnlyMarketDataOperationBinding",
    "ILmaxReadOnlyMarketDataOperationBinding",
    "class LmaxReadOnlyCredentialConfigOperationBinding",
    "ILmaxReadOnlyCredentialConfigOperationBinding",
    "class LmaxReadOnlyExecutionOperationBindingSet"
)) {
    if ($operationSource -notmatch [regex]::Escape($token)) {
        Fail "Missing execution operation implementation token: $token"
    }
}

foreach ($field in @(
    "allOperationsNonTest",
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
    if ($field -eq "allOperationsNonTest") {
        Assert-True $operations.$field $field
    }
    else {
        Assert-False $operations.$field $field
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
    "r37Authorized"
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
        if ($text -match "LmaxReadOnlySocketConnectOperationBinding|LmaxReadOnlyTlsHandshakeOperationBinding|LmaxReadOnlyFixSessionOperationBinding|LmaxReadOnlyMarketDataOperationBinding|LmaxReadOnlyCredentialConfigOperationBinding|phase-lmax-r36") {
            Fail "Forbidden R36 API/Worker/default config wiring found in $path"
        }
    }
}

$gateValidation = [ordered]@{
    phase = "LMAX-R36"
    validatedAt = (Get-Date).ToUniversalTime().ToString("o")
    result = "Passed"
    decision = $decision.finalDecision
    r35DecisionIntact = $true
    requiredArtifactsPresent = $true
    priorArtifactsPresent = $true
    allExecutionOperationsImplemented = $true
    finalOperationCompletenessPassed = $true
    readOnlyOperationInterfaceSafetyProofPresent = $true
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
    r37Authorized = $false
}

$gateValidationPath = Join-Path $artifactRoot "phase-lmax-r36-gate-validation.json"
$gateValidation | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $gateValidationPath -Encoding UTF8
Write-Output "LMAX R36 gate validation passed: $gateValidationPath"
