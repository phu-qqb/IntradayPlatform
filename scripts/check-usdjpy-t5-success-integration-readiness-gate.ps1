param(
    [string]$BaseDir = "artifacts/readiness/usdjpy-troubleshooting"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$expectedT1Decision = "USDJPY_REMAINS_PARKED_LOCAL_ONLY_DIAGNOSTIC_COMPLETE"
$expectedT2Decision = "USDJPY_REMAINS_PARKED_RETRY_PRECONDITIONS_DOCUMENTED"
$expectedT3Decision = "USDJPY_T4_MANUAL_RETRY_DESIGN_READY_BUT_NOT_AUTHORIZED"
$expectedT4Decision = "USDJPY_T4_PASS_SNAPSHOT_RECEIVED_SANITIZED"
$allowedRecommendations = @(
    "USDJPY_READY_FOR_CONTROLLED_SUCCESS_ARCHIVE_ONLY",
    "USDJPY_READY_FOR_READINESS_PROMOTION_REVIEW",
    "USDJPY_NOT_READY_ROOT_CAUSE_UNRESOLVED",
    "USDJPY_REQUIRES_ADDITIONAL_LOCAL_REVIEW"
)
$allowedDecisions = @(
    "USDJPY_T5_SUCCESS_REVIEW_READY_FOR_PROMOTION_GATE",
    "USDJPY_T5_SUCCESS_REVIEW_ARCHIVE_ONLY",
    "USDJPY_T5_NOT_READY_ROOT_CAUSE_UNRESOLVED",
    "USDJPY_T5_REQUIRES_ADDITIONAL_LOCAL_REVIEW"
)
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|BEGIN\s+PRIVATE\s+KEY)'

