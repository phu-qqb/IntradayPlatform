param(
    [switch]$AllowExternalConnections,
    [switch]$ConfirmDemoReadOnly,
    [switch]$ConfirmRepeatedManualSnapshots,
    [Parameter(Mandatory = $false)]
    [string]$Reason,
    [string]$OperatorId = "local-operator",
    [int]$AttemptCount = 0,
    [int]$DelaySeconds = 2,
    [switch]$ContinueOnFailedSafe,
    [switch]$ReplayEvidencePreviews,
    [string]$BaseUrl = "http://localhost:5050"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$prototypeScript = Join-Path $repoRoot "scripts/run-lmax-readonly-runtime-demo-snapshot-prototype.ps1"
$artifactValidator = Join-Path $repoRoot "scripts/validate-lmax-readonly-runtime-demo-snapshot-artifact.ps1"
$previewScript = Join-Path $repoRoot "scripts/preview-lmax-readonly-demo-snapshot-evidence.ps1"
$replayScript = Join-Path $repoRoot "scripts/replay-lmax-readonly-demo-snapshot-evidence-preview.ps1"

function Fail([string]$Message, [int]$Code = 2) {
    Write-Error $Message
    exit $Code
}

function Get-JsonBool($Object, [string]$Name) {
    if ($null -eq $Object -or $null -eq $Object.PSObject.Properties[$Name]) { return $false }
    return [bool]$Object.$Name
}

function Get-SanitizedArtifactPath([string[]]$Lines) {
    $line = @($Lines | Where-Object { $_ -match '^Sanitized artifact:\s*(.+)$' } | Select-Object -Last 1)
    if ($line.Count -eq 0) { return $null }
    return ($line[-1] -replace '^Sanitized artifact:\s*', '').Trim()
}

function Get-PreviewPath([string[]]$Lines) {
    $line = @($Lines | Where-Object { $_ -match '^PreviewFile:\s*(.+)$' } | Select-Object -Last 1)
    if ($line.Count -eq 0) { return $null }
    return ($line[-1] -replace '^PreviewFile:\s*', '').Trim()
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

Write-Host "LMAX Read-Only Runtime Demo Snapshot Stability Check"
Write-Host "WARNING: Demo-only, manual-only, repeated read-only market-data snapshots."
Write-Host "WARNING: EURUSD / SecurityID 4001 only. No orders, no scheduler, no runtime shadow replay submit, no trading mutation."
Write-Host "WARNING: This is not polling and not retry. Each attempt is a planned manual stability attempt."
Write-Host "Rollback: press Ctrl+C/close process, clear shell-only prototype variables, verify /health FakeLmaxGateway, run Phase 5O gate."

if (-not $AllowExternalConnections) { Fail "-AllowExternalConnections is required." }
if (-not $ConfirmDemoReadOnly) { Fail "-ConfirmDemoReadOnly is required." }
if (-not $ConfirmRepeatedManualSnapshots) { Fail "-ConfirmRepeatedManualSnapshots is required." }
if ([string]::IsNullOrWhiteSpace($Reason)) { Fail "-Reason is required." }
if ($AttemptCount -lt 1 -or $AttemptCount -gt 5) { Fail "-AttemptCount must be within 1..5." }
if ($DelaySeconds -lt 1 -or $DelaySeconds -gt 10) { Fail "-DelaySeconds must be within 1..10." }
if ($ReplayEvidencePreviews -and -not (Test-ApiAvailable $BaseUrl)) { Fail "-ReplayEvidencePreviews requires local API available at $BaseUrl." }

$runGroupId = [guid]::NewGuid().ToString("N")
$startedAt = [DateTimeOffset]::UtcNow
$attempts = @()
$warnings = @()
$errors = @()

for ($attemptNumber = 1; $attemptNumber -le $AttemptCount; $attemptNumber++) {
    Write-Host ""
    Write-Host ("Starting planned manual stability attempt {0}/{1}" -f $attemptNumber, $AttemptCount)

    $output = & powershell -NoProfile -ExecutionPolicy Bypass -File $prototypeScript `
        -AllowExternalConnections `
        -ConfirmDemoReadOnly `
        -Reason "$Reason (attempt $attemptNumber of $AttemptCount)" `
        -OperatorId $OperatorId `
        -RequestMode SnapshotPlusUpdates `
        -SymbolEncodingMode SecurityIdOnly `
        -MarketDepth 1 `
        -MaxWaitSeconds 30 `
        -MaxRuntimeSeconds 30 `
        -MaxEventsPerRun 25 2>&1

    $exitCode = $LASTEXITCODE
    $lines = @($output | ForEach-Object { [string]$_ })
    $lines | ForEach-Object { Write-Host $_ }
    $artifactPath = Get-SanitizedArtifactPath $lines
    if ([string]::IsNullOrWhiteSpace($artifactPath) -or -not (Test-Path -LiteralPath $artifactPath)) {
        $errors += "Attempt $attemptNumber did not produce a sanitized artifact path."
        if (-not $ContinueOnFailedSafe) { break }
        continue
    }

    $artifact = Get-Content -LiteralPath $artifactPath -Raw | ConvertFrom-Json
    $status = [string]$artifact.status
    $isSuccess = $status -in @("Completed", "CompletedWithWarnings")
    $isKnownFailedSafe = $status -like "FailedSafe*" -or $status -like "Blocked*"
    $previewPath = $null
    $replayStatus = $null

    if ($isSuccess) {
        powershell -NoProfile -ExecutionPolicy Bypass -File $artifactValidator -ArtifactFile $artifactPath | Out-Host
        if ($LASTEXITCODE -ne 0) {
            $errors += "Attempt $attemptNumber successful artifact failed validation."
            if (-not $ContinueOnFailedSafe) { break }
        }

        $previewOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $previewScript -ArtifactFile $artifactPath 2>&1
        $previewLines = @($previewOutput | ForEach-Object { [string]$_ })
        $previewLines | ForEach-Object { Write-Host $_ }
        if ($LASTEXITCODE -ne 0) {
            $errors += "Attempt $attemptNumber evidence preview mapping failed."
            if (-not $ContinueOnFailedSafe) { break }
        }
        $previewPath = Get-PreviewPath $previewLines

        if ($ReplayEvidencePreviews) {
            $replayOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $replayScript -EvidencePreviewFile $previewPath -BaseUrl $BaseUrl 2>&1
            $replayLines = @($replayOutput | ForEach-Object { [string]$_ })
            $replayLines | ForEach-Object { Write-Host $_ }
            if ($LASTEXITCODE -ne 0) {
                $errors += "Attempt $attemptNumber manual replay dry-run failed."
                if (-not $ContinueOnFailedSafe) { break }
            } else {
                $replayStatus = "Completed"
            }
        }
    } elseif (-not $isKnownFailedSafe) {
        $errors += "Attempt $attemptNumber returned unknown status '$status'."
        if (-not $ContinueOnFailedSafe) { break }
    }

    $attempts += [ordered]@{
        attemptNumber = $attemptNumber
        status = $status
        artifactPath = $artifactPath
        evidencePreviewPath = $previewPath
        replayStatus = $replayStatus
        snapshotReceived = Get-JsonBool $artifact "snapshotReceived"
        bestBid = $artifact.bestBid
        bestAsk = $artifact.bestAsk
        mid = $artifact.mid
        waitDurationMs = $artifact.diagnostics.request.waitDurationMs
        externalConnectionAttempted = Get-JsonBool $artifact "externalConnectionAttempted"
        logonSucceeded = Get-JsonBool $artifact "logonSucceeded"
        logoutSucceeded = Get-JsonBool $artifact "logoutSucceeded"
        credentialValuesReturned = Get-JsonBool $artifact "credentialValuesReturned"
        orderSubmissionAttempted = Get-JsonBool $artifact "orderSubmissionAttempted"
        shadowReplaySubmitAttempted = Get-JsonBool $artifact "shadowReplaySubmitAttempted"
        tradingMutationAttempted = Get-JsonBool $artifact "tradingMutationAttempted"
        schedulerStarted = Get-JsonBool $artifact "schedulerStarted"
        noSensitiveContent = Get-JsonBool $artifact "noSensitiveContent"
    }

    if (-not $isSuccess -and -not $ContinueOnFailedSafe) {
        $warnings += "Stopped after attempt $attemptNumber with status $status. Use -ContinueOnFailedSafe only after reviewing sanitized failure details."
        break
    }

    if ($attemptNumber -lt $AttemptCount) {
        Start-Sleep -Seconds $DelaySeconds
    }
}

$completedAt = [DateTimeOffset]::UtcNow
$successCount = @($attempts | Where-Object { $_.status -in @("Completed", "CompletedWithWarnings") }).Count
$failedSafeCount = @($attempts | Where-Object { $_.status -notin @("Completed", "CompletedWithWarnings") }).Count
$snapshotReceivedCount = @($attempts | Where-Object { $_.snapshotReceived }).Count
$unsafeAttempts = @($attempts | Where-Object { $_.credentialValuesReturned -or $_.orderSubmissionAttempted -or $_.shadowReplaySubmitAttempted -or $_.tradingMutationAttempted -or $_.schedulerStarted -or -not $_.noSensitiveContent })

if ($unsafeAttempts.Count -gt 0) {
    $errors += "One or more attempts reported unsafe flags. Review sanitized artifacts immediately."
}

$summary = [ordered]@{
    runGroupId = $runGroupId
    startedAtUtc = $startedAt.ToString("o")
    completedAtUtc = $completedAt.ToString("o")
    attemptCountRequested = $AttemptCount
    attemptCountCompleted = $attempts.Count
    successCount = $successCount
    failedSafeCount = $failedSafeCount
    snapshotReceivedCount = $snapshotReceivedCount
    statusesByAttempt = @($attempts | ForEach-Object { [ordered]@{ attemptNumber = $_.attemptNumber; status = $_.status } })
    noSensitiveContent = $unsafeAttempts.Count -eq 0
    redactionStatus = "Redacted"
    credentialValuesReturned = $false
    orderSubmissionAttempted = $false
    shadowReplaySubmitAttempted = $false
    tradingMutationAttempted = $false
    schedulerStarted = $false
    replayEvidencePreviews = [bool]$ReplayEvidencePreviews
    attempts = @($attempts)
    warnings = @($warnings)
    errors = @($errors)
    rollbackInstructions = @(
        "Stop this local process.",
        "Clear Phase 5O prototype environment variables from this shell.",
        "Start API with the default disabled run path.",
        "Verify /health reports FakeLmaxGateway and liveTradingEnabled=false.",
        "Run scripts/check-lmax-readonly-runtime-phase5o-stability-gate.ps1.",
        "No DB rollback is expected because no trading-state mutation is allowed."
    )
}

$summaryDir = Join-Path $repoRoot "artifacts/lmax-readonly-runtime-demo-snapshot/stability"
New-Item -ItemType Directory -Path $summaryDir -Force | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$summaryPath = Join-Path $summaryDir "lmax-readonly-demo-snapshot-stability-$stamp.json"
$summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $summaryPath -Encoding UTF8

Write-Host ""
Write-Host "StabilitySummary: $summaryPath"
Write-Host ("AttemptCountRequested: {0}" -f $AttemptCount)
Write-Host ("AttemptCountCompleted: {0}" -f $attempts.Count)
Write-Host ("SuccessCount: {0}" -f $successCount)
Write-Host ("FailedSafeCount: {0}" -f $failedSafeCount)
Write-Host ("SnapshotReceivedCount: {0}" -f $snapshotReceivedCount)
Write-Host "OrderSubmissionAttempted: false"
Write-Host "ShadowReplaySubmitAttempted: false"
Write-Host "TradingMutationAttempted: false"
Write-Host "SchedulerStarted: false"
Write-Host "CredentialValuesReturned: false"
Write-Host "Rollback: stop this process, clear this shell's Phase 5O variables, run default API startup, verify /health FakeLmaxGateway, then run the Phase 5O stability gate."

if ($errors.Count -gt 0) { exit 1 }
if ($successCount -eq $AttemptCount) { exit 0 }
exit 1
