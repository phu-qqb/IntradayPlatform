param(
    [string]$SignoffFile,
    [string]$AuditPackFile
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

function Test-Contains([string]$Path, [string]$Pattern) {
    return (Test-Path -LiteralPath $Path) -and [bool](Select-String -Path $Path -Pattern $Pattern -SimpleMatch -Quiet)
}

Write-Host "LMAX Read-Only Runtime Phase 5W Operational Signoff Gate"
Write-Host "Local-only. No external LMAX connection, no credentials, no runtime snapshot run, and no replay execution."

if ([string]::IsNullOrWhiteSpace($SignoffFile)) {
    if ([string]::IsNullOrWhiteSpace($AuditPackFile)) {
        throw "Provide -SignoffFile or -AuditPackFile."
    }
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repoRoot "scripts/signoff-lmax-readonly-marketdata-workflow.ps1") -AuditPackFile $AuditPackFile | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Operational signoff generation failed." }
    $SignoffFile = (Get-ChildItem -Path (Join-Path $repoRoot "artifacts/readiness") -Filter "lmax-readonly-marketdata-operational-signoff-*.json" | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
}

$signoffPath = Resolve-LocalPath $SignoffFile
if (-not (Test-Path -LiteralPath $signoffPath)) { throw "Missing signoff file: $signoffPath" }
$signoff = Get-Content -Raw -LiteralPath $signoffPath | ConvertFrom-Json

if ([string]$signoff.finalDecision -eq "PASS" -and [string]$signoff.auditPackFinalDecision -eq "PASS") {
    Add-Result "Signoff" "Decision" "PASS" "Signoff and audit pack decisions are PASS."
} else {
    Add-Result "Signoff" "Decision" "FAIL" "Expected signoff and audit pack decisions to be PASS."
}

if ([int]$signoff.artifactCount -gt 0 -and [int]$signoff.evidencePreviewCount -eq [int]$signoff.artifactCount -and [int]$signoff.manualReplayCount -eq [int]$signoff.evidencePreviewCount -and [int]$signoff.totalObservationCount -eq 0) {
    Add-Result "Signoff" "Counts" "PASS" "ArtifactCount=$($signoff.artifactCount) EvidencePreviewCount=$($signoff.evidencePreviewCount) ManualReplayCount=$($signoff.manualReplayCount) TotalObservationCount=0."
} else {
    Add-Result "Signoff" "Counts" "FAIL" "Artifact, preview, replay, and observation counts are unsafe."
}

if (-not [bool]$signoff.runtimeShadowReplaySubmit -and -not [bool]$signoff.externalConnectionAttempted -and -not [bool]$signoff.orderSubmissionAttempted -and -not [bool]$signoff.tradingMutationAttempted -and -not [bool]$signoff.schedulerStarted -and -not [bool]$signoff.credentialValuesReturned -and [bool]$signoff.noSensitiveContent) {
    Add-Result "Signoff" "Safety flags" "PASS" "No runtime submit, external connection, order, scheduler, mutation, or credential-value return."
} else {
    Add-Result "Signoff" "Safety flags" "FAIL" "One or more signoff safety flags are unsafe."
}

$signoffText = Get-Content -Raw -LiteralPath $signoffPath
if ($signoffText -notmatch "(?i)554\s*=" -and $signoffText -notmatch "(?i)password\s*[:=]\s*(?!\\[REDACTED\\])\\S+" -and $signoffText -notmatch "(?i)rawFix") {
    Add-Result "Signoff" "No sensitive content" "PASS" "No raw FIX, password tag, or credential-shaped values found."
} else {
    Add-Result "Signoff" "No sensitive content" "FAIL" "Sensitive content pattern found in signoff."
}

$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"
$prototypeFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlySocketPrototype.cs"
$signoffCode = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyMarketDataOperationalSignoff.cs"
$signoffScript = Join-Path $repoRoot "scripts/signoff-lmax-readonly-marketdata-workflow.ps1"

$runtimeSubmitHits = @(Select-String -Path $prototypeFile,$signoffCode -Pattern "/lmax-shadow/replay","SubmitToShadowReplayAsync","ILmaxShadowReplayService","ReplayAsync" -SimpleMatch -ErrorAction SilentlyContinue)
if ($runtimeSubmitHits.Count -eq 0) {
    Add-Result "Safety" "Runtime still does not submit to shadow replay" "PASS" "Runtime/prototype/signoff files have no shadow replay submit path."
} else {
    Add-Result "Safety" "Runtime still does not submit to shadow replay" "FAIL" (($runtimeSubmitHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$scriptText = if (Test-Path -LiteralPath $signoffScript) { Get-Content -LiteralPath $signoffScript -Raw } else { "" }
if ($scriptText -notmatch "while\s*\(" -and $scriptText -notmatch "Register-ScheduledTask|New-ScheduledTask|Start-Job|Start-ThreadJob|Timer|HostedService|Start-Sleep") {
    Add-Result "Safety" "No scheduler or automatic polling" "PASS" "No scheduler, background job, timer, hosted service, sleep loop, or polling marker found."
} else {
    Add-Result "Safety" "No scheduler or automatic polling" "FAIL" "Scheduler, background job, timer, sleep, hosted-service, or polling marker found."
}

$orderHits = @(Select-String -Path $prototypeFile -Pattern "NewOrderSingle","OrderCancelRequest","OrderCancelReplaceRequest","SubmitOrder","SendOrder","OrderStatusRequest" -SimpleMatch -ErrorAction SilentlyContinue)
if ($orderHits.Count -eq 0) {
    Add-Result "Safety" "No order command surface" "PASS" "No order command surface found in prototype runtime file."
} else {
    Add-Result "Safety" "No order command surface" "FAIL" (($orderHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$mutationHits = @(Select-String -Path $prototypeFile -Pattern "IOrderRepository","IFillRepository","PositionRepository","ModelRun","RiskState","Wallet","SubmitToShadowReplayAsync" -SimpleMatch -ErrorAction SilentlyContinue)
if ($mutationHits.Count -eq 0) {
    Add-Result "Safety" "No trading mutation dependency" "PASS" "No trading-state repository or runtime mutation dependency found."
} else {
    Add-Result "Safety" "No trading mutation dependency" "FAIL" (($mutationHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$registrationHits = @(Select-String -Path $apiProgram,$workerProgram -Pattern "LmaxReadOnlySocketPrototype","RealLmaxGateway","LmaxVenueGatewaySkeleton","ExternalReadOnlyPrototypeGateway" -SimpleMatch -ErrorAction SilentlyContinue)
if ($registrationHits.Count -eq 0 -and (Select-String -Path $apiProgram -Pattern "FakeLmaxGateway" -SimpleMatch -Quiet)) {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No prototype, real gateway, scheduler, or hosted service registration found."
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
$reportPath = Join-Path $reportDir "phase5w-operational-signoff-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    finalDecision = $decision
    signoffFile = $signoffPath
    externalConnectionAttempted = $false
    manualReplayPerformed = $false
    runtimeShadowReplaySubmit = $false
    results = $results
} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $reportPath"

if ($decision -eq "FAIL") { exit 1 }
