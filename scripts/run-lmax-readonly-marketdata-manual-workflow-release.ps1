param(
    [switch]$AllowExternalConnections,
    [switch]$ConfirmDemoReadOnly,
    [switch]$ConfirmRepeatedManualSnapshots,
    [switch]$ReplayEvidencePreviews,
    [switch]$ConfirmLocalManualReplay,
    [int]$AttemptCount = 3,
    [int]$DelaySeconds = 5,
    [Parameter(Mandatory = $false)]
    [string]$Reason,
    [string]$StabilitySummaryFile = "artifacts/lmax-readonly-runtime-demo-snapshot/stability/lmax-readonly-demo-snapshot-stability-20260508-144517.json",
    [string]$BaseUrl = "http://localhost:5050",
    [string]$OperatorId = "local-operator"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$workflowReviewScript = Join-Path $repoRoot "scripts/run-lmax-readonly-marketdata-manual-workflow-review.ps1"

function Resolve-LocalPath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return Join-Path $repoRoot $Path
}

function Get-OutputValue([string]$Text, [string]$Name) {
    $match = [regex]::Match($Text, "(?m)^\s*$([regex]::Escape($Name)):\s*(.+?)\s*$")
    if (-not $match.Success) { return $null }
    return $match.Groups[1].Value.Trim()
}

Write-Host "LMAX Read-Only Runtime Phase 5S Controlled Manual Workflow Release"
Write-Host "WARNING: Release review is local artifact workflow only. It does not run another Demo snapshot."
Write-Host "WARNING: Demo-only, read-only, MarketDataOnly evidence previews. No orders, no scheduler/polling, no runtime shadow replay submit."
Write-Host "WARNING: Optional replay requires -ReplayEvidencePreviews and -ConfirmLocalManualReplay, and uses only the local API replay path."
Write-Host "Rollback: clear Phase 5S shell variables, verify /health FakeLmaxGateway, and rerun the Phase 5O/5S gates."

if (-not $AllowExternalConnections) { throw "-AllowExternalConnections is required as an operator acknowledgement for the release workflow boundary." }
if (-not $ConfirmDemoReadOnly) { throw "-ConfirmDemoReadOnly is required." }
if (-not $ConfirmRepeatedManualSnapshots) { throw "-ConfirmRepeatedManualSnapshots is required." }
if ([string]::IsNullOrWhiteSpace($Reason)) { throw "-Reason is required." }
if ($AttemptCount -lt 1 -or $AttemptCount -gt 5) { throw "-AttemptCount must be within 1..5." }
if ($DelaySeconds -lt 1 -or $DelaySeconds -gt 10) { throw "-DelaySeconds must be within 1..10." }
if ($ReplayEvidencePreviews -and -not $ConfirmLocalManualReplay) { throw "-ReplayEvidencePreviews requires -ConfirmLocalManualReplay." }

$stabilitySummaryPath = Resolve-LocalPath $StabilitySummaryFile
if (-not (Test-Path -LiteralPath $stabilitySummaryPath)) {
    throw "Stability summary not found: $stabilitySummaryPath"
}

$summary = Get-Content -LiteralPath $stabilitySummaryPath -Raw | ConvertFrom-Json
if ([int]$summary.attemptCountRequested -ne $AttemptCount) {
    throw "AttemptCount $AttemptCount does not match stability summary attemptCountRequested=$($summary.attemptCountRequested)."
}
if ([int]$summary.attemptCountCompleted -ne [int]$summary.attemptCountRequested -or [int]$summary.successCount -ne [int]$summary.attemptCountRequested) {
    throw "Stability summary is not a successful completed Phase 5O summary."
}
if ([bool]$summary.orderSubmissionAttempted -or [bool]$summary.shadowReplaySubmitAttempted -or [bool]$summary.tradingMutationAttempted -or [bool]$summary.schedulerStarted -or [bool]$summary.credentialValuesReturned) {
    throw "Stability summary has unsafe flags."
}

$workflowArgs = @(
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", $workflowReviewScript,
    "-StabilitySummaryFile", $stabilitySummaryPath,
    "-OperatorId", $OperatorId,
    "-Reason", $Reason,
    "-BaseUrl", $BaseUrl
)
if ($ReplayEvidencePreviews) {
    $workflowArgs += "-ReplayEvidencePreviews"
    $workflowArgs += "-ConfirmLocalManualReplay"
}

$workflowOutput = & powershell @workflowArgs 2>&1
$workflowText = $workflowOutput | Out-String
Write-Host $workflowText
if ($LASTEXITCODE -ne 0) {
    throw "Controlled manual workflow review failed."
}

$workflowManifestPath = Get-OutputValue $workflowText "WorkflowManifest"
if ([string]::IsNullOrWhiteSpace($workflowManifestPath) -or -not (Test-Path -LiteralPath $workflowManifestPath)) {
    throw "Workflow review did not produce a manifest path."
}

