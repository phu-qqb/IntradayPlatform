param(
    [string]$Root = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

$artifactRoot = Join-Path $Root "artifacts/readiness/lmax-runtime-enablement"
$gatePath = Join-Path $artifactRoot "phase-lmax-r54-gate-validation.json"
$classification = "LMAX_R54_PASS_RETRY_PHASE_RESERVATION_RULE_FIXED_NO_EXTERNAL_ACTIVATION"
$cause = "ApprovedBoundedExecutableRetryPhaseReservationMissingR53"
$usdJpyCaveat = "prior failed-safe root cause remains unproven"

$allowedClassifications = @(
    "LMAX_R54_PASS_RETRY_PHASE_RESERVATION_RULE_FIXED_NO_EXTERNAL_ACTIVATION",
    "LMAX_R54_FAIL_R53_PHASE_RESERVATION_STILL_MISSING",
    "LMAX_R54_FAIL_NEXT_RETRY_PHASE_WOULD_REPEAT_SAME_BLOCKER",
    "LMAX_R54_FAIL_RETRY_PHASE_RULE_TOO_PERMISSIVE",
    "LMAX_R54_FAIL_APPROVAL_GATES_WEAKENED",
    "LMAX_R54_FAIL_COMPOSITION_CHAIN_REGRESSION",
    "LMAX_R54_FAIL_API_WORKER_GATEWAY_REGRESSION",
    "LMAX_R54_FAIL_LIVE_LAUNCHER_OR_BACKGROUND_SERVICE_INTRODUCED",
    "LMAX_R54_FAIL_FORBIDDEN_ACTION_INTRODUCED",
    "LMAX_R54_FAIL_EXTERNAL_BOUNDARY_ATTEMPTED",
    "LMAX_R54_FAIL_CREDENTIAL_VALUES_RETURNED_OR_SECRET_RISK",
    "LMAX_R54_FAIL_USDJPY_CAVEAT_WEAKENED",
    "LMAX_R54_FAIL_BUILD_OR_TESTS"
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

$required = @(
    "phase-lmax-r54-retry-phase-reservation-rule-fix-report.md",
    "phase-lmax-r54-retry-phase-reservation-rule-summary.json",
    "phase-lmax-r54-r53-blocker-before-after-classification.json",
    "phase-lmax-r54-preflight-trace-review.json",
    "phase-lmax-r54-retry-phase-rule-validation.json",
    "phase-lmax-r54-composition-chain-validation.json",
    "phase-lmax-r54-approval-gate-validation.json",
    "phase-lmax-r54-forbidden-actions-audit.json",
    "phase-lmax-r54-api-worker-fake-gateway-audit.json",
    "phase-lmax-r54-no-live-launcher-audit.json",
    "phase-lmax-r54-no-external-boundary-attempted.json",
    "phase-lmax-r54-usdjpy-caveat-preservation.json",
    "phase-lmax-r54-next-phase-recommendation.json",
    "phase-lmax-r54-gate-validation.json"
)

$checks = [ordered]@{}
foreach ($file in $required) {
    Assert-True (Test-Path -LiteralPath (Join-Path $artifactRoot $file)) "Missing R54 artifact: $file"
}
$checks.requiredArtifactsExist = $true

$r53 = Read-Json (Join-Path $artifactRoot "phase-lmax-r53-gate-validation.json")
Assert-True $r53.passed "R53 gate validation did not pass."
Assert-Equal $r53.classification "LMAX_R53_FAIL_PRE_EXTERNAL_APPROVAL_OR_COMPOSITION_REGRESSION" "R53 classification"
Assert-Equal $r53.concreteCause $cause "R53 concrete cause"
Assert-False $r53.externalActivationAttempted "R53 external activation attempted."
$checks.r53BlockerEvidencePresent = $true

$summary = Read-Json (Join-Path $artifactRoot "phase-lmax-r54-retry-phase-reservation-rule-summary.json")
Assert-True ($allowedClassifications -contains [string]$summary.classification) "R54 classification not allowed: $($summary.classification)"
Assert-Equal $summary.classification $classification "R54 classification"
Assert-False $summary.approvedBoundedExecutableRetryPhaseReservationMissingR53 "R53 reservation blocker remains true."
Assert-True $summary.r53AcceptedAsRetryPhase "R53 is not accepted."
Assert-Equal $summary.nextRetryPhase "LMAX-R55" "Next retry phase"
Assert-True $summary.nextRetryWouldAvoidSameMissingReservationBlocker "R55 would repeat missing-reservation blocker."
Assert-True $summary.exactPerPhaseOperatorApprovalStillRequired "Exact per-phase operator approval requirement missing."
Assert-False $summary.approvalGatesWeakened "Approval gates weakened."
foreach ($field in @("r42AdapterExecutablePathValid","r44BoundedRuntimeCompositionValid","r46ExecutableBoundaryOperationCompositionValid","r48ExternalBoundaryProviderExecutionCompositionValid","r50PreExternalConsolidationValid","r52CredentialConfigSourceBindingValid")) {
    Assert-True $summary.$field "Composition chain flag missing: $field"
}
Assert-False $summary.externalActivationAttempted "External activation attempted."
Assert-False $summary.externalBoundaryAttempted "External boundary attempted."
Assert-Equal ([int]$summary.attemptCount) 0 "Attempt count"
foreach ($field in @("credentialValuesReturned","credentialValuesRead","credentialValuesPrinted","credentialValuesStored","credentialValuesSerialized","productionAccountAllowed","productionAccountUsed")) {
    Assert-False $summary.$field "Unsafe summary flag true: $field"
}
Assert-True $summary.buildRepresented "Build evidence missing."
Assert-True $summary.testsRepresented "Test evidence missing."
Assert-True ([string]$summary.buildResult -like "PASS*") "Build result is not PASS."
Assert-True ([string]$summary.focusedTestResult -like "PASS*") "Focused test result is not PASS."
Assert-True ([string]$summary.fullTestResult -like "PASS*") "Full test result is not PASS."
$checks.summaryPassed = $true

$beforeAfter = Read-Json (Join-Path $artifactRoot "phase-lmax-r54-r53-blocker-before-after-classification.json")
Assert-Equal $beforeAfter.before.blocker $cause "Before blocker"
Assert-False $beforeAfter.before.isApprovedR53 "Before R53 approval should be false."
Assert-True $beforeAfter.after.blockerCleared "After blocker not cleared."
Assert-True $beforeAfter.after.isApprovedR53 "After R53 approval should be true."
Assert-True $beforeAfter.after.isApprovedR55 "After R55 approval should be true."
Assert-False $beforeAfter.after.sameMissingReservationBlockerExpectedForR55 "R55 same blocker expected."
$checks.beforeAfterPassed = $true

$traceReview = Read-Json (Join-Path $artifactRoot "phase-lmax-r54-preflight-trace-review.json")
Assert-True $traceReview.passed "Preflight trace review failed."
Assert-Equal $traceReview.r53ConcreteCause $cause "Trace review cause"
Assert-Equal ([int]$traceReview.failedGateCount) 1 "Trace failed gate count"
Assert-Equal $traceReview.failedGate.class "LmaxApprovedBoundedExecutableRetryPhaseReservations" "Failed gate class"
Assert-Equal $traceReview.failedGate.method "IsApproved" "Failed gate method"
Assert-Equal $traceReview.failedGate.actualAfterR54 "true" "Failed gate actual after R54"
Assert-False $traceReview.runtimeBoundaryFailure "Trace misclassified runtime boundary failure."
Assert-False $traceReview.credentialConfigFailure "Trace misclassified credential/config failure."
Assert-False $traceReview.tcpTlsFixMarketDataFailure "Trace misclassified network failure."
$checks.traceReviewPassed = $true

$rule = Read-Json (Join-Path $artifactRoot "phase-lmax-r54-retry-phase-rule-validation.json")
Assert-True $rule.passed "Retry phase rule validation failed."
Assert-Equal ([int]$rule.minimumRetryPhaseNumber) 43 "Minimum retry phase"
Assert-Equal ([int]$rule.maximumRetryPhaseNumber) 99 "Maximum retry phase"
Assert-True $rule.r53Accepted "R53 not accepted."
Assert-True $rule.r55Accepted "R55 not accepted."
Assert-True $rule.futureOddRetryWithinBoundAccepted "Future odd retry in bound not accepted."
Assert-False $rule.sameReservationBlockerWouldRepeatForR55 "R55 same blocker would repeat."
foreach ($field in @("arbitraryPhaseNamesAccepted","malformedPhaseNamesAccepted","nonLmaxPhasesAccepted","evenReviewFixArchivePhasesAccepted","unboundedRetryPhasesAccepted")) {
    Assert-False $rule.$field "Retry phase rule too permissive: $field"
}
$checks.retryRulePassed = $true

$composition = Read-Json (Join-Path $artifactRoot "phase-lmax-r54-composition-chain-validation.json")
Assert-True $composition.passed "Composition chain validation failed."
foreach ($field in @("r42AdapterExecutablePathValid","r44BoundedRuntimeCompositionValid","r46ExecutableBoundaryOperationCompositionValid","r48ExternalBoundaryProviderExecutionCompositionValid","r50PreExternalConsolidationValid","r52CredentialConfigSourceBindingValid","adapterModeApprovedBoundedExecutableReadOnlyStillRequired","boundedExecutorApprovalStillRequired","runtimeDelegateBindingApprovalStillRequired")) {
    Assert-True $composition.$field "Composition flag missing: $field"
}
Assert-False $composition.approvalGatesWeakened "Approval gates weakened."
Assert-False $composition.externalBoundaryAttempted "External boundary attempted in composition."
$checks.compositionChainPassed = $true

$approval = Read-Json (Join-Path $artifactRoot "phase-lmax-r54-approval-gate-validation.json")
Assert-True $approval.passed "Approval gate validation failed."
Assert-True $approval.exactPerPhaseOperatorApprovalStillRequired "Exact per-phase approval not required."
Assert-True $approval.phaseRuleAloneIsNotActivationAuthority "Phase rule alone grants activation authority."
foreach ($field in @("adapterModeApprovedBoundedExecutableReadOnlyRequired","boundedExecutorApprovedRequired","runtimeDelegateBindingApprovedRequired","r42GateRequired","r44GateRequired","r46GateRequired","r48GateRequired","r50GateRequired","r52GateRequired","operatorApprovalTextMustNameCurrentRetryPhase")) {
    Assert-True $approval.$field "Approval gate flag missing: $field"
}
Assert-False $approval.approvalGatesWeakened "Approval gates weakened."
$checks.approvalGatePassed = $true

$forbidden = Read-Json (Join-Path $artifactRoot "phase-lmax-r54-forbidden-actions-audit.json")
Assert-True $forbidden.passed "Forbidden actions audit failed."
foreach ($field in @(
    "orderSubmissionExecuted",
    "newOrderSingleTouched",
    "cancelReplaceTouched",
    "orderPathIntroduced",
    "orderPathTouched",
    "tradingEnablementExecuted",
    "tradingStateMutated",
    "productionAccountUsed",
    "productionAccountAllowed",
    "apiStarted",
    "workerStarted",
    "hostedServiceAdded",
    "backgroundServiceAdded",
    "schedulerStarted",
    "pollingStarted",
    "replayExecuted",
    "shadowReplayExecuted",
    "shadowReplaySubmitted",
    "credentialConfigBoundaryRead",
    "credentialValuesRead",
    "credentialValuesReturned",
    "credentialPrinted",
    "credentialStored",
    "credentialSerialized",
    "rawSensitiveFixLogsStored",
    "runtimeEnablementPersisted",
    "externalActivationAttempted",
    "externalBoundaryAttempted"
)) {
    Assert-False $forbidden.$field "Forbidden action flag was true: $field"
}
$checks.forbiddenActionsPassed = $true

$api = Read-Json (Join-Path $artifactRoot "phase-lmax-r54-api-worker-fake-gateway-audit.json")
Assert-True $api.passed "API/Worker fake gateway audit failed."
Assert-Equal $api.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "API/Worker gateway mode"
Assert-False $api.defaultGatewayRegistrationChanged "Default gateway registration changed."
Assert-False $api.apiStarted "API started."
Assert-False $api.workerStarted "Worker started."
Assert-False $api.appsettingsLiveEnablementIntroduced "Appsettings live enablement introduced."
Assert-True $api.requireFakeExecutionGateway "RequireFakeExecutionGateway changed."
Assert-False $api.allowExternalConnections "AllowExternalConnections changed."
Assert-False $api.retryPhaseRuleWiredIntoApiWorker "Retry phase rule wired into API/Worker."
$checks.apiWorkerFakeGatewayPassed = $true

$launcher = Read-Json (Join-Path $artifactRoot "phase-lmax-r54-no-live-launcher-audit.json")
Assert-True $launcher.passed "No-live-launcher audit failed."
foreach ($field in @("liveLauncherCreated","mainAdded","consoleAppAdded","hostedServiceAdded","backgroundServiceAdded","schedulerAdded","pollingLoopAdded","apiEndpointAdded","workerStartupAdded")) {
    Assert-False $launcher.$field "Launcher/background flag true: $field"
}
$checks.noLiveLauncherPassed = $true

$boundary = Read-Json (Join-Path $artifactRoot "phase-lmax-r54-no-external-boundary-attempted.json")
Assert-True $boundary.passed "No-external-boundary validation failed."
Assert-False $boundary.externalActivationAttempted "External activation attempted."
Assert-False $boundary.externalBoundaryAttempted "External boundary attempted."
Assert-Equal ([int]$boundary.attemptCount) 0 "Boundary attempt count"
foreach ($field in @("credentialConfig","tcpSocket","tls","fixLogonSession","marketDataRequest","marketDataResponseEntries")) {
    Assert-Equal $boundary.$field "NotAttempted" "Boundary status for $field"
}
Assert-False $boundary.credentialValuesReturned "Boundary credentialValuesReturned true."
Assert-False $boundary.credentialValuesRead "Boundary credentialValuesRead true."
$checks.noExternalBoundaryPassed = $true

$usd = Read-Json (Join-Path $artifactRoot "phase-lmax-r54-usdjpy-caveat-preservation.json")
Assert-True $usd.passed "USDJPY caveat preservation failed."
Assert-Equal $usd.securityId "4004" "USDJPY SecurityID"
Assert-Equal $usd.securityIdSource "8" "USDJPY SecurityIDSource"
Assert-Equal $usd.lineage "USDJPY_T7_FINAL_READINESS_ARCHIVE_CLOSED_WITH_CAVEAT" "USDJPY lineage"
Assert-Equal $usd.caveat $usdJpyCaveat "USDJPY caveat"
Assert-True $usd.caveatPreserved "USDJPY caveat not preserved."
Assert-False $usd.caveatWeakened "USDJPY caveat weakened."
$checks.usdJpyCaveatPassed = $true

$next = Read-Json (Join-Path $artifactRoot "phase-lmax-r54-next-phase-recommendation.json")
Assert-True ([string]$next.recommendedNextPhase -like "Phase LMAX-R55*") "Missing R55 recommendation."
Assert-False $next.r55Executed "R55 was executed."
$checks.nextPhasePassed = $true

$sourcePath = Join-Path $Root "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxApprovedBoundedExecutableRetryPhaseReservations.cs"
$source = Get-Content -LiteralPath $sourcePath -Raw
foreach ($needle in @(
    "MinimumRetryPhaseNumber = 43",
    "MaximumRetryPhaseNumber = 99",
    "TryGetRetryPhaseNumber",
    "number >= MinimumRetryPhaseNumber",
    "number <= MaximumRetryPhaseNumber",
    "IsOdd(number)",
    "LMAX-R55"
)) {
    Assert-True ($source -match [regex]::Escape($needle)) "Reservation source missing token: $needle"
}
$checks.sourceRulePassed = $true

$apiProgram = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/Program.cs") -Raw
$workerPath = Join-Path $Root "src/QQ.Production.Intraday.Worker/Program.cs"
$workerProgram = if (Test-Path -LiteralPath $workerPath) { Get-Content -LiteralPath $workerPath -Raw } else { "" }
$appsettings = Get-Content -LiteralPath (Join-Path $Root "src/QQ.Production.Intraday.Api/appsettings.json") -Raw
Assert-True ($apiProgram -match "AddSingleton<IVenueExecutionGateway,\s*FakeLmaxGateway>") "API default gateway is not FakeLmaxGateway."
Assert-True ($appsettings -match '"RequireFakeExecutionGateway"\s*:\s*true') "RequireFakeExecutionGateway changed."
Assert-True ($appsettings -match '"AllowExternalConnections"\s*:\s*false') "AllowExternalConnections default changed."
foreach ($token in @("phase-lmax-r54", "LmaxApprovedBoundedExecutableRetryPhaseReservations", "ApprovedBoundedExecutableReadOnly", "LmaxTemporaryReadOnlyActivationExecutor")) {
    Assert-True ($apiProgram -notmatch [regex]::Escape($token)) "R54 API wiring detected: $token"
    Assert-True ($workerProgram -notmatch [regex]::Escape($token)) "R54 Worker wiring detected: $token"
    Assert-True ($appsettings -notmatch [regex]::Escape($token)) "R54 appsettings wiring detected: $token"
}
$checks.startupSafetyPassed = $true

$artifactText = (Get-ChildItem -LiteralPath $artifactRoot -Filter "phase-lmax-r54-*" -File | ForEach-Object {
    Get-Content -LiteralPath $_.FullName -Raw
}) -join "`n"
foreach ($envName in @("LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD", "LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID")) {
    $value = [Environment]::GetEnvironmentVariable($envName)
    if (-not [string]::IsNullOrWhiteSpace($value)) {
        Assert-True ($artifactText -notlike "*$value*") "R54 artifacts contain raw sensitive value for $envName."
    }
}
Assert-True ($artifactText -notmatch "554=") "R54 artifacts contain raw FIX password tag."
Assert-True ($artifactText -notmatch "35=D|35=F|35=H|35=AE|35=8") "R54 artifacts contain forbidden raw sensitive FIX message type."
$checks.artifactSanitizationPassed = $true

$gate = [ordered]@{
    phase = "LMAX-R54"
    validatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    validator = "scripts/check-lmax-r54-retry-phase-reservation-rule-fix.ps1"
    passed = $true
    classification = $classification
    approvedBoundedExecutableRetryPhaseReservationMissingR53 = $false
    r53AcceptedAsRetryPhase = $true
    r55AcceptedAsRetryPhase = $true
    externalActivationAttempted = $false
    attemptCount = 0
    credentialValuesReturned = $false
    checks = $checks
}

$gate | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $gatePath -Encoding UTF8
Write-Host "LMAX R54 gate validation passed: $gatePath"
