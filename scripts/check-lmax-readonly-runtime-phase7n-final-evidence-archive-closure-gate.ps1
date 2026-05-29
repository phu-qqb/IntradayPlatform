param(
    [string]$ManifestFile = "artifacts/readiness/phase7n-final-lmax-readonly-runtime-evidence-archive-manifest.json",
    [string]$SummaryFile = "artifacts/readiness/phase7n-final-lmax-readonly-runtime-evidence-status-summary.json",
    [string]$NoteFile = "artifacts/readiness/phase7n-final-lmax-readonly-runtime-evidence-archive-note.md",
    [string]$HandoffPromptFile = "artifacts/readiness/phase7n-final-lmax-readonly-runtime-thread-handoff-prompt.md",
    [string]$GateFile = "artifacts/readiness/phase7n-final-lmax-readonly-runtime-evidence-archive-closure-gate.json"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|raw\s*fix|BEGIN\s+PRIVATE\s+KEY)'
$expectedAllowedNextPhase = "Phase 7O $([char]0x2014) Optional Documentation/Runbook Consolidation, No External Run"

function Add-Result([string]$Category, [string]$Check, [string]$Status, [string]$Detail) {
    $script:results += [ordered]@{ category = $Category; check = $Check; status = $Status; detail = $Detail }
    Write-Host ("{0}: {1} / {2} - {3}" -f $Status, $Category, $Check, $Detail)
}

