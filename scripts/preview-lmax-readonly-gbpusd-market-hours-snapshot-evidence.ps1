param(
    [Parameter(Mandatory=$true)]
    [string]$ArtifactFile,
    [string]$OutputDirectory = "artifacts/lmax-readonly-runtime-additional-snapshot/gbpusd/evidence-preview"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$reviewScript = Join-Path $PSScriptRoot "review-lmax-readonly-gbpusd-market-hours-snapshot-result.ps1"
$evidenceValidator = Join-Path $PSScriptRoot "validate-lmax-lab-evidence-file.ps1"

function Resolve-LocalPath([string]$PathValue) {
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}

Write-Host "LMAX Read-Only Phase 7C GBPUSD Market-Hours Evidence Preview"
Write-Host "Local-only. No external connection, no shadow replay submit, no credentials required."

$artifactPath = Resolve-LocalPath $ArtifactFile
if (-not (Test-Path -LiteralPath $artifactPath)) { throw "Artifact file does not exist: $artifactPath" }

& $reviewScript -ArtifactFile $artifactPath
if ($LASTEXITCODE -ne 0) { throw "GBPUSD artifact review failed; evidence preview was not mapped." }
$review = Get-Content -Raw -LiteralPath (Join-Path $repoRoot "artifacts/readiness/phase7c-gbpusd-market-hours-snapshot-review.json") | ConvertFrom-Json
if ($review.finalDecision -eq "FAIL") { throw "Unsafe artifact review; evidence preview was not mapped." }
if ($review.closureClassification -eq "FailedSafe") { throw "Failed-safe artifacts are not mapped to MarketDataOnly evidence preview." }

$artifact = Get-Content -Raw -LiteralPath $artifactPath | ConvertFrom-Json
$createdAt = if ($artifact.completedAtUtc) { ([DateTimeOffset]$artifact.completedAtUtc).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ") } else { [DateTimeOffset]::UtcNow.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ") }
$capturedAt = if ($artifact.snapshotReceivedAtUtc) { ([DateTimeOffset]$artifact.snapshotReceivedAtUtc).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ") } else { $createdAt }
$emptyBook = [string]$artifact.status -eq "CompletedWithEmptyBook"
$entries = @()
if (-not $emptyBook) {
    $entries += [ordered]@{ symbol = "GBP/USD"; securityId = "4002"; entryType = "0"; price = [decimal]$artifact.bestBid; size = 0 }
    $entries += [ordered]@{ symbol = "GBP/USD"; securityId = "4002"; entryType = "1"; price = [decimal]$artifact.bestAsk; size = 0 }
}
$warnings = if ($emptyBook) { @("Market data snapshot received with no entries") } else { @() }

$preview = [ordered]@{
    schemaVersion = "lmax-fix-lifecycle-evidence-v1"
    createdAtUtc = $createdAt
    capturedAtUtc = $capturedAt
    source = "RuntimeDemoReadOnlySnapshotArtifact"
    inputSource = "LabEvidenceFile"
    reason = "Preview sanitized GBPUSD market-hours Demo read-only market-data snapshot artifact"
    environment = "Demo"
    captureMode = "RuntimeDemoReadOnlySnapshotPreview"
    redaction = "SanitizedNoCredentialsNoRawLogon"
    redactionStatus = "Redacted"
    noSensitiveContent = $true
    dryRun = $true
    shadowReplaySubmitAttempted = $false
    tradingMutationAttempted = $false
    orderSubmissionAttempted = $false
    instrument = "GBPUSD"
    instrumentSymbol = "GBPUSD"
    securityId = "4002"
    lmaxInstrumentId = "4002"
    slashSymbol = "GBP/USD"
    evidenceMode = "MarketDataOnly"
    marketData = [ordered]@{
        status = if ($emptyBook) { "EmptyBook" } else { "Ok" }
        snapshotReceived = $true
        bestBid = if ($emptyBook) { $null } else { [decimal]$artifact.bestBid }
        bestAsk = if ($emptyBook) { $null } else { [decimal]$artifact.bestAsk }
        mid = if ($emptyBook) { $null } else { [decimal]$artifact.mid }
        entryCount = [int]$artifact.entryCount
        entries = @($entries)
    }
    executionReports = @()
    orderStatuses = @()
    tradeCaptureReports = @()
    protocolRejects = @()
    warnings = $warnings
}

$previewJson = $preview | ConvertTo-Json -Depth 12
if ($previewJson -match '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|\b553=|\b554=|rawFix|NewOrderSingle|OrderCancelRequest|OrderCancelReplaceRequest)') {
    throw "Mapped evidence preview contains forbidden sensitive/order content."
}

$outDir = Resolve-LocalPath $OutputDirectory
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$previewPath = Join-Path $outDir "lmax-readonly-gbpusd-market-hours-evidence-preview-$stamp.json"
$previewJson | Set-Content -LiteralPath $previewPath -Encoding UTF8

powershell -NoProfile -ExecutionPolicy Bypass -File $evidenceValidator -EvidenceFile $previewPath | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Mapped evidence preview failed lmax-fix-lifecycle-evidence-v1 validation." }

Write-Host "EvidenceMode: MarketDataOnly"
Write-Host ("MarketDataStatus: {0}" -f $preview.marketData.status)
Write-Host ("EntryCount: {0}" -f $preview.marketData.entryCount)
Write-Host "ExecutionReportCount: 0"
Write-Host "OrderStatusCount: 0"
Write-Host "TradeCaptureReportCount: 0"
Write-Host "ProtocolRejectCount: 0"
Write-Host "ShadowReplaySubmitAttempted: false"
Write-Host "TradingMutationAttempted: false"
Write-Host "PreviewFile: $previewPath"
