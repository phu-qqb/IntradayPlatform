param(
    [string]$Phase7K3GateFile = "artifacts/readiness/phase7k3-operator-confirmed-environment-checklist-gate.json",
    [string]$Phase7K3ChecklistRecordFile = "artifacts/readiness/phase7k3-operator-confirmed-environment-checklist-record.json",
    [string]$Phase7KStopGateFile = "artifacts/readiness/phase7k-cross-instrument-additional-instrument-external-attempt-stop-gate.json",
    [string]$Phase7KDiagnosticReportFile = "artifacts/readiness/phase7k-cross-instrument-post-success-connection-layer-pattern-analysis.json",
    [string]$PriorGbpusdSnapshotFile = "artifacts/lmax-readonly-runtime-additional-snapshot/gbpusd/lmax-readonly-gbpusd-demo-snapshot-result-20260511-103318.json",
    [string]$GbpusdFinalPreRunGateFile = "",
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
    $safe = $raw -replace 'credentialProfileName|credentialValuesReturned|credentialReadAttempted|credential|Credential','SAFE_METADATA'
    if ($safe -match $sensitivePattern) {
        throw "$Label contains credential-shaped or raw FIX content."
    }
    return @{ path = $resolved; json = ($raw | ConvertFrom-Json); raw = $raw }
}

function Find-LatestGbpusdFinalPreRunGate {
    $dir = Join-Path $repoRoot "artifacts/lmax-readonly-runtime-securityid-planning/additional-final-prerun"
    if (-not (Test-Path -LiteralPath $dir)) { return "" }
    $match = Get-ChildItem -LiteralPath $dir -Filter "lmax-readonly-additional-instrument-final-prerun-gate-GBPUSD-*.json" -File |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if ($null -eq $match) { return "" }
    return $match.FullName
}

if ([string]::IsNullOrWhiteSpace($GbpusdFinalPreRunGateFile)) {
    $GbpusdFinalPreRunGateFile = Find-LatestGbpusdFinalPreRunGate
}

$phase7K3Gate = Read-JsonArtifact $Phase7K3GateFile "Phase 7K3 gate"
$phase7K3Record = Read-JsonArtifact $Phase7K3ChecklistRecordFile "Phase 7K3 checklist record"
$phase7KStopGate = Read-JsonArtifact $Phase7KStopGateFile "Phase 7K stop gate"
$phase7KDiagnostic = Read-JsonArtifact $Phase7KDiagnosticReportFile "Phase 7K diagnostic report"
$gbpusdSnapshot = Read-JsonArtifact $PriorGbpusdSnapshotFile "Prior GBPUSD snapshot"
$gbpusdFinalGate = Read-JsonArtifact $GbpusdFinalPreRunGateFile "GBPUSD final pre-run gate"

if ([string]$phase7K3Gate.json.phase -ne "7K3" -or [string]$phase7K3Gate.json.finalDecision -ne "PASS_OPERATOR_CHECKLIST_RECORDED" -or -not [bool]$phase7K3Gate.json.checklistComplete) {
    throw "Phase 7K3 gate is not a completed operator checklist gate."
}
if ([string]$phase7K3Record.json.phase -ne "7K3" -or -not [bool]$phase7K3Record.json.checklistComplete) {
    throw "Phase 7K3 checklist record is not complete."
}
if ([string]$phase7KStopGate.json.phase -ne "7K" -or [bool]$phase7KStopGate.json.anyInstrumentExternalRunAllowed) {
    throw "Phase 7K stop gate is not present or unexpectedly allows external runs."
}
if ([string]$phase7KDiagnostic.json.phase -ne "7K" -or [string]$phase7KDiagnostic.json.broaderFailureClass -ne "CrossInstrumentFailedSafeConnectionBeforeSessionEstablishment") {
    throw "Phase 7K diagnostic report does not match the expected cross-instrument failure class."
}

if ([string]$gbpusdSnapshot.json.symbol -ne "GBPUSD" -or [string]$gbpusdSnapshot.json.securityId -ne "4002" -or [string]$gbpusdSnapshot.json.status -ne "Completed" -or -not [bool]$gbpusdSnapshot.json.snapshotReceived -or [int]$gbpusdSnapshot.json.entryCount -le 0) {
    throw "Prior GBPUSD snapshot is not a known-good completed snapshot."
}

if ([string]$gbpusdFinalGate.json.symbol -ne "GBPUSD" -or [string]$gbpusdFinalGate.json.planningSecurityId -ne "4002" -or [string]$gbpusdFinalGate.json.securityIdSource -ne "8" -or [string]$gbpusdFinalGate.json.finalDecision -ne "PASS") {
    throw "GBPUSD final pre-run gate identity or decision is invalid."
}
if (-not [bool]$gbpusdFinalGate.json.oneInstrumentAtATime -or [bool]$gbpusdFinalGate.json.batchExecutionAllowed -or [bool]$gbpusdFinalGate.json.externalRunAuthorized -or [bool]$gbpusdFinalGate.json.canRunExternalSnapshot -or [bool]$gbpusdFinalGate.json.eligibleForManualSnapshotAttempt -or [bool]$gbpusdFinalGate.json.isApprovedForExternalRun) {
    throw "GBPUSD final pre-run gate run eligibility flags are invalid."
}

$disallowedActions = @(
    "No external run in Phase 7K4.",
    "No GBPUSD control run in Phase 7K4.",
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
    phase = "7K4"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    checklistComplete = $true
    phase7k3GateDecision = "PASS_OPERATOR_CHECKLIST_RECORDED"
    selectedFutureAttemptInstrument = "GBPUSD"
    selectedFutureAttemptType = "KnownGoodControlSnapshot"
    selectedFutureAttemptReason = "GBPUSD previously completed successfully and is the safest one-instrument control candidate to test Demo connection/session health after cross-instrument pre-logon failures."
    selectedInstrumentSecurityId = "4002"
    selectedInstrumentSecurityIdSource = "8"
    selectedInstrumentEnvironment = "Demo"
    selectedInstrumentVenueProfile = "DemoLondon"
    selectedInstrumentRequestMode = "SnapshotPlusUpdates"
    selectedInstrumentSymbolEncodingMode = "SecurityIdOnly"
    selectedInstrumentMarketDepth = 1
    selectedInstrumentFinalPreRunGatePath = $gbpusdFinalGate.path
    selectedInstrumentFinalPreRunGateDecision = "PASS"
    priorSuccessfulGbpusdSnapshotPath = $gbpusdSnapshot.path
    priorSuccessfulGbpusdStatus = "Completed"
    priorSuccessfulGbpusdSnapshotReceived = $true
    priorSuccessfulGbpusdEntryCount = [int]$gbpusdSnapshot.json.entryCount
    exactlyOneFutureCandidateSelected = $true
    futureExternalRunCanBeConsidered = $true
    externalAdditionalInstrumentAttemptsCurrentlyAllowed = $false
    anyInstrumentExternalRunAllowed = $false
    externalRunAttemptedInThisPhase = $false
    snapshotRunInThisPhase = $false
    replayRunInThisPhase = $false
    controlRunInThisPhase = $false
    audusdRunInThisPhase = $false
    usdjpyRunInThisPhase = $false
    eurGbpRunInThisPhase = $false
    gbpUsdRunInThisPhase = $false
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
    allowedNextPhase = "Phase 7K5 - GBPUSD Known-Good Control Manual Market-Hours Snapshot Attempt"
    disallowedActions = $disallowedActions
    noSensitiveContent = $true
    apiWorkerGatewayMode = "FakeLmaxGateway"
    finalDecision = "PASS_SELECTION_RECORDED"
}

$note = @"
# Phase 7K4 - Single-Instrument External Attempt Selection Gate

Phase 7K3 operator checklist completion is recorded. Phase 7K4 selects exactly one future candidate but does not run it.

Selected candidate: GBPUSD known-good control snapshot.

Reason: GBPUSD previously completed successfully with a sanitized Demo read-only MarketData snapshot. A later one-instrument GBPUSD control attempt can test whether the environment/session layer is healthy again before returning to AUDUSD or USDJPY.

USDJPY remains parked. AUDUSD retry remains parked. EURGBP control is not selected.

This phase does not authorize an external run. A future GBPUSD control attempt must be one-instrument-only, manual, market-hours, and use the Phase 7H wrapper plus the selected compatible final pre-run gate.

Allowed next phase: Phase 7K5 - GBPUSD Known-Good Control Manual Market-Hours Snapshot Attempt.
"@

$outDir = Resolve-LocalPath $OutputDirectory
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$gatePath = Join-Path $outDir "phase7k4-single-instrument-external-attempt-selection-gate.json"
$notePath = Join-Path $outDir "phase7k4-single-instrument-external-attempt-selection-note.md"

$json = $gate | ConvertTo-Json -Depth 12
if ($json -match $sensitivePattern) {
    throw "Generated Phase 7K4 selection gate contains credential-shaped or raw FIX content."
}

$json | Set-Content -LiteralPath $gatePath -Encoding UTF8
$note | Set-Content -LiteralPath $notePath -Encoding UTF8

Write-Host "Phase: 7K4"
Write-Host "SelectedFutureAttemptInstrument: GBPUSD"
Write-Host "SelectedFutureAttemptType: KnownGoodControlSnapshot"
Write-Host "FinalDecision: PASS_SELECTION_RECORDED"
Write-Host "SelectionGate: $gatePath"
Write-Host "SelectionNote: $notePath"
Write-Host "SelectedInstrumentFinalPreRunGatePath: $($gbpusdFinalGate.path)"
