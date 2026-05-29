param(
    [string]$RemediationPlanFile = "artifacts/readiness/phase7k7-external-session-remediation-plan.json",
    [string]$RemediationGateFile = "artifacts/readiness/phase7k7-external-session-remediation-gate.json",
    [string]$MarkdownPlanFile = "artifacts/readiness/phase7k7-external-session-remediation-plan.md"
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
    $safe = $raw -replace 'credential|Credential|secrets|secret','SAFE_METADATA'
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

Write-Host "LMAX Read-Only Runtime Phase 7K7 External Session Remediation Gate"
Write-Host "Local-only. This gate does not connect to LMAX, request snapshots, replay evidence, schedule work, or use credentials."

$planRaw = Read-TextSafe $RemediationPlanFile "RemediationPlan"
$gateRaw = Read-TextSafe $RemediationGateFile "RemediationGate"
$markdownRaw = Read-TextSafe $MarkdownPlanFile "MarkdownPlan"

if ($null -ne $planRaw) {
    $plan = $planRaw | ConvertFrom-Json
    if ([string]$plan.phase -eq "7K7") { Add-Result "RemediationPlan" "Phase" "PASS" "7K7." } else { Add-Result "RemediationPlan" "Phase" "FAIL" "Expected 7K7." }
    if ([string]$plan.remediationScope -eq "ExternalSessionEndpointEnvironment" -and [string]$plan.priorFreezeReason -eq "KnownGoodControlFailedBeforeLogonAfterPriorSuccess") { Add-Result "RemediationPlan" "Scope/reason" "PASS" "$($plan.remediationScope) / $($plan.priorFreezeReason)" } else { Add-Result "RemediationPlan" "Scope/reason" "FAIL" "Unexpected scope or reason." }
    if ([bool]$plan.globalExternalAttemptFreezeRemains -and -not [bool]$plan.externalRunAttemptedInThisPhase -and -not [bool]$plan.snapshotRunInThisPhase -and -not [bool]$plan.replayRunInThisPhase -and -not [bool]$plan.anyInstrumentRunInThisPhase) {
        Add-Result "RemediationPlan" "Freeze remains and no execution" "PASS" "All execution flags false."
    } else {
        Add-Result "RemediationPlan" "Freeze remains and no execution" "FAIL" "Unexpected execution or freeze flag."
    }
    if (-not [bool]$plan.runtimePowerAdded -and [bool]$plan.apiWorkerRemainFakeLmaxGatewayOnly) { Add-Result "RemediationPlan" "No runtime power/API Worker fake only" "PASS" "Safe runtime flags." } else { Add-Result "RemediationPlan" "No runtime power/API Worker fake only" "FAIL" "Unsafe runtime flag." }
    if ([bool]$plan.securityIdIssueNotProven -and [bool]$plan.tokyo600xNotJustified -and [bool]$plan.marketDataRequestRejectNotObserved) { Add-Result "RemediationPlan" "No SecurityID/Tokyo/reject overclaim" "PASS" "Safe classification." } else { Add-Result "RemediationPlan" "No SecurityID/Tokyo/reject overclaim" "FAIL" "Unexpected classification." }
    if (@($plan.remediationChecklist).Count -ge 12 -and @($plan.requiredEvidenceBeforeAnyFutureRun).Count -ge 6 -and @($plan.recommendedOrderOfOperations).Count -ge 5) { Add-Result "RemediationPlan" "Checklist/evidence/order populated" "PASS" "Remediation plan has required sections." } else { Add-Result "RemediationPlan" "Checklist/evidence/order populated" "FAIL" "Missing remediation sections." }
    if ([string]$plan.finalDecision -eq "PASS_REMEDIATION_PLAN_RECORDED") { Add-Result "RemediationPlan" "Final decision" "PASS" $plan.finalDecision } else { Add-Result "RemediationPlan" "Final decision" "FAIL" "Expected PASS_REMEDIATION_PLAN_RECORDED." }
}

if ($null -ne $gateRaw) {
    $gate = $gateRaw | ConvertFrom-Json
    if ([string]$gate.phase -eq "7K7") { Add-Result "RemediationGate" "Phase" "PASS" "7K7." } else { Add-Result "RemediationGate" "Phase" "FAIL" "Expected 7K7." }
    if ([bool]$gate.globalExternalAttemptFreezeRemains -and -not [bool]$gate.anyInstrumentExternalRunAllowed -and -not [bool]$gate.externalAdditionalInstrumentAttemptsCurrentlyAllowed -and -not [bool]$gate.gbpusdControlRunAllowed -and -not [bool]$gate.eurgbpControlRunAllowed -and -not [bool]$gate.audusdRetryAllowed -and -not [bool]$gate.usdjpyRetryAllowed -and -not [bool]$gate.nextInstrumentRunAllowed -and -not [bool]$gate.futureExternalRunCanBeConsidered) {
        Add-Result "RemediationGate" "Freeze remains / all runs blocked" "PASS" "All future run permissions false."
    } else {
        Add-Result "RemediationGate" "Freeze remains / all runs blocked" "FAIL" "Unexpected run permission."
    }
    if (-not [bool]$gate.remediationCompletionRecorded -and -not [bool]$gate.freezeLifted -and -not [bool]$gate.directRunAuthorization) { Add-Result "RemediationGate" "No completion/freeze lift/direct authorization" "PASS" "All false." } else { Add-Result "RemediationGate" "No completion/freeze lift/direct authorization" "FAIL" "Unexpected gate lift flag." }
    if (-not [bool]$gate.automaticRetryRecommended -and -not [bool]$gate.batchExecutionAllowed -and -not [bool]$gate.wrapperValidationWeakened -and -not [bool]$gate.securityIdSwitchRecommended -and -not [bool]$gate.tokyo600xSwitchRecommended) {
        Add-Result "RemediationGate" "No retry/batch/wrapper/security switch" "PASS" "All false."
    } else {
        Add-Result "RemediationGate" "No retry/batch/wrapper/security switch" "FAIL" "Unsafe recommendation flag."
    }
    if (-not [bool]$gate.orderPathEnabled -and -not [bool]$gate.schedulerOrPollingEnabled -and -not [bool]$gate.runtimeShadowReplaySubmitEnabled -and -not [bool]$gate.tradingMutationEnabled -and -not [bool]$gate.gatewayRegistrationEnabled) {
        Add-Result "RemediationGate" "No runtime power enabled" "PASS" "All runtime power flags false."
    } else {
        Add-Result "RemediationGate" "No runtime power enabled" "FAIL" "Unexpected runtime power flag."
    }
    if ([string]$gate.finalDecision -eq "PASS_REMEDIATION_PLAN_RECORDED") { Add-Result "RemediationGate" "Final decision" "PASS" $gate.finalDecision } else { Add-Result "RemediationGate" "Final decision" "FAIL" "Expected PASS_REMEDIATION_PLAN_RECORDED." }
    if ([string]$gate.allowedNextPhase -eq "Phase 7K8 - Record External Session Remediation Completion, No External Run") { Add-Result "RemediationGate" "Allowed next phase" "PASS" $gate.allowedNextPhase } else { Add-Result "RemediationGate" "Allowed next phase" "FAIL" "Unexpected allowed next phase." }
}

if ($null -ne $markdownRaw) {
    foreach ($marker in @("Earlier GBPUSD and EURGBP successes", "current operational state is frozen", "No future run is allowed", "known-good control", "Phase 7K8")) {
        if ($markdownRaw.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "MarkdownPlan" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "MarkdownPlan" "Marker: $marker" "FAIL" "Marker missing." }
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
Add-Result "Credentials" "Credential values" "PASS" "This gate does not require or print credential values."

$failed = @($results | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } else { "PASS_REMEDIATION_PLAN_RECORDED" }
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7k7-external-session-remediation-gate-validation.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7K7"
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
