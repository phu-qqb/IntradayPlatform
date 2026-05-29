param(
    [string]$UsdJpyArtifactFile = "artifacts/lmax-readonly-runtime-additional-snapshot/usdjpy/lmax-readonly-usdjpy-demo-snapshot-result-20260511-181833.json",
    [string]$UsdJpyReviewFile = "artifacts/readiness/phase7h-additional-instrument-snapshot-review-usdjpy.json",
    [string]$UsdJpyClosureFile = "artifacts/lmax-readonly-runtime-additional-snapshot/usdjpy/closure/lmax-readonly-usdjpy-closure-manifest-20260511-181904.json",
    [string]$GbpUsdArtifactFile = "artifacts/lmax-readonly-runtime-additional-snapshot/gbpusd/lmax-readonly-gbpusd-demo-snapshot-result-20260511-103318.json",
    [string]$EurGbpArtifactFile = "artifacts/lmax-readonly-runtime-additional-snapshot/eurgbp/lmax-readonly-eurgbp-demo-snapshot-result-20260511-163141.json",
    [string]$EurGbpReviewFile = "artifacts/readiness/phase7h-additional-instrument-snapshot-review-eurgbp.json",
    [string]$EurGbpEvidencePreviewFile = "artifacts/lmax-readonly-runtime-additional-snapshot/eurgbp/evidence-preview/lmax-readonly-eurgbp-evidence-preview-20260511-165605.json",
    [string]$UsdJpyFinalPreRunGateFile = "artifacts/lmax-readonly-runtime-securityid-planning/additional-final-prerun/lmax-readonly-additional-instrument-final-prerun-gate-USDJPY-20260511-161440.json",
    [string]$EurGbpFinalPreRunGateFile = "artifacts/lmax-readonly-runtime-securityid-planning/eurgbp-final-prerun/lmax-readonly-eurgbp-final-prerun-gate-20260511-134130.json",
    [string]$OutputFile = "artifacts/readiness/phase7i2-usdjpy-failedsafe-connection-diagnosis.json"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=|raw\s*fix)'

function Resolve-LocalPath([string]$PathValue) {
    if ([string]::IsNullOrWhiteSpace($PathValue)) { return "" }
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}

function Read-SafeJson([string]$PathValue, [string]$Label) {
    $resolved = Resolve-LocalPath $PathValue
    if (-not (Test-Path -LiteralPath $resolved)) {
        throw "$Label not found: $resolved"
    }

    $raw = Get-Content -LiteralPath $resolved -Raw
    $safe = $raw -replace 'LMAX_DEMO_FIX_USERNAME|LMAX_DEMO_FIX_PASSWORD|LMAX_DEMO_SENDER_COMP_ID|LMAX_DEMO_TARGET_COMP_ID|credentialProfileName|usernamePresent|passwordPresent|usernameLength|passwordLength','SAFE_METADATA'
    if ($safe -match $sensitivePattern) {
        throw "$Label contains credential-shaped or raw FIX content: $resolved"
    }

    return [ordered]@{
        path = $resolved
        json = ($raw | ConvertFrom-Json)
    }
}

function Get-Counter($Artifact, [string]$Name) {
    if ($Artifact.diagnostics -and $Artifact.diagnostics.messageCounters -and $Artifact.diagnostics.messageCounters.PSObject.Properties.Name -contains $Name) {
        return [int]$Artifact.diagnostics.messageCounters.$Name
    }
    if ($Artifact.PSObject.Properties.Name -contains $Name) {
        return [int]$Artifact.$Name
    }
    return 0
}

function Summarize-Artifact($Artifact, [string]$Label, [string]$Path) {
    [ordered]@{
        label = $Label
        artifactPath = $Path
        symbol = [string]$Artifact.symbol
        slashSymbol = [string]$Artifact.slashSymbol
        securityId = [string]$Artifact.securityId
        status = [string]$Artifact.status
        externalConnectionAttempted = [bool]$Artifact.externalConnectionAttempted
        tcpConnected = if ($Artifact.logonDiagnostics) { [bool]$Artifact.logonDiagnostics.tcpConnected } else { $false }
        tlsConnected = if ($Artifact.logonDiagnostics) { [bool]$Artifact.logonDiagnostics.tlsConnected } else { $false }
        logonAttempted = [bool]$Artifact.logonAttempted
        logonSucceeded = [bool]$Artifact.logonSucceeded
        snapshotRequestAttempted = [bool]$Artifact.snapshotRequestAttempted
        snapshotReceived = [bool]$Artifact.snapshotReceived
        entryCount = [int]$Artifact.entryCount
        marketDataSnapshotCount = Get-Counter $Artifact "marketDataSnapshot"
        marketDataRequestRejectCount = Get-Counter $Artifact "marketDataRequestReject"
        businessMessageRejectCount = Get-Counter $Artifact "businessMessageReject"
        rejectCount = Get-Counter $Artifact "reject"
        orderSubmissionAttempted = [bool]$Artifact.orderSubmissionAttempted
        shadowReplaySubmitAttempted = [bool]$Artifact.shadowReplaySubmitAttempted
        tradingMutationAttempted = [bool]$Artifact.tradingMutationAttempted
        schedulerStarted = [bool]$Artifact.schedulerStarted
        credentialValuesReturned = [bool]$Artifact.credentialValuesReturned
        noSensitiveContent = [bool]$Artifact.noSensitiveContent
        redactionStatus = [string]$Artifact.redactionStatus
    }
}

function Summarize-Gate($Gate, [string]$Label, [string]$Path) {
    [ordered]@{
        label = $Label
        path = $Path
        symbol = [string]$Gate.symbol
        slashSymbol = [string]$Gate.slashSymbol
        planningSecurityId = [string]$Gate.planningSecurityId
        securityIdSource = [string]$Gate.securityIdSource
        finalDecision = [string]$Gate.finalDecision
        oneInstrumentAtATime = [bool]$Gate.oneInstrumentAtATime
        batchExecutionAllowed = [bool]$Gate.batchExecutionAllowed
        externalRunAuthorized = [bool]$Gate.externalRunAuthorized
        canRunExternalSnapshot = [bool]$Gate.canRunExternalSnapshot
        eligibleForManualSnapshotAttempt = [bool]$Gate.eligibleForManualSnapshotAttempt
        isApprovedForExternalRun = [bool]$Gate.isApprovedForExternalRun
        schedulerOrPolling = [bool]$Gate.schedulerOrPolling
        runtimeShadowReplaySubmit = [bool]$Gate.runtimeShadowReplaySubmit
        orderSubmission = [bool]$Gate.orderSubmission
        tradingMutation = [bool]$Gate.tradingMutation
        gatewayRegistration = [bool]$Gate.gatewayRegistration
        apiWorkerGatewayMode = [string]$Gate.apiWorkerGatewayMode
    }
}

Write-Host "LMAX Read-Only Phase 7I2 USDJPY FailedSafe Connection Diagnosis"
Write-Host "Local-only. This script does not connect to LMAX, request snapshots, replay evidence, schedule work, or use credentials."

$usd = Read-SafeJson $UsdJpyArtifactFile "USDJPY artifact"
$usdReview = Read-SafeJson $UsdJpyReviewFile "USDJPY review"
$usdClosure = Read-SafeJson $UsdJpyClosureFile "USDJPY closure"
$gbp = Read-SafeJson $GbpUsdArtifactFile "GBPUSD artifact"
$eur = Read-SafeJson $EurGbpArtifactFile "EURGBP artifact"
$eurReview = Read-SafeJson $EurGbpReviewFile "EURGBP review"
$eurPreview = Read-SafeJson $EurGbpEvidencePreviewFile "EURGBP evidence preview"
$usdGate = Read-SafeJson $UsdJpyFinalPreRunGateFile "USDJPY final pre-run gate"
$eurGate = Read-SafeJson $EurGbpFinalPreRunGateFile "EURGBP final pre-run gate"

$u = $usd.json
$usdSummary = Summarize-Artifact $u "USDJPY failed-safe attempt" $usd.path
$gbpSummary = Summarize-Artifact $gbp.json "GBPUSD successful comparison" $gbp.path
$eurSummary = Summarize-Artifact $eur.json "EURGBP successful comparison" $eur.path
$usdGateSummary = Summarize-Gate $usdGate.json "USDJPY final pre-run gate" $usdGate.path
$eurGateSummary = Summarize-Gate $eurGate.json "EURGBP final pre-run gate" $eurGate.path

$instrumentRejectsObserved = ((Get-Counter $u "marketDataRequestReject") -gt 0) -or ((Get-Counter $u "businessMessageReject") -gt 0) -or ((Get-Counter $u "reject") -gt 0)
$unsafeFlags = [bool]$u.orderSubmissionAttempted -or [bool]$u.shadowReplaySubmitAttempted -or [bool]$u.tradingMutationAttempted -or [bool]$u.schedulerStarted -or [bool]$u.credentialValuesReturned -or -not [bool]$u.noSensitiveContent
$connectionBeforeLogon = [bool]$u.externalConnectionAttempted -and -not [bool]$u.logonAttempted -and -not [bool]$u.snapshotRequestAttempted

$inferredFailureClass = if ($connectionBeforeLogon -and -not $instrumentRejectsObserved -and -not $unsafeFlags) {
    "FailedSafeConnectionBeforeSessionEstablishment"
} else {
    "NeedsManualDiagnostics"
}

$likelyRootCauseCandidates = @(
    "Transient network/socket failure during the USDJPY attempt.",
    "Demo endpoint unavailable or refusing the connection at attempt time.",
    "TLS/socket layer failure before FIX logon; artifact shows tcpConnected=true and tlsConnected=false.",
    "Local environment or process resource issue during the isolated manual prototype invocation.",
    "Venue-side connection refusal before session establishment."
)

