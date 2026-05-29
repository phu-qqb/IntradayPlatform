param(
    [string]$SignoffFile
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()

function Add-Result([string]$Category, [string]$Check, [string]$Status, [string]$Detail) {
    $script:results += [ordered]@{ category = $Category; check = $Check; status = $Status; detail = $Detail }
    Write-Host ("{0}: {1} / {2} - {3}" -f $Status, $Category, $Check, $Detail)
}

function Resolve-LocalPath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return $Path }
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return Join-Path $repoRoot $Path
}

Write-Host "LMAX Read-Only Runtime Phase 5X Operator Summary Gate"
Write-Host "Local-only. No external LMAX connection, no credentials, no scheduler/polling, no runtime replay, and no mutation."

$modelFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyMarketDataWorkflowStatusSummary.cs"
$scriptFile = Join-Path $repoRoot "scripts/show-lmax-readonly-marketdata-workflow-status.ps1"
$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$appFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Ui/src/App.tsx"
$typesFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Ui/src/api/types.ts"
$clientFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Ui/src/api/apiClient.ts"
$prototypeFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlySocketPrototype.cs"
$workerProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"

if ((Test-Path -LiteralPath $modelFile) -and (Select-String -Path $modelFile -Pattern "LmaxReadOnlyMarketDataWorkflowStatusSummaryValidator" -SimpleMatch -Quiet)) {
    Add-Result "Files" "Summary model and validator exist" "PASS" "Phase 5X status summary model and validator are present."
} else {
    Add-Result "Files" "Summary model and validator exist" "FAIL" "Missing Phase 5X status summary model or validator."
}

if (Test-Path -LiteralPath $scriptFile) {
    Add-Result "Files" "Summary script exists" "PASS" "Status script is present."
} else {
    Add-Result "Files" "Summary script exists" "FAIL" "Missing status script."
}

if (Select-String -Path $apiProgram -Pattern "/lmax-readonly-runtime/marketdata-workflow/status" -SimpleMatch -Quiet) {
    Add-Result "API" "Read-only status endpoint exists" "PASS" "Endpoint route is present."
} else {
    Add-Result "API" "Read-only status endpoint exists" "FAIL" "Endpoint route is missing."
}

if ((Select-String -Path $appFile -Pattern "LMAX Read-Only Demo MarketData Workflow" -SimpleMatch -Quiet) -and (Select-String -Path $clientFile -Pattern "getLmaxReadOnlyMarketDataWorkflowStatus" -SimpleMatch -Quiet) -and (Select-String -Path $typesFile -Pattern "LmaxReadOnlyMarketDataWorkflowStatusSummaryDto" -SimpleMatch -Quiet)) {
    Add-Result "UI" "Operator status panel exists" "PASS" "Read-only UI panel and client types are present."
} else {
    Add-Result "UI" "Operator status panel exists" "FAIL" "UI panel or client type wiring is missing."
}

$statusArgs = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $scriptFile)
if (-not [string]::IsNullOrWhiteSpace($SignoffFile)) {
    $statusArgs += @("-SignoffFile", $SignoffFile)
}
& powershell @statusArgs | Out-Host
if ($LASTEXITCODE -eq 0) {
    Add-Result "Status" "Signoff status can be read" "PASS" "Status summary script completed."
} else {
    Add-Result "Status" "Signoff status can be read" "FAIL" "Status summary script failed."
}

$summaryFile = Get-ChildItem -Path (Join-Path $repoRoot "artifacts/readiness") -Filter "lmax-readonly-marketdata-workflow-status-*.json" |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
if ($summaryFile) {
    $summary = Get-Content -Raw -LiteralPath $summaryFile.FullName | ConvertFrom-Json
    if ([string]$summary.operationalStatus -in @("FrozenManualReadOnly", "Pass", "PassWithWarnings", "NotAvailable") -and -not [bool]$summary.runtimeShadowReplaySubmit -and -not [bool]$summary.credentialValuesReturned) {
        Add-Result "Status" "Summary safety" "PASS" "OperationalStatus=$($summary.operationalStatus); runtimeShadowReplaySubmit=false; credentialValuesReturned=false."
    } else {
        Add-Result "Status" "Summary safety" "FAIL" "Summary status or safety flags are unsafe."
    }
}

$runtimeSubmitHits = @(Select-String -Path $prototypeFile,$modelFile -Pattern "/lmax-shadow/replay","SubmitToShadowReplayAsync","ILmaxShadowReplayService","ReplayAsync" -SimpleMatch -ErrorAction SilentlyContinue)
if ($runtimeSubmitHits.Count -eq 0) {
    Add-Result "Safety" "Runtime still does not submit to shadow replay" "PASS" "Runtime/prototype/status files have no shadow replay submit path."
} else {
    Add-Result "Safety" "Runtime still does not submit to shadow replay" "FAIL" (($runtimeSubmitHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$scriptText = Get-Content -Raw -LiteralPath $scriptFile
if ($scriptText -notmatch "while\s*\(" -and $scriptText -notmatch "Register-ScheduledTask|New-ScheduledTask|Start-Job|Start-ThreadJob|Timer|HostedService|Start-Sleep") {
    Add-Result "Safety" "No scheduler or automatic polling" "PASS" "No scheduler, background job, timer, hosted service, sleep loop, or polling marker found."
} else {
    Add-Result "Safety" "No scheduler or automatic polling" "FAIL" "Scheduler, background job, timer, hosted-service, or polling marker found."
}

$orderHits = @(Select-String -Path $prototypeFile,$scriptFile -Pattern "NewOrderSingle","OrderCancelRequest","OrderCancelReplaceRequest","SubmitOrder","SendOrder","OrderStatusRequest","TradeCapture" -SimpleMatch -ErrorAction SilentlyContinue)
if ($orderHits.Count -eq 0) {
    Add-Result "Safety" "No order command surface" "PASS" "No order command surface found in Phase 5X runtime/status files."
} else {
    Add-Result "Safety" "No order command surface" "FAIL" (($orderHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$mutationHits = @(Select-String -Path $prototypeFile,$modelFile -Pattern "IOrderRepository","IFillRepository","PositionRepository","ModelRun","RiskState","Wallet","SubmitToShadowReplayAsync" -SimpleMatch -ErrorAction SilentlyContinue)
if ($mutationHits.Count -eq 0) {
    Add-Result "Safety" "No trading mutation dependency" "PASS" "No trading-state repository or runtime mutation dependency found."
} else {
    Add-Result "Safety" "No trading mutation dependency" "FAIL" (($mutationHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$registrationHits = @(Select-String -Path $apiProgram,$workerProgram -Pattern "LmaxReadOnlySocketPrototype","RealLmaxGateway","LmaxVenueGatewaySkeleton","ExternalReadOnlyPrototypeGateway" -SimpleMatch -ErrorAction SilentlyContinue)
if ($registrationHits.Count -eq 0 -and (Select-String -Path $apiProgram -Pattern "FakeLmaxGateway" -SimpleMatch -Quiet)) {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No prototype or real gateway registration found."
} else {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" (($registrationHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

Add-Result "Runtime" "External socket attempts" "PASS" "No external socket attempt is made by this gate."
Add-Result "Replay" "Manual replay" "PASS" "No manual replay is performed by this gate; replay remains explicit local API only."

$failed = @($results | Where-Object { $_.status -eq "FAIL" })
$warnings = @($results | Where-Object { $_.status -eq "WARN" })
$decision = if ($failed.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS_WITH_KNOWN_WARNINGS" } else { "PASS" }
$reportDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$reportPath = Join-Path $reportDir "phase5x-operator-summary-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    finalDecision = $decision
    signoffFile = if ($SignoffFile) { Resolve-LocalPath $SignoffFile } else { $null }
    externalConnectionAttempted = $false
    manualReplayPerformed = $false
    runtimeShadowReplaySubmit = $false
    results = $results
} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $reportPath"
if ($decision -eq "FAIL") { exit 1 }
