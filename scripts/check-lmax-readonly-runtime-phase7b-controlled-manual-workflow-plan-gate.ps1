param(
    [string]$WorkflowPlanFile,
    [string]$PipelineManifestFile = "artifacts/lmax-readonly-runtime-securityid-planning/pipeline/lmax-readonly-additional-instrument-planning-pipeline-20260509-175849.json",
    [string]$PlanningStatusReportFile = "artifacts/readiness/phase6zc-additional-instrument-planning-status-20260509-202212.json"
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
    if ([string]::IsNullOrWhiteSpace($PathValue)) { return $PathValue }
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}

function Read-JsonForGate([string]$PathValue, [string]$Label) {
    $resolved = Resolve-LocalPath $PathValue
    if (-not (Test-Path -LiteralPath $resolved)) {
        Add-Result $Label "File exists" "FAIL" "Missing $resolved"
        return $null
    }
    Add-Result $Label "File exists" "PASS" $resolved
    $raw = Get-Content -Raw -LiteralPath $resolved
    if ($raw -match $script:sensitivePattern) {
        Add-Result $Label "No sensitive content" "FAIL" "Sensitive-shaped content found."
    } else {
        Add-Result $Label "No sensitive content" "PASS" "No credential-shaped or raw FIX content."
    }
    return ($raw | ConvertFrom-Json)
}

function Get-TextHit([string[]]$Path, [string[]]$Pattern) {
    $existing = @($Path | Where-Object { Test-Path -LiteralPath $_ })
    if ($existing.Count -eq 0) { return @() }
    return @(Select-String -Path $existing -Pattern $Pattern -SimpleMatch -ErrorAction SilentlyContinue)
}

Write-Host "LMAX Read-Only Runtime Phase 7B Controlled Manual Workflow Plan Gate"
Write-Host "Local-only. This gate does not connect to LMAX, call external APIs, request SecurityList, run snapshots, replay evidence, schedule work, or use credentials."

$model = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyControlledManualMultiInstrumentWorkflowPlan.cs"
$test = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlyControlledManualWorkflowPlanTests.cs"
$builder = Join-Path $PSScriptRoot "build-lmax-readonly-controlled-manual-multi-instrument-workflow-plan.ps1"
$doc = Join-Path $repoRoot "docs/LMAX_READONLY_RUNTIME_PHASE7_CONTROLLED_MANUAL_MULTI_INSTRUMENT_WORKFLOW_PLAN.md"

foreach ($item in @(
    @{ n = "Phase 7B model"; p = $model },
    @{ n = "Phase 7B tests"; p = $test },
    @{ n = "Phase 7B builder"; p = $builder },
    @{ n = "Phase 7B workflow plan doc"; p = $doc }
)) {
    if (Test-Path -LiteralPath $item.p) { Add-Result "Files" "$($item.n) exists" "PASS" $item.p } else { Add-Result "Files" "$($item.n) exists" "FAIL" "Missing $($item.p)" }
}

$pipeline = Read-JsonForGate $PipelineManifestFile "Pipeline"
$status = Read-JsonForGate $PlanningStatusReportFile "PlanningStatus"
if ($null -ne $pipeline) {
    if ([string]$pipeline.finalDecision -eq "PASS" -and [int]$pipeline.instrumentCount -eq 4 -and [int]$pipeline.executableCount -eq 0) { Add-Result "Pipeline" "Aggregate state" "PASS" "PASS; instrumentCount=4; executableCount=0." } else { Add-Result "Pipeline" "Aggregate state" "FAIL" "Unexpected pipeline state." }
}
if ($null -ne $status) {
    if ([int]$status.instrumentCount -eq 4 -and [int]$status.executableCount -eq 0) { Add-Result "PlanningStatus" "Aggregate state" "PASS" "instrumentCount=4; executableCount=0." } else { Add-Result "PlanningStatus" "Aggregate state" "FAIL" "Unexpected status state." }
}

if (-not [string]::IsNullOrWhiteSpace($WorkflowPlanFile)) {
    $plan = Read-JsonForGate $WorkflowPlanFile "WorkflowPlan"
    if ($null -ne $plan) {
        if ([string]$plan.finalDecision -eq "PASS" -and [int]$plan.instrumentCount -eq 4 -and [int]$plan.selectedCount -eq 4 -and [int]$plan.executableCount -eq 0) { Add-Result "WorkflowPlan" "Decision and counts" "PASS" "PASS; instrumentCount=4; selectedCount=4; executableCount=0." } else { Add-Result "WorkflowPlan" "Decision and counts" "FAIL" "Unexpected workflow plan counts/decision." }
        if (-not [bool]$plan.batchExecutionAllowed -and -not [bool]$plan.schedulerOrPolling -and -not [bool]$plan.runtimeShadowReplaySubmit -and -not [bool]$plan.orderSubmission -and -not [bool]$plan.gatewayRegistration -and -not [bool]$plan.tradingMutation -and [string]$plan.apiWorkerGatewayMode -eq "FakeLmaxGateway") { Add-Result "WorkflowPlan" "Aggregate safety flags" "PASS" "No batch/scheduler/replay/orders/gateway/mutation; FakeLmaxGateway." } else { Add-Result "WorkflowPlan" "Aggregate safety flags" "FAIL" "Unsafe aggregate flag found." }

        $expected = @(
            @{ symbol = "GBPUSD"; slash = "GBP/USD"; id = "4002"; order = 1 },
            @{ symbol = "EURGBP"; slash = "EUR/GBP"; id = "4003"; order = 2 },
            @{ symbol = "USDJPY"; slash = "USD/JPY"; id = "4004"; order = 3 },
            @{ symbol = "AUDUSD"; slash = "AUD/USD"; id = "4007"; order = 4 }
        )
        foreach ($item in $expected) {
            $instrument = @($plan.instruments | Where-Object { [string]$_.symbol -eq $item.symbol })[0]
            if ($null -eq $instrument) {
                Add-Result "WorkflowPlan" "$($item.symbol) present" "FAIL" "Missing instrument."
                continue
            }
            if ([string]$instrument.slashSymbol -eq $item.slash -and [string]$instrument.securityId -eq $item.id -and [string]$instrument.securityIdSource -eq "8" -and [int]$instrument.proposedSequenceOrder -eq $item.order) { Add-Result "WorkflowPlan" "$($item.symbol) identity and sequence" "PASS" "$($item.symbol) / $($item.id) / sequence $($item.order)." } else { Add-Result "WorkflowPlan" "$($item.symbol) identity and sequence" "FAIL" "Unexpected identity or sequence." }
            if ([bool]$instrument.oneInstrumentAtATime -and [int]$instrument.maxAttemptsPerInstrument -eq 1 -and [bool]$instrument.retryRequiresNewPhase -and [bool]$instrument.marketHoursOnly -and [bool]$instrument.manualOperatorCommandOnly) { Add-Result "WorkflowPlan" "$($item.symbol) manual attempt rules" "PASS" "One-at-a-time, one attempt, retry requires new phase, market-hours, manual command only." } else { Add-Result "WorkflowPlan" "$($item.symbol) manual attempt rules" "FAIL" "Manual attempt rule violation." }
            if (-not [bool]$instrument.canRunExternalSnapshot -and -not [bool]$instrument.isApprovedForExternalRun -and -not [bool]$instrument.eligibleForManualSnapshotAttempt -and [bool]$instrument.noSchedulerOrPolling -and [bool]$instrument.noRuntimeShadowReplaySubmit -and [bool]$instrument.noOrderSubmission -and [bool]$instrument.noGatewayRegistration -and [bool]$instrument.noTradingMutation) { Add-Result "WorkflowPlan" "$($item.symbol) non-executable flags" "PASS" "All run flags false and safety rules true." } else { Add-Result "WorkflowPlan" "$($item.symbol) non-executable flags" "FAIL" "Unsafe instrument flag found." }
        }
    }
} else {
    Add-Result "WorkflowPlan" "Workflow plan supplied" "WARN" "No workflow plan file supplied; source/script checks only."
}

