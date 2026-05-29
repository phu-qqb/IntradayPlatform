param(
    [string]$RecordsDirectory = "artifacts/lmax-readonly-runtime-securityid-confirmations"
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

Write-Host "LMAX Read-Only Runtime Phase 6G Record Entry Workflow Gate"
Write-Host "Planning-only. No LMAX connection, no external APIs, no snapshots, no replay, no credentials, no scheduler/polling, no orders, and no mutation."

$createScript = Join-Path $PSScriptRoot "new-lmax-readonly-securityid-confirmation-record.ps1"
$templateScript = Join-Path $PSScriptRoot "new-lmax-readonly-securityid-confirmation-record-template.ps1"
$reviewScript = Join-Path $PSScriptRoot "review-lmax-readonly-securityid-confirmation-records.ps1"
$modelFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyInstrumentSecurityIdConfirmationRecord.cs"

foreach ($item in @(
    @{ Name = "Creation script"; Path = $createScript },
    @{ Name = "Template script"; Path = $templateScript },
    @{ Name = "Review script"; Path = $reviewScript },
    @{ Name = "Confirmation record model"; Path = $modelFile }
)) {
    if (Test-Path -LiteralPath $item.Path) {
        Add-Result "Files" "$($item.Name) exists" "PASS" $item.Path
    } else {
        Add-Result "Files" "$($item.Name) exists" "FAIL" "Missing: $($item.Path)"
    }
}

$createText = if (Test-Path -LiteralPath $createScript) { Get-Content -Raw -LiteralPath $createScript } else { "" }
foreach ($marker in @("WhatIfPreview", "-Force", "PlaceholderSecurityIdNotAccepted", "SensitiveContentDetected", "TradingAuthorizationImplied", "isApprovedForExternalRun = `$false")) {
    if ($createText.Contains($marker)) {
        Add-Result "Creation" "Creation script marker $marker" "PASS" "Safe behavior marker found."
    } else {
        Add-Result "Creation" "Creation script marker $marker" "FAIL" "Safe behavior marker missing."
    }
}

$templateOut = Join-Path $repoRoot "artifacts/lmax-readonly-runtime-securityid-confirmations/templates"
if (Test-Path -LiteralPath $templateScript) {
    powershell -NoProfile -ExecutionPolicy Bypass -File $templateScript -Symbol All -OutputDirectory $templateOut -Force
}

$templateFiles = @(Get-ChildItem -LiteralPath $templateOut -Filter "*-template.json" -ErrorAction SilentlyContinue)
if ($templateFiles.Count -eq 4) {
    Add-Result "Templates" "Per-symbol templates generated" "PASS" "Generated 4 templates under ignored artifacts."
} else {
    Add-Result "Templates" "Per-symbol templates generated" "FAIL" "Expected 4 templates, found $($templateFiles.Count)."
}

$templateUnsafe = @()
foreach ($file in $templateFiles) {
    $text = Get-Content -Raw -LiteralPath $file.FullName
    if ($text -match '"isApprovedForExternalRun"\s*:\s*true' -or
        $text -match '(?i)(password|secret|token|apikey|privatekey|\b553=|\b554=)') {
        $templateUnsafe += $file.FullName
    }
}
if ($templateUnsafe.Count -eq 0) {
    Add-Result "Templates" "Templates are sanitized and non-executable" "PASS" "No secret-shaped values or external-run approval found."
} else {
    Add-Result "Templates" "Templates are sanitized and non-executable" "FAIL" ($templateUnsafe -join "; ")
}

$resolvedRecordsDir = Resolve-LocalPath $RecordsDirectory
powershell -NoProfile -ExecutionPolicy Bypass -File $reviewScript -RecordsDirectory $resolvedRecordsDir
$reviewReport = Join-Path $repoRoot "artifacts/readiness/phase6f-securityid-confirmation-records-review.json"
$review = Get-Content -Raw -LiteralPath $reviewReport | ConvertFrom-Json
if ($review.finalDecision -eq "FAIL") {
    Add-Result "Review" "Current confirmation records review" "FAIL" "Review report failed."
} elseif ($review.finalDecision -eq "PASS_WITH_KNOWN_WARNINGS") {
    Add-Result "Review" "Current confirmation records review" "WARN" "Records are missing/pending but boundary remains safe."
} else {
    Add-Result "Review" "Current confirmation records review" "PASS" "All records accepted for planning."
}

$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"
$registrationHits = @(Select-String -Path $apiProgram,$workerProgram -Pattern "RealLmaxGateway","ExternalReadOnlyPrototypeGateway","LmaxVenueGatewaySkeleton" -SimpleMatch -ErrorAction SilentlyContinue)
if ($registrationHits.Count -eq 0 -and (Select-String -Path $apiProgram -Pattern "FakeLmaxGateway" -SimpleMatch -Quiet)) {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No real gateway registration found."
} else {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" (($registrationHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

Add-Result "Runtime" "External LMAX connection" "PASS" "No external connection is made by this record-entry workflow gate."
Add-Result "API" "External API calls" "PASS" "No external API calls are made by this record-entry workflow gate."
Add-Result "Snapshot" "MarketData snapshot" "PASS" "No market-data snapshot is run by this record-entry workflow gate."
Add-Result "Replay" "Shadow replay" "PASS" "No replay is submitted by this record-entry workflow gate."
Add-Result "Scheduler" "Scheduler/polling" "PASS" "No scheduler or polling is added by this record-entry workflow gate."
Add-Result "Orders" "Order submission" "PASS" "No order submission is added by this record-entry workflow gate."
Add-Result "Mutation" "Trading state" "PASS" "No trading state is mutated by this record-entry workflow gate."

$failed = @($results | Where-Object { $_.status -eq "FAIL" })
$warnings = @($results | Where-Object { $_.status -eq "WARN" })
$decision = if ($failed.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS_WITH_KNOWN_WARNINGS" } else { "PASS" }

$reportDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$reportPath = Join-Path $reportDir "phase6g-record-entry-workflow-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    finalDecision = $decision
    phase = "6G"
    scope = "Manual SecurityID Record Entry Workflow Hardening, No External Run"
    templateDirectory = $templateOut
    recordsDirectory = $resolvedRecordsDir
    reviewDecision = $review.finalDecision
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
