param(
    [string]$Root = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

$artifactRoot = Join-Path $Root "artifacts/readiness/lmax-runtime-enablement"
$gatePath = Join-Path $artifactRoot "phase-lmax-r39-gate-validation.json"
$approvalPhrase = "I, Philippe, explicitly approve Phase LMAX-R39 for one temporary Demo read-only runtime market-data activation attempt after the final composition fix for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority."

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

function Assert-NotTrue($Value, [string]$Message) {
    if ($Value -eq $true) {
        throw $Message
    }
}

$requiredArtifacts = @(
    "phase-lmax-r39-operator-approval-record.json",
    "phase-lmax-r39-preflight-gate.json",
    "phase-lmax-r39-final-bounded-executor-provider-client-operation-composition-validation.json",
    "phase-lmax-r39-temporary-runtime-activation-record.json",
    "phase-lmax-r39-approved-instrument-status-record.json",
    "phase-lmax-r39-sanitized-runtime-boundary-evidence.json",
    "phase-lmax-r39-forbidden-action-validation.json",
    "phase-lmax-r39-shutdown-revert-record.json",
    "phase-lmax-r39-post-attempt-non-mutation-validation.json",
    "phase-lmax-r39-rail-isolation-validation.json",
    "phase-lmax-r39-decision-gate.json",
    "phase-lmax-r39-temporary-readonly-runtime-report.md",
    "phase-lmax-r39-operator-note.md"
)

$checks = [ordered]@{}
foreach ($file in $requiredArtifacts) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactRoot $file)) "Missing R39 artifact: $file"
}
$checks.requiredR39ArtifactsExist = $true

$missingPrior = @()
for ($phase = 1; $phase -le 38; $phase++) {
    $matches = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r$phase-*" -File -ErrorAction SilentlyContinue
    if ($matches.Count -eq 0) {
        $missingPrior += "R$phase"
    }
}
Assert-True ($missingPrior.Count -eq 0) ("Missing prior LMAX runtime artifacts: " + ($missingPrior -join ", "))
$checks.r1ThroughR38ArtifactsStillExist = $true

$r38Decision = Read-Json (Join-Path $artifactRoot "phase-lmax-r38-decision-gate.json")
$r38DecisionValue = if ($r38Decision.finalDecision) { $r38Decision.finalDecision } elseif ($r38Decision.resultClassification) { $r38Decision.resultClassification } else { $r38Decision.decision }
Assert-True ($r38DecisionValue -eq "LMAX_R38_REAL_CORE_COMPOSITION_IMPLEMENTED_NO_EXTERNAL_ACTIVATION") "R38 decision mismatch."
$checks.r38DecisionRemainsExpected = $true

$approval = Read-Json (Join-Path $artifactRoot "phase-lmax-r39-operator-approval-record.json")
Assert-True ($approval.approvalPhrase -eq $approvalPhrase) "R39 approval phrase mismatch."
Assert-True ($approval.operator -eq "Philippe") "R39 operator mismatch."
Assert-True ($approval.phase -eq "LMAX-R39") "R39 phase mismatch in approval record."
$checks.operatorApprovalExact = $true

$preflight = Read-Json (Join-Path $artifactRoot "phase-lmax-r39-preflight-gate.json")
Assert-True ($preflight.passed -eq $true) "R39 preflight did not pass."
Assert-True ($preflight.r38Decision -eq "LMAX_R38_REAL_CORE_COMPOSITION_IMPLEMENTED_NO_EXTERNAL_ACTIVATION") "R39 preflight did not validate R38 decision."
$checks.preflightPassedBeforeActivation = $true

