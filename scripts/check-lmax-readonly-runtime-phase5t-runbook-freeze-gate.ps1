param(
    [string]$WorkflowManifestFile = "artifacts/lmax-readonly-runtime-demo-snapshot/workflow/phase5s-manual-release-manifest.json",
    [string]$ReleaseGateReportFile = "artifacts/readiness/phase5s-manual-release-gate.json"
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

Write-Host "LMAX Read-Only Runtime Phase 5T Runbook Freeze Gate"
Write-Host "Local-only. No external LMAX connection, no API requirement, no scheduler/polling, no orders, and no runtime shadow replay submit."

$reviewDoc = Join-Path $repoRoot "docs/LMAX_READONLY_RUNTIME_CONTROLLED_MANUAL_WORKFLOW_REVIEW.md"
$requiredDocs = @(
    $reviewDoc,
    (Join-Path $repoRoot "README.md"),
    (Join-Path $repoRoot "docs/INDEX.md"),
    (Join-Path $repoRoot "docs/LOCAL_RUNBOOK.md"),
    (Join-Path $repoRoot "docs/OPERATOR_MANUAL.md"),
    (Join-Path $repoRoot "docs/DEVELOPER_GUIDE.md"),
    (Join-Path $repoRoot "docs/OPERATIONAL_READINESS_CHECKLIST.md"),
    (Join-Path $repoRoot "docs/LMAX_READONLY_RUNTIME_PHASE_GATES.md"),
    (Join-Path $repoRoot "docs/LMAX_READONLY_RUNTIME_ADAPTER_IMPLEMENTATION_PLAN.md"),
    (Join-Path $repoRoot "docs/LMAX_READONLY_RUNTIME_ADAPTER_DESIGN.md"),
    (Join-Path $repoRoot "docs/LMAX_READONLY_RUNTIME_FIRST_TRANSPORT_PREFLIGHT.md")
)

$missingDocs = @($requiredDocs | Where-Object { -not (Test-Path -LiteralPath $_) })
if ($missingDocs.Count -eq 0 -and (Test-Contains $reviewDoc "PASS_WITH_WARNINGS") -and (Test-Contains $reviewDoc "ConfirmLocalManualReplay")) {
    Add-Result "Docs" "Controlled manual workflow review docs exist" "PASS" "Required runbook/freeze docs exist and document optional replay warning semantics."
} else {
    Add-Result "Docs" "Controlled manual workflow review docs exist" "FAIL" "Missing docs or required Phase 5T markers: $($missingDocs -join '; ')"
}

foreach ($doc in $requiredDocs) {
    if (Test-Path -LiteralPath $doc) {
        if ((Test-Contains $doc "Phase 5T") -or ($doc -eq $reviewDoc)) {
            Add-Result "Docs" ("Phase 5T marker in " + (Split-Path $doc -Leaf)) "PASS" $doc
        } else {
            Add-Result "Docs" ("Phase 5T marker in " + (Split-Path $doc -Leaf)) "WARN" "Phase 5T marker not found in $doc"
        }
    }
}

$manifestPath = Resolve-LocalPath $WorkflowManifestFile
$reportPath = Resolve-LocalPath $ReleaseGateReportFile
$manifest = $null
$releaseReport = $null

if (Test-Path -LiteralPath $manifestPath) {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $decision = [string]$manifest.finalDecision
    if ($decision -in @("PASS", "PASS_WITH_WARNINGS")) {
        Add-Result "Manifest" "Phase 5S manifest decision" "PASS" "FinalDecision=$decision"
    } else {
        Add-Result "Manifest" "Phase 5S manifest decision" "FAIL" "Expected PASS or PASS_WITH_WARNINGS, got $decision"
    }

    $replayCount = @($manifest.manualReplayResults).Count
    $previewCount = @($manifest.evidencePreviews).Count
    $warnings = @($manifest.warnings | ForEach-Object { [string]$_ })
    if ($decision -eq "PASS_WITH_WARNINGS" -and $replayCount -eq 0 -and ($warnings -match "replay|Replay")) {
        Add-Result "Manifest" "Warning reason" "PASS" "PASS_WITH_WARNINGS is explained by optional replay skipped."
    } elseif ($decision -eq "PASS") {
        Add-Result "Manifest" "Warning reason" "PASS" "No warning to explain for PASS decision."
    } else {
        Add-Result "Manifest" "Warning reason" "FAIL" "PASS_WITH_WARNINGS must be due to optional replay skipped."
    }

    if (-not [bool]$manifest.runtimeShadowReplaySubmit -and
        -not [bool]$manifest.externalConnectionAttempted -and
        -not [bool]$manifest.credentialValuesReturned -and
        -not [bool]$manifest.orderSubmissionAttempted -and
        -not [bool]$manifest.tradingMutationAttempted -and
        -not [bool]$manifest.schedulerStarted -and
        [bool]$manifest.noSensitiveContent) {
        Add-Result "Manifest" "Safety flags" "PASS" "runtimeShadowReplaySubmit=false, externalConnectionAttempted=false, credentialValuesReturned=false, and no mutation flags are set."
    } else {
        Add-Result "Manifest" "Safety flags" "FAIL" "One or more manifest safety flags are unsafe."
    }

    if ($previewCount -gt 0 -and @($manifest.snapshotArtifacts).Count -eq $previewCount) {
        Add-Result "Manifest" "Artifact and preview counts" "PASS" "ArtifactCount=$(@($manifest.snapshotArtifacts).Count) EvidencePreviewCount=$previewCount ReplayCount=$replayCount."
    } else {
        Add-Result "Manifest" "Artifact and preview counts" "FAIL" "Artifact and preview counts must be present and equal."
    }
} else {
    Add-Result "Manifest" "Phase 5S manifest exists" "FAIL" "Missing manifest: $manifestPath"
}

if (Test-Path -LiteralPath $reportPath) {
    $releaseReport = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    $reportDecision = [string]$releaseReport.finalDecision
    if ($reportDecision -in @("PASS", "PASS_WITH_WARNINGS")) {
        Add-Result "ReleaseGate" "Phase 5S gate decision" "PASS" "FinalDecision=$reportDecision"
    } else {
        Add-Result "ReleaseGate" "Phase 5S gate decision" "FAIL" "Expected PASS or PASS_WITH_WARNINGS, got $reportDecision"
    }

    $reportWarns = @($releaseReport.results | Where-Object { [string]$_.status -eq "WARN" })
    $unexpectedWarns = @($reportWarns | Where-Object { [string]$_.detail -notmatch "Replay was skipped|replay" })
    if ($reportDecision -eq "PASS_WITH_WARNINGS" -and $unexpectedWarns.Count -eq 0) {
        Add-Result "ReleaseGate" "Warning reason" "PASS" "Warnings are limited to optional replay skipped."
    } elseif ($reportDecision -eq "PASS") {
        Add-Result "ReleaseGate" "Warning reason" "PASS" "No warning to explain for PASS decision."
    } else {
        Add-Result "ReleaseGate" "Warning reason" "FAIL" "Unexpected warning reason in Phase 5S report."
    }
} else {
    Add-Result "ReleaseGate" "Phase 5S gate report exists" "FAIL" "Missing report: $reportPath"
}

$prototypeFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlySocketPrototype.cs"
$releaseScript = Join-Path $repoRoot "scripts/run-lmax-readonly-marketdata-manual-workflow-release.ps1"
$workflowScript = Join-Path $repoRoot "scripts/run-lmax-readonly-marketdata-manual-workflow-review.ps1"
$releaseValidator = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyManualWorkflowReleaseValidator.cs"
$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"

$runtimeSubmitHits = @(Select-String -Path $prototypeFile,$releaseValidator -Pattern "/lmax-shadow/replay","SubmitToShadowReplayAsync","ILmaxShadowReplayService","ReplayAsync" -SimpleMatch -ErrorAction SilentlyContinue)
if ($runtimeSubmitHits.Count -eq 0 -and (Test-Contains $workflowScript "ConfirmLocalManualReplay")) {
    Add-Result "Safety" "Runtime still does not submit to shadow replay" "PASS" "Runtime/prototype/validator files have no submit path; replay remains script-only and explicit."
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

Add-Result "Runtime" "External socket attempts" "PASS" "No external socket attempt is made by this gate."
Add-Result "Replay" "Manual replay" "PASS" "No manual replay is performed by this gate; replay remains explicit local API only."

$failed = @($results | Where-Object { $_.status -eq "FAIL" })
$warnings = @($results | Where-Object { $_.status -eq "WARN" })
$decision = if ($failed.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS_WITH_WARNINGS" } else { "PASS" }
$reportDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$gateReportPath = Join-Path $reportDir "phase5t-runbook-freeze-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    finalDecision = $decision
    workflowManifestFile = $manifestPath
    releaseGateReportFile = $reportPath
    externalConnectionAttempted = $false
    manualReplayPerformed = $false
    runtimeShadowReplaySubmit = $false
    results = $results
} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $gateReportPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $gateReportPath"

if ($decision -eq "FAIL") { exit 1 }
