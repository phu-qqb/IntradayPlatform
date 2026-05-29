param(
    [string]$Root = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

$artifactRoot = Join-Path $Root "artifacts/readiness/lmax-runtime-enablement"
$gatePath = Join-Path $artifactRoot "phase-lmax-r41-gate-validation.json"
$expectedApproval = "I, Philippe, explicitly approve Phase LMAX-R41 for one temporary Demo read-only runtime market-data activation attempt after the final delegate binding fix for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority."
$expectedR40Decision = "LMAX_R40_RUNTIME_CORE_DELEGATE_BINDINGS_IMPLEMENTED_NO_EXTERNAL_ACTIVATION"

$allowedDecisions = @(
    "LMAX_R41_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED",
    "LMAX_R41_FAIL_PREFLIGHT_ABORTED",
    "LMAX_R41_FAIL_BOUNDED_EXECUTOR_VALIDATION_FAILED",
    "LMAX_R41_FAIL_PROVIDER_COMPLETENESS_REGRESSION",
    "LMAX_R41_FAIL_CLIENT_COMPLETENESS_REGRESSION",
    "LMAX_R41_FAIL_OPERATION_COMPLETENESS_REGRESSION",
    "LMAX_R41_FAIL_CORE_COMPOSITION_REGRESSION",
    "LMAX_R41_FAIL_RUNTIME_DELEGATE_BINDING_REGRESSION",
    "LMAX_R41_FAIL_CREDENTIAL_CONFIG_MISSING",
    "LMAX_R41_FAIL_CREDENTIAL_CONFIG_NOT_APPROVED",
    "LMAX_R41_FAIL_REQUIRES_API_WORKER_STARTUP",
    "LMAX_R41_FAIL_REQUIRES_LIVE_LAUNCHER",
    "LMAX_R41_FAIL_TCP_RUNTIME_BOUNDARY",
    "LMAX_R41_FAIL_TLS_RUNTIME_BOUNDARY",
    "LMAX_R41_FAIL_FIX_LOGON_RUNTIME_BOUNDARY",
    "LMAX_R41_FAIL_MARKETDATA_RUNTIME_BOUNDARY",
    "LMAX_R41_FAIL_SESSION_OR_MARKETDATA_BOUNDARY",
    "LMAX_R41_FAIL_SAFETY_CONSTRAINT",
    "LMAX_R41_INCONCLUSIVE_SANITIZED_EVIDENCE"
)

$allowedAbortCategories = @(
    "preflight aborted",
    "bounded executor validation failed",
    "provider completeness regression",
    "client completeness regression",
    "operation completeness regression",
    "core composition regression",
    "runtime delegate binding regression",
    "credential/config source missing",
    "credential/config not approved",
    "approved stack requires API/Worker startup",
    "approved stack would require live launcher creation",
    "safety gate rejected a specific flag/config",
    "TCP/socket runtime boundary failure",
    "TLS runtime boundary failure",
    "FIX logon/session boundary failure",
    "MarketDataRequest / market-data boundary failure"
)

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Missing required file: $Path"
    }

    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Assert-True($Condition, [string]$Message) {
    if ($Condition -ne $true) {
        throw $Message
    }
}

function Assert-False($Value, [string]$Message) {
    if ($Value -ne $false) {
        throw $Message
    }
}

function Assert-Equal($Actual, $Expected, [string]$Message) {
    if ($Actual -ne $Expected) {
        throw "$Message Expected '$Expected' but got '$Actual'."
    }
}

function Get-DecisionValue($Decision) {
    if ($Decision.finalDecision) { return [string]$Decision.finalDecision }
    if ($Decision.resultClassification) { return [string]$Decision.resultClassification }
    if ($Decision.decision) { return [string]$Decision.decision }
    return ""
}

$requiredR41Artifacts = @(
    "phase-lmax-r41-operator-approval-record.json",
    "phase-lmax-r41-preflight-gate.json",
    "phase-lmax-r41-final-bounded-executor-delegate-bound-stack-validation.json",
    "phase-lmax-r41-temporary-runtime-activation-record.json",
    "phase-lmax-r41-approved-instrument-status-record.json",
    "phase-lmax-r41-sanitized-runtime-boundary-evidence.json",
    "phase-lmax-r41-forbidden-action-validation.json",
    "phase-lmax-r41-shutdown-revert-record.json",
    "phase-lmax-r41-post-attempt-non-mutation-validation.json",
    "phase-lmax-r41-rail-isolation-validation.json",
    "phase-lmax-r41-decision-gate.json",
    "phase-lmax-r41-temporary-readonly-runtime-report.md",
    "phase-lmax-r41-operator-note.md",
    "phase-lmax-r41-gate-validation.json"
)

$checks = [ordered]@{}
foreach ($file in $requiredR41Artifacts) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactRoot $file)) "Missing R41 artifact: $file"
}
$checks.requiredR41ArtifactsExist = $true

$missingPrior = @()
for ($phase = 1; $phase -le 40; $phase++) {
    $matches = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r$phase-*" -File -ErrorAction SilentlyContinue
    if ($matches.Count -eq 0) {
        $missingPrior += "R$phase"
    }
}
Assert-True ($missingPrior.Count -eq 0) ("Missing prior LMAX runtime artifacts: " + ($missingPrior -join ", "))
$checks.r1ThroughR40ArtifactsStillExist = $true

$r40Decision = Read-Json (Join-Path $artifactRoot "phase-lmax-r40-decision-gate.json")
Assert-Equal (Get-DecisionValue $r40Decision) $expectedR40Decision "R40 decision mismatch."
$checks.r40DecisionRemainsExpected = $true

$approval = Read-Json (Join-Path $artifactRoot "phase-lmax-r41-operator-approval-record.json")
Assert-Equal $approval.approvalPhrase $expectedApproval "R41 approval phrase mismatch."
Assert-Equal $approval.operator "Philippe" "R41 operator mismatch."
Assert-Equal $approval.phase "LMAX-R41" "R41 phase mismatch."
Assert-Equal $approval.environment "Demo/read-only" "R41 environment mismatch."
Assert-True $approval.sanitizedOutputOnly "R41 approval did not require sanitized output."
Assert-True $approval.immediateAbortAuthority "R41 approval did not preserve immediate abort authority."
$approvedSymbols = @($approval.approvedInstruments | ForEach-Object { $_.symbol })
Assert-True (($approvedSymbols -join ",") -eq "GBPUSD,EURGBP,AUDUSD,USDJPY") "R41 approved instrument order/list mismatch."
Assert-Equal $approval.usdJpyCaveat "prior failed-safe root cause remains unproven" "USDJPY caveat mismatch."
$checks.operatorApprovalExact = $true

$preflight = Read-Json (Join-Path $artifactRoot "phase-lmax-r41-preflight-gate.json")
Assert-True $preflight.outputSanitizationRequired "Preflight did not require output sanitization."
Assert-True $preflight.shutdownRevertPlanPresent "Preflight did not include shutdown/revert plan."
Assert-True $preflight.boundedExecutorRequired "Preflight did not require bounded executor."
Assert-True $preflight.providerCompletenessPass "Provider completeness was not PASS in preflight."
Assert-True $preflight.clientCompletenessPass "Client completeness was not PASS in preflight."
Assert-True $preflight.operationCompletenessPass "Operation completeness was not PASS in preflight."
Assert-True $preflight.coreCompositionPass "Core composition was not PASS in preflight."
Assert-True $preflight.runtimeDelegateBindingCompletenessPass "Runtime delegate binding completeness was not PASS in preflight."
Assert-True $preflight.approvedInstrumentsOnly "Preflight did not confirm approved instruments only."
Assert-True $preflight.usdJpyCaveatPresent "Preflight did not preserve USDJPY caveat."
Assert-False $preflight.productionAccount "Preflight production account flag."
Assert-False $preflight.allowOrderSubmission "Preflight AllowOrderSubmission flag."
Assert-False $preflight.allowLiveTrading "Preflight AllowLiveTrading flag."
Assert-False $preflight.isTradingEnabled "Preflight IsTradingEnabled flag."
Assert-False $preflight.scheduler "Preflight scheduler flag."
Assert-False $preflight.polling "Preflight polling flag."
Assert-False $preflight.replay "Preflight replay flag."
Assert-False $preflight.shadowReplay "Preflight shadow replay flag."
Assert-False $preflight.tradingMutation "Preflight trading mutation flag."
Assert-False $preflight.persistentRuntimeEnablement "Preflight persistent runtime enablement flag."
Assert-False $preflight.defaultGatewayRegistrationChange "Preflight default gateway change flag."
$checks.preflightPassedOrAbortedSafely = ($preflight.passed -eq $true -or ($preflight.abortedBeforeExternalAction -eq $true -and $preflight.externalActionExecuted -eq $false))
Assert-True $checks.preflightPassedOrAbortedSafely "Preflight neither passed nor aborted safely."

