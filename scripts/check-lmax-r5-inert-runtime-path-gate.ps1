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
        '(?i)sessionpassword\s*[:=]\s*[^,\s\}\]]+'
    )

    foreach ($pattern in $patterns) {
        if ($text -match $pattern) {
            throw "Sensitive-content marker found in $Path"
        }
    }
}

$readiness = Join-Path $RepoRoot "artifacts\readiness\lmax-runtime-enablement"
$usdJpy = Join-Path $RepoRoot "artifacts\readiness\usdjpy-troubleshooting"

$requiredR5 = @(
    "phase-lmax-r5-inert-runtime-path-implementation-summary.json",
    "phase-lmax-r5-approved-instrument-allowlist.json",
    "phase-lmax-r5-safety-validator-spec.json",
    "phase-lmax-r5-sanitization-contract-summary.json",
    "phase-lmax-r5-test-coverage-summary.json",
    "phase-lmax-r5-inert-implementation-decision-gate.json",
    "phase-lmax-r5-non-run-validation.json",
    "phase-lmax-r5-inert-runtime-path-report.md",
    "phase-lmax-r5-operator-note.md"
)

$requiredPrior = @(
    "phase-lmax-r1-design-only-decision-gate.json",
    "phase-lmax-r2-preflight-decision-gate.json",
    "phase-lmax-r3-decision-gate.json",
    "phase-lmax-r4-remediation-decision-gate.json"
)

Write-Host "LMAX-R5 Inert Runtime Path Gate Validator"
Write-Host "This validator performs no external run, snapshot, replay, POST endpoint, socket, runtime activation, or config mutation."

foreach ($file in ($requiredPrior + $requiredR5)) {
    $path = Join-Path $readiness $file
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing required artifact: $path"
    }
    Assert-NoSensitiveContent $path
}

$t7GatePath = Join-Path $usdJpy "phase-usdjpy-t7-final-closure-gate.json"
if (-not (Test-Path -LiteralPath $t7GatePath)) {
    throw "Missing USDJPY T7 closure gate: $t7GatePath"
}
Assert-NoSensitiveContent $t7GatePath

$phase7nGatePath = Join-Path $RepoRoot "artifacts\readiness\phase7n-final-lmax-readonly-runtime-evidence-archive-closure-gate.json"
if (-not (Test-Path -LiteralPath $phase7nGatePath)) {
    throw "Missing Phase 7N closure gate: $phase7nGatePath"
}
Assert-NoSensitiveContent $phase7nGatePath

$r1Gate = Read-Json (Join-Path $readiness "phase-lmax-r1-design-only-decision-gate.json")
$r2Gate = Read-Json (Join-Path $readiness "phase-lmax-r2-preflight-decision-gate.json")
$r3Gate = Read-Json (Join-Path $readiness "phase-lmax-r3-decision-gate.json")
$r4Gate = Read-Json (Join-Path $readiness "phase-lmax-r4-remediation-decision-gate.json")
$r5Summary = Read-Json (Join-Path $readiness "phase-lmax-r5-inert-runtime-path-implementation-summary.json")
$r5Allowlist = Read-Json (Join-Path $readiness "phase-lmax-r5-approved-instrument-allowlist.json")
$r5ValidatorSpec = Read-Json (Join-Path $readiness "phase-lmax-r5-safety-validator-spec.json")
$r5Sanitization = Read-Json (Join-Path $readiness "phase-lmax-r5-sanitization-contract-summary.json")
$r5Tests = Read-Json (Join-Path $readiness "phase-lmax-r5-test-coverage-summary.json")
$r5Gate = Read-Json (Join-Path $readiness "phase-lmax-r5-inert-implementation-decision-gate.json")
$r5NonRun = Read-Json (Join-Path $readiness "phase-lmax-r5-non-run-validation.json")
$t7Gate = Read-Json $t7GatePath
$phase7nGate = Read-Json $phase7nGatePath

Assert-Equal $r1Gate.finalDecision "LMAX_R1_RUNTIME_ENABLEMENT_DESIGN_REVIEW_COMPLETE_NO_ENABLEMENT" "R1 decision"
Assert-Equal $r2Gate.finalDecision "LMAX_R2_READONLY_RUNTIME_PREFLIGHT_READY_NO_ACTIVATION" "R2 decision"
Assert-Equal $r3Gate.finalDecision "LMAX_R3_FAIL_PREFLIGHT_ABORTED" "R3 decision"
Assert-Equal $r4Gate.finalDecision "LMAX_R4_PREFLIGHT_ABORT_REMEDIATION_READY_NO_ACTIVATION" "R4 decision"
Assert-Equal $t7Gate.finalDecision "USDJPY_T7_FINAL_READINESS_ARCHIVE_CLOSED_WITH_CAVEAT" "USDJPY T7 decision"
Assert-Equal $phase7nGate.finalDecision "PASS_FINAL_LMAX_READONLY_RUNTIME_EVIDENCE_ARCHIVE_CLOSED" "Phase 7N decision"

Assert-Equal $r5Gate.phase "LMAX-R5" "R5 phase"
Assert-Equal $r5Gate.finalDecision "LMAX_R5_INERT_READONLY_RUNTIME_PATH_IMPLEMENTED_NO_ACTIVATION" "R5 final decision"
Assert-True $r5Gate.inertRuntimePathImplemented "Inert runtime path implemented"
Assert-True $r5Gate.activationScopeModelsAdded "Activation scope models added"
Assert-True $r5Gate.approvedInstrumentAllowlistAdded "Approved instrument allowlist added"
Assert-True $r5Gate.safetyValidatorAdded "Safety validator added"
Assert-True $r5Gate.sanitizedOutputContractsAdded "Sanitized output contracts added"
Assert-True $r5Gate.testsAdded "Tests added"
Assert-False $r5Gate.r6Authorized "R6 authorized"

Assert-True $r5Summary.inertRuntimePathImplemented "Summary inert path implemented"
Assert-False $r5Summary.runtimeActivationImplemented "Runtime activation implemented"
Assert-False $r5Summary.liveConnectionLauncherCreated "Live launcher created"
Assert-False $r5Summary.defaultConfigChanged "Default config changed"
Assert-False $r5Summary.credentialLoadingAdded "Credential loading added"
Assert-False $r5Summary.networkingCodeAdded "Networking code added"
Assert-False $r5Summary.socketCodeAdded "Socket code added"
Assert-False $r5Summary.fixClientAdded "FIX client added"
Assert-False $r5Summary.marketDataRequestSenderAdded "MarketDataRequest sender added"

foreach ($symbol in @("GBPUSD", "EURGBP", "AUDUSD", "USDJPY")) {
    if (-not (@($r5Allowlist.approvedInstruments | Where-Object { $_.symbol -eq $symbol }).Count -eq 1)) {
        throw "Allowlist missing approved instrument: $symbol"
    }
}

$usdJpy = @($r5Allowlist.approvedInstruments | Where-Object { $_.symbol -eq "USDJPY" })[0]
Assert-Equal $usdJpy.securityId "4004" "USDJPY SecurityID"
Assert-Equal $usdJpy.securityIdSource "8" "USDJPY SecurityIDSource"
Assert-Equal $usdJpy.caveat "prior failed-safe root cause remains unproven" "USDJPY caveat"
Assert-True $r5Allowlist.nonApprovedInstrumentsRejected "Non-approved instruments rejected"
Assert-True $r5Allowlist.usdJpyCaveatRequired "USDJPY caveat required"

Assert-Equal $r5ValidatorSpec.validator "LmaxTemporaryReadOnlyRuntimeActivationValidator" "Validator name"
Assert-Equal $r5ValidatorSpec.networkingBehavior "None" "Validator networking behavior"
Assert-Equal $r5ValidatorSpec.credentialBehavior "None" "Validator credential behavior"

foreach ($requiredFail in @("operator approval missing", "production account requested", "instrument outside allowlist", "USDJPY included without caveat", "AllowOrderSubmission=true", "AllowLiveTrading=true", "IsTradingEnabled=true", "scheduler enabled", "polling enabled", "replay enabled", "shadow replay enabled", "trading mutation enabled", "persistent runtime enablement requested", "default gateway registration change requested", "output sanitization disabled", "shutdown/revert plan missing")) {
    if (-not ($r5ValidatorSpec.failsIf -contains $requiredFail)) {
        throw "Validator spec missing failure: $requiredFail"
    }
}

Assert-False $r5Sanitization.credentialLoadingAdded "Sanitization credential loading added"
Assert-True $r5Sanitization.outputSanitizationRequired "Output sanitization required"

foreach ($requiredCoverage in @("valid inert read-only scope passes", "production account fails", "orders enabled fails", "live trading enabled fails", "IsTradingEnabled=true fails", "scheduler enabled fails", "polling enabled fails", "replay enabled fails", "shadow replay enabled fails", "mutation enabled fails", "non-approved instrument fails", "USDJPY without caveat fails", "persistent runtime enablement fails", "default gateway registration change fails", "missing operator approval fails", "missing shutdown/revert plan fails", "sanitization disabled fails")) {
    if (-not ($r5Tests.testCoverage -contains $requiredCoverage)) {
        throw "Test coverage missing: $requiredCoverage"
    }
}

foreach ($property in @(
    "externalRunExecuted",
    "snapshotExecuted",
    "replayExecuted",
    "postEndpointInvoked",
    "realSocketOpened",
    "tcpConnectionAttempted",
    "tlsHandshakeAttempted",
    "fixLogonAttempted",
    "marketDataRequestSent",
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
    "r6Authorized"
)) {
    Assert-False $r5Gate.$property "R5 gate $property"
    Assert-False $r5NonRun.$property "R5 non-run $property"
}

Assert-Equal $r5Gate.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "R5 gate gateway mode"
Assert-Equal $r5NonRun.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "R5 non-run gateway mode"
Assert-False $r5Gate.evidenceArchivesModified "Evidence archives modified"
Assert-True $r5Gate.usdJpyT7ClosureIntact "USDJPY T7 closure intact"
Assert-True $r5Gate.validatedRailsArchivesIntact "Validated rails intact"

$codePath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxTemporaryReadOnlyRuntimeActivation.cs"
$testPath = Join-Path $RepoRoot "tests\QQ.Production.Intraday.Tests.Unit\LmaxTemporaryReadOnlyRuntimeActivationTests.cs"
if (-not (Test-Path -LiteralPath $codePath)) { throw "Missing R5 code file: $codePath" }
if (-not (Test-Path -LiteralPath $testPath)) { throw "Missing R5 test file: $testPath" }

$codeText = Get-Content -LiteralPath $codePath -Raw
foreach ($forbiddenPattern in @("TcpClient", "Socket", "SslStream", "NetworkStream", "ConnectAsync", "QuickFix", "CredentialProfileResolver", "Password", "SessionPassword")) {
    if ($codeText -match [regex]::Escape($forbiddenPattern)) {
        throw "R5 code file contains forbidden live/credential pattern: $forbiddenPattern"
    }
}

foreach ($requiredType in @("LmaxTemporaryReadOnlyRuntimeActivationScope", "LmaxReadOnlyRuntimeApprovedInstrument", "LmaxReadOnlyRuntimeSafetyFlags", "LmaxReadOnlyRuntimeOperatorApproval", "LmaxReadOnlyRuntimePreflightGate", "LmaxReadOnlyRuntimeBoundaryEvidence", "LmaxReadOnlyRuntimeForbiddenActionValidation", "LmaxReadOnlyRuntimeShutdownRevertRecord", "LmaxReadOnlyRuntimeNonMutationValidation", "LmaxReadOnlyRuntimeRailIsolationValidation", "LmaxTemporaryReadOnlyRuntimeActivationValidator")) {
    if ($codeText -notmatch [regex]::Escape($requiredType)) {
        throw "R5 code file missing type: $requiredType"
    }
}

$apiProgram = Get-Content -LiteralPath (Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\Program.cs") -Raw
$workerProgram = Get-Content -LiteralPath (Join-Path $RepoRoot "src\QQ.Production.Intraday.Worker\Program.cs") -Raw
if ($apiProgram -notmatch "AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>") {
    throw "API FakeLmaxGateway registration not found."
}
if ($workerProgram -notmatch "AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>") {
    throw "Worker FakeLmaxGateway registration not found."
}
if ($apiProgram -match "AddSingleton<IVenueExecutionGateway,\s*Lmax" -or $workerProgram -match "AddSingleton<IVenueExecutionGateway,\s*Lmax") {
    throw "Real LMAX execution gateway registration detected."
}

$appSettings = Read-Json (Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\appsettings.json")
Assert-False $appSettings.Safety.AllowExternalConnections "Safety:AllowExternalConnections"
Assert-False $appSettings.Safety.AllowLiveTrading "Safety:AllowLiveTrading"
Assert-True $appSettings.Safety.RequireFakeExecutionGateway "Safety:RequireFakeExecutionGateway"
Assert-False $appSettings.LmaxReadOnlyRuntime.Enabled "LmaxReadOnlyRuntime:Enabled"
Assert-Equal $appSettings.LmaxReadOnlyRuntime.ImplementationMode "DesignOnly" "LmaxReadOnlyRuntime:ImplementationMode"
Assert-False $appSettings.LmaxReadOnlyRuntime.AllowExternalConnections "LmaxReadOnlyRuntime:AllowExternalConnections"
Assert-False $appSettings.LmaxReadOnlyRuntime.AllowOrderSubmission "LmaxReadOnlyRuntime:AllowOrderSubmission"
Assert-False $appSettings.LmaxReadOnlyRuntime.SchedulerEnabled "LmaxReadOnlyRuntime:SchedulerEnabled"
Assert-False $appSettings.LmaxReadOnlyRuntime.SubmitToShadowReplay "LmaxReadOnlyRuntime:SubmitToShadowReplay"

$report = Get-Content -LiteralPath (Join-Path $readiness "phase-lmax-r5-inert-runtime-path-report.md") -Raw
foreach ($marker in @(
    "Executive summary",
    "R3/R4 context",
    "Scope and non-run guarantees",
    "Files/code added",
    "Inert runtime activation model",
    "Approved instrument allowlist",
    "Safety validator behavior",
    "Sanitization contracts",
    "Test coverage",
    "What R5 allows",
    "What remains forbidden",
    "Decision",
    "Recommended next phase"
)) {
    if ($report -notmatch [regex]::Escape($marker)) {
        throw "Report missing marker: $marker"
    }
}

$validation = [ordered]@{
    phase = "LMAX-R5"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    validator = "scripts/check-lmax-r5-inert-runtime-path-gate.ps1"
    allRequiredArtifactsExist = $true
    r4Decision = $r4Gate.finalDecision
    r5Decision = $r5Gate.finalDecision
    externalRunExecuted = $false
    snapshotExecuted = $false
    replayExecuted = $false
    postEndpointInvoked = $false
    realSocketOpened = $false
    tcpConnectionAttempted = $false
    tlsHandshakeAttempted = $false
    fixLogonAttempted = $false
    marketDataRequestSent = $false
    orderSubmissionExecuted = $false
    tradingStateMutated = $false
    schedulerStarted = $false
    pollingStarted = $false
    shadowReplaySubmitted = $false
    apiWorkerStarted = $false
    runtimePoweredUp = $false
    runtimeEnablementExecuted = $false
    tradingEnablementExecuted = $false
    schedulerEnablementExecuted = $false
    orderPathEnablementExecuted = $false
    defaultGatewayRegistrationChanged = $false
    liveConnectionScriptCreated = $false
    credentialLoadingAdded = $false
    r6Authorized = $false
    apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
    finalDecision = $r5Gate.finalDecision
    result = "PASS"
}

$validationPath = Join-Path $readiness "phase-lmax-r5-gate-validation.json"
$validation | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $validationPath -Encoding UTF8

Write-Host "FinalDecision: $($r5Gate.finalDecision)"
Write-Host "Report: $validationPath"
