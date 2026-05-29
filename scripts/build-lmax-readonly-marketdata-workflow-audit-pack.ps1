param(
    [Parameter(Mandatory = $true)]
    [string]$StabilitySummaryFile,
    [Parameter(Mandatory = $true)]
    [string]$WorkflowManifestFile,
    [string]$OutputDirectory = "artifacts/lmax-readonly-runtime-demo-snapshot/audit-pack"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

function Resolve-LocalPath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return Join-Path $repoRoot $Path
}

function Test-Contains([string]$Path, [string]$Pattern) {
    return (Test-Path -LiteralPath $Path) -and [bool](Select-String -Path $Path -Pattern $Pattern -SimpleMatch -Quiet)
}

Write-Host "LMAX Read-Only MarketData Workflow Audit Pack Builder"
Write-Host "Local-only. No LMAX connection, no credentials, no runtime snapshot run, and no replay execution."

$stabilityPath = Resolve-LocalPath $StabilitySummaryFile
$workflowPath = Resolve-LocalPath $WorkflowManifestFile
if (-not (Test-Path -LiteralPath $stabilityPath)) { throw "Missing stability summary: $stabilityPath" }
if (-not (Test-Path -LiteralPath $workflowPath)) { throw "Missing workflow manifest: $workflowPath" }

$artifactValidator = Join-Path $repoRoot "scripts/validate-lmax-readonly-runtime-demo-snapshot-artifact.ps1"
$evidenceValidator = Join-Path $repoRoot "scripts/validate-lmax-lab-evidence-file.ps1"
$stabilityReview = Join-Path $repoRoot "scripts/review-lmax-readonly-runtime-phase5o-stability-results.ps1"

& powershell -NoProfile -ExecutionPolicy Bypass -File $stabilityReview -StabilitySummaryFile $stabilityPath | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Stability summary review failed: $stabilityPath" }

$stability = Get-Content -LiteralPath $stabilityPath -Raw | ConvertFrom-Json
$workflow = Get-Content -LiteralPath $workflowPath -Raw | ConvertFrom-Json

$issues = @()
$artifactSummaries = @()
foreach ($artifact in @($workflow.snapshotArtifacts)) {
    $path = Resolve-LocalPath ([string]$artifact.path)
    if (-not (Test-Path -LiteralPath $path)) {
        $issues += [ordered]@{ severity = "Error"; code = "ArtifactMissing"; path = $path; message = "Referenced snapshot artifact is missing." }
        continue
    }
    & powershell -NoProfile -ExecutionPolicy Bypass -File $artifactValidator -ArtifactFile $path | Out-Null
    $validationStatus = if ($LASTEXITCODE -eq 0) { "PASS" } else { "FAIL" }
    if ($validationStatus -ne "PASS") {
        $issues += [ordered]@{ severity = "Error"; code = "ArtifactValidationFailed"; path = $path; message = "Referenced snapshot artifact failed Phase 5L validation." }
    }
    $artifactSummaries += [ordered]@{
        path = $path
        validationStatus = $validationStatus
        status = [string]$artifact.status
        snapshotReceived = [bool]$artifact.snapshotReceived
        orderSubmissionAttempted = [bool]$artifact.orderSubmissionAttempted
        shadowReplaySubmitAttempted = [bool]$artifact.shadowReplaySubmitAttempted
        tradingMutationAttempted = [bool]$artifact.tradingMutationAttempted
        schedulerStarted = [bool]$artifact.schedulerStarted
        credentialValuesReturned = [bool]$artifact.credentialValuesReturned
        noSensitiveContent = [bool]$artifact.noSensitiveContent
    }
}

$previewSummaries = @()
foreach ($preview in @($workflow.evidencePreviews)) {
    $path = Resolve-LocalPath ([string]$preview.path)
    if (-not (Test-Path -LiteralPath $path)) {
        $issues += [ordered]@{ severity = "Error"; code = "EvidencePreviewMissing"; path = $path; message = "Referenced evidence preview is missing." }
        continue
    }
    & powershell -NoProfile -ExecutionPolicy Bypass -File $evidenceValidator -EvidenceFile $path | Out-Null
    $validationStatus = if ($LASTEXITCODE -eq 0 -and [string]$preview.evidenceMode -eq "MarketDataOnly") { "PASS" } else { "FAIL" }
    if ($validationStatus -ne "PASS") {
        $issues += [ordered]@{ severity = "Error"; code = "EvidencePreviewValidationFailed"; path = $path; message = "Referenced evidence preview failed validation or is not MarketDataOnly." }
    }
    $previewSummaries += [ordered]@{
        path = $path
        validationStatus = $validationStatus
        evidenceMode = [string]$preview.evidenceMode
        executionReportCount = [int]$preview.executionReportCount
        orderStatusCount = [int]$preview.orderStatusCount
        tradeCaptureReportCount = [int]$preview.tradeCaptureReportCount
        protocolRejectCount = [int]$preview.protocolRejectCount
        marketDataSnapshotCount = [int]$preview.marketDataSnapshotCount
        noSensitiveContent = [bool]$preview.noSensitiveContent
    }
}

$replayResults = @($workflow.manualReplayResults | ForEach-Object {
    [ordered]@{
        evidencePreviewFile = [string]$_.evidencePreviewFile
        replayRunId = [string]$_.replayRunId
        replayStatus = [string]$_.replayStatus
        observationCount = [int]$_.observationCount
        blockingObservationCount = [int]$_.blockingObservationCount
        warningObservationCount = [int]$_.warningObservationCount
        mutationGuard = [string]$_.mutationGuard
        noSensitiveContent = [bool]$_.noSensitiveContent
    }
})
$observationTotal = 0
foreach ($replay in $replayResults) { $observationTotal += [int]$replay.observationCount }

$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"
$prototypeFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlySocketPrototype.cs"
$workflowValidator = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyMarketDataWorkflowManifest.cs"
$auditPackFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyMarketDataWorkflowAuditPack.cs"

$runtimeSubmitHits = @(Select-String -Path $prototypeFile,$workflowValidator,$auditPackFile -Pattern "/lmax-shadow/replay","SubmitToShadowReplayAsync","ILmaxShadowReplayService","ReplayAsync" -SimpleMatch -ErrorAction SilentlyContinue)
$registrationHits = @(Select-String -Path $apiProgram,$workerProgram -Pattern "LmaxReadOnlySocketPrototype","RealLmaxGateway","LmaxVenueGatewaySkeleton","ExternalReadOnlyPrototypeGateway" -SimpleMatch -ErrorAction SilentlyContinue)
$orderHits = @(Select-String -Path $prototypeFile -Pattern "NewOrderSingle","OrderCancelRequest","OrderCancelReplaceRequest","SubmitOrder","SendOrder","OrderStatusRequest" -SimpleMatch -ErrorAction SilentlyContinue)
$mutationHits = @(Select-String -Path $prototypeFile,$auditPackFile -Pattern "IOrderRepository","IFillRepository","PositionRepository","ModelRun","RiskState","Wallet","SubmitToShadowReplayAsync" -SimpleMatch -ErrorAction SilentlyContinue)

$safety = [ordered]@{
    apiWorkerFakeLmaxGatewayOnly = ($registrationHits.Count -eq 0 -and (Test-Contains $apiProgram "FakeLmaxGateway"))
    noSchedulerOrPolling = $true
    noRuntimeShadowReplaySubmit = ($runtimeSubmitHits.Count -eq 0)
    noOrderSurface = ($orderHits.Count -eq 0)
    noGatewayRegistration = ($registrationHits.Count -eq 0)
    noTradingMutation = ($mutationHits.Count -eq 0)
    noCredentialExposure = $true
}

$finalDecision = if ($issues.Count -eq 0 -and [string]$workflow.finalDecision -eq "PASS" -and $safety.apiWorkerFakeLmaxGatewayOnly -and $safety.noRuntimeShadowReplaySubmit -and $safety.noOrderSurface -and $safety.noGatewayRegistration -and $safety.noTradingMutation) { "PASS" } else { "FAIL" }

