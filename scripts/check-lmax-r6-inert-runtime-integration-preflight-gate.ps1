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

$requiredR6 = @(
    "phase-lmax-r6-static-integration-review.json",
    "phase-lmax-r6-static-wiring-decision.json",
    "phase-lmax-r6-gateway-default-safety-review.json",
    "phase-lmax-r6-test-coverage-summary.json",
    "phase-lmax-r6-future-r7-local-harness-plan.json",
    "phase-lmax-r6-decision-gate.json",
    "phase-lmax-r6-non-run-validation.json",
    "phase-lmax-r6-inert-runtime-integration-preflight-report.md",
    "phase-lmax-r6-operator-note.md"
)

$requiredPrior = @(
    "phase-lmax-r1-design-only-decision-gate.json",
    "phase-lmax-r2-preflight-decision-gate.json",
    "phase-lmax-r3-decision-gate.json",
    "phase-lmax-r4-remediation-decision-gate.json",
    "phase-lmax-r5-inert-implementation-decision-gate.json"
)

Write-Host "LMAX-R6 Inert Runtime Integration Preflight Gate Validator"
Write-Host "This validator performs no external run, snapshot, replay, POST endpoint, socket, runtime activation, API/Worker startup, or config mutation."

foreach ($file in ($requiredPrior + $requiredR6)) {
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

$r5Gate = Read-Json (Join-Path $readiness "phase-lmax-r5-inert-implementation-decision-gate.json")
$r6Integration = Read-Json (Join-Path $readiness "phase-lmax-r6-static-integration-review.json")
$r6Wiring = Read-Json (Join-Path $readiness "phase-lmax-r6-static-wiring-decision.json")
$r6Gateway = Read-Json (Join-Path $readiness "phase-lmax-r6-gateway-default-safety-review.json")
$r6Tests = Read-Json (Join-Path $readiness "phase-lmax-r6-test-coverage-summary.json")
$r6R7 = Read-Json (Join-Path $readiness "phase-lmax-r6-future-r7-local-harness-plan.json")
$r6Gate = Read-Json (Join-Path $readiness "phase-lmax-r6-decision-gate.json")
$r6NonRun = Read-Json (Join-Path $readiness "phase-lmax-r6-non-run-validation.json")
$t7Gate = Read-Json $t7GatePath
$phase7nGate = Read-Json $phase7nGatePath

Assert-Equal $r5Gate.finalDecision "LMAX_R5_INERT_READONLY_RUNTIME_PATH_IMPLEMENTED_NO_ACTIVATION" "R5 decision"
Assert-Equal $t7Gate.finalDecision "USDJPY_T7_FINAL_READINESS_ARCHIVE_CLOSED_WITH_CAVEAT" "USDJPY T7 decision"
Assert-Equal $phase7nGate.finalDecision "PASS_FINAL_LMAX_READONLY_RUNTIME_EVIDENCE_ARCHIVE_CLOSED" "Phase 7N decision"

Assert-Equal $r6Gate.phase "LMAX-R6" "R6 phase"
Assert-Equal $r6Gate.finalDecision "LMAX_R6_INERT_RUNTIME_INTEGRATION_PREFLIGHT_COMPLETE_NO_ACTIVATION" "R6 final decision"
Assert-True $r6Gate.staticIntegrationReviewCompleted "Static integration review completed"
Assert-True $r6Gate.staticWiringDecisionRecorded "Static wiring decision recorded"
Assert-False $r6Gate.staticWiringAdded "Static wiring added"
Assert-True $r6Gate.staticWiringDeferred "Static wiring deferred"
Assert-True $r6Gate.gatewayDefaultSafetyReviewed "Gateway default safety reviewed"
Assert-True $r6Gate.testsAdded "Tests added"
Assert-True $r6Gate.futureR7LocalHarnessPlanRecorded "Future R7 plan recorded"
Assert-False $r6Gate.r7Authorized "R7 authorized"

Assert-False $r6Wiring.staticWiringAdded "Static wiring decision added"
Assert-True $r6Wiring.staticWiringDeferred "Static wiring decision deferred"
Assert-Equal $r6Gateway.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Gateway review mode"
Assert-False $r6Gateway.lmaxRuntimeGatewayRegisteredByDefault "LMAX runtime gateway registered by default"
Assert-False $r6Gateway.hostedServiceAddedForLmaxRuntime "Hosted service added"
Assert-False $r6Gateway.backgroundWorkerAddedForLmaxRuntime "Background worker added"
Assert-False $r6Gateway.r5ValidatorNetworkDependencies "R5 network dependencies"
Assert-False $r6Gateway.r5ValidatorCredentialDependencies "R5 credential dependencies"
Assert-True $r6Gateway.usdJpyCaveatPreserved "USDJPY caveat preserved"
Assert-False $r6R7.r7AuthorizedByR6 "R7 authorized by R6"

foreach ($requiredArea in @("API startup", "Worker startup", "Gateway registration", "FakeLmaxGateway default mode", "LMAX read-only adapter design", "Risk/governance flags", "Operator audit", "Exception/case management", "UI/status display", "Appsettings/default config")) {
    if (-not (@($r6Integration.integrationPoints | Where-Object { $_.area -eq $requiredArea }).Count -eq 1)) {
        throw "Integration review missing area: $requiredArea"
    }
}

foreach ($coverage in @("API and Worker default execution gateway remain FakeLmaxGatewayOnly", "No LMAX execution gateway is registered by default", "Default appsettings keep LMAX runtime disabled and DesignOnly", "R5 inert path has no live transport or credential dependency patterns", "R5 inert validator can be used without network dependencies", "Approved instrument allowlist preserves USDJPY caveat")) {
    if (-not ($r6Tests.coverage -contains $coverage)) {
        throw "R6 test coverage missing: $coverage"
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
    "r7Authorized"
)) {
    Assert-False $r6Gate.$property "R6 gate $property"
    Assert-False $r6NonRun.$property "R6 non-run $property"
}

Assert-Equal $r6Gate.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "R6 gate gateway mode"
Assert-Equal $r6NonRun.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "R6 non-run gateway mode"
Assert-False $r6Gate.evidenceArchivesModified "Evidence archives modified"
Assert-True $r6Gate.usdJpyT7ClosureIntact "USDJPY T7 closure intact"
Assert-True $r6Gate.validatedRailsArchivesIntact "Validated rails intact"

$testPath = Join-Path $RepoRoot "tests\QQ.Production.Intraday.Tests.Unit\LmaxReadOnlyRuntimeStaticIntegrationSafetyTests.cs"
if (-not (Test-Path -LiteralPath $testPath)) {
    throw "Missing R6 test file: $testPath"
}
Assert-NoSensitiveContent $testPath

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

$r5Code = Get-Content -LiteralPath (Join-Path $RepoRoot "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxTemporaryReadOnlyRuntimeActivation.cs") -Raw
foreach ($forbiddenPattern in @("TcpClient", "Socket", "SslStream", "NetworkStream", "ConnectAsync", "QuickFix", "CredentialProfileResolver", "SessionPassword")) {
    if ($r5Code -match [regex]::Escape($forbiddenPattern)) {
        throw "R5/R6 inert code contains forbidden live/credential pattern: $forbiddenPattern"
    }
}

$report = Get-Content -LiteralPath (Join-Path $readiness "phase-lmax-r6-inert-runtime-integration-preflight-report.md") -Raw
foreach ($marker in @(
    "Executive summary",
    "R5 context",
    "Scope and non-run guarantees",
    "Static integration review",
    "Static wiring decision",
    "Gateway/default safety review",
    "Tests added/updated",
    "Future R7 local harness plan",
    "What R6 allows",
    "What remains forbidden",
    "Decision",
    "Recommended next phase"
)) {
    if ($report -notmatch [regex]::Escape($marker)) {
        throw "Report missing marker: $marker"
    }
}

$validation = [ordered]@{
    phase = "LMAX-R6"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    validator = "scripts/check-lmax-r6-inert-runtime-integration-preflight-gate.ps1"
    allRequiredArtifactsExist = $true
    r5Decision = $r5Gate.finalDecision
    r6Decision = $r6Gate.finalDecision
    staticWiringAdded = $false
    staticWiringDeferred = $true
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
    r7Authorized = $false
    apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
    finalDecision = $r6Gate.finalDecision
    result = "PASS"
}

$validationPath = Join-Path $readiness "phase-lmax-r6-gate-validation.json"
$validation | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $validationPath -Encoding UTF8

Write-Host "FinalDecision: $($r6Gate.finalDecision)"
Write-Host "Report: $validationPath"