$finalValidation = Read-Json (Join-Path $artifactRoot "phase-lmax-r41-final-bounded-executor-delegate-bound-stack-validation.json")
Assert-True $finalValidation.boundedExecutorExists "Bounded executor missing from final validation."
Assert-True $finalValidation.boundedExecutorUsedForValidation "Bounded executor not used for validation."
Assert-Equal $finalValidation.maxAttemptCount 1 "Final validation maxAttemptCount."
Assert-Equal $finalValidation.retryCount 0 "Final validation retryCount."
Assert-False $finalValidation.batchMode "Final validation batch mode."
Assert-False $finalValidation.loopMode "Final validation loop mode."
Assert-True $finalValidation.providerCompletenessPass "Final validation provider completeness."
Assert-True $finalValidation.clientCompletenessPass "Final validation client completeness."
Assert-True $finalValidation.operationCompletenessPass "Final validation operation completeness."
Assert-True $finalValidation.coreCompositionPass "Final validation core composition."
Assert-True $finalValidation.runtimeDelegateBindingCompletenessPass "Final validation runtime delegate binding."
Assert-True $finalValidation.delegateBindingSetExists "Delegate binding set missing."
Assert-True $finalValidation.delegateBindingFactoryExists "Delegate binding factory missing."
Assert-True $finalValidation.delegateBindingValidatorExists "Delegate binding validator missing."
Assert-True $finalValidation.credentialsNotPrinted "Final validation credentials printed."
Assert-True $finalValidation.credentialsNotPersisted "Final validation credentials persisted."
Assert-False $finalValidation.apiWorkerStartupRequired "Final validation requires API/Worker startup."
Assert-False $finalValidation.defaultConfigMutationRequired "Final validation requires default config mutation."
Assert-False $finalValidation.hostedBackgroundServiceRequired "Final validation requires hosted/background service."
Assert-False $finalValidation.liveLauncherCreated "Final validation live launcher created."
$checks.finalBoundedExecutorDelegateBoundValidationExplicit = $true

$activation = Read-Json (Join-Path $artifactRoot "phase-lmax-r41-temporary-runtime-activation-record.json")
Assert-True ([int]$activation.attemptCount -le 1) "More than one activation attempt occurred."
Assert-Equal ([int]$activation.retryCount) 0 "Retry count mismatch."
Assert-False $activation.batchMode "Activation batch mode."
Assert-False $activation.loopMode "Activation loop mode."
if ($activation.activationAttemptExecuted -eq $true) {
    Assert-True $activation.boundedExecutorUsed "Activation executed without bounded executor."
    Assert-True $activation.shutdownRevertCompleted "Activation executed without shutdown/revert."
} else {
    Assert-False $activation.realSocketOpened "Activation did not execute but real socket opened."
    Assert-False $activation.realTcpConnectionAttempted "Activation did not execute but TCP attempted."
    Assert-False $activation.realTlsHandshakeAttempted "Activation did not execute but TLS attempted."
    Assert-False $activation.realFixLogonAttempted "Activation did not execute but FIX logon attempted."
    Assert-False $activation.realMarketDataRequestSent "Activation did not execute but market-data request sent."
}
$checks.atMostOneActivationAttempt = $true

$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r41-forbidden-action-validation.json")
foreach ($field in @(
    "orderSubmissionExecuted",
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
)) {
    Assert-False $forbidden.$field "Forbidden action flag was true: $field"
}
Assert-True $forbidden.outputSanitized "Forbidden action validation did not confirm sanitized output."
Assert-True $forbidden.passed "Forbidden action validation did not pass."
$checks.noForbiddenActionOccurred = $true

$post = Read-Json (Join-Path $artifactRoot "phase-lmax-r41-post-attempt-non-mutation-validation.json")
foreach ($field in @(
    "productionAccountUsed",
    "orderSubmissionExecuted",
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
    "defaultGatewayRegistrationChanged",
    "runtimeEnablementPersisted",
    "credentialsPrinted",
    "credentialsStored",
    "liveLauncherCreated",
    "hostedServiceAdded",
    "backgroundWorkerAdded"
)) {
    Assert-False $post.$field "Post-attempt forbidden/non-mutation flag was true: $field"
}
Assert-True ([int]$post.attemptCount -le 1) "Post-attempt attempt count exceeded one."
Assert-Equal ([int]$post.retryCount) 0 "Post-attempt retry count."
Assert-True $post.approvedInstrumentsOnly "Post-attempt approved-instruments-only failed."
Assert-True $post.usdJpyCaveatPreserved "Post-attempt USDJPY caveat failed."
Assert-True $post.outputSanitized "Post-attempt output sanitization failed."
Assert-True $post.shutdownOrRevertCompleted "Post-attempt shutdown/revert failed."
Assert-True $post.passed "Post-attempt non-mutation validation failed."
$checks.postAttemptNonMutationPassed = $true

$rails = Read-Json (Join-Path $artifactRoot "phase-lmax-r41-rail-isolation-validation.json")
Assert-False $rails.validatedRailsModified "Validated rails modified."
Assert-False $rails.phase7ArchivesModified "Phase 7 archives modified."
Assert-False $rails.usdJpyT1T7ArchiveArtifactsModified "USDJPY T1-T7 archives modified."
Assert-False $rails.r1ThroughR40ArtifactsModifiedExceptReadOnlyReference "R1-R40 artifacts modified beyond read-only reference."
Assert-False $rails.nonApprovedInstrumentTouched "Non-approved instrument touched."
Assert-True ($rails.archivesModified -eq $false) "Archives modified."
Assert-True $rails.passed "Rail isolation validation failed."
$checks.railIsolationPassed = $true

$decision = Read-Json (Join-Path $artifactRoot "phase-lmax-r41-decision-gate.json")
$decisionValue = Get-DecisionValue $decision
Assert-True ($allowedDecisions -contains $decisionValue) "R41 decision is not an allowed classification: $decisionValue"
if ($decisionValue -ne "LMAX_R41_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED") {
    Assert-True ($decision.abortCauseCategory -and ($allowedAbortCategories -contains [string]$decision.abortCauseCategory)) "Abort cause category is not allowed."
    Assert-False $decision.externalRunExecuted "Decision says external run executed despite failed classification."
}
Assert-False $decision.forbiddenActionOccurred "Decision says forbidden action occurred."
Assert-Equal $decision.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "API/Worker gateway mode mismatch."
$checks.r41DecisionAllowed = $true

$instrumentStatus = Read-Json (Join-Path $artifactRoot "phase-lmax-r41-approved-instrument-status-record.json")
Assert-True $instrumentStatus.approvedInstrumentsOnly "Instrument status did not confirm approved instruments only."
Assert-True $instrumentStatus.usdJpyCaveatPreserved "Instrument status did not preserve USDJPY caveat."
Assert-True (($instrumentStatus.instruments | Measure-Object).Count -eq 4) "Instrument status did not contain exactly four instruments."
$checks.approvedInstrumentScopePassed = $true

$boundary = Read-Json (Join-Path $artifactRoot "phase-lmax-r41-sanitized-runtime-boundary-evidence.json")
Assert-True $boundary.outputSanitized "Boundary evidence not sanitized."
if ($activation.activationAttemptExecuted -eq $false) {
    Assert-Equal $boundary.tcpBoundaryStatus "NotAttempted" "TCP boundary should be NotAttempted after pre-connection abort."
    Assert-Equal $boundary.tlsBoundaryStatus "NotAttempted" "TLS boundary should be NotAttempted after pre-connection abort."
    Assert-Equal $boundary.fixLogonSessionBoundaryStatus "NotAttempted" "FIX boundary should be NotAttempted after pre-connection abort."
    Assert-Equal $boundary.marketDataRequestBoundaryStatus "NotAttempted" "MarketData boundary should be NotAttempted after pre-connection abort."
}
$checks.boundaryEvidenceSanitized = $true

$shutdown = Read-Json (Join-Path $artifactRoot "phase-lmax-r41-shutdown-revert-record.json")
Assert-True $shutdown.shutdownOrRevertCompleted "Shutdown/revert record did not complete."
Assert-False $shutdown.persistentRuntimeEnablementRemaining "Persistent runtime enablement remains."
Assert-False $shutdown.defaultGatewayRegistrationChanged "Shutdown/revert changed default gateway registration."
$checks.shutdownRevertPassed = $true

$apiProgram = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/Program.cs") -Raw
$workerProgramPath = Join-Path $Root "src/QQ.Production.Intraday.Worker/Program.cs"
$workerProgram = if (Test-Path -LiteralPath $workerProgramPath) { Get-Content -LiteralPath $workerProgramPath -Raw } else { "" }
$appsettings = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/appsettings.json") -Raw
Assert-True ($apiProgram -match "AddSingleton<IVenueExecutionGateway,\s*FakeLmaxGateway>") "API default gateway is not FakeLmaxGateway."
Assert-True ($appsettings -match '"RequireFakeExecutionGateway"\s*:\s*true') "API appsettings no longer require fake execution gateway."
Assert-True ($appsettings -match '"AllowExternalConnections"\s*:\s*false') "API appsettings changed external connection default."
Assert-True ($appsettings -match '"AllowOrderSubmission"\s*:\s*false') "API appsettings changed order submission default."
foreach ($token in @("LmaxTemporaryReadOnlyActivationExecutor", "LmaxReadOnlyRuntimeCoreDelegateBindingFactory", "phase-lmax-r41")) {
    Assert-True ($apiProgram -notmatch [regex]::Escape($token)) "R41 API wiring detected: $token"
    Assert-True ($workerProgram -notmatch [regex]::Escape($token)) "R41 Worker wiring detected: $token"
    Assert-True ($appsettings -notmatch [regex]::Escape($token)) "R41 appsettings wiring detected: $token"
}
$checks.apiWorkerDefaultFakeLmaxGatewayOnly = $true
$checks.noPersistentRuntimeEnablement = $true
$checks.noDefaultConfigEnablement = $true
$checks.noLiveLauncherOrHostedServiceAdded = $true

$r41Texts = Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r41-*" -File | ForEach-Object {
    Get-Content -LiteralPath $_.FullName -Raw
}
$combinedText = $r41Texts -join "`n"
foreach ($envName in @("LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD", "LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID")) {
    $value = [Environment]::GetEnvironmentVariable($envName)
    if (-not [string]::IsNullOrWhiteSpace($value)) {
        Assert-True ($combinedText -notlike "*$value*") "R41 artifacts contain raw sensitive value for $envName."
    }
}
Assert-True ($combinedText -notmatch "554=") "R41 artifacts contain raw FIX password tag."
$checks.rawSensitiveValuesAbsent = $true

$gate = [ordered]@{
    phase = "LMAX-R41"
    validatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    validator = "scripts/check-lmax-r41-temporary-readonly-runtime-gate.ps1"
    passed = $true
    decision = $decisionValue
    preflightPassed = [bool]$preflight.passed
    activationAttemptExecuted = [bool]$activation.activationAttemptExecuted
    abortCauseCategory = $decision.abortCauseCategory
    abortCauseCode = $decision.abortCauseCode
    checks = $checks
    safetyFlags = $decision.safetyFlags
}

$gate | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $gatePath -Encoding UTF8
Write-Host "LMAX R41 gate validation passed: $gatePath"
