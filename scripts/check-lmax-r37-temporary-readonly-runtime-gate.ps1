param(
    [string]$Root = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

$artifactRoot = Join-Path $Root "artifacts/readiness/lmax-runtime-enablement"
$gatePath = Join-Path $artifactRoot "phase-lmax-r37-gate-validation.json"
$approvalPhrase = "I, Philippe, explicitly approve Phase LMAX-R37 for one temporary Demo read-only runtime market-data activation attempt after the operation completion sweep for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority."

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
    "phase-lmax-r37-operator-approval-record.json",
    "phase-lmax-r37-preflight-gate.json",
    "phase-lmax-r37-final-bounded-executor-provider-client-operation-validation.json",
    "phase-lmax-r37-temporary-runtime-activation-record.json",
    "phase-lmax-r37-approved-instrument-status-record.json",
    "phase-lmax-r37-sanitized-runtime-boundary-evidence.json",
    "phase-lmax-r37-forbidden-action-validation.json",
    "phase-lmax-r37-shutdown-revert-record.json",
    "phase-lmax-r37-post-attempt-non-mutation-validation.json",
    "phase-lmax-r37-rail-isolation-validation.json",
    "phase-lmax-r37-decision-gate.json",
    "phase-lmax-r37-temporary-readonly-runtime-report.md",
    "phase-lmax-r37-operator-note.md"
)

$checks = [ordered]@{}
foreach ($file in $requiredArtifacts) {
    $path = Join-Path $artifactRoot $file
    Assert-True (Test-Path -LiteralPath $path) "Missing R37 artifact: $file"
}
$checks.requiredR37ArtifactsExist = $true

$priorPhaseMissing = @()
for ($phase = 1; $phase -le 36; $phase++) {
    $pattern = "phase-lmax-r$phase-*"
    $matches = Get-ChildItem -LiteralPath $artifactRoot -Filter $pattern -File -ErrorAction SilentlyContinue
    if ($matches.Count -eq 0) {
        $priorPhaseMissing += "R$phase"
    }
}
Assert-True ($priorPhaseMissing.Count -eq 0) ("Missing prior LMAX runtime artifacts: " + ($priorPhaseMissing -join ", "))
$checks.r1ThroughR36ArtifactsStillExist = $true

$r36Decision = Read-Json (Join-Path $artifactRoot "phase-lmax-r36-decision-gate.json")
Assert-True (
    $r36Decision.finalDecision -eq "LMAX_R36_EXECUTION_OPERATIONS_IMPLEMENTED_NO_EXTERNAL_ACTIVATION" -or
    $r36Decision.resultClassification -eq "LMAX_R36_EXECUTION_OPERATIONS_IMPLEMENTED_NO_EXTERNAL_ACTIVATION" -or
    $r36Decision.decision -eq "LMAX_R36_EXECUTION_OPERATIONS_IMPLEMENTED_NO_EXTERNAL_ACTIVATION"
) "R36 decision mismatch."
$checks.r36DecisionRemainsExpected = $true

$approval = Read-Json (Join-Path $artifactRoot "phase-lmax-r37-operator-approval-record.json")
Assert-True ($approval.approvalPhrase -eq $approvalPhrase) "R37 approval phrase mismatch."
Assert-True ($approval.operator -eq "Philippe") "R37 operator mismatch."
Assert-True ($approval.phase -eq "LMAX-R37") "R37 phase mismatch in approval record."
$checks.operatorApprovalExact = $true

$preflight = Read-Json (Join-Path $artifactRoot "phase-lmax-r37-preflight-gate.json")
Assert-True ($preflight.passed -eq $true) "R37 preflight did not pass before final validation."
Assert-True ($preflight.r36Decision -eq "LMAX_R36_EXECUTION_OPERATIONS_IMPLEMENTED_NO_EXTERNAL_ACTIVATION") "R37 preflight did not validate R36 decision."
$checks.preflightPassedBeforeActivation = $true

