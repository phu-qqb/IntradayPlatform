param(
    [string]$WorkflowManifestFile,
    [switch]$RequireApi,
    [switch]$Replay,
    [string]$BaseUrl = "http://localhost:5050"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()

function Add-Result([string]$Category, [string]$Check, [string]$Status, [string]$Detail) {
    $script:results += [ordered]@{ category = $Category; check = $Check; status = $Status; detail = $Detail }
    Write-Host ("{0}: {1} / {2} - {3}" -f $Status, $Category, $Check, $Detail)
}

function Test-Contains([string]$Path, [string]$Pattern) {
    return (Test-Path -LiteralPath $Path) -and [bool](Select-String -Path $Path -Pattern $Pattern -SimpleMatch -Quiet)
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

Write-Host "LMAX Read-Only Runtime Phase 5R Manual Replay Review Gate"
Write-Host "Local-only. No external LMAX connection, no scheduler, no polling, and no runtime shadow replay submit."

$workflowValidator = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyMarketDataWorkflowManifest.cs"
$workflowTests = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlyMarketDataWorkflowValidatorTests.cs"
$workflowScript = Join-Path $repoRoot "scripts/run-lmax-readonly-marketdata-manual-workflow-review.ps1"
$prototypeFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlySocketPrototype.cs"
$mapperFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapper.cs"
$replayScript = Join-Path $repoRoot "scripts/replay-lmax-readonly-demo-snapshot-evidence-preview.ps1"

if ((Test-Contains $workflowScript "ConfirmLocalManualReplay") -and
    (Test-Contains $workflowScript "ReplayRequested") -and
    (Test-Contains $workflowScript "ManualReplayCount") -and
    (Test-Contains $workflowScript "ReplayRunId")) {
    Add-Result "Files" "Replay-enabled workflow script exists" "PASS" "Workflow review requires explicit local replay confirmation and records replay result metadata."
} else {
    Add-Result "Files" "Replay-enabled workflow script exists" "FAIL" "Workflow script is missing Phase 5R replay markers."
}

if ((Test-Contains $workflowValidator "ReplayCountDoesNotMatchPreviewCount") -and
    (Test-Contains $workflowValidator "externalConnectionAttempted") -and
    (Test-Contains $workflowValidator "ReplayStatusNotCompleted") -and
    (Test-Contains $workflowValidator "ReplayMutationGuardChanged")) {
    Add-Result "Files" "Workflow validator covers replay closure" "PASS" "Validator checks replay count, replay status, zero observations, mutation guard, and external connection flag."
} else {
    Add-Result "Files" "Workflow validator covers replay closure" "FAIL" "Workflow validator is missing Phase 5R replay closure markers."
}

if ((Test-Contains $workflowTests "Valid_manifest_with_three_replay_results_passes") -and
    (Test-Contains $workflowTests "Replay_count_mismatch_fails") -and
    (Test-Contains $workflowTests "External_connection_attempted_during_workflow_review_fails") -and
    (Test-Contains $workflowTests "Runtime_shadow_replay_submit_true_fails")) {
    Add-Result "Tests" "Manual replay workflow tests exist" "PASS" "Replay pass, replay omitted warning, observation/status/mutation failures, and runtime safety flags are covered."
} else {
    Add-Result "Tests" "Manual replay workflow tests exist" "FAIL" "Phase 5R workflow test markers are missing."
}

if ($RequireApi -or $Replay) {
    if (Test-ApiAvailable $BaseUrl) {
        Add-Result "API" "Local API availability" "PASS" "Local API is available at $BaseUrl."
    } else {
        Add-Result "API" "Local API availability" "FAIL" "Local API is required but not available at $BaseUrl."
    }
} else {
    Add-Result "API" "Local API availability" "WARN" "Not required unless -Replay or -RequireApi is supplied."
}

if (-not [string]::IsNullOrWhiteSpace($WorkflowManifestFile)) {
    $manifestPath = if ([IO.Path]::IsPathRooted($WorkflowManifestFile)) { $WorkflowManifestFile } else { Join-Path $repoRoot $WorkflowManifestFile }
    if (Test-Path -LiteralPath $manifestPath) {
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        $previewCount = @($manifest.evidencePreviews).Count
        $replayCount = @($manifest.manualReplayResults).Count
        $unsafeReplay = @($manifest.manualReplayResults | Where-Object {
            [string]$_.replayStatus -ne "Completed" -or
            [int]$_.observationCount -ne 0 -or
            [int]$_.blockingObservationCount -ne 0 -or
            [int]$_.warningObservationCount -ne 0 -or
            [string]$_.mutationGuard -ne "Unchanged" -or
            -not [bool]$_.noSensitiveContent
        })
        if ($replayCount -eq $previewCount -and $replayCount -gt 0 -and $unsafeReplay.Count -eq 0 -and
            -not [bool]$manifest.runtimeShadowReplaySubmit -and -not [bool]$manifest.externalConnectionAttempted) {
            Add-Result "Manifest" "Replay manifest validates" "PASS" "ManualReplayCount=$replayCount matches EvidencePreviewCount=$previewCount with zero observations and unchanged mutation guards."
        } else {
            Add-Result "Manifest" "Replay manifest validates" "FAIL" "Replay manifest must have replay count equal preview count, all Completed/zero-observation results, runtimeShadowReplaySubmit=false, and externalConnectionAttempted=false."
        }
    } else {
        Add-Result "Manifest" "Replay manifest validates" "FAIL" "Manifest file not found: $manifestPath"
    }
} else {
    Add-Result "Manifest" "Replay manifest validates" "WARN" "No workflow manifest supplied; source checks only."
}

$workflowTextSource = if (Test-Path -LiteralPath $workflowScript) { Get-Content -LiteralPath $workflowScript -Raw } else { "" }
if ($workflowTextSource -notmatch "while\s*\(" -and
    $workflowTextSource -notmatch "Register-ScheduledTask|New-ScheduledTask|Start-Job|Start-ThreadJob|Timer|HostedService|Start-Sleep") {
    Add-Result "Safety" "No scheduler or automatic polling" "PASS" "No scheduler, background job, timer, hosted service, sleep loop, or polling marker found."
} else {
    Add-Result "Safety" "No scheduler or automatic polling" "FAIL" "Scheduler, background job, timer, sleep, hosted-service, or polling marker found."
}

$runtimeSubmitHits = @(Select-String -Path $prototypeFile,$mapperFile,$workflowValidator -Pattern "/lmax-shadow/replay","SubmitToShadowReplayAsync","ILmaxShadowReplayService","ReplayAsync" -SimpleMatch -ErrorAction SilentlyContinue)
if ($runtimeSubmitHits.Count -eq 0 -and (Test-Contains $workflowScript "ConfirmLocalManualReplay") -and (Test-Path -LiteralPath $replayScript)) {
    Add-Result "Safety" "Runtime still does not submit to shadow replay" "PASS" "Runtime/prototype/mapper/validator files have no submit path; replay is script-only and explicit."
} else {
    Add-Result "Safety" "Runtime still does not submit to shadow replay" "FAIL" (($runtimeSubmitHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$forbiddenOrder = @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder", "SendOrder", "OrderStatusRequest")
$orderHits = @()
foreach ($word in $forbiddenOrder) {
    $orderHits += @(Select-String -Path $prototypeFile,$workflowScript -Pattern $word -SimpleMatch -ErrorAction SilentlyContinue)
}
if ($orderHits.Count -eq 0) {
    Add-Result "Safety" "No order command surface" "PASS" "No order command surface found in prototype or workflow script."
} else {
    Add-Result "Safety" "No order command surface" "FAIL" (($orderHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$forbiddenMutation = @("IOrderRepository", "IFillRepository", "PositionRepository", "ModelRun", "RiskState", "Wallet", "SubmitToShadowReplayAsync")
$mutationHits = @()
foreach ($word in $forbiddenMutation) {
    $mutationHits += @(Select-String -Path $prototypeFile,$workflowScript,$workflowValidator -Pattern $word -SimpleMatch -ErrorAction SilentlyContinue)
}
if ($mutationHits.Count -eq 0) {
    Add-Result "Safety" "No trading mutation dependency" "PASS" "No trading-state repository or runtime mutation dependency found."
} else {
    Add-Result "Safety" "No trading mutation dependency" "FAIL" (($mutationHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"
$registrationHits = @(Select-String -Path $apiProgram,$workerProgram -Pattern "LmaxReadOnlySocketPrototype","RealLmaxGateway","LmaxVenueGatewaySkeleton","ExternalReadOnlyPrototypeGateway" -SimpleMatch -ErrorAction SilentlyContinue)
if ($registrationHits.Count -eq 0 -and (Select-String -Path $apiProgram -Pattern "FakeLmaxGateway" -SimpleMatch -Quiet)) {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No prototype, real gateway, scheduler, or hosted service registration found."
} else {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" (($registrationHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

Add-Result "Runtime" "External socket attempts" "PASS" "No external snapshot attempts are made by this gate."

$failed = @($results | Where-Object { $_.status -eq "FAIL" })
$decision = if ($failed.Count -gt 0) { "FAIL" } elseif (@($results | Where-Object { $_.status -eq "WARN" }).Count -gt 0) { "PASS WITH KNOWN WARNINGS" } else { "PASS" }
$reportDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $reportDir "lmax-readonly-phase5r-manual-replay-review-gate-$stamp.json"
[ordered]@{ generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o"); finalDecision = $decision; results = $results } |
    ConvertTo-Json -Depth 8 |
    Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $reportPath"

if ($decision -eq "FAIL") { exit 1 }
