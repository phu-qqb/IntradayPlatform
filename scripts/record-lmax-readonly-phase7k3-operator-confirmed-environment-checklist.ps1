param(
    [string]$Phase7K2ChecklistRecordFile = "artifacts/readiness/phase7k2-operator-environment-troubleshooting-checklist-record.json",
    [string]$Phase7K2GateFile = "artifacts/readiness/phase7k2-operator-environment-troubleshooting-completion-gate.json",
    [string]$Phase7KDiagnosticReportFile = "artifacts/readiness/phase7k-cross-instrument-post-success-connection-layer-pattern-analysis.json",
    [string]$Phase7KStopGateFile = "artifacts/readiness/phase7k-cross-instrument-additional-instrument-external-attempt-stop-gate.json",
    [string]$OutputDirectory = "artifacts/readiness",
    [string]$OperatorName = "",
    [string]$Reason = "",
    [switch]$ConfirmNoCredentialValuesProvided,
    [switch]$ConfirmLocalNetworkStateChecked,
    [switch]$ConfirmVpnProxyFirewallStateChecked,
    [switch]$ConfirmDnsEndpointResolutionCheckedUsingSafeNonSecretMethod,
    [switch]$ConfirmLocalMachineClockTimeSyncChecked,
    [switch]$ConfirmLocalSocketProcessResourceStateChecked,
    [switch]$ConfirmLocalApiOrLabProcessLocksChecked,
    [switch]$ConfirmDemoEndpointAvailabilityWindowChecked,
    [switch]$ConfirmCredentialPresenceCheckedWithoutValues,
    [switch]$ConfirmPreviousSessionExhaustionOrExternalSessionLockConsidered,
    [switch]$ConfirmOperatorReviewedPhase7KNote
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$secretLikePattern = '(?i)(password|passwd|pwd|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer\s+|553=|554=|raw\s*fix|BEGIN\s+PRIVATE\s+KEY)'

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

    return Get-Content -LiteralPath $resolved -Raw | ConvertFrom-Json
}

function Test-SecretLike([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    return $Value -match $secretLikePattern
}

function New-ChecklistItem([string]$Name, [bool]$Confirmed) {
    [ordered]@{
        name = $Name
        status = if ($Confirmed) { "ConfirmedByOperator" } else { "PendingOperatorConfirmation" }
        confirmedAtUtc = if ($Confirmed) { [DateTimeOffset]::UtcNow.ToString("o") } else { $null }
        confirmationSource = if ($Confirmed) { "ExplicitOperatorFlag" } else { $null }
        noSecretMaterialIncluded = $true
    }
}

if (Test-SecretLike $OperatorName) {
    throw "OperatorName appears to contain secret-like material. Refusing to write Phase 7K3 record."
}

if (Test-SecretLike $Reason) {
    throw "Reason appears to contain secret-like material. Refusing to write Phase 7K3 record."
}

$phase7K2Record = Read-JsonArtifact $Phase7K2ChecklistRecordFile "Phase 7K2 checklist record"
$phase7K2Gate = Read-JsonArtifact $Phase7K2GateFile "Phase 7K2 gate"
$phase7KDiagnostic = Read-JsonArtifact $Phase7KDiagnosticReportFile "Phase 7K diagnostic report"
$phase7KStopGate = Read-JsonArtifact $Phase7KStopGateFile "Phase 7K stop gate"

if ([string]$phase7K2Record.phase -ne "7K2" -or [string]$phase7K2Gate.phase -ne "7K2") {
    throw "Phase 7K2 inputs are not valid Phase 7K2 artifacts."
}

if ([string]$phase7KDiagnostic.phase -ne "7K" -or [string]$phase7KStopGate.phase -ne "7K") {
    throw "Phase 7K inputs are not valid Phase 7K artifacts."
}

if ([bool]$phase7KStopGate.anyInstrumentExternalRunAllowed -or [bool]$phase7KStopGate.externalAdditionalInstrumentAttemptsCurrentlyAllowed) {
    throw "Phase 7K stop gate unexpectedly allows external attempts."
}

$items = @(
    New-ChecklistItem "localNetworkStateChecked" ([bool]$ConfirmLocalNetworkStateChecked)
    New-ChecklistItem "vpnProxyFirewallStateChecked" ([bool]$ConfirmVpnProxyFirewallStateChecked)
    New-ChecklistItem "dnsEndpointResolutionCheckedUsingSafeNonSecretMethod" ([bool]$ConfirmDnsEndpointResolutionCheckedUsingSafeNonSecretMethod)
    New-ChecklistItem "localMachineClockTimeSyncChecked" ([bool]$ConfirmLocalMachineClockTimeSyncChecked)
    New-ChecklistItem "localSocketProcessResourceStateChecked" ([bool]$ConfirmLocalSocketProcessResourceStateChecked)
    New-ChecklistItem "localApiOrLabProcessLocksChecked" ([bool]$ConfirmLocalApiOrLabProcessLocksChecked)
    New-ChecklistItem "demoEndpointAvailabilityWindowChecked" ([bool]$ConfirmDemoEndpointAvailabilityWindowChecked)
    New-ChecklistItem "credentialPresenceCheckedWithoutValues" ([bool]$ConfirmCredentialPresenceCheckedWithoutValues)
    New-ChecklistItem "previousSessionExhaustionOrExternalSessionLockConsidered" ([bool]$ConfirmPreviousSessionExhaustionOrExternalSessionLockConsidered)
    New-ChecklistItem "operatorReviewedPhase7KNote" ([bool]$ConfirmOperatorReviewedPhase7KNote)
)

$allChecklistFlagsConfirmed = @($items | Where-Object { $_.status -ne "ConfirmedByOperator" }).Count -eq 0
$operatorNamePresent = -not [string]::IsNullOrWhiteSpace($OperatorName)
$reasonPresent = -not [string]::IsNullOrWhiteSpace($Reason)
$checklistComplete = $allChecklistFlagsConfirmed -and $operatorNamePresent -and $reasonPresent -and [bool]$ConfirmNoCredentialValuesProvided
$futureExternalRunCanBeConsidered = $checklistComplete
$operatorTroubleshootingRequired = -not $checklistComplete
$finalDecision = if ($checklistComplete) { "PASS_OPERATOR_CHECKLIST_RECORDED" } else { "PASS_WITH_ACTION_REQUIRED" }
$allowedNextPhase = if ($checklistComplete) {
    "Phase 7K4 - Single-Instrument External Attempt Selection Gate, No External Run"
} else {
    "Phase 7K3 - Operator-Confirmed Environment Checklist Record, No External Run"
}
$requiredNextDecision = if ($checklistComplete) {
    "Choose exactly one possible future manual instrument attempt in a later phase: AUDUSD retry, GBPUSD control, EURGBP control, or USDJPY retry. Do not open a run in Phase 7K3."
} else {
    "Provide explicit operator confirmations for every required checklist item, OperatorName, Reason, and ConfirmNoCredentialValuesProvided."
}

$disallowedActions = @(
    "No external run in Phase 7K3.",
    "No USDJPY retry.",
    "No AUDUSD retry.",
    "No GBPUSD/EURGBP control run.",
    "No next instrument.",
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

$record = [ordered]@{
    phase = "7K3"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    checklistMode = "OperatorConfirmedLocalOnly"
    sourcePhase7K2ChecklistRecord = $Phase7K2ChecklistRecordFile
    sourcePhase7K2Gate = $Phase7K2GateFile
    sourcePhase7KDiagnosticReport = $Phase7KDiagnosticReportFile
    sourcePhase7KStopGate = $Phase7KStopGateFile
    operatorNamePresent = $operatorNamePresent
    operatorName = if ($operatorNamePresent) { $OperatorName } else { $null }
    reasonPresent = $reasonPresent
    reason = if ($reasonPresent) { $Reason } else { $null }
    confirmNoCredentialValuesProvided = [bool]$ConfirmNoCredentialValuesProvided
    externalRunAttemptedInThisPhase = $false
    snapshotRunInThisPhase = $false
    replayRunInThisPhase = $false
    controlSnapshotRunInThisPhase = $false
    anyInstrumentRunInThisPhase = $false
    credentialValuesStored = $false
    credentialValuesPrinted = $false
    noSensitiveContent = $true
    checklistItems = $items
    checklistComplete = $checklistComplete
    futureExternalRunCanBeConsidered = $futureExternalRunCanBeConsidered
    finalDecision = $finalDecision
}

$gate = [ordered]@{
    phase = "7K3"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    checklistComplete = $checklistComplete
    futureExternalRunCanBeConsidered = $futureExternalRunCanBeConsidered
    externalAdditionalInstrumentAttemptsCurrentlyAllowed = $false
    anyInstrumentExternalRunAllowed = $false
    operatorEnvironmentTroubleshootingRequired = $operatorTroubleshootingRequired
    audusdRetryAllowed = $false
    usdjpyRetryAllowed = $false
    gbpusdControlRunAllowed = $false
    eurgbpControlRunAllowed = $false
    nextInstrumentRunAllowed = $false
    automaticRetryRecommended = $false
    batchExecutionAllowed = $false
    wrapperValidationWeakened = $false
    securityIdSwitchRecommended = $false
    tokyo600xSwitchRecommended = $false
    orderPathEnabled = $false
    schedulerOrPollingEnabled = $false
    runtimeShadowReplaySubmitEnabled = $false
    tradingMutationEnabled = $false
    gatewayRegistrationEnabled = $false
    operatorNamePresent = $operatorNamePresent
    reasonPresent = $reasonPresent
    credentialValuesStored = $false
    credentialValuesPrinted = $false
    noSensitiveContent = $true
    requiredNextDecision = $requiredNextDecision
    allowedNextPhase = $allowedNextPhase
    disallowedActions = $disallowedActions
    apiWorkerGatewayMode = "FakeLmaxGateway"
    finalDecision = $finalDecision
}

$note = @"
# Phase 7K3 - Operator-Confirmed Environment Checklist Record

Phase 7K3 records explicit operator confirmations for the Phase 7K2 environment troubleshooting checklist.

Checklist complete: $checklistComplete

Future external run can be considered by a later decision phase: $futureExternalRunCanBeConsidered

This phase did not connect to LMAX, request a snapshot, replay evidence, run a control snapshot, run USDJPY, run AUDUSD, run any next instrument, submit orders, register a gateway, or mutate trading state.

Even when the checklist is complete, Phase 7K3 does not authorize an external run. It only allows a later local decision phase to consider exactly one manual one-instrument candidate.

Allowed next phase: $allowedNextPhase
"@

$outDir = Resolve-LocalPath $OutputDirectory
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$recordPath = Join-Path $outDir "phase7k3-operator-confirmed-environment-checklist-record.json"
$gatePath = Join-Path $outDir "phase7k3-operator-confirmed-environment-checklist-gate.json"
$notePath = Join-Path $outDir "phase7k3-operator-confirmed-environment-checklist-note.md"

$record | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $recordPath -Encoding UTF8
$gate | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $gatePath -Encoding UTF8
$note | Set-Content -LiteralPath $notePath -Encoding UTF8

Write-Host "Phase: 7K3"
Write-Host "ChecklistComplete: $checklistComplete"
Write-Host "FutureExternalRunCanBeConsidered: $futureExternalRunCanBeConsidered"
Write-Host "FinalDecision: $finalDecision"
Write-Host "ChecklistRecord: $recordPath"
Write-Host "DecisionGate: $gatePath"
Write-Host "OperatorNote: $notePath"
