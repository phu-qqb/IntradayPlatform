param(
    [Parameter(Mandatory=$true)]
    [string]$PipelineManifestFile,
    [Parameter(Mandatory=$true)]
    [string]$PlanningStatusReportFile,
    [Parameter(Mandatory=$true)]
    [string]$RequestedByOperatorId,
    [Parameter(Mandatory=$true)]
    [string]$Reason,
    [string]$OutputDirectory = "artifacts/lmax-readonly-runtime-securityid-planning/multi-instrument-workflow",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=|raw\s*fix|sendercompid|targetcompid)'

function Resolve-LocalPath([string]$PathValue) {
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}

function Assert-SafeText([string]$Name, [string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { throw "$Name is required." }
    if ($Value -match $script:sensitivePattern) { throw "$Name contains credential-shaped content." }
    if ($Value -match '(?i)(NewOrderSingle|OrderCancelRequest|OrderCancelReplaceRequest|TradeCapture|OrderStatusRequest|SubmitOrder|production|uat|run automatically|automatic retry|batch execution allowed)') {
        throw "$Name contains unsafe authorization or trading language."
    }
}

function Read-Json([string]$PathValue, [string]$Label) {
    $resolved = Resolve-LocalPath $PathValue
    if (-not (Test-Path -LiteralPath $resolved)) { throw "$Label not found: $resolved" }
    $raw = Get-Content -Raw -LiteralPath $resolved
    if ($raw -match $script:sensitivePattern) { throw "$Label contains credential-shaped or raw FIX content." }
    return @{ Path = $resolved; Raw = $raw; Json = ($raw | ConvertFrom-Json) }
}

Assert-SafeText "RequestedByOperatorId" $RequestedByOperatorId
Assert-SafeText "Reason" $Reason

$pipelineRef = Read-Json $PipelineManifestFile "Pipeline manifest"
$statusRef = Read-Json $PlanningStatusReportFile "Planning status report"
$phase7AAdr = Join-Path $repoRoot "docs/LMAX_READONLY_RUNTIME_PHASE7_NEXT_BOUNDARY_ADR.md"
if (-not (Test-Path -LiteralPath $phase7AAdr)) { throw "Phase 7A ADR not found: $phase7AAdr" }

$pipeline = $pipelineRef.Json
$status = $statusRef.Json

if ([string]$pipeline.finalDecision -ne "PASS") { throw "Pipeline manifest finalDecision must be PASS." }
if ([int]$pipeline.instrumentCount -ne 4 -or [int]$pipeline.executableCount -ne 0) { throw "Pipeline manifest must have instrumentCount=4 and executableCount=0." }
if ([int]$status.instrumentCount -ne 4 -or [int]$status.executableCount -ne 0) { throw "Planning status report must have instrumentCount=4 and executableCount=0." }

$sequence = @(
    @{ symbol = "GBPUSD"; slashSymbol = "GBP/USD"; securityId = "4002"; order = 1 },
    @{ symbol = "EURGBP"; slashSymbol = "EUR/GBP"; securityId = "4003"; order = 2 },
    @{ symbol = "USDJPY"; slashSymbol = "USD/JPY"; securityId = "4004"; order = 3 },
    @{ symbol = "AUDUSD"; slashSymbol = "AUD/USD"; securityId = "4007"; order = 4 }
)

$instrumentPlans = @()
foreach ($item in $sequence) {
    $pipelineInstrument = @($pipeline.instruments | Where-Object { [string]$_.symbol -eq $item.symbol })[0]
    if ($null -eq $pipelineInstrument) { throw "Pipeline manifest missing $($item.symbol)." }
    if ([string]$pipelineInstrument.slashSymbol -ne $item.slashSymbol -or [string]$pipelineInstrument.planningSecurityId -ne $item.securityId -or [string]$pipelineInstrument.securityIdSource -ne "8") {
        throw "Pipeline identity mismatch for $($item.symbol)."
    }
    if ([string]$pipelineInstrument.finalReadinessDecision -ne "PASS") { throw "$($item.symbol) final readiness must be PASS." }
    if ([bool]$pipelineInstrument.canRunExternalSnapshot -or [bool]$pipelineInstrument.isApprovedForExternalRun -or [bool]$pipelineInstrument.eligibleForManualSnapshotAttempt) {
        throw "$($item.symbol) has unsafe executable flags."
    }

    $instrumentPlans += [ordered]@{
        symbol = $item.symbol
        slashSymbol = $item.slashSymbol
        securityId = $item.securityId
        securityIdSource = "8"
        planningPipelineDecision = "PASS"
        selectedForFutureManualConsideration = $true
        proposedSequenceOrder = $item.order
        oneInstrumentAtATime = $true
        maxAttemptsPerInstrument = 1
        retryRequiresNewPhase = $true
        marketHoursOnly = $true
        manualOperatorCommandOnly = $true
        noSchedulerOrPolling = $true
        noRuntimeShadowReplaySubmit = $true
        noOrderSubmission = $true
        noTradingMutation = $true
        noGatewayRegistration = $true
        canRunExternalSnapshot = $false
        isApprovedForExternalRun = $false
        eligibleForManualSnapshotAttempt = $false
        recommendedNextAction = if ($item.symbol -eq "GBPUSD") { "Next market-hours manual candidate; wait for explicit future operator phase." } else { "Pending until GBPUSD market-hours outcome is reviewed." }
    }
}

$stamp = [DateTimeOffset]::UtcNow.ToString("yyyyMMdd-HHmmss")
$plan = [ordered]@{
    planId = "lmax-readonly-controlled-manual-multi-instrument-workflow-plan-$stamp"
    createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    requestedByOperatorId = $RequestedByOperatorId
    reason = $Reason
    sourcePhase7AAdrPath = $phase7AAdr
    sourceAdditionalInstrumentPlanningPipelinePath = $pipelineRef.Path
    sourcePlanningStatusReportPath = $statusRef.Path
    instruments = $instrumentPlans
    instrumentCount = 4
    selectedCount = 4
    executableCount = 0
    batchExecutionAllowed = $false
    schedulerOrPolling = $false
    runtimeShadowReplaySubmit = $false
    orderSubmission = $false
    gatewayRegistration = $false
    tradingMutation = $false
    apiWorkerGatewayMode = "FakeLmaxGateway"
    noSensitiveContent = $true
    workflowRules = @(
        "Each instrument attempt requires its own explicit future phase.",
        "Each attempt requires final readiness PASS, operator signoff, explicit manual command, market hours, and one attempt only.",
        "After each attempt, perform artifact review, evidence preview mapping if appropriate, optional manual local replay if appropriate, and updated planning status before the next instrument.",
        "Failed-safe or empty-book results stop the sequence until reviewed.",
        "No automatic retries, scheduling, polling, runtime replay submit, orders, gateway registration, or trading mutation."
    )
    finalDecision = "PASS"
}

$json = $plan | ConvertTo-Json -Depth 20
if ($json -match $sensitivePattern) { throw "Generated workflow plan contains credential-shaped content." }

$outDir = Resolve-LocalPath $OutputDirectory
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$jsonPath = Join-Path $outDir "lmax-readonly-controlled-manual-multi-instrument-workflow-plan-$stamp.json"
$mdPath = Join-Path $outDir "lmax-readonly-controlled-manual-multi-instrument-workflow-plan-$stamp.md"
if ((Test-Path -LiteralPath $jsonPath) -and -not $Force.IsPresent) { throw "Output already exists: $jsonPath" }

$json | Set-Content -LiteralPath $jsonPath -Encoding UTF8

$md = @"
# LMAX Read-Only Controlled Manual Multi-Instrument Workflow Plan

PlanId: $($plan.planId)

FinalDecision: PASS

Recommended sequence:

1. GBPUSD / GBP/USD / 4002
2. EURGBP / EUR/GBP / 4003
3. USDJPY / USD/JPY / 4004
4. AUDUSD / AUD/USD / 4007

`PASS` means this manual workflow plan is safe and complete. It does not authorize any external run.

Safety state:

- executableCount=0
- batchExecutionAllowed=false
- canRunExternalSnapshot=false for all instruments
- IsApprovedForExternalRun=false for all instruments
- eligibleForManualSnapshotAttempt=false for all instruments
- oneInstrumentAtATime=true for all instruments
- maxAttemptsPerInstrument=1
- retryRequiresNewPhase=true
- no scheduler/polling
- no runtime shadow replay submit
- no orders
- no gateway registration
- no trading mutation
- API/Worker FakeLmaxGateway only

Next recommended phase: Phase 7C - GBPUSD Market-Hours Manual Snapshot Attempt Closure / Evidence Workflow if the future GBPUSD market-hours attempt is run, or wait for market hours.
"@
$md | Set-Content -LiteralPath $mdPath -Encoding UTF8

Write-Host "InstrumentCount: 4"
Write-Host "Sequence: GBPUSD, EURGBP, USDJPY, AUDUSD"
Write-Host "ExecutableCount: 0"
Write-Host "BatchExecutionAllowed: false"
Write-Host "FinalDecision: PASS"
Write-Host "WorkflowPlanJson: $jsonPath"
Write-Host "WorkflowPlanMarkdown: $mdPath"
