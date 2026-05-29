param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactFile,
    [string]$OutputDirectory,
    [switch]$NoWrite
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactPath = if ([IO.Path]::IsPathRooted($ArtifactFile)) { $ArtifactFile } else { Join-Path $repoRoot $ArtifactFile }
$artifactValidator = Join-Path $repoRoot "scripts/validate-lmax-readonly-runtime-demo-snapshot-artifact.ps1"
$evidenceValidator = Join-Path $repoRoot "scripts/validate-lmax-lab-evidence-file.ps1"

function Fail([string]$Message) {
    Write-Error $Message
    exit 1
}

function Get-JsonBool($Object, [string]$Name) {
    if ($null -eq $Object.PSObject.Properties[$Name]) { return $false }
    return [bool]$Object.$Name
}

function Test-ForbiddenPreviewContent([string]$Json) {
    $patterns = @(
        "554\s*=",
        "553\s*=",
        "password\s*[:=]\s*(?!\[REDACTED\])",
        "secret\s*[:=]\s*(?!\[REDACTED\])",
        "token\s*[:=]\s*(?!\[REDACTED\])",
        "apiKey\s*[:=]\s*(?!\[REDACTED\])",
        "privateKey\s*[:=]\s*(?!\[REDACTED\])",
        "bearer\s+",
        "authorization\s*[:=]",
        "rawFix",
        "NewOrderSingle",
        "OrderCancelRequest",
        "OrderCancelReplaceRequest"
    )

    foreach ($pattern in $patterns) {
        if ($Json -match $pattern) {
            return $pattern
        }
    }

    return $null
}

Write-Host "LMAX Read-Only Runtime Demo Snapshot Evidence Preview"
Write-Host "Local-only. No external connection, no shadow replay submit, no credentials required."

if (-not (Test-Path -LiteralPath $artifactPath)) {
    Fail "Artifact file does not exist: $artifactPath"
}

powershell -NoProfile -ExecutionPolicy Bypass -File $artifactValidator -ArtifactFile $artifactPath | Tee-Object -Variable artifactValidationOutput | Out-Host
if ($LASTEXITCODE -ne 0) {
    Fail "Snapshot artifact validation failed; evidence preview was not mapped."
}

$artifact = Get-Content -LiteralPath $artifactPath -Raw | ConvertFrom-Json
if (-not (Get-JsonBool $artifact "snapshotReceived")) { Fail "Artifact must have snapshotReceived=true." }
if (-not (Get-JsonBool $artifact "logonSucceeded")) { Fail "Artifact must have logonSucceeded=true." }
if (-not (Get-JsonBool $artifact "logoutSucceeded")) { Fail "Artifact must have logoutSucceeded=true." }
if (Get-JsonBool $artifact "orderSubmissionAttempted") { Fail "Artifact reports orderSubmissionAttempted=true." }
if (Get-JsonBool $artifact "shadowReplaySubmitAttempted") { Fail "Artifact reports shadowReplaySubmitAttempted=true." }
if (Get-JsonBool $artifact "tradingMutationAttempted") { Fail "Artifact reports tradingMutationAttempted=true." }
if (Get-JsonBool $artifact "credentialValuesReturned") { Fail "Artifact reports credentialValuesReturned=true." }

