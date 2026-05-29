param(
    [Parameter(Mandatory=$true)]
    [string]$ArtifactFile
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

function Resolve-LocalPath([string]$PathValue) {
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}

function Add-Check([string]$Name, [bool]$Pass, [string]$Detail) {
    $script:checks += [ordered]@{
        name = $Name
        status = if ($Pass) { "PASS" } else { "FAIL" }
        detail = $Detail
    }
}

function Get-Counter($Artifact, [string]$Name) {
    if ($null -eq $Artifact.diagnostics -or $null -eq $Artifact.diagnostics.messageCounters) { return 0 }
    $value = $Artifact.diagnostics.messageCounters.$Name
    if ($null -eq $value) { return 0 }
    return [int]$value
}

Write-Host "LMAX Read-Only Phase 6X GBPUSD Snapshot Result Review"
Write-Host "Local-only review. This script does not connect to LMAX, request snapshots, replay, or use credentials."

$path = Resolve-LocalPath $ArtifactFile
if (-not (Test-Path -LiteralPath $path)) {
    throw "Artifact file not found: $path"
}

$raw = Get-Content -LiteralPath $path -Raw
$artifact = $raw | ConvertFrom-Json
$checks = @()

$status = [string]$artifact.status
$snapshotReceived = [bool]$artifact.snapshotReceived
$entryCount = [int]$artifact.entryCount
$marketDataSnapshotCount = Get-Counter $artifact "marketDataSnapshot"
$marketDataRequestRejectCount = Get-Counter $artifact "marketDataRequestReject"
$businessRejectCount = Get-Counter $artifact "businessMessageReject"
$sessionRejectCount = Get-Counter $artifact "reject"
$emptyBook = $status -eq "CompletedWithEmptyBook"
$completedWithBook = $status -in @("Completed", "CompletedWithWarnings")
$failedSafe = $status.StartsWith("FailedSafe", [StringComparison]::OrdinalIgnoreCase) -or $status.StartsWith("Blocked", [StringComparison]::OrdinalIgnoreCase)

Add-Check "GbpusdOnly" (($artifact.symbol -eq "GBPUSD" -or $artifact.instrument -eq "GBPUSD") -and $artifact.slashSymbol -eq "GBP/USD") "Artifact must be GBPUSD / GBP/USD."
Add-Check "SecurityId4002" ([string]$artifact.securityId -eq "4002") "GBPUSD SecurityID must be 4002."
Add-Check "SecurityIdSource8" ([string]$artifact.securityIdSource -eq "8") "SecurityIDSource must be 8."
Add-Check "Profile" ($artifact.environmentName -eq "Demo" -and $artifact.venueProfileName -eq "DemoLondon" -and $artifact.requestMode -eq "SnapshotPlusUpdates" -and $artifact.symbolEncodingMode -eq "SecurityIdOnly" -and [int]$artifact.marketDepth -eq 1) "DemoLondon SnapshotPlusUpdates SecurityIdOnly MarketDepth=1 required."
Add-Check "KnownStatus" ($completedWithBook -or $emptyBook -or $failedSafe) "Status must be completed, completed empty-book, or failed-safe."
Add-Check "CompletedTopOfBook" (-not $completedWithBook -or ($snapshotReceived -and $null -ne $artifact.bestBid -and $null -ne $artifact.bestAsk -and $null -ne $artifact.mid -and $entryCount -gt 0)) "Completed result must include top-of-book."
Add-Check "EmptyBookShape" (-not $emptyBook -or ([bool]$artifact.logonSucceeded -and [bool]$artifact.snapshotRequestAttempted -and $snapshotReceived -and $entryCount -eq 0 -and $null -eq $artifact.bestBid -and $null -eq $artifact.bestAsk -and $null -eq $artifact.mid)) "CompletedWithEmptyBook must have a received zero-entry snapshot with no top-of-book."
Add-Check "EmptyBookRejectFree" (-not $emptyBook -or ($marketDataSnapshotCount -eq 1 -and $marketDataRequestRejectCount -eq 0 -and $businessRejectCount -eq 0 -and $sessionRejectCount -eq 0 -and @($artifact.errors).Count -eq 0)) "CompletedWithEmptyBook must have one MarketDataSnapshot and no rejects/errors."
Add-Check "NoOrderSubmission" (-not [bool]$artifact.orderSubmissionAttempted) "orderSubmissionAttempted must be false."
Add-Check "NoShadowReplaySubmit" (-not [bool]$artifact.shadowReplaySubmitAttempted) "shadowReplaySubmitAttempted must be false."
Add-Check "NoTradingMutation" (-not [bool]$artifact.tradingMutationAttempted) "tradingMutationAttempted must be false."
Add-Check "NoScheduler" (-not [bool]$artifact.schedulerStarted) "schedulerStarted must be false."
Add-Check "NoCredentialValues" (-not [bool]$artifact.credentialValuesReturned) "credentialValuesReturned must be false."
Add-Check "Sanitized" ([bool]$artifact.noSensitiveContent -and $artifact.redactionStatus -eq "Redacted") "Artifact must be sanitized and redacted."

$scanText = $raw -replace "LMAX_DEMO_FIX_PASSWORD","" -replace "passwordPresent","" -replace "passwordLength","" -replace "LMAX_DEMO_FIX_USERNAME",""
Add-Check "NoSensitiveText" (-not ($scanText -match '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=)')) "No credential-shaped values may appear."
Add-Check "NoOrderText" (-not ($raw -match '(?i)(NewOrderSingle|OrderCancelRequest|OrderCancelReplaceRequest|TradeCapture|OrderStatusRequest)')) "No order/trade/order-status message surface may appear."

$failed = @($checks | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } elseif ($emptyBook) { "PASS_WITH_KNOWN_WARNINGS" } else { "PASS" }
$warningClassification = if ($emptyBook) { "CompletedWithEmptyBook" } elseif ($failedSafe) { "FailedSafe" } else { "None" }

$report = [ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "6X"
    artifactFile = $path
    status = $status
    symbol = if ($artifact.symbol) { $artifact.symbol } else { $artifact.instrument }
    slashSymbol = $artifact.slashSymbol
    securityId = $artifact.securityId
    securityIdSource = $artifact.securityIdSource
    snapshotReceived = $snapshotReceived
    entryCount = $entryCount
    bestBid = $artifact.bestBid
    bestAsk = $artifact.bestAsk
    mid = $artifact.mid
    marketDataSnapshotCount = $marketDataSnapshotCount
    marketDataRequestRejectCount = $marketDataRequestRejectCount
    businessMessageRejectCount = $businessRejectCount
    rejectCount = $sessionRejectCount
    warningClassification = $warningClassification
    warnings = @($artifact.warnings)
    orderSubmissionAttempted = [bool]$artifact.orderSubmissionAttempted
    shadowReplaySubmitAttempted = [bool]$artifact.shadowReplaySubmitAttempted
    tradingMutationAttempted = [bool]$artifact.tradingMutationAttempted
    schedulerStarted = [bool]$artifact.schedulerStarted
    credentialValuesReturned = [bool]$artifact.credentialValuesReturned
    noSensitiveContent = [bool]$artifact.noSensitiveContent
    redactionStatus = $artifact.redactionStatus
    finalDecision = $decision
    checks = $checks
}

$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase6x-gbpusd-snapshot-result-review.json"
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ("Status: {0}" -f $status)
Write-Host ("SnapshotReceived: {0}" -f $snapshotReceived)
Write-Host ("EntryCount: {0}" -f $entryCount)
Write-Host ("BestBid/BestAsk/Mid: {0} / {1} / {2}" -f $artifact.bestBid,$artifact.bestAsk,$artifact.mid)
Write-Host ("WarningClassification: {0}" -f $warningClassification)
Write-Host ("OrderSubmissionAttempted: {0}; ShadowReplaySubmitAttempted: {1}; TradingMutationAttempted: {2}; SchedulerStarted: {3}" -f [bool]$artifact.orderSubmissionAttempted,[bool]$artifact.shadowReplaySubmitAttempted,[bool]$artifact.tradingMutationAttempted,[bool]$artifact.schedulerStarted)
Write-Host ("Decision: {0}" -f $decision)
Write-Host ("Report: {0}" -f $outPath)

if ($decision -eq "FAIL") { exit 1 }
