param(
    [Parameter(Mandatory = $true)]
    [string]$PipelineManifestFile,

    [Parameter(Mandatory = $true)]
    [string]$PlanningStatusReportFile,

    [string]$OutputDirectory = "artifacts/lmax-readonly-runtime-securityid-planning/documentation-pack"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$expected = [ordered]@{
    GBPUSD = @{ Slash = "GBP/USD"; SecurityId = "4002" }
    EURGBP = @{ Slash = "EUR/GBP"; SecurityId = "4003" }
    USDJPY = @{ Slash = "USD/JPY"; SecurityId = "4004" }
    AUDUSD = @{ Slash = "AUD/USD"; SecurityId = "4007" }
}
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=|raw\s*fix|sendercompid|targetcompid)'

function Resolve-LocalPath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return Join-Path $repoRoot $Path
}

function Read-SanitizedJson([string]$Path, [string]$Label) {
    $resolved = Resolve-LocalPath $Path
    if (-not (Test-Path -LiteralPath $resolved)) {
        throw "$Label not found: $resolved"
    }

    $raw = Get-Content -Raw -LiteralPath $resolved
    if ($raw -match $script:sensitivePattern) {
        throw "$Label contains sensitive-shaped content: $resolved"
    }

    return @{
        Path = $resolved
        Json = ($raw | ConvertFrom-Json)
    }
}

Write-Host "LMAX Read-Only Additional Instruments Planning Documentation Pack"
Write-Host "Local-only. No LMAX connection, no credentials, no SecurityListRequest, no snapshot, no replay, no scheduler, no orders, and no mutation."

$pipelineInput = Read-SanitizedJson $PipelineManifestFile "Pipeline manifest"
$statusInput = Read-SanitizedJson $PlanningStatusReportFile "Planning status report"
$pipeline = $pipelineInput.Json
$status = $statusInput.Json
$issues = @()

if ([string]$pipeline.finalDecision -ne "PASS") { $issues += "PipelineDecisionNotPass" }
if ([int]$pipeline.instrumentCount -ne 4) { $issues += "PipelineInstrumentCountNotFour" }
if ([int]$pipeline.readyForFutureManualConsiderationCount -ne 4) { $issues += "ReadyForFutureManualConsiderationCountNotFour" }
if ([int]$pipeline.executableCount -ne 0) { $issues += "PipelineExecutableCountNonZero" }
if ([bool]$pipeline.isApprovedForExternalRun -or [bool]$pipeline.canRunExternalSnapshot -or [bool]$pipeline.eligibleForManualSnapshotAttempt) { $issues += "PipelineExecutableFlagTrue" }
if ([bool]$pipeline.schedulerStarted -or [bool]$pipeline.orderSubmissionAttempted -or [bool]$pipeline.shadowReplaySubmitAttempted -or [bool]$pipeline.tradingMutationAttempted) { $issues += "PipelineUnsafeAttemptFlagTrue" }
if ([string]$pipeline.apiWorkerGatewayMode -ne "FakeLmaxGateway") { $issues += "PipelineGatewayModeNotFakeLmaxGateway" }
if (-not [bool]$pipeline.noSensitiveContent) { $issues += "PipelineNoSensitiveContentFalse" }

if ([string]$status.finalDecision -ne "PASS") { $issues += "PlanningStatusDecisionNotPass" }
if ([string]$status.aggregateDecision -ne "PASS") { $issues += "PlanningStatusAggregateDecisionNotPass" }
if ([int]$status.instrumentCount -ne 4) { $issues += "PlanningStatusInstrumentCountNotFour" }
if ([int]$status.executableCount -ne 0) { $issues += "PlanningStatusExecutableCountNonZero" }
if ([bool]$status.runtimeShadowReplaySubmit -or [bool]$status.schedulerOrPolling -or [bool]$status.orderSubmission -or [bool]$status.gatewayRegistration -or [bool]$status.tradingMutation) { $issues += "PlanningStatusUnsafeRuntimeFlagTrue" }
if ([string]$status.apiWorkerGatewayMode -ne "FakeLmaxGateway") { $issues += "PlanningStatusGatewayModeNotFakeLmaxGateway" }
if (-not [bool]$status.noSensitiveContent) { $issues += "PlanningStatusNoSensitiveContentFalse" }

$instrumentRows = @()
foreach ($symbol in $expected.Keys) {
    $pipelineInstrument = @($pipeline.instruments | Where-Object { [string]$_.symbol -eq $symbol })[0]
    $statusInstrument = @($status.instruments | Where-Object { [string]$_.symbol -eq $symbol })[0]

    if ($null -eq $pipelineInstrument) {
        $issues += "MissingPipelineInstrument:$symbol"
        continue
    }
    if ($null -eq $statusInstrument) {
        $issues += "MissingPlanningStatusInstrument:$symbol"
        continue
    }

    $expectedSlash = $expected[$symbol].Slash
    $expectedSecurityId = $expected[$symbol].SecurityId
    if ([string]$pipelineInstrument.slashSymbol -ne $expectedSlash -or [string]$pipelineInstrument.planningSecurityId -ne $expectedSecurityId -or [string]$pipelineInstrument.securityIdSource -ne "8") {
        $issues += "PipelineInstrumentIdentityMismatch:$symbol"
    }
    if ([string]$statusInstrument.slashSymbol -ne $expectedSlash -or [string]$statusInstrument.planningSecurityId -ne $expectedSecurityId -or [string]$statusInstrument.securityIdSource -ne "8") {
        $issues += "PlanningStatusInstrumentIdentityMismatch:$symbol"
    }

    $expectedDecisionsOk =
        [string]$pipelineInstrument.safetyGateDecision -eq "PASS" -and
        [string]$pipelineInstrument.preflightDecision -eq "PASS" -and
        [string]$pipelineInstrument.approvalEnvelopeDecision -eq "AcceptedForPlanning" -and
        [string]$pipelineInstrument.dryRunDecision -eq "PASS" -and
        [string]$pipelineInstrument.attemptGateDecision -eq "PASS" -and
        [string]$pipelineInstrument.executionPlanDecision -eq "PASS" -and
        [string]$pipelineInstrument.operatorSignoffDecision -eq "SignedForPlanning" -and
        [string]$pipelineInstrument.finalReadinessDecision -eq "PASS"
    if (-not $expectedDecisionsOk) {
        $issues += "PipelineInstrumentDecisionMismatch:$symbol"
    }

    if ([bool]$pipelineInstrument.isApprovedForExternalRun -or [bool]$pipelineInstrument.canRunExternalSnapshot -or [bool]$pipelineInstrument.eligibleForManualSnapshotAttempt -or [bool]$pipelineInstrument.externalConnectionAttempted -or [bool]$pipelineInstrument.snapshotAttempted -or [bool]$pipelineInstrument.replayAttempted -or [bool]$pipelineInstrument.orderSubmissionAttempted -or [bool]$pipelineInstrument.shadowReplaySubmitAttempted -or [bool]$pipelineInstrument.tradingMutationAttempted -or [bool]$pipelineInstrument.schedulerStarted) {
        $issues += "PipelineInstrumentUnsafeFlagTrue:$symbol"
    }
    if ([bool]$statusInstrument.isApprovedForExternalRun -or [bool]$statusInstrument.canRunExternalSnapshot -or [bool]$statusInstrument.eligibleForManualSnapshotAttempt) {
        $issues += "PlanningStatusInstrumentUnsafeFlagTrue:$symbol"
    }
    if (-not [bool]$pipelineInstrument.noSensitiveContent) {
        $issues += "PipelineInstrumentNoSensitiveContentFalse:$symbol"
    }

    $instrumentRows += [ordered]@{
        symbol = $symbol
        slashSymbol = [string]$pipelineInstrument.slashSymbol
        planningSecurityId = [string]$pipelineInstrument.planningSecurityId
        securityIdSource = [string]$pipelineInstrument.securityIdSource
        planningDecision = "AcceptedForPlanning"
        pipelineDecision = [string]$statusInstrument.pipelineDecision
        safetyGateDecision = [string]$pipelineInstrument.safetyGateDecision
        preflightDecision = [string]$pipelineInstrument.preflightDecision
        approvalEnvelopeDecision = [string]$pipelineInstrument.approvalEnvelopeDecision
        dryRunDecision = [string]$pipelineInstrument.dryRunDecision
        attemptGateDecision = [string]$pipelineInstrument.attemptGateDecision
        executionPlanDecision = [string]$pipelineInstrument.executionPlanDecision
        operatorSignoffDecision = [string]$pipelineInstrument.operatorSignoffDecision
        finalReadinessDecision = [string]$pipelineInstrument.finalReadinessDecision
        isApprovedForExternalRun = $false
        canRunExternalSnapshot = $false
        eligibleForManualSnapshotAttempt = $false
        executable = $false
        approvalEnvelopePath = [string]$pipelineInstrument.approvalEnvelopePath
        dryRunReportPath = [string]$pipelineInstrument.dryRunReportPath
        attemptGatePath = [string]$pipelineInstrument.attemptGatePath
        executionPlanPath = [string]$pipelineInstrument.executionPlanPath
        operatorSignoffPath = [string]$pipelineInstrument.operatorSignoffPath
        finalReadinessPath = [string]$pipelineInstrument.finalReadinessPath
        recommendedNextAction = [string]$statusInstrument.recommendedNextAction
    }
}

