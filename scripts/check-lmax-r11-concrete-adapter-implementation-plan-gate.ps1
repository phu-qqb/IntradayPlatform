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
    "phase-lmax-r10-gate-validation.json"
)

$requiredR11 = @(
    "phase-lmax-r11-r10-blocker-analysis.json",
    "phase-lmax-r11-existing-code-reuse-survey.json",
    "phase-lmax-r11-concrete-adapter-design.json",
    "phase-lmax-r11-r12-implementation-boundary.json",
    "phase-lmax-r11-r13-execution-boundary.json",
    "phase-lmax-r11-concrete-adapter-safety-requirements.json",
    "phase-lmax-r11-r12-test-plan.json",
    "phase-lmax-r11-decision-gate.json",
    "phase-lmax-r11-non-run-validation.json",
    "phase-lmax-r11-concrete-adapter-implementation-plan.md",
    "phase-lmax-r11-operator-note.md"
)

Write-Host "LMAX-R11 Concrete Adapter Implementation Plan Validator"
Write-Host "This validator performs no external run, snapshot, replay, POST endpoint, socket, runtime activation, API/Worker startup, credential loading, or config mutation."

Assert-RequiredArtifacts $readiness $requiredPrior
Assert-RequiredArtifacts $readiness $requiredR11

$t7GatePath = Join-Path $usdJpy "phase-usdjpy-t7-final-closure-gate.json"
$phase7nGatePath = Join-Path $RepoRoot "artifacts\readiness\phase7n-final-lmax-readonly-runtime-evidence-archive-closure-gate.json"
if (-not (Test-Path -LiteralPath $t7GatePath)) {
    throw "Missing USDJPY T7 closure gate: $t7GatePath"
}
if (-not (Test-Path -LiteralPath $phase7nGatePath)) {
    throw "Missing Phase 7N closure gate: $phase7nGatePath"
}
Assert-NoSensitiveContent $t7GatePath
Assert-NoSensitiveContent $phase7nGatePath

$r10Gate = Read-Json (Join-Path $readiness "phase-lmax-r10-decision-gate.json")
$blocker = Read-Json (Join-Path $readiness "phase-lmax-r11-r10-blocker-analysis.json")
$survey = Read-Json (Join-Path $readiness "phase-lmax-r11-existing-code-reuse-survey.json")
$design = Read-Json (Join-Path $readiness "phase-lmax-r11-concrete-adapter-design.json")
$r12 = Read-Json (Join-Path $readiness "phase-lmax-r11-r12-implementation-boundary.json")
$r13 = Read-Json (Join-Path $readiness "phase-lmax-r11-r13-execution-boundary.json")
$safety = Read-Json (Join-Path $readiness "phase-lmax-r11-concrete-adapter-safety-requirements.json")
$tests = Read-Json (Join-Path $readiness "phase-lmax-r11-r12-test-plan.json")
$gate = Read-Json (Join-Path $readiness "phase-lmax-r11-decision-gate.json")
$nonRun = Read-Json (Join-Path $readiness "phase-lmax-r11-non-run-validation.json")
$t7Gate = Read-Json $t7GatePath
$phase7nGate = Read-Json $phase7nGatePath

Assert-Equal $r10Gate.finalDecision "LMAX_R10_FAIL_NO_CONCRETE_REAL_ADAPTER_PATH" "R10 decision"
Assert-Equal $t7Gate.finalDecision "USDJPY_T7_FINAL_READINESS_ARCHIVE_CLOSED_WITH_CAVEAT" "USDJPY T7 decision"
Assert-Equal $phase7nGate.finalDecision "PASS_FINAL_LMAX_READONLY_RUNTIME_EVIDENCE_ARCHIVE_CLOSED" "Phase 7N decision"

Assert-True $blocker.r10DecisionConfirmed "Blocker R10 confirmed"
Assert-True $blocker.r10PreflightPassed "Blocker preflight"
Assert-False $blocker.r10ActivationExecuted "Blocker activation"
Assert-False $blocker.r10ExternalRunExecuted "Blocker external"
Assert-False $blocker.r10TcpTlsFixMarketDataAttempted "Blocker TCP/TLS/FIX/MD"
Assert-Equal $blocker.missingArtifact "Concrete temporary Demo read-only runtime activation adapter" "Missing artifact"

foreach ($path in @(
    "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxTemporaryReadOnlyRuntimeAdapterPath.cs",
    "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyRuntimeActivationGateHarness.cs",
    "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxTemporaryReadOnlyRuntimeActivation.cs",
    "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlySocketPrototype.cs",
    "tools/QQ.Production.Intraday.Lmax.ConnectivityLab",
    "src/QQ.Production.Intraday.Api/Program.cs",
    "src/QQ.Production.Intraday.Api/appsettings.json"
)) {
    if (-not (@($survey.items | Where-Object { $_.path -eq $path }).Count -ge 1)) {
        throw "Reuse survey missing $path"
    }
}

Assert-Equal $design.classToImplementInR12 "LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter" "Concrete adapter class"
Assert-Equal $design.interface "ILmaxTemporaryReadOnlyRuntimeActivationAdapter" "Adapter interface"
Assert-Equal $design.r12DefaultTransport "FakeTransportOnly" "R12 default transport"
Assert-Equal $design.externalTransportInR12 "Not allowed unless a future phase explicitly authorizes execution; default implementation must be fake/test-double only." "R12 external transport"

Assert-Equal $r12.r12RecommendedPhase "Phase LMAX-R12 - Concrete Read-Only Runtime Adapter Implementation With Fake Transport, No External Activation" "R12 phase"
Assert-False $r12.externalConnectionAllowedInR12 "R12 external connection"
Assert-False $r12.r12AuthorizedByR11 "R12 authorized"
Assert-Equal $r13.firstPossibleExternalAttemptPhase "Phase LMAX-R13 - Operator-Approved Single Temporary Demo Read-Only Activation Using Concrete Adapter" "R13 first external phase"
Assert-False $r13.r13AuthorizedByR11 "R13 authorized"

foreach ($property in @("productionAccount", "orderGatewayRegistration", "tradingGatewayRegistration", "allowOrderSubmission", "allowLiveTrading", "isTradingEnabled", "scheduler", "polling", "replay", "shadowReplaySubmit", "tradingMutation", "apiWorkerStartup", "defaultConfigMutation", "credentialPrintingOrPersistence", "rawFixPersistence", "nonApprovedInstrument", "missingUsdJpyCaveat")) {
    Assert-True $safety.blocks.$property "Safety block $property"
}

foreach ($testName in @(
    "valid fake-transport adapter path succeeds locally",
    "unsafe flags fail before transport use",
    "non-approved instruments fail",
    "USDJPY without caveat fails",
    "production account fails",
    "orders enabled fails",
    "live trading enabled fails",
    "scheduler enabled fails",
    "polling enabled fails",
    "replay enabled fails",
    "shadow replay enabled fails",
    "trading mutation enabled fails",
    "credentials are not loaded in constructors or tests",
    "no network calls occur in tests",
    "sanitized result contains no secrets",
    "shutdown/revert called exactly once",
    "fake transport receives approved instruments only",
    "adapter returns correct TCP/TLS/FIX/MarketData boundary statuses from fake transport",
    "API/Worker defaults unchanged",
    "default appsettings do not enable LMAX connectivity"
)) {
    if (-not ($tests.tests -contains $testName)) {
        throw "R12 test plan missing: $testName"
    }
}
Assert-False $tests.externalIntegrationTestsAllowedByDefault "External tests allowed"
Assert-False $tests.credentialsRequired "Credentials required"
Assert-False $tests.apiWorkerStartupRequired "API/Worker startup required"

Assert-Equal $gate.finalDecision "LMAX_R11_CONCRETE_ADAPTER_IMPLEMENTATION_PLAN_READY_NO_ACTIVATION" "R11 final decision"
Assert-True $gate.planningCompleted "Gate planning"
Assert-True $gate.existingCodeSurveyCompleted "Gate survey"
Assert-True $gate.concreteAdapterDesignCompleted "Gate design"
Assert-True $gate.r12ImplementationBoundaryRecorded "Gate R12"
Assert-True $gate.r13ExecutionBoundaryRecorded "Gate R13"
Assert-True $gate.safetyRequirementsRecorded "Gate safety"
Assert-True $gate.r12TestPlanRecorded "Gate test plan"
Assert-Equal $gate.concreteAdapterToImplement "LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter" "Gate adapter"
Assert-False $gate.r12Authorized "Gate R12 authorized"
Assert-Equal $gate.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Gate gateway mode"
Assert-False $gate.evidenceArchivesModified "Gate archives"
Assert-True $gate.usdJpyT7ClosureIntact "Gate USDJPY T7"
Assert-True $gate.validatedRailsArchivesIntact "Gate validated rails"

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
    "r12Authorized"
)) {
    Assert-False $nonRun.$property "Non-run $property"
    Assert-False $gate.$property "Gate $property"
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
if ($programText -match 'LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter|ILmaxTemporaryReadOnlyRuntimeActivationAdapter') {
    throw "API Program.cs contains concrete adapter wiring."
}
if ($workerText -match 'LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter|ILmaxTemporaryReadOnlyRuntimeActivationAdapter') {
    throw "Worker Program.cs contains concrete adapter wiring."
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

$reportPath = Join-Path $readiness "phase-lmax-r11-concrete-adapter-implementation-plan.md"
$reportText = Get-Content -LiteralPath $reportPath -Raw
foreach ($heading in @(
    "Executive summary",
    "R10 blocker analysis",
    "Existing code reuse survey",
    "Concrete adapter design",
    "R12 implementation boundary",
    "R13 execution boundary",
    "Safety requirements",
    "R12 test plan",
    "What R11 allows",
    "What remains forbidden",
    "Decision",
    "Recommended next phase"
)) {
    if ($reportText -notmatch [regex]::Escape($heading)) {
        throw "R11 report missing required section: $heading"
    }
}

$validation = [ordered]@{
    phase = "LMAX-R11"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    validator = "scripts/check-lmax-r11-concrete-adapter-implementation-plan-gate.ps1"
    requiredArtifactsPresent = $true
    priorArtifactsPresent = $true
    r10DecisionConfirmed = $true
    planningCompleted = $true
    concreteAdapterToImplement = "LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter"
    r12Authorized = $false
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
    apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
    noSensitiveContent = $true
    recommendedNextPhase = $gate.recommendedNextPhase
    finalDecision = $gate.finalDecision
}

$validationPath = Join-Path $readiness "phase-lmax-r11-gate-validation.json"
$validation | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $validationPath -Encoding UTF8

Write-Host "PASS: $($gate.finalDecision)"
