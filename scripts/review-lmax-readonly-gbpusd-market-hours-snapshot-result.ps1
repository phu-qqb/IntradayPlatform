param(
    [Parameter(Mandatory=$true)]
    [string]$ArtifactFile
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=|raw\s*fix|sendercompid|targetcompid)'

function Resolve-LocalPath([string]$PathValue) {
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}

function Add-Check([string]$Name, [bool]$Pass, [string]$Detail) {
    $script:checks += [ordered]@{ name = $Name; status = if ($Pass) { "PASS" } else { "FAIL" }; detail = $Detail }
}

function Get-Counter($Artifact, [string]$Name) {
    $topLevelName = "${Name}Count"
    if ($null -ne $Artifact.PSObject.Properties[$topLevelName]) {
        $topLevelValue = $Artifact.PSObject.Properties[$topLevelName].Value
        if ($null -ne $topLevelValue) { return [int]$topLevelValue }
    }

    if ($null -eq $Artifact.diagnostics -or $null -eq $Artifact.diagnostics.messageCounters) { return 0 }
    $value = $Artifact.diagnostics.messageCounters.$Name
    if ($null -eq $value) { return 0 }
    return [int]$value
}

function Get-IssueCount($Value) {
    if ($null -eq $Value) { return 0 }
    if ($Value -is [array]) { return @($Value).Count }
    if ($Value -is [System.Collections.IEnumerable] -and $Value -isnot [string] -and $Value -isnot [pscustomobject]) {
        return @($Value).Count
    }
    if ($Value -is [pscustomobject]) {
        return @($Value.PSObject.Properties).Count
    }
    if ([string]::IsNullOrWhiteSpace([string]$Value)) { return 0 }
    return 1
}

function Get-SafeSensitiveScanText([string]$Text) {
    $safe = $Text
    foreach ($marker in @(
        "LMAX_DEMO_FIX_USERNAME",
        "LMAX_DEMO_FIX_PASSWORD",
        "LMAX_DEMO_SENDER_COMP_ID",
        "LMAX_DEMO_TARGET_COMP_ID",
        "usernamePresent",
        "passwordPresent",
        "senderCompIdPresent",
        "targetCompIdPresent",
        "usernameLength",
        "passwordLength",
        "senderCompIdLength",
        "targetCompIdLength",
        "sameSenderCompIdSourceLabel",
        "sameTargetCompIdSourceLabel",
        "senderCompIdMismatchSuspected",
        "targetCompIdMismatchSuspected"
    )) {
        $safe = $safe -replace [regex]::Escape($marker), "SANITIZED_METADATA_MARKER"
    }
    return $safe
}

Write-Host "LMAX Read-Only Phase 7C GBPUSD Market-Hours Snapshot Result Review"
Write-Host "Local-only review. This script does not connect to LMAX, request snapshots, replay, or use credentials."

$path = Resolve-LocalPath $ArtifactFile
if (-not (Test-Path -LiteralPath $path)) { throw "Artifact file not found: $path" }

$raw = Get-Content -LiteralPath $path -Raw
$artifact = $raw | ConvertFrom-Json
$checks = @()

$symbol = if ($artifact.symbol) { [string]$artifact.symbol } else { [string]$artifact.instrument }
$status = [string]$artifact.status
$snapshotReceived = [bool]$artifact.snapshotReceived
$entryCount = [int]$artifact.entryCount
$marketDataSnapshotCount = Get-Counter $artifact "marketDataSnapshot"
$marketDataRequestRejectCount = Get-Counter $artifact "marketDataRequestReject"
$businessRejectCount = Get-Counter $artifact "businessMessageReject"
$sessionRejectCount = Get-Counter $artifact "reject"
$errorCount = Get-IssueCount $artifact.errors
$completedWithBook = $status -in @("Completed", "CompletedWithWarnings")
$emptyBook = $status -eq "CompletedWithEmptyBook"
$failedSafe = $status.StartsWith("FailedSafe", [StringComparison]::OrdinalIgnoreCase) -or $status.StartsWith("Blocked", [StringComparison]::OrdinalIgnoreCase)