$finalValidation = Read-Json (Join-Path $artifactRoot "phase-lmax-r37-final-bounded-executor-provider-client-operation-validation.json")
Assert-True ($finalValidation.validationPassed -eq $false) "R37 final validation unexpectedly passed."
Assert-True ($finalValidation.resultClassification -eq "LMAX_R37_FAIL_OPERATION_BINDINGS_NOT_COMPOSABLE") "R37 final validation classification mismatch."
Assert-True ($finalValidation.concreteFinalAbortCause -eq "OperationBindingsNotComposableConcreteRealCoresNotConfigured") "R37 concrete abort cause mismatch."
Assert-True ($finalValidation.operationBindingsComposableIntoProviderClients -eq $true) "R37 operation bindings were not composable into provider clients."
Assert-True ($finalValidation.operationBindingsComposableIntoBoundedExecutorPath -eq $false) "R37 operation bindings unexpectedly composable into bounded executor path."
Assert-True ($finalValidation.concreteRealOperationCoresConfigured -eq $false) "R37 concrete real operation cores unexpectedly configured."
$checks.finalBoundedExecutorProviderClientOperationValidationExplicit = $true

$allowedDecisions = @(
    "LMAX_R37_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED",
    "LMAX_R37_FAIL_PREFLIGHT_ABORTED",
    "LMAX_R37_FAIL_BOUNDED_EXECUTOR_VALIDATION_FAILED",
    "LMAX_R37_FAIL_PROVIDER_COMPLETENESS_REGRESSION",
    "LMAX_R37_FAIL_CLIENT_COMPLETENESS_REGRESSION",
    "LMAX_R37_FAIL_OPERATION_COMPLETENESS_REGRESSION",
    "LMAX_R37_FAIL_OPERATION_BINDINGS_NOT_COMPOSABLE",
    "LMAX_R37_FAIL_CREDENTIAL_CONFIG_MISSING",
    "LMAX_R37_FAIL_CREDENTIAL_CONFIG_NOT_APPROVED",
    "LMAX_R37_FAIL_REQUIRES_API_WORKER_STARTUP",
    "LMAX_R37_FAIL_REQUIRES_LIVE_LAUNCHER",
    "LMAX_R37_FAIL_TCP_RUNTIME_BOUNDARY",
    "LMAX_R37_FAIL_TLS_RUNTIME_BOUNDARY",
    "LMAX_R37_FAIL_FIX_LOGON_RUNTIME_BOUNDARY",
    "LMAX_R37_FAIL_MARKETDATA_RUNTIME_BOUNDARY",
    "LMAX_R37_FAIL_SESSION_OR_MARKETDATA_BOUNDARY",
    "LMAX_R37_FAIL_SAFETY_CONSTRAINT",
    "LMAX_R37_INCONCLUSIVE_SANITIZED_EVIDENCE"
)

$decision = Read-Json (Join-Path $artifactRoot "phase-lmax-r37-decision-gate.json")
$r37DecisionValue = if ($decision.finalDecision) { $decision.finalDecision } elseif ($decision.resultClassification) { $decision.resultClassification } else { $decision.decision }
Assert-True ($allowedDecisions -contains $r37DecisionValue) "R37 decision is not an allowed classification."
Assert-True ($r37DecisionValue -eq "LMAX_R37_FAIL_OPERATION_BINDINGS_NOT_COMPOSABLE") "R37 decision mismatch."
Assert-True ($decision.concreteFinalAbortCause -eq "OperationBindingsNotComposableConcreteRealCoresNotConfigured") "R37 decision abort cause mismatch."
$checks.r37DecisionAllowedAndExpected = $true

$activation = Read-Json (Join-Path $artifactRoot "phase-lmax-r37-temporary-runtime-activation-record.json")
Assert-True ($activation.attemptCount -le 1) "R37 attempt count exceeded one."
Assert-True ($activation.attemptCount -eq 0) "R37 activation attempt should not have executed."
Assert-False $activation.activationAttemptExecuted "R37 activation unexpectedly executed."
Assert-False $activation.externalRunExecuted "R37 external run unexpectedly executed."
Assert-False $activation.runtimePoweredUp "R37 runtime unexpectedly powered up."
Assert-False $activation.runtimeEnablementExecuted "R37 runtime enablement unexpectedly executed."
Assert-False $activation.runtimeEnablementPersisted "R37 runtime enablement unexpectedly persisted."
Assert-NotTrue $activation.realSocketOpened "R37 opened a real socket."
Assert-NotTrue $activation.realTcpConnectionAttempted "R37 attempted TCP."
Assert-NotTrue $activation.realTlsHandshakeAttempted "R37 attempted TLS."
Assert-NotTrue $activation.realFixLogonAttempted "R37 attempted FIX logon."
Assert-NotTrue $activation.realMarketDataRequestSent "R37 sent MarketDataRequest."
$checks.atMostOneActivationAttemptOccurred = $true
$checks.activationDidNotExecute = $true

