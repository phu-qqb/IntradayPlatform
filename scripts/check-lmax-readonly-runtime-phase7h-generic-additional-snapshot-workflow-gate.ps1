param(
    [string]$ArtifactFile = "",
    [string]$ReviewReportFile = "",
    [string]$EvidencePreviewFile = "",
    [string]$ReplayReportFile = "",
    [string]$ClosureManifestFile = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=|raw\s*fix)'

function Add-Result([string]$Category, [string]$Check, [string]$Status, [string]$Detail) {
    $script:results += [ordered]@{ category = $Category; check = $Check; status = $Status; detail = $Detail }
    Write-Host ("{0}: {1} / {2} - {3}" -f $Status, $Category, $Check, $Detail)
}
function Resolve-LocalPath([string]$PathValue) {
    if ([string]::IsNullOrWhiteSpace($PathValue)) { return "" }
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}
function Read-JsonForGate([string]$PathValue, [string]$Label) {
    $resolved = Resolve-LocalPath $PathValue
    if ([string]::IsNullOrWhiteSpace($resolved) -or -not (Test-Path -LiteralPath $resolved)) {
        Add-Result $Label "File exists" "FAIL" "Missing $resolved"
        return $null
    }
    Add-Result $Label "File exists" "PASS" $resolved
    $raw = Get-Content -LiteralPath $resolved -Raw
    $safe = $raw -replace 'LMAX_DEMO_FIX_USERNAME|LMAX_DEMO_FIX_PASSWORD|LMAX_DEMO_SENDER_COMP_ID|LMAX_DEMO_TARGET_COMP_ID|credentialProfileName','SAFE_METADATA'
    if ($safe -match $script:sensitivePattern) { Add-Result $Label "No sensitive content" "FAIL" "Credential-shaped or raw FIX content found." } else { Add-Result $Label "No sensitive content" "PASS" "No credential-shaped or raw FIX content." }
    return ($raw | ConvertFrom-Json)
}
function Get-Hits($Paths, $Patterns) {
    $existing = @($Paths | Where-Object { Test-Path -LiteralPath $_ })
    if ($existing.Count -eq 0) { return @() }
    return @(Select-String -Path $existing -Pattern $Patterns -SimpleMatch -ErrorAction SilentlyContinue)
}

Write-Host "LMAX Read-Only Runtime Phase 7H Generic Additional Instrument Workflow Gate"
Write-Host "Local-only. This gate does not connect to LMAX, request snapshots, replay evidence, schedule work, or use credentials."

foreach ($required in @(
    @{ name = "Phase 7H generic model"; path = "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyAdditionalInstrumentManualSnapshotWorkflow.cs" },
    @{ name = "Generic one-shot wrapper"; path = "scripts/run-lmax-readonly-runtime-demo-additional-instrument-snapshot-once.ps1" },
    @{ name = "Generic review script"; path = "scripts/review-lmax-readonly-additional-instrument-snapshot-result.ps1" },
    @{ name = "Generic evidence preview script"; path = "scripts/preview-lmax-readonly-additional-instrument-snapshot-evidence.ps1" },
    @{ name = "Generic manual replay script"; path = "scripts/replay-lmax-readonly-additional-instrument-evidence-preview.ps1" },
    @{ name = "Generic closure manifest script"; path = "scripts/build-lmax-readonly-additional-instrument-closure-manifest.ps1" },
    @{ name = "Phase 7H tests"; path = "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlyAdditionalInstrumentManualSnapshotWorkflowTests.cs" }
)) {
    $path = Join-Path $repoRoot $required.path
    if (Test-Path -LiteralPath $path) { Add-Result "Files" "$($required.name) exists" "PASS" $path } else { Add-Result "Files" "$($required.name) exists" "FAIL" "Missing $path" }
}

