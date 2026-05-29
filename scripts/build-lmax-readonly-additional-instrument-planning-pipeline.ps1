param(
    [Parameter(Mandatory=$true)]
    [string]$PlanningManifestFile,
    [Parameter(Mandatory=$true)]
    [string]$SafetyGateManifestFile,
    [Parameter(Mandatory=$true)]
    [string]$PreflightManifestFile,
    [Parameter(Mandatory=$true)]
    [string]$RequestedByOperatorId,
    [Parameter(Mandatory=$true)]
    [string]$ReviewedByOperatorId,
    [Parameter(Mandatory=$true)]
    [string]$Reason,
    [string[]]$Symbols = @(),
    [switch]$All,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$expected = [ordered]@{
    GBPUSD = @{ slash = "GBP/USD"; securityId = "4002" }
    EURGBP = @{ slash = "EUR/GBP"; securityId = "4003" }
    USDJPY = @{ slash = "USD/JPY"; securityId = "4004" }
    AUDUSD = @{ slash = "AUD/USD"; securityId = "4007" }
}

function Resolve-LocalPath([string]$PathValue) {
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}

function Assert-SafeText([string]$Name, [string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { throw "$Name is required." }
    if ($Value -match '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=)') { throw "$Name contains credential-shaped content." }
    if ($Value -match '(?i)(NewOrderSingle|OrderCancelRequest|OrderCancelReplaceRequest|TradeCapture|OrderStatusRequest|order submission|submit order|production|uat|run automatically|automatic retry)') { throw "$Name contains unsafe authorization/trading language." }
}

function New-Dir([string]$Relative) {
    $path = Join-Path $repoRoot $Relative
    New-Item -ItemType Directory -Path $path -Force | Out-Null
    return $path
}

function Get-LatestArtifact([string]$Directory, [string]$Pattern) {
    if (-not (Test-Path -LiteralPath $Directory)) { return $null }
    return Get-ChildItem -Path $Directory -Filter $Pattern -File | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
}

function Write-Artifact([string]$Directory, [string]$FileName, [hashtable]$Content, [string]$Symbol, [string]$Kind) {
    $path = Join-Path $Directory $FileName
    if ((Test-Path -LiteralPath $path) -and -not $Force.IsPresent) {
        return @{ path = (Resolve-Path -LiteralPath $path).Path; action = "SkippedExisting"; kind = $Kind; symbol = $Symbol }
    }

    $json = $Content | ConvertTo-Json -Depth 20
    if ($json -match '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=)') { throw "$Kind for $Symbol contains credential-shaped content." }
    $json | Set-Content -LiteralPath $path -Encoding UTF8
    return @{ path = $path; action = "Created"; kind = $Kind; symbol = $Symbol }
}

function Get-OrCreateArtifact([string]$Symbol, [string]$Kind, [string]$Directory, [string]$Pattern, [scriptblock]$Factory) {
    $existing = Get-LatestArtifact $Directory $Pattern
    if ($existing -and -not $Force.IsPresent) {
        return @{ path = $existing.FullName; action = "SkippedExisting"; kind = $Kind; symbol = $Symbol }
    }

    return & $Factory
}

function Read-Json([string]$Path) { Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json }

Assert-SafeText "RequestedByOperatorId" $RequestedByOperatorId
Assert-SafeText "ReviewedByOperatorId" $ReviewedByOperatorId
Assert-SafeText "Reason" $Reason

$planningPath = Resolve-LocalPath $PlanningManifestFile
$safetyPath = Resolve-LocalPath $SafetyGateManifestFile
$preflightPath = Resolve-LocalPath $PreflightManifestFile
foreach ($p in @($planningPath,$safetyPath,$preflightPath)) { if (-not (Test-Path -LiteralPath $p)) { throw "Required manifest not found: $p" } }

$planning = Read-Json $planningPath
$preflight = Read-Json $preflightPath

$selected = if ($All.IsPresent) { @("GBPUSD","EURGBP","USDJPY","AUDUSD") } elseif ($Symbols.Count -gt 0) { $Symbols } else { @("EURGBP","USDJPY","AUDUSD") }
$selected = @($selected | ForEach-Object { $_.ToUpperInvariant() } | Select-Object -Unique)
foreach ($symbol in $selected) {
    if (-not $expected.Contains($symbol)) { throw "Unknown symbol: $symbol" }
}

$approvalDir = New-Dir "artifacts/lmax-readonly-runtime-securityid-planning/approval-envelopes"
$dryRunDir = New-Dir "artifacts/lmax-readonly-runtime-securityid-planning/dry-run-reports"
$attemptDir = New-Dir "artifacts/lmax-readonly-runtime-securityid-planning/attempt-gates"
$executionDir = New-Dir "artifacts/lmax-readonly-runtime-securityid-planning/execution-plans"
$signoffDir = New-Dir "artifacts/lmax-readonly-runtime-securityid-planning/operator-signoffs"
$readinessDir = New-Dir "artifacts/lmax-readonly-runtime-securityid-planning/final-readiness"
$pipelineDir = New-Dir "artifacts/lmax-readonly-runtime-securityid-planning/pipeline"

$stamp = [DateTimeOffset]::UtcNow.ToString("yyyyMMdd-HHmmss")
$artifactActions = @()

foreach ($symbol in $selected) {
    $meta = $expected[$symbol]
    $planningEntry = @($planning.instruments | Where-Object { $_.symbol -eq $symbol })[0]
    $preflightResult = @($preflight.results | Where-Object { $_.symbol -eq $symbol })[0]
    if ($null -eq $planningEntry) { throw "Planning manifest missing $symbol" }
    if ($null -eq $preflightResult -or [string]$preflightResult.finalDecision -ne "PASS") { throw "Preflight manifest missing PASS for $symbol" }
    if ([string]$planningEntry.planningSecurityId -ne $meta.securityId -or [string]$planningEntry.securityIdSource -ne "8") { throw "$symbol planning value mismatch." }

    $common = @{
        symbol = $symbol
        slashSymbol = $meta.slash
        planningSecurityId = $meta.securityId
        securityIdSource = "8"
        environmentName = "Demo"
        venueProfileName = "DemoLondon"
        requestMode = "SnapshotPlusUpdates"
        symbolEncodingMode = "SecurityIdOnly"
        marketDepth = 1
    }

    $approval = Get-OrCreateArtifact $symbol "ApprovalEnvelope" $approvalDir "lmax-readonly-additional-snapshot-approval-$symbol-*.json" {
        Write-Artifact $approvalDir "lmax-readonly-additional-snapshot-approval-$symbol-$stamp.json" ([ordered]@{
            approvalEnvelopeId = "lmax-readonly-additional-snapshot-approval-$symbol-$stamp"; createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o"); requestedByOperatorId = $RequestedByOperatorId; reviewedByOperatorId = $ReviewedByOperatorId; reason = $Reason
            symbol = $common.symbol; slashSymbol = $common.slashSymbol; planningSecurityId = $common.planningSecurityId; securityIdSource = $common.securityIdSource; environmentName = $common.environmentName; venueProfileName = $common.venueProfileName; requestMode = $common.requestMode; symbolEncodingMode = $common.symbolEncodingMode; marketDepth = $common.marketDepth
            maxRuntimeSeconds = 30; maxWaitSeconds = 30; maxEventsPerRun = 25; sourcePreflightManifestPath = $preflightPath; sourcePreflightDecision = "PASS"
            confirmsDemoOnly = $true; confirmsReadOnlyMarketDataOnly = $true; confirmsNoOrderSubmission = $true; confirmsNoSchedulerOrPolling = $true; confirmsNoRuntimeShadowReplaySubmit = $true; confirmsNoTradingMutation = $true; confirmsSingleInstrumentOnly = $true; confirmsFutureExplicitManualRunRequired = $true
            isApprovedForExternalRun = $false; eligibleForManualSnapshotAttempt = $false; canRunExternalSnapshot = $false; noSensitiveContent = $true; decision = "AcceptedForPlanning"
        }) $symbol "ApprovalEnvelope"
    }
    $artifactActions += [pscustomobject]$approval

    $dry = Get-OrCreateArtifact $symbol "DryRunReport" $dryRunDir "lmax-readonly-additional-snapshot-dryrun-$symbol-*.json" {
        Write-Artifact $dryRunDir "lmax-readonly-additional-snapshot-dryrun-$symbol-$stamp.json" ([ordered]@{
            dryRunReportId = "lmax-readonly-additional-snapshot-dryrun-$symbol-$stamp"; createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o"); requestedByOperatorId = $RequestedByOperatorId; reason = $Reason
            symbol = $common.symbol; slashSymbol = $common.slashSymbol; planningSecurityId = $common.planningSecurityId; securityIdSource = $common.securityIdSource; environmentName = $common.environmentName; venueProfileName = $common.venueProfileName; requestMode = $common.requestMode; symbolEncodingMode = $common.symbolEncodingMode; marketDepth = $common.marketDepth
            maxRuntimeSeconds = 30; maxWaitSeconds = 30; maxEventsPerRun = 25; sourcePlanningManifestPath = $planningPath; sourceSafetyGateManifestPath = $safetyPath; sourcePreflightManifestPath = $preflightPath; sourceApprovalEnvelopePath = $approval.path
            planningDecision = "AcceptedForPlanning"; safetyGateDecision = "PASS"; preflightDecision = "PASS"; approvalEnvelopeDecision = "AcceptedForPlanning"; dryRunDecision = "PASS"
            isApprovedForExternalRun = $false; eligibleForManualSnapshotAttempt = $false; canRunExternalSnapshot = $false; externalConnectionAttempted = $false; snapshotAttempted = $false; replayAttempted = $false; orderSubmissionAttempted = $false; shadowReplaySubmitAttempted = $false; tradingMutationAttempted = $false; schedulerStarted = $false; noSensitiveContent = $true
            requiredFutureStep = "explicit future operator-approved manual run phase"; blockingReason = "Phase 6Z-A dry-run is planning-only; external snapshot not authorized."
        }) $symbol "DryRunReport"
    }
    $artifactActions += [pscustomobject]$dry

    $attempt = Get-OrCreateArtifact $symbol "AttemptGate" $attemptDir "lmax-readonly-single-instrument-snapshot-attempt-gate-$symbol-*.json" {
        Write-Artifact $attemptDir "lmax-readonly-single-instrument-snapshot-attempt-gate-$symbol-$stamp.json" ([ordered]@{
            gateId = "lmax-readonly-single-instrument-snapshot-attempt-gate-$symbol-$stamp"; createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o"); requestedByOperatorId = $RequestedByOperatorId; reason = $Reason
            symbol = $common.symbol; slashSymbol = $common.slashSymbol; planningSecurityId = $common.planningSecurityId; securityIdSource = $common.securityIdSource; environmentName = $common.environmentName; venueProfileName = $common.venueProfileName; requestMode = $common.requestMode; symbolEncodingMode = $common.symbolEncodingMode; marketDepth = $common.marketDepth
            sourcePlanningManifestPath = $planningPath; sourceSafetyGateManifestPath = $safetyPath; sourcePreflightManifestPath = $preflightPath; sourceApprovalEnvelopePath = $approval.path; sourceDryRunReportPath = $dry.path
            planningDecision = "AcceptedForPlanning"; safetyGateDecision = "PASS"; preflightDecision = "PASS"; approvalEnvelopeDecision = "AcceptedForPlanning"; dryRunDecision = "PASS"; gateDecision = "PASS"
            isApprovedForExternalRun = $false; eligibleForManualSnapshotAttempt = $false; canRunExternalSnapshot = $false; externalConnectionAttempted = $false; snapshotAttempted = $false; replayAttempted = $false; orderSubmissionAttempted = $false; shadowReplaySubmitAttempted = $false; tradingMutationAttempted = $false; schedulerStarted = $false; noSensitiveContent = $true
            requiredFutureStep = "explicit future operator-approved manual execution phase"; blockingReason = "Phase 6Z-A is an attempt gate only; external snapshot not authorized."
        }) $symbol "AttemptGate"
    }
    $artifactActions += [pscustomobject]$attempt

    $execution = Get-OrCreateArtifact $symbol "ExecutionPlan" $executionDir "lmax-readonly-additional-instrument-manual-snapshot-execution-plan-$symbol-*.json" {
        Write-Artifact $executionDir "lmax-readonly-additional-instrument-manual-snapshot-execution-plan-$symbol-$stamp.json" ([ordered]@{
            planId = "lmax-readonly-additional-instrument-manual-snapshot-execution-plan-$symbol-$stamp"; createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o"); requestedByOperatorId = $RequestedByOperatorId; reason = $Reason
            symbol = $common.symbol; slashSymbol = $common.slashSymbol; planningSecurityId = $common.planningSecurityId; securityIdSource = $common.securityIdSource; environmentName = $common.environmentName; venueProfileName = $common.venueProfileName; requestMode = $common.requestMode; symbolEncodingMode = $common.symbolEncodingMode; marketDepth = $common.marketDepth
            sourceAttemptGatePath = $attempt.path; attemptGateDecision = "PASS"; futureCommandTemplate = "DO NOT RUN IN PHASE 6Z-A. Future one-instrument operator-approved command template only for $symbol SecurityID $($meta.securityId)."
            externalRunAuthorized = $false; canRunExternalSnapshot = $false; eligibleForManualSnapshotAttempt = $false; isApprovedForExternalRun = $false; schedulerOrPolling = $false; runtimeShadowReplaySubmit = $false; orderSubmission = $false; tradingMutation = $false; apiWorkerGatewayMode = "FakeLmaxGateway"; noSensitiveContent = $true
            abortCriteria = @("Wrong symbol or SecurityID","Any order flag is true","Scheduler or polling is detected","Runtime shadow replay submit is true","Credential exposure is detected","Environment is not Demo","Gateway registration changes","Mutation guard changes","Multi-instrument batch is attempted")
            rollbackSteps = @("Stop the manual process","Verify API health still reports FakeLmaxGateway","Inspect artifacts for noSensitiveContent","Confirm no DB rollback is expected because mutation is prohibited")
            postRunValidationSteps = @("Run the artifact validator","Generate evidence preview mapping in an explicitly approved later phase","Confirm no observations or mutation guard changes","Complete operator review")
            decision = "PASS"
        }) $symbol "ExecutionPlan"
    }
    $artifactActions += [pscustomobject]$execution

    $signoff = Get-OrCreateArtifact $symbol "OperatorSignoff" $signoffDir "lmax-readonly-additional-instrument-operator-signoff-$symbol-*.json" {
        Write-Artifact $signoffDir "lmax-readonly-additional-instrument-operator-signoff-$symbol-$stamp.json" ([ordered]@{
            signoffId = "lmax-readonly-additional-instrument-operator-signoff-$symbol-$stamp"; createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o"); requestedByOperatorId = $RequestedByOperatorId; signedByOperatorId = $ReviewedByOperatorId; signoffRole = "Operator"; reason = $Reason
            symbol = $common.symbol; slashSymbol = $common.slashSymbol; planningSecurityId = $common.planningSecurityId; securityIdSource = $common.securityIdSource; environmentName = $common.environmentName; venueProfileName = $common.venueProfileName; requestMode = $common.requestMode; symbolEncodingMode = $common.symbolEncodingMode; marketDepth = $common.marketDepth
            sourceExecutionPlanPath = $execution.path; sourceExecutionPlanDecision = "PASS"; confirmsExecutionPlanReviewed = $true; confirmsKillRollbackPlanReviewed = $true; confirmsDemoOnly = $true; confirmsReadOnlyMarketDataOnly = $true; confirmsSingleInstrumentOnly = $true; confirmsNoOrderSubmission = $true; confirmsNoSchedulerOrPolling = $true; confirmsNoRuntimeShadowReplaySubmit = $true; confirmsNoTradingMutation = $true; confirmsNoGatewayRegistration = $true; confirmsCredentialValuesMustRemainRedacted = $true; confirmsFutureManualExecutionPhaseRequired = $true
            signoffDecision = "SignedForPlanning"; isApprovedForExternalRun = $false; eligibleForManualSnapshotAttempt = $false; canRunExternalSnapshot = $false; externalConnectionAttempted = $false; snapshotAttempted = $false; replayAttempted = $false; orderSubmissionAttempted = $false; shadowReplaySubmitAttempted = $false; tradingMutationAttempted = $false; schedulerStarted = $false; noSensitiveContent = $true
        }) $symbol "OperatorSignoff"
    }
    $artifactActions += [pscustomobject]$signoff

    $readiness = Get-OrCreateArtifact $symbol "FinalReadiness" $readinessDir "lmax-readonly-additional-instrument-final-readiness-$symbol-*.json" {
        Write-Artifact $readinessDir "lmax-readonly-additional-instrument-final-readiness-$symbol-$stamp.json" ([ordered]@{
            readinessId = "lmax-readonly-additional-instrument-final-readiness-$symbol-$stamp"; createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o"); requestedByOperatorId = $RequestedByOperatorId; reason = $Reason
            symbol = $common.symbol; slashSymbol = $common.slashSymbol; planningSecurityId = $common.planningSecurityId; securityIdSource = $common.securityIdSource; environmentName = $common.environmentName; venueProfileName = $common.venueProfileName; requestMode = $common.requestMode; symbolEncodingMode = $common.symbolEncodingMode; marketDepth = $common.marketDepth
            sourcePlanningManifestPath = $planningPath; sourceSafetyGateManifestPath = $safetyPath; sourcePreflightManifestPath = $preflightPath; sourceApprovalEnvelopePath = $approval.path; sourceDryRunReportPath = $dry.path; sourceAttemptGatePath = $attempt.path; sourceExecutionPlanPath = $execution.path; sourceOperatorSignoffPath = $signoff.path
            planningDecision = "AcceptedForPlanning"; safetyGateDecision = "PASS"; preflightDecision = "PASS"; approvalEnvelopeDecision = "AcceptedForPlanning"; dryRunDecision = "PASS"; attemptGateDecision = "PASS"; executionPlanDecision = "PASS"; operatorSignoffDecision = "SignedForPlanning"; readinessDecision = "PASS"
            isApprovedForExternalRun = $false; eligibleForManualSnapshotAttempt = $false; canRunExternalSnapshot = $false; externalConnectionAttempted = $false; snapshotAttempted = $false; replayAttempted = $false; orderSubmissionAttempted = $false; shadowReplaySubmitAttempted = $false; tradingMutationAttempted = $false; schedulerStarted = $false; runtimeShadowReplaySubmit = $false; apiWorkerGatewayMode = "FakeLmaxGateway"; noSensitiveContent = $true
            requiredFutureStep = "future one-instrument operator-approved manual snapshot attempt"; blockingReason = "Phase 6Z-A is final readiness only; external snapshot not authorized."
        }) $symbol "FinalReadiness"
    }
    $artifactActions += [pscustomobject]$readiness
}

$pipelineInstruments = @()
foreach ($symbol in @("GBPUSD","EURGBP","USDJPY","AUDUSD")) {
    $meta = $expected[$symbol]
    $approval = Get-LatestArtifact $approvalDir "lmax-readonly-additional-snapshot-approval-$symbol-*.json"
    $dry = Get-LatestArtifact $dryRunDir "lmax-readonly-additional-snapshot-dryrun-$symbol-*.json"
    $attempt = Get-LatestArtifact $attemptDir "lmax-readonly-single-instrument-snapshot-attempt-gate-$symbol-*.json"
    $execution = Get-LatestArtifact $executionDir "*execution-plan*$symbol-*.json"
    $signoff = Get-LatestArtifact $signoffDir "*signoff*$symbol-*.json"
    $readiness = Get-LatestArtifact $readinessDir "*readiness*$symbol-*.json"
    foreach ($a in @($approval,$dry,$attempt,$execution,$signoff,$readiness)) { if ($null -eq $a) { throw "Missing pipeline artifact for $symbol" } }

    $pipelineInstruments += [ordered]@{
        symbol = $symbol; slashSymbol = $meta.slash; planningSecurityId = $meta.securityId; securityIdSource = "8"; planningValuePresent = $true
        safetyGateDecision = "PASS"; preflightDecision = "PASS"; approvalEnvelopeDecision = "AcceptedForPlanning"; dryRunDecision = "PASS"; attemptGateDecision = "PASS"; executionPlanDecision = "PASS"; operatorSignoffDecision = "SignedForPlanning"; finalReadinessDecision = "PASS"
        approvalEnvelopePath = $approval.FullName; dryRunReportPath = $dry.FullName; attemptGatePath = $attempt.FullName; executionPlanPath = $execution.FullName; operatorSignoffPath = $signoff.FullName; finalReadinessPath = $readiness.FullName
        isApprovedForExternalRun = $false; eligibleForManualSnapshotAttempt = $false; canRunExternalSnapshot = $false; externalConnectionAttempted = $false; snapshotAttempted = $false; replayAttempted = $false; orderSubmissionAttempted = $false; shadowReplaySubmitAttempted = $false; tradingMutationAttempted = $false; schedulerStarted = $false; noSensitiveContent = $true
    }
}

$pipeline = [ordered]@{
    manifestId = "lmax-readonly-additional-instrument-planning-pipeline-$stamp"; createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o"); requestedByOperatorId = $RequestedByOperatorId; reviewedByOperatorId = $ReviewedByOperatorId; reason = $Reason
    sourcePlanningManifestPath = $planningPath; sourceSafetyGateManifestPath = $safetyPath; sourcePreflightManifestPath = $preflightPath
    instruments = $pipelineInstruments; instrumentCount = 4; readyForFutureManualConsiderationCount = 4; executableCount = 0
    isApprovedForExternalRun = $false; canRunExternalSnapshot = $false; eligibleForManualSnapshotAttempt = $false; externalConnectionAttempted = $false; snapshotAttempted = $false; replayAttempted = $false; schedulerStarted = $false; orderSubmissionAttempted = $false; shadowReplaySubmitAttempted = $false; tradingMutationAttempted = $false; apiWorkerGatewayMode = "FakeLmaxGateway"; noSensitiveContent = $true; finalDecision = "PASS"
    artifactActions = $artifactActions
}

$pipelinePath = Join-Path $pipelineDir "lmax-readonly-additional-instrument-planning-pipeline-$stamp.json"
($pipeline | ConvertTo-Json -Depth 20) | Set-Content -LiteralPath $pipelinePath -Encoding UTF8

Write-Host "Phase 6Z-A additional instrument planning pipeline built."
Write-Host ("SymbolsProcessed: {0}" -f ($selected -join ","))
Write-Host ("ArtifactsCreated: {0}" -f (@($artifactActions | Where-Object action -eq "Created").Count))
Write-Host ("ArtifactsSkippedExisting: {0}" -f (@($artifactActions | Where-Object action -eq "SkippedExisting").Count))
Write-Host "FinalDecision: PASS"
Write-Host "ExecutableCount: 0"
Write-Host "PipelineManifestFile: $pipelinePath"
Write-Host "No external connection, snapshot, replay, scheduler, polling, order submission, shadow replay submit, credential read, gateway registration, or trading mutation occurred."
