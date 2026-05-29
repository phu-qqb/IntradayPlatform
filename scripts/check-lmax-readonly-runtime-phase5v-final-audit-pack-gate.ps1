param(
    [string]$AuditPackFile,
    [string]$StabilitySummaryFile,
    [string]$WorkflowManifestFile
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

Write-Host "LMAX Read-Only Runtime Phase 5V Final Audit Pack Gate"
Write-Host "Local-only. No external LMAX connection, no credentials, no runtime snapshot run, and no replay execution."

if ([string]::IsNullOrWhiteSpace($AuditPackFile)) {
    if ([string]::IsNullOrWhiteSpace($StabilitySummaryFile) -or [string]::IsNullOrWhiteSpace($WorkflowManifestFile)) {
        throw "Provide -AuditPackFile or both -StabilitySummaryFile and -WorkflowManifestFile."
    }
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repoRoot "scripts/build-lmax-readonly-marketdata-workflow-audit-pack.ps1") -StabilitySummaryFile $StabilitySummaryFile -WorkflowManifestFile $WorkflowManifestFile | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Audit pack build failed." }
    $AuditPackFile = (Get-ChildItem -Path (Join-Path $repoRoot "artifacts/lmax-readonly-runtime-demo-snapshot/audit-pack") -Filter "lmax-readonly-marketdata-workflow-audit-pack-*.json" | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
}

$auditPath = Resolve-LocalPath $AuditPackFile
if (-not (Test-Path -LiteralPath $auditPath)) { throw "Missing audit pack: $auditPath" }
$audit = Get-Content -LiteralPath $auditPath -Raw | ConvertFrom-Json

if ([string]$audit.finalDecision -eq "PASS") {
    Add-Result "AuditPack" "Final decision" "PASS" "Audit pack FinalDecision=PASS."
} else {
    Add-Result "AuditPack" "Final decision" "FAIL" "Expected PASS, got $($audit.finalDecision)."
}

if ([int]$audit.artifactCount -gt 0 -and [int]$audit.evidencePreviewCount -eq [int]$audit.artifactCount -and [int]$audit.manualReplayCount -eq [int]$audit.evidencePreviewCount) {
    Add-Result "AuditPack" "Counts" "PASS" "ArtifactCount=$($audit.artifactCount) EvidencePreviewCount=$($audit.evidencePreviewCount) ManualReplayCount=$($audit.manualReplayCount)."
} else {
    Add-Result "AuditPack" "Counts" "FAIL" "Artifact, preview, and replay counts must be present and equal."
}

$badReplay = @($audit.manualReplayResults | Where-Object {
    [string]$_.replayStatus -ne "Completed" -or
    [int]$_.observationCount -ne 0 -or
    [int]$_.blockingObservationCount -ne 0 -or
    [int]$_.warningObservationCount -ne 0 -or
    [string]$_.mutationGuard -ne "Unchanged" -or
    -not [bool]$_.noSensitiveContent
})
if ($badReplay.Count -eq 0) {
    Add-Result "Replay" "Replay results" "PASS" "All replay results are Completed, zero-observation, sanitized, and mutation guard unchanged."
} else {
    Add-Result "Replay" "Replay results" "FAIL" "One or more replay results are unsafe."
}

if (-not [bool]$audit.runtimeShadowReplaySubmit -and -not [bool]$audit.externalConnectionAttempted -and -not [bool]$audit.orderSubmissionAttempted -and -not [bool]$audit.tradingMutationAttempted -and -not [bool]$audit.schedulerStarted -and -not [bool]$audit.credentialValuesReturned -and [bool]$audit.noSensitiveContent) {
    Add-Result "AuditPack" "Safety flags" "PASS" "No runtime submit, external connection, order, scheduler, mutation, or credential-value return."
} else {
    Add-Result "AuditPack" "Safety flags" "FAIL" "One or more audit pack safety flags are unsafe."
}

$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"
$prototypeFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlySocketPrototype.cs"
$auditPackCode = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyMarketDataWorkflowAuditPack.cs"
$builderScript = Join-Path $repoRoot "scripts/build-lmax-readonly-marketdata-workflow-audit-pack.ps1"

$runtimeSubmitHits = @(Select-String -Path $prototypeFile,$auditPackCode -Pattern "/lmax-shadow/replay","SubmitToShadowReplayAsync","ILmaxShadowReplayService","ReplayAsync" -SimpleMatch -ErrorAction SilentlyContinue)
if ($runtimeSubmitHits.Count -eq 0) {
    Add-Result "Safety" "Runtime still does not submit to shadow replay" "PASS" "Runtime/prototype/audit-pack files have no shadow replay submit path."
} else {
    Add-Result "Safety" "Runtime still does not submit to shadow replay" "FAIL" (($runtimeSubmitHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$builderText = if (Test-Path -LiteralPath $builderScript) { Get-Content -LiteralPath $builderScript -Raw } else { "" }
if ($builderText -notmatch "while\s*\(" -and $builderText -notmatch "Register-ScheduledTask|New-ScheduledTask|Start-Job|Start-ThreadJob|Timer|HostedService|Start-Sleep") {
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
$reportPath = Join-Path $reportDir "phase5v-final-audit-pack-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    finalDecision = $decision
    auditPackFile = $auditPath
    externalConnectionAttempted = $false
    manualReplayPerformed = $false
    runtimeShadowReplaySubmit = $false
    results = $results
} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $reportPath"

if ($decision -eq "FAIL") { exit 1 }
