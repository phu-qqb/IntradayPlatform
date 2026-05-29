param(
    [string]$Root = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

$artifactRoot = Join-Path $Root "artifacts/readiness/lmax-runtime-enablement"
$gatePath = Join-Path $artifactRoot "phase-lmax-r40-gate-validation.json"

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Missing required file: $Path"
    }

    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw $Message
    }
}

function Assert-False($Value, [string]$Message) {
    if ($Value -ne $false) {
        throw $Message
    }
}

$requiredArtifacts = @(
    "phase-lmax-r40-r39-blocker-analysis.json",
    "phase-lmax-r40-runtime-core-delegate-bindings-summary.json",
    "phase-lmax-r40-operation-to-delegate-mapping-summary.json",
    "phase-lmax-r40-bounded-executor-delegate-composition-summary.json",
    "phase-lmax-r40-final-delegate-binding-completeness-check.json",
    "phase-lmax-r40-readonly-delegate-interface-safety-proof.json",
    "phase-lmax-r40-test-coverage-summary.json",
    "phase-lmax-r40-no-external-activation-proof.json",
    "phase-lmax-r40-decision-gate.json",
    "phase-lmax-r40-non-run-validation.json",
    "phase-lmax-r40-concrete-final-pre-execution-blocker-fix-report.md",
    "phase-lmax-r40-operator-note.md"
)

$checks = [ordered]@{}
foreach ($file in $requiredArtifacts) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactRoot $file)) "Missing R40 artifact: $file"
}
$checks.requiredR40ArtifactsExist = $true

$missingPrior = @()
for ($phase = 1; $phase -le 39; $phase++) {
    $matches = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r$phase-*" -File -ErrorAction SilentlyContinue
    if ($matches.Count -eq 0) {
        $missingPrior += "R$phase"
    }
}
Assert-True ($missingPrior.Count -eq 0) ("Missing prior LMAX runtime artifacts: " + ($missingPrior -join ", "))
$checks.r1ThroughR39ArtifactsStillExist = $true

$r39Decision = Read-Json (Join-Path $artifactRoot "phase-lmax-r39-decision-gate.json")
$r39DecisionValue = if ($r39Decision.finalDecision) { $r39Decision.finalDecision } elseif ($r39Decision.resultClassification) { $r39Decision.resultClassification } else { $r39Decision.decision }
Assert-True ($r39DecisionValue -eq "LMAX_R39_FAIL_CORE_COMPOSITION_REGRESSION") "R39 decision mismatch."
$checks.r39DecisionRemainsExpected = $true

$decision = Read-Json (Join-Path $artifactRoot "phase-lmax-r40-decision-gate.json")
$decisionValue = if ($decision.finalDecision) { $decision.finalDecision } elseif ($decision.resultClassification) { $decision.resultClassification } else { $decision.decision }
Assert-True ($decisionValue -eq "LMAX_R40_RUNTIME_CORE_DELEGATE_BINDINGS_IMPLEMENTED_NO_EXTERNAL_ACTIVATION") "R40 decision mismatch."
Assert-False $decision.externalActivationExecuted "R40 decision shows external activation."
Assert-False $decision.forbiddenActionOccurred "R40 decision shows forbidden action."
Assert-False $decision.r41Authorized "R40 decision authorized R41."
$checks.r40DecisionExpected = $true

$bindingCode = Join-Path $Root "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyRuntimeCoreDelegateBindings.cs"
Assert-True (Test-Path -LiteralPath $bindingCode) "R40 runtime core delegate binding code missing."
$bindingText = Get-Content -LiteralPath $bindingCode -Raw
foreach ($token in @(
    "LmaxReadOnlyRuntimeCoreDelegateBindingSet",
    "LmaxReadOnlyRuntimeCoreDelegateBindingFactory",
    "LmaxReadOnlyRuntimeCoreDelegateBindingValidator",
    "LmaxReadOnlyRuntimeCoreDelegateBindingResult",
    "SocketConnect",
    "TlsHandshake",
    "FixSession",
    "MarketData",
    "CredentialConfig",
    "LmaxReadOnlyExecutionCompositionRoot"
)) {
    Assert-True ($bindingText -match [regex]::Escape($token)) "R40 binding code missing token: $token"
}
$checks.runtimeCoreDelegateBindingCodeExists = $true
$checks.operationToDelegateMappingExists = $true
$checks.boundedExecutorDelegateCompositionExists = $true

$complete = Read-Json (Join-Path $artifactRoot "phase-lmax-r40-final-delegate-binding-completeness-check.json")
Assert-True ($complete.passed -eq $true) "R40 final delegate-binding completeness did not pass."
Assert-True ($complete.providerCompletenessPassed -eq $true) "R40 provider completeness failed."
Assert-True ($complete.clientCompletenessPassed -eq $true) "R40 client completeness failed."
Assert-True ($complete.operationCompletenessPassed -eq $true) "R40 operation completeness failed."
Assert-True ($complete.coreCompositionPassed -eq $true) "R40 core composition failed."
Assert-True ($complete.runtimeDelegateBindingPassed -eq $true) "R40 runtime delegate binding failed."
Assert-True ($complete.boundedExecutorCompositionPassed -eq $true) "R40 bounded executor composition failed."
Assert-True ($complete.operationToDelegateMappingExists -eq $true) "R40 operation-to-delegate mapping missing."
Assert-True ($complete.noFakeOrTestOnlyDelegate -eq $true) "R40 final binding includes fake/test delegate."
$checks.finalDelegateBindingCompletenessPassed = $true

$safety = Read-Json (Join-Path $artifactRoot "phase-lmax-r40-readonly-delegate-interface-safety-proof.json")
Assert-True ($safety.passed -eq $true) "R40 read-only delegate safety proof failed."
Assert-False $safety.orderTradingReplayPublicMethodsExposed "R40 exposed order/trading/replay delegate methods."
Assert-False $safety.schedulerPollingMethodsExposed "R40 exposed scheduler/polling methods."
Assert-False $safety.liveLauncherMethodsExposed "R40 exposed live launcher methods."
Assert-False $safety.hostedBackgroundServiceMethodsExposed "R40 exposed hosted/background methods."
Assert-False $safety.credentialValuesReturned "R40 returned credential values."
Assert-False $safety.credentialValuesPrinted "R40 printed credential values."
Assert-False $safety.credentialValuesStored "R40 stored credential values."
Assert-False $safety.rawFixStored "R40 stored raw FIX."
Assert-False $safety.oldPrototypeWrapperUsedDirectlyOutsideApprovedPath "R40 used old prototype/wrapper directly."
$checks.readOnlyDelegateInterfaceSafetyProofExists = $true

$nonRun = Read-Json (Join-Path $artifactRoot "phase-lmax-r40-non-run-validation.json")
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
    "r41Authorized"
)) {
    Assert-False $nonRun.$field "R40 non-run flag was true: $field"
}
Assert-True ($nonRun.apiWorkerGatewayMode -eq "FakeLmaxGatewayOnly") "R40 API/Worker gateway mode mismatch."
Assert-True ($nonRun.passed -eq $true) "R40 non-run validation failed."
$checks.noExternalActionOccurred = $true

$apiProgram = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/Program.cs") -Raw
$workerProgramPath = Join-Path $Root "src/QQ.Production.Intraday.Worker/Program.cs"
$workerProgram = if (Test-Path -LiteralPath $workerProgramPath) { Get-Content -LiteralPath $workerProgramPath -Raw } else { "" }
$appsettings = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/appsettings.json") -Raw
Assert-True ($apiProgram -match "AddSingleton<IVenueExecutionGateway,\s*FakeLmaxGateway>") "API default gateway is not FakeLmaxGateway."
foreach ($token in @("LmaxReadOnlyRuntimeCoreDelegateBindingFactory", "LmaxReadOnlyRuntimeCoreDelegateBindingSet", "phase-lmax-r40")) {
    Assert-True ($apiProgram -notmatch [regex]::Escape($token)) "R40 API wiring detected: $token"
    Assert-True ($workerProgram -notmatch [regex]::Escape($token)) "R40 Worker wiring detected: $token"
    Assert-True ($appsettings -notmatch [regex]::Escape($token)) "R40 appsettings wiring detected: $token"
}
Assert-True ($appsettings -match '"RequireFakeExecutionGateway"\s*:\s*true') "API appsettings no longer require fake execution gateway."
Assert-True ($appsettings -notmatch '"Enabled"\s*:\s*true') "API appsettings enabled runtime connectivity."
$checks.apiWorkerRemainFakeLmaxGatewayOnly = $true
$checks.noApiWorkerWiringAdded = $true
$checks.noDefaultConfigChangedToEnableLmax = $true

$unexpectedScripts = Get-ChildItem -LiteralPath (Join-Path $Root "scripts") -Filter "*lmax*r40*" -File |
    Where-Object { $_.Name -ne "check-lmax-r40-concrete-final-pre-execution-blocker-fix-gate.ps1" }
Assert-True ($unexpectedScripts.Count -eq 0) "Unexpected R40 live/script file present: $($unexpectedScripts.Name -join ', ')"
$checks.noLiveConnectionScriptCreated = $true

$gate = [ordered]@{
    phase = "LMAX-R40"
    validatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    validator = "scripts/check-lmax-r40-concrete-final-pre-execution-blocker-fix-gate.ps1"
    passed = $true
    decision = "LMAX_R40_RUNTIME_CORE_DELEGATE_BINDINGS_IMPLEMENTED_NO_EXTERNAL_ACTIVATION"
    checks = $checks
    nonRunFlags = $nonRun
}

$gate | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $gatePath -Encoding UTF8
Write-Host "LMAX R40 gate validation passed: $gatePath"
