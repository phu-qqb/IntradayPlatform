param(
    [string]$Phase7K6FreezeGateFile = "artifacts/readiness/phase7k6-global-external-attempt-freeze-gate.json",
    [string]$Phase7K6DiagnosticSummaryFile = "artifacts/readiness/phase7k6-global-external-attempt-freeze-summary.json",
    [string]$Phase7K6NoteFile = "artifacts/readiness/phase7k6-global-external-attempt-freeze-note.md",
    [string]$Phase7K3CompletedGateFile = "artifacts/readiness/phase7k3-operator-confirmed-environment-checklist-gate.json",
    [string]$Phase7K5InterpretationFile = "artifacts/readiness/phase7k5-gbpusd-known-good-control-interpretation.json",
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

function Assert-FileSafe([string]$PathValue, [string]$Label) {
    $resolved = Resolve-LocalPath $PathValue
    if (-not (Test-Path -LiteralPath $resolved)) {
        throw "$Label is missing: $resolved"
    }
    $raw = Get-Content -LiteralPath $resolved -Raw
    $safe = $raw -replace 'credential|Credential','SAFE_METADATA'
    if ($safe -match $sensitivePattern) {
        throw "$Label contains credential-shaped or raw FIX content."
    }
}

$freezeGate = Read-JsonArtifact $Phase7K6FreezeGateFile "Phase 7K6 freeze gate"
$summary = Read-JsonArtifact $Phase7K6DiagnosticSummaryFile "Phase 7K6 diagnostic summary"
Assert-FileSafe $Phase7K6NoteFile "Phase 7K6 note"
$phase7K3 = Read-JsonArtifact $Phase7K3CompletedGateFile "Phase 7K3 completed gate"
$phase7K5 = Read-JsonArtifact $Phase7K5InterpretationFile "Phase 7K5 interpretation"

if ([string]$freezeGate.json.phase -ne "7K6" -or -not [bool]$freezeGate.json.globalExternalAttemptFreeze -or [string]$freezeGate.json.finalDecision -ne "PASS_GLOBAL_FREEZE_RECORDED") {
    throw "Phase 7K6 freeze gate is not in the expected frozen state."
}
if ([string]$summary.json.phase -ne "7K6" -or [string]$summary.json.recommendedOperationalState -ne "ExternalAttemptsFrozen") {
    throw "Phase 7K6 summary is not in ExternalAttemptsFrozen state."
}
if ([string]$phase7K3.json.finalDecision -ne "PASS_OPERATOR_CHECKLIST_RECORDED") {
    throw "Phase 7K3 operator checklist is not recorded complete."
}
if ([string]$phase7K5.json.phase -ne "7K5" -or [bool]$phase7K5.json.knownGoodControlRecovered -or -not [bool]$phase7K5.json.broaderEnvironmentSessionIssueStillSuspected) {
    throw "Phase 7K5 interpretation does not show failed known-good control and suspected environment/session issue."
}

$remediationChecklist = @(
    "Verify LMAX Demo endpoint/session availability independently.",
    "Verify account/session is not locked or exhausted venue-side.",
    "Verify no previous Demo session remains open or stale at venue side.",
    "Verify local network, VPN, proxy, and firewall state.",
    "Verify DNS/TLS reachability using safe non-secret tooling.",
    "Verify local machine clock and time synchronization.",
    "Verify no stale local process or socket exhaustion.",
    "Verify no local API, lab DLL, or process lock.",
    "Verify credential labels are present without printing or storing values.",
    "Consider waiting for a new market or session window.",
    "Optionally contact LMAX/support if pre-logon failures continue.",
    "Confirm no code change is needed unless a concrete local issue is found."
)

$requiredEvidence = @(
    "operatorRemediationChecklistRecorded=true in a later phase.",
    "Global freeze explicitly lifted by a dedicated no-external-run decision gate.",
    "Exactly one future instrument selected by a dedicated selection gate.",
    "Final pre-run gate for that instrument validates PASS.",
    "Wrapper validation remains unchanged.",
    "Required operator flags remain required: -AllowExternalConnections, -ConfirmDemoReadOnly, and human-provided -Reason."
)

$recommendedOrder = @(
    "Complete remediation checks outside the app without exposing secrets.",
    "Record remediation completion in Phase 7K8 with explicit operator confirmations.",
    "Run a separate no-external-run freeze-lift decision gate.",
    "Select exactly one known-good control candidate in a later selection gate.",
    "Run a later single manual attempt only if explicitly approved by the operator."
)

$futureRunReopenPolicy = [ordered]@{
    directRunAuthorizedInPhase7K7 = $false
    futureExternalAttemptRequiresRemediationCompletion = $true
    futureExternalAttemptRequiresSeparateFreezeLiftGate = $true
    futureExternalAttemptRequiresExactlyOneCandidate = $true
    futureExternalAttemptRequiresLaterManualRunPhase = $true
    preferredFutureCandidatesAfterRemediation = @("GBPUSD known-good control", "EURGBP known-good control")
    usdJpyFirstPostRemediationCandidateRecommended = $false
    audUsdFirstPostRemediationCandidateRecommended = $false
}

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

$plan = [ordered]@{
    phase = "7K7"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    remediationScope = "ExternalSessionEndpointEnvironment"
    priorFreezeReason = "KnownGoodControlFailedBeforeLogonAfterPriorSuccess"
    globalExternalAttemptFreezeRemains = $true
    externalRunAttemptedInThisPhase = $false
    snapshotRunInThisPhase = $false
    replayRunInThisPhase = $false
    anyInstrumentRunInThisPhase = $false
    runtimePowerAdded = $false
    apiWorkerRemainFakeLmaxGatewayOnly = $true
    securityIdIssueNotProven = $true
    tokyo600xNotJustified = $true
    marketDataRequestRejectNotObserved = $true
    remediationChecklist = $remediationChecklist
    requiredEvidenceBeforeAnyFutureRun = $requiredEvidence
    recommendedOrderOfOperations = $recommendedOrder
    futureRunReopenPolicy = $futureRunReopenPolicy
    sourcePhase7K6FreezeGate = $Phase7K6FreezeGateFile
    sourcePhase7K6DiagnosticSummary = $Phase7K6DiagnosticSummaryFile
    sourcePhase7K6Note = $Phase7K6NoteFile
    sourcePhase7K3CompletedGate = $Phase7K3CompletedGateFile
    sourcePhase7K5Interpretation = $Phase7K5InterpretationFile
    disallowedActions = $disallowedActions
    noSensitiveContent = $true
    finalDecision = "PASS_REMEDIATION_PLAN_RECORDED"
}

$gate = [ordered]@{
    phase = "7K7"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    globalExternalAttemptFreezeRemains = $true
    anyInstrumentExternalRunAllowed = $false
    externalAdditionalInstrumentAttemptsCurrentlyAllowed = $false
    gbpusdControlRunAllowed = $false
    eurgbpControlRunAllowed = $false
    audusdRetryAllowed = $false
    usdjpyRetryAllowed = $false
    nextInstrumentRunAllowed = $false
    futureExternalRunCanBeConsidered = $false
    remediationCompletionRecorded = $false
    freezeLifted = $false
    directRunAuthorization = $false
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
    allowedNextPhase = "Phase 7K8 - Record External Session Remediation Completion, No External Run"
    disallowedActions = $disallowedActions
    noSensitiveContent = $true
    apiWorkerGatewayMode = "FakeLmaxGateway"
    finalDecision = "PASS_REMEDIATION_PLAN_RECORDED"
}

$markdown = @"
# Phase 7K7 - External Session Remediation Plan

Earlier GBPUSD and EURGBP successes prove the Demo read-only MarketData workflow can work.

The later pre-logon failures, including the GBPUSD known-good control failure, point to external session, endpoint, or environment availability rather than an instrument-specific MarketDataRequest problem.

The current operational state is frozen. No future external run is allowed from this phase.

Remediation work is outside the app and must not expose secrets. Credential values must not be pasted, printed, logged, or stored.

No future run is allowed until remediation completion is recorded and a separate no-external-run gate explicitly lifts the freeze. The safest post-remediation candidate is a known-good control, such as GBPUSD or EURGBP, not USDJPY or AUDUSD.

Allowed next phase: Phase 7K8 - Record External Session Remediation Completion, No External Run.
"@

$outDir = Resolve-LocalPath $OutputDirectory
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$planPath = Join-Path $outDir "phase7k7-external-session-remediation-plan.json"
$gatePath = Join-Path $outDir "phase7k7-external-session-remediation-gate.json"
$markdownPath = Join-Path $outDir "phase7k7-external-session-remediation-plan.md"

$planJson = $plan | ConvertTo-Json -Depth 12
$gateJson = $gate | ConvertTo-Json -Depth 12
if (($planJson + "`n" + $gateJson + "`n" + $markdown) -match $sensitivePattern) {
    throw "Generated Phase 7K7 artifacts contain credential-shaped or raw FIX content."
}

$planJson | Set-Content -LiteralPath $planPath -Encoding UTF8
$gateJson | Set-Content -LiteralPath $gatePath -Encoding UTF8
$markdown | Set-Content -LiteralPath $markdownPath -Encoding UTF8

Write-Host "Phase: 7K7"
Write-Host "FinalDecision: PASS_REMEDIATION_PLAN_RECORDED"
Write-Host "RemediationPlan: $planPath"
Write-Host "RemediationGate: $gatePath"
Write-Host "MarkdownPlan: $markdownPath"