Add-Check "GbpusdOnly" ($symbol -eq "GBPUSD" -and [string]$artifact.slashSymbol -eq "GBP/USD") "Artifact must be GBPUSD / GBP/USD."
Add-Check "SecurityId4002" ([string]$artifact.securityId -eq "4002") "GBPUSD SecurityID must be 4002."
Add-Check "SecurityIdSource8" ([string]$artifact.securityIdSource -eq "8") "SecurityIDSource must be 8."
Add-Check "Profile" ([string]$artifact.environmentName -eq "Demo" -and [string]$artifact.venueProfileName -eq "DemoLondon" -and [string]$artifact.requestMode -eq "SnapshotPlusUpdates" -and [string]$artifact.symbolEncodingMode -eq "SecurityIdOnly" -and [int]$artifact.marketDepth -eq 1) "DemoLondon SnapshotPlusUpdates SecurityIdOnly MarketDepth=1 required."
Add-Check "KnownStatus" ($completedWithBook -or $emptyBook -or $failedSafe) "Status must be completed, completed empty-book, or failed-safe."
Add-Check "CompletedTopOfBook" (-not $completedWithBook -or ($snapshotReceived -and $null -ne $artifact.bestBid -and $null -ne $artifact.bestAsk -and $null -ne $artifact.mid -and $entryCount -gt 0)) "Completed result must include top-of-book."
Add-Check "CompletedRejectFree" (-not $completedWithBook -or ($marketDataSnapshotCount -eq 1 -and $marketDataRequestRejectCount -eq 0 -and $businessRejectCount -eq 0 -and $sessionRejectCount -eq 0 -and $errorCount -eq 0)) "Completed result must have one MarketDataSnapshot and no rejects/errors."
Add-Check "EmptyBookShape" (-not $emptyBook -or ([bool]$artifact.logonSucceeded -and [bool]$artifact.snapshotRequestAttempted -and $snapshotReceived -and $entryCount -eq 0 -and $null -eq $artifact.bestBid -and $null -eq $artifact.bestAsk -and $null -eq $artifact.mid)) "CompletedWithEmptyBook must have a received zero-entry snapshot with no top-of-book."
Add-Check "EmptyBookRejectFree" (-not $emptyBook -or ($marketDataSnapshotCount -eq 1 -and $marketDataRequestRejectCount -eq 0 -and $businessRejectCount -eq 0 -and $sessionRejectCount -eq 0 -and $errorCount -eq 0)) "CompletedWithEmptyBook must have one MarketDataSnapshot and no rejects/errors."
Add-Check "NoOrderSubmission" (-not [bool]$artifact.orderSubmissionAttempted) "orderSubmissionAttempted must be false."
Add-Check "NoShadowReplaySubmit" (-not [bool]$artifact.shadowReplaySubmitAttempted) "shadowReplaySubmitAttempted must be false."
Add-Check "NoTradingMutation" (-not [bool]$artifact.tradingMutationAttempted) "tradingMutationAttempted must be false."
Add-Check "NoScheduler" (-not [bool]$artifact.schedulerStarted) "schedulerStarted must be false."
Add-Check "NoCredentialValues" (-not [bool]$artifact.credentialValuesReturned) "credentialValuesReturned must be false."
Add-Check "Sanitized" ([bool]$artifact.noSensitiveContent -and [string]$artifact.redactionStatus -eq "Redacted") "Artifact must be sanitized and redacted."

$scanText = Get-SafeSensitiveScanText $raw
Add-Check "NoSensitiveText" (-not ($scanText -match $sensitivePattern)) "No credential-shaped values may appear."
Add-Check "NoOrderText" (-not ($raw -match '(?i)(NewOrderSingle|OrderCancelRequest|OrderCancelReplaceRequest|TradeCapture|OrderStatusRequest)')) "No order/trade/order-status message surface may appear."

$failed = @($checks | Where-Object status -eq "FAIL")
$closureClassification = if ($failed.Count -gt 0) { "UnsafeFail" } elseif ($completedWithBook) { "CompletedWithBook" } elseif ($emptyBook) { "CompletedWithEmptyBook" } elseif ($failedSafe) { "FailedSafe" } else { "UnsafeFail" }
$decision = if ($closureClassification -eq "UnsafeFail") { "FAIL" } elseif ($closureClassification -eq "CompletedWithBook") { "PASS" } else { "PASS_WITH_KNOWN_WARNINGS" }

$report = [ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7C"
    artifactFile = $path
    status = $status
    closureClassification = $closureClassification
    symbol = $symbol
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
    warnings = @($artifact.warnings)
    errors = @($artifact.errors)
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
$outPath = Join-Path $outDir "phase7c-gbpusd-market-hours-snapshot-review.json"
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8
$legacyOutPath = Join-Path $outDir "phase7c-gbpusd-snapshot-result-review.json"
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $legacyOutPath -Encoding UTF8

Write-Host ("Status: {0}" -f $status)
Write-Host ("ClosureClassification: {0}" -f $closureClassification)
Write-Host ("SnapshotReceived: {0}" -f $snapshotReceived)
Write-Host ("EntryCount: {0}" -f $entryCount)
Write-Host ("BestBid/BestAsk/Mid: {0} / {1} / {2}" -f $artifact.bestBid,$artifact.bestAsk,$artifact.mid)
Write-Host ("OrderSubmissionAttempted: {0}; ShadowReplaySubmitAttempted: {1}; TradingMutationAttempted: {2}; SchedulerStarted: {3}" -f [bool]$artifact.orderSubmissionAttempted,[bool]$artifact.shadowReplaySubmitAttempted,[bool]$artifact.tradingMutationAttempted,[bool]$artifact.schedulerStarted)
Write-Host ("Decision: {0}" -f $decision)
Write-Host ("Report: {0}" -f $outPath)

if ($decision -eq "FAIL") { exit 1 }
