param(
    [Parameter(Mandatory=$true)]
    [string]$WorkflowPlanFile,
    [string]$GbpusdClosureManifestFile = "",
    [string]$GbpusdReviewFile = "",
    [Parameter(Mandatory=$true)]
    [string]$RequestedByOperatorId,
    [Parameter(Mandatory=$true)]
    [string]$Reason,
    [string]$OutputDirectory = "artifacts/lmax-readonly-runtime-securityid-planning/next-instrument-decisions",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=|raw\s*fix|sendercompid|targetcompid)'

function Resolve-LocalPath([string]$PathValue) {
    if ([string]::IsNullOrWhiteSpace($PathValue)) { return "" }
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}

function Assert-SafeText([string]$Name, [string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { throw "$Name is required." }
    if ($Value -match $script:sensitivePattern) { throw "$Name contains credential-shaped content." }
    if ($Value -match '(?i)(NewOrderSingle|OrderCancelRequest|OrderCancelReplaceRequest|TradeCapture|OrderStatusRequest|SubmitOrder|production|uat|batch execution allowed|run automatically|automatic retry)') {
        throw "$Name contains unsafe authorization or trading language."
    }
}

function Read-Json([string]$PathValue, [string]$Label) {
    $resolved = Resolve-LocalPath $PathValue
    if (-not (Test-Path -LiteralPath $resolved)) { throw "$Label not found: $resolved" }
    $raw = Get-Content -LiteralPath $resolved -Raw
    if ($raw -match $script:sensitivePattern) { throw "$Label contains credential-shaped or raw FIX content." }
    return @{ Path = $resolved; Raw = $raw; Json = ($raw | ConvertFrom-Json) }
}

function Get-Decision([string]$Classification, [string]$ClosureDecision) {
    if ([string]::IsNullOrWhiteSpace($Classification) -and [string]::IsNullOrWhiteSpace($ClosureDecision)) {
        return "PendingGbpusdMarketHoursAttempt"
    }
    if ($Classification -eq "CompletedWithBook" -and $ClosureDecision -eq "PASS") {
        return "ProceedToEurgbpPlanning"
    }
    if ($Classification -eq "CompletedWithEmptyBook" -and $ClosureDecision -eq "PASS_WITH_KNOWN_WARNINGS") {
        return "RetryGbpusdAtLaterMarketHours"
    }
    return "BlockSequenceForDiagnostics"
}

function Get-RequiredNextPhase([string]$Decision) {
    switch ($Decision) {
        "PendingGbpusdMarketHoursAttempt" { "Wait for market hours, run the separate operator-approved GBPUSD manual snapshot command, then complete Phase 7C closure." }
        "ProceedToEurgbpPlanning" { "Phase 7E - EURGBP Manual Snapshot Readiness Refresh / No External Run." }
        "RetryGbpusdAtLaterMarketHours" { "Phase 7E - Controlled GBPUSD Market-Hours Retry Planning / No External Run." }
        "BlockSequenceForDiagnostics" { "Phase 7E - GBPUSD Closure Diagnostics / No External Run." }
        "StopManualWorkflow" { "Stop manual additional-instrument workflow." }
        default { "Manual operator review required." }
    }
}

Write-Host "LMAX Read-Only Phase 7D Next Instrument Decision"
Write-Host "Local-only. This script does not connect to LMAX, request snapshots, request SecurityList, replay evidence, schedule work, or use credentials."

Assert-SafeText "RequestedByOperatorId" $RequestedByOperatorId
Assert-SafeText "Reason" $Reason

$workflowRef = Read-Json $WorkflowPlanFile "Workflow plan"
$workflow = $workflowRef.Json
if ([string]$workflow.finalDecision -ne "PASS") { throw "Workflow plan finalDecision must be PASS." }
if ([int]$workflow.instrumentCount -ne 4 -or [int]$workflow.executableCount -ne 0 -or [bool]$workflow.batchExecutionAllowed) {
    throw "Workflow plan must have instrumentCount=4, executableCount=0, and batchExecutionAllowed=false."
}

$gbpusd = @($workflow.instruments | Where-Object { [string]$_.symbol -eq "GBPUSD" })[0]
$eurgbp = @($workflow.instruments | Where-Object { [string]$_.symbol -eq "EURGBP" })[0]
if ($null -eq $gbpusd -or $null -eq $eurgbp) { throw "Workflow plan must include GBPUSD and EURGBP." }
if ([int]$gbpusd.proposedSequenceOrder -ne 1 -or [int]$eurgbp.proposedSequenceOrder -ne 2) { throw "Workflow plan sequence must start GBPUSD then EURGBP." }
if ([bool]$gbpusd.canRunExternalSnapshot -or [bool]$gbpusd.isApprovedForExternalRun -or [bool]$gbpusd.eligibleForManualSnapshotAttempt) {
    throw "GBPUSD workflow plan run flags must remain false."
}

$sourceClosurePath = ""
$sourceReviewPath = ""
$closureStatus = $null
$closureDecision = $null
$closureClassification = $null

if (-not [string]::IsNullOrWhiteSpace($GbpusdClosureManifestFile)) {
    $closureRef = Read-Json $GbpusdClosureManifestFile "GBPUSD closure manifest"
    $sourceClosurePath = $closureRef.Path
    $closure = $closureRef.Json
    if ([string]$closure.symbol -ne "GBPUSD" -or [string]$closure.securityId -ne "4002") { throw "Closure manifest must be GBPUSD / 4002." }
    $closureDecision = [string]$closure.finalClosureDecision
    $closureClassification = [string]$closure.closureClassification
    $closureStatus = [string]$closure.status
}

if (-not [string]::IsNullOrWhiteSpace($GbpusdReviewFile)) {
    $reviewRef = Read-Json $GbpusdReviewFile "GBPUSD review report"
    $sourceReviewPath = $reviewRef.Path
    $review = $reviewRef.Json
    if ([string]$review.symbol -ne "GBPUSD" -or [string]$review.securityId -ne "4002") { throw "Review report must be GBPUSD / 4002." }
    if ([string]::IsNullOrWhiteSpace($closureDecision)) { $closureDecision = [string]$review.finalDecision }
    if ([string]::IsNullOrWhiteSpace($closureClassification)) { $closureClassification = [string]$review.closureClassification }
    if ([string]::IsNullOrWhiteSpace($closureStatus)) { $closureStatus = [string]$review.status }
}

$decision = Get-Decision $closureClassification $closureDecision
$nextCandidate = if ($decision -eq "ProceedToEurgbpPlanning") { "EURGBP" } else { $null }
$requiredNextPhase = Get-RequiredNextPhase $decision
$stamp = [DateTimeOffset]::UtcNow.ToString("yyyyMMdd-HHmmss")

$artifact = [ordered]@{
    decisionId = "lmax-readonly-post-gbpusd-next-instrument-decision-$stamp"
    createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    requestedByOperatorId = $RequestedByOperatorId
    reason = $Reason
    sourceWorkflowPlanPath = $workflowRef.Path
    sourceGbpusdClosureManifestPath = $sourceClosurePath
    sourceGbpusdReviewPath = $sourceReviewPath
    currentInstrument = "GBPUSD"
    nextCandidateInstrument = $nextCandidate
    sequenceOrder = 1
    gbpusdClosureStatus = $closureStatus
    gbpusdClosureDecision = $closureDecision
    gbpusdClosureClassification = $closureClassification
    decision = $decision
    requiredNextPhase = $requiredNextPhase
    canRunExternalSnapshot = $false
    isApprovedForExternalRun = $false
    eligibleForManualSnapshotAttempt = $false
    batchExecutionAllowed = $false
    executableCount = 0
    schedulerOrPolling = $false
    runtimeShadowReplaySubmit = $false
    orderSubmission = $false
    gatewayRegistration = $false
    tradingMutation = $false
    apiWorkerGatewayMode = "FakeLmaxGateway"
    noSensitiveContent = $true
    notes = @(
        "Phase 7D is decision-only and does not authorize any external run.",
        "The one-instrument-at-a-time rule remains enforced.",
        "EURGBP may only become the next planning candidate after GBPUSD closes with CompletedWithBook/PASS.",
        "No batch execution, scheduler, polling, runtime shadow replay submit, order submission, gateway registration, or trading mutation is allowed."
    )
}

$json = $artifact | ConvertTo-Json -Depth 12
if ($json -match $sensitivePattern) { throw "Generated decision contains credential-shaped content." }
if ($json -match '(?i)(NewOrderSingle|OrderCancelRequest|OrderCancelReplaceRequest|TradeCapture|OrderStatusRequest|SubmitOrder|ReplaySubmitAsync|PeriodicTimer)') {
    throw "Generated decision contains forbidden runtime/order/scheduler marker."
}

$outDir = Resolve-LocalPath $OutputDirectory
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$outPath = Join-Path $outDir "lmax-readonly-post-gbpusd-next-instrument-decision-$stamp.json"
if ((Test-Path -LiteralPath $outPath) -and -not $Force.IsPresent) { throw "Output already exists: $outPath" }
$json | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host "Decision: $decision"
Write-Host "CurrentInstrument: GBPUSD"
Write-Host ("NextCandidateInstrument: {0}" -f $(if ($nextCandidate) { $nextCandidate } else { "<none>" }))
Write-Host "RequiredNextPhase: $requiredNextPhase"
Write-Host "CanRunExternalSnapshot: false"
Write-Host "BatchExecutionAllowed: false"
Write-Host "ExecutableCount: 0"
Write-Host "DecisionFile: $outPath"
