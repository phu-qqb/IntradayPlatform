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

function Assert-In($Actual, [object[]]$Allowed, [string]$Message) {
    if ($Allowed -notcontains $Actual) {
        throw "$Message. Expected one of '$($Allowed -join "', '")' but got '$Actual'."
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
    "phase-lmax-r1-runtime-enablement-boundary-map.json",
    "phase-lmax-r1-component-impact-review.json",
    "phase-lmax-r1-readonly-safety-model.json",
    "phase-lmax-r1-future-r2-preconditions.json",
    "phase-lmax-r1-design-only-decision-gate.json",
    "phase-lmax-r1-non-run-validation.json",
    "phase-lmax-r1-runtime-enablement-design-review.md",
    "phase-lmax-r1-operator-note.md",
    "phase-lmax-r2-runtime-readonly-preflight-checklist.json",
    "phase-lmax-r2-future-r3-operator-approval-model.json",
    "phase-lmax-r2-future-r3-activation-boundary-design.json",
    "phase-lmax-r2-hard-block-matrix.json",
    "phase-lmax-r2-future-r3-evidence-schema.json",
    "phase-lmax-r2-test-validator-plan.json",
    "phase-lmax-r2-preflight-decision-gate.json",
    "phase-lmax-r2-non-run-validation.json",
    "phase-lmax-r2-readonly-runtime-preflight-pack.md",
    "phase-lmax-r2-operator-note.md",
    "phase-lmax-r3-operator-approval-record.json",
    "phase-lmax-r3-preflight-gate.json",
    "phase-lmax-r3-temporary-runtime-activation-record.json",
    "phase-lmax-r3-approved-instrument-status-record.json",
    "phase-lmax-r3-sanitized-runtime-boundary-evidence.json",
    "phase-lmax-r3-forbidden-action-validation.json",
    "phase-lmax-r3-shutdown-revert-record.json",
    "phase-lmax-r3-post-attempt-non-mutation-validation.json",
    "phase-lmax-r3-rail-isolation-validation.json",
    "phase-lmax-r3-decision-gate.json",
    "phase-lmax-r3-temporary-readonly-runtime-report.md",
    "phase-lmax-r3-operator-note.md",
    "phase-lmax-r4-r3-preflight-abort-root-cause-review.json",
    "phase-lmax-r4-narrow-runtime-path-requirements.json",
    "phase-lmax-r4-component-remediation-design.json",
    "phase-lmax-r4-future-r5-implementation-plan.json",
    "phase-lmax-r4-hard-block-validation-design.json",
    "phase-lmax-r4-remediation-decision-gate.json",
    "phase-lmax-r4-non-run-validation.json",
    "phase-lmax-r4-preflight-abort-remediation-pack.md",
    "phase-lmax-r4-operator-note.md",
    "phase-lmax-r5-inert-runtime-path-implementation-summary.json",
    "phase-lmax-r5-approved-instrument-allowlist.json",
    "phase-lmax-r5-safety-validator-spec.json",
    "phase-lmax-r5-sanitization-contract-summary.json",
    "phase-lmax-r5-test-coverage-summary.json",
    "phase-lmax-r5-inert-implementation-decision-gate.json",
    "phase-lmax-r5-non-run-validation.json",
    "phase-lmax-r5-inert-runtime-path-report.md",
    "phase-lmax-r5-operator-note.md",
    "phase-lmax-r6-static-integration-review.json",
    "phase-lmax-r6-static-wiring-decision.json",
    "phase-lmax-r6-gateway-default-safety-review.json",
    "phase-lmax-r6-test-coverage-summary.json",
    "phase-lmax-r6-future-r7-local-harness-plan.json",
    "phase-lmax-r6-decision-gate.json",
    "phase-lmax-r6-non-run-validation.json",
    "phase-lmax-r6-inert-runtime-integration-preflight-report.md",
    "phase-lmax-r6-operator-note.md",
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

$requiredR8 = @(
    "phase-lmax-r8-operator-approval-record.json",
    "phase-lmax-r8-preflight-gate.json",
    "phase-lmax-r8-temporary-runtime-activation-record.json",
    "phase-lmax-r8-approved-instrument-status-record.json",
    "phase-lmax-r8-sanitized-runtime-boundary-evidence.json",
    "phase-lmax-r8-forbidden-action-validation.json",
    "phase-lmax-r8-shutdown-revert-record.json",
    "phase-lmax-r8-post-attempt-non-mutation-validation.json",
    "phase-lmax-r8-rail-isolation-validation.json",
    "phase-lmax-r8-decision-gate.json",
    "phase-lmax-r8-temporary-readonly-runtime-report.md",
    "phase-lmax-r8-operator-note.md"
)

Write-Host "LMAX-R8 Temporary Read-Only Runtime Gate Validator"
Write-Host "This validator performs no external run, snapshot, replay, POST endpoint, socket, runtime activation, API/Worker startup, credential loading, or config mutation."

Assert-RequiredArtifacts $readiness $requiredPrior
Assert-RequiredArtifacts $readiness $requiredR8

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

$r1Gate = Read-Json (Join-Path $readiness "phase-lmax-r1-design-only-decision-gate.json")
$r2Gate = Read-Json (Join-Path $readiness "phase-lmax-r2-preflight-decision-gate.json")
$r3Gate = Read-Json (Join-Path $readiness "phase-lmax-r3-decision-gate.json")
$r4Gate = Read-Json (Join-Path $readiness "phase-lmax-r4-remediation-decision-gate.json")
$r5Gate = Read-Json (Join-Path $readiness "phase-lmax-r5-inert-implementation-decision-gate.json")
$r6Gate = Read-Json (Join-Path $readiness "phase-lmax-r6-decision-gate.json")
$r7Gate = Read-Json (Join-Path $readiness "phase-lmax-r7-decision-gate.json")
$r8Approval = Read-Json (Join-Path $readiness "phase-lmax-r8-operator-approval-record.json")
$r8Preflight = Read-Json (Join-Path $readiness "phase-lmax-r8-preflight-gate.json")
$r8Activation = Read-Json (Join-Path $readiness "phase-lmax-r8-temporary-runtime-activation-record.json")
$r8InstrumentStatus = Read-Json (Join-Path $readiness "phase-lmax-r8-approved-instrument-status-record.json")
$r8Boundary = Read-Json (Join-Path $readiness "phase-lmax-r8-sanitized-runtime-boundary-evidence.json")
$r8Forbidden = Read-Json (Join-Path $readiness "phase-lmax-r8-forbidden-action-validation.json")
$r8Shutdown = Read-Json (Join-Path $readiness "phase-lmax-r8-shutdown-revert-record.json")
$r8NonMutation = Read-Json (Join-Path $readiness "phase-lmax-r8-post-attempt-non-mutation-validation.json")
$r8Isolation = Read-Json (Join-Path $readiness "phase-lmax-r8-rail-isolation-validation.json")
$r8Gate = Read-Json (Join-Path $readiness "phase-lmax-r8-decision-gate.json")
$t7Gate = Read-Json $t7GatePath
$phase7nGate = Read-Json $phase7nGatePath

Assert-Equal $r1Gate.finalDecision "LMAX_R1_RUNTIME_ENABLEMENT_DESIGN_REVIEW_COMPLETE_NO_ENABLEMENT" "R1 decision"
Assert-Equal $r2Gate.finalDecision "LMAX_R2_READONLY_RUNTIME_PREFLIGHT_READY_NO_ACTIVATION" "R2 decision"
Assert-Equal $r3Gate.finalDecision "LMAX_R3_FAIL_PREFLIGHT_ABORTED" "R3 decision"
Assert-Equal $r4Gate.finalDecision "LMAX_R4_PREFLIGHT_ABORT_REMEDIATION_READY_NO_ACTIVATION" "R4 decision"
Assert-Equal $r5Gate.finalDecision "LMAX_R5_INERT_READONLY_RUNTIME_PATH_IMPLEMENTED_NO_ACTIVATION" "R5 decision"
Assert-Equal $r6Gate.finalDecision "LMAX_R6_INERT_RUNTIME_INTEGRATION_PREFLIGHT_COMPLETE_NO_ACTIVATION" "R6 decision"
Assert-Equal $r7Gate.finalDecision "LMAX_R7_LOCAL_RUNTIME_GATE_HARNESS_READY_NO_ACTIVATION" "R7 decision"
Assert-Equal $t7Gate.finalDecision "USDJPY_T7_FINAL_READINESS_ARCHIVE_CLOSED_WITH_CAVEAT" "USDJPY T7 decision"
Assert-Equal $phase7nGate.finalDecision "PASS_FINAL_LMAX_READONLY_RUNTIME_EVIDENCE_ARCHIVE_CLOSED" "Phase 7N decision"

$expectedPhrase = "I, Philippe, explicitly approve Phase LMAX-R8 for one temporary Demo read-only runtime market-data activation attempt using the local-only R7 gate harness output for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority."

Assert-Equal $r8Approval.phase "LMAX-R8" "R8 approval phase"
Assert-Equal $r8Approval.operator "Philippe" "R8 operator"
Assert-Equal $r8Approval.approvalPhrase $expectedPhrase "R8 approval phrase"
Assert-True $r8Approval.approvalPhraseExact "R8 approval exact"
Assert-Equal $r8Approval.environment "Demo/read-only" "Approval environment"

foreach ($symbol in @("GBPUSD", "EURGBP", "AUDUSD", "USDJPY")) {
    if (-not (@($r8Approval.approvedInstruments | Where-Object { $_.symbol -eq $symbol }).Count -eq 1)) {
        throw "R8 approval missing approved instrument: $symbol"
    }
    if (-not (@($r8InstrumentStatus.instruments | Where-Object { $_.symbol -eq $symbol }).Count -eq 1)) {
        throw "R8 instrument status missing approved instrument: $symbol"
    }
}

$usdJpyApproval = @($r8Approval.approvedInstruments | Where-Object { $_.symbol -eq "USDJPY" })[0]
Assert-Equal $usdJpyApproval.securityId "4004" "Approval USDJPY SecurityID"
Assert-Equal $usdJpyApproval.securityIdSource "8" "Approval USDJPY SecurityIDSource"
Assert-Equal $usdJpyApproval.caveat "prior failed-safe root cause remains unproven" "Approval USDJPY caveat"

$usdJpyStatus = @($r8InstrumentStatus.instruments | Where-Object { $_.symbol -eq "USDJPY" })[0]
Assert-Equal $usdJpyStatus.securityId "4004" "USDJPY SecurityID"
Assert-Equal $usdJpyStatus.securityIdSource "8" "USDJPY SecurityIDSource"
Assert-Equal $usdJpyStatus.caveat "prior failed-safe root cause remains unproven" "USDJPY caveat"
Assert-False $usdJpyStatus.attempted "USDJPY attempted in R8"

Assert-True $r8Preflight.r7DecisionConfirmed "Preflight R7 decision"
Assert-Equal $r8Preflight.r7Decision "LMAX_R7_LOCAL_RUNTIME_GATE_HARNESS_READY_NO_ACTIVATION" "Preflight R7 decision value"
Assert-True $r8Preflight.r7HarnessOutputUsed "Preflight used R7 harness output"
Assert-True $r8Preflight.operatorApprovalExact "Preflight approval exact"
Assert-True $r8Preflight.approvedInstrumentListExact "Preflight instrument list exact"
Assert-True $r8Preflight.usdJpyCaveatPresent "Preflight USDJPY caveat"
Assert-True $r8Preflight.environmentDemoReadOnly "Preflight demo/read-only"
Assert-False $r8Preflight.productionAccount "Preflight production account"
Assert-False $r8Preflight.allowOrderSubmission "Preflight AllowOrderSubmission"
Assert-False $r8Preflight.allowLiveTrading "Preflight AllowLiveTrading"
Assert-False $r8Preflight.isTradingEnabled "Preflight IsTradingEnabled"
Assert-False $r8Preflight.scheduler "Preflight scheduler"
Assert-False $r8Preflight.polling "Preflight polling"
Assert-False $r8Preflight.replay "Preflight replay"
Assert-False $r8Preflight.shadowReplay "Preflight shadow replay"
Assert-False $r8Preflight.tradingMutation "Preflight trading mutation"
Assert-False $r8Preflight.persistentRuntimeEnablement "Preflight persistent runtime enablement"
Assert-False $r8Preflight.defaultGatewayRegistrationChange "Preflight default gateway registration change"
Assert-True $r8Preflight.outputSanitizationRequired "Preflight output sanitization"
Assert-True $r8Preflight.shutdownRevertPlanPresent "Preflight shutdown/revert"
Assert-False $r8Preflight.nonApprovedInstrumentConfigured "Preflight non-approved instrument"
Assert-False $r8Preflight.permanentRuntimeEnablementPlanned "Preflight permanent runtime"
Assert-False $r8Preflight.defaultGatewayConfigChangePlanned "Preflight default config change"
Assert-True $r8Preflight.outputPathUnderReadinessRuntimeEnablement "Preflight output path"
Assert-False $r8Preflight.temporaryReadOnlyRuntimeAdapterAvailable "Preflight temporary adapter available"
Assert-False $r8Preflight.temporaryReadOnlyRuntimeAdapterUsesR7HarnessOutput "Preflight temporary adapter uses R7"
Assert-False $r8Preflight.preflightPassed "Preflight passed"
Assert-True $r8Preflight.preflightAborted "Preflight aborted"
Assert-True $r8Preflight.abortBeforeActivation "Preflight abort before activation"
Assert-Equal $r8Preflight.abortClassification "PreflightImplementationGapNotLmaxConnectivityFailure" "Preflight abort classification"
Assert-False $r8Preflight.connectionAttempted "Preflight connection attempted"
Assert-False $r8Preflight.runtimeActivationAttempted "Preflight runtime activation attempted"

Assert-Equal $r8Activation.phase "LMAX-R8" "Activation record phase"
Assert-False $r8Activation.activationAttempted "Activation attempted"
Assert-False $r8Activation.activationExecuted "Temporary runtime activation executed"
Assert-Equal $r8Activation.attemptCount 0 "Activation attempt count"
Assert-Equal $r8Activation.retryCount 0 "Activation retry count"
Assert-False $r8Activation.externalRunExecuted "Activation external run"
Assert-False $r8Activation.runtimePoweredUp "Activation runtime powered up"
Assert-False $r8Activation.runtimeEnablementExecuted "Activation runtime enablement executed"
Assert-False $r8Activation.runtimeEnablementPersisted "Activation persisted"

foreach ($boundary in @("tcpBoundaryStatus", "tlsBoundaryStatus", "fixLogonBoundaryStatus", "marketDataRequestBoundaryStatus", "instrumentMarketDataStatus")) {
    Assert-Equal $r8Boundary.$boundary "NotAttemptedPreflightAborted" "Boundary $boundary"
}
Assert-True $r8Boundary.outputSanitized "Boundary output sanitized"
Assert-False $r8Boundary.sensitiveInformationDetected "Boundary sensitive info"

Assert-True $r8Forbidden.passed "Forbidden validation passed"
foreach ($property in @(
    "orderSubmissionExecuted",
    "orderStatusRequestSent",
    "orderCancelRequestSent",
    "tradeCaptureRequestSent",
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
    "nonApprovedInstrumentTouched",
    "defaultGatewayRegistrationChanged",
    "runtimeEnablementPersisted",
    "retryExecuted",
    "batchExecuted",
    "loopExecuted"
)) {
    Assert-False $r8Forbidden.$property "Forbidden validation $property"
}

Assert-True $r8Shutdown.shutdownOrRevertCompleted "Shutdown/revert complete"
Assert-True $r8Shutdown.containmentCompleted "No-activation containment"
Assert-False $r8Shutdown.activationAttempted "Shutdown activation attempted"
Assert-True $r8Shutdown.defaultGatewayRegistrationUnchanged "Shutdown default gateway"
Assert-False $r8Shutdown.runtimeEnablementPersisted "Shutdown persistent runtime"

foreach ($property in @(
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
    "credentialsLoaded",
    "archivesModified"
)) {
    Assert-False $r8NonMutation.$property "Non-mutation validation $property"
}
Assert-Equal $r8NonMutation.attemptCount 0 "Non-mutation attempt count"
Assert-Equal $r8NonMutation.retryCount 0 "Non-mutation retry count"
Assert-False $r8NonMutation.batchMode "Non-mutation batch"
Assert-False $r8NonMutation.loopMode "Non-mutation loop"
Assert-False $r8NonMutation.productionAccountUsed "Non-mutation production"
Assert-True $r8NonMutation.approvedInstrumentsOnly "Non-mutation approved instruments"
Assert-True $r8NonMutation.usdJpyCaveatPreserved "Non-mutation USDJPY caveat"
Assert-True $r8NonMutation.outputSanitized "Non-mutation output sanitized"
Assert-True $r8NonMutation.shutdownOrRevertCompleted "Non-mutation shutdown/revert"
Assert-Equal $r8NonMutation.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Non-mutation gateway mode"

Assert-True $r8Isolation.passed "Rail isolation passed"
Assert-True $r8Isolation.usdJpyCaveatPreserved "Rail isolation USDJPY caveat"
Assert-False $r8Isolation.evidenceArchivesModified "Rail isolation archives"
Assert-False $r8Isolation.validatedRailsModified "Rail isolation validated rails"
Assert-False $r8Isolation.phase7ArchiveModified "Rail isolation Phase 7"
Assert-False $r8Isolation.usdJpyT1T7ArtifactsModified "Rail isolation USDJPY T1-T7"
foreach ($property in @("r1ArtifactsModified", "r2ArtifactsModified", "r3ArtifactsModified", "r4ArtifactsModified", "r5ArtifactsModified", "r6ArtifactsModified", "r7ArtifactsModified")) {
    Assert-False $r8Isolation.$property "Rail isolation $property"
}
Assert-False $r8Isolation.nonApprovedInstrumentTouched "Rail isolation non-approved instrument"

$allowedDecisions = @(
    "LMAX_R8_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED",
    "LMAX_R8_FAIL_PREFLIGHT_ABORTED",
    "LMAX_R8_FAIL_RUNTIME_ACTIVATION_BOUNDARY",
    "LMAX_R8_FAIL_SESSION_OR_MARKETDATA_BOUNDARY",
    "LMAX_R8_FAIL_SAFETY_CONSTRAINT",
    "LMAX_R8_INCONCLUSIVE_SANITIZED_EVIDENCE"
)
Assert-Equal $r8Gate.phase "LMAX-R8" "Decision gate phase"
Assert-In $r8Gate.finalDecision $allowedDecisions "R8 final decision"
Assert-Equal $r8Gate.finalDecision "LMAX_R8_FAIL_PREFLIGHT_ABORTED" "R8 expected safe abort decision"
Assert-Equal $r8Gate.resultClassification "LMAX_R8_FAIL_PREFLIGHT_ABORTED" "R8 result classification"
Assert-True $r8Gate.operatorApprovalExact "Decision approval exact"
Assert-True $r8Gate.r7DecisionConfirmed "Decision R7 confirmed"
Assert-True $r8Gate.r7HarnessOutputUsed "Decision R7 harness"
Assert-True $r8Gate.preflightCompleted "Decision preflight completed"
Assert-False $r8Gate.preflightPassed "Decision preflight passed"
Assert-True $r8Gate.preflightAborted "Decision preflight aborted"
Assert-False $r8Gate.activationAttemptExecuted "Decision activation executed"
Assert-Equal $r8Gate.attemptCount 0 "Decision attempt count"
Assert-Equal $r8Gate.retryCount 0 "Decision retry count"
Assert-False $r8Gate.batchMode "Decision batch"
Assert-False $r8Gate.loopMode "Decision loop"
Assert-False $r8Gate.productionAccountUsed "Decision production"
Assert-True $r8Gate.approvedInstrumentsOnly "Decision approved instruments"
Assert-True $r8Gate.usdJpyCaveatPreserved "Decision USDJPY caveat"
Assert-True $r8Gate.shutdownOrRevertCompleted "Decision shutdown/revert"
Assert-Equal $r8Gate.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "Decision gateway mode"
Assert-False $r8Gate.temporaryReadOnlyMarketDataAdapterUsed "Decision temporary adapter"
Assert-False $r8Gate.orderGatewayRegistered "Decision order gateway"
Assert-False $r8Gate.tradingGatewayRegistered "Decision trading gateway"

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
    "runtimeEnablementPersisted",
    "tradingEnablementExecuted",
    "schedulerEnablementExecuted",
    "orderPathEnablementExecuted",
    "defaultGatewayRegistrationChanged",
    "liveConnectionScriptCreated",
    "credentialsPrinted",
    "credentialsStored",
    "credentialsLoaded",
    "archivesModified"
)) {
    Assert-False $r8Gate.$property "Decision gate $property"
}

$appsettingsPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\appsettings.json"
$appsettings = Read-Json $appsettingsPath
Assert-False $appsettings.Safety.AllowExternalConnections "Appsettings Safety.AllowExternalConnections"
Assert-False $appsettings.Safety.AllowLiveTrading "Appsettings Safety.AllowLiveTrading"
Assert-True $appsettings.Safety.RequireFakeExecutionGateway "Appsettings Safety.RequireFakeExecutionGateway"
Assert-False $appsettings.LmaxReadOnlyRuntime.Enabled "Appsettings LmaxReadOnlyRuntime.Enabled"
Assert-Equal $appsettings.LmaxReadOnlyRuntime.ImplementationMode "DesignOnly" "Appsettings implementation mode"
Assert-False $appsettings.LmaxReadOnlyRuntime.AllowExternalConnections "Appsettings runtime AllowExternalConnections"
Assert-False $appsettings.LmaxReadOnlyRuntime.AllowCredentialUse "Appsettings runtime AllowCredentialUse"
Assert-True $appsettings.LmaxReadOnlyRuntime.ReadOnly "Appsettings runtime ReadOnly"
Assert-False $appsettings.LmaxReadOnlyRuntime.AllowOrderSubmission "Appsettings runtime AllowOrderSubmission"
Assert-False $appsettings.LmaxReadOnlyRuntime.PersistToTradingTables "Appsettings runtime PersistToTradingTables"
Assert-False $appsettings.LmaxReadOnlyRuntime.SubmitToShadowReplay "Appsettings runtime SubmitToShadowReplay"
Assert-False $appsettings.LmaxReadOnlyRuntime.SchedulerEnabled "Appsettings runtime SchedulerEnabled"
Assert-True $appsettings.LmaxReadOnlyRuntime.DryRun "Appsettings runtime DryRun"

$programPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Api\Program.cs"
$workerProgramPath = Join-Path $RepoRoot "src\QQ.Production.Intraday.Worker\Program.cs"
$programText = Get-Content -LiteralPath $programPath -Raw
$workerText = Get-Content -LiteralPath $workerProgramPath -Raw
if ($programText -notmatch 'AddSingleton<IVenueExecutionGateway,\s*FakeLmaxGateway>') {
    throw "API Program.cs no longer registers FakeLmaxGateway as IVenueExecutionGateway."
}
if ($workerText -notmatch 'AddSingleton<IVenueExecutionGateway,\s*FakeLmaxGateway>') {
    throw "Worker Program.cs no longer registers FakeLmaxGateway as IVenueExecutionGateway."
}
if ($programText -match 'AddHostedService<.*Lmax.*Runtime|AddHostedService<.*ReadOnly.*Runtime') {
    throw "API Program.cs appears to register an LMAX runtime hosted service."
}
if ($workerText -match 'AddHostedService<.*Lmax.*Runtime|AddHostedService<.*ReadOnly.*Runtime') {
    throw "Worker Program.cs appears to register an LMAX runtime hosted service."
}
if ($programText -match 'TemporaryReadOnly.*(Connect|Start|Run)|SocketPrototype.*Run|CredentialResolver') {
    throw "API Program.cs appears to wire a live temporary read-only runtime path."
}

$reportPath = Join-Path $readiness "phase-lmax-r8-temporary-readonly-runtime-report.md"
$reportText = Get-Content -LiteralPath $reportPath -Raw
foreach ($heading in @(
    "Executive summary",
    "Operator approval",
    "Scope and constraints",
    "R7 harness result used by R8",
    "Preflight result",
    "Temporary runtime activation summary",
    "Approved instrument status",
    "USDJPY caveat preservation",
    "Runtime boundary evidence",
    "Forbidden-action validation",
    "Shutdown/revert evidence",
    "Post-attempt non-mutation validation",
    "Rail/archive isolation validation",
    "Decision",
    "Recommended next phase"
)) {
    if ($reportText -notmatch [regex]::Escape($heading)) {
        throw "R8 report missing required section: $heading"
    }
}

$validation = [ordered]@{
    phase = "LMAX-R8"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    validator = "scripts/check-lmax-r8-temporary-readonly-runtime-gate.ps1"
    requiredArtifactsPresent = $true
    priorArtifactsPresent = $true
    r7DecisionConfirmed = $true
    operatorApprovalExact = $true
    preflightPassed = $false
    preflightAbortedSafely = $true
    activationAttemptExecuted = $false
    attemptCount = 0
    retryCount = 0
    batchMode = $false
    loopMode = $false
    approvedInstrumentsOnly = $true
    usdJpyCaveatPreserved = $true
    outputSanitized = $true
    shutdownOrRevertCompleted = $true
    apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
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
    runtimeEnablementPersisted = $false
    defaultGatewayRegistrationChanged = $false
    liveConnectionScriptCreated = $false
    orderGatewayRegistered = $false
    tradingGatewayRegistered = $false
    noSensitiveContent = $true
    resultClassification = $r8Gate.resultClassification
    recommendedNextPhase = $r8Gate.recommendedNextPhase
    finalDecision = $r8Gate.finalDecision
}

$validationPath = Join-Path $readiness "phase-lmax-r8-gate-validation.json"
$validation | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $validationPath -Encoding UTF8

Write-Host "PASS: $($r8Gate.finalDecision)"
