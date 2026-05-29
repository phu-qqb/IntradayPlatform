param(
    [string]$Phase7K15EvidencePackFile = "artifacts/readiness/phase7k15-final-additional-instrument-readonly-evidence-pack.json",
    [string]$Phase7K15DayClosureGateFile = "artifacts/readiness/phase7k15-final-additional-instrument-day-closure-gate.json",
    [string]$Phase7K15MarkdownPackFile = "artifacts/readiness/phase7k15-final-additional-instrument-readonly-evidence-pack.md",
    [string]$Phase7K14PortfolioGateFile = "artifacts/readiness/phase7k14-post-remediation-additional-instrument-portfolio-decision-gate.json",
    [string]$Phase7K13AudusdClosureGateFile = "artifacts/readiness/phase7k13-audusd-post-remediation-success-closure-gate.json",
    [string]$Phase7K10GbpusdInterpretationFile = "artifacts/readiness/phase7k10-gbpusd-post-remediation-known-good-control-interpretation.json",
    [string]$EurgbpReplayFile = "artifacts/readiness/phase7h-additional-instrument-evidence-replay-eurgbp.json",
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

function Read-TextArtifact([string]$PathValue, [string]$Label) {
    $resolved = Resolve-LocalPath $PathValue
    if (-not (Test-Path -LiteralPath $resolved)) { throw "$Label is missing: $resolved" }
    $raw = Get-Content -LiteralPath $resolved -Raw
    $safe = $raw -replace 'credential|Credential|secret|Secret','SAFE_METADATA'
    if ($safe -match $sensitivePattern) { throw "$Label contains credential-shaped or raw FIX content." }
    return @{ path = $resolved; text = $raw }
}

$pack = Read-JsonArtifact $Phase7K15EvidencePackFile "Phase 7K15 evidence pack"
$dayGate = Read-JsonArtifact $Phase7K15DayClosureGateFile "Phase 7K15 day-closure gate"
$markdownPack = Read-TextArtifact $Phase7K15MarkdownPackFile "Phase 7K15 markdown pack"
$portfolioGate = Read-JsonArtifact $Phase7K14PortfolioGateFile "Phase 7K14 portfolio gate"
$audusdClosureGate = Read-JsonArtifact $Phase7K13AudusdClosureGateFile "Phase 7K13 AUDUSD closure gate"
$gbpusdInterpretation = Read-JsonArtifact $Phase7K10GbpusdInterpretationFile "Phase 7K10 GBPUSD interpretation"
$eurgbpReplay = Read-JsonArtifact $EurgbpReplayFile "EURGBP replay report"

if ([string]$pack.json.phase -ne "7K15" -or [string]$pack.json.finalDecision -ne "PASS_FINAL_ADDITIONAL_INSTRUMENT_EVIDENCE_PACK_CLOSED" -or -not [bool]$pack.json.dayClosure -or -not [bool]$pack.json.externalAttemptCycleClosed) {
    throw "Phase 7K15 evidence pack is not closed."
}
if ([string]$dayGate.json.phase -ne "7K15" -or [string]$dayGate.json.finalDecision -ne "PASS_FINAL_ADDITIONAL_INSTRUMENT_EVIDENCE_PACK_CLOSED" -or [bool]$dayGate.json.anyInstrumentExternalRunAllowed -or [bool]$dayGate.json.futureExternalRunCanBeConsidered) {
    throw "Phase 7K15 day-closure gate is not in the expected no-external-run state."
}
if ([string]$portfolioGate.json.finalDecision -ne "PASS_PORTFOLIO_DECISION_STOP_EXTERNAL_ATTEMPTS_FOR_DAY") {
    throw "Phase 7K14 portfolio gate is not stopped for day."
}
if ([string]$audusdClosureGate.json.finalDecision -ne "PASS_AUDUSD_POST_REMEDIATION_SUCCESS_CLOSED" -or -not [bool]$audusdClosureGate.json.audusdRecovered) {
    throw "Phase 7K13 AUDUSD closure is not recovered."
}
if ([string]$gbpusdInterpretation.json.finalDecision -ne "PASS_POST_REMEDIATION_CONTROL_RECOVERED" -or -not [bool]$gbpusdInterpretation.json.knownGoodControlRecovered) {
    throw "Phase 7K10 GBPUSD control is not recovered."
}
if ([string]$eurgbpReplay.json.finalDecision -ne "PASS" -or [string]$eurgbpReplay.json.replayStatus -ne "Completed" -or [int]$eurgbpReplay.json.observationCount -ne 0) {
    throw "EURGBP replay report is not PASS / Completed / zero observations."
}

$successful = @($pack.json.successfulReadOnlyEvidenceInstruments)
$parked = @($pack.json.parkedInstruments)
foreach ($instrument in @("GBPUSD", "EURGBP", "AUDUSD")) {
    if ($successful -notcontains $instrument) { throw "Evidence pack does not include successful instrument $instrument." }
}
if ($parked -notcontains "USDJPY") { throw "Evidence pack does not park USDJPY." }

$allowedNextPhase = "Phase 7L - Readiness UI/Status Update Planning, No External Run"
$finalDecision = "PASS_FINAL_OPERATOR_SIGNOFF_RECORDED"
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

$signoff = [ordered]@{
    phase = "7K16"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    signoffType = "FinalAdditionalInstrumentReadOnlyEvidenceCycleSignoff"
    operatorSignoffRecorded = $true
    evidenceCycleClosed = $true
    externalAttemptCycleClosed = $true
    successfulReadOnlyEvidenceInstruments = @("GBPUSD", "EURGBP", "AUDUSD")
    parkedInstruments = @("USDJPY")
    eurusdPriorWorkflowClosed = $true
    lmaxDemoReadOnlyEvidenceCompleteForCurrentCycle = $true
    marketDataOnlyEvidenceAvailable = $true
    dayClosureGateDecision = "PASS_FINAL_ADDITIONAL_INSTRUMENT_EVIDENCE_PACK_CLOSED"
    finalOperationalState = "NoExternalAttemptsAllowed"
    anyInstrumentExternalRunAllowed = $false
    externalAdditionalInstrumentAttemptsCurrentlyAllowed = $false
    futureExternalRunCanBeConsidered = $false
    directRunAuthorization = $false
    immediateNextExternalRunRecommended = $false
    orderSubmissionObserved = $false
    schedulerOrPollingObserved = $false
    runtimeShadowReplaySubmitObserved = $false
    tradingMutationObserved = $false
    gatewayRegistrationObserved = $false
    credentialValuesReturned = $false
    noSensitiveContent = $true
    apiWorkerRemainFakeLmaxGatewayOnly = $true
    knownLocalIssue = "LocalhostApiHealthTimeoutAffectedOptionalReplayOnly"
    usdJpyStatus = "ParkedSeparateTroubleshootingRail"
    sourcePhase7K15EvidencePackPath = $pack.path
    sourcePhase7K15DayClosureGatePath = $dayGate.path
    recommendedNextAction = @(
        "Stop all external attempts.",
        "Preserve artifacts.",
        "Address local API health timeout separately.",
        "Keep USDJPY troubleshooting separate.",
        "Continue only with documentation, readiness UI/status work, or local health follow-up unless a new explicit no-external-run gate reopens a future one-instrument attempt."
    )
    allowedNextPhase = $allowedNextPhase
    disallowedActions = $disallowedActions
    finalDecision = $finalDecision
}

$docsUpdated = @(
    "docs/LMAX_READONLY_RUNTIME_PHASE_GATES.md",
    "docs/OPERATIONAL_READINESS_CHECKLIST.md"
)
$docsSkipped = @(
    [ordered]@{ file = "README.md"; reason = "Skipped to avoid broad noisy edits; phase-level readiness docs were updated instead." },
    [ordered]@{ file = "docs/LOCAL_RUNBOOK.md"; reason = "Skipped because Phase 7K16 is signoff/documentation only and does not change run procedures." },
    [ordered]@{ file = "docs/OPERATOR_MANUAL.md"; reason = "Skipped to keep final signoff references localized to readiness/phase-gate docs." }
)

$docSummary = [ordered]@{
    phase = "7K16"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    filesUpdated = $docsUpdated
    filesNotUpdatedWithReason = $docsSkipped
    readinessNarrative = "Phase 7K16 records final operator signoff for the closed additional-instrument Demo read-only evidence cycle: GBPUSD, EURGBP, and AUDUSD are successful current-cycle evidence instruments; USDJPY remains parked separately; no further external attempts are allowed."
    evidenceLinks = [ordered]@{
        phase7K15EvidencePack = $pack.path
        phase7K15DayClosureGate = $dayGate.path
        phase7K14PortfolioGate = $portfolioGate.path
        phase7K13AudusdClosureGate = $audusdClosureGate.path
        phase7K10GbpusdInterpretation = $gbpusdInterpretation.path
        eurgbpReplay = $eurgbpReplay.path
    }
    safetyPosture = [ordered]@{
        finalOperationalState = "NoExternalAttemptsAllowed"
        apiWorkerGatewayMode = "FakeLmaxGateway"
        orderSubmissionObserved = $false
        schedulerOrPollingObserved = $false
        runtimeShadowReplaySubmitObserved = $false
        tradingMutationObserved = $false
        gatewayRegistrationObserved = $false
    }
    unresolvedItems = @(
        "USDJPY parked troubleshooting rail.",
        "Localhost API health timeout for optional replay.",
        "Existing NU1903 warnings on System.Security.Cryptography.Xml observed during validation."
    )
    noSensitiveContent = $true
    finalDecision = $finalDecision
}

$note = @"
# Phase 7K16 - Final Operator Signoff

The additional-instrument read-only evidence cycle is closed.

Successful current-cycle additional-instrument evidence includes GBPUSD, EURGBP, and AUDUSD. EURUSD prior workflow was already closed.

USDJPY remains parked separately on its troubleshooting rail and is not part of this success closure.

No trading runtime powers were enabled. No order path, scheduler/polling, runtime shadow replay submit, gateway registration, trading mutation, batch executor, retry loop, wrapper relaxation, SecurityID switch, or Tokyo 600x switch was added. API/Worker remain FakeLmaxGateway only.

The localhost API health timeout affected optional replay checks only and should be addressed separately.

The next work should be documentation, readiness UI/status updates, or local API health follow-up, not more external attempts.

Allowed next phase: $allowedNextPhase.
"@

$outDir = Resolve-LocalPath $OutputDirectory
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$signoffPath = Join-Path $outDir "phase7k16-final-operator-signoff.json"
$summaryPath = Join-Path $outDir "phase7k16-final-readiness-documentation-update-summary.json"
$notePath = Join-Path $outDir "phase7k16-final-operator-signoff-note.md"

$signoffJson = $signoff | ConvertTo-Json -Depth 12
$summaryJson = $docSummary | ConvertTo-Json -Depth 12
if (($signoffJson + "`n" + $summaryJson + "`n" + $note) -match $sensitivePattern) {
    throw "Generated Phase 7K16 artifacts contain credential-shaped or raw FIX content."
}

$signoffJson | Set-Content -LiteralPath $signoffPath -Encoding UTF8
$summaryJson | Set-Content -LiteralPath $summaryPath -Encoding UTF8
$note | Set-Content -LiteralPath $notePath -Encoding UTF8

Write-Host "Phase: 7K16"
Write-Host "SignoffType: FinalAdditionalInstrumentReadOnlyEvidenceCycleSignoff"
Write-Host "FinalDecision: $finalDecision"
Write-Host "AllowedNextPhase: $allowedNextPhase"
Write-Host "Signoff: $signoffPath"
Write-Host "DocumentationSummary: $summaryPath"
Write-Host "Note: $notePath"