$createdAt = if ($artifact.completedAtUtc) { ([DateTimeOffset]$artifact.completedAtUtc).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ") } else { [DateTimeOffset]::UtcNow.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ") }
$capturedAt = if ($artifact.snapshotReceivedAtUtc) { ([DateTimeOffset]$artifact.snapshotReceivedAtUtc).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ") } else { $createdAt }
$securityId = if ($artifact.securityId) { [string]$artifact.securityId } else { "4001" }
$instrument = if ($artifact.instrument) { [string]$artifact.instrument } else { "EURUSD" }
$entryCount = if ($artifact.entryCount) { [int]$artifact.entryCount } else { 0 }

$entries = @()
if ($null -ne $artifact.bestBid) {
    $entries += [ordered]@{
        symbol = "EUR/USD"
        securityId = $securityId
        entryType = "0"
        price = [decimal]$artifact.bestBid
        size = 0
    }
}
if ($null -ne $artifact.bestAsk) {
    $entries += [ordered]@{
        symbol = "EUR/USD"
        securityId = $securityId
        entryType = "1"
        price = [decimal]$artifact.bestAsk
        size = 0
    }
}

$preview = [ordered]@{
    schemaVersion = "lmax-fix-lifecycle-evidence-v1"
    createdAtUtc = $createdAt
    capturedAtUtc = $capturedAt
    source = "RuntimeDemoReadOnlySnapshotArtifact"
    inputSource = "LabEvidenceFile"
    reason = "Preview sanitized runtime Demo read-only market-data snapshot artifact"
    environment = "Demo"
    captureMode = "RuntimeDemoReadOnlySnapshotPreview"
    redaction = "SanitizedNoCredentialsNoRawLogon"
    redactionStatus = "Redacted"
    noSensitiveContent = $true
    dryRun = $true
    shadowReplaySubmitAttempted = $false
    tradingMutationAttempted = $false
    orderSubmissionAttempted = $false
    instrument = $instrument
    instrumentSymbol = $instrument
    securityId = $securityId
    lmaxInstrumentId = $securityId
    slashSymbol = "EUR/USD"
    evidenceMode = "MarketDataOnly"
    marketData = [ordered]@{
        status = "Ok"
        snapshotReceived = $true
        bestBid = [decimal]$artifact.bestBid
        bestAsk = [decimal]$artifact.bestAsk
        mid = [decimal]$artifact.mid
        entryCount = $entryCount
        entries = @($entries)
    }
    executionReports = @()
    orderStatuses = @()
    tradeCaptureReports = @()
    protocolRejects = @()
    warnings = @()
}

$previewJson = $preview | ConvertTo-Json -Depth 12
$forbiddenPattern = Test-ForbiddenPreviewContent $previewJson
if ($null -ne $forbiddenPattern) {
    Fail "Mapped preview contains forbidden sensitive/order content pattern: $forbiddenPattern"
}

$previewPath = $null
if (-not $NoWrite.IsPresent) {
    $outDir = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
        Join-Path $repoRoot "artifacts/lmax-readonly-runtime-demo-snapshot/evidence-preview"
    } elseif ([IO.Path]::IsPathRooted($OutputDirectory)) {
        $OutputDirectory
    } else {
        Join-Path $repoRoot $OutputDirectory
    }

    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $previewPath = Join-Path $outDir "lmax-readonly-demo-snapshot-evidence-preview-$stamp.json"
    $previewJson | Set-Content -LiteralPath $previewPath -Encoding UTF8
} else {
    $previewPath = Join-Path ([IO.Path]::GetTempPath()) ("lmax-readonly-demo-snapshot-evidence-preview-" + [Guid]::NewGuid().ToString("N") + ".json")
    $previewJson | Set-Content -LiteralPath $previewPath -Encoding UTF8
}

powershell -NoProfile -ExecutionPolicy Bypass -File $evidenceValidator -EvidenceFile $previewPath | Tee-Object -Variable evidenceValidationOutput | Out-Host
$evidenceValidationSucceeded = $LASTEXITCODE -eq 0
if ($NoWrite.IsPresent -and (Test-Path -LiteralPath $previewPath)) {
    Remove-Item -LiteralPath $previewPath -Force
}

if (-not $evidenceValidationSucceeded) {
    Fail "Mapped evidence preview failed lmax-fix-lifecycle-evidence-v1 validation."
}

Write-Host ""
Write-Host "ArtifactStatus: $($artifact.status)"
Write-Host "SnapshotReceived: $($artifact.snapshotReceived)"
Write-Host "EvidenceMode: MarketDataOnly"
Write-Host "EvidenceValidationStatus: PASS"
Write-Host "NoSensitiveContent: true"
Write-Host "ExecutionReportCount: 0"
Write-Host "OrderStatusCount: 0"
Write-Host "TradeCaptureReportCount: 0"
Write-Host "ProtocolRejectCount: 0"
Write-Host "MarketDataSnapshotCount: 1"
Write-Host "ShadowReplaySubmitAttempted: false"
Write-Host "TradingMutationAttempted: false"
if (-not $NoWrite.IsPresent) {
    Write-Host "PreviewFile: $previewPath"
}
