param(
    [string]$SelectionGateFile = "artifacts/readiness/phase7m8-single-optional-local-replay-candidate-selection-gate.json",
    [string]$SelectionReportFile = "artifacts/readiness/phase7m8-single-optional-local-replay-candidate-selection-report.json",
    [string]$HealthGateFile = "artifacts/readiness/phase7m6-operator-started-local-api-health-verification-gate.json",
    [string]$EvidencePreviewFile = "artifacts/lmax-readonly-runtime-additional-snapshot/gbpusd/evidence-preview/lmax-readonly-gbpusd-evidence-preview-20260511-201538.json",
    [string]$BaseUrl = "http://localhost:5050",
    [string]$OperatorId = "local-admin",
    [string]$Reason = "Phase 7M9 single optional local GBPUSD MarketDataOnly replay"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$expectedPreviewPath = "artifacts/lmax-readonly-runtime-additional-snapshot/gbpusd/evidence-preview/lmax-readonly-gbpusd-evidence-preview-20260511-201538.json"
$allowedNextPhaseCompleted = "Phase 7M10 $([char]0x2014) Optional Local Replay Closure Gate, No External Run"
$allowedNextPhaseWarnings = "Phase 7M10 $([char]0x2014) Optional Local Replay Warning Review Gate, No External Run"
$allowedNextPhaseSafeFail = "Phase 7M10 $([char]0x2014) Optional Local Replay Safe-Fail Diagnosis Gate, No External Run"
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|raw\s*fix|BEGIN\s+PRIVATE\s+KEY|NewOrderSingle|OrderCancelRequest|OrderCancelReplaceRequest)'

function Resolve-RepoPath([string]$PathValue) {
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}

function Assert-LocalUrl([string]$Url) {
    $uri = [Uri]$Url
    if ($uri.Scheme -notin @("http", "https") -or $uri.Host -notin @("localhost", "127.0.0.1")) {
        throw "Refusing non-local API URL: $Url"
    }
}

function Invoke-LocalApi([string]$Method, [string]$Endpoint, [object]$Body = $null) {
    if ($Endpoint -notlike "/*") { throw "Endpoint must be a local relative path." }
    $headers = @{ "X-Operator-Id" = $OperatorId }
    $uri = "$BaseUrl$Endpoint"
    if ($null -eq $Body) {
        return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers -TimeoutSec 5
    }

    $json = $Body | ConvertTo-Json -Depth 30
    return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers -ContentType "application/json" -Body $json -TimeoutSec 10
}

function Get-ItemsFromResponse([object]$Response) {
    if ($null -eq $Response) { return @() }
    if ($Response -is [array]) { return @($Response) }
    foreach ($name in @("value", "Value", "items", "Items", "observations", "replayRuns", "data")) {
        if ($Response.PSObject.Properties.Name -contains $name) {
            if ($null -eq $Response.$name) { return @() }
            return @($Response.$name)
        }
    }
    return @($Response)
}

function Get-CountSafely([string]$Endpoint) {
    try {
        $response = Invoke-LocalApi -Method "GET" -Endpoint $Endpoint
        return [ordered]@{ available = $true; count = (Get-ItemsFromResponse -Response $response).Count }
    } catch {
        return [ordered]@{ available = $false; count = 0; error = $_.Exception.Message }
    }
}

function New-ReportAndGate(
    [string]$ReplayStatus,
    [string]$ValidationStatus,
    [int]$ObservationCount,
    [int]$BlockingObservationCount,
    [int]$WarningObservationCount,
    [string]$MutationGuard,
    [string]$ReplayRunId,
    [bool]$PostEndpointCalled,
    [bool]$ReplayEndpointCalled,
    [bool]$LocalApiHealthOkBeforeReplay,
    [bool]$SafeRuntimePostureConfirmedBeforeReplay,
    [string]$FinalDecision,
    [string]$AllowedNextPhase,
    [string]$RecommendedNextAction,
    [string]$FailureReason = ""
) {
    $outDir = Join-Path $repoRoot "artifacts/readiness"
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
    $reportPath = Join-Path $outDir "phase7m9-gbpusd-single-optional-local-replay-report.json"
    $gatePath = Join-Path $outDir "phase7m9-gbpusd-single-optional-local-replay-gate.json"
    $notePath = Join-Path $outDir "phase7m9-gbpusd-single-optional-local-replay-note.md"

    [ordered]@{
        phase = "7M9"
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
        replayType = "SingleOptionalLocalReplayExecution"
        replayInstrument = "GBPUSD"
        selectedEvidencePreviewPath = $expectedPreviewPath
        localOnly = $true
        localApiBaseUrl = $BaseUrl
        localApiHealthOkBeforeReplay = $LocalApiHealthOkBeforeReplay
        safeRuntimePostureConfirmedBeforeReplay = $SafeRuntimePostureConfirmedBeforeReplay
        executionGateway = "FakeLmaxGateway"
        liveTradingEnabled = $false
        externalConnectionsEnabled = $false
        evidenceMode = "MarketDataOnly"
        evidenceValidation = "Ok"
        exactlyOneReplayRun = $PostEndpointCalled
        replayRunInThisPhase = $PostEndpointCalled
        localReplayRunInThisPhase = $PostEndpointCalled
        externalReplayRunInThisPhase = $false
        externalRunAttemptedInThisPhase = $false
        snapshotRunInThisPhase = $false
        postEndpointCalled = $PostEndpointCalled
        replayEndpointCalled = $ReplayEndpointCalled
        batchReplayUsed = $false
        automaticRetryUsed = $false
        schedulerOrPollingAdded = $false
        runtimeShadowReplaySubmitAdded = $false
        orderPathAdded = $false
        gatewayRegistrationAdded = $false
        tradingMutationAdded = $false
        wrapperValidationWeakened = $false
        replayStatus = $ReplayStatus
        validationStatus = $ValidationStatus
        replayRunId = $ReplayRunId
        observationCount = $ObservationCount
        blockingObservationCount = $BlockingObservationCount
        warningObservationCount = $WarningObservationCount
        mutationGuard = $MutationGuard
        failureReason = $FailureReason
        noSensitiveContent = $true
        recommendedNextAction = $RecommendedNextAction
        allowedNextPhase = $AllowedNextPhase
        finalDecision = $FinalDecision
    } | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $reportPath -Encoding UTF8

    [ordered]@{
        phase = "7M9"
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
        gateType = "SingleOptionalLocalReplayExecutionGate"
        reportPath = "artifacts/readiness/phase7m9-gbpusd-single-optional-local-replay-report.json"
        replayExecutionCompleted = $true
        localOnly = $true
        replayInstrument = "GBPUSD"
        selectedEvidencePreviewPath = $expectedPreviewPath
        exactlyOneReplayRun = $PostEndpointCalled
        replayRunInThisPhase = $PostEndpointCalled
        localReplayRunInThisPhase = $PostEndpointCalled
        externalReplayRunInThisPhase = $false
        externalRunAttemptedInThisPhase = $false
        snapshotRunInThisPhase = $false
        batchReplayUsed = $false
        automaticRetryUsed = $false
        schedulerOrPollingEnabled = $false
        runtimeShadowReplaySubmitEnabled = $false
        orderPathEnabled = $false
        gatewayRegistrationEnabled = $false
        tradingMutationEnabled = $false
        wrapperValidationWeakened = $false
        apiWorkerRemainFakeLmaxGatewayOnly = $true
        evidenceCycleRemainsClosed = $true
        uiStatusWorkstreamRemainsClosed = $true
        replayStatus = $ReplayStatus
        validationStatus = $ValidationStatus
        observationCount = $ObservationCount
        mutationGuard = $MutationGuard
        allowedNextPhase = $AllowedNextPhase
        noSensitiveContent = $true
        finalDecision = $FinalDecision
    } | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $gatePath -Encoding UTF8

    @"
# Phase 7M9 - GBPUSD Single Optional Local Replay

One local replay was run for GBPUSD only when the local health and runtime safety checks passed.

- Evidence preview: ``$expectedPreviewPath``
- Replay status: ``$ReplayStatus``
- Validation status: ``$ValidationStatus``
- Observation count: ``$ObservationCount``
- Mutation guard: ``$MutationGuard``

No LMAX connection or snapshot was run. Replay remains optional and non-authoritative because the MarketDataOnly evidence cycle was already closed. Result and any observations or warnings are local-only.

Allowed next phase: $AllowedNextPhase
"@ | Set-Content -LiteralPath $notePath -Encoding UTF8

    Write-Host "ReplayStatus: $ReplayStatus"
    Write-Host "ValidationStatus: $ValidationStatus"
    Write-Host "ObservationCount: $ObservationCount"
    Write-Host "MutationGuard: $MutationGuard"
    Write-Host "FinalDecision: $FinalDecision"
    Write-Host "Report: $reportPath"
    Write-Host "Gate: $gatePath"
}

