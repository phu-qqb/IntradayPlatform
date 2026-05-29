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

$requiredR7 = @(
    "phase-lmax-r7-local-gate-harness-summary.json",
    "phase-lmax-r7-dry-run-activation-scope.json",
    "phase-lmax-r7-operator-approval-template-validation.json",
    "phase-lmax-r7-dry-run-preflight-result.json",
    "phase-lmax-r7-forbidden-path-validation.json",
    "phase-lmax-r7-shutdown-revert-schema-validation.json",
    "phase-lmax-r7-test-coverage-summary.json",
    "phase-lmax-r7-decision-gate.json",
    "phase-lmax-r7-non-run-validation.json",
    "phase-lmax-r7-local-gate-harness-report.md",
    "phase-lmax-r7-operator-note.md"
)

$requiredPrior = @(
    "phase-lmax-r1-design-only-decision-gate.json",
    "phase-lmax-r2-preflight-decision-gate.json",
    "phase-lmax-r3-decision-gate.json",
    "phase-lmax-r4-remediation-decision-gate.json",
    "phase-lmax-r5-inert-implementation-decision-gate.json",
    "phase-lmax-r6-decision-gate.json"
)

Write-Host "LMAX-R7 Local Runtime Gate Harness Validator"
Write-Host "This validator performs no external run, snapshot, replay, POST endpoint, socket, runtime activation, API/Worker startup, credential loading, or config mutation."