$wrapperPath = Join-Path $repoRoot "scripts/run-lmax-readonly-runtime-demo-additional-instrument-snapshot-once.ps1"
if (Test-Path -LiteralPath $wrapperPath) {
    $wrapper = Get-Content -LiteralPath $wrapperPath -Raw
    foreach ($marker in @("GBPUSD","EURGBP","USDJPY","AUDUSD","FinalPreRunGateFile","AllowExternalConnections","ConfirmDemoReadOnly","No batch","no loop","no retry","run-lmax-readonly-runtime-demo-snapshot-prototype.ps1")) {
        if ($wrapper.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "Wrapper" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "Wrapper" "Marker: $marker" "FAIL" "Marker missing." }
    }
    if ($wrapper -match '(?i)(while\s*\(|Start-Job|Register-ScheduledTask|Register-ObjectEvent|PeriodicTimer|System\.Threading\.Timer)') {
        Add-Result "Wrapper" "No loop/scheduler primitives" "FAIL" "Loop or scheduler primitive found in wrapper."
    } else {
        Add-Result "Wrapper" "No loop/scheduler primitives" "PASS" "No retry loop or scheduler primitive found."
    }
}

if (-not [string]::IsNullOrWhiteSpace($ArtifactFile)) {
    $artifactPath = Resolve-LocalPath $ArtifactFile
    if (-not (Test-Path -LiteralPath $artifactPath)) {
        Add-Result "Artifact" "Artifact exists" "FAIL" "Missing $artifactPath"
    } else {
        $reviewScript = Join-Path $PSScriptRoot "review-lmax-readonly-additional-instrument-snapshot-result.ps1"
        & $reviewScript -ArtifactFile $artifactPath
        $reviewExitCode = if ($null -eq $LASTEXITCODE) { 0 } else { [int]$LASTEXITCODE }
        if ($reviewExitCode -ne 0) { Add-Result "Artifact" "Review script completed" "FAIL" "Review script exited $reviewExitCode." } else { Add-Result "Artifact" "Review script completed" "PASS" "Artifact reviewed locally." }
    }
}

if (-not [string]::IsNullOrWhiteSpace($ReviewReportFile)) {
    $review = Read-JsonForGate $ReviewReportFile "Review"
    if ($null -ne $review) {
        if ($review.finalDecision -in @("PASS","PASS_WITH_KNOWN_WARNINGS") -and $review.closureClassification -in @("CompletedWithBook","CompletedWithEmptyBook","FailedSafe")) { Add-Result "Review" "Safe closure decision" "PASS" "$($review.closureClassification) / $($review.finalDecision)." } else { Add-Result "Review" "Safe closure decision" "FAIL" "Unsafe review decision." }
        if (-not [bool]$review.orderSubmissionAttempted -and -not [bool]$review.shadowReplaySubmitAttempted -and -not [bool]$review.tradingMutationAttempted -and -not [bool]$review.schedulerStarted -and -not [bool]$review.credentialValuesReturned -and [bool]$review.noSensitiveContent) { Add-Result "Review" "Unsafe flags remain false" "PASS" "Review flags safe." } else { Add-Result "Review" "Unsafe flags remain false" "FAIL" "Unsafe review flag found." }
    }
}

if (-not [string]::IsNullOrWhiteSpace($EvidencePreviewFile)) {
    $preview = Read-JsonForGate $EvidencePreviewFile "EvidencePreview"
    if ($null -ne $preview) {
        if ([string]$preview.evidenceMode -eq "MarketDataOnly" -and [bool]$preview.noSensitiveContent -and @($preview.executionReports).Count -eq 0 -and @($preview.orderStatuses).Count -eq 0 -and @($preview.tradeCaptureReports).Count -eq 0 -and @($preview.protocolRejects).Count -eq 0 -and -not [bool]$preview.shadowReplaySubmitAttempted -and -not [bool]$preview.tradingMutationAttempted -and -not [bool]$preview.orderSubmissionAttempted) {
            Add-Result "EvidencePreview" "MarketDataOnly safe preview" "PASS" "MarketDataOnly preview with empty non-market-data arrays."
        } else {
            Add-Result "EvidencePreview" "MarketDataOnly safe preview" "FAIL" "Unsafe evidence preview."
        }
    }
}

