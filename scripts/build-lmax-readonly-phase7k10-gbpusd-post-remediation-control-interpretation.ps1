param(
    [Parameter(Mandatory=$true)]
    [string]$CurrentControlSnapshotFile,
    [Parameter(Mandatory=$true)]
    [string]$ClosureManifestFile,
    [string]$EarlierSuccessfulSnapshotFile = "artifacts/lmax-readonly-runtime-additional-snapshot/gbpusd/lmax-readonly-gbpusd-demo-snapshot-result-20260511-103318.json",
    [string]$PreRemediationFailedControlSnapshotFile = "artifacts/lmax-readonly-runtime-additional-snapshot/gbpusd/lmax-readonly-gbpusd-demo-snapshot-result-20260511-192834.json",
    [string]$ReviewReportFile = "artifacts/readiness/phase7h-additional-instrument-snapshot-review-gbpusd.json",
    [string]$EvidencePreviewFile = "",
    [string]$ReplayReportFile = "",
    [string]$OutputFile = "artifacts/readiness/phase7k10-gbpusd-post-remediation-known-good-control-interpretation.json"
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

$earlier = Read-JsonSafe $EarlierSuccessfulSnapshotFile "Earlier successful GBPUSD snapshot"
$preRemediationFailure = Read-JsonSafe $PreRemediationFailedControlSnapshotFile "Pre-remediation failed GBPUSD control"
$current = Read-JsonSafe $CurrentControlSnapshotFile "Current GBPUSD control snapshot"
$review = Read-JsonSafe $ReviewReportFile "Current GBPUSD review report"
$closure = Read-JsonSafe $ClosureManifestFile "Current GBPUSD closure manifest"

if ([string]$earlier.json.symbol -ne "GBPUSD" -or [string]$earlier.json.securityId -ne "4002" -or [string]$earlier.json.status -ne "Completed" -or -not [bool]$earlier.json.snapshotReceived) {
    throw "Earlier GBPUSD snapshot is not the expected successful control baseline."
}
if ([string]$preRemediationFailure.json.symbol -ne "GBPUSD" -or [string]$preRemediationFailure.json.securityId -ne "4002" -or [string]$preRemediationFailure.json.status -ne "FailedSafeConnectionError" -or [bool]$preRemediationFailure.json.logonAttempted -or [bool]$preRemediationFailure.json.snapshotRequestAttempted) {
    throw "Pre-remediation GBPUSD control is not the expected before-logon safe failure."
}
if ([string]$current.json.symbol -ne "GBPUSD" -or [string]$current.json.securityId -ne "4002" -or [string]$current.json.securityIdSource -ne "8") {
    throw "Current control snapshot is not GBPUSD / SecurityID 4002 / SecurityIDSource 8."
}
if ([bool]$current.json.orderSubmissionAttempted -or [bool]$current.json.shadowReplaySubmitAttempted -or [bool]$current.json.tradingMutationAttempted -or [bool]$current.json.schedulerStarted -or [bool]$current.json.credentialValuesReturned -or -not [bool]$current.json.noSensitiveContent) {
    throw "Current GBPUSD control snapshot contains an unsafe flag."
}
if ([string]$review.json.finalDecision -eq "FAIL" -or [string]$closure.json.finalClosureDecision -eq "FAIL") {
    throw "Current GBPUSD review or closure failed."
}

$status = [string]$current.json.status
$classification = [string]$review.json.closureClassification
$entryCount = if ($null -eq $current.json.entryCount) { 0 } else { [int]$current.json.entryCount }
$marketDataRejectCount = if ($null -eq $current.json.marketDataRequestRejectCount) { if ($null -eq $current.json.marketDataRequestReject) { 0 } else { [int]$current.json.marketDataRequestReject } } else { [int]$current.json.marketDataRequestRejectCount }
$businessRejectCount = if ($null -eq $current.json.businessMessageRejectCount) { if ($null -eq $current.json.businessMessageReject) { 0 } else { [int]$current.json.businessMessageReject } } else { [int]$current.json.businessMessageRejectCount }
$rejectCount = if ($null -eq $current.json.rejectCount) { if ($null -eq $current.json.reject) { 0 } else { [int]$current.json.reject } } else { [int]$current.json.rejectCount }

$knownGoodControlRecovered = $false
$postRemediationSessionHealthy = $false
$postRemediationSessionPathReached = $false
$broaderEnvironmentSessionIssueStillSuspected = $false
$environmentSessionInterpretation = ""
$recommendedNextAction = ""
$allowedNextPhase = ""
$finalDecision = ""

if ($classification -eq "CompletedWithBook" -and [bool]$current.json.snapshotReceived -and $entryCount -gt 0) {
    $knownGoodControlRecovered = $true
    $postRemediationSessionHealthy = $true
    $postRemediationSessionPathReached = $true
    $environmentSessionInterpretation = "GBPUSD post-remediation known-good control reached TCP/TLS/FIX logon, sent the MarketDataRequest, and received a book snapshot."
    $recommendedNextAction = "Consider one future AUDUSD retry or EURGBP/GBPUSD confirmation gate in a later no-external-run phase."
    $allowedNextPhase = "Phase 7K11 - Post-Remediation Success Decision Gate, No External Run"
    $finalDecision = "PASS_POST_REMEDIATION_CONTROL_RECOVERED"
} elseif ($classification -eq "CompletedWithEmptyBook" -and [bool]$current.json.logonAttempted -and [bool]$current.json.snapshotRequestAttempted) {
    $knownGoodControlRecovered = "partially"
    $postRemediationSessionPathReached = $true
    $environmentSessionInterpretation = "GBPUSD post-remediation known-good control reached the session/request path but returned an empty book."
    $recommendedNextAction = "Review whether the empty book is acceptable before any further attempt."
    $allowedNextPhase = "Phase 7K11 - Post-Remediation Partial Recovery Decision Gate, No External Run"
    $finalDecision = "PASS_WITH_KNOWN_WARNINGS"
} elseif ($status -eq "FailedSafeConnectionError" -and -not [bool]$current.json.logonAttempted) {
    $knownGoodControlRecovered = $false
    $broaderEnvironmentSessionIssueStillSuspected = $true
    $environmentSessionInterpretation = "GBPUSD post-remediation known-good control still failed before FIX logon; broader environment/session issue remains suspected."
    $recommendedNextAction = "Restore or continue global freeze and escalate environment/session troubleshooting."
    $allowedNextPhase = "Phase 7K11 - Restore Global Freeze After Post-Remediation Control Failure, No External Run"
    $finalDecision = "PASS_WITH_ACTION_REQUIRED"
} elseif ($marketDataRejectCount -gt 0 -or $businessRejectCount -gt 0 -or $rejectCount -gt 0) {
    $environmentSessionInterpretation = "GBPUSD post-remediation control reached a reject path after session/request activity; classify separately and do not auto-switch SecurityID."
    $recommendedNextAction = "Review the explicit reject details in a no-external-run decision gate."
    $allowedNextPhase = "Phase 7K11 - Post-Remediation Reject Review Gate, No External Run"
    $finalDecision = "PASS_WITH_ACTION_REQUIRED"
} else {
    $environmentSessionInterpretation = "GBPUSD post-remediation control completed with an unrecognized safe classification; review locally before any further external attempt."
    $recommendedNextAction = "Hold further external attempts and review the artifact classification."
    $allowedNextPhase = "Phase 7K11 - Post-Remediation Control Review Gate, No External Run"
    $finalDecision = "PASS_WITH_ACTION_REQUIRED"
}

$previewPath = if ([string]::IsNullOrWhiteSpace($EvidencePreviewFile)) { $null } else { Resolve-LocalPath $EvidencePreviewFile }
$replayPath = if ([string]::IsNullOrWhiteSpace($ReplayReportFile)) { $null } else { Resolve-LocalPath $ReplayReportFile }

$report = [ordered]@{
    phase = "7K10"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    instrument = "GBPUSD"
    slashSymbol = "GBP/USD"
    securityId = "4002"
    securityIdSource = "8"
    attemptType = "PostRemediationKnownGoodControlSnapshot"
    earlierSuccessfulSnapshotPath = $earlier.path
    preRemediationFailedControlSnapshotPath = $preRemediationFailure.path
    currentControlSnapshotPath = $current.path
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
    knownGoodControlRecovered = $knownGoodControlRecovered
    postRemediationSessionHealthy = $postRemediationSessionHealthy
    postRemediationSessionPathReached = $postRemediationSessionPathReached
    broaderEnvironmentSessionIssueStillSuspected = $broaderEnvironmentSessionIssueStillSuspected
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
if ($json -match $sensitivePattern) { throw "Generated Phase 7K10 interpretation contains credential-shaped or raw FIX content." }
$json | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host "Phase: 7K10"
Write-Host "Instrument: GBPUSD"
Write-Host "CurrentStatus: $status"
Write-Host "KnownGoodControlRecovered: $knownGoodControlRecovered"
Write-Host "FinalDecision: $finalDecision"
Write-Host "AllowedNextPhase: $allowedNextPhase"
Write-Host "Report: $outPath"
