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
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=|raw\s*fix|sendercompid|targetcompid)'

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
    if ($raw -match $script:sensitivePattern) {
        Add-Result $Label "No sensitive content" "FAIL" "Credential-shaped or raw FIX content found."
    } else {
        Add-Result $Label "No sensitive content" "PASS" "No credential-shaped or raw FIX content."
    }

    return ($raw | ConvertFrom-Json)
}

function Get-Hits($Paths, $Patterns) {
    $existing = @($Paths | Where-Object { Test-Path -LiteralPath $_ })
    if ($existing.Count -eq 0) { return @() }
    return @(Select-String -Path $existing -Pattern $Patterns -SimpleMatch -ErrorAction SilentlyContinue)
}

Write-Host "LMAX Read-Only Runtime Phase 7C GBPUSD Closure Gate"
Write-Host "Local-only. This gate does not connect to LMAX, request snapshots, replay evidence, schedule work, or use credentials."

foreach ($required in @(
    @{ name = "Phase 7C closure model"; path = "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyGbpusdMarketHoursSnapshotClosure.cs" },
    @{ name = "GBPUSD result model"; path = "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyGbpusdManualSnapshotResult.cs" },
    @{ name = "Phase 7C review script"; path = "scripts/review-lmax-readonly-gbpusd-market-hours-snapshot-result.ps1" },
    @{ name = "Phase 7C evidence preview script"; path = "scripts/preview-lmax-readonly-gbpusd-market-hours-snapshot-evidence.ps1" },
    @{ name = "Phase 7C manual replay script"; path = "scripts/replay-lmax-readonly-gbpusd-market-hours-evidence-preview.ps1" },
    @{ name = "Phase 7C closure manifest script"; path = "scripts/build-lmax-readonly-gbpusd-market-hours-closure-manifest.ps1" },
    @{ name = "Phase 7C tests"; path = "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlyGbpusdMarketHoursSnapshotClosureTests.cs" }
)) {
    $path = Join-Path $repoRoot $required.path
    if (Test-Path -LiteralPath $path) {
        Add-Result "Files" "$($required.name) exists" "PASS" $path
    } else {
        Add-Result "Files" "$($required.name) exists" "FAIL" "Missing $path"
    }
}

if ([string]::IsNullOrWhiteSpace($ArtifactFile)) {
    Add-Result "Artifact" "Market-hours GBPUSD artifact supplied" "WARN" "No artifact supplied; closure workflow is ready but no market-hours result has been reviewed."
} else {
    $artifactPath = Resolve-LocalPath $ArtifactFile
    if (-not (Test-Path -LiteralPath $artifactPath)) {
        Add-Result "Artifact" "Market-hours GBPUSD artifact exists" "FAIL" "Missing $artifactPath"
    } else {
        $reviewScript = Join-Path $PSScriptRoot "review-lmax-readonly-gbpusd-market-hours-snapshot-result.ps1"
        & $reviewScript -ArtifactFile $artifactPath
        $reviewExitCode = if ($null -eq $LASTEXITCODE) { 0 } else { [int]$LASTEXITCODE }
        if ($reviewExitCode -ne 0) {
            Add-Result "Artifact" "Review script completed" "FAIL" "Review script exited with $reviewExitCode."
        } else {
            Add-Result "Artifact" "Review script completed" "PASS" "Artifact reviewed without external connection."
        }

        if ([string]::IsNullOrWhiteSpace($ReviewReportFile)) {
            $ReviewReportFile = "artifacts/readiness/phase7c-gbpusd-market-hours-snapshot-review.json"
        }
    }
}

if (-not [string]::IsNullOrWhiteSpace($ReviewReportFile)) {
    $review = Read-JsonForGate $ReviewReportFile "Review"
    if ($null -ne $review) {
        if ([string]$review.symbol -eq "GBPUSD" -and [string]$review.securityId -eq "4002" -and [string]$review.securityIdSource -eq "8") {
            Add-Result "Review" "GBPUSD identity" "PASS" "GBPUSD / SecurityID 4002 / SecurityIDSource 8."
        } else {
            Add-Result "Review" "GBPUSD identity" "FAIL" "Unexpected symbol/security identity."
        }

        if ($review.finalDecision -in @("PASS", "PASS_WITH_KNOWN_WARNINGS") -and $review.closureClassification -in @("CompletedWithBook", "CompletedWithEmptyBook", "FailedSafe")) {
            $status = if ($review.finalDecision -eq "PASS_WITH_KNOWN_WARNINGS") { "WARN" } else { "PASS" }
            Add-Result "Review" "Closure classification" $status "Classification $($review.closureClassification); decision $($review.finalDecision)."
        } else {
            Add-Result "Review" "Closure classification" "FAIL" "Unsafe or unknown closure decision $($review.finalDecision)."
        }

        if (-not [bool]$review.orderSubmissionAttempted -and -not [bool]$review.shadowReplaySubmitAttempted -and -not [bool]$review.tradingMutationAttempted -and -not [bool]$review.schedulerStarted -and -not [bool]$review.credentialValuesReturned -and [bool]$review.noSensitiveContent) {
            Add-Result "Review" "Unsafe flags remain false" "PASS" "No order, runtime shadow submit, mutation, scheduler, credential leakage, or sensitive content."
        } else {
            Add-Result "Review" "Unsafe flags remain false" "FAIL" "Unsafe flag found in review report."
        }
    }
}

