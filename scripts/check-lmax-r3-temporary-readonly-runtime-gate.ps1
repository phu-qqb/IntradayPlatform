param(
    [string]$BaseDir = "artifacts/readiness/lmax-runtime-enablement"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$expectedR1Decision = "LMAX_R1_RUNTIME_ENABLEMENT_DESIGN_REVIEW_COMPLETE_NO_ENABLEMENT"
$expectedR2Decision = "LMAX_R2_READONLY_RUNTIME_PREFLIGHT_READY_NO_ACTIVATION"
$expectedApprovalPhrase = "I, Philippe, explicitly approve Phase LMAX-R3 for one temporary Demo read-only runtime market-data activation attempt for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority."
$allowedDecisions = @(
    "LMAX_R3_PASS_TEMPORARY_READONLY_RUNTIME_ACTIVATION_SANITIZED",
    "LMAX_R3_FAIL_PREFLIGHT_ABORTED",
    "LMAX_R3_FAIL_RUNTIME_ACTIVATION_BOUNDARY",
    "LMAX_R3_FAIL_SESSION_OR_MARKETDATA_BOUNDARY",
    "LMAX_R3_FAIL_SAFETY_CONSTRAINT",
    "LMAX_R3_INCONCLUSIVE_SANITIZED_EVIDENCE"
)
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|BEGIN\s+PRIVATE\s+KEY)'

function Add-Result([string]$Category, [string]$Check, [string]$Status, [string]$Detail) {
    $script:results += [ordered]@{ category = $Category; check = $Check; status = $Status; detail = $Detail }
    Write-Host ("{0}: {1} / {2} - {3}" -f $Status, $Category, $Check, $Detail)
}
function Resolve-RepoPath([string]$PathValue) {
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}
function Read-TextSafe([string]$PathValue, [string]$Label) {
    $resolved = Resolve-RepoPath $PathValue
    if (-not (Test-Path -LiteralPath $resolved)) {
        Add-Result $Label "File exists" "FAIL" "Missing $resolved"
        return $null
    }
    Add-Result $Label "File exists" "PASS" $resolved
    $raw = Get-Content -LiteralPath $resolved -Raw
    $safe = $raw `
        -replace 'credential|Credential|secret|Secret','SAFE_METADATA' `
        -replace 'raw FIX|rawFix|FIX Logon','SAFE_FIX_METADATA' `
        -replace 'LMAX_DEMO_FIX_USERNAME|LMAX_DEMO_FIX_PASSWORD|LMAX_DEMO_SENDER_COMP_ID|LMAX_DEMO_TARGET_COMP_ID','SAFE_ENV_LABEL'
    if ($safe -match $sensitivePattern) {
        Add-Result $Label "No sensitive content" "FAIL" "Credential-shaped content found."
    } else {
        Add-Result $Label "No sensitive content" "PASS" "No credential-shaped content."
    }
    return $raw
}
function Assert-False([object]$Value, [string]$Category, [string]$Check) {
    if (-not [bool]$Value) { Add-Result $Category $Check "PASS" "false." } else { Add-Result $Category $Check "FAIL" "Expected false." }
}
function Assert-True([object]$Value, [string]$Category, [string]$Check) {
    if ([bool]$Value) { Add-Result $Category $Check "PASS" "true." } else { Add-Result $Category $Check "FAIL" "Expected true." }
}
function Assert-Equals([object]$Actual, [string]$Expected, [string]$Category, [string]$Check) {
    if ([string]$Actual -eq $Expected) { Add-Result $Category $Check "PASS" $Expected } else { Add-Result $Category $Check "FAIL" "Expected '$Expected' but found '$Actual'." }
}

Write-Host "LMAX-R3 Temporary Read-Only Runtime Gate Validator"
Write-Host "This validator performs no external run, snapshot, replay, POST endpoint, socket, runtime activation, or config mutation."

$requiredR3 = [ordered]@{
    ApprovalRecord = "phase-lmax-r3-operator-approval-record.json"
    PreflightGate = "phase-lmax-r3-preflight-gate.json"
    ActivationRecord = "phase-lmax-r3-temporary-runtime-activation-record.json"
    InstrumentStatus = "phase-lmax-r3-approved-instrument-status-record.json"
    BoundaryEvidence = "phase-lmax-r3-sanitized-runtime-boundary-evidence.json"
    ForbiddenActionValidation = "phase-lmax-r3-forbidden-action-validation.json"
    ShutdownRevertRecord = "phase-lmax-r3-shutdown-revert-record.json"
    NonMutationValidation = "phase-lmax-r3-post-attempt-non-mutation-validation.json"
    RailIsolationValidation = "phase-lmax-r3-rail-isolation-validation.json"
    DecisionGate = "phase-lmax-r3-decision-gate.json"
    Report = "phase-lmax-r3-temporary-readonly-runtime-report.md"
    OperatorNote = "phase-lmax-r3-operator-note.md"
}

$raw = @{}
foreach ($key in $requiredR3.Keys) { $raw[$key] = Read-TextSafe (Join-Path $BaseDir $requiredR3[$key]) $key }

$r1Gate = Read-TextSafe (Join-Path $BaseDir "phase-lmax-r1-design-only-decision-gate.json") "R1DecisionGate" | ConvertFrom-Json
$r2Gate = Read-TextSafe (Join-Path $BaseDir "phase-lmax-r2-preflight-decision-gate.json") "R2DecisionGate" | ConvertFrom-Json
Assert-Equals $r1Gate.finalDecision $expectedR1Decision "R1DecisionGate" "Final decision"
Assert-Equals $r2Gate.finalDecision $expectedR2Decision "R2DecisionGate" "Final decision"

$approval = $raw.ApprovalRecord | ConvertFrom-Json
Assert-True $approval.approvalReceived "ApprovalRecord" "Approval received"
Assert-Equals $approval.approvalPhrase $expectedApprovalPhrase "ApprovalRecord" "Exact approval phrase"
Assert-True $approval.usdJpyCaveatPreserved "ApprovalRecord" "USDJPY caveat preserved"

$preflight = $raw.PreflightGate | ConvertFrom-Json
Assert-True $preflight.preflightCompleted "PreflightGate" "Preflight completed"
if ([bool]$preflight.preflightPassed -or [bool]$preflight.preflightAborted) {
    Add-Result "PreflightGate" "Passed or safely aborted" "PASS" "preflightPassed=$($preflight.preflightPassed), preflightAborted=$($preflight.preflightAborted)"
} else {
    Add-Result "PreflightGate" "Passed or safely aborted" "FAIL" "Preflight neither passed nor aborted."
}
Assert-True $preflight.abortBeforeActivation "PreflightGate" "Abort before activation"
Assert-False $preflight.temporaryReadOnlyRuntimeActivationPathAvailable "PreflightGate" "Temporary runtime path available"

$activation = $raw.ActivationRecord | ConvertFrom-Json
if ([int]$activation.attemptCount -le 1) { Add-Result "ActivationRecord" "At most one attempt" "PASS" "attemptCount=$($activation.attemptCount)" } else { Add-Result "ActivationRecord" "At most one attempt" "FAIL" "attemptCount=$($activation.attemptCount)" }
Assert-False $activation.activationAttempted "ActivationRecord" "Activation attempted"
Assert-False $activation.externalRunExecuted "ActivationRecord" "External run executed"
Assert-False $activation.runtimePoweredUp "ActivationRecord" "Runtime powered up"
Assert-False $activation.runtimeEnablementPersisted "ActivationRecord" "Runtime enablement persisted"
Assert-False $activation.orderGatewayRegistered "ActivationRecord" "Order gateway registered"
Assert-False $activation.tradingGatewayRegistered "ActivationRecord" "Trading gateway registered"
Assert-Equals $activation.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "ActivationRecord" "API/Worker gateway mode"
Assert-True $activation.shutdownOrRevertCompleted "ActivationRecord" "Shutdown/revert completed"

$instrument = $raw.InstrumentStatus | ConvertFrom-Json
Assert-True $instrument.approvedInstrumentsOnly "InstrumentStatus" "Approved instruments only"
Assert-False $instrument.nonApprovedInstrumentTouched "InstrumentStatus" "Non-approved instrument touched"
Assert-True $instrument.usdJpyCaveatPreserved "InstrumentStatus" "USDJPY caveat preserved"
if (($instrument.instrumentStatuses.instrument -contains "GBPUSD") -and ($instrument.instrumentStatuses.instrument -contains "EURGBP") -and ($instrument.instrumentStatuses.instrument -contains "AUDUSD") -and ($instrument.instrumentStatuses.instrument -contains "USDJPY")) {
    Add-Result "InstrumentStatus" "Approved instrument list" "PASS" "All four approved instruments present."
} else {
    Add-Result "InstrumentStatus" "Approved instrument list" "FAIL" "Missing approved instrument."
}

$forbidden = $raw.ForbiddenActionValidation | ConvertFrom-Json
foreach ($flag in @("ordersSubmitted", "orderStatusRequestsSent", "orderCancelRequestsSent", "tradeCaptureRequestsSent", "orderPathEnabled", "allowOrderSubmission", "allowLiveTrading", "isTradingEnabled", "schedulerStarted", "pollingStarted", "replayExecuted", "shadowReplaySubmitted", "tradingStateMutated", "productionAccountUsed", "nonApprovedInstrumentTouched", "defaultGatewayRegistrationChanged", "runtimeEnablementPersisted", "retryExecuted", "batchExecuted", "loopExecuted")) {
    Assert-False $forbidden.$flag "ForbiddenActionValidation" $flag
}

$nonMutation = $raw.NonMutationValidation | ConvertFrom-Json
foreach ($flag in @("productionAccountUsed", "orderSubmissionExecuted", "orderPathEnabled", "allowOrderSubmission", "allowLiveTrading", "isTradingEnabled", "schedulerStarted", "pollingStarted", "replayExecuted", "shadowReplaySubmitted", "tradingStateMutated", "defaultGatewayRegistrationChanged", "runtimeEnablementPersisted", "credentialsPrinted", "credentialsStored", "archivesModified")) {
    Assert-False $nonMutation.$flag "NonMutationValidation" $flag
}
Assert-True $nonMutation.approvedInstrumentsOnly "NonMutationValidation" "Approved instruments only"
Assert-True $nonMutation.usdJpyCaveatPreserved "NonMutationValidation" "USDJPY caveat preserved"
Assert-True $nonMutation.shutdownOrRevertCompleted "NonMutationValidation" "Shutdown/revert completed"

$isolation = $raw.RailIsolationValidation | ConvertFrom-Json
foreach ($flag in @("evidenceArchivesModified", "phase7ArchiveModified", "gbpusdArchiveModified", "eurgbpArchiveModified", "audusdArchiveModified", "usdJpyT1T7ArtifactsModified", "r1ArtifactsModified", "r2ArtifactsModified", "nonApprovedInstrumentTouched")) {
    Assert-False $isolation.$flag "RailIsolationValidation" $flag
}
Assert-True $isolation.usdJpyCaveatPreserved "RailIsolationValidation" "USDJPY caveat preserved"

$shutdown = $raw.ShutdownRevertRecord | ConvertFrom-Json
Assert-True $shutdown.shutdownOrRevertCompleted "ShutdownRevertRecord" "Shutdown/revert completed"
Assert-True $shutdown.defaultConfigRestoredOrUnchanged "ShutdownRevertRecord" "Default config restored/unchanged"
Assert-Equals $shutdown.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "ShutdownRevertRecord" "API/Worker gateway mode"

$gate = $raw.DecisionGate | ConvertFrom-Json
Assert-Equals $gate.phase "LMAX-R3" "DecisionGate" "Phase"
if ($allowedDecisions -contains [string]$gate.finalDecision) { Add-Result "DecisionGate" "Allowed final decision" "PASS" $gate.finalDecision } else { Add-Result "DecisionGate" "Allowed final decision" "FAIL" "Unexpected finalDecision $($gate.finalDecision)" }
Assert-Equals $gate.finalDecision $gate.resultClassification "DecisionGate" "Result classification matches final decision"
if ([int]$gate.attemptCount -le 1) { Add-Result "DecisionGate" "At most one attempt" "PASS" "attemptCount=$($gate.attemptCount)" } else { Add-Result "DecisionGate" "At most one attempt" "FAIL" "attemptCount=$($gate.attemptCount)" }
foreach ($flag in @("replayExecuted", "postEndpointInvoked", "orderSubmissionExecuted", "tradingStateMutated", "schedulerStarted", "pollingStarted", "shadowReplaySubmitted", "apiWorkerStarted", "retryExecuted", "batchExecuted", "loopExecuted", "runtimeEnablementPersisted", "tradingEnablementExecuted", "schedulerEnablementExecuted", "orderPathEnablementExecuted", "defaultGatewayRegistrationChanged", "liveConnectionScriptCreated", "orderGatewayRegistered", "tradingGatewayRegistered", "credentialsPrinted", "credentialsStored", "archivesModified")) {
    Assert-False $gate.$flag "DecisionGate" $flag
}
Assert-Equals $gate.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "DecisionGate" "API/Worker gateway mode"
Assert-True $gate.outputSanitized "DecisionGate" "Output sanitized"
Assert-True $gate.shutdownOrRevertCompleted "DecisionGate" "Shutdown/revert completed"

if ($null -ne $raw.Report) {
    foreach ($marker in @("Executive summary", "Operator approval", "Scope and constraints", "Preflight result", "Temporary runtime activation summary", "Approved instrument status", "USDJPY caveat preservation", "Runtime boundary evidence", "Forbidden-action validation", "Shutdown/revert evidence", "Post-attempt non-mutation validation", "Rail/archive isolation validation", "Decision", "Recommended next phase")) {
        if ($raw.Report.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "Report" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "Report" "Marker: $marker" "FAIL" "Marker missing." }
    }
}

$apiProgramPath = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgramPath = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"
$appSettingsPath = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/appsettings.json"
$apiProgram = Get-Content -Raw -LiteralPath $apiProgramPath
$workerProgram = Get-Content -Raw -LiteralPath $workerProgramPath
$appSettings = Get-Content -Raw -LiteralPath $appSettingsPath | ConvertFrom-Json
if ($apiProgram -match "AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>" -and $workerProgram -match "AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>" -and -not ($apiProgram -match "RealLmaxGateway|LmaxVenueGatewaySkeleton" -or $workerProgram -match "RealLmaxGateway|LmaxVenueGatewaySkeleton")) {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No real gateway registration found."
} else {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" "Unexpected gateway registration marker."
}
Assert-False $appSettings.Safety.AllowExternalConnections "AppSettings" "Safety:AllowExternalConnections"
Assert-False $appSettings.Safety.AllowLiveTrading "AppSettings" "Safety:AllowLiveTrading"
Assert-True $appSettings.Safety.RequireFakeExecutionGateway "AppSettings" "Safety:RequireFakeExecutionGateway"
Assert-False $appSettings.LmaxReadOnlyRuntime.Enabled "AppSettings" "LmaxReadOnlyRuntime:Enabled"
Assert-Equals $appSettings.LmaxReadOnlyRuntime.ImplementationMode "DesignOnly" "AppSettings" "LmaxReadOnlyRuntime:ImplementationMode"
Assert-False $appSettings.LmaxReadOnlyRuntime.AllowExternalConnections "AppSettings" "LmaxReadOnlyRuntime:AllowExternalConnections"
Assert-False $appSettings.LmaxReadOnlyRuntime.AllowOrderSubmission "AppSettings" "LmaxReadOnlyRuntime:AllowOrderSubmission"
Assert-False $appSettings.LmaxReadOnlyRuntime.SchedulerEnabled "AppSettings" "LmaxReadOnlyRuntime:SchedulerEnabled"
Assert-False $appSettings.LmaxReadOnlyRuntime.SubmitToShadowReplay "AppSettings" "LmaxReadOnlyRuntime:SubmitToShadowReplay"

$failed = @($results | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } else { [string]$gate.finalDecision }
$outPath = Resolve-RepoPath (Join-Path $BaseDir "phase-lmax-r3-gate-validation.json")
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "LMAX-R3"
    finalDecision = $decision
    resultClassification = [string]$gate.resultClassification
    externalRunExecuted = [bool]$gate.externalRunExecuted
    snapshotExecuted = [bool]$gate.snapshotExecuted
    replayExecuted = $false
    postEndpointInvoked = $false
    realSocketOpened = [bool]$gate.realSocketOpened
    tcpConnectionAttempted = [bool]$gate.tcpConnectionAttempted
    tlsHandshakeAttempted = [bool]$gate.tlsHandshakeAttempted
    fixLogonAttempted = [bool]$gate.fixLogonAttempted
    marketDataRequestSent = [bool]$gate.marketDataRequestSent
    orderSubmissionExecuted = $false
    tradingStateMutated = $false
    schedulerStarted = $false
    pollingStarted = $false
    shadowReplaySubmitted = $false
    apiWorkerStarted = $false
    runtimePoweredUp = [bool]$gate.runtimePoweredUp
    retryExecuted = $false
    batchExecuted = $false
    loopExecuted = $false
    runtimeEnablementExecuted = [bool]$gate.runtimeEnablementExecuted
    runtimeEnablementPersisted = $false
    tradingEnablementExecuted = $false
    schedulerEnablementExecuted = $false
    orderPathEnablementExecuted = $false
    defaultGatewayRegistrationChanged = $false
    liveConnectionScriptCreated = $false
    temporaryReadOnlyMarketDataAdapterUsed = [bool]$gate.temporaryReadOnlyMarketDataAdapterUsed
    orderGatewayRegistered = $false
    tradingGatewayRegistered = $false
    apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
    outputSanitized = $true
    archivesModified = $false
    noSensitiveContent = $true
    results = $results
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }
