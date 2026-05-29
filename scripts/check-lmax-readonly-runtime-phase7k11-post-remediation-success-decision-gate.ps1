param(
    [string]$DecisionGateFile = "artifacts/readiness/phase7k11-post-remediation-success-decision-gate.json",
    [string]$DecisionNoteFile = "artifacts/readiness/phase7k11-post-remediation-success-decision-note.md"
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

Write-Host "LMAX Read-Only Runtime Phase 7K11 Post-Remediation Success Decision Gate"
Write-Host "Local-only. This gate does not connect to LMAX, request snapshots, replay evidence, schedule work, or use credentials."

$gateRaw = Read-TextSafe $DecisionGateFile "DecisionGate"
$noteRaw = Read-TextSafe $DecisionNoteFile "DecisionNote"
$gate = $null

if ($null -ne $gateRaw) {
    $gate = $gateRaw | ConvertFrom-Json
    Assert-Equals $gate.phase "7K11" "DecisionGate" "Phase"
    Assert-True $gate.postRemediationKnownGoodControlRecovered "DecisionGate" "Post-remediation known-good control recovered"
    Assert-Equals $gate.knownGoodControlInstrument "GBPUSD" "DecisionGate" "Known-good control instrument"
    Assert-Equals $gate.knownGoodControlSecurityId "4002" "DecisionGate" "Known-good control SecurityID"
    Assert-Equals $gate.knownGoodControlSnapshotStatus "Completed" "DecisionGate" "Known-good control status"
    Assert-Equals $gate.knownGoodControlEvidenceMode "MarketDataOnly" "DecisionGate" "Known-good control evidence mode"
    Assert-Equals $gate.knownGoodControlEvidenceValidation "Ok" "DecisionGate" "Known-good control evidence validation"
    Assert-Equals $gate.localReplayNotRunReason "LocalApiHealthTimeoutOptionalReplay" "DecisionGate" "Local replay not run reason"
    Assert-True $gate.sessionLayerRecoveredForKnownGoodControl "DecisionGate" "Session layer recovered for known-good control"

    Assert-Equals $gate.selectedFutureAttemptInstrument "AUDUSD" "DecisionGate" "Selected future attempt instrument"
    Assert-Equals $gate.selectedFutureAttemptType "PostRemediationAdditionalInstrumentRetry" "DecisionGate" "Selected future attempt type"
    Assert-Equals $gate.selectedInstrumentSecurityId "4007" "DecisionGate" "Selected instrument SecurityID"
    Assert-Equals $gate.selectedInstrumentSecurityIdSource "8" "DecisionGate" "Selected instrument SecurityIDSource"
    Assert-Equals $gate.selectedInstrumentEnvironment "Demo" "DecisionGate" "Selected environment"
    Assert-Equals $gate.selectedInstrumentVenueProfile "DemoLondon" "DecisionGate" "Selected venue profile"
    Assert-Equals $gate.selectedInstrumentRequestMode "SnapshotPlusUpdates" "DecisionGate" "Selected request mode"
    Assert-Equals $gate.selectedInstrumentSymbolEncodingMode "SecurityIdOnly" "DecisionGate" "Selected encoding mode"
    if ([int]$gate.selectedInstrumentMarketDepth -eq 1) { Add-Result "DecisionGate" "Selected market depth" "PASS" "1." } else { Add-Result "DecisionGate" "Selected market depth" "FAIL" "Expected 1." }
    Assert-Equals $gate.selectedInstrumentFinalPreRunGateDecision "PASS" "DecisionGate" "Selected final pre-run gate decision"
    Assert-True $gate.exactlyOneFutureCandidateSelected "DecisionGate" "Exactly one future candidate selected"
    Assert-True $gate.futureExternalRunCanBeConsidered "DecisionGate" "Future external run can be considered"
    Assert-True $gate.usdJpyRemainsParked "DecisionGate" "USDJPY remains parked"

    foreach ($flag in @(
        "directRunAuthorization",
        "anyInstrumentExternalRunAllowed",
        "externalAdditionalInstrumentAttemptsCurrentlyAllowed",
        "externalRunAttemptedInThisPhase",
        "snapshotRunInThisPhase",
        "replayRunInThisPhase",
        "audusdRunInThisPhase",
        "usdjpyRunInThisPhase",
        "gbpUsdRunInThisPhase",
        "eurGbpRunInThisPhase",
        "batchExecutionAllowed",
        "automaticRetryRecommended",
        "wrapperValidationWeakened",
        "securityIdSwitchRecommended",
        "tokyo600xSwitchRecommended",
        "orderPathEnabled",
        "schedulerOrPollingEnabled",
        "runtimeShadowReplaySubmitEnabled",
        "tradingMutationEnabled",
        "gatewayRegistrationEnabled"
    )) {
        Assert-False $gate.$flag "DecisionGate" $flag
    }

    Assert-Equals $gate.allowedNextPhase "Phase 7K12 - AUDUSD Post-Remediation One-Instrument Manual Snapshot Attempt" "DecisionGate" "Allowed next phase"
    Assert-Equals $gate.finalDecision "PASS_POST_REMEDIATION_SUCCESS_DECISION_RECORDED" "DecisionGate" "Final decision"
    if ([bool]$gate.noSensitiveContent) { Add-Result "DecisionGate" "noSensitiveContent" "PASS" "true." } else { Add-Result "DecisionGate" "noSensitiveContent" "FAIL" "Expected true." }

    foreach ($flag in @("-AllowExternalConnections", "-ConfirmDemoReadOnly", "human-provided -Reason")) {
        if (@($gate.requiredFutureOperatorFlags) -contains $flag) { Add-Result "DecisionGate" "Required future flag: $flag" "PASS" "Present." } else { Add-Result "DecisionGate" "Required future flag: $flag" "FAIL" "Missing." }
    }

    $disallowedText = (@($gate.disallowedActions) -join "`n").ToLowerInvariant()
    foreach ($marker in @(
        "no external run in phase 7k11",
        "no audusd run in phase 7k11",
        "no usdjpy retry",
        "no gbpusd rerun",
        "no eurgbp control run",
        "no batch",
        "no loop",
        "no automatic retry",
        "no wrapper relaxation",
        "no securityid switch",
        "no tokyo 600x switch"
    )) {
        if ($disallowedText.Contains($marker)) { Add-Result "DecisionGate" "Disallowed action: $marker" "PASS" "Present." } else { Add-Result "DecisionGate" "Disallowed action: $marker" "FAIL" "Missing." }
    }
}

if ($null -ne $noteRaw) {
    foreach ($marker in @("GBPUSD post-remediation known-good control recovered successfully", "AUDUSD is selected", "USDJPY remains parked", "No run is authorized in Phase 7K11", "Phase 7K12")) {
        if ($noteRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "DecisionNote" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "DecisionNote" "Marker: $marker" "FAIL" "Marker missing." }
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
$decision = if ($failed.Count -gt 0) { "FAIL" } else { "PASS_POST_REMEDIATION_SUCCESS_DECISION_RECORDED" }
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7k11-post-remediation-success-decision-gate-validation.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7K11"
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
    selectedFutureAttemptInstrument = if ($null -ne $gate) { $gate.selectedFutureAttemptInstrument } else { $null }
    results = $results
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }
