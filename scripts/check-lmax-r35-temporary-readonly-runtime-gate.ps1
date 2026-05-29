param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

function Fail($Message) {
    throw "LMAX R35 gate validation failed: $Message"
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
    "phase-lmax-r35-operator-approval-record.json",
    "phase-lmax-r35-preflight-gate.json",
    "phase-lmax-r35-final-bounded-executor-provider-client-validation.json",
    "phase-lmax-r35-temporary-runtime-activation-record.json",
    "phase-lmax-r35-approved-instrument-status-record.json",
    "phase-lmax-r35-sanitized-runtime-boundary-evidence.json",
    "phase-lmax-r35-forbidden-action-validation.json",
    "phase-lmax-r35-shutdown-revert-record.json",
    "phase-lmax-r35-post-attempt-non-mutation-validation.json",
    "phase-lmax-r35-rail-isolation-validation.json",
    "phase-lmax-r35-decision-gate.json",
    "phase-lmax-r35-temporary-readonly-runtime-report.md",
    "phase-lmax-r35-operator-note.md"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot $artifact))) {
        Fail "Missing R35 artifact: $artifact"
    }
}

for ($phase = 1; $phase -le 34; $phase++) {
    if (-not (Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r$phase-*" -File -ErrorAction SilentlyContinue)) {
        Fail "Missing prior phase artifacts for R$phase"
    }
}

$r34Decision = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r34-decision-gate.json")
if ($r34Decision.finalDecision -ne "LMAX_R34_EXECUTABLE_PROVIDER_CLIENTS_IMPLEMENTED_NO_EXTERNAL_ACTIVATION") {
    Fail "R34 decision is not intact"
}

$approval = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r35-operator-approval-record.json")
$expectedApproval = "I, Philippe, explicitly approve Phase LMAX-R35 for one temporary Demo read-only runtime market-data activation attempt after the client completion sweep for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority."
if ($approval.approvalPhrase -ne $expectedApproval -or $approval.approvalPhraseMatchedExactly -ne $true) {
    Fail "Operator approval phrase is not exact"
}

$preflight = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r35-preflight-gate.json")
$finalValidation = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r35-final-bounded-executor-provider-client-validation.json")
$activation = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r35-temporary-runtime-activation-record.json")
$instruments = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r35-approved-instrument-status-record.json")
$boundary = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r35-sanitized-runtime-boundary-evidence.json")
$forbidden = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r35-forbidden-action-validation.json")
$shutdown = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r35-shutdown-revert-record.json")
$nonMutation = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r35-post-attempt-non-mutation-validation.json")
$rail = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r35-rail-isolation-validation.json")
$decision = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r35-decision-gate.json")

if ($preflight.passed -ne $true) {
    Fail "Preflight did not pass"
}

if ($finalValidation.resultClassification -ne "LMAX_R35_FAIL_BOUNDED_EXECUTOR_VALIDATION_FAILED" -or
    $finalValidation.concreteFinalAbortCause -ne "BoundedExecutorValidationFailedProviderClientExecutionOperationsNotConfigured" -or
    $finalValidation.validationPassed -ne $false) {
    Fail "Final bounded-executor/provider-client validation result is not explicit"
}

foreach ($field in @(
    "boundedExecutorExists",
    "boundedExecutorUsedForValidation",
    "executorIsNotLiveLauncher",
    "providerCompletenessRemainsPass",
    "clientCompletenessRemainsPass",
    "socketClientExists",
    "tlsClientExists",
    "fixClientExists",
    "marketDataClientExists",
    "credentialConfigClientExists",
    "credentialConfigProviderExists",
    "credentialLabelsPresent",
    "defaultClientOperationsWouldNotConnect"
)) {
    Assert-True $finalValidation.$field $field
}

foreach ($field in @(
    "mainConsoleScriptHostedServiceOrApiEndpointInvolved",
    "credentialValuesReturned",
    "credentialValuesPrinted",
    "credentialValuesStored",
    "apiWorkerStartupRequired",
    "defaultConfigMutationRequired",
    "hostedBackgroundServiceRequired",
    "liveLauncherCreated",
    "approvedStackBypass",
    "providerClientExecutionOperationsConfigured"
)) {
    Assert-False $finalValidation.$field $field
}

$allowedDecisions = @(
    "LMAX_R35_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED",
    "LMAX_R35_FAIL_PREFLIGHT_ABORTED",
    "LMAX_R35_FAIL_BOUNDED_EXECUTOR_VALIDATION_FAILED",
    "LMAX_R35_FAIL_PROVIDER_COMPLETENESS_REGRESSION",
    "LMAX_R35_FAIL_CLIENT_COMPLETENESS_REGRESSION",
    "LMAX_R35_FAIL_CREDENTIAL_CONFIG_MISSING",
    "LMAX_R35_FAIL_CREDENTIAL_CONFIG_NOT_APPROVED",
    "LMAX_R35_FAIL_REQUIRES_API_WORKER_STARTUP",
    "LMAX_R35_FAIL_REQUIRES_LIVE_LAUNCHER",
    "LMAX_R35_FAIL_TCP_RUNTIME_BOUNDARY",
    "LMAX_R35_FAIL_TLS_RUNTIME_BOUNDARY",
    "LMAX_R35_FAIL_FIX_LOGON_RUNTIME_BOUNDARY",
    "LMAX_R35_FAIL_MARKETDATA_RUNTIME_BOUNDARY",
    "LMAX_R35_FAIL_SESSION_OR_MARKETDATA_BOUNDARY",
    "LMAX_R35_FAIL_SAFETY_CONSTRAINT",
    "LMAX_R35_INCONCLUSIVE_SANITIZED_EVIDENCE"
)

if ($allowedDecisions -notcontains $decision.finalDecision) {
    Fail "R35 decision is not an allowed classification"
}

if ($decision.finalDecision -ne "LMAX_R35_FAIL_BOUNDED_EXECUTOR_VALIDATION_FAILED") {
    Fail "R35 decision is not the expected safe abort classification"
}

if ($decision.concreteFinalAbortCause -ne "BoundedExecutorValidationFailedProviderClientExecutionOperationsNotConfigured") {
    Fail "R35 concrete abort cause is not specific"
}

if ($activation.attemptCount -gt 1) {
    Fail "More than one activation attempt recorded"
}

Assert-False $activation.activationAttemptExecuted "activationAttemptExecuted"
Assert-False $activation.externalRunExecuted "externalRunExecuted"
Assert-False $activation.runtimePoweredUp "runtimePoweredUp"
Assert-False $activation.runtimeEnablementExecuted "runtimeEnablementExecuted"
Assert-False $activation.runtimeEnablementPersisted "runtimeEnablementPersisted"
if ($activation.retryCount -ne 0 -or $activation.batchMode -ne $false -or $activation.loopMode -ne $false) {
    Fail "Retry, batch, or loop mode was recorded"
}

Assert-True $instruments.approvedInstrumentsOnly "approvedInstrumentsOnly"
Assert-True $instruments.usdJpyCaveatPreserved "usdJpyCaveatPreserved"
Assert-False $instruments.nonApprovedInstrumentTouched "nonApprovedInstrumentTouched"
foreach ($entry in $instruments.instruments) {
    if ($entry.status -ne "InScopeNotTouched") {
        Fail "Instrument status must remain InScopeNotTouched for $($entry.symbol)"
    }
}

foreach ($field in @(
    "boundedExecutorConfirmed",
    "providerCompletenessConfirmed",
    "clientCompletenessConfirmed",
    "credentialConfigAccessSafe",
    "credentialLabelsPresent",
    "readOnlyInterfaceConfirmed",
    "boundedExecutorUsed",
    "outputSanitized"
)) {
    Assert-True $boundary.$field $field
}

foreach ($field in @(
    "credentialValuesReturned",
    "credentialValuesPrinted",
    "credentialValuesStored",
    "runtimeActivationAttempted",
    "temporaryReadOnlyMarketDataAdapterUsed",
    "concreteAdapterUsed",
    "realTransportUsed",
    "executableSessionClientUsed",
    "lowLevelStackUsed",
    "realLowLevelDependenciesUsed",
    "realDependencyProvidersUsed",
    "providerClientsUsed",
    "socketProviderUsed",
    "socketClientUsed",
    "tlsProviderUsed",
    "tlsClientUsed",
    "fixProviderUsed",
    "fixClientUsed",
    "marketDataProviderUsed",
    "marketDataClientUsed",
    "credentialConfigProviderUsed",
    "credentialConfigClientUsed"
)) {
    Assert-False $boundary.$field $field
}

foreach ($field in @(
    "ordersSubmitted",
    "orderPathEnabled",
    "orderGatewayRegistered",
    "tradingGatewayRegistered",
    "allowOrderSubmission",
    "allowLiveTrading",
    "isTradingEnabled",
    "schedulerStarted",
    "pollingStarted",
    "replayExecuted",
    "shadowReplaySubmitted",
    "tradingStateMutated",
    "productionAccountUsed",
    "defaultGatewayRegistrationChanged",
    "runtimeEnablementPersisted",
    "liveLauncherCreated",
    "hostedServiceAdded",
    "backgroundWorkerAdded",
    "apiWorkerStarted"
)) {
    Assert-False $forbidden.$field $field
}

Assert-True $forbidden.passed "forbiddenActionValidationPassed"
Assert-True $shutdown.shutdownOrRevertCompleted "shutdownOrRevertCompleted"

foreach ($field in @(
    "productionAccountUsed",
    "orderSubmissionExecuted",
    "orderPathEnabled",
    "orderGatewayRegistered",
    "tradingGatewayRegistered",
    "allowOrderSubmission",
    "allowLiveTrading",
    "isTradingEnabled",
    "schedulerStarted",
    "pollingStarted",
    "replayExecuted",
    "shadowReplaySubmitted",
    "tradingStateMutated",
    "defaultGatewayRegistrationChanged",
    "runtimeEnablementPersisted",
    "credentialsPrinted",
    "credentialsStored",
    "archivesModified",
    "liveLauncherCreated",
    "hostedServiceAdded",
    "backgroundWorkerAdded"
)) {
    Assert-False $nonMutation.$field $field
}

Assert-True $nonMutation.approvedInstrumentsOnly "postAttemptApprovedInstrumentsOnly"
Assert-True $nonMutation.usdJpyCaveatPreserved "postAttemptUsdJpyCaveatPreserved"
Assert-True $nonMutation.outputSanitized "postAttemptOutputSanitized"
Assert-True $nonMutation.shutdownOrRevertCompleted "postAttemptShutdownOrRevertCompleted"

foreach ($field in @(
    "validatedEvidenceArchivesModified",
    "phase7AThrough7NArchivesModified",
    "usdJpyT1ThroughT7ArtifactsModified",
    "r1ThroughR34ArtifactsModifiedExceptReadOnlyReference",
    "nonApprovedInstrumentTouched"
)) {
    Assert-False $rail.$field $field
}

Assert-True $rail.gbpusdArchiveIntact "gbpusdArchiveIntact"
Assert-True $rail.eurgbpArchiveIntact "eurgbpArchiveIntact"
Assert-True $rail.audusdArchiveIntact "audusdArchiveIntact"
Assert-True $rail.usdJpyT7ClosureIntact "usdJpyT7ClosureIntact"
Assert-True $rail.phase7NClosureIntact "phase7NClosureIntact"

$apiProgram = Join-Path $RepoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$apiSettings = Join-Path $RepoRoot "src/QQ.Production.Intraday.Api/appsettings.json"
$workerProgram = Join-Path $RepoRoot "src/QQ.Production.Intraday.Worker/Program.cs"

$apiProgramText = Get-Content -LiteralPath $apiProgram -Raw
if ($apiProgramText -notmatch "FakeLmaxGateway") {
    Fail "API/Worker default gateway mode could not be confirmed as FakeLmaxGatewayOnly"
}

foreach ($path in @($apiProgram, $apiSettings, $workerProgram)) {
    if (Test-Path -LiteralPath $path) {
        $text = Get-Content -LiteralPath $path -Raw
        if ($text -match "phase-lmax-r35|LmaxTemporaryReadOnlyActivationExecutor|LmaxRealReadOnlySocketConnectionClient|LmaxRealReadOnlyTlsHandshakeClient|LmaxRealReadOnlyFixFrameClient|LmaxRealReadOnlyMarketDataFrameClient|LmaxRealReadOnlyCredentialConfigClient") {
            Fail "Forbidden R35 API/Worker/default config wiring found in $path"
        }
    }
}

$scriptMatches = Get-ChildItem -LiteralPath (Join-Path $RepoRoot "scripts") -File -Filter "*r35*" -ErrorAction SilentlyContinue |
    Where-Object {
        $_.Name -notin @("check-lmax-r35-temporary-readonly-runtime-gate.ps1") -and
        (Get-Content -LiteralPath $_.FullName -Raw) -match "TcpClient|SslStream|MarketDataRequest|LmaxTemporaryReadOnlyActivationExecutor"
    }

if ($scriptMatches) {
    Fail "Potential live R35 connection script found: $($scriptMatches.Name -join ', ')"
}

$gateValidation = [ordered]@{
    phase = "LMAX-R35"
    validatedAt = (Get-Date).ToUniversalTime().ToString("o")
    result = "Passed"
    decision = $decision.finalDecision
    concreteFinalAbortCause = $decision.concreteFinalAbortCause
    priorDecisionIntact = $true
    requiredArtifactsPresent = $true
    priorArtifactsPresent = $true
    operatorApprovalExact = $true
    preflightPassedBeforeActivation = $true
    finalBoundedExecutorProviderClientValidationExplicit = $true
    attemptCount = $activation.attemptCount
    activationAttemptExecuted = $activation.activationAttemptExecuted
    noRetryBatchLoop = $true
    noForbiddenActionOccurred = $true
    productionAccountUsed = $false
    approvedInstrumentsOnly = $true
    usdJpyCaveatPreserved = $true
    outputSanitized = $true
    archivesModified = $false
    persistentRuntimeEnablementOccurred = $false
    defaultGatewayRegistrationChanged = $false
    liveLauncherCreated = $false
    hostedServiceAdded = $false
    backgroundWorkerAdded = $false
    apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
    shutdownOrRevertCompleted = $true
}

$gateValidationPath = Join-Path $artifactRoot "phase-lmax-r35-gate-validation.json"
$gateValidation | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $gateValidationPath -Encoding UTF8
Write-Output "LMAX R35 gate validation passed: $gateValidationPath"