$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r37-forbidden-action-validation.json")
$forbiddenFalseFields = @(
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
)
foreach ($field in $forbiddenFalseFields) {
    Assert-False $forbidden.$field "Forbidden action flag was true: $field"
}
Assert-True ($forbidden.passed -eq $true) "R37 forbidden action summary did not pass."
$checks.noForbiddenActionOccurred = $true

$instrumentStatus = Read-Json (Join-Path $artifactRoot "phase-lmax-r37-approved-instrument-status-record.json")
Assert-True ($instrumentStatus.approvedInstrumentsOnly -eq $true) "R37 approvedInstrumentsOnly was false."
Assert-True ($instrumentStatus.nonApprovedInstrumentTouched -eq $false) "R37 touched non-approved instruments."
Assert-True ($instrumentStatus.usdJpyCaveatPreserved -eq $true) "R37 USDJPY caveat was not preserved."
$checks.onlyApprovedInstrumentsTouched = $true
$checks.usdJpyCaveatPreserved = $true

$boundary = Read-Json (Join-Path $artifactRoot "phase-lmax-r37-sanitized-runtime-boundary-evidence.json")
Assert-True ($boundary.outputSanitized -eq $true) "R37 boundary evidence was not sanitized."
Assert-False $boundary.credentialValuesPrinted "R37 boundary evidence printed credentials."
Assert-False $boundary.credentialValuesStored "R37 boundary evidence stored credentials."
Assert-False $boundary.credentialValuesReturned "R37 boundary evidence returned credentials."
Assert-NotTrue $boundary.rawFixStored "R37 boundary evidence stored raw FIX."
Assert-True ($boundary.tcpBoundaryStatus -eq "NotAttempted") "R37 TCP boundary status mismatch."
Assert-True ($boundary.tlsBoundaryStatus -eq "NotAttempted") "R37 TLS boundary status mismatch."
Assert-True ($boundary.fixLogonSessionBoundaryStatus -eq "NotAttempted") "R37 FIX boundary status mismatch."
Assert-True ($boundary.marketDataRequestBoundaryStatus -eq "NotAttempted") "R37 MarketData boundary status mismatch."
$checks.outputSanitized = $true

$shutdown = Read-Json (Join-Path $artifactRoot "phase-lmax-r37-shutdown-revert-record.json")
Assert-True ($shutdown.shutdownOrRevertCompleted -eq $true) "R37 shutdown/revert did not complete."
Assert-False $shutdown.activationStarted "R37 activation unexpectedly started."
Assert-False $shutdown.persistentRuntimeEnablementRemoved "R37 reported persisted runtime enablement cleanup."
Assert-True ($shutdown.defaultGatewayRegistrationRestoredOrUnchanged -eq $true) "R37 default gateway registration was not unchanged/restored."
Assert-True ($shutdown.apiWorkerGatewayMode -eq "FakeLmaxGatewayOnly") "R37 API/Worker gateway mode mismatch in shutdown record."
Assert-NotTrue $shutdown.runtimeEnablementPersisted "R37 runtime enablement persisted."
Assert-NotTrue $shutdown.defaultGatewayRegistrationChanged "R37 default gateway registration changed."
Assert-NotTrue $shutdown.liveLauncherCreated "R37 live launcher was created."
Assert-NotTrue $shutdown.hostedServiceAdded "R37 hosted service was added."
Assert-NotTrue $shutdown.backgroundWorkerAdded "R37 background worker was added."
$checks.shutdownRevertCompleted = $true