if (-not [string]::IsNullOrWhiteSpace($EvidencePreviewFile)) {
    $preview = Read-JsonForGate $EvidencePreviewFile "EvidencePreview"
    if ($null -ne $preview) {
        if ([string]$preview.evidenceMode -eq "MarketDataOnly" -and [string]$preview.instrument -eq "GBPUSD" -and [string]$preview.securityId -eq "4002" -and [bool]$preview.noSensitiveContent) {
            Add-Result "EvidencePreview" "MarketDataOnly GBPUSD preview" "PASS" "GBPUSD MarketDataOnly / 4002."
        } else {
            Add-Result "EvidencePreview" "MarketDataOnly GBPUSD preview" "FAIL" "Unexpected evidence preview identity or sensitivity flag."
        }

        $nonMarketArraysEmpty = @($preview.executionReports).Count -eq 0 -and @($preview.orderStatuses).Count -eq 0 -and @($preview.tradeCaptureReports).Count -eq 0 -and @($preview.protocolRejects).Count -eq 0
        if ($nonMarketArraysEmpty -and -not [bool]$preview.shadowReplaySubmitAttempted -and -not [bool]$preview.tradingMutationAttempted -and -not [bool]$preview.orderSubmissionAttempted) {
            Add-Result "EvidencePreview" "No non-market-data replay/order payloads" "PASS" "Execution/order/trade/reject arrays empty; no runtime submit or mutation flags."
        } else {
            Add-Result "EvidencePreview" "No non-market-data replay/order payloads" "FAIL" "Unsafe evidence preview payload or flag found."
        }
    }
}

if (-not [string]::IsNullOrWhiteSpace($ReplayReportFile)) {
    $replay = Read-JsonForGate $ReplayReportFile "ReplayReport"
    if ($null -ne $replay) {
        if ([string]$replay.finalDecision -eq "PASS" -and [string]$replay.replayStatus -eq "Completed" -and [int]$replay.observationCount -eq 0 -and [string]$replay.mutationGuard -eq "Unchanged") {
            Add-Result "ReplayReport" "Manual replay result" "PASS" "Completed; zero observations; mutation unchanged."
        } else {
            Add-Result "ReplayReport" "Manual replay result" "FAIL" "Replay report is not clean."
        }
    }
}

if (-not [string]::IsNullOrWhiteSpace($ClosureManifestFile)) {
    $closure = Read-JsonForGate $ClosureManifestFile "ClosureManifest"
    if ($null -ne $closure) {
        if ([string]$closure.symbol -eq "GBPUSD" -and [string]$closure.securityId -eq "4002" -and $closure.finalClosureDecision -in @("PASS", "PASS_WITH_KNOWN_WARNINGS")) {
            $status = if ($closure.finalClosureDecision -eq "PASS_WITH_KNOWN_WARNINGS") { "WARN" } else { "PASS" }
            Add-Result "ClosureManifest" "Final closure decision" $status "Closure decision $($closure.finalClosureDecision)."
        } else {
            Add-Result "ClosureManifest" "Final closure decision" "FAIL" "Unexpected closure manifest identity or decision."
        }

        if (-not [bool]$closure.orderSubmissionAttempted -and -not [bool]$closure.shadowReplaySubmitAttempted -and -not [bool]$closure.tradingMutationAttempted -and -not [bool]$closure.schedulerStarted -and [bool]$closure.noSensitiveContent) {
            Add-Result "ClosureManifest" "Unsafe flags remain false" "PASS" "Closure manifest is non-executable and sanitized."
        } else {
            Add-Result "ClosureManifest" "Unsafe flags remain false" "FAIL" "Unsafe closure manifest flag found."
        }
    }
}

$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"
$startupFiles = @($apiProgram, $workerProgram)
$startupText = ($startupFiles | Where-Object { Test-Path -LiteralPath $_ } | ForEach-Object { Get-Content -Raw -LiteralPath $_ }) -join "`n"

if ($startupText.Contains("FakeLmaxGateway") -and -not ($startupText.Contains("RealLmaxGateway") -or $startupText.Contains("LmaxVenueGatewaySkeleton"))) {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No real gateway registration found."
} else {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" "Unexpected gateway registration marker."
}

foreach ($scan in @(
    @{ category = "Scheduler"; check = "No scheduler/polling added"; patterns = @("PeriodicTimer", "System.Threading.Timer", "LmaxScheduler", "SecurityListPolling", "MarketDataPolling", "SnapshotPolling") },
    @{ category = "Replay"; check = "Runtime still does not submit to shadow replay"; patterns = @("SubmitToShadowReplay = true", "SubmittedToShadowReplay = true", "ReplaySubmitAsync") },
    @{ category = "Orders"; check = "No order surface"; patterns = @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "OrderStatusRequest", "TradeCaptureReportRequest", "SubmitOrder") },
    @{ category = "Mutation"; check = "No trading-state mutation references"; patterns = @("PersistTrade", "TradingState", "IOrderRepository", "IFillRepository", "IPositionRepository", "PersistLiveFix") }
)) {
    $hits = Get-Hits $startupFiles $scan.patterns
    if ($hits.Count -eq 0) {
        Add-Result $scan.category $scan.check "PASS" "No marker found in API/Worker startup."
    } else {
        Add-Result $scan.category $scan.check "FAIL" (($hits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
    }
}

Add-Result "Runtime" "External LMAX connection" "PASS" "This gate does not connect to LMAX."
Add-Result "Snapshot" "MarketData snapshot" "PASS" "This gate does not request snapshots."
Add-Result "SecurityList" "SecurityListRequest" "PASS" "This gate does not request SecurityList."
Add-Result "Replay" "Automatic replay" "PASS" "This gate does not replay evidence."
Add-Result "Credentials" "Credential values" "PASS" "This gate does not require or print credential values."

$failed = @($results | Where-Object status -eq "FAIL")
$warnings = @($results | Where-Object status -eq "WARN")
$decision = if ($failed.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS_WITH_KNOWN_WARNINGS" } else { "PASS" }

$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7c-gbpusd-closure-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7C"
    finalDecision = $decision
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
