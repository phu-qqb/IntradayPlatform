param(
    [Parameter(Mandatory=$true)]
    [string]$ArtifactFile,
    [string]$Reason = "Phase 7H additional-instrument MarketDataOnly evidence preview"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$reviewScript = Join-Path $PSScriptRoot "review-lmax-readonly-additional-instrument-snapshot-result.ps1"
$validator = Join-Path $PSScriptRoot "validate-lmax-lab-evidence-file.ps1"

function Resolve-LocalPath([string]$PathValue) {
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}
function New-JsonArray($Items) {
    $array = [System.Collections.ArrayList]::new()
    foreach ($item in @($Items)) { [void]$array.Add($item) }
    return $array
}

$artifactPath = Resolve-LocalPath $ArtifactFile
$artifact = Get-Content -Raw -LiteralPath $artifactPath | ConvertFrom-Json
$symbol = ([string]$artifact.symbol).ToUpperInvariant()
$reviewPath = Join-Path $repoRoot ("artifacts/readiness/phase7h-additional-instrument-snapshot-review-{0}.json" -f $symbol.ToLowerInvariant())

& $reviewScript -ArtifactFile $artifactPath | Out-Host
if (-not (Test-Path -LiteralPath $reviewPath)) {
    $reviewExitCode = if ($null -eq $LASTEXITCODE) { 0 } else { [int]$LASTEXITCODE }
    throw "Additional-instrument artifact review report was not written. Review exit code: $reviewExitCode."
}

$review = Get-Content -Raw -LiteralPath $reviewPath | ConvertFrom-Json
$reviewDecision = if ($review.PSObject.Properties.Name -contains "finalDecision") { [string]$review.finalDecision } elseif ($review.PSObject.Properties.Name -contains "decision") { [string]$review.decision } else { "" }
$reviewClassification = [string]$review.closureClassification
if ($reviewDecision -eq "FAIL" -or $reviewClassification -eq "UnsafeFail") { throw "Unsafe artifact cannot be mapped." }
if ($reviewDecision -notin @("PASS", "PASS_WITH_KNOWN_WARNINGS")) { throw "Additional-instrument artifact review did not produce a safe decision: $reviewDecision." }
if ($reviewClassification -eq "FailedSafe") { throw "FailedSafe artifact is safe to retain but cannot be mapped to a MarketData snapshot preview." }
if ($reviewClassification -notin @("CompletedWithBook", "CompletedWithEmptyBook")) { throw "Additional-instrument artifact review classification cannot be mapped: $reviewClassification." }

$isEmptyBook = $reviewClassification -eq "CompletedWithEmptyBook"
$warnings = if ($isEmptyBook) { @("Market data snapshot received with no entries") + @($review.warnings) } else { @() }
$entries = @()
if (-not $isEmptyBook) {
    $entries = @(
        [ordered]@{ symbol = [string]$artifact.slashSymbol; securityId = [string]$artifact.securityId; entryType = "0"; price = $artifact.bestBid; size = 0 },
        [ordered]@{ symbol = [string]$artifact.slashSymbol; securityId = [string]$artifact.securityId; entryType = "1"; price = $artifact.bestAsk; size = 0 }
    )
}

$createdAt = if ($artifact.completedAtUtc) { [string]$artifact.completedAtUtc } else { [DateTimeOffset]::UtcNow.ToString("o") }
$preview = [ordered]@{
    schemaVersion = "lmax-fix-lifecycle-evidence-v1"
    createdAtUtc = $createdAt
    capturedAtUtc = $createdAt
    source = "RuntimeDemoReadOnlySnapshotArtifact"
    inputSource = "LabEvidenceFile"
    reason = $Reason
    environment = "Demo"
    captureMode = "RuntimeDemoReadOnlySnapshotPreview"
    redaction = "SanitizedNoCredentialsNoRawLogon"
    redactionStatus = "Redacted"
    noSensitiveContent = $true
    dryRun = $true
    shadowReplaySubmitAttempted = $false
    tradingMutationAttempted = $false
    orderSubmissionAttempted = $false
    instrument = $symbol
    instrumentSymbol = $symbol
    securityId = [string]$artifact.securityId
    lmaxInstrumentId = [string]$artifact.securityId
    slashSymbol = [string]$artifact.slashSymbol
    evidenceMode = "MarketDataOnly"
    marketData = [ordered]@{
        status = if ($isEmptyBook) { "EmptyBook" } else { "Ok" }
        snapshotReceived = $true
        bestBid = if ($isEmptyBook) { $null } else { $artifact.bestBid }
        bestAsk = if ($isEmptyBook) { $null } else { $artifact.bestAsk }
        mid = if ($isEmptyBook) { $null } else { $artifact.mid }
        entryCount = if ($isEmptyBook) { 0 } else { [int]$artifact.entryCount }
        entries = $entries
    }
    executionReports = @()
    orderStatuses = @()
    tradeCaptureReports = @()
    protocolRejects = @()
    warnings = @($warnings | Select-Object -Unique)
}

$outDir = Join-Path $repoRoot ("artifacts/lmax-readonly-runtime-additional-snapshot/{0}/evidence-preview" -f $symbol.ToLowerInvariant())
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$outPath = Join-Path $outDir ("lmax-readonly-{0}-evidence-preview-{1}.json" -f $symbol.ToLowerInvariant(), $stamp)
$preview | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $outPath -Encoding UTF8

& powershell -NoProfile -ExecutionPolicy Bypass -File $validator -EvidenceFile $outPath | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Evidence preview validation failed." }

Write-Host "EvidenceMode: MarketDataOnly"
Write-Host ("Instrument: {0} / {1}" -f $symbol, $artifact.securityId)
Write-Host ("ExecutionReports/OrderStatuses/TradeCaptureReports/ProtocolRejects: 0/0/0/0")
Write-Host "EvidencePreview: $outPath"
