param(
    [string]$ModelFile = "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyInstrumentSecurityIdConfirmationRecord.cs",
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

Write-Host "LMAX Read-Only Runtime Phase 6F SecurityID Confirmation Records Gate"
Write-Host "Planning-only. No LMAX connection, no external APIs, no snapshots, no replay, no credentials, no scheduler/polling, no orders, and no mutation."

$resolvedModel = Resolve-LocalPath $ModelFile
$resolvedRecordsDir = Resolve-LocalPath $RecordsDirectory

if (Test-Path -LiteralPath $resolvedModel) {
    Add-Result "Files" "Confirmation record model exists" "PASS" $resolvedModel
} else {
    Add-Result "Files" "Confirmation record model exists" "FAIL" "Missing file: $resolvedModel"
}

$modelText = if (Test-Path -LiteralPath $resolvedModel) { Get-Content -Raw -LiteralPath $resolvedModel } else { "" }
foreach ($type in @("LmaxReadOnlyInstrumentSecurityIdConfirmationRecord", "LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidator", "ReviewRecords")) {
    if ($modelText.Contains($type)) {
        Add-Result "Model" "$type exists" "PASS" "Type/member present."
    } else {
        Add-Result "Model" "$type exists" "FAIL" "Missing type/member."
    }
}

foreach ($script in @("new-lmax-readonly-securityid-confirmation-record.ps1", "new-lmax-readonly-securityid-confirmation-record-template.ps1", "review-lmax-readonly-securityid-confirmation-records.ps1")) {
    $scriptPath = Join-Path $PSScriptRoot $script
    if (Test-Path -LiteralPath $scriptPath) {
        Add-Result "Scripts" "$script exists" "PASS" $scriptPath
    } else {
        Add-Result "Scripts" "$script exists" "FAIL" "Missing script: $scriptPath"
    }
}

$records = @()
if (Test-Path -LiteralPath $resolvedRecordsDir) {
    $records = @(Get-ChildItem -LiteralPath $resolvedRecordsDir -Filter "*.json")
    Add-Result "Records" "Confirmation records directory exists" "PASS" "$($records.Count) record file(s) found."
} else {
    Add-Result "Records" "Confirmation records directory exists" "WARN" "No confirmation records directory yet; this is acceptable before manual capture."
}

if ($records.Count -eq 0) {
    Add-Result "Records" "Accepted confirmation records" "WARN" "No confirmation records have been captured yet."
} else {
    powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "review-lmax-readonly-securityid-confirmation-records.ps1") -RecordsDirectory $RecordsDirectory
    $reviewReport = Join-Path $repoRoot "artifacts/readiness/phase6f-securityid-confirmation-records-review.json"
    $review = Get-Content -Raw -LiteralPath $reviewReport | ConvertFrom-Json
    if ($review.finalDecision -eq "FAIL") {
        Add-Result "Records" "Confirmation records review" "FAIL" "Review report failed."
    } elseif ($review.finalDecision -eq "PASS_WITH_KNOWN_WARNINGS") {
        Add-Result "Records" "Confirmation records review" "WARN" "Review report has known warnings."
    } else {
        Add-Result "Records" "Confirmation records review" "PASS" "Review report passed."
    }
}

if ($modelText.Contains("IsApprovedForExternalRun: false") -or $modelText.Contains("IsApprovedForExternalRun=false")) {
    Add-Result "Approval" "External run approval remains false" "PASS" "Model/scripts force confirmation records to remain non-executable."
} else {
    Add-Result "Approval" "External run approval remains false" "FAIL" "Missing explicit false external-run approval marker."
}

$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"
$registrationHits = @(Select-String -Path $apiProgram,$workerProgram -Pattern "RealLmaxGateway","ExternalReadOnlyPrototypeGateway","LmaxVenueGatewaySkeleton" -SimpleMatch -ErrorAction SilentlyContinue)
if ($registrationHits.Count -eq 0 -and (Select-String -Path $apiProgram -Pattern "FakeLmaxGateway" -SimpleMatch -Quiet)) {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No real gateway registration found."
} else {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" (($registrationHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

Add-Result "Runtime" "External LMAX connection" "PASS" "No external connection is made by this confirmation records gate."
Add-Result "API" "External API calls" "PASS" "No external API calls are made by this confirmation records gate."
Add-Result "Snapshot" "MarketData snapshot" "PASS" "No market-data snapshot is run by this confirmation records gate."
Add-Result "Replay" "Shadow replay" "PASS" "No replay is submitted by this confirmation records gate."
Add-Result "Scheduler" "Scheduler/polling" "PASS" "No scheduler or polling is added by this confirmation records gate."
Add-Result "Orders" "Order submission" "PASS" "No order submission is added by this confirmation records gate."
Add-Result "Mutation" "Trading state" "PASS" "No trading state is mutated by this confirmation records gate."

$failed = @($results | Where-Object { $_.status -eq "FAIL" })
$warnings = @($results | Where-Object { $_.status -eq "WARN" })
$decision = if ($failed.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS_WITH_KNOWN_WARNINGS" } else { "PASS" }

$reportDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$reportPath = Join-Path $reportDir "phase6f-confirmation-records-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    finalDecision = $decision
    phase = "6F"
    scope = "Manual SecurityID Evidence Capture / Operator Confirmation Records, No External Run"
    modelFile = $resolvedModel
    recordsDirectory = $resolvedRecordsDir
    recordCount = $records.Count
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
