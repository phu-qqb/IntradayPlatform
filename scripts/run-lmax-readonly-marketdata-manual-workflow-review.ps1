param(
    [string]$StabilitySummaryFile,
    [string[]]$ArtifactFile = @(),
    [switch]$RegeneratePreviews,
    [switch]$ReplayEvidencePreviews,
    [switch]$ConfirmLocalReplay,
    [switch]$ConfirmLocalManualReplay,
    [string]$BaseUrl = "http://localhost:5050",
    [string]$OperatorId = "local-operator",
    [string]$Reason = "Phase 5R controlled manual MarketData evidence workflow review"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactValidator = Join-Path $repoRoot "scripts/validate-lmax-readonly-runtime-demo-snapshot-artifact.ps1"
$previewScript = Join-Path $repoRoot "scripts/preview-lmax-readonly-demo-snapshot-evidence.ps1"
$evidenceValidator = Join-Path $repoRoot "scripts/validate-lmax-lab-evidence-file.ps1"
$replayScript = Join-Path $repoRoot "scripts/replay-lmax-readonly-demo-snapshot-evidence-preview.ps1"

function Resolve-LocalPath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return Join-Path $repoRoot $Path
}

function Test-ApiAvailable([string]$Url) {
    try {
        $uri = [Uri]$Url
        if ($uri.Host -notin @("localhost", "127.0.0.1")) { return $false }
        Invoke-RestMethod -Method GET -Uri "$Url/health" -TimeoutSec 3 | Out-Null
        return $true
    } catch {
        return $false
    }
}

function Get-PreviewPath([string[]]$Lines) {
    $line = @($Lines | Where-Object { $_ -match '^PreviewFile:\s*(.+)$' } | Select-Object -Last 1)
    if ($line.Count -eq 0) { return $null }
    return ($line[-1] -replace '^PreviewFile:\s*', '').Trim()
}

function Get-OutputValue([string]$Text, [string]$Name) {
    $match = [regex]::Match($Text, "(?m)^\s*$([regex]::Escape($Name)):\s*(.+?)\s*$")
    if (-not $match.Success) { return $null }
    return $match.Groups[1].Value.Trim()
}

Write-Host "LMAX Read-Only MarketData Manual Workflow Review"
Write-Host "Local-only by default. No external LMAX connection, no credentials, no scheduler, no runtime shadow replay submit."

$artifactInputs = @()
$stabilitySummaryPath = $null
if (-not [string]::IsNullOrWhiteSpace($StabilitySummaryFile)) {
    $stabilitySummaryPath = Resolve-LocalPath $StabilitySummaryFile
    if (-not (Test-Path -LiteralPath $stabilitySummaryPath)) { throw "Stability summary not found: $stabilitySummaryPath" }
    $summary = Get-Content -LiteralPath $stabilitySummaryPath -Raw | ConvertFrom-Json
    foreach ($attempt in @($summary.attempts)) {
        if (-not [string]::IsNullOrWhiteSpace([string]$attempt.artifactPath)) {
            $artifactInputs += [string]$attempt.artifactPath
        }
    }
}

foreach ($path in $ArtifactFile) {
    $artifactInputs += $path
}

$artifactInputs = @($artifactInputs | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
if ($artifactInputs.Count -eq 0) {
    throw "Provide -StabilitySummaryFile with artifact paths or one or more -ArtifactFile values."
}

if ($ReplayEvidencePreviews -and -not ($ConfirmLocalReplay -or $ConfirmLocalManualReplay)) {
    throw "-ReplayEvidencePreviews requires -ConfirmLocalManualReplay."
}
if ($ReplayEvidencePreviews -and -not (Test-ApiAvailable $BaseUrl)) {
    throw "-ReplayEvidencePreviews requires local API available at $BaseUrl."
}

$artifactResults = @()
$previewResults = @()
$replayResults = @()
$warnings = @()
$errors = @()

foreach ($artifactInput in $artifactInputs) {
    $artifactPath = Resolve-LocalPath $artifactInput
    if (-not (Test-Path -LiteralPath $artifactPath)) {
        $errors += "Artifact not found: $artifactPath"
        continue
    }

    $artifactOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $artifactValidator -ArtifactFile $artifactPath 2>&1
    $artifactText = $artifactOutput | Out-String
    $artifactValidationStatus = if ($LASTEXITCODE -eq 0) { "PASS" } else { "FAIL" }
    if ($artifactValidationStatus -ne "PASS") { $errors += "Artifact validation failed: $artifactPath" }
    $artifact = Get-Content -LiteralPath $artifactPath -Raw | ConvertFrom-Json
    $artifactResults += [ordered]@{
        path = $artifactPath
        validationStatus = $artifactValidationStatus
        status = [string]$artifact.status
        snapshotReceived = [bool]$artifact.snapshotReceived
        orderSubmissionAttempted = [bool]$artifact.orderSubmissionAttempted
        shadowReplaySubmitAttempted = [bool]$artifact.shadowReplaySubmitAttempted
        tradingMutationAttempted = [bool]$artifact.tradingMutationAttempted
        schedulerStarted = [bool]$artifact.schedulerStarted
        credentialValuesReturned = [bool]$artifact.credentialValuesReturned
        noSensitiveContent = [bool]$artifact.noSensitiveContent
    }

    $previewPath = $null
    if (-not $RegeneratePreviews -and $stabilitySummaryPath) {
        $match = @($summary.attempts | Where-Object { [string]$_.artifactPath -eq $artifactInput -or [string]$_.artifactPath -eq $artifactPath } | Select-Object -First 1)
        if ($match.Count -gt 0) { $previewPath = [string]$match[0].evidencePreviewPath }
    }

    if ([string]::IsNullOrWhiteSpace($previewPath) -or -not (Test-Path -LiteralPath (Resolve-LocalPath $previewPath)) -or $RegeneratePreviews) {
        $previewOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $previewScript -ArtifactFile $artifactPath 2>&1
        $previewLines = @($previewOutput | ForEach-Object { [string]$_ })
        if ($LASTEXITCODE -ne 0) {
            $errors += "Preview generation failed for artifact: $artifactPath"
            continue
        }
        $previewPath = Get-PreviewPath $previewLines
    }

    $resolvedPreview = Resolve-LocalPath $previewPath
    $previewOutput2 = & powershell -NoProfile -ExecutionPolicy Bypass -File $evidenceValidator -EvidenceFile $resolvedPreview 2>&1
    $previewValidationStatus = if ($LASTEXITCODE -eq 0) { "PASS" } else { "FAIL" }
    if ($previewValidationStatus -ne "PASS") { $errors += "Evidence preview validation failed: $resolvedPreview" }
    $preview = Get-Content -LiteralPath $resolvedPreview -Raw | ConvertFrom-Json
    $previewResults += [ordered]@{
        path = $resolvedPreview
        validationStatus = $previewValidationStatus
        evidenceMode = [string]$preview.evidenceMode
        executionReportCount = @($preview.executionReports).Count
        orderStatusCount = @($preview.orderStatuses).Count
        tradeCaptureReportCount = @($preview.tradeCaptureReports).Count
        protocolRejectCount = @($preview.protocolRejects).Count
        marketDataSnapshotCount = if ($preview.marketData.snapshotReceived) { 1 } else { 0 }
        noSensitiveContent = [bool]$preview.noSensitiveContent
    }

    if ($ReplayEvidencePreviews) {
        $replayOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $replayScript -EvidencePreviewFile $resolvedPreview -BaseUrl $BaseUrl -OperatorId $OperatorId -Reason $Reason 2>&1
        $replayText = $replayOutput | Out-String
        if ($LASTEXITCODE -ne 0) {
            $errors += "Manual replay failed for preview: $resolvedPreview"
            $replayResults += [ordered]@{
                evidencePreviewFile = $resolvedPreview
                replayRunId = $null
                replayStatus = "Failed"
                observationCount = -1
                blockingObservationCount = -1
                warningObservationCount = -1
                mutationGuard = "Unknown"
                noSensitiveContent = $false
            }
        } else {
            $replayStatus = Get-OutputValue $replayText "ReplayStatus"
            $replayRunId = Get-OutputValue $replayText "ReplayRunId"
            $observationCount = [int](Get-OutputValue $replayText "ObservationCount")
            $blockingObservationCount = [int](Get-OutputValue $replayText "BlockingObservationCount")
            $warningObservationCount = [int](Get-OutputValue $replayText "WarningObservationCount")
            $mutationGuard = Get-OutputValue $replayText "MutationGuard"
            if ($replayStatus -ne "Completed" -or $observationCount -ne 0 -or $blockingObservationCount -ne 0 -or $warningObservationCount -ne 0 -or $mutationGuard -ne "Unchanged") {
                $errors += "Manual replay returned unsafe result for preview: $resolvedPreview"
            }
            $replayResults += [ordered]@{
                evidencePreviewFile = $resolvedPreview
                replayRunId = $replayRunId
                replayStatus = $replayStatus
                observationCount = $observationCount
                blockingObservationCount = $blockingObservationCount
                warningObservationCount = $warningObservationCount
                mutationGuard = $mutationGuard
                noSensitiveContent = $true
            }
        }
    }
}

if (-not $ReplayEvidencePreviews) {
    $warnings += "Manual replay was not requested; workflow manifest records artifact and preview validation only."
}

$hasErrors = $errors.Count -gt 0
$decision = if ($hasErrors) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS_WITH_WARNINGS" } else { "PASS" }
$manifest = [ordered]@{
    workflowId = [guid]::NewGuid().ToString("N")
    createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    operatorId = $OperatorId
    reason = $Reason
    sourceStabilitySummaryPath = $stabilitySummaryPath
    replayRequested = [bool]$ReplayEvidencePreviews
    replayPerformed = ($replayResults.Count -gt 0)
    artifactCount = $artifactResults.Count
    evidencePreviewCount = $previewResults.Count
    manualReplayCount = $replayResults.Count
    runtimeShadowReplaySubmit = $false
    externalConnectionAttempted = $false
    orderSubmissionAttempted = $false
    shadowReplaySubmitAttempted = $false
    tradingMutationAttempted = $false
    schedulerStarted = $false
    credentialValuesReturned = $false
    noSensitiveContent = $true
    redactionStatus = "Redacted"
    snapshotArtifacts = @($artifactResults)
    evidencePreviews = @($previewResults)
    manualReplayResults = @($replayResults)
    stabilitySummaryPath = $stabilitySummaryPath
    warnings = @($warnings)
    errors = @($errors)
    finalDecision = $decision
}

$manifestDir = Join-Path $repoRoot "artifacts/lmax-readonly-runtime-demo-snapshot/workflow"
New-Item -ItemType Directory -Path $manifestDir -Force | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$manifestPath = Join-Path $manifestDir "lmax-readonly-marketdata-workflow-$stamp.json"
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

Write-Host ""
Write-Host "WorkflowManifest: $manifestPath"
Write-Host ("ArtifactCount: {0}" -f $artifactResults.Count)
Write-Host ("EvidencePreviewCount: {0}" -f $previewResults.Count)
Write-Host ("ManualReplayCount: {0}" -f $replayResults.Count)
Write-Host ("ReplayRequested: {0}" -f ([bool]$ReplayEvidencePreviews).ToString().ToLowerInvariant())
Write-Host ("ReplayPerformed: {0}" -f ($replayResults.Count -gt 0).ToString().ToLowerInvariant())
Write-Host ("WarningCount: {0}" -f $warnings.Count)
Write-Host ("ErrorCount: {0}" -f $errors.Count)
Write-Host "FinalDecision: $decision"
Write-Host "RuntimeShadowReplaySubmit: false"
Write-Host "ExternalConnectionAttempted: false"

if ($decision -eq "FAIL") { exit 1 }
