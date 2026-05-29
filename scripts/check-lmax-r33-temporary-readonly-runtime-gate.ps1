param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

function Fail($Message) {
    throw "LMAX R33 gate validation failed: $Message"
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
    "phase-lmax-r33-operator-approval-record.json",
    "phase-lmax-r33-preflight-gate.json",
    "phase-lmax-r33-final-bounded-executor-validation.json",
    "phase-lmax-r33-temporary-runtime-activation-record.json",
    "phase-lmax-r33-approved-instrument-status-record.json",
    "phase-lmax-r33-sanitized-runtime-boundary-evidence.json",
    "phase-lmax-r33-forbidden-action-validation.json",
    "phase-lmax-r33-shutdown-revert-record.json",
    "phase-lmax-r33-post-attempt-non-mutation-validation.json",
    "phase-lmax-r33-rail-isolation-validation.json",
    "phase-lmax-r33-decision-gate.json",
    "phase-lmax-r33-temporary-readonly-runtime-report.md",
    "phase-lmax-r33-operator-note.md"
)

foreach ($artifact in $requiredArtifacts) {
    $path = Join-Path $artifactRoot $artifact
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing R33 artifact: $artifact"
    }
}

$priorDecisionPath = Join-Path $artifactRoot "phase-lmax-r32-decision-gate.json"
$priorDecision = Read-JsonFile $priorDecisionPath
if ($priorDecision.decision -ne "LMAX_R32_BOUNDED_TEMPORARY_EXECUTOR_IMPLEMENTED_NO_EXTERNAL_ACTIVATION" -and
    $priorDecision.finalDecision -ne "LMAX_R32_BOUNDED_TEMPORARY_EXECUTOR_IMPLEMENTED_NO_EXTERNAL_ACTIVATION") {
    Fail "R32 decision is not intact"
}

$missingPrior = @()
for ($phase = 1; $phase -le 32; $phase++) {
    $pattern = "phase-lmax-r$phase-*"
    if (-not (Get-ChildItem -LiteralPath $artifactRoot -Filter $pattern -File -ErrorAction SilentlyContinue)) {
        $missingPrior += "R$phase"
    }
}

if ($missingPrior.Count -gt 0) {
    Fail "Missing prior phase artifacts: $($missingPrior -join ', ')"
}

$approval = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r33-operator-approval-record.json")
$expectedApproval = "I, Philippe, explicitly approve Phase LMAX-R33 for one temporary Demo read-only runtime market-data activation attempt using the bounded executor for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority."
if ($approval.approvalPhrase -ne $expectedApproval) {
    Fail "Operator approval phrase is not exact"
}

$preflight = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r33-preflight-gate.json")
$finalValidation = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r33-final-bounded-executor-validation.json")
$activation = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r33-temporary-runtime-activation-record.json")
$instruments = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r33-approved-instrument-status-record.json")
$forbidden = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r33-forbidden-action-validation.json")
$shutdown = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r33-shutdown-revert-record.json")
$nonMutation = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r33-post-attempt-non-mutation-validation.json")
$rail = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r33-rail-isolation-validation.json")
$decision = Read-JsonFile (Join-Path $artifactRoot "phase-lmax-r33-decision-gate.json")

if ($preflight.passed -ne $true) {
    Fail "Preflight did not pass"
}

if ($finalValidation.resultClassification -ne "LMAX_R33_FAIL_BOUNDED_EXECUTOR_VALIDATION_FAILED" -or
    $finalValidation.concreteFinalAbortCause -ne "BoundedExecutorValidationFailedExecutableProviderClientsMissing" -or
    $finalValidation.validationPassed -ne $false) {
    Fail "Final bounded-executor validation result is not explicit"
}

$allowedDecisions = @(
    "LMAX_R33_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED",
    "LMAX_R33_FAIL_PREFLIGHT_ABORTED",
    "LMAX_R33_FAIL_BOUNDED_EXECUTOR_VALIDATION_FAILED",
    "LMAX_R33_FAIL_UNSAFE_PROVIDER_STACK_REJECTED",
    "LMAX_R33_FAIL_CREDENTIAL_CONFIG_MISSING",
    "LMAX_R33_FAIL_CREDENTIAL_CONFIG_NOT_APPROVED",
    "LMAX_R33_FAIL_REQUIRES_API_WORKER_STARTUP",
    "LMAX_R33_FAIL_REQUIRES_LIVE_LAUNCHER",
    "LMAX_R33_FAIL_PROVIDER_COMPLETENESS_REGRESSION",
    "LMAX_R33_FAIL_TCP_RUNTIME_BOUNDARY",
    "LMAX_R33_FAIL_TLS_RUNTIME_BOUNDARY",
    "LMAX_R33_FAIL_FIX_LOGON_RUNTIME_BOUNDARY",
    "LMAX_R33_FAIL_MARKETDATA_RUNTIME_BOUNDARY",
    "LMAX_R33_FAIL_SESSION_OR_MARKETDATA_BOUNDARY",
    "LMAX_R33_FAIL_SAFETY_CONSTRAINT",
    "LMAX_R33_INCONCLUSIVE_SANITIZED_EVIDENCE"
)

if ($allowedDecisions -notcontains $decision.finalDecision) {
    Fail "R33 decision is not an allowed classification"
}

if ($decision.finalDecision -ne "LMAX_R33_FAIL_BOUNDED_EXECUTOR_VALIDATION_FAILED") {
    Fail "R33 decision is not the expected safe abort classification"
}

if ($decision.concreteFinalAbortCause -ne "BoundedExecutorValidationFailedExecutableProviderClientsMissing") {
    Fail "R33 concrete abort cause is not specific"
}

if ($activation.attemptCount -gt 1) {
    Fail "More than one activation attempt recorded"
}

if ($activation.activationAttemptExecuted -ne $false) {
    Fail "Activation must not have executed for this abort"
}

if ($activation.retryCount -ne 0 -or $activation.batchMode -ne $false -or $activation.loopMode -ne $false) {
    Fail "Retry, batch, or loop mode was recorded"
}

Assert-True $instruments.approvedInstrumentsOnly "approvedInstrumentsOnly"
Assert-True $instruments.usdJpyCaveatPreserved "usdJpyCaveatPreserved"

foreach ($entry in $instruments.instruments) {
    if ($entry.status -ne "InScopeNotTouched") {
        Fail "Instrument status must remain InScopeNotTouched for $($entry.symbol)"
    }
}

$falseFlags = @(
    "ordersSubmitted",
    "orderPathEnabled",
    "tradingStateMutated",
    "schedulerStarted",
    "pollingStarted",
    "replayExecuted",
    "shadowReplaySubmitted",
    "productionAccountUsed",
    "orderGatewayRegistered",
    "tradingGatewayRegistered",
    "defaultGatewayRegistrationChanged",
    "runtimeEnablementPersisted",
    "liveLauncherCreated",
    "hostedServiceAdded",
    "backgroundWorkerAdded",
    "apiWorkerStarted"
)

foreach ($flag in $falseFlags) {
    Assert-False $forbidden.$flag $flag
}

Assert-True $nonMutation.outputSanitized "outputSanitized"
Assert-False $nonMutation.credentialsPrinted "credentialsPrinted"
Assert-False $nonMutation.credentialsStored "credentialsStored"

$activationFalseFlags = @(
    "externalRunExecuted",
    "runtimePoweredUp",
    "runtimeEnablementExecuted",
    "runtimeEnablementPersisted"
)

foreach ($flag in $activationFalseFlags) {
    Assert-False $activation.$flag $flag
}

$nonRunFalseFlags = @(
    "replayExecuted",
    "orderSubmissionExecuted",
    "tradingStateMutated",
    "schedulerStarted",
    "pollingStarted",
    "shadowReplaySubmitted",
    "runtimeEnablementPersisted",
    "defaultGatewayRegistrationChanged",
    "hostedServiceAdded",
    "backgroundWorkerAdded"
)

foreach ($flag in $nonRunFalseFlags) {
    Assert-False $nonMutation.$flag $flag
}

Assert-True $shutdown.shutdownOrRevertCompleted "shutdownOrRevertCompleted"
Assert-False $rail.validatedEvidenceArchivesModified "validatedEvidenceArchivesModified"
Assert-False $rail.phase7AThrough7NArchivesModified "phase7AThrough7NArchivesModified"
Assert-False $rail.usdJpyT1ThroughT7ArtifactsModified "usdJpyT1ThroughT7ArtifactsModified"
Assert-False $rail.r1ThroughR32ArtifactsModifiedExceptReadOnlyReference "r1ThroughR32ArtifactsModifiedExceptReadOnlyReference"
Assert-True $rail.gbpusdArchiveIntact "gbpusdArchiveIntact"
Assert-True $rail.eurgbpArchiveIntact "eurgbpArchiveIntact"
Assert-True $rail.audusdArchiveIntact "audusdArchiveIntact"
Assert-True $rail.usdJpyT7ClosureIntact "usdJpyT7ClosureIntact"
Assert-True $rail.phase7NClosureIntact "phase7NClosureIntact"

$apiProgram = Join-Path $RepoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$apiSettings = Join-Path $RepoRoot "src/QQ.Production.Intraday.Api/appsettings.json"
$workerProgram = Join-Path $RepoRoot "src/QQ.Production.Intraday.Worker/Program.cs"

if (-not (Test-Path -LiteralPath $apiProgram)) {
    Fail "API Program.cs missing"
}

$apiProgramText = Get-Content -LiteralPath $apiProgram -Raw
if ($apiProgramText -notmatch "FakeLmaxGateway") {
    Fail "API/Worker default gateway mode could not be confirmed as FakeLmaxGatewayOnly"
}

foreach ($path in @($apiProgram, $apiSettings, $workerProgram)) {
    if (Test-Path -LiteralPath $path) {
        $text = Get-Content -LiteralPath $path -Raw
        if ($text -match "phase-lmax-r33|LmaxTemporaryReadOnlyActivationExecutor|BoundedTemporaryExecutor|TemporaryReadOnlyActivationExecutor") {
            Fail "Forbidden R33 API/Worker/default config wiring found in $path"
        }
    }
}

$scriptMatches = Get-ChildItem -LiteralPath (Join-Path $RepoRoot "scripts") -File -Filter "*r33*" -ErrorAction SilentlyContinue |
    Where-Object {
        $_.Name -notin @("check-lmax-r33-temporary-readonly-runtime-gate.ps1") -and
        (Get-Content -LiteralPath $_.FullName -Raw) -match "LmaxTemporaryReadOnlyActivationExecutor|TcpClient|SslStream|MarketDataRequest"
    }

if ($scriptMatches) {
    Fail "Potential live R33 connection script found: $($scriptMatches.Name -join ', ')"
}

$gateValidation = [ordered]@{
    phase = "LMAX-R33"
    validatedAt = (Get-Date).ToUniversalTime().ToString("o")
    result = "Passed"
    decision = $decision.finalDecision
    concreteFinalAbortCause = $decision.concreteFinalAbortCause
    priorDecisionIntact = $true
    requiredArtifactsPresent = $true
    priorArtifactsPresent = $true
    operatorApprovalExact = $true
    preflightPassedBeforeActivation = $true
    finalBoundedExecutorValidationExplicit = $true
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
    r34Authorized = $false
}

$gateValidationPath = Join-Path $artifactRoot "phase-lmax-r33-gate-validation.json"
$gateValidation | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $gateValidationPath -Encoding UTF8
Write-Output "LMAX R33 gate validation passed: $gateValidationPath"