$manifest = Get-Content -LiteralPath $workflowManifestPath -Raw | ConvertFrom-Json
$finalDecision = [string]$manifest.finalDecision
if ($ReplayEvidencePreviews -and $finalDecision -ne "PASS") {
    throw "Replay-enabled release workflow must produce PASS. Actual: $finalDecision"
}

$releaseManifest = [ordered]@{
    workflowId = [string]$manifest.workflowId
    phase = "5S"
    createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    operatorId = $OperatorId
    reason = $Reason
    sourceStabilitySummaryPath = $stabilitySummaryPath
    sourceWorkflowManifestPath = $workflowManifestPath
    allowExternalConnectionsAcknowledged = [bool]$AllowExternalConnections
    confirmDemoReadOnly = [bool]$ConfirmDemoReadOnly
    confirmRepeatedManualSnapshots = [bool]$ConfirmRepeatedManualSnapshots
    attemptCount = $AttemptCount
    delaySeconds = $DelaySeconds
    replayRequested = [bool]$manifest.replayRequested
    replayPerformed = [bool]$manifest.replayPerformed
    artifactCount = [int]$manifest.artifactCount
    evidencePreviewCount = [int]$manifest.evidencePreviewCount
    manualReplayCount = [int]$manifest.manualReplayCount
    runtimeShadowReplaySubmit = $false
    externalConnectionAttempted = $false
    orderSubmissionAttempted = $false
    shadowReplaySubmitAttempted = $false
    tradingMutationAttempted = $false
    schedulerStarted = $false
    credentialValuesReturned = $false
    noSensitiveContent = $true
    redactionStatus = "Redacted"
    snapshotArtifacts = @($manifest.snapshotArtifacts)
    evidencePreviews = @($manifest.evidencePreviews)
    manualReplayResults = @($manifest.manualReplayResults)
    warnings = @($manifest.warnings)
    errors = @($manifest.errors)
    finalDecision = $finalDecision
    rollbackInstructions = @(
        "Stop the local release workflow process.",
        "Clear Phase 5S shell variables from the current terminal.",
        "Verify /health reports FakeLmaxGateway before any further operation.",
        "Re-run scripts/check-lmax-readonly-runtime-phase5o-stability-gate.ps1 if stability artifacts are in doubt.",
        "Re-run scripts/check-lmax-readonly-runtime-phase5s-release-gate.ps1 -ReleaseManifestFile <manifest>.",
        "No DB rollback is expected because this workflow must not mutate trading state."
    )
}

$manifestDir = Join-Path $repoRoot "artifacts/lmax-readonly-runtime-demo-snapshot/workflow"
New-Item -ItemType Directory -Path $manifestDir -Force | Out-Null
$replayResultsPath = $null
if (@($releaseManifest.manualReplayResults).Count -gt 0) {
    $replayResultsPath = Join-Path $manifestDir "phase5s-manual-release-replay-results.json"
    [ordered]@{
        phase = "5S"
        workflowId = $releaseManifest.workflowId
        createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
        replayRequested = [bool]$ReplayEvidencePreviews
        replayPerformed = $true
        manualReplayCount = [int]$releaseManifest.manualReplayCount
        runtimeShadowReplaySubmit = $false
        externalConnectionAttempted = $false
        noSensitiveContent = $true
        redactionStatus = "Redacted"
        replayResults = @($releaseManifest.manualReplayResults)
    } | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath $replayResultsPath -Encoding UTF8
    $releaseManifest.replayResultsFile = $replayResultsPath
}
$releaseManifestPath = Join-Path $manifestDir "phase5s-manual-release-manifest.json"
$releaseManifest | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath $releaseManifestPath -Encoding UTF8

$observationCount = 0
if (@($releaseManifest.manualReplayResults).Count -gt 0) {
    $sum = (@($releaseManifest.manualReplayResults) | Measure-Object -Property observationCount -Sum).Sum
    if ($null -ne $sum) {
        $observationCount = [int]$sum
    }
}

Write-Host ""
Write-Host "ReleaseManifest: $releaseManifestPath"
if ($replayResultsPath) {
    Write-Host "ReplayResults: $replayResultsPath"
}
Write-Host ("ArtifactCount: {0}" -f $releaseManifest.artifactCount)
Write-Host ("EvidencePreviewCount: {0}" -f $releaseManifest.evidencePreviewCount)
Write-Host ("ManualReplayCount: {0}" -f $releaseManifest.manualReplayCount)
Write-Host ("ObservationCount: {0}" -f $observationCount)
Write-Host ("MutationGuard: {0}" -f ($(if (@($releaseManifest.manualReplayResults).Count -eq 0) { "NotReplayed" } elseif (@($releaseManifest.manualReplayResults | Where-Object { [string]$_.mutationGuard -ne "Unchanged" }).Count -eq 0) { "Unchanged" } else { "Changed" })))
Write-Host "RuntimeShadowReplaySubmit: false"
Write-Host "ExternalConnectionAttempted: false"
Write-Host "CredentialValuesReturned: false"
Write-Host "FinalDecision: $finalDecision"

if ($finalDecision -eq "FAIL") { exit 1 }
