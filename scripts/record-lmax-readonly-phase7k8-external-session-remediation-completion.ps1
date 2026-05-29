param(
    [string]$Phase7K7PlanFile = "artifacts/readiness/phase7k7-external-session-remediation-plan.json",
    [string]$Phase7K7GateFile = "artifacts/readiness/phase7k7-external-session-remediation-gate.json",
    [string]$OutputDirectory = "artifacts/readiness",
    [string]$OperatorName = "",
    [string]$Reason = "",
    [switch]$ConfirmNoCredentialValuesProvided,
    [switch]$ConfirmDemoEndpointSessionAvailabilityVerified,
    [switch]$ConfirmAccountSessionNotLockedOrExhaustedVenueSide,
    [switch]$ConfirmNoPreviousDemoSessionOpenOrStaleVenueSide,
    [switch]$ConfirmLocalNetworkVpnProxyFirewallStateVerified,
    [switch]$ConfirmDnsTlsReachabilityCheckedSafely,
    [switch]$ConfirmLocalMachineClockTimeSyncChecked,
    [switch]$ConfirmNoStaleLocalProcessOrSocketExhaustion,
    [switch]$ConfirmNoLocalApiLabDllOrProcessLock,
    [switch]$ConfirmCredentialLabelsPresentWithoutValues,
    [switch]$ConfirmNewMarketOrSessionWindowConsidered,
    [switch]$ConfirmVenueSupportEscalationConsideredIfNeeded,
    [switch]$ConfirmNoCodeChangeNeededWithoutConcreteLocalIssue
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$secretLikePattern = '(?i)(password\s*[:=]|passwd\s*[:=]|pwd\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|raw\s*fix|BEGIN\s+PRIVATE\s+KEY)'

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
    if ($safe -match $secretLikePattern) {
        throw "$Label contains secret-like or raw FIX content."
    }
    return @{ path = $resolved; json = ($raw | ConvertFrom-Json) }
}

function Test-SecretLike([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    return $Value -match $secretLikePattern
}

function New-RemediationItem([string]$Name, [string]$Description, [bool]$Confirmed) {
    [ordered]@{
        name = $Name
        description = $Description
        status = if ($Confirmed) { "ConfirmedByOperator" } else { "PendingOperatorConfirmation" }
        confirmedAtUtc = if ($Confirmed) { [DateTimeOffset]::UtcNow.ToString("o") } else { $null }
        confirmationSource = if ($Confirmed) { "ExplicitOperatorFlag" } else { $null }
        noSecretMaterialIncluded = $true
    }
}

if (Test-SecretLike $OperatorName) {
    throw "OperatorName appears to contain secret-like material. Refusing to write Phase 7K8 record."
}
if (Test-SecretLike $Reason) {
    throw "Reason appears to contain secret-like material. Refusing to write Phase 7K8 record."
}

$plan = Read-JsonArtifact $Phase7K7PlanFile "Phase 7K7 remediation plan"
$gate = Read-JsonArtifact $Phase7K7GateFile "Phase 7K7 remediation gate"

if ([string]$plan.json.phase -ne "7K7" -or [string]$plan.json.finalDecision -ne "PASS_REMEDIATION_PLAN_RECORDED") {
    throw "Phase 7K7 plan is not in the expected PASS_REMEDIATION_PLAN_RECORDED state."
}
if ([string]$gate.json.phase -ne "7K7" -or -not [bool]$gate.json.globalExternalAttemptFreezeRemains -or [bool]$gate.json.anyInstrumentExternalRunAllowed) {
    throw "Phase 7K7 gate is not in the expected frozen state."
}

$items = @(
    New-RemediationItem "demoEndpointSessionAvailabilityVerified" "Verify LMAX Demo endpoint/session availability independently." ([bool]$ConfirmDemoEndpointSessionAvailabilityVerified)
    New-RemediationItem "accountSessionNotLockedOrExhaustedVenueSide" "Verify account/session is not locked or exhausted venue-side." ([bool]$ConfirmAccountSessionNotLockedOrExhaustedVenueSide)
    New-RemediationItem "noPreviousDemoSessionOpenOrStaleVenueSide" "Verify no previous Demo session remains open or stale at venue side." ([bool]$ConfirmNoPreviousDemoSessionOpenOrStaleVenueSide)
    New-RemediationItem "localNetworkVpnProxyFirewallStateVerified" "Verify local network, VPN, proxy, and firewall state." ([bool]$ConfirmLocalNetworkVpnProxyFirewallStateVerified)
    New-RemediationItem "dnsTlsReachabilityCheckedSafely" "Verify DNS/TLS reachability using safe non-secret tooling." ([bool]$ConfirmDnsTlsReachabilityCheckedSafely)
    New-RemediationItem "localMachineClockTimeSyncChecked" "Verify local machine clock and time synchronization." ([bool]$ConfirmLocalMachineClockTimeSyncChecked)
    New-RemediationItem "noStaleLocalProcessOrSocketExhaustion" "Verify no stale local process or socket exhaustion." ([bool]$ConfirmNoStaleLocalProcessOrSocketExhaustion)
    New-RemediationItem "noLocalApiLabDllOrProcessLock" "Verify no local API, lab DLL, or process lock." ([bool]$ConfirmNoLocalApiLabDllOrProcessLock)
    New-RemediationItem "credentialLabelsPresentWithoutValues" "Verify credential labels are present without printing or storing values." ([bool]$ConfirmCredentialLabelsPresentWithoutValues)
    New-RemediationItem "newMarketOrSessionWindowConsidered" "Consider waiting for a new market or session window." ([bool]$ConfirmNewMarketOrSessionWindowConsidered)
    New-RemediationItem "venueSupportEscalationConsideredIfNeeded" "Optionally contact LMAX/support if pre-logon failures continue." ([bool]$ConfirmVenueSupportEscalationConsideredIfNeeded)
    New-RemediationItem "noCodeChangeNeededWithoutConcreteLocalIssue" "Confirm no code change is needed unless a concrete local issue is found." ([bool]$ConfirmNoCodeChangeNeededWithoutConcreteLocalIssue)
)

$allConfirmed = @($items | Where-Object { $_.status -ne "ConfirmedByOperator" }).Count -eq 0
$operatorNamePresent = -not [string]::IsNullOrWhiteSpace($OperatorName)
$reasonPresent = -not [string]::IsNullOrWhiteSpace($Reason)
$remediationCompletionRecorded = $allConfirmed -and $operatorNamePresent -and $reasonPresent -and [bool]$ConfirmNoCredentialValuesProvided
$freezeLiftCanBeConsidered = $remediationCompletionRecorded
$finalDecision = if ($remediationCompletionRecorded) { "PASS_REMEDIATION_COMPLETION_RECORDED" } else { "PASS_WITH_ACTION_REQUIRED" }
$allowedNextPhase = if ($remediationCompletionRecorded) {
    "Phase 7K9 - Freeze Lift Decision Gate and Known-Good Control Candidate Selection, No External Run"
} else {
    "Phase 7K8 - Record External Session Remediation Completion, No External Run"
}

$disallowedActions = @(
    "No external run in Phase 7K8.",
    "No freeze lift in Phase 7K8.",
    "No GBPUSD control retry.",
    "No EURGBP control run.",
    "No AUDUSD retry.",
    "No USDJPY retry.",
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

$record = [ordered]@{
    phase = "7K8"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    remediationMode = "OperatorRecordedLocalOnly"
    sourcePhase7K7Plan = $Phase7K7PlanFile
    sourcePhase7K7Gate = $Phase7K7GateFile
    operatorNamePresent = $operatorNamePresent
    operatorName = if ($operatorNamePresent) { $OperatorName } else { $null }
    reasonPresent = $reasonPresent
    reason = if ($reasonPresent) { $Reason } else { $null }
    confirmNoCredentialValuesProvided = [bool]$ConfirmNoCredentialValuesProvided
    remediationItems = $items
    remediationCompletionRecorded = $remediationCompletionRecorded
    freezeLiftCanBeConsidered = $freezeLiftCanBeConsidered
    globalExternalAttemptFreezeRemains = $true
    externalRunAttemptedInThisPhase = $false
    snapshotRunInThisPhase = $false
    replayRunInThisPhase = $false
    anyInstrumentRunInThisPhase = $false
    credentialValuesStored = $false
    credentialValuesPrinted = $false
    noSensitiveContent = $true
    finalDecision = $finalDecision
}

$gateOut = [ordered]@{
    phase = "7K8"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    remediationCompletionRecorded = $remediationCompletionRecorded
    freezeLiftCanBeConsidered = $freezeLiftCanBeConsidered
    globalExternalAttemptFreezeRemains = $true
    freezeLifted = $false
    directRunAuthorization = $false
    anyInstrumentExternalRunAllowed = $false
    externalAdditionalInstrumentAttemptsCurrentlyAllowed = $false
    gbpusdControlRunAllowed = $false
    eurgbpControlRunAllowed = $false
    audusdRetryAllowed = $false
    usdjpyRetryAllowed = $false
    nextInstrumentRunAllowed = $false
    futureExternalRunCanBeConsidered = $false
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
    requiredNextDecision = if ($remediationCompletionRecorded) { "Run a no-external-run freeze lift and one-candidate selection gate. Do not run externally in Phase 7K8." } else { "Record explicit operator confirmations for every remediation checklist item." }
    allowedNextPhase = $allowedNextPhase
    disallowedActions = $disallowedActions
    apiWorkerGatewayMode = "FakeLmaxGateway"
    finalDecision = $finalDecision
}

$note = @"
# Phase 7K8 - External Session Remediation Completion Record

Phase 7K8 records operator-confirmed external/session remediation completion.

Remediation completion recorded: $remediationCompletionRecorded

Freeze lift can be considered by a later no-external-run gate: $freezeLiftCanBeConsidered

The global external attempt freeze remains active. Phase 7K8 does not lift the freeze and does not authorize any external run.

No LMAX connection, snapshot, replay, control run, USDJPY run, AUDUSD run, batch, loop, automatic retry, order path, scheduler/polling, runtime shadow replay submit, gateway registration, or trading-state mutation is performed in this phase.

Allowed next phase: $allowedNextPhase
"@

$outDir = Resolve-LocalPath $OutputDirectory
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$recordPath = Join-Path $outDir "phase7k8-external-session-remediation-completion-record.json"
$gatePath = Join-Path $outDir "phase7k8-external-session-remediation-completion-gate.json"
$notePath = Join-Path $outDir "phase7k8-external-session-remediation-completion-note.md"

$recordJson = $record | ConvertTo-Json -Depth 12
$gateJson = $gateOut | ConvertTo-Json -Depth 12
if (($recordJson + "`n" + $gateJson + "`n" + $note) -match $secretLikePattern) {
    throw "Generated Phase 7K8 artifacts contain secret-like or raw FIX content."
}

$recordJson | Set-Content -LiteralPath $recordPath -Encoding UTF8
$gateJson | Set-Content -LiteralPath $gatePath -Encoding UTF8
$note | Set-Content -LiteralPath $notePath -Encoding UTF8

Write-Host "Phase: 7K8"
Write-Host "RemediationCompletionRecorded: $remediationCompletionRecorded"
Write-Host "FreezeLiftCanBeConsidered: $freezeLiftCanBeConsidered"
Write-Host "FinalDecision: $finalDecision"
Write-Host "Record: $recordPath"
Write-Host "Gate: $gatePath"
Write-Host "Note: $notePath"
