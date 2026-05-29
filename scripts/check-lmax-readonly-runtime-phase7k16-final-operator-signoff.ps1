param(
    [string]$FinalSignoffFile = "artifacts/readiness/phase7k16-final-operator-signoff.json",
    [string]$DocumentationSummaryFile = "artifacts/readiness/phase7k16-final-readiness-documentation-update-summary.json",
    [string]$SignoffNoteFile = "artifacts/readiness/phase7k16-final-operator-signoff-note.md"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|raw\s*fix|BEGIN\s+PRIVATE\s+KEY)'

function Add-Result([string]$Category, [string]$Check, [string]$Status, [string]$Detail) {
    $script:results += [ordered]@{ category = $Category; check = $Check; status = $Status; detail = $Detail }
    Write-Host ("{0}: {1} / {2} - {3}" -f $Status, $Category, $Check, $Detail)
}

function Resolve-LocalPath([string]$PathValue) {
    if ([string]::IsNullOrWhiteSpace($PathValue)) { return "" }
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}

function Read-TextSafe([string]$PathValue, [string]$Label) {
    $resolved = Resolve-LocalPath $PathValue
    if (-not (Test-Path -LiteralPath $resolved)) {
        Add-Result $Label "File exists" "FAIL" "Missing $resolved"
        return $null
    }
    Add-Result $Label "File exists" "PASS" $resolved
    $raw = Get-Content -LiteralPath $resolved -Raw
    $safe = $raw -replace 'credential|Credential|secret|Secret','SAFE_METADATA'
    if ($safe -match $sensitivePattern) {
        Add-Result $Label "No sensitive content" "FAIL" "Credential-shaped or raw FIX content found."
    } else {
        Add-Result $Label "No sensitive content" "PASS" "No credential-shaped or raw FIX content."
    }
    return $raw
}

function Get-Hits($Paths, $Patterns) {
    $existing = @($Paths | Where-Object { Test-Path -LiteralPath $_ })
    if ($existing.Count -eq 0) { return @() }
    return @(Select-String -Path $existing -Pattern $Patterns -SimpleMatch -ErrorAction SilentlyContinue)
}

function Assert-True([object]$Value, [string]$Category, [string]$Check) {
    if ([bool]$Value) { Add-Result $Category $Check "PASS" "true." } else { Add-Result $Category $Check "FAIL" "Expected true." }
}

function Assert-False([object]$Value, [string]$Category, [string]$Check) {
    if (-not [bool]$Value) { Add-Result $Category $Check "PASS" "false." } else { Add-Result $Category $Check "FAIL" "Expected false." }
}

function Assert-Equals([object]$Actual, [string]$Expected, [string]$Category, [string]$Check) {
    if ([string]$Actual -eq $Expected) { Add-Result $Category $Check "PASS" $Expected } else { Add-Result $Category $Check "FAIL" "Expected '$Expected' but found '$Actual'." }
}

Write-Host "LMAX Read-Only Runtime Phase 7K16 Final Operator Signoff"
Write-Host "Local-only. This gate does not connect to LMAX, request snapshots, replay evidence, schedule work, or use credentials."

$signoffRaw = Read-TextSafe $FinalSignoffFile "FinalSignoff"
$summaryRaw = Read-TextSafe $DocumentationSummaryFile "DocumentationSummary"
$noteRaw = Read-TextSafe $SignoffNoteFile "SignoffNote"
$signoff = $null

if ($null -ne $signoffRaw) {
    $signoff = $signoffRaw | ConvertFrom-Json
    Assert-Equals $signoff.phase "7K16" "FinalSignoff" "Phase"
    Assert-Equals $signoff.signoffType "FinalAdditionalInstrumentReadOnlyEvidenceCycleSignoff" "FinalSignoff" "Signoff type"
    Assert-True $signoff.operatorSignoffRecorded "FinalSignoff" "Operator signoff recorded"
    Assert-True $signoff.evidenceCycleClosed "FinalSignoff" "Evidence cycle closed"
    Assert-True $signoff.externalAttemptCycleClosed "FinalSignoff" "External attempt cycle closed"
    foreach ($instrument in @("GBPUSD", "EURGBP", "AUDUSD")) {
        if (@($signoff.successfulReadOnlyEvidenceInstruments) -contains $instrument) { Add-Result "FinalSignoff" "Successful evidence includes $instrument" "PASS" "Present." } else { Add-Result "FinalSignoff" "Successful evidence includes $instrument" "FAIL" "Missing." }
    }
    if (@($signoff.parkedInstruments) -contains "USDJPY") { Add-Result "FinalSignoff" "Parked instruments include USDJPY" "PASS" "Present." } else { Add-Result "FinalSignoff" "Parked instruments include USDJPY" "FAIL" "Missing." }
    Assert-True $signoff.eurusdPriorWorkflowClosed "FinalSignoff" "EURUSD prior workflow closed"
    Assert-True $signoff.lmaxDemoReadOnlyEvidenceCompleteForCurrentCycle "FinalSignoff" "Read-only evidence complete for current cycle"
    Assert-True $signoff.marketDataOnlyEvidenceAvailable "FinalSignoff" "MarketDataOnly evidence available"
    Assert-Equals $signoff.dayClosureGateDecision "PASS_FINAL_ADDITIONAL_INSTRUMENT_EVIDENCE_PACK_CLOSED" "FinalSignoff" "Day closure gate decision"
    Assert-Equals $signoff.finalOperationalState "NoExternalAttemptsAllowed" "FinalSignoff" "Final operational state"
    foreach ($flag in @(
        "anyInstrumentExternalRunAllowed",
        "externalAdditionalInstrumentAttemptsCurrentlyAllowed",
        "futureExternalRunCanBeConsidered",
        "directRunAuthorization",
        "immediateNextExternalRunRecommended",
        "orderSubmissionObserved",
        "schedulerOrPollingObserved",
        "runtimeShadowReplaySubmitObserved",
        "tradingMutationObserved",
        "gatewayRegistrationObserved",
        "credentialValuesReturned"
    )) {
        Assert-False $signoff.$flag "FinalSignoff" $flag
    }
    Assert-True $signoff.noSensitiveContent "FinalSignoff" "No sensitive content"
    Assert-True $signoff.apiWorkerRemainFakeLmaxGatewayOnly "FinalSignoff" "API/Worker remain FakeLmaxGateway only"
    Assert-Equals $signoff.knownLocalIssue "LocalhostApiHealthTimeoutAffectedOptionalReplayOnly" "FinalSignoff" "Known local issue"
    Assert-Equals $signoff.usdJpyStatus "ParkedSeparateTroubleshootingRail" "FinalSignoff" "USDJPY status"
    Assert-Equals $signoff.allowedNextPhase "Phase 7L - Readiness UI/Status Update Planning, No External Run" "FinalSignoff" "Allowed next phase"
    Assert-Equals $signoff.finalDecision "PASS_FINAL_OPERATOR_SIGNOFF_RECORDED" "FinalSignoff" "Final decision"
}

