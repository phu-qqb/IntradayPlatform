param(
    [Parameter(Mandatory = $true)]
    [string]$StabilitySummaryFile
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

function Add-Issue([System.Collections.Generic.List[object]]$Issues, [string]$Severity, [string]$Code, [string]$Path, [string]$Message) {
    $Issues.Add([ordered]@{ severity = $Severity; code = $Code; path = $Path; message = $Message }) | Out-Null
}

function Test-UnderSnapshotArtifacts([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    $root = [IO.Path]::GetFullPath((Join-Path $repoRoot "artifacts/lmax-readonly-runtime-demo-snapshot"))
    return $full.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)
}

function Get-Bool($Object, [string]$Name) {
    if ($null -eq $Object -or $null -eq $Object.PSObject.Properties[$Name]) { return $false }
    return [bool]$Object.$Name
}

Write-Host "LMAX Read-Only Runtime Phase 5P Stability Results Review"
Write-Host "Local-only. No LMAX connection, no runtime prototype call, no credentials, no shadow replay submit."

$summaryPath = if ([IO.Path]::IsPathRooted($StabilitySummaryFile)) { $StabilitySummaryFile } else { Join-Path $repoRoot $StabilitySummaryFile }
if (-not (Test-Path -LiteralPath $summaryPath)) {
    Write-Error "Stability summary file not found: $summaryPath"
    exit 1
}

$raw = Get-Content -LiteralPath $summaryPath -Raw
$summary = $raw | ConvertFrom-Json
$issues = [System.Collections.Generic.List[object]]::new()

$scanText = $raw `
    -replace 'credentialValuesReturned', '' `
    -replace 'LMAX_DEMO_FIX_USERNAME', '' `
    -replace 'LMAX_DEMO_FIX_PASSWORD', '' `
    -replace 'LMAX_DEMO_SENDER_COMP_ID', '' `
    -replace 'LMAX_DEMO_TARGET_COMP_ID', ''
$forbiddenPatterns = @(
    '(?i)554\s*=',
    '(?i)553\s*=',
    '(?i)password\s*[:=]\s*(?!\[REDACTED\])[^\\s,;\"}]+',
    '(?i)secret\s*[:=]\s*(?!\[REDACTED\])[^\\s,;\"}]+',
    '(?i)token\s*[:=]\s*(?!\[REDACTED\])[^\\s,;\"}]+',
    '(?i)apiKey\s*[:=]\s*(?!\[REDACTED\])[^\\s,;\"}]+',
    '(?i)privateKey\s*[:=]\s*(?!\[REDACTED\])[^\\s,;\"}]+',
    '(?i)bearer\s+(?!\[REDACTED\])[^\\s,;\"}]+',
    '(?i)authorization\s*[:=]\s*(?!\[REDACTED\])[^\\r\\n,;\"}]+',
    '(?i)rawFix',
    '(?i)NewOrderSingle|OrderCancelRequest|OrderCancelReplaceRequest|TradeCapture|OrderStatusRequest'
)
foreach ($pattern in $forbiddenPatterns) {
    if ($scanText -match $pattern) {
        Add-Issue $issues "Error" "ForbiddenSensitiveContent" "$" "Summary contains forbidden sensitive/order content pattern: $pattern"
    }
}

if ([int]$summary.attemptCountRequested -lt 1 -or [int]$summary.attemptCountRequested -gt 5) {
    Add-Issue $issues "Error" "AttemptCountOutOfRange" "$.attemptCountRequested" "AttemptCountRequested must be within 1..5."
}
if ([int]$summary.attemptCountCompleted -ne [int]$summary.attemptCountRequested) {
    Add-Issue $issues "Error" "AttemptCountNotCompleted" "$.attemptCountCompleted" "AttemptCountCompleted must equal AttemptCountRequested."
}
if ([int]$summary.successCount -ne [int]$summary.attemptCountRequested) {
    Add-Issue $issues "Error" "SuccessCountMismatch" "$.successCount" "SuccessCount must equal AttemptCountRequested."
}
if ([int]$summary.failedSafeCount -ne 0) {
    Add-Issue $issues "Error" "FailedSafeCountNonZero" "$.failedSafeCount" "FailedSafeCount must be 0 for Phase 5P successful closure."
}
if ([int]$summary.snapshotReceivedCount -ne [int]$summary.attemptCountRequested) {
    Add-Issue $issues "Error" "SnapshotReceivedCountMismatch" "$.snapshotReceivedCount" "SnapshotReceivedCount must equal AttemptCountRequested."
}

$requiredFalse = @("orderSubmissionAttempted", "shadowReplaySubmitAttempted", "tradingMutationAttempted", "schedulerStarted", "credentialValuesReturned")
foreach ($name in $requiredFalse) {
    if (Get-Bool $summary $name) {
        Add-Issue $issues "Error" "UnexpectedBooleanFlag" "`$.$name" "$name must be false."
    }
}
if (-not (Get-Bool $summary "noSensitiveContent")) {
    Add-Issue $issues "Error" "UnexpectedBooleanFlag" '$.noSensitiveContent' "noSensitiveContent must be true."
}
if ([string]$summary.redactionStatus -ne "Redacted") {
    Add-Issue $issues "Error" "RedactionStatusNotRedacted" '$.redactionStatus' "redactionStatus must be Redacted."
}

