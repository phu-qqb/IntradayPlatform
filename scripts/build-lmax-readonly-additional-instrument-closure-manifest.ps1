param(
    [Parameter(Mandatory=$true)]
    [string]$ArtifactFile,
    [string]$EvidencePreviewFile = "",
    [string]$ReplayReportFile = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$reviewScript = Join-Path $PSScriptRoot "review-lmax-readonly-additional-instrument-snapshot-result.ps1"
$evidenceValidator = Join-Path $PSScriptRoot "validate-lmax-lab-evidence-file.ps1"

function Resolve-LocalPath([string]$PathValue) {
    if ([string]::IsNullOrWhiteSpace($PathValue)) { return "" }
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}

$artifactPath = Resolve-LocalPath $ArtifactFile
& $reviewScript -ArtifactFile $artifactPath | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Artifact review failed; closure manifest not written." }
$artifact = Get-Content -Raw -LiteralPath $artifactPath | ConvertFrom-Json
$symbol = ([string]$artifact.symbol).ToUpperInvariant()
$reviewPath = Join-Path $repoRoot ("artifacts/readiness/phase7h-additional-instrument-snapshot-review-{0}.json" -f $symbol.ToLowerInvariant())
$review = Get-Content -Raw -LiteralPath $reviewPath | ConvertFrom-Json

$issues = @()
$previewDecision = "NOT_SUPPLIED"
if (-not [string]::IsNullOrWhiteSpace($EvidencePreviewFile)) {
    $previewPath = Resolve-LocalPath $EvidencePreviewFile
    if (-not (Test-Path -LiteralPath $previewPath)) { $issues += "MissingEvidencePreview" } else {
        powershell -NoProfile -ExecutionPolicy Bypass -File $evidenceValidator -EvidenceFile $previewPath | Out-Host
        if ($LASTEXITCODE -ne 0) { $issues += "EvidencePreviewValidationFailed"; $previewDecision = "FAIL" } else {
            $preview = Get-Content -Raw -LiteralPath $previewPath | ConvertFrom-Json
            if ([string]$preview.evidenceMode -eq "MarketDataOnly" -and [string]$preview.instrument -eq $symbol -and [bool]$preview.noSensitiveContent -and -not [bool]$preview.orderSubmissionAttempted -and -not [bool]$preview.shadowReplaySubmitAttempted -and -not [bool]$preview.tradingMutationAttempted) {
                $previewDecision = "PASS"
            } else {
                $issues += "EvidencePreviewUnsafeOrWrongIdentity"
                $previewDecision = "FAIL"
            }
        }
    }
}

$replayDecision = "NOT_SUPPLIED"
if (-not [string]::IsNullOrWhiteSpace($ReplayReportFile)) {
    $replayPath = Resolve-LocalPath $ReplayReportFile
    if (-not (Test-Path -LiteralPath $replayPath)) { $issues += "MissingReplayReport" } else {
        $replay = Get-Content -Raw -LiteralPath $replayPath | ConvertFrom-Json
        if ([string]$replay.finalDecision -eq "PASS" -and [string]$replay.replayStatus -eq "Completed" -and [int]$replay.observationCount -eq 0 -and [string]$replay.mutationGuard -eq "Unchanged" -and -not [bool]$replay.runtimeShadowReplaySubmit -and -not [bool]$replay.externalConnectionAttempted -and [bool]$replay.noSensitiveContent) {
            $replayDecision = "PASS"
        } else {
            $issues += "ReplayReportUnsafe"
            $replayDecision = "FAIL"
        }
    }
}

$finalDecision = if ($issues.Count -gt 0 -or [string]$review.finalDecision -eq "FAIL") {
    "FAIL"
} elseif ([string]$review.closureClassification -eq "CompletedWithBook" -and ($previewDecision -eq "PASS" -or $previewDecision -eq "NOT_SUPPLIED") -and ($replayDecision -eq "PASS" -or $replayDecision -eq "NOT_SUPPLIED")) {
    "PASS"
} elseif ([string]$review.closureClassification -in @("CompletedWithEmptyBook","FailedSafe")) {
    "PASS_WITH_KNOWN_WARNINGS"
} else {
    "FAIL"
}

$manifest = [ordered]@{
    manifestId = "phase7h-additional-instrument-closure-" + [guid]::NewGuid().ToString("N")
    createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7H"
    symbol = $symbol
    slashSymbol = [string]$artifact.slashSymbol
    securityId = [string]$artifact.securityId
    securityIdSource = [string]$artifact.securityIdSource
    sourceArtifactFile = "$artifactPath"
    sourceReviewFile = "$reviewPath"
    evidencePreviewFile = if ([string]::IsNullOrWhiteSpace($EvidencePreviewFile)) { $null } else { "$(Resolve-LocalPath $EvidencePreviewFile)" }
    replayReportFile = if ([string]::IsNullOrWhiteSpace($ReplayReportFile)) { $null } else { "$(Resolve-LocalPath $ReplayReportFile)" }
    artifactReviewDecision = [string]$review.finalDecision
    closureClassification = [string]$review.closureClassification
    evidencePreviewDecision = $previewDecision
    replayDecision = $replayDecision
    finalClosureDecision = $finalDecision
    orderSubmissionAttempted = $false
    shadowReplaySubmitAttempted = $false
    tradingMutationAttempted = $false
    schedulerStarted = $false
    credentialValuesReturned = $false
    noSensitiveContent = $true
    issues = $issues
}

$outDir = Join-Path $repoRoot ("artifacts/lmax-readonly-runtime-additional-snapshot/{0}/closure" -f $symbol.ToLowerInvariant())
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$jsonPath = Join-Path $outDir ("lmax-readonly-{0}-closure-manifest-{1}.json" -f $symbol.ToLowerInvariant(), $stamp)
$mdPath = Join-Path $outDir ("lmax-readonly-{0}-closure-manifest-{1}.md" -f $symbol.ToLowerInvariant(), $stamp)
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
@(
    "# LMAX Read-Only Additional Instrument Closure Manifest",
    "",
    "- Phase: 7H",
    "- Symbol: $symbol / $($artifact.slashSymbol) / $($artifact.securityId)",
    "- Closure classification: $($review.closureClassification)",
    "- Artifact review decision: $($review.finalDecision)",
    "- Evidence preview decision: $previewDecision",
    "- Replay decision: $replayDecision",
    "- Final closure decision: $finalDecision",
    "- No scheduler/polling, runtime shadow replay submit, orders, gateway registration, or trading mutation.",
    "- API/Worker remain FakeLmaxGateway only."
) | Set-Content -LiteralPath $mdPath -Encoding UTF8

Write-Host "Symbol: $symbol"
Write-Host "ClosureClassification: $($review.closureClassification)"
Write-Host "FinalClosureDecision: $finalDecision"
Write-Host "Manifest: $jsonPath"
if ($finalDecision -eq "FAIL") { exit 1 }