$finalValidation = Read-Json (Join-Path $artifactRoot "phase-lmax-r39-final-bounded-executor-provider-client-operation-composition-validation.json")
Assert-True ($finalValidation.validationPassed -eq $false) "R39 final validation unexpectedly passed."
Assert-True ($finalValidation.resultClassification -eq "LMAX_R39_FAIL_CORE_COMPOSITION_REGRESSION") "R39 final validation classification mismatch."
Assert-True ($finalValidation.concreteFinalAbortCause -eq "CoreCompositionRegressionExecutableRuntimeCoreDelegatesNotBound") "R39 concrete abort cause mismatch."
Assert-True ($finalValidation.providerCompletenessRemainsPass -eq $true) "R39 provider completeness regression."
Assert-True ($finalValidation.clientCompletenessRemainsPass -eq $true) "R39 client completeness regression."
Assert-True ($finalValidation.operationCompletenessRemainsPass -eq $true) "R39 operation completeness regression."
Assert-True ($finalValidation.coreCompositionRemainsPassByR38Artifact -eq $true) "R39 did not preserve R38 composition artifact pass."
Assert-False $finalValidation.operationBindingsComposableIntoBoundedExecutorPath "R39 unexpectedly found executable composition."
Assert-False $finalValidation.concreteRealCoresConfiguredBehindOperationBindings "R39 unexpectedly found concrete real cores configured."
Assert-False $finalValidation.executableRuntimeCoreDelegatesBound "R39 unexpectedly found executable runtime core delegates bound."
Assert-False $finalValidation.olderPrototypeWrapperUsed "R39 used older prototype wrapper."
$checks.finalBoundedExecutorProviderClientOperationCompositionValidationExplicit = $true

$allowedDecisions = @(
    "LMAX_R39_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED",
    "LMAX_R39_FAIL_PREFLIGHT_ABORTED",
    "LMAX_R39_FAIL_BOUNDED_EXECUTOR_VALIDATION_FAILED",
    "LMAX_R39_FAIL_PROVIDER_COMPLETENESS_REGRESSION",
    "LMAX_R39_FAIL_CLIENT_COMPLETENESS_REGRESSION",
    "LMAX_R39_FAIL_OPERATION_COMPLETENESS_REGRESSION",
    "LMAX_R39_FAIL_CORE_COMPOSITION_REGRESSION",
    "LMAX_R39_FAIL_OPERATION_BINDINGS_NOT_COMPOSABLE",
    "LMAX_R39_FAIL_CREDENTIAL_CONFIG_MISSING",
    "LMAX_R39_FAIL_CREDENTIAL_CONFIG_NOT_APPROVED",
    "LMAX_R39_FAIL_REQUIRES_API_WORKER_STARTUP",
    "LMAX_R39_FAIL_REQUIRES_LIVE_LAUNCHER",
    "LMAX_R39_FAIL_TCP_RUNTIME_BOUNDARY",
    "LMAX_R39_FAIL_TLS_RUNTIME_BOUNDARY",
    "LMAX_R39_FAIL_FIX_LOGON_RUNTIME_BOUNDARY",
    "LMAX_R39_FAIL_MARKETDATA_RUNTIME_BOUNDARY",
    "LMAX_R39_FAIL_SESSION_OR_MARKETDATA_BOUNDARY",
    "LMAX_R39_FAIL_SAFETY_CONSTRAINT",
    "LMAX_R39_INCONCLUSIVE_SANITIZED_EVIDENCE"
)

$decision = Read-Json (Join-Path $artifactRoot "phase-lmax-r39-decision-gate.json")
$r39DecisionValue = if ($decision.finalDecision) { $decision.finalDecision } elseif ($decision.resultClassification) { $decision.resultClassification } else { $decision.decision }
Assert-True ($allowedDecisions -contains $r39DecisionValue) "R39 decision is not allowed."
Assert-True ($r39DecisionValue -eq "LMAX_R39_FAIL_CORE_COMPOSITION_REGRESSION") "R39 decision mismatch."
Assert-True ($decision.concreteFinalAbortCause -eq "CoreCompositionRegressionExecutableRuntimeCoreDelegatesNotBound") "R39 decision abort cause mismatch."
Assert-False $decision.temporaryActivationAttemptExecuted "R39 decision says activation executed."
Assert-False $decision.forbiddenActionOccurred "R39 decision says forbidden action occurred."
$checks.r39DecisionAllowedAndExpected = $true

$activation = Read-Json (Join-Path $artifactRoot "phase-lmax-r39-temporary-runtime-activation-record.json")
Assert-True ($activation.attemptCount -le 1) "R39 attempt count exceeded one."
Assert-True ($activation.attemptCount -eq 0) "R39 activation attempt should not have executed."
Assert-False $activation.activationAttemptExecuted "R39 activation unexpectedly executed."
Assert-False $activation.externalRunExecuted "R39 external run unexpectedly executed."
Assert-False $activation.runtimePoweredUp "R39 runtime unexpectedly powered up."
Assert-False $activation.runtimeEnablementExecuted "R39 runtime enablement unexpectedly executed."
Assert-False $activation.runtimeEnablementPersisted "R39 runtime enablement unexpectedly persisted."
$checks.atMostOneActivationAttemptOccurred = $true
$checks.activationDidNotExecute = $true