$attempts = @($summary.attempts)
for ($i = 0; $i -lt $attempts.Count; $i++) {
    $attempt = $attempts[$i]
    $attemptPath = "`$.attempts[$i]"
    if ([string]$attempt.status -notin @("Completed", "CompletedWithWarnings")) {
        Add-Issue $issues "Error" "AttemptStatusNotSuccessful" "$attemptPath.status" "Every attempt must be Completed or CompletedWithWarnings."
    }

    foreach ($name in $requiredFalse) {
        if (Get-Bool $attempt $name) {
            Add-Issue $issues "Error" "UnexpectedAttemptBooleanFlag" "$attemptPath.$name" "$name must be false for every attempt."
        }
    }
    if (-not (Get-Bool $attempt "snapshotReceived")) {
        Add-Issue $issues "Error" "AttemptSnapshotMissing" "$attemptPath.snapshotReceived" "Every attempt must receive a snapshot."
    }
    if (-not (Get-Bool $attempt "noSensitiveContent")) {
        Add-Issue $issues "Error" "AttemptSensitiveContentFlag" "$attemptPath.noSensitiveContent" "Every attempt must report noSensitiveContent=true."
    }

    $artifactPath = [string]$attempt.artifactPath
    if ([string]::IsNullOrWhiteSpace($artifactPath)) {
        Add-Issue $issues "Warning" "ArtifactPathMissing" "$attemptPath.artifactPath" "Artifact path missing; artifact validation skipped."
    } else {
        $resolvedArtifact = if ([IO.Path]::IsPathRooted($artifactPath)) { $artifactPath } else { Join-Path $repoRoot $artifactPath }
        if (-not (Test-UnderSnapshotArtifacts $resolvedArtifact)) {
            Add-Issue $issues "Error" "ArtifactOutsideSnapshotDirectory" "$attemptPath.artifactPath" "Artifact must live under artifacts/lmax-readonly-runtime-demo-snapshot."
        } elseif (Test-Path -LiteralPath $resolvedArtifact) {
            $artifactOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repoRoot "scripts/validate-lmax-readonly-runtime-demo-snapshot-artifact.ps1") -ArtifactFile $resolvedArtifact 2>&1
            if ($LASTEXITCODE -ne 0) {
                Add-Issue $issues "Error" "ReferencedArtifactValidationFailed" "$attemptPath.artifactPath" (($artifactOutput | Out-String).Trim())
            }
        } else {
            Add-Issue $issues "Warning" "ReferencedArtifactMissing" "$attemptPath.artifactPath" "Referenced artifact file is not present locally."
        }
    }

    $previewPath = [string]$attempt.evidencePreviewPath
    if ([string]::IsNullOrWhiteSpace($previewPath)) {
        Add-Issue $issues "Warning" "EvidencePreviewPathMissing" "$attemptPath.evidencePreviewPath" "Evidence preview path missing; preview validation skipped."
    } else {
        $resolvedPreview = if ([IO.Path]::IsPathRooted($previewPath)) { $previewPath } else { Join-Path $repoRoot $previewPath }
        if (-not (Test-UnderSnapshotArtifacts $resolvedPreview)) {
            Add-Issue $issues "Error" "EvidencePreviewOutsideSnapshotDirectory" "$attemptPath.evidencePreviewPath" "Evidence preview must live under artifacts/lmax-readonly-runtime-demo-snapshot."
        } elseif (Test-Path -LiteralPath $resolvedPreview) {
            $preview = Get-Content -LiteralPath $resolvedPreview -Raw | ConvertFrom-Json
            $executionCount = @($preview.executionReports).Count
            $orderCount = @($preview.orderStatuses).Count
            $tradeCount = @($preview.tradeCaptureReports).Count
            $rejectCount = @($preview.protocolRejects).Count
            if ([string]$preview.evidenceMode -ne "MarketDataOnly" -or $executionCount -ne 0 -or $orderCount -ne 0 -or $tradeCount -ne 0 -or $rejectCount -ne 0) {
                Add-Issue $issues "Error" "EvidencePreviewNotMarketDataOnly" "$attemptPath.evidencePreviewPath" "Evidence preview must be MarketDataOnly with empty execution/order/trade/reject arrays."
            }
            $previewOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repoRoot "scripts/validate-lmax-lab-evidence-file.ps1") -EvidenceFile $resolvedPreview 2>&1
            if ($LASTEXITCODE -ne 0) {
                Add-Issue $issues "Error" "EvidencePreviewValidationFailed" "$attemptPath.evidencePreviewPath" (($previewOutput | Out-String).Trim())
            }
        } else {
            Add-Issue $issues "Warning" "ReferencedEvidencePreviewMissing" "$attemptPath.evidencePreviewPath" "Referenced evidence preview file is not present locally."
        }
    }
}

$hasErrors = @($issues | Where-Object { $_.severity -eq "Error" }).Count -gt 0
$hasWarnings = @($issues | Where-Object { $_.severity -eq "Warning" }).Count -gt 0
$decision = if ($hasErrors) { "FAIL" } elseif ($hasWarnings) { "PASS_WITH_WARNINGS" } else { "PASS" }
$recommendation = if ($decision -eq "FAIL") {
    "Do not proceed. Resolve Phase 5P stability closure issues and rerun this review."
} else {
    "Ready to consider Phase 5Q controlled manual MarketData evidence workflow hardening. This does not authorize scheduler, polling, order submission, gateway registration, runtime shadow replay submit, trading mutation, or production use."
}

$result = [ordered]@{
    stabilitySummaryFile = (Resolve-Path -LiteralPath $summaryPath).Path
    decision = $decision
    attemptCountRequested = [int]$summary.attemptCountRequested
    attemptCountCompleted = [int]$summary.attemptCountCompleted
    successCount = [int]$summary.successCount
    failedSafeCount = [int]$summary.failedSafeCount
    snapshotReceivedCount = [int]$summary.snapshotReceivedCount
    orderSubmissionAttempted = Get-Bool $summary "orderSubmissionAttempted"
    shadowReplaySubmitAttempted = Get-Bool $summary "shadowReplaySubmitAttempted"
    tradingMutationAttempted = Get-Bool $summary "tradingMutationAttempted"
    schedulerStarted = Get-Bool $summary "schedulerStarted"
    credentialValuesReturned = Get-Bool $summary "credentialValuesReturned"
    noSensitiveContent = Get-Bool $summary "noSensitiveContent"
    readinessRecommendation = $recommendation
    issues = @($issues)
}

$json = $result | ConvertTo-Json -Depth 8
Write-Host $json
Write-Host "Decision: $decision"
Write-Host "ReadinessRecommendation: $recommendation"
Write-Host "RuntimeShadowReplaySubmit: false"
Write-Host "ExternalConnectionAttemptedByReview: false"

if ($decision -eq "FAIL") { exit 1 }
