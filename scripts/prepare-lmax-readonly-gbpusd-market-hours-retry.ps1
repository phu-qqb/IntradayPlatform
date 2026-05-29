param(
    [Parameter(Mandatory=$true)]
    [string]$FinalReadinessFile,
    [Parameter(Mandatory=$true)]
    [string]$Phase6XReviewFile,
    [Parameter(Mandatory=$true)]
    [string]$RequestedByOperatorId,
    [Parameter(Mandatory=$true)]
    [string]$Reason,
    [string]$OutputDirectory = "artifacts/lmax-readonly-runtime-securityid-planning/market-hours-retry",
    [switch]$WhatIfPreview,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

function Resolve-LocalPath([string]$PathValue) {
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}

function Assert-SafeText([string]$Name, [string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "$Name is required."
    }

    if ($Value -match '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=)') {
        throw "$Name contains credential-shaped content."
    }

    if ($Value -match '(?i)(NewOrderSingle|OrderCancelRequest|OrderCancelReplaceRequest|TradeCapture|OrderStatusRequest|order submission|submit order|production|uat|run automatically|automatic retry)') {
        throw "$Name contains unsafe authorization/trading language."
    }
}

Assert-SafeText "RequestedByOperatorId" $RequestedByOperatorId
Assert-SafeText "Reason" $Reason

$finalPath = Resolve-LocalPath $FinalReadinessFile
$reviewPath = Resolve-LocalPath $Phase6XReviewFile
if (-not (Test-Path -LiteralPath $finalPath)) { throw "Final readiness file not found: $finalPath" }
if (-not (Test-Path -LiteralPath $reviewPath)) { throw "Phase 6X review file not found: $reviewPath" }

$finalRaw = Get-Content -LiteralPath $finalPath -Raw
$reviewRaw = Get-Content -LiteralPath $reviewPath -Raw
$final = $finalRaw | ConvertFrom-Json
$review = $reviewRaw | ConvertFrom-Json

if ([string]$final.readinessDecision -ne "PASS") { throw "Final readiness must be PASS." }
if ([string]$final.symbol -ne "GBPUSD" -or [string]$final.planningSecurityId -ne "4002" -or [string]$final.securityIdSource -ne "8") { throw "Final readiness must be GBPUSD SecurityID 4002 / SecurityIDSource 8." }
if ([bool]$final.canRunExternalSnapshot -or [bool]$final.isApprovedForExternalRun -or [bool]$final.eligibleForManualSnapshotAttempt -or [bool]$final.schedulerStarted -or [bool]$final.runtimeShadowReplaySubmit -or [bool]$final.orderSubmissionAttempted -or [bool]$final.tradingMutationAttempted) {
    throw "Final readiness contains executable or unsafe flags."
}

if ([string]$review.status -ne "CompletedWithEmptyBook") { throw "Phase 6X review must be CompletedWithEmptyBook." }
if ([string]$review.finalDecision -ne "PASS_WITH_KNOWN_WARNINGS") { throw "Phase 6X review must be PASS_WITH_KNOWN_WARNINGS." }
if ([string]$review.symbol -ne "GBPUSD" -or [string]$review.securityId -ne "4002" -or [string]$review.securityIdSource -ne "8") { throw "Phase 6X review must be GBPUSD SecurityID 4002 / SecurityIDSource 8." }
if (-not [bool]$review.snapshotReceived -or [int]$review.entryCount -ne 0 -or [int]$review.marketDataSnapshotCount -ne 1 -or [int]$review.marketDataRequestRejectCount -ne 0 -or [int]$review.businessMessageRejectCount -ne 0 -or [int]$review.rejectCount -ne 0) {
    throw "Phase 6X review must describe a one-snapshot empty book with zero rejects."
}
if ([bool]$review.orderSubmissionAttempted -or [bool]$review.shadowReplaySubmitAttempted -or [bool]$review.tradingMutationAttempted -or [bool]$review.schedulerStarted -or [bool]$review.credentialValuesReturned -or -not [bool]$review.noSensitiveContent) {
    throw "Phase 6X review contains unsafe flags."
}

$futureCommand = @"
DO NOT RUN FROM THIS SCRIPT. Future Phase 6Z operator-approved command only:
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-lmax-readonly-runtime-demo-gbpusd-snapshot-once.ps1 `
  -FinalReadinessFile artifacts/lmax-readonly-runtime-securityid-planning/final-readiness/lmax-readonly-gbpusd-manual-snapshot-final-readiness-20260509-165343.json `
  -AllowExternalConnections `
  -ConfirmDemoReadOnly `
  -Reason "Phase 6Z operator-approved market-hours GBPUSD read-only snapshot attempt"
"@

$artifact = [ordered]@{
    retryReadinessId = "phase6y-gbpusd-market-hours-retry-" + [Guid]::NewGuid().ToString("N")
    createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    requestedByOperatorId = $RequestedByOperatorId
    reason = $Reason
    symbol = "GBPUSD"
    slashSymbol = "GBP/USD"
    securityId = "4002"
    securityIdSource = "8"
    sourceFinalReadinessFile = $finalPath
    sourcePhase6XReviewFile = $reviewPath
    previousResultStatus = "CompletedWithEmptyBook"
    previousAttemptWasOutsideMarketHours = $true
    retryAllowedOnlyDuringMarketHours = $true
    retryIsManualOnly = $true
    retryAttemptCount = 1
    noScheduler = $true
    noPolling = $true
    noRuntimeShadowReplaySubmit = $true
    noOrderSubmission = $true
    noTradingMutation = $true
    apiWorkerGatewayMode = "FakeLmaxGateway"
    canRunAutomatically = $false
    noSensitiveContent = $true
    futureCommandTemplate = $futureCommand
    requiredFutureStep = "Phase 6Z operator-approved GBPUSD market-hours snapshot attempt"
    blockingReason = "Phase 6Y prepares the market-hours retry plan and does not run GBPUSD."
    decision = "PASS"
    externalConnectionAttempted = $false
    snapshotAttempted = $false
    replayAttempted = $false
    schedulerStarted = $false
    orderSubmissionAttempted = $false
    shadowReplaySubmitAttempted = $false
    tradingMutationAttempted = $false
}

$json = $artifact | ConvertTo-Json -Depth 12
if ($json -match '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=)') {
    throw "Generated retry readiness contains credential-shaped content."
}

if ($WhatIfPreview.IsPresent) {
    Write-Host $json
    Write-Host "WhatIfPreview only. No file written. No LMAX connection, snapshot, replay, scheduler, or credential read occurred."
    return
}

$outDir = Resolve-LocalPath $OutputDirectory
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$stamp = [DateTimeOffset]::UtcNow.ToString("yyyyMMdd-HHmmss")
$outPath = Join-Path $outDir "lmax-readonly-gbpusd-market-hours-retry-$stamp.json"
if ((Test-Path -LiteralPath $outPath) -and -not $Force.IsPresent) {
    throw "Output file already exists. Use -Force to overwrite: $outPath"
}

$json | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host "Phase 6Y GBPUSD market-hours retry readiness prepared."
Write-Host "Decision: PASS"
Write-Host "RetryReadinessFile: $outPath"
Write-Host "Future command template follows. DO NOT RUN FROM THIS SCRIPT."
Write-Host $futureCommand
Write-Host "No LMAX connection, snapshot, replay, scheduler, polling, order submission, shadow replay submit, credential read, or trading mutation occurred."