$finalDecision = if ($issues.Count -eq 0) { "PASS" } else { "FAIL" }
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$outDir = Resolve-LocalPath $OutputDirectory
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$jsonPath = Join-Path $outDir "lmax-readonly-additional-instruments-planning-doc-pack-$stamp.json"
$mdPath = Join-Path $outDir "lmax-readonly-additional-instruments-planning-doc-pack-$stamp.md"

$pack = [ordered]@{
    docPackId = "lmax-readonly-additional-instruments-planning-doc-pack-$stamp"
    createdAtUtc = (Get-Date).ToUniversalTime().ToString("O")
    phase = "6Z-D"
    sourcePipelineManifestFile = $pipelineInput.Path
    sourcePlanningStatusReportFile = $statusInput.Path
    securityIdSourceEvidence = "Uploaded LMAX instrument CSVs; London/NewYork 400x IDs selected for DemoLondon; Tokyo 600x IDs explicitly not selected."
    aggregateDecision = [string]$pipeline.finalDecision
    instrumentCount = [int]$pipeline.instrumentCount
    readyForFutureManualConsiderationCount = [int]$pipeline.readyForFutureManualConsiderationCount
    executableCount = [int]$pipeline.executableCount
    runtimeShadowReplaySubmit = $false
    schedulerOrPolling = $false
    orderSubmission = $false
    gatewayRegistration = $false
    tradingMutation = $false
    apiWorkerGatewayMode = "FakeLmaxGateway"
    isApprovedForExternalRun = $false
    canRunExternalSnapshot = $false
    eligibleForManualSnapshotAttempt = $false
    externalConnectionAttempted = $false
    snapshotAttempted = $false
    replayAttempted = $false
    noSensitiveContent = $true
    instruments = $instrumentRows
    artifactReferences = [ordered]@{
        confirmationRecordsDirectory = "artifacts/lmax-readonly-runtime-securityid-confirmations/real/"
        planningManifest = [string]$pipeline.sourcePlanningManifestPath
        safetyGateManifest = [string]$pipeline.sourceSafetyGateManifestPath
        preflightManifest = [string]$pipeline.sourcePreflightManifestPath
        aggregatePipelineManifest = $pipelineInput.Path
        operatorPlanningStatusReport = $statusInput.Path
        phase6zcGateReport = "artifacts/readiness/phase6zc-additional-instrument-status-panel-gate.json"
        phase6zdDoc = "docs/LMAX_READONLY_ADDITIONAL_INSTRUMENTS_PLANNING_FINAL_DOC.md"
    }
    whatPassMeans = "The additional-instrument planning pipeline is complete, internally consistent, sanitized, and non-executable."
    whatPassDoesNotAuthorize = @(
        "External run",
        "Scheduler or polling",
        "Runtime shadow replay submit",
        "Order submission",
        "Gateway registration",
        "Production or UAT use",
        "Multi-instrument batch",
        "Trading-state mutation"
    )
    nextRecommendedPhase = "Phase 6Z-B - Operator-approved Market-Hours Snapshot Attempt for one selected additional instrument, only when market is open and the operator explicitly chooses; otherwise stop with planning frozen."
    issues = @($issues)
    finalDecision = $finalDecision
}

