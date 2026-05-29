param(
    [string]$Phase7K5InterpretationFile = "artifacts/readiness/phase7k5-gbpusd-known-good-control-interpretation.json",
    [string]$Phase7K5GbpusdControlArtifactFile = "artifacts/lmax-readonly-runtime-additional-snapshot/gbpusd/lmax-readonly-gbpusd-demo-snapshot-result-20260511-192834.json",
    [string]$Phase7K5ClosureFile = "artifacts/lmax-readonly-runtime-additional-snapshot/gbpusd/closure/lmax-readonly-gbpusd-closure-manifest-20260511-192854.json",
    [string]$EarlierGbpusdArtifactFile = "artifacts/lmax-readonly-runtime-additional-snapshot/gbpusd/lmax-readonly-gbpusd-demo-snapshot-result-20260511-103318.json",
    [string]$EarlierEurgbpArtifactFile = "artifacts/lmax-readonly-runtime-additional-snapshot/eurgbp/lmax-readonly-eurgbp-demo-snapshot-result-20260511-163141.json",
    [string[]]$LaterFailedArtifactFiles = @(
        "artifacts/lmax-readonly-runtime-additional-snapshot/usdjpy/lmax-readonly-usdjpy-demo-snapshot-result-20260511-181833.json",
        "artifacts/lmax-readonly-runtime-additional-snapshot/usdjpy/lmax-readonly-usdjpy-demo-snapshot-result-20260511-182651.json",
        "artifacts/lmax-readonly-runtime-additional-snapshot/audusd/lmax-readonly-audusd-demo-snapshot-result-20260511-185948.json",
        "artifacts/lmax-readonly-runtime-additional-snapshot/gbpusd/lmax-readonly-gbpusd-demo-snapshot-result-20260511-192834.json"
    ),
    [string]$Phase7KPatternReportFile = "artifacts/readiness/phase7k-cross-instrument-post-success-connection-layer-pattern-analysis.json",
    [string]$Phase7KStopGateFile = "artifacts/readiness/phase7k-cross-instrument-additional-instrument-external-attempt-stop-gate.json",
    [string]$Phase7K3CompletedGateFile = "artifacts/readiness/phase7k3-operator-confirmed-environment-checklist-gate.json",
    [string]$Phase7K4SelectionGateFile = "artifacts/readiness/phase7k4-single-instrument-external-attempt-selection-gate.json",
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
    $safe = $raw -replace 'credentialProfileName|credentialReadAttempted|credentialValuesReturned|credentialAvailability|usernamePresent|passwordPresent|usernameLength|passwordLength|Credential|credential','SAFE_METADATA'
    if ($safe -match $sensitivePattern) {
        throw "$Label contains credential-shaped or raw FIX content."
    }
    return @{ path = $resolved; json = ($raw | ConvertFrom-Json) }
}

function Get-Counter($Artifact, [string]$Name) {
    if ($null -ne $Artifact.diagnostics -and $null -ne $Artifact.diagnostics.messageCounters -and $null -ne $Artifact.diagnostics.messageCounters.$Name) {
        return [int]$Artifact.diagnostics.messageCounters.$Name
    }
    $topLevelName = switch ($Name) {
        "marketDataRequestReject" { "marketDataRequestRejectCount" }
        "businessMessageReject" { "businessMessageRejectCount" }
        "reject" { "rejectCount" }
        default { $Name }
    }
    if ($null -ne $Artifact.$topLevelName) { return [int]$Artifact.$topLevelName }
    return 0
}

function New-AttemptSummary($ArtifactRef, [string]$Label) {
    $artifact = $ArtifactRef.json
    [ordered]@{
        label = $Label
        sourceFile = $ArtifactRef.path
        symbol = [string]$artifact.symbol
        slashSymbol = [string]$artifact.slashSymbol
        securityId = [string]$artifact.securityId
        securityIdSource = [string]$artifact.securityIdSource
        status = [string]$artifact.status
        snapshotReceived = [bool]$artifact.snapshotReceived
        entryCount = [int]$artifact.entryCount
        externalConnectionAttempted = [bool]$artifact.externalConnectionAttempted
        logonAttempted = [bool]$artifact.logonAttempted
        snapshotRequestAttempted = [bool]$artifact.snapshotRequestAttempted
        marketDataRequestReject = Get-Counter $artifact "marketDataRequestReject"
        businessMessageReject = Get-Counter $artifact "businessMessageReject"
        reject = Get-Counter $artifact "reject"
        tcpConnected = if ($null -ne $artifact.logonDiagnostics) { [bool]$artifact.logonDiagnostics.tcpConnected } else { $null }
        tlsConnected = if ($null -ne $artifact.logonDiagnostics) { [bool]$artifact.logonDiagnostics.tlsConnected } else { $null }
        orderSubmissionAttempted = [bool]$artifact.orderSubmissionAttempted
        shadowReplaySubmitAttempted = [bool]$artifact.shadowReplaySubmitAttempted
        tradingMutationAttempted = [bool]$artifact.tradingMutationAttempted
        schedulerStarted = [bool]$artifact.schedulerStarted
        credentialValuesReturned = [bool]$artifact.credentialValuesReturned
        noSensitiveContent = [bool]$artifact.noSensitiveContent
        redactionStatus = [string]$artifact.redactionStatus
    }
}

$interpretation = Read-JsonArtifact $Phase7K5InterpretationFile "Phase 7K5 interpretation"
$controlArtifact = Read-JsonArtifact $Phase7K5GbpusdControlArtifactFile "Phase 7K5 GBPUSD control artifact"
$controlClosure = Read-JsonArtifact $Phase7K5ClosureFile "Phase 7K5 closure"
$earlierGbpusd = Read-JsonArtifact $EarlierGbpusdArtifactFile "Earlier GBPUSD artifact"
$earlierEurgbp = Read-JsonArtifact $EarlierEurgbpArtifactFile "Earlier EURGBP artifact"
$phase7KPattern = Read-JsonArtifact $Phase7KPatternReportFile "Phase 7K pattern report"
$phase7KStop = Read-JsonArtifact $Phase7KStopGateFile "Phase 7K stop gate"
$phase7K3 = Read-JsonArtifact $Phase7K3CompletedGateFile "Phase 7K3 completed gate"
$phase7K4 = Read-JsonArtifact $Phase7K4SelectionGateFile "Phase 7K4 selection gate"

if ([string]$interpretation.json.phase -ne "7K5" -or [bool]$interpretation.json.knownGoodControlRecovered -or -not [bool]$interpretation.json.broaderEnvironmentSessionIssueStillSuspected) {
    throw "Phase 7K5 interpretation does not show failed known-good control with broader environment/session issue still suspected."
}
if ([string]$controlClosure.json.finalClosureDecision -ne "PASS_WITH_KNOWN_WARNINGS") {
    throw "Phase 7K5 closure is not the expected safe warning closure."
}
if ([string]$phase7K3.json.finalDecision -ne "PASS_OPERATOR_CHECKLIST_RECORDED") {
    throw "Phase 7K3 checklist gate is not completed."
}
if ([string]$phase7K4.json.finalDecision -ne "PASS_SELECTION_RECORDED" -or [string]$phase7K4.json.selectedFutureAttemptInstrument -ne "GBPUSD") {
    throw "Phase 7K4 did not select GBPUSD known-good control."
}

$successfulEarlierAttempts = @(
    New-AttemptSummary $earlierGbpusd "Earlier GBPUSD Completed"
    New-AttemptSummary $earlierEurgbp "Earlier EURGBP Completed"
)
$laterFailedAttempts = @()
foreach ($path in $LaterFailedArtifactFiles) {
    $laterFailedAttempts += New-AttemptSummary (Read-JsonArtifact $path "Later failed attempt") "Later FailedSafeConnectionError before logon"
}

$allLaterFailuresBeforeLogon = @($laterFailedAttempts | Where-Object { $_.status -ne "FailedSafeConnectionError" -or [bool]$_.logonAttempted }).Count -eq 0
$allLaterFailuresHadNoSnapshotRequest = @($laterFailedAttempts | Where-Object { [bool]$_.snapshotRequestAttempted }).Count -eq 0
$allLaterFailuresHadZeroRejects = @($laterFailedAttempts | Where-Object { [int]$_.marketDataRequestReject -ne 0 -or [int]$_.businessMessageReject -ne 0 -or [int]$_.reject -ne 0 }).Count -eq 0

$requiredRemediation = @(
    "Verify LMAX Demo endpoint and session availability independently without exposing secrets.",
    "Verify the account/session is not locked, exhausted, throttled, or otherwise blocked at venue side.",
    "Verify local network, VPN, proxy, and firewall state again.",
    "Verify DNS and TLS reachability using safe non-secret tooling.",
    "Verify local machine clock and time synchronization.",
    "Verify no stale local process, socket exhaustion, or lab process lock is present.",
    "Verify credential labels are present without printing values.",
    "Consider waiting for a new market or session window.",
    "Optionally contact venue/support if Demo endpoint continues to refuse pre-logon session establishment."
)