foreach ($file in ($requiredPrior + $requiredR7)) {
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

$r6Gate = Read-Json (Join-Path $readiness "phase-lmax-r6-decision-gate.json")
$r7Summary = Read-Json (Join-Path $readiness "phase-lmax-r7-local-gate-harness-summary.json")
$r7Scope = Read-Json (Join-Path $readiness "phase-lmax-r7-dry-run-activation-scope.json")
$r7Approval = Read-Json (Join-Path $readiness "phase-lmax-r7-operator-approval-template-validation.json")
$r7Preflight = Read-Json (Join-Path $readiness "phase-lmax-r7-dry-run-preflight-result.json")
$r7Forbidden = Read-Json (Join-Path $readiness "phase-lmax-r7-forbidden-path-validation.json")
$r7Shutdown = Read-Json (Join-Path $readiness "phase-lmax-r7-shutdown-revert-schema-validation.json")
$r7Tests = Read-Json (Join-Path $readiness "phase-lmax-r7-test-coverage-summary.json")
$r7Gate = Read-Json (Join-Path $readiness "phase-lmax-r7-decision-gate.json")
$r7NonRun = Read-Json (Join-Path $readiness "phase-lmax-r7-non-run-validation.json")
$t7Gate = Read-Json $t7GatePath
$phase7nGate = Read-Json $phase7nGatePath

Assert-Equal $r6Gate.finalDecision "LMAX_R6_INERT_RUNTIME_INTEGRATION_PREFLIGHT_COMPLETE_NO_ACTIVATION" "R6 decision"
Assert-Equal $t7Gate.finalDecision "USDJPY_T7_FINAL_READINESS_ARCHIVE_CLOSED_WITH_CAVEAT" "USDJPY T7 decision"
Assert-Equal $phase7nGate.finalDecision "PASS_FINAL_LMAX_READONLY_RUNTIME_EVIDENCE_ARCHIVE_CLOSED" "Phase 7N decision"

Assert-Equal $r7Gate.phase "LMAX-R7" "R7 phase"
Assert-Equal $r7Gate.finalDecision "LMAX_R7_LOCAL_RUNTIME_GATE_HARNESS_READY_NO_ACTIVATION" "R7 final decision"
Assert-True $r7Gate.localGateHarnessImplemented "Harness implemented"
Assert-True $r7Gate.usesR5InertContracts "Uses R5 inert contracts"
Assert-True $r7Gate.dryRunActivationScopeBuilt "Dry-run scope built"
Assert-True $r7Gate.operatorApprovalTemplateValidated "Approval template validated"
Assert-False $r7Gate.activeR8AuthorizationCollected "Active R8 authorization collected"
Assert-False $r7Gate.r8Authorized "R8 authorized"
Assert-True $r7Gate.approvedInstrumentAllowlistValidated "Allowlist validated"
Assert-True $r7Gate.usdJpyCaveatPreserved "USDJPY caveat preserved"
Assert-True $r7Gate.safetyFlagsValidated "Safety flags validated"
Assert-True $r7Gate.shutdownRevertSchemaValidated "Shutdown/revert schema validated"
Assert-True $r7Gate.dryRunHarnessPassed "Dry-run harness passed"

Assert-True $r7Summary.harnessImplemented "Summary harness implemented"
Assert-True $r7Summary.usesR5InertContracts "Summary uses R5"
Assert-True $r7Summary.dryRunOnly "Summary dry-run only"
Assert-True $r7Summary.localOnly "Summary local-only"
Assert-False $r7Summary.r8Authorized "Summary R8 authorized"
Assert-False $r7Summary.credentialLoadingAdded "Summary credential loading"
Assert-False $r7Summary.liveConnectionScriptCreated "Summary live launcher"
Assert-False $r7Summary.apiWorkerWiringAdded "Summary API/Worker wiring"

Assert-True $r7Scope.scopeBuilt "Scope built"
Assert-Equal $r7Scope.environment "Demo" "Scope environment"
Assert-True $r7Scope.temporary "Scope temporary"
Assert-True $r7Scope.inertValidatorOnly "Scope inert"
Assert-True $r7Scope.shutdownRevertPlanPresent "Scope shutdown/revert"
Assert-True $r7Scope.scopeValidationPassed "Scope validation"
Assert-False $r7Scope.r8Authorized "Scope R8 authorized"

foreach ($symbol in @("GBPUSD", "EURGBP", "AUDUSD", "USDJPY")) {
    if (-not (@($r7Scope.approvedInstruments | Where-Object { $_.symbol -eq $symbol }).Count -eq 1)) {
        throw "Dry-run scope missing approved instrument: $symbol"
    }
}
$usdJpyScope = @($r7Scope.approvedInstruments | Where-Object { $_.symbol -eq "USDJPY" })[0]
Assert-Equal $usdJpyScope.securityId "4004" "USDJPY SecurityID"
Assert-Equal $usdJpyScope.securityIdSource "8" "USDJPY SecurityIDSource"
Assert-Equal $usdJpyScope.caveat "prior failed-safe root cause remains unproven" "USDJPY caveat"

Assert-True $r7Approval.templatePresent "Approval template present"
Assert-True $r7Approval.templateMatchesExpected "Approval template matches"
Assert-False $r7Approval.activeAuthorization "Approval active authorization"
Assert-False $r7Approval.r8Authorized "Approval R8 authorized"

Assert-True $r7Preflight.preflightPassed "Dry-run preflight passed"
Assert-False $r7Preflight.activeAuthorization "Preflight active authorization"
Assert-False $r7Preflight.r8Authorized "Preflight R8 authorized"
Assert-False $r7Preflight.externalRunExecuted "Preflight external run"
Assert-False $r7Preflight.runtimeActivationExecuted "Preflight runtime activation"
Assert-False $r7Preflight.credentialLoadingAdded "Preflight credential loading"

Assert-True $r7Forbidden.passed "Forbidden path validation passed"
foreach ($property in @(
    "ordersSubmitted",
    "orderPathEnabled",
    "allowOrderSubmission",
    "allowLiveTrading",
    "isTradingEnabled",
    "schedulerStarted",
    "schedulerEnabled",
    "pollingStarted",
    "pollingEnabled",
    "replayExecuted",
    "replayEnabled",
    "shadowReplaySubmitted",
    "shadowReplayEnabled",
    "tradingStateMutated",
    "tradingMutationEnabled",
    "productionAccountUsed",
    "orderGatewayRegistered",
    "tradingGatewayRegistered",
    "persistentRuntimeEnablementRequested",
    "defaultGatewayRegistrationChangeRequested"
)) {
    Assert-False $r7Forbidden.$property "Forbidden path $property"
}

Assert-True $r7Shutdown.shutdownRevertPlanRequired "Shutdown/revert required"
Assert-True $r7Shutdown.shutdownRevertPlanPresent "Shutdown/revert present"
Assert-True $r7Shutdown.schemaValidationPassed "Shutdown/revert schema passed"
Assert-False $r7Shutdown.runtimeActivationExecuted "Shutdown runtime activation"
Assert-False $r7Shutdown.shutdownExecuted "Shutdown executed"
Assert-False $r7Shutdown.revertExecuted "Revert executed"

foreach ($coverage in @("valid dry-run scope passes", "R8 approval phrase template is recognized as template only", "R8 active authorization is false", "USDJPY without caveat fails", "non-approved instrument fails", "production account fails", "orders enabled fails", "live trading enabled fails", "scheduler enabled fails", "polling enabled fails", "replay enabled fails", "shadow replay enabled fails", "trading mutation enabled fails", "persistent runtime enablement fails", "default gateway registration change fails", "missing shutdown/revert plan fails", "sanitization disabled fails", "no credential loading/network dependency exists")) {
    if (-not ($r7Tests.coverage -contains $coverage)) {
        throw "R7 test coverage missing: $coverage"
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
    "hostedServiceAdded",
    "backgroundWorkerAdded",
    "apiWorkerWiringAdded",
    "r8Authorized"
)) {
    Assert-False $r7Gate.$property "R7 gate $property"
    Assert-False $r7NonRun.$property "R7 non-run $property"
}

Assert-Equal $r7Gate.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "R7 gate gateway mode"
Assert-Equal $r7NonRun.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "R7 non-run gateway mode"
Assert-False $r7Gate.evidenceArchivesModified "Evidence archives modified"
Assert-True $r7Gate.usdJpyT7ClosureIntact "USDJPY T7 closure intact"
Assert-True $r7Gate.validatedRailsArchivesIntact "Validated rails intact"

$codePath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyRuntimeActivationGateHarness.cs"
$testPath = Join-Path $RepoRoot "tests\QQ.Production.Intraday.Tests.Unit\LmaxReadOnlyRuntimeActivationGateHarnessTests.cs"
if (-not (Test-Path -LiteralPath $codePath)) { throw "Missing R7 code file: $codePath" }
if (-not (Test-Path -LiteralPath $testPath)) { throw "Missing R7 test file: $testPath" }
Assert-NoSensitiveContent $codePath
Assert-NoSensitiveContent $testPath

$codeText = Get-Content -LiteralPath $codePath -Raw
foreach ($forbiddenPattern in @("TcpClient", "Socket", "SslStream", "NetworkStream", "ConnectAsync", "QuickFix", "CredentialProfileResolver", "SessionPassword")) {
    if ($codeText -match [regex]::Escape($forbiddenPattern)) {
        throw "R7 harness contains forbidden live/credential pattern: $forbiddenPattern"
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
if ($apiProgram -match "AddHostedService<.*Lmax" -or $workerProgram -match "AddHostedService<.*Lmax") {
    throw "LMAX hosted service registration detected."
}

$appSettings = Read-Json (Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\appsettings.json")
Assert-False $appSettings.Safety.AllowExternalConnections "Safety:AllowExternalConnections"
Assert-False $appSettings.Safety.AllowLiveTrading "Safety:AllowLiveTrading"
Assert-True $appSettings.Safety.RequireFakeExecutionGateway "Safety:RequireFakeExecutionGateway"
Assert-False $appSettings.LmaxReadOnlyRuntime.Enabled "LmaxReadOnlyRuntime:Enabled"
Assert-Equal $appSettings.LmaxReadOnlyRuntime.ImplementationMode "DesignOnly" "LmaxReadOnlyRuntime:ImplementationMode"
Assert-False $appSettings.LmaxReadOnlyRuntime.AllowExternalConnections "LmaxReadOnlyRuntime:AllowExternalConnections"
Assert-False $appSettings.LmaxReadOnlyRuntime.AllowCredentialUse "LmaxReadOnlyRuntime:AllowCredentialUse"
Assert-False $appSettings.LmaxReadOnlyRuntime.AllowOrderSubmission "LmaxReadOnlyRuntime:AllowOrderSubmission"
Assert-False $appSettings.LmaxReadOnlyRuntime.SchedulerEnabled "LmaxReadOnlyRuntime:SchedulerEnabled"
Assert-False $appSettings.LmaxReadOnlyRuntime.SubmitToShadowReplay "LmaxReadOnlyRuntime:SubmitToShadowReplay"

$report = Get-Content -LiteralPath (Join-Path $readiness "phase-lmax-r7-local-gate-harness-report.md") -Raw
foreach ($marker in @(
    "Executive summary",
    "R6 context",
    "Scope and non-run guarantees",
    "Local-only gate harness design",
    "Dry-run activation scope",
    "Operator approval template validation",
    "Dry-run preflight result",
    "Forbidden path validation",
    "Shutdown/revert schema validation",
    "Tests added/updated",
    "What R7 allows",
    "What remains forbidden",
    "Decision",
    "Recommended next phase"
)) {
    if ($report -notmatch [regex]::Escape($marker)) {
        throw "Report missing marker: $marker"
    }
}

$validation = [ordered]@{
    phase = "LMAX-R7"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    validator = "scripts/check-lmax-r7-local-runtime-gate-harness.ps1"
    allRequiredArtifactsExist = $true
    r6Decision = $r6Gate.finalDecision
    r7Decision = $r7Gate.finalDecision
    dryRunHarnessPassed = $true
    r8Authorized = $false
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
    hostedServiceAdded = $false
    backgroundWorkerAdded = $false
    apiWorkerWiringAdded = $false
    apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
    finalDecision = $r7Gate.finalDecision
    result = "PASS"
}

$validationPath = Join-Path $readiness "phase-lmax-r7-gate-validation.json"
$validation | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $validationPath -Encoding UTF8

Write-Host "FinalDecision: $($r7Gate.finalDecision)"
Write-Host "Report: $validationPath"