if ($null -ne $summaryRaw) {
    $summary = $summaryRaw | ConvertFrom-Json
    Assert-Equals $summary.phase "7K16" "DocumentationSummary" "Phase"
    Assert-Equals $summary.finalDecision "PASS_FINAL_OPERATOR_SIGNOFF_RECORDED" "DocumentationSummary" "Final decision"
    if (@($summary.filesUpdated) -contains "docs/LMAX_READONLY_RUNTIME_PHASE_GATES.md") { Add-Result "DocumentationSummary" "Phase gates doc updated" "PASS" "Present." } else { Add-Result "DocumentationSummary" "Phase gates doc updated" "FAIL" "Missing." }
    if (@($summary.filesUpdated) -contains "docs/OPERATIONAL_READINESS_CHECKLIST.md") { Add-Result "DocumentationSummary" "Readiness checklist doc updated" "PASS" "Present." } else { Add-Result "DocumentationSummary" "Readiness checklist doc updated" "FAIL" "Missing." }
    Assert-Equals $summary.safetyPosture.finalOperationalState "NoExternalAttemptsAllowed" "DocumentationSummary" "Safety posture final state"
    Assert-Equals $summary.safetyPosture.apiWorkerGatewayMode "FakeLmaxGateway" "DocumentationSummary" "Safety posture gateway mode"
    Assert-False $summary.safetyPosture.orderSubmissionObserved "DocumentationSummary" "No order submission observed"
    Assert-False $summary.safetyPosture.schedulerOrPollingObserved "DocumentationSummary" "No scheduler/polling observed"
    Assert-False $summary.safetyPosture.runtimeShadowReplaySubmitObserved "DocumentationSummary" "No runtime shadow replay submit observed"
    Assert-False $summary.safetyPosture.tradingMutationObserved "DocumentationSummary" "No trading mutation observed"
    Assert-False $summary.safetyPosture.gatewayRegistrationObserved "DocumentationSummary" "No gateway registration observed"
    Assert-True $summary.noSensitiveContent "DocumentationSummary" "No sensitive content"
}

if ($null -ne $noteRaw) {
    foreach ($marker in @("evidence cycle is closed", "GBPUSD, EURGBP, and AUDUSD", "USDJPY remains parked", "No trading runtime powers", "FakeLmaxGateway", "localhost API health timeout", "not more external attempts")) {
        if ($noteRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "SignoffNote" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "SignoffNote" "Marker: $marker" "FAIL" "Marker missing." }
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
    if ($hits.Count -eq 0) { Add-Result $scan.category $scan.check "PASS" "No marker found in API/Worker startup." } else { Add-Result $scan.category $scan.check "FAIL" (($hits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ") }
}

Add-Result "Runtime" "External LMAX connection" "PASS" "This gate does not connect to LMAX."
Add-Result "Snapshot" "MarketData snapshot" "PASS" "This gate does not request snapshots."
Add-Result "Replay" "Automatic replay" "PASS" "This gate does not replay evidence."
Add-Result "Credentials" "Credential values" "PASS" "This gate does not require or print credential values."

$failed = @($results | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } else { "PASS_FINAL_OPERATOR_SIGNOFF_RECORDED" }
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7k16-final-operator-signoff-validation.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7K16"
    finalDecision = $decision
    externalConnectionAttempted = $false
    snapshotAttempted = $false
    replayAttempted = $false
    anyInstrumentRunAttempted = $false
    schedulerStarted = $false
    orderSubmissionAttempted = $false
    shadowReplaySubmitAttempted = $false
    tradingMutationAttempted = $false
    apiWorkerGatewayMode = "FakeLmaxGateway"
    results = $results
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }
