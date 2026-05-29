param(
    [string]$Phase7K13ClosureGateFile = "artifacts/readiness/phase7k13-audusd-post-remediation-success-closure-gate.json",
    [string]$Phase7K13ClosureSummaryFile = "artifacts/readiness/phase7k13-audusd-post-remediation-success-closure-summary.json",
    [string]$Phase7K12AudusdInterpretationFile = "artifacts/readiness/phase7k12-audusd-post-remediation-snapshot-interpretation.json",
    [string]$Phase7K10GbpusdInterpretationFile = "artifacts/readiness/phase7k10-gbpusd-post-remediation-known-good-control-interpretation.json",
    [string]$EarlierGbpusdSnapshotFile = "artifacts/lmax-readonly-runtime-additional-snapshot/gbpusd/lmax-readonly-gbpusd-demo-snapshot-result-20260511-103318.json",
    [string]$EarlierEurgbpSnapshotFile = "artifacts/lmax-readonly-runtime-additional-snapshot/eurgbp/lmax-readonly-eurgbp-demo-snapshot-result-20260511-163141.json",
    [string]$AudusdSnapshotFile = "artifacts/lmax-readonly-runtime-additional-snapshot/audusd/lmax-readonly-audusd-demo-snapshot-result-20260511-202943.json",
    [string]$AudusdEvidencePreviewFile = "artifacts/lmax-readonly-runtime-additional-snapshot/audusd/evidence-preview/lmax-readonly-audusd-evidence-preview-20260511-203000.json",
    [string]$UsdJpyRepeatedFailureFile = "artifacts/readiness/phase7i4-usdjpy-repeated-failedsafe-pattern-analysis.json",
    [string]$UsdJpyTroubleshootingGateFile = "artifacts/readiness/phase7i6-usdjpy-operator-troubleshooting-decision-gate.json",
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

$phase7K13Gate = Read-JsonArtifact $Phase7K13ClosureGateFile "Phase 7K13 closure gate"
$phase7K13Summary = Read-JsonArtifact $Phase7K13ClosureSummaryFile "Phase 7K13 closure summary"
$phase7K12 = Read-JsonArtifact $Phase7K12AudusdInterpretationFile "Phase 7K12 AUDUSD interpretation"
$phase7K10 = Read-JsonArtifact $Phase7K10GbpusdInterpretationFile "Phase 7K10 GBPUSD interpretation"
$gbpusd = Read-JsonArtifact $EarlierGbpusdSnapshotFile "Earlier GBPUSD snapshot"
$eurgbp = Read-JsonArtifact $EarlierEurgbpSnapshotFile "Earlier EURGBP snapshot"
$audusd = Read-JsonArtifact $AudusdSnapshotFile "AUDUSD post-remediation snapshot"
$audusdPreview = Read-JsonArtifact $AudusdEvidencePreviewFile "AUDUSD evidence preview"
$usdJpyRepeated = Read-JsonArtifact $UsdJpyRepeatedFailureFile "USDJPY repeated failure analysis"
$usdJpyTroubleshooting = Read-JsonArtifact $UsdJpyTroubleshootingGateFile "USDJPY troubleshooting gate"

if ([string]$phase7K13Gate.json.finalDecision -ne "PASS_AUDUSD_POST_REMEDIATION_SUCCESS_CLOSED" -or -not [bool]$phase7K13Gate.json.audusdRecovered) {
    throw "Phase 7K13 does not close AUDUSD successfully."
}
if ([string]$phase7K13Summary.json.finalDecision -ne "PASS_AUDUSD_POST_REMEDIATION_SUCCESS_CLOSED") {
    throw "Phase 7K13 summary is not closed."
}
if ([string]$phase7K12.json.finalDecision -ne "PASS_AUDUSD_POST_REMEDIATION_RECOVERED" -or -not [bool]$phase7K12.json.audusdRecovered) {
    throw "Phase 7K12 AUDUSD interpretation is not recovered."
}
if ([string]$phase7K10.json.finalDecision -ne "PASS_POST_REMEDIATION_CONTROL_RECOVERED" -or -not [bool]$phase7K10.json.knownGoodControlRecovered) {
    throw "Phase 7K10 GBPUSD control is not recovered."
}
if ([string]$gbpusd.json.symbol -ne "GBPUSD" -or [string]$gbpusd.json.status -ne "Completed" -or -not [bool]$gbpusd.json.snapshotReceived) {
    throw "Earlier GBPUSD evidence is not Completed."
}
if ([string]$eurgbp.json.symbol -ne "EURGBP" -or [string]$eurgbp.json.status -ne "Completed" -or -not [bool]$eurgbp.json.snapshotReceived) {
    throw "Earlier EURGBP evidence is not Completed."
}
if ([string]$audusd.json.symbol -ne "AUDUSD" -or [string]$audusd.json.status -ne "Completed" -or -not [bool]$audusd.json.snapshotReceived) {
    throw "AUDUSD post-remediation evidence is not Completed."
}
if ([string]$audusdPreview.json.evidenceMode -ne "MarketDataOnly" -or [string]$audusdPreview.json.marketData.status -ne "Ok") {
    throw "AUDUSD evidence preview is not MarketDataOnly / Ok."
}
if ([string]$usdJpyRepeated.json.phase -ne "7I4" -or -not [bool]$usdJpyRepeated.json.externalRetryStopRecommended -or -not [bool]$usdJpyRepeated.json.securityIdNotBlamed) {
    throw "USDJPY repeated-failure report does not support parked status."
}
if ([string]$usdJpyTroubleshooting.json.phase -ne "7I6" -or [bool]$usdJpyTroubleshooting.json.thirdRetryCurrentlyAllowed) {
    throw "USDJPY troubleshooting gate does not keep USDJPY parked."
}

$disallowedActions = @(
    "No external run in Phase 7K14.",
    "No AUDUSD run.",
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

$allowedNextPhase = "Phase 7K15 - Final Additional-Instrument Read-Only Evidence Pack and Day Closure, No External Run"

$gate = [ordered]@{
    phase = "7K14"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    portfolioDecision = "StopExternalAttemptsForDay"
    externalAttemptCycleClosed = $true
    successfulReadOnlyEvidenceInstruments = @("GBPUSD", "EURGBP", "AUDUSD")
    gbpusdEvidenceStatus = "Completed"
    eurgbpEvidenceStatus = "Completed"
    audusdEvidenceStatus = "Completed"
    audusdPostRemediationRecovered = $true
    gbpusdPostRemediationControlRecovered = $true
    usdJpyStatus = "ParkedSeparateTroubleshootingRail"
    usdJpyRetryRecommended = $false
    usdJpyRetryAllowed = $false
    eurgbpConfirmationRecommended = $false
    audusdRetryRecommended = $false
    nextInstrumentRunRecommended = $false
    batchExecutionRecommended = $false
    automaticRetryRecommended = $false
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
    batchExecutionAllowed = $false
    wrapperValidationWeakened = $false
    securityIdSwitchRecommended = $false
    tokyo600xSwitchRecommended = $false
    orderPathEnabled = $false
    schedulerOrPollingEnabled = $false
    runtimeShadowReplaySubmitEnabled = $false
    tradingMutationEnabled = $false
    gatewayRegistrationEnabled = $false
    sourcePhase7K13ClosureGatePath = $phase7K13Gate.path
    sourcePhase7K12AudusdInterpretationPath = $phase7K12.path
    sourcePhase7K10GbpusdInterpretationPath = $phase7K10.path
    sourceUsdJpyTroubleshootingGatePath = $usdJpyTroubleshooting.path
    recommendedNextAction = @(
        "Stop external attempts for the day.",
        "Preserve all successful sanitized artifacts.",
        "Review local API health timeout separately before relying on optional local replay workflows.",
        "Continue with documentation and operational readiness work, not more LMAX external attempts.",
        "Keep USDJPY as a separate future troubleshooting rail."
    )
    allowedNextPhase = $allowedNextPhase
    disallowedActions = $disallowedActions
    noSensitiveContent = $true
    apiWorkerGatewayMode = "FakeLmaxGateway"
    finalDecision = "PASS_PORTFOLIO_DECISION_STOP_EXTERNAL_ATTEMPTS_FOR_DAY"
}

$summary = [ordered]@{
    phase = "7K14"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    evidenceCycleOutcome = [ordered]@{
        eurusd = "Previously completed full Demo read-only MarketData workflow."
        gbpusd = "Completed initial market-hours snapshot and post-remediation known-good control recovered."
        eurgbp = "Completed market-hours snapshot, evidence preview, and local replay."
        audusd = "Completed post-remediation snapshot and evidence preview."
        usdjpy = "Remains parked after repeated pre-logon failures and is not mixed into this success closure."
    }
    recoveredEnvironmentSessionInterpretation = [ordered]@{
        preRemediationFailuresLikelyExternalSessionEnvironment = $true
        postRemediationGbpusdAndAudusdSuccessesSupportRecovery = $true
        invalidSecurityIdNotProven = $true
        tokyo600xNotJustified = $true
    }
    safetyPosture = [ordered]@{
        orderPathEnabled = $false
        schedulerOrPollingEnabled = $false
        runtimeShadowReplaySubmitEnabled = $false
        tradingMutationEnabled = $false
        gatewayRegistrationEnabled = $false
        apiWorkerGatewayMode = "FakeLmaxGateway"
    }
    knownLocalIssue = [ordered]@{
        localApiHealthTimedOutDuringOptionalReplayAfter7K10And7K12 = $true
        lmaxEvidenceFailure = $false
        recommendation = "Address separately before making local replay mandatory."
    }
    recommendedOperationalState = "StopExternalAttemptsForDay"
    noSensitiveContent = $true
    finalDecision = "PASS_PORTFOLIO_DECISION_STOP_EXTERNAL_ATTEMPTS_FOR_DAY"
}

$note = @"
# Phase 7K14 - Portfolio Decision

The current evidence set is enough for today. GBPUSD, EURGBP, and AUDUSD now provide sufficient successful Demo read-only MarketData evidence for this additional-instrument expansion cycle.

More external attempts would add risk and noise rather than useful evidence. USDJPY remains a separate parked troubleshooting rail and is not mixed into this success closure.

The post-remediation GBPUSD and AUDUSD recoveries support the interpretation that the earlier pre-logon failures were external/session/environment availability issues, not a demonstrated SecurityID or MarketDataRequest problem.

No trading functionality was enabled. No order path, scheduler, polling, runtime shadow replay submit, gateway registration, or trading-state mutation was added. API/Worker remain FakeLmaxGateway only.

The local API health timeout during optional replay checks is a local follow-up item, not an LMAX evidence failure. Address it separately before making local replay mandatory.

Next useful work: package the evidence and document the day. Do not run more snapshots from this phase.

Allowed next phase: $allowedNextPhase.
"@

$outDir = Resolve-LocalPath $OutputDirectory
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$gatePath = Join-Path $outDir "phase7k14-post-remediation-additional-instrument-portfolio-decision-gate.json"
$summaryPath = Join-Path $outDir "phase7k14-post-remediation-additional-instrument-portfolio-summary.json"
$notePath = Join-Path $outDir "phase7k14-post-remediation-additional-instrument-portfolio-note.md"

$gateJson = $gate | ConvertTo-Json -Depth 12
$summaryJson = $summary | ConvertTo-Json -Depth 12
if (($gateJson + "`n" + $summaryJson + "`n" + $note) -match $sensitivePattern) {
    throw "Generated Phase 7K14 artifacts contain credential-shaped or raw FIX content."
}

$gateJson | Set-Content -LiteralPath $gatePath -Encoding UTF8
$summaryJson | Set-Content -LiteralPath $summaryPath -Encoding UTF8
$note | Set-Content -LiteralPath $notePath -Encoding UTF8

Write-Host "Phase: 7K14"
Write-Host "PortfolioDecision: StopExternalAttemptsForDay"
Write-Host "SuccessfulReadOnlyEvidenceInstruments: GBPUSD, EURGBP, AUDUSD"
Write-Host "FinalDecision: PASS_PORTFOLIO_DECISION_STOP_EXTERNAL_ATTEMPTS_FOR_DAY"
Write-Host "AllowedNextPhase: $allowedNextPhase"
Write-Host "DecisionGate: $gatePath"
Write-Host "Summary: $summaryPath"
Write-Host "Note: $notePath"
