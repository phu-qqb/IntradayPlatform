param(
    [string]$ReleaseManifestFile = "artifacts/lmax-readonly-runtime-demo-snapshot/workflow/phase5s-manual-release-manifest.json",
    [switch]$RequireReplay,
    [string]$BaseUrl = "http://localhost:5050"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()

function Add-Result([string]$Category, [string]$Check, [string]$Status, [string]$Detail) {
    $script:results += [ordered]@{ category = $Category; check = $Check; status = $Status; detail = $Detail }
    Write-Host ("{0}: {1} / {2} - {3}" -f $Status, $Category, $Check, $Detail)
}

function Resolve-LocalPath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return Join-Path $repoRoot $Path
}

function Test-Contains([string]$Path, [string]$Pattern) {
    return (Test-Path -LiteralPath $Path) -and [bool](Select-String -Path $Path -Pattern $Pattern -SimpleMatch -Quiet)
}

Write-Host "LMAX Read-Only Runtime Phase 5S Controlled Manual Workflow Release Gate"
Write-Host "Local-only. No external LMAX connection, no scheduler/polling, no orders, and no runtime shadow replay submit."

$releaseScript = Join-Path $repoRoot "scripts/run-lmax-readonly-marketdata-manual-workflow-release.ps1"
$releaseValidator = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyManualWorkflowReleaseValidator.cs"
$releaseTests = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlyManualWorkflowReleaseValidatorTests.cs"
$artifactValidator = Join-Path $repoRoot "scripts/validate-lmax-readonly-runtime-demo-snapshot-artifact.ps1"
$evidenceValidator = Join-Path $repoRoot "scripts/validate-lmax-lab-evidence-file.ps1"
$prototypeFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlySocketPrototype.cs"
$workflowValidator = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyMarketDataWorkflowManifest.cs"
$workflowScript = Join-Path $repoRoot "scripts/run-lmax-readonly-marketdata-manual-workflow-review.ps1"
$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"

if ((Test-Contains $releaseScript "ConfirmRepeatedManualSnapshots") -and
    (Test-Contains $releaseScript "ConfirmLocalManualReplay") -and
    (Test-Contains $releaseScript "phase5s-manual-release-manifest.json")) {
    Add-Result "Files" "Release workflow script exists" "PASS" "Script records a fixed Phase 5S release manifest and keeps replay explicit."
} else {
    Add-Result "Files" "Release workflow script exists" "FAIL" "Release script or required markers are missing."
}

if ((Test-Contains $releaseValidator "LmaxReadOnlyManualWorkflowReleaseValidator") -and
    (Test-Contains $releaseValidator "ReplayCountDoesNotMatchPreviewCount") -and
    (Test-Contains $releaseValidator "ReplayObservationsPresent") -and
    (Test-Contains $releaseValidator "runtimeShadowReplaySubmit")) {
    Add-Result "Files" "Release validator exists" "PASS" "Validator covers artifacts, previews, replay results, and safety flags."
} else {
    Add-Result "Files" "Release validator exists" "FAIL" "Release validator or required markers are missing."
}

if ((Test-Contains $releaseTests "Release_manifest_with_three_replays_passes") -and
    (Test-Contains $releaseTests "Release_manifest_without_replay_passes_with_warning") -and
    (Test-Contains $releaseTests "Replay_count_mismatch_fails") -and
    (Test-Contains $releaseTests "Runtime_shadow_submit_or_external_connection_fails")) {
    Add-Result "Tests" "Release validator tests exist" "PASS" "PASS, PASS_WITH_WARNINGS, replay failure, and safety flag tests are present."
} else {
    Add-Result "Tests" "Release validator tests exist" "FAIL" "Phase 5S release test markers are missing."
}

$manifestPath = Resolve-LocalPath $ReleaseManifestFile
if (Test-Path -LiteralPath $manifestPath) {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $artifactCount = @($manifest.snapshotArtifacts).Count
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

    if ($artifactCount -gt 0 -and $previewCount -gt 0 -and $artifactCount -eq $previewCount) {
        Add-Result "Manifest" "Artifact and preview counts" "PASS" "ArtifactCount=$artifactCount EvidencePreviewCount=$previewCount."
    } else {
        Add-Result "Manifest" "Artifact and preview counts" "FAIL" "Artifact and preview counts must be present and equal."
    }

    if ($replayCount -eq 0 -and -not $RequireReplay) {
        Add-Result "Manifest" "Manual replay results" "WARN" "Replay was skipped; release is PASS_WITH_WARNINGS unless replay is required."
    } elseif ($replayCount -eq $previewCount -and $unsafeReplay.Count -eq 0) {
        Add-Result "Manifest" "Manual replay results" "PASS" "ManualReplayCount=$replayCount matches EvidencePreviewCount=$previewCount with Completed zero-observation replays."
    } else {
        Add-Result "Manifest" "Manual replay results" "FAIL" "Manual replay count must match preview count and all replay results must be Completed/zero-observation/Unchanged."
    }

    if (-not [bool]$manifest.runtimeShadowReplaySubmit -and -not [bool]$manifest.externalConnectionAttempted -and
        -not [bool]$manifest.orderSubmissionAttempted -and -not [bool]$manifest.tradingMutationAttempted -and
        -not [bool]$manifest.schedulerStarted -and -not [bool]$manifest.credentialValuesReturned -and [bool]$manifest.noSensitiveContent) {
        Add-Result "Manifest" "Safety flags" "PASS" "No runtime shadow submit, external connection, order, scheduler, mutation, or credential-value return in release manifest."
    } else {
        Add-Result "Manifest" "Safety flags" "FAIL" "One or more release manifest safety flags are unsafe."
    }

    foreach ($artifact in @($manifest.snapshotArtifacts)) {
        $path = Resolve-LocalPath ([string]$artifact.path)
        if (-not (Test-Path -LiteralPath $path)) {
            Add-Result "Artifacts" "Snapshot artifact exists" "FAIL" "Missing artifact: $path"
            continue
        }
        & powershell -NoProfile -ExecutionPolicy Bypass -File $artifactValidator -ArtifactFile $path | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Add-Result "Artifacts" "Snapshot artifact validates" "PASS" $path
        } else {
            Add-Result "Artifacts" "Snapshot artifact validates" "FAIL" $path
        }
    }

    foreach ($preview in @($manifest.evidencePreviews)) {
        $path = Resolve-LocalPath ([string]$preview.path)
        if (-not (Test-Path -LiteralPath $path)) {
            Add-Result "Evidence" "Evidence preview exists" "FAIL" "Missing preview: $path"
            continue
        }
        & powershell -NoProfile -ExecutionPolicy Bypass -File $evidenceValidator -EvidenceFile $path | Out-Null
        if ($LASTEXITCODE -eq 0 -and [string]$preview.evidenceMode -eq "MarketDataOnly" -and [bool]$preview.noSensitiveContent) {
            Add-Result "Evidence" "Evidence preview validates" "PASS" $path
        } else {
            Add-Result "Evidence" "Evidence preview validates" "FAIL" $path
        }
    }
} else {
    Add-Result "Manifest" "Release manifest present" "WARN" "Release manifest not found: $manifestPath"
}

$runtimeSubmitHits = @(Select-String -Path $prototypeFile,$workflowValidator,$releaseValidator -Pattern "/lmax-shadow/replay","SubmitToShadowReplayAsync","ILmaxShadowReplayService","ReplayAsync" -SimpleMatch -ErrorAction SilentlyContinue)
if ($runtimeSubmitHits.Count -eq 0 -and (Test-Contains $workflowScript "ConfirmLocalManualReplay")) {
    Add-Result "Safety" "Runtime still does not submit to shadow replay" "PASS" "Runtime/prototype/validator files have no submit path; replay is script-only and explicit."
} else {
    Add-Result "Safety" "Runtime still does not submit to shadow replay" "FAIL" (($runtimeSubmitHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$releaseText = if (Test-Path -LiteralPath $releaseScript) { Get-Content -LiteralPath $releaseScript -Raw } else { "" }
if ($releaseText -notmatch "while\s*\(" -and $releaseText -notmatch "Register-ScheduledTask|New-ScheduledTask|Start-Job|Start-ThreadJob|Timer|HostedService|Start-Sleep") {
    Add-Result "Safety" "No scheduler or automatic polling" "PASS" "No scheduler, background job, timer, hosted service, sleep loop, or polling marker found."
} else {
    Add-Result "Safety" "No scheduler or automatic polling" "FAIL" "Scheduler, background job, timer, sleep, hosted-service, or polling marker found."
}

$forbiddenOrder = @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder", "SendOrder", "OrderStatusRequest")
$orderHits = @()
foreach ($word in $forbiddenOrder) {
    $orderHits += @(Select-String -Path $prototypeFile,$releaseScript -Pattern $word -SimpleMatch -ErrorAction SilentlyContinue)
}
if ($orderHits.Count -eq 0) {
    Add-Result "Safety" "No order command surface" "PASS" "No order command surface found in prototype or release script."
} else {
    Add-Result "Safety" "No order command surface" "FAIL" (($orderHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$mutationHits = @(Select-String -Path $prototypeFile,$releaseScript,$releaseValidator -Pattern "IOrderRepository","IFillRepository","PositionRepository","ModelRun","RiskState","Wallet","SubmitToShadowReplayAsync" -SimpleMatch -ErrorAction SilentlyContinue)
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

Add-Result "Runtime" "External socket attempts" "PASS" "No external snapshot attempts are made by this gate."

$failed = @($results | Where-Object { $_.status -eq "FAIL" })
$warnings = @($results | Where-Object { $_.status -eq "WARN" })
$decision = if ($failed.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS_WITH_WARNINGS" } else { "PASS" }
$reportDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$reportPath = Join-Path $reportDir "phase5s-manual-release-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    finalDecision = $decision
    releaseManifestFile = $manifestPath
    results = $results
} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $reportPath"

if ($decision -eq "FAIL") { exit 1 }