$pack | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $jsonPath -Encoding UTF8

$md = @(
    "# LMAX Additional Instruments Planning Documentation Pack",
    "",
    "FinalDecision: $finalDecision",
    "",
    "This pack is local-only documentation and audit evidence. It does not connect to LMAX, read credentials, request SecurityList, run snapshots, replay evidence, schedule work, submit orders, register gateways, or mutate trading state.",
    "",
    "## Summary",
    "",
    "| Field | Value |",
    "| --- | --- |",
    "| AggregateDecision | $($pipeline.finalDecision) |",
    "| InstrumentCount | $($pipeline.instrumentCount) |",
    "| ReadyForFutureManualConsiderationCount | $($pipeline.readyForFutureManualConsiderationCount) |",
    "| ExecutableCount | $($pipeline.executableCount) |",
    "| API/Worker Gateway | FakeLmaxGateway |",
    "| NoSensitiveContent | true |",
    "",
    "## Instruments",
    "",
    "| Symbol | Slash | SecurityID | Safety | Preflight | Approval | Dry Run | Attempt Gate | Execution Plan | Signoff | Final Readiness | Executable |",
    "| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |"
)
foreach ($row in $instrumentRows) {
    $md += "| $($row.symbol) | $($row.slashSymbol) | $($row.planningSecurityId) | $($row.safetyGateDecision) | $($row.preflightDecision) | $($row.approvalEnvelopeDecision) | $($row.dryRunDecision) | $($row.attemptGateDecision) | $($row.executionPlanDecision) | $($row.operatorSignoffDecision) | $($row.finalReadinessDecision) | false |"
}
$md += @(
    "",
    "## Safety Confirmations",
    "",
    "- `executableCount=0`.",
    "- `IsApprovedForExternalRun=false` for all instruments.",
    "- `canRunExternalSnapshot=false` for all instruments.",
    "- `eligibleForManualSnapshotAttempt=false` for all instruments.",
    "- `runtimeShadowReplaySubmit=false`.",
    "- `schedulerOrPolling=false`.",
    "- `orderSubmission=false`.",
    "- `gatewayRegistration=false`.",
    "- `tradingMutation=false`.",
    "- API/Worker remain `FakeLmaxGateway` only.",
    "",
    "## What PASS Means",
    "",
    "PASS means the planning artifact chain is complete, internally consistent, sanitized, and non-executable.",
    "",
    "## What PASS Does Not Authorize",
    "",
    "- External run.",
    "- Scheduler or polling.",
    "- Runtime shadow replay submit.",
    "- Order submission.",
    "- Gateway registration.",
    "- Production or UAT use.",
    "- Multi-instrument batch.",
    "- Trading-state mutation.",
    "",
    "## Next Action",
    "",
    "Wait for market hours. If the operator explicitly chooses to proceed later, use one selected instrument only. GBPUSD remains the next selected candidate because the first Saturday attempt completed safely with an empty book outside normal FX market hours.",
    "",
    "## References",
    "",
    ("- Pipeline manifest: ``{0}``" -f $pipelineInput.Path),
    ("- Planning status report: ``{0}``" -f $statusInput.Path),
    "- Final doc: ``docs/LMAX_READONLY_ADDITIONAL_INSTRUMENTS_PLANNING_FINAL_DOC.md``"
)
$md | Set-Content -LiteralPath $mdPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $finalDecision"
Write-Host "ExecutableCount: $($pipeline.executableCount)"
Write-Host "JSON: $jsonPath"
Write-Host "Markdown: $mdPath"
if ($finalDecision -eq "FAIL") {
    Write-Host "Issues: $($issues -join ', ')"
    exit 1
}