$nonMutation = Read-Json (Join-Path $artifactRoot "phase-lmax-r37-post-attempt-non-mutation-validation.json")
$nonMutationFalseFields = @(
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
)
foreach ($field in $nonMutationFalseFields) {
    Assert-False $nonMutation.$field "Post-attempt non-mutation flag was true: $field"
}
Assert-True ($nonMutation.approvedInstrumentsOnly -eq $true) "R37 non-mutation approvedInstrumentsOnly was false."
Assert-True ($nonMutation.usdJpyCaveatPreserved -eq $true) "R37 non-mutation USDJPY caveat was not preserved."
Assert-True ($nonMutation.outputSanitized -eq $true) "R37 non-mutation output was not sanitized."
Assert-True ($nonMutation.shutdownOrRevertCompleted -eq $true) "R37 non-mutation shutdown/revert did not complete."
$checks.noPersistentRuntimeEnablementOccurred = $true

$rail = Read-Json (Join-Path $artifactRoot "phase-lmax-r37-rail-isolation-validation.json")
Assert-False $rail.validatedEvidenceArchivesModified "R37 modified validated evidence archives."
Assert-False $rail.phase7AThrough7NArchivesModified "R37 modified Phase 7A-7N archives."
Assert-False $rail.usdJpyT1ThroughT7ArtifactsModified "R37 modified USDJPY T1-T7 artifacts."
Assert-False $rail.r1ThroughR36ArtifactsModifiedExceptReadOnlyReference "R37 modified R1-R36 artifacts outside read-only reference."
Assert-True ($rail.passed -eq $true) "R37 rail isolation failed."
$checks.archivesWereNotModified = $true

$appsettingsPath = Join-Path $Root "src/QQ.Production.Intraday.Api/appsettings.json"
$appsettingsText = Get-Content -LiteralPath $appsettingsPath -Raw
Assert-True ($appsettingsText -match '"RequireFakeExecutionGateway"\s*:\s*true') "API appsettings does not require fake execution gateway."
Assert-True ($appsettingsText -notmatch "AllowLiveTrading`"\s*:\s*true") "API appsettings enables AllowLiveTrading."
Assert-True ($appsettingsText -notmatch "AllowOrderSubmission`"\s*:\s*true") "API appsettings enables AllowOrderSubmission."
$checks.apiWorkerGatewayModeRemainsFakeLmaxGatewayOnly = $true

$programPaths = @(
    (Join-Path $Root "src/QQ.Production.Intraday.Api/Program.cs"),
    (Join-Path $Root "src/QQ.Production.Intraday.Worker/Program.cs")
)
foreach ($programPath in $programPaths) {
    if (Test-Path -LiteralPath $programPath) {
        $programText = Get-Content -LiteralPath $programPath -Raw
        if ($programPath -like "*QQ.Production.Intraday.Api*") {
            Assert-True ($programText -match "AddSingleton<IVenueExecutionGateway,\s*FakeLmaxGateway>") "API Program.cs does not preserve FakeLmaxGateway default registration."
        }
        Assert-True ($programText -notmatch "LmaxTemporaryReadOnlyActivationExecutor") "R37 added API/Worker wiring for bounded executor in $programPath"
        Assert-True ($programText -notmatch "LmaxReadOnlyExecutionOperationBindingSet") "R37 added API/Worker wiring for operation bindings in $programPath"
    }
}
$checks.noApiWorkerWiringAdded = $true

$unexpectedScripts = Get-ChildItem -LiteralPath (Join-Path $Root "scripts") -Filter "*lmax*r37*" -File |
    Where-Object { $_.Name -ne "check-lmax-r37-temporary-readonly-runtime-gate.ps1" }
Assert-True ($unexpectedScripts.Count -eq 0) "Unexpected R37 live/script file present: $($unexpectedScripts.Name -join ', ')"
$checks.noLiveConnectionScriptCreated = $true

$gate = [ordered]@{
    phase = "LMAX-R37"
    validatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    validator = "scripts/check-lmax-r37-temporary-readonly-runtime-gate.ps1"
    passed = $true
    decision = "LMAX_R37_FAIL_OPERATION_BINDINGS_NOT_COMPOSABLE"
    concreteFinalAbortCause = "OperationBindingsNotComposableConcreteRealCoresNotConfigured"
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
Write-Host "LMAX R37 gate validation passed: $gatePath"
