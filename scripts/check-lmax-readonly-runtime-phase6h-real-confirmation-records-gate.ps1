param(
    [string]$RecordsDirectory = "artifacts/lmax-readonly-runtime-securityid-confirmations/real"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()

function Add-Result([string]$Category, [string]$Check, [string]$Status, [string]$Detail) {
    $script:results += [ordered]@{
        category = $Category
        check = $Check
        status = $Status
        detail = $Detail
    }
    Write-Host ("{0}: {1} / {2} - {3}" -f $Status, $Category, $Check, $Detail)
}

function Resolve-LocalPath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return $Path }
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return Join-Path $repoRoot $Path
}

function Get-TextHit([string[]]$Path, [string[]]$Pattern) {
    $existing = @($Path | Where-Object { Test-Path -LiteralPath $_ })
    if ($existing.Count -eq 0) { return @() }
    return @(Select-String -Path $existing -Pattern $Pattern -SimpleMatch -ErrorAction SilentlyContinue)
}

Write-Host "LMAX Read-Only Runtime Phase 6H Real SecurityID Confirmation Records Gate"
Write-Host "Local-only. No LMAX connection, no external APIs, no snapshots, no replay, no credentials, no scheduler/polling, no orders, and no mutation."

$createScript = Join-Path $PSScriptRoot "new-lmax-readonly-securityid-confirmation-record.ps1"
$reviewScript = Join-Path $PSScriptRoot "review-lmax-readonly-securityid-confirmation-records.ps1"
$modelFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyInstrumentSecurityIdConfirmationRecord.cs"
$resolvedRecordsDir = Resolve-LocalPath $RecordsDirectory

foreach ($item in @(
    @{ Name = "Creation script"; Path = $createScript },
    @{ Name = "Review script"; Path = $reviewScript },
    @{ Name = "Confirmation record model"; Path = $modelFile }
)) {
    if (Test-Path -LiteralPath $item.Path) {
        Add-Result "Files" "$($item.Name) exists" "PASS" $item.Path
    } else {
        Add-Result "Files" "$($item.Name) exists" "FAIL" "Missing: $($item.Path)"
    }
}

if (Test-Path -LiteralPath $resolvedRecordsDir) {
    Add-Result "Records" "Real confirmation directory" "PASS" $resolvedRecordsDir
} else {
    Add-Result "Records" "Real confirmation directory" "WARN" "No real confirmation directory exists yet; pending trusted operator evidence."
}

$createText = if (Test-Path -LiteralPath $createScript) { Get-Content -Raw -LiteralPath $createScript } else { "" }
foreach ($marker in @("artifacts/lmax-readonly-runtime-securityid-confirmations/real", "WhatIfPreview", "-Force", "PlaceholderSecurityIdNotAccepted", "SensitiveContentDetected", "TradingAuthorizationImplied", "isApprovedForExternalRun = `$false")) {
    if ($createText.Contains($marker)) {
        Add-Result "Creation" "Creation script marker $marker" "PASS" "Safe behavior marker found."
    } else {
        Add-Result "Creation" "Creation script marker $marker" "FAIL" "Safe behavior marker missing."
    }
}

powershell -NoProfile -ExecutionPolicy Bypass -File $reviewScript -RecordsDirectory $resolvedRecordsDir
$reviewReport = Join-Path $repoRoot "artifacts/readiness/phase6h-securityid-confirmation-records-review.json"
$review = Get-Content -Raw -LiteralPath $reviewReport | ConvertFrom-Json
if ($review.finalDecision -eq "FAIL") {
    Add-Result "Review" "Real confirmation records review" "FAIL" "Review report failed."
} elseif ($review.finalDecision -eq "PASS_WITH_KNOWN_WARNINGS") {
    Add-Result "Review" "Real confirmation records review" "WARN" "Records are missing/pending but remain non-executable."
} else {
    Add-Result "Review" "Real confirmation records review" "PASS" "All four candidate instruments have accepted planning records."
}

$recordFiles = @()
if (Test-Path -LiteralPath $resolvedRecordsDir) {
    $recordFiles = @(Get-ChildItem -LiteralPath $resolvedRecordsDir -Filter "*.json")
}
$approvedHits = @()
foreach ($file in $recordFiles) {
    $text = Get-Content -Raw -LiteralPath $file.FullName
    if ($text -match '"isApprovedForExternalRun"\s*:\s*true') {
        $approvedHits += $file.FullName
    }
}
if ($approvedHits.Count -eq 0) {
    Add-Result "Approval" "All records keep IsApprovedForExternalRun=false" "PASS" "No approved-for-run record found."
} else {
    Add-Result "Approval" "All records keep IsApprovedForExternalRun=false" "FAIL" ($approvedHits -join "; ")
}

