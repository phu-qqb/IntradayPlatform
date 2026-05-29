param(
    [string]$Phase7K14PortfolioGateFile = "artifacts/readiness/phase7k14-post-remediation-additional-instrument-portfolio-decision-gate.json",
    [string]$Phase7K14PortfolioSummaryFile = "artifacts/readiness/phase7k14-post-remediation-additional-instrument-portfolio-summary.json",
    [string]$GbpusdInitialSnapshotFile = "artifacts/lmax-readonly-runtime-additional-snapshot/gbpusd/lmax-readonly-gbpusd-demo-snapshot-result-20260511-103318.json",
    [string]$GbpusdPostRemediationSnapshotFile = "artifacts/lmax-readonly-runtime-additional-snapshot/gbpusd/lmax-readonly-gbpusd-demo-snapshot-result-20260511-201523.json",
    [string]$GbpusdPostRemediationEvidencePreviewFile = "artifacts/lmax-readonly-runtime-additional-snapshot/gbpusd/evidence-preview/lmax-readonly-gbpusd-evidence-preview-20260511-201538.json",
    [string]$GbpusdPostRemediationClosureFile = "artifacts/lmax-readonly-runtime-additional-snapshot/gbpusd/closure/lmax-readonly-gbpusd-closure-manifest-20260511-201559.json",
    [string]$Phase7K10GbpusdInterpretationFile = "artifacts/readiness/phase7k10-gbpusd-post-remediation-known-good-control-interpretation.json",
    [string]$EurgbpSnapshotFile = "artifacts/lmax-readonly-runtime-additional-snapshot/eurgbp/lmax-readonly-eurgbp-demo-snapshot-result-20260511-163141.json",
    [string]$EurgbpReviewFile = "artifacts/readiness/phase7h-additional-instrument-snapshot-review-eurgbp.json",
    [string]$EurgbpEvidencePreviewFile = "artifacts/lmax-readonly-runtime-additional-snapshot/eurgbp/evidence-preview/lmax-readonly-eurgbp-evidence-preview-20260511-165605.json",
    [string]$EurgbpReplayFile = "artifacts/readiness/phase7h-additional-instrument-evidence-replay-eurgbp.json",
    [string]$AudusdSnapshotFile = "artifacts/lmax-readonly-runtime-additional-snapshot/audusd/lmax-readonly-audusd-demo-snapshot-result-20260511-202943.json",
    [string]$AudusdEvidencePreviewFile = "artifacts/lmax-readonly-runtime-additional-snapshot/audusd/evidence-preview/lmax-readonly-audusd-evidence-preview-20260511-203000.json",
    [string]$AudusdClosureFile = "artifacts/lmax-readonly-runtime-additional-snapshot/audusd/closure/lmax-readonly-audusd-closure-manifest-20260511-203009.json",
    [string]$Phase7K12AudusdInterpretationFile = "artifacts/readiness/phase7k12-audusd-post-remediation-snapshot-interpretation.json",
    [string]$Phase7K13AudusdClosureGateFile = "artifacts/readiness/phase7k13-audusd-post-remediation-success-closure-gate.json",
    [string]$Phase7K13AudusdClosureSummaryFile = "artifacts/readiness/phase7k13-audusd-post-remediation-success-closure-summary.json",
    [string]$UsdJpyRepeatedFailureFile = "artifacts/readiness/phase7i4-usdjpy-repeated-failedsafe-pattern-analysis.json",
    [string]$UsdJpyTroubleshootingGateFile = "artifacts/readiness/phase7i6-usdjpy-operator-troubleshooting-decision-gate.json",
    [string]$UsdJpyFirstAttemptFile = "artifacts/lmax-readonly-runtime-additional-snapshot/usdjpy/lmax-readonly-usdjpy-demo-snapshot-result-20260511-181833.json",
    [string]$UsdJpyRetryAttemptFile = "artifacts/lmax-readonly-runtime-additional-snapshot/usdjpy/lmax-readonly-usdjpy-demo-snapshot-result-20260511-182651.json",
    [string]$Phase7K6FreezeSummaryFile = "artifacts/readiness/phase7k6-global-external-attempt-freeze-summary.json",
    [string]$Phase7K7RemediationPlanFile = "artifacts/readiness/phase7k7-external-session-remediation-plan.json",
    [string]$Phase7K8RemediationCompletionGateFile = "artifacts/readiness/phase7k8-external-session-remediation-completion-gate.json",
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

function New-EvidenceItem([string]$Name, [string]$Kind, [hashtable]$Artifact, [string]$Status, [string]$Decision = "") {
    return [ordered]@{
        name = $Name
        kind = $Kind
        path = $Artifact.path
        status = $Status
        decision = $Decision
        noSensitiveContent = $true
    }
}

$phase7K14Gate = Read-JsonArtifact $Phase7K14PortfolioGateFile "Phase 7K14 portfolio gate"
$phase7K14Summary = Read-JsonArtifact $Phase7K14PortfolioSummaryFile "Phase 7K14 portfolio summary"
$gbpusdInitial = Read-JsonArtifact $GbpusdInitialSnapshotFile "GBPUSD initial success"
$gbpusdPost = Read-JsonArtifact $GbpusdPostRemediationSnapshotFile "GBPUSD post-remediation success"
$gbpusdPreview = Read-JsonArtifact $GbpusdPostRemediationEvidencePreviewFile "GBPUSD post-remediation evidence preview"
$gbpusdClosure = Read-JsonArtifact $GbpusdPostRemediationClosureFile "GBPUSD post-remediation closure"
$phase7K10 = Read-JsonArtifact $Phase7K10GbpusdInterpretationFile "Phase 7K10 GBPUSD interpretation"
$eurgbpSnapshot = Read-JsonArtifact $EurgbpSnapshotFile "EURGBP snapshot"
$eurgbpReview = Read-JsonArtifact $EurgbpReviewFile "EURGBP review"
$eurgbpPreview = Read-JsonArtifact $EurgbpEvidencePreviewFile "EURGBP evidence preview"
$eurgbpReplay = Read-JsonArtifact $EurgbpReplayFile "EURGBP replay"
$audusdSnapshot = Read-JsonArtifact $AudusdSnapshotFile "AUDUSD snapshot"
$audusdPreview = Read-JsonArtifact $AudusdEvidencePreviewFile "AUDUSD evidence preview"
$audusdClosure = Read-JsonArtifact $AudusdClosureFile "AUDUSD closure"
$phase7K12 = Read-JsonArtifact $Phase7K12AudusdInterpretationFile "Phase 7K12 AUDUSD interpretation"
$phase7K13Gate = Read-JsonArtifact $Phase7K13AudusdClosureGateFile "Phase 7K13 AUDUSD closure gate"
$phase7K13Summary = Read-JsonArtifact $Phase7K13AudusdClosureSummaryFile "Phase 7K13 AUDUSD closure summary"
$usdJpyRepeated = Read-JsonArtifact $UsdJpyRepeatedFailureFile "USDJPY repeated failure analysis"
$usdJpyTroubleshooting = Read-JsonArtifact $UsdJpyTroubleshootingGateFile "USDJPY troubleshooting gate"
$usdJpyFirst = Read-JsonArtifact $UsdJpyFirstAttemptFile "USDJPY first failed-safe attempt"
$usdJpyRetry = Read-JsonArtifact $UsdJpyRetryAttemptFile "USDJPY retry failed-safe attempt"
$phase7K6Summary = Read-JsonArtifact $Phase7K6FreezeSummaryFile "Phase 7K6 freeze summary"
$phase7K7Plan = Read-JsonArtifact $Phase7K7RemediationPlanFile "Phase 7K7 remediation plan"
$phase7K8Gate = Read-JsonArtifact $Phase7K8RemediationCompletionGateFile "Phase 7K8 remediation completion gate"
$phase7K9Gate = Read-JsonArtifact $Phase7K9SelectionGateFile "Phase 7K9 selection gate"

if ([string]$phase7K14Gate.json.finalDecision -ne "PASS_PORTFOLIO_DECISION_STOP_EXTERNAL_ATTEMPTS_FOR_DAY" -or -not [bool]$phase7K14Gate.json.externalAttemptCycleClosed) { throw "Phase 7K14 portfolio gate is not closed." }
if ([string]$phase7K14Summary.json.finalDecision -ne "PASS_PORTFOLIO_DECISION_STOP_EXTERNAL_ATTEMPTS_FOR_DAY") { throw "Phase 7K14 portfolio summary is invalid." }
foreach ($pair in @(
    @{ artifact = $gbpusdInitial; symbol = "GBPUSD"; status = "Completed" },
    @{ artifact = $gbpusdPost; symbol = "GBPUSD"; status = "Completed" },
    @{ artifact = $eurgbpSnapshot; symbol = "EURGBP"; status = "Completed" },
    @{ artifact = $audusdSnapshot; symbol = "AUDUSD"; status = "Completed" }
)) {
    if ([string]$pair.artifact.json.symbol -ne $pair.symbol -or [string]$pair.artifact.json.status -ne $pair.status -or -not [bool]$pair.artifact.json.snapshotReceived) {
        throw "$($pair.symbol) successful evidence is not Completed with snapshotReceived=true."
    }
}
if ([string]$gbpusdPreview.json.evidenceMode -ne "MarketDataOnly" -or [string]$gbpusdPreview.json.marketData.status -ne "Ok") { throw "GBPUSD preview is not MarketDataOnly / Ok." }
if ([string]$eurgbpPreview.json.evidenceMode -ne "MarketDataOnly" -or [string]$eurgbpPreview.json.marketData.status -ne "Ok") { throw "EURGBP preview is not MarketDataOnly / Ok." }
if ([string]$audusdPreview.json.evidenceMode -ne "MarketDataOnly" -or [string]$audusdPreview.json.marketData.status -ne "Ok") { throw "AUDUSD preview is not MarketDataOnly / Ok." }
if ([string]$gbpusdClosure.json.finalClosureDecision -ne "PASS" -or [string]$audusdClosure.json.finalClosureDecision -ne "PASS") { throw "GBPUSD or AUDUSD closure is not PASS." }
if ([string]$eurgbpReplay.json.finalDecision -ne "PASS" -or [string]$eurgbpReplay.json.replayStatus -ne "Completed" -or [int]$eurgbpReplay.json.observationCount -ne 0) { throw "EURGBP local replay is not PASS / Completed / zero observations." }
if ([string]$phase7K10.json.finalDecision -ne "PASS_POST_REMEDIATION_CONTROL_RECOVERED") { throw "Phase 7K10 is not recovered." }
if ([string]$phase7K12.json.finalDecision -ne "PASS_AUDUSD_POST_REMEDIATION_RECOVERED") { throw "Phase 7K12 is not recovered." }
if ([string]$phase7K13Gate.json.finalDecision -ne "PASS_AUDUSD_POST_REMEDIATION_SUCCESS_CLOSED") { throw "Phase 7K13 is not closed." }
if ([string]$usdJpyRepeated.json.phase -ne "7I4" -or -not [bool]$usdJpyRepeated.json.externalRetryStopRecommended) { throw "USDJPY repeated-failure artifact does not keep retries stopped." }
if ([string]$usdJpyTroubleshooting.json.phase -ne "7I6" -or [bool]$usdJpyTroubleshooting.json.thirdRetryCurrentlyAllowed) { throw "USDJPY troubleshooting gate does not keep USDJPY parked." }
foreach ($failed in @($usdJpyFirst, $usdJpyRetry)) {
    if ([string]$failed.json.symbol -ne "USDJPY" -or [string]$failed.json.status -ne "FailedSafeConnectionError" -or [bool]$failed.json.logonAttempted -or [bool]$failed.json.snapshotRequestAttempted) {
        throw "USDJPY failed-safe artifact is not the expected before-logon failure."
    }
}

$evidenceItems = @(
    (New-EvidenceItem "GBPUSD initial success" "SnapshotArtifact" $gbpusdInitial "Completed" "PASS"),
    (New-EvidenceItem "GBPUSD post-remediation known-good control success" "SnapshotArtifact" $gbpusdPost "Completed" "PASS"),
    (New-EvidenceItem "GBPUSD post-remediation evidence preview" "EvidencePreview" $gbpusdPreview "MarketDataOnly" "Ok"),
    (New-EvidenceItem "GBPUSD post-remediation closure" "ClosureManifest" $gbpusdClosure "CompletedWithBook" "PASS"),
    (New-EvidenceItem "GBPUSD post-remediation interpretation" "ReadinessDecision" $phase7K10 "Recovered" "PASS_POST_REMEDIATION_CONTROL_RECOVERED"),
    (New-EvidenceItem "EURGBP success" "SnapshotArtifact" $eurgbpSnapshot "Completed" "PASS"),
    (New-EvidenceItem "EURGBP review" "ReadinessDecision" $eurgbpReview "CompletedWithBook" "PASS"),
    (New-EvidenceItem "EURGBP evidence preview" "EvidencePreview" $eurgbpPreview "MarketDataOnly" "Ok"),
    (New-EvidenceItem "EURGBP local replay" "LocalReplayReport" $eurgbpReplay "Completed" "PASS"),
    (New-EvidenceItem "AUDUSD post-remediation success" "SnapshotArtifact" $audusdSnapshot "Completed" "PASS"),
    (New-EvidenceItem "AUDUSD evidence preview" "EvidencePreview" $audusdPreview "MarketDataOnly" "Ok"),
    (New-EvidenceItem "AUDUSD closure" "ClosureManifest" $audusdClosure "CompletedWithBook" "PASS"),
    (New-EvidenceItem "AUDUSD post-remediation interpretation" "ReadinessDecision" $phase7K12 "Recovered" "PASS_AUDUSD_POST_REMEDIATION_RECOVERED"),
    (New-EvidenceItem "AUDUSD post-remediation closure gate" "ReadinessGate" $phase7K13Gate "Closed" "PASS_AUDUSD_POST_REMEDIATION_SUCCESS_CLOSED"),
    (New-EvidenceItem "USDJPY repeated failed-safe diagnostics" "ReadinessDiagnosis" $usdJpyRepeated "Parked" "PASS_WITH_KNOWN_WARNINGS"),
    (New-EvidenceItem "USDJPY troubleshooting decision gate" "ReadinessGate" $usdJpyTroubleshooting "Parked" "PASS_WITH_ACTION_REQUIRED"),
    (New-EvidenceItem "USDJPY first failed-safe attempt" "SnapshotArtifact" $usdJpyFirst "FailedSafeConnectionError" "PASS_WITH_KNOWN_WARNINGS"),
    (New-EvidenceItem "USDJPY retry failed-safe attempt" "SnapshotArtifact" $usdJpyRetry "FailedSafeConnectionError" "PASS_WITH_KNOWN_WARNINGS"),
    (New-EvidenceItem "Phase 7K6 global freeze summary" "ReadinessSummary" $phase7K6Summary "Frozen" "PASS_GLOBAL_FREEZE_RECORDED"),
    (New-EvidenceItem "Phase 7K7 remediation plan" "ReadinessPlan" $phase7K7Plan "Recorded" "PASS_REMEDIATION_PLAN_RECORDED"),
    (New-EvidenceItem "Phase 7K8 remediation completion gate" "ReadinessGate" $phase7K8Gate "Recorded" "PASS_REMEDIATION_COMPLETION_RECORDED"),
    (New-EvidenceItem "Phase 7K9 freeze lift selection gate" "ReadinessGate" $phase7K9Gate "Recorded" "PASS_FREEZE_LIFT_SELECTION_RECORDED"),
    (New-EvidenceItem "Phase 7K14 portfolio decision gate" "ReadinessGate" $phase7K14Gate "Closed" "PASS_PORTFOLIO_DECISION_STOP_EXTERNAL_ATTEMPTS_FOR_DAY")
)

$allowedNextPhase = "Phase 7K16 - Final Operator Signoff and Readiness Documentation Update, No External Run"
$finalDecision = "PASS_FINAL_ADDITIONAL_INSTRUMENT_EVIDENCE_PACK_CLOSED"

$manifest = [ordered]@{
    phase = "7K15"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    evidencePackType = "FinalAdditionalInstrumentReadOnlyEvidencePack"
    dayClosure = $true
    externalAttemptCycleClosed = $true
    portfolioDecision = "StopExternalAttemptsForDay"
    successfulReadOnlyEvidenceInstruments = @("GBPUSD", "EURGBP", "AUDUSD")
    parkedInstruments = @("USDJPY")
    evidenceItems = $evidenceItems
    lmaxDemoReadOnlyEvidenceCompleteForCurrentCycle = $true
    marketDataOnlyEvidenceAvailable = $true
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
    recommendedNextAction = @(
        "Stop all external attempts for the day.",
        "Preserve artifacts.",
        "Review local API health timeout separately before making optional replay mandatory.",
        "Continue with documentation, readiness UI/status updates, or final operator signoff only.",
        "Keep USDJPY separate and parked."
    )
    allowedNextPhase = $allowedNextPhase
    finalDecision = $finalDecision
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

$gate = [ordered]@{
    phase = "7K15"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    dayClosureDecision = "CloseAdditionalInstrumentExternalAttemptCycle"
    externalAttemptCycleClosed = $true
    anyInstrumentExternalRunAllowed = $false
    externalAdditionalInstrumentAttemptsCurrentlyAllowed = $false
    gbpusdControlRunAllowed = $false
    eurgbpControlRunAllowed = $false
    audusdRetryAllowed = $false
    usdjpyRetryAllowed = $false
    nextInstrumentRunAllowed = $false
    directRunAuthorization = $false
    futureExternalRunCanBeConsidered = $false
    immediateNextExternalRunRecommended = $false
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
    apiWorkerRemainFakeLmaxGatewayOnly = $true
    requiredBeforeAnyFutureExternalAttempt = @(
        "A new no-external-run decision gate must explicitly reopen one instrument.",
        "The Phase 7H wrapper must still require -AllowExternalConnections and -ConfirmDemoReadOnly.",
        "A human-provided reason remains required.",
        "No batch, loop, retry mechanism, scheduler, order path, runtime shadow replay submit, gateway registration, or trading-state mutation may be introduced."
    )
    allowedNextPhase = $allowedNextPhase
    disallowedActions = $disallowedActions
    noSensitiveContent = $true
    finalDecision = $finalDecision
}

$report = @"
# Phase 7K15 - Final Additional-Instrument Read-Only Evidence Pack

The additional-instrument external attempt cycle is closed for the day.

## What Succeeded

- GBPUSD initial market-hours read-only snapshot completed with a two-entry book.
- GBPUSD post-remediation known-good control completed with a two-entry book.
- EURGBP completed its market-hours snapshot, MarketDataOnly evidence preview, and local replay.
- AUDUSD completed its post-remediation read-only snapshot and MarketDataOnly evidence preview.

## What Remains Separate

USDJPY remains parked after repeated pre-logon failed-safe attempts. It is a separate troubleshooting rail and is not mixed into this success closure.

## Remediation Interpretation

The pre-logon failures were likely external/session/environment availability because GBPUSD and AUDUSD both recovered after remediation. No instrument-level reject was observed for AUDUSD, and no Tokyo 600x switch is justified.

## Safety Posture

No orders, scheduler/polling, runtime shadow replay submit, trading mutation, real gateway registration, batch executor, retry loop, wrapper weakening, SecurityID switch, or Tokyo 600x switch was added. API/Worker remain FakeLmaxGateway only.

## Known Local Issue

The localhost API health timeout affected optional replay only. It is not an LMAX evidence failure and should be addressed separately before making local replay mandatory.

## Closure Decision

No more external attempts should be run today. Preserve the sanitized artifacts and continue with final operator signoff and readiness documentation only.

Allowed next phase: $allowedNextPhase.
"@

$outDir = Resolve-LocalPath $OutputDirectory
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$manifestPath = Join-Path $outDir "phase7k15-final-additional-instrument-readonly-evidence-pack.json"
$gatePath = Join-Path $outDir "phase7k15-final-additional-instrument-day-closure-gate.json"
$reportPath = Join-Path $outDir "phase7k15-final-additional-instrument-readonly-evidence-pack.md"

$manifestJson = $manifest | ConvertTo-Json -Depth 16
$gateJson = $gate | ConvertTo-Json -Depth 12
if (($manifestJson + "`n" + $gateJson + "`n" + $report) -match $sensitivePattern) {
    throw "Generated Phase 7K15 artifacts contain credential-shaped or raw FIX content."
}

$manifestJson | Set-Content -LiteralPath $manifestPath -Encoding UTF8
$gateJson | Set-Content -LiteralPath $gatePath -Encoding UTF8
$report | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host "Phase: 7K15"
Write-Host "EvidencePackType: FinalAdditionalInstrumentReadOnlyEvidencePack"
Write-Host "DayClosure: true"
Write-Host "FinalDecision: $finalDecision"
Write-Host "AllowedNextPhase: $allowedNextPhase"
Write-Host "EvidencePack: $manifestPath"
Write-Host "DayClosureGate: $gatePath"
Write-Host "MarkdownReport: $reportPath"
