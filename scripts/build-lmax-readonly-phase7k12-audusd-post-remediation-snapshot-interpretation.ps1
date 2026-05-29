param(
    [Parameter(Mandatory=$true)]
    [string]$CurrentSnapshotFile,
    [Parameter(Mandatory=$true)]
    [string]$ClosureManifestFile,
    [string]$PriorFailedSnapshotFile = "artifacts/lmax-readonly-runtime-additional-snapshot/audusd/lmax-readonly-audusd-demo-snapshot-result-20260511-185948.json",
    [string]$PostRemediationKnownGoodControlFile = "artifacts/lmax-readonly-runtime-additional-snapshot/gbpusd/lmax-readonly-gbpusd-demo-snapshot-result-20260511-201523.json",
    [string]$ReviewReportFile = "artifacts/readiness/phase7h-additional-instrument-snapshot-review-audusd.json",
    [string]$EvidencePreviewFile = "",
    [string]$ReplayReportFile = "",
    [string]$OutputFile = "artifacts/readiness/phase7k12-audusd-post-remediation-snapshot-interpretation.json"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|raw\s*fix|BEGIN\s+PRIVATE\s+KEY)'

function Resolve-LocalPath([string]$PathValue) {
    if ([string]::IsNullOrWhiteSpace($PathValue)) { return "" }
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}

function Read-JsonSafe([string]$PathValue, [string]$Label) {
    $resolved = Resolve-LocalPath $PathValue
    if (-not (Test-Path -LiteralPath $resolved)) { throw "$Label is missing: $resolved" }
    $raw = Get-Content -LiteralPath $resolved -Raw
    $safe = $raw -replace 'credential|Credential|secret|Secret','SAFE_METADATA'
    if ($safe -match $sensitivePattern) { throw "$Label contains credential-shaped or raw FIX content." }
    return @{ path = $resolved; json = ($raw | ConvertFrom-Json) }
}

$priorFailed = Read-JsonSafe $PriorFailedSnapshotFile "Prior AUDUSD failed-safe snapshot"
$knownGoodControl = Read-JsonSafe $PostRemediationKnownGoodControlFile "Post-remediation GBPUSD known-good control"
$current = Read-JsonSafe $CurrentSnapshotFile "Current AUDUSD snapshot"
$review = Read-JsonSafe $ReviewReportFile "Current AUDUSD review report"
$closure = Read-JsonSafe $ClosureManifestFile "Current AUDUSD closure manifest"

if ([string]$priorFailed.json.symbol -ne "AUDUSD" -or [string]$priorFailed.json.securityId -ne "4007" -or [string]$priorFailed.json.status -ne "FailedSafeConnectionError" -or [bool]$priorFailed.json.logonAttempted -or [bool]$priorFailed.json.snapshotRequestAttempted) {
    throw "Prior AUDUSD snapshot is not the expected pre-remediation before-logon safe failure."
}
if ([string]$knownGoodControl.json.symbol -ne "GBPUSD" -or [string]$knownGoodControl.json.securityId -ne "4002" -or [string]$knownGoodControl.json.status -ne "Completed" -or -not [bool]$knownGoodControl.json.snapshotReceived -or [int]$knownGoodControl.json.entryCount -le 0) {
    throw "Post-remediation GBPUSD known-good control is not recovered."
}
if ([string]$current.json.symbol -ne "AUDUSD" -or [string]$current.json.securityId -ne "4007" -or [string]$current.json.securityIdSource -ne "8") {
    throw "Current snapshot is not AUDUSD / SecurityID 4007 / SecurityIDSource 8."
}
if ([bool]$current.json.orderSubmissionAttempted -or [bool]$current.json.shadowReplaySubmitAttempted -or [bool]$current.json.tradingMutationAttempted -or [bool]$current.json.schedulerStarted -or [bool]$current.json.credentialValuesReturned -or -not [bool]$current.json.noSensitiveContent) {
    throw "Current AUDUSD snapshot contains an unsafe flag."
}
if ([string]$review.json.finalDecision -eq "FAIL" -or [string]$closure.json.finalClosureDecision -eq "FAIL") {
    throw "Current AUDUSD review or closure failed."
}