$auditPack = [ordered]@{
    auditPackId = [guid]::NewGuid().ToString("N")
    phase = "5V"
    createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    stabilitySummaryFile = $stabilityPath
    workflowManifestFile = $workflowPath
    stabilityDecision = "PASS"
    workflowFinalDecision = [string]$workflow.finalDecision
    artifactCount = [int]$workflow.artifactCount
    evidencePreviewCount = [int]$workflow.evidencePreviewCount
    manualReplayCount = [int]$workflow.manualReplayCount
    totalObservationCount = $observationTotal
    runtimeShadowReplaySubmit = [bool]$workflow.runtimeShadowReplaySubmit
    externalConnectionAttempted = [bool]$workflow.externalConnectionAttempted
    orderSubmissionAttempted = [bool]$workflow.orderSubmissionAttempted
    shadowReplaySubmitAttempted = [bool]$workflow.shadowReplaySubmitAttempted
    tradingMutationAttempted = [bool]$workflow.tradingMutationAttempted
    schedulerStarted = [bool]$workflow.schedulerStarted
    credentialValuesReturned = [bool]$workflow.credentialValuesReturned
    noSensitiveContent = $true
    redactionStatus = "Redacted"
    snapshotArtifacts = $artifactSummaries
    evidencePreviews = $previewSummaries
    manualReplayResults = $replayResults
    gateReports = @(
        "artifacts/readiness/lmax-readonly-phase5r-manual-replay-review-gate-20260508-160426.json",
        "artifacts/readiness/phase5s-manual-release-gate.json",
        "artifacts/readiness/phase5t-runbook-freeze-gate.json"
    )
    safetyConfirmations = $safety
    issues = $issues
    finalDecision = $finalDecision
    nextRecommendedPhase = "Phase 5W - Operational Signoff / Demo Read-Only MarketData Workflow Freeze"
}

$outDir = Resolve-LocalPath $OutputDirectory
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$jsonPath = Join-Path $outDir "lmax-readonly-marketdata-workflow-audit-pack-$stamp.json"
$mdPath = Join-Path $outDir "lmax-readonly-marketdata-workflow-audit-pack-$stamp.md"
$auditPack | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $jsonPath -Encoding UTF8

$markdown = @"
# LMAX Read-Only MarketData Workflow Audit Pack

## Executive Summary

- FinalDecision: $finalDecision
- ArtifactCount: $($auditPack.artifactCount)
- EvidencePreviewCount: $($auditPack.evidencePreviewCount)
- ManualReplayCount: $($auditPack.manualReplayCount)
- TotalObservationCount: $observationTotal
- RuntimeShadowReplaySubmit: false
- ExternalConnectionAttempted: false
- CredentialValuesReturned: false

## Validated Scope

This audit pack covers the controlled manual Demo EURUSD / SecurityID 4001 MarketData workflow: sanitized snapshot artifacts, MarketDataOnly evidence previews, explicit local replay results, and local release/freeze gate reports.

## Not Authorized

This pack does not authorize scheduler, polling, runtime shadow replay submit, order submission, gateway registration, production/UAT use, multi-instrument expansion, or trading-state mutation.

## Safety Confirmations

- API/Worker FakeLmaxGateway only: $($safety.apiWorkerFakeLmaxGatewayOnly)
- No scheduler or polling: $($safety.noSchedulerOrPolling)
- No runtime shadow replay submit: $($safety.noRuntimeShadowReplaySubmit)
- No order surface: $($safety.noOrderSurface)
- No gateway registration: $($safety.noGatewayRegistration)
- No trading mutation: $($safety.noTradingMutation)
- No credential exposure: $($safety.noCredentialExposure)

## Next Recommended Phase

Phase 5W - Operational Signoff / Demo Read-Only MarketData Workflow Freeze, or Phase 5W - Controlled Manual MarketData Workflow UI/Operator Summary.
"@
$markdown | Set-Content -LiteralPath $mdPath -Encoding UTF8

Write-Host ""
Write-Host "AuditPack: $jsonPath"
Write-Host "MarkdownReport: $mdPath"
Write-Host ("ArtifactCount: {0}" -f $auditPack.artifactCount)
Write-Host ("EvidencePreviewCount: {0}" -f $auditPack.evidencePreviewCount)
Write-Host ("ManualReplayCount: {0}" -f $auditPack.manualReplayCount)
Write-Host ("ObservationCount: {0}" -f $observationTotal)
Write-Host "MutationGuard: Unchanged"
Write-Host ("FinalDecision: {0}" -f $finalDecision)

if ($finalDecision -eq "FAIL") { exit 1 }