$apiWorkerFiles = @((Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"), (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"))
$apiWorkerText = ($apiWorkerFiles | Where-Object { Test-Path -LiteralPath $_ } | ForEach-Object { Get-Content -Raw -LiteralPath $_ }) -join "`n"
if ($apiWorkerText.Contains("FakeLmaxGateway") -and -not ($apiWorkerText.Contains("RealLmaxGateway") -or $apiWorkerText.Contains("LmaxVenueGatewaySkeleton"))) { Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No real gateway registration found." } else { Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" "Unexpected gateway registration marker." }
if ((Get-TextHit -Path $apiWorkerFiles -Pattern @("PeriodicTimer", "LmaxScheduler", "SecurityListPolling", "MarketDataPolling", "SnapshotPolling")).Count -eq 0) { Add-Result "Scheduler" "No scheduler/polling added" "PASS" "No LMAX scheduler/polling marker found in API/Worker startup." } else { Add-Result "Scheduler" "No scheduler/polling added" "FAIL" "LMAX scheduler/polling marker found." }
if ((Get-TextHit -Path $apiWorkerFiles -Pattern @("SubmitToShadowReplay = true", "SubmittedToShadowReplay = true", "ReplaySubmitAsync")).Count -eq 0) { Add-Result "Replay" "Runtime still does not submit to shadow replay" "PASS" "No runtime replay submit marker found." } else { Add-Result "Replay" "Runtime still does not submit to shadow replay" "FAIL" "Runtime replay submit marker found." }
if ((Get-TextHit -Path $apiWorkerFiles -Pattern @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "OrderStatusRequest", "TradeCaptureReportRequest", "SubmitOrder")).Count -eq 0) { Add-Result "Orders" "No order surface" "PASS" "No order marker found in API/Worker startup." } else { Add-Result "Orders" "No order surface" "FAIL" "Order marker found." }
if ((Get-TextHit -Path $apiWorkerFiles -Pattern @("PersistTrade", "TradingState", "IOrderRepository", "IFillRepository", "IPositionRepository", "PersistLiveFix")).Count -eq 0) { Add-Result "Mutation" "No trading-state mutation references" "PASS" "No mutation marker found in API/Worker startup." } else { Add-Result "Mutation" "No trading-state mutation references" "FAIL" "Mutation marker found." }

Add-Result "Runtime" "External LMAX connection" "PASS" "This gate does not connect to LMAX."
Add-Result "Snapshot" "MarketData snapshot" "PASS" "This gate does not request snapshots."
Add-Result "SecurityList" "SecurityListRequest" "PASS" "This gate does not request SecurityList."
Add-Result "Replay" "Manual replay" "PASS" "This gate does not replay evidence."
Add-Result "Credentials" "Credential values" "PASS" "This gate does not require or print credential values."

$final = if ($results.status -contains "FAIL") { "FAIL" } elseif ($results.status -contains "WARN") { "PASS_WITH_KNOWN_WARNINGS" } else { "PASS" }
$report = [ordered]@{
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("O")
    phase = "7B"
    finalDecision = $final
    recommendedSequence = @("GBPUSD", "EURGBP", "USDJPY", "AUDUSD")
    executableCount = 0
    batchExecutionAllowed = $false
    externalConnectionAttempted = $false
    snapshotAttempted = $false
    securityListRequestAttempted = $false
    replayAttempted = $false
    schedulerStarted = $false
    orderSubmissionAttempted = $false
    shadowReplaySubmitAttempted = $false
    tradingMutationAttempted = $false
    apiWorkerGatewayMode = "FakeLmaxGateway"
    results = $results
}
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$outFile = Join-Path $outDir "phase7b-controlled-manual-workflow-plan-gate.json"
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $outFile -Encoding UTF8
Write-Host ""
Write-Host "FinalDecision: $final"
Write-Host "Report: $outFile"
if ($final -eq "FAIL") { exit 1 }
