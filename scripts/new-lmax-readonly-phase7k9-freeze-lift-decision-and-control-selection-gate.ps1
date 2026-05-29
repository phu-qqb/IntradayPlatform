param(
    [string]$Phase7K8GateFile = "artifacts/readiness/phase7k8-external-session-remediation-completion-gate.json",
    [string]$Phase7K8RecordFile = "artifacts/readiness/phase7k8-external-session-remediation-completion-record.json",
    [string]$Phase7K6FreezeGateFile = "artifacts/readiness/phase7k6-global-external-attempt-freeze-gate.json",
    [string]$Phase7K7PlanFile = "artifacts/readiness/phase7k7-external-session-remediation-plan.json",
    [string]$Phase7K7GateFile = "artifacts/readiness/phase7k7-external-session-remediation-gate.json",
    [string]$Phase7K5InterpretationFile = "artifacts/readiness/phase7k5-gbpusd-known-good-control-interpretation.json",
    [string]$EarlierGbpusdArtifactFile = "artifacts/lmax-readonly-runtime-additional-snapshot/gbpusd/lmax-readonly-gbpusd-demo-snapshot-result-20260511-103318.json",
    [string]$FailedGbpusdControlArtifactFile = "artifacts/lmax-readonly-runtime-additional-snapshot/gbpusd/lmax-readonly-gbpusd-demo-snapshot-result-20260511-192834.json",
    [string]$GbpusdFinalPreRunGateFile = "artifacts/lmax-readonly-runtime-securityid-planning/additional-final-prerun/lmax-readonly-additional-instrument-final-prerun-gate-GBPUSD-20260511-172337.json",
    [string]$OutputDirectory = "artifacts/readiness"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|raw\s*fix|BEGIN\s+PRIVATE\s+KEY)'

function Resolve-LocalPath([string]$PathValue) {
    if ([string]::IsNullOrWhiteSpace($PathValue)) { return "" }
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}

function Read-JsonArtifact([string]$PathValue, [string]$Label) {
    $resolved = Resolve-LocalPath $PathValue
    if (-not (Test-Path -LiteralPath $resolved)) {
        throw "$Label is missing: $resolved"
    }
    $raw = Get-Content -LiteralPath $resolved -Raw
    $safe = $raw -replace 'credential|Credential','SAFE_METADATA'
    if ($safe -match $sensitivePattern) {
        throw "$Label contains credential-shaped or raw FIX content."
    }
    return @{ path = $resolved; json = ($raw | ConvertFrom-Json) }
}

$phase7K8Gate = Read-JsonArtifact $Phase7K8GateFile "Phase 7K8 gate"
$phase7K8Record = Read-JsonArtifact $Phase7K8RecordFile "Phase 7K8 record"
$phase7K6Freeze = Read-JsonArtifact $Phase7K6FreezeGateFile "Phase 7K6 freeze gate"
$phase7K7Plan = Read-JsonArtifact $Phase7K7PlanFile "Phase 7K7 plan"
$phase7K7Gate = Read-JsonArtifact $Phase7K7GateFile "Phase 7K7 gate"
$phase7K5 = Read-JsonArtifact $Phase7K5InterpretationFile "Phase 7K5 interpretation"
$earlierGbpusd = Read-JsonArtifact $EarlierGbpusdArtifactFile "Earlier GBPUSD snapshot"
$failedGbpusd = Read-JsonArtifact $FailedGbpusdControlArtifactFile "Failed GBPUSD control"
$gbpusdFinalGate = Read-JsonArtifact $GbpusdFinalPreRunGateFile "GBPUSD final pre-run gate"

if ([string]$phase7K8Gate.json.phase -ne "7K8" -or [string]$phase7K8Gate.json.finalDecision -ne "PASS_REMEDIATION_COMPLETION_RECORDED" -or -not [bool]$phase7K8Gate.json.remediationCompletionRecorded -or -not [bool]$phase7K8Gate.json.freezeLiftCanBeConsidered) {
    throw "Phase 7K8 gate is not in the expected remediation-complete state."
}
if ([string]$phase7K8Record.json.phase -ne "7K8" -or -not [bool]$phase7K8Record.json.remediationCompletionRecorded) {
    throw "Phase 7K8 record is not complete."
}
if ([string]$phase7K6Freeze.json.phase -ne "7K6" -or -not [bool]$phase7K6Freeze.json.globalExternalAttemptFreeze) {
    throw "Phase 7K6 global freeze gate is not present."
}
if ([string]$phase7K7Plan.json.phase -ne "7K7" -or [string]$phase7K7Plan.json.finalDecision -ne "PASS_REMEDIATION_PLAN_RECORDED") {
    throw "Phase 7K7 remediation plan is not recorded."
}
if ([string]$phase7K7Gate.json.phase -ne "7K7" -or -not [bool]$phase7K7Gate.json.globalExternalAttemptFreezeRemains) {
    throw "Phase 7K7 remediation gate is not in frozen state."
}
if ([string]$phase7K5.json.phase -ne "7K5" -or [bool]$phase7K5.json.knownGoodControlRecovered -or -not [bool]$phase7K5.json.broaderEnvironmentSessionIssueStillSuspected) {
    throw "Phase 7K5 interpretation does not show failed known-good control with suspected session issue."
}
if ([string]$earlierGbpusd.json.symbol -ne "GBPUSD" -or [string]$earlierGbpusd.json.securityId -ne "4002" -or [string]$earlierGbpusd.json.status -ne "Completed" -or -not [bool]$earlierGbpusd.json.snapshotReceived -or [int]$earlierGbpusd.json.entryCount -le 0) {
    throw "Earlier GBPUSD snapshot is not the expected successful known-good snapshot."
}
if ([string]$failedGbpusd.json.symbol -ne "GBPUSD" -or [string]$failedGbpusd.json.securityId -ne "4002" -or [string]$failedGbpusd.json.status -ne "FailedSafeConnectionError" -or [bool]$failedGbpusd.json.logonAttempted -or [bool]$failedGbpusd.json.snapshotRequestAttempted) {
    throw "Failed GBPUSD control is not the expected pre-logon safe failure."
}
if ([string]$gbpusdFinalGate.json.symbol -ne "GBPUSD" -or [string]$gbpusdFinalGate.json.planningSecurityId -ne "4002" -or [string]$gbpusdFinalGate.json.finalDecision -ne "PASS") {
    throw "GBPUSD final pre-run gate is invalid."
}
if (-not [bool]$gbpusdFinalGate.json.oneInstrumentAtATime -or [bool]$gbpusdFinalGate.json.batchExecutionAllowed -or [bool]$gbpusdFinalGate.json.externalRunAuthorized -or [bool]$gbpusdFinalGate.json.canRunExternalSnapshot -or [bool]$gbpusdFinalGate.json.eligibleForManualSnapshotAttempt -or [bool]$gbpusdFinalGate.json.isApprovedForExternalRun) {
    throw "GBPUSD final pre-run gate safety flags are invalid."
}

$disallowedActions = @(
    "No external run in Phase 7K9.",
    "No GBPUSD control run in Phase 7K9.",
    "No EURGBP control run.",
    "No USDJPY retry.",
    "No AUDUSD retry.",
    "No next instrument run.",
    "No batch.",
    "No loop.",
    "No automatic retry.",
    "No wrapper relaxation.",
    "No SecurityID switch.",
    "No Tokyo 600x switch.",
    "No replay without MarketDataOnly evidence.",
    "No order path.",
    "No scheduler or polling.",
    "No runtime shadow replay submit.",
    "No trading-state mutation.",
    "No gateway registration."
)

$gate = [ordered]@{
    phase = "7K9"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    remediationCompletionRecorded = $true
    freezeLiftCanBeConsidered = $true
    freezeLiftDecision = "LiftForSingleFutureKnownGoodControlOnly"
    globalExternalAttemptFreezeLiftedForFutureSelection = $true
    directRunAuthorization = $false
    anyInstrumentExternalRunAllowed = $false
    externalAdditionalInstrumentAttemptsCurrentlyAllowed = $false
    selectedFutureAttemptInstrument = "GBPUSD"
    selectedFutureAttemptType = "KnownGoodControlSnapshot"
    selectedFutureAttemptReason = "GBPUSD is the best known-good control: it previously completed successfully and later failed during the pre-logon issue, so it can verify whether remediation restored the external/session layer in a later phase."
    selectedInstrumentSecurityId = "4002"
    selectedInstrumentSecurityIdSource = "8"
    selectedInstrumentEnvironment = "Demo"
    selectedInstrumentVenueProfile = "DemoLondon"
    selectedInstrumentRequestMode = "SnapshotPlusUpdates"
    selectedInstrumentSymbolEncodingMode = "SecurityIdOnly"
    selectedInstrumentMarketDepth = 1
    selectedInstrumentFinalPreRunGatePath = $gbpusdFinalGate.path
    selectedInstrumentFinalPreRunGateDecision = "PASS"
    priorSuccessfulGbpusdSnapshotPath = $earlierGbpusd.path
    failedGbpusdKnownGoodControlPath = $failedGbpusd.path
    exactlyOneFutureCandidateSelected = $true
    futureExternalRunCanBeConsidered = $true
    externalRunAttemptedInThisPhase = $false
    snapshotRunInThisPhase = $false
    replayRunInThisPhase = $false
    controlRunInThisPhase = $false
    gbpUsdRunInThisPhase = $false
    eurGbpRunInThisPhase = $false
    audusdRunInThisPhase = $false
    usdjpyRunInThisPhase = $false
    batchExecutionAllowed = $false
    automaticRetryRecommended = $false
    wrapperValidationWeakened = $false
    securityIdSwitchRecommended = $false
    tokyo600xSwitchRecommended = $false
    orderPathEnabled = $false
    schedulerOrPollingEnabled = $false
    runtimeShadowReplaySubmitEnabled = $false
    tradingMutationEnabled = $false
    gatewayRegistrationEnabled = $false
    requiredFutureOperatorFlags = @("-AllowExternalConnections", "-ConfirmDemoReadOnly", "human-provided -Reason")
    allowedNextPhase = "Phase 7K10 - GBPUSD Post-Remediation Known-Good Control Manual Market-Hours Snapshot Attempt"
    disallowedActions = $disallowedActions
    noSensitiveContent = $true
    apiWorkerGatewayMode = "FakeLmaxGateway"
    finalDecision = "PASS_FREEZE_LIFT_SELECTION_RECORDED"
}

$note = @"
# Phase 7K9 - Freeze Lift Decision and Known-Good Control Selection

Phase 7K8 remediation completion is recorded.

Phase 7K9 lifts the global freeze only far enough to select one future known-good control candidate. It does not authorize or run an external connection.

Selected future candidate: GBPUSD known-good control.

GBPUSD is selected because it previously succeeded, then later failed during the pre-logon issue. That makes it the cleanest control for checking whether remediation restored the external/session layer.

USDJPY and AUDUSD remain parked. EURGBP control is not selected. The next phase must be one instrument only and must use the Phase 7H wrapper plus a compatible final pre-run gate with explicit operator flags.

Allowed next phase: Phase 7K10 - GBPUSD Post-Remediation Known-Good Control Manual Market-Hours Snapshot Attempt.
"@

$outDir = Resolve-LocalPath $OutputDirectory
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$gatePath = Join-Path $outDir "phase7k9-freeze-lift-decision-and-known-good-control-selection-gate.json"
$notePath = Join-Path $outDir "phase7k9-freeze-lift-decision-and-known-good-control-selection-note.md"

$json = $gate | ConvertTo-Json -Depth 12
if (($json + "`n" + $note) -match $sensitivePattern) {
    throw "Generated Phase 7K9 artifacts contain credential-shaped or raw FIX content."
}

$json | Set-Content -LiteralPath $gatePath -Encoding UTF8
$note | Set-Content -LiteralPath $notePath -Encoding UTF8

Write-Host "Phase: 7K9"
Write-Host "FreezeLiftDecision: LiftForSingleFutureKnownGoodControlOnly"
Write-Host "SelectedFutureAttemptInstrument: GBPUSD"
Write-Host "FinalDecision: PASS_FREEZE_LIFT_SELECTION_RECORDED"
Write-Host "SelectionGate: $gatePath"
Write-Host "SelectionNote: $notePath"