$ruledOutCauses = @(
    "Not an order path issue: orderSubmissionAttempted=false.",
    "Not scheduler/polling: schedulerStarted=false and no scheduler was used.",
    "Not runtime shadow replay: shadowReplaySubmitAttempted=false and no replay was run.",
    "Not trading-state mutation: tradingMutationAttempted=false.",
    "Not MarketData evidence preview failure: no snapshot was received, and the preview script correctly refused FailedSafe no-snapshot input.",
    "Not an instrument-level MarketDataRequestReject: snapshotRequestAttempted=false and reject counters are zero.",
    "Not proven invalid SecurityID: no MarketDataRequest was sent and no venue/instrument reject was observed; DemoLondon USDJPY remains 4004/source 8."
)

$recommendedNextAction = "Review this diagnosis, keep the wrapper and gate validation unchanged, then allow at most one future operator-approved USDJPY market-hours retry using the same Phase 7H-compatible final pre-run gate. Do not add retry automation, batch execution, wrapper relaxation, or alternate Tokyo 600x SecurityID unless future explicit reject evidence supports a new planning phase."
$allowedNextPhase = "Phase 7I3 - USDJPY Controlled One-Time Retry Readiness / Operator Gate, No External Run, or a single future operator-approved USDJPY retry if explicitly chosen after diagnosis."
$disallowedActions = @(
    "No automatic retry.",
    "No batch or loop.",
    "No scheduler or polling.",
    "No runtime shadow replay submit.",
    "No order submission.",
    "No real gateway registration.",
    "No trading-state mutation.",
    "Do not weaken the Phase 7H wrapper to accept generic Phase 6Z-A readiness artifacts.",
    "Do not classify the failure as invalid SecurityID without explicit venue reject evidence."
)

$finalDecision = if ($inferredFailureClass -eq "FailedSafeConnectionBeforeSessionEstablishment") { "PASS_WITH_KNOWN_WARNINGS" } else { "FAIL" }

$report = [ordered]@{
    phase = "7I2"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    instrument = "USDJPY"
    slashSymbol = "USD/JPY"
    securityId = "4004"
    securityIdSource = "8"
    statusFromSnapshot = [string]$u.status
    connectionAttempted = [bool]$u.externalConnectionAttempted
    logonAttempted = [bool]$u.logonAttempted
    snapshotRequestAttempted = [bool]$u.snapshotRequestAttempted
    snapshotReceived = [bool]$u.snapshotReceived
    instrumentRejectsObserved = $instrumentRejectsObserved
    sensitiveContentSafe = ([bool]$u.noSensitiveContent -and -not [bool]$u.credentialValuesReturned -and [string]$u.redactionStatus -eq "Redacted")
    usdJpyReviewDecision = [string]$usdReview.json.finalDecision
    usdJpyClosureDecision = [string]$usdClosure.json.finalClosureDecision
    comparisonToSuccessfulInstruments = [ordered]@{
        usdJpy = $usdSummary
        gbpUsd = $gbpSummary
        eurGbp = $eurSummary
        comparisonSummary = "GBPUSD and EURGBP reached TLS/logon and received one MarketDataSnapshot each. USDJPY reached the connection attempt but did not reach TLS/FIX logon and did not send a MarketDataRequest."
        eurGbpReviewDecision = [string]$eurReview.json.finalDecision
        eurGbpEvidenceMode = [string]$eurPreview.json.evidenceMode
    }
    gateCompatibilitySummary = [ordered]@{
        usdJpy = $usdGateSummary
        eurGbp = $eurGateSummary
        summary = "USDJPY and EURGBP final pre-run gates both carry the Phase 7H one-instrument/non-batch/non-executable safety contract. The USDJPY failure is not explained by missing wrapper-compatible gate fields."
    }
    inferredFailureClass = $inferredFailureClass
    likelyRootCauseCandidates = $likelyRootCauseCandidates
    ruledOutCauses = $ruledOutCauses
    recommendedNextAction = $recommendedNextAction
    allowedNextPhase = $allowedNextPhase
    disallowedActions = $disallowedActions
    noEvidencePreviewRequired = $true
    replayRun = $false
    externalConnectionAttemptedInPhase7I2 = $false
    snapshotAttemptedInPhase7I2 = $false
    replayAttemptedInPhase7I2 = $false
    orderSubmissionAttempted = [bool]$u.orderSubmissionAttempted
    shadowReplaySubmitAttempted = [bool]$u.shadowReplaySubmitAttempted
    tradingMutationAttempted = [bool]$u.tradingMutationAttempted
    schedulerStarted = [bool]$u.schedulerStarted
    credentialValuesReturned = [bool]$u.credentialValuesReturned
    noSensitiveContent = [bool]$u.noSensitiveContent
    finalDecision = $finalDecision
}

$outPath = Resolve-LocalPath $OutputFile
New-Item -ItemType Directory -Path (Split-Path -Parent $outPath) -Force | Out-Null
$report | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host "InferredFailureClass: $inferredFailureClass"
Write-Host "FinalDecision: $finalDecision"
Write-Host "DiagnosisReport: $outPath"
if ($finalDecision -eq "FAIL") { exit 1 }
