param(
    [string]$Phase7KStopGateFile = "artifacts/readiness/phase7k-cross-instrument-additional-instrument-external-attempt-stop-gate.json",
    [string]$Phase7KDiagnosticReportFile = "artifacts/readiness/phase7k-cross-instrument-post-success-connection-layer-pattern-analysis.json",
    [string]$Phase7KOperatorNoteFile = "artifacts/readiness/phase7k-cross-instrument-operator-environment-note.md",
    [string]$OutputDirectory = "artifacts/readiness",
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

function Assert-FileExists([string]$PathValue, [string]$Label) {
    $resolved = Resolve-LocalPath $PathValue
    if (-not (Test-Path -LiteralPath $resolved)) {
        throw "$Label is missing: $resolved"
    }
}

function New-ChecklistItem([string]$Name, [bool]$Confirmed, [string]$Evidence) {
    [ordered]@{
        name = $Name
        status = if ($Confirmed) { "OperatorConfirmed" } else { "PendingOperatorConfirmation" }
        recordedAtUtc = if ($Confirmed) { [DateTimeOffset]::UtcNow.ToString("o") } else { $null }
        evidence = if ($Confirmed) { $Evidence } else { "No operator confirmation input supplied in this phase." }
        storesCredentialValues = $false
        printsCredentialValues = $false
    }
}

$stopGate = Read-JsonArtifact $Phase7KStopGateFile "Phase 7K stop gate"
$diagnostic = Read-JsonArtifact $Phase7KDiagnosticReportFile "Phase 7K diagnostic report"
Assert-FileExists $Phase7KOperatorNoteFile "Phase 7K operator note"

if ([string]$stopGate.phase -ne "7K" -or [string]$stopGate.finalDecision -ne "PASS_WITH_ACTION_REQUIRED") {
    throw "Phase 7K stop gate is not in the expected PASS_WITH_ACTION_REQUIRED state."
}

if ([string]$diagnostic.phase -ne "7K" -or [string]$diagnostic.broaderFailureClass -ne "CrossInstrumentFailedSafeConnectionBeforeSessionEstablishment") {
    throw "Phase 7K diagnostic report is not the expected cross-instrument connection-layer analysis."
}

$items = @(
    New-ChecklistItem "localNetworkStateChecked" ([bool]$ConfirmLocalNetworkStateChecked) "Operator confirmed local network state was checked without recording secret material."
    New-ChecklistItem "vpnProxyFirewallStateChecked" ([bool]$ConfirmVpnProxyFirewallStateChecked) "Operator confirmed VPN, proxy, and firewall state was checked without recording secret material."
    New-ChecklistItem "dnsEndpointResolutionCheckedUsingSafeNonSecretMethod" ([bool]$ConfirmDnsEndpointResolutionCheckedUsingSafeNonSecretMethod) "Operator confirmed DNS or endpoint reachability was checked using safe non-secret methods."
    New-ChecklistItem "localMachineClockTimeSyncChecked" ([bool]$ConfirmLocalMachineClockTimeSyncChecked) "Operator confirmed local machine clock and time synchronization were checked."
    New-ChecklistItem "localSocketProcessResourceStateChecked" ([bool]$ConfirmLocalSocketProcessResourceStateChecked) "Operator confirmed socket, process, and resource state was checked."
    New-ChecklistItem "localApiOrLabProcessLocksChecked" ([bool]$ConfirmLocalApiOrLabProcessLocksChecked) "Operator confirmed local API or lab process locks were checked."
    New-ChecklistItem "demoEndpointAvailabilityWindowChecked" ([bool]$ConfirmDemoEndpointAvailabilityWindowChecked) "Operator confirmed Demo endpoint availability window was checked."
    New-ChecklistItem "credentialPresenceCheckedWithoutValues" ([bool]$ConfirmCredentialPresenceCheckedWithoutValues) "Operator confirmed credential labels were present without printing or storing values."
    New-ChecklistItem "previousSessionExhaustionOrExternalSessionLockConsidered" ([bool]$ConfirmPreviousSessionExhaustionOrExternalSessionLockConsidered) "Operator considered previous session exhaustion or external session lock possibility."
    New-ChecklistItem "operatorReviewedPhase7KNote" ([bool]$ConfirmOperatorReviewedPhase7KNote) "Operator confirmed review of the Phase 7K environment note."
)

$checklistComplete = @($items | Where-Object { $_.status -ne "OperatorConfirmed" }).Count -eq 0
$futureExternalRunCanBeConsidered = $checklistComplete
$finalDecision = if ($checklistComplete) { "PASS" } else { "PASS_WITH_ACTION_REQUIRED" }
$allowedNextPhase = if ($checklistComplete) {
    "Phase 7K3 - One-Instrument External Attempt Candidate Gate, No External Run"
} else {
    "Phase 7K3 - Operator-Confirmed Environment Checklist Record, No External Run"
}

$disallowedActions = @(
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
    phase = "7K2"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    checklistMode = "OperatorRecordedLocalOnly"
    sourcePhase7KStopGate = $Phase7KStopGateFile
    sourcePhase7KDiagnosticReport = $Phase7KDiagnosticReportFile
    sourcePhase7KOperatorNote = $Phase7KOperatorNoteFile
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
    phase = "7K2"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    checklistComplete = $checklistComplete
    futureExternalRunCanBeConsidered = $futureExternalRunCanBeConsidered
    externalAdditionalInstrumentAttemptsCurrentlyAllowed = $false
    anyInstrumentExternalRunAllowed = $false
    operatorEnvironmentTroubleshootingRequired = -not $checklistComplete
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
    requiredNextOperatorAction = if ($checklistComplete) {
        "Open a new local-only one-instrument candidate gate before any external attempt is considered."
    } else {
        "Record explicit operator confirmations for every Phase 7K2 troubleshooting checklist item in a new local-only gate."
    }
    allowedNextPhase = $allowedNextPhase
    disallowedActions = $disallowedActions
    noSensitiveContent = $true
    apiWorkerGatewayMode = "FakeLmaxGateway"
    finalDecision = $finalDecision
}

$note = @"
# Phase 7K2 - Operator Environment Troubleshooting Completion Gate

Phase 7K2 records the local checklist gate after the Phase 7K cross-instrument pre-logon failure pattern.

No external connection, snapshot, replay, control snapshot, USDJPY retry, AUDUSD retry, or next-instrument run was performed in this phase.

No operator environment checks have been confirmed in this default record because no explicit confirmation switches were provided. Each checklist item is therefore marked `PendingOperatorConfirmation`.

External runs remain blocked. The next phase is to record explicit operator confirmations, still with no external run.

Allowed next phase: $allowedNextPhase.

This gate does not authorize retry, control runs, batch execution, loop execution, automatic retry, wrapper relaxation, SecurityID switching, Tokyo 600x switching, replay without MarketDataOnly evidence, order paths, scheduler/polling, runtime shadow replay submit, trading-state mutation, or gateway registration.
"@

$outDir = Resolve-LocalPath $OutputDirectory
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$recordPath = Join-Path $outDir "phase7k2-operator-environment-troubleshooting-checklist-record.json"
$gatePath = Join-Path $outDir "phase7k2-operator-environment-troubleshooting-completion-gate.json"
$notePath = Join-Path $outDir "phase7k2-operator-environment-troubleshooting-completion-note.md"

$record | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $recordPath -Encoding UTF8
$gate | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $gatePath -Encoding UTF8
$note | Set-Content -LiteralPath $notePath -Encoding UTF8

Write-Host "Phase: 7K2"
Write-Host "ChecklistComplete: $checklistComplete"
Write-Host "FutureExternalRunCanBeConsidered: $futureExternalRunCanBeConsidered"
Write-Host "FinalDecision: $finalDecision"
Write-Host "ChecklistRecord: $recordPath"
Write-Host "DecisionGate: $gatePath"
Write-Host "OperatorNote: $notePath"