$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r39-forbidden-action-validation.json")
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
    "apiWorkerStarted",
    "approvedStackBypassed"
)) {
    Assert-False $forbidden.$field "Forbidden action flag was true: $field"
}
Assert-True ($forbidden.passed -eq $true) "R39 forbidden action validation failed."
$checks.noForbiddenActionOccurred = $true

$instruments = Read-Json (Join-Path $artifactRoot "phase-lmax-r39-approved-instrument-status-record.json")
Assert-True ($instruments.approvedInstrumentsOnly -eq $true) "R39 approvedInstrumentsOnly false."
Assert-False $instruments.nonApprovedInstrumentTouched "R39 touched non-approved instrument."
Assert-True ($instruments.usdJpyCaveatPreserved -eq $true) "R39 USDJPY caveat not preserved."
$checks.onlyApprovedInstrumentsTouched = $true
$checks.usdJpyCaveatPreserved = $true

$boundary = Read-Json (Join-Path $artifactRoot "phase-lmax-r39-sanitized-runtime-boundary-evidence.json")
Assert-True ($boundary.outputSanitized -eq $true) "R39 boundary output not sanitized."
Assert-False $boundary.credentialValuesPrinted "R39 printed credential values."
Assert-False $boundary.credentialValuesReturned "R39 returned credential values."
Assert-False $boundary.credentialValuesStored "R39 stored credential values."
Assert-True ($boundary.tcpBoundaryStatus -eq "NotAttempted") "R39 TCP boundary was attempted."
Assert-True ($boundary.tlsBoundaryStatus -eq "NotAttempted") "R39 TLS boundary was attempted."
Assert-True ($boundary.fixLogonSessionBoundaryStatus -eq "NotAttempted") "R39 FIX boundary was attempted."
Assert-True ($boundary.marketDataRequestBoundaryStatus -eq "NotAttempted") "R39 MarketData boundary was attempted."
Assert-False $boundary.providerClientsUsed "R39 used provider clients at runtime."
Assert-False $boundary.operationBindingsUsed "R39 used operation bindings at runtime."
Assert-False $boundary.coreCompositionUsed "R39 used core composition at runtime."
$checks.outputSanitized = $true

$shutdown = Read-Json (Join-Path $artifactRoot "phase-lmax-r39-shutdown-revert-record.json")
Assert-False $shutdown.activationStarted "R39 activation unexpectedly started."
Assert-True ($shutdown.shutdownOrRevertCompleted -eq $true) "R39 shutdown/revert incomplete."
Assert-True ($shutdown.apiWorkerGatewayMode -eq "FakeLmaxGatewayOnly") "R39 gateway mode mismatch in shutdown record."
$checks.shutdownRevertCompleted = $true

