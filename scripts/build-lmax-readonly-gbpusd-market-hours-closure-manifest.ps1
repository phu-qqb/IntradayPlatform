param(
    [Parameter(Mandatory=$true)]
    [string]$ArtifactFile,
    [string]$EvidencePreviewFile = "",
    [string]$ReplayReportFile = "",
    [string]$OutputDirectory = "artifacts/lmax-readonly-runtime-additional-snapshot/gbpusd/closure"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$reviewScript = Join-Path $PSScriptRoot "review-lmax-readonly-gbpusd-market-hours-snapshot-result.ps1"
$evidenceValidator = Join-Path $PSScriptRoot "validate-lmax-lab-evidence-file.ps1"

function Resolve-LocalPath([string]$PathValue) {
    if ([string]::IsNullOrWhiteSpace($PathValue)) { return "" }
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}

$artifactPath = Resolve-LocalPath $ArtifactFile
& $reviewScript -ArtifactFile $artifactPath
if ($LASTEXITCODE -ne 0) { throw "Artifact review failed; closure manifest was not built." }
$reviewPath = Join-Path $repoRoot "artifacts/readiness/phase7c-gbpusd-market-hours-snapshot-review.json"
$review = Get-Content -Raw -LiteralPath $reviewPath | ConvertFrom-Json

$previewDecision = "NotProvided"
$previewPath = Resolve-LocalPath $EvidencePreviewFile
if (-not [string]::IsNullOrWhiteSpace($previewPath)) {
    if (-not (Test-Path -LiteralPath $previewPath)) { throw "Evidence preview file not found: $previewPath" }
    powershell -NoProfile -ExecutionPolicy Bypass -File $evidenceValidator -EvidenceFile $previewPath | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Evidence preview validation failed." }
    $preview = Get-Content -Raw -LiteralPath $previewPath | ConvertFrom-Json
    if ([string]$preview.evidenceMode -ne "MarketDataOnly" -or [string]$preview.instrument -ne "GBPUSD" -or [string]$preview.securityId -ne "4002") { throw "Evidence preview must be GBPUSD MarketDataOnly / 4002." }
    $previewDecision = "PASS"
}

$replayDecision = "NotProvided"
$replayPath = Resolve-LocalPath $ReplayReportFile
if (-not [string]::IsNullOrWhiteSpace($replayPath)) {
    if (-not (Test-Path -LiteralPath $replayPath)) { throw "Replay report file not found: $replayPath" }
    $replay = Get-Content -Raw -LiteralPath $replayPath | ConvertFrom-Json
    if ([string]$replay.finalDecision -eq "PASS" -and [string]$replay.replayStatus -eq "Completed" -and [int]$replay.observationCount -eq 0 -and [string]$replay.mutationGuard -eq "Unchanged") { $replayDecision = "PASS" } else { $replayDecision = "FAIL" }
}

$finalDecision = if ($review.finalDecision -eq "FAIL" -or $previewDecision -eq "FAIL" -or $replayDecision -eq "FAIL") {
    "FAIL"
} elseif ($review.closureClassification -eq "CompletedWithBook" -and $previewDecision -eq "PASS") {
    "PASS"
} elseif ($review.closureClassification -eq "CompletedWithEmptyBook" -and ($previewDecision -in @("PASS", "NotProvided"))) {
    "PASS_WITH_KNOWN_WARNINGS"
} elseif ($review.closureClassification -eq "FailedSafe") {
    "PASS_WITH_KNOWN_WARNINGS"
} else {
    "FAIL"
}

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$manifest = [ordered]@{
    manifestId = "lmax-readonly-gbpusd-market-hours-closure-$stamp"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7C"
    artifactFile = $artifactPath
    artifactReviewReportFile = $reviewPath
    evidencePreviewFile = $previewPath
    replayReportFile = $replayPath
    symbol = "GBPUSD"
    slashSymbol = "GBP/USD"
    securityId = "4002"
    securityIdSource = "8"
    artifactReviewDecision = $review.finalDecision
    closureClassification = $review.closureClassification
    evidencePreviewDecision = $previewDecision
    replayDecision = $replayDecision
    finalClosureDecision = $finalDecision
    externalConnectionAttemptedByClosure = $false
    snapshotAttemptedByClosure = $false
    replayAttemptedByClosure = $false
    orderSubmissionAttempted = $false
    shadowReplaySubmitAttempted = $false
    tradingMutationAttempted = $false
    schedulerStarted = $false
    noSensitiveContent = $true
}

$json = $manifest | ConvertTo-Json -Depth 12
if ($json -match '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|\b553=|\b554=|rawFix|NewOrderSingle|OrderCancelRequest|OrderCancelReplaceRequest)') { throw "Closure manifest contains forbidden sensitive/order content." }
$outDir = Resolve-LocalPath $OutputDirectory
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "lmax-readonly-gbpusd-market-hours-closure-$stamp.json"
$json | Set-Content -LiteralPath $outPath -Encoding UTF8
Write-Host "ClosureClassification: $($review.closureClassification)"
Write-Host "FinalClosureDecision: $finalDecision"
Write-Host "ClosureManifestFile: $outPath"
if ($finalDecision -eq "FAIL") { exit 1 }