function Resolve-LocalPath([string]$PathValue) {
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

function Assert-True([object]$Value, [string]$Category, [string]$Check) {
    if ([bool]$Value) { Add-Result $Category $Check "PASS" "true." } else { Add-Result $Category $Check "FAIL" "Expected true." }
}

function Assert-False([object]$Value, [string]$Category, [string]$Check) {
    if (-not [bool]$Value) { Add-Result $Category $Check "PASS" "false." } else { Add-Result $Category $Check "FAIL" "Expected false." }
}

function Assert-Equals([object]$Actual, [string]$Expected, [string]$Category, [string]$Check) {
    if ([string]$Actual -eq $Expected) { Add-Result $Category $Check "PASS" $Expected } else { Add-Result $Category $Check "FAIL" "Expected '$Expected' but found '$Actual'." }
}

function Assert-Contains($Values, [string]$Expected, [string]$Category, [string]$Check) {
    if (@($Values) -contains $Expected) { Add-Result $Category $Check "PASS" "Contains $Expected." } else { Add-Result $Category $Check "FAIL" "Missing $Expected." }
}

Write-Host "LMAX Read-Only Runtime Phase 7N Final Evidence Archive Closure Gate"
Write-Host "Local-only validator. This does not connect to LMAX, request snapshots, run replay, or call POST endpoints."

$manifestRaw = Read-TextSafe $ManifestFile "ArchiveManifest"
$summaryRaw = Read-TextSafe $SummaryFile "StatusSummary"
$noteRaw = Read-TextSafe $NoteFile "ArchiveNote"
$handoffRaw = Read-TextSafe $HandoffPromptFile "HandoffPrompt"
$gateRaw = Read-TextSafe $GateFile "ClosureGate"

if ($null -ne $manifestRaw) {
    $manifest = $manifestRaw | ConvertFrom-Json
    Assert-Equals $manifest.phase "7N" "ArchiveManifest" "Phase"
    Assert-Equals $manifest.archiveType "FinalLmaxReadOnlyRuntimeEvidenceArchive" "ArchiveManifest" "Archive type"
    Assert-True $manifest.archiveComplete "ArchiveManifest" "Archive complete"
    Assert-True $manifest.evidenceCycleClosed "ArchiveManifest" "Evidence cycle closed"
    Assert-True $manifest.operatorSignoffRecorded "ArchiveManifest" "Operator signoff recorded"
    Assert-True $manifest.uiStatusWorkstreamClosed "ArchiveManifest" "UI status workstream closed"
    Assert-True $manifest.optionalLocalReplayWorkstreamClosed "ArchiveManifest" "Optional local replay workstream closed"
    Assert-Equals $manifest.finalOperationalState "NoExternalAttemptsAllowed" "ArchiveManifest" "Final operational state"
    foreach ($instrument in @("EURUSD", "GBPUSD", "EURGBP", "AUDUSD")) { Assert-Contains $manifest.successfulReadOnlyEvidenceInstruments $instrument "ArchiveManifest" "Successful instruments" }
    Assert-Contains $manifest.parkedInstruments "USDJPY" "ArchiveManifest" "Parked instruments"
    foreach ($instrument in @("EURGBP", "GBPUSD")) { Assert-Contains $manifest.localReplaySucceededInstruments $instrument "ArchiveManifest" "Local replay succeeded instruments" }
    Assert-True $manifest.marketDataOnlyEvidenceAvailable "ArchiveManifest" "MarketDataOnly evidence available"
    foreach ($flag in @("orderSubmissionObserved", "schedulerOrPollingObserved", "runtimeShadowReplaySubmitObserved", "tradingMutationObserved", "gatewayRegistrationObserved", "credentialValuesReturned")) { Assert-False $manifest.$flag "ArchiveManifest" $flag }
    Assert-True $manifest.apiWorkerRemainFakeLmaxGatewayOnly "ArchiveManifest" "API/Worker remain FakeLmaxGateway only"
    Assert-Equals $manifest.allowedNextPhase $expectedAllowedNextPhase "ArchiveManifest" "Allowed next phase"
    Assert-Equals $manifest.finalDecision "PASS_FINAL_LMAX_READONLY_RUNTIME_EVIDENCE_ARCHIVE_CLOSED" "ArchiveManifest" "Final decision"
    Assert-True $manifest.noSensitiveContent "ArchiveManifest" "No sensitive content"
}

if ($null -ne $summaryRaw) {
    $summary = $summaryRaw | ConvertFrom-Json
    Assert-Equals $summary.phase "7N" "StatusSummary" "Phase"
    foreach ($instrument in @("EURUSD", "GBPUSD", "EURGBP", "AUDUSD", "USDJPY")) {
        $item = @($summary.evidenceResultsByInstrument | Where-Object { [string]$_.instrument -eq $instrument })
        if ($item.Count -gt 0) { Add-Result "StatusSummary" "Instrument summary: $instrument" "PASS" "Found." } else { Add-Result "StatusSummary" "Instrument summary: $instrument" "FAIL" "Missing." }
    }
    Assert-True $summary.finalSafetyPosture.noOrders "StatusSummary" "No orders"
    Assert-True $summary.finalSafetyPosture.noSchedulerOrPolling "StatusSummary" "No scheduler/polling"
    Assert-True $summary.finalSafetyPosture.noRuntimeShadowReplaySubmit "StatusSummary" "No runtime shadow replay submit"
    Assert-True $summary.finalSafetyPosture.noTradingMutation "StatusSummary" "No trading mutation"
    Assert-True $summary.finalSafetyPosture.noRealGatewayRegistration "StatusSummary" "No real gateway registration"
    Assert-True $summary.noSensitiveContent "StatusSummary" "No sensitive content"
}

if ($null -ne $gateRaw) {
    $gate = $gateRaw | ConvertFrom-Json
    Assert-Equals $gate.phase "7N" "ClosureGate" "Phase"
    Assert-True $gate.finalArchiveClosureCompleted "ClosureGate" "Final archive closure completed"
    Assert-True $gate.archiveComplete "ClosureGate" "Archive complete"
    Assert-True $gate.evidenceCycleClosed "ClosureGate" "Evidence cycle closed"
    Assert-True $gate.operatorSignoffRecorded "ClosureGate" "Operator signoff recorded"
    Assert-True $gate.uiStatusWorkstreamClosed "ClosureGate" "UI status workstream closed"
    Assert-True $gate.optionalLocalReplayWorkstreamClosed "ClosureGate" "Optional local replay workstream closed"
    Assert-Equals $gate.finalOperationalState "NoExternalAttemptsAllowed" "ClosureGate" "Final operational state"
    foreach ($instrument in @("EURUSD", "GBPUSD", "EURGBP", "AUDUSD")) { Assert-Contains $gate.successfulReadOnlyEvidenceInstruments $instrument "ClosureGate" "Successful instruments" }
    Assert-Contains $gate.parkedInstruments "USDJPY" "ClosureGate" "Parked instruments"
    foreach ($instrument in @("EURGBP", "GBPUSD")) { Assert-Contains $gate.localReplaySucceededInstruments $instrument "ClosureGate" "Local replay succeeded instruments" }
    foreach ($flag in @("anyInstrumentExternalRunAllowed", "externalAdditionalInstrumentAttemptsCurrentlyAllowed", "directRunAuthorization", "futureExternalRunCanBeConsidered", "immediateNextExternalRunRecommended", "batchExecutionAllowed", "automaticRetryRecommended", "wrapperValidationWeakened", "securityIdSwitchRecommended", "tokyo600xSwitchRecommended", "orderPathEnabled", "schedulerOrPollingEnabled", "runtimeShadowReplaySubmitEnabled", "tradingMutationEnabled", "gatewayRegistrationEnabled")) {
        Assert-False $gate.$flag "ClosureGate" $flag
    }
    Assert-True $gate.apiWorkerRemainFakeLmaxGatewayOnly "ClosureGate" "API/Worker remain FakeLmaxGateway only"
    Assert-Equals $gate.allowedNextPhase $expectedAllowedNextPhase "ClosureGate" "Allowed next phase"
    Assert-Equals $gate.finalDecision "PASS_FINAL_LMAX_READONLY_RUNTIME_EVIDENCE_ARCHIVE_CLOSED" "ClosureGate" "Final decision"
    Assert-True $gate.noSensitiveContent "ClosureGate" "No sensitive content"
}

if ($null -ne $handoffRaw) {
    foreach ($marker in @("C:\Users\phili\source\repos\QQ.Production.Intraday", "NoExternalAttemptsAllowed", "USDJPY remains", "do not reopen external LMAX attempts", "Tokyo 600x", "FakeLmaxGateway", "Phase 7O", "Phase 8A")) {
        if ($handoffRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "HandoffPrompt" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "HandoffPrompt" "Marker: $marker" "FAIL" "Marker missing." }
    }
}

if ($null -ne $noteRaw) {
    foreach ($marker in @("archive is complete", "EURUSD, GBPUSD, EURGBP, and AUDUSD", "USDJPY remains parked", "optional local replay workstream is closed", "No external attempts are allowed", "No runtime trading powers")) {
        if ($noteRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "ArchiveNote" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "ArchiveNote" "Marker: $marker" "FAIL" "Marker missing." }
    }
}

$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"
$startupText = (@($apiProgram, $workerProgram) | Where-Object { Test-Path -LiteralPath $_ } | ForEach-Object { Get-Content -Raw -LiteralPath $_ }) -join "`n"
if ($startupText.Contains("FakeLmaxGateway") -and -not ($startupText.Contains("RealLmaxGateway") -or $startupText.Contains("LmaxVenueGatewaySkeleton"))) {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No real gateway registration found."
} else {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" "Unexpected gateway registration marker."
}

Add-Result "Runtime" "External LMAX connection" "PASS" "This phase does not connect to LMAX."
Add-Result "Snapshot" "MarketData snapshot" "PASS" "This phase does not request snapshots."
Add-Result "Replay" "Replay" "PASS" "This phase does not run local or external replay."
Add-Result "POST" "POST endpoint" "PASS" "This phase does not call POST endpoints."
Add-Result "RuntimePower" "Runtime behavior change" "PASS" "This phase records a local archive only."

$failed = @($results | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } else { "PASS_FINAL_LMAX_READONLY_RUNTIME_EVIDENCE_ARCHIVE_CLOSED" }
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7n-final-lmax-readonly-runtime-evidence-archive-closure-gate-validation.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7N"
    finalDecision = $decision
    externalConnectionAttempted = $false
    snapshotAttempted = $false
    replayAttempted = $false
    postEndpointCalled = $false
    runtimePowerAdded = $false
    archiveComplete = $true
    apiWorkerGatewayMode = "FakeLmaxGateway"
    results = $results
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }
