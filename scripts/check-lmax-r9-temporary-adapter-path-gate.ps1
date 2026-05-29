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
    "phase-lmax-r8-preflight-gate.json",
    "phase-lmax-r8-gate-validation.json"
)

$requiredR9 = @(
    "phase-lmax-r9-r8-preflight-abort-analysis.json",
    "phase-lmax-r9-temporary-adapter-path-implementation-summary.json",
    "phase-lmax-r9-harness-backed-request-contract.json",
    "phase-lmax-r9-dry-run-adapter-validation-summary.json",
    "phase-lmax-r9-safety-and-no-network-proof.json",
    "phase-lmax-r9-test-coverage-summary.json",
    "phase-lmax-r9-decision-gate.json",
    "phase-lmax-r9-non-run-validation.json",
    "phase-lmax-r9-temporary-adapter-path-report.md",
    "phase-lmax-r9-operator-note.md"
)

Write-Host "LMAX-R9 Temporary Adapter Path Gate Validator"
Write-Host "This validator performs no external run, snapshot, replay, POST endpoint, socket, runtime activation, API/Worker startup, credential loading, or config mutation."

Assert-RequiredArtifacts $readiness $requiredPrior
Assert-RequiredArtifacts $readiness $requiredR9

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

$r8Gate = Read-Json (Join-Path $readiness "phase-lmax-r8-decision-gate.json")
$r9Abort = Read-Json (Join-Path $readiness "phase-lmax-r9-r8-preflight-abort-analysis.json")
$r9Summary = Read-Json (Join-Path $readiness "phase-lmax-r9-temporary-adapter-path-implementation-summary.json")
$r9Contract = Read-Json (Join-Path $readiness "phase-lmax-r9-harness-backed-request-contract.json")
$r9DryRun = Read-Json (Join-Path $readiness "phase-lmax-r9-dry-run-adapter-validation-summary.json")
$r9Proof = Read-Json (Join-Path $readiness "phase-lmax-r9-safety-and-no-network-proof.json")
$r9Tests = Read-Json (Join-Path $readiness "phase-lmax-r9-test-coverage-summary.json")
$r9Gate = Read-Json (Join-Path $readiness "phase-lmax-r9-decision-gate.json")
$r9NonRun = Read-Json (Join-Path $readiness "phase-lmax-r9-non-run-validation.json")
$t7Gate = Read-Json $t7GatePath
$phase7nGate = Read-Json $phase7nGatePath

Assert-Equal $r8Gate.finalDecision "LMAX_R8_FAIL_PREFLIGHT_ABORTED" "R8 decision"
Assert-Equal $t7Gate.finalDecision "USDJPY_T7_FINAL_READINESS_ARCHIVE_CLOSED_WITH_CAVEAT" "USDJPY T7 decision"
Assert-Equal $phase7nGate.finalDecision "PASS_FINAL_LMAX_READONLY_RUNTIME_EVIDENCE_ARCHIVE_CLOSED" "Phase 7N decision"

Assert-True $r9Abort.r8DecisionConfirmed "R8 abort decision confirmed"
Assert-Equal $r9Abort.r8Decision "LMAX_R8_FAIL_PREFLIGHT_ABORTED" "R8 abort analysis decision"
Assert-True $r9Abort.r8AbortWasSafetySuccess "R8 abort safety success"
Assert-False $r9Abort.r8AbortWasLmaxConnectivityFailure "R8 abort connectivity failure"
foreach ($property in @("r8ConnectionAttempted", "r8TcpAttempted", "r8TlsAttempted", "r8FixLogonAttempted", "r8MarketDataRequestSent")) {
    Assert-False $r9Abort.$property "R8 abort analysis $property"
}

Assert-True $r9Summary.adapterConsumesR7HarnessOutput "Summary consumes R7 harness"
Assert-True $r9Summary.requestConstructedFromHarnessResult "Summary request constructed from harness"
Assert-Equal $r9Summary.dryRunImplementationAdded "LmaxDryRunTemporaryReadOnlyRuntimeActivationAdapter" "Dry-run implementation"
Assert-Equal $r9Summary.realAdapterSkeletonAdded "LmaxRealTemporaryReadOnlyRuntimeActivationAdapterSkeleton" "Skeleton implementation"
Assert-False $r9Summary.realAdapterSkeletonCanExecute "Skeleton can execute"
Assert-False $r9Summary.apiWorkerWiringAdded "Summary API/Worker wiring"
Assert-False $r9Summary.defaultConfigChanged "Summary default config"
Assert-False $r9Summary.liveConnectionScriptCreated "Summary live connection script"
Assert-False $r9Summary.credentialLoadingAdded "Summary credential loading"
Assert-False $r9Summary.hostedServiceAdded "Summary hosted service"
Assert-False $r9Summary.backgroundWorkerAdded "Summary background worker"
Assert-False $r9Summary.runtimeActivationExecuted "Summary runtime activation"
Assert-Equal $r9Summary.finalDecision "LMAX_R9_TEMPORARY_ADAPTER_PATH_IMPLEMENTED_DRY_RUN_ONLY_NO_ACTIVATION" "Summary final decision"

Assert-Equal $r9Contract.requestType "LmaxTemporaryReadOnlyRuntimeActivationRequest" "Request type"
Assert-Equal $r9Contract.mustBeConstructedFrom "LmaxReadOnlyRuntimeActivationGateHarnessResult" "Request source"
Assert-False $r9Contract.freeFormInstrumentInputAllowed "Free-form instrument input"
Assert-True $r9Contract.requiresR7HarnessPreflightPassed "Requires R7 harness"
Assert-True $r9Contract.requiresApprovedInstrumentsOnly "Requires approved instruments"
Assert-True $r9Contract.requiresUsdJpyCaveatPreserved "Requires USDJPY caveat"
Assert-Equal $r9Contract.requestedNextApprovalPhase "LMAX-R10" "Next approval phase"
Assert-False $r9Contract.r10AuthorizedByR9 "R10 authorized by R9"

Assert-Equal $r9DryRun.adapter "LmaxDryRunTemporaryReadOnlyRuntimeActivationAdapter" "Dry-run adapter"
Assert-Equal $r9DryRun.mode "DryRunOnly" "Dry-run mode"
Assert-True $r9DryRun.consumesR7HarnessOutput "Dry-run consumes harness"
Assert-True $r9DryRun.rerunsSafetyValidation "Dry-run reruns safety"
Assert-True $r9DryRun.validatesApprovedInstruments "Dry-run validates instruments"
Assert-True $r9DryRun.validatesUsdJpyCaveat "Dry-run validates USDJPY caveat"
Assert-True $r9DryRun.emitsSanitizedResult "Dry-run sanitized result"
foreach ($property in @("externalRunExecuted", "realSocketOpened", "tcpConnectionAttempted", "tlsHandshakeAttempted", "fixLogonAttempted", "marketDataRequestSent", "credentialsLoaded", "apiWorkerStarted", "runtimePoweredUp")) {
    Assert-False $r9DryRun.$property "Dry-run $property"
}
Assert-Equal $r9DryRun.resultForValidHarnessOutput "DryRunAccepted" "Dry-run valid result"
Assert-True $r9DryRun.futureR10ApprovalRequired "Dry-run future R10 required"

foreach ($property in @("networkTypesReferenced", "credentialResolversReferenced", "apiWorkerRegistrationAdded", "hostedServiceAdded", "backgroundWorkerAdded", "liveConnectionScriptCreated", "defaultGatewayRegistrationChanged", "defaultConfigChangedToEnableConnectivity", "externalRunExecuted", "runtimeActivationExecuted")) {
    Assert-False $r9Proof.$property "Proof $property"
}
Assert-Equal $r9Proof.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Proof gateway mode"

foreach ($coverage in @(
    "dry-run adapter accepts valid R7 harness output",
    "dry-run adapter preserves approved instruments",
    "dry-run adapter preserves USDJPY caveat",
    "dry-run adapter returns sanitized dry-run result",
    "dry-run adapter rejects missing R7 harness validation",
    "dry-run adapter rejects non-approved instrument",
    "dry-run adapter rejects USDJPY without caveat",
    "dry-run adapter rejects production account",
    "dry-run adapter rejects orders enabled",
    "dry-run adapter rejects live trading enabled",
    "dry-run adapter rejects scheduler enabled",
    "dry-run adapter rejects polling enabled",
    "dry-run adapter rejects replay enabled",
    "dry-run adapter rejects shadow replay enabled",
    "dry-run adapter rejects trading mutation enabled",
    "dry-run adapter rejects persistent runtime enablement",
    "dry-run adapter rejects non-dry-run adapter mode",
    "real adapter skeleton cannot execute",
    "adapter path has no credential or network dependency",
    "no API/Worker/default config wiring was added"
)) {
    if (-not ($r9Tests.coverage -contains $coverage)) {
        throw "R9 test coverage missing: $coverage"
    }
}

Assert-Equal $r9Gate.finalDecision "LMAX_R9_TEMPORARY_ADAPTER_PATH_IMPLEMENTED_DRY_RUN_ONLY_NO_ACTIVATION" "R9 final decision"
Assert-True $r9Gate.temporaryAdapterPathImplemented "Gate adapter path implemented"
Assert-True $r9Gate.adapterConsumesR7HarnessOutput "Gate consumes harness"
Assert-True $r9Gate.dryRunAdapterImplemented "Gate dry-run adapter"
Assert-True $r9Gate.realAdapterSkeletonImplemented "Gate skeleton"
Assert-False $r9Gate.realAdapterSkeletonCanExecute "Gate skeleton can execute"
Assert-True $r9Gate.dryRunOnly "Gate dry-run only"
Assert-False $r9Gate.r10Authorized "Gate R10 authorized"
Assert-Equal $r9Gate.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Gate API/Worker mode"
Assert-False $r9Gate.evidenceArchivesModified "Gate archives modified"
Assert-True $r9Gate.usdJpyT7ClosureIntact "Gate USDJPY T7"
Assert-True $r9Gate.validatedRailsArchivesIntact "Gate validated rails"

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
    "r10Authorized"
)) {
    Assert-False $r9NonRun.$property "Non-run $property"
    Assert-False $r9Gate.$property "Gate $property"
}
Assert-Equal $r9NonRun.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Non-run gateway mode"

$adapterPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxTemporaryReadOnlyRuntimeAdapterPath.cs"
$testPath = Join-Path $RepoRoot "tests\QQ.Production.Intraday.Tests.Unit\LmaxTemporaryReadOnlyRuntimeAdapterPathTests.cs"
if (-not (Test-Path -LiteralPath $adapterPath)) {
    throw "Missing R9 adapter code file: $adapterPath"
}
if (-not (Test-Path -LiteralPath $testPath)) {
    throw "Missing R9 test code file: $testPath"
}
$adapterSource = Get-Content -LiteralPath $adapterPath -Raw
foreach ($pattern in @("TcpClient", "System.Net.Sockets", "new Socket", "SslStream", "NetworkStream", "ConnectAsync", "QuickFix", "CredentialProfileResolver", "SessionPassword")) {
    if ($adapterSource -match [regex]::Escape($pattern)) {
        throw "R9 adapter source references forbidden network/credential token: $pattern"
    }
}
foreach ($requiredToken in @("ILmaxTemporaryReadOnlyRuntimeActivationAdapter", "LmaxTemporaryReadOnlyRuntimeActivationRequest", "LmaxDryRunTemporaryReadOnlyRuntimeActivationAdapter", "LmaxRealTemporaryReadOnlyRuntimeActivationAdapterSkeleton", "FromHarnessResult")) {
    if ($adapterSource -notmatch [regex]::Escape($requiredToken)) {
        throw "R9 adapter source missing required token: $requiredToken"
    }
}

$programPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\Program.cs"
$workerPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Worker\Program.cs"
$appsettingsPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\appsettings.json"
$programText = Get-Content -LiteralPath $programPath -Raw
$workerText = Get-Content -LiteralPath $workerPath -Raw
$appsettings = Read-Json $appsettingsPath
if ($programText -notmatch 'AddSingleton<IVenueExecutionGateway,\s*FakeLmaxGateway>') {
    throw "API Program.cs no longer registers FakeLmaxGateway as IVenueExecutionGateway."
}
if ($workerText -notmatch 'AddSingleton<IVenueExecutionGateway,\s*FakeLmaxGateway>') {
    throw "Worker Program.cs no longer registers FakeLmaxGateway as IVenueExecutionGateway."
}
if ($programText -match 'LmaxDryRunTemporaryReadOnlyRuntimeActivationAdapter|LmaxRealTemporaryReadOnlyRuntimeActivationAdapterSkeleton|ILmaxTemporaryReadOnlyRuntimeActivationAdapter') {
    throw "API Program.cs contains R9 adapter wiring."
}
if ($workerText -match 'LmaxDryRunTemporaryReadOnlyRuntimeActivationAdapter|LmaxRealTemporaryReadOnlyRuntimeActivationAdapterSkeleton|ILmaxTemporaryReadOnlyRuntimeActivationAdapter') {
    throw "Worker Program.cs contains R9 adapter wiring."
}
Assert-False $appsettings.Safety.AllowExternalConnections "Appsettings Safety.AllowExternalConnections"
Assert-False $appsettings.Safety.AllowLiveTrading "Appsettings Safety.AllowLiveTrading"
Assert-True $appsettings.Safety.RequireFakeExecutionGateway "Appsettings Safety.RequireFakeExecutionGateway"
Assert-False $appsettings.LmaxReadOnlyRuntime.Enabled "Appsettings runtime Enabled"
Assert-Equal $appsettings.LmaxReadOnlyRuntime.ImplementationMode "DesignOnly" "Appsettings runtime ImplementationMode"
Assert-False $appsettings.LmaxReadOnlyRuntime.AllowExternalConnections "Appsettings runtime AllowExternalConnections"
Assert-False $appsettings.LmaxReadOnlyRuntime.AllowCredentialUse "Appsettings runtime AllowCredentialUse"
Assert-False $appsettings.LmaxReadOnlyRuntime.AllowOrderSubmission "Appsettings runtime AllowOrderSubmission"
Assert-False $appsettings.LmaxReadOnlyRuntime.SubmitToShadowReplay "Appsettings runtime SubmitToShadowReplay"
Assert-False $appsettings.LmaxReadOnlyRuntime.SchedulerEnabled "Appsettings runtime SchedulerEnabled"

$reportPath = Join-Path $readiness "phase-lmax-r9-temporary-adapter-path-report.md"
$reportText = Get-Content -LiteralPath $reportPath -Raw
foreach ($heading in @(
    "Executive summary",
    "R8 abort analysis",
    "Scope and non-run guarantees",
    "Temporary adapter path contracts",
    "Harness-backed request model",
    "Dry-run adapter behavior",
    "Safety/no-network proof",
    "Tests added/updated",
    "What R9 now provides",
    "What remains forbidden",
    "Decision",
    "Recommended next phase"
)) {
    if ($reportText -notmatch [regex]::Escape($heading)) {
        throw "R9 report missing required section: $heading"
    }
}

$validation = [ordered]@{
    phase = "LMAX-R9"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    validator = "scripts/check-lmax-r9-temporary-adapter-path-gate.ps1"
    requiredArtifactsPresent = $true
    priorArtifactsPresent = $true
    r8DecisionConfirmed = $true
    temporaryAdapterPathImplemented = $true
    adapterConsumesR7HarnessOutput = $true
    dryRunOnly = $true
    r10Authorized = $false
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
    recommendedNextPhase = $r9Gate.recommendedNextPhase
    finalDecision = $r9Gate.finalDecision
}

$validationPath = Join-Path $readiness "phase-lmax-r9-gate-validation.json"
$validation | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $validationPath -Encoding UTF8

Write-Host "PASS: $($r9Gate.finalDecision)"
