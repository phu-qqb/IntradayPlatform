param(
    [string]$Root = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

$artifactRoot = Join-Path $Root "artifacts/readiness/lmax-runtime-enablement"
$gatePath = Join-Path $artifactRoot "phase-lmax-r38-gate-validation.json"

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
    "phase-lmax-r38-r37-blocker-analysis.json",
    "phase-lmax-r38-real-core-composition-summary.json",
    "phase-lmax-r38-operation-to-core-mapping-summary.json",
    "phase-lmax-r38-bounded-executor-composition-summary.json",
    "phase-lmax-r38-final-composition-completeness-check.json",
    "phase-lmax-r38-readonly-composition-interface-safety-proof.json",
    "phase-lmax-r38-test-coverage-summary.json",
    "phase-lmax-r38-no-external-activation-proof.json",
    "phase-lmax-r38-decision-gate.json",
    "phase-lmax-r38-non-run-validation.json",
    "phase-lmax-r38-concrete-final-pre-execution-blocker-fix-report.md",
    "phase-lmax-r38-operator-note.md"
)

$checks = [ordered]@{}
foreach ($file in $requiredArtifacts) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactRoot $file)) "Missing R38 artifact: $file"
}
$checks.requiredR38ArtifactsExist = $true

$missingPrior = @()
for ($phase = 1; $phase -le 37; $phase++) {
    $matches = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r$phase-*" -File -ErrorAction SilentlyContinue
    if ($matches.Count -eq 0) {
        $missingPrior += "R$phase"
    }
}
Assert-True ($missingPrior.Count -eq 0) ("Missing prior LMAX runtime artifacts: " + ($missingPrior -join ", "))
$checks.r1ThroughR37ArtifactsStillExist = $true

$r37Decision = Read-Json (Join-Path $artifactRoot "phase-lmax-r37-decision-gate.json")
$r37DecisionValue = if ($r37Decision.finalDecision) { $r37Decision.finalDecision } elseif ($r37Decision.resultClassification) { $r37Decision.resultClassification } else { $r37Decision.decision }
Assert-True ($r37DecisionValue -eq "LMAX_R37_FAIL_OPERATION_BINDINGS_NOT_COMPOSABLE") "R37 decision mismatch."
$checks.r37DecisionRemainsExpected = $true

$decision = Read-Json (Join-Path $artifactRoot "phase-lmax-r38-decision-gate.json")
$decisionValue = if ($decision.finalDecision) { $decision.finalDecision } elseif ($decision.resultClassification) { $decision.resultClassification } else { $decision.decision }
Assert-True ($decisionValue -eq "LMAX_R38_REAL_CORE_COMPOSITION_IMPLEMENTED_NO_EXTERNAL_ACTIVATION") "R38 decision mismatch."
Assert-False $decision.externalActivationExecuted "R38 decision shows external activation."
Assert-False $decision.forbiddenActionOccurred "R38 decision shows forbidden action."
Assert-False $decision.r39Authorized "R38 decision authorized R39."
$checks.r38DecisionExpected = $true

$compositionCode = Join-Path $Root "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyExecutionComposition.cs"
Assert-True (Test-Path -LiteralPath $compositionCode) "R38 composition code missing."
$compositionText = Get-Content -LiteralPath $compositionCode -Raw
foreach ($token in @(
    "LmaxReadOnlyExecutionCompositionRoot",
    "LmaxReadOnlyExecutionCoreBindingSet",
    "LmaxReadOnlyExecutionCompositionResult",
    "LmaxReadOnlyExecutionCompositionValidator",
    "LmaxReadOnlySocketConnectOperationBinding",
    "LmaxReadOnlyTlsHandshakeOperationBinding",
    "LmaxReadOnlyFixSessionOperationBinding",
    "LmaxReadOnlyMarketDataOperationBinding",
    "LmaxReadOnlyCredentialConfigOperationBinding"
)) {
    Assert-True ($compositionText -match [regex]::Escape($token)) "R38 composition code missing token: $token"
}
$checks.realCoreCompositionCodeExists = $true
$checks.operationToCoreMappingExists = $true
$checks.boundedExecutorCompositionExists = $true

$complete = Read-Json (Join-Path $artifactRoot "phase-lmax-r38-final-composition-completeness-check.json")
Assert-True ($complete.passed -eq $true) "R38 final composition completeness did not pass."
Assert-True ($complete.providerCompletenessPassed -eq $true) "R38 provider completeness failed."
Assert-True ($complete.clientCompletenessPassed -eq $true) "R38 client completeness failed."
Assert-True ($complete.operationCompletenessPassed -eq $true) "R38 operation completeness failed."
Assert-True ($complete.realCoreCompositionPassed -eq $true) "R38 real core composition failed."
Assert-True ($complete.boundedExecutorCompositionPassed -eq $true) "R38 bounded executor composition failed."
Assert-True ($complete.operationToCoreMappingExists -eq $true) "R38 operation-to-core mapping missing."
Assert-True ($complete.noFakeOrTestOnlyCore -eq $true) "R38 final composition includes fake/test core."
$checks.finalCompositionCompletenessPassed = $true

$safety = Read-Json (Join-Path $artifactRoot "phase-lmax-r38-readonly-composition-interface-safety-proof.json")
Assert-True ($safety.passed -eq $true) "R38 read-only composition safety proof failed."
Assert-False $safety.orderTradingReplayPublicMethodsExposed "R38 exposed order/trading/replay composition methods."
Assert-False $safety.schedulerPollingMethodsExposed "R38 exposed scheduler/polling methods."
Assert-False $safety.liveLauncherMethodsExposed "R38 exposed live launcher methods."
Assert-False $safety.hostedBackgroundServiceMethodsExposed "R38 exposed hosted/background methods."
Assert-False $safety.credentialValuesReturned "R38 returned credential values."
Assert-False $safety.credentialValuesPrinted "R38 printed credential values."
Assert-False $safety.credentialValuesStored "R38 stored credential values."
Assert-False $safety.rawFixStored "R38 stored raw FIX."
$checks.readOnlyCompositionInterfaceSafetyProofExists = $true

$nonRun = Read-Json (Join-Path $artifactRoot "phase-lmax-r38-non-run-validation.json")
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
    "r39Authorized"
)) {
    Assert-False $nonRun.$field "R38 non-run flag was true: $field"
}
Assert-True ($nonRun.apiWorkerGatewayMode -eq "FakeLmaxGatewayOnly") "R38 API/Worker gateway mode mismatch."
Assert-True ($nonRun.passed -eq $true) "R38 non-run validation failed."
$checks.noExternalActionOccurred = $true

$apiProgram = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/Program.cs") -Raw
$workerProgramPath = Join-Path $Root "src/QQ.Production.Intraday.Worker/Program.cs"
$workerProgram = if (Test-Path -LiteralPath $workerProgramPath) { Get-Content -LiteralPath $workerProgramPath -Raw } else { "" }
$appsettings = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/appsettings.json") -Raw
Assert-True ($apiProgram -match "AddSingleton<IVenueExecutionGateway,\s*FakeLmaxGateway>") "API default gateway is not FakeLmaxGateway."
foreach ($token in @("LmaxReadOnlyExecutionCompositionRoot", "LmaxReadOnlyExecutionCoreBindingSet", "phase-lmax-r38")) {
    Assert-True ($apiProgram -notmatch [regex]::Escape($token)) "R38 API wiring detected: $token"
    Assert-True ($workerProgram -notmatch [regex]::Escape($token)) "R38 Worker wiring detected: $token"
    Assert-True ($appsettings -notmatch [regex]::Escape($token)) "R38 appsettings wiring detected: $token"
}
Assert-True ($appsettings -match '"RequireFakeExecutionGateway"\s*:\s*true') "API appsettings no longer require fake execution gateway."
Assert-True ($appsettings -notmatch '"Enabled"\s*:\s*true') "API appsettings enabled runtime connectivity."
$checks.apiWorkerRemainFakeLmaxGatewayOnly = $true
$checks.noApiWorkerWiringAdded = $true
$checks.noDefaultConfigChangedToEnableLmax = $true

$unexpectedScripts = Get-ChildItem -LiteralPath (Join-Path $Root "scripts") -Filter "*lmax*r38*" -File |
    Where-Object { $_.Name -ne "check-lmax-r38-concrete-final-pre-execution-blocker-fix-gate.ps1" }
Assert-True ($unexpectedScripts.Count -eq 0) "Unexpected R38 live/script file present: $($unexpectedScripts.Name -join ', ')"
$checks.noLiveConnectionScriptCreated = $true

$gate = [ordered]@{
    phase = "LMAX-R38"
    validatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    validator = "scripts/check-lmax-r38-concrete-final-pre-execution-blocker-fix-gate.ps1"
    passed = $true
    decision = "LMAX_R38_REAL_CORE_COMPOSITION_IMPLEMENTED_NO_EXTERNAL_ACTIVATION"
    checks = $checks
    nonRunFlags = $nonRun
}

$gate | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $gatePath -Encoding UTF8
Write-Host "LMAX R38 gate validation passed: $gatePath"
