param(
    [string]$EurgbpReadinessFile = "",
    [string]$Phase7DDecisionFile = "",
    [string]$PipelineManifestFile = "",
    [string]$PlanningManifestFile = "",
    [string]$SafetyGateManifestFile = "",
    [string]$PreflightManifestFile = ""
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

Write-Host "LMAX Read-Only Runtime Phase 7E2 EURGBP Readiness Gate"
Write-Host "Local-only. This gate does not connect to LMAX, call external APIs, request SecurityList, request snapshots, replay evidence, schedule work, or use credentials."

foreach ($required in @(
    @{ name = "Phase 7E2 readiness model"; path = "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydration.cs" },
    @{ name = "Phase 7E2 rehydration script"; path = "scripts/rehydrate-lmax-readonly-eurgbp-manual-snapshot-readiness.ps1" },
    @{ name = "Phase 7E2 tests"; path = "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationTests.cs" }
)) {
    $path = Join-Path $repoRoot $required.path
    if (Test-Path -LiteralPath $path) {
        Add-Result "Files" "$($required.name) exists" "PASS" $path
    } else {
        Add-Result "Files" "$($required.name) exists" "FAIL" "Missing $path"
    }
}

if (-not [string]::IsNullOrWhiteSpace($Phase7DDecisionFile)) {
    $decision = Read-JsonForGate $Phase7DDecisionFile "Phase7DDecision"
    if ($null -ne $decision) {
        if ([string]$decision.decision -eq "ProceedToEurgbpPlanning" -and [string]$decision.nextCandidateInstrument -eq "EURGBP") {
            Add-Result "Phase7DDecision" "Proceed to EURGBP" "PASS" "Phase 7D selected EURGBP after GBPUSD closure."
        } else {
            Add-Result "Phase7DDecision" "Proceed to EURGBP" "FAIL" "Decision $($decision.decision), next $($decision.nextCandidateInstrument)."
        }
    }
}

if (-not [string]::IsNullOrWhiteSpace($PipelineManifestFile)) {
    $pipeline = Read-JsonForGate $PipelineManifestFile "PipelineManifest"
    if ($null -ne $pipeline) {
        $eurgbp = @($pipeline.instruments | Where-Object { [string]$_.symbol -eq "EURGBP" })[0]
        if ($null -ne $eurgbp -and [string]$eurgbp.planningSecurityId -eq "4003" -and [string]$eurgbp.securityIdSource -eq "8" -and [string]$eurgbp.finalReadinessDecision -eq "PASS") {
            Add-Result "PipelineManifest" "EURGBP pipeline ready" "PASS" "EURGBP / 4003 has final readiness PASS."
        } else {
            Add-Result "PipelineManifest" "EURGBP pipeline ready" "FAIL" "EURGBP planning pipeline missing or not PASS."
        }

        if ([int]$pipeline.executableCount -eq 0) {
            Add-Result "PipelineManifest" "Executable count zero" "PASS" "Pipeline executableCount=0."
        } else {
            Add-Result "PipelineManifest" "Executable count zero" "FAIL" "Pipeline executableCount=$($pipeline.executableCount)."
        }
    }
}

foreach ($manifest in @(
    @{ label = "PlanningManifest"; path = $PlanningManifestFile; collection = "instruments"; decision = "AcceptedForPlanning" },
    @{ label = "SafetyGateManifest"; path = $SafetyGateManifestFile; collection = "instruments"; decision = "PASS" },
    @{ label = "PreflightManifest"; path = $PreflightManifestFile; collection = "results"; decision = "PASS" }
)) {
    if ([string]::IsNullOrWhiteSpace($manifest.path)) { continue }
    $json = Read-JsonForGate $manifest.path $manifest.label
    if ($null -eq $json) { continue }
    $items = $json.($manifest.collection)
    $eurgbp = @($items | Where-Object { [string]$_.symbol -eq "EURGBP" })[0]
    if ($null -eq $eurgbp) {
        Add-Result $manifest.label "EURGBP present" "FAIL" "EURGBP missing."
        continue
    }

    $decisionValue = if ($manifest.label -eq "PlanningManifest") { [string]$eurgbp.decision } else { [string]$eurgbp.finalDecision }
    $securitySourceOk = if ($manifest.label -eq "PreflightManifest") { $true } else { [string]$eurgbp.securityIdSource -eq "8" }
    if ([string]$eurgbp.planningSecurityId -eq "4003" -and $securitySourceOk -and $decisionValue -eq $manifest.decision) {
        Add-Result $manifest.label "EURGBP safe value" "PASS" "EURGBP / 4003 / source 8 / $decisionValue."
    } else {
        Add-Result $manifest.label "EURGBP safe value" "FAIL" "Unexpected EURGBP value or decision."
    }
}

if ([string]::IsNullOrWhiteSpace($EurgbpReadinessFile)) {
    Add-Result "Readiness" "EURGBP readiness supplied" "WARN" "No EURGBP readiness artifact supplied; source checks only."
} else {
    $readiness = Read-JsonForGate $EurgbpReadinessFile "Readiness"
    if ($null -ne $readiness) {
        if ([string]$readiness.selectedInstrument -eq "EURGBP" -and [string]$readiness.securityId -eq "4003" -and [string]$readiness.securityIdSource -eq "8") {
            Add-Result "Readiness" "EURGBP identity" "PASS" "EURGBP / SecurityID 4003 / source 8."
        } else {
            Add-Result "Readiness" "EURGBP identity" "FAIL" "Unexpected selected instrument/security identity."
        }

        if ([string]$readiness.previousDecision -eq "ProceedToEurgbpPlanning" -and [string]$readiness.previousInstrumentClosureDecision -eq "PASS") {
            Add-Result "Readiness" "Phase 7D bridge" "PASS" "GBPUSD closure PASS bridged to EURGBP planning."
        } else {
            Add-Result "Readiness" "Phase 7D bridge" "FAIL" "Previous decision/closure was not safe proceed."
        }

        if ([bool]$readiness.oneInstrumentAtATime -and -not [bool]$readiness.batchExecutionAllowed -and [int]$readiness.executableCount -eq 0) {
            Add-Result "Readiness" "Manual sequencing" "PASS" "One instrument at a time; batch disabled; executableCount=0."
        } else {
            Add-Result "Readiness" "Manual sequencing" "FAIL" "Manual sequencing flags invalid."
        }

        if (-not [bool]$readiness.isApprovedForExternalRun -and -not [bool]$readiness.canRunExternalSnapshot -and -not [bool]$readiness.eligibleForManualSnapshotAttempt) {
            Add-Result "Readiness" "Run eligibility false" "PASS" "EURGBP remains non-executable."
        } else {
            Add-Result "Readiness" "Run eligibility false" "FAIL" "Run eligibility flag is true."
        }

        if (-not [bool]$readiness.externalConnectionAttempted -and -not [bool]$readiness.snapshotAttempted -and -not [bool]$readiness.replayAttempted -and -not [bool]$readiness.orderSubmissionAttempted -and -not [bool]$readiness.shadowReplaySubmitAttempted -and -not [bool]$readiness.tradingMutationAttempted -and -not [bool]$readiness.schedulerStarted -and [bool]$readiness.noSensitiveContent) {
            Add-Result "Readiness" "Attempt and mutation flags false" "PASS" "No attempt, replay, scheduler, order, mutation, or sensitive content."
        } else {
            Add-Result "Readiness" "Attempt and mutation flags false" "FAIL" "Unsafe attempt/mutation/sensitivity flag found."
        }

        if ([string]$readiness.finalDecision -eq "PASS") {
            Add-Result "Readiness" "Final decision" "PASS" "EURGBP readiness rehydration PASS."
        } else {
            Add-Result "Readiness" "Final decision" "FAIL" "Final decision $($readiness.finalDecision)."
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
$outPath = Join-Path $outDir "phase7e2-eurgbp-readiness-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7E2"
    finalDecision = $decision
    selectedInstrument = "EURGBP"
    securityId = "4003"
    canRunExternalSnapshot = $false
    isApprovedForExternalRun = $false
    eligibleForManualSnapshotAttempt = $false
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