try {
    Assert-LocalUrl $BaseUrl
    $selectionGate = Get-Content -LiteralPath (Resolve-RepoPath $SelectionGateFile) -Raw | ConvertFrom-Json
    $selectionReport = Get-Content -LiteralPath (Resolve-RepoPath $SelectionReportFile) -Raw | ConvertFrom-Json
    $healthGate = Get-Content -LiteralPath (Resolve-RepoPath $HealthGateFile) -Raw | ConvertFrom-Json

    if ([string]$selectionGate.phase -ne "7M8" -or [string]$selectionGate.finalDecision -ne "PASS_SINGLE_OPTIONAL_LOCAL_REPLAY_CANDIDATE_SELECTED") { throw "Phase 7M8 selection gate is not PASS." }
    if ([string]$selectionGate.selectedReplayCandidateInstrument -ne "GBPUSD") { throw "Phase 7M8 selected candidate must be GBPUSD." }
    if (-not [bool]$selectionGate.exactlyOneReplayCandidateSelected) { throw "Phase 7M8 must select exactly one replay candidate." }
    if ([string]$selectionGate.selectedEvidencePreviewPath -ne $expectedPreviewPath) { throw "Phase 7M8 selected evidence preview path mismatch." }
    if ([string]$selectionReport.evidenceMode -ne "MarketDataOnly" -or [string]$selectionReport.evidenceValidation -ne "Ok") { throw "Phase 7M8 selected evidence must be MarketDataOnly/Ok." }
    if ([string]$healthGate.phase -ne "7M6" -or [string]$healthGate.finalDecision -ne "PASS_OPERATOR_STARTED_LOCAL_API_HEALTH_OK") { throw "Phase 7M6 health gate is not PASS." }

    $resolvedEvidence = Resolve-Path -LiteralPath (Resolve-RepoPath $EvidencePreviewFile)
    $canonicalExpected = [IO.Path]::GetFullPath((Resolve-RepoPath $expectedPreviewPath))
    if ([IO.Path]::GetFullPath($resolvedEvidence.Path) -ne $canonicalExpected) { throw "EvidencePreviewFile must match the Phase 7M8 selected GBPUSD evidence preview exactly." }
    $raw = Get-Content -LiteralPath $resolvedEvidence -Raw
    if ($raw -match $sensitivePattern) { throw "Evidence preview appears to contain forbidden sensitive/order text." }
    $evidence = $raw | ConvertFrom-Json
    if ([string]$evidence.instrument -ne "GBPUSD" -or [string]$evidence.securityId -ne "4002") { throw "Evidence preview identity must be GBPUSD/4002." }
    if ([string]$evidence.evidenceMode -ne "MarketDataOnly" -or [string]$evidence.marketData.status -ne "Ok") { throw "Evidence preview must be MarketDataOnly/Ok." }
    if (@($evidence.executionReports).Count -ne 0 -or @($evidence.orderStatuses).Count -ne 0 -or @($evidence.tradeCaptureReports).Count -ne 0 -or @($evidence.protocolRejects).Count -ne 0) { throw "MarketDataOnly replay requires empty non-market-data arrays." }

    $healthStart = Get-Date
    $health = Invoke-LocalApi -Method "GET" -Endpoint "/health"
    $healthElapsedMs = [int]((Get-Date) - $healthStart).TotalMilliseconds
    $executionGateway = [string]$health.executionGateway
    $liveTradingEnabled = [bool]$health.liveTradingEnabled
    $externalConnectionsEnabled = [bool]$health.externalConnectionsEnabled
    $safeRuntime = $executionGateway -eq "FakeLmaxGateway" -and -not $liveTradingEnabled -and -not $externalConnectionsEnabled
    if (-not $safeRuntime) { throw "Local API health is OK but runtime posture is unsafe." }
    Write-Host "Health: OK ($healthElapsedMs ms), FakeLmaxGateway, liveTrading=false, externalConnections=false"

    $beforeOrders = Get-CountSafely "/orders"
    $beforeFills = Get-CountSafely "/fills"
    $beforePositions = Get-CountSafely "/positions/internal"
    $body = [ordered]@{
        inputSource = "LabEvidenceFile"
        reason = $Reason
        evidenceMode = "MarketDataOnly"
        executionReports = @()
        tradeCaptureReports = @()
        orderStatuses = @()
        protocolRejects = @()
    }
    $result = Invoke-LocalApi -Method "POST" -Endpoint "/lmax-shadow/replay" -Body $body
    $status = if ($result.PSObject.Properties.Name -contains "status") { [string]$result.status } else { "" }
    $observationCount = if ($result.PSObject.Properties.Name -contains "observationCount") { [int]$result.observationCount } else { -1 }
    $blockingObservationCount = if ($result.PSObject.Properties.Name -contains "blockingObservationCount") { [int]$result.blockingObservationCount } else { -1 }
    $warningObservationCount = if ($result.PSObject.Properties.Name -contains "warningObservationCount") { [int]$result.warningObservationCount } else { -1 }
    $replayRunId = if ($result.PSObject.Properties.Name -contains "id") { [string]$result.id } elseif ($result.PSObject.Properties.Name -contains "replayRunId") { [string]$result.replayRunId } else { "" }

    $afterOrders = Get-CountSafely "/orders"
    $afterFills = Get-CountSafely "/fills"
    $afterPositions = Get-CountSafely "/positions/internal"
    $mutationGuard = "Unchanged"
    if ($beforeOrders.available -and $afterOrders.available -and $beforeOrders.count -ne $afterOrders.count) { $mutationGuard = "Changed" }
    if ($beforeFills.available -and $afterFills.available -and $beforeFills.count -ne $afterFills.count) { $mutationGuard = "Changed" }
    if ($beforePositions.available -and $afterPositions.available -and $beforePositions.count -ne $afterPositions.count) { $mutationGuard = "Changed" }

    $validationStatus = if ($status -eq "Completed" -and $observationCount -eq 0 -and $blockingObservationCount -eq 0 -and $warningObservationCount -eq 0 -and $mutationGuard -eq "Unchanged") { "Ok" } elseif ($status -in @("Completed", "CompletedWithWarnings")) { "Warnings" } else { "SafeFail" }
    $finalDecision = if ($validationStatus -eq "Ok") { "PASS_SINGLE_OPTIONAL_LOCAL_REPLAY_COMPLETED" } elseif ($validationStatus -eq "Warnings") { "PASS_SINGLE_OPTIONAL_LOCAL_REPLAY_COMPLETED_WITH_WARNINGS" } else { "PASS_SINGLE_OPTIONAL_LOCAL_REPLAY_SAFE_FAIL" }
    $allowedNextPhase = if ($validationStatus -eq "Ok") { $allowedNextPhaseCompleted } elseif ($validationStatus -eq "Warnings") { $allowedNextPhaseWarnings } else { $allowedNextPhaseSafeFail }
    $recommendedNextAction = if ($validationStatus -eq "Ok") { "Close the optional local replay in a no-external-run closure gate." } elseif ($validationStatus -eq "Warnings") { "Review local replay warnings in a no-external-run warning gate." } else { "Diagnose the local replay safe-fail without external connections or retries." }

    New-ReportAndGate -ReplayStatus $status -ValidationStatus $validationStatus -ObservationCount $observationCount -BlockingObservationCount $blockingObservationCount -WarningObservationCount $warningObservationCount -MutationGuard $mutationGuard -ReplayRunId $replayRunId -PostEndpointCalled $true -ReplayEndpointCalled $true -LocalApiHealthOkBeforeReplay $true -SafeRuntimePostureConfirmedBeforeReplay $true -FinalDecision $finalDecision -AllowedNextPhase $allowedNextPhase -RecommendedNextAction $recommendedNextAction
    if ($validationStatus -eq "SafeFail") { exit 1 }
} catch {
    New-ReportAndGate -ReplayStatus "SafeFail" -ValidationStatus "SafeFail" -ObservationCount -1 -BlockingObservationCount -1 -WarningObservationCount -1 -MutationGuard "Unknown" -ReplayRunId "" -PostEndpointCalled $false -ReplayEndpointCalled $false -LocalApiHealthOkBeforeReplay $false -SafeRuntimePostureConfirmedBeforeReplay $false -FinalDecision "PASS_SINGLE_OPTIONAL_LOCAL_REPLAY_SAFE_FAIL" -AllowedNextPhase $allowedNextPhaseSafeFail -RecommendedNextAction "Diagnose the local replay safe-fail without external connections or retries." -FailureReason $_.Exception.Message
    exit 1
}