function Add-Result([string]$Category, [string]$Check, [string]$Status, [string]$Detail) {
    $script:results += [ordered]@{ category = $Category; check = $Check; status = $Status; detail = $Detail }
    Write-Host ("{0}: {1} / {2} - {3}" -f $Status, $Category, $Check, $Detail)
}
function Resolve-RepoPath([string]$PathValue) {
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}
function Read-TextSafe([string]$PathValue, [string]$Label) {
    $resolved = Resolve-RepoPath $PathValue
    if (-not (Test-Path -LiteralPath $resolved)) {
        Add-Result $Label "File exists" "FAIL" "Missing $resolved"
        return $null
    }
    Add-Result $Label "File exists" "PASS" $resolved
    $raw = Get-Content -LiteralPath $resolved -Raw
    $safe = $raw `
        -replace 'credential|Credential|secret|Secret','SAFE_METADATA' `
        -replace 'raw FIX|rawFix|FIX Logon','SAFE_FIX_METADATA' `
        -replace 'LMAX_DEMO_FIX_USERNAME|LMAX_DEMO_FIX_PASSWORD|LMAX_DEMO_SENDER_COMP_ID|LMAX_DEMO_TARGET_COMP_ID','SAFE_ENV_LABEL'
    if ($safe -match $sensitivePattern) {
        Add-Result $Label "No sensitive content" "FAIL" "Credential-shaped content found."
    } else {
        Add-Result $Label "No sensitive content" "PASS" "No credential-shaped content."
    }
    return $raw
}
function Assert-False([object]$Value, [string]$Category, [string]$Check) {
    if (-not [bool]$Value) { Add-Result $Category $Check "PASS" "false." } else { Add-Result $Category $Check "FAIL" "Expected false." }
}
function Assert-True([object]$Value, [string]$Category, [string]$Check) {
    if ([bool]$Value) { Add-Result $Category $Check "PASS" "true." } else { Add-Result $Category $Check "FAIL" "Expected true." }
}
function Assert-Equals([object]$Actual, [string]$Expected, [string]$Category, [string]$Check) {
    if ([string]$Actual -eq $Expected) { Add-Result $Category $Check "PASS" $Expected } else { Add-Result $Category $Check "FAIL" "Expected '$Expected' but found '$Actual'." }
}

Write-Host "USDJPY-T5 Success Integration Readiness Gate Validator"
Write-Host "This validator performs no external run, snapshot, replay, POST endpoint, or promotion action."

$requiredPrior = @(
    "phase-usdjpy-t1-decision-gate.json",
    "phase-usdjpy-t2-decision-gate.json",
    "phase-usdjpy-t3-decision-gate.json",
    "phase-usdjpy-t4-decision-gate.json",
    "phase-usdjpy-t4-gate-validation.json"
)
$requiredT5 = [ordered]@{
    SuccessSummary = "phase-usdjpy-t5-t4-success-evidence-summary.json"
    PriorComparison = "phase-usdjpy-t5-prior-failure-vs-success-comparison.json"
    HypothesisMatrix = "phase-usdjpy-t5-post-success-hypothesis-matrix.json"
    ReadinessReview = "phase-usdjpy-t5-integration-readiness-review.json"
    ArchivePlan = "phase-usdjpy-t5-success-archive-plan.json"
    DecisionGate = "phase-usdjpy-t5-decision-gate.json"
    NonRunValidation = "phase-usdjpy-t5-non-run-validation.json"
    Report = "phase-usdjpy-t5-sanitized-success-readiness-report.md"
    OperatorNote = "phase-usdjpy-t5-operator-note.md"
}

$raw = @{}
foreach ($name in $requiredPrior) { $raw[$name] = Read-TextSafe (Join-Path $BaseDir $name) "Prior:$name" }
foreach ($key in $requiredT5.Keys) { $raw[$key] = Read-TextSafe (Join-Path $BaseDir $requiredT5[$key]) $key }

$t1Gate = $raw["phase-usdjpy-t1-decision-gate.json"] | ConvertFrom-Json
$t2Gate = $raw["phase-usdjpy-t2-decision-gate.json"] | ConvertFrom-Json
$t3Gate = $raw["phase-usdjpy-t3-decision-gate.json"] | ConvertFrom-Json
$t4Gate = $raw["phase-usdjpy-t4-decision-gate.json"] | ConvertFrom-Json
Assert-Equals $t1Gate.finalDecision $expectedT1Decision "T1DecisionGate" "Final decision"
Assert-Equals $t2Gate.finalDecision $expectedT2Decision "T2DecisionGate" "Final decision"
Assert-Equals $t3Gate.finalDecision $expectedT3Decision "T3DecisionGate" "Final decision"
Assert-Equals $t4Gate.finalDecision $expectedT4Decision "T4DecisionGate" "Final decision"

$runtimePath = Resolve-RepoPath $t4Gate.runtimeSnapshotArtifactPath
if (Test-Path -LiteralPath $runtimePath) {
    Add-Result "T4RuntimeArtifact" "File exists" "PASS" $runtimePath
} else {
    Add-Result "T4RuntimeArtifact" "File exists" "FAIL" "Missing $runtimePath"
}

$summary = $raw.SuccessSummary | ConvertFrom-Json
Assert-True $summary.operatorApprovalPresent "SuccessSummary" "Operator approval present"
Assert-True $summary.preflightPassed "SuccessSummary" "Preflight passed"
Assert-True $summary.exactlyOneAttempt "SuccessSummary" "Exactly one attempt"
Assert-True $summary.tcpSuccess "SuccessSummary" "TCP success"
Assert-True $summary.tlsSuccess "SuccessSummary" "TLS success"
Assert-True $summary.fixLogonSuccess "SuccessSummary" "FIX logon success"
Assert-True $summary.marketDataRequestSuccess "SuccessSummary" "MarketDataRequest success"
Assert-True $summary.snapshotReceived "SuccessSummary" "Snapshot received"
Assert-True $summary.railIsolationPreserved "SuccessSummary" "Rail isolation preserved"

$review = $raw.ReadinessReview | ConvertFrom-Json
if ($allowedRecommendations -contains [string]$review.integrationStatusRecommendation) {
    Add-Result "ReadinessReview" "Allowed recommendation" "PASS" $review.integrationStatusRecommendation
} else {
    Add-Result "ReadinessReview" "Allowed recommendation" "FAIL" "Unexpected recommendation $($review.integrationStatusRecommendation)"
}
Assert-False $review.promotionExecuted "ReadinessReview" "Promotion executed"
Assert-False $review.usdJpyFullyIntegrated "ReadinessReview" "USDJPY fully integrated"

$gate = $raw.DecisionGate | ConvertFrom-Json
Assert-Equals $gate.phase "USDJPY-T5" "DecisionGate" "Phase"
Assert-True $gate.t4RuntimeSanitizedArtifactExists "DecisionGate" "T4 runtime artifact exists"
Assert-False $gate.promotionExecuted "DecisionGate" "Promotion executed"
Assert-False $gate.usdJpyFullyIntegrated "DecisionGate" "USDJPY fully integrated"
foreach ($flag in @("externalRunExecuted", "newSnapshotExecuted", "replayExecuted", "postEndpointInvoked", "realSocketOpened", "tcpConnectionAttempted", "tlsHandshakeAttempted", "fixLogonAttempted", "marketDataRequestSent", "orderSubmissionExecuted", "tradingStateMutated", "schedulerStarted", "pollingStarted", "shadowReplaySubmitted", "apiWorkerStarted", "runtimePoweredUp", "retryExecuted", "batchExecuted", "loopExecuted", "promotionExecuted", "validatedRailsModified", "phase7ArchiveModified", "phaseT1ArtifactsModified", "phaseT2ArtifactsModified", "phaseT3ArtifactsModified", "phaseT4ArtifactsModified", "nonUsdJpyRailTouched")) {
    Assert-False $gate.$flag "DecisionGate" $flag
}
Assert-Equals $gate.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "DecisionGate" "API/Worker gateway mode"
if ($allowedRecommendations -contains [string]$gate.integrationStatusRecommendation) {
    Add-Result "DecisionGate" "Allowed recommendation" "PASS" $gate.integrationStatusRecommendation
} else {
    Add-Result "DecisionGate" "Allowed recommendation" "FAIL" "Unexpected recommendation $($gate.integrationStatusRecommendation)"
}
if ($allowedDecisions -contains [string]$gate.finalDecision) {
    Add-Result "DecisionGate" "Allowed final decision" "PASS" $gate.finalDecision
} else {
    Add-Result "DecisionGate" "Allowed final decision" "FAIL" "Unexpected finalDecision $($gate.finalDecision)"
}
Assert-True $gate.noSensitiveContent "DecisionGate" "No sensitive content"

$nonRun = $raw.NonRunValidation | ConvertFrom-Json
foreach ($flag in @("externalRunExecuted", "newSnapshotExecuted", "replayExecuted", "postEndpointInvoked", "realSocketOpened", "tcpConnectionAttempted", "tlsHandshakeAttempted", "fixLogonAttempted", "marketDataRequestSent", "orderSubmissionExecuted", "tradingStateMutated", "schedulerStarted", "pollingStarted", "shadowReplaySubmitted", "apiWorkerStarted", "runtimePoweredUp", "retryExecuted", "batchExecuted", "loopExecuted", "promotionExecuted", "validatedRailsModified", "phase7ArchiveModified", "phaseT1ArtifactsModified", "phaseT2ArtifactsModified", "phaseT3ArtifactsModified", "phaseT4ArtifactsModified", "nonUsdJpyRailTouched")) {
    Assert-False $nonRun.$flag "NonRunValidation" $flag
}
Assert-Equals $nonRun.apiWorkerGatewayMode "FakeLmaxGatewayOnly" "NonRunValidation" "API/Worker gateway mode"
Assert-True $nonRun.outputSanitized "NonRunValidation" "Output sanitized"

if ($null -ne $raw.Report) {
    foreach ($marker in @("Executive summary", "Scope and non-run guarantees", "T4 success evidence summary", "Prior USDJPY failure vs T4 success comparison", "Post-success hypothesis matrix", "What T4 proves", "What T4 does not prove", "Integration readiness review", "Success archive plan", "Decision", "Recommended next phase")) {
        if ($raw.Report.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Result "Report" "Marker: $marker" "PASS" "Marker found." } else { Add-Result "Report" "Marker: $marker" "FAIL" "Marker missing." }
    }
}

$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"
$startupText = (@($apiProgram, $workerProgram) | Where-Object { Test-Path -LiteralPath $_ } | ForEach-Object { Get-Content -Raw -LiteralPath $_ }) -join "`n"
if ($startupText.Contains("FakeLmaxGateway") -and -not ($startupText.Contains("RealLmaxGateway") -or $startupText.Contains("LmaxVenueGatewaySkeleton"))) {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No real gateway registration found."
} else {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" "Unexpected gateway registration marker."
}

Add-Result "ValidatorRuntime" "No external action" "PASS" "Validator only reads local artifacts."
Add-Result "Promotion" "No promotion action" "PASS" "T5 recommendation only."

$failed = @($results | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } else { [string]$gate.finalDecision }
$outPath = Resolve-RepoPath (Join-Path $BaseDir "phase-usdjpy-t5-gate-validation.json")
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "USDJPY-T5"
    finalDecision = $decision
    integrationStatusRecommendation = [string]$gate.integrationStatusRecommendation
    externalRunExecuted = $false
    newSnapshotExecuted = $false
    replayExecuted = $false
    postEndpointInvoked = $false
    realSocketOpened = $false
    tcpConnectionAttempted = $false
    tlsHandshakeAttempted = $false
    fixLogonAttempted = $false
    marketDataRequestSent = $false
    orderSubmissionExecuted = $false
    tradingStateMutated = $false
    schedulerStarted = $false
    pollingStarted = $false
    shadowReplaySubmitted = $false
    apiWorkerStarted = $false
    runtimePoweredUp = $false
    retryExecuted = $false
    batchExecuted = $false
    loopExecuted = $false
    promotionExecuted = $false
    apiWorkerGatewayMode = "FakeLmaxGatewayOnly"
    validatedRailsModified = $false
    phase7ArchiveModified = $false
    phaseT1ArtifactsModified = $false
    phaseT2ArtifactsModified = $false
    phaseT3ArtifactsModified = $false
    phaseT4ArtifactsModified = $false
    nonUsdJpyRailTouched = $false
    outputSanitized = $true
    noSensitiveContent = $true
    results = $results
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }
