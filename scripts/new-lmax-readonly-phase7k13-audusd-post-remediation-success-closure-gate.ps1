param(
    [string]$Phase7K12InterpretationFile = "artifacts/readiness/phase7k12-audusd-post-remediation-snapshot-interpretation.json",
    [string]$Phase7K12SnapshotFile = "artifacts/lmax-readonly-runtime-additional-snapshot/audusd/lmax-readonly-audusd-demo-snapshot-result-20260511-202943.json",
    [string]$Phase7K12EvidencePreviewFile = "artifacts/lmax-readonly-runtime-additional-snapshot/audusd/evidence-preview/lmax-readonly-audusd-evidence-preview-20260511-203000.json",
    [string]$Phase7K12ClosureFile = "artifacts/lmax-readonly-runtime-additional-snapshot/audusd/closure/lmax-readonly-audusd-closure-manifest-20260511-203009.json",
    [string]$Phase7K10GbpusdInterpretationFile = "artifacts/readiness/phase7k10-gbpusd-post-remediation-known-good-control-interpretation.json",
    [string]$Phase7K11DecisionGateFile = "artifacts/readiness/phase7k11-post-remediation-success-decision-gate.json",
    [string]$Phase7K6FreezeSummaryFile = "artifacts/readiness/phase7k6-global-external-attempt-freeze-summary.json",
    [string]$Phase7K6FreezeGateFile = "artifacts/readiness/phase7k6-global-external-attempt-freeze-gate.json",
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

$phase7K12 = Read-JsonArtifact $Phase7K12InterpretationFile "Phase 7K12 AUDUSD interpretation"
$audusdSnapshot = Read-JsonArtifact $Phase7K12SnapshotFile "Phase 7K12 AUDUSD snapshot"
$audusdPreview = Read-JsonArtifact $Phase7K12EvidencePreviewFile "Phase 7K12 AUDUSD evidence preview"
$audusdClosure = Read-JsonArtifact $Phase7K12ClosureFile "Phase 7K12 AUDUSD closure"
$phase7K10 = Read-JsonArtifact $Phase7K10GbpusdInterpretationFile "Phase 7K10 GBPUSD interpretation"
$phase7K11 = Read-JsonArtifact $Phase7K11DecisionGateFile "Phase 7K11 decision gate"
$phase7K6Summary = Read-JsonArtifact $Phase7K6FreezeSummaryFile "Phase 7K6 freeze summary"
$phase7K6Gate = Read-JsonArtifact $Phase7K6FreezeGateFile "Phase 7K6 freeze gate"

if ([string]$phase7K12.json.phase -ne "7K12" -or [string]$phase7K12.json.finalDecision -ne "PASS_AUDUSD_POST_REMEDIATION_RECOVERED" -or -not [bool]$phase7K12.json.audusdRecovered -or -not [bool]$phase7K12.json.postRemediationAdditionalInstrumentHealthy) {
    throw "Phase 7K12 interpretation does not record recovered AUDUSD."
}
if ([string]$audusdSnapshot.json.symbol -ne "AUDUSD" -or [string]$audusdSnapshot.json.securityId -ne "4007" -or [string]$audusdSnapshot.json.securityIdSource -ne "8" -or [string]$audusdSnapshot.json.status -ne "Completed" -or -not [bool]$audusdSnapshot.json.snapshotReceived -or [int]$audusdSnapshot.json.entryCount -ne 2) {
    throw "Phase 7K12 AUDUSD snapshot is not the expected recovered snapshot."
}
if ([bool]$audusdSnapshot.json.orderSubmissionAttempted -or [bool]$audusdSnapshot.json.shadowReplaySubmitAttempted -or [bool]$audusdSnapshot.json.tradingMutationAttempted -or [bool]$audusdSnapshot.json.schedulerStarted -or [bool]$audusdSnapshot.json.credentialValuesReturned -or -not [bool]$audusdSnapshot.json.noSensitiveContent) {
    throw "Phase 7K12 AUDUSD snapshot contains an unsafe flag."
}
if ([string]$audusdPreview.json.evidenceMode -ne "MarketDataOnly" -or [string]$audusdPreview.json.instrument -ne "AUDUSD" -or [string]$audusdPreview.json.securityId -ne "4007" -or [string]$audusdPreview.json.marketData.status -ne "Ok" -or -not [bool]$audusdPreview.json.noSensitiveContent) {
    throw "Phase 7K12 AUDUSD evidence preview is not MarketDataOnly Ok."
}
if ([string]$audusdClosure.json.finalClosureDecision -ne "PASS" -or [string]$audusdClosure.json.closureClassification -ne "CompletedWithBook") {
    throw "Phase 7K12 AUDUSD closure is not PASS / CompletedWithBook."
}
if ([string]$phase7K10.json.finalDecision -ne "PASS_POST_REMEDIATION_CONTROL_RECOVERED" -or -not [bool]$phase7K10.json.knownGoodControlRecovered) {
    throw "Phase 7K10 GBPUSD control did not recover."
}
if ([string]$phase7K11.json.finalDecision -ne "PASS_POST_REMEDIATION_SUCCESS_DECISION_RECORDED" -or [string]$phase7K11.json.selectedFutureAttemptInstrument -ne "AUDUSD") {
    throw "Phase 7K11 did not select AUDUSD."
}
if ([string]$phase7K6Summary.json.finalDecision -ne "PASS_GLOBAL_FREEZE_RECORDED" -or -not [bool]$phase7K6Summary.json.invalidSecurityIdNotProven -or -not [bool]$phase7K6Summary.json.tokyo600xNotJustified) {
    throw "Phase 7K6 summary does not contain the expected safety conclusions."
}
if ([string]$phase7K6Gate.json.phase -ne "7K6" -or -not [bool]$phase7K6Gate.json.globalExternalAttemptFreeze) {
    throw "Phase 7K6 freeze gate is not present."
}

$disallowedActions = @(
    "No external run in Phase 7K13.",
    "No AUDUSD run in Phase 7K13.",
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

$allowedNextPhase = "Phase 7K14 - Post-Remediation Additional Instrument Portfolio Decision Gate, No External Run"

$gate = [ordered]@{
    phase = "7K13"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    instrument = "AUDUSD"
    securityId = "4007"
    securityIdSource = "8"
    environment = "Demo"
    venueProfile = "DemoLondon"
    requestMode = "SnapshotPlusUpdates"
    symbolEncodingMode = "SecurityIdOnly"
    marketDepth = 1
    audusdRecovered = $true
    audusdSnapshotStatus = "Completed"
    audusdSnapshotReceived = $true
    audusdEntryCount = 2
    audusdEvidenceMode = "MarketDataOnly"
    audusdEvidenceValidation = "Ok"
    audusdClosureDecision = "PASS"
    postRemediationAdditionalInstrumentHealthy = $true
    gbpusdPostRemediationControlRecovered = $true
    priorCrossInstrumentFailureResolvedForAudusd = $true
    invalidSecurityIdNotProven = $true
    tokyo600xNotJustified = $true
    marketDataRequestRejectObserved = $false
    usdJpyRemainsParked = $true
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
    automaticRetryRecommended = $false
    wrapperValidationWeakened = $false
    securityIdSwitchRecommended = $false
    tokyo600xSwitchRecommended = $false
    orderPathEnabled = $false
    schedulerOrPollingEnabled = $false
    runtimeShadowReplaySubmitEnabled = $false
    tradingMutationEnabled = $false
    gatewayRegistrationEnabled = $false
    sourcePhase7K12InterpretationPath = $phase7K12.path
    sourceAudusdSnapshotPath = $audusdSnapshot.path
    sourceAudusdEvidencePreviewPath = $audusdPreview.path
    sourceAudusdClosurePath = $audusdClosure.path
    sourcePhase7K10GbpusdInterpretationPath = $phase7K10.path
    sourcePhase7K11DecisionGatePath = $phase7K11.path
    recommendedNextAction = "Proceed to a no-external-run portfolio decision gate to decide whether to stop external attempts for the day, plan EURGBP confirmation, plan USDJPY troubleshooting separately, or close the current additional-instrument evidence expansion."
    allowedNextPhase = $allowedNextPhase
    disallowedActions = $disallowedActions
    noSensitiveContent = $true
    apiWorkerGatewayMode = "FakeLmaxGateway"
    finalDecision = "PASS_AUDUSD_POST_REMEDIATION_SUCCESS_CLOSED"
}

$summary = [ordered]@{
    phase = "7K13"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    earlierSuccessfulInstruments = @(
        [ordered]@{ instrument = "GBPUSD"; status = "Completed"; entryCount = 2; evidence = "Initial successful read-only MarketData snapshot" },
        [ordered]@{ instrument = "EURGBP"; status = "Completed"; entryCount = 2; evidence = "Initial successful read-only MarketData snapshot" }
    )
    preRemediationFailures = @(
        [ordered]@{ instrument = "USDJPY"; attempt = "first"; status = "FailedSafeConnectionError"; failurePoint = "BeforeLogon" },
        [ordered]@{ instrument = "USDJPY"; attempt = "retry"; status = "FailedSafeConnectionError"; failurePoint = "BeforeLogon" },
        [ordered]@{ instrument = "AUDUSD"; attempt = "first"; status = "FailedSafeConnectionError"; failurePoint = "BeforeLogon" },
        [ordered]@{ instrument = "GBPUSD"; attempt = "known-good control"; status = "FailedSafeConnectionError"; failurePoint = "BeforeLogon" }
    )
    remediationSequence = @(
        "Phase 7K8 remediation completion recorded.",
        "Phase 7K10 GBPUSD post-remediation control recovered.",
        "Phase 7K12 AUDUSD post-remediation recovered."
    )
    conclusion = [ordered]@{
        externalSessionLayerRecoveredForGbpusdAndAudusd = $true
        audusdDemoLondon4007ReadOnlyMarketDataEvidenceSucceeded = $true
        noInstrumentLevelRejectObserved = $true
        noTokyo600xJustification = $true
        usdJpyRemainsParkedForSeparateFutureTroubleshooting = $true
        recommendedOperationalState = "ProceedToPortfolioDecisionNoExternalRun"
    }
    noSensitiveContent = $true
    finalDecision = "PASS_AUDUSD_POST_REMEDIATION_SUCCESS_CLOSED"
}

$note = @"
# Phase 7K13 - AUDUSD Post-Remediation Success Closure

AUDUSD is now recovered and closed for this read-only evidence step. The post-remediation AUDUSD attempt reached logon, sent the MarketData request, received a two-entry book, and produced a valid MarketDataOnly evidence preview.

The earlier pre-logon failures were likely external/session/environment availability, because GBPUSD and AUDUSD both recovered after remediation.

No trading functionality was enabled. No order path was used. API/Worker remain FakeLmaxGateway only.

USDJPY remains a separate parked issue and is not recommended for immediate retry from this phase.

The next step should be a portfolio-level no-external-run decision, not another immediate snapshot.

Allowed next phase: $allowedNextPhase.
"@

$outDir = Resolve-LocalPath $OutputDirectory
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$gatePath = Join-Path $outDir "phase7k13-audusd-post-remediation-success-closure-gate.json"
$summaryPath = Join-Path $outDir "phase7k13-audusd-post-remediation-success-closure-summary.json"
$notePath = Join-Path $outDir "phase7k13-audusd-post-remediation-success-closure-note.md"

$gateJson = $gate | ConvertTo-Json -Depth 12
$summaryJson = $summary | ConvertTo-Json -Depth 12
if (($gateJson + "`n" + $summaryJson + "`n" + $note) -match $sensitivePattern) {
    throw "Generated Phase 7K13 artifacts contain credential-shaped or raw FIX content."
}

$gateJson | Set-Content -LiteralPath $gatePath -Encoding UTF8
$summaryJson | Set-Content -LiteralPath $summaryPath -Encoding UTF8
$note | Set-Content -LiteralPath $notePath -Encoding UTF8

Write-Host "Phase: 7K13"
Write-Host "Instrument: AUDUSD"
Write-Host "AudusdRecovered: true"
Write-Host "FinalDecision: PASS_AUDUSD_POST_REMEDIATION_SUCCESS_CLOSED"
Write-Host "AllowedNextPhase: $allowedNextPhase"
Write-Host "ClosureGate: $gatePath"
Write-Host "Summary: $summaryPath"
Write-Host "Note: $notePath"