$status = [string]$current.json.status
$classification = [string]$review.json.closureClassification
$entryCount = if ($null -eq $current.json.entryCount) { 0 } else { [int]$current.json.entryCount }
$marketDataRejectCount = if ($null -eq $current.json.marketDataRequestRejectCount) { if ($null -eq $current.json.marketDataRequestReject) { 0 } else { [int]$current.json.marketDataRequestReject } } else { [int]$current.json.marketDataRequestRejectCount }
$businessRejectCount = if ($null -eq $current.json.businessMessageRejectCount) { if ($null -eq $current.json.businessMessageReject) { 0 } else { [int]$current.json.businessMessageReject } } else { [int]$current.json.businessMessageRejectCount }
$rejectCount = if ($null -eq $current.json.rejectCount) { if ($null -eq $current.json.reject) { 0 } else { [int]$current.json.reject } } else { [int]$current.json.rejectCount }

$audusdRecovered = $false
$postRemediationAdditionalInstrumentHealthy = $false
$postRemediationSessionPathReached = $false
$audusdSpecificOrIntermittentSessionIssueStillPossible = $false
$audusdInstrumentLevelRejectObserved = $false
$securityIdIssueMayBeInvestigatedButNoSwitchAllowedYet = $false
$environmentSessionInterpretation = ""
$recommendedNextAction = ""
$allowedNextPhase = ""
$finalDecision = ""

if ($classification -eq "CompletedWithBook" -and [bool]$current.json.snapshotReceived -and $entryCount -gt 0) {
    $audusdRecovered = $true
    $postRemediationAdditionalInstrumentHealthy = $true
    $postRemediationSessionPathReached = $true
    $environmentSessionInterpretation = "AUDUSD post-remediation attempt reached TCP/TLS/FIX logon, sent the MarketDataRequest, and received a book snapshot."
    $recommendedNextAction = "Consider closure of AUDUSD onboarding or one no-external-run decision gate for the next instrument."
    $allowedNextPhase = "Phase 7K13 - AUDUSD Post-Remediation Success Closure Gate, No External Run"
    $finalDecision = "PASS_AUDUSD_POST_REMEDIATION_RECOVERED"
} elseif ($classification -eq "CompletedWithEmptyBook" -and [bool]$current.json.logonAttempted -and [bool]$current.json.snapshotRequestAttempted) {
    $audusdRecovered = "partial"
    $postRemediationSessionPathReached = $true
    $environmentSessionInterpretation = "AUDUSD post-remediation attempt reached the session/request path but returned an empty book."
    $recommendedNextAction = "Review empty-book acceptability before further attempts."
    $allowedNextPhase = "Phase 7K13 - AUDUSD Partial Recovery Decision Gate, No External Run"
    $finalDecision = "PASS_WITH_KNOWN_WARNINGS"
} elseif ($status -eq "FailedSafeConnectionError" -and -not [bool]$current.json.logonAttempted) {
    $audusdRecovered = $false
    $audusdSpecificOrIntermittentSessionIssueStillPossible = $true
    $environmentSessionInterpretation = "AUDUSD still failed before FIX logon despite GBPUSD control recovery."
    $recommendedNextAction = "Do not retry; create no-external-run diagnosis gate."
    $allowedNextPhase = "Phase 7K13 - AUDUSD Post-Remediation FailedSafe Diagnosis Gate, No External Run"
    $finalDecision = "PASS_WITH_ACTION_REQUIRED"
} elseif ($marketDataRejectCount -gt 0 -or $businessRejectCount -gt 0 -or $rejectCount -gt 0) {
    $audusdRecovered = $false
    $audusdInstrumentLevelRejectObserved = $true
    $securityIdIssueMayBeInvestigatedButNoSwitchAllowedYet = $true
    $environmentSessionInterpretation = "AUDUSD post-remediation attempt reached a reject path after session/request activity."
    $recommendedNextAction = "Local-only reject diagnosis; no Tokyo switch."
    $allowedNextPhase = "Phase 7K13 - AUDUSD MarketDataReject Diagnosis Gate, No External Run"
    $finalDecision = "PASS_WITH_ACTION_REQUIRED"
} else {
    $environmentSessionInterpretation = "AUDUSD post-remediation attempt completed with an unrecognized safe classification; review locally before any further external attempt."
    $recommendedNextAction = "Hold further external attempts and review the artifact classification."
    $allowedNextPhase = "Phase 7K13 - AUDUSD Post-Remediation Review Gate, No External Run"
    $finalDecision = "PASS_WITH_ACTION_REQUIRED"
}

$previewPath = if ([string]::IsNullOrWhiteSpace($EvidencePreviewFile)) { $null } else { Resolve-LocalPath $EvidencePreviewFile }
$replayPath = if ([string]::IsNullOrWhiteSpace($ReplayReportFile)) { $null } else { Resolve-LocalPath $ReplayReportFile }

$report = [ordered]@{
    phase = "7K12"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    instrument = "AUDUSD"
    slashSymbol = "AUD/USD"
    securityId = "4007"
    securityIdSource = "8"
    attemptType = "PostRemediationAdditionalInstrumentRetry"
    priorFailedSnapshotPath = $priorFailed.path
    postRemediationKnownGoodControlPath = $knownGoodControl.path
    currentSnapshotPath = $current.path
    currentReviewReportPath = $review.path
    currentEvidencePreviewPath = $previewPath
    currentReplayReportPath = $replayPath
    currentClosureManifestPath = $closure.path
    currentStatus = $status
    currentLogonAttempted = [bool]$current.json.logonAttempted
    currentSnapshotRequestAttempted = [bool]$current.json.snapshotRequestAttempted
    currentSnapshotReceived = [bool]$current.json.snapshotReceived
    currentEntryCount = $entryCount
    currentBestBid = $current.json.bestBid
    currentBestAsk = $current.json.bestAsk
    currentMid = $current.json.mid
    marketDataRequestReject = $marketDataRejectCount
    businessMessageReject = $businessRejectCount
    reject = $rejectCount
    closureClassification = $classification
    reviewDecision = [string]$review.json.finalDecision
    closureDecision = [string]$closure.json.finalClosureDecision
    evidencePreviewGenerated = -not [string]::IsNullOrWhiteSpace($EvidencePreviewFile)
    evidencePreviewStatus = if ([string]::IsNullOrWhiteSpace($EvidencePreviewFile)) { "NOT_RUN" } else { "Ok" }
    replayRun = -not [string]::IsNullOrWhiteSpace($ReplayReportFile)
    replayStatus = if ([string]::IsNullOrWhiteSpace($ReplayReportFile)) { "NOT_RUN" } else { "Completed" }
    audusdRecovered = $audusdRecovered
    postRemediationAdditionalInstrumentHealthy = $postRemediationAdditionalInstrumentHealthy
    postRemediationSessionPathReached = $postRemediationSessionPathReached
    audusdSpecificOrIntermittentSessionIssueStillPossible = $audusdSpecificOrIntermittentSessionIssueStillPossible
    audusdInstrumentLevelRejectObserved = $audusdInstrumentLevelRejectObserved
    securityIdIssueMayBeInvestigatedButNoSwitchAllowedYet = $securityIdIssueMayBeInvestigatedButNoSwitchAllowedYet
    tokyo600xSwitchRecommended = $false
    environmentSessionInterpretation = $environmentSessionInterpretation
    recommendedNextAction = $recommendedNextAction
    allowedNextPhase = $allowedNextPhase
    orderSubmissionAttempted = $false
    shadowReplaySubmitAttempted = $false
    tradingMutationAttempted = $false
    schedulerStarted = $false
    credentialValuesReturned = $false
    noSensitiveContent = $true
    redactionStatus = "Redacted"
    finalDecision = $finalDecision
}

$outPath = Resolve-LocalPath $OutputFile
$outDir = Split-Path -Parent $outPath
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$json = $report | ConvertTo-Json -Depth 12
if ($json -match $sensitivePattern) { throw "Generated Phase 7K12 interpretation contains credential-shaped or raw FIX content." }
$json | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host "Phase: 7K12"
Write-Host "Instrument: AUDUSD"
Write-Host "CurrentStatus: $status"
Write-Host "AudusdRecovered: $audusdRecovered"
Write-Host "FinalDecision: $finalDecision"
Write-Host "AllowedNextPhase: $allowedNextPhase"
Write-Host "Report: $outPath"