$disallowedActions = @(
    "No external run.",
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

$summary = [ordered]@{
    phase = "7K6"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    successfulEarlierAttempts = $successfulEarlierAttempts
    laterFailedAttempts = $laterFailedAttempts
    allLaterFailuresBeforeLogon = $allLaterFailuresBeforeLogon
    allLaterFailuresHadNoSnapshotRequest = $allLaterFailuresHadNoSnapshotRequest
    allLaterFailuresHadZeroRejects = $allLaterFailuresHadZeroRejects
    instrumentLevelRejectsObserved = $false
    marketDataRequestRejectObserved = $false
    invalidSecurityIdNotProven = $true
    tokyo600xNotJustified = $true
    environmentSessionLayerSuspected = $true
    recommendedOperationalState = "ExternalAttemptsFrozen"
    noSensitiveContent = $true
    finalDecision = "PASS_GLOBAL_FREEZE_RECORDED"
}

$gate = [ordered]@{
    phase = "7K6"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    freezeReason = "KnownGoodControlFailedBeforeLogonAfterPriorSuccess"
    globalExternalAttemptFreeze = $true
    anyInstrumentExternalRunAllowed = $false
    externalAdditionalInstrumentAttemptsCurrentlyAllowed = $false
    gbpusdControlRunAllowed = $false
    eurgbpControlRunAllowed = $false
    audusdRetryAllowed = $false
    usdjpyRetryAllowed = $false
    nextInstrumentRunAllowed = $false
    futureExternalRunCanBeConsidered = $false
    operatorEnvironmentChecklistPreviouslyCompleted = $true
    knownGoodControlRecovered = $false
    broaderEnvironmentSessionIssueStillSuspected = $true
    failedKnownGoodControlInstrument = "GBPUSD"
    failedKnownGoodControlSecurityId = "4002"
    externalRunAttemptedInThisPhase = $false
    snapshotRunInThisPhase = $false
    replayRunInThisPhase = $false
    controlRunInThisPhase = $false
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
    requiredRemediationBeforeAnyFutureExternalRun = $requiredRemediation
    allowedNextPhase = "Phase 7K7 - External Session Remediation Plan, No External Run"
    disallowedActions = $disallowedActions
    sourcePhase7K5Interpretation = $Phase7K5InterpretationFile
    sourcePhase7KPatternReport = $Phase7KPatternReportFile
    sourcePhase7KStopGate = $Phase7KStopGateFile
    sourcePhase7K3CompletedGate = $Phase7K3CompletedGateFile
    sourcePhase7K4SelectionGate = $Phase7K4SelectionGateFile
    noSensitiveContent = $true
    apiWorkerGatewayMode = "FakeLmaxGateway"
    finalDecision = "PASS_GLOBAL_FREEZE_RECORDED"
}

$note = @"
# Phase 7K6 - Global External Attempt Freeze

Earlier GBPUSD and EURGBP snapshots proved the Demo read-only MarketData workflow can work.

Later USDJPY, USDJPY retry, AUDUSD, and now GBPUSD known-good control all failed before TLS/FIX logon and before any MarketDataRequest. Reject counters remained zero. This points to a broader environment, session, or endpoint availability issue rather than an instrument-level MarketDataRequest problem.

This does not prove invalid SecurityIDs and does not justify switching to Tokyo 600x identifiers.

No more external attempts are allowed until remediation is planned and recorded. That includes no GBPUSD control retry, no EURGBP control run, no AUDUSD retry, no USDJPY retry, no next instrument, no batch, no loop, and no automatic retry.

The platform remains safe: API/Worker stay FakeLmaxGateway-only, no orders are enabled, no scheduler/polling is added, no runtime shadow replay submit is enabled, and no trading-state mutation is introduced.

Allowed next phase: Phase 7K7 - External Session Remediation Plan, No External Run.
"@

$outDir = Resolve-LocalPath $OutputDirectory
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$gatePath = Join-Path $outDir "phase7k6-global-external-attempt-freeze-gate.json"
$summaryPath = Join-Path $outDir "phase7k6-global-external-attempt-freeze-summary.json"
$notePath = Join-Path $outDir "phase7k6-global-external-attempt-freeze-note.md"

$gateJson = $gate | ConvertTo-Json -Depth 16
$summaryJson = $summary | ConvertTo-Json -Depth 16
if (($gateJson + "`n" + $summaryJson + "`n" + $note) -match $sensitivePattern) {
    throw "Generated Phase 7K6 artifacts contain credential-shaped or raw FIX content."
}

$gateJson | Set-Content -LiteralPath $gatePath -Encoding UTF8
$summaryJson | Set-Content -LiteralPath $summaryPath -Encoding UTF8
$note | Set-Content -LiteralPath $notePath -Encoding UTF8

Write-Host "Phase: 7K6"
Write-Host "FreezeReason: KnownGoodControlFailedBeforeLogonAfterPriorSuccess"
Write-Host "FinalDecision: PASS_GLOBAL_FREEZE_RECORDED"
Write-Host "GlobalFreezeGate: $gatePath"
Write-Host "DiagnosticSummary: $summaryPath"
Write-Host "FreezeNote: $notePath"