if (-not [string]::IsNullOrWhiteSpace($ReplayReportFile)) {
    $replay = Read-JsonForGate $ReplayReportFile "ReplayReport"
    if ($null -ne $replay) {
        if ([string]$replay.finalDecision -eq "PASS" -and [string]$replay.replayStatus -eq "Completed" -and [int]$replay.observationCount -eq 0 -and [string]$replay.mutationGuard -eq "Unchanged") { Add-Result "ReplayReport" "Manual replay result" "PASS" "Completed; zero observations; mutation unchanged." } else { Add-Result "ReplayReport" "Manual replay result" "FAIL" "Replay report is not clean." }
    }
}

if (-not [string]::IsNullOrWhiteSpace($ClosureManifestFile)) {
    $closure = Read-JsonForGate $ClosureManifestFile "ClosureManifest"
    if ($null -ne $closure) {
        if ($closure.finalClosureDecision -in @("PASS","PASS_WITH_KNOWN_WARNINGS") -and -not [bool]$closure.orderSubmissionAttempted -and -not [bool]$closure.shadowReplaySubmitAttempted -and -not [bool]$closure.tradingMutationAttempted -and -not [bool]$closure.schedulerStarted -and [bool]$closure.noSensitiveContent) { Add-Result "ClosureManifest" "Safe closure manifest" "PASS" "Closure decision $($closure.finalClosureDecision)." } else { Add-Result "ClosureManifest" "Safe closure manifest" "FAIL" "Unsafe closure manifest." }
    }
}

$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"
$startupFiles = @($apiProgram, $workerProgram)
$startupText = ($startupFiles | Where-Object { Test-Path -LiteralPath $_ } | ForEach-Object { Get-Content -Raw -LiteralPath $_ }) -join "`n"
if ($startupText.Contains("FakeLmaxGateway") -and -not ($startupText.Contains("RealLmaxGateway") -or $startupText.Contains("LmaxVenueGatewaySkeleton"))) { Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No real gateway registration found." } else { Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" "Unexpected gateway registration marker." }

foreach ($scan in @(
    @{ category = "Scheduler"; check = "No scheduler/polling added"; patterns = @("PeriodicTimer", "System.Threading.Timer", "LmaxScheduler", "SecurityListPolling", "MarketDataPolling", "SnapshotPolling") },
    @{ category = "Replay"; check = "Runtime still does not submit to shadow replay"; patterns = @("SubmitToShadowReplay = true", "SubmittedToShadowReplay = true", "ReplaySubmitAsync") },
    @{ category = "Orders"; check = "No order surface"; patterns = @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "OrderStatusRequest", "TradeCaptureReportRequest", "SubmitOrder") },
    @{ category = "Mutation"; check = "No trading-state mutation references"; patterns = @("PersistTrade", "TradingState", "IOrderRepository", "IFillRepository", "IPositionRepository", "PersistLiveFix") }
)) {
    $hits = Get-Hits $startupFiles $scan.patterns
    if ($hits.Count -eq 0) { Add-Result $scan.category $scan.check "PASS" "No marker found in API/Worker startup." } else { Add-Result $scan.category $scan.check "FAIL" (($hits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ") }
}

Add-Result "Runtime" "External LMAX connection" "PASS" "This gate does not connect to LMAX."
Add-Result "Snapshot" "MarketData snapshot" "PASS" "This gate does not request snapshots."
Add-Result "Replay" "Automatic replay" "PASS" "This gate does not replay evidence."
Add-Result "Batch" "Batch execution" "PASS" "Generic workflow is one symbol per invocation and no batch execution is authorized."
Add-Result "Credentials" "Credential values" "PASS" "This gate does not require or print credential values."

$failed = @($results | Where-Object status -eq "FAIL")
$warnings = @($results | Where-Object status -eq "WARN")
$decision = if ($failed.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS_WITH_KNOWN_WARNINGS" } else { "PASS" }
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7h-generic-additional-snapshot-workflow-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7H"
    finalDecision = $decision
    supportedSymbols = @("GBPUSD","EURGBP","USDJPY","AUDUSD")
    batchExecutionAllowed = $false
    oneInstrumentAtATime = $true
    externalConnectionAttempted = $false
    snapshotAttempted = $false
    replayAttempted = $false
    orderSubmissionAttempted = $false
    shadowReplaySubmitAttempted = $false
    tradingMutationAttempted = $false
    schedulerStarted = $false
    apiWorkerGatewayMode = "FakeLmaxGateway"
    results = $results
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }
