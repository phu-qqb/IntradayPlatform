param(
    [Parameter(Mandatory=$true)]
    [string]$Phase7DDecisionFile,
    [Parameter(Mandatory=$true)]
    [string]$PipelineManifestFile,
    [Parameter(Mandatory=$true)]
    [string]$PlanningManifestFile,
    [Parameter(Mandatory=$true)]
    [string]$SafetyGateManifestFile,
    [Parameter(Mandatory=$true)]
    [string]$PreflightManifestFile,
    [Parameter(Mandatory=$true)]
    [string]$RequestedByOperatorId,
    [Parameter(Mandatory=$true)]
    [string]$Reason,
    [string]$OutputDirectory = "artifacts/lmax-readonly-runtime-securityid-planning/eurgbp-readiness",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=|raw\s*fix)'
$unsafeLanguagePattern = '(?i)(NewOrderSingle|OrderCancelRequest|OrderCancelReplaceRequest|TradeCapture|OrderStatus|SubmitOrder|production\s+(run|environment|authorization|execution)|uat\s+(run|environment|authorization|execution)|environmentName"?\s*[:=]\s*"?(Production|UAT)|run\s+is\s+authorized|external\s+run\s+authorized|can\s+run\s+external|batch\s+execution\s+allowed|automatic\s+retry|run\s+automatically|ReplaySubmitAsync|PeriodicTimer|LmaxScheduler|MarketDataPolling|SecurityListPolling)'

function Resolve-LocalPath([string]$PathValue) {
    if ([string]::IsNullOrWhiteSpace($PathValue)) { return "" }
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}

function Assert-SafeText([string]$Name, [string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { throw "$Name is required." }
    if ($Value -match $script:sensitivePattern) { throw "$Name contains credential-shaped content." }
    if ($Value -match $script:unsafeLanguagePattern) { throw "$Name contains unsafe authorization/runtime/trading language." }
}

function Read-Json([string]$PathValue, [string]$Label) {
    $resolved = Resolve-LocalPath $PathValue
    if (-not (Test-Path -LiteralPath $resolved)) { throw "$Label not found: $resolved" }
    $raw = Get-Content -LiteralPath $resolved -Raw
    if ($raw -match $script:sensitivePattern) { throw "$Label contains credential-shaped or raw FIX content." }
    return @{ Path = (Resolve-Path -LiteralPath $resolved).Path; Raw = $raw; Json = ($raw | ConvertFrom-Json) }
}

function Find-FirstBySymbol($Items, [string]$Symbol) {
    return @($Items | Where-Object { [string]$_.symbol -eq $Symbol })[0]
}

function Assert-False([object]$Value, [string]$Name) {
    if ([bool]$Value) { throw "$Name must remain false." }
}

Write-Host "LMAX Read-Only Phase 7E2 EURGBP Readiness Rehydration"
Write-Host "Local-only. This script does not connect to LMAX, request SecurityList, request snapshots, replay evidence, schedule work, or use credentials."

Assert-SafeText "RequestedByOperatorId" $RequestedByOperatorId
Assert-SafeText "Reason" $Reason

$phase7d = Read-Json $Phase7DDecisionFile "Phase 7D decision"
$pipelineRef = Read-Json $PipelineManifestFile "Pipeline manifest"
$planningRef = Read-Json $PlanningManifestFile "Planning manifest"
$safetyRef = Read-Json $SafetyGateManifestFile "Safety gate manifest"
$preflightRef = Read-Json $PreflightManifestFile "Preflight manifest"

$decision = $phase7d.Json
if ([string]$decision.decision -ne "ProceedToEurgbpPlanning") { throw "Phase 7D decision must be ProceedToEurgbpPlanning." }
if ([string]$decision.nextCandidateInstrument -ne "EURGBP") { throw "Phase 7D next candidate must be EURGBP." }
if ([string]$decision.currentInstrument -ne "GBPUSD") { throw "Phase 7D current instrument must be GBPUSD." }
if ([string]$decision.gbpusdClosureDecision -ne "PASS" -or [string]$decision.gbpusdClosureClassification -ne "CompletedWithBook") { throw "Phase 7D must be based on GBPUSD CompletedWithBook/PASS closure." }
Assert-False $decision.canRunExternalSnapshot "Phase7D canRunExternalSnapshot"
Assert-False $decision.batchExecutionAllowed "Phase7D batchExecutionAllowed"
if ([int]$decision.executableCount -ne 0) { throw "Phase 7D executableCount must be 0." }

$pipeline = $pipelineRef.Json
if ([string]$pipeline.finalDecision -ne "PASS" -or [int]$pipeline.executableCount -ne 0) { throw "Pipeline must be PASS with executableCount=0." }
$pipelineInstrument = Find-FirstBySymbol $pipeline.instruments "EURGBP"
if ($null -eq $pipelineInstrument) { throw "Pipeline manifest missing EURGBP." }
if ([string]$pipelineInstrument.slashSymbol -ne "EUR/GBP" -or [string]$pipelineInstrument.planningSecurityId -ne "4003" -or [string]$pipelineInstrument.securityIdSource -ne "8") { throw "Pipeline EURGBP identity mismatch." }
if ([string]$pipelineInstrument.safetyGateDecision -ne "PASS" -or [string]$pipelineInstrument.preflightDecision -ne "PASS" -or [string]$pipelineInstrument.approvalEnvelopeDecision -ne "AcceptedForPlanning" -or [string]$pipelineInstrument.dryRunDecision -ne "PASS" -or [string]$pipelineInstrument.attemptGateDecision -ne "PASS" -or [string]$pipelineInstrument.executionPlanDecision -ne "PASS" -or [string]$pipelineInstrument.operatorSignoffDecision -ne "SignedForPlanning" -or [string]$pipelineInstrument.finalReadinessDecision -ne "PASS") {
    throw "Pipeline EURGBP decisions are not all expected safe planning values."
}
Assert-False $pipelineInstrument.isApprovedForExternalRun "Pipeline EURGBP isApprovedForExternalRun"
Assert-False $pipelineInstrument.canRunExternalSnapshot "Pipeline EURGBP canRunExternalSnapshot"
Assert-False $pipelineInstrument.eligibleForManualSnapshotAttempt "Pipeline EURGBP eligibleForManualSnapshotAttempt"
Assert-False $pipelineInstrument.externalConnectionAttempted "Pipeline EURGBP externalConnectionAttempted"
Assert-False $pipelineInstrument.snapshotAttempted "Pipeline EURGBP snapshotAttempted"
Assert-False $pipelineInstrument.replayAttempted "Pipeline EURGBP replayAttempted"
Assert-False $pipelineInstrument.orderSubmissionAttempted "Pipeline EURGBP orderSubmissionAttempted"
Assert-False $pipelineInstrument.shadowReplaySubmitAttempted "Pipeline EURGBP shadowReplaySubmitAttempted"
Assert-False $pipelineInstrument.tradingMutationAttempted "Pipeline EURGBP tradingMutationAttempted"
Assert-False $pipelineInstrument.schedulerStarted "Pipeline EURGBP schedulerStarted"

$planningInstrument = Find-FirstBySymbol $planningRef.Json.instruments "EURGBP"
if ($null -eq $planningInstrument -or [string]$planningInstrument.planningSecurityId -ne "4003" -or [string]$planningInstrument.securityIdSource -ne "8" -or [string]$planningInstrument.decision -ne "AcceptedForPlanning") {
    throw "Planning manifest must contain EURGBP AcceptedForPlanning / SecurityID 4003 / source 8."
}
Assert-False $planningInstrument.isApprovedForExternalRun "Planning EURGBP isApprovedForExternalRun"

$safetyInstrument = Find-FirstBySymbol $safetyRef.Json.instruments "EURGBP"
if ($null -eq $safetyInstrument -or [string]$safetyInstrument.finalDecision -ne "PASS" -or [string]$safetyInstrument.planningSecurityId -ne "4003") {
    throw "Safety gate manifest must contain EURGBP PASS / SecurityID 4003."
}

$preflightInstrument = Find-FirstBySymbol $preflightRef.Json.results "EURGBP"
if ($null -eq $preflightInstrument -or [string]$preflightInstrument.finalDecision -ne "PASS" -or [string]$preflightInstrument.planningSecurityId -ne "4003") {
    throw "Preflight manifest must contain EURGBP PASS / SecurityID 4003."
}
Assert-False $preflightInstrument.canRunExternalSnapshot "Preflight EURGBP canRunExternalSnapshot"
Assert-False $preflightInstrument.isApprovedForExternalRun "Preflight EURGBP isApprovedForExternalRun"
Assert-False $preflightInstrument.eligibleForManualSnapshotAttempt "Preflight EURGBP eligibleForManualSnapshotAttempt"

foreach ($artifactPath in @(
    $pipelineInstrument.approvalEnvelopePath,
    $pipelineInstrument.dryRunReportPath,
    $pipelineInstrument.attemptGatePath,
    $pipelineInstrument.executionPlanPath,
    $pipelineInstrument.operatorSignoffPath,
    $pipelineInstrument.finalReadinessPath
)) {
    if (-not (Test-Path -LiteralPath $artifactPath)) { throw "Expected EURGBP planning artifact not found: $artifactPath" }
}

$stamp = [DateTimeOffset]::UtcNow.ToString("yyyyMMdd-HHmmss")
$artifact = [ordered]@{
    rehydrationId = "lmax-readonly-eurgbp-manual-snapshot-readiness-rehydration-$stamp"
    createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    requestedByOperatorId = $RequestedByOperatorId
    reason = $Reason
    sourcePhase7DDecisionFile = $phase7d.Path
    sourcePipelineManifestFile = $pipelineRef.Path
    sourcePlanningManifestFile = $planningRef.Path
    sourceSafetyGateManifestFile = $safetyRef.Path
    sourcePreflightManifestFile = $preflightRef.Path
    selectedInstrument = "EURGBP"
    slashSymbol = "EUR/GBP"
    securityId = "4003"
    securityIdSource = "8"
    environmentName = "Demo"
    venueProfileName = "DemoLondon"
    requestMode = "SnapshotPlusUpdates"
    symbolEncodingMode = "SecurityIdOnly"
    marketDepth = 1
    previousInstrument = "GBPUSD"
    previousInstrumentClosureDecision = "PASS"
    previousDecision = "ProceedToEurgbpPlanning"
    nextCandidateInstrument = "EURGBP"
    pipelineDecision = "PASS"
    planningDecision = "AcceptedForPlanning"
    safetyGateDecision = "PASS"
    preflightDecision = "PASS"
    approvalEnvelopeDecision = "AcceptedForPlanning"
    dryRunDecision = "PASS"
    attemptGateDecision = "PASS"
    executionPlanDecision = "PASS"
    operatorSignoffDecision = "SignedForPlanning"
    finalReadinessDecision = "PASS"
    approvalEnvelopePath = $pipelineInstrument.approvalEnvelopePath
    dryRunReportPath = $pipelineInstrument.dryRunReportPath
    attemptGatePath = $pipelineInstrument.attemptGatePath
    executionPlanPath = $pipelineInstrument.executionPlanPath
    operatorSignoffPath = $pipelineInstrument.operatorSignoffPath
    finalReadinessPath = $pipelineInstrument.finalReadinessPath
    oneInstrumentAtATime = $true
    batchExecutionAllowed = $false
    executableCount = 0
    isApprovedForExternalRun = $false
    canRunExternalSnapshot = $false
    eligibleForManualSnapshotAttempt = $false
    externalConnectionAttempted = $false
    snapshotAttempted = $false
    replayAttempted = $false
    orderSubmissionAttempted = $false
    shadowReplaySubmitAttempted = $false
    tradingMutationAttempted = $false
    schedulerStarted = $false
    noSensitiveContent = $true
    finalDecision = "PASS"
    notes = @(
        "Phase 7E2 rehydrates EURGBP planning readiness only.",
        "EURGBP remains non-executable; no external run is approved or attempted.",
        "One-instrument-at-a-time remains enforced after GBPUSD CompletedWithBook/PASS closure.",
        "No scheduler, polling, runtime shadow replay submit, orders, gateway registration, or trading mutation is authorized."
    )
}

$json = $artifact | ConvertTo-Json -Depth 20
if ($json -match $sensitivePattern) { throw "Generated EURGBP rehydration artifact contains credential-shaped content." }
if ($json -match $unsafeLanguagePattern) { throw "Generated EURGBP rehydration artifact contains unsafe authorization/runtime/trading language." }

$outDir = Resolve-LocalPath $OutputDirectory
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$jsonPath = Join-Path $outDir "lmax-readonly-eurgbp-manual-snapshot-readiness-rehydration-$stamp.json"
$mdPath = Join-Path $outDir "lmax-readonly-eurgbp-manual-snapshot-readiness-rehydration-$stamp.md"
if ((Test-Path -LiteralPath $jsonPath) -and -not $Force.IsPresent) { throw "Output already exists: $jsonPath" }

$json | Set-Content -LiteralPath $jsonPath -Encoding UTF8
@"
# EURGBP Manual Snapshot Readiness Rehydration

- Rehydration ID: $($artifact.rehydrationId)
- Selected instrument: EURGBP / EUR/GBP
- SecurityID: 4003
- SecurityIDSource: 8
- Environment: Demo / DemoLondon
- Request mode: SnapshotPlusUpdates
- Symbol encoding mode: SecurityIdOnly
- MarketDepth: 1
- Previous instrument: GBPUSD
- Previous closure decision: PASS
- Phase 7D decision: ProceedToEurgbpPlanning
- One instrument at a time: true
- Batch execution allowed: false
- Executable count: 0
- IsApprovedForExternalRun: false
- canRunExternalSnapshot: false
- eligibleForManualSnapshotAttempt: false
- Final decision: PASS

This artifact is planning-only. It does not authorize or perform an external run.
"@ | Set-Content -LiteralPath $mdPath -Encoding UTF8

Write-Host "SelectedInstrument: EURGBP"
Write-Host "SecurityId: 4003"
Write-Host "FinalDecision: PASS"
Write-Host "CanRunExternalSnapshot: false"
Write-Host "IsApprovedForExternalRun: false"
Write-Host "EligibleForManualSnapshotAttempt: false"
Write-Host "OneInstrumentAtATime: true"
Write-Host "OutputPath: $jsonPath"
Write-Host "MarkdownPath: $mdPath"
Write-Host "No external connection, SecurityListRequest, snapshot, replay, scheduler, polling, order submission, shadow replay submit, gateway registration, credential read, or trading mutation occurred."