$nonMutation = Read-Json (Join-Path $artifactRoot "phase-lmax-r39-post-attempt-non-mutation-validation.json")
Assert-True ($nonMutation.passed -eq $true) "R39 non-mutation validation failed."
foreach ($field in @(
    "productionAccountUsed",
    "orderSubmissionExecuted",
    "orderPathEnabled",
    "orderGatewayRegistered",
    "tradingGatewayRegistered",
    "AllowOrderSubmission",
    "AllowLiveTrading",
    "IsTradingEnabled",
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
    Assert-False $nonMutation.$field "R39 non-mutation flag was true: $field"
}
$checks.noPersistentRuntimeEnablementOccurred = $true

$rail = Read-Json (Join-Path $artifactRoot "phase-lmax-r39-rail-isolation-validation.json")
Assert-True ($rail.passed -eq $true) "R39 rail isolation failed."
Assert-False $rail.validatedEvidenceArchivesModified "R39 modified validated evidence archives."
Assert-False $rail.phase7AThrough7NArchivesModified "R39 modified Phase 7A-7N archives."
Assert-False $rail.usdJpyT1ThroughT7ArtifactsModified "R39 modified USDJPY T1-T7 artifacts."
Assert-False $rail.r1ThroughR38ArtifactsModifiedExceptReadOnlyReference "R39 modified R1-R38 artifacts."
$checks.archivesWereNotModified = $true

$apiProgram = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/Program.cs") -Raw
$workerProgramPath = Join-Path $Root "src/QQ.Production.Intraday.Worker/Program.cs"
$workerProgram = if (Test-Path -LiteralPath $workerProgramPath) { Get-Content -LiteralPath $workerProgramPath -Raw } else { "" }
$appsettings = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/appsettings.json") -Raw
Assert-True ($apiProgram -match "AddSingleton<IVenueExecutionGateway,\s*FakeLmaxGateway>") "API default gateway is not FakeLmaxGateway."
foreach ($token in @("phase-lmax-r39", "LmaxReadOnlyExecutionCompositionRoot", "LmaxReadOnlyExecutionCoreBindingSet")) {
    Assert-True ($apiProgram -notmatch [regex]::Escape($token)) "R39 API wiring detected: $token"
    Assert-True ($workerProgram -notmatch [regex]::Escape($token)) "R39 Worker wiring detected: $token"
    Assert-True ($appsettings -notmatch [regex]::Escape($token)) "R39 appsettings wiring detected: $token"
}
Assert-True ($appsettings -match '"RequireFakeExecutionGateway"\s*:\s*true') "API appsettings no longer require fake execution gateway."
Assert-True ($appsettings -notmatch '"Enabled"\s*:\s*true') "API appsettings enabled runtime connectivity."
$checks.apiWorkerGatewayModeRemainsFakeLmaxGatewayOnly = $true
$checks.noApiWorkerWiringAdded = $true
$checks.noDefaultConfigChangedToEnableLmax = $true

$unexpectedScripts = Get-ChildItem -LiteralPath (Join-Path $Root "scripts") -Filter "*lmax*r39*" -File |
    Where-Object { $_.Name -ne "check-lmax-r39-temporary-readonly-runtime-gate.ps1" }
Assert-True ($unexpectedScripts.Count -eq 0) "Unexpected R39 live/script file present: $($unexpectedScripts.Name -join ', ')"
$checks.noLiveConnectionScriptCreated = $true

$gate = [ordered]@{
    phase = "LMAX-R39"
    validatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    validator = "scripts/check-lmax-r39-temporary-readonly-runtime-gate.ps1"
    passed = $true
    decision = "LMAX_R39_FAIL_CORE_COMPOSITION_REGRESSION"
    concreteFinalAbortCause = "CoreCompositionRegressionExecutableRuntimeCoreDelegatesNotBound"
    checks = $checks
    nonRunFlags = [ordered]@{
        externalRunExecuted = $false
        realSnapshotExecuted = $false
        replayExecuted = $false
        postEndpointInvoked = $false
        realSocketOpened = $false
        realTcpConnectionAttempted = $false
        realTlsHandshakeAttempted = $false
        realFixLogonAttempted = $false
        realMarketDataRequestSent = $false
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
        runtimeEnablementPersisted = $false
        tradingEnablementExecuted = $false
        schedulerEnablementExecuted = $false
        orderPathEnablementExecuted = $false
        defaultGatewayRegistrationChanged = $false
        liveConnectionScriptCreated = $false
        hostedServiceAdded = $false
        backgroundWorkerAdded = $false
    }
    gatewaySafety = [ordered]@{
        apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
        boundedExecutorUsed = $true
        temporaryReadOnlyMarketDataAdapterUsed = $false
        concreteAdapterUsed = $false
        realTransportUsed = $false
        executableSessionClientUsed = $false
        lowLevelStackUsed = $false
        realLowLevelDependenciesUsed = $false
        realDependencyProvidersUsed = $false
        providerClientsUsed = $false
        operationBindingsUsed = $false
        coreCompositionUsed = $false
        socketProviderUsed = $false
        tlsProviderUsed = $false
        fixProviderUsed = $false
        marketDataProviderUsed = $false
        credentialConfigProviderUsed = $false
        orderGatewayRegistered = $false
        tradingGatewayRegistered = $false
    }
}

$gate | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $gatePath -Encoding UTF8
Write-Host "LMAX R39 gate validation passed: $gatePath"