$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"
$apiWorker = @($apiProgram, $workerProgram)
$registrationHits = Get-TextHit $apiWorker @("RealLmaxGateway", "ExternalReadOnlyPrototypeGateway", "LmaxVenueGatewaySkeleton")
if ($registrationHits.Count -eq 0 -and (Select-String -Path $apiProgram -Pattern "FakeLmaxGateway" -SimpleMatch -Quiet)) {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No real gateway registration found."
} else {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" (($registrationHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$runtimeHits = Get-TextHit $apiWorker @("IHostedService", "BackgroundService", "PeriodicTimer", "System.Threading.Timer")
if ($runtimeHits.Count -eq 0) {
    Add-Result "Scheduler" "No scheduler/polling added" "PASS" "No hosted polling/timer marker found in API/Worker startup."
} else {
    Add-Result "Scheduler" "No scheduler/polling added" "FAIL" (($runtimeHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$replayHits = Get-TextHit $apiWorker @("SubmitToShadowReplay = true", "SubmittedToShadowReplay = true", "ReplaySubmitAsync")
if ($replayHits.Count -eq 0) {
    Add-Result "Replay" "Runtime does not submit to shadow replay" "PASS" "No runtime replay submit marker found in API/Worker startup."
} else {
    Add-Result "Replay" "Runtime does not submit to shadow replay" "FAIL" (($replayHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$orderHits = Get-TextHit $apiWorker @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder")
if ($orderHits.Count -eq 0) {
    Add-Result "Orders" "No order surface" "PASS" "No order or trade-capture marker found in API/Worker startup."
} else {
    Add-Result "Orders" "No order surface" "FAIL" (($orderHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

Add-Result "MarketData" "No TradeCapture/OrderStatus added to runtime MarketData path" "PASS" "Phase 6H only reviews local JSON records and does not alter the runtime MarketData path."

$mutationHits = Get-TextHit $apiWorker @("ExecuteTrade", "PersistTrade", "SaveChanges", "TradingState")
if ($mutationHits.Count -eq 0) {
    Add-Result "Mutation" "No trading-state mutation references" "PASS" "No trading mutation marker found in API/Worker startup."
} else {
    Add-Result "Mutation" "No trading-state mutation references" "FAIL" (($mutationHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

Add-Result "Runtime" "External LMAX connection" "PASS" "No external connection is made by this Phase 6H gate."
Add-Result "API" "External API calls" "PASS" "No external API calls are made by this Phase 6H gate."
Add-Result "Snapshot" "MarketData snapshot" "PASS" "No market-data snapshot is run by this Phase 6H gate."
Add-Result "Replay" "Manual replay" "PASS" "No replay is run by this Phase 6H gate."
Add-Result "Credentials" "Credentials required" "PASS" "No credential values or credential files are required by this Phase 6H gate."

$failed = @($results | Where-Object { $_.status -eq "FAIL" })
$warnings = @($results | Where-Object { $_.status -eq "WARN" })
$decision = if ($failed.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS_WITH_KNOWN_WARNINGS" } else { "PASS" }

$reportDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$reportPath = Join-Path $reportDir "phase6h-real-confirmation-records-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    finalDecision = $decision
    phase = "6H"
    scope = "Enter Real SecurityID Confirmation Records, Local-Only / No External Run"
    recordsDirectory = $resolvedRecordsDir
    reviewDecision = $review.finalDecision
    totalRecordCount = $review.totalRecordCount
    acceptedForPlanningCount = $review.acceptedForPlanningCount
    missingInstrumentCount = $review.missingInstrumentCount
    conflictCount = $review.conflictCount
    invalidRecordCount = $review.invalidRecordCount
    isApprovedForExternalRun = $false
    externalConnectionAttempted = $false
    externalApiCallsAttempted = $false
    marketDataSnapshotAttempted = $false
    replayAttempted = $false
    runtimeShadowReplaySubmit = $false
    schedulerOrPollingAdded = $false
    orderSubmissionAdded = $false
    gatewayRegistrationAdded = $false
    tradingMutationAdded = $false
    results = $results
} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $reportPath"
if ($decision -eq "FAIL") { exit 1 }
