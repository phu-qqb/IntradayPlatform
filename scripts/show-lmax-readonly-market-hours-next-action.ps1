param(
    [string]$FinalReadinessFile = "artifacts/lmax-readonly-runtime-securityid-planning/final-readiness/lmax-readonly-gbpusd-manual-snapshot-final-readiness-20260509-165343.json",
    [string]$MarketHoursRetryReadinessFile = "artifacts/lmax-readonly-runtime-securityid-planning/market-hours-retry/lmax-readonly-gbpusd-market-hours-retry-20260509-174442.json",
    [string]$Phase6XReviewFile = "artifacts/readiness/phase6x-gbpusd-snapshot-result-review.json",
    [string]$DocumentationPackFile = "artifacts/lmax-readonly-runtime-securityid-planning/documentation-pack/lmax-readonly-additional-instruments-planning-doc-pack-20260510-132804.json",
    [string]$OutputDirectory = "artifacts/readiness",
    [switch]$WriteMarkdown
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=|raw\s*fix|sendercompid|targetcompid)'

function Resolve-LocalPath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return Join-Path $repoRoot $Path
}

function Read-SafeJson([string]$Path, [string]$Label) {
    $resolved = Resolve-LocalPath $Path
    if (-not (Test-Path -LiteralPath $resolved)) {
        throw "$Label not found: $resolved"
    }
    $raw = Get-Content -Raw -LiteralPath $resolved
    if ($raw -match $script:sensitivePattern) {
        throw "$Label contains sensitive-shaped content: $resolved"
    }
    return @{ Path = $resolved; Json = ($raw | ConvertFrom-Json) }
}

Write-Host "LMAX Market-Hours Next Action"
Write-Host "Local-only. No LMAX connection, no credentials, no snapshot, no replay, no scheduler, no orders, and no mutation."

$finalReadinessInput = Read-SafeJson $FinalReadinessFile "Final readiness"
$retryInput = Read-SafeJson $MarketHoursRetryReadinessFile "Market-hours retry readiness"
$reviewInput = Read-SafeJson $Phase6XReviewFile "Phase 6X review"
$docPackInput = Read-SafeJson $DocumentationPackFile "Documentation pack"

$finalReadiness = $finalReadinessInput.Json
$retry = $retryInput.Json
$review = $reviewInput.Json
$docPack = $docPackInput.Json
$issues = @()

if ([string]$finalReadiness.symbol -ne "GBPUSD" -or [string]$finalReadiness.planningSecurityId -ne "4002" -or [string]$finalReadiness.securityIdSource -ne "8" -or [string]$finalReadiness.readinessDecision -ne "PASS") { $issues += "FinalReadinessMismatch" }
if ([string]$retry.symbol -ne "GBPUSD" -or [string]$retry.securityId -ne "4002" -or [string]$retry.securityIdSource -ne "8" -or [string]$retry.decision -ne "PASS") { $issues += "RetryReadinessMismatch" }
if ([string]$review.status -ne "CompletedWithEmptyBook" -or [string]$review.finalDecision -ne "PASS_WITH_KNOWN_WARNINGS") { $issues += "PreviousResultNotSafeEmptyBookWarning" }
if (-not [bool]$retry.previousAttemptWasOutsideMarketHours) { $issues += "PreviousAttemptNotOutsideMarketHours" }
if ([string]$docPack.finalDecision -ne "PASS" -or [int]$docPack.executableCount -ne 0) { $issues += "DocumentationPackMismatch" }

foreach ($flag in @(
    @{ n = "final.isApprovedForExternalRun"; v = [bool]$finalReadiness.isApprovedForExternalRun },
    @{ n = "final.canRunExternalSnapshot"; v = [bool]$finalReadiness.canRunExternalSnapshot },
    @{ n = "final.eligibleForManualSnapshotAttempt"; v = [bool]$finalReadiness.eligibleForManualSnapshotAttempt },
    @{ n = "retry.canRunAutomatically"; v = [bool]$retry.canRunAutomatically },
    @{ n = "retry.schedulerStarted"; v = [bool]$retry.schedulerStarted },
    @{ n = "retry.orderSubmissionAttempted"; v = [bool]$retry.orderSubmissionAttempted },
    @{ n = "retry.shadowReplaySubmitAttempted"; v = [bool]$retry.shadowReplaySubmitAttempted },
    @{ n = "retry.tradingMutationAttempted"; v = [bool]$retry.tradingMutationAttempted },
    @{ n = "review.orderSubmissionAttempted"; v = [bool]$review.orderSubmissionAttempted },
    @{ n = "review.shadowReplaySubmitAttempted"; v = [bool]$review.shadowReplaySubmitAttempted },
    @{ n = "review.tradingMutationAttempted"; v = [bool]$review.tradingMutationAttempted },
    @{ n = "review.schedulerStarted"; v = [bool]$review.schedulerStarted },
    @{ n = "docPack.isApprovedForExternalRun"; v = [bool]$docPack.isApprovedForExternalRun },
    @{ n = "docPack.canRunExternalSnapshot"; v = [bool]$docPack.canRunExternalSnapshot },
    @{ n = "docPack.eligibleForManualSnapshotAttempt"; v = [bool]$docPack.eligibleForManualSnapshotAttempt },
    @{ n = "docPack.runtimeShadowReplaySubmit"; v = [bool]$docPack.runtimeShadowReplaySubmit },
    @{ n = "docPack.schedulerOrPolling"; v = [bool]$docPack.schedulerOrPolling },
    @{ n = "docPack.orderSubmission"; v = [bool]$docPack.orderSubmission },
    @{ n = "docPack.gatewayRegistration"; v = [bool]$docPack.gatewayRegistration },
    @{ n = "docPack.tradingMutation"; v = [bool]$docPack.tradingMutation }
)) {
    if ($flag.v) { $issues += "$($flag.n)True" }
}

if (-not [bool]$finalReadiness.noSensitiveContent -or -not [bool]$retry.noSensitiveContent -or -not [bool]$review.noSensitiveContent -or -not [bool]$docPack.noSensitiveContent) {
    $issues += "NoSensitiveContentFalse"
}

$decision = if ($issues.Count -eq 0) { "PASS" } else { "FAIL" }
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$summary = [ordered]@{
    summaryId = "lmax-readonly-market-hours-next-action-$stamp"
    createdAtUtc = (Get-Date).ToUniversalTime().ToString("O")
    recommendedAction = "OperatorApprovedGbpusdMarketHoursSnapshotAttempt"
    status = "ReadyForManualMarketHoursAttemptPlanningOnly"
    selectedInstrument = [ordered]@{
        symbol = "GBPUSD"
        slashSymbol = "GBP/USD"
        securityId = "4002"
        securityIdSource = "8"
        requestMode = "SnapshotPlusUpdates"
        symbolEncodingMode = "SecurityIdOnly"
        marketDepth = 1
    }
    sourceArtifacts = [ordered]@{
        finalReadinessFile = $finalReadinessInput.Path
        marketHoursRetryReadinessFile = $retryInput.Path
        phase6XReviewFile = $reviewInput.Path
        documentationPackFile = $docPackInput.Path
    }
    previousAttempt = [ordered]@{
        status = [string]$review.status
        outsideMarketHours = [bool]$retry.previousAttemptWasOutsideMarketHours
        safe = ([string]$review.finalDecision -eq "PASS_WITH_KNOWN_WARNINGS" -and [string]$review.status -eq "CompletedWithEmptyBook")
        snapshotReceived = [bool]$review.snapshotReceived
        entryCount = [int]$review.entryCount
        warningClassification = [string]$review.warningClassification
    }
    finalReadinessDecision = [string]$finalReadiness.readinessDecision
    marketHoursRetryReadinessDecision = [string]$retry.decision
    phase6XReviewDecision = [string]$review.finalDecision
    documentationPackDecision = [string]$docPack.finalDecision
    executableCount = [int]$docPack.executableCount
    isApprovedForExternalRun = $false
    canRunExternalSnapshot = $false
    eligibleForManualSnapshotAttempt = $false
    runtimeShadowReplaySubmit = $false
    schedulerOrPolling = $false
    orderSubmission = $false
    gatewayRegistration = $false
    tradingMutation = $false
    apiWorkerGatewayMode = "FakeLmaxGateway"
    whatIsAllowed = @("Review readiness", "Inspect artifacts", "Wait for market hours")
    whatIsNotAllowed = @("Run now from UI", "Scheduler", "Polling", "Runtime shadow replay submit", "Order submission", "Gateway registration", "Production/UAT", "Multi-instrument batch")
    noSensitiveContent = $true
    issues = @($issues | ForEach-Object { [ordered]@{ severity = "Error"; code = $_; path = ""; message = $_ } })
    finalDecision = $decision
}

$outDir = Resolve-LocalPath $OutputDirectory
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$jsonPath = Join-Path $outDir "phase6ze-market-hours-next-action-$stamp.json"
$summary | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $jsonPath -Encoding UTF8

if ($WriteMarkdown.IsPresent) {
    $mdPath = [IO.Path]::ChangeExtension($jsonPath, ".md")
    @(
        "# LMAX Market-Hours Next Action",
        "",
        "- FinalDecision: $decision",
        "- RecommendedAction: OperatorApprovedGbpusdMarketHoursSnapshotAttempt",
        "- SelectedInstrument: GBPUSD / GBP/USD / 4002",
        "- PreviousResult: $($review.status) outside market hours",
        "- FinalReadiness: $($finalReadiness.readinessDecision)",
        "- RetryReadiness: $($retry.decision)",
        "- PlanningFreeze: $($docPack.finalDecision)",
        "- executableCount: $($docPack.executableCount)",
        "",
        "This report is read-only and does not authorize UI execution, scheduler/polling, runtime shadow replay submit, order submission, gateway registration, production/UAT, multi-instrument batch, or trading mutation."
    ) | Set-Content -LiteralPath $mdPath -Encoding UTF8
}

Write-Host ""
Write-Host "RecommendedAction: Wait for market hours, then run one operator-approved GBPUSD read-only snapshot attempt."
Write-Host "SelectedInstrument: GBPUSD / GBP/USD / 4002"
Write-Host "PreviousResult: $($review.status) outsideMarketHours=$([bool]$retry.previousAttemptWasOutsideMarketHours)"
Write-Host "FinalReadiness: $($finalReadiness.readinessDecision)"
Write-Host "RetryReadiness: $($retry.decision)"
Write-Host "PlanningFreeze: $($docPack.finalDecision)"
Write-Host "ExecutableCount: $($docPack.executableCount)"
Write-Host "IsApprovedForExternalRun: false"
Write-Host "CanRunExternalSnapshot: false"
Write-Host "EligibleForManualSnapshotAttempt: false"
Write-Host "FinalDecision: $decision"
Write-Host "Report: $jsonPath"
if ($decision -eq "FAIL") { exit 1 }
