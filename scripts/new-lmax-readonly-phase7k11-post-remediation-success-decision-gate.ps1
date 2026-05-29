param(
    [string]$Phase7K10InterpretationFile = "artifacts/readiness/phase7k10-gbpusd-post-remediation-known-good-control-interpretation.json",
    [string]$Phase7K10SnapshotFile = "artifacts/lmax-readonly-runtime-additional-snapshot/gbpusd/lmax-readonly-gbpusd-demo-snapshot-result-20260511-201523.json",
    [string]$Phase7K10EvidencePreviewFile = "artifacts/lmax-readonly-runtime-additional-snapshot/gbpusd/evidence-preview/lmax-readonly-gbpusd-evidence-preview-20260511-201538.json",
    [string]$Phase7K10ClosureFile = "artifacts/lmax-readonly-runtime-additional-snapshot/gbpusd/closure/lmax-readonly-gbpusd-closure-manifest-20260511-201559.json",
    [string]$PreviousAudusdFailedSafeArtifactFile = "artifacts/lmax-readonly-runtime-additional-snapshot/audusd/lmax-readonly-audusd-demo-snapshot-result-20260511-185948.json",
    [string]$AudusdFinalPreRunGateFile = "artifacts/lmax-readonly-runtime-securityid-planning/additional-final-prerun/lmax-readonly-additional-instrument-final-prerun-gate-AUDUSD-20260511-161447.json",
    [string]$Phase7K6FreezeGateFile = "artifacts/readiness/phase7k6-global-external-attempt-freeze-gate.json",
    [string]$Phase7K8CompletionGateFile = "artifacts/readiness/phase7k8-external-session-remediation-completion-gate.json",
    [string]$Phase7K9SelectionGateFile = "artifacts/readiness/phase7k9-freeze-lift-decision-and-known-good-control-selection-gate.json",
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
    if (-not (Test-Path -LiteralPath $resolved)) { throw "$Label is missing: $resolved" }
    $raw = Get-Content -LiteralPath $resolved -Raw
    $safe = $raw -replace 'credential|Credential|secret|Secret','SAFE_METADATA'
    if ($safe -match $sensitivePattern) { throw "$Label contains credential-shaped or raw FIX content." }
    return @{ path = $resolved; json = ($raw | ConvertFrom-Json) }
}

$phase7K10 = Read-JsonArtifact $Phase7K10InterpretationFile "Phase 7K10 interpretation"
$gbpusdSnapshot = Read-JsonArtifact $Phase7K10SnapshotFile "Phase 7K10 GBPUSD snapshot"
$gbpusdPreview = Read-JsonArtifact $Phase7K10EvidencePreviewFile "Phase 7K10 GBPUSD evidence preview"
$gbpusdClosure = Read-JsonArtifact $Phase7K10ClosureFile "Phase 7K10 GBPUSD closure"
$audusdFailed = Read-JsonArtifact $PreviousAudusdFailedSafeArtifactFile "Previous AUDUSD failed-safe artifact"
$audusdGate = Read-JsonArtifact $AudusdFinalPreRunGateFile "AUDUSD final pre-run gate"
$phase7K6 = Read-JsonArtifact $Phase7K6FreezeGateFile "Phase 7K6 freeze gate"
$phase7K8 = Read-JsonArtifact $Phase7K8CompletionGateFile "Phase 7K8 completion gate"
$phase7K9 = Read-JsonArtifact $Phase7K9SelectionGateFile "Phase 7K9 selection gate"

if ([string]$phase7K10.json.phase -ne "7K10" -or [string]$phase7K10.json.finalDecision -ne "PASS_POST_REMEDIATION_CONTROL_RECOVERED" -or -not [bool]$phase7K10.json.knownGoodControlRecovered -or -not [bool]$phase7K10.json.postRemediationSessionHealthy) {
    throw "Phase 7K10 interpretation does not record recovered known-good control."
}
if ([string]$gbpusdSnapshot.json.symbol -ne "GBPUSD" -or [string]$gbpusdSnapshot.json.securityId -ne "4002" -or [string]$gbpusdSnapshot.json.status -ne "Completed" -or -not [bool]$gbpusdSnapshot.json.snapshotReceived -or [int]$gbpusdSnapshot.json.entryCount -le 0) {
    throw "Phase 7K10 GBPUSD snapshot is not the expected successful recovered control."
}
if ([bool]$gbpusdSnapshot.json.orderSubmissionAttempted -or [bool]$gbpusdSnapshot.json.shadowReplaySubmitAttempted -or [bool]$gbpusdSnapshot.json.tradingMutationAttempted -or [bool]$gbpusdSnapshot.json.schedulerStarted -or [bool]$gbpusdSnapshot.json.credentialValuesReturned -or -not [bool]$gbpusdSnapshot.json.noSensitiveContent) {
    throw "Phase 7K10 GBPUSD snapshot contains an unsafe flag."
}
if ([string]$gbpusdPreview.json.evidenceMode -ne "MarketDataOnly" -or [string]$gbpusdPreview.json.instrument -ne "GBPUSD" -or [string]$gbpusdPreview.json.securityId -ne "4002" -or [string]$gbpusdPreview.json.marketData.status -ne "Ok" -or -not [bool]$gbpusdPreview.json.noSensitiveContent) {
    throw "Phase 7K10 GBPUSD evidence preview is not valid MarketDataOnly Ok evidence."
}
if ([string]$gbpusdClosure.json.finalClosureDecision -ne "PASS" -or [string]$gbpusdClosure.json.closureClassification -ne "CompletedWithBook") {
    throw "Phase 7K10 GBPUSD closure is not PASS / CompletedWithBook."
}
if ([string]$audusdFailed.json.symbol -ne "AUDUSD" -or [string]$audusdFailed.json.securityId -ne "4007" -or [string]$audusdFailed.json.status -ne "FailedSafeConnectionError" -or [bool]$audusdFailed.json.logonAttempted -or [bool]$audusdFailed.json.snapshotRequestAttempted) {
    throw "Previous AUDUSD artifact is not the expected pre-remediation before-logon safe failure."
}
if ([string]$audusdGate.json.symbol -ne "AUDUSD" -or [string]$audusdGate.json.planningSecurityId -ne "4007" -or [string]$audusdGate.json.finalDecision -ne "PASS") {
    throw "AUDUSD final pre-run gate is invalid."
}
if (-not [bool]$audusdGate.json.oneInstrumentAtATime -or [bool]$audusdGate.json.batchExecutionAllowed -or [bool]$audusdGate.json.externalRunAuthorized -or [bool]$audusdGate.json.canRunExternalSnapshot -or [bool]$audusdGate.json.eligibleForManualSnapshotAttempt -or [bool]$audusdGate.json.isApprovedForExternalRun) {
    throw "AUDUSD final pre-run gate safety flags are invalid."
}
if ([string]$phase7K6.json.phase -ne "7K6" -or -not [bool]$phase7K6.json.globalExternalAttemptFreeze) {
    throw "Phase 7K6 freeze gate is not present."
}
if ([string]$phase7K8.json.finalDecision -ne "PASS_REMEDIATION_COMPLETION_RECORDED" -or -not [bool]$phase7K8.json.remediationCompletionRecorded) {
    throw "Phase 7K8 remediation completion gate is not complete."
}
if ([string]$phase7K9.json.finalDecision -ne "PASS_FREEZE_LIFT_SELECTION_RECORDED" -or [string]$phase7K9.json.selectedFutureAttemptInstrument -ne "GBPUSD") {
    throw "Phase 7K9 selection gate did not select GBPUSD control."
}

$disallowedActions = @(
    "No external run in Phase 7K11.",
    "No AUDUSD run in Phase 7K11.",
    "No USDJPY retry.",
    "No GBPUSD rerun.",
    "No EURGBP control run.",
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
    phase = "7K11"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    postRemediationKnownGoodControlRecovered = $true
    knownGoodControlInstrument = "GBPUSD"
    knownGoodControlSecurityId = "4002"
    knownGoodControlSnapshotStatus = "Completed"
    knownGoodControlEvidenceMode = "MarketDataOnly"
    knownGoodControlEvidenceValidation = "Ok"
    localReplayNotRunReason = "LocalApiHealthTimeoutOptionalReplay"
    sessionLayerRecoveredForKnownGoodControl = $true
    selectedFutureAttemptInstrument = "AUDUSD"
    selectedFutureAttemptType = "PostRemediationAdditionalInstrumentRetry"
    selectedFutureAttemptReason = "AUDUSD failed during the pre-remediation cross-instrument connection issue; GBPUSD known-good control now confirms recovered external/session connectivity."
    selectedInstrumentSecurityId = "4007"
    selectedInstrumentSecurityIdSource = "8"
    selectedInstrumentEnvironment = "Demo"
    selectedInstrumentVenueProfile = "DemoLondon"
    selectedInstrumentRequestMode = "SnapshotPlusUpdates"
    selectedInstrumentSymbolEncodingMode = "SecurityIdOnly"
    selectedInstrumentMarketDepth = 1
    selectedInstrumentFinalPreRunGatePath = $audusdGate.path
    selectedInstrumentFinalPreRunGateDecision = "PASS"
    previousAudusdFailedSafeArtifactPath = $audusdFailed.path
    phase7K10InterpretationPath = $phase7K10.path
    phase7K10SnapshotPath = $gbpusdSnapshot.path
    phase7K10EvidencePreviewPath = $gbpusdPreview.path
    phase7K10ClosurePath = $gbpusdClosure.path
    exactlyOneFutureCandidateSelected = $true
    futureExternalRunCanBeConsidered = $true
    directRunAuthorization = $false
    anyInstrumentExternalRunAllowed = $false
    externalAdditionalInstrumentAttemptsCurrentlyAllowed = $false
    externalRunAttemptedInThisPhase = $false
    snapshotRunInThisPhase = $false
    replayRunInThisPhase = $false
    audusdRunInThisPhase = $false
    usdjpyRunInThisPhase = $false
    gbpUsdRunInThisPhase = $false
    eurGbpRunInThisPhase = $false
    usdJpyRemainsParked = $true
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
    allowedNextPhase = "Phase 7K12 - AUDUSD Post-Remediation One-Instrument Manual Snapshot Attempt"
    disallowedActions = $disallowedActions
    noSensitiveContent = $true
    apiWorkerGatewayMode = "FakeLmaxGateway"
    finalDecision = "PASS_POST_REMEDIATION_SUCCESS_DECISION_RECORDED"
}

$note = @"
# Phase 7K11 - Post-Remediation Success Decision

GBPUSD post-remediation known-good control recovered successfully. The control reached logon, sent the MarketData request, received a two-entry book, and produced a valid MarketDataOnly evidence preview.

This suggests the external/session layer is healthy again for a known-good control instrument.

AUDUSD is selected as the next future one-instrument manual candidate because it failed during the earlier session issue. USDJPY remains parked because it has a separate repeated-failure troubleshooting trail.

No run is authorized in Phase 7K11. The next phase must be one-instrument-only, manual, AUDUSD only, and must use explicit operator flags with the Phase 7H wrapper and the AUDUSD final pre-run gate.

Allowed next phase: Phase 7K12 - AUDUSD Post-Remediation One-Instrument Manual Snapshot Attempt.
"@

$outDir = Resolve-LocalPath $OutputDirectory
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$gatePath = Join-Path $outDir "phase7k11-post-remediation-success-decision-gate.json"
$notePath = Join-Path $outDir "phase7k11-post-remediation-success-decision-note.md"

$json = $gate | ConvertTo-Json -Depth 12
if (($json + "`n" + $note) -match $sensitivePattern) {
    throw "Generated Phase 7K11 artifacts contain credential-shaped or raw FIX content."
}

$json | Set-Content -LiteralPath $gatePath -Encoding UTF8
$note | Set-Content -LiteralPath $notePath -Encoding UTF8

Write-Host "Phase: 7K11"
Write-Host "KnownGoodControlRecovered: true"
Write-Host "SelectedFutureAttemptInstrument: AUDUSD"
Write-Host "FinalDecision: PASS_POST_REMEDIATION_SUCCESS_DECISION_RECORDED"
Write-Host "AllowedNextPhase: Phase 7K12 - AUDUSD Post-Remediation One-Instrument Manual Snapshot Attempt"
Write-Host "DecisionGate: $gatePath"
Write-Host "DecisionNote: $notePath"
